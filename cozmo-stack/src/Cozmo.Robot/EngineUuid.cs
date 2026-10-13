using System.Buffers.Binary;
using Cozmo.Robot.Animation;
namespace Cozmo.Robot;
// fidelity: M3-055 — GetUUIDString 0x00831578..0x00831600, StringFromUUIDBytes 0x0083B814..0x0083B8BA.
internal static class EngineUuid
{
    internal static ulong ToUUIDWord(double scaled) => scaled >= 18446744073709551616.0 ? ulong.MaxValue : (ulong)scaled;
    internal static string GetUUIDString(EngineRandom? random = null)
    {
        random ??= new EngineRandom(0); // independent entropy generator for every call.
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, ToUUIDWord(random.GetNextDbl() * 18446744073709551616.0 + 0.0));
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..], ToUUIDWord(random.GetNextDbl() * 18446744073709551616.0 + 0.0));
        string hex = Convert.ToHexString(bytes);
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }
}
