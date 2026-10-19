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
public sealed class M3M4CallbackBuilderTests
{
    private const ObjectType Type=ObjectType.Block_LIGHTCUBE1;
    private static string Shipped()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent)
        {var p=Path.Combine(d.FullName,"re-analysis/obb/assets/cozmo_resources");if(Directory.Exists(p))return p;}
        throw new DirectoryNotFoundException();
    }

    // reach: production — shipped animations, public play/enable commands, real engine expiry and outgoing CubeLights.
    [Fact]
    public void StateLayerCompletionReReadsTheNewEngineLayerBeforeResending()
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var lights=rig.Robot.Lights.Cubes;lights.SetEnableFreeplayLightStates(false);lights.SetEnableFreeplayLightStates(true);
        int callbacks=0;bool played=false;uint timer=0;
        Assert.True(lights.PlayLightAnim(Type,"Flash",2,completed:()=>
        {
            callbacks++;played=lights.PlayLightAnim(Type,"Dance_01");
            timer=lights.PlayingForTest(Type,1)!.Value.Timer;
        }));
        int mark=rig.Mark();rig.Tick(610);
        Assert.Equal(1,callbacks);Assert.True(played);Assert.Equal("dance_green",lights.TopPatternName(Type));
        Assert.Equal(timer,lights.PlayingForTest(Type,1)!.Value.Timer);
        var lightsSent=rig.SentSince(mark).OfType<CubeLights>().ToArray();
        Assert.Equal(2,lightsSent.Length); // public B play, then native PlayingPrevious resend of B (no default).
        Assert.All(lightsSent,m=>Assert.All(m.Lights,l=>
        {
            Assert.Equal((ushort)0x83E0,l.OnColor);Assert.Equal((ushort)0x83E0,l.OffColor); // shipped Dance_01 green
            Assert.Equal((byte)4,l.OnFrames);Assert.Equal((byte)4,l.OffFrames);
        }));
        Assert.Contains(rig.Log,l=>l.Contains("Have previous anim dancing_01 on layer Engine"));
    }

    // reach: production — ObjectMoved through message dispatch; Known cube is pre-existing world fixture state.
    // This covers the checked movement -> MarkObjectDirty -> SetPoseStateHelper -> cube-light join, not vision recovery.
    [Fact]
    public void RobotObjectMovedChangesKnownVisibleCubeToConnectedLights()
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var lights=rig.Robot.Lights.Cubes;lights.SetEnableFreeplayLightStates(false);lights.SetEnableFreeplayLightStates(true);
        var world=rig.Robot.LocalizationWorld;var connected=world.ConnectedObjects.Single(o=>o.ActiveId==0);
        var located=new ObservableObject(connected.ObjectId,Type,CubeGeometry.MarkersFor(Type))
            {OriginId=world.CurrentOriginId,PoseState=PoseState.Known,Pose=Pose3d.Identity};
        world.AddLocatedObject(located);Assert.Equal("visible",lights.TopPatternName(Type));
        int mark=rig.Mark();rig.Data(new ObjectMoved{ObjectID=0,Timestamp=100});rig.Tick();
        Assert.Equal(PoseState.Dirty,located.PoseState);Assert.Equal("connected",lights.TopPatternName(Type));
        var payload=Assert.Single(rig.SentSince(mark).OfType<CubeLights>());
        // Shipped connected.json: cyan127, black alpha255,10/5000ms,1000ms transitions,0 offset.
        Assert.All(payload.Lights,l=>
        {
            Assert.Equal((ushort)0x81EF,l.OnColor);Assert.Equal((ushort)0x8000,l.OffColor);
            Assert.Equal((byte)1,l.OnFrames);Assert.Equal((byte)167,l.OffFrames);
            Assert.Equal((byte)34,l.TransitionOnFrames);Assert.Equal((byte)34,l.TransitionOffFrames);Assert.Equal((short)0,l.Offset);
        });
    }

    // reach: unit (direct SetPoseStateHelper with non-current origin; native world-origin gate at0x00506162).
    [Fact]
    public void PoseStateFromAnotherOriginDoesNotReachTheCubeLightRecipient()
    {
        using var robot=CozmoRobot.CreateOffline();var world=robot.LocalizationWorld;int calls=0;
        world.ActiveObjectPoseStateChanged=(_,_,_)=>calls++;
        var cube=new ObservableObject(1,Type,CubeGeometry.MarkersFor(Type)){OriginId=world.CurrentOriginId+1,PoseState=PoseState.Known};
        Assert.True(world.SetPoseStateHelper(cube,PoseState.Dirty));Assert.Equal(0,calls);Assert.Equal(PoseState.Dirty,cube.PoseState);
        cube.OriginId=world.CurrentOriginId;Assert.True(world.SetPoseStateHelper(cube,PoseState.Known));Assert.Equal(1,calls);
        Assert.True(world.SetPoseStateHelper(cube,PoseState.Known));Assert.Equal(1,calls); // equal state has no broadcast
    }

    // reach: unit (substitutes synthetic100ms Connected assets; actual worker, framed messages and public commands during a real callback).
    // SendFault is a transparent observer returning null, so normal transport sends still run.
    [Fact]
    // Fixture substitutes synthetic 100ms Connected assets; queued host/native callback timing is live, not shipped animation coverage.
    public async Task HostCommandsQueueUntilCompletionCallbackAndUpdateFinish()
    {
        string root=M4ControlTests.CubeLightResources();
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        int inCallback=0,engineThread=0,callbackTimedOut=0;var illegal=new ConcurrentQueue<string>();var commandThreads=new ConcurrentQueue<int>();
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));File.WriteAllText(Path.Combine(root,"animations","a.json"),"{\"a\":[]}");
            using var robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions{ResourcesPath=root,BlockPoolPath="",BeforeStart=e=>
            {
                e.LogEntry+=l=>
                {
                    if(l.Name is "CubeLightComponent.EnableCubeSleep" or "CubeLightComponent.EnableGameLayerOnly")
                    {commandThreads.Enqueue(Environment.CurrentManagedThreadId);if(Volatile.Read(ref inCallback)!=0)illegal.Enqueue(l.Name);}
                };
            }});
            try
            {
                Assert.True(SpinWait.SpinUntil(()=>robot.Engine.IsReadyDelivered,TimeSpan.FromSeconds(10)));
                robot.ConnectToRobot(IPAddress.Loopback,isSimulated:true);
                Assert.True(SpinWait.SpinUntil(()=>robot.Engine.ConnectionState==1,TimeSpan.FromSeconds(5)));
                ushort seq=1;
                void In(ReliableMessageType type,byte[] body)=>robot.Transport.ProcessIncoming(FrameCodec.Encode(Frame.Single(new SubMessage(type,body,seq++),1)));
                void Data(RobotMessage message)=>In(ReliableMessageType.SingleReliableMessage,message.ToBytes());
                In(ReliableMessageType.ConnectionResponse,Array.Empty<byte>());
                Assert.True(SpinWait.SpinUntil(()=>robot.Engine.ConnectionState==2,TimeSpan.FromSeconds(5)));
                Data(new FirmwareVersion{RobotId=1,Signature=Encoding.UTF8.GetBytes("{\"sim\":true}")});
                Assert.True(SpinWait.SpinUntil(()=>robot.Engine.RobotValidated==true,TimeSpan.FromSeconds(5)));
                Data(new SyncTimeAck());
                Data(new RobotState{Timestamp=100,PoseOriginId=1,Pose=new RobotPose(),BatteryVoltage=4,Status=(uint)RobotStatusFlag.IsBodyAccMode,
                    Accel=new AccelData{Z=9800},Gyro=new GyroData(),CliffDataRaw=new ushort[]{500,500,500,500}});
                Data(new ObjectConnectionState{ObjectID=0,FactoryID=0x1000,ObjectType=Type,Connected=true});
                Assert.True(SpinWait.SpinUntil(()=>robot.LocalizationWorld.ConnectedObjects.Count==1,TimeSpan.FromSeconds(5)));
                robot.Engine.InvokeEngineRequest(()=>
                {
                    engineThread=Environment.CurrentManagedThreadId;
                    robot.Engine.Robot!.SendFault=m=>{if(m is CubeLights && Volatile.Read(ref inCallback)!=0)illegal.Enqueue("CubeLights during callback");return null;};
                    return true;
                });
                Assert.True(robot.Lights.Cubes.PlayLightAnim(Type,"Connected",2,completed:()=>
                {
                    Volatile.Write(ref inCallback,1);entered.Set();
                    if(!release.Wait(TimeSpan.FromSeconds(5)))Interlocked.Exchange(ref callbackTimedOut,1);
                    Volatile.Write(ref inCallback,0);
                }));
                Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
                var pending=(ConcurrentQueue<Action>)typeof(CozmoEngine).GetField("_gameMessages",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(robot.Engine)!;
                var sleep=Task.Run(()=>robot.Lights.Cubes.EnableCubeSleep(false));
                Assert.True(SpinWait.SpinUntil(()=>pending.Count>=1,TimeSpan.FromSeconds(2)));
                var game=Task.Run(()=>robot.Lights.Cubes.SetEnableFreeplayLightStates(false));
                Assert.True(SpinWait.SpinUntil(()=>pending.Count>=2,TimeSpan.FromSeconds(2)));
                Assert.False(sleep.IsCompleted);Assert.False(game.IsCompleted);Assert.Empty(illegal);
                release.Set();await Task.WhenAll(sleep,game).WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(0,callbackTimedOut);Assert.Empty(illegal);Assert.True(commandThreads.Count>=3);
                Assert.All(commandThreads,id=>Assert.Equal(engineThread,id));
            }
            finally{release.Set();}
        }
        finally{Directory.Delete(root,true);}
    }
}
