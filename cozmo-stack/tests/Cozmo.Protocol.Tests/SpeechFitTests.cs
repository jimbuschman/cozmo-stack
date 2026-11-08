using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class SpeechFitTests
{
    private static AnimationClip FaceClip(uint last, float angle = 0) => new()
    {
        Name = "face", Tracks = AnimationTrack.Face,
        Keyframes = new Keyframe[] { new FaceKeyframe(0, new ProceduralFacePose { FaceAngle = angle }),
            new FaceKeyframe(last, new ProceduralFacePose { FaceAngle = angle + 1 }) }
    };

    [Fact]
    public void FitReselectsAtLatestTriggerPlus33AndDoesNotMutateSource()
    {
        var first = FaceClip(990, 1);
        var second = FaceClip(429, 3);
        var choices = new Queue<AnimationClip>(new[] { first, second });
        int calls = 0;
        var fit = SpeechFitBuilder.Build(1024, () => { calls++; return choices.Dequeue(); });
        Assert.False(fit.IsLive);
        Assert.Single(fit.Keyframes.OfType<AudioKeyframe>());
        Assert.Equal(new uint[] { 33, 1023, 1056, 1485 }, fit.Keyframes.OfType<FaceKeyframe>().Select(k => k.TriggerTimeMs));
        Assert.Equal(2, calls);
        Assert.Equal(new uint[] { 0, 990 }, first.Keyframes.Select(k => k.TriggerTimeMs));
        var copied = fit.Keyframes.OfType<FaceKeyframe>().First();
        copied.Pose.FaceAngle = 99;
        Assert.Equal(1f, ((FaceKeyframe)first.Keyframes[0]).Pose.FaceAngle);
        calls = 0;
        var equal = SpeechFitBuilder.Build(1023, () => { calls++; return first; });
        Assert.Equal(1, calls);
        Assert.Equal(1023u, equal.Keyframes.Max(k => k.TriggerTimeMs));
    }

    [Fact]
    public void FitKeepsAudioOnMissingSelectionAndRejectsUnsupportedTracks()
    {
        int calls = 0;
        Assert.Single(SpeechFitBuilder.Build(100, () => { calls++; return null; }).Keyframes);
        Assert.Equal(1, calls);
        calls = 0;
        Assert.Single(SpeechFitBuilder.Build(60001, () => { calls++; return FaceClip(1); }).Keyframes);
        Assert.Equal(0, calls);
        calls = 0;
        var atCap = SpeechFitBuilder.Build(60000, () => { calls++; return FaceClip(59967); });
        Assert.Equal(1, calls);
        Assert.Equal(60000u, atCap.Keyframes.Max(k => k.TriggerTimeMs));
        calls = 0;
        var partial = SpeechFitBuilder.Build(2000, () => ++calls == 1 ? FaceClip(990) : null);
        Assert.Equal(2, calls);
        Assert.Equal(new uint[] { 0, 33, 1023 }, partial.Keyframes.Select(k => k.TriggerTimeMs));
        Assert.Throws<InvalidOperationException>(() => SpeechFitBuilder.Build(100, () => FaceClip(uint.MaxValue)));
        var invalid = new AnimationClip { Keyframes = new Keyframe[] { new AudioKeyframe(0, new long[] { 5 }, 1, new[] { 1f }, false) } };
        Assert.Throws<NotSupportedException>(() => SpeechFitBuilder.Build(100, () => invalid));
    }
}
