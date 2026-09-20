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
    private readonly System.Collections.Concurrent.BlockingCollection<Action> _work = new();
    private Thread? _worker;
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

        // The arbiter cannot see an animation the application started directly through
        // robot.Animations.Play, so without this a reaction would happily replace one.
        Arbiter.CallerAnimationRunning ??= () => _robot.Animations.IsPlaying;
    }

    /// <summary>
    /// Whether reaction work runs on this layer's own thread rather than on the caller's.
    ///
    /// Sensor callbacks arrive on the transport's dispatch thread, which also carries robot state. Doing
    /// animation selection and playback there stalls telemetry for everything else, so reactions are
    /// queued onto a serialized worker and the dispatch thread returns immediately. Turned off in tests
    /// that want <see cref="Fire"/> to complete before they assert.
    /// </summary>
    public bool Asynchronous { get; set; } = true;

    /// <summary>How many reactions are waiting to be handled. For tests and diagnostics.</summary>
    public int Queued => _work.Count;

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
        if (Asynchronous && _worker is null)
        {
            _worker = new Thread(WorkLoop) { IsBackground = true, Name = "cozmo-reactions" };
            _worker.Start();
        }
        _robot.Sensors.CliffDetected += OnCliff;
        _robot.Sensors.PickedUpChanged += OnPickedUp;
        _robot.Sensors.OffTreadsStateChanged += OnOffTreads;
        _robot.Sensors.OnChargerChanged += OnCharger;
        _robot.Sensors.FallingChanged += OnFalling;
        _robot.Sensors.FallingStopped += OnFallingStopped;
    }

    /// <summary>
    /// The engine's RobotPickedUp reaction is triggered by the derived off-treads state becoming InAir
    /// (<c>ReactionTriggerStrategyFactory</c> lambda at 0x0060DDCE: <c>Robot+0x355 == 1</c>), not by the raw
    /// IS_PICKED_UP flag. This is the M10 correction to M7, which used the flag. The classifier only runs
    /// once the head has reported a calibration, as the engine's does; until then <see cref="OnPickedUp"/>
    /// keeps firing on the flag and says so.
    /// </summary>
    private void OnOffTreads(OffTreadsState from, OffTreadsState to)
    {
        if (to == OffTreadsState.InAir) Post(() => Fire(ReactionTrigger.RobotPickedUp));
    }

    /// <summary>
    /// Runs queued reactions one at a time, in the order they arrived, so ordering is preserved and a
    /// slow one cannot overlap the next.
    /// </summary>
    private void WorkLoop()
    {
        foreach (var job in _work.GetConsumingEnumerable())
        {
            try { job(); }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
        }
    }

    /// <summary>Queues work, or runs it inline when asynchronous handling is off.</summary>
    private void Post(Action job)
    {
        if (!Asynchronous || _work.IsAddingCompleted) { job(); return; }
        try { _work.Add(job); }
        catch (InvalidOperationException) { job(); }   // completed while we were adding
    }

    /// <summary>Stops watching. Anything already playing is left to finish.</summary>
    public void Stop()
    {
        if (!_subscribed) return;
        _subscribed = false;
        _robot.Sensors.CliffDetected -= OnCliff;
        _robot.Sensors.PickedUpChanged -= OnPickedUp;
        _robot.Sensors.OffTreadsStateChanged -= OnOffTreads;
        _robot.Sensors.OnChargerChanged -= OnCharger;
        _robot.Sensors.FallingChanged -= OnFalling;
        _robot.Sensors.FallingStopped -= OnFallingStopped;
    }

    private void OnCliff(CliffReport report) => Post(() => Fire(ReactionTrigger.CliffDetected));

    /// <summary>
    /// The engine does not react to falling; it reacts to landing. The shipped map sends
    /// <c>RobotFalling</c> to <c>BehaviorReactToImpact</c>, whose <c>AlwaysHandle</c> at 0x00606408 clears
    /// its flags on <c>FallingStarted</c> and waits for <c>FallingStopped</c>. So the start of a fall is
    /// reported here and plays nothing.
    /// </summary>
    private void OnFalling(bool falling)
    {
        if (falling) Post(() => Report(new BehaviorDecision(BehaviorPriority.Reaction, BehaviorOutcome.Unresolved,
            "falling: the engine's ReactToImpact waits for the landing (FallingStopped) before it plays anything")
        { Reaction = ReactionTrigger.RobotFalling }));
    }

    /// <summary>
    /// <c>BehaviorReactToImpact</c>: on <c>FallingStopped</c> the impact intensity must exceed
    /// <see cref="ReactionTable.ImpactIntensityThreshold"/> (1000), and <c>InitInternal</c> at 0x006061F8
    /// then waits up to 5 s (<c>WaitForLambdaAction</c>, timeout 5.0) for the head and lift to finish the
    /// recalibration a fall triggers before <c>TransitionToPlayingAnim</c> plays <c>ReactToImpact</c>. A
    /// landing softer than the threshold ends the behaviour with no animation.
    /// </summary>
    private void OnFallingStopped(FallingStoppedReport report)
    {
        if (report.ImpactIntensity <= ReactionTable.ImpactIntensityThreshold)
        {
            Post(() => Report(new BehaviorDecision(BehaviorPriority.Reaction, BehaviorOutcome.Unresolved,
                $"landed with impact {report.ImpactIntensity:F0}, at or below the engine's threshold of " +
                $"{ReactionTable.ImpactIntensityThreshold:F0}: ReactToImpact plays nothing")
            { Reaction = ReactionTrigger.RobotFalling }));
            return;
        }
        Post(() =>
        {
            var deadline = DateTime.UtcNow + ImpactCalibrationWait;
            while (_robot.State.CalibratingMotors && DateTime.UtcNow < deadline) Thread.Sleep(25);
            Fire(ReactionTrigger.RobotFalling);
        });
    }

    /// <summary>The engine's 5 s allowance for the post-fall motor recalibration (WaitForLambdaAction timeout in InitInternal).</summary>
    public static readonly TimeSpan ImpactCalibrationWait = TimeSpan.FromSeconds(5);

    private void OnPickedUp(bool picked)
    {
        // Only the pick-up has a shipped reaction. Being put down is a real transition and is reported,
        // but the shipped ReactionTrigger set has no "put down" member, so nothing is played for it
        // rather than something being chosen to fill the gap.
        if (!picked)
        {
            Post(() => Report(new BehaviorDecision(BehaviorPriority.Reaction, BehaviorOutcome.Unresolved,
                "put down: the shipped ReactionTrigger set has no member for it")));
            return;
        }
        if (_robot.Sensors.OffTreadsClassifierEnabled)
        {
            // The derived state fires the reaction (OnOffTreads); the flag alone is only recorded.
            Post(() => Report(new BehaviorDecision(BehaviorPriority.Reaction, BehaviorOutcome.Unresolved,
                "IS_PICKED_UP set: the engine reacts to the derived InAir state, which follows from the classifier")
            { Reaction = ReactionTrigger.RobotPickedUp }));
            return;
        }
        // LOCAL_POLICY fallback: no head calibration has been reported, so the classifier is off (as the
        // engine's would be) and the raw flag stands in for it.
        Post(() => Fire(ReactionTrigger.RobotPickedUp));
    }

    private void OnCharger(bool onCharger)
    {
        if (onCharger) Post(() => Fire(ReactionTrigger.PlacedOnCharger));
        else Post(() => Report(new BehaviorDecision(BehaviorPriority.Reaction, BehaviorOutcome.Unresolved,
            "off charger: the shipped ReactionTrigger set has no member for it")));
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

    public void Dispose()
    {
        Stop();
        _work.CompleteAdding();
        _worker?.Join(TimeSpan.FromSeconds(1));
        _worker = null;
        _work.Dispose();
    }
}
