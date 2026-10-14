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
    [Fact]
    public void ShippedShorterSlotSkipsFreedNodeResendAndKeepsOtherQueuedSends()
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
            rig.Tick(400); // B's send is queued; Dance ends and exposes stale A's index 1 against length 1.
            Assert.Contains(entries,e=>e.Name=="Stack.CubeLightComponent.SkipDanglingBlendedResend"&&e.Level==EngineLogLevel.Warning&&e.Text.Contains("M1-034 / M4-045"));
            Assert.Equal("speedTapWin",lights.PlayingForTest(a,1)!.Value.Name); // Entry retained, resend alone skipped.
            Assert.Contains(rig.RawSince(mark),bytes=>bytes[0]==(byte)new CubeID().Id&&BitConverter.ToUInt32(bytes,1)==1); // Other cube's queued send survived.
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
        string root=Path.Combine(Path.GetTempPath(),"absent-cube-"+Guid.NewGuid().ToString("N"));var entries=new List<(EngineLogLevel Level,string Key)>();
        CubeLightAnimations.Load(root,sourceLog:(level,_,key,_)=>entries.Add((level,key)));
        Assert.Equal(new[]{"Stack.CubeLightLoader.TriggerMapNotFound","Stack.CubeLightLoader.AnimationsNotFound","RobotDataLoader.LoadCubeLightAnimations.LoadTime"},entries.Select(e=>e.Key));
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
            robot.Animations.LoadFrom(root);Assert.Empty(robot.Lights.Cubes.Animations.AnimationNames);
        }
        finally{Directory.Delete(root,true);}
    }
    private sealed class Port:IEngineTransport
    {public bool TimedOut=>false;public event Action<ReceiverEvent>? Received{add{}remove{}}public void Start(){}public void Connect(System.Net.IPAddress ip,bool factory){}public void Disconnect(System.Net.IPEndPoint endpoint){}public void SendData(byte[] data){}}
}
