using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary><c>Anki::Cozmo::ActionResult</c> (UNITY values) for the outcomes this stack produces.</summary>
public enum ActionResult : uint
{
    Success = 0, Running = 0x01000000, CancelledWhileRunning = 0x02000000, Abort = 0x03000000,
    BadObject = 0x03000004, BadPose = 0x03000005, NoPreActionPoses = 0x03000010, NotCarryingObjectAbort = 0x03000011,
    PathPlanningFailedAbort = 0x03000013, StillCarryingObject = 0x03000017, Timeout = 0x03000018, VisualObservationFailed = 0x0300001D,
    /// <summary>0x03000014: <c>PickupObjectAction::Verify</c> "ObjectStillMoving" (r4 = 0x03000004 + 0x10 at 0x00553CEA).</summary>
    PickupObjectStillMoving = 0x03000014,
    /// <summary>0x03000015: <c>PickupObjectAction::Verify</c> not seen recently enough (r4 + 0x11 at 0x00553D6E).</summary>
    PickupObjectNotSeenRecently = 0x03000015,
    /// <summary>0x04000005: <c>PickupObjectAction::Verify</c> "expected carrying" (0x00553FC8..0x00554004, r5 = 0x04000005 at 0x00553DE8) and "seeing the object in its original pose" (0x00554116).</summary>
    PickupRetry = 0x04000005,
    Retry = 0x04000000, DidNotReachPreActionPose = 0x04000001, FailedTraversingPath = 0x04000002,
    /// <summary>0x03000008 (<c>0x0055AB7A/0x0055AB7C</c>): <c>DriveToPoseAction::CheckIfDone</c>'s initial result, kept when the arrival test fails while the path ids differ (M12-023).</summary>
    FollowingPathButNotTraversing = 0x03000008,
    /// <summary>0x0300000D: <c>DriveToObjectAction::InitHelper</c> for ActionType 6 with a strictly negative <c>+0x84</c> (0x00558FE2/0x00558FEA; NaN does not, 0x00558FDC..0x0055903E, M12-022).</summary>
    NoDistanceSet = 0x0300000D,
    /// <summary>0x03000002: <c>IDockAction::Init</c>'s "IDockAction.Init.NullDockMarker" (research Q6.8, M12-017).</summary>
    BadMarker = 0x03000002,
    /// <summary>0x04000008: <c>DriveToPlaceCarriedObjectAction::CheckIfDone</c> when the placement goal is not free (0x00559FA4, M12-030).</summary>
    PlacementGoalNotFree = 0x04000008,
    /// <summary>0x04000006: <c>BackupOntoChargerAction::CheckIfDone</c>'s fall-through when the straight drive
    /// finishes, and <c>ConfigureDriveForRetryAction</c>'s result (M13-008).</summary>
    RetryDriveDone = 0x04000006,
    /// <summary>0x04000009: <c>DriveOffChargerContactsAction::CheckIfDone</c> fails with this while the robot
    /// is still on the charger contacts (0x00558344, M13-013).</summary>
    StillOnCharger = 0x04000009,
    /// <summary>0x0400000A: <c>BackupOntoChargerAction::CheckIfDone</c>'s pitch-below result (0x0054E7DE, M13-008).</summary>
    BackupPitchedTooFar = 0x0400000A,
    /// <summary>0x03000016: <c>PlaceObjectOnGroundAction::Init</c> when <c>CarryingComponent::PlaceObjectOnGround</c> fails ("...SendPlaceObjectOnGroundFailed", <c>add.w r8,r8,#5</c> 0x005547F8, M15-022).</summary>
    SendPlaceObjectOnGroundFailed = 0x03000016,
}

/// <summary>
/// <c>ERobotDriveToPoseStatus</c>, the value <c>DriveToPoseAction::CheckIfDone</c> switches on
/// (<c>[[robot+0x5C]+0x38]</c>): the name table at 0x0102F610, indexed by
/// <c>PathComponent::SetDriveToPoseStatus</c> 0x006492DE..0x006492E8 (M12-023).
/// </summary>
public enum DriveToPoseStatus
{
    Failed = 0, ComputingPath = 1, WaitingToBeginPath = 2, FollowingPath = 3, Ready = 4,
    WaitingToCancelPath = 5, WaitingToCancelPathAndSetFailure = 6,
}

/// <summary>
/// The inputs one call of <c>DriveToPoseAction::CheckIfDone</c> 0x0055AB4C reads from outside the action:
/// the path status <c>[[robot+0x5C]+0x38]</c>, the clock, the robot pose, <c>Robot::GetHeight</c> and the two
/// path ids <c>[[robot+0x5C]+0x42]</c> and <c>[[robot+0x5C]+0x44]</c> (M12-023). The engine's names for the
/// two ids are not in the inventory, so they are named by offset.
/// </summary>
public readonly record struct DriveToPoseTick(int PathStatus, double NowSec, Pose3d RobotPose, double RobotHeightMm,
                                              ushort PathIdAt0x42, ushort PathIdAt0x44);

/// <summary>
/// The engine's <c>DrivingAnimationHandler</c> (<c>[robot+0x24C]</c>) as <c>DriveToPoseAction::CheckIfDone</c>
/// uses it: the state word it reads first (<c>[[robot+0x24C]] == 3</c> keeps the action RUNNING), the start
/// animation at path state 3 and the end animation at the tail. M12-024 (RECOVERABLE_GAP): the handler's
/// states, the animations it chooses and what its return value means are not read, so the default
/// implementation is <see cref="UnreadDrivingAnimationHandler"/>, which throws.
/// </summary>
// fidelity: M12-024
public interface IDrivingAnimationHandler
{
    /// <summary><c>[[robot+0x24C]]</c> (0x0055AB5C..0x0055AB6A).</summary>
    int State { get; }
    /// <summary><c>DrivingAnimationHandler::PlayStartAnim</c> (PLT 0x004AB134; call 0x0055AC08). Its return value is not used by CheckIfDone.</summary>
    void PlayStartAnim();
    /// <summary><c>DrivingAnimationHandler::PlayEndAnim</c> (PLT 0x004A5764; call 0x0055B0AA). Non-zero keeps CheckIfDone RUNNING (0x0055B0AA..0x0055B0CC).</summary>
    uint PlayEndAnim();
}

/// <summary>
/// The visible stub for M12-024: every member throws <see cref="NotSupportedException"/>, because the
/// handler's body is RECOVERABLE_GAP (not read). It exists so that nothing can run
/// <c>DriveToPoseAction.CheckIfDone</c> and get a silently-invented animation behaviour.
/// </summary>
// fidelity: M12-024
public sealed class UnreadDrivingAnimationHandler : IDrivingAnimationHandler
{
    public static readonly UnreadDrivingAnimationHandler Instance = new();
    private const string Gap = "M12-024 RECOVERABLE_GAP: DrivingAnimationHandler (PLT 0x004AB134 / 0x004A5764) is not read.";
    public int State => throw new NotSupportedException(Gap);
    public void PlayStartAnim() => throw new NotSupportedException(Gap);
    public uint PlayEndAnim() => throw new NotSupportedException(Gap);
}

/// <summary>
/// The engine's <c>DriveToPoseAction</c> (0x0055A238..0x0055B1E8): the head goes to the path-following angle
/// −15 degrees (0xBE860A92, <c>MovementComponent::MoveHeadToAngle</c> in Init), a path to the goal is planned
/// and executed, and the action succeeds when the robot's pose is within the goal tolerance (angle 0.174533 rad,
/// 0x3E32B8C2, from the constructor; distance from <see cref="CubePreActionPoses.DistanceThresholdMm"/> when a
/// pre-action pose is the goal) once the path has been traversed. INFERRED: the traversal timeout (LOCAL, from
/// the path length). When no motion-primitive set is loaded the planner is LOCAL (<see cref="StraightLinePlanner"/>);
/// the engine always has the lattice planner.
///
/// <see cref="CheckIfDone(DriveToPoseTick)"/> is the engine's <c>CheckIfDone</c> 0x0055AB4C state machine
/// (M12-023). It is a pure function of its inputs and is NOT what <see cref="RunAsync"/> polls: the C# has no
/// <c>PathComponent</c> status word to feed it (which robot events set states 1..6 is not in the inventory), so
/// <see cref="RunAsync"/> waits on the path's terminal event and then applies the state-4 (Ready) arrival test
/// through the same <see cref="ReadyState"/>.
/// </summary>
public sealed class DriveToPoseAction
{
    public const double PathFollowingHeadAngleRad = -0.261799;
    /// <summary><c>[this+0x9C]</c>'s constructor default, Radians(0x3E32B8C2) (0.174533).</summary>
    public const double GoalAngleToleranceRad = 0.174533;
    /// <summary><c>[this+0x90..0x98]</c>'s constructor default, 10.0 (inventory G3.9, base ctor 0x0055A238).</summary>
    public const double DefaultGoalDistanceToleranceMm = 10.0;
    /// <summary><c>[this+0xA8]</c>'s constructor default, 4.0 (inventory G3.9): the ComputingPath timeout in seconds.</summary>
    public const double DefaultComputingPathTimeoutSec = 4.0;

    private readonly ManipulationSystem _m;

    public DriveToPoseAction(ManipulationSystem m) { _m = m; AbortPath = () => _m.Paths.Abort(); }

    public Pose3d? Goal { get; set; }
    /// <summary>Alternative goals for the lattice planner (a cube's pre-action poses); the reached one becomes <see cref="Goal"/>.</summary>
    public IReadOnlyList<Pose3d>? Goals { get; set; }
    /// <summary>The engine's <c>idx</c>, the byte at <c>[[this+0x88]]</c> that selects <c>goals[idx]</c> (0x0055ACCE..0x0055ACEA).</summary>
    public int GoalIndex { get; set; }
    /// <summary>Objects not to treat as obstacles (the one being docked with).</summary>
    public IEnumerable<uint>? IgnoreObstacleIds { get; set; }
    /// <summary><c>[this+0x90]</c>/<c>[this+0x94]</c>: the x and y position tolerance when <see cref="PreActionObjectPose"/> is null.</summary>
    public double DistanceToleranceMm { get; set; } = DefaultGoalDistanceToleranceMm;
    /// <summary>
    /// <c>[this+0xB4]</c> with <c>[this+0xC0] != 0</c> (M12-020, M12-023): the object pose the goal belongs to.
    /// When set, the Ready-state tolerance x/y come from <c>ComputePreActionPoseDistThreshold(out, goals[idx],
    /// objectPose, [this+0x9C])</c> (0x0055ACF0). The engine sets <c>+0xC0 = 1</c> only in
    /// <c>DriveToObjectAction::InitHelper</c> (0x0055933E) and 0 in the constructor (0x0055A31E).
    /// </summary>
    public Pose3d? PreActionObjectPose { get; set; }
    /// <summary><c>[this+0x9C]</c>: the one angle tolerance, used both by the threshold call (0x0055ACF0) and by <c>IsSameAs</c> (0x0055ADA8).</summary>
    public double AngleToleranceRad { get; set; } = GoalAngleToleranceRad;
    /// <summary><c>[this+0xA8]</c>, seconds.</summary>
    public double PlanningTimeoutSec { get; set; } = DefaultComputingPathTimeoutSec;
    /// <summary><c>[this+0xB0]</c>: the ComputingPath deadline in seconds, -1.0 when unset.</summary>
    public double DeadlineSec { get; private set; } = -1.0;
    /// <summary><c>[this+0xC4]</c>, incremented at path state 3.</summary>
    public int FollowingPathTicks { get; private set; }
    public IDrivingAnimationHandler DrivingAnimations { get; set; } = UnreadDrivingAnimationHandler.Instance;
    /// <summary><c>PathComponent::Abort</c> (0x0055AD5C).</summary>
    public Action AbortPath { get; set; }
    public PathMotionProfile Profile { get; set; } = PathMotionProfile.Default;
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    private Pose3d GoalAtIndex()
    {
        if (Goals is { Count: > 0 } g && GoalIndex >= 0 && GoalIndex < g.Count) return g[GoalIndex];
        return Goal ?? throw new InvalidOperationException("DriveToPoseAction has no goal");
    }

    /// <summary>
    /// <c>DriveToPoseAction::CheckIfDone</c> 0x0055AB4C, one call (M12-023). NO PRODUCTION CALLER: only tests call it; <see cref="RunAsync"/> applies just the
    /// state-4 test through <see cref="ReadyState"/> (the C# has no PathComponent status word to feed this, M12-032).
    /// <list type="bullet">
    /// <item><c>[[robot+0x24C]] == 3</c> returns RUNNING (0x0055AB5C..0x0055AB6A); a path status above 4 returns RUNNING (0x0055AB74).</item>
    /// <item>The initial result is 0x03000008 (0x0055AB7A).</item>
    /// <item>State 0 Failed: <c>[+0xB0] = -1</c>, 0x03000013, no end animation. State 1 ComputingPath: an unset deadline becomes
    ///   now+<c>[+0xA8]</c> and returns RUNNING, a future deadline returns RUNNING, otherwise <c>PathComponent::Abort</c>,
    ///   deadline -1 and 0x03000013. State 2 returns RUNNING. State 3: <c>PlayStartAnim</c>, deadline -1, <c>[+0xC4]++</c>, RUNNING.</item>
    /// <item>State 4 Ready: see <see cref="ReadyState"/>.</item>
    /// <item>Tail (0x0055B0AA..0x0055B0CC): a result other than RUNNING and 0x03000013 calls <c>PlayEndAnim</c>; non-zero keeps RUNNING.</item>
    /// </list>
    /// </summary>
    // fidelity: M12-023
    public ActionResult CheckIfDone(DriveToPoseTick tick)
    {
        var result = ActionResult.FollowingPathButNotTraversing;
        if (DrivingAnimations.State == 3) return ActionResult.Running;
        if ((uint)tick.PathStatus > 4) return ActionResult.Running;
        switch ((DriveToPoseStatus)tick.PathStatus)
        {
            case DriveToPoseStatus.Failed:
                DeadlineSec = -1.0;
                _trace.Add("DriveToPoseAction.CheckIfDone: path status Failed");
                return ActionResult.PathPlanningFailedAbort;
            case DriveToPoseStatus.ComputingPath:
                if (DeadlineSec < 0) { DeadlineSec = tick.NowSec + PlanningTimeoutSec; return ActionResult.Running; }
                if (tick.NowSec < DeadlineSec) return ActionResult.Running;
                _trace.Add($"DriveToPoseAction.CheckIfDone.ComputingPathTimeout: {PlanningTimeoutSec}");
                AbortPath();
                DeadlineSec = -1.0;
                return ActionResult.PathPlanningFailedAbort;
            case DriveToPoseStatus.WaitingToBeginPath:
                return ActionResult.Running;
            case DriveToPoseStatus.FollowingPath:
                DrivingAnimations.PlayStartAnim();
                DeadlineSec = -1.0;
                FollowingPathTicks++;
                return ActionResult.Running;
            default:
                result = ReadyState(GoalAtIndex(), tick.RobotPose, tick.RobotHeightMm, tick.PathIdAt0x42, tick.PathIdAt0x44);
                break;
        }
        if (result != ActionResult.Running && result != ActionResult.PathPlanningFailedAbort
            && DrivingAnimations.PlayEndAnim() != 0)
            result = ActionResult.Running;
        return result;
    }

    /// <summary>
    /// State 4 (Ready) of <c>DriveToPoseAction::CheckIfDone</c> (0x0055ACA2..0x0055B0A6), before the tail:
    /// <c>[+0xB0] = -1</c>; the tolerance <c>Point3 {[+0x90], [+0x94], Robot::GetHeight}</c>; if <c>[+0xC0]</c> the x and y are
    /// replaced by BOTH outputs of <c>ComputePreActionPoseDistThreshold(out, goals[idx], this+0xB4, this+0x9C)</c>
    /// (0x0055ACF0, goal first and object second; the -1.0 sentinel pair is used as it comes back); arrival is
    /// <c>Pose3d::IsSameAs(robotPose, goals[idx], tolerance, [+0x9C])</c>; true gives 0, false gives 0x04000002 when the two
    /// path ids are equal and otherwise keeps 0x03000008 (0x0055AEC2..0x0055B0A6).
    /// </summary>
    // fidelity: M12-020, M12-023
    internal ActionResult ReadyState(Pose3d goal, Pose3d robotPose, double heightMm, ushort pathIdAt0x42, ushort pathIdAt0x44)
    {
        DeadlineSec = -1.0;
        double tx = DistanceToleranceMm, ty = DistanceToleranceMm;
        if (PreActionObjectPose is { } objectPose)
            CubePreActionPoses.DistanceThresholdMm(goal, objectPose, AngleToleranceRad, out tx, out ty);
        var tol = new Vec3(tx, ty, heightMm);
        // Pose3d::IsSameAs 0x00846EA4 uses a default-constructed empty RotationAmbiguities and compares
        // the full relative rotation (GetAngleDiffFrom 0x0084A694), so the full goal pose is passed.
        if (robotPose.IsSameAs(goal, tol, AngleToleranceRad, out _))
        {
            _trace.Add($"DriveToPoseAction.CheckIfDone.Success: threshold=({tx:F1},{ty:F1})");
            return ActionResult.Success;
        }
        if (pathIdAt0x42 != pathIdAt0x44)
        {
            _trace.Add("DriveToPoseAction.CheckIfDone: not at the goal and the path ids differ; result stays 0x03000008");
            return ActionResult.FollowingPathButNotTraversing;
        }
        _trace.Add($"DriveToPoseAction.CheckIfDone.DoneNotInPlace: not within threshold=({tx:F1},{ty:F1})");
        return ActionResult.FailedTraversingPath;
    }

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        if (Goal is not { } goal) { _trace.Add("DriveToPoseAction.Init.NoGoalSet"); return ActionResult.BadPose; }
        var start = _m.RobotPose();
        if (start is null) { _trace.Add("no robot state"); return ActionResult.Abort; }
        _ = _m.Robot.Motion.SetHeadAngleAsync((float)PathFollowingHeadAngleRad, CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2, requireCalibration: false);
        IReadOnlyList<PathSegment> path;
        if (_m.Planner is { } planner)
        {
            // LatticePlannerImpl::StartPlanning 0x004FEB44: store the replan bool at impl+0xA1 and import the
            // world's obstacles with the bool hard-coded 1 (0x004FEC38); then DoPlanning, whose failure
            // sends no path.
            // fidelity: M13-003, M13-005, M13-018
            planner.StartPlanning(_m.World, _m.Docking.Carrying.CarriedObjectId, forceReplan: true, IgnoreObstacleIds);
            var goals = Goals is { Count: > 0 } ? Goals : new[] { goal };
            var planned = planner.PlanTo(start.Value, goals, Profile);
            if (planned is { } pl)
            {
                path = pl.Path; goal = pl.Goal;
                _trace.Add($"lattice plan: {pl.Plan.Actions.Count} primitive(s), cost {pl.Plan.Cost:F0}, {pl.Plan.Expansions} expansions, {planner.Env.ObstacleCount} obstacle(s), DoPlanning={planner.LastPlanningResult}");
            }
            else
            {
                // The engine returns a planning failure and the action fails; it does not substitute a
                // path of its own. This used to drive the straight line whenever the same environment
                // reported it clear, which is a path the engine would never have sent.
                _trace.Add($"DriveToPoseAction.Init.PlanningFailed: no lattice plan ({planner.Env.ObstacleCount} obstacle(s)); no path sent");
                return ActionResult.PathPlanningFailedAbort;
            }
        }
        else path = StraightLinePlanner.Plan(start.Value, goal, Profile);
        if (path.Count == 0) { _trace.Add("already at the goal"); return ActionResult.Success; }
        using var run = _m.StartPath(path);
        ushort id = run.PathId;
        _trace.Add($"path {id}: {path.Count} segment(s) from {start.Value.Translation} to {goal.Translation}");
        double lengthMm = 0;
        foreach (var s in path)
        {
            if (s is PathSegment.Line l) lengthMm += Math.Sqrt((l.ToX - l.FromX) * (l.ToX - l.FromX) + (l.ToY - l.FromY) * (l.ToY - l.FromY));
            else if (s is PathSegment.Arc a) lengthMm += Math.Abs(a.SweepRad) * a.RadiusMm;
        }
        var timeout = TimeSpan.FromSeconds(5 + lengthMm / Math.Max(20, Profile.SpeedMmps) * 2 + path.Count * 3);
        var ev = await run.WaitAsync(timeout, cancel);
        if (ev is null) { _trace.Add("DriveToPoseAction.CheckIfDone.Failure: no path completion; path aborted"); return cancel.IsCancellationRequested ? ActionResult.CancelledWhileRunning : ActionResult.FailedTraversingPath; }
        if (ev == PathEventType.Interrupted) { _trace.Add("path interrupted"); return ActionResult.FailedTraversingPath; }
        var now = _m.RobotPose();
        if (now is null) return ActionResult.Abort;
        // M12-024 (RECOVERABLE_GAP): the engine plays DrivingAnimationHandler::PlayStartAnim at path state 3 and
        // PlayEndAnim at the tail of CheckIfDone; this wait-for-the-terminal-event flow plays neither.
        _trace.Add("DrivingAnimationHandler PlayStartAnim/PlayEndAnim not run (M12-024 RECOVERABLE_GAP)");
        // fidelity: M12-023, M12-032
        // The terminal event (Completed) of the path just executed stands for the Ready state (status 4, M12-023). The result for "finished but outside the
        // tolerance" is decided by the path-id pair (M12-023): equal ids give 0x04000002, different ids keep 0x03000008. ASSUMPTION, disclosed: both ids are
        // this path's id, because +0x42 is the last id sent (ExecutePath increments it) and +0x44 the last id the robot reported started (M12-032, research
        // Q8), and a Completed event for this id follows a Started event for it. That changes the live result for a finished drive that ended outside the
        // tolerance from 0x04000001 (the earlier code) to 0x04000002; callers that switch on the code see it (CubeGameBehaviors' PopAWheelie retry). The
        // interrupted event returns FailedTraversingPath without the Ready test although the engine sets status 4 for it too (research 8.3): M12-032 is
        // RECOVERABLE_GAP and the setters' conditions are not all read.
        var ready = ReadyState(goal, now.Value, RobotHeightMm(), id, id);
        if (ready == ActionResult.Success) Goal = goal;
        return ready;
    }

    /// <summary>
    /// <c>Robot::GetHeight</c> 0x00516F0C = <c>max(66·sin(liftAngle) + 45 + 5, 67.7)</c> (66 at 0x00516F54,
    /// 45 at 0x00516F58, 5.0, floor 0x42876666 = 67.7; C-E7/E9). It is the z of the <c>Point3</c> tolerance
    /// <c>DriveToPoseAction::CheckIfDone</c> passes to <c>IsSameAs</c> (0x0055ACBA, 0x0055ACC2/0x0055ACF8).
    /// </summary>
    private double RobotHeightMm() => _m.RobotHeightMm();

}

/// <summary>
/// The C# stand-in for the <c>std::function</c> at <c>DriveToObjectAction+0x150</c> (M12-011, M12-029, M12-035): called with
/// the object, it returns the action result, the pre-action poses (the engine's vector arrives empty at both call sites this stack models) and the in-position flag
/// (<c>bool&amp;</c> at 0x00559A9C: an IN/OUT argument, because the flip-block installers read it, M12-035). The elements are <see cref="PreActionPose"/>s whose
/// <see cref="PreActionPose.WorldPose"/> is the engine's <c>Pose3d</c>.
/// </summary>
public delegate ActionResult PosesFunction(ObservableObject obj, out IReadOnlyList<PreActionPose> poses, ref bool alreadyInPosition);

/// <summary>
/// The engine's <c>DriveToObjectAction</c> (0x00558524..0x00559A00): <c>Init</c> looks the object up in the
/// world ("block world does not have an ActionableObject with ID"), <c>InitHelper</c> takes the object's
/// pre-action poses for the action type from the <c>std::function</c> at <c>+0x150</c> (<see cref="GetPossiblePoses"/> by default, M12-029), and unless the
/// robot is already in position runs a <c>DriveToPoseAction</c> (thresholds (10, 10, 10), angle 0.174533, the object pose as its <c>+0xB4</c> and
/// <c>+0xC0 = 1</c>; its goals are ALL the returned poses in the engine and one reduced goal here, see <see cref="InitHelperAsync"/>), then a
/// <c>TurnTowardsObjectAction</c> unless the robot is carrying the object (research Q12.8). The pre-action angle tolerance is the constructor's 7.5 degrees (0x3E060A92 at <c>+0x14C</c>).
///
/// There are two constructors and they differ in <c>ActionType</c> (<c>+0x80</c>) and <c>+0x84</c> (M12-022). The
/// 7-arg constructor 0x0055850C takes the ActionType as an argument and stores -1.0 at <c>+0x84</c> (0x005585A0). The
/// 4-arg constructor 0x005587B8 stores ActionType 6 and its float argument at <c>+0x84</c> (0x0055883C, 0x0055884C).
/// ActionType 6 is the only value for which <c>InitHelper</c> builds the object+delta goal and <c>CheckIfDone</c>
/// compares against <c>+0x84</c>; every other ActionType goes through the <c>std::function</c> at <c>+0x150</c>.
/// </summary>
public sealed class DriveToObjectAction
{
    // fidelity: M12-011
    /// <summary><c>+0x14C</c>: Radians 0x3E060A92 (7.5 degrees), the ctor's <c>GetPossiblePoses</c> angle tolerance (research Q12.6, 0x0055850C).</summary>
    public static readonly double PreActionAngleToleranceRad = BitConverter.UInt32BitsToSingle(0x3E060A92);
    /// <summary><c>+0x84</c> on the 7-arg path: -1.0f (0x005585A0, r1 = 0xBF800000).</summary>
    public const float UnsetDistanceMm = -1.0f;
    /// <summary>
    /// The ActionType <c>DriveToPlaceCarriedObjectAction</c>'s constructor 0x00559CD4 passes to the 7-arg base: 2 when
    /// its first bool is non-zero (0x00559CDE/0x00559CE4/0x00559CE8); its only caller passes 1 there
    /// (0x00554B40/0x00554B42), so production is always 2 (M12-022; the class is <see cref="DriveToPlaceCarriedObjectAction"/>, M12-030).
    /// </summary>
    public const PreActionType DriveToPlaceCarriedObjectActionType = PreActionType.PlaceOnGround;

    private readonly ManipulationSystem _m;

    /// <summary>The 7-arg constructor 0x0055850C: <paramref name="type"/> is <c>+0x80</c>, <c>+0x84</c> is -1.0 and <c>+0x148</c> is 1 (inventory G3.9); the default <c>+0x150</c> function is installed (0x005585F8).</summary>
    public DriveToObjectAction(ManipulationSystem m, uint objectId, PreActionType type)
    {
        _m = m; ObjectId = objectId; ActionType = type;
        DistanceFromObjectOrigin = UnsetDistanceMm;
        PosesFunction = GetPossiblePoses;
    }

    /// <summary>The 4-arg constructor 0x005587B8: ActionType 6 (<see cref="PreActionType.None"/>), <c>+0x84</c> is <paramref name="distanceFromObjectOriginMm"/>, <c>+0x8C</c> the bool, <c>+0x148</c> is 1; the default function is installed (0x0055889C).</summary>
    public DriveToObjectAction(ManipulationSystem m, uint objectId, float distanceFromObjectOriginMm, bool useManualSpeed)
    {
        _m = m; ObjectId = objectId; ActionType = PreActionType.None;
        DistanceFromObjectOrigin = distanceFromObjectOriginMm;
        UseManualSpeed = useManualSpeed;
        PosesFunction = GetPossiblePoses;
    }

    /// <summary>
    /// The RobotActionUnion GotoObject factory 0x0052A090 (M12-022): <c>usePreDockPose</c> set (<c>[msg+0x35]</c>, 0x0052A0BA) builds
    /// the 7-arg constructor with ActionType 0; clear builds the 4-arg constructor with
    /// <c>distanceFromObjectOrigin_mm</c> (<c>[msg+0x30]</c>) and <c>useManualSpeed</c> (<c>[msg+0x34]</c>) (0x0052A0E0..0x0052A0EE).
    /// The message's "selected object" id and custom motion profile are not modelled. NO PRODUCTION CALLER: nothing in this stack decodes the app's
    /// GotoObject message and the shipped app always sends <c>usePreDockPose</c> true (M12-022), so this factory is reached only from tests.
    /// </summary>
    public static DriveToObjectAction FromGotoObject(ManipulationSystem m, uint objectId, float distanceFromObjectOriginMm,
                                                     bool useManualSpeed, bool usePreDockPose) =>
        usePreDockPose ? new DriveToObjectAction(m, objectId, PreActionType.Docking)
                       : new DriveToObjectAction(m, objectId, distanceFromObjectOriginMm, useManualSpeed);

    public uint ObjectId { get; }
    /// <summary><c>+0x80</c>.</summary>
    public PreActionType ActionType { get; }
    public PreActionType Type => ActionType;
    /// <summary><c>+0x84</c>, a float.</summary>
    public float DistanceFromObjectOrigin { get; }
    /// <summary><c>+0x8C</c>. Not plumbed into <see cref="DriveToPoseAction"/> (which has no manual-speed input here).</summary>
    public bool UseManualSpeed { get; }
    /// <summary>
    /// <c>+0x88</c>, <c>+0x13C</c>, <c>+0x140</c>: the 7-arg constructor's distanceFromMarker, useApproachAngle and approach angle, which feed
    /// <c>PreActionPoseInput+0x10/+0x14/+0x18</c> (research Q12.6). The callers' values are not itemised (M12-022 (b)); the C# callers build the action
    /// without them, so they default to 0, false and 0 as they always did.
    /// </summary>
    public double DistanceFromMarkerMm { get; set; }
    public bool UseApproachAngle { get; set; }
    public double ApproachAngleRad { get; set; }
    /// <summary><c>+0x148</c>: 1 from both constructors (inventory G3.9; 11.1). Zero makes <see cref="CheckIfDone"/> return 0.</summary>
    public bool CheckAtEnd { get; set; } = true;
    /// <summary>The <c>std::function</c> at <c>+0x150</c> (M12-011). The default is <see cref="GetPossiblePoses"/> (M12-029).</summary>
    // fidelity: M12-028
    public PosesFunction PosesFunction { get; set; }
    public PathMotionProfile Profile { get; set; } = PathMotionProfile.Default;
    /// <summary>
    /// The pre-action pose the drive reached, or the closest one when the robot was already in position. C# bookkeeping for <see cref="DockHelper"/>'s retry
    /// exclusion; the engine has no such field (its <c>DriveToHelper+0x114</c> is the pose <see cref="ExcludePoses"/> stands for).
    /// </summary>
    public PreActionPose? Chosen { get; private set; }
    private IReadOnlyList<Pose3d> _excludePoses = Array.Empty<Pose3d>();
    private Pose3d? _driveToHelperMatch;
    /// <summary>
    /// The poses a previous attempt already failed from, dropped the way <c>IBehavior::UseSecondClosestPreActionPose</c> 0x005BEE56 does it (M12-004): it asks
    /// <c>GetPossiblePoses</c> again, requires at least two poses (0x005BEE7C..0x005BEE80) and calls <c>IDockAction::RemoveMatchingPredockPose</c>
    /// (0x00551418, M12-029) for the pose, clearing the in-position flag when it removed something. <see cref="DockHelper"/> (M8) accumulates the poses of every failed
    /// attempt, so the removal is repeated per pose while more than one remains (a C# generalisation of the engine's single pose, disclosed).
    /// </summary>
    // fidelity: M12-004, M12-029
    public IReadOnlyList<Pose3d> ExcludePoses
    {
        get => _excludePoses;
        set { _excludePoses = value; PosesFunction = UsesExclusions ? PosesWithExclusions : GetPossiblePoses; }
    }
    /// <summary>
    /// The <c>DriveToHelper</c> functor (0x005B58C2 installs it, operator() 0x005B61A2; M12-029): <see cref="GetPossiblePoses"/>, then, when it returns 0,
    /// <c>RemoveMatchingPredockPose</c> for the pose at <c>DriveToHelper+0x114</c> with NO minimum pose count, clearing the in-position flag if it removed something.
    /// NO PRODUCTION CALLER: <c>DriveToHelper</c> (M8) is not built here, so only a test sets it.
    /// </summary>
    // fidelity: M12-029
    public Pose3d? DriveToHelperMatchPose
    {
        get => _driveToHelperMatch;
        set { _driveToHelperMatch = value; PosesFunction = UsesExclusions ? PosesWithExclusions : GetPossiblePoses; }
    }
    private bool UsesExclusions => _driveToHelperMatch is not null || _excludePoses.Count > 0;
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    /// <summary>
    /// <c>DriveToObjectAction::GetPossiblePoses(obj, poses, inPos&amp;)</c> 0x00558C80 (M12-029; the default functors 0x0055DB72 / 0x0055DBDE tail-call it): builds
    /// the <c>PreActionPoseInput</c> {obj, ActionType, flag A 0, <c>[+0x14C]</c>, <c>[+0x88]</c>, <c>[+0x13C]</c>, <c>[+0x140]</c>} and calls
    /// <c>IDockAction::GetPreActionPoses</c> (M12-031); a non-zero result is returned; an empty pose list warns "did not return any pre-action poses" and returns
    /// 0x03000010; <c>*inPos</c> is the output's in-position byte (<c>out+0x1C</c>); every pose is pushed in order (with <c>inPos</c> set the engine only logs
    /// "close enough ... using current robot pose as goal" and replaces no pose); returns 0.
    /// </summary>
    // fidelity: M12-029, M12-031
    public ActionResult GetPossiblePoses(ObservableObject obj, out IReadOnlyList<PreActionPose> posesOut, ref bool alreadyInPosition)
    {
        posesOut = Array.Empty<PreActionPose>();
        var robot = _m.RobotPose();
        if (robot is null) return ActionResult.Abort;
        var output = DockPreActionPoses.Get(new PreActionPoseInput(obj, ActionType, FlagA: false, PreActionAngleToleranceRad, DistanceFromMarkerMm, UseApproachAngle, ApproachAngleRad),
                                            robot.Value, _m.Docking.Carrying.CarriedObjectId, () => _m.GetObstacles(robot.Value), _trace);
        if (output.Result != ActionResult.Success) return output.Result;
        if (output.Poses.Count == 0)
        {
            _trace.Add($"DriveToObjectAction.CheckPreconditions.NoPreActionPoses: ActionableObject {obj.ObjectId} did not return any pre-action poses with action type {(int)ActionType}.");
            return ActionResult.NoPreActionPoses;
        }
        alreadyInPosition = output.InPosition;
        _trace.Add($"GetPreActionPoses: {output.Poses.Count} pose(s), closest #{output.ClosestIndex} at |d|=({output.OffsetX:F1},{output.OffsetY:F1}), threshold=({output.ThresholdX:F1},{output.ThresholdY:F1}), inPosition={output.InPosition}");
        if (alreadyInPosition) _trace.Add("DriveToObjectAction.GetPossiblePoses.UseRobotPose: Robot's current pose is close enough to preAction pose");
        posesOut = output.Poses.ToList();
        return ActionResult.Success;
    }

    /// <summary>The <c>+0x150</c> function when <see cref="DriveToHelperMatchPose"/> or <see cref="ExcludePoses"/> is set: <see cref="GetPossiblePoses"/> then the removals described there.</summary>
    // fidelity: M12-029, M12-004
    private ActionResult PosesWithExclusions(ObservableObject obj, out IReadOnlyList<PreActionPose> posesOut, ref bool alreadyInPosition)
    {
        var result = GetPossiblePoses(obj, out posesOut, ref alreadyInPosition);
        if (result != ActionResult.Success) return result;
        var list = posesOut.ToList();
        bool removed = false;
        if (_driveToHelperMatch is { } match) removed |= DockPreActionPoses.RemoveMatchingPredockPose(match, list);
        foreach (var used in _excludePoses)
        {
            if (list.Count <= 1) break;                       // UseSecondClosestPreActionPose 0x005BEE7C..0x005BEE80: at least two poses
            removed |= DockPreActionPoses.RemoveMatchingPredockPose(used, list);
        }
        if (removed) { alreadyInPosition = false; _trace.Add("Trying again with a different predock pose"); }
        posesOut = list;
        return result;
    }

    /// <summary>
    /// <c>DriveToObjectAction::Init</c> 0x00559650 (the object lookup; the cube-light step 12.7 is M10 and not modelled) then <c>InitHelper</c>, then the
    /// <c>CheckIfDone</c> the compound reaches.
    /// </summary>
    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var obj = _m.World.GetLocatedObjectById(ObjectId);
        if (obj is null) { _trace.Add($"DriveToObjectAction.CheckPreconditions.NoObjectWithID {ObjectId}"); return ActionResult.BadObject; }
        var init = await InitHelperAsync(obj, cancel);
        if (init != ActionResult.Success) return init;
        return CheckIfDone();
    }

    /// <summary>
    /// <c>DriveToObjectAction::InitHelper(obj)</c> 0x00558FB4 (M12-022; research Q12.8), including the compound it starts (the async model runs the compound to its
    /// end): ActionType 6 builds the object+delta goal (reachable only from an external GotoObject), every other type asks the <c>+0x150</c> function for the poses
    /// and the in-position flag; unless in position the <c>DriveToPoseAction</c> to ALL the poses runs; then the turn towards the object unless it is carried.
    /// </summary>
    // fidelity: M12-022, M12-029
    internal async Task<ActionResult> InitHelperAsync(ObservableObject obj, CancellationToken cancel)
    {
        var robot = _m.RobotPose();
        if (robot is null) return ActionResult.Abort;
        if (ActionType == PreActionType.None)
        {
            // InitHelper 0x00558FB4 for ActionType 6: the object+delta goal.
            var r6 = await DriveToObjectDeltaGoalAsync(obj, robot.Value, cancel);
            if (r6 != ActionResult.Success) return r6;
        }
        else
        {
            // InitHelper 0x00558FB4 for every other ActionType: the std::function at +0x150 (0x00559048..0x00559058).
            bool inPosition = false;                                   // the recorded call site passes 0 (0x00559048..0x00559054)
            var fnResult = PosesFunction(obj, out var poses, ref inPosition);
            if (fnResult != ActionResult.Success) return fnResult;
            if (inPosition)
            {
                Chosen = poses.Count > 0 ? CubePreActionPoses.Closest(poses, robot.Value) : null;
                _trace.Add("DriveToObjectAction: in position, no DriveToPoseAction");
            }
            else
            {
                if (poses.Count == 0)
                {
                    // The function returned success with no poses (the DriveToHelper functor can empty the list). DriveToPoseAction::Init would call
                    // StartDrivingToPose with no goals, which fails (research 12.5, 0x0064ADF6..0x0064AE2E) and gives FailedToFindPath 0x03000013 (12.4).
                    // ASSUMPTION, disclosed: that SetGoals marks the goals as set (+0x78) even for an empty vector, otherwise 12.4 would give 0x0300000F.
                    _trace.Add("DriveToPoseAction.Init.FailedToFindPath: no goals");
                    return ActionResult.PathPlanningFailedAbort;
                }
                // LABELLED REDUCTION of SetGoals 0x0055A6D0, which stores the returned poses as-is as the goal vector (research Q12.8, M12-022): the goal
                // here is the closest pose's position with the heading toward the object, not all the poses with their full rotation. Reason: the pose
                // estimates this stack's vision produces for a far cube can be tilted (a cube at 260 mm comes out 21 degrees off flat, past the 20-degree
                // flatten clamp of M11-006), so a full-rotation goal built from it can never satisfy the full-rotation IsSameAs arrival test (M12-020) that a
                // flat robot pose is held to. The object pose as +0xB4, +0xC0 = 1 (the threshold pair) and the (10, 10, 10) / 0x3E32B8C2 tolerances are the engine's.
                var closest = CubePreActionPoses.Closest(poses, robot.Value)!;
                var goal = new Pose3d(Mat3.AboutZ(YawTowardObject(obj.Pose, closest.WorldPose)), closest.WorldPose.Translation);
                var drive = new DriveToPoseAction(_m) { Goal = goal, Goals = new[] { goal }, IgnoreObstacleIds = new[] { ObjectId },
                                                         PreActionObjectPose = obj.Pose, Profile = Profile };
                var r = await drive.RunAsync(cancel);
                _trace.AddRange(drive.Trace);
                if (r != ActionResult.Success) return r;
                Chosen = closest;
                if (drive.Goal is { } reached)
                {
                    // the goal is yaw-only with the heading toward the object, so match the reached goal to a pose by position and that same heading
                    var match = poses.FirstOrDefault(p =>
                        (p.WorldPose.Translation - reached.Translation).Length <= 0.5
                        && Math.Abs(StraightLinePlanner.Wrap(YawTowardObject(obj.Pose, p.WorldPose) - reached.AngleAroundZ)) <= 1e-3);
                    Chosen = match ?? closest;
                }
            }
        }
        if (!_m.Docking.Carrying.IsCarrying(ObjectId))
        {
            bool turned = await _m.TurnTowardsObjectAsync(ObjectId, Math.PI, cancel);
            if (!turned)
            {
                // The final orientation is part of this action: the engine runs the TurnTowardsObjectAction as
                // the second half of a compound action, and a compound action fails when a part of it fails.
                // Reporting Success here let DockHelper dock from an orientation the robot never reached.
                // The engine's own result for that turn was not read; DidNotReachPreActionPose is the nearest
                // shipped result and keeps the outcome retryable (reduction labelled).
                _trace.Add("TurnTowardsObjectAction did not complete: the robot is not facing the object");
                return ActionResult.DidNotReachPreActionPose;
            }
            _trace.Add("turned towards the object");
        }
        return ActionResult.Success;
    }

    /// <summary>
    /// The ActionType-6 branch of <c>InitHelper</c> (M12-022; pre-extraction rows 11.8, 11.11, 11.13, verified): a strictly NEGATIVE <c>+0x84</c> is
    /// <c>NO_DISTANCE_SET</c> 0x0300000D (<c>vcmpe s0,#0</c> at 0x00558FE2, <c>bpl</c> at 0x00558FEA is taken for NaN, +0 and -0, so they do not error;
    /// error block 0x00558FEC..0x0055903E); a robot already closer to the object than <c>+0x84</c> needs no drive; otherwise a <c>DriveToPoseAction</c> to the
    /// object+delta goal (the object pose is its <c>+0xB4</c> with <c>+0xC0 = 1</c>, thresholds (10,10,10) and Radians 0.174533 through <c>SetGoals</c>) is
    /// run. A NaN <c>+0x84</c> flows into the goal as NaN (0x005591E4) and InitHelper returns no result for it; what the drive and the planner (M13) do with a
    /// NaN goal is not in the inventory, so that case throws <see cref="NotSupportedException"/> instead of sending anything.
    /// </summary>
    private async Task<ActionResult> DriveToObjectDeltaGoalAsync(ObservableObject obj, Pose3d robot, CancellationToken cancel)
    {
        float distance = DistanceFromObjectOrigin;
        if (distance < 0)
        {
            _trace.Add("DriveToObjectAction.InitHelper.NoDistanceSet: ActionType==NONE but no distance set either");
            return ActionResult.NoDistanceSet;
        }
        if (!TryBuildObjectDeltaGoal(obj.Pose, robot, distance, out var goal))
        {
            _trace.Add("DriveToObjectAction.InitHelper: already within the distance of the object; no drive");
            return ActionResult.Success;
        }
        if (double.IsNaN(goal.Translation.X) || double.IsNaN(goal.Translation.Y) || double.IsNaN(goal.AngleAroundZ))
            throw new NotSupportedException("MISSING: DriveToObjectAction.InitHelper with a NaN +0x84 builds a NaN goal (0x005591E4) and returns no result; what DriveToPoseAction / PathComponent (M13) do with a NaN goal is not in the inventory.");
        var drive = new DriveToPoseAction(_m)
        {
            Goal = goal, Goals = new[] { goal }, IgnoreObstacleIds = new[] { ObjectId }, PreActionObjectPose = obj.Pose,
            DistanceToleranceMm = 10.0, AngleToleranceRad = 0.174533, Profile = Profile,
        };
        var r = await drive.RunAsync(cancel);
        _trace.AddRange(drive.Trace);
        return r;
    }

    /// <summary>
    /// The goal <c>DriveToObjectAction::InitHelper</c> 0x00558FB4 builds for ActionType 6 (M12-022). <c>delta = robot.xy -
    /// objectInRobotParent.xy</c> (0x005590C8..0x005590FE); if its squared length is positive it is normalised and that length is
    /// <c>d</c>, otherwise <c>d = 0</c> and delta is left as it is (0x00559108..0x00559160). <c>d &lt; +0x84</c> means already in
    /// position: no goal (returns false; 0x005591C2..0x005591D6; the compare is <c>bpl</c>-style, so a NaN <c>+0x84</c> is NOT already in position). Otherwise <c>delta *= +0x84</c> (0x005591D8..0x005591EC), the
    /// goal is <c>{obj.x + delta.x, obj.y + delta.y, robot.z}</c> (0x00559220..0x00559230) and its yaw is
    /// <c>atan2f(-delta.y, -delta.x)</c> (0x00559232..0x0055923A) about Z (0x00559278).
    /// </summary>
    // fidelity: M12-022
    public static bool TryBuildObjectDeltaGoal(Pose3d objectPose, Pose3d robotPose, float distance, out Pose3d goal)
    {
        double dx = robotPose.Translation.X - objectPose.Translation.X;
        double dy = robotPose.Translation.Y - objectPose.Translation.Y;
        double n2 = dx * dx + dy * dy, d;
        if (n2 > 0) { d = Math.Sqrt(n2); dx /= d; dy /= d; } else d = 0.0;
        if (d < distance) { goal = default; return false; }
        dx *= distance; dy *= distance;
        double yaw = Math.Atan2(-dy, -dx);
        goal = new Pose3d(yaw, new Vec3(0, 0, 1), new Vec3(objectPose.Translation.X + dx, objectPose.Translation.Y + dy, robotPose.Translation.Z));
        return true;
    }

    /// <summary>
    /// <c>DriveToObjectAction::CheckIfDone</c> 0x00559880 after the compound has finished (M12-011): <c>[+0x148] == 0</c> returns
    /// 0 (0x0055989C); an object that is not located is <c>BadObject</c> 0x03000004 (0x005598B6/0x005598BA). ActionType 6
    /// compares the robot-to-object-origin XY distance squared against <c>+0x84</c> squared and fails with 0x04000001 when it is
    /// larger (0x00559998..0x005599FE). Every other ActionType calls the <c>+0x150</c> function with the in-position flag
    /// (0x00559A9C): the vector starts empty and the flag 0; flag 0 gives 0x04000001 (0x00559AE4), otherwise the function's result (0x00559AE6).
    /// This function does not call <c>ComputePreActionPoseDistThreshold</c> (M12-020).
    /// </summary>
    // fidelity: M12-011
    public ActionResult CheckIfDone()
    {
        if (!CheckAtEnd) { _trace.Add("DriveToObjectAction.CheckIfDone: not checking at the end"); return ActionResult.Success; }
        var obj = _m.World.GetLocatedObjectById(ObjectId);
        if (obj is null) return ActionResult.BadObject;
        if (ActionType == PreActionType.None)
        {
            var robot = _m.RobotPose();
            if (robot is null) return ActionResult.BadObject;
            double dx = robot.Value.Translation.X - obj.Pose.Translation.X, dy = robot.Value.Translation.Y - obj.Pose.Translation.Y;
            double dist = DistanceFromObjectOrigin;
            if (dx * dx + dy * dy > dist * dist)
            {
                _trace.Add("DriveToObjectAction.CheckIfDone: farther from the object than the distance");
                return ActionResult.DidNotReachPreActionPose;
            }
            return ActionResult.Success;
        }
        // 0x00559A86..0x00559AE6: the function gets a FRESH EMPTY pose vector and an in-position bool of 0 (movs r0,#0; strd/str/strb to the locals; call 0x00559A9C); flag zero returns 0x04000001
        // whatever the function returned (0x00559AE4), non-zero returns the function's result. The flip installers never write the flag, so they always end 0x04000001 here (M12-035).
        bool inPosition = false;
        var result = PosesFunction(obj, out _, ref inPosition);
        if (!inPosition)
        {
            _trace.Add("DriveToObjectAction.CheckIfDone: not in position");
            return ActionResult.DidNotReachPreActionPose;
        }
        return result;
    }

    /// <summary>
    /// LABELLED REDUCTION for the non-6 goal (M12-022): the heading from <paramref name="pose"/>'s position toward <paramref name="objectPose"/>. The engine's
    /// ActionType-6 goal uses <c>atan2f(-delta.y, -delta.x)</c> with <c>delta = normalize(robot.xy - object.xy) * +0x84</c> (0x00559232/0x00559236/0x0055923A);
    /// for other ActionTypes the goals are the poses the <c>+0x150</c> function returns (see the reduction in <see cref="InitHelperAsync"/>).
    /// </summary>
    internal static double YawTowardObject(Pose3d objectPose, Pose3d pose) =>
        Math.Atan2(objectPose.Translation.Y - pose.Translation.Y, objectPose.Translation.X - pose.Translation.X);

    /// <summary>
    /// <c>Pose3d::IsSameAs</c> as <c>RemoveMatchingPredockPose</c> calls it (Point3(100,100,100), Radians 0x3F060A92): the receiver is <paramref name="a"/>, the
    /// vector element (M12-029). <see cref="DockHelper"/> uses it to recognise a pose it already tried.
    /// </summary>
    internal static bool IsSamePredockPose(Pose3d a, Pose3d b) =>
        a.IsSameAs(b, DockPreActionPoses.MatchToleranceMm, DockPreActionPoses.MatchAngleRad, out _);
}

/// <summary>
/// <c>DriveToPlaceCarriedObjectAction</c> (M12-030; re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q13, verified in
/// 20260929-R-VIS-verify-M12-gap2.md): drives to where a carried object is to be put down.
///
/// <para>Constructor 0x00559CD4: base <c>DriveToObjectAction(robot, carried id, &amp;type, 0.0f, false, 0.0f, useManualSpeed)</c> with ActionType 2 when the first bool
/// is non-zero and 1 otherwise (0x00559CDE..0x00559CE8; production is always 2, M12-022), <c>+0x16C</c> the pose, <c>+0x178</c>, <c>+0x179</c> and <c>+0x17C</c> the
/// remaining arguments. The PlaceObjectOnGround wire fields are 0x44 level, 0x45 useManualSpeed, 0x46 useExactRotation, 0x47 checkDestinationFree; those names are
/// INFERRED from the CLAD field order (research 13.1), so the C# names its arguments by engine offset: <c>+0x178</c> (<c>useExactRotation</c> by inference) and
/// <c>+0x179</c> (<c>checkDestinationFree</c> by inference). The carried id at construction is <c>[robot+0x284]+8</c>, or the invalid id when nothing is carried.</para>
///
/// <para><c>Init</c> 0x00559DB8: (1) no carried object is 0x03000011; (2) <c>[+0x7C]</c> becomes the carried id and the object is looked up by the constructor's id
/// (<c>+0x78</c>): missing is 0x03000004; (3) with <c>+0x178</c> the approach angle comes from <c>ComputePlacementApproachAngle</c> 0x005504A0: only its angle helper
/// 0x00550858 is in the inventory (<see cref="PlacementApproachAngle"/>), not the function's own body, so that case throws <see cref="NotSupportedException"/>;
/// (4) a clone of the object is put at the placement pose (<c>InitPose</c>, Known) and <c>DriveToObjectAction::InitHelper(clone)</c> runs; <c>DriveToObjectAction::Init</c> is
/// NOT called (so no cube light). <c>ObservableObject::Clone</c> (ActiveCube thunk 0x004E3954 -&gt; 0x004E38D4) is <c>new ActiveCube(ObjectType)</c> and keeps NOTHING: the clone has
/// ObjectID -1 (here <see cref="uint.MaxValue"/>), ActiveID -1 and FactoryID 0 (0x0087660E, 0x004E0C4C..0x004E0C58), so the inner <c>GetPreActionPoses</c> does not see a carried
/// object (0x00550904 compares -1 with the carried id) and answers with the poses of the clone at the placement pose.</para>
///
/// <para><c>CheckIfDone</c> 0x00559FA4: the compound's result, replaced by 0x04000008 (PLACEMENT_GOAL_NOT_FREE) when <c>+0x179</c> is set and
/// <see cref="IsPlacementGoalFree"/> is false, on every tick regardless of state. The async model has no ticks: <see cref="RunAsync"/> polls it every
/// <see cref="TickPeriod"/> while the drive runs (a C# stand-in for the engine's per-tick call; the period is not in the inventory) and once more when it ends.
/// <c>DriveToObjectAction::CheckIfDone</c> is not called, so there is no in-position check afterwards.</para>
/// </summary>
// fidelity: M12-030
public sealed class DriveToPlaceCarriedObjectAction
{
    /// <summary>The stand-in for the engine's per-tick call of <c>CheckIfDone</c> while the drive runs (not in the inventory).</summary>
    public static readonly TimeSpan TickPeriod = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// <c>BlockWorld::FindLocatedIntersectingObjects(quad, out, padding, filter)</c> as <c>IsPlacementGoalFree</c> uses it (0x0055A070): true iff the result is empty. The default is
    /// <see cref="EngineFindLocatedIntersectingObjectsIsEmpty"/>; a test can supply another as an input.
    /// </summary>
    public delegate bool PlacementGoalQuery(ObservableObject carried, Pose3d placementPose, double padding);

    private readonly ManipulationSystem _m;
    private readonly DriveToObjectAction _inner;
    private ObservableObject? _clone;

    public DriveToPlaceCarriedObjectAction(ManipulationSystem m, Pose3d placementPose, bool placeOnGroundType, bool flagAt0x178,
                                           bool useManualSpeed, bool flagAt0x179, double paddingAt0x17C)
    {
        _m = m;
        CarriedIdAtConstruction = m.Docking.Carrying.CarriedObjectId ?? uint.MaxValue;
        ActionType = placeOnGroundType ? PreActionType.PlaceOnGround : PreActionType.PlaceRelative;
        PlacementPose = placementPose; FlagAt0x178 = flagAt0x178; FlagAt0x179 = flagAt0x179; PaddingAt0x17C = paddingAt0x17C;
        _inner = new DriveToObjectAction(m, CarriedIdAtConstruction, ActionType);
        FindLocatedIntersectingObjectsIsEmpty = EngineFindLocatedIntersectingObjectsIsEmpty;
    }

    /// <summary>
    /// <c>BlockWorld::FindLocatedIntersectingObjects</c> 0x00626870 with the default filter <c>IsPlacementGoalFree</c> passes (M12-030): the located objects of the current origin
    /// (no family, type or PoseState restriction, 0x0061EBF2..0x0061EE8A) except the ignored id (the carried object's); the reference quad is
    /// <c>carried-&gt;vtbl[0x50](carried, placementPose, 0.0)</c> and the predicate 0x0062750E is <c>q = o-&gt;vtbl[0x50](o, o-&gt;GetPose(), padding); q.Intersects(*ref)</c>, so the
    /// padding inflates the CANDIDATE's quad, and there is NO Z test. Returns true iff nothing intersects (the engine's <c>out.empty()</c>). Footprints are M13-007's
    /// (<see cref="Footprint"/>); a pair for which one of them cannot be computed (a non-cube, or a cube whose pose is not yaw-only) is decided by M13-023's LABELLED STAND-IN,
    /// <see cref="BlockConfigurationManager.PlanarStandIn"/> with its height test neutralised (this query has none): centres within half a cube in x, y. That differs from the engine.
    /// </summary>
    // fidelity: M12-030, M13-007, M13-023
    public bool EngineFindLocatedIntersectingObjectsIsEmpty(ObservableObject carried, Pose3d placementPose, double padding) =>
        StaticFindLocatedIntersectingObjectsIsEmpty(_m, carried, placementPose, padding, _trace);

    /// <summary>
    /// The same query as a static: <c>FindFreePoseInBeacon</c>'s obstacle test (0x005E0BEA..0x005E0C32, M15-019) is the same <c>vtable[0x50](carried, pose, 0.0f)</c> quad, a default filter ignoring the
    /// carried id, and <c>FindLocatedIntersectingObjects(quad, vec, 10.0f, filter)</c>, empty or not.
    /// </summary>
    // fidelity: M12-030, M15-019
    public static bool StaticFindLocatedIntersectingObjectsIsEmpty(ManipulationSystem m, ObservableObject carried, Pose3d placementPose, double padding, List<string>? trace)
    {
        bool refOk = Footprint.TryGetBoundingQuadXY(carried, placementPose, 0f, out var refQuad);
        var atPlacement = new ObservableObject(carried.ObjectId, carried.Type, carried.Markers) { Pose = placementPose, PoseState = PoseState.Known };
        foreach (var o in m.World.LocatedObjects)
        {
            if (o.ObjectId == carried.ObjectId) continue;                       // ignoreIDs {carried id}
            bool hit;
            if (refOk && Footprint.TryGetBoundingQuadXY(o, o.Pose, (float)padding, out var candQuad)) hit = candQuad!.Intersects(refQuad!);
            else
            {
                hit = BlockConfigurationManager.PlanarStandIn(atPlacement, o, onTop: true, double.PositiveInfinity);
                trace?.Add($"IsPlacementGoalFree: object {o.ObjectId} decided by the M13-023 planar stand-in ({(hit ? "intersects" : "clear")})");
            }
            if (hit) return false;
        }
        return true;
    }

    /// <summary><c>+0x78</c>: the carried object's id when the action was built.</summary>
    public uint CarriedIdAtConstruction { get; }
    /// <summary>The base constructor's ActionType: 2 (<see cref="PreActionType.PlaceOnGround"/>) when the first bool was set, else 1.</summary>
    public PreActionType ActionType { get; }
    /// <summary><c>+0x16C</c>.</summary>
    public Pose3d PlacementPose { get; }
    /// <summary><c>+0x178</c>: when set, <c>Init</c> asks <c>ComputePlacementApproachAngle</c> (a visible stub, M12-034).</summary>
    public bool FlagAt0x178 { get; }
    /// <summary><c>+0x179</c>: when set, <c>CheckIfDone</c> fails with 0x04000008 while the placement goal is not free.</summary>
    public bool FlagAt0x179 { get; }
    /// <summary><c>+0x17C</c>: the padding <c>IsPlacementGoalFree</c> passes.</summary>
    public double PaddingAt0x17C { get; }
    /// <summary>The query behind <see cref="IsPlacementGoalFree"/>; the default is the visible stub.</summary>
    public PlacementGoalQuery FindLocatedIntersectingObjectsIsEmpty { get; set; }
    /// <summary>The clone <c>Init</c> step 4 made of the carried object (<c>InitPose(+0x16C, Known)</c>), once <see cref="RunAsync"/> has got that far.</summary>
    public ObservableObject? Clone => _clone;
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    /// <summary><c>IsPlacementGoalFree</c> 0x0055A070: the carried object (missing: true), a default filter ignoring its own id, its quad at the placement pose with the padding; true iff nothing intersects.</summary>
    public bool IsPlacementGoalFree()
    {
        if (_m.Docking.Carrying.CarriedObjectId is not { } id || _m.World.GetLocatedObjectById(id) is not { } carried) return true;
        return FindLocatedIntersectingObjectsIsEmpty(carried, PlacementPose, PaddingAt0x17C);
    }

    /// <summary><c>CheckIfDone</c> 0x00559FA4 over the compound's result <paramref name="compoundResult"/>.</summary>
    public ActionResult CheckIfDone(ActionResult compoundResult)
    {
        if (FlagAt0x179 && !IsPlacementGoalFree())
        {
            _trace.Add("Placement goal is not free to drop the cube, failing with retry.");
            return ActionResult.PlacementGoalNotFree;
        }
        return compoundResult;
    }

    /// <summary><c>Init</c> 0x00559DB8 up to and including the start of the inner <c>InitHelper</c>; returns the first non-zero <c>Init</c> result or the inner compound's result.</summary>
    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        if (_m.Docking.Carrying.CarriedObjectId is null)
        {
            _trace.Add("DriveToPlaceCarriedObjectAction.CheckPreconditions.NotCarryingObject");
            return ActionResult.NotCarryingObjectAbort;
        }
        if (_m.World.GetLocatedObjectById(CarriedIdAtConstruction) is not { } obj)
        {
            _trace.Add("DriveToPlaceCarriedObjectAction.CheckPreconditions.NoObjectWithID");
            return ActionResult.BadObject;
        }
        if (FlagAt0x178)
            throw new NotSupportedException("M12-039 RECOVERABLE_GAP: ComputePlacementApproachAngle 0x005504A0 (Init step 3) is readable but unread; only its angle helper 0x00550858 (PlacementApproachAngle) is built, so the +0x178 case is a visible stub.");
        // ObservableObject::Clone keeps nothing: ObjectID -1 (uint.MaxValue here), a fresh marker set, no pose; then InitPose(+0x16C, Known).
        _clone = new ObservableObject(uint.MaxValue, obj.Type, CubeGeometry.MarkersFor(obj.Type)) { Pose = PlacementPose, PoseState = PoseState.Known };
        // [+0x7C] := the carried id is not modelled separately: the inner action turns towards (and tests carrying for) CarriedIdAtConstruction, which is the
        // same id unless a different object was picked up after this action was built.
        if (!FlagAt0x179) return CheckIfDone(await _inner.InitHelperAsync(_clone, cancel));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        var drive = _inner.InitHelperAsync(_clone, linked.Token);
        while (!drive.IsCompleted)
        {
            await Task.WhenAny(drive, _m.Wait(TickPeriod, CancellationToken.None));
            if (!drive.IsCompleted && CheckIfDone(ActionResult.Running) == ActionResult.PlacementGoalNotFree)
            {
                linked.Cancel();
                try { await drive; } catch (OperationCanceledException) { }
                return ActionResult.PlacementGoalNotFree;
            }
        }
        return CheckIfDone(await drive);
    }
}

/// <summary>
/// The angle helper of <c>ComputePlacementApproachAngle</c> 0x00550858..0x005508C4 (M12-030): <c>axis = RotationMatrix3d::GetRotatedParentAxis&lt;'Z'&gt;(R)</c> (+-1 X, +-2 Y, +-3 Z,
/// <see cref="RotatedParentAxis"/>), then <c>tbb</c> on <c>axis + 3</c> (table 04 0A 10 1A 1A 1F 24 at 0x00550872; an index above 6 takes the default arm): -3 gives
/// <c>-GetAngleAroundZaxis</c>, -2 <c>-GetAngleAroundYaxis</c>, -1 <c>-GetAngleAroundXaxis</c>, 0 and 1 <c>+GetAngleAroundXaxis</c>, 2 <c>+GetAngleAroundYaxis</c>, 3
/// <c>+GetAngleAroundZaxis</c>, any other value <c>+GetAngleAroundXaxis</c>. <c>GetAngleAroundZaxis</c> is M12-033's (<see cref="EngineRadians.GetAngleAroundZaxis"/>); the bodies
/// of <c>Rotation3d::GetAngleAroundXaxis</c> and <c>GetAngleAroundYaxis</c> are readable but unread (M12-039 RECOVERABLE_GAP, not built), so those arms call the two functions given, which throw by default.
/// </summary>
// fidelity: M12-030, M12-039
public static class PlacementApproachAngle
{
    public static double Helper(Mat3 r, Func<Mat3, double>? angleAroundX = null, Func<Mat3, double>? angleAroundY = null)
    {
        angleAroundX ??= _ => throw new NotSupportedException("M12-039 RECOVERABLE_GAP: Rotation3d::GetAngleAroundXaxis (PLT 0x4AB914) is readable but unread (helper 0x00550858 arms -1, 0, 1 and the default).");
        angleAroundY ??= _ => throw new NotSupportedException("M12-039 RECOVERABLE_GAP: Rotation3d::GetAngleAroundYaxis (PLT 0x4AB908) is readable but unread (helper 0x00550858 arms -2 and 2).");
        int axis = RotatedParentAxis.Get(r, 2);
        return axis switch
        {
            -3 => -EngineRadians.GetAngleAroundZaxis(r),
            -2 => -angleAroundY(r),
            -1 => -angleAroundX(r),
            2 => angleAroundY(r),
            3 => EngineRadians.GetAngleAroundZaxis(r),
            _ => angleAroundX(r),                                            // 0, 1 and every other value
        };
    }
}

/// <summary>
/// The engine's <c>DriveStraightAction(robot, distance_mm, speed_mmps, shouldPlayAnimation)</c>: one line
/// segment along the robot's heading, executed as a path. A negative distance drives backwards.
/// </summary>
public sealed class DriveStraightAction
{
    private readonly ManipulationSystem _m;
    public DriveStraightAction(ManipulationSystem m, double distanceMm, float speedMmps = 100f) { _m = m; DistanceMm = (float)distanceMm; SpeedMmps = speedMmps; }   // the engine's distance_mm is a float
    public double DistanceMm { get; }
    public float SpeedMmps { get; }

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var robot = _m.RobotPose();
        if (robot is null) return ActionResult.Abort;
        double h = robot.Value.AngleAroundZ;
        var to = robot.Value.Translation + new Vec3(Math.Cos(h) * DistanceMm, Math.Sin(h) * DistanceMm, 0);
        var p = PathMotionProfile.Default;
        float speed = DistanceMm < 0 ? -Math.Abs(SpeedMmps) : Math.Abs(SpeedMmps);
        using var run = _m.StartPath(new PathSegment[] { new PathSegment.Line(robot.Value.Translation.X, robot.Value.Translation.Y, to.X, to.Y, speed, p.AccelMmps2, p.DecelMmps2) });
        // WaitAsync clears the path when it ends without a terminal event, so a behaviour interrupted mid-drive
        // does not leave the firmware following this line.
        var ev = await run.WaitAsync(TimeSpan.FromSeconds(3 + Math.Abs(DistanceMm) / Math.Max(10, Math.Abs(SpeedMmps)) * 2), cancel);
        return ev == PathEventType.Completed ? ActionResult.Success
             : ev is null ? (cancel.IsCancellationRequested ? ActionResult.CancelledWhileRunning : ActionResult.Timeout)
             : ActionResult.FailedTraversingPath;
    }
}
