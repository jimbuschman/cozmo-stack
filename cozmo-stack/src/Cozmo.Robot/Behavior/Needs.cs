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
    // fidelity: M15-008
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
/// <c>Anki.Cozmo.NeedsActionId</c> (Unity <c>unity/scripts/csharp/Anki.Cozmo/NeedsActionId.cs</c>): the
/// <c>actionCausingTheUpdate</c> of a <c>NeedsState</c>. <c>SendNeedsStateToGame</c> 0x0069383C takes one;
/// the decay tick passes <c>Decay</c> (C1 §2).
/// </summary>
public enum NeedsActionId { NoAction = 0, Decay = 1 }

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
/// starting at one: for each need it takes the level and walks that need's modifier list, <b>sorted in
/// descending threshold order</b> (0x00691040), and applies the <b>single first entry</b> whose threshold is
/// at or below the level (<c>vcmpe</c> at 0x0069C270, <c>bge</c> at 0x0069C278); it does not combine every
/// matching entry. <c>ApplyDecayAllNeeds</c> 0x00695CFE asks for those multipliers once and hands them to
/// <c>NeedsState::ApplyDecay</c>.
///
/// In the shipped file the entries for Repair are thresholds 0.5 (Energy x1), 0.3 (Play x1), 0.03 (Play x2)
/// and 0 (Play x1): at a Repair level of 0.03 the Play entry with multiplier 2 is the first threshold at or
/// below the level, and below that the trailing 0 entry wins instead.
/// </summary>
public sealed record DecayConfig(IReadOnlyDictionary<NeedId, IReadOnlyList<(double Threshold, double PerMinute)>> Connected,
                                 IReadOnlyDictionary<NeedId, IReadOnlyList<(double Threshold, double PerMinute)>> Unconnected,
                                 IReadOnlyDictionary<NeedId, IReadOnlyList<(double Threshold, NeedId Other, double Multiplier)>>? Modifiers = null)
{
    /// <summary>
    /// The three multipliers <c>GetDecayMultipliers</c> works out from the current levels: one per need,
    /// each from the single first modifier entry (in descending threshold order) whose threshold is at or
    /// below the source need's level. An entry with threshold 0 always matches when nothing larger does.
    /// </summary>
    // fidelity: M15-004
    public IReadOnlyDictionary<NeedId, double> DecayMultipliers(Func<NeedId, double> level)
    {
        var result = new Dictionary<NeedId, double> { [NeedId.Repair] = 1, [NeedId.Energy] = 1, [NeedId.Play] = 1 };
        if (Modifiers is null) return result;
        foreach (var (source, entries) in Modifiers)
        {
            double l = level(source);
            // 0x00691040 sorts the modifier list descending by threshold; 0x0069C270/0x0069C278 stop at the
            // first entry whose threshold is <= the level and apply only that entry.
            foreach (var (threshold, other, multiplier) in entries.OrderByDescending(e => e.Threshold))
            {
                if (threshold > l) continue;
                result[other] *= multiplier;
                break;
            }
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
    /// <summary>
    /// <c>NeedsState</c> +0x70: the cached bracket per need, refreshed by <c>UpdateCurNeedsBrackets</c> while
    /// the dirty byte at +0x88 is set (0x0069C12C).
    /// </summary>
    private readonly int[] _curBrackets = new int[3];
    private bool _bracketsDirty = true;

    public NeedsState(NeedsConfig cfg)
    {
        _cfg = cfg;
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play }) _levels[(int)n] = cfg.InitialLevels.GetValueOrDefault(n, 1);
    }

    public double GetNeedLevel(NeedId n) => _levels[(int)n];
    public void SetNeedLevel(NeedId n, double level)
    {
        _levels[(int)n] = Math.Clamp(level, _cfg.MinimumNeedLevel, _cfg.MaximumNeedLevel);
        _bracketsDirty = true;                 // the engine dirties +0x88 whenever a level changes
    }

    /// <summary>The four shipped bracket thresholds for a need, in order: Full, Normal, Warning, Critical.</summary>
    private (double Full, double Normal, double Warning, double Critical) Thresholds(NeedId n) => _cfg.Brackets[n];

    /// <summary>
    /// <c>NeedsState::UpdateCurNeedsBrackets</c> 0x0069C12C: for each need, scan its shipped threshold
    /// vector in order and take the first index whose threshold is at or below the level, or the last index
    /// when none is (the Critical entry is 0, so the last index is the fall-through). The result is stored in
    /// the +0x70 cache and the dirty byte is cleared.
    /// </summary>
    // fidelity: M15-001
    public void UpdateCurNeedsBrackets()
    {
        if (!_bracketsDirty) return;
        for (int i = 0; i < 3; i++)
        {
            var (full, normal, warning, critical) = Thresholds((NeedId)i);
            double level = _levels[i];
            var thresholds = new[] { full, normal, warning, critical };
            int index = thresholds.Length - 1;
            for (int j = 0; j < thresholds.Length; j++)
                if (thresholds[j] <= level) { index = j; break; }
            _curBrackets[i] = index;
        }
        _bracketsDirty = false;
    }

    /// <summary>
    /// <c>NeedsState::GetNeedBracket</c> 0x0069CBCC: refresh the cache, then return it. An invalid need warns
    /// and returns 4 (there are only three needs).
    /// </summary>
    // fidelity: M15-001
    public NeedBracketId GetNeedBracket(NeedId n) => GetNeedBracket((int)n);

    /// <summary>The integer form the engine uses; an index outside 0..2 warns and returns 4.</summary>
    // fidelity: M15-001
    public NeedBracketId GetNeedBracket(int need)
    {
        if (need is < 0 or > 2) { Warn?.Invoke($"warning: NeedsState.GetNeedBracket: invalid need {need}"); return (NeedBracketId)4; }
        UpdateCurNeedsBrackets();
        return (NeedBracketId)_curBrackets[need];
    }

    /// <summary>
    /// <c>NeedsState::IsNeedAtBracket</c> 0x0069CD80: refresh the cache, then compare. An invalid need errors
    /// and returns false.
    /// </summary>
    // fidelity: M15-001
    public bool IsNeedAtBracket(NeedId n, NeedBracketId b)
    {
        if ((int)n is < 0 or > 2) { Warn?.Invoke($"error: NeedsState.IsNeedAtBracket: invalid need {(int)n}"); return false; }
        return GetNeedBracket(n) == b;
    }

    /// <summary>The engine's warning sink for an invalid need index.</summary>
    public event Action<string>? Warn;

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
    /// <summary>
    /// <c>+0x1FC</c>: the clock time a need was filled to Full, and <c>+0x208</c> its fullness-cooldown
    /// deadline (<c>fill + cooldown</c>). While the deadline is in the future <c>ApplyDecayAllNeeds</c>
    /// skips the need (0x00695D4C..0x00695D58); once it has passed, the cooldown window
    /// (<c>deadline - start</c>) is added to <c>+0x1E4</c> so only the time outside it decays
    /// (0x00695D5A..0x00695D6A, 0x00695D84).
    /// </summary>
    private readonly Dictionary<NeedId, double> _fullnessStartSec = new();
    private readonly Dictionary<NeedId, double> _fullnessDeadlineSec = new();
    /// <summary>
    /// <c>+0x1E4</c>: the clock time each need last decayed. <c>ApplyDecayAllNeeds</c> 0x00695CFE passes
    /// <c>now - this[need].lastDecay</c> to <c>NeedsState::ApplyDecay</c> and stores <c>+0x1E4 = now</c>;
    /// a skipped need keeps its value so the next decay covers the whole gap.
    /// </summary>
    private readonly Dictionary<NeedId, double> _lastDecaySec = new();
    /// <summary>
    /// The pause/disconnect state (<c>+0x1D5</c>, <c>+0x3B0</c>, <c>+0x3B4</c>, <c>+0x1D8</c>,
    /// <c>+0x1E4</c>, <c>+0x208</c>, <c>+4</c>) is written by the engine-thread callbacks
    /// (<c>Engine.ConnectionResponse</c>, <c>Engine.RobotDisconnected</c>) and read/written by the tick.
    /// A short lock on this gate makes the two threads agree; the approach is a lock, not a marshal to the
    /// tick, so the transition is visible as soon as the callback returns.
    /// </summary>
    private readonly object _gate = new();
    private readonly HashSet<NeedId> _severeExpressed = new();
    private readonly Dictionary<NeedId, NeedBracketId> _prevBrackets = new();
    /// <summary><c>+0x1d5</c>: while set, <c>Update</c> returns at once (0x00695CA4).</summary>
    private bool _paused;
    /// <summary><c>+0x3b0</c>: the next decay time; <c>Update</c> adds the interval <c>+0x130</c> each time it passes.</summary>
    private double _nextDecaySec;
    /// <summary><c>+0x3b4</c>: the time still owed to the decay schedule when paused (0x00695EC2).</summary>
    private double _pausedRemainingSec;
    /// <summary><c>+0x1cc</c>/<c>+0x34</c>: the previous and current robot serial (the serial-dispatch edge of M15-014 is the gap).</summary>
    private uint _previousSerial, _serial;
    /// <summary><c>+0x1d4</c>: whether a robot is connected (the flag <c>Update</c> passes as <c>robot != 0</c>).</summary>
    private bool _robotConnected = true;
    /// <summary>The clock time the current pause began (<c>+0x1d8</c>, 0x00695EB6).</summary>
    private double _pausedAtSec;
    /// <summary><c>+0x18/+0x1c</c>: the clock time of the last disconnect (0x00695908).</summary>
    private double _lastDisconnectSec;
    /// <summary>
    /// The per-need pause flags at <c>+0x1dc</c>, written by <c>HandleMessage&lt;SetNeedsPauseStates&gt;</c>
    /// 0x00698918; <c>ApplyDecayAllNeeds</c> skips a paused need (0x00695D36).
    /// </summary>
    private readonly bool[] _needPaused = new bool[3];
    /// <summary><c>this+8/+0xc</c>: the stored write time for the 61 ms rate limiter (0x00695DD2).</summary>
    private double _lastWriteSec;
    /// <summary><c>+0x1b8</c>: the needs levels copied at disconnect before the DAS bracket check (0x00695930).</summary>
    private double[]? _disconnectSnapshot;

    public NeedsManager(Func<double> clockSec, NeedsConfig? config = null, DecayConfig? decay = null, IReadOnlyDictionary<string, NeedsActionDelta>? actions = null, Random? random = null)
    {
        _clockSec = clockSec;
        Config = config ?? NeedsConfig.Default;
        Decay = decay ?? new DecayConfig(new Dictionary<NeedId, IReadOnlyList<(double, double)>>(), new Dictionary<NeedId, IReadOnlyList<(double, double)>>());
        Actions = actions ?? new Dictionary<string, NeedsActionDelta>();
        State = new NeedsState(Config);
        Random = random ?? new Random();
        State.Warn += m => Log?.Invoke(m);
        double now = clockSec();
        _nextDecaySec = now + Config.DecayPeriodSeconds;   // the first interval runs from Init
        // +0x1E4 is initialised at construction, so the first decay is one period's worth.
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
        {
            _lastDecaySec[n] = now;
            _prevBrackets[n] = State.GetNeedBracket(n);
        }
    }

    public NeedsConfig Config { get; }
    public DecayConfig Decay { get; }
    public IReadOnlyDictionary<string, NeedsActionDelta> Actions { get; }
    public NeedsState State { get; }
    public Random Random { get; }
    /// <summary>Whether the robot is connected (the connected decay rates apply); <c>Update</c> passes <c>robot != 0</c>.</summary>
    public bool Connected { get { lock (_gate) return _robotConnected; } set { lock (_gate) _robotConnected = value; } }
    /// <summary>Whether the needs manager is paused (<c>+0x1d5</c>).</summary>
    public bool IsPaused { get { lock (_gate) return _paused; } }
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

    /// <summary>
    /// <c>NeedsManager::Update(now)</c> 0x00695C9C: return at once while paused (+0x1d5, 0x00695CA4);
    /// update the local notifications; when the accumulator (+0x3b0) has reached now, add the decay interval
    /// (+0x130), <c>ApplyDecayAllNeeds(robot != 0)</c> (0x00695CE4), <c>SendNeedsStateToGame(Decay)</c>
    /// (0x00695CEC), then the tail <c>PossiblyWriteToDevice</c>.
    /// </summary>
    // fidelity: M15-001
    public void Update()
    {
        lock (_gate)
        {
            if (_paused) return;
            LocalNotificationsUpdate?.Invoke();
            double now = _clockSec();
            if (_nextDecaySec > now) return;
            _nextDecaySec += Config.DecayPeriodSeconds;
            ApplyDecayAllNeeds(_robotConnected);
            SendNeedsStateToGame(NeedsActionId.Decay);
            PossiblyWriteToDevice();
        }
    }

    /// <summary>
    /// <c>NeedsManager::ApplyDecayAllNeeds(connected)</c> 0x00695CFE: <c>GetDecayMultipliers</c> is asked once
    /// from the levels as they stand and the same three multipliers are used for the whole pass, then each
    /// need decays by its connected or unconnected rate over <c>now - +0x1E4</c> (its own last decay), not a
    /// fixed period. Two per-need skips run in the loop (C1 §2): the per-need pause flag at <c>+0x1dc</c>
    /// (0x00695D36), written by the <c>SetNeedsPauseStates</c> message (0x00698918), and the fullness-cooldown
    /// deadline at <c>+0x208</c> (0x00695D4C..0x00695D58, 0x00695D84), written by
    /// <c>StartFullnessCooldownForNeed</c> 0x006970AC as <c>now + config value</c> from the fill time
    /// <c>+0x1FC</c>. A need still inside its cooldown is skipped and keeps its <c>+0x1E4</c>; once the
    /// deadline has passed the cooldown window <c>+0x208 - +0x1FC</c> is added to <c>+0x1E4</c> before the
    /// decay (0x00695D5A..0x00695D6A), so only the time outside the window decays. <c>+0x1E4 = now</c> after
    /// a decay.
    /// </summary>
    // fidelity: M15-001
    public void ApplyDecayAllNeeds(bool connected)
    {
        lock (_gate)
        {
            double now = _clockSec();
            var multipliers = Decay.DecayMultipliers(n => State.GetNeedLevel(n));
            foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
            {
                if (_needPaused[(int)n]) continue;                                 // 0x00695D36
                if (_fullnessDeadlineSec.TryGetValue(n, out var deadline))
                {
                    if (now <= deadline) continue;                                  // 0x00695D4C..0x00695D58
                    // 0x00695D5A..0x00695D6A: +0x1E4 += (+0x208 - +0x1FC), so the cooldown window
                    // (the fill time through the deadline) is excluded from the decay; then the passed
                    // deadline and start are cleared (0x00695D84).
                    double start = _fullnessStartSec.GetValueOrDefault(n, deadline);
                    _lastDecaySec[n] = _lastDecaySec.GetValueOrDefault(n, now) + (deadline - start);
                    _fullnessDeadlineSec.Remove(n);
                    _fullnessStartSec.Remove(n);
                }
                double elapsed = now - _lastDecaySec.GetValueOrDefault(n, now);      // 0x00695D8A..0x00695D96
                if (elapsed > 0)
                {
                    double rate = Decay.RatePerMinute(n, State.GetNeedLevel(n), connected) * multipliers[n];
                    State.ApplyDelta(n, -rate * elapsed / 60.0);
                }
                _lastDecaySec[n] = now;
            }
            DetectBracketChanges();
        }
    }

    /// <summary>
    /// The per-need pause flag at <c>+0x1dc</c>, written by
    /// <c>HandleMessage&lt;SetNeedsPauseStates&gt;</c> 0x00698918. A paused need is skipped by
    /// <see cref="ApplyDecayAllNeeds"/> (0x00695D36). This is the seam the unbuilt message will call.
    /// </summary>
    // fidelity: M15-001
    public void SetNeedPaused(NeedId need, bool paused) { lock (_gate) _needPaused[(int)need] = paused; }

    /// <summary>Whether a need's <c>+0x1dc</c> pause flag is set.</summary>
    // fidelity: M15-001
    public bool IsNeedPaused(NeedId need) { lock (_gate) return _needPaused[(int)need]; }

    /// <summary>
    /// <c>NeedsManager::SendNeedsStateToGame(action)</c> 0x0069383C: refreshes the brackets, builds the
    /// level/bracket/damaged-part vectors and sends one <c>MessageEngineToGame</c> carrying a
    /// <c>NeedsState</c> whose <c>actionCausingTheUpdate</c> is the argument. The wire message is the app's;
    /// the stack exposes the action so a host can send it.
    /// </summary>
    // fidelity: M15-001
    public void SendNeedsStateToGame(NeedsActionId action)
    {
        State.UpdateCurNeedsBrackets();   // 0x0069383C refreshes the brackets before building the vectors
        NeedsStateSent?.Invoke(action);
    }

    /// <summary>Raised by <see cref="SendNeedsStateToGame"/>; the action is <c>Decay</c> on a decay tick.</summary>
    public event Action<NeedsActionId>? NeedsStateSent;

    /// <summary>Raised at the top of <see cref="Update"/> (the engine's <c>LocalNotifications::Update</c>).</summary>
    public event Action? LocalNotificationsUpdate;

    /// <summary>
    /// <c>NeedsManager::PossiblyWriteToDevice</c> 0x00695DC4: a 61 ms rate limiter. The engine builds
    /// <c>0x03A2C940 = 61,000,000</c> ns (0x00695DD2/0x00695DDA); when the elapsed time since the stored
    /// write time (<c>this+8/+0xc</c>) is at least that it stores now and calls
    /// <c>WriteToDevice(this, false)</c> (0x00695DFA), otherwise it returns.
    /// The file path is host state; the serial-dispatch edge that would choose it is the M15-014
    /// RECOVERABLE_GAP, so the host supplies the path and the write only happens when one is set.
    /// </summary>
    // fidelity: M15-001
    public void PossiblyWriteToDevice()
    {
        double now = _clockSec();
        if (now - _lastWriteSec < WriteThrottleSec) return;
        _lastWriteSec = now;
        WriteToDevice?.Invoke(false);
    }

    /// <summary>The engine's 61,000,000 ns write throttle, in seconds.</summary>
    public const double WriteThrottleSec = 0.061;

    /// <summary>
    /// The host's write seam. The forced flag is the engine's second argument: <c>true</c> from
    /// <c>SetPaused</c> and the disconnect path, <c>false</c> from <see cref="PossiblyWriteToDevice"/>.
    /// </summary>
    // fidelity: M15-001
    // fidelity: M15-016
    public Action<bool>? WriteToDevice { get; set; }

    /// <summary>
    /// <c>NeedsManager::LocalNotifications::SetPaused(paused)</c> 0x00695F6C: the local-notification pause
    /// seam. The app-facing notification wire is unbuilt.
    /// </summary>
    // fidelity: M15-016
    public Action<bool>? LocalNotificationsSetPaused { get; set; }

    /// <summary>
    /// <c>NeedsManager::SendNeedsPauseStateToGame()</c> 0x00695F78: the pause-state broadcast. The
    /// app-facing wire is unbuilt.
    /// </summary>
    // fidelity: M15-016
    public Action? SendNeedsPauseStateToGame { get; set; }

    /// <summary>
    /// <c>NeedsManager::SendNeedsLevelsDasEvent(reason)</c> 0x0069594C: the DAS level broadcast. The
    /// app-facing DAS wire is unbuilt; the disconnect path calls it with <c>"disconnect"</c>.
    /// </summary>
    // fidelity: M15-016
    public Action<string>? SendNeedsLevelsDasEvent { get; set; }

    /// <summary>
    /// <c>NeedsManager::InitAfterConnection</c> 0x00694384: the robot pointer is set (the connected flag
    /// <c>Update</c> reads) and <c>+0x1d4</c>/<c>+0x3d0</c> become 1. The serial arrives by a separate edge
    /// (M15-014's recoverable gap).
    /// </summary>
    // fidelity: M15-016
    public void InitAfterConnection() { lock (_gate) _robotConnected = true; }

    /// <summary>
    /// <c>NeedsManager::OnRobotDisconnected</c> 0x00695908 (C1 §6): write the disconnect timestamp to
    /// <c>+0x18/+0x1c</c>; clear the serial state <c>+0x30</c>; if not paused call
    /// <c>WriteToDevice(this, true)</c> (0x00695924..0x0069592A); clear the robot pointer <c>+4</c>;
    /// snapshot the needs into <c>+0x1b8</c>; <c>DetectBracketChangeForDas(true)</c> (0x0069593C); and
    /// <c>SendNeedsLevelsDasEvent("disconnect")</c> (0x0069594C). There is <b>no</b>
    /// <c>SendNeedsStateToGame</c> here.
    /// </summary>
    // fidelity: M15-016
    public void OnRobotDisconnected()
    {
        lock (_gate)
        {
            _lastDisconnectSec = _clockSec();
            _serial = 0;                                          // clear the serial state (+0x30)
            if (!_paused) WriteToDevice?.Invoke(true);            // forced write (0x00695924..0x0069592A)
            _robotConnected = false;                              // clear the robot pointer (+4)
            _disconnectSnapshot = new[] { State.GetNeedLevel(NeedId.Repair), State.GetNeedLevel(NeedId.Energy), State.GetNeedLevel(NeedId.Play) };
            DetectBracketChangeForDas(true);
            SendNeedsLevelsDasEvent?.Invoke("disconnect");
        }
    }

    /// <summary>
    /// <c>NeedsManager::DetectBracketChangeForDas(force)</c> 0x00695958: for each need the engine enters the
    /// event branch when the cached bracket (<c>+0x214</c>) differs from the current one <b>or</b>
    /// <paramref name="force"/> is set. On <paramref name="force"/> it emits for all three needs even when
    /// unchanged and does <b>not</b> update the cached bracket; the disconnect path passes <c>true</c>. The
    /// app-facing DAS wire is unbuilt, so the host observes <see cref="BracketChanged"/>.
    /// </summary>
    // fidelity: M15-016
    public void DetectBracketChangeForDas(bool force) => DetectBracketChanges(force);

    /// <summary><c>+0x18/+0x1c</c>: the clock time of the last disconnect.</summary>
    // fidelity: M15-016
    public double LastDisconnectSec { get { lock (_gate) return _lastDisconnectSec; } }

    /// <summary><c>+0x1b8</c>: the needs levels copied at disconnect before the DAS bracket check.</summary>
    // fidelity: M15-016
    public IReadOnlyList<double>? DisconnectNeedsSnapshot { get { lock (_gate) return _disconnectSnapshot; } }

    /// <summary>
    /// <c>NeedsManager::SetPaused</c> 0x00695E04 (C1 §6): if the new state equals <c>+0x1d5</c> it logs
    /// <c>NeedsManager.SetPaused.Redundant</c> and returns with no send, write or notification
    /// (0x00695E0C..0x00695E12). Pausing stores <c>+0x1d5 = 1</c>, the pause time <c>+0x1d8 = now</c> and
    /// the owed decay time <c>+0x3b4 = +0x3b0 - now</c>, then <c>SendNeedsStateToGame(NoAction)</c> and the
    /// forced <c>WriteToDevice(this, true)</c>. Unpausing stores <c>+0x1d5 = 0</c>, shifts <c>+0x3b0</c> to
    /// <c>now + +0x3b4</c> and adds the pause duration to each need's schedule; it sends no state and writes
    /// nothing. Both non-redundant branches end with <c>LocalNotifications::SetPaused</c> and
    /// <c>SendNeedsPauseStateToGame</c> (0x00695F6C..0x00695F78).
    ///
    /// This is reached <b>only</b> from app game messages: SetGameBeingPaused (tag 85),
    /// SetNeedsPauseState (tag 201, live through the generated invoker <c>FUN_0069B0E2</c>; the named
    /// <c>HandleMessage&lt;SetNeedsPauseState&gt;</c> is dead), RegisterOnboardingComplete (tag 200) and
    /// EnterSdkMode/ExitSdkMode (tags 241/242). None of those layers is built here, so this stays the seam
    /// the game-message layer will call.
    /// </summary>
    // fidelity: M15-016
    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            if (paused == _paused) { Log?.Invoke("NeedsManager.SetPaused.Redundant"); return; }
            double now = _clockSec();
            if (paused)
            {
                _paused = true;
                _pausedAtSec = now;
                _pausedRemainingSec = _nextDecaySec - now;        // +0x3b4
                SendNeedsStateToGame(NeedsActionId.NoAction);
                WriteToDevice?.Invoke(true);
            }
            else
            {
                _paused = false;
                double pauseDuration = now - _pausedAtSec;
                _nextDecaySec = now + _pausedRemainingSec;        // +0x3b0 = now + +0x3b4
                // C1 §6: the engine adds pauseDuration to each need's +0x1e4 (last decay) and, when non-zero,
                // +0x208 (the fullness deadline) and +0x1FC (the fullness start). This stack keeps exactly
                // those three per-need times.
                foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
                {
                    if (_lastDecaySec.TryGetValue(n, out var last)) _lastDecaySec[n] = last + pauseDuration;
                    if (_fullnessStartSec.TryGetValue(n, out var start)) _fullnessStartSec[n] = start + pauseDuration;
                    if (_fullnessDeadlineSec.TryGetValue(n, out var deadline)) _fullnessDeadlineSec[n] = deadline + pauseDuration;
                }
            }
            LocalNotificationsSetPaused?.Invoke(paused);
            SendNeedsPauseStateToGame?.Invoke();
        }
    }

    /// <summary><c>+0x34</c>: the current robot serial, 0 until the serial-dispatch edge supplies it (M15-014).</summary>
    public uint SerialNumber => _serial;
    /// <summary><c>+0x1cc</c>: the previous serial, kept across a re-acquire.</summary>
    public uint PreviousSerialNumber => _previousSerial;

    /// <summary>
    /// <c>NeedsManager::InitAfterSerialNumberAcquired</c> 0x006943A0: keep the previous serial at +0x1cc,
    /// store the new one at +0x34, clear +0x1ca/+0x1c8 and start the read from the robot. The generated
    /// dispatch that would call this with a serial is the M15-014 RECOVERABLE_GAP; the read itself is the
    /// host's seam here.
    /// </summary>
    // fidelity: M15-014
    public void InitAfterSerialNumberAcquired(uint serial)
    {
        _previousSerial = _serial;
        _serial = serial;
        ReadFromRobot?.Invoke();
    }

    /// <summary>The host's read seam, <c>StartReadFromRobot</c> 0x0069449C; null while the serial edge is the gap.</summary>
    // fidelity: M15-014
    public Action? ReadFromRobot { get; set; }

    /// <summary><c>RegisterNeedsActionCompleted</c>: the action's deltas (± range) applied; unknown actions are ignored and logged.</summary>
    public bool RegisterNeedsActionCompleted(string actionId)
    {
        if (!Actions.TryGetValue(actionId, out var d)) { Log?.Invoke($"needs.action_completed_ignored {actionId}"); return false; }
        lock (_gate)
        {
        void Apply(NeedId n, double delta, double range)
        {
            if (delta == 0 && range == 0) return;
            double v = delta + (range > 0 ? (Random.NextDouble() * 2 - 1) * range : 0);
            State.ApplyDelta(n, v);
            // +0x1FC = the fill time; +0x208 = now + the configured fullness cooldown
            // (StartFullnessCooldownForNeed 0x006970AC).
            if (v > 0 && State.GetNeedBracket(n) == NeedBracketId.Full)
            {
                double filledAt = _clockSec();
                _fullnessStartSec[n] = filledAt;
                _fullnessDeadlineSec[n] = filledAt + Config.FullnessDecayCooldownSec.GetValueOrDefault(n, 0);
            }
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
    }

    /// <summary>
    /// The name the engine gives one robot's needs file: the fixed prefix <c>needsState_</c>, the decimal
    /// serial and <c>.json</c> (<c>NeedsFilenameFromSerialNumber</c> 0x00695224; the prefix is the static
    /// initialiser at 0x004D8DDC..0x004D8E10).
    /// </summary>
    // fidelity: M15-014
    public static string FileNameForSerial(uint serialNumber) => $"needsState_{serialNumber}.json";

    /// <summary>The fixed file the engine writes before a serial is known: <c>needsState.json</c>.</summary>
    // fidelity: M15-014
    public const string FixedFileName = "needsState.json";

    /// <summary>
    /// <c>NeedsManager::WriteToDevice</c> 0x00693BB0 writes one JSON object with the exact keys the static
    /// initialiser establishes (0x004D8DDC..0x004D9142): <c>_StateFileVersion</c>, <c>_DateTime</c>,
    /// <c>_SerialNumber</c>, <c>CurNeedLevel</c>, <c>PartIsDamaged</c>, <c>CurNeedsUnlockLevel</c>,
    /// <c>NumStarsAwarded</c>, <c>NumStarsForNextUnlock</c>, <c>TimeCreated</c>, <c>TimeLastStarAwarded</c>,
    /// <c>TimeLastDisconnect</c>, <c>TimeLastAppBackgrounded</c>, <c>OpenAppAfterDisconnect</c> and
    /// <c>ForceNextSong</c>. The stars/unlock/notification fields are the app economy's; only the levels,
    /// damaged parts, serial and timestamp are meaningful here.
    /// The serial-dispatch edge that would supply <paramref name="serialNumber"/> is the M15-014
    /// RECOVERABLE_GAP, so the caller passes it (0 when unknown).
    /// </summary>
    // fidelity: M15-014
    public void Save(string path, long? unixTimeSec = null, uint serialNumber = 0)
    {
        long now = unixTimeSec ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var levels = new[] { NeedId.Repair, NeedId.Energy, NeedId.Play }.Select(n => State.GetNeedLevel(n)).ToArray();
        var damaged = new[] { NeedId.Repair, NeedId.Energy, NeedId.Play }.Select(n => State.NumDamagedPartsForRepairLevel(State.GetNeedLevel(n)) > 0).ToArray();
        var doc = new Dictionary<string, object>
        {
            ["_StateFileVersion"] = 1,
            ["_DateTime"] = now,
            ["_SerialNumber"] = serialNumber,
            ["CurNeedLevel"] = levels,
            ["PartIsDamaged"] = damaged,
            ["CurNeedsUnlockLevel"] = 0,
            ["NumStarsAwarded"] = 0,
            ["NumStarsForNextUnlock"] = 0,
            ["TimeCreated"] = now,
            ["TimeLastStarAwarded"] = 0,
            ["TimeLastDisconnect"] = 0,
            ["TimeLastAppBackgrounded"] = 0,
            ["OpenAppAfterDisconnect"] = false,
            ["ForceNextSong"] = false,
        };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(doc));
    }

    /// <summary>
    /// Reads a file written by <see cref="Save"/> (the exact keys above) and, as
    /// <c>ApplyDecayForTimeSinceLastDeviceWrite</c> 0x00695304 does, decays the levels for the time that has
    /// passed since <c>_DateTime</c> was written. Returns false when there is no file to read.
    /// </summary>
    // fidelity: M15-014
    public bool Load(string path, long? unixTimeSec = null, bool applyElapsedDecay = true)
    {
        if (!File.Exists(path)) return false;
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (root.TryGetProperty("_SerialNumber", out var sn) && sn.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            _previousSerial = _serial;
            _serial = sn.GetUInt32();
        }
        if (root.TryGetProperty("CurNeedLevel", out var levels) && levels.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var needs = new[] { NeedId.Repair, NeedId.Energy, NeedId.Play };
            int i = 0;
            foreach (var v in levels.EnumerateArray())
            {
                if (i >= needs.Length) break;
                if (v.ValueKind == System.Text.Json.JsonValueKind.Number) State.SetNeedLevel(needs[i], v.GetDouble());
                i++;
            }
        }
        if (applyElapsedDecay && root.TryGetProperty("_DateTime", out var w) && w.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            long now = unixTimeSec ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            double elapsed = now - w.GetInt64();
            if (elapsed > 0) State.ApplyDecay(Decay, elapsed, _robotConnected);
        }
        DetectBracketChanges();
        return true;
    }

    public bool IsSevereExpressed(NeedId n) => _severeExpressed.Contains(n);
    public void SetSevereExpressed(NeedId n, bool expressed) { if (expressed) _severeExpressed.Add(n); else _severeExpressed.Remove(n); }

    private void DetectBracketChanges(bool force = false)
    {
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
        {
            var b = State.GetNeedBracket(n);
            // 0x00695958 enters the event branch when cached (+0x214) != current or force is set.
            if (b != _prevBrackets[n] || force)
            {
                Log?.Invoke($"need {n}: {_prevBrackets[n]} -> {b} (level {State.GetNeedLevel(n):F3})");
                if (b != NeedBracketId.Critical) _severeExpressed.Remove(n);
                BracketChanged?.Invoke(n, _prevBrackets[n], b);
                // The force path emits for every need and does not update the cached bracket (+0x214).
                if (!force) _prevBrackets[n] = b;
            }
        }
    }

    /// <summary>Test / tool hook: set a level directly and report the bracket change.</summary>
    public void SetLevel(NeedId n, double level) { State.SetNeedLevel(n, level); DetectBracketChanges(); }
}
