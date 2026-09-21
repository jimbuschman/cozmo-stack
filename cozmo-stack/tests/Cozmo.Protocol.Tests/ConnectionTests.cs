using Cozmo.Protocol;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>Drives the reliability state machine with a manual clock and a scripted "robot".</summary>
public class ConnectionTests
{
    private static (ReliableTransport t, ManualClock clk, List<byte[]> delivered) Offline()
    {
        var clk = new ManualClock { NowMs = 1000 };
        var t = ReliableTransport.CreateOffline(TransportOptions.EngineDefaults, clk);
        var delivered = new List<byte[]>();
        t.DataReceived += delivered.Add;
        return (t, clk, delivered);
    }

    private static byte[] RobotFrame(ushort seqMin, ushort seqMax, ushort ack, params SubMessage[] msgs)
    {
        var f = msgs.Length == 1 && !ReliableMessageTypes.IsMultiple(msgs[0].Type)
            ? Frame.Single(msgs[0], ack)
            : new Frame { Type = ReliableMessageType.MultipleMixedMessages, SeqMin = seqMin, SeqMax = seqMax, Ack = ack, Messages = msgs.ToList() };
        return FrameCodec.Encode(f);
    }

    [Fact]
    public void ConnectionRequestIsReliableSeq1AndConnectsOnResponse()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        var req = Assert.Single(t.OfflineOutbound);
        Assert.Equal(ReliableMessageType.ConnectionRequest, req.Type);
        Assert.Equal(1, req.SeqMin); Assert.Equal(1, req.SeqMax); Assert.Equal(0, req.Ack);
        // PyCozmo's RESET frame is exactly this: COZ\x03RE\x01 01 0100 0100 0000
        Assert.Equal(Hex.Parse("434f5a03524501 01 0100 0100 0000"), FrameCodec.Encode(req));

        bool connected = false; t.Connected += () => connected = true;
        // robot answers: reliable ConnectionResponse seq 1, acking our seq 1
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        Assert.True(connected);
        Assert.Equal(LinkState.Connected, t.State);
        Assert.Equal(0, t.Connection!.PendingCount);        // our request was acked
        Assert.Equal(1, t.Connection.LastInAcked);           // we accepted robot seq 1
        Assert.Equal(2, t.Connection.NextInSeq);
        Assert.Equal(2, t.Connection.NextOutSeq);
    }

    [Fact]
    public void OutgoingHeaderCarriesLastInAckedAndResendsAfter33ms()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        t.OfflineOutbound.Clear();
        clk.Advance(10);
        t.SendData(new GetManufacturingInfo().ToBytes(), reliable: true, flush: true);
        var f = Assert.Single(t.OfflineOutbound);
        Assert.Equal(ReliableMessageType.SingleReliableMessage, f.Type);
        Assert.Equal(2, f.SeqMin); Assert.Equal(1, f.Ack);
        // not acked: no resend before TimeBetweenResends (33.3 ms)
        clk.Advance(20); t.OfflineTick();
        Assert.Single(t.OfflineOutbound);
        clk.Advance(20); t.OfflineTick();
        Assert.Equal(2, t.OfflineOutbound.Count);
        Assert.Equal(1, t.Connection!.ResendFrames);
        // robot acks seq 2 in a header -> pending drained, no further resends
        t.ProcessIncoming(RobotFrame(0, 0, 2, new SubMessage(ReliableMessageType.Ping, new PingPayload(1, 1, 0, false).ToBytes())));
        Assert.Equal(0, t.Connection.PendingCount);
        clk.Advance(100); t.OfflineTick();
        // nothing but idle pings may follow once the message is acked
        Assert.Equal(2, t.OfflineOutbound.Count(f => f.Type != ReliableMessageType.Ping));
    }

    [Fact]
    public void IdlePingsEvery33msOnlyWhenPendingIsEmpty()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        t.OfflineOutbound.Clear();
        for (int i = 0; i < 100; i++) { clk.Advance(2); t.OfflineTick(); }   // 200 ms idle
        var pings = t.OfflineOutbound.Where(f => f.Type == ReliableMessageType.Ping).ToList();
        Assert.InRange(pings.Count, 4, 6);                    // ~ every 33.3 ms (+ ~33 ms initial idle guard)
        Assert.All(pings, p => Assert.False(p.Header.IsReliable));
        Assert.True(PingPayload.TryParse(pings[0].Messages[0].Payload, out var pp));
        Assert.Equal(1u, pp.NumPingsSent); Assert.False(pp.IsReply);
    }

    [Fact]
    public void DuplicateAndOutOfOrderReliableMessagesAreDroppedButUnreliableInMixedFrameStillDelivered()
    {
        var (t, clk, delivered) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        var state = new RobotState { Timestamp = 5 }.ToBytes();
        var avail = new RobotAvailable { SerialNumberHead = 0x88 }.ToBytes();
        // reliable seq 2 (RobotAvailable) + unreliable state
        t.ProcessIncoming(RobotFrame(2, 2, 1, SubMessage.Data(avail, true, 2), SubMessage.Data(state, false)));
        Assert.Equal(2, delivered.Count);
        // the same frame again (robot resend): reliable dropped, unreliable state still delivered (official MultipleMixed rule)
        t.ProcessIncoming(RobotFrame(2, 2, 1, SubMessage.Data(avail, true, 2), SubMessage.Data(state, false)));
        Assert.Equal(3, delivered.Count);
        Assert.Equal(1, t.Connection!.DuplicateReliableDropped);
        // a pure reliable frame out of order (seq 5 when 3 expected) is ignored entirely
        t.ProcessIncoming(RobotFrame(5, 5, 1, SubMessage.Data(avail, true, 5)));
        Assert.Equal(3, delivered.Count);
        Assert.Equal(3, t.Connection.NextInSeq);
        Assert.Equal(2, t.Connection.LastInAcked);
    }

    [Fact]
    public void MultipartMessagesAreReassembled()
    {
        var (t, clk, delivered) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        var big = Enumerable.Range(0, 300).Select(i => (byte)i).ToArray(); big[0] = (byte)RobotMessageId.Image;
        var p1 = new byte[] { 1, 2 }.Concat(big.Take(150)).ToArray();
        var p2 = new byte[] { 2, 2 }.Concat(big.Skip(150)).ToArray();
        t.ProcessIncoming(RobotFrame(2, 2, 1, new SubMessage(ReliableMessageType.MultiPartMessage, p1, 2)));
        Assert.Empty(delivered);
        t.ProcessIncoming(RobotFrame(3, 3, 1, new SubMessage(ReliableMessageType.MultiPartMessage, p2, 3)));
        Assert.Equal(big, Assert.Single(delivered));
    }

    [Fact]
    public void ConnectionTimesOutAfter5sWithoutDatagrams()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        clk.Advance(4999); Assert.True(t.OfflineTick());
        clk.Advance(2); Assert.False(t.OfflineTick());
    }

    [Fact]
    public void LargeOutgoingPayloadIsSplitIntoMultipartParts()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        t.OfflineOutbound.Clear();
        // The split follows the engine's frame bound rather than a number written down here: each part
        // carries a two-byte {index, total} header, so the count falls out of MaxFramePayloadBytes.
        const int total = 2500;
        int perPart = new TransportOptions().MaxFramePayloadBytes - 2;
        int expected = (total + perPart - 1) / perPart;
        Assert.Equal(2, expected);                                  // 1406 a frame, where 1037 gave three
        var payload = new byte[total];
        t.SendData(payload, reliable: true, flush: true);
        for (int i = 0; i < 40; i++) { clk.Advance(5); t.OfflineTick(); }
        var parts = t.OfflineOutbound.SelectMany(f => f.Messages).Where(m => m.Type == ReliableMessageType.MultiPartMessage).DistinctBy(m => m.Seq).OrderBy(m => m.Seq).ToList();
        Assert.Equal(expected, parts.Count);
        Assert.Equal(new byte[] { 1, (byte)expected }, parts[0].Payload.Take(2));
        Assert.Equal(new byte[] { (byte)expected, (byte)expected }, parts[^1].Payload.Take(2));
        Assert.Equal(total, parts.Sum(p => p.Payload.Length - 2));
    }
}
