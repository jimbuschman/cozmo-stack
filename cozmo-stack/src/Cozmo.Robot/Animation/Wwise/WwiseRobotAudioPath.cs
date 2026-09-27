// fidelity: M6-016
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// RobotAudioAnimation's state byte (<c>+0x3C</c>, A1): 0 Preparing, 1 LoadingStream, 2 LoadingStreamFrames,
/// 3 AudioFramesReady, 4 AnimationCompleted, 5 AnimationError. The native names are the table built at
/// <c>0x00596460..0x005964E4</c> (strings at <c>0x00596694..0x005966FC</c>).
/// </summary>
public enum WwiseRobotAudioState : byte
{
    Preparing = 0,
    LoadingStream = 1,
    LoadingStreamFrames = 2,
    AudioFramesReady = 3,
    AnimationCompleted = 4,
    AnimationError = 5,
}

/// <summary>
/// The <c>state</c> byte of one queued <c>AnimationEvent</c> (A2, A6, A9): 0 when InitAnimation pushes it,
/// 1 when its lambda runs, 2 on a Complete callback (AK EndOfEvent) and 3 on an Error callback.
/// </summary>
public enum WwiseRobotAudioEventState : byte
{
    Pending = 0,
    Posted = 1,
    Complete = 2,
    Error = 3,
}

/// <summary>
/// One alternative of a RobotAudio keyframe, exactly as <c>InitAnimation</c> reads it (A2): the event,
/// the volume at <c>ref+4</c>, and the <c>ref+0xC</c> byte. A non-zero <c>ref+0xC</c> sets the animation's
/// <c>+0x3D</c> flag; <b>what <c>+0x3D</c> is used for is UNKNOWN</b> (A2), so the flag is carried and not
/// acted on.
/// </summary>
public readonly record struct WwiseRobotAudioRef(uint EventId, float Volume, bool RefPlusC);

/// <summary>
/// One RobotAudio track keyframe as the caller supplies it (A2): the trigger time (<c>kf+0xC</c>), its
/// alternatives (<c>ref+4</c>, <c>ref+0xC</c>) and their probabilities. The alternative is chosen by the
/// caller's <see cref="WwiseRobotAudioRefSelector"/> — the M5 C14 <c>GetAudioRefIndex(true)</c> interface.
/// </summary>
public sealed record WwiseRobotAudioKeyframe(
    uint TriggerTimeMs,
    IReadOnlyList<WwiseRobotAudioRef> Refs,
    IReadOnlyList<float> Probabilities);

/// <summary>
/// <c>GetAudioRefIndex(true)</c> for one keyframe (A2, the M5 C14 interface): returns a zero-based
/// alternative index, or negative when none is chosen. The path calls it once per keyframe while drawing,
/// before anything is posted, which is what makes the draw up front.
/// </summary>
public delegate int WwiseRobotAudioRefSelector(int count, IReadOnlyList<float> probabilities);

/// <summary>
/// The AK callback information type the Anki trampoline carries (A8, A9, gapE 6.2–6.3): 3 Complete (from
/// AK EndOfEvent) and 4 Error (the failed-PostEvent AudioErrorCallbackInfo).
/// </summary>
public enum WwiseAudioCallbackType : byte
{
    Complete = 3,
    Error = 4,
}

/// <summary>The callback the path passes to <c>PostEvent</c> (A7: <c>callback 0x008D8D41, cookie ctx</c>).</summary>
public interface IWwiseRobotAudioCallback
{
    /// <summary>The Wwise thread invokes this when the event completes or errors.</summary>
    void Invoke(WwiseAudioCallbackType type);
}

/// <summary>
/// <c>Dispatch::Create(queue, priority 2)</c> / <c>Dispatch::After(queue, delay, ...)</c> /
/// <c>Dispatch::Stop(queue)</c> (A3, A5, A23). The wall clock is the dispatcher's own; the path only states
/// the millisecond offsets.
/// </summary>
public interface IWwiseDispatchQueue
{
    /// <summary>A5: run <paramref name="action"/> after that many milliseconds of wall-clock time.</summary>
    void After(double delayMs, Action action);

    /// <summary>A23: <c>Dispatch::Stop</c>; posted lambdas that have not run are dropped.</summary>
    void Stop();
}

/// <summary>A3: <c>Dispatch::Create(queue, priority 2)</c>, which the path stores.</summary>
public interface IWwiseDispatchFactory
{
    IWwiseDispatchQueue Create(int priority);
}

/// <summary>
/// The RobotAudioBuffer the caller registers for a game object (M6-015 A15/A17/A18; M6-016 A3/A19/A21/A23).
/// Its stream's <c>GetCurrentTimeInMilliseconds</c> stamp is <see cref="CreatedMilliseconds"/>, its
/// front/back stream state is the two data flags, and its <c>PopNextAudioFrameData</c> returns the float
/// frames the Hijack delivered. This is a caller input: the path never creates a buffer or a stream.
/// </summary>
public interface IWwiseRobotAudioBuffer
{
    /// <summary>RobotAudioAnimation's <c>+0x1C</c> (A4, A18): while set, PrepareAnimation does not begin buffering.</summary>
    bool IsWaitingForReset { get; }

    /// <summary>A19: an audio stream exists (the Hijack prepared one).</summary>
    bool HasStream { get; }

    /// <summary>A19/A20: frame data is available now.</summary>
    bool HasData { get; }

    /// <summary>A19: the stream is complete (CloseAudioBuffer set the back stream's <c>+8</c>).</summary>
    bool IsComplete { get; }

    /// <summary>A15: the stream's creation wall clock (<c>GetCurrentTimeInMilliseconds</c>), for <c>+0x50</c>.</summary>
    double CreatedMilliseconds { get; }

    /// <summary>A19 (<c>PopAudioBufferStream</c>): drop the front stream.</summary>
    void PopAudioBufferStream();

    /// <summary>A21 (<c>PopNextAudioFrameData</c>): the next float frames of the front stream; empty is allowed (D2.9).</summary>
    ReadOnlyMemory<float> PopNextAudioFrameData();

    /// <summary>A23 (<c>ResetAudioBufferAnimationCompleted</c>): set waiting-for-reset until the next Close.</summary>
    void ResetAudioBufferAnimationCompleted();
}

/// <summary>
/// The Wwise API the path calls (A7/A11): the game-object routing setters, the per-playing-id
/// <c>event_volume</c> setter and the buffer lookup. The caller supplies it; the path invents none of it.
/// </summary>
public interface IWwiseRobotAudioHost
{
    /// <summary>A11 / gapC 2.1: <c>SetGameObjectAuxSendValues(gameObj, [{bus, gain}])</c>.</summary>
    void SetGameObjectAuxSendValues(uint gameObject, uint busId, float sendGain);

    /// <summary>A11 / gapC 2.2: <c>SetGameObjectOutputBusVolume(gameObj, value)</c>; the dry path is muted with 0.0.</summary>
    void SetGameObjectOutputBusVolume(uint gameObject, float volume);

    /// <summary>A11: <c>CozmoAudioController::RegisterRobotAudioBuffer(gameObj, pluginId)</c>.</summary>
    void RegisterRobotAudioBuffer(uint gameObject, int pluginId);

    /// <summary>A3: the client's buffer lookup for the game object; null is the error path.</summary>
    IWwiseRobotAudioBuffer? GetRobotAudioBuffer(uint gameObject);
}

/// <summary>
/// The Wwise calls the path makes on the other side of Anki's wrappers (A6/A7/A8/A10/A23). The engine is the
/// caller's; the path never implements Wwise.
/// </summary>
public interface IWwiseRobotAudioEngine
{
    /// <summary>
    /// A7/A8: <c>PostCozmoEvent</c> → <c>PostAudioEvent</c> → <c>PostEvent(event, gameObj, flags, callback, cookie)</c>.
    /// Returns the playing id, or 0 (<c>AK_INVALID_PLAYING_ID</c>) when the event is not found. On 0 the path
    /// applies the error itself (A8); the engine must not also invoke the callback for the same call.
    /// </summary>
    uint PostEvent(uint eventId, uint gameObjectId, IWwiseRobotAudioCallback callback);

    /// <summary>A6: <c>SetCozmoEventParameter(playingId, event_volume, volume)</c>, only when the playing id is non-zero.</summary>
    void SetEventVolume(uint playingId, float volume);

    /// <summary>A6/A10: <c>ProcessEvents = RenderAudio(true)</c>.</summary>
    void RenderAudio();

    /// <summary>A23: <c>StopCozmoEvent = StopAll(gameObj)</c>.</summary>
    void StopAll(uint gameObjectId);
}

/// <summary>One A11 routing entry: game object 7..10 (and 6) → Anki plug-in index → Robot_Bus_N.</summary>
public readonly record struct WwiseRobotAudioRoute(
    uint GameObject, int PluginId, uint BusId, float AuxSendGain, float DryOutputVolume);

/// <summary>
/// The engine's robot-audio path on the Anki side (M6-016, rows M6 A1..A25 plus gapC 2.8 and gapE 6.1..6.4):
/// the RobotAudioAnimation state machine and the RobotAudioClient routing around it.
///
/// <para><b>Rows implemented.</b>
/// <list type="bullet">
/// <item>A1 states; A2 the up-front alternative draw and the <c>+0x3D</c> flag.</item>
/// <item>A3 the no-events, no-buffer and OnDevice branches, <c>Dispatch::Create(queue, priority 2)</c> and
/// PrepareAnimation.</item>
/// <item>A4 PrepareAnimation/BeginBuffering gate and the Update dispatch; A5 the wall-clock
/// <c>Dispatch::After</c> offsets; A6 the lambda (state 1, PostCozmoEvent, <c>event_volume</c> per playing
/// id, ProcessEvents); A7/A8 the synchronous post and its immediate error; A9 Complete/Error callbacks.</item>
/// <item>A11 the fixed 7..10 (and 6) routing registration.</item>
/// <item>A19 UpdateLoading; A20 UpdateAudioFramesReady; A21 PopRobotAudioMessage; A22 the no-stream
/// AudioSilence behaviour; A23 abort.</item>
/// <item>gapE 6.1–6.4 the queued callback context and its drain at the end of a CozmoEngine tick.</item>
/// </list></para>
///
/// <para><b>Explicitly not built, and visible rather than defaulted.</b>
/// <list type="bullet">
/// <item><b>The OnDevice path (A3, game object 6, <c>0x00596DC8</c>).</b> It is a sibling path, not the
/// robot-audio path, and no row here describes it, so <see cref="InitAnimation"/> throws
/// <see cref="NotSupportedException"/> for game object 6 instead of guessing.</item>
/// <item><b>What <c>+0x3D</c> is for (A2); the audio-thread scheduling (A10); the mix rate (A12); the
/// Hijack and bus rows A13..A18; robot_volume's RTPC reach (A24); the second Hijack registration (A25).</b>
/// Those are other records' work; <see cref="Flag3D"/> and the caller-supplied engine and buffer carry what
/// this record reads.</item>
/// <item><b>The <c>0x9A206C(playingID)</c> callback-manager call (gapE 6.2) and the mutex internals.</b> Only
/// the queue-then-drain order A9 runs is modelled.</item>
/// </list></para>
///
/// <para><b>Seams.</b> The keyframe data, the wall clock (via <see cref="IWwiseDispatchQueue"/>), the game
/// object graph (<see cref="IWwiseRobotAudioHost"/>), the Wwise calls
/// (<see cref="IWwiseRobotAudioEngine"/>) and the Anki keyframe draw (<see cref="WwiseRobotAudioRefSelector"/>)
/// are all caller inputs. <b>Not wired:</b> this is a standalone class; it is not yet part of
/// <c>WwisePlayback</c>, <c>WwiseAudioSource</c>, <c>WwiseSongRenderer</c> or <c>AnimationScheduler</c>.</para>
/// </summary>
public sealed class WwiseRobotAudioPath
{
    /// <summary>A3: game object 6 selects the OnDevice path, not the robot path.</summary>
    public const uint OnDeviceGameObject = 6;

    /// <summary>A11: game object 7 → plug-in 1 → Robot_Bus_1 <c>0x9FA5953C</c>.</summary>
    public const uint RobotBus1 = 2678428988;

    /// <summary>A11: game object 8 → plug-in 2 → Robot_Bus_2 <c>0x9FA5953F</c>.</summary>
    public const uint RobotBus2 = 2678428991;

    /// <summary>A11: game object 9 → plug-in 3 → Robot_Bus_3 <c>0x9FA5953E</c>.</summary>
    public const uint RobotBus3 = 2678428990;

    /// <summary>A11: game object 10 → plug-in 4 → Robot_Bus_4 <c>0x9FA59539</c>.</summary>
    public const uint RobotBus4 = 2678428985;

    /// <summary>
    /// The RobotAudioClient constructor registrations (A11): game objects 7..10 → plug-in 1..4 →
    /// Robot_Bus_1..4, plus game object 6 → plug-in 0 → no bus. Each gets an aux send of 1.0 and a dry
    /// output-bus volume of 0.0. For game object 6 the bus id is 0, which <c>SetGameObjectAuxSendValues</c>
    /// ignores (gapC 2.1); the call is still made, as A11 states it for every entry.
    /// </summary>
    public static IReadOnlyList<WwiseRobotAudioRoute> Routes { get; } = new[]
    {
        new WwiseRobotAudioRoute(7, 1, RobotBus1, 1.0f, 0.0f),
        new WwiseRobotAudioRoute(8, 2, RobotBus2, 1.0f, 0.0f),
        new WwiseRobotAudioRoute(9, 3, RobotBus3, 1.0f, 0.0f),
        new WwiseRobotAudioRoute(10, 4, RobotBus4, 1.0f, 0.0f),
        new WwiseRobotAudioRoute(OnDeviceGameObject, 0, 0, 1.0f, 0.0f),
    };

    /// <summary>A6: the RTPC id <c>0xD2687048</c> "event_volume", set per playing id.</summary>
    public const uint EventVolumeRtpc = 0xD2687048;

    private readonly IWwiseRobotAudioHost _host;
    private readonly IWwiseRobotAudioEngine _engine;
    private readonly IWwiseDispatchFactory _dispatchFactory;
    private readonly WwiseRobotAudioRefSelector _selectRef;
    private readonly List<WwiseRobotAudioEvent> _events = new();
    private readonly Queue<(WwiseRobotAudioEvent Event, WwiseAudioCallbackType Type)> _callbackQueue = new();
    private readonly object _gate = new();

    private IWwiseRobotAudioBuffer? _buffer;
    private IWwiseDispatchQueue? _queue;
    private bool _alive;
    private bool _started;
    private double _offset50;
    private double _streamTime;
    private int _nextEventIndex;
    private int _postedCount;
    private int _callbacksReceived;

    /// <param name="host">The game-object graph and buffer lookup (A11, A3). Its routing is registered on construction.</param>
    /// <param name="engine">The Wwise calls (A6–A8, A10, A23).</param>
    /// <param name="dispatchFactory">The dispatcher; A3 creates one queue at priority 2.</param>
    /// <param name="selectRef">The M5 C14 draw (A2).</param>
    public WwiseRobotAudioPath(
        IWwiseRobotAudioHost host,
        IWwiseRobotAudioEngine engine,
        IWwiseDispatchFactory dispatchFactory,
        WwiseRobotAudioRefSelector selectRef)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _dispatchFactory = dispatchFactory ?? throw new ArgumentNullException(nameof(dispatchFactory));
        _selectRef = selectRef ?? throw new ArgumentNullException(nameof(selectRef));
        RegisterRobotAudioRouting();
    }

    /// <summary>A1/A21: the animation's state byte (<c>+0x3C</c>).</summary>
    public WwiseRobotAudioState State { get; private set; } = WwiseRobotAudioState.Preparing;

    /// <summary>A2: the game object the events post on (the native <c>gameObj+0x34</c>).</summary>
    public uint GameObjectId { get; private set; }

    /// <summary>A2: the drawn events, in keyframe order; the 1-based index is the native <c>idx</c>.</summary>
    public IReadOnlyList<WwiseRobotAudioEvent> Events => _events;

    /// <summary>A2: the animation's <c>+0x3D</c> flag, set when any drawn ref's <c>+0xC</c> is non-zero. Its use is UNKNOWN.</summary>
    public bool Flag3D { get; private set; }

    /// <summary>A6: the count at <c>+0x44</c>, incremented when an event's lambda runs.</summary>
    public int PostedCount { get { lock (_gate) return _postedCount; } }

    /// <summary>A9: the count at <c>+0x48</c>, incremented by every Complete or Error callback.</summary>
    public int CallbacksReceived { get { lock (_gate) return _callbacksReceived; } }

    /// <summary>
    /// The event cursor at <c>+0x40</c> (A20 advances it past state-3 events, A21 past events below the
    /// elapsed time). Exposed so the row's two advances are observable.
    /// </summary>
    public int EventCursor => _nextEventIndex;

    /// <summary>A3: the error text of the no-buffer branch, or null.</summary>
    public string? LastError { get; private set; }

    /// <summary>A19: <c>nextEvent.time ≤ streamTime − start</c> uses this animation start (a caller input).</summary>
    public double StartTimeMs { get; set; }

    /// <summary>A11: emit the fixed routing registrations to <see cref="IWwiseRobotAudioHost"/>.</summary>
    public void RegisterRobotAudioRouting()
    {
        foreach (var r in Routes)
        {
            _host.SetGameObjectAuxSendValues(r.GameObject, r.BusId, r.AuxSendGain);
            _host.SetGameObjectOutputBusVolume(r.GameObject, r.DryOutputVolume);
            _host.RegisterRobotAudioBuffer(r.GameObject, r.PluginId);
        }
    }

    /// <summary>
    /// A2/A3 InitAnimation: draw every keyframe's alternative up front, then, when there are events, find
    /// the game object's buffer, create the dispatch queue and run PrepareAnimation. The RobotAudio track is
    /// consumed to its end here, because every keyframe is read.
    /// </summary>
    /// <param name="keyframes">The RobotAudio track's keyframes.</param>
    /// <param name="gameObjectId">The game object (<c>gameObj+0x34</c>); 6 selects the OnDevice path (A3).</param>
    public void InitAnimation(IReadOnlyList<WwiseRobotAudioKeyframe> keyframes, uint gameObjectId)
    {
        ArgumentNullException.ThrowIfNull(keyframes);

        _events.Clear();
        Flag3D = false;
        LastError = null;
        _started = false;
        _offset50 = 0;
        _nextEventIndex = 0;
        _postedCount = 0;
        _callbacksReceived = 0;
        GameObjectId = gameObjectId;

        // A2: GetAudioRefIndex(true) for each keyframe, then GetAudioRef and the AnimationEvent push. The
        // index is the 1-based ordinal over pushed events: a rejected ref (index < 0) and an eventId == 0
        // ref both skip the push, and with it the counter increment at 0x005968BE.
        ushort pushed = 0;
        for (int i = 0; i < keyframes.Count; i++)
        {
            var kf = keyframes[i];
            int index = _selectRef(kf.Refs.Count, kf.Probabilities);
            if (index < 0) continue;
            var reference = kf.Refs[index];
            if (reference.EventId == 0) continue;
            pushed++;
            if (reference.RefPlusC) Flag3D = true;                  // A2: +0x3D, inside the push block (use UNKNOWN)
            _events.Add(new WwiseRobotAudioEvent(
                pushed, reference.EventId, kf.TriggerTimeMs, reference.Volume));
        }

        State = WwiseRobotAudioState.Preparing;

        // A3: no events → state 4.
        if (_events.Count == 0)
        {
            State = WwiseRobotAudioState.AnimationCompleted;
            return;
        }

        // A3: game object 6 takes the OnDevice path. That path is not this record, so it is not guessed.
        if (gameObjectId == OnDeviceGameObject)
            throw new NotSupportedException(
                "M6-016 A3: game object 6 selects the OnDevice path (0x00596DC8), which is not the " +
                "robot-audio path and is not built here.");

        // A3: buffer = client vfunc+0x20(gameObj); null → state 5 and error.
        _buffer = _host.GetRobotAudioBuffer(gameObjectId);
        if (_buffer is null)
        {
            State = WwiseRobotAudioState.AnimationError;
            LastError = $"M6-016 A3: no RobotAudioBuffer is registered for game object {gameObjectId}.";
            return;
        }

        _alive = true;
        _queue = _dispatchFactory.Create(priority: 2);              // A3: Dispatch::Create(queue, 2)
        PrepareAnimation();                                         // A3: vfunc+0x14
    }

    /// <summary>
    /// A4: while Preparing and the buffer is not waiting for reset, BeginBufferingAudioOnRobotMode runs.
    /// </summary>
    public void PrepareAnimation()
    {
        if (State == WwiseRobotAudioState.Preparing && _buffer is { IsWaitingForReset: false })
            BeginBuffering();
    }

    /// <summary>
    /// A5 BeginBuffering: state 1, then each event is posted through <c>Dispatch::After</c> at its offset
    /// from the first event's trigger time. The first is therefore at 0 ms and runs at once.
    /// </summary>
    public void BeginBuffering()
    {
        State = WwiseRobotAudioState.LoadingStream;
        if (_buffer is null || _queue is null || _events.Count == 0) return;
        uint first = _events[0].TriggerTimeMs;
        foreach (var ev in _events)
        {
            double delay = ev.TriggerTimeMs - first;
            var captured = ev;
            _queue.After(delay, () => PostEvent(captured));
        }
    }

    /// <summary>
    /// A4: the per-update dispatch — states 1/2 run UpdateLoading, state 3 runs UpdateAudioFramesReady,
    /// and the state byte is returned.
    /// </summary>
    /// <param name="streamTimeMs">The animation stream's current time; <see cref="StartTimeMs"/> is subtracted.</param>
    public WwiseRobotAudioState Update(double streamTimeMs)
    {
        _streamTime = streamTimeMs;
        switch (State)
        {
            case WwiseRobotAudioState.LoadingStream:
            case WwiseRobotAudioState.LoadingStreamFrames:
                UpdateLoading();
                break;
            case WwiseRobotAudioState.AudioFramesReady:
                UpdateAudioFramesReady();
                break;
        }
        return State;
    }

    /// <summary>
    /// A19 UpdateLoading, for states 1 and 2. The four branches are the row's: no stream, stream with no
    /// data, data not yet started, and started.
    /// </summary>
    public void UpdateLoading()
    {
        if (_buffer is null) return;

        if (!_buffer.HasStream)
        {
            // A19: IsAnimationDone = callbacks ≥ events and no stream.
            if (CallbacksReceived >= _events.Count && !_buffer.HasStream)
                State = WwiseRobotAudioState.AnimationCompleted;
            return;
        }

        if (!_buffer.HasData)
        {
            if (_buffer.IsComplete)
            {
                _buffer.PopAudioBufferStream();
                State = WwiseRobotAudioState.LoadingStream;
            }
            return;
        }

        double elapsed = _streamTime - StartTimeMs;

        if (!_started)
        {
            if (_nextEventIndex < _events.Count &&
                _events[_nextEventIndex].TriggerTimeMs <= elapsed)
            {
                _started = true;
                // A19: +0x50 = streamCreated_ms − event.time.
                _offset50 = _buffer.CreatedMilliseconds - _events[_nextEventIndex].TriggerTimeMs;
                State = WwiseRobotAudioState.AudioFramesReady;
            }
            return;
        }

        // A19: the event is due, or elapsed ≥ floor(streamCreated − +0x50); else complete → state 1.
        bool due = _nextEventIndex < _events.Count &&
                   _events[_nextEventIndex].TriggerTimeMs <= elapsed;
        bool byOffset = _nextEventIndex < _events.Count &&
                        elapsed >= Math.Floor(_buffer.CreatedMilliseconds - _offset50);
        if (due || byOffset)
            State = WwiseRobotAudioState.AudioFramesReady;
        else if (_buffer.IsComplete)
            State = WwiseRobotAudioState.LoadingStream;
    }

    /// <summary>
    /// A20 UpdateAudioFramesReady: no data stalls the animation back to state 2; otherwise the event cursor
    /// advances past events whose state is 3 (error).
    /// </summary>
    public void UpdateAudioFramesReady()
    {
        if (_buffer is null) return;
        if (!_buffer.HasData)
        {
            State = WwiseRobotAudioState.LoadingStreamFrames;
            return;
        }
        while (_nextEventIndex < _events.Count &&
               _events[_nextEventIndex].State == WwiseRobotAudioEventState.Error)
            _nextEventIndex++;
    }

    /// <summary>
    /// A21 PopRobotAudioMessage, state 3 only: pop the front stream's next float frame, encode each sample
    /// with <see cref="AnkiMuLaw"/>, zero-pad a short frame to 744, then advance the event cursor past
    /// events whose time is below the elapsed time.
    /// </summary>
    public byte[]? PopRobotAudioMessage()
    {
        if (State != WwiseRobotAudioState.AudioFramesReady || _buffer is null) return null;

        var data = _buffer.PopNextAudioFrameData();
        var frame = new byte[CozmoAudio.SamplesPerFrame];               // zero-padded tail (A21)
        int n = Math.Min(data.Length, frame.Length);
        for (int i = 0; i < n; i++) frame[i] = AnkiMuLaw.Encode(data.Span[i]);

        double elapsed = _streamTime - StartTimeMs;
        while (_nextEventIndex < _events.Count &&
               _events[_nextEventIndex].TriggerTimeMs < elapsed)
            _nextEventIndex++;

        return frame;
    }

    /// <summary>
    /// A23 abort: <c>Dispatch::Stop</c>; flush the callback queue; <c>ResetAudioBufferAnimationCompleted</c>;
    /// <c>StopCozmoEvent = StopAll(gameObj)</c> then <c>ProcessEvents</c>; state 4.
    /// </summary>
    public void Abort()
    {
        _alive = false;
        _queue?.Stop();
        DrainCallbacks();
        _buffer?.ResetAudioBufferAnimationCompleted();
        _engine.StopAll(GameObjectId);
        _engine.RenderAudio();
        State = WwiseRobotAudioState.AnimationCompleted;
    }

    /// <summary>
    /// gapE 6.2–6.4: drain the queued callbacks at the end of a <c>CozmoEngine::Update</c>, in the order the
    /// Wwise thread queued them. Each one is what <c>HandleCozmoEventCallback</c> (A9) does.
    /// </summary>
    public int DrainCallbacks()
    {
        int n = 0;
        while (true)
        {
            (WwiseRobotAudioEvent Event, WwiseAudioCallbackType Type) entry;
            lock (_gate)
            {
                if (_callbackQueue.Count == 0) break;
                entry = _callbackQueue.Dequeue();
            }
            ApplyCallback(entry.Event, entry.Type);
            n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- the A6 lambda

    private void PostEvent(WwiseRobotAudioEvent ev)
    {
        if (!_alive) return;                                            // A6: the weak pointer is dead

        lock (_gate) { ev.State = WwiseRobotAudioEventState.Posted; _postedCount++; }

        var callback = new Callback(this, ev);
        uint playingId = _engine.PostEvent(ev.EventId, GameObjectId, callback);
        if (playingId == 0)
        {
            // A8: PostEvent returned 0, so PostAudioEvent immediately delivers the type-4 error.
            ApplyCallback(ev, WwiseAudioCallbackType.Error);
        }
        else
        {
            ev.PlayingId = playingId;                                    // A8: the playing id goes to ctx+4
            _engine.SetEventVolume(playingId, ev.Volume);                // A6: SetCozmoEventParameter
        }

        _engine.RenderAudio();                                           // A6/A10: ProcessEvents
    }

    private void EnqueueCallback(WwiseRobotAudioEvent ev, WwiseAudioCallbackType type)
    {
        // A7/gapE 6.1: ctx+0x38 = 0, so the trampoline queues rather than dispatching.
        lock (_gate) _callbackQueue.Enqueue((ev, type));
    }

    private void ApplyCallback(WwiseRobotAudioEvent ev, WwiseAudioCallbackType type)
    {
        switch (type)
        {
            case WwiseAudioCallbackType.Complete:
                ev.State = WwiseRobotAudioEventState.Complete;           // A9: state 2
                break;
            case WwiseAudioCallbackType.Error:
                ev.State = WwiseRobotAudioEventState.Error;              // A9: state 3
                LastError = $"M6-016 A9: event 0x{ev.EventId:X8} reported an audio error.";
                break;
        }
        lock (_gate) _callbacksReceived++;                                // A9: +0x48++
    }

    private sealed class Callback : IWwiseRobotAudioCallback
    {
        private readonly WwiseRobotAudioPath _path;
        private readonly WwiseRobotAudioEvent _event;

        public Callback(WwiseRobotAudioPath path, WwiseRobotAudioEvent @event)
        {
            _path = path;
            _event = @event;
        }

        public void Invoke(WwiseAudioCallbackType type) => _path.EnqueueCallback(_event, type);
    }
}

/// <summary>
/// One queued <c>AnimationEvent</c> the draw produced (A2): the 1-based keyframe index, the event, the
/// keyframe trigger time and the alternative's volume, plus the state the lambda and the callbacks move.
/// </summary>
public sealed class WwiseRobotAudioEvent
{
    internal WwiseRobotAudioEvent(ushort index, uint eventId, uint triggerTimeMs, float volume)
    {
        Index = index;
        EventId = eventId;
        TriggerTimeMs = triggerTimeMs;
        Volume = volume;
    }

    /// <summary>A2: the native <c>u16 idx</c>, the 1-based ordinal over pushed events (a rejected ref or an eventId of 0 does not advance it).</summary>
    public ushort Index { get; }

    /// <summary>A2: the drawn event id.</summary>
    public uint EventId { get; }

    /// <summary>A2: the keyframe trigger time, <c>kf+0xC</c>.</summary>
    public uint TriggerTimeMs { get; }

    /// <summary>A2: the alternative's volume, <c>ref+4</c>.</summary>
    public float Volume { get; }

    /// <summary>A2/A6/A9: 0 pending, 1 posted, 2 complete, 3 error.</summary>
    public WwiseRobotAudioEventState State { get; internal set; }

    /// <summary>A8: the playing id PostEvent returned, or 0.</summary>
    public uint PlayingId { get; internal set; }
}
