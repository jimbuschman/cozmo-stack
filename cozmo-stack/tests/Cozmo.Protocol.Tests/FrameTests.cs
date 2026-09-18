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
        // robot echo of PyCozmo's first ping: time 0, counter 1, received 0, isReply byte 0 (the firmware echoes the payload verbatim)
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

    [Fact]
    public void SequenceIdsWrapLikeTheEngine()
    {
        Assert.Equal(1, SequenceId.Next(65534));
        Assert.Equal(65534, SequenceId.Previous(1));
        Assert.True(SequenceId.InRange(2, 65530, 5));
        Assert.False(SequenceId.InRange(100, 65530, 5));
        Assert.True(SequenceId.InRange(10, 10, 10));
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

    [Fact]
    public void PingPayloadRoundTrip()
    {
        var p = new PingPayload(1234.5, 7, 3, true);
        var b = p.ToBytes();
        Assert.Equal(17, b.Length); Assert.Equal(1, b[16]);
        Assert.True(PingPayload.TryParse(b, out var q)); Assert.Equal(p, q);
    }

    [Fact]
    public void HexDiffReportsFieldHints()
    {
        var a = Hex.Parse(EngineType7); var b = (byte[])a.Clone(); b[12] = 0x90;
        var d = Hex.Diff(a, b);
        Assert.Single(d); Assert.Contains("ack", d[0]);
    }
}
