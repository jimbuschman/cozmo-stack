using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M3M4SettlingBuilderTests
{
    private static string Shipped()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent)
        {var p=Path.Combine(d.FullName,"re-analysis/obb/assets/cozmo_resources");if(Directory.Exists(p))return p;}
        throw new DirectoryNotFoundException("shipped cube assets");
    }

    // reach: production — connection/disconnection packets, actual world identities, public PlayLightAnim and wire sends.
    [Theory][InlineData("Flash")][InlineData("SpeedTapWin")]
    public void ReconnectInsideWakeUpBlendsShippedAnimationFromTheNewObjectsZeroState(string trigger)
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();
        M4ControlTests.ConnectCube(rig);
        var before=rig.Robot.LocalizationWorld.ConnectedObjects.Single(o=>o.ActiveId==0);
        rig.Data(new ObjectConnectionState{ObjectID=0,FactoryID=0x1000,ObjectType=ObjectType.Block_LIGHTCUBE1,Connected=false});rig.Tick();
        M4ControlTests.ConnectCube(rig);
        var after=rig.Robot.LocalizationWorld.ConnectedObjects.Single(o=>o.ActiveId==0);
        Assert.NotSame(before,after);Assert.All(after.ActiveLedState,l=>Assert.Equal(default,l));
        int mark=rig.Mark();
        Assert.True(rig.Robot.Lights.Cubes.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,trigger));
        var words=Assert.Single(rig.SentSince(mark).OfType<CubeLights>()).Lights;
        Assert.Equal(4,words.Length);
        // flash.json: all on white; all off zero. speedTapWin.json: LED0 white, LEDs1-3 zero. White balance: 255/153/153.
        // Native packed RGB: alpha bit15 | red5<<10 | green5<<5 | blue5 = 0xFE73.
        if(trigger=="Flash")Assert.All(words,l=>{Assert.Equal((ushort)0xFE73,l.OnColor);Assert.Equal((ushort)0,l.OffColor);});
        else {Assert.Equal((ushort)0xFE73,words[0].OnColor);Assert.All(words.Skip(1),l=>Assert.Equal((ushort)0,l.OnColor));}
        Assert.True(rig.Robot.Lights.Cubes.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1)!.Value.Blended);
    }

    // reach: unit (substitutes internal ApplyRelativeObservation/located state; the production ObjectMoved join is covered in B32).
    [Fact]
    public void ActivePoseChangeReachesCubeLightsBeforeTheHostPoseNotification()
    {
        string root=M4ControlTests.CubeLightResources();
        try
        {
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
            var world=rig.Robot.LocalizationWorld;var connected=world.ConnectedObjects.Single(o=>o.ActiveId==0);
            var located=new ObservableObject(connected.ObjectId,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>())
                {PoseState=PoseState.Known,OriginId=world.CurrentOriginId,Pose=Pose3d.Identity};
            world.AddLocatedObject(located);
            bool atHost=false;
            world.PoseStateChanged+=(o,old,state)=>{if(o==located)atHost=rig.Log.Any(l=>l.Contains("Playing trigger Connected"));};
            int mark=rig.Mark();world.ApplyRelativeObservation(located,located.Pose,PoseState.Dirty,1);
            Assert.Equal(PoseState.Dirty,located.PoseState);Assert.True(atHost);
            Assert.Contains(rig.SentSince(mark),m=>m is CubeLights);
        }
        finally{Directory.Delete(root,true);}
    }

    // reach: production — public PlayLightAnim, actual Update expiry/stop; callback observes the entry already popped and reenters.
    [Theory][InlineData(false)][InlineData(true)]
    public void CompletionPopsBeforeCallbackAndAllowsReentrantPlayback(bool stop)
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var lights=rig.Robot.Lights.Cubes;int count=0;bool popped=false,played=false;
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Flash",completed:()=>
        {
            count++;popped=lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1) is null;
            played=lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Dance_01"); // native callback reentry is on this engine thread
        }));
        if(stop)lights.StopLightAnim("Flash",1,ObjectType.Block_LIGHTCUBE1);else rig.Tick(600);
        Assert.Equal(1,count);Assert.True(popped);Assert.True(played);Assert.Equal("dance_green",lights.TopPatternName(ObjectType.Block_LIGHTCUBE1));
        rig.Tick(10);Assert.Equal(1,count);
    }

    // reach: production — shipped playback/shrinking blend slot and real Update fault-skip; no forged iterator state.
    [Fact]
    public void FaultRetirementDoesNotReportSuccessfulAnimationCompletion()
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var lights=rig.Robot.Lights.Cubes;int completed=0;
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"SpeedTapWin",completed:()=>completed++));
        rig.Tick(800); // SpeedTapWin is at index1 (770ms first pattern).
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Dance_01")); // 984ms, ordinary list.
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Flash")); // Shared blend slot shrinks to one pattern, 600ms.
        rig.Tick(610);rig.Tick(400);rig.Tick(1); // Flash/Dance end; freed lower iterator is skipped, then retired.
        Assert.Equal(0,completed);
        Assert.Contains(rig.Log,s=>s.Contains("SkipDanglingBlendedAdvance"));
    }

    // reach: unit (internal Robot.Delocalize and seeded negative/positive ObjectIDs; native hardware producer is not asserted).
    [Fact]
    public void RealDelocalizePlaysConnectedOnNonCarriedCubesInSignedIdOrder()
    {
        string root=M4ControlTests.CubeLightResources();
        var ids=(Dictionary<ObjectType,uint>)typeof(ObjectIdSpace).GetField("s_uniqueIds",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        var saved=ids.ToArray();
        try
        {
            ObjectIdSpace.SeedUniqueIdForTests(ObjectType.Block_LIGHTCUBE1,0xFFFFFFFE);
            ObjectIdSpace.SeedUniqueIdForTests(ObjectType.Block_LIGHTCUBE2,1);
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();
            M4ControlTests.ConnectCube(rig,1,ObjectType.Block_LIGHTCUBE2);M4ControlTests.ConnectCube(rig,0);
            int[] expected={-2,1}; // signed order differs from u32: native ObjectInfo map walks -2 before +1
            var actual=new List<int>();
            rig.Engine.LogEntry+=e=>{if(e.Name=="CubeLightComponent.PlayLightAnim.SettingLights"&&e.Text.Contains("trigger Connected"))actual.Add(unchecked((int)uint.Parse(e.Text.Split("object ")[1])));};
            rig.Engine.Robot!.Delocalize(false);
            Assert.Equal(expected,actual);
        }
        finally{ids.Clear();foreach(var pair in saved)ids.Add(pair.Key,pair.Value);Directory.Delete(root,true);}
    }

    // reach: production — real dispatch and native admission; legacy test name retained for manifest references.
    [Fact]
    public void CliffHistoryExcludesRejectedFailedAndDuplicateStates()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        rig.Engine.Robot!.OfflineSeamAcceptsAnyOrigin=false;
        var history=rig.Engine.Robot.PoseHistory;
        var good=rig.MakeState(origin:rig.Engine.Robot.CurrentOriginId);rig.Data(good);rig.Tick();
        Assert.Equal(1,history.Count);
        rig.Data(good);rig.Tick();Assert.Equal(1,history.Count);
        rig.Data(rig.MakeState(origin:999));rig.Tick();Assert.Equal(1,history.Count);
        var gap=rig.MakeState(origin:rig.Engine.Robot.CurrentOriginId);gap.Timestamp=good.Timestamp+6000;
        rig.Data(gap);rig.Tick();Assert.Equal(1,history.Count);
    }

    // reach: production — real dispatch now admits history; legacy name retained for manifest references.
    [Fact]
    public void UnknownHistoryAdmissionIsVisibleAndDoesNotAdmitCliffSamples()
    {
        Cozmo.Robot.Behavior.SteppedBehavior.ResetMissingForTests();
        var missing=new List<string>();void Capture(string text)=>missing.Add(text);
        Cozmo.Robot.Behavior.SteppedBehavior.MissingReported+=Capture;
        try
        {
            using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
            Assert.Equal(1,rig.Engine.Robot!.PoseHistory.Count);
            Assert.DoesNotContain(missing,s=>s.Contains("M11-097 AddRobotStateToHistory result")||s.Contains("M11-097 C12.4"));
        }
        finally{Cozmo.Robot.Behavior.SteppedBehavior.MissingReported-=Capture;}
    }

    // reach: unit (direct Save with a real Windows sharing denial; no substituted delete/write implementation).
    [Fact]
    public void FailedPoolDeleteStillAttemptsTheWritableTruncatingOpen()
    {
        if(!OperatingSystem.IsWindows())return; // Windows denies unlink while a handle excludes FileShare.Delete.
        string root=Directory.CreateTempSubdirectory("pool-remove-result-").FullName;
        string path=Path.Combine(root,"pool.txt");
        try
        {
            var pool=new CubeConnections(_=>{},()=>0);pool.Init(path);
            File.WriteAllText(path,"prior");
            using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            {
                Assert.Throws<IOException>(()=>File.Delete(path));
                typeof(CubeConnections).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pool,null);
                Assert.Equal(0,held.Length); // remove failed, yet native's subsequent output-open still truncated the file.
            }
        }
        finally{Directory.Delete(root,true);}
    }

    // reach: production — public host tap-filter input, actual queued engine handling and robot construction.
    [Fact]
    public void TapFilterInputWithoutRobotIsDroppedAndNewRobotStartsEnabled()
    {
        using var rig=new M4ControlTests.Rig();
        rig.Robot.Cubes.SetBlockTapFilterEnabled(false);rig.Tick();Assert.True(rig.Robot.Cubes.BlockTapFilterEnabled);
        rig.ToSynced();Assert.True(rig.Robot.Cubes.BlockTapFilterEnabled);
        rig.Robot.Cubes.SetBlockTapFilterEnabled(false);rig.Tick();Assert.False(rig.Robot.Cubes.BlockTapFilterEnabled);
        rig.Engine.Robots.RemoveRobot(1,false);rig.Robot.Cubes.SetBlockTapFilterEnabled(false);rig.Tick();Assert.True(rig.Robot.Cubes.BlockTapFilterEnabled);
    }

    // reach: production — public startup/connect, framed DefaultCameraParams, concurrent public host commands and real ticker.
    [Fact]
    public async Task CameraHostCommandsAndDefaultsKeepWholePairsOnTheEngineThread()
    {
        string root=Directory.CreateTempSubdirectory("camera-host-thread-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));File.WriteAllText(Path.Combine(root,"animations","a.json"),"{\"a\":[]}");
            var pairs=new ConcurrentQueue<(int,float)>();var nativeThreads=new ConcurrentQueue<int>();int engineThread=0;
            CameraSettings? tickCamera=null;
            using var robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions
            {
                ResourcesPath=root,BlockPoolPath="",BeforeStart=e=>
                {
                    e.TickCompleted+=()=>{engineThread=Environment.CurrentManagedThreadId;if(Volatile.Read(ref tickCamera)?.NextCameraParams is {} p)pairs.Enqueue(p);};
                    e.LogEntry+=l=>{if(l.Name=="VisionComponent.SetCameraSettings")nativeThreads.Enqueue(Environment.CurrentManagedThreadId);};
                }
            });
            Assert.True(SpinWait.SpinUntil(()=>robot.Engine.IsReadyDelivered,TimeSpan.FromSeconds(10)));
            robot.ConnectToRobot(IPAddress.Loopback,isSimulated:true);
            Assert.True(SpinWait.SpinUntil(()=>robot.Engine.ConnectionState==1,TimeSpan.FromSeconds(5)));
            // The transport opens its connection asynchronously; a ConnectionResponse injected before that is dropped.
            Assert.True(SpinWait.SpinUntil(()=>robot.Transport.Connection is not null,TimeSpan.FromSeconds(5)));
            ushort seq=1;
            void In(ReliableMessageType type,byte[] body)=>robot.Transport.ProcessIncoming(FrameCodec.Encode(Frame.Single(new SubMessage(type,body,seq++),1)));
            In(ReliableMessageType.ConnectionResponse,Array.Empty<byte>());
            Assert.True(SpinWait.SpinUntil(()=>robot.Engine.ConnectionState==2,TimeSpan.FromSeconds(5)));
            In(ReliableMessageType.SingleReliableMessage,new FirmwareVersion{RobotId=1,Signature=Encoding.UTF8.GetBytes("{\"sim\":true}")}.ToBytes());
            Assert.True(SpinWait.SpinUntil(()=>robot.Engine.RobotValidated==true,TimeSpan.FromSeconds(5)));
            var camera=robot.CameraSettings;
            Volatile.Write(ref tickCamera,camera);
            var work=Enumerable.Range(0,12).Select(i=>Task.Run(()=>
            {
                if(i%2==0)Assert.True(camera.SetCameraSettings((ushort)10,1f));
                else camera.SetAndDisableAutoExposure((ushort)20,2f);
            })).ToArray();
            In(ReliableMessageType.SingleReliableMessage,new DefaultCameraParams{Field0=4,Field1=1.5f,Field2=1,Field3=66,Field4=new byte[17]}.ToBytes());
            await Task.WhenAll(work).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(SpinWait.SpinUntil(()=>pairs.Count>0,TimeSpan.FromSeconds(5)));
            Assert.All(pairs,p=>Assert.Contains(p,new[]{(10,1f),(20,2f),(16,1.5f)}));
            // The injected DefaultCameraParams is handled on a later engine tick; wait for its log entry.
            Assert.True(SpinWait.SpinUntil(()=>nativeThreads.Count>=13,TimeSpan.FromSeconds(5)));Assert.All(nativeThreads,t=>Assert.Equal(engineThread,t));
        }
        finally{Directory.Delete(root,true);}
    }
}
