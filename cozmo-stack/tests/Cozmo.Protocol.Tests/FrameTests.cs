using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class FrameTests
{
    // Real-hardware captures from PyCozmo (tests/test_frame.py). Values below are WIRE values.
    public const string EngineType7 =
        "b'COZ\\x03RE\\x01\\x07\\x9d\\n\\xa0\\n\\x8f\\x00\\x04\\x01\\x00\\x8f\\x04\\x1d\\x00\\x97\\x1a\\x00\\x15\\xb0\\xaa\\x9c\\xac\\xb2@\\xa8\\xba^\\xac\\xb2@\\x02\\xb4\\xa2\\xa0\\xb0\\xaa@\\xac\\xb2`\\xb0\\xaa\\x1b\\x04 \\x00\\x03\\x1f\\x80\\x1f\\x80\\t\\x00\\x00\\x00\\x00\\x00\\x1f\\x80\\x1f\\x80\\t\\x00\\x00\\x00\\x00\\x00\\x1f\\x80\\x1f\\x80\\t\\x00\\x00\\x00\\x00\\x00\\x00\\x04\\x16\\x00\\x11\\x1f\\x80\\x1f\\x80\\t\\x00\\x00\\x00\\x00\\x00\\x1f\\x80\\x1f\\x80\\t\\x00\\x00\\x00\\x00\\x00\\x00'";
    public const string RobotType9 =
        "b'COZ\\x03RE\\x01\\t\\x00\\x00\\x00\\x00\\x13\\x00\\x0b\\x11\\x00\\x00\\x00\\x00\\x00\\x00\\x00\\x00\\x00\\x01\\x00\\x00\\x00\\x00\\x00\\x00\\x00\\x00\\x05\\x0f\\x00\\xf1y\\x8bJO$\\x00\\x00\\x00\\x06\\x00\\x00\\x00\\xff\\x00'";

    [Fact]
    public void HeaderIs14BytesWithOfficialLayout()
    {
        var h = new ReliableHeader(ReliableMessageType.ConnectionRequest, 1, 1, 0);
        var b = new byte[ReliableHeader.Length]; h.Write(b);
        Assert.Equal(new byte[] { (byte)'C', (byte)'O', (byte)'Z', 3, (byte)'R', (byte)'E', 1, 1, 1, 0, 1, 0, 0, 0 }, b);
        Assert.True(ReliableHeader.TryParse(b, out var p, out _));
        Assert.Equal(h, p);
        Assert.True(p.IsReliable);
    }

    [Fact]
    public void PyCozmoEngineFixtureDecodesAndRoundTrips()
    {
        var raw = Hex.Parse(EngineType7);
        var f = FrameCodec.Decode(raw);
        Assert.Equal(ReliableMessageType.MultipleReliableMessages, f.Type);
        Assert.Equal(2717, f.SeqMin); Assert.Equal(2720, f.SeqMax); Assert.Equal(143, f.Ack);
        Assert.Equal(4, f.Messages.Count);
        Assert.All(f.Messages, m => Assert.Equal(ReliableMessageType.SingleReliableMessage, m.Type));
        Assert.Equal(new ushort[] { 2717, 2718, 2719, 2720 }, f.Messages.Select(m => m.Seq).ToArray());
        // first sub-message: 1 byte payload = tag 0x8f AudioSilence (animAudioSilence)
        Assert.Equal(RobotMessageId.AnimAudioSilence, (RobotMessageId)f.Messages[0].Payload[0]);
        Assert.Equal(RobotMessageId.AnimFaceImage, (RobotMessageId)f.Messages[1].Payload[0]);
        Assert.Equal(RobotMessageId.SetBackpackLightsMiddle, (RobotMessageId)f.Messages[2].Payload[0]);
        Assert.Equal(RobotMessageId.SetBackpackLightsTurnSignals, (RobotMessageId)f.Messages[3].Payload[0]);
        Assert.Equal(raw, FrameCodec.Encode(f));
    }

    [Fact]
    public void PyCozmoRobotFixtureDecodesPingEchoAndUnreliableState()
    {
        var raw = Hex.Parse(RobotType9);
        var f = FrameCodec.Decode(raw);
        Assert.Equal(ReliableMessageType.MultipleMixedMessages, f.Type);
        Assert.False(f.Header.IsReliable);
        Assert.Equal(19, f.Ack);
        Assert.Equal(2, f.Messages.Count);
        Assert.Equal(ReliableMessageType.Ping, f.Messages[0].Type);
        Assert.True(PingPayload.TryParse(f.Messages[0].Payload, out var ping));
        Assert.Equal(17, f.Messages[0].Payload.Length);
        // What the fixture's bytes decode to: time 0, counter 1, received 0, isReply byte 0. This checks the
        // decoder against captured bytes only; what the robot itself puts in a ping it sends or echoes is not
        // established by the package (M1-033, HARDWARE_ONLY).
        Assert.Equal(0.0, ping.TimeSentMs); Assert.Equal(1u, ping.NumPingsSent); Assert.Equal(0u, ping.NumPingsReceived); Assert.False(ping.IsReply);
        Assert.Equal(ReliableMessageType.SingleUnreliableMessage, f.Messages[1].Type);
        Assert.Equal(0, f.Messages[1].Seq);
        Assert.Equal(RobotMessageId.AnimState, (RobotMessageId)f.Messages[1].Payload[0]);
        Assert.Equal(raw, FrameCodec.Encode(f));
    }

    [Fact]
    public void SingleMessageFrameHasNoSubHeader()
    {
        var m = new SubMessage(ReliableMessageType.SingleReliableMessage, new byte[] { 0x25 }, seq: 7);
        var raw = FrameCodec.Encode(Frame.Single(m, ack: 3));
        Assert.Equal(15, raw.Length);
        Assert.Equal(4, raw[7]); Assert.Equal(7, raw[8]); Assert.Equal(7, raw[10]); Assert.Equal(3, raw[12]); Assert.Equal(0x25, raw[14]);
        var f = FrameCodec.Decode(raw);
        Assert.Single(f.Messages); Assert.Equal(7, f.Messages[0].Seq);
    }

    [Fact]
    public void MultipleFrameTypeSelectionFollowsOfficialRule()
    {
        var rel = new SubMessage(ReliableMessageType.SingleReliableMessage, new byte[] { 1 }, 5);
        var rel2 = new SubMessage(ReliableMessageType.SingleReliableMessage, new byte[] { 2 }, 6);
        var un = new SubMessage(ReliableMessageType.Ping, new PingPayload(1, 1, 0, false).ToBytes());
        Assert.Equal(ReliableMessageType.MultipleReliableMessages, Frame.Multiple(new[] { rel, rel2 }, 0).Type);
        Assert.Equal(ReliableMessageType.MultipleUnreliableMessages, Frame.Multiple(new[] { un, un }, 0).Type);
        var mixed = Frame.Multiple(new[] { rel, un, rel2 }, 9);
        Assert.Equal(ReliableMessageType.MultipleMixedMessages, mixed.Type);
        Assert.Equal(5, mixed.SeqMin); Assert.Equal(6, mixed.SeqMax);
        var back = FrameCodec.Decode(FrameCodec.Encode(mixed));
        Assert.Equal(new ushort[] { 5, 0, 6 }, back.Messages.Select(m => m.Seq).ToArray());
    }

    /// <summary>
    /// M1-003. The type sets are the inventory's, checked over every byte value:
    /// R5 IsValidMessageType 0x0083673C (valid = 1..11), R6 0x00836708 (always unreliable =
    /// {5,7,8,9,10,11}), R7 IsMutlipleMessagesType 0x00836722 (containers = {7,8,9}).
    /// </summary>
    [Fact]
    public void MessageTypeSetsAreTheEngines()
    {
        var alwaysUnreliable = new[] { 5, 7, 8, 9, 10, 11 };   // R6
        var containers = new[] { 7, 8, 9 };                    // R7
        for (int t = 0; t <= 255; t++)
        {
            Assert.Equal(t >= 1 && t <= 11, ReliableMessageTypes.IsValid((byte)t));                                   // R5
            Assert.Equal(alwaysUnreliable.Contains(t), ReliableMessageTypes.IsAlwaysUnreliable((ReliableMessageType)t));
            Assert.Equal(containers.Contains(t), ReliableMessageTypes.IsMultiple((ReliableMessageType)t));
        }
    }

    /// <summary>
    /// M1-004. R14 NextSequenceId 0x0083675A: 0xFFFE goes to 1, anything else +1, so ids run 1..65534;
    /// PreviousSequenceId 0x00836748: 1 goes to 0xFFFE, else -1. R15 IsSequenceIdInRange
    /// 0x0083676A..0x0083678C: when last >= first, first &lt;= id &lt;= last; when wrapped, id >= first or
    /// id &lt;= last.
    /// </summary>
    [Fact]
    public void SequenceIdsWrapLikeTheEngine()
    {
        // R14
        Assert.Equal(1, SequenceId.Next(0xFFFE));
        Assert.Equal(2, SequenceId.Next(1));
        Assert.Equal(0xFFFE, SequenceId.Next(0xFFFD));
        Assert.Equal(0xFFFE, SequenceId.Previous(1));
        Assert.Equal(1, SequenceId.Previous(2));
        // R15, last >= first: both ends inclusive, nothing outside
        Assert.True(SequenceId.InRange(10, 10, 10));
        Assert.True(SequenceId.InRange(10, 10, 12));
        Assert.True(SequenceId.InRange(12, 10, 12));
        Assert.False(SequenceId.InRange(9, 10, 12));
        Assert.False(SequenceId.InRange(13, 10, 12));
        // R15, wrapped (last < first): id >= first or id <= last
        Assert.True(SequenceId.InRange(65530, 65530, 5));
        Assert.True(SequenceId.InRange(0xFFFE, 65530, 5));
        Assert.True(SequenceId.InRange(2, 65530, 5));
        Assert.True(SequenceId.InRange(5, 65530, 5));
        Assert.False(SequenceId.InRange(6, 65530, 5));
        Assert.False(SequenceId.InRange(100, 65530, 5));
        Assert.False(SequenceId.InRange(65529, 65530, 5));
    }

    [Fact]
    public void BadPrefixIsRejected()
    {
        var raw = Hex.Parse(EngineType7); raw[0] = (byte)'X';
        Assert.False(FrameCodec.TryDecode(raw, out _, out var err));
        Assert.Contains("UDP prefix", err);
        raw = Hex.Parse(EngineType7); raw[6] = 2;
        Assert.False(FrameCodec.TryDecode(raw, out _, out err));
        Assert.Contains("reliable prefix", err);
    }

    /// <summary>
    /// M1-008. R30 SendPing 0x00835C00..0x00835C62 (movs r2,#0x11): 17 bytes, f64 time, u32 numPingsSent,
    /// u32 numPingsReceived, u8 isReply. The expected bytes are written out by hand from that layout, not
    /// produced by the encoder: 1234.5 is the IEEE-754 double 0x40934A0000000000, little-endian.
    /// </summary>
    [Fact]
    public void PingPayloadHasTheEnginesSeventeenByteLayout()
    {
        var expected = new byte[]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x4A, 0x93, 0x40,   // f64 time 1234.5
            0x07, 0x00, 0x00, 0x00,                           // u32 numPingsSent 7
            0x03, 0x00, 0x00, 0x00,                           // u32 numPingsReceived 3
            0x01,                                             // u8 isReply
        };
        Assert.Equal(expected, new PingPayload(1234.5, 7, 3, true).ToBytes());

        Assert.True(PingPayload.TryParse(expected, out var q));
        Assert.Equal(1234.5, q.TimeSentMs); Assert.Equal(7u, q.NumPingsSent); Assert.Equal(3u, q.NumPingsReceived); Assert.True(q.IsReply);
    }

    [Fact]
    public void HexDiffReportsFieldHints()
    {
        var a = Hex.Parse(EngineType7); var b = (byte[])a.Clone(); b[12] = 0x90;
        var d = Hex.Diff(a, b);
        Assert.Single(d); Assert.Contains("ack", d[0]);
    }
}
