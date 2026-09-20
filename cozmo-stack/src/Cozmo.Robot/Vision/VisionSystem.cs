using System.Diagnostics;
using Cozmo.Protocol;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Vision;

/// <summary>What one frame produced.</summary>
public sealed record VisionFrameResult(uint ImageId, uint Timestamp, VisionPoseData PoseData, IReadOnlyList<ObservedMarker> Markers,
                                       IReadOnlyList<ObjectObservation> Objects, IReadOnlyList<ObservableObject> Forgotten, TimeSpan Elapsed)
{
    public override string ToString() =>
        $"image {ImageId} t={Timestamp}: {Markers.Count} marker(s) [{string.Join(" ", Markers.Select(m => m.Code))}], " +
        $"{Objects.Count} object(s), {Forgotten.Count} forgotten, {Elapsed.TotalMilliseconds:F0} ms";
}

/// <summary>
/// The engine's <c>VisionSystem</c> + <c>VisionComponent</c> for the marker path: decode the frame to gray, pair
/// it with the robot state at its timestamp (<c>VisionPoseData</c>), detect markers, update <see cref="BlockWorld"/>,
/// and check for objects that should have been seen. Faces, pets, motion, overhead edges, laser points and
/// tool codes, which share <c>VisionSystem::Update</c>, are OUT OF SCOPE: face and pet detection are Omron OKAO
/// library calls statically linked into the engine, with no Anki algorithm to transcribe.
///
/// The engine refuses to run without a camera calibration; so does this. Frames arriving while one is being
/// processed are dropped (the engine processes at most one image at a time as well).
/// </summary>
public sealed class VisionSystem : IDisposable
{
    private readonly CozmoRobot _robot;
    private readonly object _busy = new();
    private int _processing;
    private bool _warnedNoCalibration;

    public VisionSystem(CozmoRobot robot, CameraCalibration? calibration = null, MarkerDetector? detector = null)
    {
        _robot = robot;
        Calibration = calibration;
        Detector = detector ?? new MarkerDetector();
        Faces.Log += l => Log?.Invoke(l);
        World = new BlockWorld(() => robot.Cubes.ConnectedCubes.Where(c => c.ObjectId is not null).Select(c => (c.ObjectId!.Value, c.Type)));
        History = new RobotStateHistory();
        Locator = new CubeLocator(this);
        robot.Message += OnMessage;
        robot.Camera.FrameReceived += OnFrame;
    }

    /// <summary>The robot this system watches.</summary>
    public CozmoRobot Robot => _robot;

    /// <summary>The robot's calibration; null until read from NV storage (<see cref="NvCalibrationReader"/>) or set.</summary>
    public CameraCalibration? Calibration { get; set; }
    public MarkerDetector Detector { get; }
    public BlockWorld World { get; }
    public RobotStateHistory History { get; }
    /// <summary>The real <see cref="ICubeLocator"/> the M10 cube reaction was built against.</summary>
    public CubeLocator Locator { get; }
    /// <summary>The face world (M14); filled only while a <see cref="FaceDetector"/> that is available is attached.</summary>
    public FaceWorld Faces { get; } = new();
    public PetWorld Pets { get; } = new();
    /// <summary>The face detector: the stock one is the OKAO boundary and reports itself unavailable.</summary>
    public IFaceDetector FaceDetector { get; set; } = new OkaoFaceDetector();
    public IPetDetector PetDetector { get; set; } = new OkaoPetDetector();
    /// <summary>Replaceable body-and-head turn for the face actions (tests move a fake robot with it).</summary>
    public Func<Pose3d, double, CancellationToken, Task<bool>>? TurnOverride { get; set; }
    /// <summary>Replaceable pan-and-tilt (absolute body heading, head angle) for the explorer behaviours.</summary>
    public Func<double, double, CancellationToken, Task<bool>>? PanTiltOverride { get; set; }
    /// <summary>Faces seen in the last processed frame.</summary>
    public IReadOnlyList<TrackedFace> LastFaces { get; private set; } = Array.Empty<TrackedFace>();
    /// <summary>Whether frames from the camera are processed as they arrive.</summary>
    public bool Enabled { get; set; } = true;
    public int FramesProcessed { get; private set; }
    public int FramesDropped { get; private set; }
    public VisionFrameResult? LastResult { get; private set; }

    public event Action<VisionFrameResult>? FrameProcessed;
    public event Action<string>? Log;

    /// <summary>Reads the calibration from the robot (the engine's connection-time <c>NVStorageComponent::Read</c>).</summary>
    public async Task<CameraCalibration?> ReadCalibrationAsync(TimeSpan? timeout = null)
    {
        using var reader = new NvCalibrationReader(_robot);
        var cal = await reader.ReadAsync(timeout);
        foreach (var l in reader.Log) Log?.Invoke(l);
        if (cal is not null) Calibration = cal;
        return cal;
    }

    /// <summary>The camera at the latest known robot state, for visibility questions asked now.</summary>
    public CameraModel? CurrentCamera()
    {
        if (Calibration is null || History.Latest is not { } pd) return null;
        return new CameraModel(Calibration, pd.CameraPose);
    }

    private void OnMessage(RobotMessage m)
    {
        switch (m)
        {
            case RobotState s: History.Add(s); break;
            case ObjectMoved mv: World.MarkDirty(mv.ObjectID); break;
            // A cube that has dropped its radio link cannot be tracked or docked with any more, and its last
            // pose will go stale the moment someone moves it. The engine drops such an object from the world
            // model; here its pose goes Unknown, which is what every located-object query already tests
            // (LOCAL: the engine's ObjectConnectionState handling was not transcribed, only its effect).
            case ObjectConnectionState cs when !cs.Connected: OnCubeDisconnected(cs.ObjectID); break;
        }
    }

    private void OnCubeDisconnected(uint objectId)
    {
        if (World.GetObjectById(objectId) is not { } o || o.PoseState == PoseState.Unknown) return;
        Log?.Invoke($"object {objectId} disconnected: its pose is no longer known");
        World.MarkUnknown(objectId);
    }

    private void OnFrame(CameraFrame f)
    {
        if (!Enabled) return;
        if (Interlocked.CompareExchange(ref _processing, 1, 0) != 0) { FramesDropped++; return; }
        Task.Run(() =>
        {
            try { ProcessFrame(f); }
            catch (Exception e) { Log?.Invoke($"frame {f.ImageId}: {e.GetType().Name}: {e.Message}"); }
            finally { Interlocked.Exchange(ref _processing, 0); }
        });
    }

    /// <summary>
    /// The camera timestamp of the last frame whose timestamp had to be replaced by the paired robot state's
    /// (0 on fw2457 captures). Diagnostics only: the world model is dated by the state's timestamp.
    /// </summary>
    public uint? LastRawFrameTimestamp { get; private set; }

    /// <summary>Processes one camera frame against the recorded robot state; null without calibration or state.</summary>
    public VisionFrameResult? ProcessFrame(CameraFrame f)
    {
        if (Calibration is null)
        {
            if (!_warnedNoCalibration) { _warnedNoCalibration = true; Log?.Invoke("Must be initialized and have calibrated camera to Update (no calibration set)"); }
            return null;
        }
        return ProcessCapture(GrayImage.FromFrame(f), f.ImageId, f.Timestamp);
    }

    /// <summary>
    /// The camera path with the image already decoded: pairs the capture with the robot state at its
    /// timestamp and processes it. Split out from <see cref="ProcessFrame(CameraFrame)"/> so the fw2457
    /// timestamp-zero path can be driven without a JPEG.
    /// </summary>
    public VisionFrameResult? ProcessCapture(GrayImage gray, uint imageId, uint cameraTimestamp)
    {
        if (Calibration is null)
        {
            if (!_warnedNoCalibration) { _warnedNoCalibration = true; Log?.Invoke("Must be initialized and have calibrated camera to Update (no calibration set)"); }
            return null;
        }
        // the fw2457 captures carry FrameTimestamp 0 on every chunk; a frame with no timestamp is paired with the
        // latest state (LOCAL fallback; the engine's EncodedImage rejects a bad timestamp instead)
        var pd = cameraTimestamp == 0 ? History.Latest : History.At(cameraTimestamp);
        if (pd is null) { Log?.Invoke($"frame {imageId}: no robot state to pair with"); return null; }
        // Once the fallback has chosen a state, that state's timestamp is the observation time for the whole
        // frame. Passing the camera's zero through would date every marker, object and face at 0, which makes
        // observation age, face expiry and object-position age meaningless on real fw2457 hardware.
        uint effective = cameraTimestamp == 0 ? pd.Value.Timestamp : cameraTimestamp;
        if (effective != cameraTimestamp) LastRawFrameTimestamp = cameraTimestamp;
        return ProcessImage(gray, imageId, effective, pd.Value);
    }

    /// <summary>The core update over a decoded image and the robot's pose data for it (usable offline).</summary>
    public VisionFrameResult ProcessImage(GrayImage gray, uint imageId, uint timestamp, VisionPoseData pd)
    {
        if (Calibration is null) throw new InvalidOperationException("no camera calibration");
        var sw = Stopwatch.StartNew();
        var cal = gray.Width == Calibration.Columns && gray.Height == Calibration.Rows ? Calibration : Calibration.Scaled(gray.Width, gray.Height);
        var camera = new CameraModel(cal, pd.CameraPose);
        IReadOnlyList<ObservedMarker> markers;
        IReadOnlyList<ObjectObservation> objects;
        IReadOnlyList<ObservableObject> forgotten;
        lock (_busy)
        {
            markers = Detector.Detect(gray, timestamp);
            objects = World.UpdateObservedMarkers(markers, camera, timestamp);
            forgotten = World.CheckForUnobservedObjects(camera, timestamp, objects.Select(o => o.Object.ObjectId).ToHashSet(), pd.Moving, pd.RotatingTooFast);
            // VisionSystem::Update in DetectingFaces mode: FaceTracker::Update, TrackedFace::UpdateTranslation(camera), FaceWorld::AddOrUpdateFace
            if (FaceDetector.IsAvailable)
            {
                var faces = new List<TrackedFace>();
                foreach (var d in FaceDetector.Detect(gray, timestamp))
                {
                    var tf = new TrackedFace(d, timestamp);
                    tf.UpdateTranslation(camera);
                    faces.Add(tf);
                    Faces.AddOrUpdateFace(tf, pd.RobotPose, pd.RotatingTooFast);
                }
                LastFaces = faces;
                Faces.Update(timestamp);
            }
            if (PetDetector.IsAvailable) Pets.Update(PetDetector.Detect(gray, timestamp), timestamp, pd.RotatingTooFast);
        }
        var result = new VisionFrameResult(imageId, timestamp, pd, markers, objects, forgotten, sw.Elapsed);
        FramesProcessed++;
        LastResult = result;
        FrameProcessed?.Invoke(result);
        return result;
    }

    public void Dispose()
    {
        _robot.Message -= OnMessage;
        _robot.Camera.FrameReceived -= OnFrame;
    }
}

/// <summary>
/// The <see cref="ICubeLocator"/> the M10 cube-moved path asked for, answered by <see cref="BlockWorld"/>:
/// located = the world model has a pose that is not Unknown; distance = between the object's and the robot's
/// poses; visible = <c>IsVisibleFrom(camera now, 0.785398 rad)</c>; turn = <c>TurnTowardsPoseAction</c>.
/// </summary>
public sealed class CubeLocator : ICubeLocator
{
    private readonly VisionSystem _vision;

    public CubeLocator(VisionSystem vision) => _vision = vision;

    public BlockWorld World => _vision.World;

    public bool IsLocated(uint objectId) => World.GetLocatedObjectById(objectId) is not null;

    public float? DistanceFromRobotMm(uint objectId)
    {
        if (_vision.History.Latest is not { } pd) return null;
        return World.DistanceFromRobotMm(objectId, pd.RobotPose) is { } d ? (float)d : null;
    }

    public bool IsVisibleFromCamera(uint objectId)
    {
        var o = World.GetLocatedObjectById(objectId);
        var cam = _vision.CurrentCamera();
        if (o is null || cam is null) return false;
        return o.IsVisibleFrom(cam, BlockWorld.VisibilityNormalAngleRad, World.MinVisibleMarkerSizePx, 0, 0, out _);
    }

    /// <summary>The turn the behaviours run; replaceable so tests can observe it without a robot.</summary>
    public Func<uint, double, CancellationToken, Task<bool>>? TurnOverride { get; set; }

    public Task<bool> TurnTowardsAsync(uint objectId, CancellationToken cancel) => TurnTowardsAsync(objectId, Math.PI, cancel);

    /// <summary><c>TurnTowardsPoseAction(robot, pose, maxTurnAngle)</c> for a located object.</summary>
    public Task<bool> TurnTowardsAsync(uint objectId, double maxTurnAngleRad, CancellationToken cancel)
    {
        if (TurnOverride is not null) return TurnOverride(objectId, maxTurnAngleRad, cancel);
        var o = World.GetLocatedObjectById(objectId);
        if (o is null) return Task.FromResult(false);
        return TurnTowardsPose.RunAsync(_vision, o.Pose, maxTurnAngleRad, cancel);
    }
}

/// <summary>
/// <c>TurnTowardsPoseAction::Init</c> (0x0054A8FC): the pose is taken with respect to the robot, the turn is
/// <c>atan2(y, x)</c> of its translation, and the action fails ("Required turn angle ... is larger than max angle")
/// when |turn| exceeds the maximum; the head angle to look at the pose is computed
/// (<c>GetAbsoluteHeadAngleToLookAtPose</c>) and clamped to −25..44.5 degrees. The body turn goes out as
/// <c>SetBodyAngle</c> (0x39) through <c>MovementComponent::TurnInPlace(angle, maxSpeed, accel, tolerance,
/// numHalfRevolutions, useShortestDirection, actionId)</c>, whose field order is the packing read at 0x00640898
/// (NATIVE). INFERRED, hardware-pending (HARDWARE_TEST_PLAN item L): that the angle is the absolute body angle
/// in the robot's pose frame. LOCAL_POLICY: speed 100 deg/s and acceleration 10 rad/s²; the engine's
/// <c>TurnInPlaceAction</c> constructor holds 5.23599 and a 2 degree tolerance (0x3D0EFA35), and the tolerance is
/// used here.
/// </summary>
public static class TurnTowardsPose
{
    public const double ToleranceRad = 0.0349066;
    public const double MaxSpeedRadPerSec = 1.745329;
    public const double AccelRadPerSec2 = 10.0;

    /// <summary>The engine's message for a body turn; exposed so the conformance tool can show the bytes.</summary>
    public static SetBodyAngle Message(double absoluteAngleRad, double maxSpeed, double accel, double tolerance, ushort numHalfRevolutions, bool useShortestDirection, byte actionId) => new()
    {
        Field0 = BitConverter.SingleToUInt32Bits((float)absoluteAngleRad),
        Field1 = BitConverter.SingleToUInt32Bits((float)maxSpeed),
        Field2 = BitConverter.SingleToUInt32Bits((float)accel),
        Field3 = BitConverter.SingleToUInt32Bits((float)tolerance),
        Field4 = numHalfRevolutions,
        Field5 = (byte)(useShortestDirection ? 1 : 0),
        Field6 = actionId,
    };

    /// <summary>The relative turn to face a world pose from a robot pose: the engine's atan2 over the pose taken with respect to the robot.</summary>
    public static double RelativeTurnRad(Pose3d robot, Pose3d target)
    {
        var rel = target.WithRespectTo(robot).Translation;
        return Math.Atan2(rel.Y, rel.X);
    }

    /// <summary>
    /// <c>Robot::ComputeHeadAngleToSeePose</c> (0x00518344), iterated to put the target at the image centre's
    /// row (LOCAL numerics: bisection over the head range; the engine iterates with its own step). Null when the
    /// target cannot be centred within the head's range.
    /// </summary>
    public static double HeadAngleToSee(CameraCalibration cal, Pose3d robot, Vec3 targetWorld, double rowFraction = 0.5)
    {
        double lo = HeadGeometry.MinHeadAngleRad, hi = HeadGeometry.MaxHeadAngleRad;
        double Err(double h)
        {
            var cam = new CameraModel(cal, HeadGeometry.CameraPoseInWorld(robot, h));
            var c = cam.ToCamera(targetWorld);
            if (c.Z <= 1e-6) return double.NaN;
            return cam.Distort(new Vec2(c.X / c.Z, c.Y / c.Z)).Y - cal.Rows * rowFraction;
        }
        double eLo = Err(lo), eHi = Err(hi);
        if (double.IsNaN(eLo) || double.IsNaN(eHi)) return Math.Clamp(0, lo, hi);
        // raising the head moves the target down the image (larger row); pick the bracket end otherwise
        if (Math.Sign(eLo) == Math.Sign(eHi)) return Math.Abs(eLo) < Math.Abs(eHi) ? lo : hi;
        for (int i = 0; i < 40; i++)
        {
            double mid = (lo + hi) / 2, e = Err(mid);
            if (double.IsNaN(e)) break;
            if (Math.Sign(e) == Math.Sign(eLo)) { lo = mid; eLo = e; } else hi = mid;
        }
        return (lo + hi) / 2;
    }

    public static async Task<bool> RunAsync(VisionSystem vision, Pose3d target, double maxTurnAngleRad, CancellationToken cancel, TimeSpan? timeout = null)
    {
        var robot = vision.History.Latest?.RobotPose;
        if (robot is null) return false;
        double turn = RelativeTurnRad(robot.Value, target);
        if (Math.Abs(turn) > maxTurnAngleRad) return false;
        double absolute = robot.Value.AngleAroundZ + turn;
        var transport = vision.Robot.Transport;
        transport.Send(Message(absolute, MaxSpeedRadPerSec, AccelRadPerSec2, ToleranceRad, 0, true, 2), flush: true);
        if (vision.Calibration is { } cal)
        {
            double head = HeadAngleToSee(cal, robot.Value, target.Translation);
            transport.Send(new SetHeadAngle { AngleRad = (float)head, MaxSpeedRadPerSec = 10f, AccelRadPerSec2 = 10f, DurationSec = 0f, ActionId = 3 }, flush: true);
        }
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(6));
        while (DateTime.UtcNow < deadline)
        {
            if (cancel.IsCancellationRequested) return false;
            await Task.Delay(33, CancellationToken.None);
            if (vision.History.Latest is { } now)
            {
                double err = Math.Atan2(Math.Sin(absolute - now.RobotPose.AngleAroundZ), Math.Cos(absolute - now.RobotPose.AngleAroundZ));
                if (Math.Abs(err) <= ToleranceRad && !now.Moving) return true;
            }
        }
        return false;
    }
}
