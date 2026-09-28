using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary><c>Anki::Cozmo::ActionResult</c> (UNITY values) for the outcomes this stack produces.</summary>
public enum ActionResult : uint
{
    Success = 0, Running = 0x01000000, CancelledWhileRunning = 0x02000000, Abort = 0x03000000,
    BadObject = 0x03000004, BadPose = 0x03000005, NoPreActionPoses = 0x03000010, NotCarryingObjectAbort = 0x03000011,
    PathPlanningFailedAbort = 0x03000013, StillCarryingObject = 0x03000017, Timeout = 0x03000018, VisualObservationFailed = 0x0300001D,
    Retry = 0x04000000, DidNotReachPreActionPose = 0x04000001, FailedTraversingPath = 0x04000002,
}

/// <summary>
/// The engine's <c>DriveToPoseAction</c> (0x0055A238..0x0055B1E8): the head goes to the path-following angle
/// −15 degrees (0xBE860A92, <c>MovementComponent::MoveHeadToAngle</c> in Init), a path to the goal is planned
/// and executed, and the action succeeds when the robot's pose is within the goal tolerance (angle 0.174533 rad,
/// 0x3E32B8C2, from the constructor; distance from <see cref="CubePreActionPoses.DistanceThresholdMm"/> when a
/// pre-action pose is the goal) once the path has been traversed. INFERRED: the planning timeout (the engine
/// warns "Robot has been planning for more than %f seconds"; the value was not read, 5 s here) and the
/// traversal timeout (LOCAL, from the path length). The planner itself is LOCAL (<see cref="StraightLinePlanner"/>).
/// </summary>
public sealed class DriveToPoseAction
{
    public const double PathFollowingHeadAngleRad = -0.261799;
    public const double GoalAngleToleranceRad = 0.174533;
    public const double DefaultGoalDistanceToleranceMm = 10.0;

    private readonly ManipulationSystem _m;

    public DriveToPoseAction(ManipulationSystem m) => _m = m;

    public Pose3d? Goal { get; set; }
    /// <summary>Alternative goals for the lattice planner (a cube's pre-action poses); the reached one becomes <see cref="Goal"/>.</summary>
    public IReadOnlyList<Pose3d>? Goals { get; set; }
    /// <summary>Objects not to treat as obstacles (the one being docked with).</summary>
    public IEnumerable<uint>? IgnoreObstacleIds { get; set; }
    public double DistanceToleranceMm { get; set; } = DefaultGoalDistanceToleranceMm;
    /// <summary>
    /// The object pose the goal's pre-action pose belongs to. When set, <c>CheckIfDone</c> takes its distance
    /// tolerance from <see cref="CubePreActionPoses.DistanceThresholdMm"/> (the engine's
    /// <c>DriveToPoseAction::CheckIfDone</c> 0x0055AB4C calls it at 0x0055ACF0, M12-020), not from the plain
    /// <see cref="DistanceToleranceMm"/>.
    /// </summary>
    public Pose3d? PreActionObjectPose { get; set; }
    /// <summary>The angle tolerance fed to the pre-action distance threshold (the constructor's 0.1309 rad).</summary>
    public double PreActionAngleToleranceRad { get; set; } = DriveToObjectAction.PreActionAngleToleranceRad;
    public PathMotionProfile Profile { get; set; } = PathMotionProfile.Default;
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        if (Goal is not { } goal) { _trace.Add("DriveToPoseAction.Init.NoGoalSet"); return ActionResult.BadPose; }
        var start = _m.RobotPose();
        if (start is null) { _trace.Add("no robot state"); return ActionResult.Abort; }
        _ = _m.Robot.Motion.SetHeadAngleAsync((float)PathFollowingHeadAngleRad, CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2, requireCalibration: false);
        IReadOnlyList<PathSegment> path;
        if (_m.Planner is { } planner)
        {
            // LatticePlannerImpl::StartPlanning: import the world's obstacles, plan, and turn the plan into segments
            planner.Env.ImportBlockWorldObstacles(_m.World, _m.Docking.Carrying.CarriedObjectId, IgnoreObstacleIds);
            var goals = Goals is { Count: > 0 } ? Goals : new[] { goal };
            var planned = planner.PlanTo(start.Value, goals, Profile);
            if (planned is { } pl)
            {
                path = pl.Path; goal = pl.Goal;
                _trace.Add($"lattice plan: {pl.Plan.Actions.Count} primitive(s), cost {pl.Plan.Cost:F0}, {pl.Plan.Expansions} expansions, {planner.Env.ObstacleCount} obstacle(s)");
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
        // fidelity: M12-020
        // DriveToPoseAction::CheckIfDone 0x0055AB4C calls ComputePreActionPoseDistThreshold at 0x0055ACF0
        // and reads BOTH outputs (0x0055ACF4 ldrd) as the x/y of the Point3 position tolerance of
        // Pose3d::IsSameAs(robotPose, preActionPose, Point3{out0, out1, Robot::GetHeight}, this+0x9c).
        double tx = DistanceToleranceMm, ty = DistanceToleranceMm;
        if (PreActionObjectPose is { } objectPose
            && CubePreActionPoses.DistanceThresholdMm(objectPose, goal, PreActionAngleToleranceRad, out double out0, out double out1))
        {
            tx = out0;
            ty = out1;
        }
        var tol = new Vec3(tx, ty, RobotHeightMm());
        // E9: Pose3d::IsSameAs 0x00846EA4 uses a default-constructed empty RotationAmbiguities and compares
        // the full relative rotation (GetAngleDiffFrom 0x0084A694), so the full goal pose is passed.
        if (now.Value.IsSameAs(goal, tol, GoalAngleToleranceRad, out _))
        {
            _trace.Add($"DriveToPoseAction.CheckIfDone.Success: threshold=({tx:F1},{ty:F1})");
            Goal = goal;
            return ActionResult.Success;
        }
        _trace.Add($"DriveToPoseAction.CheckIfDone.DoneNotInPlace: not within threshold=({tx:F1},{ty:F1})");
        return ActionResult.DidNotReachPreActionPose;
    }

    /// <summary>
    /// <c>Robot::GetHeight</c> 0x00516F0C = <c>max(66·sin(liftAngle) + 45 + 5, 67.7)</c> (66 at 0x00516F54,
    /// 45 at 0x00516F58, 5.0, floor 0x42876666 = 67.7; C-E7/E9). It is the z of the <c>Point3</c> tolerance
    /// <c>DriveToPoseAction::CheckIfDone</c> passes to <c>IsSameAs</c> (0x0055ACBA, 0x0055ACC2/0x0055ACF8).
    /// </summary>
    private double RobotHeightMm()
    {
        float lift = _m.Robot.State.Latest?.LiftAngle ?? 0f;
        return Math.Max((double)RobotState.LiftHeightMmFromAngle(lift) + 5.0, 67.7);
    }

}

/// <summary>
/// The engine's <c>DriveToObjectAction</c> (0x00558524..0x00559A00): <c>Init</c> looks the object up in the
/// world ("block world does not have an ActionableObject with ID"), <c>InitHelper</c> takes the object's
/// pre-action poses for the action type ("did not return any pre-action poses"), picks the closest
/// (<c>GetClosestPreDockPose</c>) or, when the robot is already within the distance threshold of one, keeps the
/// current pose ("Robot's current pose is close enough to preAction pose"), then runs a <c>DriveToPoseAction</c>
/// (angle tolerance 0.174533) followed by a <c>TurnTowardsObjectAction</c> unless the robot is carrying the
/// object. The pre-action angle tolerance is the constructor's 0.1309 rad (7.5 degrees, 0x3E060A92).
/// </summary>
public sealed class DriveToObjectAction
{
    // fidelity: M12-011
    public const double PreActionAngleToleranceRad = 0.1309;

    private readonly ManipulationSystem _m;

    public DriveToObjectAction(ManipulationSystem m, uint objectId, PreActionType type)
    {
        _m = m; ObjectId = objectId; Type = type;
    }

    public uint ObjectId { get; }
    public PreActionType Type { get; }
    public PathMotionProfile Profile { get; set; } = PathMotionProfile.Default;
    public PreActionPose? Chosen { get; private set; }
    /// <summary>
    /// Pre-action poses a previous attempt already failed from, excluded here the way
    /// <c>IBehavior::UseSecondClosestPreActionPose</c> (0x005BEE56) does it: it re-reads the possible poses and
    /// calls <c>IDockAction::RemoveMatchingPredockPose</c> (0x00551418), which drops the entry that
    /// <c>Pose3d::IsSameAs</c> matches within 100 mm on each axis and 0.523599 rad, but only while more than
    /// one pose remains.
    /// </summary>
    public IReadOnlyList<Pose3d> ExcludePoses { get; set; } = Array.Empty<Pose3d>();
    /// <summary>NATIVE, <c>RemoveMatchingPredockPose</c> 0x00551438: the axis tolerance of the match.</summary>
    public const double SamePoseDistanceMm = 100.0;
    /// <summary>NATIVE, <c>RemoveMatchingPredockPose</c> 0x0055143C: the angle tolerance of the match.</summary>
    public const double SamePoseAngleRad = 0.523599;
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var obj = _m.World.GetLocatedObjectById(ObjectId);
        if (obj is null) { _trace.Add($"DriveToObjectAction.CheckPreconditions.NoObjectWithID {ObjectId}"); return ActionResult.BadObject; }
        var robot = _m.RobotPose();
        if (robot is null) return ActionResult.Abort;
        var poses = CubePreActionPoses.For(obj, Type, robot.Value);
        if (poses.Count == 0) { _trace.Add($"DriveToObjectAction.CheckPreconditions.NoPreActionPoses for {Type}"); return ActionResult.NoPreActionPoses; }
        foreach (var used in ExcludePoses)
        {
            if (poses.Count <= 1) break;                       // the engine only removes while more than one remains
            var match = poses.FirstOrDefault(p => IsSamePredockPose(p.WorldPose, used));
            if (match is null) continue;
            poses = poses.Where(p => !ReferenceEquals(p, match)).ToList();
            _trace.Add("Trying again with a different predock pose");
        }
        var closest = CubePreActionPoses.Closest(poses, robot.Value)!;
        Chosen = closest;
        // The engine's DriveToObjectAction::InitHelper keeps the current pose when the robot is already
        // within the closest pose's threshold pair (IDockAction::GetPreActionPoses, reached through
        // GetPossiblePoses/GetClosestPreDockPose); it is not a fixed box.
        bool close = DockActionBase.IsCloseEnoughToPreActionPose(obj, poses, robot.Value, PreActionAngleToleranceRad);
        if (close) _trace.Add("DriveToObjectAction.GetPossiblePoses.UseRobotPose: within the pre-action pose threshold");
        else
        {
            // fidelity: M12-022
            // DriveToObjectAction::InitHelper 0x00558FB4 builds a yaw-only DriveToPoseAction goal. The goal
            // position is the chosen pre-action pose's position; the yaw is atan2f(-delta.y, -delta.x) with
            // delta = normalize(robot.xy - objectInRobotParent.xy) * DriveToObjectAction+0x84 (0x005591E6
            // vmul; goal = object.xy + delta at 0x00559220/0x00559224; yaw at 0x00559232/0x00559236/
            // 0x0055923A). -delta points from the goal toward the object, so the yaw is the heading from
            // the pose position toward the object pose. DriveToPoseAction itself does not flatten:
            // CheckIfDone passes the full (yaw-only) goal to Pose3d::IsSameAs.
            var goal = new Pose3d(Mat3.AboutZ(YawTowardObject(obj.Pose, closest.WorldPose)), closest.WorldPose.Translation);
            var goals = new[] { goal };
            var drive = new DriveToPoseAction(_m) { Goal = goal, Goals = goals, IgnoreObstacleIds = new[] { ObjectId },
                                                     PreActionObjectPose = obj.Pose, Profile = Profile };
            var r = await drive.RunAsync(cancel);
            _trace.AddRange(drive.Trace);
            if (r != ActionResult.Success) return r;
            if (drive.Goal is { } reached)
            {
                // the goal is yaw-only with the heading toward the object, so match the reached goal to a
                // pre-action pose by position and that same heading; keep the closest when nothing matches
                // (never null).
                var match = poses.FirstOrDefault(p =>
                    (p.WorldPose.Translation - reached.Translation).Length <= 0.5
                    && Math.Abs(StraightLinePlanner.Wrap(YawTowardObject(obj.Pose, p.WorldPose) - reached.AngleAroundZ)) <= 1e-3);
                Chosen = match ?? closest;
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
        // the object must still be located where we expect it
        return _m.World.GetLocatedObjectById(ObjectId) is null ? ActionResult.BadObject : ActionResult.Success;
    }

    /// <summary><c>IDockAction::RemoveMatchingPredockPose</c>'s <c>Pose3d::IsSameAs(pose, (100,100,100), 0.523599)</c>.</summary>
    internal static bool IsSamePredockPose(Pose3d a, Pose3d b)
    {
        var d = a.Translation - b.Translation;
        return Math.Abs(d.X) <= SamePoseDistanceMm && Math.Abs(d.Y) <= SamePoseDistanceMm && Math.Abs(d.Z) <= SamePoseDistanceMm
               && Math.Abs(StraightLinePlanner.Wrap(a.AngleAroundZ - b.AngleAroundZ)) <= SamePoseAngleRad;
    }

    /// <summary>
    /// <c>DriveToObjectAction::InitHelper</c> 0x00558FB4's yaw-only goal heading: the heading from
    /// <paramref name="pose"/>'s position toward <paramref name="objectPose"/>, i.e. the engine's
    /// <c>atan2f(-delta.y, -delta.x)</c> with <c>delta = normalize(robot.xy - object.xy) *
    /// DriveToObjectAction+0x84</c> (0x00559232/0x00559236/0x0055923A).
    /// </summary>
    internal static double YawTowardObject(Pose3d objectPose, Pose3d pose) =>
        Math.Atan2(objectPose.Translation.Y - pose.Translation.Y, objectPose.Translation.X - pose.Translation.X);
}

/// <summary>
/// The engine's <c>DriveStraightAction(robot, distance_mm, speed_mmps, shouldPlayAnimation)</c>: one line
/// segment along the robot's heading, executed as a path. A negative distance drives backwards.
/// </summary>
public sealed class DriveStraightAction
{
    private readonly ManipulationSystem _m;
    public DriveStraightAction(ManipulationSystem m, double distanceMm, float speedMmps = 100f) { _m = m; DistanceMm = distanceMm; SpeedMmps = speedMmps; }
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
