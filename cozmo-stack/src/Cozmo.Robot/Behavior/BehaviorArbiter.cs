namespace Cozmo.Robot.Behavior;

/// <summary>
/// Who is asking for an animation. Higher wins; a request never displaces an equal or higher one.
/// </summary>
public enum BehaviorPriority
{
    /// <summary>Keeps an otherwise still robot alive. Yields to everything.</summary>
    Idle = 0,
    /// <summary>A response to something that happened. Yields to the caller.</summary>
    Reaction = 1,
    /// <summary>The application asked for it directly. Wins.</summary>
    Caller = 2,
}

/// <summary>What happened to a request, and why. Every decision is explainable.</summary>
public enum BehaviorOutcome
{
    /// <summary>It started.</summary>
    Played,
    /// <summary>It started, ending something of lower priority.</summary>
    Interrupted,
    /// <summary>Something of equal or higher priority was running, so it did not start.</summary>
    Suppressed,
    /// <summary>Autonomous behaviour is switched off, and this was not a caller request.</summary>
    Disabled,
    /// <summary>Its trigger resolved to no animation.</summary>
    Unresolved,
    /// <summary>The scheduler refused it.</summary>
    Refused,
    /// <summary>It was asked for again inside its own cooldown.</summary>
    OnCooldown,
}

/// <summary>A complete account of one request: what was asked, what happened, and why.</summary>
public sealed record BehaviorDecision(
    BehaviorPriority Priority,
    BehaviorOutcome Outcome,
    string Reason)
{
    public ReactionTrigger? Reaction { get; init; }
    public AnimationTrigger? Animation { get; init; }
    public string? Group { get; init; }
    public string? Clip { get; init; }
    /// <summary>What was running when this was asked for, when something was.</summary>
    public string? Displaced { get; init; }

    public bool Started => Outcome is BehaviorOutcome.Played or BehaviorOutcome.Interrupted;

    public override string ToString()
    {
        var what = Reaction is { } r ? r.ToString() : Animation?.ToString() ?? "(clip)";
        var got = Clip is null ? "" : $" -> {Group} -> {Clip}";
        return $"{Priority} {what}{got}: {Outcome} ({Reason})";
    }
}

/// <summary>
/// Decides who gets to animate.
///
/// The hierarchy is explicit and is the whole point of this class: **caller beats reaction beats idle**.
/// An application that drives the robot directly must never find itself fighting the idle system, and a
/// reaction must never stamp on something the application deliberately started.
///
/// The arbiter owns the decision but not the playing: it says yes or no and explains itself, and the
/// caller does the work. That keeps M5's scheduler the only thing that owns timing.
/// </summary>
public sealed class BehaviorArbiter
{
    private readonly object _gate = new();
    private readonly Dictionary<ReactionTrigger, DateTime> _lastFired = new();
    private BehaviorPriority? _running;
    private string? _runningWhat;

    /// <summary>
    /// Whether reactions and idle behaviour may run at all. Off by default: autonomy is something a
    /// caller opts into, not something that starts happening because a robot was connected.
    /// </summary>
    public bool AutonomyEnabled { get; set; }

    /// <summary>
    /// How long a given reaction is suppressed after firing. Stops a flapping sensor — a cliff sensor at
    /// the edge of a table, say — from retriggering the same animation continuously.
    /// </summary>
    public TimeSpan ReactionCooldown { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>What is running now, if anything.</summary>
    public BehaviorPriority? Running { get { lock (_gate) return _running; } }

    /// <summary>Raised for every decision, including the ones that played nothing.</summary>
    public event Action<BehaviorDecision>? Decided;

    /// <summary>
    /// Asks to start something. Returns the decision; the caller plays it only when
    /// <see cref="BehaviorDecision.Started"/>.
    /// </summary>
    public BehaviorDecision Request(BehaviorPriority priority, string what,
                                    ReactionTrigger? reaction = null, DateTime? now = null)
    {
        var at = now ?? DateTime.UtcNow;
        BehaviorDecision decision;
        lock (_gate)
        {
            if (priority != BehaviorPriority.Caller && !AutonomyEnabled)
            {
                decision = new BehaviorDecision(priority, BehaviorOutcome.Disabled,
                    "autonomous behaviour is switched off") { Reaction = reaction };
            }
            else if (reaction is { } r && _lastFired.TryGetValue(r, out var last)
                     && at - last < ReactionCooldown)
            {
                decision = new BehaviorDecision(priority, BehaviorOutcome.OnCooldown,
                    $"fired {(at - last).TotalSeconds:F1}s ago, cooldown is {ReactionCooldown.TotalSeconds:F0}s")
                { Reaction = reaction };
            }
            else if (_running is { } current && current >= priority)
            {
                decision = new BehaviorDecision(priority, BehaviorOutcome.Suppressed,
                    $"{current} '{_runningWhat}' is already running")
                { Reaction = reaction, Displaced = _runningWhat };
            }
            else
            {
                bool interrupting = _running is not null;
                var displaced = _runningWhat;
                _running = priority;
                _runningWhat = what;
                if (reaction is { } fired) _lastFired[fired] = at;
                decision = new BehaviorDecision(priority,
                    interrupting ? BehaviorOutcome.Interrupted : BehaviorOutcome.Played,
                    interrupting ? $"took over from {displaced}" : "nothing was running")
                { Reaction = reaction, Displaced = interrupting ? displaced : null };
            }
        }
        Decided?.Invoke(decision);
        return decision;
    }

    /// <summary>
    /// Records that whatever was running has finished. Ignores a report from a lower priority than the
    /// one running, so a late completion cannot clear something that already took over.
    /// </summary>
    public void Finished(BehaviorPriority priority)
    {
        lock (_gate)
        {
            if (_running is null || _running > priority) return;
            _running = null;
            _runningWhat = null;
        }
    }

    /// <summary>Reports a decision that was made elsewhere, so it appears in the trace.</summary>
    public void Report(BehaviorDecision decision) => Decided?.Invoke(decision);

    /// <summary>Forgets every cooldown. For tests and for a deliberate restart.</summary>
    public void ResetCooldowns() { lock (_gate) _lastFired.Clear(); }
}
