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
        var d = now.Value.Translation - goal.Translation;
        double dist = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        double dAngle = Math.Abs(StraightLinePlanner.Wrap(now.Value.AngleAroundZ - goal.AngleAroundZ));
        if (dist <= DistanceToleranceMm && dAngle <= GoalAngleToleranceRad)
        {
            _trace.Add($"DriveToPoseAction.CheckIfDone.Success: Tdiff={dist:F1}mm");
            Goal = goal;
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
    /// <summary>
    /// Pre-action poses a previous attempt already failed from, excluded here the way
    /// <c>IBehavior::UseSecondClosestPreActionPose</c> (0x005BEE40) does it: it re-reads the possible poses and
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
        var poses = CubePreActionPoses.For(obj, Type);
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
        double thresh = CubePreActionPoses.DistanceThresholdMm(obj.Pose, closest.WorldPose, PreActionAngleToleranceRad);
        var d = robot.Value.Translation - closest.WorldPose.Translation;
        bool close = Math.Abs(d.X) <= thresh && Math.Abs(d.Y) <= thresh
                     && Math.Abs(StraightLinePlanner.Wrap(robot.Value.AngleAroundZ - closest.WorldPose.AngleAroundZ)) <= PreActionAngleToleranceRad;
        if (close) _trace.Add($"DriveToObjectAction.GetPossiblePoses.UseRobotPose: within ({thresh:F1},{thresh:F1}) of the pre-action pose");
        else
        {
            var drive = new DriveToPoseAction(_m) { Goal = closest.WorldPose, Goals = poses.Select(p => p.WorldPose).ToList(), IgnoreObstacleIds = new[] { ObjectId },
                                                     DistanceToleranceMm = Math.Max(thresh, DriveToPoseAction.DefaultGoalDistanceToleranceMm), Profile = Profile };
            var r = await drive.RunAsync(cancel);
            _trace.AddRange(drive.Trace);
            if (r != ActionResult.Success) return r;
            if (drive.Goal is { } reached) Chosen = poses.FirstOrDefault(p => p.WorldPose.Equals(reached)) ?? closest;
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
