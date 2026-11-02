using System.Text;
using Cozmo.Transport;
namespace Cozmo.Robot.Vision;

public sealed partial class VisionSystem
{
    // fidelity: M11-059 — C1-C3 component prefix before the system Init recipient.
    internal static uint TimeBeforeQualityErrorMs=3000,RepeatedQualityErrorIntervalMs=5000;
    internal float PendingDropStatsSeconds=-1f;
    private bool ReadComponentQualityPrefix(FirmwareJsonValue root,Func<bool>? current=null)
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

            return true;
        }
        catch(Exception e) when(e is JsonLogicError or JsonRuntimeError)
        {
            if(current is null||current())(TimeBeforeQualityErrorMs,RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs)=old;
            throw; // M1-034 skips terminating config unit; defined missing-key returns keep earlier writes.
        }
        bool Read(FirmwareJsonValue? group,string key,out FirmwareJsonValue? value)
        {
            value=Member(group,key);
            if(value is not null&&value.Kind!=FirmwareJsonKind.Null)return true;
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionSystem.Init.MissingJsonParameter",key);
            if(current is null||current())EngineErrorState.StoreAndMaybeBreak();return false;
        }
        static FirmwareJsonValue? Member(FirmwareJsonValue? group,string key)
        {
            if(group is null||group.Kind==FirmwareJsonKind.Null)return null;
            if(group.Kind!=FirmwareJsonKind.Object)throw new JsonLogicError("Json::Value::find requires objectValue");
            return group.Members.GetValueOrDefault(Convert.ToHexString(Encoding.UTF8.GetBytes(key)));
        }
    }
}
