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

// fidelity: M15-017
/// <summary>
/// <c>Anki::Cozmo::NeedsStateOnRobot</c>: the binary blob the robot's NV key 0x194000 holds (Appendix H).
/// Little-endian; <c>Pack</c>/<c>Unpack</c> use the same field order and raw byte copies. The serialized
/// offsets are: version u32 @0, <c>timeLastWritten</c> u64 @4, <c>curNeedLevel[10]</c> i32 @0x0C,
/// <c>curNeedsUnlockLevel</c> i32 @0x34, <c>numStarsAwarded</c> i32 @0x38, <c>partIsDamaged[32]</c> u8 @0x3C,
/// <c>timeLastStarAwarded</c> u64 @0x5C, <c>onboardingStageCompleted</c> i32 @0x64,
/// <c>forceNextSong</c> i32 (UnlockId, not a bool) @0x68, <c>timeCreated</c> u64 @0x6C; total
/// <see cref="Size"/> = 0x74 (116 bytes).
/// </summary>
public sealed class NeedsStateOnRobot
{
    /// <summary><c>NeedsStateOnRobot::Size</c> 0x007848C8: 0x74 bytes.</summary>
    public const int Size = 0x74;
    /// <summary>The engine's ten needs (<c>curNeedLevel[10]</c>).</summary>
    public const int NeedCount = 10;
    /// <summary>The engine's 32 part-damage flags (<c>partIsDamaged[32]</c>).</summary>
    public const int PartCount = 32;

    public uint Version;
    public ulong TimeLastWritten;
    public readonly int[] CurNeedLevel = new int[NeedCount];
    public int CurNeedsUnlockLevel;
    public int NumStarsAwarded;
    public readonly byte[] PartIsDamaged = new byte[PartCount];
    public ulong TimeLastStarAwarded;
    public int OnboardingStageCompleted;
    public int ForceNextSong;
    public ulong TimeCreated;

    // fidelity: M15-017
    /// <summary><c>NeedsStateOnRobot::Pack</c> 0x0078479E: the version-5 layout, 116 bytes.</summary>
    public static byte[] Pack(NeedsStateOnRobot s)
    {
        var b = new byte[Size];
        WriteU32(b, 0x00, s.Version);
        WriteU64(b, 0x04, s.TimeLastWritten);
        for (int i = 0; i < NeedCount; i++) WriteI32(b, 0x0C + i * 4, s.CurNeedLevel[i]);
        WriteI32(b, 0x34, s.CurNeedsUnlockLevel);
        WriteI32(b, 0x38, s.NumStarsAwarded);
        for (int i = 0; i < PartCount; i++) b[0x3C + i] = s.PartIsDamaged[i];
        WriteU64(b, 0x5C, s.TimeLastStarAwarded);
        WriteI32(b, 0x64, s.OnboardingStageCompleted);
        WriteI32(b, 0x68, s.ForceNextSong);
        WriteU64(b, 0x6C, s.TimeCreated);
        return b;
    }

    // fidelity: M15-017
    /// <summary><c>NeedsStateOnRobot::Unpack</c> 0x007846B4: the version-5 layout.</summary>
    public static NeedsStateOnRobot Unpack(byte[] data) => UnpackVersioned(data, 5);

    // fidelity: M15-017
    /// <summary>
    /// The v1-4 conversion (Appendix H): the prefix is always present; a shorter layout's missing tail is
    /// zeroed; the converted struct's version is forced to 5. v2 adds <c>timeLastStarAwarded</c>, v3 adds
    /// <c>onboardingStageCompleted</c>, v4 adds <c>forceNextSong</c>, v5 adds <c>timeCreated</c>.
    /// </summary>
    public static NeedsStateOnRobot UnpackVersioned(byte[] data, int version)
    {
        var s = new NeedsStateOnRobot { Version = 5 };
        s.TimeLastWritten = ReadU64(data, 0x04);
        for (int i = 0; i < NeedCount; i++) s.CurNeedLevel[i] = ReadI32(data, 0x0C + i * 4);
        s.CurNeedsUnlockLevel = ReadI32(data, 0x34);
        s.NumStarsAwarded = ReadI32(data, 0x38);
        for (int i = 0; i < PartCount; i++) s.PartIsDamaged[i] = 0x3C + i < data.Length ? data[0x3C + i] : (byte)0;
        if (version >= 2) s.TimeLastStarAwarded = ReadU64(data, 0x5C);
        if (version >= 3) s.OnboardingStageCompleted = ReadI32(data, 0x64);
        if (version >= 4) s.ForceNextSong = ReadI32(data, 0x68);
        if (version >= 5) s.TimeCreated = ReadU64(data, 0x6C);
        return s;
    }

    private static void WriteU32(byte[] b, int o, uint v) => BitConverter.GetBytes(v).CopyTo(b, o);
    private static void WriteI32(byte[] b, int o, int v) => BitConverter.GetBytes(v).CopyTo(b, o);
    private static void WriteU64(byte[] b, int o, ulong v) => BitConverter.GetBytes(v).CopyTo(b, o);
    private static uint ReadU32(byte[] b, int o) => o + 4 <= b.Length ? BitConverter.ToUInt32(b, o) : 0;
    private static int ReadI32(byte[] b, int o) => o + 4 <= b.Length ? BitConverter.ToInt32(b, o) : 0;
    private static ulong ReadU64(byte[] b, int o) => o + 8 <= b.Length ? BitConverter.ToUInt64(b, o) : 0;
}

/// <summary>
/// One parsed needs persistence copy (C2 rows 3/11/13): the levels, the stored serial and the write
/// timestamp (<c>_DateTime</c>), plus the file version. The device file and the robot's NV item share the
/// same shape, so the resolver can compare them. The robot blob has no serial field (Appendix H), so its
/// <see cref="SerialNumber"/> is 0 and the resolver compares <c>+0x1CC</c> with <c>+0x34</c> instead.
/// </summary>
// fidelity: M15-014
public sealed record NeedsCopy(double[] Levels, uint SerialNumber, long DateTimeSec, int Version);

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
    /// <summary><c>+0x1cc</c>/<c>+0x34</c>: the previous and current robot serial (C2 row 9; the current one is <see cref="SerialNumber"/>).</summary>
    private uint _previousSerial, _serial;
    /// <summary><c>+0x1c8</c>: whether the robot NV read produced usable data (<c>FinishReadFromRobot</c>'s Boolean, C2 row 11).</summary>
    private bool _robotReadSucceeded;
    /// <summary><c>+0x1ca</c>: the robot copy needs rewriting (versions 0-4; C2 row 11).</summary>
    private bool _robotRewriteNeeded;
    /// <summary><c>+0x3d0</c>: a robot needs read is outstanding (set by <c>InitAfterConnection</c>, cleared by <c>StartReadFromRobot</c>'s failure or the callback; C2 rows 9-11).</summary>
    private bool _awaitingRobotData;
    /// <summary><c>+0x1c9</c>: the device copy is present (<c>InitInternal</c>'s attempt result, re-marked on a resolved device write; C2 rows 1/13).</summary>
    private bool _deviceDataPresent;
    /// <summary>The parsed robot NV copy, or null (C2 row 11).</summary>
    private NeedsCopy? _robotCopy;
    /// <summary>The device copy loaded from a file - the startup fixed file or the per-serial alternate (C2 rows 3/12).</summary>
    private NeedsCopy? _deviceCopy;
    /// <summary>The current <c>NeedsState</c>'s <c>DateTime</c> (<c>this+8</c>): the copy last applied to the state (Appendix I4).</summary>
    private double _stateDateTimeSec;
    /// <summary><c>TimeLastAppBackgrounded</c>: cleared when a mismatched serial selects the robot data (C2 row 13).</summary>
    private double _lastAppBackgroundSec;
    /// <summary>
    /// <c>+0x30</c> <c>OpenAppAfterDisconnect</c> (J5): an int counter, not a bool. Read as <c>asInt</c>
    /// (0x00699A1C), written as <c>Value(int)</c> (0x00693CC6), incremented on a successful
    /// <c>AttemptReadFromDevice</c> (0x006936CE), reset to 0 by <c>OnRobotDisconnected</c> (0x00695922) and
    /// cleared when a mismatched serial selects the robot data (C2 row 13).
    /// </summary>
    private int _openAppAfterDisconnect;
    /// <summary><c>+0x1c0/+0x1c4</c>: the time the last robot write was built with (Appendix G Q1).</summary>
    private double _lastWriteToRobotSec;
    /// <summary><c>_errG</c>: set when a robot write fails (Appendix G Q1).</summary>
    private bool _writeToRobotError;
    /// <summary><c>+0x1cb</c>: the device file's <c>versionUpdated</c> out-flag, set when an old device format needs rewriting (C2 row 1, Appendix I1).</summary>
    private bool _deviceVersionUpdated;
    /// <summary><c>+0x4</c>: whether a robot is connected (the flag <c>Update</c> passes as <c>robot != 0</c>; J12). <c>+0x1d4</c> is a separate field (the notification gate <c>LocalNotifications::ShouldBeRegistered</c> reads, J12) and is not modelled here.</summary>
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
    /// <summary>
    /// <c>+0x1f0/+0x1f4/+0x1f8</c> (J9): the clock time a need's per-need pause began. The constructor zeroes
    /// it (0x00692264); <c>HandleMessage&lt;SetNeedsPauseStates&gt;</c> sets it to <c>+0x3ac</c> when a need
    /// becomes paused (0x00698A48) and reads it when the pause is unwound (0x00698A18); <c>SetPaused</c>'s
    /// unpause adds the pause duration always (0x00695F40). That game message is unbuilt, so this stack only
    /// shifts the field.
    /// </summary>
    private readonly double[] _needPauseStartSec = new double[3];
    /// <summary>
    /// <c>+0x214/+0x218/+0x21c</c> (J10): the clock time a need's bracket last changed. <c>InitReset</c>
    /// seeds it to <c>+0x3ac</c> (0x006935BC); <c>DetectBracketChangeForDas</c> reads it (0x006959D6),
    /// computes the DAS elapsed <c>now - +0x214</c> (0x006959FA) and writes <c>+0x214 = now</c> only when
    /// <c>force == 0</c> (0x00695BA2); <c>SetPaused</c>'s unpause adds the pause duration always
    /// (0x00695F66).
    /// </summary>
    private readonly double[] _bracketChangedSec = new double[3];
    /// <summary><c>this+8/+0xc</c>: the stored write time for the 61 s rate limiter (0x00695DD2; J14).</summary>
    private double _lastWriteSec;
    /// <summary>
    /// <c>+0x1b8/+0x1bc</c> (J4): the <c>NeedsState</c> <c>DateTime</c> (<c>+8/+0xC</c>) snapshotted at a
    /// successful device read (0x006936B6) and at disconnect (0x00695934). The resolver compares the robot
    /// copy's <c>timeLastWritten</c> (<c>+0x98</c>) against it (decomp <c>00694608.c:309-357</c>, around
    /// 0x0069493C), not against the device copy's own timestamp.
    /// </summary>
    private double _deviceTimestampSnapshotSec;

    public NeedsManager(Func<double> clockSec, NeedsConfig? config = null, DecayConfig? decay = null, IReadOnlyDictionary<string, NeedsActionDelta>? actions = null, Random? random = null)
    {
        _clockSec = clockSec;
        Config = config ?? NeedsConfig.Default;
        Decay = decay ?? new DecayConfig(new Dictionary<NeedId, IReadOnlyList<(double, double)>>(), new Dictionary<NeedId, IReadOnlyList<(double, double)>>());
        Actions = actions ?? new Dictionary<string, NeedsActionDelta>();
        State = new NeedsState(Config);
        Random = random ?? new Random();
        State.Warn += m => Log?.Invoke(m);
        InitReset(clockSec());
    }

    /// <summary>
    /// <c>NeedsManager::InitReset</c> 0x006934A8..0x006935E0 (J2): seed the decay schedule and the per-need
    /// clocks from the passed time. <c>+0x3B0 = +0x130 + now</c> (the first interval runs from Init), each
    /// need's <c>+0x1E4</c> (last decay) and <c>+0x214</c> (bracket-change clock) is the passed time, and the
    /// bracket cache <c>+0x70</c> is refreshed. <c>InitReset</c>'s other writes are already a fresh object's
    /// defaults: the fullness fields <c>+0x1FC</c>/<c>+0x208</c> are zero (this stack's fullness
    /// dictionaries are empty), the six per-need pause-flag bytes <c>+0x1DC..+0x1E1</c> are false, and
    /// <c>+0x1F0</c> is not written (J2/J9); the <c>__aeabi_memclr4(this+0x244, 0x158)</c> at 0x006935D6
    /// clears a region that is already zero on a fresh object.
    /// </summary>
    // fidelity: M15-014
    private void InitReset(double nowSec)
    {
        _nextDecaySec = nowSec + Config.DecayPeriodSeconds;
        // +0x1E4 is initialised here, so the first decay is one period's worth.
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
        {
            _lastDecaySec[n] = nowSec;
            _prevBrackets[n] = State.GetNeedBracket(n);
            _bracketChangedSec[(int)n] = nowSec;   // J10: InitReset seeds +0x214 = +0x3ac; +0x1f0 stays 0
        }
    }

    /// <summary>
    /// <c>NeedsManager::InitInternal</c> 0x00693444..0x00693492 (J1): <c>InitReset</c>, clear the device
    /// <c>versionUpdated</c> out-flag <c>+0x1CB</c> (0x0069345C) and the device-read result <c>+0x1C9</c>
    /// (0x00693462), attempt the fixed <c>needsState.json</c> device read (0x00693466), store its result at
    /// <c>+0x1C9</c> (0x0069346C), send the default state when it failed (0x00693476), force
    /// <c>WriteToDevice(true)</c> (0x0069347E), raise the <c>app_start</c> DAS event (0x00693486) and run
    /// <c>LocalNotifications::Generate</c> (tail branch 0x00693492). It does not touch the robot-rewrite
    /// flag <c>+0x1CA</c>.
    /// </summary>
    // fidelity: M15-014
    public void InitInternal(double nowSec)
    {
        InitReset(nowSec);
        lock (_gate)
        {
            _deviceVersionUpdated = false;      // +0x1CB = 0 (0x0069345C)
            _deviceDataPresent = false;         // +0x1C9 = 0 (0x00693462)
        }
        bool ok = AttemptReadFromDevice(FixedDeviceFilePath);      // 0x00693466
        lock (_gate) _deviceDataPresent = ok;   // +0x1C9 = (byte)result (0x0069346C)
        if (!ok) SendNeedsStateToGame(NeedsActionId.NoAction);   // 0x00693476
        WriteToDevice?.Invoke(true);                             // 0x0069347E
        SendNeedsLevelsDasEvent?.Invoke("app_start");            // 0x00693486
        LocalNotificationsGenerate?.Invoke();                    // 0x00693492
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
    /// <summary>
    /// Raised by <see cref="DetectBracketChangeForDas"/>; the fourth argument is the DAS elapsed
    /// <c>now - +0x214</c> (J10: read 0x006959D6, elapsed 0x006959FA).
    /// </summary>
    public event Action<NeedId, NeedBracketId, NeedBracketId, double>? BracketChanged;
    public event Action<string>? Log;

    // fidelity: M15-014
    public static NeedsManager FromObb(string obbRoot, Func<double> clockSec, Random? random = null, string? deviceDirectory = null)
    {
        var dir = Path.Combine(obbRoot, "assets", "cozmo_resources", "config", "engine");
        string? Read(string name) { var p = Path.Combine(dir, name); return File.Exists(p) ? File.ReadAllText(p) : null; }
        var cfg = Read("needs_config.json") is { } c ? NeedsConfig.Parse(c) : null;
        var decay = Read("needs_decay_config.json") is { } d ? DecayConfig.Parse(d) : null;
        var actions = Read("needs_action_config.json") is { } a ? NeedsActionDelta.Parse(a) : null;
        var needs = new NeedsManager(clockSec, cfg, decay, actions, random);
        // J3: the directory the engine's DataPlatform resolves ("nurture/") is the host's; the caller supplies it.
        needs.DeviceDirectory = deviceDirectory;
        // J7: InitInternal's forced WriteToDevice(true) (0x0069347E) must reach a real file when the host
        // supplied a device directory, so the seam is wired before InitInternal runs.
        if (deviceDirectory is not null) needs.WriteToDevice = needs.WriteDeviceFile;
        // NeedsManager::Init 0x00692574 ends by calling InitInternal 0x006926CE (J1/J14): the construction
        // analogue here, so a manager built from the OBB has run the startup device read and its tail.
        needs.InitInternal(clockSec());
        return needs;
    }

    /// <summary>
    /// <c>NeedsManager::Update(now)</c> 0x00695C9C: return at once while paused (+0x1d5, 0x00695CA4);
    /// update the local notifications; when the accumulator (+0x3b0) has reached now, add the decay interval
    /// (+0x130), <c>ApplyDecayAllNeeds(robot != 0)</c> (0x00695CE4), <c>SendNeedsStateToGame(Decay)</c>
    /// (0x00695CEC), then the tail <c>PossiblyWriteToDevice</c>. The engine passes
    /// <c>BaseStationTimer::GetCurrentTimeInSeconds</c> (M1-024, 0x004ED632..0x004ED640); the parameterless
    /// overload keeps the manager's own clock for direct callers.
    /// </summary>
    // fidelity: M15-001
    public void Update() => Update(_clockSec());

    // fidelity: M1-024
    /// <summary>The engine tick's <c>NeedsManager::Update(now)</c> with the tick's BaseStationTimer seconds.</summary>
    public void Update(double now)
    {
        lock (_gate)
        {
            if (_paused) return;
            LocalNotificationsUpdate?.Invoke();
            if (_nextDecaySec > now) return;
            _nextDecaySec += Config.DecayPeriodSeconds;
            ApplyDecayAllNeeds(_robotConnected, now);
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
    public void ApplyDecayAllNeeds(bool connected) => ApplyDecayAllNeeds(connected, _clockSec());

    /// <summary>The engine's decay pass on an explicit now, which <c>Update(now)</c> passes (M1-024).</summary>
    // fidelity: M1-024
    public void ApplyDecayAllNeeds(bool connected, double now)
    {
        lock (_gate)
        {
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
            DetectBracketChanges(nowOverride: now);
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

    /// <summary><c>+0x1f0</c> (J9): the clock time a need's per-need pause began; zero until a pause unwinds.</summary>
    // fidelity: M15-016
    public double NeedPauseStartSec(NeedId need) { lock (_gate) return _needPauseStartSec[(int)need]; }

    /// <summary><c>+0x214</c> (J10): the clock time a need's bracket last changed.</summary>
    // fidelity: M15-016
    public double BracketChangedSec(NeedId need) { lock (_gate) return _bracketChangedSec[(int)need]; }

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
    /// <c>NeedsManager::PossiblyWriteToDevice</c> 0x00695DC4: a 61-second rate limiter. The engine compares
    /// <c>system_clock::now() - this+8/+0xC</c> against <c>0x03A2C940 = 61,000,000</c> on the microsecond
    /// clock (<c>ApplyDecayForTimeSinceLastDeviceWrite</c> divides by 1,000,000 at 0x0069532C), so the
    /// throttle is 61 s; when the elapsed time is at least that it stores now and calls
    /// <c>WriteToDevice(this, false)</c> (0x00695DFA), otherwise it returns (J14).
    /// The file path is host state; the serial-dispatch edge (C2) supplies the per-serial path, and the host
    /// sets the seam, so the write only happens when one is set.
    /// </summary>
    // fidelity: M15-001
    public void PossiblyWriteToDevice()
    {
        double now = _clockSec();
        if (now - _lastWriteSec < WriteThrottleSec) return;
        _lastWriteSec = now;
        WriteToDevice?.Invoke(false);
    }

    /// <summary>The engine's 61,000,000-unit (microsecond) write throttle, 61 s (J14).</summary>
    // fidelity: M15-001
    public const double WriteThrottleSec = 61.0;

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
    /// <c>NeedsManager::SendTimeSinceBackgroundedDasEvent()</c> 0x0069770C..0x006977AA (J5): emits
    /// <c>needs.app_backgrounded_time</c> with the <c>+0x30 OpenAppAfterDisconnect</c> counter and the
    /// elapsed time since <c>+0x20/+0x24</c> (the last app background). It emits nothing when that time is
    /// zero (0x0069771E), which the caller guards. The DAS wire is the app's.
    /// </summary>
    // fidelity: M15-014
    public Action<int, double>? SendTimeSinceBackgroundedDasEvent { get; set; }

    /// <summary>
    /// <c>NeedsManager::InitInternal</c>'s tail calls <c>LocalNotifications::Generate</c> 0x0068CA9C
    /// (J1/J8): the feature gate, the notification cache and the next-generate time
    /// <c>+0x18 = NeedsManager[0x3AC] + 60.0</c>. The app-facing notification wire is unbuilt, so this is
    /// the seam.
    /// </summary>
    // fidelity: M15-014
    public Action? LocalNotificationsGenerate { get; set; }

    /// <summary>
    /// <c>NeedsManager::InitAfterConnection</c> 0x00694384: the robot pointer is set (the connected flag
    /// <c>Update</c> reads), <c>+0x1d4</c> becomes 1 and the robot-data flag <c>+0x3d0</c> becomes 1
    /// (C2 row 6). The serial arrives by a separate edge (C2 row 9, <see cref="InitAfterSerialNumberAcquired"/>).
    /// </summary>
    // fidelity: M15-014, M15-016
    public void InitAfterConnection() { lock (_gate) { _robotConnected = true; _awaitingRobotData = true; } }

    /// <summary>
    /// <c>NeedsManager::OnRobotDisconnected</c> 0x00695908 (C1 §6, J5): write the disconnect timestamp to
    /// <c>+0x18/+0x1c</c>; reset the <c>+0x30 OpenAppAfterDisconnect</c> counter to 0 (0x00695922); if not
    /// paused call <c>WriteToDevice(this, true)</c> (0x00695924..0x0069592A); clear the robot pointer
    /// <c>+4</c>; snapshot the state timestamp into <c>+0x1b8</c> (0x00695934); <c>DetectBracketChangeForDas(true)</c>
    /// (0x0069593C); and <c>SendNeedsLevelsDasEvent("disconnect")</c> (0x0069594C). There is <b>no</b>
    /// <c>SendNeedsStateToGame</c> here.
    /// </summary>
    // fidelity: M15-014, M15-016
    public void OnRobotDisconnected()
    {
        lock (_gate)
        {
            _lastDisconnectSec = _clockSec();
            _openAppAfterDisconnect = 0;                          // reset the +0x30 counter (J5, 0x00695922)
            if (!_paused) WriteToDevice?.Invoke(true);            // forced write (0x00695924..0x0069592A)
            _robotConnected = false;                              // clear the robot pointer (+4)
            _deviceTimestampSnapshotSec = _stateDateTimeSec;      // +0x1B8/+0x1BC = +8/+0xC (0x00695934)
            DetectBracketChangeForDas(true);
            SendNeedsLevelsDasEvent?.Invoke("disconnect");
        }
    }

    /// <summary>
    /// <c>NeedsManager::DetectBracketChangeForDas(force)</c> 0x00695958: for each need the engine enters the
    /// event branch when the cached bracket (<c>+0x214</c>) differs from the current one <b>or</b>
    /// <paramref name="force"/> is set. On <paramref name="force"/> it emits for all three needs even when
    /// unchanged and does <b>not</b> update the cached bracket; the disconnect path passes <c>true</c>. Each
    /// emission carries the DAS elapsed <c>now - +0x214</c> (J10). The app-facing DAS wire is unbuilt, so the
    /// host observes <see cref="BracketChanged"/>.
    /// </summary>
    // fidelity: M15-016
    public void DetectBracketChangeForDas(bool force) => DetectBracketChanges(force);

    /// <summary><c>+0x18/+0x1c</c>: the clock time of the last disconnect.</summary>
    // fidelity: M15-016
    public double LastDisconnectSec { get { lock (_gate) return _lastDisconnectSec; } }

    /// <summary><c>+0x1b8</c>: the device timestamp snapshotted at a device read or disconnect (J4).</summary>
    // fidelity: M15-014, M15-016
    public double DeviceTimestampSnapshotSec { get { lock (_gate) return _deviceTimestampSnapshotSec; } }

    /// <summary>
    /// <c>NeedsManager::SetPaused</c> 0x00695E04 (C1 §6): if the new state equals <c>+0x1d5</c> it logs
    /// <c>NeedsManager.SetPaused.Redundant</c> and returns with no send, write or notification
    /// (0x00695E0C..0x00695E12). Pausing stores <c>+0x1d5 = 1</c>, the pause time <c>+0x1d8 = now</c> and
    /// the owed decay time <c>+0x3b4 = +0x3b0 - now</c>, then <c>SendNeedsStateToGame(NoAction)</c> and the
    /// forced <c>WriteToDevice(this, true)</c>. Unpausing stores <c>+0x1d5 = 0</c>, shifts <c>+0x3b0</c> to
    /// <c>now + +0x3b4</c> and adds the pause duration to each need's <c>+0x1e4</c>, <c>+0x1f0</c> and
    /// <c>+0x214</c> always, and to <c>+0x208</c>/<c>+0x1fc</c> only when <c>+0x208 != 0</c> (J11); it sends
    /// no state and writes nothing. Both non-redundant branches end with <c>LocalNotifications::SetPaused</c>
    /// and <c>SendNeedsPauseStateToGame</c> (0x00695F6C..0x00695F78).
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
                // J11 (0x00695F02..0x00695F6A): the engine adds pauseDuration to each need's +0x1e4 (last
                // decay) and +0x1f0 (the per-need pause start) always, and to +0x208 (the fullness deadline)
                // and +0x1fc (the fullness start) only when +0x208 != 0, and to +0x214 (the bracket-change
                // clock) always. This stack keeps the fullness start and derives its deadline as
                // fill + cooldown.
                foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
                {
                    _needPauseStartSec[(int)n] += pauseDuration;  // +0x1f0 always (0x00695F40)
                    _bracketChangedSec[(int)n] += pauseDuration;  // +0x214 always (0x00695F66)
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
    /// <c>NeedsManager::InitAfterSerialNumberAcquired</c> 0x006943A0 (C2 row 9): copy the prior device-loaded
    /// serial <c>+0x34</c> to <c>+0x1cc</c>, store the inbound serial at <c>+0x34</c>, clear the robot-read
    /// result <c>+0x1c8</c> and the robot-rewrite flag <c>+0x1ca</c>, and call <c>StartReadFromRobot</c>. If
    /// that returns 0 the manager immediately calls <c>InitAfterReadFromRobotAttempt</c>
    /// (0x006943F8..0x00694402). The serial comes from the engine's mfgId edge (C2 rows 5-6).
    /// </summary>
    // fidelity: M15-014
    public void InitAfterSerialNumberAcquired(uint serial)
    {
        lock (_gate)
        {
            _previousSerial = _serial;
            _serial = serial;
            _robotReadSucceeded = false;       // +0x1c8
            _robotRewriteNeeded = false;       // +0x1ca
        }
        // The NV read (and the immediate fallback) run outside _gate.
        if (StartReadFromRobot() == 0) InitAfterReadFromRobotAttempt();
    }

    /// <summary>C2 row 10: the NV key <c>StartReadFromRobot</c> queues (the robot's needs item).</summary>
    // fidelity: M15-014
    public const uint NeedsNvKey = 0x194000;

    /// <summary>
    /// C2 row 10: the connected robot's NV component (<c>robot.Engine.NvStorage</c>, as the camera's
    /// calibration read uses). A null component is this stack's defensive guard; the engine's own failure is
    /// only an invalid tag (Appendix G Q2).
    /// </summary>
    // fidelity: M15-014
    public NvStorageComponent? NvStorage { get; set; }

    /// <summary>
    /// <c>NeedsManager::StartReadFromRobot</c> 0x006944B4..0x0069453E (C2 row 10): queue an NVStorage read of
    /// key <see cref="NeedsNvKey"/> on the connected robot's NV component. 1 when the tag is valid and queued
    /// (Appendix G Q2), and the resolution waits for the callback; on failure it logs, clears <c>+0x3d0</c>
    /// and returns 0. <paramref name="tag"/> is a test seam; the live path passes <see cref="NeedsNvKey"/>.
    /// </summary>
    // fidelity: M15-014
    public int StartReadFromRobot(uint tag = NeedsNvKey)
    {
        NvStorageComponent? nv;
        lock (_gate) nv = NvStorage;
        if (nv is null)
        {
            Log?.Invoke("error: NeedsManager.StartReadFromRobot: no NV storage to queue the robot needs read on");
            lock (_gate) _awaitingRobotData = false;    // +0x3d0
            return 0;
        }
        // The NV read happens outside _gate; an invalid tag invokes OnRobotRead synchronously.
        if (nv.Read(tag, OnRobotRead) == 0)
        {
            Log?.Invoke($"error: NeedsManager.StartReadFromRobot: the NV tag 0x{tag:X8} is invalid; the robot needs read was not queued");
            lock (_gate) _awaitingRobotData = false;    // +0x3d0
            return 0;
        }
        return 1;
    }

    /// <summary>
    /// The robot-read NV callback 0x0069BEB2..0x0069BED4 (C2 row 11): clear <c>+0x3d0</c>, call
    /// <c>FinishReadFromRobot(data,size,result)</c>, store its Boolean at <c>+0x1c8</c>, and always call
    /// <c>InitAfterReadFromRobotAttempt</c>.
    /// </summary>
    // fidelity: M15-014
    private void OnRobotRead(NvResult r)
    {
        bool ok;
        lock (_gate)
        {
            _awaitingRobotData = false;            // +0x3d0
            ok = FinishReadFromRobot(r.Data, r.Result);
            _robotReadSucceeded = ok;              // +0x1c8
        }
        InitAfterReadFromRobotAttempt();
    }

    /// <summary>
    /// <c>FinishReadFromRobot(data,size,result)</c> 0x00699DB0..0x0069A1AC, corrected contract (C2 row 11,
    /// Appendix H): a missing NV item (result -1) and any other NV failure (result &lt; -1) return false; a
    /// blob version above 5 returns false; version 5 unpacks the full binary layout and returns true without
    /// <c>+0x1ca</c>; versions 1-4 unpack the shorter layout (missing tail zeroed, version forced to 5),
    /// set <c>+0x1ca</c> and return true; version 0 is read as all-zero fields (M15-018), sets <c>+0x1ca</c>
    /// and returns true. The success constant is 0x0069A176; the only zero is 0x00699F06.
    /// </summary>
    // fidelity: M15-014, M15-017, M15-018
    public bool FinishReadFromRobot(byte[] data, sbyte result)
    {
        if (result <= -1) return false;                              // -1 missing, < -1 other failure
        if (data.Length == 0) return false;                          // no version byte to dispatch on
        int version = data[0];
        if (version > 5) return false;                               // 0x00699DD2/0x00699DD4, sVerifyFailedReturnFalse

        NeedsStateOnRobot robot;
        if (version == 0)
        {
            // fidelity: M15-018
            // The engine runs no Unpack variant and leaves the struct prefix uninitialised; the stack reads
            // it as all-zero (the forced SD2 policy), sets the rewrite flag and returns success.
            robot = new NeedsStateOnRobot { Version = 5 };
            Log?.Invoke("warning: NeedsManager.FinishReadFromRobot: Version 0 found on robot but not supported");
        }
        else
        {
            robot = NeedsStateOnRobot.UnpackVersioned(data, version);
        }

        var levels = new double[3];
        for (int i = 0; i < levels.Length; i++) levels[i] = robot.CurNeedLevel[i] / 100000.0;
        _robotCopy = new NeedsCopy(levels, 0, (long)robot.TimeLastWritten, 5);   // the robot blob has no serial
        if (version != 5) _robotRewriteNeeded = true;                // 0x00699E64: every version but 5 rewrites
        return true;
    }

    /// <summary>
    /// <c>InitAfterReadFromRobotAttempt</c> 0x00694608..0x00694F55 (decision 0x0069481C..0x00694D06;
    /// Appendix I1): resolve the robot and device copies and schedule the writes. The nine cases set the
    /// device-write flag (<c>[sp,#0x18]</c>) and the robot-write flag (<c>r8</c>) exactly as the table does:
    /// robot-absent cases force RW=1, the device-newer case forces RW=1, otherwise RW is <c>+0x1CA != 0</c>;
    /// DW is 0 when the device copy is kept (cases 2, 8, 9). The robot copy is applied (and decayed for the
    /// time since its <c>timeLastWritten</c>) in cases 5-7; the per-serial alternate read replaces the device
    /// copy in cases 3/4 and is not followed by a robot apply. Cases 1 and 5-7 send
    /// <c>SendNeedsStateToGame</c> (arg 0 in case 1, Decay in 5-7; the successful alternate read in
    /// <c>AttemptReadFromDevice</c> sends Decay too); cases 3, 5, 6 and 7 send
    /// <c>RobotChangedFromLastSession</c>. One captured <c>system_clock::now()</c> is used for both writes; a
    /// device write sets <c>+0x1c9</c> and clears <c>+0x1cb</c>, a robot write clears <c>+0x1ca</c>, both with
    /// the "storage version update" log.
    /// </summary>
    // fidelity: M15-014
    public void InitAfterReadFromRobotAttempt()
    {
        // Phase 1: the per-serial alternate read (I/O and the host path seam) outside _gate.
        bool alternateTried;
        uint serialForAlternate;
        lock (_gate)
        {
            alternateTried = _robotCopy is null && _deviceCopy is not null && _previousSerial != _serial;
            serialForAlternate = _serial;
        }
        bool alternateOk = false;
        if (alternateTried)
        {
            alternateOk = TryLoadAlternateDeviceFile(serialForAlternate);
            if (!alternateOk)
                Log?.Invoke("NeedsManager.InitAfterReadFromRobotAttempt: no per-serial needs file; a brand-new robot is possible, resolution continues");
        }

        // Phase 2: the case decision and the selected-copy apply, under _gate.
        bool deviceWrite, robotWrite, robotChanged, clearDisconnect, clearAll;
        NeedsActionId? send;
        lock (_gate)
        {
            bool hasRobot = _robotCopy is not null;
            bool hasDevice = _deviceCopy is not null;
            bool rewrite = _robotRewriteNeeded;
            bool serialsEqual = _previousSerial == _serial;
            robotChanged = false; clearDisconnect = false; clearAll = false; send = null;

            if (!hasRobot && !hasDevice)                                 // case 1
            {
                deviceWrite = true; robotWrite = true; send = NeedsActionId.NoAction;
            }
            else if (!hasRobot && hasDevice)
            {
                if (alternateTried)                                      // case 3 / 4
                {
                    // Appendix I1: both the alternate success and failure tails converge on the
                    // RobotChangedFromLastSession send (0x00694CA6 -> 0x00694AA8). The alternate read runs
                    // AttemptReadFromDevice, whose success tail sends Decay (0x006936C6), so the resolver
                    // sends nothing here.
                    deviceWrite = alternateOk; robotWrite = true; robotChanged = true;
                }
                else                                                     // case 2
                {
                    deviceWrite = false; robotWrite = true;
                }
            }
            else if (hasRobot && !hasDevice)                             // case 5
            {
                // 0x00694A46..0x00694A4C: clear the disconnect timestamp +0x18/+0x1C before the apply.
                clearDisconnect = true;
                ApplyRobotCopyLocked(_robotCopy!);
                deviceWrite = true; robotWrite = rewrite; send = NeedsActionId.Decay; robotChanged = true;
            }
            else if (!serialsEqual)                                      // case 6
            {
                ApplyRobotCopyLocked(_robotCopy!);
                clearAll = true;                                         // +0x18..+0x24
                deviceWrite = true; robotWrite = rewrite; send = NeedsActionId.Decay; robotChanged = true;
            }
            else if (_robotCopy!.DateTimeSec > _deviceTimestampSnapshotSec) // case 7
            {
                ApplyRobotCopyLocked(_robotCopy);
                deviceWrite = true; robotWrite = rewrite; send = NeedsActionId.Decay; robotChanged = true;
            }
            else if (_deviceTimestampSnapshotSec > _robotCopy.DateTimeSec)   // case 8
            {
                deviceWrite = false; robotWrite = true;
            }
            else                                                         // case 9
            {
                deviceWrite = false; robotWrite = rewrite;
            }

            if (clearDisconnect) _lastDisconnectSec = 0;
            if (clearAll)
            {
                _lastDisconnectSec = 0;
                _lastAppBackgroundSec = 0;
                _openAppAfterDisconnect = 0;
            }
        }

        // Phase 3: the game messages and the writes, outside _gate.
        if (send is { } action) { lock (_gate) SendNeedsStateToGame(action); }
        if (robotChanged) RobotChangedFromLastSession?.Invoke();

        if (deviceWrite || robotWrite)
        {
            long now = (long)_clockSec();                 // one system_clock::now() for both writes (0x00694D9C)
            if (deviceWrite)
            {
                lock (_gate)
                {
                    _deviceCopy = SnapshotState(now);
                    _deviceDataPresent = true;            // +0x1c9
                    ResolvedDeviceWriteTimestampSec = now;
                    if (_deviceVersionUpdated)
                    {
                        _deviceVersionUpdated = false;    // +0x1cb, cleared with the log (0x00694DB6)
                        Log?.Invoke("info: NeedsManager.InitAfterReadFromRobotAttempt: storage version update (device)");
                    }
                }
                WriteToDevice?.Invoke(false);
            }
            if (robotWrite)
            {
                lock (_gate)
                {
                    if (_robotRewriteNeeded)
                    {
                        _robotRewriteNeeded = false;      // +0x1ca, cleared with the log (0x00694E62)
                        Log?.Invoke("info: NeedsManager.InitAfterReadFromRobotAttempt: storage version update (robot)");
                    }
                }
                StartWriteToRobot(now);
            }
        }
    }

    /// <summary>C2 row 13: the timestamp a resolved device write was stamped with (<c>system_clock::now</c>).</summary>
    // fidelity: M15-014
    public long ResolvedDeviceWriteTimestampSec { get; private set; }

    /// <summary>
    /// <c>NeedsManager::StartWriteToRobot</c> 0x00695494..0x00695763 (Appendix G Q1): returns at once with
    /// no connected robot; while a robot read is outstanding (<c>+0x3D0</c>) it logs "Aborting writing needs
    /// state to robot, because we are reading needs state from robot" and returns without queueing; otherwise
    /// it stores <paramref name="nowSec"/> at <c>+0x1C0/+0x1C4</c>, builds a fresh version-5
    /// <see cref="NeedsStateOnRobot"/> (116 bytes) and writes it to NV key <see cref="NeedsNvKey"/> through
    /// <see cref="NvStorageComponent.Write"/>. A <c>Write</c> return of 0 logs
    /// <c>NeedsManager.StartWriteToRobot.WriteFailed</c> and sets the error flag; the terminal
    /// <see cref="FinishWriteToRobot"/> handles the result.
    /// </summary>
    // fidelity: M15-014, M15-017
    public void StartWriteToRobot(double nowSec)
    {
        byte[] blob;
        NvStorageComponent? nv;
        lock (_gate)
        {
            if (!_robotConnected) return;                            // the connected robot +4 is null
            if (_awaitingRobotData)
            {
                Log?.Invoke("warning: NeedsManager.StartWriteToRobot: Aborting writing needs state to robot, because we are reading needs state from robot");
                return;
            }
            _lastWriteToRobotSec = nowSec;                           // +0x1c0/+0x1c4

            var needs = new[] { NeedId.Repair, NeedId.Energy, NeedId.Play };
            var robot = new NeedsStateOnRobot { Version = 5, TimeLastWritten = (ulong)nowSec };
            for (int i = 0; i < needs.Length; i++) robot.CurNeedLevel[i] = (int)(State.GetNeedLevel(needs[i]) * 100000.0 + 0.5);
            int damaged = State.NumDamagedParts();
            for (int i = 0; i < damaged && i < NeedsStateOnRobot.PartCount; i++) robot.PartIsDamaged[i] = 1;
            // The engine copies its economy fields into the blob (0x00695540..0x00695640:
            // curNeedsUnlockLevel, numStarsAwarded, timeLastStarAwarded, timeCreated, onboardingStageCompleted,
            // forceNextSong). This stack does not model the stars/onboarding economy, so those fields stay at
            // their zero defaults; they are not invented here.
            blob = NeedsStateOnRobot.Pack(robot);
            nv = NvStorage;
        }

        // The NV write happens outside _gate (finding 5: do not hold _gate across NvStorageComponent.Write).
        if (nv is null)
        {
            Log?.Invoke("error: NeedsManager.StartWriteToRobot.WriteFailed: no NV storage to queue the robot needs write on");
            _writeToRobotError = true;
            return;
        }
        if (nv.Write(NeedsNvKey, blob, r => FinishWriteToRobot(r.Result)) == 0)
        {
            Log?.Invoke("error: NeedsManager.StartWriteToRobot.WriteFailed");
            _writeToRobotError = true;
        }
    }

    // fidelity: M15-014
    /// <summary>
    /// <c>NeedsManager::PossiblyStartWriteToRobot(force)</c> 0x00696ECC..0x00696F0D (Appendix I2): return when
    /// the connected robot is null; read <c>+0x1C0/+0x1C4</c> (the last robot write) and start one when the
    /// elapsed time is strictly greater than 600,999,999 clock units, or when <paramref name="force"/>.
    /// <c>StartWriteToRobot</c> stamps <c>+0x1C0/+0x1C4</c>.
    /// MISSING (unbuilt layers): the other two callers, <c>NeedsManager::UpdateStarsState</c> 0x00696AAC with
    /// <c>force = true</c> and <c>HandleMessage&lt;RegisterOnboardingComplete&gt;</c> 0x006984CC with
    /// <c>force = true</c>, belong to the app's stars/onboarding economy and are not built here.
    /// </summary>
    public void PossiblyStartWriteToRobot(bool force)
    {
        double now;
        double last;
        lock (_gate)
        {
            if (!_robotConnected) return;                            // the connected robot +4 is null
            last = _lastWriteToRobotSec;
            now = _clockSec();
        }
        bool overdue = now - last > RobotWriteIntervalSec;           // strictly greater
        if (overdue || force) StartWriteToRobot(now);
    }

    /// <summary>
    /// Appendix I2: the engine's 0x23D2883F clock value, 600,999,999 of its microsecond clock = 600.999999 s.
    /// </summary>
    // fidelity: M15-014
    public const double RobotWriteIntervalSec = 600.999999;

    /// <summary>
    /// <c>NeedsManager::FinishWriteToRobot</c> 0x00699CD8..0x00699D39 (Appendix G Q1): a result below 0 logs
    /// <c>FinishWriteToRobot.WriteFailed</c> and sets the error flag; a result of 0 or more is a no-op. It
    /// touches no NeedsManager flag and does not retry.
    /// </summary>
    // fidelity: M15-014
    public void FinishWriteToRobot(sbyte result)
    {
        if (result >= 0) return;
        Log?.Invoke("error: NeedsManager.FinishWriteToRobot.WriteFailed");
        _writeToRobotError = true;
    }

    /// <summary><c>+0x1c0/+0x1c4</c>: the time the last robot write was built with.</summary>
    // fidelity: M15-014
    public double LastWriteToRobotSec => _lastWriteToRobotSec;

    /// <summary><c>_errG</c>: set by a failed robot write (Appendix G Q1).</summary>
    // fidelity: M15-014
    public bool WriteToRobotError => _writeToRobotError;

    /// <summary>
    /// C2 row 12: the path of a serial's alternate per-serial device file, or null when the host has none.
    /// </summary>
    // fidelity: M15-014
    public Func<uint, string?>? AlternateDeviceFilePath { get; set; }

    /// <summary>
    /// J3: the host directory holding the fixed <c>needsState.json</c> and the per-serial
    /// <c>needsState_&lt;serial&gt;.json</c> files. The engine's directory is
    /// <c>DataPlatform::pathToResource("nurture/")</c> (ctor 0x006921A6..0x006921D8); resolving it is host
    /// business, so it is a seam here.
    /// </summary>
    // fidelity: M15-014
    public string? DeviceDirectory { get; set; }

    /// <summary>J3: <c>&lt;DeviceDirectory&gt;/needsState.json</c>, or null when no directory is set.</summary>
    // fidelity: M15-014
    public string? FixedDeviceFilePath => DeviceDirectory is null ? null : Path.Combine(DeviceDirectory, FixedFileName);

    /// <summary><c>+0x1c8</c>: the robot read produced usable data.</summary>
    // fidelity: M15-014
    public bool RobotReadSucceeded => _robotReadSucceeded;
    /// <summary><c>+0x1ca</c>: the robot copy needs rewriting (version 0-4).</summary>
    // fidelity: M15-014
    public bool RobotRewriteNeeded => _robotRewriteNeeded;
    /// <summary><c>+0x1c9</c>: a device copy is present.</summary>
    // fidelity: M15-014
    public bool DeviceDataPresent => _deviceDataPresent;
    /// <summary><c>+0x3d0</c>: a robot needs read is outstanding.</summary>
    // fidelity: M15-014
    public bool AwaitingRobotData => _awaitingRobotData;
    /// <summary>Whether a parsed robot copy exists.</summary>
    // fidelity: M15-014
    public bool HasRobotCopy => _robotCopy is not null;
    /// <summary>Whether a device copy exists.</summary>
    // fidelity: M15-014
    public bool HasDeviceCopy => _deviceCopy is not null;

    /// <summary>
    /// Appendix I1: cases 3, 4, 5, 6 and 7 send a <c>RobotChangedFromLastSession</c> game message
    /// (0x00694AA8..0x00694AC4); cases 1, 2, 8 and 9 do not. The game wire is the app's, so it is an event.
    /// </summary>
    // fidelity: M15-014
    public Action? RobotChangedFromLastSession { get; set; }

    private bool TryLoadAlternateDeviceFile(uint serial)
    {
        // J3: the host may name the alternate file itself, or leave it to the device directory.
        string? path = AlternateDeviceFilePath?.Invoke(serial)
                       ?? (DeviceDirectory is null ? null : Path.Combine(DeviceDirectory, FileNameForSerial(serial)));
        if (path is null) return false;
        return AttemptReadFromDevice(path);
    }

    /// <summary>
    /// Appendix I1: the robot-copy apply is the copy followed by <c>ApplyDecayForTimeSinceLastDeviceWrite(false)</c>
    /// (0x00694AD0..0x00694B04). The state's <c>DateTime</c> becomes the robot blob's <c>timeLastWritten</c>.
    /// </summary>
    private void ApplyRobotCopyLocked(NeedsCopy copy)
    {
        var needs = new[] { NeedId.Repair, NeedId.Energy, NeedId.Play };
        for (int i = 0; i < needs.Length && i < copy.Levels.Length; i++) State.SetNeedLevel(needs[i], copy.Levels[i]);
        _stateDateTimeSec = copy.DateTimeSec;
        ApplyDecayForTimeSinceLastDeviceWrite(false);
    }

    // fidelity: M15-014
    /// <summary>
    /// <c>NeedsManager::ApplyDecayForTimeSinceLastDeviceWrite(bool)</c> 0x00695304..0x00695374 (Appendix I4):
    /// <c>elapsed = now - the current state's DateTime</c>; for each need, rewind <c>+0x1E4</c> to
    /// <c>now - elapsed</c> (so <c>ApplyDecayAllNeeds</c> decays each need by this whole gap), and, only when
    /// that need's fullness deadline <c>+0x208</c> is non-zero, subtract <c>elapsed</c> from the deadline and
    /// the fullness start <c>+0x1FC</c>; then tail-call <c>ApplyDecayAllNeeds(connected)</c>. The device-read
    /// and resolver callers pass <c>false</c> (the unconnected table at <c>this+0x17C</c>); a third caller,
    /// <c>HandleMessage&lt;SetGameBeingPaused&gt;</c> 0x00698F44 (<c>0x006990DE..0x006990E8</c>), passes
    /// <c>robot != 0</c>. That game-message caller is unbuilt (M15-016's named gap).
    /// </summary>
    public void ApplyDecayForTimeSinceLastDeviceWrite(bool connected)
    {
        lock (_gate)
        {
            double now = _clockSec();
            double elapsed = now - _stateDateTimeSec;
            foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
            {
                _lastDecaySec[n] = now - elapsed;                        // [+0x1E4] = [+0x3AC] - elapsed
                if (_fullnessDeadlineSec.TryGetValue(n, out var deadline) && deadline != 0)
                {
                    _fullnessDeadlineSec[n] = deadline - elapsed;
                    if (_fullnessStartSec.TryGetValue(n, out var start)) _fullnessStartSec[n] = start - elapsed;
                }
            }
            ApplyDecayAllNeeds(connected);
        }
    }

    private NeedsCopy SnapshotState(long dateTimeSec) => new(
        new[] { State.GetNeedLevel(NeedId.Repair), State.GetNeedLevel(NeedId.Energy), State.GetNeedLevel(NeedId.Play) },
        _serial, dateTimeSec, CurrentStateFileVersion);

    /// <summary>C2 row 11: the version a non-rewriting robot/device copy carries (versions 1-4 rewrite, 5 does not).</summary>
    // fidelity: M15-014
    public const int CurrentStateFileVersion = 5;

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
        }
        // Appendix I2: RegisterNeedsActionCompleted 0x006960D0 calls PossiblyStartWriteToRobot with force = false
        // (0x00696534..0x00696536), after the deltas. Outside _gate so the NV write is not queued under it.
        PossiblyStartWriteToRobot(false);
        return true;
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
    /// damaged parts, serial and timestamp are meaningful here. The state-file version is
    /// <see cref="CurrentStateFileVersion"/> 5: versions 1-4 are rewritten on read (C2 row 11), 5 is not.
    /// The serial is the current robot's (C2 row 9); the caller may override it for a fixed-file write.
    /// </summary>
    // fidelity: M15-014
    public void Save(string path, long? unixTimeSec = null, uint serialNumber = 0)
    {
        long now = unixTimeSec ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var levels = new[] { NeedId.Repair, NeedId.Energy, NeedId.Play }.Select(n => State.GetNeedLevel(n)).ToArray();
        var damaged = new[] { NeedId.Repair, NeedId.Energy, NeedId.Play }.Select(n => State.NumDamagedPartsForRepairLevel(State.GetNeedLevel(n)) > 0).ToArray();
        var doc = new Dictionary<string, object>
        {
            ["_StateFileVersion"] = CurrentStateFileVersion,
            ["_DateTime"] = now,
            ["_SerialNumber"] = serialNumber,
            ["CurNeedLevel"] = levels,
            ["PartIsDamaged"] = damaged,
            ["CurNeedsUnlockLevel"] = 0,
            ["NumStarsAwarded"] = 0,
            ["NumStarsForNextUnlock"] = 0,
            ["TimeCreated"] = now,
            ["TimeLastStarAwarded"] = 0,
            ["TimeLastDisconnect"] = _lastDisconnectSec,
            ["TimeLastAppBackgrounded"] = _lastAppBackgroundSec,
            ["OpenAppAfterDisconnect"] = _openAppAfterDisconnect,
            ["ForceNextSong"] = false,
        };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(doc));
    }

    /// <summary>
    /// J7: <c>NeedsManager::WriteToDevice(bool refreshDateTime)</c> 0x00693BB0 reached through the host's
    /// <see cref="WriteToDevice"/> seam. It returns when the host has no fixed file path
    /// (<c>FixedDeviceFilePath</c> is null); with <paramref name="refreshDateTime"/> it stamps
    /// <c>_stateDateTimeSec</c> (<c>+8/+0xC</c>) from the stack clock - the engine's
    /// <c>system_clock::now()</c> at 0x00693BC4, which is the same base the decay uses - and then writes
    /// <c>&lt;DeviceDirectory&gt;/needsState.json</c> with that timestamp.
    /// </summary>
    // fidelity: M15-014
    public void WriteDeviceFile(bool refreshDateTime)
    {
        if (FixedDeviceFilePath is not { } path) return;
        if (refreshDateTime) _stateDateTimeSec = _clockSec();    // 0x00693BC4
        Save(path, unixTimeSec: (long)_stateDateTimeSec, serialNumber: _serial);   // 0x00693BB0 (+0x34)
    }

    /// <summary>
    /// Reads a file written by <see cref="Save"/> (the exact keys above) and, as
    /// <c>AttemptReadFromDevice</c> 0x006936B0..0x0069371E does, calls
    /// <see cref="ApplyDecayForTimeSinceLastDeviceWrite"/><c>(false)</c> for the time that has passed since
    /// <c>_DateTime</c> was written. Returns false when there is no file to read, or when the state-file
    /// version is above 5 (C2 row 3, <c>ReadFromDevice</c>). A successful read records the device copy
    /// (<c>+0x1c9</c> set) so the resolver can compare it with the robot's. The elapsed decay uses the stack
    /// clock (Appendix I4); <paramref name="unixTimeSec"/> is retained for callers that stamp a file time.
    /// </summary>
    // fidelity: M15-014
    public bool Load(string path, long? unixTimeSec = null, bool applyElapsedDecay = true)
    {
        if (!File.Exists(path)) return false;
        string text = File.ReadAllText(path);            // the file I/O is outside _gate
        lock (_gate)
        {
        using var doc = System.Text.Json.JsonDocument.Parse(text);
        var root = doc.RootElement;
        int version = root.TryGetProperty("_StateFileVersion", out var ver) && ver.ValueKind == System.Text.Json.JsonValueKind.Number ? ver.GetInt32() : 1;
        if (version > 5) { Log?.Invoke($"warning: NeedsManager.ReadFromDevice: state file version {version} is above 5; not read"); return false; }
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
        long dateTime = root.TryGetProperty("_DateTime", out var w) && w.ValueKind == System.Text.Json.JsonValueKind.Number ? w.GetInt64() : 0;
        _stateDateTimeSec = dateTime;
        _deviceTimestampSnapshotSec = _stateDateTimeSec;   // +0x1B8/+0x1BC = +8/+0xC (J4)
        _deviceVersionUpdated = version != 5;            // +0x1cb (C2 row 1)
        if (applyElapsedDecay) ApplyDecayForTimeSinceLastDeviceWrite(false);
        else DetectBracketChanges();
        _deviceCopy = SnapshotState(dateTime);
        _deviceDataPresent = true;       // +0x1c9
        return true;
        }
    }

    /// <summary>
    /// <c>NeedsManager::AttemptReadFromDevice</c> 0x00693690..0x00693792 (J4): if <paramref name="path"/>
    /// (the fixed <c>needsState.json</c> at startup, or the per-serial alternate during resolution) is not
    /// on the device it logs <c>"FAILED to FIND file &lt;name&gt; on device"</c> (0x0069378C) and returns
    /// false; if <see cref="Load"/> (the engine's <c>ReadFromDevice</c>) fails it logs
    /// <c>"FAILED to read file &lt;name&gt; on device"</c> and returns false. On success it snapshots the
    /// state timestamp into <c>+0x1B8</c> (0x006936B6), sends <c>SendNeedsStateToGame(Decay)</c>
    /// (0x006936C6), increments the <c>+0x30 OpenAppAfterDisconnect</c> counter (0x006936CE), raises
    /// <see cref="SendTimeSinceBackgroundedDasEvent"/> (0x006936D2; the engine emits nothing when
    /// <c>+0x20|+0x24 == 0</c>, 0x0069771E, so the caller guards on the app-background time) and logs
    /// <c>"Successfully read file &lt;name&gt; from device"</c> (0x0069371C). <see cref="Load"/> already
    /// applies the elapsed decay through its <c>applyElapsedDecay</c> default (the engine's
    /// <c>ApplyDecayForTimeSinceLastDeviceWrite(false)</c> at 0x006936BE), so it is not applied again.
    /// </summary>
    // fidelity: M15-014
    public bool AttemptReadFromDevice(string? path)
    {
        string name = path is null ? FixedFileName : Path.GetFileName(path);
        if (path is null || !File.Exists(path))
        {
            Log?.Invoke($"FAILED to FIND file {name} on device");
            return false;
        }
        if (!Load(path))
        {
            Log?.Invoke($"FAILED to read file {name} on device");
            return false;
        }
        lock (_gate) _deviceTimestampSnapshotSec = _stateDateTimeSec;   // +0x1B8/+0x1BC = +8/+0xC (0x006936B6)
        SendNeedsStateToGame(NeedsActionId.Decay);              // 0x006936C6
        int openApp;
        lock (_gate)
        {
            _openAppAfterDisconnect += 1;                       // +0x30 (0x006936CE)
            openApp = _openAppAfterDisconnect;
        }
        if (_lastAppBackgroundSec != 0)
            SendTimeSinceBackgroundedDasEvent?.Invoke(openApp, _clockSec() - _lastAppBackgroundSec);   // 0x006936D2
        Log?.Invoke($"Successfully read file {name} from device");   // 0x0069371C
        return true;
    }

    public bool IsSevereExpressed(NeedId n) => _severeExpressed.Contains(n);
    public void SetSevereExpressed(NeedId n, bool expressed) { if (expressed) _severeExpressed.Add(n); else _severeExpressed.Remove(n); }

    private void DetectBracketChanges(bool force = false, double? nowOverride = null)
    {
        double now = nowOverride ?? _clockSec();
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
        {
            var b = State.GetNeedBracket(n);
            // 0x00695958 enters the event branch when cached (+0x214) != current or force is set.
            if (b != _prevBrackets[n] || force)
            {
                // J10: the DAS elapsed is now - +0x214, read before the emit (0x006959D6, 0x006959FA).
                double elapsed = now - _bracketChangedSec[(int)n];
                Log?.Invoke($"need {n}: {_prevBrackets[n]} -> {b} (level {State.GetNeedLevel(n):F3})");
                if (b != NeedBracketId.Critical) _severeExpressed.Remove(n);
                BracketChanged?.Invoke(n, _prevBrackets[n], b, elapsed);
                // The force path emits for every need and does not update the cached bracket (+0x214);
                // only the conditional pass writes +0x214 = now (0x00695BA2).
                if (!force)
                {
                    _prevBrackets[n] = b;
                    _bracketChangedSec[(int)n] = now;
                }
            }
        }
    }

    /// <summary>Test / tool hook: set a level directly and report the bracket change.</summary>
    public void SetLevel(NeedId n, double level) { State.SetNeedLevel(n, level); DetectBracketChanges(); }
}
