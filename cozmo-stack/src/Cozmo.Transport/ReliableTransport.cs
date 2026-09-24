using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Cozmo.Protocol;

namespace Cozmo.Transport;

public enum LinkState { Idle, Connecting, Connected, Disconnected }

public sealed record FrameEvent(bool Outbound, DateTime Utc, byte[] Raw, Frame? Frame, string? Error);

/// <summary>
/// Client-side port of Anki::Util::ReliableTransport + UDPTransport for a single peer (the robot), with the
/// RobotConnectionManager rules that sit on either side of it.
/// UDP framing: "COZ\x03" prefix, no CRC (RobotConnectionManager::Init), then the 10-byte reliable header.
///
/// Each datagram is processed in the engine's order:
///  1. UDPTransport::HandleReceivedMessage: reject a datagram without the COZ prefix;
///  2. ReliableTransport::ReceiveData 0x00837632-42: fewer than 10 bytes after the prefix, or no "RE\x01",
///     and the bytes are handed on as a data message without any address lookup (0x00837792-A0);
///  3. FindConnection 0x008377FC: the frame is processed only when it comes from the peer, IP and port
///     (TransportAddress::operator&lt; 0x00838ED2), and dropped with a warning otherwise (0x0083789A);
///  4. UpdateLastAckedMessage 0x00837818, the resend-on-ack gate 0x0083781C-2E, and for a reliable header
///     IsWaitingForAnyInRange 0x0083783E (out of range drops everything but a MultipleMixed frame) and
///     AckMessage(seqMax) 0x0083784A;
///  5. the sub-messages in order, each through HandleSubMessage, stopping at the first invalid type
///     (0x0083790E → 0x008379AE) or size overrun (0x00837926 → 0x00837A0C) with the earlier ones handled;
///     a remainder too short for a sub-message header is a size overrun too. This stack also stops after a
///     DisconnectRequest sub-message has been handled (policy M1-038).
/// A data payload (types 4/5, a completed multipart, or a datagram handed on at step 2) is delivered only
/// while the link is Connected and it comes from the peer's IP, judged at its own position in arrival
/// order (RobotConnectionManager 0x0062F724-2A, TransportAddress::operator== 0x0062F744); otherwise it is
/// dropped.
///
/// Threading. Two worker threads: <c>cozmo-transport</c> runs the engine's ReliableTransport::Update every
/// <see cref="TransportOptions.UpdateIntervalMs"/> — drain the socket without blocking, then update the
/// connection — and <c>cozmo-dispatch</c> raises every public event. Handlers therefore never run on the
/// transport thread, so a slow or throwing handler cannot stall or kill the transport; events still arrive
/// in the order they were produced. All connection state is protected by <see cref="_lock"/>, which is
/// never held while a handler runs.
/// </summary>
public sealed class ReliableTransport : IDisposable
{
    private readonly TransportOptions _o;
    private readonly INetClock _clock;
    private readonly object _lock = new();
    private Socket? _sock;
    private IPEndPoint? _peer;
    private ReliableConnection? _conn;
    private Thread? _io, _dispatch;
    private BlockingCollection<Action>? _events;
    private volatile bool _running;
    private readonly List<byte> _multipart = new();
    private int _multipartNext = 1, _multipartLast;
    // fidelity: M1-002
    /// <summary>
    /// B16/B17: UDPTransport reads each datagram with recvmsg into a 0x5C0 = 1472-byte buffer
    /// (0x0083AA66..0x0083AA98); a larger datagram arrives truncated and is dropped (see <see cref="DrainLocked"/>).
    /// </summary>
    internal const int ReceiveBufferBytes = 0x5C0;
    private readonly byte[] _rxBuffer = new byte[ReceiveBufferBytes];

    // fidelity: M1-002, M1-007
    /// <summary>
    /// Receive errors counted by <c>UDPTransport</c>, by the code it passes to AddRecvError: 1 = a truncated
    /// datagram (B17, 0x0083A8C4..0x0083A8C8). Diagnostics only; nothing reads them.
    /// </summary>
    public ReceiveErrorCounts UdpReceiveErrors { get; } = new();

    /// <summary>
    /// Receive errors counted by <c>ReliableTransport::ReceiveData</c>, by the code it passes to AddRecvError:
    /// 0 = under 10 bytes and 2 = no RE\x01 prefix, each followed by 4 (R3, 0x00837632..0x008377A0);
    /// 4 = an invalid sub-message type and 1 = a sub-message size overrun (R10, 0x0083790A..0x00837926);
    /// 5 = a reliable frame out of range that is not type 9 (R16, 0x00837A6A). Diagnostics only.
    /// </summary>
    public ReceiveErrorCounts ReliableReceiveErrors { get; } = new();

    /// <summary>How long a worker thread is given to finish during shutdown before it is abandoned.</summary>
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(2);

    public LinkState State { get; private set; } = LinkState.Idle;
    public event Action? Connected;
    public event Action<string>? Disconnected;
    /// <summary>A complete application payload (a CLAD robot message: tag + body) received in order.</summary>
    public event Action<byte[]>? DataReceived;
    /// <summary>Every raw frame in both directions (for logging / conformance capture).</summary>
    public event Action<FrameEvent>? FrameTrace;
    public event Action<string>? Warning;

    /// <summary>Handlers that threw, counted so a test or a caller can notice swallowed failures.</summary>
    public int HandlerFaults { get; private set; }

    public ReliableTransport(TransportOptions? options = null, INetClock? clock = null)
    {
        _o = options ?? TransportOptions.EngineDefaults; _clock = clock ?? new StopwatchClock();
    }

    public ReliableConnection? Connection => _conn;
    public IPEndPoint? Peer => _peer;
    public TransportOptions Options => _o;

    /// <summary>Frames the connection produced while offline (see <see cref="CreateOffline"/>).</summary>
    public List<Frame> OfflineOutbound { get; } = new();

    // ------------------------------------------------------------- test seams

    /// <summary>Stands in for <see cref="Socket.ReceiveFrom(byte[], ref EndPoint)"/> on the receive path.</summary>
    internal delegate int ReceiveFromHook(Socket socket, byte[] buffer, ref EndPoint from);

    /// <summary>
    /// Test seam: when set, the transport update reads datagrams, and receive errors, from this instead of
    /// the socket. Null in production.
    /// </summary>
    internal ReceiveFromHook? ReceiveHook { get; set; }

    /// <summary>
    /// Test seam: <see cref="Connect"/> starts no worker threads, events are raised inline, and the caller
    /// runs each transport update itself with <see cref="Pump"/>. False in production.
    /// </summary>
    internal bool ManualPump { get; init; }

    /// <summary>The socket's local endpoint, for tests that need to see it reopened.</summary>
    internal EndPoint? LocalEndPoint { get { lock (_lock) return _sock?.LocalEndPoint; } }

    // ------------------------------------------------------------- event dispatch

    /// <summary>
    /// Queues a handler call for the dispatch thread. With no dispatch thread (offline transports used by
    /// tests and the replay tool) it runs inline, so replay stays synchronous and deterministic.
    /// </summary>
    private void Raise(Action a)
    {
        var q = _events;
        if (q is null) { Safe(a); return; }
        try { q.Add(a); }
        catch (ObjectDisposedException) { Safe(a); }
        catch (InvalidOperationException) { Safe(a); }   // adding completed during shutdown
    }

    /// <summary>
    /// Raises an event, isolating its subscribers from one another.
    ///
    /// A plain <c>Handler?.Invoke(x)</c> runs the whole multicast list as a single call, so a subscriber
    /// that throws stops every subscriber registered after it from running at all — one bad handler
    /// silently disabling the rest. Each target is invoked separately instead, and a fault is counted and
    /// reported against that handler alone.
    /// </summary>
    private void Fan<T>(Action<T>? handler, T arg)
    {
        if (handler is null) return;
        var targets = handler.GetInvocationList();
        if (targets.Length == 1) { SafeOne(() => handler(arg)); return; }
        foreach (var t in targets)
        {
            var one = (Action<T>)t;
            SafeOne(() => one(arg));
        }
    }

    /// <summary>The argument-less form of <see cref="Fan{T}"/>.</summary>
    private void Fan(Action? handler)
    {
        if (handler is null) return;
        var targets = handler.GetInvocationList();
        if (targets.Length == 1) { SafeOne((Action)targets[0]); return; }
        foreach (var t in targets) SafeOne((Action)t);
    }

    private void Safe(Action a) => SafeOne(a);

    private void SafeOne(Action a)
    {
        try { a(); }
        catch (Exception e)
        {
            lock (_lock) HandlerFaults++;
            try { Warning?.Invoke($"event handler threw: {e.GetType().Name}: {e.Message}"); } catch { }
        }
    }

    private void DispatchLoop(BlockingCollection<Action> q)
    {
        try { foreach (var a in q.GetConsumingEnumerable()) Safe(a); }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }   // completed while enumerating
    }

    /// <summary>Raises, in order, what processing under the lock produced, then ends the link if it has to.</summary>
    private void Complete(List<Action> effects, string? shutdownReason)
    {
        foreach (var a in effects) Raise(a);
        if (shutdownReason is not null) Shutdown(shutdownReason);
    }

    // ------------------------------------------------------------- offline mode

    /// <summary>
    /// A transport with no socket, for replay/conformance tests: incoming datagrams are fed with
    /// <see cref="ProcessIncoming(byte[])"/>, outgoing frames are captured in <see cref="OfflineOutbound"/>.
    /// Events are raised inline on the calling thread. Datagrams fed without an address are taken to come
    /// from <see cref="Peer"/>, a loopback stand-in for the robot.
    /// </summary>
    public static ReliableTransport CreateOffline(TransportOptions? options = null, INetClock? clock = null)
    {
        var t = new ReliableTransport(options, clock);
        t._peer = new IPEndPoint(IPAddress.Loopback, t._o.RobotPort);
        t._conn = new ReliableConnection(t._o, t._clock, (ty, mn, mx, body) =>
        {
            var hdr = new ReliableHeader(ty, mn, mx, t._conn!.LastInAcked);
            var raw = new byte[ReliableHeader.Length + body.Length]; hdr.Write(raw); body.CopyTo(raw, ReliableHeader.Length);
            FrameCodec.TryDecode(raw, out var f, out _);
            if (f is not null) t.OfflineOutbound.Add(f);
            t.Raise(() => t.Fan(t.FrameTrace, new FrameEvent(true, DateTime.UtcNow, raw, f, null)));
        });
        t._running = true; t.State = LinkState.Connecting;
        return t;
    }

    /// <summary>Offline only: queue the initial ConnectionRequest like <see cref="Connect"/> does.</summary>
    public void OfflineConnect() { lock (_lock) _conn!.Queue(ReliableMessageType.ConnectionRequest, Array.Empty<byte>(), true, true); }

    /// <summary>
    /// Offline only: the robot's answer to <see cref="OfflineConnect"/> — a reliable ConnectionResponse, its
    /// seq 1, acking our seq 1 — fed from the peer through the normal receive path.
    /// </summary>
    public void OfflineAcceptConnection() =>
        ProcessIncoming(FrameCodec.Encode(Frame.Single(
            new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), SequenceId.Min), SequenceId.Min)));

    /// <summary>Offline only: run one connection update tick. False when there is no live connection.</summary>
    public bool OfflineTick() { lock (_lock) return _conn?.Update() ?? false; }

    // ------------------------------------------------------------------ connect

    /// <summary>Open the socket (ephemeral local port, like the engine's UDPTransport client) and send ConnectionRequest (reliable seq 1).</summary>
    public void Connect(IPAddress robot, int? port = null)
    {
        lock (_lock)
        {
            if (_running) throw new InvalidOperationException("already running");
            if (_io is not null || _dispatch is not null)
                throw new InvalidOperationException("the previous connection has not finished shutting down");

            // Every connection starts from clean session state; a half-assembled multipart message from the
            // last connection must not be completed with fragments from this one.
            _multipart.Clear(); _multipartNext = 1; _multipartLast = 0;
            OfflineOutbound.Clear();
            HandlerFaults = 0;

            _peer = new IPEndPoint(robot, port ?? _o.RobotPort);
            _sock = OpenSocket();
            _conn = new ReliableConnection(_o, _clock, SendFrame);
            _running = true; State = LinkState.Connecting;

            if (!ManualPump)
            {
                var q = new BlockingCollection<Action>();
                _events = q;
                _dispatch = new Thread(() => DispatchLoop(q)) { IsBackground = true, Name = "cozmo-dispatch" };
                _io = new Thread(TransportLoop) { IsBackground = true, Name = "cozmo-transport" };
                _dispatch.Start(); _io.Start();
            }
            _conn.Queue(ReliableMessageType.ConnectionRequest, Array.Empty<byte>(), reliable: true, flush: true);
        }
    }

    /// <summary>A UDP client socket on an ephemeral local port, read without blocking.</summary>
    private static Socket OpenSocket()
    {
        var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            s.Bind(new IPEndPoint(IPAddress.Any, 0));
            s.Blocking = false;
            return s;
        }
        catch { s.Dispose(); throw; }
    }

    /// <summary>
    /// Send a CLAD robot message the way the engine sends every robot message: this is the
    /// <c>RobotConnectionManager::SendData(buf, len)</c> layer.
    ///
    /// It refuses unless the link is Connected, as the manager refuses unless its state is 2
    /// (0x0062F5A2-A8 → 0x0062F610): nothing is queued and no sequence id is used. It takes no delivery
    /// options: it calls <c>ReliableTransport::SendData</c> with reliable = 1 (0x0062F5CE) and flush = 0
    /// (0x0062F5C2-CA), and <c>MessageHandler::SendMessage</c> 0x0069DCF6 reaches it the same way. So
    /// <paramref name="reliable"/> and <paramref name="flush"/> do not change what is sent: every message is
    /// queued reliable and unflushed, and goes out when <c>IsPacketWorthSending</c> says so.
    /// </summary>
    public void Send(RobotMessage m, bool reliable = true, bool flush = false)
    {
        var bytes = m.ToBytes();
        lock (_lock)
        {
            if (_conn is null || State is not LinkState.Connected) throw new InvalidOperationException("not connected");
            SendData(bytes, reliable: true, flush: false);
        }
    }

    /// <summary>
    /// ReliableTransport::SendData 0x008370E4: queue one message with the given delivery options (type 4 when
    /// reliable, 5 when not, and the caller's flush). The manager's state gate is not part of this layer
    /// (see <see cref="Send"/>); this refuses only when there is no live connection to queue on.
    /// </summary>
    public void SendData(byte[] cladMessage, bool reliable = true, bool flush = false)
    {
        lock (_lock)
        {
            if (_conn is null || State is LinkState.Idle or LinkState.Disconnected) throw new InvalidOperationException("not connected");
            _conn.Queue(reliable ? ReliableMessageType.SingleReliableMessage : ReliableMessageType.SingleUnreliableMessage, cladMessage, reliable, flush);
        }
    }

    /// <summary>
    /// Official ReliableTransport::Disconnect: one reliable, flushed DisconnectRequest through the normal send
    /// path (0x0083801C), then the connection is deleted at once (0x0083802A → DeleteConnection 0x008375F0).
    /// Whether the request reaches the wire is up to that one send attempt; nothing waits for it.
    /// </summary>
    public void Disconnect(string reason = "requested")
    {
        lock (_lock) DisconnectLocked();
        Shutdown(reason);
    }

    private void DisconnectLocked()
    {
        if (_conn is not null && State is LinkState.Connected or LinkState.Connecting)
        {
            try { _conn.Queue(ReliableMessageType.DisconnectRequest, Array.Empty<byte>(), true, true); } catch { }
        }
        _conn = null;
    }

    /// <summary>
    /// Stops the workers, closes the socket and reports the reason once. Safe to call from any thread,
    /// including a worker: a thread never joins itself. After it returns, <see cref="Connect"/> may be used
    /// again, and no worker from the previous connection survives.
    /// </summary>
    private void Shutdown(string reason)
    {
        Thread? io, dispatch;
        BlockingCollection<Action>? q;
        bool notify;
        lock (_lock)
        {
            notify = _running;
            if (!notify && _io is null && _dispatch is null) return;   // already down
            _running = false;
            State = LinkState.Disconnected;
            try { _sock?.Close(); } catch { }
            _sock = null;
            _multipart.Clear(); _multipartNext = 1; _multipartLast = 0;
            io = _io; dispatch = _dispatch; q = _events;
            _io = null;
        }

        Join(io);
        if (notify) Raise(() => Fan(Disconnected, reason));

        if (q is not null)
        {
            try { q.CompleteAdding(); } catch (ObjectDisposedException) { }
            Join(dispatch);
        }
        lock (_lock)
        {
            if (ReferenceEquals(_events, q)) { _events = null; _dispatch = null; }
        }

        static void Join(Thread? t)
        {
            if (t is null || t == Thread.CurrentThread || !t.IsAlive) return;
            t.Join(JoinTimeout);
        }
    }

    /// <summary>
    /// <c>~RobotConnectionManager</c> 0x0062EE98: <c>DisconnectCurrent</c> 0x0062EF20, which disconnects the
    /// peer (0x0062EF52), and only then <c>StopClient</c> (0x0062EEA2). A Connected transport therefore
    /// disconnects exactly as <see cref="Disconnect"/> does before its socket is closed. A transport that is
    /// still Connecting sends nothing.
    /// </summary>
    public void Dispose()
    {
        lock (_lock) { if (State == LinkState.Connected) DisconnectLocked(); }
        Shutdown("disposed");
    }

    // ------------------------------------------------------------------ wire

    private void SendFrame(ReliableMessageType type, ushort seqMin, ushort seqMax, byte[] body)
    {
        // called under _lock by the connection, so nothing here may run a user handler inline
        var hdr = new ReliableHeader(type, seqMin, seqMax, _conn!.LastInAcked);
        var raw = new byte[ReliableHeader.Length + body.Length];
        hdr.Write(raw); body.CopyTo(raw, ReliableHeader.Length);
        try { _sock?.SendTo(raw, _peer!); }
        catch (ObjectDisposedException) { return; }                       // closed under us during shutdown
        catch (SocketException e) { Raise(() => Fan(Warning, $"sendto failed: {e.SocketErrorCode} ({e.Message})")); }
        if (FrameTrace is not null)
        {
            FrameCodec.TryDecode(raw, out var f, out var err);
            var utc = DateTime.UtcNow;
            Raise(() => Fan(FrameTrace, new FrameEvent(true, utc, raw, f, err)));
        }
    }

    /// <summary>
    /// The transport thread: <c>ReliableTransport::Update</c> on the engine's 2 ms schedule
    /// (<see cref="TransportCadence"/>). The timer resolution is raised because without it a 1 ms sleep on
    /// Windows lasts about 15.6 ms, which turns the 2 ms cadence into roughly 15 ms and quietly breaks the
    /// resend and packet-separation timing this transport is meant to reproduce.
    /// </summary>
    private void TransportLoop()
    {
        using var _ = new HighResolutionTimer();
        var cadence = new TransportCadence(new StopwatchClock(), _o.UpdateIntervalMs);
        while (_running)
        {
            if (cadence.TryBegin()) { Pump(); continue; }
            double wait = cadence.MsUntilDue;
            if (wait > 1.5) Thread.Sleep(1);
            else Thread.SpinWait(200);
        }
    }

    /// <summary>
    /// One <c>ReliableTransport::Update</c> 0x00837B8C: under the lock, the UDP transport's own update runs
    /// first (0x00837BA2) and drains every datagram waiting on the socket, then each connection is updated.
    /// An incoming ack is therefore applied before the same update decides whether anything needs resending.
    /// </summary>
    internal void Pump()
    {
        var effects = new List<Action>();
        string? end;
        lock (_lock)
        {
            if (!_running) return;
            end = DrainLocked(effects);
            if (end is null && _conn is not null && !_conn.Update())
                end = $"connection timed out (> {_o.ConnectionTimeoutMs} ms without any datagram)";
        }
        Complete(effects, end);
    }

    /// <summary>
    /// UDPTransport::Update 0x0083AD10-18 / TryToReadMessage 0x0083AA44: read datagrams until none is waiting.
    /// A receive error ends the read for this update and never tears the connection down. EAGAIN is silent
    /// (0x0083AAC4); every other error is a warning (0x0083AAF6), and ENOTCONN, after that same warning, also
    /// closes and reopens the socket (0x0083AB22-34).
    ///
    /// A datagram larger than the 1472-byte buffer (MSG_TRUNC) is dropped after the prefix checks and the
    /// read goes on (B17). The host reports it as <see cref="SocketError.MessageSize"/> with the buffer
    /// holding the datagram's first 1472 bytes and the datagram consumed.
    /// </summary>
    private string? DrainLocked(List<Action> effects)
    {
        var buf = _rxBuffer;
        while (_running && _sock is { } sock)
        {
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            int n;
            try { n = ReceiveHook is { } hook ? hook(sock, buf, ref from) : sock.ReceiveFrom(buf, ref from); }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.WouldBlock) { return null; }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.MessageSize)
            {
                TruncatedDatagramLocked(buf.ToArray(), effects);
                continue;
            }
            catch (SocketException e)
            {
                var code = e.SocketErrorCode; var msg = e.Message;
                effects.Add(() => Fan(Warning, $"receive failed: {code} ({msg})"));
                if (code == SocketError.NotConnected) ReopenSocketLocked(effects);
                return null;
            }
            catch (ObjectDisposedException) { return null; }

            var disc = ProcessDatagramLocked(buf.AsSpan(0, n).ToArray(), from as IPEndPoint, effects);
            if (disc is not null) return disc;
        }
        return null;
    }

    /// <summary>ENOTCONN: close the socket and open a new one on an ephemeral port; the connection is kept.</summary>
    private void ReopenSocketLocked(List<Action> effects)
    {
        try { _sock?.Close(); } catch { }
        _sock = null;
        try { _sock = OpenSocket(); }
        catch (SocketException e)
        {
            var code = e.SocketErrorCode; var msg = e.Message;
            effects.Add(() => Fan(Warning, $"reopening the closed socket failed: {code} ({msg})"));
        }
    }

    // --------------------------------------------------------------- receive

    /// <summary>Feed a raw datagram from the peer (also used by the conformance replay tool).</summary>
    public void ProcessIncoming(byte[] raw) => ProcessIncoming(raw, null);

    /// <summary>Feed a raw datagram from <paramref name="from"/>; null means the peer.</summary>
    public void ProcessIncoming(byte[] raw, IPEndPoint? from)
    {
        var effects = new List<Action>();
        string? disc;
        lock (_lock) disc = ProcessDatagramLocked(raw, from ?? _peer, effects);
        Complete(effects, disc);
    }

    // fidelity: M1-002
    /// <summary>
    /// B17: a datagram with MSG_TRUNC set still gets the size and prefix checks first (0x0083A780,
    /// 0x0083A80E); if it passes them it logs Recv.Truncated, counts AddRecvError(1) (0x0083A8C4..0x0083A8C8)
    /// and is dropped, and the read loop goes on (0x0083AABA returns 1). <paramref name="raw"/> is the
    /// 1472 bytes the buffer holds.
    /// </summary>
    private void TruncatedDatagramLocked(byte[] raw, List<Action> effects)
    {
        var utc = DateTime.UtcNow;
        if (!UdpPrefixOkLocked(raw, utc, effects)) return;
        UdpReceiveErrors.Add(1);
        const string err = "Recv.Truncated: datagram larger than the 1472-byte receive buffer; dropped";
        effects.Add(() => Fan(Warning, err));
    }

    /// <summary>
    /// UDPTransport::HandleReceivedMessage: at least the prefix length, then the COZ\x03 prefix (B17,
    /// 0x0083A780, 0x0083A80E). False, with a trace and a warning, when the datagram fails either.
    /// </summary>
    private bool UdpPrefixOkLocked(byte[] raw, DateTime utc, List<Action> effects)
    {
        if (raw.Length >= ReliableHeader.UdpPrefixLength && raw.AsSpan(0, ReliableHeader.UdpPrefixLength).SequenceEqual(ReliableHeader.UdpPrefix))
            return true;
        string err = raw.Length < ReliableHeader.UdpPrefixLength
            ? $"frame too small ({raw.Length} < {ReliableHeader.UdpPrefixLength})"
            : "bad UDP prefix (expected COZ\\x03)";
        effects.Add(() => Fan(FrameTrace, new FrameEvent(false, utc, raw, null, err)));
        effects.Add(() => Fan(Warning, $"bad frame: {err}"));
        return false;
    }

    /// <summary>One datagram, in the order set out on the class. Returns a reason when the link must end.</summary>
    private string? ProcessDatagramLocked(byte[] raw, IPEndPoint? from, List<Action> effects)
    {
        if (!_running) return null;
        var utc = DateTime.UtcNow;

        // UDPTransport::HandleReceivedMessage: the COZ\x03 prefix.
        if (!UdpPrefixOkLocked(raw, utc, effects)) return null;
        var post = raw.AsSpan(ReliableHeader.UdpPrefixLength);

        // fidelity: M1-002
        // ReliableTransport::ReceiveData 0x00837632-42: no reliable header, so the bytes go to the receiver
        // as they are, with no connection lookup. R3: counted with AddRecvError(0) when under 10 bytes or
        // AddRecvError(2) for a bad prefix, then AddRecvError(4).
        if (post.Length < ReliableHeader.ReliableLength || !post[..ReliableHeader.ReliablePrefix.Length].SequenceEqual(ReliableHeader.ReliablePrefix))
        {
            bool tooSmall = post.Length < ReliableHeader.ReliableLength;
            string err = tooSmall
                ? $"reliable header too small ({post.Length} < {ReliableHeader.ReliableLength}); passed on as data"
                : "no RE\\x01 reliable prefix; passed on as data";
            ReliableReceiveErrors.Add(tooSmall ? 0 : 2);
            ReliableReceiveErrors.Add(4);
            effects.Add(() => Fan(FrameTrace, new FrameEvent(false, utc, raw, null, err)));
            effects.Add(() => Fan(Warning, err));
            DeliverDataLocked(post.ToArray(), from, effects);
            return null;
        }

        // FindConnection: the peer, on IP and port.
        if (from is null || _peer is null || !from.Equals(_peer))
        {
            var seen = from;
            effects.Add(() => Fan(Warning, $"datagram from unexpected {seen}"));
            return null;
        }
        var c = _conn;
        if (c is null)
        {
            var seen = from;
            effects.Add(() => Fan(Warning, $"no connection for {seen}"));
            return null;
        }

        var type = (ReliableMessageType)post[3];
        ushort seqMin = BinaryPrimitives.ReadUInt16LittleEndian(post[4..]);
        ushort seqMax = BinaryPrimitives.ReadUInt16LittleEndian(post[6..]);
        ushort ack = BinaryPrimitives.ReadUInt16LittleEndian(post[8..]);
        bool isReliable = seqMin != SequenceId.Invalid || seqMax != SequenceId.Invalid;
        var body = post[ReliableHeader.ReliableLength..];
        bool multiple = ReliableMessageTypes.IsMultiple(type);

        FrameCodec.TryDecode(raw, out var traced, out var traceErr);
        effects.Add(() => Fan(FrameTrace, new FrameEvent(false, utc, raw, traced, traceErr)));

        // fidelity: M1-016, M1-032
        // R18 / G2.6: every valid frame from a known connection runs UpdateLastAckedMessage (0x00837818),
        // before anything in its body is looked at, so a frame whose body turns out malformed still refreshes
        // lastRecv and applies its ack.
        bool anyAck = c.UpdateLastAckedMessage(ack);
        if (anyAck && _o.MaxPacketsToReSendOnAck > 0) c.SendOptimalUnAckedPackets(_o.MaxPacketsToReSendOnAck);
        // fidelity: M1-007
        if (isReliable)
        {
            if (!c.IsWaitingForAnyInRange(seqMin, seqMax))
            {
                // R16: out of range, only a type-9 frame is still walked; any other type is AddRecvError(5)
                // and dropped (0x008378EA, 0x00837A6A).
                if (type != ReliableMessageType.MultipleMixedMessages) { ReliableReceiveErrors.Add(5); return null; }
            }
            else
            {
                c.AckMessage(seqMax);
                if (_o.SendAckOnReceipt) c.Queue(ReliableMessageType.Ack, Array.Empty<byte>(), false, true);
            }
        }

        string? disc = null;
        if (multiple)
        {
            // fidelity: M1-007
            // R10: an invalid sub-type is AddRecvError(4) (0x0083790A..0x00837910) and a size overrun
            // AddRecvError(1) (0x00837926); either abandons the rest of the frame, the earlier sub-messages
            // having been handled already.
            ushort seq = seqMin; int o = 0;
            while (o < body.Length)
            {
                byte t = body[o];
                if (!ReliableMessageTypes.IsValid(t))
                {
                    ReliableReceiveErrors.Add(4);
                    int at = o;
                    effects.Add(() => Fan(Warning, $"invalid sub-message type {t} at body offset {at}; rest of frame dropped"));
                    break;
                }
                // A remainder of one or two bytes cannot hold the size: the engine reads it past the end of the
                // datagram (0x0083791C) and takes the overrun branch (0x00837926). That over-read is not
                // reproduced; the remainder is handled as the size overrun it produces.
                if (body.Length - o < FrameCodec.SubMessageOverhead)
                {
                    ReliableReceiveErrors.Add(1);
                    int at = o;
                    effects.Add(() => Fan(Warning, $"partial sub-message header at body offset {at}; rest of frame dropped"));
                    break;
                }
                int size = BinaryPrimitives.ReadUInt16LittleEndian(body[(o + 1)..]);
                int start = o + FrameCodec.SubMessageOverhead;
                if (start + size > body.Length)
                {
                    ReliableReceiveErrors.Add(1);
                    int at = start;
                    effects.Add(() => Fan(Warning, $"sub-message size {size} overruns body at offset {at}; rest of frame dropped"));
                    break;
                }
                var subType = (ReliableMessageType)t;
                bool subReliable = isReliable && !ReliableMessageTypes.IsAlwaysUnreliable(subType);
                HandleSubMessageLocked(subType, body.Slice(start, size).ToArray(), subReliable ? seq : SequenceId.Invalid, c, from, effects, ref disc);
                if (subReliable) seq = SequenceId.Next(seq);
                o = start + size;

                // fidelity: M1-038
                // Policy D8: once a DisconnectRequest has been handled, the rest of the frame is not processed.
                // The original deletes the connection there (0x008374A0) and keeps walking through the freed
                // pointer, which is not reproduced. A DisconnectRequest dropped by R12 (out of sequence) is not
                // handled, sets nothing, and the walk goes on as in the original.
                if (disc is not null) break;
            }
        }
        else
        {
            HandleSubMessageLocked(type, body.ToArray(), isReliable ? seqMin : SequenceId.Invalid, c, from, effects, ref disc);
        }
        return disc;
    }

    // fidelity: M1-003
    /// <summary>ReliableTransport::HandleSubMessage 0x00837418.</summary>
    private void HandleSubMessageLocked(ReliableMessageType type, byte[] payload, ushort seq, ReliableConnection c,
                                        IPEndPoint from, List<Action> effects, ref string? disc)
    {
        if (seq != SequenceId.Invalid && !c.AcceptReliable(seq)) return;
        switch (type)
        {
            case ReliableMessageType.ConnectionRequest: break; // host-only; robot never sends it
            case ReliableMessageType.ConnectionResponse:
                if (State == LinkState.Connecting) { State = LinkState.Connected; effects.Add(() => Fan(Connected)); }
                break;
            case ReliableMessageType.DisconnectRequest: disc = "peer sent DisconnectRequest"; break;
            case ReliableMessageType.SingleReliableMessage:
            case ReliableMessageType.SingleUnreliableMessage:
                DeliverDataLocked(payload, from, effects); break;
            case ReliableMessageType.MultiPartMessage:
                if (payload.Length > 2)
                {
                    int idx = payload[0], cnt = payload[1];
                    if (idx == _multipartNext)
                    {
                        if (idx == 1) { _multipart.Clear(); _multipartLast = cnt; }
                        _multipart.AddRange(payload.AsSpan(2).ToArray()); _multipartNext++;
                        if (idx == _multipartLast)
                        {
                            DeliverDataLocked(_multipart.ToArray(), from, effects);
                            _multipart.Clear(); _multipartNext = 1; _multipartLast = 0;
                        }
                    }
                    else
                    {
                        var w = $"multipart out of order {idx}/{cnt}, expected {_multipartNext}";
                        effects.Add(() => Fan(Warning, w));
                    }
                }
                break;
            case ReliableMessageType.Ack: break;
            case ReliableMessageType.Ping: c.ReceivePing(payload); break;
            default:
                var unhandled = $"unhandled sub-message type {type}";
                effects.Add(() => Fan(Warning, unhandled));
                break;
        }
    }

    /// <summary>
    /// RobotConnectionManager's gate on an arrived data message: delivered only while Connected
    /// (0x0062F724-2A) and from the peer's IP (0x0062F744); dropped otherwise, never held. The address test
    /// is TransportAddress::operator== 0x00838E92-A6, which compares the IP and not the port.
    /// </summary>
    private void DeliverDataLocked(byte[] payload, IPEndPoint? from, List<Action> effects)
    {
        if (State != LinkState.Connected || from is null || _peer is null || !from.Address.Equals(_peer.Address)) return;
        effects.Add(() => Fan(DataReceived, payload));
    }
}

/// <summary>
/// Counts of AddRecvError calls by error code, for diagnostics only: nothing in the transport reads them.
/// </summary>
public sealed class ReceiveErrorCounts
{
    private readonly Dictionary<int, int> _byCode = new();

    /// <summary>How many times AddRecvError(<paramref name="code"/>) has been counted.</summary>
    public int this[int code] { get { lock (_byCode) return _byCode.GetValueOrDefault(code); } }

    internal void Add(int code) { lock (_byCode) _byCode[code] = _byCode.GetValueOrDefault(code) + 1; }
}

/// <summary>
/// The transport's update schedule. <c>ReliableTransport::ChangeSyncMode</c> schedules its update every
/// 2 ms (<c>Dispatch::ScheduleCallback</c> 0x0083689E), and <c>TaskExecutor::ProcessDeferredQueue</c> re-arms
/// a repeating task at the <c>steady_clock::now()</c> it took when it picked the task up (0x007FC1BC) plus
/// the period (0x007FC36A-76). A late run is followed by the next one a full period after it began, never
/// by catch-up runs.
/// </summary>
internal sealed class TransportCadence
{
    private readonly INetClock _clock;
    private readonly double _periodMs;
    private double _dueMs;

    public TransportCadence(INetClock clock, double periodMs)
    {
        _clock = clock; _periodMs = periodMs; _dueMs = clock.NowMs + periodMs;
    }

    /// <summary>When the next update may run.</summary>
    public double DueMs => _dueMs;

    public double MsUntilDue => _dueMs - _clock.NowMs;

    /// <summary>If an update is due, re-arm for now + period and return true.</summary>
    public bool TryBegin()
    {
        double now = _clock.NowMs;
        if (now < _dueMs) return false;
        _dueMs = now + _periodMs;
        return true;
    }
}
