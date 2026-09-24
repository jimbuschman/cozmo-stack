using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Cozmo.Protocol;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The M1 live-transport repair: the robot-message send policy, the receive order, the delivery gate,
/// disconnect and dispose, the single transport thread and receive errors. Each test names the engine
/// sites its oracle comes from. Tests marked PRIMARY-SOURCE ORACLE assert behaviour read from
/// libcozmoEngine.so; REGRESSION ONLY tests guard this stack's own behaviour and prove nothing about the
/// engine.
/// </summary>
public class TransportRepairTests
{
    private static readonly byte[] Data = new GetManufacturingInfo().ToBytes();

    private static (ReliableTransport t, ManualClock clk, List<byte[]> delivered) Offline(TransportOptions? o = null)
    {
        var clk = new ManualClock { NowMs = 1000 };
        var t = ReliableTransport.CreateOffline(o ?? TransportOptions.EngineDefaults, clk);
        var delivered = new List<byte[]>();
        t.DataReceived += delivered.Add;
        return (t, clk, delivered);
    }

    /// <summary>ConnectionRequest at t = 1000 (sent at once), then the robot's ConnectionResponse, seq 1, acking it.</summary>
    private static void Connect(ReliableTransport t)
    {
        t.OfflineConnect();
        t.OfflineAcceptConnection();
        Assert.Equal(LinkState.Connected, t.State);
    }

    /// <summary>COZ\x03 + a raw reliable header + body, built by hand so malformed bodies can be expressed.</summary>
    private static byte[] Raw(ReliableMessageType type, ushort seqMin, ushort seqMax, ushort ack, byte[] body)
    {
        var raw = new byte[ReliableHeader.Length + body.Length];
        new ReliableHeader(type, seqMin, seqMax, ack).Write(raw);
        body.CopyTo(raw, ReliableHeader.Length);
        return raw;
    }

    /// <summary>A container body: [type u8][size u16][payload] per entry; size is the payload length unless given.</summary>
    private static byte[] Body(params (byte type, byte[] payload, int? size)[] subs)
    {
        var o = new List<byte>();
        foreach (var (type, payload, size) in subs)
        {
            o.Add(type);
            var s = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(s, (ushort)(size ?? payload.Length)); o.AddRange(s);
            o.AddRange(payload);
        }
        return o.ToArray();
    }

    private static (byte, byte[], int?) Sub(ReliableMessageType t, byte[] p, int? size = null) => ((byte)t, p, size);

    private static byte[] Coz(params byte[] rest) => new byte[] { (byte)'C', (byte)'O', (byte)'Z', 3 }.Concat(rest).ToArray();

    // ================================================================ M1-a

    /// <summary>
    /// T-a1 — PRIMARY-SOURCE ORACLE. RobotConnectionManager::SendData 0x0062F5C2-D2 passes flush = 0 whatever
    /// the caller wanted, so a robot message sent "with flush" right after another application send gets a
    /// reliable id and waits for IsPacketWorthSending 0x008362F4 like any other: its flush short-circuit
    /// (0x00836368-6E) never fires, and the frame goes only once now &gt; lastSent + 32.3 ms
    /// (ConfigureReliableTransport 0x0062F0C8).
    /// </summary>
    [Fact]
    public void T_a1_AFlushRequestOnTheRobotMessagePathStillWaitsForTheUnflushedGate()
    {
        var (t, clk, _) = Offline();
        Connect(t);
        clk.NowMs = 1040;                                   // > 1000 + 32.3: the first send goes at once
        t.Send(new SyncTime(0));
        Assert.Equal(2, t.OfflineOutbound.Count);
        double lastSent = t.Connection!.LatestMessageSentMs;
        Assert.Equal(1040, lastSent);

        clk.NowMs = 1045;
        t.Send(new GetManufacturingInfo(), flush: true);
        var queued = t.Connection.Pending[^1];
        Assert.Equal(3, queued.Seq);                        // a reliable id
        Assert.False(queued.FlushPacket);                   // queued unflushed
        Assert.Equal(2, t.OfflineOutbound.Count);           // and not sent

        clk.NowMs = lastSent + 32.3; t.OfflineTick();       // not yet: the rule is strictly greater
        Assert.Equal(2, t.OfflineOutbound.Count);
        clk.NowMs = lastSent + 32.4; t.OfflineTick();
        var f = t.OfflineOutbound[^1];
        Assert.Contains(f.Messages, m => m.Seq == 3 && m.Payload.SequenceEqual(Data));
    }

    /// <summary>
    /// T-a2 — PRIMARY-SOURCE ORACLE. RobotConnectionManager::SendData always passes reliable = 1 (0x0062F5CE),
    /// so a robot message asked to go unreliably is still a reliable single message with a sequence id.
    /// ReliableTransport.SendData itself keeps its parameters.
    /// </summary>
    [Fact]
    public void T_a2_AnUnreliableRequestOnTheRobotMessagePathIsStillSentReliably()
    {
        var (t, clk, _) = Offline();
        Connect(t);
        clk.NowMs = 1040;
        t.Send(new GetManufacturingInfo(), reliable: false);
        var f = t.OfflineOutbound[^1];
        Assert.Equal(ReliableMessageType.SingleReliableMessage, f.Type);
        Assert.Equal(2, f.SeqMin);
        Assert.Equal(2, Assert.Single(f.Messages).Seq);

        clk.NowMs = 1080;
        t.SendData(Data, reliable: false, flush: true);     // the library call is not the robot-message path
        var u = Assert.Single(t.OfflineOutbound[^1].Messages, m => m.Payload.SequenceEqual(Data) && m.Seq == SequenceId.Invalid);
        Assert.Equal(ReliableMessageType.SingleUnreliableMessage, u.Type);
    }

    /// <summary>
    /// T-a3 — PRIMARY-SOURCE ORACLE. The transport's own messages keep their flush flags: ConnectionRequest
    /// is queued reliable and flushed (ReliableTransport::Connect 0x00837104-1C), DisconnectRequest is sent
    /// reliable and flushed (0x0083801C), Ping unreliable and flushed (SendPing 0x00835C40-58).
    /// </summary>
    [Fact]
    public void T_a3_ConnectionRequestDisconnectRequestAndPingKeepTheirFlush()
    {
        // ConnectionRequest: still pending right after it is sent, so its flags can be read
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        var req = Assert.Single(t.Connection!.Pending);
        Assert.Equal(ReliableMessageType.ConnectionRequest, req.Type);
        Assert.True(req.FlushPacket); Assert.True(req.IsReliable);

        // Ping: sent 0.5 ms after the request, the packet separation interval holds it in the queue
        clk.NowMs = 1000.5;
        t.Connection.SendPing();
        var ping = t.Connection.Pending[^1];
        Assert.Equal(ReliableMessageType.Ping, ping.Type);
        Assert.True(ping.FlushPacket); Assert.False(ping.IsReliable);

        // DisconnectRequest: 5 ms after the last send, far inside 32.3 ms, it still goes out on its one
        // attempt, which only a flushed message can do
        var (d, dclk, _) = Offline();
        Connect(d);
        dclk.NowMs = 1005;
        d.Disconnect();
        var dr = d.OfflineOutbound[^1];
        Assert.Contains(dr.Messages, m => m.Type == ReliableMessageType.DisconnectRequest && m.IsReliable);
    }

    // ================================================================ M1-b

    /// <summary>
    /// T-b1 — PRIMARY-SOURCE ORACLE. The connected-state gate 0x0062F724-2A is applied to each arrived
    /// message in FIFO order (0x0062E25C / 0x0062F3A4), and state 2 is set when the ConnectionResponse is
    /// reached (0x0062F954-5A). In one frame [data, ConnectionResponse, data], only the second is delivered.
    /// </summary>
    [Fact]
    public void T_b1_DataBeforeTheConnectionResponseInTheSameFrameIsDropped()
    {
        var (t, _, delivered) = Offline();
        t.OfflineConnect();
        var first = new RobotAvailable { SerialNumberHead = 1 }.ToBytes();
        var second = new RobotAvailable { SerialNumberHead = 2 }.ToBytes();
        t.ProcessIncoming(Raw(ReliableMessageType.MultipleReliableMessages, 1, 3, 1, Body(
            Sub(ReliableMessageType.SingleReliableMessage, first),
            Sub(ReliableMessageType.ConnectionResponse, Array.Empty<byte>()),
            Sub(ReliableMessageType.SingleReliableMessage, second))));
        Assert.Equal(LinkState.Connected, t.State);
        Assert.Equal(second, Assert.Single(delivered));
    }

    /// <summary>
    /// T-b2 — PRIMARY-SOURCE ORACLE. The delivery gate's address test is TransportAddress::operator==
    /// (0x0062F744), which compares the IP only (0x00838E92-A6). A forwarded datagram from the robot's IP on
    /// another port is delivered; one from another IP is not. (Reliable frames are looked up on IP and port
    /// first — see T-d1.)
    /// </summary>
    [Fact]
    public void T_b2_ForwardedDataIsGatedOnThePeersIpButNotItsPort()
    {
        var (t, _, delivered) = Offline();
        Connect(t);
        var peer = t.Peer!;
        t.ProcessIncoming(Coz(0x25, 0x01, 0x02), new IPEndPoint(peer.Address, peer.Port + 1));
        Assert.Equal(new byte[] { 0x25, 0x01, 0x02 }, Assert.Single(delivered));

        t.ProcessIncoming(Coz(0x25, 0x03), new IPEndPoint(IPAddress.Parse("10.9.8.7"), peer.Port));
        Assert.Single(delivered);
    }

    // ================================================================ M1-c

    /// <summary>
    /// T-c1 — PRIMARY-SOURCE ORACLE. RobotConnectionManager::SendData sends only in state 2
    /// (0x0062F5A2-A8) and otherwise returns without queueing (0x0062F610). Its equivalent here is the
    /// robot-message path, <see cref="ReliableTransport.Send"/>: while Connecting nothing is queued, no
    /// sequence id is used and nothing goes out.
    /// </summary>
    [Fact]
    public void T_c1_ARobotMessageSentWhileConnectingIsRefused()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        Assert.Equal(LinkState.Connecting, t.State);
        int pending = t.Connection!.PendingCount, frames = t.OfflineOutbound.Count;
        ushort next = t.Connection.NextOutSeq;
        clk.NowMs = 1100;
        Assert.Throws<InvalidOperationException>(() => t.Send(new GetManufacturingInfo()));
        Assert.Throws<InvalidOperationException>(() => t.Send(new GetManufacturingInfo(), reliable: true, flush: true));
        Assert.Equal(pending, t.Connection.PendingCount);
        Assert.Equal(next, t.Connection.NextOutSeq);
        Assert.Equal(frames, t.OfflineOutbound.Count);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. ReliableTransport::SendData 0x008370E4-0x00837102 has no state gate: it picks
    /// type 4 or 5 from its reliable argument and hands that, with its flush argument, to QueueMessage. The
    /// manager's state-2 gate is a layer above it, so while a connection exists but the link is still
    /// Connecting the low-level call queues with its own options.
    /// </summary>
    [Fact]
    public void TheLowLevelSendDataIsNotGatedOnTheManagersConnectedState()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        Assert.Equal(LinkState.Connecting, t.State);
        var c = t.Connection!;
        ushort next = c.NextOutSeq;

        clk.NowMs = 1001;                                   // inside the 2 ms separation: both stay queued
        t.SendData(Data, reliable: true, flush: false);
        t.SendData(Data, reliable: false, flush: true);
        var rel = c.Pending[^2]; var unrel = c.Pending[^1];
        Assert.Equal(ReliableMessageType.SingleReliableMessage, rel.Type);
        Assert.Equal(next, rel.Seq);
        Assert.False(rel.FlushPacket);
        Assert.Equal(ReliableMessageType.SingleUnreliableMessage, unrel.Type);
        Assert.Equal(SequenceId.Invalid, unrel.Seq);
        Assert.True(unrel.FlushPacket);
        Assert.Equal(SequenceId.Next(next), c.NextOutSeq);
    }

    // ================================================================ M1-d

    /// <summary>
    /// T-d1 — PRIMARY-SOURCE ORACLE. A reliable frame is processed only for a connection found by
    /// TransportAddress::operator&lt; 0x00838ED2, which orders by IP (0x00838F18-1E) and then port
    /// (0x00838F26-28); from the robot's IP on another port there is no connection and the frame is dropped
    /// with a warning (0x0083789A) before any ack processing or delivery.
    /// </summary>
    [Fact]
    public void T_d1_AReliableFrameFromTheRightIpButAnotherPortIsDroppedBeforeAcks()
    {
        var (t, clk, delivered) = Offline();
        var warnings = new List<string>(); t.Warning += warnings.Add;
        Connect(t);
        clk.NowMs = 1040; t.Send(new SyncTime(0));          // our seq 2, sent, waiting for an ack
        var c = t.Connection!;
        Assert.Equal(1, c.PendingCount);
        var frame = Raw(ReliableMessageType.SingleReliableMessage, 2, 2, 2, Data);

        t.ProcessIncoming(frame, new IPEndPoint(t.Peer!.Address, t.Peer.Port + 1));
        Assert.Equal(1, c.PendingCount);                    // the ack in its header was not applied
        Assert.Equal(1, c.LastInAcked);
        Assert.Equal(2, c.NextInSeq);
        Assert.Empty(delivered);
        Assert.Contains(warnings, w => w.Contains("unexpected"));

        t.ProcessIncoming(frame);                           // the same frame from the peer is processed
        Assert.Equal(0, c.PendingCount);
        Assert.Equal(2, c.LastInAcked);
        Assert.Single(delivered);
    }

    // ================================================================ M1-e

    /// <summary>
    /// T-e1 — PRIMARY-SOURCE ORACLE. ReceiveData applies the header before the sub-messages:
    /// UpdateLastAckedMessage 0x00837818, the resend-on-ack gate 0x0083781C-2E, IsWaitingForAnyInRange
    /// 0x0083783E and AckMessage(seqMax) 0x0083784A. The walk then stops at the first invalid type
    /// (0x0083790E → 0x008379AE) with the earlier sub-messages already handled.
    /// Resend-on-ack is off in the engine (sMaxPacketsToReSendOnAck = 0), so it is turned on here only to
    /// make the gate's position observable.
    /// </summary>
    [Fact]
    public void T_e1_AnInvalidSubMessageTypeStopsTheWalkAfterTheHeaderHasBeenApplied()
    {
        var (t, clk, delivered) = Offline(new TransportOptions { MaxPacketsToReSendOnAck = 1 });
        Connect(t);
        clk.NowMs = 1040; t.Send(new SyncTime(0));                   // seq 2, sent at 1040
        clk.NowMs = 1041; t.SendData(Data, reliable: true, flush: true); // seq 3, held by the 2 ms separation
        var c = t.Connection!;
        Assert.Equal(2, c.PendingCount);
        int frames = t.OfflineOutbound.Count;

        var a = new RobotAvailable { SerialNumberHead = 7 }.ToBytes();
        var b = new RobotAvailable { SerialNumberHead = 8 }.ToBytes();
        clk.NowMs = 1050;
        t.ProcessIncoming(Raw(ReliableMessageType.MultipleReliableMessages, 2, 4, 2, Body(
            Sub(ReliableMessageType.SingleReliableMessage, a),
            ((byte)0x20, new byte[] { 1, 2, 3 }, null),              // not a valid message type
            Sub(ReliableMessageType.SingleReliableMessage, b))));

        Assert.Equal(1, c.PendingCount);                    // header ack removed seq 2
        Assert.Equal(frames + 1, t.OfflineOutbound.Count);  // and the resend-on-ack gate sent seq 3
        Assert.Contains(t.OfflineOutbound[^1].Messages, m => m.Seq == 3);
        Assert.Equal(4, c.LastInAcked);                     // AckMessage(seqMax)
        Assert.Equal(a, Assert.Single(delivered));          // first handled, the rest dropped
        Assert.Equal(3, c.NextInSeq);
    }

    /// <summary>
    /// T-e2 — PRIMARY-SOURCE ORACLE. The same, but the walk stops at a sub-message whose size overruns the
    /// body (0x00837926 → 0x00837A0C); what came before it stays processed.
    /// </summary>
    [Fact]
    public void T_e2_ASizeOverrunStopsTheWalkAndKeepsWhatCameBefore()
    {
        var (t, clk, delivered) = Offline();
        Connect(t);
        clk.NowMs = 1040; t.Send(new SyncTime(0));
        var c = t.Connection!;
        var a = new RobotAvailable { SerialNumberHead = 7 }.ToBytes();
        t.ProcessIncoming(Raw(ReliableMessageType.MultipleReliableMessages, 2, 3, 2, Body(
            Sub(ReliableMessageType.SingleReliableMessage, a),
            Sub(ReliableMessageType.SingleReliableMessage, new byte[] { 0x25, 0, 0 }, size: 200))));
        Assert.Equal(0, c.PendingCount);
        Assert.Equal(3, c.LastInAcked);
        Assert.Equal(a, Assert.Single(delivered));
    }

    /// <summary>
    /// T-e3 — PRIMARY-SOURCE ORACLE. A single-message frame whose type is not valid still has its reliable
    /// header applied, and HandleSubMessage's default case (0x0083751A) delivers nothing. HandleSubMessage
    /// checks and advances the sequence id before it switches on the type (0x00837430-3C).
    /// </summary>
    [Fact]
    public void T_e3_ASingleFrameOfInvalidTypeHasItsAcksAppliedAndDeliversNothing()
    {
        var (t, clk, delivered) = Offline();
        Connect(t);
        clk.NowMs = 1040; t.Send(new SyncTime(0));
        var c = t.Connection!;
        t.ProcessIncoming(Raw((ReliableMessageType)0x20, 2, 2, 2, Data));
        Assert.Equal(0, c.PendingCount);
        Assert.Equal(2, c.LastInAcked);
        Assert.Equal(3, c.NextInSeq);
        Assert.Empty(delivered);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-007 R10 (0x0083791C..0x00837926, 0x00837A04), M1-016 R18 (0x00837814,
    /// UpdateLastAckedMessage 0x00835AEA, +0x50 = now 0x00835B9C), M1-032 G2.6 (0x00837818). A container
    /// whose walk reaches a one- or two-byte remainder starting with a valid type is a size overrun: the
    /// header has already been applied (the ack removes seq 2, lastRecv is refreshed, ackOut = seqMax per
    /// R16 0x00837848), the sub-message before it is delivered, AddRecvError(1) is counted and the rest of the
    /// frame is abandoned. The engine's 1..2-byte over-read itself is not reproduced.
    /// Replaces the earlier test that asserted the whole frame was rejected, which R10 contradicts.
    /// </summary>
    [Theory]
    [InlineData(new byte[] { 0x04 })]
    [InlineData(new byte[] { 0x04, 0x01 })]
    public void AContainerEndingInAPartialSubMessageHeaderIsASizeOverrunAfterTheHeader(byte[] remainder)
    {
        var (t, clk, delivered) = Offline();
        Connect(t);
        clk.NowMs = 1040; t.Send(new SyncTime(0));          // our seq 2, sent, waiting for an ack
        var c = t.Connection!;
        Assert.Equal(1, c.PendingCount);
        clk.NowMs = 1050;
        var body = Body(Sub(ReliableMessageType.SingleReliableMessage, Data)).Concat(remainder).ToArray();
        t.ProcessIncoming(Raw(ReliableMessageType.MultipleReliableMessages, 2, 3, 2, body));
        Assert.Equal(0, c.PendingCount);                    // R18: ack 2 applied
        Assert.Equal(1050, c.LatestRecvMs);                 // R18 / G2.6: lastRecv refreshed
        Assert.Equal(3, c.LastInAcked);                     // R16: ackOut = seqMax before the walk
        Assert.Equal(Data, Assert.Single(delivered));       // R10: the earlier sub-message delivered
        Assert.Equal(3, c.NextInSeq);
        Assert.Equal(1, t.ReliableReceiveErrors[1]);        // R10: size overrun, AddRecvError(1)
        Assert.Equal(0, t.ReliableReceiveErrors[4]);
        Assert.Equal(LinkState.Connected, t.State);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-007 R10: a one- or two-byte remainder whose first byte is not a valid type
    /// is an invalid type, not a size overrun: the walk tests the type (IsValidMessageType 0x0083790A-0E)
    /// before it reads a size, counts AddRecvError(4) and stops there (→ 0x008379AE). The header has been
    /// applied and the earlier sub-messages handled.
    /// </summary>
    [Theory]
    [InlineData(new byte[] { 0x00 })]
    [InlineData(new byte[] { 0x20, 0x01 })]
    public void ARemainderStartingWithAnInvalidTypeStopsTheWalkThere(byte[] remainder)
    {
        var (t, clk, delivered) = Offline();
        Connect(t);
        clk.NowMs = 1040; t.Send(new SyncTime(0));
        var c = t.Connection!;
        var body = Body(Sub(ReliableMessageType.SingleReliableMessage, Data)).Concat(remainder).ToArray();
        t.ProcessIncoming(Raw(ReliableMessageType.MultipleReliableMessages, 2, 3, 2, body));
        Assert.Equal(0, c.PendingCount);
        Assert.Equal(3, c.LastInAcked);
        Assert.Equal(Data, Assert.Single(delivered));
        Assert.Equal(1, t.ReliableReceiveErrors[4]);
        Assert.Equal(0, t.ReliableReceiveErrors[1]);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-007 R10: an invalid sub-type counts AddRecvError(4) (0x0083790A..0x00837910)
    /// and a size overrun AddRecvError(1) (0x00837926), once per frame, since either abandons the rest.
    /// </summary>
    [Fact]
    public void R10_ContainerParseErrorsAreCountedByCode()
    {
        var (t, clk, _) = Offline();
        Connect(t);
        clk.NowMs = 1040;
        t.ProcessIncoming(Raw(ReliableMessageType.MultipleUnreliableMessages, 0, 0, 1, Body(
            ((byte)0x20, new byte[] { 1 }, null),
            ((byte)0x21, new byte[] { 2 }, null))));
        Assert.Equal(1, t.ReliableReceiveErrors[4]);
        t.ProcessIncoming(Raw(ReliableMessageType.MultipleUnreliableMessages, 0, 0, 1, Body(
            Sub(ReliableMessageType.SingleUnreliableMessage, new byte[] { 0x25 }, size: 50))));
        Assert.Equal(1, t.ReliableReceiveErrors[1]);
        Assert.Equal(1, t.ReliableReceiveErrors[4]);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-007 R16 (0x008378EA cmp.w r8,#9; 0x00837A6A): a reliable frame whose range
    /// does not hold nextIn is dropped with AddRecvError(5) unless it is type 9, which is still walked
    /// (its unreliable sub-messages delivered) and counts nothing.
    /// </summary>
    [Fact]
    public void R16_AnOutOfRangeReliableFrameCountsError5UnlessItIsMixed()
    {
        var (t, _, delivered) = Offline();
        Connect(t);                                         // nextIn is now 2
        var c = t.Connection!;
        t.ProcessIncoming(Raw(ReliableMessageType.SingleReliableMessage, 5, 5, 1, Data));
        Assert.Equal(1, t.ReliableReceiveErrors[5]);
        Assert.Empty(delivered);

        t.ProcessIncoming(Raw(ReliableMessageType.MultipleMixedMessages, 5, 5, 1, Body(
            Sub(ReliableMessageType.SingleUnreliableMessage, Data))));
        Assert.Equal(1, t.ReliableReceiveErrors[5]);
        Assert.Equal(Data, Assert.Single(delivered));
        Assert.Equal(2, c.NextInSeq);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-002 R3 (0x00837632 cmp r7,#0xa; prefix table 0x00837B0C; pass-through
    /// 0x00837792..0x008377A0): a datagram under 10 bytes after the COZ prefix counts AddRecvError(0) then
    /// AddRecvError(4); one without the RE\x01 prefix counts AddRecvError(2) then AddRecvError(4). Both are
    /// still passed on as data.
    /// </summary>
    [Fact]
    public void R3_HeaderValidationFailuresAreCountedAndPassedOn()
    {
        var (t, _, delivered) = Offline();
        Connect(t);
        t.ProcessIncoming(Coz(0x25, 1, 2, 3, 4));
        Assert.Equal(1, t.ReliableReceiveErrors[0]);
        Assert.Equal(0, t.ReliableReceiveErrors[2]);
        Assert.Equal(1, t.ReliableReceiveErrors[4]);
        t.ProcessIncoming(Coz(0x25, 0x45, 0x01, 9, 8, 7, 6, 5, 4, 3, 2, 1));
        Assert.Equal(1, t.ReliableReceiveErrors[0]);
        Assert.Equal(1, t.ReliableReceiveErrors[2]);
        Assert.Equal(2, t.ReliableReceiveErrors[4]);
        Assert.Equal(2, delivered.Count);
    }

    /// <summary>
    /// COMPATIBILITY_POLICY M1-038 (operator decision D8). The original deletes the connection on a handled
    /// DisconnectRequest (HandleSubMessage 0x008374A0, DeleteConnection veneer 0x008D123C) and keeps walking
    /// the frame through the freed pointer; this stack stops processing the frame there, so the data
    /// sub-message after it is not delivered.
    /// </summary>
    [Fact]
    public void M1_038_NothingAfterAHandledDisconnectRequestInTheSameFrameIsProcessed()
    {
        var (t, _, delivered) = Offline();
        var reasons = new List<string>(); t.Disconnected += reasons.Add;
        Connect(t);                                         // nextIn is now 2
        t.ProcessIncoming(Raw(ReliableMessageType.MultipleReliableMessages, 2, 3, 1, Body(
            Sub(ReliableMessageType.DisconnectRequest, Array.Empty<byte>()),
            Sub(ReliableMessageType.SingleReliableMessage, Data))));
        Assert.Empty(delivered);
        Assert.Single(reasons);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE, the edge of M1-038. M1-007 R12 (0x0083742E..0x0083743C): a DisconnectRequest
    /// whose seq is not nextIn is dropped silently before dispatch, so the connection is not deleted and the
    /// walk goes on as in the original; the policy does not apply and the next sub-message is delivered.
    /// </summary>
    [Fact]
    public void AnOutOfSequenceDisconnectRequestDoesNotStopTheFrame()
    {
        var (t, _, delivered) = Offline();
        var reasons = new List<string>(); t.Disconnected += reasons.Add;
        Connect(t);                                         // nextIn is now 2
        t.ProcessIncoming(Raw(ReliableMessageType.MultipleReliableMessages, 1, 2, 1, Body(
            Sub(ReliableMessageType.DisconnectRequest, Array.Empty<byte>()),     // seq 1: a duplicate
            Sub(ReliableMessageType.SingleReliableMessage, Data))));            // seq 2
        Assert.Equal(Data, Assert.Single(delivered));
        Assert.Empty(reasons);
        Assert.Equal(LinkState.Connected, t.State);
    }

    // ================================================================ M1-f

    /// <summary>
    /// T-f1 — PRIMARY-SOURCE ORACLE. ReliableTransport::ReceiveData hands the bytes after the COZ prefix to
    /// its receiver unchanged when fewer than 10 follow or they do not begin RE\x01 (0x00837632-42, the
    /// receiver call 0x00837792-A0), and RobotConnectionData::ReceiveData 0x0062E4B0 queues them as an
    /// arrived message (PushArrivedMessage 0x0062E25C), delivered while Connected.
    /// </summary>
    [Fact]
    public void T_f1_ADatagramWithoutAReliableHeaderIsPassedOnAsData()
    {
        var (t, _, delivered) = Offline();
        Connect(t);
        var shortOne = new byte[] { 0x25, 1, 2, 3, 4 };
        var noPrefix = new byte[] { 0x25, 0x45, 0x01, 9, 8, 7, 6, 5, 4, 3, 2, 1 };
        t.ProcessIncoming(Coz(shortOne));
        t.ProcessIncoming(Coz(noPrefix));
        Assert.Equal(2, delivered.Count);
        Assert.Equal(shortOne, delivered[0]);
        Assert.Equal(noPrefix, delivered[1]);
    }

    /// <summary>T-f2 — PRIMARY-SOURCE ORACLE. The same datagrams are dropped while Connecting (0x0062F724-2A).</summary>
    [Fact]
    public void T_f2_TheSameDatagramsAreDroppedWhileConnecting()
    {
        var (t, _, delivered) = Offline();
        t.OfflineConnect();
        t.ProcessIncoming(Coz(0x25, 1, 2, 3, 4));
        t.ProcessIncoming(Coz(0x25, 0x45, 0x01, 9, 8, 7, 6, 5, 4, 3, 2, 1));
        Assert.Empty(delivered);
        Assert.Equal(LinkState.Connecting, t.State);
    }

    // ================================================================ M1-h

    /// <summary>
    /// T-h1 — PRIMARY-SOURCE ORACLE. ReliableTransport's disconnect sends one DisconnectRequest (0x0083801C)
    /// and then deletes the connection (0x0083802A → DeleteConnection 0x008375F0): nothing is resent or
    /// pinged for it afterwards, and later frames are not processed for it.
    /// </summary>
    [Fact]
    public void T_h1_DisconnectMakesOneAttemptAndDeletesTheConnectionAtOnce()
    {
        var (t, clk, delivered) = Offline();
        var reasons = new List<string>(); t.Disconnected += reasons.Add;
        Connect(t);
        clk.NowMs = 1040; t.Send(new SyncTime(0));          // an unacked message that would be resent
        clk.NowMs = 1045;
        t.Disconnect("test");
        Assert.Null(t.Connection);
        Assert.Equal(LinkState.Disconnected, t.State);
        Assert.Equal(1, t.OfflineOutbound.Count(f => f.Messages.Any(m => m.Type == ReliableMessageType.DisconnectRequest)));
        int frames = t.OfflineOutbound.Count;

        clk.NowMs = 1200; t.OfflineTick();                  // well past 33.3 ms: no resend, no ping
        Assert.Equal(frames, t.OfflineOutbound.Count);
        t.ProcessIncoming(Raw(ReliableMessageType.SingleReliableMessage, 2, 2, 3, Data));
        Assert.Empty(delivered);
        Assert.Equal(new[] { "test" }, reasons);
    }

    /// <summary>
    /// T-h2 — PRIMARY-SOURCE ORACLE. The one attempt is subject to the minimum send separation
    /// (SendOptimalUnAckedPackets 0x008363E2-FC): inside it no packet goes out, and the connection is still
    /// deleted at once (0x0083802A). Nothing waits to force the request onto the wire.
    /// </summary>
    [Fact]
    public void T_h2_ADisconnectInsideTheSendSeparationSendsNothingAndStillDeletes()
    {
        var (t, clk, _) = Offline();
        Connect(t);
        int frames = t.OfflineOutbound.Count;               // the ConnectionRequest, sent at 1000
        clk.NowMs = 1001;                                   // 1 ms later: inside the 2 ms separation
        t.Disconnect();
        Assert.Null(t.Connection);
        Assert.Equal(frames, t.OfflineOutbound.Count);
        Assert.Equal(LinkState.Disconnected, t.State);
    }

    // ================================================================ M1-i

    /// <summary>
    /// T-i1 — PRIMARY-SOURCE ORACLE. ~RobotConnectionManager 0x0062EE98 runs DisconnectCurrent 0x0062EF20,
    /// which disconnects the peer (0x0062EF52), and only then StopClient (0x0062EEA2). Dispose while
    /// Connected makes the disconnect attempt, then closes the socket, without waiting for anything queued.
    /// </summary>
    [Fact]
    public void T_i1_DisposeWhileConnectedDisconnectsThenClosesWithoutDraining()
    {
        var clk = new ManualClock { NowMs = 1000 };
        var net = new ScriptedReceive();
        var t = new ReliableTransport(TransportOptions.EngineDefaults, clk) { ManualPump = true, ReceiveHook = net.Receive };
        var outbound = new List<Frame>();
        t.FrameTrace += e => { if (e.Outbound && e.Frame is not null) outbound.Add(e.Frame); };
        t.Connect(IPAddress.Loopback, 59981);
        net.Datagram(ConnectionResponse(), t.Peer!);
        clk.NowMs = 1001; t.Pump();
        Assert.Equal(LinkState.Connected, t.State);

        clk.NowMs = 1010;
        t.Send(new SyncTime(0)); t.Send(new GetManufacturingInfo());     // queued, not yet worth sending
        var c = t.Connection!;
        clk.NowMs = 1015;
        t.Dispose();

        Assert.Null(t.Connection);
        Assert.Null(t.LocalEndPoint);                       // socket closed
        Assert.Equal(LinkState.Disconnected, t.State);
        Assert.Contains(outbound, f => f.Messages.Any(m => m.Type == ReliableMessageType.DisconnectRequest));
        Assert.Contains(c.Pending, p => p.Type == ReliableMessageType.DisconnectRequest);  // unacked, not waited for
    }

    /// <summary>T-i2 — REGRESSION ONLY. Dispose with no connection sends nothing and reports nothing.</summary>
    [Fact]
    public void T_i2_DisposeWithNoConnectionSendsNothing()
    {
        var t = new ReliableTransport();
        var events = new List<FrameEvent>(); t.FrameTrace += events.Add;
        var reasons = new List<string>(); t.Disconnected += reasons.Add;
        t.Dispose();
        Assert.Empty(events);
        Assert.Empty(reasons);
    }

    /// <summary>
    /// BLOCKED, kept visible. What the engine does when destroyed while its connection is still being set
    /// up is not established; this stack sends nothing, as before. REGRESSION ONLY.
    /// </summary>
    [Fact]
    public void DisposeWhileConnectingSendsNothing()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        int frames = t.OfflineOutbound.Count;
        clk.NowMs = 1050;
        t.Dispose();
        Assert.Equal(frames, t.OfflineOutbound.Count);
        Assert.Equal(LinkState.Disconnected, t.State);
    }

    // ================================================================ M1-j

    /// <summary>
    /// T-j1 — PRIMARY-SOURCE ORACLE. The update is scheduled every 2 ms (0x0083689E) and re-armed at the
    /// now() taken when the run began (0x007FC1BC) plus the period (0x007FC36A-76): never sooner than 2 ms
    /// after the re-arm point, and a late run is followed by one run a full period later, not a burst.
    /// </summary>
    [Fact]
    public void T_j1_TheUpdateIsReArmedFromWhenItRanWithNoCatchUp()
    {
        var clk = new ManualClock { NowMs = 0 };
        var cadence = new TransportCadence(clk, 2.0);
        clk.NowMs = 1.9; Assert.False(cadence.TryBegin());
        clk.NowMs = 2.0; Assert.True(cadence.TryBegin());
        Assert.Equal(4.0, cadence.DueMs);
        clk.NowMs = 3.9; Assert.False(cadence.TryBegin());

        clk.NowMs = 9.0; Assert.True(cadence.TryBegin());  // late by 5 ms
        Assert.Equal(11.0, cadence.DueMs);                  // from the new re-arm point
        Assert.False(cadence.TryBegin());                   // no catch-up run for the missed periods
        clk.NowMs = 10.9; Assert.False(cadence.TryBegin());
        clk.NowMs = 11.0; Assert.True(cadence.TryBegin());
    }

    /// <summary>
    /// T-j2 — PRIMARY-SOURCE ORACLE. ReliableTransport::Update runs the UDP transport's update first
    /// (0x00837BA2), draining the socket, and only then updates the connection. An ack that arrives in time
    /// for the update that would resend therefore prevents that resend.
    /// </summary>
    [Fact]
    public void T_j2_IncomingAcksAreAppliedBeforeTheSameUpdateResends()
    {
        static (ReliableTransport t, ManualClock clk, ScriptedReceive net) Rig(int port)
        {
            var clk = new ManualClock { NowMs = 1000 };
            var net = new ScriptedReceive();
            var t = new ReliableTransport(TransportOptions.EngineDefaults, clk) { ManualPump = true, ReceiveHook = net.Receive };
            t.Connect(IPAddress.Loopback, port);
            net.Datagram(ConnectionResponse(), t.Peer!);
            clk.NowMs = 1001; t.Pump();
            clk.NowMs = 1010; t.SendData(Data, reliable: true, flush: true);   // seq 2, sent at 1010
            clk.NowMs = 1044;                                                  // > 1010 + 33.3: resend due
            return (t, clk, net);
        }
        var ackOnly = Raw(ReliableMessageType.Ping, 0, 0, 2, new PingPayload(1, 1, 0, false).ToBytes());

        var (acked, _, net) = Rig(59982);
        net.Datagram(ackOnly, acked.Peer!);
        acked.Pump();
        Assert.Equal(0, acked.Connection!.PendingCount);
        Assert.Equal(0, acked.Connection.ResendFrames);
        acked.Dispose();

        var (control, _, _) = Rig(59983);                   // without the ack the same update resends
        control.Pump();
        Assert.Equal(1, control.Connection!.ResendFrames);
        control.Dispose();
    }

    // ================================================================ M1-k

    /// <summary>
    /// T-k1 — PRIMARY-SOURCE ORACLE. On ENOTCONN, UDPTransport::TryToReadMessage first gives the warning
    /// every non-EAGAIN error gets (0x0083AAF6), then closes the socket and opens a new one
    /// (0x0083AB22-34); the connection and its reliable state are kept.
    /// </summary>
    [Fact]
    public void T_k1_NotConnectedReopensTheSocketAndKeepsTheConnection()
    {
        var (t, clk, net) = Connected(59984);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        clk.NowMs = 1040; t.Send(new SyncTime(0));
        var c = t.Connection!;
        ushort next = c.NextOutSeq, lastIn = c.LastInAcked; int pending = c.PendingCount;
        Assert.True(pending > 0);

        net.Error(SocketError.NotConnected);
        clk.NowMs = 1041; t.Pump();
        var old = net.SocketsSeen[^1];
        clk.NowMs = 1043; t.Pump();                         // the next read is on the new socket
        var fresh = net.SocketsSeen[^1];

        Assert.NotSame(old, fresh);
        Assert.True(old.SafeHandle.IsClosed);
        var local = Assert.IsType<IPEndPoint>(fresh.LocalEndPoint);
        Assert.Equal(IPAddress.Any, local.Address);
        Assert.NotEqual(0, local.Port);
        Assert.Equal(LinkState.Connected, t.State);
        Assert.Same(c, t.Connection);
        Assert.Equal(next, c.NextOutSeq);
        Assert.Equal(lastIn, c.LastInAcked);
        Assert.Equal(pending, c.PendingCount);
        Assert.Contains("NotConnected", Assert.Single(warnings));
        t.Dispose();
    }

    /// <summary>
    /// T-k2 — PRIMARY-SOURCE ORACLE. Any other receive error is a warning (0x0083AAF6) and nothing more:
    /// the link stays Connected with its reliable state.
    /// </summary>
    [Fact]
    public void T_k2_AnyOtherReceiveErrorIsOnlyAWarning()
    {
        var (t, clk, net) = Connected(59985);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        clk.NowMs = 1040; t.Send(new SyncTime(0));
        var c = t.Connection!;
        ushort next = c.NextOutSeq, lastIn = c.LastInAcked; int pending = c.PendingCount;

        net.Error(SocketError.ConnectionReset);
        clk.NowMs = 1041; t.Pump();
        Assert.Single(warnings);
        Assert.Equal(LinkState.Connected, t.State);
        Assert.Same(c, t.Connection);
        Assert.Equal(next, c.NextOutSeq);
        Assert.Equal(lastIn, c.LastInAcked);
        Assert.Equal(pending, c.PendingCount);
        t.Dispose();
    }

    /// <summary>T-k3 — PRIMARY-SOURCE ORACLE. EAGAIN (WouldBlock) is silent and ends nothing (0x0083AAC4).</summary>
    [Fact]
    public void T_k3_WouldBlockIsSilent()
    {
        var (t, clk, net) = Connected(59986);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        var reasons = new List<string>(); t.Disconnected += reasons.Add;
        net.Error(SocketError.WouldBlock);
        clk.NowMs = 1041; t.Pump();
        Assert.Empty(warnings);
        Assert.Empty(reasons);
        Assert.Equal(LinkState.Connected, t.State);
        Assert.NotNull(t.Connection);
        t.Dispose();
    }

    /// <summary>A single unreliable frame whose whole datagram is <paramref name="datagramBytes"/> long.</summary>
    private static byte[] UnreliableOfLength(int datagramBytes)
    {
        var payload = new byte[datagramBytes - ReliableHeader.Length];
        payload[0] = 0x25;
        return Raw(ReliableMessageType.SingleUnreliableMessage, 0, 0, 1, payload);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-002 B16/B17: the receive buffer is 0x5C0 = 1472 bytes (0x0083AA66..0x0083AA98).
    /// A 1472-byte datagram is processed; a 1473-byte one arrives with MSG_TRUNC set (0x0083AAA8 ubfx r0,r7,#5,#1),
    /// passes the size and prefix checks (0x0083A780, 0x0083A80E), logs Recv.Truncated, counts
    /// AddRecvError(1) (0x0083A8C4..0x0083A8C8) and is dropped, and the read loop goes on (0x0083AABA returns 1):
    /// the datagram after it is read in the same update.
    /// </summary>
    [Fact]
    public void B17_ADatagramLargerThan1472BytesIsDroppedAsTruncatedAndTheDrainGoesOn()
    {
        var (t, clk, net) = Connected(59987);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        var delivered = new List<byte[]>(); t.DataReceived += delivered.Add;
        Assert.Equal(1472, ReliableTransport.ReceiveBufferBytes);

        var fits = UnreliableOfLength(1472);
        net.Datagram(fits, t.Peer!);
        net.Datagram(UnreliableOfLength(1473), t.Peer!);
        net.Datagram(Raw(ReliableMessageType.SingleReliableMessage, 2, 2, 1, Data), t.Peer!);
        clk.NowMs = 1041; t.Pump();

        Assert.Equal(2, delivered.Count);
        Assert.Equal(fits.AsSpan(ReliableHeader.Length).ToArray(), delivered[0]);
        Assert.Equal(Data, delivered[1]);                   // read after the truncated one, same update
        Assert.Equal(1, t.UdpReceiveErrors[1]);
        Assert.Contains(warnings, w => w.Contains("Recv.Truncated"));
        Assert.Equal(LinkState.Connected, t.State);
        t.Dispose();
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-002 B17 order: the prefix check (0x0083A80E) comes before the truncation
    /// test (0x0083A888), so an oversize datagram with a bad prefix takes the BadPrefix path, not
    /// Recv.Truncated, and is not counted as AddRecvError(1). The read still goes on.
    /// </summary>
    [Fact]
    public void B17_AnOversizeDatagramWithABadPrefixFailsThePrefixCheckFirst()
    {
        var (t, clk, net) = Connected(59988);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        var delivered = new List<byte[]>(); t.DataReceived += delivered.Add;
        var bad = new byte[1500]; bad[0] = (byte)'X';
        net.Datagram(bad, t.Peer!);
        net.Datagram(Raw(ReliableMessageType.SingleReliableMessage, 2, 2, 1, Data), t.Peer!);
        clk.NowMs = 1041; t.Pump();

        Assert.Equal(0, t.UdpReceiveErrors[1]);
        Assert.Contains(warnings, w => w.Contains("bad UDP prefix"));
        Assert.DoesNotContain(warnings, w => w.Contains("Recv.Truncated"));
        Assert.Equal(Data, Assert.Single(delivered));
        t.Dispose();
    }

    /// <summary>
    /// HOST MAPPING, not engine behaviour. M1-002 B17 is implemented on the host's report of a truncated
    /// datagram: on this platform a real UDP socket read into a 1472-byte buffer fails with MessageSize,
    /// leaves the datagram's first bytes in the buffer and consumes it. This checks that mapping end to end
    /// on the production receive path (no hook): the oversize datagram is counted as AddRecvError(1) and
    /// the one after it is still read in the same update.
    /// </summary>
    [Fact]
    public void B17_TheHostReportsAnOversizeDatagramAsTruncatedAndTheDrainGoesOn()
    {
        using var robot = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        robot.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var clk = new ManualClock { NowMs = 1000 };
        var t = new ReliableTransport(TransportOptions.EngineDefaults, clk) { ManualPump = true };
        var delivered = new List<byte[]>(); t.DataReceived += delivered.Add;
        t.Connect(IPAddress.Loopback, ((IPEndPoint)robot.LocalEndPoint!).Port);
        var local = new IPEndPoint(IPAddress.Loopback, ((IPEndPoint)t.LocalEndPoint!).Port);

        robot.SendTo(ConnectionResponse(), local);
        Thread.Sleep(100);
        clk.NowMs = 1001; t.Pump();
        Assert.Equal(LinkState.Connected, t.State);

        robot.SendTo(UnreliableOfLength(1500), local);
        robot.SendTo(Raw(ReliableMessageType.SingleReliableMessage, 2, 2, 1, Data), local);
        Thread.Sleep(100);
        clk.NowMs = 1041; t.Pump();

        Assert.Equal(1, t.UdpReceiveErrors[1]);
        Assert.Equal(Data, Assert.Single(delivered));
        Assert.Equal(LinkState.Connected, t.State);
        t.Dispose();
    }

    // ------------------------------------------------------------------ rig

    private static byte[] ConnectionResponse() =>
        FrameCodec.Encode(Frame.Single(new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1), 1));

    private static (ReliableTransport t, ManualClock clk, ScriptedReceive net) Connected(int port)
    {
        var clk = new ManualClock { NowMs = 1000 };
        var net = new ScriptedReceive();
        var t = new ReliableTransport(TransportOptions.EngineDefaults, clk) { ManualPump = true, ReceiveHook = net.Receive };
        t.Connect(IPAddress.Loopback, port);
        net.Datagram(ConnectionResponse(), t.Peer!);
        clk.NowMs = 1001; t.Pump();
        Assert.Equal(LinkState.Connected, t.State);
        return (t, clk, net);
    }

    /// <summary>
    /// What the transport's receive call returns, in order; once empty it reports WouldBlock. A datagram
    /// larger than the buffer is reported as Winsock reports it (see
    /// <see cref="B17_TheHostReportsAnOversizeDatagramAsTruncatedAndTheDrainGoesOn"/>): the buffer holds its
    /// first bytes, the datagram is consumed, and the call fails with MessageSize.
    /// </summary>
    private sealed class ScriptedReceive
    {
        private readonly Queue<object> _items = new();
        public readonly List<Socket> SocketsSeen = new();

        public void Datagram(byte[] data, IPEndPoint from) => _items.Enqueue((data, from));
        public void Error(SocketError e) => _items.Enqueue(e);

        public int Receive(Socket socket, byte[] buffer, ref EndPoint from)
        {
            SocketsSeen.Add(socket);
            if (_items.Count == 0) throw new SocketException((int)SocketError.WouldBlock);
            var item = _items.Dequeue();
            if (item is SocketError e) throw new SocketException((int)e);
            var (data, ep) = ((byte[], IPEndPoint))item;
            if (data.Length > buffer.Length)
            {
                data.AsSpan(0, buffer.Length).CopyTo(buffer);
                throw new SocketException((int)SocketError.MessageSize);
            }
            data.CopyTo(buffer, 0);
            from = ep;
            return data.Length;
        }
    }
}
