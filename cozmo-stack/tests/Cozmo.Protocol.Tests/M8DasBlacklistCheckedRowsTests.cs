using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M8DasBlacklistCheckedRowsTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly string Root=Directory.CreateTempSubdirectory("cozmo-das-blacklist-").FullName;
        internal readonly CozmoEngine Engine;
        internal readonly List<EngineLogEntry> Logs=new();
        internal readonly List<string> Missing=new();
        private readonly bool _error=EngineErrorState.ErrorFlagSet;
        internal string FilePath=>Root+"/"+RobotManager.DasConfigRelativePath;
        internal Fixture(string json="{}")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);File.WriteAllText(FilePath,json);
            Engine=new CozmoEngine(new M4ControlTests.FakePort(),new CozmoEngineOptions{ResourcesPath=Root},()=>1_000_000_000);
            Engine.LogEntry+=Logs.Add;SteppedBehavior.MissingReported+=Missing.Add;
        }
        internal void Load(string json){Assert.True(Json.TryParseFirst(Encoding.UTF8.GetBytes(json),out var doc));Engine.Robots.LoadDasBlacklistedAnimationTriggers(doc.RootElement);}
        public void Dispose(){SteppedBehavior.MissingReported-=Missing.Add;Engine.Dispose();Directory.Delete(Root,true);EngineErrorState.ErrorFlagSet=_error;}
    }

    [Fact]
    public void LiveEngineConstructionLoadsTheSixShippedTriggerOrdinals()
    {
        string? shipped=null;
        for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent)
        {var candidate=Path.Combine(directory.FullName,"re-analysis/obb/assets/cozmo_resources/config/engine/das_event_config.json");if(File.Exists(candidate)){shipped=candidate;break;}}
        Assert.NotNull(shipped);
        using var f=new Fixture(File.ReadAllText(shipped));
        // Native table pairs cited in OV22, not values taken from the port's enum.
        Assert.Equal(new[]{0x98,0x9A,0x9C,0x9E,0xA3,0x146},f.Engine.Robots.DasBlacklistedTriggers.Select(t=>(int)t).Order().ToArray());
    }

    [Fact]
    public void LoaderUsesTheLiteralKeyAndKeepsUniqueEntriesAcrossLoads()
    {
        using var f=new Fixture();
        f.Load("{\"DasBlacklistedAnimationTriggers\":[\"NeutralFace\"]}");Assert.Empty(f.Engine.Robots.DasBlacklistedTriggers);
        f.Load("{\"blacklisted_animation_triggers\":[\"NeutralFace\",\"NeutralFace\"]}");
        f.Load("{\"blacklisted_animation_triggers\":[\"DroneModeForwardDrivingStart\"]}");
        f.Load("{}");Assert.Equal(new[]{0x9E,0x146},f.Engine.Robots.DasBlacklistedTriggers.Select(t=>(int)t).Order().ToArray());
    }

    [Fact]
    public void UnknownNameWritesAndFlushesRawStderrThenInsertsEnumZero()
    {
        using var f=new Fixture();using var stderr=new FlushCountingStream();f.Engine.Robots.CerrOverride=stderr;
        f.Load("{\"blacklisted_animation_triggers\":[\"neutralface\",\"0\"]}");
        Assert.Equal(new[]{0},f.Engine.Robots.DasBlacklistedTriggers.Select(t=>(int)t).ToArray());
        Assert.Equal("error: string 'neutralface' is not a valid AnimationTrigger value\nerror: string '0' is not a valid AnimationTrigger value\n",Encoding.UTF8.GetString(stderr.ToArray()));
        Assert.Equal(2,stderr.FlushCount);Assert.Empty(f.Logs);
    }
    private sealed class FlushCountingStream : MemoryStream
    {internal int FlushCount;public override void Flush(){FlushCount++;base.Flush();}}

    [Theory]
    [InlineData("null")][InlineData("{}")][InlineData("{\"blacklisted_animation_triggers\":{}}")]
    [InlineData("{\"blacklisted_animation_triggers\":3}")][InlineData("{\"blacklisted_animation_triggers\":[]}")]
    public void SizeZeroLeavesThePriorSetUnchanged(string json)
    {
        using var f=new Fixture();f.Load("{\"blacklisted_animation_triggers\":[\"NeutralFace\"]}");f.Load(json);
        Assert.Equal(new[]{0x146},f.Engine.Robots.DasBlacklistedTriggers.Select(t=>(int)t).ToArray());
    }

    [Theory]
    [InlineData("[]")][InlineData("true")][InlineData("{\"blacklisted_animation_triggers\":{\"x\":1}}")]
    [InlineData("{\"blacklisted_animation_triggers\":[\"DroneModeForwardDrivingStart\",{}]}")]
    public void TerminatingWrongKindSkipsTheConfigAndKeepsThePriorSet(string json)
    {
        using var f=new Fixture();f.Load("{\"blacklisted_animation_triggers\":[\"NeutralFace\"]}");f.Load(json);
        Assert.Equal(new[]{0x146},f.Engine.Robots.DasBlacklistedTriggers.Select(t=>(int)t).ToArray());Assert.Empty(f.Logs);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void MissingOrEmptyReadKeepsNullAndEmitsExactErrorsWithTheDebugGapVisible(bool empty)
    {
        using var f=new Fixture();if(empty)File.WriteAllText(f.FilePath,"");else File.Delete(f.FilePath);EngineErrorState.ErrorFlagSet=false;
        var root=RobotManager.ReadDasEventConfig(f.Root,f.Engine);Assert.Equal(FirmwareJsonKind.Null,root.Kind);
        Assert.Equal(new[]{"DataPlatform.readAsJson","RobotDataLoader.DasEventConfigJsonNotFound"},f.Logs.Select(l=>l.Name));
        Assert.All(f.Logs,l=>Assert.Equal(EngineLogLevel.Error,l.Level));Assert.True(EngineErrorState.ErrorFlagSet);
        Assert.EndsWith($"Failed to read [{f.FilePath}]",f.Logs[0].Text);
        Assert.EndsWith("DAS Event Json config file config/engine/das_event_config.json not found or failed to parse",f.Logs[1].Text);
        Assert.Contains(f.Missing,m=>m.Contains("M8-022/M5-051")&&m.Contains(f.FilePath)&&m.Contains("getFormattedErrorMessages"));
        f.Engine.Robots.LoadDasBlacklistedAnimationTriggers(root);Assert.Empty(f.Engine.Robots.DasBlacklistedTriggers);
    }

    [Fact]
    public void FailedParseStillHandsItsPartialRootToTheLoader()
    {
        using var f=new Fixture();File.WriteAllText(f.FilePath,"{\"blacklisted_animation_triggers\":[\"NeutralFace\"],");
        var root=RobotManager.ReadDasEventConfig(f.Root,f.Engine);f.Engine.Robots.LoadDasBlacklistedAnimationTriggers(root);
        Assert.Equal(new[]{0x146},f.Engine.Robots.DasBlacklistedTriggers.Select(t=>(int)t).ToArray());Assert.Equal(2,f.Logs.Count);
    }

    [Theory]
    [InlineData(9_999_999L,"9")][InlineData(-9_999_999L,"-9")][InlineData(999_999L,"0")]
    public void InitEventFollowsLoadAndUsesSignedIntegerNsDifferenceTruncatedToMs(long difference,string expected)
    {
        using var f=new Fixture();Assert.True(Json.TryParseFirst("{\"blacklisted_animation_triggers\":[\"NeutralFace\"]}"u8.ToArray(),out var document));
        f.Engine.Handler.Initialised=false;int calls=0;
        long Clock()
        {
            if(calls++==0){Assert.False(f.Engine.Handler.Initialised);Assert.Empty(f.Engine.Robots.DasBlacklistedTriggers);return 3_000_000_000;}
            Assert.True(f.Engine.Handler.Initialised);Assert.Equal(new[]{0x146},f.Engine.Robots.DasBlacklistedTriggers.Select(t=>(int)t).ToArray());Assert.Empty(f.Logs);
            return 3_000_000_000+difference;
        }
        f.Engine.Robots.InitCheckedDasTail(document.RootElement,Clock);
        var entry=Assert.Single(f.Logs);Assert.Equal(2,calls);Assert.Equal("robot.init.time_spent_ms",entry.Name);
        Assert.Equal(EngineLogLevel.Event,entry.Level);Assert.Null(entry.Channel);Assert.Empty(entry.Data);Assert.Equal(expected,entry.Text);
    }
}
