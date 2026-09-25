using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The wire lifecycle of the live animation (M7-017) and of a cancelled animation (M5-023), against
/// <c>AnimationStreamer</c> in libcozmoEngine.so as the frozen M5 inventory reads it (A5, A12, A13, A20, A24, A29).
/// </summary>
public class AnimationStreamLifecycleTests
{
    private sealed class LogSink : IAnimationSink
    {
        public readonly List<string> Log = new();
        public void Face(FaceBitmap bitmap) { }
        public void Audio(byte[]? mulawFrame) => Log.Add(mulawFrame is null ? "silence" : "audio");
        public void Head(sbyte angleDeg, uint durationMs) => Log.Add("head");
        public void Lift(byte heightMm, uint durationMs) => Log.Add("lift");
        public void AnimationStarted(byte tag) => Log.Add($"start:{tag}");
        public void AnimationEnded() => Log.Add("end");
        public void Body(BodyKeyframe keyframe) => Log.Add("body");
        public void BodyStop() => Log.Add("bodystop");
        public void Lights(LightsKeyframe keyframe) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) => Log.Add("finished");
    }

    /// <summary>A clip that stays streaming for 5 s: one head keyframe now and an event pending at 5000 (A19, D2).</summary>
    private static AnimationClip HeadClip() => new()
    {
        Name = "head",
        Keyframes = new List<Keyframe> { new HeadKeyframe(0, 100, 10, 0), new EventKeyframe(5_000, "TAPPED_BLOCK") },
        Tracks = AnimationTrack.Head | AnimationTrack.Event,
        DurationMs = 5_000,
    };

    /// <summary>
    /// UpdateLiveAnimation 0x0057D5F8 only appends keyframes to the live Animation (A29); the next Update re-inits the live
    /// idle (InitStream(live, 0xFF), no frame), and the one after sends the frame as audio, then StartOfAnimation once
    /// (0x0057C9D0), then the body keyframe (0x0057CA56).
    /// </summary>
    [Fact]
    public void ALiveBodyKeyframeGoesOutInsideAnOpenedLiveStream()
    {
        var sink = new LogSink();
        var s = new AnimationScheduler(sink, new Random(1));
        Assert.True(s.StreamLive(new BodyKeyframe(0, 1000, "STRAIGHT", 40), 0));
        Assert.Empty(sink.Log);
        s.Advance(0);
        Assert.Empty(sink.Log);
        s.Advance(33);
        Assert.Equal(new[] { "silence", "start:255", "body" }, sink.Log);

        // a second live keyframe does not reopen the stream
        Assert.True(s.StreamLive(new HeadKeyframe(0, 100, 5, 0), 70));
        s.Advance(120);
        Assert.Single(sink.Log, e => e.StartsWith("start:"));
    }

    /// <summary>
    /// A clip taking over ends the live stream without an EndOfAnimation (InitStream only drops the buffer, A12); the clip
    /// ends with its own End (A20), completes on the next Update (A13), and on that Update the live idle is re-initialised
    /// (+0x64 = 0 after the clip's UpdateStream, A13; InitStream(live, 0xFF), A29) with no frame; the Update after streams
    /// the live keyframe with StartOfAnimation 0xFF.
    /// </summary>
    [Fact]
    public void AClipTakingOverEndsTheLiveStreamWithoutAnEndOfAnimationAndTheLiveStreamReopensAfterIt()
    {
        var sink = new LogSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.StreamLive(new HeadKeyframe(0, 100, 5, 0), 0);
        s.Play(new AnimationClip { Name = "h", Keyframes = new List<Keyframe> { new HeadKeyframe(0, 100, 10, 0) }, Tracks = AnimationTrack.Head }, 0);
        s.Advance(0);
        Assert.Equal(new[] { "silence", "start:1", "head", "end" }, sink.Log);
        sink.Log.Clear();
        s.Advance(34);
        Assert.False(s.IsPlaying);
        Assert.Equal(new[] { "finished" }, sink.Log);
        sink.Log.Clear();
        s.Advance(68);
        Assert.Equal(new[] { "silence", "start:255", "head", "end" }, sink.Log);
    }

    /// <summary>
    /// A29: once the live animation's keyframes are consumed it has ended (endSent, no frames, buffer empty), so every
    /// Update re-inits it (InitStream(live, 0xFF)) and streams nothing until a keyframe is appended.
    /// </summary>
    [Fact]
    public void TheLiveIdleReInitsOnEveryUpdateOnceItsKeyframesAreConsumed()
    {
        var sink = new LogSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.StreamLive(new BodyKeyframe(0, 100, "STRAIGHT", 40), 0);
        for (int i = 0; i <= 10; i++) s.Advance(60 * i);
        Assert.False(s.LiveBodyRunning);
        Assert.True(s.HasPendingWork);
        sink.Log.Clear();
        for (int i = 11; i <= 20; i++) s.Advance(60 * i);
        Assert.Empty(sink.Log);
    }

    /// <summary>Abort 0x0057B3E0 sends no body stop (A24); the keyframe's own stop (C5) never came because it was cancelled first.</summary>
    [Fact]
    public void CancellingAnAnimationWithABodyKeyframeRunningSendsNoBodyStop()
    {
        var sink = new LogSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.Play(new AnimationClip
        {
            Name = "drive",
            Keyframes = new List<Keyframe> { new BodyKeyframe(0, 2000, "STRAIGHT", 40) },
            Tracks = AnimationTrack.Body,
            DurationMs = 5_000,
        }, 0);
        s.Advance(0);
        Assert.Contains("body", sink.Log);
        int before = sink.Log.Count;
        Assert.True(s.Stop());
        Assert.DoesNotContain("bodystop", sink.Log.Skip(before));
    }

    /// <summary>
    /// AnimationStreamer::Abort 0x0057B3E0 sends the robot nothing itself: no EndOfAnimation and no trailing audio (A24).
    /// </summary>
    [Fact]
    public void ACancelledAnimationSendsNoEndOfAnimation()
    {
        var sink = new LogSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.Play(HeadClip(), 0);
        s.Advance(0);
        Assert.Contains("start:1", sink.Log);
        int before = sink.Log.Count;
        Assert.True(s.Stop());
        Assert.Equal(new[] { "finished" }, sink.Log.Skip(before));
    }

    [Fact]
    public void AReplacedAnimationSendsNoEndOfAnimationBeforeTheNewOnesStart()
    {
        var sink = new LogSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.Play(HeadClip(), 0);
        s.Advance(0);
        int before = sink.Log.Count;
        s.Play(HeadClip(), 10);
        s.Advance(10);
        var after = sink.Log.Skip(before).ToList();
        Assert.DoesNotContain("end", after);
        Assert.Contains("start:2", after);
    }
}
