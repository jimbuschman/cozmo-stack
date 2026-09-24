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
        _robot.SendMessage(new Protocol.FaceImage { Image = payload }, flush: true);
    }

    /// <summary>
    /// What the robot says it has played, straight out of <c>animState</c>. Null until the first one
    /// arrives, which leaves the scheduler unpaced rather than stalled.
    /// </summary>
    public int? AudioFramesPlayed => _robot.State.Animation?.NumAudioFramesPlayed;

    public void Audio(byte[]? mulawFrame)
    {
        if (mulawFrame is null) _robot.SendMessage(new AudioSilence(), flush: true);
        else _robot.SendMessage(new AudioSample { Samples = mulawFrame }, flush: true);
    }

    /// <summary>
    /// Sends the head keyframe the way the engine does: as <c>animHeadAngle</c> (0x93), the message
    /// <c>HeadAngleKeyFrame::GetStreamMessage</c> at 0x004F8C08 builds. The duration is stored as a u16
    /// there (<c>strh</c>), so it is truncated the same way here.
    ///
    /// This replaced a <c>SetHeadAngle</c> motor command carrying invented speed and acceleration values.
    /// The command moved the head on hardware, but it is not what the engine sends inside an animation and
    /// it discarded the keyframe's variability.
    /// </summary>
    public void Head(sbyte angleDeg, uint durationMs) =>
        _robot.SendMessage(new Protocol.HeadAngle { DurationTimeMs = (ushort)durationMs, AngleDeg = angleDeg }, flush: true);

    /// <summary>As <see cref="Head"/>, for <c>animLiftHeight</c> (0x94) from <c>LiftHeightKeyFrame::GetStreamMessage</c> at 0x004F8F80.</summary>
    public void Lift(byte heightMm, uint durationMs) =>
        _robot.SendMessage(new Protocol.LiftHeight { DurationTimeMs = (ushort)durationMs, HeightMm = heightMm }, flush: true);

    /// <summary>
    /// Opens the animation on the robot. AnimationStreamer::SendStartOfAnimation at 0x0057C400 in
    /// libcozmoEngine.so does exactly this, sending the tag as a single byte, and the robot reports that
    /// tag back in its AnimationState. Keyframes that arrive outside an open animation are ignored, which
    /// is why body motion did nothing before this was sent.
    /// </summary>
    public void AnimationStarted(byte tag) =>
        _robot.SendMessage(new StartOfAnimation { AnimId = tag }, flush: true);

    /// <summary>Closes it, as AnimationStreamer::SendEndOfAnimation does.</summary>
    public void AnimationEnded() => _robot.SendMessage(new EndOfAnimation(), flush: true);

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
        _robot.SendMessage(new BodyMotion { Speed = k.Speed, RadiusMm = radius }, flush: true);
        _bodyMoving = k.Speed != 0;
    }

    /// <summary>Zero speed on the straight radius, which is how the engine's own keyframe ends.</summary>
    public void BodyStop()
    {
        _bodyMoving = false;
        _robot.SendMessage(new BodyMotion { Speed = 0, RadiusMm = BodyKeyframe.StraightRadius }, flush: true);
    }

    /// <summary>Whether a body keyframe this sink sent is still driving (nothing has stopped it yet).</summary>
    private bool _bodyMoving;

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
        // The scheduler stops the body when its keyframe expires and again if an animation is cut short
        // (AnimationScheduler: BodyStop on both paths), so normally there is nothing to undo. What this
        // must not do is stop the wheels for every clip: a face or audio animation ending would then kill
        // a path or a direct drive that has nothing to do with it, which with M13 navigation and M15
        // autonomy running underneath is a real collision. Only body motion this animation started and
        // nothing has stopped is cleaned up, and with the keyframe's own stop rather than DriveWheels.
        if (_bodyMoving) BodyStop();
        _ = _lastFacePayload;
    }

    // fidelity: M1-025, M1-015
    /// <summary>Back to the state right after construction, for a removed robot (CB33, CC26, CC27). Subscribers are kept.</summary>
    internal void ResetToConstructed()
    {
        _lastFacePayload = null;
        _bodyMoving = false;
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

    /// <summary>The tag the running animation was opened with.</summary>
    public byte CurrentTag => _scheduler.CurrentTag;

    /// <summary>
    /// The tag the robot says it is playing, from its AnimationState stream. When this matches
    /// <see cref="CurrentTag"/> the robot has accepted the animation; while they differ it has not.
    /// </summary>
    public byte? RobotReportedTag => _robot.State.Animation?.Tag;

    /// <summary>Named events raised by a running animation.</summary>
    public event Action<string>? Event;
    /// <summary>Keyframes that were reached but whose effect is not implemented.</summary>
    public event Action<string>? NotImplemented;
    /// <summary>
    /// Raised when the tick loop stopped because the robot went away underneath it. The animation is
    /// ended and the loop exits; this is how a caller learns why.
    /// </summary>
    public event Action<Exception>? Faulted;

    /// <summary>Whether the animation tick loop is running. False once nothing is left for it to do.</summary>
    public bool IsTicking { get { lock (_gate) return _ticker is not null; } }

    /// <summary>Loads Cozmo's own animation assets from an unpacked resources tree.</summary>
    public AnimationLibrary LoadFrom(string assetsRoot)
    {
        // The pre-rendered face animations live beside the clips, and the faceAnimations track names them.
        FaceAnimations = FaceAnimationLibrary.Open(assetsRoot);
        _scheduler.FaceAnimations = FaceAnimations.Frames;
        return Library = AnimationLibrary.Open(assetsRoot);
    }

    /// <summary>The pre-rendered face animations found beside the clips, once <see cref="LoadFrom"/> has run.</summary>
    public FaceAnimationLibrary? FaceAnimations { get; private set; }

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

    /// <summary>
    /// Streams one keyframe of the engine's live animation - the keep-alive clip the streamer always has
    /// open - on the animation system's own clock, and keeps the tick loop running while that keyframe
    /// still has work outstanding.
    ///
    /// Both halves of that matter. <see cref="AnimationScheduler.StreamLive"/> records a body keyframe's
    /// stop time against the clock it is given, and <see cref="AnimationScheduler.Advance"/> is driven on
    /// the tick loop's clock, so the two have to be the same clock; and a keep-alive body keyframe runs
    /// while no clip is playing, so without this the loop would not be running to serve the deadline at
    /// all and <c>DriveWheels</c> would carry on past its duration. The engine has no such gap: its
    /// streamer updates every tick whether or not a real animation is streaming.
    ///
    /// Returns false when a running clip owns the keyframe's track, which is the engine's own condition.
    /// </summary>
    public bool StreamLive(Keyframe k)
    {
        bool streamed = _scheduler.StreamLive(k, NowMs());
        if (streamed && _scheduler.HasPendingWork) StartTicker();
        return streamed;
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

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26: the Robot and its AnimationStreamer
    /// are deleted; CC27: the next connect builds them afresh): the scheduler and the sink as built, so the live stream
    /// is closed and tags start again. The tick loop stops by itself once nothing is pending. The loaded assets
    /// (<see cref="Library"/>, <see cref="FaceAnimations"/>, <see cref="SoundNames"/>), <see cref="AudioSource"/> and
    /// the random source are kept, as are subscribers.
    /// </summary>
    internal void ResetToConstructed()
    {
        _scheduler.ResetToConstructed();
        _sink.ResetToConstructed();
    }

    /// <summary>
    /// Which animation is running, as an opaque token. A caller that starts an animation can keep this
    /// and later ask whether that same one is still playing.
    /// </summary>
    public long Generation => _scheduler.Generation;

    /// <summary>
    /// Stops the running animation only if it is still the one this token identifies.
    ///
    /// This is what lets a behaviour clean up after itself without harm: stopping a behaviour must end the
    /// animation it started, but must not end an unrelated one that has since replaced it.
    /// </summary>
    public bool StopIfCurrent(long generation) => _scheduler.StopIfCurrent(generation);

    /// <summary>
    /// Plays a clip and reports which animation it became, so the caller can stop exactly that one.
    /// Returns null when the scheduler refused it.
    /// </summary>
    public AnimationTicket? PlayTracked(string name, bool replaceRunning = true)
    {
        var lib = Library ?? throw new InvalidOperationException("no animation assets are loaded; call LoadFrom first");
        return PlayTracked(lib.GetClip(name), replaceRunning);
    }

    /// <summary>
    /// The same for a clip already in hand. The token comes from the playback itself rather than from a
    /// second look at <see cref="Generation"/>: between starting an animation and reading which one is
    /// running, another caller can have replaced it, and the ticket would then carry their token - so
    /// stopping by it would stop their animation and not this one.
    /// </summary>
    public AnimationTicket? PlayTracked(AnimationClip clip, bool replaceRunning = true)
    {
        var handle = _scheduler.Play(clip, NowMs(), replaceRunning);
        if (handle is null) return null;
        StartTicker();
        return new AnimationTicket(handle.Completion, handle.Generation);
    }

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
        while (true)
        {
            try
            {
                // fidelity: M1-041
                // CD12: Robot::Update runs AnimationStreamer::Update only after the first full state and only while
                // time synced and ready to stream. While that gate is shut nothing is streamed.
                // MISSING (M5): what the animation timeline does while the streamer is not updated is not in the M1
                // inventory; here the scheduler is simply not advanced, and it catches up to the clock once open.
                if (_robot.AnimationStreamingOpen) _scheduler.Advance(origin + sw.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                // The robot went away underneath the animation: every send throws from here on. This is a
                // background thread, so letting that escape would take the process with it. End the
                // animation - which completes whatever is awaiting it - and stop ticking.
                try { Faulted?.Invoke(ex); } catch { }     // a subscriber must not take the thread either
                try { _scheduler.Stop(); } catch { }
                lock (_gate) { _running = false; _ticker = null; }
                return;
            }

            // Deciding to stop and clearing _running must happen under the same lock StartTicker takes.
            // Previously the loop broke out first and cleared _running afterwards, which left a window
            // where a Play arriving in between saw _running still true, started no ticker, and left the
            // new animation with nothing to advance it.
            lock (_gate)
            {
                if (!_running) { _ticker = null; return; }       // Dispose asked us to stop
                if (!_scheduler.HasPendingWork)
                {
                    // Play sets the clip before calling StartTicker, so anything started before this
                    // check is seen here and keeps the loop alive; anything after it finds _running
                    // false and starts a fresh ticker. The same holds for a live keyframe: StreamLive
                    // arms its deadline before raising LiveWorkPending.
                    _running = false;
                    _ticker = null;
                    return;
                }
            }

            next += FrameIntervalMs;
            double wait = next - sw.Elapsed.TotalMilliseconds;
            if (wait > 3) Thread.Sleep((int)(wait - 2));
            else if (wait > 0) Thread.SpinWait(200);
            else next = sw.Elapsed.TotalMilliseconds;
        }
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
    public ProceduralFacePose Current { get; private set; } = ProceduralFacePose.ShippedNeutral();

    // fidelity: M1-025, M1-015
    /// <summary>Back to the state right after construction, for a removed robot (CB33, CC26, CC27): the shipped neutral pose.</summary>
    internal void ResetToConstructed() => Current = ProceduralFacePose.ShippedNeutral();

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

/// <summary>
/// A started animation and the token identifying it, so a caller can stop that one and no other.
/// </summary>
public sealed record AnimationTicket(Task<AnimationEndReason> Completion, long Generation);
