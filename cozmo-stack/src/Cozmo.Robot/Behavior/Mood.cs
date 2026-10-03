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
public sealed record EmotionEvent(string Name, IReadOnlyList<EmotionAffector> Affectors,
                                 DecayGraph? RepetitionPenalty = null);

/// <summary>
/// How an emotion fades. The shipped config gives a piecewise-linear curve of seconds since the last
/// change against a multiplier applied to the value, so <c>default</c> reaches zero after 150 seconds.
/// </summary>
public sealed record DecayGraph(string EmotionType, IReadOnlyList<(double Seconds, double Multiplier)> Nodes)
{
    /// <summary>
    /// The multiplier at a given age: linear between nodes, flat outside them.
    ///
    /// <c>Anki::Util::GraphEvaluator2d::EvaluateY</c> at 0x00804BD0 is the whole rule. Below the first
    /// node it returns that node's y (0x00804BE2), above the last it returns the last node's y (the loop
    /// at 0x00804C00 falling through to 0x00804C3C), a graph of fewer than two nodes is its first node's
    /// y whatever the x, and between two nodes it interpolates unless they are closer than 1e-5 apart in
    /// x, in which case it returns the left node's y.
    /// </summary>
    // fidelity: M8-003
    public double At(double seconds) => GraphEvaluator.EvaluateY(Nodes, seconds, empty: 1);
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
// fidelity: M7-012
public sealed class MoodModel
{
    private readonly Dictionary<string, EmotionEvent> _events = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DecayGraph> _decay = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string ActionType, string ResultCategory), string> _actionResultEvents = new();

    /// <summary>Every emotion event the shipped configuration defines.</summary>
    public IReadOnlyCollection<EmotionEvent> Events => _events.Values;
    /// <summary>The decay curves, by emotion name. <c>default</c> applies to any emotion without its own.</summary>
    public IReadOnlyCollection<DecayGraph> DecayGraphs => _decay.Values;

    /// <summary>
    /// <c>mood_config.json</c>'s <c>defaultRepetitionPenalty</c> graph, evaluated at the seconds since an
    /// event last fired (<c>StaticMoodData</c>+0x78; <c>UpdateEventTimeAndCalculateRepetitionPenalty</c>
    /// 0x0067bedc). Null when the shipped file has none.
    /// </summary>
    public DecayGraph? DefaultRepetitionPenalty { get; private set; }

    /// <summary>
    /// <c>mood_config.json</c>'s <c>actionResultEmotionEvents</c>: <c>(RobotActionType, ActionResultCategory)</c>
    /// -> emotion-event name (<c>LoadActionCompletedEventMap</c> 0x0067afb4 stores it at MoodManager+0x134).
    /// The shipped <c>eventMapper.emotionEvents</c> array is empty, so this map and the emotionevents files
    /// are the only places an event name comes from.
    /// </summary>
    public IReadOnlyDictionary<(string ActionType, string ResultCategory), string> ActionResultEvents => _actionResultEvents;

    /// <summary>Names in the shipped files that are not emotions this build knows.</summary>
    public IReadOnlyList<string> UnknownEmotions { get; private set; } = Array.Empty<string>();

    /// <summary>Adds or replaces a decay curve, for a model built without the shipped files.</summary>
    public void AddDecayGraph(DecayGraph graph) => _decay[graph.EmotionType] = graph;

    /// <summary>Adds or replaces an emotion event, for a model built without the shipped files.</summary>
    public void AddEvent(EmotionEvent e) => _events[e.Name] = e;

    /// <summary>Adds or replaces an action-result -> event mapping, for a model built without the shipped files.</summary>
    public void AddActionResultEvent(string actionType, string resultCategory, string eventName) =>
        _actionResultEvents[(actionType, resultCategory)] = eventName;

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

            // StaticMoodData::ReadFromJson 0x0067ce6c: after decayGraphs, defaultRepetitionPenalty
            // (GraphEvaluator2d::ReadFromJson 0x0067d128) and eventMapper; MoodManager::Init 0x0067aebc
            // then LoadActionCompletedEventMap 0x0067afb4 reads actionResultEmotionEvents.
            if (doc.RootElement.TryGetProperty("defaultRepetitionPenalty", out var dp))
                model.DefaultRepetitionPenalty = ReadGraph("defaultRepetitionPenalty", dp);
            if (doc.RootElement.TryGetProperty("actionResultEmotionEvents", out var ar) &&
                ar.ValueKind == JsonValueKind.Object)
                foreach (var action in ar.EnumerateObject())
                    foreach (var result in action.Value.EnumerateObject())
                        if (result.Value.ValueKind == JsonValueKind.String)
                            model._actionResultEvents[(action.Name, result.Name)] = result.Value.GetString()!;
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

    private static DecayGraph? ReadGraph(string name, JsonElement e)
    {
        if (!e.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array) return null;
        var pts = new List<(double, double)>();
        foreach (var n in nodes.EnumerateArray())
            if (n.TryGetProperty("x", out var x) && n.TryGetProperty("y", out var y))
                pts.Add((x.GetDouble(), y.GetDouble()));
        pts.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return pts.Count == 0 ? null : new DecayGraph(name, pts);
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
// fidelity: M7-012, M7-013, M7-020
public sealed class MoodState
{
    /// <summary>
    /// A change smaller than this does not restart the decay clock. <c>Emotion::Add</c> at 0x00679618
    /// compares the magnitude of the change against 0.05 (the literal at 0x0067967E).
    /// </summary>
    public const double DecayResetThreshold = 0.05;

    /// <summary>
    /// The elapsed time returned for an event's first trigger: FLT_MAX, <c>3.4028235e38</c>
    /// (<c>UpdateLatestEventTimeAndGetTimeElapsedInSeconds</c> 0x0067be48).
    /// </summary>
    public const double NoPreviousEventSec = 3.4028235e38;

    private readonly MoodModel _model;
    private readonly double[] _values = new double[Enum.GetValues<EmotionType>().Length];

    /// <summary>
    /// The engine's own per-emotion decay clock, <c>Emotion</c> this+0x1C. It is not simply the time
    /// since the last change: see <see cref="Trigger"/>.
    /// </summary>
    private readonly double[] _decaySec = new double[Enum.GetValues<EmotionType>().Length];
    private double _lastAdvanceSec;

    /// <summary>MoodManager+0x120: the last trigger time of each event name, for the repetition penalty.</summary>
    private readonly Dictionary<string, double> _lastEventSec = new(StringComparer.Ordinal);

    /// <summary>MoodManager+0x140: action ids whose completion must not raise a mood event.</summary>
    private readonly HashSet<string> _completionDisabled = new(StringComparer.Ordinal);

    public MoodState(MoodModel model) => _model = model;

    /// <summary>The current value of one axis, after decay up to the last <see cref="Advance"/>.</summary>
    public double this[EmotionType e] => _values[(int)e];

    /// <summary>
    /// Applies a named event. Unknown names change nothing and report false.
    ///
    /// <c>Emotion::Add</c> at 0x00679618 clamps the sum to [-1, 1] and then decides whether to zero the
    /// decay clock at this+0x1C (<c>str.w ip,[r0,#0x1c]</c> at 0x006796BC). The clock is zeroed in two
    /// cases:
    ///
    /// <list type="bullet">
    /// <item><b>the value changed sign</b> - <c>teq</c> of (old >= 0) against (clamped new >= 0) at
    /// 0x006796A8, then <c>bne #0x6796bc</c> at 0x006796AC jumps straight to the reset; or</item>
    /// <item>the sign was kept and <b>all</b> of: the change is larger than
    /// <see cref="DecayResetThreshold"/> in magnitude (0x0067968A), and it pushes the value further from
    /// zero rather than back towards it - the <c>eor</c> of (old >= 0) against (delta >= 0) at
    /// 0x006796AE.</item>
    /// </list>
    ///
    /// So a sign flip always restarts the decay, whatever its size; a small nudge, or one that pulls an
    /// emotion back towards neutral without crossing zero, moves the value but leaves it decaying on the
    /// schedule it was already on. This stack used to restart the clock on every affector.
    /// </summary>
    public bool Trigger(string eventName, double nowSec)
    {
        var e = _model.Event(eventName);
        if (e is null) return false;
        Advance(nowSec);
        double penalty = RepetitionPenalty(e, eventName, nowSec);
        foreach (var a in e.Affectors)
        {
            int i = (int)a.Emotion;
            double delta = penalty * a.Value;
            double old = _values[i];
            double updated = Math.Clamp(old + delta, -1, 1);
            _values[i] = updated;

            bool keptItsSign = old >= 0 == updated >= 0;
            bool awayFromZero = old >= 0 == delta >= 0;
            // 0x006796A8/0x006796AC: a sign flip jumps straight to the reset; otherwise the three tests.
            if (!keptItsSign || (Math.Abs(delta) > DecayResetThreshold && awayFromZero)) _decaySec[i] = 0;
        }
        return true;
    }

    /// <summary>
    /// <c>MoodManager::TriggerEmotionEvent</c> 0x0067b85c: the elapsed time from
    /// <c>UpdateLatestEventTimeAndGetTimeElapsedInSeconds</c> 0x0067be48, then the event's own
    /// <c>EmotionEvent::CalculateRepetitionPenalty</c> 0x00679bb8 (<c>GraphEvaluator2d::EvaluateY</c> on
    /// the graph at <c>EmotionEvent</c>+0x18). The shipped events carry no repetition graph
    /// (<c>EmotionEvent::ReadFromJson</c> 0x00679bc0 clears it), and an empty graph evaluates to 1, so
    /// every shipped event applies its affectors at full strength; a first trigger uses FLT_MAX and so
    /// also lands on the graph's final value. The elapsed time is stamped on every trigger.
    /// </summary>
    private double RepetitionPenalty(EmotionEvent e, string eventName, double nowSec)
    {
        double elapsed;
        if (_lastEventSec.TryGetValue(eventName, out var last)) elapsed = nowSec - last;
        else elapsed = NoPreviousEventSec;
        _lastEventSec[eventName] = nowSec;
        return e.RepetitionPenalty?.At(elapsed) ?? 1.0;
    }

    /// <summary>
    /// <c>MoodManager::SetEnableMoodEventOnCompletion</c> / the set at +0x140 that
    /// <c>HandleActionEnded</c> checks (0x0067b318).
    /// </summary>
    public void SetMoodEventOnCompletionEnabled(string actionId, bool enabled)
    {
        if (enabled) _completionDisabled.Remove(actionId);
        else _completionDisabled.Add(actionId);
    }

    /// <summary>
    /// <c>MoodManager::HandleActionEnded</c> 0x0067b318: an action id in the disabled set at +0x140 is
    /// erased and produces no event; otherwise the <c>(actionType, resultCategory)</c> map at +0x134 is
    /// looked up and its event triggered. The stack has no <c>ActionList</c> action-ended callback, so
    /// nothing calls this yet (the record's unresolved); the behaviours raise their named events directly
    /// through <see cref="Trigger"/>.
    /// </summary>
    public bool HandleActionEnded(string actionType, string resultCategory, string actionId, double nowSec)
    {
        if (_completionDisabled.Remove(actionId)) return false;
        if (!_model.ActionResultEvents.TryGetValue((actionType, resultCategory), out var name)) return false;
        return Trigger(name, nowSec);
    }

    /// <summary>
    /// <c>MoodManager::SendEmotionsToGame</c> 0x0067b724 / gap1 G1: the nine values at
    /// <c>+0x18 + 0x20*i</c>, in <see cref="EmotionType"/> order. The engine no-ops when the external
    /// interface at +0x12c is null and otherwise builds and broadcasts a MoodState message.
    /// <see cref="EmotionsBroadcast"/> is this stack's local seam; the app-facing wire message is not
    /// wired (the record's unresolved).
    /// </summary>
    public IReadOnlyList<double> EmotionValues() =>
        Enum.GetValues<EmotionType>().Select(e => _values[(int)e]).ToArray();

    /// <summary>Raised with the nine values when <see cref="SendEmotionsToGame"/> is called.</summary>
    public event Action<IReadOnlyList<double>>? EmotionsBroadcast;

    /// <summary>Sends the nine emotion values out through the stack's seam.</summary>
    public void SendEmotionsToGame() => EmotionsBroadcast?.Invoke(EmotionValues());

    /// <summary>
    /// Fades every axis.
    ///
    /// <c>Emotion::Update</c> at 0x006795A4 does not read the curve at the age and multiply the value it
    /// had when it last changed; it multiplies the current value by the <b>ratio</b> of the curve at the
    /// new decay time to the curve at the old one when the old reading is above 1e-5, and by the <b>raw
    /// new reading</b> when it is not (<c>vmul.f32 s0,s2,s0</c> at 0x006795F8 under the <c>it gt</c> at
    /// 0x006795EE, the 1e-5 literal at 0x006795D6). Over a run of updates that telescopes to the same
    /// thing while the value is untouched, and differs the moment a change leaves the clock running -
    /// which is exactly what <see cref="Trigger"/> arranges.
    /// </summary>
    public void Advance(double nowSec)
    {
        double dt = nowSec - _lastAdvanceSec;
        _lastAdvanceSec = nowSec;
        if (dt <= 0) return;

        for (int i = 0; i < _values.Length; i++)
        {
            var graph = _model.DecayFor((EmotionType)i);
            if (graph is null) continue;
            double before = graph.At(_decaySec[i]);
            _decaySec[i] += dt;
            double after = graph.At(_decaySec[i]);
            _values[i] *= before > 1e-5 ? after / before : after;
        }
    }

    /// <summary>Every axis and its current value, for diagnostics.</summary>
    public IReadOnlyDictionary<EmotionType, double> Snapshot() =>
        Enum.GetValues<EmotionType>().ToDictionary(e => e, e => _values[(int)e]);
}
