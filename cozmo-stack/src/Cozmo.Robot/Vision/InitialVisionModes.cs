using Cozmo.Transport;
using Cozmo.Robot.Behavior;
namespace Cozmo.Robot.Vision;
public sealed partial class VisionSystem
{
    // M11-082: evaluate in source member order. Unknown/unbuilt mode input is unavailable,
    // preserving any earlier native-ordered stores; it is not a native Result1.
    internal void ApplyInitialVisionModes(FirmwareJsonValue modes)=>TryApplyInitialVisionModes(modes,()=>true);
    internal bool TryApplyInitialVisionModes(FirmwareJsonValue modes,Func<bool> current)
    {
        int previous=ModeEnableMask;
        try
        {
            if(modes.Kind is not(FirmwareJsonKind.Null or FirmwareJsonKind.Object))throw new JsonLogicError("getMemberNames value must be objectValue0x008E6D08");
            var checkedNames=new HashSet<string>(new[]{"CheckingQuality","ComputingStatistics","DetectingBlinkAmount","DetectingFaces","DetectingGaze","DetectingLaserPoints","DetectingMarkers","DetectingMotion","DetectingOverheadEdges","DetectingPets","DetectingSmileAmount","EstimatingFacialExpression"}.Select(n=>Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(n))),StringComparer.Ordinal);
            foreach(string key in modes.Members.Keys.Order(StringComparer.Ordinal))
            {
                if(!current())return false;
                if(!checkedNames.Contains(key))
                {SteppedBehavior.ReportMissing("M11-082 initial mode key outside checked input scope; source recipient unavailable at this member");return false;}
                var match=Enum.GetValues<VisionMode>().Where(m=>Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(m.ToString()))==key).ToArray();
                if(match.Length!=1){SteppedBehavior.ReportMissing("M11-082 initial vision mode lookup unavailable");return false;}
                var value=modes.Members[key];
                bool enabled=value.Kind switch{FirmwareJsonKind.Null=>false,FirmwareJsonKind.Boolean=>value.Boolean,FirmwareJsonKind.Int=>value.Signed!=0,FirmwareJsonKind.UInt=>value.Unsigned!=0,FirmwareJsonKind.Real=>value.Real!=0,_=>throw new JsonLogicError("asBool invalid kind")};
                InitialModeCallObservedForTest?.Invoke((int)match[0],enabled);
                if(!current())return false;
                EnableModeImmediately((int)match[0],enabled);
                if(!current())return false;
            }
            return true;
        }
        catch(JsonLogicError error)
        {
            if(current())ModeEnableMask=previous;
            StackDiagnostics.Write("M1-034/M11-082 InitialVisionModes getMemberNames/asBool throw: skip config unit, prior mode mask retained: "+error.Message);
            return false;
        }
    }
    internal Action<int,bool>? InitialModeCallObservedForTest;
}
