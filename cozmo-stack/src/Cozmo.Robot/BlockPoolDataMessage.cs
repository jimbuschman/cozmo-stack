using Cozmo.Protocol;
namespace Cozmo.Robot;
// fidelity: M4-035
public readonly record struct BlockPoolBlockData(uint FactoryId, int ObjectType);
// MessageEngineToGame 0x7E; external interface payload, not a robot message.
public sealed record BlockPoolDataMessage(bool Enabled, IReadOnlyList<BlockPoolBlockData> Blocks)
{
    public byte[] ToBytes()
    {
        var w = new CladWriter(); w.U8(0x7E); w.U8(Enabled ? (byte)1 : (byte)0); w.U8((byte)Blocks.Count);
        foreach (var block in Blocks) { w.U32(block.FactoryId); w.I32(block.ObjectType); }
        return w.ToArray();
    }
}
