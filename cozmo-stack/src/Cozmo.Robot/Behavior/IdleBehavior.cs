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

        // A dart or blink lasts for its own duration and no longer. Without this the last one simply
        // stayed on screen until the next moved it further, which is how the eyes drifted and grew.
        if (faceFree) ExpireTransient(nowMs);
        else ForgetBaseFace();   // something else owns the face; whatever base we had is stale
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
        if (!motor || ExecuteMotors) Perform(action, nowMs, amount, durationMs);
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
    private void Perform(IdleAction action, double nowMs, double amount, double durationMs)
    {
        try
        {
            switch (action)
            {
                case IdleAction.Blink:
                    Blink(nowMs);
                    break;
                case IdleAction.EyeDart:
                    Dart(amount, nowMs, durationMs);
                    break;
                case IdleAction.HeadMove when Execute:
                    _ = _robot.Motion.SetHeadAngleAsync(
                        (float)(_robot.State.HeadAngleRad + amount * Math.PI / 180.0),
                        durationSec: (float)(durationMs / 1000.0));
                    break;
                case IdleAction.LiftMove when Execute:
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

    // ---------------------------------------------------------------- the face
    //
    // How the engine does this, and why the first attempt here was wrong.
    //
    // A dart is not a change to the face; it is a *layer* over it. The engine's
    // TrackLayerComponent::AddOrUpdateEyeShift(trackMask, name, x, y, durationMs, ...) calls
    // FaceLayerManager::GenerateEyeShift(..., durationMs, ProceduralFaceKeyFrame&) and stores the result
    // through AddPersistentLayer(name, Track<ProceduralFaceKeyFrame>). So each shift is a named,
    // time-limited keyframe combined onto a base face that is never itself modified, and
    // RemoveEyeShift takes it away again. Blinks work the same way, via AddBlink.
    //
    // The first implementation here instead read Face.Current, offset it, and wrote it back as the new
    // persistent face. Every dart therefore compounded the last: positions random-walked away from centre
    // and EyeScale grew by another 0.1 each time, until on hardware the two eyes merged into one large
    // rectangle. A blink then restored that corrupted pose rather than a stable one.
    //
    // What is reproduced here: a stable base pose, transient offsets from it with a duration, and a
    // return to base when the transient expires. What is NOT claimed is the exact native use of
    // EyeDartUpMaxScale and EyeDartDownMinScale; they are applied as clamps rather than as an invented
    // formula. Whatever the remaining fidelity question, nothing accumulates.

    /// <summary>The pose every transient is measured from. Never modified by idle.</summary>
    private ProceduralFacePose? _base;

    /// <summary>The transient currently displayed, and when it expires.</summary>
    private ProceduralFacePose? _transient;
    private double _transientEndsAtMs = double.NegativeInfinity;

    /// <summary>
    /// Captures the base pose the first time idle touches the face, so darts and blinks are measured from
    /// a stable starting point rather than from whatever the last one left behind.
    /// </summary>
    private ProceduralFacePose Base()
    {
        return _base ??= _robot.Face.Current.Clone();
    }

    /// <summary>
    /// Forgets the captured base, so the next idle action re-reads the face. Used when something else has
    /// taken the face over, because the base captured before is no longer what is on screen.
    /// </summary>
    public void ForgetBaseFace()
    {
        lock (_gate) { _base = null; _transient = null; _transientEndsAtMs = double.NegativeInfinity; }
    }

    /// <summary>Displays a transient pose for a while, after which the face returns to base.</summary>
    private void ShowTransient(ProceduralFacePose pose, double nowMs, double durationMs)
    {
        lock (_gate)
        {
            _transient = pose;
            _transientEndsAtMs = nowMs + Math.Max(1, durationMs);
        }
        if (Execute) _robot.Face.SetParameters(pose);
    }

    /// <summary>
    /// Puts the base pose back when the current transient has run its duration. This is the half that was
    /// missing: without it a dart stayed on screen until the next one moved it further.
    /// </summary>
    private void ExpireTransient(double nowMs)
    {
        ProceduralFacePose? restore = null;
        lock (_gate)
        {
            if (_transient is null || nowMs < _transientEndsAtMs) return;
            _transient = null;
            _transientEndsAtMs = double.NegativeInfinity;
            restore = _base?.Clone();
        }
        if (restore is not null && Execute) _robot.Face.SetParameters(restore);
    }

    /// <summary>A blink: lids closed over the base pose, then back to the base pose.</summary>
    private void Blink(double nowMs)
    {
        var shut = Base().Clone();
        shut.Left[(int)EyeParam.UpperLidY] = 1f;
        shut.Right[(int)EyeParam.UpperLidY] = 1f;
        // A blink is brief; the duration bounds it the same way a dart is bounded.
        ShowTransient(shut, nowMs, BlinkDurationMs);
    }

    /// <summary>How long the lids stay shut. The shipped parameters do not name a blink duration.</summary>
    private const double BlinkDurationMs = 100;

    /// <summary>
    /// An eye dart: both eyes shift from the base by the same amount, and the eye further from the
    /// direction of travel grows slightly, which is what EyeDartOuterEyeScaleIncrease describes.
    ///
    /// Everything is computed from the base and clamped, so repeating it cannot walk the eyes off centre
    /// or inflate them.
    /// </summary>
    private void Dart(double pixels, double nowMs, double durationMs)
    {
        var b = Base();
        var pose = b.Clone();
        float shift = (float)Math.Clamp(pixels, -_p.EyeDartMaxDistancePix, _p.EyeDartMaxDistancePix);

        pose.Left[(int)EyeParam.EyeCenterX] = b.Left[(int)EyeParam.EyeCenterX] + shift;
        pose.Right[(int)EyeParam.EyeCenterX] = b.Right[(int)EyeParam.EyeCenterX] + shift;

        var outer = shift < 0 ? pose.Right : pose.Left;
        var outerBase = shift < 0 ? b.Right : b.Left;
        outer[(int)EyeParam.EyeScaleX] = Scale(outerBase[(int)EyeParam.EyeScaleX]);
        outer[(int)EyeParam.EyeScaleY] = Scale(outerBase[(int)EyeParam.EyeScaleY]);

        ShowTransient(pose, nowMs, durationMs);
    }

    /// <summary>
    /// One eye's scale for a dart: the base scale plus the outer-eye increase, held inside the shipped
    /// min and max. Clamping against the parameters rather than adding freely is what stops the growth;
    /// the engine's exact use of EyeDartUpMaxScale and EyeDartDownMinScale is not established, so they
    /// are not invented into a formula here.
    /// </summary>
    private float Scale(float baseScale) =>
        (float)Math.Clamp(baseScale + _p.EyeDartOuterEyeScaleIncrease,
                          _p.EyeDartMinScale, _p.EyeDartMaxScale);

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
