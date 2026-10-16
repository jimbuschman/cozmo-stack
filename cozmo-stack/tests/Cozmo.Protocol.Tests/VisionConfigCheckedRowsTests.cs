using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class VisionConfigCheckedRowsTests:IDisposable
{
    private readonly bool _error=EngineErrorState.ErrorFlagSet;
    private readonly ImageQualitySettings _saved=new(){TooBright=ImageQualitySettings.Globals.TooBright,TooDark=ImageQualitySettings.Globals.TooDark,MeterFromDetections=ImageQualitySettings.Globals.MeterFromDetections,Low=ImageQualitySettings.Globals.Low,Mid=ImageQualitySettings.Globals.Mid,High=ImageQualitySettings.Globals.High};
    private readonly uint _time=VisionSystem.TimeBeforeQualityErrorMs,_repeat=VisionSystem.RepeatedQualityErrorIntervalMs;
    private readonly ushort _exposure=CameraSettings.ConfiguredInitialExposureTimeMs;
    public void Dispose()
    {
        var g=ImageQualitySettings.Globals;(g.TooBright,g.TooDark,g.MeterFromDetections,g.Low,g.Mid,g.High)=(_saved.TooBright,_saved.TooDark,_saved.MeterFromDetections,_saved.Low,_saved.Mid,_saved.High);
        VisionSystem.TimeBeforeQualityErrorMs=_time;VisionSystem.RepeatedQualityErrorIntervalMs=_repeat;CameraSettings.ConfiguredInitialExposureTimeMs=_exposure;EngineErrorState.ErrorFlagSet=_error;
    }
    [Fact] public void ShippedExposureInputsConnectToActualFrameConsumer()
    {
        using var resources=new BackupTestResources();using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,resources.Options());
        using var vision=new VisionSystem(robot,new CameraCalibration{Rows=16,Columns=16,FocalLengthX=20,FocalLengthY=20,CenterX=8,CenterY=8}){ModeEnableMask=1<<7};
        var p=Assert.IsType<ExposurePipeline>(vision.QualityProcessor.Pipeline);Assert.Equal((byte)115,p.MidValue);Assert.Equal(0x3F000000u,BitConverter.SingleToUInt32Bits(p.MidPercentile));Assert.Equal(0x3F4CCCCDu,BitConverter.SingleToUInt32Bits(p.MaxChange));Assert.Equal(2,p.SubSample);
        vision.QualityRectangleInput=()=>Array.Empty<ImageQualityRectangle>();var image=new GrayImage(16,16);image.Fill(100);
        var frame=vision.ProcessImage(image,1,1,new VisionPoseData(1,Pose3d.Identity,0,0,false,false));Assert.NotNull(frame.ImageQuality);Assert.Equal(64,p.Histogram.Total);Assert.Equal(64,p.Histogram.Bins[100]);
        // Explicit test ROI boundary does not claim detector rectangle production or camera application.
    }
    [Fact] public void InitFailureLogsParameterErrorThenItsFailureTailWithoutSuccessInfo()
    {
        using var resources=new BackupTestResources();var options=resources.Options();string path=Path.Combine(options.ResourcesPath!,CozmoEngine.VisionConfigurationPath);
        File.WriteAllText(path,File.ReadAllText(path).Replace("0.80","2.00",StringComparison.Ordinal));
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,options);var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        using var vision=new VisionSystem(robot);Assert.Null(vision.QualityProcessor.Pipeline);
        Assert.Equal(new[]{"ImagingPipeline.SetExposureParameters.BadMaxChangeFraction","VisionSystem.Init.SetExposureParametersFailed"},logs.Where(l=>l.Level==EngineLogLevel.Error).Select(l=>l.Name));Assert.DoesNotContain(logs,l=>l.Name=="VisionSystem.SetAutoExposureParams");
    }
    [Fact] public void InitialModesUseByteKeyOrderAndPassFalseEntriesToo()
    {
        using var resources=new BackupTestResources();using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,resources.Options());using var vision=new VisionSystem(robot){ModeEnableMask=0};
        var calls=new List<(int Mode,bool Enable)>();vision.InitialModeCallObservedForTest=(m,e)=>calls.Add((m,e));
        var modes=robot.Engine.VisionConfiguration.Members[Convert.ToHexString("InitialVisionModes"u8)];vision.ApplyInitialVisionModes(modes);
        Assert.Equal(new[]{"CheckingQuality","ComputingStatistics","DetectingBlinkAmount","DetectingFaces","DetectingGaze","DetectingLaserPoints","DetectingMarkers","DetectingMotion","DetectingOverheadEdges","DetectingPets","DetectingSmileAmount","EstimatingFacialExpression"},calls.Select(c=>((VisionMode)c.Mode).ToString()));
        Assert.Equal(new[]{true,true,false,true,false,true,true,true,true,true,false,false},calls.Select(c=>c.Enable));Assert.Equal(0x839E,vision.ModeEnableMask);
        // This exercises the real loop/body on its owner; full native Init prerequisite join remains M11-059.
    }
    [Fact] public void GetMemberNamesEmptyArraySkipsConfigButNullIsEmpty()
    {
        using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot){ModeEnableMask=4};var calls=new List<int>();vision.InitialModeCallObservedForTest=(m,_)=>calls.Add(m);
        vision.ApplyInitialVisionModes(new());Assert.Empty(calls);Assert.Equal(4,vision.ModeEnableMask);
        var diagnostics=new List<string>();StackDiagnostics.Reported+=diagnostics.Add;
        try{vision.ApplyInitialVisionModes(new(){Kind=FirmwareJsonKind.Array});Assert.Equal(4,vision.ModeEnableMask);Assert.Contains(diagnostics,s=>s.Contains("M1-034/M11-082")&&s.Contains("008E6D08"));}
        finally{StackDiagnostics.Reported-=diagnostics.Add;}
    }
    [Fact] public void MissingVisionConfigLogsFailureAndContinuesToLaterStartupWithoutClearingValue()
    {
        using var resources=new BackupTestResources();var options=resources.Options();string path=Path.Combine(options.ResourcesPath!,CozmoEngine.VisionConfigurationPath);
        var missing=new List<string>();SteppedBehavior.MissingReported+=missing.Add;CozmoEngine? engine=null;
        try
        {
            engine=new(new M4ControlTests.FakePort(),options,()=>0);var prior=engine.VisionConfiguration;Assert.Equal(FirmwareJsonKind.Object,prior.Kind);
            var logs=new List<EngineLogEntry>();engine.LogEntry+=logs.Add;File.Delete(path);EngineErrorState.ErrorFlagSet=false;engine.LoadVisionConfiguration();
            Assert.False(engine.VisionConfigurationReadSucceeded);Assert.Same(prior,engine.VisionConfiguration);Assert.Equal(FirmwareJsonKind.Object,prior.Kind);
            Assert.Equal(new[]{"DataPlatform.readAsJson","RobotDataLoader.VisionConfigJsonNotFound"},logs.Select(l=>l.Name));Assert.All(logs,e=>Assert.Equal(EngineLogLevel.Error,e.Level));
            Assert.EndsWith($"Failed to read [{options.ResourcesPath}/{CozmoEngine.VisionConfigurationPath}]",logs[0].Text);Assert.EndsWith("Vision Json config file config/engine/vision_config.json not found or failed to parse",logs[1].Text);Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.Contains(missing,s=>s.Contains("LoadGameRequestAndTrickConfigs"));Assert.Contains(missing,s=>s.Contains("M5-051"));Assert.True(engine.Experiments.Enabled);Assert.Equal(3,engine.Experiments.Definition.Experiments!.Count);
        }
        finally{engine?.Dispose();SteppedBehavior.MissingReported-=missing.Add;}
    }
}
