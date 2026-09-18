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
    void Head(float radians, uint durationMs);
    void Lift(float heightMm, uint durationMs);
    void Body(BodyKeyframe keyframe);
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

    public AnimationScheduler(IAnimationSink sink) => _sink = sink;

    /// <summary>The animation currently running, or null.</summary>
    public string? Playing { get { lock (_gate) return _clip?.Name; } }
    public bool IsPlaying { get { lock (_gate) return _clip is not null; } }
    /// <summary>Tracks currently claimed by the running animation.</summary>
    public AnimationTrack OwnedTracks { get { lock (_gate) return _clip?.Tracks ?? AnimationTrack.None; } }
    /// <summary>How far into the current animation the last tick was, in milliseconds.</summary>
    public double PositionMs { get; private set; }
    /// <summary>Keyframes fired since the current animation started.</summary>
    public int KeyframesFired { get; private set; }

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
            _handle = new AnimationHandle(clip.Name, clip.Tracks);
            _startMs = nowMs;
            _nextFrame = 0;
            _facePoses = clip.Keyframes.OfType<FaceKeyframe>().ToList();
            _faceIndex = -1;
            KeyframesFired = 0;
            PositionMs = 0;
            return _handle;
        }
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
        var h = _handle; _handle = null;
        _facePoses = Array.Empty<FaceKeyframe>();
        _faceIndex = -1;
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
        lock (_gate)
        {
            if (_clip is null) return;
            clip = _clip;
            t = nowMs - _startMs;
            PositionMs = t;
        }

        var due = new List<Keyframe>();
        lock (_gate)
        {
            if (_clip != clip) return;                            // replaced while we were looking
            while (_nextFrame < clip.Keyframes.Count && clip.Keyframes[_nextFrame].TriggerTimeMs <= t)
            {
                var k = clip.Keyframes[_nextFrame++];
                due.Add(k);
                if (k is FaceKeyframe f) _faceIndex = IndexOfFace(f);
            }
            KeyframesFired += due.Count;
        }

        foreach (var k in due)
        {
            Dispatch(k);
            KeyframeFired?.Invoke(k);
        }

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

    private int IndexOfFace(FaceKeyframe f)
    {
        for (int i = 0; i < _facePoses.Count; i++) if (ReferenceEquals(_facePoses[i], f)) return i;
        return _faceIndex;
    }

    private void Dispatch(Keyframe k)
    {
        switch (k)
        {
            case HeadKeyframe h: _sink.Head(h.AngleRad, h.DurationTimeMs); break;
            case LiftKeyframe l: _sink.Lift(l.HeightMm, l.DurationTimeMs); break;
            case BodyKeyframe b: _sink.Body(b); break;
            case LightsKeyframe li: _sink.Lights(li); break;
            case EventKeyframe e: _sink.Event(e.EventId); break;
            case AudioKeyframe: _sink.Audio(null); break;         // the bank is not decoded yet; see below
            case FaceKeyframe: break;                             // handled by the blend above
            case FaceAnimationKeyframe: break;                    // pre-rendered face clips are not loaded yet
            case RecordHeadingKeyframe: break;
            case TurnToRecordedHeadingKeyframe: break;
        }
    }
}
