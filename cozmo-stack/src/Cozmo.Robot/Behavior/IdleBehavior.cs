using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

/// <summary>What the idle layer decided to do on one tick.</summary>
public enum IdleAction { None, Blink, EyeDart, HeadMove, LiftMove, BodyMove }

/// <summary>One idle action, with everything needed to reproduce and explain it.</summary>
public sealed record IdleEvent(IdleAction Action, double AtMs)
{
    /// <summary>The value the action used: radians for head, mm for lift, mm/s for body, pixels for a dart.</summary>
    public double Amount { get; init; }
    public double DurationMs { get; init; }
    /// <summary>Why nothing happened, when nothing did.</summary>
    public string? Suppressed { get; init; }
}

/// <summary>
/// Keeps an otherwise still robot alive: blinking, eye darts, and small head, lift and body movements.
///
/// This is a reconstruction of the engine's <c>AnimationStreamer::UpdateLiveAnimation</c> keep-alive, and
/// it follows two things the engine actually does rather than a guess at what looks lively:
///
/// * **The tunables are the engine's.** All 30 come from <see cref="IdleParameters"/>, disassembled out of
///   <c>SetDefaultParams()</c>. Blink spacing really is 3–4 s, a body shuffle really is 10 mm/s.
/// * **Idle yields per track.** <c>UpdateLiveAnimation</c> calls
///   <c>MovementComponent::AreAnyTracksLocked</c> once per track group before moving it, so a locked track
///   is skipped while the others carry on. <see cref="Advance"/> does the same against the M5 scheduler's
///   owned tracks, so a caller animation moving only the head does not stop idle blinking.
///
/// Timing is taken, not read: <see cref="Advance"/> is given the time, as the M5 scheduler is, so the
/// whole thing is testable without a robot and without waiting.
///
/// **Nothing runs until autonomy is enabled on the arbiter.**
/// </summary>
public sealed class IdleBehavior
{
    private readonly CozmoRobot _robot;
    private readonly IdleParameters _p;
    private readonly Random _random;
    private readonly object _gate = new();

    private double _nextBlinkMs, _nextDartMs, _nextHeadMs, _nextLiftMs, _nextBodyMs;
    private double _idleSinceMs = double.NaN;
    private bool _primed;

    public IdleBehavior(CozmoRobot robot, BehaviorArbiter arbiter,
                        IdleParameters? parameters = null, Random? random = null)
    {
        _robot = robot;
        Arbiter = arbiter;
        _p = parameters ?? IdleParameters.Default;
        _random = random ?? new Random();
    }

    public BehaviorArbiter Arbiter { get; }
    public IdleParameters Parameters => _p;

    /// <summary>Raised for every idle action, and for every one that was suppressed.</summary>
    public event Action<IdleEvent>? Acted;

    /// <summary>Actions taken since the last <see cref="Reset"/>, for tests and diagnostics.</summary>
    public int ActionCount { get; private set; }

    /// <summary>Forgets all timers, so the next <see cref="Advance"/> re-primes them.</summary>
    public void Reset()
    {
        lock (_gate) { _primed = false; _idleSinceMs = double.NaN; ActionCount = 0; }
    }

    /// <summary>
    /// Advances idle behaviour to this moment, doing at most one thing per category.
    ///
    /// Returns what it did, so a caller can log it. An empty list means it deliberately did nothing —
    /// which is the normal case on most ticks, since the shortest spacing is 250 ms.
    /// </summary>
    public IReadOnlyList<IdleEvent> Advance(double nowMs)
    {
        var done = new List<IdleEvent>();

        if (!Arbiter.AutonomyEnabled)
        {
            lock (_gate) { _primed = false; _idleSinceMs = double.NaN; }
            return done;
        }

        // Anything of higher priority running means the robot is not idle at all. The engine's equivalent
        // is the streamer simply not reaching its live-animation update while a real animation streams.
        if (Arbiter.Running is { } running && running > BehaviorPriority.Idle)
        {
            lock (_gate) { _idleSinceMs = double.NaN; }
            Raise(done, new IdleEvent(IdleAction.None, nowMs) { Suppressed = $"{running} is running" });
            return done;
        }

        lock (_gate)
        {
            if (!_primed)
            {
                _primed = true;
                _nextBlinkMs = nowMs + Between(_p.BlinkSpacingMinMs, _p.BlinkSpacingMaxMs);
                _nextDartMs = nowMs + Between(_p.EyeDartSpacingMinMs, _p.EyeDartSpacingMaxMs);
                _nextHeadMs = nowMs + Between(_p.HeadMovementSpacingMinMs, _p.HeadMovementSpacingMaxMs);
                _nextLiftMs = nowMs + Between(_p.LiftMovementSpacingMinMs, _p.LiftMovementSpacingMaxMs);
                _nextBodyMs = nowMs + Between(_p.BodyMovementSpacingMinMs, _p.BodyMovementSpacingMaxMs);
            }
            if (double.IsNaN(_idleSinceMs)) _idleSinceMs = nowMs;
        }

        var owned = _robot.Animations.OwnedTracks;
        bool faceFree = (owned & AnimationTrack.Face) == 0;
        bool headFree = (owned & AnimationTrack.Head) == 0;
        bool liftFree = (owned & AnimationTrack.Lift) == 0;
        bool bodyFree = (owned & AnimationTrack.Body) == 0;

        // The face keeps going from the first moment. Movement waits out TimeBeforeWiggleMotions_ms,
        // which is what stops the robot twitching the instant it is put down.
        bool mayMove;
        lock (_gate) mayMove = nowMs - _idleSinceMs >= _p.TimeBeforeWiggleMotionsMs;

        if (Due(ref _nextBlinkMs, nowMs, _p.BlinkSpacingMinMs, _p.BlinkSpacingMaxMs))
            Raise(done, faceFree
                ? Do(IdleAction.Blink, nowMs)
                : new IdleEvent(IdleAction.Blink, nowMs) { Suppressed = "the face track is owned" });

        if (Due(ref _nextDartMs, nowMs, _p.EyeDartSpacingMinMs, _p.EyeDartSpacingMaxMs))
            Raise(done, faceFree
                ? Do(IdleAction.EyeDart, nowMs, Between(-_p.EyeDartMaxDistancePix, _p.EyeDartMaxDistancePix),
                     Between(_p.EyeDartMinDurationMs, _p.EyeDartMaxDurationMs))
                : new IdleEvent(IdleAction.EyeDart, nowMs) { Suppressed = "the face track is owned" });

        if (mayMove && Due(ref _nextHeadMs, nowMs, _p.HeadMovementSpacingMinMs, _p.HeadMovementSpacingMaxMs))
            Raise(done, headFree
                ? Do(IdleAction.HeadMove, nowMs,
                     Between(-_p.HeadAngleVariabilityDeg, _p.HeadAngleVariabilityDeg),
                     Between(_p.HeadMovementDurationMinMs, _p.HeadMovementDurationMaxMs))
                : new IdleEvent(IdleAction.HeadMove, nowMs) { Suppressed = "the head track is owned" });

        if (mayMove && Due(ref _nextLiftMs, nowMs, _p.LiftMovementSpacingMinMs, _p.LiftMovementSpacingMaxMs))
            Raise(done, liftFree
                ? Do(IdleAction.LiftMove, nowMs,
                     _p.LiftHeightMeanMm + Between(-_p.LiftHeightVariabilityMm, _p.LiftHeightVariabilityMm),
                     Between(_p.LiftMovementDurationMinMs, _p.LiftMovementDurationMaxMs))
                : new IdleEvent(IdleAction.LiftMove, nowMs) { Suppressed = "the lift track is owned" });

        if (mayMove && Due(ref _nextBodyMs, nowMs, _p.BodyMovementSpacingMinMs, _p.BodyMovementSpacingMaxMs))
            Raise(done, bodyFree
                ? Do(IdleAction.BodyMove, nowMs,
                     Between(-_p.BodyMovementSpeedMmps, _p.BodyMovementSpeedMmps),
                     Between(_p.BodyMovementDurationMinMs, _p.BodyMovementDurationMaxMs))
                : new IdleEvent(IdleAction.BodyMove, nowMs) { Suppressed = "the body track is owned" });

        return done;
    }

    /// <summary>
    /// Whether idle actually drives the robot, or only decides and reports. Off in tests and offline
    /// replay; on when something is actually connected.
    ///
    /// This is the master switch. <see cref="ExecuteMotors"/> gates the motors separately.
    /// </summary>
    public bool Execute { get; set; } = true;

    /// <summary>
    /// Whether idle may drive the head, lift and body, as distinct from the face.
    ///
    /// These are deliberately separate. Blinking and eye darts are the visible sign that keep-alive is
    /// working at all, and they move nothing; head and lift movement is real motion that an operator may
    /// not want from an unattended robot. Tying them together meant a no-motion idle run could not blink,
    /// so there was nothing to watch.
    /// </summary>
    public bool ExecuteMotors { get; set; } = true;

    private IdleEvent Do(IdleAction action, double nowMs, double amount = 0, double durationMs = 0)
    {
        ActionCount++;
        bool motor = action is IdleAction.HeadMove or IdleAction.LiftMove or IdleAction.BodyMove;
        if (Execute && (!motor || ExecuteMotors)) Perform(action, amount, durationMs);
        return new IdleEvent(action, nowMs) { Amount = amount, DurationMs = durationMs };
    }

    /// <summary>
    /// Carries out one idle action on the robot.
    ///
    /// Blinks and eye darts go through the M5 procedural face, which is the closest thing this stack has
    /// to the engine's face layering. Head and lift use the M4 motion API at the engine's own durations.
    /// Body movement is deliberately **not** driven: a 10 mm/s shuffle is within the engine's parameters,
    /// but sending wheel commands to an unattended robot is not something to switch on without watching
    /// it happen, so it is decided and reported and left for hardware acceptance to enable.
    /// </summary>
    private void Perform(IdleAction action, double amount, double durationMs)
    {
        try
        {
            switch (action)
            {
                case IdleAction.Blink:
                    Blink();
                    break;
                case IdleAction.EyeDart:
                    Dart(amount);
                    break;
                case IdleAction.HeadMove:
                    _ = _robot.Motion.SetHeadAngleAsync(
                        (float)(_robot.State.HeadAngleRad + amount * Math.PI / 180.0),
                        durationSec: (float)(durationMs / 1000.0));
                    break;
                case IdleAction.LiftMove:
                    _ = _robot.Motion.SetLiftHeightAsync((float)amount,
                        durationSec: (float)(durationMs / 1000.0));
                    break;
                case IdleAction.BodyMove:
                    // see the note above: decided, reported, not driven
                    break;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // A robot that went away mid-idle is not an idle bug.
        }
    }

    /// <summary>A blink: lids closed, then back to where the face was.</summary>
    private void Blink()
    {
        var before = _robot.Face.Current;
        var shut = before.Clone();
        shut.Left[(int)EyeParam.UpperLidY] = 1f;
        shut.Right[(int)EyeParam.UpperLidY] = 1f;
        _robot.Face.SetParameters(shut);
        _robot.Face.SetParameters(before);
    }

    /// <summary>
    /// An eye dart: both eyes shift by the same amount, and the eye further from the direction of travel
    /// grows slightly, which is what EyeDartOuterEyeScaleIncrease describes.
    /// </summary>
    private void Dart(double pixels)
    {
        var pose = _robot.Face.Current.Clone();
        pose.Left[(int)EyeParam.EyeCenterX] += (float)pixels;
        pose.Right[(int)EyeParam.EyeCenterX] += (float)pixels;
        var outer = pixels < 0 ? pose.Right : pose.Left;
        outer[(int)EyeParam.EyeScaleX] += (float)_p.EyeDartOuterEyeScaleIncrease;
        outer[(int)EyeParam.EyeScaleY] += (float)_p.EyeDartOuterEyeScaleIncrease;
        _robot.Face.SetParameters(pose);
    }

    private void Raise(List<IdleEvent> into, IdleEvent e)
    {
        into.Add(e);
        Acted?.Invoke(e);
    }

    /// <summary>True when the next occurrence is due, rescheduling it if so.</summary>
    private bool Due(ref double next, double nowMs, double minSpacing, double maxSpacing)
    {
        lock (_gate)
        {
            if (nowMs < next) return false;
            next = nowMs + Between(minSpacing, maxSpacing);
            return true;
        }
    }

    private double Between(double a, double b) => a + _random.NextDouble() * (b - a);
}
