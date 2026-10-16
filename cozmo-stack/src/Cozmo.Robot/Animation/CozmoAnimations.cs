using Cozmo.Protocol;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation;

// fidelity: M5-004, M5-006, M5-013, M5-016, M5-025, M5-026, M5-033
/// <summary>
/// The stream's messages onto a real robot: each sink call sends one EngineToRobot message through the engine's send
/// path (every message reliable, not hot; M1-026, M3-014), and reports whether it went out (C14).
/// </summary>
public sealed class RobotAnimationSink : IAnimationSink
{
    private readonly CozmoRobot _robot;

    public RobotAnimationSink(CozmoRobot robot) => _robot = robot;

    /// <summary>Events the animation raised, newest last, for this stack's callers.</summary>
    public event Action<string>? AnimationEvent;
    /// <summary>Keyframes whose effect is not implemented, so a caller can see what was skipped.</summary>
    public event Action<string>? NotImplemented;

    /// <summary>A picture from a caller (this stack's API): encoded as the display encodes it and sent as 0x97.</summary>
    public void Face(FaceBitmap bitmap) => FaceImage(FaceBitmapCodec.Encode(bitmap));

    /// <summary>
    /// 0x97 FaceImage with the stream's payload (BufferFaceToSend, M3 B5; a sprite frame's stored RLE, C12). A payload
    /// larger than one frame holds is not sent (<see cref="CozmoDisplay.MaxPayload"/>).
    /// </summary>
    public void FaceImage(byte[] payload)
    {
        if (payload.Length > _robot.Display.MaxPayload) { LastSendSucceeded = true; return; }
        Send(new Protocol.FaceImage { Image = payload });
    }

    // fidelity: M3-012
    /// <summary>Whether the last message this sink sent went out (the stream counts only successful sends, 0x0057BFAE).</summary>
    public bool LastSendSucceeded { get; private set; } = true;

    private void Send(RobotMessage m) => LastSendSucceeded = _robot.SendMessage(m, flush: true);

    // fidelity: M3-012
    /// <summary>
    /// The Robot's numAudioFramesPlayed (+0x240), which only the time-synced AnimationState handler writes (C10) and
    /// the Robot constructor zeroes (C13): the engine budget applies from the first frame.
    /// The offline seam (<see cref="CozmoRobot.CreateOffline"/>, no engine thread and no robot playing anything)
    /// reports null, which leaves the scheduler unpaced, until an AnimationState has been handled.
    /// </summary>
    public int? AudioFramesPlayed => Paced ? _robot.Engine.Robot?.NumAudioFramesPlayed ?? 0 : null;

    /// <summary>The Robot's numAnimBytesPlayed (+0x238), as <see cref="AudioFramesPlayed"/> (C10, C13).</summary>
    public int? AnimBytesPlayed => Paced ? _robot.Engine.Robot?.NumAnimBytesPlayed ?? 0 : null;

    private bool Paced => _robot.Engine.IsProduction || (_robot.Engine.Robot?.AnimationStateHandled ?? false);

    public void Audio(byte[]? mulawFrame)
    {
        if (mulawFrame is null) Send(new AudioSilence());
        else Send(new AudioSample { Samples = mulawFrame });
    }

    /// <summary>0x93 animHeadAngle, the duration a u16 (strh, C2).</summary>
    public void Head(sbyte angleDeg, uint durationMs) =>
        Send(new Protocol.HeadAngle { DurationTimeMs = (ushort)durationMs, AngleDeg = angleDeg });

    /// <summary>0x94 animLiftHeight (C3).</summary>
    public void Lift(byte heightMm, uint durationMs) =>
        Send(new Protocol.LiftHeight { DurationTimeMs = (ushort)durationMs, HeightMm = heightMm });

    /// <summary>0x9B StartOfAnimation{tag} (A21).</summary>
    public void AnimationStarted(byte tag) => Send(new StartOfAnimation { AnimId = tag });

    /// <summary>0x9A EndOfAnimation (A20).</summary>
    public void AnimationEnded() => Send(new EndOfAnimation());

    /// <summary>
    /// 0x99 BodyMotion {speed, radius} (C4, C5). A radius token the engine does not understand is rejected at load; a
    /// keyframe built in code with one sends nothing.
    /// </summary>
    public void Body(BodyKeyframe k)
    {
        if (k.EncodedRadius is not { } radius)
        {
            NotImplemented?.Invoke($"body motion radius '{k.RadiusRaw}' is not a token the engine understands");
            LastSendSucceeded = true;
            return;
        }
        Send(new BodyMotion { Speed = k.Speed, RadiusMm = radius });
    }

    /// <summary>0x99 BodyMotion {0, 0x7FFF}: the body keyframe's own stop (C5).</summary>
    public void BodyStop() => Send(new BodyMotion { Speed = 0, RadiusMm = BodyKeyframe.StraightRadius });

    /// <summary>Not called by the stream (the backpack track is <see cref="BackpackLights"/>).</summary>
    public void Lights(LightsKeyframe k) { }

    /// <summary>0x98 BackpackLights, five u16 in the order Left, Front, Middle, Back, Right (C18).</summary>
    public void BackpackLights(ushort[] leds) => Send(new Protocol.BackpackLights { Field0 = (ushort[])leds.Clone() });

    /// <summary>0x95 Event {u8 AnimEvent} (C15).</summary>
    public void AnimEvent(byte animEvent) => Send(new Protocol.Event { Field0 = animEvent });

    /// <summary>0x91 RecordHeading, empty (C19).</summary>
    public void RecordHeading() => Send(new Protocol.RecordHeading());

    // fidelity: M5-033
    /// <summary>
    /// 0x92 TurnToRecordedHeading (gap4 T1, T2): the 13 bytes from keyframe +0x10 in order, s16 offset_deg, s16 speed,
    /// s16 accel, s16 decel, u16 tolerance, u16 numHalfRevs, u8 useShortestDir.
    /// </summary>
    public void TurnToRecordedHeading(TurnToRecordedHeadingKeyframe k) => Send(ToMessage(k));

    internal static Protocol.TurnToRecordedHeading ToMessage(TurnToRecordedHeadingKeyframe k) => new()
    {
        Field0 = unchecked((ushort)k.OffsetDeg),
        Field1 = unchecked((ushort)k.SpeedDegPerSec),
        Field2 = unchecked((ushort)k.AccelDegPerSec2),
        Field3 = unchecked((ushort)k.DecelDegPerSec2),
        Field4 = k.ToleranceDeg,
        Field5 = k.NumHalfRevs,
        Field6 = (byte)(k.UseShortestDir ? 1 : 0),
    };

    public void Event(string eventId) => AnimationEvent?.Invoke(eventId);

    /// <summary>
    /// An animation ended. Nothing is sent: the engine's Abort sends the robot nothing itself and SendEndOfAnimation is
    /// the only end (A20, A24); a body keyframe's stop is the keyframe's own message (C5).
    /// </summary>
    public void Finished(string clipName, bool completed) { }

    // fidelity: M1-025, M1-015
    /// <summary>Back to the state right after construction, for a removed robot (CB33, CC26, CC27). Subscribers are kept.</summary>
    internal void ResetToConstructed() => LastSendSucceeded = true;
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
    private readonly AnimationLibrary _emptyLibrary;
    private readonly Random _random = new();
    private readonly object _gate = new();
    private Thread? _ticker;
    private volatile bool _running;

    internal CozmoAnimations(CozmoRobot robot)
    {
        _robot = robot;
        robot.Engine.DestroyDataLoader = DestroyLoader;
        _sink = new RobotAnimationSink(robot);
        DrivingAnimations = new DrivingAnimationHandler(robot);
        // fidelity: M5-045
        _scheduler = new AnimationScheduler(_sink, contextRandom: robot.Engine.ContextRandom);
        _scheduler.Stream.Log = robot.Engine.Log;
        _scheduler.Stream.SourceLog = (level, channel, key, text) => robot.Engine.EmitSourceLog(level, channel, key, text);
        _scheduler.Log = robot.Engine.Log;
        _scheduler.SourceLog = (level, channel, key, text) => robot.Engine.EmitSourceLog(level, channel, key, text);
        _scheduler.FlushSourceLogger = robot.Engine.FlushSourceLogger;
        // fidelity: M5-010, M5-065 — loader containers exist empty before files are loaded.
        _emptyLibrary = new AnimationLibrary(robot.Engine.ContextRandom) { Log = robot.Engine.Log, SourceLog = _scheduler.SourceLog };
        _scheduler.CatalogProvider = () => Library ?? _emptyLibrary;
        _scheduler.FaceAnimationVariants = name => FaceAnimations?.Variants(name);
        // fidelity: M6-028
        // CreateAudioAnimation (0x00599FF0) switches on RobotAudioClient's output source, which HandleFirmwareVersion sets (CozmoEngine robot AudioOutputSource);
        // a robot with no firmware version yet has the client constructor's 0 (None).
        robot.Engine.CreateGoToSleepSequence = _ => GoToSleepAnimation.Create(robot, SleepTriggerFactory);
        _scheduler.OutputSource = () => robot.Engine.Robot?.AudioOutputSource;
        _sink.AnimationEvent += e => Event?.Invoke(e);
        _sink.NotImplemented += w => NotImplemented?.Invoke(w);
        _scheduler.NotImplemented += w => NotImplemented?.Invoke(w);
        // fidelity: M5-023
        // A23: RobotEventHandler subscribes to the E2G AnimationAborted (tag 95); the broadcast is synchronous, and its
        // handler calls Robot::AbortAnimation → SendAbortAnimation: AbortAnimation 0x8D, reliable, sent directly
        // (0x00517DE4..0x00517E0A), not through the stream buffer.
        _scheduler.AnimationAborted += _ =>
        {
            if (robot.Engine.Robot is null) robot.Engine.EmitEngineLog(EngineLogLevel.Warning, null, "RobotEventHandler.HandleAnimationAborted.InvalidRobotID", "Failed to find robot.");
            else { robot.SendMessage(new AbortAnimation()); robot.Engine.EmitEngineLog(EngineLogLevel.Info, "Unnamed", "RobotEventHandler.HandleAnimationAborted.SendingRobotAbortAnimation", ""); }
        };
        // fidelity: M5-030
        // UpdateLiveAnimation's robot inputs (gap4 L2..L6, gap1 R2..R4), from the last RobotState the robot stored: status bit
        // 2 (DockingComponent+4), IS_MOVING (+9), !LIFT_IN_POS (+0xB), !HEAD_IN_POS (+0xA); the track locks (M4-014); the head
        // angle (robot+0x2FC). Before any state they read clear, as the components' constructors leave them.
        var live = _scheduler.LiveIdleInputs;
        live.PickingOrPlacing = () => robot.State.Latest?.Has(RobotStatusFlag.IsPickingOrPlacing) ?? false;
        live.Moving = () => robot.State.Latest?.Has(RobotStatusFlag.IsMoving) ?? false;
        live.LiftNotInPosition = () => robot.State.Latest is { } s && !s.Has(RobotStatusFlag.LiftInPos);
        live.HeadNotInPosition = () => robot.State.Latest is { } s && !s.Has(RobotStatusFlag.HeadInPos);
        live.LockedTracks = () => robot.Motion.LockedTracks;
        // fidelity: M7-008
        // [[robot+0x284]+8] + 1 != 0: CarryingComponent's carried-object id is not -1 (0x0057D97E..0x0057D9CA). The M12 carrying
        // state reaches this robot as Motion.IsCarryingObject, wired by the manipulation system after construction, so it is read
        // at call time; unwired reads as not carrying.
        live.Carrying = () => robot.Motion.IsCarryingObject?.Invoke() == true;
        live.HeadAngleRad = () => robot.State.HeadAngleRad ?? 0f;
    }

    // fidelity: M5-053, M5-044
    internal void BindRobotLifetime(EngineRobot robot)
    {
        // fidelity: M5-010 — streamer constructor, once per native Robot, never on load completion.
        _scheduler.LoadNeutralFace();
        DrivingAnimations.Bind(robot);
        robot.ActionList.GameSend = completion => _robot.Engine.AnimationMessages.Deliver(RobotCompletedAction.UnionTag, true, completion);
        var driving = new AnimationSubscriptionOwners(_robot.Engine, new byte[] { 0x5A, 0xB5, 0xB6 }, true, (tag, payload) =>
        {
            if (tag == 0x5A && payload is RobotCompletedAction done) DrivingAnimations.HandleActionCompleted(done.Tag, done.Result);
            else if (tag == 0xB5 && payload is PushDrivingAnimationMessage push) DrivingAnimations.Push(push.Animations, push.LockName);
            else if (tag == 0xB6 && payload is string name) DrivingAnimations.Remove(name);
        });
        var parameter = new AnimationSubscriptionOwners(_robot.Engine, new byte[] { 0xB4 }, false, (_, payload) =>
        {
            if (payload is AnimationParameterMessage p) _scheduler.HandleSetParameters(p.Names, p.Values, p.SetUnspecifiedToDefault);
        });
        var streamer = new AnimationSubscriptionOwners(_robot.Engine, new byte[] { 0xAE, 0xAF, 0xAD, 0xC4 }, false);
        robot.Lifetime.Bind(0x24C, _ =>
        {
            driving.Release();
            DrivingAnimations.ClearEntriesForDestroy(); // vector strings/storage only, 0x004F090C..0x004F0942.
        });
        _scheduler.ReleaseSetupHandlers = streamer.Release;
        _scheduler.ReleaseParameterSubscription = parameter.Release;
        robot.Lifetime.Bind(0x60, _ => Dispose());
        // fidelity: M6-030 — the scheduler's audio stand-in is not the native client/Dispatch/controller ownership graph.
        robot.Lifetime.Bind(0x58,_=>Behavior.SteppedBehavior.ReportMissing("M6-030 RobotAudioClient teardown 0x00599A6C..0x00599ADA: native Dispatch Stop/Release and controller-presence/current-animation ownership are absent. Abort/Clear, unregister 7,8,9,10,6, deque/hash/base recipients remain unbound; no assumed registration or fabricated Unregister Error. Scheduler.Destroy already drops its audio stand-in and cannot substitute for this client sequence"));
    }

    // fidelity: M5-063 — same handler as the subscriptions, not a parallel implementation.
    internal DrivingAnimationHandler DrivingAnimations { get; }
    internal Cozmo.Robot.Behavior.AnimationTriggerMap? DrivingTriggerMap { get; set; }
    public void PushDrivingAnimations(AnimationTrigger start, AnimationTrigger loop, AnimationTrigger end, string lockName)
    {
        var payload = new PushDrivingAnimationMessage(new(start, loop, end), lockName);
        _robot.Engine.PostGameMessage(() => _robot.Engine.AnimationMessages.Deliver(0xB5, false, payload));
    }
    public void RemoveDrivingAnimations(string lockName)
        => _robot.Engine.PostGameMessage(() => _robot.Engine.AnimationMessages.Deliver(0xB6, false, lockName));

    // fidelity: M5-062 — host supplies the app B4 payload; delivery is on the engine thread.
    public void SetLiveIdleAnimationParameters(IReadOnlyList<LiveIdleParam> names, IReadOnlyList<float> values, bool setUnspecifiedToDefault = false)
    {
        var payload = new AnimationParameterMessage(names.Select(p => unchecked((byte)p)).ToArray(), values.ToArray(), setUnspecifiedToDefault);
        _robot.Engine.PostGameMessage(() => _robot.Engine.AnimationMessages.Deliver(0xB4, false, payload));
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

    /// <summary>
    /// Whether the animation is being advanced. On a production robot the engine tick advances it while streaming is
    /// open (CD12) and anything is pending; on the offline seams, whether the tick loop thread is running.
    /// </summary>
    public bool IsTicking
    {
        get
        {
            if (EngineDriven) return _robot.AnimationStreamingOpen && _scheduler.HasPendingWork;
            lock (_gate) return _ticker is not null;
        }
    }

    // fidelity: M3-013
    /// <summary>
    /// Whether the engine tick drives the streamer: Robot::Update runs AnimationStreamer::Update each 60 ms tick while
    /// synced and ready to stream (CD12), one engine Update of the streamer per tick (C15). The offline seams have no
    /// engine thread, so there the tick loop thread below stands in for it.
    /// </summary>
    internal bool EngineDriven => _robot.Engine.IsProduction;

    // fidelity: M13-021 - host exclusion shared with all existing layer writers.
    internal object NativeLayerGate => _scheduler.NativeLayerGate;
    internal void WithTrackLayers(Action<TrackLayerComponent> body) => _scheduler.WithTrackLayers(body);

    // fidelity: M3-013
    /// <summary>AnimationStreamer::Update for one engine tick (called by Robot::Update only while streaming is open).</summary>
    internal int EngineUpdate()
    {
        if (!EngineDriven) return 0;
        try { return _scheduler.Advance(_robot.Engine.Timer.TimeStampMs, _robot.Engine.Timer.Nanoseconds); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            _robot.Engine.EmitStackLog($"Animation host handler isolated (M1-034): {ex}");
            try { Faulted?.Invoke(ex); } catch { }
            try { _scheduler.Stop(); } catch { }
            return 0;
        }
    }

    /// <summary>Something was buffered for the stream outside an Advance (a policy API): make sure a drain will run.</summary>
    internal void KickStream() => StartTicker();

    private int _collectRan;

    // fidelity: M5-050, M5-055
    private readonly object _loaderGate = new();
    private Thread? _loader;
    private bool _loaderDestroyed;
    internal Action<string>? LoaderDestructionObserved;
    private void DestroyLoader()
    {
        Thread? loader;
        lock (_loaderGate)
        {
            _loaderDestroyed = true;
            loader = _loader;
            if (loader is not null) Library?.AbortLoading(); // nonzero thread handle gate.
        }
        if (loader is not null)
        {
            LoaderDestructionObserved?.Invoke("abort");
            if (loader != Thread.CurrentThread) loader.Join();
            LoaderDestructionObserved?.Invoke("join");
        }
    }

    /// <summary>Loads Cozmo's own animation assets from an unpacked resources tree.</summary>
    public AnimationLibrary LoadFrom(string assetsRoot)
    {
        var mapDirectory = AnimationTriggerMap.ResourceDirectory(assetsRoot);
        DrivingTriggerMap = mapDirectory is null ? null : AnimationTriggerMap.LoadDirectory(mapDirectory,
            _robot.Engine.EmitStackLog, (level,channel,key,text) => _robot.Engine.EmitSourceLog(level,channel,key,text), DrivingTriggerMap);
        AnimationLibrary? loaded = null;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        // X9 G1: LoadNonConfigData owns a loader thread; the API waits for that load's result.
        var loader = new Thread(() => { try { loaded = LoadOnLoaderThread(assetsRoot); } catch (Exception e) { failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e); } }) { IsBackground = true };
        lock (_loaderGate)
        {
            ObjectDisposedException.ThrowIf(_loaderDestroyed, this);
            _loader = loader;
            loader.Start();
        }
        loader.Join(); failure?.Throw(); return loaded!;
    }
    private AnimationLibrary LoadOnLoaderThread(string assetsRoot)
    {
        Library = AnimationLibrary.OpenForLoad(assetsRoot, _robot.Engine.Log,
            (level, channel, key, text) => _robot.Engine.EmitSourceLog(level, channel, key, text),
            _scheduler.ContextRandom,
            // Consume freshness when collection starts, even if a later file load fails.
            () => Interlocked.Exchange(ref _collectRan, 1) == 0,
            null, () =>
            {
                // L0 / M4-036: after groups (0x0051F3BC), lights (0x0051F3C2/3C8), then faces (3CE).
                // Cube files have just been consumed from the type-2 collect vector; backpack logs remain deferred.
                _robot.EmitLightLoadingLogs();
                FaceAnimations = FaceAnimationLibrary.Open(assetsRoot, _robot.Engine.Log,
                    (level, channel, key, text) => _robot.Engine.EmitSourceLog(level, channel, key, text));
            }, _robot.BeforeAnimationFileStat, lib =>
            {
                lock (_loaderGate)
                {
                    Library = lib;
                    if (_loaderDestroyed) lib.AbortLoading();
                }
            }, cubeFiles => _robot.Lights.Cubes.Animations.LoadCollected(_robot.Engine.Options.ResourcesPath,
                cubeFiles, _robot.Engine.Log,
                (level, channel, key, text) => _robot.Engine.EmitSourceLog(level, channel, key, text)),
                cubeResourceRoot:()=>_robot.Engine.Options.ResourcesPath,
                // Reuse the full multi-file container; do not parse it again through a separate adapter.
                triggerResponses:DrivingTriggerMap?.ToResponses() ?? new AnimationTriggerResponses());
        Library.Log = _robot.Engine.Log;
        Library.SourceLog = (level, channel, key, text) => _robot.Engine.EmitSourceLog(level, channel, key, text);
        // D5: the head-angle gate reads robot+0x2FC (the reported head angle, radians)
        Library.HeadAngleRad = () => _robot.State.HeadAngleRad;
        // fidelity: M5-045
        // R45-2..R45-3: loader -> container+0x28 -> group+0 uses context+0x14.
        Library.ContextRandom = _scheduler.ContextRandom;
        bool reportedMoodClock = false;
        Library.CooldownTimeSec = () =>
        {
            if (!reportedMoodClock)
            {
                reportedMoodClock = true;
                _robot.Engine.EmitStackLog("MISSING: M5-011 MoodManager+0x130 clock input (M7, 0x0058A95C/0x0067B67E); existing BaseStationTimer stand-in remains unverified");
            }
            return _robot.Engine.Timer.Seconds;
        }; // M7 owns the cached clock. This job does not build or claim that recipient.
        return Library;
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
        if (RejectCustomSong(name)) return null;
        var lib = Library ?? throw new InvalidOperationException("no animation assets are loaded; call LoadFrom first");
        return Play(lib.GetClip(name), replaceRunning);
    }

    // fidelity: M9-029 — approved CodeLab/internal-SDK exclusion, native name producer 0x00543F3A..0x00543F96.
    internal bool RejectCustomSong(string name) => _scheduler.RejectCustomSong(name);

    /// <summary>Plays a clip that is already loaded or built in memory.</summary>
    public Task<AnimationEndReason>? Play(AnimationClip clip, bool replaceRunning = true)
    {
        var handle = _scheduler.Play(clip, NowMs(), replaceRunning);
        if (handle is null) return null;
        StartTicker();
        return handle.Completion;
    }

    /// <summary>
    /// A tool and test seam (R-BEH2 batch 2): appends one keyframe to the engine's live animation (+0xA8) on the
    /// animation system's own clock, and keeps the tick loop running while that keyframe still has work outstanding.
    /// It does not push ProceduralLive: the live animation streams only while ProceduralLive is the top of the idle
    /// stack (the engine has no pusher of it), so a caller pushes it through <c>Scheduler.PushIdleAnimation</c> first.
    /// The engine's own source of live keyframes is <c>AnimationStreamer::UpdateLiveAnimation</c>.
    ///
    /// Both halves of that matter. <see cref="AnimationScheduler.StreamLive"/> records a body keyframe's
    /// stop time against the clock it is given, and <see cref="AnimationScheduler.Advance"/> is driven on
    /// the tick loop's clock, so the two have to be the same clock; and a keep-alive body keyframe runs
    /// while no clip is playing, so without this the loop would not be running to serve the deadline at
    /// all and <c>DriveWheels</c> would carry on past its duration. The engine has no such gap: its
    /// streamer updates every tick whether or not a real animation is streaming.
    ///
    /// Returns false while any animation is streaming (the streamer reaches the live animation only in its
    /// no-animation path), whatever track the keyframe is on.
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
        string name = lib.GetAnimationNameFromGroup(group, strict: false);
        return name.Length == 0 ? null : Play(name, replaceRunning);
    }

    // fidelity: M5-020
    /// <summary>
    /// Plays the clip a shipped trigger resolves to, the same chain the engine walks for a trigger
    /// (M5-034 D8 <c>AnimationTriggerResponsesContainer::GetResponse</c> then M5-011's group choice): the
    /// trigger names an animation group in the shipped <c>AnimationTriggerMap.json</c>, and the group picks a
    /// clip. Returns null when the shipped map names no group for the trigger.
    /// </summary>
    public Task<AnimationEndReason>? PlayTrigger(AnimationTrigger trigger, bool replaceRunning = true)
    {
        var lib = Library ?? throw new InvalidOperationException("no animation assets are loaded; call LoadFrom first");
        var group = lib.GetAnimationForTrigger(trigger);
        if (string.IsNullOrEmpty(group)) return null;
        return PlayGroup(group, replaceRunning: replaceRunning);
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
        DrivingAnimations.ResetToConstructed();
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
    public AnimationTicket? PlayTracked(string name, bool replaceRunning = true, AnimationTrack lockTracks = AnimationTrack.None, int numLoops = 1, byte heldLockMask = 0, string? heldLockOwner = null)
    {
        if (RejectCustomSong(name)) return null;
        var lib = Library ?? throw new InvalidOperationException("no animation assets are loaded; call LoadFrom first");
        return PlayTracked(lib.GetClip(name), replaceRunning, lockTracks, numLoops, heldLockMask, heldLockOwner);
    }

    /// <summary>
    /// The same for a clip already in hand. The token comes from the playback itself rather than from a
    /// second look at <see cref="Generation"/>: between starting an animation and reading which one is
    /// running, another caller can have replaced it, and the ticket would then carry their token - so
    /// stopping by it would stop their animation and not this one.
    /// </summary>
    public AnimationTicket? PlayTracked(AnimationClip clip, bool replaceRunning = true, AnimationTrack lockTracks = AnimationTrack.None, int numLoops = 1, byte heldLockMask = 0, string? heldLockOwner = null)
    {
        // IActionRunner::Update 0x00540370: AreAnyTracksLocked(mask) at 0x00540440; a required track that is held fails the action with
        // 0x03000019 (0x00540572..0x0054057c) - it is not retried - so this returns null. Otherwise MovementComponent::LockTracks 0x00640098
        // takes one owner entry per track, released by UnlockTracks 0x0063fe5c. A caller whose action already took the lock (the behaviour's
        // action locks before its Init, 0x00540584..0x0054058e) passes it in: it is released here when the animation ends.
        if (RejectCustomSong(clip.Name)) return null;
        byte mask = heldLockOwner is null ? CozmoMotion.MaskFor(lockTracks) : heldLockMask;
        string? owner = heldLockOwner;
        if (mask != 0 && owner is null)
        {
            if (_robot.Motion.AreAnyTracksLocked(mask))
            {
                _robot.Engine.Log("warning: Action not running because required tracks are locked");
                return null;
            }
            // The lock owner is to_string(IActionRunner +0x60), the action's id from the same tag counter every
            // action draws from (0x0053fe54..0x0053fe68; LockTracks 0x00540584..0x0054058a via 0x004f0f4c).
            owner = _robot.Motion.NextActionTag();
            _robot.Motion.LockTracks(mask, owner);
        }
        var handle = _scheduler.Play(clip, NowMs(), replaceRunning, numLoops);
        if (handle is null)
        {
            if (owner is not null && heldLockOwner is null) _robot.Motion.UnlockTracks(mask, owner);   // a held lock is the caller's to release
            return null;
        }
        StartTicker();
        if (owner is not null)
        {
            // IActionRunner::Update 0x00540370 releases the lock as the action ends (MovementComponent::
            // UnlockTracks 0x0063fe5c). Register it as a synchronous end callback so the unlock happens in
            // EndHandleLocked, in the action's own end path, before a following play checks the tracks -
            // not on a thread-pool continuation of the handle's Completion.
            byte lockedMask = mask;
            string lockedOwner = owner;
            int released = 0;
            void Release()
            {
                if (System.Threading.Interlocked.Exchange(ref released, 1) == 0)
                    _robot.Motion.UnlockTracks(lockedMask, lockedOwner);
            }
            handle.OnEnd(_ => Release());
            if (!handle.IsRunning) Release();   // ended between Play returning and the callback being registered
        }
        return new AnimationTicket(handle.Completion, handle.Generation);
    }

    internal Func<SleepAnimationArguments, ActionRunner?>? SleepTriggerFactory { get; set; } // M8 recipient seam, named MISSING when absent.

    internal bool ManualTicking { get; set; }
    internal Func<double>? ClockMs { get; set; }
    private double NowMs() => ClockMs?.Invoke() ?? (EngineDriven ? _robot.Engine.Timer.TimeStampMs : Environment.TickCount64);
    internal event Action? TickObserved;
    internal void WaitForTickerStop()
    {
        Thread? ticker;
        lock (_gate) ticker = _ticker;
        if (ticker is not null && ticker != Thread.CurrentThread) ticker.Join();
    }

    private void StartTicker()
    {
        if (EngineDriven || ManualTicking) return;       // the engine tick advances the scheduler (CD12)
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
                if (_robot.AnimationStreamingOpen) _scheduler.Advance(ClockMs?.Invoke() ?? origin + sw.Elapsed.TotalMilliseconds);
                TickObserved?.Invoke();
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
        _running = false;
        Thread? t;
        lock (_gate) t = _ticker;
        if (t is not null && t != Thread.CurrentThread) t.Join();
        _scheduler.Destroy();
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
        using var clipLog = Eye.UseWarningLog(_robot.Engine.Log, (level, channel, key, text) => _robot.Engine.EmitSourceLog(level, channel, key, text));
        Current = pose.Clone();
        var bmp = ProceduralFaceRenderer.Render(pose);
        _robot.Display.Show(bmp);
        return bmp;
    }

    // fidelity: M5-020
    /// <summary>
    /// Shows one of the shipped named expressions. <see cref="Expression.Neutral"/> is the shipped neutral face and
    /// is sent as a face image; a Code Lab expression is an animation trigger and plays through the shipped
    /// animation-group path. Returns the neutral bitmap for Neutral, or null when an animation was started instead.
    /// </summary>
    public FaceBitmap? ShowExpression(Expression expression)
    {
        if (expression == Expression.Neutral) return SetParameters(ProceduralFacePose.ShippedNeutral());
        PlayExpression(expression);
        return null;
    }

    // fidelity: M5-020
    /// <summary>
    /// Holds one of the shipped named expressions. Neutral is held as a face for the duration; a Code Lab expression
    /// is an animation, so it plays its mapped trigger and the duration is not used (the clip's own timeline decides
    /// how long it runs).
    /// </summary>
    public void HoldExpression(Expression expression, TimeSpan duration)
    {
        if (expression == Expression.Neutral)
        {
            var pose = ProceduralFacePose.ShippedNeutral();
            Current = pose;
            _robot.Display.Hold(ProceduralFaceRenderer.Render(pose), duration);
            return;
        }
        PlayExpression(expression);
    }

    private void PlayExpression(Expression expression)
    {
        var trigger = Expressions.TriggerFor(expression)
            ?? throw new ArgumentOutOfRangeException(nameof(expression), expression, "not a Code Lab expression");
        _robot.Animations.PlayTrigger(trigger);
    }
}

/// <summary>
/// A started animation and the token identifying it, so a caller can stop that one and no other.
/// </summary>
public sealed record AnimationTicket(Task<AnimationEndReason> Completion, long Generation);
