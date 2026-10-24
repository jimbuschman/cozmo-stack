using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M3M4WorldOwnershipTests
{
    private static string Shipped()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent)
        {var p=Path.Combine(d.FullName,"re-analysis/obb/assets/cozmo_resources");if(Directory.Exists(p))return p;}
        throw new DirectoryNotFoundException();
    }

    // reach: production - CozmoRobot.Create, live worker, framed simulated peer, real message subscriptions and
    // public Flash play. A known located cube is supplied as world input; vision pose recovery/hardware not claimed.
    [Fact]
    public void RobotOwnedWorldReceivesMovementAndPublicPlaySendsShippedFlash()
    {
        var log=new ConcurrentQueue<string>();var wire=new ConcurrentQueue<CubeLights>();
        using var robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions{ResourcesPath=Shipped(),BlockPoolPath="",BeforeStart=e=>e.StackLogLine+=log.Enqueue});
        var vision=robot.Vision;Assert.Same(robot.LocalizationWorld,vision.World);Assert.Same(vision,robot.VisionModeRecipient);
        Assert.Null(vision.Calibration);Assert.False(vision.Enabled);
        Assert.True(SpinWait.SpinUntil(()=>robot.Engine.IsReadyDelivered,TimeSpan.FromSeconds(15)));
        ushort seq=1;
        void In(ReliableMessageType type,byte[] body)=>robot.Transport.ProcessIncoming(FrameCodec.Encode(Frame.Single(new SubMessage(type,body,seq++),1)));
        void Data(RobotMessage m)=>In(ReliableMessageType.SingleReliableMessage,m.ToBytes());
        robot.ConnectToRobot(IPAddress.Loopback,isSimulated:true);
        Assert.True(SpinWait.SpinUntil(()=>robot.Engine.ConnectionState==1,5000));
        // The transport opens its connection asynchronously; a ConnectionResponse injected before that is dropped.
        Assert.True(SpinWait.SpinUntil(()=>robot.Transport.Connection is not null,5000));
        In(ReliableMessageType.ConnectionResponse,Array.Empty<byte>());
        Assert.True(SpinWait.SpinUntil(()=>robot.Engine.ConnectionState==2,5000));
        Data(new FirmwareVersion{RobotId=1,Signature=Encoding.UTF8.GetBytes("{\"sim\":true}")});
        Assert.True(SpinWait.SpinUntil(()=>robot.Engine.RobotValidated==true,5000));
        Data(new SyncTimeAck());
        Data(new RobotState{Timestamp=100,PoseOriginId=robot.Engine.Robot!.CurrentOriginId,PoseFrameId=robot.Engine.Robot.PoseFrameId,
            BatteryVoltage=4,Status=(uint)(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos),
            Accel=new AccelData{Z=9800},Gyro=new GyroData(),CliffDataRaw=new ushort[]{500,500,500,500}});
        Assert.True(SpinWait.SpinUntil(()=>robot.State.Latest?.Timestamp==100,5000));
        Data(new ObjectConnectionState{ObjectID=0,FactoryID=123,ObjectType=ObjectType.Block_LIGHTCUBE1,Connected=true});
        Assert.True(SpinWait.SpinUntil(()=>vision.World.ConnectedObjectIdForActiveId(0)!=null,5000));
        uint id=vision.World.ConnectedObjectIdForActiveId(0)!.Value;
        var located=new ObservableObject(id,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {OriginId=vision.World.CurrentOriginId,PoseState=PoseState.Known,ActiveId=0};
        robot.Engine.InvokeEngineRequest(()=>{vision.World.AddLocatedObject(located);return true;});
        Data(new ObjectMoved{ObjectID=0,Timestamp=120,AxisOfAccel=UpAxis.ZPositive});
        Assert.True(SpinWait.SpinUntil(()=>located.PoseState==PoseState.Dirty,5000));
        robot.Engine.InvokeEngineRequest(()=>{robot.Engine.Robot!.SendFault=m=>{if(m is CubeLights lights)wire.Enqueue(lights);return null;};return true;});
        Assert.True(robot.Lights.Cubes.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Flash"));
        var lights=Assert.Single(wire).Lights;
        // Shipped flash.json: all white/black, on/off100ms, transitions/offset0. Checked white balance/wire frames.
        Assert.All(lights,l=>{Assert.Equal((ushort)0xFE73,l.OnColor);Assert.Equal((ushort)0x8000,l.OffColor);
            Assert.Equal((byte)4,l.OnFrames);Assert.Equal((byte)4,l.OffFrames);
            Assert.Equal((byte)0,l.TransitionOnFrames);Assert.Equal((byte)0,l.TransitionOffFrames);Assert.Equal((short)0,l.Offset);});
        Assert.DoesNotContain(log,s=>s.Contains("MISSING: M4-009 / M11-004"));
        Assert.Same(vision,robot.Vision);
    }

    // reach: unit (explicit internal world-less composition, kept only to exercise its visible guards).
    [Fact]
    public void OnlyTheExplicitInternalTestCompositionOmitsVision()
    {
        using var robot=CozmoRobot.CreateOffline(engineOptions:new CozmoEngineOptions{OmitVisionForTest=true});
        Assert.Null(robot.VisionModeRecipient);Assert.Throws<InvalidOperationException>(()=>robot.Vision);
    }

    // reach: unit (construction options and host teardown; no substitute vision owner, join or shutdown step).
    [Fact]
    public void RobotOptionsReachTheOwnedInstanceAndRobotDisposesItOnce()
    {
        var calibration=CameraCalibration.Nominal();var detector=new MarkerDetector();
        var robot=CozmoRobot.CreateOffline(engineOptions:new CozmoEngineOptions{VisionCalibration=calibration,VisionDetector=detector});
        var vision=robot.Vision;Assert.Same(calibration,vision.Calibration);Assert.Same(detector,vision.Detector);
        var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        robot.Dispose();Assert.Null(robot.VisionModeRecipient);
        int count=logs.Count(e=>e.Name=="ImageBase.CloseAllDisplayWindows.NoDisplayOnAndroidOrIOS");
        Assert.Equal(2,count);vision.Dispose();
        Assert.Equal(count,logs.Count(e=>e.Name=="ImageBase.CloseAllDisplayWindows.NoDisplayOnAndroidOrIOS"));
    }
}
