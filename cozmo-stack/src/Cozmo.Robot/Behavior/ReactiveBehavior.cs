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

    /// <summary>
    /// Starts watching the robot's state. Idempotent.
    ///
    /// No-op when a <see cref="BehaviorManager"/> is already dispatching reactions over the same arbiter
    /// (<see cref="BehaviorArbiter.ManagerDispatchesReactions"/>): every reaction this layer plays now has a
    /// behaviour registered with the manager, and two dispatchers over one robot would play each one twice.
    /// </summary>
    public void Start()
    {
        if (_subscribed) return;
        if (Arbiter.ManagerDispatchesReactions)
        {
            Report(new BehaviorDecision(BehaviorPriority.Reaction, BehaviorOutcome.Unresolved,
                "a BehaviorManager is dispatching reactions over this arbiter; the M7 dispatcher stands down so nothing fires twice"));
            return;
        }
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

/// <summary>
/// One behaviour class's 21-byte reaction-lock table, as the engine ships it in <c>.rodata</c>.
///
/// <c>BehaviorManager::DisableReactionsWithLock</c> reads the table at
/// <c>[table + trigger*2 + 1]</c> and disables only the triggers whose byte is non-zero
/// (<c>0x005A283E add r0,r0,r8,lsl #1</c>; <c>0x005A2842 ldrb r0,[r0,#1]</c>; <c>0x005A2846 beq</c>),
/// so a table is a per-class set of triggers, not a boolean for all of them. The tables below are the 13
/// recovered in the M7 inventory Appendix F; each entry lists the trigger ordinals whose byte is 1.
/// </summary>
// fidelity: M7-014
public sealed record ReactionLockTable(string Name, string TableAddress, IReadOnlyList<int> LockedTriggers)
{
    /// <summary>
    /// The engine's bool[21], indexed by <see cref="ReactionTrigger"/> ordinal, as
    /// <c>BehaviorManager::DisableReactionsWithLock</c> consumes it.
    /// </summary>
    public bool[] ToMask()
    {
        var mask = new bool[ReactionLockTables.TriggerCount];
        foreach (var t in LockedTriggers)
            if (t >= 0 && t < mask.Length) mask[t] = true;
        return mask;
    }
}

/// <summary>
/// The 13 concrete reaction-lock tables recovered from the engine (M7 inventory Appendix F). The
/// behaviour classes acquire their lock through <c>IBehavior::SmartDisableReactionsWithLock</c>, which
/// appends <c>"_behaviorLock"</c> and calls the manager with the class's table; this is that table data.
///
/// Trigger ordinals are <see cref="ReactionTrigger"/> values:
/// 0 CliffDetected, 1 CubeMoved, 2 FacePositionUpdated, 3 FistBump, 4 Frustration, 5 Hiccup,
/// 6 MotorCalibration, 7 NoPreDockPoses, 8 ObjectPositionUpdated, 9 PlacedOnCharger,
/// 10 PetInitialDetection, 11 RobotFalling, 12 RobotPickedUp, 13 RobotPlacedOnSlope,
/// 14 ReturnedToTreads, 15 RobotOnBack, 16 RobotOnFace, 17 RobotOnSide, 18 RobotShaken,
/// 19 Sparked, 20 UnexpectedMovement.
/// </summary>
// fidelity: M7-014
public static class ReactionLockTables
{
    /// <summary>The engine's table is one entry per reaction trigger.</summary>
    public const int TriggerCount = 21;

    public static readonly ReactionLockTable ReactToCliff =
        new("ReactToCliff", "0xC73746", new[] { 1, 2, 8, 20 });
    public static readonly ReactionLockTable ReactToMotorCalibration =
        new("ReactToMotorCalibration", "0xC74032", new[] { 0, 5, 12, 14, 15, 16, 17 });
    public static readonly ReactionLockTable ReactToPlacedOnSlope =
        new("ReactToPlacedOnSlope", "0xC745E0", new[] { 0, 12, 14 });
    public static readonly ReactionLockTable ReactToRobotShaken =
        new("ReactToRobotShaken", "0xC750E0", new[] { 0, 1, 2, 3, 4, 5, 8, 10, 11, 12, 13, 14, 15, 16, 17, 20 });
    public static readonly ReactionLockTable CubeLiftWorkout =
        new("CubeLiftWorkout", "0xc6b7e0", new[] { 1, 2, 8, 10, 20 });
    public static readonly ReactionLockTable PeekABoo =
        new("PeekABoo", "0xc70960", new[] { 1, 2, 3, 8, 10 });
    public static readonly ReactionLockTable PutDownBlock =
        new("PutDownBlock", "0xc68b20", Array.Empty<int>());
    public static readonly ReactionLockTable FistBump =
        new("FistBump", "0xc6fdc8", new[] { 1, 2, 8, 10, 11, 12, 14, 20 });
    public static readonly ReactionLockTable Bouncer =
        new("Bouncer", "0xc6fb90", new[] { 1, 2, 3, 4, 5, 8, 10, 20 });
    public static readonly ReactionLockTable GuardDog =
        new("GuardDog", "0xc6ff06", new[] { 1, 2, 3, 4, 5, 8, 10, 20 });
    public static readonly ReactionLockTable Dance =
        new("Dance", "0xc6f1c8", new[] { 1, 2, 3, 8, 10, 20 });
    public static readonly ReactionLockTable EnrollFace =
        new("EnrollFace", "0xc71b6c", new[] { 0, 1, 2, 3, 4, 5, 8, 11, 12, 14, 15, 16, 17, 20 });
    public static readonly ReactionLockTable OnboardingShowCube =
        new("OnboardingShowCube", "0xc72454", new[] { 1, 2, 3, 4, 5, 8, 10, 20 });

    /// <summary>All 13, in the order Appendix F lists them.</summary>
    public static readonly IReadOnlyList<ReactionLockTable> All = new[]
    {
        ReactToCliff, ReactToMotorCalibration, ReactToPlacedOnSlope, ReactToRobotShaken,
        CubeLiftWorkout, PeekABoo, PutDownBlock, FistBump, Bouncer, GuardDog, Dance,
        EnrollFace, OnboardingShowCube,
    };

    // The five tables of the classes this stack builds whose table was not among the 13, and the table
    // IBehavior::Init/Resume install under "SparkBehaviorDisables" (gap pass 1 section 3.3; each is 21 consecutive
    // {trigger, bool} pairs, the value byte non-zero = disabled, tested at 0x005A283C..0x005A2846). They are not in
    // All (the 13 of Appendix F) so For(class) keeps answering for the original 13 only.

    /// <summary>BehaviorReactToImpact::InitInternal 0x00606200..0x00606216: 0x00C73D36, mask 111111111011111111011.</summary>
    // fidelity: M7-014
    public static readonly ReactionLockTable ReactToImpact =
        new("ReactToImpact", "0xC73D36", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 14, 15, 16, 17, 19, 20 });
    /// <summary>BehaviorDriveOffCharger::InitInternal 0x005C0B2A: 0x00C672F0, mask 111001001100000000001.</summary>
    // fidelity: M7-014
    public static readonly ReactionLockTable DriveOffCharger =
        new("DriveOffCharger", "0xC672F0", new[] { 0, 1, 2, 5, 8, 9, 20 });
    /// <summary>BehaviorSinging::InitInternal 0x005EEB5E: 0x00C6F590, mask 011000001010000000001.</summary>
    // fidelity: M7-014
    public static readonly ReactionLockTable Singing =
        new("Singing", "0xC6F590", new[] { 1, 2, 8, 10, 20 });
    /// <summary>BehaviorAcknowledgeCubeMoved::InitInternal 0x00602242: 0x00C72BB2, mask 000000001000000000000.</summary>
    // fidelity: M7-014
    public static readonly ReactionLockTable AcknowledgeCubeMoved =
        new("AcknowledgeCubeMoved", "0xC72BB2", new[] { 8 });
    /// <summary>BehaviorReactToOnCharger::InitInternal 0x00606CB0: 0x00C74182, mask 011111011010000000011.</summary>
    // fidelity: M7-014
    public static readonly ReactionLockTable ReactToOnCharger =
        new("ReactToOnCharger", "0xC74182", new[] { 1, 2, 3, 4, 5, 7, 8, 10, 19, 20 });
    // R-FIX3 round 3 (the tables were read from libcozmoEngine.so: 21 consecutive {ordinal, value} pairs, the first byte of pair i is i; the value byte non-zero = disabled, 0x005A283C..0x005A2846).

    /// <summary>BehaviorKnockOverCubes::InitializeMemberVars 0x005C31FA (its InitInternal's first call, 0x005C31A2..0x005C31A8): 0x00C67CB2, mask 010001001000000000000.</summary>
    // fidelity: M7-014
    public static readonly ReactionLockTable KnockOverCubes =
        new("KnockOverCubes", "0xC67CB2", new[] { 1, 5, 8 });
    /// <summary>BehaviorKnockOverCubes::PrepareForKnockOverAttempt 0x005C37F0, lock name "preparingToKnockOverDisable" (literal 0x005C3820): 0x00C67CDC, mask 000000000000000000000.</summary>
    // fidelity: M7-014
    public static readonly ReactionLockTable KnockOverCubesPreparing =
        new("KnockOverCubesPreparing", "0xC67CDC", Array.Empty<int>());
    /// <summary>The lock name PrepareForKnockOverAttempt removes and installs (literal 0x005C3820, 27 characters).</summary>
    public const string KnockOverCubesPreparingName = "preparingToKnockOverDisable";
    /// <summary>BehaviorPopAWheelie's pre-dock callback 0x005C7BC8 (SmartDisableReactionsWithLock at 0x005C7BE8): 0x00C6883D, mask 110100000001101100001.</summary>
    // fidelity: M7-014
    public static readonly ReactionLockTable PopAWheelie =
        new("PopAWheelie", "0xC6883D", new[] { 0, 1, 3, 11, 12, 14, 15, 20 });
    /// <summary>BehaviorBuildPyramid::TransitionToPlacingTopBlock 0x005DC0D6: 0x00C6C691, mask 000000001000000000000.</summary>
    // fidelity: M7-014
    public static readonly ReactionLockTable BuildPyramid =
        new("BuildPyramid", "0xC6C691", new[] { 8 });
    /// <summary>BehaviorRamIntoBlock::TransitionToRammingIntoBlock 0x00604A44: 0x00C73520, mask 101000001000000000001.</summary>
    // fidelity: M7-014
    public static readonly ReactionLockTable RamIntoBlock =
        new("RamIntoBlock", "0xC73520", new[] { 0, 2, 8, 20 });

    /// <summary>IBehavior::Init 0x005BCD44 and IBehavior::Resume 0x005BCFDE, lock name "SparkBehaviorDisables": 0x00C65F90, mask 000000001000000000000.</summary>
    // fidelity: M7-014
    public static readonly ReactionLockTable SparkBehaviorDisables =
        new("SparkBehaviorDisables", "0xC65F90", new[] { 8 });

    /// <summary>The lock name IBehavior::Init/Resume pass (literal 0x00BF2B0A).</summary>
    public const string SparkBehaviorDisablesName = "SparkBehaviorDisables";

    /// <summary>The table for a behaviour class, or null when the class ships no table.</summary>
    public static ReactionLockTable? For(string behaviorClass) =>
        All.FirstOrDefault(t => string.Equals(t.Name, behaviorClass, StringComparison.Ordinal));
}
