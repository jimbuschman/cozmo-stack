using System.Text.Json;

namespace Cozmo.Robot.Behavior;

/// <summary><c>Anki::Cozmo::NeedId</c> (UNITY): Repair, Energy, Play.</summary>
public enum NeedId { Repair = 0, Energy = 1, Play = 2 }

/// <summary><c>Anki::Cozmo::NeedBracketId</c> (UNITY): Full, Normal, Warning, Critical.</summary>
public enum NeedBracketId { Full = 0, Normal = 1, Warning = 2, Critical = 3 }

/// <summary>
/// ASSET <c>config/engine/needs_config.json</c>: the level range, initial levels, the bracket thresholds per
/// need, the decay period and the fullness cooldown.
/// </summary>
public sealed record NeedsConfig(double MaximumNeedLevel, double MinimumNeedLevel, double DecayPeriodSeconds,
                                 IReadOnlyDictionary<NeedId, double> InitialLevels,
                                 IReadOnlyDictionary<NeedId, (double Full, double Normal, double Warning, double Critical)> Brackets,
                                 IReadOnlyDictionary<NeedId, double> FullnessDecayCooldownSec)
{
    public static NeedsConfig Default => new(1, 0.03, 60,
        new Dictionary<NeedId, double> { [NeedId.Repair] = 1, [NeedId.Energy] = 1, [NeedId.Play] = 1 },
        new Dictionary<NeedId, (double, double, double, double)>
        {
            [NeedId.Repair] = (0.99, 0.6, 0.27, 0), [NeedId.Energy] = (0.99, 0.6, 0.21, 0), [NeedId.Play] = (0.99, 0.5, 0.14, 0),
        },
        new Dictionary<NeedId, double> { [NeedId.Repair] = 1200, [NeedId.Energy] = 1200, [NeedId.Play] = 1200 });

    public static NeedsConfig Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var r = doc.RootElement;
        double D(string k, double d) => r.TryGetProperty(k, out var v) ? v.GetDouble() : d;
        var needs = new[] { NeedId.Repair, NeedId.Energy, NeedId.Play };
        return new NeedsConfig(D("MaximumNeedLevel", 1), D("MinimumNeedLevel", 0.03), D("DecayPeriodSeconds", 60),
            needs.ToDictionary(n => n, n => D($"InitialNeedLevel{n}", 1)),
            needs.ToDictionary(n => n, n => (D($"BracketLevel{n}Full", 0.99), D($"BracketLevel{n}Normal", 0.6), D($"BracketLevel{n}Warning", 0.2), D($"BracketLevel{n}Critical", 0))),
            needs.ToDictionary(n => n, n => D($"FullnessDecayCooldown{n}", 1200)));
    }
}

/// <summary>
/// ASSET <c>needs_decay_config.json</c>: per need, the decay per minute for the level band above each
/// threshold, connected and unconnected. The rows are in descending threshold order; the rate for a level is
/// the first row whose threshold the level exceeds (INFERRED reading of "Threshold" / "DecayPerMinute"). The
/// <c>DecayModifiers</c> (one need's level scaling another's decay) are DEFERRED.
/// </summary>
public sealed record DecayConfig(IReadOnlyDictionary<NeedId, IReadOnlyList<(double Threshold, double PerMinute)>> Connected,
                                 IReadOnlyDictionary<NeedId, IReadOnlyList<(double Threshold, double PerMinute)>> Unconnected)
{
    public double RatePerMinute(NeedId need, double level, bool connected)
    {
        var table = (connected ? Connected : Unconnected).GetValueOrDefault(need);
        if (table is null || table.Count == 0) return 0;
        foreach (var (t, rate) in table) if (level > t) return rate;
        return table[^1].PerMinute;
    }

    public static DecayConfig Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var rates = doc.RootElement.GetProperty("DecayRates");
        IReadOnlyDictionary<NeedId, IReadOnlyList<(double, double)>> Read(string prefix) =>
            new[] { NeedId.Repair, NeedId.Energy, NeedId.Play }.ToDictionary(n => n, n =>
                (IReadOnlyList<(double, double)>)(rates.TryGetProperty($"{prefix}DecayRates{n}", out var arr)
                    ? arr.EnumerateArray().Select(e => (e.GetProperty("Threshold").GetDouble(), e.GetProperty("DecayPerMinute").GetDouble())).OrderByDescending(x => x.Item1).ToList()
                    : new List<(double, double)>()));
        return new DecayConfig(Read("Connected"), Read("Unconnected"));
    }
}

/// <summary>ASSET <c>needs_action_config.json</c> <c>actionDeltas</c>: what completing a needs action does to each need (delta ± a random range).</summary>
public sealed record NeedsActionDelta(string ActionId, double RepairDelta, double RepairRange, double EnergyDelta, double EnergyRange, double PlayDelta, double PlayRange, double CooldownSec, double FreeplaySparksRewardWeight)
{
    public static IReadOnlyDictionary<string, NeedsActionDelta> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var d = new Dictionary<string, NeedsActionDelta>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in doc.RootElement.GetProperty("actionDeltas").EnumerateArray())
        {
            double G(string k) => e.TryGetProperty(k, out var v) ? v.GetDouble() : 0;
            var id = e.GetProperty("actionId").GetString()!;
            d[id] = new NeedsActionDelta(id, G("repairDelta"), G("repairRange"), G("energyDelta"), G("energyRange"), G("playDelta"), G("playRange"), G("cooldownSecs"), G("freeplaySparksRewardWeight"));
        }
        return d;
    }
}

/// <summary>
/// The engine's <c>NeedsState</c>: the three levels, their brackets (<c>GetNeedBracket</c>: the highest bracket
/// whose threshold the level reaches, <c>IsNeedAtBracket</c>, <c>GetLowestNeedAndBracket</c>), <c>ApplyDelta</c>
/// clamped to the configured range, and <c>ApplyDecay</c> ("Decaying need index %d with elapsed time of %f
/// seconds", per-minute rates over the elapsed seconds / 60). The repair "damaged parts" bookkeeping is DEFERRED.
/// </summary>
public sealed class NeedsState
{
    private readonly NeedsConfig _cfg;
    private readonly double[] _levels = new double[3];

    public NeedsState(NeedsConfig cfg)
    {
        _cfg = cfg;
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play }) _levels[(int)n] = cfg.InitialLevels.GetValueOrDefault(n, 1);
    }

    public double GetNeedLevel(NeedId n) => _levels[(int)n];
    public void SetNeedLevel(NeedId n, double level) => _levels[(int)n] = Math.Clamp(level, _cfg.MinimumNeedLevel, _cfg.MaximumNeedLevel);

    public NeedBracketId GetNeedBracket(NeedId n)
    {
        var (full, normal, warning, _) = _cfg.Brackets[n];
        double l = _levels[(int)n];
        return l >= full ? NeedBracketId.Full : l >= normal ? NeedBracketId.Normal : l >= warning ? NeedBracketId.Warning : NeedBracketId.Critical;
    }

    public bool IsNeedAtBracket(NeedId n, NeedBracketId b) => GetNeedBracket(n) == b;

    public (NeedId Need, NeedBracketId Bracket) GetLowestNeedAndBracket()
    {
        var lowest = NeedId.Repair;
        foreach (var n in new[] { NeedId.Energy, NeedId.Play }) if (_levels[(int)n] < _levels[(int)lowest]) lowest = n;
        return (lowest, GetNeedBracket(lowest));
    }

    public bool AreNeedsMet() => new[] { NeedId.Repair, NeedId.Energy, NeedId.Play }.All(n => GetNeedBracket(n) is NeedBracketId.Full or NeedBracketId.Normal);

    public void ApplyDelta(NeedId n, double delta) => SetNeedLevel(n, _levels[(int)n] + delta);

    public void ApplyDecay(DecayConfig decay, double elapsedSec, bool connected)
    {
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
        {
            double rate = decay.RatePerMinute(n, _levels[(int)n], connected);
            SetNeedLevel(n, _levels[(int)n] - rate * elapsedSec / 60.0);
        }
    }
}

/// <summary>
/// The engine's <c>NeedsManager</c> (0x0069210C..0x00696D70): owns the state and configs, decays every
/// <c>DecayPeriodSeconds</c> (<c>Update</c> → <c>ApplyDecayAllNeeds</c>, skipped for a need within its fullness
/// cooldown after being filled), applies action deltas (<c>RegisterNeedsActionCompleted</c>: "needs.action_completed",
/// deltas ± range, <c>DetectBracketChangeForDas</c>), and remembers which severe states were expressed
/// (<c>BehaviorPlayAnimOnNeedsChange</c> / <c>ExpressNeeds</c> read and clear it). The stars / sparks economy
/// (<c>UpdateStarsState</c>, <c>RewardSparksForFreeplay</c>) is the app's; a freeplay sparks award is modelled only
/// as the <see cref="SparksRewardPending"/> flag <c>EarnedSparks</c> reads. Persistence to the device is DEFERRED.
/// </summary>
public sealed class NeedsManager
{
    private readonly Func<double> _clockSec;
    private double _lastDecaySec = double.NaN;
    private readonly Dictionary<NeedId, double> _filledAtSec = new();
    private readonly HashSet<NeedId> _severeExpressed = new();
    private readonly Dictionary<NeedId, NeedBracketId> _prevBrackets = new();

    public NeedsManager(Func<double> clockSec, NeedsConfig? config = null, DecayConfig? decay = null, IReadOnlyDictionary<string, NeedsActionDelta>? actions = null, Random? random = null)
    {
        _clockSec = clockSec;
        Config = config ?? NeedsConfig.Default;
        Decay = decay ?? new DecayConfig(new Dictionary<NeedId, IReadOnlyList<(double, double)>>(), new Dictionary<NeedId, IReadOnlyList<(double, double)>>());
        Actions = actions ?? new Dictionary<string, NeedsActionDelta>();
        State = new NeedsState(Config);
        Random = random ?? new Random();
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play }) _prevBrackets[n] = State.GetNeedBracket(n);
    }

    public NeedsConfig Config { get; }
    public DecayConfig Decay { get; }
    public IReadOnlyDictionary<string, NeedsActionDelta> Actions { get; }
    public NeedsState State { get; }
    public Random Random { get; }
    /// <summary>Whether the robot is connected (the connected decay rates apply).</summary>
    public bool Connected { get; set; } = true;
    /// <summary>Set when a freeplay sparks reward is pending (the engine's <c>RewardSparksForFreeplay</c>); <c>EarnedSparks</c> clears it.</summary>
    public bool SparksRewardPending { get; set; }
    public event Action<NeedId, NeedBracketId, NeedBracketId>? BracketChanged;
    public event Action<string>? Log;

    public static NeedsManager FromObb(string obbRoot, Func<double> clockSec, Random? random = null)
    {
        var dir = Path.Combine(obbRoot, "assets", "cozmo_resources", "config", "engine");
        string? Read(string name) { var p = Path.Combine(dir, name); return File.Exists(p) ? File.ReadAllText(p) : null; }
        var cfg = Read("needs_config.json") is { } c ? NeedsConfig.Parse(c) : null;
        var decay = Read("needs_decay_config.json") is { } d ? DecayConfig.Parse(d) : null;
        var actions = Read("needs_action_config.json") is { } a ? NeedsActionDelta.Parse(a) : null;
        return new NeedsManager(clockSec, cfg, decay, actions, random);
    }

    /// <summary><c>NeedsManager::Update</c>: decay when the period has elapsed.</summary>
    public void Update()
    {
        double now = _clockSec();
        if (double.IsNaN(_lastDecaySec)) { _lastDecaySec = now; return; }
        double elapsed = now - _lastDecaySec;
        if (elapsed < Config.DecayPeriodSeconds) return;
        _lastDecaySec = now;
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
        {
            if (_filledAtSec.TryGetValue(n, out var filled) && now - filled < Config.FullnessDecayCooldownSec.GetValueOrDefault(n, 0)) continue;
            double rate = Decay.RatePerMinute(n, State.GetNeedLevel(n), Connected);
            State.ApplyDelta(n, -rate * elapsed / 60.0);
        }
        DetectBracketChanges();
    }

    /// <summary><c>RegisterNeedsActionCompleted</c>: the action's deltas (± range) applied; unknown actions are ignored and logged.</summary>
    public bool RegisterNeedsActionCompleted(string actionId)
    {
        if (!Actions.TryGetValue(actionId, out var d)) { Log?.Invoke($"needs.action_completed_ignored {actionId}"); return false; }
        void Apply(NeedId n, double delta, double range)
        {
            if (delta == 0 && range == 0) return;
            double v = delta + (range > 0 ? (Random.NextDouble() * 2 - 1) * range : 0);
            State.ApplyDelta(n, v);
            if (v > 0 && State.GetNeedBracket(n) == NeedBracketId.Full) _filledAtSec[n] = _clockSec();
            if (v > 0) _severeExpressed.Remove(n);
        }
        Apply(NeedId.Repair, d.RepairDelta, d.RepairRange);
        Apply(NeedId.Energy, d.EnergyDelta, d.EnergyRange);
        Apply(NeedId.Play, d.PlayDelta, d.PlayRange);
        Log?.Invoke($"needs.action_completed {actionId}: repair {State.GetNeedLevel(NeedId.Repair):F3} energy {State.GetNeedLevel(NeedId.Energy):F3} play {State.GetNeedLevel(NeedId.Play):F3}");
        if (d.FreeplaySparksRewardWeight > 0) SparksRewardPending = true;
        DetectBracketChanges();
        return true;
    }

    /// <summary>The severe (Critical) state of a need has been expressed (the get-in played).</summary>
    public bool IsSevereExpressed(NeedId n) => _severeExpressed.Contains(n);
    public void SetSevereExpressed(NeedId n, bool expressed) { if (expressed) _severeExpressed.Add(n); else _severeExpressed.Remove(n); }

    private void DetectBracketChanges()
    {
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
        {
            var b = State.GetNeedBracket(n);
            if (b != _prevBrackets[n])
            {
                Log?.Invoke($"need {n}: {_prevBrackets[n]} -> {b} (level {State.GetNeedLevel(n):F3})");
                if (b != NeedBracketId.Critical) _severeExpressed.Remove(n);
                BracketChanged?.Invoke(n, _prevBrackets[n], b);
                _prevBrackets[n] = b;
            }
        }
    }

    /// <summary>Test / tool hook: set a level directly and report the bracket change.</summary>
    public void SetLevel(NeedId n, double level) { State.SetNeedLevel(n, level); DetectBracketChanges(); }
}
