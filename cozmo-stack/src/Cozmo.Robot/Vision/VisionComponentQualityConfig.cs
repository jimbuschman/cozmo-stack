using System.Text;
using Cozmo.Transport;
namespace Cozmo.Robot.Vision;

public sealed partial class VisionSystem
{
    // fidelity: M11-059 — C1-C3 prefix only; full component/system Init and success remain unbuilt.
    internal static uint TimeBeforeQualityErrorMs=3000,RepeatedQualityErrorIntervalMs=5000;
    internal float PendingDropStatsSeconds=-1f;
    private bool ReadComponentQualityPrefix(FirmwareJsonValue root)
    {
        var old=(TimeBeforeQualityErrorMs,RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs);
        try
        {
            var quality=Member(root,"ImageQuality");
            if(!Read(quality,"TimeBeforeErrorMessage_ms",out var value))return false;
            TimeBeforeQualityErrorMs=Json.AsUInt(value!);
            if(!Read(quality,"RepeatedErrorMessageInverval_ms",out value))return false;
            RepeatedQualityErrorIntervalMs=Json.AsUInt(value!);
            if(!Read(quality,"InitialExposureTime_ms",out value))return false;
            CameraSettings.ConfiguredInitialExposureTimeMs=unchecked((ushort)Json.AsUInt(value!));
            PendingDropStatsSeconds=-1f;
            if(!Read(Member(root,"PerformanceLogging"),"DropStatsWindowLength_sec",out value))return false;
            PendingDropStatsSeconds=Json.AsFloat(value!);
            SteppedMissingInit();
            return true;
        }
        catch(Exception e) when(e is JsonLogicError or JsonRuntimeError)
        {
            (TimeBeforeQualityErrorMs,RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs)=old;
            throw; // M1-034 skips terminating config unit; defined missing-key returns keep earlier writes.
        }
        bool Read(FirmwareJsonValue? group,string key,out FirmwareJsonValue? value)
        {
            value=Member(group,key);
            if(value is not null&&value.Kind!=FirmwareJsonKind.Null)return true;
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionSystem.Init.MissingJsonParameter",key);
            EngineErrorState.StoreAndMaybeBreak();return false;
        }
        static FirmwareJsonValue? Member(FirmwareJsonValue? group,string key)
        {
            if(group is null||group.Kind==FirmwareJsonKind.Null)return null;
            if(group.Kind!=FirmwareJsonKind.Object)throw new JsonLogicError("Json::Value::find requires objectValue");
            return group.Members.GetValueOrDefault(Convert.ToHexString(Encoding.UTF8.GetBytes(key)));
        }
    }
    private static void SteppedMissingInit()=>Behavior.SteppedBehavior.ReportMissing("M11-059 component config C1-C3 only: Robot-owned VisionComponent/Init result and data-platform gate 0x005103BC, system setup/OMCV results 0x006B0658..0x006B141A and album branch before component success 0x00650E36..0x006510F8 remain unbuilt; no component success/count or system ready byte fabricated");
}
