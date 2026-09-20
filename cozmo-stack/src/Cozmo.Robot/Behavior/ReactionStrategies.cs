using Cozmo.Protocol;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// What the engine calls an <c>IReactionTriggerStrategy</c>: the thing that decides, every tick, whether
/// one <see cref="ReactionTrigger"/> should fire its behaviour. <c>BehaviorManager::CheckReactionTriggerStrategies</c>
/// (0x005A3550) walks the 22 triggers and asks each strategy <c>ShouldTriggerBehavior</c>.
///
/// The strategies are built by <c>ReactionTriggerStrategyFactory::CreateReactionTriggerStrategy</c>
/// (0x0060D5A0), whose switch on the trigger was read case by case; the addresses are in each
/// implementation's <see cref="Basis"/>. Two shapes recur:
/// <list type="bullet">
/// <item><b>a state callback</b> — <c>ReactionTriggerStrategyGeneric::SetShouldTriggerCallback</c> with a
/// lambda over the robot: RobotPickedUp, RobotOnBack, RobotOnFace, RobotOnSide;</item>
/// <item><b>a latched event</b> — <c>ConfigureRelevantEvents(tags, filter)</c>: the generic wants-to-run
/// strategy (<c>StrategyGeneric</c>) sets a flag when a subscribed message passes the filter
/// (<c>AlwaysHandleInternal</c> 0x00613998), and <c>WantsToRunInternal</c> (0x00613948) returns that flag
/// and clears it. CliffDetected, MotorCalibration, RobotFalling (with a 3000 ms window),
/// ReturnedToTreads, UnexpectedMovement.</item>
/// </list>
/// plus the purpose-built <c>StrategyRobotShaken</c>, <c>StrategyRobotPlacedOnSlope</c> and
/// <c>ReactionTriggerStrategyFrustration</c>. The generic strategy's result is
/// <c>(forced || behaviour.IsRunnable) &amp;&amp; wantsToRun</c> (0x0060F3BC); the force flag is the
/// app's <c>ExecuteReactionTrigger</c> debug message and is not modelled.
/// </summary>
public interface IReactionTriggerStrategy
{
    ReactionTrigger Trigger { get; }

    /// <summary>Where in the binary this decision was read, and what it is.</summary>
    string Basis { get; }

    /// <summary>
    /// Whether the trigger should fire now. <paramref name="current"/> is the reaction the manager is
    /// running, if any; some strategies refuse to re-fire over themselves.
    /// </summary>
    bool ShouldTrigger(BehaviorContext context, ReactionTrigger? current, double nowSec);
}

/// <summary>
/// A trigger decided by a predicate over the robot's current state: the engine's
/// <c>SetShouldTriggerCallback</c> lambdas.
/// </summary>
public sealed class StateCallbackStrategy : IReactionTriggerStrategy
{
    private readonly Func<CozmoRobot, bool> _predicate;

    public StateCallbackStrategy(ReactionTrigger trigger, Func<CozmoRobot, bool> predicate, string basis)
    {
        Trigger = trigger; _predicate = predicate; Basis = basis;
    }

    public ReactionTrigger Trigger { get; }
    public string Basis { get; }

    public bool ShouldTrigger(BehaviorContext context, ReactionTrigger? current, double nowSec) =>
        _predicate(context.Robot);
}

/// <summary>
/// A trigger latched by an event: the engine's <c>StrategyGeneric</c> with relevant events. The latch is
/// set when a subscribed event passes its filter and is consumed by the next evaluation, exactly as
/// <c>WantsToRunInternal</c> clears <c>+0x80</c> after reading it. With a window (RobotFalling: 3000 ms)
/// the reaction strategy additionally requires an event inside the window
/// (<c>ReactionTriggerStrategyGeneric::ShouldTriggerBehaviorInternal</c> 0x0060F3EC..0x0060F432).
/// </summary>
public sealed class LatchedEventStrategy : IReactionTriggerStrategy, IDisposable
{
    private readonly object _gate = new();
    private readonly Action? _unsubscribe;
    private readonly double? _windowSec;
    private bool _latched;
    private double _lastEventSec = double.NegativeInfinity;
    private readonly Func<double> _clock;

    /// <param name="subscribe">Hooks the event source; it is handed the latch to call and returns the unhook.</param>
    public LatchedEventStrategy(ReactionTrigger trigger, Func<Action, Action> subscribe, string basis,
                                double? windowSec = null, Func<double>? clockSec = null)
    {
        Trigger = trigger; Basis = basis; _windowSec = windowSec;
        _clock = clockSec ?? (() => Environment.TickCount64 / 1000.0);
        _unsubscribe = subscribe(Latch);
    }

    public ReactionTrigger Trigger { get; }
    public string Basis { get; }

    /// <summary>Whether an event is waiting to be consumed.</summary>
    public bool Latched { get { lock (_gate) return _latched; } }

    /// <summary>Records that a relevant event passed the filter.</summary>
    public void Latch()
    {
        lock (_gate) { _latched = true; _lastEventSec = _clock(); }
    }

    public bool ShouldTrigger(BehaviorContext context, ReactionTrigger? current, double nowSec)
    {
        bool wants;
        lock (_gate)
        {
            wants = _latched;
            _latched = false;
            if (wants && _windowSec is { } w && _clock() - _lastEventSec > w) wants = false;
        }
        return wants;
    }

    public void Dispose() => _unsubscribe?.Invoke();
}

/// <summary>
/// <c>StrategyRobotShaken::WantsToRunInternal</c> at 0x006146CC: the filtered accelerometer magnitude
/// (<c>Robot+0x37c</c>, the engine's 0.95/0.05 filter) is above 16000 (0x467A0000). The behaviour it
/// starts then keeps looping while that value stays above 13000.
/// </summary>
public sealed class RobotShakenStrategy : IReactionTriggerStrategy
{
    public const float ShakenAccelThreshold = 16000f;
    public ReactionTrigger Trigger => ReactionTrigger.RobotShaken;
    public string Basis => "StrategyRobotShaken::WantsToRunInternal 0x006146CC: Robot+0x37c (filtered |accel|) > 16000";

    public bool ShouldTrigger(BehaviorContext context, ReactionTrigger? current, double nowSec) =>
        context.Robot.Sensors.FilteredAccelMagnitude > ShakenAccelThreshold;
}

/// <summary>
/// <c>StrategyRobotPlacedOnSlope::WantsToRunInternal</c> at 0x0061455C, with the constants its
/// constructor (0x006144FC) stores: the robot wants to react to a slope when
/// <list type="bullet">
/// <item>the pitch is strictly inside (10°, 55°) (<c>+0x28</c> = 10.0, <c>+0x2C</c> = 55.0);</item>
/// <item>the largest |gyro component| has been at or below 0.01 rad/s for more than 0.4 s
/// (<c>+0x30</c> = 0.01, <c>+0x38</c> = 0.4 s; the last time any axis exceeded it is kept at <c>+0x18</c>);</item>
/// <item>the robot is picked up now, or was put down less than 1.5 s ago (<c>+0x40</c> = 1.5 s; the
/// picked-up flag <c>Robot+0x349</c> is the IS_PICKED_UP status bit, the time it was last set is <c>+0x20</c>);</item>
/// <item>the off-treads state is OnTreads or InAir (<c>Robot+0x355 &lt; 2</c>).</item>
/// </list>
/// The reaction strategy that owns it (<c>ReactionTriggerStrategyRobotPlacedOnSlope</c> 0x00612EB0) then
/// requires the behaviour to be runnable.
/// </summary>
public sealed class PlacedOnSlopeStrategy : IReactionTriggerStrategy
{
    public const float MinPitchDeg = 10f, MaxPitchDeg = 55f;
    public const float GyroQuietRadps = 0.01f;
    public const double QuietForSec = 0.4, PutDownWithinSec = 1.5;

    // Both start at 0, as the constructor zeroes +0x18 and +0x20: on a fresh strategy the gyro counts as
    // quiet since time zero and the robot as never picked up.
    private double _lastGyroActiveSec = 0;
    private double _lastPickedUpSec = double.NegativeInfinity;

    public ReactionTrigger Trigger => ReactionTrigger.RobotPlacedOnSlope;
    public string Basis => "StrategyRobotPlacedOnSlope::WantsToRunInternal 0x0061455C; ctor 0x006144FC: pitch in (10, 55) deg, " +
                           "max |gyro| <= 0.01 rad/s for > 0.4 s, picked up or put down < 1.5 s ago, OffTreadsState < 2";

    public bool ShouldTrigger(BehaviorContext context, ReactionTrigger? current, double nowSec)
    {
        var sensors = context.Robot.Sensors;
        var gyro = sensors.Gyroscope ?? default;
        float maxGyro = MathF.Max(MathF.Abs(gyro.X), MathF.Max(MathF.Abs(gyro.Y), MathF.Abs(gyro.Z)));
        if (maxGyro > GyroQuietRadps) _lastGyroActiveSec = nowSec;
        double quietFor = nowSec - _lastGyroActiveSec;

        float pitchDeg = (sensors.PitchRad ?? 0f) * (180f / MathF.PI);
        bool pitchOutside = pitchDeg <= MinPitchDeg || pitchDeg >= MaxPitchDeg;

        bool pickedUp = sensors.PickedUp;
        if (pickedUp) _lastPickedUpSec = nowSec;
        bool putDownRecently = !pickedUp && nowSec - _lastPickedUpSec < PutDownWithinSec;

        if (pitchOutside || quietFor <= QuietForSec) return false;
        bool wants = pickedUp || putDownRecently;
        return wants && sensors.OffTreadsState < OffTreadsState.OnBack;
    }
}

/// <summary>
/// <c>ReactionTriggerStrategyFrustration::ShouldTriggerBehaviorInternal</c> at 0x0060EE6E, with
/// <c>LoadJson</c> (0x0060ED88) reading <c>frustrationParams.maxConfidence</c> and <c>cooldownTime_s</c>:
/// fire when the current reaction is not already Frustration, the mood's Confident axis is below
/// <c>maxConfidence</c>, and either no frustration animation has completed yet or more than the cooldown
/// has passed since one did (<c>AnimationComplete</c> 0x0060EEF4 stamps the time). The shipped map gives
/// Minor −0.6 with a 60 s cooldown and Major −0.9 with none.
/// </summary>
public sealed class FrustrationStrategy : IReactionTriggerStrategy
{
    private readonly Func<double>? _clock;

    /// <param name="clockSec">
    /// The clock both the cooldown stamp and its evaluation use. When given, <see cref="ShouldTrigger"/>
    /// ignores the manager's <c>nowSec</c> and <see cref="AnimationComplete()"/> stamps this clock, so the
    /// behaviour's completion and the strategy's test share one time base (the engine's is BaseStationTimer
    /// for both). Without one, the manager's <c>nowSec</c> is the time base and the behaviour must stamp
    /// through <see cref="AnimationComplete(double)"/> with a value from that same source.
    /// </param>
    public FrustrationStrategy(float maxConfidence, float cooldownSec, Func<double>? clockSec = null)
    {
        MaxConfidence = maxConfidence; CooldownSec = cooldownSec; _clock = clockSec;
    }

    public float MaxConfidence { get; }
    public float CooldownSec { get; }
    /// <summary>Whether the strategy was built with its own clock (the shipped construction).</summary>
    public bool HasClock => _clock is not null;
    /// <summary>When a frustration animation last completed, seconds on the strategy's time base; null until one has.</summary>
    public double? LastAnimationCompleteSec { get; private set; }

    /// <summary>The engine's <c>AnimationComplete</c>, stamped on the strategy's own clock (requires one).</summary>
    public void AnimationComplete()
    {
        if (_clock is null) throw new InvalidOperationException("FrustrationStrategy has no clock; stamp with AnimationComplete(nowSec) on the manager's time base");
        LastAnimationCompleteSec = _clock();
    }

    public ReactionTrigger Trigger => ReactionTrigger.Frustration;
    public string Basis => "ReactionTriggerStrategyFrustration::ShouldTriggerBehaviorInternal 0x0060EE6E: current != Frustration, " +
                           $"mood.Confident < {MaxConfidence}, cooldown {CooldownSec}s since AnimationComplete";

    /// <summary>The engine's <c>AnimationComplete</c> with an explicit time on the manager's time base (a strategy with a clock ignores the argument).</summary>
    public void AnimationComplete(double nowSec) => LastAnimationCompleteSec = _clock?.Invoke() ?? nowSec;

    /// <summary>Whether the cooldown has elapsed, on the strategy's clock when it has one, else at the manager time given.</summary>
    public bool CooldownElapsed(double nowSec)
    {
        if (LastAnimationCompleteSec is not { } last) return true;
        double now = _clock?.Invoke() ?? nowSec;
        return now - last > CooldownSec;
    }

    public bool ShouldTrigger(BehaviorContext context, ReactionTrigger? current, double nowSec)
    {
        if (current == ReactionTrigger.Frustration) return false;
        if (context.Mood is not { } mood) return false;
        if (!(mood[EmotionType.Confident] < MaxConfidence)) return false;
        return CooldownElapsed(nowSec);
    }
}

/// <summary>
/// The shipped strategies this stack can drive, built against one robot. Each is the engine's own rule for
/// that trigger; the ones the factory builds from message subscriptions are wired to the sensor events
/// that carry the same information.
/// </summary>
public static class ShippedReactionStrategies
{
    /// <summary>
    /// The strategies for every trigger whose input this stack has. Cliff, falling and charger are listed
    /// for completeness; the M7 <see cref="ReactiveBehavior"/> already reacts to those through the raw reports.
    /// </summary>
    public static IReadOnlyList<IReactionTriggerStrategy> ForRobot(CozmoRobot robot, Func<double>? clockSec = null)
    {
        var sensors = robot.Sensors;
        return new IReactionTriggerStrategy[]
        {
            // factory case RobotPickedUp (0x0060D854): SetShouldTriggerCallback, lambda 0x0060DDCE
            new StateCallbackStrategy(ReactionTrigger.RobotPickedUp,
                r => r.Sensors.OffTreadsState == OffTreadsState.InAir,
                "CreateReactionTriggerStrategy 0x0060D854 -> SetShouldTriggerCallback lambda 0x0060DDCE: Robot+0x355 == 1 (InAir)"),
            // RobotOnBack (0x0060D8DA), lambda 0x0060DEB2
            new StateCallbackStrategy(ReactionTrigger.RobotOnBack,
                r => r.Sensors.OffTreadsState == OffTreadsState.OnBack,
                "CreateReactionTriggerStrategy 0x0060D8DA -> lambda 0x0060DEB2: Robot+0x355 == 2 (OnBack)"),
            // RobotOnFace (0x0060D8FC), lambda 0x0060DF16
            new StateCallbackStrategy(ReactionTrigger.RobotOnFace,
                r => r.Sensors.OffTreadsState == OffTreadsState.OnFace,
                "CreateReactionTriggerStrategy 0x0060D8FC -> lambda 0x0060DF16: Robot+0x355 == 5 (OnFace)"),
            // RobotOnSide (0x0060D91E), lambda 0x0060DF7E: (state - 3) < 2
            new StateCallbackStrategy(ReactionTrigger.RobotOnSide,
                r => r.Sensors.OffTreadsState is OffTreadsState.OnLeftSide or OffTreadsState.OnRightSide,
                "CreateReactionTriggerStrategy 0x0060D91E -> lambda 0x0060DF7E: (Robot+0x355 - 3) < 2 (OnLeftSide, OnRightSide)"),
            // ReturnedToTreads (0x0060D88C): relevant event RobotOffTreadsStateChanged (tag 53), filter 0x0060DE36: treadsState == OnTreads
            new LatchedEventStrategy(ReactionTrigger.ReturnedToTreads, latch =>
                {
                    void on(OffTreadsState from, OffTreadsState to) { if (to == OffTreadsState.OnTreads) latch(); }
                    sensors.OffTreadsStateChanged += on;
                    return () => sensors.OffTreadsStateChanged -= on;
                },
                "CreateReactionTriggerStrategy 0x0060D88C -> ConfigureRelevantEvents({RobotOffTreadsStateChanged}), filter lambda 0x0060DE36: state == 0 (OnTreads)",
                clockSec: clockSec),
            // MotorCalibration (0x0060D764): relevant event MotorCalibration (tag 30), filter 0x0060DCFA: calibStarted && autoStarted
            new LatchedEventStrategy(ReactionTrigger.MotorCalibration, latch =>
                {
                    void on(MotorCalibration _) => latch();
                    sensors.AutoCalibrationStarted += on;
                    return () => sensors.AutoCalibrationStarted -= on;
                },
                "CreateReactionTriggerStrategy 0x0060D764 -> ConfigureRelevantEvents({MotorCalibration}), filter lambda 0x0060DCFA: calibStarted && autoStarted",
                clockSec: clockSec),
            // UnexpectedMovement (0x0060D96A): relevant event UnexpectedMovement (tag 60), no filter
            new LatchedEventStrategy(ReactionTrigger.UnexpectedMovement, latch =>
                {
                    void on(UnexpectedMovementReport _) => latch();
                    sensors.UnexpectedMovementDetected += on;
                    return () => sensors.UnexpectedMovementDetected -= on;
                },
                "CreateReactionTriggerStrategy 0x0060D96A -> ConfigureRelevantEvents({UnexpectedMovement}) with no filter; the message comes from MovementComponent::CheckForUnexpectedMovement 0x0063E398",
                clockSec: clockSec),
            // RobotShaken (0x0060D956): purpose-built strategy
            new RobotShakenStrategy(),
            // RobotPlacedOnSlope (0x0060D878): purpose-built strategy
            new PlacedOnSlopeStrategy(),
            // Frustration (0x0060D73C): frustrationParams from the shipped map, Minor entry
            new FrustrationStrategy(maxConfidence: -0.6f, cooldownSec: 60f, clockSec),
        };
    }
}
