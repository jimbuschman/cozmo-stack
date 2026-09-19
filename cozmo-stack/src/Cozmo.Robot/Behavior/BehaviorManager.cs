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
    }

    /// <summary>Every behaviour this manager knows.</summary>
    public IReadOnlyList<IBehavior> Behaviors => _behaviors;

    /// <summary>What is running, if anything.</summary>
    public IBehavior? Current { get { lock (_gate) return _current; } }

    /// <summary>Raised for every selection, including the ones that chose nothing.</summary>
    public event Action<BehaviorSelection>? Selected;

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
        if (!current.Update(_context, nowMs)) Stop(BehaviorStopReason.Completed, nowSec);
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
    }

    /// <summary>How long the running behaviour has been going, in seconds.</summary>
    public double RunningDurationSec(double nowSec)
    {
        lock (_gate) return _current is null ? 0 : nowSec - _startedSec;
    }

    public void Dispose() => Stop(BehaviorStopReason.Cancelled, 0);
}
