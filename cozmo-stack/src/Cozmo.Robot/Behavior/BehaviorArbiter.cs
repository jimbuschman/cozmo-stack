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
    /// Whether a <see cref="BehaviorManager"/> is dispatching reactions over this arbiter.
    ///
    /// The engine has one reaction dispatcher (<c>BehaviorManager::CheckReactionTriggerStrategies</c>). This
    /// stack grew a second one first (the M7 <c>ReactiveBehavior</c>), and every source-backed reaction now
    /// lives under the manager, so the older dispatcher stands down when a manager shares its arbiter rather
    /// than firing the same trigger a second time.
    /// </summary>
    public bool ManagerDispatchesReactions { get; set; }

    /// <summary>
    /// How long a given reaction is suppressed after firing. Stops a flapping sensor — a cliff sensor at
    /// the edge of a table, say — from retriggering the same animation continuously.
    ///
    /// No blanket cooldown: the default is zero, because the engine has none. The shipped
    /// <c>reactionTrigger_behavior_map.json</c>
    /// gives no cooldown to <c>CliffDetected</c>, <c>RobotPickedUp</c>, <c>PlacedOnCharger</c> or
    /// <c>RobotFalling</c>; its cooldowns exist only where a trigger's strategy config names one (60 s for
    /// minor frustration, 180–300 s for the fist-bump objectives). The engine avoids re-triggering these
    /// four by other means — the reaction locks its behaviour with <c>SmartDisableReactionsWithLock</c>
    /// while it runs, and the trigger strategies watch transitions, not levels.
    /// </summary>
    public TimeSpan ReactionCooldown { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// A cooldown for one trigger, where the shipped strategy config names one - 60 s on minor
    /// frustration, 180 to 300 s on the fist-bump objectives. A trigger with no entry has no cooldown,
    /// which is the case for every reaction this stack drives, so the table is empty until something
    /// loads one in.
    /// </summary>
    public Dictionary<ReactionTrigger, TimeSpan> TriggerCooldowns { get; } = new();

    private TimeSpan CooldownFor(ReactionTrigger t) =>
        TriggerCooldowns.TryGetValue(t, out var c) ? c : ReactionCooldown;

    /// <summary>
    /// Whether a direct caller animation counts as the caller holding the floor.
    ///
    /// The arbiter only ever saw requests made through itself, so an application calling
    /// <c>robot.Animations.Play(...)</c> — the ordinary public path — was invisible to it and a reaction
    /// would happily replace that animation. This closes that hole without changing frozen M5: the arbiter
    /// asks the scheduler whether an animation is running that it did not itself start, and treats one as
    /// a caller.
    /// </summary>
    public Func<bool>? CallerAnimationRunning { get; set; }

    /// <summary>What is running now, if anything, including a caller animation the arbiter did not start.</summary>
    public BehaviorPriority? Running
    {
        get
        {
            lock (_gate)
            {
                if (_running is { } r) return r;
            }
            return CallerAnimationRunning?.Invoke() == true ? BehaviorPriority.Caller : null;
        }
    }

    /// <summary>
    /// Behaviours currently holding the reaction lock, the engine's SmartDisableReactionsWithLock. While
    /// any is held, reactions are suppressed; releasing the last one restores them.
    /// </summary>
    private readonly HashSet<object> _reactionLocks = new();

    /// <summary>Whether anything is currently holding reactions off.</summary>
    public bool ReactionsDisabled { get { lock (_gate) return _reactionLocks.Count > 0; } }

    /// <summary>Takes a reaction lock on behalf of <paramref name="owner"/>.</summary>
    public void DisableReactions(object owner)
    {
        lock (_gate) _reactionLocks.Add(owner);
    }

    /// <summary>Releases that owner's reaction lock.</summary>
    public void EnableReactions(object owner)
    {
        lock (_gate) _reactionLocks.Remove(owner);
    }

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

        // Asked before taking the lock, because the callback reaches into the scheduler.
        bool callerAnimating = priority != BehaviorPriority.Caller
                               && CallerAnimationRunning?.Invoke() == true;

        lock (_gate)
        {
            if (priority == BehaviorPriority.Reaction && _reactionLocks.Count > 0)
            {
                decision = new BehaviorDecision(priority, BehaviorOutcome.Suppressed,
                    $"{_reactionLocks.Count} reaction lock(s) are held")
                { Reaction = reaction };
            }
            else if (callerAnimating && _running is null)
            {
                // An animation the application started directly. It outranks everything autonomous, and
                // the arbiter never started it, so there is nothing of its own to compare against.
                decision = new BehaviorDecision(priority, BehaviorOutcome.Suppressed,
                    "a caller animation is already running")
                { Reaction = reaction, Displaced = "caller animation" };
            }
            else if (priority != BehaviorPriority.Caller && !AutonomyEnabled)
            {
                decision = new BehaviorDecision(priority, BehaviorOutcome.Disabled,
                    "autonomous behaviour is switched off") { Reaction = reaction };
            }
            else if (reaction is { } r && CooldownFor(r) > TimeSpan.Zero
                     && _lastFired.TryGetValue(r, out var last) && at - last < CooldownFor(r))
            {
                decision = new BehaviorDecision(priority, BehaviorOutcome.OnCooldown,
                    $"fired {(at - last).TotalSeconds:F1}s ago, cooldown is {CooldownFor(r).TotalSeconds:F0}s")
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
