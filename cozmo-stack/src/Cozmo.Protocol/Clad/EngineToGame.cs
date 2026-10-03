namespace Cozmo.Protocol;

/// <summary>
/// The engine-to-game <c>MoodState</c> message, the one <c>MoodManager::SendEmotionsToGame</c> 0x0067B724 broadcasts through
/// <c>Robot::Broadcast(MessageEngineToGame)</c> (0x0067B77E..0x0067B79C). It has one CLAD field, a variable-length vector of
/// <c>float_32</c> emotion values, so on the wire it is a counted float vector, not nine named fields.
///
/// Packed body (<see cref="PackBody"/>): the vector's length truncated to one byte (the pack helper computes <c>(end - begin) / 4</c>
/// and writes one byte, 0x00704F76..0x00704F9E), then every element as four raw IEEE-754 bytes
/// (<c>SafeMessageBuffer::WriteBytes(..., 4)</c> per element, 0x0070FB56..0x0070FB84). With the engine's nine emotions that is
/// 1 + 9 * 4 = 37 bytes. Unpacking (0x00704FD6..0x00704FFE, 0x0070FB86..0x0070FC04) reads the one-byte count, then exactly that many
/// four-byte floats.
///
/// Inside <c>MessageEngineToGame</c> the union writes a preceding <c>uint_16</c> tag (<c>Set_MoodState</c> stores 0x0061, 0x00725FFC..0x00726032;
/// the packer writes the two-byte tag before dispatching tag 0x61 to the MoodState vector packer, 0x0072928C..0x00729298 and
/// 0x0072974C..0x00729756), so the union body is 39 bytes. There are no timestamps, names or doubles in this message. The outer transport
/// framing is not part of this type.
/// </summary>
// fidelity: M7-012
public sealed class MoodStateMessage
{
    /// <summary>The <c>MessageEngineToGame</c> union tag for <c>MoodState</c>: <c>uint_16</c> 0x0061 (0x0072928C..0x00729298).</summary>
    public const ushort UnionTag = 0x0061;

    /// <summary>The emotion values in order (the engine's nine, in <c>EmotionType</c> order, for this producer).</summary>
    public IReadOnlyList<float> Emotions { get; }

    public MoodStateMessage(IEnumerable<float> emotions) => Emotions = emotions.ToArray();

    /// <summary>The <c>MoodState</c> payload: <c>uint8 count</c> then <c>count</c> little-endian float32.</summary>
    public byte[] PackBody()
    {
        var w = new CladWriter().U8(unchecked((byte)Emotions.Count));
        foreach (var e in Emotions) w.F32(e);
        return w.ToArray();
    }

    /// <summary>The <c>MessageEngineToGame</c> union member: <c>uint16 0x0061</c> then <see cref="PackBody"/>.</summary>
    public byte[] ToUnionBytes()
    {
        var w = new CladWriter().U16(UnionTag);
        w.Bytes(PackBody());
        return w.ToArray();
    }

    /// <summary>The inverse of <see cref="PackBody"/>: the one-byte count, then that many four-byte floats.</summary>
    public static MoodStateMessage UnpackBody(ReadOnlyMemory<byte> body)
    {
        var r = new CladReader(body);
        int count = r.U8();
        var values = new List<float>(count);
        for (int i = 0; i < count; i++) values.Add(r.F32());
        if (r.LastReadFailed) throw new FormatException("MoodState: the buffer ends before the counted floats do");
        return new MoodStateMessage(values);
    }
}
