using Cozmo.Protocol;
using Cozmo.Robot.Vision;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Animation;
using System.Text.Json;

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
    private readonly IReactToObjectListener _listener;
    private readonly Dictionary<uint, Record> _records = new();
    private readonly object _gate = new();

    public ObjectPositionUpdatedStrategy(BlockWorld world, AcknowledgeObjectBehavior behavior, CozmoRobot? robot = null)
    {
        _world = world;
        _behavior = behavior;
        _robot = robot;
        world.ObjectObserved += OnObserved;
        _listener = new Listener(this);
        behavior.AddListener(_listener); // T4/T5: strategy listener subobject, 0x00611674..90
    }

    private sealed class Listener(ObjectPositionUpdatedStrategy owner) : IReactToObjectListener
    {
        public void RobotReactedToId(uint id) => owner.RobotReactedToId(id);
        public void ClearDesiredTargets() => owner.ClearDesiredTargets();
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
                _robot?.Engine.EmitSourceLog(EngineLogLevel.Debug, null, "ReactionTriggerStrategyPositionUpdate.RobotReactedToId", $"no record for {id}");
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
        _behavior.RemoveListener(_listener);
    }
}

/// <summary>The two listener slots called by AcknowledgeObject (T5/S1).</summary>
internal interface IReactToObjectListener
{
    void RobotReactedToId(uint id);
    void ClearDesiredTargets();
}

/// <summary>
/// <c>BehaviorAcknowledgeObject</c> (0x00602FA4..0x00604020). The constructor defaults, shipped configuration,
/// target set, action sequence and objective callback follow ACKNOWLEDGE-OBJECT-1 C2..F2. M11-010 visibility and
/// M12-033 ghost marker geometry remain explicit missing inputs; supplied helper delegates are unit-test boundaries.
/// </summary>
public sealed class AcknowledgeObjectBehavior : SteppedBehavior
{
    public const double MaxTurnAngleRad = 0.785398;
    public const double PanToleranceRad = 0.0872665;
    public const double TiltToleranceRad = 0.0872665;
    [Obsolete("Host compatibility constant; native verification uses the IAction timeout, not this value.")]
    public const double VerifyTimeoutSec = 2.0;
    public const int NumImagesToWaitFor = 2;

    public enum Phase { Idle, Turning, Verifying, Reacting }

    private readonly BlockWorld _world;
    private readonly SortedSet<int> _targets = new(); // native std::set<int>, +0x14C
    private readonly List<IReactToObjectListener> _listeners = new();
    private uint? _current;
    private uint _ghostId = ObservableObject.UnassignedId;
    private bool _ghostIdAllocated;
    // C3/P1: this retained ghost stays outside BlockWorld. Marker construction is the separate M12-033 frontier.
    private readonly Pose3d _ghostPose = new("");
    private readonly object _ghostLifetime = new();
    private int _ghostUsers;
    private int _ghostRetired;
    private long _hostGeneration;
    internal ObjectType GhostType => ObjectType.Block_LIGHTCUBE_GHOST;
    internal PoseState GhostPoseState { get; private set; } = PoseState.Dirty;
    private bool _beginPending;
    private ManipulationSystem? _nativeManipulation;
    private BehaviorManager? _configuredManager;
    private readonly HashSet<string> _configDiagnostics = new();
    internal float ConfigMaxTurn { get; private set; } = BitConverter.UInt32BitsToSingle(0x3F490FDB);
    internal float ConfigPanTolerance { get; private set; } = BitConverter.UInt32BitsToSingle(0x3DB2B8C2);
    internal float ConfigTiltTolerance { get; private set; } = BitConverter.UInt32BitsToSingle(0x3DB2B8C2);
    internal int ConfigImages { get; private set; } = 2;
    internal AnimationTrigger ConfigReaction { get; private set; } = (AnimationTrigger)0x23F;
    internal bool ConfigAlwaysStreamline { get; private set; }
    private bool _highTarget = true; // C2: +0x149 = 1; retained across Stop
    private bool _hardSpark;
    private bool _lookedUp;
    private NativeVerifyObjectRunner? _displayVerify;
    private TriggerLiftSafeAnimationAction? _displayAnimation;
    // Unit-only supplied inputs for the unresolved shared helpers; production never supplies these delegates.
    internal Func<Pose3d, int>? SuppliedVisibilityReason;
    internal Func<Pose3d, (bool Success, Pose3d? Pose)>? SuppliedClosestMarker;
    internal Func<Pose3d, (int Result, float Angle)>? SuppliedHeadAngle;
    internal Action<bool, Action>? SuppliedGhostLook;

    public AcknowledgeObjectBehavior(BlockWorld world, CubeLocator? locator = null) : base("AcknowledgeObject", "AcknowledgeObject")
    {
        _world = world;
        _ = locator; // retained host constructor boundary; native turns use ActionList.
    }

    // C4/C5: config is attached to the early reaction owner, never a parallel behavior instance.
    internal void BindConfiguredInputs(ManipulationSystem? manipulation, JsonElement json, BehaviorManager? manager = null)
    {
        var reaction = ConfigReaction;
        int images = ConfigImages;
        float maxTurn = ConfigMaxTurn, panTolerance = ConfigPanTolerance, tiltTolerance = ConfigTiltTolerance;
        bool always = ConfigAlwaysStreamline;
        if (json.TryGetProperty("ReactionAnimGroup", out var group) && group.ValueKind == JsonValueKind.String &&
            Enum.TryParse<AnimationTrigger>(group.GetString(), out var trigger)) reaction = trigger;
        if (json.TryGetProperty("NumImagesToWaitFor", out var frames) && frames.ValueKind != JsonValueKind.Null) images = frames.GetInt32();
        if (ReadAngle(json, "MaxTurnAngle_deg", out float max)) maxTurn = max;
        if (ReadAngle(json, "PanTolerance_deg", out float pan)) panTolerance = pan;
        if (ReadAngle(json, "TiltTolerance_deg", out float tilt)) tiltTolerance = tilt;
        if (json.TryGetProperty("alwaysStreamline", out var streamline)) always = streamline.GetBoolean();
        _nativeManipulation = manipulation;
        _configuredManager = manager;
        ConfigReaction = reaction; ConfigImages = images; ConfigMaxTurn = maxTurn;
        ConfigPanTolerance = panTolerance; ConfigTiltTolerance = tiltTolerance; ConfigAlwaysStreamline = always;
    }
    private bool ReadAngle(JsonElement json, string key, out float angle)
    {
        angle = 0;
        if (!json.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) return false;
        ReportMissing("M7-018 ACKNOWLEDGE-OBJECT-1 C4: supplied angle uses host JsonElement parsing; native lexical rounding, locale and integer-token classification remain UNKNOWN", _configDiagnostics);
        float input = value.ValueKind switch
        {
            JsonValueKind.Number => NumberAsFloat(value),
            JsonValueKind.True => 1f,
            JsonValueKind.False => 0f,
            _ => throw new NotSupportedException("M1-034/M7-018 GetAngleOptional JsonCpp asFloat throw at 0x008E9D00 for " + key),
        };
        float radians = input * BitConverter.UInt32BitsToSingle(0x3C8EFA35);
        if (!float.IsFinite(radians))
            throw new NotSupportedException("MISSING ACKNOWLEDGE-OBJECT-1 C4 nonfinite angle conversion for " + key);
        if (MathF.Abs(radians) >= 10f)
        {
            float count = MathF.Ceiling(radians / Radians.TwoPi - 0.5f);
            if (count >= 2147483648f || count < -2147483648f)
                throw new NotSupportedException("MISSING ACKNOWLEDGE-OBJECT-1 C4 out-of-range signed angle wrap for " + key);
        }
        angle = new Radians(radians).Value; // Q3-5..9: f32 multiply, then Radians normalization
        return true;
    }
    private static float NumberAsFloat(JsonElement value)
    {
        if (value.TryGetInt64(out long signed)) return (float)signed;
        if (value.TryGetUInt64(out ulong unsigned)) return (float)unsigned;
        if (!value.TryGetDouble(out double real))
            throw new NotSupportedException("MISSING ACKNOWLEDGE-OBJECT-1 C4 host numeric parser range");
        return (float)real; // Json real: f64 -> f32 before multiplication
    }

    public Phase CurrentPhase { get; private set; }
    public uint? CurrentTarget => _current;
    public IReadOnlyList<uint> PendingTargets { get { lock (_targets) return _targets.Select(x => unchecked((uint)x)).ToList(); } }
    internal uint GhostId => _ghostId;
    // Host lifetime cleanup after the manager has stopped the behavior and unsubscribed its strategy.
    internal long HostGeneration => Interlocked.Read(ref _hostGeneration);
    internal void ReleaseHostGhost()
    {
        lock (_ghostLifetime)
        {
            if (_ghostRetired != 0) return;
            Volatile.Write(ref _ghostRetired, 1);
            Interlocked.Increment(ref _hostGeneration);
            if (_ghostUsers == 0) _ghostPose.Dispose();
        }
    }
    private bool EnterGhostUse()
    { lock (_ghostLifetime) { if (_ghostRetired != 0) return false; _ghostUsers++; return true; } }
    private void ExitGhostUse()
    { lock (_ghostLifetime) { _ghostUsers--; if (_ghostRetired != 0 && _ghostUsers == 0) _ghostPose.Dispose(); } }
    private bool IsGeneration(long generation) => Volatile.Read(ref _ghostRetired) == 0 && EngineRunning && HostGeneration == generation;
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) =>
        (context?.Robot.Engine ?? _nativeManipulation?.Robot.Engine)?.EnterSynchronousOwner();
    protected override long CaptureHostExecutionVersion() => HostGeneration;
    protected override void HostRunStarting() => Interlocked.Increment(ref _hostGeneration);
    protected override bool HostExecutionVersionCurrent(long version) => HostGeneration == version && Volatile.Read(ref _ghostRetired) == 0;
    protected override bool HostStopContinuationCurrent(long version) => HostGeneration == unchecked(version + 1) && Volatile.Read(ref _ghostRetired) == 0;
    protected override bool HostResumeContinuationCurrent(long version) =>
        Volatile.Read(ref _ghostRetired) == 0 && (HostGeneration == version || HostGeneration == unchecked(version + 1));

    /// <summary>Raised when a target has been acknowledged (the strategy's <c>ReactedToID</c>).</summary>
    public event Action<uint>? ReactedTo;
    internal void AddListener(IReactToObjectListener listener) { lock (_targets) if (!_listeners.Contains(listener)) _listeners.Add(listener); }
    internal void RemoveListener(IReactToObjectListener listener) { lock (_targets) _listeners.Remove(listener); }
    private IReactToObjectListener[] Listeners() { lock (_targets) return _listeners.ToArray(); }

    /// <summary>Replaces the pending queue (used to undo a staged trigger the behaviour could not take).</summary>
    public void ResetTargets(IEnumerable<uint> ids)
    {
        lock (_targets)
        {
            _targets.Clear();
            foreach (var id in ids) _targets.Add(unchecked((int)id));
        }
    }

    public void SetTargets(IEnumerable<uint> ids)
    {
        lock (_targets)
        {
            foreach (var id in ids) if (id != _current) _targets.Add(unchecked((int)id));
        }
    }

    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => true;
    public override bool IsRunnable(BehaviorContext context) => IsRunnableBase(context) && IsRunnableInternal(context);
    protected override bool IsRunnableInternal(BehaviorContext context) { lock (_targets) return _targets.Count != 0; }
    protected override bool KeepsRunningWithoutAction => _beginPending || HasCurrentAction;

    protected override void OnStart()
    {
        if (!_ghostIdAllocated) { _ghostId = ObjectIdSpace.Set(); _ghostIdAllocated = true; } // operator N2: once, process-wide
        _beginPending = true;
        var manager = _configuredManager ?? Context.Robot.Engine.Robot?.BehaviorManagerOwner;
        if (manager is null)
        {
            _beginPending = false;
            ReportMissing("ACKNOWLEDGE-OBJECT-1 B7: actual BehaviorManager spark owner unavailable");
            return;
        }
        _hardSpark = manager.ActiveSparkId != 0x55 && (manager.ActiveSparkSoft ^ 1) != 0;
    }

    protected override void OnUpdate()
    {
        long generation = HostGeneration;
        if (_beginPending)
        {
            _beginPending = false;
            BeginIteration(generation); // U1: first update, not Init
        }
        if (!IsGeneration(generation)) return;
        // Existing host phase display follows the actual child runners; it is not a native state claim.
        if (_displayAnimation is { State: not EngineActionResult.NotStarted }) CurrentPhase = Phase.Reacting;
        else if (_displayVerify is { State: not EngineActionResult.NotStarted }) CurrentPhase = Phase.Verifying;
    }

    private void BeginIteration(long generation)
    {
        if (!IsGeneration(generation)) return;
        uint id;
        lock (_targets)
        {
            if (_targets.Count == 0) { _current = null; CurrentPhase = Phase.Idle; return; }
            id = unchecked((uint)_targets.Min);
        }
        _current = id;
        var obj = _world.GetLocatedObjectById(id);
        if (obj is null)
        {
            Log($"BehaviorAcknowledgeObject.BeginIteration.NullObject: Object id {id} is a target, but can't get it from blockworld");
            if (!IsGeneration(generation)) return;
            lock (_targets) _targets.Remove(unchecked((int)id));
            _current = null;
            BeginIteration(generation);
            return;
        }
        var m = _nativeManipulation;
        var robot = Context.Robot.Engine.Robot;
        if (m is null || robot is null)
        {
            _beginPending = false;
            ReportMissing("ACKNOWLEDGE-OBJECT-1 B3..B8: actual manipulation/robot owner unavailable");
            return;
        }
        using (var relative = new Pose3d(""))
        {
            obj.NativePose.TryWithRespectTo(robot.GetCheckedMainPose(), relative); // B3 ignores the result
            _highTarget = (float)relative.Translation.Z > BitConverter.UInt32BitsToSingle(0x42076666);
        }
        CurrentPhase = Phase.Turning;
        var sequence = new CompoundActionSequential(() => m.Robot.Engine.Timer.SecondsF);
        sequence.SeedNativeEmptyCompletion();
        var turn = new NativeTurnTowardsObjectRunner(m, id, NativeTurnTowardsObjectRunner.AnyCode, ConfigMaxTurn, false, false);
        turn.SetTolerances(ConfigPanTolerance, ConfigTiltTolerance);
        sequence.AddAction(turn, false, false);
        _displayVerify = ConfigImages >= 1 ? new NativeVerifyObjectRunner(m, id, NativeTurnTowardsObjectRunner.AnyCode, ConfigImages) : null;
        if (_displayVerify is not null) sequence.AddAction(_displayVerify, false, false);
        _displayAnimation = null;
        if (!ConfigAlwaysStreamline && !_hardSpark)
        {
            _displayAnimation = new TriggerLiftSafeAnimationAction(m.Robot, new(ConfigReaction, 1, true, 0, 60f, false));
            sequence.AddAction(_displayAnimation, false, false);
        }
        StartOwnedAction(generation, sequence, result =>
        {
            if (!IsGeneration(generation)) return;
            if (result.Result == 0) { _lookedUp = false; LookForStackedCubes(generation); }
            else FinishIteration(generation);
        });
    }

    private void LookForStackedCubes(long generation)
    {
        if (!IsGeneration(generation) || !EnterGhostUse()) return;
        try { LookForStackedCubesCore(generation); }
        finally { ExitGhostUse(); }
    }
    private void LookForStackedCubesCore(long generation)
    {
        if (_current is not { } id || _world.GetLocatedObjectById(id) is not { } target)
        {
            Log("BehaviorAcknowledgeObject.StackedCube.NullTargetObject");
            FinishIteration(generation);
            return;
        }
        float height;
        try
        {
            using var flat = target.NativePose.GetWithRespectToRoot();
            height = (float)NativeConfigurationGeometry.Dimensions(target, flat).Z;
        }
        catch (NativeConfigurationUnavailable e) { MissingGhost(generation, "M12-033 target dimensions: " + e.Message); return; }
        if (_highTarget)
        {
            if (!GhostVisibility(generation, target, -height, out bool belowVisible, out _)) return;
            if (!IsGeneration(generation)) return;
            _highTarget = !belowVisible;
        }
        if (!GhostVisibility(generation, target, height, out bool aboveVisible, out bool outside)) return;
        if (!IsGeneration(generation)) return;
        if (!aboveVisible && outside)
        {
            SetGhostRelative(target, height);
            bool backup = _lookedUp;
            if (!backup)
            {
                if (SuppliedClosestMarker is null) { MissingGhost(generation, "M12-033 closest marker/ghost geometry"); return; }
                var marker = SuppliedClosestMarker(_ghostPose);
                if (!IsGeneration(generation)) return;
                var pose = marker.Success ? marker.Pose : _ghostPose; // L6: actual failure uses ghost pose
                if (pose is null) { MissingGhost(generation, "M12-033 supplied successful closest-marker pose missing"); return; }
                if (SuppliedHeadAngle is null) { MissingGhost(generation, "M13-026 head angle input"); return; }
                var head = SuppliedHeadAngle(pose);
                if (!IsGeneration(generation)) return;
                _lookedUp = true;
                backup = head.Result != 0 || new Radians(head.Angle) > new Radians(BitConverter.UInt32BitsToSingle(0x3F1A266B));
            }
            LookAtGhost(generation, backup, () => LookForStackedCubes(generation));
            return;
        }
        if (_highTarget)
        {
            SetGhostRelative(target, -height);
            LookAtGhost(generation, false, () => FinishIteration(generation));
        }
        else FinishIteration(generation);
    }

    private void ReportGhostUnavailable(string detail) =>
        ReportMissing("M15-081 M11-010/M12-033/M13-026 stacked-cube look unavailable: " + detail);

    private void MissingGhost(long generation, string detail)
    {
        ReportGhostUnavailable(detail);
        if (IsGeneration(generation)) FinishIteration(generation);
    }
    private void SetGhostRelative(ObservableObject target, float offset)
    {
        using var flat = target.NativePose.GetWithRespectToRoot();
        using var root = target.NativePose.FindRoot();
        var p = flat.Translation;
        using var next = new Pose3d(flat.Quaternion, new Vec3((float)p.X, (float)p.Y, (float)p.Z + offset), parent: root);
        _ghostPose.Assign(next); GhostPoseState = PoseState.Dirty; // G2/P1
    }
    private bool GhostVisibility(long generation, ObservableObject target, float offset, out bool visible, out bool outside)
    {
        visible = outside = false;
        if (SuppliedVisibilityReason is null) { MissingGhost(generation, "M11-010 ghost visibility reason"); return false; }
        using var saved = new Pose3d(_ghostPose);
        SetGhostRelative(target, offset);
        int reason;
        try { reason = SuppliedVisibilityReason(_ghostPose); }
        finally { _ghostPose.Assign(saved); GhostPoseState = PoseState.Dirty; }
        outside = reason == 6;
        visible = reason is 0 or 7 or 8;
        return true;
    }
    private void LookAtGhost(long generation, bool backup, Action continuation)
    {
        if (!IsGeneration(generation)) return;
        if (SuppliedGhostLook is { } supplied)
        {
            supplied(backup, () => { using var owner = EnterHostExecutionOwner(Context); if (IsGeneration(generation)) continuation(); });
            return;
        }
        if (_nativeManipulation is not { } m) { ReportMissing("M15-081 ghost action owner unavailable; ending behavior"); return; }
        var sequence = new GhostLookSequence(m, this, generation);
        sequence.SeedNativeEmptyCompletion();
        if (backup) sequence.AddAction(new DriveStraightAction(m, -25, 100f, false), false, false);
        sequence.AddAction(new MissingGhostTurn(m), false, false);
        sequence.AddAction(new NativeWaitForImagesRunner(m, ConfigImages, (VisionMode)1, 0), false, false);
        StartOwnedAction(generation, sequence, _ => { if (IsGeneration(generation)) continuation(); }); // G4: result ignored
    }
    private sealed class MissingGhostTurn(ManipulationSystem m) : NativeVisionRunner(m, 0x2C, 5)
    {
        protected override string ActionName => "TurnTowardsCustomObject";
        public override int Init() => throw new NativeActionUnavailable("M12-033 ghost marker geometry/closest marker input unavailable at 0x0054A27C");
        public override uint CheckIfDone() => EngineActionResult.Running;
    }
    private sealed class GhostLookSequence(ManipulationSystem m, AcknowledgeObjectBehavior owner, long generation)
        : CompoundActionSequential(() => m.Robot.Engine.Timer.SecondsF)
    {
        private bool _missing;
        public override uint Update()
        {
            if (_missing) return EngineActionResult.Running;
            int depth = Watcher?.HostUpdateDepth ?? 0;
            try { return base.Update(); }
            catch (NativeActionUnavailable error)
            {
                Watcher?.UnwindUnavailableHostUpdate(depth);
                _missing = true;
                if (owner.IsGeneration(generation))
                {
                    owner.ReportGhostUnavailable(error.Message);
                    m.Robot.Engine.Robot!.ActionList.DeferHostCleanup(() =>
                    {
                        if (!owner.IsGeneration(generation)) return;
                        owner.StopActing();
                        if (owner.IsGeneration(generation)) owner.FinishIteration(generation);
                    });
                }
                return EngineActionResult.Running;
            }
        }
    }

    private void FinishIteration(long generation)
    {
        if (!IsGeneration(generation)) return;
        if (_current is { } id)
        {
            _displayVerify = null; _displayAnimation = null;
            foreach (var listener in Listeners())
            {
                listener.RobotReactedToId(id);
                if (!IsGeneration(generation)) return;
            }
            ReactedTo?.Invoke(id);
            if (!IsGeneration(generation)) return;
            lock (_targets) _targets.Remove(unchecked((int)id));
            bool more; lock (_targets) more = _targets.Count != 0;
            if (more) { ReportObjective(generation); if (IsGeneration(generation)) BeginIteration(generation); return; }
            var m = _nativeManipulation;
            if (m is null) { CurrentPhase = Phase.Idle; ReportMissing("ACKNOWLEDGE-OBJECT-1 F1: final turn requires actual manipulation owner; ending behavior"); return; }
            CurrentPhase = Phase.Turning;
            var turn = new NativeTurnTowardsObjectRunner(m, id, NativeTurnTowardsObjectRunner.AnyCode, ConfigMaxTurn, false, false);
            StartOwnedAction(generation, turn, _ => { ReportObjective(generation); if (IsGeneration(generation)) BeginIteration(generation); }); // F2 ignores result
            return;
        }
    }

    private void ReportObjective(long generation)
    { if (IsGeneration(generation)) Context.Robot.Engine.ReportBehaviorObjective(0x1B, "ReactedAcknowledgedObject", Id); }

    private void StartOwnedAction(long generation, ActionRunner action, Action<RobotCompletedAction> completion)
    {
        if (!IsGeneration(generation)) { action.WatcherEnding(); return; }
        StartActing(action, completion);
        if (!IsGeneration(generation) && !action.DestructionObserved)
            Context.Robot.Engine.Robot?.ActionList.Cancel(action.Tag); // reentrant Stop during QueueAction: retire stale admission before leaving owner
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        long stoppingGeneration = Interlocked.Increment(ref _hostGeneration);
        foreach (var listener in Listeners())
        {
            listener.ClearDesiredTargets(); // S1, slot 1
            if (HostGeneration != stoppingGeneration || Volatile.Read(ref _ghostRetired) != 0) return;
        }
        _current = null;
        _beginPending = false;
        _displayVerify = null; _displayAnimation = null;
        CurrentPhase = Phase.Idle;
        lock (_targets) _targets.Clear();
    }
}
