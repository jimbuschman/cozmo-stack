using System.Buffers.Binary;

namespace Cozmo.Protocol;

/// <summary>
/// Payload of a <see cref="ReliableMessageType.Ping"/> sub-message. 17 bytes on the wire (official PingPayload,
/// sizeof 24 minus 7 padding; confirmed by ReliableConnection::SendPing "movs r2,#0x11" in libcozmoEngine):
/// <code>
///  0 timeSent          f64 LE  sender's NetTimeStamp in ms (steady clock, ms since process start); echoed unchanged in a reply
///  8 numPingsSent      u32 LE  sender's running count of pings it has sent (incremented before each send, so first = 1)
/// 12 numPingsReceived  u32 LE  sender's running count of pings it has received
/// 16 isReply           u8      1 when this is an echo of a received ping
/// </code>
/// PyCozmo declares (time_sent_ms f64, counter u32, unknown u32) and misses the isReply byte; captured robot
/// replies show 17 bytes.
/// </summary>
public readonly record struct PingPayload(double TimeSentMs, uint NumPingsSent, uint NumPingsReceived, bool IsReply)
{
    public const int Length = 17;

    public byte[] ToBytes()
    {
        var b = new byte[Length];
        BinaryPrimitives.WriteDoubleLittleEndian(b, TimeSentMs);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8), NumPingsSent);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(12), NumPingsReceived);
        b[16] = (byte)(IsReply ? 1 : 0);
        return b;
    }

    public static bool TryParse(ReadOnlySpan<byte> src, out PingPayload p)
    {
        p = default;
        if (src.Length < Length) return false;
        p = new PingPayload(BinaryPrimitives.ReadDoubleLittleEndian(src),
            BinaryPrimitives.ReadUInt32LittleEndian(src[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(src[12..]),
            src[16] != 0);
        return true;
    }

    public override string ToString() => $"Ping(t={TimeSentMs:F3}ms sent={NumPingsSent} recv={NumPingsReceived} reply={IsReply})";
}
