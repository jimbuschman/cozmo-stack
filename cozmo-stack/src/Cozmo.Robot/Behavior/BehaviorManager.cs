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
/// * <c>CheckReactionTriggerStrategies</c> and <c>DisableReactionsWithLock</c> — reactions preempt the
///   scored behaviour, unless the running behaviour has taken a lock against them.
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
    }

    /// <summary>Every behaviour this manager knows.</summary>
    public IReadOnlyList<IBehavior> Behaviors => _behaviors;

    /// <summary>What is running, if anything.</summary>
    public IBehavior? Current { get { lock (_gate) return _current; } }

    /// <summary>Raised for every selection, including the ones that chose nothing.</summary>
    public event Action<BehaviorSelection>? Selected;

    // ------------------------------------------------------------------ reactions (M10)

    /// <summary>One shipped reaction: the strategy that decides it, the behaviour it runs, and whether the
    /// behaviour it interrupted is resumed afterwards (<c>genericStrategyParams.shouldResumeLast</c>).</summary>
    public sealed record ReactionRegistration(IReactionTriggerStrategy Strategy, IBehavior Behavior, bool ResumeLast);

    /// <summary>What happened when a reaction fired.</summary>
    public sealed record ReactionSwitch(ReactionTrigger Trigger, string Behavior, string? Interrupted, bool WillResume);

    private readonly List<ReactionRegistration> _reactions = new();
    private readonly HashSet<ReactionTrigger> _disabledTriggers = new();
    private ReactionTrigger? _currentReaction;
    private IBehavior? _resumeAfterReaction;

    /// <summary>The engine's reaction trigger map, one registration per trigger this stack can drive.</summary>
    public IReadOnlyList<ReactionRegistration> Reactions { get { lock (_gate) return _reactions.ToList(); } }

    /// <summary>The reaction that is running, if the current behaviour was started by one (<c>GetCurrentReactionTrigger</c>).</summary>
    public ReactionTrigger? CurrentReactionTrigger { get { lock (_gate) return _currentReaction; } }

    /// <summary>Raised when a reaction takes over.</summary>
    /// <summary>
    /// The repetition history this manager records on completion, shared with every chooser so a behaviour's
    /// recovery curve is the behaviour's and not one copy per activity.
    /// </summary>
    public RepetitionPenalty Penalty => _penalty;

    public event Action<ReactionSwitch>? ReactionTriggered;

    /// <summary>
    /// A strategy wanted to trigger but its behaviour could not start: the engine's
    /// "Trigger strategy %s tried to trigger behavior %s, but init failed".
    /// </summary>
    public event Action<string>? ReactionRefused;

    /// <summary>
    /// Registers a reaction. The engine builds one strategy per entry of <c>reactionTrigger_behavior_map.json</c>
    /// and looks the behaviour up by id; here both are handed in. A second registration for the same trigger
    /// replaces the first.
    /// </summary>
    public void AddReaction(IReactionTriggerStrategy strategy, IBehavior behavior, bool resumeLast = false)
    {
        List<ReactionRegistration> replaced;
        lock (_gate)
        {
            replaced = _reactions.Where(r => r.Strategy.Trigger == strategy.Trigger).ToList();
            _reactions.RemoveAll(r => r.Strategy.Trigger == strategy.Trigger);
            _reactions.Add(new ReactionRegistration(strategy, behavior, resumeLast));
            _behaviors.RemoveAll(b => b.Id == behavior.Id);
        }
        // A registered strategy is owned by the manager: the message-subscribing ones (latched events, cube
        // moved, object position) hold a robot subscription that must be released when they are replaced.
        foreach (var r in replaced)
            if (!ReferenceEquals(r.Strategy, strategy) && r.Strategy is IDisposable d) d.Dispose();
    }

    /// <summary>The engine's per-trigger enable (<c>IsReactionTriggerEnabled</c>). Every trigger starts enabled.</summary>
    public void SetTriggerEnabled(ReactionTrigger trigger, bool enabled)
    {
        lock (_gate) { if (enabled) _disabledTriggers.Remove(trigger); else _disabledTriggers.Add(trigger); }
    }

    public bool IsTriggerEnabled(ReactionTrigger trigger) { lock (_gate) return !_disabledTriggers.Contains(trigger); }

    /// <summary>
    /// The engine's <c>BehaviorManager::CheckReactionTriggerStrategies</c> (0x005A3550): unless a behaviour
    /// holds the reaction lock, ask every enabled strategy whether it should fire; the first that does, and
    /// whose behaviour is runnable, takes over from whatever is running (<c>SwitchToReactionTrigger</c>). The
    /// engine stops all motors and unlocks the tracks before switching; stopping the interrupted behaviour does
    /// that here. Returns the switch, or null when nothing fired.
    /// </summary>
    public ReactionSwitch? CheckReactions(double nowSec)
    {
        if (_context.Arbiter?.ReactionsDisabled == true) return null;

        List<ReactionRegistration> regs;
        ReactionTrigger? current;
        lock (_gate) { regs = _reactions.ToList(); current = _currentReaction; }

        foreach (var reg in regs)
        {
            if (!IsTriggerEnabled(reg.Strategy.Trigger)) continue;
            // The engine's order (CheckReactionTriggerStrategies 0x005A3550) is ShouldTriggerBehavior(robot,
            // behavior) first — the behaviour is an argument, so a strategy fills in its target there — and the
            // behaviour's runnability only afterwards, inside SwitchToReactionTrigger ("...but init failed").
            // A strategy that produces a target follows that order through ITargetPreparingStrategy. A latched
            // strategy keeps runnable-before-consume, so a cliff or calibration report seen while its behaviour
            // cannot run is not thrown away (this stack's latches are the strategy's own state, not the
            // engine's message queue).
            if (reg.Strategy is ITargetPreparingStrategy prep)
            {
                if (!prep.PrepareTarget(_context, current, nowSec)) continue;
                if (!reg.Behavior.IsRunnable(_context))
                {
                    prep.AbandonTarget();
                    ReactionRefused?.Invoke($"Trigger strategy {reg.Strategy.Trigger} tried to trigger behavior {reg.Behavior.Id}, but init failed");
                    continue;
                }
                prep.CommitTarget();
            }
            else
            {
                if (!reg.Behavior.IsRunnable(_context)) continue;
                if (!reg.Strategy.ShouldTrigger(_context, current, nowSec)) continue;
            }

            string? interrupted;
            bool willResume;
            BehaviorScope scope;
            lock (_gate)
            {
                if (_current is { } running && running.Id == reg.Behavior.Id) return null;   // already reacting to this
                interrupted = _current?.Id;
                // Only a non-reaction behaviour is resumed; a reaction interrupted by a reaction is not.
                willResume = reg.ResumeLast && _current is not null && _currentReaction is null;
                var interruptedBehavior = _current;
                if (_current is not null) StopCurrentLocked(BehaviorStopReason.Interrupted, nowSec);   // clears any parked resume
                _resumeAfterReaction = willResume ? interruptedBehavior : null;
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
        return null;
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
                StopCurrentLocked(BehaviorStopReason.Interrupted, nowSec);
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
            if (_current is not null) StopCurrentLocked(BehaviorStopReason.Interrupted, nowSec);
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

    /// <summary>Advances the running behaviour; stops it when it says it has finished.</summary>
    public void Update(double nowMs, double nowSec)
    {
        IBehavior? current;
        lock (_gate) current = _current;
        if (current is null) return;
        if (current.Update(_context, nowMs)) return;

        IBehavior? resume;
        lock (_gate)
        {
            resume = _resumeAfterReaction;
            _resumeAfterReaction = null;
            StopCurrentLocked(BehaviorStopReason.Completed, nowSec);
        }
        // The engine's "resume last": a reaction whose map entry says shouldResumeLast restarts the
        // behaviour it interrupted, if that behaviour still wants to run.
        if (resume is not null && resume.IsRunnable(_context))
        {
            BehaviorScope scope;
            lock (_gate)
            {
                scope = new BehaviorScope(_context.Arbiter);
                _current = resume;
                _scope = scope;
                _startedSec = nowSec;
            }
            _ = resume.StartAsync(_context, scope, CancellationToken.None);
            Selected?.Invoke(new BehaviorSelection(resume.Id, "resumed after the reaction"));
        }
    }

    /// <summary>The engine's FinishCurrentBehavior.</summary>
    public void Stop(BehaviorStopReason reason, double nowSec)
    {
        lock (_gate) StopCurrentLocked(reason, nowSec);
    }

    private void StopCurrentLocked(BehaviorStopReason reason, double nowSec)
    {
        if (_current is not { } b) return;
        try { b.Stop(reason); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }

        // Only a behaviour that ran to completion is penalised for repeating. The engine's
        // StopWithoutImmediateRepetitionPenalty exists for exactly the interrupted case.
        if (reason == BehaviorStopReason.Completed) _penalty.Ran(b.Id, nowSec);

        _scope?.Dispose();
        _scope = null;
        _current = null;
        _currentReaction = null;
        // A behaviour parked for "resume last" is only resumed by the reaction that interrupted it finishing
        // (Update reads it before calling here). Any other stop, external or a replacement, drops it, so a
        // later completion cannot restart a behaviour nobody interrupted.
        _resumeAfterReaction = null;
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
        List<ReactionRegistration> regs;
        lock (_gate) { regs = _reactions.ToList(); _reactions.Clear(); }
        foreach (var r in regs) if (r.Strategy is IDisposable d) d.Dispose();
    }
}
