using System.Collections.Concurrent;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M3M4X21BuilderTests
{
    private static string Shipped()
    {for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent){var p=Path.Combine(d.FullName,"re-analysis/obb/assets/cozmo_resources");if(Directory.Exists(p))return p;}throw new InvalidOperationException("Shipped cube assets required");}
    private static CubeLightModifier Modifier=>new(Enumerable.Repeat(LedPattern.Solid(new LedColor(0,0,0,0)),4).ToArray(),0,0,default);
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ShippedShorterSlotSkipsFreedNodeResendAndKeepsOtherQueuedSends(bool stop)
    {
        string root=M4ControlTests.CubeLightResources();
        try
        {
            foreach(string name in new[]{"speedTapWin","flash","dancing_01"})
                File.Copy(Path.Combine(Shipped(),CubeLightAnimations.AnimationDirectory,name+".json"),Path.Combine(root,CubeLightAnimations.AnimationDirectory,name+".json"),true);
            File.Copy(Path.Combine(Shipped(),CubeLightAnimations.TriggerMapPath),Path.Combine(root,CubeLightAnimations.TriggerMapPath),true);
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();
            // Insert B first: its Update queues a send before A hits the dangling resend.
            M4ControlTests.ConnectCube(rig,slot:1,type:ObjectType.Block_LIGHTCUBE2);M4ControlTests.ConnectCube(rig,slot:0,type:ObjectType.Block_LIGHTCUBE1);
            var lights=rig.Robot.Lights.Cubes;var a=ObjectType.Block_LIGHTCUBE1;var b=ObjectType.Block_LIGHTCUBE2;
            Assert.True(lights.PlayLightAnim(a,"SpeedTapWin",1,modifier:true,lights:Modifier));
            rig.Tick(800);Assert.Equal("speedTapWin_flash",lights.TopPatternName(a)); // Shipped first duration 770; A index 1.
            Assert.True(lights.PlayLightAnim(a,"Dance_01",1)); // Non-blended 984 ms.
            Assert.True(lights.PlayLightAnim(a,"Flash",1,modifier:true,lights:Modifier)); // Shared slot now has one node, 600 ms.
            rig.Tick(610);Assert.Equal("dancing_01",lights.PlayingForTest(a,1)!.Value.Name);
            Assert.True(lights.PlayLightAnim(b,"SpeedTapWin",1,timerOffsetMs:-500)); // Non-blended first deadline +270 ms, then next-pattern send.
            int mark=rig.Mark();var entries=new ConcurrentQueue<EngineLogEntry>();rig.Engine.LogEntry+=entries.Enqueue;
            var stack=new ConcurrentQueue<string>();rig.Engine.StackLogLine+=stack.Enqueue;
            rig.Tick(400); // B's send is queued; Dance ends and exposes stale A's index 1 against length 1.
            Assert.Contains(stack,e=>e.StartsWith("Stack.CubeLightComponent.SkipDanglingBlendedResend:")&&e.Contains("M1-034 / M4-045"));
            Assert.DoesNotContain(entries,e=>e.Name.StartsWith("Stack.CubeLightComponent.SkipDangling"));
            Assert.Equal("speedTapWin",lights.PlayingForTest(a,1)!.Value.Name); // Entry retained, resend alone skipped.
            Assert.Contains(rig.RawSince(mark),bytes=>bytes[0]==(byte)new CubeID().Id&&BitConverter.ToUInt32(bytes,1)==1); // Other cube's queued send survived.
            if(stop)lights.StopLightAnim("SpeedTapWin",1,a);else rig.Tick(1);
            Assert.Contains(stack,e=>e.StartsWith("Stack.CubeLightComponent.SkipDanglingBlendedAdvance:")&&e.Contains(stop?"skipped Stop":"skipped advance")&&e.Contains("0x006379A6..0x006379AA"));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void DeferredHostTouchAndCollectedLoadParseAndLogOnlyOnce()
    {
        string root=M4ControlTests.CubeLightResources();var entries=new ConcurrentQueue<string>();
        try
        {
            SourceLogSink sink=(_,_,key,_)=>entries.Enqueue(key);
            var library=CubeLightAnimations.CreateDeferred(root,null,sink);
            Assert.NotNull(library.ForTrigger("Sleep"));
            library.LoadCollected(root,Directory.GetFiles(Path.Combine(root,CubeLightAnimations.AnimationDirectory)),null,sink);
            Assert.Single(entries.Where(k=>k=="RobotDataLoader.LoadCubeLightAnimations.LoadTime"));Assert.NotNull(library.ForTrigger("Sleep"));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void MissingDirectoryStillEmitsWarningsThenLoadTime()
    {
        string root=Path.Combine(Path.GetTempPath(),"absent-cube-"+Guid.NewGuid().ToString("N"));var entries=new List<(EngineLogLevel Level,string Key)>();var order=new List<string>();
        CubeLightAnimations.Load(root,log:line=>order.Add(line.Split(':')[0]),sourceLog:(level,_,key,_)=>{entries.Add((level,key));order.Add(key);});
        Assert.Equal(new[]{"Stack.CubeLightLoader.TriggerMapNotFound","Stack.CubeLightLoader.AnimationsNotFound","RobotDataLoader.LoadCubeLightAnimations.LoadTime"},order);
        Assert.Equal("RobotDataLoader.LoadCubeLightAnimations.LoadTime",Assert.Single(entries).Key);
        Assert.Equal(EngineLogLevel.Info,entries[^1].Level);
    }
    [Fact]
    public void NullResourceInputDoesNotFallBackToAnimationAssets()
    {
        string root=M4ControlTests.CubeLightResources();
        Directory.CreateDirectory(Path.Combine(root,"assets/animations"));
        try
        {
            using var robot=CozmoRobot.CreateForTest(new Port(),()=>0,new CozmoEngineOptions{ResourcesPath=null,BlockPoolPath=""});
            var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
            robot.Animations.LoadFrom(root);Assert.Empty(robot.Lights.Cubes.Animations.AnimationNames);
            Assert.Single(logs.Where(e=>e.Name=="RobotDataLoader.LoadCubeLightAnimations.LoadTime"));
        }
        finally{Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RegrownSlotDoesNotReviveAnIteratorFreedByEarlierShrink(bool modifier)
    {
        string root=M4ControlTests.CubeLightResources();
        try
        {
            foreach(string name in new[]{"speedTapWin","flash","dancing_01"})
                File.Copy(Path.Combine(Shipped(),CubeLightAnimations.AnimationDirectory,name+".json"),Path.Combine(root,CubeLightAnimations.AnimationDirectory,name+".json"),true);
            File.Copy(Path.Combine(Shipped(),CubeLightAnimations.TriggerMapPath),Path.Combine(root,CubeLightAnimations.TriggerMapPath),true);
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
            var lights=rig.Robot.Lights.Cubes;var type=ObjectType.Block_LIGHTCUBE1;
            var stack=new ConcurrentQueue<string>();var logs=new ConcurrentQueue<EngineLogEntry>();rig.Engine.StackLogLine+=stack.Enqueue;rig.Engine.LogEntry+=logs.Enqueue;
            Assert.True(lights.PlayLightAnim(type,"SpeedTapWin",1,modifier:modifier,lights:modifier?Modifier:null));Assert.True(lights.PlayingForTest(type,1)!.Value.Blended);rig.Tick(800); // A index 1; shipped duration 770.
            Assert.True(lights.PlayLightAnim(type,"Dance_01",1,durationMs:3000)); // B: 984+3000, ends at 4784 ms.
            Assert.True(lights.PlayLightAnim(type,"Flash",1,modifier:modifier,lights:modifier?Modifier:null));Assert.True(lights.PlayingForTest(type,1)!.Value.Blended);rig.Tick(610); // Shrink frees A node 1; Flash 600 ms ends.
            Assert.Equal("dancing_01",lights.PlayingForTest(type,1)!.Value.Name);
            Assert.True(lights.PlayLightAnim(type,"SpeedTapWin",1,modifier:modifier,lights:modifier?Modifier:null));Assert.True(lights.PlayingForTest(type,1)!.Value.Blended); // D grows shared slot to 2; false uses the shipped zero-color data.
            rig.Tick(780);rig.Tick(510);Assert.Equal("dancing_01",lights.PlayingForTest(type,1)!.Value.Name); // D 770/500 ms ends before B.
            int mark=rig.Mark();rig.Tick(2100); // B ends, A's freed iterator is still invalid although index 1 < length 2.
            Assert.Equal("speedTapWin",lights.PlayingForTest(type,1)!.Value.Name);
            Assert.Contains(stack,l=>l.StartsWith("Stack.CubeLightComponent.SkipDanglingBlendedResend:")&&l.Contains("current shared slot length 2"));
            Assert.DoesNotContain(rig.RawSince(mark),b=>b[0]==(byte)new CubeID().Id&&BitConverter.ToUInt32(b,1)==0);
            rig.Tick(1);Assert.Contains(stack,l=>l.StartsWith("Stack.CubeLightComponent.SkipDanglingBlendedAdvance:"));
            Assert.DoesNotContain(logs,l=>l.Name.StartsWith("Stack.CubeLightComponent.SkipDangling"));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void CollectedCubeFilesAndTriggerMapBothUseResourcesPath()
    {
        string resources=M4ControlTests.CubeLightResources();string assets=Path.Combine(Path.GetTempPath(),"cube-assets-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(assets,"assets/animations"));
        try
        {
            using var robot=CozmoRobot.CreateForTest(new Port(),()=>0,new CozmoEngineOptions{ResourcesPath=resources,BlockPoolPath=""});
            robot.Animations.LoadFrom(assets);
            Assert.NotNull(robot.Lights.Cubes.Animations.ForTrigger("Sleep")); // Definition is present only under ResourcesPath, never assetsRoot.
        }
        finally{Directory.Delete(resources,true);Directory.Delete(assets,true);}
    }
    [Fact]
    public void NullRootDirectLoadEmitsLoadTimeOnce()
    {
        var logs=new List<string>();CubeLightAnimations.Load(null,sourceLog:(_,_,key,_)=>logs.Add(key));
        Assert.Equal(new[]{"RobotDataLoader.LoadCubeLightAnimations.LoadTime"},logs);
    }
    [Fact]
    public void RepeatedFatalFileWithoutTextSinkIsANondeduplicatedEngineStackPolicySkip()
    {
        string root=M4ControlTests.CubeLightResources();
        try
        {
            string file=Path.Combine(root,CubeLightAnimations.AnimationDirectory,"fatal.bin");File.WriteAllText(file,"[1]");
            using var engine=new CozmoEngine(new Port(),new CozmoEngineOptions(),()=>0);
            var stack=new ConcurrentQueue<string>();var source=new ConcurrentQueue<EngineLogEntry>();engine.StackLogLine+=stack.Enqueue;engine.LogEntry+=source.Enqueue;
            SourceLogSink sink=(l,c,k,t)=>engine.EmitSourceLog(l,c,k,t);
            CubeLightAnimations.Load(root,sourceLog:sink);CubeLightAnimations.Load(root,sourceLog:sink);
            var skips=stack.Where(line=>line.Contains(file)&&line.Contains("Stack.CubeLightLoader.SkipFatalFile")).ToArray();
            Assert.Equal(2,skips.Length);Assert.Equal(skips[0],skips[1]);Assert.All(skips,line=>{Assert.StartsWith("Stack.CubeLightLoader.SkipFatalFile:",line);Assert.DoesNotContain("MISSING:",line);});
            Assert.DoesNotContain(source,e=>e.Name?.StartsWith("Stack.")==true);Assert.Equal(2,source.Count(e=>e.Name=="RobotDataLoader.LoadCubeLightAnimations.LoadTime"));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void MissingResourceNoticesWithoutTextSinkUseTheEngineStackChannel()
    {
        using var engine=new CozmoEngine(new Port(),new CozmoEngineOptions(),()=>0);
        var order=new List<string>();var source=new List<EngineLogEntry>();
        engine.StackLogLine+=line=>{if(line.StartsWith("Stack.CubeLightLoader."))order.Add(line.Split(':')[0]);};
        engine.LogEntry+=entry=>{source.Add(entry);order.Add(entry.Name!);};
        CubeLightAnimations.Load(Path.Combine(Path.GetTempPath(),"missing-lights-"+Guid.NewGuid().ToString("N")),sourceLog:(l,c,k,t)=>engine.EmitSourceLog(l,c,k,t));
        Assert.Equal(new[]{"Stack.CubeLightLoader.TriggerMapNotFound","Stack.CubeLightLoader.AnimationsNotFound","RobotDataLoader.LoadCubeLightAnimations.LoadTime"},order);
        Assert.Equal("RobotDataLoader.LoadCubeLightAnimations.LoadTime",Assert.Single(source).Name);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BackpackCollectUsesTheConstructorResourceRootAndNeverAnAssetsDecoy(bool supplied)
    {
        string resources=M4ControlTests.CubeLightResources(),assets=Directory.CreateTempSubdirectory("backpack-collect-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(assets,"animations"));
            string actual=Path.Combine(resources,BackpackLightAnimations.ResourceDirectory),decoy=Path.Combine(assets,BackpackLightAnimations.ResourceDirectory);
            Directory.CreateDirectory(actual);Directory.CreateDirectory(decoy);
            File.Copy(Directory.GetFiles(Path.Combine(Shipped(),BackpackLightAnimations.ResourceDirectory)).Single(),Path.Combine(actual,"actual.json"));
            File.WriteAllText(Path.Combine(decoy,"decoy1.json"),"{}");File.WriteAllText(Path.Combine(decoy,"decoy2.json"),"{}");
            using var robot=CozmoRobot.CreateForTest(new Port(),()=>0,new CozmoEngineOptions{ResourcesPath=supplied?resources:null,BlockPoolPath=""});
            var entries=new List<EngineLogEntry>();robot.Engine.LogEntry+=entries.Add;robot.Animations.LoadFrom(assets);
            var type3=entries.Where(e=>e.Name=="RobotDataLoader.CollectAnimFiles.Results"&&e.Text.EndsWith("type 3")).ToArray();
            if(supplied)Assert.Equal("Found 1 animation files of type 3",Assert.Single(type3).Text);else Assert.Empty(type3);
            Assert.Equal(supplied,robot.Lights.Body.Animations.Names.Contains("Charging"));
        }
        finally{Directory.Delete(resources,true);Directory.Delete(assets,true);}
    }
    private sealed class Port:IEngineTransport
    {public bool TimedOut=>false;public event Action<ReceiverEvent>? Received{add{}remove{}}public void Start(){}public void Connect(System.Net.IPAddress ip,bool factory){}public void Disconnect(System.Net.IPEndPoint endpoint){}public void SendData(byte[] data){}}
}
