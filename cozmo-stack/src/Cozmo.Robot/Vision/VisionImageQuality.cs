using Cozmo.Transport;
using Cozmo.Robot.Behavior;
namespace Cozmo.Robot.Vision;

public sealed partial class VisionSystem
{
    // fidelity: M11-080, M11-057
    internal readonly ImageQualityProcessor QualityProcessor=new();
    // Native rectangles are supplied by the detector producers, not reconstructed from world objects.
    // OV14 explicitly leaves which rectangles each detector appends unchecked.
    internal Func<IReadOnlyList<ImageQualityRectangle>>? QualityRectangleInput;
    private bool TryCheckFrameQuality(GrayImage image,out uint status,out ImageQualityProposal proposal)
    {
        status=0;proposal=default;
        if(QualityRectangleInput is null)
        {
            SteppedBehavior.ReportMissing("M11-080 detector rectangle producers (DetectMarkersWithCLAHE/DetectFaces/DetectPets -> 0x006B2D4C): which rectangles are appended is not checked; no empty/guessed ROI vector supplied");
            return false;
        }
        SteppedBehavior.ReportMissing("M11-080 mode-7 Profiler.Tic/Toc CheckingImageQuality 0x006B55F6/0x006B560E remain unbuilt timing producers; no Stopwatch timer substituted");
        return QualityProcessor.TryCheck(image,QualityRectangleInput(),_robot.CameraSettings.QualityState,ModeEnableMask,out status,out proposal);
    }
    private bool LoadExposureParameters(FirmwareJsonValue root)
    {
        var section=root.Members.GetValueOrDefault(Convert.ToHexString("ImageQuality"u8));
        if(section is null)return false;
        FirmwareJsonValue? Get(string key)=>section.Members.GetValueOrDefault(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(key)));
        foreach(string key in new[]{"MidValue","MaxChangeFraction","SubSample"})
        {
            if(Get(key) is {} v && v.Kind!=FirmwareJsonKind.Null)continue;
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionSystem.Init.MissingJsonParameter",key);EngineErrorState.StoreAndMaybeBreak();return false;
        }
        uint status=SetAutoExposureParams(Json.AsInt(Get("SubSample")!),unchecked((byte)Json.AsUInt(Get("MidValue")!)),QualityProcessor.Settings.Mid,Json.AsFloat(Get("MaxChangeFraction")!));
        if(status!=0){_robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionSystem.Init.SetExposureParametersFailed","");EngineErrorState.StoreAndMaybeBreak();return false;}
        return true;
    }
    private void LoadQualityConfiguration()
    {
        var engine=_robot.Engine;
        if(!engine.VisionConfigurationRead)
        {SteppedBehavior.ReportMissing("M11-080/M11-082 quality Init has no supplied DataPlatform config input; no native complete Init is claimed");return;}
        var root=engine.VisionConfiguration;
        if(!engine.VisionConfigurationReadSucceeded && root.Kind!=FirmwareJsonKind.Null)
        {SteppedBehavior.ReportMissing("M11-082 partial-object config contents are unreachable with shipped assets and are not built; loader value retained without interpretation");return;}
        var settings=QualityProcessor.Settings;
        var prior=(settings.TooBright,settings.TooDark,settings.MeterFromDetections,settings.Low,settings.Mid,settings.High,TimeBeforeQualityErrorMs,RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs,PendingDropStatsSeconds);
        try
        {
            bool loaded;
            {
                if(!ReadComponentQualityPrefix(root))return;
                loaded=QualityProcessor.Settings.Load(root,QualityProcessor.SourceLog!);
                if(loaded)loaded=LoadExposureParameters(root);
                if(loaded)SteppedBehavior.ReportMissing("M11-059/M11-082 remaining VisionSystem Init prerequisites before InitialVisionModes loop0x006B114C are unbuilt; ordered mode helper is not a fabricated complete Init");
            }

        }
        catch(Exception e) when(e is JsonLogicError or JsonRuntimeError)
        {
            (settings.TooBright,settings.TooDark,settings.MeterFromDetections,settings.Low,settings.Mid,settings.High,TimeBeforeQualityErrorMs,RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs,PendingDropStatsSeconds)=prior;
            StackDiagnostics.Write("M1-034/M11-080/M11-081 quality config JsonCpp terminating input at0x006B07DE..0x006B09F4: skip configuration, prior values retained; native complete Init remains M11-059");
        }
    }
}
