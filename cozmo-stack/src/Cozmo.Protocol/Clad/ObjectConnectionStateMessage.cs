namespace Cozmo.Protocol;

/// <summary>The E2G ObjectConnectionState body; the ID is a world ObjectID, not a radio slot.</summary>
// fidelity: M11-085
public sealed record ObjectConnectionStateMessage(uint ObjectID, uint FactoryID, ObjectType ObjectType, byte ConnectedByte)
{
    public const ushort UnionTag = 0x000D; // constructor 0x00721EDC
    // Pack 0x007AFEC0..0x007AFF02; Write<bool> 0x0083C0CC copies its byte unchanged.
    public byte[] PackBody() => new CladWriter().U32(ObjectID).U32(FactoryID).I32((int)ObjectType).U8(ConnectedByte).ToArray();
    public byte[] ToUnionBytes() => new CladWriter().U16(UnionTag).Bytes(PackBody()).ToArray();
}
