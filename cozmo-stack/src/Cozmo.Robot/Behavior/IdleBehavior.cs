using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

/// <summary>What the idle layer decided to do on one tick.</summary>
public enum IdleAction { None, Blink, EyeDart, HeadMove, LiftMove, BodyMove }

/// <summary>One idle action, with everything needed to reproduce and explain it.</summary>
public sealed record IdleEvent(IdleAction Action, double AtMs)
{
    /// <summary>The value the action used: radians for head, mm for lift, mm/s for body, horizontal pixels for a dart.</summary>
    public double Amount { get; init; }
    /// <summary>A dart's vertical shift in pixels; zero for every other action.</summary>
    public double AmountY { get; init; }
    public double DurationMs { get; init; }
    /// <summary>A body shuffle's radius token, <c>STRAIGHT</c> or <c>TURN_IN_PLACE</c>; null for the rest.</summary>
    public string? BodyRadius { get; init; }
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

    // The engine's own counters, in the engine's own units: whole milliseconds, run down by one tick's
    // worth on every tick. FaceLayerManager holds the two face timers at this+0x14 and this+0x18;
    // AnimationStreamer holds the three motion pairs at this+0x198..0x1AC and the idle clock at this+0x44.
    // All of them start at zero - FaceLayerManager's constructor at 0x0058CD70 writes a pair of zeroes and
    // AnimationStreamer memclr4s 0x18 bytes at 0x0057A03C - so every one of them is due on the first tick
    // it is allowed to run.
    private double _blinkMs, _dartMs;
    private double _bodyMs, _liftMs, _headMs;
    private double _bodyGapMs, _liftGapMs, _headGapMs;
    private double _idleMs;
    private double _lastTickMs;
    private bool _primed;

    /// <summary>
    /// The engine's main loop period, and therefore the quantum of every idle timer.
    ///
    /// <c>CozmoInstanceRunner::Run</c> at 0x0065B3A8 sets each iteration's deadline to
    /// <c>steady_clock::now() + 0x03938700 ns</c> - 60 000 000 ns, 60 ms - and calls
    /// <c>CozmoEngine::Update</c> once per iteration. Every idle countdown is decremented by exactly 60
    /// per call (<c>UpdateLiveAnimation</c> 0x0057D650, 0x0057D68A, 0x0057D6BA and <c>KeepFaceAlive</c>
    /// 0x0058D388), so the countdowns are in milliseconds and they move in 60 ms steps.
    ///
    /// That settles what the tunables mean: <c>BlinkSpacingMin_ms</c> really is 3000 milliseconds, not
    /// 3000 ticks. It also means an idle event can only happen on a 60 ms boundary, which is why this
    /// class turns the wall clock it is given into whole ticks rather than comparing against it.
    /// </summary>
    public const double EngineTickMs = 60;

    /// <summary>
    /// The most ticks one <see cref="Advance"/> makes up after a stall. The engine never catches up - it
    /// runs one tick per loop iteration however long the iteration took - but this class is driven by
    /// whatever clock its caller has, so a caller that stops calling for a minute should not get a
    /// minute of idle behaviour in one go.
    /// </summary>
    public const int MaxCatchUpTicks = 16;

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
        lock (_gate) { _primed = false; ActionCount = 0; }
    }

    /// <summary>
    /// Advances idle behaviour to this moment, doing at most one thing per category per engine tick.
    ///
    /// Returns what it did, so a caller can log it. An empty list means it deliberately did nothing -
    /// which is the normal case on most ticks, since the shortest spacing is 250 ms.
    /// </summary>
    public IReadOnlyList<IdleEvent> Advance(double nowMs)
    {
        var done = new List<IdleEvent>();

        if (!Arbiter.AutonomyEnabled)
        {
            lock (_gate) _primed = false;
            return done;
        }

        // Anything of higher priority running means the robot is not idle at all. The engine's equivalent
        // is the streamer simply not reaching its live-animation update while a real animation streams,
        // which also leaves the idle clock at this+0x44 zeroed (0x0057D000).
        if (Arbiter.Running is { } running && running > BehaviorPriority.Idle)
        {
            lock (_gate) _idleMs = 0;
            Raise(done, new IdleEvent(IdleAction.None, nowMs) { Suppressed = $"{running} is running" });
            return done;
        }

        var owned = _robot.Animations.OwnedTracks;
        bool faceFree = (owned & AnimationTrack.Face) == 0;

        // A dart or blink lasts for its own duration and no longer. Without this the last one simply
        // stayed on screen until the next moved it further, which is how the eyes drifted and grew. This
        // is the face being rendered, not a timer, so it runs on every call rather than once per tick.
        if (faceFree) ExpireTransient(nowMs);
        else ForgetBaseFace();   // something else owns the face; whatever base we had is stale

        int ticks;
        lock (_gate)
        {
            if (!_primed)
            {
                _primed = true;
                _blinkMs = _dartMs = 0;
                _bodyMs = _liftMs = _headMs = 0;
                _bodyGapMs = _liftGapMs = _headGapMs = 0;
                _idleMs = 0;
                _lastTickMs = nowMs - EngineTickMs;     // this call is the first tick
            }
            ticks = (int)Math.Floor((nowMs - _lastTickMs) / EngineTickMs);
            if (ticks > MaxCatchUpTicks) { ticks = MaxCatchUpTicks; _lastTickMs = nowMs; }
            else _lastTickMs += ticks * EngineTickMs;
        }

        for (int i = 0; i < ticks; i++) Tick(done, nowMs, owned);
        return done;
    }

    /// <summary>
    /// One engine tick of the keep-alive, in the engine's order: the two face timers first
    /// (<c>FaceLayerManager::KeepFaceAlive</c> 0x0058D374), then body, lift and head
    /// (<c>AnimationStreamer::UpdateLiveAnimation</c> 0x0057D5F8).
    ///
    /// Two things about the counters are worth stating because they are easy to get wrong and this class
    /// used to get both wrong:
    ///
    /// * <b>A timer that cannot fire is not rescheduled.</b> The engine decrements it and leaves it, so
    ///   the action happens on the first tick the track is free. It used to be rescheduled as though it
    ///   had fired, which quietly dropped every idle action taken during a caller animation.
    /// * <b>A motion timer counts the movement and the gap after it.</b> The countdown is set to the
    ///   movement's own duration when it starts and the gap is drawn separately; the next movement is due
    ///   when countdown + gap has run out, so it comes duration + gap later, not gap later.
    /// </summary>
    private void Tick(List<IdleEvent> done, double nowMs, AnimationTrack owned)
    {
        bool faceFree = (owned & AnimationTrack.Face) == 0;
        bool headFree = (owned & AnimationTrack.Head) == 0;
        bool liftFree = (owned & AnimationTrack.Lift) == 0;
        bool bodyFree = (owned & AnimationTrack.Body) == 0;

        // ---- the face. Both timers run down at the top of KeepFaceAlive, whatever happens next.
        lock (_gate)
        {
            _blinkMs -= EngineTickMs;
            _dartMs -= EngineTickMs;
        }

        // The dart is skipped entirely when the dart distance is zero, and it will not go on top of
        // another layer: KeepFaceAlive proceeds only when the layer manager holds no layer at all, or
        // holds exactly one and that one is the dart's own (0x0058D3B2..0x0058D3C4). A blink in progress
        // therefore holds the dart off, and the dart timer keeps counting while it waits.
        bool dartDue;
        lock (_gate) dartDue = _p.EyeDartMaxDistancePix > 0 && _dartMs <= 0;
        if (dartDue)
        {
            bool otherLayerUp = AnotherLayerIsUp();
            if (faceFree && !otherLayerUp)
            {
                // GenerateEyeShift draws whole pixels in both axes and a whole-millisecond duration with
                // RandIntInRange, each end inclusive.
                int reach = (int)_p.EyeDartMaxDistancePix;
                int dx = _random.Next(-reach, reach + 1), dy = _random.Next(-reach, reach + 1);
                int dur = RandInt(_p.EyeDartMinDurationMs, _p.EyeDartMaxDurationMs);
                lock (_gate) _dartMs = RandInt(_p.EyeDartSpacingMinMs, _p.EyeDartSpacingMaxMs);
                Raise(done, Do(IdleAction.EyeDart, nowMs, dx, dur, dy));
            }
            else
            {
                Raise(done, new IdleEvent(IdleAction.EyeDart, nowMs)
                {
                    Suppressed = otherLayerUp ? "another face layer is up" : "the face track is owned",
                });
            }
        }

        bool blinkDue;
        lock (_gate) blinkDue = _blinkMs <= 0;
        if (blinkDue)
        {
            if (faceFree)
            {
                lock (_gate) _blinkMs = RandInt(_p.BlinkSpacingMinMs, _p.BlinkSpacingMaxMs);
                Raise(done, Do(IdleAction.Blink, nowMs, durationMs: BlinkTotalMs));
            }
            else
            {
                Raise(done, new IdleEvent(IdleAction.Blink, nowMs) { Suppressed = "the face track is owned" });
            }
        }

        // ---- motion. UpdateLiveAnimation returns before any of it until the robot has been idle for
        // TimeBeforeWiggleMotions_ms (0x0057D612), which is what stops it twitching the instant it is
        // put down. The idle clock itself only advances on the ticks that reach the live animation.
        bool mayMove;
        lock (_gate)
        {
            mayMove = _idleMs >= _p.TimeBeforeWiggleMotionsMs;
            _idleMs += EngineTickMs;
        }
        if (!mayMove) return;

        // ---- body
        if (Due(ref _bodyMs, ref _bodyGapMs, bodyFree))
        {
            int duration = RandInt(_p.BodyMovementDurationMinMs, _p.BodyMovementDurationMaxMs);
            int speed = _random.Next(-(int)_p.BodyMovementSpeedMmps, (int)_p.BodyMovementSpeedMmps + 1);
            bool straight = _random.NextDouble() <= _p.BodyMovementStraightFraction;
            lock (_gate) _bodyMs = duration;
            Raise(done, Do(IdleAction.BodyMove, nowMs, speed, duration,
                           radius: straight ? StraightToken : TurnInPlaceToken));
            lock (_gate) _bodyGapMs = RandInt(_p.BodyMovementSpacingMinMs, _p.BodyMovementSpacingMaxMs);
        }
        else if (!bodyFree)
        {
            Raise(done, new IdleEvent(IdleAction.BodyMove, nowMs) { Suppressed = "the body track is owned" });
        }

        // ---- lift
        if (Due(ref _liftMs, ref _liftGapMs, liftFree))
        {
            int duration = RandInt(_p.LiftMovementDurationMinMs, _p.LiftMovementDurationMaxMs);
            lock (_gate) _liftMs = duration;
            Raise(done, Do(IdleAction.LiftMove, nowMs, _p.LiftHeightMeanMm, duration));
            lock (_gate) _liftGapMs = RandInt(_p.LiftMovementSpacingMinMs, _p.LiftMovementSpacingMaxMs);
        }
        else if (!liftFree)
        {
            Raise(done, new IdleEvent(IdleAction.LiftMove, nowMs) { Suppressed = "the lift track is owned" });
        }

        // ---- head
        if (Due(ref _headMs, ref _headGapMs, headFree))
        {
            int duration = RandInt(_p.HeadMovementDurationMinMs, _p.HeadMovementDurationMaxMs);
            lock (_gate) _headMs = duration;
            Raise(done, Do(IdleAction.HeadMove, nowMs, (_robot.State.HeadAngleRad ?? 0f) * 180.0 / Math.PI, duration));
            lock (_gate) _headGapMs = RandInt(_p.HeadMovementSpacingMinMs, _p.HeadMovementSpacingMaxMs);
        }
        else if (!headFree)
        {
            Raise(done, new IdleEvent(IdleAction.HeadMove, nowMs) { Suppressed = "the head track is owned" });
        }
    }

    /// <summary>The radius token a straight keep-alive shuffle carries; the engine sends 0x7FFF for it.</summary>
    public const string StraightToken = "STRAIGHT";

    /// <summary>The radius token a keep-alive turn carries; the engine sends 0 for it.</summary>
    public const string TurnInPlaceToken = "TURN_IN_PLACE";

    /// <summary>
    /// A motion timer: due when the movement's remaining duration plus the gap after it has run out and
    /// the track is free. Not due, but still counting, otherwise - including while the track is owned,
    /// which is what makes the action happen the moment the track comes free.
    /// </summary>
    private bool Due(ref double countdownMs, ref double gapMs, bool free)
    {
        lock (_gate)
        {
            if (free && countdownMs + gapMs <= 0) return true;
            countdownMs -= EngineTickMs;
            return false;
        }
    }

    /// <summary>
    /// A whole-millisecond draw, inclusive at both ends, as <c>RandomGenerator::RandIntInRange</c> makes
    /// it. Every spacing and duration the keep-alive draws goes through it; the engine casts the float
    /// tunable to int first (<c>vcvt.s32.f32</c> at 0x0058D45A) and so does this.
    /// </summary>
    private int RandInt(double min, double max) => _random.Next((int)min, (int)max + 1);

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

    private IdleEvent Do(IdleAction action, double nowMs, double amount = 0, double durationMs = 0,
                         double amountY = 0, string? radius = null)
    {
        ActionCount++;
        bool motor = action is IdleAction.HeadMove or IdleAction.LiftMove or IdleAction.BodyMove;
        if (!motor || ExecuteMotors) Perform(action, nowMs, amount, durationMs, amountY, radius);
        return new IdleEvent(action, nowMs)
        {
            Amount = amount,
            AmountY = amountY,
            DurationMs = durationMs,
            BodyRadius = radius,
        };
    }

    /// <summary>
    /// Carries out one idle action on the robot.
    ///
    /// Blinks and eye darts go through the M5 procedural face as transient layers over a base pose, which
    /// is how the engine's FaceLayerManager composes them.
    ///
    /// Head, lift and body are keyframes of the engine's live animation, not motor commands.
    /// <c>UpdateLiveAnimation</c> at 0x0057D5F8 appends them to the streamer's own always-open
    /// <c>Animation</c> (this+0xA8, <c>SetIsLive(true)</c>) and they reach the robot as the ordinary
    /// animHeadAngle 0x93, animLiftHeight 0x94 and animBodyMotion 0x99 stream messages:
    ///
    /// <list type="bullet">
    /// <item><c>HeadAngleKeyFrame(currentAngleDeg, HeadAngleVariability_deg, duration)</c> at 0x0057D85C.
    /// The angle is the robot's head angle right now, truncated to whole degrees; the movement comes
    /// entirely from the variability, which is drawn at stream time, not here.</item>
    /// <item><c>LiftHeightKeyFrame(LiftHeightMean_mm, LiftHeightVariability_mm, duration)</c> at
    /// 0x0057D9C0 - 35 and 8, again drawn at stream time.</item>
    /// <item><c>BodyMotionKeyFrame(speed, radius, duration)</c> at 0x0057D8EA, with radius 0x7FFF for a
    /// straight shuffle and 0 for a turn on the spot.</item>
    /// </list>
    ///
    /// The turn carries an eye shift with it (0x0057D7FC) and the straight one takes any such shift away
    /// again (<c>RemoveEyeShift</c> at 0x0057D8D2), so the eyes lead the turn and settle when he drives
    /// straight. Its constants are the call's own, not the eye-dart tunables: a 33 ms shift over a
    /// 64 x 32 range with 1.1 / 0.85 / 0.1 for the three scales.
    /// </summary>
    private void Perform(IdleAction action, double nowMs, double amount, double durationMs, double amountY,
                         string? radius)
    {
        try
        {
            switch (action)
            {
                case IdleAction.Blink:
                    Blink(nowMs);
                    break;
                case IdleAction.EyeDart:
                    Dart((int)amount, (int)amountY, nowMs, durationMs);
                    break;
                case IdleAction.HeadMove when Execute:
                    _robot.Animations.StreamLive(
                        new HeadKeyframe(0, (uint)durationMs,
                                         (sbyte)Math.Clamp((int)amount, sbyte.MinValue, sbyte.MaxValue),
                                         (byte)_p.HeadAngleVariabilityDeg));
                    break;
                case IdleAction.LiftMove when Execute:
                    _robot.Animations.StreamLive(
                        new LiftKeyframe(0, (uint)durationMs,
                                         (byte)Math.Clamp((int)amount, byte.MinValue, byte.MaxValue),
                                         (byte)_p.LiftHeightVariabilityMm));
                    break;
                case IdleAction.BodyMove when Execute:
                    // on the animation system's clock, not the idle tick's: the keyframe's stop time is
                    // served by the animation tick loop
                    _robot.Animations.StreamLive(
                        new BodyKeyframe(0, (uint)durationMs, radius ?? StraightToken,
                                         (short)amount));
                    if (radius == TurnInPlaceToken) TurnEyeShift((short)amount, nowMs);
                    else ClearTurnEyeShift();
                    break;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // A robot that went away mid-idle is not an idle bug.
        }
    }

    /// <summary>
    /// The eye shift a keep-alive turn carries, from 0x0057D754..0x0057D7FC: the horizontal shift is a
    /// whole number of pixels in 0..21 taking the sign of the drawn wheel speed, the vertical one is in
    /// -10..10, and the layer - the engine names it <c>LiveIdleTurn</c> - lasts 33 ms.
    ///
    /// The five trailing constants of the <c>AddOrUpdateEyeShift</c> call are its own, not the eye-dart
    /// tunables: xMax 64, yMax 32, and 1.1 / 0.85 / 0.1 where a dart uses 5, 5 and the three
    /// <c>EyeDart*Scale</c> parameters.
    /// </summary>
    private void TurnEyeShift(short speed, double nowMs)
    {
        int x = Math.Sign(speed) * _random.Next(0, TurnShiftMaxXPix + 1);
        int y = _random.Next(-TurnShiftMaxYPix, TurnShiftMaxYPix + 1);
        ShowLayer(IdleAction.BodyMove,
                  (face, _) => LookAt(face, x, y, TurnShiftXRange, TurnShiftYRange, TurnShiftUpMaxScale,
                                      TurnShiftDownMinScale, TurnShiftOuterEyeScaleIncrease),
                  nowMs, nowMs + TurnShiftDurationMs, nowMs);
    }

    /// <summary>
    /// A straight shuffle takes the turn's eye shift away again (<c>RemoveEyeShift</c>, 0x0057D8D2), and
    /// only that one: the engine removes the layer by its own tag, so a blink in progress is untouched.
    /// </summary>
    private void ClearTurnEyeShift()
    {
        bool removed;
        lock (_gate) removed = _layers.RemoveAll(l => l.Kind == IdleAction.BodyMove) > 0;
        if (removed) Render(double.NaN);
    }

    /// <summary>
    /// Whether any layer other than the dart's own is up, which holds the next dart off:
    /// <c>KeepFaceAlive</c> proceeds only when the manager holds no layer at all, or exactly one and
    /// that one is the dart's (0x0058D3B2..0x0058D3C4).
    /// </summary>
    private bool AnotherLayerIsUp()
    {
        lock (_gate) return _layers.Any(l => l.Kind != IdleAction.EyeDart);
    }

    /// <summary>The horizontal draw for a turn's eye shift: RandIntInRange(0, 21) at 0x0057D75C.</summary>
    public const int TurnShiftMaxXPix = 21;
    /// <summary>The vertical draw: RandIntInRange(-10, 10) at 0x0057D76C.</summary>
    public const int TurnShiftMaxYPix = 10;
    /// <summary>The shift lasts one 33 ms frame.</summary>
    public const uint TurnShiftDurationMs = 33;
    internal const float TurnShiftXRange = 64f;
    internal const float TurnShiftYRange = 32f;
    internal const float TurnShiftUpMaxScale = 1.1f;
    internal const float TurnShiftDownMinScale = 0.85f;
    internal const float TurnShiftOuterEyeScaleIncrease = 0.1f;

    // ---------------------------------------------------------------- the face
    //
    // How the engine does this. The idle face is the streamer's base face with named, time-limited
    // layers combined onto it: FaceLayerManager::KeepFaceAlive at 0x0058D374 counts down a blink timer
    // and an eye-dart timer and, when one expires, generates a layer and adds it with AddLayer (blink)
    // or AddPersistentLayer / AddToPersistentLayer (dart). ProceduralFace::Combine at 0x005846A8
    // applies a layer by ADDING its eye centres, eye angles and lid angles, MULTIPLYING its eye scales
    // and face scales, adding its face angle and adding its face position. A layer built from a
    // default-constructed ProceduralFace (scales 1, everything else 0) is therefore the identity, and a
    // blink or dart is expressed purely as multipliers on the scales and offsets on the positions. The
    // base face is never written.
    //
    // The first implementation here read Face.Current, offset it and wrote it back, so every dart
    // compounded the last and the eyes drifted and grew until they merged on hardware. The second kept a
    // base pose but invented both the blink (upper lids shut for 100 ms) and the dart geometry (an
    // EyeCenterX shift, the eye away from the dart grown by 0.1, scales clamped to 0.92..1.08). Both are
    // now taken from the binary:
    //
    //   * Blink: ProceduralFaceDrawer::GetNextBlinkFrame at 0x00585F18 steps through a fixed table of
    //     seven frames (BlinkFrames below), each a pair of multipliers on EyeScaleX and EyeScaleY and a
    //     duration, then restores the face. FaceLayerManager::GenerateBlink at 0x0058D2AC turns them into
    //     keyframes whose trigger times are the cumulative durations.
    //   * Dart: FaceLayerManager::GenerateEyeShift at 0x0058D100 draws x and y in
    //     [-EyeDartMaxDistance, +EyeDartMaxDistance] pixels and a duration in
    //     [EyeDartMinDuration, EyeDartMaxDuration], then calls ProceduralFace::LookAt at 0x00584158 with
    //     (x, y, 5, 5, EyeDartUpMaxScale, EyeDartDownMinScale, EyeDartOuterEyeScaleIncrease). LookAt
    //     moves the WHOLE FACE by (x, y), scales EyeScaleY only (up looks bigger, down smaller, and the
    //     eye on the side being looked towards a little bigger than the other), and turns the eyes
    //     inwards when looking down. See Dart below for the arithmetic.
    //
    // The dart's lifecycle, which used to be the open question here, is settled by the layer machinery:
    // a dart ramps to its gaze and then holds it, and the eyes do not come back to centre in between.
    //
    //   * ITrackLayerManager::ApplyLayersToFrame at 0x0058E644 queues a layer for removal when it runs
    //     out only if it is NOT persistent (the branch at 0x0058E6A0, taken when the byte at layer+0x30
    //     is zero). A persistent layer that runs out is rewound to its first keyframe and trimmed of the
    //     ones already consumed (0x0058E682..0x0058E698), so it keeps applying its last face for ever.
    //     The dart goes in through AddToPersistentLayer; the blink through AddLayer, which is not.
    //   * The drawn EyeDartDuration is not how long the gaze lasts, it is how long the move to it takes.
    //     AddToPersistentLayer at 0x0058EAA0 sets the new keyframe's trigger time to
    //     lastKeyFrameTime + drawn + 0x21, and GetFaceHelper at 0x0058CD80 interpolates between the
    //     keyframe before it and it (ProceduralFaceKeyFrame::GetInterpolatedFace, 0x004F99E6, a linear
    //     blend clamped at 1) until that time arrives. With no keyframe beyond, it applies the last face
    //     unchanged.
    //
    // GenerateEyeShift at 0x0058CFC4 also clamps the shift against the eyes' bounding box so they stay
    // on the 128 x 64 screen (GetEyeBoundingBox and the two mins at 0x0058D004 and 0x0058D026). A six
    // pixel dart never reaches that from the resting face, so it is not reproduced here.

    /// <summary>
    /// The engine's blink, from the table <c>ProceduralFaceDrawer::GetNextBlinkFrame</c> copies out of
    /// .rodata at 0x00C5AAD8: seven frames of (EyeScaleX multiplier, EyeScaleY multiplier, duration ms).
    /// The eyes squash flat while widening, hold shut for one frame, then reopen; the last frame lingers
    /// for 100 ms before the base face is restored. Each frame lasts one 33 ms animation frame except the
    /// last.
    /// </summary>
    public static readonly IReadOnlyList<(float ScaleX, float ScaleY, uint DurationMs)> BlinkFrames = new[]
    {
        (1.05f, 0.85f, 33u),
        (1.2f, 0.6f, 33u),
        (2.5f, 0.1f, 33u),
        (5.0f, 0.05f, 33u),     // the closed frame; the engine also flips its scanline parity here
        (2.0f, 0.15f, 33u),
        (1.2f, 0.7f, 33u),
        (1.0f, 0.9f, 100u),
    };

    /// <summary>The whole blink, first frame to base restored: 6 x 33 + 100 + 33 = 331 ms.</summary>
    public static readonly double BlinkTotalMs = BlinkFrames.Sum(f => f.DurationMs) + 33;

    /// <summary>The pose every layer is measured from. Never modified by idle.</summary>
    private ProceduralFacePose? _base;

    /// <summary>
    /// One named face layer, as <c>ITrackLayerManager</c> holds them: something applied to the face that
    /// is there so far, for as long as it lasts.
    ///
    /// The engine keeps a list of these, not one at a time, and <c>ProceduralFace::Combine</c> at
    /// 0x005846A8 folds each onto the result of the last: eye centres, eye angles, lid angles, face angle
    /// and face position add, eye scales and face scales multiply. So a blink and an eye dart at the same
    /// moment are a squash <em>and</em> a shifted gaze - which is exactly what the keep-alive produces on
    /// its very first tick, since every timer starts at zero. One slot for one transient dropped whichever
    /// of the two came second.
    /// </summary>
    private sealed record FaceLayer(IdleAction Kind, Func<ProceduralFacePose, double, ProceduralFacePose> Apply,
                                    double StartMs, double EndsAtMs, double VariesUntilMs);

    private readonly List<FaceLayer> _layers = new();

    /// <summary>
    /// Where the eyes are looking now. The dart layer is persistent, so a dart does not fade: the gaze it
    /// reached is the gaze the next dart starts from.
    /// </summary>
    private GazeShift _gaze;

    /// <summary>When the face was last composed, so a layer is drawn once more after it stops moving.</summary>
    private double _lastRenderMs = double.NegativeInfinity;

    /// <summary>One streamed animation frame, which a dart's ramp is one longer than its drawn duration.</summary>
    public const double AnimationFrameMs = 33;

    /// <summary>
    /// Captures the base pose the first time idle touches the face, so darts and blinks are measured from
    /// a stable starting point rather than from whatever the last one left behind.
    /// </summary>
    private ProceduralFacePose Base() => _base ??= _robot.Face.Current.Clone();

    /// <summary>
    /// Forgets the captured base and every layer on it, so the next idle action re-reads the face. Used
    /// when something else has taken the face over, because the base captured before is no longer what is
    /// on screen.
    /// </summary>
    public void ForgetBaseFace()
    {
        lock (_gate)
        {
            _base = null;
            _layers.Clear();
            _gaze = default;
        }
    }

    /// <summary>
    /// Adds a layer, or replaces the one of the same kind already there - which is what
    /// <c>AddOrUpdateEyeShift</c> does by tag and <c>AddLayer</c> does by name.
    /// </summary>
    private void ShowLayer(IdleAction kind, Func<ProceduralFacePose, double, ProceduralFacePose> apply,
                           double nowMs, double endsAtMs, double variesUntilMs)
    {
        lock (_gate)
        {
            _layers.RemoveAll(l => l.Kind == kind);
            _layers.Add(new FaceLayer(kind, apply, nowMs, endsAtMs, variesUntilMs));
        }
        Render(nowMs);
    }

    /// <summary>
    /// Drops the layers that have run out and redraws if anything changed or anything left is still
    /// moving. With nothing left the base comes back, untouched by any of it.
    /// </summary>
    private void ExpireTransient(double nowMs)
    {
        bool redraw;
        lock (_gate)
        {
            int before = _layers.Count;
            _layers.RemoveAll(l => nowMs >= l.EndsAtMs);
            // A layer that has stopped moving still needs one last draw, so a ramp lands exactly on its
            // end value rather than on wherever the last tick before it happened to fall.
            redraw = _layers.Count != before || _layers.Any(l => l.VariesUntilMs > _lastRenderMs);
        }
        if (redraw) Render(nowMs);
    }

    /// <summary>Composes the layers onto the base, oldest first, and puts the result on the screen.</summary>
    private void Render(double nowMs)
    {
        ProceduralFacePose pose;
        lock (_gate)
        {
            pose = Base().Clone();
            foreach (var l in _layers) pose = l.Apply(pose, nowMs - l.StartMs);
            if (!double.IsNaN(nowMs)) _lastRenderMs = nowMs;
        }
        if (Execute) _robot.Face.SetParameters(pose);
    }

    /// <summary>
    /// A blink: the engine's seven-frame squash-and-reopen sequence layered onto the base pose, then the
    /// base pose again. Between frames the engine interpolates linearly
    /// (<c>ProceduralFaceKeyFrame::GetInterpolatedFace</c>), which only shows on the 100 ms last frame.
    /// </summary>
    private void Blink(double nowMs)
    {
        ShowLayer(IdleAction.Blink, (face, t) => BlinkPose(face, t),
                  nowMs, nowMs + BlinkTotalMs, nowMs + BlinkTotalMs);
    }

    /// <summary>
    /// The blink layer at <paramref name="elapsedMs"/> since the blink started, combined onto
    /// <paramref name="b"/>. Keyframe i is reached at the cumulative time of frames 0..i and the final
    /// keyframe restores unit multipliers; before the first keyframe is reached the base shows unchanged.
    /// </summary>
    internal static ProceduralFacePose BlinkPose(ProceduralFacePose b, double elapsedMs)
    {
        // keyframe times and multipliers, the last being the restore frame
        var times = new double[BlinkFrames.Count + 1];
        var sx = new float[BlinkFrames.Count + 1];
        var sy = new float[BlinkFrames.Count + 1];
        double t = 0;
        for (int i = 0; i < BlinkFrames.Count; i++)
        {
            t += BlinkFrames[i].DurationMs;
            times[i] = t; sx[i] = BlinkFrames[i].ScaleX; sy[i] = BlinkFrames[i].ScaleY;
        }
        times[^1] = t + 33; sx[^1] = 1f; sy[^1] = 1f;

        float mx = 1f, my = 1f;
        if (elapsedMs >= times[0])
        {
            int i = 0;
            while (i + 1 < times.Length && elapsedMs >= times[i + 1]) i++;
            if (i + 1 < times.Length)
            {
                float k = (float)((elapsedMs - times[i]) / (times[i + 1] - times[i]));
                mx = sx[i] + (sx[i + 1] - sx[i]) * k;
                my = sy[i] + (sy[i + 1] - sy[i]) * k;
            }
            else { mx = sx[^1]; my = sy[^1]; }
        }

        var pose = b.Clone();
        foreach (var eye in new[] { pose.Left, pose.Right })
        {
            eye[EyeParam.EyeScaleX] = Eye.Clip(EyeParam.EyeScaleX, eye[EyeParam.EyeScaleX] * mx);
            eye[EyeParam.EyeScaleY] = Eye.Clip(EyeParam.EyeScaleY, eye[EyeParam.EyeScaleY] * my);
        }
        return pose;
    }

    /// <summary>
    /// An eye dart: the engine's <c>GenerateEyeShift</c> draws both a horizontal and a vertical shift in
    /// pixels, each uniformly in <c>[-EyeDartMaxDistance, +EyeDartMaxDistance]</c> as whole numbers, and
    /// hands them to <c>ProceduralFace::LookAt</c> with the shipped scale parameters. The result is a
    /// whole-face move with the eye heights following the gaze; see <see cref="DartPose"/>.
    /// </summary>
    private void Dart(int xPix, int yPix, double nowMs, double durationMs)
    {
        // The layer is persistent and the shift is a ramp, not a snap: see the summary above.
        var from = _gaze;
        var to = Gaze(xPix, yPix, 5f, 5f, (float)_p.EyeDartUpMaxScale, (float)_p.EyeDartDownMinScale,
                      (float)_p.EyeDartOuterEyeScaleIncrease);
        double ramp = durationMs + AnimationFrameMs;
        lock (_gate) _gaze = to;
        ShowLayer(IdleAction.EyeDart,
                  (face, t) => ApplyGaze(face, GazeShift.Lerp(from, to, ramp <= 0 ? 1f : (float)Math.Clamp(t / ramp, 0, 1))),
                  nowMs, double.PositiveInfinity, nowMs + ramp);
    }

    /// <summary>
    /// <c>ProceduralFace::LookAt(x, y, xMax, yMax, lookUpMaxScale, lookDownMinScale,
    /// outerEyeScaleIncrease)</c> at 0x00584158, applied as a layer onto <paramref name="b"/>, with the
    /// arguments <c>FaceLayerManager::GenerateEyeShift</c> at 0x0058D100 passes: xMax = yMax = 5 and the
    /// three scale parameters from the idle tunables.
    ///
    /// <list type="bullet">
    /// <item>The face position moves by (x, y): <c>SetFacePosition</c>, added to the base's centre.</item>
    /// <item>A vertical factor <c>v = down + (up - down) * min(1, (yMax - y) / (2 yMax))</c>: 1.1 looking
    /// fully up, 0.975 straight ahead, 0.85 looking fully down.</item>
    /// <item>A horizontal asymmetry <c>h = min(1, |x| / xMax) * outerEyeScaleIncrease</c>. Looking left
    /// (x &lt; 0) the left eye's EyeScaleY becomes <c>v (1 + h)</c> and the right eye's <c>v (1 - h)</c>;
    /// looking right the reverse. Only EyeScaleY changes; EyeScaleX does not.</item>
    /// <item>Looking down (y &gt; 0) the eyes converge: EyeCenterX moves by <c>+2 min(1, y / yMax)</c> on
    /// the left eye and the negative of that on the right.</item>
    /// </list>
    ///
    /// Two things the engine does that this does not: <c>SetFacePosition</c> clamps the move so the eyes'
    /// bounding box stays on the 128 x 64 canvas, which a six-pixel shift never reaches from the resting
    /// face; and every scale passes through <c>ProceduralFace::Clip</c>, which only enforces a floor of
    /// zero here. <c>EyeDartMinScale</c> and <c>EyeDartMaxScale</c> are not consulted by this path in the
    /// engine and are not applied.
    /// </summary>
    internal static ProceduralFacePose DartPose(ProceduralFacePose b, int xPix, int yPix, IdleParameters p) =>
        LookAt(b, xPix, yPix, 5f, 5f, (float)p.EyeDartUpMaxScale, (float)p.EyeDartDownMinScale,
               (float)p.EyeDartOuterEyeScaleIncrease);

    /// <summary>
    /// <c>ProceduralFace::LookAt</c> itself, with its seven arguments given rather than assumed, because
    /// the keep-alive calls it with two different sets: a dart passes 5, 5 and the three
    /// <c>EyeDart*Scale</c> tunables, a turn's eye shift passes 64, 32, 1.1, 0.85 and 0.1.
    /// </summary>
    internal static ProceduralFacePose LookAt(ProceduralFacePose b, float x, float y, float xMax, float yMax,
                                              float up, float down, float inc) =>
        ApplyGaze(b.Clone(), Gaze(x, y, xMax, yMax, up, down, inc));

    /// <summary>
    /// What <c>LookAt</c> writes into the layer, as numbers rather than as a pose. A layer is built on a
    /// default-constructed face, so these <em>are</em> the layer's parameters, and interpolating a layer
    /// - which is what <c>ProceduralFaceKeyFrame::GetInterpolatedFace</c> at 0x004F99E6 does between two
    /// keyframes - is interpolating these.
    /// </summary>
    internal readonly record struct GazeShift(float X, float Y, float LeftScaleY, float RightScaleY, float Converge)
    {
        public static GazeShift Identity => new(0, 0, 1, 1, 0);

        public static GazeShift Lerp(GazeShift a, GazeShift b, float f)
        {
            if (a == default) a = Identity;
            if (b == default) b = Identity;
            return new(a.X + (b.X - a.X) * f,
                       a.Y + (b.Y - a.Y) * f,
                       a.LeftScaleY + (b.LeftScaleY - a.LeftScaleY) * f,
                       a.RightScaleY + (b.RightScaleY - a.RightScaleY) * f,
                       a.Converge + (b.Converge - a.Converge) * f);
        }
    }

    /// <summary><c>ProceduralFace::LookAt</c> at 0x00584158, as the shift it produces.</summary>
    internal static GazeShift Gaze(float x, float y, float xMax, float yMax, float up, float down, float inc)
    {
        float fy = MathF.Min(1f, (yMax - y) / (2f * yMax));
        float vertical = down + (up - down) * fy;
        float fx = MathF.Min(1f, MathF.Abs(x) / xMax);
        float towards = vertical * (1f + fx * inc);   // the eye on the side being looked towards
        float away = vertical * (1f - fx * inc);
        return new GazeShift(x, y,
                             x < 0 ? towards : away,
                             x < 0 ? away : towards,
                             y > 0 ? 2f * MathF.Min(1f, y / yMax) : 0f);
    }

    /// <summary>Combines a gaze shift onto a face, as ProceduralFace::Combine does: positions and the
    /// convergence add, the eye scales multiply.</summary>
    internal static ProceduralFacePose ApplyGaze(ProceduralFacePose pose, GazeShift g)
    {
        if (g == default) g = GazeShift.Identity;
        pose.FaceCenterX += g.X;
        pose.FaceCenterY += g.Y;
        pose.Left[EyeParam.EyeScaleY] = Eye.Clip(EyeParam.EyeScaleY, pose.Left[EyeParam.EyeScaleY] * g.LeftScaleY);
        pose.Right[EyeParam.EyeScaleY] = Eye.Clip(EyeParam.EyeScaleY, pose.Right[EyeParam.EyeScaleY] * g.RightScaleY);
        pose.Left[EyeParam.EyeCenterX] += g.Converge;
        pose.Right[EyeParam.EyeCenterX] -= g.Converge;
        return pose;
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
