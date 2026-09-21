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
public enum DockingMethod : byte { Default = 0, Method1 = 1, Method2 = 2 }

/// <summary><c>PickAndPlaceResult.blockStatus</c>: 0 no block, 1 block picked up, 2 block placed (<c>HandlePickAndPlaceResult</c> 0x00533781).</summary>
public enum BlockStatus : byte { NoBlock = 0, BlockPickedUp = 1, BlockPlaced = 2 }

/// <summary>The robot's report at the end of a dock: <c>PickAndPlaceResult {timestamp u32, didSucceed u8, result u8, blockStatus u8}</c>.</summary>
public sealed record DockResult(uint Timestamp, bool Succeeded, byte DockingResult, BlockStatus Status)
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
/// The engine's <c>CarryingComponent</c>: which object is on the lift. <c>SetDockObjectAsAttachedToLift</c>
/// runs when a pick-up result reports <c>BlockPickedUp</c>, <c>SetCarriedObjectAsUnattached</c> when a place
/// reports <c>BlockPlaced</c> (<c>HandlePickAndPlaceResult</c>). The carried object's pose follows the robot
/// while carried (INFERRED reduction of the engine's pose-parent chain to the lift).
/// </summary>
public sealed class CarryingComponent
{
    private readonly object _gate = new();
    private uint? _carried;

    public bool IsCarryingObject { get { lock (_gate) return _carried is not null; } }
    public uint? CarriedObjectId { get { lock (_gate) return _carried; } }
    public event Action<uint?>? Changed;

    public bool IsCarrying(uint objectId) { lock (_gate) return _carried == objectId; }

    public void SetCarrying(uint objectId) { lock (_gate) _carried = objectId; Changed?.Invoke(objectId); }

    public void UnsetCarrying() { lock (_gate) _carried = null; Changed?.Invoke(null); }
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
/// <see cref="Message"/> - including the two words that are genuinely zero there. The two trailing bytes
/// of the error signal are still unread (hardware item N).
/// </summary>
public sealed class DockingSystem : IDisposable
{
    public const double ClampToFlatAngleRad = 0.698132;
    public const double RotatingTooFastRadPerSec = 22.9183 * Math.PI / 180;

    private readonly CozmoRobot _robot;
    private readonly VisionSystem _vision;
    private readonly object _gate = new();
    private TaskCompletionSource<DockResult>? _pending;
    private (uint ObjectId, MarkerType Marker, double OffX, double OffY, double OffAngle)? _active;

    public DockingSystem(CozmoRobot robot, VisionSystem vision)
    {
        _robot = robot; _vision = vision;
        robot.Message += OnMessage;
        vision.FrameProcessed += OnFrame;
    }

    public CarryingComponent Carrying { get; } = new();
    public List<RobotMessage> Sent { get; } = new();
    public int ErrorSignalsSent { get; private set; }
    public event Action<string>? Log;
    /// <summary>Raised when the robot reports it is moving the lift after a dock (<c>MovingLiftPostDock</c>).</summary>
    public event Action<bool>? MovingLiftPostDock;

    private void Send(RobotMessage m) { Sent.Add(m); _robot.Transport.Send(m, flush: true); }

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
        Field0 = 0,
        Field1 = BitConverter.SingleToUInt32Bits(speedMmps),
        Field2 = BitConverter.SingleToUInt32Bits(accelMmps2),
        Field3 = BitConverter.SingleToUInt32Bits(decelMmps2),
        Field4 = (byte)action,
        Field5 = (byte)(unlockLiftTrack ? 1 : 0),
        Field6 = 0,
        Field7 = (byte)method,
        Field8 = (byte)(flag8 ? 1 : 0),
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
        TaskCompletionSource<DockResult> tcs;
        lock (_gate)
        {
            if (_pending is not null) throw new InvalidOperationException("a dock is already running");
            tcs = _pending = new TaskCompletionSource<DockResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _active = (target.ObjectId, marker.Code, placementOffsetX, placementOffsetY, placementOffsetAngle);
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
        Send(new AbortDocking());
        lock (_gate) { _pending?.TrySetCanceled(); _pending = null; _active = null; }
    }

    private void OnFrame(VisionFrameResult r)
    {
        (uint ObjectId, MarkerType Marker, double OffX, double OffY, double OffAngle) active;
        lock (_gate) { if (_active is not { } a) return; active = a; }
        if (r.PoseData.RotatingTooFast) return;
        var seen = r.Markers.FirstOrDefault(m => m.Code == active.Marker);
        if (seen is null) return;
        var obj = _vision.World.GetObjectById(active.ObjectId);
        var known = obj?.Markers.FirstOrDefault(k => k.Code == active.Marker);
        if (obj is null || known is null || _vision.Calibration is null) return;
        // the marker's pose from this frame: solve it directly from the observed corners
        var cal = _vision.Calibration;
        var camera = new CameraModel(cal, r.PoseData.CameraPose);
        var solved = PoseEstimation.Solve(camera, KnownMarker.CanonicalCorners.Select(c => c * known.SizeMm).ToList(), seen.Corners);
        if (solved is null) return;
        var markerInWorld = camera.Pose.Compose(solved.ObjectInCamera);
        var wrtRobot = markerInWorld.WithRespectTo(r.PoseData.RobotPose);
        var flat = BlockWorld.ClampPoseToFlat(wrtRobot, ClampToFlatAngleRad);
        double x = wrtRobot.Translation.X - active.OffX, y = wrtRobot.Translation.Y + active.OffY, z = wrtRobot.Translation.Z;
        double angle = flat.AngleAroundZ + Math.PI / 2 + active.OffAngle;
        Send(new DockingErrorSignal { XDist = (float)x, YDist = (float)y, ZDist = (float)z, Angle = (float)angle, Field4 = r.Timestamp, Field5 = 0, Field6 = 0 });
        ErrorSignalsSent++;
    }

    private void OnMessage(RobotMessage m)
    {
        switch (m)
        {
            case PickAndPlaceResult r:
            {
                var result = new DockResult(r.Field0, r.Field1 != 0, r.Field2, (BlockStatus)r.Field3);
                Log?.Invoke($"PickAndPlaceResult: {result}");
                uint? objectId; lock (_gate) objectId = _active?.ObjectId;
                if (result.Succeeded && result.Status == BlockStatus.BlockPickedUp && objectId is { } id) Carrying.SetCarrying(id);
                if (result.Status == BlockStatus.BlockPlaced) Carrying.UnsetCarrying();
                TaskCompletionSource<DockResult>? tcs; lock (_gate) tcs = _pending;
                tcs?.TrySetResult(result);
                break;
            }
            case MovingLiftPostDock ml: MovingLiftPostDock?.Invoke(ml.Field0 != 0); break;
        }
    }

    public void Dispose()
    {
        _robot.Message -= OnMessage;
        _vision.FrameProcessed -= OnFrame;
    }
}
