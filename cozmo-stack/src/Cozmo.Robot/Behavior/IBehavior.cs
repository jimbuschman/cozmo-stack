namespace Cozmo.Robot.Behavior;

/// <summary>Where a behaviour is in its lifecycle.</summary>
public enum BehaviorLifecycle
{
    /// <summary>Constructed and configured, not running.</summary>
    Ready,
    /// <summary>Running.</summary>
    Running,
    /// <summary>Running, and waiting for the action it started to finish.</summary>
    Acting,
    /// <summary>Asked to stop once its current action completes.</summary>
    Stopping,
    /// <summary>Not running. May be started again.</summary>
    Stopped,
}

/// <summary>Why a behaviour stopped, which decides whether it is penalised for repeating.</summary>
public enum BehaviorStopReason
{
    /// <summary>It finished what it set out to do.</summary>
    Completed,
    /// <summary>Something of higher priority took over.</summary>
    Interrupted,
    /// <summary>A caller stopped it.</summary>
    Cancelled,
    /// <summary>It could not continue.</summary>
    Failed,
}

/// <summary>
/// What a behaviour can reach. Passed in rather than reached for, so a behaviour can be exercised
/// offline against a robot that is not connected.
/// </summary>
public sealed class BehaviorContext
{
    public required CozmoRobot Robot { get; init; }
    public required AnimationTriggerMap Triggers { get; init; }
    public BehaviorArbiter? Arbiter { get; init; }
    public MoodState? Mood { get; set; }
    /// <summary>
    /// The engine's <c>AIComponent</c> at <c>[robot+0x264]</c>; its +0x10 is the <c>BehaviorHelperComponent</c> that <c>IBehavior::SmartDelegateToHelper</c> 0x005bebee..0x005bebf8
    /// and <c>IBehavior::StopHelperWithoutCallback</c> 0x005bd2a6..0x005bd2b0 reach through <c>[[[this+0x2c]+0x264]+0x10]</c>. Set by <see cref="FreeplayStack.Create"/>.
    /// Null: no helper component, and a behaviour that asks to delegate to a helper throws <see cref="NotSupportedException"/>.
    /// </summary>
    // fidelity: M8-011
    public AIComponent? AI { get; set; }
    public Random Random { get; init; } = new();
    // fidelity: M10-009
    // The engine's StrategyObstacleDetected (0x006141F8) reads AIComponent+4 (0x006143CA) and nothing writes it, so this context has no obstacle seam of its own: the flag
    // is AIComponent.ObstacleDetected, which the AIComponent (BehaviorContext.AI) hosts and no production code can raise.

    /// <summary>
    /// The needs, for the two wants-to-run strategies that read them: <c>InNeedsBracket</c> and
    /// <c>ExpressNeedsTransition</c>. Null means a behaviour with one of those strategies cannot say yes,
    /// which is what a robot with no needs manager would mean.
    /// </summary>
    public NeedsManager? Needs { get; set; }

    /// <summary>
    /// The one value <c>StrategyExpressNeedsTransition::WantsToRunInternal</c> 0x006136D8 compares its need
    /// against: <c>[[robot+0x264]+0x30]+0x14</c> (<c>ldr.w r0,[r1,#0x264]</c> 0x006136DC; <c>ldr r5,[r0,#0x30]</c>
    /// 0x006136E2; <c>ldr r2,[r5,#0x14]</c> 0x006136FC). The inventory records the name of that object and of
    /// its +0x14 field as UNKNOWN, and nothing in this stack supplies that exact value (the per-need
    /// <c>NeedsManager.IsSevereExpressed</c> set is not it), so it is a seam: null makes
    /// <c>ExpressNeedsTransition</c> throw <see cref="NotSupportedException"/> rather than answer. MISSING: M8-006.
    /// </summary>
    // fidelity: M8-006
    public Func<NeedId?>? AiExpressedNeedValue { get; set; }

    /// <summary>
    /// The <c>needsActionID</c> each shipped behaviour config carries, by <c>behaviorID</c>
    /// (<see cref="BehaviorNeedsActions.Load"/>). <c>IBehavior::NeedActionCompleted</c> 0x005BE40C reports
    /// the running behaviour's own id when the caller names none, so the hook has to know it. Null: no
    /// behaviour has one, and only the explicitly named actions are reported.
    /// </summary>
    public IReadOnlyDictionary<string, string>? NeedsActionIds { get; set; }

    /// <summary>
    /// The memory map, the engine's <c>MapComponent::GetCurrentMemoryMapHelper</c>. Null means no map is
    /// attached and the behaviours that ask it a question get no answer - which is not the same as an
    /// answer of "clear".
    /// </summary>
    public Vision.MemoryMap? Map { get; set; }

    /// <summary>
    /// The shared repetition-penalty history. A behaviour that wants the engine's
    /// <c>StopWithoutImmediateRepetitionPenalty</c> (the M7/M15 concrete behaviours that call it) needs
    /// this to stamp its own +0x108 window; the scored chooser reads the same object. Set by
    /// <see cref="BehaviorManager"/>'s constructor.
    /// </summary>
    public RepetitionPenalty? Penalty { get; set; }

    /// <summary>The behaviour clock (seconds), for the recent-event windows below. Null: the windows cannot be met.</summary>
    public Func<double>? ClockSec { get; set; }
    /// <summary>
    /// The timestamps <c>IBehavior::IsRunnableBase</c> (0x005BD778) compares against <c>requiredRecentDriveOffCharger_sec</c>,
    /// <c>requiredRecentOnTreadsEventSecs</c> and <c>requiredRecentSwitchToParent_sec</c> (the engine reads them off robot
    /// components; the freeplay layer stamps them here). Null: the event has not happened.
    /// </summary>
    /// <summary>
    /// <c>[[robot+0x264]+0x18]+0x44</c>, a float: written only by <c>BehaviorDriveOffCharger::UpdateInternal</c> 0x005C0E08 (<c>BaseStationTimer::GetCurrentTimeInSeconds</c> is a float), read by
    /// <c>IBehavior::IsRunnableBase</c> 0x005BD93C; the whiteboard constructor stores -1.0f (0x0056A2B6), here null.
    /// </summary>
    public float? LastDriveOffChargerSec { get; set; }
    public double? LastOnTreadsEventSec { get; set; }
    public double? LastActivitySwitchSec { get; set; }
}

/// <summary>
/// One behaviour: something Cozmo can decide to do.
///
/// This is a reconstruction of the shipped <c>IBehavior</c>, whose interface **is** exported by the engine
/// even though the concrete <c>BehaviorReactToX</c> subclasses are not. The lifecycle below is taken from
/// those exported names rather than designed here:
///
/// * <c>Init</c>, <c>IsRunnable</c>, <c>Update</c>/<c>UpdateInternal</c>, <c>Stop</c>, <c>Resume</c>
/// * <c>StartActing</c>, <c>StopActing</c>, <c>HandleActionComplete</c>,
///   <c>StopOnNextActionComplete</c> — a behaviour runs *actions* and is told when they finish
/// * <c>EvaluateScore</c>, <c>EvaluateRepetitionPenalty</c>, <c>IncreaseScoreWhileActing</c> — which
///   behaviour runs is decided by score, not by a fixed list
/// * the <c>Smart*</c> family — <c>SmartLockTracks</c>, <c>SmartPushIdleAnimation</c>,
///   <c>SmartSetCustomLightPattern</c>, <c>SmartDisableReactionsWithLock</c> and their removals — are
///   scoped acquisitions the engine releases for the behaviour when it stops. That pattern is reproduced
///   by <see cref="BehaviorScope"/>.
/// </summary>
public interface IBehavior
{
    /// <summary>The shipped <c>behaviorID</c> this implements.</summary>
    string Id { get; }

    /// <summary>The shipped <c>behaviorClass</c> this implements.</summary>
    string Class { get; }

    /// <summary>Whether this could run right now, given the robot's state.</summary>
    bool IsRunnable(BehaviorContext context);

    /// <summary>
    /// How much this wants to run, before penalties. The engine's <c>EvaluateScoreInternal</c> 0x005BEEC2:
    /// the behaviour's emotion scorers if its config gave it any, otherwise its <c>flatScore</c>, which
    /// <c>IBehavior::IBehavior</c> leaves at zero when the config carries no scoring (0x005BBD28). A
    /// behaviour built in code carries that same zero (M8-004): no in-code score is invented. Zero or
    /// less means it does not want to run at all.
    /// </summary>
    double EvaluateScore(BehaviorContext context);

    /// <summary>Starts. Called only when <see cref="IsRunnable"/> was true.</summary>
    Task StartAsync(BehaviorContext context, BehaviorScope scope, CancellationToken cancel);

    /// <summary>
    /// Advances the behaviour. Returns false when it has finished of its own accord, which the engine
    /// expresses by the behaviour completing its actions.
    /// </summary>
    bool Update(BehaviorContext context, double nowMs);

    /// <summary>Stops. Anything the behaviour acquired through its scope is released after this returns.</summary>
    void Stop(BehaviorStopReason reason);
}

/// <summary>
/// The scoped resources a behaviour holds while it runs.
///
/// The engine's <c>Smart*</c> methods exist so a behaviour cannot leak a track lock or a light pattern by
/// forgetting to undo it: whatever it takes is released when it stops. This does the same, and
/// <see cref="Dispose"/> is what releases everything at once.
/// </summary>
// fidelity: M8-011
public sealed class BehaviorScope : IDisposable
{
    // IBehavior::Stop 0x005bd08c releases the scope in this fixed order regardless of acquisition order:
    // (a) disable-reaction locks (0x005bd12c), (b) the idle animation (0x005bd142), (c) the motion
    // profile (0x005bd150), (d) the track-lock map (0x005bd15c..0x005bd174), then the custom light-pattern
    // vector at +0xcc/+0xd0 (0x005bd1a2..0x005bd1c6). This stack's own OnRelease hooks have no engine
    // counterpart and run last. Within a category the engine's order is the container's: the reaction
    // locks are a std::set<string> taken lowest key first (0x005bd12c/0x005bd130) and the track locks a
    // std::map<string, u8> walked in key order (0x005bd15c..0x005bd196), so each undo carries its key and
    // Dispose sorts by it, ordinal. M8-009.
    private const int OrderReactionLocks = 0, OrderIdle = 1, OrderMotionProfile = 2, OrderTrackLocks = 3,
                      OrderLightPatterns = 4, OrderOther = 5;
    private readonly List<(int Order, string Key, Action Undo)> _undo = new();
    private readonly object _gate = new();
    private readonly BehaviorArbiter? _arbiter;
    private readonly CozmoMotion? _motion;
    private readonly BehaviorManager? _manager;
    private readonly string _owner = "scope-" + System.Threading.Interlocked.Increment(ref _scopeCounter);
    private static int _scopeCounter;
    private Animation.AnimationTrack _motionLocked;
    private bool _disposed;

    private void AddUndo(int order, Action undo)
    {
        if (_disposed) undo();
        else _undo.Add((order, "", undo));
    }

    // IBehavior's Smart* state: the per-resource flags the engine keeps at +0xb0 (idle), +0xc0
    // (motion profile), +0xb4 (named track locks), +0xcc (custom light patterns) and +0xa4 (reaction
    // locks). They make the double-acquire and missing-release VERIFY paths expressible.
    private readonly Dictionary<string, Animation.AnimationTrack> _trackLocks = new(StringComparer.Ordinal);
    private readonly HashSet<uint> _lightPatterns = new();
    private readonly HashSet<string> _reactionLockNames = new(StringComparer.Ordinal);
    private bool _arbiterReactionLock;
    // The tracks this scope claimed through the unnamed LockTracks (not an engine Smart* helper); the
    // named SmartLockTracks claims live in _trackLocks. LockedTracks is derived from both, so no undo has
    // to restore a saved mask and the release order cannot leave one behind.
    private Animation.AnimationTrack _unnamedLocked;
    private Action? _idleRemove;
    private Action? _motionClear;

    /// <summary>
    /// A scope with no arbiter/motion models the locks without enforcing them, which is only useful in
    /// tests. Pass the arbiter for the reaction lock and the motion for the track lock to take effect.
    /// </summary>
    public BehaviorScope(BehaviorArbiter? arbiter = null, CozmoMotion? motion = null, BehaviorManager? manager = null)
    {
        _arbiter = arbiter;
        _motion = motion;
        _manager = manager;
    }

    /// <summary>The manager this scope's reaction locks go through (<c>[robot+0x44]</c>), or null in a scope built without one. <c>PlaceObjectOnGroundAction::Init</c> takes its own lock on it (M15-022).</summary>
    internal BehaviorManager? Manager => _manager;

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    internal bool IsDisposed { get { lock (_gate) return _disposed; } }

    /// <summary>Tracks this behaviour has claimed, released when it stops.</summary>
    public Animation.AnimationTrack LockedTracks
    {
        get
        {
            lock (_gate)
            {
                var tracks = _unnamedLocked;
                foreach (var held in _trackLocks.Values) tracks |= held;
                return tracks;
            }
        }
    }

    /// <summary>Whether this behaviour has asked for reactions to be held off.</summary>
    public bool ReactionsDisabled { get; private set; }

    /// <summary>
    /// The engine's <c>IBehavior::SmartLockTracks</c> 0x005be5bc: claim tracks for as long as this behaviour
    /// runs. The claim is taken on the robot's <c>MovementComponent</c> (one owner per track in the multiset,
    /// <c>LockTracks</c> 0x00640098) so another action's <c>AreAnyTracksLocked</c> sees it, and it is
    /// released through <c>UnlockTracks</c> 0x0063fe5c when the scope is disposed.
    /// </summary>
    public void LockTracks(Animation.AnimationTrack tracks)
    {
        lock (_gate)
        {
            if (_disposed) return;
            bool first = _unnamedLocked == Animation.AnimationTrack.None;
            _unnamedLocked |= tracks;
            var newMotion = tracks & ~_motionLocked;
            if (_motion is { } motion && newMotion != Animation.AnimationTrack.None)
            {
                byte mask = CozmoMotion.MaskFor(newMotion);
                if (mask != 0) motion.LockTracks(mask, _owner);
                _motionLocked |= newMotion;
            }
            if (!first) return;
            _undo.Add((OrderTrackLocks, _owner, () =>
            {
                _unnamedLocked = Animation.AnimationTrack.None;
                if (_motion is { } m && _motionLocked != Animation.AnimationTrack.None)
                {
                    byte mask = CozmoMotion.MaskFor(_motionLocked);
                    if (mask != 0) m.UnlockTracks(mask, _owner);
                    _motionLocked = Animation.AnimationTrack.None;
                }
            }));
        }
    }

    /// <summary>
    /// The engine's SmartDisableReactionsWithLock: stop reactions interrupting this behaviour.
    ///
    /// The lock is taken on the arbiter, so it actually suppresses reactions rather than only recording
    /// that it was asked for, and it is released with the scope.
    /// </summary>
    public void DisableReactions()
    {
        lock (_gate)
        {
            if (_disposed || _arbiterReactionLock) return;
            _arbiterReactionLock = true;
            ReactionsDisabled = true;
            _arbiter?.DisableReactions(this);
            _undo.Add((OrderReactionLocks, "", () =>
            {
                _arbiterReactionLock = false;
                ReactionsDisabled = false;
                _arbiter?.EnableReactions(this);
            }));
        }
    }

    /// <summary>Registers any other undo, for resources this type does not model directly.</summary>
    public void OnRelease(Action undo)
    {
        lock (_gate) AddUndo(OrderOther, undo);
    }

    // ============================================================== the Smart* scope helpers (M8-011)

    /// <summary>Whether an idle animation is pushed (IBehavior +0xb0).</summary>
    public bool IdleAnimationSet { get; private set; }
    /// <summary>Whether a custom motion profile is set (IBehavior +0xc0).</summary>
    public bool MotionProfileSet { get; private set; }
    /// <summary>The custom light-pattern object ids set (IBehavior +0xcc).</summary>
    public IReadOnlyCollection<uint> CustomLightPatterns => _lightPatterns;
    /// <summary>The named track locks held (IBehavior +0xb4).</summary>
    public IReadOnlyDictionary<string, Animation.AnimationTrack> NamedTrackLocks => _trackLocks;

    /// <summary>
    /// A VERIFY failure, the engine's <c>sVerifyFailedReturnFalse</c>. Raised when a Smart* helper is
    /// misused (a double acquire or a release of something not held); the helper returns false.
    /// </summary>
    public event Action<string>? VerifyFailed;
    private void Verify(string what) => VerifyFailed?.Invoke("VERIFY: IBehavior." + what);

    /// <summary>
    /// <c>IBehavior::SmartPushIdleAnimation</c> 0x005be41c: fails if +0xb0 is already set; otherwise
    /// pushes the idle animation and sets +0xb0.
    /// </summary>
    public bool SmartPushIdleAnimation(Action push, Action remove)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (IdleAnimationSet) { Verify("SmartPushIdleAnimation: an idle is already set"); return false; }
            push();
            IdleAnimationSet = true;
            _idleRemove = remove;
            _undo.Add((OrderIdle, "", () => { if (IdleAnimationSet) { IdleAnimationSet = false; _idleRemove = null; remove(); } }));
            return true;
        }
    }

    /// <summary>
    /// <c>IBehavior::SmartRemoveIdleAnimation</c> 0x005bd4c8: when +0xb0 is clear it raises
    /// <c>VERIFY(%s): Behavior %s is trying to remove an idle, but none is currently set</c> through
    /// <c>sVerifyFailedReturnFalse</c>; otherwise it removes the idle and clears +0xb0.
    /// </summary>
    public bool SmartRemoveIdleAnimation()
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (!IdleAnimationSet) { Verify("SmartRemoveIdleAnimation: no idle is currently set"); return false; }
            IdleAnimationSet = false;
            var remove = _idleRemove; _idleRemove = null;
            remove?.Invoke();
            return true;
        }
    }

    /// <summary><c>IBehavior::SmartSetMotionProfile</c> 0x005be518 verifies +0xc0 == 0, sets it, and records the clear.</summary>
    public bool SmartSetMotionProfile(Action set, Action clear)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (MotionProfileSet) { Verify("SmartSetMotionProfile: a motion profile is already set"); return false; }
            set();
            MotionProfileSet = true;
            _motionClear = clear;
            _undo.Add((OrderMotionProfile, "", () => { if (MotionProfileSet) { MotionProfileSet = false; _motionClear = null; clear(); } }));
            return true;
        }
    }

    /// <summary><c>IBehavior::SmartClearMotionProfile</c> 0x005bd584 verifies +0xc0 != 0, clears it.</summary>
    public bool SmartClearMotionProfile()
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (!MotionProfileSet) { Verify("SmartClearMotionProfile: no motion profile is set"); return false; }
            MotionProfileSet = false;
            var clear = _motionClear; _motionClear = null;
            clear?.Invoke();
            return true;
        }
    }

    /// <summary>
    /// <c>IBehavior::SmartLockTracks</c> 0x005be5bc: a new key locks the tracks and returns true; an
    /// existing key warns "Attempted to lock tracks with key named %s but key already exists" and returns
    /// false without locking twice.
    /// </summary>
    public bool SmartLockTracks(string name, Animation.AnimationTrack tracks)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (_trackLocks.ContainsKey(name)) { Verify($"SmartLockTracks: track lock '{name}' already exists"); return false; }
            _trackLocks[name] = tracks;
            byte mask = CozmoMotion.MaskFor(tracks);
            if (_motion is { } m && mask != 0) m.LockTracks(mask, _owner + ":" + name);
            _undo.Add((OrderTrackLocks, name, () =>
            {
                if (_trackLocks.Remove(name))
                {
                    if (_motion is { } mm && mask != 0) mm.UnlockTracks(mask, _owner + ":" + name);
                }
            }));
            return true;
        }
    }

    /// <summary>
    /// <c>IBehavior::SmartUnLockTracks</c> 0x005be6e0: found -> unlock, erase, true; absent -> warn, false.
    /// </summary>
    public bool SmartUnLockTracks(string name)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (!_trackLocks.TryGetValue(name, out var tracks)) { Verify($"SmartUnLockTracks: no track lock named '{name}'"); return false; }
            _trackLocks.Remove(name);
            byte mask = CozmoMotion.MaskFor(tracks);
            if (_motion is { } m && mask != 0) m.UnlockTracks(mask, _owner + ":" + name);
            return true;
        }
    }

    /// <summary>
    /// <c>IBehavior::SmartSetCustomLightPattern</c> 0x005be7f0: an ObjectID already in the vector at +0xcc
    /// logs on the "Unnamed" channel and returns false; otherwise it plays the light animation, appends the
    /// ObjectID and returns true.
    /// </summary>
    public bool SmartSetCustomLightPattern(uint objectId, Action play)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (!_lightPatterns.Add(objectId)) { Verify($"SmartSetCustomLightPattern: a light pattern is already set for object {objectId}"); return false; }
            play();
            _undo.Add((OrderLightPatterns, "", () => _lightPatterns.Remove(objectId)));
            return true;
        }
    }

    /// <summary>
    /// <c>IBehavior::SmartRemoveCustomLightPattern</c> 0x005be9b0: an ObjectID not set logs "No custom light
    /// pattern is set for object %d" and returns false; otherwise the triggers' animations are stopped and
    /// the entry is erased.
    /// </summary>
    public bool SmartRemoveCustomLightPattern(uint objectId, Action remove)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (!_lightPatterns.Remove(objectId)) { Verify($"SmartRemoveCustomLightPattern: no light pattern is set for object {objectId}"); return false; }
            remove();
            return true;
        }
    }

    /// <summary>
    /// <c>IBehavior::SmartDisableReactionsWithLock</c> 0x005bce3c: the name gets the "_behaviorLock"
    /// suffix at the manager, and the original name goes into the per-behaviour set at +0xa4. The manager
    /// side is M7-014; here it holds the scope's arbiter reaction lock.
    /// </summary>
    public bool SmartDisableReactionsWithLock(string name)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (!_reactionLockNames.Add(name)) { Verify($"SmartDisableReactionsWithLock: '{name}' is already held"); return false; }
            ReactionsDisabled = true;
            if (!_arbiterReactionLock)
            {
                _arbiterReactionLock = true;
                _arbiter?.DisableReactions(this);
            }
            _undo.Add((OrderReactionLocks, name, () =>
            {
                if (_reactionLockNames.Remove(name) && _reactionLockNames.Count == 0)
                {
                    _arbiterReactionLock = false;
                    ReactionsDisabled = false;
                    _arbiter?.EnableReactions(this);
                }
            }));
            return true;
        }
    }

    /// <summary>
    /// <c>IBehavior::SmartDisableReactionsWithLock(lockName, table)</c> 0x005bce3c: append
    /// <c>"_behaviorLock"</c>, call <c>BehaviorManager::DisableReactionsWithLock(manager,
    /// name+"_behaviorLock", table, true)</c> (0x005bce62), then insert the original name into the
    /// per-behaviour set at +0xa4 (0x005bce7e). The manager disables only the triggers the class's own
    /// 21-byte table marks, so this takes the manager reference; the original name is what
    /// <see cref="SmartRemoveDisableReactionsLock"/> removes.
    /// </summary>
    // fidelity: M7-014
    public bool SmartDisableReactionsWithLock(string name, ReactionLockTable table)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (!_reactionLockNames.Add(name)) { Verify($"SmartDisableReactionsWithLock: '{name}' is already held"); return false; }
            ReactionsDisabled = true;
            string managerName = name + "_behaviorLock";
            _manager?.DisableReactionsWithLock(managerName, table, stopCurrent: true);
            _undo.Add((OrderReactionLocks, name, () =>
            {
                if (_reactionLockNames.Remove(name))
                {
                    _manager?.RemoveDisableReactionsLock(managerName);
                    if (_reactionLockNames.Count == 0) ReactionsDisabled = false;
                }
            }));
            return true;
        }
    }

    /// <summary><c>IBehavior::SmartRemoveDisableReactionsLock</c> 0x005bd470: append "_behaviorLock", call
    /// <c>BehaviorManager::RemoveDisableReactionsLock</c> (0x005bd48c), then erase the original name from
    /// +0xa4 (0x005bd4a4). The manager side is M7-014.</summary>
    // fidelity: M7-014
    public bool SmartRemoveDisableReactionsLock(string name)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (!_reactionLockNames.Remove(name)) { Verify($"SmartRemoveDisableReactionsLock: '{name}' is not held"); return false; }
            _manager?.RemoveDisableReactionsLock(name + "_behaviorLock");
            if (_reactionLockNames.Count == 0 && ReactionsDisabled)
            {
                _arbiterReactionLock = false;
                ReactionsDisabled = false;
                _arbiter?.EnableReactions(this);
            }
            return true;
        }
    }

    // fidelity: M8-011
    // IBehavior::SmartDelegateToHelper 0x005beb10 is SteppedBehavior.SmartDelegateToHelper: the weak reference at +0xc4/+0xc8 belongs to the behaviour, not to a
    // run's scope, and the call reaches BehaviorHelperComponent::DelegateToHelper through BehaviorContext.AI.

    /// <summary>
    /// Releases the scope in the engine's fixed order (<c>IBehavior::Stop</c> 0x005bd08c):
    /// (a) disable-reaction locks (0x005bd12c), (b) the idle animation (0x005bd142), (c) the motion
    /// profile (0x005bd150), (d) the track locks (0x005bd15c..0x005bd174), then the custom light-pattern
    /// vector at +0xcc/+0xd0 (0x005bd1a2..0x005bd1c6). Within a category the engine's container order
    /// applies: the reaction locks are a <c>std::set</c> of names, each pass taking the first node
    /// (0x005bd12c/0x005bd130), lowest key first; the track locks are a <c>std::map</c> from name to mask
    /// walked in key order, one <c>UnlockTracks</c> (and so possibly one EnableAnimTracks) per entry
    /// (0x005bd15c..0x005bd196). Both sort by name, ordinal (the byte order of <c>std::less&lt;string&gt;</c>
    /// for the ASCII names in use). This stack's own OnRelease hooks have no engine counterpart and run last,
    /// most-recent-first.
    /// </summary>
    // fidelity: M8-009
    public void Dispose()
    {
        List<(int Order, string Key, Action Undo)> undo;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            undo = new List<(int Order, string Key, Action Undo)>(_undo);
            _undo.Clear();
        }
        for (int order = OrderReactionLocks; order <= OrderOther; order++)
        {
            if (order == OrderOther)
            {
                for (int i = undo.Count - 1; i >= 0; i--)
                    if (undo[i].Order == order) Run(undo[i].Undo);
                continue;
            }
            foreach (var item in undo.Where(u => u.Order == order).OrderBy(u => u.Key, StringComparer.Ordinal))
                Run(item.Undo);
        }
    }

    private static void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
    }
}

/// <summary>
/// The <c>IBehavior::Init</c> action-tag guard (0x005bcb54..0x005bcd80). <c>action+0x60</c> is the
/// action's 32-bit tag; the guard flags an action whose tag is greater than <c>0x2dc6c0</c>
/// (<c>movw r6,#0xc6c0</c> 0x005bcbd0 / <c>movt r6,#0x2d</c> 0x005bcbda; <c>cmp r2,r6</c> 0x005bcbec;
/// <c>movhi r5,#1</c> 0x005bcbf0). <c>0x2dc6c0</c> is the integer 3,000,000, the tag-counter sentinel
/// (<c>sTagCounter</c> at 0x01051020 starts at 0x002dc6c1), not a float and not a timeout.
///
/// The warning <c>IBehavior.Init.ActionsInQueue</c> fires only when at least one engine-tagged action is
/// present; an action tagged through the game path (<c>IActionRunner::SetTag</c> 0x00540098) carries a tag
/// in [1, 2,000,000] and does not set the flag. The count it reports is the main queue's (map key 0)
/// current action plus its queued actions (<c>ldr r1,[r0,#0x14]</c> 0x005bcd64;
/// <c>ldr r0,[r0,#0x20]</c> 0x005bcd66; <c>addne r0,#1</c> 0x005bcd6c).
/// </summary>
// fidelity: M8-001
public static class BehaviorInit
{
    /// <summary>The tag-counter sentinel: an action tag above this is an engine tag.</summary>
    public const int ActionTagSentinel = 0x2dc6c0;

    /// <summary>Whether any action in the queues carries an engine tag, i.e. whether the warning fires.</summary>
    public static bool HasEngineTaggedAction(IEnumerable<int> actionTags) => actionTags.Any(t => t > ActionTagSentinel);

    /// <summary>The main queue's count: its queued actions plus one when a current action is set.</summary>
    public static int MainQueueActionCount(bool hasCurrentAction, int queuedActionCount) =>
        queuedActionCount + (hasCurrentAction ? 1 : 0);
}

/// <summary>
/// The repetition penalty: how much a behaviour's score is reduced for having run recently.
///
/// The engine's graph belongs to the behaviour (<c>IBehavior</c> +0xe8), read from its own
/// <c>repetitionPenalty</c> key. When the key is missing <c>IBehavior::ReadFromScoredJson</c> leaves the graph
/// empty and then adds one node, <c>AddNode(0.0f, 1.0f, true)</c> (0x005bc55c..0x005bc566): a flat 1.0, no
/// penalty at all. <c>mood_config.json</c>'s <c>defaultRepetitionPenalty</c> (0 s to 0.0, 30 s to 1.0) is read
/// only by the emotion-event fallback in <c>MoodManager::UpdateEventTimeAndCalculateRepetitionPenalty</c>
/// (0x0067befe..0x0067bf0a), never by a behaviour, so it is not this class's default. This class holds the
/// per-behaviour history (the +0x30 last-run stamp and the +0x108 suppression window) and evaluates the
/// graph it is given; with none given it is the flat graph.
/// </summary>
// fidelity: M8-002
public sealed class RepetitionPenalty
{
    /// <summary>1.0f, the engine's constant (<c>mov.w r2,#0x3f800000</c> 0x005bc560; <c>vmov.f32 s0,#1.0</c> 0x005beeb0).</summary>
    public static readonly float One = BitConverter.Int32BitsToSingle(0x3f800000);

    /// <summary>The graph a behaviour gets when its config has no <c>repetitionPenalty</c>: one node, (0.0, 1.0).</summary>
    public static DecayGraph FlatGraph() => new("flat", new (double, double)[] { (0.0, One) });

    private readonly DecayGraph _graph;
    private readonly Dictionary<string, double> _lastRunSec = new();
    // IBehavior +0x108: the repetition-penalty suppression threshold, in seconds. Zero until
    // StopWithoutImmediateRepetitionPenalty sets it to now + 1.0 (0x005beeb0..0x005beebc).
    private readonly Dictionary<string, double> _suppressUntilSec = new();

    public RepetitionPenalty(DecayGraph? graph = null) => _graph = graph ?? FlatGraph();

    /// <summary>
    /// The multiplier for a behaviour.
    /// <c>IBehavior::EvaluateRepetitionPenalty</c> 0x005beee6: 1.0 when the last-run stamp is &lt;= 0 (and for a
    /// NaN stamp: <c>vcmpe.f32 s0,#0</c>, <c>itt le</c>, <c>movle.w r0,#0x3f800000</c>, 0x005beeea..0x005beef8),
    /// else the graph at <c>now - lastRun</c> (0x005bef0e). A behaviour that has never been stopped has no
    /// stamp and is not penalised. This is the pure graph evaluation only; the <c>+0x108</c> suppression is
    /// applied by <c>EvaluateScore</c>'s non-running branch, not here (<c>vldr s0,[r4,#0x108]</c> 0x005befe2
    /// is in EvaluateScore).
    /// </summary>
    public double For(string behaviorId, double nowSec)
    {
        lock (_lastRunSec)
        {
            if (!_lastRunSec.TryGetValue(behaviorId, out var stored)) return One;
            float last = (float)stored;
            if (!(last > 0)) return One;
            return _graph.At((float)nowSec - last);                     // vsub.f32 now - stamp (0x005bef12)
        }
    }

    /// <summary>IBehavior +0x108 read on its own: whether the penalty is suppressed at <paramref name="nowSec"/>.</summary>
    public bool IsSuppressed(string behaviorId, double nowSec)
    {
        lock (_lastRunSec) return _suppressUntilSec.TryGetValue(behaviorId, out var until) && (float)nowSec < (float)until;   // vcmpe.f32 0x005befea: applied when now >= +0x108
    }

    /// <summary>
    /// When a behaviour last ran to completion, or null. The repetition history belongs to the behaviour, not
    /// to whichever activity's chooser happened to pick it, so a behaviour named by two activities carries one
    /// history between them.
    /// </summary>
    public double? LastRunSec(string behaviorId)
    {
        lock (_lastRunSec) return _lastRunSec.TryGetValue(behaviorId, out var last) ? last : (double?)null;
    }

    /// <summary>Records that a behaviour ran.</summary>
    public void Ran(string behaviorId, double nowSec) { lock (_lastRunSec) _lastRunSec[behaviorId] = (float)nowSec; }   // the stamp is a float (0x005bd11a)

    /// <summary>
    /// The engine's <c>IBehavior::StopWithoutImmediateRepetitionPenalty</c> (0x005beea0): the +0x108
    /// suppression threshold becomes <c>now + 1.0</c>, so the repetition penalty is skipped for about a
    /// second while the last-run stamp itself is left in place. The only engine callers are
    /// <c>BehaviorPickUpCube::UpdateInternal</c> 0x005c685c, <c>BehaviorStackBlocks::UpdateInternal</c>
    /// 0x005c991c and <c>BehaviorBuildPyramidBase::UpdateInternal</c> 0x005dd110 (all M7/M15);
    /// <c>IBehavior::Stop</c> does not call it, and nothing in M8 does. This is only the +0x108 half: the
    /// engine function calls <c>IBehavior::Stop</c> first (0x005beea4), which
    /// <see cref="SteppedBehavior.StopWithoutImmediateRepetitionPenalty"/> does before it calls this. The sum
    /// is the engine's <c>vadd.f32</c> on floats (0x005beeb0..0x005beebc).
    /// </summary>
    public void StopWithoutImmediateRepetitionPenalty(string behaviorId, double nowSec)
    {
        lock (_lastRunSec) _suppressUntilSec[behaviorId] = (float)nowSec + One;
    }

    /// <summary>
    /// The engine's <c>StopWithoutImmediateRepetitionPenalty</c>: a behaviour that was interrupted rather
    /// than completed is not penalised for it.
    /// </summary>
    public void Forget(string behaviorId) { lock (_lastRunSec) { _lastRunSec.Remove(behaviorId); _suppressUntilSec.Remove(behaviorId); } }
}
