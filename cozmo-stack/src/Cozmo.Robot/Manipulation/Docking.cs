using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary><c>Anki::Cozmo::DockAction</c> (UNITY enum): what the robot's firmware does once the marker is in reach.</summary>
public enum DockAction : byte
{
    PickupLow = 0, PickupHigh = 1, PlaceHigh = 2, PlaceLow = 3, PlaceLowBlind = 4, RollLow = 5, DeepRollLow = 6, PostDockRoll = 7,
    FacePlant = 8, PopAWheelie = 9, Align = 10, AlignSpecial = 11, RampAscend = 12, RampDescend = 13, CrossBridge = 14,
}

/// <summary>
/// The docking method the engine sends in <c>DockWithObject</c> field 7, held at <c>IDockAction</c> +0xBB
/// and named by <c>DriveToPickupObjectAction::SetDockingMethod</c> 0x0055C5A2.
///
/// Only two values are observed in shipped code: the constructor leaves 0, and
/// <c>PickupObjectAction</c> 0x005536EA writes 2. The names of the values are not established, so they
/// are numbered; <c>AlignWithObjectAction</c>, <c>PlaceRelObjectAction</c> and <c>RollObjectAction</c>
/// each write this field too.
/// </summary>
public enum DockingMethod : byte { Default = 0, Method1 = 1, Method2 = 2, Method3 = 3 }

// fidelity: M2-014
/// <summary>
/// <c>PickAndPlaceResult.blockStatus</c>: 0 NO_BLOCK, 1 BLOCK_PLACED, 2 BLOCK_PICKED_UP. The names and values are
/// the engine's <c>EnumToString(BlockStatus)</c> 0x007C168C, whose table at 0x01034984 points to "NO_BLOCK",
/// "BLOCK_PLACED", "BLOCK_PICKED_UP"; <c>HandlePickAndPlaceResult</c> branches on +6 == 2 to its BlockPickedUp
/// log (0x005337A4) and on +6 == 1 to its BlockPlaced log (0x005337A8).
/// </summary>
public enum BlockStatus : byte { NoBlock = 0, BlockPlaced = 1, BlockPickedUp = 2 }

/// <summary>
/// The robot's report at the end of a dock: <c>PickAndPlaceResult {u32 timestamp, bool success, i8 DockingResult,
/// u8 blockStatus}</c> (Unpack 0x007C1AC6, <c>Read&lt;bool&gt;</c> 0x007C1ADA; the handler reads +5 with ldrsb).
/// </summary>
public sealed record DockResult(uint Timestamp, bool Succeeded, sbyte DockingResult, BlockStatus Status)
{
    public override string ToString() => $"{(Succeeded ? "succeeded" : "failed")} result={DockingResult} status={Status} t={Timestamp}";
}

/// <summary>
/// <c>MoveLiftToHeightAction::Preset</c>: LowDock, HighDock, HeightCarry, OutOfFOV (names and order from
/// <c>GetPresetName</c> 0x00548CD4: 0 LowDock, 1 HighDock, 2 HeightCarry, 3 OutOfFOV).
///
/// NATIVE heights, from the preset table at 0x00C54688: 32, 76, 92 and −1. This is the reading the source
/// fidelity sweep settled (CONTROL_LAYER.md "Errata", SOURCE_FIDELITY_AUDIT §"lift limits"); M12 regressed it
/// to 32 / 92 / 72 by taking the lift range limits for the dock heights and a later open-source constant for
/// the carry height. HighDock is 76 mm, and the carry height is 92 mm — which is what
/// <see cref="FlipBlockAction"/> raises the lift to, so the wrong value went straight into M13.
///
/// OutOfFOV is unresolved: the table's fourth entry is −1, which is not a height, and no other constant was
/// found. The carry height stands in for it and says so.
/// </summary>
public static class LiftPresets
{
    public const float LowDockMm = 32f;
    public const float HighDockMm = 76f;
    public const float CarryMm = 92f;
    /// <summary>Unresolved (the table holds −1); the carry height stands in.</summary>
    public const float OutOfFovMm = CarryMm;
}

/// <summary>
/// The engine's <c>CarryingComponent</c>: which object is on the lift, and where the lift is holding it.
/// <c>SetDockObjectAsAttachedToLift</c> runs when a pick-up result reports <c>BlockPickedUp</c>,
/// <c>SetCarriedObjectAsUnattached</c> when a place reports <c>BlockPlaced</c>
/// (<c>HandlePickAndPlaceResult</c>).
///
/// The engine keeps the object in its pose tree, hanging off the lift, so it moves with the robot without
/// anyone recomputing it. This stack has no pose tree, so the same chain is composed on demand - see
/// <see cref="LiftGeometry"/>, which holds every link of it with the address it came from.
/// </summary>
public sealed class CarryingComponent
{
    private readonly object _gate = new();
    private uint? _carried;
    private KnownMarker? _dockMarker;

    public bool IsCarryingObject { get { lock (_gate) return _carried is not null; } }
    public uint? CarriedObjectId { get { lock (_gate) return _carried; } }
    /// <summary>The marker the object was picked up by; the engine measures the hold from it.</summary>
    public KnownMarker? DockMarker { get { lock (_gate) return _dockMarker; } }
    public event Action<uint?>? Changed;

    public bool IsCarrying(uint objectId) { lock (_gate) return _carried == objectId; }

    public void SetCarrying(uint objectId, KnownMarker? dockMarker = null)
    {
        lock (_gate) { _carried = objectId; _dockMarker = dockMarker; }
        Changed?.Invoke(objectId);
    }

    /// <summary>
    /// <c>SetObjectAsAttachedToLift</c>'s state (M12-008): <c>SetCarryingObject</c> plus the object's pose with respect to the lift
    /// (step 6, rotation kept) and <c>this+0x14</c>, the object resting on the carried one (step 8; null is the engine's -1) with its
    /// pose with respect to the carried object.
    /// </summary>
    // fidelity: M12-008
    internal void AttachToLift(uint objectId, KnownMarker dockMarker, Pose3d objectWrtLift, uint? onTopId, Pose3d? topWrtCarried)
    {
        lock (_gate) { _carried = objectId; _dockMarker = dockMarker; _objectWrtLift = objectWrtLift; _onTopId = onTopId; _topWrtCarried = topWrtCarried; }
        Changed?.Invoke(objectId);
    }

    private Pose3d? _objectWrtLift;
    private uint? _onTopId;
    private Pose3d? _topWrtCarried;
    /// <summary>The carried object's pose with respect to the lift pose (translation <c>(L + 4, 0, -12.5)</c>, rotation kept); null before <c>AttachToLift</c>.</summary>
    public Pose3d? ObjectWrtLift { get { lock (_gate) return _objectWrtLift; } }
    /// <summary><c>CarryingComponent+0x14</c>: the object found resting on the carried one when it was attached; null is the engine's -1.</summary>
    public uint? CarriedOnTopId { get { lock (_gate) return _onTopId; } }
    /// <summary>The on-top object's pose with respect to the carried object: the engine parents it to the carried object (<c>SetParent(obj.pose)</c>, 0x00633006..0x00633010), so it moves with it.</summary>
    public Pose3d? TopWrtCarried { get { lock (_gate) return _topWrtCarried; } }

    public void UnsetCarrying() { lock (_gate) { _carried = null; _dockMarker = null; _objectWrtLift = null; _onTopId = null; _topWrtCarried = null; } Changed?.Invoke(null); }
}

/// <summary>
/// The engine's <c>DockingComponent</c>: the firmware docks; the engine tells it to start
/// (<c>DockWithObject</c> 0x42) and then, for every camera frame, sends where the dock marker is relative to
/// the robot (<c>DockingErrorSignal</c> 0x48, <c>UpdateDockingErrorSignal</c> 0x0063BE80) until the robot
/// reports <c>PickAndPlaceResult</c> (0xB8). NATIVE from 0x0063C0B6..0x0063C1F0: the marker's pose is taken with
/// respect to the robot pose at the frame's timestamp, clamped flat within 40 degrees (0x3F32B8C2), and the
/// signal is {x = pose.x − placementOffsetX, y = pose.y + placementOffsetY, z = pose.z, angle = yaw + π/2 +
/// placementOffsetAngle, timestamp}; the signal is skipped while the body rotated faster than 22.9 deg/s
/// (0x41B758B4) around the frame time. Message field order is the engine's packing (0x0063BD50 / 0x0063C548);
/// Every field of <c>DockWithObject</c> is now read from the engine rather than guessed - see
/// <see cref="Message"/> - including the two words that are genuinely zero there. So is the error
/// signal's, and its timestamp turned out to come first: the name table had matched the 16-byte
/// <c>VizInterface::DockingErrorSignal</c> by prefix, which shifted every field of this 22-byte message
/// by one word. Its last two bytes the builder never writes at all.
/// </summary>
public sealed class DockingSystem : IDisposable
{
    public const double ClampToFlatAngleRad = 0.698132;
    public const double RotatingTooFastRadPerSec = 22.9183 * Math.PI / 180;

    private readonly CozmoRobot _robot;
    private readonly VisionSystem _vision;
    private readonly object _gate = new();
    private TaskCompletionSource<DockResult>? _pending;
    private (uint ObjectId, KnownMarker Marker, double OffX, double OffY, double OffAngle, DockAction Action)? _active;

    // fidelity: M4-009
    /// <summary>
    /// DockingComponent+0xC (C11.1 D1..D3): the engine ObjectID of the object the robot is currently docking
    /// with. Default −1 (represented as null); the only writer is <c>DockWithObject</c>; <c>AbortDocking</c>
    /// does not reset it. The Moved (CD10a) and Stopped (CD10b) broadcasts exclude a cube whose id equals it.
    /// </summary>
    public uint? DockTargetObjectId { get; private set; }

    public DockingSystem(CozmoRobot robot, VisionSystem vision)
    {
        _robot = robot; _vision = vision;
        robot.Message += OnMessage;
        vision.FrameProcessed += OnFrame;
        // Everything this instance installs on the vision system is remembered, so Dispose can take back
        // what it put there - and only what it put there. A replacement installs its own and owns them
        // from that moment; a late disposal of the old one must not clear the new one's.
        _isCarrying = Carrying.IsCarrying;               // the guard at 0x00534116 needs this
        vision.IsCarryingObject = _isCarrying;
        // A delocalization forgets what was located in the origin that has gone, except what the robot is
        // holding: Robot::Delocalize moves the carried objects into the new origin instead (0x00510CF0).
        _carriedObjects = () => Carrying.CarriedObjectId is { } id
            ? new HashSet<uint> { id } : new HashSet<uint>();
        vision.CarriedObjects = _carriedObjects;
        // The engine parents the carried object to the lift, so it follows for free; here the chain is
        // recomposed whenever a new state arrives.
        _carriedPose = _ => UpdateCarriedObjectPose();
        vision.FrameProcessed += _carriedPose;
    }

    private readonly Func<uint, bool> _isCarrying;
    private readonly Func<IReadOnlySet<uint>> _carriedObjects;
    private readonly Action<VisionFrameResult> _carriedPose;

    public CarryingComponent Carrying { get; } = new();
    public List<RobotMessage> Sent { get; } = new();
    public int ErrorSignalsSent { get; private set; }
    public event Action<string>? Log;
    /// <summary>Raised when the robot reports it is moving the lift after a dock (<c>MovingLiftPostDock</c>).</summary>
    public event Action<bool>? MovingLiftPostDock;
    /// <summary>
    /// Raised when the robot reports a <c>LiftLoad</c> (0xDA) while a dock is running. The engine registers a
    /// handler for this tag in <c>IDockAction::Init</c> 0x00551750; its body is the action's own state machine,
    /// so the stack only receives it (M12-017).
    /// </summary>
    public event Action<bool>? LiftLoad;

    private void Send(RobotMessage m) { Sent.Add(m); _robot.SendMessage(m, flush: true); }

    /// <summary>
    /// Puts the carried object where the lift is holding it, composing the chain the engine keeps as a
    /// pose tree: robot origin, lift pivot, arm, object. See <see cref="LiftGeometry"/>.
    ///
    /// The engine never recomputes this - the object is parented to the lift, so it follows for free -
    /// which is why this is called whenever the robot pose or the lift angle moves as well as on attach.
    /// </summary>
    /// <summary>
    /// Lets go of the carried object, as <c>CarryingComponent::SetCarriedObjectAsUnattached(bool)</c>
    /// 0x006333D4 does.
    ///
    /// The object keeps the pose it was being held at: the engine takes that pose with respect to the
    /// robot and hands it to <c>ObjectPoseConfirmer::AddRobotRelativeObservation(object, pose,
    /// PoseState 2)</c> at 0x00633458, so it stays located, and Dirty, exactly where the lift left it.
    /// It is not forgotten and it is not left Known: it is somewhere the robot put down and has not
    /// looked at since.
    ///
    /// <paramref name="forget"/> is the engine's argument, and it decides whether that survives. With it
    /// set, 0x00633930 also runs <c>BlockWorld::DeleteLocatedObjects</c> and the object goes. A put-down
    /// passes false (<c>BehaviorPutDownBlock</c> 0x005C84CE); a pick-up that turned out to have failed
    /// passes true (<c>PickupObjectAction::Verify</c> 0x00553CAA and 0x00553D2A), because then nobody
    /// knows where the cube is.
    /// </summary>
    public void ReleaseCarriedObject(bool forget = false)
    {
        // fidelity: M12-006
        if (Carrying.CarriedObjectId is { } id)
        {
            if (forget) _vision.World.MarkUnknown(id);
            else { UpdateCarriedObjectPose(); _vision.World.MarkReleased(id); }
        }
        Carrying.UnsetCarrying();
    }

    public void UpdateCarriedObjectPose()
    {
        if (Carrying.CarriedObjectId is not { } id || Carrying.DockMarker is not { } marker) return;
        if (_vision.World.GetObjectById(id) is not { } obj) return;
        if (_vision.History.Latest is not { } state) return;
        // fidelity: M12-008
        // The pose it was attached at keeps its rotation (SetObjectAsAttachedToLift step 6); an object marked as carried
        // without an attach has only the marker to go by.
        var carriedPose = Carrying.ObjectWrtLift is { } wrtLift
            ? LiftGeometry.CarriedObjectWorldPose(state.RobotPose, state.LiftAngleRad, wrtLift)
            : LiftGeometry.CarriedObjectWorldPose(state.RobotPose, state.LiftAngleRad, marker);
        _vision.World.SetCarriedPose(id, carriedPose);
        if (Carrying.CarriedOnTopId is { } topId && Carrying.TopWrtCarried is { } topWrt)
            _vision.World.SetCarriedPose(topId, carriedPose.Compose(topWrt));
    }

    /// <summary>The <see cref="BlockConfigurationManager"/> whose dirty flag (<c>+0xC</c>) <c>SetObjectAsAttachedToLift</c> sets (step 9); wired by <see cref="ManipulationSystem"/>.</summary>
    public BlockConfigurationManager? Configurations { get; set; }

    /// <summary>
    /// The value the last pick-up's <c>SetObjectAsAttachedToLift</c> returned (M12-008); 0 is success. The engine discards it (0x00533848..0x00533854); this property only records it.
    /// </summary>
    public uint LastAttachResult { get; private set; }

    /// <summary>
    /// <c>DockingComponent+5</c>: the PickAndPlaceResult <c>success</c> byte <c>HandlePickAndPlaceResult</c> 0x00533780 stores before it branches on blockStatus
    /// (0x00533790..0x0053379A, M12-008). No reader is in the inventory.
    /// </summary>
    // fidelity: M12-008
    public bool DockingSuccessByte { get; private set; }

    /// <summary>
    /// <c>DockingComponent::CanStackOnTopOfObject</c> 0x0063C5C4 (M12-012) = <see cref="CanInteractWithObjectHelper"/> 0x0063C654 and then
    /// <c>!IsPoseTooHigh(pose, 1.0, 15.0, 0.5)</c> (1.0 at 0x0063C614, 15.0 at 0x0063C60E, 0.5 at 0x0063C606, call 0x0063C618); it does not reuse the helper's found-object result.
    /// <c>IsPoseTooHigh</c> (0x00877955) is given the object's pose WITH RESPECT TO THE ROBOT'S POSE (the helper's out pose), so its z is the height above the robot's z, not the world z;
    /// the two differ once the robot's z is not 0. Without a robot pose the <c>GetWithRespectTo</c> fails and the answer is false (as the helper's own failure).
    /// </summary>
    // fidelity: M12-012
    public bool CanStackOnTopOfObject(ObservableObject obj)
    {
        if (!CanInteractWithObjectHelper(obj)) return false;
        if (_vision.History.Latest is not { } state) return false;
        double d = CubeGeometry.DimInParentFrameZ(obj);
        double zWrtRobot = obj.Pose.WithRespectTo(state.RobotPose).Translation.Z;
        return !(d * 1.0 + 15.0 + 1e-5 < d * 0.5 + zWrtRobot);      // ObservableObject::IsPoseTooHigh 0x00877954 (M12-012 C-E4)
    }

    /// <summary>
    /// <c>DockingComponent::CanInteractWithObjectHelper</c> 0x0063C654..0x0063C794 (M12-012; re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q7, corrected by
    /// 20260929-R-VIS-verify-M12-fix2.md): it reads only the object's family (1 or 2: Block or LightCube), <c>IsRestingFlat(Radians(0x3E32B8C2))</c>, the carried compare
    /// (<c>[[robot+0x284]+8] != id</c>, 0x0063C686..0x0063C698), <c>GetWithRespectTo(robot pose)</c> (always succeeds with one pose origin) and finally
    /// <c>FindObjectOnTopOrUnderneathHelper(obj, 15.0, default filter, onTop = 1) == null</c> (0x0063C6C0..0x0063C730, 0x0063C78A..0x0063C792; the search is M13-007's over the located
    /// cubes, with M13-023's stand-in for tilted/non-cube pairs). NO PoseState test exists in the unit. <c>CanPickUpObject</c> 0x0063C7F0 and <c>CanPickUpObjectFromGround</c> 0x0063C880
    /// are recorded (M12-012) but NO C# caller exists, so they are not built.
    /// </summary>
    // fidelity: M12-012
    public bool CanInteractWithObjectHelper(ObservableObject obj)
    {
        if (obj.Family is not (ObjectFamily.Block or ObjectFamily.LightCube)) return false;
        if (!obj.IsRestingFlat(BitConverter.Int32BitsToSingle(0x3E32B8C2))) return false;   // Radians(0x3E32B8C2), cited above
        if (Carrying.CarriedObjectId == obj.ObjectId) return false;
        var cubes = _vision.World.LocatedObjects.Where(o => CubeGeometry.IsCube(o.Type)).ToList();
        return BlockConfigurationManager.FindObjectOnTopOrUnderneath(obj, cubes, onTop: true, BlockConfigurationManager.RestingOnToleranceMm) is null;
    }

    /// <summary><c>CarryingComponent::SetObjectAsAttachedToLift</c> 0x00632CC4 (M12-008): see <see cref="LiftGeometry.SetObjectAsAttachedToLift"/>. Returns 0 on success.</summary>
    // fidelity: M12-008
    public uint SetObjectAsAttachedToLift(uint? objectId, MarkerType markerCode) =>
        LiftGeometry.SetObjectAsAttachedToLift(Carrying, _vision.World, Configurations, _vision.History.Latest, objectId, markerCode, Log);

    /// <summary>
    /// The engine's message for a dock, field for field.
    ///
    /// The builder at 0x0063BD50 fills the 21 bytes from arguments <c>DockingComponent::DockWithObject</c>
    /// 0x0063BA44 hands it, and <c>IDockAction::CheckIfDone</c> 0x005521AC is the only thing that calls
    /// that. Following the three of them through gives every field a source:
    ///
    /// <list type="bullet">
    /// <item><b>0</b> — a literal zero. <c>movs r4, #0</c> / <c>str r4, [sp, #0x1c]</c>, and that local is
    ///   what the builder dereferences into the first word. The engine has no other path here, so this
    ///   word is zero on every dock the app has ever sent.</item>
    /// <item><b>1, 2, 3</b> — speed, acceleration, deceleration, from <c>IDockAction</c> +0xAC, +0xB0 and
    ///   +0xB4. Named by their setters: <c>SetSpeed</c> writes +0xAC, <c>SetAccel</c> writes +0xB0 and
    ///   +0xB4, <c>SetSpeedAndAccel</c> writes all three. Defaults 60, 200, 500.</item>
    /// <item><b>4</b> — the <see cref="DockAction"/>, from +0x80.</item>
    /// <item><b>5</b> — <c>IDockAction</c> +0x95, the constructor's <c>bool</c>, passed straight down by
    ///   <c>PickupObjectAction</c>, <c>PopAWheelieAction</c> and <c>RollObjectAction</c> from their own
    ///   third argument. The same flag decides whether the lift track is locked (tracks 7 when it is
    ///   false, 3 when true), so the lift is left to whatever this asks for. Its CLAD name is not
    ///   established; its source is.</item>
    /// <item><b>6</b> — <c>IDockAction</c> +0xBA. The constructor clears it and no shipped action writes
    ///   it, so it is zero on every dock. Zero here is the engine's value, not a stand-in for one.</item>
    /// <item><b>7</b> — the docking method, +0xBB, named by
    ///   <c>DriveToPickupObjectAction::SetDockingMethod</c> 0x0055C5A2. <c>PickupObjectAction</c> sets 2.</item>
    /// <item><b>8</b> — <c>IDockAction</c> +0xC1, a <c>bool</c>; <c>PickupObjectAction</c> sets 1,
    ///   the constructor 0.</item>
    /// </list>
    ///
    /// Until this was read, the three speeds were written one field early — into words 0, 1 and 2, with
    /// zero in word 3 — so every dock this stack sent had its speed where the robot reads whatever word 0
    /// is, and no deceleration at all.
    /// </summary>
    public static DockWithObject Message(float speedMmps, float accelMmps2, float decelMmps2, DockAction action,
                                         bool unlockLiftTrack = false, DockingMethod method = DockingMethod.Default,
                                         bool flag8 = false) => new()
    {
        // fidelity: M12-005
        UnusedZero = 0,
        SpeedMmps = speedMmps,
        AccelMmps2 = accelMmps2,
        DecelMmps2 = decelMmps2,
        DockAction = (byte)action,
        Field5 = unlockLiftTrack,
        Field6 = 0,
        DockingMethod = (byte)method,
        Field8 = flag8,
    };

    /// <summary>
    /// Starts a dock on a located object's marker and completes with the robot's result (or null on timeout).
    /// While it runs, every processed frame that sees the marker sends a docking error signal.
    /// </summary>
    public async Task<DockResult?> DockAsync(ObservableObject target, KnownMarker marker, DockAction action, PathMotionProfile profile,
                                             double placementOffsetX = 0, double placementOffsetY = 0, double placementOffsetAngle = 0,
                                             bool unlockLiftTrack = false, DockingMethod method = DockingMethod.Default,
                                             bool flag8 = false, TimeSpan? timeout = null, CancellationToken cancel = default)
    {
        // fidelity: M12-003
        TaskCompletionSource<DockResult> tcs;
        lock (_gate)
        {
            if (_pending is not null) throw new InvalidOperationException("a dock is already running");
            tcs = _pending = new TaskCompletionSource<DockResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _active = (target.ObjectId, marker, placementOffsetX, placementOffsetY, placementOffsetAngle, action);
            // fidelity: M4-009
            // C11.1 D2 (0x0063BAAC/0x0063BAB6): DockWithObject copies the docked object's ObjectID into +0xC.
            DockTargetObjectId = target.ObjectId;
            ErrorSignalsSent = 0;
        }
        _vision.World.MarkDirty(target.ObjectId);                          // ObjectPoseConfirmer::MarkObjectDirty in DockWithObject
        Log?.Invoke($"Docking with marker {marker.Code} using action {action}.");
        Send(Message(profile.DockSpeedMmps, profile.DockAccelMmps2, profile.DockDecelMmps2, action, unlockLiftTrack, method, flag8));
        // the error signal for the current frame, if the marker is in it right now
        if (_vision.LastResult is { } last) OnFrame(last);
        using var reg = cancel.Register(() => tcs.TrySetCanceled());
        var done = await Task.WhenAny(tcs.Task, Task.Delay(timeout ?? TimeSpan.FromSeconds(20), CancellationToken.None));
        lock (_gate) { _pending = null; _active = null; }
        if (done != tcs.Task || tcs.Task.IsCanceled)
        {
            Log?.Invoke("dock did not report a result in time; aborting");
            Send(new AbortDocking());
            return null;
        }
        return tcs.Task.Result;
    }

    /// <summary>
    /// <c>CarryingComponent::PlaceObjectOnGround</c> → <c>PlaceObjectOnGround</c> (0x44); completes with the
    /// robot's <c>PickAndPlaceResult</c> (expected <c>BlockPlaced</c>), or null on timeout.
    /// </summary>
    public async Task<DockResult?> PlaceOnGroundAsync(PlaceObjectOnGround message, TimeSpan timeout, CancellationToken cancel)
    {
        TaskCompletionSource<DockResult> tcs;
        lock (_gate)
        {
            if (_pending is not null) throw new InvalidOperationException("a dock is already running");
            tcs = _pending = new TaskCompletionSource<DockResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        Log?.Invoke("PlaceObjectOnGround sent");
        Send(message);
        using var reg = cancel.Register(() => tcs.TrySetCanceled());
        var done = await Task.WhenAny(tcs.Task, Task.Delay(timeout, CancellationToken.None));
        lock (_gate) _pending = null;
        return done == tcs.Task && !tcs.Task.IsCanceled ? tcs.Task.Result : null;
    }

    /// <summary><c>DockingComponent::AbortDocking</c>.</summary>
    public void Abort()
    {
        // fidelity: M12-018
        Send(new AbortDocking());
        lock (_gate) { _pending?.TrySetCanceled(); _pending = null; _active = null; }
    }

    private void OnFrame(VisionFrameResult r)
    {
        // fidelity: M12-013, M12-014, M12-019
        (uint ObjectId, KnownMarker Marker, double OffX, double OffY, double OffAngle, DockAction Action) active;
        lock (_gate) { if (_active is not { } a) return; active = a; }
        if (r.PoseData.RotatingTooFast) return;
        var seen = r.Markers.FirstOrDefault(m => m.Code == active.Marker.Code);
        if (seen is null) return;
        var obj = _vision.World.GetObjectById(active.ObjectId);
        var known = obj?.Markers.FirstOrDefault(k => k.Code == active.Marker.Code);
        if (obj is null || known is null || _vision.Calibration is null) return;
        // the marker's pose from this frame: solve it directly from the observed corners
        var cal = _vision.Calibration;
        var camera = new CameraModel(cal, r.PoseData.CameraPose);
        // the marker's own geometry, not a square of its width: the charger's marker is 20 x 27, and
        // solving it as square puts the pose the docking error signal is built from in the wrong place
        var solved = PoseEstimation.Solve(camera, known.Corners3d(), seen.Corners);
        if (solved is null) return;
        var markerInWorld = camera.Pose.Compose(solved.ObjectInCamera);
        var wrtRobot = markerInWorld.WithRespectTo(r.PoseData.RobotPose);
        var flat = BlockWorld.ClampPoseToFlat(wrtRobot, ClampToFlatAngleRad);
        double x = wrtRobot.Translation.X - active.OffX, y = wrtRobot.Translation.Y + active.OffY, z = wrtRobot.Translation.Z;
        double angle = flat.AngleAroundZ + Math.PI / 2 + active.OffAngle;
        // The timestamp is the first word, not the last: UpdateDockingErrorSignal writes its only
        // argument straight into it at 0x0063C14A, before any of the geometry.
        Send(new DockingErrorSignal { Timestamp = r.Timestamp, XDist = (float)x, YDist = (float)y, ZDist = (float)z,
                                      Angle = (float)angle, Field5 = false, Field6 = false });
        ErrorSignalsSent++;
    }

    private void OnMessage(RobotMessage m)
    {
        switch (m)
        {
            case PickAndPlaceResult r:
            {
                var result = new DockResult(r.Field0, r.Field1, r.Field2, (BlockStatus)r.Field3);
                Log?.Invoke($"PickAndPlaceResult: {result}");
                // HandlePickAndPlaceResult 0x00533780 stores [msg+4] (success) into DockingComponent+5 (0x00533790..0x0053379A) before it branches on blockStatus (M12-008).
                // Nothing in the inventory reads that byte, so it is stored here and consumed by nothing.
                // fidelity: M12-008
                DockingSuccessByte = result.Succeeded;
                // The dock in progress; a result that arrives with no dock running has neither an object nor a marker, so the attach below cannot
                // be reached without both (the previous code's "no marker" case was unreachable: object and marker come from the same tuple).
                (uint ObjectId, KnownMarker Marker)? dock;
                lock (_gate) dock = _active is { } a ? (a.ObjectId, a.Marker) : null;
                if (result.Succeeded && result.Status == BlockStatus.BlockPickedUp && dock is { } d)
                {
                    // HandlePickAndPlaceResult 0x00533850 (blockStatus == 2 and success != 0) calls SetDockObjectAsAttachedToLift, i.e.
                    // SetObjectAsAttachedToLift 0x00632CC4, which places the object on the lift, so the world model stops holding it where it
                    // was last seen on the table. The engine DISCARDS the return value (0x00533848..0x00533854: r0 is not read, the next
                    // instruction returns): a failed attach changes nothing (no rollback, message or retry). LastAttachResult and the log line
                    // are this stack's observation only; nothing acts on them.
                    // fidelity: M12-008
                    uint attach = SetObjectAsAttachedToLift(d.ObjectId, d.Marker.Code);
                    LastAttachResult = attach;
                    if (attach != 0) Log?.Invoke($"SetObjectAsAttachedToLift returned {attach} (discarded by HandlePickAndPlaceResult, M12-008)");
                    // The engine parents the object on top of the carried one to it at once (SetParent 0x00633006..0x00633010); the per-tick
                    // recomposition is this stack's substitute for the pose tree (M12-027), so run it now rather than at the next frame.
                    UpdateCarriedObjectPose();
                }
                if (result.Succeeded && result.Status == BlockStatus.BlockPlaced) ReleaseCarriedObject();
                TaskCompletionSource<DockResult>? tcs; lock (_gate) tcs = _pending;
                tcs?.TrySetResult(result);
                break;
            }
            // M12-005 / R-P4: the engine compares the received byte for equality with IDockAction+0x80
            // (the DockAction this dock was started with), not for non-zero.
            case MovingLiftPostDock ml:
            {
                DockAction? activeAction; lock (_gate) activeAction = _active?.Action;
                MovingLiftPostDock?.Invoke(activeAction is { } a && ml.Field0 == (byte)a);
                break;
            }
            // M12-017: IDockAction::Init 0x00551750 registers a handler for tag 0xDA (LiftLoad); it is
            // action-scoped, so only while a dock is active.
            case LiftLoad ll:
            {
                bool activeDock; lock (_gate) activeDock = _active is not null;
                if (activeDock) LiftLoad?.Invoke(ll.Field0);
                break;
            }
        }
    }

    /// <summary>
    /// Gives the vision system back everything this instance installed on it, and nothing else.
    ///
    /// A disposed component that leaves a callback behind is still deciding: the carried-object updater
    /// would go on writing a pose into the world, and the carrying question the world asks before it
    /// dirties a moved cube would still be answered by a component nobody is using. But a replacement may
    /// already have installed its own, so each delegate is only cleared while it is still the one this
    /// instance put there.
    /// </summary>
    public void Dispose()
    {
        _robot.Message -= OnMessage;
        _vision.FrameProcessed -= OnFrame;
        _vision.FrameProcessed -= _carriedPose;
        if (ReferenceEquals(_vision.IsCarryingObject, _isCarrying)) _vision.IsCarryingObject = null;
        if (ReferenceEquals(_vision.CarriedObjects, _carriedObjects)) _vision.CarriedObjects = null;
    }
}
