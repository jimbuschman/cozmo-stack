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
        {
            // GenerateEyeShift draws whole pixels in both axes and a whole-millisecond duration with
            // RandIntInRange, each end inclusive.
            int reach = (int)_p.EyeDartMaxDistancePix;
            int dx = _random.Next(-reach, reach + 1), dy = _random.Next(-reach, reach + 1);
            int dur = _random.Next((int)_p.EyeDartMinDurationMs, (int)_p.EyeDartMaxDurationMs + 1);
            Raise(done, faceFree
                ? Do(IdleAction.EyeDart, nowMs, dx, dur, dy)
                : new IdleEvent(IdleAction.EyeDart, nowMs) { Suppressed = "the face track is owned" });
        }

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

    private IdleEvent Do(IdleAction action, double nowMs, double amount = 0, double durationMs = 0, double amountY = 0)
    {
        ActionCount++;
        bool motor = action is IdleAction.HeadMove or IdleAction.LiftMove or IdleAction.BodyMove;
        if (!motor || ExecuteMotors) Perform(action, nowMs, amount, durationMs, amountY);
        return new IdleEvent(action, nowMs) { Amount = amount, AmountY = amountY, DurationMs = durationMs };
    }

    /// <summary>
    /// Carries out one idle action on the robot.
    ///
    /// Blinks and eye darts go through the M5 procedural face as transient layers over a base pose, which
    /// is how the engine's FaceLayerManager composes them. Head and lift use the M4 motion API at the
    /// engine's own durations; the engine itself streams them as HeadAngle/LiftHeight keyframes of its live
    /// animation (UpdateLiveAnimation at 0x0057D5F8), a difference that is recorded, not hidden.
    /// Body movement is deliberately **not** driven: a 10 mm/s shuffle is within the engine's parameters,
    /// but sending wheel commands to an unattended robot is not something to switch on without watching
    /// it happen, so it is decided and reported and left for hardware acceptance to enable.
    /// </summary>
    private void Perform(IdleAction action, double nowMs, double amount, double durationMs, double amountY)
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
    // What is not established is the dart's lifecycle. GenerateEyeShift's keyframe is appended to a
    // persistent layer whose replay logic (ITrackLayerManager::ApplyLayersToFrame at 0x0058E644) trims
    // the layer to its last keyframe and resets its stream time once it runs out; read statically, that
    // does not settle whether the shifted gaze is held until the next dart or dropped. This keeps the
    // earlier reading, a transient that returns to base when its duration expires, and labels it as a
    // local reading rather than a recovered fact.

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

    /// <summary>The pose every transient is measured from. Never modified by idle.</summary>
    private ProceduralFacePose? _base;

    /// <summary>The transient in progress: a pose as a function of the time since it started, and when it ends.</summary>
    private Func<double, ProceduralFacePose>? _transient;
    private bool _transientVaries;
    private double _transientStartMs, _transientEndsAtMs = double.NegativeInfinity;

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

    /// <summary>
    /// Starts a transient that lasts <paramref name="durationMs"/> and then gives way to the base.
    /// <paramref name="varies"/> says whether the pose changes over the transient's life (a blink) or is
    /// held (a dart), so a held pose is rendered once rather than every tick.
    /// </summary>
    private void ShowTransient(Func<double, ProceduralFacePose> pose, double nowMs, double durationMs, bool varies)
    {
        lock (_gate)
        {
            _transient = pose;
            _transientVaries = varies;
            _transientStartMs = nowMs;
            _transientEndsAtMs = nowMs + Math.Max(1, durationMs);
        }
        if (Execute) _robot.Face.SetParameters(pose(0));
    }

    /// <summary>
    /// Advances the transient in progress: re-evaluates a time-varying one, such as a blink stepping
    /// through its frames, and puts the base pose back once it has run its duration.
    /// </summary>
    private void ExpireTransient(double nowMs)
    {
        ProceduralFacePose? show = null;
        lock (_gate)
        {
            if (_transient is null) return;
            if (nowMs < _transientEndsAtMs) show = _transientVaries ? _transient(nowMs - _transientStartMs) : null;
            else
            {
                _transient = null;
                _transientEndsAtMs = double.NegativeInfinity;
                show = _base?.Clone();
            }
        }
        if (show is not null && Execute) _robot.Face.SetParameters(show);
    }

    /// <summary>
    /// A blink: the engine's seven-frame squash-and-reopen sequence layered onto the base pose, then the
    /// base pose again. Between frames the engine interpolates linearly
    /// (<c>ProceduralFaceKeyFrame::GetInterpolatedFace</c>), which only shows on the 100 ms last frame.
    /// </summary>
    private void Blink(double nowMs)
    {
        var b = Base();
        ShowTransient(t => BlinkPose(b, t), nowMs, BlinkTotalMs, varies: true);
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
        var b = Base();
        var pose = DartPose(b, xPix, yPix, _p);
        ShowTransient(_ => pose, nowMs, durationMs, varies: false);
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
    internal static ProceduralFacePose DartPose(ProceduralFacePose b, int xPix, int yPix, IdleParameters p)
    {
        const float xMax = 5f, yMax = 5f;
        float x = xPix, y = yPix;
        float up = (float)p.EyeDartUpMaxScale, down = (float)p.EyeDartDownMinScale;
        float inc = (float)p.EyeDartOuterEyeScaleIncrease;

        float fy = MathF.Min(1f, (yMax - y) / (2f * yMax));
        float vertical = down + (up - down) * fy;
        float fx = MathF.Min(1f, MathF.Abs(x) / xMax);
        float towards = vertical * (1f + fx * inc);   // the eye on the side being looked towards
        float away = vertical * (1f - fx * inc);

        var pose = b.Clone();
        pose.FaceCenterX = b.FaceCenterX + x;
        pose.FaceCenterY = b.FaceCenterY + y;

        float leftFactor = x < 0 ? towards : away;
        float rightFactor = x < 0 ? away : towards;
        pose.Left[EyeParam.EyeScaleY] = Eye.Clip(EyeParam.EyeScaleY, b.Left[EyeParam.EyeScaleY] * leftFactor);
        pose.Right[EyeParam.EyeScaleY] = Eye.Clip(EyeParam.EyeScaleY, b.Right[EyeParam.EyeScaleY] * rightFactor);

        if (y > 0)
        {
            float converge = 2f * MathF.Min(1f, y / yMax);
            pose.Left[EyeParam.EyeCenterX] = b.Left[EyeParam.EyeCenterX] + converge;
            pose.Right[EyeParam.EyeCenterX] = b.Right[EyeParam.EyeCenterX] - converge;
        }
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
