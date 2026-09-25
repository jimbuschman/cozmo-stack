using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The wire lifecycle of the live animation (M7-017) and of a cancelled animation (M5-023), against
/// <c>AnimationStreamer</c> in libcozmoEngine.so.
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

    private static AnimationClip HeadClip(uint durationMs) => new()
    {
        Name = "head",
        Keyframes = new List<Keyframe> { new HeadKeyframe(0, 100, 10, 0) },
        Tracks = AnimationTrack.Head,
        DurationMs = durationMs,
    };

    /// <summary>
    /// UpdateLiveAnimation 0x0057D5F8 only appends keyframes to the live Animation; Update opens it with
    /// InitStream(live, 0xFF) (0x0057D3FE) and UpdateStream 0x0057C84C sends each frame as audio, then
    /// StartOfAnimation once (0x0057C9D0), then the body keyframe (0x0057CA56).
    /// </summary>
    [Fact]
    public void ALiveBodyKeyframeGoesOutInsideAnOpenedLiveStream()
    {
        var sink = new LogSink();
        var s = new AnimationScheduler(sink, new Random(1));
        Assert.True(s.StreamLive(new BodyKeyframe(0, 1000, "STRAIGHT", 40), 0));
        Assert.Equal(new[] { "silence", "start:255", "body" }, sink.Log);

        // a second live keyframe does not reopen the stream
        Assert.True(s.StreamLive(new HeadKeyframe(0, 100, 5, 0), 10));
        Assert.Single(sink.Log, e => e.StartsWith("start:"));

        // each frame of the live stream carries its audio message while the body keyframe runs
        sink.Log.Clear();
        s.Advance(40);
        Assert.Equal(new[] { "silence" }, sink.Log);
    }

    [Fact]
    public void AClipTakingOverEndsTheLiveStreamWithoutAnEndOfAnimationAndTheLiveStreamReopensAfterIt()
    {
        var sink = new LogSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.StreamLive(new HeadKeyframe(0, 100, 5, 0), 0);
        s.Play(HeadClip(33), 0);
        s.Advance(0);
        s.Advance(34);
        Assert.False(s.IsPlaying);
        Assert.DoesNotContain(sink.Log.TakeWhile(e => e != "start:1"), e => e == "end");
        Assert.Contains("start:1", sink.Log);

        // the next update re-inits the live stream (InitStream(live, 0xFF), M3 inventory / M5 A29, 0x0057D3FE) and builds
        // no frame (r7 = 0 at 0x0057D404); its first frame, with StartOfAnimation 0xFF, goes on the update after
        sink.Log.Clear();
        s.Advance(68);
        Assert.Empty(sink.Log);
        s.Advance(101);
        Assert.Equal(new[] { "silence", "start:255" }, sink.Log);
    }

    /// <summary>
    /// Update streams the live animation on every update (UpdateStream(live) at 0x0057D430), not only while a
    /// live keyframe is pending.
    /// </summary>
    [Fact]
    public void TheLiveStreamKeepsStreamingAfterItsKeyframesHaveEnded()
    {
        var sink = new LogSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.StreamLive(new BodyKeyframe(0, 100, "STRAIGHT", 40), 0);
        s.Advance(200);                       // the body keyframe's deadline passes
        Assert.False(s.LiveBodyRunning);
        Assert.True(s.HasPendingWork);
        sink.Log.Clear();
        for (int i = 1; i <= 5; i++) s.Advance(200 + 33 * i);
        Assert.Equal(Enumerable.Repeat("silence", 5), sink.Log);
    }

    /// <summary>Abort 0x0057B3E0 sends no body stop; completion still stops a body keyframe still running.</summary>
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
    /// AnimationStreamer::Abort 0x0057B3E0 sends the robot nothing: no EndOfAnimation and no trailing
    /// audio. Only an animation that completes is closed (SendEndOfAnimation from UpdateStream 0x0057CB80).
    /// </summary>
    [Fact]
    public void ACancelledAnimationSendsNoEndOfAnimation()
    {
        var sink = new LogSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.Play(HeadClip(5_000), 0);
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
        s.Play(HeadClip(5_000), 0);
        s.Advance(0);
        int before = sink.Log.Count;
        s.Play(HeadClip(5_000), 10);
        s.Advance(10);
        var after = sink.Log.Skip(before).ToList();
        Assert.DoesNotContain("end", after);
        Assert.Contains("start:2", after);
    }
}
