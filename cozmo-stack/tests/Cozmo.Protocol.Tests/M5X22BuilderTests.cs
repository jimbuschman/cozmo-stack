using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M5X22BuilderTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly string Root = Directory.CreateTempSubdirectory("b522-").FullName;
        internal string Maps => Path.Combine(Root, "assets", "animationGroupMaps");
        internal readonly M4ControlTests.Rig Rig = new();
        internal readonly List<string> Stack = new();
        internal readonly List<EngineLogEntry> Logs = new();
        private readonly bool _oldError = EngineErrorState.ErrorFlagSet;
        internal Fixture()
        {
            Directory.CreateDirectory(Maps);
            Directory.CreateDirectory(Path.Combine(Root, "assets", "animations"));
            File.WriteAllText(Path.Combine(Root, "assets", "animations", "a.json"), "{\"a\":[]}");
            Rig.Engine.StackLogLine += Stack.Add; Rig.Engine.LogEntry += Logs.Add;
            EngineErrorState.ErrorFlagSet = false;
        }
        internal string Write(string name, string text)
        {
            var file = Path.Combine(Maps, name); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, text); return file;
        }
        public void Dispose() { Rig.Dispose(); Directory.Delete(Root, true); EngineErrorState.ErrorFlagSet = _oldError; }
    }
    private static IReadOnlyDictionary<string, string> Pairs(AnimationTriggerMap map) =>
        (Dictionary<string, string>)typeof(AnimationTriggerMap).GetField("_groups", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(map)!;

    [Fact]
    public void LiveLoadUsesAllRecursiveJsonFilesAndIgnoresUnrelatedMaps()
    {
        using var f = new Fixture();
        f.Write("first.json", "{\"Pairs\":[{\"CladEvent\":\"DriveStartHappy\",\"AnimName\":\"first\"}]}");
        f.Write("nested/second.json", "{\"Pairs\":[{\"CladEvent\":\"DriveStartHappy\",\"AnimName\":\"duplicate\"},{\"CladEvent\":\"BlockReact\",\"AnimName\":\"second\"}]}");
        File.WriteAllText(Path.Combine(f.Root, "AnimationTriggerMap.json"), "{\"Pairs\":[{\"CladEvent\":\"AcknowledgeObject\",\"AnimName\":\"wrong-directory\"}]}");
        f.Write("ignored.txt", "{\"Pairs\":[{\"CladEvent\":\"AcknowledgeObject\",\"AnimName\":\"wrong-extension\"}]}");
        var library = f.Rig.Robot.Animations.LoadFrom(f.Root);
        var map = f.Rig.Robot.Animations.DrivingTriggerMap!;
        Assert.Equal(2, map.Count); Assert.Equal("first", map.GroupFor(AnimationTrigger.DriveStartHappy));
        Assert.Equal("second", map.GroupFor(AnimationTrigger.BlockReact)); Assert.Null(map.GroupFor(AnimationTrigger.AcknowledgeObject));
        Assert.Equal(2, library.Triggers.Count); Assert.Equal("second", library.Triggers.GetResponse(AnimationTrigger.BlockReact));
        Assert.Equal("first", library.Triggers.GetResponse(AnimationTrigger.DriveStartHappy));
        Assert.Contains("a", library.ClipNames);
    }

    [Fact]
    public void ASubdirectoryContributesAtItsEncounterPositionRatherThanAfterParentFiles()
    {
        using var f = new Fixture();
        f.Write("000-nested/first.json", "{\"Pairs\":[{\"CladEvent\":\"BlockReact\",\"AnimName\":\"nested\"}]}");
        f.Write("999-parent.json", "{\"Pairs\":[{\"CladEvent\":\"BlockReact\",\"AnimName\":\"parent\"}]}");
        // Filesystem entry order is external input (X9 G2b); traversal must consume a subtree at that entry.
        var firstInput = new DirectoryInfo(f.Maps).EnumerateFileSystemInfos().First();
        Assert.IsType<DirectoryInfo>(firstInput);
        f.Rig.Robot.Animations.LoadFrom(f.Root);
        Assert.Equal("nested", f.Rig.Robot.Animations.DrivingTriggerMap!.GroupFor(AnimationTrigger.BlockReact));
    }

    [Fact]
    public void CrossFileFirstInsertFollowsTheSuppliedFileOrder()
    {
        using var f = new Fixture();
        var first = f.Write("one.json", "{\"Pairs\":[{\"CladEvent\":\"BlockReact\",\"AnimName\":\"one\"}]}");
        var second = f.Write("two.json", "{\"Pairs\":[{\"CladEvent\":\"BlockReact\",\"AnimName\":\"two\"}]}");
        Assert.Equal("one", AnimationTriggerMap.LoadFiles(new[] { first, second }, f.Maps, f.Stack.Add, null).GroupFor(AnimationTrigger.BlockReact));
        Assert.Equal("two", AnimationTriggerMap.LoadFiles(new[] { second, first }, f.Maps, f.Stack.Add, null).GroupFor(AnimationTrigger.BlockReact));
    }

    [Theory]
    [InlineData("null", "", "")]
    [InlineData("{}", "", "")]
    [InlineData("{\"AnimName\":\"g\"}", "", "g")]
    [InlineData("{\"CladEvent\":\"BlockReact\"}", "BlockReact", "")]
    [InlineData("{\"CladEvent\":null,\"AnimName\":null}", "", "")]
    public void MissingOrNullMembersInsertJsonCppEmptyStrings(string pair, string key, string value)
    {
        using var f = new Fixture(); f.Write("map.json", "{\"Pairs\":[" + pair + "]}");
        f.Rig.Robot.Animations.LoadFrom(f.Root);
        var entry = Assert.Single(Pairs(f.Rig.Robot.Animations.DrivingTriggerMap!));
        Assert.Equal(key, entry.Key); Assert.Equal(value, entry.Value);
        Assert.DoesNotContain(f.Stack, line => line.Contains("M1-034"));
    }

    [Theory]
    [InlineData("{\"Pairs\":[{\"CladEvent\":\"AcknowledgeObject\",\"AnimName\":\"must-rollback\"},1]}")]
    [InlineData("{\"Pairs\":[true]}")]
    [InlineData("{\"Pairs\":[\"s\"]}")]
    [InlineData("{\"Pairs\":[[]]}")]
    [InlineData("{\"Pairs\":[{\"CladEvent\":[]}]}")]
    [InlineData("{\"Pairs\":[{\"CladEvent\":{}}]}")]
    [InlineData("{\"Pairs\":[{\"AnimName\":[]}]}")]
    [InlineData("{\"Pairs\":[{\"AnimName\":{}}]}")]
    [InlineData("{\"Pairs\":{\"x\":1}}")]
    [InlineData("[]")]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("\"root\"")]
    public void EveryTerminatingInputSkipsOnlyItsFileAndStillLoadsLaterFiles(string bad)
    {
        using var f = new Fixture();
        var prior = f.Write("prior.json", "{\"Pairs\":[{\"CladEvent\":\"BlockReact\",\"AnimName\":\"prior\"}]}");
        var crash = f.Write("crash.json", bad);
        var later = f.Write("later.json", "{\"Pairs\":[{\"CladEvent\":\"DriveStartHappy\",\"AnimName\":\"later\"}]}");
        var ordered = AnimationTriggerMap.LoadFiles(new[] { prior, crash, later }, f.Maps, f.Stack.Add, null);
        Assert.Equal(2, ordered.Count); Assert.Equal("prior", ordered.GroupFor(AnimationTrigger.BlockReact));
        Assert.Equal("later", ordered.GroupFor(AnimationTrigger.DriveStartHappy)); Assert.Null(ordered.GroupFor(AnimationTrigger.AcknowledgeObject));
        f.Stack.Clear();
        var library = f.Rig.Robot.Animations.LoadFrom(f.Root);
        Assert.Equal(2, library.Triggers.Count); Assert.Contains("a", library.ClipNames);
        var skip = Assert.Single(f.Stack.Where(line => line.Contains("M1-034 / M7-002")));
        Assert.Contains(crash, skip); Assert.Contains("M5-034", skip);
        Assert.DoesNotContain(f.Logs, entry => entry.Text.Contains("M1-034"));
        Assert.False(EngineErrorState.ErrorFlagSet);
    }

    [Theory]
    [InlineData("null")] [InlineData("{}")] [InlineData("1")] [InlineData("true")] [InlineData("\"s\"")]
    public void ZeroSizePairsNeverReachTheThrowingIntegerIndexer(string pairs)
    {
        // Json::Value::size 0x008E6E40..0x008E6E78; Load 0x006704BC..0x006704C6.
        using var f = new Fixture(); f.Write("empty.json", "{\"Pairs\":" + pairs + "}");
        f.Rig.Robot.Animations.LoadFrom(f.Root);
        Assert.Equal(0, f.Rig.Robot.Animations.DrivingTriggerMap!.Count);
        Assert.DoesNotContain(f.Stack, line => line.Contains("M1-034"));
        Assert.DoesNotContain(f.Logs, entry => entry.Level is EngineLogLevel.Error or EngineLogLevel.Warning);
    }
}
