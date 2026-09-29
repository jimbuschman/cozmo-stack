using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary><c>Anki::Cozmo::AlignmentType</c>: how <c>AlignWithObjectAction</c> measures its distance.
/// The numeric values 0..3 are the engine's; the names are this stack's labels (the M13-016 inventory
/// settles the numeric table, not the enum names).</summary>
public enum AlignmentType : byte { LiftFinger = 0, LiftPlate = 1, Body = 2, Custom = 3 }

/// <summary>
/// The engine's <c>AlignWithObjectAction</c> (constructor body 0x00553370): a dock action that stops a
/// chosen part of the robot at a distance from the object's marker instead of engaging it.
///
/// The alignment-type table (M13-016, corrected in gap pass 2) is by numeric type, not by name:
/// <list type="bullet">
/// <item>type 0 -&gt; distance <b>6.0</b> (<c>vmov.f32 s16,#6.0</c> at 0x005533EE);</item>
/// <item>type 1 -&gt; flag +0xBB = <b>2</b> and distance stays <b>0.0</b> (0x005533F4);</item>
/// <item>type 2 -&gt; distance <b>-15.0</b> (0x005533FC);</item>
/// <item>type 3 -&gt; distance <b>argument + (-27.0)</b> (0x00553402..0x0055340A);</item>
/// <item>invalid (&gt; 3) -&gt; distance stays 0.0 (0x005533C8, the table is skipped at 0x005533E4).</item>
/// </list>
/// The distance is clamped to 0.0 when it is below <b>-16.000009536743164</b> (0xC1800005 at 0x00553480;
/// <c>vcmpe/it mi/vmovmi</c> at 0x0055341C..0x00553426).
///
/// <c>GetPreActionTypeFromAlignmentType</c> 0x005532B8 maps type 0-&gt;1, 1-&gt;0, 2-&gt;1, 3-&gt;1 and an
/// invalid type logs and returns 1 (table at 0x00553360, default at 0x00553320). The value is stored at
/// +0xFC. This stack casts it to <see cref="PreActionType"/> by numeric value; whether the engine's
/// pre-action numbering is the same enum is not established by the M13 inventory (see the report).
///
/// The firmware dock action is <c>ALIGN</c> with the resulting distance as the dock's placement offset
/// along X.
/// </summary>
// fidelity: M13-016
public sealed class AlignWithObjectAction : DockActionBase
{
    /// <summary>Alignment type 0's distance: 6.0 (0x005533EE).</summary>
    public const double Type0DistanceMm = 6.0;
    /// <summary>Alignment type 2's distance: -15.0 (0x005533FC).</summary>
    public const double Type2DistanceMm = -15.0;
    /// <summary>Alignment type 3's offset: argument + (-27.0) (0x00553402..0x0055340A).</summary>
    public const double Type3OffsetMm = -27.0;
    /// <summary>The clamp threshold: 0xC1800005 = -16.000009536743164 (0x00553480).</summary>
    public const double ClampThresholdMm = -16.000009536743164;
    /// <summary>The flag value type 1 writes at +0xBB (0x005533F4/0x005533F6): <see cref="DockingMethod.Method2"/>.</summary>
    public const byte Type1Flag = 2;

    public AlignWithObjectAction(ManipulationSystem m, uint objectId, double distanceMm, AlignmentType alignment) : base(m, objectId)
    {
        DistanceMm = distanceMm; Alignment = alignment;
    }

    public double DistanceMm { get; }
    public AlignmentType Alignment { get; }

    /// <summary>
    /// +0xBB, <c>DockWithObject</c> field 7: the engine's constructor leaves it 0 and alignment type 1
    /// writes 2 (0x005533F4/0x005533F6, M13-016).
    /// </summary>
    protected override DockingMethod DockingMethod => (int)Alignment == 1 ? DockingMethod.Method2 : DockingMethod.Default;

    /// <summary>+0xBB as this stack sends it (see <see cref="DockingMethod"/>).</summary>
    public DockingMethod AlignmentDockingMethod => DockingMethod;

    /// <summary><c>GetPreActionTypeFromAlignmentType</c> 0x005532B8: 0-&gt;1, 1-&gt;0, 2-&gt;1, 3-&gt;1, invalid-&gt;1.</summary>
    public static int GetPreActionTypeFromAlignmentType(int alignmentType) => alignmentType switch
    {
        0 => 1,
        1 => 0,
        2 => 1,
        3 => 1,
        _ => 1,
    };

    /// <summary>The pre-action type the engine stores at +0xFC, by numeric value.</summary>
    public int AlignmentPreActionType => GetPreActionTypeFromAlignmentType((int)Alignment);

    /// <summary>The distance the dock is asked for, after the alignment offset and the clamp.</summary>
    public double DockDistanceMm
    {
        get
        {
            double d = (int)Alignment switch
            {
                0 => Type0DistanceMm,
                1 => 0.0,
                2 => Type2DistanceMm,
                3 => DistanceMm + Type3OffsetMm,
                _ => 0.0,
            };
            return d < ClampThresholdMm ? 0.0 : d;
        }
    }

    protected override PreActionType PreActionType => (PreActionType)AlignmentPreActionType;
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
// fidelity: M13-008, M13-012
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
    /// <summary>0x3FDF66F3 = 1.7453292608261108 rad/s = 100 deg/s (0x0054E550/0x0054E55A, M13-008).</summary>
    public const double TurnMaxSpeedRadPerSec = 1.7453292608261108;
    /// <summary>0x40A78D36 = 5.235987663269043 rad/s^2 (0x0054E55E/0x0054E568, M13-008).</summary>
    public const double TurnAccelRadPerSec2 = 5.235987663269043;
    /// <summary>-15 degrees: BackupOntoChargerAction fails below this pitch (0xBE860A92 = -0.2617993950843811, M13-008).</summary>
    public const double MaxBackupPitchRad = -0.2617993950843811;
    /// <summary>pi/2: the window CheckIfDone uses to decide whether a failed mount is worth retrying.</summary>
    public const double RetryHeadingWindowRad = Math.PI / 2;
    /// <summary>0x04000006: the retry drive's result (0x0054E3E8, M13-008).</summary>
    public const ActionResult RetryDriveResult = ActionResult.RetryDriveDone;

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
        await _m.Robot.Motion.SetHeadAngleAsync(0f, CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2, requireCalibration: false);

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
        return RetryDriveResult;
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
            _trace.Add($"BackupOntoChargerAction: pitched to {pitch} rad, below {MaxBackupPitchRad}; 0x0400000A");
            return ActionResult.BackupPitchedTooFar;
        }
        // the drive ran its length without ever reaching the contacts: the fall-through returns 0x04000006
        _trace.Add($"BackupOntoChargerAction: the reverse finished ({ev}) off the contacts; 0x04000006");
        return ActionResult.RetryDriveDone;
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
/// The engine's <c>DriveOffChargerContactsAction</c> (ctor 0x00558228): a <c>DriveStraightAction</c>
/// constructed with <b>10 mm at 20 mm/s, false</b> (0x00558232/0x00558236/0x0055823E), with +0x44 = 7
/// (0x00558276/0x00558278), which is <c>IActionRunner</c>'s <c>RobotActionType</c>
/// <b>DRIVE_OFF_CHARGER_CONTACTS</b> (RobotActionTypeFromString 0x0075A448), and, <b>in SDK mode only</b>,
/// <c>SetTracksToLock(0)</c> in the constructor (0x0055827C/0x00558286/0x00558288). That clear is a local
/// <c>IActionRunner+0x54</c> flag write, not a robot message; this stack has no track-lock model, so it is
/// noted in the trace and not sent.
///
/// <c>Init</c> 0x005582D0 copies the robot's on-contacts flag (robot+0x338) into action+0x8B and returns 0
/// when the robot is not on the contacts. <c>CheckIfDone</c> 0x005582E4 retries while the drive is still
/// running and fails <b>0x04000009</b> if the robot is still on the contacts (0x00558344).
/// <c>BehaviorDriveOffCharger</c> gives it the charger's length plus its config's extra distance (M13-017).
/// </summary>
// fidelity: M13-013
public sealed class DriveOffChargerContactsAction
{
    /// <summary>The constructor's 10 mm (0x41200000 at 0x00558232).</summary>
    public const double ConstructorDistanceMm = 10.0;
    /// <summary>The constructor's 20 mm/s (0x41A00000 at 0x00558236).</summary>
    public const float ConstructorSpeedMmps = 20f;
    /// <summary>+0x44 = 7 = <c>RobotActionType::DRIVE_OFF_CHARGER_CONTACTS</c> (0x00558276/0x00558278).</summary>
    public const int RobotActionTypeDriveOffChargerContacts = 7;
    public const float DefaultSpeedMmps = ConstructorSpeedMmps;

    private readonly ManipulationSystem _m;
    public DriveOffChargerContactsAction(ManipulationSystem m, double distanceMm, float speedMmps = DefaultSpeedMmps) { _m = m; DistanceMm = distanceMm; SpeedMmps = speedMmps; }
    public double DistanceMm { get; }
    public float SpeedMmps { get; }
    /// <summary>action+0x8B: the robot's on-contacts flag captured at <c>Init</c>.</summary>
    public bool WasOnContactsAtInit { get; private set; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        // Init 0x005582D0: capture robot+0x338 and return 0 (success) when not on the contacts.
        WasOnContactsAtInit = _m.Robot.Sensors.OnCharger;
        _trace.Add("IActionRunner.SetTracksToLock(0): local track-lock flag, no robot message (no track-lock model)");
        if (!WasOnContactsAtInit) { _trace.Add("DriveOffChargerContactsAction.Init: not on contacts, nothing to do"); return ActionResult.Success; }
        var r = await new DriveStraightAction(_m, DistanceMm, SpeedMmps).RunAsync(cancel);
        if (r != ActionResult.Success) { _trace.Add($"drive off the charger: {r}"); return r; }
        if (_m.Robot.Sensors.OnCharger) { _trace.Add("DriveOffChargerContactsAction.StillOnCharger: 0x04000009"); return ActionResult.StillOnCharger; }
        return ActionResult.Success;
    }
}
