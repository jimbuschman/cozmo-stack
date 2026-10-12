using System.Reflection;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class M5FinalBuilderTests : IDisposable
{
    private readonly bool _oldError = EngineErrorState.ErrorFlagSet;
    private readonly EngineRandom _oldDistortionRng = ScanlineDistorter.Rng;
    public void Dispose() { EngineErrorState.ErrorFlagSet = _oldError; ScanlineDistorter.Rng = _oldDistortionRng; }
    private sealed class Sink : IAnimationSink
    {
        public int? Played;
        public int? AudioFramesPlayed => Played;
        public void Face(FaceBitmap b) { }
        public void Audio(byte[]? b) { }
        public void Head(sbyte a, uint d) { }
        public void Lift(byte h, uint d) { }
        public void Body(BodyKeyframe k) { }
        public void BodyStop() { }
        public void Lights(LightsKeyframe k) { }
        public void Event(string e) { }
        public void AnimationStarted(byte t) { }
        public void AnimationEnded() { }
        public void Finished(string c, bool complete) { }
    }
    private record Log(EngineLogLevel Level, string? Channel, string Key, string Text);
    private static SourceLogSink Capture(List<Log> logs) => (l, c, k, t) => logs.Add(new(l, c, k, t));
    private static AnimationClip Clip(params Keyframe[] frames) => new()
    { Name = "c", Keyframes = frames, Tracks = frames.Aggregate(AnimationTrack.None, (t, k) => t | k.Track) };

    [Fact]
    public void FlushCountsRobotMessagesAndExcludesBlockedBackpackCallbacks()
    {
        // +0x94 at 0x0057D142/0x0057D18E; audio budget is 14 at 0x0057C79E.
        var logs = new List<Log>(); var scheduler = new AnimationScheduler(new Sink { Played = 0 }) { SourceLog = Capture(logs) };
        scheduler.Play(Clip(new LightsKeyframe(462, 33, new float[3], new float[3], new float[3], new float[3], new float[3])), 0);
        scheduler.Advance(1000);
        Assert.True(scheduler.Stream.Count > scheduler.Stream.RobotMessageCount);
        int count = scheduler.Stream.RobotMessageCount;
        Assert.Equal(2, count); // frame 14: blocked AudioSilence then animBackpackLights; KeyframeFired costs zero.
        scheduler.Stop();
        scheduler.Advance(1000);
        Assert.Equal($"Expect send buffer to be emptied by UpdateStream or StreamFaceLayers by now, has {count} messages",
            Assert.Single(logs, l => l.Key == "AnimationStreamer.Update.SendBufferNotEmpty").Text);
    }

    [Fact]
    public void RemoveIdleNeverFindsIndex1000AndReportsTheSourceOverflow()
    {
        // 0x0057BB3E..0x0057BB60: 1000 searched entries; %s:%d with line 0x210.
        var logs = new List<Log>(); var scheduler = new AnimationScheduler(new Sink()) { SourceLog = Capture(logs) };
        for (int i = 0; i < 1000; i++) scheduler.PushIdleAnimation(AnimationTrigger.NeutralFace, "lock" + i);
        logs.Clear();
        Assert.Equal(1, scheduler.RemoveIdleAnimation("lock999", 0));
        Assert.Equal(new[] { "LoopBoundOverflow", "AnimationStreamer.RemoveIdleAnimation.NoLockNameFoundInStack" }, logs.Select(l => l.Key));
        Assert.Equal(EngineLogLevel.Error, logs[0].Level);
        Assert.Equal("../../../../engine/animations/animationStreamer.cpp:528", logs[0].Text);
        Assert.True(EngineErrorState.ErrorFlagSet);
    }

    [Theory]
    [InlineData("BodyMotion", 4)] [InlineData("LiftHeight", 2)] [InlineData("HeadAngle", 1)]
    public void LiveAppendFailureLogsInnerThenTrackThenOuter(string track, byte unlocked)
    {
        // 0x0057D882/0x0057D914/0x0057D9E4: empty format, errG/break, return 1.
        var logs = new List<Log>(); var scheduler = new AnimationScheduler(new Sink()) { SourceLog = Capture(logs) };
        scheduler.LiveIdleParameters[LiveIdleParam.TimeBeforeWiggleMotions_ms] = 0;
        scheduler.LiveIdleInputs.LockedTracks = () => (byte)(7 & ~unlocked);
        for (int i = 0; i < 1001; i++)
            Assert.True(scheduler.StreamLive(track switch
            {
                "BodyMotion" => new BodyKeyframe(0, 0, "STRAIGHT", 0),
                "LiftHeight" => new LiftKeyframe(0, 0, 0, 0),
                _ => new HeadKeyframe(0, 0, 0, 0)
            }, 0));
        scheduler.PushIdleAnimation(AnimationTrigger.ProceduralLive, "live");
        Assert.Equal(1, scheduler.Advance(0));
        var errors = logs.Where(l => l.Level == EngineLogLevel.Error).ToArray();
        Assert.Equal(new[] { "Animation.AddKeyFrameToBack.Failed", "AnimationStreamer.UpdateLiveAnimation.Add" + track + "KeyFrameFailed", "AnimationStreamer.Update.LiveUpdateFailed" }, errors.Select(l => l.Key));
        Assert.Equal("", errors[1].Text);
    }

    [Fact]
    public void GlitchUsesTheAnimationsDebugChannel()
    {
        var logs = new List<Log>(); var scheduler = new AnimationScheduler(new Sink()) { SourceLog = Capture(logs) };
        scheduler.Layers.AddGlitch(1f);
        var log = Assert.Single(logs, l => l.Key == "TrackLayerComponent.AddGlitch");
        Assert.Equal(EngineLogLevel.Debug, log.Level); Assert.Equal("Animations", log.Channel); Assert.Equal("Degree 1.00", log.Text);
    }

    [Fact]
    public void DistortionRowsMultiplyTheBinary32Reciprocal()
    {
        // 0x00585E28: vdiv.f32 1/6 -> 0x3E2AAAAB (11184811 * 2^-26).
        // 0x00585E34: vmul.f32 by i=5 -> round(55924055/4) * 2^-24
        // = 13981014 * 2^-24 = 0x3F555556. Direct 5/6 rounds to 0x3F555555.
        float inv = 1f / 6;
        Assert.Equal(0x3E2AAAAB, BitConverter.SingleToInt32Bits(inv));
        Assert.Equal(0x3F555556, BitConverter.SingleToInt32Bits(ProceduralFaceRenderer.DistortionRowFraction(inv, 5)));
    }

    [Theory]
    [InlineData("BodyMotionKeyFrame", "\"durationTime_ms\":10,\"speed\":1", "BodyMotionKeyFrame.SetMembersFromJson.MissingRadius", "c: Missing 'radius_mm' field.")]
    [InlineData("BodyMotionKeyFrame", "\"durationTime_ms\":10,\"speed\":1,\"radius_mm\":\"bad\"", "BodyMotionKeyFrame.BadRadiusString", "c: Unrecognized string for 'radius_mm' field: bad")]
    [InlineData("BodyMotionKeyFrame", "\"speed\":1,\"radius_mm\":0", "IKeyFrame.GetMemberFromJsonMacro", "Failed to get 'durationTime_ms' from Json file.")]
    [InlineData("BodyMotionKeyFrame", "\"durationTime_ms\":10,\"radius_mm\":0", "IKeyFrame.GetMemberFromJsonMacro", "Failed to get 'speed' from Json file.")]
    [InlineData("RobotAudioKeyFrame", "\"audioEventId\":[1,2],\"probability\":[1]", "RobotAudioKeyFrame.SetMembersFromJson.UnknownProbabilities", "c: The number of audio event IDs (2) does not match number of probabilities (1)")]
    [InlineData("RobotAudioKeyFrame", "\"audioEventId\":[1,2],\"probability\":[1,1]", "RobotAudioKeyFrame.SetMembersFromJson.TotalProbabilitiesTooHigh", "c: The total probability of all audio events combined exceeds 1.0")]
    [InlineData("BackpackLightsKeyFrame", "\"Back\":[0,0,0],\"Front\":[0,0,0],\"Middle\":[0,0,0],\"Left\":[0,0,0],\"Right\":[0,0,0]", "IKeyFrame.GetMemberFromJsonMacro", "Failed to get 'durationTime_ms' from Json file.")]
    [InlineData("BackpackLightsKeyFrame", "\"Back\":[0,0,0],\"Front\":[0,0,0],\"Middle\":[0,0,0],\"Left\":[0,0,0],\"Right\":[0,0,0],\"durationTime_ms\":null", "IKeyFrame.GetMemberFromJsonMacro", "Failed to get 'durationTime_ms' from Json file.")]
    public void JsonFailuresLogTheirSourceTailAndStopTheParent(string type, string members, string key, string text)
    {
        // Body 0x004FB816..0x004FB8F8, audio 0x004FA35A/0x004FA40E, backpack 0x004FADD2..0x004FAE30;
        // parent 0x00576E74..0x00576EC2, frame index 1, keeps the preceding frame and exits.
        var logs = new List<Log>(); var stack = new List<string>();
        using var doc = JsonDocument.Parse("{\"c\":[{\"Name\":\"RecordHeadingKeyFrame\",\"triggerTime_ms\":0},{\"Name\":\"" + type + "\",\"triggerTime_ms\":1," + members + "},{\"Name\":\"RecordHeadingKeyFrame\",\"triggerTime_ms\":2}]}");
        var clip = JsonClipLoader.LoadDocument(doc, stack.Add, Capture(logs))!;
        Assert.True(clip.LoadTruncated); Assert.Single(clip.Keyframes);
        var errors = logs.Where(l => l.Level == EngineLogLevel.Error).ToArray();
        Assert.Equal(new[] { key, "Animation.DefineFromJson.AddKeyFrameFailure" }, errors.Select(l => l.Key));
        Assert.Equal(text, errors[0].Text); Assert.Equal($"Adding {type} frame 1 failed.", errors[1].Text);
        Assert.Empty(stack); Assert.True(EngineErrorState.ErrorFlagSet);
    }

    [Fact]
    public void KeepAliveUsesNanosecondsAtTheStrictHalfSecondBoundary()
    {
        // 0x0084BC80 and 0x0057CF86..AE: (float)(ns/1e9), vsub.f32, strict > 0.5f.
        var scheduler = new AnimationScheduler(new Sink());
        scheduler.Play(Clip(new HeadKeyframe(0, 100, 5, 0)), 2000);
        scheduler.Advance(2000, 2_000_000_000);
        scheduler.Advance(2033, 2_033_000_000);
        scheduler.Advance(2500, 2_500_000_000);
        Assert.DoesNotContain("KeepAliveEyeDart", scheduler.FaceLayerNames);
        // 200 ns survives binary32 at 2.5 s (next float 0x40200001); integer ms is still 2500.
        scheduler.Advance(2500, 2_500_000_200);
        Assert.Contains("KeepAliveEyeDart", scheduler.FaceLayerNames);
    }

    [Fact]
    public void FaceMapMissIsCaseSensitiveAndConsumed()
    {
        string root = Path.Combine(Path.GetTempPath(), "m5-final-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "faceAnimations", "Exact"));
        try
        {
            var library = FaceAnimationLibrary.Open(root);
            Assert.True(library.Has("Exact")); Assert.False(library.Has("exact"));
            Assert.NotNull(library.Frames("Exact")); Assert.Null(library.Frames("exact"));
            Assert.NotNull(library.Variants("Exact")); Assert.Null(library.Variants("exact"));
            var clip = Clip(new FaceAnimationKeyframe(0, "exact"));
            var scheduler = new AnimationScheduler(new Sink()) { FaceAnimationVariants = library.Variants };
            scheduler.Play(clip, 0); scheduler.Advance(0);
            Assert.True(StreamAnimation.Of(clip).FaceAnim.AtEnd); // 0x0058173A -> IsDone -> Pull consumes.
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void FrameGateRefusesNonemptyBufferAndWithoutAudioUsesHasFramesLeft()
    {
        // 0x0057CC72..0x0057CC8C, production predicate (also called by Advance/UpdateStream).
        var scheduler = new AnimationScheduler(new Sink());
        var anim = new StreamAnimation("frames", false);
        var gate = typeof(AnimationScheduler).GetMethod("ShouldProcessAnimationFrame", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.False((bool)gate.Invoke(scheduler, new object[] { anim })!);
        anim.AddNew(new HeadKeyframe(0, 33, 0, 0)); anim.Init();
        Assert.True((bool)gate.Invoke(scheduler, new object[] { anim })!);
        scheduler.Stream.Buffer(null, new Cozmo.Protocol.HeadAngle(), _ => false);
        Assert.False((bool)gate.Invoke(scheduler, new object[] { anim })!);
    }

    [Theory]
    [InlineData("{}", "AnimationGroupEntry.DefineFromJson.NoName")]
    [InlineData("{\"Name\":\"absent\"}", "AnimationGroupEntry.DefineFromJson.InvalidName")]
    [InlineData("{\"Name\":\"c\"}", "AnimationGroupEntry.DefineFromJson.NoWeight")]
    [InlineData("{\"Name\":\"c\",\"Weight\":1}", "AnimationGroupEntry.DefineFromJson.NoMood")]
    [InlineData("{\"Name\":\"c\",\"Weight\":1,\"Mood\":\"Default\",\"UseHeadAngle\":true}", "AnimationGroupEntry.DefineFromJson.NoHeadAngleWhenUsingHeadAngles")]
    public void GroupRejectionSetsErrorBeforeTheParentLogAndRetainsFollowingEntries(string entry, string key)
    {
        // Shared reject 0x0058C6E8..F4, parent 0x0058A6A2..D8; head error 0x0058C7F2.
        string root = Path.Combine(Path.GetTempPath(), "m5-group-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "animations"));
        Directory.CreateDirectory(Path.Combine(root, "animationGroups"));
        File.WriteAllText(Path.Combine(root, "animations", "c.json"), "{\"c\":[{\"Name\":\"RecordHeadingKeyFrame\",\"triggerTime_ms\":0}]}");
        File.WriteAllText(Path.Combine(root, "animationGroups", "g.json"), "{\"Animations\":[" + entry + ",{\"Name\":\"c\",\"Weight\":1,\"Mood\":\"Default\"}]}");
        try
        {
            var logs = new List<Log>();
            bool parentSawError = false;
            var library = AnimationLibrary.Open(root, sourceLog: (l, c, k, t) =>
            {
                if (k == "AnimationGroup.DefineFromJson.AddEntryFailure") parentSawError = EngineErrorState.ErrorFlagSet;
                logs.Add(new(l, c, k, t));
            });
            Assert.Single(library.GetGroup("g")!.Entries);
            var errors = logs.Where(l => l.Level == EngineLogLevel.Error).ToArray();
            Assert.Equal(new[] { key, "AnimationGroup.DefineFromJson.AddEntryFailure" }, errors.Select(l => l.Key));
            Assert.Equal("Adding animation 0 failed.", errors[1].Text); Assert.True(parentSawError);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void GroupWithoutAnimationsSetsTheSourceErrorFlag()
    {
        string root = Path.Combine(Path.GetTempPath(), "m5-no-group-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "animationGroups"));
        File.WriteAllText(Path.Combine(root, "animationGroups", "g.json"), "{\"other\":1}");
        try
        {
            var logs = new List<Log>(); EngineErrorState.ErrorFlagSet = false;
            AnimationLibrary.Open(root, sourceLog: Capture(logs));
            Assert.Single(logs, l => l.Key == "AnimationGroup.DefineFromJson.NoAnimations");
            Assert.True(EngineErrorState.ErrorFlagSet); // 0x0058A788, conditional break 0x0058A78C.
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(new byte[] { 1 })]
    [InlineData(new byte[] { 255, 255, 255, 255, 0, 0, 0, 0 })]
    public void MalformedBinaryLogsBinaryDataNullWithoutThrowing(byte[] bytes)
    {
        // BinaryDataNull sErrorF 0x00521634, errG/break in the following tail.
        string root = Path.Combine(Path.GetTempPath(), "m5-binary-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "animations"));
        string path = Path.Combine(root, "animations", "bad.bin"); File.WriteAllBytes(path, bytes);
        try
        {
            var logs = new List<Log>();
            var library = AnimationLibrary.Open(root, sourceLog: Capture(logs));
            Assert.Empty(library.ClipNames);
            Assert.Equal("Found no data in " + path, Assert.Single(logs, l => l.Key == "RobotDataLoader.LoadAnimationFile.BinaryDataNull").Text);
            Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally { Directory.Delete(root, true); }
    }
}
