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

    // reach: production — real connected/located world owner, existing observation writer -> pose broadcast -> actual light recipient.
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
        var lights=rig.Robot.Lights.Cubes;int count=0;
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Flash",completed:()=>
        {
            count++;Assert.Null(lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1));
            Assert.True(Task.Run(()=>lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Dance_01")).Wait(TimeSpan.FromSeconds(2)));
        }));
        if(stop)lights.StopLightAnim("Flash",1,ObjectType.Block_LIGHTCUBE1);else rig.Tick(600);
        Assert.Equal(1,count);Assert.Equal("dance_green",lights.TopPatternName(ObjectType.Block_LIGHTCUBE1));
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

    // reach: production — full engine Delocalize, real cube-light recipient, connected non-carried objects; no substituted hook.
    [Fact]
    public void RealDelocalizePlaysConnectedOnNonCarriedCubesInSignedIdOrder()
    {
        string root=M4ControlTests.CubeLightResources();
        try
        {
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();
            M4ControlTests.ConnectCube(rig,1,ObjectType.Block_LIGHTCUBE2);M4ControlTests.ConnectCube(rig,0);
            var expected=rig.Robot.LocalizationWorld.ConnectedObjects.OrderBy(o=>unchecked((int)o.ObjectId)).Select(o=>unchecked((int)o.ObjectId)).ToArray();
            var actual=new List<int>();
            rig.Engine.LogEntry+=e=>{if(e.Name=="CubeLightComponent.PlayLightAnim.SettingLights"&&e.Text.Contains("trigger Connected"))actual.Add(int.Parse(e.Text.Split("object ")[1]));};
            rig.Engine.Robot!.Delocalize(false);
            Assert.Equal(expected,actual);
        }
        finally{Directory.Delete(root,true);}
    }

    // reach: unit (explicit native add/query outcomes; M11-097 producer remains MISSING in production).
    [Fact]
    public void CliffHistoryExcludesRejectedFailedAndDuplicateStates()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        rig.Engine.Robot!.OfflineSeamAcceptsAnyOrigin=false;
        var sensors=rig.Robot.Sensors;bool admit=true;
        sensors.HistoryAdmissionForTest=_=>admit;sensors.HistoryStatsGateForTest=_=>true;
        var history=(System.Collections.IList)typeof(CozmoSensors).GetField("_cliffHistory",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(sensors)!;
        var good=rig.MakeState(origin:rig.Engine.Robot.CurrentOriginId);rig.Data(good);rig.Tick();
        Assert.Single(history.Cast<object>());
        rig.Data(good);rig.Tick();Assert.Single(history.Cast<object>());
        rig.Data(rig.MakeState(origin:999));rig.Tick();Assert.Single(history.Cast<object>());
        admit=false;rig.Data(rig.MakeState(origin:rig.Engine.Robot.CurrentOriginId));rig.Tick();Assert.Single(history.Cast<object>());
    }

    // reach: production — accepted RobotState through real dispatch; no invented admission/query result is supplied.
    [Fact]
    public void UnknownHistoryAdmissionIsVisibleAndDoesNotAdmitCliffSamples()
    {
        Cozmo.Robot.Behavior.SteppedBehavior.ResetMissingForTests();
        var missing=new List<string>();
        void Capture(string text)=>missing.Add(text);
        Cozmo.Robot.Behavior.SteppedBehavior.MissingReported+=Capture;
        try
        {
            using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
            var history=(System.Collections.IList)typeof(CozmoSensors).GetField("_cliffHistory",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(rig.Robot.Sensors)!;
            Assert.Empty(history.Cast<object>());
            Assert.Contains(missing,s=>s.Contains("M11-097 AddRobotStateToHistory result"));
            Assert.Contains(missing,s=>s.Contains("M11-097 C12.4"));
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
            Assert.True(nativeThreads.Count>=13);Assert.All(nativeThreads,t=>Assert.Equal(engineThread,t));
        }
        finally{Directory.Delete(root,true);}
    }
}
