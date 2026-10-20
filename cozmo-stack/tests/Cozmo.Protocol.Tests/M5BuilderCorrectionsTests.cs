using System.Collections.Concurrent;
using System.Net;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class M5BuilderCorrectionsTests
{
    private static SourceLogSink Source(CozmoEngine engine) =>
        (level, channel, key, text) => engine.EmitSourceLog(level, channel, key, text);

    private static string Assets()
    {
        string root = Path.Combine(Path.GetTempPath(), "B-M5-3-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "animations"));
        Directory.CreateDirectory(Path.Combine(root, "animationGroups"));
        File.WriteAllText(Path.Combine(root, "animations", "a.json"), "{\"a\":[]}");
        File.WriteAllText(Path.Combine(root, "animationGroups", "g.json"),
            "{\"Animations\":[{\"Name\":\"a\",\"Weight\":1.0,\"Mood\":\"Default\"}]}");
        return root;
    }

    [Theory]
    [InlineData(SimpleMood.Default, 3890346734u, 3586334585u)]
    [InlineData(SimpleMood.Happy, 545404204u, 4161255391u)]
    public void G4_EmptyPassAlwaysConsumesTwoMtWords(SimpleMood mood, uint low, uint high)
    {
        // 0x0058A9FC -> 0x0058AA72..7E before the empty test at 0x0058AA86.
        // Happy recurses into Default: two passes. Words are standard mt19937 seed 5489 outputs 3..6.
        using var robot = CozmoRobot.CreateOffline();
        robot.Engine.ContextRandom.SetSeed(5489);
        var group = new AnimationGroup();
        Assert.Null(group.GetAnimationName(robot.Engine.ContextRandom, mood, 0, 0, false));
        Assert.Equal((low + high * 4294967296d) / 18446744073709551616d, robot.Engine.ContextRandom.GetNextDbl());
    }

    [Fact]
    public void M5_047_ShippedNoWindowGroupOnCooldownLogsNoBackupAndReturnsFirst()
    {
        // B1..B4 0x0058ABBA..0x0058ADBA; approved pre-first-window policy.
        string root = Resources();
        using var robot = CozmoRobot.CreateOffline();
        var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var library = robot.Animations.LoadFrom(root);
        var group = library.GroupNames.Select(n => library.GetGroup(n)!).First(g =>
            g.Entries.Count > 0 && g.Entries.All(e => !e.UseHeadAngle && e.MoodType == SimpleMood.Default));
        foreach (var e in group.Entries) e.Container.SetCooldown(e.Name, 100);
        entries.Clear();
        Assert.Equal(group.Entries[0].Name, library.GetAnimationNameFromGroup(group.Name, false));
        Assert.Contains(entries, e => e.Name == "AnimationGroup.GetAnimation.NoBackup");
        Assert.DoesNotContain(entries, e => e.Name is "AnimationGroup.GetAnimation.NoAnimation" or "AnimationGroup.GetAnimation.BackupAnimationChosen");
        Assert.All(group.Entries, e => Assert.Equal(100d, e.CooldownEndsSec));
    }

    [Fact]
    public void B6_InheritedWindowRejectsAnOutOfWindowBackup()
    {
        string root = Assets();
        try
        {
            File.WriteAllText(Path.Combine(root, "animations", "b.json"), "{\"b\":[]}");
            File.WriteAllText(Path.Combine(root, "animationGroups", "g.json"), """
                {"Animations":[{"Name":"a","Weight":1.0,"Mood":"Default","UseHeadAngle":true,"HeadAngleMin_Deg":-10.0,"HeadAngleMax_Deg":10.0},
                {"Name":"b","Weight":1.0,"Mood":"Default"}]}
                """);
            using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
            var library = robot.Animations.LoadFrom(root); var group = library.GetGroup("g")!;
            group.Entries[0].Container.SetCooldown("a", 100); group.Entries[1].Container.SetCooldown("b", 50);
            library.HeadAngleRad = () => 1;
            entries.Clear(); Assert.Equal("a", library.GetAnimationNameFromGroup("g", false));
            Assert.Contains(entries, e => e.Name == "AnimationGroup.GetAnimation.NoBackup");
            library.HeadAngleRad = () => 0;
            entries.Clear(); Assert.Equal("b", library.GetAnimationNameFromGroup("g", false));
            Assert.Contains(entries, e => e.Name == "AnimationGroup.GetAnimation.BackupAnimationFound");
        }
        finally { Directory.Delete(root, true); }
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void L6_4_OpenReadFailuresBecomeBinaryDataEmpty(bool unauthorized)
    {
        // ReadFileAsBinary 0x0080361C..22 returns empty; LoadAnimationFile 0x005213B4/DA.
        string root = Assets(), path = Path.Combine(root, "animations", "bad.bin");
        File.WriteAllBytes(path, new byte[] { 1 });
        var keys = new ConcurrentQueue<string>();
        try
        {
            var library = AnimationLibrary.Open(root, sourceLog: (_, _, key, text) =>
            {
                keys.Enqueue(key);
                if (key == "RobotDataLoader.LoadAnimationFile.LoadingAnimationsFromBinaryOrJson" && text.EndsWith(path))
                { File.Delete(path); if (unauthorized) Directory.CreateDirectory(path); }
            });
            Assert.True(library.HasClip("a"));
            Assert.Contains("RobotDataLoader.LoadAnimationFile.BinaryDataEmpty", keys);
            Assert.DoesNotContain("RobotDataLoader.LoadAnimationFile.BinaryDataNull", keys);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void G1_MissingSimpleMoodIsVisibleOnceAndTheContextStreamIncludesFallbackDraw()
    {
        string root = Assets();
        try
        {
            using var robot = CozmoRobot.CreateOffline(); var diagnostics = new List<string>(); robot.Engine.StackLogLine += diagnostics.Add;
            var library = robot.Animations.LoadFrom(root);
            Assert.Equal("a", library.GetAnimationNameFromGroup("g", false));
            Assert.Equal("a", library.GetAnimationNameFromGroup("g", false));
            Assert.Single(diagnostics, s => s.StartsWith("MISSING: M7-026 SimpleMood"));
            library.CurrentMood = () => SimpleMood.Happy; robot.Engine.ContextRandom.SetSeed(5489);
            Assert.Equal("a", library.GetAnimationNameFromGroup("g", false));
            Assert.Equal((545404204d + 4161255391d * 4294967296d) / 18446744073709551616d,
                robot.Engine.ContextRandom.GetNextDbl());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void M5_040_NaNLogsWhileClippingWarningsAreDisabledAndAcrossThreads()
    {
        // isnanf 0x005848B8, EnumToString 0x005848C8; strings at the CE/D2 adr targets.
        using var robot = CozmoRobot.CreateOffline(); var entries = new ConcurrentQueue<EngineLogEntry>(); robot.Engine.LogEntry += entries.Enqueue;
        using var scope = Eye.UseWarningLog(null, Source(robot.Engine));
        bool old = Eye.ClippingWarningEnabled;
        try
        {
            Eye.ClippingWarningEnabled = false;
            float result = 0;
            using (ExecutionContext.SuppressFlow())
            {
                var worker = new Thread(() => result = Eye.Clip(EyeParam.EyeScaleX, float.NaN, 7f));
                worker.Start(); worker.Join();
            }
            Assert.Equal(7f, result);
            var entry = Assert.Single(entries);
            Assert.Equal("ProceduralFace.Clip.NaN", entry.Name);
            Assert.Equal(EngineLogLevel.Warning, entry.Level);
            Assert.Equal("(tc0000) : Returning original value instead of NaN for EyeScaleX", entry.Text);
            entries.Clear(); Eye.ClippingWarningEnabled = true;
            var thread = new Thread(() => Eye.Clip(EyeParam.UpperLidAngle, 46)); thread.Start(); thread.Join();
            Assert.Equal("ProceduralFace.Clip.OutOfRange", Assert.Single(entries).Name);
        }
        finally { Eye.ClippingWarningEnabled = old; }
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void JsonReservedNameLogsLoadingBeforeReservedName()
    {
        // 0x00588788 precedes reserved-name decision at 0x005887B2.
        string root = Assets();
        try
        {
            string path = Path.Combine(root, "reserved.json"); File.WriteAllText(path, "{\"_PROCEDURAL_\":[]}");
            using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
            Assert.Null(JsonClipLoader.Load(path, null, Source(robot.Engine)));
            Assert.Equal(new[] { "CannedAnimationContainer::DefineFromJson", "CannedAnimationContainer.DefineFromJson.ReservedName" }, entries.Select(e => e.Name));
        }
        finally { Directory.Delete(root, true); }
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory]
    [InlineData("false")] [InlineData("17")] [InlineData("null")] [InlineData("{}")]
    public void JsonBackpackWrongTypesWarnAndReject(string bad)
    {
        CheckColors(bad, "\"bad-name\"", new[] { "JsonTools.GetColorOptional" });
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void JsonBackpackWarningsFollowColorReadOrder()
    {
        // Back GetU8 before Front NamedColors (J1.9 / 0x00840398, 0x00840282).
        CheckColors("[-1,0,0]", "\"bad-name\"", new[] { "ColorRGBA.GetU8.OutOfRangeValue", "NamedColors.GetByString" });
    }

    private static void CheckColors(string back, string front, string[] expected)
    {
        string root = Assets();
        try
        {
            string path = Path.Combine(root, "lights.json");
            File.WriteAllText(path, "{\"lights\":[{\"Name\":\"BackpackLightsKeyFrame\",\"triggerTime_ms\":0,\"durationTime_ms\":1,\"Back\":" + back + ",\"Front\":" + front + ",\"Middle\":[0,0,0],\"Left\":[0,0,0],\"Right\":[0,0,0]}]}");
            using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
            var clip = JsonClipLoader.Load(path, null, Source(robot.Engine))!;
            Assert.Equal(expected, entries.Where(e => e.Level == EngineLogLevel.Warning).Select(e => e.Name));
            Assert.Equal(expected.Length == 1, clip.LoadTruncated);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void AudioPlayBufferUsesAudioSampleTagInClearLogs()
    {
        using var robot = CozmoRobot.CreateOffline(); var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var stream = new StreamSendBuffer { SourceLog = Source(robot.Engine) };
        var audio = new CozmoAudio(_ => throw new Exception("No send expected"));
        audio.AttachStream(stream, () =>
        {
            Assert.Equal("animAudioSample", stream.TagNames); // official EngineToRobot tag 0x8E.
            stream.ClearSendBuffer();
        });
        audio.Play(new short[] { 1 });
        var entry = Assert.Single(entries);
        Assert.Equal("AnimationStreamer.ClearSendBuffer", entry.Name);
        Assert.Equal("Clearing unsent animAudioSample", entry.Text);
    }

    private sealed class Port : IEngineTransport
    {
        public bool TimedOut => false;
        public event Action<Cozmo.Transport.ReceiverEvent>? Received { add { } remove { } }
        public void Start() { }
        public void Connect(IPAddress ip, bool simulated) { }
        public void Disconnect(IPEndPoint endpoint) { }
        public void SendData(byte[] data) { }
    }

    [Fact]
    public void HostAnimationExceptionIsIsolatedWithoutAnEngineFailureResult()
    {
        using var robot = CozmoRobot.CreateForTest(new Port(), () => 1_000_000_000, new CozmoEngineOptions { BlockPoolPath = "" });
        robot.Engine.StartProduction(5489);
        var diagnostics = new List<string>(); robot.Engine.StackLogLine += diagnostics.Add;
        var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        robot.Animations.Scheduler.Stream.Buffer(null, new Cozmo.Protocol.HeadAngle(), _ => throw new InvalidOperationException("host failed"));
        Assert.Equal(0, robot.Animations.EngineUpdate());
        Assert.Contains(diagnostics, s => s.Contains("host failed"));
        Assert.DoesNotContain(entries, e => e.Name == "Robot.Update.AnimationStreamer");
    }

    private static string Resources()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "re-analysis", "obb", "assets", "cozmo_resources");
            if (Directory.Exists(path)) return path;
        }
        throw new DirectoryNotFoundException("Shipped resources required");
    }
}
