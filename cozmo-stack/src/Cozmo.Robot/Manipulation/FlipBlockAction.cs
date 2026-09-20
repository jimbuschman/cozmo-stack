using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// The engine's <c>FlipBlockAction</c> (0x0055EC80..0x0055F1C0, a <c>CompoundActionSequential</c>, RobotActionType
/// 0xF). Constructor members (NATIVE values; roles INFERRED from their use): +0x12C = 150.0 (the drive speed),
/// +0x130 = 20.0 (driven past the cube's centre), +0x134 = 40.0 (the lift height while approaching),
/// +0x138 = 45.0 (the distance at which the lift comes up). <c>Init</c>: the object must be located; unless
/// told otherwise the robot must be at a Flipping pre-action pose within 5° (0.0872665); reactions are locked;
/// a <c>DriveStraightAction(distance to the cube's centre + 20, 150, playAnim)</c> and a
/// <c>MoveLiftToHeightAction(40, speed 5.0)</c> are added. <c>CheckIfDone</c>: once the robot is within 45 mm of
/// the cube it queues <c>MoveLiftToHeightAction(preset 2 = carry height, speed 5.0)</c> IN_PARALLEL
/// (<c>QueueActionPosition</c> 5) and marks the object's pose Unknown (<c>ObjectPoseConfirmer::MarkObjectUnknown</c>):
/// the rising lift flips the cube over the robot's shoulder as it drives through.
/// </summary>
public sealed class FlipBlockAction
{
    public const float DriveSpeedMmps = 150f;
    public const double DrivePastMm = 20.0;
    public const double ApproachLiftHeightMm = 40.0;
    public const double LiftTriggerDistanceMm = 45.0;
    public const double PreActionAngleToleranceRad = 0.0872665;
    public const float LiftSpeedRadPerSec = 5f;

    private readonly ManipulationSystem _m;
    public FlipBlockAction(ManipulationSystem m, uint objectId) { _m = m; ObjectId = objectId; }

    public uint ObjectId { get; }
    /// <summary><c>SetShouldCheckPreActionPose</c>: false when the behaviour flips blindly after a failed drive.</summary>
    public bool CheckPreActionPose { get; set; } = true;
    public bool LiftRaised { get; private set; }
    private Task? _raise;
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var target = _m.World.GetLocatedObjectById(ObjectId);
        if (target is null) { _trace.Add("FlipBlockAction.Init.NullObject"); return ActionResult.BadObject; }
        var robot = _m.RobotPose();
        if (robot is null) return ActionResult.Abort;
        if (CheckPreActionPose)
        {
            var poses = CubePreActionPoses.For(target, PreActionType.Flipping);
            bool near = poses.Any(p =>
            {
                double thresh = CubePreActionPoses.DistanceThresholdMm(target.Pose, p.WorldPose, PreActionAngleToleranceRad);
                var d = robot.Value.Translation - p.WorldPose.Translation;
                return Math.Abs(d.X) <= Math.Max(thresh, 10) && Math.Abs(d.Y) <= Math.Max(thresh, 10)
                       && Math.Abs(StraightLinePlanner.Wrap(robot.Value.AngleAroundZ - p.WorldPose.AngleAroundZ)) <= PreActionAngleToleranceRad;
            });
            if (!near) { _trace.Add("FlipBlockAction.Init.NotAtPreActionPose"); return ActionResult.DidNotReachPreActionPose; }
        }
        var toCube = target.Pose.Translation - robot.Value.Translation;
        double dist = Math.Sqrt(toCube.X * toCube.X + toCube.Y * toCube.Y);
        _trace.Add($"FlipBlockAction: driving {dist + DrivePastMm:F0} mm at {DriveSpeedMmps} mm/s with the lift at {ApproachLiftHeightMm} mm");
        var lift = _m.Robot.Motion.SetLiftHeightAsync((float)ApproachLiftHeightMm, maxSpeedRadPerSec: LiftSpeedRadPerSec, requireCalibration: false);
        var drive = new DriveStraightAction(_m, dist + DrivePastMm, DriveSpeedMmps).RunAsync(cancel);
        // CheckIfDone: watch the distance to the cube while driving
        var cubePos = target.Pose.Translation;
        while (!drive.IsCompleted)
        {
            var now = _m.RobotPose();
            if (now is { } n && !LiftRaised)
            {
                var d = cubePos - n.Translation;
                if (Math.Sqrt(d.X * d.X + d.Y * d.Y) < LiftTriggerDistanceMm) RaiseLift();
            }
            await Task.WhenAny(drive, Task.Delay(10, CancellationToken.None));
        }
        if (!LiftRaised) RaiseLift();          // the fake or a fast robot may finish the drive before a state showed it close
        var r = await drive;
        await lift;
        // The lift-up is the operation that actually flips the cube, so it belongs to the action's lifetime:
        // the engine's compound action does not report done until every part of it is. Reporting Success while
        // this was still in flight meant the caller could start the next action mid-flip.
        if (_raise is { } raise) await raise;
        _trace.Add($"FlipBlockAction: drive {r}; object {ObjectId} marked Unknown");
        return r == ActionResult.Success ? ActionResult.Success : r;

        void RaiseLift()
        {
            LiftRaised = true;
            _trace.Add($"FlipBlockAction.CheckIfDone: within {LiftTriggerDistanceMm} mm, lift to carry height ({LiftPresets.CarryMm} mm)");
            _raise = _m.Robot.Motion.SetLiftHeightAsync(LiftPresets.CarryMm, maxSpeedRadPerSec: LiftSpeedRadPerSec, requireCalibration: false);
            _m.World.MarkUnknown(ObjectId);
        }
    }
}

/// <summary>
/// The engine's <c>DriveAndFlipBlockAction</c> (0x0055E208): an <c>IDriveToInteractWithObject</c> of a
/// <c>FlipBlockAction</c>: drive to a Flipping pre-action pose (the engine prefers the one nearest the last
/// observed face, <c>FaceWorld::GetLastObservedFace</c> in <c>GetPossiblePoses</c>; without face tracking the
/// closest is used, LOCAL_POLICY), then flip without re-checking the pose.
/// </summary>
public sealed class DriveAndFlipBlockAction
{
    private readonly ManipulationSystem _m;
    public DriveAndFlipBlockAction(ManipulationSystem m, uint objectId) { _m = m; ObjectId = objectId; }
    public uint ObjectId { get; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();
    public FlipBlockAction? Flip { get; private set; }

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var drive = new DriveToObjectAction(_m, ObjectId, PreActionType.Flipping);
        var d = await drive.RunAsync(cancel);
        _trace.AddRange(drive.Trace);
        if (d != ActionResult.Success) return d;
        Flip = new FlipBlockAction(_m, ObjectId) { CheckPreActionPose = false };
        var r = await Flip.RunAsync(cancel);
        _trace.AddRange(Flip.Trace);
        return r;
    }
}
