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

    /// <summary>
    /// The engine's built-in decay curve: <c>StaticMoodData</c>'s constructor calls <c>InitDecayGraphs</c> (0x0067CDC3), which fills
    /// every emotion with (0,1), (15,1), (60,0.9f), (150,0.6f), (300,0); the config's own graphs replace it and its "default"
    /// graph fills the emotions without one (0x0067D0C6).
    /// </summary>
    public static readonly DecayGraph BuiltInDecay = new("builtin",
        new[] { (0.0, 1.0), (15.0, 1.0), (60.0, (double)0.9f), (150.0, (double)0.6f), (300.0, 0.0) });

    /// <summary>The decay curve for an emotion: its own, else the config's <c>default</c>, else the built-in curve.</summary>
    public DecayGraph DecayFor(EmotionType emotion) =>
        _decay.TryGetValue(emotion.ToString(), out var g) ? g
        : _decay.TryGetValue("default", out var d) ? d : BuiltInDecay;
}

/// <summary>
/// The ring buffer at the start of each engine <c>Emotion</c> (this+0x00..+0x17: data pointer, end, capacity, head +0x0C, count
/// +0x10, capacity +0x14): 0x80 eight-byte samples <c>{float value, float dt}</c>. The constructor (0x00679406..0x00679428) sizes it
/// to 0x80 (0x0067940A, 0x00679438), zeroes head and count, then pushes one <c>{0.0f, 0.0f}</c> sample (0x00679418..0x00679420);
/// <c>Emotion::Update</c> pushes <c>{value, dt}</c> after every update (0x00679600..0x00679608).
/// </summary>
// fidelity: M7-013
internal sealed class EmotionHistory
{
    /// <summary>0x80 (<c>movs r1,#0x80</c> at 0x0067940A).</summary>
    public const int Capacity = 0x80;

    private readonly (float Value, float Dt)[] _data = new (float, float)[Capacity];
    private uint _head;      // +0x0C
    private uint _count;     // +0x10

    public EmotionHistory() => Push(0f, 0f);

    public uint Head => _head;
    public uint Count => _count;

    /// <summary>
    /// The append at 0x0067945C..0x00679498: when count &gt;= capacity (0x00679468 <c>blo</c> not taken) count is decremented and head
    /// advanced, wrapping to 0 at the capacity (0x0067946C..0x0067947A); the slot is <c>(head + count) mod capacity</c> (unsigned
    /// remainder, 0x0067947C..0x0067947E); the sample is stored there and the count incremented (0x00679492..0x00679496).
    /// </summary>
    public void Push(float value, float dt)
    {
        uint head = _head, count = _count;
        if (count >= Capacity)
        {
            count -= 1;
            head += 1;
            _head = head; _count = count;
            if (head >= Capacity) { head = 0; _head = 0; }
        }
        uint idx = (head + count) % Capacity;
        _data[idx] = (value, dt);
        _count += 1;
    }

    /// <summary>
    /// <c>Emotion::GetHistoryValueTicksAgo(ticks)</c> 0x006794F8: <paramref name="ticks"/> == 0 or an empty ring (0x006794F8,
    /// 0x006794FC) gives the emotion's current value (this+0x18, 0x00679520); otherwise the slot is
    /// <c>(head + (count &gt; ticks ? count - ticks : 0)) mod capacity</c> (unsigned: <c>subs; it hi; addhi</c> 0x00679508..0x0067950C,
    /// remainder 0x00679510..0x00679512) and the value is the first word of that sample (0x00679516, 0x00679522).
    /// </summary>
    public float GetValueTicksAgo(uint ticks, float current)
    {
        if (ticks == 0 || _count == 0) return current;
        uint r2 = _head;
        if (_count > ticks) r2 += _count - ticks;
        return _data[r2 % Capacity].Value;
    }
}

/// <summary>
/// A mood: a value per emotion axis, moved by named events and faded by the shipped decay curves.
///
/// Time is taken rather than read, as everywhere else in this stack, so a mood can be wound forward in a
/// test without waiting. Values are clamped to [-1, 1], which is the range the shipped affectors and the
/// decompiled <c>CurrentMoodCondition</c> range attribute both imply.
///
/// The engine's <c>MoodManager</c> / <c>Emotion</c> arithmetic is <c>float</c> throughout (every <c>vadd.f32</c>, <c>vmul.f32</c>,
/// <c>vdiv.f32</c> named below), so the values, the decay clocks and the event stamps are held as <c>float</c>; the public
/// <c>double</c> surface is the exact widening of those floats.
/// </summary>
// fidelity: M7-012, M7-013, M7-020
public sealed class MoodState
{
    /// <summary>
    /// A change smaller than this does not restart the decay clock. <c>Emotion::Add</c> at 0x00679618
    /// compares the magnitude of the change against 0.05f, bits 0x3D4CCCCD (the literal at 0x0067967E).
    /// </summary>
    public static readonly float DecayResetThreshold = BitConverter.Int32BitsToSingle(0x3D4CCCCD);

    /// <summary>
    /// The floor of <c>MoodManager::Update</c>'s step: 1e-4f, bits 0x38D1B717 (the literal at 0x0067B5EA). It is the step when the
    /// stored last time (+0x130) is 0, and no step is ever smaller (0x0067B5E6..0x0067B612).
    /// </summary>
    public static readonly float MinUpdateStepSec = BitConverter.Int32BitsToSingle(0x38D1B717);

    /// <summary>
    /// <c>Emotion::Update</c>'s "old reading" threshold: 1e-5f, bits 0x3727C5AC (0x006795D6; the literal at 0x00679614).
    /// </summary>
    public static readonly float OldReadingFloor = BitConverter.Int32BitsToSingle(0x3727C5AC);

    /// <summary>
    /// The elapsed time returned for an event's first trigger: FLT_MAX, bits 0x7F7FFFFF
    /// (<c>UpdateLatestEventTimeAndGetTimeElapsedInSeconds</c> 0x0067be48, the literal at 0x0067BEA4).
    /// </summary>
    public const float NoPreviousEventSec = float.MaxValue;

    private readonly MoodModel _model;
    private readonly float[] _values = new float[Enum.GetValues<EmotionType>().Length];

    /// <summary>
    /// The engine's own per-emotion decay clock, <c>Emotion</c> this+0x1C (float). It is not simply the time
    /// since the last change: see <see cref="Trigger"/>.
    /// </summary>
    private readonly float[] _decaySec = new float[Enum.GetValues<EmotionType>().Length];
    private readonly EmotionHistory[] _history = Enumerable.Range(0, Enum.GetValues<EmotionType>().Length)
        .Select(_ => new EmotionHistory()).ToArray();

    /// <summary>MoodManager+0x130: the time of the last <see cref="Advance"/> (float seconds; 0 until the first).</summary>
    private float _lastUpdateSec;

    /// <summary>MoodManager+0x120: the last trigger time of each event name (a float map), for the repetition penalty.</summary>
    private readonly Dictionary<string, float> _lastEventSec = new(StringComparer.Ordinal);

    /// <summary>MoodManager+0x140: action ids whose completion must not raise a mood event.</summary>
    private readonly HashSet<string> _completionDisabled = new(StringComparer.Ordinal);

    /// <summary>The warnings the engine logs (<c>MoodManager::Update</c>'s step below the floor).</summary>
    public Action<string>? Log { get; set; }

    public MoodState(MoodModel model) => _model = model;

    /// <summary>The current value of one axis (the engine's float, widened), after decay up to the last <see cref="Advance"/>.</summary>
    public double this[EmotionType e] => _values[(int)e];

    /// <summary>
    /// <c>Emotion::GetHistoryValueTicksAgo(ticks)</c> 0x006794F8 on one emotion: its value that many <see cref="Advance"/> updates
    /// ago (the ring holds the 0x80 most recent), or the current value for 0 ticks or an empty ring.
    /// </summary>
    // fidelity: M7-013
    public float GetHistoryValueTicksAgo(EmotionType e, uint ticks) => _history[(int)e].GetValueTicksAgo(ticks, _values[(int)e]);

    /// <summary>
    /// Applies a named event. Unknown names change nothing and report false.
    ///
    /// <c>MoodManager::TriggerEmotionEvent</c> 0x0067b85c: FindEvent, then <c>UpdateLatestEventTimeAndGetTimeElapsedInSeconds</c>,
    /// <c>EmotionEvent::CalculateRepetitionPenalty</c>, then per affector <c>Emotion::Add(penalty * value)</c> (<c>vmul.f32</c> at
    /// 0x0067b8f6, call 0x0067b902). It calls no <c>Emotion::Update</c> and decays nothing: decay happens only in
    /// <see cref="Advance"/> (<c>MoodManager::Update</c>).
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
    /// schedule it was already on.
    /// </summary>
    // fidelity: M7-013
    public bool Trigger(string eventName, double nowSec)
    {
        var e = _model.Event(eventName);
        if (e is null) return false;
        float penalty = RepetitionPenalty(e, eventName, (float)nowSec);
        foreach (var a in e.Affectors)
            Add((int)a.Emotion, penalty * (float)a.Value);
        return true;
    }

    /// <summary>
    /// <c>Emotion::Add(float d)</c> 0x00679618..0x006796C0: <c>s = value + d</c> (float); the stored value is -1.0f when
    /// not <c>s &gt; -1.0f</c>, 1.0f when <c>s &gt;= 1.0f</c>, else s (0x00679628..0x0067964E); the decay clock is zeroed when the sign
    /// of (old &gt;= 0) differs from that of (new &gt;= 0), or when |d| &gt; 0.05f and (old &gt;= 0) == (d &gt;= 0).
    /// </summary>
    private void Add(int i, float d)
    {
        float old = _values[i];
        float s = old + d;
        float updated = s > -1f ? (s >= 1f ? 1f : s) : -1f;
        _values[i] = updated;

        bool keptItsSign = old >= 0 == updated >= 0;
        bool awayFromZero = old >= 0 == d >= 0;
        // 0x006796A8/0x006796AC: a sign flip jumps straight to the reset; otherwise the two tests.
        if (!keptItsSign || (MathF.Abs(d) > DecayResetThreshold && awayFromZero)) _decaySec[i] = 0f;
    }

    /// <summary>
    /// <c>MoodManager::TriggerEmotionEvent</c> 0x0067b85c: the elapsed time from
    /// <c>UpdateLatestEventTimeAndGetTimeElapsedInSeconds</c> 0x0067be48 (a float map: a new key gives FLT_MAX, an existing one
    /// <c>now - old</c> by <c>vsub.f32</c> at 0x0067BEB2, and <c>now</c> is stamped), then the event's own
    /// <c>EmotionEvent::CalculateRepetitionPenalty</c> 0x00679bb8 (<c>GraphEvaluator2d::EvaluateY</c> on
    /// the graph at <c>EmotionEvent</c>+0x18). The shipped events carry no repetition graph
    /// (<c>EmotionEvent::ReadFromJson</c> 0x00679bc0 clears it); a missing graph gives 1.0 here (that an empty engine graph
    /// evaluates to 1 is not established: check 2 section D).
    /// </summary>
    private float RepetitionPenalty(EmotionEvent e, string eventName, float now)
    {
        float elapsed;
        if (_lastEventSec.TryGetValue(eventName, out var last)) elapsed = now - last;
        else elapsed = NoPreviousEventSec;
        _lastEventSec[eventName] = now;
        return e.RepetitionPenalty is { } g ? (float)g.At(elapsed) : 1f;
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
        Enum.GetValues<EmotionType>().Select(e => (double)_values[(int)e]).ToArray();

    /// <summary>Raised with the nine values when <see cref="SendEmotionsToGame"/> is called.</summary>
    public event Action<IReadOnlyList<double>>? EmotionsBroadcast;

    /// <summary>Sends the nine emotion values out through the stack's seam.</summary>
    public void SendEmotionsToGame() => EmotionsBroadcast?.Invoke(EmotionValues());

    /// <summary>
    /// <c>MoodManager::Update(float t)</c> 0x0067b5d4, the only place decay happens. The step is <c>t - last</c> (float) when the
    /// stored last time (+0x130) is non-zero and 1e-4f when it is zero (<c>vcmp s0,#0</c>, <c>it ne</c>, 0x0067B5EE..0x0067B604);
    /// a step below 1e-4f is replaced by 1e-4f with a warning (0x0067B608..0x0067B66C); +0x130 = t (0x0067B67E); then
    /// <c>Emotion::Update(decayGraph, t, dt)</c> for the nine emotions in order (0x0067B682..0x0067B69E). (The call to
    /// <c>SendEmotionsToGame</c> that follows, 0x0067B6A4, is <see cref="SendEmotionsToGame"/>, made by the caller.)
    ///
    /// <c>Emotion::Update</c> 0x006795A4: old = graph(decay); decay += dt; new = graph(decay); the value is multiplied by
    /// new / old when old &gt; 1e-5f and by the raw new reading when it is not (<c>vdiv.f32</c> 0x006795E6, <c>it gt</c> 0x006795EE,
    /// <c>vmul.f32</c> 0x006795F8); then <c>{value, dt}</c> is appended to the emotion's history (0x00679600..0x00679608).
    /// </summary>
    // fidelity: M7-013
    public void Advance(double nowSec)
    {
        float t = (float)nowSec;
        float last = _lastUpdateSec;
        float dt = last != 0 ? t - last : MinUpdateStepSec;
        if (dt < MinUpdateStepSec)
        {
            Log?.Invoke($"warning: MoodManager.Update.TimeStepTooSmall: dt {dt}, last {last}, now {t}");
            dt = MinUpdateStepSec;
        }
        _lastUpdateSec = t;

        for (int i = 0; i < _values.Length; i++)
        {
            var graph = _model.DecayFor((EmotionType)i);
            float before = (float)graph.At(_decaySec[i]);
            _decaySec[i] += dt;
            float after = (float)graph.At(_decaySec[i]);
            float f = before > OldReadingFloor ? after / before : after;
            _values[i] = _values[i] * f;
            _history[i].Push(_values[i], dt);
        }
    }

    /// <summary>Every axis and its current value, for diagnostics.</summary>
    public IReadOnlyDictionary<EmotionType, double> Snapshot() =>
        Enum.GetValues<EmotionType>().ToDictionary(e => e, e => (double)_values[(int)e]);
}
