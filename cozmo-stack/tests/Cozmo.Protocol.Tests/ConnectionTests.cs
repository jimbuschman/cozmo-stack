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

    /// <summary>
    /// A robot message goes out unflushed (RobotConnectionManager::SendData 0x0062F5C2-D2), so it waits for
    /// IsPacketWorthSending 0x008362F4: strictly more than MaxTimeSinceLastSend = 32.3 ms since the last send
    /// (ConfigureReliableTransport 0x0062F0C8). Unacked, SendOptimalUnAckedPackets 0x008363B0 resends it once
    /// strictly more than TimeBetweenResends = 33.3 ms have passed since it was sent. Its header carries the
    /// last robot id we accepted.
    /// </summary>
    [Fact]
    public void OutgoingHeaderCarriesLastInAckedAndResendsAfter33ms()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        double requestSent = t.Connection!.LatestMessageSentMs;
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        t.OfflineOutbound.Clear();
        clk.Advance(10);
        t.Send(new GetManufacturingInfo());
        Assert.Empty(t.OfflineOutbound);                       // flush = 0: not worth sending yet
        clk.NowMs = requestSent + 32.3; t.OfflineTick();
        Assert.Empty(t.OfflineOutbound);                       // the rule is strictly greater
        clk.NowMs = requestSent + 32.4; t.OfflineTick();
        var f = Assert.Single(t.OfflineOutbound);
        Assert.Equal(ReliableMessageType.SingleReliableMessage, f.Type);
        Assert.Equal(2, f.SeqMin); Assert.Equal(1, f.Ack);
        double sent = t.Connection.LatestMessageSentMs;

        // not acked: no resend until strictly more than TimeBetweenResends (33.3 ms)
        clk.NowMs = sent + 33.3; t.OfflineTick();
        Assert.Single(t.OfflineOutbound);
        clk.NowMs = sent + 33.4; t.OfflineTick();
        Assert.Equal(2, t.OfflineOutbound.Count);
        Assert.Equal(1, t.Connection!.ResendFrames);
        // robot acks seq 2 in a header -> pending drained, no further resends
        t.ProcessIncoming(RobotFrame(0, 0, 2, new SubMessage(ReliableMessageType.Ping, new PingPayload(1, 1, 0, false).ToBytes())));
        Assert.Equal(0, t.Connection.PendingCount);
        clk.Advance(100); t.OfflineTick();
        // nothing but idle pings may follow once the message is acked
        Assert.Equal(2, t.OfflineOutbound.Count(f => f.Type != ReliableMessageType.Ping));
    }

    /// <summary>
    /// M1-017. R32 0x0083652E..0x00836590, with sSendSeparatePingMessages = 0: a ping is sent only when the
    /// queue is empty, lastSend > 0, now > lastSend + 33.3 and now >= lastPing + 33.3. R33
    /// ReliableConnection::Update 0x00836518..0x008365A8 runs that check first, then
    /// SendOptimalUnAckedPackets(1).
    ///
    /// The ConnectionRequest goes out at 1000 and is acked at 1000; the test ticks every 2 ms to 1200. So the
    /// first ping is at the first tick past 1000 + 33.3, which is 1034. Each ping is itself the last send, so
    /// the next is at the first tick past the previous + 33.3: 1068, 1102, 1136, 1170; 1204 is past the end.
    /// R30 SendPing 0x00835C00..0x00835C62 gives each one's payload: time = now, numPingsSent incremented
    /// first (1..5), numPingsReceived 0, isReply 0, sent unreliable (seq 0).
    /// </summary>
    [Fact]
    public void IdlePingsEvery33msOnlyWhenPendingIsEmpty()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        t.OfflineOutbound.Clear();
        for (int i = 0; i < 100; i++) { clk.Advance(2); t.OfflineTick(); }   // 200 ms idle
        var pings = t.OfflineOutbound.Where(f => f.Type == ReliableMessageType.Ping).ToList();
        Assert.Equal(t.OfflineOutbound.Count, pings.Count);                   // nothing but pings while idle
        var payloads = pings.Select(p =>
        {
            Assert.False(p.Header.IsReliable);
            Assert.True(PingPayload.TryParse(Assert.Single(p.Messages).Payload, out var pp));
            return pp;
        }).ToList();
        Assert.Equal(new[] { 1034.0, 1068.0, 1102.0, 1136.0, 1170.0 }, payloads.Select(p => p.TimeSentMs));
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5 }, payloads.Select(p => p.NumPingsSent));
        Assert.All(payloads, p => { Assert.Equal(0u, p.NumPingsReceived); Assert.False(p.IsReply); });
    }

    /// <summary>
    /// M1-008 on the send path. R30 SendPing 0x00835C00..0x00835C62: an unreliable message of type 0x0B
    /// (movs r1,#0xb), 17 bytes (movs r2,#0x11): f64 time = now, u32 numPingsSent incremented first, u32
    /// numPingsReceived, u8 isReply = 0 for a request; sent through ReliableTransport::SendMessage
    /// (0x00835C58), so it can go out on the same call. Alone in its frame it keeps its own type with no
    /// sub-header (R8 0x00835EB6); the header carries seq 0/0 (unreliable) and the last robot id accepted
    /// (R2). The expected datagram is written out by hand: 1034.0 is the double 0x4090280000000000.
    /// </summary>
    [Fact]
    public void AnIdlePingIsOneUnreliableType11FrameWithTheEnginesPayload()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        var raws = new List<byte[]>();
        t.FrameTrace += e => { if (e.Outbound) raws.Add(e.Raw); };
        while (raws.Count == 0 && clk.NowMs < 1100) { clk.Advance(2); t.OfflineTick(); }

        var expected = new byte[]
        {
            0x43, 0x4F, 0x5A, 0x03,                           // UDP prefix
            0x52, 0x45, 0x01, 0x0B,                           // RE 01, type 11
            0x00, 0x00, 0x00, 0x00,                           // seq 0 / 0: unreliable
            0x01, 0x00,                                       // ack: robot seq 1
            0x00, 0x00, 0x00, 0x00, 0x00, 0x28, 0x90, 0x40,   // f64 time 1034.0
            0x01, 0x00, 0x00, 0x00,                           // numPingsSent 1
            0x00, 0x00, 0x00, 0x00,                           // numPingsReceived 0
            0x00,                                             // isReply 0
        };
        Assert.Equal(expected, Assert.Single(raws));
    }

    /// <summary>
    /// M1-017, the other two conditions of R32 0x0083652E..0x00836590: no ping before the first frame
    /// (lastSend > 0), and none while anything is queued (the queue must be empty).
    /// </summary>
    [Fact]
    public void NoIdlePingBeforeTheFirstFrameOrWhileAnythingIsQueued()
    {
        // nothing has ever been sent: lastSend is 0, so there is no ping at all
        var clk = new ManualClock { NowMs = 1000 };
        int frames = 0;
        var fresh = new ReliableConnection(TransportOptions.EngineDefaults, clk, (_, _, _, _) => frames++);
        for (int i = 0; i < 100; i++) { clk.Advance(2); fresh.Update(); }
        Assert.Equal(0, frames);
        Assert.Equal(0u, fresh.NumPingsSent);

        // a reliable message that is never acked stays queued: resends only, no ping
        var (t, tclk, _) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        t.OfflineOutbound.Clear();
        tclk.Advance(10);
        t.Send(new GetManufacturingInfo());
        for (int i = 0; i < 100; i++) { tclk.Advance(2); t.OfflineTick(); }
        Assert.NotEmpty(t.OfflineOutbound);
        Assert.All(t.OfflineOutbound, f => Assert.Equal(ReliableMessageType.SingleReliableMessage, f.Type));
        Assert.Equal(0u, t.Connection!.NumPingsSent);
    }

    /// <summary>
    /// M1-003, R8 0x00835F36..0x00835F52: a packed frame is type 7 when it holds only reliable messages, 8
    /// only unreliable, 9 mixed; a single message keeps its own type with no sub-header (0x00835EB6,
    /// 0x00835EC4). R9 (0x00835F10..0x00835F24): each packed message is [type u8][size u16 LE][payload].
    /// R1 header, R2 ack = the last robot id accepted (1). The first message of each pair is queued
    /// unflushed and is not worth sending alone (R26); the second is flushed, so both go in one frame
    /// (R25, R27). The expected datagrams are written out by hand.
    /// </summary>
    [Theory]
    [InlineData(true, true, new byte[] { 0x07, 0x02, 0x00, 0x03, 0x00, 0x01, 0x00, 0x04, 0x02, 0x00, 0x01, 0x02, 0x04, 0x01, 0x00, 0x04 })]
    [InlineData(false, false, new byte[] { 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x05, 0x02, 0x00, 0x01, 0x02, 0x05, 0x01, 0x00, 0x04 })]
    [InlineData(true, false, new byte[] { 0x09, 0x02, 0x00, 0x02, 0x00, 0x01, 0x00, 0x04, 0x02, 0x00, 0x01, 0x02, 0x05, 0x01, 0x00, 0x04 })]
    public void PackedFrameTypeFollowsWhatItHolds(bool firstReliable, bool secondReliable, byte[] afterRePrefix)
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        var raws = new List<byte[]>();
        t.FrameTrace += e => { if (e.Outbound) raws.Add(e.Raw); };
        clk.Advance(10);
        t.SendData(new byte[] { 0x01, 0x02 }, reliable: firstReliable, flush: false);
        Assert.Empty(raws);
        t.SendData(new byte[] { 0x04 }, reliable: secondReliable, flush: true);
        var expected = new byte[] { 0x43, 0x4F, 0x5A, 0x03, 0x52, 0x45, 0x01 }.Concat(afterRePrefix).ToArray();
        Assert.Equal(expected, Assert.Single(raws));
    }

    /// <summary>M1-003, R8 single case (0x00835EB6, 0x00835EC4): one message alone keeps its type, no sub-header.</summary>
    [Fact]
    public void ASingleMessageKeepsItsOwnTypeWithNoSubHeader()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        var raws = new List<byte[]>();
        t.FrameTrace += e => { if (e.Outbound) raws.Add(e.Raw); };
        clk.Advance(10);
        t.SendData(new byte[] { 0x01, 0x02 }, reliable: true, flush: true);
        Assert.Equal(new byte[] { 0x43, 0x4F, 0x5A, 0x03, 0x52, 0x45, 0x01, 0x04, 0x02, 0x00, 0x02, 0x00, 0x01, 0x00, 0x01, 0x02 },
                     Assert.Single(raws));
    }

    /// <summary>
    /// M1-003, R12 HandleSubMessage table 0x0083744A: 4 and 5 deliver, 7..10 do nothing, 11 is ReceivePing.
    /// In an unreliable container every sub-message has seq 0 (R11), so none is dropped as out of order.
    /// </summary>
    [Fact]
    public void SubMessagesAreDispatchedByType()
    {
        var (t, clk, delivered) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        t.OfflineOutbound.Clear();
        var ping = new byte[17]; ping[8] = 1;                      // time 0, numPingsSent 1, received 0, isReply 0
        var frame = new Frame
        {
            Type = ReliableMessageType.MultipleUnreliableMessages, SeqMin = 0, SeqMax = 0, Ack = 1,
            Messages = new()
            {
                new SubMessage(ReliableMessageType.Ack, Array.Empty<byte>()),                        // 10: nothing
                new SubMessage(ReliableMessageType.Ping, ping),                                       // 11: ReceivePing
                new SubMessage(ReliableMessageType.SingleUnreliableMessage, new byte[] { 0x42 }),     // 5: deliver
                new SubMessage(ReliableMessageType.MultipleReliableMessages, Array.Empty<byte>()),    // 7: nothing
                new SubMessage(ReliableMessageType.MultipleUnreliableMessages, Array.Empty<byte>()),  // 8: nothing
                new SubMessage(ReliableMessageType.MultipleMixedMessages, Array.Empty<byte>()),       // 9: nothing
                new SubMessage(ReliableMessageType.SingleReliableMessage, new byte[] { 0x43 }),       // 4: deliver
            },
        };
        t.ProcessIncoming(FrameCodec.Encode(frame));
        Assert.Equal(new[] { new byte[] { 0x42 }, new byte[] { 0x43 } }, delivered);
        Assert.Equal(1u, t.Connection!.NumPingsReceived);
        Assert.Equal(LinkState.Connected, t.State);
        Assert.Empty(t.OfflineOutbound);
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

    /// <summary>
    /// M1-012 on the send path. R24 / B5 / B8: the reliable payload bound is sMaxNetMessageSize 1420
    /// (RobotConnectionManager::Init 0x0062EFC6 movw #0x58c) less the 4-byte prefix (no CRC) at
    /// 0x0083AF50..0x0083AF56, less the 10-byte header at 0x00836A3E = 1406. R21 (0x00836CD6, 0x00836D60):
    /// a message above that bound becomes type 6 parts; R22 0x00836D7A: each part carries 1404 bytes after
    /// its [index][count] prefix, and R14 gives each part its own seq. So 1406 bytes go out as one type-4
    /// frame, a 1420-byte datagram, and 1407 bytes are split in two, the first part again a 1420-byte
    /// datagram. B9: that is within the 1472-byte send buffer (0x0083A34E), so no datagram here reaches the
    /// buffer's overflow path. The messages are sent the way RobotConnectionManager::SendData sends every
    /// robot message: reliable, flush 0 (B28, 0x0062F5C2..0x0062F5CE); each goes out at once because a
    /// frame's worth of bytes makes it worth sending (R26).
    /// </summary>
    [Fact]
    public void TheFramePayloadBoundIs1406OnTheSendPath()
    {
        static (ReliableTransport t, List<byte[]> raws) Connected()
        {
            var (t, clk, _) = Offline();
            t.OfflineConnect();
            t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
            var raws = new List<byte[]>();
            t.FrameTrace += e => { if (e.Outbound) raws.Add(e.Raw); };
            clk.Advance(10);
            return (t, raws);
        }

        var fits = Enumerable.Range(0, 1406).Select(i => (byte)i).ToArray();
        var (a, rawsA) = Connected();
        a.SendData(fits, reliable: true, flush: false);
        var one = Assert.Single(rawsA);
        Assert.Equal(1420, one.Length);                                            // 4 + 10 + 1406
        Assert.Equal(new byte[] { 0x43, 0x4F, 0x5A, 0x03, 0x52, 0x45, 0x01, 0x04, 0x02, 0x00, 0x02, 0x00, 0x01, 0x00 }, one.Take(14));
        Assert.Equal(fits, one.Skip(14));

        var over = Enumerable.Range(0, 1407).Select(i => (byte)i).ToArray();
        var (b, rawsB) = Connected();
        b.SendData(over, reliable: true, flush: false);
        var first = Assert.Single(rawsB);
        Assert.Equal(1420, first.Length);                                          // 4 + 10 + 2 + 1404
        Assert.Equal(new byte[] { 0x43, 0x4F, 0x5A, 0x03, 0x52, 0x45, 0x01, 0x06, 0x02, 0x00, 0x02, 0x00, 0x01, 0x00, 0x01, 0x02 }, first.Take(16));
        Assert.Equal(over.Take(1404), first.Skip(16));
        var queued = b.Connection!.Pending;
        Assert.Equal(2, queued.Count);
        Assert.All(queued, p => Assert.Equal(ReliableMessageType.MultiPartMessage, p.Type));
        Assert.Equal(new ushort[] { 2, 3 }, queued.Select(p => p.Seq));
        Assert.Equal(new byte[] { 0x02, 0x02 }.Concat(over.Skip(1404)), queued[1].Payload);
    }

    [Fact]
    public void LargeOutgoingPayloadIsSplitIntoMultipartParts()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
        t.OfflineOutbound.Clear();
        // Each part carries 1404 bytes of the message after its two-byte {index, total} header (0x00836D7A):
        // 2500 bytes are one full part and a 1096-byte remainder.
        const int total = 2500;
        const int expected = 2;
        var payload = new byte[total];
        t.SendData(payload, reliable: true, flush: true);
        for (int i = 0; i < 40; i++) { clk.Advance(5); t.OfflineTick(); }
        var parts = t.OfflineOutbound.SelectMany(f => f.Messages).Where(m => m.Type == ReliableMessageType.MultiPartMessage).DistinctBy(m => m.Seq).OrderBy(m => m.Seq).ToList();
        Assert.Equal(expected, parts.Count);
        Assert.Equal(new byte[] { 1, (byte)expected }, parts[0].Payload.Take(2));
        Assert.Equal(new byte[] { (byte)expected, (byte)expected }, parts[^1].Payload.Take(2));
        Assert.Equal(1404, parts[0].Payload.Length - 2);
        Assert.Equal(1096, parts[1].Payload.Length - 2);
        Assert.Equal(total, parts.Sum(p => p.Payload.Length - 2));
    }
}
