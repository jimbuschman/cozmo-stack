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
/// Chooses which behaviour runs, and switches between them.
///
/// A reconstruction of the shipped <c>BehaviorManager</c>, following what its exported names say it does:
///
/// * <c>ChooseNextScoredBehaviorAndSwitch</c> — selection is by **score**, not a fixed list or a priority
///   ladder. Every runnable behaviour is asked how much it wants to run and the highest wins.
/// * <c>EvaluateRepetitionPenalty</c> — that score is multiplied by a recovery curve so a behaviour that
///   just ran scores zero and climbs back over thirty seconds. This is what produces variety without
///   anything having to forbid a repeat.
/// * <c>CheckReactionTriggerStrategies</c> and the disable locks — the M10 reaction dispatch, from the M10
///   inventory rows C3..C11 and gap pass 1 section 4.
/// * <c>FinishCurrentBehavior</c>, <c>GetCurrentBehavior</c>, <c>FindBehaviorByID</c>,
///   <c>FindBehaviorsByClass</c>.
///
/// Scored selection sits **below** the M7 arbiter, not beside it: the arbiter decides whether autonomy
/// may act at all and keeps the caller above everything, and this decides what autonomy does with its turn.
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

    /// <summary>What is running, if anything.</summary>
    public IBehavior? Current { get { lock (_gate) return _current; } }

    /// <summary>Raised for every selection, including the ones that chose nothing.</summary>
    public event Action<BehaviorSelection>? Selected;

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
    /// <summary>manager+0x1C +0x10: the current reaction trigger (null is NoneTrigger 0x16).</summary>
    private ReactionTrigger? _currentReaction;
    /// <summary>manager+0x1C +8: the behaviour parked for resume.</summary>
    private IBehavior? _resumeAfterReaction;
    /// <summary>manager+0x4C: the sticky gate (C3); 0 in the constructor (0x5A08F8).</summary>
    private bool _reactionGateOpen;
    private bool _warnedNoActionList;
    /// <summary>manager+8 / +0xC (C11); null is the constructor's value, which the rows do not give.</summary>
    private float? _defaultHeadRad, _defaultLiftMm;

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
                lock (_gate) StopCurrentLocked(BehaviorStopReason.Interrupted, _startedSec, keepResume: false);
            }
        }
    }

    // fidelity: M10-004
    /// <summary>The game DisableAllReactionsWithLock (gap1 4e): table 0xC60D40, every trigger, stopCurrent = 1.</summary>
    public void DisableAllReactionsWithLock(string lockName) =>
        DisableReactionsWithLock(lockName, Enumerable.Repeat(true, TriggerCount).ToArray(), stopCurrent: true);

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
    /// BehaviorManager::CheckReactionTriggerStrategies (0x5A3550; C3..C8).
    /// <list type="number">
    /// <item>C3: manager+0x4C |= !ActionList.IsEmpty(); while it is 0 nothing is consulted.</item>
    /// <item>C4: the triggers in ascending map order, skipping one whose lock set is non-empty; each trigger's entries in
    /// JSON order.</item>
    /// <item>C5: with a current reaction, CanInterruptSelf when it is this strategy's trigger, else CanInterruptOther.</item>
    /// <item>C6: ShouldTriggerBehavior(robot, behaviour).</item>
    /// <item>C7: StopAllMotors, then the track-unlock rule.</item>
    /// <item>C8: SwitchToReactionTrigger, logged; the loop does not break, and a second switch in the tick logs
    /// "Multiple behaviors switched to in a single basestation tick".</item>
    /// </list>
    /// The stack's arbiter reaction lock (BehaviorScope.DisableReactions, standing in for the M8 Smart* locks) is kept
    /// ahead of it as the M8 interface. <paramref name="nowSec"/> is BaseStationTimer seconds. Returns the last switch.
    /// </summary>
    public ReactionSwitch? CheckReactions(double nowSec)
    {
        if (_context.Arbiter?.ReactionsDisabled == true) return null;

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

    // fidelity: M10-008
    /// <summary>
    /// C7 (0x5A3610..0x5A3682): StopAllMotors; then if AreAnyTracksLocked(0xFF) &amp;&amp; (!(MC+0xB8 || B9 || BA) || MC+0xD4),
    /// warn "Some tracks are locked, unlocking them" and CompletelyUnlockAllTracks.
    /// MISSING: CompletelyUnlockAllTracks' body (which locks it clears and what it sends) is not in any inventory row, so
    /// the unlock is not performed; it is logged.
    /// </summary>
    private void StopMotorsForReaction()
    {
        var motion = _context.Robot.Motion;
        motion.StopAllMotors();
        if (motion.LockedTracks != 0 && (!motion.DirectDriveHoldsAnyTrack || motion.DirectDriveDisabled))
        {
            Log?.Invoke("warning: BehaviorManager.CheckReactionTriggerStrategies: Some tracks are locked, unlocking them");
            Log?.Invoke("MISSING: MovementComponent::CompletelyUnlockAllTracks is not in the rows; the tracks stay locked");
        }
    }

    // fidelity: M10-008
    /// <summary>
    /// SwitchToReactionTrigger (C9, 0x5A25E4..0x5A26DA): a null behaviour fails; info = {current = the behaviour,
    /// trigger = strategy+0x18}; with shouldResumeLast (vslot +0x08) the resume is the existing parked one if there is
    /// one, otherwise the running behaviour, reaction or not; without it the resume is none. Then SwitchToBehaviorBase
    /// (M8, not in the rows): here the running behaviour is stopped and the reaction started.
    /// </summary>
    private ReactionSwitch? SwitchToReactionTrigger(ReactionRegistration reg, double nowSec)
    {
        if (reg.Behavior is null) return null;
        string? interrupted;
        bool willResume;
        BehaviorScope scope;
        lock (_gate)
        {
            interrupted = _current?.Id;
            var resume = reg.Strategy.ShouldResumeLast ? (_resumeAfterReaction ?? _current) : null;
            if (_current is not null) StopCurrentLocked(BehaviorStopReason.Interrupted, nowSec, keepResume: true);
            _resumeAfterReaction = resume;
            willResume = resume is not null;
            scope = new BehaviorScope(_context.Arbiter);
            _current = reg.Behavior;
            _scope = scope;
            _startedSec = nowSec;
            _currentReaction = reg.Strategy.Trigger;
        }
        _ = reg.Behavior.StartAsync(_context, scope, CancellationToken.None);
        var sw = new ReactionSwitch(reg.Strategy.Trigger, reg.Behavior.Id, interrupted, willResume);
        ReactionTriggered?.Invoke(sw);
        Selected?.Invoke(new BehaviorSelection(reg.Behavior.Id, $"reaction {reg.Strategy.Trigger}") { Replaced = interrupted });
        return sw;
    }

    // fidelity: M10-008
    /// <summary>
    /// The game SetDefaultHeadAndLiftState (C11, handler 0x5A5042): enable stores both (0x5A1BCC/0x5A1BD0) and moves now if
    /// the action list is empty; disable stores FLT_MAX (0x5A1D1E..0x5A1D26). These are the values TryToResume restores.
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

    /// <summary>
    /// C11: CompoundActionParallel{MoveHeadToAngleAction(tol 0.0349066), MoveLiftToHeightAction}; the head action with its
    /// constructor's 15 rad/s and 20 rad/s² (M4 MA9).
    /// MISSING: MoveLiftToHeightAction's engine-internal constructor defaults (speed, acceleration, tolerance) are not in
    /// the rows; the stack's lift action is used with its own defaults.
    /// </summary>
    private void QueueHeadAndLift(float headRad, float liftMm)
    {
        _ = _context.Robot.Motion.SetHeadAngleAsync(headRad, CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2, requireCalibration: false);
        _ = _context.Robot.Motion.SetLiftHeightAsync(liftMm, requireCalibration: false);
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
    /// Scores every runnable behaviour and switches to the best, if it beats what is running.
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
                StopCurrentLocked(BehaviorStopReason.Interrupted, nowSec, keepResume: false);
            }
            else replaced = null;

            // Actually switch. Without this the method would only ever name a winner, and nothing would
            // become current, so no behaviour would ever be recorded as having run and the repetition
            // penalty would never apply.
            scope = new BehaviorScope(_context.Arbiter);
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
        if (b is null || !b.IsRunnable(_context)) return false;

        BehaviorScope scope;
        lock (_gate)
        {
            if (_current is not null) StopCurrentLocked(BehaviorStopReason.Interrupted, nowSec, keepResume: false);
            scope = new BehaviorScope(_context.Arbiter);
            _current = b;
            _scope = scope;
            _startedSec = nowSec;
        }
        try
        {
            await b.StartAsync(_context, scope, cancel).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            Stop(BehaviorStopReason.Cancelled, nowSec);
            return false;
        }
    }

    // fidelity: M10-008
    /// <summary>
    /// Advances the running behaviour; stops it when it says it has finished. When a behaviour is parked (C9), the
    /// engine's TryToResumeBehavior (0x5A2BB8..0x5A2C3A) restores the head and lift from SetDefaultHeadAndLiftState's
    /// values (C11) and resumes it. IBehavior::Resume and its failure path are M8 (not in the rows); here the parked
    /// behaviour is started again when it is runnable.
    /// MISSING (C11): the constructor values of manager+8/+0xC, and whether the restore is skipped when they are FLT_MAX,
    /// are not in the rows; the restore runs only for values SetDefaultHeadAndLiftState(enable) stored.
    /// </summary>
    public void Update(double nowMs, double nowSec)
    {
        IBehavior? current;
        lock (_gate) current = _current;
        if (current is null) return;
        if (current.Update(_context, nowMs)) return;

        IBehavior? resume;
        float? headRad, liftMm;
        lock (_gate)
        {
            resume = _resumeAfterReaction;
            headRad = _defaultHeadRad;
            liftMm = _defaultLiftMm;
            StopCurrentLocked(BehaviorStopReason.Completed, nowSec, keepResume: false);
        }
        if (resume is null) return;
        if (headRad is { } h && liftMm is { } l && h != float.MaxValue) QueueHeadAndLift(h, l);
        else Log?.Invoke("MISSING: BehaviorManager.TryToResumeBehavior: no SetDefaultHeadAndLiftState values; the head and lift are not restored");

        if (!resume.IsRunnable(_context))
        {
            Selected?.Invoke(new BehaviorSelection(resume.Id, "tried to resume, but it would not run"));
            return;
        }
        BehaviorScope resumeScope;
        lock (_gate)
        {
            resumeScope = new BehaviorScope(_context.Arbiter);
            _current = resume;
            _scope = resumeScope;
            _startedSec = nowSec;
        }
        _ = resume.StartAsync(_context, resumeScope, CancellationToken.None);
        Selected?.Invoke(new BehaviorSelection(resume.Id, "resumed after the reaction"));
    }

    /// <summary>The engine's FinishCurrentBehavior.</summary>
    public void Stop(BehaviorStopReason reason, double nowSec)
    {
        lock (_gate) StopCurrentLocked(reason, nowSec, keepResume: false);
    }

    private void StopCurrentLocked(BehaviorStopReason reason, double nowSec, bool keepResume)
    {
        if (!keepResume) _resumeAfterReaction = null;
        if (_current is not { } b) { _currentReaction = null; return; }
        try { b.Stop(reason); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }

        // Only a behaviour that ran to completion is penalised for repeating. The engine's
        // StopWithoutImmediateRepetitionPenalty exists for exactly the interrupted case.
        if (reason == BehaviorStopReason.Completed) _penalty.Ran(b.Id, nowSec);

        _scope?.Dispose();
        _scope = null;
        _current = null;
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
