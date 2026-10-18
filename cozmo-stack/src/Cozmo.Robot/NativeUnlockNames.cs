using System.Text;
namespace Cozmo.Robot;

// fidelity: M15-044 — exact checked 81-entry name/value table; lookup never parses numeric/flag syntax.
internal static class NativeUnlockNames
{
    private static readonly IReadOnlyDictionary<string,uint> Names=Enum.GetValues<UnityUnlockId>()
        .ToDictionary(id=>Convert.ToHexString(Encoding.UTF8.GetBytes(Enum.GetName(id)!)),id=>(uint)id,StringComparer.Ordinal);
    [ThreadStatic] internal static Action<byte[]>? StderrForTest;
    internal static uint FromBytes(byte[] name)
    {
        if(Names.TryGetValue(Convert.ToHexString(name),out uint id))return id;
        byte[] line="error: string '"u8.ToArray().Concat(name).Concat("' is not a valid UnlockId value\n"u8.ToArray()).ToArray();
        if(StderrForTest is {} observe)observe(line);
        else { using var stderr=Console.OpenStandardError();stderr.Write(line);stderr.Flush(); }
        return 0;
    }
    internal static string NameOf(uint id)=>Enum.GetName((UnityUnlockId)id)??"(null)";
}
