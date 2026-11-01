using System.Text.Json;
namespace Cozmo.Robot.Behavior;

internal static class ExecutableBehaviorNames
{
    internal static readonly string[] Names={"EnrollFace","FindFaces","FireTruckAlarm","FistBump","KnockOverCubes","LiftLoadTest","LookAroundInPlace","PounceOnMotion","RollBlock","StackBlocks","TrackLaser","Wait","Count"};
    internal static byte FromConfig(JsonElement config)
    {
        if(!config.TryGetProperty("executableBehaviorType",out var value)||value.ValueKind!=JsonValueKind.String)return 12;
        int id=Array.IndexOf(Names,value.GetString());
        if(id>=0)return (byte)id;
        StackDiagnostics.Write("ExecutableBehaviorTypeFromString: unknown name; using EnrollFace (0)");
        return 0;
    }
}
