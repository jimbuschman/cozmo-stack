using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Robot.Vision.Jpeg;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class M11DecodeBoundaryTests
{
    private static CameraCalibration Cal()=>new(){Rows=16,Columns=16,FocalLengthX=20,FocalLengthY=20,CenterX=8,CenterY=8};
    private static CameraFrame Frame(byte encoding,uint id=101)=>new(){ImageId=id,Timestamp=id,Width=16,Height=16,Encoding=encoding,RawPayload=encoding==1?new byte[256]:Array.Empty<byte>()};
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void HelpersUnwindRatherThanInventAFailureResult(bool rgb)
    {
        var frame=Frame(5);var logs=new List<string>();
        if(rgb)Assert.Throws<OpenCvException>(()=>frame.TryDecodeRgb(out _,out _,logs.Add));
        else Assert.Throws<OpenCvException>(()=>frame.TryDecodeGray(out _,out _,logs.Add));
        Assert.StartsWith("error: cv::error(): OpenCV Error:",Assert.Single(logs));
    }
    [Theory]
    [InlineData((byte)5)] [InlineData((byte)6)]
    public void SynchronousEntrySkipsThrowKeepsCacheAndEmitsOnlyStackPolicy(byte encoding)
    {
        using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot,Cal());
        var prior=new GrayImage(2,2);vision.EncodedFrameCache.Reset(prior);
        var source=new List<EngineLogEntry>();robot.Engine.LogEntry+=source.Add;
        var missing=new List<string>();SteppedBehavior.MissingReported+=missing.Add;
        try{Assert.Null(vision.ProcessFrame(Frame(encoding,(uint)(110+encoding))));}
        finally{SteppedBehavior.MissingReported-=missing.Add;}
        Assert.Same(prior,vision.EncodedFrameCache.Gray);Assert.True(vision.EncodedFrameCache.GrayValid);
        Assert.Contains(missing,m=>m.Contains("M1-034/M11-058 decode throw"));
        Assert.DoesNotContain(source,e=>e.Name=="VisionComponent.UpdateVisionSystem.UpdateFailed");
        Assert.Null(vision.LastResult);Assert.Equal(0,vision.FramesProcessed);
    }
    [Fact]
    public void ReturnedFailureResetsEmptyCacheAndReachesTheNativeWarning()
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot,Cal());
            vision.EncodedFrameCache.Reset(new GrayImage(2,2));var source=new List<EngineLogEntry>();robot.Engine.LogEntry+=source.Add;
            Assert.Null(vision.ProcessFrame(Frame(3)));
            Assert.Null(vision.EncodedFrameCache.Gray);Assert.True(vision.EncodedFrameCache.RgbValid);Assert.Equal(0,vision.EncodedFrameCache.Width);
            var warning=Assert.Single(source,e=>e.Name=="VisionComponent.UpdateVisionSystem.UpdateFailed");
            Assert.Equal(EngineLogLevel.Warning,warning.Level);Assert.Null(warning.Channel);Assert.Equal("(tc0000) : ",warning.Text);
        }
        finally{EngineErrorState.ErrorFlagSet=old;}
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WorkerSkipsCompletionAfterThrowButDiscardsPendingAfterReturnedFailure(bool throws)
    {
        bool old=EngineErrorState.ErrorFlagSet;
        using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot,Cal()){Enabled=true,ModeEnableMask=0};
        int handed=0;
        void Pending()=>vision.HandOverFrame(Frame(1,120));
        void Missing(string text){if(text.Contains("M1-034/M11-058 decode throw")&&text.Contains("image 119 ")){Interlocked.Increment(ref handed);Pending();}}
        void Source(EngineLogEntry entry){if(!throws&&entry.Name=="VisionComponent.UpdateVisionSystem.UpdateFailed"){Interlocked.Increment(ref handed);Pending();}}
        SteppedBehavior.MissingReported+=Missing;robot.Engine.LogEntry+=Source;
        try
        {
            vision.HandOverFrame(Frame(throws?(byte)5:(byte)3,119));vision.WaitForProcessorIdle();
            Assert.Equal(1,handed);
            Assert.Equal(throws?16:0,vision.EncodedFrameCache.Width);
            Assert.Null(vision.LastResult);
        }
        finally{SteppedBehavior.MissingReported-=Missing;robot.Engine.LogEntry-=Source;EngineErrorState.ErrorFlagSet=old;}
    }
    // reach: unit (direct replay and seeded face world substitute album restore; OKAO album parsing MISSING M11-016).
    [Theory]
    [InlineData(0)] [InlineData(2)]
    public void ReplayErasesOnceThenLogsEachNamedEntryBeforeBroadcast(int count)
    {
        using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot,Cal());
        for(int id=1;id<=count;id++)vision.Faces.AddOrUpdateFace(new TrackedFace(new DetectedFace(id,new FaceRect(0,0,10,10),Name:"name"+id),1),Pose3d.Identity,false);
        var host=new List<string>();var local=new List<string>();var logs=new List<string>();
        vision.EnrolledFacesErased+=()=>host.Add("erase");vision.EnrolledFacesErasedInternal+=()=>local.Add("erase");
        robot.Engine.LogEntry+=e=>{if(e.Name=="VisionComponent.BroadcastLoadedNamesAndIDs"){Assert.Equal("VisionComponent",e.Channel);Assert.Equal(EngineLogLevel.Info,e.Level);logs.Add(e.Text);}};
        vision.LoadedFaceName+=(id,name)=>host.Add("name"+id);
        vision.LoadedFaceNameInternal+=(id,name)=>local.Add("name"+id);
        vision.BroadcastLoadedNamesAndIDs();Assert.Empty(host);
        Assert.Equal(new[]{"erase"}.Concat(Enumerable.Range(1,count).Select(id=>"name"+id)),local);
        Assert.Equal(Enumerable.Range(1,count).Select(id=>"broadcasting loaded face id: "+id),logs);
        robot.Engine.DrainAppDelivery();Assert.Equal(local,host);
    }
    [Theory]
    [InlineData("TimeBeforeErrorMessage_ms")]
    [InlineData("RepeatedErrorMessageInverval_ms")]
    [InlineData("InitialExposureTime_ms")]
    [InlineData("DropStatsWindowLength_sec")]
    public void ComponentConfigReportsTheFirstMissingRequiredKeyAndKeepsEarlierStores(string absent)
    {
        RunConfig(absent,(robot,vision,logs)=>
        {
            var entry=Assert.Single(logs.Where(e=>e.Name=="VisionSystem.Init.MissingJsonParameter"));
            Assert.Equal("(tc0000) : "+absent,entry.Text);Assert.Equal(EngineLogLevel.Error,entry.Level);Assert.Null(entry.Channel);
            Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.Equal(absent=="TimeBeforeErrorMessage_ms"?3000u:3001u,VisionSystem.TimeBeforeQualityErrorMs);
            Assert.Equal(absent is "TimeBeforeErrorMessage_ms" or "RepeatedErrorMessageInverval_ms"?5000u:5001u,VisionSystem.RepeatedQualityErrorIntervalMs);
            Assert.Equal((ushort)(absent=="DropStatsWindowLength_sec"?17:16),CameraSettings.ConfiguredInitialExposureTimeMs);
        });
    }
    [Fact]
    public void ComponentConfigNarrowsAsUIntToU16AndReadsDropSecondsBeforeSystemQuality()
    {
        RunConfig(null,(robot,vision,logs)=>
        {
            Assert.Equal((ushort)17,CameraSettings.ConfiguredInitialExposureTimeMs);
            Assert.Equal(30f,vision.PendingDropStatsSeconds);
            // All C reads passed; first omitted S field is TooBrightValue. No successful full Init is inferred.
            Assert.Equal("(tc0000) : TooBrightValue",Assert.Single(logs.Where(e=>e.Name=="VisionSystem.Init.MissingJsonParameter")).Text);
            robot.CameraSettings.Handle(new Cozmo.Protocol.DefaultCameraParams{Field0=4,Field1=1,Field2=1,Field3=66});
            Assert.Equal((17,1f),robot.CameraSettings.NextCameraParams!.Value);
            Assert.Contains(logs,e=>e.Name=="VisionSystem.SetCameraExposureParams.Success"&&e.Text.Contains("Current Gain:17ms"));
        });
    }
    private static void RunConfig(string? absent,Action<CozmoRobot,VisionSystem,List<EngineLogEntry>> check)
    {
        var old=(VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs,EngineErrorState.ErrorFlagSet);
        string root=Directory.CreateTempSubdirectory("cozmo-b117-").FullName;
        try
        {
            VisionSystem.TimeBeforeQualityErrorMs=3000;VisionSystem.RepeatedQualityErrorIntervalMs=5000;CameraSettings.ConfiguredInitialExposureTimeMs=16;EngineErrorState.ErrorFlagSet=false;
            var quality=new Dictionary<string,object>{{"TimeBeforeErrorMessage_ms",3001},{"RepeatedErrorMessageInverval_ms",5001},{"InitialExposureTime_ms",65553}};
            var performance=new Dictionary<string,object>{{"DropStatsWindowLength_sec",30}};
            if(absent=="DropStatsWindowLength_sec")performance.Remove(absent);else if(absent is not null)quality.Remove(absent);
            string folder=Path.Combine(root,"config","engine");Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"vision_config.json"),System.Text.Json.JsonSerializer.Serialize(new{ImageQuality=quality,PerformanceLogging=performance}));
            using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,new CozmoEngineOptions{ResourcesPath=root});
            var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
            using var vision=new VisionSystem(robot,Cal());check(robot,vision,logs);
        }
        finally
        {
            (VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs,EngineErrorState.ErrorFlagSet)=old;
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()),Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("cozmo-b117-",Path.GetFileName(root));Directory.Delete(root,true);
        }
    }

    [Theory]
    [InlineData((byte)5)] [InlineData((byte)6)]
    public void WrongSizeReturnedFailureResetsWithTheActualDecodedImage(byte encoding)
    {
        using var zip=System.IO.Compression.ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory,"Fixtures","jpeg_decode","data.zip"));
        using var input=zip.GetEntry("in/b0_16x16_ri1_n_ad2_f0.bin")!.Open();using var bytes=new MemoryStream();input.CopyTo(bytes);
        using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot,CameraCalibration.Nominal());
        var frame=new CameraFrame{ImageId=130,Timestamp=130,Width=320,Height=240,Encoding=encoding,Jpeg=bytes.ToArray(),RawPayload=bytes.ToArray()};
        var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        Assert.Null(vision.ProcessFrame(frame));Assert.Equal(16,vision.EncodedFrameCache.Width);Assert.Equal(16,vision.EncodedFrameCache.Height);
        Assert.Equal(encoding==6,vision.EncodedFrameCache.RgbValid);Assert.Equal(encoding==5,vision.EncodedFrameCache.GrayValid);
        Assert.Contains(logs,e=>e.Name=="VisionComponent.UpdateVisionSystem.UpdateFailed");
    }

}
