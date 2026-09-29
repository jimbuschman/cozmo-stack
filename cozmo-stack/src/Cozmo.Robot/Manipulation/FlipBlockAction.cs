using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// The engine's <c>FlipBlockAction</c> (0x0055EC80..0x0055F1C0, a <c>CompoundActionSequential</c>, RobotActionType
/// 0xF). Constructor constants, exactly as stored (M13-002): +0x12C = 150.0 (0x43160000), +0x130 = 20.0
/// (0x41A00000), +0x134 = 45.0 (0x42340000), +0x138 = 40.0 (0x42200000), +0x13C = -1, +0x140 = 1.
///
/// <b>The role of each offset (M13-002, gap pass 3).</b> +0x12C is the drive speed (150 mm/s),
/// +0x130 the drive-past distance (20 mm), +0x134 the approach lift height (45 mm), +0x138 the lift trigger
/// distance (40 mm), +0x13C the queued-lift action id (-1 when none) and +0x140 shouldCheckPreActionPose.
///
/// <c>Init</c> 0x0055EDC8: the object must be located; unless told otherwise the robot must be at a Flipping
/// pre-action pose within 5° (0.0872665); reactions are locked; a <c>MoveLiftToHeightAction(45, 5.0, 0)</c>
/// and a <c>DriveStraightAction(distance to the cube's centre + 20, 150, playAnim)</c> are added.
/// <c>CheckIfDone</c> 0x0055F074: once the robot is within <b>40 mm</b> of the cube and +0x13C is still -1 it
/// queues <c>MoveLiftToHeightAction(preset 2 = carry height, speed 5.0)</c> IN_PARALLEL
/// (<c>QueueActionPosition</c> 5) and stores its id at +0x13C.
/// </summary>
// fidelity: M13-002
public sealed class FlipBlockAction
{
    // M13-002: the constructor's exact stores, by offset and role.
    public const double Offset12C = 150.0;      // drive speed (mm/s)
    public const double Offset130 = 20.0;       // drive-past distance (mm)
    public const double Offset134 = 45.0;       // approach lift height (mm)
    public const double Offset138 = 40.0;       // lift trigger distance (mm)
    public const int Offset13C = -1;            // queued-lift action id (-1 = none)
    public const int Offset140 = 1;             // shouldCheckPreActionPose

    public const float DriveSpeedMmps = 150f;
    public const double DrivePastMm = 20.0;
    public const double ApproachLiftHeightMm = 45.0;
    public const double LiftTriggerDistanceMm = 40.0;
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
            // M12-020 C-E7/E9: FlipBlockAction::Init 0x0055EDC8 reaches IDockAction::GetPreActionPoses at
            // 0x0055EE5E, so it uses the threshold pair, not a fixed box.
            var poses = CubePreActionPoses.For(target, PreActionType.Flipping, robot.Value);
            if (!DockActionBase.IsCloseEnoughToPreActionPose(target, poses, robot.Value, PreActionAngleToleranceRad))
            { _trace.Add("FlipBlockAction.Init.NotAtPreActionPose"); return ActionResult.DidNotReachPreActionPose; }
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

    /// <summary>
    /// The maximum turn towards the last observed face after the drive, the constructor's Radians argument.
    /// <c>IDriveToInteractWithObject</c> 0x0055B1F4 adds a <c>TurnTowardsLastFacePoseAction</c> with it (and the
    /// say-name flag) after the drive when it is greater than zero (0x0055B380..0x0055B3C0), with its failure
    /// ignored (AddAction(action, true) at 0x0055B3D4); zero adds nothing. The constructor's trailing float is
    /// never read (0x0055E208 loads its stack arguments only up to +0x7C).
    /// </summary>
    public double MaxTurnTowardsFaceRad { get; init; }

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var drive = new DriveToObjectAction(_m, ObjectId, PreActionType.Flipping);
        var d = await drive.RunAsync(cancel);
        _trace.AddRange(drive.Trace);
        if (d != ActionResult.Success) return d;
        // fidelity: M13-014
        // IDriveToInteractWithObject 0x0055B1F4 adds TWO actions when maxTurn > 0: a
        // TurnTowardsLastFacePoseAction (vtable overwritten from TurnTowardsFaceAction at
        // 0x0055B3C4..0x0055B3D8) and a TurnTowardsObjectAction (0x0055B42E/0x0055B43C), both with
        // failure ignored (AddAction(action, true)). This stack has no TurnTowardsLastFacePoseAction
        // class, so the first is a TurnTowardsFaceAction to the last face - a labelled reduction, see
        // the build report. The second is built here.
        if (MaxTurnTowardsFaceRad > 0)
        {
            using var face = new TurnTowardsFaceAction(_m.Vision, SmartFaceID.Invalid, MaxTurnTowardsFaceRad, sayName: false);
            var f = await face.RunAsync(cancel);
            _trace.Add($"TurnTowardsLastFacePose (max {MaxTurnTowardsFaceRad:F3} rad): {f}, ignored");
            bool t = await _m.TurnTowardsObjectAsync(ObjectId, MaxTurnTowardsFaceRad, cancel);
            _trace.Add($"TurnTowardsObjectAction (max {MaxTurnTowardsFaceRad:F3} rad): {t}, ignored");
        }
        Flip = new FlipBlockAction(_m, ObjectId) { CheckPreActionPose = false };
        var r = await Flip.RunAsync(cancel);
        _trace.AddRange(Flip.Trace);
        return r;
    }
}
