using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// <c>BehaviorReactToRobotOnBack</c> (0x006087B8..0x006089FC). <c>InitInternal</c> calls
/// <c>FlipDownIfNeeded</c>: while the off-treads state is OnBack, read cliff sensor 0
/// (<c>CliffSensorComponent::GetCliffDataRaw(0)</c>); if <c>raw &gt;&gt; 4 ≤ 0x18</c> (raw below 400) play
/// <see cref="AnimationTrigger.FlipDownFromBack"/> (0xCB; <c>HiccupRobotOnBack</c> 0xE4 instead when the
/// whiteboard's hiccup flag is set, a system this stack does not have), otherwise the sensor is being
/// blocked and the head is recalibrated instead (<c>CalibrateMotorAction(head)</c>, DAS
/// <c>CalibratingHead</c>). Either way <c>DelayThenFlipDown</c> follows: still on back → wait 0.5 s
/// (<c>WaitAction</c> 0x3F000000) and try again; otherwise objective <c>ReactedToRobotOnBack</c> and done.
/// </summary>
public sealed class ReactToRobotOnBackBehavior : SteppedBehavior
{
    private long _generation;
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => context?.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion() => Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version) => Interlocked.Read(ref _generation) == version;
    protected override bool HostStopContinuationCurrent(long version) => Interlocked.Read(ref _generation) == unchecked(version + 1);
    protected override void OnStop(BehaviorStopReason reason) => Interlocked.Increment(ref _generation);
    /// <summary>The cliff-sensor test: <c>(raw &gt;&gt; 4) &gt; 0x18</c>, i.e. raw ≥ 400, means calibrate rather than flip.</summary>
    public const int CliffRawCalibrateAt = 400;

    public ReactToRobotOnBackBehavior() : base("ReactToRobotOnBack", "ReactToRobotOnBack") { }

    protected override void OnStart() => FlipDownIfNeeded();

    private void FlipDownIfNeeded()
    {
        var sensors = Context.Robot.Sensors;
        if (sensors.OffTreadsState != OffTreadsState.OnBack)
        {
            long epoch = CaptureHostExecutionVersion();
            Log("not on back: objective ReactedToRobotOnBack");
            if (!HostExecutionVersionCurrent(epoch)) return;
            Context.Robot.Engine.ReportBehaviorObjective(0x21, "ReactedToRobotOnBack", Id);
            if (!HostExecutionVersionCurrent(epoch)) return;
            Finish();
            return;
        }
        var raw = sensors.CliffSensorsRaw;
        int front = raw is { Length: > 0 } ? raw[0] : 0;
        if ((front >> 4) <= 0x18) PlayTrigger(AnimationTrigger.FlipDownFromBack, DelayThenFlipDown);
        else
        {
            Log($"cliff sensor 0 reads {front} (>= {CliffRawCalibrateAt}): calibrating the head instead of flipping");
            CalibrateHead(DelayThenFlipDown);
        }
    }

    private void DelayThenFlipDown()
    {
        if (Context.Robot.Sensors.OffTreadsState != OffTreadsState.OnBack)
        {
            long epoch = CaptureHostExecutionVersion();
            Log("back on treads: objective ReactedToRobotOnBack");
            if (!HostExecutionVersionCurrent(epoch)) return;
            Context.Robot.Engine.ReportBehaviorObjective(0x21, "ReactedToRobotOnBack", Id);
            if (!HostExecutionVersionCurrent(epoch)) return;
            Finish();
            return;
        }
        Wait(0.5, FlipDownIfNeeded);
    }
}

/// <summary>
/// <c>BehaviorReactToRobotOnFace</c> (0x00608A9C..0x00608C98). <c>FlipOverIfNeeded</c>: while OnFace, play a
/// face-plant roll and then <c>DelayThenCheckState</c> (still on face → wait 0.5 s → <c>CheckFlipSuccess</c>:
/// still on face → <see cref="AnimationTrigger.FailedToRightFromFace"/> (0xA7) and try again; otherwise
/// objective <c>ReactedToRobotOnFace</c>).
///
/// Which roll: the code (0x00608ADE..0x00608B20) compares <c>Robot+0x300</c> with 45.0 and picks
/// <see cref="AnimationTrigger.FacePlantRoll"/> (0xA5) below it, <see cref="AnimationTrigger.FacePlantRollArmUp"/>
/// (0xA6) otherwise. <c>Robot+0x300</c> is the lift <b>angle in radians</b> (stored from <c>RobotState.liftAngle</c>
/// at 0x0051296A), which is never anywhere near 45, so on this build the arm-up variant is unreachable and
/// FacePlantRoll always plays. That is reproduced, not corrected. <c>HiccupRobotOnFace</c> (0xE5) replaces it
/// under the hiccup flag, which this stack does not have.
/// </summary>
public sealed class ReactToRobotOnFaceBehavior : SteppedBehavior
{
    private long _generation;
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => context?.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion() => Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version) => Interlocked.Read(ref _generation) == version;
    protected override bool HostStopContinuationCurrent(long version) => Interlocked.Read(ref _generation) == unchecked(version + 1);
    protected override void OnStop(BehaviorStopReason reason) => Interlocked.Increment(ref _generation);
    public ReactToRobotOnFaceBehavior() : base("ReactToRobotOnFace", "ReactToRobotOnFace") { }

    protected override void OnStart() => FlipOverIfNeeded();

    private void FlipOverIfNeeded()
    {
        if (Context.Robot.Sensors.OffTreadsState != OffTreadsState.OnFace) { Log("not on face"); Finish(); return; }
        float liftAngle = Context.Robot.Sensors.LiftAngleRad ?? 0f;
        var roll = liftAngle < 45f ? AnimationTrigger.FacePlantRoll : AnimationTrigger.FacePlantRollArmUp;
        PlayTrigger(roll, DelayThenCheckState);
    }

    private void DelayThenCheckState()
    {
        if (Context.Robot.Sensors.OffTreadsState != OffTreadsState.OnFace) { Log("righted"); Finish(); return; }
        Wait(0.5, CheckFlipSuccess);
    }

    private void CheckFlipSuccess()
    {
        if (Context.Robot.Sensors.OffTreadsState == OffTreadsState.OnFace)
            PlayTrigger(AnimationTrigger.FailedToRightFromFace, FlipOverIfNeeded);
        else
        {
            long epoch = CaptureHostExecutionVersion();
            Log("righted: objective ReactedToRobotOnFace");
            if (!HostExecutionVersionCurrent(epoch)) return;
            Context.Robot.Engine.ReportBehaviorObjective(0x22, "ReactedToRobotOnFace", Id);
            if (!HostExecutionVersionCurrent(epoch)) return;
            Finish();
        }
    }
}

/// <summary>
/// <c>BehaviorReactToRobotOnSide</c> (0x00608D38..0x00609064). <c>ReactToBeingOnSide</c> plays
/// <see cref="AnimationTrigger.ReactToOnLeftSide"/> (0x1A6) or <see cref="AnimationTrigger.ReactToOnRightSide"/>
/// (0x1A7) for the side it is on, then <c>AskToBeRighted</c> plays <see cref="AnimationTrigger.AskToBeRightedLeft"/>
/// (4) or <c>Right</c> (5), then <c>HoldingLoop</c>: for 15 s (the deadline at <c>+0x11c</c>, −1 until set)
/// it plays <see cref="AnimationTrigger.WaitOnSideLoop"/> (0x22B) over and over; when the 15 s are up it
/// plays the bored sequence <see cref="AnimationTrigger.NothingToDoBoredIntro"/>, <c>Event</c>, <c>Outro</c>
/// (0x149, 0x147, 0x14A) as a <c>CompoundActionSequential</c>, clears the deadline and loops. Any transition
/// finding the robot no longer on a side ends the behaviour. It reports two needs actions by name:
/// <c>PlacedOnSide</c> (0x2F) from <c>InitInternal</c> (0x00608D70) and <c>BoredOnSide</c> (0x30) each time
/// the hold runs out and the bored sequence plays (0x00608F78).
/// </summary>
public sealed class ReactToRobotOnSideBehavior : SteppedBehavior
{
    public const double HoldBeforeBoredSec = 15.0;
    private double _deadlineMs = -1;

    public ReactToRobotOnSideBehavior() : base("ReactToRobotOnSide", "ReactToRobotOnSide") { }

    /// <summary>How many bored sequences have played this run, for tests.</summary>
    public int BoredSequences { get; private set; }

    protected override void OnStart()
    {
        _deadlineMs = -1;
        BoredSequences = 0;
        if (NeedActionCompleted("PlacedOnSide") is { } action) Log($"needs action {action}");
        ReactToBeingOnSide();
    }

    private OffTreadsState State => Context.Robot.Sensors.OffTreadsState;

    private void ReactToBeingOnSide()
    {
        var trigger = State switch
        {
            OffTreadsState.OnLeftSide => AnimationTrigger.ReactToOnLeftSide,
            OffTreadsState.OnRightSide => AnimationTrigger.ReactToOnRightSide,
            _ => (AnimationTrigger?)null,
        };
        if (trigger is null) { Log("not on a side"); Finish(); return; }
        PlayTrigger(trigger.Value, AskToBeRighted);
    }

    private void AskToBeRighted()
    {
        var trigger = State switch
        {
            OffTreadsState.OnLeftSide => AnimationTrigger.AskToBeRightedLeft,
            OffTreadsState.OnRightSide => AnimationTrigger.AskToBeRightedRight,
            _ => (AnimationTrigger?)null,
        };
        if (trigger is null) { Log("righted before asking"); Finish(); return; }
        PlayTrigger(trigger.Value, HoldingLoop);
    }

    private void HoldingLoop()
    {
        if (State is not (OffTreadsState.OnLeftSide or OffTreadsState.OnRightSide)) { Log("righted"); Finish(); return; }
        double t = NowMs;
        if (_deadlineMs < 0) _deadlineMs = t + HoldBeforeBoredSec * 1000;
        if (t < _deadlineMs)
        {
            PlayTrigger(AnimationTrigger.WaitOnSideLoop, HoldingLoop);
            return;
        }
        _deadlineMs = -1;
        BoredSequences++;
        Log("held for 15 s: bored sequence");
        if (NeedActionCompleted("BoredOnSide") is { } action) Log($"needs action {action}");
        PlayTrigger(AnimationTrigger.NothingToDoBoredIntro, () =>
            PlayTrigger(AnimationTrigger.NothingToDoBoredEvent, () =>
                PlayTrigger(AnimationTrigger.NothingToDoBoredOutro, HoldingLoop)));
    }
}

/// <summary>
/// <c>BehaviorReactToPlacedOnSlope</c> (0x00607FB8..0x00608240). <c>InitInternal</c> takes the reaction lock,
/// then: if it last ran less than 10 s ago (<c>+0x120</c>, seconds, <c>vmov.f64 d1, #10.0</c>) <b>and</b>
/// that run ended with the pitch still above 10° (<c>+0x11c</c>), the pitch reading is suspected and the head
/// is recalibrated instead (DAS <c>CalibratingHead</c> with the pitch in degrees), clearing the flag.
/// Otherwise it plays <see cref="AnimationTrigger.ReactToPerchedOnBlock"/> (0x1A8; the severe-needs variants
/// <c>NeedsSevereLowRepairSlopeReact</c> 0x145 / <c>NeedsSevereLowEnergySlopeReact</c> 0x139 replace it when the
/// needs system says so, which this stack does not model) and then <c>CheckPitch</c> records whether the
/// pitch is still above 10°. The run time is stamped at the end of <c>InitInternal</c> either way.
/// </summary>
/// <summary>
/// <c>BehaviorReactToImpact</c>, the behaviour the shipped map runs for <c>ReactionTrigger.RobotFalling</c>
/// (constructor 0x0060617C; subscribes the tags {0x3a, 0x3b, 0x1e} from the table at 0x00C73D30).
///
/// It is <b>always runnable</b> (<c>IsRunnableInternal</c> 0x00606486 is <c>movs r0,#1; bx lr</c>): nothing gates it on the
/// impact. <c>AlwaysHandle</c> (0x00606408) keeps three bytes, in every state: FallingStarted (0x3a) zeroes +0x11e
/// (0x00606422) and the halfword at +0x11c, i.e. +0x11c and +0x11d (0x00606426); FallingStopped (0x3b) stores +0x11d = 1
/// and then +0x11e = (impactIntensity &gt; 1000.0f) <b>unconditionally</b>, 0 or 1 (0x0060642c..0x0060644e; the literal is
/// at 0x00606470); MotorCalibration (0x1e) stores +0x11c = 1 when <c>Robot::IsHeadCalibrated()</c> and
/// <c>Robot::IsLiftCalibrated()</c> are both 1 (0x00606454..0x0060646a). <c>InitInternal</c> (0x006061F8) takes the lock
/// table 0x00C73D36 (0x00606200..0x00606216), then <b>always</b> builds a <c>WaitForLambdaAction</c> with timeout 5.0f
/// (0x40a00000, 0x00606240) whose predicate is <c>+0x11d &amp;&amp; +0x11c</c> (0x006064c6..0x006064dc) and starts it with
/// <c>StartActing</c> (0x00606254) and <c>TransitionToPlayingAnim</c> (0x00606348) as its completion. That runs whether the
/// predicate came true or the 5 s passed. <c>TransitionToPlayingAnim</c> acts only when +0x11e is set: then a
/// <c>TriggerAnimationAction(robot, 0x1a0, 1, true, 0, 60.0f)</c> (0x0060637e, 0x00606384). <c>StopInternal</c> (0x006063fc) clears +0x11c,
/// +0x11d and +0x11e.
/// </summary>
// fidelity: M7-003, M7-014
public sealed class ReactToImpactBehavior : SteppedBehavior
{
    /// <summary>The engine's 5 s allowance for the post-fall motor recalibration (0x40a00000 in InitInternal).</summary>
    public const double CalibrationWaitSec = 5.0;

    /// <summary>
    /// The <c>TriggerAnimationAction</c> timeout the engine passes to <c>TransitionToPlayingAnim</c>:
    /// 60.0 s (<c>movt r0,#0x4270</c> at 0x0060636e).
    /// </summary>
    public const double AnimationTimeoutSec = 60.0;

    /// <summary>The tags the constructor subscribes (table 0x00C73D30): FallingStarted, FallingStopped, MotorCalibration.</summary>
    public static readonly int[] SubscribedTags = { 0x3a, 0x3b, 0x1e };

    public ReactToImpactBehavior(string id = "ReactToImpact") : base(id, "ReactToImpact") { }

    /// <summary>
    /// Wires <c>AlwaysHandle</c> to the robot's messages: the three tags it switches on, in every state of the behaviour.
    /// </summary>
    public ReactToImpactBehavior(CozmoRobot robot, string id = "ReactToImpact") : this(id)
    {
        robot.Sensors.FallingStartedInternal += _ => HandleFallingStarted();
        robot.Sensors.FallingStoppedInternal += r => HandleFallingStopped(r.ImpactIntensity);
        robot.Sensors.MotorCalibrationReportedInternal += _ => HandleMotorCalibration(robot.State.HeadCalibrated, robot.State.LiftCalibrated);
    }

    private volatile bool _impact;             // +0x11e
    private volatile bool _calibrated;         // +0x11c
    private volatile bool _fallingStoppedSeen; // +0x11d

    /// <summary>+0x11e: the last FallingStopped had an impact intensity above 1000.0f.</summary>
    public bool ImpactRecorded => _impact;
    /// <summary>+0x11c: the head and lift both reported calibrated.</summary>
    public bool CalibratedRecorded => _calibrated;
    /// <summary>+0x11d: a FallingStopped has been seen.</summary>
    public bool FallingStoppedSeen => _fallingStoppedSeen;

    /// <summary>AlwaysHandle tag 0x3a: +0x11e = 0 (0x00606422), then the halfword at +0x11c = 0 (0x00606426): +0x11c and +0x11d.</summary>
    public void HandleFallingStarted() { _impact = false; _calibrated = false; _fallingStoppedSeen = false; }

    /// <summary>AlwaysHandle tag 0x3b: +0x11d = 1, then +0x11e = (intensity &gt; 1000.0f), stored whichever way it came out (0x0060643c..0x0060644e).</summary>
    public void HandleFallingStopped(float impactIntensity)
    {
        _fallingStoppedSeen = true;
        _impact = impactIntensity > ReactionTable.ImpactIntensityThreshold;
    }

    /// <summary>AlwaysHandle tag 0x1e: +0x11c = 1 only when both IsHeadCalibrated and IsLiftCalibrated hold (0x00606454..0x0060646a).</summary>
    public void HandleMotorCalibration(bool headCalibrated, bool liftCalibrated)
    {
        if (headCalibrated && liftCalibrated) _calibrated = true;
    }

    /// <summary><c>IsRunnableInternal</c> 0x00606486: <c>movs r0,#1</c>.</summary>
    protected override bool IsRunnableInternal(BehaviorContext context) => true;

    /// <summary>Whether the last run's wait ended without both flags (the 5 s ran out).</summary>
    public bool WaitedOut { get; private set; }

    /// <summary><c>InitInternal</c> 0x006061F8: the lock, then the 5 s <c>WaitForLambdaAction(+0x11d &amp;&amp; +0x11c)</c> whichever the flags say.</summary>
    protected override void OnStart()
    {
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.ReactToImpact);   // 0x00606216
        WaitedOut = false;
        WaitUntil(() => _fallingStoppedSeen && _calibrated, CalibrationWaitSec,
                  met => { WaitedOut = !met; TransitionToPlayingAnim(); }, "the motor recalibration a fall triggers");
    }

    /// <summary>
    /// <c>TransitionToPlayingAnim</c> 0x00606348: acts only when <c>+0x11e</c> is set (0x0060635a), then starts
    /// <c>TriggerAnimationAction(robot, 0x1a0, 1, true, 0, 60.0f, ...)</c>. The inventory gives no completion callback for it.
    /// </summary>
    private void TransitionToPlayingAnim()
    {
        if (!_impact) return;
        PlayTrigger(AnimationTrigger.ReactToImpact, () => { }, timeoutSec: AnimationTimeoutSec);
    }

    /// <summary><c>StopInternal</c> 0x006063fc: +0x11c, +0x11d and +0x11e cleared.</summary>
    protected override void OnStop(BehaviorStopReason reason) { _impact = false; _calibrated = false; _fallingStoppedSeen = false; }
}

public sealed class ReactToPlacedOnSlopeBehavior : SteppedBehavior
{
    public const double RepeatWithinSec = 10.0;
    public const float HighPitchDeg = 10f;
    private double _lastRunMs = double.NegativeInfinity;

    public ReactToPlacedOnSlopeBehavior() : base("ReactToPlacedOnSlope", "ReactToPlacedOnSlope") { }

    /// <summary>Whether the last run ended with the pitch still above 10° (<c>+0x11c</c>).</summary>
    public bool PitchWasHigh { get; private set; }

    protected override void OnStart()
    {
        // fidelity: M7-014
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.ReactToPlacedOnSlope);
        double now = Clock();
        if (now - _lastRunMs < RepeatWithinSec * 1000 && PitchWasHigh)
        {
            float deg = (Context.Robot.Sensors.PitchRad ?? 0f) * (180f / MathF.PI);
            Log($"placed on a slope again within 10 s with the pitch still high ({deg:F1} deg): calibrating the head");
            CalibrateHead(() => { PitchWasHigh = false; Finish(); });
        }
        else
        {
            int selected = (Context.Needs ?? Context.Robot.Engine.Needs).SevereNeeds.Selected;
            PlayTrigger((AnimationTrigger)(selected == 0 ? 0x145 : selected == 1 ? 0x139 : 0x1A8), CheckPitch);
        }
        _lastRunMs = now;
    }

    private void CheckPitch()
    {
        float deg = (Context.Robot.Sensors.PitchRad ?? 0f) * (180f / MathF.PI);
        PitchWasHigh = deg > HighPitchDeg;
        Log($"pitch after the reaction {deg:F1} deg: {(PitchWasHigh ? "still high" : "level")}");
        Finish();
    }
}

/// <summary>
/// <c>BehaviorReactToReturnedToTreads</c> (0x006084E4..0x00608718): wait 0.5 s, then <c>CheckForHighPitch</c>:
/// if |pitch| &gt; 10° the head is recalibrated (DAS <c>CalibratingHead</c>). It plays no animation at all;
/// "returning to treads" is a head-calibration check, not a reaction the user sees.
/// </summary>
public sealed class ReactToReturnedToTreadsBehavior : SteppedBehavior
{
    public const float HighPitchDeg = 10f;

    public ReactToReturnedToTreadsBehavior() : base("ReactToReturnedToTreads", "ReactToReturnedToTreads") { }

    /// <summary>Whether the last run decided to recalibrate.</summary>
    public bool Recalibrated { get; private set; }

    protected override void OnStart()
    {
        Recalibrated = false;
        Wait(0.5, CheckForHighPitch);
    }

    private void CheckForHighPitch()
    {
        float deg = (Context.Robot.Sensors.PitchRad ?? 0f) * (180f / MathF.PI);
        if (MathF.Abs(deg) > HighPitchDeg)
        {
            Recalibrated = true;
            Log($"pitch {deg:F1} deg after returning to treads: calibrating the head");
            CalibrateHead(Finish);
        }
        else
        {
            Log($"pitch {deg:F1} deg: nothing to do");
            Finish();
        }
    }
}

/// <summary>
/// <c>BehaviorReactToRobotShaken</c> (0x00609104..0x006097A8), a five-state <c>UpdateInternal</c>
/// (the <c>tbb</c> at 0x00609228):
/// <list type="number">
/// <item><b>Shaking</b>: <c>InitInternal</c> took the reaction lock, stamped the start time and started
/// <see cref="AnimationTrigger.DizzyShakeLoop"/> (0x89) with <c>numLoops = 0</c> (forever). Each tick the
/// filtered |accel| (<c>Robot+0x37c</c>) updates the maximum; when it drops below 13000 (0x464B2000) the
/// shaken duration is stamped and the state advances.</item>
/// <item><b>Stopping</b>: <c>StopActing</c>, then <see cref="AnimationTrigger.DizzyShakeStop"/> (0x8A) and
/// <see cref="AnimationTrigger.DizzyStillPickedUp"/> (0x8B) in sequence.</item>
/// <item><b>Waiting</b>: back on treads (<c>Robot+0x355 == 0</c>) → choose a reaction; still off treads
/// once the sequence has finished → no reaction (code 4) and finish.</item>
/// <item><b>Choosing</b>: <c>StopActing</c>; duration &gt; 5 s → <see cref="AnimationTrigger.DizzyReactionHard"/>
/// (0x86, needs action <c>DizzyHard</c>), &gt; 2.5 s → <c>Medium</c> (0x87), else <c>Soft</c> (0x88).</item>
/// <item><b>Finishing</b>: when the animation ends, objective <c>ReactedToRobotShaken</c>.</item>
/// </list>
/// <c>StopInternal</c> sends DAS <c>robot.dizzy_reaction</c> with the duration in ms, the peak magnitude and
/// the reaction played; that line is written to the trace. The reaction codes 1..3 follow the needs-action
/// names the engine pairs with them; 0 and 4 are named here (LOCAL naming of native values).
/// </summary>
public sealed class ReactToRobotShakenBehavior : SteppedBehavior
{
    private Action? _completionLease;
    private long _generation;
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => context?.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion() => Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version) => Interlocked.Read(ref _generation) == version;
    protected override bool HostStopContinuationCurrent(long version) => Interlocked.Read(ref _generation) == unchecked(version + 1);
    public enum Phase { Shaking, Stopping, Waiting, Choosing, Finishing }
    public enum Reaction : byte { None = 0, Soft = 1, Medium = 2, Hard = 3, NotBackOnTreads = 4 }

    /// <summary>The shake is over when the filtered |accel| drops below this: 13000 (0x464B2000).</summary>
    public const float ShakeContinuesAbove = 13000f;
    public const double HardAboveSec = 5.0, MediumAboveSec = 2.5;

    public ReactToRobotShakenBehavior() : base("ReactToRobotShaken", "ReactToRobotShaken") { }

    public Phase CurrentPhase { get; private set; }
    public float MaxAccelMagnitude { get; private set; }
    public double ShakenDurationSec { get; private set; }
    public Reaction Played { get; private set; }

    protected override bool KeepsRunningWithoutAction => true;

    protected override void OnStart()
    {
        // fidelity: M7-014
        long epoch = CaptureHostExecutionVersion();
        if (!Scope.SmartDisableReactionsWithLockGuarded(Id, ReactionLockTables.ReactToRobotShaken, () => HostExecutionVersionCurrent(epoch))) return;
        var severe = (Context.Needs ?? Context.Robot.Engine.Needs).SevereNeeds;
        if (severe.Selected != 3) severe.Clear();
        if (!HostExecutionVersionCurrent(epoch)) return;
        CurrentPhase = Phase.Shaking;
        MaxAccelMagnitude = 0;
        ShakenDurationSec = 0;
        Played = Reaction.None;
        LoopShake();
    }

    private void LoopShake()
    {
        if (CurrentPhase != Phase.Shaking) return;
        long epoch = CaptureHostExecutionVersion();
        _completionLease ??= AcquireActionCompletionOwner(Context.Robot);
        var action = new TriggerAnimationAction(Context.Robot, new(AnimationTrigger.DizzyShakeLoop, 0, true, 0, 60f, false));
        if (!HostExecutionVersionCurrent(epoch)) { action.RetireStaleConstructor(() => false); return; }
        StartActing(action, _ => { }, () => HostExecutionVersionCurrent(epoch));
    }

    protected override void OnUpdate()
    {
        var sensors = Context.Robot.Sensors;
        switch (CurrentPhase)
        {
            case Phase.Shaking:
            {
                float mag = sensors.FilteredAccelMagnitude;
                if (mag > MaxAccelMagnitude) MaxAccelMagnitude = mag;
                if (mag < ShakeContinuesAbove)
                {
                    ShakenDurationSec = (NowMs - (StartedMs ?? NowMs)) / 1000.0;
                    Log($"shaking stopped after {ShakenDurationSec:F2} s, peak |accel| {MaxAccelMagnitude:F0}");
                    CurrentPhase = Phase.Stopping;
                }
                break;
            }
            case Phase.Stopping:
                StopActing();
                PlayTrigger(AnimationTrigger.DizzyShakeStop, () => PlayTrigger(AnimationTrigger.DizzyStillPickedUp, () => { }));
                CurrentPhase = Phase.Waiting;
                break;

            case Phase.Waiting:
                if (sensors.OffTreadsState == OffTreadsState.OnTreads) CurrentPhase = Phase.Choosing;
                else if (!HasCurrentAction && !Busy)
                {
                    Played = Reaction.NotBackOnTreads;
                    Log("still off treads after the shake sequence: no dizzy reaction");
                    CurrentPhase = Phase.Finishing;
                }
                break;

            case Phase.Choosing:
            {
                StopActing();
                AnimationTrigger trigger;
                if (ShakenDurationSec > HardAboveSec) { Played = Reaction.Hard; trigger = AnimationTrigger.DizzyReactionHard; }
                else if (ShakenDurationSec > MediumAboveSec) { Played = Reaction.Medium; trigger = AnimationTrigger.DizzyReactionMedium; }
                else { Played = Reaction.Soft; trigger = AnimationTrigger.DizzyReactionSoft; }
                Log($"back on treads: dizzy reaction {Played}");
                // 0x00609460 / 0x0060946C name DizzyMedium (0x0F) and DizzySoft (0x10); the hard tier's
                // DizzyHard (0x0E) follows the same naming.
                if (NeedActionCompleted($"Dizzy{Played}") is { } action) Log($"needs action {action}");
                PlayTrigger(trigger, () => { });
                CurrentPhase = Phase.Finishing;
                break;
            }
            case Phase.Finishing:
                if (!HasCurrentAction && !Busy)
                {
                    long epoch = CaptureHostExecutionVersion();
                    Log("objective ReactedToRobotShaken");
                    if (!HostExecutionVersionCurrent(epoch)) return;
                    Context.Robot.Engine.ReportBehaviorObjective(0x24, "ReactedToRobotShaken", Id);
                    if (!HostExecutionVersionCurrent(epoch)) return;
                    Finish();
                }
                break;
        }
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        Interlocked.Increment(ref _generation);
        _completionLease?.Invoke(); _completionLease = null;
        Log($"DAS robot.dizzy_reaction: shakenDuration = {ShakenDurationSec:F3}s, maxShakingAccelMag = {MaxAccelMagnitude:F1}, reactionPlayed = '{Played}'");
    }
}

/// <summary>
/// <c>BehaviorReactToUnexpectedMovement</c> (class 0x4E, 0x00609A58..0x00609C76; REACT-UNEXPECTED-MOVEMENT-1,
/// re-analysis/research/20261029-react-unexpected-movement-1-read.md). The ctor sets <c>+0x11C</c> = 0 and subscribes to
/// tag 0x3C (C1); <c>AlwaysHandle</c> keeps the last broadcast's <c>movementSide</c> in <c>+0x11C</c> whether or not the
/// behaviour runs (H1/H2). <c>InitInternal</c> fires the emotion event <c>"ReactToUnexpectedMovement"</c> (I1), picks the
/// trigger from the selected severe need (I2/I3), locks BODY when the side was BACK (I4), and starts one
/// <c>TriggerLiftSafeAnimationAction(trigger, 1, true, lock, 60.0f, false)</c> (I5/I6); when it ends, with any result, the
/// objective <c>ReactedToUnexpectedMovement</c> (0x25) is reported (K1/K2) and the behaviour ends with its action (U1).
/// <c>StopInternal</c> is empty (T1); Resume is the base one (P1).
/// </summary>
// fidelity: M7-031
public sealed class ReactToUnexpectedMovementBehavior : SteppedBehavior
{
    public const string EmotionEventName = "ReactToUnexpectedMovement";
    /// <summary>K2 0x00609C5A: <c>movs r1,#0x25</c>, BehaviorObjective ReactedToUnexpectedMovement.</summary>
    public const uint ObjectiveReactedToUnexpectedMovement = 0x25;
    /// <summary>I5: the action timeout 60.0f (0x42700000).</summary>
    public static readonly float AnimationTimeoutSec = BitConverter.Int32BitsToSingle(0x42700000);

    // C1/H2: +0x11C, 0 from the ctor (0x00609A58..0x00609A9A); written only by AlwaysHandle (0x00609BF8..0x00609C0A), kept across runs.
    private int _side;
    private readonly bool _subscribed;

    /// <summary>Unbound: no robot to subscribe to, so <c>AlwaysHandle</c> never runs; Init reports MISSING and fails.</summary>
    public ReactToUnexpectedMovementBehavior() : base("ReactToUnexpectedMovement", "ReactToUnexpectedMovement") { }

    /// <summary>C1: subscribes to the UnexpectedMovement broadcast (tag 0x3C) for the behaviour's lifetime.</summary>
    public ReactToUnexpectedMovementBehavior(CozmoRobot robot) : this()
    {
        // H1/H2/H3: AlwaysHandle runs inside the broadcast (Sensors' synchronous in-engine delivery), before the manager's switch.
        robot.Sensors.UnexpectedMovementDetectedInternal += r => Volatile.Write(ref _side, (byte)r.Side);
        _subscribed = true;
        // I6/K1: StartActing's completion arrives through the IBehavior ctor's RobotCompletedAction subscription (0x005BBD5E..7A).
        BindActionCompletionOwner(robot);
    }

    /// <summary>H2: <c>+0x11C</c>, the movementSide of the last UnexpectedMovement received (0 = Unknown until then).</summary>
    public UnexpectedMovementSide Side => (UnexpectedMovementSide)Volatile.Read(ref _side);

    // R1: base +0x20 0x005BF04C returns 0 (not off treads). R2: base +0x24 0x0059EC12 returns 0 (not with robot+0x34A).
    // R3: +0x28 0x00609C1A returns 1 (runnable while carrying). R5: IsRunnableInternal 0x00609ABC returns 1 (the base default).
    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => true;
    // U1: base IBehavior::UpdateInternal (vtable +0xC): the behaviour ends when its action ends.
    protected override bool KeepsRunningWithoutAction => HasCurrentAction;

    protected override void OnStart()
    {
        var robot = Context.Robot;
        if (!_subscribed)
        {
            ReportMissing("REACT-UNEXPECTED-MOVEMENT-1 C1/H2 (0x00609A58, 0x00609BF8): this instance has no robot UnexpectedMovement subscription, so +0x11C is not maintained; Init not performed");
            InitFailed = true;
            return;
        }
        if (robot.Engine.Robot is null)
        {
            ReportMissing("REACT-UNEXPECTED-MOVEMENT-1 I5/I6 (0x00609B12..0x00609B92): no robot+0x250 ActionList owner for the TriggerLiftSafeAnimationAction; Init not performed");
            InitFailed = true;
            return;
        }
        if (robot.Engine.Needs is not { } needs)
        {
            ReportMissing("REACT-UNEXPECTED-MOVEMENT-1 I2 (0x00609B06..0x00609B10): no SevereNeeds owner for [[robot+0x264]+0x30]+0x14; Init not performed");
            InitFailed = true;
            return;
        }
        BindActionCompletionOwner(robot); // no-op when the ctor bound it
        // I1 0x00609ACC..0x00609AF4: TriggerEmotionEvent("ReactToUnexpectedMovement").
        bool known = Context.Mood?.Trigger(EmotionEventName, Clock() / 1000.0) ?? false;
        Log($"emotion event {EmotionEventName}: {(Context.Mood is null ? "no mood attached" : known ? "applied" : "not in the loaded mood model")}");
        // I2 0x00609B06..0x00609B10: the selected severe need.
        int need = needs.SevereNeeds.Selected;
        // I4 0x00609B1E..0x00609B22: `cmp r7,#2; moveq r2,#4`: BODY iff +0x11C == 2 (BACK).
        int tracksToLock = Volatile.Read(ref _side) == 2 ? 4 : 0;
        // I3 0x00609B30..0x00609B44: 0x1AE when need == 0 (Repair), 0x1AD when need == 1 (Energy), else 0x1AC.
        int trigger = need == 0 ? 0x1AE : need == 1 ? 0x1AD : 0x1AC;
        // I5 0x00609B12..0x00609B4E: TriggerLiftSafeAnimationAction(robot, trigger, 1, true, lock, 60.0f, false).
        var action = new TriggerLiftSafeAnimationAction(robot, new((AnimationTrigger)trigger, 1, true, tracksToLock, AnimationTimeoutSec, false));
        // I6 0x00609B52..0x00609B92: StartActing (the void() callback overload); Init returns 0.
        StartActing(action, OnActionCompleted);
    }

    // K1 0x005BF790: the wrapper ignores the ActionResult. K2 0x00609C5A..0x00609C60: BehaviorObjectiveAchieved(0x25, true).
    private void OnActionCompleted(RobotCompletedAction completed) =>
        Context.Robot.Engine.ReportBehaviorObjective(ObjectiveReactedToUnexpectedMovement, "ReactedToUnexpectedMovement", Id);
    // T1 0x00609C1E `bx lr`: no StopInternal of its own. P1 0x005BDA94..0x005BDAC8: the base ResumeInternal (Init again).
}

/// <summary>
/// <c>BehaviorReactToMotorCalibration</c> (0x0060658C..0x00606A0E): take the reaction lock and start a
/// <c>WaitAction</c> of 5 s (0x40A00000) as a timeout; on each <c>MotorCalibration</c> report
/// (<c>HandleWhileRunning</c>, tag 0x1E) stop as soon as <c>Robot::IsHeadCalibrated</c> and
/// <c>Robot::IsLiftCalibrated</c> both hold; if the wait runs out first, warn
/// <c>"Calibration didn't complete (lift: %d, head: %d)"</c>. No animation is played. The trigger that starts it
/// is a calibration the robot began by itself (<c>calibStarted &amp;&amp; autoStarted</c>).
/// </summary>
public sealed class ReactToMotorCalibrationBehavior : SteppedBehavior
{
    private long _generation;
    protected override bool KeepsRunningWithoutAction => HasCurrentAction;
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => context?.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion() => Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version) => Interlocked.Read(ref _generation) == version;
    protected override bool HostStopContinuationCurrent(long version) => Interlocked.Read(ref _generation) == unchecked(version + 1);
    public const double TimeoutSec = 5.0;

    private Cozmo.Robot.Manipulation.ManipulationSystem? _manipulation;
    internal void BindManipulation(Cozmo.Robot.Manipulation.ManipulationSystem manipulation) => _manipulation = manipulation;
    private Action? _completionLease;
    public ReactToMotorCalibrationBehavior(Cozmo.Robot.Manipulation.ManipulationSystem? manipulation = null) : base("ReactToMotorCalibration", "ReactToMotorCalibration")
    {
        _manipulation = manipulation;
    }

    /// <summary>Whether the last run saw both motors calibrated before the timeout.</summary>
    public bool Completed { get; private set; }
    private Action<Cozmo.Protocol.MotorCalibration>? _calibrationHandler;

    protected override void OnStart()
    {
        // fidelity: M7-014
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.ReactToMotorCalibration);
        _completionLease = AcquireActionCompletionOwner(Context.Robot);
        Completed = false;
        long epoch = CaptureHostExecutionVersion();
        _calibrationHandler = _ =>
        {
            using var owner = Context.Robot.Engine.EnterSynchronousOwner();
            var state = Context.Robot.State;
            if (!EngineRunning || !HostExecutionVersionCurrent(epoch) || !state.HeadCalibrated || !state.LiftCalibrated) return;
            Completed = true;
            StopActing(true, false);
        };
        Context.Robot.Sensors.MotorCalibrationReportedInternal += _calibrationHandler;
        if (_manipulation is null)
        {
            ReportMissing("M7-019 ReactToMotorCalibration WaitAction requires the manipulation/action-list owner");
            InitFailed = true;
            return;
        }
        StartActing(new Cozmo.Robot.Manipulation.NativeTimedWaitRunner(_manipulation, (float)TimeoutSec), _ => { });
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        Interlocked.Increment(ref _generation);
        if (_calibrationHandler is { } handler)
            Context.Robot.Sensors.MotorCalibrationReportedInternal -= handler;
        _calibrationHandler = null;
        _completionLease?.Invoke(); _completionLease = null;
    }
}

/// <summary>
/// <c>BehaviorReactToFrustration</c> (0x00605904..0x00606084) as configured by the shipped
/// <c>reactToFrustrationMinor.json</c>: <c>LoadJson</c> reads <c>anim</c> (an AnimationTrigger name) and
/// <c>finalEmotionEvent</c>; <c>TransitionToReaction</c> plays the animation with a
/// <c>TriggerLiftSafeAnimationAction</c>; <c>AnimationComplete</c> fires the final emotion event on the mood
/// manager and, when <c>randomDriveMaxDist_mm</c> is above 1e-5, drives to a random pose. The Minor config has
/// no drive, so it completes there. The Major config (<c>randomDriveMinDist_mm</c> 150, <c>randomDriveMaxDist_mm</c>
/// 400, <c>randomDriveMinAngle_deg</c> 80, <c>randomDriveMaxAngle_deg</c> 180) drives to a random pose: a
/// distance in [min, max] at a heading offset of ±[minAngle, maxAngle] from the robot's, through a
/// <c>DriveToPoseAction</c> (M13, on the lattice planner when loaded). <c>InitInternal</c> also pushes driving
/// animations, which only matter for the drive (DEFERRED).
/// </summary>
public sealed class ReactToFrustrationBehavior : SteppedBehavior
{
    private Action? _completionLease;
    private long _generation;
    private ScopedAcquisition? _drivingLease;
    private readonly Dictionary<long, (Cozmo.Robot.Vision.Pose3d? Goal, Cozmo.Robot.Vision.Pose3d? Parent)> _retiredPoses = new();
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => context?.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion() => Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version) => Interlocked.Read(ref _generation) == version;
    protected override bool HostStopContinuationCurrent(long version) => Interlocked.Read(ref _generation) == unchecked(version + 1);
    protected override bool KeepsRunningWithoutAction => HasCurrentAction;
    private Cozmo.Robot.Vision.Pose3d? _driveParent;
    private readonly FrustrationStrategy? _strategy;
    private Cozmo.Robot.Manipulation.ManipulationSystem? _m;

    /// <summary>The shipped Major configuration, with its random drive on the manipulation system's paths.</summary>
    public static ReactToFrustrationBehavior Major(Cozmo.Robot.Manipulation.ManipulationSystem m, FrustrationStrategy? strategy = null) =>
        new("ReactToFrustrationMajor", AnimationTrigger.FrustratedByFailureMajor, "FinishedMajorFrustration", strategy)
            { _m = m, RandomDriveMinDistMm = 150, RandomDriveMaxDistMm = 400, RandomDriveMinAngleDeg = 80, RandomDriveMaxAngleDeg = 180 };

    internal void BindManipulation(Cozmo.Robot.Manipulation.ManipulationSystem manipulation) => _m = manipulation;
    public double RandomDriveMinDistMm { get; init; }
    public double RandomDriveMaxDistMm { get; init; }
    public double RandomDriveMinAngleDeg { get; init; }
    public double RandomDriveMaxAngleDeg { get; init; }
    /// <summary>The random goal of the last drive, for tests and the log.</summary>
    public Cozmo.Robot.Vision.Pose3d? DriveGoal { get; private set; }

    public ReactToFrustrationBehavior(string id, AnimationTrigger animation, string finalEmotionEvent,
                                      FrustrationStrategy? strategy = null)
        : base(id, "ReactToFrustration")
    {
        Animation = animation;
        FinalEmotionEvent = finalEmotionEvent;
        _strategy = strategy;
    }

    /// <summary>The shipped Minor configuration.</summary>
    public static ReactToFrustrationBehavior Minor(FrustrationStrategy? strategy = null) =>
        new("ReactToFrustrationMinor", AnimationTrigger.FrustratedByFailure, "FinishedMinorFrustration", strategy);

    public AnimationTrigger Animation { get; }
    public string FinalEmotionEvent { get; }

    internal static (float radians, float distance) RandomDriveScalars(float minAngle, float maxAngle, float minDistance, float maxDistance, Func<double> draw)
    {
        float angle = (float)(((double)maxAngle - minAngle) * draw() + minAngle);
        if (draw() < 0.5) angle = -angle;
        float radians = angle * BitConverter.Int32BitsToSingle(0x3C8EFA35);
        float distance = (float)(((double)maxDistance - minDistance) * draw() + minDistance);
        return (radians, distance);
    }

    protected override void OnStart()
    {
        long epoch = CaptureHostExecutionVersion();
        DriveGoal?.Dispose(); DriveGoal = null;
        _driveParent?.Dispose(); _driveParent = null;
        var driving = _drivingLease = new ScopedAcquisition();
        Context.Robot.Animations.DrivingAnimations.PushOwned(new DrivingAnimations((AnimationTrigger)0x94, (AnimationTrigger)0x90, (AnimationTrigger)0x8C), Id, driving);
        if (!HostExecutionVersionCurrent(epoch)) return;
        _completionLease = AcquireActionCompletionOwner(Context.Robot);
        if ((int)Animation == 0x23F) { InitFailed = true; return; }
        var reaction = new TriggerLiftSafeAnimationAction(Context.Robot, new(Animation, 1, true, 0, 60f, false));
        if (!HostExecutionVersionCurrent(epoch)) { reaction.RetireStaleConstructor(() => false); return; }
        StartActing(reaction, _ =>
        {
            double nowSec = Clock() / 1000.0;
            if (!string.IsNullOrEmpty(FinalEmotionEvent))
            {
                bool known = Context.Mood?.Trigger(FinalEmotionEvent, nowSec) ?? false;
                Log($"emotion event {FinalEmotionEvent}: {(Context.Mood is null ? "no mood attached" : known ? "applied" : "not in the loaded mood model")}");
            }
            if (!HostExecutionVersionCurrent(epoch)) return;
            // The strategy stamps the clock it also evaluates the cooldown on (ShippedReactionStrategies hands
            // one clock to every strategy); a strategy built without one is stamped with this behaviour's
            // clock in seconds, and its caller must drive the manager from that same clock.
            if (_strategy is not null)
            {
                if (_strategy.HasClock) _strategy.AnimationComplete(); else _strategy.AnimationComplete(nowSec);
            }
            if (!HostExecutionVersionCurrent(epoch)) return;
            bool driveEnabled = (float)RandomDriveMaxDistMm > BitConverter.Int32BitsToSingle(0x3727C5AC);
            float ang = 0f, dist = 0f;
            if (driveEnabled)
            {
                var rnd = Context.Robot.Engine.ContextRandom;
                (ang, dist) = RandomDriveScalars((float)RandomDriveMinAngleDeg, (float)RandomDriveMaxAngleDeg,
                    (float)RandomDriveMinDistMm, (float)RandomDriveMaxDistMm, rnd.GetNextDbl);
            }
            if (driveEnabled && _m is not null && Context.Robot.Engine.Robot is { } robot)
            {
                _driveParent = new Cozmo.Robot.Vision.Pose3d(Cozmo.Robot.Vision.Rotation3d.FromAngleAxis(ang, new(0, 0, 1)), Cozmo.Robot.Vision.Vec3.Zero, "", robot.MainPose);
                var goal = new Cozmo.Robot.Vision.Pose3d(Cozmo.Robot.Vision.Rotation3d.Identity, new(dist, 0, 0), "", _driveParent);
                DriveGoal = goal;
                Log($"random drive: {dist:F0} mm at {ang * 180 / Math.PI:F0} deg");
                if (!HostExecutionVersionCurrent(epoch)) return;
                var drive = new Cozmo.Robot.Manipulation.DriveToPoseAction(_m, false, false);
                drive.SetNativeGoals(new[] { goal });
                StartActing(drive, _ => { }, () => HostExecutionVersionCurrent(epoch));
                if (!HostExecutionVersionCurrent(epoch)) return;
                Context.Robot.Engine.ReportBehaviorObjective(0x1D, "ReactedToFrustration", Id);
                return;
            }
            if (driveEnabled)
                ReportMissing("M7-019 ReactToFrustration random drive requires its manipulation and robot-pose owners");
            if (!HostExecutionVersionCurrent(epoch)) return;
            Context.Robot.Engine.ReportBehaviorObjective(0x1D, "ReactedToFrustration", Id);
            if (!HostExecutionVersionCurrent(epoch)) return;
            Finish();
        }, () => HostExecutionVersionCurrent(epoch));
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        long old = CaptureHostExecutionVersion();
        long stopped = Interlocked.Increment(ref _generation);
        var goal = DriveGoal; var parent = _driveParent; DriveGoal = null; _driveParent = null;
        _retiredPoses[old] = (goal, parent);
        var lease = _completionLease; _completionLease = null; lease?.Invoke();
        var driving = _drivingLease; _drivingLease = null;
        if (driving is not null)
        {
            var handler = Context.Robot.Animations.DrivingAnimations;
            var removed = handler.RemoveScoped(Id, () => Interlocked.Read(ref _generation) == stopped);
            if (!removed.Completed) handler.RemoveOwned(driving);
            driving.Cancel();
        }
    }

    protected override void OnStopped(BehaviorStopReason reason, long runVersion)
    {
        if (_retiredPoses.Remove(runVersion, out var poses)) { poses.Goal?.Dispose(); poses.Parent?.Dispose(); }
    }
}
