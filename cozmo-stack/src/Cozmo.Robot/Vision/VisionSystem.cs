using Cozmo.Transport;
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
    public ImageQualityProposal? ImageQuality { get; init; }

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
public sealed partial class VisionSystem : IDisposable
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
        LifetimeProfiler.SourceLog = (l, c, k, t) => robot.Engine.EmitSourceLog(l, c, k, t);
        QualityProcessor.SourceLog = (l,c,k,t) => robot.Engine.EmitSourceLog(l,c,k,t);
        LoadQualityConfiguration();
        Calibration = calibration;
        _constructedCalibration = calibration;
        Detector = detector ?? new MarkerDetector();
        Faces.Log += l => Log?.Invoke(l);
        // fidelity: M14-011
        Recognizer = new FaceRecognizer(okaoRecognizer ?? new OkaoFaceRecognizer());
        Recognizer.Log += l => Log?.Invoke(l);
        Recognizer.SourceLog = (l, c, k, t) => robot.Engine.EmitSourceLog(l, c, k, t);
        // fidelity: M11-041
        // The world keeps its own connected objects (BlockWorld::AddConnectedActiveObject, fed by ObjectConnectionState below), and
        // reads the carrying and treads state from the components that own them.
        World = robot.LocalizationWorld; // preserve constructor map/origin owner, M13-032
        World.SourceLog = (l, c, k, t) => robot.Engine.EmitSourceLog(l, c, k, t);
        // CozmoRobot owns the one stack-log subscription on this shared world.
        robot.LocalizationWorld = World;
        // fidelity: M4-018, M4-017
        // C11.2: read the same localization owner as SetLocalizedTo and the published game status.
        robot.Lights.Cubes.IsLocalized = () => World.Robot2C4 != 0;
        // fidelity: M4-017
        robot.Lights.AttachVisionSystem(this);   // set-recipient + take-pending, atomic with SetHeadlight
        World.IsCarryingObject = id => Carrying(id);
        World.OnTreads = () => robot.Sensors.OffTreadsState == OffTreadsState.OnTreads;
        World.PickingOrPlacing = () => robot.State.Latest?.Has(RobotStatusFlag.IsPickingOrPlacing) ?? false;
        // fidelity: M11-037
        // VisionComponent::SetPhysicalRobot writes the lift occluder points when the firmware version arrives (0x0051397C). The event fires once, so a system built
        // after the firmware version was handled (the tools build it after connecting) catches up from what the engine robot recorded: PhysicalRobotRecorded is set when the
        // handler got as far as SetPhysicalRobot, independent of the audio output source.
        robot.Engine.PhysicalRobotSet += World.SetPhysicalRobot;
        if (robot.Engine.Robot is { PhysicalRobotRecorded: true } handled) World.SetPhysicalRobot(handled.IsPhysicalRobot);
        // fidelity: M11-044
        // What the localization candidates and the frame sequence read from the robot: its current pose (Robot::GetPose), the robot states behind
        // RobotStateHistory::GetComputedStateAt, the MovementComponent bytes +0xA/+0xC, Robot::GetLastImageTimeStamp and BaseStationTimer::GetCurrentTimeInSeconds.
        World.CurrentRobotPose = () => robot.Engine.Robot?.MainPose;
        // fidelity: M1-053
        // Supplied by localization/vision (M11-053/055/035), not the incoming packet or camera receipt time.
        robot.Engine.PublicationPose = robot.Sensors.PublicationPose;
        robot.Engine.PublicationLocalizedTo = () => unchecked((int)(World.LocalizedToObjectId ?? uint.MaxValue));
        robot.Engine.PublicationGameStatus = () => (World.Robot2C4, (sbyte)robot.Sensors.OffTreadsState);
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
        // fidelity: M4-019
        // Robot::Update 0x00513CD8..0x00513E2A: while the platform flag is set and the robot is on its treads, no located charger, or a robot footprint that no longer intersects the
        // charger's, clears it. The world and the robot pose it reads are this system's.
        _platformStep = () => robot.Sensors.UpdateOnChargerPlatformFromWorld(World, World.CurrentRobotPose?.Invoke());
        robot.Engine.ChargerPlatformUpdate = _platformStep;
        Locator = new CubeLocator(this);
        robot.MessageInternal += OnMessage;
        robot.Engine.DelocalizationReported += OnDelocalizationReported;
        // fidelity: M3-005, M3-022
        // Vision gets what HandleImageChunk hands to SetNextImage (the per-tick cap, A3), and the calibration and the
        // enable the connection-time NV read gives (1j).
        robot.Camera.FrameForVision += OnFrame;
        robot.CameraSettings.CalibrationInstalled += OnCalibrationInstalled;
        robot.CameraSettings.VisionEnabledSet += OnVisionEnabledSet;
        if (robot.CameraSettings.Calibration is { } read) UpdateCameraCalibration(read);   // fidelity: M11-039: the callback's install already ran, so the catch-up runs it too
        // A VisionSystem built after the connection's NV callback already fired picks up the enable (2d).
        if (robot.CameraSettings.VisionEnabled) Enabled = true;
        robot.RobotRemoved += ResetToConstructed;
        // fidelity: M1-041
        _historyCleared = () => History.Clear();
        robot.StateHistoryCleared += _historyCleared;
        // fidelity: M4-023
        _doubleTapEnded = OnDoubleTapPendingEnded;
        robot.Cubes.DoubleTapPendingEnded += _doubleTapEnded;
        // fidelity: M4-008
        // A12: HandleObjectPowerLevel reads the BlockWorld at Robot+0x34 unconditionally (0x00537360); the stack's
        // BlockWorld is this VisionSystem's, so the lookup is installed wherever a VisionSystem is built.
        robot.Cubes.ConnectedObjectIdForActiveId = World.ConnectedObjectIdForActiveId;
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
    private readonly Action _platformStep;

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
    /// <c>VisionSystem::UpdateCameraCalibration</c> 0x006B1E3E: <c>Camera::SetCalibration</c> (0x006B1E5E, defined at 0x0085DEDC) stores the shared pointer and returns 1,
    /// except that it returns 0 and stores nothing when the new pointer is null (0x0085DEE6/0x0085DF0C: with a calibration already set it first logs
    /// <c>Camera.SetCalibration.NullCalibration</c>, "VERIFY(calib != nullptr): Camera already calibrated. Cannot set with nullptr.") or is the very pointer already stored
    /// (0x0085DEE8..0x0085DEEC, 0x0085DEEE: identity, not value equality). Only a non-zero result (<c>cbz r6</c> 0x006B1E6C) reaches <c>MarkerDetector::Init</c>
    /// 0x006B1E78, which runs <c>Parameters::Initialize</c> 0x008752F8. The function itself returns 0 always (0x006B1E7C).
    /// </summary>
    public void UpdateCameraCalibration(CameraCalibration? calibration)
    {
        var current = Calibration;
        if (calibration is null)
        {
            if (current is not null)
                Log?.Invoke("Camera.SetCalibration.NullCalibration: VERIFY(calib != nullptr): Camera already calibrated. Cannot set with nullptr.");
            return;
        }
        if (ReferenceEquals(calibration, current)) return;
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
        // fidelity: M11-061, M4-017, M1-025, M4-027
        // CB33/CC26/CC27: removal deletes this owner; a new owner's request deque is empty.
        // The retained-host reset remains the M4-027 candidate; do not replay requests on a later robot.
        lock (_modeGate) _limitedExposureRequests.Clear();
        RemovalInvalidated?.Invoke();
        bool locked = Monitor.TryEnter(_busy, RemovalWait);
        try
        {
            if (!locked) Log?.Invoke($"vision reset: a frame was still being processed after {RemovalWait.TotalSeconds:F0} s; it is discarded at its next check");
            Calibration = _constructedCalibration;
            // B-M11-8 round 2: a removal is expected to leave no robot, so this reset does not go through History's MISSING report.
            (_robot.Engine.Robot?.PoseHistory ?? _noRobotHistory).ResetToConstructed();
            Imu.ResetToConstructed();
            World.ResetToConstructed();
            Faces.ResetToConstructed();
            Pets.ResetToConstructed();
            LastFaces = Array.Empty<TrackedFace>();
            FramesProcessed = 0;
            FramesDropped = 0;
            LastResult = null;
            _robot.CameraSettings.LastProcessedImageTimestamp = 0;
            LastRawFrameTimestamp = null;
            _warnedNoCalibration = false;
        }
        finally { if (locked) Monitor.Exit(_busy); }
    }
    internal event Action? RemovalInvalidated;

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
    // fidelity: M11-069, M11-070
    /// <summary>
    /// The robot's one history, Robot+0x390 (SetNewPose 0x00512708..0x0051271C, AddVisionOnlyState 0x005310D8), read from
    /// the current robot on every use so a RemoveRobot/AddRobot cannot split vision-only and raw states (B-M11-8 item 5).
    /// With no robot there is no native history; a detached one is used and reported as MISSING.
    /// </summary>
    public RobotStateHistory History
    {
        get
        {
            if (_robot.Engine.Robot is { } r) return r.PoseHistory;
            Behavior.SteppedBehavior.ReportMissing("M11-069 VisionSystem.History with no Robot: the native history lives at Robot+0x390 and has no robot-less counterpart; a detached host history is used");
            return _noRobotHistory;
        }
    }
    private readonly RobotStateHistory _noRobotHistory = new();
    private readonly Action _historyCleared;
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
    /// 5 (ReadingToolCode), 6 (ComputingCalibration), 14 (LimitedExposure) start clear: constructor memclr,
    /// then Init enables only the listed modes (OV14).
    /// </summary>
    public const int ShippedModeEnableMask = 0x0002 | 0x0004 | 0x0008 | 0x0010 | 0x0080 | 0x0100 | 0x0200 | 0x8000;

    /// <summary>
    /// The front schedule list (<c>VisionSystem+0xd0</c>); the engine tail-calls
    /// <c>CheckTimeToProcessAndAdvance</c> on its front schedule (0x006B5AB4..0x006B5AC6). The default is
    /// <c>InitDefaultSchedules</c> (all 16 modes {true}, counter 0), so every enabled mode runs every frame.
    /// </summary>
    public AllVisionModesSchedule Schedules { get; set; } = AllVisionModesSchedule.Default;

    /// <summary>The mode enable bitmask at <c>VisionSystem+0xac</c>, from the shipped config (C2.3).</summary>
    private int _modeEnableMask = ShippedModeEnableMask;
    public int ModeEnableMask { get { lock(_modeGate)return _modeEnableMask; } set { lock(_modeGate)_modeEnableMask=value; } }

    // fidelity: M11-061
    // E5: VisionSystem+0xB0. The existing FIFO carries mode 14 and the M3-021 auto-exposure mode-7 request.
    private sealed record PendingVisionMode(int Mode, bool Enable);
    private readonly Queue<PendingVisionMode> _limitedExposureRequests = new();
    private readonly object _modeGate = new();
    internal void QueueLimitedExposure(bool enable)
    {
        QueueVisionMode(14,enable);
    }

    internal void QueueVisionMode(int mode, bool enable)
    {
        lock (_modeGate) _limitedExposureRequests.Enqueue(new(mode,enable));
    }

    // fidelity: M12-006 -- C1 reuses the checked EnableMode body synchronously after unattach.
    internal void EnableModeImmediately(int mode,bool enable)
    {
        LogCallbackDeferral.RunGated(_modeGate,()=>
        {
            int bit = 1 << mode;
            string modeName = ((VisionMode)mode).ToString();
            if (((ModeEnableMask & bit) != 0) != enable)
            {
                string current = string.Join("+", Enumerable.Range(0, 16)
                    .Where(i => (ModeEnableMask & (1 << i)) != 0).Select(i => ((VisionMode)i).ToString()));
                string key=enable?"VisionSystem.EnablingMode":"VisionSystem.DisablingMode";
                string text=enable?$"Adding mode {modeName} to current mode {current}.":$"Removing mode {modeName} from current mode {current}.";
                if(SourceLog is {} sink)LogCallbackDeferral.Run(()=>sink(EngineLogLevel.Info,"VisionSystem",key,text));
                else
                {
                    _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"VisionSystem",key,text);
                    LogCallbackDeferral.Run(()=>SourceLogDispatch.Write(null,Log,EngineLogLevel.Info,null,key,text)); // standalone display; never parsed into engine entries
                }
                if (enable) ModeEnableMask = (ModeEnableMask & ~1) | bit;
                else
                {
                    ModeEnableMask &= ~bit;
                    if (ModeEnableMask == 0) ModeEnableMask = 1;
                }
            }
        });
    }

    private void ApplyQueuedLimitedExposure()
    {
        // E6: EnableMode before pop_front; E7: log before writing +0xAC, and no log for an unchanged bit.
        while (true)
        {
            PendingVisionMode request;
            lock (_modeGate) if (!_limitedExposureRequests.TryPeek(out request!)) return;
            EnableModeImmediately(request.Mode,request.Enable);
            lock (_modeGate)
            {
                // ResetToConstructed can invalidate this owner while its image update is in progress.
                // Pop only the entry just applied; never a new owner's request after a removal cleared it.
                if (_limitedExposureRequests.TryPeek(out var head) && ReferenceEquals(head, request))
                    _limitedExposureRequests.Dequeue();
                else return;
            }
        }
    }

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

    internal event Action<VisionFrameResult>? DockingMarkersProcessed; // M11-064 UpdateVisionMarkers, before LocalHandlerFailed
    public event Action<VisionFrameResult>? FrameProcessed;
    public event Action<string>? Log;
    public SourceLogSink? SourceLog { get; set; }

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
        // The constructor's catch-up ran the install (UpdateCameraCalibration, M11-039) for a read that completed before this system was built.
        if (Calibration is { } already) return already;
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
    /// <c>RobotErasedAllEnrolledFaces</c> first, then one <c>LoadedKnownFace</c> per entry. The
    /// host FIFO captures each report at its native Broadcast point; local engine hooks remain synchronous.
    /// </summary>
    public event Action? EnrolledFacesErased;
    internal event Action? EnrolledFacesErasedInternal;
    // fidelity: M14-012
    public event Action<int, string>? LoadedFaceName;
    internal event Action<int, string>? LoadedFaceNameInternal;

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
    /// then one <c>LoadedKnownFace</c> per named entry, queued to the host FIFO.
    /// </summary>
    public void BroadcastLoadedNamesAndIDs()
    {
        _robot.Engine.QueueAppBroadcast(EnrolledFacesErased); // 0x0065159C
        EnrolledFacesErasedInternal?.Invoke();
        foreach (var f in Faces.Faces)
            if (f.HasName)
            {
                _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"VisionComponent","VisionComponent.BroadcastLoadedNamesAndIDs",FormattableString.Invariant($"broadcasting loaded face id: {f.Id}"));
                _robot.Engine.QueueAppBroadcast(LoadedFaceName,f.Id,f.Name!); // 0x00651612
                LoadedFaceNameInternal?.Invoke(f.Id,f.Name!);
            }
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
        if(nv.Read(FaceAlbumNvTag,null,album)==0)
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.LoadFaceAlbumFromRobot.ReadFaceAlbumDataFail","");
        else if(nv.Read(FaceEnrollmentNvTag,r=>
        {
            if(r.Result==-1)_robot.Engine.EmitSourceLog(EngineLogLevel.Info,null,"VisionComponent.LoadFaceAlbumFromRobot.ReadFaceEnrollDataNotFound","");
            else if(r.Result!=0)_robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.LoadFaceAlbumFromRobot.ReadFaceEnrollDataFail",$"NVResult = {NvStorageComponent.NvResultName(r.Result)}");
            if(r.Result==0)AdoptFaceAlbum(album.ToArray(),r.Data);
        })==0)
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.LoadFaceAlbumFromRobot.ReadFaceEnrollDataFail","");
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","VisionComponent.LoadFaceAlbumToRobot.Initiated","Initiated load of album and enroll data from NVStorage");
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
            case RobotState:
                // Robot-owned admission/current-pose/treads processing runs in Sensors, before this subscriber.
                break;
            // HandleActiveObjectMoved 0x00533E30 dirties the pose only when the robot is not carrying
            // the object (the guard at 0x00534116); a cube on the lift reporting motion is ignored.
            // fidelity: M11-009
            // M4-009: CozmoCubes owns the complete Moved/Stopped located-object update,
            // including its matched-object dirty operation. Do not repeat it here.
            // fidelity: M11-041
            // HandleActiveObjectConnectionState 0x00533B3C: a connection registers the connected object (AddConnectedActiveObject), a
            // disconnection erases it (RemoveConnectedActiveObject).
            // Registration is performed by RouteToDevices before the connection broadcast reaches lights.
            // 0x00533BD6 precedes Robot::Broadcast at 0x00533D2C; do not register twice here.

            // fidelity: M11-004
            // HandleImageImuData 0x00535C20 appends every image IMU sample to VisionComponent+0xb0's
            // ImuDataHistory (AddImuData 0x00538B24); the rotating gate reads it back.
            case ImageImuData d: Imu.Add(d.ImageId, d.RateX, d.RateY, d.RateZ, d.Line2Number); break;
        }
    }

    /// <summary>Relay the native delocalization report to this facade after its queued app delivery (M11-072).</summary>
    // fidelity: M11-019
    private void OnDelocalizationReported(uint origin)=>RobotDelocalized?.Invoke(origin);

    // fidelity: M11-041, M11-042
    /// <summary>
    /// The engine calls <c>AddConnectedActiveObject(activeID, factoryID, type)</c> for a connection and
    /// <c>RemoveConnectedActiveObject(activeID)</c> for a disconnection (0x00533BD6, 0x00533CA0); <c>Robot::HandleConnectedToObject</c> is
    /// <see cref="CozmoCubes"/>'s (M4), which sees the same message. An unread body that throws (<c>CreateActiveObjectByType</c> for a type it
    /// does not build) is logged, not swallowed.
    /// </summary>
    internal void HandleObjectConnectionState(ObjectConnectionState cs)
    {
        using var frameMutation = World.EnterPoseMutation();
        try { World.HandleActiveObjectConnectionState(cs.ObjectID, cs.FactoryID, cs.ObjectType, cs.Connected); }
        catch (NotSupportedException e) { Log?.Invoke($"object {cs.ObjectID}: {e.Message}"); }
        // OV10 C2 retains located poses; removal clears only the active/factory IDs.
    }

    // ------------------------------------------------------------------------------ the mailbox (M11-040, M11-049)

    private readonly object _mailbox = new();
    /// <summary>The one pending image (VisionComponent's "next" slot, +0x74) and the removal epoch it arrived in.</summary>
    private (CameraFrame Frame, int Removal, VisionPoseData? Pose)? _next;
    private bool _processorRunning;
    private Thread? _processorThread;
    private volatile bool _processorRun = true;
    // Native +0x4B has no shipped writer; internal input seam exercises its checked read.
    internal volatile bool SkipProcessorPass;
    private bool _disposed;

    /// <summary>The Processor thread's sleep between polls: <c>sleep_for(2,000,000 ns)</c> (0x001E8480 built at 0x00651FDA, called at 0x0065225E).</summary>
    // fidelity: M11-040
    internal static readonly TimeSpan ProcessorPollInterval = TimeSpan.FromMilliseconds(2);

    private void OnFrame(CameraFrame f)
    {
        if(!Enabled||!_processorRun||_disposed)return;
        if(Calibration is null){WarnNoCalibration(Volatile.Read(ref _removals));return;}
        var pose=QueryFramePose(f.Timestamp);
        if(pose is {} captured)HandOverFrame(f,captured);
    }
    // fidelity: M11-097 — checked SetNextImage caller flag1 (OV12,0x00652C32).
    // Manager-adopted SetNextImage join0x00652C22..0x00652E40: clamp to newest raw time before flag1 query.
    private VisionPoseData? QueryFramePose(uint timestamp)
    {
        var history=History;
        VisionPoseData? Query()
        {
            using var value=new HistRobotState();uint time=0;
            uint result=history.ComputeStateAt(Math.Min(timestamp,history.NewestTimeStamp),ref time,value,true);
            if(result==0)
            {
                // M11-049: PoseData construction after0x00652CFC is unread. Retain the existing host pairing's
                // camera/motion fields beyond this checked robot state; no new PoseData construction is inferred.
                return RobotStateHistory.PoseData(time,value);
            }
            var sink=SourceLog??((SourceLogSink)((l,c,k,t)=>_robot.Engine.EmitSourceLog(l,c,k,t)));
            if(result==0x06000000)
                sink(EngineLogLevel.Info,"Unnamed","VisionComponent.SetNextImage.OriginMismatch",$"Could not get pose data for t={timestamp} due to origin mismatch. Returning OK");
            else
            {
                var sizes=history.FrameQueryHistorySummary();
                sink(EngineLogLevel.Warning,null,"VisionComponent.SetNextImage.StateHistoryFail",$"Unable to get computed pose at image timestamp of {timestamp}. (rawStates: have {sizes.RawCount} from {sizes.RawOldest}:{sizes.RawNewest}) (visionStates: have {sizes.VisionCount} from {sizes.VisionOldest}:{sizes.VisionNewest})");
            }
            return null;
        }
        // Snapshot selection, native query and the retained host pose conversion share every native tree writer's gate.
        if(history.NativePoseTreeGate is not {} tree)return Query();
        VisionPoseData? pose=null;LogCallbackDeferral.RunGated(tree,()=>pose=Query());return pose;
    }

    /// <summary>
    /// <c>VisionComponent::SetNextImage</c> 0x00652B04 in its asynchronous mode. An image is discarded, with only an info log, while
    /// <see cref="Enabled"/> is false (0x00652B20..0x00652BEA): that byte (VisionComponent+0x48) has one writer, the NV calibration-read callback,
    /// which sets it on every outcome of the read (M11-049, 0x0065AE80), so no image is processed before that callback has run once. The
    /// pause byte (+0x4B) has no writer anywhere (it is only read), so there is no paused state here. Otherwise the image goes into the ONE pending
    /// slot: a slot still occupied is a dropped frame (DropStats, "SetNextImage.DroppedFrame", 0x00653070..0x006530C8) and the newest image
    /// replaces it (latest wins, 0x006530CC..0x00653140). The Processor takes the pending image, processes it, and on completion discards the
    /// current image and any image that arrived meanwhile without counting it (0x00652234..0x0065224C), then sleeps 2 ms and polls again.
    /// NOT BUILT (M11-040): the synchronous mode (<c>SetIsSynchronous</c>, <c>Start</c>/<c>Stop</c>), the timestamp monotonicity test
    /// and the drop-statistics events. Production captures the checked robot state at hand-in; remaining PoseData fields after0x00652CFC stay unread.
    /// </summary>
    // fidelity: M11-040, M11-049
    internal void HandOverFrame(CameraFrame f)
        => HandOverFrame(f,null); // direct unit hand-in; production supplies its checked captured state
    private void HandOverFrame(CameraFrame f,VisionPoseData? pose)
    {
        if (!Enabled) return;
        lock (_mailbox)
        {
            if (!_processorRun || _disposed) return;
            if (_next is { } pending)
            {
                FramesDropped++;
                Log?.Invoke($"VisionComponent.SetNextImage.DroppedFrame: Setting next image with t={f.Timestamp}, but existing next image from t={pending.Frame.Timestamp} not yet processed");
            }
            _next = (f, Volatile.Read(ref _removals),pose);
            if (_processorThread is not null) return;
            _processorThread = new Thread(Processor) { IsBackground = true };
            _processorThread.Start();
        }
    }

    // fidelity: M11-055, M11-040
    // OV13 V2b: poll until +0x4A is cleared, skip on +0x4B, and sleep on every pass.
    // Thread creation remains the host's lazy start; native Init/start is M11-059.
    private void Processor()
    {
        try
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "Unnamed", "VisionComponent.Processor",
                "Starting Robot VisionComponent::Processor thread...");
            Thread.CurrentThread.Name = "VisionSystem"; // pthread_self/setname_np, 0x00651F54..0x00651F5C
            SteppedBehavior.ReportMissing("M11-055 Processor VisionPoseData/IMU/Mat/pose temporary copy/swap/release 0x0065204A..0x00652200 remains M11-040/M11-077/M11-078; the mailbox carries a host CameraFrame, not native shared-control ownership");
            while (_processorRun)
            {
                (CameraFrame Frame, int Removal, VisionPoseData? Pose)? taken = null;
                lock (_mailbox)
                {
                    if (!SkipProcessorPass && _next is { } n)
                    {
                        taken = n;
                        _next = null;
                        _processorRunning = true;
                    }
                }
                if (taken is { } frame)
                {
                    bool completed=false;
                    try { ProcessEncodedFrame(frame.Frame, frame.Removal,frame.Pose);completed=true; }
                    catch (Jpeg.OpenCvException e) { ReportDecodeThrow(frame.Frame,e); }
                    catch (Exception e) { completed=true;Log?.Invoke($"frame {frame.Frame.ImageId}: {e.GetType().Name}: {e.Message}"); }
                    finally
                    {
                        lock (_mailbox)
                        {
                            if(completed)_next = null; // native completion only follows a returned Update; a throw keeps later arrivals
                            _processorRunning = false;
                            Monitor.PulseAll(_mailbox);
                        }
                    }
                }
                Thread.Sleep(ProcessorPollInterval);
            }
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "Unnamed", "VisionComponent.Processor",
                "Terminated Robot VisionComponent::Processor thread");
        }
        finally
        {
            lock (_mailbox) { _processorRunning = false; Monitor.PulseAll(_mailbox); }
        }
    }
    internal void WaitForProcessorIdle()
    {
        lock (_mailbox) while (_processorRunning || (_processorRun && _next is not null)) Monitor.Wait(_mailbox);
    }

    // V1 then V2; imported join is represented by Thread.Join without a timeout.
    private void StopProcessor()
    {
        _robot.Engine.EmitSourceLog(EngineLogLevel.Debug, "Unnamed",
            "ImageBase.CloseAllDisplayWindows.NoDisplayOnAndroidOrIOS", "");
        _robot.Engine.EmitSourceLog(EngineLogLevel.Debug, "Unnamed",
            "ImageBase.CloseAllDisplayWindows.NoDisplayOnAndroidOrIOS", "");
        Thread? thread;
        lock (_mailbox) { _processorRun = false; thread = _processorThread; }
        thread?.Join();
        lock (_mailbox) { _next = null; Monitor.PulseAll(_mailbox); }
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
    // fidelity: M11-058 — synchronous host entry. Native direct calibration/dot-test entry remains unbuilt.
    private VisionFrameResult? ProcessFrame(CameraFrame f, int removal)
    {
        try{return ProcessEncodedFrame(f,removal);}
        catch(Jpeg.OpenCvException e){ReportDecodeThrow(f,e);return null;}
    }
    private static void ReportDecodeThrow(CameraFrame f,Jpeg.OpenCvException e)
        => SteppedBehavior.ReportMissing($"M1-034/M11-058 decode throw: skipped image {f.ImageId} ({e.Message}); native cleanup unwinds to terminate at 0x004E39F8; no Toc, cache Reset, rollback or Processor completion");
    // Host diagnostic snapshot only; native persistent cache/tree ownership is still M11-033/M11-078.
    internal ImageCache EncodedFrameCache {get;private set;}=new();
    private VisionFrameResult? ProcessEncodedFrame(CameraFrame f,int removal,VisionPoseData? capturedPose=null)
    {
        var cache = new ImageCache(); // retain per-call host buffers so separate host entries cannot corrupt a frame
        SteppedBehavior.ReportMissing("M11-033/M11-078 native ImageCache ResetHelper size-tree and image shared-control ownership 0x008744E6..0x0087465C are not replaced by host buffer assignment; bounded decoded image/valid flags only");
        SteppedBehavior.ReportMissing("M11-058/M11-055 DecodeJPEG Tic/Toc 0x006B4B76/0x006B4C6C native timing producer unbuilt; no host timer substituted");
        // fidelity: M3-018
        // VisionSystem::Update(PoseData, EncodedImage) 0x006B4B68 calls EncodedImage::IsColor at 0x006B4B7C
        // (the encoding byte). The engine's VERIFY(false) log for encoding 0 is emitted here, once per
        // VisionSystem::Update call, not when the frame was assembled.
        bool isColor = EncodedImageDecoder.IsColor(f.Encoding, Log);
        var decoded=EncodedImageDecoder.DecodeForCache(f,isColor,Log,SourceLog??((l,c,k,t)=>_robot.Engine.EmitSourceLog(l,c,k,t)));
        if(decoded.PolicySkipped)return null; // M1-034/M3-037: skip the whole decode, including cache Reset/publication.
        cache.ResetDecoded(decoded.Image,isColor); // native Reset even for nonzero Result; throw bypasses this
        EncodedFrameCache=cache; // publish after Reset; a throw leaves the previous diagnostic snapshot
        if(!decoded.Ok)return DecodeReturnedFailure();
        return ProcessCapture(cache, f.ImageId, f.Timestamp, removal,capturedPose);
        VisionFrameResult? DecodeReturnedFailure()
        {
            // Reset already ran; the unbuilt native Toc above belongs only to this normal-return tail.
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.UpdateVisionSystem.UpdateFailed","");
            return null;
        }
    }

    /// <summary>
    /// The camera path with the image already decoded: pairs the capture with the robot state at its
    /// timestamp and processes it. Split out from <see cref="ProcessFrame(CameraFrame)"/> so the fw2457
    /// timestamp-zero path can be driven without a JPEG. This is <c>VisionSystem::Update(PoseData,
    /// ImageCache)</c> 0x006B4D5C: it requires initialization and calibration, calls <c>UpdatePoseData</c> 0x006B4D88
    /// and <c>GetGray</c> 0x006B4D94, then dispatches the enabled modes.
    /// </summary>
    // fidelity: M11-033
    public VisionFrameResult? ProcessCapture(GrayImage gray, uint imageId, uint cameraTimestamp)
        => ProcessCapture(gray, imageId, cameraTimestamp, Volatile.Read(ref _removals));

    /// <summary>The same from an <see cref="ImageCache"/>; <c>GetGray</c> supplies the grey image (M11-033).</summary>
    // fidelity: M11-033
    public VisionFrameResult? ProcessCapture(ImageCache cache, uint imageId, uint cameraTimestamp)
        => ProcessCapture(cache, imageId, cameraTimestamp, Volatile.Read(ref _removals));

    private VisionFrameResult? ProcessCapture(ImageCache cache, uint imageId, uint cameraTimestamp, int removal,VisionPoseData? capturedPose=null)
    {
        GrayImage gray;
        try { gray = cache.GetGray(); }
        catch (InvalidOperationException e) { Log?.Invoke($"frame {imageId}: {e.Message}"); return null; }
        return ProcessCapture(gray, imageId, cameraTimestamp, removal,capturedPose);
    }

    private VisionFrameResult? ProcessCapture(GrayImage gray, uint imageId, uint cameraTimestamp, int removal,VisionPoseData? capturedPose=null)
    {
        var calibration = Calibration;
        if (calibration is null) { WarnNoCalibration(removal); return null; }
        var pd=capturedPose??QueryFramePose(cameraTimestamp);
        if (pd is null)return null;
        // Query selection affects the captured state; processing/observations retain the image timestamp.
        uint effective=cameraTimestamp;
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

    /// <summary>
    /// <c>Robot::IsPoseInWorldOrigin(pose)</c> 0x0051290C: the pose's root is the world origin (<c>[[robot+0x294]+0x14]</c>, 0x005128FC). The computed state's root is the origin of the raw state it
    /// came from, so this compares that origin id with the engine robot's current origin. The offline test link accepts whatever origin its states report
    /// (<see cref="EngineRobot.OfflineSeamAcceptsAnyOrigin"/>), so there the states are in the world origin by construction.
    /// </summary>
    // fidelity: M11-050
    private bool IsPoseInWorldOrigin(uint stateOriginId) =>
        _robot.Engine.Robot is not { } er || er.OfflineSeamAcceptsAnyOrigin || stateOriginId == er.CurrentOriginId;

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
            // fidelity: M11-061 — E6, before ShouldProcessVisionMode dispatch in this image update.
            ApplyQueuedLimitedExposure();
            // fidelity: M11-060
            _robot.CameraSettings.ApplyPendingCameraParams(); // 0x006B50C8..0x006B50D8, before CLAHE/dispatch
            // fidelity: M11-021 — ApplyCLAHE(image, 4, out) 0x006B44EC runs unconditionally before the
            // marker-mode gate; DetectMarkersWithCLAHE picks the original or the CLAHE image from the
            // enum-4 dark-test flag (0x006B47A8..0x006B47B4). The marker-mode gate then decides whether
            // Detect runs at all (0x006B5162..0x006B516A).
            var markerImage = SelectMarkerImage(gray);
            markers = ShouldProcessVisionMode(DetectingMarkers) ? Detector.Detect(markerImage, timestamp) : Array.Empty<ObservedMarker>();
            if (RemovedSince(removal)) return null;
            // Separate producers from recipients: a mode-7 failure publishes none of this frame's modes.
            var detectedFaces = ShouldProcessVisionMode((int)VisionMode.DetectingFaces) && FaceDetector.IsAvailable ? FaceDetector.Detect(gray,timestamp) : null;
            var detectedPets = ShouldProcessVisionMode((int)VisionMode.DetectingPets) && PetDetector.IsAvailable ? PetDetector.Detect(gray,timestamp) : null;
            if(ShouldProcessVisionMode((int)VisionMode.DetectingOverheadEdges) && OverheadEdges is {} edges) overheadEdges=edges.Detect(gray,camera,pd.RobotPose,timestamp,pd.LiftAngleRad);
            if(RemovedSince(removal))return null;
            // fidelity: M11-080, M11-057
            ImageQualityProposal? quality=null;
            if(ShouldProcessVisionMode((int)VisionMode.CheckingQuality))
            {
                if(!TryCheckFrameQuality(gray,out uint qualityStatus,out var proposed))
                {
                    // Offline ProcessImage is a host helper with explicit decoded pixels/pose, not the native Update entry.
                    // A live frame with unknown quality input is stopped without forging a native failure/log.
                    if(!offline)return null;
                }
                else if(qualityStatus!=0)
                {
                    _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionSystem.Update.CheckImageQualityFailed","");
                    EngineErrorState.StoreAndMaybeBreak();
                    return null;
                }
                else
                {
                    quality=proposed;
                    SteppedBehavior.ReportMissing("M11-080/M11-035 mode-7 native processed-mask/result deque 0x006B566A..0x006B568C and UpdateImageQuality 0x0065558C..0x006556E8 are not replaced by the host FrameProcessed proposal; native result serialization/recipient remains unbuilt");
                }
            }
            // fidelity: M11-044 — UpdateVisionMarkers puts a computed state at the result's timestamp in the history before a result with markers reaches BlockWorld
            // (RobotStateHistory::ComputeAndInsertStateAt, 0x00654D92); GetComputedStateAt (Insert P5) finds only those.
            // A failure (no raw state at or after the timestamp) logs (0x00654DE2) and jumps to 0x0065507E: UpdateObservedMarkers is never reached and the function returns 0.
            bool stateComputed = true;
            var keptMarkers = markers;
            // fidelity: M11-036, M11-050
            // UpdateVisionMarkers 0x00654D60..0x006550AC, for a result that has markers (an empty list goes straight to UpdateObservedMarkers, 0x00654D76..0x00654DEC): after the computed state
            // (0x00654D92) the engine (b) drops the frame, logging, when Robot::IsPoseInWorldOrigin(state pose) is false (0x00654E56, log 0x00655028..0x00655074), (c) drops it, silently, when
            // WasRotatingTooFast(t, body 0x3F060A92, head 0x3E32B8C2, 0) (0x00654E62..0x00654E7E): NOT BUILT, see below, (d) takes Robot::GetHistoricalCamera(state, ts) (0x00654E8E) and drops each marker whose
            // timestamp differs from the result's (error log, 0x00654F0C), and passes only the kept list to UpdateObservedMarkers (0x00654DEC..0x00654DF4). A drop in (a)..(c) returns without
            // calling it, so none of the world's per-frame sequence runs for that frame.
            // NOT BUILT (c): the rotation gate. The engine's ImuDataHistory keys its samples by a timestamp that ImuDataHistory::CalculateTimestampForImageIMU 0x00538C00 computes later, from SetNextImage
            // (0x00652C16) with the image's own timestamp, the constant 65.0f (0x42820000) and line2Number; GetImuDataBeforeAndAfter 0x00538D82 needs two samples and takes the first sample STRICTLY after t.
            // This stack keys samples by ImageId (a frame counter on fw2457) and has image timestamp 0 there, so it cannot build the engine's keys: the gate is skipped, never turned into a drop.
            // (a) the 0x06000000 silent branch (0x00654D9A; the producer, M11-052's origin-mismatch return, is not built, so ComputeAndInsertStateAt cannot return it), the IsValidKey warning
            // drop (0x00654F24; the key table at RobotStateHistory+0x28 has no counterpart and the check cannot fail right after the insert), the post-call BlockWorldUpdateFailed warning (0x00654DFA; the
            // world returns no Result) and the DockingComponent::UpdateDockingErrorSignal call for a non-empty kept list (0x00654E4A; M12's DockingSystem runs from FrameProcessed). The historical camera is
            // the camera built from pd above: History.At is the nearer raw state, which is what the interpolation stand-in of ComputeAndInsertStateAt stores (M11-052).
            if (markers.Count > 0)
            {
                uint stateOrigin = 0;
                bool originKnown = false;
                if (offline) History.InsertComputedStateAt(timestamp, pd.RobotPose);   // LOCAL: the caller's pose data stands for the raw state
                else { stateComputed = History.ComputeAndInsertStateAt(timestamp, out stateOrigin); originKnown = stateComputed; }
                if (!stateComputed)
                    Log?.Invoke($"VisionComponent.UpdateVisionMarkers.HistoricalPoseNotFound: Time: {timestamp}, hist: {History.OldestTimeStamp} to {History.NewestTimeStamp}");
                else if (originKnown && !IsPoseInWorldOrigin(stateOrigin))
                {
                    stateComputed = false;
                    Log?.Invoke($"VisionComponent.UpdateVisionMarkers.OldOrigin: Ignoring observed marker from origin {stateOrigin} (robot origin is {_robot.Engine.Robot?.CurrentOriginId})");
                }
                else
                {
                    var kept = new List<ObservedMarker>(markers.Count);
                    foreach (var m in markers)
                    {
                        if (m.Timestamp != timestamp)
                        {
                            Log?.Invoke($"VisionComponent.UpdateVisionMarkers.MismatchedTimestamps: Marker t={m.Timestamp} vs. ProcResult t={timestamp}");
                            continue;
                        }
                        kept.Add(m);
                    }
                    keptMarkers = kept;
                }
            }
            // fidelity: M11-035, M11-036 — UpdateVisionMarkers (0x00654D60) calls
            // BlockWorld::UpdateObservedMarkers (0x00654DF4) first in the per-mode handler order. The whole
            // frame sequence (M11-037, C3.2) runs inside it: occluders, lift occluder, create/add, then the
            // unobserved check, stacked poses, block configs and markerless objects.
            if (stateComputed)
            {
                var worldFrame = World.UpdateObservedMarkers(keptMarkers, camera, timestamp, pd);
                objects = worldFrame.Objects;
                forgotten = worldFrame.Forgotten;
                // fidelity: M11-064 — warning, docking signal (even after failure), then caller's error.
                if (worldFrame.Status != 0)
                    _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.UpdateVisionResults.BlockWorldUpdateFailed","");
                if (keptMarkers.Count != 0)
                    DockingMarkersProcessed?.Invoke(new VisionFrameResult(imageId,timestamp,pd,keptMarkers,objects,forgotten,sw.Elapsed));
                if (worldFrame.Status != 0)
                {
                    _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionComponent.UpdateAllResults.LocalHandlerFailed","DetectingMarkers Failed");
                    EngineErrorState.StoreAndMaybeBreak();
                }

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
            if (detectedFaces is {} detected)
            {
                var faces = new List<TrackedFace>();
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
            if (detectedPets is {} pets)
            {
                if (RemovedSince(removal)) return null;
                Pets.Update(pets, timestamp, pd.RotatingTooFast);
            }
            // The ground in front of the robot, on this frame's own pose data: the detector needs the
            // camera where it was when the image was taken and the lift angle it had then, which is what
            // VisionPoseData carries. The points come back in robot coordinates, so whoever puts them in
            // the map uses the same frame's robot pose - which is what the engine's map component looks
            // up by the frame's timestamp (RobotStateHistory::ComputeAndInsertStateAt at 0x0067F8A2).
            if (RemovedSince(removal)) return null;
            result = new VisionFrameResult(imageId, timestamp, pd, markers, objects, forgotten, sw.Elapsed)
            {
                OverheadEdges = overheadEdges,
                ImageQuality = quality,
            };
            if (rawTimestamp is { } raw) LastRawFrameTimestamp = raw;
            FramesProcessed++;
            LastResult = result;
            // fidelity: M11-035
            _robot.CameraSettings.LastProcessedImageTimestamp = result.Timestamp;
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
    /// Handlers run while the vision frame gate is held. They must not block waiting for vision work.
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
        // This is a host API precondition: native Robot teardown runs on its owner thread.
        // Reject before changing state, so a frame callback cannot join its own worker.
        lock (_disposeGate)
        {
            if (_disposed) return;
            if (ReferenceEquals(Thread.CurrentThread, _processorThread))
                throw new InvalidOperationException("Dispose VisionSystem outside its Processor callback.");
            _disposed = true;
        }
        // Do not hold a host lock across join or callbacks on the worker's exit log.
        StopProcessor();
        // V3-V7 depend on native recipient ownership, not host reset/GC.
        SteppedBehavior.ReportMissing("M11-055 V3-V7 0x00652580..0x00652718 / 0x006B1E92..0x006B2142: native VisionSystem/member ownership and recursive recipient teardown are not built; host subscriptions are detached after join");
        // fidelity: M11-078, M11-090 — checked leaves do not establish the missing constructed recipient graph.
        SteppedBehavior.ReportMissing("M11-078/M11-090 Vision teardown: native result lists/deque 0x006586EC..0x00658A92/0x006B9F68..0x006BA092, ImageCache null-before-tree 0x006B1E9C..0x006B2142, Camera/ImageCache/CLAHE, FaceRecognizer trees, Block/MarkerlessObject and ProtoHandle reset/remove need their owned native recipients; host caches/GC are not those owners. Capture finalizers and allocator recipients remain unread; SDK CustomObject is unreachable");
        Recognizer.Shutdown();
        SteppedBehavior.ReportMissing("M11-055 Profiler timer producers/Tic/Toc and VisionSystem::Init PerformanceLogging binding 0x006B0E5C..0x006B0F2A are not built; native timer input boundary only, no host Stopwatch data substituted");
        LifetimeProfiler.Dispose();
        if (_robot.Lights.DetachVisionSystem(this))
        {
            _robot.Lights.Cubes.IsLocalized = () => _robot.Sensors.OffTreads.Robot2C4 != 0;
        }
        _robot.MessageInternal -= OnMessage;
        _robot.Engine.DelocalizationReported -= OnDelocalizationReported;
        _robot.Camera.FrameForVision -= OnFrame;
        _robot.CameraSettings.CalibrationInstalled -= OnCalibrationInstalled;
        _robot.CameraSettings.VisionEnabledSet -= OnVisionEnabledSet;
        _robot.RobotRemoved -= ResetToConstructed;
        _robot.StateHistoryCleared -= _historyCleared;
        _robot.Cubes.DoubleTapPendingEnded -= _doubleTapEnded;
        // fidelity: M3-033, M3-034
        _robot.Engine.ConnectionFaceAlbumLoaded -= _faceAlbumLoaded;
        _robot.Engine.PhysicalRobotSet -= World.SetPhysicalRobot;
        if (_robot.Engine.ChargerPlatformUpdate == _platformStep) _robot.Engine.ChargerPlatformUpdate = _robot.ChargerPlatformStepWithoutWorld;   // only if it is still the installed one
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
        // fidelity: M11-008
        // The cube-moved strategy's call (0x0060BCF0..0x0060BD08): r3 = 0 (movs r3,#0 at 0x0060BCFE) is the minimum marker size, and the stack words are
        // requireSomethingBehind = 0, xPad = 0, yPad = 0. The 40 px of World.MinVisibleMarkerSizePx belongs to CheckForUnobservedObjects (0x006220F0) only.
        return o.IsVisibleFrom(cam, BlockWorld.VisibilityNormalAngleRad, 0, 0, 0, out _);
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
    public const uint ToleranceBits = 0x3D0EFA35;   // movw/movt 0x00545A78..0x00545A80, Radians(float) at 0x00545A84 into +0xB0
    public static readonly double ToleranceRad = BitConverter.Int32BitsToSingle(unchecked((int)ToleranceBits));
    /// <summary>300 deg/s: the constructor's 0x40A78D36, copied to the action's max speed at +0xC4.</summary>
    // fidelity: M11-015
    public const uint MaxSpeedBits = 0x40A78D36;    // movw/movt 0x00545A28/0x00545A32, stored to +0x78 at 0x00545A44
    public static readonly double MaxSpeedRadPerSec = BitConverter.Int32BitsToSingle(unchecked((int)MaxSpeedBits));
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
    /// <c>Robot::ComputeHeadAngleToSeePose(pose, &amp;angle, tolerance)</c> 0x00518344..0x00518620 (M13-021), the engine's own iteration. The pose (already in the robot frame) is taken with respect to the neck pose ([robot+0x2CC]:
    /// (-13, 0, 49) in the robot frame, 0x0051838E; a failure is "Robot.ComputeHeadAngleToSeePose.OriginMismatch" and 0x06000000, impossible with one pose origin); <c>dist = sqrtf(x*x + y*y)</c>,
    /// <c>P = (dist, 0, z)</c> (0x005183A6..0x005183E4). Without a camera calibration: "NullCamera" and 1 (0x0051851E). The threshold is <c>(float)rows * tolerance + 1e-5f</c> (0x00518432..0x00518436),
    /// the angle starts at 0.0f and the loop runs while the counter <c>k</c> (from 1) is below 25: the camera pose for the angle is the head-cam pose pre-multiplied by a rotation about Y of -angle
    /// (<c>Robot::GetCameraPose</c> 0x00510FFC), <c>Q = inverse * P</c> (0x00518448..0x00518480); <c>Q.z &lt;= 1e-5f</c> is "BadProjectedZ" and 1 (0x00518494); <c>dv = fy * (Q.y / Q.z)</c>; when
    /// <c>threshold &gt;= |dv|</c> (0x005184AE) the angle is the answer (0x005185B2) unless k is 25, which is "MaxIterations" and 1 (0x005185C6..0x00518600); otherwise <c>angle += atan2f(dv, fy) * -0.8f</c>
    /// (0x005184BC..0x005184DA, -0.8f = 0xBF4CCCCD). After the 25th non-converged iteration the function returns 0 with the angle UNASSIGNED (the counter compared at 0x005185C6 is then 26): the caller's
    /// Radians local, 0, is the result.
    /// <para>Scalar operations are binary32 in the engine's order. The pose algebra (the neck pose, <c>GetInverse</c>, the quaternion rotation of P) is this stack's double
    /// <see cref="Pose3d"/>, narrowed to binary32 for Q: the engine's own Transform3d/Rotation3d operations were not read here, MISSING (reported once) for the last bits.</para>
    /// </summary>
    // fidelity: M13-021
    public static uint ComputeHeadAngleToSeePose(CameraCalibration? cal, Pose3d poseInRobotFrame, float tolerance, out float angle, Action<string>? log = null)
    {
        angle = 0.0f;
        Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("Robot::ComputeHeadAngleToSeePose 0x00518344: the neck-pose composition, GetInverse and the rotation of P (0x004A4864, 0x004A8194, Transform3d) are this stack's double Pose3d algebra narrowed to binary32, not the engine's quaternion operations; the loop's scalar operations are binary32");
        var wrt = poseInRobotFrame.WithRespectTo(new Pose3d(Mat3.Identity, HeadGeometry.NeckPositionMm)).Translation;     // 0x0051838E GetWithRespectTo(pose, [robot+0x2CC]); the pose arrives in the robot frame (0x0054A94C)
        float tx = (float)wrt.X, ty = (float)wrt.Y, tz = (float)wrt.Z;
        float sq = tx * tx;
        sq = sq + ty * ty;
        float dist = MathF.Sqrt(sq);                                                          // vsqrt.f32 0x005183BA
        var p = new Vec3(dist, 0.0, tz);
        if (cal is null) { log?.Invoke("Robot.ComputeHeadAngleToSeePose.NullCamera"); return 1; }
        float fy = (float)cal.FocalLengthY;
        float threshold = (float)(ushort)cal.Rows * tolerance;
        threshold += BitConverter.Int32BitsToSingle(0x3727C5AC);
        float eps = BitConverter.Int32BitsToSingle(0x3727C5AC);
        float a = 0.0f;
        Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("vision model head-cam z differs from the engine's (-8.0): blocked on M11 solvePnP");
        var camInNeck = new Pose3d(HeadGeometry.DefaultHeadCamRotation, HeadGeometry.EngineHeadCamPositionMm);   // the engine's own composition (Robot::Robot 0x0050FF7C..0x0050FFBA); the vision model still uses the older z
        for (int k = 1; k <= 25; k++)
        {
            var cam = HeadGeometry.GetCameraPose(camInNeck, a);                                   // GetCameraPose(a) 0x00510FFC relative to the neck: RotateBy(RotationVector3d(Radians(-a), Y)) (R28, R22)
            var q = cam.Inverse().Apply(p);
            float qy = (float)q.Y, qz = (float)q.Z;
            if (qz <= eps) { log?.Invoke("Robot.ComputeHeadAngleToSeePose.BadProjectedZ"); return 1; }
            float dv = fy * (qy / qz);
            if (threshold >= MathF.Abs(dv))
            {
                if (k == 25) { log?.Invoke("Robot.ComputeHeadAngleToSeePose.MaxIterations"); return 1; }
                angle = (float)Cozmo.Robot.Manipulation.EngineRadians32.Rescale(a);               // Radians::operator= 0x005185B6
                return 0;
            }
            float step = MathF.Atan2(dv, fy);
            step = step * BitConverter.Int32BitsToSingle(unchecked((int)0xBF4CCCCD));
            a = a + step;
        }
        return 0;                                                                              // 0x005185C6: the counter is 26, not 25
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
