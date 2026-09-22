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
    public void AClipTakingOverEndsTheLiveStreamWithoutAnEndOfAnimationAndTheNextLiveKeyframeReopensIt()
    {
        var sink = new LogSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.StreamLive(new HeadKeyframe(0, 100, 5, 0), 0);
        s.Play(HeadClip(33), 0);
        s.Advance(0);
        s.Advance(34);
        s.Advance(68);
        Assert.DoesNotContain(sink.Log.TakeWhile(e => e != "start:1"), e => e == "end");
        Assert.Contains("start:1", sink.Log);

        sink.Log.Clear();
        s.StreamLive(new HeadKeyframe(0, 100, 5, 0), 100);
        Assert.Equal(new[] { "silence", "start:255", "head" }, sink.Log);
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
