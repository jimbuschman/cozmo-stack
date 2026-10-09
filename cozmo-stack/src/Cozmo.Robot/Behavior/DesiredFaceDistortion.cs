using System.Globalization;
using System.Text.Json;
using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

// fidelity: M7-017
/// <summary>
/// The engine's <c>Anki::Cozmo::DesiredFaceDistortionComponent</c> (ctor 0x0063B3BE, Init 0x0063B3D4,
/// Params 0x0063B434, GetCurrentDesiredDistortion 0x0063B760). NeedsManager constructs it (0x0069210C,
/// 0x00692212) and owns it at +0x3D4.
///
/// The component samples a "degree" of face distortion from the repair need level, on the engine tick clock,
/// and the streamer's <c>TrackLayerComponent::Update</c> (0x0064EDE8, the call at 0x0057CF7A) reads it and
/// calls <c>AddGlitch</c> when it is above 1e-5. This class is the source of that degree.
///
/// Layout: params pointer +0 (null); deadline +4 = -1.0f (0xBF800000); manager +8; RNG +0xC (null); cached
/// degree +0x10 = -1.0f; cached tick +0x14 = 0 (D02). Init stores params and RNG and does <b>not</b> reset
/// the deadline, cached degree or cached tick (D04).
///
/// Every float is its engine bit pattern and every arithmetic step is float32 at the engine's width and
/// association (D14, D15, D17). The two draws use <see cref="EngineRandom.RandDblInRange"/> in float64
/// (0x0082FA48 = <c>low + GetNextDbl() * (high - low)</c>).
/// </summary>
public sealed class DesiredFaceDistortionComponent
{
    // D02/D10: -1.0f, the sentinel the deadline and the cached degree are initialised to.
    private static readonly float NegativeOne = BitConverter.Int32BitsToSingle(unchecked((int)0xBF800000));
    // D14/D15: 0x3F000000 (0.5f) and 0x3F800000 (1.0f), the range-widening constants.
    private static readonly float Half = BitConverter.Int32BitsToSingle(0x3F000000);
    private static readonly float One = BitConverter.Int32BitsToSingle(0x3F800000);
    // D13: 0x3DCCCCCD (0.1f), the degree threshold; equality is admitted (the engine's blt rejects below only).
    private static readonly float MinDegree = BitConverter.Int32BitsToSingle(unchecked((int)0x3DCCCCCD));

    /// <summary>
    /// The engine's global error flag at <c>0x0105DD34</c> (D05, D06; the live-update error P05 writes it too).
    /// The reader is not recovered, so this is visibility only. One process-wide flag, as the engine's is.
    /// </summary>
    public static bool ErrorFlagSet
    {
        get => Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        set => Cozmo.Transport.EngineErrorState.ErrorFlagSet = value;
    }

    private readonly NeedsManager _manager;

    /// <summary>+0: the parsed params, or null when the component has not been initialised (D03's skip).</summary>
    private Params? _params;
    /// <summary>+4: the cooldown deadline in BaseStationTimer seconds; -1.0f until the first sample (D02).</summary>
    private float _deadline = NegativeOne;
    /// <summary>+0xC: the RNG, set by <see cref="Init"/> (D04); null leaves the getter at the -1 sentinel.</summary>
    public EngineRandom? Rng { get; set; }
    /// <summary>+0x10: the cached sampled degree; -1.0f until a sample (D02, D10, D17).</summary>
    private float _cachedDegree = NegativeOne;
    /// <summary>+0x14: the tick count the cached degree was sampled on; 0 from construction (D02).</summary>
    private long _cachedTick;

    /// <summary>The log sink (the engine's "NeedsSystem" channel; the exact strings are D05, D06, D10, D17).</summary>
    public Action<string>? Log { get; set; }

    /// <summary>
    /// The tick-count seam. The engine reads <c>BaseStationTimer::GetTickCount</c> (the timer's +0x24,
    /// 0x0084BCE0; monotonic, not wall time). Defaults to 0.
    /// </summary>
    public Func<long> TickCount { get; set; } = () => 0;

    /// <summary>
    /// The seconds seam. The engine reads <c>BaseStationTimer::GetCurrentTimeInSeconds</c> (float32). Defaults to 0.
    /// </summary>
    public Func<float> ClockSeconds { get; set; } = () => 0f;

    public DesiredFaceDistortionComponent(NeedsManager manager) => _manager = manager;

    /// <summary>+0/+4/+8/+0xC as the engine's Params holds them (D05, D06).</summary>
    private sealed class Params
    {
        public Graph2d? Cooldown;          // +0
        public float CooldownMultiplier;   // +4, default 0.0f
        public Graph2d? Degree;            // +8
        public float DegreeMultiplier;     // +0xC, default 0.0f
    }

    /// <summary>
    /// <c>DesiredFaceDistortionComponent::Init</c> 0x0063B3D4 (D04): read <c>needsBasedFaceDistortion</c>, build a
    /// replacement <see cref="Params"/>, swap it into +0 and destroy the old, and store the RNG at +0xC. The
    /// deadline, cached degree and cached tick are not reset.
    /// </summary>
    // fidelity: M7-017
    public void Init(JsonElement needsBasedFaceDistortion, EngineRandom? rng)
    {
        var p = new Params();
        ParseParams(needsBasedFaceDistortion, p);   // D05/D06: a verify/parse failure leaves the partial Params
        _params = p;                                 // swapped in, old destroyed
        Rng = rng;                                   // +0xC
    }

    /// <summary>
    /// The Params parser (D05, D06). Cooldown first: a missing/null <c>cooldown</c> key calls
    /// <c>sVerifyFailedReturnFalse</c> (0x0063B478 <c>bne</c>) and then <b>falls through to the multiplier
    /// read</b> (0x0063B4EC), leaving the default-constructed empty graph in place; a failed read or an empty
    /// graph logs <c>ConfigError.CooldownParsingFailed</c>, sets the global error flag and continues. Then the
    /// <c>cooldown_range_multiplier</c>. Degree next, and the shipped quirk: after the degree read the emptiness
    /// check examines the <b>cooldown</b> graph, not the degree one, so a non-empty degree graph with an empty
    /// cooldown graph logs <c>ConfigError.DegreeParsingFailed</c>. Then <c>degree_range_multiplier</c>.
    /// </summary>
    // fidelity: M7-017
    private void ParseParams(JsonElement block, Params p)
    {
        bool hasBlock = block.ValueKind == JsonValueKind.Object;
        // The cooldown graph is the default-constructed empty graph until ReadFromJson succeeds (0x0063B44C).
        bool cooldownEmpty = true;
        if (!hasBlock || !block.TryGetProperty("cooldown", out var cooldownEl) || cooldownEl.ValueKind == JsonValueKind.Null)
        {
            VerifyFailedReturnFalse("DesiredFaceDistortionComponent.ConfigError.NoCooldownConfig", "No cooldown config specified", "!cooldownEvaluatorConfig.isNull()");
        }
        else
        {
            bool cooldownRead = TryReadGraph(cooldownEl, out var cooldown, out bool empty);
            p.Cooldown = cooldown;
            cooldownEmpty = !cooldownRead || empty;
            if (cooldownEmpty)
                Error("DesiredFaceDistortionComponent.ConfigError.CooldownParsingFailed", "failed to parse cooldown graph evaluator");
        }
        p.CooldownMultiplier = AsFloat(block, "cooldown_range_multiplier");   // +4; a missing key keeps 0

        if (!hasBlock || !block.TryGetProperty("degree", out var degreeEl) || degreeEl.ValueKind == JsonValueKind.Null)
        {
            VerifyFailedReturnFalse("DesiredFaceDistortionComponent.ConfigError.NoDegreeConfig", "No degree config specified", "!degreeEvaluatorConfig.isNull()");
        }
        else
        {
            bool degreeRead = TryReadGraph(degreeEl, out var degree, out _);
            p.Degree = degree;
            // Shipped quirk (D06): the emptiness check after the degree read examines the cooldown graph (+0).
            if (!degreeRead || cooldownEmpty)
                Error("DesiredFaceDistortionComponent.ConfigError.DegreeParsingFailed", "failed to parse degree graph evaluator");
        }
        p.DegreeMultiplier = AsFloat(block, "degree_range_multiplier");       // +0xC; a missing key keeps 0
    }

    /// <summary>
    /// The engine's <c>GraphEvaluator2d::ReadFromJson</c> result. <paramref name="graph"/> is null for a failed
    /// read or an empty node list; <paramref name="empty"/> is true only for a successful read of an empty list
    /// (D06 needs the two apart for its cooldown-graph quirk).
    /// </summary>
    private static bool TryReadGraph(JsonElement e, out Graph2d? graph, out bool empty)
    {
        graph = null;
        empty = false;
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("nodes", out var nodes)) return false;
        var list = new List<(double X, double Y)>();
        foreach (var n in nodes.EnumerateArray())
            list.Add((n.GetProperty("x").GetDouble(), n.GetProperty("y").GetDouble()));
        if (list.Count == 0) { empty = true; return true; }
        graph = new Graph2d(list);
        return true;
    }

    /// <summary><c>asFloat</c> of a config key; a missing key keeps the field's 0.0f default (D05, D06).</summary>
    private static float AsFloat(JsonElement block, string key)
        => block.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? (float)v.GetDouble() : 0f;

    /// <summary>The engine's <c>sVerifyFailedReturnFalse</c>: logs the VERIFY failure, sets the global flag, and stops the parse.</summary>
    private void VerifyFailedReturnFalse(string tag, string message, string condition)
    {
        ErrorFlagSet = true;
        Log?.Invoke($"error: VERIFY({tag}): {message} ({condition})");
    }

    /// <summary>The engine's <c>sErrorF</c> on the parse-failure branches: logs and sets the global error flag.</summary>
    private void Error(string tag, string message)
    {
        ErrorFlagSet = true;
        Log?.Invoke($"error: {tag}: {message}");
    }

    /// <summary>
    /// <c>DesiredFaceDistortionComponent::GetCurrentDesiredDistortion</c> 0x0063B760 (D09..D17), in the engine's
    /// order. Returns the sampled degree, or the -1.0f sentinel when the tick is cached, a gate fails, the deadline
    /// is in the future, or the degree graph is below 0.1f.
    /// </summary>
    // fidelity: M7-017
    public float GetCurrentDesiredDistortion()
    {
        // D09: the per-tick cache; an equal tick returns the cached degree immediately.
        long tick = TickCount();
        if (tick == _cachedTick) return _cachedDegree;

        // D10: a new tick resets the cache to -1 and the tick to the current one, then gates on params/RNG/pause.
        _cachedDegree = NegativeOne;
        _cachedTick = tick;
        if (_params is null || Rng is null || _manager.IsPaused) return NegativeOne;

        // D12: the seconds clock (float32); eligible when deadline < 0 or deadline <= now. The engine's
        // vcmpe/BMI/BHI: NaN deadline falls through to the -1 return, as both comparisons are false.
        float now = ClockSeconds();
        if (!(_deadline < 0f) && !(_deadline <= now)) return NegativeOne;

        // D13: the repair need level, then the degree graph; below 0.1f (equality admitted) returns -1.
        double level = _manager.State.GetNeedLevel(NeedId.Repair);
        float degree = (float)(_params.Degree?.EvaluateY(level) ?? 0.0);
        if (degree < MinDegree) return NegativeOne;

        // D14: the degree draw FIRST. h = float32(degree_multiplier * 0.5f); the bounds are float32;
        // RandDblInRange widens them to float64 (0x0082FA48).
        float degreeH = _params.DegreeMultiplier * Half;
        float degreeLow = degree * (One - degreeH);
        float degreeHigh = degree * (degreeH + One);
        double degreeDraw = Rng.RandDblInRange(degreeLow, degreeHigh);

        // D15: the cooldown draw SECOND, with the same repair level and the identical float32 operations.
        float cooldown = (float)(_params.Cooldown?.EvaluateY(level) ?? 0.0);
        float cooldownH = _params.CooldownMultiplier * Half;
        float cooldownLow = cooldown * (One - cooldownH);
        float cooldownHigh = cooldown * (cooldownH + One);
        double cooldownDraw = Rng.RandDblInRange(cooldownLow, cooldownHigh);

        // D17: convert to float32, log degree then cooldown, store the cached degree and the deadline.
        float sampledDegree = (float)degreeDraw;
        float sampledCooldown = (float)cooldownDraw;
        Log?.Invoke(string.Format(CultureInfo.InvariantCulture,
            "NeedsSystem: DesiredFaceDistortionComponent.DistortingFace.Degree: Repair level: {0:F6}, degree: {1:F6} (desired {2:F6}, range({3:F6}-{4:F6}))",
            level, sampledDegree, degree, degreeLow, degreeHigh));
        Log?.Invoke(string.Format(CultureInfo.InvariantCulture,
            "NeedsSystem: DesiredFaceDistortionComponent.DistortingFace.Cooldown: Repair level: {0:F6}, cooldown: {1:F6}s (desired {2:F6}, range({3:F6}-{4:F6}))",
            level, sampledCooldown, cooldown, cooldownLow, cooldownHigh));

        _cachedDegree = sampledDegree;                 // +0x10 (0x0063B950)
        _deadline = now + sampledCooldown;             // +4 = float32(now + sampledCooldown) (0x0063B954)
        return sampledDegree;
    }

    /// <summary>+4: the cooldown deadline (visibility for tests).</summary>
    internal float Deadline => _deadline;
    /// <summary>+0x10: the cached degree (visibility for tests).</summary>
    internal float CachedDegree => _cachedDegree;
    /// <summary>+0x14: the cached tick (visibility for tests).</summary>
    internal long CachedTick => _cachedTick;
}
