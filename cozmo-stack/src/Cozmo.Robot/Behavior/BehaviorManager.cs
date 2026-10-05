namespace Cozmo.Robot.Behavior;

/// <summary>Why a behaviour was or was not chosen. Every selection is explainable.</summary>
public sealed record BehaviorSelection(string? Chosen, string Reason)
{
    /// <summary>Every candidate's score after penalties, for showing the working.</summary>
    public IReadOnlyList<(string Id, double Score, string Note)> Scores { get; init; }
        = Array.Empty<(string, double, string)>();
    public string? Replaced { get; init; }
}

/// <summary>
/// The activity as <c>BehaviorManager::Update</c> uses it: <c>GetCurrentActivity()</c> then the activity's own
/// <c>Update</c> (<c>vtable+0x20</c>, 0x005a2f84), and <c>ChooseNextScoredBehaviorAndSwitch</c> 0x005a2a20 asking it
/// <c>GetDesiredActiveBehavior(robot, current behaviour)</c> (0x005a2a4a). The concrete activity classes are M7/M15.
/// </summary>
// fidelity: M8-012
public interface IManagedActivity
{
    /// <summary><c>IActivity::Update</c>, <c>vtable+0x20</c> (slot 8). Its result is ignored (0x005a2f84).</summary>
    void Update(double nowSec);

    /// <summary><c>IActivity::GetDesiredActiveBehavior</c> 0x005b387c: the behaviour the activity wants running, given the one running.</summary>
    IBehavior? GetDesiredActiveBehavior(IBehavior? current, double nowSec);

    /// <summary>
    /// The stack's bookkeeping hook, called once <c>ChooseNextScoredBehaviorAndSwitch</c> has switched to
    /// <paramref name="desired"/> (null: to nothing): whether the switch started it. The engine has no such call; the
    /// stack's activity layer uses it to report what it decided.
    /// </summary>
    void BehaviorSwitched(IBehavior? desired, bool started, double nowSec);
}

/// <summary>
/// Chooses which behaviour runs, and switches between them.
///
/// A reconstruction of the shipped <c>BehaviorManager</c>. <see cref="Update"/> is its per-tick entry
/// (<c>BehaviorManager::Update</c> 0x005a2f68..0x005a31be), in the engine's order; the running state is the
/// engine's <c>BehaviorRunningAndResumeInfo</c> at <c>manager+0x1c</c>: <b>the running behaviour (+0), the behaviour
/// parked for resume (+8) and the running <c>ReactionTrigger</c> (+0x10)</b>. <c>NoneTrigger</c> (0x16) is a trigger
/// (<c>EnumToString(ReactionTrigger)</c> 0x0077065c: 21 Count, 22 NoneTrigger), not a behaviour class: "no reaction
/// is running" is a null <see cref="CurrentReactionTrigger"/> here.
///
/// * <c>ChooseNextScoredBehaviorAndSwitch</c> — selection is by the activity's chooser; the manager asks the
///   activity and switches to what it answers.
/// * <c>CheckReactionTriggerStrategies</c> and the disable locks — the M10 reaction dispatch, from the M10
///   inventory rows C3..C11 and gap pass 1 section 4.
/// * <c>FinishCurrentBehavior</c>, <c>GetCurrentBehavior</c>, <c>FindBehaviorByID</c>,
///   <c>FindBehaviorsByClass</c>.
///
/// <see cref="ChooseAndSwitch"/> is this stack's own simple ranking, with no engine counterpart (M8-004); only tests
/// and tools call it.
/// </summary>
public sealed class BehaviorManager : IDisposable
{
    private readonly List<IBehavior> _behaviors = new();
    private readonly RepetitionPenalty _penalty;
    private readonly BehaviorContext _context;
    private readonly object _gate = new();

    private IBehavior? _current;
    private BehaviorScope? _scope;
    private double _startedSec;

    public BehaviorManager(BehaviorContext context, RepetitionPenalty? penalty = null)
    {
        _context = context;
        _penalty = penalty ?? new RepetitionPenalty();
        // The concrete behaviours' StopWithoutImmediateRepetitionPenalty (M7/M15) stamps the +0x108
        // suppression window on this shared history; the scored chooser reads it back.
        context.Penalty ??= _penalty;
        if (context.Arbiter is { } arb) arb.ManagerDispatchesReactions = true;
        // fidelity: M10-004, M10-007
        // All 21 triggers are mapped and enabled at startup (gap1 4f, 4g); the detector's B12 gate asks this map.
        for (int t = 0; t < TriggerCount; t++) _map[(ReactionTrigger)t] = new TriggerInfo();
        _unexpectedMovementGate = () => IsReactionTriggerEnabled(ReactionTrigger.UnexpectedMovement);
        context.Robot.Sensors.UnexpectedMovement.ReactionTriggerEnabled = _unexpectedMovementGate;
    }

    private readonly Func<bool> _unexpectedMovementGate;

    /// <summary>Every behaviour this manager knows.</summary>
    public IReadOnlyList<IBehavior> Behaviors => _behaviors;

    /// <summary>What is running, if anything (the running info's current behaviour, +0).</summary>
    public IBehavior? Current { get { lock (_gate) return _current; } }

    /// <summary>
    /// The manager's init byte (IBehavior/BehaviorManager +0). The engine's <c>BehaviorManager::Update</c>
    /// 0x005a2f70 returns immediately with <c>"BehaviorManager.Update.NotInitialized"</c> when it is zero;
    /// this stack constructs the manager ready, so it is true unless a caller says otherwise.
    /// </summary>
    public bool Initialized { get; set; } = true;

    /// <summary>Raised for every selection, including the ones that chose nothing.</summary>
    public event Action<BehaviorSelection>? Selected;

    /// <summary>
    /// The activity <c>GetCurrentActivity()</c> returns (0x005a2f78): ticked first in <see cref="Update"/> and asked for the
    /// desired behaviour by <c>ChooseNextScoredBehaviorAndSwitch</c>. Null: no activity is attached, so neither happens.
    /// </summary>
    public IManagedActivity? Activity { get; set; }

    // ------------------------------------------------------------------ reactions (M10)

    // fidelity: M10-004
    /// <summary>One entry of the trigger map: a strategy and the behaviour the map names for it (AddStrategyMapping 0x5A1864).</summary>
    public sealed record ReactionRegistration(IReactionTriggerStrategy Strategy, IBehavior Behavior);

    /// <summary>What happened when a reaction fired.</summary>
    public sealed record ReactionSwitch(ReactionTrigger Trigger, string Behavior, string? Interrupted, bool WillResume);

    /// <summary>A map node (gap1 4a): the (strategy, behaviour) vector in JSON order (+0x14) and the lock set&lt;string&gt; (+0x20..+0x28).</summary>
    private sealed class TriggerInfo
    {
        public readonly List<ReactionRegistration> Entries = new();
        public readonly SortedSet<string> Locks = new(StringComparer.Ordinal);
    }

    /// <summary>ReactionTrigger::Count.</summary>
    public const int TriggerCount = 21;

    /// <summary>The std::map&lt;ReactionTrigger, TriggerBehaviorInfo&gt; in ascending trigger order (C4).</summary>
    private readonly SortedDictionary<ReactionTrigger, TriggerInfo> _map = new();
    /// <summary>manager+0x1C +0x10: the running <c>ReactionTrigger</c> (null is NoneTrigger 0x16).</summary>
    private ReactionTrigger? _currentReaction;
    /// <summary>manager+0x1C +8: the behaviour parked for resume.</summary>
    private IBehavior? _resumeAfterReaction;
    /// <summary>manager+0x4C: the sticky gate (C3); 0 in the constructor (0x5A08F8).</summary>
    private bool _reactionGateOpen;
    private bool _warnedNoActionList;
    // fidelity: M10-008
    /// <summary>
    /// manager+8 / +0xC (C2/C11): the constructor sets both to FLT_MAX (0x5A0882..0x5A088A), and
    /// <see cref="SetDefaultHeadAndLiftState"/> overwrites them. The FLT_MAX in +8 is what makes
    /// <see cref="TryToResumeBehavior"/> skip the restore until a default state has been set.
    /// </summary>
    private float _defaultHeadRad = float.MaxValue, _defaultLiftMm = float.MaxValue;

    /// <summary>The trigger map's entries, ascending trigger then JSON order.</summary>
    public IReadOnlyList<ReactionRegistration> Reactions { get { lock (_gate) return _map.Values.SelectMany(i => i.Entries).ToList(); } }

    /// <summary>GetCurrentReactionTrigger (0x5A19EC): the reaction that is running; null is NoneTrigger.</summary>
    public ReactionTrigger? CurrentReactionTrigger { get { lock (_gate) return _currentReaction; } }

    /// <summary>The behaviour parked for resume (manager+0x1C +8).</summary>
    public IBehavior? ParkedBehavior { get { lock (_gate) return _resumeAfterReaction; } }

    /// <summary>
    /// The repetition history this manager records on completion, shared with every chooser so a behaviour's
    /// recovery curve is the behaviour's and not one copy per activity.
    /// </summary>
    public RepetitionPenalty Penalty => _penalty;

    /// <summary>Raised when a reaction takes over (C8 "SwitchingToReaction").</summary>
    public event Action<ReactionSwitch>? ReactionTriggered;

    /// <summary>The manager's log lines (C7, C8, gap1 4a..4c).</summary>
    public event Action<string>? Log;

    /// <summary>
    /// C3: ActionList::IsEmpty, the M8/M12 interface. Null: no ActionList is attached to this stack, and the sticky
    /// "first action queued" gate cannot be evaluated; it is then not applied (logged once as MISSING).
    /// </summary>
    public Func<bool>? ActionListIsEmpty { get; set; }

    // fidelity: M10-004
    /// <summary>
    /// Adds an entry to the trigger map: appended to its trigger's vector (C4). The strategy is owned by the manager (its
    /// filters ask <see cref="IsReactionTriggerEnabled"/>), and a message-subscribing one is released with it.
    /// </summary>
    public void AddReaction(IReactionTriggerStrategy strategy, IBehavior behavior)
    {
        lock (_gate)
        {
            strategy.Manager = this;
            _map[strategy.Trigger].Entries.Add(new ReactionRegistration(strategy, behavior));
            _behaviors.RemoveAll(b => b.Id == behavior.Id);
        }
    }

    // fidelity: M10-004
    /// <summary>
    /// IsReactionTriggerEnabled (gap1 4a, 0x5A40B8..0x5A4108): the node's lock set is empty. A trigger not in the map
    /// VERIFY-fails "Reached the end of the reaction trigger map" and is disabled; all 21 are mapped (4g).
    /// </summary>
    public bool IsReactionTriggerEnabled(ReactionTrigger trigger)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(trigger, out var info)) return info.Locks.Count == 0;
        }
        Log?.Invoke($"VERIFY: BehaviorManager.IsReactionTriggerEnabled: Reached the end of the reaction trigger map ({trigger})");
        return false;
    }

    /// <summary>Whether a lock is held on a trigger.</summary>
    public bool HasDisableLock(ReactionTrigger trigger, string lockName)
    {
        lock (_gate) return _map.TryGetValue(trigger, out var info) && info.Locks.Contains(lockName);
    }

    // fidelity: M10-004
    /// <summary>
    /// DisableReactionsWithLock(lock, bool[21], stopCurrent) (gap1 4b, 0x5A27F6..0x5A2960). For each map node in ascending
    /// order whose entry is set: log "DisablingWithLock"; if its set was empty, EnabledStateChanged(robot, false) on each
    /// strategy; if the lock is not yet present, add it; if stopCurrent and the trigger is the current one, log and
    /// SwitchToBehaviorBase({null, null, 0x16}). The game message passes stopCurrent = 1 (4e). The "sdk" compare in the
    /// function is dead code (its result is discarded).
    /// </summary>
    public void DisableReactionsWithLock(string lockName, IReadOnlyList<bool> triggers, bool stopCurrent = true)
    {
        foreach (var (t, info) in Nodes())
        {
            int i = (int)t;
            if (i >= triggers.Count || !triggers[i]) continue;
            Log?.Invoke($"BehaviorManager.DisableReactionsWithLock.DisablingWithLock: {t} with lock {lockName}");
            List<ReactionRegistration> notify;
            lock (_gate) notify = info.Locks.Count == 0 ? info.Entries.ToList() : new List<ReactionRegistration>();
            foreach (var r in notify) r.Strategy.EnabledStateChanged(_context, false);
            bool stop;
            lock (_gate)
            {
                info.Locks.Add(lockName);
                stop = stopCurrent && _currentReaction == t;
            }
            if (stop)
            {
                Log?.Invoke("BehaviorManager.DisableReactionsWithLock: Disabling reaction triggers - stopping currently running one");
                lock (_gate) StopCurrentLocked(BehaviorStopReason.Interrupted, NowSec(_startedSec));
            }
        }
    }

    // fidelity: M10-004
    /// <summary>The game DisableAllReactionsWithLock (gap1 4e): table 0xC60D40, every trigger, stopCurrent = 1.</summary>
    public void DisableAllReactionsWithLock(string lockName) =>
        DisableReactionsWithLock(lockName, Enumerable.Repeat(true, TriggerCount).ToArray(), stopCurrent: true);

    // fidelity: M7-014
    /// <summary>
    /// The manager side of a behaviour class's lock: <c>BehaviorManager::DisableReactionsWithLock(manager,
    /// name, table, true)</c> as <c>IBehavior::SmartDisableReactionsWithLock</c> 0x005bce3c calls it. The
    /// class's own 21-entry table is expanded to the bool mask the manager consumes.
    /// </summary>
    public void DisableReactionsWithLock(string lockName, ReactionLockTable table, bool stopCurrent = true) =>
        DisableReactionsWithLock(lockName, table.ToMask(), stopCurrent);

    // fidelity: M10-004
    /// <summary>
    /// RemoveDisableReactionsLock(lock) (gap1 4c, 0x5A3A52..0x5A3B94): "sdk" sets the sticky gate (C3); for each node that
    /// holds the lock: log, remove; when the set becomes empty: log "ReactionReEnabled" and EnabledStateChanged(robot,
    /// true) on each strategy. The sender of "sdk" is outside the app (4i, BLOCKED_EXTERNAL for who sends it).
    /// </summary>
    public void RemoveDisableReactionsLock(string lockName)
    {
        if (lockName == "sdk") lock (_gate) _reactionGateOpen = true;
        foreach (var (t, info) in Nodes())
        {
            List<ReactionRegistration>? notify = null;
            lock (_gate)
            {
                if (!info.Locks.Contains(lockName)) continue;
                info.Locks.Remove(lockName);
                if (info.Locks.Count == 0) notify = info.Entries.ToList();
            }
            Log?.Invoke($"BehaviorManager.RemoveDisableReactionsLock: {t} lock {lockName} removed");
            if (notify is null) continue;
            Log?.Invoke($"BehaviorManager.RemoveDisableReactionsLock.ReactionReEnabled: {t}");
            foreach (var r in notify) r.Strategy.EnabledStateChanged(_context, true);
        }
    }

    private List<(ReactionTrigger, TriggerInfo)> Nodes() { lock (_gate) return _map.Select(kv => (kv.Key, kv.Value)).ToList(); }

    /// <summary>Whether a behaviour is the one running (IBehavior+0xA1 as this stack has it: the manager's current).</summary>
    public bool IsRunning(IBehavior behavior) { lock (_gate) return ReferenceEquals(_current, behavior); }

    // fidelity: M10-004, M10-008
    /// <summary>
    /// BehaviorManager::CheckReactionTriggerStrategies (0x5A3550; C3..C8). <see cref="Update"/> calls it every tick
    /// (0x005a3060), whether or not a reaction is already running.
    /// <list type="number">
    /// <item>C3: manager+0x4C |= !ActionList.IsEmpty(); while it is 0 nothing is consulted.</item>
    /// <item>C4: the triggers in ascending map order, skipping one whose lock set is non-empty (the disable count at node+0x28,
    /// 0x005a359a..0x005a359e - the only gate: the engine has no global any-lock test); each trigger's entries in JSON order.</item>
    /// <item>C5: with a current reaction, CanInterruptSelf when it is this strategy's trigger, else CanInterruptOther.</item>
    /// <item>C6: ShouldTriggerBehavior(robot, behaviour).</item>
    /// <item>C7: StopAllMotors, then the track-unlock rule.</item>
    /// <item>C8: SwitchToReactionTrigger, logged; the loop does not break, and a second switch in the tick logs
    /// "Multiple behaviors switched to in a single basestation tick".</item>
    /// </list>
    /// <paramref name="nowSec"/> is BaseStationTimer seconds. Returns the last switch.
    /// </summary>
    // fidelity: M8-012
    public ReactionSwitch? CheckReactions(double nowSec)
    {
        // C3
        if (ActionListIsEmpty is { } empty)
        {
            bool queued = !empty();
            lock (_gate)
            {
                if (queued) _reactionGateOpen = true;
                if (!_reactionGateOpen) return null;
            }
        }
        else if (!_warnedNoActionList)
        {
            _warnedNoActionList = true;
            Log?.Invoke("MISSING: BehaviorManager.CheckReactionTriggerStrategies: no ActionList is attached, so the sticky first-action gate (C3) is not applied");
        }

        ReactionSwitch? last = null;
        foreach (var (t, info) in Nodes())
        {
            List<ReactionRegistration> entries;
            lock (_gate)
            {
                if (info.Locks.Count > 0) continue;                                         // C4
                entries = info.Entries.ToList();
            }
            foreach (var reg in entries)
            {
                ReactionTrigger? cur;
                lock (_gate) cur = _currentReaction;
                if (cur is { } c)                                                           // C5
                {
                    bool may = c == reg.Strategy.Trigger ? reg.Strategy.CanInterruptSelf : reg.Strategy.CanInterruptOtherTriggeredBehavior;
                    if (!may) continue;
                }
                var rc = new ReactionContext(_context, nowSec, cur, IsRunning);
                if (!reg.Strategy.ShouldTriggerBehavior(rc, reg.Behavior)) continue;        // C6

                StopMotorsForReaction();                                                    // C7
                var sw = SwitchToReactionTrigger(reg, nowSec);                              // C8, C9
                if (sw is null)
                {
                    Log?.Invoke($"BehaviorManager.CheckReactionTriggerStrategies.FailedToSwitch: {t} -> {reg.Behavior.Id}");
                    continue;
                }
                Log?.Invoke($"BehaviorManager.CheckReactionTriggerStrategies.SwitchingToReaction: {t} -> {reg.Behavior.Id}");
                if (last is not null)
                    Log?.Invoke("BehaviorManager.CheckReactionTriggerStrategies: Multiple behaviors switched to in a single basestation tick");
                last = sw;
            }
        }
        return last;
    }

    // fidelity: M10-004, M10-008, M8-012
    /// <summary>
    /// C7 (0x5A3610..0x5A3682): StopAllMotors (0x005a3616); then if AreAnyTracksLocked(0xFF) (0x005a3622) &amp;&amp;
    /// (!(MC+0xB8 || B9 || BA) || MC+0xD4), warn "Some tracks are locked, unlocking them" and CompletelyUnlockAllTracks
    /// (C1, 0x640F84; 0x005a3682). Both are wired here to the stack's motion component, which sends
    /// StopAllMotors and, per locked track index, EnableAnimTracks.
    /// </summary>
    private void StopMotorsForReaction()
    {
        var motion = _context.Robot.Motion;
        motion.StopAllMotors();
        // The "|| MC+0xD4" half (0x005a3642..0x005a3646) reads a flag nothing here ever sets (its writer is SetRunningAndResumeInfo's
        // UpdateRobotPropertiesForReaction, MISSING), so it is always false.
        if (motion.LockedTracks != 0 && (!motion.DirectDriveHoldsAnyTrack || motion.DirectDriveDisabled))
        {
            Log?.Invoke("warning: BehaviorManager.CheckReactionTriggerStrategies: Some tracks are locked, unlocking them");
            motion.CompletelyUnlockAllTracks();
        }
    }

    // fidelity: M10-008
    /// <summary>
    /// SwitchToReactionTrigger (C9, 0x5A25E4..0x5A26DA): a null behaviour fails; info = {current = the behaviour,
    /// trigger = strategy+0x18}; with shouldResumeLast (vslot +0x08) the resume is the existing parked one if there is
    /// one, otherwise the running behaviour, reaction or not; without it the resume is none. Then it calls
    /// <c>SwitchToBehaviorBase</c> (<c>0x005a26da</c>) with that info, which stops the running behaviour and starts the
    /// reaction.
    /// </summary>
    private ReactionSwitch? SwitchToReactionTrigger(ReactionRegistration reg, double nowSec)
    {
        if (reg.Behavior is null) return null;
        string? interrupted;
        IBehavior? resume;
        lock (_gate)
        {
            interrupted = _current?.Id;
            resume = reg.Strategy.ShouldResumeLast ? (_resumeAfterReaction ?? _current) : null;
        }
        // 0x005a26da SwitchToBehaviorBase
        if (!SwitchToBehaviorBaseCore(new RunningInfo(reg.Behavior, resume, reg.Strategy.Trigger), nowSec, BehaviorStopReason.Interrupted))
            return null;
        var sw = new ReactionSwitch(reg.Strategy.Trigger, reg.Behavior.Id, interrupted, resume is not null);
        ReactionTriggered?.Invoke(sw);
        Selected?.Invoke(new BehaviorSelection(reg.Behavior.Id, $"reaction {reg.Strategy.Trigger}") { Replaced = interrupted });
        return sw;
    }

    // fidelity: M10-008
    /// <summary>
    /// The game SetDefaultHeadAndLiftState (C2/C11, handler 0x5A5042, 0x5A1B40): the disable arm sets +8 and +0xC to
    /// FLT_MAX; the enable arm stores head to +8 and lift to +0xC unconditionally and, only when the action list is
    /// empty, queues the compound head/lift action now.
    /// </summary>
    public void SetDefaultHeadAndLiftState(bool enable, float headRad, float liftHeightMm)
    {
        lock (_gate)
        {
            _defaultHeadRad = enable ? headRad : float.MaxValue;
            _defaultLiftMm = enable ? liftHeightMm : float.MaxValue;
        }
        if (!enable) return;
        if (ActionListIsEmpty is not { } empty)
        {
            Log?.Invoke("MISSING: BehaviorManager.SetDefaultHeadAndLiftState: no ActionList is attached, so whether to move now is not known; not moved");
            return;
        }
        if (empty()) QueueHeadAndLift(headRad, liftHeightMm);
    }

    // fidelity: M10-008
    /// <summary>
    /// C2/C11 (0x5A1C24..0x5A1C9C, 0x5A2BB6..0x5A2C3A): the CompoundActionParallel{MoveHeadToAngleAction,
    /// MoveLiftToHeightAction}, list order {head, lift}. Head: <c>Radians(head)</c> with tolerance
    /// <c>Radians(0x3D0EFA35 = 0.0349066)</c> and variability <c>Radians(0)</c>, and the action's constructor defaults
    /// 15 rad/s and 20 rad/s² (MA9). Lift: <c>height</c>, tolerance 5.0f (0x40A00000), variability 0.0f, and the
    /// constructor constants +0x8C = 10.0f and +0x90 = 20.0f. <c>MoveLiftToHeightAction</c> has no defaults for
    /// height or tolerance.
    /// </summary>
    private void QueueHeadAndLift(float headRad, float liftMm)
    {
        // B-ACTIONS batch 2 stand-in for the source's CompoundActionParallel queued at position 0 (20260929-R-ANIM-pre-extraction.md
        // 4.4/4.5, 0x5A1C24..0x5A1C9C, movs r2,#2 at 0x5A1C80; R1 0x0054FA58..0x0054FB16) with children {head, lift}.
        // Until batch 3 builds the compound, the head is queued NOW (key 0) and the lift InParallel (key 1), so the
        // ActionList ticks them on the same list tick in ascending key order, {head, lift}.
        _ = _context.Robot.Motion.SetHeadAngleAsync(new Radians(headRad).Value, CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2, requireCalibration: false);
        _ = _context.Robot.Motion.SetLiftHeightAsync(liftMm, requireCalibration: false, position: QueueActionPosition.InParallel);
    }

    /// <summary>Adds a behaviour. Ids are unique; adding the same id twice replaces the first.</summary>
    public void Add(IBehavior behavior)
    {
        lock (_gate)
        {
            _behaviors.RemoveAll(b => b.Id == behavior.Id);
            _behaviors.Add(behavior);
        }
    }

    /// <summary>The engine's FindBehaviorByID.</summary>
    public IBehavior? Find(string id)
    {
        lock (_gate) return _behaviors.FirstOrDefault(b => b.Id == id);
    }

    /// <summary>The engine's FindBehaviorsByClass.</summary>
    public IReadOnlyList<IBehavior> FindByClass(string behaviorClass)
    {
        lock (_gate) return _behaviors.Where(b => b.Class == behaviorClass).ToList();
    }

    /// <summary>
    /// Scores every runnable behaviour and switches to the best, if it beats what is running. This is the stack's
    /// own ranking (M8-004), not the engine's selection - which is the activity's chooser, reached through
    /// <see cref="Update"/> - and it applies the penalty graph the manager's <see cref="RepetitionPenalty"/> was given
    /// (a flat 1.0 unless one was passed in).
    ///
    /// Returns what was decided and why. Choosing nothing is a normal outcome and is reported like any
    /// other, rather than being silent.
    /// </summary>
    public BehaviorSelection ChooseAndSwitch(double nowSec)
    {
        var scores = new List<(string, double, string)>();
        IBehavior? best = null;
        double bestScore = 0;

        lock (_gate)
        {
            // A reaction runs to its end or until another reaction takes over; ordinary scoring does not
            // replace it (the engine's ChooseNextScoredBehaviorAndSwitch is not entered while a reaction
            // trigger is current). Scoring resumes once the reaction finishes.
            if (_currentReaction is { } reacting && _current is { } reaction)
            {
                var held = new BehaviorSelection(reaction.Id, $"reaction {reacting} is running; scoring waits");
                Selected?.Invoke(held);
                return held;
            }
            foreach (var b in _behaviors)
            {
                if (!b.IsRunnable(_context))
                {
                    scores.Add((b.Id, 0, "not runnable"));
                    continue;
                }
                double raw = b.EvaluateScore(_context);
                double mult = _penalty.For(b.Id, nowSec);
                double score = raw * mult;
                scores.Add((b.Id, score,
                    mult < 1 ? $"{raw:F2} x {mult:F2} repetition penalty" : $"{raw:F2}"));
                if (score > bestScore) { bestScore = score; best = b; }
            }
        }

        if (best is null)
        {
            var none = new BehaviorSelection(null, "no behaviour is runnable and wants to run")
            { Scores = scores };
            Selected?.Invoke(none);
            return none;
        }

        string? replaced;
        BehaviorScope scope;
        lock (_gate)
        {
            if (_current is { } running)
            {
                if (running.Id == best.Id)
                {
                    var same = new BehaviorSelection(best.Id, "already running") { Scores = scores };
                    Selected?.Invoke(same);
                    return same;
                }
                replaced = running.Id;
                StopCurrentLocked(BehaviorStopReason.Interrupted, nowSec);
            }
            else replaced = null;

            // Actually switch. Without this the method would only ever name a winner, and nothing would
            // become current, so no behaviour would ever be recorded as having run and the repetition
            // penalty would never apply.
            scope = new BehaviorScope(_context.Arbiter, _context.Robot.Motion, this);
            _current = best;
            _scope = scope;
            _startedSec = nowSec;
        }

        // Started outside the lock, because a behaviour talks to the robot as it starts.
        _ = best.StartAsync(_context, scope, CancellationToken.None);

        var selection = new BehaviorSelection(best.Id, $"highest score {bestScore:F2}")
        { Scores = scores, Replaced = replaced };
        Selected?.Invoke(selection);
        return selection;
    }

    /// <summary>Starts a behaviour by id, outside scoring. For a caller that wants a specific one.</summary>
    public async Task<bool> StartAsync(string id, double nowSec, CancellationToken cancel = default)
    {
        var b = Find(id);
        return b is not null && await SwitchToBehaviorBase(b, nowSec, cancel).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ BehaviorManager::Update (M8-012)

    /// <summary>
    /// The engine's <c>BehaviorRunningAndResumeInfo</c> (<c>manager+0x1c</c>): the behaviour (+0), the one parked for
    /// resume (+8) and the <see cref="ReactionTrigger"/> (+0x10; null here is <c>NoneTrigger</c>, 0x16). It is what
    /// <c>SwitchToBehaviorBase</c> is handed and what <c>SetRunningAndResumeInfo</c> stores.
    /// </summary>
    // fidelity: M8-012
    private readonly record struct RunningInfo(IBehavior? Current, IBehavior? Resume, ReactionTrigger? Trigger);

    /// <summary>
    /// manager+0x20, the byte <c>BehaviorManager::Update</c> compares against 1 before it calls
    /// <c>EnsureRequestGameIsClear</c> (<c>ldrb r0,[r4,#0x20]; cmp r0,#1</c> 0x005a2f94). The inventory gives the compare
    /// and not the byte's meaning or initial value; it has no effect until <see cref="EnsureRequestGameIsClear"/> is
    /// wired (MISSING: M8-012).
    /// </summary>
    public byte ManagerByte0x20 { get; set; }

    /// <summary><c>EnsureRequestGameIsClear</c> (0x005a2f9e, 0x005a38e6), a cross-layer seam (the request-game component).</summary>
    public Action? EnsureRequestGameIsClear { get; set; }

    /// <summary>manager+0x38: a UI request-game behaviour is wanted (0x005a2fa2). Nothing in this stack sets it.</summary>
    public bool UiGameRequestPending { get; set; }

    /// <summary>manager+0x30: the UI request-game behaviour (a shared_ptr), set by <c>SelectUIRequestGameBehavior</c>.</summary>
    public IBehavior? UiGameBehavior { get; set; }

    /// <summary>
    /// <c>SelectUIRequestGameBehavior</c> 0x005a2faa. Its body is not in the M8 inventory (MISSING: M8-012), so while
    /// <see cref="UiGameRequestPending"/> is set and this is null, <see cref="Update"/> throws.
    /// </summary>
    public Action? SelectUIRequestGameBehavior { get; set; }

    /// <summary>
    /// The byte <c>[[this+0x30]+0x220] = 1</c> the engine writes on the UI game behaviour when the behaviour that was
    /// running when the request was selected had class 0x2e (<c>RequestGameSimple</c>; 0x005a2fde, 0x005a305a).
    /// </summary>
    public Action<IBehavior>? SetUiGameBehaviorRequested { get; set; }

    /// <summary>
    /// <c>SwitchToUIGameRequestBehavior</c> 0x005a309a. Its body is not in the M8 inventory (MISSING: M8-012), so when the
    /// condition for calling it holds and this is null, <see cref="Update"/> throws.
    /// </summary>
    public Action? SwitchToUIGameRequestBehavior { get; set; }

    /// <summary>The seconds clock the stack's stamps use: the context's, else <paramref name="fallback"/>.</summary>
    private double NowSec(double fallback) => _context.ClockSec?.Invoke() ?? fallback;

    /// <summary>
    /// <c>BehaviorManager::Update</c> 0x005a2f68..0x005a31be, in the engine's order:
    /// <list type="number">
    /// <item>the not-initialised guard (0x005a2f70);</item>
    /// <item>the activity's own <c>Update</c> (<c>vtable+0x20</c>, 0x005a2f84), its result ignored;</item>
    /// <item><c>EnsureRequestGameIsClear</c> when <c>robot+0x355</c> (the off-treads state) is non-zero or the byte at +0x20 is
    /// not 1 (0x005a2f8e..0x005a2f9e);</item>
    /// <item>the UI game request when +0x38 is set: <c>SelectUIRequestGameBehavior</c>, clear +0x38, and when the behaviour
    /// that was running had class <c>RequestGameSimple</c> flag the UI game behaviour (0x005a2fa2..0x005a305c);</item>
    /// <item><c>CheckReactionTriggerStrategies</c>, every tick, whether or not a reaction is running (0x005a3062);</item>
    /// <item><c>ChooseNextScoredBehaviorAndSwitch</c> - only when no reaction fired, there is no UI game behaviour
    /// (+0x30 is null) and the running trigger is <c>NoneTrigger</c> (0x005a3068..0x005a3074);</item>
    /// <item><c>SwitchToUIGameRequestBehavior</c> when no reaction fired and the UI game behaviour exists and is not the
    /// running one (0x005a307c..0x005a309a);</item>
    /// <item><c>IBehavior::Update</c> on the running behaviour (0x005a30bc): 1 keeps it; 0 (failure) or 2 (complete) call
    /// <c>FinishCurrentBehavior(behaviour, trigger != NoneTrigger)</c> (0x005a3128/0x005a31a6).</item>
    /// </list>
    /// A behaviour of this stack that ends by itself (its <c>Update</c> returns false) is the engine's status 2.
    /// </summary>
    // fidelity: M8-012
    public void Update(double nowMs, double nowSec)
    {
        // 0x005a2f70 ldrb r0,[r4]; cbz: the engine logs the error and returns without touching a behaviour.
        if (!Initialized) { Log?.Invoke("BehaviorManager.Update.NotInitialized"); return; }

        // 0x005a2f74..0x005a2f84: GetCurrentActivity(), then the activity's vtable+0x20.
        Activity?.Update(nowSec);

        // 0x005a2f8e..0x005a2f9e
        if (_context.Robot.Sensors.OffTreadsState != OffTreadsState.OnTreads || ManagerByte0x20 != 1)
            EnsureRequestGameIsClear?.Invoke();

        // 0x005a2fa2..0x005a305c
        if (UiGameRequestPending)
        {
            var select = SelectUIRequestGameBehavior ?? throw new NotSupportedException(
                "M8-012: BehaviorManager::SelectUIRequestGameBehavior 0x005a2faa has no body in the M8 inventory, and a UI game request is pending.");
            select();
            UiGameRequestPending = false;
            // the running behaviour is loaded after the select and the flag clear (0x005a2fb8..0x005a2fc8) and its class compared with 0x2e
            if (Current?.Class == "RequestGameSimple" && UiGameBehavior is { } uiGame) SetUiGameBehaviorRequested?.Invoke(uiGame);
        }

        // 0x005a3060: CheckReactionTriggerStrategies, unconditionally at this level.
        bool reactionFired = CheckReactions(nowSec) is not null;

        // 0x005a3068..0x005a3078: three gates.
        if (!reactionFired && UiGameBehavior is null && CurrentReactionTrigger is null) ChooseNextScoredBehaviorAndSwitch(nowSec);

        // 0x005a307c..0x005a309a
        if (!reactionFired && UiGameBehavior is { } ui && !ReferenceEquals(ui, Current))
        {
            var toUi = SwitchToUIGameRequestBehavior ?? throw new NotSupportedException(
                "M8-012: BehaviorManager::SwitchToUIGameRequestBehavior 0x005a309a has no body in the M8 inventory, and a UI game behaviour is set.");
            toUi();
        }

        // 0x005a30a6..0x005a30bc: the running behaviour and the running trigger are read after the switches above.
        IBehavior? running;
        ReactionTrigger? trigger;
        lock (_gate) { running = _current; trigger = _currentReaction; }
        if (running is null) return;
        bool keep = running.Update(_context, nowMs);
        if (keep) return;

        // 0x005a30e8 BehaviorManager.Update.BehaviorComplete, then FinishCurrentBehavior(behaviour, trigger != 0x16).
        Log?.Invoke($"BehaviorManager.Update.BehaviorComplete: Behavior '{running.Id}' returned  Status::Complete");
        FinishCurrentBehavior(running, tryToResume: trigger is not null, nowSec);
    }

    /// <summary>
    /// <c>BehaviorManager::ChooseNextScoredBehaviorAndSwitch</c> 0x005a2a20: ask the activity
    /// (<c>GetDesiredActiveBehavior(robot, current)</c> 0x005a2a4a); when the answer differs from the running behaviour,
    /// <c>SwitchToBehaviorBase</c> with the info {desired, none, NoneTrigger} (0x005a2ab4).
    /// </summary>
    // fidelity: M8-012
    private void ChooseNextScoredBehaviorAndSwitch(double nowSec)
    {
        if (Activity is not { } activity) return;
        var current = Current;
        var desired = activity.GetDesiredActiveBehavior(current, nowSec);
        if (ReferenceEquals(desired, current)) return;
        bool started = SwitchToBehaviorBaseCore(new RunningInfo(desired, null, null), nowSec, BehaviorStopReason.Interrupted);
        activity.BehaviorSwitched(desired, started, nowSec);
    }

    // fidelity: M8-012
    /// <summary>
    /// <c>BehaviorManager::SwitchToBehaviorBase</c> 0x005a1e20 for a <paramref name="behavior"/> with no resume behaviour
    /// and no reaction trigger (info {behavior, none, NoneTrigger}). See <see cref="SwitchToBehaviorBaseCore"/>.
    /// </summary>
    public Task<bool> SwitchToBehaviorBase(IBehavior behavior, double nowSec, CancellationToken cancel = default,
                                           BehaviorStopReason stopReason = BehaviorStopReason.Interrupted) =>
        Task.FromResult(SwitchToBehaviorBaseCore(new RunningInfo(behavior, null, null), nowSec, stopReason, cancel));

    /// <summary>
    /// <c>BehaviorManager::SwitchToBehaviorBase(info)</c> 0x005a1e20: stops the current behaviour
    /// (<c>StopAndNullifyCurrentBehavior</c> 0x005a1e6a: <c>Stop()</c> only if the behaviour's running flag +0xa1 is set,
    /// 0x005a2028); a null <c>info.Current</c> goes straight to storing the info; otherwise it calls
    /// <c>IBehavior::IsRunnable(robot)</c> and logs "BehaviorManager.SwitchToBehaviorBase.BehaviorNotRunnable" on false
    /// (0x005a1e76/0x005a1e88) and <b>carries on</b>, then <c>IBehavior::Init</c> (0x005a1e94). A non-zero
    /// <c>Init</c> (<see cref="SteppedBehavior.InitFailed"/>) logs "BehaviorManager.SetCurrentBehavior.InitFailed"
    /// (0x005a1eae) and nulls the info's behaviour (0x005a1efc). In every case <c>SetRunningAndResumeInfo(info)</c>
    /// (0x005a1f12: the behaviour, the resume behaviour and the trigger) and <c>SendDasTransitionMessage</c>
    /// (0x005a1f1c) follow, so a reaction whose <c>Init</c> failed is still the running trigger with its resume
    /// behaviour parked. Returns whether <c>Init</c> succeeded.
    /// </summary>
    // fidelity: M8-012
    private bool SwitchToBehaviorBaseCore(RunningInfo info, double nowSec, BehaviorStopReason stopReason,
                                          CancellationToken cancel = default)
    {
        lock (_gate) StopAndNullifyLocked(stopReason, nowSec);

        var next = info.Current;
        bool initFailed = false;
        BehaviorScope? scope = null;
        if (next is not null)
        {
            if (!next.IsRunnable(_context))
            {
                // 0x005a1e88 "BehaviorManager.SwitchToBehaviorBase.BehaviorNotRunnable" through
                // sVerifyFailedReturnFalse (0x005a1e8e); it then falls through to 0x005a1e94 IBehavior::Init.
                Log?.Invoke($"BehaviorManager.SwitchToBehaviorBase.BehaviorNotRunnable: {next.Id}");
            }

            // Init runs with the manager's current behaviour still null (0x005a1e6a nulled it; SetRunningAndResumeInfo is 0x005a1f12), so a
            // lock the behaviour takes in Init that stops "the current behaviour" finds nothing to stop.
            scope = ScopeFor(next);
            try
            {
                next.StartAsync(_context, scope, cancel).GetAwaiter().GetResult();
                initFailed = next is SteppedBehavior sb && sb.InitFailed;
                if (next is not SteppedBehavior) lock (_gate) _resumeState.Remove(next);       // Init zeroes +0x114 (0x005bcd58)
            }
            catch (OperationCanceledException)
            {
                lock (_gate) _orphanScopes[next] = scope;
                return false;
            }
            if (initFailed)
            {
                // 0x005a1eae "BehaviorManager.SetCurrentBehavior.InitFailed": the behaviour is cleared, the
                // running/resume info and the DAS transition still follow. Nothing calls Stop for it, so whatever its Init
                // took stays held until its next Stop (0x005a2028).
                Log?.Invoke($"BehaviorManager.SetCurrentBehavior.InitFailed: {next.Id}");
                lock (_gate) _orphanScopes[next] = scope;
            }
        }

        SetRunningAndResumeInfo(info with { Current = initFailed ? null : next }, scope, nowSec);
        if (next is not null) DasTransition?.Invoke(next);   // SendDasTransitionMessage 0x005a1f1c
        return !initFailed;
    }

    /// <summary>
    /// <c>BehaviorManager::SetRunningAndResumeInfo</c> 0x005a209e..0x005a22ae: stores the three fields, and also (a) when the trigger
    /// changes sends the game message <c>ReactionTriggerTransition{old, new}</c> (0x005a20ba..0x005a2ee8), (b) calls
    /// <c>UpdateRobotPropertiesForReaction</c> (0x005a2102..0x005a2110, helper 0x005a2f54: <c>robot+0x2c7</c> and the movement component's +0xd4,
    /// from <c>new trigger != NoneTrigger</c>) and (c) on a behaviour change walks the vector at manager+0x78 calling
    /// <c>CubeLightComponent::StopLightAnimAndResumePrevious</c> (0x005a2230..0x005a223e). No inventory row covers (a)..(c); each is a seam that is
    /// reported MISSING while nothing is attached, so this method stores the info and does not claim the rest.
    /// </summary>
    // fidelity: M8-012
    private void SetRunningAndResumeInfo(RunningInfo info, BehaviorScope? scope, double nowSec)
    {
        ReactionTrigger? oldTrigger;
        IBehavior? oldCurrent;
        lock (_gate)
        {
            oldTrigger = _currentReaction;
            oldCurrent = _current;
            _current = info.Current;
            _scope = info.Current is null ? null : scope;
            if (info.Current is not null) _startedSec = nowSec;
            _resumeAfterReaction = info.Resume;
            _currentReaction = info.Trigger;
        }
        if (oldTrigger != info.Trigger)
        {
            if (ReactionTriggerTransitionSender is { } send) send(oldTrigger, info.Trigger);
            else SteppedBehavior.ReportMissing("BehaviorManager::SetRunningAndResumeInfo sends ReactionTriggerTransition{old,new} (0x005a20ba..0x005a2ee8): no game-message sender attached");
            if (UpdateRobotPropertiesForReaction is { } update) update(info.Trigger is not null);
            else SteppedBehavior.ReportMissing("BehaviorManager::UpdateRobotPropertiesForReaction (0x005a2102, 0x005a2f54) writes robot+0x2c7 and MC+0xd4: not built (MC+0xd4 has no writer here)");
        }
        if (!ReferenceEquals(oldCurrent, info.Current))
        {
            if (StopLightAnimsOnBehaviorChange is { } stopLights) stopLights();
            else SteppedBehavior.ReportMissing("BehaviorManager::SetRunningAndResumeInfo stops the cube light animations on a behaviour change (0x005a2230..0x005a223e): not built");
        }
    }

    /// <summary>The <c>ReactionTriggerTransition</c> game message (0x005a2ee8). MISSING: nothing sends it.</summary>
    public Action<ReactionTrigger?, ReactionTrigger?>? ReactionTriggerTransitionSender { get; set; }
    /// <summary><c>UpdateRobotPropertiesForReaction</c> (0x005a2f54); the argument is <c>new trigger != NoneTrigger</c>. MISSING: robot+0x2c7 and MC+0xd4 have no stack counterpart.</summary>
    public Action<bool>? UpdateRobotPropertiesForReaction { get; set; }
    /// <summary>The cube light stop on a behaviour change (0x005a2230..0x005a223e). MISSING.</summary>
    public Action? StopLightAnimsOnBehaviorChange { get; set; }

    // Scopes of behaviours whose Init failed or whose resume failed: the engine never calls Stop for them, so what they took stays held
    // until their next Stop, which is when the scope is released (0x005a2028, 0x005bd08c).
    private readonly Dictionary<IBehavior, BehaviorScope> _orphanScopes = new();
    // Resume state of behaviours that are not SteppedBehaviors: +0x114 and +0x118 (see ResumeNonStepped).
    private readonly Dictionary<IBehavior, (int Count, float SuppressUntil)> _resumeState = new();

    private BehaviorScope ScopeFor(IBehavior b)
    {
        lock (_gate)
        {
            if (_orphanScopes.Remove(b, out var held) && !held.IsDisposed) return held;
            return new BehaviorScope(_context.Arbiter, _context.Robot.Motion, this);
        }
    }

    /// <summary>The DAS transition send (SendDasTransitionMessage 0x005a1f1c), a cross-layer seam.</summary>
    public Action<IBehavior>? DasTransition { get; set; }

    // fidelity: M8-012
    /// <summary>
    /// <c>BehaviorManager::FinishCurrentBehavior(behavior, tryToResume)</c> 0x005a38c4. The second argument is
    /// <b>try to resume</b>: <c>cmp r2,#1</c> and a tail call of <c>TryToResumeBehavior</c> (0x005a38ca, 0x005a38d6); the
    /// manager passes <c>trigger != NoneTrigger</c> (<c>movne r2,#1</c> 0x005a311e..0x005a3122), so a finished reaction
    /// tries to resume what it interrupted and a finished ordinary behaviour does not. With 0: when the UI game
    /// behaviour (+0x30) is set and is <paramref name="behavior"/>, <c>EnsureRequestGameIsClear</c>
    /// (0x005a38dc..0x005a38e6); then <c>SwitchToBehaviorBase</c> with the empty info {none, none, NoneTrigger}
    /// (<c>movs r0,#0x16</c> 0x005a38f4; 0x005a38fe).
    /// </summary>
    public void FinishCurrentBehavior(IBehavior behavior, bool tryToResume, double nowSec)
    {
        if (tryToResume) { TryToResumeBehavior(nowSec); return; }
        if (UiGameBehavior is { } ui && ReferenceEquals(ui, behavior)) EnsureRequestGameIsClear?.Invoke();
        SwitchToBehaviorBaseCore(new RunningInfo(null, null, null), nowSec, BehaviorStopReason.Completed);
    }

    // fidelity: M8-012, M10-008
    /// <summary>
    /// <c>BehaviorManager::TryToResumeBehavior</c> 0x005a2b40, in the engine's order:
    /// <list type="number">
    /// <item>when the stored default head angle (manager+8) is not FLT_MAX (<c>0x5a2e58</c>) and the action list is empty,
    /// log and queue the head/lift restore (C2/C11, 0x005a2b48..0x005a2c3a); the lift value (+0xC) is never tested;
    /// <b>this runs whether or not there is a behaviour to resume</b>;</item>
    /// <item>when the parked resume behaviour (info+8) is null, switch to the empty info (0x005a2c58 <c>cbz r6</c> to
    /// 0x005a2ccc);</item>
    /// <item>else <c>StopAndNullifyCurrentBehavior</c> (0x005a2c5c) and <c>IBehavior::Resume(trigger)</c> on it with the
    /// running trigger (0x005a2c68..0x005a2c76) - <b>no <c>IsRunnable</c> pre-check by the manager</b>; the test is the
    /// behaviour's own, inside <c>ResumeInternal</c> (0x005bda9c..0x005bdab0);</item>
    /// <item>a non-zero result: the resume behaviour is dropped (0x005a2cd6) and <c>SwitchToBehaviorBase</c> takes the empty
    /// info (0x005a2ce2..0x005a2cf4); a zero result: the info becomes {the behaviour, none, NoneTrigger}, the DAS
    /// transition is sent, then <c>SetRunningAndResumeInfo</c> (0x005a2d10..0x005a2d94). <c>Init</c> is not called.</item>
    /// </list>
    /// </summary>
    private void TryToResumeBehavior(double nowSec)
    {
        IBehavior? resume;
        ReactionTrigger? trigger;
        float headRad, liftMm;
        lock (_gate)
        {
            resume = _resumeAfterReaction;
            trigger = _currentReaction;
            headRad = _defaultHeadRad;
            liftMm = _defaultLiftMm;
        }

        // C2/C11: the restore gate reads +8 only (lift is never tested) and requires the action list to be empty.
        if (headRad != float.MaxValue)
        {
            if (ActionListIsEmpty is { } empty)
            {
                if (empty())
                {
                    Log?.Invoke($"BehaviorManager.DefaultHeadAnfLiftState.ResumeBehavior: Resuming behavior and don't have an action, so setting head angle {headRad}, lift height {liftMm}");
                    QueueHeadAndLift(headRad, liftMm);
                }
            }
            else
            {
                Log?.Invoke("MISSING: BehaviorManager.TryToResumeBehavior: no ActionList is attached, so whether the action list is empty (the C2/C11 restore gate) is not known; the head and lift are not restored");
            }
        }

        if (resume is null)
        {
            SwitchToBehaviorBaseCore(new RunningInfo(null, null, null), nowSec, BehaviorStopReason.Completed);
            return;
        }

        lock (_gate) StopAndNullifyLocked(BehaviorStopReason.Completed, nowSec);

        // IBehavior::Resume(trigger) 0x005a2c76: the trigger is the reaction that just ended (info+0x10); null is NoneTrigger.
        var scope = ScopeFor(resume);
        bool failed;
        if (resume is SteppedBehavior stepped)
        {
            stepped.AttachRun(_context, scope);
            failed = stepped.Resume(trigger, nowSec);
        }
        else failed = ResumeNonStepped(resume, trigger, scope, nowSec);

        if (failed)
        {
            // A failed Resume never calls Stop: what it took stays held (0x005a2c7a..0x005a2cd6).
            lock (_gate) _orphanScopes[resume] = scope;
            Log?.Invoke($"BehaviorManager.ResumeFailed: Tried to resume behavior '{resume.Id}', but failed. Clearing current behavior");
            Selected?.Invoke(new BehaviorSelection(resume.Id, "tried to resume, but it would not resume"));
            SwitchToBehaviorBaseCore(new RunningInfo(null, null, null), nowSec, BehaviorStopReason.Completed);
            return;
        }

        SetRunningAndResumeInfoForResume(resume, scope, nowSec);
        Log?.Invoke("BehaviorManager.ResumeBehavior: Successfully resumed");
        Selected?.Invoke(new BehaviorSelection(resume.Id, "resumed after the reaction"));
    }

    private void SetRunningAndResumeInfoForResume(IBehavior resume, BehaviorScope scope, double nowSec)
    {
        DasTransition?.Invoke(resume);                                           // 0x005a2d10..0x005a2d94: DAS first, then the info
        SetRunningAndResumeInfo(new RunningInfo(resume, null, null), scope, nowSec);
    }

    /// <summary>
    /// <c>IBehavior::Resume</c> 0x005bceac for a behaviour that is not a <see cref="SteppedBehavior"/>. The engine's Resume is the same for every
    /// behaviour: the +0x114 counter and the refusal from the second CliffDetected/UnexpectedMovement resume (+0x118 = now + 15.0f, the
    /// <c>TooManyResumesCliffOrMovement</c> emotion event), then <c>ResumeInternal</c>, which is <c>IsRunnableBase</c> and the vtable+0x50 test
    /// (this stack's <c>IsRunnable</c>) and then <c>InitInternal</c> (its <c>StartAsync</c>). The counter, the refusal and the event are done here for it;
    /// the +0x118 test inside <c>IsRunnableBase</c> and the spark lock belong to the behaviour and are MISSING for such a class.
    /// </summary>
    // fidelity: M8-001
    private bool ResumeNonStepped(IBehavior resume, ReactionTrigger? trigger, BehaviorScope scope, double nowSec)
    {
        SteppedBehavior.ReportMissing("IBehavior::Resume for a behaviour that is not a SteppedBehavior: its IsRunnableBase +0x118 test (0x005bd8be) and spark lock are not modelled");
        if (trigger is ReactionTrigger.CliffDetected or ReactionTrigger.UnexpectedMovement)
        {
            (int Count, float SuppressUntil) st;
            lock (_gate) { _resumeState.TryGetValue(resume, out st); st.Count++; _resumeState[resume] = (st.Count, st.SuppressUntil); }
            if (st.Count - 1 >= 1)
            {
                lock (_gate) _resumeState[resume] = (st.Count, (float)nowSec + SteppedBehavior.TooManyResumesCooldownSec);
                _context.Mood?.Trigger(SteppedBehavior.TooManyResumesEventName, nowSec);
                return true;
            }
        }
        if (!resume.IsRunnable(_context)) return true;
        resume.StartAsync(_context, scope, CancellationToken.None).GetAwaiter().GetResult();
        return false;
    }

    /// <summary>The engine's FinishCurrentBehavior for a caller that stops what is running.</summary>
    public void Stop(BehaviorStopReason reason, double nowSec)
    {
        lock (_gate) StopCurrentLocked(reason, nowSec);
    }

    /// <summary>
    /// <c>BehaviorManager::StopAndNullifyCurrentBehavior</c> 0x005a2028: <c>IBehavior::Stop()</c> only if the behaviour's
    /// running flag (+0xa1) is set - a <see cref="SteppedBehavior"/> that already stopped itself, such as one that called
    /// <c>StopWithoutImmediateRepetitionPenalty</c>, is not stopped twice; a behaviour that is not a
    /// <see cref="SteppedBehavior"/> has no flag to read here and is always stopped. Nothing else is released: the scope is
    /// undone by <c>Stop</c> (<c>IBehavior::Stop</c> 0x005bd08c) and, for a behaviour that is not running, is kept for its next Stop.
    /// Then the current pointer is null.
    /// </summary>
    // fidelity: M8-012
    private void StopAndNullifyLocked(BehaviorStopReason reason, double nowSec)
    {
        if (_current is not { } b) return;
        bool running = b is not SteppedBehavior sb || sb.EngineRunning;
        if (running)
        {
            try { b.Stop(reason); }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }

            // IBehavior::Stop 0x005bd11a/0x005bd11e stamps the last-run clock +0x30 on every stop, and
            // EvaluateRepetitionPenalty 0x005beee6 reads it. The +0x108 suppression window is NOT written
            // here: StopWithoutImmediateRepetitionPenalty's only callers are the M7/M15 concrete behaviours
            // (BehaviorPickUpCube 0x005c685c, BehaviorStackBlocks 0x005c991c, BehaviorBuildPyramidBase
            // 0x005dd110); IBehavior::Stop, StopActing, FinishCurrentBehavior and the manager do not call it.
            // fidelity: M8-002
            _penalty.Ran(b.Id, nowSec);
            _scope?.Dispose();
        }
        else if (_scope is { } kept) _orphanScopes[b] = kept;

        _scope = null;
        _current = null;
    }

    /// <summary>
    /// Stops what is running and forgets what was parked for resume and the running trigger: the running info becomes
    /// {none, none, NoneTrigger}, as <c>SwitchToBehaviorBase</c> with the empty info leaves it.
    /// </summary>
    private void StopCurrentLocked(BehaviorStopReason reason, double nowSec)
    {
        _resumeAfterReaction = null;
        StopAndNullifyLocked(reason, nowSec);
        _currentReaction = null;
    }

    /// <summary>How long the running behaviour has been going, in seconds.</summary>
    public double RunningDurationSec(double nowSec)
    {
        lock (_gate) return _current is null ? 0 : nowSec - _startedSec;
    }

    /// <summary>Stops what is running and releases the registered strategies' subscriptions.</summary>
    public void Dispose()
    {
        Stop(BehaviorStopReason.Cancelled, 0);
        List<IReactionTriggerStrategy> strategies;
        lock (_gate)
        {
            strategies = _map.Values.SelectMany(i => i.Entries).Select(r => r.Strategy).Distinct().ToList();
            foreach (var i in _map.Values) i.Entries.Clear();
        }
        foreach (var s in strategies) if (s is IDisposable d) d.Dispose();
        var um = _context.Robot.Sensors.UnexpectedMovement;
        if (ReferenceEquals(um.ReactionTriggerEnabled, _unexpectedMovementGate)) um.ReactionTriggerEnabled = null;
    }
}
