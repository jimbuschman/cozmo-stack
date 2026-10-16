using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class M11ImageQualityRowsTests
{
    private static ImageQualityProcessor Processor()=>new(){Settings=new ImageQualitySettings()};
    private static CameraSettings.QualityCameraState State(int e=16,float g=2,int minE=1,int maxE=66,float minG=0.1f,float maxG=4)=>new(e,g,minE,maxE,minG,maxG);
    private sealed class Pipeline:IImageQualityPipeline
    {
        internal uint Status;
        internal float Factor=1;
        internal GrayImage? Mask;
        internal float InitialFactor;
        internal uint Low=231,High=19;
        public uint ComputeExposureAdjustment(GrayImage image,GrayImage? mask,ref float factor)
        {Mask=mask;InitialFactor=factor;factor=Factor;return Status;}
        public uint ComputePercentile(float percentile)=>percentile<0.5f?Low:High;
    }
    [Theory]
    [InlineData(16,66,20)] [InlineData(60,66,60)] [InlineData(66,66,60)]
    [InlineData(70,66,70)] [InlineData(1,9,0)] [InlineData(-11,66,0)]
    [InlineData(int.MaxValue,66,-2147483646)] [InlineData(-10,0,-10)]
    public void LimitedExposureFollowsSignedDivisionWrapAndDivisibilityBypass(int e,int max,int expected)
        =>Assert.Equal(expected,ImageQualityProcessor.Quantize(e,max));

    [Theory]
    [InlineData(0x3F7FFF58u)] [InlineData(0x3F800054u)] [InlineData(0x7FC12345u)]
    public void DeadbandEqualityAndNaNLeaveGainBitsAndStillQuantize(uint bits)
    {
        var p=Processor();float g=BitConverter.UInt32BitsToSingle(0x7F854321);
        var result=p.Propose(BitConverter.UInt32BitsToSingle(bits),State(16,g),1<<14,_=>throw new Exception("unexpected percentile"));
        Assert.Equal(20,result.Exposure);Assert.Equal((byte)1,result.Quality);
        Assert.Equal(0x7F854321u,BitConverter.SingleToUInt32Bits(result.Gain));
        Assert.Equal(16,p.Propose(1,State(),0,_=>0).Exposure);
    }
    [Fact]
    public void ExposureFirstDecreaseGainFirstIncreaseAndArmConversionAreExact()
    {
        var p=Processor();
        Assert.Equal(new ImageQualityProposal(1,8,2),p.Propose(0.5f,State(),0,_=>0));
        Assert.Equal(new ImageQualityProposal(1,1,1),p.Propose(0.5f,State(1),0,_=>0));
        Assert.Equal(new ImageQualityProposal(1,16,4),p.Propose(2,State(),0,_=>0));
        Assert.Equal(new ImageQualityProposal(1,32,4),p.Propose(2,State(16,4),0,_=>0));
        var nanGain=p.Propose(2,State(16,float.NaN),0,_=>0);
        Assert.Equal(32,nanGain.Exposure);Assert.True(float.IsNaN(nanGain.Gain));
        // 0 * +inf is NaN; native unordered product retains maxGain.
        Assert.Equal(4,p.Propose(float.PositiveInfinity,State(16,0),0,_=>0).Gain);
        Assert.Equal(int.MaxValue,ImageQualityProcessor.ArmSigned(float.PositiveInfinity));
        Assert.Equal(int.MinValue,ImageQualityProcessor.ArmSigned(float.NegativeInfinity));
        Assert.Equal(0,ImageQualityProcessor.ArmSigned(float.NaN));
        Assert.Equal(5,p.Propose(0.5f,State(9),0,_=>0).Exposure); // roundf ties away
    }
    [Theory]
    [InlineData(231u,3)] [InlineData(230u,1)]
    public void TooBrightUsesUnsignedStrictPercentileAndUnorderedGain(uint value,byte expected)
    {
        var p=Processor();float requested=0;
        var result=p.Propose(0.5f,State(1,float.NaN),0,v=>{requested=v;return value;});
        Assert.Equal(expected,result.Quality);Assert.Equal(0x3DCCCCCDu,BitConverter.SingleToUInt32Bits(requested));
    }
    [Theory]
    [InlineData(19u,2)] [InlineData(20u,1)]
    public void TooDarkUsesShippedThresholdAndNaNGainExposureBranch(uint value,byte expected)
    {
        var p=Processor();p.Settings.TooDark=20;float requested=0;
        var result=p.Propose(2,State(66,float.NaN),0,v=>{requested=v;return value;});
        Assert.Equal(expected,result.Quality);Assert.Equal(0x3F666666u,BitConverter.SingleToUInt32Bits(requested));
    }
    [Fact]
    public void MaskUsesSignedDoubleCountedAreaAndComplementWeights()
    {
        var p=Processor();var image=new GrayImage(10,10);
        Assert.True(p.TryMask(image,new[]{new ImageQualityRectangle(0,0,2,5)},out var mask));
        Assert.Equal((byte)230,mask![0,0]);Assert.Equal((byte)25,mask[9,9]);
        Assert.True(p.TryMask(image,new[]{new ImageQualityRectangle(0,0,2,5),new ImageQualityRectangle(0,0,2,5)},out mask));
        Assert.Equal((byte)204,mask![0,0]);Assert.Equal((byte)51,mask[9,9]);
        Assert.True(p.TryMask(image,new[]{new ImageQualityRectangle(0,0,5,10)},out mask));Assert.Null(mask); // equality -> whole image
        p.Settings.MeterFromDetections=false;
        Assert.True(p.TryMask(image,new[]{new ImageQualityRectangle(-1,0,2,5)},out mask));Assert.Null(mask);
    }
    [Fact]
    public void PipelineStartsWithOneAndFailureDoesNotComputeOrQuantizeAProposal()
    {
        var p=Processor();var pipe=new Pipeline{Status=7};p.Pipeline=pipe;
        var logs=new List<(EngineLogLevel Level,string? Channel,string Key,string Text)>();p.SourceLog=(l,c,k,t)=>logs.Add((l,c,k,t));
        Assert.True(p.TryCheck(new GrayImage(10,10),new[]{new ImageQualityRectangle(0,0,2,5)},State(),1<<14,out var status,out var proposal));
        Assert.Equal(7u,status);Assert.Equal(default,proposal);Assert.Equal(1,pipe.InitialFactor);
        var log=Assert.Single(logs);Assert.Equal(EngineLogLevel.Warning,log.Level);Assert.Null(log.Channel);
        Assert.Equal("VisionSystem.CheckImageQuality.ComputeNewExposureFailed",log.Key);Assert.Equal("Detection Quads=1",log.Text);
    }
    [Fact]
    public void ConfigUsesShippedByteValueAndMissingKeyStopsAfterPriorWrites()
    {
        var settings=new ImageQualitySettings();var logs=new List<(string Key,string Text)>();
        bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            Assert.True(Json.TryParseFirst(Encoding.UTF8.GetBytes("{\"ImageQuality\":{\"TooBrightValue\":230,\"TooDarkValue\":20,\"MeterFromDetections\":true,\"LowPercentile\":0.1,\"MidPercentile\":0.5,\"HighPercentile\":0.9}}"),out var doc));
            using(doc)Assert.True(settings.Load(doc.RootElement,(_,_,k,t)=>logs.Add((k,t))));
            Assert.Equal((byte)20,settings.TooDark);Assert.Empty(logs);
            Assert.True(Json.TryParseFirst("{\"ImageQuality\":{\"TooBrightValue\":229}}"u8.ToArray(),out doc));
            EngineErrorState.ErrorFlagSet=false;
            using(doc)Assert.False(settings.Load(doc.RootElement,(_,_,k,t)=>logs.Add((k,t))));
            Assert.Equal((byte)229,settings.TooBright);Assert.Equal((byte)20,settings.TooDark);
            Assert.Equal(("VisionSystem.Init.MissingJsonParameter","TooDarkValue"),Assert.Single(logs));Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally{EngineErrorState.ErrorFlagSet=old;}
    }
    private sealed class Faces:IFaceDetector
    {
        internal int Calls;
        public bool IsAvailable=>true;public string Description=>"native detector input";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image,uint timestamp)
        {Calls++;return new[]{new DetectedFace(1,new FaceRect(1,1,8,8))};}
    }
    private static CameraCalibration Cal()=>new(){Rows=16,Columns=16,FocalLengthX=20,FocalLengthY=20,CenterX=8,CenterY=8};
    private static VisionFrameResult Frame(VisionSystem vision,uint n=1)=>vision.ProcessImage(new GrayImage(16,16),n,n,new VisionPoseData(n,Pose3d.Identity,0,0,false,false));
    [Fact]
    public void RealFrameEntryDropsAllRecipientsAndPublicationOnQualityFailure()
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot,Cal()){ModeEnableMask=(1<<7)|(1<<2)};
            var pipe=new Pipeline();vision.QualityProcessor.Pipeline=pipe;vision.QualityRectangleInput=()=>Array.Empty<ImageQualityRectangle>();
            var first=Frame(vision);int worldCalls=vision.World.UnbuiltLocalizeRobotCalls;
            int published=0;vision.FrameProcessed+=_=>published++;
            var faces=new Faces();vision.FaceDetector=faces;pipe.Status=7;
            Assert.True(robot.CameraSettings.SetCameraSettings(31,2));
            var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
            Assert.Throws<OperationCanceledException>(()=>Frame(vision,2));
            Assert.Equal(1,faces.Calls);Assert.Equal(0,vision.Faces.Count);Assert.Equal(worldCalls,vision.World.UnbuiltLocalizeRobotCalls);
            Assert.Same(first,vision.LastResult);Assert.Equal(1,vision.FramesProcessed);Assert.Equal(0,published);
            Assert.Equal(new[]{"VisionSystem.CheckImageQuality.ComputeNewExposureFailed","VisionSystem.Update.CheckImageQualityFailed"},logs.Select(l=>l.Name));
            Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.Equal(31,robot.CameraSettings.CurrentExposureMs);Assert.Null(robot.CameraSettings.NextCameraParams); // later quality failure does not undo earlier copy
        }
        finally{EngineErrorState.ErrorFlagSet=old;}
    }
    [Fact]
    public void PendingPairCopiesBeforeQualityAndLimitedExposureDoesNotOverwriteCurrent()
    {
        using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot,Cal()){ModeEnableMask=(1<<7)|(1<<14)};
        vision.QualityProcessor.Pipeline=new Pipeline();vision.QualityRectangleInput=()=>Array.Empty<ImageQualityRectangle>();
        Assert.True(robot.CameraSettings.SetCameraSettings(17,1.5f));Assert.Equal(16,robot.CameraSettings.CurrentExposureMs);
        var result=Frame(vision);
        Assert.Equal(new ImageQualityProposal(1,20,1.5f),result.ImageQuality);
        Assert.Equal(17,robot.CameraSettings.CurrentExposureMs);Assert.Equal(1.5f,robot.CameraSettings.CurrentGain);Assert.Null(robot.CameraSettings.NextCameraParams);
    }
    [Fact]
    public void SetAndDisableBroadcastsOldLiveByteThenQueuesModeSevenInSharedFifo()
    {
        using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot,Cal()){ModeEnableMask=1<<7};
        var reports=new List<CurrentCameraParams>();robot.CameraSettings.CurrentCameraParamsChanged+=p=>
        {Assert.True(robot.CameraSettings.AutoExposureEnabled);Assert.Equal(1<<7,vision.ModeEnableMask);reports.Add(p);};
        robot.CameraSettings.SetAndDisableAutoExposure(16,1);
        Assert.True(Assert.Single(reports).AutoExposureEnabled);Assert.False(robot.CameraSettings.AutoExposureEnabled);
        Assert.Equal(1<<7,vision.ModeEnableMask);
        robot.Lights.SetHeadlight(true);Frame(vision);
        Assert.Equal(1<<14,vision.ModeEnableMask);
    }
    [Fact]
    public void NullConfigValueIsMissingRatherThanAZeroAndTerminatingConversionKeepsPriorStatics()
    {
        var settings=new ImageQualitySettings();var logs=new List<string>();bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            Assert.True(Json.TryParseFirst("{\"ImageQuality\":{\"TooBrightValue\":null}}"u8.ToArray(),out var doc));
            using(doc)Assert.False(settings.Load(doc.RootElement,(_,_,_,text)=>logs.Add(text)));
            Assert.Equal("TooBrightValue",Assert.Single(logs));Assert.Equal((byte)230,settings.TooBright);
            Assert.True(Json.TryParseFirst("{\"ImageQuality\":{\"TooBrightValue\":229,\"TooDarkValue\":\"bad\"}}"u8.ToArray(),out doc));
            using(doc)Assert.Throws<JsonLogicError>(()=>settings.Load(doc.RootElement,(_,_,_,_)=>{}));
            Assert.Equal((byte)230,settings.TooBright);Assert.Equal((byte)15,settings.TooDark);
        }
        finally{EngineErrorState.ErrorFlagSet=old;}
    }
    [Fact]
    public void LiveCaptureStopsOnUnknownRoiProducerWithoutFabricatingAnEngineFailure()
    {
        using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot,Cal()){ModeEnableMask=1<<7};
        vision.History.Add(new Cozmo.Protocol.RobotState{Timestamp=1,PoseOriginId=1,Pose=new Cozmo.Protocol.RobotPose(),Accel=new Cozmo.Protocol.AccelData(),Gyro=new Cozmo.Protocol.GyroData()});
        var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        Assert.Null(vision.ProcessCapture(new GrayImage(16,16),1,1));Assert.Null(vision.LastResult);Assert.Equal(0,vision.FramesProcessed);
        Assert.DoesNotContain(logs,l=>l.Name=="VisionSystem.Update.CheckImageQualityFailed"||l.Name=="VisionSystem.CheckImageQuality.ComputeNewExposureFailed");
        vision.QualityRectangleInput=()=>Array.Empty<ImageQualityRectangle>();vision.QualityProcessor.Pipeline=new Pipeline();
        Assert.NotNull(vision.ProcessCapture(new GrayImage(16,16),2,1));
    }

    [Fact]
    public void VisionConstructorLoadsTheRealShippedQualityStaticsFromItsResourceInput()
    {
        string root=Directory.CreateTempSubdirectory("cozmo-b116-").FullName;
        var global=ImageQualitySettings.Globals;
        var previous=(global.TooBright,global.TooDark,global.MeterFromDetections,global.Low,global.Mid,global.High);
        try
        {
            string source=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../re-analysis/obb/assets/cozmo_resources/config/engine/vision_config.json"));
            string folder=Path.Combine(root,"config","engine");Directory.CreateDirectory(folder);
            File.Copy(source,Path.Combine(folder,"vision_config.json"));
            using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,new CozmoEngineOptions{ResourcesPath=root});
            using var vision=new VisionSystem(robot,Cal());
            Assert.Equal((byte)20,vision.QualityProcessor.Settings.TooDark);
            Assert.Equal((byte)230,vision.QualityProcessor.Settings.TooBright);
            Assert.Equal(0x3F666666u,BitConverter.SingleToUInt32Bits(vision.QualityProcessor.Settings.High));
        }
        finally
        {
            (global.TooBright,global.TooDark,global.MeterFromDetections,global.Low,global.Mid,global.High)=previous;
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()),Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("cozmo-b116-",Path.GetFileName(root));
            Directory.Delete(root,true);
        }
    }

    [Fact]
    public void ADisabledFaceProducerIsNotRunBeforeTheQualityConsumer()
    {
        using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot,Cal()){ModeEnableMask=1<<7};
        var faces=new Faces();vision.FaceDetector=faces;
        vision.QualityProcessor.Pipeline=new Pipeline();vision.QualityRectangleInput=()=>Array.Empty<ImageQualityRectangle>();
        Assert.NotNull(Frame(vision).ImageQuality);Assert.Equal(0,faces.Calls);Assert.Equal(0,vision.Faces.Count);
    }

    [Fact]
    public void SetAndDisableKeepsSignedNativeExposureAndDisablesAfterValidationFailure()
    {
        using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot,Cal()){ModeEnableMask=1<<7};
        int reports=0;robot.CameraSettings.CurrentCameraParamsChanged+=_=>reports++;
        var warnings=new List<EngineLogEntry>();robot.Engine.LogEntry+=entry=>
        {
            if(entry.Name!="VisionSystem.IsExposureValid.OOR")return;
            Assert.True(robot.CameraSettings.AutoExposureEnabled);Assert.Equal(1<<7,vision.ModeEnableMask);
            warnings.Add(entry);
        };
        robot.CameraSettings.SetAndDisableAutoExposure(-11,1);
        Assert.Equal("(tc0000) : Exposure -11ms not in range 1ms to 66ms",Assert.Single(warnings).Text);
        Assert.Equal(0,reports);Assert.Null(robot.CameraSettings.NextCameraParams);Assert.False(robot.CameraSettings.AutoExposureEnabled);
        Frame(vision);Assert.Equal(1,vision.ModeEnableMask); // request still applied after failed settings validation
    }

    [Fact]
    public void HostRobotTeardownUsesTheSharedProcessFlagGate()
    {
        using var robot=CozmoRobot.CreateOffline();robot.Engine.Robots.AddRobot(CozmoEngine.RobotId);
        var native=robot.Engine.Robot!;var node=native.GetNativeOrigin(native.CurrentOriginId).Node!;
        using var started=new ManualResetEventSlim();Task task;
        lock(Pose3d.UnownedParentsGate)
        {
            task=Task.Run(()=>{started.Set();native.DestroyPoseOriginOwners();});
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(task.Wait(100));Assert.Equal(1u,node.Owners);
        }
        Assert.True(task.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(0u,node.Owners);Assert.False(Pose3d.UnownedParentsAllowed);
    }

}
