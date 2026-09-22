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
    /// How many audio frames the robot reports having played, from <c>animState.numAudioFramesPlayed</c>.
    ///
    /// The engine paces an animation against this exact counter: UpdateAmountToSend at 0x0057C6F0 computes
    /// the frames it may still send as <c>14 - (streamed - played)</c>, and ShouldProcessAnimationFrame at
    /// 0x0057CC6C refuses to process a frame at all until the robot has room. Null means no robot is
    /// reporting yet, and the scheduler then runs unpaced.
    /// </summary>
    int? AudioFramesPlayed => null;
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
/// Every timed thing an animation does is scheduled here, on a single 30 Hz tick, so the face, the audio,
/// the motors and the lights stay on one timeline. The device classes underneath keep their own APIs but do
/// not invent their own animation timing: the scheduler calls them, not the other way round.
///
/// One animation streams at a time, as in the engine: <c>AnimationStreamer</c> holds a single streaming
/// animation (this+0x38), and <c>SetStreamingAnimation</c> at 0x0057B174 either interrupts it or turns the
/// newcomer away (see <see cref="Play"/>). Two animations never run side by side, whatever tracks they use.
///
/// Time on the timeline is counted in streamed frames of 33 ms, as the engine counts it (see
/// <see cref="Advance"/>); the wall clock only decides how many frames a tick is owed. A live robot gets a
/// thread calling <see cref="Advance"/> every <see cref="FrameInterval"/>; a test calls it directly with
/// whatever times it likes, which is what makes the timeline deterministic and testable.
/// </summary>
public sealed class AnimationScheduler
{
    /// <summary>The engine's animation tick: 30 frames per second.</summary>
    public const int FrameRateHz = 30;
    /// <summary>The wall-clock spacing of frames: one robot audio frame, 744 samples at 22320 Hz.</summary>
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
    /// A keyframe belongs to one playback. Checking that the playback still owns the timeline and then
    /// emitting its command are two operations, and between them another thread can cancel or replace the
    /// animation - so a re-check before the send narrows the window without closing it, and the command
    /// goes out into somebody else's animation. On hardware that looks like one animation's motion
    /// appearing in the middle of another.
    ///
    /// The contract is: <b>a command belonging to a playback may only be emitted while this gate is held
    /// and the generation still matches, and anything that changes ownership takes this gate before it
    /// bumps the generation.</b> An emission and a replacement therefore cannot interleave. The gate is
    /// held across a send, which is safe because a send only queues a message under the transport's own
    /// lock - it does no network work - so the wait a canceller can see is bounded by one keyframe.
    ///
    /// The lock order is always this gate and then <see cref="_gate"/>, never the other way about.
    /// </summary>
    private readonly object _emit = new();

    private AnimationClip? _clip;
    private AnimationHandle? _handle;
    private int _framesStreamed;               // frames sent for the running clip; the timeline is this x 33 ms
    private double _nextDueWallMs;             // wall time the next frame is due; falls behind during a stall, so the debt is known
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
    private bool _startSent;                   // has StartOfAnimation gone out for the running clip
    private long _generation;                  // bumped whenever the running animation changes
    private int _playedBaseline;               // robot's audio-frame count when the clip started
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
    /// Whether <see cref="Advance"/> still has something to do: a clip is running, or a live keyframe has
    /// armed a deadline that only <see cref="Advance"/> can serve. The live animation is not on a clip
    /// timeline, so <see cref="IsPlaying"/> is false while a keep-alive body shuffle is still driving the
    /// wheels - and a caller that stops ticking then leaves the robot moving.
    /// </summary>
    public bool HasPendingWork { get { lock (_gate) return _clip is not null || _liveBodyStopsAtMs is not null; } }

    /// <summary>
    /// Whether the last live keyframe armed a deadline <see cref="Advance"/> still has to serve. The
    /// caller that streams live keyframes uses this to decide whether a tick loop is needed.
    /// </summary>
    public bool LiveBodyRunning { get { lock (_gate) return _liveBodyStopsAtMs is not null; } }
    /// <summary>Tracks currently claimed by the running animation.</summary>
    public AnimationTrack OwnedTracks { get { lock (_gate) return _clip?.Tracks ?? AnimationTrack.None; } }
    /// <summary>
    /// The timeline position of the last streamed frame, in milliseconds: frames streamed times
    /// <see cref="FrameStepMs"/>, which is the engine's stream time, not elapsed wall time.
    /// </summary>
    public double PositionMs { get; private set; }
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

    /// <summary>Raised for every keyframe as it fires, for logging and tests.</summary>
    public event Action<Keyframe>? KeyframeFired;

    /// <summary>
    /// Starts a clip.
    ///
    /// The engine streams exactly one animation at a time. <c>AnimationStreamer::SetStreamingAnimation</c>
    /// at 0x0057B174 keeps a single streaming animation, and when one is already streaming a newcomer
    /// either interrupts it (its <c>interruptRunning</c> flag: "Animation %s is interrupting animation %s",
    /// then <c>Abort()</c> and <c>InitStream</c>) or is turned away whatever tracks it uses ("Already
    /// streaming %s, will not interrupt with %s", nothing changes). Nothing in the engine runs two
    /// animations side by side on disjoint tracks: blinks, eye shifts and squints ride on the streaming
    /// animation as layers (<c>TrackLayerComponent</c>), the idle animation streams only while nothing else
    /// does, and the track locks in <c>MovementComponent</c> mute tracks of the one animation rather than
    /// share them out.
    ///
    /// <paramref name="replaceRunning"/> is the engine's <c>interruptRunning</c>. With it the running
    /// animation ends as <see cref="AnimationEndReason.Replaced"/> and this one starts. Without it the clip
    /// is refused and null is returned whenever anything is running, even on tracks the running clip does
    /// not touch, so a caller can tell "did not play" from "played and finished". An earlier version
    /// refused only on a track clash and otherwise replaced the running clip anyway, which neither mode of
    /// the engine does.
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
            _playedBaseline = _sink.AudioFramesPlayed ?? 0;
            _framesStreamed = 0;
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
        // StartOfAnimation is deliberately not sent here. The engine buffers it inside UpdateStream, on the
        // first frame that actually streams and after that frame's audio message (0x0057C9C8), never at the
        // moment the animation is set up. Advance does the same.
    }

    /// <summary>
    /// Streams one keyframe of the engine's <b>live animation</b> — the keep-alive clip the streamer
    /// always has open.
    ///
    /// <c>AnimationStreamer</c> constructs an <c>Animation</c> of its own at this+0xA8, marks it live
    /// (<c>SetIsLive(true)</c> at 0x0057A060) and streams it whenever no real animation is playing;
    /// <c>UpdateLiveAnimation</c> at 0x0057D5F8 appends head, lift and body keyframes to it as their
    /// timers expire. The keyframes that come out are the ordinary 0x93 / 0x94 / 0x99 stream messages,
    /// with the same stream-time variability draw — which is why this goes through the same
    /// <see cref="Dispatch"/> arithmetic rather than through the motion API.
    ///
    /// Returns false when a running clip owns the keyframe's track, which is the engine's own condition:
    /// the streamer only reaches the live animation when nothing else is streaming.
    /// </summary>
    public bool StreamLive(Keyframe k, double nowMs)
    {
        lock (_gate)
            if (_clip is not null && (_clip.Tracks & k.Track) != AnimationTrack.None) return false;

        switch (k)
        {
            case HeadKeyframe h:
                _sink.Head((sbyte)Math.Clamp(WithVariability(h.AngleDeg, h.VariabilityDeg),
                                             sbyte.MinValue, sbyte.MaxValue), h.DurationTimeMs);
                return true;
            case LiftKeyframe l:
                _sink.Lift((byte)Math.Clamp(WithVariability(l.HeightMm, l.VariabilityMm),
                                            byte.MinValue, byte.MaxValue), l.DurationTimeMs);
                return true;
            case BodyKeyframe b:
                _sink.Body(b);
                lock (_gate)
                    // The deadline is in the clock the caller passes, which has to be the same clock
                    // Advance is driven on - see CozmoAnimations.StreamLive, which supplies both.
                    _liveBodyStopsAtMs = b.RadiusIsKnown && b.DurationTimeMs > 0 && b.Speed != 0
                        ? nowMs + b.DurationTimeMs
                        : null;
                return true;
            default:
                return false;
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

    private void EndLocked(AnimationEndReason reason)
    {
        var name = _clip?.Name ?? "";
        _clip = null;
        _generation++;
        var h = _handle; _handle = null;
        _facePoses = Array.Empty<FaceKeyframe>();
        _faceIndex = -1;
        bool bodyWasRunning = _bodyEndsAtMs is not null;
        _bodyEndsAtMs = null;
        _audioPcm = null; _audioPos = 0; _audioEventId = null;
        CurrentTag = 0;
        bool wasOpen = _startSent;
        _startSent = false;
        // The sink talks to the robot, and by the time an animation is being ended the robot may be gone -
        // a disconnect is exactly when something is cut short. None of these may stop the handle
        // completing: a caller awaiting Play must not be left waiting forever because the link dropped.
        try
        {
            // An animation that is cut short must not leave the wheels turning.
            if (bodyWasRunning) _sink.BodyStop();
            // Only close an animation that was actually opened; a clip stopped before its first streamed
            // frame never sent a StartOfAnimation, and an unmatched EndOfAnimation would close someone
            // else's.
            if (wasOpen)
            {
                _sink.AnimationEnded();
                // UpdateStream buffers one more AudioSilence straight after SendEndOfAnimation
                // (0x0057CB92), so the robot's audio buffer is left with a frame rather than running dry
                // on the last sample.
                _sink.Audio(null);
            }
            _sink.Finished(name, reason == AnimationEndReason.Completed);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // the robot went away mid-animation; there is nothing left to tell it
        }
        h?.Complete(reason);
    }

    /// <summary>
    /// Advances the timeline: streams the frames this tick is owed, firing every keyframe each frame
    /// reaches and updating the face blend.
    ///
    /// Animation time is a count of streamed frames, as in the engine. <c>AnimationStreamer::UpdateStream</c>
    /// at 0x0057C84C hands one stream time (this+0x84) to <c>GetAudioToSend</c>, to the layer component and
    /// to every track's <c>GetCurrentStreamingMessage</c>, and adds 33 to it only after the frame's messages
    /// have been sent (0x0057CA94..0x0057CA9C). <c>ShouldProcessAnimationFrame</c> at 0x0057CC6C ends the
    /// frame loop, leaving the stream time where it is, while the send buffer still holds messages
    /// (this+0x94) or the audio client reports no room. So while the robot has no room the animation does
    /// not move at all, and when room returns the engine streams the frames it owes one 33 ms step at a
    /// time, each with its own audio frame and its own keyframes, up to the audio budget. Wall-clock time
    /// never enters the stream time; <c>AnimationStreamer::Update</c> at 0x0057CE5C reads the clock only
    /// for the idle keep-alive timers.
    ///
    /// Before this the timeline was <c>nowMs - startMs</c>. A stall let wall time run on, and the first
    /// frame after it jumped forward, firing every keyframe that had come due in one frame with one audio
    /// frame, while the audio position, which had always advanced per frame, fell behind the keyframes.
    ///
    /// How many frames one call streams is this stack's choice, not the engine's: normally one, because the
    /// live thread calls this every <see cref="FrameInterval"/>. Frames that could not go, because the tick
    /// came late or the robot had no room, stay owed, and the next call that can stream makes them up one
    /// frame at a time, never more than a robot buffer's worth in one call. The engine streams to the
    /// budget on every update regardless of the clock, which after a stall comes to the same burst. Each
    /// frame is checked against the robot's audio budget before it goes, whichever way it was owed.
    /// </summary>
    public void Advance(double nowMs)
    {
        // A keep-alive body keyframe has to be stopped whether or not a clip is running, because
        // DriveWheels runs until countermanded and the live animation is not on the clip timeline.
        bool stopLiveBody = false;
        lock (_gate)
            if (_liveBodyStopsAtMs is { } end && nowMs >= end)
            {
                _liveBodyStopsAtMs = null;
                stopLiveBody = true;
            }
        if (stopLiveBody) _sink.BodyStop();

        double interval = FrameInterval.TotalMilliseconds;
        double maxDebt = CozmoAudio.RobotBufferFrames * interval;
        int frames;
        lock (_gate)
        {
            if (_clip is null) return;
            double late = nowMs - _nextDueWallMs;
            if (late < 0) { _nextDueWallMs = nowMs; late = 0; }                 // ahead of schedule: this tick is the frame
            frames = Math.Min(1 + (int)Math.Floor(Math.Min(late, maxDebt) / interval), CozmoAudio.RobotBufferFrames);
        }
        for (int i = 0; i < frames; i++)
        {
            if (!StreamFrame()) break;
            lock (_gate) { _nextDueWallMs += interval; }
        }
        lock (_gate)
        {
            // A stall that outlasts the robot's whole buffer is not owed more than that buffer.
            if (nowMs - _nextDueWallMs > maxDebt) _nextDueWallMs = nowMs - maxDebt;
        }
    }

    /// <summary>
    /// Streams one frame of the running animation at the timeline position the frame count gives. Returns
    /// false when this call can stream nothing more: nothing running, no room in the robot's audio buffer,
    /// the animation replaced under us, or the clip completed on this frame.
    /// </summary>
    private bool StreamFrame()
    {
        AnimationClip clip;
        double t;
        long generation;
        lock (_gate)
        {
            if (_clip is null) return false;
            generation = _generation;
            // ShouldProcessAnimationFrame at 0x0057CC6C refuses to process a frame until the robot has
            // room, and UpdateAmountToSend at 0x0057C6F0 measures that room as
            // 14 - (audioFramesStreamed - audioFramesPlayed). Streaming past it would only pile up frames
            // the robot drops, and the timeline would drift away from what the robot is actually playing.
            if (_sink.AudioFramesPlayed is { } played &&
                AudioFramesSent - (played - _playedBaseline) >= CozmoAudio.RobotBufferFrames)
                return false;
            clip = _clip;
            t = _framesStreamed * (double)FrameStepMs;
            PositionMs = t;
        }

        // Exactly one audio message goes out on every streamed frame: a sample when the clip has sound at
        // this moment, animAudioSilence when it does not. UpdateStream buffers one or the other with no way
        // past (0x0057C992 and 0x0057C9AE), and SendBufferedMessages counts 0x8E and 0x8F alike against the
        // robot's audio budget with (tag & 0xFE) == 0x8E. The silence frames are what carry an animation
        // forward: a clip streamed without them opens on the robot and then never advances, which is why
        // body motion did nothing and the face stopped appearing once we started bracketing.
        byte[]? frame = null;
        lock (_gate)
        {
            if (_generation != generation) return false;
            if (_audioPcm is { } pcm && _audioPos < pcm.Length)
            {
                int n = Math.Min(CozmoAudio.SamplesPerFrame, pcm.Length - _audioPos);
                var samples = new byte[CozmoAudio.SamplesPerFrame];
                for (int i = 0; i < n; i++) samples[i] = AnkiMuLaw.Encode(pcm[_audioPos + i]);
                for (int i = n; i < CozmoAudio.SamplesPerFrame; i++) samples[i] = AnkiMuLaw.Encode(0);
                _audioPos += n;
                if (_audioPos >= pcm.Length) { _audioPcm = null; _audioPos = 0; _audioEventId = null; }
                frame = samples;
            }
        }
        _sink.Audio(frame);
        AudioFramesSent++;
        // The frame has gone, so the timeline moves one step, as UpdateStream adds 33 after SendBufferedMessages.
        lock (_gate) { if (_generation == generation) _framesStreamed++; }

        // The animation is opened here rather than in Play, on the first frame that streams and after that
        // frame's audio, matching the guarded SendStartOfAnimation at 0x0057C9C8.
        lock (_emit)
        {
            byte openWith = 0;
            lock (_gate)
            {
                if (_generation == generation && !_startSent) { _startSent = true; openWith = CurrentTag; }
            }
            if (openWith != 0) _sink.AnimationStarted(openWith);
        }

        var due = new List<Keyframe>();
        lock (_gate)
        {
            if (_generation != generation) return false;              // replaced while we were looking
            while (_nextFrame < clip.Keyframes.Count && clip.Keyframes[_nextFrame].TriggerTimeMs <= t)
            {
                var k = clip.Keyframes[_nextFrame++];
                due.Add(k);
                if (k is FaceKeyframe f) _faceIndex = IndexOfFace(f);
            }
            KeyframesFired += due.Count;
        }

        // Emitted in the engine's per-frame track order rather than the order the clip happens to list
        // them: head, lift, event, face, lights, body (UpdateStream 0x0057C9D4 onwards).
        //
        // Dispatch has to happen outside the lock, because it talks to the transport. That leaves a window
        // in which the animation can be replaced, so the generation is re-checked before every keyframe:
        // without it, keyframes collected for the outgoing clip were emitted into the incoming one, which
        // on hardware looks like one animation's motion appearing in the middle of another.
        foreach (var k in due.OrderBy(TrackOrder))
        {
            // the emission contract: owned and sent as one step, so a replacement cannot land between
            lock (_emit)
            {
                lock (_gate) { if (_generation != generation) return false; }
                Dispatch(k);
            }
            KeyframeFired?.Invoke(k);
        }

        // A body keyframe drives the wheels for its own duration and no longer. DriveWheels runs until
        // countermanded, so without this the wheels keep turning until the whole animation ends, which on
        // anim_bored_01 meant 800 ms of backward travel where the asset asked for 264 ms.
        lock (_emit)
        {
            bool stopBody = false;
            lock (_gate)
            {
                if (_generation == generation && _bodyEndsAtMs is { } end && t >= end)
                {
                    _bodyEndsAtMs = null;
                    stopBody = true;
                }
            }
            if (stopBody) _sink.BodyStop();
        }

        // The face is continuous rather than stepped. A face keyframe is a pose to be AT when its trigger
        // time arrives, so the pose held now is interpolated forward towards the next one, not backwards
        // from the previous one. Interpolating backwards would leave the face frozen on one keyframe until
        // the next fired and then snap, which is a step, not an animation.
        // A pre-rendered face animation owns the screen while it lasts: it puts one frame out per
        // streaming tick and stops when its frames run out, the way FaceAnimationKeyFrame::IsDone ends the
        // track. Nothing in the shipped assets runs one over a procedural face track.
        FaceBitmap? animFrame = null;
        lock (_gate)
        {
            if (_faceAnim is { } anim)
            {
                if (_faceAnimFrame < anim.Count) animFrame = anim[_faceAnimFrame++];
                if (_faceAnimFrame >= anim.Count) _faceAnim = null;
            }
        }
        if (animFrame is not null)
        {
            _lastFace = animFrame;
            _sink.Face(animFrame);
            lock (_gate)
            {
                if (_clip == clip && t >= clip.DurationMs && _nextFrame >= clip.Keyframes.Count)
                {
                    EndLocked(AnimationEndReason.Completed);
                    return false;
                }
                return _clip == clip && _generation == generation;
            }
        }

        FaceKeyframe? current = null, next = null;
        lock (_gate)
        {
            if (_faceIndex >= 0 && _faceIndex < _facePoses.Count)
            {
                current = _facePoses[_faceIndex];
                if (_faceIndex + 1 < _facePoses.Count) next = _facePoses[_faceIndex + 1];
            }
        }
        if (current is not null)
        {
            ProceduralFacePose pose;
            if (next is null || next.TriggerTimeMs <= current.TriggerTimeMs) pose = current.Pose;
            else
            {
                float span = next.TriggerTimeMs - current.TriggerTimeMs;
                float k = (float)((t - current.TriggerTimeMs) / span);
                pose = current.Pose.BlendTo(next.Pose, Math.Clamp(k, 0f, 1f));
            }
            var bmp = ProceduralFaceRenderer.Render(pose);
            _lastFace = bmp;
            _sink.Face(bmp);
        }
        else if (_lastFace is not null)
        {
            // Hold the last face rather than blanking. Nothing in the engine clears the screen when an
            // animation ends: SendEndOfAnimation 0x0057C448 sends the EndOfAnimation keyframe and nothing
            // else, and the robot keeps showing the last image it was given until the next one arrives -
            // which, between animations, is the keep-alive's.
            _sink.Face(_lastFace);
        }

        lock (_gate)
        {
            if (_clip == clip && t >= clip.DurationMs && _nextFrame >= clip.Keyframes.Count)
            {
                EndLocked(AnimationEndReason.Completed);
                return false;
            }
            return _clip == clip && _generation == generation;
        }
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
    /// Where a keyframe sits in the engine's per-frame order. UpdateStream buffers its tracks in a fixed
    /// sequence from 0x0057C9D4: head, lift, event, face, backpack lights, body motion, record heading,
    /// turn to recorded heading. Audio is emitted before all of them and is not a keyframe here.
    /// </summary>
    private static int TrackOrder(Keyframe k) => k switch
    {
        HeadKeyframe => 0,
        LiftKeyframe => 1,
        EventKeyframe => 2,
        FaceKeyframe => 3,
        LightsKeyframe => 4,
        BodyKeyframe => 5,
        _ => 6,
    };

    private void Dispatch(Keyframe k)
    {
        switch (k)
        {
            case HeadKeyframe h:
                _sink.Head((sbyte)Math.Clamp(WithVariability(h.AngleDeg, h.VariabilityDeg), sbyte.MinValue, sbyte.MaxValue),
                           h.DurationTimeMs);
                break;
            case LiftKeyframe l:
                _sink.Lift((byte)Math.Clamp(WithVariability(l.HeightMm, l.VariabilityMm), byte.MinValue, byte.MaxValue),
                           l.DurationTimeMs);
                break;
            case BodyKeyframe b:
                _sink.Body(b);
                // Only a keyframe that actually moves the body needs stopping. Any radius the engine
                // understands now runs, arcs included, because the robot does the geometry.
                lock (_gate)
                    _bodyEndsAtMs = b.RadiusIsKnown && b.DurationTimeMs > 0 && b.Speed != 0
                        ? b.TriggerTimeMs + b.DurationTimeMs
                        : null;
                break;
            case LightsKeyframe li: _sink.Lights(li); break;
            case EventKeyframe e: _sink.Event(e.EventId); break;
            case AudioKeyframe a: StartAudio(a); break;
            case FaceKeyframe: break;                             // handled by the blend above
            case FaceAnimationKeyframe fa: StartFaceAnimation(fa); break;
            case RecordHeadingKeyframe: break;
            case TurnToRecordedHeadingKeyframe: break;
        }
    }
}
