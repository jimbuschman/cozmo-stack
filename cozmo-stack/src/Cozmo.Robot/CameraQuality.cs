using Cozmo.Robot.Vision;
namespace Cozmo.Robot;

// fidelity: M3-021, M11-060
public sealed partial class CameraSettings
{
    public void EnableAutoExposure(bool enable)
    {
        lock (_gate) AutoExposureEnabled=enable;
        _robot.Lights.QueueVisionMode(7,enable);
    }
    public void SetAndDisableAutoExposure(ushort exposureMs,float gain) => SetAndDisableAutoExposure((int)exposureMs,gain);
    internal void SetAndDisableAutoExposure(int exposureMs,float gain)
    {
        SetCameraSettings(exposureMs,gain); // report uses the old +0x329 byte, including send failure
        EnableAutoExposure(false);
    }
    internal readonly record struct QualityCameraState(int Exposure,float Gain,int MinExposure,int MaxExposure,float MinGain,float MaxGain);
    internal QualityCameraState QualityState
    {
        get { lock(_gate)return new(CurrentExposureMs,CurrentGain,MinExposureMs,MaxExposureMs,MinGain,MaxGain); }
    }
    // OV15-17 B1-B5: no lock/barrier, exposure then gain copy, then flag clear; payload stays.
    internal void ApplyPendingCameraParams()
    {
        if(!_nextCameraParamsPending)return;
        CurrentExposureMs=_nextExposureMs;
        CurrentGain=_nextGain;
        _nextCameraParamsPending=false;
    }
}
