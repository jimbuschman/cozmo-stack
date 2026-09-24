using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Cozmo.Protocol;

namespace Cozmo.Transport;

/// <summary>
/// The caller-facing state of the link to the current peer (the address the last <see cref="ReliableTransport.Connect"/>
/// named). It is this stack's facade over the transport's connection events, not a transport row: the engine
/// keeps its connection state in RobotConnectionManager (batch 3).
/// </summary>
public enum LinkState { Idle, Connecting, Connected, Disconnected }

public sealed record FrameEvent(bool Outbound, DateTime Utc, byte[] Raw, Frame? Frame, string? Error);

// fidelity: M1-019
/// <summary>
/// R40: what the transport hands its receiver. The connection events arrive as ReceiveData(marker, 0, addr)
/// with the markers OnConnectRequest 0x01037598, OnConnected 0x01037594 and OnDisconnected 0x0103759C
/// (0x00837478..0x00837496); everything else is data, ReceiveData(bytes, size, addr).
/// </summary>
public enum ReceiverMarker { Data, OnConnectRequest, OnConnected, OnDisconnected }

/// <summary>One call of the receiver's ReceiveData (R40): the marker, the connection's address and, for data, the bytes.</summary>
public sealed record ReceiverEvent(ReceiverMarker Marker, IPEndPoint? Address, byte[]? Data);

/// <summary>
/// Client-side port of Anki::Util::ReliableTransport + UDPTransport, with the RobotConnectionManager rules that
/// sit on either side of it.
/// UDP framing: "COZ\x03" prefix, no CRC (RobotConnectionManager::Init), then the 10-byte reliable header.
///
/// Connections (M1-018, M1-019). The transport keeps one connection per address, found by that address
/// (FindConnection 0x00837098; R13, B19). A connection is created only by a type-1 ConnectionRequest: on the
/// send side by the SendMessage of a type 1 (G2.1, G2.2), on the receive side by a type-1 frame or a container
/// whose first sub-message is type 1 (R13, G2.10). A connection is deleted by a handled DisconnectRequest
/// (R12), a timeout (R19, B22), <see cref="Disconnect(IPEndPoint)"/> (R38) or <see cref="Stop"/> (R39). None of
/// these stops the transport: its update, its socket and its other connections go on.
///
/// Each datagram is processed in the engine's order:
///  1. UDPTransport::HandleReceivedMessage: reject a datagram without the COZ prefix;
///  2. ReliableTransport::ReceiveData 0x00837632-42: fewer than 10 bytes after the prefix, or no "RE\x01",
///     and the bytes are handed on as a data message without any address lookup (0x00837792-A0);
///  3. FindConnection 0x008377FC by the datagram's address, creating the connection for a type 1 or a
///     container whose first sub-message is type 1 (R13, B19, G2.10); with no connection the frame is
///     dropped with the warning "unconnected source" (0x0083789A), its ack not processed;
///  4. UpdateLastAckedMessage 0x00837818, the resend-on-ack gate 0x0083781C-2E, and for a reliable header
///     IsWaitingForAnyInRange 0x0083783E (out of range drops everything but a MultipleMixed frame) and
///     AckMessage(seqMax) 0x0083784A;
///  5. the sub-messages in order, each through HandleSubMessage, stopping at the first invalid type
///     (0x0083790E → 0x008379AE) or size overrun (0x00837926 → 0x00837A0C) with the earlier ones handled;
///     a remainder too short for a sub-message header is a size overrun too. This stack also stops after a
///     DisconnectRequest sub-message has been handled (policy M1-038).
/// Every connection event and data payload goes to the receiver, <see cref="Received"/> (R40). The facade
/// events <see cref="Connected"/>, <see cref="Disconnected"/> and <see cref="DataReceived"/> follow the
/// current peer only. A data payload (types 4/5, a completed multipart, or a datagram handed on at step 2)
/// reaches <see cref="DataReceived"/> only while the link is Connected and it comes from the peer's IP,
/// judged at its own position in arrival order (RobotConnectionManager 0x0062F724-2A, TransportAddress::
/// operator== 0x0062F744); otherwise it is dropped there.
///
/// Threading (M1-010, M1-020, M1-021, M1-035; host structure M1-014). Production runs the transport
/// asynchronously (B15). At construction the transport requests its repeating 2 ms update (R35, B14): the
/// <c>cozmo-transport-timer</c> thread is the deferred scheduler (G1.6..G1.8) and only posts a copy of the
/// update when it is due; the <c>cozmo-transport</c> thread is the RelTransport executor (G1.9, G1.10) and
/// runs everything posted to it one item at a time, in order. Connect, FinishConnection, SendData, Send,
/// Disconnect, Start and Stop post closures to that same executor (R37, R38; Start/Stop per the batch 2b-ii
/// verifier reading), so sends and updates are FIFO on one thread and a caller never waits for a running
/// update to send. (Connect takes the transport lock briefly for the facade's own state, see _linkConn.) The update keeps being requested for the life of the
/// transport. In async mode <c>cozmo-dispatch</c> raises every public event, for the life of the transport,
/// so handlers never run on the executor. Connection state is protected by <see cref="_lock"/> (the
/// transport mutex R34/R37 name). In async mode no handler runs under it; in sync mode Raise runs handlers
/// inline, so a handler raised from SendFrame runs with it held.
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
    /// Guards <see cref="_disposed"/> and the posts callers make to the executor. It is held only for that
    /// field and for a post, never while <see cref="_lock"/> is taken and never while a user handler runs,
    /// so taking it never waits for the transport thread and a handler can always reach the transport.
    /// </summary>
    private readonly object _life = new();
    /// <summary>Written under <see cref="_life"/> before the dispose closure is posted; read under <see cref="_lock"/> by a sync-mode Connect.</summary>
    private volatile bool _disposed;
    // fidelity: M1-022
    /// <summary>
    /// UDPTransport's socket, the fd at +0x94; null is fd −1. Written under <see cref="_lock"/> on the executor;
    /// read without it only by the network-bind handler's fd test (G4.4).
    /// </summary>
    private volatile Socket? _sock;
    // fidelity: M1-022
    /// <summary>
    /// UDPTransport's stored local port, +0x98. The ctor stores 0xBAC9 (0x00839546) and RobotConnectionManager::Init
    /// then stores 0 (0x0062EFEA str.w r5,[r6,#0x98]), so the first open is ephemeral (B12). CloseSocket stores
    /// 0xBAC9 = 47817 on both of its paths (B38, 0x00839694..0x0083969C), so every open after a close binds 47817
    /// (B16, B18; correction C1).
    /// </summary>
    private int _localPort;
    internal const int PortAfterClose = 0xBAC9;
    // fidelity: M1-023
    /// <summary>The reset flag, +0x9D (G4.5): set by <see cref="ResetSocket"/>, acted on and cleared by the next update.</summary>
    private volatile bool _resetRequested;
    // fidelity: M1-037
    /// <summary>The host notification that raises the socket reset (policy M1-037, decision D5).</summary>
    private readonly HostNetworkChange _networkChange;
    /// <summary>The current peer: the address the last Connect named (the facade's RobotConnectionData+0x30, B21).</summary>
    private volatile IPEndPoint? _peer;
    /// <summary>
    /// The facade only (not a transport row): the connection the current link uses, bound by its Connect's
    /// type-1 SendMessage, and the number of the last Connect. Written under <see cref="_lock"/>, as are
    /// <see cref="_peer"/> and <see cref="State"/>, so a link's facade state changes only on its own
    /// connection: the end of an older link's connection cannot end a newer link, and a ConnectionResponse on
    /// the newer link's connection is not lost. No protocol action is skipped because of them.
    /// </summary>
    private ReliableConnection? _linkConn;
    private long _linkGen;

    // fidelity: M1-018, M1-019
    /// <summary>
    /// The connections, keyed by address (R13, B19: FindConnection 0x00837098; G2.2: map insert). Replaced,
    /// never changed in place, under <see cref="_lock"/>, so a caller can read a snapshot without the lock.
    /// Kept in the order the connections were made.
    /// MISSING: R34 updates "each connection"; the rows do not give the order of the engine's map (it is
    /// ordered by TransportAddress::operator&lt;, whose comparison no frozen row states), so updates run in the
    /// order the connections were made.
    /// </summary>
    private volatile KeyValuePair<IPEndPoint, ReliableConnection>[] _connections = Array.Empty<KeyValuePair<IPEndPoint, ReliableConnection>>();

    /// <summary>A transport made by <see cref="CreateOffline"/>: frames go to <see cref="OfflineOutbound"/>, not a socket.</summary>
    private bool _offline;
    private volatile Thread? _dispatch;
    private BlockingCollection<Action>? _events;
    /// <summary>Set on a dispatch thread to the transport it belongs to, so Dispose can tell it is running there.</summary>
    [ThreadStatic] private static ReliableTransport? t_dispatchOwner;
    private volatile LinkState _state = LinkState.Idle;

    // fidelity: M1-015
    /// <summary>
    /// The +0xA1 timed-out flag (R35, R41, B36): cleared by the ctor and by Connect (0x008367DA, 0x0083710E),
    /// set by the tick lambda when ReliableTransport::Update returns false (0x008383D4..0x008383DC).
    /// </summary>
    private volatile bool _timedOut;

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

    // fidelity: M1-022
    /// <summary>
    /// Send errors counted by <c>UDPTransport</c>, by the code it passes to AddSendError: 6 = a failed sendto
    /// (B10, 0x0083A552). Diagnostics only; nothing reads them.
    /// </summary>
    public ReceiveErrorCounts UdpSendErrors { get; } = new();

    // fidelity: M1-022
    /// <summary>
    /// B10: the time of the last failed sendto, which UDPTransport stores at +0x88 (0x0083A5F4). This stack's
    /// initial value is −1; the source's initial value is 0.0 (0x0083953C; verifier reading, batch 2c; pending
    /// inventory correction), which is not reproduced here. When the time is stored is MISSING (B10 rate limit;
    /// verifier reading 0x0083A648..0x0083A666, pending inventory correction): see the send path.
    /// MISSING: B10 says "time at +0x88" without naming the clock; this stack's net clock is used.
    /// </summary>
    public double LastSendErrorMs { get; private set; } = -1;

    /// <summary>How long a worker thread is given to finish during shutdown before it is abandoned.</summary>
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(2);

    public LinkState State { get => _state; private set => _state = value; }

    // fidelity: M1-015
    /// <summary>
    /// The +0xA1 timed-out flag, read-only (R41, B36): true once a tick's ReliableTransport::Update has
    /// reported a timed-out connection, until the next <see cref="Connect"/>. Only the async tick sets it
    /// (R35); a sync-mode <see cref="Pump"/> does not. What the app layer does with it (B31) is batch 3.
    /// </summary>
    public bool TimedOut => _timedOut;

    /// <summary>The facade: the current peer's ConnectionResponse arrived while Connecting.</summary>
    public event Action? Connected;
    /// <summary>
    /// The facade: the link to the current peer ended (a DisconnectRequest from it, its timeout, a Disconnect,
    /// Stop or Dispose), with the reason. Raised once per link.
    /// </summary>
    public event Action<string>? Disconnected;
    /// <summary>A complete application payload (a CLAD robot message: tag + body) received in order.</summary>
    public event Action<byte[]>? DataReceived;
    /// <summary>
    /// The receiver (R40): every connection event and every data payload, for every connection, with its
    /// address. The app layer decides what to keep (for example B20 drops OnConnectRequest).
    /// </summary>
    public event Action<ReceiverEvent>? Received;
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
    internal ReliableTransport(TransportOptions? options, INetClock? clock, bool manualPump, HostNetworkChange? networkChange = null)
    {
        _o = options ?? TransportOptions.EngineDefaults; _clock = clock ?? new StopwatchClock();
        ManualPump = manualPump;

        // fidelity: M1-022
        // B12: the UDP ctor stores 0xBAC9 at +0x98 (0x00839546) and RobotConnectionManager::Init stores 0 over
        // it (0x0062EFEA) before its StartClient, so the first open binds an ephemeral port.
        _localPort = PortAfterClose;
        _localPort = 0;

        // fidelity: M1-037, M1-023
        // G4.1: the RCM ctor registers its UDP transport with WifiUtil for the RCM's lifetime; this stack raises
        // the same handler (G4.4) from the host's address-change notification (policy M1-037, D5), subscribed
        // here and unsubscribed by Dispose.
        _networkChange = networkChange ?? HostNetworkChange.Host;
        _networkChange.Subscribe(OnNetworkAddressChanged);

        // fidelity: M1-010, M1-021, M1-014, M1-020
        // R35/B14: the ctor creates the RelTransport queue and ChangeSyncMode(false) schedules the update
        // every 2 ms (ScheduleCallback 0x0083689E); it is requested once, here, not per connection. Sync mode
        // (R36) has no timer; the queue itself exists in both modes.
        // MISSING: R35/B14 create the queue at "priority 3"; the rows do not say what host thread priority
        // that is, so both threads keep the default priority.
        _exec = new SerialExecutor("cozmo-transport");
        _exec.Start();
        if (manualPump) return;
        // fidelity: M1-014
        // Host structure: the dispatch thread lives as long as the transport, since connection events can
        // now arrive with no link up (an inbound ConnectionRequest, R13).
        var q = new BlockingCollection<Action>();
        _events = q;
        _dispatch = new Thread(() => DispatchLoop(q)) { IsBackground = true, Name = "cozmo-dispatch" };
        _dispatch.Start();
        _sched = new TransportScheduler(TransportScheduler.HostNowNs, _o.UpdateIntervalMs, PostTick);
        _sched.Start();
    }

    /// <summary>The current peer's connection, or null when there is none.</summary>
    public ReliableConnection? Connection => FindConnection(_peer);
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

    /// <summary>Test seam: the connection for <paramref name="address"/>, or null.</summary>
    internal ReliableConnection? ConnectionFor(IPEndPoint address) => FindConnection(address);

    /// <summary>Test seam: the addresses that have a connection, in the order the connections were made.</summary>
    internal IReadOnlyList<IPEndPoint> ConnectionAddresses => _connections.Select(kv => kv.Key).ToList();

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

    /// <summary>The socket's local endpoint, for tests that need to see it opened, reopened or closed.</summary>
    internal EndPoint? LocalEndPoint { get { lock (_lock) return _sock?.LocalEndPoint; } }

    /// <summary>Test seam: the socket itself (fd +0x94), or null when there is none (fd −1).</summary>
    internal Socket? CurrentSocket => _sock;

    /// <summary>Test seam: the stored local port (+0x98) the next open binds.</summary>
    internal int StoredLocalPort { get { lock (_lock) return _localPort; } }

    /// <summary>Test seam: the reset flag (+0x9D).</summary>
    internal bool ResetRequested => _resetRequested;

    /// <summary>Stands in for <see cref="Socket.SendTo(byte[], SocketFlags, EndPoint)"/> on the send path; returns the bytes sent.</summary>
    internal delegate int SendToHook(Socket socket, byte[] datagram, IPEndPoint to);

    /// <summary>Test seam: when set, sendto goes through this instead of the socket. Null in production.</summary>
    internal SendToHook? SendHook { get; set; }

    /// <summary>
    /// Test seam: when set, CloseSocket's close(fd) is this instead of <see cref="Socket.Close()"/>; throwing is a
    /// failed close. Null in production.
    /// </summary>
    internal Action<Socket>? CloseHook { get; set; }

    // ------------------------------------------------------------- event dispatch

    /// <summary>
    /// Queues a handler call for the dispatch thread. With no dispatch thread (sync mode, offline transports
    /// used by tests and the replay tool, or after Dispose) it runs inline, so replay stays synchronous and
    /// deterministic.
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

    private void RaiseAll(List<Action> effects) { foreach (var a in effects) Raise(a); }

    // fidelity: M1-019
    /// <summary>R40: one ReceiveData call on the receiver, raised in order with the other effects.</summary>
    private void Receiver(List<Action> effects, ReceiverMarker marker, IPEndPoint? address, byte[]? data = null)
    {
        var e = new ReceiverEvent(marker, address, data);
        effects.Add(() => Fan(Received, e));
    }

    // ------------------------------------------------------------- connections

    private ReliableConnection? FindConnection(IPEndPoint? address)
    {
        if (address is null) return null;
        foreach (var kv in _connections) if (kv.Key.Equals(address)) return kv.Value;
        return null;
    }

    // fidelity: M1-018, M1-032
    /// <summary>
    /// G2.2: a new connection, inserted in the map under its address. Its ctor stamps lastRecv with the
    /// current time (G2.3), so a connection that is never answered times out 5000 ms after this (G2.7).
    /// Its frames go to its own address.
    /// </summary>
    private ReliableConnection CreateConnectionLocked(IPEndPoint address)
    {
        ReliableConnection c = null!;
        c = new ReliableConnection(_o, _clock, (ty, mn, mx, body) => SendFrame(address, c, ty, mn, mx, body));
        var old = _connections;
        var next = new KeyValuePair<IPEndPoint, ReliableConnection>[old.Length + 1];
        old.CopyTo(next, 0);
        next[^1] = new(address, c);
        _connections = next;
        return c;
    }

    // fidelity: M1-003, M1-015, M1-019
    /// <summary>
    /// DeleteConnection 0x008375F0 (veneer 0x008D123C): that address's connection goes, with its multipart
    /// assembly (M1-009); nothing else does. Returns the connection deleted, or null.
    /// </summary>
    private ReliableConnection? DeleteConnectionLocked(IPEndPoint address)
    {
        var old = _connections;
        int i = Array.FindIndex(old, kv => kv.Key.Equals(address));
        if (i < 0) return null;
        _connections = old.Where((_, j) => j != i).ToArray();
        return old[i].Value;
    }

    // fidelity: M1-019
    /// <summary>R39 ClearConnections 0x00837374: every connection goes, and nothing is sent.</summary>
    private void ClearConnectionsLocked()
    {
        _connections = Array.Empty<KeyValuePair<IPEndPoint, ReliableConnection>>();
    }

    /// <summary>
    /// The facade: the link to the current peer ends, once. Not a transport row; what the app layer does
    /// when a connection goes (B31, B32) is batch 3.
    /// </summary>
    private void EndLinkLocked(string reason, List<Action> effects)
    {
        _linkConn = null;
        if (State is not (LinkState.Connecting or LinkState.Connected)) return;
        State = LinkState.Disconnected;
        effects.Add(() => Fan(Disconnected, reason));
    }

    /// <summary>The facade: a connection went; if it was the current link's, that link ends.</summary>
    private void ConnectionGoneLocked(ReliableConnection? gone, string reason, List<Action> effects)
    {
        if (gone is not null && ReferenceEquals(gone, _linkConn)) EndLinkLocked(reason, effects);
    }

    // ------------------------------------------------------------- offline mode

    /// <summary>
    /// A transport with no socket, for replay/conformance tests: incoming datagrams are fed with
    /// <see cref="ProcessIncoming(byte[])"/>, outgoing frames are captured in <see cref="OfflineOutbound"/>.
    /// Events are raised inline on the calling thread. Datagrams fed without an address are taken to come
    /// from <see cref="Peer"/>, a loopback stand-in for the robot, whose connection exists from the start.
    /// </summary>
    public static ReliableTransport CreateOffline(TransportOptions? options = null, INetClock? clock = null)
    {
        var t = new ReliableTransport(options, clock, manualPump: true) { _offline = true };
        var peer = new IPEndPoint(IPAddress.Loopback, RobotAddress.RemotePort(isSimulated: false));
        t._peer = peer;
        lock (t._lock) t._linkConn = t.CreateConnectionLocked(peer);
        t.State = LinkState.Connecting;
        return t;
    }

    /// <summary>Offline only: queue the initial ConnectionRequest like <see cref="Connect"/> does.</summary>
    public void OfflineConnect() { lock (_lock) Connection!.Queue(ReliableMessageType.ConnectionRequest, Array.Empty<byte>(), true, true); }

    /// <summary>
    /// Offline only: the robot's answer to <see cref="OfflineConnect"/> — a reliable ConnectionResponse, its
    /// seq 1, acking our seq 1 — fed from the peer through the normal receive path.
    /// </summary>
    public void OfflineAcceptConnection() =>
        ProcessIncoming(FrameCodec.Encode(Frame.Single(
            new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), SequenceId.Min), SequenceId.Min)));

    /// <summary>Offline only: run one update of the peer's connection. False when there is no live connection.</summary>
    public bool OfflineTick() { lock (_lock) return Connection?.Update() ?? false; }

    // ------------------------------------------------------------------ start / stop

    // fidelity: M1-019, M1-035
    /// <summary>
    /// R39 / B12: Start calls UDPTransport::StartClient, which opens the socket only if there is none
    /// (OpenSocket if fd == −1, 0x0083AD58), so a second Start keeps the socket it has. Connect does not open
    /// a socket. RT::StartClient posts that as an action (0x008371F4..0x00837214 → QueueAction; closure
    /// 0x0083808E → UDP StartClient; verifier reading, batch 2b-ii; pending inventory correction), so it runs
    /// on the RelTransport executor in order with the sends and updates, in both modes, and this returns
    /// before the socket is open. It opens on the stored local port (B12): 0, ephemeral, for the first open;
    /// 47817 after any close (B38). After Dispose has begun this throws <see cref="ObjectDisposedException"/>.
    /// </summary>
    public void Start() => QueueAction("start", () =>
    {
        var effects = new List<Action>();
        lock (_lock)
        {
            // fidelity: M1-022
            if (_sock is null) OpenSocketLocked(_localPort, effects);   // B12: OpenSocket if fd == −1 (0x0083AD58)
        }
        RaiseAll(effects);
    });

    // fidelity: M1-019, M1-035
    /// <summary>
    /// R39: Stop calls UDP Stop* (the socket is closed) and then ClearConnections (0x008380F6, veneer
    /// 0x008D122C; 0x00837374): every connection goes and no frame is sent; the receiver is told nothing
    /// (R39 names no event). RT::StopClient posts that as an action (0x00837284..0x008372A4 → QueueAction;
    /// closure 0x008380F6; verifier reading, batch 2b-ii; pending inventory correction), so it runs on the
    /// executor after everything posted before it, in both modes: a Connect made before it still sends its
    /// ConnectionRequest before the socket closes. The facade link ends with the reason "stopped" if its
    /// connection was cleared. The update goes on being run (R35); <see cref="Start"/> opens a socket again.
    /// </summary>
    public void Stop() => QueueAction("stop", () =>
    {
        var effects = new List<Action>();
        lock (_lock) StopLocked("stopped", effects);
        RaiseAll(effects);
    });

    private void StopLocked(string reason, List<Action> effects)
    {
        // fidelity: M1-022
        // MISSING: R39 says only "Stop calls UDP Stop*"; no row says that UDP Stop* closes through CloseSocket
        // (and so stores 47817 at +0x98, B38). The close goes through CloseSocket here, per the batch 2c brief,
        // so a Start after Stop binds 47817.
        CloseSocketLocked(effects);
        var link = _linkConn;
        ClearConnectionsLocked();
        ConnectionGoneLocked(link, reason, effects);
    }

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
    private void QueueMessage(IPEndPoint address, ReliableMessageType type, byte[] payload, bool reliable, bool flush)
    {
        if (ManualPump)
        {
            var now = new List<Action>();
            lock (_lock) SendMessageLocked(address, type, payload, reliable, flush, 0.0, now);
            RaiseAll(now);
            return;
        }
        double posted = _clock.NowMs;
        QueueAction("send", () =>
        {
            var effects = new List<Action>();
            lock (_lock) SendMessageLocked(address, type, payload, reliable, flush, posted, effects);
            RaiseAll(effects);
        });
    }

    // fidelity: M1-035, M1-018, M1-032
    /// <summary>
    /// ReliableTransport::SendMessage, as the posted closures reach it. G2.1: FindConnection(addr, create =
    /// type == 1); G2.2: a miss with create makes the connection, whose ctor stamps lastRecv with the
    /// current time (G2.3). R13: any other type with no connection is dropped with the warning
    /// "unconnected destination" and nothing is queued.
    /// </summary>
    private ReliableConnection? SendMessageLocked(IPEndPoint address, ReliableMessageType type, byte[] payload, bool reliable, bool flush, double postedMs, List<Action> effects)
    {
        var c = FindConnection(address);
        if (c is null)
        {
            if (type != ReliableMessageType.ConnectionRequest)
            {
                effects.Add(() => Fan(Warning, $"unconnected destination {address}; {type} dropped"));
                return null;
            }
            c = CreateConnectionLocked(address);
        }
        // MISSING: R37 says SendMessage is called with the posted time, but no row says what SendMessage does
        // with it (which PendingMessage field, if any, it sets), so postedMs changes nothing here.
        _ = postedMs;
        c.Queue(type, payload, reliable, flush);
        return c;
    }

    // fidelity: M1-019, M1-015
    /// <summary>
    /// R38 / B21: RT::Connect clears the +0xA1 timed-out flag (0x0083710E) and queues QueueMessage(type 1,
    /// reliable, flush 1) to the address (0x0083711C). That is all it does: it opens no socket (that is
    /// <see cref="Start"/>, R39) and refuses nothing. In async mode the ConnectionRequest is sent by the
    /// posted closure (R37), in order with everything posted before it, and its SendMessage creates the
    /// connection if the address has none (G2.1, G2.2); in sync mode (R36) both happen here. A Connect to an
    /// address that already has a connection queues the type 1 on it.
    ///
    /// The facade: the address becomes the current peer and <see cref="State"/> is Connecting when this
    /// returns (the engine keeps both in RobotConnectionManager, B21: batch 3).
    ///
    /// Once Dispose has begun this refuses, and <see cref="ObjectDisposedException"/> is thrown; so it is if
    /// the executor accepts nothing more, and the peer and state are put back. In async mode the post is made
    /// under <see cref="_life"/>, as Dispose's is, so it lands before the dispose closure or not at all. In
    /// sync mode the ConnectionRequest is queued under <see cref="_lock"/> only, after checking that Dispose
    /// has not begun; the dispose closure needs that same lock, so it runs after this.
    ///
    /// M1-001: the remote port is 5552 when <paramref name="isSimulated"/>, else 5551 (B3); the IP is the
    /// caller's (B3 builds the TransportAddress from ConnectToRobot's ipAddress).
    /// </summary>
    // fidelity: M1-001
    public void Connect(IPAddress robot, bool isSimulated = false) =>
        Connect(new IPEndPoint(robot, RobotAddress.RemotePort(isSimulated)));

    /// <summary>
    /// Test seam, not a production path: a Connect to an explicit port, so tests can reach a stand-in robot on
    /// a loopback port of their own. The engine has no port override (M1-001, B3); production callers use
    /// <see cref="Connect(IPAddress, bool)"/>.
    /// </summary>
    internal void Connect(IPAddress robot, int port) => Connect(new IPEndPoint(robot, port));

    private void Connect(IPEndPoint peer)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ReliableTransport));
        _timedOut = false;                                  // R38 / B36: Connect clears +0xA1 (0x0083710E)
        // The facade, under the transport lock with every other facade transition (see _linkConn).
        IPEndPoint? previousPeer; LinkState previousState; ReliableConnection? previousConn; long gen;
        lock (_lock)
        {
            previousPeer = _peer; previousState = State; previousConn = _linkConn;
            gen = ++_linkGen;
            _peer = peer;
            _linkConn = null;
            State = LinkState.Connecting;
        }

        // fidelity: M1-035, M1-020
        var effects = new List<Action>();
        bool refused;
        if (ManualPump)
        {
            // R36: sync mode calls SendMessage directly, with time 0.0 (see QueueMessage). Not under _life.
            lock (_lock)
            {
                refused = _disposed;
                if (!refused) SendConnectLocked(peer, gen, 0.0, effects);
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
                    lock (_lock) SendConnectLocked(peer, gen, posted, fx);
                    RaiseAll(fx);
                });
            }
        }
        if (refused)
        {
            lock (_lock)
            {
                if (_linkGen == gen) { _peer = previousPeer; State = previousState; _linkConn = previousConn; }
            }
            throw new ObjectDisposedException(nameof(ReliableTransport));
        }
        RaiseAll(effects);
    }

    /// <summary>
    /// The Connect action: the host session reset, then SendMessage(type 1, reliable, flush 1) (R38, B21). The
    /// facade binds its link to the connection that SendMessage used, unless a later Connect has since begun.
    /// </summary>
    private void SendConnectLocked(IPEndPoint peer, long gen, double postedMs, List<Action> effects)
    {
        OfflineOutbound.Clear();
        HandlerFaults = 0;
        var c = SendMessageLocked(peer, ReliableMessageType.ConnectionRequest, Array.Empty<byte>(), true, true, postedMs, effects);
        if (gen == _linkGen) _linkConn = c;
    }

    // fidelity: M1-019
    /// <summary>
    /// R38 FinishConnection 0x0083712E: a reliable type-2 ConnectionResponse, flush 1, to
    /// <paramref name="address"/>. With no connection to that address nothing is queued and the warning is
    /// "unconnected destination" (R13 send side).
    /// MISSING: R38 does not say whether FinishConnection queues through QueueMessage (as Connect does, B21)
    /// or calls SendMessage directly; it is queued like Connect and SendData here.
    /// </summary>
    public void FinishConnection(IPEndPoint address) =>
        QueueMessage(address, ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), reliable: true, flush: true);

    // ------------------------------------------------------------------ the UDP socket

    // fidelity: M1-022
    /// <summary>
    /// UDPTransport::OpenSocket (B11): close any previous socket (0x00839A3A blx CloseSocket; verifier reading,
    /// batch 2c; pending inventory correction); getaddrinfo(NULL, port, AI_PASSIVE, SOCK_DGRAM, AF_INET)
    /// (0x00839ACA, 0x00839AD0, 0x00839AF2), i.e. INADDR_ANY:port; socket (0x00839B8A); for AF_INET, SO_BROADCAST
    /// = 1 (0x00839D18/0x00839D1A); bind (0x00839D3A). A bind that fails with EADDRINUSE is a warning only
    /// (0x00839E70): the socket is kept. No O_NONBLOCK and no buffer options, so the socket stays blocking; the
    /// reads are made non-blocking one call at a time (B16, see <see cref="ReadDontWait"/>).
    /// A failed setsockopt or any other failed bind closes the new socket through CloseSocket and OpenSocket
    /// returns failure, leaving no socket and the stored port 47817 (verifier reading, batch 2c; pending
    /// inventory correction: setsockopt 0x00839D20 ble → 0x00839CF0 CloseSocket → return 0; bind 0x00839E72
    /// bne → 0x0083A026 → 0x00839CF0).
    /// Host mapping: EADDRINUSE is <see cref="SocketError.AddressAlreadyInUse"/>.
    /// </summary>
    private void OpenSocketLocked(int port, List<Action> effects)
    {
        // B11: close the previous socket, through CloseSocket (0x00839A3A; verifier reading, batch 2c; pending
        // inventory correction). In this stack OpenSocket is reached only with no socket (Start opens only if
        // fd == −1; the reopens follow a CloseSocket, which leaves fd −1 on both paths, B38), so this never runs.
        if (_sock is not null) CloseSocketLocked(effects);

        Socket s;
        try { s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); }
        catch (SocketException e)
        {
            // MISSING: B11 does not say what OpenSocket does when socket() fails; nothing is opened here.
            var code = e.SocketErrorCode; var msg = e.Message;
            effects.Add(() => Fan(Warning, $"OpenSocket: socket() failed: {code} ({msg}); no socket (MISSING: the engine's handling is not in the inventory)"));
            return;
        }
        _sock = s;                                          // the new fd, which CloseSocket closes on the failures below
        try { s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1); }   // B11: SO_BROADCAST = 1
        catch (SocketException e)
        {
            // Verifier reading, batch 2c; pending inventory correction: 0x00839D20 ble → 0x00839CF0 CloseSocket
            // → return 0.
            var code = e.SocketErrorCode; var msg = e.Message;
            effects.Add(() => Fan(Warning, $"OpenSocket: SO_BROADCAST failed: {code} ({msg}); socket closed"));
            CloseSocketLocked(effects);
            return;
        }
        try { s.Bind(new IPEndPoint(IPAddress.Any, port)); }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            var msg = e.Message;
            effects.Add(() => Fan(Warning, $"OpenSocket: bind to port {port} failed: AddressAlreadyInUse ({msg}); socket kept"));
        }
        catch (SocketException e)
        {
            // Verifier reading, batch 2c; pending inventory correction: 0x00839E72 bne → 0x0083A026 → 0x00839CF0
            // CloseSocket, so the stored port becomes 47817.
            var code = e.SocketErrorCode; var msg = e.Message;
            effects.Add(() => Fan(Warning, $"OpenSocket: bind to port {port} failed: {code} ({msg}); socket closed"));
            CloseSocketLocked(effects);
        }
    }

    // fidelity: M1-022
    /// <summary>
    /// UDPTransport::CloseSocket (B38, 0x00839694..0x0083969C): close(fd); a failure is logged; on both paths fd
    /// becomes −1 and the stored port 0xBAC9 = 47817. Returns true when the close succeeded (the callers reopen
    /// only then: 0x0083AB2C cbz r0, 0x0083ACF4 cmp r0,#1).
    /// Host mapping: .NET does not report a failed closesocket; a close is taken as failed only when
    /// <see cref="Socket.Close()"/> (or the <see cref="CloseHook"/> test seam) throws.
    /// MISSING: no row says what CloseSocket does or returns when there is no socket (fd −1). Here it does
    /// nothing and returns false, so nothing is reopened; fd and the stored port are left as they are.
    /// </summary>
    private bool CloseSocketLocked(List<Action> effects)
    {
        var s = _sock;
        if (s is null) return false;
        bool ok = true;
        try { if (CloseHook is { } hook) hook(s); else s.Close(); }
        catch (Exception e)
        {
            ok = false;
            var msg = $"{e.GetType().Name}: {e.Message}";
            effects.Add(() => Fan(Warning, $"CloseSocket: close failed: {msg}"));
        }
        _sock = null;
        _localPort = PortAfterClose;
        return ok;
    }

    // fidelity: M1-022
    /// <summary>
    /// B16: recvmsg(fd, msg, MSG_DONTWAIT) (0x0083AA76 movs r3,#0x40) on the blocking socket B11 opens.
    /// Host mapping: Windows has no MSG_DONTWAIT; the socket is left blocking, and a read is made only when a
    /// zero-timeout poll says one will not block. With nothing to read this reports
    /// <see cref="SocketError.WouldBlock"/>, the host's EAGAIN. Only the executor reads, so nothing else can take
    /// the datagram between the poll and the read.
    /// </summary>
    private static int ReadDontWait(Socket s, byte[] buffer, ref EndPoint from)
    {
        if (!s.Poll(0, SelectMode.SelectRead)) throw new SocketException((int)SocketError.WouldBlock);
        return s.ReceiveFrom(buffer, SocketFlags.None, ref from);
    }

    // fidelity: M1-023
    /// <summary>
    /// UDPTransport::ResetSocket (G4.5, 0x0083A256 movs r1,#1; 0x0083A258 strb.w r1,[r0,#0x9d]): it sets the
    /// reset flag and does nothing else. The next update acts on it (see <see cref="DrainLocked"/>).
    /// </summary>
    internal void ResetSocket() => _resetRequested = true;

    // fidelity: M1-023
    /// <summary>
    /// The WifiUtil handler RegisterTransport binds to the transport (G4.2, 0x0083BAD1; G4.4): if fd (+0x94) &lt; 0
    /// nothing (0x0083BAD6 ldr.w r0,[r4,#0x94]; 0x0083BADC blt); otherwise ResetSocket (0x0083BAE0) and the log
    /// "WifiUtil.BindTransport" / "reset socket %d" (0x0083BB54, 0x0083BB6C). No other condition. The log goes
    /// out as a <see cref="Warning"/>, this stack's one log channel; the host's socket handle stands for the fd.
    /// </summary>
    internal void NetworkBindSignal()
    {
        var s = _sock;
        if (s is null) return;
        ResetSocket();
        long fd;
        try { fd = (long)s.Handle; } catch (ObjectDisposedException) { fd = -1; }
        Raise(() => Fan(Warning, $"WifiUtil.BindTransport: reset socket {fd}"));
    }

    // fidelity: M1-037
    /// <summary>
    /// Policy M1-037 (D5): the host's <c>NetworkChange.NetworkAddressChanged</c> stands in for Android's process
    /// network bind and unbind (G4.6..G4.12), and raises the same handler.
    /// </summary>
    private void OnNetworkAddressChanged(object? sender, EventArgs e) => NetworkBindSignal();

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
        if (Connection is null || State is not LinkState.Connected) throw new InvalidOperationException("not connected");
        SendData(bytes, reliable: true, flush: false);
    }

    // fidelity: M1-019
    /// <summary>
    /// ReliableTransport::SendData 0x008370E4: queue one message to the current peer with the given delivery
    /// options (type 4 when reliable, 5 when not, and the caller's flush). The manager's state gate is not
    /// part of this layer (see <see cref="Send"/>); this refuses only when no link is up to queue on. In
    /// async mode the message is posted (R37) and this returns without waiting for it to be queued.
    /// </summary>
    public void SendData(byte[] cladMessage, bool reliable = true, bool flush = false)
    {
        // In async mode the connection is created by the posted Connect closure, so only the link state is
        // looked at here; in sync mode the connection is already there whenever a link is up.
        var peer = _peer;
        if (peer is null || (ManualPump && Connection is null) || State is LinkState.Idle or LinkState.Disconnected)
            throw new InvalidOperationException("not connected");
        QueueMessage(peer, reliable ? ReliableMessageType.SingleReliableMessage : ReliableMessageType.SingleUnreliableMessage, cladMessage, reliable, flush);
    }

    // fidelity: M1-019, M1-035
    /// <summary>
    /// ReliableTransport::Disconnect(addr): it queues an action (B21, closure 0x00837FFA) that sends one
    /// reliable, flushed DisconnectRequest to that address through the normal send path (0x0083801C), then
    /// deletes that address's connection at once (0x0083802A → DeleteConnection 0x008375F0). Whether the
    /// request reaches the wire is up to that one send attempt; nothing waits for it. With no connection to
    /// the address the send is refused as "unconnected destination" (R13) and nothing is deleted. The socket,
    /// the update and every other connection go on. In async mode this returns before the action has run.
    /// </summary>
    public void Disconnect(IPEndPoint address) => PostDisconnect(address, "requested");

    /// <summary>
    /// <see cref="Disconnect(IPEndPoint)"/> for the current peer, taken when this is called (B21 / B33 pass
    /// the address). The facade link ends with <paramref name="reason"/> once the action has run.
    /// </summary>
    public void Disconnect(string reason = "requested") => PostDisconnect(_peer, reason);

    private void PostDisconnect(IPEndPoint? address, string reason)
    {
        lock (_life)
        {
            if (_disposed) return;
            bool posted = TryPostLifeLocked("disconnect", () =>
            {
                if (address is null) return;               // no address was ever named: nothing to disconnect
                var effects = new List<Action>();
                lock (_lock) ConnectionGoneLocked(DisconnectLocked(address, effects), reason, effects);
                RaiseAll(effects);
            });
            if (!posted) throw new ObjectDisposedException(nameof(ReliableTransport));
        }
    }

    // fidelity: M1-019
    /// <summary>
    /// The Disconnect closure 0x00837FFA: SendMessage(type 3, reliable, flush 1), then DeleteConnection
    /// (b.w 0x8D123C). Returns the connection deleted, or null.
    /// </summary>
    private ReliableConnection? DisconnectLocked(IPEndPoint address, List<Action> effects)
    {
        // The time the closure passes is 0.0: 0x0083800A strd r1,r1,[sp,#0x10] with r1 = 0 (verifier reading,
        // batch 2b-ii; pending inventory correction). SendMessageLocked discards the time (see there).
        SendMessageLocked(address, ReliableMessageType.DisconnectRequest, Array.Empty<byte>(), true, true, 0.0, effects);
        return DeleteConnectionLocked(address);
    }

    /// <summary>
    /// <c>~RobotConnectionManager</c> 0x0062EE98: <c>DisconnectCurrent</c> 0x0062EF20, which calls RT Disconnect
    /// with the robot address unconditionally (B33, 0x0062EF20..0x0062EF52), and only then <c>StopClient</c>
    /// (0x0062EEA2). So whenever an address has been named, Connecting or Connected, the transport disconnects
    /// it exactly as <see cref="Disconnect(string)"/> does (a type 3 to its connection, if it has one, then
    /// DeleteConnection) before it stops (<see cref="Stop"/>: the socket closed, every connection cleared).
    /// The two actions are run back to back in one posted closure, as nothing can be posted between them.
    ///
    /// The disconnect is posted like every other action (QueueAction, both modes), behind whatever was posted
    /// before it, and the host threads are then released: the timer stops, the dispatch thread ends, and the
    /// executor finishes what it holds and ends. Dispose waits for that, up to the join bound, except on this
    /// transport's own dispatch thread (which the dispose closure joins) or on the executor itself.
    /// </summary>
    public void Dispose()
    {
        lock (_life)
        {
            if (_disposed) return;
            _disposed = true;
            // fidelity: M1-037
            // The host trigger lives as long as the transport (G4.1: the registration lives as long as the RCM).
            _networkChange.Unsubscribe(OnNetworkAddressChanged);
            TryPostLifeLocked("dispose", () =>
            {
                var effects = new List<Action>();
                lock (_lock)
                {
                    if (_peer is { } p) ConnectionGoneLocked(DisconnectLocked(p, effects), "disposed", effects);   // B33
                    StopLocked("disposed", effects);
                    EndLinkLocked("disposed", effects);
                }
                RaiseAll(effects);
                StopDispatch();
                _tickHandleExpired = true;   // G1.10: copies posted after this are skipped
            });
        }

        // fidelity: M1-014
        _sched?.Stop();
        _exec.Complete();
        // The self-join skip: on this transport's dispatch thread the dispose closure is joining this very
        // thread, and in a handler raised inline under _lock (sync mode) the dispose closure needs the lock this
        // thread holds; waiting for the executor in either case would only run out the join bound.
        if (t_dispatchOwner != this && !Monitor.IsEntered(_lock)) _exec.Join(JoinTimeout);
    }

    // fidelity: M1-014
    /// <summary>Host teardown: the dispatch thread raises what it holds and ends; later events run inline.</summary>
    private void StopDispatch()
    {
        var q = _events; var dispatch = _dispatch;
        if (q is null) return;
        try { q.CompleteAdding(); } catch (ObjectDisposedException) { }
        if (dispatch is not null && dispatch != Thread.CurrentThread && dispatch.IsAlive) dispatch.Join(JoinTimeout);
        _events = null; _dispatch = null;
    }

    // ------------------------------------------------------------------ wire

    /// <summary>
    /// A frame from <paramref name="conn"/>, to its address. The header's ack is that connection's (R2).
    /// Called under _lock by the connection; events go through Raise, which in async mode hands them to the
    /// dispatch thread and in sync mode runs them inline under _lock.
    /// </summary>
    private void SendFrame(IPEndPoint address, ReliableConnection conn, ReliableMessageType type, ushort seqMin, ushort seqMax, byte[] body)
    {
        var hdr = new ReliableHeader(type, seqMin, seqMax, conn.LastInAcked);
        var raw = new byte[ReliableHeader.Length + body.Length];
        hdr.Write(raw); body.CopyTo(raw, ReliableHeader.Length);
        if (_offline)
        {
            FrameCodec.TryDecode(raw, out var f, out _);
            if (f is not null) OfflineOutbound.Add(f);
            Raise(() => Fan(FrameTrace, new FrameEvent(true, DateTime.UtcNow, raw, f, null)));
            return;
        }
        // fidelity: M1-022
        // B10: only an IP address is sent to (0x0083A606: "UDP can only send to IP addresses!"); sendto with flags
        // 0 (0x0083A37C); a short count logs "SentWrongNumBytes" (0x0083A39A); a failure counts AddSendError(6)
        // (0x0083A552), stores the time at +0x88 (0x0083A5F4) and warns; there is no retry and no disconnect.
        // AddSendError(6) is unconditional.
        // MISSING (B10 rate limit; verifier reading 0x0083A648..0x0083A666, pending inventory correction): the
        // row says only "a warning if verbose, time at +0x88". The verifier read that verbose logging is off
        // (kEnableVerboseNetworkLogging 0x00C934A4) and that the warning and the +0x88 store happen only when
        // +0x88 == 0.0 or now > +0x88 + 30000.0 (0x0083A6C8). That is not in the frozen row and is not
        // implemented: here the warning is given and the time stored on every failure.
        if (address.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
        {
            Raise(() => Fan(Warning, "UDP can only send to IP addresses!"));
            return;
        }
        var sock = _sock;
        if (sock is null)
        {
            // fd −1 (a send before Start or after Stop): UDP SendData has no fd guard, and sendto(−1) fails and
            // takes the AddSendError(6) path (0x0083A374, 0x0083A386; verifier reading, batch 2c; pending
            // inventory correction). Nothing is sent.
            SendFailedLocked("no socket (fd -1)");
        }
        else
        {
            try
            {
                int sent = SendHook is { } hook ? hook(sock, raw, address) : sock.SendTo(raw, SocketFlags.None, address);
                if (sent != raw.Length)
                {
                    int want = raw.Length;
                    Raise(() => Fan(Warning, $"SentWrongNumBytes: sent {sent} of {want} bytes to {address}"));
                }
            }
            catch (ObjectDisposedException) { return; }                   // closed under us during shutdown
            catch (SocketException e) { SendFailedLocked($"{e.SocketErrorCode} ({e.Message})"); }
        }
        if (FrameTrace is not null)
        {
            FrameCodec.TryDecode(raw, out var f, out var err);
            var utc = DateTime.UtcNow;
            Raise(() => Fan(FrameTrace, new FrameEvent(true, utc, raw, f, err)));
        }
    }

    // fidelity: M1-022
    /// <summary>
    /// B10's failure path: AddSendError(6) (0x0083A552), always; then the time at +0x88 (0x0083A5F4) and the
    /// warning, both given every time here. MISSING (B10 rate limit; verifier reading 0x0083A648..0x0083A666,
    /// pending inventory correction): the source's condition on the warning and the +0x88 store is not
    /// reproduced.
    /// </summary>
    private void SendFailedLocked(string why)
    {
        UdpSendErrors.Add(6);
        LastSendErrorMs = _clock.NowMs;
        Raise(() => Fan(Warning, $"sendto failed: {why}"));
    }

    // fidelity: M1-010, M1-021
    /// <summary>
    /// Called by the scheduler when the 2 ms entry is due: G1.7, a copy of the callback is posted to the
    /// executor (AddTaskHolder 0x007FC270); the scheduler thread never runs it.
    /// </summary>
    private void PostTick() => _exec.Post(seq => { ExecutorTrace?.Invoke(new ExecutorItem("tick", seq)); RunTick(); });

    // fidelity: M1-010, M1-015
    /// <summary>
    /// The R35 lambda (0x008383CE) as each posted copy runs it on the executor: ReliableTransport::Update,
    /// here <see cref="Pump"/>, and when it returns false the +0xA1 timed-out flag is set (0x008383D4..
    /// 0x008383DC; B22, B36). G1.10: a copy whose handle has expired (the transport has been disposed) is
    /// skipped. With no connection the update finds nothing to do, and it goes on being run all the same.
    /// </summary>
    private void RunTick()
    {
        if (_tickHandleExpired) return;
        if (!Pump()) _timedOut = true;
    }

    // fidelity: M1-010, M1-015, M1-032
    /// <summary>
    /// One <c>ReliableTransport::Update</c> 0x00837B8C (R34): under the lock, the UDP transport's own update
    /// runs first (0x00837BA2) and drains every datagram waiting on the socket, then each connection is
    /// updated. An incoming ack is therefore applied before the same update decides whether anything needs
    /// resending. R19 / B22 / G2.8: a connection whose Update reports a timeout is logged
    /// "Disconnecting TimedOut Connection" (0x00837BCC), the receiver gets OnDisconnected with its address
    /// (0x00837C50..0x00837C56), it is deleted (0x00837C60), and the update returns false; no frame is sent
    /// for it. The other connections are still updated. Returns false if any connection timed out.
    /// </summary>
    internal bool Pump()
    {
        var effects = new List<Action>();
        bool ok = true;
        lock (_lock)
        {
            DrainLocked(effects);
            foreach (var (address, c) in _connections)
            {
                if (c.Update()) continue;
                var at = address;
                effects.Add(() => Fan(Warning, $"Disconnecting TimedOut Connection {at}"));
                Receiver(effects, ReceiverMarker.OnDisconnected, address);
                ConnectionGoneLocked(DeleteConnectionLocked(address),
                    $"connection timed out (> {_o.ConnectionTimeoutMs} ms without any datagram)", effects);
                ok = false;
            }
        }
        RaiseAll(effects);
        return ok;
    }

    // fidelity: M1-022, M1-023
    /// <summary>
    /// UDPTransport::Update. First the reset (G4.5, B18): with the flag set, CloseSocket (0x0083ACF0) and, only if
    /// that returned 1 (0x0083ACF4), OpenSocket on the stored port (0x0083ACF8..0x0083ACFE), which the close has
    /// just set to 47817 (B38); the flag is cleared either way (0x0083AD04). Then the read loop, only if fd &gt;= 0
    /// (0x0083AD08), for as long as TryToReadMessage returns 1 (0x0083AD10..0x0083AD18; B16):
    ///  - a read that returns ≤ 0 stops the loop for this update (0x0083AA98), a 0-byte datagram included;
    ///    whether a 0-byte read also warns is MISSING (see the loop);
    ///  - EAGAIN is silent (0x0083AAC4); any other error warns "ReadFailed" (0x0083AAF6);
    ///  - ENOTCONN (0x0083AB22 cmp r0,#0x6b), after that warning, closes the socket (0x0083AB28) and, only if the
    ///    close succeeded (0x0083AB2C cbz r0), reopens it on the stored port (0x0083AB2E, 0x0083AB34), 47817;
    ///  - a datagram larger than the 1472-byte buffer (MSG_TRUNC) is dropped after the prefix checks and the read
    ///    goes on (B17).
    /// A receive error never tears a connection down.
    /// Host mapping: EAGAIN is <see cref="SocketError.WouldBlock"/>, ENOTCONN is <see cref="SocketError.NotConnected"/>,
    /// and MSG_TRUNC is <see cref="SocketError.MessageSize"/> with the buffer holding the datagram's first 1472
    /// bytes and the datagram consumed. Windows also reports an ICMP port-unreachable as a receive error,
    /// <see cref="SocketError.ConnectionReset"/> (SIO_UDP_CONNRESET is left on); here it is a generic
    /// "ReadFailed" error, pending policy M1-039, which a later step implements.
    /// </summary>
    private void DrainLocked(List<Action> effects)
    {
        if (_resetRequested)
        {
            if (CloseSocketLocked(effects)) OpenSocketLocked(_localPort, effects);
            _resetRequested = false;
        }

        var buf = _rxBuffer;
        while (_sock is { } sock)
        {
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            int n;
            try { n = ReceiveHook is { } hook ? hook(sock, buf, ref from) : ReadDontWait(sock, buf, ref from); }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.WouldBlock) { return; }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.MessageSize)
            {
                TruncatedDatagramLocked(buf.ToArray(), effects);
                continue;
            }
            catch (SocketException e)
            {
                var code = e.SocketErrorCode; var msg = e.Message;
                effects.Add(() => Fan(Warning, $"ReadFailed: {code} ({msg})"));
                if (code == SocketError.NotConnected && CloseSocketLocked(effects)) OpenSocketLocked(_localPort, effects);
                return;
            }
            catch (ObjectDisposedException) { return; }

            // B16: ≤ 0 stops, a 0-byte datagram too (0x0083AA98).
            // MISSING: the source sends a 0-byte read down the errno path (0x0083AA98 blt → 0x0083AAC4 /
            // 0x0083AAF6 / 0x0083AB22) with whatever errno is left over (verifier reading, batch 2c; pending
            // inventory correction), so whether it warns "ReadFailed" or even reopens is not established. Here it
            // only stops the loop, with no warning.
            if (n <= 0) return;
            ProcessDatagramLocked(buf.AsSpan(0, n).ToArray(), from as IPEndPoint, effects);
        }
    }

    // --------------------------------------------------------------- receive

    /// <summary>Feed a raw datagram from the peer (also used by the conformance replay tool).</summary>
    public void ProcessIncoming(byte[] raw) => ProcessIncoming(raw, null);

    /// <summary>Feed a raw datagram from <paramref name="from"/>; null means the peer.</summary>
    public void ProcessIncoming(byte[] raw, IPEndPoint? from)
    {
        var effects = new List<Action>();
        lock (_lock) ProcessDatagramLocked(raw, from ?? _peer, effects);
        RaiseAll(effects);
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

    /// <summary>One datagram, in the order set out on the class.</summary>
    private void ProcessDatagramLocked(byte[] raw, IPEndPoint? from, List<Action> effects)
    {
        var utc = DateTime.UtcNow;

        // UDPTransport::HandleReceivedMessage: the COZ\x03 prefix.
        if (!UdpPrefixOkLocked(raw, utc, effects)) return;
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
            return;
        }

        var type = (ReliableMessageType)post[3];
        ushort seqMin = BinaryPrimitives.ReadUInt16LittleEndian(post[4..]);
        ushort seqMax = BinaryPrimitives.ReadUInt16LittleEndian(post[6..]);
        ushort ack = BinaryPrimitives.ReadUInt16LittleEndian(post[8..]);
        bool isReliable = seqMin != SequenceId.Invalid || seqMax != SequenceId.Invalid;
        var body = post[ReliableHeader.ReliableLength..];
        bool multiple = ReliableMessageTypes.IsMultiple(type);

        // fidelity: M1-018, M1-032
        // R13 / B19 / G2.10: FindConnection(addr, create) (0x008377CE..0x008377FC), where create means a type-1
        // frame or a container whose first sub-message is type 1. A new connection is processed from here as a
        // known one. With none the frame is dropped as "unconnected source" (0x0083789A), its ack unprocessed.
        // MISSING: R13 does not say what the first-sub test reads for a container with an empty body; with no
        // sub-message there is no type 1, so no connection is created.
        bool create = type == ReliableMessageType.ConnectionRequest
                      || (multiple && body.Length > 0 && body[0] == (byte)ReliableMessageType.ConnectionRequest);
        var c = FindConnection(from);
        if (c is null)
        {
            if (from is null || !create)
            {
                var seen = from;
                effects.Add(() => Fan(Warning, $"unconnected source {seen}; {type} frame dropped"));
                return;
            }
            c = CreateConnectionLocked(from);
        }

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
                if (type != ReliableMessageType.MultipleMixedMessages) { ReliableReceiveErrors.Add(5); return; }
            }
            else
            {
                c.AckMessage(seqMax);
                if (_o.SendAckOnReceipt) c.Queue(ReliableMessageType.Ack, Array.Empty<byte>(), false, true);
            }
        }

        bool stop = false;
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
                HandleSubMessageLocked(subType, body.Slice(start, size).ToArray(), subReliable ? seq : SequenceId.Invalid, c, from!, effects, ref stop);
                if (subReliable) seq = SequenceId.Next(seq);
                o = start + size;

                // fidelity: M1-038
                // Policy D8: once a DisconnectRequest has been handled, the rest of the frame is not processed.
                // The original deletes the connection there (0x008374A0) and keeps walking through the freed
                // pointer, which is not reproduced. A DisconnectRequest dropped by R12 (out of sequence) is not
                // handled, sets nothing, and the walk goes on as in the original.
                if (stop) break;
            }
        }
        else
        {
            HandleSubMessageLocked(type, body.ToArray(), isReliable ? seqMin : SequenceId.Invalid, c, from!, effects, ref stop);
        }
    }

    // fidelity: M1-003, M1-019
    /// <summary>
    /// ReliableTransport::HandleSubMessage 0x00837418 (R12): a reliable sub-message out of sequence is dropped
    /// silently; then 1 = OnConnectRequest, 2 = OnConnected, 3 = OnDisconnected then DeleteConnection (that
    /// connection only; <paramref name="stop"/> is set for M1-038), 4 and 5 = data, 6 = multipart, 7..10 =
    /// nothing, 11 = ReceivePing. The connection events go to the receiver with the address (R40).
    /// </summary>
    private void HandleSubMessageLocked(ReliableMessageType type, byte[] payload, ushort seq, ReliableConnection c,
                                        IPEndPoint from, List<Action> effects, ref bool stop)
    {
        if (seq != SequenceId.Invalid && !c.AcceptReliable(seq)) return;
        switch (type)
        {
            case ReliableMessageType.ConnectionRequest:
                Receiver(effects, ReceiverMarker.OnConnectRequest, from);
                break;
            case ReliableMessageType.ConnectionResponse:
                Receiver(effects, ReceiverMarker.OnConnected, from);
                // The facade: only the current link's connection, and only while it is Connecting.
                // MISSING: the rows do not say whether the app layer checks the address of a connection event
                // (B23 and B31 name none); the facade follows the current link's connection only, as it did
                // when no other connection could exist.
                if (ReferenceEquals(c, _linkConn) && State == LinkState.Connecting) { State = LinkState.Connected; effects.Add(() => Fan(Connected)); }
                break;
            case ReliableMessageType.DisconnectRequest:
                // fidelity: M1-003, M1-038
                Receiver(effects, ReceiverMarker.OnDisconnected, from);
                ConnectionGoneLocked(DeleteConnectionLocked(from), "peer sent DisconnectRequest", effects);
                stop = true;
                break;
            case ReliableMessageType.SingleReliableMessage:
            case ReliableMessageType.SingleUnreliableMessage:
                DeliverDataLocked(payload, from, effects); break;
            case ReliableMessageType.MultiPartMessage:
            {
                // fidelity: M1-009
                // R23 on this connection's own assembly (GetPendingMultiPartMessage 0x008374C8).
                var mp = c.MultiPart;
                if (payload.Length > 2)
                {
                    int idx = payload[0], cnt = payload[1];
                    if (idx == mp.Next)
                    {
                        if (idx == 1) { mp.Data.Clear(); mp.Last = cnt; }
                        mp.Data.AddRange(payload.AsSpan(2).ToArray()); mp.Next++;
                        if (idx == mp.Last)
                        {
                            DeliverDataLocked(mp.Data.ToArray(), from, effects);
                            mp.Clear();
                        }
                    }
                    else
                    {
                        var w = $"multipart out of order {idx}/{cnt}, expected {mp.Next}";
                        effects.Add(() => Fan(Warning, w));
                    }
                }
                break;
            }
            case ReliableMessageType.Ack: break;
            case ReliableMessageType.Ping: c.ReceivePing(payload); break;
            default:
                var unhandled = $"unhandled sub-message type {type}";
                effects.Add(() => Fan(Warning, unhandled));
                break;
        }
    }

    /// <summary>
    /// A data payload: to the receiver with its address (R40), then RobotConnectionManager's gate on an
    /// arrived data message for the facade: delivered only while Connected (0x0062F724-2A) and from the
    /// peer's IP (0x0062F744); dropped otherwise, never held. The address test is TransportAddress::operator==
    /// 0x00838E92-A6, which compares the IP and not the port.
    /// </summary>
    private void DeliverDataLocked(byte[] payload, IPEndPoint? from, List<Action> effects)
    {
        Receiver(effects, ReceiverMarker.Data, from, payload);
        if (State != LinkState.Connected || from is null || _peer is not { } peer || !from.Address.Equals(peer.Address)) return;
        effects.Add(() => Fan(DataReceived, payload));
    }
}


/// <summary>
/// Counts of AddRecvError (or, for <see cref="ReliableTransport.UdpSendErrors"/>, AddSendError) calls by error
/// code, for diagnostics only: nothing in the transport reads them.
/// </summary>
public sealed class ReceiveErrorCounts
{
    private readonly Dictionary<int, int> _byCode = new();

    /// <summary>How many times AddRecvError(<paramref name="code"/>) has been counted.</summary>
    public int this[int code] { get { lock (_byCode) return _byCode.GetValueOrDefault(code); } }

    internal void Add(int code) { lock (_byCode) _byCode[code] = _byCode.GetValueOrDefault(code) + 1; }
}

// fidelity: M1-037
/// <summary>
/// Policy M1-037 (D5): the host notification that stands in for Android's process network bind and unbind.
/// <see cref="Host"/> is <c>System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged</c>, the
/// production source; tests give a transport a source of their own so they can raise it.
/// </summary>
internal sealed class HostNetworkChange
{
    private readonly Action<NetworkAddressChangedEventHandler> _add, _remove;

    internal HostNetworkChange(Action<NetworkAddressChangedEventHandler> add,
                               Action<NetworkAddressChangedEventHandler> remove)
    {
        _add = add; _remove = remove;
    }

    /// <summary>The production source: <c>NetworkChange.NetworkAddressChanged</c>.</summary>
    internal static readonly HostNetworkChange Host = new(
        h => NetworkChange.NetworkAddressChanged += h,
        h => NetworkChange.NetworkAddressChanged -= h);

    internal void Subscribe(NetworkAddressChangedEventHandler h) => _add(h);
    internal void Unsubscribe(NetworkAddressChangedEventHandler h) => _remove(h);
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
