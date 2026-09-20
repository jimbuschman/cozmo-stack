using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// <c>ReactionTriggerStrategyObjectPositionUpdated</c> (0x00611054) over its base
/// <c>ReactionTriggerStrategyPositionUpdate</c> (0x0061216E..0x00612A0C): the shipped map sends
/// <c>ObjectPositionUpdated</c> to the behaviour <c>AcknowledgeObject</c>. NATIVE, from the base constructor's
/// stores: an object is a desired target when its last observed pose is not the pose the robot last reacted to
/// within 80 mm (0x42A00000) and 45 degrees (0x3F490FDB) (<c>ShouldReactToTarget_poseHelper</c> →
/// <c>Pose3d::IsSameAs</c>), and the observation is no older than 600000 ms (0x927C0) against the last image
/// timestamp. <c>HandleObjectObserved</c> feeds it from <c>RobotObservedObject</c> (tag 0x44), which here is
/// <see cref="BlockWorld.ObjectObserved"/>; <c>ReactedToID</c> records the reacted pose. A 30.0 constant stored
/// beside them was not traced to a use (unlabelled). The strategy does not fire while AcknowledgeObject itself
/// is the running behaviour.
/// </summary>
public sealed class ObjectPositionUpdatedStrategy : IReactionTriggerStrategy, ITargetPreparingStrategy, IDisposable
{
    public const double SameDistanceMm = 80.0;
    public const double SameAngleRad = 0.785398;
    public const uint MaxObservationAgeMs = 600000;

    private sealed class ReactionData
    {
        public Pose3d? LastReactedPose;
        public Pose3d LastObservedPose;
        public uint ObservedTimestamp;
    }

    private readonly BlockWorld _world;
    private readonly AcknowledgeObjectBehavior _behavior;
    private readonly Dictionary<uint, ReactionData> _data = new();
    private readonly object _gate = new();
    private uint _lastImageTimestamp;

    public ObjectPositionUpdatedStrategy(BlockWorld world, AcknowledgeObjectBehavior behavior)
    {
        _world = world;
        _behavior = behavior;
        world.ObjectObserved += OnObserved;
        behavior.ReactedTo += ReactedToId;
    }

    public ReactionTrigger Trigger => ReactionTrigger.ObjectPositionUpdated;
    public string Basis => "ReactionTriggerStrategyPositionUpdate ctor 0x0061216E: IsSameAs(lastReacted, observed, 80 mm, 0.785398 rad) false && age <= 600000 ms; " +
                           "ReactionTriggerStrategyObjectPositionUpdated::HandleObjectObserved 0x006114A0";

    private void OnObserved(ObjectObservation o)
    {
        lock (_gate)
        {
            _lastImageTimestamp = o.Timestamp;
            if (!_data.TryGetValue(o.Object.ObjectId, out var d)) _data[o.Object.ObjectId] = d = new ReactionData();
            d.LastObservedPose = o.Object.Pose;
            d.ObservedTimestamp = o.Timestamp;
        }
    }

    /// <summary><c>ReactedToID</c>: the pose the robot has now acknowledged.</summary>
    public void ReactedToId(uint objectId)
    {
        lock (_gate)
            if (_data.TryGetValue(objectId, out var d)) d.LastReactedPose = d.LastObservedPose;
    }

    /// <summary><c>ShouldReactToTarget</c> for one object.</summary>
    public bool ShouldReactTo(uint objectId)
    {
        lock (_gate)
        {
            if (!_data.TryGetValue(objectId, out var d)) return false;
            if (_world.GetLocatedObjectById(objectId) is null) return false;
            if (unchecked(_lastImageTimestamp - d.ObservedTimestamp) > MaxObservationAgeMs) return false;
            return d.LastReactedPose is not { } reacted || !reacted.IsSameAs(d.LastObservedPose, SameDistanceMm, SameAngleRad);
        }
    }

    /// <summary><c>GetDesiredReactionTargets</c>.</summary>
    public IReadOnlyList<uint> DesiredTargets()
    {
        List<uint> ids;
        lock (_gate) ids = _data.Keys.ToList();
        return ids.Where(ShouldReactTo).OrderBy(i => i).ToList();
    }

    public bool ShouldTrigger(BehaviorContext context, ReactionTrigger? current, double nowSec)
    {
        if (!PrepareTarget(context, current, nowSec)) return false;
        CommitTarget();
        return true;
    }

    /// <summary>
    /// <c>GetDesiredReactionTargets</c> put on the behaviour, which is how the engine's
    /// <c>ShouldTriggerBehavior(robot, behavior)</c> hands them over. Nothing here is consumed: the strategy's
    /// reacted-to record only moves when the behaviour reports an acknowledgement.
    /// </summary>
    public bool PrepareTarget(BehaviorContext context, ReactionTrigger? current, double nowSec)
    {
        if (current == ReactionTrigger.ObjectPositionUpdated) return false;
        var targets = DesiredTargets();
        if (targets.Count == 0) return false;
        _stagedBefore = _behavior.PendingTargets;
        _behavior.SetTargets(targets);
        _staged = true;
        return true;
    }

    public void CommitTarget() { _staged = false; _stagedBefore = null; }

    public void AbandonTarget()
    {
        if (_staged && _stagedBefore is { } before) _behavior.ResetTargets(before);
        _staged = false; _stagedBefore = null;
    }

    private bool _staged;
    private IReadOnlyList<uint>? _stagedBefore;

    /// <summary><c>ClearDesiredTargets</c>: everything currently desired counts as reacted to.</summary>
    public void ClearDesiredTargets() { foreach (var id in DesiredTargets()) ReactedToId(id); }

    public void Dispose()
    {
        _world.ObjectObserved -= OnObserved;
        _behavior.ReactedTo -= ReactedToId;
    }
}

/// <summary>
/// <c>BehaviorAcknowledgeObject</c> (0x00602FA4..0x00604020), the behaviour the shipped map runs for
/// <c>ObjectPositionUpdated</c>. NATIVE, from the constructor and <c>LoadConfig</c>: max turn 45 degrees
/// (0x3F490FDB), pan and tilt tolerance 5 degrees (0x3DB2B8C2), <c>ReactionAnimGroup</c> AcknowledgeObject and
/// <c>NumImagesToWaitFor</c> 2 from <c>acknowledgeObject.json</c>. <c>BeginIteration</c> takes the next target,
/// asks the world model for it ("Object id %d is a target, but can't get it from blockworld" when unlocated),
/// runs <c>TurnTowardsObjectAction(robot, id, maxTurn)</c> with the tolerances and then a sequential
/// <c>VisuallyVerifyObjectAction(id, NumImagesToWaitFor)</c> and <c>TriggerLiftSafeAnimationAction(group)</c>;
/// <c>FinishIteration</c> reports the objective and moves to the next target. Its <c>LookForStackedCubes</c>
/// ghost-object search (looking up or down for a cube stacked on the target) is DEFERRED. INFERRED: a turn that
/// fails (target beyond 45 degrees) or a verification that times out ends the iteration without the animation;
/// the verification timeout is LOCAL (2 s).
/// </summary>
public sealed class AcknowledgeObjectBehavior : SteppedBehavior
{
    public const double MaxTurnAngleRad = 0.785398;
    public const double PanToleranceRad = 0.0872665;
    public const double TiltToleranceRad = 0.0872665;
    public const int NumImagesToWaitFor = 2;
    public const double VerifyTimeoutSec = 2.0;

    public enum Phase { Idle, Turning, Verifying, Reacting }

    private readonly BlockWorld _world;
    private readonly CubeLocator? _locator;
    private readonly Queue<uint> _targets = new();
    private uint? _current;
    private int _sightings;
    private CancellationTokenSource? _turnCancel;

    public AcknowledgeObjectBehavior(BlockWorld world, CubeLocator? locator = null) : base("AcknowledgeObject", "AcknowledgeObject")
    {
        _world = world;
        _locator = locator;
        world.ObjectObserved += OnObserved;
    }

    public Phase CurrentPhase { get; private set; }
    public uint? CurrentTarget => _current;
    public IReadOnlyList<uint> PendingTargets { get { lock (_targets) return _targets.ToList(); } }

    /// <summary>Raised when a target has been acknowledged (the strategy's <c>ReactedToID</c>).</summary>
    public event Action<uint>? ReactedTo;

    /// <summary>Replaces the pending queue (used to undo a staged trigger the behaviour could not take).</summary>
    public void ResetTargets(IEnumerable<uint> ids)
    {
        lock (_targets)
        {
            _targets.Clear();
            foreach (var id in ids) _targets.Enqueue(id);
        }
    }

    public void SetTargets(IEnumerable<uint> ids)
    {
        lock (_targets)
        {
            foreach (var id in ids) if (!_targets.Contains(id) && id != _current) _targets.Enqueue(id);
        }
    }

    protected override bool IsRunnableInternal(BehaviorContext context) { lock (_targets) return _targets.Count > 0 || _current is not null; }
    protected override bool KeepsRunningWithoutAction => CurrentPhase is Phase.Turning or Phase.Verifying;

    private void OnObserved(ObjectObservation o)
    {
        if (o.Object.ObjectId == _current) Interlocked.Increment(ref _sightings);
    }

    protected override void OnStart()
    {
        Scope.DisableReactions();
        BeginIteration();
    }

    private void BeginIteration()
    {
        uint id;
        lock (_targets)
        {
            if (_targets.Count == 0) { _current = null; CurrentPhase = Phase.Idle; Finish(); return; }
            id = _targets.Dequeue();
        }
        _current = id;
        var obj = _world.GetLocatedObjectById(id);
        if (obj is null)
        {
            Log($"BehaviorAcknowledgeObject.BeginIteration.NullObject: Object id {id} is a target, but can't get it from blockworld");
            ReactedTo?.Invoke(id);
            BeginIteration();
            return;
        }
        CurrentPhase = Phase.Turning;
        _sightings = 0;
        Log($"BeginIteration: turning towards object {id} (max {MaxTurnAngleRad * 180 / Math.PI:F0} deg)");
        if (_locator is null)
        {
            // no robot to turn (tests, replays): the turn is taken as done
            Post(() => TurnDone(true));
            return;
        }
        _turnCancel = new CancellationTokenSource();
        var pending = _locator.TurnTowardsAsync(id, MaxTurnAngleRad, _turnCancel.Token);
        pending.ContinueWith(t => Post(() => TurnDone(t.IsCompletedSuccessfully && t.Result)), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void TurnDone(bool ok)
    {
        if (CurrentPhase != Phase.Turning) return;
        if (!ok)
        {
            Log("TurnTowardsObjectAction failed (turn too large or robot state missing); iteration ends without the reaction (INFERRED)");
            FinishIteration();
            return;
        }
        CurrentPhase = Phase.Verifying;
        Log($"VisuallyVerifyObjectAction: waiting for {NumImagesToWaitFor} images of object {_current}");
        WaitUntil(() => Volatile.Read(ref _sightings) >= NumImagesToWaitFor, VerifyTimeoutSec, seen =>
        {
            if (!seen) { Log("object not verified in view; iteration ends without the reaction (INFERRED)"); FinishIteration(); return; }
            CurrentPhase = Phase.Reacting;
            PlayTrigger(AnimationTrigger.AcknowledgeObject, FinishIteration);
        }, "visual verification");
    }

    private void FinishIteration()
    {
        if (_current is { } id)
        {
            Log($"FinishIteration: objective achieved for object {id}");
            ReactedTo?.Invoke(id);
        }
        _current = null;
        BeginIteration();
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        _turnCancel?.Cancel();
        if (reason != BehaviorStopReason.Completed && _current is { } id) ReactedTo?.Invoke(id);
        _current = null;
        CurrentPhase = Phase.Idle;
        lock (_targets) _targets.Clear();
    }
}
