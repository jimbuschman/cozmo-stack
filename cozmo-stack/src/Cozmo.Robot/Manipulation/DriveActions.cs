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
    public double DistanceToleranceMm { get; set; } = DefaultGoalDistanceToleranceMm;
    public PathMotionProfile Profile { get; set; } = PathMotionProfile.Default;
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        if (Goal is not { } goal) { _trace.Add("DriveToPoseAction.Init.NoGoalSet"); return ActionResult.BadPose; }
        var start = _m.RobotPose();
        if (start is null) { _trace.Add("no robot state"); return ActionResult.Abort; }
        _ = _m.Robot.Motion.SetHeadAngleAsync((float)PathFollowingHeadAngleRad, requireCalibration: false);
        var path = StraightLinePlanner.Plan(start.Value, goal, Profile);
        if (path.Count == 0) { _trace.Add("already at the goal"); return ActionResult.Success; }
        ushort id = _m.Paths.Execute(path);
        _trace.Add($"path {id}: {path.Count} segment(s) from {start.Value.Translation} to {goal.Translation}");
        double lengthMm = 0;
        foreach (var s in path) if (s is PathSegment.Line l) lengthMm += Math.Sqrt((l.ToX - l.FromX) * (l.ToX - l.FromX) + (l.ToY - l.FromY) * (l.ToY - l.FromY));
        var timeout = TimeSpan.FromSeconds(5 + lengthMm / Math.Max(20, Profile.SpeedMmps) * 2 + path.Count * 3);
        var ev = await _m.Follower.WaitForEndAsync(id, timeout, cancel);
        if (ev is null) { _trace.Add("DriveToPoseAction.CheckIfDone.Failure: no path completion"); _m.Paths.Abort(); return cancel.IsCancellationRequested ? ActionResult.CancelledWhileRunning : ActionResult.FailedTraversingPath; }
        if (ev == PathEventType.Interrupted) { _trace.Add("path interrupted"); return ActionResult.FailedTraversingPath; }
        var now = _m.RobotPose();
        if (now is null) return ActionResult.Abort;
        var d = now.Value.Translation - goal.Translation;
        double dist = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        double dAngle = Math.Abs(StraightLinePlanner.Wrap(now.Value.AngleAroundZ - goal.AngleAroundZ));
        if (dist <= DistanceToleranceMm && dAngle <= GoalAngleToleranceRad)
        {
            _trace.Add($"DriveToPoseAction.CheckIfDone.Success: Tdiff={dist:F1}mm");
            return ActionResult.Success;
        }
        _trace.Add($"DriveToPoseAction.CheckIfDone.DoneNotInPlace: dist={dist:F1}mm angle={dAngle * 180 / Math.PI:F1}deg");
        return ActionResult.DidNotReachPreActionPose;
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
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var obj = _m.World.GetLocatedObjectById(ObjectId);
        if (obj is null) { _trace.Add($"DriveToObjectAction.CheckPreconditions.NoObjectWithID {ObjectId}"); return ActionResult.BadObject; }
        var robot = _m.RobotPose();
        if (robot is null) return ActionResult.Abort;
        var poses = CubePreActionPoses.For(obj, Type);
        if (poses.Count == 0) { _trace.Add($"DriveToObjectAction.CheckPreconditions.NoPreActionPoses for {Type}"); return ActionResult.NoPreActionPoses; }
        var closest = CubePreActionPoses.Closest(poses, robot.Value)!;
        Chosen = closest;
        double thresh = CubePreActionPoses.DistanceThresholdMm(obj.Pose, closest.WorldPose, PreActionAngleToleranceRad);
        var d = robot.Value.Translation - closest.WorldPose.Translation;
        bool close = Math.Abs(d.X) <= thresh && Math.Abs(d.Y) <= thresh
                     && Math.Abs(StraightLinePlanner.Wrap(robot.Value.AngleAroundZ - closest.WorldPose.AngleAroundZ)) <= PreActionAngleToleranceRad;
        if (close) _trace.Add($"DriveToObjectAction.GetPossiblePoses.UseRobotPose: within ({thresh:F1},{thresh:F1}) of the pre-action pose");
        else
        {
            var drive = new DriveToPoseAction(_m) { Goal = closest.WorldPose, DistanceToleranceMm = Math.Max(thresh, DriveToPoseAction.DefaultGoalDistanceToleranceMm), Profile = Profile };
            var r = await drive.RunAsync(cancel);
            _trace.AddRange(drive.Trace);
            if (r != ActionResult.Success) return r;
        }
        if (!_m.Docking.Carrying.IsCarrying(ObjectId))
        {
            bool turned = await _m.TurnTowardsObjectAsync(ObjectId, Math.PI, cancel);
            _trace.Add(turned ? "turned towards the object" : "TurnTowardsObjectAction did not complete");
        }
        // the object must still be located where we expect it
        return _m.World.GetLocatedObjectById(ObjectId) is null ? ActionResult.BadObject : ActionResult.Success;
    }
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
        ushort id = _m.Paths.Execute(new PathSegment[] { new PathSegment.Line(robot.Value.Translation.X, robot.Value.Translation.Y, to.X, to.Y, speed, p.AccelMmps2, p.DecelMmps2) });
        var ev = await _m.Follower.WaitForEndAsync(id, TimeSpan.FromSeconds(3 + Math.Abs(DistanceMm) / Math.Max(10, Math.Abs(SpeedMmps)) * 2), cancel);
        return ev == PathEventType.Completed ? ActionResult.Success : ev is null ? ActionResult.Timeout : ActionResult.FailedTraversingPath;
    }
}
