namespace Cozmo.Protocol;

// fidelity: M8-013
/// <summary>
/// The two <c>MessageGameToEngine</c> union members <c>SelectionBSRunnableChooser</c> subscribes
/// (<c>HandleExecuteBehavior</c> 0x0060AA58..0x0060AC45, subscriptions 0x0060A898..0x0060A8B4 and 0x0060A91C..0x0060A938).
///
/// Wire layout (inventory M8-framework, Correction A4; research 20260929-R-BEH2-pre-extraction.md section 7): the union tag is a
/// <c>uint16</c> (0x0093 for <c>ExecuteBehaviorByExecutableType</c>, 0x0094 for <c>ExecuteBehaviorByID</c>; the native handler
/// tests the halfwords at 0x0060AA64 and 0x0060AB4E), then <c>uint8</c> selector at payload offset 0 and a signed little-endian
/// <c>int32 numRuns</c>. The C++ object pads <c>numRuns</c> to in-memory offset +4, and that padding is not serialised (native pack
/// 0x00738C38..0x00738C68 / 0x00738D7C..0x00738DAC, unpack 0x00738BA8..0x00738BCA / 0x00738CEC..0x00738D0E): the payload is five bytes, the
/// union member seven.
/// </summary>
public abstract class ExecuteBehaviorMessage
{
    /// <summary>The selector byte: the <c>ExecutableBehaviorType</c> (tag 0x0093) or the <c>BehaviorID</c> (tag 0x0094).</summary>
    public byte Selector { get; }

    /// <summary>The run budget; -1 is unlimited, zero and positive values are carried verbatim.</summary>
    public int NumRuns { get; }

    /// <summary>The payload size: one selector byte and four bytes of <c>numRuns</c>.</summary>
    public const int PayloadSize = 5;

    /// <summary>The union member's <c>uint16</c> tag.</summary>
    public abstract ushort Tag { get; }

    protected ExecuteBehaviorMessage(byte selector, int numRuns) { Selector = selector; NumRuns = numRuns; }

    /// <summary>The payload: <c>uint8 selector</c> then little-endian <c>int32 numRuns</c>, no padding.</summary>
    public byte[] PackBody() => new CladWriter().U8(Selector).I32(NumRuns).ToArray();

    /// <summary>The <c>MessageGameToEngine</c> union member: <c>uint16</c> tag then <see cref="PackBody"/> (7 bytes).</summary>
    public byte[] ToUnionBytes()
    {
        var w = new CladWriter().U16(Tag);
        w.Bytes(PackBody());
        return w.ToArray();
    }

    /// <summary>
    /// Reads the payload: <c>ReadBytes(1)</c> then <c>ReadBytes(4)</c> (native unpack 0x00738BA8..0x00738CEC) with no length check; <c>MessageGameToEngine::Unpack</c>
    /// 0x00751E8E forwards to the buffer unpack with no size compare (batch 3c verifier). A short buffer yields what <see cref="CladReader"/> yields on
    /// underflow (zero for the read that fails), and trailing bytes are ignored.
    /// </summary>
    protected static (byte Selector, int NumRuns) ReadBody(ReadOnlyMemory<byte> body)
    {
        var r = new CladReader(body);
        byte selector = r.U8();
        int numRuns = r.I32();
        return (selector, numRuns);
    }
}

/// <summary><c>ExecuteBehaviorByExecutableType</c>, union tag 0x0093 (147); byte 0 is the <c>ExecutableBehaviorType</c>.</summary>
public sealed class ExecuteBehaviorByExecutableTypeMessage : ExecuteBehaviorMessage
{
    public const ushort UnionTag = 0x0093;
    public override ushort Tag => UnionTag;
    public byte ExecutableBehaviorType => Selector;
    public ExecuteBehaviorByExecutableTypeMessage(byte executableBehaviorType, int numRuns) : base(executableBehaviorType, numRuns) { }
    public static ExecuteBehaviorByExecutableTypeMessage UnpackBody(ReadOnlyMemory<byte> body)
    {
        var (s, n) = ReadBody(body);
        return new ExecuteBehaviorByExecutableTypeMessage(s, n);
    }
}

/// <summary><c>ExecuteBehaviorByID</c>, union tag 0x0094 (148); byte 0 is the <c>BehaviorID</c>.</summary>
public sealed class ExecuteBehaviorByIDMessage : ExecuteBehaviorMessage
{
    public const ushort UnionTag = 0x0094;
    public override ushort Tag => UnionTag;
    public byte BehaviorID => Selector;
    public ExecuteBehaviorByIDMessage(byte behaviorId, int numRuns) : base(behaviorId, numRuns) { }
    public static ExecuteBehaviorByIDMessage UnpackBody(ReadOnlyMemory<byte> body)
    {
        var (s, n) = ReadBody(body);
        return new ExecuteBehaviorByIDMessage(s, n);
    }
}
