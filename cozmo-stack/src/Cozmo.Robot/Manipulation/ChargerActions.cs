using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
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
/// The alignment-type table (M13-016, corrected in gap pass 2) is by numeric type, not by name, and every value is binary32
/// (the constructor holds the distance in <c>s16</c> and stores it with <c>vstr</c> at +0x9C, 0x00553436):
/// <list type="bullet">
/// <item>type 0 -&gt; distance <b>6.0</b> (<c>vmov.f32 s16,#6.0</c> at 0x005533EE);</item>
/// <item>type 1 -&gt; flag +0xBB = <b>2</b> and distance stays <b>0.0</b> (0x005533F4);</item>
/// <item>type 2 -&gt; distance <b>-15.0</b> (0x005533FC);</item>
/// <item>type 3 -&gt; distance <b>argument + (-27.0)</b>, a <c>vadd.f32</c> (0x00553402..0x0055340A);</item>
/// <item>invalid (&gt; 3) -&gt; distance stays 0.0 (<c>bhi</c> at 0x005533E4).</item>
/// </list>
/// The distance is clamped to 0.0 when it is below the float <b>0xC1800005</b> (-16.000009536743164, 0x00553414) by
/// <c>vcmpe.f32</c>/<c>vmovmi.f32</c> (0x0055341C..0x00553426): a strict float less-than, so NaN is kept.
///
/// <c>GetPreActionTypeFromAlignmentType</c> 0x005532B8 maps type 0-&gt;1, 1-&gt;0, 2-&gt;1, 3-&gt;1 and an
/// invalid type logs and returns 1 (table at 0x00553360, default at 0x00553320). The value is stored at
/// +0xFC. This stack casts it to <see cref="PreActionType"/> by numeric value; whether the engine's
/// pre-action numbering is the same enum is not established by the M13 inventory (see the report).
///
/// The firmware dock action is <c>ALIGN</c> with the resulting distance as the dock's placement offset
/// along X (+0x9C, with +0xA0 and +0xA4 zero, 0x00553430).
/// </summary>
// fidelity: M13-016
public sealed class AlignWithObjectAction : DockActionBase
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    /// <summary>Alignment type 0's distance: 6.0f (<c>vmov.f32 s16,#6.0</c> at 0x005533EE).</summary>
    public static readonly float Type0DistanceMm = F(0x40C00000);
    /// <summary>Alignment type 2's distance: -15.0f (<c>vmov.f32 s16,#-15.0</c> at 0x005533FC).</summary>
    public static readonly float Type2DistanceMm = F(0xC1700000);
    /// <summary>Alignment type 3's offset: argument + (-27.0f) (<c>vmov.f32 s2,#-27.0</c> at 0x00553402).</summary>
    public static readonly float Type3OffsetMm = F(0xC1D80000);
    /// <summary>The clamp threshold: the float 0xC1800005 = -16.000009536743164 (the literal loaded at 0x00553414).</summary>
    public static readonly float ClampThresholdMm = F(0xC1800005);
    /// <summary>The flag value type 1 writes at +0xBB (0x005533F4/0x005533F6): <see cref="DockingMethod.Method2"/>.</summary>
    public const byte Type1Flag = 2;

    private readonly bool _dockFlag95;

    /// <param name="dockFlag95">The constructor's last bool, passed to <c>IDockAction::IDockAction</c> (0x005533A2..0x005533B0: <c>[sp+0x44]</c>), which holds it at +0x95 as <c>DockWithObject</c> field 5 (M12-005).</param>
    public AlignWithObjectAction(ManipulationSystem m, uint objectId, float distanceMm, AlignmentType alignment, bool dockFlag95 = false) : base(m, objectId)
    {
        DistanceMm = distanceMm; Alignment = alignment; _dockFlag95 = dockFlag95;
        // 0x00553370: s16 starts at 0.0 (0x005533C8); the table at 0x005533E6 sets it for types 0..3; the clamp follows.
        float s16 = 0f;
        switch ((int)alignment)
        {
            case 0: s16 = Type0DistanceMm; break;
            case 2: s16 = Type2DistanceMm; break;
            case 3: s16 = distanceMm + Type3OffsetMm; break;               // vadd.f32 s16, s0, s2
        }
        if (s16 < ClampThresholdMm) s16 = 0f;                              // vcmpe.f32 s16, s0; vmovmi.f32 s16, s2 (s2 = 0.0)
        DockDistanceMm = s16;                                              // vstr s16, [r4, #0x9C]
    }

    /// <summary>The constructor's float argument (r3, moved to <c>s0</c> at 0x00553406).</summary>
    public float DistanceMm { get; }
    public AlignmentType Alignment { get; }

    /// <summary>
    /// +0xBB, <c>DockWithObject</c> field 7: the engine's constructor leaves it 0 and alignment type 1
    /// writes 2 (0x005533F4/0x005533F6, M13-016).
    /// </summary>
    protected override DockingMethod DockingMethod => (int)Alignment == 1 ? DockingMethod.Method2 : DockingMethod.Default;

    /// <summary><c>DockWithObject</c> field 5, the constructor's last bool (+0x95).</summary>
    protected override bool DockFlag95 => _dockFlag95;

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

    /// <summary>The distance the dock is asked for (+0x9C): after the alignment offset and the clamp, in binary32.</summary>
    public float DockDistanceMm { get; }

    protected override PreActionType PreActionType => (PreActionType)AlignmentPreActionType;
    // fidelity: M13-016
    /// <summary>
    /// <c>AlignWithObjectAction::SelectDockAction</c> 0x005534CC..0x005534DE: the dock action byte +0x80 is 0xB when the alignment type byte at +0xF8 (stored by the constructor at 0x005533D0)
    /// is 1 (<see cref="AlignmentType.LiftPlate"/>), and 0xA otherwise; it returns 0. Not in M13-016's text, read from the binary at the manager's instruction.
    /// </summary>
    protected override DockAction? SelectDockAction(ObservableObject target) => (byte)Alignment == 1 ? DockAction.AlignSpecial : DockAction.Align;
    protected override (double X, double Y, double Angle) PlacementOffset => (DockDistanceMm, 0, 0);

    /// <summary>
    /// <c>AlignWithObjectAction::Verify</c> 0x005534E0, as a function of what it reads: the dock action byte +0x80, the two bytes at <c>[this+0xCC]+4</c> and
    /// <c>+5</c>, and <c>PathComponent::IsActive()</c>. A dock action other than 0xA or 0xB (<c>(a &amp; 0xFE) == 0xA</c>, 0x005534EA..0x005534EE) warns
    /// "AlignWithObjectAction.Verify.ReachedDefaultCase" and fails 0x0300001A (0x00553596..0x00553598); otherwise byte +4 NON-ZERO gives 0x04000003 (0x005534FC..0x00553504 with
    /// <c>r5 = 0x04000003</c>; zero falls through), an active path gives 0x04000002 (0x0055350A..0x00553512), byte +5 clear gives 0x04000003 (0x00553518..0x0055351C) and otherwise it logs
    /// "Align with object SUCCEEDED!" and returns 0.
    /// </summary>
    // fidelity: M13-016
    public static uint VerifyResult(byte dockAction, byte resultByte4, byte resultByte5, bool pathActive, out string? log)
    {
        log = null;
        if ((dockAction & 0xFE) != 0xA) { log = "AlignWithObjectAction.Verify.ReachedDefaultCase"; return 0x0300001A; }
        if (resultByte4 != 0) return 0x04000003;                                      // 0x00553500 cmp r0,#0; mov r0,r5; bne 0x0055359C: NONZERO returns 0x04000003
        if (pathActive) return 0x04000002;
        if (resultByte5 == 0) return 0x04000003;
        log = "Align with object SUCCEEDED!";
        return 0;
    }

    /// <summary>
    /// The live <c>Verify</c> keeps the stack's earlier mapping: the engine's inputs are <c>[this+0xCC]+4</c> and <c>+5</c>, written by the 0xC5 handler (M12-034, unread), so there is no
    /// source for them here; <see cref="VerifyResult"/> is the engine's function over them.
    /// </summary>
    protected override ActionResult Verify(ObservableObject? target, DockResult result)
    {
        SteppedBehavior.ReportMissing("AlignWithObjectAction::Verify 0x005534E0 inputs [[this+0xCC]+4] and [[this+0xCC]+5] (written by the unread 0xC5 handler, M12-034) have no counterpart in this stack; the live Verify keeps Success/Retry from DockResult.Succeeded (the engine's results 0x04000003/0x04000002/0x0300001A are in VerifyResult only)");
        return result.Succeeded ? ActionResult.Success : ActionResult.Retry;
    }
}

/// <summary>
/// One step of a tick-driven sub-action: the engine's <c>Init</c> (a result, 0 = go on) and <c>CheckIfDone</c> (0, RUNNING 0x01000000 or a failure). The straight drive's
/// body is not in the M13 inventory (<c>DriveStraightAction::Init</c>/<c>CheckIfDone</c>, PLT 0x4AB590 and its base), so the live implementation
/// (<see cref="HostDriveStraightTick"/>) wraps this stack's own async <see cref="DriveStraightAction"/> and reports RUNNING until it ends.
/// </summary>
public interface IDriveStraightTick
{
    uint Init();
    uint CheckIfDone();
    void Cancel();
}

/// <summary>The stack's async <see cref="DriveStraightAction"/> as a tick-driven sub-action (see <see cref="IDriveStraightTick"/>).</summary>
public sealed class HostDriveStraightTick : IDriveStraightTick
{
    private readonly ManipulationSystem _m;
    private readonly float _distanceMm, _speedMmps;
    private readonly CancellationToken _parent;
    private CancellationTokenSource? _cts;
    private Task<ActionResult>? _task;

    public HostDriveStraightTick(ManipulationSystem m, float distanceMm, float speedMmps, CancellationToken parent = default)
    { _m = m; _distanceMm = distanceMm; _speedMmps = speedMmps; _parent = parent; }

    public uint Init()
    {
        SteppedBehavior.ReportMissing("DriveStraightAction::Init 0x005475D8 and CheckIfDone 0x005478C0 are not transliterated: this stack's drive ends on the path's own 'completed' event (HostDriveStraightTick), not on the engine's own conditions");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(_parent);
        _task = RunLineAsync(_cts.Token);
        return 0;
    }

    /// <summary>
    /// <see cref="DriveStraightAction.RunAsync"/>'s line, without its host wait of 3 + |d|/|v| * 2 s (that bound is this stack's, not the engine's): the engine ends a drive only by its own
    /// <c>CheckIfDone</c> or by the IAction timeout slot, which the owner of this tick applies on the engine clock (30.0f by default, 5.0f for <c>BackupOntoChargerAction</c>).
    /// </summary>
    private async Task<ActionResult> RunLineAsync(CancellationToken cancel)
    {
        var robot = _m.RobotPose();
        if (robot is null) return ActionResult.Abort;
        double h = robot.Value.AngleAroundZ;
        var to = robot.Value.Translation + new Vec3(Math.Cos(h) * _distanceMm, Math.Sin(h) * _distanceMm, 0);
        var p = PathMotionProfile.Default;
        float speed = _distanceMm < 0 ? -Math.Abs(_speedMmps) : Math.Abs(_speedMmps);
        using var run = _m.StartPath(new PathSegment[] { new PathSegment.Line(robot.Value.Translation.X, robot.Value.Translation.Y, to.X, to.Y, speed, p.AccelMmps2, p.DecelMmps2) });
        var ev = await run.WaitAsync(Timeout.InfiniteTimeSpan, cancel);
        return ev == PathEventType.Completed ? ActionResult.Success
             : ev is null ? ActionResult.CancelledWhileRunning
             : ActionResult.FailedTraversingPath;
    }

    public uint CheckIfDone()
    {
        if (_task is null) return (uint)ActionResult.Abort;
        if (!_task.IsCompleted) return (uint)ActionResult.Running;
        return _task.IsFaulted || _task.IsCanceled ? (uint)ActionResult.Abort : (uint)_task.Result;
    }

    public void Cancel() => _cts?.Cancel();
}

/// <summary>
/// The engine's <c>MountChargerAction</c> (0x0054E018, RobotActionType 0x11), read through. This is the engine's state machine over this stack's
/// async sub-actions: the engine's action list (<c>IActionRunner::Update</c>, <c>CompoundActionSequential</c>, the <c>IAction</c> timeouts) is not built
/// (M8), so each sub-action is run to its end by ticking its <c>Init</c>/<c>CheckIfDone</c> (the stack's pattern, M2-002) and a compound's failure is the
/// failing child's result (<c>AddAction(.., false, false)</c>, 0x0054E23E/0x0054E624 pass ignoreFailure = false).
///
/// <c>Init</c> 0x0054E0CC looks the object up (family 4); a missing one or one whose type is not 0xD (Charger) warns
/// "MountChargerAction.Init.InvalidCharger" and fails 0x03000004 (0x0054E12E..0x0054E170); otherwise it stores the charger id at robot+0x334
/// (0x0054E11C..0x0054E120) and builds the align compound.
///
/// <c>ConfigureAlignWithChargerAction</c> 0x0054E1C4 builds a sequence of:
/// <list type="number">
/// <item><c>AlignWithObjectAction(charger, 120.0f (0x42F00000), alignmentType 3, [this+0x81])</c>, <c>SetSpeed(30.0f)</c> (0x41F00000);</item>
/// <item><c>MoveHeadToAngleAction(Radians(0), Radians(0x3D0EFA35), Radians(0))</c> (the 2-degree tolerance).</item>
/// </list>
///
/// <c>CheckIfDone</c> 0x0054E2D0: the align compound's result, when it is 0, is destroyed and <c>ConfigureTurnAndMountAction</c> 0x0054E458 runs; a non-zero
/// result from it (0x03000004 when the charger is gone) is returned; the turn-and-mount compound's own failure is tested next (a result that is neither 0 nor
/// RUNNING): the charger's yaw less the robot's yaw as <c>Radians</c> (float subtraction and rescale), <c>getAbsoluteVal()</c> strictly greater than the float
/// 0x3FC90FDB (pi/2) - or a missing charger - warns "MountChargerAction.CheckIfDone.PositionForRetry" and builds the retry drive, whose result 0 becomes 0x04000006;
/// inside the window the failure stands.
///
/// <c>ConfigureTurnAndMountAction</c> builds, in one sequential compound (+0x56 = 1 on it):
/// <list type="number">
/// <item><c>TurnInPlaceAction(atan2f(v.y, v.x), absolute)</c> with <c>SetMaxSpeed(0x3FDF66F3)</c> and <c>SetAccel(0x40A78D36)</c>, where v is
/// <c>ComputeVectorBetween(robotPose, dockPoint)</c> (the robot minus the dock point, so v points away from the charger); the dock point is
/// <c>Pose3d(Radians(0), Z, (30, 0, 0))</c> pre-composed with the charger's pose. The turn goes out as <c>SetBodyAngle</c> (<c>MovementComponent::TurnInPlace</c>);</item>
/// <item><c>MoveLiftToHeightAction(45.0f, tolerance 5.0f, variability 0)</c>, but only when <c>Robot::GetLiftHeight()</c> read at configure time is
/// strictly below 45.0f (<c>vcmpe.f32</c> against 0x42340000, <c>bpl</c> at 0x0054E59E skips it). The 5.0 is the TOLERANCE (the constructor's third float), not a speed;</item>
/// <item><c>BackupOntoChargerAction(-120.0f (0xC2F00000), 30.0f (0x41F00000))</c>, a <c>DriveStraightAction</c> with its own <c>CheckIfDone</c> 0x0054E7A8.</item>
/// </list>
///
/// <b>The reverse does watch the contacts.</b> <c>BackupOntoChargerAction::CheckIfDone</c> 0x0054E7A8: on the contacts (robot+0x338) it calls
/// <c>Robot::SetPoseOnCharger()</c> and returns 0; a pitch below the float -0.261799 (0xBE860A92) returns 0x0400000A; otherwise the drive's own
/// <c>CheckIfDone</c> result when it is not 0 (RUNNING or a failure), and 0x04000006 when the drive is done off the contacts.
/// </summary>
// fidelity: M13-008, M13-012
public sealed class MountChargerAction
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    /// <summary>120.0f: the custom align distance, 0x42F00000 (0x0054E21C).</summary>
    public static readonly float AlignDistanceMm = F(0x42F00000);
    /// <summary>30.0f mm/s: IDockAction::SetSpeed, 0x41F00000 (0x0054E22A).</summary>
    public static readonly float AlignSpeedMmps = F(0x41F00000);
    /// <summary>The head move's tolerance, Radians(0x3D0EFA35) (2 degrees; movw 0xFA35 / movt 0x3D0E at 0x0054E26A..0x0054E270).</summary>
    public static readonly float HeadToleranceRad = F(0x3D0EFA35);
    /// <summary>45.0f mm (0x42340000): the height the lift is raised to when it is strictly below that (the literal compared at 0x0054E592, loaded by movt 0x4234 at 0x0054E5BA).</summary>
    public static readonly float LiftHeightForMountMm = F(0x42340000);
    /// <summary>
    /// 5.0f (0x40A00000, movt 0x40A0 at 0x0054E5BE): the <c>MoveLiftToHeightAction</c> constructor's THIRD float, its TOLERANCE in mm, not a speed (M4's
    /// <c>GameLiftToleranceMm</c> is the same 5.0f). The lift's speed and acceleration are the action constructor's defaults.
    /// </summary>
    public static readonly float LiftToleranceMm = F(0x40A00000);
    /// <summary>-120.0f (0xC2F00000, movt 0xC2F0 at 0x0054E5FC).</summary>
    public static readonly float MountDriveMm = F(0xC2F00000);
    /// <summary>30.0f mm/s (0x41F00000, movt 0x41F0 at 0x0054E600).</summary>
    public static readonly float MountSpeedMmps = F(0x41F00000);
    /// <summary>120.0f (0x42F00000, movt 0x42F0 at 0x0054E742).</summary>
    public static readonly float RetryDriveMm = F(0x42F00000);
    /// <summary>100.0f mm/s (0x42C80000, movt 0x42C8 at 0x0054E748).</summary>
    public static readonly float RetrySpeedMmps = F(0x42C80000);
    /// <summary>0x3FDF66F3 = 1.7453292608261108 rad/s = 100 deg/s (0x0054E550/0x0054E55A, M13-008).</summary>
    public static readonly float TurnMaxSpeedRadPerSec = F(0x3FDF66F3);
    /// <summary>0x40A78D36 = 5.235987663269043 rad/s^2 (0x0054E55E/0x0054E568, M13-008).</summary>
    public static readonly float TurnAccelRadPerSec2 = F(0x40A78D36);
    /// <summary>-15 degrees: BackupOntoChargerAction fails below this pitch (0xBE860A92 = -0.2617993950843811, 0x0054E7CC, M13-008).</summary>
    public static readonly float MaxBackupPitchRad = F(0xBE860A92);
    /// <summary>pi/2 (0x3FC90FDB, 0x0054E374): the window CheckIfDone uses to decide whether a failed mount is worth retrying.</summary>
    public static readonly float RetryHeadingWindowRad = F(0x3FC90FDB);
    /// <summary>The dock point's x on the charger (30.0f, 0x41F00000 at 0x0054E4BE/0x0054E4C6).</summary>
    public static readonly float DockPointXMm = F(0x41F00000);
    /// <summary>0x04000006: the retry drive's result (0x0054E3E8, M13-008).</summary>
    public const ActionResult RetryDriveResult = ActionResult.RetryDriveDone;

    private readonly ManipulationSystem _m;
    /// <param name="flag80">The constructor's fourth argument, stored at +0x80 and copied to the backup drive's byte +0x8B (0x0054E5F0/0x0054E60E). Its callers' values are not in the inventory.</param>
    /// <param name="flag81">The constructor's fifth argument, stored at +0x81 and passed on to the align action's last constructor bool (0x0054E20E..0x0054E216).</param>
    public MountChargerAction(ManipulationSystem m, uint chargerId, bool flag80 = false, bool flag81 = false)
    { _m = m; ChargerId = chargerId; Flag80 = flag80; Flag81 = flag81; }

    public uint ChargerId { get; }
    public bool Flag80 { get; }
    public bool Flag81 { get; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    /// <summary>
    /// Always one. The engine's action makes a single attempt and returns a retryable result; the loop,
    /// if there is to be one, belongs to whoever ran it.
    /// </summary>
    public int Attempts { get; private set; }

    /// <summary>Test seam: builds the backup drive (the engine's DriveStraightAction base); the default is <see cref="HostDriveStraightTick"/>.</summary>
    public Func<float, float, CancellationToken, IDriveStraightTick>? DriveFactory { get; set; }

    /// <summary>The engine clock the IAction timeouts read (<c>BaseStationTimer::GetCurrentTimeInSeconds</c>, a float, 0x00540D4E); a test seam.</summary>
    public Func<float>? Clock { get; set; }
    private float Now() => Clock?.Invoke() ?? _m.Robot.Engine.Timer.SecondsF;
    /// <summary>The IAction timeout slot (vtable +0x2c) of <c>MountChargerAction</c>, <c>TurnInPlaceAction</c>, <c>MoveHeadToAngleAction</c>, <c>MoveLiftToHeightAction</c>, <c>AlignWithObjectAction</c>/<c>IDockAction</c> and <c>DriveStraightAction</c>: the function 0x0052B0C2 (table entry 0x0052B0C3, Thumb), 30.0f.</summary>
    public const float DefaultActionTimeoutSec = 30f;
    /// <summary><c>BackupOntoChargerAction</c> overrides the slot: vtable 0x01021F80 + 8 + 0x2c = 0x0054E97B, body 0x0054E97A <c>movt r0, #0x40A0</c> = 5.0f.</summary>
    public const float BackupTimeoutSec = 5f;
    /// <summary>The mount's own start time (IAction +0x74 is set from the first Update, 0x00540D64); the whole action times out 30.0f later with 0x03000018.</summary>
    private float _wholeStart;
    private bool WholeTimedOut() => Now() >= _wholeStart + DefaultActionTimeoutSec;                  // 0x00540DA6 vcmpe s16, s0; bge

    /// <summary>The seconds left of the mount's own slot, as the timeout handed to a motion that stops itself on the engine clock (at least a millisecond).</summary>
    private TimeSpan RemainingForChild() => TimeSpan.FromSeconds(Math.Max(0.001, Math.Min(DefaultActionTimeoutSec, _wholeStart + DefaultActionTimeoutSec - Now())));

    /// <summary>
    /// One child of the mount run under the nested IAction timeouts: <c>IAction::UpdateInternal</c> tests <c>now &gt;= start + slot</c> on every Update before anything else (0x00540D90..0x00540DAA),
    /// the mount's own 30.0f slot first and then the child's (0x0052B0C3 = 30.0f for AlignWithObjectAction, IDockAction, MoveHeadToAngleAction, MoveLiftToHeightAction). At a deadline the child is
    /// cancelled (a motion that cannot be cancelled also carries the remaining time as its own engine-clock timeout) and <c>TimedOut</c> is true, which the caller turns into 0x03000018.
    /// </summary>
    private async Task<(bool TimedOut, T? Value)> RunStage<T>(Func<CancellationToken, Task<T>> start, CancellationToken cancel, float slotSec)
    {
        float begin = Now();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        var task = start(cts.Token);
        while (!task.IsCompleted)
        {
            if (WholeTimedOut() || Now() >= begin + slotSec)
            {
                cts.Cancel();
                _ = task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                return (true, default);
            }
            await Task.WhenAny(task, Task.Delay(1, CancellationToken.None));
        }
        return (false, await task);
    }

    private IDriveStraightTick MakeDrive(float distanceMm, float speedMmps, CancellationToken cancel) =>
        DriveFactory?.Invoke(distanceMm, speedMmps, cancel) ?? new HostDriveStraightTick(_m, distanceMm, speedMmps, cancel);

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        Attempts = 1;
        _wholeStart = Now();
        // ---- Init 0x0054E0CC
        var charger = _m.World.GetLocatedObjectById(ChargerId);
        if (charger is null || charger.Type != ObjectType.Charger_Basic)
        {
            _trace.Add($"warning: MountChargerAction.Init.InvalidCharger: No charger object with ID {ChargerId} in block world!");
            return ActionResult.BadObject;                                            // 0x03000004 (0x0054E166..0x0054E16C)
        }
        SteppedBehavior.ReportMissing("MountChargerAction::Init stores the charger id at robot+0x334 (0x0054E11C..0x0054E120): no counterpart in this stack (its readers are not in the M13 inventory)");

        // ---- ConfigureAlignWithChargerAction 0x0054E1C4: [AlignWithObjectAction, MoveHeadToAngleAction]
        var align = new AlignWithObjectAction(_m, ChargerId, AlignDistanceMm, AlignmentType.Custom, Flag81)
            { Profile = PathMotionProfile.Default with { DockSpeedMmps = AlignSpeedMmps }, CheckPreActionPose = false };
        var (alignTimedOut, alignResult) = await RunStage(ct => align.RunAsync(ct), cancel, DefaultActionTimeoutSec);   // AlignWithObjectAction's own slot: 30.0f
        _trace.AddRange(align.Trace);
        if (alignTimedOut) { _trace.Add("MountChargerAction: the align timed out (30 s IAction slot 0x0052B0C3)"); return ActionResult.Timeout; }
        var a = alignResult;
        if (a != ActionResult.Success)
        {
            // CheckIfDone 0x0054E2E4: a non-zero compound result is returned and ConfigureTurnAndMountAction never runs (0x0054E2FA is reached only for 0)
            _trace.Add($"align with the charger: {a}");
            return a;
        }
        if (WholeTimedOut()) { _trace.Add("MountChargerAction: timed out (30 s IAction slot 0x0052B0C2)"); return ActionResult.Timeout; }
        var (headTimedOut, head) = await RunStage(_ => _m.Robot.Motion.SetHeadAngleAsync(0f, CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2,
                                                                                         timeout: RemainingForChild(), requireCalibration: false), cancel, DefaultActionTimeoutSec);
        if (headTimedOut) { _trace.Add("MountChargerAction: the head move timed out (30 s IAction slot 0x0052B0C3)"); return ActionResult.Timeout; }
        if (head!.Result != MotionResult.Acknowledged)
        {
            var failure = FlipBlockAction.LiftFailureResult(head);                    // the head move is the compound's second child: its failure is the compound's
            _trace.Add($"align with the charger: the head move failed ({head.Result}); {failure}");
            return (ActionResult)(uint)failure;
        }
        if (WholeTimedOut()) { _trace.Add("MountChargerAction: timed out (30 s IAction slot 0x0052B0C2)"); return ActionResult.Timeout; }

        // ---- ConfigureTurnAndMountAction 0x0054E458
        var (configured, turnAngle, liftBelow) = ConfigureTurnAndMount();
        if (configured != 0) return (ActionResult)configured;
        uint b = await RunTurnAndMountAsync(turnAngle, liftBelow, cancel);
        if (b == 0) { _trace.Add("MountChargerAction: mounted"); return ActionResult.Success; }

        // ---- CheckIfDone 0x0054E31A..: the failure path and the only place the pi/2 window is consulted
        if (InsideRetryWindow())
        {
            _trace.Add($"MountChargerAction: the mount failed ({(ActionResult)b}) within a right angle of the charger, so no retry drive");
            return (ActionResult)b;
        }
        _trace.Add($"warning: MountChargerAction.CheckIfDone.PositionForRetry: Turning and mounting the charger failed (action result = {(ActionResult)b}). Driving forward to position for a retry");
        // ConfigureDriveForRetryAction 0x0054E72C: DriveStraightAction(120.0f, 100.0f, false), +0x56 = 1
        var retry = MakeDrive(RetryDriveMm, RetrySpeedMmps, cancel);
        uint r = retry.Init();
        if (r == 0) r = await RunToEnd(retry.CheckIfDone, cancel, DefaultActionTimeoutSec);          // DriveStraightAction's own slot: 30.0f
        return r == 0 ? RetryDriveResult : (ActionResult)r;                           // 0x0054E3E8: a finished drive (0) becomes 0x04000006
    }

    /// <summary>
    /// <c>ConfigureTurnAndMountAction</c> 0x0054E458 up to the compound's contents: the charger must still be a charger (else 0x03000004 after the warning
    /// "MountChargerAction.ConfigureTurnAndMountAction.InvalidCharger", 0x0054E638..0x0054E672); the turn angle is <c>atan2f(v.y, v.x)</c> of the robot's
    /// position less the dock point's; and the lift test reads <c>Robot::GetLiftHeight</c> now.
    /// </summary>
    private (uint Result, float TurnAngle, bool LiftBelow) ConfigureTurnAndMount()
    {
        var charger = _m.World.GetLocatedObjectById(ChargerId);
        var robot = _m.RobotPose();
        if (charger is null || charger.Type != ObjectType.Charger_Basic || robot is null)
        {
            _trace.Add($"warning: MountChargerAction.ConfigureTurnAndMountAction.InvalidCharger: No charger object with ID {ChargerId} in block world!");
            return ((uint)ActionResult.BadObject, 0f, false);
        }
        // The stack's poses are double; the engine's are float, so each is rounded where the engine holds it: the dock point (the charger's pose composed with the
        // (30, 0, 0) pose) and the robot's position become floats, the difference and atan2f are float.
        var dock = charger.Pose.Compose(new Pose3d(Mat3.Identity, new Vec3(DockPointXMm, 0, 0))).Translation;
        float vx = (float)robot.Value.Translation.X - (float)dock.X;
        float vy = (float)robot.Value.Translation.Y - (float)dock.Y;
        float angle = MathF.Atan2(vy, vx);                                            // blx atan2f 0x0054E536
        bool liftBelow = _m.Robot.Sensors.LiftHeightMm is { } lift && lift < LiftHeightForMountMm;   // 0x0054E592..0x0054E59E: bpl skips when >= 45
        return (0, angle, liftBelow);
    }

    /// <summary>The sequential compound: [TurnInPlaceAction, (MoveLiftToHeightAction if the lift was below 45), BackupOntoChargerAction]; the first failing child's result is the compound's.</summary>
    private async Task<uint> RunTurnAndMountAsync(float turnAngle, bool liftBelow, CancellationToken cancel)
    {
        uint r = await RunTurnAsync(turnAngle, cancel);
        if (r != 0) { _trace.Add($"MountChargerAction: the turn ended with {(ActionResult)r}"); return r; }
        if (liftBelow)
        {
            // the lift goes UP to 45 when it is below it, to clear the charger (M13-012). The 5.0f is the tolerance, not a speed: no speed is passed.
            var (liftTimedOut, lift) = await RunStage(_ => _m.Robot.Motion.SetLiftHeightAsync(LiftHeightForMountMm, timeout: RemainingForChild(), requireCalibration: false), cancel, DefaultActionTimeoutSec);
            if (liftTimedOut) { _trace.Add("MountChargerAction: the lift move timed out (30 s IAction slot 0x0052B0C3)"); return (uint)ActionResult.Timeout; }
            if (lift!.Result != MotionResult.Acknowledged)
            {
                uint failure = (uint)FlipBlockAction.LiftFailureResult(lift);
                _trace.Add($"MountChargerAction: the lift move did not complete ({lift.Result}); {(ActionResult)failure}");
                return failure;
            }
        }
        return await RunBackupAsync(cancel);
    }

    /// <summary>
    /// <c>TurnInPlaceAction(angle, absolute)</c> run through its own <c>Init</c>/<c>CheckIfDone</c> (M13-022): the body turn goes out as <c>SetBodyAngle</c>
    /// and ends on the motor ack and the body in position. The action's IAction timeout is the engine's default (30 s, M4-016); no other limit is applied.
    /// </summary>
    private async Task<uint> RunTurnAsync(float angle, CancellationToken cancel)
    {
        var turn = new TurnInPlaceAction(new LivePanTiltRobot(_m), angle, isAbsolute: true);
        turn.SetMaxSpeed(TurnMaxSpeedRadPerSec);
        turn.SetAccel(TurnAccelRadPerSec2);
        turn.Log += l => _trace.Add(l);
        void OnMessage(RobotMessage msg) { if (msg is MotorActionAck ack) turn.HandleMotorActionAck(ack.ActionId); }
        _m.Robot.Message += OnMessage;
        try
        {
            uint init = turn.Init();
            if (init != 0) return init;
            return await RunToEnd(turn.CheckIfDone, cancel, DefaultActionTimeoutSec);
        }
        finally { _m.Robot.Message -= OnMessage; }
    }

    /// <summary><c>BackupOntoChargerAction::CheckIfDone</c> 0x0054E7A8 over a <c>DriveStraightAction(-120.0f, 30.0f, false)</c>.</summary>
    private async Task<uint> RunBackupAsync(CancellationToken cancel)
    {
        var drive = MakeDrive(MountDriveMm, MountSpeedMmps, cancel);
        uint init = drive.Init();
        if (init != 0) return init;
        try { return await RunToEnd(() => BackupCheckIfDone(drive), cancel, BackupTimeoutSec); }
        finally { drive.Cancel(); }
    }

    /// <summary>One <c>BackupOntoChargerAction::CheckIfDone</c> call (0x0054E7A8..0x0054E7EE).</summary>
    // fidelity: M13-008
    internal uint BackupCheckIfDone(IDriveStraightTick drive)
    {
        if (_m.Robot.Sensors.OnCharger)                                               // robot+0x338 (0x0054E7B0)
        {
            SteppedBehavior.ReportMissing("Robot::SetPoseOnCharger() 0x0054E7B8 (BackupOntoChargerAction::CheckIfDone on the contacts): its body is not in the M13 inventory and has no counterpart in this stack");
            _trace.Add("BackupOntoChargerAction: the contacts reported, so the reverse ends here");
            return 0;
        }
        if (_m.Robot.Sensors.PitchRad is { } pitch && pitch < MaxBackupPitchRad)       // vcmpe.f32 s0, s2; bpl: not below (or NaN) falls through
        {
            _trace.Add($"BackupOntoChargerAction: pitched to {pitch} rad, below {MaxBackupPitchRad}; 0x0400000A");
            return (uint)ActionResult.BackupPitchedTooFar;
        }
        uint r = drive.CheckIfDone();                                                 // DriveStraightAction::CheckIfDone 0x0054E7E4
        if (r != 0) return r;                                                         // movne r4, r0: RUNNING or the drive's failure
        _trace.Add("BackupOntoChargerAction: the reverse finished off the contacts; 0x04000006");
        return (uint)ActionResult.RetryDriveDone;
    }

    /// <summary>
    /// The pi/2 test of <c>CheckIfDone</c> 0x0054E324..0x0054E380: <c>GetLocatedObjectByIdHelper</c> null goes straight to the retry; otherwise the charger's
    /// <c>GetAngleAroundZaxis</c> less the robot's (<c>Anki::operator-(Radians, Radians)</c> 0x0084CA18: a float subtraction, then rescale) with
    /// <c>getAbsoluteVal()</c> compared by <c>vcmpe.f32</c>/<c>ble</c>: at or below the float pi/2 the failure stands.
    /// </summary>
    internal bool InsideRetryWindow()
    {
        var obj = _m.World.GetLocatedObjectById(ChargerId);
        var robot = _m.RobotPose();
        if (obj is null || robot is null) return false;
        float chargerYaw = (float)EngineRadians.GetAngleAroundZaxis(obj.Pose.Rotation);
        float robotYaw = (float)EngineRadians.GetAngleAroundZaxis(robot.Value.Rotation);
        float diff = (float)EngineRadians.Rescale(chargerYaw - robotYaw);
        return !(MathF.Abs(diff) > RetryHeadingWindowRad);
    }

    /// <summary>
    /// Ticks <paramref name="checkIfDone"/> until it is not RUNNING. <c>IAction::UpdateInternal</c> 0x00540D1C tests the timeout before <c>CheckIfDone</c> (0x00540D90..0x00540DAA): with the start
    /// time stored at the first Update (0x00540D64), <c>now &gt;= start + GetTimeoutInSeconds()</c> in float fails 0x03000018 (0x00540E7C..0x00540E86). The mount's own 30.0f slot is tested first
    /// on every tick, as the parent's Update precedes its children's.
    /// </summary>
    private async Task<uint> RunToEnd(Func<uint> checkIfDone, CancellationToken cancel, float timeoutSec)
    {
        float start = Now();
        while (true)
        {
            float now = Now();
            if (WholeTimedOut()) return (uint)ActionResult.Timeout;
            if (now >= start + timeoutSec) return (uint)ActionResult.Timeout;
            uint r = checkIfDone();
            if (r != (uint)ActionResult.Running) return r;
            if (cancel.IsCancellationRequested) return (uint)ActionResult.CancelledWhileRunning;
            await Task.Delay(1, CancellationToken.None);
        }
    }

    /// <summary>
    /// What <see cref="TurnInPlaceAction"/> reads from the live robot (M13-022's <c>IPanTiltRobot</c>). <c>MovementComponent::TurnInPlace</c>'s body is unread (M13-021), so the send is the
    /// <c>SetBodyAngle</c> message the turn path of this stack already builds (<see cref="TurnTowardsPose.Message"/>, M11-014), with the shared action-id counter (M4-005).
    /// </summary>
    internal sealed class LivePanTiltRobot : IPanTiltRobot
    {
        private readonly ManipulationSystem _m;
        public LivePanTiltRobot(ManipulationSystem m) => _m = m;
        public bool OnTreads => _m.Robot.Sensors.OffTreadsState == OffTreadsState.OnTreads;
        public uint OriginId => _m.Robot.Engine.Robot?.CurrentOriginId ?? 0;
        public float PoseAngleAroundZ => _m.RobotPose() is { } p ? (float)EngineRadians.GetAngleAroundZaxis(p.Rotation) : 0f;
        public float HeadAngle => _m.Robot.Motion.HeadAngleRad;
        public bool BodyMoving => _m.Robot.Motion.BodyMoving;
        public bool HeadMoving => _m.Robot.Motion.HeadMoving;

        public uint TurnInPlace(float targetRad, float speed, float accel, float toleranceRad, ushort numHalfRevolutions, bool isAbsolute, out byte actionId)
        {
            actionId = _m.Robot.Motion.NextActionId();
            var msg = TurnTowardsPose.Message(targetRad, speed, accel, toleranceRad, numHalfRevolutions, isAbsolute, actionId);
            return _m.Robot.SendMessage(msg, flush: true) ? 0u : PanTiltResult.SendMessageToRobotFailed;
        }

        public uint MoveHeadToAngle(float angleRad, float speed, float accel, float durationSec, out byte actionId) =>
            throw new NotSupportedException("The mount sends its head move through CozmoMotion.SetHeadAngleAsync (M4); MovementComponent::MoveHeadToAngle's body is unread (M13-021).");
    }
}

/// <summary>
/// The engine's <c>DriveOffChargerContactsAction</c> (ctor 0x00558228, which takes the robot only): a <c>DriveStraightAction</c>
/// constructed with <b>10.0f mm at 20.0f mm/s, false</b> (0x00558232/0x00558236/0x0055823E), with +0x44 = 7
/// (0x00558276/0x00558278), which is <c>IActionRunner</c>'s <c>RobotActionType</c>
/// <b>DRIVE_OFF_CHARGER_CONTACTS</b> (RobotActionTypeFromString 0x0075A448), and, <b>in SDK mode only</b>,
/// <c>SetTracksToLock(0)</c> in the constructor (<c>CozmoContext::IsInSdkMode</c> 0x0055827C, <c>bne</c> at 0x00558282, the call at 0x00558288). The behaviour
/// <c>BehaviorDriveOffCharger</c> does not use this action: it starts a plain <c>DriveStraightAction</c> (0x005C0C08, M13-017).
///
/// <c>Init</c> 0x005582D0 copies the robot's on-contacts flag (robot+0x338) into action+0x8B and, when it is set, tail-calls
/// <c>DriveStraightAction::Init</c> (<c>b.w</c> 0x005582DC); when it is clear it returns 0. <c>CheckIfDone</c> 0x005582E4 returns 0 when +0x8B is clear; otherwise
/// the base <c>CheckIfDone</c>'s RUNNING is returned unchanged and every other result of the drive (success or failure) is IGNORED: the action then fails
/// <b>0x04000009</b> after the warning "DriveOffChargerContactsAction.CheckIfDone.StillOnCharger" if the robot is on the contacts now (0x00558308..0x00558344),
/// and returns 0 if it is not (0x0055830C..0x0055830E).
/// </summary>
// fidelity: M13-013
public sealed class DriveOffChargerContactsAction
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    /// <summary>The constructor's 10.0f (0x41200000 at 0x00558232).</summary>
    public static readonly float ConstructorDistanceMm = F(0x41200000);
    /// <summary>The constructor's 20.0f mm/s (0x41A00000 at 0x00558236).</summary>
    public static readonly float ConstructorSpeedMmps = F(0x41A00000);
    /// <summary>+0x44 = 7 = <c>RobotActionType::DRIVE_OFF_CHARGER_CONTACTS</c> (0x00558276/0x00558278).</summary>
    public const int RobotActionTypeDriveOffChargerContacts = 7;
    /// <summary>The warning's event name (0x0055837C).</summary>
    public const string StillOnChargerWarning = "DriveOffChargerContactsAction.CheckIfDone.StillOnCharger";

    private readonly ManipulationSystem _m;
    private readonly IDriveStraightTick _drive;
    /// <summary>The engine clock the IAction timeout reads (BaseStationTimer::GetCurrentTimeInSeconds, a float); a test seam.</summary>
    public Func<float>? Clock { get; set; }
    private float Now() => Clock?.Invoke() ?? _m.Robot.Engine.Timer.SecondsF;

    /// <param name="isInSdkMode"><c>CozmoContext::IsInSdkMode</c> (0x0055827C). This stack has no SDK-mode state, so a null seam reads false and is reported MISSING.</param>
    /// <param name="drive">The base <c>DriveStraightAction</c> (Init/CheckIfDone); the default wraps this stack's async drive of 10 mm at 20 mm/s.</param>
    public DriveOffChargerContactsAction(ManipulationSystem m, Func<bool>? isInSdkMode = null, IDriveStraightTick? drive = null, CancellationToken cancel = default)
    {
        _m = m;
        _drive = drive ?? new HostDriveStraightTick(m, ConstructorDistanceMm, ConstructorSpeedMmps, cancel);
        bool sdk;
        if (isInSdkMode is null)
        {
            SteppedBehavior.ReportMissing("CozmoContext::IsInSdkMode (DriveOffChargerContactsAction constructor 0x0055827C): this stack has no SDK-mode state, so the SDK-only SetTracksToLock(0) is not made");
            sdk = false;
        }
        else sdk = isInSdkMode();
        // SetTracksToLock(0) is IActionRunner's local field write (0x00558288), made only in SDK mode; this stack has no track-lock field on its async actions.
        TracksToLockCleared = sdk;
    }

    /// <summary>Whether the constructor's SDK-only <c>SetTracksToLock(0)</c> ran.</summary>
    public bool TracksToLockCleared { get; private set; }
    /// <summary>action+0x8B: the robot's on-contacts flag captured at <c>Init</c>.</summary>
    public bool WasOnContactsAtInit { get; private set; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    /// <summary><c>Init</c> 0x005582D0.</summary>
    public uint Init()
    {
        WasOnContactsAtInit = _m.Robot.Sensors.OnCharger;       // ldrb robot+0x338 -> action+0x8B
        if (!WasOnContactsAtInit) return 0;                     // cbz -> movs r0,#0
        return _drive.Init();                                   // b.w DriveStraightAction::Init
    }

    /// <summary><c>CheckIfDone</c> 0x005582E4.</summary>
    public uint CheckIfDone()
    {
        if (!WasOnContactsAtInit) return 0;                     // 0x005582EA..0x00558302
        uint r = _drive.CheckIfDone();
        if (r == (uint)ActionResult.Running) return r;          // cmp r0,#0x1000000: RUNNING is returned as it is
        if (_m.Robot.Sensors.OnCharger)                         // robot+0x338 now (0x00558308)
        {
            _trace.Add($"warning: {StillOnChargerWarning}");
            return (uint)ActionResult.StillOnCharger;           // 0x04000009
        }
        return 0;                                               // the drive's own result is not looked at
    }

    /// <summary>Init, then CheckIfDone on every tick until it is not RUNNING.</summary>
    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        uint r = Init();
        if (r != 0) return (ActionResult)r;
        float start = Now();                                    // IAction +0x74, set at the first Update (0x00540D64)
        try
        {
            while (true)
            {
                // IAction::UpdateInternal's timeout test precedes CheckIfDone (0x00540DA6): the slot is DriveOffChargerContactsAction's vtable +0x2c = 0x0052B0C2, 30.0f
                if (Now() >= start + 30f) return ActionResult.Timeout;
                r = CheckIfDone();
                if (r != (uint)ActionResult.Running) return (ActionResult)r;
                if (cancel.IsCancellationRequested) return ActionResult.CancelledWhileRunning;
                await Task.Delay(1, CancellationToken.None);
            }
        }
        finally { _drive.Cancel(); }
    }
}
