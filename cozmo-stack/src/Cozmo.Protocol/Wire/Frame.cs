using System.Buffers.Binary;

namespace Cozmo.Protocol;

/// <summary>
/// The 14-byte on-the-wire frame header = UDP transport prefix (4) + Anki reliable header (10).
///
/// Official layout (libcozmoEngine RobotConnectionManager::Init sets the prefix to "COZ\x03", no CRC;
/// ReliableTransport::BuildHeader / AnkiReliablePacketHeader):
/// <code>
///  0  'C' 'O' 'Z' 0x03      UDPTransport header prefix (4 bytes, sDoesHeaderHaveCRC = false)
///  4  'R' 'E' 0x01          k_AnkiReliablePacketHeaderPrefix ("Reliable Transport Layer 1")
///  7  type       u8         ReliableMessageType
///  8  seqIdMin   u16 LE     first reliable sequence id in this frame, 0 if none
/// 10  seqIdMax   u16 LE     last reliable sequence id in this frame, 0 if none
/// 12  seqIdLastReceived u16 LE  the peer's last reliable id we have accepted in order (our ACK)
/// 14  body
/// </code>
/// PyCozmo describes the same 14 bytes as id "COZ\x03RE\x01" + type + first_seq + seq + ack, and stores seq
/// values minus one internally; this implementation always uses the raw wire values.
/// </summary>
public readonly record struct ReliableHeader(ReliableMessageType Type, ushort SeqMin, ushort SeqMax, ushort Ack)
{
    public static ReadOnlySpan<byte> UdpPrefix => "COZ"u8;
    public static ReadOnlySpan<byte> ReliablePrefix => "RE"u8;
    public const int UdpPrefixLength = 4;
    public const int ReliableLength = 10;
    public const int Length = UdpPrefixLength + ReliableLength; // 14

    /// <summary>Official AnkiReliablePacketHeader::IsReliable: either sequence field non-zero.</summary>
    public bool IsReliable => SeqMin != SequenceId.Invalid || SeqMax != SequenceId.Invalid;

    public void Write(Span<byte> dst)
    {
        if (dst.Length < Length) throw new ArgumentException("header needs 14 bytes", nameof(dst));
        UdpPrefix.CopyTo(dst);
        ReliablePrefix.CopyTo(dst[4..]);
        dst[7] = (byte)Type;
        BinaryPrimitives.WriteUInt16LittleEndian(dst[8..], SeqMin);
        BinaryPrimitives.WriteUInt16LittleEndian(dst[10..], SeqMax);
        BinaryPrimitives.WriteUInt16LittleEndian(dst[12..], Ack);
    }

    public static bool TryParse(ReadOnlySpan<byte> src, out ReliableHeader header, out string? error)
    {
        header = default; error = null;
        if (src.Length < Length) { error = $"frame too small ({src.Length} < {Length})"; return false; }
        if (!src[..4].SequenceEqual(UdpPrefix)) { error = "bad UDP prefix (expected COZ\\x03)"; return false; }
        if (!src[4..7].SequenceEqual(ReliablePrefix)) { error = "bad reliable prefix (expected RE\\x01)"; return false; }
        header = new ReliableHeader((ReliableMessageType)src[7],
            BinaryPrimitives.ReadUInt16LittleEndian(src[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(src[10..]),
            BinaryPrimitives.ReadUInt16LittleEndian(src[12..]));
        return true;
    }
}

/// <summary>One logical message inside a frame. Seq == 0 means unreliable (not sequenced, never resent).</summary>
public sealed class SubMessage
{
    public ReliableMessageType Type { get; }
    public ushort Seq { get; init; }
    public byte[] Payload { get; }
    public bool IsReliable => Seq != SequenceId.Invalid;

    public SubMessage(ReliableMessageType type, byte[] payload, ushort seq = SequenceId.Invalid)
    {
        Type = type; Payload = payload; Seq = seq;
    }

    /// <summary>Convenience: wrap a CLAD robot message (tag + body) as a data sub-message.</summary>
    public static SubMessage Data(byte[] cladMessage, bool reliable, ushort seq = SequenceId.Invalid) =>
        new(reliable ? ReliableMessageType.SingleReliableMessage : ReliableMessageType.SingleUnreliableMessage, cladMessage, seq);

    public override string ToString() => $"{Type}(seq={Seq},{Payload.Length}B)";
}

/// <summary>A decoded (or to-be-encoded) UDP frame.</summary>
public sealed class Frame
{
    public ReliableMessageType Type { get; init; }
    public ushort SeqMin { get; init; }
    public ushort SeqMax { get; init; }
    public ushort Ack { get; init; }
    public List<SubMessage> Messages { get; init; } = new();
    public ReliableHeader Header => new(Type, SeqMin, SeqMax, Ack);

    /// <summary>Frame carrying exactly one sub-message with the header type equal to the message type (official single-message path).</summary>
    public static Frame Single(SubMessage m, ushort ack) => new()
    {
        Type = m.Type, SeqMin = m.Seq, SeqMax = m.Seq, Ack = ack, Messages = new() { m }
    };

    /// <summary>
    /// Frame batching several sub-messages. Container type follows official ReliableConnection::SendUnAckedMessages:
    /// reliable+unreliable -> MultipleMixedMessages (9); only reliable -> MultipleReliableMessages (7); else (8).
    /// seqMin/seqMax = first/last reliable sequence id present (0/0 if none).
    /// </summary>
    // fidelity: M1-003
    public static Frame Multiple(IReadOnlyList<SubMessage> ms, ushort ack)
    {
        if (ms.Count == 0) throw new ArgumentException("empty frame", nameof(ms));
        bool rel = false, unrel = false; ushort min = 0, max = 0;
        foreach (var m in ms)
        {
            if (m.IsReliable) { if (!rel) { min = m.Seq; rel = true; } max = m.Seq; }
            else unrel = true;
        }
        var type = rel && unrel ? ReliableMessageType.MultipleMixedMessages
                 : rel ? ReliableMessageType.MultipleReliableMessages
                 : ReliableMessageType.MultipleUnreliableMessages;
        return new Frame { Type = type, SeqMin = min, SeqMax = max, Ack = ack, Messages = new(ms) };
    }

    public override string ToString() =>
        $"Frame {Type}(0x{(byte)Type:x2}) seq {SeqMin}..{SeqMax} ack {Ack} [{string.Join(", ", Messages)}]";
}

/// <summary>Encodes/decodes frames exactly as ReliableTransport::ReceiveData / SendUnAckedMessages / ReSendReliableMessage.</summary>
public static class FrameCodec
{
    public const int SubMessageOverhead = 3; // type u8 + size u16

    public static byte[] Encode(Frame f)
    {
        bool multiple = ReliableMessageTypes.IsMultiple(f.Type);
        if (!multiple && f.Messages.Count != 1)
            throw new InvalidOperationException("non-container frame must hold exactly one sub-message");
        int bodyLen = 0;
        foreach (var m in f.Messages) bodyLen += m.Payload.Length + (multiple ? SubMessageOverhead : 0);
        var buf = new byte[ReliableHeader.Length + bodyLen];
        f.Header.Write(buf);
        int o = ReliableHeader.Length;
        foreach (var m in f.Messages)
        {
            if (multiple)
            {
                buf[o++] = (byte)m.Type;
                BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(o), (ushort)m.Payload.Length); o += 2;
            }
            m.Payload.CopyTo(buf, o); o += m.Payload.Length;
        }
        return buf;
    }

    public static Frame Decode(ReadOnlySpan<byte> raw)
    {
        if (!TryDecode(raw, out var f, out var err)) throw new FormatException(err);
        return f!;
    }

    /// <summary>
    /// Decodes a frame. Sequence ids are assigned to reliable sub-messages exactly as the engine does:
    /// starting at seqMin, incremented for every sub-message whose type is not "always unreliable"
    /// (when the header itself is reliable); unreliable sub-messages get seq 0.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> raw, out Frame? frame, out string? error)
    {
        frame = null;
        if (!ReliableHeader.TryParse(raw, out var h, out error)) return false;
        var body = raw[ReliableHeader.Length..];
        var msgs = new List<SubMessage>();
        if (ReliableMessageTypes.IsMultiple(h.Type))
        {
            ushort seq = h.SeqMin; int o = 0;
            while (o < body.Length)
            {
                if (o + SubMessageOverhead > body.Length) { error = $"truncated sub-message header at body offset {o}"; return false; }
                byte t = body[o];
                if (!ReliableMessageTypes.IsValid(t)) { error = $"invalid sub-message type {t} at body offset {o}"; return false; }
                ushort size = BinaryPrimitives.ReadUInt16LittleEndian(body[(o + 1)..]);
                o += SubMessageOverhead;
                if (o + size > body.Length) { error = $"sub-message size {size} overruns body at offset {o}"; return false; }
                var type = (ReliableMessageType)t;
                bool subReliable = h.IsReliable && !ReliableMessageTypes.IsAlwaysUnreliable(type);
                msgs.Add(new SubMessage(type, body.Slice(o, size).ToArray(), subReliable ? seq : SequenceId.Invalid));
                if (subReliable) seq = SequenceId.Next(seq);
                o += size;
            }
        }
        else
        {
            if (!ReliableMessageTypes.IsValid((byte)h.Type)) { error = $"invalid frame type {(byte)h.Type}"; return false; }
            msgs.Add(new SubMessage(h.Type, body.ToArray(), h.IsReliable ? h.SeqMin : SequenceId.Invalid));
        }
        frame = new Frame { Type = h.Type, SeqMin = h.SeqMin, SeqMax = h.SeqMax, Ack = h.Ack, Messages = msgs };
        return true;
    }
}
