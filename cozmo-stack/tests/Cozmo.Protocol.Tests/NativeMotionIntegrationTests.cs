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
    private sealed class UnavailableFaces : IFaceDetector
    {
        public bool IsAvailable => false;
        public string Description => "supplied unavailable detector";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp) => throw new InvalidOperationException();
    }
    // Native 0x00539258/0x005394BC oracle, RateZ=3 with no-after fallback.
    private static readonly uint[] NativeZ3ShiftBits = { 0x3F0CCCCDu, 0x3F8CCCCDu, 0x3FD33334u, 0x400CCCCDu, 0x40300000u, 0x40533333u, 0x40766666u, 0x408CCCCDu, 0x409E6667u, 0x40B00001u, 0x40C1999Bu, 0x40D33335u, 0x40E4CCCFu, 0x40F66669u, 0x41040001u, 0x410CCCCEu, 0x4115999Bu, 0x411E6668u, 0x41273335u, 0x41300002u, 0x4138CCCFu, 0x4141999Cu, 0x414A6669u, 0x41533336u, 0x415C0003u, 0x4164CCD0u, 0x416D999Du, 0x4176666Au, 0x417F3337u, 0x41840002u, 0x41886668u, 0x418CCCCEu, 0x41913334u, 0x4195999Au, 0x419A0000u, 0x419E6666u, 0x41A2CCCCu, 0x41A73332u, 0x41AB9998u, 0x41AFFFFEu, 0x41B46664u, 0x41B8CCCAu, 0x41BD3330u, 0x41C19996u, 0x41C5FFFCu, 0x41CA6662u, 0x41CECCC8u, 0x41D3332Eu, 0x41D79994u, 0x41DBFFFAu, 0x41E06660u, 0x41E4CCC6u, 0x41E9332Cu, 0x41ED9992u, 0x41F1FFF8u, 0x41F6665Eu, 0x41FACCC4u, 0x41FF332Au, 0x4201CCC8u, 0x4203FFFBu, 0x4206332Eu, 0x42086661u, 0x420A9994u, 0x420CCCC7u, 0x420EFFFAu, 0x4211332Du, 0x42136660u, 0x42159993u, 0x4217CCC6u, 0x4219FFF9u, 0x421C332Cu, 0x421E665Fu, 0x42209992u, 0x4222CCC5u, 0x4224FFF8u, 0x4227332Bu, 0x4229665Eu, 0x422B9991u, 0x422DCCC4u, 0x422FFFF7u, 0x4232332Au, 0x4234665Du, 0x42369990u, 0x4238CCC3u, 0x423AFFF6u, 0x423D3329u, 0x423F665Cu, 0x4241998Fu, 0x4243CCC2u, 0x4245FFF5u, 0x42483328u, 0x424A665Bu, 0x424C998Eu, 0x424ECCC1u, 0x4250FFF4u, 0x42533327u, 0x4255665Au, 0x4257998Du, 0x4259CCC0u, 0x425BFFF3u, 0x425E3326u, 0x42606659u, 0x4262998Cu, 0x4264CCBFu, 0x4266FFF2u, 0x42693325u, 0x426B6658u, 0x426D998Bu, 0x426FCCBEu, 0x4271FFF1u, 0x42743324u, 0x42766657u, 0x4278998Au, 0x427ACCBDu, 0x427CFFF0u, 0x427F3323u, 0x4280B32Bu, 0x4281CCC5u, 0x4282E65Fu, 0x4283FFF9u };
    // 0x656260/0x656384: original live VC history, strict 0x3E32B8C2 on Y then Z.
    [Theory]
    [InlineData(0x3E32B8C2, false, false)]
    [InlineData(0x3E32B8C3, false, true)]
    [InlineData(0xBE32B8C2, true, false)]
    [InlineData(0xBE32B8C3, true, true)]
    public void WorldRotationUsesLiveF4BracketAndNativeThreshold(uint bits,bool body,bool expected)
    {
        using var f=new Fixture();var vision=f.M.Vision;var owner=f.Robot;
        float rate=BitConverter.Int32BitsToSingle(unchecked((int)bits));
        f.Rig.Data(new ImageImuData{ImageId=101,Line2Number=120,RateY=body?0:rate,RateZ=body?rate:0});f.Rig.Tick();
        uint timestamp=owner.StoredState!.Timestamp;Assert.True(timestamp>65);
        var priorGate=f.Rig.Engine.HasCameraCalibration;f.Rig.Engine.HasCameraCalibration=()=>false;
        try
        {
            f.Rig.CameraGrayFrame(101,timestamp,Enumerable.Repeat((byte)150,320*240).ToArray());
            vision.WaitForProcessorIdle();
            Assert.True(vision.NativeWorldRotatingTooFast(owner,timestamp)); // no after record yet
            f.Rig.Data(new ImageImuData{ImageId=102,Line2Number=0,RateY=body?0:rate,RateZ=body?rate:0});f.Rig.Tick();
            Assert.True(vision.NativeWorldHasImuBracketForTest(owner,timestamp));
            Assert.Equal(expected,vision.NativeWorldRotatingTooFast(owner,timestamp));
        }
        finally{f.Rig.Engine.HasCameraCalibration=priorGate;}
    }
    [Fact]
    public void WorldRotationBracketIncludes64AndRejects65Milliseconds()
    {
        NativeImageImu before=default,after=default;
        var at64=new[]{new NativeImageImu(1,0,0,0,0,936),new NativeImageImu(2,0,0,0,0,0)};
        var at65=new[]{new NativeImageImu(1,0,0,0,0,935),new NativeImageImu(2,0,0,0,0,0)};
        Assert.True(NativeImageImuHistory.TryBracket(at64,1000,ref before,ref after));
        Assert.Equal(936u,before.ComputedTimestamp);Assert.Equal(0u,after.ComputedTimestamp);
        Assert.False(NativeImageImuHistory.TryBracket(at65,1000,ref before,ref after));
    }    [Fact]
    public void WorldMovingGateSkipsRotationQuery()
    {
        using var f=new Fixture();int calls=0;
        var camera=new CameraModel(f.M.Vision.Calibration!,Pose3d.Identity);
        _=f.M.World.CheckForUnobservedObjects(camera,1000,true,false,_=>{calls++;return false;});
        Assert.Equal(0,calls);
    }    [Fact]
    public void WorldRotationBypassIsOriginalOwnerStateAndResetsOnReplacement()
    {
        using var f=new Fixture();var vision=f.M.Vision;var old=f.Robot;
        Assert.True(vision.NativeWorldRotatingTooFast(old,1000));
        f.Rig.Robot.SetVisionWhileMoving(true);f.Rig.Tick();Assert.False(vision.NativeWorldRotatingTooFast(old,1000));
        f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
        var replacement=f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        Assert.True(vision.NativeWorldRotatingTooFast(old,1000));
        Assert.True(vision.NativeWorldRotatingTooFast(replacement,1000));
        vision.SetNativeVisionWhileMoving(old,true);Assert.True(vision.NativeWorldRotatingTooFast(replacement,1000));
    }    [Fact]
    public void VisionWhileMovingRequiresContextInterfaceAtOwnerConstruction()
    {
        using var f=new Fixture();var engine=f.Rig.Engine;var vision=f.M.Vision;
        engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
        engine.NeedsContextExternalInterfaceAvailable=false;
        try
        {
            var owner=engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
            f.Rig.Robot.SetVisionWhileMoving(true);f.Rig.Tick();
            Assert.True(vision.NativeWorldRotatingTooFast(owner,1000));
        }
        finally{engine.NeedsContextExternalInterfaceAvailable=true;}
    }
    [Fact]
    public void QueuedVisionWhileMovingCannotReachReplacementOwner()
    {
        using var f=new Fixture();var engine=f.Rig.Engine;var vision=f.M.Vision;
        f.Rig.Robot.SetVisionWhileMoving(true); // queued for the old original owner
        engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
        var replacement=engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        f.Rig.Tick();Assert.True(vision.NativeWorldRotatingTooFast(replacement,1000));
    }    private sealed class NullFaces:IFaceDetector
    {
        public bool IsAvailable=>true;public string Description=>"supplied null output";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image,uint timestamp)=>null!;
    }
    private sealed class NullPets:IPetDetector
    {
        public bool IsAvailable=>true;
        public IReadOnlyList<DetectedPet> Detect(GrayImage image,uint timestamp)=>null!;
    }
    [Fact]
    public void NullAttachedDetectorsAreVisibleAndDoNotBlockQuality()
    {
        using var f=new Fixture();var vision=f.M.Vision;vision.FaceDetector=new NullFaces();vision.PetDetector=new NullPets();
        var missing=new List<string>();var reports=new List<ProcessedImageReport>();
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x43,true,p=>reports.Add(Assert.IsType<ProcessedImageReport>(p)));
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=missing.Add;
        try
        {
            f.Image(91,modes:(1<<2)|(1<<9)|(1<<7));
            Assert.Contains(missing,x=>x.Contains("attached face detector returned null"));
            Assert.Contains(missing,x=>x.Contains("attached pet detector returned null"));
            var report=Assert.Single(reports);Assert.Contains(7u,report.Modes);
            Assert.DoesNotContain(2u,report.Modes);Assert.DoesNotContain(9u,report.Modes);
        }
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact]
    public void QualityInputReentryCannotCommitOldFrameToReplacement()
    {
        using var f=new Fixture();var vision=f.M.Vision;var engine=f.Rig.Engine;EngineRobot? replacement=null;
        int replacementFrames=-1;
        vision.QualityRectangleInput=()=>
        {
            engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
            replacement=engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
            replacementFrames=vision.FramesProcessed;
            return Array.Empty<ImageQualityRectangle>();
        };
        try
        {
            f.Image(92,modes:1<<7);
            Assert.NotNull(replacement);Assert.Same(replacement,engine.Robot);
            Assert.Equal(replacementFrames,vision.FramesProcessed);Assert.Null(vision.LastResult);
        }
        finally {vision.QualityRectangleInput=null;}
    }
    [Fact]
    public void QualityDiagnosticReentryDoesNotCallOldFrameProvider()
    {
        using var f=new Fixture();var vision=f.M.Vision;var engine=f.Rig.Engine;EngineRobot? replacement=null;int calls=0;
        vision.QualityRectangleInput=()=>{calls++;return Array.Empty<ImageQualityRectangle>();};
        void Missing(string value)
        {
            if(replacement is not null || !value.Contains("Profiler.Tic/Toc CheckingImageQuality"))return;
            engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
            replacement=engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        }
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Missing;
        try {f.Image(93,modes:1<<7);Assert.NotNull(replacement);Assert.Equal(0,calls);Assert.Null(vision.LastResult);}
        finally {SteppedBehavior.MissingReported-=Missing;vision.QualityRectangleInput=null;}
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderedMarkersRespectLatchedPhysicalCorrectionBoundary(bool physical)
    {
        using var f=new Fixture();var vision=f.M.Vision;var engine=f.Rig.Engine;var owner=f.Robot;
        owner.HandleFirmwareVersion(new FirmwareVersion{Signature=System.Text.Encoding.UTF8.GetBytes(physical?"{}":"{\"sim\":true}")});
        vision.SetNativeOuterCalibration(vision.Calibration!);
        Assert.Equal(physical,vision.NativeWorkerPhysicalForTest);
        Assert.True(ImageQualitySettings.Globals.MeterFromDetections);Assert.Null(vision.QualityRectangleInput);
        vision.ModeEnableMask=(1<<1)|(1<<7);vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        var reports=new List<ProcessedImageReport>();var frames=new List<VisionFrameResult>();var missing=new List<string>();
        var rawMarkers=new List<ObservedMarker>();var rawRectangles=new List<IReadOnlyList<ImageQualityRectangle>>();
        vision.NativeQualityRectanglesObservedForTest=rectangles=>rawRectangles.Add(rectangles.ToArray());
        using var sub=engine.AnimationMessages.SubscribeMessage(0x43,true,p=>reports.Add(Assert.IsType<ProcessedImageReport>(p)));
        void Frame(VisionFrameResult value)=>frames.Add(value);
        vision.FrameProcessed+=Frame;SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=missing.Add;
        try
        {
            for(uint index=1;index<=2;index++)
            {
                f.Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.HeadInPos,head:-.15f);
                uint stamp=owner.PoseHistory.NewestTimeStamp;var pd=vision.History.At(stamp)!.Value;
                Assert.True(stamp>65); // Every native strip query exceeds the retained zero timestamp.
                var image=new GrayImage(320,240);image.Fill(150);
                using(var objectPose=new Pose3d(Mat3.Identity,new Vec3(150,0,22)))
                using(var cameraPose=pd.CameraPose)
                    MarkerRenderer.DrawObject(image,MarkerLibrary.EmbeddedOrNull??throw new InvalidOperationException("Required marker assets missing"),new CameraModel(vision.Calibration!,cameraPose),ObjectType.Block_LIGHTCUBE1,objectPose);
                // Earlier image ID leaves its timestamp zero; native helper uses its last-sample fallback.
                f.Rig.Data(new ImageImuData{ImageId=0,RateZ=physical?3f:0f,Line2Number=0});f.Rig.Tick();
                f.Rig.CameraGrayFrame(index,stamp,image.Pixels);vision.WaitForProcessorIdle();
                rawMarkers.Add(vision.Detector.LastQuads.Select(x=>x.Marker).First(x=>x is not null)!);
            }
            engine.VisionUpdateAllResultsFailed!();
            Assert.Equal(2,reports.Count);
            Assert.DoesNotContain(missing,x=>x.Contains("marker pixel-shift"));
            Assert.Equal(2,frames.Count);Assert.All(reports,r=>Assert.Contains(1u,r.Modes));
            Assert.Contains(frames,x=>x.Markers.Count!=0);
            Assert.Contains(vision.World.LocatedObjects,x=>x.Type==ObjectType.Block_LIGHTCUBE1);
            Assert.Equal(2,rawMarkers.Count);Assert.Equal(2,rawRectangles.Count);
            for(int i=0;i<2;i++)
            {
                var raw=rawMarkers[i];var xs=raw.Corners.Select(x=>(int)(float)x.X).ToArray();var ys=raw.Corners.Select(x=>(int)(float)x.Y).ToArray();
                Assert.Equal(new ImageQualityRectangle(xs.Min(),ys.Min(),xs.Max()-xs.Min(),ys.Max()-ys.Min()),Assert.Single(rawRectangles[i]));
                var corrected=Assert.Single(frames[i].Markers.Where(x=>x.Code==raw.Code));
                int stripe=(int)MathF.Floor((float)raw.Corners[0].Y/2f);
                float shift=physical?BitConverter.UInt32BitsToSingle(NativeZ3ShiftBits[stripe]):0f;
                float expected=(float)raw.Corners[0].X-shift;
                Assert.Equal(BitConverter.SingleToUInt32Bits(expected),BitConverter.SingleToUInt32Bits((float)corrected.Corners[0].X));
            }
        }
        finally {vision.NativeQualityRectanglesObservedForTest=null;vision.FrameProcessed-=Frame;SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact]
    public void RealInitMeterUsesCurrentFrameVectorWithoutExternalRectangleProvider()
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
            settings.MeterFromDetections=true;reports.Clear();Frame(32);Assert.Contains(7u,Assert.Single(reports).Modes);
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
        vision.FaceDetector=new UnavailableFaces();
        using var pose=new Pose3d(Rotation3d.Identity,Vec3.Zero);
        var raw=new RobotState{Timestamp=1000,HeadAngle=0,Status=0x200,Pose=new RobotPose(),Accel=new AccelData(),Gyro=new GyroData(),CliffDataRaw=new ushort[4]};
        using var history=HistRobotState.From(pose,raw);
        using var ground=new NativeGroundFrame(original,1000,history,false,new float[9]);
        vision.ModeEnableMask=(1<<2)|(1<<3);vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        EngineRobot? replacement=null;VisionModeSchedule? schedule=null;int counter=-1;
        void Missing(string message)
        {
            if(replacement is not null || !message.Contains("face detector unavailable"))return;
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
