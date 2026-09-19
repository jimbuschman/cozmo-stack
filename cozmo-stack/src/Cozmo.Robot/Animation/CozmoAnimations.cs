using Cozmo.Protocol;

namespace Cozmo.Robot.Animation;

/// <summary>
/// Drives a real robot from the animation scheduler.
///
/// Every method here is called on the scheduler's tick, so the devices underneath never decide their own
/// animation timing. The face goes out as an animation keyframe, the audio as a frame on the same tick, and
/// the motors as ordinary commands whose acknowledgement the animation does not wait for: an animation is a
/// timeline, not a sequence of round trips.
/// </summary>
public sealed class RobotAnimationSink : IAnimationSink
{
    private readonly CozmoRobot _robot;
    private byte[]? _lastFacePayload;

    public RobotAnimationSink(CozmoRobot robot) => _robot = robot;

    /// <summary>Events the animation raised, newest last. The engine routes these to game code.</summary>
    public event Action<string>? AnimationEvent;
    /// <summary>Keyframes whose effect is not implemented yet, so a caller can see what was skipped.</summary>
    public event Action<string>? NotImplemented;

    public void Face(FaceBitmap bitmap)
    {
        var payload = FaceBitmapCodec.Encode(bitmap);
        if (payload.Length > _robot.Display.MaxPayload) return;    // never send a partial face
        _lastFacePayload = payload;
        _robot.Transport.Send(new Protocol.FaceImage { Image = payload }, flush: true);
    }

    public void Audio(byte[]? mulawFrame)
    {
        if (mulawFrame is null) _robot.Transport.Send(new AudioSilence(), flush: true);
        else _robot.Transport.Send(new AudioSample { Samples = mulawFrame }, flush: true);
    }

    public void Head(float radians, uint durationMs) =>
        _robot.Transport.Send(new SetHeadAngle(radians, 10f, 10f, durationMs / 1000f, 0), flush: true);

    public void Lift(float heightMm, uint durationMs) =>
        _robot.Transport.Send(new SetLiftHeight(heightMm, 3f, 20f, durationMs / 1000f, 0), flush: true);

    public void Body(BodyKeyframe k)
    {
        // The engine does not synthesise wheel speeds: it sends the speed and a 16-bit radius and lets the
        // firmware do the geometry, which is what makes an arc work without knowing the wheel base.
        // BodyMotionKeyFrame::GetStreamMessage at 0x004FBA8C builds exactly this message.
        if (k.EncodedRadius is not { } radius)
        {
            NotImplemented?.Invoke($"body motion radius '{k.RadiusRaw}' is not a token the engine understands");
            return;
        }
        _robot.Transport.Send(new BodyMotion { Speed = k.Speed, RadiusMm = radius }, flush: true);
    }

    /// <summary>Zero speed on the straight radius, which is how the engine's own keyframe ends.</summary>
    public void BodyStop() =>
        _robot.Transport.Send(new BodyMotion { Speed = 0, RadiusMm = BodyKeyframe.StraightRadius }, flush: true);

    public void Lights(LightsKeyframe k)
    {
        // The official engine does not implement this either. BackpackLightsKeyFrame::SetMembersFromFlatBuf
        // at 0x004FAAD4 in libcozmoEngine.so is a stub whose whole body logs "The
        // BackpackLightsKeyFrame::SetMembersFromFlatBuf() method still needs to be implemented" and returns
        // failure, so the light track of a .bin animation does nothing on a retail robot. Mapping these
        // five float arrays onto the wire message would be inventing behaviour Anki never shipped, so it is
        // reported rather than guessed.
        NotImplemented?.Invoke(
            "backpack lights keyframe ignored: the shipping engine never implemented this track from " +
            "animation assets (SetMembersFromFlatBuf is a stub), so there is no behaviour to reproduce");
    }

    public void Event(string eventId) => AnimationEvent?.Invoke(eventId);

    public void Finished(string clipName, bool completed)
    {
        // The scheduler stops the body when its keyframe expires and again if an animation is cut short,
        // so there is nothing to undo here. One last zero command is still cheap insurance against a
        // keyframe the scheduler did not know had started the wheels.
        _robot.Transport.Send(new DriveWheels(0f, 0f, 0f, 0f), flush: true);
        _ = _lastFacePayload;
    }
}

/// <summary>
/// The animation system as a robot sees it.
///
/// <code>
/// robot.Animations.LoadFrom(@"...\cozmo_resources\assets");
/// await robot.Animations.Play("anim_bored_01");
/// robot.Animations.Stop();
/// </code>
///
/// One background thread ticks the scheduler at 30 Hz while something is playing, so face, audio, motors
/// and lights all advance on the same clock.
/// </summary>
public sealed class CozmoAnimations : IDisposable
{
    private readonly CozmoRobot _robot;
    private readonly RobotAnimationSink _sink;
    private readonly AnimationScheduler _scheduler;
    private readonly Random _random = new();
    private readonly object _gate = new();
    private Thread? _ticker;
    private volatile bool _running;

    internal CozmoAnimations(CozmoRobot robot)
    {
        _robot = robot;
        _sink = new RobotAnimationSink(robot);
        _scheduler = new AnimationScheduler(_sink);
        _sink.AnimationEvent += e => Event?.Invoke(e);
        _sink.NotImplemented += w => NotImplemented?.Invoke(w);
    }

    /// <summary>The loaded asset library, or null until <see cref="LoadFrom"/> is called.</summary>
    public AnimationLibrary? Library { get; private set; }
    /// <summary>The scheduler, for callers that want to drive the timeline themselves.</summary>
    public AnimationScheduler Scheduler => _scheduler;

    public bool IsPlaying => _scheduler.IsPlaying;
    public string? Playing => _scheduler.Playing;
    public AnimationTrack OwnedTracks => _scheduler.OwnedTracks;

    /// <summary>Named events raised by a running animation.</summary>
    public event Action<string>? Event;
    /// <summary>Keyframes that were reached but whose effect is not implemented.</summary>
    public event Action<string>? NotImplemented;

    /// <summary>Loads Cozmo's own animation assets from an unpacked resources tree.</summary>
    public AnimationLibrary LoadFrom(string assetsRoot) => Library = AnimationLibrary.Open(assetsRoot);

    /// <summary>
    /// Where the sound for an audio keyframe comes from. Left null, audio keyframes keep the timeline but
    /// are silent. See <see cref="IAnimationAudioSource"/> and <see cref="WavAudioSource"/>.
    /// </summary>
    public IAnimationAudioSource? AudioSource
    {
        get => _scheduler.AudioSource;
        set => _scheduler.AudioSource = value;
    }

    /// <summary>
    /// Reads Cozmo's own sound metadata so audio events can at least be named. This resolves ids to names,
    /// not to audio: the event-to-file mapping lives in the banks, which are not parsed. Returns null when
    /// no metadata is found.
    /// </summary>
    public SoundBankIndex? LoadSoundNames(string soundRoot) => SoundNames = SoundBankIndex.Open(soundRoot);

    /// <summary>The sound metadata, once loaded.</summary>
    public SoundBankIndex? SoundNames { get; private set; }

    /// <summary>Names of every clip available.</summary>
    public IReadOnlyCollection<string> ClipNames => Library?.ClipNames ?? Array.Empty<string>();
    /// <summary>Names of every group available.</summary>
    public IReadOnlyCollection<string> GroupNames => Library?.GroupNames ?? Array.Empty<string>();

    /// <summary>
    /// Plays a clip by name and returns when it finishes, is cancelled or is replaced. Returns null without
    /// playing when <paramref name="replaceRunning"/> is false and a track it needs is already owned.
    /// </summary>
    public Task<AnimationEndReason>? Play(string name, bool replaceRunning = true)
    {
        var lib = Library ?? throw new InvalidOperationException("no animation assets are loaded; call LoadFrom first");
        return Play(lib.GetClip(name), replaceRunning);
    }

    /// <summary>Plays a clip that is already loaded or built in memory.</summary>
    public Task<AnimationEndReason>? Play(AnimationClip clip, bool replaceRunning = true)
    {
        var handle = _scheduler.Play(clip, NowMs(), replaceRunning);
        if (handle is null) return null;
        StartTicker();
        return handle.Completion;
    }

    /// <summary>Picks one animation from a group by weight and plays it.</summary>
    public Task<AnimationEndReason>? PlayGroup(string group, string? mood = null, bool replaceRunning = true)
    {
        var lib = Library ?? throw new InvalidOperationException("no animation assets are loaded; call LoadFrom first");
        var g = lib.GetGroup(group) ?? throw new KeyNotFoundException($"no animation group called '{group}'");
        var pick = g.Choose(_random, mood) ?? throw new InvalidOperationException($"group '{group}' is empty");
        return Play(pick.Name, replaceRunning);
    }

    /// <summary>Stops whatever is playing. Returns false when nothing was.</summary>
    public bool Stop() => _scheduler.Stop();

    private static double NowMs() => Environment.TickCount64;

    private void StartTicker()
    {
        lock (_gate)
        {
            if (_running) return;
            _running = true;
            _ticker = new Thread(TickLoop) { IsBackground = true, Name = "cozmo-animation" };
            _ticker.Start();
        }
    }

    private void TickLoop()
    {
        // The animation clock has to hold a 33 ms slot; on Windows a plain sleep is quantised to 15.6 ms,
        // which is half a frame, so the resolution is raised for as long as the loop runs.
        using var _ = new Cozmo.Transport.HighResolutionTimer();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double next = 0;
        double origin = NowMs();
        while (_running)
        {
            _scheduler.Advance(origin + sw.Elapsed.TotalMilliseconds);
            if (!_scheduler.IsPlaying) break;

            next += FrameIntervalMs;
            double wait = next - sw.Elapsed.TotalMilliseconds;
            if (wait > 3) Thread.Sleep((int)(wait - 2));
            else if (wait > 0) Thread.SpinWait(200);
            else next = sw.Elapsed.TotalMilliseconds;
        }
        lock (_gate) { _running = false; _ticker = null; }
    }

    private const double FrameIntervalMs = 1000.0 / AnimationScheduler.FrameRateHz;

    public void Dispose()
    {
        _scheduler.Stop();
        _running = false;
        Thread? t;
        lock (_gate) t = _ticker;
        if (t is not null && t != Thread.CurrentThread) t.Join(TimeSpan.FromSeconds(1));
    }
}

/// <summary>
/// The procedural face as a robot sees it: set the nineteen parameters per eye directly, or ask for a named
/// expression.
/// </summary>
public sealed class CozmoFace
{
    private readonly CozmoRobot _robot;
    internal CozmoFace(CozmoRobot robot) => _robot = robot;

    /// <summary>The pose last sent, so a caller can read it back and adjust one parameter.</summary>
    public ProceduralFacePose Current { get; private set; } = ProceduralFaceRenderer.Neutral();

    /// <summary>Renders a pose and shows it. Returns the bitmap that was sent, for inspection and tests.</summary>
    public FaceBitmap SetParameters(ProceduralFacePose pose)
    {
        Current = pose.Clone();
        var bmp = ProceduralFaceRenderer.Render(pose);
        _robot.Display.Show(bmp);
        return bmp;
    }

    /// <summary>Shows one of the built-in expressions.</summary>
    public FaceBitmap ShowExpression(Expression expression) => SetParameters(Expressions.Get(expression));

    /// <summary>Holds an expression on the face for a while, re-sending it at the animation rate.</summary>
    public void HoldExpression(Expression expression, TimeSpan duration)
    {
        var bmp = ProceduralFaceRenderer.Render(Expressions.Get(expression));
        Current = Expressions.Get(expression);
        _robot.Display.Hold(bmp, duration);
    }
}
