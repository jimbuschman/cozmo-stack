using Cozmo.Protocol;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation;

/// <summary>What the stream sends, one call per robot message. Kept separate from the transport so the timeline can be
/// tested without a robot.</summary>
public interface IAnimationSink
{
    /// <summary>A face, as this stack's 128 x 32 picture. The stream calls <see cref="FaceImage"/>, whose default lands here.</summary>
    void Face(FaceBitmap bitmap);
    /// <summary>One audio frame's worth of samples (AudioSample 0x8E), or AudioSilence 0x8F when the argument is null.</summary>
    void Audio(byte[]? mulawFrame);
    /// <summary>
    /// How many audio frames the robot reports having played: the Robot's +0x240, which only the AnimationState
    /// handler writes (numAudioFramesPlayed, C10). The engine's audio budget is
    /// <c>max(14 - (framesStreamed - framesPlayed), 0)</c> (C9, <see cref="StreamSendBuffer"/>).
    ///
    /// Null is the test seam: a sink that models no robot. The scheduler then applies no budget and streams by the
    /// caller's clock (see <see cref="AnimationScheduler.Advance"/>). A sink that talks to a robot reports a number,
    /// 0 until the robot says otherwise, as the Robot constructor zeroes it (C13).
    /// </summary>
    int? AudioFramesPlayed => null;
    /// <summary>
    /// How many animation bytes the robot reports having played: the Robot's +0x238, written only by the
    /// AnimationState handler (numAnimBytesPlayed, C10). The byte budget is <c>min(8192 - (bytesStreamed -
    /// bytesPlayed), 30000)</c> (C9). Null counts as 0, the constructor's value (C13).
    /// </summary>
    int? AnimBytesPlayed => null;
    /// <summary>
    /// Whether the robot message the last call sent went out (MessageHandler::SendMessage's result). The drain stops at
    /// a failed send and does not count it (C14, 0x0057BFAE). A sink that sends nothing, or cannot fail, reports true.
    /// </summary>
    bool LastSendSucceeded => true;
    /// <summary>0x93 animHeadAngle {u16 duration, s8 angle} (C2), the variability already drawn.</summary>
    void Head(sbyte angleDeg, uint durationMs);
    /// <summary>0x94 animLiftHeight {u16 duration, u8 height} (C3), the variability already drawn.</summary>
    void Lift(byte heightMm, uint durationMs);
    /// <summary>0x9B StartOfAnimation {tag} (A21), buffered after a frame's audio message.</summary>
    void AnimationStarted(byte tag);
    /// <summary>0x9A EndOfAnimation (A20), sent directly.</summary>
    void AnimationEnded();
    /// <summary>0x99 BodyMotion {speed, radius}: a body keyframe's first message (C5).</summary>
    void Body(BodyKeyframe keyframe);
    /// <summary>0x99 BodyMotion {0, 0x7FFF}: a body keyframe's stop, on its first frame with counter ≥ duration (C5).</summary>
    void BodyStop();
    /// <summary>Not called by the stream: the backpack track goes out as <see cref="BackpackLights"/>.</summary>
    void Lights(LightsKeyframe keyframe);
    /// <summary>An event keyframe, for this stack's callers (the wire message is <see cref="AnimEvent"/>).</summary>
    void Event(string eventId);
    /// <summary>Called once when an animation ends, whether it finished or was cancelled.</summary>
    void Finished(string clipName, bool completed);

    /// <summary>
    /// 0x97 FaceImage with its payload: <c>CompressRLE</c> of the 64 x 128 canvas (M3 B5..B15). The default decodes it to
    /// this stack's picture and calls <see cref="Face"/>.
    /// </summary>
    void FaceImage(byte[] payload) => Face(FaceBitmapCodec.Decode(payload));
    /// <summary>0x98 BackpackLights: five LED words, Left, Front, Middle, Back, Right (C18).</summary>
    void BackpackLights(ushort[] leds) { }
    /// <summary>0x95 Event {u8 AnimEvent} (C15).</summary>
    void AnimEvent(byte animEvent) { }
    /// <summary>0x91 RecordHeading, empty (C19).</summary>
    void RecordHeading() { }
    /// <summary>
    /// 0x92 TurnToRecordedHeading (gap4 T2): 13 bytes, s16 offset_deg, s16 speed, s16 accel, s16 decel, u16 tolerance,
    /// u16 numHalfRevs, u8 useShortestDir.
    /// </summary>
    void TurnToRecordedHeading(TurnToRecordedHeadingKeyframe keyframe) { }
}

// fidelity: M3-012, M3-013, M3-014
/// <summary>
/// The engine's stream send path: <c>AnimationStreamer</c>'s send buffer and its drain
/// (<c>SendBufferedMessages</c> 0x0057BF60..0x0057C010, C14), the per-Update budgets (<c>UpdateAmountToSend</c>
/// 0x0057C6F6..0x0057C7AC, C9) and the Robot's stream counters (+0x238 bytes played, +0x23C bytes streamed,
/// +0x240 frames played, +0x244 frames streamed; C10..C13).
///
/// <list type="bullet">
/// <item><b>Budgets (C9).</b> Bytes: <c>min(8192 - (bytesStreamed - bytesPlayed), 30000)</c>, a negative value
/// warning and becoming 0. Audio: <c>max(14 - (framesStreamed - framesPlayed), 0)</c>. The played counters come
/// only from AnimationState (C10). 14 is the engine's figure; the robot's real buffer is firmware (C18).</item>
/// <item><b>Drain (C14).</b> FIFO. It stops at the first message larger than the byte budget left, or at an audio
/// message (0x8E/0x8F) when the audio budget left is 0, and reports whether the buffer emptied. Each send spends
/// its size from the byte budget and, for an audio message, one frame from the audio budget. A send that fails
/// stops the drain and stays at the front.</item>
/// <item><b>Counters (C11, C12).</b> Every send adds its <c>EngineToRobot::Size()</c> (1 for the tag plus the
/// member's packed size, which is this stack's serialised length) to bytes streamed, and 1 to frames streamed
/// for AudioSample, AudioSilence and EndOfAnimation. EndOfAnimation goes directly, not budget-gated
/// (<see cref="SendDirect(RobotMessage, Func{RobotMessage, bool})"/>).</item>
/// </list>
/// Every stream message is sent reliable and not hot (C14): this stack's single send path already sends every
/// message reliably (M1-026).
/// </summary>
public sealed class StreamSendBuffer
{
    /// <summary>The engine's audio budget: 14 unplayed frames (C9, <c>add.w r1,r1,#0xe</c> at 0x0057C79E; C18).</summary>
    public const int AudioFramesAhead = 14;
    /// <summary>The byte budget's base: 8192 unplayed bytes (C9).</summary>
    public const int BytesAhead = 8192;
    /// <summary>The byte budget's cap per Update: 30000 (C9).</summary>
    public const int MaxBytesPerUpdate = 30000;

    private sealed class Entry
    {
        public object? Owner;
        public int Size;
        public bool IsAudio;
        public Func<bool> Send = () => true;
    }

    private readonly object _gate = new();
    private readonly LinkedList<Entry> _fifo = new();
    private int _bytesStreamed, _framesStreamed, _bytesToSend, _framesToSend;

    /// <summary>The engine log for the budget warning (C9).</summary>
    public Action<string>? Log { get; set; }

    /// <summary>Robot+0x23C: bytes streamed since the Robot was built (C11..C13).</summary>
    public int BytesStreamed { get { lock (_gate) return _bytesStreamed; } }
    /// <summary>Robot+0x244: audio frames streamed (AudioSample, AudioSilence, EndOfAnimation) since the Robot was built.</summary>
    public int FramesStreamed { get { lock (_gate) return _framesStreamed; } }
    /// <summary>The byte budget left in this Update.</summary>
    public int NumBytesToSend { get { lock (_gate) return _bytesToSend; } }
    /// <summary>The audio budget left in this Update.</summary>
    public int NumAudioFramesToSend { get { lock (_gate) return _framesToSend; } }
    /// <summary>Messages waiting in the buffer.</summary>
    public int Count { get { lock (_gate) return _fifo.Count; } }
    public bool IsEmpty { get { lock (_gate) return _fifo.Count == 0; } }

    /// <summary>EngineToRobot::Size(): 1 for the tag plus the member's packed size (C11).</summary>
    public static int SizeOf(RobotMessage m) => m.ToBytes().Length;

    /// <summary>The audio messages the budget counts: tags 0x8E (AudioSample) and 0x8F (AudioSilence) (C11, C14).</summary>
    public static bool IsAudioMessage(RobotMessageId id) => ((byte)id & 0xFE) == 0x8E;

    /// <summary>UpdateAmountToSend (C9), from the played counters AnimationState last reported (C10).</summary>
    public void UpdateAmountToSend(int bytesPlayed, int framesPlayed)
    {
        string? warning = null;
        lock (_gate)
        {
            int bytes = BytesAhead - unchecked(_bytesStreamed - bytesPlayed);
            if (bytes < 0)
            {
                warning = $"warning: AnimationStreamer.UpdateAmountToSend: {-bytes} bytes more unplayed than {BytesAhead}; budget 0";
                bytes = 0;
            }
            _bytesToSend = Math.Min(bytes, MaxBytesPerUpdate);
            _framesToSend = Math.Max(AudioFramesAhead - unchecked(_framesStreamed - framesPlayed), 0);
        }
        if (warning is not null) Log?.Invoke(warning);
    }

    /// <summary>The test seam's budget (a sink that models no robot): nothing is held back.</summary>
    internal void Unlimited()
    {
        lock (_gate) { _bytesToSend = int.MaxValue; _framesToSend = int.MaxValue; }
    }

    /// <summary>BufferMessageToSend: appends a message whose size and kind are given, to be sent by <paramref name="send"/>.</summary>
    public void Buffer(object? owner, int size, bool isAudio, Func<bool> send)
    {
        lock (_gate) _fifo.AddLast(new Entry { Owner = owner, Size = size, IsAudio = isAudio, Send = send });
    }

    /// <summary>BufferMessageToSend for a message: its size and kind are its own (C11).</summary>
    public void Buffer(object? owner, RobotMessage m, Func<RobotMessage, bool> send)
        => Buffer(owner, SizeOf(m), IsAudioMessage(m.Id), () => send(m));

    /// <summary>How a drain ended (C14, A18): the buffer emptied, a budget stopped it, or a send failed.</summary>
    public enum DrainResult { Empty, BudgetStop, SendError }

    /// <summary>
    /// SendBufferedMessages (C14; 0x0057BF72..0x0057C00A): FIFO; it stops at the first message larger than the byte
    /// budget left (0x0057BF98) or at an audio message with the audio budget 0 (0x0057BFA0), which the engine reports as
    /// its 0 result like an emptied buffer, and at a send that fails, which it returns as the error. A failed message
    /// stays at the front and is not counted (only a successful send counts, 0x0057BFAE).
    /// </summary>
    public DrainResult Drain()
    {
        lock (_gate)
        {
            while (_fifo.First is { } node)
            {
                var e = node.Value;
                if (e.Size > _bytesToSend) return DrainResult.BudgetStop;
                if (e.IsAudio && _framesToSend <= 0) return DrainResult.BudgetStop;
                if (!e.Send()) return DrainResult.SendError;
                if (node.List == _fifo) _fifo.Remove(node);   // a send callback may have cleared the buffer
                _bytesToSend -= e.Size;
                if (e.IsAudio) _framesToSend--;
                CountLocked(e.Size, e.IsAudio);
            }
            return DrainResult.Empty;
        }
    }

    /// <summary><see cref="Drain"/>, true when the buffer emptied.</summary>
    public bool SendBufferedMessages() => Drain() == DrainResult.Empty;

    /// <summary>
    /// A message sent directly, not through the buffer and not budget-gated, but counted: EndOfAnimation's path
    /// (SendEndOfAnimation 0x0057C464..0x0057C496, C12). <paramref name="countsAsFrame"/> adds one to frames streamed.
    /// </summary>
    public bool SendDirect(int size, bool countsAsFrame, Func<bool> send)
    {
        lock (_gate)
        {
            if (!send()) return false;
            CountLocked(size, countsAsFrame);
            return true;
        }
    }

    /// <summary>A direct, counted send of a message: frames streamed rises for 0x8E, 0x8F and EndOfAnimation (C11, C12).</summary>
    public bool SendDirect(RobotMessage m, Func<RobotMessage, bool> send)
        => SendDirect(SizeOf(m), IsAudioMessage(m.Id) || m.Id == RobotMessageId.AnimEndOfAnimation, () => send(m));

    private void CountLocked(int size, bool frame)
    {
        _bytesStreamed = unchecked(_bytesStreamed + size);
        if (frame) _framesStreamed = unchecked(_framesStreamed + 1);
    }

    /// <summary>Messages still buffered for <paramref name="owner"/>.</summary>
    public int PendingFor(object owner)
    {
        lock (_gate)
        {
            int n = 0;
            foreach (var e in _fifo) if (ReferenceEquals(e.Owner, owner)) n++;
            return n;
        }
    }

    /// <summary>Drops every buffered message whose owner matches; returns how many.</summary>
    public int Remove(Func<object?, bool> ownerMatches)
    {
        lock (_gate)
        {
            int n = 0;
            for (var node = _fifo.First; node is not null; )
            {
                var next = node.Next;
                if (ownerMatches(node.Value.Owner)) { _fifo.Remove(node); n++; }
                node = next;
            }
            return n;
        }
    }

    // fidelity: M1-025, M1-015
    /// <summary>The Robot constructor's state (C13: the counters zeroed), for a removed robot: nothing buffered, no budget.</summary>
    internal void ResetToConstructed()
    {
        lock (_gate)
        {
            _fifo.Clear();
            _bytesStreamed = _framesStreamed = 0;
            _bytesToSend = _framesToSend = 0;
        }
    }
}

/// <summary>Why an animation stopped.</summary>
public enum AnimationEndReason { Completed, Cancelled, Replaced, Error }

/// <summary>What happened to a request to play something.</summary>
public sealed record AnimationHandle(string ClipName, AnimationTrack Tracks)
{
    /// <summary>
    /// The ownership token of this playback, fixed when it started. Reading
    /// <see cref="AnimationScheduler.Generation"/> afterwards is not the same thing: between starting an
    /// animation and asking which one is running, another caller can have replaced it, and the token
    /// that comes back then belongs to their playback rather than this one.
    /// </summary>
    public long Generation { get; init; }

    /// <summary>The tag SetStreamingAnimation returned for it (A9): 1..0xFE.</summary>
    public byte Tag { get; init; }

    private readonly TaskCompletionSource<AnimationEndReason> _done =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the animation stops, with the reason.</summary>
    public Task<AnimationEndReason> Completion => _done.Task;
    public bool IsRunning => !_done.Task.IsCompleted;

    internal void Complete(AnimationEndReason reason) => _done.TrySetResult(reason);
}

// fidelity: M5-027
/// <summary>
/// Where the streamer's idle and neutral-face animations come from (A2, A29): the trigger map (RobotManager's
/// GetAnimationForTrigger, D8, gap1 C7), the groups (GetAnimationNameFromGroup, D5..D7) and the clips.
/// </summary>
public interface IAnimationCatalog
{
    /// <summary>HasAnimationForTrigger.</summary>
    bool HasAnimationForTrigger(AnimationTrigger trigger);
    /// <summary>GetAnimationForTrigger: the group name, "" when the trigger is not mapped.</summary>
    string GetAnimationForTrigger(AnimationTrigger trigger);
    /// <summary>GetAnimationNameFromGroup(group, strict): a clip name, "" when none.</summary>
    string GetAnimationNameFromGroup(string group, bool strict);
    /// <summary>GetFirstAnimationName (A2): the group's first entry, "" when the group is missing or empty; with its size.</summary>
    (string Name, int Count) GetFirstAnimationName(string group);
    /// <summary>The container's clip by name, or null.</summary>
    AnimationClip? GetAnimation(string name);
}

// fidelity: M5-007, M5-008, M5-018, M5-022, M5-023, M5-024, M5-025, M5-026, M5-027, M5-028, M5-035, M3-013
/// <summary>
/// The engine's <c>AnimationStreamer</c> (robot+0x60), reproduced from the M5 inventory (Appendix A, A1..A37, and the
/// gap passes). One Update (<see cref="Advance"/>) runs, in the engine's order:
/// <list type="number">
/// <item><b>The keep-alive block (A31)</b>, once something has streamed (+0x88 &gt; 0): TrackLayerComponent::Update (the
/// glitch, G1), then, when nothing streams and the idle is the live animation or there is no idle and more than +0x1C0
/// (0.5 s, A32) has passed since the last stream: the neutral-face replay after an abort to nothing (+0x73), the default
/// parameters once, and FaceLayerManager::KeepFaceAlive.</item>
/// <item><b>The streaming branch (A13)</b>: a finished loop re-inits with the same tag or ends the animation; otherwise
/// UpdateStream(storeFace = 1) and +0x88 = now.</item>
/// <item><b>The no-animation path (A28..A30, A36)</b>: StreamLayers when the idle stack's top is Count and layers exist;
/// otherwise the ProceduralLive idle, the leftovers flushed and a pending EndOfAnimation sent, then the idle animation
/// (InitStream(idle, 0xFF) or UpdateStream(storeFace = 0)).</item>
/// </list>
/// UpdateStream (A14..A21) refreshes the budgets, flushes leftovers, and builds one 33 ms frame at a time while
/// ShouldProcessAnimationFrame holds (A15), in the A16 order, pulling at most one keyframe per track per frame (A19);
/// the stream time takes its 33 ms after every drain that returned without a send error (A18); with no frames left the
/// end is sent (A20). SetStreamingAnimation (A4..A9), InitStream (A10..A12) and Abort (A22..A24, with the
/// <see cref="AnimationAborted"/> broadcast that makes the robot layer send AbortAnimation 0x8D, A23) are as the engine
/// does them. The streamer never reads enabledAnimTracks and never skips a locked track (B1..B3).
///
/// Budgets and the drain are <see cref="StreamSendBuffer"/> (M3). A sink that reports no robot (null
/// <see cref="IAnimationSink.AudioFramesPlayed"/>) is the test seam: no budget applies, and each frame loop builds only
/// the frames the caller's clock owes, one per <see cref="FrameInterval"/>, at most 14 per call.
/// </summary>
public sealed class AnimationScheduler
{
    /// <summary>The engine's animation tick: 30 frames per second.</summary>
    public const int FrameRateHz = 30;
    /// <summary>The wall-clock spacing of frames on the test seam's clock: one robot audio frame, 744 samples at 22320 Hz.</summary>
    public static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / FrameRateHz);
    /// <summary>The stream-time step: +0x84 += 33 per built frame (A18).</summary>
    public const int FrameStepMs = 33;
    /// <summary>The tag idle animations (the live one included) are initialised with: InitStream(idle, 0xFF) (A29).</summary>
    public const byte LiveAnimationTag = 0xFF;
    /// <summary>AnimationTrigger::Count (0x23F): the idle stack's "no idle" entry (A1).</summary>
    public const AnimationTrigger IdleCount = (AnimationTrigger)0x23F;
    /// <summary>The idle stack's initial lock name (A1, string 0x00BEF4CF).</summary>
    public const string DefaultAnimLock = "default_anim_lock";
    /// <summary>The lock name the <see cref="StreamLive"/> seam pushes ProceduralLive under (M7-017, an M7 interface).</summary>
    public const string StreamLiveLock = "StreamLive";

    private readonly IAnimationSink _sink;
    private readonly object _gate = new();
    private readonly EngineRandom _keyframeRng;          // IKeyFrame::sRNG (C2, C3, C14; gap4 R2)
    private readonly EngineRandom _contextRng;           // the context RNG (streamer+0xA4, the layer managers, the groups; R3)
    private readonly ScanLineState _scanLines = ScanLineState.Process;   // process statics (M3 B3; fix B8)
    private readonly TrackLayerComponent _tlc;

    private sealed class StreamOwner { }                 // marks the streamer's own buffered messages
    private readonly StreamOwner _owner = new();

    // AnimationStreamer fields (A1 map)
    private StreamAnimation? _idleAnim;                  // +0x34
    private StreamAnimation? _streaming;                 // +0x38
    private AnimationClip? _streamingClip;
    private AnimationHandle? _handle;
    private AnimationClip? _neutral;                     // +0x40
    private int _idleMs;                                 // +0x44
    private readonly List<(AnimationTrigger Trigger, string Lock)> _idleStack = new();   // +0x48
    private string _lastStreamedName = "";               // +0x54
    private bool _idleInitialised;                       // +0x64
    private int _numLoops = 1;                           // +0x68
    private int _loopCounter;                            // +0x6C
    private byte _tagCounter;                            // +0x70
    private bool _startSent;                             // +0x71
    private bool _endSent;                               // +0x72
    private bool _abortedToNothing;                      // +0x73
    private long _startMs;                               // +0x80
    private long _streamMs;                              // +0x84
    private double _lastStreamSec = -float.MaxValue;     // +0x88
    private byte _tag;                                   // +0xA0 (not set by the ctors, A open question 2: 0 here)
    private StreamAnimation _live;                       // +0xA8
    private bool _liveFlag;                              // +0x194
    private AudioAnimation? _audio;                      // +0x1B0 RobotAudioClient's current animation
    private double _keepAliveTimeoutSec = 0.5;           // +0x1C0
    private string _lastInitName = "";                   // +0x1C8
    private long _lastToggleMs;                          // +0x1D4
    private bool _defaultParamsSet;

    private double _seamDueMs;                           // the test seam's clock
    private double _nowMs;
    private long _generation;

    /// <param name="random">
    /// Null: IKeyFrame::sRNG and the context RNG are mt19937s seeded from OS entropy, as the engine seeds both from
    /// /dev/urandom (gap4 R1..R3, C3). A test seam: a given instance seeds both, so a timeline can be reproduced.
    /// </param>
    public AnimationScheduler(IAnimationSink sink, Random? random = null)
    {
        _sink = sink;
        _keyframeRng = random is null ? new EngineRandom() : new EngineRandom(random);
        _contextRng = random is null ? new EngineRandom() : new EngineRandom(random);
        _tlc = new TrackLayerComponent(_contextRng, _scanLines) { Log = l => Log?.Invoke(l) };
        _live = StreamAnimation.Live();
        _idleStack.Add((IdleCount, DefaultAnimLock));
    }

    /// <summary>The engine log.</summary>
    public Action<string>? Log { get; set; }

    /// <summary>The context RNG (context+0x14, R3): the live idle, the layer managers and the group draw share it.</summary>
    public EngineRandom ContextRandom => _contextRng;

    /// <summary>The robot inputs UpdateLiveAnimation reads (L2..L6; R2..R4 of gap1).</summary>
    public LiveIdleRobotInputs LiveIdleInputs { get; } = new();

    /// <summary>
    /// The engine's send buffer, budgets and stream counters (M3-012..M3-014). The policy APIs that stream audio
    /// outside an animation (<see cref="CozmoAudio.Play"/>, M3-017) go through the same buffer and budget.
    /// </summary>
    public StreamSendBuffer Stream { get; } = new();

    /// <summary>Where the sound for an audio keyframe comes from (M6); null leaves audio keyframes silent.</summary>
    public IAnimationAudioSource? AudioSource { get; set; }

    /// <summary>The idle and neutral animations' source (A2, A29). Null: no idle animation can be picked.</summary>
    public IAnimationCatalog? Catalog { get; set; }

    /// <summary>The live-idle and keep-alive parameters (A34; set to the defaults by the first keep-alive, A31).</summary>
    public LiveIdleParams LiveIdleParameters { get; } = new();

    // fidelity: M5-031
    /// <summary>
    /// The DesiredFaceDistortion degree the glitch reads (G1, G2: an M7 interface, DesiredFaceDistortionComponent).
    /// </summary>
    public Func<float>? DesiredFaceDistortion
    {
        get => _tlc.DesiredFaceDistortion;
        set => _tlc.DesiredFaceDistortion = value;
    }

    /// <summary>
    /// The E2G AnimationAborted{tag} broadcast Abort makes when its tag is non-zero (A22). It is delivered synchronously:
    /// the robot layer's RobotEventHandler subscribes and sends AbortAnimation 0x8D, reliable, directly (A23).
    /// </summary>
    public event Action<byte>? AnimationAborted;

    /// <summary>Audio frames streamed since the current animation started (successful sends only).</summary>
    public int AudioFramesSent { get; private set; }

    /// <summary>The animation currently streaming (+0x38), or null.</summary>
    public string? Playing { get { lock (_gate) return _streamingClip?.Name; } }
    public bool IsPlaying { get { lock (_gate) return _streaming is not null; } }

    /// <summary>
    /// Whether <see cref="Advance"/> still has something to do: an animation streams, an idle animation is on the idle
    /// stack, messages wait in the buffer, layers remain to stream, or a neutral-face replay is pending. (The engine's
    /// Update runs every tick regardless; this is only for the offline tick loop.)
    /// </summary>
    public bool HasPendingWork
    {
        get
        {
            lock (_gate)
                return _streaming is not null || TopTrigger != IdleCount || !Stream.IsEmpty || _tlc.HaveLayersToSend
                       || (_abortedToNothing && _lastStreamSec > 0);
        }
    }

    /// <summary>Whether the live animation is the idle animation (+0x34 == +0xA8).</summary>
    public bool LiveStreamActive { get { lock (_gate) return ReferenceEquals(_idleAnim, _live); } }

    /// <summary>Whether a body keyframe of the live animation is still current.</summary>
    public bool LiveBodyRunning { get { lock (_gate) return _live.Body.Current is not null; } }

    /// <summary>Tracks the streaming clip touches.</summary>
    public AnimationTrack OwnedTracks { get { lock (_gate) return _streamingClip?.Tracks ?? AnimationTrack.None; } }

    /// <summary>The stream time of the last frame built for the streaming animation, relative to its start.</summary>
    public double PositionMs { get; private set; }

    /// <summary>The stream time +0x84 less the start +0x80 (A12, A18): 33 ms per built frame.</summary>
    public double StreamTimeMs { get { lock (_gate) return _streamMs - _startMs; } }

    /// <summary>Keyframes fired since the current animation started.</summary>
    public int KeyframesFired { get; private set; }

    /// <summary>The streaming animation's tag (+0xA0) while one streams; 0 otherwise.</summary>
    public byte CurrentTag { get { lock (_gate) return _streaming is null ? (byte)0 : _tag; } }

    /// <summary>The streamer's +0xA0 as it stands (not cleared by Abort, A24).</summary>
    public byte StreamTag { get { lock (_gate) return _tag; } }

    /// <summary>startSent (+0x71) and endSent (+0x72).</summary>
    public (bool StartSent, bool EndSent) StartEndFlags { get { lock (_gate) return (_startSent, _endSent); } }

    /// <summary>The idle stack, bottom first (A1, A30).</summary>
    public IReadOnlyList<(AnimationTrigger Trigger, string Lock)> IdleStack { get { lock (_gate) return _idleStack.ToArray(); } }

    /// <summary>The two _firstScanLine values (the drawer's and the FaceAnimationManager's).</summary>
    public (int Drawer, int FaceAnimation) FirstScanLines => (_scanLines.Drawer, _scanLines.FaceAnimation);

    /// <summary>+0x1C0, the keep-alive timeout in seconds (0.5 from the ctor; Hiccup, M7, writes 5.0; A32).</summary>
    public double KeepAliveTimeoutSec { get { lock (_gate) return _keepAliveTimeoutSec; } set { lock (_gate) _keepAliveTimeoutSec = value; } }

    /// <summary>ResetKeepFaceAliveLastStreamTimeout (A32, 0x0057E010): +0x1C0 = 0.5 s.</summary>
    public void ResetKeepFaceAliveLastStreamTimeout() { lock (_gate) _keepAliveTimeoutSec = 0.5; }

    /// <summary>The layer-base face (TLC+0x10).</summary>
    public ProceduralFacePose LayerBaseFace { get { lock (_gate) return _tlc.LastFace.Clone(); } }

    /// <summary>Whether the track layer component holds any layer (Q1.1).</summary>
    public bool HaveLayersToSend { get { lock (_gate) return _tlc.HaveLayersToSend; } }

    /// <summary>The face layers by name, lowest tag first (diagnostics and tests).</summary>
    public IReadOnlyList<string> FaceLayerNames { get { lock (_gate) return _tlc.Face.AllLayers.Select(l => l.Name).ToArray(); } }

    internal TrackLayerComponent Layers => _tlc;

    /// <summary>Identifies the animation currently streaming; changes whenever one starts or ends.</summary>
    public long Generation { get { lock (_gate) return _generation; } }

    /// <summary>Raised for every keyframe as it fires (when its first message goes out, or when it is consumed).</summary>
    public event Action<Keyframe>? KeyframeFired;

    /// <summary>Raised for a keyframe whose effect is not built (the MISSING items), for a caller to see.</summary>
    public event Action<string>? NotImplemented;

    private AnimationTrigger TopTrigger => _idleStack.Count == 0 ? IdleCount : _idleStack[^1].Trigger;

    // ================================================================== neutral face (A2)

    // fidelity: M5-010
    /// <summary>
    /// A2: the neutral-face animation, GetAnimationForTrigger(NeutralFace) → ag_neutral_face → GetFirstAnimationName (a
    /// group with more than one warns and uses the first; a null or empty group is an error). It is stored (+0x40), the
    /// face of its first ProceduralFace keyframe becomes ProceduralFace's reset data, and TrackLayerComponent::Init resets
    /// the layer-base face to it.
    /// </summary>
    public void LoadNeutralFace()
    {
        lock (_gate)
        {
            _neutral = null;
            var catalog = Catalog;
            string group = catalog?.GetAnimationForTrigger(AnimationTrigger.NeutralFace) ?? "";
            var (name, count) = group.Length == 0 || catalog is null ? ("", 0) : catalog.GetFirstAnimationName(group);
            if (count == 0 || name.Length == 0)
            {
                Log?.Invoke($"error: AnimationStreamer.Constructor.NeutralFaceGroupEmpty: neutral face animation group '{group}' is null or empty");
                _tlc.Init(null);
                return;
            }
            if (count > 1)
                Log?.Invoke($"warning: AnimationStreamer.Constructor.NeutralFaceGroupSize: Neutral face animation group {group} has {count} animations instead of one");
            _neutral = catalog!.GetAnimation(name);
            var face = _neutral?.Keyframes.OfType<FaceKeyframe>().FirstOrDefault()?.Pose;
            _tlc.Init(face);
        }
    }

    /// <summary>The neutral-face clip (+0x40), or null.</summary>
    public AnimationClip? NeutralFaceAnimation { get { lock (_gate) return _neutral; } }

    // ================================================================== SetStreamingAnimation (A4..A9)

    /// <summary>
    /// Starts a clip: <c>SetStreamingAnimation(anim, numLoops, interrupt = <paramref name="replaceRunning"/>)</c>. Returns
    /// null when refused (a streaming animation and no interrupt, A4). numLoops: each loop re-inits with the same tag; 0
    /// loops forever (A13).
    /// </summary>
    public AnimationHandle? Play(AnimationClip clip, double nowMs, bool replaceRunning = true, int numLoops = 1)
    {
        lock (_gate)
        {
            _nowMs = nowMs;
            byte tag = SetStreamingAnimationLocked(clip, numLoops, replaceRunning);
            return tag == 0 ? null : _handle;
        }
    }

    /// <summary>
    /// ReplayLastAnimation (A27, G2E 0xAD): SetStreamingAnimation(the last streamed name +0x54, numLoops, interrupt = 1).
    /// </summary>
    public AnimationHandle? ReplayLastAnimation(double nowMs, int numLoops = 1)
    {
        lock (_gate)
        {
            _nowMs = nowMs;
            var clip = _lastStreamedName.Length == 0 ? null : Catalog?.GetAnimation(_lastStreamedName);
            byte tag = SetStreamingAnimationLocked(clip, numLoops, interrupt: true);
            return tag == 0 ? null : _handle;
        }
    }

    /// <summary>
    /// A4..A9 SetStreamingAnimation. A4: streaming and a non-null newcomer without interrupt → NotInterrupting, 0.
    /// A5: streaming (and null or interrupt) → Aborting and Abort; nothing streaming: a null newcomer → Abort, a
    /// non-null one → Abort only with an idle animation set; then +0x38 = the newcomer. A6: a null newcomer sets +0x73
    /// when there was an old animation and returns 0. A8: a live animation with numLoops ≠ 1 is forced to 1. A9: the name
    /// to +0x54, a new tag (IncrementTagCtr, 1..0xFE), InitStream(anim, tag), +0x68 = numLoops, +0x6C = 0; the tag is
    /// returned.
    /// </summary>
    private byte SetStreamingAnimationLocked(AnimationClip? clip, int numLoops, bool interrupt)
    {
        var old = _streaming;
        if (old is not null && clip is not null && !interrupt)
        {
            Log?.Invoke($"info: AnimationStreamer.SetStreamingAnimation.NotInterrupting: Already streaming {_streamingClip?.Name}, will not interrupt with {clip.Name}");
            return 0;
        }
        if (old is not null)
        {
            Log?.Invoke($"info: AnimationStreamer.SetStreamingAnimation.Aborting: {_streamingClip?.Name}");
            AbortLocked();
        }
        else if (clip is null || _idleAnim is not null)
        {
            AbortLocked();
        }

        // the old animation's handle ends here
        if (old is not null)
            EndHandleLocked(clip is null ? AnimationEndReason.Cancelled : AnimationEndReason.Replaced);

        _streaming = clip is null ? null : StreamAnimation.Of(clip);
        _streamingClip = clip;
        if (clip is null)
        {
            if (old is not null) _abortedToNothing = true;       // A6
            return 0;
        }
        if (_streaming!.IsLive && numLoops != 1)
        {
            Log?.Invoke("error: VERIFY failed: a live animation streams one loop");
            numLoops = 1;                                          // A8
        }
        _lastStreamedName = clip.Name;
        byte tag = IncrementTagCtr();
        InitStreamLocked(_streaming, tag);
        _numLoops = numLoops;
        _loopCounter = 0;

        _generation++;
        _handle = new AnimationHandle(clip.Name, clip.Tracks) { Generation = _generation, Tag = tag };
        AudioFramesSent = 0;
        AudioStops = 0;
        KeyframesFired = 0;
        PositionMs = 0;
        return tag;
    }

    /// <summary>
    /// IncrementTagCtr (A9, 0x0057B660..0x0057B672): +0x70 incremented, again while the value it came from was above
    /// 0xFD, so the tags cycle 1..0xFE (0 and 0xFF are never produced).
    /// </summary>
    private byte IncrementTagCtr()
    {
        byte from;
        do
        {
            from = _tagCounter;
            _tagCounter = unchecked((byte)(_tagCounter + 1));
        }
        while (from > 0xFD);
        return _tagCounter;
    }

    private void EndHandleLocked(AnimationEndReason reason)
    {
        var name = _streamingClip?.Name ?? "";
        var h = _handle;
        _handle = null;
        _generation++;
        try { _sink.Finished(name, reason == AnimationEndReason.Completed); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
        h?.Complete(reason);
    }

    /// <summary>Stops the streaming animation: SetStreamingAnimation(null, 0, true). Returns false when nothing streamed.</summary>
    public bool Stop()
    {
        lock (_gate)
        {
            if (_streaming is null) return false;
            SetStreamingAnimationLocked(null, 0, interrupt: true);
            return true;
        }
    }

    /// <summary>Stops the streaming animation only if it is still the one <paramref name="generation"/> names.</summary>
    public bool StopIfCurrent(long generation)
    {
        lock (_gate)
        {
            if (_streaming is null || _handle?.Generation != generation) return false;
            SetStreamingAnimationLocked(null, 0, interrupt: true);
            return true;
        }
    }

    // ================================================================== Abort (A22..A24)

    /// <summary>
    /// A22..A24 Abort: with +0xA0 ≠ 0 the AnimationAborted{tag} broadcast (→ AbortAnimation 0x8D, A23); nothing more
    /// when neither +0x38 nor +0x34 is set; otherwise the current FaceAnimation keyframe's frame index reset to 0,
    /// startSent = endSent = 0, and the audio animation aborted and cleared. +0x38, +0xA0 and the send buffer are not
    /// cleared, and the streamer sends nothing itself.
    /// </summary>
    private void AbortLocked()
    {
        if (_tag != 0)
        {
            try { AnimationAborted?.Invoke(_tag); }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
        }
        if (_streaming is null && _idleAnim is null) return;
        Log?.Invoke($"info: AnimationStreamer.Abort: streaming {_streamingClip?.Name ?? "none"}, {Stream.Count} buffered");
        var anim = _streaming ?? _idleAnim!;
        if (anim.FaceAnim.Current is { } fa) fa.FaceAnimIndex = 0;
        _startSent = false;
        _endSent = false;
        _audio?.Abort();
        _audio = null;
    }

    // ================================================================== InitStream (A10..A12)

    /// <summary>
    /// A10..A12 InitStream(anim, tag): Animation::Init; +0xA0 = tag; +0x80 = now; both _firstScanLine values toggled when
    /// the name differs from +0x1C8 or now + GetLastKeyFrameEndTime − +0x1D4 &gt; 30000 (unsigned), then +0x1D4 = now;
    /// +0x1C8 = the name; +0x84 = +0x80; a non-empty send buffer dropped with SendBufferNotEmpty; endSent = IsEmpty,
    /// startSent = 0; the audio animation created; for a non-live animation RemoveKeepFaceAlive(99). Nothing is sent.
    /// </summary>
    private void InitStreamLocked(StreamAnimation anim, byte tag)
    {
        anim.Init();
        _tag = tag;
        _startMs = (long)_nowMs;
        if (anim.Name != _lastInitName || unchecked((uint)(_startMs + anim.LastKeyFrameEndTime - _lastToggleMs)) > 30000u)
        {
            _scanLines.Drawer = 1 - _scanLines.Drawer;
            _scanLines.FaceAnimation = 1 - _scanLines.FaceAnimation;
            _lastToggleMs = _startMs;
        }
        _lastInitName = anim.Name;
        _streamMs = _startMs;
        if (!Stream.IsEmpty)
        {
            Log?.Invoke("warning: Animation.Init.SendBufferNotEmpty");
            Stream.Remove(o => o is StreamOwner);      // ClearSendBuffer: dropped, not sent (A12)
        }
        _endSent = anim.IsEmpty;
        _startSent = false;
        _audio = anim.RobotAudio.IsEmpty ? null : new AudioAnimation(this, anim.RobotAudio);
        if (!anim.IsLive) _tlc.RemoveKeepFaceAlive(99);
        _seamDueMs = _nowMs;
    }

    // ================================================================== idle stack (A30)

    // fidelity: M5-027
    /// <summary>PushIdleAnimation(T, lock) (A30): pushed; T = Count clears +0x34 and +0x64.</summary>
    public void PushIdleAnimation(AnimationTrigger trigger, string lockName)
    {
        lock (_gate)
        {
            _idleStack.Add((trigger, lockName));
            if (trigger == IdleCount) { _idleAnim = null; _idleInitialised = false; }
        }
    }

    /// <summary>
    /// RemoveIdleAnimation(lock) (A30): refuses to pop the last entry (1); a lock not found warns and returns 1; removing
    /// from the middle warns. When the new top is Count while an idle plays and nothing streams:
    /// SetStreamingAnimation(neutral, 1, true, false) and +0x34/+0x64 cleared. Returns 0 on success.
    /// </summary>
    public int RemoveIdleAnimation(string lockName, double nowMs)
    {
        lock (_gate)
        {
            _nowMs = nowMs;
            if (_idleStack.Count <= 1) return 1;
            int i = _idleStack.FindLastIndex(e => e.Lock == lockName);
            if (i < 0)
            {
                Log?.Invoke($"warning: AnimationStreamer.RemoveIdleAnimation.LockNotFound: {lockName}");
                return 1;
            }
            if (i != _idleStack.Count - 1)
                Log?.Invoke($"warning: AnimationStreamer.RemoveIdleAnimation.RemovingFromMiddle: {lockName}");
            _idleStack.RemoveAt(i);
            if (TopTrigger == IdleCount && _idleAnim is not null && _streaming is null)
            {
                SetStreamingAnimationLocked(_neutral, 1, interrupt: true);
                _idleAnim = null;
                _idleInitialised = false;
            }
            return 0;
        }
    }

    // ================================================================== the live animation seam (M7-017)

    /// <summary>
    /// Appends a keyframe to the live animation (+0xA8), as UpdateLiveAnimation's AddKeyFrameToBack does, for the M7
    /// idle behaviour (the M7-017 seam; the engine's own generator, A35, is not built). The live animation streams only
    /// as the idle, so the seam puts ProceduralLive on the idle stack when it is not on top. Refused (false) while an
    /// animation streams: the engine reaches the live animation only in the no-animation path. Nothing is sent here; the
    /// keyframe goes out in the Updates that follow (A29).
    /// </summary>
    public bool StreamLive(Keyframe k, double nowMs)
    {
        lock (_gate)
        {
            _ = nowMs;
            if (_streaming is not null) return false;
            if (TopTrigger != AnimationTrigger.ProceduralLive) _idleStack.Add((AnimationTrigger.ProceduralLive, StreamLiveLock));
            return _live.AddLive(k);
        }
    }

    // ================================================================== Update

    /// <summary>One engine Update of the streamer (see the class summary).</summary>
    public void Advance(double nowMs)
    {
        lock (_gate)
        {
            _nowMs = nowMs;
            double nowSec = nowMs / 1000.0;

            // A31: the keep-alive block
            if (_lastStreamSec > 0)
            {
                _tlc.Update();
                if (_streaming is null
                    && (ReferenceEquals(_idleAnim, _live) || (_idleAnim is null && nowSec - _lastStreamSec > _keepAliveTimeoutSec)))
                {
                    if (_abortedToNothing)
                    {
                        SetStreamingAnimationLocked(_neutral, 1, interrupt: true);
                        _abortedToNothing = false;
                    }
                    if (!_defaultParamsSet)
                    {
                        LiveIdleParameters.SetDefaultParams();
                        _defaultParamsSet = true;
                    }
                    _tlc.KeepFaceAlive(LiveIdleParameters);
                }
            }

            // A13: the streaming branch
            if (_streaming is not null)
            {
                _idleMs = 0;
                if (_endSent && !_streaming.HasFramesLeft && Stream.IsEmpty)
                {
                    _loopCounter++;
                    if (unchecked((uint)(_numLoops - 1)) >= (uint)_loopCounter)
                        InitStreamLocked(_streaming, _tagCounter);
                    else
                    {
                        _streaming = null;
                        EndHandleLocked(AnimationEndReason.Completed);
                        _streamingClip = null;
                    }
                }
                else
                {
                    UpdateStreamLocked(_streaming, storeFace: true);
                    _idleInitialised = false;
                    _lastStreamSec = nowSec;
                }
                if (_streaming is not null) return;
            }

            NoAnimationPathLocked();
        }
    }

    /// <summary>
    /// A28, A29, A36, gap4 L8: the no-animation path. With the idle stack empty or its top Count: StreamLayers when layers
    /// exist, otherwise a non-empty buffer is flushed and, started, emptied and not ended, EndOfAnimation (Q1.9); nothing
    /// more. Any other top goes straight to the idle (0x0057D03A..0x0057D04C branch to 0x0057D064; the flush at 0x0057D122
    /// is reached only from 0x0057D056), which neither flushes nor sends an End: an InitStream drops the leftovers (A12).
    /// </summary>
    private void NoAnimationPathLocked()
    {
        var top = TopTrigger;
        if (top == IdleCount)
        {
            if (_tlc.HaveLayersToSend)
            {
                StreamLayersLocked();                     // Q1.7: +0x88 is not updated
                return;
            }
            if (!Stream.IsEmpty)
            {
                Log?.Invoke("warning: AnimationStreamer.Update.SendBufferNotEmpty");
                RefreshBudget();
                if (Stream.Drain() == StreamSendBuffer.DrainResult.SendError) return;
                if (_startSent && Stream.IsEmpty && !_endSent) SendEndOfAnimationLocked();
            }
            return;
        }

        var old = _idleAnim;
        if (top == AnimationTrigger.ProceduralLive)
        {
            // L8: +0x194 = 1, +0x34 = the live animation, UpdateLiveAnimation first; then the tail below
            _liveFlag = true;
            _idleAnim = _live;
            if (UpdateLiveAnimationLocked() != 0) Log?.Invoke("error: AnimationStreamer.Update.LiveUpdateFailed");
        }
        else
        {
            // A29: a new idle is picked when there is none, the current one ended, or +0x64 = 0
            bool ended = _idleAnim is not null && _endSent && !_idleAnim.HasFramesLeft && Stream.IsEmpty;
            if (_idleAnim is null || ended || !_idleInitialised)
            {
                if (Catalog is { } catalog && catalog.HasAnimationForTrigger(top))
                {
                    var picked = PickIdleLocked(catalog, top);
                    if (picked is null)
                    {
                        _idleAnim = null;                // Q1: 0x0057D29A, 0x0057D356
                        return;
                    }
                    _idleAnim = picked;
                }
                // Q1: a trigger with no animation goes on to the tail with no error (0x0057D218)
            }
        }

        if (_idleAnim is not null)
        {
            // C4 (0x0057D3F0..0x0057D412): InitStream(idle, 0xFF) when the previous idle is not this one, or +0x64 == 0
            // (every streaming Update clears it, A13), or the idle has ended; otherwise UpdateStream
            bool init = !ReferenceEquals(old, _idleAnim) || !_idleInitialised
                        || (_endSent && !_idleAnim.HasFramesLeft && Stream.IsEmpty);
            if (init)
            {
                InitStreamLocked(_idleAnim, LiveAnimationTag);
                _idleInitialised = true;
            }
            else
            {
                UpdateStreamLocked(_idleAnim, storeFace: false);
                _lastStreamSec = _nowMs / 1000.0;         // B3: +0x88 = now after the idle's UpdateStream (0x0057D428..0x0057D43E)
            }
        }
        _idleMs += 60;
    }

    /// <summary>
    /// A29 picking: GetAnimationForTrigger → GetAnimationNameFromGroup(strict = false) → GetAnimation. An empty name or a
    /// null animation is an error; the stack is not popped.
    /// </summary>
    private StreamAnimation? PickIdleLocked(IAnimationCatalog catalog, AnimationTrigger trigger)
    {
        string group = catalog.GetAnimationForTrigger(trigger);
        string name = catalog.GetAnimationNameFromGroup(group, strict: false);
        var clip = name.Length == 0 ? null : catalog.GetAnimation(name);
        if (clip is null)
        {
            Log?.Invoke($"error: AnimationStreamer.Update.IdleAnimNotFound: idle group '{group}' gave '{name}'; the idle is cleared, the stack is not popped");
            return null;
        }
        return StreamAnimation.Of(clip);
    }

    // live-idle timers (gap4 L1): +0x198/+0x19C/+0x1A0 durations, +0x1A4/+0x1A8/+0x1AC spacings, +0x1C4 the eye-shift tag
    /// <summary>C4: the engine's rad-to-deg float 0x42652EE1 (180/π), [0x0057DB2C].</summary>
    internal static readonly float RadToDeg = BitConverter.Int32BitsToSingle(0x42652EE1);

    private int _bodyDurMs, _liftDurMs, _headDurMs, _bodySpacingMs, _liftSpacingMs, _headSpacingMs;
    private byte _liveEyeShiftTag;

    // fidelity: M5-030
    /// <summary>
    /// UpdateLiveAnimation (gap4 L1..L7). Gates, with no decrement when one fails: +0x194; +0x44 ≥ GetParam&lt;int&gt;(2)
    /// (unsigned); DockingComponent+4 (picking or placing) clear. Then per track, body → lift → head: while the
    /// MovementComponent flag (moving / lift not in position / head not in position) is set, the track is locked, the lift
    /// is carrying, or duration + spacing &gt; 0, the duration loses 60; otherwise a keyframe is generated with the draws in
    /// the L4..L6 order on the context RNG, appended to the live animation with trigger 0 and no order check
    /// (AddKeyFrameToBack, L7), and the spacing drawn on success. Returns 1 when an append fails.
    /// </summary>
    private int UpdateLiveAnimationLocked()
    {
        if (!_liveFlag) return 0;
        // The M7-017 seam: with ProceduralLive pushed by StreamLive, the M7 idle behaviour appends the live keyframes itself
        // (an M7 interface until M7 pushes ProceduralLive), so the streamer's own generator does not run on top of it.
        if (_idleStack.Count > 0 && _idleStack[^1].Lock == StreamLiveLock) return 0;
        var p = LiveIdleParameters;
        var inputs = LiveIdleInputs;
        int Pi(LiveIdleParam i) => (int)p[i];                             // GetParam<int>: vcvt.s32.f32
        byte Pu8(LiveIdleParam i) => unchecked((byte)(uint)Math.Max(0f, p[i]));   // GetParam<u8>: vcvt.u32.f32
        if ((uint)_idleMs < (uint)Pi(LiveIdleParam.TimeBeforeWiggleMotions_ms)) return 0;
        if (inputs.PickingOrPlacing()) return 0;
        byte locked = inputs.LockedTracks();
        var rng = _contextRng;

        // body (L3, L4)
        if (inputs.Moving() || (locked & 4) != 0 || _bodyDurMs + _bodySpacingMs > 0) _bodyDurMs -= 60;
        else
        {
            _bodyDurMs = rng.RandIntInRange(Pi(LiveIdleParam.BodyMovementDurationMin_ms), Pi(LiveIdleParam.BodyMovementDurationMax_ms));
            int speedMax = Pi(LiveIdleParam.BodyMovementSpeedMinMax_mmps);
            short speed = unchecked((short)rng.RandIntInRange(-speedMax, speedMax));
            double r = rng.RandDblInRange(0.0, 1.0);
            string radius;
            if (r > (double)p[LiveIdleParam.BodyMovementStraightFraction])
            {
                int x = rng.RandIntInRange(0, 21);
                int y = rng.RandIntInRange(-10, 10);
                float xs = (float)(int)((speed < 0 ? -1 : 1) * x);
                _tlc.Face.AddOrUpdateEyeShift(ref _liveEyeShiftTag, "LiveIdleTurn", xs, y, 33, 64f, 32f, 1.1f, 0.85f, 0.1f);
                radius = "TURN_IN_PLACE";
            }
            else
            {
                if (_liveEyeShiftTag != 0) _tlc.Face.RemoveEyeShift(ref _liveEyeShiftTag, 0);
                radius = "STRAIGHT";
            }
            if (!_live.AddLive(new BodyKeyframe(0, unchecked((uint)_bodyDurMs), radius, speed)))
            {
                Log?.Invoke("error: AnimationStreamer.UpdateLiveAnimation.AddBodyMotionKeyFrameFailed");
                return 1;
            }
            _bodySpacingMs = rng.RandIntInRange(Pi(LiveIdleParam.BodyMovementSpacingMin_ms), Pi(LiveIdleParam.BodyMovementSpacingMax_ms));
        }

        // lift (L3, L5)
        if (inputs.LiftNotInPosition() || (locked & 2) != 0 || inputs.Carrying() || _liftDurMs + _liftSpacingMs > 0) _liftDurMs -= 60;
        else
        {
            _liftDurMs = rng.RandIntInRange(Pi(LiveIdleParam.LiftMovementDurationMin_ms), Pi(LiveIdleParam.LiftMovementDurationMax_ms));
            if (!_live.AddLive(new LiftKeyframe(0, unchecked((uint)_liftDurMs), Pu8(LiveIdleParam.LiftHeightMean_mm), Pu8(LiveIdleParam.LiftHeightVariability_mm))))
            {
                Log?.Invoke("error: AnimationStreamer.UpdateLiveAnimation.AddLiftHeightKeyFrameFailed");
                return 1;
            }
            _liftSpacingMs = rng.RandIntInRange(Pi(LiveIdleParam.LiftMovementSpacingMin_ms), Pi(LiveIdleParam.LiftMovementSpacingMax_ms));
        }

        // head (L3, L6): the angle truncated, (s8)vcvt.s32.f32(robot+0x2FC · 0x42652EE1) (C4: [0x0057DB2C], 0x0057D838)
        if (inputs.HeadNotInPosition() || (locked & 1) != 0 || _headDurMs + _headSpacingMs > 0) _headDurMs -= 60;
        else
        {
            _headDurMs = rng.RandIntInRange(Pi(LiveIdleParam.HeadMovementDurationMin_ms), Pi(LiveIdleParam.HeadMovementDurationMax_ms));
            sbyte angle = unchecked((sbyte)(int)(inputs.HeadAngleRad() * RadToDeg));
            if (!_live.AddLive(new HeadKeyframe(0, unchecked((uint)_headDurMs), angle, Pu8(LiveIdleParam.HeadAngleVariability_deg))))
            {
                Log?.Invoke("error: AnimationStreamer.UpdateLiveAnimation.AddHeadAngleKeyFrameFailed");
                return 1;
            }
            _headSpacingMs = rng.RandIntInRange(Pi(LiveIdleParam.HeadMovementSpacingMin_ms), Pi(LiveIdleParam.HeadMovementSpacingMax_ms));
        }
        return 0;
    }

    // ================================================================== UpdateStream (A14..A21)

    private void RefreshBudget()
    {
        int? framesPlayed = _sink.AudioFramesPlayed;
        if (framesPlayed is not null) Stream.UpdateAmountToSend(_sink.AnimBytesPlayed ?? 0, framesPlayed.Value);
        else Stream.Unlimited();
    }

    private bool Paced => _sink.AudioFramesPlayed is not null;

    /// <summary>The test seam's frame allowance for this call: 1 + floor(late / interval), at most 14.</summary>
    private int SeamFramesOwed()
    {
        if (Paced) return int.MaxValue;
        double interval = FrameInterval.TotalMilliseconds;
        double late = _nowMs - _seamDueMs;
        if (late < 0) { _seamDueMs = _nowMs; late = 0; }
        double maxDebt = StreamSendBuffer.AudioFramesAhead * interval;
        if (late > maxDebt) { _seamDueMs = _nowMs - maxDebt; late = maxDebt; }
        return Math.Min(1 + (int)Math.Floor(late / interval), StreamSendBuffer.AudioFramesAhead);
    }

    /// <summary>
    /// A15 ShouldProcessAnimationFrame: false with a non-empty buffer; with no audio animation, HasFramesLeft; with one,
    /// its Update(start, streamTime) and then its readiness (C16: ready, and a completed one is cleared).
    /// </summary>
    private bool ShouldProcessAnimationFrame(StreamAnimation anim)
    {
        if (!Stream.IsEmpty) return false;
        if (_audio is null) return anim.HasFramesLeft;
        _audio.Update(_startMs, _streamMs);
        if (_audio.IsComplete)
        {
            _audio = null;
            return true;
        }
        return _audio.IsReady;
    }

    /// <summary>
    /// A14..A20 UpdateStream(anim, storeFace): an uninitialised animation is an error; the budgets refreshed and the
    /// leftovers flushed (a send error returns at once); frames built while ShouldProcessAnimationFrame holds, each drained,
    /// the stream time +33 after every drain that returned without a send error (A18); then, not processing and no
    /// frames left: with the audio complete, the buffer empty and the end not sent, EndOfAnimation directly when Start was
    /// sent, otherwise AudioSilence and StartOfAnimation buffered and drained (the End follows on a later Update).
    /// </summary>
    private void UpdateStreamLocked(StreamAnimation anim, bool storeFace)
    {
        if (!anim.Initialised)
        {
            Log?.Invoke("error: AnimationStreamer.UpdateStream: Animation must be initialized");
            return;
        }
        RefreshBudget();
        if (Stream.Drain() == StreamSendBuffer.DrainResult.SendError) return;       // A14

        int owed = SeamFramesOwed();
        int built = 0;
        bool processing = true;
        while (built < owed && (processing = ShouldProcessAnimationFrame(anim)))
        {
            BuildFrameLocked(anim, storeFace);
            built++;
            if (Stream.Drain() == StreamSendBuffer.DrainResult.SendError) return;   // A18: no step on a send error
            _streamMs += FrameStepMs;
            _seamDueMs += FrameInterval.TotalMilliseconds;
        }
        // The seam's allowance ran out while frames remain: the engine's next check would have built another.
        if (processing && Stream.IsEmpty && (_audio is not null || anim.HasFramesLeft)) return;

        // A20
        if (anim.HasFramesLeft) return;
        bool audioComplete = _audio is null || _audio.IsComplete;
        if (audioComplete && Stream.IsEmpty && !_endSent)
        {
            if (_startSent)
            {
                _audio = null;                           // RobotAudioClient::ClearCurrentAnimation
                SendEndOfAnimationLocked();
            }
            else
            {
                BufferAudioLocked(null);
                SendStartOfAnimationLocked();
                Stream.Drain();
            }
        }
    }

    /// <summary>A21 SendStartOfAnimation: StartOfAnimation{+0xA0} buffered; startSent = 1, endSent = 0.</summary>
    private void SendStartOfAnimationLocked()
    {
        byte tag = _tag;
        Buffer(StreamSizes.StartOfAnimation, false, () => _sink.AnimationStarted(tag));
        _startSent = true;
        _endSent = false;
    }

    /// <summary>
    /// SendEndOfAnimation (A20, 0x0057C448..0x0057C496; C12): directly, not budget-gated, counted as a frame plus its
    /// bytes; on success startSent = 0 and endSent = 1.
    /// </summary>
    private void SendEndOfAnimationLocked()
    {
        bool sent = Stream.SendDirect(StreamSizes.EndOfAnimation, true, () => SafeSend(_sink.AnimationEnded));
        if (!sent) return;
        _startSent = false;
        _endSent = true;
    }

    // ================================================================== one frame (A16, A17, A19)

    // fidelity: M3-015, M5-004, M5-005, M5-006, M5-013, M5-016, M5-025, M5-033
    /// <summary>
    /// One frame in the A16 order (0x0057C94E..0x0057CA7A): (1) the audio animation's frame; (2) ApplyLayersToAnim;
    /// (3) AudioSample or AudioSilence; (4) StartOfAnimation once; (5) Head, (6) Lift, (7) Event, (8) FaceAnimation;
    /// (9) the procedural face, only if (8) buffered nothing, the FaceAnimation track is at its end (A17) and the layered
    /// face flag is set; (10) BackpackLights (layered); (11) Body, (12) RecordHeading, (13) TurnToRecordedHeading.
    /// </summary>
    private void BuildFrameLocked(StreamAnimation anim, bool storeFace)
    {
        int start = (int)_startMs, t = (int)_streamMs;
        if (ReferenceEquals(anim, _streaming)) PositionMs = t - start;

        byte[]? sample = _audio?.PopFrame();                                        // (1)
        var consumed = new List<StreamKeyframe>();
        var consumedFaces = new List<FaceFrame>();
        var layered = _tlc.ApplyLayersToAnim(anim, start, t, sample, storeFace,
                                             consumed.Add, consumedFaces.Add);      // (2)
        BufferAudioLocked(layered.Audio ? layered.AudioSample : null);               // (3)
        if (!_startSent) SendStartOfAnimationLocked();                               // (4)

        Pull(anim.Head, start, t, k =>                                              // (5)
        {
            var h = (HeadKeyframe)k.Source;
            var deg = unchecked((sbyte)WithVariability(h.AngleDeg, h.VariabilityDeg));
            Buffer(StreamSizes.HeadAngle, false, () => _sink.Head(deg, h.DurationTimeMs), k);
            return true;
        });
        Pull(anim.Lift, start, t, k =>                                              // (6)
        {
            var l = (LiftKeyframe)k.Source;
            var mm = unchecked((byte)WithVariability(l.HeightMm, l.VariabilityMm));
            Buffer(StreamSizes.LiftHeight, false, () => _sink.Lift(mm, l.DurationTimeMs), k);
            return true;
        });
        Pull(anim.Event, start, t, k =>                                             // (7)
        {
            var e = (EventKeyframe)k.Source;
            if (e.Parsed is { } ev)
                Buffer(StreamSizes.Event, false, () => { _sink.AnimEvent((byte)ev); _sink.Event(e.EventId); }, k);
            else
                BufferLocal(() => _sink.Event(e.EventId), k);    // a name the engine rejects at load: no wire message
            return true;
        });
        bool faceAnimBuffered = false;                                              // (8)
        Pull(anim.FaceAnim, start, t, k =>
        {
            faceAnimBuffered = FaceAnimationFrameLocked(k);
            var n = FrameCount((FaceAnimationKeyframe)k.Source);
            if (k.FaceAnimIndex >= n) { k.FaceAnimIndex = 0; return true; }       // IsDone; the index back to 0
            return false;
        });

        foreach (var k in consumed) BufferLocal(null, k);                           // backpack keyframes, for KeyframeFired
        foreach (var f in consumedFaces) BufferFaceFired(f);

        if (!faceAnimBuffered && anim.FaceAnim.AtEnd && layered.Face && layered.FaceOut is { } face)   // (9)
            BufferFaceToSendLocked(face);
        if (layered.Backpack && layered.BackpackLeds is { } leds)                   // (10)
            Buffer(StreamSizes.BackpackLights, false, () => _sink.BackpackLights(leds));

        Pull(anim.Body, start, t, k =>                                              // (11)
        {
            var b = (BodyKeyframe)k.Source;
            if (k.Counter == 0)
                Buffer(StreamSizes.Body(b), false, () => _sink.Body(b), k);
            else if (k.Counter >= b.DurationTimeMs && b.EncodedRadius is not null)
                Buffer(StreamSizes.BodyStop, false, _sink.BodyStop);    // a radius the load would have rejected sends nothing
            return k.IsDoneHelper(b.DurationTimeMs);
        });
        Pull(anim.RecHeading, start, t, k =>                                        // (12)
        {
            Buffer(StreamSizes.RecordHeading, false, _sink.RecordHeading, k);
            return true;
        });
        Pull(anim.TurnTo, start, t, k =>                                            // (13)
        {
            var tt = (TurnToRecordedHeadingKeyframe)k.Source;
            if (k.Counter == 0)                                                     // gap4 T2: only at counter 0
                Buffer(StreamSizes.TurnToRecordedHeading, false, () => _sink.TurnToRecordedHeading(tt), k);
            return k.IsDoneHelper(tt.DurationTimeMs);
        });
    }

    /// <summary>
    /// A19: only the current keyframe of a track is considered; when start + trigger ≤ streamTime its message is built
    /// (<paramref name="streamMessage"/>, which returns IsDone) and the track moves on when it is done. At most one
    /// keyframe per track per frame.
    /// </summary>
    private static void Pull(StreamTrack<StreamKeyframe> track, int start, int t, Func<StreamKeyframe, bool> streamMessage)
    {
        var k = track.Current;
        if (k is null || start + (long)k.Trigger > t) return;
        if (streamMessage(k)) track.MoveToNext();
    }

    private IReadOnlyList<FaceAnimationFrame>? FramesOf(FaceAnimationKeyframe k)
    {
        var variants = FaceAnimationVariants?.Invoke(k.AnimName);
        if (variants is not null) return variants;
        var pictures = FaceAnimations?.Invoke(k.AnimName);
        return pictures?.Select(FromPicture).ToArray();
    }

    private int FrameCount(FaceAnimationKeyframe k) => FramesOf(k)?.Count ?? 0;

    /// <summary>
    /// C12 FaceAnimationKeyFrame::GetStreamMessage: frame = GetFrame(name, index) with the FaceAnimationManager's
    /// _firstScanLine; an empty frame is skipped (index++, no message); a missing one logs an error. Returns whether a
    /// message was buffered.
    /// </summary>
    private bool FaceAnimationFrameLocked(StreamKeyframe k)
    {
        var fa = (FaceAnimationKeyframe)k.Source;
        var frames = FramesOf(fa);
        if (frames is null || k.FaceAnimIndex >= frames.Count)
        {
            Log?.Invoke($"error: FaceAnimationKeyFrame.GetStreamMessage: no frame {k.FaceAnimIndex} of '{fa.AnimName}'");
            return false;
        }
        var payload = frames[k.FaceAnimIndex].ForScanLine(_scanLines.FaceAnimation);
        k.FaceAnimIndex++;
        if (payload.Length == 0) return false;
        var shown = payload;
        Buffer(StreamSizes.Face(shown), false, () => _sink.FaceImage(shown), k);
        return true;
    }

    /// <summary>A test picture as a stored frame: bitmap row y on canvas rows 2y and 2y + 1.</summary>
    private static FaceAnimationFrame FromPicture(FaceBitmap b)
    {
        var canvas = new byte[FaceBitmapCodec.CanvasRows * FaceBitmapCodec.CanvasColumns];
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = 0; x < FaceBitmap.Width; x++)
                if (b[x, y] != 0)
                {
                    canvas[2 * y * FaceBitmapCodec.CanvasColumns + x] = 255;
                    canvas[(2 * y + 1) * FaceBitmapCodec.CanvasColumns + x] = 255;
                }
        return FaceAnimationFrame.FromCanvas(canvas);
    }

    /// <summary>
    /// BufferFaceToSend (M3 B5, 0x0057C1FC..0x0057C2DA): DrawFace with the drawer's _firstScanLine, then CompressRLE,
    /// buffered as FaceImage every time, with no de-duplication (Q1.8).
    /// </summary>
    private void BufferFaceToSendLocked(ProceduralFacePose face)
    {
        var canvas = ProceduralFaceRenderer.DrawFace(face, _scanLines.Drawer);
        var payload = FaceBitmapCodec.EncodeCanvas(canvas, FaceBitmapCodec.CanvasRows, FaceBitmapCodec.CanvasColumns);
        if (payload is null)
        {
            Log?.Invoke("error: AnimationStreamer.BufferFaceToSend: Failed to get RLE frame from procedural face");
            return;
        }
        Buffer(StreamSizes.Face(payload), false, () => _sink.FaceImage(payload));
    }

    private void BufferFaceFired(FaceFrame f)
    {
        if (f.Fired || f.Source is null) return;
        f.Fired = true;
        var src = f.Source;
        Stream.Buffer(_owner, 0, false, () => { KeyframesFired++; KeyframeFired?.Invoke(src); return true; });
    }

    /// <summary>(3): one audio message, AudioSample with the frame's 744 bytes or AudioSilence.</summary>
    private void BufferAudioLocked(byte[]? sample)
    {
        Buffer(sample is null ? StreamSizes.AudioSilence : StreamSizes.AudioSample, true,
               () => _sink.Audio(sample), onSent: () => AudioFramesSent++);
    }

    // ================================================================== StreamLayers (A36, Q1.8, Q1.9)

    /// <summary>
    /// A36 / Q1.8 StreamLayers: the budgets and a flush first (a send error returns); with an empty buffer, while layers
    /// remain: ApplyLayersToAnim(null), AudioSample or AudioSilence, a new tag and StartOfAnimation when not started,
    /// BackpackLights, the face (every frame, no de-duplication), a drain, +0x84 += 33, and again only while the buffer
    /// empties. Then the end check: EndOfAnimation when started, no layers, the buffer empty and not ended (Q1.9).
    /// </summary>
    private void StreamLayersLocked()
    {
        RefreshBudget();
        if (Stream.Drain() == StreamSendBuffer.DrainResult.SendError) return;
        if (Stream.IsEmpty)
        {
            if (!_startSent) _seamDueMs = _nowMs;         // the seam's clock starts with a new layer stream
            int owed = SeamFramesOwed();
            int built = 0;
            while (_tlc.HaveLayersToSend && built < owed)
            {
                var frame = _tlc.ApplyLayersToAnim(null, (int)_startMs, (int)_streamMs, null, storeFace: false);
                BufferAudioLocked(frame.Audio ? frame.AudioSample : null);
                if (!_startSent)
                {
                    _tag = IncrementTagCtr();
                    SendStartOfAnimationLocked();
                }
                if (frame.Backpack && frame.BackpackLeds is { } leds)
                    Buffer(StreamSizes.BackpackLights, false, () => _sink.BackpackLights(leds));
                if (frame.Face && frame.FaceOut is { } face) BufferFaceToSendLocked(face);
                built++;
                if (Stream.Drain() == StreamSendBuffer.DrainResult.SendError) return;
                _streamMs += FrameStepMs;
                _seamDueMs += FrameInterval.TotalMilliseconds;
                if (!Stream.IsEmpty) break;
            }
        }
        if (_startSent && !_tlc.HaveLayersToSend && Stream.IsEmpty && !_endSent) SendEndOfAnimationLocked();
    }

    // ================================================================== buffering

    /// <summary>
    /// Buffers one sink call that sends a robot message; the drain sees whether it went out. A send that throws (the
    /// robot gone) is a failed send: it stays at the front and is not counted (C14), as MessageHandler's result would be.
    /// A keyframe's KeyframeFired goes when its first message is sent.
    /// </summary>
    private void Buffer(int size, bool isAudio, Action send, StreamKeyframe? fires = null, Action? onSent = null)
    {
        Stream.Buffer(_owner, size, isAudio, () =>
        {
            if (!SafeSend(send)) return false;
            onSent?.Invoke();
            if (fires is not null && !fires.Fired)
            {
                fires.Fired = true;
                KeyframesFired++;
                KeyframeFired?.Invoke(fires.Source);
            }
            return true;
        });
    }

    private bool SafeSend(Action send)
    {
        try
        {
            send();
            return _sink.LastSendSucceeded;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>A zero-size step that sends no robot message (a local event, a keyframe notification).</summary>
    private void BufferLocal(Action? act, StreamKeyframe? fires = null)
    {
        Stream.Buffer(_owner, 0, false, () =>
        {
            try { act?.Invoke(); }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { return false; }
            if (fires is not null && !fires.Fired)
            {
                fires.Fired = true;
                KeyframesFired++;
                KeyframeFired?.Invoke(fires.Source);
            }
            return true;
        });
    }

    /// <summary>
    /// C2/C3: RandIntInRange(v − var, v + var) on IKeyFrame::sRNG, drawn on every GetStreamMessage when var &gt; 0, no
    /// clamp (the caller's cast truncates or wraps).
    /// </summary>
    private int WithVariability(int value, int variability) =>
        variability > 0 ? _keyframeRng.RandIntInRange(value - variability, value + variability) : value;

    // ================================================================== audio (M6 interface)

    /// <summary>Whether a sound is streaming right now (audio frames carry samples rather than silence).</summary>
    public bool AudioStreaming { get { lock (_gate) return _audio?.Streaming ?? false; } }

    /// <summary>How many times a Stop event ended a streaming sound in the current or last animation.</summary>
    public int AudioStops { get; private set; }

    // fidelity: M5-017, M5-012
    /// <summary>
    /// The stand-in for RobotAudioClient's RobotAudioAnimation (an M6 interface): InitStream creates one for an animation
    /// with RobotAudio keyframes. Its Update (A15) starts each audio keyframe when start + trigger ≤ the stream time and
    /// moves the RobotAudio track on; its frames are the sound's 744-sample mu-law frames, or none (silence) while the
    /// source has not rendered them; it is ready always and complete once the track is at its end and nothing plays. The
    /// engine's readiness states (C16, Q5) are M6's and not built.
    /// </summary>
    private sealed class AudioAnimation
    {
        private readonly AnimationScheduler _s;
        private readonly StreamTrack<StreamKeyframe> _track;
        private short[]? _pcm;
        private long? _eventId;
        private int _pos;

        public AudioAnimation(AnimationScheduler s, StreamTrack<StreamKeyframe> track)
        {
            _s = s;
            _track = track;
        }

        public bool IsReady => true;
        public bool IsComplete => _track.AtEnd && _pcm is null;
        public bool Streaming => _pcm is not null;

        public void Abort() { _pcm = null; _eventId = null; _pos = 0; }

        public void Update(long start, long streamTime)
        {
            var k = _track.Current;
            if (k is null || start + k.Trigger > streamTime) return;
            Start((AudioKeyframe)k.Source);
            _s.BufferLocal(null, k);
            _track.MoveToNext();
        }

        /// <summary>
        /// C14 GetAudioRef → GetAudioRefIndex(true): r = (float)RandDbl(1); the first alternative with lower ≤ r ≤ upper,
        /// skipping |p| &lt; 1e-5; none → the default {event 0, volume 1.0}, which plays nothing. A Stop action ends the
        /// playing sound whose target it covers.
        /// </summary>
        private void Start(AudioKeyframe k)
        {
            var source = _s.AudioSource;
            if (source is null || k.EventIds.Length == 0) return;
            int chosen = ChooseAlternative(k.EventIds.Length, k.Probabilities, _s._keyframeRng.RandDbl(1));
            if (chosen < 0) return;
            long id = k.EventIds[chosen];
            if (source.IsStopEvent(id))
            {
                if (_pcm is not null && (_eventId is not { } playing || source.StopAffects(id, playing)))
                {
                    _pcm = null; _pos = 0; _eventId = null;
                    _s.AudioStops++;
                }
                return;
            }
            var pcm = source.GetPcm(id, k.Volume);
            if (pcm is null || pcm.Length == 0) return;
            _pcm = pcm; _pos = 0; _eventId = id;
        }

        /// <summary>
        /// PopRobotAudioMessage: 744 mu-law bytes (a short frame zero-padded, M3 C5), or null when there is no sound or the
        /// source has not rendered the next frame yet.
        /// </summary>
        public byte[]? PopFrame()
        {
            if (_pcm is not { } pcm || _pos >= pcm.Length) return null;
            int want = Math.Min(CozmoAudio.SamplesPerFrame, pcm.Length - _pos);
            int ready = _s.AudioSource is { } src ? src.ReadySamples(pcm, _pos) : pcm.Length;
            if (Math.Max(0, ready - _pos) < want) return null;
            var samples = new byte[CozmoAudio.SamplesPerFrame];
            for (int i = 0; i < want; i++) samples[i] = AnkiMuLaw.Encode(pcm[_pos + i]);
            _pos += want;
            if (_pos >= pcm.Length) { _pcm = null; _pos = 0; _eventId = null; }
            return samples;
        }
    }

    /// <summary>
    /// C14 GetAudioRefIndex(true) over the loaded probabilities (C13: absent probabilities are 1/N; a count mismatch or a
    /// sum above 1 rejects the keyframe at load, so a keyframe built in code with such probabilities falls back to 1/N
    /// here): the first alternative whose range [acc, acc + p] holds <paramref name="draw"/>, skipping |p| &lt; 1e-5;
    /// −1 when none does.
    /// </summary>
    internal static int ChooseAlternative(int count, float[] probabilities, double draw)
    {
        if (count <= 0) return -1;
        float[] p;
        if (probabilities.Length == count && probabilities.Sum() <= 1f + 1e-4f)
            p = probabilities;
        else
        {
            p = new float[count];
            Array.Fill(p, 1f / count);
        }

        float acc = 0f, r = (float)draw;
        for (int i = 0; i < count; i++)
        {
            if (MathF.Abs(p[i]) < 1e-5f) continue;
            float next = acc + p[i];
            if (!(acc > r) && next >= r) return i;
            acc = next;
        }
        return -1;
    }

    // ================================================================== face animations

    /// <summary>The stored face-animation frames by name, two variants each (C12); <see cref="FaceAnimationLibrary.Variants"/>.</summary>
    public Func<string, IReadOnlyList<FaceAnimationFrame>?>? FaceAnimationVariants { get; set; }

    /// <summary>
    /// A test seam: face-animation frames given as this stack's pictures (each bitmap row on both canvas rows of its
    /// pair). <see cref="FaceAnimationVariants"/> takes precedence.
    /// </summary>
    public Func<string, IReadOnlyList<FaceBitmap>?>? FaceAnimations { get; set; }

    /// <summary>The face animation keyframe current on the streaming animation, and how far into it the stream is.</summary>
    public (string Name, int Frame, int Count)? FaceAnimation
    {
        get
        {
            lock (_gate)
            {
                if (_streaming?.FaceAnim.Current is not { } k || k.FaceAnimIndex == 0) return null;
                var fa = (FaceAnimationKeyframe)k.Source;
                return (fa.AnimName, k.FaceAnimIndex, FrameCount(fa));
            }
        }
    }

    // ================================================================== reset

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the constructed state, for a removed robot (CB33, CC26: the Robot and its AnimationStreamer are deleted;
    /// CC27: the next connect builds them afresh). A streaming clip's handle ends as cancelled; then every A1 field is as
    /// the constructor leaves it, the send buffer and its counters are zeroed (C13), and the layer-base face is Reset to the
    /// neutral again (A2). <see cref="Generation"/> is not reset (callers hold it), and the catalog, sources and
    /// subscribers are kept.
    /// </summary>
    internal void ResetToConstructed()
    {
        lock (_gate)
        {
            if (_streaming is not null) EndHandleLocked(AnimationEndReason.Cancelled);
            Stream.ResetToConstructed();
            _streaming = null;
            _streamingClip = null;
            _idleAnim = null;
            _idleMs = 0;
            _idleStack.Clear();
            _idleStack.Add((IdleCount, DefaultAnimLock));
            _lastStreamedName = "";
            _idleInitialised = false;
            _numLoops = 1;
            _loopCounter = 0;
            _tagCounter = 0;
            _startSent = false;
            _endSent = false;
            _abortedToNothing = false;
            _startMs = _streamMs = 0;
            _lastStreamSec = -float.MaxValue;
            _tag = 0;
            _live = StreamAnimation.Live();
            _liveFlag = false;
            _bodyDurMs = _liftDurMs = _headDurMs = _bodySpacingMs = _liftSpacingMs = _headSpacingMs = 0;
            _liveEyeShiftTag = 0;
            _audio = null;
            _keepAliveTimeoutSec = 0.5;
            _lastInitName = "";
            _lastToggleMs = 0;
            _defaultParamsSet = false;
            _seamDueMs = 0;
            _nowMs = 0;
            var neutralFace = _neutral?.Keyframes.OfType<FaceKeyframe>().FirstOrDefault()?.Pose;
            _tlc.Reset();
            _tlc.Init(neutralFace);
            AudioFramesSent = 0;
            PositionMs = 0;
            KeyframesFired = 0;
            AudioStops = 0;
        }
    }
}

// fidelity: M5-030
/// <summary>
/// What UpdateLiveAnimation reads from the robot (gap4 L2..L6; gap1 R2..R4): DockingComponent+4 (picking or placing,
/// status bit 2), the MovementComponent flags +9 (IS_MOVING), +0xB (not LIFT_IN_POS) and +0xA (not HEAD_IN_POS), the track
/// locks (AreAnyTracksLocked: body 4, lift 2, head 1), CarryingComponent+8 ≠ −1 (carrying, M12's), and robot+0x2FC (the
/// head angle, radians). Unset, each reads as clear or 0.
/// </summary>
public sealed class LiveIdleRobotInputs
{
    public Func<bool> PickingOrPlacing { get; set; } = () => false;
    public Func<bool> Moving { get; set; } = () => false;
    public Func<bool> LiftNotInPosition { get; set; } = () => false;
    public Func<bool> HeadNotInPosition { get; set; } = () => false;
    public Func<byte> LockedTracks { get; set; } = () => 0;
    public Func<bool> Carrying { get; set; } = () => false;
    public Func<float> HeadAngleRad { get; set; } = () => 0f;
}

/// <summary>
/// <c>EngineToRobot::Size()</c> of each stream message (C11): the serialised length of the message
/// <see cref="RobotAnimationSink"/> sends for that call. Calls that send no robot message count 0.
/// </summary>
internal static class StreamSizes
{
    public static readonly int AudioSample = StreamSendBuffer.SizeOf(new AudioSample());
    public static readonly int AudioSilence = StreamSendBuffer.SizeOf(new AudioSilence());
    public static readonly int StartOfAnimation = StreamSendBuffer.SizeOf(new StartOfAnimation());
    public static readonly int EndOfAnimation = StreamSendBuffer.SizeOf(new EndOfAnimation());
    public static readonly int HeadAngle = StreamSendBuffer.SizeOf(new Protocol.HeadAngle());
    public static readonly int LiftHeight = StreamSendBuffer.SizeOf(new Protocol.LiftHeight());
    public static readonly int BodyStop = StreamSendBuffer.SizeOf(new BodyMotion());
    public static readonly int BackpackLights = StreamSendBuffer.SizeOf(new Protocol.BackpackLights());
    public static readonly int Event = StreamSendBuffer.SizeOf(new Protocol.Event());
    public static readonly int RecordHeading = StreamSendBuffer.SizeOf(new Protocol.RecordHeading());
    public static readonly int TurnToRecordedHeading = StreamSendBuffer.SizeOf(new Protocol.TurnToRecordedHeading());

    /// <summary>A body keyframe whose radius the engine does not understand sends nothing.</summary>
    public static int Body(BodyKeyframe b) => b.EncodedRadius is null ? 0 : BodyStop;

    public static int Face(byte[] payload) => StreamSendBuffer.SizeOf(new Protocol.FaceImage { Image = payload });
}
