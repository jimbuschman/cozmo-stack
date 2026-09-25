using Cozmo.Protocol;

namespace Cozmo.Robot.Animation;

/// <summary>What a keyframe should do when the scheduler reaches it. Kept separate from the transport so the
/// timeline can be tested without a robot.</summary>
public interface IAnimationSink
{
    /// <summary>The face for this tick, already rendered. Null means leave the face alone.</summary>
    void Face(FaceBitmap bitmap);
    /// <summary>One audio frame's worth of samples, or silence when the argument is null.</summary>
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
    /// <summary>
    /// A head keyframe, as the engine streams it: <c>HeadAngleKeyFrame::GetStreamMessage</c> at 0x004F8C08
    /// builds <c>AnimKeyFrame::HeadAngle</c> (animHeadAngle, 0x93) from the keyframe's duration and its
    /// angle in whole degrees, with the keyframe's variability already applied. It is an animation
    /// keyframe, not a <c>SetHeadAngle</c> motor command.
    /// </summary>
    void Head(sbyte angleDeg, uint durationMs);
    /// <summary>
    /// A lift keyframe, as <c>LiftHeightKeyFrame::GetStreamMessage</c> at 0x004F8F80 streams it:
    /// <c>AnimKeyFrame::LiftHeight</c> (animLiftHeight, 0x94) with the duration and the height in whole
    /// millimetres, variability already applied.
    /// </summary>
    void Lift(byte heightMm, uint durationMs);
    /// <summary>
    /// An animation is about to start streaming. The engine opens every animation with a
    /// StartOfAnimation carrying a tag, and the robot reports that tag back in its AnimationState, so a
    /// caller can tell the robot really accepted it.
    /// </summary>
    void AnimationStarted(byte tag);
    /// <summary>The animation has finished streaming, however it ended.</summary>
    void AnimationEnded();
    void Body(BodyKeyframe keyframe);
    /// <summary>
    /// Stop the body. DriveWheels runs until countermanded, so unlike head and lift the scheduler has to
    /// end a body keyframe explicitly when its duration expires.
    /// </summary>
    void BodyStop();
    void Lights(LightsKeyframe keyframe);
    void Event(string eventId);
    /// <summary>Called once when an animation ends, whether it finished or was cancelled.</summary>
    void Finished(string clipName, bool completed);
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

    private readonly TaskCompletionSource<AnimationEndReason> _done =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the animation stops, with the reason.</summary>
    public Task<AnimationEndReason> Completion => _done.Task;
    public bool IsRunning => !_done.Task.IsCompleted;

    internal void Complete(AnimationEndReason reason) => _done.TrySetResult(reason);
}

/// <summary>
/// The one clock for animation.
///
/// Every timed thing an animation does is scheduled here, so the face, the audio, the motors and the lights stay
/// on one timeline. The device classes underneath keep their own APIs but do not invent their own animation
/// timing: the scheduler calls them, not the other way round.
///
/// One animation streams at a time, as in the engine: <c>AnimationStreamer</c> holds a single streaming
/// animation (this+0x38), and <c>SetStreamingAnimation</c> at 0x0057B174 either interrupts it or turns the
/// newcomer away (see <see cref="Play"/>). Two animations never run side by side, whatever tracks they use.
///
/// Time on the timeline is counted in built frames of 33 ms, as the engine counts it (A18). Each
/// <see cref="Advance"/> is one engine Update of the streamer (C15): the budgets are refreshed, leftovers are
/// flushed, and frames are built one at a time while the buffer is empty (<see cref="Stream"/>).
/// </summary>
public sealed class AnimationScheduler
{
    /// <summary>The engine's animation tick: 30 frames per second.</summary>
    public const int FrameRateHz = 30;
    /// <summary>The wall-clock spacing of frames on the test seam's clock: one robot audio frame, 744 samples at 22320 Hz.</summary>
    public static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / FrameRateHz);
    /// <summary>
    /// The engine's stream-time step. <c>AnimationStreamer::UpdateStream</c> at 0x0057C84C adds 33 (0x21)
    /// to its stream time (this+0x84) after each frame it has sent (0x0057CA94..0x0057CA9C), so animation
    /// time moves in whole 33 ms steps, one per streamed frame, and never by the wall clock.
    /// </summary>
    public const int FrameStepMs = 33;

    private readonly IAnimationSink _sink;
    private readonly object _gate = new();

    /// <summary>
    /// The emission gate, and the contract that goes with it.
    ///
    /// A keyframe belongs to one playback. Building a frame, draining the stream buffer and changing which
    /// playback owns the timeline all happen under this gate, so a replacement cannot land between a frame being
    /// built and its messages going out. A replacement drops what is buffered (InitStream's ClearSendBuffer, A12); a
    /// cancel leaves it for the next Update to flush (A24, A25).
    ///
    /// The lock order is always this gate, then <see cref="_gate"/>, then the stream buffer's own lock.
    /// </summary>
    private readonly object _emit = new();

    private AnimationClip? _clip;
    private AnimationHandle? _handle;
    private sealed class StreamOwner { }       // marks the scheduler's own buffered messages
    private StreamOwner _clipOwner = new();    // the buffered messages of the running clip carry this
    private readonly StreamOwner _liveOwner = new(); // those of the live stream carry this
    private int _framesStreamed;               // frames built for the running clip (A18); the stream time is this x 33 ms
    private bool _framesLeft;                  // the running clip has frames left to build (HasFramesLeft)
    private bool _endSent;                     // +0x72: EndOfAnimation sent (or the clip is empty, A12)
    private double _nextDueWallMs;             // test seam: wall time the next frame is due
    private int _nextFrame;                    // index of the next keyframe to fire
    private IReadOnlyList<FaceKeyframe> _facePoses = Array.Empty<FaceKeyframe>();
    private int _faceIndex = -1;               // index into _facePoses of the pose currently held
    private FaceBitmap? _lastFace;
    private IReadOnlyList<FaceBitmap>? _faceAnim;   // the pre-rendered face animation being played, if any
    private int _faceAnimFrame;                     // how many of its frames have gone out
    private double? _bodyEndsAtMs;             // when the running body keyframe should stop, if one is running
    private double? _liveBodyStopsAtMs;        // the same, for a keep-alive body keyframe, on the wall clock
    private short[]? _audioPcm;                // the sound currently streaming, if any
    private long? _audioEventId;               // the event that started it, for a Stop event to match
    private int _audioPos;                     // how far into it the last frame reached
    private byte _nextTag = 1;                 // the tag the next animation opens with
    private bool _startSent;                   // has StartOfAnimation been buffered for the running clip
    private bool _liveOpen;                    // has the live animation's StartOfAnimation (tag 0xFF) been buffered
    private bool _liveActive;                  // the live stream is the idle stream: streamed whenever no clip is
    private int _liveFramesSent;               // frames of the live stream built since it was opened
    private bool _liveReopenPending;           // a clip ended with the live stream active: the next Update re-inits live (A29)

    /// <summary>
    /// The tag the live animation streams under: <c>AnimationStreamer::Update</c> opens it with
    /// <c>InitStream(live, 0xFF)</c> (movs r2, #0xff at 0x0057D3FC).
    /// </summary>
    public const byte LiveAnimationTag = 0xFF;
    private long _generation;                  // bumped whenever the running animation changes
    private readonly Random _random;

    /// <param name="random">
    /// The engine's <c>IKeyFrame::sRNG</c>: it decides head and lift variability and which audio
    /// alternative a keyframe plays. Pass a seeded instance for a reproducible timeline.
    /// </param>
    public AnimationScheduler(IAnimationSink sink, Random? random = null)
    {
        _sink = sink;
        _random = random ?? new Random();
    }

    /// <summary>
    /// The engine's send buffer, budgets and stream counters (M3-012..M3-014). The policy APIs that stream audio
    /// outside an animation (<see cref="CozmoAudio.Play"/>, M3-017) go through the same buffer and budget.
    /// </summary>
    public StreamSendBuffer Stream { get; } = new();

    /// <summary>
    /// Where the sound for an audio keyframe comes from. Left null, audio keyframes send silence so the
    /// timeline stays intact but nothing is heard. See <see cref="IAnimationAudioSource"/>.
    /// </summary>
    public IAnimationAudioSource? AudioSource { get; set; }

    /// <summary>Audio frames streamed since the current animation started.</summary>
    public int AudioFramesSent { get; private set; }

    /// <summary>The animation currently running, or null.</summary>
    public string? Playing { get { lock (_gate) return _clip?.Name; } }
    public bool IsPlaying { get { lock (_gate) return _clip is not null; } }
    /// <summary>
    /// Whether <see cref="Advance"/> still has something to do: a clip is running, a live keyframe has armed a
    /// deadline that only <see cref="Advance"/> can serve, the live stream is active, or messages are waiting in
    /// the stream buffer.
    /// </summary>
    public bool HasPendingWork
    {
        get
        {
            lock (_gate)
                if (_clip is not null || _liveBodyStopsAtMs is not null || _liveActive) return true;
            return !Stream.IsEmpty;
        }
    }

    /// <summary>
    /// Whether the live stream is active. Once it is, it is streamed on every <see cref="Advance"/> in which
    /// no clip is running, as <c>AnimationStreamer::Update</c> 0x0057CE5C calls <c>UpdateStream(live)</c> on
    /// every update (0x0057D430) and only a clip's <c>InitStream</c> takes its place.
    /// </summary>
    public bool LiveStreamActive { get { lock (_gate) return _liveActive; } }

    /// <summary>
    /// Whether the last live keyframe armed a deadline <see cref="Advance"/> still has to serve. The
    /// caller that streams live keyframes uses this to decide whether a tick loop is needed.
    /// </summary>
    public bool LiveBodyRunning { get { lock (_gate) return _liveBodyStopsAtMs is not null; } }
    /// <summary>Tracks currently claimed by the running animation.</summary>
    public AnimationTrack OwnedTracks { get { lock (_gate) return _clip?.Tracks ?? AnimationTrack.None; } }
    /// <summary>The timeline position of the last frame built, in milliseconds (its stream time), not elapsed wall time.</summary>
    public double PositionMs { get; private set; }

    // fidelity: M3-013
    /// <summary>
    /// The engine's stream time for the running clip, +0x84 less its start +0x80 (A12): 33 ms for every frame built
    /// whose drain returned without a send error, a budget stop included (A18).
    /// </summary>
    public double StreamTimeMs { get { lock (_gate) return _framesStreamed * (double)FrameStepMs; } }
    /// <summary>Keyframes fired since the current animation started.</summary>
    public int KeyframesFired { get; private set; }

    /// <summary>
    /// The tag the running animation was opened with, which the robot echoes in its AnimationState. Zero
    /// when nothing is running; the engine never uses zero for a running animation either.
    /// </summary>
    public byte CurrentTag { get; private set; }

    /// <summary>
    /// Identifies the animation currently running. Changes whenever one starts or ends, so a caller that
    /// started an animation can tell whether it is still the one playing.
    /// </summary>
    public long Generation { get { lock (_gate) return _generation; } }

    /// <summary>
    /// Stops the running animation, but only if it is still the one identified by
    /// <paramref name="generation"/>. Returns false when something else has taken over, which is what
    /// stops one caller cancelling an animation that replaced its own.
    /// </summary>
    public bool StopIfCurrent(long generation)
    {
        lock (_emit)
        lock (_gate)
        {
            if (_clip is null || _generation != generation) return false;
            EndLocked(AnimationEndReason.Cancelled);
            return true;
        }
    }

    /// <summary>Raised for every keyframe as it fires (when its message goes out), for logging and tests.</summary>
    public event Action<Keyframe>? KeyframeFired;

    /// <summary>
    /// Starts a clip.
    ///
    /// The engine streams exactly one animation at a time. <c>AnimationStreamer::SetStreamingAnimation</c>
    /// at 0x0057B174 keeps a single streaming animation, and when one is already streaming a newcomer
    /// either interrupts it (its <c>interruptRunning</c> flag: "Animation %s is interrupting animation %s",
    /// then <c>Abort()</c> and <c>InitStream</c>) or is turned away whatever tracks it uses ("Already
    /// streaming %s, will not interrupt with %s", nothing changes).
    ///
    /// <paramref name="replaceRunning"/> is the engine's <c>interruptRunning</c>. With it the running
    /// animation ends as <see cref="AnimationEndReason.Replaced"/> and this one starts. Without it the clip
    /// is refused and null is returned whenever anything is running.
    /// </summary>
    public AnimationHandle? Play(AnimationClip clip, double nowMs, bool replaceRunning = true)
    {
        lock (_emit)
        lock (_gate)
        {
            if (_clip is not null)
            {
                if (!replaceRunning) return null;
                EndLocked(AnimationEndReason.Replaced);
            }
            _clip = clip;
            _generation++;
            _handle = new AnimationHandle(clip.Name, clip.Tracks) { Generation = _generation };
            CurrentTag = _nextTag;
            // IncrementTagCtr at 0x0057B660 keeps incrementing while the value it came from was above
            // 0xFD, so the engine stores neither 0x00 nor 0xFF. The usable range is 1..0xFE.
            _nextTag = _nextTag >= 0xFE ? (byte)1 : (byte)(_nextTag + 1);
            _startSent = false;
            // A clip taking over ends the live stream without an EndOfAnimation: InitStream 0x0057B674 only clears
            // the send buffer and the started/ended flags before the clip's own StartOfAnimation. What the live
            // stream still had buffered goes with it.
            _liveOpen = false;
            _liveReopenPending = false;
            ClearSendBufferLocked();
            _clipOwner = new StreamOwner();
            _framesStreamed = 0;
            // InitStream (A12): endSent = the clip is empty, startSent = 0
            bool empty = clip.Keyframes.Count == 0 && clip.DurationMs == 0;
            _endSent = empty;
            _framesLeft = !empty;
            _nextDueWallMs = nowMs;
            _nextFrame = 0;
            _facePoses = clip.Keyframes.OfType<FaceKeyframe>().ToList();
            _faceIndex = -1;
            _bodyEndsAtMs = null;
            _audioPcm = null; _audioPos = 0; _audioEventId = null;
            AudioFramesSent = 0;
            AudioStops = 0;
            KeyframesFired = 0;
            PositionMs = 0;
            return _handle;
        }
        // StartOfAnimation is not buffered here. The engine buffers it inside UpdateStream, on the first frame that
        // actually streams and after that frame's audio message (0x0057C9C8). Advance does the same.
    }

    /// <summary>
    /// Streams one keyframe of the engine's <b>live animation</b> — the keep-alive clip the streamer
    /// always has open.
    ///
    /// <c>AnimationStreamer</c> constructs an <c>Animation</c> of its own at this+0xA8, marks it live
    /// (<c>SetIsLive(true)</c> at 0x0057A060) and streams it whenever no real animation is playing;
    /// <c>UpdateLiveAnimation</c> at 0x0057D5F8 appends head, lift and body keyframes to it as their
    /// timers expire. The keyframes that come out are the ordinary 0x93 / 0x94 / 0x99 stream messages,
    /// with the same stream-time variability draw.
    ///
    /// The messages go into the stream buffer (M3-014) and the buffer is drained at once within the budget this
    /// Update has left; what does not fit goes at the next Update. Returns false when a running clip owns the
    /// keyframe's track, which is the engine's own condition.
    /// </summary>
    public bool StreamLive(Keyframe k, double nowMs)
    {
        lock (_emit)
        {
            lock (_gate)
            {
                if (_clip is not null && (_clip.Tracks & k.Track) != AnimationTrack.None) return false;
                _liveActive = true;
            }
            // The live keyframes are not sent bare. UpdateLiveAnimation 0x0057D5F8 only appends them to the live
            // Animation; they reach the robot through the ordinary stream, InitStream(live, 0xFF) at 0x0057D3FE
            // and then UpdateStream 0x0057C84C, whose every frame buffers an audio message, then StartOfAnimation
            // once, then the track messages.
            // fidelity: M3-013
            // A clip that just ended left the live stream to be re-initialised (A29, 0x0057D3FE): its InitStream drops
            // the clip's leftovers (A12, 0x0057B7CE). The live keyframe belongs to the re-initialised live stream, so the
            // drop happens here, before the keyframe is buffered, rather than at the next Update.
            bool reopenNow;
            lock (_gate) { reopenNow = _clip is null && _liveReopenPending; if (reopenNow) _liveReopenPending = false; }
            if (reopenNow) ClearSendBufferLocked();
            bool open;
            lock (_gate) open = _clip is null && !_liveOpen;
            if (open) BuildLiveFrame();

            switch (k)
            {
                case HeadKeyframe h:
                {
                    var deg = (sbyte)Math.Clamp(WithVariability(h.AngleDeg, h.VariabilityDeg), sbyte.MinValue, sbyte.MaxValue);
                    Buffer(_liveOwner, StreamSizes.HeadAngle, false, () => _sink.Head(deg, h.DurationTimeMs));
                    break;
                }
                case LiftKeyframe l:
                {
                    var mm = (byte)Math.Clamp(WithVariability(l.HeightMm, l.VariabilityMm), byte.MinValue, byte.MaxValue);
                    Buffer(_liveOwner, StreamSizes.LiftHeight, false, () => _sink.Lift(mm, l.DurationTimeMs));
                    break;
                }
                case BodyKeyframe b:
                    Buffer(_liveOwner, StreamSizes.Body(b), false, () => _sink.Body(b));
                    lock (_gate)
                        // The deadline is in the clock the caller passes, which has to be the same clock
                        // Advance is driven on - see CozmoAnimations.StreamLive, which supplies both.
                        _liveBodyStopsAtMs = b.RadiusIsKnown && b.DurationTimeMs > 0 && b.Speed != 0
                            ? nowMs + b.DurationTimeMs
                            : null;
                    break;
                default:
                    return false;
            }
            if (_sink.AudioFramesPlayed is null) Stream.Unlimited();
            Stream.Drain();
            return true;
        }
    }

    /// <summary>Stops whatever is running. Returns false when nothing was.</summary>
    public bool Stop()
    {
        lock (_emit)
        lock (_gate)
        {
            if (_clip is null) return false;
            EndLocked(AnimationEndReason.Cancelled);
            return true;
        }
    }

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26: the Robot and its AnimationStreamer
    /// are deleted; CC27: the next connect builds them afresh). A running clip ends as <see cref="Stop"/> ends it; then
    /// the live stream is closed and inactive, the tag counter is back at 1, the stream buffer is empty and its
    /// counters zeroed (C13), and every timeline field and count is as built. <see cref="Generation"/> is not reset:
    /// it is a token callers hold, and restarting it could make a token from before the removal name an animation
    /// started after it. <see cref="AudioSource"/>, <see cref="FaceAnimations"/>, the random source and subscribers are kept.
    /// </summary>
    internal void ResetToConstructed()
    {
        lock (_emit)
        lock (_gate)
        {
            if (_clip is not null) EndLocked(AnimationEndReason.Cancelled);
            Stream.ResetToConstructed();
            _framesStreamed = 0;
            _framesLeft = false;
            _endSent = false;
            _nextDueWallMs = 0;
            _nextFrame = 0;
            _facePoses = Array.Empty<FaceKeyframe>();
            _faceIndex = -1;
            _lastFace = null;
            _faceAnim = null;
            _faceAnimFrame = 0;
            _faceAnimName = "";
            _bodyEndsAtMs = null;
            _liveBodyStopsAtMs = null;
            _audioPcm = null;
            _audioEventId = null;
            _audioPos = 0;
            _nextTag = 1;
            _startSent = false;
            _liveOpen = false;
            _liveActive = false;
            _liveReopenPending = false;
            _liveFramesSent = 0;
            AudioFramesSent = 0;
            PositionMs = 0;
            KeyframesFired = 0;
            CurrentTag = 0;
            AudioStops = 0;
        }
    }

    /// <summary>
    /// InitStream's ClearSendBuffer (A12, 0x0057B7CE): the queued stream messages are dropped, not sent. Only the
    /// scheduler's own messages (clips and the live stream) go; what a policy API (M3-017) buffered stays.
    /// </summary>
    private void ClearSendBufferLocked() => Stream.Remove(o => o is StreamOwner);

    private void EndLocked(AnimationEndReason reason)
    {
        var name = _clip?.Name ?? "";
        _clip = null;
        _generation++;
        var h = _handle; _handle = null;
        _facePoses = Array.Empty<FaceKeyframe>();
        _faceIndex = -1;
        _bodyEndsAtMs = null;
        _audioPcm = null; _audioPos = 0; _audioEventId = null;
        CurrentTag = 0;
        // Abort 0x0057B3E0 clears startSent/endSent (A24, 0x0057B578) and does not clear the send buffer: what a
        // cancelled clip left buffered is flushed by the next Update (A25, 0x0057D122..0x0057D174), and since startSent
        // is clear no EndOfAnimation follows. A replacement's InitStream drops it instead (Play). A completed clip
        // arrives here after its EndOfAnimation went (Advance).
        _startSent = false;
        _endSent = false;
        _framesLeft = false;
        // fidelity: M3-013
        // With the live stream active (this stack's ProceduralLive idle), every clip Update leaves +0x64 = 0 (A13,
        // 0x0057D022), so the Update after the clip ends takes the idle path and re-inits the live animation
        // (InitStream(live, 0xFF), A29, 0x0057D3EE..0x0057D412), dropping whatever the clip left buffered (A12).
        _liveReopenPending = _liveActive;
        // The sink talks to the robot, and by the time an animation is being ended the robot may be gone -
        // a disconnect is exactly when something is cut short. None of this may stop the handle completing.
        try { _sink.Finished(name, reason == AnimationEndReason.Completed); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // the robot went away mid-animation; there is nothing left to tell it
        }
        h?.Complete(reason);
    }

    /// <summary>Buffers one sink call that sends a robot message; the drain sees whether that send succeeded.</summary>
    private void Buffer(object? owner, int size, bool isAudio, Action send)
        => Stream.Buffer(owner, size, isAudio, () => { send(); return _sink.LastSendSucceeded; });

    /// <summary>Buffers a zero-size step that sends no robot message (an event, a keyframe notification).</summary>
    private void BufferLocal(object? owner, Action act)
        => Stream.Buffer(owner, 0, false, () => { act(); return true; });

    /// <summary>
    /// One engine Update of the streamer (C15; A13..A20).
    /// <list type="bullet">
    /// <item><b>Preamble (A14).</b> UpdateAmountToSend refreshes the budgets (C9) and SendBufferedMessages flushes what
    /// the last Update left; a send error ends the Update. With nothing streaming this is the no-animation path's
    /// flush of leftovers (A25, A28).</item>
    /// <item><b>Frames (A15..A18).</b> While ShouldProcessAnimationFrame holds - the buffer is empty and the clip has
    /// frames left - one frame is built and drained. The stream time then takes its 33 ms step whether the drain
    /// emptied the buffer or a budget stopped it (0x0057CA88..0x0057CA9C; a budget stop also returns 0); only a send
    /// error stops the loop without the step. A frame left in the buffer makes the next check refuse, so nothing more
    /// is built until a later Update has flushed it.</item>
    /// <item><b>End (A20, 0x0057CB44..0x0057CBB0).</b> When no frames are left and the buffer is empty and the end has
    /// not been sent: if StartOfAnimation was sent, EndOfAnimation goes directly (C12) and nothing follows it; if it
    /// never was, AudioSilence and StartOfAnimation are buffered and drained, and EndOfAnimation follows on a later
    /// Update.</item>
    /// </list>
    /// The budget is the sink's robot counters (<see cref="IAnimationSink.AudioFramesPlayed"/>,
    /// <see cref="IAnimationSink.AnimBytesPlayed"/>). A sink that reports no robot (null) is the test seam: no budget
    /// applies, and the clip frames a call builds are the ones its clock owes, one per <see cref="FrameInterval"/>, at
    /// most <see cref="StreamSendBuffer.AudioFramesAhead"/>, and one live-stream frame per call.
    ///
    /// NOT BUILT HERE (MD5, M5): ShouldProcessAnimationFrame's audio-animation readiness (C15, C16, A15, Q5), and the
    /// track pull rule of one keyframe per track per frame (A19); a sound whose samples are not rendered yet sends
    /// silence for the frame and keeps its place, as before.
    /// </summary>
    public void Advance(double nowMs)
    {
        lock (_emit)
        {
            // fidelity: M3-013
            // The Update after a clip COMPLETED with the live stream active (A29, 0x0057D064 with the idle top
            // ProceduralLive): +0x64 is 0 (A13, 0x0057D022), so it calls InitStream(live, 0xFF) (0x0057D3FE), whose
            // ClearSendBuffer drops the clip's leftovers (A12, 0x0057B7CE), and builds no frame in that Update (r7 = 0 at
            // 0x0057D404); the live stream's StartOfAnimation 0xFF goes with its first frame, on the next Update.
            // After a CANCEL the engine instead replays the neutral-face clip first (A6 sets +0x73; A31 at
            // 0x0057CFC0..0x0057CFD2), whose InitStream is what drops the leftovers; this stack has no neutral replay yet
            // (M5 gap, M5-010/M5-028), so it takes the same drop-and-reopen path, which matches the engine only in the
            // drop. With no live stream the leftovers are flushed instead (A25), below.
            bool reopen;
            lock (_gate)
            {
                reopen = _clip is null && _liveActive && _liveReopenPending;
                if (reopen) { _liveReopenPending = false; _liveOpen = false; }
            }
            if (reopen)
            {
                ClearSendBufferLocked();
                return;
            }

            // A keep-alive body keyframe has to be stopped whether or not a clip is running, because
            // DriveWheels runs until countermanded and the live animation is not on the clip timeline.
            bool stopLiveBody = false;
            lock (_gate)
                if (_liveBodyStopsAtMs is { } end && nowMs >= end)
                {
                    _liveBodyStopsAtMs = null;
                    stopLiveBody = true;
                }
            if (stopLiveBody) Buffer(_liveOwner, StreamSizes.BodyStop, false, _sink.BodyStop);

            int? framesPlayed = _sink.AudioFramesPlayed;
            bool paced = framesPlayed is not null;
            if (paced) Stream.UpdateAmountToSend(_sink.AnimBytesPlayed ?? 0, framesPlayed!.Value);
            else Stream.Unlimited();

            if (Stream.Drain() == StreamSendBuffer.DrainResult.SendError) return;      // A14

            bool clipRunning;
            lock (_gate) clipRunning = _clip is not null;
            if (clipRunning) StreamClip(nowMs, paced);
            else if (LiveStreamActive) StreamLiveFrames(paced);

            if (!paced)
            {
                lock (_gate)
                {
                    double maxDebt = StreamSendBuffer.AudioFramesAhead * FrameInterval.TotalMilliseconds;
                    // A stall that outlasts the robot's whole buffer is not owed more than that buffer.
                    if (nowMs - _nextDueWallMs > maxDebt) _nextDueWallMs = nowMs - maxDebt;
                }
            }
        }
    }

    // fidelity: M3-013, M3-015
    /// <summary>UpdateStream for the running clip (A15..A18, A20). Called under <see cref="_emit"/>.</summary>
    private void StreamClip(double nowMs, bool paced)
    {
        lock (_gate)
            if (_endSent && !_framesLeft)
            {
                // A13: an empty clip (endSent from InitStream) with nothing left completes without a message
                EndLocked(AnimationEndReason.Completed);
                return;
            }
        int owed = paced ? int.MaxValue : SeamFramesOwed(nowMs);
        int built = 0;
        // ShouldProcessAnimationFrame (A15): the buffer is empty and the clip has frames left
        while (Stream.IsEmpty && FramesLeft && built < owed)
        {
            BuildClipFrame();
            built++;
            var r = Stream.Drain();
            if (r == StreamSendBuffer.DrainResult.SendError) return;            // A18: no step on a send error
            lock (_gate)
            {
                // A18: +0x84 += 33 after every frame whose drain returned 0, a budget stop included
                _framesStreamed++;
                _nextDueWallMs += FrameInterval.TotalMilliseconds;
            }
        }

        // A20: not processing, no frames left, the buffer empty and the end not sent
        bool atEnd;
        lock (_gate) atEnd = _clip is not null && !_framesLeft && !_endSent;
        if (!atEnd || !Stream.IsEmpty) return;

        // A body keyframe still running when the clip completes is stopped (this stack's body track, M5); it goes
        // before the end, so the end waits until it has been sent.
        bool stopBody;
        lock (_gate) { stopBody = _bodyEndsAtMs is not null; _bodyEndsAtMs = null; }
        if (stopBody)
        {
            Buffer(_clipOwner, StreamSizes.BodyStop, false, _sink.BodyStop);
            if (Stream.Drain() != StreamSendBuffer.DrainResult.Empty) return;
        }

        bool startSent;
        lock (_gate) startSent = _startSent;
        if (startSent)
        {
            // SendEndOfAnimation (0x0057C448..0x0057C496): direct, reliable, not hot, not budget-gated; on success
            // startSent = 0 and endSent = 1, and it counts one frame plus its bytes (C12). Nothing is sent after it.
            bool sent = Stream.SendDirect(StreamSizes.EndOfAnimation, true, () => { _sink.AnimationEnded(); return _sink.LastSendSucceeded; });
            if (!sent) return;
            lock (_gate)
            {
                _startSent = false;
                _endSent = true;
                EndLocked(AnimationEndReason.Completed);
            }
        }
        else
        {
            // Start was never sent (0x0057CB8E..0x0057CB9C): AudioSilence and StartOfAnimation are buffered and drained;
            // EndOfAnimation follows on a later Update.
            byte tag;
            lock (_gate) { tag = CurrentTag; _startSent = true; _endSent = false; }
            Buffer(_clipOwner, StreamSizes.AudioSilence, true, () => _sink.Audio(null));
            Buffer(_clipOwner, StreamSizes.StartOfAnimation, false, () => _sink.AnimationStarted(tag));
            Stream.Drain();
        }
    }

    /// <summary>Whether the running clip has frames left (the stack's HasFramesLeft: its keyframes and duration, M5).</summary>
    private bool FramesLeft { get { lock (_gate) return _clip is not null && _framesLeft; } }

    // fidelity: M3-015
    /// <summary>The live stream's frames (one AudioSilence each), while the buffer is empty. Called under <see cref="_emit"/>.</summary>
    private void StreamLiveFrames(bool paced)
    {
        do
        {
            if (!Stream.IsEmpty) return;
            BuildLiveFrame();
        }
        while (paced && Stream.Drain() == StreamSendBuffer.DrainResult.Empty);
        if (!paced) Stream.Drain();
    }

    /// <summary>
    /// The test seam's clock: how many clip frames a call at <paramref name="nowMs"/> is owed, one per
    /// <see cref="FrameInterval"/> since the last, at least one and at most <see cref="StreamSendBuffer.AudioFramesAhead"/>.
    /// </summary>
    private int SeamFramesOwed(double nowMs)
    {
        lock (_gate)
        {
            if (_clip is null) return 0;
            double interval = FrameInterval.TotalMilliseconds;
            double maxDebt = StreamSendBuffer.AudioFramesAhead * interval;
            double late = nowMs - _nextDueWallMs;
            if (late < 0) { _nextDueWallMs = nowMs; late = 0; }                 // ahead of schedule: this tick is the frame
            return Math.Min(1 + (int)Math.Floor(Math.Min(late, maxDebt) / interval), StreamSendBuffer.AudioFramesAhead);
        }
    }

    // fidelity: M3-015
    /// <summary>
    /// Builds one frame of the live stream into the buffer: its audio message, AudioSilence, and on the first frame
    /// StartOfAnimation with tag 0xFF after it (UpdateStream 0x0057C9A8..0x0057C9D0 after InitStream(live, 0xFF)).
    /// Called under <see cref="_emit"/>.
    /// </summary>
    private void BuildLiveFrame()
    {
        bool open;
        lock (_gate) { open = !_liveOpen; _liveOpen = true; if (open) _liveFramesSent = 0; _liveFramesSent++; }
        Buffer(_liveOwner, StreamSizes.AudioSilence, true, () => _sink.Audio(null));
        if (open) Buffer(_liveOwner, StreamSizes.StartOfAnimation, false, () => _sink.AnimationStarted(LiveAnimationTag));
    }

    // fidelity: M3-015
    /// <summary>
    /// Builds one frame of the running clip into the buffer at the stream time the frame count gives, in the engine's
    /// order (A16, 0x0057C94E..0x0057CA7A):
    /// (1) exactly one audio message, AudioSample with 744 mu-law bytes or AudioSilence (C4); (2) StartOfAnimation, only
    /// if not yet sent, after the audio message (A21); (3) head; (4) lift; (5) event; (6) the face animation's frame;
    /// (7) the procedural face, only if (6) buffered nothing and the face-animation track is at its end (A17: a pending
    /// faceAnimations keyframe, even one not yet due, holds it back); (8) backpack lights; (9) body, and this stack's
    /// body stop when a body keyframe's duration has run out; (10) record heading; (11) turn to recorded heading.
    /// Called under <see cref="_emit"/>.
    /// </summary>
    private void BuildClipFrame()
    {
        AnimationClip clip;
        double t;
        object owner;
        lock (_gate)
        {
            clip = _clip!;
            owner = _clipOwner;
            t = _framesStreamed * (double)FrameStepMs;
            PositionMs = t;
        }

        // How much of the sound is really rendered. A source that renders while it plays fills its buffer from the
        // front; sending the tail's zeros would put silence on the robot in place of the music. Asked outside the
        // lock because the source may do real work, and it is also how a streaming source learns how far playback
        // has got. (The engine's own gate, audio-animation readiness, is M5's: see Advance.)
        int ready = int.MaxValue;
        {
            short[]? pcmNow; int posNow;
            lock (_gate) { pcmNow = _audioPcm; posNow = _audioPos; }
            if (pcmNow is not null && AudioSource is { } src) ready = src.ReadySamples(pcmNow, posNow);
        }

        byte[]? frame = null;
        lock (_gate)
        {
            if (_audioPcm is { } pcm && _audioPos < pcm.Length)
            {
                int want = Math.Min(CozmoAudio.SamplesPerFrame, pcm.Length - _audioPos);
                int have = Math.Max(0, ready - _audioPos);
                if (have >= want)
                {
                    // C5: a frame shorter than 744 samples is zero-padded (0x00 is silence in this codec)
                    var samples = new byte[CozmoAudio.SamplesPerFrame];
                    for (int i = 0; i < want; i++) samples[i] = AnkiMuLaw.Encode(pcm[_audioPos + i]);
                    _audioPos += want;
                    if (_audioPos >= pcm.Length) { _audioPcm = null; _audioPos = 0; _audioEventId = null; }
                    frame = samples;
                }
            }
        }
        var audio = frame;
        // (1)
        Buffer(owner, audio is null ? StreamSizes.AudioSilence : StreamSizes.AudioSample, true,
               () => { _sink.Audio(audio); AudioFramesSent++; });

        // (2) SendStartOfAnimation (A21): startSent = 1, endSent = 0
        byte openWith = 0;
        lock (_gate) if (!_startSent) { _startSent = true; _endSent = false; openWith = CurrentTag; }
        if (openWith != 0) Buffer(owner, StreamSizes.StartOfAnimation, false, () => _sink.AnimationStarted(openWith));

        var due = new List<Keyframe>();
        lock (_gate)
        {
            while (_nextFrame < clip.Keyframes.Count && clip.Keyframes[_nextFrame].TriggerTimeMs <= t)
            {
                var k = clip.Keyframes[_nextFrame++];
                due.Add(k);
                if (k is FaceKeyframe f) _faceIndex = IndexOfFace(f);
            }
            KeyframesFired += due.Count;
        }
        var ordered = due.OrderBy(TrackOrder).ToList();
        // (3) head, (4) lift, (5) event; face poses and audio keyframes change state only
        foreach (var k in ordered.Where(k => TrackOrder(k) <= 3)) BufferKeyframe(k, owner);

        // (6) the face animation: one pre-rendered frame per frame until its frames run out
        FaceBitmap? face = null;
        bool faceAnimAtEnd;
        lock (_gate)
        {
            if (_faceAnim is { } anim)
            {
                if (_faceAnimFrame < anim.Count) face = anim[_faceAnimFrame++];
                if (_faceAnimFrame >= anim.Count) _faceAnim = null;
            }
            faceAnimAtEnd = _faceAnim is null && !PendingFaceAnimationLocked(clip);
        }
        if (face is not null)
        {
            _lastFace = face;
            var shown = face;
            Buffer(owner, StreamSizes.Face(shown), false, () => _sink.Face(shown));
        }
        else if (faceAnimAtEnd)
        {
            // (7) the procedural face
            FaceKeyframe? current = null, next = null;
            lock (_gate)
            {
                if (_faceIndex >= 0 && _faceIndex < _facePoses.Count)
                {
                    current = _facePoses[_faceIndex];
                    if (_faceIndex + 1 < _facePoses.Count) next = _facePoses[_faceIndex + 1];
                }
            }
            FaceBitmap? procedural;
            if (current is not null)
            {
                // A face keyframe is a pose to be AT when its trigger time arrives, so the pose held now is
                // interpolated forward towards the next one.
                ProceduralFacePose pose;
                if (next is null || next.TriggerTimeMs <= current.TriggerTimeMs) pose = current.Pose;
                else
                {
                    float span = next.TriggerTimeMs - current.TriggerTimeMs;
                    float kk = (float)((t - current.TriggerTimeMs) / span);
                    pose = current.Pose.BlendTo(next.Pose, Math.Clamp(kk, 0f, 1f));
                }
                procedural = ProceduralFaceRenderer.Render(pose);
            }
            else
                // Hold the last face rather than blanking: the robot keeps showing the last image it was given.
                procedural = _lastFace;
            if (procedural is not null)
            {
                _lastFace = procedural;
                var shown = procedural;
                Buffer(owner, StreamSizes.Face(shown), false, () => _sink.Face(shown));
            }
        }

        // (8) lights, (9) body
        foreach (var k in ordered.Where(k => TrackOrder(k) is 4 or 5)) BufferKeyframe(k, owner);
        // A body keyframe drives the wheels for its own duration and no longer (this stack's stop, at the body slot).
        bool stopBody = false;
        lock (_gate)
            if (_bodyEndsAtMs is { } end && t >= end) { _bodyEndsAtMs = null; stopBody = true; }
        if (stopBody) Buffer(owner, StreamSizes.BodyStop, false, _sink.BodyStop);
        // (10) record heading, (11) turn to recorded heading, and anything else
        foreach (var k in ordered.Where(k => TrackOrder(k) >= 6)) BufferKeyframe(k, owner);

        lock (_gate) _framesLeft = !(t >= clip.DurationMs && _nextFrame >= clip.Keyframes.Count);
    }

    /// <summary>A17: a faceAnimations keyframe not yet reached holds the procedural face back until the track is exhausted.</summary>
    private bool PendingFaceAnimationLocked(AnimationClip clip)
    {
        for (int i = _nextFrame; i < clip.Keyframes.Count; i++)
            if (clip.Keyframes[i] is FaceAnimationKeyframe) return true;
        return false;
    }

    /// <summary>
    /// Begins streaming the sound an audio keyframe asks for.
    ///
    /// A keyframe can name several alternatives, and the engine picks <b>one</b> of them by probability:
    /// <c>RobotAudioKeyFrame::GetAudioRef()</c> at 0x004F9E18 calls <c>GetAudioRefIndex(true)</c>, which
    /// draws <c>RandDbl(1.0)</c> and walks the references' probabilities cumulatively until the draw falls
    /// inside one (0x004F9AEC; see <see cref="ChooseAlternative"/>). Choosing the first alternative that
    /// happened to decode, as this did before, made every clip play the same alternative every time.
    ///
    /// The draw is the whole of it. The engine hands the chosen event to Wwise and, if nothing comes
    /// back, plays nothing - there is no second draw and no walk down the remaining alternatives. This
    /// stack used to try the rest in order, which turned a decoder gap of its own (M6-003) into a
    /// different alternative being heard than the app would have played. With no source, or nothing
    /// produced for the chosen event, the track stays silent and the timeline is unchanged.
    /// </summary>
    private void StartAudio(AudioKeyframe k)
    {
        var source = AudioSource;
        if (source is null || k.EventIds.Length == 0) return;

        int chosen = ChooseAlternative(k.EventIds.Length, k.Probabilities, _random.NextDouble());
        if (chosen < 0) return;

        {
            long id = k.EventIds[chosen];
            // A Stop action is not a silent alternative: Wwise stops the target's playing voices. The songs
            // end this way (Stop__Robot_VO__Cozmo_Singing_Stop at the tempo clip's end); before this the
            // event returned null, was skipped, and a 462 s song kept streaming past the animation.
            if (source.IsStopEvent(id))
            {
                bool stopped = false;
                lock (_gate)
                {
                    if (_audioPcm is not null && (_audioEventId is not { } playing || source.StopAffects(id, playing)))
                    {
                        _audioPcm = null; _audioPos = 0; _audioEventId = null; stopped = true;
                    }
                }
                if (stopped) AudioStops++;
                return;
            }
            var pcm = source.GetPcm(id, k.Volume);
            if (pcm is null || pcm.Length == 0) return;
            lock (_gate) { _audioPcm = pcm; _audioPos = 0; _audioEventId = id; }
            return;
        }
    }

    /// <summary>Whether a sound is streaming right now (audio frames carry samples rather than silence).</summary>
    public bool AudioStreaming { get { lock (_gate) return _audioPcm is not null; } }

    /// <summary>How many times a Stop event ended a streaming sound in the current or last animation.</summary>
    public int AudioStops { get; private set; }

    /// <summary>
    /// The engine's alternative selection, from <c>RobotAudioKeyFrame::SetMembersFromFlatBuf</c> at
    /// 0x004F9E54 and <c>GetAudioRefIndex(bool)</c> at 0x004F9AEC.
    ///
    /// Loading: when the clip carries as many probabilities as event ids they are used as given; otherwise
    /// every alternative gets <c>1 / n</c>. Selecting: with <paramref name="draw"/> uniform in [0, 1),
    /// probabilities below 1e-5 in magnitude are skipped, and the first alternative whose cumulative range
    /// <c>[acc, acc + p]</c> contains the draw is chosen. When no range contains it, which happens when the
    /// probabilities sum to less than one, the engine returns index -1 and plays nothing; this returns -1
    /// likewise. The engine's branch for probabilities summing above one (0x004F9FFA) was not traced, so
    /// such a clip is treated as carrying no usable probabilities and falls back to <c>1 / n</c>.
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

    /// <summary>
    /// The keyframe's variability, applied as <c>HeadAngleKeyFrame::GetStreamMessage</c> and
    /// <c>LiftHeightKeyFrame::GetStreamMessage</c> apply it at stream time: <c>RandIntInRange(value - var,
    /// value + var)</c> when the variability is non-zero, the value itself otherwise.
    /// </summary>
    /// <summary>
    /// Where the frames of a <c>faceAnimations</c> keyframe come from, by animation name.
    /// <see cref="FaceAnimationLibrary.Frames"/> is the one that reads the shipped assets; left unset, a
    /// face animation keyframe puts nothing on the screen, which is what this stack did before.
    /// </summary>
    public Func<string, IReadOnlyList<FaceBitmap>?>? FaceAnimations { get; set; }

    /// <summary>The face animation playing now, if any, and how far into it the stream has reached.</summary>
    public (string Name, int Frame, int Count)? FaceAnimation
    {
        get
        {
            lock (_gate)
                return _faceAnim is null ? null : (_faceAnimName, _faceAnimFrame, _faceAnim.Count);
        }
    }

    private string _faceAnimName = "";

    /// <summary>
    /// <c>FaceAnimationKeyFrame::GetStreamMessage</c> 0x004F97C8 asks the FaceAnimationManager for
    /// <c>GetFrame(name, index)</c>, sends it as a face image and moves the index on; <c>IsDone</c>
    /// 0x004F976C is true once the index has reached <c>GetNumFrames(name)</c>. So the track plays one
    /// pre-rendered frame per streaming tick and then stops, which is what starting one here sets up. A
    /// name the library does not know logs nothing and plays nothing, as the engine's error path does.
    /// </summary>
    private void StartFaceAnimation(FaceAnimationKeyframe k)
    {
        var frames = FaceAnimations?.Invoke(k.AnimName);
        lock (_gate)
        {
            _faceAnim = frames is { Count: > 0 } ? frames : null;
            _faceAnimName = k.AnimName;
            _faceAnimFrame = 0;
        }
    }

    private int WithVariability(int value, int variability) =>
        variability == 0 ? value : _random.Next(value - variability, value + variability + 1);

    private int IndexOfFace(FaceKeyframe f)
    {
        for (int i = 0; i < _facePoses.Count; i++) if (ReferenceEquals(_facePoses[i], f)) return i;
        return _faceIndex;
    }

    /// <summary>
    /// Where a keyframe sits in the engine's per-frame order (A16, 0x0057C94E..0x0057CA7A): head, lift, event, then
    /// the face steps (face animation, procedural face), backpack lights, body, record heading, turn to recorded
    /// heading. The audio message and StartOfAnimation go before all of them.
    /// </summary>
    private static int TrackOrder(Keyframe k) => k switch
    {
        HeadKeyframe => 0,
        LiftKeyframe => 1,
        EventKeyframe => 2,
        FaceKeyframe or FaceAnimationKeyframe or AudioKeyframe => 3,   // state changes before the face steps (A16 (6), (7))
        LightsKeyframe => 4,
        BodyKeyframe => 5,
        RecordHeadingKeyframe => 6,
        TurnToRecordedHeadingKeyframe => 7,
        _ => 8,
    };

    /// <summary>
    /// Buffers what one keyframe sends, its values decided now, at build time, as <c>GetStreamMessage</c> decides
    /// them (variability included). State a keyframe changes (a sound starting, a face animation, a body deadline)
    /// changes now; the message and <see cref="KeyframeFired"/> go when the drain reaches it.
    /// </summary>
    private void BufferKeyframe(Keyframe k, object owner)
    {
        switch (k)
        {
            case HeadKeyframe h:
            {
                var deg = (sbyte)Math.Clamp(WithVariability(h.AngleDeg, h.VariabilityDeg), sbyte.MinValue, sbyte.MaxValue);
                Buffer(owner, StreamSizes.HeadAngle, false, () => { _sink.Head(deg, h.DurationTimeMs); KeyframeFired?.Invoke(k); });
                return;
            }
            case LiftKeyframe l:
            {
                var mm = (byte)Math.Clamp(WithVariability(l.HeightMm, l.VariabilityMm), byte.MinValue, byte.MaxValue);
                Buffer(owner, StreamSizes.LiftHeight, false, () => { _sink.Lift(mm, l.DurationTimeMs); KeyframeFired?.Invoke(k); });
                return;
            }
            case BodyKeyframe b:
                // Only a keyframe that actually moves the body needs stopping. Any radius the engine
                // understands now runs, arcs included, because the robot does the geometry.
                lock (_gate)
                    _bodyEndsAtMs = b.RadiusIsKnown && b.DurationTimeMs > 0 && b.Speed != 0
                        ? b.TriggerTimeMs + b.DurationTimeMs
                        : null;
                Buffer(owner, StreamSizes.Body(b), false, () => { _sink.Body(b); KeyframeFired?.Invoke(k); });
                return;
            case LightsKeyframe li: BufferLocal(owner, () => { _sink.Lights(li); KeyframeFired?.Invoke(k); }); return;
            case EventKeyframe e: BufferLocal(owner, () => { _sink.Event(e.EventId); KeyframeFired?.Invoke(k); }); return;
            case AudioKeyframe a: StartAudio(a); break;
            case FaceAnimationKeyframe fa: StartFaceAnimation(fa); break;
        }
        // keyframes that send no message of their own (face poses, audio, face animations, headings)
        BufferLocal(owner, () => KeyframeFired?.Invoke(k));
    }
}

/// <summary>
/// <c>EngineToRobot::Size()</c> of each stream message the scheduler buffers for its sink (C11): the serialised
/// length of the message <see cref="RobotAnimationSink"/> sends for that call. Calls that send no robot message
/// (lights, events) count 0.
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

    /// <summary>A body keyframe whose radius the engine does not understand sends nothing.</summary>
    public static int Body(BodyKeyframe b) => b.EncodedRadius is null ? 0 : BodyStop;

    public static int Face(FaceBitmap f) => StreamSendBuffer.SizeOf(new Protocol.FaceImage { Image = FaceBitmapCodec.Encode(f) });
}
