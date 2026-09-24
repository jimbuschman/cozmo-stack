using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
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
/// Threading (M1-010, M1-020, M1-021, M1-035; host structure M1-014). Production runs the transport
/// asynchronously (B15). At construction the transport requests its repeating 2 ms update (R35, B14): the
/// <c>cozmo-transport-timer</c> thread is the deferred scheduler (G1.6..G1.8) and only posts a copy of the
/// update when it is due; the <c>cozmo-transport</c> thread is the RelTransport executor (G1.9, G1.10) and
/// runs everything posted to it one item at a time, in order. Connect, SendData, Send and Disconnect post
/// closures to that same executor (R37, R38), so sends and updates are FIFO on one thread and a caller never
/// waits for a running update to send. The update keeps being requested for the life of the transport, after
/// a connection ends as well. <c>cozmo-dispatch</c> raises every public event, so handlers never run on the
/// executor while a link is up. Connection state is protected by <see cref="_lock"/> (the transport mutex
/// R34/R37 name). In async mode no handler runs under it; in sync mode Raise runs handlers inline, so a
/// handler raised from SendFrame runs with it held.
///
/// Sync mode (R36) is the test and offline seam: no timer exists and the owner calls <see cref="Pump"/> or
/// <see cref="OfflineTick"/>. Only QueueMessage changes with the mode: in sync mode it calls SendMessage
/// directly, on the caller's thread (R36, 0x00836B42..0x00836B5C). QueueAction posts to the RelTransport
/// executor in both modes (R37; B21), so Disconnect and Dispose are posted in sync mode too, and the
/// executor exists in both.
/// </summary>
public sealed class ReliableTransport : IDisposable
{
    private readonly TransportOptions _o;
    private readonly INetClock _clock;
    private readonly object _lock = new();
    /// <summary>
    /// Guards the caller-side lifecycle fields (<see cref="_linkOpen"/>, <see cref="_disposed"/>) and the
    /// posts callers make to the executor. It is held only for those fields and for a post, never while
    /// <see cref="_lock"/> is taken and never while a user handler runs, so taking it never waits for the
    /// transport thread and a handler can always reach the transport.
    /// </summary>
    private readonly object _life = new();
    /// <summary>A Connect has been accepted and its link has not finished shutting down.</summary>
    private bool _linkOpen;
    /// <summary>Written under <see cref="_life"/> before the dispose closure is posted; read under <see cref="_lock"/> by a sync-mode Connect.</summary>
    private volatile bool _disposed;
    private Socket? _sock;
    private IPEndPoint? _peer;
    private volatile ReliableConnection? _conn;
    private volatile Thread? _dispatch;
    private BlockingCollection<Action>? _events;
    /// <summary>Set on a dispatch thread to the transport it belongs to, so Dispose can tell it is running there.</summary>
    [ThreadStatic] private static ReliableTransport? t_dispatchOwner;
    private volatile bool _running;
    private volatile LinkState _state = LinkState.Idle;

    // fidelity: M1-010, M1-020, M1-021, M1-035, M1-014
    /// <summary>
    /// The RelTransport executor (G1.9, G1.10). It exists in both modes: QueueAction posts to it whatever the
    /// mode (R37; B21), and sync mode only lacks the repeating update (R36).
    /// </summary>
    private readonly SerialExecutor _exec;
    /// <summary>The deferred scheduler holding the one repeating 2 ms entry (G1.1..G1.8); null in sync mode.</summary>
    private readonly TransportScheduler? _sched;
    /// <summary>G1.10: once the scheduled callback's handle has gone, posted copies of it are skipped.</summary>
    private volatile bool _tickHandleExpired;
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

    public LinkState State { get => _state; private set => _state = value; }
    public event Action? Connected;
    public event Action<string>? Disconnected;
    /// <summary>A complete application payload (a CLAD robot message: tag + body) received in order.</summary>
    public event Action<byte[]>? DataReceived;
    /// <summary>Every raw frame in both directions (for logging / conformance capture).</summary>
    public event Action<FrameEvent>? FrameTrace;
    public event Action<string>? Warning;

    /// <summary>Handlers that threw, counted so a test or a caller can notice swallowed failures.</summary>
    public int HandlerFaults { get; private set; }

    /// <summary>A production transport: async mode, with its 2 ms update requested here (R35, B14, B15).</summary>
    public ReliableTransport(TransportOptions? options = null, INetClock? clock = null) : this(options, clock, manualPump: false) { }

    /// <summary>
    /// <paramref name="manualPump"/> true gives sync mode (R36), the test seam: no repeating update, the caller
    /// runs each update with <see cref="Pump"/>, no dispatch thread (events are raised inline), and
    /// QueueMessage calls SendMessage on the caller's thread. The RelTransport executor still exists and still
    /// runs what QueueAction posts (Disconnect, Dispose); <see cref="Flush"/> waits for it.
    /// </summary>
    internal ReliableTransport(TransportOptions? options, INetClock? clock, bool manualPump)
    {
        _o = options ?? TransportOptions.EngineDefaults; _clock = clock ?? new StopwatchClock();
        ManualPump = manualPump;

        // fidelity: M1-010, M1-021, M1-014, M1-020
        // R35/B14: the ctor creates the RelTransport queue and ChangeSyncMode(false) schedules the update
        // every 2 ms (ScheduleCallback 0x0083689E); it is requested once, here, not per connection. Sync mode
        // (R36) has no timer; the queue itself exists in both modes.
        // MISSING: R35/B14 create the queue at "priority 3"; the rows do not say what host thread priority
        // that is, so both threads keep the default priority.
        _exec = new SerialExecutor("cozmo-transport");
        _exec.Start();
        if (manualPump) return;
        _sched = new TransportScheduler(TransportScheduler.HostNowNs, _o.UpdateIntervalMs, PostTick);
        _sched.Start();
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
    /// Test seam, sync mode (R36): no timer or worker threads, events are raised inline, and the caller
    /// runs each transport update itself with <see cref="Pump"/>. False in production. Chosen at
    /// construction, because the async-mode update is requested there.
    /// </summary>
    internal bool ManualPump { get; }

    /// <summary>One item run by the executor: what it was, and its number in the order it was posted.</summary>
    internal readonly record struct ExecutorItem(string Kind, long Seq);

    /// <summary>Test seam: called on the executor thread just before each posted item runs. Null in production.</summary>
    internal Action<ExecutorItem>? ExecutorTrace { get; set; }

    /// <summary>Test seam: the executor (both modes) and the scheduler (async mode only; null in sync mode).</summary>
    internal SerialExecutor Executor => _exec;
    internal TransportScheduler? Scheduler => _sched;

    /// <summary>
    /// Test seam: waits until everything posted to the executor before this call has run. True if it did
    /// within <paramref name="timeout"/>, or if the executor has been completed and accepts nothing more.
    /// </summary>
    internal bool Flush(TimeSpan timeout)
    {
        var done = new ManualResetEventSlim();
        if (!_exec.Post(() => done.Set())) return true;   // completed: nothing more will run
        return done.Wait(timeout);
    }

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
        t_dispatchOwner = this;
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
        var t = new ReliableTransport(options, clock, manualPump: true);
        t._linkOpen = true;
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

    // fidelity: M1-035, M1-020
    /// <summary>
    /// Posts to the RelTransport executor, behind every update and closure already posted. The caller holds
    /// <see cref="_life"/>, so nothing a caller posts can land after the dispose closure. False when the
    /// executor accepts nothing more (it has been completed).
    /// </summary>
    private bool TryPostLifeLocked(string kind, Action action) =>
        _exec.Post(seq => { ExecutorTrace?.Invoke(new ExecutorItem(kind, seq)); action(); });

    // fidelity: M1-035, M1-020
    /// <summary>
    /// R37 / QueueAction 0x00836FF6 (B21: RT::Disconnect queues an action): the action is posted to the
    /// RelTransport executor in both modes; there is no sync-mode branch. After Dispose has begun, or once the
    /// executor accepts nothing more, the post is refused with <see cref="ObjectDisposedException"/>.
    /// </summary>
    private void QueueAction(string kind, Action action)
    {
        lock (_life)
        {
            if (_disposed || !TryPostLifeLocked(kind, action)) throw new ObjectDisposedException(nameof(ReliableTransport));
        }
    }

    // fidelity: M1-035, M1-020
    /// <summary>
    /// R37 QueueMessage 0x00836B66..0x00836BE4: in async mode the time is read when the message is posted, and
    /// the closure (0x00837DEA) takes the transport mutex and calls SendMessage with that posted time. In sync
    /// mode (R36, 0x00836B42..0x00836B5C) SendMessage is called directly, on the caller's thread, with time 0.0
    /// (0x00836B48/0x00836B4C, from the batch 2b-i verification; not in the frozen rows).
    /// </summary>
    private void QueueMessage(ReliableMessageType type, byte[] payload, bool reliable, bool flush)
    {
        if (ManualPump)
        {
            var now = new List<Action>();
            lock (_lock) SendMessageLocked(type, payload, reliable, flush, 0.0, now);
            foreach (var a in now) Raise(a);
            return;
        }
        double posted = _clock.NowMs;
        QueueAction("send", () =>
        {
            var effects = new List<Action>();
            lock (_lock) SendMessageLocked(type, payload, reliable, flush, posted, effects);
            foreach (var a in effects) Raise(a);
        });
    }

    // fidelity: M1-035
    /// <summary>
    /// ReliableTransport::SendMessage, as the posted closures reach it. G2.1: FindConnection(addr, create =
    /// type == 1); G2.2: a miss with create makes the connection, whose ctor stamps lastRecv with the
    /// current time (G2.3). R13: any other type with no connection is dropped with the warning
    /// "unconnected destination".
    /// </summary>
    private void SendMessageLocked(ReliableMessageType type, byte[] payload, bool reliable, bool flush, double postedMs, List<Action> effects)
    {
        var c = _conn;
        if (c is null)
        {
            if (type != ReliableMessageType.ConnectionRequest)
            {
                var dest = _peer;
                effects.Add(() => Fan(Warning, $"unconnected destination {dest}; {type} dropped"));
                return;
            }
            c = _conn = new ReliableConnection(_o, _clock, SendFrame);
        }
        // MISSING: R37 says SendMessage is called with the posted time, but no row says what SendMessage does
        // with it (which PendingMessage field, if any, it sets), so postedMs changes nothing here.
        _ = postedMs;
        c.Queue(type, payload, reliable, flush);
    }

    /// <summary>
    /// Open the socket (ephemeral local port, like the engine's UDPTransport client) and send ConnectionRequest
    /// (reliable seq 1). The socket is opened here, so a failure to open it is thrown to the caller. R38 / B21:
    /// RT::Connect queues QueueMessage(type 1, reliable, flush 1) (0x0083710E, 0x0083711C): in async mode the
    /// link is set up by that posted closure (R37), in order with everything posted before it, and its
    /// SendMessage creates the connection (G2.1, G2.2); in sync mode (R36) both happen here.
    /// <see cref="State"/> is Connecting when this returns.
    ///
    /// Once Dispose has begun this refuses before opening anything. If Dispose begins while the socket is
    /// being opened, or the executor accepts nothing more, the link is refused: the socket is closed, the link
    /// released, and <see cref="ObjectDisposedException"/> thrown. In async mode the post is made under
    /// <see cref="_life"/>, as Dispose's is, so it lands before the dispose closure or not at all. In sync mode
    /// the link is set up under <see cref="_lock"/> only, after checking that Dispose has not begun; the
    /// dispose closure needs that same lock, so it runs after the setup and ends the link.
    /// </summary>
    public void Connect(IPAddress robot, int? port = null)
    {
        lock (_life)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ReliableTransport));
            if (_linkOpen) throw new InvalidOperationException("already running");
            _linkOpen = true;
        }
        var peer = new IPEndPoint(robot, port ?? _o.RobotPort);
        Socket sock;
        try { sock = OpenSocket(); }
        catch { lock (_life) _linkOpen = false; throw; }
        // No link is up (the previous one has finished shutting down), so nothing else writes State now.
        var previous = State;
        State = LinkState.Connecting;

        // fidelity: M1-035, M1-020
        var effects = new List<Action>();
        bool refused;
        if (ManualPump)
        {
            // R36: sync mode calls SendMessage directly, with time 0.0 (see QueueMessage). Not under _life.
            lock (_lock)
            {
                refused = _disposed;
                if (!refused)
                {
                    BeginLinkLocked(peer, sock);
                    SendMessageLocked(ReliableMessageType.ConnectionRequest, Array.Empty<byte>(), true, true, 0.0, effects);
                }
            }
        }
        else lock (_life)
        {
            refused = _disposed;
            if (!refused)
            {
                double posted = _clock.NowMs;
                refused = !TryPostLifeLocked("connect", () =>
                {
                    var fx = new List<Action>();
                    lock (_lock)
                    {
                        BeginLinkLocked(peer, sock);
                        SendMessageLocked(ReliableMessageType.ConnectionRequest, Array.Empty<byte>(), true, true, posted, fx);
                    }
                    foreach (var a in fx) Raise(a);
                });
            }
        }
        if (refused)
        {
            try { sock.Dispose(); } catch { }
            State = previous;
            lock (_life) _linkOpen = false;
            throw new ObjectDisposedException(nameof(ReliableTransport));
        }
        foreach (var a in effects) Raise(a);
    }

    /// <summary>The per-link host state a Connect sets up, on the executor in async mode.</summary>
    private void BeginLinkLocked(IPEndPoint peer, Socket sock)
    {
        // Every connection starts from clean session state; a half-assembled multipart message from the
        // last connection must not be completed with fragments from this one.
        _multipart.Clear(); _multipartNext = 1; _multipartLast = 0;
        OfflineOutbound.Clear();
        HandlerFaults = 0;

        _peer = peer;
        _sock = sock;
        _conn = null;
        if (!ManualPump)
        {
            var q = new BlockingCollection<Action>();
            _events = q;
            _dispatch = new Thread(() => DispatchLoop(q)) { IsBackground = true, Name = "cozmo-dispatch" };
            _dispatch.Start();
        }
        _running = true;
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
        if (_conn is null || State is not LinkState.Connected) throw new InvalidOperationException("not connected");
        SendData(bytes, reliable: true, flush: false);
    }

    /// <summary>
    /// ReliableTransport::SendData 0x008370E4: queue one message with the given delivery options (type 4 when
    /// reliable, 5 when not, and the caller's flush). The manager's state gate is not part of this layer
    /// (see <see cref="Send"/>); this refuses only when no link is up to queue on. In async mode the message
    /// is posted (R37) and this returns without waiting for it to be queued.
    /// </summary>
    public void SendData(byte[] cladMessage, bool reliable = true, bool flush = false)
    {
        // In async mode the connection is created by the posted Connect closure, so only the link state is
        // looked at here; in sync mode the connection is already there whenever a link is up.
        if ((ManualPump && _conn is null) || State is LinkState.Idle or LinkState.Disconnected)
            throw new InvalidOperationException("not connected");
        QueueMessage(reliable ? ReliableMessageType.SingleReliableMessage : ReliableMessageType.SingleUnreliableMessage, cladMessage, reliable, flush);
    }

    // fidelity: M1-035
    /// <summary>
    /// Official ReliableTransport::Disconnect: it queues an action (B21, closure 0x00837FFA) that sends one
    /// reliable, flushed DisconnectRequest through the normal send path (0x0083801C), then deletes the
    /// connection at once (0x0083802A → DeleteConnection 0x008375F0). Whether the request reaches the wire is
    /// up to that one send attempt; nothing waits for it. In async mode this returns before the action has
    /// run, so <see cref="State"/> changes when it does.
    /// </summary>
    public void Disconnect(string reason = "requested")
    {
        lock (_life)
        {
            if (_disposed) return;
            // B21 / R37: the queued action acts on whatever connection exists when it runs.
            bool posted = TryPostLifeLocked("disconnect", () =>
            {
                lock (_lock) DisconnectLocked();
                Shutdown(reason);
            });
            if (!posted) throw new ObjectDisposedException(nameof(ReliableTransport));
        }
    }

    private void DisconnectLocked()
    {
        if (_conn is not null && State is LinkState.Connected or LinkState.Connecting)
        {
            // MISSING: B21 gives the closure's call as SendMessage(1, addr, null, 0, type 3, 1) and no row says
            // what time it passes; SendMessageLocked discards the time (see there), so NaN is passed.
            try { SendMessageLocked(ReliableMessageType.DisconnectRequest, Array.Empty<byte>(), true, true, double.NaN, new List<Action>()); } catch { }
        }
        _conn = null;
    }

    /// <summary>
    /// Ends the link: closes the socket and reports the reason once. Safe to call from any thread, including
    /// the executor; a thread never joins itself. The 2 ms update is not stopped: it keeps being requested
    /// for the life of the transport (R35). After it returns, <see cref="Connect"/> may be used again.
    /// </summary>
    private void Shutdown(string reason)
    {
        Thread? dispatch;
        BlockingCollection<Action>? q;
        bool notify;
        lock (_lock)
        {
            notify = _running;
            if (!notify && _dispatch is null) return;   // already down
            _running = false;
            State = LinkState.Disconnected;
            try { _sock?.Close(); } catch { }
            _sock = null;
            _multipart.Clear(); _multipartNext = 1; _multipartLast = 0;
            dispatch = _dispatch; q = _events;
        }

        if (notify) Raise(() => Fan(Disconnected, reason));

        if (q is not null)
        {
            try { q.CompleteAdding(); } catch (ObjectDisposedException) { }
            if (dispatch is not null && dispatch != Thread.CurrentThread && dispatch.IsAlive) dispatch.Join(JoinTimeout);
        }
        lock (_lock)
        {
            if (ReferenceEquals(_events, q)) { _events = null; _dispatch = null; }
        }
        lock (_life) _linkOpen = false;
    }

    /// <summary>
    /// <c>~RobotConnectionManager</c> 0x0062EE98: <c>DisconnectCurrent</c> 0x0062EF20, which disconnects the
    /// peer (0x0062EF52), and only then <c>StopClient</c> (0x0062EEA2). A Connected transport therefore
    /// disconnects exactly as <see cref="Disconnect"/> does before its socket is closed. A transport that is
    /// still Connecting sends nothing.
    ///
    /// The disconnect is posted like every other action (QueueAction, both modes), behind whatever was posted
    /// before it, and the host threads are then released: the timer stops, and the executor finishes what it
    /// holds and ends. Dispose waits for that, up to the join bound, except on this transport's own dispatch
    /// thread (which the executor's shutdown joins) or on the executor itself.
    /// </summary>
    public void Dispose()
    {
        lock (_life)
        {
            if (_disposed) return;
            _disposed = true;
            TryPostLifeLocked("dispose", () =>
            {
                lock (_lock) { if (State == LinkState.Connected) DisconnectLocked(); }
                Shutdown("disposed");
                _tickHandleExpired = true;   // G1.10: copies posted after this are skipped
            });
        }

        // fidelity: M1-014
        _sched?.Stop();
        _exec.Complete();
        // The self-join skip: on this transport's dispatch thread the executor's Shutdown is joining this very
        // thread, and in a handler raised inline under _lock (sync mode) the dispose closure needs the lock this
        // thread holds; waiting for the executor in either case would only run out the join bound.
        if (t_dispatchOwner != this && !Monitor.IsEntered(_lock)) _exec.Join(JoinTimeout);
    }

    // ------------------------------------------------------------------ wire

    private void SendFrame(ReliableMessageType type, ushort seqMin, ushort seqMax, byte[] body)
    {
        // called under _lock by the connection; events go through Raise, which in async mode hands them to the
        // dispatch thread and in sync mode runs them inline under _lock
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

    // fidelity: M1-010, M1-021
    /// <summary>
    /// Called by the scheduler when the 2 ms entry is due: G1.7, a copy of the callback is posted to the
    /// executor (AddTaskHolder 0x007FC270); the scheduler thread never runs it.
    /// </summary>
    private void PostTick() => _exec.Post(seq => { ExecutorTrace?.Invoke(new ExecutorItem("tick", seq)); RunTick(); });

    /// <summary>
    /// The R35 lambda (0x008383CE) as each posted copy runs it on the executor: ReliableTransport::Update,
    /// here <see cref="Pump"/>. G1.10: a copy whose handle has expired (the transport has been disposed) is
    /// skipped. With no link up the update finds nothing to do, and it goes on being run all the same.
    /// Not reproduced in this batch: the lambda's +0xA1 write on a false return (R35, B36; M1-015).
    /// </summary>
    private void RunTick()
    {
        if (_tickHandleExpired) return;
        Pump();
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

// fidelity: M1-021, M1-010, M1-013, M1-014
/// <summary>
/// The deferred side of the engine's TaskExecutor, holding the transport's one repeating entry
/// (WakeAfterRepeat, G1.1). The first time is steady_clock::now() + period × 1e6 ns (G1.2), so the first run
/// is one period after scheduling. Each pass reads now (G1.7, 0x007FC1BC); if now is before the entry's time
/// it waits; otherwise it posts one copy to the executor (AddTaskHolder, G1.7) and re-arms the entry at that
/// now + period (G1.8): a fixed delay from when the entry was seen due, with missed periods not added back.
/// This thread never runs the callback itself.
///
/// Host realisation: a dedicated thread (M1-014) waits with the raised timer resolution (M1-013), sleeping
/// 1 ms while more than 1.5 ms remain and spinning otherwise, in place of wait_until (G1.6). Times are
/// nanoseconds on the host's monotonic clock.
/// </summary>
internal sealed class TransportScheduler
{
    private readonly Func<long> _nowNs;
    private readonly long _periodNs;
    private readonly Action _post;
    private long _dueNs;
    private long _posted;
    private volatile bool _stop;
    private Thread? _thread;

    internal TransportScheduler(Func<long> nowNs, double periodMs, Action post)
    {
        _nowNs = nowNs; _post = post;
        _periodNs = (long)(periodMs * 1_000_000);   // G1.2: period × 1e6 ns (0x007FCEBE..0x007FCECA)
        _dueNs = nowNs() + _periodNs;               // G1.2: first time = now + period (0x007FCED2..0x007FCED6)
    }

    /// <summary>When the entry is next due, in host nanoseconds.</summary>
    internal long DueNs => Volatile.Read(ref _dueNs);

    /// <summary>How many copies have been posted so far.</summary>
    internal long Posted => Interlocked.Read(ref _posted);

    internal int? ThreadId => _thread?.ManagedThreadId;

    /// <summary>One pass of ProcessDeferredQueue over the entry. True if a copy was posted.</summary>
    internal bool Pass()
    {
        long now = _nowNs();                         // G1.7: now (0x007FC1BC)
        if (now < Volatile.Read(ref _dueNs)) return false;   // G1.7: not due (0x007FC1C8..0x007FC1D6)
        _post();                                     // G1.7: a copy to the immediate queue (0x007FC270)
        Interlocked.Increment(ref _posted);
        Volatile.Write(ref _dueNs, now + _periodNs); // G1.8: re-added at that now + period (0x007FC352..0x007FC376)
        return true;
    }

    internal void Start()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "cozmo-transport-timer" };
        _thread.Start();
    }

    /// <summary>Host teardown: the thread ends and posts nothing more.</summary>
    internal void Stop()
    {
        _stop = true;
        var t = _thread;
        if (t is not null && t != Thread.CurrentThread && t.IsAlive) t.Join(TimeSpan.FromSeconds(2));
    }

    private void Loop()
    {
        using var _ = new HighResolutionTimer();
        while (!_stop)
        {
            if (Pass()) continue;
            long wait = Volatile.Read(ref _dueNs) - _nowNs();
            if (wait > 1_500_000) Thread.Sleep(1);
            else Thread.SpinWait(200);
        }
    }

    /// <summary>The host's monotonic clock in nanoseconds, standing in for steady_clock::now().</summary>
    internal static long HostNowNs() => (long)((Int128)Stopwatch.GetTimestamp() * 1_000_000_000 / Stopwatch.Frequency);
}

// fidelity: M1-021, M1-035, M1-014
/// <summary>
/// The immediate side of the TaskExecutor, the RelTransport queue. AddTaskHolder pushes each posted item
/// and never merges them (G1.9); one Execute thread runs them one at a time, in the order they were posted
/// (G1.10), so items never overlap and a backlog runs back to back. The engine's Run swaps the whole vector
/// out and runs it in order; taking the items one by one in the same order gives the same sequence.
///
/// An exception escaping an item is not caught here; on the host it ends the process, as an exception on
/// the transport thread did before this executor existed.
/// </summary>
internal sealed class SerialExecutor
{
    private readonly BlockingCollection<(long seq, Action<long> item)> _q = new();
    private readonly object _postLock = new();
    private long _seq;
    private readonly Thread _thread;

    internal SerialExecutor(string name) => _thread = new Thread(Run) { IsBackground = true, Name = name };

    internal int ThreadId => _thread.ManagedThreadId;

    internal void Start() => _thread.Start();

    /// <summary>
    /// Appends an item (G1.9) and numbers it in post order, under the same lock as the append, so the numbers
    /// are exactly the queue order. The item is called with its number. False once the executor has been
    /// completed.
    /// </summary>
    internal bool Post(Action<long> item)
    {
        lock (_postLock)
        {
            if (_q.IsAddingCompleted) return false;
            long seq = _seq + 1;
            _q.Add((seq, item));
            _seq = seq;
            return true;
        }
    }

    internal bool Post(Action item) => Post(_ => item());

    /// <summary>Host teardown: no more items are accepted; the thread ends once it has run what it holds.</summary>
    internal void Complete()
    {
        lock (_postLock) _q.CompleteAdding();
    }

    internal void Join(TimeSpan timeout)
    {
        if (Thread.CurrentThread != _thread && _thread.IsAlive) _thread.Join(timeout);
    }

    private void Run()
    {
        foreach (var (seq, item) in _q.GetConsumingEnumerable()) item(seq);
    }
}
