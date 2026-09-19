using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// Cozmo's emotion axes. Anki's own, from the decompiled
/// <c>unity/scripts/csharp/Anki.Cozmo/EmotionType.cs</c>; the trailing <c>Count</c> sentinel is left out.
/// </summary>
public enum EmotionType : byte
{
    Happy, Calm, Brave, Confident, Charged, Excited, Social, Winning, WantToPlay,
}

/// <summary>One emotion axis moving by one amount. The unit of change in the shipped model.</summary>
public sealed record EmotionAffector(EmotionType Emotion, double Value);

/// <summary>A named thing that happens, and what it does to the mood.</summary>
public sealed record EmotionEvent(string Name, IReadOnlyList<EmotionAffector> Affectors);

/// <summary>
/// How an emotion fades. The shipped config gives a piecewise-linear curve of seconds since the last
/// change against a multiplier applied to the value, so <c>default</c> reaches zero after 150 seconds.
/// </summary>
public sealed record DecayGraph(string EmotionType, IReadOnlyList<(double Seconds, double Multiplier)> Nodes)
{
    /// <summary>The multiplier at a given age, interpolated between nodes and flat outside them.</summary>
    public double At(double seconds)
    {
        if (Nodes.Count == 0) return 1;
        if (seconds <= Nodes[0].Seconds) return Nodes[0].Multiplier;
        for (int i = 1; i < Nodes.Count; i++)
        {
            var (x1, y1) = Nodes[i];
            var (x0, y0) = Nodes[i - 1];
            if (seconds > x1) continue;
            double span = x1 - x0;
            return span <= 0 ? y1 : y0 + (y1 - y0) * ((seconds - x0) / span);
        }
        return Nodes[^1].Multiplier;
    }
}

/// <summary>
/// Cozmo's mood model, loaded from the shipped configuration.
///
/// **An important finding first, because it bounds what this is for.** In this build the mood system does
/// **not** change which animation is chosen. Every one of the 1047 entries across the shipped
/// <c>animationGroups</c> assets carries <c>"Mood": "Default"</c> — there is not a single mood-specific
/// alternative anywhere. Selection is therefore mood-invariant no matter what the mood is, and nothing
/// here pretends otherwise. Where mood does bear on behaviour is in gating which *behaviours* may run
/// (the decompiled <c>CurrentMoodCondition</c> tests a <c>SimpleMoodType</c> against a min and max), and
/// that belongs with the behaviour system rather than here.
///
/// So this class recovers the model as data — the axes, the events that move them, and the decay curves —
/// and stops there. It is deliberately not a general emotion simulator.
///
/// Sources: <c>config/engine/mood_config.json</c> for the decay graphs, and the eleven files under
/// <c>config/engine/emotionevents/</c> for the events. Both are JSON with C-style comments, which the
/// loader tolerates because the shipped files contain them.
/// </summary>
public sealed class MoodModel
{
    private readonly Dictionary<string, EmotionEvent> _events = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DecayGraph> _decay = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every emotion event the shipped configuration defines.</summary>
    public IReadOnlyCollection<EmotionEvent> Events => _events.Values;
    /// <summary>The decay curves, by emotion name. <c>default</c> applies to any emotion without its own.</summary>
    public IReadOnlyCollection<DecayGraph> DecayGraphs => _decay.Values;

    /// <summary>Names in the shipped files that are not emotions this build knows.</summary>
    public IReadOnlyList<string> UnknownEmotions { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Loads the model from an OBB directory, or any directory containing <c>mood_config.json</c> and an
    /// <c>emotionevents</c> directory at some depth.
    /// </summary>
    public static MoodModel Load(string root)
    {
        var model = new MoodModel();
        var unknown = new List<string>();

        var config = FindFile(root, "mood_config.json");
        if (config is not null)
        {
            using var doc = JsonDocument.Parse(Decomment(File.ReadAllText(config)));
            if (doc.RootElement.TryGetProperty("decayGraphs", out var graphs))
                foreach (var g in graphs.EnumerateArray())
                {
                    var name = g.TryGetProperty("emotionType", out var t) ? t.GetString() : null;
                    if (name is null || !g.TryGetProperty("nodes", out var nodes)) continue;
                    var pts = new List<(double, double)>();
                    foreach (var n in nodes.EnumerateArray())
                        if (n.TryGetProperty("x", out var x) && n.TryGetProperty("y", out var y))
                            pts.Add((x.GetDouble(), y.GetDouble()));
                    pts.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                    model._decay[name] = new DecayGraph(name, pts);
                }
        }

        foreach (var file in FindEventFiles(root))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(Decomment(File.ReadAllText(file))); }
            catch (JsonException) { continue; }
            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("emotionEvents", out var evs)) continue;
                foreach (var e in evs.EnumerateArray())
                {
                    var name = e.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (name is null) continue;
                    var affectors = new List<EmotionAffector>();
                    if (e.TryGetProperty("emotionAffectors", out var aff))
                        foreach (var a in aff.EnumerateArray())
                        {
                            var et = a.TryGetProperty("emotionType", out var t) ? t.GetString() : null;
                            if (et is null) continue;
                            if (!Enum.TryParse<EmotionType>(et, ignoreCase: true, out var emotion))
                            { unknown.Add(et); continue; }
                            double v = a.TryGetProperty("value", out var val) ? val.GetDouble() : 0;
                            affectors.Add(new EmotionAffector(emotion, v));
                        }
                    model._events[name] = new EmotionEvent(name, affectors);
                }
            }
        }
        model.UnknownEmotions = unknown.Distinct().ToList();
        return model;
    }

    /// <summary>Strips <c>//</c> comments, which the shipped config files contain and JSON does not allow.</summary>
    internal static string Decomment(string json) =>
        Regex.Replace(json, @"//[^\n\r]*", "", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static string? FindFile(string root, string name)
    {
        if (!Directory.Exists(root)) return null;
        var direct = Path.Combine(root, name);
        if (File.Exists(direct)) return direct;
        return Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
    }

    private static IEnumerable<string> FindEventFiles(string root)
    {
        if (!Directory.Exists(root)) yield break;
        foreach (var dir in Directory.EnumerateDirectories(root, "emotionevents", SearchOption.AllDirectories))
            foreach (var f in Directory.EnumerateFiles(dir, "*.json"))
                yield return f;
    }

    /// <summary>The named event, or null when the shipped configuration does not define it.</summary>
    public EmotionEvent? Event(string name) => _events.TryGetValue(name, out var e) ? e : null;

    /// <summary>The decay curve for an emotion, falling back to <c>default</c> as the engine's config does.</summary>
    public DecayGraph? DecayFor(EmotionType emotion) =>
        _decay.TryGetValue(emotion.ToString(), out var g) ? g
        : _decay.TryGetValue("default", out var d) ? d : null;
}

/// <summary>
/// A mood: a value per emotion axis, moved by named events and faded by the shipped decay curves.
///
/// Time is taken rather than read, as everywhere else in this stack, so a mood can be wound forward in a
/// test without waiting. Values are clamped to [-1, 1], which is the range the shipped affectors and the
/// decompiled <c>CurrentMoodCondition</c> range attribute both imply.
/// </summary>
public sealed class MoodState
{
    private readonly MoodModel _model;
    private readonly double[] _values = new double[Enum.GetValues<EmotionType>().Length];
    private readonly double[] _lastChangeSec = new double[Enum.GetValues<EmotionType>().Length];
    private readonly double[] _atLastChange = new double[Enum.GetValues<EmotionType>().Length];

    public MoodState(MoodModel model) => _model = model;

    /// <summary>The current value of one axis, after decay up to the last <see cref="Advance"/>.</summary>
    public double this[EmotionType e] => _values[(int)e];

    /// <summary>Applies a named event. Unknown names change nothing and report false.</summary>
    public bool Trigger(string eventName, double nowSec)
    {
        var e = _model.Event(eventName);
        if (e is null) return false;
        foreach (var a in e.Affectors)
        {
            int i = (int)a.Emotion;
            _values[i] = Math.Clamp(_values[i] + a.Value, -1, 1);
            _atLastChange[i] = _values[i];
            _lastChangeSec[i] = nowSec;
        }
        return true;
    }

    /// <summary>Fades every axis according to its decay curve.</summary>
    public void Advance(double nowSec)
    {
        for (int i = 0; i < _values.Length; i++)
        {
            if (_atLastChange[i] == 0) { _values[i] = 0; continue; }
            var graph = _model.DecayFor((EmotionType)i);
            double mult = graph?.At(nowSec - _lastChangeSec[i]) ?? 1;
            _values[i] = Math.Clamp(_atLastChange[i] * mult, -1, 1);
        }
    }

    /// <summary>Every axis and its current value, for diagnostics.</summary>
    public IReadOnlyDictionary<EmotionType, double> Snapshot() =>
        Enum.GetValues<EmotionType>().ToDictionary(e => e, e => _values[(int)e]);
}
