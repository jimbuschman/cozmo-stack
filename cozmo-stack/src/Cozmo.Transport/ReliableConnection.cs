using Cozmo.Protocol;

namespace Cozmo.Transport;

/// <summary>A queued outgoing message (official PendingMessage).</summary>
public sealed class PendingMessage
{
    public ReliableMessageType Type { get; }
    public ushort Seq { get; }
    public byte[] Payload { get; }
    public bool FlushPacket { get; }
    public double QueuedTimeMs { get; }
    public double FirstSentTimeMs { get; private set; }
    public double LastSentTimeMs { get; private set; }
    public bool IsReliable => Seq != SequenceId.Invalid;
    public bool HasBeenSent => LastSentTimeMs != 0;
    public int SendCount { get; private set; }

    public PendingMessage(ReliableMessageType type, ushort seq, byte[] payload, bool flush, double queuedMs)
    { Type = type; Seq = seq; Payload = payload; FlushPacket = flush; QueuedTimeMs = queuedMs; }

    public void MarkSent(double nowMs) { if (FirstSentTimeMs == 0) FirstSentTimeMs = nowMs; LastSentTimeMs = nowMs; SendCount++; }
}

// fidelity: M1-009
/// <summary>
/// PendingMultiPartMessage: the assembly of one incoming multipart message (R23). It belongs to its
/// connection: HandleSubMessage passes the connection (0x008374C6 mov r0,r5) to
/// ReliableConnection::GetPendingMultiPartMessage (0x008374C8; body 0x00835A94 adds r0,#0x20) and calls
/// AddMessagePart on what it returns, so it goes with its connection when DeleteConnection destroys it
/// (verifier reading, batch 2b-ii; pending inventory correction).
/// </summary>
public sealed class PendingMultiPartMessage
{
    internal readonly List<byte> Data = new();
    /// <summary>The index expected next (+4, from 1).</summary>
    internal int Next = 1;
    /// <summary>The part count, set by part 1.</summary>
    internal int Last;

    internal void Clear() { Data.Clear(); Next = 1; Last = 0; }
}

/// <summary>Callback the connection uses to put a finished frame on the wire.</summary>
public delegate void FrameSender(ReliableMessageType type, ushort seqMin, ushort seqMax, byte[] body);

/// <summary>
/// Per-peer reliability state. This is a faithful port of Anki::Util::ReliableConnection (open-sourced in the Vector
/// repository, byte-identical behaviour confirmed against the Cozmo engine disassembly), parameterised with the
/// Cozmo engine's tunables (<see cref="TransportOptions"/>). It is not thread-safe; the owner serialises access.
///
/// Sequence model: every reliable message gets the next id from <see cref="NextOutSeq"/> (starting at 1). All
/// pending messages (reliable and not-yet-sent unreliable ones) sit in one list. A frame is built from a contiguous
/// run of the list (up to <see cref="TransportOptions.MaxFramePayloadBytes"/>); unreliable messages are dropped from
/// the list once sent, reliable ones stay until the peer's header ack (seqIdLastReceived) covers them. Resends are
/// whole frames rebuilt from the oldest unacked message, at most every TimeBetweenResendsMs. There is no explicit
/// window: back-pressure is only the ~33 ms resend cadence and the peer's in-order acceptance.
/// </summary>
public sealed class ReliableConnection
{
    private readonly TransportOptions _o;
    private readonly INetClock _clock;
    private readonly FrameSender _send;
    private readonly List<PendingMessage> _pending = new();

    public ushort NextOutSeq { get; private set; } = SequenceId.Min;
    /// <summary>Last in-order reliable id we accepted from the peer; goes into every outgoing header as seqIdLastReceived.</summary>
    public ushort LastInAcked { get; private set; } = SequenceId.Invalid;
    /// <summary>The reliable id we expect next from the peer.</summary>
    public ushort NextInSeq { get; private set; } = SequenceId.Min;

    public double LatestMessageSentMs { get; private set; }
    public double LatestRecvMs { get; private set; }
    public double LatestPingSentMs { get; private set; }
    public uint NumPingsSent, NumPingsReceived, NumPingsSentThatArrived, NumPingsSentTowardsUs;
    public double LastPingRoundTripMs { get; private set; } = double.NaN;
    public int PingRepliesSeen { get; private set; }
    public int PendingCount => _pending.Count;

    // fidelity: M1-009
    /// <summary>GetPendingMultiPartMessage 0x00835A94: this connection's multipart assembly (R23).</summary>
    public PendingMultiPartMessage MultiPart { get; } = new();

    public int FramesSent { get; private set; }
    public int ResendFrames { get; private set; }
    public int DuplicateReliableDropped { get; private set; }

    public ReliableConnection(TransportOptions options, INetClock clock, FrameSender send)
    {
        _o = options; _clock = clock; _send = send; LatestRecvMs = clock.NowMs;
    }

    // ------------------------------------------------------------------ outgoing

    /// <summary>Official ReliableTransport::SendMessage for a single (non multipart) message: queue, then try to send one packet.</summary>
    public void Queue(ReliableMessageType type, byte[] payload, bool reliable, bool flush)
    {
        int maxPayload = _o.MaxFramePayloadBytes;
        if (payload.Length > maxPayload)
        {
            // Official: split into MultiPartMessage parts, forced reliable: [index u8 1-based][count u8][bytes]
            int perPart = maxPayload - 2;
            int count = (payload.Length + perPart - 1) / perPart;
            for (int i = 0; i < count; i++)
            {
                int off = i * perPart, len = Math.Min(perPart, payload.Length - off);
                var part = new byte[2 + len]; part[0] = (byte)(i + 1); part[1] = (byte)count; Array.Copy(payload, off, part, 2, len);
                _pending.Add(new PendingMessage(ReliableMessageType.MultiPartMessage, TakeNextOutSeq(), part, flush, _clock.NowMs));
            }
        }
        else
        {
            ushort seq = reliable ? TakeNextOutSeq() : SequenceId.Invalid;
            _pending.Add(new PendingMessage(type, seq, payload, flush, _clock.NowMs));
        }
        if (_o.MaxPacketsToSendOnSendMessage > 0) SendOptimalUnAckedPackets(_o.MaxPacketsToSendOnSendMessage);
    }

    // fidelity: M1-004
    private ushort TakeNextOutSeq() { var s = NextOutSeq; NextOutSeq = SequenceId.Next(s); return s; }

    private ushort FirstUnackedOutId() { foreach (var m in _pending) if (m.IsReliable) return m.Seq; return SequenceId.Invalid; }
    private ushort LastUnackedOutId() { for (int i = _pending.Count - 1; i >= 0; i--) if (_pending[i].IsReliable) return _pending[i].Seq; return SequenceId.Invalid; }

    /// <summary>Official SendUnAckedMessages: build one frame starting at index firstToSend (extending backwards if room), send it, mark times, drop sent unreliable messages. Returns messages sent from firstToSend onward.</summary>
    private int SendUnAckedMessages(int firstToSend)
    {
        if (firstToSend >= _pending.Count) return 0;
        int max = _o.MaxFramePayloadBytes;
        var first = _pending[firstToSend];
        int num = 1; int bytes = first.Payload.Length + FrameCodec.SubMessageOverhead;
        for (int i = firstToSend + 1; i < _pending.Count; i++)
        {
            int ifAdded = bytes + FrameCodec.SubMessageOverhead + _pending[i].Payload.Length;
            if (ifAdded > max) break;
            num++; bytes = ifAdded;
        }
        int earlier = 0;
        while (firstToSend > 0)
        {
            var pm = _pending[firstToSend - 1];
            int ifAdded = bytes + FrameCodec.SubMessageOverhead + pm.Payload.Length;
            if (ifAdded > max) break;
            firstToSend--; earlier++; num++; bytes = ifAdded;
        }
        var slice = _pending.GetRange(firstToSend, num);
        Frame frame = num == 1
            ? Frame.Single(new SubMessage(slice[0].Type, slice[0].Payload, slice[0].Seq), LastInAcked)
            : Frame.Multiple(slice.Select(p => new SubMessage(p.Type, p.Payload, p.Seq)).ToList(), LastInAcked);
        var body = FrameCodec.Encode(frame).AsSpan(ReliableHeader.Length).ToArray();
        _send(frame.Type, frame.SeqMin, frame.SeqMax, body);
        FramesSent++;
        double now = _clock.NowMs; LatestMessageSentMs = now;
        bool anyResend = false;
        for (int i = firstToSend + num - 1; i >= firstToSend; i--)
        {
            var pm = _pending[i];
            if (pm.HasBeenSent) anyResend = true;
            pm.MarkSent(now);
            if (!pm.IsReliable) _pending.RemoveAt(i);
        }
        if (anyResend) ResendFrames++;
        return num - earlier;
    }

    private int SendUnAckedPackets(int maxPackets, int firstToSend)
    {
        int packets = 0, sent;
        do
        {
            sent = SendUnAckedMessages(firstToSend);
            if (sent > 0) { firstToSend += sent; packets++; }
        } while (sent > 0 && packets < maxPackets);
        return packets;
    }

    /// <summary>Official IsPacketWorthSending with the engine's settings (SendPacketsImmediately=false).</summary>
    private bool IsPacketWorthSending(double now, int firstToSend)
    {
        if (_o.SendPacketsImmediately) return true;
        if (LatestMessageSentMs > 0 && now > LatestMessageSentMs + _o.MaxTimeSinceLastSendMs) return true;
        int minBytesForFull = _o.MaxFramePayloadBytes - _o.MaxBytesFreeInAFullPacket;
        int bytes = 0;
        for (int i = firstToSend; i < _pending.Count; i++)
        {
            var pm = _pending[i];
            if (pm.FlushPacket) return true;
            if (pm.LastSentTimeMs > 0 && now > pm.LastSentTimeMs + _o.MaxTimeSinceLastSendMs) return true;
            bytes += FrameCodec.SubMessageOverhead + pm.Payload.Length;
            if (bytes >= minBytesForFull) return true;
        }
        return false;
    }

    /// <summary>Official SendOptimalUnAckedPackets: pick the oldest (least recently sent) message and, if due, send up to maxPackets frames from it.</summary>
    public int SendOptimalUnAckedPackets(int maxPackets)
    {
        double now = _clock.NowMs;
        if (_o.PacketSeparationIntervalMs > 0 && LatestMessageSentMs != 0 && now < LatestMessageSentMs + _o.PacketSeparationIntervalMs) return 0;
        double neverSent = now - (_o.TimeBetweenResendsMs + 1.0);
        double shouldBeAcked = LatestRecvMs - _o.MinExpectedPacketAckTimeMs;
        double oldestTime = 0; int oldestIdx = 0;
        for (int i = 0; i < _pending.Count; i++)
        {
            double t = _pending[i].LastSentTimeMs;
            if (t == 0) t = neverSent;
            else if (shouldBeAcked > 0 && t < shouldBeAcked) t -= _o.TimeBetweenResendsMs;
            if (i == 0 || t < oldestTime) { oldestTime = t; oldestIdx = i; }
        }
        if (_pending.Count == 0) return 0;
        if (IsPacketWorthSending(now, oldestIdx) && now > oldestTime + _o.TimeBetweenResendsMs)
            return SendUnAckedPackets(maxPackets, oldestIdx);
        return 0;
    }

    // ------------------------------------------------------------------ incoming

    /// <summary>Header ack from the peer: drop every pending reliable message up to and including seq. Returns true if anything was acked.</summary>
    public bool UpdateLastAckedMessage(ushort ack)
    {
        bool any = false;
        if (ack != SequenceId.Invalid && _pending.Count > 0)
        {
            while (_pending.Count > 0 && SequenceId.InRange(ack, FirstUnackedOutId(), LastUnackedOutId()))
            {
                // official removes list[0] while in range; list[0] may be an unsent unreliable message — official asserts it is reliable,
                // in practice unreliable ones are removed right after sending so [0] is reliable here.
                _pending.RemoveAt(0); any = true;
            }
        }
        LatestRecvMs = _clock.NowMs;
        return any;
    }

    public bool IsWaitingForAnyInRange(ushort min, ushort max) => SequenceId.InRange(NextInSeq, min, max);
    public bool IsNextInSequence(ushort seq) => NextInSeq == seq;
    public void AdvanceNextInSequence() => NextInSeq = SequenceId.Next(NextInSeq);
    public void AckMessage(ushort seq) => LastInAcked = seq;

    /// <summary>Returns true if this reliable sub-message is new and in order (and advances), false if it must be ignored (dup/out of order).</summary>
    public bool AcceptReliable(ushort seq)
    {
        if (IsNextInSequence(seq)) { AdvanceNextInSequence(); return true; }
        DuplicateReliableDropped++; return false;
    }

    // fidelity: M1-008
    public void SendPing(double incomingPingTime = 0, bool isReply = false)
    {
        NumPingsSent++;
        double now = _clock.NowMs;
        var p = new PingPayload(isReply ? incomingPingTime : now, NumPingsSent, NumPingsReceived, isReply);
        // official: SendMessage(unreliable, Ping, flush=true) -> queued (SendUnreliableMessagesImmediately=false) then one packet attempt
        _pending.Add(new PendingMessage(ReliableMessageType.Ping, SequenceId.Invalid, p.ToBytes(), true, now));
        if (_o.MaxPacketsToSendOnSendMessage > 0) SendOptimalUnAckedPackets(_o.MaxPacketsToSendOnSendMessage);
        if (!isReply) LatestPingSentMs = now;
    }

    // fidelity: M1-011
    public void ReceivePing(ReadOnlySpan<byte> payload)
    {
        if (!PingPayload.TryParse(payload, out var p)) return;
        double now = _clock.NowMs;
        NumPingsReceived++;
        if (p.NumPingsSent > NumPingsSentTowardsUs) { NumPingsSentTowardsUs = p.NumPingsSent; NumPingsSentThatArrived = p.NumPingsReceived; }
        // ReliableConnection::ReceivePing 0x00835C70: the byte at payload+0x10 decides everything. Non-zero
        // and it is a reply, so the round trip is now - payload.timeSent and goes into the stats
        // accumulator; zero and the engine answers it with a ping of its own carrying the same timestamp
        // and isReply set, but only when sSendSeparatePingMessages is on, and measures nothing.
        //
        // There is no third case. This stack used to treat an echo of a timestamp it had sent as a reply
        // too, on the strength of a PyCozmo capture showing the robot echoing with isReply still 0. The
        // engine does not do that: against a robot that echoes, the app measures no round trip at all.
        if (p.IsReply) { LastPingRoundTripMs = now - p.TimeSentMs; PingRepliesSeen++; }
        else if (_o.SendSeparatePingMessages) SendPing(p.TimeSentMs, true);
    }

    /// <summary>Official Update: idle pings, resends, timeout check. Returns false when the connection has timed out.</summary>
    // fidelity: M1-017
    public bool Update()
    {
        double now = _clock.NowMs;
        if (_o.SendSeparatePingMessages ||
            (_pending.Count == 0 && LatestMessageSentMs > 0 && now > LatestMessageSentMs + _o.TimeBetweenResendsMs))
        {
            if (now >= LatestPingSentMs + _o.TimeBetweenPingsMs) SendPing();
        }
        SendOptimalUnAckedPackets(_o.MaxPacketsToReSendOnUpdate);
        return !HasTimedOut;
    }

    public bool HasTimedOut => _clock.NowMs > LatestRecvMs + _o.ConnectionTimeoutMs;

    public IReadOnlyList<PendingMessage> Pending => _pending;
}
