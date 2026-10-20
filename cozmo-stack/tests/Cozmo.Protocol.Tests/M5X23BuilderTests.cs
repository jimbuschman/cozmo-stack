using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M5X23BuilderTests
{
    private sealed class Assets:IDisposable
    {
        internal readonly string Root=Directory.CreateTempSubdirectory("m523-").FullName;
        internal Assets()
        {
            Directory.CreateDirectory(Path.Combine(Root,"animations"));Directory.CreateDirectory(Path.Combine(Root,"animationGroups"));
            foreach(var name in new[]{"minus","zero","clamped"})
                File.WriteAllText(Path.Combine(Root,"animations",name+".json"),"{\""+name+"\":[{\"Name\":\"HeadAngleKeyFrame\",\"triggerTime_ms\":0,\"durationTime_ms\":10000,\"angle_deg\":5,\"angleVariability_deg\":0}]}");
            File.WriteAllText(Path.Combine(Root,"animationGroups","g.json"),"""
                {"Animations":[
                  {"Name":"minus","Weight":1.0,"Mood":"Default","UseHeadAngle":true,"HeadAngleMin_Deg":-26,"HeadAngleMax_Deg":-24},
                  {"Name":"zero","Weight":1.0,"Mood":"Default","UseHeadAngle":true,"HeadAngleMin_Deg":-1,"HeadAngleMax_Deg":1},
                  {"Name":"clamped","Weight":1.0,"Mood":"Default","UseHeadAngle":true,"HeadAngleMin_Deg":44,"HeadAngleMax_Deg":45}]}
                """);
        }
        public void Dispose()=>Directory.Delete(Root,true);
    }
    private static void Ready(M4ControlTests.Rig rig)
    {
        rig.ToSynced();rig.State(flags:RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,head:0);
        // A real missing-entry terminal result for each connection read, rather than replacing readiness or history.
        for(int i=0;!rig.Engine.NvStorage!.IsIdle&&i<100;i++)
        {
            var command=rig.SentSince(0).OfType<NVCommand>().Last();
            rig.Data(new NVOpResult{Tag=command.Tag,Op=command.Op,Result=-1,Length=0,Data=Array.Empty<byte>()});rig.Tick();
        }
        Assert.True(rig.Engine.NvStorage!.IsIdle);
    }
    private sealed class Live:IDisposable
    {
        internal readonly CozmoRobot Robot;
        internal readonly ConcurrentQueue<RobotMessage> Wire=new();
        private ushort _sequence=1;
        internal Live()
        {
            string? resources=null;
            for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent)
            {var p=Path.Combine(d.FullName,"re-analysis/obb/assets/cozmo_resources");if(Directory.Exists(p)){resources=p;break;}}
            Assert.NotNull(resources);
            Robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions{ResourcesPath=resources,BlockPoolPath=""});
            Assert.True(SpinWait.SpinUntil(()=>Robot.Engine.IsReadyDelivered,TimeSpan.FromSeconds(20)));
            Robot.ConnectToRobot(IPAddress.Loopback,isSimulated:true);
            Assert.True(SpinWait.SpinUntil(()=>Robot.Engine.ConnectionState==1,5000));
            Incoming(ReliableMessageType.ConnectionResponse,Array.Empty<byte>());
            Assert.True(SpinWait.SpinUntil(()=>Robot.Engine.ConnectionState==2,5000));
            Robot.Engine.InvokeEngineRequest(()=>{Robot.Engine.Robot!.SendFault=m=>{Wire.Enqueue(m);return null;};return true;});
            Data(new RobotAvailable{SerialNumberHead=1,HwVersion=5});
            Data(new FirmwareVersion{RobotId=1,Signature=Encoding.UTF8.GetBytes("{\"sim\":true}")});
            Assert.True(SpinWait.SpinUntil(()=>Robot.Engine.RobotValidated==true,5000));
            Data(new ManufacturingID{SerialNumber=2,BodyHwVersion=7,BodyColor=2});
            Data(new SyncTimeAck());
            Data(new RobotState{Timestamp=100,PoseOriginId=Robot.Engine.Robot!.CurrentOriginId,PoseFrameId=Robot.Engine.Robot.PoseFrameId,
                HeadAngle=0,BatteryVoltage=4,Status=(uint)(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos),
                Accel=new AccelData{Z=9800},Gyro=new GyroData(),CliffDataRaw=new ushort[]{500,500,500,500}});
            Assert.True(SpinWait.SpinUntil(()=>Robot.State.Latest?.Timestamp==100,5000));
            for(int i=0;!Robot.Engine.NvStorage!.IsIdle&&i<100;i++)
            {
                Assert.True(SpinWait.SpinUntil(()=>Wire.OfType<NVCommand>().Any(),3000));
                var cmd=Wire.OfType<NVCommand>().Last();int count=Wire.OfType<NVCommand>().Count();
                Data(new NVOpResult{Tag=cmd.Tag,Op=cmd.Op,Result=-1,Length=0,Data=Array.Empty<byte>()});
                Assert.True(SpinWait.SpinUntil(()=>Robot.Engine.NvStorage.IsIdle||Wire.OfType<NVCommand>().Count()>count,3000));
            }
            Assert.True(SpinWait.SpinUntil(()=>Robot.AnimationStreamingOpen,5000));
        }
        private void Incoming(ReliableMessageType type,byte[] body)=>Robot.Transport.ProcessIncoming(FrameCodec.Encode(Frame.Single(new SubMessage(type,body,_sequence++),1)));
        private void Data(RobotMessage message)=>Incoming(ReliableMessageType.SingleReliableMessage,message.ToBytes());
        public void Dispose()=>Robot.Dispose();
    }

    // reach: production - connected robot, real calibration/state dispatch and public LoadFrom/PlayGroup;
    // fixture group windows separate ctor -25, raw0 and clamped44.5. No substituted head provider or selection.
    [Fact]
    public void PublicGroupPlayUsesTheUncalibratedCtorHeadThenBothReportClamps()
    {
        using var assets=new Assets();using var rig=new M4ControlTests.Rig();Ready(rig);
        Assert.False(rig.Robot.State.HeadCalibrated);Assert.Equal(0f,rig.Robot.State.HeadAngleRad);
        Assert.Equal(0xBEDF66F3u,BitConverter.SingleToUInt32Bits(rig.Robot.Motion.HeadAngleRad));
        rig.Robot.Animations.LoadFrom(assets.Root);
        Assert.NotNull(rig.Robot.Animations.PlayGroup("g"));Assert.Equal("minus",rig.Robot.Animations.Scheduler.Playing);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=false});rig.Tick();
        rig.State(flags:RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,head:2);
        Assert.Equal(0x3F46D3F2u,BitConverter.SingleToUInt32Bits(rig.Robot.Motion.HeadAngleRad));
        Assert.NotNull(rig.Robot.Animations.PlayGroup("g"));Assert.Equal("clamped",rig.Robot.Animations.Scheduler.Playing);
        rig.State(flags:RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,head:-2);
        Assert.NotNull(rig.Robot.Animations.PlayGroup("g"));Assert.Equal("minus",rig.Robot.Animations.Scheduler.Playing);
    }

    // reach: production - connected uncalibrated head/raw0, public parameter input/ProceduralLive push, actual
    // engine tick/sink/wire. Defaults gate first generation at1000ms; no substituted generator, pose or head input.
    [Theory][InlineData(0)][InlineData(3)]
    public void PublicProceduralLiveUsesMinus25AsItsFirstWireHeadBase(int variability)
    {
        using var live=new Live();var robot=live.Robot;
        robot.Animations.SetLiveIdleAnimationParameters(new[]{LiveIdleParam.HeadAngleVariability_deg},new[]{(float)variability},true);
        robot.Engine.InvokeEngineRequest(()=>{robot.Animations.Scheduler.PushIdleAnimation(AnimationTrigger.ProceduralLive,"head-input");return true;});
        Assert.True(SpinWait.SpinUntil(()=>live.Wire.OfType<HeadAngle>().Any(),5000));
        var first=live.Wire.OfType<HeadAngle>().First();
        // Native BEDF66F3 * 42652EE1 is binary32 -25.000002; trunc toward zero -> s8 -25.
        Assert.InRange((int)first.AngleDeg,-25-variability,-25+variability);
        Assert.False(robot.State.HeadCalibrated);Assert.Equal(0f,robot.State.HeadAngleRad);
    }

    // reach: unit (standalone library, no robot provider; checked ctor word supplies the default head input).
    [Fact]
    public void StandaloneLibraryWithoutHeadProviderUsesTheCtorHead()
    {
        using var assets=new Assets();var library=AnimationLibrary.Open(assets.Root);
        Assert.Equal("minus",library.GetAnimationNameFromGroup("g",true));
    }
}
