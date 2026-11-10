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
    private readonly object _groundGate = new();
    private readonly List<IDisposable> _groundDeferredRelease = new();

    private void ReleaseGround(IDisposable? frame)
    {
        if (frame is null) return;
        if (Monitor.IsEntered(_mailbox)) { _groundDeferredRelease.Add(frame); return; }
        frame.Dispose();
    }

    private void DrainGroundReleases()
    {
        if (Monitor.IsEntered(_mailbox)) return;
        IDisposable[] frames;
        lock (_mailbox) { frames = _groundDeferredRelease.ToArray(); _groundDeferredRelease.Clear(); }
        foreach (var frame in frames) frame.Dispose();
    }
    private EngineRobot? _groundOwner;
    private CameraCalibration? _groundOuterCalibration;
    private CameraCalibration? _groundPendingOuter;
    private CameraCalibration? _groundInstalled;
    private NativeGroundPlaneLookup? _groundLookup;
    private int _groundEpoch;
    private bool _nativeWorkerPhysical; // worker+0x50: ctor false, refreshed by SetCameraCalibration tail
    internal bool NativeWorkerPhysicalForTest { get { lock(_groundGate)return _nativeWorkerPhysical; } }
    private NativeVisionInitialization? _nativeInitialization;
    private int _nativeFaceEpoch;
    private int _nativeInitializationEpoch;
    private readonly System.Collections.Concurrent.ConcurrentQueue<NativeVisionInitialization> _retiredMotionStates=new();
    private void DrainRetiredMotionStates()
    {
        while(_retiredMotionStates.TryDequeue(out var state)){state.MotionPose.Dispose();state.StockPet?.Retire();}
    }
    private NativeImageImuHistory? _nativeImu;
    private bool _nativeVisionWhileMoving;
    private RetainedSubscription? _nativeImuSubscription;
    private RetainedSubscription? _nativeVisionWhileMovingSubscription;
    internal int NativeGroundEpochForTest { get { lock (_groundGate) return _groundEpoch; } }
    internal Action<IReadOnlyList<ImageQualityRectangle>>? NativeQualityRectanglesObservedForTest;
    internal Action<CameraCalibration,Pose3d>? NativeMarkerCameraObservedForTest;
    internal Func<IReadOnlyList<ObservedMarker>,IReadOnlyList<ObservedMarker>>? NativeMarkerListForTest;
    internal readonly record struct NativeMarkerCarrier(ObservedMarker Marker, uint CameraId);
    internal Func<IReadOnlyList<NativeMarkerCarrier>,IReadOnlyList<NativeMarkerCarrier>>? NativeMarkerCarriersForTest;
    internal Action<RobotStateHistory,uint>? NativeMarkerKeyComputedForTest;
    internal NativeVisionInitialization? NativeInitializationForTest { get { lock (_groundGate) return _nativeInitialization; } }
    internal bool NativeMotionReadyFor(EngineRobot owner)
    {
        lock(_groundGate)return ReferenceEquals(_groundOwner,owner)&&!owner.Lifetime.Destroying&&
            _nativeImu is not null&&_nativeInitialization is { AvailabilityKnown:true,Initialized:true,MotionKnowledgeKnown:true,Motion:not null } state&&ReferenceEquals(state.Owner,owner);
    }
    internal bool NativeMotionOwnerCurrent(EngineRobot owner)
    {
        lock(_groundGate)return ReferenceEquals(_groundOwner,owner)&&!owner.Lifetime.Destroying&&ReferenceEquals(_robot.Engine.Robot,owner);
    }

    // 0x65805C: explicit original-owner recipient; message subscription is a separate source boundary.
    internal void SetNativeVisionWhileMoving(EngineRobot owner,bool enabled)
    {
        lock(_groundGate)if(ReferenceEquals(_groundOwner,owner)&&!owner.Lifetime.Destroying&&ReferenceEquals(_robot.Engine.Robot,owner))_nativeVisionWhileMoving=enabled;
    }
    internal bool NativeWorldHasImuBracketForTest(EngineRobot owner,uint timestamp)
    {
        lock(_groundGate)
        {
            if(!ReferenceEquals(_groundOwner,owner)||_nativeImu is null)return false;
            NativeImageImu before=default,after=default;
            return NativeImageImuHistory.TryBracket(_nativeImu.Snapshot(),timestamp,ref before,ref after);
        }
    }
    // UpdateVisionMarkers 0x00654E60: zero-count head then body gate at the resolved history time.
    private bool NativeMarkerRotatingTooFast(EngineRobot owner,uint timestamp,Action<EngineLogLevel,string?,string,string> sourceLog,Action<string> missing)
    {
        NativeImageImu[]? history=null;
        lock(_groundGate)
        {
            if(ReferenceEquals(_groundOwner,owner)&&!owner.Lifetime.Destroying&&ReferenceEquals(_robot.Engine.Robot,owner)&&_nativeImu is not null)
            { if(_nativeVisionWhileMoving)return false;history=_nativeImu.Snapshot(); }
        }
        if(history is null)
        {missing("M11-050 original marker IMU owner unavailable");return true;}
        NativeImageImu before=default,after=default;
        if(!NativeImageImuHistory.TryBracket(history,timestamp,ref before,ref after))
        {
            sourceLog(EngineLogLevel.Info,"VisionComponent","VisionComponent.VisionComponent.WasHeadRotatingTooFast.NoIMUData",$"Could not get next/previous imu data for timestamp {timestamp}");
            return true;
        }
        float head=BitConverter.UInt32BitsToSingle(0x3E32B8C2),body=BitConverter.UInt32BitsToSingle(0x3F060A92);
        if(MathF.Abs(before.RateY)>head || MathF.Abs(after.RateY)>head)return true;
        return MathF.Abs(before.RateZ)>body || MathF.Abs(after.RateZ)>body;
    }
    // M11-004: 0x621C9A queries this original owner's LIVE VC+B0 history in the world handler.
    internal bool NativeWorldRotatingTooFast(EngineRobot owner,uint timestamp)
    {
        NativeImageImu[]? history=null;
        lock(_groundGate)
        {
            if(ReferenceEquals(_groundOwner,owner)&&!owner.Lifetime.Destroying&&ReferenceEquals(_robot.Engine.Robot,owner)&&_nativeImu is not null)
            {if(_nativeVisionWhileMoving)return false;history=_nativeImu.Snapshot();}
        }
        if(history is null)
        {SteppedBehavior.ReportMissing("M11-004 original live IMU owner unavailable at world rotation gate");return true;}
        NativeImageImu before=default,after=default;
        if(!NativeImageImuHistory.TryBracket(history,timestamp,ref before,ref after))
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.WasHeadRotatingTooFast.NoIMUData","No IMU data for world rotation query");
            return true;
        }
        float threshold=BitConverter.Int32BitsToSingle(unchecked((int)0x3E32B8C2));
        if(MathF.Abs(before.RateY)>threshold||MathF.Abs(after.RateY)>threshold)return true;
        return MathF.Abs(before.RateZ)>threshold||MathF.Abs(after.RateZ)>threshold;
    }

    // Docking 0x0063BEA4..0x0063BEBE calls WasBodyRotatingTooFast with its own threshold.
    internal bool NativeDockingRotatingTooFast(EngineRobot owner, uint timestamp)
    {
        NativeImageImu[]? history = null;
        lock (_groundGate)
        {
            if (ReferenceEquals(_groundOwner, owner) && !owner.Lifetime.Destroying &&
                ReferenceEquals(_robot.Engine.Robot, owner) && _nativeImu is not null)
            {
                if (_nativeVisionWhileMoving) return false;
                history = _nativeImu.Snapshot();
            }
        }
        if (history is null)
        {
            SteppedBehavior.ReportMissing("M12-019 original docking IMU owner unavailable");
            return true;
        }
        NativeImageImu before = default, after = default;
        if (!NativeImageImuHistory.TryBracket(history, timestamp, ref before, ref after))
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning, null, "VisionComponent.WasBodyRotatingTooFast.NoIMUData", "No IMU data for docking rotation query");
            return true;
        }
        float threshold = BitConverter.UInt32BitsToSingle(0x41B758B4);
        if (MathF.Abs(before.RateZ) > threshold) return true;
        return !(MathF.Abs(after.RateZ) <= threshold);
    }

    // FaceWorld 0x004F456E uses a different body threshold and history count from marker processing.
    internal bool? NativeFaceRotatingTooFast(EngineRobot owner, uint timestamp, bool changedTrack)
        => NativeObservationRotatingTooFast(owner,timestamp,changedTrack,"M14-008 native face");

    private bool? NativeObservationRotatingTooFast(EngineRobot owner, uint timestamp, bool changedTrack, string label, Action? noBracket = null)
    {
        NativeImageImu[]? history = null;
        lock (_groundGate)
        {
            if (ReferenceEquals(_groundOwner, owner) && !owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, owner) && _nativeImu is not null)
            {
                if (_nativeVisionWhileMoving) return false;
                history = _nativeImu.Snapshot();
            }
        }
        if (history is null)
        {
            SteppedBehavior.ReportMissing($"{label} rotation gate has no original-owner IMU history");
            return null;
        }
        return NativeFaceRotationFromHistory(history, timestamp, changedTrack, noBracket);
    }

    internal static bool NativeFaceRotationFromHistory(IReadOnlyList<NativeImageImu> history, uint timestamp, bool changedTrack, Action? noBracket = null)
    {
        float head = BitConverter.UInt32BitsToSingle(0x3E32B8C2);
        float body = BitConverter.UInt32BitsToSingle(0x3F060A92);
        if (!changedTrack)
        {
            NativeImageImu before = default, after = default;
            if (!NativeImageImuHistory.TryBracket(history, timestamp, ref before, ref after)) { noBracket?.Invoke(); return true; }
            return MathF.Abs(before.RateY) > head || MathF.Abs(after.RateY) > head ||
                   MathF.Abs(before.RateZ) > body || MathF.Abs(after.RateZ) > body;
        }
        if (history.Count == 0 || history[0].ComputedTimestamp == 0) return false;
        int candidate = -1;
        for (int i = 0; i < history.Count; i++)
        {
            uint stamp = history[i].ComputedTimestamp;
            int difference = i == 0 ? int.MaxValue : unchecked((int)(history[i - 1].ComputedTimestamp - timestamp));
            int magnitude = difference < 0 ? unchecked(-difference) : difference;
            if (stamp > timestamp || (i > 0 && stamp == 0 && magnitude <= 64)) { candidate = i; break; }
        }
        if (candidate < 0) return false;
        for (int i = candidate; i >= 0 && candidate - i < 5; i--)
        {
            var sample = history[i];
            float x = MathF.Abs(sample.RateX), y = MathF.Abs(sample.RateY), z = MathF.Abs(sample.RateZ);
            if (x > 0 && y > head && z > 0) return true;
        }
        for (int i = candidate; i >= 0 && candidate - i < 5; i--)
        {
            var sample = history[i];
            float x = MathF.Abs(sample.RateX), y = MathF.Abs(sample.RateY), z = MathF.Abs(sample.RateZ);
            if (x > 0 && y > 0 && z > body) return true;
        }
        return false;
    }
    // The outer VisionComponent camera has its own identity check, separate from the inner marker camera.
    internal void BindNativeGroundOwner(EngineRobot owner)
    {
        RetainedSubscription? oldSubscription,oldMovingSubscription;
        var imu=new NativeImageImuHistory();
        lock (_groundGate)
        {
            bool sameOwner=ReferenceEquals(_groundOwner,owner);
            uint receivedTimestamp=sameOwner ? _nativeInitialization?.LastReceivedTimestamp ?? 0 : 0;
            if(!sameOwner)_robot.CameraSettings.LastProcessedImageTimestamp=0;
            oldSubscription=_nativeImuSubscription;_nativeImuSubscription=null;
            oldMovingSubscription=_nativeVisionWhileMovingSubscription;_nativeVisionWhileMovingSubscription=null;_nativeImu=imu;
            if(_nativeInitialization is {} displaced)_retiredMotionStates.Enqueue(displaced);
            if (_groundOwner is not null) _groundPendingOuter = _constructedCalibration;
            _groundOwner = owner;
            Pets.BindNativeOwner(owner);
            _nativeWorkerPhysical=false;
            _nativeVisionWhileMoving=false;
            _nativeInitialization=new NativeVisionInitialization(owner) { LastReceivedTimestamp=receivedTimestamp };_nativeInitializationEpoch++;
            PendingDropStatsSeconds=-1f; // per-owner diagnostic; native Init's source is a local value
            _groundOuterCalibration = null;
            _groundInstalled = null;
            _groundLookup = new NativeGroundPlaneLookup();
            _groundEpoch++;
            lock(_modeGate){_modeEnableMask=0;_schedules.Clear();}
            _exposurePipeline=null;QualityProcessor.Pipeline=null;
        }
        oldSubscription?.Dispose();oldMovingSubscription?.Dispose();
        if(Monitor.TryEnter(_busy,RemovalWait))
        {try{DrainRetiredMotionStates();}finally{Monitor.Exit(_busy);}}
        RetainedSubscription? subscription=owner.SubscribeMessage(RobotMessageId.ImageGyro,m=>
        {
            lock(_groundGate)
                if(ReferenceEquals(_groundOwner,owner)&&ReferenceEquals(_nativeImu,imu)&&!owner.Lifetime.Destroying)
                    imu.Add((ImageImuData)m);
        },"VisionComponent image IMU");
        lock(_groundGate)
        {
            if(ReferenceEquals(_groundOwner,owner)&&ReferenceEquals(_nativeImu,imu)&&!owner.Lifetime.Destroying)
            { _nativeImuSubscription=subscription;subscription=null; }
        }
        subscription?.Dispose();
        RetainedSubscription? movingSubscription=owner.Engine.NeedsContextExternalInterfaceAvailable
            ? owner.Engine.AnimationMessages.SubscribeMessage(0x86,false,p=>
              {if(p is bool enabled)SetNativeVisionWhileMoving(owner,enabled);}) : null;
        lock(_groundGate)
            if(ReferenceEquals(_groundOwner,owner)&&!owner.Lifetime.Destroying)
            {_nativeVisionWhileMovingSubscription=movingSubscription;movingSubscription=null;}
        movingSubscription?.Dispose();
    }

    internal void SetNativeOuterCalibration(CameraCalibration calibration,EngineRobot? expectedOwner=null)
    {
        EngineRobot? owner;
        int epoch;
        lock (_groundGate)
        {
            owner = _groundOwner;
            if(expectedOwner is not null && !ReferenceEquals(owner,expectedOwner))return;
            if (owner is null || owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, owner)) { _groundPendingOuter=calibration; return; }
            if (ReferenceEquals(_groundOuterCalibration, calibration)) { if (ReferenceEquals(_groundPendingOuter,calibration)) _groundPendingOuter=null; _nativeWorkerPhysical=owner.IsPhysicalRobot; return; }
            _groundOuterCalibration = calibration;
            _groundPendingOuter = null;
            epoch = ++_groundEpoch;
            // Non-null Camera::SetCalibration and detector initialization have no callbacks.
            // Keep owner admission and the inner install in one native-lifetime interval.
            UpdateCameraCalibration(calibration);
        }
        lock (_groundGate)
            if (!ReferenceEquals(_groundOwner, owner) || owner.Lifetime.Destroying || epoch != _groundEpoch || !ReferenceEquals(_groundOuterCalibration, calibration)) return;
        InstallNativeGroundCalibration(calibration);
        lock (_groundGate)
            if (ReferenceEquals(_groundOwner,owner) && !owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot,owner) &&
                ReferenceEquals(_groundOuterCalibration,calibration) && (_groundEpoch==epoch || _groundEpoch==epoch+1))
                _nativeWorkerPhysical=owner.IsPhysicalRobot;
    }

    private void InstallNativeGroundCalibration(CameraCalibration calibration)
    {
        EngineRobot? owner;
        NativeGroundPlaneLookup candidate;
        int epoch;
        lock (_groundGate)
        {
            owner = _groundOwner;
            if (owner is null || owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, owner) || !ReferenceEquals(_groundOuterCalibration, calibration) || ReferenceEquals(_groundInstalled, calibration) || _groundLookup is null) return;
            epoch = _groundEpoch;
            candidate = _groundLookup.Copy();
        }
        bool populated = false;
        LogCallbackDeferral.RunGated(owner.PoseTreeGate, () => populated = candidate.Populate(owner, calibration));
        bool unavailable = false;
        lock (_groundGate)
        {
            if (!ReferenceEquals(_groundOwner, owner) || owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, owner) || epoch != _groundEpoch) return;
            if (!populated)
            {
                _groundLookup = null;
                unavailable = true;
            }
            else { _groundLookup = candidate; _groundInstalled = calibration; }
            _groundEpoch++;
        }
        if (unavailable) SteppedBehavior.ReportMissing("M11-017 ground-plane LUT pose input unavailable; retained native lookup history cannot be established");
    }
    /// <summary>Bumped by every removal. A frame started before one is discarded: nothing it found is kept or raised.</summary>
    private int _removals;
    /// <summary>How long a removal waits for a frame being processed to reach a point where it can be discarded.</summary>
    internal static readonly TimeSpan RemovalWait = TimeSpan.FromSeconds(2);
    /// <summary>M3-034: the FaceAlbum/Enrollment consumer, kept so Dispose can unsubscribe it.</summary>
    private readonly Action<byte[], byte[]> _faceAlbumLoaded;
    private readonly bool _stockRecognizer;
    private readonly Action<NativeObservedObject> _nativeObjectObserved;
    private readonly RetainedSubscription _renameRequest;
    private readonly RetainedSubscription _eraseRequest;
    private readonly RetainedSubscription _fileAlbumRequest;

    internal VisionSystem(CozmoRobot robot, CameraCalibration? calibration = null, MarkerDetector? detector = null, IOkaoFaceRecognizer? okaoRecognizer = null)
    {
        _robot = robot;
        _stockFaceDetector = CreateStockFaceDetector();
        FaceDetector = _stockFaceDetector;
        LifetimeProfiler.SourceLog = (l, c, k, t) => robot.Engine.EmitSourceLog(l, c, k, t);
        QualityProcessor.SourceLog = (l,c,k,t) => robot.Engine.EmitSourceLog(l,c,k,t);

        Calibration = calibration;
        _constructedCalibration = calibration;
        _groundPendingOuter = calibration;
        Detector = detector ?? new MarkerDetector();
        Faces.Log += l => Log?.Invoke(l);
        // fidelity: M14-011
        _stockRecognizer = okaoRecognizer is null;
        Recognizer = CreateFaceRecognizer(okaoRecognizer ?? new NativeOkaoFaceRecognizer(_stockFaceDetector));
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
        Faces.NativePublish=(owner,tag,payload)=>
        {
            if(owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,owner) || !owner.HasExternalInterface)return;
            _robot.Engine.PublishFaceIdReport(tag,payload);
        };
        Faces.NativeGeneration=()=>Volatile.Read(ref _removals);
        World.NativeLocalizationGeneration=()=>Volatile.Read(ref _removals);
        World.NativeObjectObserved=_nativeObjectObserved=observation=>
        {
            if(_robot.Engine.Robot?.HasExternalInterface!=true)return;
            SteppedBehavior.ReportMissing("M11-038 native44 complete app report unavailable; checked immutable LOCAL prefix only, no truncated wire");
            _robot.Engine.AnimationMessages.Deliver(0x44,true,observation);
        };
        Faces.NativeMainPose=()=>_robot.Engine.Robot?.GetCheckedMainPose();
        Faces.NativePoseScope=()=>World.EnterPoseMutation();
        Faces.NativeOwner=()=>_robot.Engine.Robot; // M14-009: IsPoseInWorldOrigin reads this robot's current origin
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
        // Robot::GetLastImageTimeStamp 0x00516EC0 -> VisionComponent+0xE8 (0x006527A2).
        // Result handlers see the previous consumed result; the existing finally tail publishes this result afterward.
        World.LastImageTimestamp = () => robot.CameraSettings.LastProcessedImageTimestamp;
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
        robot.CameraSettings.NativeCalibrationInstalled += OnCalibrationInstalled;
        robot.CameraSettings.VisionEnabledSet += OnVisionEnabledSet;
        if (robot.CameraSettings.Calibration is { } read) UpdateCameraCalibration(read);   // fidelity: M11-039: the callback's install already ran, so the catch-up runs it too
        // A VisionSystem built after the connection's NV callback already fired picks up the enable (2d).
        if (robot.CameraSettings.VisionEnabled) Enabled = true;
        robot.RobotRemoved += ResetToConstructed;
        robot.Engine.VisionUpdateAllResultsFailed=DrainPreparedResults;
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
        if(!_stockRecognizer)robot.Engine.ConnectionFaceAlbumLoaded += _faceAlbumLoaded;
        if (!_stockRecognizer && robot.Engine.ConnectionFaceAlbumResult is { } loaded)
            _faceAlbumLoaded(loaded.Album, loaded.Enrollment);
        _renameRequest=robot.Engine.AnimationMessages.SubscribeMessage(0x8C,false,value=>
        {
            try
            {
                if(value is NativeRenameFaceRequest request && robot.Engine.Robot is {} owner)
                    RenameStockEnrolledFace(owner,request);
            }
            catch(Exception error)
            {
                robot.Engine.EmitSourceLog(EngineLogLevel.Error, null, "VisionComponent.RenameFace.HandlerFailed", error.Message);
            }
        });
        _eraseRequest=robot.Engine.AnimationMessages.SubscribeMessage(0x89,false,value=>
        {
            try
            {
                if(value is NativeEraseFaceRequest request)
                    EraseStockEnrolledFace(request.Owner,request.Id);
            }
            catch(Exception error)
            {
                robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionComponent.EraseFace.HandlerFailed",error.Message);
            }
        });
        _fileAlbumRequest=robot.Engine.AnimationMessages.SubscribeMessage(0x8F,false,value=>
        {
            if(value is NativeLoadFaceAlbumFileRequest request && ReferenceEquals(request.Vision,this))
                HandleFileAlbumRequest(request);
        });
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
    private void OnCalibrationInstalled(EngineRobot owner, CameraCalibration c)
    {
        SetNativeOuterCalibration(c,owner);
    }

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
            {
                _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"Camera.SetCalibration.NullCalibration","VERIFY(calib != nullptr): Camera already calibrated. Cannot set with nullptr.");
                EngineErrorState.ErrorFlagSet=true; // sVerifyFailedReturnFalse 0x0080D9F4; no debug-break call.
                _robot.Engine.FlushSourceLogger();
                Log?.Invoke("Camera.SetCalibration.NullCalibration: VERIFY(calib != nullptr): Camera already calibrated. Cannot set with nullptr.");
            }
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
    // 0x00511162..16C / 0x0065258E: the destroyed VisionComponent owns its LUT.
    // A newer, already-bound owner must survive this retained-facade reset.
    private (EngineRobot? Owner,int Removal)? RetireDestroyedGroundOwner()
    {
        RetainedSubscription? subscription=null,movingSubscription=null;
        EngineRobot? retiredOwner;
        int removal;
        lock (_groundGate)
        {
            if (_groundOwner is { Lifetime.Destroying: false }) return null;
            retiredOwner=_groundOwner;
            removal=Interlocked.Increment(ref _removals);
            if(retiredOwner is null)return (null,removal);
            subscription=_nativeImuSubscription;_nativeImuSubscription=null;_nativeImu=null;
            movingSubscription=_nativeVisionWhileMovingSubscription;_nativeVisionWhileMovingSubscription=null;
            _groundOwner=null;_groundOuterCalibration=null;_groundInstalled=null;_groundLookup=null;_nativeWorkerPhysical=false;
            _nativeVisionWhileMoving=false;
            if(_nativeInitialization is {} retired)_retiredMotionStates.Enqueue(retired);
            _nativeInitialization=null;_nativeInitializationEpoch++;
            PendingDropStatsSeconds=-1f;
            _exposurePipeline=null;QualityProcessor.Pipeline=null;
            lock(_modeGate){_modeEnableMask=0;_schedules.Clear();}
            _groundPendingOuter=_constructedCalibration;
            _groundEpoch++; // host stale-work token; intentionally monotonic across owners
        }
        subscription?.Dispose();movingSubscription?.Dispose();
        return (retiredOwner,removal);
    }
    internal void ResetToConstructed()
    {
        if(RetireDestroyedGroundOwner() is not { } retired)return;
        var (retiringOwner,removal)=retired;
        DiscardPreparedResults(retiringOwner,discardAll:false);
        bool StillRetired(){lock(_groundGate)return _groundOwner is null && removal==Volatile.Read(ref _removals);}
        // fidelity: M11-061, M4-017, M1-025, M4-027
        // CB33/CC26/CC27: removal deletes this owner; a new owner's request deque is empty.
        // The retained-host reset remains the M4-027 candidate; do not replay requests on a later robot.
        lock(_groundGate)
        {
            if(_groundOwner is not null || removal!=Volatile.Read(ref _removals))return;
            lock (_modeGate) _limitedExposureRequests.Clear();
        }
        RemovalInvalidated?.Invoke();
        // The callback may synchronously bind a new owner. The old removal must not reset its facade state.
        if(!StillRetired())return;
        bool locked = Monitor.TryEnter(_busy, RemovalWait);
        try
        {
            if(locked)DrainRetiredMotionStates();
            if (!locked) Log?.Invoke($"vision reset: a frame was still being processed after {RemovalWait.TotalSeconds:F0} s; it is discarded at its next check");
            lock(_groundGate)
            {
                if(_groundOwner is not null || removal!=Volatile.Read(ref _removals))return;
                Calibration = _constructedCalibration;
            }
            // fidelity: M3-034 — removal destroys VC+0x2F4/+0x300 (0x0051116C, ~VisionComponent0x0065258E).
            // The robot-owned host instance survives; discard the retained raw NV vectors with that owner.
            _serializedFaceAlbum = Array.Empty<byte>();
            _serializedFaceEnrollment = Array.Empty<byte>();
            // B-M11-8 round 2: a removal is expected to leave no robot, so this reset does not go through History's MISSING report.
            _noRobotHistory.ResetToConstructed();
            if(!StillRetired())return;
            Imu.ResetToConstructed();
            World.ResetToConstructed();
            if(!StillRetired())return;
            Faces.ResetToConstructed();
            OkaoFaceDetector oldFaceDetector;FaceRecognizer oldRecognizer;
            lock(_groundGate)
            {
                if(_groundOwner is not null || removal!=Volatile.Read(ref _removals))return;
                oldFaceDetector = _stockFaceDetector;
                oldRecognizer = Recognizer;
                _stockFaceDetector = CreateStockFaceDetector();
                if (ReferenceEquals(FaceDetector, oldFaceDetector)) FaceDetector = _stockFaceDetector;
                if (_stockRecognizer) Recognizer = CreateFaceRecognizer(new NativeOkaoFaceRecognizer(_stockFaceDetector));
            }
            void RetireFaceOwner()
            {
                if (_stockRecognizer) RetireFaceSession(oldFaceDetector,oldRecognizer);
                else { lock(_busy) oldFaceDetector.Dispose(); }
            }
            _ = Task.Run(RetireFaceOwner);
            lock (_groundGate)
            {
                if (_groundOwner is not null || removal != Volatile.Read(ref _removals)) return;
                Pets.ResetToConstructed();
            }
            LastFaces = Array.Empty<TrackedFace>();
            FramesProcessed = 0;
            FramesDropped = 0;
            LastResult = null;
            ResetQualityReceiver();
            if(!StillRetired())return;
            lock(_groundGate)
            {
                if(_groundOwner is not null || removal!=Volatile.Read(ref _removals))return;
                _robot.CameraSettings.LastProcessedImageTimestamp = 0;
            }
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
    internal CameraCalibration? NativeOuterCalibrationFor(EngineRobot owner)
    {
        lock (_groundGate)
            return ReferenceEquals(_groundOwner, owner) && !owner.Lifetime.Destroying &&
                ReferenceEquals(_robot.Engine.Robot, owner) ? _groundOuterCalibration : null;
    }
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
    private OkaoFaceDetector _stockFaceDetector;
    private OkaoFaceDetector CreateStockFaceDetector() => new() { Diagnostic = message => SteppedBehavior.ReportMissing(message) };
    /// <summary>The stock detector uses the shipped OKAO library when present; callers may supply another detector.</summary>
    public IFaceDetector FaceDetector { get; set; }
    // fidelity: M14-011
    /// <summary>
    /// The engine's <c>FaceRecognizer</c> (0x008640E4): the Anki state machine over the OKAO_FR_* seam
    /// (Correction C3). It uses the stock detector's native recognition owner or a caller-supplied
    /// <see cref="IOkaoFaceRecognizer"/>. The enrollment mode/id chain
    /// (<c>FaceWorld::Enroll</c> -> <c>VisionComponent::SetFaceEnrollmentMode</c> -> ... ->
    /// <c>SetAllowedEnrollments</c>, C1-F11) reaches it through <see cref="SetFaceEnrollmentMode"/>.
    /// </summary>
    public FaceRecognizer Recognizer { get; private set; }

    internal Func<long>? FaceRecognitionClockForTest;
    private FaceRecognizer CreateFaceRecognizer(IOkaoFaceRecognizer backend)
    {
        var recognizer = new FaceRecognizer(backend, () => FaceRecognitionClockForTest?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000);
        recognizer.Log += l => LogCallbackDeferral.Run(() => Log?.Invoke(l));
        recognizer.SourceLog = (l,c,k,t) => _robot.Engine.EmitSourceLog(l,c,k,t);
        return recognizer;
    }
    private readonly OkaoPetDetector _unavailablePetDetector = new();
    private IPetDetector? _petDetectorOverride;
    public IPetDetector PetDetector
    {
        get { lock (_groundGate) return _petDetectorOverride ?? (_nativeInitialization is { Initialized:true } state ? state.StockPet : null) ?? _unavailablePetDetector; }
        set { lock (_groundGate) _petDetectorOverride = value ?? throw new ArgumentNullException(nameof(value)); }
    }
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
    /// <see cref="ProcessImage(GrayImage, uint, uint, VisionPoseData, OverheadEdgeGroundInput?)"/> (offline tools and tests) does not go through the gate.
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
    private readonly LinkedList<AllVisionModesSchedule> _schedules = new();
    public AllVisionModesSchedule? Schedules
    {
        get { lock(_modeGate)return _schedules.First?.Value; }
        set { lock(_modeGate){_schedules.Clear();if(value is not null)_schedules.AddFirst(value);} }
    }
    private void PushNativeSchedule(AllVisionModesSchedule value){lock(_modeGate)_schedules.AddFirst(value);}

    /// <summary>The mode enable bitmask at <c>VisionSystem+0xac</c>, from the shipped config (C2.3).</summary>
    private int _modeEnableMask;
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
    internal event Action<EngineRobot, uint>? NativeDockingMarkersProcessed;
    public event Action<VisionFrameResult>? FrameProcessed;
    public event Action<string>? Log;
    public SourceLogSink? SourceLog { get; set; }

    /// <summary>Raises the system's log line (used by the face actions' failure paths).</summary>
    internal void LogLine(string line) => LogCallbackDeferral.Run(() => Log?.Invoke(line));

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
    /// consistency/capacity install, nextFaceID). The stock path uses the detector's native owner and
    /// retains prior data when restore fails.
    /// </summary>
    public void InstallSerializedFaceData(byte[] album, byte[] enrollment)
    {
        var owner = _robot.Engine.Robot;
        var recognizer = Recognizer;
        int removal = Volatile.Read(ref _removals);
        TryInstallSerializedFaceData(album, enrollment, owner, recognizer, removal);
    }

    private bool TryInstallSerializedFaceData(byte[] album, byte[] enrollment, EngineRobot? owner, FaceRecognizer expected, int removal)
    {
        string? error = null;
        bool installed = false;
        lock (_busy)
        {
            bool Current() => removal == Volatile.Read(ref _removals) && ReferenceEquals(Recognizer, expected) &&
                (owner is null ? _robot.Engine.Robot is null : !owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, owner));
            if (!Current()) return false;
            var inputAlbum = album ?? Array.Empty<byte>();
            var inputEnrollment = enrollment ?? Array.Empty<byte>();
            var recognizer = expected;
            if (!_stockRecognizer)
            {
                _serializedFaceAlbum = inputAlbum;
                _serializedFaceEnrollment = inputEnrollment;
                return recognizer.IsAvailable && recognizer.SetSerializedData(inputAlbum, inputEnrollment) == 0;
            }
            try
            {
                if (recognizer.OwnsStockDetector(_stockFaceDetector) && recognizer.IsAvailable)
                {
                    recognizer.InitializeStock();
                    int status = recognizer.SetSerializedData(inputAlbum, inputEnrollment);
                    installed = status == 0 && Current();
                    if (status != 0 && inputAlbum.Length != 0) error = $"native restore returned {status}";
                    if (installed) { _serializedFaceAlbum = inputAlbum; _serializedFaceEnrollment = inputEnrollment; }
                }
            }
            catch (Exception ex) { error = ex.Message; }
        }
        if (error is not null) SteppedBehavior.ReportMissing($"M14-011 native face album restore failed: {error}");
        return installed;
    }

    // fidelity: M14-011, M14-012
    /// <summary>
    /// <c>VisionSystem::GetSerializedFaceData</c> (F26/C5-12), including the stock named-only album export.
    /// </summary>
    public (byte[] Album, byte[] Enrollment) GetSerializedFaceData()
    {
        var owner = _robot.Engine.Robot;
        var recognizer = Recognizer;
        int removal = Volatile.Read(ref _removals);
        lock (_busy)
        {
            if (_stockRecognizer)
            {
                bool Current() => removal == Volatile.Read(ref _removals) && ReferenceEquals(Recognizer, recognizer) &&
                    owner is not null && !owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, owner) &&
                    recognizer.OwnsStockDetector(_stockFaceDetector);
                if (!Current())
                    throw new InvalidOperationException("M14-011 face recognizer owner changed");
                var result = recognizer.ExportStockData();
                if (!Current()) throw new InvalidOperationException("M14-011 face recognizer owner changed during export");
                return result;
            }
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
        if (_stockRecognizer)
        {
            var owner = _robot.Engine.Robot;
            var recognizer = Recognizer;
            int removal = Volatile.Read(ref _removals);
            using (LogCallbackDeferral.EnterGated(_busy))
            {
                if (owner is null || owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, owner) ||
                    removal != Volatile.Read(ref _removals) || !ReferenceEquals(Recognizer, recognizer) ||
                    !recognizer.OwnsStockDetector(_stockFaceDetector) || !recognizer.IsAvailable) return;
                recognizer.SetAllowedEnrollments(id != 0 ? FaceWorld.EnrollModeKnownFace : FaceWorld.EnrollModeNewFace, id);
            }
            return;
        }
        var (_, mode) = Faces.Enroll(id);
        Recognizer.SetAllowedEnrollments(mode, id);
    }

    // VisionComponent holds the vision owner while the stock recognizer mutates
    // its enrollment tree. Observations, not this call, publish the changed name.
    internal bool AssignStockFaceName(int selectedId, string name, int saveId)
    {
        var owner = _robot.Engine.Robot;
        var recognizer = Recognizer;
        int removal = Volatile.Read(ref _removals);
        using (LogCallbackDeferral.EnterGated(_busy))
        {
            if (!_stockRecognizer || owner is null || owner.Lifetime.Destroying ||
                !ReferenceEquals(_robot.Engine.Robot, owner) || removal != Volatile.Read(ref _removals) ||
                !ReferenceEquals(Recognizer, recognizer) || !recognizer.OwnsStockDetector(_stockFaceDetector) ||
                !recognizer.IsAvailable) return false;
            return recognizer.AssignNameToFace(selectedId, name, saveId) == 0;
        }
    }

    // 0x657A60..BE: remove the recognizer entry, attempt the normal save,
    // then publish 57. Neither operation directly evicts a FaceWorld entry.
    internal bool EraseStockEnrolledFace(EngineRobot owner, int id, Func<bool>? current = null)
    {
        var recognizer = Recognizer;
        int removal = Volatile.Read(ref _removals);
        bool Current() => _stockRecognizer && !owner.Lifetime.Destroying &&
            ReferenceEquals(_robot.Engine.Robot, owner) && removal == Volatile.Read(ref _removals) &&
            ReferenceEquals(Recognizer, recognizer) && recognizer.OwnsStockDetector(_stockFaceDetector) && (current is null || current());
        using (LogCallbackDeferral.EnterGated(_busy))
        {
            if (!Current() || !recognizer.IsAvailable || recognizer.EraseFace(id) != 0) return false;
        }
        if (!Current()) return false;
        try { SaveFaceAlbumToRobot(owner,recognizer,removal,_robot.Engine.NvStorage,null,null); }
        catch(Exception error)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionComponent.EraseFace.SaveFailed",error.Message);
        }
        if (!Current()) return false;
        if (owner.HasExternalInterface)
        {
            byte[] body = new byte[5];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(body, id);
            _robot.Engine.DeliverAnimationReportToApp(0x57, body,_robot.Engine.CaptureFaceErased(new FaceErasedReport(id,string.Empty)));
            _robot.Engine.AnimationMessages.Deliver(0x57, true, new NativeErasedEnrolledFace(id, string.Empty));
        }
        return true;
    }

    // 0x657B24: mutation under the vision gate; save then report without waiting for NV replies.
    private void RenameStockEnrolledFace(EngineRobot owner,NativeRenameFaceRequest request)
    {
        var recognizer=Recognizer;int removal=Volatile.Read(ref _removals);
        bool Current()=>_stockRecognizer && !owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot,owner) &&
            removal==Volatile.Read(ref _removals) && ReferenceEquals(Recognizer,recognizer) && recognizer.OwnsStockDetector(_stockFaceDetector);
        using(LogCallbackDeferral.EnterGated(_busy))
        {
            if(!Current() || !recognizer.IsAvailable || recognizer.RenameFace(request.Id,request.OldName,request.NewName)!=0)return;
        }
        if(!Current())return;
        try { SaveFaceAlbumToRobot(owner,recognizer,removal,_robot.Engine.NvStorage,null,null); }
        catch(Exception error)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionComponent.RenameFace.SaveFailed",error.Message);
        }
        if(Current())owner.PublishFaceRenamed(request.Id,request.NewName);
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
        var owner = _robot.Engine.Robot;
        var recognizer = Recognizer;
        int removal = Volatile.Read(ref _removals);
        (int Id, string Name)[] loaded;
        bool installed;
        lock (_busy)
        {
            installed = TryInstallSerializedFaceData(album, enrollment, owner, recognizer, removal);
            loaded = installed && ReferenceEquals(Recognizer, recognizer)
                ? recognizer.EnrolledFaces.OrderBy(f => f.FaceID).Select(f => (f.FaceID, f.Name)).ToArray()
                : Array.Empty<(int, string)>();
        }
        if (!_stockRecognizer) { BroadcastLoadedNamesAndIDs(); return; }
        bool Current() => removal == Volatile.Read(ref _removals) && ReferenceEquals(Recognizer, recognizer) &&
            (owner is null ? _robot.Engine.Robot is null : !owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, owner));
        if (!installed || !Current()) return;
        _robot.Engine.QueueAppBroadcast(EnrolledFacesErased);
        EnrolledFacesErasedInternal?.Invoke();
        foreach (var face in loaded)
        {
            if (!Current()) return;
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "VisionComponent", "VisionComponent.BroadcastLoadedNamesAndIDs",
                FormattableString.Invariant($"broadcasting loaded face id: {face.Id}"));
            if (!Current()) return;
            _robot.Engine.QueueAppBroadcast(LoadedFaceName, face.Id, face.Name);
            LoadedFaceNameInternal?.Invoke(face.Id, face.Name);
        }
    }

    // fidelity: M14-012
    /// <summary>
    /// <c>VisionComponent::SaveFaceAlbumToRobot</c> (F26/G1-4/C5-13): the two serialized vectors,
    /// size-checked against their NV tags.  The erase path runs only when <b>both</b> vectors are empty,
    /// album 0x184000 first and 0x183000 only if that erase succeeded; otherwise both are rounded up to a
    /// four-byte boundary and written, album first.
    /// </summary>
    public void SaveFaceAlbumToRobot() => SaveFaceAlbumToRobot(null, null);

    // The behavior's two slots are completed independently by the NV wrappers (0x65A478/0x65A6CC).
    internal void SaveFaceAlbumToRobot(Action<NvResult>? albumCompleted, Action<NvResult>? enrollmentCompleted)
    {
        var owner = _robot.Engine.Robot;
        var recognizer = Recognizer;
        int removal = Volatile.Read(ref _removals);
        var nv = _robot.Engine.NvStorage;
        if (Monitor.IsEntered(_busy) || Monitor.IsEntered(World.FrameGate))
        {
            _robot.Engine.PostGameMessageFromVisionGate(() => SaveFaceAlbumToRobot(owner, recognizer, removal, nv, albumCompleted, enrollmentCompleted));
            return;
        }
        SaveFaceAlbumToRobot(owner, recognizer, removal, nv, albumCompleted, enrollmentCompleted);
    }

    private void SaveFaceAlbumToRobot(EngineRobot? owner, FaceRecognizer recognizer, int removal, NvStorageComponent? nv,
        Action<NvResult>? albumCompleted, Action<NvResult>? enrollmentCompleted)
    {
        using var lease = _robot.Engine.EnterSynchronousOwner();
        bool Current() => nv is not null && ReferenceEquals(_robot.Engine.NvStorage, nv) &&
            removal == Volatile.Read(ref _removals) && ReferenceEquals(Recognizer, recognizer) &&
            owner is not null && !owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, owner);
        void Completed(NvResult result, Action<NvResult>? callback)
        {
            if(owner is not null)CompleteFaceAlbumSave(owner,result,Current,callback,false);
        }
        if (!Current()) return;
        byte[] album, enrollment;
        try
        {
            lock (_busy)
            {
                if (!Current()) return;
                (album, enrollment) = GetSerializedFaceData();
            }
        }
        catch (Exception ex)
        {
            SteppedBehavior.ReportMissing($"M14-011 native face album export failed; save skipped: {ex.Message}");
            return;
        }
        int albumMax = NvStorageComponent.MaxSizeForEntryTag(FaceAlbumNvTag);            // 0x10000
        int enrollmentMax = NvStorageComponent.MaxSizeForEntryTag(FaceEnrollmentNvTag);  // 0x1000
        if (album.Length >= albumMax || enrollment.Length >= enrollmentMax)
        {
            Log?.Invoke($"SaveFaceAlbumToRobot: serialized data too large (album {album.Length}/{albumMax}, enrollment {enrollment.Length}/{enrollmentMax})");
            return;
        }
        if (album.Length == 0 && enrollment.Length == 0)
        {
            // C5-13: erase the album first, then the enrollment only when the album erase succeeded.
            Log?.Invoke("SaveFaceAlbumToRobot: EmptyAlbumData");
            if(Current())AdmitFaceAlbumSave(nv!,album,enrollment,Current,r=>Completed(r,albumCompleted),r=>Completed(r,enrollmentCompleted),false);
            return;
        }
        if(Current())AdmitFaceAlbumSave(nv!,album,enrollment,Current,r=>Completed(r,albumCompleted),r=>Completed(r,enrollmentCompleted),false);
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
    private (CameraFrame Frame, int Removal, VisionPoseData? Pose, NativeGroundFrame? Ground, EngineRobot? Owner)? _next;
    internal NativeGroundFrame? PendingGroundForTest { get { lock (_mailbox) return _next?.Ground; } }
    internal bool HasPendingFrameForTest { get { lock (_mailbox) return _next is not null; } }
    private bool _processorRunning;
    private Thread? _processorThread;
    private volatile bool _processorRun = true;
    // Native +0x4B has no shipped writer; internal input seam exercises its checked read.
    internal volatile bool SkipProcessorPass;
    private volatile bool _disposed;

    /// <summary>The Processor thread's sleep between polls: <c>sleep_for(2,000,000 ns)</c> (0x001E8480 built at 0x00651FDA, called at 0x0065225E).</summary>
    // fidelity: M11-040
    internal static readonly TimeSpan ProcessorPollInterval = TimeSpan.FromMilliseconds(2);

    // Host availability evidence: DetectingMotion is unbuilt, so a received frame withdraws no-motion proof.
    private long _motionFrameInputs;
    // Test-only observer; no production assignment. The carrier remains owned by the worker.
    internal Action<NativeGroundFrame>? NativeGroundFrameConsumedForTest;
    internal long MotionFrameInputs=>Interlocked.Read(ref _motionFrameInputs);
    private void OnFrame(CameraFrame f)
    {
        Interlocked.Increment(ref _motionFrameInputs);
        if(!_processorRun||_disposed)return;
        EngineRobot? frameOwner;
        lock (_groundGate) frameOwner=_groundOwner;
        int removal=Volatile.Read(ref _removals);
        if (frameOwner is null) { SteppedBehavior.ReportMissing("M11-033 camera handoff: original Robot owner unavailable"); return; }
        bool CurrentFrameOwner()
        {
            bool current=frameOwner is not null && !frameOwner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot,frameOwner) && removal==Volatile.Read(ref _removals);
            lock (_groundGate) return current && ReferenceEquals(frameOwner,_groundOwner);
        }
        if (!CurrentFrameOwner()) return;
        uint previous;
        bool rollback, unavailable=false, uninitialized=false, enabled=false;
        lock(_groundGate)
        {
            if(!ReferenceEquals(_groundOwner,frameOwner) || frameOwner.Lifetime.Destroying ||
                !ReferenceEquals(_robot.Engine.Robot,frameOwner) || removal!=Volatile.Read(ref _removals))return;
            var initialization=_nativeInitialization;
            unavailable=initialization is null || !ReferenceEquals(initialization.Owner,frameOwner) || !initialization.ComponentAvailabilityKnown;
            uninitialized=!unavailable && !initialization!.ComponentInitialized;
            enabled=Enabled;
            previous=0;rollback=false;
            if(!unavailable && !uninitialized && enabled)
            {
                previous=initialization!.LastReceivedTimestamp;
                rollback=previous!=0 && f.Timestamp<previous;
                if(rollback)
                {
                    initialization.LastReceivedTimestamp=0;
                    _robot.CameraSettings.LastProcessedImageTimestamp=0;
                }
                else initialization.LastReceivedTimestamp=f.Timestamp;
            }
        }
        if(unavailable) { SteppedBehavior.ReportMissing("M11-040 SetNextImage original VisionComponent initialization unavailable");return; }
        if(uninitialized) { _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.SetNextImage.NotInitialized",$"t={f.Timestamp}({unchecked((int)f.Timestamp)})");return; }
        if(!enabled)return;
        if(rollback)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.SetNextImage.UnexpectedTimeStamp",$"Current:{f.Timestamp} Last:{previous}");
            return;
        }
        CameraCalibration? pendingOuter;
        lock (_groundGate) pendingOuter=_groundPendingOuter;
        if (pendingOuter is not null) SetNativeOuterCalibration(pendingOuter);
        if (!CurrentFrameOwner()) return;
        if(Calibration is null){WarnNoCalibration(Volatile.Read(ref _removals));return;}
        lock(_groundGate)
        {
            if(!ReferenceEquals(_groundOwner,frameOwner)||_nativeImu is null)return;
            _nativeImu.Associate(f.ImageId,f.Timestamp,f.Height);
        }
        var pose=QueryFramePose(f.Timestamp, out var ground);
        if(CurrentFrameOwner() && pose is {} captured)HandOverFrame(f,captured,ground,frameOwner,removal);
        else { ReleaseGround(ground); ReleaseGround(pose?.RobotPose); DrainGroundReleases(); }
    }
    // fidelity: M11-097 — checked SetNextImage caller flag1 (OV12,0x00652C32).
    // Manager-adopted SetNextImage join0x00652C22..0x00652E40: clamp to newest raw time before flag1 query.
    private VisionPoseData? QueryFramePose(uint timestamp)
    {
        var pose = QueryFramePose(timestamp, out var ground);
        ground?.Dispose();
        return pose;
    }
    private VisionPoseData? QueryFramePose(uint timestamp, out NativeGroundFrame? ground)
    {
        EngineRobot? owner;
        lock (_groundGate) owner = _groundOwner;
        var history=owner?.PoseHistory ?? History;
        NativeGroundFrame? capturedGround = null;
        VisionPoseData? Query()
        {
            using var value=new HistRobotState();uint time=0;
            uint result=history.ComputeStateAt(Math.Min(timestamp,history.NewestTimeStamp),ref time,value,true);
            if(result==0)
            {
                NativeGroundPlaneLookup? lookup; int epoch;
                lock (_groundGate)
                {
                    epoch=_groundEpoch;
                    lookup=owner is not null && ReferenceEquals(owner,_groundOwner) && !owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot,owner) && _groundInstalled is not null ? _groundLookup?.Copy() : null;
                }
                if (owner is not null && lookup is not null)
                {
                    float[] h = new float[9];
                    bool valid = lookup.TryGet(value.State.HeadAngle, h);
                    var candidate = new NativeGroundFrame(owner, time, value, valid, h);
                    lock (_groundGate)
                        if (ReferenceEquals(owner, _groundOwner) && !owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, owner) && epoch == _groundEpoch) capturedGround = candidate;
                    if (capturedGround is null) candidate.Dispose();
                }
                // The legacy marker facade remains separate from the captured native ground carrier.
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
        VisionPoseData? pose=null;
        if(history.NativePoseTreeGate is not {} tree) pose=Query();
        else LogCallbackDeferral.RunGated(tree,()=>pose=Query());
        if (capturedGround is not null)
        {
            bool current;
            lock (_groundGate) current=ReferenceEquals(owner,_groundOwner) && owner is not null && !owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot,owner);
            if (!current) { capturedGround.Dispose(); capturedGround=null; }
        }
        ground = capturedGround;
        if (pose is not null && ground is null)
            SteppedBehavior.ReportMissing("M11-033/M11-040 captured ground carrier: original owner or calibration lookup unavailable");
        return pose;
    }

    /// <summary>
    /// <c>VisionComponent::SetNextImage</c> 0x00652B04 in its asynchronous mode. An image is discarded, with only an info log, while
    /// <see cref="Enabled"/> is false (0x00652B20..0x00652BEA): that byte (VisionComponent+0x48) has one writer, the NV calibration-read callback,
    /// which sets it on every outcome of the read (M11-049, 0x0065AE80), so no image is processed before that callback has run once. The
    /// pause byte (+0x4B) has no writer anywhere (it is only read), so there is no paused state here. Otherwise the image goes into the ONE pending
    /// slot: a slot still occupied is a dropped frame (DropStats, "SetNextImage.DroppedFrame", 0x00653070..0x006530C8) and the newest image
    /// replaces it (latest wins, 0x006530CC..0x00653140). The Processor takes the pending image, processes it, and on completion discards the
    /// current image and any image that arrived meanwhile without counting it (0x00652234..0x0065224C), then sleeps 2 ms and polls again.
    /// NOT BUILT (M11-040): the synchronous mode (<c>SetIsSynchronous</c>, <c>Start</c>/<c>Stop</c>)
    /// and the drop-statistics events. Production captures the source ground pose and matrix at hand-in; the native IMU deque,
    /// worker anchor and motion consumption remain unavailable.
    /// </summary>
    // fidelity: M11-040, M11-049
    internal void HandOverFrame(CameraFrame f)
    { Interlocked.Increment(ref _motionFrameInputs);HandOverFrame(f,null,null,null,null); } // direct unit hand-in; production supplies its checked captured state
    private void HandOverFrame(CameraFrame f,VisionPoseData? pose,NativeGroundFrame? ground,EngineRobot? sourceOwner,int? sourceRemoval)
    {
        if (!Enabled) { ReleaseGround(ground); ReleaseGround(pose?.RobotPose); DrainGroundReleases(); return; }
        NativeGroundFrame? displaced = null;
        bool accepted = false;
        try
        {
            lock (_mailbox)
            {
                if (_processorRun && !_disposed)
                {
                    if (_next is { } pending)
                    {
                        FramesDropped++;
                        Log?.Invoke($"VisionComponent.SetNextImage.DroppedFrame: Setting next image with t={f.Timestamp}, but existing next image from t={pending.Frame.Timestamp} not yet processed");
                    }
                    if (sourceOwner is not null)
                    {
                        bool ownerCurrent;
                        lock (_groundGate) ownerCurrent=ReferenceEquals(_groundOwner,sourceOwner);
                        if (!ownerCurrent || sourceOwner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,sourceOwner) || sourceRemoval!=Volatile.Read(ref _removals)) return;
                        lock(_groundGate)
                        {
                            if(!ReferenceEquals(_groundOwner,sourceOwner)||_nativeImu is null)return;
                            if(ground is not null)ground.Imu=_nativeImu.Snapshot();
                        }
                    }
                    displaced = _next?.Ground;
                    _next = (f, sourceRemoval ?? Volatile.Read(ref _removals),pose,ground,sourceOwner);
                    accepted = true;
                    if (_processorThread is null)
                    {
                        _processorThread = new Thread(Processor) { IsBackground = true };
                        _processorThread.Start();
                    }
                }
            }
        }
        finally
        {
            ReleaseGround(displaced);
            if (!accepted) { ReleaseGround(ground); ReleaseGround(pose?.RobotPose); }
            DrainGroundReleases();
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
                (CameraFrame Frame, int Removal, VisionPoseData? Pose, NativeGroundFrame? Ground, EngineRobot? Owner)? taken = null;
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
                    try
                    {
                        if (frame.Ground is not null)
                        {
                            NativeGroundFrameConsumedForTest?.Invoke(frame.Ground);
                        }
                        ProcessEncodedFrame(frame.Frame, frame.Removal,frame.Pose,queueForEngine:true,sourceOwner:frame.Owner,ground:frame.Ground);completed=true;
                    }
                    catch (Jpeg.OpenCvException e) { ReportDecodeThrow(frame.Frame,e); }
                    catch (Exception e) { completed=true;Log?.Invoke($"frame {frame.Frame.ImageId}: {e.GetType().Name}: {e.Message}"); }
                    finally
                    {
                        ReleaseGround(frame.Ground);
                        lock(_busy)DrainRetiredMotionStates();
                        NativeGroundFrame? discarded = null;
                        lock (_mailbox)
                        {
                            if(completed) { discarded = _next?.Ground; _next = null; } // native completion discards later arrivals
                            _processorRunning = false;
                            Monitor.PulseAll(_mailbox);
                        }
                        ReleaseGround(discarded);
                        DrainGroundReleases();
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
        NativeGroundFrame? discarded;
        lock (_mailbox) { discarded = _next?.Ground; _next = null; Monitor.PulseAll(_mailbox); }
        ReleaseGround(discarded);
        DrainGroundReleases();
    }

    /// <summary>
    /// The camera timestamp of the last frame whose timestamp had to be replaced by the paired robot state's
    /// (0 on fw2457 captures). Diagnostics only: the world model is dated by the state's timestamp.
    /// </summary>
    public uint? LastRawFrameTimestamp { get; private set; }

    /// <summary>Processes one camera frame against the recorded robot state; null without calibration or state.</summary>
    public VisionFrameResult? ProcessFrame(CameraFrame f)
    { Interlocked.Increment(ref _motionFrameInputs);return ProcessFrame(f, Volatile.Read(ref _removals)); }

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
    private VisionFrameResult? ProcessEncodedFrame(CameraFrame f,int removal,VisionPoseData? capturedPose=null,bool queueForEngine=false,EngineRobot? sourceOwner=null,NativeGroundFrame? ground=null)
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
        return ProcessCapture(cache, f.ImageId, f.Timestamp, removal,capturedPose,queueForEngine,sourceOwner,ground);
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

    // Explicit typed-carrier unit seam; production arrives through the F2 mailbox.
    internal void ProcessNativeCapturedForTest(EngineRobot owner,NativeGroundFrame ground,GrayImage image,uint imageId,uint timestamp)
    {
        var cache=new ImageCache();cache.Reset(image);
        using var pose=new Pose3d(ground.RobotPose);
        var data=new VisionPoseData(timestamp,pose,ground.State.HeadAngle,ground.State.LiftAngle,false,false);
        ProcessCapture(cache,imageId,timestamp,Volatile.Read(ref _removals),data,true,owner,ground);
    }

    private VisionFrameResult? ProcessCapture(ImageCache cache, uint imageId, uint cameraTimestamp, int removal,VisionPoseData? capturedPose=null,bool queueForEngine=false,EngineRobot? sourceOwner=null,NativeGroundFrame? ground=null)
    {
        if(queueForEngine && !Monitor.IsEntered(_busy))
        {lock(_busy)return ProcessCapture(cache,imageId,cameraTimestamp,removal,capturedPose,true,sourceOwner,ground);}
        if(queueForEngine && !NativeDecodedAdmission(sourceOwner,removal))return null;
        if(queueForEngine && Calibration is null){WarnNoCalibration(removal);return null;}
        NativeVisionInitialization? nativeState=null;int nativeFaceEpoch=0;
        if(queueForEngine)
        {
            if(ground is null)
            {
                lock(_groundGate)if(ReferenceEquals(_groundOwner,sourceOwner))_nativeInitialization?.MarkMotionUnavailable();
                SteppedBehavior.ReportMissing("M11-033/M11-098 captured native pose/ground input unavailable; worker result withheld");return null;
            }
            lock(_groundGate)
            {
                if(!ReferenceEquals(_groundOwner,sourceOwner)||_nativeInitialization is not {} state||!ReferenceEquals(state.Owner,sourceOwner))return null;
                nativeState=state;nativeFaceEpoch=state.StockFaceEpoch;
            }
            lock(_busy)
            {
                if(RemovedSince(removal))return null;
                nativeState.MotionPose.Admit(ground); // 0x6B4D88 precedes GetGray for every admitted frame.
                if(nativeState.Motion is not {} motion)return null;
                int channels=cache.RgbValid?3:1;
                byte[]? pixels=channels==3?cache.Rgb:cache.Gray?.Pixels;
                if(pixels is null||cache.Width<=0||cache.Height<=0)return null;
                motion.ImageCache.Reset(new NativeMotionImage(cache.Height,cache.Width,channels,cache.Width*channels,pixels,cameraTimestamp));
            }
        }
        GrayImage gray;
        try { gray = cache.GetGray(); }
        catch (InvalidOperationException e) { Log?.Invoke($"frame {imageId}: {e.Message}"); return null; }
        return ProcessCapture(gray, imageId, cameraTimestamp, removal,capturedPose,queueForEngine,nativeState,sourceOwner,nativeFaceEpoch);
    }

    private VisionFrameResult? ProcessCapture(GrayImage gray, uint imageId, uint cameraTimestamp, int removal,VisionPoseData? capturedPose=null,bool queueForEngine=false,NativeVisionInitialization? nativeState=null,EngineRobot? sourceOwner=null,int nativeFaceEpoch=0)
    {
        var calibration = Calibration;
        if (calibration is null) { WarnNoCalibration(removal); return null; }
        // fidelity: M11-017 -- the public decoded-capture entry keeps its SetNextImage query's ground carrier for Detect (rows 4-6).
        NativeGroundFrame? edgeGround=null;
        var pd=capturedPose??QueryFramePose(cameraTimestamp,out edgeGround);
        using var edgeGroundScope=edgeGround;
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
                            effective != cameraTimestamp ? cameraTimestamp : null,queueForEngine:queueForEngine,nativeState:nativeState,sourceOwner:sourceOwner,
                            edgeGround:edgeGround is null?null:EdgeGroundInput(edgeGround),nativeFaceEpoch:nativeFaceEpoch);
    }

    // fidelity: M11-017 -- VisionPoseData::Set 0x006AF330..0x006AF38A: H at +0x7C, the visible byte at +0x78, the RobotState status at +0x5C.
    private static OverheadEdgeGroundInput EdgeGroundInput(NativeGroundFrame ground) =>
        new(ground.Homography, ground.GroundValid, ground.State.Status);

    /// <summary>
    /// Applies the engine's rotating gate to a frame's pose data: <c>VisionComponent::WasRotatingTooFast</c>
    /// 0x0065359C over the <c>ImuDataHistory</c> at <c>VisionComponent+0xb0</c> (C3.3). Used by both the
    /// capture path and the offline <see cref="ProcessImage(GrayImage, uint, uint, VisionPoseData, OverheadEdgeGroundInput?)"/> path,
    /// so the gate reaches <see cref="BlockWorld"/> either way.
    /// </summary>
    // fidelity: M11-004
    private VisionPoseData WithRotatingGate(VisionPoseData pd, uint timestamp) =>
        pd with { RotatingTooFast = Imu.WasRotatingTooFast(timestamp, BlockWorld.MaxRotationRateRadPerSec) };

    /// <summary>
    /// The core update over a decoded image and the robot's pose data for it (usable offline). Throws
    /// <see cref="OperationCanceledException"/> when the robot is removed while the image is being processed, and
    /// <see cref="InvalidOperationException"/> when marker detection fails and the engine abandons the frame (DetectMarkersFailed).
    /// </summary>
    /// <param name="overheadEdgeGround">The frame's VisionPoseData fields the overhead-edge detector reads (H, visible, status; M11-017
    /// rows 4-6). Without them DetectingOverheadEdges reports MISSING and appends no frame.</param>
    public VisionFrameResult ProcessImage(GrayImage gray, uint imageId, uint timestamp, VisionPoseData pd, OverheadEdgeGroundInput? overheadEdgeGround = null)
    {
        var calibration = Calibration ?? throw new InvalidOperationException("no camera calibration");
        pd = WithRotatingGate(pd, timestamp);
        return ProcessImage(gray, imageId, timestamp, pd, calibration, Volatile.Read(ref _removals), null, offline: true, edgeGround: overheadEdgeGround)
            ?? throw (Detector.LastResult != 0
                ? new InvalidOperationException($"VisionSystem.Update.DetectMarkersFailed: marker detection returned 0x{Detector.LastResult:X8}; the engine abandons this frame")
                : new OperationCanceledException("the robot was removed while this image was being processed"));
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
                                            CameraCalibration calibration, int removal, uint? rawTimestamp, bool offline = false,bool queueForEngine=false,NativeVisionInitialization? nativeState=null,EngineRobot? sourceOwner=null,OverheadEdgeGroundInput? edgeGround=null,int nativeFaceEpoch=0)
    {
        var sw = Stopwatch.StartNew();
        var cal = gray.Width == calibration.Columns && gray.Height == calibration.Rows ? calibration : calibration.Scaled(gray.Width, gray.Height);
        using var detectionCameraPose=pd.CameraPose;
        var camera = new CameraModel(cal,detectionCameraPose);
        IReadOnlyList<ObservedMarker> markers;
        (Transform3d Camera, Transform3d Robot)? nativeFaceFrames = null;

        OverheadEdgeFrame? overheadEdges = null;
        lock (_busy)
        {
            if (RemovedSince(removal)) return null;
            if (!offline && nativeState?.MotionPose.Current is { } faceCamera && faceCamera.Timestamp == timestamp && ReferenceEquals(faceCamera.Owner, sourceOwner))
                nativeFaceFrames = (faceCamera.CameraPose.Transform, faceCamera.RobotPose.Transform);
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
            bool physical;
            lock(_groundGate)physical=_nativeWorkerPhysical;
            NativeRollingShutter.Shift[]? shifts=null;
            if(queueForEngine && physical && nativeState?.MotionPose.Current is {} currentGround)
            {
                shifts=NativeRollingShutter.Compute(currentGround.Timestamp,gray.Height,currentGround.Imu,false,out bool incomplete);
                if(incomplete)
                {
                    var history=currentGround.Imu;
                    if(history.Length==0)_robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"RollingShutterCorrector.ComputePixelShifts.EmptyHistory",$"No ImageIMU data from timestamp {currentGround.Timestamp}, imuDataHistory is empty");
                    else _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"RollingShutterCorrector.ComputePixelShifts.NoImageIMUData",$"No ImageIMU data from timestamp {currentGround.Timestamp} have data from time {history[0].ComputedTimestamp}:{history[^1].ComputedTimestamp}");
                }
                if(RemovedSince(removal) || sourceOwner is not null && (sourceOwner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,sourceOwner)))return null;
            }
            ushort processedMask=0;
            byte imageMean=0;
            var motionReports=new List<NativeObservedMotion>();
            var qualityRectangles=new List<ImageQualityRectangle>(); // fresh source Update-local vector
            if(ShouldProcessVisionMode((int)VisionMode.ComputingStatistics))
            {imageMean=SampleImageMean(gray);processedMask|=1<<8;}
            bool markerMode=ShouldProcessVisionMode(DetectingMarkers);
            markers = markerMode ? Detector.Detect(markerImage, timestamp) : Array.Empty<ObservedMarker>();
            if(RemovedSince(removal) || sourceOwner is not null && (sourceOwner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,sourceOwner)))return null;
            // fidelity: M11-022, M11-033 — a non-zero Detect result (row 35; reached only by the homography loop meeting quad 100, row 18)
            // is DetectMarkersWithCLAHE's return ([sp+0x10], 0x006B47DE..0x006B4AB4). VisionSystem::Update then logs the unchanneled
            // Error VisionSystem.Update.DetectMarkersFailed with an empty format (0x006B518A..0x006B51A0), sets Anki::Util::_errG and
            // calls sDebugBreakOnError when _errBreakOnError is set (0x006B51C6..0x006B51E2), and leaves through 0x006B5692 with that
            // result: no processed bit for markers (|= 2 at 0x006B51E8 is skipped), no later mode, no frame result. The Processor's
            // UpdateVisionSystem sees the non-zero result, warns VisionComponent.UpdateVisionSystem.UpdateFailed (0x00653D46) and goes on.
            // _errG: no reader found on this path (886 of 944 GOT references traced to a store; 58 hoisted pointers not followed to the end).
            if(markerMode && Detector.LastResult != 0)
            {
                _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionSystem.Update.DetectMarkersFailed","");
                EngineErrorState.StoreAndMaybeBreak();
                if(queueForEngine)_robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.UpdateVisionSystem.UpdateFailed","");
                return null;
            }
            foreach(var marker in markers)qualityRectangles.Add(NativeQualityRectangles.Marker(marker)); // raw quad, before correction
            if(queueForEngine && physical && markerMode && shifts is not null)
            {
                var corrected=new List<ObservedMarker>(markers.Count);
                foreach(var marker in markers)
                {
                    if(NativeRollingShutter.TryCorrect(marker,gray.Width,gray.Height,shifts,out var value,out bool badIndex))corrected.Add(value);
                    else if(badIndex)SteppedBehavior.ReportMissing("M1-034/M11-033 rolling-shutter strip vector.at 0x006B48D4: invalid marker skipped");
                    else _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"VisionSystem","VisionSystem.DetectMarkersWithCLAHE.RemovingMarkerOOB",marker.Code.ToString());
                    if(RemovedSince(removal) || sourceOwner is not null && (sourceOwner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,sourceOwner)))return null;
                }
                markers=corrected;
            }
            if(markerMode)processedMask|=1<<DetectingMarkers;
            // Detector and CLAHE retain the stock inner Camera ID 0 with every marker.
            // Capture it on the worker with this frame rather than inferring it at delivery.
            IReadOnlyList<NativeMarkerCarrier>? capturedNativeMarkers = queueForEngine
                ? markers.Select(marker => new NativeMarkerCarrier(marker, 0)).ToArray() : null;
            if (RemovedSince(removal)) return null;
            // Separate producers from recipients: a mode-7 failure publishes none of this frame's modes.
            Func<bool>? petSessionCurrent=null;
            Func<bool>? faceSessionCurrent=null;
            bool StaleFrame()=>RemovedSince(removal) || (queueForEngine && (_disposed || sourceOwner is null)) || sourceOwner is not null && (sourceOwner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,sourceOwner)) || petSessionCurrent is not null && !petSessionCurrent() || faceSessionCurrent is not null && !faceSessionCurrent();
            bool faceMode=ShouldProcessVisionMode((int)VisionMode.DetectingFaces);
            IFaceDetector faceDetector;FaceRecognizer capturedRecognizer;
            lock(_groundGate)
            {
                faceDetector=queueForEngine && ReferenceEquals(FaceDetector,_stockFaceDetector) && nativeState?.StockFace is { } stock ? stock : FaceDetector;
                capturedRecognizer=queueForEngine && ReferenceEquals(faceDetector,nativeState?.StockFace) ? nativeState!.StockFaceRecognizer! : Recognizer;
                if(queueForEngine && ReferenceEquals(faceDetector,nativeState?.StockFace) && faceDetector is OkaoFaceDetector selected)
                {
                    faceSessionCurrent=()=>{lock(_groundGate)return ReferenceEquals(_nativeInitialization,nativeState) && _nativeFaceEpoch==nativeFaceEpoch && nativeState!.StockFaceEpoch==nativeFaceEpoch && ReferenceEquals(nativeState.StockFace,selected) && ReferenceEquals(nativeState.StockFaceRecognizer,capturedRecognizer);};
                }
            }
            bool stockFaceDetector = ReferenceEquals(faceDetector, _stockFaceDetector);
            if(faceMode&&!faceDetector.IsAvailable && !(faceDetector is OkaoFaceDetector failed && failed.StockStatus is 2 or 3))SteppedBehavior.ReportMissing("M11-016/M11-080 face detector unavailable; its detection and quality rectangles are omitted");
            if(StaleFrame())return null;
            var faceImage=qualityRectangles.Count==0?gray:NativeQualityRectangles.BlackOut(gray,qualityRectangles);
            IReadOnlyList<DetectedFace>? detectedFaces = null;
            bool completeStockFaceReport = false;
            IReadOnlyList<(int OldId, int NewId, string? Name)> faceIdChanges = Array.Empty<(int, int, string?)>();
            if (faceMode && stockFaceDetector && faceDetector is OkaoFaceDetector failedFace && failedFace.StockStatus==2)
            {
                _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"FaceTrackerImpl.Update.NotInitialized","");
                if(StaleFrame())return null;
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                detectedFaces=Array.Empty<DetectedFace>();
            }
            else if (faceMode && stockFaceDetector && faceDetector is OkaoFaceDetector unavailableFace && unavailableFace.StockStatus==3)
                SteppedBehavior.ReportMissing("M11-016 stock face guest initialization unavailable; face mode omitted");
            else if (faceMode && faceDetector.IsAvailable)
            {
                try
                {
                    if (faceDetector is OkaoFaceDetector okao && stockFaceDetector)
                    {
                        bool reset = false;
                        if (nativeState?.MotionPose.Current is { } currentFacePose && nativeState.MotionPose.Previous is { } previousFacePose)
                        {
                            float threshold = BitConverter.UInt32BitsToSingle(0x3E0EFA35);
                            reset = !NativeMotionPoseState.HeadSame(currentFacePose, previousFacePose, threshold) ||
                                !NativeMotionPoseState.BodySame(currentFacePose, previousFacePose, 10f, threshold);
                        }
                        else if (queueForEngine) SteppedBehavior.ReportMissing("M14-010 face motion reset has no prior admitted VisionPoseData");
                        var recognizer = capturedRecognizer;
                        if (_stockRecognizer && recognizer.OwnsStockDetector(okao) && recognizer.IsAvailable)
                        {
                            recognizer.InitializeStock();
                            completeStockFaceReport = true;
                            int attributeMask=ModeEnableMask & 0x3C00; // captured for this detector execution
                            (detectedFaces, faceIdChanges) = okao.DetectRecognized(faceImage, timestamp, reset, recognizer, attributeMask);
                        }
                        else if (!_stockRecognizer) detectedFaces = okao.Detect(faceImage, timestamp, reset, ModeEnableMask & 0x3C00);
                        else SteppedBehavior.ReportMissing("M14-011 detector/recognizer owner changed during face frame; frame skipped");
                    }
                    else detectedFaces = faceDetector.Detect(faceImage, timestamp);
                }
                catch (Exception ex) when (stockFaceDetector)
                {
                    SteppedBehavior.ReportMissing($"M14-010 face detection failed: {ex.Message}; other frame results continue");
                }
            }
            if(StaleFrame())return null;
            if(faceMode && faceDetector.IsAvailable && detectedFaces is null)
                SteppedBehavior.ReportMissing("M11-016/M11-080 attached face detector returned null; its detection and quality rectangles are omitted");
            if(StaleFrame())return null;
            if(detectedFaces is not null)foreach(var face in detectedFaces)qualityRectangles.Add(NativeQualityRectangles.Detection(face.Rect));
            bool petMode=ShouldProcessVisionMode((int)VisionMode.DetectingPets);
            IPetDetector petDetector;bool ownerStock;
            lock (_groundGate)
            {
                ownerStock=queueForEngine && _petDetectorOverride is null && nativeState?.StockPet is not null;
                petDetector = _petDetectorOverride ?? (queueForEngine ? nativeState?.StockPet : null) ?? _unavailablePetDetector;
            }
            bool petAvailable=petMode && petDetector.IsAvailable;
            if (ownerStock && petAvailable && petDetector is OkaoPetDetector capturedPet)
                petSessionCurrent=()=>
                {
                    lock (_groundGate) return ReferenceEquals(_nativeInitialization,nativeState) && nativeState!.Initialized &&
                        ReferenceEquals(nativeState.StockPet,capturedPet) && capturedPet.ReadySnapshot;
                };
            if(petMode&&!petAvailable)SteppedBehavior.ReportMissing("M11-016/M11-080 pet detector unavailable; its detection and quality rectangles are omitted");
            if(StaleFrame())return null;
            var petImage=qualityRectangles.Count==0?gray:NativeQualityRectangles.BlackOut(gray,qualityRectangles);
            bool nativePetProfile=petMode && petAvailable && petDetector.ProvidesNativeResults;
            bool completePetProfile=nativePetProfile;
            DetectedPet[]? detectedPets;
            try { detectedPets = petMode && petAvailable ? petDetector.Detect(petImage,timestamp)?.ToArray() : null; }
            catch (Emulation.PetGuestFailure failure) when (petDetector is OkaoPetDetector)
            {
                if (StaleFrame()) return null;
                _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,failure.Key,failure.Message);
                if (StaleFrame()) return null;
                _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionSystem.DetectPets.PetTrackerUpdateFailed","");
                if (StaleFrame()) return null;
                _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionSystem.Update.DetectPetsFailed","");
                if (!StaleFrame()) Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                return null;
            }
            catch (Exception e) when (petDetector is OkaoPetDetector)
            {
                ((OkaoPetDetector)petDetector).Retire();
                SteppedBehavior.ReportMissing($"M11-016/M11-080 stock pet guest faulted; frame withheld: {e.Message}");
                return null;
            }
            if(completePetProfile && detectedPets is not null && detectedPets.Any(p=>p.TrackerResultFlag is null || p.Type is not (PetType.Cat or PetType.Dog)))
            {
                completePetProfile=false;
                SteppedBehavior.ReportMissing("M14-014 supplied pet result lacks native tracker flags or supported type; native update withheld");
            }
            if(StaleFrame())return null;
            if(petMode && petAvailable && detectedPets is null)
                SteppedBehavior.ReportMissing("M11-016/M11-080 attached pet detector returned null; its detection and quality rectangles are omitted");
            if(StaleFrame())return null;
            if(detectedPets is not null)foreach(var pet in detectedPets)qualityRectangles.Add(NativeQualityRectangles.Detection(pet.Rect));
            if(ShouldProcessVisionMode((int)VisionMode.DetectingMotion))
            {
                if(nativeState?.Motion is not {} motion || !queueForEngine)
                    SteppedBehavior.ReportMissing("M11-098 DetectingMotion requires the actual decoded worker input");
                else if(!motion.Detector.Detect(motion.ImageCache.GetHalf(),nativeState.MotionPose,gray.Width,gray.Height,motionReports))
                {nativeState.MarkMotionUnavailable();SteppedBehavior.ReportMissing("M11-098 actual motion producer input unavailable; frame result withheld");return null;}
                else processedMask|=1<<3;
                if(RemovedSince(removal) || sourceOwner is not null && (sourceOwner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,sourceOwner)))return null;
            }
            bool overheadEdgesRan=false;
            if(ShouldProcessVisionMode((int)VisionMode.DetectingOverheadEdges) && OverheadEdges is {} edges)
            {
                // fidelity: M11-017 -- Detect(ImageCache, VisionPoseData fp+0x104, result) 0x006B5398..0x006B53A4 reads the frame's VisionPoseData:
                // H +0x7C, the visible byte +0x78 and the status +0x5C (inventory rows 4-6). Production's carrier is MotionPose.Current, this
                // frame's SetNextImage capture; the public decoded-capture entry passes its own SetNextImage query, and the offline host entry
                // takes the fields from its caller as it takes the pixels and the pose. Its return is always 0 here
                // (row 9's pose-tree failure needs lift and camera poses without a common root; both hang off this frame's robot pose), so the
                // whole-result drop of row 2 has no input and the processed bit 0x10 is set whenever Detect ran.
                var carrier=nativeState?.MotionPose.Current is {} current?EdgeGroundInput(current):edgeGround;
                if(carrier is not {} edgeInput)SteppedBehavior.ReportMissing("M11-017 overhead-edge Detect: this entry has no VisionPoseData ground carrier (homography +0x7C, visible +0x78, status +0x5C); Detect not run, no frame");
                else
                {
                    overheadEdges=edges.Detect(gray,edgeInput,camera,pd.RobotPose,timestamp,pd.LiftAngleRad);
                    overheadEdgesRan=true;
                }
            }
            if(detectedFaces is not null)processedMask|=1<<2;
            if(detectedPets is not null)processedMask|=1<<9;
            if(overheadEdgesRan)processedMask|=1<<4; // 0x006B540A..0x006B5416: set on Detect's zero return, with or without a frame
            foreach(int mode in new[]{5,6,15})if(ShouldProcessVisionMode(mode))
                SteppedBehavior.ReportMissing("M11-035 "+(VisionMode)mode+" producer/handler unavailable; no processed bit or native success invented");
            if(RemovedSince(removal))return null;
            // fidelity: M11-080, M11-057
            ImageQualityProposal? quality=null;
            NativeQualityRectanglesObservedForTest?.Invoke(Array.AsReadOnly(qualityRectangles.ToArray()));
            if(StaleFrame())return null;
            if(ShouldProcessVisionMode((int)VisionMode.CheckingQuality))
            {
                bool available=TryCheckFrameQuality(gray,qualityRectangles,()=>!StaleFrame(),out uint qualityStatus,out var proposed);
                if(StaleFrame())return null;
                if(!available)
                {
                    // Offline ProcessImage is a host helper with explicit decoded pixels/pose, not the native Update entry.
                    // A live frame with unknown quality input is stopped without forging a native failure/log.
                    if(!offline)
                    {
                        if(queueForEngine && motionReports.Count!=0)nativeState?.MarkMotionUnavailable();
                        return null;
                    }
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
                    processedMask|=1<<7; // successful actual proposal; unknown/nonzero status publishes nothing.
                }
            }
            bool handlerFailed=false;
            VisionFrameResult? ApplyResult(VisionPoseData processingPose)
            {
                IReadOnlyList<ObjectObservation> objects;
                IReadOnlyList<ObservableObject> forgotten;
                VisionFrameResult result;
                List<Action>? markerDeferredLogs=null;
                void MarkerSourceLog(EngineLogLevel level,string? channel,string key,string text)
                    =>(markerDeferredLogs??=new()).Add(()=>_robot.Engine.EmitSourceLog(level,channel,key,text));
                void MarkerPublicLog(string text)
                {
                    var handlers=Log;
                    (markerDeferredLogs??=new()).Add(()=>EventFan.Raise(handlers,text,e=>_robot.Engine.EmitStackLog("M1-034 Vision log subscriber skipped: "+e)));
                }
                void MarkerMissing(string text)=>(markerDeferredLogs??=new()).Add(()=>SteppedBehavior.ReportMissing(text));
                using var resultCameraPose=processingPose.CameraPose;
                var processingCamera=new CameraModel(cal,resultCameraPose);
                lock(_busy)
                {
                if(queueForEngine ? StaleFrame() : RemovedSince(removal))return null;
            // UpdateVisionMarkers 0x00654D76..0x006550A8. The direct/offline
            // adapter retains its supplied-pose semantics; queued results use the captured owner.
            bool stateComputed = markerMode;
            var sourceMarkers=queueForEngine && NativeMarkerListForTest is {} markerFixture ? markerFixture(markers) : markers;
            IReadOnlyList<NativeMarkerCarrier>? nativeMarkers = queueForEngine && NativeMarkerListForTest is not null
                ? sourceMarkers.Select(marker => new NativeMarkerCarrier(marker, 0)).ToArray() : capturedNativeMarkers;
            if (nativeMarkers is not null && NativeMarkerCarriersForTest is { } carrierFixture)
                nativeMarkers = carrierFixture(nativeMarkers);
            var keptMarkers = sourceMarkers;
            uint historicalKey = 0;
            BlockWorldFrameResult? worldFrame;
            using(World.EnterPoseMutation())
            {
            using var historicalResult=new HistRobotState();
            using var nativeResultCameraPose=new Pose3d("");
            var markerCamera=processingCamera;
            // fidelity: M11-036, M11-050
            if (markerMode && sourceMarkers.Count > 0)
            {
                if (offline) History.InsertComputedStateAt(timestamp, processingPose.RobotPose);   // LOCAL: the caller's pose data stands for the raw state
                else if(queueForEngine)
                {
                    if(sourceOwner is null || StaleFrame())stateComputed=false;
                    else
                    {
                        uint status=sourceOwner.PoseHistory.ComputeAndInsertKeyedState(timestamp,historicalResult,out uint resolvedTime,out historicalKey);
                        if (status == 0) NativeMarkerKeyComputedForTest?.Invoke(sourceOwner.PoseHistory, historicalKey);
                        if (StaleFrame()) return null;
                        if(status!=0)
                        {
                            stateComputed=false;
                            if(status!=0x06000000)
                            {
                                string detail=$"Time: {timestamp}, hist: {sourceOwner.PoseHistory.OldestTimeStamp} to {sourceOwner.PoseHistory.NewestTimeStamp}";
                                MarkerSourceLog(EngineLogLevel.Info,"VisionComponent","VisionComponent.UpdateVisionMarkers.HistoricalPoseNotFound",detail);
                                MarkerPublicLog("VisionComponent.UpdateVisionMarkers.HistoricalPoseNotFound: "+detail);
                            }
                        }
                        else if(!sourceOwner.TryGetNativeOriginOwner(sourceOwner.CurrentOriginId,out var origin) || origin?.Node is null || historicalResult.Pose.Node is null)
                        {stateComputed=false;MarkerMissing("M11-050 historical marker origin owner unavailable; marker handler skipped");}
                        else if(!PoseTreeNode.HasSameRoot(historicalResult.Pose.Node,origin.Node))
                        {
                            stateComputed=false;
                            string detail=$"Ignoring observed marker from origin {historicalResult.Pose.GetNamedPathToRoot(false)} (robot origin is {origin.Name})";
                            MarkerSourceLog(EngineLogLevel.Info,"VisionComponent","VisionComponent.UpdateVisionMarkers.OldOrigin",detail);
                            MarkerPublicLog("VisionComponent.UpdateVisionMarkers.OldOrigin: "+detail);
                        }
                        else if(NativeMarkerRotatingTooFast(sourceOwner,resolvedTime,MarkerSourceLog,MarkerMissing))stateComputed=false;
                        else if(StaleFrame())stateComputed=false;
                        else
                        {
                            CameraCalibration? outer;
                            lock(_groundGate)outer=ReferenceEquals(_groundOwner,sourceOwner)&&!sourceOwner.Lifetime.Destroying&&ReferenceEquals(_robot.Engine.Robot,sourceOwner)?_groundOuterCalibration:null;
                            if(outer is null)
                            {stateComputed=false;MarkerMissing("M11-050 original outer historical camera calibration unavailable; marker handler skipped");}
                            else
                            {
                                using var nativeCamera=sourceOwner.CopyHistoricalCameraPose(historicalResult);
                                nativeResultCameraPose.AssignInputValue(nativeCamera);
                                markerCamera=new CameraModel(outer,nativeResultCameraPose);
                                NativeMarkerCameraObservedForTest?.Invoke(outer,nativeResultCameraPose);
                                if(StaleFrame())stateComputed=false;
                            }
                        }
                    }
                }
                else
                {
                    stateComputed=History.ComputeAndInsertStateAt(timestamp,historicalResult,out uint stateOrigin);
                    if(!stateComputed)Log?.Invoke($"VisionComponent.UpdateVisionMarkers.HistoricalPoseNotFound: Time: {timestamp}, hist: {History.OldestTimeStamp} to {History.NewestTimeStamp}");
                    else
                    {
                        // Preserve the existing direct ProcessCapture adapter's historical camera.
                        if(_robot.Engine.Robot is not {} cameraOwner || Calibration is not {} installed)
                        {SteppedBehavior.ReportMissing("M11-050 native historical Camera owner/calibration unavailable; result withheld");return null;}
                        using var nativeCamera=cameraOwner.CopyHistoricalCameraPose(historicalResult);
                        nativeResultCameraPose.AssignInputValue(nativeCamera);
                        markerCamera=new CameraModel(installed,nativeResultCameraPose);
                        if(!IsPoseInWorldOrigin(stateOrigin)){stateComputed=false;Log?.Invoke($"VisionComponent.UpdateVisionMarkers.OldOrigin: Ignoring observed marker from origin {stateOrigin} (robot origin is {_robot.Engine.Robot?.CurrentOriginId})");}
                    }
                }
                if(queueForEngine && StaleFrame())stateComputed=false;
                if(stateComputed)
                {
                    var kept = new List<ObservedMarker>(sourceMarkers.Count);
                    var candidates = nativeMarkers ?? sourceMarkers.Select(marker => new NativeMarkerCarrier(marker, 0)).ToArray();
                    foreach (var carrier in candidates)
                    {
                        var m = carrier.Marker;
                        if (m.Timestamp != timestamp)
                        {
                            if(queueForEngine)
                            {
                                MarkerSourceLog(EngineLogLevel.Error,null,"VisionComponent.UpdateVisionMarkers.MismatchedTimestamps",$"Marker t={m.Timestamp} vs. ProcResult t={timestamp}");
                                EngineErrorState.StoreAndMaybeBreak();
                            }
                            string detail=$"VisionComponent.UpdateVisionMarkers.MismatchedTimestamps: Marker t={m.Timestamp} vs. ProcResult t={timestamp}";
                            if(queueForEngine)MarkerPublicLog(detail);else Log?.Invoke(detail);
                            continue;
                        }
                        if (queueForEngine && sourceOwner is not null && carrier.CameraId == sourceOwner.NativeRobotId &&
                            !sourceOwner.PoseHistory.IsValidComputedKey(historicalKey))
                        {
                            MarkerSourceLog(EngineLogLevel.Warning, null, "VisionComponent.Update.InvalidHistStateKey", $"key={unchecked((int)historicalKey)}");
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
            worldFrame=stateComputed?World.UpdateObservedMarkers(keptMarkers, markerCamera, timestamp, processingPose,queueForEngine && sourceOwner is not null ? t=>NativeWorldRotatingTooFast(sourceOwner,t) : null,queueForEngine && sourceOwner is not null ? (obj,prefix)=>PublishObservedCube(sourceOwner,obj,prefix,()=>!StaleFrame()) : null,queueForEngine && sourceOwner is not null ? sourceOwner.PoseHistory.GetComputedNativePoseAt : null):null;
            } // Dispose marker-only pose owners and drain world events/logs BEFORE caller docking/faces.
            if(queueForEngine && StaleFrame())return null;
            if (worldFrame is not null)
            {
                objects = worldFrame.Objects;
                forgotten = worldFrame.Forgotten;
                // fidelity: M11-064 — warning, docking signal (even after failure), then caller's error.
                if (worldFrame.Status != 0)
                    _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.UpdateVisionResults.BlockWorldUpdateFailed","");
                if (keptMarkers.Count != 0)
                {
                    if (queueForEngine && sourceOwner is not null && !sourceOwner.Lifetime.Destroying &&
                        ReferenceEquals(_robot.Engine.Robot, sourceOwner) && !RemovedSince(removal))
                        NativeDockingMarkersProcessed?.Invoke(sourceOwner, timestamp);
                    if (queueForEngine && (sourceOwner is null || sourceOwner.Lifetime.Destroying ||
                        !ReferenceEquals(_robot.Engine.Robot, sourceOwner) || RemovedSince(removal))) return null;
                    DockingMarkersProcessed?.Invoke(new VisionFrameResult(imageId,timestamp,pd,keptMarkers,objects,forgotten,sw.Elapsed));
                    if(queueForEngine && StaleFrame())return null;
                }
                if (worldFrame.Status != 0)
                {
                    handlerFailed=true;
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
            // stack runs the supported marker, face, pet, motion, overhead-edge and quality handlers in that order.
            // Tool-code, computed-calibration and laser-point recipients remain unavailable.
            // VisionSystem::Update in DetectingFaces mode: FaceTracker::Update, TrackedFace::UpdateTranslation(processingCamera), FaceWorld::AddOrUpdateFace
            // fidelity: M14-008
            if (detectedFaces is {} detected)
            {
                var faces = new List<TrackedFace>();
                if (RemovedSince(removal)) return null;
                foreach (var change in faceIdChanges)
                {
                    if (StaleFrame()) return null;
                    Faces.ChangeFaceID(change.OldId, change.NewId, change.Name);
                    if (StaleFrame()) return null;
                }
                // 0x655204..26C: all enrollment pulses precede every face observation.
                foreach (var d in detected) Faces.ApplyEnrollmentCount(d.NativeEnrollmentCount);
                foreach (var d in detected)
                {
                    var tf = new TrackedFace(d, timestamp);
                    if (queueForEngine && nativeFaceFrames is { } frames)
                    {
                        tf.UpdateNativeCameraTranslation(processingCamera.Calibration);
                        if (StaleFrame()) return null;
                        // TrackedFace starts at +X pi/2; DetectFaces parents it to
                        // the historical camera, then flattens to root (0x6B37B0..EC).
                        var pose = ComposeNativeFacePose(tf.NativeCameraTranslation, frames.Camera, frames.Robot);
                        tf.NativeTrackingTranslation = pose.Translation;
                        tf.NativeTrackingRotation = pose.Rotation;
                        // Keep the legacy public pose for manual adapters; the detached native transform is stored above.
                        tf.HeadPose.Dispose();
                        tf.HeadPose = new Pose3d(processingCamera.Pose, processingCamera.Pose.Apply(tf.NativeCameraTranslation));
                    }
                    else if (queueForEngine && !offline)
                    {
                        SteppedBehavior.ReportMissing("M14-001 tracked face has no original historical camera/robot carrier; face withheld");
                        continue;
                    }
                    else tf.UpdateTranslation(processingCamera);
                    if (StaleFrame()) return null;
                    faces.Add(tf);
                    FaceObservation? observation;
                    if (offline)
                    {
                        // M14-009: with a native robot, the offline path also anchors the face to its observation origin,
                        // so the face queries' IsPoseInWorldOrigin gate can be evaluated; the offline gates are unchanged.
                        PoseTreeNode? offlineOrigin=null;
                        if(_robot.Engine.Robot is { } offlineOwner)
                        {
                            using var faceMutation=World.EnterPoseMutation();
                            lock(offlineOwner.PoseTreeGate) offlineOrigin=ResolveFaceObservationOrigin(History,offlineOwner,timestamp,native:false);
                        }
                        observation=offlineOrigin is null
                            ? Faces.AddOrUpdateFace(tf, processingPose.RobotPose, processingPose.RotatingTooFast)
                            : Faces.AddOrUpdateNativeFace(tf, processingPose.RobotPose, processingPose.RotatingTooFast, offlineOrigin);
                    }
                    else
                    {
                        using var faceMutation=World.EnterPoseMutation();
                        if(_robot.Engine.Robot is not { } owner)
                        {
                            SteppedBehavior.ReportMissing("M14-008 FaceWorld native Robot origin owner unavailable; face update skipped");
                            continue;
                        }
                        if (queueForEngine && !ReferenceEquals(owner, sourceOwner)) return null;
                        lock(owner.PoseTreeGate)
                        {
                            var originNode=ResolveFaceObservationOrigin(History,owner,timestamp);
                            if(originNode is null)continue;
                            observation=d.NativeTrackerFlagChanged is { } changed
                                ? Faces.AddOrUpdateNativeFace(tf,processingPose.RobotPose,()=>NativeFaceRotatingTooFast(owner,timestamp,changed),originNode,_robot.Sensors.OffTreadsState==OffTreadsState.OnTreads,requireNativePose:queueForEngine,expectedOwner:sourceOwner,expectedGeneration:removal)
                                : Faces.AddOrUpdateNativeFace(tf,processingPose.RobotPose,d.NativeNewEntryRotatingTooFast,originNode,_robot.Sensors.OffTreadsState==OffTreadsState.OnTreads,requireNativePose:queueForEngine,expectedOwner:sourceOwner,expectedGeneration:removal);
                        }
                    }
                    if(RemovedSince(removal) || sourceOwner is not null && (sourceOwner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,sourceOwner)))return null;
                    if(observation?.NativeSnapshot is { } snapshot && (snapshot.Count>3||!string.IsNullOrEmpty(snapshot.Name)) && _robot.Engine.Robot is {HasExternalInterface:true} reportOwner)
                    {
                        if(queueForEngine && !ReferenceEquals(reportOwner,sourceOwner))return null;
                        RobotObservedFaceReport? report = null;
                        if(!offline && queueForEngine && ReferenceEquals(reportOwner,sourceOwner) && completeStockFaceReport && d.NativeFeatures is { } features && d.NativeAttributes is { } attributes &&
                            snapshot.Translation is { } translation && snapshot.Rotation is { } rotation && snapshot.Origin is { } faceOrigin &&
                            string.Equals(snapshot.Name ?? "",d.Name ?? "",StringComparison.Ordinal))
                        {
                            string name=snapshot.Name ?? "";
                            var bytes=snapshot.NameBytes;
                            if(bytes is not null)
                            {
                                using var faceMutation=World.EnterPoseMutation();
                                lock(reportOwner.PoseTreeGate)
                                {
                                    using var parent=Pose3d.WrapExisting(faceOrigin);
                                    using var pose=new Pose3d(rotation,translation,parent:parent);
                                    if(NativeObjectPose.TryConvert(pose,reportOwner,out var converted))
                                        report=new(snapshot.Id,snapshot.Timestamp,converted,snapshot.Rect,name,features.LeftEye,features.RightEye,features.Nose,features.Mouth)
                                        { Expression=attributes.Expression, Smile=attributes.Smile, Gaze=attributes.Gaze, Blink=attributes.Blink, ExpressionScores=attributes.Scores, NativeNameBytes=bytes.ToArray() };
                                }
                            }
                        }
                        if(StaleFrame())return null;
                        if(report is null) Behavior.SteppedBehavior.ReportMissing("M14-008 RobotObservedFace complete stock pose, parts, name or optional-mode input unavailable; delivering local ID/timestamp only");
                        else _robot.Engine.PublishRobotObservedFace(report);
                        if(StaleFrame())return null;
                        _robot.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(snapshot.Id,snapshot.Timestamp));
                        if(queueForEngine && StaleFrame())return null;
                    }
                }
                LastFaces = faces;
                if(queueForEngine && sourceOwner is not null) Faces.UpdateNative(_robot.CameraSettings.LastProcessedImageTimestamp,sourceOwner,()=>!StaleFrame());
                else Faces.Update(timestamp);
                if(queueForEngine && StaleFrame())return null;
                // fidelity: M14-011
                // Stock FaceTracker recognition is processed before this observation pass; optional
                // enrollment pose modes and positive-ID merge/remap transitions remain MISSING.
            }
            if (detectedPets is {} pets)
            {
                if (RemovedSince(removal)) return null;
                if (queueForEngine && sourceOwner is not null && completePetProfile)
                {
                    bool CurrentPet() => !StaleFrame();
                    bool ok = Pets.UpdateNative(pets, timestamp,
                        flag => NativeObservationRotatingTooFast(sourceOwner, timestamp, flag, "M14-014 native pet", () =>
                            (markerDeferredLogs ??= new()).Add(() =>
                            {
                                if (!StaleFrame()) _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "VisionComponent", "VisionComponent.VisionComponent.WasHeadRotatingTooFast.NoIMUData",
                                    $"Could not get next/previous imu data for timestamp {timestamp}");
                            })), CurrentPet,
                        (input, pet, currentEpoch) =>
                        {
                            if (!input.TrackerResultFlag!.Value)
                            {
                                if (pet.TimesObserved != 1)
                                {
                                    _robot.Engine.EmitSourceLog(EngineLogLevel.Error, null, "PetWorld.Update.NewPetDetectionShouldBeObservedOnce",
                                        $"VERIFY(knownPet.GetNumTimesObserved() == 1): ID:{pet.Id} NumTimesObserved:{pet.TimesObserved}");
                                    if (!currentEpoch()) return;
                                    Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                                    _robot.Engine.Das.ForceFlushNow();
                                    if (!currentEpoch()) return;
                                }
                                _robot.Engine.EmitSourceLog(EngineLogLevel.Event, null, "robot.vision.detected_pet", pet.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                    new Dictionary<string, string> { { "$data", pet.Type == PetType.Cat ? "Cat" : "Dog" } });
                            }
                            if (!currentEpoch() || !sourceOwner.HasExternalInterface) return;
                            var report = new RobotObservedPetReport(pet.Id, timestamp, unchecked((uint)pet.TimesObserved), (float)input.Score, pet.Rect, pet.Type);
                            _robot.Engine.PublishRobotObservedPet(report);
                            if (currentEpoch()) _robot.Engine.AnimationMessages.Deliver(0x48, true, report);
                        }, _robot.Engine.RunIsolated);
                    if (!ok && StaleFrame()) return null;
                }
                else if(!nativePetProfile || !queueForEngine) Pets.Update(pets, timestamp, processingPose.RotatingTooFast);
                if(queueForEngine && StaleFrame())return null;
            }
            // The ground in front of the robot, on this frame's own pose data: the detector needs the
            // camera where it was when the image was taken and the lift angle it had then, which is what
            // VisionPoseData carries. The points come back in robot coordinates, so whoever puts them in
            // the map uses the same frame's robot pose - which is what the engine's map component looks
            // up by the frame's timestamp (RobotStateHistory::ComputeAndInsertStateAt at 0x0067F8A2).
            if (RemovedSince(removal)) return null;
            if(queueForEngine && sourceOwner is not null && sourceOwner.HasExternalInterface)
            {
                foreach(var motion in motionReports)
                {
                    if(RemovedSince(removal)||sourceOwner.Lifetime.Destroying||!ReferenceEquals(_robot.Engine.Robot,sourceOwner))return null;
                    _robot.Engine.DeliverAnimationReportToApp(0x49,motion.PackBody());
                    if(RemovedSince(removal)||sourceOwner.Lifetime.Destroying||!ReferenceEquals(_robot.Engine.Robot,sourceOwner))return null;
                    _robot.Engine.AnimationMessages.Deliver(0x49,true,motion);
                    if(StaleFrame())return null;
                }
            }
            if(overheadEdges is {} overhead)_robot.Engine.Robot?.NativeBehaviorRuntime?.ApplyOverhead(overhead,this);
            if(queueForEngine && StaleFrame())return null;
            if(queueForEngine && quality is {} qualityResult)handlerFailed|=UpdateImageQuality(timestamp,qualityResult)!=0;
            if(queueForEngine && StaleFrame())return null;
            result = new VisionFrameResult(imageId, timestamp, pd, markers, objects, forgotten, sw.Elapsed)
            {
                OverheadEdges = overheadEdges,
                ImageQuality = quality,
            };
            if (rawTimestamp is { } raw) LastRawFrameTimestamp = raw;
            FramesProcessed++;
            LastResult = result;
                }
            if(markerDeferredLogs is not null)foreach(var dispatch in markerDeferredLogs)
            {
                dispatch();
                if(queueForEngine && StaleFrame())return null;
            }
            // Native result state is committed before optional host callbacks; a host
            // callback may dispose/join the worker without holding its processing lock.
            // Raised under the lock a removal waits on, so a frame the removal discarded never raises it afterwards.
            // fidelity: M11-035 -- 0x006549A2..0x006549AA: handlers see the previous +0xE8.
            // A consumed result still reaches the tail when a host handler throws.
            try
            {
                if(!queueForEngine)FrameProcessed?.Invoke(result);
                else if(FrameProcessed is { } handlers)
                {
                    foreach(Action<VisionFrameResult> handler in handlers.GetInvocationList())
                    {
                        if(StaleFrame())break;
                        try { handler(result); }
                        catch(Exception e) { _robot.Engine.EmitStackLog("M1-034 Vision FrameProcessed subscriber skipped: "+e); }
                    }
                }
            }
            finally
            {
                if(!queueForEngine) _robot.CameraSettings.LastProcessedImageTimestamp = result.Timestamp;
                else
                {
                    bool committed;
                    lock(_groundGate)
                    {
                        committed=!StaleFrame() && ReferenceEquals(_groundOwner,sourceOwner);
                        if(committed)_robot.CameraSettings.LastProcessedImageTimestamp = result.Timestamp;
                    }
                    if(committed && sourceOwner is not null)PublishProcessedImage(sourceOwner,timestamp,processedMask,imageMean,()=>!StaleFrame());
                }
            }
            return result;

            }
            if(queueForEngine)
            {
                object poseTreeGate=History.NativePoseTreeGate??_busy;
                Pose3d ownedPose;
                lock(poseTreeGate)ownedPose=new Pose3d(pd.RobotPose);
                var ownedData=pd with{RobotPose=ownedPose};
                bool enqueued=false;
                lock(_resultGate)
                {
                    if(!StaleFrame())
                    {
                        _preparedResults.Enqueue(new PreparedVisionResult(removal,sourceOwner,ownedPose,poseTreeGate,()=>
                        {ApplyResult(ownedData);return handlerFailed;}));
                        enqueued=true;
                    }
                }
                if(!enqueued)lock(poseTreeGate)ownedPose.Dispose();
                return null;
            }
            return ApplyResult(pd);
        }
    }

    internal static Transform3d ComposeNativeFacePose(Vec3 translation, Transform3d camera, Transform3d robot)
    {
        var pose = new Transform3d(Rotation3d.FromAngleAxis(BitConverter.UInt32BitsToSingle(0x3FC90FDB), new Vec3(1, 0, 0)), translation);
        pose.PreComposeWith(ref camera);
        pose.PreComposeWith(ref robot);
        return pose;
    }

    internal static PoseTreeNode? ResolveFaceObservationOrigin(RobotStateHistory history,EngineRobot owner,uint timestamp,bool native=true)
    {
        if(!history.ComputeFaceObservationOriginId(timestamp,out uint originId,native))
        { SteppedBehavior.ReportMissing("M14-008 FaceWorld observation-time historical pose unavailable; face update skipped"); return null; }
        if(!owner.TryGetNativeOriginOwner(originId,out var origin) || origin?.Node is not { } node)
        { SteppedBehavior.ReportMissing("M14-008 FaceWorld observation origin ID has no registered native PoseOriginList owner; face update skipped"); return null; }
        return node;
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
        _renameRequest.Dispose();
        _eraseRequest.Dispose();
        _fileAlbumRequest.Dispose();
        // Do not hold a host lock across join or callbacks on the worker's exit log.
        StopProcessor();
        if(ReferenceEquals(World.NativeObjectObserved,_nativeObjectObserved))World.NativeObjectObserved=null;
        bool stockAlbumFailed=false;
        lock(_busy)
        {
            DiscardPreparedResults();
            OkaoPetDetector? pet;
            lock (_groundGate) { pet=_nativeInitialization?.StockPet; if (_nativeInitialization is { } state) state.StockPet=null; }
            pet?.Retire();
            DrainRetiredMotionStates();
            try
            {
                _stockFaceDetector.RetireRecognition();
                if (_stockRecognizer && _stockFaceDetector.NativeUsable) stockAlbumFailed=Recognizer.ShutdownStockDeferred();
                else if (_stockRecognizer) SteppedBehavior.ReportMissing("M1-034 M14-011 faulted recognition owner skipped guest shutdown");
            }
            finally { _stockFaceDetector.Dispose(); }
        }
        if(stockAlbumFailed)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"FaceRecognizer.Shutdown.FaceLibAlbumHandleDeleteFail","");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
        }
        ReportFaceCleanup(_stockFaceDetector);
        if(_robot.Engine.VisionUpdateAllResultsFailed==DrainPreparedResults)_robot.Engine.VisionUpdateAllResultsFailed=null;
        // V3-V7 depend on native recipient ownership, not host reset/GC.
        SteppedBehavior.ReportMissing("M11-055 V3-V7 0x00652580..0x00652718 / 0x006B1E92..0x006B2142: native VisionSystem/member ownership and recursive recipient teardown are not built; host subscriptions are detached after join");
        // fidelity: M11-078, M11-090 — checked leaves do not establish the missing constructed recipient graph.
        SteppedBehavior.ReportMissing("M11-078/M11-090 Vision teardown: native result lists/deque 0x006586EC..0x00658A92/0x006B9F68..0x006BA092, ImageCache null-before-tree 0x006B1E9C..0x006B2142, Camera/ImageCache/CLAHE, FaceRecognizer trees, Block/MarkerlessObject and ProtoHandle reset/remove need their owned native recipients; host caches/GC are not those owners. Capture finalizers and allocator recipients remain unread; SDK CustomObject is unreachable");
        if (!_stockRecognizer) Recognizer.Shutdown();
        SteppedBehavior.ReportMissing("M11-055 Profiler timer producers/Tic/Toc and VisionSystem::Init PerformanceLogging binding 0x006B0E5C..0x006B0F2A are not built; native timer input boundary only, no host Stopwatch data substituted");
        LifetimeProfiler.Dispose();
        if (_robot.Lights.DetachVisionSystem(this))
        {
            _robot.Lights.Cubes.IsLocalized = () => _robot.Sensors.OffTreads.Robot2C4 != 0;
        }
        _robot.MessageInternal -= OnMessage;
        _robot.Engine.DelocalizationReported -= OnDelocalizationReported;
        _robot.Camera.FrameForVision -= OnFrame;
        _robot.CameraSettings.NativeCalibrationInstalled -= OnCalibrationInstalled;
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
    /// <c>Robot::ComputeHeadAngleToSeePose(pose, &amp;angle, tolerance)</c> 0x00518344..0x00518620 (M13-026), the engine's own iteration. The pose (already in the robot frame) is taken with respect to the neck pose ([robot+0x2CC]:
    /// (-13, 0, 49) in the robot frame, 0x0051838E; a failure is "Robot.ComputeHeadAngleToSeePose.OriginMismatch" and 0x06000000); <c>dist = sqrtf(x*x + y*y)</c>,
    /// <c>P = (dist, 0, z)</c> (0x005183A6..0x005183E4). Without a camera calibration: "NullCamera" and 1 (0x0051851E). The threshold is <c>(float)rows * tolerance + 1e-5f</c> (0x00518432..0x00518436),
    /// the angle starts at 0.0f and the loop executes iterations 1 through 25: the camera pose for the angle is the head-cam pose pre-multiplied by a rotation about Y of -angle
    /// (<c>Robot::GetCameraPose</c> 0x00510FFC), <c>Q = inverse * P</c> (0x00518448..0x00518480); <c>Q.z &lt;= 1e-5f</c> is "BadProjectedZ" and 1 (0x00518494); <c>dv = fy * (Q.y / Q.z)</c>; when
    /// <c>threshold &gt;= |dv|</c> (0x005184AE) the angle is written (0x005185B2/B6); k=25 then returns "MaxIterations" and 1 (0x005185C6..0x00518600); otherwise <c>angle += atan2f(dv, fy) * -0.8f</c>
    /// (0x005184BC..0x005184DA, -0.8f = 0xBF4CCCCD). After the 25th non-converged iteration the function returns 0 with the angle UNASSIGNED (the counter compared at 0x005185C6 is then 26): the caller's
    /// Radians local, 0, is the result.
    /// <para>The native overload requires the actual live MAIN owner for the neck and uses checked Transform3d/Rotation3d arithmetic;
    /// scalar operations follow the sequential binary32 order. Host libm remains the existing platform boundary.
    /// This public static adapter preserves its labelled M11-066 flattened-input behavior and is not the native receiver.</para>
    /// </summary>
    // fidelity: M11-066
    public static uint ComputeHeadAngleToSeePose(CameraCalibration? cal, Pose3d poseInRobotFrame, float tolerance, out float angle, Action<string>? log = null)
        =>ComputeHeadAngleInput(cal,poseInRobotFrame,tolerance,out angle,log,null,true);

    // fidelity: M13-026
    internal static uint ComputeHeadAngleToSeePose(CameraCalibration? cal,Pose3d poseInRobotFrame,float tolerance,out float angle,Action<string>? log,Pose3d actualMain)
        =>ComputeHeadAngleInput(cal,poseInRobotFrame,tolerance,out angle,log,actualMain??throw new ArgumentNullException(nameof(actualMain)),false);

    private static uint ComputeHeadAngleInput(CameraCalibration? cal,Pose3d poseInRobotFrame,float tolerance,out float angle,Action<string>? log,Pose3d? actualMain,bool publicAdapter)
    {
        angle = 0.0f;
        using var neck=new Pose3d(Mat3.Identity,HeadGeometry.NeckPositionMm);
        if(actualMain is not null)neck.SetParent(actualMain);
        else if(poseInRobotFrame.HasParent){Pose3d parent=null!;if(poseInRobotFrame.TryGetParent(ref parent))using(parent)neck.SetParent(parent);}
        using var relativeToNeck=new Pose3d("");
        if(!poseInRobotFrame.TryWithRespectTo(neck,relativeToNeck))
        {
            // Preserve the existing labelled M11-066 public flattened-input adapter only.
            // Native parented callers retain the checked conversion-failure result.
            if(publicAdapter&&poseInRobotFrame.HasLegacyFrameInput&&neck.HasLegacyFrameInput&&!poseInRobotFrame.HasParent&&!neck.HasParent)
            {using var legacy=poseInRobotFrame.WithRespectTo(neck);relativeToNeck.AssignInputValue(legacy);}
            else return 0x06000000;
        }
        var wrt=relativeToNeck.Translation; // 0x0051838E: goal and neck both belong to the robot frame
        float tx = (float)wrt.X, ty = (float)wrt.Y, tz = (float)wrt.Z;
        float sq = tx * tx;
        sq = sq + ty * ty;
        float dist = MathF.Sqrt(sq);                                                          // vsqrt.f32 0x005183BA
        var p = new Vec3(dist, 0.0, tz);
        if (cal is null) { log?.Invoke("Robot.ComputeHeadAngleToSeePose.NullCamera"); Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();return 1; }
        float fy = (float)cal.FocalLengthY;
        float threshold = (float)(ushort)cal.Rows * tolerance;
        threshold += BitConverter.Int32BitsToSingle(0x3727C5AC);
        float eps = BitConverter.Int32BitsToSingle(0x3727C5AC);
        float a = 0.0f;
        using var camInNeck = new Pose3d(HeadGeometry.DefaultHeadCamRotation, HeadGeometry.EngineHeadCamPositionMm);   // the engine's own composition (Robot::Robot 0x0050FF7C..0x0050FFBA)
        for (int k = 1; k <= 25; k++)
        {
            using var cam = HeadGeometry.GetCameraPose(camInNeck, a);                                   // GetCameraPose(a) 0x00510FFC relative to the neck: RotateBy(RotationVector3d(Radians(-a), Y)) (R28, R22)
            using var inverse=cam.Inverse();
            var q = inverse.Apply(p);
            float qy = (float)q.Y, qz = (float)q.Z;
            if (qz <= eps) { log?.Invoke("Robot.ComputeHeadAngleToSeePose.BadProjectedZ"); return 1; }
            float dv = fy * (qy / qz);
            if (threshold >= MathF.Abs(dv))
            {
                angle = (float)Cozmo.Robot.Manipulation.EngineRadians32.Rescale(a);               // Radians::operator= 0x005185B6
                if (k == 25) { log?.Invoke("Robot.ComputeHeadAngleToSeePose.MaxIterations"); return 1; }
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
