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
    private bool _warnedNoCalibration;
    /// <summary>The calibration the constructor was given, which a removal restores.</summary>
    private readonly CameraCalibration? _constructedCalibration;
    /// <summary>Bumped by every removal. A frame started before one is discarded: nothing it found is kept or raised.</summary>
    private int _removals;
    /// <summary>How long a removal waits for a frame being processed to reach a point where it can be discarded.</summary>
    internal static readonly TimeSpan RemovalWait = TimeSpan.FromSeconds(2);
    /// <summary>M3-034: the FaceAlbum/Enrollment consumer, kept so Dispose can unsubscribe it.</summary>
    private readonly Action<byte[], byte[]> _faceAlbumLoaded;

    public VisionSystem(CozmoRobot robot, CameraCalibration? calibration = null, MarkerDetector? detector = null, IOkaoFaceRecognizer? okaoRecognizer = null)
    {
        _robot = robot;
        Calibration = calibration;
        _constructedCalibration = calibration;
        Detector = detector ?? new MarkerDetector();
        Faces.Log += l => Log?.Invoke(l);
        // fidelity: M14-011
        Recognizer = new FaceRecognizer(okaoRecognizer ?? new OkaoFaceRecognizer());
        Recognizer.Log += l => Log?.Invoke(l);
        // fidelity: M11-041
        // The world keeps its own connected objects (BlockWorld::AddConnectedActiveObject, fed by ObjectConnectionState below), and
        // reads the carrying and treads state from the components that own them.
        World = new BlockWorld();
        World.IsCarryingObject = id => Carrying(id);
        World.OnTreads = () => robot.Sensors.OffTreadsState == OffTreadsState.OnTreads;
        // fidelity: M11-037
        // VisionComponent::SetPhysicalRobot writes the lift occluder points when the firmware version arrives (0x0051397C). The event fires once, so a system built
        // after the firmware version was handled (the tools build it after connecting) catches up from what the engine robot recorded: PhysicalRobotRecorded is set when the
        // handler got as far as SetPhysicalRobot, independent of the audio output source.
        robot.Engine.PhysicalRobotSet += World.SetPhysicalRobot;
        if (robot.Engine.Robot is { PhysicalRobotRecorded: true } handled) World.SetPhysicalRobot(handled.IsPhysicalRobot);
        // fidelity: M11-044
        // What the localization candidates and the frame sequence read from the robot: its current pose (Robot::GetPose), the robot states behind
        // RobotStateHistory::GetComputedStateAt, the MovementComponent bytes +0xA/+0xC, Robot::GetLastImageTimeStamp and BaseStationTimer::GetCurrentTimeInSeconds.
        World.CurrentRobotPose = () => History.Latest?.RobotPose;
        World.ComputedRobotPoseAt = ts => History.GetComputedStateAt(ts);
        World.MovementBytes = () => robot.State.Latest is { } st
            ? ((st.Status & (uint)RobotStatusFlag.HeadInPos) == 0, (st.Status & (uint)RobotStatusFlag.AreWheelsMoving) != 0)
            : (false, false);
        // UNSOURCED STAND-IN: Robot::GetLastImageTimeStamp 0x00516EC0 is a Vision::Camera getter ("ldr r0,[r0,#0x258]; b 0x8CAE6C"); no citation shows that the larger of the last received
        // camera frame's timestamp and the timestamp of the frame being processed is what it returns (M11-037, M11-044).
        World.LastImageTimestamp = () => Math.Max(robot.Camera.LastFrame?.Timestamp ?? 0u, Volatile.Read(ref _frameTimestamp));
        // fidelity: M11-044
        // The commit to OnTreads (0x005120F6..0x00512188): BlockWorld::AnyRemainingLocalizableObjects decides, and only when nothing remains do Robot+0x2C4 = 1 and Robot+0x2B8 = -1 follow.
        // The classifier (M10 A11) calls these two seams from that spot.
        robot.Sensors.OffTreads.AnyRemainingLocalizableObjects = World.AnyRemainingLocalizableObjects;
        robot.Sensors.OffTreads.NothingLocalizableRemainsOnTreads = World.ClearLocalizationOnTreads;
        World.BaseStationSeconds = () => robot.Engine.Timer.Seconds;
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
        _doubleTapEnded = OnDoubleTapPendingEnded;
        robot.Cubes.DoubleTapPendingEnded += _doubleTapEnded;
        // fidelity: M3-033, M3-034
        // VisionComponent::Init's FaceAlbum/Enrollment reads complete after Gate A, i.e. after
        // CozmoRobot.ConnectAsync returns and the caller builds this VisionSystem. The engine raises the result
        // when #4 completes; subscribe here and adopt a result that already completed.
        _faceAlbumLoaded = AdoptFaceAlbum;
        robot.Engine.ConnectionFaceAlbumLoaded += _faceAlbumLoaded;
        if (robot.Engine.ConnectionFaceAlbumResult is { } loaded)
            _faceAlbumLoaded(loaded.Album, loaded.Enrollment);
    }

    private readonly Action<uint> _doubleTapEnded;

    /// <summary>The timestamp of the image being processed: what <c>Robot::GetLastImageTimeStamp</c> is at least (M11-037).</summary>
    private uint _frameTimestamp;

    // fidelity: M4-023, M11-041
    /// <summary>
    /// CD10g's MarkObjectDirty on the located copy of a cube whose double-tap window ended. The cube is named by its radio slot (activeID), and the
    /// world's ObjectID is the connected object's (<see cref="BlockWorld.ConnectedObjectIdForActiveId"/>); a slot with no connected object does nothing.
    /// </summary>
    private void OnDoubleTapPendingEnded(uint activeId)
    {
        if (World.ConnectedObjectIdForActiveId(activeId) is { } id) World.MarkDirty(id);
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
    // fidelity: M14-011
    /// <summary>
    /// The engine's <c>FaceRecognizer</c> (0x008640E4): the Anki state machine over the OKAO_FR_* seam
    /// (Correction C3).  The stock seam reports itself unavailable, so this runs only when a caller
    /// attaches a working <see cref="IOkaoFaceRecognizer"/>.  The enrollment mode/id chain
    /// (<c>FaceWorld::Enroll</c> -> <c>VisionComponent::SetFaceEnrollmentMode</c> -> ... ->
    /// <c>SetAllowedEnrollments</c>, C1-F11) reaches it through <see cref="SetFaceEnrollmentMode"/>.
    /// </summary>
    public FaceRecognizer Recognizer { get; }
    public IPetDetector PetDetector { get; set; } = new OkaoPetDetector();
    /// <summary>Replaceable body-and-head turn for the face actions (tests move a fake robot with it).</summary>
    public Func<Pose3d, double, CancellationToken, Task<bool>>? TurnOverride { get; set; }
    /// <summary>Replaceable pan-and-tilt (absolute body heading, head angle) for the explorer behaviours.</summary>
    public Func<double, double, CancellationToken, Task<bool>>? PanTiltOverride { get; set; }
    /// <summary>Faces seen in the last processed frame.</summary>
    public IReadOnlyList<TrackedFace> LastFaces { get; private set; } = Array.Empty<TrackedFace>();
    /// <summary>
    /// Whether frames from the camera are processed as they arrive. The engine's VisionComponent +0x48 starts 0
    /// (2a) and is set to 1 only by the NV calibration callback (1j, 2d), on every outcome of that read (M11-049,
    /// 0x0065AE80), so this starts false and the callback turns it on. A caller that drives frames directly through
    /// <see cref="ProcessImage(GrayImage, uint, uint, VisionPoseData)"/> (offline tools and tests) does not go through the gate.
    /// </summary>
    // fidelity: M11-049
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

    /// <summary>Raises the system's log line (used by the face actions' failure paths).</summary>
    internal void LogLine(string line) => Log?.Invoke(line);

    // fidelity: M3-022, M3-033
    /// <summary>
    /// Waits for the connection-time NV calibration read the engine queues once (M3-022, 0x006583FA inside
    /// <c>SendConnectionResponse</c> 0x006583E2..0x0065842C), and returns the calibration the callback installed
    /// (0x0065AB68), or null when <paramref name="timeout"/> elapses first. The read is queued before
    /// <c>CozmoRobot.ConnectAsync</c> returns and completes only after Gate A (M3-026), so a caller that builds
    /// this system after ConnectAsync subscribes here; the constructor's catch-up at line 96 covers a read that
    /// already completed. There is no second 0x80000001 read: the engine reads it once.
    /// </summary>
    public async Task<CameraCalibration?> WaitForConnectionCalibrationAsync(TimeSpan timeout)
    {
        // The constructor's catch-up sets Calibration but does not run the install (MarkerDetector::Init); the
        // removed direct read did, so do it here for a read that completed before this system was built.
        if (Calibration is { } already) { UpdateCameraCalibration(already); return already; }
        var tcs = new TaskCompletionSource<CameraCalibration>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnInstalled(CameraCalibration c) => tcs.TrySetResult(c);
        // Subscribe before the second check so a completion between the two is not missed.
        _robot.CameraSettings.CalibrationInstalled += OnInstalled;
        try
        {
            if (Calibration is { } now) return now;
            var done = await Task.WhenAny(tcs.Task, Task.Delay(timeout)).ConfigureAwait(false);
            return done == tcs.Task ? await tcs.Task.ConfigureAwait(false) : null;
        }
        finally
        {
            _robot.CameraSettings.CalibrationInstalled -= OnInstalled;
        }
    }

    /// <summary>The camera at the latest known robot state, for visibility questions asked now.</summary>
    public CameraModel? CurrentCamera()
    {
        if (Calibration is null || History.Latest is not { } pd) return null;
        return new CameraModel(Calibration, pd.CameraPose);
    }

    // ------------------------------------------------------------------ the face album (M14-012)

    // fidelity: M14-012
    /// <summary>The album's NV tag, 0x184000 (F24).</summary>
    public const uint FaceAlbumNvTag = 0x184000;
    // fidelity: M14-012
    /// <summary>The enrollment data's NV tag, 0x183000 (F24).</summary>
    public const uint FaceEnrollmentNvTag = 0x183000;

    private byte[] _serializedFaceAlbum = Array.Empty<byte>();
    private byte[] _serializedFaceEnrollment = Array.Empty<byte>();

    // fidelity: M14-012
    /// <summary>
    /// The observable replacement for <c>BroadcastLoadedNamesAndIDs</c>'s EngineToGame broadcasts (F27):
    /// <c>RobotErasedAllEnrolledFaces</c> first, then one <c>LoadedKnownFace</c> per entry. This stack has
    /// no EngineToGame channel (the M11-038 precedent), so the events carry the same observable ordering;
    /// the wire broadcasts are the M2/M10 gap.
    /// </summary>
    public event Action? EnrolledFacesErased;
    // fidelity: M14-012
    public event Action<int, string>? LoadedFaceName;

    // fidelity: M14-011, M14-012
    /// <summary>
    /// <c>VisionSystem::SetSerializedFaceData</c> under the vision mutex (F25): stores the two byte
    /// vectors the robot returned and, when the OKAO seam is available, installs them through the
    /// recognizer's <c>SetSerializedData</c> endpoint (C3-24: album restore, enrollment parse,
    /// consistency/capacity install, nextFaceID).  The stock seam reports itself unavailable, so the
    /// bytes are stored and cannot be parsed: that is the M11-016 third-party boundary, not a silent
    /// default.
    /// </summary>
    public void InstallSerializedFaceData(byte[] album, byte[] enrollment)
    {
        lock (_busy)
        {
            _serializedFaceAlbum = album ?? Array.Empty<byte>();
            _serializedFaceEnrollment = enrollment ?? Array.Empty<byte>();
            if (Recognizer.IsAvailable)
                Recognizer.SetSerializedData(_serializedFaceAlbum, _serializedFaceEnrollment);
        }
    }

    // fidelity: M14-011, M14-012
    /// <summary>
    /// <c>VisionSystem::GetSerializedFaceData</c> (F26/C5-12).  When the OKAO seam is available the album
    /// vector is the restored album (whose inverse serialization is OKAO's <c>RestoreAlbum</c> boundary,
    /// M11-016) and the enrollment vector is serialized from the recognizer's enrolled faces only when the
    /// album vector is non-empty (<c>0x00867D5A..0x00867D66</c>); an empty album yields an empty
    /// enrollment.  With the stock unavailable seam the raw vectors last installed are returned.
    /// </summary>
    public (byte[] Album, byte[] Enrollment) GetSerializedFaceData()
    {
        lock (_busy)
        {
            if (Recognizer.IsAvailable)
                return (_serializedFaceAlbum,
                        _serializedFaceAlbum.Length > 0 ? Recognizer.SerializeEnrollment() : Array.Empty<byte>());
            return (_serializedFaceAlbum, _serializedFaceEnrollment);
        }
    }

    // fidelity: M14-009, M14-011
    /// <summary>
    /// <c>VisionComponent::SetFaceEnrollmentMode(pose=0, id, mode)</c> (C1-F11): selects the mode
    /// (4 for a nonzero id, -1 for zero) and stores the target through
    /// <c>FaceRecognizer::SetAllowedEnrollments</c>.
    /// </summary>
    public void SetFaceEnrollmentMode(int id)
    {
        var (_, mode) = Faces.Enroll(id);
        Recognizer.SetAllowedEnrollments(mode, id);
    }

    // fidelity: M14-012
    /// <summary>
    /// <c>VisionComponent::BroadcastLoadedNamesAndIDs</c> (F27): <c>RobotErasedAllEnrolledFaces</c> first,
    /// then one <c>LoadedKnownFace</c> per named entry. The wire broadcasts are M2/M10's.
    /// </summary>
    public void BroadcastLoadedNamesAndIDs()
    {
        EnrolledFacesErased?.Invoke();
        foreach (var f in Faces.Faces)
            if (f.HasName) LoadedFaceName?.Invoke(f.Id, f.Name!);
    }

    // fidelity: M14-012
    /// <summary>
    /// <c>VisionComponent::LoadFaceAlbumFromRobot</c> (F24/F25): reads NV <see cref="FaceAlbumNvTag"/>
    /// first and <see cref="FaceEnrollmentNvTag"/> second, then on the enrollment completion adopts
    /// both. M3-033 owns the connection-time queue; this direct form remains the M14-012 entry the
    /// FaceTests drive. The engine's connection path calls <see cref="AdoptFaceAlbum"/> with the bytes
    /// its #3/#4 reads produced.
    /// </summary>
    public void LoadFaceAlbumFromRobot()
    {
        var nv = _robot.Engine.NvStorage;
        if (nv is null) return;
        var album = new List<byte>();
        nv.Read(FaceAlbumNvTag, _ => { }, album);
        nv.Read(FaceEnrollmentNvTag, r =>
        {
            if (r.Result != NvStorageComponent.ResultOkay) return;
            AdoptFaceAlbum(album.ToArray(), r.Data);
        });
    }

    // fidelity: M14-012
    /// <summary>
    /// The M14-012 consumer: install the two vectors under the vision mutex
    /// (<c>SetSerializedFaceData</c>, 0x0065A876) and replay the loaded names
    /// (<c>BroadcastLoadedNamesAndIDs</c>). Used both by the direct read above and by the engine's
    /// connection-time <c>ConnectionFaceAlbumLoaded</c>.
    /// </summary>
    public void AdoptFaceAlbum(byte[] album, byte[] enrollment)
    {
        InstallSerializedFaceData(album, enrollment);
        BroadcastLoadedNamesAndIDs();
    }

    // fidelity: M14-012
    /// <summary>
    /// <c>VisionComponent::SaveFaceAlbumToRobot</c> (F26/G1-4/C5-13): the two serialized vectors,
    /// size-checked against their NV tags.  The erase path runs only when <b>both</b> vectors are empty,
    /// album 0x184000 first and 0x183000 only if that erase succeeded; otherwise both are rounded up to a
    /// four-byte boundary and written, album first.
    /// </summary>
    public void SaveFaceAlbumToRobot()
    {
        var nv = _robot.Engine.NvStorage;
        if (nv is null) return;
        var (album, enrollment) = GetSerializedFaceData();
        int albumMax = NvStorageComponent.MaxSizeForEntryTag(FaceAlbumNvTag);            // 0x10000
        int enrollmentMax = NvStorageComponent.MaxSizeForEntryTag(FaceEnrollmentNvTag);  // 0x1000
        if (album.Length > albumMax || enrollment.Length > enrollmentMax)
        {
            Log?.Invoke($"SaveFaceAlbumToRobot: serialized data too large (album {album.Length}/{albumMax}, enrollment {enrollment.Length}/{enrollmentMax})");
            return;
        }
        if (album.Length == 0 && enrollment.Length == 0)
        {
            // C5-13: erase the album first, then the enrollment only when the album erase succeeded.
            Log?.Invoke("SaveFaceAlbumToRobot: EmptyAlbumData");
            nv.Request(FaceAlbumNvTag, 0, NvStorageComponent.OpErase, Array.Empty<byte>(), r =>
            {
                if (r.Result != NvStorageComponent.ResultOkay) return;
                nv.Request(FaceEnrollmentNvTag, 0, NvStorageComponent.OpErase, Array.Empty<byte>(), _ => { });
            });
            return;
        }
        WriteAlbumEntry(nv, FaceAlbumNvTag, album);
        WriteAlbumEntry(nv, FaceEnrollmentNvTag, enrollment);
    }

    // fidelity: M14-012
    private static void WriteAlbumEntry(NvStorageComponent nv, uint tag, byte[] data)
    {
        int padded = (data.Length + 3) & ~3;      // the engine's four-byte alignment
        var bytes = new byte[padded];
        Array.Copy(data, bytes, data.Length);
        nv.Request(tag, padded, NvStorageComponent.OpWrite, bytes, _ => { });
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
                // fidelity: M11-044
                // UpdateFullRobotState ORs into Robot+0x2BC (0x00512B56..0x00512B8E) BEFORE the origin lookup (0x00512C54): a state whose origin is rejected still ORs in.
                // Only a state that passed the time-sync gate gets that far (StoredState, CozmoEngine.UpdateFullRobotState).
                if (ReferenceEquals(_robot.Engine.Robot?.StoredState, s))
                    World.NoteRobotState((s.Status & (uint)RobotStatusFlag.HeadInPos) == 0 || (s.Status & (uint)RobotStatusFlag.AreWheelsMoving) != 0,
                                         _robot.Sensors.OffTreadsState != OffTreadsState.OnTreads);
                // fidelity: M11-044
                // The engine's Delocalize trigger (0x00512A62..0x00512A94, 0x00512B88..0x00512BA6): a treads commit that involves OnTreads (CozmoSensors.DelocalizeTrigger). +0x2C0 = 0, Robot::Delocalize,
                // then a jump to 0x00512FB4 that skips the history and pose steps of UpdateFullRobotState: the history below does not take this state. NOT built: the pose steps outside the history
                // (the cliff schedule and the rest of the sensor route still see the state), Delocalize's origin allocation and the (status & 2) argument (it only gates a warning, 0x00510C6A..0x00510C98) and the carried move gated by [[+0x284]+8] != -1 (the carried set decides here). RobotDelocalized carries the OLD origin id: AddNewOrigin is not built (M11-053).
                if (ReferenceEquals(_robot.Sensors.DelocalizeTrigger, s) && ReferenceEquals(_robot.Engine.Robot?.StoredState, s))
                {
                    var carriedNow = CarriedObjectIds();
                    World.DelocalizeOnTreadBoundary(carriedNow);
                    RobotDelocalized?.Invoke(History.OriginId);
                    break;
                }
                if (_robot.Engine.Robot?.OriginAccepted(s) != true) break;
                // STAND-IN (M11-019, not the engine's trigger): the engine has no origin-change trigger for Delocalize. This stack keeps treating a new origin id in the state stream as a
                // delocalization, because it does not allocate origins itself: the robot's own report of a new origin is the only sign it has. Robot::Delocalize 0x00510A24 (allocates the new
                // origin, moves what the robot carries across (0x00510CF0), BlockWorld::OnRobotDelocalized) is what the engine does at the tread boundary above.
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
            {
                // fidelity: M4-009, M4-023
                // HandleActiveObjectMoved (CD10a, 0x00533E4C..0x005341BA): an unknown active id, the charger's garbage
                // moves and a movement inside the double-tap window (step 3) return before SetIsMoving and MarkObjectDirty.
                if (_robot.Cubes.MovedStopsBeforeTheWorld(mv.ObjectID)) break;
                // fidelity: M11-041
                // The message names the cube by its radio slot; the engine finds the connected object of that slot
                // (GetConnectedActiveObjectByActiveIdHelper) and works on its ObjectID. No connected object: nothing more.
                if (World.ConnectedObjectIdForActiveId(mv.ObjectID) is not { } movedId) break;
                World.SetMoving(movedId, true);
                if (!Carrying(movedId)) World.MarkDirty(movedId);
                break;
            }
            case ObjectStoppedMoving sm:
            {
                // fidelity: M4-009
                // HandleActiveObjectStopped (CD10b, 0x00534636..0x00534AA4): the same lookup and charger filter as Moved;
                // the double-tap test's result is discarded.
                if (_robot.Cubes.StoppedStopsBeforeTheWorld(sm.ObjectID)) break;
                // fidelity: M11-041
                if (World.ConnectedObjectIdForActiveId(sm.ObjectID) is not { } stoppedId) break;
                World.SetMoving(stoppedId, false);
                break;
            }
            // fidelity: M11-041
            // HandleActiveObjectConnectionState 0x00533B3C: a connection registers the connected object (AddConnectedActiveObject), a
            // disconnection erases it (RemoveConnectedActiveObject).
            case ObjectConnectionState cs: HandleObjectConnectionState(cs); break;
            // fidelity: M11-004
            // HandleImageImuData 0x00535C20 appends every image IMU sample to VisionComponent+0xb0's
            // ImuDataHistory (AddImuData 0x00538B24); the rotating gate reads it back.
            case ImageImuData d: Imu.Add(d.ImageId, d.RateX, d.RateY, d.RateZ, d.Line2Number); break;
        }
    }

    // fidelity: M11-041, M11-042
    /// <summary>
    /// The engine calls <c>AddConnectedActiveObject(activeID, factoryID, type)</c> for a connection and
    /// <c>RemoveConnectedActiveObject(activeID)</c> for a disconnection (0x00533BD6, 0x00533CA0); <c>Robot::HandleConnectedToObject</c> is
    /// <see cref="CozmoCubes"/>'s (M4), which sees the same message. An unread body that throws (<c>CreateActiveObjectByType</c> for a type it
    /// does not build) is logged, not swallowed.
    /// </summary>
    private void HandleObjectConnectionState(ObjectConnectionState cs)
    {
        uint? worldId = cs.Connected ? null : World.ConnectedObjectIdForActiveId(cs.ObjectID);
        try { World.HandleActiveObjectConnectionState(cs.ObjectID, cs.FactoryID, cs.ObjectType, cs.Connected); }
        catch (NotSupportedException e) { Log?.Invoke($"object {cs.ObjectID}: {e.Message}"); }
        if (!cs.Connected && worldId is { } id) OnCubeDisconnected(id);
    }

    // A cube that has dropped its radio link cannot be tracked or docked with any more, and its last pose will go stale the moment someone
    // moves it. LOCAL: this stack forgets the located object (the engine's disconnection handling does not delete it, M11-042).
    private void OnCubeDisconnected(uint objectId)
    {
        if (World.GetObjectById(objectId) is not { } o || o.PoseState == PoseState.Unknown) return;
        Log?.Invoke($"object {objectId} disconnected: its pose is no longer known");
        World.MarkUnknown(objectId);
    }

    // ------------------------------------------------------------------------------ the mailbox (M11-040, M11-049)

    private readonly object _mailbox = new();
    /// <summary>The one pending image (VisionComponent's "next" slot, +0x74) and the removal epoch it arrived in.</summary>
    private (CameraFrame Frame, int Removal)? _next;
    private bool _processorRunning;

    /// <summary>The Processor thread's sleep between polls: <c>sleep_for(2,000,000 ns)</c> (0x001E8480 built at 0x00651FDA, called at 0x0065225E).</summary>
    // fidelity: M11-040
    internal static readonly TimeSpan ProcessorPollInterval = TimeSpan.FromMilliseconds(2);

    private void OnFrame(CameraFrame f) => HandOverFrame(f);

    /// <summary>
    /// <c>VisionComponent::SetNextImage</c> 0x00652B04 in its asynchronous mode. An image is discarded, with only an info log, while
    /// <see cref="Enabled"/> is false (0x00652B20..0x00652BEA): that byte (VisionComponent+0x48) has one writer, the NV calibration-read callback,
    /// which sets it on every outcome of the read (M11-049, 0x0065AE80), so no image is processed before that callback has run once. The
    /// pause byte (+0x4B) has no writer anywhere (it is only read), so there is no paused state here. Otherwise the image goes into the ONE pending
    /// slot: a slot still occupied is a dropped frame (DropStats, "SetNextImage.DroppedFrame", 0x00653070..0x006530C8) and the newest image
    /// replaces it (latest wins, 0x006530CC..0x00653140). The Processor takes the pending image, processes it, and on completion discards the
    /// current image and any image that arrived meanwhile without counting it (0x00652234..0x0065224C), then sleeps 2 ms and polls again.
    /// NOT BUILT (M11-040): the synchronous mode (<c>SetIsSynchronous</c>, <c>Start</c>/<c>Stop</c>), the timestamp monotonicity test, the pose capture
    /// at hand-in (this stack pairs the pose when it processes) and the drop-statistics events.
    /// </summary>
    // fidelity: M11-040, M11-049
    internal void HandOverFrame(CameraFrame f)
    {
        if (!Enabled) return;
        lock (_mailbox)
        {
            if (_next is { } pending)
            {
                FramesDropped++;
                Log?.Invoke($"VisionComponent.SetNextImage.DroppedFrame: Setting next image with t={f.Timestamp}, but existing next image from t={pending.Frame.Timestamp} not yet processed");
            }
            _next = (f, Volatile.Read(ref _removals));
            if (_processorRunning) return;
            _processorRunning = true;
        }
        Task.Run(Processor);
    }

    /// <summary>
    /// <c>VisionComponent::Processor</c> 0x00651F08: while there is a pending image, take it (0x0065201A..0x00652216), run
    /// <c>UpdateVisionSystem</c> without holding the mailbox lock (0x0065222E), discard the pending slot (0x00652234..0x0065224C), sleep 2 ms
    /// (0x0065225E). The engine's thread polls for as long as it runs; this worker leaves when the slot is empty at a poll and is started again
    /// by the next hand-in, which a frame arriving in the sleep still finds (the loop re-checks after it).
    /// </summary>
    // fidelity: M11-040
    private void Processor()
    {
        while (true)
        {
            (CameraFrame Frame, int Removal) taken;
            lock (_mailbox)
            {
                if (_next is not { } n) { _processorRunning = false; return; }
                taken = n;
                _next = null;
            }
            try { ProcessFrame(taken.Frame, taken.Removal); }
            catch (Exception e) { Log?.Invoke($"frame {taken.Frame.ImageId}: {e.GetType().Name}: {e.Message}"); }
            lock (_mailbox) _next = null;                        // a frame that arrived meanwhile is discarded, not processed and not counted
            Thread.Sleep(ProcessorPollInterval);
        }
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
        // fidelity: M3-018
        // VisionSystem::Update(PoseData, EncodedImage) 0x006B4B68 calls EncodedImage::IsColor at 0x006B4B7C
        // (the encoding byte). The engine's VERIFY(false) log for encoding 0 is emitted here, once per
        // VisionSystem::Update call, not when the frame was assembled.
        bool isColor = EncodedImageDecoder.IsColor(f.Encoding, Log);
        if (isColor)
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
        return ProcessImage(gray, imageId, timestamp, pd, calibration, Volatile.Read(ref _removals), null, offline: true)
            ?? throw new OperationCanceledException("the robot was removed while this image was being processed");
    }

    // fidelity: M1-025, M1-015
    // The removal checks: a frame started before a removal keeps and raises nothing after it (ResetToConstructed).
    private VisionFrameResult? ProcessImage(GrayImage gray, uint imageId, uint timestamp, VisionPoseData pd,
                                            CameraCalibration calibration, int removal, uint? rawTimestamp, bool offline = false)
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
            Volatile.Write(ref _frameTimestamp, timestamp);
            // fidelity: M11-021 — ApplyCLAHE(image, 4, out) 0x006B44EC runs unconditionally before the
            // marker-mode gate; DetectMarkersWithCLAHE picks the original or the CLAHE image from the
            // enum-4 dark-test flag (0x006B47A8..0x006B47B4). The marker-mode gate then decides whether
            // Detect runs at all (0x006B5162..0x006B516A).
            var markerImage = SelectMarkerImage(gray);
            markers = ShouldProcessVisionMode(DetectingMarkers) ? Detector.Detect(markerImage, timestamp) : Array.Empty<ObservedMarker>();
            if (RemovedSince(removal)) return null;
            // fidelity: M11-044 — UpdateVisionMarkers puts a computed state at the result's timestamp in the history before a result with markers reaches BlockWorld
            // (RobotStateHistory::ComputeAndInsertStateAt, 0x00654D92); GetComputedStateAt (Insert P5) finds only those.
            // A failure (no raw state at or after the timestamp) logs (0x00654DE2) and jumps to 0x0065507E: UpdateObservedMarkers is never reached and the function returns 0.
            bool stateComputed = true;
            // M11-050 (IMPLEMENTATION_GAP, NOT BUILT): between this call and UpdateObservedMarkers the engine (a) treats 0x06000000 as a silent drop (0x006550A0), (b) drops the frame when
            // Robot::IsPoseInWorldOrigin(state pose) is false (0x00654E56), (c) drops it when WasRotatingTooFast(key, 0.5236, 0.1745, 0) (0x00654E78; this stack only has the (0.1745, 0.1745, 0) gate of
            // 0x00621C9A), (d) replaces each marker's camera with Robot::GetHistoricalCamera(state, ts) of the computed state (0x00654E8E) and drops markers with a differing timestamp or an invalid key
            // (0x00654F0C..0x00654FDA), and passes only that new list on. This stack uses the camera built from History.At (the nearest raw state) for the markers and the objects instead.
            if (markers.Count > 0)
            {
                if (offline) History.InsertComputedStateAt(timestamp, pd.RobotPose);   // LOCAL: the caller's pose data stands for the raw state
                else stateComputed = History.ComputeAndInsertStateAt(timestamp);
                if (!stateComputed) Log?.Invoke($"frame {imageId}: ComputeAndInsertStateAt failed for timestamp {timestamp}; the markers are not processed");
            }
            // fidelity: M11-035, M11-036 — UpdateVisionMarkers (0x00654D60) calls
            // BlockWorld::UpdateObservedMarkers (0x00654DF4) first in the per-mode handler order. The whole
            // frame sequence (M11-037, C3.2) runs inside it: occluders, lift occluder, create/add, then the
            // unobserved check, stacked poses, block configs and markerless objects.
            if (stateComputed)
            {
                var worldFrame = World.UpdateObservedMarkers(markers, camera, timestamp, pd);
                objects = worldFrame.Objects;
                forgotten = worldFrame.Forgotten;
            }
            else { objects = Array.Empty<ObjectObservation>(); forgotten = Array.Empty<ObservableObject>(); }
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
            // fidelity: M14-008
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
                // fidelity: M14-011
                // FaceTracker::Impl::Update hands each enrollable face to FaceRecognizer::SetNextFaceToRecognize
                // and reads GetRecognitionData (F22); RecognizeFace needs the OKAO feature blob. IFaceDetector
                // does not carry that feature, so the recognizer is not called from here: the feature carrier is
                // M14-010's unrecovered recognition scheduling, and inventing one would be a plausible
                // substitution. The recognizer itself is built and its album/serialization endpoints are wired.
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
        _robot.Cubes.DoubleTapPendingEnded -= _doubleTapEnded;
        // fidelity: M3-033, M3-034
        _robot.Engine.ConnectionFaceAlbumLoaded -= _faceAlbumLoaded;
        _robot.Engine.PhysicalRobotSet -= World.SetPhysicalRobot;
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
        // fidelity: M4-005
        // MA8: TurnInPlace and the direct SetHeadAngle take the shared u8 counter (MC+8), pre-incremented, so the
        // body and head commands here run on the same id sequence as the M4 head and lift actions.
        transport.SendMessage(Message(absolute, MaxSpeedRadPerSec, AccelRadPerSec2, ToleranceRad, 0, true, transport.Motion.NextActionId()), flush: true);
        if (vision.Calibration is { } cal)
        {
            double head = HeadAngleToSee(cal, robot.Value, target.Translation);
            transport.SendMessage(new SetHeadAngle { AngleRad = (float)head, MaxSpeedRadPerSec = 10f, AccelRadPerSec2 = 10f, DurationSec = 0f, ActionId = transport.Motion.NextActionId() }, flush: true);
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
