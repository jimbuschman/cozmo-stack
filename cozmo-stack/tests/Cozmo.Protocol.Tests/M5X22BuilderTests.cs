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

    // reach: unit (file enumeration order supplied to the real container loader; no substituted reader or parse result).
    [Fact]
    public void ReadAsJsonFalseBetweenGoodFilesContinuesWithoutRollingBackOtherFiles()
    {
        using var f=new Fixture();
        var first=f.Write("first.json","{\"Pairs\":[{\"CladEvent\":\"BlockReact\",\"AnimName\":\"first\"}]}");
        var bad=f.Write("bad.json","{");
        var last=f.Write("last.json","{\"Pairs\":[{\"CladEvent\":\"DriveStartHappy\",\"AnimName\":\"last\"}]}");
        var map=AnimationTriggerMap.LoadFiles(new[]{first,bad,last},f.Maps,f.Stack.Add,(l,c,k,t)=>f.Rig.Engine.EmitSourceLog(l,c,k,t));
        Assert.Equal("first",map.GroupFor(AnimationTrigger.BlockReact));Assert.Equal("last",map.GroupFor(AnimationTrigger.DriveStartHappy));
        Assert.Equal(2,map.Count);Assert.True(EngineErrorState.ErrorFlagSet);
        Assert.Single(f.Logs.Where(e=>e.Name=="DataPlatform.readAsJson"));Assert.DoesNotContain(f.Stack,s=>s.Contains("M1-034"));
    }

    // reach: unit (ordered files supplied; duplicate pair still evaluates AnimName.asString before insertion).
    [Theory][InlineData("[]")][InlineData("{}")]
    public void DuplicateKeyWithAggregateAnimNameStillTerminatesItsFile(string aggregate)
    {
        using var f=new Fixture();
        var first=f.Write("first.json","{\"Pairs\":[{\"CladEvent\":\"BlockReact\",\"AnimName\":\"prior\"}]}");
        var bad=f.Write("bad.json","{\"Pairs\":[{\"CladEvent\":\"AcknowledgeObject\",\"AnimName\":\"rollback\"},{\"CladEvent\":\"BlockReact\",\"AnimName\":"+aggregate+"}]}");
        var last=f.Write("last.json","{\"Pairs\":[{\"CladEvent\":\"DriveStartHappy\",\"AnimName\":\"last\"}]}");
        var map=AnimationTriggerMap.LoadFiles(new[]{first,bad,last},f.Maps,f.Stack.Add,null);
        Assert.Equal("prior",map.GroupFor(AnimationTrigger.BlockReact));Assert.Equal("last",map.GroupFor(AnimationTrigger.DriveStartHappy));
        Assert.Null(map.GroupFor(AnimationTrigger.AcknowledgeObject));Assert.Single(f.Stack.Where(s=>s.Contains("asString throw")));
        Assert.False(EngineErrorState.ErrorFlagSet);
    }

    // reach: unit (host reload with a depth-limit file; native terminates, M1-034 skips it and preserves previous map).
    [Fact]
    public void DepthFatalCountsAsTerminatingForTheHostReloadPolicyAndNamesItsOwner()
    {
        using var f=new Fixture();
        var file=f.Write("depth.json","{\"Pairs\":[{\"CladEvent\":\"BlockReact\",\"AnimName\":\"prior\"}]}");
        var previous=AnimationTriggerMap.LoadFiles(new[]{file},f.Maps,f.Stack.Add,null);
        File.WriteAllText(file,new string('[',1001)+"0"+new string(']',1001));
        var reloaded=AnimationTriggerMap.LoadFiles(new[]{file},f.Maps,f.Stack.Add,null,previous);
        Assert.Same(previous,reloaded);
        var skip=Assert.Single(f.Stack.Where(s=>s.Contains("stack-limit fatal path")));
        Assert.Contains("M1-034",skip);Assert.Contains("M5-034",skip);Assert.Contains(file,skip);
        Assert.False(EngineErrorState.ErrorFlagSet);
    }

    // reach: production - live robot loader/constructor or public input with actual component wiring; fixture assets supplied where stated.
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

    // reach: production - live robot loader/constructor or public input with actual component wiring; fixture assets supplied where stated.
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

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void CrossFileFirstInsertFollowsTheSuppliedFileOrder()
    {
        using var f = new Fixture();
        var first = f.Write("one.json", "{\"Pairs\":[{\"CladEvent\":\"BlockReact\",\"AnimName\":\"one\"}]}");
        var second = f.Write("two.json", "{\"Pairs\":[{\"CladEvent\":\"BlockReact\",\"AnimName\":\"two\"}]}");
        Assert.Equal("one", AnimationTriggerMap.LoadFiles(new[] { first, second }, f.Maps, f.Stack.Add, null).GroupFor(AnimationTrigger.BlockReact));
        Assert.Equal("two", AnimationTriggerMap.LoadFiles(new[] { second, first }, f.Maps, f.Stack.Add, null).GroupFor(AnimationTrigger.BlockReact));
    }

    // reach: production - live robot loader/constructor or public input with actual component wiring; fixture assets supplied where stated.
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

    // reach: unit for supplied-order LoadFiles; production for the LoadFrom half using the real recursive loader; malformed file inputs are explicit fixtures.
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

    // reach: production - live robot loader/constructor or public input with actual component wiring; fixture assets supplied where stated.
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
