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
        Assert.True(d.Flush(TimeSpan.FromSeconds(5)));      // R37 / B21: Disconnect is a queued action, in sync mode too
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
    /// with a warning (0x0083789A) before any ack processing or delivery. M1-018 R13: the warning is
    /// "unconnected source", and a type-4 frame creates no connection.
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
        Assert.Contains(warnings, w => w.Contains("unconnected source"));   // M1-018 R13

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
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));      // R37 / B21: Disconnect is a queued action, in sync mode too
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
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));      // R37 / B21: Disconnect is a queued action, in sync mode too
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
        var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true) { ReceiveHook = net.Receive };
        var outbound = new List<Frame>();
        t.FrameTrace += e => { if (e.Outbound && e.Frame is not null) outbound.Add(e.Frame); };
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));   // R39: the socket is opened by Start (posted), not Connect
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
    /// PRIMARY-SOURCE ORACLE. Replaces the test that Dispose while Connecting sends nothing. M1-025 / M1-019 B33:
    /// DisconnectCurrent (0x0062EF20..0x0062EF52) calls RT Disconnect with the robot address unconditionally,
    /// then ~RobotConnectionManager calls StopClient (0x0062EE98, 0x0062EEA2). A Connecting transport has a
    /// connection (made by its type-1 SendMessage, G2.1), so the Disconnect closure (0x00837FFA) sends its
    /// type 3 on it (here outside the 2 ms separation), deletes it, and Stop then clears and closes.
    /// </summary>
    [Fact]
    public void B33_DisposeWhileConnectingSendsTheDisconnectRequestThenStops()
    {
        var (t, clk, _) = Offline();
        t.OfflineConnect();
        var c = t.Connection!;
        int frames = t.OfflineOutbound.Count;
        clk.NowMs = 1050;
        t.Dispose();
        Assert.Equal(frames + 1, t.OfflineOutbound.Count);
        Assert.Contains(t.OfflineOutbound[^1].Messages, m => m.Type == ReliableMessageType.DisconnectRequest && m.IsReliable);
        Assert.Contains(c.Pending, p => p.Type == ReliableMessageType.DisconnectRequest);
        Assert.Null(t.Connection);
        Assert.Equal(LinkState.Disconnected, t.State);
    }

    // ================================================================ M1-j

    // Replaces T-j1, which tested a cadence helper that was not the production scheduler, and whose comment
    // ("a late run is followed by one run a full period later, not a burst") contradicted G1.10: a backlog of
    // posted copies does run back to back.

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-021 G1.2 (0x007FCEBA steady_clock::now; 0x007FCEBE/C2 #0xF4240; 0x007FCEC6
    /// umull; 0x007FCED2 adds; 0x007FCED6 strd): the first time is now + period × 1e6 ns, so on the production
    /// scheduler with the 2 ms period (R35 ScheduleCallback 0x00836896 movs r2,#2) nothing is posted before
    /// 2 000 000 ns have passed since scheduling, and one copy is posted at exactly that time.
    /// </summary>
    [Fact]
    public void M1_021_G1_2_TheFirstRunIsDueOnePeriodAfterScheduling()
    {
        long now = 5_000_000; int posts = 0;
        var s = new TransportScheduler(() => now, TransportOptions.EngineDefaults.UpdateIntervalMs, () => posts++);
        Assert.Equal(5_000_000 + 2 * 1_000_000, s.DueNs);
        now = 6_999_999; Assert.False(s.Pass());
        Assert.Equal(0, posts);
        now = 7_000_000; Assert.True(s.Pass());
        Assert.Equal(1, posts);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-021 G1.7 (0x007FC1BC now; 0x007FC1C8..0x007FC1D6 due test; 0x007FC270
    /// AddTaskHolder) and G1.8 (0x007FC344 repeat byte; 0x007FC352..0x007FC376; 0x007FC3C4): a due pass posts
    /// ONE copy and re-arms the entry at the now it read + period. A pass made late by several periods still
    /// posts one copy, not one per missed period, and the next is due a full period after that late now
    /// (fixed delay; missed periods are not added back). The scheduler only posts; it never runs the callback.
    /// </summary>
    [Fact]
    public void M1_021_G1_7_G1_8_ADuePassPostsOneCopyAndReArmsAtThatNowPlusThePeriod()
    {
        long now = 0; int posts = 0;
        var s = new TransportScheduler(() => now, 2.0, () => posts++);
        now = 2_000_000; Assert.True(s.Pass());
        Assert.Equal(4_000_000, s.DueNs);                   // G1.8: that now + period
        Assert.False(s.Pass());                             // same now: not due again

        now = 10_500_000;                                   // late by 6.5 ms, three periods missed
        Assert.True(s.Pass());
        Assert.Equal(2, posts);                             // one copy, no catch-up copies
        Assert.Equal(12_500_000, s.DueNs);                  // fixed delay from the late now
        Assert.False(s.Pass());
        now = 12_499_999; Assert.False(s.Pass());
        now = 12_500_000; Assert.True(s.Pass());
        Assert.Equal(3, posts);
        Assert.Equal(3, s.Posted);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-021 G1.9 (AddTaskHolder 0x007FCCF4, push 0x007FCD50..0x007FCD54, never merged)
    /// and G1.10 (Execute 0x007FBF78..0x007FBF8E; Run 0x007FD0C6..0x007FD128): on the production executor,
    /// items posted while it is busy are all kept, then run one at a time, in the order posted, on its one
    /// thread, back to back and never overlapping.
    /// </summary>
    [Fact]
    public void M1_021_G1_9_G1_10_PostedItemsRunInOrderOnOneThreadWithoutOverlapAndABacklogRunsBackToBack()
    {
        var exec = new SerialExecutor("test-executor");
        exec.Start();
        var gate = new ManualResetEventSlim(); var busy = new ManualResetEventSlim();
        exec.Post(() => { busy.Set(); gate.Wait(TimeSpan.FromSeconds(10)); });
        Assert.True(busy.Wait(TimeSpan.FromSeconds(5)));

        var order = new List<int>(); var threads = new HashSet<int>();
        int running = 0, maxRunning = 0;
        for (int i = 0; i < 20; i++)
        {
            int n = i;
            Assert.True(exec.Post(() =>
            {
                int r = Interlocked.Increment(ref running);
                maxRunning = Math.Max(maxRunning, r);
                order.Add(n); threads.Add(Environment.CurrentManagedThreadId);
                Thread.SpinWait(2000);
                Interlocked.Decrement(ref running);
            }));
        }
        Assert.Empty(order);                                 // all twenty are waiting behind the busy item
        var done = new ManualResetEventSlim();
        exec.Post(() => done.Set());
        gate.Set();
        Assert.True(done.Wait(TimeSpan.FromSeconds(5)));

        Assert.Equal(Enumerable.Range(0, 20), order);        // every copy kept, in order (G1.9, G1.10)
        Assert.Equal(1, maxRunning);                         // never overlapping
        Assert.Equal(exec.ThreadId, Assert.Single(threads)); // on the executor's one thread
        exec.Complete(); exec.Join(TimeSpan.FromSeconds(2));
    }

    /// <summary>Records each item the transport's executor runs, with the thread it ran on.</summary>
    private static void Traced(ReliableTransport t, List<(ReliableTransport.ExecutorItem item, int thread)> into) =>
        t.ExecutorTrace = i => { lock (into) into.Add((i, Environment.CurrentManagedThreadId)); };

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-010 R35 / B14: the ctor sets up the RelTransport queue and ChangeSyncMode(false)
    /// (0x008367E2) requests the 2 ms update (0x00836896, 0x0083689E), so a production transport runs its
    /// update without any Connect. M1-021 G1.7: the scheduler posts copies and never runs them, so every update
    /// runs on the executor thread, not on the scheduler's.
    /// </summary>
    [Fact]
    public void M1_010_R35_TheUpdateIsRequestedAtConstructionAndRunsOnTheExecutor()
    {
        using var t = new ReliableTransport();
        var seen = new List<(ReliableTransport.ExecutorItem item, int thread)>();
        Traced(t, seen);
        Assert.True(SpinWait.SpinUntil(() => { lock (seen) return seen.Count(s => s.item.Kind == "tick") >= 3; }, 5000),
            "no update ran without a Connect");
        Assert.Equal(LinkState.Idle, t.State);
        List<(ReliableTransport.ExecutorItem item, int thread)> snap; lock (seen) snap = seen.ToList();
        Assert.All(snap, s => Assert.Equal(t.Executor.ThreadId, s.thread));
        Assert.NotEqual(t.Scheduler!.ThreadId, t.Executor.ThreadId);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-010 R35 / B14: the update is scheduled once, at construction, and the lambda
    /// (0x008383CE) runs Update on every copy; nothing in the rows stops the schedule when a connection times
    /// out (R19 / G2.7: now &gt; lastRecv + 5000, lastRecv stamped when the connection is made, G2.3). So after
    /// the timeout the update goes on running.
    /// </summary>
    [Fact]
    public void M1_010_R35_TheUpdateKeepsRunningAfterAConnectionTimesOut()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk);
        var seen = new List<(ReliableTransport.ExecutorItem item, int thread)>();
        Traced(t, seen);
        t.Connect(IPAddress.Loopback, 59990);
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        Assert.Equal(1000, t.Connection!.LatestRecvMs);      // G2.3: stamped when the posted Connect made it

        clk.NowMs = 1000 + 5000.1;                            // G2.7: strictly past lastRecv + 5000
        Assert.True(SpinWait.SpinUntil(() => t.State == LinkState.Disconnected, 5000), "the connection never timed out");
        int ticksAtTimeout; lock (seen) ticksAtTimeout = seen.Count(s => s.item.Kind == "tick");
        Assert.True(SpinWait.SpinUntil(() => { lock (seen) return seen.Count(s => s.item.Kind == "tick") >= ticksAtTimeout + 5; }, 5000),
            "the update stopped when the connection timed out");
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-035 R37 (QueueMessage 0x00836B66..0x00836BE4; closure 0x00837DEA; QueueAction
    /// 0x00836FF6): in async mode a send is not run on the caller's thread; it is posted to the same queue as
    /// the update, and the closure calls SendMessage under the transport mutex. Sends and updates are therefore
    /// FIFO on one thread: with the executor held busy, the update copies posted before the send (G1.9: kept,
    /// never merged) run first, back to back (G1.10), then the send, then the copies posted after it. The
    /// caller does not wait for the busy executor. M1-020 B15: this is the production (async) mode.
    /// The posted time R37 passes to SendMessage is checked in
    /// <see cref="M1_035_CA10_TheTimeSendMessageIsCalledWithIsStoredOnEveryEntryItQueues"/>.
    /// </summary>
    [Fact]
    public void M1_035_R37_ASendIsPostedAndRunsFifoBetweenUpdatesOnTheTransportThread()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk);
        var seen = new List<(ReliableTransport.ExecutorItem item, int thread)>();
        Traced(t, seen);
        t.Connect(IPAddress.Loopback, 59989);
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        var c = t.Connection!;
        int pendingBefore = c.PendingCount;                   // the ConnectionRequest, unacked

        var gate = new ManualResetEventSlim(); var busy = new ManualResetEventSlim();
        t.Executor.Post(() => { busy.Set(); gate.Wait(TimeSpan.FromSeconds(10)); });   // a long-running item
        Assert.True(busy.Wait(TimeSpan.FromSeconds(5)));
        int start; lock (seen) start = seen.Count;
        long p0 = t.Scheduler!.Posted;
        Assert.True(SpinWait.SpinUntil(() => t.Scheduler.Posted >= p0 + 3, 5000));

        clk.NowMs = 1010;
        t.SendData(Data, reliable: true, flush: false);       // returns although the executor is busy
        Assert.Equal(pendingBefore, c.PendingCount);          // and did not run SendMessage on this thread
        long p1 = t.Scheduler.Posted;
        Assert.True(SpinWait.SpinUntil(() => t.Scheduler.Posted >= p1 + 2, 5000));
        gate.Set();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));

        List<(ReliableTransport.ExecutorItem item, int thread)> after; lock (seen) after = seen.Skip(start).ToList();
        var seqs = after.Select(s => s.item.Seq).ToList();
        Assert.Equal(seqs.OrderBy(x => x), seqs);             // run in the order posted (numbered at enqueue)
        int sendAt = after.FindIndex(s => s.item.Kind == "send");
        Assert.True(sendAt >= 3, $"expected the backlog of updates before the send, got {sendAt}");
        Assert.All(after.Take(sendAt), s => Assert.Equal("tick", s.item.Kind));   // back to back
        Assert.Contains(after.Skip(sendAt + 1), s => s.item.Kind == "tick");
        Assert.All(after, s => Assert.Equal(t.Executor.ThreadId, s.thread));
        Assert.NotEqual(Environment.CurrentManagedThreadId, t.Executor.ThreadId);
        Assert.Equal(pendingBefore + 1, c.PendingCount);
        Assert.Equal(Data, c.Pending[^1].Payload);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-020 R36 (0x00836B42..0x00836B5C): in sync mode QueueMessage calls SendMessage
    /// directly and no timer exists, so the send has been queued when the call returns, on the caller's
    /// thread. This stack's sync mode is the offline / manual-pump seam; production is async (B15).
    /// </summary>
    [Fact]
    public void M1_020_R36_InSyncModeASendIsQueuedOnTheCallersThreadAtOnce()
    {
        var (t, clk, _) = Offline();
        Connect(t);
        Assert.Null(t.Scheduler);                            // R36: no timer in sync mode
        int before = t.Connection!.PendingCount;
        clk.NowMs = 1001;                                    // inside the 2 ms separation: stays queued
        t.SendData(Data, reliable: true, flush: false);
        Assert.Equal(before + 1, t.Connection.PendingCount);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-035 R37 (QueueAction 0x00836FF6 posts to the same queue) and B21
    /// (RT::Disconnect queues an action, closure 0x00837FFA) and CA16: QueueAction 0x00836FAC..0x00836FF6 never
    /// reads the sync-mode flag +0xA0, and ChangeSyncMode(true) only drops the callback handle
    /// (0x00836862..0x0083687C), so the queue survives. In sync mode too, a Disconnect is
    /// posted: with the executor held busy nothing has happened when Disconnect returns, and the connection
    /// is deleted once the executor runs it.
    /// </summary>
    [Fact]
    public void M1_035_R37_InSyncModeTooQueueActionPostsToTheExecutor()
    {
        var (t, _, _) = Offline();
        Connect(t);
        var gate = new ManualResetEventSlim(); var busy = new ManualResetEventSlim();
        t.Executor.Post(() => { busy.Set(); gate.Wait(TimeSpan.FromSeconds(10)); });
        Assert.True(busy.Wait(TimeSpan.FromSeconds(5)));
        t.Disconnect("queued");
        Assert.NotNull(t.Connection);                        // not run on the caller's thread
        Assert.Equal(LinkState.Connected, t.State);
        gate.Set();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        Assert.Null(t.Connection);
        Assert.Equal(LinkState.Disconnected, t.State);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-035 / M1-019 B21 (RCM::Connect calls RT->Disconnect(addr) 0x0062F576; RT::Disconnect
    /// queues an action through QueueAction 0x0083719A; closure 0x00837FFA: SendMessage(1, addr, null, 0, type 3,
    /// 1), then DeleteConnection) with R13 / G2.1 (SendMessage finds the connection by address when it runs): the
    /// action carries the address it was called with and acts on whatever connection that address has when it
    /// runs. Here the peer's own DisconnectRequest deletes the first connection (R12) and a new Connect makes a
    /// second one at the same address before the action runs; the action sends its type 3 on the second and
    /// deletes it. A Disconnect made before any address was named has no address and does nothing.
    /// Replaces the earlier test in which a Disconnect posted before any Connect ended the later connection:
    /// B21 passes the address as the call's argument.
    /// </summary>
    [Fact]
    public void M1_035_B21_AQueuedDisconnectActsOnTheConnectionItsAddressHasWhenItRuns()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        var reasons = new List<string>(); t.Disconnected += r => { lock (reasons) reasons.Add(r); };
        var gate = new ManualResetEventSlim(); var busy = new ManualResetEventSlim();
        t.Executor.Post(() => { busy.Set(); gate.Wait(TimeSpan.FromSeconds(10)); });
        Assert.True(busy.Wait(TimeSpan.FromSeconds(5)));
        t.Disconnect("before any address");                 // no address yet: nothing to disconnect
        t.Connect(IPAddress.Loopback, 59980);               // sync mode: connection A is made here (R36, G2.1)
        var a = t.Connection!;
        t.Disconnect("posted first");                       // carries the peer's address
        t.ProcessIncoming(Raw(ReliableMessageType.DisconnectRequest, 1, 1, 1, Array.Empty<byte>()));   // R12: A deleted
        Assert.Null(t.Connection);
        clk.NowMs = 1010;
        t.Connect(IPAddress.Loopback, 59980);               // connection B, same address
        var b = t.Connection!;
        Assert.NotSame(a, b);
        Assert.Equal(LinkState.Connecting, t.State);
        gate.Set();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        Assert.Null(t.Connection);                          // DeleteConnection on B
        Assert.Contains(b.Pending, p => p.Type == ReliableMessageType.DisconnectRequest);   // the type 3 went on B
        Assert.DoesNotContain(a.Pending, p => p.Type == ReliableMessageType.DisconnectRequest);
        Assert.Equal(LinkState.Disconnected, t.State);
        lock (reasons) Assert.Equal(new[] { "peer sent DisconnectRequest", "posted first" }, reasons);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-005 evidence / G2.4 GetCurrentNetTimeStamp 0x008355F8: the elapsed nanoseconds
    /// are divided by 0x3E8 as an integer (0x00835644), and the whole microseconds are multiplied by the double
    /// at 0x00835660, 0x3F50624DD2F1A9FC = 0.001 (0x0083564C). Anything under a microsecond is dropped.
    /// </summary>
    [Fact]
    public void M1_005_G2_4_TheClockIsWholeMicrosecondsTimes0_001()
    {
        double k = BitConverter.Int64BitsToDouble(0x3F50624DD2F1A9FC);
        Assert.Equal(0.001, k);
        const long ns = 1_000_000_000;                       // a nanosecond tick
        Assert.Equal(0.0, NetTimeStamp.FromElapsedTicks(999, ns));
        Assert.Equal(1 * k, NetTimeStamp.FromElapsedTicks(1_999, ns));
        Assert.Equal(1_234_567 * k, NetTimeStamp.FromElapsedTicks(1_234_567_999, ns));
        const long hundredNs = 10_000_000;                   // the usual Windows Stopwatch tick
        Assert.Equal(1_234_567 * k, NetTimeStamp.FromElapsedTicks(12_345_679, hundredNs));
        Assert.Equal(0.0, NetTimeStamp.FromElapsedTicks(9, hundredNs));
        Assert.Equal(86_400_000_000L * k, NetTimeStamp.FromElapsedTicks(864_000_000_000, hundredNs));   // a day, no overflow
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-005 evidence / G2.4 (0x0083561A/0x00835628): the epoch is one static, taken at
    /// the first call, so every clock in the process reads the same time; a clock made later does not start
    /// again from zero. The live reading is whole microseconds. The transport's default clock is this one.
    /// </summary>
    [Fact]
    public void M1_005_G2_4_EveryClockReadsOneProcessWideEpoch()
    {
        var first = new StopwatchClock();
        double a1 = first.NowMs;
        Thread.Sleep(30);
        var later = new StopwatchClock();
        double b = later.NowMs, a2 = first.NowMs;
        Assert.True(b >= a1 + 25, $"a clock made later restarted its epoch: {b} vs {a1}");
        Assert.True(a1 <= b && b <= a2, $"{a1} <= {b} <= {a2}");
        double us = b * 1000;
        Assert.True(Math.Abs(us - Math.Round(us)) < 1e-3, $"{b} is not whole microseconds");

        double before = NetTimeStamp.NowMs;
        var t = ReliableTransport.CreateOffline();            // default clock; the connection stamps lastRecv (G2.3)
        double after = NetTimeStamp.NowMs;
        Assert.InRange(t.Connection!.LatestRecvMs, before, after);
    }

    /// <summary>
    /// COMPATIBILITY_POLICY M1-034 (operator decision D6). The original has no handler isolation; this stack
    /// isolates each subscriber (comparison, policies table: RobotLink.OnData raised State and Message as plain
    /// multicasts). A State subscriber that throws neither stops the State subscriber after it nor keeps
    /// Message from being raised, and the fault is counted and reported.
    /// </summary>
    [Fact]
    public void M1_034_RobotLinkIsolatesEachStateAndMessageSubscriber()
    {
        var (t, _, _) = Offline();
        var link = new RobotLink(t);
        var states = new List<RobotState>(); var messages = new List<RobotMessage>(); var faults = new List<Exception>();
        link.State += _ => throw new InvalidOperationException("broken state handler");
        link.State += states.Add;
        link.Message += _ => throw new InvalidOperationException("broken message handler");
        link.Message += messages.Add;
        link.HandlerFaulted += faults.Add;
        Connect(t);

        t.ProcessIncoming(Raw(ReliableMessageType.SingleReliableMessage, 2, 2, 1, new RobotState().ToBytes()));
        Assert.Single(states);
        Assert.IsType<RobotState>(Assert.Single(messages));
        Assert.Equal(2, link.HandlerFaults);
        Assert.Equal(2, faults.Count);
        Assert.Equal(0, t.HandlerFaults);                     // nothing escaped into the transport
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
            var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true) { ReceiveHook = net.Receive };
            t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
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
    /// T-k1 — PRIMARY-SOURCE ORACLE, repaired for correction C1. M1-022 B16: on ENOTCONN (0x0083AB22 cmp r0,#0x6b)
    /// UDPTransport::TryToReadMessage first gives the "ReadFailed" warning every non-EAGAIN error gets
    /// (0x0083AAC4, 0x0083AAF6), then CloseSocket (0x0083AB28) and, since the close succeeded (0x0083AB2C cbz r0),
    /// OpenSocket on +0x98 (0x0083AB2E, 0x0083AB34), which CloseSocket has just set to 0xBAC9 = 47817 on both of
    /// its paths (B38, 0x00839694..0x0083969C): the new socket is bound to INADDR_ANY:47817, not an ephemeral
    /// port. The read returned −1, so the loop stops for this update (0x0083AA98): a datagram waiting behind the
    /// error is read by the next update, on the new socket. The connection and its reliable state are kept.
    /// Host mapping: ENOTCONN is SocketError.NotConnected.
    /// </summary>
    [Fact]
    public void T_k1_NotConnectedReopensTheSocketOnPort47817AndKeepsTheConnection()
    {
        var (t, clk, net) = Connected(59984);
        using var _t = t;                                   // disposed even if an assertion fails: frees 47817
        var warnings = new List<string>(); t.Warning += warnings.Add;
        var delivered = new List<byte[]>(); t.DataReceived += delivered.Add;
        clk.NowMs = 1040; t.Send(new SyncTime(0));
        var c = t.Connection!;
        ushort next = c.NextOutSeq, lastIn = c.LastInAcked; int pending = c.PendingCount;
        Assert.True(pending > 0);
        var old = t.CurrentSocket!;

        net.Error(SocketError.NotConnected);
        net.Datagram(Raw(ReliableMessageType.SingleUnreliableMessage, 0, 0, 1, Data), t.Peer!);
        clk.NowMs = 1041; t.Pump();
        Assert.Empty(delivered);                            // B16: the loop stopped at the error
        var fresh = t.CurrentSocket!;
        Assert.NotSame(old, fresh);
        Assert.True(old.SafeHandle.IsClosed);
        var local = Assert.IsType<IPEndPoint>(fresh.LocalEndPoint);
        Assert.Equal(IPAddress.Any, local.Address);
        Assert.Equal(47817, local.Port);                    // C1 / B38: 0xBAC9
        Assert.Equal(47817, t.StoredLocalPort);

        clk.NowMs = 1043; t.Pump();                         // the next read is on the new socket
        Assert.Same(fresh, net.SocketsSeen[^1]);
        Assert.Equal(Data, Assert.Single(delivered));

        Assert.Equal(LinkState.Connected, t.State);
        Assert.Same(c, t.Connection);
        Assert.Equal(next, c.NextOutSeq);
        Assert.Equal(lastIn, c.LastInAcked);
        Assert.Equal(pending, c.PendingCount);
        var w = Assert.Single(warnings);
        Assert.Contains("ReadFailed", w);
        Assert.Contains("NotConnected", w);
        t.Dispose();
    }

    /// <summary>
    /// T-k2 — PRIMARY-SOURCE ORACLE. M1-022 B16: any other receive error is the "ReadFailed" warning
    /// (0x0083AAC4, 0x0083AAF6) and nothing more: the socket is kept and the link stays Connected with its
    /// reliable state. The read returned −1, so the loop stops for this update (0x0083AA98). The input error is
    /// NetworkDown, a host error that is neither EAGAIN nor ENOTCONN (ConnectionReset is policy M1-039, see
    /// <see cref="M1_039_OnWindowsAConnectionResetReceiveIsNoDataAndTheDrainGoesOn"/>).
    /// </summary>
    [Fact]
    public void T_k2_AnyOtherReceiveErrorIsOnlyAWarning()
    {
        var (t, clk, net) = Connected(59985);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        clk.NowMs = 1040; t.Send(new SyncTime(0));
        var c = t.Connection!;
        ushort next = c.NextOutSeq, lastIn = c.LastInAcked; int pending = c.PendingCount;

        var sock = t.CurrentSocket;
        var delivered = new List<byte[]>(); t.DataReceived += delivered.Add;
        net.Error(SocketError.NetworkDown);
        net.Datagram(Raw(ReliableMessageType.SingleUnreliableMessage, 0, 0, 1, Data), t.Peer!);
        clk.NowMs = 1041; t.Pump();
        Assert.Contains("ReadFailed", Assert.Single(warnings));
        Assert.Empty(delivered);                            // B16: the loop stopped at the error
        clk.NowMs = 1043; t.Pump();
        Assert.Equal(Data, Assert.Single(delivered));       // read by the next update
        Assert.Same(sock, t.CurrentSocket);
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

    /// <summary>
    /// POLICY TEST (M1-039, operator 2026-09-24), not a source oracle. On Windows a UDP receive that fails with
    /// ConnectionReset (Winsock's report of an ICMP port-unreachable) is no data for that receive attempt: no
    /// warning, and the drain goes on, so the datagram behind it is read in the same update. The socket, the
    /// link and its reliable state are untouched. Off Windows the policy does not apply and the error keeps its
    /// B16 handling: "ReadFailed", and the loop stops for this update (0x0083AAC4, 0x0083AAF6, 0x0083AA98).
    /// </summary>
    [Fact]
    public void M1_039_OnWindowsAConnectionResetReceiveIsNoDataAndTheDrainGoesOn()
    {
        var (t, clk, net) = Connected(59979);
        using var _t = t;
        var warnings = new List<string>(); t.Warning += warnings.Add;
        var delivered = new List<byte[]>(); t.DataReceived += delivered.Add;
        var sock = t.CurrentSocket;
        var c = t.Connection!;
        net.Error(SocketError.ConnectionReset);
        net.Datagram(Raw(ReliableMessageType.SingleUnreliableMessage, 0, 0, 1, Data), t.Peer!);
        net.Error(SocketError.ConnectionReset);
        clk.NowMs = 1041; t.Pump();
        if (OperatingSystem.IsWindows())
        {
            Assert.Empty(warnings);
            Assert.Equal(Data, Assert.Single(delivered));   // read after the first error, in the same update
            Assert.Equal(0, net.Pending);                   // and the drain went on past the second
        }
        else
        {
            Assert.Contains("ReadFailed", Assert.Single(warnings));
            Assert.Empty(delivered);
        }
        Assert.Same(sock, t.CurrentSocket);
        Assert.Same(c, t.Connection);
        Assert.Equal(LinkState.Connected, t.State);
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
    /// PRIMARY-SOURCE ORACLE. M1-002 B17 / CA5 order: the prefix check (0x0083A80E) comes before the truncation
    /// test (0x0083A888), so an oversize datagram with a bad prefix takes the BadPrefix path (CA4: the warning
    /// "UDPTransport.BadPrefix", AddRecvError(2)), not Recv.Truncated, and is not counted as AddRecvError(1). The
    /// read still goes on.
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
        Assert.Equal(1, t.UdpReceiveErrors[2]);             // CA4: AddRecvError(2)
        Assert.Contains(warnings, w => w.StartsWith("UDPTransport.BadPrefix:"));
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
        var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        var delivered = new List<byte[]>(); t.DataReceived += delivered.Add;
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
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

    // ================================================================ connection lifetime

    private static List<ReceiverEvent> Receiver(ReliableTransport t)
    {
        var events = new List<ReceiverEvent>();
        t.Received += e => { lock (events) events.Add(e); };
        return events;
    }

    private static List<T> Snap<T>(List<T> list) { lock (list) return list.ToList(); }

    private static int Ticks(List<(ReliableTransport.ExecutorItem item, int thread)> seen)
    {
        lock (seen) return seen.Count(s => s.item.Kind == "tick");
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-003 R12 (tbb 0x00837446, table 0x0083744A; type 3 → 0x008374A0 OnDisconnected, then
    /// DeleteConnection veneer 0x008D123C) with R40 (ReceiveData(OnDisconnected 0x0103759C, 0, addr), 0x00837478..
    /// 0x00837496): a handled DisconnectRequest from the peer raises OnDisconnected with the peer's address and
    /// deletes that connection only. A second connection, made by a type 1 from another address (M1-018 R13,
    /// 0x008377CE..0x008377FC), stays; the socket stays open; and the next ReliableTransport::Update (R34,
    /// 0x00837B9A..0x00837C92) still drains the socket and updates the remaining connection.
    /// </summary>
    [Fact]
    public void M1_003_R12_AHandledDisconnectRequestDeletesOnlyThatConnectionAndTheTransportGoesOn()
    {
        var (t, clk, net) = Connected(59970);
        var events = Receiver(t);
        var reasons = new List<string>(); t.Disconnected += reasons.Add;
        var peer = t.Peer!;
        var other = new IPEndPoint(IPAddress.Loopback, 59971);
        t.ProcessIncoming(Raw(ReliableMessageType.ConnectionRequest, 1, 1, 0, Array.Empty<byte>()), other);
        var oc = t.ConnectionFor(other);
        Assert.NotNull(oc);
        var local = t.LocalEndPoint;
        events.Clear();

        t.ProcessIncoming(Raw(ReliableMessageType.DisconnectRequest, 2, 2, 1, Array.Empty<byte>()));   // from the peer, seq 2 = nextIn

        Assert.Null(t.ConnectionFor(peer));                                   // R12: DeleteConnection
        Assert.Same(oc, t.ConnectionFor(other));                              // only that one
        Assert.Equal(new[] { other }, t.ConnectionAddresses);
        var e = Assert.Single(events);                                        // R40
        Assert.Equal(ReceiverMarker.OnDisconnected, e.Marker);
        Assert.Equal(peer, e.Address);
        Assert.NotNull(t.LocalEndPoint);                                      // the socket is kept
        Assert.Equal(local, t.LocalEndPoint);
        Assert.Equal(LinkState.Disconnected, t.State);                        // the facade's link to the peer ended
        Assert.Equal(new[] { "peer sent DisconnectRequest" }, reasons);

        clk.NowMs = 1100;                                                     // R34: the next update still drains and updates
        net.Datagram(Raw(ReliableMessageType.Ping, 0, 0, 0, new PingPayload(1, 1, 0, false).ToBytes()), other);
        Assert.True(t.Pump());
        Assert.Equal(1100, oc!.LatestRecvMs);                                 // R18: the frame was processed for it
        Assert.Equal(1u, oc.NumPingsReceived);                                // R12: type 11 → ReceivePing
        t.Dispose();
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-003 R12 (type 3 → OnDisconnected, DeleteConnection 0x008374A0 / 0x008D123C) on the
    /// production (async) path, M1-010 R35 / B14 (the 2 ms update is requested once at construction, 0x00836896,
    /// 0x0083689E) and M1-019 R39 (only Stop closes the socket, 0x008380F6): over a real loopback socket, the
    /// robot's DisconnectRequest is read by the update, the peer's connection is deleted, and the update goes on
    /// being run with the socket still open. Nothing sets the +0xA1 flag (R35: only a false Update does, B36).
    /// </summary>
    [Fact]
    public void M1_003_R12_AfterTheRobotsDisconnectRequestTheAsyncUpdateAndSocketGoOn()
    {
        using var robot = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        robot.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk);
        var seen = new List<(ReliableTransport.ExecutorItem item, int thread)>();
        Traced(t, seen);
        var events = Receiver(t);
        t.Start();
        t.Connect(IPAddress.Loopback, ((IPEndPoint)robot.LocalEndPoint!).Port);
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        var local = new IPEndPoint(IPAddress.Loopback, ((IPEndPoint)t.LocalEndPoint!).Port);

        robot.SendTo(ConnectionResponse(), local);
        Assert.True(SpinWait.SpinUntil(() => t.State == LinkState.Connected, 5000), "the ConnectionResponse was never read");
        robot.SendTo(Raw(ReliableMessageType.DisconnectRequest, 2, 2, 1, Array.Empty<byte>()), local);
        Assert.True(SpinWait.SpinUntil(() => t.Connection is null, 5000), "the DisconnectRequest was never handled");

        Assert.True(SpinWait.SpinUntil(() => Snap(events).Any(e => e.Marker == ReceiverMarker.OnDisconnected), 5000));
        Assert.Equal(robot.LocalEndPoint, Snap(events).Single(e => e.Marker == ReceiverMarker.OnDisconnected).Address);
        Assert.Equal(LinkState.Disconnected, t.State);
        Assert.NotNull(t.LocalEndPoint);                                      // the socket is kept
        int ticks = Ticks(seen);
        Assert.True(SpinWait.SpinUntil(() => Ticks(seen) >= ticks + 5, 5000), "the update stopped");
        Assert.False(t.TimedOut);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-015 R19 (0x00837BCC..0x00837C74: warning; ReceiveData(OnDisconnected, 0, addr);
    /// the connection deleted; ReliableTransport::Update returns false; no frame is sent), B22 ("Disconnecting
    /// TimedOut Connection" 0x00837BCC; OnDisconnected 0x00837C50..0x00837C56; delete 0x00837C60; the lambda sets
    /// +0xA1 0x008383D4..0x008383DC), B36 (+0xA1 cleared by the ctor and Connect 0x008367DA / 0x0083710E);
    /// M1-032 G2.3 (lastRecv = the ctor's time 0x0083593C), G2.7 (now &gt; +0x50 + 5000.0, strictly) and G2.8 (every
    /// Update checks it); M1-019 R38 (Connect clears +0xA1). On the production (async) path: at exactly 5000 ms
    /// nothing happens; past it the warning, OnDisconnected with the address, the deletion and the flag, but no
    /// DisconnectRequest and no frame afterwards; the update and the socket go on; the next Connect clears the
    /// flag and makes a new connection.
    /// </summary>
    [Fact]
    public void M1_015_R19_B22_ATimeoutDeletesTheConnectionSetsTheFlagAndTheTransportGoesOn()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk);
        var seen = new List<(ReliableTransport.ExecutorItem item, int thread)>();
        Traced(t, seen);
        var events = Receiver(t);
        var warnings = new List<string>(); t.Warning += w => { lock (warnings) warnings.Add(w); };
        var reasons = new List<string>(); t.Disconnected += r => { lock (reasons) reasons.Add(r); };
        var outbound = new List<Frame>(); t.FrameTrace += f => { if (f.Outbound && f.Frame is not null) lock (outbound) outbound.Add(f.Frame); };
        Assert.False(t.TimedOut);                                             // B36: cleared by the ctor
        t.Start();
        t.Connect(IPAddress.Loopback, 59968);
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        var peer = t.Peer!;
        Assert.Equal(1000, t.Connection!.LatestRecvMs);                       // G2.3

        clk.NowMs = 1000 + 5000.0;                                            // G2.7: not past it yet
        int t0 = Ticks(seen);
        Assert.True(SpinWait.SpinUntil(() => Ticks(seen) >= t0 + 5, 5000));
        Assert.NotNull(t.Connection);
        Assert.False(t.TimedOut);

        clk.NowMs = 1000 + 5000.1;
        Assert.True(SpinWait.SpinUntil(() => t.TimedOut, 5000), "the +0xA1 flag was never set");
        Assert.True(SpinWait.SpinUntil(() => Snap(reasons).Count == 1, 5000));
        Assert.Null(t.Connection);                                            // deleted
        Assert.Empty(t.ConnectionAddresses);
        Assert.Contains(Snap(warnings), w => w.Contains("Disconnecting TimedOut Connection"));
        var e = Assert.Single(Snap(events), x => x.Marker == ReceiverMarker.OnDisconnected);
        Assert.Equal(peer, e.Address);
        Assert.Equal(LinkState.Disconnected, t.State);
        Assert.DoesNotContain(Snap(outbound), f => f.Messages.Any(m => m.Type == ReliableMessageType.DisconnectRequest));
        Assert.NotNull(t.LocalEndPoint);                                      // the socket is kept

        int frames = Snap(outbound).Count, t1 = Ticks(seen);
        clk.NowMs = 7000;
        Assert.True(SpinWait.SpinUntil(() => Ticks(seen) >= t1 + 5, 5000), "the update stopped after the timeout");
        Assert.Equal(frames, Snap(outbound).Count);                           // nothing sent for the deleted connection
        Assert.True(t.TimedOut);                                              // only Connect clears it

        clk.NowMs = 7100;
        t.Connect(IPAddress.Loopback, 59968);
        Assert.False(t.TimedOut);                                             // R38: cleared by Connect itself
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        Assert.Equal(7100, t.Connection!.LatestRecvMs);                       // G2.1 / G2.3: a new connection
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-015 R19 / G2.8 on the sync path, where the owner runs ReliableTransport::Update
    /// itself (R36, B15): the timed-out connection is deleted with the warning and OnDisconnected, and the update
    /// returns false. B36: the +0xA1 flag is set only by the tick lambda (0x008383DC), so a sync-mode update does
    /// not set it.
    /// </summary>
    [Fact]
    public void M1_015_R19_InSyncModeTheUpdateReportsTheTimeoutButOnlyTheTickSetsTheFlag()
    {
        var (t, clk, _) = Connected(59967);
        var events = Receiver(t);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        var peer = t.Peer!;
        clk.NowMs = t.Connection!.LatestRecvMs + 5000.1;
        Assert.False(t.Pump());
        Assert.Null(t.Connection);
        Assert.Equal(peer, Assert.Single(events, e => e.Marker == ReceiverMarker.OnDisconnected).Address);
        Assert.Contains(warnings, w => w.Contains("Disconnecting TimedOut Connection"));
        Assert.False(t.TimedOut);
        Assert.True(t.Pump());                                                // nothing left to time out
        Assert.NotNull(t.LocalEndPoint);
        t.Dispose();
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-018 R13 receive side (0x008377CE..0x008377FC; FindConnection 0x00837098 cmp r5,#1),
    /// B19, M1-032 G2.10 (0x008377D6, 0x008377EA..0x008377F2): a type-1 frame from an address with no connection
    /// creates one (lastRecv stamped by the ctor, G2.3) and is then processed as for a known connection: R16
    /// acks seqMax (0x00837848) and R12 accepts seq 1 and raises OnConnectRequest with the address (R40,
    /// 0x01037598). The facade link is untouched (B20: the app layer drops the marker).
    /// </summary>
    [Fact]
    public void M1_018_R13_ATypeOneFrameFromAnUnknownAddressCreatesAConnection()
    {
        var clk = new ManualClock { NowMs = 2000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        var events = Receiver(t);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        var from = new IPEndPoint(IPAddress.Parse("10.1.2.3"), 5551);

        t.ProcessIncoming(Raw(ReliableMessageType.ConnectionRequest, 1, 1, 0, Array.Empty<byte>()), from);

        var c = t.ConnectionFor(from);
        Assert.NotNull(c);
        Assert.Equal(2000, c!.LatestRecvMs);
        Assert.Equal(1, c.LastInAcked);                                       // R16
        Assert.Equal(2, c.NextInSeq);                                         // R12
        var e = Assert.Single(events);
        Assert.Equal(ReceiverMarker.OnConnectRequest, e.Marker);
        Assert.Equal(from, e.Address);
        Assert.Equal(LinkState.Idle, t.State);
        Assert.Empty(warnings);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-018 R13 / B19 ("a multi-message with first sub type 1"): a container whose first
    /// sub-message is type 1 creates the connection too, and its walk goes on as for a known connection (R9,
    /// R11: the always-unreliable type 5 after it takes seq 0 and is delivered to the receiver, R40).
    /// </summary>
    [Fact]
    public void M1_018_R13_AContainerWhoseFirstSubIsTypeOneCreatesAConnection()
    {
        var clk = new ManualClock { NowMs = 2000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        var events = Receiver(t);
        var from = new IPEndPoint(IPAddress.Parse("10.1.2.4"), 5551);

        t.ProcessIncoming(Raw(ReliableMessageType.MultipleMixedMessages, 1, 1, 0, Body(
            Sub(ReliableMessageType.ConnectionRequest, Array.Empty<byte>()),
            Sub(ReliableMessageType.SingleUnreliableMessage, Data))), from);

        Assert.NotNull(t.ConnectionFor(from));
        Assert.Equal(2, events.Count);
        Assert.Equal(ReceiverMarker.OnConnectRequest, events[0].Marker);
        Assert.Equal(from, events[0].Address);
        Assert.Equal(ReceiverMarker.Data, events[1].Marker);
        Assert.Equal(from, events[1].Address);
        Assert.Equal(Data, events[1].Data);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-018 R13 (0x0083789A) / B19: any other frame from an address with no connection
    /// is logged "unconnected source" and dropped: no connection, no receiver event, ack not processed. Here a
    /// single reliable message, a ConnectionResponse, and a container whose first sub-message is not type 1
    /// although a later one is.
    /// </summary>
    [Fact]
    public void M1_018_R13_OtherFramesFromAnUnknownAddressAreDroppedAsUnconnectedSource()
    {
        var clk = new ManualClock { NowMs = 2000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        var events = Receiver(t);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        var from = new IPEndPoint(IPAddress.Parse("10.1.2.5"), 5551);

        t.ProcessIncoming(Raw(ReliableMessageType.SingleReliableMessage, 1, 1, 0, Data), from);
        t.ProcessIncoming(Raw(ReliableMessageType.ConnectionResponse, 1, 1, 0, Array.Empty<byte>()), from);
        t.ProcessIncoming(Raw(ReliableMessageType.MultipleReliableMessages, 1, 2, 0, Body(
            Sub(ReliableMessageType.SingleReliableMessage, Data),
            Sub(ReliableMessageType.ConnectionRequest, Array.Empty<byte>()))), from);

        Assert.Null(t.ConnectionFor(from));
        Assert.Empty(t.ConnectionAddresses);
        Assert.Empty(events);
        Assert.Equal(3, warnings.Count(w => w.Contains("unconnected source")));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-019 R40 (ReceiveData(OnConnected 0x01037594, 0, addr), 0x00837478..0x00837496) and
    /// R12 (type 2 → OnConnected): the peer's ConnectionResponse reaches the receiver as OnConnected with the
    /// peer's address; the facade then reports the link Connected.
    /// </summary>
    [Fact]
    public void M1_019_R40_TheConnectionResponseReachesTheReceiverAsOnConnected()
    {
        var (t, _, _) = Offline();
        var events = Receiver(t);
        t.OfflineConnect();
        t.OfflineAcceptConnection();
        var e = Assert.Single(events);
        Assert.Equal(ReceiverMarker.OnConnected, e.Marker);
        Assert.Equal(t.Peer, e.Address);
        Assert.Equal(LinkState.Connected, t.State);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-019 R38 FinishConnection 0x0083712E: reliable type 2, flag 1, to the address; with
    /// R13 / G2.1 (only a type 1 creates a connection) it goes on the connection an inbound type 1 made. Checked on
    /// the wire over a real loopback socket: the datagram reaching that address is COZ + RE header type 2, seq
    /// 1..1, ack 1 (R1; R2: that connection's ackOut, set to the type 1's seq by R16), and the pending entry is
    /// reliable and flushed (R20 +0x2B).
    /// </summary>
    [Fact]
    public void M1_019_R38_FinishConnectionSendsAReliableFlushedTypeTwoToThatAddress()
    {
        using var robot = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        robot.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        robot.ReceiveTimeout = 3000;
        var robotEp = (IPEndPoint)robot.LocalEndPoint!;
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        t.ProcessIncoming(Raw(ReliableMessageType.ConnectionRequest, 1, 1, 0, Array.Empty<byte>()), robotEp);

        clk.NowMs = 1010;
        t.FinishConnection(robotEp);

        var p = Assert.Single(t.ConnectionFor(robotEp)!.Pending);
        Assert.Equal(ReliableMessageType.ConnectionResponse, p.Type);
        Assert.True(p.IsReliable);
        Assert.Equal(1, p.Seq);
        Assert.True(p.FlushPacket);

        var buf = new byte[2048]; EndPoint src = new IPEndPoint(IPAddress.Any, 0);
        int n = robot.ReceiveFrom(buf, ref src);
        Assert.True(FrameCodec.TryDecode(buf.AsSpan(0, n).ToArray(), out var f, out var err), err);
        Assert.Equal(ReliableMessageType.ConnectionResponse, f!.Type);
        Assert.Equal(1, f.SeqMin);
        Assert.Equal(1, f.SeqMax);
        Assert.Equal(1, f.Ack);
        Assert.Equal(((IPEndPoint)t.LocalEndPoint!).Port, ((IPEndPoint)src).Port);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-018 R13 send side (SendMessage 0x00836C5A..0x00836C6E): a FinishConnection to an
    /// address with no connection creates none, queues nothing and sends nothing; the warning is "unconnected
    /// destination".
    /// </summary>
    [Fact]
    public void M1_019_R38_FinishConnectionWithNoConnectionIsAnUnconnectedDestination()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        var frames = new List<FrameEvent>(); t.FrameTrace += frames.Add;
        var to = new IPEndPoint(IPAddress.Loopback, 59964);
        t.FinishConnection(to);
        Assert.Null(t.ConnectionFor(to));
        Assert.Empty(frames);
        Assert.Contains(warnings, w => w.Contains("unconnected destination"));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-019 R39 (Stop → UDP Stop*, then ClearConnections 0x008380F6, veneer 0x008D122C,
    /// 0x00837374; no frames are sent): with a message pending and a resend due, Stop sends nothing, clears every
    /// connection and closes the socket; R39 names no receiver event and none is raised. The update still runs
    /// (R35) and has nothing to send. B12: Start then opens a socket again, since there is none
    /// (OpenSocket if fd == −1, 0x0083AD58), and after the close that is 47817 (CA21, CA30). Start and Stop are
    /// posted actions (CA20: 0x00837214, 0x008372A4 → QueueAction), so the test waits for the executor before it
    /// looks.
    /// </summary>
    [Fact]
    public void M1_019_R39_StopSendsNothingClearsTheConnectionsAndClosesTheSocket()
    {
        var (t, clk, _) = Connected(59966);
        using var _t = t;                                   // disposed even if an assertion fails: frees 47817
        clk.NowMs = 1040; t.Send(new SyncTime(0));                            // pending, sent once
        var other = new IPEndPoint(IPAddress.Loopback, 59963);
        t.ProcessIncoming(Raw(ReliableMessageType.ConnectionRequest, 1, 1, 0, Array.Empty<byte>()), other);
        var events = Receiver(t);
        var reasons = new List<string>(); t.Disconnected += reasons.Add;
        var frames = new List<FrameEvent>(); t.FrameTrace += e => { if (e.Outbound) frames.Add(e); };

        clk.NowMs = 1100;                                                     // a resend would be due
        t.Stop();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));

        Assert.Empty(frames);
        Assert.Null(t.Connection);
        Assert.Empty(t.ConnectionAddresses);
        Assert.Null(t.LocalEndPoint);
        Assert.Empty(events);
        Assert.Equal(LinkState.Disconnected, t.State);                        // the facade link ended
        Assert.Equal(new[] { "stopped" }, reasons);
        Assert.True(t.Pump());
        Assert.Empty(frames);

        t.Start();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        Assert.NotNull(t.LocalEndPoint);
        t.Dispose();
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-019 R38 (Connect only clears +0xA1 and queues the type 1, 0x0083710E / 0x0083711C;
    /// Disconnect sends type 3 then DeleteConnection, closure 0x00837FFA), R39 (Start → StartClient 0x0083808E) and
    /// B12 (StartClient → OpenSocket if fd == −1, 0x0083AD58): Connect opens no socket; Start opens one and a second
    /// Start keeps it; Disconnect deletes the connection and leaves the socket open. Start is a posted action
    /// (CA20: 0x00837214 → QueueAction), so the test waits for the executor before it looks.
    /// </summary>
    [Fact]
    public void M1_019_R38_R39_StartOpensTheSocketOnceAndConnectAndDisconnectLeaveItAlone()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        t.Connect(IPAddress.Loopback, 59965);
        Assert.Null(t.LocalEndPoint);
        Assert.NotNull(t.Connection);

        t.Start();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        var ep = t.LocalEndPoint;
        Assert.NotNull(ep);
        t.Start();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        Assert.Equal(ep, t.LocalEndPoint);

        t.Disconnect();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        Assert.Null(t.Connection);
        Assert.Equal(ep, t.LocalEndPoint);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-019 R38 (Connect queues QueueMessage(type 1, reliable, flush 1), 0x0083711C) and
    /// R39 with CA20 (RT::StopClient posts through QueueAction, 0x008372A4; closure 0x008380F6..0x00838108 → UDP
    /// vtbl+0x18, then ClearConnections) and M1-035 R37 / CA12 (one FIFO queue): on the production (async) path a Connect followed at once by Stop still sends its ConnectionRequest
    /// before the socket closes, over a real loopback socket; then the socket is closed and no connection is left.
    /// </summary>
    [Fact]
    public void M1_019_R39_AConnectFollowedByStopStillSendsTheConnectionRequestFirst()
    {
        using var robot = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        robot.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        robot.ReceiveTimeout = 3000;
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk);
        t.Start();
        t.Connect(IPAddress.Loopback, ((IPEndPoint)robot.LocalEndPoint!).Port);
        t.Stop();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));

        var buf = new byte[2048]; EndPoint src = new IPEndPoint(IPAddress.Any, 0);
        int n = robot.ReceiveFrom(buf, ref src);
        Assert.True(FrameCodec.TryDecode(buf.AsSpan(0, n).ToArray(), out var f, out var err), err);
        Assert.Equal(ReliableMessageType.ConnectionRequest, f!.Type);
        Assert.Equal(1, f.SeqMin);
        Assert.Null(t.LocalEndPoint);
        Assert.Empty(t.ConnectionAddresses);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE for the assembly rules, M1-009 R23 (AddMessagePart 0x008358A0..0x008358E8;
    /// 0x008374C6..0x00837504). MISSING for the per-connection part: that HandleSubMessage passes the connection
    /// (0x008374C6 mov r0,r5) to GetPendingMultiPartMessage (0x008374C8; 0x00835A94 adds r0,#0x20), so each
    /// connection assembles its own message, is the batch 2b-ii verifier reading; no frozen row states it, so this
    /// guards the current build only. Two connections interleaving their parts each complete their own message, delivered to the
    /// receiver with their own address (R40).
    /// </summary>
    [Fact]
    public void M1_009_R23_EachConnectionAssemblesItsOwnMultipartMessage()
    {
        var (t, _, _) = Offline();
        Connect(t);                                                           // A: the peer, nextIn 2
        var a = t.Peer!;
        var b = new IPEndPoint(IPAddress.Parse("10.0.0.9"), 5551);
        t.ProcessIncoming(Raw(ReliableMessageType.ConnectionRequest, 1, 1, 0, Array.Empty<byte>()), b);   // B, nextIn 2
        var events = Receiver(t);

        t.ProcessIncoming(Raw(ReliableMessageType.MultiPartMessage, 2, 2, 1, new byte[] { 1, 2, 0xA1 }), a);
        t.ProcessIncoming(Raw(ReliableMessageType.MultiPartMessage, 2, 2, 0, new byte[] { 1, 2, 0xB1 }), b);
        t.ProcessIncoming(Raw(ReliableMessageType.MultiPartMessage, 3, 3, 1, new byte[] { 2, 2, 0xA2 }), a);
        t.ProcessIncoming(Raw(ReliableMessageType.MultiPartMessage, 3, 3, 0, new byte[] { 2, 2, 0xB2 }), b);

        var data = events.Where(e => e.Marker == ReceiverMarker.Data).ToList();
        Assert.Equal(2, data.Count);
        Assert.Equal(a, data[0].Address);
        Assert.Equal(new byte[] { 0xA1, 0xA2 }, data[0].Data);
        Assert.Equal(b, data[1].Address);
        Assert.Equal(new byte[] { 0xB1, 0xB2 }, data[1].Data);
    }

    /// <summary>
    /// REGRESSION ONLY for the per-connection assembly (GetPendingMultiPartMessage 0x008374C8 / 0x00835A94 is the
    /// batch 2b-ii verifier reading; no frozen row states it: MISSING), with M1-003 R12 (type 3 → DeleteConnection 0x008D123C, which
    /// destroys only that connection): deleting B mid-message leaves A's partial message intact, and A's next
    /// part completes it.
    /// </summary>
    [Fact]
    public void M1_009_R23_DeletingOneConnectionLeavesAnothersPartialMessageIntact()
    {
        var (t, _, _) = Offline();
        Connect(t);
        var a = t.Peer!;
        var b = new IPEndPoint(IPAddress.Parse("10.0.0.9"), 5551);
        t.ProcessIncoming(Raw(ReliableMessageType.ConnectionRequest, 1, 1, 0, Array.Empty<byte>()), b);
        var events = Receiver(t);

        t.ProcessIncoming(Raw(ReliableMessageType.MultiPartMessage, 2, 2, 1, new byte[] { 1, 2, 0xA1 }), a);
        t.ProcessIncoming(Raw(ReliableMessageType.MultiPartMessage, 2, 2, 0, new byte[] { 1, 2, 0xB1 }), b);
        t.ProcessIncoming(Raw(ReliableMessageType.DisconnectRequest, 3, 3, 0, Array.Empty<byte>()), b);
        Assert.Null(t.ConnectionFor(b));
        t.ProcessIncoming(Raw(ReliableMessageType.MultiPartMessage, 3, 3, 1, new byte[] { 2, 2, 0xA2 }), a);

        var d = Assert.Single(events, e => e.Marker == ReceiverMarker.Data);
        Assert.Equal(a, d.Address);
        Assert.Equal(new byte[] { 0xA1, 0xA2 }, d.Data);
    }

    /// <summary>
    /// REGRESSION ONLY (the facade's own state, not a transport row). On the production (async) path a Disconnect
    /// of a Connected link and a new Connect to the same address are both made before either runs. Every protocol
    /// action still happens, in order (R37): the Disconnect closure sends its type 3 on the old connection and
    /// deletes it (B21, 0x00837FFA), and the Connect closure makes a new connection (G2.1). The old link's end
    /// must not turn the newer Connecting link to Disconnected, and the new connection's ConnectionResponse must
    /// then make it Connected.
    /// </summary>
    [Fact]
    public void FacadeAnOldLinksEndDoesNotClobberANewerConnect()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk);
        t.Connect(IPAddress.Loopback, 59962);
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        t.ProcessIncoming(ConnectionResponse());
        Assert.Equal(LinkState.Connected, t.State);
        var x = t.Connection!;

        var gate = new ManualResetEventSlim(); var busy = new ManualResetEventSlim();
        t.Executor.Post(() => { busy.Set(); gate.Wait(TimeSpan.FromSeconds(10)); });
        Assert.True(busy.Wait(TimeSpan.FromSeconds(5)));
        clk.NowMs = 1050;
        t.Disconnect("old link");
        t.Connect(IPAddress.Loopback, 59962);
        Assert.Equal(LinkState.Connecting, t.State);
        gate.Set();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));

        Assert.Contains(x.Pending, p => p.Type == ReliableMessageType.DisconnectRequest);   // not dropped
        var y = t.Connection!;
        Assert.NotSame(x, y);
        Assert.Equal(LinkState.Connecting, t.State);                         // not clobbered by the old end
        t.ProcessIncoming(ConnectionResponse());                              // seq 1 on the new connection
        Assert.Equal(LinkState.Connected, t.State);                           // not lost
    }

    // ================================================================ the UDP socket and addressing

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-022 B12: the UDP ctor stores 0xBAC9 at +0x98 (0x00839546) but
    /// RobotConnectionManager::Init stores 0 over it (0x0062EFEA str.w r5,[r6,#0x98]) before StartClient opens
    /// the socket (0x0062F07E, 0x0083808E, 0x0083AD58), so the first open binds INADDR_ANY on an ephemeral port.
    /// B38 / CA30 (0x00839694..0x0083969C): CloseSocket sets fd -1 and +0x98 = 0xBAC9 = 47817. CA21: UDP StopClient
    /// closes through CloseSocket if fd &gt;= 0 (0x0083AD66..0x0083AD70), and StartClient opens OpenSocket(+0x98) if
    /// fd == -1 (0x0083AD4C..0x0083AD5C), so a Start after Stop binds 47817.
    /// </summary>
    [Fact]
    public void M1_022_B12_TheFirstOpenIsEphemeralAndAnOpenAfterACloseBinds47817()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        Assert.Equal(0, t.StoredLocalPort);                 // B12: Init's 0
        Assert.Null(t.CurrentSocket);                       // fd -1 until StartClient
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        var first = Assert.IsType<IPEndPoint>(t.LocalEndPoint);
        Assert.Equal(IPAddress.Any, first.Address);
        Assert.NotEqual(0, first.Port);
        Assert.NotEqual(47817, first.Port);                 // ephemeral

        t.Stop(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        Assert.Null(t.CurrentSocket);                       // B38: fd -1
        Assert.Equal(47817, t.StoredLocalPort);             // B38: 0xBAC9
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        var second = Assert.IsType<IPEndPoint>(t.LocalEndPoint);
        Assert.Equal(new IPEndPoint(IPAddress.Any, 47817), second);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-022 B11: OpenSocket asks getaddrinfo for (NULL, port, AI_PASSIVE, SOCK_DGRAM,
    /// AF_INET) (0x00839ACA, 0x00839AD0, 0x00839AF2), sets SO_BROADCAST = 1 for AF_INET (0x00839D18/0x00839D1A)
    /// and binds (0x00839D3A); there is no O_NONBLOCK. So the socket is an IPv4 UDP socket on INADDR_ANY, with
    /// SO_BROADCAST set, and blocking.
    /// </summary>
    [Fact]
    public void M1_022_B11_TheSocketIsIpv4UdpOnInaddrAnyWithSoBroadcastAndBlocking()
    {
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, new ManualClock(), manualPump: true);
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        var s = t.CurrentSocket!;
        Assert.Equal(AddressFamily.InterNetwork, s.AddressFamily);
        Assert.Equal(SocketType.Dgram, s.SocketType);
        Assert.Equal(ProtocolType.Udp, s.ProtocolType);
        Assert.Equal(IPAddress.Any, ((IPEndPoint)s.LocalEndPoint!).Address);
        Assert.Equal(1, (int)s.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast)!);
        Assert.True(s.Blocking);
    }

    /// <summary>
    /// HOST MAPPING of a PRIMARY-SOURCE row. M1-022 B11 / CA29: a bind that fails with EADDRINUSE is only the
    /// warning "BindInUse" (0x00839E76..0x00839EB6), the socket stays open but unbound, and OpenSocket succeeds. On
    /// this host EADDRINUSE is SocketError.AddressAlreadyInUse: with 47817 held by another socket, the open after
    /// a close (B38) warns and keeps its (unbound) socket.
    /// </summary>
    [Fact]
    public void M1_022_B11_AnAddressInUseBindIsOnlyAWarningAndTheSocketIsKept()
    {
        using var holder = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        holder.Bind(new IPEndPoint(IPAddress.Any, 47817));
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, new ManualClock(), manualPump: true);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));   // ephemeral: no conflict
        Assert.Empty(warnings);
        t.Stop(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));   // 47817: held
        var inUse = Assert.Single(warnings);
        Assert.StartsWith("UDPTransport.BindInUse", inUse);   // CA29: a warning, not an error
        Assert.Contains("AddressAlreadyInUse", inUse);
        Assert.NotNull(t.CurrentSocket);                    // kept
        Assert.Null(t.LocalEndPoint);                       // not bound
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE, with a HOST MAPPING for the failed close. M1-022 B16: after ENOTCONN, CloseSocket is
    /// called (0x0083AB28) and OpenSocket only if it returned success (0x0083AB2C cbz r0); B38: close failures are
    /// logged and fd becomes -1 and +0x98 0xBAC9 on both paths (0x00839694..0x0083969C). With the close failing,
    /// no socket is opened, and the next update reads nothing, since the read loop runs only if fd &gt;= 0
    /// (G4.5, 0x0083AD08). Host mapping: .NET reports no closesocket failure, so the failure is simulated through
    /// the close seam.
    /// </summary>
    [Fact]
    public void M1_022_B16_ANotConnectedReopenIsSkippedWhenTheCloseFailed()
    {
        var (t, clk, net) = Connected(59961);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        t.CloseHook = s => { s.Close(); throw new SocketException((int)SocketError.NotSocket); };
        net.Error(SocketError.NotConnected);
        clk.NowMs = 1041; t.Pump();
        Assert.Null(t.CurrentSocket);                       // fd -1, not reopened
        Assert.Equal(47817, t.StoredLocalPort);             // set on the failure path too
        Assert.Contains(warnings, w => w.Contains("ReadFailed"));
        Assert.Contains(warnings, w => w.Contains("CloseSocket: close failed"));
        Assert.NotNull(t.Connection);                       // nothing torn down

        int reads = net.SocketsSeen.Count;
        net.Datagram(Raw(ReliableMessageType.SingleUnreliableMessage, 0, 0, 1, Data), t.Peer!);
        clk.NowMs = 1043; t.Pump();
        Assert.Equal(reads, net.SocketsSeen.Count);         // no socket: no read
        t.Dispose();
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-022 B16: the loop runs while TryToReadMessage returns 1 (0x0083AD10..0x0083AD18);
    /// a recvmsg result &lt;= 0 stops it (0x0083AA98), a 0-byte datagram too. The datagram behind it is read by
    /// the next update (CA35). Whether the 0-byte read warns is HARDWARE_ONLY (M1-043: it goes down the errno
    /// path with a stale errno), so no warning is asserted either way.
    /// </summary>
    [Fact]
    public void M1_022_B16_AZeroByteDatagramStopsTheDrainForThisUpdate()
    {
        var (t, clk, net) = Connected(59960);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        var delivered = new List<byte[]>(); t.DataReceived += delivered.Add;
        net.Datagram(Array.Empty<byte>(), t.Peer!);
        net.Datagram(Raw(ReliableMessageType.SingleUnreliableMessage, 0, 0, 1, Data), t.Peer!);
        clk.NowMs = 1041; t.Pump();
        Assert.Empty(delivered);                            // the loop stopped; whether it warns is M1-043
        clk.NowMs = 1043; t.Pump();
        Assert.Equal(Data, Assert.Single(delivered));
        t.Dispose();
    }

    /// <summary>
    /// HOST MAPPING of M1-022 B16 on the production read (no hook): recvmsg(MSG_DONTWAIT) on the blocking socket
    /// is realised as a zero-timeout poll then a read, over a real loopback socket. A 0-byte datagram stops the
    /// drain for the update (0x0083AA98); the datagram behind it is read by the next update; with nothing waiting
    /// the read reports EAGAIN and does not block. (No warning is asserted: whether the 0-byte read warns is
    /// HARDWARE_ONLY, M1-043.)
    /// </summary>
    [Fact]
    public void M1_022_B16_OnTheHostAZeroByteDatagramStopsTheDrainAndAnEmptyReadDoesNotBlock()
    {
        using var robot = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        robot.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        var delivered = new List<byte[]>(); t.DataReceived += delivered.Add;
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        t.Connect(IPAddress.Loopback, ((IPEndPoint)robot.LocalEndPoint!).Port);
        var local = new IPEndPoint(IPAddress.Loopback, ((IPEndPoint)t.LocalEndPoint!).Port);
        robot.SendTo(ConnectionResponse(), local);
        Thread.Sleep(100);
        clk.NowMs = 1001; t.Pump();
        Assert.Equal(LinkState.Connected, t.State);

        robot.SendTo(Array.Empty<byte>(), local);
        robot.SendTo(Raw(ReliableMessageType.SingleReliableMessage, 2, 2, 1, Data), local);
        Thread.Sleep(100);
        clk.NowMs = 1041; t.Pump();
        Assert.Empty(delivered);
        clk.NowMs = 1043;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        t.Pump();
        Assert.Equal(Data, Assert.Single(delivered));
        t.Pump();                                           // nothing waiting: EAGAIN, silent, no block
        Assert.True(sw.ElapsedMilliseconds < 1000, $"a read blocked for {sw.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-022 B10 / CA31: sendto with flags 0 (0x0083A37C); a send that returns fewer bytes
    /// than asked is the error log "SentWrongNumBytes" (0x0083A38A..0x0083A3DA). It is not a failure: no
    /// AddSendError.
    /// </summary>
    [Fact]
    public void M1_022_B10_AShortSendLogsSentWrongNumBytes()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        t.SendHook = (_, datagram, _) => datagram.Length - 1;
        t.Connect(IPAddress.Loopback, 59959);
        Assert.Contains(warnings, w => w.StartsWith(ReliableTransport.ErrorLevel + "UDPTransport.SentWrongNumBytes"));
        Assert.Equal(0, t.UdpSendErrors[6]);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-022 B10: a failed sendto counts AddSendError(6) (0x0083A552); there is no retry
    /// and no disconnect. The one attempt is made, the link and its connection stay, and the message stays
    /// pending for the ordinary resend rules. The rate-limited warning and +0x88 are
    /// <see cref="M1_022_CA32_TheSendFailureWarningAndItsTimeAreRateLimitedTo30Seconds"/>. Host mapping: a failed
    /// sendto is a SocketException.
    /// </summary>
    [Fact]
    public void M1_022_B10_AFailedSendCountsAddSendError6AndDoesNotRetryOrDisconnect()
    {
        var clk = new ManualClock { NowMs = 1234 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        var reasons = new List<string>(); t.Disconnected += reasons.Add;
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        int calls = 0;
        t.SendHook = (_, _, _) => { calls++; throw new SocketException((int)SocketError.NetworkUnreachable); };
        t.Connect(IPAddress.Loopback, 59958);
        Assert.Equal(1, calls);                             // one attempt, no retry
        Assert.Equal(1, t.UdpSendErrors[6]);
        Assert.Equal(LinkState.Connecting, t.State);        // no disconnect
        Assert.Empty(reasons);
        Assert.Contains(t.Connection!.Pending, p => p.Type == ReliableMessageType.ConnectionRequest);
        Assert.NotNull(t.CurrentSocket);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-022 CA31 with fd −1: UDP SendData has no fd guard, so sendto(−1) fails and takes
    /// the AddSendError(6) path (0x0083A374..0x0083A386; CA32).
    /// A Connect before Start counts AddSendError(6) once for its one ConnectionRequest send, and nothing
    /// else changes: no disconnect, the request stays pending.
    /// </summary>
    [Fact]
    public void M1_022_B10_ASendWithNoSocketCountsAddSendError6()
    {
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, new ManualClock { NowMs = 1000 }, manualPump: true);
        Assert.Null(t.CurrentSocket);
        t.Connect(IPAddress.Loopback, 59955);
        Assert.Equal(1, t.UdpSendErrors[6]);
        Assert.Equal(LinkState.Connecting, t.State);
        Assert.Contains(t.Connection!.Pending, p => p.Type == ReliableMessageType.ConnectionRequest);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-023 G4.4: the WifiUtil handler does nothing if fd (+0x94) &lt; 0 (0x0083BAD6,
    /// 0x0083BADC blt); otherwise it calls ResetSocket (0x0083BAE0) and logs "WifiUtil.BindTransport" / "reset
    /// socket %d" (0x0083BB54, 0x0083BB6C). G4.5: ResetSocket only sets +0x9D (0x0083A258): the socket is
    /// untouched until the next update.
    /// </summary>
    [Fact]
    public void M1_023_G4_4_TheBindSignalSetsTheResetFlagOnlyWhenASocketIsOpen()
    {
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, new ManualClock(), manualPump: true);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        t.NetworkBindSignal();                              // fd -1: nothing
        Assert.False(t.ResetRequested);
        Assert.Empty(warnings);

        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        var s = t.CurrentSocket;
        t.NetworkBindSignal();
        Assert.True(t.ResetRequested);
        Assert.Same(s, t.CurrentSocket);                    // only the flag
        Assert.Contains(warnings, w => w.Contains("WifiUtil.BindTransport") && w.Contains("reset socket"));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-023 G4.5 / B18: the next UDPTransport::Update calls CloseSocket (0x0083ACF0) and,
    /// since it returned 1 (0x0083ACF4), OpenSocket on +0x98 (0x0083ACF8..0x0083ACFE), which the close has just
    /// set to 47817 (B38); the flag is cleared (0x0083AD04); then the read loop runs on the new socket
    /// (0x0083AD08..0x0083AD18). The connections are untouched.
    /// </summary>
    [Fact]
    public void M1_023_G4_5_TheNextUpdateClosesReopensOn47817ClearsTheFlagAndReads()
    {
        var (t, clk, net) = Connected(59957);
        using var _t = t;                                   // disposed even if an assertion fails: frees 47817
        var delivered = new List<byte[]>(); t.DataReceived += delivered.Add;
        var c = t.Connection;
        var old = t.CurrentSocket!;
        t.NetworkBindSignal();
        net.Datagram(Raw(ReliableMessageType.SingleUnreliableMessage, 0, 0, 1, Data), t.Peer!);
        int reads = net.SocketsSeen.Count;
        clk.NowMs = 1041; t.Pump();
        Assert.False(t.ResetRequested);
        Assert.True(old.SafeHandle.IsClosed);
        var fresh = t.CurrentSocket!;
        Assert.NotSame(old, fresh);
        Assert.Equal(new IPEndPoint(IPAddress.Any, 47817), fresh.LocalEndPoint);
        Assert.True(net.SocketsSeen.Count > reads);
        Assert.All(net.SocketsSeen.Skip(reads), s => Assert.Same(fresh, s));   // read on the new socket
        Assert.Equal(Data, Assert.Single(delivered));
        Assert.Same(c, t.Connection);
        Assert.Equal(LinkState.Connected, t.State);
        t.Dispose();
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE, with a HOST MAPPING for the failed close. M1-023 G4.5: with the reset flag set, a close
    /// that does not return 1 (0x0083ACF4 cmp r0,#1; bne) is not reopened, the flag is cleared either way
    /// (0x0083AD04), and with fd -1 the read loop does not run (0x0083AD08).
    /// </summary>
    [Fact]
    public void M1_023_G4_5_AFailedCloseIsNotReopenedAndTheFlagIsStillCleared()
    {
        var (t, clk, net) = Connected(59956);
        t.CloseHook = s => { s.Close(); throw new SocketException((int)SocketError.NotSocket); };
        t.NetworkBindSignal();
        int reads = net.SocketsSeen.Count;
        clk.NowMs = 1041; t.Pump();
        Assert.False(t.ResetRequested);
        Assert.Null(t.CurrentSocket);
        Assert.Equal(reads, net.SocketsSeen.Count);         // no socket: the read loop did not run
        t.Dispose();
    }

    /// <summary>A stand-in for NetworkChange.NetworkAddressChanged that a test can raise.</summary>
    private sealed class FakeNetworkChange
    {
        private System.Net.NetworkInformation.NetworkAddressChangedEventHandler? _handlers;
        public HostNetworkChange Source => new(h => _handlers += h, h => _handlers -= h);
        public int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public void Raise() => _handlers?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// POLICY TEST (M1-037, decision D5), not a source oracle. The host's address-change notification raises the
    /// WifiUtil handler (M1-023 G4.4), which sets the reset flag; the transport is subscribed for its lifetime
    /// (G4.1: the registration lives as long as the RCM) and unsubscribed by Dispose, after which the
    /// notification reaches nothing.
    /// </summary>
    [Fact]
    public void M1_037_TheHostAddressChangeRaisesTheSocketResetForTheTransportsLifetime()
    {
        var net = new FakeNetworkChange();
        var t = new ReliableTransport(TransportOptions.EngineDefaults, new ManualClock(), manualPump: true, net.Source);
        Assert.Equal(1, net.Subscribers);
        net.Raise();                                        // no socket yet: G4.4 does nothing
        Assert.False(t.ResetRequested);
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        net.Raise();
        Assert.True(t.ResetRequested);
        t.Dispose();
        Assert.Equal(0, net.Subscribers);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-001 B3: the remote port is 5552 if isSimulated (0x0069DE84 movw r2,#0x15b0;
    /// 0x0069DE92 ldrb r0,[r1,#0x10]), else 5551 (0x0069DE98 movweq r2,#0x15af), with the IP taken from the
    /// caller (0x0069DE9E TransportAddress(char const*, int)). No socket is opened, so nothing is sent.
    /// </summary>
    [Fact]
    public void M1_001_B3_TheRemotePortIs5551Or5552ByIsSimulated()
    {
        Assert.Equal(5551, RobotAddress.RemotePort(isSimulated: false));
        Assert.Equal(5552, RobotAddress.RemotePort(isSimulated: true));
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, new ManualClock(), manualPump: true);
        var ip = IPAddress.Parse("10.9.8.7");
        t.Connect(ip);
        Assert.Equal(new IPEndPoint(ip, 5551), t.Peer);
        t.Connect(ip, isSimulated: true);
        Assert.Equal(new IPEndPoint(ip, 5552), t.Peer);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-001 B1 (unity/scripts/csharp/ConnectionFlowController.cs:199, :204, :673;
    /// RobotEngineManager.cs:524-526): the IP is 172.31.1.1 for a physical robot and 127.0.0.1 for the simulator;
    /// with B3 the port follows isSimulated. RobotLink.ConnectAsync with no IP uses them; an explicit IP is kept.
    /// The send is swallowed by the send seam so nothing leaves the host.
    /// </summary>
    [Fact]
    public async Task M1_001_B1_TheDefaultIpIs172_31_1_1Or127_0_0_1()
    {
        Assert.Equal(IPAddress.Parse("172.31.1.1"), RobotAddress.DefaultFor(isSimulated: false));
        Assert.Equal(IPAddress.Parse("127.0.0.1"), RobotAddress.DefaultFor(isSimulated: true));

        foreach (var (sim, ip, expected) in new[]
        {
            (false, (IPAddress?)null, new IPEndPoint(IPAddress.Parse("172.31.1.1"), 5551)),
            (true, (IPAddress?)null, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5552)),
            (false, IPAddress.Parse("10.9.8.7"), new IPEndPoint(IPAddress.Parse("10.9.8.7"), 5551)),
        })
        {
            using var t = new ReliableTransport(TransportOptions.EngineDefaults, new ManualClock(), manualPump: true);
            t.SendHook = (_, datagram, _) => datagram.Length;
            using var link = new RobotLink(t);
            await Assert.ThrowsAsync<TimeoutException>(() => link.ConnectAsync(ip, sim, TimeSpan.FromMilliseconds(50)));
            Assert.Equal(expected, t.Peer);
        }
    }

    // ================================================================ closure rows (CA)

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-002 CA7: AddRecvMessage(size) counts every datagram before any check (0x0083A776).
    /// CA3: a datagram shorter than the 4-byte prefix is the warning "UDPTransport.BadPrefix.TooSmall" and
    /// AddRecvError(0) (0x0083A780; 0x0083A7F0..0x0083A7F4). CA4: a failed memcmp of the prefix is the warning
    /// "UDPTransport.BadPrefix" and AddRecvError(2) (0x0083A80E; 0x0083A87E..0x0083A882). Neither reaches
    /// ReliableTransport::ReceiveData (CA7: only a pass is handed on), and the read goes on (B16).
    /// </summary>
    [Fact]
    public void M1_002_CA3_CA4_CA7_TheUdpLayerCountsEveryDatagramAndItsPrefixFailuresByCode()
    {
        var (t, clk, net) = Connected(59972);
        using var _t = t;
        var warnings = new List<string>(); t.Warning += warnings.Add;
        var delivered = new List<byte[]>(); t.DataReceived += delivered.Add;
        long msgs0 = t.UdpMessagesReceived, bytes0 = t.UdpBytesReceived;
        var tooSmall = new byte[] { (byte)'C', (byte)'O', (byte)'Z' };              // 3 < 4
        var badPrefix = new byte[] { (byte)'C', (byte)'O', (byte)'Z', 4, 0, 0 };     // 4th prefix byte 04, not 03
        var good = Raw(ReliableMessageType.SingleUnreliableMessage, 0, 0, 1, Data);
        net.Datagram(tooSmall, t.Peer!);
        net.Datagram(badPrefix, t.Peer!);
        net.Datagram(good, t.Peer!);
        clk.NowMs = 1041; t.Pump();

        Assert.Equal(1, t.UdpReceiveErrors[0]);             // CA3
        Assert.Equal(1, t.UdpReceiveErrors[2]);             // CA4
        Assert.Equal(0, t.UdpReceiveErrors[1]);
        Assert.Equal(2, warnings.Count);
        Assert.StartsWith("UDPTransport.BadPrefix.TooSmall", warnings[0]);
        Assert.StartsWith("UDPTransport.BadPrefix:", warnings[1]);
        Assert.Equal(msgs0 + 3, t.UdpMessagesReceived);     // CA7: all three, the failures included
        Assert.Equal(bytes0 + tooSmall.Length + badPrefix.Length + good.Length, t.UdpBytesReceived);
        Assert.Equal(0, t.ReliableReceiveErrors[4]);        // not handed on to ReliableTransport::ReceiveData
        Assert.Equal(Data, Assert.Single(delivered));       // the read went on
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-010 / M1-018 CA18: ReliableTransport::Update visits the connections in ascending
    /// TransportAddress::operator&lt; order, and a timed-out one gives OnDisconnected and is deleted
    /// (0x00837B9A..0x00837C92). CA19 (0x00838EDA..0x00838F62; 0x008384E8..0x00838518): the type byte first,
    /// '6' IPv6 &lt; 'i' IPv4; for IPv4 the u32 at +8 (raw sin_addr), then the u16 port in host order. Expected
    /// order worked by hand: sin_addr is network-order bytes, read as a little-endian u32 on the engine's ARM, so
    /// 10.0.0.1 = 0x0100000A &lt; 10.0.0.2 = 0x0200000A &lt; 9.0.0.3 = 0x03000009, and 10.0.0.1:5551 &lt; 10.0.0.1:5552.
    /// The connections are made in another order (a, b, c, d), and a dotted-quad sort would put 9.0.0.3 first.
    /// </summary>
    [Fact]
    public void M1_010_CA18_CA19_TheUpdateVisitsTheConnectionsInTransportAddressOrder()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        var a = new IPEndPoint(IPAddress.Parse("10.0.0.2"), 5551);
        var b = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 5552);
        var c = new IPEndPoint(IPAddress.Parse("9.0.0.3"), 5551);
        var d = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 5551);
        var v6 = new IPEndPoint(IPAddress.IPv6Loopback, 5551);
        foreach (var from in new[] { a, b, c, v6, d })
            t.ProcessIncoming(Raw(ReliableMessageType.ConnectionRequest, 1, 1, 0, Array.Empty<byte>()), from);   // R13
        var expected = new[] { v6, d, b, a, c };
        Assert.Equal(expected, t.ConnectionAddresses);

        var events = Receiver(t);
        clk.NowMs = 1000 + 5000.1;                          // R19 / G2.7: all of them time out in this update
        Assert.False(t.Pump());
        Assert.Equal(expected, events.Where(e => e.Marker == ReceiverMarker.OnDisconnected).Select(e => e.Address));
        Assert.Empty(t.ConnectionAddresses);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE, with a HOST MAPPING for the failure (a SocketException from the step, through the
    /// OpenSocket seam). M1-022 CA24: OpenSocket calls CloseSocket and stores the port argument (0x00839A3A,
    /// 0x00839A46); the first open's argument is Init's 0 (CA37). CA26: socket() failing leaves fd −1, gives the
    /// error "OpenSocketFailed", and its CloseSocket is a no-op (CA30), so +0x98 keeps 0 (0x00839B8A..0x00839CF2).
    /// CA27: SO_BROADCAST failing gives the error "SetBroadcastFailed" and CloseSocket, so port 47817
    /// (0x00839D00..0x00839D24). CA28: a bind failure other than EADDRINUSE gives the error "BindFailed" and
    /// CloseSocket, so port 47817 (0x00839D3A..0x00839D42; 0x00839FA2..0x0083A026). No socket is left.
    /// </summary>
    [Theory]
    [InlineData("socket", "UDPTransport.OpenSocketFailed", 0)]
    [InlineData("broadcast", "UDPTransport.SetBroadcastFailed", 47817)]
    [InlineData("bind", "UDPTransport.BindFailed", 47817)]
    public void M1_022_CA24_CA26_CA27_CA28_AFailedOpenSocketStepLeavesNoSocket(string step, string error, int storedPort)
    {
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, new ManualClock(), manualPump: true);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        t.OpenSocketFault = at => { if (at == step) throw new SocketException((int)SocketError.AccessDenied); };
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        Assert.Null(t.CurrentSocket);
        Assert.Equal(storedPort, t.StoredLocalPort);
        Assert.StartsWith(ReliableTransport.ErrorLevel + error, Assert.Single(warnings));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-022 CA32 (0x0083A546..0x0083A56C; 0x0083A648..0x0083A666, literal 0x0083A6C8): a send
    /// failure always counts AddSendError(6); it warns "UDPTransport.SendFailed" and stores +0x88 = now only when
    /// verbose (CA33: const 0), or +0x88 == 0.0, or now &gt; +0x88 + 30000.0. CA34 / CA36: +0x88 starts at 0.0 and
    /// the clock is GetCurrentNetTimeStamp. CA31: with no socket every sendto fails (no fd guard). The resends
    /// come from R25 (33.3 ms), called directly so the 5 s timeout of the update does not apply.
    /// </summary>
    [Fact]
    public void M1_022_CA32_TheSendFailureWarningAndItsTimeAreRateLimitedTo30Seconds()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true);
        var warnings = new List<string>(); t.Warning += warnings.Add;
        Assert.Equal(0.0, t.LastSendErrorMs);               // CA34 / CA36
        t.Connect(IPAddress.Loopback, 59973);               // the ConnectionRequest's one send fails
        Assert.Equal(1, t.UdpSendErrors[6]);
        Assert.Equal(1000, t.LastSendErrorMs);              // +0x88 was 0.0: warn and store
        Assert.StartsWith("UDPTransport.SendFailed", Assert.Single(warnings));
        var c = t.Connection!;

        clk.NowMs = 2000; Assert.Equal(1, c.SendOptimalUnAckedPackets(1));
        Assert.Equal(2, t.UdpSendErrors[6]);                // always counted
        Assert.Equal(1000, t.LastSendErrorMs);              // not stored
        Assert.Single(warnings);                            // no warning

        clk.NowMs = 31000; Assert.Equal(1, c.SendOptimalUnAckedPackets(1));   // == +0x88 + 30000: not greater
        Assert.Equal(3, t.UdpSendErrors[6]);
        Assert.Equal(1000, t.LastSendErrorMs);
        Assert.Single(warnings);

        clk.NowMs = 31040; Assert.Equal(1, c.SendOptimalUnAckedPackets(1));   // > +0x88 + 30000
        Assert.Equal(4, t.UdpSendErrors[6]);
        Assert.Equal(31040, t.LastSendErrorMs);
        Assert.Equal(2, warnings.Count);
        Assert.StartsWith("UDPTransport.SendFailed", warnings[1]);
        Assert.Equal(4, t.UdpMessagesSent);                 // CA31: AddSentMessage before each sendto
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-019 CA14 (0x00838004..0x0083802A; 0x00836C5A..0x00836C6E, 0x00836CDC..0x00836D06): the
    /// Disconnect closure's SendMessage(type 3) to an address with no connection finds none, gives the warning
    /// "unconnected destination" and sends nothing; DeleteConnection is a no-op. No connection is created (R13:
    /// only a type 1 creates one on the send side).
    /// </summary>
    [Fact]
    public void M1_019_CA14_ADisconnectWithNoConnectionSendsNothingAndOnlyWarns()
    {
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, new ManualClock { NowMs = 1000 }, manualPump: true);
        var warnings = new List<string>(); t.Warning += w => { lock (warnings) warnings.Add(w); };
        var frames = new List<FrameEvent>(); t.FrameTrace += f => { lock (frames) frames.Add(f); };
        var to = new IPEndPoint(IPAddress.Loopback, 59974);
        t.Disconnect(to);
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        lock (frames) Assert.Empty(frames);
        Assert.Equal(0, t.UdpMessagesSent);
        Assert.Null(t.ConnectionFor(to));
        lock (warnings) Assert.Contains(warnings, w => w.Contains("unconnected destination"));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-035 CA10: SendMessage stores the time it is called with at PendingMessage +0x00
    /// (0x00835802). CA11: in async mode that is the time QueueMessage read when it posted the closure
    /// (closure+0x40), not the time the closure runs. CA13: every type-6 part of a split message carries the same
    /// time. CA15: in sync mode QueueMessage calls SendMessage directly with time 0.0 (0x00836B42..0x00836B5C).
    /// The entries are read on the executor, where no update can run beside the read.
    /// </summary>
    [Fact]
    public void M1_035_CA10_TheTimeSendMessageIsCalledWithIsStoredOnEveryEntryItQueues()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk);
        t.Connect(IPAddress.Loopback, 59975);
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        var c = t.Connection!;

        var gate = new ManualResetEventSlim(); var busy = new ManualResetEventSlim();
        t.Executor.Post(() => { busy.Set(); gate.Wait(TimeSpan.FromSeconds(10)); });
        Assert.True(busy.Wait(TimeSpan.FromSeconds(5)));
        clk.NowMs = 1010;
        t.SendData(new byte[3000], reliable: true, flush: false);   // posted at 1010; R22: three type-6 parts
        clk.NowMs = 1020;                                            // runs later
        gate.Set();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));

        List<(ReliableMessageType type, double time)>? seen = null;
        t.Executor.Post(() => seen = c.Pending.Select(p => (p.Type, p.QueuedTimeMs)).ToList());
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        Assert.Equal((ReliableMessageType.ConnectionRequest, 1000.0), seen![0]);   // posted at 1000
        var parts = seen.Where(p => p.type == ReliableMessageType.MultiPartMessage).ToList();
        Assert.Equal(3, parts.Count);
        Assert.All(parts, p => Assert.Equal(1010.0, p.time));

        var (sync, sclk, _) = Offline();
        using var _s = sync;
        Connect(sync);
        sclk.NowMs = 1500;
        sync.SendData(Data, reliable: true, flush: false);
        Assert.Equal(0.0, sync.Connection!.Pending[^1].QueuedTimeMs);   // CA15
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE, CA11: QueueMessage copies the caller's buffer when it posts (operator new[] +
    /// memcpy, 0x00836B66..0x00836B72), and PendingMessage::Set copies it again (CreateCombinedBuffer
    /// 0x0083581E). So a caller that reuses its array after SendData changes neither what is queued nor what
    /// is resent, in either mode.
    /// </summary>
    [Fact]
    public void M1_035_CA11_SendDataCopiesTheCallersBuffer()
    {
        var clk = new ManualClock { NowMs = 1000 };
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, clk);
        t.Connect(IPAddress.Loopback, 59973);
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        var c = t.Connection!;
        var gate = new ManualResetEventSlim(); var busy = new ManualResetEventSlim();
        t.Executor.Post(() => { busy.Set(); gate.Wait(TimeSpan.FromSeconds(10)); });
        Assert.True(busy.Wait(TimeSpan.FromSeconds(5)));
        var buf = new byte[] { 0x10, 0x20, 0x30 };
        t.SendData(buf, reliable: true, flush: false);        // posted while the executor is busy
        buf[0] = 0xEE;                                         // the caller reuses its array before the closure runs
        gate.Set();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        byte[]? queued = null;
        t.Executor.Post(() => queued = c.Pending[^1].Payload.ToArray());
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        Assert.Equal(new byte[] { 0x10, 0x20, 0x30 }, queued);

        var (sync, _, _) = Offline();
        using var _s = sync;
        Connect(sync);
        var sbuf = new byte[] { 0x01, 0x02 };
        sync.SendData(sbuf, reliable: true, flush: false);    // sync mode: SendMessage directly, Set still copies
        sbuf[0] = 0xEE;
        Assert.Equal(new byte[] { 0x01, 0x02 }, sync.Connection!.Pending[^1].Payload);
    }

    // ------------------------------------------------------------------ rig

    private static byte[] ConnectionResponse() =>
        FrameCodec.Encode(Frame.Single(new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1), 1));

    private static (ReliableTransport t, ManualClock clk, ScriptedReceive net) Connected(int port)
    {
        var clk = new ManualClock { NowMs = 1000 };
        var net = new ScriptedReceive();
        var t = new ReliableTransport(TransportOptions.EngineDefaults, clk, manualPump: true) { ReceiveHook = net.Receive };
        t.Start(); Assert.True(t.Flush(TimeSpan.FromSeconds(5)));   // R39 / B12: the socket, once (a posted action)
        t.Connect(IPAddress.Loopback, port);                // R38: only the ConnectionRequest
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
        /// <summary>Items not yet returned by a receive.</summary>
        public int Pending => _items.Count;

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
