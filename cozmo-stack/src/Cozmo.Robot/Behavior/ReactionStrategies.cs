using Cozmo.Protocol;

namespace Cozmo.Robot.Behavior;

// fidelity: M10-003, M10-004
/// <summary>
/// What a strategy sees when <c>BehaviorManager::CheckReactionTriggerStrategies</c> asks it
/// <c>ShouldTriggerBehavior(robot, behavior)</c>: the behaviour context, BaseStationTimer seconds, the current reaction
/// trigger (manager+0x1C → +0x10; null is 0x16 NoneTrigger, C5) and whether a behaviour is running (IBehavior+0xA1,
/// gap1 2a).
/// </summary>
public sealed class ReactionContext
{
    private readonly Func<IBehavior, bool>? _isRunning;

    public ReactionContext(BehaviorContext context, double nowSec, ReactionTrigger? currentTrigger = null,
                           Func<IBehavior, bool>? isRunning = null)
    {
        Context = context; NowSec = nowSec; CurrentTrigger = currentTrigger; _isRunning = isRunning;
    }

    public BehaviorContext Context { get; }
    /// <summary>BaseStationTimer::GetCurrentTimeInSeconds for this tick (the caller passes it).</summary>
    public double NowSec { get; }
    /// <summary>GetCurrentReactionTrigger (0x5A19EC); null is NoneTrigger 0x16.</summary>
    public ReactionTrigger? CurrentTrigger { get; }

    /// <summary>IBehavior+0xA1, "is running" (gap1 2a): the manager's current behaviour.</summary>
    public bool IsRunning(IBehavior behavior) => _isRunning?.Invoke(behavior) ?? false;

    /// <summary>
    /// <c>beh+0xA1 || IsRunnable</c> (C14, C15, gap1 2c): IsRunnableBase returns true for the behaviour already running (2b).
    /// </summary>
    public bool RunningOrRunnable(IBehavior behavior) => IsRunning(behavior) || behavior.IsRunnable(Context);
}

// fidelity: M10-003, M10-004
/// <summary>
/// The engine's <c>IReactionTriggerStrategy</c> with the vtable slots the M10 rows read (gap1 "Strategy vtable layout"):
/// +0x08 shouldResumeLast, +0x0C CanInterruptOther, +0x10 CanInterruptSelf, +0x1C EnabledStateChanged, and
/// ShouldTriggerBehavior (C6) over +0x24 ShouldTriggerBehaviorInternal and +0x20 SetupForceTriggerBehavior. The trigger
/// byte is strategy+0x18.
/// </summary>
public interface IReactionTriggerStrategy
{
    ReactionTrigger Trigger { get; }

    /// <summary>Where in the binary this decision was read, and what it is.</summary>
    string Basis { get; }

    /// <summary>vslot +0x08 (C9, C10).</summary>
    bool ShouldResumeLast { get; }
    /// <summary>vslot +0x0C (C5).</summary>
    bool CanInterruptOtherTriggeredBehavior { get; }
    /// <summary>vslot +0x10 (C5).</summary>
    bool CanInterruptSelf { get; }

    /// <summary><c>IReactionTriggerStrategy::ShouldTriggerBehavior(robot, behavior)</c> (C6).</summary>
    bool ShouldTriggerBehavior(ReactionContext rc, IBehavior behavior);

    /// <summary>vslot +0x1C, called by the lock add/remove paths (gap1 4b, 4c, 4j).</summary>
    void EnabledStateChanged(BehaviorContext context, bool enabled);

    /// <summary>The manager whose trigger map holds this strategy (set by <see cref="BehaviorManager.AddReaction"/>).</summary>
    BehaviorManager? Manager { get; set; }
}

// fidelity: M10-003, M10-004
/// <summary>
/// The base <c>IReactionTriggerStrategy</c>: C6 ShouldTriggerBehavior. If forced (+0x29), call Internal; if that returns
/// false, call SetupForceTriggerBehavior; clear +0x29 and return true. Otherwise return Internal.
/// The writer of +0x29 is not in the rows (the manager's NOT DONE list); <see cref="Forced"/> is settable for that reason only.
/// </summary>
public abstract class ReactionTriggerStrategy : IReactionTriggerStrategy
{
    public abstract ReactionTrigger Trigger { get; }
    public abstract string Basis { get; }
    public abstract bool ShouldResumeLast { get; }
    public abstract bool CanInterruptOtherTriggeredBehavior { get; }
    public abstract bool CanInterruptSelf { get; }
    public BehaviorManager? Manager { get; set; }

    /// <summary>+0x29, the forced flag (C6).</summary>
    public bool Forced { get; set; }

    public virtual bool ShouldTriggerBehavior(ReactionContext rc, IBehavior behavior)
    {
        if (Forced)
        {
            if (!ShouldTriggerBehaviorInternal(rc, behavior)) SetupForceTriggerBehavior(rc, behavior);
            Forced = false;
            return true;
        }
        return ShouldTriggerBehaviorInternal(rc, behavior);
    }

    /// <summary>vslot +0x24.</summary>
    protected abstract bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior);

    /// <summary>
    /// vslot +0x20. Only CubeMoved's is in the rows (gap2 2c); the others were not read and do nothing here. It is
    /// reached only through <see cref="Forced"/>, which nothing in this stack sets.
    /// </summary>
    protected virtual void SetupForceTriggerBehavior(ReactionContext rc, IBehavior behavior) { }

    public abstract void EnabledStateChanged(BehaviorContext context, bool enabled);

    /// <summary>IsReactionTriggerEnabled through the owning manager; a strategy in no map is "not found" and so false (gap1 4a).</summary>
    protected bool IsReactionTriggerEnabled(ReactionTrigger t) => Manager?.IsReactionTriggerEnabled(t) ?? false;
}

// fidelity: M10-003
/// <summary>
/// <c>ReactionTriggerStrategyGeneric</c> over <c>StrategyGeneric</c> (C12..C14, C5, C10):
/// <list type="bullet">
/// <item>AlwaysHandleInternal (0x613998..0x6139EC): for a subscribed tag, the tag's BaseStationTimer ms stamp is taken
/// whether or not the filter passes (0x60F34C..0x60F3B2), and the latch (+0x80) is set when there is no filter or the
/// filter returns true; a false filter leaves the latch unchanged;</item>
/// <item>WantsToRunInternal (0x613948..0x61397E): latch || (no events configured &amp;&amp; callback(robot)), and the latch is
/// cleared on every call;</item>
/// <item>ShouldTriggerBehaviorInternal (0x60F3BC..0x60F454): runnable (running || IsRunnable) &amp;&amp; WantsToRun &amp;&amp;
/// timeoutOK, WantsToRun only when runnable, so the latch survives while the behaviour is not runnable; timeoutOK means
/// some subscribed tag's last stamp is &gt; now − timeout;</item>
/// <item>shouldResumeLast +0x38 from genericStrategyParams, default 1; canInterruptOtherTriggeredBehavior +0x39, default
/// 1, set by no map entry; CanInterruptSelf returns 0 (0x60B732); EnabledStateChanged clears +0x2A, which nothing reads.</item>
/// </list>
/// </summary>
public sealed class GenericReactionStrategy : ReactionTriggerStrategy, IDisposable
{
    private readonly object _gate = new();
    private readonly HashSet<int> _tags;
    private readonly Dictionary<int, uint> _stamps = new();
    private readonly Func<object?, bool>? _filter;
    private readonly Func<CozmoRobot, bool>? _callback;
    private readonly Func<uint> _clockMs;
    private readonly Action? _unsubscribe;
    private bool _latch;

    // Host observation of the actual generic predicate and latch consumption.
    // No diagnostic invokes a native predicate a second time (0x613948..0x61397E).
    internal sealed record EventCheck(int Tag, uint AtMs, bool FilterPassed, string? OffTreadsState,
        string? CliffSensors, bool? StoppedForCliff, string? UnexpectedMovementType);
    internal sealed record Evaluation(uint AtMs, bool Runnable, bool? WantsToRun, bool? TimeoutPassed,
        bool? LatchedBefore, string? OffTreadsState, string[]? RequiredOffTreadsStates,
        EventCheck? LastEvent, uint? TimeoutMs, uint? EventAgeMs);
    internal Evaluation? LastEvaluation { get; private set; }
    private EventCheck? _lastEvent;
    private static readonly string[] InAirStates = [nameof(OffTreadsState.InAir)];
    private static readonly string[] OnBackStates = [nameof(OffTreadsState.OnBack)];
    private static readonly string[] OnFaceStates = [nameof(OffTreadsState.OnFace)];
    private static readonly string[] OnSideStates = [nameof(OffTreadsState.OnLeftSide), nameof(OffTreadsState.OnRightSide)];
    private static readonly string[] OnTreadsStates = [nameof(OffTreadsState.OnTreads)];
    private string[]? RequiredStates => Trigger switch
    {
        ReactionTrigger.RobotPickedUp => InAirStates, // 0x0060DDCE
        ReactionTrigger.RobotOnBack => OnBackStates, // 0x0060DEB2
        ReactionTrigger.RobotOnFace => OnFaceStates, // 0x0060DF16
        ReactionTrigger.RobotOnSide => OnSideStates, // 0x0060DF7E
        ReactionTrigger.ReturnedToTreads => OnTreadsStates, // 0x0060DE36..56
        _ => null,
    };
    private bool ObservePhysical => RequiredStates is not null || Trigger is
        ReactionTrigger.CliffDetected or ReactionTrigger.RobotFalling or ReactionTrigger.UnexpectedMovement;

    /// <param name="subscribe">Hooks the events that carry the tags; handed <see cref="AlwaysHandle"/>, returns the unhook.</param>
    /// <param name="clockMs">BaseStationTimer::GetCurrentTimeStamp.</param>
    public GenericReactionStrategy(ReactionTrigger trigger, string basis, Func<uint> clockMs,
                                   IEnumerable<int>? tags = null, Func<object?, bool>? filter = null,
                                   Func<CozmoRobot, bool>? shouldTriggerCallback = null, uint? timeoutMs = null,
                                   bool shouldResumeLast = true, Func<Action<int, object?>, Action>? subscribe = null)
    {
        Trigger = trigger; Basis = basis; _clockMs = clockMs;
        _tags = tags is null ? new HashSet<int>() : new HashSet<int>(tags);
        _filter = filter; _callback = shouldTriggerCallback; TimeoutMs = timeoutMs;
        ShouldResumeLast = shouldResumeLast;
        _unsubscribe = subscribe?.Invoke(AlwaysHandle);
    }

    public override ReactionTrigger Trigger { get; }
    public override string Basis { get; }
    public override bool ShouldResumeLast { get; }
    public override bool CanInterruptOtherTriggeredBehavior => true;
    public override bool CanInterruptSelf => false;
    /// <summary>The WithTimeout window (RobotFalling: 3000 ms, 0x60D838); null for ConfigureRelevantEvents without one.</summary>
    public uint? TimeoutMs { get; }

    /// <summary>Whether the latch (+0x80) is set.</summary>
    public bool Latched { get { lock (_gate) return _latch; } }

    /// <summary>AlwaysHandleInternal: one event of <paramref name="tag"/>, with the message the filter reads.</summary>
    public void AlwaysHandle(int tag, object? message)
    {
        if (!_tags.Contains(tag)) return;
        uint stamp;
        lock (_gate) _stamps[tag] = stamp = _clockMs();
        bool pass = _filter is null || _filter(message);
        if (pass || ObservePhysical) lock (_gate)
        {
            if (pass) _latch = true;
            if (ObservePhysical) _lastEvent = new(tag, stamp, pass, message is OffTreadsState state ? state.ToString() : null,
                (message as CliffReport)?.Sensors.ToString(), (message as CliffReport)?.StoppedForCliff,
                (message as UnexpectedMovementReport)?.Type.ToString());
        }
    }

    /// <summary>WantsToRunInternal.</summary>
    public bool WantsToRun(CozmoRobot robot) => WantsToRun(robot, out _);

    private bool WantsToRun(CozmoRobot robot, out bool latched)
    {
        lock (_gate) { latched = _latch; _latch = false; }
        return latched || (_tags.Count == 0 && _callback is not null && _callback(robot));
    }

    protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior)
    {
        bool runnable = rc.RunningOrRunnable(behavior);
        uint observedAt = rc.Context.Robot.Engine.Timer.TimeStampMs; // host snapshot only
        if (!runnable) { Observe(false, null, null, null, null, observedAt); return false; }
        string? state = ObservePhysical && RequiredStates is not null ? rc.Context.Robot.Sensors.OffTreadsState.ToString() : null;
        bool wants = WantsToRun(rc.Context.Robot, out bool latched);
        if (!wants) { Observe(true, false, null, latched, state, observedAt); return false; }
        if (TimeoutMs is not { } timeout) { Observe(true, true, null, latched, state, observedAt); return true; }
        uint now = _clockMs();
        uint since = unchecked(now - timeout);
        bool passed;
        lock (_gate) passed = _stamps.Values.Any(s => s > since);
        Observe(true, true, passed, latched, state, now);
        return passed;
    }

    private void Observe(bool runnable, bool? wants, bool? timed, bool? latched, string? state, uint now)
    {
        if (!ObservePhysical) return;
        lock (_gate) LastEvaluation = new(now, runnable, wants, timed, latched, state, RequiredStates,
            _lastEvent, TimeoutMs, _lastEvent is { } e ? unchecked(now - e.AtMs) : null);
    }

    /// <summary>+0x1C: clears +0x2A (0x60F910), which has no reader (gap1 4j).</summary>
    public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }

    public void Dispose() => _unsubscribe?.Invoke();
}

// fidelity: M10-003
/// <summary>
/// RobotShaken (C15, C5, C10): (running || IsRunnable) &amp;&amp; WantsToRun, where WantsToRun is always called and is
/// robot+0x37C (the 0.05/0.95 filtered |accel|, RS8) &gt; 16000 (0x6146CC..0x6146E0). Flags (0, 1, 0).
/// C5: its EnabledStateChanged (+0x1C) is the base no-op (0x60B73B); the disable/enable calls have no effect.
/// </summary>
public sealed class RobotShakenStrategy : ReactionTriggerStrategy
{
    public const float ShakenAccelThreshold = 16000f;
    public override ReactionTrigger Trigger => ReactionTrigger.RobotShaken;
    public override string Basis => "StrategyRobotShaken 0x612FD4..0x61300C, WantsToRun 0x6146CC..0x6146E0: robot+0x37C > 16000";
    public override bool ShouldResumeLast => false;
    public override bool CanInterruptOtherTriggeredBehavior => true;
    public override bool CanInterruptSelf => false;

    /// <summary>The wants-to-run test alone.</summary>
    public static bool WantsToRun(CozmoRobot robot) => robot.Sensors.OffTreads.FilteredAccelMagnitude > ShakenAccelThreshold;

    protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior)
    {
        bool ready = rc.RunningOrRunnable(behavior); // 612FDE..613004 checks the running flag and IsRunnable first.
        bool wants = WantsToRun(rc.Context.Robot);
        return ready && wants;
    }

    public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }
}

// fidelity: M10-003
/// <summary>
/// RobotPlacedOnSlope (C16, C5, C10): WantsToRun, then IsRunnable. Constants 10/55 deg, 0.01 rad/s over the raw gyro
/// (+0x36C..+0x374), 0.4 s, 1.5 s; +0x18 and +0x20 start at 0.0; times are BaseStationTimer seconds. Result = pitch in
/// (10, 55) &amp;&amp; quietFor &gt; 0.4 &amp;&amp; (pickedUp || now − lastPicked &lt; 1.5) &amp;&amp; +0x355 &lt; 2. Flags (0, 1, 0).
/// The axis magnitudes are absolute (0x614570..0x6145C6). The gyro stamp is updated at 0x6145DE..0x6145E2 and
/// the pickup stamp at 0x61462C..0x614632 before either rejection can return.
/// C5: its EnabledStateChanged (+0x1C) is the base no-op (0x60B73B); the disable/enable calls have no effect.
/// </summary>
public sealed class PlacedOnSlopeStrategy : ReactionTriggerStrategy
{
    public const float MinPitchDeg = 10f, MaxPitchDeg = 55f;
    public const float GyroQuietRadps = 0.01f;
    public const double QuietForSec = 0.4, PutDownWithinSec = 1.5;

    private double _lastGyroActiveSec;      // C16: starts at 0.0
    private bool _c2AwaitingStablePutdown;
    private double _lastPickedUpSec;        // C16: starts at 0.0

    internal sealed record Evaluation(double AtSec, double? PitchDeg, double? MaxAbsGyroRadps,
        double QuietForSec, double LastPickupAgeSec, bool PickedUp, string OffTreadsState,
        bool PitchInRange, bool GyroQuietLongEnough, bool PickupWindowOpen, bool TreadsAllowed, bool WantsToRun,
        float MinPitchDeg = PlacedOnSlopeStrategy.MinPitchDeg, float MaxPitchDeg = PlacedOnSlopeStrategy.MaxPitchDeg,
        float GyroQuietThresholdRadps = PlacedOnSlopeStrategy.GyroQuietRadps,
        double RequiredQuietSec = PlacedOnSlopeStrategy.QuietForSec, double PickupWindowSec = PlacedOnSlopeStrategy.PutDownWithinSec,
        bool? Runnable = null);
    internal Evaluation? LastEvaluation { get; private set; }

    public override ReactionTrigger Trigger => ReactionTrigger.RobotPlacedOnSlope;
    public override string Basis => "ReactionTriggerStrategyRobotPlacedOnSlope 0x612EB0..0x612ECC; StrategyRobotPlacedOnSlope ctor 0x6144FC..0x614554, " +
                                    "WantsToRun 0x61455C..0x614688: pitch in (10, 55) deg, quiet > 0.4 s, picked up or < 1.5 s since, +0x355 < 2";
    public override bool ShouldResumeLast => false;
    public override bool CanInterruptOtherTriggeredBehavior => true;
    public override bool CanInterruptSelf => false;

    /// <summary>The wants-to-run test (StrategyRobotPlacedOnSlope::WantsToRunInternal).</summary>
    public bool WantsToRun(CozmoRobot robot, double nowSec)
    {
        var sensors = robot.Sensors;
        // 0x005129B0..0x005129BE copies all three gyro values from the packet.
        // 0x00614594..0x006145C6 compares in axis order with a strict less-than.
        var gyro = sensors.OffTreads.RawGyro;
        float maxGyro = MathF.Abs(gyro.X);
        float y = MathF.Abs(gyro.Y), z = MathF.Abs(gyro.Z);
        if (maxGyro < y) maxGyro = y;
        if (maxGyro < z) maxGyro = z;
        if (maxGyro > GyroQuietRadps) _lastGyroActiveSec = nowSec;
        bool pickedUp = sensors.PickedUp;
        if (pickedUp) _lastPickedUpSec = nowSec;

        float pitchDeg = sensors.OffTreads.PitchRad * BitConverter.UInt32BitsToSingle(0x42652EE1); // 0x0051237E copies Robot+0x304; 0x006145FC reads degrees.
        // Same comparisons, same already-updated stamps (0x00614604..0x00614680).
        double quiet = nowSec - _lastGyroActiveSec, sincePickup = nowSec - _lastPickedUpSec;
        bool pitchOk = pitchDeg > MinPitchDeg && pitchDeg < MaxPitchDeg;
        bool quietOk = quiet > QuietForSec;
        bool pickupOk = pickedUp || sincePickup < PutDownWithinSec;
        var treads = sensors.OffTreadsState;
        bool treadsOk = treads < OffTreadsState.OnBack;
        bool wants = pitchOk && quietOk && pickupOk && treadsOk;
        // Our C2 policy adds one post-placement check to the native 0x0061455C
        // evaluation. A pickup arms it. A settled flat placement, another safety
        // reaction or expiry retires it so unrelated later tilt cannot trigger it.
        if (robot.Engine.Options.C2.SlopePutdown)
        {
            if (pickedUp) _c2AwaitingStablePutdown = true;
            else if (_c2AwaitingStablePutdown)
            {
                var active = Manager?.CurrentReactionTrigger;
                if (wants || sincePickup > 10 || treads > OffTreadsState.InAir ||
                    sensors.CliffDetectedNow || sensors.CliffDetectedByEvent ||
                    (active is not (null or ReactionTrigger.ReturnedToTreads or ReactionTrigger.RobotPickedUp)))
                    _c2AwaitingStablePutdown = false;
                else if (!pickupOk && quietOk && treads == OffTreadsState.OnTreads &&
                    active is not ReactionTrigger.RobotPickedUp && float.IsFinite(pitchDeg) &&
                    pitchDeg != 0 && float.IsFinite(maxGyro) && maxGyro <= GyroQuietRadps)
                {
                    _c2AwaitingStablePutdown = false;
                    wants = pitchOk;
                    robot.Engine.EmitStackLog($"{CozmoC2Settings.SlopePutdownName}: settled put-down after {sincePickup:F2}s, pitch {pitchDeg:F1} degrees, slope check {wants}");
                }
            }
        }
        LastEvaluation = new(nowSec, float.IsFinite(pitchDeg) ? pitchDeg : null, float.IsFinite(maxGyro) ? maxGyro : null,
            quiet, sincePickup, pickedUp, treads.ToString(), pitchOk, quietOk, pickupOk, treadsOk, wants);
        return wants;
    }

    protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior)
    {
        if (!WantsToRun(rc.Context.Robot, behavior is SteppedBehavior { NativeBaseInputs: not null }
            ? rc.Context.Robot.Engine.Timer.SecondsF : rc.NowSec)) return false;
        var observation = LastEvaluation;
        bool runnable = behavior.IsRunnable(rc.Context); // 0x00612EB0..0x00612ECC
        if (ReferenceEquals(observation, LastEvaluation) && observation is not null)
            LastEvaluation = observation with { Runnable = runnable };
        return runnable;
    }

    public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }
}

// fidelity: M10-003
/// <summary>
/// <c>ReactionTriggerStrategyFrustration</c> (C17, gap2 6c, C5, C10): current trigger ≠ 4 &amp;&amp; mood Confident (the
/// MoodManager's emotion 3, +0x78) &lt; maxConfidence &amp;&amp; (last ≤ 0 || now − last &gt; cooldown) &amp;&amp; IsRunnable.
/// AnimationComplete stamps BaseStationTimer seconds (0x60EEF4). Flags: shouldResumeLast 0, CanInterruptOther 0;
/// CanInterruptSelf is not in the rows and cannot be observed (the predicate is false while Frustration is current).
/// The params are the map's frustrationParams (maxConfidence, cooldownTime_s). The mood is the M7 interface (MD2).
/// C5: its EnabledStateChanged (+0x1C) is the base no-op (0x60B73B); the disable/enable calls have no effect.
/// </summary>
public sealed class FrustrationStrategy : ReactionTriggerStrategy
{
    private readonly Func<double>? _clock;

    /// <param name="clockSec">BaseStationTimer seconds for both the stamp and the test; without one the test uses the
    /// manager's time and the stamp must come through <see cref="AnimationComplete(double)"/>.</param>
    public FrustrationStrategy(float maxConfidence, float cooldownSec, Func<double>? clockSec = null)
    {
        MaxConfidence = maxConfidence; CooldownSec = cooldownSec; _clock = clockSec;
    }

    public float MaxConfidence { get; }
    public float CooldownSec { get; }
    public bool HasClock => _clock is not null;
    /// <summary>+0x38, the last AnimationComplete time; 0 until one (the test is last ≤ 0).</summary>
    public double LastAnimationCompleteSec { get; private set; }

    public override ReactionTrigger Trigger => ReactionTrigger.Frustration;
    public override string Basis => "ReactionTriggerStrategyFrustration::ShouldTriggerBehaviorInternal 0x60EE6E..0x60EED8: current != 4, " +
                                    $"Confident < {MaxConfidence}, last <= 0 or now - last > {CooldownSec}s, IsRunnable";
    public override bool ShouldResumeLast => false;
    public override bool CanInterruptOtherTriggeredBehavior => false;
    public override bool CanInterruptSelf => false;

    /// <summary>AnimationComplete on the strategy's own clock (requires one).</summary>
    public void AnimationComplete()
    {
        if (_clock is null) throw new InvalidOperationException("FrustrationStrategy has no clock; stamp with AnimationComplete(nowSec)");
        LastAnimationCompleteSec = _clock();
    }

    /// <summary>AnimationComplete with an explicit BaseStationTimer time (a strategy with a clock ignores the argument).</summary>
    public void AnimationComplete(double nowSec) => LastAnimationCompleteSec = _clock?.Invoke() ?? nowSec;

    /// <summary>last ≤ 0 || now − last &gt; cooldown.</summary>
    public bool CooldownElapsed(double nowSec)
    {
        float last = (float)LastAnimationCompleteSec;
        float now = (float)(_clock?.Invoke() ?? nowSec);
        return last <= 0 || now - last > CooldownSec; // 60EEA6..60EEC8
    }

    protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior)
    {
        if (rc.CurrentTrigger == ReactionTrigger.Frustration) return false;
        if (rc.Context.Mood is not { } mood) return false;                         // MD2: no MoodManager, no Confident value
        if (!((float)mood[EmotionType.Confident] < MaxConfidence)) return false; // 60EE8C..60EEA4 (ordered f32)
        if (!CooldownElapsed(rc.NowSec)) return false;
        return behavior.IsRunnable(rc.Context);
    }

    public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }
}

// fidelity: M10-003
/// <summary>
/// PlacedOnCharger (gap1 8, gap2 1a..1d): flags (0, 1, 0). ShouldTriggerBehaviorInternal is WantsToRun alone, with no
/// IsRunnable. StrategyPlacedOnCharger: latch +0x15 = 0, deadline +0x18 = −1.0; on ChargerEvent (tag 57) latch =
/// msg.onCharger; WantsToRunInternal sets the deadline to now + 20.0 s on its first call, returns now ≥ deadline &amp;&amp;
/// latch, and clears the latch on every call. EnabledStateChanged is a no-op (0x60B73A).
/// </summary>
public sealed class PlacedOnChargerStrategy : ReactionTriggerStrategy, IDisposable
{
    public const double FirstCallDelaySec = 20.0;
    private readonly object _gate = new();
    private bool _latch;
    private double _deadline = -1.0;
    private readonly CozmoRobot? _robot;

    public PlacedOnChargerStrategy(CozmoRobot? robot = null)
    {
        _robot = robot;
        if (robot is not null) robot.Sensors.ChargerEventInternal += HandleChargerEvent;
    }

    public void Dispose()
    {
        if (_robot is not null) _robot.Sensors.ChargerEventInternal -= HandleChargerEvent;
    }

    public override ReactionTrigger Trigger => ReactionTrigger.PlacedOnCharger;
    public override string Basis => "ReactionTriggerStrategy 0x6120D0..0x6120E8 over StrategyPlacedOnCharger 0x6143F8..0x6144D0: " +
                                    "deadline = first call + 20 s; now >= deadline && ChargerEvent latch; latch cleared each call";
    public override bool ShouldResumeLast => false;
    public override bool CanInterruptOtherTriggeredBehavior => true;
    public override bool CanInterruptSelf => false;

    /// <summary>The ChargerEvent handler (vtable address point 0x102D690 slot +0xC): latch = onCharger.</summary>
    public void HandleChargerEvent(bool onCharger) { lock (_gate) _latch = onCharger; }

    /// <summary>WantsToRunInternal (0x614474..0x6144B6).</summary>
    public bool WantsToRun(double nowSec)
    {
        lock (_gate)
        {
            if (_deadline < 0) _deadline = nowSec + FirstCallDelaySec;
            bool r = nowSec >= _deadline && _latch;
            _latch = false;
            return r;
        }
    }

    protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior) => WantsToRun(rc.NowSec);

    public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }
}

// fidelity: M10-003
/// <summary>
/// The Generic strategies the factory (C12, gap1 1) builds for the triggers whose events this stack raises, and the
/// purpose-built ones, each with the map's genericStrategyParams.shouldResumeLast (reactionTrigger_behavior_map.json).
/// E2G tags: CliffEvent 34, RobotStopped 52, MotorCalibration 30, FallingStarted 58, RobotOffTreadsStateChanged 53,
/// UnexpectedMovement 60.
/// </summary>
public static class ShippedReactionStrategies
{
    public const int TagMotorCalibration = 30, TagCliffEvent = 34, TagRobotStopped = 52, TagRobotOffTreadsStateChanged = 53,
                     TagFallingStarted = 58, TagUnexpectedMovement = 60;
    /// <summary>RobotFalling's WithTimeout (0x60D838 `movw r3,#0xbb8`).</summary>
    public const uint FallingTimeoutMs = 3000;

    /// <summary>CliffDetected: {34, 52}, filter 0x60DC76 (gap1 1b): enabled(0), then current ≠ 0 → true, else CanInterruptSelf (0).</summary>
    public static GenericReactionStrategy Cliff(CozmoRobot robot)
    {
        GenericReactionStrategy? self = null;
        var s = new GenericReactionStrategy(ReactionTrigger.CliffDetected,
            "CreateReactionTriggerStrategy 0x60D618 CliffDetected -> {CliffEvent 34, RobotStopped 52}, filter 0x60DC7C..0x60DCA2",
            () => robot.Engine.Timer.TimeStampMs, new[] { TagCliffEvent, TagRobotStopped },
            filter: _ =>
            {
                var m = self!.Manager;
                if (m is null || !m.IsReactionTriggerEnabled(ReactionTrigger.CliffDetected)) return false;
                if (m.CurrentReactionTrigger != ReactionTrigger.CliffDetected) return true;
                return self.CanInterruptSelf;
            },
            shouldResumeLast: true,
            subscribe: handle =>
            {
                void on(CliffReport c) => handle(TagCliffEvent, c);
                void stopped() => handle(TagRobotStopped, null);
                robot.Sensors.CliffDetectedInternal += on;
                robot.Sensors.RobotStoppedInternal += stopped;
                return () =>
                {
                    robot.Sensors.CliffDetectedInternal -= on;
                    robot.Sensors.RobotStoppedInternal -= stopped;
                };
            });
        self = s;
        return s;
    }

    /// <summary>MotorCalibration: {30}, filter 0x60DCFA: msg+1 &amp;&amp; msg+2 (calibStarted &amp;&amp; autoStarted).</summary>
    public static GenericReactionStrategy MotorCalibration(CozmoRobot robot) =>
        new(ReactionTrigger.MotorCalibration,
            "CreateReactionTriggerStrategy 0x60D618 MotorCalibration -> {MotorCalibration 30}, filter 0x60DCFA: calibStarted && autoStarted",
            () => robot.Engine.Timer.TimeStampMs, new[] { TagMotorCalibration },
            filter: m => m is Protocol.MotorCalibration mc && mc.CalibStarted && mc.AutoStarted,
            shouldResumeLast: true,
            subscribe: handle =>
            {
                void on(Protocol.MotorCalibration mc) => handle(TagMotorCalibration, mc);
                robot.Sensors.MotorCalibrationReportedInternal += on;
                return () => robot.Sensors.MotorCalibrationReportedInternal -= on;
            });

    /// <summary>RobotFalling: {FallingStarted 58} WithTimeout 3000, filter IsReactionTriggerEnabled(11) (0x60DD6E).</summary>
    public static GenericReactionStrategy Falling(CozmoRobot robot)
    {
        GenericReactionStrategy? self = null;
        var s = new GenericReactionStrategy(ReactionTrigger.RobotFalling,
            "CreateReactionTriggerStrategy 0x60D618 RobotFalling -> {FallingStarted 58} WithTimeout 3000 (0x60D838), filter 0x60DD6E: enabled(11)",
            () => robot.Engine.Timer.TimeStampMs, new[] { TagFallingStarted },
            filter: _ => self!.Manager?.IsReactionTriggerEnabled(ReactionTrigger.RobotFalling) ?? false,
            timeoutMs: FallingTimeoutMs, shouldResumeLast: false,
            subscribe: handle =>
            {
                void on(uint ts) => handle(TagFallingStarted, ts);
                robot.Sensors.FallingStartedInternal += on;
                return () => robot.Sensors.FallingStartedInternal -= on;
            });
        self = s;
        return s;
    }

    /// <summary>ReturnedToTreads: {53}, filter 0x60DE36..0x60DE56: enabled(14) &amp;&amp; state == 0.</summary>
    public static GenericReactionStrategy ReturnedToTreads(CozmoRobot robot)
    {
        GenericReactionStrategy? self = null;
        var s = new GenericReactionStrategy(ReactionTrigger.ReturnedToTreads,
            "CreateReactionTriggerStrategy 0x60D618 ReturnedToTreads -> {RobotOffTreadsStateChanged 53}, filter 0x60DE36..0x60DE56: enabled(14) && state == 0",
            () => robot.Engine.Timer.TimeStampMs, new[] { TagRobotOffTreadsStateChanged },
            filter: m => (self!.Manager?.IsReactionTriggerEnabled(ReactionTrigger.ReturnedToTreads) ?? false) && m is OffTreadsState st && st == OffTreadsState.OnTreads,
            shouldResumeLast: false,
            subscribe: handle =>
            {
                void on(OffTreadsState from, OffTreadsState to) => handle(TagRobotOffTreadsStateChanged, to);
                robot.Sensors.ReturnedToTreadsStateChanged += on;
                return () => robot.Sensors.ReturnedToTreadsStateChanged -= on;
            });
        self = s;
        return s;
    }

    /// <summary>UnexpectedMovement: {60}, no filter (0x60D96A).</summary>
    public static GenericReactionStrategy UnexpectedMovement(CozmoRobot robot) =>
        new(ReactionTrigger.UnexpectedMovement,
            "CreateReactionTriggerStrategy 0x60D618 UnexpectedMovement -> {UnexpectedMovement 60}, no filter; broadcast by CheckForUnexpectedMovement 0x63E932",
            () => robot.Engine.Timer.TimeStampMs, new[] { TagUnexpectedMovement },
            shouldResumeLast: true,
            subscribe: handle =>
            {
                void on(UnexpectedMovementReport r) => handle(TagUnexpectedMovement, r);
                robot.Sensors.UnexpectedMovementDetectedInternal += on;
                return () => robot.Sensors.UnexpectedMovementDetectedInternal -= on;
            });

    /// <summary>A state callback (SetShouldTriggerCallback): no events, so WantsToRun is the callback.</summary>
    private static GenericReactionStrategy Callback(CozmoRobot robot, ReactionTrigger t, string basis, Func<CozmoRobot, bool> cb) =>
        new(t, basis, () => robot.Engine.Timer.TimeStampMs, shouldTriggerCallback: cb, shouldResumeLast: false);

    /// <summary>PickedUp: +0x355 == 1 (lambda 0x60DDCE).</summary>
    public static GenericReactionStrategy PickedUp(CozmoRobot robot) =>
        Callback(robot, ReactionTrigger.RobotPickedUp, "CreateReactionTriggerStrategy 0x60D618 RobotPickedUp -> callback 0x60DDCE: robot+0x355 == 1",
                 r => r.Sensors.OffTreadsState == OffTreadsState.InAir);

    /// <summary>OnBack: +0x355 == 2 (lambda 0x60DEB2).</summary>
    public static GenericReactionStrategy OnBack(CozmoRobot robot) =>
        Callback(robot, ReactionTrigger.RobotOnBack, "CreateReactionTriggerStrategy 0x60D618 RobotOnBack -> callback 0x60DEB2: robot+0x355 == 2",
                 r => r.Sensors.OffTreadsState == OffTreadsState.OnBack);

    /// <summary>OnFace: +0x355 == 5 (lambda 0x60DF16).</summary>
    public static GenericReactionStrategy OnFace(CozmoRobot robot) =>
        Callback(robot, ReactionTrigger.RobotOnFace, "CreateReactionTriggerStrategy 0x60D618 RobotOnFace -> callback 0x60DF16: robot+0x355 == 5",
                 r => r.Sensors.OffTreadsState == OffTreadsState.OnFace);

    /// <summary>OnSide: (+0x355 − 3) &lt; 2 (lambda 0x60DF7E).</summary>
    public static GenericReactionStrategy OnSide(CozmoRobot robot) =>
        Callback(robot, ReactionTrigger.RobotOnSide, "CreateReactionTriggerStrategy 0x60D618 RobotOnSide -> callback 0x60DF7E: (robot+0x355 - 3) < 2",
                 r => r.Sensors.OffTreadsState is OffTreadsState.OnLeftSide or OffTreadsState.OnRightSide);

    /// <summary>
    /// The strategies above plus Shaken, PlacedOnSlope and Frustration (the map's Minor entry: maxConfidence −0.6,
    /// cooldown 60 s), for one robot. <paramref name="clockSec"/> is Frustration's BaseStationTimer seconds.
    /// </summary>
    // fidelity: M7-011
    public static IReadOnlyList<IReactionTriggerStrategy> ForRobot(CozmoRobot robot, Func<double>? clockSec = null) => new IReactionTriggerStrategy[]
    {
        PickedUp(robot), OnBack(robot), OnFace(robot), OnSide(robot),
        ReturnedToTreads(robot), MotorCalibration(robot), UnexpectedMovement(robot), Cliff(robot), Falling(robot),
        new RobotShakenStrategy(), new PlacedOnSlopeStrategy(),
        new FrustrationStrategy(maxConfidence: -0.6f, cooldownSec: 60f, clockSec),
        new PlacedOnChargerStrategy(robot),
    };
}
