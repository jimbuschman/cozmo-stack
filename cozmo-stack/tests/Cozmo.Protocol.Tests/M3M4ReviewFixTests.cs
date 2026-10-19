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
public sealed class M3M4ReviewFixTests
{
    private const ObjectType Type=ObjectType.Block_LIGHTCUBE1;
    private static string Shipped()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent)
        {var p=Path.Combine(d.FullName,"re-analysis/obb/assets/cozmo_resources");if(Directory.Exists(p))return p;}
        throw new DirectoryNotFoundException();
    }
    private sealed class Live:IDisposable
    {
        internal readonly CozmoRobot Robot;
        internal readonly VisionSystem Vision;
        internal readonly ConcurrentQueue<CubeLights> Wire=new();
        internal readonly ConcurrentQueue<EngineLogEntry> Logs=new();
        private ushort _seq=1;
        internal Live(bool state=true)
        {
            Robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions{ResourcesPath=Shipped(),BlockPoolPath="",BeforeStart=e=>e.LogEntry+=Logs.Enqueue});
            Assert.True(SpinWait.SpinUntil(()=>Robot.Engine.IsReadyDelivered,TimeSpan.FromSeconds(15)));
            Robot.ConnectToRobot(IPAddress.Loopback,isSimulated:true);
            Assert.True(SpinWait.SpinUntil(()=>Robot.Engine.ConnectionState==1,TimeSpan.FromSeconds(5)));
            In(ReliableMessageType.ConnectionResponse,Array.Empty<byte>());
            Assert.True(SpinWait.SpinUntil(()=>Robot.Engine.ConnectionState==2,TimeSpan.FromSeconds(5)));
            Data(new FirmwareVersion{RobotId=1,Signature=Encoding.UTF8.GetBytes("{\"sim\":true}")});
            Assert.True(SpinWait.SpinUntil(()=>Robot.Engine.RobotValidated==true,TimeSpan.FromSeconds(5)));
            Data(new SyncTimeAck());
            Vision=Robot.Vision; Vision.Calibration=CameraCalibration.Nominal(); Vision.Enabled=true; Vision.ModeEnableMask=1<<VisionSystem.DetectingMarkers;
            Robot.Engine.InvokeEngineRequest(()=>{Robot.Engine.Robot!.SendFault=m=>{if(m is CubeLights lights)Wire.Enqueue(lights);return null;};return true;});
            if(state)State(100);
        }
        private void In(ReliableMessageType type,byte[] body)=>Robot.Transport.ProcessIncoming(FrameCodec.Encode(Frame.Single(new SubMessage(type,body,_seq++),1)));
        internal void Data(RobotMessage message)=>In(ReliableMessageType.SingleReliableMessage,message.ToBytes());
        internal void State(uint ts,float x=0)
        {
            var native=Robot.Engine.Robot!;
            Data(new RobotState{Timestamp=ts,PoseOriginId=native.CurrentOriginId,PoseFrameId=native.PoseFrameId,Pose=new RobotPose{X=x},HeadAngle=-0.15f,
                BatteryVoltage=4,Status=(uint)(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos),
                Accel=new AccelData{Z=9800},Gyro=new GyroData(),CliffDataRaw=new ushort[]{500,500,500,500}});
            Assert.True(SpinWait.SpinUntil(()=>native.PoseHistory.NewestTimeStamp==ts,TimeSpan.FromSeconds(5)));
        }
        internal GrayImage Render()
        {
            var cal=Vision.Calibration!;var image=new GrayImage(cal.Columns,cal.Rows);image.Fill(150);
            var pd=new VisionPoseData(100,Pose3d.Identity,-0.15f,0,false,false);
            using var cube=new Pose3d(Mat3.Identity,new Vec3(150,0,22));
            MarkerRenderer.DrawObject(image,MarkerLibrary.EmbeddedOrNull??throw new InvalidOperationException("marker assets required"),new CameraModel(cal,pd.CameraPose),Type,cube);
            return image;
        }
        internal void Image(GrayImage image,uint id,uint timestamp)
        {
            const int chunkSize=1200;int count=(image.Pixels.Length+chunkSize-1)/chunkSize;
            for(int i=0;i<count;i++)Data(new ImageChunk{ImageId=id,FrameTimestamp=timestamp,ImageEncoding=1,ImageResolution=4,
                ImageChunkCount=(byte)count,ChunkId=(byte)i,Data=image.Pixels.Skip(i*chunkSize).Take(chunkSize).ToArray()});
        }
        public void Dispose(){Vision.Dispose();Robot.Dispose();}
    }

    // reach: production - rendered raw camera packets and RobotState dispatch; nominal calibration is a host input.
    // The host event calls real PlayLightAnim against a running engine; SendFault is a transparent observer (returns null).
    [Fact]
    public void CameraHandoffReachesObjectObservedAndItsHostLightCommandDoesNotHoldFrameGate()
    {
        using var f=new Live();
        f.Data(new ObjectConnectionState{ObjectID=0,FactoryID=0x1234,ObjectType=Type,Connected=true});
        Assert.True(SpinWait.SpinUntil(()=>f.Robot.LocalizationWorld.ConnectedObjects.Count==1,TimeSpan.FromSeconds(5)));
        int callbacks=0,insideGate=0,played=0;var errors=new ConcurrentQueue<Exception>();
        f.Vision.World.ObjectObserved+=_=>
        {
            try
            {
                if(Monitor.IsEntered(f.Vision.World.FrameGate))Interlocked.Increment(ref insideGate);
                if(f.Robot.Lights.Cubes.PlayLightAnim(Type,"Flash"))Interlocked.Increment(ref played);
            }
            catch(Exception ex){errors.Enqueue(ex);}
            finally{Interlocked.Increment(ref callbacks);}
        };
        var image=f.Render();f.Image(image,1,100);
        Assert.True(SpinWait.SpinUntil(()=>f.Vision.FramesProcessed>=1,TimeSpan.FromSeconds(5)));
        f.State(133);int wire=f.Wire.Count;f.Image(image,2,133);
        Assert.True(SpinWait.SpinUntil(()=>Volatile.Read(ref callbacks)>0,TimeSpan.FromSeconds(5)));
        Assert.Empty(errors);Assert.Equal(0,insideGate);Assert.True(played>0);Assert.True(f.Wire.Count>wire);
        Assert.NotEmpty(f.Vision.LastResult!.Markers);
    }

    // reach: production - camera packet framing with no raw state; native query failure diagnostic and no handoff.
    [Fact]
    public void CameraBeforeAnyRawStateDropsWithTheNativeHistorySummary()
    {
        using var f=new Live(state:false);f.Image(new GrayImage(320,240),1,100);
        Assert.True(SpinWait.SpinUntil(()=>f.Logs.Any(e=>e.Name=="VisionComponent.SetNextImage.StateHistoryFail"),TimeSpan.FromSeconds(5)));
        var log=Assert.Single(f.Logs.Where(e=>e.Name=="VisionComponent.SetNextImage.StateHistoryFail"));
        Assert.Contains("rawStates: have 0 from 0:0",log.Text);Assert.Contains("visionStates: have 0 from 0:0",log.Text);
        Assert.Equal(0,f.Vision.FramesProcessed);
    }

    // reach: unit (pauses worker scheduling to force a history change after actual Camera.FrameForVision ingress;
    // no substituted history/query state; this does not claim the native paused-mode writer).
    [Fact]
    public void CameraUsesTheRobotStateCapturedAtHandoffRatherThanRequeryingOnTheWorker()
    {
        using var f=new Live();f.Vision.SkipProcessorPass=true;
        using var handed=new ManualResetEventSlim();f.Robot.Camera.FrameForVision+=_=>handed.Set();
        f.Image(f.Render(),1,150);Assert.True(handed.Wait(TimeSpan.FromSeconds(5)));
        try{f.State(200,10);}finally{f.Vision.SkipProcessorPass=false;}
        Assert.True(SpinWait.SpinUntil(()=>f.Vision.FramesProcessed>0,TimeSpan.FromSeconds(5)));
        Assert.Equal(150u,f.Vision.LastResult!.Timestamp);
        Assert.Equal(0f,(float)f.Vision.LastResult.PoseData.RobotPose.Translation.X); // query at newest100, not later interpolated150/x5
    }

    // reach: unit (native SetNewPose creates the frame boundary; real state and camera packet dispatch/query gates).
    [Fact]
    public void CameraFrameMismatchDropsWithNativeOriginMismatchLog()
    {
        using var f=new Live();f.Robot.Engine.InvokeEngineRequest(()=>{using var p=new Pose3d(f.Robot.Engine.Robot!.MainPose);f.Robot.Engine.Robot.SetNewPose(p);return true;});
        f.State(200);f.Image(f.Render(),1,150);
        Assert.True(SpinWait.SpinUntil(()=>f.Logs.Any(e=>e.Name=="VisionComponent.SetNextImage.OriginMismatch"),TimeSpan.FromSeconds(5)));
        Assert.Equal(0,f.Vision.FramesProcessed);Assert.Contains("t=150",f.Logs.First(e=>e.Name=="VisionComponent.SetNextImage.OriginMismatch").Text);
    }

    // reach: unit (worker calls native SetPoseStateHelper on a known located fixture object; no substituted light recipient).
    [Fact]
    public async Task PoseStateHostHandlerCanCommandLightsAndFrameGateMisuseFailsImmediately()
    {
        using var f=new Live();f.Data(new ObjectConnectionState{ObjectID=0,FactoryID=0x1234,ObjectType=Type,Connected=true});
        Assert.True(SpinWait.SpinUntil(()=>f.Robot.LocalizationWorld.ConnectedObjects.Count==1,TimeSpan.FromSeconds(5)));
        var world=f.Vision.World;var connected=world.ConnectedObjects.Single();
        var located=new ObservableObject(connected.ObjectId,Type,CubeGeometry.MarkersFor(Type)){OriginId=world.CurrentOriginId,Pose=Pose3d.Identity,PoseState=PoseState.Known};
        f.Robot.Engine.InvokeEngineRequest(()=>{world.AddLocatedObject(located);return true;});
        bool held=true,played=false;world.PoseStateChanged+=(_,_,_)=>{held=Monitor.IsEntered(world.FrameGate);played=f.Robot.Lights.Cubes.PlayLightAnim(Type,"Flash");};
        int count=f.Wire.Count;
        await Task.Run(()=>world.SetPoseStateHelper(located,PoseState.Dirty)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(held);Assert.True(played);Assert.True(f.Wire.Count>count);
        var error=await Task.Run(()=>{using var frame=world.EnterPoseMutation();return Assert.Throws<InvalidOperationException>(()=>f.Robot.Lights.Cubes.PlayLightAnim(Type,"Flash"));});
        Assert.Contains("FrameGate",error.Message);
    }

    // reach: production - shipped Dance_01/Flash, public play/enable commands and the stopped-entry loop.
    [Fact]
    public void StoppedLoopPopsDanceWithoutCallingItsCallbackAfterFlash()
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var lights=rig.Robot.Lights.Cubes;var calls=new List<string>();var logs=new List<string>();rig.Engine.StackLogLine+=logs.Add;
        Assert.True(lights.PlayLightAnim(Type,"Dance_01",1,completed:()=>calls.Add("X")));
        Assert.True(lights.PlayLightAnim(Type,"Flash",1,completed:()=>calls.Add("Y")));
        lights.SetEnableFreeplayLightStates(false);
        Assert.Equal(new[]{"Y"},calls);
        Assert.Single(logs.Where(s=>s.Contains("M1-034/M4-051")&&s.Contains("0x00637A06/0x00637A0C")));
    }

    // reach: production - connection dispatch on a previously located/known fixture cube; observes real native light inputs.
    [Fact]
    public void ReconnectedLocatedCubeMarkDirtyReachesTheLightRecipient()
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var world=rig.Robot.LocalizationWorld;var connected=world.ConnectedObjects.Single(o=>o.ActiveId==0);
        var located=new ObservableObject(connected.ObjectId,Type,CubeGeometry.MarkersFor(Type)){OriginId=world.CurrentOriginId,PoseState=PoseState.Known,Pose=Pose3d.Identity};
        world.AddLocatedObject(located);
        rig.Data(new ObjectConnectionState{ObjectID=0,FactoryID=0x1000,ObjectType=Type,Connected=false});rig.Tick();
        world.SetPoseStateHelper(located,PoseState.Known);
        var calls=new List<(uint Id,string Trigger,int Layer)>();rig.Robot.Lights.Cubes.PoseStatePlayObservedForTest=(id,trigger,layer,_,_,_)=>calls.Add((id,trigger,layer));
        rig.Data(new ObjectConnectionState{ObjectID=0,FactoryID=0x1000,ObjectType=Type,Connected=true});rig.Tick();
        Assert.Equal(PoseState.Dirty,located.PoseState);
        Assert.Contains((located.ObjectId,"Connected",2),calls);
    }

    // reach: unit (sets only PendingSend on an actual shipped playback entry; checks the native Update skip,0x00637964).
    [Fact]
    public void PendingSendSkipsAnOtherwiseDuePlaybackEntry()
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var lights=rig.Robot.Lights.Cubes;Assert.True(lights.PlayLightAnim(Type,"Flash",1));
        var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var infos=(System.Collections.IDictionary)typeof(CubeLightComponent).GetField("_infos",flags)!.GetValue(lights)!;
        var info=infos[Type]!;var layers=(Array)info.GetType().GetField("Layers",flags)!.GetValue(info)!;
        var stack=(System.Collections.IList)layers.GetValue(1)!;var top=stack[stack.Count-1]!;
        var pending=top.GetType().GetField("PendingSend",flags)!;var timer=top.GetType().GetField("TimerMs",flags)!;var index=top.GetType().GetField("Index",flags)!;
        timer.SetValue(top,1u);pending.SetValue(top,true);int mark=rig.Mark();lights.Update();
        Assert.Equal(0,index.GetValue(top));Assert.Equal(1u,timer.GetValue(top));Assert.DoesNotContain(rig.SentSince(mark),m=>m is CubeLights);
        pending.SetValue(top,false);lights.Update();Assert.Equal(1,index.GetValue(top));
    }

    // reach: unit (known located object and explicit nested native frame scopes; native recipient remains real and synchronous).
    [Fact]
    public void PublicWorldEventsKeepTheirOrderAfterTheOutermostFrameScopeAndNativeRecipientsStaySynchronous()
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var world=rig.Robot.LocalizationWorld;var connected=world.ConnectedObjects.Single(o=>o.ActiveId==0);
        var obj=new ObservableObject(connected.ObjectId,Type,CubeGeometry.MarkersFor(Type)){OriginId=world.CurrentOriginId,Pose=Pose3d.Identity,PoseState=PoseState.Known};world.AddLocatedObject(obj);
        var publicEvents=new List<string>();var native=new List<PoseState>();bool publicHeld=false,nativeHeld=true;
        var prior=world.ActiveObjectPoseStateChanged;
        world.ActiveObjectPoseStateChanged=(id,old,state)=>{native.Add(state);nativeHeld&=Monitor.IsEntered(world.FrameGate);prior?.Invoke(id,old,state);};
        world.PoseStateChanged+=(_,_,state)=>{publicHeld|=Monitor.IsEntered(world.FrameGate);publicEvents.Add(state.ToString());};
        world.Log+=text=>{if(text=="boundary"){publicHeld|=Monitor.IsEntered(world.FrameGate);publicEvents.Add(text);}};
        world.LocatedObjectDeleted+=_=>{publicHeld|=Monitor.IsEntered(world.FrameGate);publicEvents.Add("deleted");};
        using(world.EnterPoseMutation())
        {
            world.SetPoseStateHelper(obj,PoseState.Dirty);world.LogLine("boundary");world.SetPoseStateHelper(obj,PoseState.Known);
            var filter=new BlockWorldFilter();filter.AllowedIds.Add(obj.ObjectId);world.DeleteLocatedObjects(filter);
            Assert.Empty(publicEvents);Assert.Equal(new[]{PoseState.Dirty,PoseState.Known,PoseState.Unknown},native);Assert.True(nativeHeld);
        }
        Assert.False(publicHeld);Assert.Equal(new[]{"Dirty","boundary","Known","Unknown","deleted"},publicEvents);
    }

    // reach: unit (constructs allowed root raw input and a decreasing frame sequence to isolate the checked H9 condition;
    // production OnRamp/root-input reachability is not claimed, no native parent helper is substituted).
    [Fact]
    public void CorrectionDoesNotAskForAnchorParentWhenItsFrameDiffersAndNoFrameIncreaseOccurs()
    {
        var history=new RobotStateHistory();byte gaps=RobotStateHistory.ConsecutiveGaps;
        using var origin=new Pose3d(){Id=1};using var rootRaw=new Pose3d("");
        using var child=new Pose3d(Rotation3d.Identity,new Vec3(10,0,0),parent:origin);
        using var vision=new Pose3d(Rotation3d.Identity,new Vec3(20,0,0),parent:origin);
        try
        {
            Assert.Equal(0u,history.AddRawOdomState(100,rootRaw,new RobotState{Timestamp=100,PoseFrameId=2}));
            Assert.Equal(0u,history.AddRawOdomState(200,child,new RobotState{Timestamp=200,PoseFrameId=1}));
            Assert.Equal(0u,history.AddVisionOnlyState(100,vision,3,0,0));
            using var output=new HistRobotState();uint time=0;
            Assert.Equal(0u,history.ComputeStateAt(200,ref time,output,false));
            Assert.Equal(20f,(float)output.Pose.Translation.X);Assert.Equal(1u,output.State.PoseFrameId);Assert.Equal(200u,time);
        }
        finally{history.Clear();RobotStateHistory.ConsecutiveGaps=gaps;}
    }

    // reach: production - public WakeUp relative modifier and normal engine tick pass its first duration;
    // real ComputeNormal and Update send, with a known located world input (vision producer not claimed).
    [Fact]
    public void RelativeUpdateSendsAfterNativeNormalInsteadOfReportingMissing()
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var world=rig.Robot.LocalizationWorld;var connected=world.ConnectedObjects.Single(o=>o.ActiveId==0);
        world.AddLocatedObject(new ObservableObject(connected.ObjectId,Type,CubeGeometry.MarkersFor(Type)){OriginId=world.CurrentOriginId,PoseState=PoseState.Known});
        var lights=rig.Robot.Lights.Cubes;
        Assert.True(lights.PlayLightAnim(Type,"WakeUp",modifier:true,lights:new(new LedPattern[4],0,1,System.Numerics.Vector2.Zero)));
        var source=new List<EngineLogEntry>();var log=new List<string>();rig.Engine.LogEntry+=source.Add;rig.Engine.StackLogLine+=log.Add;
        int mark=rig.Mark();rig.Tick(1290);
        Assert.Single(rig.SentSince(mark).OfType<CubeLights>());
        Assert.DoesNotContain(log,s=>s.Contains("RelativeSendSkipped"));
        Assert.DoesNotContain(source,e=>e.Name=="CubeLightComponent.Update.SetLightsFailed");
    }
}
