namespace Cozmo.Robot.Behavior;

using System.Text.Json;

/// <summary>One <c>behaviorObjectiveTriggerParams</c> entry of a FistBump reaction-map row.</summary>
public sealed record FistBumpObjectiveParams(string BehaviorObjective, double TriggerCooldownTimeSec,
                                             double TriggerProbability, double TriggerExpirationSec);

/// <summary>The <c>hiccupParams</c> block of the Hiccup reaction-map row.</summary>
public sealed record HiccupParams(int MinHiccupOccurrenceFrequencySec, int MaxHiccupOccurrenceFrequencySec,
                                  int MinNumberOfHiccupsToDo, int MaxNumberOfHiccupsToDo,
                                  int MinHiccupSpacingMs, int MaxHiccupSpacingMs,
                                  int HiccupsWontOccurAfterBeingCuredTimeSec, string HiccupsUnlockId);

/// <summary>
/// One row of the shipped <c>reactionTrigger_behavior_map.json</c>: the reaction trigger, the behaviour id it
/// dispatches to, and whichever parameter block that row carries.
/// </summary>
public sealed record ReactionMapEntry(
    ReactionTrigger Trigger,
    string BehaviorId,
    bool? ShouldResumeLast,
    string? StrategyType,
    double? FrustrationMaxConfidence,
    double? FrustrationCooldownSec,
    IReadOnlyList<FistBumpObjectiveParams> FistBumpObjectiveParams,
    HiccupParams? Hiccup);

/// <summary>
/// The shipped reaction-trigger map, read as data.
///
/// <c>RobotDataLoader::LoadReactionTriggerMap</c> 0x00520bc8 reads
/// <c>config/engine/behaviorSystem/reactionTrigger_behavior_map.json</c> (path string at 0x520ce8) through
/// <c>DataPlatform::readAsJson</c> (0x00520c2e) and logs <c>"Failed to read '%s'"</c> through
/// <c>sErrorF</c> (0x00520c54) when it fails; <c>BehaviorManager::InitReactionTriggerMap</c> 0x005a16e4
/// then iterates the array and builds the trigger -> behaviour dispatch. This class is the file half; the
/// binding half is <see cref="ShippedBehaviors.Reactions"/>.
/// </summary>
// fidelity: M7-002
public static class ReactionTriggerMap
{
    public const string RelativePath = "config/engine/behaviorSystem/reactionTrigger_behavior_map.json";
    public const string FileName = "reactionTrigger_behavior_map.json";

    /// <summary>Reads the map from an OBB root (or any directory containing the file). Empty when absent.</summary>
    public static IReadOnlyList<ReactionMapEntry> Load(string root)
    {
        var file = FindFile(root);
        return file is null ? Array.Empty<ReactionMapEntry>() : LoadFromFile(file);
    }

    private static string? FindFile(string root)
    {
        if (File.Exists(root)) return root;
        if (!Directory.Exists(root)) return null;
        var direct = Path.Combine(root, RelativePath);
        if (File.Exists(direct)) return direct;
        return Directory.EnumerateFiles(root, FileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    /// <summary>Reads the map from the file itself. The shipped file contains C-style comments.</summary>
    public static IReadOnlyList<ReactionMapEntry> LoadFromFile(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        if (!doc.RootElement.TryGetProperty("reactionTriggerBehaviorMap", out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"{file}: no reactionTriggerBehaviorMap array");

        var list = new List<ReactionMapEntry>();
        foreach (var e in arr.EnumerateArray())
        {
            var triggerName = e.TryGetProperty("reactionTrigger", out var t) ? t.GetString() : null;
            var behaviorId = e.TryGetProperty("behaviorID", out var b) ? b.GetString() : null;
            if (triggerName is null || behaviorId is null) continue;
            if (!Enum.TryParse<ReactionTrigger>(triggerName, out var trigger))
                throw new InvalidDataException($"{file}: unknown reactionTrigger '{triggerName}'");

            bool? resume = null;
            string? strategyType = null;
            if (e.TryGetProperty("genericStrategyParams", out var g))
            {
                if (g.TryGetProperty("shouldResumeLast", out var sr) &&
                    (sr.ValueKind == JsonValueKind.True || sr.ValueKind == JsonValueKind.False))
                    resume = sr.GetBoolean();
                if (g.TryGetProperty("debugStrategyName", out var ds) && ds.ValueKind == JsonValueKind.String)
                    strategyType = ds.GetString();
            }
            if (e.TryGetProperty("wantsToRunStrategyConfig", out var wtr) &&
                wtr.TryGetProperty("strategyType", out var st) && st.ValueKind == JsonValueKind.String)
                strategyType = st.GetString();

            double? maxConf = null, cooldown = null;
            if (e.TryGetProperty("frustrationParams", out var fp))
            {
                if (fp.TryGetProperty("maxConfidence", out var mc) && mc.ValueKind == JsonValueKind.Number)
                    maxConf = mc.GetDouble();
                if (fp.TryGetProperty("cooldownTime_s", out var cd) && cd.ValueKind == JsonValueKind.Number)
                    cooldown = cd.GetDouble();
            }

            var fist = new List<FistBumpObjectiveParams>();
            if (e.TryGetProperty("behaviorObjectiveTriggerParams", out var bp) && bp.ValueKind == JsonValueKind.Array)
                foreach (var p in bp.EnumerateArray())
                    fist.Add(new FistBumpObjectiveParams(
                        p.TryGetProperty("behaviorObjective", out var bo) ? bo.GetString() ?? "" : "",
                        Num(p, "triggerCooldownTime_s"), Num(p, "triggerProbability"), Num(p, "triggerExpiration_s")));

            HiccupParams? hiccup = null;
            if (e.TryGetProperty("hiccupParams", out var h))
                hiccup = new HiccupParams(
                    (int)Num(h, "minHiccupOccurrenceFrequency_s"), (int)Num(h, "maxHiccupOccurrenceFrequency_s"),
                    (int)Num(h, "minNumberOfHiccupsToDo"), (int)Num(h, "maxNumberOfHiccupsToDo"),
                    (int)Num(h, "minHiccupSpacing_ms"), (int)Num(h, "maxHiccupSpacing_ms"),
                    (int)Num(h, "hiccupsWontOccurAfterBeingCuredTime_s"),
                    h.TryGetProperty("hiccupsUnlockId", out var u) ? u.GetString() ?? "" : "");

            list.Add(new ReactionMapEntry(trigger, behaviorId, resume, strategyType, maxConf, cooldown, fist, hiccup));
        }
        return list;
    }

    private static double Num(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
}

/// <summary>How well grounded a single mapping is. Recorded per entry, not assumed for the table.</summary>
public enum ReactionEvidence
{
    /// <summary>
    /// Read from a shipped artifact: a config file, a decompiled enum, or disassembled engine code.
    /// </summary>
    Shipped,

    /// <summary>
    /// Inferred from Anki's own naming across artifacts, because the deciding code was not read. No entry
    /// in the default table carries this any more; it is kept so a caller-built table can still say so.
    /// </summary>
    NameCorrespondence,
}

/// <summary>One reaction: what happened, what it plays, and how well established that link is.</summary>
public sealed record ReactionEntry(
    ReactionTrigger Trigger,
    AnimationTrigger Animation,
    ReactionEvidence Evidence,
    string Basis);

/// <summary>
/// Which animation a reaction plays.
///
/// The chain has three links, and all three are now read from shipped material:
///
/// <code>
/// robot state  ->  ReactionTrigger  ->  behaviour  ->  AnimationTrigger  ->  group  ->  clip
///      (a)              (b)               (c)              (d)
/// </code>
///
/// * <b>(a)</b> Anki's <see cref="ReactionTrigger"/> values are named after the robot status flags M4
///   reports, and the engine's reaction strategies watch exactly those.
/// * <b>(b)</b> is the shipped <c>config/engine/behaviorSystem/reactionTrigger_behavior_map.json</c>,
///   loaded by <c>RobotDataLoader::LoadReactionTriggerMap</c> at 0x00520BC8 in libcozmoEngine.so. It maps
///   each reaction trigger to a <c>behaviorID</c>. An earlier version of this table assumed the link was
///   unrecoverable and inferred it from names; the file was in the OBB all along.
/// * <b>(c)</b> is the behaviour class's own code. The four classes here are exported
///   (<c>BehaviorReactToCliff</c>, <c>BehaviorReactToPickup</c>, <c>BehaviorReactToOnCharger</c>,
///   <c>BehaviorReactToImpact</c>) and each constructs a <c>TriggerAnimationAction</c> or
///   <c>TriggerLiftSafeAnimationAction</c> with an <c>AnimationTrigger</c> immediate. The addresses are
///   in each entry's basis.
/// * <b>(d)</b> is Anki's own <c>AnimationTriggerMap.json</c>, read by <see cref="AnimationTriggerMap"/>.
///
/// <b>The falling reaction is not "ReactToFalling".</b> The shipped map sends <c>RobotFalling</c> to the
/// behaviour <c>ReactToImpact</c>, and <c>BehaviorReactToImpact::TransitionToPlayingAnim</c> plays
/// <see cref="AnimationTrigger.ReactToImpact"/>, and only after the fall has ended with an impact intensity
/// above 1000 (see <see cref="ReactiveBehavior"/>). Nothing in the engine plays
/// <c>AnimationTrigger.ReactToFalling</c> from these classes.
///
/// What each behaviour does around its animation is recorded here and not reproduced: ReactToCliff first
/// plays <c>ReactToCliffDetectorStop</c> while the wheels stop, substitutes the severe-needs cliff
/// reactions when a Repair or Energy need is being expressed, and backs up 60 mm at 100 mm/s if the cliff
/// is still under it; ReactToPickup waits 0.5 s, prefers a face or pet acknowledgement when one is in view,
/// plays <c>HiccupRobotPickedUp</c> instead while Cozmo has the hiccups, and repeats every 3-6 s (stretching
/// by a third each time) while still held; ReactToOnCharger pushes an idle animation of <c>Count</c> (none),
/// then after <c>timeTilSleepAnimation_s</c> (300 s in its config) the idle-timeout component plays
/// <c>GoToSleepGetIn</c>, <c>GoToSleepSleeping</c> and <c>GoToSleepOff</c> with the lift lowered.
///
/// <b>No reaction is invented.</b> A <see cref="ReactionTrigger"/> this stack cannot detect, or whose
/// behaviour needs cubes, vision, orientation classification or the mood and spark systems, is left out.
/// </summary>
public sealed class ReactionTable
{
    private readonly Dictionary<ReactionTrigger, ReactionEntry> _entries;

    public ReactionTable(IEnumerable<ReactionEntry> entries) =>
        _entries = entries.ToDictionary(e => e.Trigger);

    /// <summary>The default table: the reactions this stack can both detect and play today.</summary>
    public static ReactionTable Default { get; } = new(DefaultEntries());

    /// <summary>Every mapping in this table.</summary>
    public IReadOnlyCollection<ReactionEntry> Entries => _entries.Values;

    /// <summary>The animation a reaction plays, or null when this table maps none.</summary>
    public ReactionEntry? For(ReactionTrigger trigger) =>
        _entries.TryGetValue(trigger, out var e) ? e : null;

    /// <summary>
    /// The impact intensity a fall has to end with before the engine reacts to it:
    /// <c>BehaviorReactToImpact::AlwaysHandle</c> at 0x00606408 compares <c>FallingStopped.impactIntensity</c>
    /// against 1000.0 (vldr of 0x447A0000) and only then lets the animation play.
    /// </summary>
    // fidelity: M7-003
    public const float ImpactIntensityThreshold = 1000f;

    private static IEnumerable<ReactionEntry> DefaultEntries()
    {
        const ReactionEvidence Shipped = ReactionEvidence.Shipped;

        yield return new ReactionEntry(ReactionTrigger.CliffDetected, AnimationTrigger.ReactToCliff, Shipped,
            "reactionTrigger_behavior_map.json: ReactionTrigger.CliffDetected -> behaviour ReactToCliff; " +
            "BehaviorReactToCliff::TransitionToPlayingCliffReaction at 0x006050F0 passes AnimationTrigger 0x19D (ReactToCliff) " +
            "to TriggerLiftSafeAnimationAction (movw r2, #0x19d at 0x0060518E) = AnimationTrigger.ReactToCliff, with 0x13D/0x131 substituted under severe needs");

        yield return new ReactionEntry(ReactionTrigger.RobotPickedUp, AnimationTrigger.ReactToPickup, Shipped,
            "reactionTrigger_behavior_map.json: ReactionTrigger.RobotPickedUp -> behaviour ReactToPickup; " +
            "BehaviorReactToPickup::StartAnim at 0x00607820 passes 0x1A9 (ReactToPickup) to TriggerAnimationAction " +
            "(movw r2, #0x1a9 at 0x00607958) = AnimationTrigger.ReactToPickup; 0xE6 HiccupRobotPickedUp when the whiteboard's hiccup flag is set");

        yield return new ReactionEntry(ReactionTrigger.PlacedOnCharger, AnimationTrigger.PlacedOnCharger, Shipped,
            "reactionTrigger_behavior_map.json: ReactionTrigger.PlacedOnCharger -> behaviour ReactToOnCharger; " +
            "BehaviorReactToOnCharger::InitInternal at 0x00606C94 passes 0x189 (PlacedOnCharger) to TriggerLiftSafeAnimationAction " +
            "(movw r2, #0x189 at 0x00606CEC) = AnimationTrigger.PlacedOnCharger, after SmartPushIdleAnimation(0x23F = Count, no idle)");

        yield return new ReactionEntry(ReactionTrigger.RobotFalling, AnimationTrigger.ReactToImpact, Shipped,
            "reactionTrigger_behavior_map.json: ReactionTrigger.RobotFalling -> behaviour ReactToImpact; " +
            "BehaviorReactToImpact::TransitionToPlayingAnim at 0x00606348 passes 0x1A0 (ReactToImpact) to TriggerAnimationAction " +
            "(mov.w r2, #0x1a0 at 0x0060637E) = AnimationTrigger.ReactToImpact, gated on FallingStopped.impactIntensity > 1000 in AlwaysHandle at 0x00606408");

        // Absent from this table, and why. This table is the M7 dispatcher's: one animation per trigger.
        // The reactions the shipped map names for the triggers below are multi-step behaviours and run as
        // SteppedBehavior classes under BehaviorManager.CheckReactions (M10, ShippedBehaviors.Reactions):
        //
        //   RobotOnBack, RobotOnFace, RobotOnSide, RobotPlacedOnSlope,
        //   ReturnedToTreads, RobotShaken, UnexpectedMovement,
        //   MotorCalibration, Frustration (Minor)                      - M10, from the derived robot state
        //   CubeMoved -> ReactToCubeMoved                               - M10, transcribed; waits on a cube pose
        //
        // Still absent because the input does not exist here:
        //
        //   ObjectPositionUpdated -> AcknowledgeObject,
        //   NoPreDockPoses -> RamIntoBlock                              - M13, NoPreDockPosesStrategy, with a manipulation system
        //   FistBump -> FistBump                                        - needs located cubes and objectives
        //   FacePositionUpdated -> AcknowledgeFace,
        //   PetInitialDetection -> ReactToPet                          - need vision
        //   Sparked -> ReactToSparked, Hiccup -> Hiccup                 - need the app's spark request and the hiccup system
        //
        // None of these is mapped to a plausible-looking animation to pad the table.
    }
}
