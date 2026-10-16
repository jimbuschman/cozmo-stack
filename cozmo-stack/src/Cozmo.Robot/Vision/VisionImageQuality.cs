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
    private void LoadQualityConfiguration()
    {
        string? resources=_robot.Engine.Options.ResourcesPath;
        if(string.IsNullOrEmpty(resources))
        {SteppedBehavior.ReportMissing("M11-080 quality Init config input absent; compiled static defaults retained, not labelled shipped vision_config values");return;}
        string path=Path.Combine(resources,"config","engine","vision_config.json");
        if(!File.Exists(path))
        {SteppedBehavior.ReportMissing("M11-080 quality config file missing; DataPlatform/RobotDataLoader failure path remains M11-059/M11-082, not fabricated as an Init Error");return;}
        try
        {
            if(!Json.TryParseFirst(File.ReadAllBytes(path),out var doc))
            {SteppedBehavior.ReportMissing("M11-080 quality config parse failure: DataPlatform reader failure path not built; prior statics retained");return;}
            bool loaded;
            using(doc)loaded=QualityProcessor.Settings.Load(doc.RootElement,QualityProcessor.SourceLog!);
            if(loaded)SteppedBehavior.ReportMissing("M11-081 quality Init SetAutoExposureParams 0x006B0A14 recipient is checked but not built (B-M11-11); six ImageQuality statics only loaded here");
        }
        catch(Exception e) when(e is JsonLogicError or JsonRuntimeError)
        {SteppedBehavior.ReportMissing("M1-034/M11-080 quality config JsonCpp terminating input at 0x006B07DE..0x006B0934: skip configuration; native complete Init is M11-059");}
    }
}
