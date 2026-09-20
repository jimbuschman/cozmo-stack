using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary><c>Anki::Cozmo::DockAction</c> (UNITY enum): what the robot's firmware does once the marker is in reach.</summary>
public enum DockAction : byte
{
    PickupLow = 0, PickupHigh = 1, PlaceHigh = 2, PlaceLow = 3, PlaceLowBlind = 4, RollLow = 5, DeepRollLow = 6, PostDockRoll = 7,
    FacePlant = 8, PopAWheelie = 9, Align = 10, AlignSpecial = 11, RampAscend = 12, RampDescend = 13, CrossBridge = 14,
}

/// <summary><c>PickAndPlaceResult.blockStatus</c>: 0 no block, 1 block picked up, 2 block placed (<c>HandlePickAndPlaceResult</c> 0x00533781).</summary>
public enum BlockStatus : byte { NoBlock = 0, BlockPickedUp = 1, BlockPlaced = 2 }

/// <summary>The robot's report at the end of a dock: <c>PickAndPlaceResult {timestamp u32, didSucceed u8, result u8, blockStatus u8}</c>.</summary>
public sealed record DockResult(uint Timestamp, bool Succeeded, byte DockingResult, BlockStatus Status)
{
    public override string ToString() => $"{(Succeeded ? "succeeded" : "failed")} result={DockingResult} status={Status} t={Timestamp}";
}

/// <summary>
/// <c>MoveLiftToHeightAction::Preset</c>: LowDock, HighDock, HeightCarry, OutOfFOV (names from
/// <c>GetPresetName</c> 0x00548D04). Heights INFERRED: the low-dock and high-dock heights are the robot's lift
/// range limits (32 / 92 mm, the SDK's <c>MIN_LIFT_HEIGHT</c> / <c>MAX_LIFT_HEIGHT</c>, also this stack's
/// <c>CozmoMotion.MinLiftHeightMm</c> / <c>MaxLiftHeightMm</c>); the carry height (72 mm) is the value the same
/// constant carries in Anki's later open-sourced engine; the out-of-view height was not found (taken as the
/// carry height). The engine fills its preset map at runtime from constants this build does not export.
/// </summary>
public static class LiftPresets
{
    public const float LowDockMm = 32f;
    public const float HighDockMm = 92f;
    public const float CarryMm = 72f;
    public const float OutOfFovMm = 72f;
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
/// INFERRED: the fourth float of <c>DockWithObject</c> and its last two bytes are sent as zero, the two trailing
/// bytes of the error signal likewise (hardware item N).
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

    /// <summary>The engine's message for a dock (<c>DockingComponent::DockWithObject</c> → 0x0063BD50).</summary>
    public static DockWithObject Message(float speedMmps, float accelMmps2, float decelMmps2, DockAction action, byte numRetries, bool doLiftLoadCheck) => new()
    {
        Field0 = BitConverter.SingleToUInt32Bits(speedMmps), Field1 = BitConverter.SingleToUInt32Bits(accelMmps2), Field2 = BitConverter.SingleToUInt32Bits(decelMmps2),
        Field3 = 0, Field4 = (byte)action, Field5 = numRetries, Field6 = (byte)(doLiftLoadCheck ? 1 : 0), Field7 = 0, Field8 = 0,
    };

    /// <summary>
    /// Starts a dock on a located object's marker and completes with the robot's result (or null on timeout).
    /// While it runs, every processed frame that sees the marker sends a docking error signal.
    /// </summary>
    public async Task<DockResult?> DockAsync(ObservableObject target, KnownMarker marker, DockAction action, PathMotionProfile profile,
                                             double placementOffsetX = 0, double placementOffsetY = 0, double placementOffsetAngle = 0,
                                             byte numRetries = 0, bool doLiftLoadCheck = false, TimeSpan? timeout = null, CancellationToken cancel = default)
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
        Send(Message(profile.DockSpeedMmps, profile.DockAccelMmps2, profile.DockDecelMmps2, action, numRetries, doLiftLoadCheck));
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
