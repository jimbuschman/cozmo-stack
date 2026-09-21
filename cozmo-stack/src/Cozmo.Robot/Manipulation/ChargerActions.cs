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
/// The engine's <c>MountChargerAction</c> (0x0054E018, RobotActionType 0x11), read through.
///
/// <c>Init</c> needs a located object of type Charger (0xD). Then two compound sub-actions run in turn.
///
/// <c>ConfigureAlignWithChargerAction</c> 0x0054E1C4 builds a sequence of:
/// <list type="number">
/// <item><c>AlignWithObjectAction(charger, 120 mm (0x42F00000), alignmentType 3)</c>, <c>SetSpeed(30)</c>;</item>
/// <item><c>MoveHeadToAngleAction(0 rad, tolerance 0.0349066 rad = 2 degrees)</c>.</item>
/// </list>
///
/// <c>ConfigureTurnAndMountAction</c> 0x0054E458 builds:
/// <list type="number">
/// <item><c>TurnInPlaceAction(atan2(v.y, v.x), absolute)</c> with <c>SetMaxSpeed(1.74533)</c> and
/// <c>SetAccel(5.23599)</c>, where v is <c>ComputeVectorBetween(robotPose, dockPoint)</c> - and that
/// function returns <b>a - b</b> (0x008476E6), so v points away from the charger and the robot ends up
/// facing out. The dock point is <c>Pose3d(0, Z, (30, 0, 0))</c> pre-composed with the charger's pose,
/// which is <see cref="ChargerGeometry.DockedRobotPose"/>'s translation;</item>
/// <item><c>MoveLiftToHeightAction(45 mm, speed 5, accel 0)</c>, but <b>only when the lift is below
/// 45</b> (<c>vcmpe</c> against 0x42340000 then <c>bpl</c> at 0x0054E59E) - the lift is raised to clear
/// the charger, not lowered;</item>
/// <item><c>BackupOntoChargerAction(-120 mm, 30 mm/s)</c>.</item>
/// </list>
///
/// <b>The reverse does watch the contacts.</b> <c>BackupOntoChargerAction::CheckIfDone</c> 0x0054E7A8 is
/// three lines: on the charger contacts it calls <c>Robot::SetPoseOnCharger()</c> and succeeds at once;
/// a pitch below -0.261799 rad (-15 degrees) fails it; otherwise it defers to
/// <c>DriveStraightAction::CheckIfDone</c>. An earlier reading of this stack had the engine always
/// driving the full 120 mm, which is not what the subclass does.
///
/// <b>The pi/2 heading test is a retry condition, not a success test.</b> <c>CheckIfDone</c> 0x0054E2D0
/// reaches it only when the turn-and-mount sub-action has <em>failed</em> (0x0054E31A skips it for
/// success and for still-running). Inside a right angle of the charger's yaw the failure simply stands;
/// outside it - the robot has turned round, so driving forward moves it clear - it warns "Turning and
/// mounting the charger failed ... Driving forward to position for a retry" and runs
/// <c>ConfigureDriveForRetryAction</c> 0x0054E72C, a <c>DriveStraightAction(120 mm, 100 mm/s)</c> whose
/// completion returns 0x04000006. The engine does not loop: it hands a retryable result back to
/// whoever ran it.
/// </summary>
public sealed class MountChargerAction
{
    /// <summary>120 mm: the custom align distance, 0x42F00000 at 0x0054E21C.</summary>
    public const double AlignDistanceMm = 120.0;
    /// <summary>30 mm/s: IDockAction::SetSpeed, 0x41F00000 at 0x0054E22A.</summary>
    public const float AlignSpeedMmps = 30f;
    /// <summary>2 degrees, the head tolerance the align sequence ends with (0x3D0EFA35).</summary>
    public const double HeadToleranceRad = 0.0349066;
    /// <summary>45 mm, the height the lift is raised to when it is below that (0x42340000).</summary>
    public const double LiftHeightForMountMm = 45.0;
    /// <summary>5 rad/s, the lift speed for that move (0x40A00000).</summary>
    public const float LiftSpeedRadPerSec = 5f;
    public const double MountDriveMm = -120.0;
    public const float MountSpeedMmps = 30f;
    public const double RetryDriveMm = 120.0;
    public const float RetrySpeedMmps = 100f;
    public const double TurnMaxSpeedRadPerSec = 1.74533;
    public const double TurnAccelRadPerSec2 = 5.23599;
    /// <summary>-15 degrees: BackupOntoChargerAction fails below this pitch (0xBE860A92).</summary>
    public const double MaxBackupPitchRad = -0.261799;
    /// <summary>pi/2: the window CheckIfDone uses to decide whether a failed mount is worth retrying.</summary>
    public const double RetryHeadingWindowRad = Math.PI / 2;

    private readonly ManipulationSystem _m;
    public MountChargerAction(ManipulationSystem m, uint chargerId) { _m = m; ChargerId = chargerId; }

    public uint ChargerId { get; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    /// <summary>
    /// Always one. The engine's action makes a single attempt and returns a retryable result; the loop,
    /// if there is to be one, belongs to whoever ran it.
    /// </summary>
    public int Attempts { get; private set; }

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        Attempts = 1;
        var charger = _m.World.GetLocatedObjectById(ChargerId);
        if (charger is null || charger.Type != ObjectType.Charger_Basic)
        {
            _trace.Add("MountChargerAction.Init.InvalidObject: not a located charger");
            return ActionResult.BadObject;
        }

        // ---- ConfigureAlignWithChargerAction
        var align = new AlignWithObjectAction(_m, ChargerId, AlignDistanceMm, AlignmentType.Custom)
            { Profile = PathMotionProfile.Default with { DockSpeedMmps = AlignSpeedMmps }, CheckPreActionPose = false };
        var a = await align.RunAsync(cancel);
        _trace.AddRange(align.Trace);
        if (a != ActionResult.Success)
        {
            // the align is the first sub-action of a sequence: its failure is the action's failure, and
            // the turn-and-mount is never configured (0x0054E304 is only reached on success)
            _trace.Add($"align with the charger: {a}");
            return a;
        }
        await _m.Robot.Motion.SetHeadAngleAsync(0f, requireCalibration: false);

        // ---- ConfigureTurnAndMountAction
        charger = _m.World.GetLocatedObjectById(ChargerId);
        var robot = _m.RobotPose();
        if (charger is null || robot is null) return ActionResult.BadObject;

        // The engine turns to atan2 of (robotPose - dockPoint), which faces out of the charger.
        var dock = ChargerGeometry.DockedRobotPose(charger.Pose).Translation;
        var away = robot.Value.Translation - dock;
        double heading = StraightLinePlanner.Wrap(Math.Atan2(away.Y, away.X));
        var turn = new PathSegment.PointTurn(robot.Value.Translation.X, robot.Value.Translation.Y, heading,
                                             StraightLinePlanner.PointTurnToleranceRad,
                                             (float)TurnMaxSpeedRadPerSec, (float)TurnAccelRadPerSec2,
                                             (float)TurnAccelRadPerSec2, true);
        using (var run = _m.StartPath(new PathSegment[] { turn }))
        {
            var ev = await run.WaitAsync(TimeSpan.FromSeconds(6), cancel);
            if (ev != PathEventType.Completed) { _trace.Add("MountChargerAction: the turn did not complete"); return ActionResult.Retry; }
        }

        // the lift goes UP to 45 when it is below it, to clear the charger
        if (_m.Robot.Sensors.LiftHeightMm is { } lift && lift < LiftHeightForMountMm)
            await _m.Robot.Motion.SetLiftHeightAsync((float)LiftHeightForMountMm,
                                                     maxSpeedRadPerSec: LiftSpeedRadPerSec, requireCalibration: false);

        // ---- BackupOntoChargerAction: the reverse ends the moment the contacts report
        var r = await BackupOntoChargerAsync(cancel);
        if (r == ActionResult.Success)
        {
            _trace.Add("MountChargerAction: on the contacts");
            return ActionResult.Success;
        }

        // ---- the failure path, and the only place the pi/2 window is consulted
        var chargerNow = _m.World.GetLocatedObjectById(ChargerId);
        var robotNow = _m.RobotPose();
        if (chargerNow is not null && robotNow is not null &&
            Math.Abs(StraightLinePlanner.Wrap(chargerNow.Pose.AngleAroundZ - robotNow.Value.AngleAroundZ))
                <= RetryHeadingWindowRad)
        {
            _trace.Add($"MountChargerAction: the mount failed ({r}) within a right angle of the charger, so no retry drive");
            return r;
        }
        _trace.Add($"Turning and mounting the charger failed ({r}). Driving forward to position for a retry");
        await new DriveStraightAction(_m, RetryDriveMm, RetrySpeedMmps).RunAsync(cancel);
        return ActionResult.Retry;
    }

    /// <summary>
    /// <c>BackupOntoChargerAction</c>: the straight drive, ended early by the charger contacts and failed
    /// by too steep a pitch.
    /// </summary>
    private async Task<ActionResult> BackupOntoChargerAsync(CancellationToken cancel)
    {
        if (_m.Robot.Sensors.OnCharger) return ActionResult.Success;
        var robot = _m.RobotPose();
        if (robot is null) return ActionResult.Abort;

        double h = robot.Value.AngleAroundZ;
        var to = robot.Value.Translation + new Vec3(Math.Cos(h) * MountDriveMm, Math.Sin(h) * MountDriveMm, 0);
        var profile = PathMotionProfile.Default;
        using var run = _m.StartPath(new PathSegment[]
        {
            new PathSegment.Line(robot.Value.Translation.X, robot.Value.Translation.Y, to.X, to.Y,
                                 -MountSpeedMmps, profile.AccelMmps2, profile.DecelMmps2),
        });

        var arrived = run.WaitAsync(TimeSpan.FromSeconds(3 + Math.Abs(MountDriveMm) / MountSpeedMmps * 2), cancel);
        var contacts = WaitForChargerAsync(TimeSpan.FromSeconds(20), cancel);
        var first = await Task.WhenAny(arrived, contacts);
        if (first == contacts && contacts.Result)
        {
            run.Abort();
            _trace.Add("BackupOntoChargerAction: the contacts reported, so the reverse ends here");
            return ActionResult.Success;
        }

        var ev = await arrived;
        if (_m.Robot.Sensors.OnCharger) return ActionResult.Success;
        if (_m.Robot.Sensors.PitchRad is { } pitch && pitch < MaxBackupPitchRad)
        {
            _trace.Add($"BackupOntoChargerAction: pitched to {pitch} rad, below {MaxBackupPitchRad}");
            return ActionResult.Retry;
        }
        // the drive ran its length without ever reaching the contacts
        _trace.Add($"BackupOntoChargerAction: the reverse finished ({ev}) off the contacts");
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
