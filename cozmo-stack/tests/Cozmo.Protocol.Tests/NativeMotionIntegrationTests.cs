using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Robot.Behavior;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;
[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeMotionIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderedMarkersRespectLatchedPhysicalCorrectionBoundary(bool physical)
    {
        using var f=new Fixture();var vision=f.M.Vision;var engine=f.Rig.Engine;var owner=f.Robot;
        owner.HandleFirmwareVersion(new FirmwareVersion{Signature=System.Text.Encoding.UTF8.GetBytes(physical?"{}":"{\"sim\":true}")});
        vision.SetNativeOuterCalibration(vision.Calibration!);
        Assert.Equal(physical,vision.NativeWorkerPhysicalForTest);
        vision.ModeEnableMask=1<<1;vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        var reports=new List<ProcessedImageReport>();var frames=new List<VisionFrameResult>();var missing=new List<string>();
        using var sub=engine.AnimationMessages.SubscribeMessage(0x43,true,p=>reports.Add(Assert.IsType<ProcessedImageReport>(p)));
        void Frame(VisionFrameResult value)=>frames.Add(value);
        vision.FrameProcessed+=Frame;SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=missing.Add;
        try
        {
            for(uint index=1;index<=2;index++)
            {
                f.Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.HeadInPos,head:-.15f);
                uint stamp=owner.PoseHistory.NewestTimeStamp;var pd=vision.History.At(stamp)!.Value;
                var image=new GrayImage(320,240);image.Fill(150);
                using(var objectPose=new Pose3d(Mat3.Identity,new Vec3(150,0,22)))
                using(var cameraPose=pd.CameraPose)
                    MarkerRenderer.DrawObject(image,MarkerLibrary.EmbeddedOrNull??throw new InvalidOperationException("Required marker assets missing"),new CameraModel(vision.Calibration!,cameraPose),ObjectType.Block_LIGHTCUBE1,objectPose);
                f.Rig.Data(new ImageImuData{ImageId=index,Line2Number=0});f.Rig.Tick();
                f.Rig.CameraGrayFrame(index,stamp,image.Pixels);vision.WaitForProcessorIdle();
            }
            engine.VisionUpdateAllResultsFailed!();
            Assert.Equal(2,reports.Count);
            if(physical)
            {
                Assert.Contains(missing,x=>x.Contains("marker pixel-shift"));
                Assert.Empty(frames);Assert.All(reports,r=>Assert.DoesNotContain(1u,r.Modes));
            }
            else
            {
                Assert.DoesNotContain(missing,x=>x.Contains("marker pixel-shift"));
                Assert.Equal(2,frames.Count);Assert.All(reports,r=>Assert.Contains(1u,r.Modes));
                Assert.Contains(frames,x=>x.Markers.Count!=0);
            }
        }
        finally {vision.FrameProcessed-=Frame;SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact]
    public void RealInitFalseMeterUsesFullImageWithoutRectangleProvider()
    {
        var settings=ImageQualitySettings.Globals;
        var saved=(settings.TooBright,settings.TooDark,settings.MeterFromDetections,settings.Low,settings.Mid,settings.High);
        var schedules=AllVisionModesSchedule.CopyDefault();
        var component=(VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs);
        bool error=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            using var f=new Fixture();var vision=f.M.Vision;var engine=f.Rig.Engine;
            var config=new FirmwareJsonValue();
            Assert.True(Json.TryParseInto(File.ReadAllBytes(Path.Combine(engine.Options.ResourcesPath!,"config","engine","vision_config.json")),config));
            var section=config.Members[Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes("ImageQuality"))];
            section.Members[Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes("MeterFromDetections"))]=new FirmwareJsonValue{Kind=FirmwareJsonKind.Boolean,Boolean=false};
            var initialized=vision.InitializeNativeVision(f.Robot,config);Assert.True(initialized.Available);Assert.Equal(0u,initialized.Result);
            Assert.False(settings.MeterFromDetections);Assert.NotNull(vision.QualityProcessor.Pipeline);Assert.Null(vision.QualityRectangleInput);
            vision.ModeEnableMask=1<<7;vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
            var reports=new List<ProcessedImageReport>();
            using var sub=engine.AnimationMessages.SubscribeMessage(0x43,true,p=>reports.Add(Assert.IsType<ProcessedImageReport>(p)));
            void Frame(uint id)
            {
                f.Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,head:-.15f);
                uint stamp=f.Robot.StoredState!.Timestamp;
                f.Rig.Data(new ImageImuData{ImageId=id,Line2Number=0});f.Rig.Tick();
                f.Rig.CameraGrayFrame(id,stamp,Enumerable.Repeat((byte)100,320*240).ToArray());vision.WaitForProcessorIdle();
                engine.VisionUpdateAllResultsFailed!();
            }
            Frame(31);Assert.Contains(7u,Assert.Single(reports).Modes);
            settings.MeterFromDetections=true;reports.Clear();Frame(32);Assert.Empty(reports);
        }
        finally
        {
            (settings.TooBright,settings.TooDark,settings.MeterFromDetections,settings.Low,settings.Mid,settings.High)=saved;
            AllVisionModesSchedule.RestoreDefaultForTest(schedules);
            (VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs)=component;
            Cozmo.Transport.EngineErrorState.ErrorFlagSet=error;
        }
    }
    [Fact]
    public void WorkerPhysicalFlagIsLatchedByCalibrationIncludingSamePointer()
    {
        using var f=new Fixture();var owner=f.Robot;var vision=f.M.Vision;
        var calibration=CameraCalibration.Nominal();
        owner.HandleFirmwareVersion(new FirmwareVersion{Signature=System.Text.Encoding.UTF8.GetBytes("{\"sim\":true}")});
        vision.SetNativeOuterCalibration(calibration);Assert.False(vision.NativeWorkerPhysicalForTest);
        owner.HandleFirmwareVersion(new FirmwareVersion{Signature=System.Text.Encoding.UTF8.GetBytes("{}")});
        Assert.True(owner.IsPhysicalRobot);Assert.False(vision.NativeWorkerPhysicalForTest);
        vision.SetNativeOuterCalibration(calibration);Assert.True(vision.NativeWorkerPhysicalForTest);
        owner.HandleFirmwareVersion(new FirmwareVersion{Signature=System.Text.Encoding.UTF8.GetBytes("{\"sim\":true}")});
        Assert.False(owner.IsPhysicalRobot);Assert.True(vision.NativeWorkerPhysicalForTest);
        vision.SetNativeOuterCalibration(calibration);Assert.False(vision.NativeWorkerPhysicalForTest);
    }
    [Fact]
    public void FaceMissingCallbackReplacementCannotAdvanceReplacementMotionSchedule()
    {
        using var f=new Fixture();var vision=f.M.Vision;var engine=f.Rig.Engine;var original=f.Robot;
        using var pose=new Pose3d(Rotation3d.Identity,Vec3.Zero);
        var raw=new RobotState{Timestamp=1000,HeadAngle=0,Status=0x200,Pose=new RobotPose(),Accel=new AccelData(),Gyro=new GyroData(),CliffDataRaw=new ushort[4]};
        using var history=HistRobotState.From(pose,raw);
        using var ground=new NativeGroundFrame(original,1000,history,false,new float[9]);
        vision.ModeEnableMask=(1<<2)|(1<<3);vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        EngineRobot? replacement=null;VisionModeSchedule? schedule=null;int counter=-1;
        void Missing(string message)
        {
            if(replacement is not null || !message.Contains("enabled face detector input unavailable"))return;
            engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
            replacement=engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
            schedule=vision.Schedules![(int)VisionMode.DetectingMotion];counter=schedule.Counter;
        }
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Missing;
        try
        {
            var image=new GrayImage(320,240);image.Fill(100);
            vision.ProcessNativeCapturedForTest(original,ground,image,1,1000);
            Assert.NotNull(replacement);Assert.NotNull(schedule);
            Assert.Same(schedule,vision.Schedules![(int)VisionMode.DetectingMotion]);Assert.Equal(counter,schedule!.Counter);
        }
        finally {SteppedBehavior.MissingReported-=Missing;}
    }
    [Fact]
    public void MissingMotionConfigReturnsDefinedFailureButConversionThrowIsUnavailable()
    {
        bool oldError=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
        using var f=new Fixture();var vision=f.M.Vision;var owner=f.Robot;var engine=f.Rig.Engine;
        using var pose=new Pose3d(Rotation3d.Identity,Vec3.Zero);
        var raw=new RobotState{Timestamp=1000,HeadAngle=0,Status=0x200,Pose=new RobotPose(),Accel=new AccelData(),Gyro=new GyroData(),CliffDataRaw=new ushort[4]};
        using var history=HistRobotState.From(pose,raw);
        using var ground=new NativeGroundFrame(owner,1000,history,false,new float[9]);
        vision.ModeEnableMask=1<<3;vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        var state=vision.NativeInitializationForTest!;var reports=new List<ProcessedImageReport>();
        using var sub=engine.AnimationMessages.SubscribeMessage(0x43,true,p=>reports.Add(Assert.IsType<ProcessedImageReport>(p)));
        var image=new GrayImage(320,240);image.Fill(100);
        state.ReplaceMotion(vision,new FirmwareJsonValue{Kind=FirmwareJsonKind.Object});
        vision.ProcessNativeCapturedForTest(owner,ground,image,1,1000);engine.VisionUpdateAllResultsFailed!();
        Assert.Contains(3u,Assert.Single(reports).Modes);
        Assert.False(state.Motion!.Detector.HasPreviousImage);Assert.Equal(0u,state.Motion.Detector.ComparisonCount);
        var bad=new FirmwareJsonValue();Assert.True(Json.TryParseInto(System.Text.Encoding.UTF8.GetBytes("{\"MotionDetector\":{\"HorizontalSize\":\"bad\"}}"),bad));
        state.ReplaceMotion(vision,bad);reports.Clear();
        vision.ProcessNativeCapturedForTest(owner,ground,image,2,1001);engine.VisionUpdateAllResultsFailed!();
        Assert.Empty(reports);Assert.False(state.Motion!.Detector.HasPreviousImage);Assert.False(state.MotionKnowledgeKnown);
        }
        finally {Cozmo.Transport.EngineErrorState.ErrorFlagSet=oldError;}
    }
    [Fact]
    public void CalibrationRemovedAfterHandoffDoesNotAdvanceWorkerPose()
    {
        using var fixture=new Fixture();var vision=fixture.Rig.Robot.Vision;var owner=fixture.Robot;
        Assert.True(SpinWait.SpinUntil(()=>vision.Enabled,TimeSpan.FromSeconds(5)));
        var worker=vision.NativeInitializationForTest!.MotionPose;uint before=worker.FrameCount;
        var calibration=vision.Calibration!;vision.SkipProcessorPass=true;
        try
        {
            uint stamp=owner.StoredState!.Timestamp,id=71;
            fixture.Rig.Data(new ImageImuData{ImageId=id,RateX=0,RateY=0,RateZ=0,Line2Number=0});fixture.Rig.Tick();
            fixture.Rig.CameraGrayFrame(id,stamp,Enumerable.Repeat((byte)150,320*240).ToArray());
            Assert.True(SpinWait.SpinUntil(()=>vision.HasPendingFrameForTest,TimeSpan.FromSeconds(5)));
            vision.Calibration=null;vision.SkipProcessorPass=false;vision.WaitForProcessorIdle();
            Assert.Equal(before,worker.FrameCount);
        }
        finally {vision.SkipProcessorPass=false;vision.Calibration=calibration;}
    }
    [Fact]
    public void ChangedRealF2FramesProduceTwoOrderedMotionReportsAtEngineDrain()
    {
        using var f=new Fixture();var vision=f.M.Vision;var engine=f.Rig.Engine;
        Assert.True(SpinWait.SpinUntil(()=>vision.Enabled,TimeSpan.FromSeconds(5)));
        vision.ModeEnableMask=1<<3;vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules(); // supplied mode input
        var local=new List<NativeObservedMotion>();var raw=new List<byte[]>();
        using var sub=engine.AnimationMessages.SubscribeMessage(0x49,true,p=>local.Add(Assert.IsType<NativeObservedMotion>(p)));
        void App(ushort tag,ReadOnlyMemory<byte> body){if(tag==0x49)raw.Add(body.ToArray());}
        engine.EngineToGameMessage+=App;var previousGate=engine.HasCameraCalibration;
        engine.HasCameraCalibration=()=>false; // supplied drain gate only; frames, owner and detector stay actual
        try
        {
            void Frame(uint id,byte pixel)
            {
                uint stamp=f.Robot.StoredState!.Timestamp;
                f.Rig.Data(new ImageImuData{ImageId=id,RateX=0,RateY=0,RateZ=0,Line2Number=0});f.Rig.Tick();
                f.Rig.CameraGrayFrame(id,stamp,Enumerable.Repeat(pixel,320*240).ToArray());vision.WaitForProcessorIdle();
            }
            Frame(1,100);
            for(int i=0;i<20;i++)f.Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,head:-.15f);
            uint expectedTime=f.Robot.StoredState!.Timestamp;Frame(2,200);
            Assert.Equal(1u,vision.NativeInitializationForTest!.Motion!.Detector.ComparisonCount);
            Assert.Empty(local);Assert.Empty(raw);
            engine.HasCameraCalibration=previousGate;engine.VisionUpdateAllResultsFailed!();engine.DrainAppDelivery();
            Assert.Equal(2,local.Count);Assert.Equal(local[0],local[1]);Assert.Equal(expectedTime,local[0].Timestamp);
            Assert.Equal(1f,local[0].ImageArea);Assert.Equal((short)160,local[0].ImageX);Assert.Equal((short)120,local[0].ImageY);
            Assert.Equal(2,raw.Count);Assert.Equal(raw[0],raw[1]);Assert.Equal(44,raw[0].Length);
            Assert.Equal(expectedTime,System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(raw[0]));
            Assert.Equal(0x3F800000u,System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(raw[0].AsSpan(4)));
            Assert.Equal((short)160,System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(raw[0].AsSpan(8)));
            Assert.Equal((short)120,System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(raw[0].AsSpan(10)));
        }
        finally {engine.HasCameraCalibration=previousGate;engine.EngineToGameMessage-=App;}
    }    [Fact]
    public void GroundSolveLogReplacementCannotAdvanceNewOwnerSchedule()
    {
        using var f=new Fixture();var vision=f.M.Vision;var engine=f.Rig.Engine;var original=f.Robot;
        // Explicit source-state unit input: singular H projects the checked quad onto a diagonal.
        float[] singular={1,0,1,1,0,1,0,0,1};
        using var pose=new Pose3d(Rotation3d.Identity,Vec3.Zero);
        var raw=new RobotState{Timestamp=1000,HeadAngle=0,Status=0x200,Pose=new RobotPose(),Accel=new AccelData(),Gyro=new GyroData(),CliffDataRaw=new ushort[4]};
        using var history=HistRobotState.From(pose,raw);
        using var a=new NativeGroundFrame(original,1000,history,true,singular);
        using var b=new NativeGroundFrame(original,1601,history,true,singular);
        vision.ModeEnableMask=(1<<3)|(1<<4);vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        bool fired=false;EngineRobot? replacement=null;VisionModeSchedule? nextSchedule=null;int counter=-1;
        void Log(EngineLogEntry entry)
        {
            if(fired||entry.Name!="MotionDetector.ExtractGroundPlaneMotion.SolveFailed")return;
            fired=true;engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
            replacement=engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
            nextSchedule=vision.Schedules![(int)VisionMode.DetectingOverheadEdges];counter=nextSchedule.Counter;
        }
        engine.LogEntry+=Log;
        try
        {
            var first=new GrayImage(320,240);first.Fill(100);vision.ProcessNativeCapturedForTest(original,a,first,1,1000);
            var second=new GrayImage(320,240);second.Fill(200);vision.ProcessNativeCapturedForTest(original,b,second,2,1601);
            Assert.True(fired);Assert.NotNull(replacement);Assert.NotNull(nextSchedule);
            Assert.Same(nextSchedule,vision.Schedules![(int)VisionMode.DetectingOverheadEdges]);Assert.Equal(counter,nextSchedule!.Counter);
        }
        finally {engine.LogEntry-=Log;}
    }    [Fact]
    public void ActualF4AndF2CaptureWholeAssociatedHistoryByValue()
    {
        using var f=new Fixture();var vision=f.M.Vision;
        Assert.True(SpinWait.SpinUntil(()=>vision.Enabled,TimeSpan.FromSeconds(5)));
        var state=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,head:-.15f);state.Timestamp=1000;
        f.Rig.Data(state);f.Rig.Tick();vision.ModeEnableMask=0;vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        NativeImageImu[]? captured=null;
        vision.NativeGroundFrameConsumedForTest=frame=>captured=frame.Imu.ToArray();
        try
        {
            f.Rig.Data(new ImageImuData{ImageId=42,RateX=1,RateY=-2,RateZ=3.5f,Line2Number=120});f.Rig.Tick();
            f.Rig.CameraGrayFrame(42,1000,Enumerable.Repeat((byte)100,320*240).ToArray());vision.WaitForProcessorIdle();
            Assert.NotNull(captured);var entry=Assert.Single(captured!);
            Assert.Equal(42u,entry.ImageId);Assert.Equal(120,entry.Line);Assert.Equal(967u,entry.ComputedTimestamp);
            Assert.Equal((1f,-2f,3.5f),(entry.RateX,entry.RateY,entry.RateZ));
            f.Rig.Data(new ImageImuData{ImageId=43,Line2Number=0});f.Rig.Tick();
            Assert.Single(captured!);Assert.Equal(967u,captured![0].ComputedTimestamp);
        }
        finally {vision.NativeGroundFrameConsumedForTest=null;}
    }}
