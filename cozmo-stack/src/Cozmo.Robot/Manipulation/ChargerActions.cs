using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary><c>Anki::Cozmo::AlignmentType</c> (UNITY): how <c>AlignWithObjectAction</c> measures its distance.</summary>
public enum AlignmentType : byte { LiftFinger = 0, LiftPlate = 1, Body = 2, Custom = 3 }

/// <summary>
/// The engine's <c>AlignWithObjectAction</c> (constructor 0x0054C6xx): a dock action that stops a chosen part
/// of the robot at a distance from the object's marker instead of engaging it. The constructor's alignment
/// offsets (NATIVE): LIFT_FINGER 0, LIFT_PLATE 6, BODY −15, and CUSTOM = requested distance − 27 (the robot
/// origin is 27 mm behind the lift fingers). The firmware dock action is <c>ALIGN</c> with the resulting
/// distance as the dock's placement offset along X; the placement offset for a floor object is −16 (not used
/// for the charger). Verify: success when the firmware reports the dock succeeded.
/// </summary>
public sealed class AlignWithObjectAction : DockActionBase
{
    public const double FingerToOriginMm = 27.0;

    public AlignWithObjectAction(ManipulationSystem m, uint objectId, double distanceMm, AlignmentType alignment) : base(m, objectId)
    {
        DistanceMm = distanceMm; Alignment = alignment;
    }

    public double DistanceMm { get; }
    public AlignmentType Alignment { get; }

    /// <summary>The distance the dock is asked for, after the alignment offset.</summary>
    public double DockDistanceMm => Alignment switch
    {
        AlignmentType.LiftFinger => DistanceMm,
        AlignmentType.LiftPlate => DistanceMm + 6,
        AlignmentType.Body => DistanceMm - 15,
        _ => DistanceMm - FingerToOriginMm,
    };

    protected override PreActionType PreActionType => PreActionType.Docking;
    protected override DockAction? SelectDockAction(ObservableObject target) => DockAction.Align;
    protected override (double X, double Y, double Angle) PlacementOffset => (DockDistanceMm, 0, 0);
    protected override ActionResult Verify(ObservableObject? target, DockResult result) => result.Succeeded ? ActionResult.Success : ActionResult.Retry;
}

/// <summary>
/// The engine's <c>MountChargerAction</c> (0x0054E018..0x0054E9xx, RobotActionType 0x11): <c>Init</c> needs a
/// located object of type Charger (0xD); <c>ConfigureAlignWithChargerAction</c> runs an
/// <c>AlignWithObjectAction(charger, 120 mm (0x42F00000), CUSTOM)</c> at 30 mm/s (<c>SetSpeed</c>, 0x41F00000)
/// with the head at 0 (tolerance 2°); then <c>ConfigureTurnAndMountAction</c>: a <c>TurnInPlaceAction</c> to the
/// heading of the vector towards the docked pose (absolute; max speed 1.74533 rad/s, accel 5.23599), the lift
/// lowered to 45 mm (0x42340000, speed 5) if it is above 45, and a <c>DriveStraightAction(−120 mm, 30 mm/s)</c>
/// backwards onto the charger. When the turn-and-mount fails ("Turning and mounting the charger failed ...
/// Driving forward to position for a retry") <c>ConfigureDriveForRetryAction</c> drives forward 120 mm at
/// 100 mm/s and the align repeats. Success is the robot's IS_ON_CHARGER flag (the engine's
/// <c>CheckIfDone</c> reads <c>Robot::IsOnChargerContacts</c>; INFERRED: the backwards drive is allowed to end
/// short when the contacts report). Retries: <see cref="MaxRetries"/> (INFERRED 2).
/// </summary>
public sealed class MountChargerAction
{
    public const double AlignDistanceMm = 120.0;
    public const float AlignSpeedMmps = 30f;
    public const double LiftHeightForMountMm = 45.0;
    public const double MountDriveMm = -120.0;
    public const float MountSpeedMmps = 30f;
    public const double RetryDriveMm = 120.0;
    public const float RetrySpeedMmps = 100f;
    public const double TurnMaxSpeedRadPerSec = 1.74533;
    public const double TurnAccelRadPerSec2 = 5.23599;
    public const int MaxRetries = 2;

    private readonly ManipulationSystem _m;
    public MountChargerAction(ManipulationSystem m, uint chargerId) { _m = m; ChargerId = chargerId; }

    public uint ChargerId { get; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();
    public int Attempts { get; private set; }

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var charger = _m.World.GetLocatedObjectById(ChargerId);
        if (charger is null || charger.Type != ObjectType.Charger_Basic) { _trace.Add("MountChargerAction.Init.InvalidObject: not a located charger"); return ActionResult.BadObject; }
        for (Attempts = 1; Attempts <= 1 + MaxRetries; Attempts++)
        {
            if (cancel.IsCancellationRequested) return ActionResult.CancelledWhileRunning;
            _ = _m.Robot.Motion.SetHeadAngleAsync(0f, requireCalibration: false);
            var align = new AlignWithObjectAction(_m, ChargerId, AlignDistanceMm, AlignmentType.Custom)
                { Profile = PathMotionProfile.Default with { DockSpeedMmps = AlignSpeedMmps }, CheckPreActionPose = false };
            var a = await align.RunAsync(cancel);
            _trace.AddRange(align.Trace);
            if (a != ActionResult.Success) { _trace.Add($"align with the charger: {a}"); if (a is ActionResult.BadObject or ActionResult.CancelledWhileRunning) return a; continue; }

            // turn to face away from the charger: towards the docked pose's heading vector
            charger = _m.World.GetLocatedObjectById(ChargerId);
            var robot = _m.RobotPose();
            if (charger is null || robot is null) return ActionResult.BadObject;
            var docked = ChargerGeometry.DockedRobotPose(charger.Pose);
            var v = docked.Translation - robot.Value.Translation;
            double heading = StraightLinePlanner.Wrap(Math.Atan2(v.Y, v.X) + Math.PI);      // the robot backs towards the docked pose
            var turn = new PathSegment.PointTurn(robot.Value.Translation.X, robot.Value.Translation.Y, heading, StraightLinePlanner.PointTurnToleranceRad,
                                                 (float)TurnMaxSpeedRadPerSec, (float)TurnAccelRadPerSec2, (float)TurnAccelRadPerSec2, true);
            using var run = _m.StartPath(new PathSegment[] { turn });
            var ev = await run.WaitAsync(TimeSpan.FromSeconds(6), cancel);
            if (ev != PathEventType.Completed) { _trace.Add("MountChargerAction: the turn did not complete"); continue; }
            if (_m.Robot.Sensors.LiftHeightMm is { } lift && lift > LiftHeightForMountMm)
                await _m.Robot.Motion.SetLiftHeightAsync((float)LiftHeightForMountMm, maxSpeedRadPerSec: 5f, requireCalibration: false);

            var back = new DriveStraightAction(_m, MountDriveMm, MountSpeedMmps);
            var drive = back.RunAsync(cancel);
            var onCharger = WaitForChargerAsync(TimeSpan.FromSeconds(3 + Math.Abs(MountDriveMm) / MountSpeedMmps * 2), cancel);
            var done = await Task.WhenAny(drive, onCharger);
            if (done == onCharger && onCharger.Result) { _m.Paths.Abort(); _trace.Add("MountChargerAction: on the charger contacts"); return ActionResult.Success; }
            var r = await drive;
            if (_m.Robot.Sensors.OnCharger) { _trace.Add("MountChargerAction: on the charger"); return ActionResult.Success; }
            _trace.Add($"Turning and mounting the charger failed ({r}). Driving forward to position for a retry");
            await new DriveStraightAction(_m, RetryDriveMm, RetrySpeedMmps).RunAsync(cancel);
        }
        _trace.Add("MountChargerAction: out of retries");
        return ActionResult.Retry;
    }

    private async Task<bool> WaitForChargerAsync(TimeSpan timeout, CancellationToken cancel)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void On(bool on) { if (on) tcs.TrySetResult(true); }
        _m.Robot.Sensors.OnChargerChanged += On;
        try
        {
            if (_m.Robot.Sensors.OnCharger) return true;
            using var reg = cancel.Register(() => tcs.TrySetResult(false));
            var t = await Task.WhenAny(tcs.Task, Task.Delay(timeout, CancellationToken.None));
            return t == tcs.Task && tcs.Task.Result;
        }
        finally { _m.Robot.Sensors.OnChargerChanged -= On; }
    }
}

/// <summary>
/// The engine's <c>DriveOffChargerContactsAction</c> (0x00558228): a <c>DriveStraightAction</c> (constructed
/// with 10 mm at 20 mm/s, then given its distance by the behaviour) whose <c>CheckIfDone</c> also requires the
/// robot to have left the charger contacts ("StillOnCharger" retries). <c>BehaviorDriveOffCharger</c> drives the
/// charger's length (96) plus its config's <c>extraDistanceToDrive_mm</c>.
/// </summary>
public sealed class DriveOffChargerContactsAction
{
    public const float DefaultSpeedMmps = 20f;
    private readonly ManipulationSystem _m;
    public DriveOffChargerContactsAction(ManipulationSystem m, double distanceMm, float speedMmps = DefaultSpeedMmps) { _m = m; DistanceMm = distanceMm; SpeedMmps = speedMmps; }
    public double DistanceMm { get; }
    public float SpeedMmps { get; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var r = await new DriveStraightAction(_m, DistanceMm, SpeedMmps).RunAsync(cancel);
        if (r != ActionResult.Success) { _trace.Add($"drive off the charger: {r}"); return r; }
        if (_m.Robot.Sensors.OnCharger) { _trace.Add("DriveOffChargerContactsAction.StillOnCharger"); return ActionResult.Retry; }
        return ActionResult.Success;
    }
}
