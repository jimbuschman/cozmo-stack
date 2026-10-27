namespace Cozmo.Robot.Vision;

/// <summary>The engine's one-byte EngineErrorCodeMessage report.</summary>
public readonly record struct EngineErrorCodeReport(byte Code);

public sealed partial class VisionSystem
{
    // IQ1, ctor00650130..17C. LastReported is a RAW quality, not a Boolean.
    internal byte LastObservedQuality=1,LastReportedQuality;
    internal uint QualityStamp,QualityThreshold;
    public event Action<EngineErrorCodeReport>? ImageQualityError;
    private void ResetQualityReceiver()
    {LastObservedQuality=1;LastReportedQuality=0;QualityStamp=QualityThreshold=0;}

    // fidelity: M11-035, M11-080
    // Complete00655578..006556EA receiver; producer ROI availability is separate.
    internal uint UpdateImageQuality(uint timestamp,ImageQualityProposal proposal)
    {
        if(_robot.Engine.Robot?.IsPhysicalRobot!=true || proposal.Quality==0)return 0;
        if(_robot.CameraSettings.AutoExposureEnabled)
            _robot.CameraSettings.SetCameraSettings(proposal.Exposure,proposal.Gain); // BEFORE debounce; status ignored.
        byte quality=proposal.Quality;
        if(quality!=LastObservedQuality || QualityStamp==0)
        {
            QualityStamp=timestamp;LastReportedQuality=0;QualityThreshold=TimeBeforeQualityErrorMs;
            LastObservedQuality=quality;return 0;
        }
        uint elapsed=unchecked(timestamp-QualityStamp);
        if(LastReportedQuality!=1 && elapsed>QualityThreshold)
        {
            byte code=quality switch{1=>6,2=>4,3=>5,_=>7};
            string errorName=code switch{4=>"ImageQualityTooDark",5=>"ImageQualityTooBright",6=>"ImageQualityGood",_=>"Count"};
            string qualityName=quality switch{1=>"Good",2=>"TooDark",3=>"TooBright",_=>"(null)"};
            _robot.Engine.EmitSourceLog(EngineLogLevel.Event,null,"robot.vision.image_quality",errorName);
            _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"VisionComponent","VisionComponent.UpdateImageQuality.BroadcastingImageQualityChange",
                $"Seeing {qualityName} for more than {elapsed} > {QualityThreshold}ms, broadcasting {errorName}");
            var report=new EngineErrorCodeReport(code);
            if(_robot.Engine.Robot?.HasExternalInterface==true)
            {
                _robot.Engine.DeliverAnimationReportToApp(0x7C,new[]{code},_robot.Engine.CaptureAppEvent(ImageQualityError,report));
                _robot.Engine.AnimationMessages.Deliver(0x7C,true,report);
            }
            // Live receiver fields AFTER synchronous broadcast, then original
            // result fields through the common tail (IQ7), not an atomic snapshot.
            LastReportedQuality=LastObservedQuality;
            QualityStamp=timestamp;
            QualityThreshold=RepeatedQualityErrorIntervalMs;
        }
        LastObservedQuality=proposal.Quality;
        return 0; // dark/bright/camera validation rejection never promotes drain failure.
    }
}
