namespace Cozmo.Robot.Behavior;

/// <summary>How well grounded a single mapping is. Recorded per entry, not assumed for the table.</summary>
public enum ReactionEvidence
{
    /// <summary>
    /// Read from a shipped artifact: a config file, a decompiled enum, or disassembled engine code.
    /// </summary>
    Shipped,

    /// <summary>
    /// Inferred from Anki's own naming across three independent artifacts, because the deciding code is
    /// not reachable. Correct in all likelihood, but **not** a decompiled fact. See <see cref="ReactionTable"/>.
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
/// **This table carries a documented uncertainty, and it is worth being plain about where.**
///
/// The chain has three links:
///
/// <code>
/// robot state  ->  ReactionTrigger  ->  AnimationTrigger  ->  group  ->  clip
///      (a)              (b)                   (c)
/// </code>
///
/// * **(a) and (c) are established.** (a) because Anki's <see cref="ReactionTrigger"/> values are named
///   after the very robot status flags M4 already reports — <c>CliffDetected</c>, <c>RobotPickedUp</c>,
///   <c>PlacedOnCharger</c>, <c>RobotFalling</c> are the flag names. (c) because
///   <see cref="AnimationTriggerMap"/> is Anki's own shipped file.
/// * **(b) is not.** In the shipped engine each reaction is a native <c>BehaviorReactToX</c> class that
///   holds its animation trigger in code. Those classes are **not exported**: the dynamic symbol table
///   contains only their <c>shared_ptr</c> deleters, so the deciding constant is not reachable without
///   locating each vtable and disassembling through it. Their JSON configs under
///   <c>config/engine/behaviorSystem/behaviors/reactions/</c> carry only <c>behaviorClass</c> and
///   <c>behaviorID</c> — no animation. Config-driven behaviours elsewhere do name their animation in an
///   <c>animTriggers</c> field, but none of the reaction behaviours does.
///
/// So the entries below are marked <see cref="ReactionEvidence.NameCorrespondence"/>: Anki named the
/// reaction trigger, the behaviour class and the animation trigger the same thing in three separate
/// artifacts, and the table follows that. That is strong, and it is still an assumption. It is recorded
/// here rather than buried, every entry states its basis, and <see cref="Entries"/> can be replaced
/// outright once the native path is read.
///
/// **No reaction is invented.** A <see cref="ReactionTrigger"/> with no plausible animation counterpart
/// is left out entirely rather than pointed at something that looks about right.
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

    private static IEnumerable<ReactionEntry> DefaultEntries()
    {
        const ReactionEvidence Corr = ReactionEvidence.NameCorrespondence;

        // Each of these is a reaction whose cause M4 already reports and whose animation trigger Anki
        // named to match. The basis string names the three artifacts that agree.
        yield return new ReactionEntry(ReactionTrigger.CliffDetected, AnimationTrigger.ReactToCliff, Corr,
            "RobotStatusFlag.CliffDetected; ReactionTrigger.CliffDetected; BehaviorReactToCliff; AnimationTrigger.ReactToCliff");

        yield return new ReactionEntry(ReactionTrigger.RobotPickedUp, AnimationTrigger.ReactToPickup, Corr,
            "RobotStatusFlag.IsPickedUp; ReactionTrigger.RobotPickedUp; BehaviorReactToPickup; AnimationTrigger.ReactToPickup");

        yield return new ReactionEntry(ReactionTrigger.PlacedOnCharger, AnimationTrigger.PlacedOnCharger, Corr,
            "RobotStatusFlag.IsOnCharger; ReactionTrigger.PlacedOnCharger; BehaviorReactToOnCharger; AnimationTrigger.PlacedOnCharger");

        yield return new ReactionEntry(ReactionTrigger.RobotFalling, AnimationTrigger.ReactToFalling, Corr,
            "RobotStatusFlag.IsFalling; ReactionTrigger.RobotFalling; AnimationTrigger.ReactToFalling");

        // Deliberately absent, and why:
        //
        //   CubeMoved, ObjectPositionUpdated, NoPreDockPoses, FistBump  - need cubes (M4 cube acceptance
        //                                                                is still pending hardware)
        //   FacePositionUpdated, PetInitialDetection                    - need vision, which does not exist yet
        //   RobotOnBack, RobotOnFace, RobotOnSide, RobotPlacedOnSlope,
        //   ReturnedToTreads, RobotShaken, UnexpectedMovement           - need IMU orientation classification
        //                                                                that M4 reports raw but does not classify
        //   Frustration, Sparked, Hiccup                                - need the mood/spark systems
        //   MotorCalibration                                            - AnimationTrigger.ReactToMotorCalibration
        //                                                                exists but the shipped map gives it no
        //                                                                group, so there is nothing to play
        //
        // None of these is mapped to a plausible-looking animation to pad the table.
    }
}
