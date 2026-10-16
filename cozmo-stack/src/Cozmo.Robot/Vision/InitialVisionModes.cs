using Cozmo.Transport;
using Cozmo.Robot.Behavior;
namespace Cozmo.Robot.Vision;
public sealed partial class VisionSystem
{
    // fidelity: M11-082 — checked loop body, native full Init prerequisite join remains M11-059.
    internal void ApplyInitialVisionModes(FirmwareJsonValue modes)
    {
        int previous=ModeEnableMask;
        try
        {
            if(modes.Kind is not(FirmwareJsonKind.Null or FirmwareJsonKind.Object))throw new JsonLogicError("getMemberNames value must be objectValue0x008E6D08");
            var checkedNames=new HashSet<string>(new[]{"CheckingQuality","ComputingStatistics","DetectingBlinkAmount","DetectingFaces","DetectingGaze","DetectingLaserPoints","DetectingMarkers","DetectingMotion","DetectingOverheadEdges","DetectingPets","DetectingSmileAmount","EstimatingFacialExpression"}.Select(n=>Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(n))),StringComparer.Ordinal);
            if(modes.Members.Keys.Any(k=>!checkedNames.Contains(k)))
            {SteppedBehavior.ReportMissing("M11-082 initial mode key outside checked twelve-key input scope; full GetModeFromString/unknown Warning recipient needs expansion; config unit stopped");return;}
            // Uppercase hex ordering is exactly unsigned-byte lexical order, including prefix length/NUL.
            foreach(string key in modes.Members.Keys.Order(StringComparer.Ordinal))
            {
                var match=Enum.GetValues<VisionMode>().Where(m=>Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(m.ToString()))==key).ToArray();
                if(match.Length!=1){SteppedBehavior.ReportMissing("M11-082 unknown initial vision mode Warning0x006B11A4..0x006B11E8 exact text recipient not expanded; no invented warning");continue;}
                var value=modes.Members[key];
                bool enabled=value.Kind switch{FirmwareJsonKind.Null=>false,FirmwareJsonKind.Boolean=>value.Boolean,FirmwareJsonKind.Int=>value.Signed!=0,FirmwareJsonKind.UInt=>value.Unsigned!=0,FirmwareJsonKind.Real=>value.Real!=0,_=>throw new JsonLogicError("asBool invalid kind")};
                InitialModeCallObservedForTest?.Invoke((int)match[0],enabled);
                EnableModeImmediately((int)match[0],enabled);
            }
        }
        catch(JsonLogicError)
        {
            ModeEnableMask=previous;
            StackDiagnostics.Write("M1-034/M11-082 InitialVisionModes getMemberNames throw0x008E6D08 / asBool throw: skip this config unit, prior mode mask retained; CZString null-RHS throw0x008E7EB6 is unrepresentable in managed byte-key storage");
        }
    }
    internal Action<int,bool>? InitialModeCallObservedForTest;
}
