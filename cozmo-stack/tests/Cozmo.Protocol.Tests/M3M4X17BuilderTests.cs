using System.Collections.Concurrent;
using System.Text;
using Cozmo.Robot;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M3M4X17BuilderTests
{
    private static string Repo()
    { for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent) if(File.Exists(Path.Combine(d.FullName,"PROJECT_STATE.md")))return d.FullName;throw new Exception(); }
    private static string Pattern(string name, uint duration, byte red=11, bool over=true) => $$"""
        {"pattern":{"onColors":[[{{red}},2,3,255],[{{red}},2,3,255],[{{red}},2,3,255],[{{red}},2,3,255]],
        "offColors":[[1,1,1,255],[1,1,1,255],[1,1,1,255],[1,1,1,255]],
        "onPeriod_ms":[1,1,1,1],"offPeriod_ms":[2,2,2,2],"transitionOnPeriod_ms":[0,0,0,0],
        "transitionOffPeriod_ms":[0,0,0,0],"offset":[0,0,0,0],"rotationPeriod_ms":0},
        "duration_ms":{{duration}},"canBeOverridden":{{(over?"true":"false")}},"patternDebugName":"{{name}}"}
        """;
    private static void Define(CubeLightAnimations library,string name,params string[] patterns)
    { Assert.True(Json.TryParseFirst(Encoding.UTF8.GetBytes("{\""+name+"\":["+string.Join(",",patterns)+"]}"),out var doc));using(doc)library.Define(doc.RootElement,null); }
    private static CubeLightModifier Modifier => new(Enumerable.Repeat(LedPattern.Solid(new LedColor(0,0,0,0)),4).ToArray(),0,0,default);
    [Theory]
    [InlineData("WakeUp","wakeUp")] [InlineData("Sleep","sleep")]
    public void ShippedLastPatternRejectsLayerTwoOverride(string trigger,string name)
    {
        string root=M4ControlTests.CubeLightResources();
        try
        {
            string shipped=Path.Combine(Repo(),"re-analysis/obb/assets/cozmo_resources",CubeLightAnimations.AnimationDirectory);
            File.Copy(Directory.EnumerateFiles(shipped,name+".json",SearchOption.AllDirectories).Single(),Path.Combine(root,CubeLightAnimations.AnimationDirectory,name+".json"),true);
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
            var c=rig.Robot.Lights.Cubes;
            if(trigger=="Sleep") Assert.True(c.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,trigger,2));
            var entries=new ConcurrentQueue<EngineLogEntry>();rig.Engine.LogEntry+=entries.Enqueue;
            // Both shipped files begin overridable and end false; 0x6386EE loads __prev_, the last node.
            Assert.True(c.Animations.ForTrigger(trigger)![0].CanBeOverridden);
            Assert.False(c.Animations.ForTrigger(trigger)![^1].CanBeOverridden);
            Assert.False(c.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Visible",2));
            Assert.Contains(entries,e=>e.Name=="CubeLightComponent.PlayLightAnim.CantBeOverridden" && e.Level==EngineLogLevel.Info);
        }
        finally {Directory.Delete(root,true);}
    }
    [Fact]
    public void BuriedBlendedEntryResumesWithTheLastAssignmentToItsLayerSlot()
    {
        string root=M4ControlTests.CubeLightResources();
        try
        {
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
            var c=rig.Robot.Lights.Cubes;var type=ObjectType.Block_LIGHTCUBE1;
            Define(c.Animations,"sleep",Pattern("A",1000,11));
            Define(c.Animations,"visible",Pattern("B",1000,22));
            Define(c.Animations,"wakeUp",Pattern("C",1000,33));
            Assert.True(c.PlayLightAnim(type,"Sleep",1,modifier:true,lights:Modifier));
            Assert.True(c.PlayLightAnim(type,"Visible",1));
            Assert.True(c.PlayLightAnim(type,"WakeUp",1,modifier:true,lights:Modifier));
            c.StopLightAnim("WakeUp",1,pickDefaultLayer:false);
            Assert.Equal("B",c.PlayingForTest(type,1)!.Value.Patterns[0].DebugName);
            c.StopLightAnim("Visible",1,pickDefaultLayer:false);
            var resumed=c.PlayingForTest(type,1)!.Value;
            Assert.Equal("sleep",resumed.Name);Assert.True(resumed.Blended);Assert.Equal(1,resumed.Count);
            // assign at 0x6385B6 mutates the slot retained by A (0x638698..A8).
            Assert.Equal("C",resumed.Patterns[0].DebugName);Assert.Equal((byte)33,resumed.Patterns[0].Leds[0].OnColor.R);
        }
        finally {Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData(0)] [InlineData(7)] [InlineData(-9)]
    public void AdvancingUsesStoredOffsetAndHoldsOnlyOnZeroTimer(int offset)
    {
        string root=M4ControlTests.CubeLightResources();
        try
        {
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
            var c=rig.Robot.Lights.Cubes;var type=ObjectType.Block_LIGHTCUBE1;
            Define(c.Animations,"sleep",Pattern("first",9),Pattern("second",0));
            Assert.True(c.PlayLightAnim(type,"Sleep",1,timerOffsetMs:offset));
            rig.Tick(60);
            uint now=rig.Engine.Timer.TimeStampMs;
            var second=c.PlayingForTest(type,1)!.Value;
            // 0x637B3A clears, then ORs duration and +0x44 before adding now + dur + offset.
            Assert.Equal(offset==0?0:unchecked(now+(uint)offset),second.Timer);
            if(offset==0) { rig.Tick(600);Assert.Equal(0u,c.PlayingForTest(type,1)!.Value.Timer); }
            else { rig.Tick(60);Assert.Null(c.PlayingForTest(type,1)); }
        }
        finally {Directory.Delete(root,true);}
    }
    [Fact]
    public void BadTriggerPairIsSkippedAndMissingPathsKeepVisibleWarnings()
    {
        string root=M4ControlTests.CubeLightResources();var logs=new List<EngineLogEntry>();var stack=new List<string>();
        SourceLogSink sink=(l,c,k,t)=>logs.Add(new(l,c,k,t,new Dictionary<string,string>()));
        try
        {
            string map=Path.Combine(root,CubeLightAnimations.TriggerMapPath);
            File.WriteAllText(map,"{\"Pairs\":[{\"CladEvent\":\"Bad\"},{\"CladEvent\":\"Sleep\",\"AnimName\":\"sleep\"}]}");
            var library=CubeLightAnimations.Load(root,sourceLog:sink);Assert.NotNull(library.ForTrigger("Sleep"));Assert.Null(library.ForTrigger("Bad"));
            File.Delete(map);Directory.Delete(Path.Combine(root,CubeLightAnimations.AnimationDirectory),true);
            logs.Clear();CubeLightAnimations.Load(root,log:stack.Add,sourceLog:sink);
            Assert.Equal(new[]{"Stack.CubeLightLoader.TriggerMapNotFound: cube light trigger map not found at "+map,"Stack.CubeLightLoader.AnimationsNotFound: cube light animations not found at "+Path.Combine(root,CubeLightAnimations.AnimationDirectory)},stack);
            var entry=Assert.Single(logs);Assert.Equal("RobotDataLoader.LoadCubeLightAnimations.LoadTime",entry.Name);Assert.Equal(EngineLogLevel.Info,entry.Level);
        }
        finally {Directory.Delete(root,true);}
    }
}
