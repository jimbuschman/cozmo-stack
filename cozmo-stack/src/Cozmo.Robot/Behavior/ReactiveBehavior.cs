using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// Turns the robot's own reported state into reactions.
///
/// Only the transitions M4 already reports are watched, and only the reactions
/// <see cref="ReactionTable"/> maps are played. Nothing is inferred from timing, nothing is simulated,
/// and a transition with no mapped reaction is reported as such rather than quietly ignored.
///
/// Everything goes through <see cref="BehaviorArbiter"/>, so a reaction never stamps on something the
/// application started, and every decision — including the ones that played nothing — is traceable.
///
/// This layer is **off until <see cref="BehaviorArbiter.AutonomyEnabled"/> is set**. Connecting to a
/// robot does not make it start moving on its own.
/// </summary>
public sealed class ReactiveBehavior : IDisposable
{
    private readonly CozmoRobot _robot;
    private readonly AnimationTriggerMap _map;
    private readonly ReactionTable _table;
    private readonly Random _random;
    private bool _subscribed;

    public ReactiveBehavior(CozmoRobot robot, AnimationTriggerMap map,
                            ReactionTable? table = null, BehaviorArbiter? arbiter = null,
                            Random? random = null)
    {
        _robot = robot;
        _map = map;
        _table = table ?? ReactionTable.Default;
        Arbiter = arbiter ?? new BehaviorArbiter();
        _random = random ?? new Random();
    }

    /// <summary>Decides what is allowed to run. Shared with the idle layer.</summary>
    public BehaviorArbiter Arbiter { get; }

    /// <summary>The mood selection runs under, when one is set.</summary>
    public string? Mood { get; set; }

    /// <summary>Raised for every reaction considered, played or not.</summary>
    public event Action<BehaviorDecision>? Reacted;

    /// <summary>Starts watching the robot's state. Idempotent.</summary>
    public void Start()
    {
        if (_subscribed) return;
        _subscribed = true;
        _robot.Sensors.CliffDetected += OnCliff;
        _robot.Sensors.PickedUpChanged += OnPickedUp;
        _robot.Sensors.OnChargerChanged += OnCharger;
    }

    /// <summary>Stops watching. Anything already playing is left to finish.</summary>
    public void Stop()
    {
        if (!_subscribed) return;
        _subscribed = false;
        _robot.Sensors.CliffDetected -= OnCliff;
        _robot.Sensors.PickedUpChanged -= OnPickedUp;
        _robot.Sensors.OnChargerChanged -= OnCharger;
    }

    private void OnCliff(CliffReport report) => Fire(ReactionTrigger.CliffDetected);

    private void OnPickedUp(bool picked)
    {
        // Only the pick-up has a shipped reaction. Being put down is a real transition and is reported,
        // but the shipped ReactionTrigger set has no "put down" member, so nothing is played for it
        // rather than something being chosen to fill the gap.
        if (picked) Fire(ReactionTrigger.RobotPickedUp);
        else Report(new BehaviorDecision(BehaviorPriority.Reaction, BehaviorOutcome.Unresolved,
            "put down: the shipped ReactionTrigger set has no member for it"));
    }

    private void OnCharger(bool onCharger)
    {
        if (onCharger) Fire(ReactionTrigger.PlacedOnCharger);
        else Report(new BehaviorDecision(BehaviorPriority.Reaction, BehaviorOutcome.Unresolved,
            "off charger: the shipped ReactionTrigger set has no member for it"));
    }

    /// <summary>
    /// Runs one reaction through the whole chain, recording every step. Public so a caller can raise a
    /// reaction the sensors cannot yet detect, and so tests can drive it without a robot.
    /// </summary>
    public BehaviorDecision Fire(ReactionTrigger trigger, DateTime? now = null)
    {
        var entry = _table.For(trigger);
        if (entry is null)
            return Report(new BehaviorDecision(BehaviorPriority.Reaction, BehaviorOutcome.Unresolved,
                "this build maps no animation for that reaction") { Reaction = trigger });

        var lib = _robot.Animations.Library;
        if (lib is null)
            return Report(new BehaviorDecision(BehaviorPriority.Reaction, BehaviorOutcome.Unresolved,
                "no animation assets are loaded")
            { Reaction = trigger, Animation = entry.Animation });

        var resolved = _map.Resolve(entry.Animation, lib, _random, Mood);
        if (!resolved.Resolved)
            return Report(new BehaviorDecision(BehaviorPriority.Reaction, BehaviorOutcome.Unresolved,
                resolved.Problem ?? "the trigger resolved to no animation")
            { Reaction = trigger, Animation = entry.Animation, Group = resolved.GroupName });

        var decision = Arbiter.Request(BehaviorPriority.Reaction, resolved.Selected!, trigger, now);
        decision = decision with
        {
            Reaction = trigger,
            Animation = entry.Animation,
            Group = resolved.GroupName,
            Clip = resolved.Selected,
        };
        if (!decision.Started) { Reacted?.Invoke(decision); return decision; }

        var task = _robot.Animations.Play(resolved.Selected!);
        if (task is null)
        {
            Arbiter.Finished(BehaviorPriority.Reaction);
            decision = decision with
            {
                Outcome = BehaviorOutcome.Refused,
                Reason = "the scheduler refused it: a track it needs is owned",
            };
        }
        else
        {
            task.ContinueWith(_ => Arbiter.Finished(BehaviorPriority.Reaction),
                              TaskScheduler.Default);
        }
        Reacted?.Invoke(decision);
        return decision;
    }

    private BehaviorDecision Report(BehaviorDecision d)
    {
        Arbiter.Report(d);
        Reacted?.Invoke(d);
        return d;
    }

    public void Dispose() => Stop();
}
