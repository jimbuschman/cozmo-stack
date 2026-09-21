using System.Text.Json;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// The behaviour end of the needs system: which behaviour reports which needs action, and the reporting
/// itself.
///
/// <c>IBehavior::NeedActionCompleted(NeedsActionId)</c> 0x005BE40C is four instructions: when the caller
/// passes <c>Invalid</c> (0) the behaviour's own <c>needsActionID</c> is used instead (the <c>cbnz</c> at
/// 0x005BE40C and the load from <c>this+0x68</c>), and the result goes to
/// <c>NeedsManager::RegisterNeedsActionCompleted</c> through the robot. That +0x68 is filled by
/// <c>IBehavior::IBehavior</c> at 0x005BBCAA from <c>ExtractNeedsActionIDFromConfig</c> 0x005BBAE8, which
/// reads the config's <c>needsActionID</c> string and turns it into the enum; a behaviour whose config has
/// no such key gets <c>Invalid</c> and reports nothing.
///
/// Sixteen behaviours call it. Ten pass <c>Invalid</c> and so report their own configured action -
/// <c>BehaviorKnockOverCubes::TransitionToPlayingReaction</c> (only when the stack came apart: the flag at
/// +0x14c gates both the objective and the report, 0x005C3950), <c>BehaviorPopAWheelie</c> on the docking
/// success (0x005C7E14, beside objective <c>PoppedWheelie</c>), <c>BehaviorRollBlock::TransitionToRollSuccess</c>,
/// <c>BehaviorStackBlocks::TransitionToPlayingFinalAnim</c>, <c>BehaviorCubeLiftWorkout::EndIteration</c>,
/// <c>BehaviorBuildPyramid::TransitionToReactingToPyramid</c>, <c>BehaviorFistBump::UpdateInternal</c>,
/// <c>BehaviorPeekABoo::TransitionExit</c>, <c>BehaviorTrackLaser::StopInternal</c> and the singing
/// behaviour's last step, which falls back to <c>CozmoSings</c> (0x0D) when its config gives none
/// (0x005EF7CA). Six name an action outright: <c>PickupCube</c> (0x1F) from
/// <c>BehaviorExploreBringCubeToBeacon::TransitionToObjectPickedUp</c>, <c>StackCube</c> (0x2A) from the
/// explorer's stacking callback, <c>GuardDogLose</c>/<c>Win</c>/<c>NoInteraction</c> (0x15/0x16/0x17) from
/// <c>BehaviorGuardDog::RecordResult</c>, <c>PlacedOnSide</c> (0x2F) and <c>BoredOnSide</c> (0x30) from
/// <c>BehaviorReactToRobotOnSide</c>, and <c>DizzyMedium</c>/<c>DizzySoft</c> (0x0F/0x10) from
/// <c>BehaviorReactToRobotShaken</c>. One activity reports directly to the manager:
/// <c>ActivityGatherCubes::Update</c> passes <c>GatherCubes</c> (0x14) when every cube is in a beacon
/// (0x005AF2F0).
///
/// An <b>activity's</b> own <c>needsActionID</c> is not a hook. <c>IActivity::ReadConfig</c> stores it at
/// <c>IActivity+0x1C</c> (0x005B2A9C) and nothing in this build ever loads that word again - no activity
/// method reads it, and the only activity that reports an action names it itself. So the three shipped
/// activity-level ids (<c>PyramidCompleted</c>, <c>PyramidCompleted_Sparked</c>,
/// <c>CozmoSingsCompleted_Sparked</c>) are dead data in 3.4.0, and an earlier version of this stack, which
/// reported the activity's id when the activity ended, was reporting something the engine never reports.
/// </summary>
public static class BehaviorNeedsActions
{
    /// <summary>
    /// Every shipped behaviour config's <c>needsActionID</c>, by <c>behaviorID</c>. Twenty-two of them carry
    /// one; the rest report nothing of their own.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Load(string obbRoot)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var dir = Path.Combine(obbRoot, "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "behaviors");
        if (!Directory.Exists(dir)) return map;
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(f), options);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;
                if (root.TryGetProperty("behaviorID", out var id) && id.GetString() is { } bid
                    && root.TryGetProperty("needsActionID", out var na) && na.GetString() is { Length: > 0 } action)
                    map[bid] = action;
            }
            catch (JsonException) { }
        }
        return map;
    }

    /// <summary>
    /// <c>IBehavior::NeedActionCompleted</c> 0x005BE40C: <paramref name="actionId"/> when the caller names
    /// one, otherwise the behaviour's own configured id, otherwise <paramref name="fallback"/> (the singing
    /// behaviour is the one caller that has one). Returns the action reported, or null when there was none
    /// to report or no needs manager to report it to.
    /// </summary>
    public static string? Complete(IBehavior behavior, BehaviorContext context, string? actionId = null, string? fallback = null)
    {
        string? id = actionId
                  ?? (context.NeedsActionIds is { } ids && ids.TryGetValue(behavior.Id, out var own) ? own : null)
                  ?? fallback;
        if (id is null || context.Needs is null) return null;
        context.Needs.RegisterNeedsActionCompleted(id);
        return id;
    }
}

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
                                 IReadOnlyDictionary<NeedId, double> FullnessDecayCooldownSec,
                                 IReadOnlyList<double>? BrokenPartThresholdList = null)
{
    /// <summary>
    /// The config's <c>BrokenPartThreshold0..2</c>, in order: the repair levels at or above which each of
    /// Cozmo's three parts counts as damaged (0.98, 0.6 and 0.3 in the shipped file). See
    /// <see cref="NeedsState.NumDamagedPartsForRepairLevel"/>.
    /// </summary>
    public IReadOnlyList<double> BrokenPartThresholds => BrokenPartThresholdList ?? new[] { 0.98, 0.6, 0.3 };

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
            needs.ToDictionary(n => n, n => D($"FullnessDecayCooldown{n}", 1200)),
            new[] { D("BrokenPartThreshold0", 0.98), D("BrokenPartThreshold1", 0.6), D("BrokenPartThreshold2", 0.3) });
    }
}

/// <summary>
/// ASSET <c>needs_decay_config.json</c>: per need, the decay per minute for the level band above each
/// threshold, connected and unconnected. The rows are in descending threshold order; the rate for a level is
/// the first row whose threshold the level exceeds (INFERRED reading of "Threshold" / "DecayPerMinute").
///
/// <c>DecayModifiers</c> is the other half, and it is read now.
/// <c>NeedsState::GetDecayMultipliers</c> 0x0069C214 fills an array of three multipliers, one per need,
/// starting at one: for each need it takes the level and walks that need's modifier list, and every entry
/// whose threshold the level is at or under multiplies the listed other need's entry in the array
/// (<c>vcmpe</c> at 0x0069C270, the multiply and store at 0x0069C2A4). <c>ApplyDecayAllNeeds</c> 0x00695CFE
/// asks for those multipliers once and hands them to <c>NeedsState::ApplyDecay</c>.
///
/// In the shipped file only one entry ever changes anything: while Repair is at or below 0.03, Play's
/// decay is doubled. Everything else multiplies by one.
/// </summary>
public sealed record DecayConfig(IReadOnlyDictionary<NeedId, IReadOnlyList<(double Threshold, double PerMinute)>> Connected,
                                 IReadOnlyDictionary<NeedId, IReadOnlyList<(double Threshold, double PerMinute)>> Unconnected,
                                 IReadOnlyDictionary<NeedId, IReadOnlyList<(double Threshold, NeedId Other, double Multiplier)>>? Modifiers = null)
{
    /// <summary>
    /// The three multipliers <c>GetDecayMultipliers</c> works out from the current levels: one per need,
    /// each the product of every modifier entry that applies to it.
    /// </summary>
    public IReadOnlyDictionary<NeedId, double> DecayMultipliers(Func<NeedId, double> level)
    {
        var result = new Dictionary<NeedId, double> { [NeedId.Repair] = 1, [NeedId.Energy] = 1, [NeedId.Play] = 1 };
        if (Modifiers is null) return result;
        foreach (var (source, entries) in Modifiers)
        {
            double l = level(source);
            foreach (var (threshold, other, multiplier) in entries)
                if (l <= threshold) result[other] *= multiplier;
        }
        return result;
    }

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
        var modifiers = new Dictionary<NeedId, IReadOnlyList<(double, NeedId, double)>>();
        if (doc.RootElement.TryGetProperty("DecayModifiers", out var mods))
            foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
            {
                if (!mods.TryGetProperty($"ConnectedDecayModifiers{n}", out var arr)) continue;
                var list = new List<(double, NeedId, double)>();
                foreach (var e in arr.EnumerateArray())
                {
                    double threshold = e.GetProperty("Threshold").GetDouble();
                    if (!e.TryGetProperty("OtherNeedsAffected", out var others)) continue;
                    foreach (var o in others.EnumerateArray())
                        if (Enum.TryParse<NeedId>(o.GetProperty("OtherNeedID").GetString(), true, out var other))
                            list.Add((threshold, other, o.GetProperty("Multiplier").GetDouble()));
                }
                if (list.Count > 0) modifiers[n] = list;
            }
        IReadOnlyDictionary<NeedId, IReadOnlyList<(double, double)>> Read(string prefix) =>
            new[] { NeedId.Repair, NeedId.Energy, NeedId.Play }.ToDictionary(n => n, n =>
                (IReadOnlyList<(double, double)>)(rates.TryGetProperty($"{prefix}DecayRates{n}", out var arr)
                    ? arr.EnumerateArray().Select(e => (e.GetProperty("Threshold").GetDouble(), e.GetProperty("DecayPerMinute").GetDouble())).OrderByDescending(x => x.Item1).ToList()
                    : new List<(double, double)>()));
        return new DecayConfig(Read("Connected"), Read("Unconnected"), modifiers.Count > 0 ? modifiers : null);
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
        // GetDecayMultipliers is asked once, from the levels as they stand, and the same three multipliers
        // are used for the whole pass (ApplyDecayAllNeeds 0x00695CFE).
        var multipliers = decay.DecayMultipliers(n => _levels[(int)n]);
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
        {
            double rate = decay.RatePerMinute(n, _levels[(int)n], connected) * multipliers[n];
            SetNeedLevel(n, _levels[(int)n] - rate * elapsedSec / 60.0);
        }
    }

    /// <summary>
    /// How many of Cozmo's parts count as damaged at a repair level.
    /// <c>NeedsState::NumDamagedPartsForRepairLevel</c> 0x0069CCAC walks the broken-part thresholds in
    /// order and counts the leading run that is at or above the level, stopping at the first one below it
    /// (the <c>vcmpe</c> and <c>bxmi</c> at 0x0069CCC8). The thresholds are the config's
    /// <c>BrokenPartThreshold0..2</c>, 0.98, 0.6 and 0.3 in the shipped file, so a full robot has none
    /// damaged, one below 0.98 has one, below 0.6 two and below 0.3 all three.
    /// </summary>
    public int NumDamagedPartsForRepairLevel(double repairLevel)
    {
        int count = 0;
        foreach (var threshold in _cfg.BrokenPartThresholds)
        {
            if (threshold < repairLevel) break;
            count++;
        }
        return count;
    }

    /// <summary>How many parts are damaged now, from the Repair level.</summary>
    public int NumDamagedParts() => NumDamagedPartsForRepairLevel(GetNeedLevel(NeedId.Repair));
}

/// <summary>
/// The engine's <c>NeedsManager</c> (0x0069210C..0x00696D70): owns the state and configs, decays every
/// <c>DecayPeriodSeconds</c> (<c>Update</c> → <c>ApplyDecayAllNeeds</c>, skipped for a need within its fullness
/// cooldown after being filled), applies action deltas (<c>RegisterNeedsActionCompleted</c>: "needs.action_completed",
/// deltas ± range, <c>DetectBracketChangeForDas</c>), and remembers which severe states were expressed
/// (<c>BehaviorPlayAnimOnNeedsChange</c> / <c>ExpressNeeds</c> read and clear it). The stars / sparks economy
/// (<c>UpdateStarsState</c>, <c>RewardSparksForFreeplay</c>) is the app's; a freeplay sparks award is modelled only
/// as the <see cref="SparksRewardPending"/> flag <c>EarnedSparks</c> reads.
///
/// Persistence is per robot. <c>NeedsFilenameFromSerialNumber</c> 0x00695224 builds a name from a prefix,
/// an underscore (0x006952FC) and the serial number, with ".json" on the end (0x00BE5869);
/// <c>WriteToDevice</c> 0x00693BB0 writes the levels with a timestamp from <c>system_clock::now()</c>
/// divided by a million - seconds - and <c>ApplyDecayForTimeSinceLastDeviceWrite</c> 0x00695304 decays
/// for the time that has passed when the file is read back. <c>InitAfterSerialNumberAcquired</c>,
/// <c>StartReadFromRobot</c> and <c>InitAfterReadFromRobotAttempt</c> order the startup around it.
/// <see cref="Save"/> and <see cref="Load"/> are that arrangement; where the file lives is the host's
/// business, as it is the app's in the engine.
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

    /// <summary>Raised when a pending freeplay sparks reward is handed to the app.</summary>
    public event Action? SparksRewardAwarded;

    /// <summary>
    /// <c>NeedsManager::SparksRewardCommunicatedToUser</c> 0x00696E64: clears the pending flag (+0x3d8) and
    /// sends <c>FreeplaySparksAwarded</c> with the amount at +0x3dc to the app. Two things call it - an
    /// activity ending with a reward still pending (<c>IActivity::OnDeselected</c> 0x005B3548) and the
    /// EarnedSparks behaviour stopping (<c>BehaviorEarnedSparks::StopInternal</c> 0x005DAF7C) - plus
    /// <c>RegisterNeedsActionCompleted</c> itself, which sends a pending reward before creating a new one.
    /// How many sparks are awarded is the app's economy, so only the flag and the event are here.
    /// </summary>
    public void SparksRewardCommunicatedToUser()
    {
        if (!SparksRewardPending) return;
        SparksRewardPending = false;
        Log?.Invoke("needs.freeplay_sparks_awarded");
        SparksRewardAwarded?.Invoke();
    }
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
        if (d.FreeplaySparksRewardWeight > 0)
        {
            // "About to create freeplay sparks reward message but there was a pending one, so sending that
            // one now" (0x006963C0): the pending reward goes out before the new one replaces it.
            if (SparksRewardPending)
            {
                Log?.Invoke("needs.sparks_reward_pending_sent_first");
                SparksRewardCommunicatedToUser();
            }
            SparksRewardPending = true;
        }
        DetectBracketChanges();
        return true;
    }

    /// <summary>The severe (Critical) state of a need has been expressed (the get-in played).</summary>
    /// <summary>
    /// The name the engine gives one robot's needs file: the prefix, an underscore, the serial and
    /// ".json" (<c>NeedsFilenameFromSerialNumber</c> 0x00695224). The prefix is a runtime path in the
    /// engine, so only the tail is fixed here.
    /// </summary>
    public static string FileNameForSerial(uint serialNumber) => $"needs_{serialNumber}.json";

    /// <summary>
    /// Writes the levels and the moment they were written, as <c>WriteToDevice</c> 0x00693BB0 does: the
    /// timestamp is whole seconds (the engine divides <c>system_clock::now()</c> by a million).
    /// </summary>
    public void Save(string path, long? unixTimeSec = null)
    {
        var doc = new Dictionary<string, object>
        {
            ["writtenAtSec"] = unixTimeSec ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["Repair"] = State.GetNeedLevel(NeedId.Repair),
            ["Energy"] = State.GetNeedLevel(NeedId.Energy),
            ["Play"] = State.GetNeedLevel(NeedId.Play),
        };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(doc));
    }

    /// <summary>
    /// Reads a file written by <see cref="Save"/> and, as
    /// <c>ApplyDecayForTimeSinceLastDeviceWrite</c> 0x00695304 does, decays the levels for the time that
    /// has passed since it was written. Returns false when there is no file to read.
    /// </summary>
    public bool Load(string path, long? unixTimeSec = null, bool applyElapsedDecay = true)
    {
        if (!File.Exists(path)) return false;
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
            if (root.TryGetProperty(n.ToString(), out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number)
                State.SetNeedLevel(n, v.GetDouble());

        if (applyElapsedDecay && root.TryGetProperty("writtenAtSec", out var w) && w.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            long now = unixTimeSec ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            double elapsed = now - w.GetInt64();
            if (elapsed > 0) State.ApplyDecay(Decay, elapsed, Connected);
        }
        DetectBracketChanges();
        return true;
    }

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
