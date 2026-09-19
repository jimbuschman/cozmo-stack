namespace Cozmo.Robot.Behavior;

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

        // Deliberately absent, and why. The shipped map names a behaviour for every one of these; what
        // is missing is the input, not the mapping.
        //
        //   CubeMoved -> ReactToCubeMoved, ObjectPositionUpdated -> AcknowledgeObject,
        //   NoPreDockPoses -> RamIntoBlock, FistBump -> FistBump      - need cubes (M4 cube acceptance
        //                                                                is still pending hardware)
        //   FacePositionUpdated -> AcknowledgeFace,
        //   PetInitialDetection -> ReactToPet                          - need vision, which does not exist yet
        //   RobotOnBack, RobotOnFace, RobotOnSide, RobotPlacedOnSlope,
        //   ReturnedToTreads, RobotShaken, UnexpectedMovement           - need IMU orientation classification
        //                                                                that M4 reports raw but does not classify
        //   Frustration -> ReactToFrustrationMinor/Major,
        //   Sparked -> ReactToSparked, Hiccup -> Hiccup                 - need the mood, spark and hiccup systems
        //   MotorCalibration -> ReactToMotorCalibration                 - its AnimationTrigger exists but the
        //                                                                shipped map gives it no group
        //
        // None of these is mapped to a plausible-looking animation to pad the table.
    }
}
