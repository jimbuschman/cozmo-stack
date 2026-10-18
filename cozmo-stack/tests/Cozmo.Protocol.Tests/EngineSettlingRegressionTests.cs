using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class EngineSettlingRegressionTests
{
    // reach: production — public Create/BeforeStart, real startup/ticker, and framed packets from a scripted peer.
    // No dispatcher, readiness, connection response, or component result is substituted.
    [Fact]
    public async Task StartupOwnsNeedsAndConnectRequestsPoolBeforeTheHostResponse()
    {
        string root=Directory.CreateTempSubdirectory("engine-settling-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));
            File.WriteAllText(Path.Combine(root,"animations","a.json"),"{\"a\":[]}");
            var order=new ConcurrentQueue<string>();
            var raw=new ConcurrentQueue<uint>();
            var failures=new ConcurrentQueue<string>();
            var pool=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var removed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool needsPresent=false,requestBeforeResponse=false;
            using var robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions
            {
                ResourcesPath=root,BlockPoolPath="",BeforeStart=e=>
                {
                    needsPresent=e.Needs is not null;
                    var actualRequest=e.RequestInitialBlockPool!;
                    e.RequestInitialBlockPool=()=>{actualRequest();order.Enqueue("request");}; // observes the actual app input; no result substituted.
                    e.ConnectionResponse+=r=>
                    {
                        if(r.Result!=RobotConnectionResult.Success)return;
                        requestBeforeResponse=order.Contains("request");order.Enqueue("response");
                    };
                    e.BlockPoolData+=m=>{order.Enqueue("pool");pool.TrySetResult();};
                }
            });
            Assert.True(needsPresent);
            Assert.True(SpinWait.SpinUntil(()=>robot.Engine.IsReadyDelivered,TimeSpan.FromSeconds(10)));
            Assert.Equal(0,robot.Engine.AnimationMessages.SubscriberCountForTest(0x60,true));
            var gate=typeof(CozmoEngine).GetField("_tickLock",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(robot.Engine)!;
            robot.Message+=m=>
            {
                if(Monitor.IsEntered(gate))failures.Enqueue("raw host callback owns tick lock");
                if(m is RobotAvailable a)raw.Enqueue(a.SerialNumberHead);
            };
            robot.Engine.RobotRemoved+=()=>
            {
                if(robot.Engine.Handler.IsDispatchingDataForTest)
                    failures.Enqueue("RemoveRobot occurred inside PopData dispatch loop");
                removed.TrySetResult();
            };
            robot.ConnectToRobot(IPAddress.Loopback,isSimulated:true);
            Assert.True(SpinWait.SpinUntil(()=>robot.Engine.ConnectionState==1,TimeSpan.FromSeconds(5)));
            void FrameIn(ReliableMessageType type,byte[] bytes,ushort seq)=>robot.Transport.ProcessIncoming(FrameCodec.Encode(Frame.Single(new SubMessage(type,bytes,seq),1)));
            FrameIn(ReliableMessageType.ConnectionResponse,Array.Empty<byte>(),1);
            Assert.True(SpinWait.SpinUntil(()=>robot.Engine.ConnectionState==2,TimeSpan.FromSeconds(5)));
            FrameIn(ReliableMessageType.SingleReliableMessage,new FirmwareVersion{RobotId=1,Signature=Encoding.UTF8.GetBytes("{\"sim\":true}")}.ToBytes(),2);
            Assert.True(SpinWait.SpinUntil(()=>robot.Engine.RobotValidated==true,TimeSpan.FromSeconds(5)));
            FrameIn(ReliableMessageType.SingleReliableMessage,new ManufacturingID{SerialNumber=0x1234,BodyHwVersion=5,BodyColor=0}.ToBytes(),3);
            await pool.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(requestBeforeResponse);
            Assert.Equal(new[]{"request","response","pool"},order);
            FrameIn(ReliableMessageType.SingleReliableMessage,new RobotAvailable{SerialNumberHead=11,HwVersion=5}.ToBytes(),4);
            FrameIn(ReliableMessageType.SingleReliableMessage,new RobotAvailable{SerialNumberHead=12,HwVersion=5}.ToBytes(),5);
            Assert.True(SpinWait.SpinUntil(()=>raw.Count==2,TimeSpan.FromSeconds(5)));
            Assert.Equal(new uint[]{11,12},raw);
            robot.Disconnect();
            await removed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Empty(failures);
        }
        finally{Directory.Delete(root,true);}
    }
    // reach: production — shipped config, public startup, real ticker, and the public Freeplay factory.
    [Fact]
    public async Task NeedsInitRunsBeforeStartAndFreeplayReusesTheEngineLifetime()
    {
        string? obb=null;
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null;dir=dir.Parent)
            if(Directory.Exists(Path.Combine(dir.FullName,"re-analysis","obb","assets","cozmo_resources")))
            {obb=Path.Combine(dir.FullName,"re-analysis","obb");break;}
        Assert.NotNull(obb);
        string state=Directory.CreateTempSubdirectory("needs-engine-owner-").FullName;
        try
        {
            int ticks=0;bool initialized=false;int ticksAtReady=-1;
            var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions
            {
                ResourcesPath=Path.Combine(obb,"assets","cozmo_resources"),BlockPoolPath="",NeedsDeviceDirectory=state,
                BeforeStart=e=>
                {
                    initialized=e.Needs is not null;
                    var actual=e.NeedsUpdate!;
                    e.NeedsUpdate=now=>{actual(now);Interlocked.Increment(ref ticks);};
                    e.AnimationMessages.SubscribeMessage(0x7B,true,p=>{if(p is EngineStateTransition{NewState:3})ticksAtReady=Volatile.Read(ref ticks);});
                    e.EngineStateChanged+=s=>{if(s.NewState==3)ready.TrySetResult();};
                }
            });
            Assert.True(initialized);
            Assert.False(File.Exists(Path.Combine(state,"needsState.json"))); // M15-030 device persistence is gated.
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(60));
            Assert.Equal(0,ticksAtReady);
            var owner=robot.Engine.Needs;
            var tickHook=robot.Engine.NeedsUpdate;
            using(var stack=FreeplayStack.Create(obb,robot,new BehaviorContext{Robot=robot,Triggers=new AnimationTriggerMap()},()=>robot.Engine.Timer.SecondsF,withReactions:false))
                Assert.Same(owner,stack.Needs);
            Assert.Same(owner,robot.Engine.Needs);
            Assert.Same(tickHook,robot.Engine.NeedsUpdate);
            Assert.True(SpinWait.SpinUntil(()=>Volatile.Read(ref ticks)>0,TimeSpan.FromSeconds(5)));
        }
        finally{Directory.Delete(state,true);}
    }

}
