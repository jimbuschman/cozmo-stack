using Cozmo.Protocol;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// What the engine's <c>BlockWorld</c> knows about a cube that this stack does not yet: where it is. Every
/// step of the cube-moved reaction asks the world model first (<c>BlockWorld::GetLocatedObjectByIdHelper</c>),
/// and a cube it cannot locate is dropped from the reaction list. Cube pose comes from the vision pipeline,
/// which is the next milestone; until it exists nothing implements this and the reaction cannot fire, which is
/// exactly what the engine does before it has seen a cube.
/// </summary>
public interface ICubeLocator
{
    /// <summary>Whether the world model has a located pose for this object.</summary>
    bool IsLocated(uint objectId);

    /// <summary>Distance between the object's pose and the robot's, millimetres, or null when not located.</summary>
    float? DistanceFromRobotMm(uint objectId);

    /// <summary>
    /// The engine's <c>ObservableObject::IsVisibleFrom(camera, 0.785398 rad, ...)</c>: whether the camera should
    /// be seeing the object where the world model has it.
    /// </summary>
    bool IsVisibleFromCamera(uint objectId);

    /// <summary>The engine's <c>TurnTowardsPoseAction(robot, objectPose, maxTurn = π)</c>.</summary>
    Task<bool> TurnTowardsAsync(uint objectId, CancellationToken cancel);
}

/// <summary>
/// The engine's <c>ReactionObjectData</c> (0x0060BC60..0x0060C3C0): per tracked cube, when it started moving,
/// its up axis, and whether it has been observed since. Fed by the strategy from the cube messages and by the
/// world model when a cube is seen.
/// </summary>
public sealed class CubeMotionTracker
{
    /// <summary>One cube's record. Field names follow the byte offsets in the engine's struct.</summary>
    public sealed class Entry
    {
        public uint ObjectId { get; init; }
        /// <summary><c>+0x8</c>: the state timestamp of the ObjectMoved that started the current movement.</summary>
        public uint MovedTimestamp { get; internal set; }
        /// <summary><c>+0xC</c>: the up axis from the last movement report; 7 (UnknownAxis) until one arrives.</summary>
        public UpAxis UpAxis { get; internal set; } = UpAxis.UnknownAxis;
        /// <summary><c>+0xD</c>: between ObjectMoved and ObjectStoppedMoving.</summary>
        public bool Moving { get; internal set; }
        /// <summary><c>+0xE</c>: the object has been observed (located) since it was tracked.</summary>
        public bool Observed { get; internal set; }
        /// <summary><c>+0xF</c>: the up axis changed while moving.</summary>
        public bool UpAxisChanged { get; internal set; }
    }

    /// <summary>An object must have been moving this long before the reaction fires: 1000 ms (0x3E8 at 0x0060BE6C).</summary>
    public const uint MovedLongEnoughMs = 1000;
    /// <summary>An object closer than this to the robot is ignored: 50 mm (0x42480000 at 0x0060BE26).</summary>
    public const float IgnoreAreaMm = 50f;

    private readonly Dictionary<uint, Entry> _entries = new();
    private readonly object _gate = new();

    public IReadOnlyList<Entry> Entries { get { lock (_gate) return _entries.Values.ToList(); } }

    /// <summary><c>GetReactionaryIterator</c>: the record for an object, created on first sight.</summary>
    public Entry Track(uint objectId)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(objectId, out var e)) _entries[objectId] = e = new Entry { ObjectId = objectId };
            return e;
        }
    }

    public void Forget(uint objectId) { lock (_gate) _entries.Remove(objectId); }

    /// <summary><c>ObjectStartedMoving</c> (0x0060C336).</summary>
    public void ObjectMoved(ObjectMoved m, bool located)
    {
        var e = Track(m.ObjectID);
        lock (_gate)
        {
            if (!located) { e.Moving = false; return; }
            if (e.Moving)
            {
                if (m.AxisOfAccel != e.UpAxis) e.UpAxisChanged = true;
                e.UpAxis = m.AxisOfAccel;
                return;
            }
            e.Moving = true;
            e.MovedTimestamp = m.Timestamp;
            e.UpAxis = m.AxisOfAccel;
        }
    }

    /// <summary><c>ObjectStoppedMoving</c> (0x0060C37E).</summary>
    public void ObjectStopped(uint objectId) { var e = Track(objectId); lock (_gate) e.Moving = false; }

    /// <summary>The strategy's <c>HandleObjectUpAxisChanged</c> (0x0060C008): a located object's axis changed.</summary>
    public void ObjectUpAxisChanged(uint objectId, bool located)
    {
        var e = Track(objectId);
        lock (_gate) if (located) e.UpAxisChanged = true;
    }

    /// <summary><c>ObjectObserved</c> (0x0060C314): seeing a located object clears its movement and marks it observed.</summary>
    public void ObjectObserved(uint objectId, bool located)
    {
        var e = Track(objectId);
        lock (_gate)
        {
            if (!located) return;
            e.UpAxisChanged = false; e.MovedTimestamp = 0; e.Moving = false; e.Observed = true;
        }
    }

    /// <summary><c>ResetObject</c> (0x0060BC74).</summary>
    public void Reset(uint objectId)
    {
        var e = Track(objectId);
        lock (_gate) { e.UpAxisChanged = false; e.Moving = false; e.Observed = false; e.MovedTimestamp = 0; }
    }

    /// <summary><c>ObjectHasMovedLongEnough</c> (0x0060BE48): located, moving, observed, and moving for more than 1000 ms of robot time.</summary>
    public bool HasMovedLongEnough(Entry e, bool located, uint robotTimestamp)
    {
        lock (_gate)
            return located && e.Moving && e.Observed && e.MovedTimestamp != 0
                   && unchecked(robotTimestamp - e.MovedTimestamp) > MovedLongEnoughMs;
    }

    /// <summary><c>ObjectUpAxisHasChanged</c> (0x0060BE82): located, axis changed, observed.</summary>
    public bool UpAxisHasChanged(Entry e, bool located) { lock (_gate) return located && e.UpAxisChanged && e.Observed; }

    /// <summary><c>ObjectOutsideIgnoreArea</c> (0x0060BDF8): located and more than 50 mm from the robot.</summary>
    public static bool OutsideIgnoreArea(float? distanceMm) => distanceMm is { } d && d > IgnoreAreaMm;
}

/// <summary>
/// <c>ReactionTriggerStrategyCubeMoved</c> (0x0060B810..0x0060C280). <c>AlwaysHandleInternal</c> feeds
/// <see cref="CubeMotionTracker"/> from <c>ObjectMoved</c> (tag 0xF), <c>ObjectStoppedMoving</c> (0x10),
/// <c>ObjectUpAxisChanged</c> (0x11) and <c>RobotObservedObject</c> (0x44), while the trigger is enabled.
/// <c>ShouldTriggerBehaviorInternal</c> (0x0060BC80) walks the tracked objects: one the world model cannot
/// locate is erased; one outside the 50 mm ignore area that has moved for over a second (or changed its up axis)
/// and is <b>not</b> where the camera can see it fires the reaction, after resetting its record and handing its
/// id to the behaviour. Every test needs the located pose, so without a locator the strategy never fires.
/// </summary>
public sealed class CubeMovedReactionStrategy : IReactionTriggerStrategy, ITargetPreparingStrategy, IDisposable
{
    private readonly CozmoRobot _robot;
    private readonly ICubeLocator? _locator;
    private readonly AcknowledgeCubeMovedBehavior _behavior;
    private readonly Cozmo.Robot.Vision.BlockWorld? _world;
    private uint? _staged, _targetBeforeStaging;

    /// <param name="world">
    /// The world model whose <c>ObjectObserved</c> is the engine's <c>RobotObservedObject</c> (tag 0x44) for
    /// this strategy's <c>AlwaysHandleInternal</c>. Without it the sighting path has to be driven by hand.
    /// </param>
    public CubeMovedReactionStrategy(CozmoRobot robot, AcknowledgeCubeMovedBehavior behavior, ICubeLocator? locator,
                                     Cozmo.Robot.Vision.BlockWorld? world = null)
    {
        _robot = robot;
        _behavior = behavior;
        _locator = locator;
        _world = world;
        robot.Message += OnMessage;
        if (_world is not null) _world.ObjectObserved += OnWorldObserved;
    }

    private void OnWorldObserved(Cozmo.Robot.Vision.ObjectObservation o) => ObjectObserved(o.Object.ObjectId);

    public CubeMotionTracker Tracker { get; } = new();
    public ReactionTrigger Trigger => ReactionTrigger.CubeMoved;
    public string Basis => "ReactionTriggerStrategyCubeMoved::ShouldTriggerBehaviorInternal 0x0060BC80 over ReactionObjectData 0x0060BC60..0x0060C3C0; " +
                           "located (BlockWorld) && distance > 50 mm && (moved > 1000 ms || up axis changed) && !IsVisibleFrom(camera, 0.785398)";

    /// <summary>Whether a world model is attached. Without one the reaction is unreachable, as in the engine before it sees a cube.</summary>
    public bool HasLocator => _locator is not null;

    private bool Located(uint id) => _locator?.IsLocated(id) ?? false;

    private void OnMessage(RobotMessage m)
    {
        switch (m)
        {
            case ObjectMoved mv: Tracker.ObjectMoved(mv, Located(mv.ObjectID)); break;
            case ObjectStoppedMoving sm: Tracker.ObjectStopped(sm.ObjectID); break;
            case ObjectUpAxisChanged ua: Tracker.ObjectUpAxisChanged(ua.ObjectID, Located(ua.ObjectID)); break;
        }
    }

    /// <summary>The world model reports a sighting: the engine's <c>RobotObservedObject</c> path.</summary>
    public void ObjectObserved(uint objectId)
    {
        Tracker.ObjectObserved(objectId, Located(objectId));
        _behavior.ObjectObserved(objectId);
    }

    public bool ShouldTrigger(BehaviorContext context, ReactionTrigger? current, double nowSec)
    {
        if (!PrepareTarget(context, current, nowSec)) return false;
        CommitTarget();
        return true;
    }

    /// <summary>
    /// <c>ShouldTriggerBehaviorInternal</c> (0x0060BC80) without the <c>ResetObject</c> at the end: the
    /// candidate's id is put on the behaviour (the engine passes the behaviour in) but the tracker entry is
    /// left alone until the manager has seen that the behaviour can run.
    /// </summary>
    public bool PrepareTarget(BehaviorContext context, ReactionTrigger? current, double nowSec)
    {
        if (_locator is null) return false;
        uint robotTimestamp = _robot.State.Latest?.Timestamp ?? 0;
        foreach (var e in Tracker.Entries)
        {
            bool located = _locator.IsLocated(e.ObjectId);
            if (!located) { Tracker.Forget(e.ObjectId); continue; }
            if (!CubeMotionTracker.OutsideIgnoreArea(_locator.DistanceFromRobotMm(e.ObjectId))) continue;
            if (!Tracker.HasMovedLongEnough(e, located, robotTimestamp) && !Tracker.UpAxisHasChanged(e, located)) continue;
            if (_locator.IsVisibleFromCamera(e.ObjectId)) continue;
            _targetBeforeStaging = _behavior.TargetObjectId;
            _staged = e.ObjectId;
            _behavior.TargetObjectId = e.ObjectId;
            return true;
        }
        return false;
    }

    /// <summary>The <c>ResetObject</c> the engine does once the reaction is taken.</summary>
    public void CommitTarget()
    {
        if (_staged is { } id) Tracker.Reset(id);
        _staged = null; _targetBeforeStaging = null;
    }

    public void AbandonTarget()
    {
        if (_staged is not null) _behavior.TargetObjectId = _targetBeforeStaging;
        _staged = null; _targetBeforeStaging = null;
    }

    public void Dispose()
    {
        _robot.Message -= OnMessage;
        if (_world is not null) _world.ObjectObserved -= OnWorldObserved;
    }
}

/// <summary>
/// <c>BehaviorAcknowledgeCubeMoved</c> (0x0060219C..0x006027F0), the class the shipped map runs for
/// <c>ReactToCubeMoved</c>. Runnable while it has a target object (<c>+0x124 != -1</c>). <c>InitInternal</c>
/// takes the reaction lock and (unless resuming mid-turn) plays <see cref="AnimationTrigger.CubeMovedSense"/>
/// (0x72) in parallel with a 0.5 s wait; then <c>TransitionToTurningToLastLocationOfBlock</c> runs a
/// <c>TurnTowardsPoseAction</c> to the cube's last known pose (max turn π) in parallel with a 0.5 s wait, and
/// <c>UpdateInternal</c> watches for a <c>RobotObservedObject</c> of the target: seen → <c>StopActing</c>,
/// <see cref="AnimationTrigger.AcknowledgeObject"/> (3), state <c>ReactingToBlockPresence</c>; the turn finishing
/// without a sighting → <see cref="AnimationTrigger.CubeMovedUpset"/> (0x73), objective achieved.
/// If the pose is no longer valid the engine logs <c>"The robot's context has changed and the block's location is
/// no longer valid"</c>; what it does next was not read, and going straight to the upset reaction is INFERRED.
/// </summary>
public sealed class AcknowledgeCubeMovedBehavior : SteppedBehavior
{
    public enum Phase { Idle, TurningToLastLocation, PlayingSenseReaction, ReactingToBlockPresence, ReactingToBlockAbsence }

    private readonly ICubeLocator? _locator;
    private bool _turnDone, _waitDone;
    private volatile bool _observed;
    private CancellationTokenSource? _turnCancel;

    public AcknowledgeCubeMovedBehavior(ICubeLocator? locator = null) : base("ReactToCubeMoved", "ReactToCubeMoved") =>
        _locator = locator;

    /// <summary>The cube to react to (<c>+0x124</c>), set by the strategy; null is the engine's −1.</summary>
    public uint? TargetObjectId { get; set; }
    public Phase CurrentPhase { get; private set; }

    protected override bool IsRunnableInternal(BehaviorContext context) => TargetObjectId is not null;
    protected override bool KeepsRunningWithoutAction => CurrentPhase == Phase.TurningToLastLocation;

    /// <summary>The engine's <c>HandleObservedObject</c>: a sighting of the target while running.</summary>
    public void ObjectObserved(uint objectId)
    {
        if (TargetObjectId == objectId) _observed = true;
    }

    protected override void OnStart()
    {
        Scope.DisableReactions();
        _observed = false;
        if (CurrentPhase == Phase.TurningToLastLocation) TransitionToTurningToLastLocationOfBlock();
        else TransitionToPlayingSenseReaction();
    }

    private void TransitionToPlayingSenseReaction()
    {
        CurrentPhase = Phase.PlayingSenseReaction;
        Log("PlayingSenseReaction");
        _waitDone = false; _turnDone = false;
        Wait(0.5, () => { _waitDone = true; if (_turnDone) TransitionToTurningToLastLocationOfBlock(); });
        PlayTrigger(AnimationTrigger.CubeMovedSense, () => { _turnDone = true; if (_waitDone) TransitionToTurningToLastLocationOfBlock(); });
    }

    private void TransitionToTurningToLastLocationOfBlock()
    {
        CurrentPhase = Phase.TurningToLastLocation;
        Log("TurningToLastLocationOfBlock");
        _waitDone = false; _turnDone = false;
        if (TargetObjectId is not { } id || _locator is null || !_locator.IsLocated(id))
        {
            Log($"the block's location is no longer valid (ObjectID={TargetObjectId?.ToString() ?? "-1"}); treating the block as absent (INFERRED)");
            TransitionToReactingToBlockAbsence();
            return;
        }
        Wait(0.5, () => { _waitDone = true; if (_turnDone) TransitionToReactingToBlockAbsence(); });
        _turnCancel = new CancellationTokenSource();
        var pending = _locator.TurnTowardsAsync(id, _turnCancel.Token);
        pending.ContinueWith(t =>
        {
            Post(() =>
            {
                if (CurrentPhase != Phase.TurningToLastLocation) return;
                _turnDone = true;
                Log(t.IsCompletedSuccessfully && t.Result ? "turned towards the last location" : "the turn did not complete");
                if (_waitDone) TransitionToReactingToBlockAbsence();
            });
        }, TaskScheduler.Default);
    }

    protected override void OnUpdate()
    {
        if (CurrentPhase == Phase.TurningToLastLocation && _observed)
        {
            StopActing();
            _turnCancel?.Cancel();
            CurrentPhase = Phase.ReactingToBlockPresence;
            Log("ReactingToBlockPresence: the cube was seen where expected");
            PlayTrigger(AnimationTrigger.AcknowledgeObject, () => { CurrentPhase = Phase.Idle; Finish(); });
        }
    }

    private void TransitionToReactingToBlockAbsence()
    {
        if (CurrentPhase == Phase.ReactingToBlockPresence) return;
        CurrentPhase = Phase.ReactingToBlockAbsence;
        Log("ReactingToBlockAbsence");
        PlayTrigger(AnimationTrigger.CubeMovedUpset, () =>
        {
            Log("objective achieved (cube moved and not found)");
            CurrentPhase = Phase.Idle;
            Finish();
        });
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        _turnCancel?.Cancel();
        if (reason != BehaviorStopReason.Completed && CurrentPhase != Phase.TurningToLastLocation) CurrentPhase = Phase.Idle;
        if (reason == BehaviorStopReason.Completed) TargetObjectId = null;
    }
}
