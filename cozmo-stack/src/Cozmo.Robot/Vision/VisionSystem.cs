using System.Diagnostics;
using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Vision;

/// <summary>What one frame produced.</summary>
public sealed record VisionFrameResult(uint ImageId, uint Timestamp, VisionPoseData PoseData, IReadOnlyList<ObservedMarker> Markers,
                                       IReadOnlyList<ObjectObservation> Objects, IReadOnlyList<ObservableObject> Forgotten, TimeSpan Elapsed)
{
    /// <summary>
    /// The ground in front of the robot as this frame saw it, when the overhead-edge detector is on.
    /// The engine carries the same thing on its <c>VisionProcessingResult</c>, one frame per image, and
    /// <c>VisionComponent::UpdateOverheadEdges</c> 0x006553FC hands it to the map component.
    /// </summary>
    public OverheadEdgeFrame? OverheadEdges { get; init; }

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
    /// <summary>The calibration the constructor was given, which a removal restores.</summary>
    private readonly CameraCalibration? _constructedCalibration;
    /// <summary>Bumped by every removal. A frame started before one is discarded: nothing it found is kept or raised.</summary>
    private int _removals;
    /// <summary>How long a removal waits for a frame being processed to reach a point where it can be discarded.</summary>
    internal static readonly TimeSpan RemovalWait = TimeSpan.FromSeconds(2);

    public VisionSystem(CozmoRobot robot, CameraCalibration? calibration = null, MarkerDetector? detector = null)
    {
        _robot = robot;
        Calibration = calibration;
        _constructedCalibration = calibration;
        Detector = detector ?? new MarkerDetector();
        Faces.Log += l => Log?.Invoke(l);
        World = new BlockWorld(() => robot.Cubes.ConnectedCubes.Where(c => c.ObjectId is not null).Select(c => (c.ObjectId!.Value, c.Type)));
        History = new RobotStateHistory();
        Locator = new CubeLocator(this);
        robot.Message += OnMessage;
        // fidelity: M3-005, M3-022
        // Vision gets what HandleImageChunk hands to SetNextImage (the per-tick cap, A3), and the calibration and the
        // enable the connection-time NV read gives (1j).
        robot.Camera.FrameForVision += OnFrame;
        robot.CameraSettings.CalibrationInstalled += OnCalibrationInstalled;
        robot.CameraSettings.VisionEnabledSet += OnVisionEnabledSet;
        if (robot.CameraSettings.Calibration is { } read) Calibration = read;
        // A VisionSystem built after the connection's NV callback already fired picks up the enable (2d).
        if (robot.CameraSettings.VisionEnabled) Enabled = true;
        robot.RobotRemoved += ResetToConstructed;
        // fidelity: M1-041
        robot.StateHistoryCleared += History.Clear;
        // fidelity: M4-023
        robot.Cubes.DoubleTapPendingEnded += World.MarkDirty;
    }

    // fidelity: M3-022
    /// <summary>SetCameraCalibration from the connection-time NV read (1j).</summary>
    private void OnCalibrationInstalled(CameraCalibration c) => UpdateCameraCalibration(c);

    // fidelity: M11-039
    /// <summary>
    /// <c>VisionSystem::UpdateCameraCalibration</c> 0x006B1E3E: install the calibration
    /// (<c>Camera::SetCalibration</c> 0x006B1E5E) and, on success, call <c>MarkerDetector::Init</c>
    /// 0x006B1E78, which runs <c>Parameters::Initialize</c> 0x008752F8. The M3 NV callback only raises the
    /// install event on the success path, so reaching here is the engine's success condition.
    /// </summary>
    public void UpdateCameraCalibration(CameraCalibration calibration)
    {
        Calibration = calibration;
        Detector.Init();
    }

    // fidelity: M3-022
    /// <summary>The NV callback's +0x48 = 1 (1j, 2d). The gate that reads it is M11's (A4).</summary>
    private void OnVisionEnabledSet() => Enabled = true;

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Run when the robot is removed (CB33, CC26: the Robot is deleted with its components; CC27: the next connect builds
    /// them afresh). The calibration belongs to the engine's VisionComponent, which is destroyed with the Robot, so it
    /// goes back to the one the constructor was given. The state history, the world, the face and pet worlds and the
    /// frame counts go back to their as-constructed state. <see cref="Enabled"/>, the detectors, the overrides and the
    /// hooks other systems installed are kept, as are subscribers.
    ///
    /// A frame being processed when this runs is discarded: the removal count is bumped first, so the frame drops out
    /// at its next check without keeping or raising anything more, and then this waits for it under the processing
    /// lock, at most <see cref="RemovalWait"/>. What a world update already under way when the removal began has
    /// raised cannot be taken back; the world is cleared after it. Processing never takes the engine's tick lock, so
    /// the wait (on the engine thread, inside the tick) ends; only a subscriber to a vision event that posts a game
    /// message on the offline auto-ticking seam could hold it to the bound.
    /// </summary>
    internal void ResetToConstructed()
    {
        Interlocked.Increment(ref _removals);
        bool locked = Monitor.TryEnter(_busy, RemovalWait);
        try
        {
            if (!locked) Log?.Invoke($"vision reset: a frame was still being processed after {RemovalWait.TotalSeconds:F0} s; it is discarded at its next check");
            Calibration = _constructedCalibration;
            History.ResetToConstructed();
            Imu.ResetToConstructed();
            World.ResetToConstructed();
            Faces.ResetToConstructed();
            Pets.ResetToConstructed();
            LastFaces = Array.Empty<TrackedFace>();
            FramesProcessed = 0;
            FramesDropped = 0;
            LastResult = null;
            LastRawFrameTimestamp = null;
            _warnedNoCalibration = false;
        }
        finally { if (locked) Monitor.Exit(_busy); }
    }

    /// <summary>Whether a removal happened since the frame that captured <paramref name="removal"/> started.</summary>
    private bool RemovedSince(int removal) => Volatile.Read(ref _removals) != removal;

    private void WarnNoCalibration(int removal)
    {
        lock (_busy)
        {
            if (RemovedSince(removal) || _warnedNoCalibration) return;
            _warnedNoCalibration = true;
            Log?.Invoke("Must be initialized and have calibrated camera to Update (no calibration set)");
        }
    }

    /// <summary>The robot this system watches.</summary>
    public CozmoRobot Robot => _robot;

    /// <summary>The robot's calibration; null until read from NV storage (<see cref="NvCalibrationReader"/>) or set.</summary>
    public CameraCalibration? Calibration { get; set; }
    public MarkerDetector Detector { get; }
    public BlockWorld World { get; }
    public RobotStateHistory History { get; }
    /// <summary>
    /// The engine's <c>ImuDataHistory</c> at <c>VisionComponent+0xb0</c> (C3.3), fed by
    /// <c>HandleImageImuData</c> 0x00535C20 and read by <c>WasRotatingTooFast</c> 0x0065359C.
    /// </summary>
    // fidelity: M11-004
    public ImuDataHistory Imu { get; } = new();
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
    /// <summary>
    /// Whether frames from the camera are processed as they arrive. The engine's VisionComponent +0x48 starts 0
    /// (2a) and is set to 1 only by the NV calibration callback (1j, 2d), so this starts false and the callback
    /// turns it on; a caller that drives frames directly (offline tools and tests) sets it itself.
    /// </summary>
    public bool Enabled { get; set; }

    // fidelity: M11-021, M11-034
    /// <summary>The vision-mode number of <c>DetectingMarkers</c> (0x006B5162: <c>movs r1,#1</c>).</summary>
    public const int DetectingMarkers = (int)VisionMode.DetectingMarkers;

    /// <summary>
    /// The shipped <c>vision_config.json</c> <c>InitialVisionModes</c> as the mode-enable bitmask
    /// (M11-034, C2.3): markers 1, faces 2, motion 3, overhead edges 4, quality 7, statistics 8, pets 9 and
    /// laser points 15 are enabled; 10..13 are disabled. The modes the config does not list - 0 (Idle),
    /// 5 (ReadingToolCode), 6 (ComputingCalibration), 14 (LimitedExposure) - have an UNKNOWN default and
    /// are left clear.
    /// </summary>
    public const int ShippedModeEnableMask = 0x0002 | 0x0004 | 0x0008 | 0x0010 | 0x0080 | 0x0100 | 0x0200 | 0x8000;

    /// <summary>
    /// The front schedule list (<c>VisionSystem+0xd0</c>); the engine tail-calls
    /// <c>CheckTimeToProcessAndAdvance</c> on its front schedule (0x006B5AB4..0x006B5AC6). The default is
    /// <c>InitDefaultSchedules</c> (all 16 modes {true}, counter 0), so every enabled mode runs every frame.
    /// </summary>
    public AllVisionModesSchedule Schedules { get; set; } = AllVisionModesSchedule.Default;

    /// <summary>The mode enable bitmask at <c>VisionSystem+0xac</c>, from the shipped config (C2.3).</summary>
    public int ModeEnableMask { get; set; } = ShippedModeEnableMask;

    /// <summary>
    /// <c>VisionSystem::ShouldProcessVisionMode(mode)</c> 0x006B5AA4: the mode's enable bit in the
    /// bitmask at <c>VisionSystem+0xac</c> AND the front schedule's
    /// <c>AllVisionModesSchedule::CheckTimeToProcessAndAdvance(mode)</c> 0x006AF1F8. A mode whose bit is
    /// clear returns false without touching the schedule (0x006B5AB0..0x006B5AB2).
    /// </summary>
    public bool ShouldProcessVisionMode(int mode)
    {
        if ((ModeEnableMask & (1 << mode)) == 0) return false;
        // 0x006B5AB8: cbz r2,0x6b5aca — an empty front-schedule list returns 0 without advancing anything.
        var schedules = Schedules;
        if (schedules is null) return false;
        return schedules[mode].CheckTimeToProcessAndAdvance();
    }

    // fidelity: M11-021
    /// <summary>The CLAHE tile grid the engine sets (<c>setTilesGridSize(4,4)</c> 0x006B45D2, 0xC8E148).</summary>
    internal const int ClaheTiles = 4;
    /// <summary>The clip limit the engine sets (<c>setClipLimit(32.0)</c> 0x006B4630, 0xC8E144).</summary>
    internal const double ClaheClipLimit = 32.0;

    /// <summary>
    /// <c>VisionSystem::ApplyCLAHE(image, 4, out)</c> 0x006B44EC and <c>DetectMarkersWithCLAHE</c>'s pick
    /// 0x006B47A8..0x006B47B4. The enum-4 dark test sets the flag, sums every 3rd byte of every 3rd row and
    /// clears it when <c>sum &gt;= 80*((cols+2)/3)*((rows+2)/3)</c> (80 at 0xC8E14C); a set flag means the
    /// image is dark, so CLAHE runs and its output is returned, otherwise the original image is returned.
    /// The CLAHE output is post-filtered with
    /// <c>boxFilter(out,out,-1,(3,3),(-1,-1),normalize=true,borderType=4)</c> (0x006B4686..0x006B46A8).
    /// The image timestamp copy <c>[image+0x3c] -&gt; [out+0x3c]</c> (0x006B46B4) is represented by passing the
    /// same <c>timestamp</c> to <c>Detector.Detect</c>, since <see cref="GrayImage"/> carries no timestamp.
    ///
    /// The engine calls <c>setTilesGridSize</c>/<c>setClipLimit</c> only when its cached fields
    /// (<c>VisionSystem+0x350</c>/<c>+0x354</c>) differ. Those fields are zero from the ctor and the shipped
    /// values are 4 and 32.0, so the first call always sets both; this stack applies them every call (a
    /// CLAHE object per frame), which is observationally the same and avoids a cached-field guess.
    /// </summary>
    internal static GrayImage SelectMarkerImage(GrayImage image)
    {
        if (!IsDarkForClahe(image)) return image;
        var clahe = OpenCv310.Clahe8U(image.Pixels, image.Height, image.Width, ClaheTiles, ClaheTiles, ClaheClipLimit);
        var filtered = OpenCv310.BoxFilter8UTo8U(clahe, image.Height, image.Width, 3, 3, OpenCv310.BorderReflect101);
        return new GrayImage(image.Width, image.Height, filtered);
    }

    /// <summary>
    /// The enum-4 dark test 0x006B451A..0x006B457E: sum every 3rd byte of every 3rd row; the flag is
    /// cleared (CLAHE skipped) when the sum reaches <c>80*((cols+2)/3)*((rows+2)/3)</c>. Returns true when
    /// the flag stayed set, i.e. CLAHE applies.
    /// </summary>
    internal static bool IsDarkForClahe(GrayImage image)
    {
        long sum = 0;
        for (int y = 0; y < image.Height; y += 3)
            for (int x = 0; x < image.Width; x += 3)
                sum += image.Pixels[y * image.Width + x];
        long threshold = 80L * ((image.Width + 2) / 3) * ((image.Height + 2) / 3);
        return sum < threshold;
    }

    public int FramesProcessed { get; private set; }
    public int FramesDropped { get; private set; }
    public VisionFrameResult? LastResult { get; private set; }

    public event Action<VisionFrameResult>? FrameProcessed;
    public event Action<string>? Log;

    /// <summary>
    /// Reads the calibration from the robot through the shared NV queue (the engine's connection-time
    /// <c>NVStorageComponent::Read</c>). The camera calibration is a factory entry, so the component computes the
    /// request length from the tag (M3-027; <see cref="CameraSettings.CalibrationReadLength"/> = 1) and the reply's
    /// index-0 blob is the 56-byte struct.
    /// </summary>
    public async Task<CameraCalibration?> ReadCalibrationAsync(TimeSpan? timeout = null)
    {
        var nv = _robot.Engine.NvStorage;
        if (nv is null) return null;
        var r = await nv.ReadAsync(CameraCalibration.NvEntryTag, timeout).ConfigureAwait(false);
        foreach (var l in nv.Log) Log?.Invoke(l);
        if (r.Result != 0 || r.Data.Length != CameraSettings.CalibrationBytes) return null;
        var cal = CameraCalibration.Unpack(r.Data);
        // The connection-time callback's rule (1j): a body hardware version <= 6 zeroes the distortion.
        if (_robot.CameraSettings.BodyHwVersion <= 6) cal = cal with { DistortionCoefficients = new double[8] };
        // fidelity: M11-039 — the install path is UpdateCameraCalibration (0x006B1E3E), not a bare set.
        UpdateCameraCalibration(cal);
        return cal;
    }

    /// <summary>The camera at the latest known robot state, for visibility questions asked now.</summary>
    public CameraModel? CurrentCamera()
    {
        if (Calibration is null || History.Latest is not { } pd) return null;
        return new CameraModel(Calibration, pd.CameraPose);
    }

    /// <summary>
    /// Whether the robot is carrying an object, so the movement handler can apply the engine's guard.
    /// The carrying component lives in the docking system, which is built on top of this one, so it
    /// hands the question down rather than this reaching up for it.
    /// </summary>
    public Func<uint, bool>? IsCarryingObject { get; set; }

    private bool Carrying(uint objectId) => IsCarryingObject?.Invoke(objectId) ?? false;

    private void OnMessage(RobotMessage m)
    {
        switch (m)
        {
            case RobotState s:
            {
                // fidelity: M4-020
                // The history and the pose are after UpdateFullRobotState's origin check (SC4f, M4 correction C3): a
                // state the Robot drops before time sync, or whose origin it rejects, does not reach them.
                if (_robot.Engine.Robot?.OriginAccepted(s) != true) break;
                // The robot reports which origin its pose is in. A different one means it has been
                // delocalized - Robot::Delocalize 0x00510A24 allocates the new origin and tells the robot
                // - and everything located in the old one is in a frame that no longer exists. What the
                // robot is carrying moves across with it (0x00510CF0); the rest stops being located, and
                // whoever holds spatial state of their own is told so they can do the same.
                // fidelity: M11-019
                uint before = History.OriginId;
                History.Add(s);
                uint now = History.OriginId;
                if (now != before && before != 0)
                {
                    var carried = CarriedObjectIds();
                    World.OnRobotDelocalized(carried);
                    RobotDelocalized?.Invoke(now);
                }
                break;
            }
            // HandleActiveObjectMoved 0x00533E30 dirties the pose only when the robot is not carrying
            // the object (the guard at 0x00534116); a cube on the lift reporting motion is ignored.
            // fidelity: M11-009
            case ObjectMoved mv:
                // fidelity: M4-009, M4-023
                // HandleActiveObjectMoved (CD10a, 0x00533E4C..0x005341BA): an unknown active id, the charger's garbage
                // moves and a movement inside the double-tap window (step 3) return before SetIsMoving and MarkObjectDirty.
                if (_robot.Cubes.MovedStopsBeforeTheWorld(mv.ObjectID)) break;
                World.SetMoving(mv.ObjectID, true);
                if (!Carrying(mv.ObjectID)) World.MarkDirty(mv.ObjectID);
                break;
            case ObjectStoppedMoving sm:
                // fidelity: M4-009
                // HandleActiveObjectStopped (CD10b, 0x00534636..0x00534AA4): the same lookup and charger filter as Moved;
                // the double-tap test's result is discarded.
                if (_robot.Cubes.StoppedStopsBeforeTheWorld(sm.ObjectID)) break;
                World.SetMoving(sm.ObjectID, false);
                break;
            // A cube that has dropped its radio link cannot be tracked or docked with any more, and its last
            // pose will go stale the moment someone moves it. The engine drops such an object from the world
            // model; here its pose goes Unknown, which is what every located-object query already tests
            // (LOCAL: the engine's ObjectConnectionState handling was not transcribed, only its effect).
            case ObjectConnectionState cs when !cs.Connected: OnCubeDisconnected(cs.ObjectID); break;
            // fidelity: M11-004
            // HandleImageImuData 0x00535C20 appends every image IMU sample to VisionComponent+0xb0's
            // ImuDataHistory (AddImuData 0x00538B24); the rotating gate reads it back.
            case ImageImuData d: Imu.Add(d.ImageId, d.RateX, d.RateY, d.RateZ, d.Line2Number); break;
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
        // fidelity: M11-040 — VisionComponent::SetNextImage 0x00652B04 puts the EncodedImage in the
        // component; the Processor thread 0x00651F08 pops it and calls UpdateVisionSystem(pose, image)
        // 0x00653D30. The mailbox holds one image at a time (the M3 per-frame bound): a frame that arrives
        // while one is being processed is dropped.
        if (!Enabled) return;
        if (Interlocked.CompareExchange(ref _processing, 1, 0) != 0) { FramesDropped++; return; }
        int removal = Volatile.Read(ref _removals);
        Task.Run(() =>
        {
            try { ProcessFrame(f, removal); }
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
    public VisionFrameResult? ProcessFrame(CameraFrame f) => ProcessFrame(f, Volatile.Read(ref _removals));

    // fidelity: M11-033 — VisionSystem::Update(PoseData, EncodedImage) 0x006B4B68: the EncodedImage's
    // IsColor (0x006B4B7C, the encoding byte) chooses DecodeImageRGB (0x006B4B90) or DecodeImageGray
    // (0x006B4C02), the ImageCache is reset with that result, then Update(PoseData, ImageCache) 0x006B4C78
    // runs. The shipped camera is grey, so only the grey member is populated and the colour branch is
    // inert (C2.2).
    private VisionFrameResult? ProcessFrame(CameraFrame f, int removal)
    {
        if (Calibration is null) { WarnNoCalibration(removal); return null; }
        var cache = new ImageCache();
        if (f.IsColor)
        {
            // colour -> DecodeImageRGB + ImageCache::Reset(ImageRGB const&) (0x0087459E, RGB at +0x54)
            if (!f.TryDecodeRgb(out var rgb, out var error)) { Log?.Invoke($"frame {f.ImageId}: {error}"); return null; }
            cache.Reset(rgb!, f.Width, f.Height);
        }
        else
        {
            // fidelity: M3-001
            // A6: a frame that does not decode (A8..A11, policy M3-020) is not processed.
            if (!f.TryDecodeGray(out var gray, out var error)) { Log?.Invoke($"frame {f.ImageId}: {error}"); return null; }
            cache.Reset(gray!);
        }
        return ProcessCapture(cache, f.ImageId, f.Timestamp, removal);
    }

    /// <summary>
    /// The camera path with the image already decoded: pairs the capture with the robot state at its
    /// timestamp and processes it. Split out from <see cref="ProcessFrame(CameraFrame)"/> so the fw2457
    /// timestamp-zero path can be driven without a JPEG. This is <c>VisionSystem::Update(PoseData,
    /// ImageCache)</c> 0x006B4D5C: it requires the pose and image, calls <c>UpdatePoseData</c> 0x006B4D88
    /// and <c>GetGray</c> 0x006B4D94, then dispatches the enabled modes.
    /// </summary>
    // fidelity: M11-033
    public VisionFrameResult? ProcessCapture(GrayImage gray, uint imageId, uint cameraTimestamp)
        => ProcessCapture(gray, imageId, cameraTimestamp, Volatile.Read(ref _removals));

    /// <summary>The same from an <see cref="ImageCache"/>; <c>GetGray</c> supplies the grey image (M11-033).</summary>
    // fidelity: M11-033
    public VisionFrameResult? ProcessCapture(ImageCache cache, uint imageId, uint cameraTimestamp)
        => ProcessCapture(cache, imageId, cameraTimestamp, Volatile.Read(ref _removals));

    private VisionFrameResult? ProcessCapture(ImageCache cache, uint imageId, uint cameraTimestamp, int removal)
    {
        GrayImage gray;
        try { gray = cache.GetGray(); }
        catch (InvalidOperationException e) { Log?.Invoke($"frame {imageId}: {e.Message}"); return null; }
        return ProcessCapture(gray, imageId, cameraTimestamp, removal);
    }

    private VisionFrameResult? ProcessCapture(GrayImage gray, uint imageId, uint cameraTimestamp, int removal)
    {
        var calibration = Calibration;
        if (calibration is null) { WarnNoCalibration(removal); return null; }
        // the fw2457 captures carry FrameTimestamp 0 on every chunk; a frame with no timestamp is paired with the
        // latest state (LOCAL fallback; the engine's EncodedImage rejects a bad timestamp instead)
        var pd = cameraTimestamp == 0 ? History.Latest : History.At(cameraTimestamp);
        if (pd is null) { Log?.Invoke($"frame {imageId}: no robot state to pair with"); return null; }
        // Once the fallback has chosen a state, that state's timestamp is the observation time for the whole
        // frame. Passing the camera's zero through would date every marker, object and face at 0, which makes
        // observation age, face expiry and object-position age meaningless on real fw2457 hardware.
        uint effective = cameraTimestamp == 0 ? pd.Value.Timestamp : cameraTimestamp;
        // fidelity: M11-004 — VisionComponent::WasRotatingTooFast(timestamp, 0.174533, 0.174533, 0)
        // 0x00621C9A..0x00621CAE over the ImuDataHistory at VisionComponent+0xb0 (C3.3). The history is
        // fed from ImageImuData by its first field (the image id/timestamp), the same field the engine's
        // HandleImageImuData passes to AddImuData; a frame with no bracketing sample is treated as rotating
        // (fail-safe true), as the engine's no-IMU-data path returns 1.
        var poseData = WithRotatingGate(pd.Value, effective);
        return ProcessImage(gray, imageId, effective, poseData, calibration, removal,
                            effective != cameraTimestamp ? cameraTimestamp : null);
    }

    /// <summary>
    /// Applies the engine's rotating gate to a frame's pose data: <c>VisionComponent::WasRotatingTooFast</c>
    /// 0x0065359C over the <c>ImuDataHistory</c> at <c>VisionComponent+0xb0</c> (C3.3). Used by both the
    /// capture path and the offline <see cref="ProcessImage(GrayImage, uint, uint, VisionPoseData)"/> path,
    /// so the gate reaches <see cref="BlockWorld"/> either way.
    /// </summary>
    // fidelity: M11-004
    private VisionPoseData WithRotatingGate(VisionPoseData pd, uint timestamp) =>
        pd with { RotatingTooFast = Imu.WasRotatingTooFast(timestamp, BlockWorld.MaxRotationRateRadPerSec) };

    /// <summary>
    /// The core update over a decoded image and the robot's pose data for it (usable offline). Throws
    /// <see cref="OperationCanceledException"/> when the robot is removed while the image is being processed.
    /// </summary>
    public VisionFrameResult ProcessImage(GrayImage gray, uint imageId, uint timestamp, VisionPoseData pd)
    {
        var calibration = Calibration ?? throw new InvalidOperationException("no camera calibration");
        pd = WithRotatingGate(pd, timestamp);
        return ProcessImage(gray, imageId, timestamp, pd, calibration, Volatile.Read(ref _removals), null)
            ?? throw new OperationCanceledException("the robot was removed while this image was being processed");
    }

    // fidelity: M1-025, M1-015
    // The removal checks: a frame started before a removal keeps and raises nothing after it (ResetToConstructed).
    private VisionFrameResult? ProcessImage(GrayImage gray, uint imageId, uint timestamp, VisionPoseData pd,
                                            CameraCalibration calibration, int removal, uint? rawTimestamp)
    {
        var sw = Stopwatch.StartNew();
        var cal = gray.Width == calibration.Columns && gray.Height == calibration.Rows ? calibration : calibration.Scaled(gray.Width, gray.Height);
        var camera = new CameraModel(cal, pd.CameraPose);
        IReadOnlyList<ObservedMarker> markers;
        IReadOnlyList<ObjectObservation> objects;
        IReadOnlyList<ObservableObject> forgotten;
        OverheadEdgeFrame? overheadEdges = null;
        VisionFrameResult result;
        lock (_busy)
        {
            if (RemovedSince(removal)) return null;
            // fidelity: M11-021 — ApplyCLAHE(image, 4, out) 0x006B44EC runs unconditionally before the
            // marker-mode gate; DetectMarkersWithCLAHE picks the original or the CLAHE image from the
            // enum-4 dark-test flag (0x006B47A8..0x006B47B4). The marker-mode gate then decides whether
            // Detect runs at all (0x006B5162..0x006B516A).
            var markerImage = SelectMarkerImage(gray);
            markers = ShouldProcessVisionMode(DetectingMarkers) ? Detector.Detect(markerImage, timestamp) : Array.Empty<ObservedMarker>();
            if (RemovedSince(removal)) return null;
            // fidelity: M11-035, M11-036 — UpdateVisionMarkers (0x00654D60) calls
            // BlockWorld::UpdateObservedMarkers (0x00654DF4) first in the per-mode handler order. The whole
            // frame sequence (M11-037, C3.2) runs inside it: occluders, lift occluder, create/add, then the
            // unobserved check, stacked poses, block configs and markerless objects.
            var worldFrame = World.UpdateObservedMarkers(markers, camera, timestamp, pd);
            objects = worldFrame.Objects;
            forgotten = worldFrame.Forgotten;
            if (RemovedSince(removal)) return null;
            // fidelity: M11-035 — VisionComponent::UpdateAllResults 0x006542EC runs the per-mode result handlers in
            // order: markers (UpdateVisionMarkers 0x006544A2), faces (0x00654510), pets (PetWorld::Update
            // 0x0065457E), motion (0x006545E8), overhead edges (0x00654652), tool code (0x006546BC),
            // computed calibration (0x00654726), image quality (0x00654790), laser points (0x006547FA), then
            // CheckMailbox 0x00654A74 and the RobotProcessedImage broadcast 0x00654A14/0x00654A1C. This
            // stack runs the marker, face, pet and overhead-edge handlers in that relative order; the
            // motion, tool-code, computed-calibration, image-quality and laser-point handlers, CheckMailbox
            // and the RobotProcessedImage layout are not built (see M11-035's unresolved).
            // VisionSystem::Update in DetectingFaces mode: FaceTracker::Update, TrackedFace::UpdateTranslation(camera), FaceWorld::AddOrUpdateFace
            if (FaceDetector.IsAvailable)
            {
                var faces = new List<TrackedFace>();
                var detected = FaceDetector.Detect(gray, timestamp);
                if (RemovedSince(removal)) return null;
                foreach (var d in detected)
                {
                    var tf = new TrackedFace(d, timestamp);
                    tf.UpdateTranslation(camera);
                    faces.Add(tf);
                    Faces.AddOrUpdateFace(tf, pd.RobotPose, pd.RotatingTooFast);
                }
                LastFaces = faces;
                Faces.Update(timestamp);
            }
            if (PetDetector.IsAvailable)
            {
                var pets = PetDetector.Detect(gray, timestamp);
                if (RemovedSince(removal)) return null;
                Pets.Update(pets, timestamp, pd.RotatingTooFast);
            }
            // The ground in front of the robot, on this frame's own pose data: the detector needs the
            // camera where it was when the image was taken and the lift angle it had then, which is what
            // VisionPoseData carries. The points come back in robot coordinates, so whoever puts them in
            // the map uses the same frame's robot pose - which is what the engine's map component looks
            // up by the frame's timestamp (RobotStateHistory::ComputeAndInsertStateAt at 0x0067F8A2).
            if (OverheadEdges is { } edges)
                overheadEdges = edges.Detect(gray, camera, pd.RobotPose, timestamp, pd.LiftAngleRad);
            if (RemovedSince(removal)) return null;
            result = new VisionFrameResult(imageId, timestamp, pd, markers, objects, forgotten, sw.Elapsed)
            {
                OverheadEdges = overheadEdges,
            };
            if (rawTimestamp is { } raw) LastRawFrameTimestamp = raw;
            FramesProcessed++;
            LastResult = result;
            // Raised under the lock a removal waits on, so a frame the removal discarded never raises it afterwards.
            FrameProcessed?.Invoke(result);
        }
        return result;
    }

    /// <summary>
    /// Raised when the robot's state stream reports a different pose origin from the one before: the
    /// robot has been delocalized and everything positioned in the old origin is in a frame that has
    /// gone. The world has already forgotten its located objects by the time this is raised; a caller
    /// with spatial state of its own - the memory map above all, which the engine rebuilds for the new
    /// origin - clears it here.
    /// </summary>
    public event Action<uint>? RobotDelocalized;

    /// <summary>The origin the robot's poses are reported in, as of its last state.</summary>
    public uint OriginId => History.OriginId;

    /// <summary>
    /// Which objects the robot is carrying, so a delocalization can move them into the new origin rather
    /// than forgetting them: they are held, so where they are relative to the robot is still known.
    /// Set by whoever tracks carrying (the manipulation system does).
    /// </summary>
    public Func<IReadOnlySet<uint>>? CarriedObjects { get; set; }

    private IReadOnlySet<uint> CarriedObjectIds()
    {
        try { return CarriedObjects?.Invoke() ?? new HashSet<uint>(); }
        catch { return new HashSet<uint>(); }
    }

    /// <summary>
    /// The overhead-edge detector, or null to leave the ground alone. <c>VisionSystem::Update</c> runs it
    /// as one of its modes; here it is off until something asks for it, because the only consumer is the
    /// memory map and a stack without one has no use for the work.
    /// </summary>
    public OverheadEdgesDetector? OverheadEdges { get; set; }

    public void Dispose()
    {
        _robot.Message -= OnMessage;
        _robot.Camera.FrameForVision -= OnFrame;
        _robot.CameraSettings.CalibrationInstalled -= OnCalibrationInstalled;
        _robot.CameraSettings.VisionEnabledSet -= OnVisionEnabledSet;
        _robot.RobotRemoved -= ResetToConstructed;
        _robot.StateHistoryCleared -= History.Clear;
        _robot.Cubes.DoubleTapPendingEnded -= World.MarkDirty;
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
/// numHalfRevolutions, isAbsolute, actionId)</c>, packed at 0x00640898 / 0x006408F4.
///
/// <b>The angle is absolute, and that is read rather than assumed.</b> <c>TurnInPlaceAction::Init</c>
/// 0x00545FA0 has two paths and both put an absolute heading in the first word. When the action is
/// absolute (+0xC0 set) it writes <c>Radians(requested + variability)</c> straight to +0x9C; when it is
/// relative it computes <c>current + requested</c> into +0x9C at 0x005460EC and keeps the relative amount
/// separately at +0xA4. Either way +0x9C is what goes on the wire, so the field is the absolute body
/// angle in the robot's pose frame. Hardware item L was going to ask the robot this; it did not need to.
///
/// The two paths differ in the fields that follow, and that is what those fields are for:
/// <list type="bullet">
/// <item><c>numHalfRevolutions</c> is 0 on the absolute path and <c>floor(|relative| / π)</c> on the
///   relative one (0x00546164), so a relative turn of more than half a circle tells the robot how many
///   half-turns to take before settling on the angle.</item>
/// <item><c>isAbsolute</c> is +0xC0, normalised to 0 or 1 at 0x0054619C.</item>
/// <item>on the relative path only, the sign of the turn is stuffed into <b>bit 31 of the speed</b> -
///   <c>bfi r1, r0, #0x1f, #1</c> at 0x0054610C - so the speed word carries the direction. The absolute
///   path skips that, which is why a positive speed is right here.</item>
/// <item>the last byte is a counter <c>MovementComponent</c> keeps at +8, incremented per command and
///   handed back to the caller through its out-parameter.</item>
/// </list>
///
/// The speeds are the engine's, from the <c>TurnInPlaceAction</c> constructor 0x005459D4, which stores
/// 5.23599, 10.0 and 25.0 at +0x78 and copies the first two to the action's max speed and acceleration:
/// <b>300 deg/s and 10 rad/s²</b>, with a 2 degree tolerance (0x3D0EFA35) and a 25 revolution bound.
/// This stack had been turning at 100 deg/s.
/// </summary>
// fidelity: M11-014, M11-015
public static class TurnTowardsPose
{
    /// <summary>2 degrees; the <c>TurnInPlaceAction</c> constructor's 0x3D0EFA35 at +0xB0.</summary>
    // fidelity: M11-015
    public const double ToleranceRad = 0.0349066;
    /// <summary>300 deg/s: the constructor's 0x40A78D36, copied to the action's max speed at +0xC4.</summary>
    // fidelity: M11-015
    public const double MaxSpeedRadPerSec = 5.23599;
    /// <summary>The constructor's 0x41200000 at +0x7C, copied to the action's acceleration at +0xC8.</summary>
    // fidelity: M11-015
    public const double AccelRadPerSec2 = 10.0;
    /// <summary>The bound a relative turn is refused above: 25 revolutions (+0x80, checked at 0x0054606E).</summary>
    // fidelity: M11-015
    public const double MaxRevolutions = 25.0;

    /// <summary>The engine's message for a body turn; exposed so the conformance tool can show the bytes.</summary>
    public static SetBodyAngle Message(double absoluteAngleRad, double maxSpeed, double accel, double tolerance,
                                       ushort numHalfRevolutions, bool isAbsolute, byte actionId) => new()
    {
        AngleRad = (float)absoluteAngleRad,
        MaxSpeedRadPerSec = (float)maxSpeed,
        AccelRadPerSec2 = (float)accel,
        ToleranceRad = (float)tolerance,
        NumHalfRevolutions = numHalfRevolutions,
        IsAbsolute = isAbsolute,
        ActionId = actionId,
    };

    /// <summary>
    /// The relative form, as <c>TurnInPlaceAction::Init</c> sends it: the absolute target still goes in
    /// the first word, the half-revolutions are counted off the relative amount, and the sign of that
    /// amount rides in bit 31 of the speed (0x0054610C).
    /// </summary>
    public static SetBodyAngle RelativeMessage(double currentAngleRad, double relativeTurnRad, double maxSpeed,
                                               double accel, double tolerance, byte actionId)
    {
        double absolute = Wrap(currentAngleRad + relativeTurnRad);
        float speed = (float)Math.Abs(maxSpeed);
        uint bits = BitConverter.SingleToUInt32Bits(speed);
        if (relativeTurnRad < 0) bits |= 0x8000_0000u;
        return new SetBodyAngle
        {
            AngleRad = (float)absolute,
            MaxSpeedRadPerSec = BitConverter.UInt32BitsToSingle(bits),
            AccelRadPerSec2 = (float)accel,
            ToleranceRad = (float)tolerance,
            NumHalfRevolutions = (ushort)Math.Floor(Math.Abs(relativeTurnRad) / Math.PI),
            IsAbsolute = false,
            ActionId = actionId,
        };
    }

    /// <summary>What <c>Anki::Radians</c> does to an angle: wrap it to (-pi, pi].</summary>
    private static double Wrap(double rad)
    {
        double r = Math.IEEERemainder(rad, 2 * Math.PI);
        return r <= -Math.PI ? r + 2 * Math.PI : r;
    }

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
        var transport = vision.Robot;   // M1-026: through the app send path (B28, CB26, CB29)
        transport.SendMessage(Message(absolute, MaxSpeedRadPerSec, AccelRadPerSec2, ToleranceRad, 0, true, 2), flush: true);
        if (vision.Calibration is { } cal)
        {
            double head = HeadAngleToSee(cal, robot.Value, target.Translation);
            transport.SendMessage(new SetHeadAngle { AngleRad = (float)head, MaxSpeedRadPerSec = 10f, AccelRadPerSec2 = 10f, DurationSec = 0f, ActionId = 3 }, flush: true);
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
