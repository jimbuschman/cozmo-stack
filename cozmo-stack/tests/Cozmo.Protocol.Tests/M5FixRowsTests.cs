using System.Collections.Concurrent;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

// Expected values are from 20261011-m5-fix-rows.md and the adopted CW/FK/TD rows, not an implementation oracle.
[Collection("M5 process statics")]
public class M5FixRowsTests
{
    private sealed class Sink : IAnimationSink
    {
        public bool Succeeds = true;
        public bool LastSendSucceeded => Succeeds;
        public int Sends;
        public void Face(FaceBitmap b) => Sends++;
        public void Audio(byte[]? b) => Sends++;
        public void Head(sbyte a, uint d) => Sends++;
        public void Lift(byte h, uint d) => Sends++;
        public void AnimationStarted(byte t) => Sends++;
        public void AnimationEnded() => Sends++;
        public void Body(BodyKeyframe k) => Sends++;
        public void BodyStop() => Sends++;
        public void Lights(LightsKeyframe k) => Sends++;
        public void Event(string s) { }
        public void Finished(string s, bool c) { }
    }
    private sealed class Catalog : IAnimationCatalog
    {
        public int Choices;
        public AnimationClip? Clip;
        public string Name = "idle";
        public int Count = 1;
        public bool HasAnimationForTrigger(AnimationTrigger t) => true;
        public string GetAnimationForTrigger(AnimationTrigger t) => "group";
        public string GetAnimationNameFromGroup(string g, bool s) { Choices++; return Name; }
        public (string Name, int Count) GetFirstAnimationName(string g) => (Name, Count);
        public AnimationClip? GetAnimation(string n) => Clip;
    }
    private static AnimationClip Clip(string name, params Keyframe[] frames) => new()
    { Name = name, Keyframes = frames.ToList(), Tracks = frames.Aggregate(AnimationTrack.None, (t, k) => t | k.Track) };
    private static string TempAssets()
    {
        string root = Path.Combine(Path.GetTempPath(), "m5-rows-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "animations"));
        return root;
    }
    private static void JsonClip(string root, string file, string name) => File.WriteAllText(Path.Combine(root, "animations", file),
        "{\"" + name + "\":[{\"Name\":\"HeadAngleKeyFrame\",\"triggerTime_ms\":0,\"durationTime_ms\":100,\"angle_deg\":5,\"angleVariability_deg\":0}]}");
    private static SourceLogSink Source(CozmoEngine engine) =>
        (level, channel, key, text) => engine.EmitSourceLog(level, channel, key, text);
    private static void Entry(List<EngineLogEntry> entries, string key, EngineLogLevel level, string? channel, string text)
    {
        var e = Assert.Single(entries, e => e.Name == key);
        Assert.Equal(level, e.Level); Assert.Equal(channel, e.Channel); Assert.Empty(e.Data);
        Assert.Equal(level is EngineLogLevel.Warning or EngineLogLevel.Error ? "(tc0000) : " + text : text, e.Text);
    }

    [Fact]
    public void X1ToX5_ExhaustionSkipsTheIdleUntilTheNextTick()
    {
        var cat = new Catalog { Clip = Clip("idle", new HeadKeyframe(0, 100, 3, 0)) };
        var s = new AnimationScheduler(new Sink()) { Catalog = cat };
        s.PushIdleAnimation(AnimationTrigger.NeutralFace, "idle");
        s.Play(Clip("empty"), 0);
        Assert.Equal(0, s.Advance(0)); Assert.Equal(0, cat.Choices);
        Assert.Equal(0, s.Advance(60)); Assert.Equal(1, cat.Choices);
    }

    [Fact]
    public void G4G5_DrawNarrowsToF32BeforeSubtraction()
    {
        // MT5489 gives 3499211612,581869302: r=0.1354770042967805. f32 r is 0x3E0ABA7C;
        // subtracting exactly that first weight leaves zero, so the second candidate wins.
        float first = BitConverter.Int32BitsToSingle(0x3E0ABA7C);
        var g = new AnimationGroup { Name = "g", Entries = new[]
        { new AnimationGroupEntry("first", first, 0, "Default"), new AnimationGroupEntry("second", 1f - first, 0, "Default") } };
        Assert.Equal("second", g.GetAnimationName(new EngineRandom(5489), SimpleMood.Default, 0, 0, false)!.Name);
    }

    [Fact]
    public void G6_TimeIsF32AndCooldownRemainsF64()
    {
        var e = new AnimationGroupEntry("a", 1, 1.25, "Default");
        var g = new AnimationGroup { Entries = new[] { e } };
        g.GetAnimationName(new EngineRandom(5489), SimpleMood.Default, 16777217d, 0, true);
        Assert.Equal(16777217.25d, e.CooldownEndsSec);
        Assert.Equal(0f, e.Container.TimeUntilCooldownOver("absent", 99));
    }

    [Fact]
    public void B6_ThePreviousHeadWindowIsInherited()
    {
        string root = TempAssets();
        try
        {
            JsonClip(root, "a.json", "a"); JsonClip(root, "b.json", "b");
            Directory.CreateDirectory(Path.Combine(root, "animationGroups"));
            File.WriteAllText(Path.Combine(root, "animationGroups", "g.json"), """
                {"Animations":[{"Name":"a","Weight":1.0,"Mood":"Default","UseHeadAngle":true,"HeadAngleMin_Deg":-10.0,"HeadAngleMax_Deg":10.0},
                {"Name":"b","Weight":1.0,"Mood":"Default","CooldownTime_Sec":0.1234567890123}]}
                """);
            var lib = AnimationLibrary.Open(root); var g = lib.GetGroup("g")!;
            Assert.Equal(0.1234567890123d, g.Entries[1].CooldownSec);
            g.Entries[0].Container.SetCooldown("a", 100); g.Entries[1].Container.SetCooldown("b", 50);
            Assert.Equal("b", g.GetAnimationName(new EngineRandom(5489), SimpleMood.Default, 1, 0, false)!.Name);
            Assert.Equal(50d, g.Entries[1].CooldownEndsSec); // B3 has no SetCooldown.
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void G2G7G9_GroupEntriesReachTheStructuredLogger()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var g = new AnimationGroup { Name = "g", Entries = new[] { new AnimationGroupEntry("a", 1, 0, "Default") }, Log = robot.Engine.Log, SourceLog = Source(robot.Engine) };
        Assert.NotNull(g.GetAnimationName(new EngineRandom(5489), SimpleMood.Default, 0, 0, true));
        Entry(entries, "AnimationGroup.GetAnimation", EngineLogLevel.Debug, "Unnamed", "getting animation from group 'g', simple mood = 'Default'");
        Entry(entries, "AnimationGroup.GetAnimation.Found", EngineLogLevel.Info, "Animations", "Group 'g' returning animation name 'a'");
        entries.Clear(); g.Entries[0].Container.SetCooldown("a", 10);
        Assert.Null(g.GetAnimationName(new EngineRandom(5489), SimpleMood.Default, 0, 0, true));
        Entry(entries, "AnimationGroup.GetAnimation.NoAnimation", EngineLogLevel.Error, null, "Could not find a single animation from group 'g' to run. Returning empty");
    }

    [Fact]
    public void L1ToL9_LoadsEagerlyWithOwnNamesCaseSensitiveSuffixesAndJsonFailureProgress()
    {
        string root = TempAssets(); var logs = new ConcurrentQueue<string>();
        try
        {
            JsonClip(root, "file.json", "OwnName"); JsonClip(root, "ignored.JSON", "ignored");
            File.WriteAllText(Path.Combine(root, "animations", "failed.json"), "invalid json");
            var lib = AnimationLibrary.Open(root, logs.Enqueue);
            JsonClip(root, "file.json", "Replacement");
            Assert.Equal("OwnName", lib.GetClip("OwnName").Name);
            Assert.False(lib.HasClip("file")); Assert.False(lib.HasClip("ownname")); Assert.False(lib.HasClip("ignored"));
            Assert.Equal(BitConverter.SingleToInt32Bits(0.7f), BitConverter.SingleToInt32Bits(lib.LoadingProgress));
            Assert.DoesNotContain(logs, l => l.StartsWith("MISSING: M5-046"));
            Assert.Contains("info: [Animations] RobotDataLoader.CollectAnimFiles.Results: Found 2 animation files of type 0", logs);
            Assert.Contains("info: [Animations] RobotDataLoader.LoadAnimations.CannedAnimationsCount: Total number of canned animations available = 1", logs);
            Assert.True(Eye.ClippingWarningEnabled);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(-46f, -45f)] [InlineData(46f, 45f)]
    public void CW5_ClippingLogsBothBranchesAndLeavesUnmappedParametersAlone(float input, float expected)
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        using var scope = Eye.UseWarningLog(robot.Engine.Log, sourceLog: Source(robot.Engine));
        Eye.ClippingWarningEnabled = true;
        Assert.Equal(expected, Eye.Clip(EyeParam.UpperLidAngle, input));
        Entry(entries, "ProceduralFace.Clip.OutOfRange", EngineLogLevel.Warning, null,
            FormattableString.Invariant($"Value of {input:F6} out of range [-45.000000,45.000000] for parameter UpperLidAngle. Clipping."));
        entries.Clear(); Assert.Equal(999f, Eye.Clip(EyeParam.EyeCenterX, 999)); Assert.Empty(entries);
        Eye.ClippingWarningEnabled = false; Assert.Equal(expected, Eye.Clip(EyeParam.UpperLidAngle, input)); Assert.Empty(entries);
        Eye.ClippingWarningEnabled = true;
    }

    [Fact]
    public void ColorWarningsArePerFloatComponentAndTheRawPathIsSilent()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        uint c = BackpackColor.Default;
        Assert.True(BackpackColor.GetColorOptional(new[] { -1f, 0f, 0f, 2f }, ref c, robot.Engine.Log, sourceLog: Source(robot.Engine)));
        Assert.Equal(2, entries.Count(e => e.Name == "ColorRGBA.GetU8.OutOfRangeValue"));
        Assert.All(entries, e => { Assert.Equal(EngineLogLevel.Warning, e.Level); Assert.Equal("(tc0000) : Float RGBA values should be on the interval [0,1]. Will clip.", e.Text); });
        entries.Clear(); Assert.True(BackpackColor.GetColorOptional(new[] { 2f, -1f, 0f, 2f }, ref c, robot.Engine.Log, sourceLog: Source(robot.Engine))); Assert.Empty(entries);
        Assert.False(BackpackColor.GetColorOptional(new[] { 1f }, ref c, robot.Engine.Log, sourceLog: Source(robot.Engine)));
        Entry(entries, "JsonTools.GetColorOptional", EngineLogLevel.Warning, null, "Expecting color in Json to be a string or 3 or 4 element array");
    }

    [Fact]
    public void D0ToD4_AudioDrawsEveryKeyframeAtInitWithoutAnAudioSource()
    {
        var logs = new List<string>(); var s = new AnimationScheduler(new Sink()) { Log = logs.Add };
        s.Play(Clip("audio", new AudioKeyframe(500, new long[] { 1 }, 1, new[] { 1f }, false),
            new AudioKeyframe(600, new long[] { 0 }, 1, new[] { 1f }, true), new AudioKeyframe(700, Array.Empty<long>(), 1, Array.Empty<float>(), false)), 0);
        Assert.Equal(2, logs.Count(l => l.StartsWith("debug: [Audio] RobotAudioKeyFrame.GetAudioRef.RandomAudioSelection:")));
        Assert.Single(logs, l => l.StartsWith("error: RobotAudioKeyFrame.GetAudioRefIndex.EmptyAudioReferences:"));
        s.Advance(0); Assert.Equal(2, logs.Count(l => l.StartsWith("debug: [Audio] RobotAudioKeyFrame.GetAudioRef.RandomAudioSelection:")));
    }

    [Fact]
    public void DA1ToDA4_DeviceAudioConsumesAtMostOneBeforeTheBudgetEvenWhenSendingFails()
    {
        var sink = new Sink { Succeeds = false }; var s = new AnimationScheduler(sink);
        var clip = Clip("device", new DeviceAudioKeyframe(10, "one"), new DeviceAudioKeyframe(20, "two"));
        s.Play(clip, 0); Assert.Equal(1, s.Advance(100));
        Assert.Equal(20u, StreamAnimation.Of(clip).DeviceAudio.Current!.Trigger);
        Assert.Equal(1, s.Advance(100)); Assert.True(StreamAnimation.Of(clip).DeviceAudio.AtEnd);
    }

    [Fact]
    public void U3_UninitializedAndSendFailuresReturnOne()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var s = new AnimationScheduler(new Sink()) { Log = robot.Engine.Log, SourceLog = Source(robot.Engine) };
        var method = typeof(AnimationScheduler).GetMethod("UpdateStreamLocked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Assert.Equal(1, method.Invoke(s, new object[] { new StreamAnimation("u", false), true }));
        Entry(entries, "Animation.Update", EngineLogLevel.Error, null, "u: Animation must be initialized before it can be played/updated.");
        var failed = new AnimationScheduler(new Sink { Succeeds = false }); failed.Play(Clip("h", new HeadKeyframe(0, 100, 0, 0)), 0);
        Assert.Equal(1, failed.Advance(0));
    }

    [Fact]
    public void TD8_DestroyDropsMessagesAndReleasesHostWaitersWithoutSendsOrEndCallbacks()
    {
        var sink = new Sink(); var logs = new List<string>(); var s = new AnimationScheduler(sink) { Log = logs.Add };
        s.Stream.Log = logs.Add; var h = s.Play(Clip("future", new HeadKeyframe(500, 100, 0, 0)), 0)!;
        bool callback = false; h.OnEnd(_ => callback = true);
        s.Stream.Buffer(null, new Cozmo.Protocol.HeadAngle(), _ => { sink.Sends++; return true; });
        s.Destroy(); Assert.Equal(0, sink.Sends); Assert.False(callback);
        Assert.True(h.Completion.IsCompletedSuccessfully); Assert.Equal(AnimationEndReason.Cancelled, h.Completion.GetAwaiter().GetResult());
        Assert.Contains(logs, l => l.Contains("AnimationStreamer.ClearSendBuffer: Clearing unsent"));
    }

    [Fact]
    public void M5_038_FactoryBuildsCheckedConstructorArgumentsAndParallelLift()
    {
        using var robot = CozmoRobot.CreateOffline(); var args = new List<SleepAnimationArguments>();
        var parallel = GoToSleepAnimation.Create(robot, a => { args.Add(a); return new Child(); })!;
        Assert.Equal(new ushort[] { 0xD2, 0xD5, 0xD4 }, args.Select(a => (ushort)a.Trigger));
        Assert.All(args, a => { Assert.Equal(1, a.NumLoops); Assert.True(a.Interrupt); Assert.Equal(AnimationTrack.None, a.Locks); Assert.Equal(0x42700000, BitConverter.SingleToInt32Bits(a.TimeoutSeconds)); Assert.False(a.Strict); });
        Assert.Equal(2, parallel.Children.Count); Assert.Equal(3, Assert.IsType<CompoundActionSequential>(parallel.Children[0]).Children.Count);
        Assert.Equal(2u, parallel.Children[1].RequiredTrackMask);
        var missing = new List<string>(); robot.Engine.StackLogLine += missing.Add;
        Assert.Null(GoToSleepAnimation.Create(robot, null)); Assert.Contains(missing, l => l.Contains("MISSING: M5-038 -> M8"));
    }
    private sealed class Child() : ActionRunner(26, 0) { public override uint CheckIfDone() => EngineActionResult.Success; public override bool CanInterrupt() => true; }

    [Fact]
    public void FK1ToFK4_FaceOverrideControlsIsDoneThroughTheStreamer()
    {
        // 0x004F9770..0x004F9778: +0x28 != 0 returns false even for an absent name (0 frames).
        // Input injection only: the native writers of +0x28 remain RECOVERABLE_GAP, not an asset feature.
        var clip = Clip("override", new FaceAnimationKeyframe(0, "absent"));
        var anim = StreamAnimation.Of(clip);
        var scheduler = new AnimationScheduler(new Sink());
        scheduler.Play(clip, 0);
        anim.FaceAnim.Current!.FaceAnimationOverride = 1;
        scheduler.Advance(0);
        Assert.False(anim.FaceAnim.AtEnd);
        anim.FaceAnim.Current!.FaceAnimationOverride = 0;
        scheduler.Advance(33);
        Assert.True(anim.FaceAnim.AtEnd);
    }
    // A minimal schema fixture, encoded independently of the production readers.
    private static FlatTable Table(params object?[] fields)
    {
        var bytes = new List<byte>(new byte[132 + fields.Length * 8]);
        void Write(int at, byte[] value) { for (int i = 0; i < value.Length; i++) bytes[at + i] = value[i]; }
        Write(64, BitConverter.GetBytes((ushort)(4 + 2 * fields.Length)));
        Write(66, BitConverter.GetBytes((ushort)(4 + 8 * fields.Length)));
        Write(128, BitConverter.GetBytes(64));
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i] is not { } value) continue;
            int slot = 132 + i * 8;
            Write(68 + i * 2, BitConverter.GetBytes((ushort)(4 + i * 8)));
            byte[]? scalar = value switch { uint n => BitConverter.GetBytes(n), short n => BitConverter.GetBytes(n), float n => BitConverter.GetBytes(n), _ => null };
            if (scalar != null) { Write(slot, scalar); continue; }
            int payload = bytes.Count;
            Write(slot, BitConverter.GetBytes((uint)(payload - slot)));
            if (value is string text) { var b = System.Text.Encoding.UTF8.GetBytes(text); bytes.AddRange(BitConverter.GetBytes(b.Length)); bytes.AddRange(b); bytes.Add(0); }
            else if (value is long[] longs) { bytes.AddRange(BitConverter.GetBytes(longs.Length)); foreach (long n in longs) bytes.AddRange(BitConverter.GetBytes(n)); }
            else if (value is float[] floats) { bytes.AddRange(BitConverter.GetBytes(floats.Length)); foreach (float n in floats) bytes.AddRange(BitConverter.GetBytes(n)); }
            else throw new InvalidOperationException();
        }
        return new FlatTable(bytes.ToArray(), 128);
    }
    private static object? Define(string method, FlatTable table, Action<string> log, SourceLogSink sourceLog, string name = "a") =>
        typeof(AnimationLibrary).GetMethod(method, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, new object[] { table, log, name, sourceLog });

    [Fact]
    public void M5_001_002_JsonOwnNameAndWrongEyeLengthEntries()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        string root = TempAssets();
        try
        {
            File.WriteAllText(Path.Combine(root, "animations", "a.json"), "{\"a\":[{\"Name\":\"ProceduralFaceKeyFrame\",\"triggerTime_ms\":0,\"leftEye\":[0]}]}");
            var lib = AnimationLibrary.Open(root, robot.Engine.Log, sourceLog: Source(robot.Engine));
            Entry(entries, "CannedAnimationContainer::DefineFromJson", EngineLogLevel.Debug, "Unnamed", "Loading 'a'");
            Entry(entries, "RobotDataLoader.CollectAnimFiles.Results", EngineLogLevel.Info, "Animations", "Found 1 animation files of type 0");
            Entry(entries, "ProceduralFace.SetEyeArrayHelper.WrongNumParams", EngineLogLevel.Warning, null, "Unexpected number of parameters for leftEye array (1 vs. 19)");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void M5_003_FramesTooCloseLogsAndAdvancesWithoutProducingFace()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var track = new StreamTrack<FaceFrame>(); track.AddKeyFrameToBack(FaceFrame.Default(10)); track.AddKeyFrameToBack(FaceFrame.Default(20));
        var face = new ProceduralFacePose();
        Assert.False(TrackLayerComponent.GetFaceHelper(track, 100, 125, ref face, true, robot.Engine.Log, sourceLog: Source(robot.Engine)));
        Assert.Equal(20u, track.Current!.Trigger);
        Entry(entries, "AnimationStreamer.GetFaceHelper.FramesTooClose", EngineLogLevel.Warning, null, "currentFrameTriggerTime: 10 ms, nextFrameTriggerTime: 20, StreamTime: 25");
    }

    [Theory]
    [InlineData("POINT_TURN", (short)-301, "CheckRotationSpeed.PointTurnSpeedExceedsLimit", "a: PointTurn speed 301 deg/s exceeds limit of 300.000000 deg/s. Clamping")]
    [InlineData("STRAIGHT", (short)221, "CheckStraightSpeed.StraightSpeedExceedsLimit", "a: Speed 221 mm/s exceeds limit of 220.000000 mm/s. Clamping")]
    [InlineData("50", (short)-221, "CheckTurnSpeed.ArcSpeedExceedsLimit", "a: Speed 221 mm/s exceeds limit of 220.000000 mm/s. Clamping")]
    public void M5_006_BodyClampInfo(string radius, short speed, string key, string text)
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var frame = Assert.IsType<BodyKeyframe>(Define("DefineBody", Table(0u, 100u, radius, speed), robot.Engine.Log, sourceLog: Source(robot.Engine)));
        Assert.Equal(radius == "POINT_TURN" ? 300 : 220, Math.Abs((int)frame.Speed));
        Entry(entries, "BodyMotionKeyFrame." + key, EngineLogLevel.Info, "Animations", text);
        entries.Clear(); Assert.Null(Define("DefineBody", Table(0u, 100u, "bad", speed), robot.Engine.Log, sourceLog: Source(robot.Engine)));
        Entry(entries, "BodyMotionKeyFrame.BadRadiusString", EngineLogLevel.Error, null, "a: Unrecognized string for 'radius_mm' field: bad");
    }

    [Fact]
    public void M5_008_023_024_StreamingLogsCarryNamesAndQueuedMessageTags()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var s = new AnimationScheduler(new Sink()) { Log = robot.Engine.Log, SourceLog = Source(robot.Engine) }; s.Stream.Log = robot.Engine.Log; s.Stream.SourceLog = Source(robot.Engine);
        s.Play(Clip("first", new HeadKeyframe(0, 100, 0, 0)), 0);
        Assert.Null(s.Play(Clip("second"), 0, false));
        Entry(entries, "AnimationStreamer.SetStreamingAnimation.NotInterrupting", EngineLogLevel.Info, "Unnamed", "Already streaming first, will not interrupt with second");
        s.Stream.Buffer(null, new Cozmo.Protocol.HeadAngle(), _ => true);
        s.Play(Clip("second"), 0);
        Entry(entries, "AnimationStreamer.SetStreamingAnimation.Aborting", EngineLogLevel.Info, "Unnamed", "Animation second is interrupting animation first");
        Entry(entries, "AnimationStreamer.Abort", EngineLogLevel.Info, "Unnamed", "Tag=1 Streaming:first hasFramesLeft=1 audioComplete=1 startSent=0 endSent=0 sendBuffer=animHeadAngle");
        Entry(entries, "Animation.Init.SendBufferNotEmpty", EngineLogLevel.Info, "Unnamed", "Expecting SendBuffer to be empty. Will clear.");
        Entry(entries, "AnimationStreamer.ClearSendBuffer", EngineLogLevel.Debug, "Unnamed", "Clearing unsent animHeadAngle");
    }

    [Theory]
    [InlineData(false, "UnknownProbabilities", "a: The number of audio event IDs (2) does not match number of probabilities (1)")]
    [InlineData(true, "TotalProbabilitiesTooHigh", "a: The total probability of all audio events combined exceeds 1.0")]
    public void M5_009_AudioDefinitionRejectsAndLogs(bool sum, string key, string text)
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        Assert.Null(Define("DefineAudio", Table(0u, new long[] { 1, 2 }, 1f, sum ? new[] { 0.75f, 0.5f } : new[] { 0.5f }), robot.Engine.Log, sourceLog: Source(robot.Engine)));
        Entry(entries, "RobotAudioKeyFrame.SetMembersFromFlatBuf." + key, EngineLogLevel.Error, null, text);
        Assert.True(Cozmo.Transport.EngineErrorState.ErrorFlagSet);
    }

    [Fact]
    public void M5_010_NeutralConstructorError()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var s = new AnimationScheduler(new Sink()) { Log = robot.Engine.Log, SourceLog = Source(robot.Engine), Catalog = new Catalog { Count = 0 } }; s.LoadNeutralFace();
        Entry(entries, "AnimationStreamer.Constructor.BadNeutralAnimGroup", EngineLogLevel.Error, null, "Neutral animation group group for trigger NeutralFace is empty or null");
    }

    [Fact]
    public void M5_014_BackupEntriesWidenTheF32CooldownForPrintf()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var e = new AnimationGroupEntry("a", 1, 10, "Default") { UseHeadAngle = true, HeadAngleMinDeg = -10, HeadAngleMaxDeg = 10 }; e.Container.SetCooldown("a", 10);
        var g = new AnimationGroup { Name = "g", Entries = new[] { e }, Log = robot.Engine.Log, SourceLog = Source(robot.Engine) };
        e.Container.SetCooldown("a", 10);
        Assert.NotNull(g.GetAnimationName(new EngineRandom(5489), SimpleMood.Default, 1, 0, false));
        Entry(entries, "AnimationGroup.GetAnimation.BackupAnimationChosen", EngineLogLevel.Info, "Animations", "Animation is 'a' and new min cooldown is '9.000000'");
        Entry(entries, "AnimationGroup.GetAnimation.BackupAnimationFound", EngineLogLevel.Info, "Animations", "All animations in group 'g' were on cooldown / invalid, so selected 'a'");
    }

    [Fact]
    public void M5_027_StackLogsAndIdleErrorRepeatWithoutPopping()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var s = new AnimationScheduler(new Sink()) { Log = robot.Engine.Log, SourceLog = Source(robot.Engine), Catalog = new Catalog { Name = "" } };
        s.PushIdleAnimation(AnimationTrigger.NeutralFace, "lock");
        Entry(entries, "AnimationStreamer.PushIdleAnimation", EngineLogLevel.Info, "Animations", "Setting idle animation to 'NeutralFace'.");
        Assert.Equal(1, s.Advance(0)); Assert.Equal(1, s.Advance(60));
        var errors = entries.Where(e => e.Name == "AnimationStreamer.Update.EmptyIdleAnimationFromGroup").ToArray();
        Assert.Equal(2, errors.Length); Assert.All(errors, e => { Assert.Equal(EngineLogLevel.Error, e.Level); Assert.Null(e.Channel); Assert.Empty(e.Data); Assert.Equal("(tc0000) : Returned empty animation name for group NeutralFace. Popping the group.", e.Text); });
        entries.Clear(); Assert.Equal(1, s.RemoveIdleAnimation("absent", 60));
        Entry(entries, "AnimationStreamer.RemoveIdleAnimation.NoLockNameFoundInStack", EngineLogLevel.Warning, null, "Attempted to remove idle with lock name absent, but not found");
    }

    [Fact]
    public void M5_028_029_LayerWarningsAndRemovalInfo()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var face = new FaceLayerManager(new EngineRandom(5489), new ScanLineState()) { Log = robot.Engine.Log, SourceLog = Source(robot.Engine) };
        var p = new LiveIdleParams(); p[LiveIdleParam.EyeDartMaxDistance_pix] = 0; p[LiveIdleParam.BlinkSpacingMinTime_ms] = 10; p[LiveIdleParam.BlinkSpacingMaxTime_ms] = 10;
        face.KeepFaceAlive(p);
        Entry(entries, "AnimationStreamer.KeepFaceAlive.BadBlinkSpacingParams", EngineLogLevel.Warning, null, "Max (10) must be greater than min (10)");
        entries.Clear(); byte tag = face.AddPersistentLayer("empty", new StreamTrack<FaceFrame>());
        face.ApplyLayersToFrame((_, _, _) => false, robot.Engine.Log, sourceLog: Source(robot.Engine));
        Entry(entries, "AnimationStreamer.UpdateFace.EmptyPersistentLayer", EngineLogLevel.Warning, null, $"Persistent face layer is empty - perhaps live frames were used? (tag={tag})");
        face.RemovePersistentLayer(tag, 0);
        Entry(entries, "ITrackLayerManager.RemovePersistentLayer", EngineLogLevel.Info, "Unnamed", $"empty, Tag = {tag} (Layers remaining=1)");
    }

    [Fact]
    public void M5_029_030_TrackCapacityAndLiveAddFailureLogInOrder()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var a = new StreamAnimation("live", true) { Log = robot.Engine.Log, SourceLog = Source(robot.Engine) };
        for (int i = 0; i < 1001; i++) Assert.True(a.AddLive(new HeadKeyframe((uint)i, 1, 0, 0)));
        Assert.False(a.AddLive(new HeadKeyframe(1001, 1, 0, 0)));
        Entry(entries, "Animation.Track.AddKeyFrameToBack.TooManyFrames", EngineLogLevel.Warning, null, "There are already 1001 frames in track of type N4Anki5Cozmo17HeadAngleKeyFrameE. Refusing to add more.");
        Entry(entries, "Animation.AddKeyFrameToBack.Failed", EngineLogLevel.Error, null, "AnimationName:live");
        Assert.Equal(new[] { "Animation.Track.AddKeyFrameToBack.TooManyFrames", "Animation.AddKeyFrameToBack.Failed" }, entries.Select(e => e.Name));
    }

    [Fact]
    public void M5_033_TurnClampsAndUnknownEvent()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        Define("DefineTurnTo", Table(0u, 100u, (short)0, (short)-301, (short)13637, (short)-13637), robot.Engine.Log, sourceLog: Source(robot.Engine));
        Entry(entries, "TurnToRecordedHeadingKeyFrame.CheckRotationAccel.PointTurnDecelExceedsLimit", EngineLogLevel.Info, "Animations", "a: PointTurn decel 13637 deg/s^2 exceeds limit of 13636.363281 deg/s^2. Clamping");
        entries.Clear(); Assert.Null(Define("DefineEvent", Table(0u, "bad"), robot.Engine.Log, sourceLog: Source(robot.Engine)));
        Entry(entries, "EventKeyFrame.UnrecognizedEventName", EngineLogLevel.Warning, null, "bad");
    }

    [Fact]
    public void M5_034_UnknownTriggerWarningUsesTheCppQualifiedKey()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var map = AnimationTriggerResponses.Load(null);
        map.Log = robot.Engine.Log; map.SourceLog = Source(robot.Engine);
        Assert.Equal("", map.GetResponse(AnimationTrigger.NeutralFace));
        Entry(entries, "AnimationTriggerResponsesContainer::GetResponse", EngineLogLevel.Warning, null, "Animation requested for unknown response 'NeutralFace'");
    }

    [Fact]
    public void M5_005_Int32WidthWrapsBeforeWidening()
    {
        // a=INT_MIN,b=INT_MAX: b-a+1 wraps to 0; the draw still occurs, but its product is 0.
        Assert.Equal(int.MinValue, new EngineRandom(5489).RandIntInRange(int.MinValue, int.MaxValue));
    }

    [Fact]
    public void M5_017_AllThreeDebugFormatsAndEmptyReferenceError()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        Assert.Equal(0, AnimationScheduler.ChooseAlternative(1, new[] { 1f }, 0.25, robot.Engine.Log, sourceLog: Source(robot.Engine)));
        Entry(entries, "RobotAudioKeyFrame.GetAudioRefIndex.ShowInfo", EngineLogLevel.Debug, "Audio", "random value = 0.250000, idx = 0 and range = 0.000000 to 1.000000");
        entries.Clear(); var s = new AnimationScheduler(new Sink()) { Log = robot.Engine.Log, SourceLog = Source(robot.Engine) };
        Assert.Equal(0, s.GetAudioRefIndex(new AudioKeyframe(0, new long[] { 1 }, 1, new[] { 1f }, false), false));
        Entry(entries, "RobotAudioKeyFrame.GetAudioRef.RandomAudioSelection", EngineLogLevel.Debug, "Audio", "Randomly selected audio index = 0");
        entries.Clear(); Assert.Equal(0, s.GetAudioRefIndex(new AudioKeyframe(0, new long[] { 1 }, 1, new[] { 1f }, false), true));
        Entry(entries, "RobotAudioKeyFrame.GetAudioRef.RandomAudioSelection", EngineLogLevel.Debug, "Audio", "Probability selected audio index = 0");
        entries.Clear(); Assert.Equal(-1, s.GetAudioRefIndex(new AudioKeyframe(0, Array.Empty<long>(), 1, Array.Empty<float>(), false), true));
        Entry(entries, "RobotAudioKeyFrame.GetAudioRefIndex.EmptyAudioReferences", EngineLogLevel.Error, null, "Check to make sure animation loaded successfully - sound file(s) probably not found.");
    }

    [Fact]
    public void M5_029_EveryAuthoredTrackCapacitySiteNamesItsNativeKeyframeType()
    {
        // Native instantiations in fix rows section 16; RobotAudio 0x005631D8 (warning 0x00563202).
        var cases = new (Keyframe Frame, string NativeType)[]
        {
            (new LiftKeyframe(0, 1, 0, 0), "N4Anki5Cozmo18LiftHeightKeyFrameE"),
            (new FaceKeyframe(0, new ProceduralFacePose()), "N4Anki5Cozmo22ProceduralFaceKeyFrameE"),
            (new HeadKeyframe(0, 1, 0, 0), "N4Anki5Cozmo17HeadAngleKeyFrameE"),
            (new AudioKeyframe(0, new long[] { 1 }, 1, new[] { 1f }, false), "N4Anki5Cozmo18RobotAudioKeyFrameE"),
            (new DeviceAudioKeyframe(0, "one"), "N4Anki5Cozmo19DeviceAudioKeyFrameE"),
            (new LightsKeyframe(0, 1, new[] { 0f, 0f, 0f }, new[] { 0f, 0f, 0f }, new[] { 0f, 0f, 0f }, new[] { 0f, 0f, 0f }, new[] { 0f, 0f, 0f }), "N4Anki5Cozmo22BackpackLightsKeyFrameE"),
            (new FaceAnimationKeyframe(0, "f"), "N4Anki5Cozmo21FaceAnimationKeyFrameE"),
            (new EventKeyframe(0, "bad"), "N4Anki5Cozmo13EventKeyFrameE"),
            (new BodyKeyframe(0, 1, "STRAIGHT", 1), "N4Anki5Cozmo18BodyMotionKeyFrameE"),
            (new RecordHeadingKeyframe(0), "N4Anki5Cozmo21RecordHeadingKeyFrameE"),
            (new TurnToRecordedHeadingKeyframe(0, 1, 0, 1, 1, 1, 2, 0, false), "N4Anki5Cozmo29TurnToRecordedHeadingKeyFrameE")
        };
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        foreach (var (frame, nativeType) in cases)
        {
            entries.Clear(); var animation = new StreamAnimation("a", true) { Log = robot.Engine.Log, SourceLog = Source(robot.Engine) };
            for (int i = 0; i < 1001; i++) Assert.True(animation.AddLive(frame));
            Assert.False(animation.AddLive(frame));
            Entry(entries, "Animation.Track.AddKeyFrameToBack.TooManyFrames", EngineLogLevel.Warning, null, $"There are already 1001 frames in track of type {nativeType}. Refusing to add more.");
        }
    }

}
