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
/// Track ownership is what stops two animations fighting. An animation claims the tracks its keyframes
/// touch for as long as it runs. A second animation that wants an already-claimed track is refused, or
/// replaces the first outright, depending on how it is started; it never interleaves with it.
///
/// The tick is driven by <see cref="Advance"/>, which takes the current time. A live robot gets a thread
/// calling it; a test calls it directly with whatever times it likes, which is what makes the timeline
/// deterministic and testable.
/// </summary>
public sealed class AnimationScheduler
{
    /// <summary>The engine's animation tick: 30 frames per second.</summary>
    public const int FrameRateHz = 30;
    public static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / FrameRateHz);

    private readonly IAnimationSink _sink;
    private readonly object _gate = new();

    private AnimationClip? _clip;
    private AnimationHandle? _handle;
    private double _startMs;
    private int _nextFrame;                    // index of the next keyframe to fire
    private IReadOnlyList<FaceKeyframe> _facePoses = Array.Empty<FaceKeyframe>();
    private int _faceIndex = -1;               // index into _facePoses of the pose currently held
    private FaceBitmap? _lastFace;
    private double? _bodyEndsAtMs;             // when the running body keyframe should stop, if one is running
    private short[]? _audioPcm;                // the sound currently streaming, if any
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
    /// <summary>Tracks currently claimed by the running animation.</summary>
    public AnimationTrack OwnedTracks { get { lock (_gate) return _clip?.Tracks ?? AnimationTrack.None; } }
    /// <summary>How far into the current animation the last tick was, in milliseconds.</summary>
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
    /// With <paramref name="replaceRunning"/> the running animation is cancelled first and this one takes
    /// its tracks. Without it, a clip that needs a track someone else owns is refused and returns null, so a
    /// caller can tell "did not play" from "played and finished".
    /// </summary>
    public AnimationHandle? Play(AnimationClip clip, double nowMs, bool replaceRunning = true)
    {
        lock (_gate)
        {
            if (_clip is not null)
            {
                bool clash = (_clip.Tracks & clip.Tracks) != 0;
                if (clash && !replaceRunning) return null;
                EndLocked(AnimationEndReason.Replaced);
            }
            _clip = clip;
            _generation++;
            _handle = new AnimationHandle(clip.Name, clip.Tracks);
            CurrentTag = _nextTag;
            // IncrementTagCtr at 0x0057B660 keeps incrementing while the value it came from was above
            // 0xFD, so the engine stores neither 0x00 nor 0xFF. The usable range is 1..0xFE.
            _nextTag = _nextTag >= 0xFE ? (byte)1 : (byte)(_nextTag + 1);
            _startSent = false;
            _playedBaseline = _sink.AudioFramesPlayed ?? 0;
            _startMs = nowMs;
            _nextFrame = 0;
            _facePoses = clip.Keyframes.OfType<FaceKeyframe>().ToList();
            _faceIndex = -1;
            _bodyEndsAtMs = null;
            _audioPcm = null; _audioPos = 0;
            AudioFramesSent = 0;
            KeyframesFired = 0;
            PositionMs = 0;
            return _handle;
        }
        // StartOfAnimation is deliberately not sent here. The engine buffers it inside UpdateStream, on the
        // first frame that actually streams and after that frame's audio message (0x0057C9C8), never at the
        // moment the animation is set up. Advance does the same.
    }

    /// <summary>Stops whatever is running. Returns false when nothing was.</summary>
    public bool Stop()
    {
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
        _audioPcm = null; _audioPos = 0;
        CurrentTag = 0;
        bool wasOpen = _startSent;
        _startSent = false;
        // An animation that is cut short must not leave the wheels turning.
        if (bodyWasRunning) _sink.BodyStop();
        // Only close an animation that was actually opened; a clip stopped before its first streamed frame
        // never sent a StartOfAnimation, and an unmatched EndOfAnimation would close someone else's.
        if (wasOpen)
        {
            _sink.AnimationEnded();
            // UpdateStream buffers one more AudioSilence straight after SendEndOfAnimation (0x0057CB92),
            // so the robot's audio buffer is left with a frame rather than running dry on the last sample.
            _sink.Audio(null);
        }
        _sink.Finished(name, reason == AnimationEndReason.Completed);
        h?.Complete(reason);
    }

    /// <summary>
    /// Advances the timeline to this moment, firing every keyframe that is now due and updating the face
    /// blend. Safe to call at any rate; calling it late fires everything that was missed, in order, rather
    /// than skipping it.
    /// </summary>
    public void Advance(double nowMs)
    {
        AnimationClip clip;
        double t;
        long generation;
        lock (_gate)
        {
            if (_clip is null) return;
            generation = _generation;
            // ShouldProcessAnimationFrame at 0x0057CC6C refuses to process a frame until the robot has
            // room, and UpdateAmountToSend at 0x0057C6F0 measures that room as
            // 14 - (audioFramesStreamed - audioFramesPlayed). Streaming past it would only pile up frames
            // the robot drops, and the timeline would drift away from what the robot is actually playing.
            if (_sink.AudioFramesPlayed is { } played &&
                AudioFramesSent - (played - _playedBaseline) >= CozmoAudio.RobotBufferFrames)
                return;
            clip = _clip;
            t = nowMs - _startMs;
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
            if (_generation != generation) return;
            if (_audioPcm is { } pcm && _audioPos < pcm.Length)
            {
                int n = Math.Min(CozmoAudio.SamplesPerFrame, pcm.Length - _audioPos);
                var samples = new byte[CozmoAudio.SamplesPerFrame];
                for (int i = 0; i < n; i++) samples[i] = AnkiMuLaw.Encode(pcm[_audioPos + i]);
                for (int i = n; i < CozmoAudio.SamplesPerFrame; i++) samples[i] = AnkiMuLaw.Encode(0);
                _audioPos += n;
                if (_audioPos >= pcm.Length) { _audioPcm = null; _audioPos = 0; }
                frame = samples;
            }
        }
        _sink.Audio(frame);
        AudioFramesSent++;

        // The animation is opened here rather than in Play, on the first frame that streams and after that
        // frame's audio, matching the guarded SendStartOfAnimation at 0x0057C9C8.
        byte openWith = 0;
        lock (_gate)
        {
            if (_generation == generation && !_startSent) { _startSent = true; openWith = CurrentTag; }
        }
        if (openWith != 0) _sink.AnimationStarted(openWith);

        var due = new List<Keyframe>();
        lock (_gate)
        {
            if (_generation != generation) return;                // replaced while we were looking
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
            lock (_gate) { if (_generation != generation) return; }
            Dispatch(k);
            KeyframeFired?.Invoke(k);
        }

        // A body keyframe drives the wheels for its own duration and no longer. DriveWheels runs until
        // countermanded, so without this the wheels keep turning until the whole animation ends, which on
        // anim_bored_01 meant 800 ms of backward travel where the asset asked for 264 ms.
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

        // The face is continuous rather than stepped. A face keyframe is a pose to be AT when its trigger
        // time arrives, so the pose held now is interpolated forward towards the next one, not backwards
        // from the previous one. Interpolating backwards would leave the face frozen on one keyframe until
        // the next fired and then snap, which is a step, not an animation.
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
            _sink.Face(_lastFace);                                // hold the last face rather than blanking
        }

        lock (_gate)
        {
            if (_clip == clip && t >= clip.DurationMs && _nextFrame >= clip.Keyframes.Count)
                EndLocked(AnimationEndReason.Completed);
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
    /// If the chosen alternative cannot be produced by this source, the remaining ones are tried in
    /// order. That fallback is ours: the engine hands the event to Wwise and plays nothing on failure,
    /// but a decoder gap on our side is not a reason to lose the sound entirely. With no source, or no
    /// alternative available, the track stays silent and the timeline is unchanged.
    /// </summary>
    private void StartAudio(AudioKeyframe k)
    {
        var source = AudioSource;
        if (source is null || k.EventIds.Length == 0) return;

        int chosen = ChooseAlternative(k.EventIds.Length, k.Probabilities, _random.NextDouble());
        var order = new List<long>(k.EventIds.Length);
        if (chosen >= 0) order.Add(k.EventIds[chosen]);
        for (int i = 0; i < k.EventIds.Length; i++) if (i != chosen) order.Add(k.EventIds[i]);

        foreach (var id in order)
        {
            var pcm = source.GetPcm(id, k.Volume);
            if (pcm is null || pcm.Length == 0) continue;
            lock (_gate) { _audioPcm = pcm; _audioPos = 0; }
            return;
        }
    }

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
            case FaceAnimationKeyframe: break;                    // pre-rendered face clips are not loaded yet
            case RecordHeadingKeyframe: break;
            case TurnToRecordedHeadingKeyframe: break;
        }
    }
}
