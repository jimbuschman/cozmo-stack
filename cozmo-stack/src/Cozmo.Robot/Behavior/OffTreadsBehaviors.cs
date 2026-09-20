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
    /// <summary>The cliff-sensor test: <c>(raw &gt;&gt; 4) &gt; 0x18</c>, i.e. raw ≥ 400, means calibrate rather than flip.</summary>
    public const int CliffRawCalibrateAt = 400;

    public ReactToRobotOnBackBehavior() : base("ReactToRobotOnBack", "ReactToRobotOnBack") { }

    protected override void OnStart() => FlipDownIfNeeded();

    private void FlipDownIfNeeded()
    {
        var sensors = Context.Robot.Sensors;
        if (sensors.OffTreadsState != OffTreadsState.OnBack)
        {
            Log("not on back: objective ReactedToRobotOnBack");
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
            Log("back on treads: objective ReactedToRobotOnBack");
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
            Log("righted: objective ReactedToRobotOnFace");
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
/// finding the robot no longer on a side ends the behaviour. The engine also reports the needs actions
/// <c>PlacedOnSide</c> (0x2F) and <c>BoredOnSide</c> (0x30); there is no needs system here.
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
        Log("held for 15 s: bored sequence (needs action BoredOnSide)");
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
        Scope.DisableReactions();
        double now = Clock();
        if (now - _lastRunMs < RepeatWithinSec * 1000 && PitchWasHigh)
        {
            float deg = (Context.Robot.Sensors.PitchRad ?? 0f) * (180f / MathF.PI);
            Log($"placed on a slope again within 10 s with the pitch still high ({deg:F1} deg): calibrating the head");
            CalibrateHead(() => { PitchWasHigh = false; Finish(); });
        }
        else PlayTrigger(AnimationTrigger.ReactToPerchedOnBlock, CheckPitch);
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
        Scope.DisableReactions();
        CurrentPhase = Phase.Shaking;
        MaxAccelMagnitude = 0;
        ShakenDurationSec = 0;
        Played = Reaction.None;
        LoopShake();
    }

    private void LoopShake()
    {
        if (CurrentPhase != Phase.Shaking) return;
        PlayTrigger(AnimationTrigger.DizzyShakeLoop, LoopShake);
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
                else if (!Busy)
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
                Log($"back on treads: dizzy reaction {Played} (needs action Dizzy{Played})");
                PlayTrigger(trigger, () => { });
                CurrentPhase = Phase.Finishing;
                break;
            }
            case Phase.Finishing:
                if (!Busy)
                {
                    Log("objective ReactedToRobotShaken");
                    Finish();
                }
                break;
        }
    }

    protected override void OnStop(BehaviorStopReason reason) =>
        Log($"DAS robot.dizzy_reaction: shakenDuration = {ShakenDurationSec:F3}s, maxShakingAccelMag = {MaxAccelMagnitude:F1}, reactionPlayed = '{Played}'");
}

/// <summary>
/// <c>BehaviorReactToUnexpectedMovement</c> (0x00609A58..0x00609C74). It subscribes to the
/// <c>UnexpectedMovement</c> message and keeps its <c>movementSide</c> (<c>AlwaysHandle</c>, byte +5 →
/// <c>+0x11c</c>). <c>InitInternal</c> fires the emotion event <c>"ReactToUnexpectedMovement"</c> on the
/// mood manager, then plays <see cref="AnimationTrigger.ReactToUnexpectedMovement"/> (0x1AC) with a
/// <c>TriggerLiftSafeAnimationAction</c>, locking the body track (<c>tracksToLock = 4</c>) when the movement
/// came from <see cref="UnexpectedMovementSide.Back"/> so the robot does not drive during the reaction; when
/// it ends, objective <c>ReactedToUnexpectedMovement</c>. The severe-needs variants
/// (<c>_Severe_Energy</c> 0x1AD, <c>_Severe_Repair</c> 0x1AE) depend on the needs system and are not modelled.
/// </summary>
public sealed class ReactToUnexpectedMovementBehavior : SteppedBehavior
{
    public const string EmotionEventName = "ReactToUnexpectedMovement";

    public ReactToUnexpectedMovementBehavior() : base("ReactToUnexpectedMovement", "ReactToUnexpectedMovement") { }

    /// <summary>The side the last run reacted to.</summary>
    public UnexpectedMovementSide Side { get; private set; }

    protected override void OnStart()
    {
        var report = Context.Robot.Sensors.UnexpectedMovement.Last;
        Side = report?.Side ?? UnexpectedMovementSide.Unknown;
        bool known = Context.Mood?.Trigger(EmotionEventName, Clock() / 1000.0) ?? false;
        Log($"emotion event {EmotionEventName}: {(Context.Mood is null ? "no mood attached" : known ? "applied" : "not in the loaded mood model")}");
        var suppress = Side == UnexpectedMovementSide.Back ? AnimationTrack.Body : AnimationTrack.None;
        PlayTrigger(AnimationTrigger.ReactToUnexpectedMovement, () =>
        {
            Log("objective ReactedToUnexpectedMovement");
            Finish();
        }, suppress);
    }
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
    public const double TimeoutSec = 5.0;

    public ReactToMotorCalibrationBehavior() : base("ReactToMotorCalibration", "ReactToMotorCalibration") { }

    /// <summary>Whether the last run saw both motors calibrated before the timeout.</summary>
    public bool Completed { get; private set; }

    protected override void OnStart()
    {
        Scope.DisableReactions();
        var state = Context.Robot.State;
        Completed = false;
        WaitUntil(() => !state.HeadCalibrating && !state.LiftCalibrating && state.HeadCalibrated && state.LiftCalibrated,
            TimeoutSec, ok =>
            {
                Completed = ok;
                Log(ok ? "head and lift report calibrated"
                       : $"Calibration didn't complete (lift: {(state.LiftCalibrated && !state.LiftCalibrating ? 1 : 0)}, head: {(state.HeadCalibrated && !state.HeadCalibrating ? 1 : 0)})");
                Finish();
            }, "both motors calibrated");
    }
}

/// <summary>
/// <c>BehaviorReactToFrustration</c> (0x00605904..0x00606084) as configured by the shipped
/// <c>reactToFrustrationMinor.json</c>: <c>LoadJson</c> reads <c>anim</c> (an AnimationTrigger name) and
/// <c>finalEmotionEvent</c>; <c>TransitionToReaction</c> plays the animation with a
/// <c>TriggerLiftSafeAnimationAction</c>; <c>AnimationComplete</c> fires the final emotion event on the mood
/// manager and, when <c>randomDriveMaxDist_mm</c> is above 1e-5, drives to a random pose. The Minor config has
/// no drive, so it completes there; the Major config's random <c>DriveToPoseAction</c> needs path planning and
/// is not built. <c>InitInternal</c> also pushes driving animations, which only matter for the drive.
/// </summary>
public sealed class ReactToFrustrationBehavior : SteppedBehavior
{
    private readonly FrustrationStrategy? _strategy;

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

    protected override void OnStart()
    {
        PlayTrigger(Animation, () =>
        {
            double nowSec = Clock() / 1000.0;
            bool known = Context.Mood?.Trigger(FinalEmotionEvent, nowSec) ?? false;
            Log($"emotion event {FinalEmotionEvent}: {(Context.Mood is null ? "no mood attached" : known ? "applied" : "not in the loaded mood model")}");
            _strategy?.AnimationComplete(nowSec);
            Finish();
        });
    }
}
