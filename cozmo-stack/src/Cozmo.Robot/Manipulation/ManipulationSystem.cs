using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// The cube-manipulation foundation: paths, docking and carrying over the M11 world model. This is what the
/// engine spreads over <c>PathComponent</c>, <c>DockingComponent</c>, <c>CarryingComponent</c> and the action
/// classes; the actions here are async methods rather than the engine's ticked <c>IActionRunner</c>s.
/// </summary>
public sealed class ManipulationSystem : IDisposable
{
    public ManipulationSystem(CozmoRobot robot, VisionSystem vision)
        : this(robot,vision,robot.Engine.Robot) { }
    /// <summary>Explicit host driver with a private whiteboard/configuration clock. Native services are borrowed.</summary>
    public static ManipulationSystem CreateStandalone(CozmoRobot robot,VisionSystem vision,Func<double> clockSec)
        =>robot.Engine.InvokeEngineRequest(()=>new ManipulationSystem(robot,vision,robot.Engine.Robot,true,clockSec));
    internal ManipulationSystem(CozmoRobot robot, VisionSystem vision, EngineRobot? nativeOwner)
        :this(robot,vision,nativeOwner,false,null){}
    private ManipulationSystem(CozmoRobot robot,VisionSystem vision,EngineRobot? nativeOwner,bool standalone,Func<double>? clockSec)
    {
        Robot = robot; Vision = vision;
        AiExpressedNeedValue=()=>robot.Engine.Needs.SevereNeeds.SelectedNeed;
        var shared=nativeOwner?.NativeBehaviorRuntime?.Manipulation;
        if(standalone&&(shared is null||shared._disposed||!ReferenceEquals(robot.Engine.Robot,nativeOwner)))throw new InvalidOperationException("CreateStandalone requires the actual live native manipulation owner to borrow services");
        _standalone=standalone;
        _ownsSharedServices=shared is null;
        _ownsWhiteboard=standalone||_ownsSharedServices;
        _clockSec=standalone?(clockSec??throw new ArgumentNullException(nameof(clockSec))):()=>robot.Engine.Timer.SecondsF;
        Paths=shared?.Paths??new PathSender(robot);
        Follower=shared?.Follower??new PathFollower(robot);
        Docking = shared?.Docking??new DockingSystem(robot, vision,nativeOwner);
        _dockingLogForwarder = l => Log?.Invoke(l);
        Docking.Log += _dockingLogForwarder;
        Configurations = new BlockConfigurationManager(vision.World, () => ClockSec()) { NativeRobot = () => robot.Engine.Robot };
        if(_ownsSharedServices)Docking.Configurations = Configurations; // host extension belongs only to the owned service
        // M11-072: the native R2 callback owns +0xC even before this host facade exists.
        Whiteboard = standalone?new AIWhiteboard(vision.World,()=>ClockSec()):shared?.Whiteboard??new AIWhiteboard(vision.World, () => ClockSec());
        if(_ownsSharedServices)vision.World.MapClearQuad=Whiteboard.ProcessClearQuad;
        // fidelity: M8-026
        Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("M8-026 Robot::GetWorldOrigin 0x004A4C54 to AIWhiteboard 0x0056AC92: native world-origin capture provider is not bound; ProcessClearQuad production unit is built");
        // AIWhiteboard::Init 0x0056a394 registers the three handlers through the robot's external interface
        // or warns "Initialized whiteboard with no external interface. Will miss events." (M8-014). This
        // stack has no external interface registered, so the warning path is the live one.
        if(_ownsSharedServices)Whiteboard.Init();
        // fidelity: M15-020
        // The FindCubesInBeacon predicate's first test, CarryingComponent::IsCarryingObject(id) (0x0056D142).
        if(_ownsWhiteboard)
        {
        Whiteboard.IsCarryingObject = Docking.Carrying.IsCarryingObjectId;
        Whiteboard.CarriedObjectId = () => Docking.Carrying.CarriedObjectId;
        Whiteboard.CanPickUpObject = Docking.CanPickUpObject;
        }
        // fidelity: M11-037, M11-063, M13-040
        // 0x00624892 queues IDs only; 0x0062520C consumes them at the frame tail.
        vision.World.SetObjectPoseChanged = Configurations.SetObjectPoseChanged;
        vision.World.BlockConfigurationManagerUpdate = Configurations.Update;
        if(_ownsSharedServices)
        {
        vision.World.UnsetCarryingObjects = topOnly => Docking.Carrying.UnsetCarrying(topOnly);
        vision.World.CarriedPrimaryId = () => Docking.Carrying.CarriedObjectId;

        // fidelity: M4-003, M4-009, M4-012
        // The M4 control layer's M12 seams. MA12: a lift height of exactly 32 mm while carrying becomes
        // PlaceObjectOnGroundAction. MA20: a lift that starts calibrating while carrying detaches the carried object
        // (SetCarriedObjectAsUnattached(true) = ReleaseCarriedObject(forget: true)). CD10a/CD10b and C11.1: the carried
        // object (CarryingComponent+8) and the dock target (DockingComponent+0xC) are excluded from the
        // Moved/Stopped broadcast.
        robot.Motion.IsCarryingObject = () => Docking.Carrying.IsCarryingObject;
        robot.Motion.PlaceObjectOnGroundAsync = async () =>
        {
            var result = await new PlaceObjectOnGroundAction(this).RunAsync(CancellationToken.None);
            return new MotionOutcome(result == ActionResult.Success ? MotionResult.Acknowledged : MotionResult.Failed,
                $"PlaceObjectOnGroundAction: {result}");
        };
        robot.Sensors.UnattachCarriedObjectIfCarrying = () =>
        {
            if (Docking.Carrying.IsCarryingObject) Docking.ReleaseCarriedObject(forget: true);
        };
        // fidelity: M4-009
        // CD10a/CD10b: the carried object (CarryingComponent+8) and the dock target (DockingComponent+0xC, C11.1)
        // are excluded from the Moved/Stopped broadcast.
        // Cube.ObjectId is the radio slot (activeID); the carried object and the dock target are BlockWorld ObjectIDs (M11-013, M11-041), so the slot is translated to the connected object's ObjectID first.
        robot.Cubes.ExcludeFromMovedBroadcast = c => c.ObjectId is { } slot && vision.World.ConnectedObjectIdForActiveId(slot) is { } id
            && (Docking.Carrying.IsCarrying(id) || Docking.DockTargetObjectId == id);
        // LC3: the default-layer choice prefers the carried object over Visible and Connected (the carried ObjectID is a world ID: its type is the connected object's).
        robot.Lights.Cubes.IsCarried = t => Docking.Carrying.CarriedObjectId is { } id
            && vision.World.ConnectedObjects.FirstOrDefault(o => o.ObjectId == id)?.Type == t;
        }
        // Retire only the exact callbacks installed by this owner; later replacements remain usable.
        var hostHook0=vision.World.SetObjectPoseChanged;
        _installedCallbacks.Add(()=>{if(ReferenceEquals(vision.World.SetObjectPoseChanged,hostHook0))vision.World.SetObjectPoseChanged=null;});
        var hostHook1=vision.World.BlockConfigurationManagerUpdate;
        _installedCallbacks.Add(()=>{if(ReferenceEquals(vision.World.BlockConfigurationManagerUpdate,hostHook1))vision.World.BlockConfigurationManagerUpdate=null;});
        if(_ownsSharedServices)
        {
            var pathSend=robot.Motion.Path.SendPathMessage;
            _installedCallbacks.Add(()=>{if(ReferenceEquals(robot.Motion.Path.SendPathMessage,pathSend))robot.Motion.Path.SendPathMessage=null;});
            var ownedHook0=robot.Motion.IsCarryingObject;
            _installedCallbacks.Add(()=>{if(ReferenceEquals(robot.Motion.IsCarryingObject,ownedHook0))robot.Motion.IsCarryingObject=null;});
            var ownedHook1=robot.Motion.PlaceObjectOnGroundAsync;
            _installedCallbacks.Add(()=>{if(ReferenceEquals(robot.Motion.PlaceObjectOnGroundAsync,ownedHook1))robot.Motion.PlaceObjectOnGroundAsync=null;});
            var ownedHook2=robot.Sensors.UnattachCarriedObjectIfCarrying;
            _installedCallbacks.Add(()=>{if(ReferenceEquals(robot.Sensors.UnattachCarriedObjectIfCarrying,ownedHook2))robot.Sensors.UnattachCarriedObjectIfCarrying=null;});
            var ownedHook3=robot.Cubes.ExcludeFromMovedBroadcast;
            _installedCallbacks.Add(()=>{if(ReferenceEquals(robot.Cubes.ExcludeFromMovedBroadcast,ownedHook3))robot.Cubes.ExcludeFromMovedBroadcast=null;});
            var ownedHook4=robot.Lights.Cubes.IsCarried;
            _installedCallbacks.Add(()=>{if(ReferenceEquals(robot.Lights.Cubes.IsCarried,ownedHook4))robot.Lights.Cubes.IsCarried=null;});
            var ownedHook5=vision.World.UnsetCarryingObjects;
            _installedCallbacks.Add(()=>{if(ReferenceEquals(vision.World.UnsetCarryingObjects,ownedHook5))vision.World.UnsetCarryingObjects=null;});
            var ownedHook6=vision.World.CarriedPrimaryId;
            _installedCallbacks.Add(()=>{if(ReferenceEquals(vision.World.CarriedPrimaryId,ownedHook6))vision.World.CarriedPrimaryId=null;});
        }
    }
    private readonly List<Action> _installedCallbacks=new();
    private readonly bool _ownsSharedServices,_ownsWhiteboard,_standalone;
    private Func<double> _clockSec;
    private readonly Action<string> _dockingLogForwarder;
    private bool _disposed;

    /// <summary>
    /// The same seam as <c>BehaviorContext.AiExpressedNeedValue</c>: <c>[[[robot+0x264]+0x30]+0x14]</c>, the SevereNeedsComponent's severe NeedId (3 = none when unset or null).
    /// <c>ShouldApplyDockingSquint</c> 0x005524AC reads it.
    /// </summary>
    public Func<Cozmo.Robot.Behavior.NeedId?>? AiExpressedNeedValue { get; set; }

    /// <summary>Native views read the engine clock. CreateStandalone owns a replaceable host clock shared with its private whiteboard/configuration adapter.</summary>
    public Func<double> ClockSec
    {
        get=>_clockSec;
        set
        {
            if(!_standalone&&!_ownsSharedServices)throw new InvalidOperationException("Native manipulation views use the engine clock. Use CreateStandalone for a controlled host clock.");
            _clockSec=value??throw new ArgumentNullException(nameof(value));
        }
    }

    /// <summary>
    /// The engine's lattice planner (M13), when the motion-primitive set is available: <see cref="DriveToPoseAction"/>
    /// plans with it around the world's located objects and falls back to <see cref="StraightLinePlanner"/> when
    /// it is null or finds no plan (the engine's <c>PathComponent::SelectPlanner</c> also keeps a simpler planner
    /// for short, clear goals; LOCAL_POLICY fallback).
    /// </summary>
    public LatticePlanner? Planner { get; set; }
    internal NativePlannerSet? NativePlanners;
    internal NativePlannerSet GetNativePlanners()=>NativePlanners is null || NativePlanners.IsDisposed?NativePlanners=new(this):NativePlanners;

    /// <summary>Attaches the lattice planner from an OBB root's <c>cozmo_mprim.json</c>; false when the file is missing.</summary>
    public bool LoadPlanner(string obbRoot)
    {
        var prims = MotionPrimitiveSet.FromObb(obbRoot);
        if (prims is null) return false;
        Planner = new LatticePlanner(new LatticeEnvironment(prims));
        NativePlanners?.Dispose();NativePlanners=null;GetNativePlanners(); // native worker exists before the first ComputePath
        return true;
    }
    internal bool LoadNativePlanner(string resources)
    {
        if(Planner is not null)return true;
        string path=Path.Combine(resources,"config","engine","cozmo_mprim.json");
        if(!File.Exists(path)){Robot.Engine.EmitStackLog("MISSING: M13-033 native planner motion primitive asset unavailable: "+path);return false;}
        MotionPrimitiveSet primitives;
        try{primitives=MotionPrimitiveSet.Parse(File.ReadAllText(path));}
        catch(Exception error)when(error is IOException or InvalidDataException or System.Text.Json.JsonException)
        {Robot.Engine.EmitStackLog("MISSING: M13-033 native planner motion primitive load failed: "+error);return false;}
        Planner=new LatticePlanner(new LatticeEnvironment(primitives));
        NativePlanners?.Dispose();NativePlanners=null;GetNativePlanners();return true;
    }

    /// <summary>
    /// The BehaviorManager the actions take reaction locks through (<c>[robot+0x44]</c>): <c>PlaceObjectOnGroundAction::Init</c> 0x005548C6 takes "placeOnGroundAction" for EVERY action. Reads the current native robot owner even before Freeplay; null before a native robot exists. An explicit host/unit override can be supplied.
    /// </summary>
    // fidelity: M15-022
    private Cozmo.Robot.Behavior.BehaviorManager? _reactionLocksOverride;
    public Cozmo.Robot.Behavior.BehaviorManager? ReactionLocks { get=>_reactionLocksOverride??Robot.BehaviorManager; set=>_reactionLocksOverride=value; }

    /// <summary>The block configurations (stacks, pyramid bases, pyramids) over the world model, rebuilt on every observation.</summary>
    public BlockConfigurationManager Configurations { get; }
    internal NativeConfigurationManager NativeConfigurations
    {
        get
        {
            var owner=World.NativeConfigurations;
            if(owner is null)
                throw new NativeConfigurationUnavailable("actual native configuration producer owner absent; no host/empty snapshot substituted");
            if(!owner.InputsAvailable)
                Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("Native configuration consumer: rebuild inputs UNKNOWN; retained complete prior publication used, no host/empty cache substituted");
            return owner;
        }
    }
    internal IReadOnlyList<NativePyramidBase> NativePyramidBases=>NativeConfigurations.Snapshot.Bases;
    internal IReadOnlyList<NativePyramidConfiguration> NativePyramids=>NativeConfigurations.Snapshot.Pyramids;
    internal IReadOnlyList<NativeBlockConfiguration> NativeCache(BlockConfigurationType type)=>NativeConfigurations.Snapshot.Cache(type);
    internal StackOfCubes? NativeTallestStack()=>NativeConfigurations.Tallest()?.View;
    internal bool NativeIsPartOfConfiguration(uint id,BlockConfigurationType type)=>NativeConfigurations.Contains(id,type);

    /// <summary>Beacons and object-failure memory shared by the behaviours.</summary>
    public AIWhiteboard Whiteboard { get; }
    /// <summary>The workout configs, when loaded from the OBB.</summary>
    public WorkoutComponent? Workouts { get; set; }

    public CozmoRobot Robot { get; }
    // Test-only dependency isolation for geometry/wire fixtures; null on every production construction.
    // fidelity: M12-024 — does not supply or claim the native action-side Init/start/end lifecycle.
    internal Func<IDrivingAnimationHandler>? DrivingAnimationsForTest { get; set; }
    // Explicit fixtures only. Production must receive recovered native PathComponent/timeout inputs before claiming this path.
    internal Func<DriveToPoseAction,DriveToPoseTick>? DriveTickForTest { get; set; }
    internal Func<DriveToPoseAction,float>? DriveTimeoutForTest { get; set; }
    internal Action<ActionResult>? DriveResultObservedForTest { get; set; }
    internal Func<bool>? PathIsActiveForTest { get; set; }
    internal Action<bool>? ErasePlannerObstaclesForTest { get; set; }
    internal Func<DriveStraightAction,DriveStraightTick>? StraightTickForTest { get; set; }
    internal Func<DriveStraightAction,float>? StraightTimeoutForTest { get; set; }
    public VisionSystem Vision { get; }
    public BlockWorld World => Vision.World;
    public PathSender Paths { get; }
    public PathFollower Follower { get; }

    /// <summary>
    /// Starts a path and returns its owner with ExecuteCustomPath's result. Nonempty paths reserve before sending;
    /// empty or failed starts return without an event wait. An unfinished successful wait clears its owned path.
    /// </summary>
    public PathRun StartPath(IReadOnlyList<PathSegment> path)
    {
        PathFollower.Reservation? reservation = null;
        var execution = Paths.ExecuteWithResult(path, newId => reservation = Follower.Reserve(newId));
        return new PathRun(this, execution.PathId, reservation, execution.Result);
    }
    public DockingSystem Docking { get; }
    public event Action<string>? Log;

    /// <summary>Replaceable turn, for tests without a robot (the locator's own override is used when set).</summary>
    public Func<uint, double, CancellationToken, Task<bool>>? TurnOverride { get; set; }

    /// <summary>
    /// How an action waits out a real interval - the look-around pauses of
    /// <see cref="SearchForNearbyObjectAction"/>, for one. Replaceable so a rig can run a search without
    /// sitting out its three seconds.
    /// </summary>
    public Func<TimeSpan, CancellationToken, Task> Wait { get; set; } = (t, c) => Task.Delay(t, c);

    public Pose3d? RobotPose() => Vision.History.Latest?.RobotPose;

    internal Pose3d NativeMainSnapshot()=>Robot.Engine.Robot?.CopyCheckedMainPose()
        ??throw new NativeActionUnavailable("M12-029 actual MAIN owner unavailable");
    internal List<PreActionObstacle> NativeObstacles(Pose3d main)
    {
        var owner=Robot.Engine.Robot??throw new NativeActionUnavailable("M12-036 actual Robot owner unavailable");
        var carrying=owner.CarryingOwner??throw new NativeActionUnavailable("M12-036 actual carrying owner unavailable");
        using var root=main.GetWithRespectToRoot();
        var obstacles=PreActionValidity.GetObstacles(World.FindLocatedMatchingObjects(new BlockWorldFilter()),carrying.GetCarryingObjects(),root,NativeHeight(),0f);
        if(obstacles.Any(o=>o.IsStandIn))throw new NativeActionUnavailable("M12-036 native obstacle footprint input unavailable; no planar stand-in on the native approach");
        return obstacles;
    }
    internal float NativeHeight()
    {
        float lift=Robot.Engine.Robot?.NativeLiftAngle??throw new NativeActionUnavailable("M12-036 actual synchronized raw lift input unavailable");
        float height=66f*MathF.Sin(lift);height=height+45f;height=height+0f;height=height+5f;
        float floor=BitConverter.UInt32BitsToSingle(0x42876666);return height>floor?height:floor;
    }

    // Owned, scoped input for native pre-action geometry only; general callers use RobotPose's history value.
    internal Pose3d? CopyPreActionRobotPose() => Vision.History.Latest is {} latest
        ? Robot.Engine.Robot?.CopyCheckedMainPose() ?? new Pose3d(latest.RobotPose) : null;


    /// <summary>
    /// <c>Robot::GetHeight</c> 0x00516F0C = <c>max(66·sin(liftAngle) + 45 + 5, 67.7)</c> (66 at 0x00516F54, 45 at 0x00516F58, 5.0, floor 0x42876666 = 67.7; C-E7/E9). It is the z of the
    /// <c>Point3</c> tolerance <c>DriveToPoseAction::CheckIfDone</c> passes to <c>IsSameAs</c> (0x0055ACBA, 0x0055ACC2/0x0055ACF8) and <c>maxZ - minZ</c> in <c>BlockWorld::GetObstacles</c> (M12-036).
    /// </summary>
    // fidelity: M12-023, M12-036
    public double RobotHeightMm()
    {
        float lift = Robot.State.Latest?.LiftAngle ?? 0f;
        return Math.Max((double)RobotState.LiftHeightMmFromAngle(lift) + 5.0, 67.7);
    }

    /// <summary>
    /// <c>BlockWorld::GetObstacles(vec, 0.0)</c> 0x00626D44 as <c>IDockAction::GetPreActionPoses</c> calls it (M12-036): the ignore-ID set is the result of
    /// <c>CarryingComponent::GetCarryingObjects</c>, here the carried object and the object resting on it (<c>CarryingComponent+0x14</c>): 0x00633BB0 inserts [this+4] when [this+8] != -1 and [this+0x10] when [this+0x14] != -1
    /// (fix-round-2 verifier). See <see cref="PreActionValidity.GetObstacles"/>.
    /// </summary>
    // fidelity: M12-036
    public List<PreActionObstacle> GetObstacles(Pose3d robotPose)
    {
        var ignore = new List<uint>();
        if (Docking.Carrying.CarriedObjectId is { } carried) ignore.Add(carried);
        if (Docking.Carrying.CarriedOnTopId is { } onTop) ignore.Add(onTop);
        return PreActionValidity.GetObstacles(World.LocatedObjects, ignore, robotPose, RobotHeightMm(), 0f);
    }

    /// <summary><c>TurnTowardsObjectAction</c>: the located object's pose through <see cref="TurnTowardsPose"/>.</summary>
    public Task<bool> TurnTowardsObjectAsync(uint objectId, double maxTurnRad, CancellationToken cancel)
    {
        if (TurnOverride is not null) return TurnOverride(objectId, maxTurnRad, cancel);
        return Vision.Locator.TurnTowardsAsync(objectId, maxTurnRad, cancel);
    }

    /// <summary>
    /// <c>VisuallyVerifyObjectAction</c>: waits until a frame observes the object and returns the marker seen
    /// (the closest to the image centre when several). Null when nothing is seen in time.
    /// </summary>
    public async Task<KnownMarker?> WaitForVisibleMarkerAsync(uint objectId, TimeSpan timeout, CancellationToken cancel)
    {
        var tcs = new TaskCompletionSource<KnownMarker?>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnObserved(ObjectObservation o)
        {
            if (o.Object.ObjectId != objectId) return;
            var code = o.Markers.FirstOrDefault();
            tcs.TrySetResult(o.Object.Markers.FirstOrDefault(m => m.Code == code));
        }
        World.ObjectObserved += OnObserved;
        try
        {
            // a sighting in the frame just processed counts
            var obj = World.GetObjectById(objectId);
            if (obj is { IsLocated: true } && Vision.LastResult is { } last && last.Objects.Any(x => x.Object.ObjectId == objectId))
                return obj.Markers.FirstOrDefault(m => obj.LastObservedMarkers.Contains(m.Code));
            using var reg = cancel.Register(() => tcs.TrySetResult(null));
            var done = await Task.WhenAny(tcs.Task, Task.Delay(timeout, CancellationToken.None));
            return done == tcs.Task ? tcs.Task.Result : null;
        }
        finally { World.ObjectObserved -= OnObserved; }
    }

    public void Dispose()
    {
        if(_disposed)return;
        _disposed=true;
        Docking.Log -= _dockingLogForwarder;
        foreach(var detach in _installedCallbacks)detach();
        _installedCallbacks.Clear();
        if(_standalone)Whiteboard.RetireStandaloneStorage();
        else if(_ownsWhiteboard)Whiteboard.ClearAllBeacons(); // public handles do not retire the native shared owner
        NativePlanners?.Stop();NativePlanners?.Dispose();
        if(_ownsSharedServices && World.MapClearQuad==Whiteboard.ProcessClearQuad)World.MapClearQuad=null;
        if(_ownsSharedServices)Follower.Dispose();
        if(_ownsSharedServices)Docking.Dispose();
    }
}

/// <summary>
/// The engine's <c>PickupBlockHelper</c> (0x005B7B48..0x005B8610) and <c>RollBlockHelper</c>: drive to the
/// object's pre-action pose when not already there ("Cozmo is not at pre-action pose for cube %d, delegating to
/// driveToHelper"), then run the dock action without its own pre-action check ("Picking up target object %d");
/// on a failed dock, retry ("Failed dock attempt %d / %d"), giving up after the attempt limit ("Failing helper
/// because pickup was already attempted %d times"); a visual-observation failure that saw an unexpected
/// object marks the target as failed and tries another; other failures retry from a different pre-dock pose.
/// The pickup limit is two (M12-009) and the roll limit three (M12-016).
/// </summary>
/// <summary>
/// A path this process started and owns until it ends: the reservation is taken before the path is sent, the
/// wait cannot miss an immediate terminal event, and a wait that is cancelled or times out clears the path on
/// the robot instead of leaving firmware motion running behind an abandoned action.
/// </summary>
public sealed class PathRun : IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly PathFollower.Reservation? _reservation;
    private int _ended;

    internal PathRun(ManipulationSystem m, ushort pathId, PathFollower.Reservation? reservation,int result)
    { _m = m; PathId = pathId; _reservation = reservation; Result=result; if(result!=0 || reservation is null)_ended=1; }

    public ushort PathId { get; }
    /// <summary>ExecuteCustomPath's native return code: zero on success.</summary>
    public int Result { get; }
    /// <summary>Whether the path was aborted because the wait was cancelled or timed out.</summary>
    public bool Aborted { get; private set; }

    /// <summary>
    /// Waits for the path's terminal event. A failed start returns null immediately (see Result); an empty success
    /// completes locally. A successful nonempty wait that times out or is cancelled clears its owned path.
    /// </summary>
    public async Task<PathEventType?> WaitAsync(TimeSpan timeout, CancellationToken cancel)
    {
        if(Result!=0)return null;
        if(_reservation is null)return PathEventType.Completed; // Host empty-path completion; no synthetic engine event.
        var ev = await _reservation.WaitAsync(timeout, cancel);
        if (ev is null) Abort();
        else Interlocked.Exchange(ref _ended, 1);
        return ev;
    }

    /// <summary>
    /// Clears the path on the robot, but only while this run still owns it. Safe to call more than once.
    ///
    /// A run whose wait was cancelled or timed out can be cleaning up long after another action has
    /// installed its own path; clearing unconditionally would stop that one instead. Returns nothing
    /// because the caller has nothing to do either way - <see cref="Aborted"/> records that this run gave
    /// up, and <see cref="ClearedRobotPath"/> whether the robot was actually told.
    /// </summary>
    public void Abort()
    {
        if (Interlocked.Exchange(ref _ended, 1) != 0) return;
        Aborted = true;
        ClearedRobotPath = _m.Paths.AbortIfCurrent(PathId);
    }

    /// <summary>Whether this run's abort actually cleared the robot's path, or found it no longer owned.</summary>
    public bool ClearedRobotPath { get; private set; }

    public void Dispose() => _reservation?.Dispose();
}

public sealed class DockHelper
{
    // fidelity: M12-009, M12-016
    /// <summary>
    /// Two, which is what <c>PickupBlockHelper::RespondToPickupResult</c> allows: at 0x005B8192 it reads
    /// the attempt count at +0x108 and takes the retry branch only while it is <c>&lt;= 1</c>, and the
    /// log beside it is built with a literal 2 (<c>movs r6, #2</c> at 0x005B814A), so it reads
    /// "attempt 1 / 2" and "attempt 2 / 2". This stack had three.
    /// </summary>
    public const int MaxAttempts = 2;

    /// <summary>
    /// Three, for a roll. <c>RollBlockHelper::StartRollingAction</c> begins by reading its own attempt
    /// count at helper+0x124 and comparing it with the literal 3 (<c>cmp r0, #3</c> at 0x005B9F0E);
    /// at or above it the helper calls <c>MarkTargetAsFailedToRoll</c> and stops, and otherwise it
    /// increments the count and starts another <c>DriveToRollObjectAction</c>. The limit is not carried
    /// in <c>RollBlockParameters</c> after all - the ctor at 0x005B98D4 copies only the callback, two
    /// words at +0x110 and a <c>Radians</c> at +0x118, and zeroes the counter at +0x124.
    /// </summary>
    public const int MaxRollAttempts = 3;

    private readonly ManipulationSystem _m;

    public DockHelper(ManipulationSystem m) => _m = m;

    /// <summary>How many attempts this helper makes. The engine's limit is per helper, not shared.</summary>
    public int AttemptLimit { get; init; } = MaxAttempts;

    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();
    public int Attempts { get; private set; }
    /// <summary>The pre-action poses already tried and failed, excluded from the next attempt.</summary>
    public IReadOnlyList<Pose3d> ExcludedPoses => _excluded;
    private readonly List<Pose3d> _excluded = new();

    /// <summary>
    /// Drive to the object (for the action's pre-action type) and run the dock action, with retries.
    ///
    /// A retry approaches from a different pre-dock pose, as <c>IBehavior::UseSecondClosestPreActionPose</c>
    /// (0x005BEE56) does: it asks <c>DriveToObjectAction::GetPossiblePoses</c> again and, while more than one
    /// pose remains (<c>cmp r0, #2</c> at 0x005BEE80), removes the one just used through
    /// <c>IDockAction::RemoveMatchingPredockPose</c>. Retrying from the identical geometry that just failed is
    /// what the engine avoids; the pose to exclude is the one the failed attempt actually drove to.
    /// </summary>
    public async Task<ActionResult> RunAsync(uint objectId, PreActionType type, Func<DockActionBase> makeAction, CancellationToken cancel)
    {
        ActionResult last = ActionResult.Abort;
        _excluded.Clear();
        for (Attempts = 1; Attempts <= AttemptLimit; Attempts++)
        {
            if (cancel.IsCancellationRequested) return ActionResult.CancelledWhileRunning;
            var drive = new DriveToObjectAction(_m, objectId, type) { ExcludePoses = _excluded.ToList() };
            var d = await drive.RunAsync(cancel);
            _trace.AddRange(drive.Trace);
            if (drive.Chosen is { } tried && !_excluded.Any(p => DriveToObjectAction.IsSamePredockPose(p, tried.WorldPose)))
                _excluded.Add(tried.WorldPose);
            if (d != ActionResult.Success)
            {
                _trace.Add($"drive to pre-action pose: {d}");
                if (d is ActionResult.BadObject or ActionResult.NoPreActionPoses) return d;
                last = d;
                await SearchAsync(objectId, cancel);
                continue;
            }
            var action = makeAction();
            action.CheckPreActionPose = false;
            var r = await action.RunAsync(cancel);
            _trace.AddRange(action.Trace);
            if (r == ActionResult.Success) return r;
            last = r;
            if (r is ActionResult.BadObject or ActionResult.CancelledWhileRunning) return r;
            _trace.Add($"Failed dock attempt {Attempts} / {AttemptLimit}");
            await SearchAsync(objectId, cancel);
        }
        _trace.Add($"Failing helper because the action was already attempted {AttemptLimit} times");
        return last;
    }

    /// <summary>
    /// The search a failed stage delegates to. <c>DriveToHelper::RespondToDriveResult</c> at 0x005B5D68
    /// and <c>PickupBlockHelper::RespondToPickupResult</c> at 0x005B83B6 both build a
    /// <c>SearchForBlockHelper</c> when their stage has failed and the target is still in the world;
    /// 0x005B5D3E skips it when the lookup comes back empty.
    /// </summary>
    private async Task SearchAsync(uint objectId, CancellationToken cancel)
    {
        if (!SearchOnFailure || cancel.IsCancellationRequested) return;
        if (_m.World.GetLocatedObjectById(objectId) is null) return;
        var search = new SearchForBlockHelper(_m, objectId) { Wait = _m.Wait };
        await search.RunAsync(cancel);
        _trace.AddRange(search.Trace);
    }

    /// <summary>Whether a failed stage looks around for the target before trying again.</summary>
    public bool SearchOnFailure { get; init; } = true;
}
