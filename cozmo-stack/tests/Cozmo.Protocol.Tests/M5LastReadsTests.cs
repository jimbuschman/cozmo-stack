using System.Collections.Concurrent;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class M5LastReadsTests
{
    private const string ResultsKey = "RobotDataLoader.CollectAnimFiles.Results";

    [Theory]
    [InlineData(5489u, "5489")]
    [InlineData(uint.MaxValue, "4294967295")]
    public void R45_5_StartForwardsNonzeroSeedAndEmitsTheNamedEvent(uint seed, string expected)
    {
        using var robot = CozmoRobot.CreateOffline();
        var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        var shared = robot.Animations.Scheduler.ContextRandom;
        shared.GetNextDbl(); // Reseeding must discard the old stream position.
        robot.Engine.StartOffline(false, seed, () => throw new Exception("Nonzero seed must bypass entropy"));
        Assert.Same(shared, robot.Engine.ContextRandom);
        var entry = Assert.Single(entries);
        Assert.Equal(EngineLogLevel.Event, entry.Level);
        Assert.Null(entry.Channel);
        Assert.Equal("app.random_seed", entry.Name);
        Assert.Equal(expected, entry.Text); // native %u, 0x0082F910..914.
        Assert.Equal("CozmoContext", Assert.Single(entry.Data).Value);
        Assert.True(entry.Data.ContainsKey("$data"));
        if (seed == 5489)
            Assert.Equal((3499211612d + 581869302d * 4294967296d) / 18446744073709551616d,
                shared.GetNextDbl()); // standard mt19937 initial words, also covered by M5-005.
    }

    [Theory]
    [InlineData(0u, "0")]
    [InlineData(42u, "42")]
    public void R45_4_5_EachStartTakesOneFreshEntropyWordIncludingZero(uint word, string expected)
    {
        using var robot = CozmoRobot.CreateOffline();
        var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        int reads = 0;
        for (int i = 0; i < 2; i++)
            robot.Engine.StartOffline(false, 0, () => { reads++; return word; });
        Assert.Equal(2, reads); Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => { Assert.Equal("app.random_seed", entry.Name); Assert.Equal(expected, entry.Text); });
    }

    private sealed class Port : IEngineTransport
    {
        public bool Started;
        public void Start() => Started = true;
        public void Connect(System.Net.IPAddress ip, bool isSimulated) { }
        public void Disconnect(System.Net.IPEndPoint address) { }
        public void SendData(byte[] clad) { }
        public bool TimedOut => false;
        public event Action<Cozmo.Transport.ReceiverEvent>? Received { add { } remove { } }
    }

    [Fact]
    public void R45_5_ProductionStartEmitsNamedSeed()
    {
        var port = new Port();
        using var engine = new CozmoEngine(port, null);
        var entries = new ConcurrentQueue<EngineLogEntry>(); engine.LogEntry += entries.Enqueue;
        engine.StartProduction(0, () => 123);
        Assert.True(port.Started);
        var entry = Assert.Single(entries.Where(e => e.Name == "app.random_seed"));
        Assert.Equal(EngineLogLevel.Event, entry.Level);
        Assert.Equal("123", entry.Text);
        Assert.Equal("CozmoContext", entry.Data["$data"]);
    }

    [Fact]
    public void R45_1_3_R46_7_8_ShippedLoadUsesSharedInstanceAndAllFourResultsInOrder()
    {
        string root = Resources();
        using var robot = CozmoRobot.CreateOffline();
        var entries = new ConcurrentQueue<EngineLogEntry>(); robot.Engine.LogEntry += entries.Enqueue;
        var diagnostics = new ConcurrentQueue<string>(); robot.Engine.StackLogLine += diagnostics.Enqueue;
        var library = robot.Animations.LoadFrom(root);
        Assert.Same(robot.Engine.ContextRandom, robot.Animations.Scheduler.ContextRandom);
        Assert.Same(robot.Engine.ContextRandom, library.ContextRandom);
        var results = entries.Where(e => e.Name == ResultsKey).ToArray();
        // Vector sizes counted independently in the shipped resource tree (files, not clips).
        Assert.Equal(new[] { "Found 507 animation files of type 1", "Found 1 animation files of type 3",
            "Found 37 animation files of type 2", "Found 293 animation files of type 0" }, results.Select(e => e.Text));
        Assert.All(results, e => { Assert.Equal(EngineLogLevel.Info, e.Level); Assert.Equal("Animations", e.Channel); Assert.Empty(e.Data); });
        Assert.DoesNotContain(diagnostics, l => l.StartsWith("MISSING: M5-045") || l.StartsWith("MISSING: M5-046"));
    }

    [Fact]
    public void B_M5_4_ResourcesOptionLoadsLightsAfterSeedCollectClipsAndGroups()
    {
        // L0: 0x0051F3B0..0x0051F3CE; seed precedes loading at 0x004ECBD4..0x004ECC4A.
        string root = Resources();
        var entries = new ConcurrentQueue<EngineLogEntry>();
        using var robot = CozmoRobot.CreateForTest(new Port(), () => 1_000_000_000,
            new CozmoEngineOptions { ResourcesPath = root, BlockPoolPath = "" },
            engineCreated: engine => engine.LogEntry += entries.Enqueue);
        Assert.NotNull(robot.Lights.Body.Animations.Get("Charging"));
        robot.Engine.StartProduction(5489);
        robot.Animations.LoadFrom(root);
        var logs = entries.ToArray();
        int seed = Array.FindIndex(logs, e => e.Name == "app.random_seed");
        var results = logs.Select((entry, index) => (entry, index)).Where(x => x.entry.Name == ResultsKey).ToArray();
        Assert.Equal(new[] { "Found 507 animation files of type 1", "Found 1 animation files of type 3",
            "Found 37 animation files of type 2", "Found 293 animation files of type 0" }, results.Select(x => x.entry.Text));
        Assert.True(seed >= 0); Assert.All(results, x => Assert.True(x.index > seed));
        int clips = Array.FindIndex(logs, e => e.Name == "RobotDataLoader.LoadAnimationsInternal.LoadTime");
        int backpack = Array.FindIndex(logs, e => e.Name == "RobotDataLoader.LoadBackpackLightAnimations.LoadTime");
        Assert.True(clips > results.Last().index);
        Assert.True(backpack > clips);
        Assert.Single(logs.Where(e => e.Name == "RobotDataLoader.LoadBackpackLightAnimations.LoadTime"));
        Assert.NotNull(robot.Lights.Body.Animations.Get("Charging"));
        Assert.NotNull(robot.Animations.FaceAnimations);
    }

    [Fact]
    public void B_M5_6_DeferredStatFailureWarnsOnceInCollectOrder()
    {
        // Collect's four walks: 0x0051F58A/5DA/62C/67E. LoadBackpackLightAnimations has no walk.
        string root = Path.Combine(Path.GetTempPath(), "m5-stat-order-" + Guid.NewGuid());
        string[] dirs = { "assets/animations", "config/engine/lights/cubeLights",
            "config/engine/lights/backpackLights", "assets/animationGroups" };
        var entries = new ConcurrentQueue<EngineLogEntry>();
        try
        {
            foreach (string dir in dirs)
            {
                Directory.CreateDirectory(Path.Combine(root, dir));
                File.WriteAllText(Path.Combine(root, dir, "good.json"), dir == dirs[0] ? "{\"good\":[]}" : "{}");
                File.WriteAllText(Path.Combine(root, dir, "fail.json"), "{}");
            }
            // Delete between enumeration and stat, exercising the real FileInfo failure in both walks.
            using var robot = CozmoRobot.CreateForTest(new Port(), () => 1_000_000_000,
                new CozmoEngineOptions { ResourcesPath = root, BlockPoolPath = "" },
                engineCreated: engine => engine.LogEntry += entries.Enqueue,
                beforeAnimationFileStat: path => { if (Path.GetFileName(path) == "fail.json") File.Delete(path); });
            Assert.DoesNotContain(entries, e => e.Name == "RobotDataLoader.WalkAnimationDir");
            foreach (string dir in dirs) File.WriteAllText(Path.Combine(root, dir, "fail.json"), "{}");
            robot.Animations.LoadFrom(root);
            var warnings = entries.Where(e => e.Name == "RobotDataLoader.WalkAnimationDir").ToArray();
            Assert.Equal(4, warnings.Length);
            for (int i = 0; i < dirs.Length; i++)
            {
                Assert.Equal(EngineLogLevel.Warning, warnings[i].Level);
                Assert.Null(warnings[i].Channel);
                Assert.EndsWith("could not get mtime for " + Path.GetFullPath(Path.Combine(root, dirs[i], "fail.json")),
                    warnings[i].Text.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
            }
            Assert.Single(warnings.Where(e => e.Text.Contains("backpackLights")));
            Assert.Single(entries.Where(e => e.Name == "RobotDataLoader.LoadBackpackLightAnimations.LoadTime"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void B_M5_6_DisposeReportsPendingLoaderLogsOnlyOnce(bool load)
    {
        string root = Path.Combine(Path.GetTempPath(), "m5-pending-logs-" + Guid.NewGuid());
        var diagnostics = new ConcurrentQueue<string>();
        var entries = new ConcurrentQueue<EngineLogEntry>();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "assets", "animations"));
            Directory.CreateDirectory(Path.Combine(root, "config", "engine", "lights", "backpackLights"));
            var robot = CozmoRobot.CreateForTest(new Port(), () => 1_000_000_000,
                new CozmoEngineOptions { ResourcesPath = root, BlockPoolPath = "" },
                engineCreated: engine => { engine.StackLogLine += diagnostics.Enqueue; engine.LogEntry += entries.Enqueue; });
            try { if (load) robot.Animations.LoadFrom(root); }
            finally { robot.Dispose(); }
            robot.Dispose();
            var missing = diagnostics.Where(l => l.StartsWith("MISSING: M4-036 deferred light loader logs:")).ToArray();
            if (load) Assert.Empty(missing);
            else Assert.Equal("MISSING: M4-036 deferred light loader logs: disposed with 1 captured loader logs still pending; the engine never loads lights outside LoadNonConfigData", Assert.Single(missing));
            Assert.DoesNotContain(entries, e => e.Text.StartsWith("MISSING:"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void B_M5_4_GroupDefinitionPrecedesBackpackLoadTime()
    {
        string root = Path.Combine(Path.GetTempPath(), "m5-loading-order-" + Guid.NewGuid());
        try
        {
            foreach (string dir in new[] { "assets/animations", "assets/animationGroups", "config/engine/lights/cubeLights", "config/engine/lights/backpackLights" })
            {
                Directory.CreateDirectory(Path.Combine(root, dir));
                File.WriteAllText(Path.Combine(root, dir, "a.json"), dir == "assets/animations" ? "{\"a\":[]}" : "{}");
            }
            using var robot = CozmoRobot.CreateForTest(new Port(), () => 1_000_000_000,
                new CozmoEngineOptions { ResourcesPath = root, BlockPoolPath = "" });
            var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
            robot.Animations.LoadFrom(root);
            // NoAnimations is the checked group failure before the next loader step, 0x0058B89E..0x0058B8B2.
            int group = entries.FindIndex(e => e.Name == "AnimationGroup.DefineFromJson.NoAnimations");
            int backpack = entries.FindIndex(e => e.Name == "RobotDataLoader.LoadBackpackLightAnimations.LoadTime");
            Assert.True(group >= 0); Assert.True(backpack > group);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void B_M5_4_SecondProductionLibraryLoadRefusesTheUncheckedCollectHistory()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Animations.LoadFrom(Resources());
        var entries = new ConcurrentQueue<EngineLogEntry>(); robot.Engine.LogEntry += entries.Enqueue;
        var diagnostics = new ConcurrentQueue<string>(); robot.Engine.StackLogLine += diagnostics.Enqueue;
        robot.Animations.LoadFrom(Resources());
        Assert.DoesNotContain(entries, e => e.Name == ResultsKey);
        Assert.Single(diagnostics.Where(l => l.StartsWith("MISSING: M5-046") && l.Contains("non-fresh map")));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void R46_8_AbsentTypeEmitsMissingInsteadOfAssumingOrder(int absent)
    {
        string root = Path.Combine(Path.GetTempPath(), "m5-last-reads-" + Guid.NewGuid());
        var dirs = new[] { "assets/animations", "assets/animationGroups", "config/engine/lights/cubeLights", "config/engine/lights/backpackLights" };
        try
        {
            foreach (string dir in dirs) Directory.CreateDirectory(Path.Combine(root, dir));
            for (int type = 0; type < 4; type++)
                if (type != absent) File.WriteAllText(Path.Combine(root, dirs[type], "a.json"), type == 0 ? "{\"a\":[]}" : "{}");
            using var robot = CozmoRobot.CreateOffline();
            var entries = new ConcurrentQueue<EngineLogEntry>(); robot.Engine.LogEntry += entries.Enqueue;
            var diagnostics = new ConcurrentQueue<string>(); robot.Engine.StackLogLine += diagnostics.Enqueue;
            robot.Animations.LoadFrom(root);
            Assert.Single(diagnostics.Where(l => l.StartsWith("MISSING: M5-046")));
            Assert.DoesNotContain(entries, e => e.Name == ResultsKey);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void R46_8_NonFreshMapEmitsMissingInsteadOfAssumingOrder()
    {
        var diagnostics = new List<string>(); var entries = new List<string>();
        AnimationLibrary.EmitCollectResults(new[] { 1, 1, 1, 1 }, false, diagnostics.Add,
            (_, _, _, text) => entries.Add(text));
        Assert.Single(diagnostics); Assert.StartsWith("MISSING: M5-046", diagnostics[0]); Assert.Empty(entries);
    }

    private static string Resources()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "re-analysis", "obb", "assets", "cozmo_resources");
            if (Directory.Exists(path)) return path;
        }
        throw new DirectoryNotFoundException("Shipped resources required for R46-7 regression");
    }
}

