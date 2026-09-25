using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// fidelity: M10-003
/// <summary>
/// <c>ReactionTriggerStrategyObjectPositionUpdated</c> over <c>ReactionTriggerStrategyPositionUpdate</c> (gap2 4a..4l,
/// gap1 8); flags (1, 1, 0).
/// <list type="bullet">
/// <item>Base (4a): angle tolerance 0.785398, time threshold 600 000 ms, distance tolerance 80 mm, trigger 8; per-target
/// record {observed pose, reacted pose, observed time, reacted time} (4b).</item>
/// <item>Recording (4c) with the flag IsReactionTriggerEnabled(8): an existing target takes the observed pose and time, and
/// with the flag false also the reacted ones; a new one is added with reacted time 0, or with the observation as reacted
/// when the flag is false.</item>
/// <item>poseHelper (4d): moved more than 80 mm or 45°; ShouldReactToTarget(anyMode false) (4e): reacted time 0, or
/// poseHelper, or lastImageTimeStamp − reacted time &gt; 600 000 (u32). Has/GetDesiredReactionTargets (4f).</item>
/// <item>Event handler (4j): tag 0x44 only if a current behaviour exists and its class ≠ +0x54 (the bound behaviour's
/// class, 4i); with no current behaviour observations are dropped. The observed handler (4k): only the families
/// {LightCube, Block}; the carried object or the docking object records with the flag forced false.</item>
/// <item>STBI (0x6113E8..0x611470): false if carrying, picking or placing, or on the charger platform; false without a
/// desired target; else AcknowledgeObject+0x14C = the targets, and IsRunnable.</item>
/// <item>RobotReactedToId (4h): reacted pose = observed, reacted time = Robot::GetLastImageTimeStamp.
/// ClearDesiredTargets (4l): RobotReactedToId for each desired target. RobotDelocalized's ResetReactionData changes
/// nothing (3c); GetBestTarget is on no path (4g). EnabledStateChanged is a no-op (0x60B73A).</item>
/// </list>
/// Seams (M11/M12): the carried and docking object ids, Robot::GetLastImageTimeStamp, the manager's current behaviour.
/// MISSING (4d): the frame the {80, 80, 80} per-axis test is made in (GetWithRespectTo then IsSameAs) is not in the rows;
/// the world-frame <see cref="Pose3d.IsSameAs"/> is used.
/// </summary>
public sealed class ObjectPositionUpdatedStrategy : ReactionTriggerStrategy, IDisposable
{
    public const double SameDistanceMm = 80.0;
    public const double SameAngleRad = 0.785398;
    public const uint TimeThresholdMs = 600000;

    private sealed class Record
    {
        public Pose3d ObservedPose, ReactedPose;
        public uint ObservedTime, ReactedTime;
    }

    private readonly CozmoRobot? _robot;
    private readonly BlockWorld _world;
    private readonly AcknowledgeObjectBehavior _behavior;
    private readonly Dictionary<uint, Record> _records = new();
    private readonly object _gate = new();

    public ObjectPositionUpdatedStrategy(BlockWorld world, AcknowledgeObjectBehavior behavior, CozmoRobot? robot = null)
    {
        _world = world;
        _behavior = behavior;
        _robot = robot;
        world.ObjectObserved += OnObserved;
        behavior.ReactedTo += RobotReactedToId;
    }

    public override ReactionTrigger Trigger => ReactionTrigger.ObjectPositionUpdated;
    public override string Basis => "ReactionTriggerStrategyPositionUpdate ctor 0x612168..0x6121D4 (80 mm, 45 deg, 600000 ms); " +
                                    "ObjectPositionUpdated STBI 0x6113E8..0x611470, handlers 0x6114A0..0x611582";
    public override bool ShouldResumeLast => true;
    public override bool CanInterruptOtherTriggeredBehavior => true;
    public override bool CanInterruptSelf => false;

    /// <summary>[robot+0x284]+4: the carried object (M12). Null: nothing is carried.</summary>
    public Func<uint?>? CarriedObjectId { get; set; }
    /// <summary>[robot+0x280]+8: the docking component's ObjectID (M12). Null: none.</summary>
    public Func<uint?>? DockingObjectId { get; set; }
    /// <summary>Robot::GetLastImageTimeStamp (M11).</summary>
    public Func<uint>? LastImageTimestamp { get; set; }

    private uint LastImageTs() => LastImageTimestamp?.Invoke() ?? 0;

    /// <summary>The event handler (4j) and the object-observed handler (4k).</summary>
    private void OnObserved(ObjectObservation o)
    {
        var current = Manager?.Current;
        if (current is null || current.Class == _behavior.Class) return;                          // 4j
        var obj = o.Object;
        if (obj.Family is not (ObjectFamily.LightCube or ObjectFamily.Block)) return;             // 4k
        uint id = obj.ObjectId;
        bool forcedReacted = id == CarriedObjectId?.Invoke() || id == DockingObjectId?.Invoke();
        bool enabled = !forcedReacted && IsReactionTriggerEnabled(ReactionTrigger.ObjectPositionUpdated);
        RecordObservation(id, obj.Pose, o.Timestamp, enabled);
    }

    /// <summary>4c: recording an observation with the enabled flag.</summary>
    public void RecordObservation(uint id, Pose3d pose, uint time, bool enabled)
    {
        lock (_gate)
        {
            if (_records.TryGetValue(id, out var r))
            {
                r.ObservedPose = pose; r.ObservedTime = time;
                if (!enabled) { r.ReactedPose = pose; r.ReactedTime = time; }
                return;
            }
            _records[id] = new Record
            {
                ObservedPose = pose, ObservedTime = time,
                ReactedPose = enabled ? default : pose, ReactedTime = enabled ? 0 : time,
            };
        }
    }

    /// <summary>4h: RobotReactedToId.</summary>
    public void RobotReactedToId(uint id)
    {
        lock (_gate)
        {
            if (!_records.TryGetValue(id, out var r))
            {
                _robot?.Engine.Log($"debug: ReactionTriggerStrategyPositionUpdate.RobotReactedToId: no record for {id}");
                return;
            }
            r.ReactedPose = r.ObservedPose;
            r.ReactedTime = LastImageTs();
        }
    }

    /// <summary>4d: moved more than 80 mm or 45°.</summary>
    private static bool PoseHelper(Pose3d observed, Pose3d reacted) => !reacted.IsSameAs(observed, SameDistanceMm, SameAngleRad);

    /// <summary>4e with anyMode false.</summary>
    public bool ShouldReactToTarget(uint id)
    {
        lock (_gate)
        {
            if (!_records.TryGetValue(id, out var r)) return false;
            if (r.ReactedTime == 0) return true;
            return PoseHelper(r.ObservedPose, r.ReactedPose) || unchecked(LastImageTs() - r.ReactedTime) > TimeThresholdMs;
        }
    }

    /// <summary>4f: GetDesiredReactionTargets.</summary>
    public IReadOnlyList<uint> DesiredTargets()
    {
        List<uint> ids;
        lock (_gate) ids = _records.Keys.ToList();
        return ids.Where(ShouldReactToTarget).ToList();
    }

    protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior)
    {
        if (CarriedObjectId?.Invoke() is not null) return false;
        if (rc.Context.Robot.State.Latest?.Has(RobotStatusFlag.IsPickingOrPlacing) == true) return false;
        if (rc.Context.Robot.Sensors.OnChargerPlatform) return false;
        var targets = DesiredTargets();
        if (targets.Count == 0) return false;
        _behavior.ResetTargets(targets);                                                          // +0x14C = the targets
        return behavior.IsRunnable(rc.Context);
    }

    /// <summary>4l: ClearDesiredTargets.</summary>
    public void ClearDesiredTargets() { foreach (var id in DesiredTargets()) RobotReactedToId(id); }

    public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }

    public void Dispose()
    {
        _world.ObjectObserved -= OnObserved;
        _behavior.ReactedTo -= RobotReactedToId;
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
