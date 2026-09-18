using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Cozmo.Protocol;

namespace Cozmo.Transport;

public enum LinkState { Idle, Connecting, Connected, Disconnected }

public sealed record FrameEvent(bool Outbound, DateTime Utc, byte[] Raw, Frame? Frame, string? Error);

/// <summary>
/// Client-side port of Anki::Util::ReliableTransport + UDPTransport for a single peer (the robot).
/// UDP framing: "COZ\x03" prefix, no CRC (RobotConnectionManager::Init), then the 10-byte reliable header.
/// Receive processing mirrors ReliableTransport::ReceiveData / HandleSubMessage exactly:
///  1. reject bad prefix; 2. UpdateLastAckedMessage(header.ack); 3. for reliable frames check
///  IsWaitingForAnyInRange(seqMin,seqMax) (a MultipleMixed frame is still processed for its unreliable content
///  when out of range) and AckMessage(seqMax); 4. walk sub-messages, assigning seq ids, dropping duplicates,
///  dispatching ConnectionResponse/Disconnect/data/multipart/ping.
///
/// Threading. Three worker threads: <c>cozmo-rx</c> blocks on the socket, <c>cozmo-tick</c> runs the
/// connection update every <see cref="TransportOptions.UpdateIntervalMs"/>, and <c>cozmo-dispatch</c> raises
/// every public event. Handlers therefore never run on the receive or tick threads, so a slow or throwing
/// handler cannot stall or kill the transport; events still arrive in the order they were produced. All
/// connection state is protected by <see cref="_lock"/>, which is never held while a handler runs.
/// </summary>
public sealed class ReliableTransport : IDisposable
{
    private readonly TransportOptions _o;
    private readonly INetClock _clock;
    private readonly object _lock = new();
    private Socket? _sock;
    private IPEndPoint? _peer;
    private ReliableConnection? _conn;
    private Thread? _rx, _tick, _dispatch;
    private BlockingCollection<Action>? _events;
    private volatile bool _running;
    private readonly List<byte> _multipart = new();
    private int _multipartNext = 1, _multipartLast;

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

    private void Safe(Action a)
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

    // ------------------------------------------------------------- offline mode

    /// <summary>
    /// A transport with no socket, for replay/conformance tests: incoming datagrams are fed with
    /// <see cref="ProcessIncoming"/>, outgoing frames are captured in <see cref="OfflineOutbound"/>.
    /// Events are raised inline on the calling thread.
    /// </summary>
    public static ReliableTransport CreateOffline(TransportOptions? options = null, INetClock? clock = null)
    {
        var t = new ReliableTransport(options, clock);
        t._conn = new ReliableConnection(t._o, t._clock, (ty, mn, mx, body) =>
        {
            var hdr = new ReliableHeader(ty, mn, mx, t._conn!.LastInAcked);
            var raw = new byte[ReliableHeader.Length + body.Length]; hdr.Write(raw); body.CopyTo(raw, ReliableHeader.Length);
            FrameCodec.TryDecode(raw, out var f, out _);
            if (f is not null) t.OfflineOutbound.Add(f);
            t.Raise(() => t.FrameTrace?.Invoke(new FrameEvent(true, DateTime.UtcNow, raw, f, null)));
        });
        t._running = true; t.State = LinkState.Connecting;
        return t;
    }

    /// <summary>Offline only: queue the initial ConnectionRequest like <see cref="Connect"/> does.</summary>
    public void OfflineConnect() { lock (_lock) _conn!.Queue(ReliableMessageType.ConnectionRequest, Array.Empty<byte>(), true, true); }

    /// <summary>Offline only: run one connection update tick.</summary>
    public bool OfflineTick() { lock (_lock) return _conn!.Update(); }

    // ------------------------------------------------------------------ connect

    /// <summary>Open the socket (ephemeral local port, like the engine's UDPTransport client) and send ConnectionRequest (reliable seq 1).</summary>
    public void Connect(IPAddress robot, int? port = null)
    {
        BlockingCollection<Action> q;
        lock (_lock)
        {
            if (_running) throw new InvalidOperationException("already running");
            if (_rx is not null || _tick is not null || _dispatch is not null)
                throw new InvalidOperationException("the previous connection has not finished shutting down");

            // Every connection starts from clean session state; a half-assembled multipart message from the
            // last connection must not be completed with fragments from this one.
            _multipart.Clear(); _multipartNext = 1; _multipartLast = 0;
            OfflineOutbound.Clear();
            HandlerFaults = 0;

            _peer = new IPEndPoint(robot, port ?? _o.RobotPort);
            _sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _sock.Bind(new IPEndPoint(IPAddress.Any, 0));
            _sock.ReceiveTimeout = 50;
            _conn = new ReliableConnection(_o, _clock, SendFrame);
            _running = true; State = LinkState.Connecting;

            _events = q = new BlockingCollection<Action>();
            _dispatch = new Thread(() => DispatchLoop(q)) { IsBackground = true, Name = "cozmo-dispatch" };
            _rx = new Thread(RxLoop) { IsBackground = true, Name = "cozmo-rx" };
            _tick = new Thread(TickLoop) { IsBackground = true, Name = "cozmo-tick" };
            _dispatch.Start(); _rx.Start(); _tick.Start();
            _conn.Queue(ReliableMessageType.ConnectionRequest, Array.Empty<byte>(), reliable: true, flush: true);
        }
    }

    /// <summary>Send a CLAD robot message. Reliable by default (official engine sends everything reliable except a few unreliable streams).</summary>
    public void Send(RobotMessage m, bool reliable = true, bool flush = false) => SendData(m.ToBytes(), reliable, flush);

    public void SendData(byte[] cladMessage, bool reliable = true, bool flush = false)
    {
        lock (_lock)
        {
            if (_conn is null || State is LinkState.Idle or LinkState.Disconnected) throw new InvalidOperationException("not connected");
            _conn.Queue(reliable ? ReliableMessageType.SingleReliableMessage : ReliableMessageType.SingleUnreliableMessage, cladMessage, reliable, flush);
        }
    }

    /// <summary>Official ReliableTransport::Disconnect: reliable DisconnectRequest then drop the connection (we flush once before closing).</summary>
    public void Disconnect(string reason = "requested")
    {
        lock (_lock)
        {
            if (_conn is not null && State is LinkState.Connected or LinkState.Connecting)
            {
                try { _conn.Queue(ReliableMessageType.DisconnectRequest, Array.Empty<byte>(), true, true); } catch { }
            }
        }
        Thread.Sleep(40);
        Shutdown(reason);
    }

    /// <summary>
    /// Stops the workers, closes the socket and reports the reason once. Safe to call from any thread,
    /// including a worker: a thread never joins itself. After it returns, <see cref="Connect"/> may be used
    /// again, and no worker from the previous connection survives.
    /// </summary>
    private void Shutdown(string reason)
    {
        Thread? rx, tick, dispatch;
        BlockingCollection<Action>? q;
        bool notify;
        lock (_lock)
        {
            notify = _running;
            if (!notify && _rx is null && _tick is null && _dispatch is null) return;   // already down
            _running = false;
            State = LinkState.Disconnected;
            try { _sock?.Close(); } catch { }
            _sock = null;
            _multipart.Clear(); _multipartNext = 1; _multipartLast = 0;
            rx = _rx; tick = _tick; dispatch = _dispatch; q = _events;
            _rx = null; _tick = null;
        }

        Join(rx); Join(tick);
        if (notify) Raise(() => Disconnected?.Invoke(reason));

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

    public void Dispose() => Shutdown("disposed");

    // ------------------------------------------------------------------ wire

    private void SendFrame(ReliableMessageType type, ushort seqMin, ushort seqMax, byte[] body)
    {
        // called under _lock by the connection, so nothing here may run a user handler inline
        var hdr = new ReliableHeader(type, seqMin, seqMax, _conn!.LastInAcked);
        var raw = new byte[ReliableHeader.Length + body.Length];
        hdr.Write(raw); body.CopyTo(raw, ReliableHeader.Length);
        try { _sock?.SendTo(raw, _peer!); }
        catch (ObjectDisposedException) { return; }                       // closed under us during shutdown
        catch (SocketException e) { Raise(() => Warning?.Invoke($"sendto failed: {e.SocketErrorCode} ({e.Message})")); }
        if (FrameTrace is not null)
        {
            FrameCodec.TryDecode(raw, out var f, out var err);
            var utc = DateTime.UtcNow;
            Raise(() => FrameTrace?.Invoke(new FrameEvent(true, utc, raw, f, err)));
        }
    }

    private void RxLoop()
    {
        var buf = new byte[2048];
        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            int n;
            try { n = _sock!.ReceiveFrom(buf, ref from); }
            catch (SocketException e) when (e.SocketErrorCode is SocketError.TimedOut or SocketError.WouldBlock)
            {
                continue;
            }
            catch (SocketException e) when (e.SocketErrorCode is SocketError.ConnectionReset
                                                or SocketError.NetworkReset or SocketError.MessageSize)
            {
                // UDP is connectionless: these report a problem with one earlier datagram (typically an ICMP
                // port-unreachable because the robot is not listening yet), not a dead socket. Keep going and
                // let the connection timeout decide, exactly as the engine does.
                var code = e.SocketErrorCode;
                Raise(() => Warning?.Invoke($"datagram rejected by the network: {code}"));
                continue;
            }
            catch (ObjectDisposedException) { return; }                   // socket closed by Shutdown
            catch (SocketException e)
            {
                if (_running) Shutdown($"socket error: {e.SocketErrorCode} ({e.Message})");
                return;
            }
            catch (Exception e)
            {
                if (_running) Shutdown($"receive failed: {e.GetType().Name}: {e.Message}");
                return;
            }

            if (from is IPEndPoint ip && !ip.Address.Equals(_peer!.Address))
            {
                var seen = ip;
                Raise(() => Warning?.Invoke($"datagram from unexpected {seen}"));
                continue;
            }
            ProcessIncoming(buf.AsSpan(0, n).ToArray());
        }
    }

    /// <summary>Feed a raw datagram (also used by the conformance replay tool).</summary>
    public void ProcessIncoming(byte[] raw)
    {
        var utc = DateTime.UtcNow;
        if (!FrameCodec.TryDecode(raw, out var frame, out var err))
        {
            Raise(() => FrameTrace?.Invoke(new FrameEvent(false, utc, raw, null, err)));
            Raise(() => Warning?.Invoke($"bad frame: {err}"));
            return;
        }
        Raise(() => FrameTrace?.Invoke(new FrameEvent(false, utc, raw, frame, null)));

        var deliver = new List<byte[]>();
        var warnings = new List<string>();
        bool connected = false; string? disc = null;
        lock (_lock)
        {
            var c = _conn; if (c is null || !_running) return;
            var h = frame!.Header;
            bool anyAck = c.UpdateLastAckedMessage(h.Ack);
            if (anyAck && _o.MaxPacketsToReSendOnAck > 0) c.SendOptimalUnAckedPackets(_o.MaxPacketsToReSendOnAck);
            if (h.IsReliable)
            {
                bool waiting = c.IsWaitingForAnyInRange(h.SeqMin, h.SeqMax);
                if (!waiting)
                {
                    if (h.Type != ReliableMessageType.MultipleMixedMessages) return; // out of order / duplicate frame: ignore entirely
                }
                else
                {
                    c.AckMessage(h.SeqMax);
                    if (_o.SendAckOnReceipt) c.Queue(ReliableMessageType.Ack, Array.Empty<byte>(), false, true);
                }
            }
            foreach (var sm in frame.Messages)
            {
                if (sm.IsReliable && !c.AcceptReliable(sm.Seq)) continue;
                switch (sm.Type)
                {
                    case ReliableMessageType.ConnectionRequest: break; // host-only; robot never sends it
                    case ReliableMessageType.ConnectionResponse:
                        if (State == LinkState.Connecting) { State = LinkState.Connected; connected = true; }
                        break;
                    case ReliableMessageType.DisconnectRequest: disc = "peer sent DisconnectRequest"; break;
                    case ReliableMessageType.SingleReliableMessage:
                    case ReliableMessageType.SingleUnreliableMessage:
                        deliver.Add(sm.Payload); break;
                    case ReliableMessageType.MultiPartMessage:
                        if (sm.Payload.Length > 2)
                        {
                            int idx = sm.Payload[0], cnt = sm.Payload[1];
                            if (idx == _multipartNext)
                            {
                                if (idx == 1) { _multipart.Clear(); _multipartLast = cnt; }
                                _multipart.AddRange(sm.Payload.AsSpan(2).ToArray()); _multipartNext++;
                                if (idx == _multipartLast) { deliver.Add(_multipart.ToArray()); _multipart.Clear(); _multipartNext = 1; _multipartLast = 0; }
                            }
                            else
                            {
                                warnings.Add($"multipart out of order {idx}/{cnt}, expected {_multipartNext}");
                                _multipart.Clear(); _multipartNext = 1; _multipartLast = 0;
                            }
                        }
                        break;
                    case ReliableMessageType.Ack: break;
                    case ReliableMessageType.Ping: c.ReceivePing(sm.Payload); break;
                    default:
                        warnings.Add($"unhandled sub-message type {sm.Type}");
                        break;
                }
            }
        }
        foreach (var w in warnings) Raise(() => Warning?.Invoke(w));
        if (connected) Raise(() => Connected?.Invoke());
        foreach (var d in deliver) Raise(() => DataReceived?.Invoke(d));
        if (disc is not null) Shutdown(disc);
    }

    private void TickLoop()
    {
        // The engine schedules Update every 2 ms. Without raising the timer resolution a 1 ms sleep on
        // Windows lasts about 15.6 ms, which turns the 2 ms cadence into roughly 15 ms and quietly breaks
        // the resend and packet-separation timing this transport is meant to reproduce.
        using var _ = new HighResolutionTimer();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double next = 0;
        while (_running)
        {
            bool alive = true;
            lock (_lock) { if (_conn is not null && _running) alive = _conn.Update(); }
            if (!alive)
            {
                Shutdown($"connection timed out (> {_o.ConnectionTimeoutMs} ms without any datagram)");
                return;
            }
            next += _o.UpdateIntervalMs;
            double wait = next - sw.Elapsed.TotalMilliseconds;
            if (wait > 1.5) Thread.Sleep(1);
            else if (wait > 0) Thread.SpinWait(200);
            else next = sw.Elapsed.TotalMilliseconds;   // fell behind: resynchronise rather than burst
        }
    }
}
