using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Transport;

namespace Cozmo.Robot;

// =====================================================================================================
// The engine's app layer for M1: CozmoInstanceRunner (the 60 ms tick), MessageHandler,
// RobotConnectionManager / RobotConnectionData, RobotManager with RobotInitialConnection, and the parts of
// Robot that the connection opens (time sync, ready to stream, first full state, idle timeout).
// Every behaviour here cites a row of re-analysis/inventory/M1-transport.md or a COMPATIBILITY_POLICY record.
// =====================================================================================================

// fidelity: M1-028, M1-015
/// <summary>RobotConnectionResult, as the engine produces it (B29, CB15, CB32). NeedsPin, InvalidPin and PinMax exist in the game's enum but the engine never produces them (CB35); their values are not in the inventory.</summary>
public enum RobotConnectionResult : byte
{
    Success = 0,
    ConnectionFailure = 1,
    ConnectionRejected = 2,
    OutdatedFirmware = 3,
    OutdatedApp = 4,
}

// fidelity: M1-025
/// <summary>RobotDisconnectReason 0..11 (CC18, 0x00775BEC, table 0x01033970). Its only reader is a DAS event (CC20).</summary>
public enum RobotDisconnectReason : byte
{
    Unknown = 0, WifiTimeout = 1, SleepSettings = 2, SleepEraseCozmo = 3, SleepPlacedOnCharger = 4,
    SleepBackground = 5, ExitSDKMode = 6, OutdatedFirmware = 7, OutdatedApp = 8, DebugForceDisconnect = 9,
    DebugDataPersistenceReset = 10, AppTerminated = 11,
}

// fidelity: M1-028
/// <summary>The game message RobotConnectionResponse (CB19): {result u8, fwVersion u32, serial u32, bodyHWVersion i32, bodyColor i8}, 14 bytes.</summary>
public sealed record RobotConnectionResponse(RobotConnectionResult Result, uint FwVersion, uint SerialNumber, int BodyHWVersion, sbyte BodyColor);

// fidelity: M1-015
/// <summary>The game message RobotDisconnected{timeSinceLastMsg_sec 0.0} (CB33).</summary>
public sealed record RobotDisconnectedMessage(float TimeSinceLastMsgSec);

// fidelity: M1-027
/// <summary>The game message RobotErrorPassThrough{code} for a non-fatal robotError (CC16).</summary>
public sealed record RobotErrorPassThrough(uint Code);

/// <summary>Thrown by <see cref="CozmoRobot.ConnectAsync"/> when the engine's RobotConnectionResponse is not Success.</summary>
public sealed class RobotConnectionException : IOException
{
    public RobotConnectionResponse Response { get; }
    public RobotConnectionException(RobotConnectionResponse response)
        : base($"RobotConnectionResponse {response.Result} (fw {response.FwVersion})") => Response = response;
}

/// <summary>Where the engine finds its inputs, and the app defaults policy M1-042 sends.</summary>
public sealed class CozmoEngineOptions
{
    // fidelity: M1-029
    /// <summary>
    /// DataPlatformResourcesPath: the extracted <c>cozmo_resources</c> directory (G5.34, G5.35). The firmware
    /// header is read from <see cref="FirmwareHeader.ResourceRelativePath"/> under it (G5.17). Null: the header
    /// is not loaded and the expected version and time stay 0/0 (G5.31).
    /// MISSING: this stack has no fixed resources location; the caller names it.
    /// </summary>
    public string? ResourcesPath { get; init; }

    /// <summary>Where the block pool is kept (<see cref="CubeConnections.Init"/>); an empty path turns persistence off.</summary>
    public string? BlockPoolPath { get; init; }

    // fidelity: M1-042
    /// <summary>The stored robot volume the app sends after a Success response (policy M1-042; default 1.0).</summary>
    public float RobotVolume { get; init; } = 1.0f;
}

// fidelity: M1-019, M1-026
/// <summary>
/// What the app layer uses of the transport (R38, R39, R40, B21, B28): Start (RCM::Init's tail StartClient, B12),
/// Connect and Disconnect by address, SendData (always reliable, flush 0, B28), the +0xA1 timed-out flag (B36) and
/// the receiver (R40).
/// </summary>
internal interface IEngineTransport
{
    void Start();
    void Connect(IPAddress ip, bool isSimulated);
    void Disconnect(IPEndPoint address);
    void SendData(byte[] clad);
    bool TimedOut { get; }
    event Action<ReceiverEvent>? Received;
}

// fidelity: M1-026
/// <summary>The production port: the stack's <see cref="ReliableTransport"/>.</summary>
internal sealed class ReliableTransportPort : IEngineTransport
{
    private readonly ReliableTransport _t;
    /// <summary>The engine's log, set once the engine exists.</summary>
    internal Action<string>? Log;
    public ReliableTransportPort(ReliableTransport t) => _t = t;
    private void _log(string line) => Log?.Invoke(line);
    public void Start() => _t.Start();
    public void Connect(IPAddress ip, bool isSimulated) => _t.Connect(ip, isSimulated);
    public void Disconnect(IPEndPoint address) { try { _t.Disconnect(address); } catch (ObjectDisposedException) { } }

    /// <summary>
    /// B28: RT->SendData(reliable = 1, addr, buf, size, flush = 0) (0x0062F5CA..0x0062F5D2). The original posts the
    /// send and a SendMessage that finds no connection only warns "unconnected destination" (R13 send side, E9).
    /// Host mapping: the stack's transport facade refuses with an exception once its link has ended, which is the
    /// same drop; it is logged as that warning and not thrown at the caller (CB29: no exception).
    /// </summary>
    public void SendData(byte[] clad)
    {
        try { _t.SendData(clad, reliable: true, flush: false); }
        catch (ObjectDisposedException) { _log("warning: unconnected destination: the transport is disposed"); }
        catch (InvalidOperationException) { _log("warning: unconnected destination: robot message dropped by the transport"); }
    }

    public bool TimedOut => _t.TimedOut;
    public event Action<ReceiverEvent>? Received { add => _t.Received += value; remove => _t.Received -= value; }
}

// fidelity: M1-024
/// <summary>BaseStationTimer (CD9): the engine clock, updated once per tick in state 3 (CD8, CC10).</summary>
public sealed class BaseStationTimer
{
    private long _ns;
    private double _seconds;
    /// <summary>Nanoseconds since the run started, as of this tick's start (CD2, CD9).</summary>
    public long Nanoseconds => Volatile.Read(ref _ns);
    public double Seconds => Volatile.Read(ref _seconds);
    public float SecondsF => (float)Seconds;
    public double DeltaSeconds { get; private set; }
    public long TickCount { get; private set; }
    /// <summary>GetCurrentTimeStamp = (u32)(seconds × 1000) (CD9).</summary>
    public uint TimeStampMs => (uint)(Seconds * 1000.0);

    internal void UpdateTime(long ns)
    {
        double s = ns / 1e9;
        DeltaSeconds = s - Seconds;
        Volatile.Write(ref _ns, ns);
        Volatile.Write(ref _seconds, s);
        TickCount++;
    }
}

// fidelity: M1-024, M1-013
/// <summary>
/// CozmoInstanceRunner::Run (B24, CD1..CD5): a fixed-rate 60 ms loop on the monotonic clock.
///  - CD1: runStart = now; the first tick runs at once; the first target is runStart + 60 ms.
///  - CD2: the tick is given the ns elapsed since runStart at its start; a nonzero result logs and stops the loop.
///  - CD3: after the tick rem = target − now; it sleeps rem_us only if rem_ns ≥ 1000; next target = old + 60 ms,
///    so overrun ticks run back to back until caught up.
///  - CD4: "overtime" is a log only (debug below −10 ms, warning below −200 ms).
///  - CD5: if rem ≤ −240 ms the target moves forward by floor(−rem / 60 ms) × 60 ms; it never runs extra ticks.
/// The catch-up adds to the regular +60 ms: the target += period at 0x0065B5EE, then += n × period at 0x0065B63C
/// (batch 3 verifier reading of CD3/CD5's ranges).
/// Host realisation (policy M1-013): the thread holds <see cref="HighResolutionTimer"/>; a sleep is 1 ms steps
/// while more than 1.5 ms remain and a spin for the rest, as the transport scheduler does.
/// </summary>
internal sealed class EngineTickRunner
{
    /// <summary>B24: 0x3938700 ns = 60 ms (0x0065B3D2/0x0065B3D8).</summary>
    public const long PeriodNs = 0x3938700;
    public const long OvertimeDebugNs = -10_000_000;     // CD4
    public const long OvertimeWarnNs = -200_000_000;     // CD4
    public const long CatchupNs = -240_000_000;          // CD5

    private readonly Func<long> _nowNs;
    private readonly Action<long> _sleepNs;
    private readonly Func<long, int> _tick;
    private readonly Action<string> _log;
    private volatile bool _stop;
    private Thread? _thread;

    public EngineTickRunner(Func<long> nowNs, Action<long> sleepNs, Func<long, int> tick, Action<string> log)
    {
        _nowNs = nowNs; _sleepNs = sleepNs; _tick = tick; _log = log;
    }

    /// <summary>The loop itself; returns when stopped or when a tick returns nonzero.</summary>
    public void Run()
    {
        long runStart = _nowNs();                        // CD1 (0x0065B3B8)
        long target = runStart + PeriodNs;               // CD1 (0x0065B3E0..0x0065B3EA)
        while (!_stop)
        {
            long start = _nowNs();
            int r = _tick(start - runStart);             // CD2 (0x0065B3F4..0x0065B42C)
            if (r != 0) { _log($"error: engine update returned {r}; the run loop stops"); return; }   // CD2 (0x0065B704..0x0065B712)
            long rem = target - _nowNs();                // CD3 (0x0065B432..0x0065B45E)
            if (rem < OvertimeWarnNs) _log($"warning: overtime {rem / 1e6:F1} ms");                   // CD4
            else if (rem < OvertimeDebugNs) _log($"debug: overtime {rem / 1e6:F1} ms");               // CD4
            if (rem >= 1000) _sleepNs(rem / 1000 * 1000);                                           // CD3: rem_us (0x0065B576..0x0065B5D2)
            target += PeriodNs;                                                                       // CD3 (0x0065B5EE)
            if (rem <= CatchupNs) target += -rem / PeriodNs * PeriodNs;                               // CD5 (0x0065B63C)
        }
    }

    public void Start(string name)
    {
        _thread = new Thread(() => { using var _ = new HighResolutionTimer(); Run(); }) { IsBackground = true, Name = name };
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
        var t = _thread;
        if (t is not null && t != Thread.CurrentThread && t.IsAlive) t.Join(TimeSpan.FromSeconds(2));
    }

    /// <summary>The host's monotonic clock in nanoseconds, standing in for steady_clock::now().</summary>
    public static long HostNowNs() => (long)((Int128)Stopwatch.GetTimestamp() * 1_000_000_000 / Stopwatch.Frequency);

    // fidelity: M1-013
    /// <summary>The host sleep for rem_us (policy M1-013).</summary>
    public static void HostSleepNs(long ns)
    {
        long end = HostNowNs() + ns;
        while (true)
        {
            long left = end - HostNowNs();
            if (left <= 0) return;
            if (left > 1_500_000) Thread.Sleep(1); else Thread.SpinWait(200);
        }
    }
}

// fidelity: M1-024
/// <summary>
/// RobotConnectionData: state (+4: 0 none, 1 connecting, 2 connected), the robot address (+0x30) and the
/// arrived-message queue filled by the transport's receiver under a mutex (B20).
/// </summary>
internal sealed class RobotConnectionData
{
    /// <summary>B20: the arrived-message kinds. 1 (connect request) is never pushed: RCD::ReceiveData drops that marker.</summary>
    internal enum ArrivedKind : byte { Data = 0, ConnectRequest = 1, Connected = 2, Disconnected = 3 }
    internal readonly record struct Arrived(ArrivedKind Kind, IPEndPoint? Address, byte[]? Data);

    private readonly object _mutex = new();
    private readonly Queue<Arrived> _queue = new();
    private volatile int _state;
    private volatile IPEndPoint? _address;

    public int State { get => _state; set => _state = value; }
    public IPEndPoint? Address { get => _address; set => _address = value; }

    /// <summary>Raised after an arrival has been queued (the offline test seam runs the engine tick from it).</summary>
    internal Action? Queued;

    // fidelity: M1-024, M1-025
    /// <summary>
    /// RCD::ReceiveData (B20): the OnConnectRequest marker is dropped (0x0062E4BE); PushArrivedMessage maps
    /// OnConnected → 2, OnDisconnected → 3, anything else to data with the bytes copied (0x0062E274..0x0062E2C2),
    /// under the mutex. No address is compared here (CB38).
    /// </summary>
    public void ReceiveData(ReceiverEvent e)
    {
        ArrivedKind kind;
        switch (e.Marker)
        {
            case ReceiverMarker.OnConnectRequest: return;
            case ReceiverMarker.OnConnected: kind = ArrivedKind.Connected; break;
            case ReceiverMarker.OnDisconnected: kind = ArrivedKind.Disconnected; break;
            default: kind = ArrivedKind.Data; break;
        }
        byte[]? data = e.Data is null ? null : (byte[])e.Data.Clone();
        lock (_mutex) _queue.Enqueue(new Arrived(kind, e.Address, data));
        Queued?.Invoke();
    }

    // fidelity: M1-025
    /// <summary>QueueConnectionDisconnect (CC29): one OnDisconnected marker onto the queue, on the engine thread.</summary>
    public void QueueConnectionDisconnect()
    {
        lock (_mutex) _queue.Enqueue(new Arrived(ArrivedKind.Disconnected, _address, null));
        Queued?.Invoke();
    }

    public bool TryPop(out Arrived a)
    {
        lock (_mutex)
        {
            if (_queue.Count == 0) { a = default; return false; }
            a = _queue.Dequeue();
            return true;
        }
    }

    public bool HasArrivals { get { lock (_mutex) return _queue.Count > 0; } }

    /// <summary>
    /// RobotConnectionData::Clear (CB4, CC23; 0x0062E4CC..0x0062E50A, batch 3 verifier reading): the state goes to 0,
    /// the address becomes the default TransportAddress() (0x0062E4E2..0x0062E4F2, here null: unset), and the queue
    /// is cleared under the mutex (CC30: "RCD::Clear drops later items"). So a DisconnectCurrent after a handled
    /// disconnect addresses an unset address and sends nothing.
    /// </summary>
    public void Clear()
    {
        lock (_mutex)
        {
            _queue.Clear();
            _address = null;
            _state = 0;
        }
    }
}

// fidelity: M1-024, M1-025, M1-026, M1-015
/// <summary>RobotConnectionManager: the connection lifecycle and the data deque (+0x20) MessageHandler pops.</summary>
internal sealed class RobotConnectionManager
{
    private readonly CozmoEngine _engine;
    private readonly IEngineTransport _t;
    private readonly Queue<byte[]> _data = new();       // RCM+0x20, touched only on the engine thread
    public RobotConnectionData Rcd { get; } = new();

    /// <summary>The disconnect reason byte, RCM+0x38 (CC19). Its only reader is a DAS event (CC20).</summary>
    public RobotDisconnectReason Reason { get; set; }

    public RobotConnectionManager(CozmoEngine engine, IEngineTransport t) { _engine = engine; _t = t; }

    // fidelity: M1-025
    /// <summary>RCM::Connect (CB4, B21): RCD Clear; address to RCD+0x30; RT Disconnect(addr); RT Connect(addr); state 1. It does not reset the reason (CC19).</summary>
    public void Connect(IPAddress ip, bool isSimulated)
    {
        var addr = new IPEndPoint(ip, RobotAddress.RemotePort(isSimulated));   // CB3 / B3 (M1-001)
        Rcd.Clear();
        Rcd.Address = addr;
        _t.Disconnect(addr);
        _t.Connect(ip, isSimulated);
        Rcd.State = 1;
    }

    // fidelity: M1-026
    /// <summary>RCM::SendData (B28): only in state 2, else "NotValidState" and failure; then RT->SendData(reliable 1, flush 0).</summary>
    public bool SendData(byte[] clad)
    {
        if (Rcd.State != 2) { _engine.Log("warning: RobotConnectionManager.SendData.NotValidState"); return false; }
        _t.SendData(clad);
        return true;
    }

    // fidelity: M1-025
    /// <summary>
    /// DisconnectCurrent (B33, CC29): RT Disconnect(RCD+0x30), which queues the type 3 then DeleteConnection, then
    /// QueueConnectionDisconnect pushes one OnDisconnected marker; no state check. The marker is handled at the next
    /// ProcessArrivedMessages (CC30). The queue-stats DAS event is telemetry (B37) and not reproduced.
    /// With the address unset (never connected, or cleared by RCD::Clear) the original's RT Disconnect finds no
    /// connection, warns "unconnected destination" and sends nothing (CA14); skipping the transport call here is
    /// wire-equivalent.
    /// </summary>
    public void DisconnectCurrent()
    {
        if (Rcd.Address is { } a) _t.Disconnect(a);
        Rcd.QueueConnectionDisconnect();
    }

    /// <summary>RCM::ClearData (CC15): drops what is left in RCM+0x20.</summary>
    public void ClearData() => _data.Clear();

    public bool PopData(out byte[] data)
    {
        if (_data.Count == 0) { data = Array.Empty<byte>(); return false; }
        data = _data.Dequeue();
        return true;
    }

    // fidelity: M1-024
    /// <summary>
    /// RCM::Update (B25): ProcessArrivedMessages drains the RCD queue FIFO: 0 → HandleDataMessage, 2 →
    /// HandleConnectionResponseMessage, 3 → HandleDisconnectMessage (tbb 0x0062F434).
    /// The sync-mode RT::Update (B15) is not reached in production (M1-020) and is not reproduced.
    /// It pops until the queue is empty (0x0062F4B4..0x0062F4BC, batch 3 verifier reading of B25's range), so an
    /// arrival queued during the drain is handled in the same drain.
    /// </summary>
    public void Update()
    {
        while (Rcd.TryPop(out var a))
        {
            switch (a.Kind)
            {
                case RobotConnectionData.ArrivedKind.Data: HandleDataMessage(a); break;
                case RobotConnectionData.ArrivedKind.Connected: HandleConnectionResponseMessage(); break;
                case RobotConnectionData.ArrivedKind.Disconnected: HandleDisconnectMessage(); break;
            }
        }
    }

    // fidelity: M1-024
    /// <summary>
    /// HandleDataMessage (B26): state ≠ 2 → "Connection not yet valid, dropping message" (0x0062F728); source ≠
    /// RCD+0x30 → "Expected messages from %s ... Dropping" (0x0062F744); otherwise onto the RCM deque (0x0062F7DE).
    /// The compare is TransportAddress::operator==, which compares the IP and not the port (0x0062F744 → 0x00838E5C;
    /// 0x00838E9A..0x00838EA4, batch 3 verifier reading).
    /// </summary>
    private void HandleDataMessage(RobotConnectionData.Arrived a)
    {
        if (Rcd.State != 2) { _engine.Log("warning: Connection not yet valid, dropping message"); return; }
        if (a.Address is null || Rcd.Address is not { } expected || !a.Address.Address.Equals(expected.Address))
        {
            _engine.Log($"warning: Expected messages from {Rcd.Address}, got one from {a.Address}. Dropping");
            return;
        }
        _data.Enqueue(a.Data ?? Array.Empty<byte>());
    }

    // fidelity: M1-025
    /// <summary>HandleConnectionResponseMessage (B23, CB10): state 1 → 2 and reason 0; otherwise an error. No address compare, nothing sent.</summary>
    private void HandleConnectionResponseMessage()
    {
        if (Rcd.State == 1) { Rcd.State = 2; Reason = RobotDisconnectReason.Unknown; }
        else _engine.Log("error: Got connection response at unexpected time");
    }

    // fidelity: M1-015
    /// <summary>
    /// HandleDisconnectMessage (CC23, B31, CB32): the message fields are ignored; the RCD state is captured first;
    /// the +0xA1 timed-out flag gives reason 1; a DAS event with the reason (logged here); reason = 0; RCD Clear;
    /// then, if a robot exists, RemoveRobot(id, state == 1). It runs even with state 0.
    /// </summary>
    private void HandleDisconnectMessage()
    {
        int state = Rcd.State;
        if (_t.TimedOut) Reason = RobotDisconnectReason.WifiTimeout;
        _engine.Log($"info: disconnect_reason {Reason}");
        Reason = RobotDisconnectReason.Unknown;
        Rcd.Clear();
        if (_engine.Robots.RobotExists(CozmoEngine.RobotId)) _engine.Robots.RemoveRobot(CozmoEngine.RobotId, wasConnecting: state == 1);
    }
}

// fidelity: M1-024, M1-026, M1-027, M1-030
/// <summary>MessageHandler: the per-tick dispatch of robot messages (CC32) and the send path (B28, CB29).</summary>
internal sealed class MessageHandler
{
    private readonly CozmoEngine _engine;
    private readonly RobotConnectionManager _rcm;
    private int _sendCalls;       // MH+0x3C
    private int _processCalls;    // MH+0x38

    /// <summary>MH+0x28: set once the handler is initialised; ProcessMessages and SendMessage do nothing without it.</summary>
    public bool Initialised { get; set; }

    public MessageHandler(CozmoEngine engine, RobotConnectionManager rcm) { _engine = engine; _rcm = rcm; }

    public int SendCalls => Volatile.Read(ref _sendCalls);

    // fidelity: M1-024, M1-027, M1-030
    /// <summary>
    /// ProcessMessages (CC32, B25, B27): if MH+0x28 == 0 skip everything; else RCM::Update, then PopData FIFO with
    /// no cap. Per message: no robot → silent drop (CC31); size 0 → error; filtered → silent drop before unpack
    /// (CB28); unpack size mismatch → error and drop; robotError 0xD9: fatal → event, Broadcast, ClearData,
    /// DisconnectCurrent and the loop exits (CC15); non-fatal → event, RobotErrorPassThrough to the game, then
    /// Broadcast (CC16); otherwise Broadcast.
    /// </summary>
    public void ProcessMessages()
    {
        if (!Initialised) return;
        _rcm.Update();
        _processCalls++;
        while (_rcm.PopData(out var data))
        {
            var robot = _engine.Robots.Get(CozmoEngine.RobotId);
            if (robot is null) continue;
            if (data.Length == 0) { _engine.Log("error: MessageHandler.ProcessMessages: message of size 0"); continue; }
            if (_engine.Robots.ShouldFilterMessage(CozmoEngine.RobotId, data[0], robotToEngine: true)) continue;
            if (!TryUnpack(data, out var msg))
            {
                _engine.Log($"error: MessageHandler.ProcessMessages: size mismatch unpacking tag 0x{data[0]:X2} ({data.Length} bytes); dropped");
                continue;
            }
            if (msg is RobotErrorReport err)
            {
                // fidelity: M1-027
                // CC12/CC13: {u32 code, bool fatal}; fatal is decided by the byte only.
                if (err.Field1)
                {
                    _engine.Log($"error: robot.error.fatal code {err.Field0}");
                    _engine.Broadcast(msg);
                    _rcm.ClearData();
                    _rcm.DisconnectCurrent();
                    return;
                }
                _engine.Log($"warning: robot.error.nonfatal code {err.Field0}");
                _engine.RaiseRobotErrorPassThrough(new RobotErrorPassThrough(err.Field0));
                _engine.Broadcast(msg);
                continue;
            }
            _engine.Broadcast(msg);
        }
    }

    // fidelity: M1-027, M2-010, M2-011
    /// <summary>
    /// The unpack and its size check (B27, CC35; M2 inventory D1..D12). <c>RobotToEngine::Unpack</c> reads the tag
    /// and switches on tag - 0xB0 through the TBH table at 0x007B1B10 (D4). A tag outside 0xB0..0xF5, and each
    /// of the 14 in-range tags with no codec (0xCC, 0xDF..0xEB), takes the default at 0x007B1BBE, which returns
    /// GetBytesRead = 1 (D5): the tag alone is consumed, so a 1-byte message is kept and a longer one is a size
    /// error. The other 56 tags are the generated codecs. The unpack always returns the bytes consumed and every
    /// field read's failure is otherwise ignored (D7, D8, D10, D12, as <see cref="CladReader"/> reads), and
    /// ProcessMessages keeps the message only when that count equals the length (D1): trailing bytes drop it,
    /// an over-counted u8 array is kept shorter, and a truncated fixed message is kept when later smaller reads
    /// happen to consume exactly what is left (D11).
    /// </summary>
    internal static bool TryUnpack(byte[] data, out RobotMessage msg)
    {
        var id = (RobotMessageId)data[0];
        if (data[0] < 0xB0 || data[0] > 0xF5)
        {
            // CC35 / D5: outside the robot-to-engine union the unpack reads the tag alone.
            msg = new RawRobotMessage(id, data[1..], "tag outside the robot-to-engine range");
            return data.Length == 1;
        }
        if (GeneratedMessages.Parsers.TryGetValue(id, out var parse))
        {
            var r = new CladReader(data.AsMemory(1));
            msg = parse(r);
            return r.Remaining == 0;
        }
        // D5: 0xCC and 0xDF..0xEB have no codec; the default case consumes the tag alone.
        msg = new RawRobotMessage(id, data[1..], "no codec for this tag");
        return data.Length == 1;
    }

    // fidelity: M1-026, M1-030
    /// <summary>
    /// MessageHandler::SendMessage (B28, CB29): its reliable and hot arguments are ignored; it always counts the
    /// call (+0x3C) and returns failure silently (no exception, no log) when uninitialised, when the connection is
    /// not in state 2, or when the message is filtered (CB26); otherwise it packs and calls RCM::SendData. There is
    /// no batching: the transport gets the message at once (CD13).
    /// </summary>
    public bool SendMessage(RobotMessage m)
    {
        Interlocked.Increment(ref _sendCalls);
        if (!Initialised || _rcm.Rcd.State != 2) return false;
        if (_engine.Robots.ShouldFilterMessage(CozmoEngine.RobotId, (byte)m.Id, robotToEngine: false)) return false;
        return _rcm.SendData(m.ToBytes());
    }

    // fidelity: M1-031, M1-025
    /// <summary>MessageHandler::Disconnect = RCM::DisconnectCurrent (CC9).</summary>
    public void Disconnect() => _rcm.DisconnectCurrent();
}

// fidelity: M1-029
/// <summary>
/// FirmwareUpdater::LoadHeader and ParseFirmwareHeader (G5.16..G5.20, G5.31): read the whole file on a loader
/// thread, parse the JSON in the first 0x800 bytes up to the first NUL, and on success hand "version" and "time"
/// to the RobotManager. A missing file, a file shorter than 0x800 bytes or a parse failure leave the values as
/// they are (0/0).
/// Host mapping: jsoncpp's Reader is stood in for by System.Text.Json reading one value.
/// MISSING: jsoncpp's grammar (comments, trailing commas, other leniencies) is not in the rows; the reader here
/// skips comments and allows trailing commas, which may accept or refuse different inputs from the original.
/// </summary>
public static class FirmwareHeader
{
    /// <summary>G5.17: "config/engine/" + "firmware" + "/cozmo.safe" under the Scope 1 resources path.</summary>
    public const string ResourceRelativePath = "config/engine/firmware/cozmo.safe";
    /// <summary>G5.19: the header is the first 0x800 bytes.</summary>
    public const int HeaderBytes = 0x800;

    /// <summary>The header path under a resources directory (G5.17, G5.32 scope 1).</summary>
    public static string PathUnder(string resourcesPath) => Path.Combine(resourcesPath, "config", "engine", "firmware", "cozmo.safe");

    /// <summary>
    /// G5.19/G5.20 on bytes already read: null when the data is shorter than 0x800 or does not parse; otherwise
    /// the optional "version" and "time" values.
    /// </summary>
    public static (uint? Version, uint? Time)? Parse(byte[] file)
    {
        if (file.Length < HeaderBytes) return null;
        var head = file.AsSpan(0, HeaderBytes);
        int nul = head.IndexOf((byte)0);
        if (nul >= 0) head = head[..nul];
        if (!Json.TryParseFirst(head.ToArray(), out var root)) return null;
        using (root)
            return (Json.OptionalUInt(root.RootElement, "version"), Json.OptionalUInt(root.RootElement, "time"));
    }

    // fidelity: M1-029
    /// <summary>G5.18/G5.25: starts the loader thread; <paramref name="parsed"/> runs on it only when the header parsed.</summary>
    internal static Thread LoadAsync(string path, Action<uint?, uint?> parsed, Action<string> log)
    {
        var t = new Thread(() =>
        {
            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log($"error: FirmwareUpdater.LoadFirmwareFile: {path}: {e.Message}");
                return;
            }
            var h = Parse(file);
            if (h is null) { log($"error: FirmwareUpdater.LoadHeaderData: {path}: no header (size {file.Length} or unparsable)"); return; }
            parsed(h.Value.Version, h.Value.Time);
        }) { IsBackground = true, Name = "cozmo-firmware-header" };
        t.Start();
        return t;
    }
}

/// <summary>Reading JSON the way the rows need it (G5.2, G5.5, G5.19).</summary>
internal static class Json
{
    public static bool TryParseFirst(byte[] bytes, out JsonDocument doc)
    {
        try
        {
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (JsonDocument.TryParseValue(ref reader, out var d) && d is not null) { doc = d; return true; }
        }
        catch (JsonException) { }
        doc = null!;
        return false;
    }

    /// <summary>GetValueOptional&lt;uint&gt;: the value when the key holds an unsigned number, else nothing (G5.20).</summary>
    public static uint? OptionalUInt(JsonElement root, string key)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(key, out var v)) return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetUInt32(out var u) ? u : null;
    }
}

// fidelity: M1-031
/// <summary>
/// RobotIdleTimeoutComponent (CC1..CC9, B34), built with the Robot. Two deadlines in float seconds, both −1.0f:
/// faceOff (+0x10) and disconnect (+0x14). It is updated first in Robot::Update (CC6), on the engine clock (CC10).
/// </summary>
internal sealed class IdleTimeoutComponent
{
    private readonly EngineRobot _robot;
    public float FaceOffDeadline { get; private set; } = -1.0f;
    public float DisconnectDeadline { get; private set; } = -1.0f;

    public IdleTimeoutComponent(EngineRobot robot) => _robot = robot;

    /// <summary>
    /// StartIdleTimeout {f32 faceOffTime_s, f32 disconnectTime_s} (CC2, CC3): now = BaseStationTimer seconds;
    /// faceOff is taken only if the first full state has been handled (+0x34E) and it is ≥ 0; disconnect if ≥ 0;
    /// each candidate now + t is stored if its deadline is −1, else only if it is earlier. 0 is accepted.
    /// </summary>
    public void Start(float faceOffTimeS, float disconnectTimeS)
    {
        float now = _robot.Engine.Timer.SecondsF;
        if (_robot.FirstFullStateHandled && faceOffTimeS >= 0)
        {
            float c = now + faceOffTimeS;
            if (FaceOffDeadline == -1.0f || c < FaceOffDeadline) FaceOffDeadline = c;
        }
        if (disconnectTimeS >= 0)
        {
            float c = now + disconnectTimeS;
            if (DisconnectDeadline == -1.0f || c < DisconnectDeadline) DisconnectDeadline = c;
        }
    }

    /// <summary>CancelIdleTimeout (CC5): both deadlines −1.0f.</summary>
    public void Cancel() { FaceOffDeadline = -1.0f; DisconnectDeadline = -1.0f; }

    /// <summary>
    /// CC6/CC7: sleep first: if 0 &lt; faceOff ≤ now, it becomes 0.0 and the go-to-sleep sequence is queued; then if
    /// 0 &lt; disconnect ≤ now, it becomes 0.0 and MessageHandler::Disconnect is called (no reason is written, CC9).
    /// A 0.0 deadline does nothing and blocks re-arming until a Cancel.
    /// </summary>
    public void Update()
    {
        float now = _robot.Engine.Timer.SecondsF;
        if (FaceOffDeadline > 0 && FaceOffDeadline <= now)
        {
            FaceOffDeadline = 0.0f;
            _robot.Engine.QueueGoToSleep();
        }
        if (DisconnectDeadline > 0 && DisconnectDeadline <= now)
        {
            DisconnectDeadline = 0.0f;
            _robot.Engine.Handler.Disconnect();
        }
    }
}

// fidelity: M1-041, M1-031
/// <summary>
/// The parts of the engine's Robot that the connection opens (M1-041): time sync (+0x29), ready to stream
/// (+0x2A), the first full state (+0x34E), the SyncTime sent time (+0x520), the idle component (+0x51C), and
/// TracePrinter's crash-report requests.
/// </summary>
public sealed class EngineRobot
{
    internal CozmoEngine Engine { get; }
    internal IdleTimeoutComponent Idle { get; }
    private int? _crashReportIndex;
    private volatile bool _timeSynced, _readyToStream, _firstFullState, _streamGate;

    internal EngineRobot(CozmoEngine engine)
    {
        Engine = engine;
        Idle = new IdleTimeoutComponent(this);
        ConstructorDelocalize();
    }

    /// <summary>Robot+0x29: set by SyncTimeAck (CD19), cleared by Robot::SyncTime (CB23).</summary>
    public bool TimeSynced { get => _timeSynced; internal set => _timeSynced = value; }
    /// <summary>Robot+0x2A: ready to stream animations, set by the NV on-idle callback (CB22, CD20).</summary>
    public bool ReadyToStream { get => _readyToStream; internal set => _readyToStream = value; }
    /// <summary>Robot+0x34E: the first full robot state after time sync has been handled (CC4, CD23).</summary>
    public bool FirstFullStateHandled { get => _firstFullState; internal set => _firstFullState = value; }
    /// <summary>Robot+0x520: when the SyncTime was sent, engine seconds; 0 when none is outstanding (CD18, CD19).</summary>
    public double SyncTimeSentAt { get; internal set; }
    /// <summary>
    /// Whether the last Robot::Update ran AnimationStreamer::Update: past the first-full-state return, time synced
    /// and ready to stream (CD12). The stack's animation loop streams only while this is set.
    /// </summary>
    public bool AnimationStreamingOpen { get => _streamGate; internal set => _streamGate = value; }

    // fidelity: M3-012
    private volatile int _animBytesPlayed, _audioFramesPlayed;
    private volatile bool _animationStateHandled;
    /// <summary>Robot+0x238: numAnimBytesPlayed, written only by the AnimationState handler (C10); 0 from the constructor (C13).</summary>
    public int NumAnimBytesPlayed => _animBytesPlayed;
    /// <summary>Robot+0x240: numAudioFramesPlayed, written only by the AnimationState handler (C10); 0 from the constructor (C13).</summary>
    public int NumAudioFramesPlayed => _audioFramesPlayed;
    /// <summary>Robot+0x348: enabledAnimTracks from the last AnimationState (C10).</summary>
    public byte EnabledAnimTracks { get; private set; }
    /// <summary>Robot+0x248: the tag from the last AnimationState (C10).</summary>
    public byte AnimationStateTag { get; private set; }
    /// <summary>Whether an AnimationState has been handled. Not an engine field: the offline seam's switch (RobotAnimationSink).</summary>
    internal bool AnimationStateHandled => _animationStateHandled;

    // fidelity: M3-012
    /// <summary>The AnimationState handler (C10, 0x00537FD0..0x0053800C): gated by +0x29; writes +0x238, +0x240, +0x348, +0x248.</summary>
    internal void HandleAnimationState(AnimationState a)
    {
        if (!TimeSynced) return;
        _animBytesPlayed = a.NumAnimBytesPlayed;
        _audioFramesPlayed = a.NumAudioFramesPlayed;
        EnabledAnimTracks = a.EnabledAnimTracks;
        AnimationStateTag = a.Tag;
        _animationStateHandled = true;
    }

    // fidelity: M3-024
    /// <summary>
    /// The audio output source HandleFirmwareVersion sets (C1): <see cref="RobotAudioOutputSource.PlayOnRobot"/> when the
    /// firmware JSON's "sim" is null, else <see cref="RobotAudioOutputSource.PlayOnDevice"/>. Null until a firmware
    /// version has been handled. What source 1 plays through (C2's RobotAudioAnimationOnDevice) is not built: this
    /// stack's animation audio always streams to the robot.
    /// </summary>
    public RobotAudioOutputSource? AudioOutputSource { get; private set; }
    // fidelity: M10-010
    /// <summary>
    /// Robot+0x14, the physical flag (M10 A4): 0 from the constructor until FirmwareVersion arrives, then
    /// json["sim"].isNull(). While it is 0: CheckForUnexpectedMovement does nothing (B2), the on-back centre is the sim
    /// value (A5), and the tap filter does not queue (M4 CD10e).
    /// </summary>
    public bool IsPhysicalRobot { get; private set; }

    // fidelity: M3-024
    /// <summary>
    /// RobotToEngineImplMessaging::HandleFirmwareVersion (C1, 0x00536934..0x0053698E; "sim" at 0x00536A4C): if
    /// <c>json["sim"].isNull()</c>, SetPhysicalRobot(true) and SetOutputSource(2 = PlayOnRobot); otherwise source 1.
    /// MISSING: what the handler does when the JSON does not parse, or its root is not an object, is not in the rows;
    /// the source is left as it was and a warning is logged.
    /// MISSING (M4 interface): SetPhysicalRobot(true) also loads the persistent block pool; this stack still does that
    /// in the M1-042 app defaults (<see cref="CozmoRobot"/>), not here.
    /// </summary>
    internal void HandleFirmwareVersion(FirmwareVersion f)
    {
        if (!Json.TryParseFirst(f.Signature, out var doc))
        {
            Engine.Log("warning: MISSING: HandleFirmwareVersion: the firmware JSON did not parse; the audio output source is not set (M3-024)");
            return;
        }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                Engine.Log("warning: MISSING: HandleFirmwareVersion: the firmware JSON root is not an object; the audio output source is not set (M3-024)");
                return;
            }
            bool simIsNull = !root.TryGetProperty("sim", out var sim) || sim.ValueKind == System.Text.Json.JsonValueKind.Null;
            // fidelity: M10-010
            // A4: Robot::SetPhysicalRobot's only caller passes json["sim"].isNull() (0x00536980); it writes robot+0x14
            // (0x0051397A), which the Robot constructor zeroed (0x0050FC36).
            IsPhysicalRobot = simIsNull;
            AudioOutputSource = simIsNull ? RobotAudioOutputSource.PlayOnRobot : RobotAudioOutputSource.PlayOnDevice;
        }
    }

    // fidelity: M1-041, M4-020
    /// <summary>
    /// Robot::SyncTime (CB23, CD18): +0x29 = 0; RobotStateHistory::Clear; SendSyncTime; on success +0x520 = now.
    /// SendSyncTime, every send reliable through MessageHandler: SyncTime {u32 BaseStationTimer ms, 0xC1A00000};
    /// only if that was sent, InitController; only if that was sent, ImageRequest {Stream, QVGA 4}; then the log
    /// "Setting pose to (0,0,0)" and AbsoluteLocalizationUpdate {timestamp 0, frameId robot+0x2B0, originId the current
    /// pose origin, x 0, y 0, angle 0} (CD18; M4-020: frameId 0 and originId 1 from the constructor's Delocalize, SC4e,
    /// SC4g, SC4h). A failed send warns "FailedToSend" and stops. SendSyncTime returns the AbsoluteLocalizationUpdate
    /// send's result (0x005153AE), so +0x520 is set only when that send succeeds.
    /// The history clear reaches this stack's RobotStateHistory (VisionSystem) through
    /// <see cref="CozmoEngine.RobotStateHistoryClear"/>.
    /// </summary>
    internal void SyncTime()
    {
        TimeSynced = false;
        if (Engine.RobotStateHistoryClear is { } clear) Engine.RunIsolated(clear);
        // fidelity: M2-004
        // SyncTime {u32 GetCurrentTimeStamp() (0x00515266), f32 -20.0 (0xC1A00000, 0x0051526C/0x00515270)}
        if (!Send(new Protocol.SyncTime(Engine.Timer.TimeStampMs, Protocol.SyncTime.EngineConstant), "SyncTime")) return;
        if (!Send(new InitController(), "InitController")) return;
        if (!Send(new ImageRequest { Mode = ImageSendMode.Stream, ImageResolution = 4 }, "ImageRequest")) return;
        Engine.Log("info: Setting pose to (0,0,0)");
        if (!SendAbsLocalizationUpdate()) return;
        SyncTimeSentAt = Engine.Timer.Seconds;
    }

    // fidelity: M4-020
    /// <summary>
    /// SendAbsLocalizationUpdate as SendSyncTime calls it (CD18, 0x00512734..0x005127B6): timestamp 0, the pose frame id
    /// (robot+0x2B0), the pose parent's id (the current origin) and the identity pose (0, 0, 0).
    /// </summary>
    private bool SendAbsLocalizationUpdate() => Send(new AbsoluteLocalizationUpdate
    {
        Timestamp = 0, PoseFrameId = PoseFrameId, PoseOriginId = CurrentOriginId, PoseX = 0f, PoseY = 0f, PoseAngleRad = 0f,
    }, "AbsoluteLocalizationUpdate");

    // ------------------------------------------------------------ pose origins (M4-020)

    // fidelity: M4-020
    /// <summary>PoseOriginList (SC4g): the next id starts at 1 (0x0084792C..0x0084793C); UnknownOriginID 0 is never added.</summary>
    private uint _nextOriginId = 1;
    private readonly List<uint> _origins = new();

    /// <summary>The current pose origin's id: 1 after the constructor's Delocalize.</summary>
    public uint CurrentOriginId { get; private set; }
    /// <summary>
    /// Robot+0x2B0, the pose frame id: 0 from the constructor (SC4e, 0x0050FF02 and 0x0051032C). Its only other writer,
    /// AddVisionOnlyStateToHistory, has no counterpart here, so it stays 0.
    /// </summary>
    public uint PoseFrameId { get; private set; }
    /// <summary>The ids in the pose-origin list.</summary>
    public IReadOnlyList<uint> PoseOriginIds => _origins.ToArray();

    // fidelity: M4-020
    /// <summary>
    /// The Robot constructor's Delocalize (manager spot-check, 0x00510A24): ClearCliffRunningStats (the cache is already
    /// 400, so nothing is sent, SC4d), PoseOriginList::AddNewOrigin (0x00510A66) creates origin 1, SetNewPose
    /// (0x00510B4C); the constructor then puts the frame id back to 0 (0x0051032C). Not time synced, so no
    /// AbsoluteLocalizationUpdate (0x00510BF4).
    /// </summary>
    private void ConstructorDelocalize()
    {
        CurrentOriginId = _nextOriginId++;
        _origins.Add(CurrentOriginId);
        PoseFrameId = 0;
    }

    /// <summary>PoseOriginList::ContainsOriginID.</summary>
    internal bool ContainsOriginId(uint id) => _origins.Contains(id);

    /// <summary>
    /// The offline test seam only (<see cref="CozmoEngine.InitOfflineLink"/>): the seam feeds recorded or synthetic
    /// states that were never answered by an AbsoluteLocalizationUpdate exchange, so it takes whatever origin they report
    /// as known. Never set in production or by CozmoRobot.CreateForTest.
    /// </summary>
    internal bool OfflineSeamAcceptsAnyOrigin { get; set; }

    /// <summary>
    /// The last RobotState that passed the time-sync gate: what UpdateFullRobotState stores before the origin check
    /// (RS1 timestamp, RS7 lift angle, RS10 battery, RS11 status word; M2 App. B). Null before any.
    /// </summary>
    internal RobotState? StoredState { get; private set; }

    // fidelity: M4-022
    /// <summary>Robot+0x34D: the RobotStates counted without IS_BODY_ACC_MODE since the last SetBodyRadioMode.</summary>
    private int _bodyNotInAccModeCount;

    // fidelity: M4-022
    /// <summary>
    /// SC10 (0x00512AD0..0x00512B52; C4): a synced state without IS_BODY_ACC_MODE counts; at 16 the warning
    /// "BodyNotInAccessoryMode", SetBodyRadioMode {1, 0} reliable (0x00512B46), and the count restarts; a state with the
    /// bit does not reset it. UFRS runs it after SetOnCharger (0x00512AB4) and before MovementComponent::Update
    /// (0x00512B5C), so it is called from the device route at that point (<see cref="CozmoSensors"/>), for every state
    /// that passed the time-sync gate, origin-rejected or not (M4 C3).
    /// </summary>
    internal void CountBodyRadioMode(RobotState s)
    {
        if ((s.Status & (uint)RobotStatusFlag.IsBodyAccMode) != 0) return;
        _bodyNotInAccModeCount++;
        if (_bodyNotInAccModeCount < 16) return;
        Engine.Log("warning: Robot.UpdateFullRobotState.BodyNotInAccessoryMode");
        Engine.Handler.SendMessage(new SetBodyRadioMode { RadioMode = BodyRadioMode.BODY_ACCESSORY_OPERATING_MODE, WifiChannel = 0 });
        _bodyNotInAccModeCount = 0;
    }

    private bool Send(RobotMessage m, string what)
    {
        if (Engine.Handler.SendMessage(m)) return true;
        Engine.Log($"warning: Robot.SendSyncTime.FailedToSend {what}");
        return false;
    }

    // fidelity: M1-041
    /// <summary>HandleSyncTimeAck (CD19): +0x520 = 0 and +0x29 = 1; nothing is sent.</summary>
    internal void HandleSyncTimeAck() { SyncTimeSentAt = 0; TimeSynced = true; }

    // fidelity: M1-041, M4-020, M4-022
    /// <summary>
    /// UpdateFullRobotState's gates (CD23, CC4; SC4f; M4 correction C3). Before time sync the state is dropped (returns
    /// 0). Right after the +0x29 gate the first full state is marked, +0x34E = 1 (0x0051293C..0x00512948), ahead of the
    /// origin check. Then the part before the origin check: the stored fields (RS1..RS11; the robot clock the cube path
    /// reads, RS1; the head angle, lift angle, cliff data, IMU, treads, status bits, SetOnCharger, the body-radio-mode
    /// count (<see cref="CountBodyRadioMode"/>) and MovementComponent::Update, which the devices apply in that order, see
    /// CozmoRobot.RouteToDevices and CozmoSensors). This stack has no ramp (+0x316
    /// SetOnRamp has no writer here), so every state takes the off-ramp branch: ContainsOriginID(state+8)
    /// (0x00512C3E..0x00512C4A) must pass, otherwise the warning "Received RobotState with originID" and the rest (the
    /// pose, the history and the later steps) is skipped (0x00512EC4..0x00512F12). Returns whether the state passed the
    /// time-sync gate; <see cref="AcceptedState"/> says whether it also passed the origin check.
    /// </summary>
    internal bool UpdateFullRobotState(RobotState s)
    {
        if (!TimeSynced) return false;
        FirstFullStateHandled = true;
        StoredState = s;
        if (Engine.StateStored is { } stored) Engine.RunIsolated(() => stored(s));
        if (!OfflineSeamAcceptsAnyOrigin && !ContainsOriginId(s.PoseOriginId))
        {
            Engine.Log($"warning: Robot.UpdateFullRobotState: Received RobotState with originID {s.PoseOriginId}, which is not in the pose origin list (current origin {CurrentOriginId}); the pose and the later steps are skipped");
            AcceptedState = null;
            return true;
        }
        AcceptedState = s;
        return true;
    }

    // fidelity: M4-020
    /// <summary>
    /// The last state that also passed the origin check, so that the steps after it (the pose, the history, the cliff
    /// threshold schedule) run; null when the last synced state was origin-rejected (SC4f, C3).
    /// </summary>
    internal RobotState? AcceptedState { get; private set; }

    /// <summary>Whether this state passed the origin check (the steps after 0x00512C4A run for it).</summary>
    internal bool OriginAccepted(RobotState s) => ReferenceEquals(AcceptedState, s);

    // fidelity: M1-041
    /// <summary>
    /// NVStorage::AddOneShotOnIdleCallback (CD20; 0x00645C20..0x00645C32). The callback is appended and runs from
    /// <see cref="NvStorageComponent.ProcessOnIdle"/> only when the NV request deque is empty and nothing is in
    /// flight, so the AnimationStreamer (ready to stream) starts only after every queued NV request has drained.
    /// It is not run at the moment it is added, or the calibration read queued later in the same connection
    /// broadcast would be missed.
    /// </summary>
    internal void NvOnIdle(Action callback) => Engine.NvStorage?.OnIdle(callback);

    // fidelity: M1-041
    /// <summary>
    /// TracePrinter (CD22): SetAppRunID (16 bytes, all 0xFF with no DAS platform id), then RequestCrashReports{0}.
    /// </summary>
    internal void TracePrinterOnConnected()
    {
        if (!Engine.Handler.SendMessage(new SetAppRunID { Field0 = new[] { 0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu } })) return;
        _crashReportIndex = 0;
        Engine.Handler.SendMessage(new RequestCrashReports { Field0 = 0 });
    }

    /// <summary>
    /// CD22: each crashReport received triggers the request for the next index, indices 0..3 inclusive
    /// (0x0053BC42..0x0053BC48, 0x0053D3CA..0x0053D3DC, 0x0053CA88..0x0053CA9E, batch 3 verifier reading).
    /// </summary>
    internal void TracePrinterOnCrashReport()
    {
        if (_crashReportIndex is not { } i || i >= 3) return;
        _crashReportIndex = i + 1;
        Engine.Handler.SendMessage(new RequestCrashReports { Field0 = (uint)(i + 1) });
    }

    // fidelity: M1-041, M1-031
    /// <summary>
    /// Robot::Update (CD12): the idle component always runs; then the SyncTimeAck check (CD19: +0x520 &gt; 0 and
    /// now &gt; +0x520 + 5.0 s warns "SyncTimeAckNotReceived" and sets +0x520 = 0; never retried); then, if the first
    /// full state has not been handled, it returns. After that: ActionList (none in this stack), the
    /// AnimationStreamer only if synced and ready to stream, then NVStorage (its on-idle callbacks run here when
    /// the request deque is empty and nothing is in flight, CD20, which is what opens ready to stream). The later
    /// components (path, block filter, object connection, map, lights) run in their own layers in this stack.
    /// </summary>
    internal void Update()
    {
        Idle.Update();
        double now = Engine.Timer.Seconds;
        if (SyncTimeSentAt > 0 && now > SyncTimeSentAt + 5.0)
        {
            Engine.Log("warning: Robot.Update.SyncTimeAckNotReceived");
            SyncTimeSentAt = 0;
        }
        if (!FirstFullStateHandled) { AnimationStreamingOpen = false; return; }
        AnimationStreamingOpen = TimeSynced && ReadyToStream;
        // fidelity: M3-013
        // CD12: AnimationStreamer::Update runs here, only while synced and ready to stream; each call is one engine
        // Update of the streamer (C15).
        if (AnimationStreamingOpen && Engine.AnimationStreamerUpdate is { } streamer) Engine.RunIsolated(streamer);
        // fidelity: M1-041, M3-022
        // CD12: NVStorage::Update runs here, after the animation streamer: its on-idle callbacks run now if the
        // request deque is empty and nothing is in flight, which is what gates ready to stream (CD20).
        Engine.NvStorage?.ProcessOnIdle();
        // fidelity: M4-010, M4-017, M4-018, M4-023
        // CD2/CD12: after that, BlockTapFilter (0x00513EA4), BlockFilter, CheckDisconnected, ConnectToRequested
        // (0x0051422A..0x00514236), CubeLight::Update(true) (0x00514468) and BodyLight (0x00514470).
        if (Engine.RobotComponentsUpdate is { } components) Engine.RunIsolated(components);
    }
}

// fidelity: M3-024
/// <summary>The audio output sources of SetOutputSource (C1, C2): 0 none, 1 play on the device, 2 play on the robot.</summary>
public enum RobotAudioOutputSource : byte { None = 0, PlayOnDevice = 1, PlayOnRobot = 2 }

// fidelity: M1-028, M1-029, M1-030, M1-040, M1-015
/// <summary>
/// RobotInitialConnection (CB7..CB20, G5.1..G5.13, E1..E6). Built by RobotManager::AddRobot with the expected
/// firmware version and time copied from RobotManager+0x84/+0x88 at that moment (CB5, G5.14, G5.27).
/// </summary>
internal sealed class RobotInitialConnection
{
    /// <summary>Policy M1-040: the shipped header's version (G5.21), against which this stack only warns.</summary>
    public const uint ShippedFirmwareVersion = 2381;

    private readonly CozmoEngine _engine;
    private readonly List<Subscription> _handles = new();
    private readonly List<Subscription> _mfgId = new();
    private volatile bool _validated;

    public uint ExpectedVersion { get; }     // +0x1C
    public uint ExpectedTime { get; }        // +0x20
    public bool ResponseSent { get; private set; }            // +0x10
    public uint Serial { get; private set; }                  // +0x24
    public int BodyHwVersion { get; private set; } = -1;      // +0x28 (0xFFFFFFFF)
    public sbyte BodyColor { get; private set; } = -1;        // +0x2C (0xFF)
    /// <summary>+0x2D: while false, only the handshake messages pass either way (B30, CB25, CB26).</summary>
    public bool Validated => _validated;
    public bool RobotAvailableSeen { get; private set; }      // +0x2E
    /// <summary>The $session_id the Success path sets (CB18).</summary>
    public Guid? SessionId { get; private set; }

    private sealed class Subscription { public bool Active = true; public Action<RobotMessage> Fn = _ => { }; }

    // fidelity: M1-028
    /// <summary>
    /// The ctor (CB7): responseSent 0, serial 0, hw 0xFFFFFFFF, colour 0xFF, validated 0, robotAvailable 0, E_v and
    /// E_t stored. With an external interface it subscribes, in order, 0xD2 → HandleFactoryFirmware, 0xEE →
    /// HandleFirmwareVersion, 0xC9 → HandleRobotAvailable (CB8). No timer and no Update (CB7, CB31).
    /// </summary>
    public RobotInitialConnection(CozmoEngine engine, uint expectedVersion, uint expectedTime)
    {
        _engine = engine;
        ExpectedVersion = expectedVersion;
        ExpectedTime = expectedTime;
        _handles.Add(new Subscription { Fn = m => { if (m.Id == RobotMessageId.FactoryFirmwareVersion) HandleFactoryFirmware(); } });
        _handles.Add(new Subscription { Fn = m => { if (m is FirmwareVersion f) HandleFirmwareVersion(f); } });
        _handles.Add(new Subscription { Fn = m => { if (m.Id == RobotMessageId.RobotAvailable) HandleRobotAvailable(); } });
    }

    /// <summary>
    /// The RIC's subscribers for one message, in subscription order. E3: a link is called only while its function
    /// is set, and next is read after the call, so a handle released during the emit is never reached.
    /// </summary>
    public void OnMessage(RobotMessage m)
    {
        for (int i = 0; i < _handles.Count; i++) if (_handles[i].Active) _handles[i].Fn(m);
        if (m.Id == RobotMessageId.MfgId)
            for (int i = 0; i < _mfgId.Count; i++) if (_mfgId[i].Active) _mfgId[i].Fn(m);
    }

    // fidelity: M1-030
    /// <summary>
    /// ShouldFilterMessage through the RIC (CB25, CB26, B30): with +0x2D set everything passes; otherwise robot →
    /// engine passes only 0xC9 robotAvailable, 0xD2 factoryFirmwareVersion, 0xEE firmwareVersion and 0xEF otaAck,
    /// and engine → robot only 0xA9 shutdownRobot and 0xAF otaWrite.
    /// </summary>
    public bool ShouldFilter(byte tag, bool robotToEngine)
    {
        if (_validated) return false;
        return robotToEngine ? tag is not (0xC9 or 0xD2 or 0xEE or 0xEF) : tag is not (0xA9 or 0xAF);
    }

    // fidelity: M1-028
    /// <summary>HandleRobotAvailable (CB12): +0x2E = 1, no guard, payload ignored.</summary>
    private void HandleRobotAvailable() => RobotAvailableSeen = true;

    // fidelity: M1-028, M1-040
    /// <summary>HandleFactoryFirmware (CB13): guard on +0x10/+0x14; info; event "0"; OnNotified(3, 0). It does not check +0x2E.</summary>
    private void HandleFactoryFirmware()
    {
        if (ResponseSent) return;
        _engine.Log("info: robot.factory_firmware_version 0");
        _engine.Log("warning: robot firmware: factory firmware, version unknown (policy M1-040: not " + ShippedFirmwareVersion + ")");
        OnNotified(3, 0);
    }

    // fidelity: M1-028, M1-029, M1-040
    /// <summary>
    /// HandleFirmwareVersion (G5.1..G5.13, CB14):
    ///  - G5.1: return if the response was sent;
    ///  - G5.2: the bytes are parsed as JSON; a failure is an error and OnNotified(3, 0);
    ///  - G5.3/G5.4: "build" == "FACTORY" logs FactoryFirmware or UnknownVersion and OnNotified(3, 0);
    ///  - G5.5/G5.6: v = version, t = time; sim = (v | t) == 0 and "sim" is not null;
    ///  - G5.7: the version log (telemetry only);
    ///  - G5.9/CB14: no robotAvailable and not sim: error, +0x2D = 1, SendConnectionResponse(1, 0) directly, +0x2D = 0;
    ///  - G5.10..G5.12: sim → 0; robotDev (v == t) ≠ appDev (E_v == E_t) → 3; E_v == v → 0; E_v &gt; v → 3; else 4;
    ///  - G5.13: OnNotified(result, v).
    /// Policy M1-040 logs the version on every connection and warns when it is not 2381.
    /// MISSING: jsoncpp's asUInt of a value that is neither a number nor null is not in the rows (it throws in
    /// jsoncpp); such a value is read as 0 with a warning here.
    /// </summary>
    private void HandleFirmwareVersion(FirmwareVersion f)
    {
        if (ResponseSent) return;
        if (!Json.TryParseFirst(f.Signature, out var doc))
        {
            _engine.Log("error: RobotInitialConnection.HandleFirmwareVersion: firmware version JSON did not parse");
            _engine.Log("warning: robot firmware: version unknown (policy M1-040: not " + ShippedFirmwareVersion + ")");
            OnNotified(3, 0);
            return;
        }
        using (doc)
        {
            var root = doc.RootElement;
            bool isObject = root.ValueKind == JsonValueKind.Object;
            if (isObject && root.TryGetProperty("build", out var build) && build.ValueKind == JsonValueKind.String && build.GetString() == "FACTORY")
            {
                bool f0 = root.TryGetProperty("version", out var vs) && vs.ValueKind == JsonValueKind.String && (vs.GetString() ?? "").StartsWith('F');
                _engine.Log(f0 ? "info: FactoryFirmware" : "info: UnknownVersion");
                _engine.Log("info: robot.factory_firmware_version");
                _engine.Log("warning: robot firmware: factory build (policy M1-040: not " + ShippedFirmwareVersion + ")");
                OnNotified(3, 0);
                return;
            }
            uint v = AsUInt(root, "version"), t = AsUInt(root, "time");
            uint ev = ExpectedVersion, et = ExpectedTime;
            bool sim = (v | t) == 0 && isObject && root.TryGetProperty("sim", out var s) && s.ValueKind != JsonValueKind.Null;

            // G5.7 (telemetry and log only)
            _engine.Log($"info: robot firmware: {v}{(v == t ? " (dev)" : "")}{(sim ? " (SIM)" : "")} (app: {ev}{(ev == et ? " (dev)" : "")})");
            // fidelity: M1-040
            if (v != ShippedFirmwareVersion) _engine.Log($"warning: robot firmware {v} is not {ShippedFirmwareVersion} (policy M1-040: accepted)");

            if (!RobotAvailableSeen && !sim)
            {
                _engine.Log("error: RobotInitialConnection.HandleFirmwareVersion: no robotAvailable before firmwareVersion");
                _validated = true;
                SendConnectionResponse(RobotConnectionResult.ConnectionFailure, 0);
                _validated = false;
                return;
            }
            byte result;
            if (sim) result = 0;
            else if ((v == t) != (ev == et)) result = 3;
            else if (ev == v) result = 0;
            else result = ev > v ? (byte)3 : (byte)4;
            OnNotified(result, v);
        }
    }

    private uint AsUInt(JsonElement root, string key)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetUInt32(out var u)) return u;
        _engine.Log($"warning: MISSING: firmwareVersion \"{key}\" is {v.ValueKind}; read as 0 (M1-029)");
        return 0;
    }

    // fidelity: M1-028, M1-040
    /// <summary>
    /// OnNotified(result, fw) (CB15): 4 → reason 8, +0x2D = 0, response; 3 → reason 7, +0x2D = 0, response; any other
    /// value → +0x2D = 1, then the response if it is not 0, and CB16 if it is.
    /// Policy M1-040 overrides the outcome: a result of 3 or 4 (OutdatedFirmware, OutdatedApp) never refuses the
    /// robot; it goes on as a 0 with the same fw, so no reason 7/8 is written and the handshake proceeds as Success.
    /// </summary>
    private void OnNotified(byte result, uint fw)
    {
        if (result is 3 or 4)
        {
            _engine.Log($"warning: policy M1-040: the original's result {(RobotConnectionResult)result} is not applied; the robot is accepted");
            result = 0;
        }
        _validated = true;
        if (result != 0) { SendConnectionResponse((RobotConnectionResult)result, fw); return; }
        OnSuccess(fw);
    }

    // fidelity: M1-028, M1-030
    /// <summary>
    /// CB16/CD15: +0x2D = 1 first; subscribe the mfgId lambda {this, 0, fw}; then GetManufacturingInfo (0x25, empty),
    /// reliable, hot 0. No timer and no retry. A second accepted firmwareVersion adds a second lambda and a second
    /// request (E1).
    /// </summary>
    private void OnSuccess(uint fw)
    {
        _validated = true;
        _mfgId.Add(new Subscription { Fn = m => { if (m is ManufacturingID id) HandleMfgId(id, fw); } });
        _engine.Handler.SendMessage(new GetManufacturingInfo());
    }

    // fidelity: M1-028
    /// <summary>
    /// The mfgId lambda (CB18): +0x24 = serial (word 0); +0x28 = hw (word 1); +0x2C = colour only if the low byte of
    /// word 2 is in {0, 2, 3, 4}, else an error and 0xFF; $session_id = a new UUID; SendConnectionResponse(0, fw);
    /// then ReadLabAssignmentsFromRobot(serial) and ConnectRobotToNeedsManager(serial) (CD16).
    /// MISSING: the two NV reads (ReadLabAssignmentsFromRobot(serial) and ConnectRobotToNeedsManager(serial)) are
    /// still not made; the robot-level NV component now exists (<see cref="NvStorageComponent"/>).
    /// </summary>
    private void HandleMfgId(ManufacturingID id, uint fw)
    {
        Serial = id.SerialNumber;
        BodyHwVersion = unchecked((int)id.BodyHwVersion);
        byte c = (byte)(id.BodyColor & 0xFF);
        if (c is 0 or 2 or 3 or 4) BodyColor = (sbyte)c;
        else { _engine.Log($"error: RobotInitialConnection: bad body colour {c}"); BodyColor = -1; }
        SessionId = Guid.NewGuid();
        SendConnectionResponse(RobotConnectionResult.Success, fw);
    }

    // fidelity: M1-028
    /// <summary>
    /// SendConnectionResponse (CB19): +0x10 = 1; every subscription handle is released (so a later firmwareVersion is
    /// ignored and a second mfgId lambda is never reached, E4, E5); the response {result, fw, serial, hw, colour}
    /// goes to the game first and then to the engine subscribers synchronously (CB21, CD17).
    /// </summary>
    private void SendConnectionResponse(RobotConnectionResult result, uint fw)
    {
        ResponseSent = true;
        for (int i = _mfgId.Count - 1; i >= 0; i--) _mfgId[i].Active = false;
        for (int i = _handles.Count - 1; i >= 0; i--) _handles[i].Active = false;
        _engine.BroadcastConnectionResponse(new RobotConnectionResponse(result, fw, Serial, BodyHwVersion, BodyColor));
    }

    // fidelity: M1-015
    /// <summary>
    /// RIC::HandleDisconnect (CC25, CB32): 0 if the response was already sent; else info, OnNotified(result, 0),
    /// which sets +0x2D = 1 and sends the response {result, 0, serial, hw, colour}, and 1.
    /// </summary>
    public bool HandleDisconnect(RobotConnectionResult result)
    {
        if (ResponseSent) return false;
        _engine.Log($"info: RobotInitialConnection.HandleDisconnect: {result}");
        OnNotified((byte)result, 0);
        return true;
    }
}

// fidelity: M1-025, M1-029, M1-015
/// <summary>RobotManager: Robot 1, its RobotInitialConnection, and the expected firmware values (+0x84/+0x88).</summary>
internal sealed class RobotManager
{
    private readonly CozmoEngine _engine;
    private uint _expectedVersion, _expectedTime;     // +0x84, +0x88: 0 from the ctor (G5.15)
    private volatile EngineRobot? _robot;
    private volatile RobotInitialConnection? _ric;

    public RobotManager(CozmoEngine engine) => _engine = engine;

    public uint ExpectedVersion => Volatile.Read(ref _expectedVersion);
    public uint ExpectedTime => Volatile.Read(ref _expectedTime);
    public EngineRobot? Get(uint id) => id == CozmoEngine.RobotId ? _robot : null;
    public bool RobotExists(uint id) => Get(id) is not null;
    public RobotInitialConnection? Ric => _ric;

    // fidelity: M1-029
    /// <summary>ParseFirmwareHeader (G5.20): "version" → +0x84, "time" → +0x88 when present; a warning if either is 0.</summary>
    public void ParseFirmwareHeader(uint? version, uint? time)
    {
        if (version is { } v) Volatile.Write(ref _expectedVersion, v);
        if (time is { } t) Volatile.Write(ref _expectedTime, t);
        if (ExpectedVersion == 0 || ExpectedTime == 0) _engine.Log("warning: RobotManager.ParseFirmwareHeader: version or time is 0");
    }

    // fidelity: M1-025, M1-028, M1-029
    /// <summary>
    /// RM::AddRobot(id) (CB5): if the id exists, a warning only; otherwise a new Robot and a RIC emplaced with
    /// (id, MessageHandler, IExternalInterface, the values at +0x84/+0x88 now). No check of the header load (G5.27).
    /// <paramref name="withRic"/> false is the offline test seam (no RIC: nothing is filtered, CB27).
    /// </summary>
    public EngineRobot? AddRobot(uint id, bool withRic = true)
    {
        if (RobotExists(id)) { _engine.Log($"warning: RobotManager.AddRobot: robot {id} already exists"); return _robot; }
        var r = new EngineRobot(_engine);
        _robot = r;
        _ric = withRic ? new RobotInitialConnection(_engine, ExpectedVersion, ExpectedTime) : null;
        return r;
    }

    // fidelity: M1-030
    /// <summary>RM::ShouldFilterMessage (CB27, CC33): the RIC is looked up by robot id; with no RIC nothing is filtered.</summary>
    public bool ShouldFilterMessage(uint id, byte tag, bool robotToEngine)
        => id == CozmoEngine.RobotId && _ric is { } ric && ric.ShouldFilter(tag, robotToEngine);

    // fidelity: M1-015
    /// <summary>
    /// RemoveRobot(id, wasConnecting) (CB32, CB33, CC26): the result is 2 ConnectionRejected when the transport
    /// connect was never answered (RCD state 1) and 1 ConnectionFailure otherwise; if RIC::HandleDisconnect
    /// answered, OnRobotDisconnected, the RobotDisconnected broadcast and the $session_id clear are skipped,
    /// otherwise all three run. Either way the Robot is deleted and the map, id and RIC erased. No reconnect.
    /// The NeedsManager, PerfMetric and DAS notifications are outside this stack.
    /// </summary>
    public void RemoveRobot(uint id, bool wasConnecting)
    {
        if (!RobotExists(id)) return;
        var result = wasConnecting ? RobotConnectionResult.ConnectionRejected : RobotConnectionResult.ConnectionFailure;
        bool answered = _ric?.HandleDisconnect(result) ?? false;
        if (!answered) _engine.RaiseRobotDisconnected(new RobotDisconnectedMessage(0.0f));
        var r = _robot;
        _robot = null;
        _ric = null;
        if (r is not null) r.AnimationStreamingOpen = false;
        _engine.RobotRemoved?.Invoke();
    }
}

// fidelity: M1-024, M1-025, M1-041, M1-042, M1-034
/// <summary>
/// The engine for one robot: the game-message queue and the per-tick order (CD6..CD11), the connection manager,
/// MessageHandler, RobotManager and the firmware header load. Game messages are the calls on this class; they are
/// queued and dispatched synchronously at the start of the next tick (CD7).
/// </summary>
public sealed class CozmoEngine : IDisposable
{
    /// <summary>The only robot id the engine uses (B2: robot 1).</summary>
    public const uint RobotId = 1;

    private readonly IEngineTransport _t;
    private readonly ConcurrentQueue<Action> _gameMessages = new();
    private readonly object _tickLock = new();
    private readonly Func<long> _nowNs;
    private readonly long _runStartNs;
    private EngineTickRunner? _runner;
    private bool _inTick;
    private bool _autoTick;
    private volatile bool _disposed;

    internal RobotConnectionManager Rcm { get; }
    internal MessageHandler Handler { get; }
    internal RobotManager Robots { get; }
    internal CozmoEngineOptions Options { get; }

    /// <summary>The engine clock (BaseStationTimer, CD9).</summary>
    public BaseStationTimer Timer { get; } = new();

    // ---- the host's hooks (set by CozmoRobot)
    /// <summary>The stack's device handlers for one broadcast robot message, and whether a RobotState is handled (CD23).</summary>
    internal Action<RobotMessage, bool>? DeviceRoute;
    /// <summary>The public per-message event, raised last.</summary>
    internal Action<RobotMessage>? PublicRoute;
    /// <summary>Runs when Robot 1 is deleted (CB33), so the host can stop what belonged to it.</summary>
    internal Action? RobotRemoved;
    /// <summary>The game side's reaction to a Success response (policy M1-042), run as a game message.</summary>
    internal Action? AfterSuccessDefaults;
    /// <summary>Raised when a subscriber or handler threw (policy M1-034: isolated, and reported).</summary>
    internal Action<Exception>? Faulted;
    /// <summary>AnimationStreamer::Update, run by Robot::Update while synced and ready to stream (CD12, C15).</summary>
    internal Action? AnimationStreamerUpdate;
    /// <summary>The VisionComponent's RobotConnectionResponse subscriber, run in its place in the Success broadcast (CD21, 1h). Its argument is the body hardware version (mfgId word 1, the engine Robot's +0x24).</summary>
    internal Action<int>? VisionConnected;
    /// <summary>The robot-level NV storage owner (NVStorageComponent), set by CozmoRobot; one queue serves every read.</summary>
    internal NvStorageComponent? NvStorage { get; set; }
    /// <summary>RobotStateHistory::Clear, run by Robot::SyncTime (CD18).</summary>
    internal Action? RobotStateHistoryClear;
    /// <summary>UpdateFullRobotState's storage before the origin check (RS1: the robot clock), for a synced state.</summary>
    internal Action<RobotState>? StateStored;
    /// <summary>The M4 components Robot::Update runs after the animation streamer (CD2, CD12).</summary>
    internal Action? RobotComponentsUpdate;

    // ---- the game-facing messages
    /// <summary>RobotConnectionResponse to the game (CB19); raised on the engine thread before the engine's own subscribers (CD17).</summary>
    public event Action<RobotConnectionResponse>? ConnectionResponse;
    /// <summary>RobotDisconnected to the game (CB33).</summary>
    public event Action<RobotDisconnectedMessage>? RobotDisconnected;
    /// <summary>RobotErrorPassThrough to the game (CC16).</summary>
    public event Action<RobotErrorPassThrough>? RobotErrorPassThrough;
    /// <summary>The engine's log: each line starts with its level (debug, info, warning, error).</summary>
    public event Action<string>? LogLine;
    /// <summary>
    /// The idle faceOff deadline expired (CC6): the engine queues CreateGoToSleepAnimSequence here.
    /// MISSING: this stack has no ActionList or go-to-sleep sequence (CC8 is an M5 interface); the request is
    /// raised for the animation layer and logged, and nothing is sent.
    /// </summary>
    public event Action? GoToSleepRequested;

    internal CozmoEngine(IEngineTransport transport, CozmoEngineOptions? options, Func<long>? nowNs = null)
    {
        _t = transport;
        Options = options ?? new CozmoEngineOptions();
        _nowNs = nowNs ?? EngineTickRunner.HostNowNs;
        _runStartNs = _nowNs();
        Rcm = new RobotConnectionManager(this, transport);
        Handler = new MessageHandler(this, Rcm);
        Robots = new RobotManager(this);
        // fidelity: M1-024
        // R40/B20: the transport's receiver is RobotConnectionData.
        _t.Received += Rcm.Rcd.ReceiveData;
        Rcm.Rcd.Queued = Kick;
        Handler.Initialised = true;
        // fidelity: M1-029
        // G5.16..G5.18, G5.25: RobotManager::Init starts the header load on its own thread; nothing waits for it.
        if (Options.ResourcesPath is { } res) FirmwareHeader.LoadAsync(FirmwareHeader.PathUnder(res), Robots.ParseFirmwareHeader, Log);
        else Log("warning: MISSING: no resources path, so the firmware header is not loaded; expected version and time stay 0/0 (G5.31)");
    }

    // ------------------------------------------------------------------ running

    /// <summary>B24/B12: production. The transport is started (RCM::Init's StartClient) and the 60 ms engine thread runs.</summary>
    internal void StartProduction()
    {
        _t.Start();
        _runner = new EngineTickRunner(_nowNs, EngineTickRunner.HostSleepNs, TickAt, Log);
        _runner.Start("cozmo-engine");
    }

    /// <summary>
    /// The offline test seam: no engine thread. With <paramref name="autoTick"/>, every arrival and every game
    /// message runs engine ticks on the calling thread until nothing is pending, so a replayed datagram reaches the
    /// handlers before the call that fed it returns.
    /// </summary>
    internal void StartOffline(bool autoTick) => _autoTick = autoTick;

    /// <summary>Whether the 60 ms engine thread runs (production), rather than one of the offline seams.</summary>
    internal bool IsProduction => _runner is not null;

    /// <summary>
    /// The offline test seam's starting state (see <see cref="CozmoRobot.CreateOffline"/>): RCD state 1 on
    /// <paramref name="peer"/>, whose connection the offline transport already has; Robot 1 without a RIC, time
    /// synced, ready to stream, first full state handled and streaming open. Not a production path.
    /// </summary>
    internal void InitOfflineLink(IPEndPoint peer)
    {
        Rcm.Rcd.Address = peer;
        Rcm.Rcd.State = 1;
        var r = Robots.AddRobot(RobotId, withRic: false)!;
        r.TimeSynced = true;
        r.ReadyToStream = true;
        r.FirstFullStateHandled = true;
        r.AnimationStreamingOpen = true;
        r.OfflineSeamAcceptsAnyOrigin = true;
    }

    /// <summary>One engine tick now, on the calling thread (the offline seam; tests).</summary>
    internal void Tick() => TickAt(_nowNs() - _runStartNs);

    private void Kick()
    {
        if (!_autoTick || _disposed) return;
        lock (_tickLock)
        {
            if (_inTick) return;          // the running tick drains it (or the loop below runs another)
            do TickAt(_nowNs() - _runStartNs);
            while (!_disposed && (Rcm.Rcd.HasArrivals || !_gameMessages.IsEmpty));
        }
    }

    // fidelity: M1-024
    /// <summary>
    /// CozmoEngine::Update for one tick (CD6..CD11), state 3 (Running) only:
    ///  1. UiMessageHandler::Update: the queued game messages are dispatched synchronously (CD7);
    ///  2. BaseStationTimer::UpdateTime (CD8, CD9);
    ///  3. UpdateRobotConnection → MessageHandler::ProcessMessages: every robot-message handler runs here (CD10);
    ///  4. UpdateAllRobots → Robot::Update (CD11).
    /// NeedsManager::Update, UpdateLatencyInfo, the audio controller and the RobotState broadcast to the game are
    /// outside this stack. Engine states 0, 1, 2 and 4 (data loading, firmware update) are not modelled.
    /// </summary>
    private int TickAt(long elapsedNs)
    {
        lock (_tickLock)
        {
            _inTick = true;
            try
            {
                while (_gameMessages.TryDequeue(out var g)) Isolated(g);
                Timer.UpdateTime(elapsedNs);
                Handler.ProcessMessages();
                if (Robots.Get(RobotId) is { } r) r.Update();
            }
            finally { _inTick = false; }
        }
        return 0;
    }

    private void Post(Action gameMessage)
    {
        _gameMessages.Enqueue(gameMessage);
        Kick();
    }

    // ------------------------------------------------------------------ game messages

    // fidelity: M1-025, M1-001
    /// <summary>
    /// The ConnectToRobot game message (B1, B2, CB1..CB6): if robot 1 exists, "Robot already connected" and nothing
    /// else (CB36). Otherwise AddRobotConnection (port 5552 simulated, else 5551, CB3; RCM::Connect, CB4) and then
    /// AddRobot(1) at once, before any transport exchange; "Connected to robot!" is logged; nothing goes to the game.
    /// </summary>
    public void ConnectToRobot(IPAddress ip, bool isSimulated = false) => Post(() =>
    {
        if (Robots.RobotExists(RobotId)) { Log("info: Robot already connected"); return; }
        Rcm.Connect(ip, isSimulated);
        if (Robots.AddRobot(RobotId) is null) { Log("error: CozmoEngine.AddRobot failed"); return; }
        Log("info: Connected to robot!");
    });

    // fidelity: M1-025
    /// <summary>
    /// This stack's disconnect request: DisconnectCurrent on the engine thread (B33, CC29). The type 3 is queued on
    /// the transport and one OnDisconnected marker is handled in the same tick's UpdateRobotConnection (CC30).
    /// The original's game-message callers are ExitSdkMode (CC22) and the idle timeout (CC9); this API stands in for them.
    /// </summary>
    public void DisconnectCurrent() => Post(() => Rcm.DisconnectCurrent());

    // fidelity: M1-031
    /// <summary>StartIdleTimeout (game tag 83, CC1..CC3); handled by Robot 1's idle component if it exists.</summary>
    public void StartIdleTimeout(float faceOffTimeS, float disconnectTimeS)
        => Post(() => Robots.Get(RobotId)?.Idle.Start(faceOffTimeS, disconnectTimeS));

    // fidelity: M1-031
    /// <summary>CancelIdleTimeout (game tag 84, CC5).</summary>
    public void CancelIdleTimeout() => Post(() => Robots.Get(RobotId)?.Idle.Cancel());

    // fidelity: M1-025
    /// <summary>SetRobotDisconnectReason (game tag 88, B35, CC19): writes the reason byte (DAS only, CC20).</summary>
    public void SetRobotDisconnectReason(RobotDisconnectReason reason) => Post(() => Rcm.Reason = reason);

    // fidelity: M1-042
    /// <summary>
    /// SetRobotVolume (policy M1-042; CD27): the engine sends SetAudioVolume {u16 vol × 65535}; see <see cref="AudioVolumeLevel"/>.
    /// </summary>
    public void SetRobotVolume(float volume) => Post(() => SendRobotVolume(volume));

    internal void SendRobotVolume(float volume) => Handler.SendMessage(new SetAudioVolume { Level = AudioVolumeLevel(volume) });

    // fidelity: M1-042
    /// <summary>
    /// CD27's conversion (0x0059A22A..0x0059A244, batch 3 verifier reading): vmul.f32 by 65535.0f (literal at
    /// 0x0059A2B0), vcvt.u32.f32 — truncating toward zero, saturating to [0, 2^32 − 1], NaN → 0 — then strh, which
    /// stores the low 16 bits. So 1.0 → 0xFFFF and 1.5 → 98302 → 0x7FFE.
    /// </summary>
    internal static ushort AudioVolumeLevel(float volume)
    {
        float p = volume * 65535.0f;
        uint u;
        if (float.IsNaN(p) || p <= 0f) u = 0;
        else if (p >= 4294967296.0f) u = uint.MaxValue;
        else u = (uint)p;
        return (ushort)(u & 0xFFFF);
    }

    /// <summary>Runs <paramref name="gameMessage"/> as a game message at the start of the next tick (CD7).</summary>
    internal void PostGameMessage(Action gameMessage) => Post(gameMessage);

    // ------------------------------------------------------------------ robot → engine

    /// <summary>MessageHandler::SendMessage (B28, CB29): true when the message was handed to the transport.</summary>
    public bool SendMessage(RobotMessage m) => Handler.SendMessage(m);

    /// <summary>RCD state: 0 none, 1 connecting, 2 connected (B21, B23).</summary>
    public int ConnectionState => Rcm.Rcd.State;
    /// <summary>The robot address (RCD+0x30): set by ConnectToRobot, reset to unset (null) by RCD::Clear when a disconnect is handled (CC23).</summary>
    public IPEndPoint? RobotAddressInUse => Rcm.Rcd.Address;
    public RobotDisconnectReason DisconnectReason => Rcm.Reason;
    /// <summary>Robot 1, or null when there is none (B2, CB33).</summary>
    public EngineRobot? Robot => Robots.Get(RobotId);
    /// <summary>The firmware version and time from the header (RobotManager+0x84/+0x88; 0 until loaded, G5.15, G5.31).</summary>
    public uint ExpectedFirmwareVersion => Robots.ExpectedVersion;
    public uint ExpectedFirmwareTime => Robots.ExpectedTime;
    /// <summary>The RobotInitialConnection's +0x2D: whether the robot has been validated (null when there is no RIC).</summary>
    public bool? RobotValidated => Robots.Ric?.Validated;
    /// <summary>Whether the animation streamer ran in the last Robot::Update (CD12).</summary>
    public bool AnimationStreamingOpen => Robots.Get(RobotId)?.AnimationStreamingOpen ?? false;

    // fidelity: M1-024, M1-041, M1-028, M1-034
    /// <summary>
    /// MessageHandler's Broadcast (CC36) to the engine's subscribers of the tag, in the stack's order: the Robot's
    /// own handlers (SyncTimeAck CD19; the RobotState gate CD23; crashReport for TracePrinter CD22), then the stack's
    /// devices (for mfgId that is HandleRobotSetBodyID, which comes before the RIC's lambdas, E2), then the RIC's
    /// subscriptions, then the public event. Each subscriber is isolated (policy M1-034).
    /// </summary>
    internal void Broadcast(RobotMessage m)
    {
        var robot = Robots.Get(RobotId);
        bool stateHandled = true;
        switch (m)
        {
            case SyncTimeAck: if (robot is not null) Isolated(robot.HandleSyncTimeAck); break;
            case RobotState s: stateHandled = robot?.UpdateFullRobotState(s) ?? false; break;
            case CrashReport: if (robot is not null) Isolated(robot.TracePrinterOnCrashReport); break;
            // fidelity: M3-012
            case AnimationState a: if (robot is not null) Isolated(() => robot.HandleAnimationState(a)); break;
            // fidelity: M3-024
            case FirmwareVersion f: if (robot is not null) Isolated(() => robot.HandleFirmwareVersion(f)); break;
        }
        if (DeviceRoute is { } d) Isolated(() => d(m, stateHandled));
        if (Robots.Ric is { } ric) Isolated(() => ric.OnMessage(m));
        if (PublicRoute is { } p) Isolated(() => p(m));
    }

    // fidelity: M1-041, M1-042, M1-028
    /// <summary>
    /// The response's broadcast (CB21, CD17): the game first, then the engine subscribers synchronously in their
    /// subscription order. On Success: RobotEventHandler (Robot::SyncTime, then the NV on-idle callback that sets
    /// ready to stream, CB22, CD18, CD20), VisionComponent (CD21, 1h: the NV CameraCalib read queued, then
    /// SetCameraParams; <see cref="CameraSettings.OnRobotConnected"/>), TracePrinter (CD22). Policy M1-042 then queues
    /// the app's own reaction (stored volume, block pool) as a game message.
    /// </summary>
    internal void BroadcastConnectionResponse(RobotConnectionResponse resp)
    {
        FanOut(ConnectionResponse, resp);
        if (resp.Result != RobotConnectionResult.Success) return;
        if (Robots.Get(RobotId) is not { } robot) return;
        Isolated(() =>
        {
            robot.SyncTime();
            robot.NvOnIdle(() => robot.ReadyToStream = true);
        });
        // fidelity: M3-019, M3-022
        if (VisionConnected is { } vision) Isolated(() => vision(resp.BodyHWVersion));
        Isolated(robot.TracePrinterOnConnected);
        // fidelity: M1-042
        if (AfterSuccessDefaults is { } defaults) Post(defaults);
    }

    internal void RaiseRobotDisconnected(RobotDisconnectedMessage m) => FanOut(RobotDisconnected, m);
    internal void RaiseRobotErrorPassThrough(RobotErrorPassThrough m) => FanOut(RobotErrorPassThrough, m);

    internal void QueueGoToSleep()
    {
        Log("warning: MISSING: CreateGoToSleepAnimSequence (CC8, M5 interface) is not implemented; GoToSleepRequested raised");
        var h = GoToSleepRequested;
        if (h is null) return;
        foreach (var t in h.GetInvocationList()) Isolated((Action)t);
    }

    internal void Log(string line)
    {
        var h = LogLine;
        if (h is null) return;
        foreach (var t in h.GetInvocationList())
        {
            try { ((Action<string>)t)(line); } catch (Exception e) { ReportFault(e); }
        }
    }

    /// <summary>Runs a host hook isolated (policy M1-034).</summary>
    internal void RunIsolated(Action a) => Isolated(a);

    // fidelity: M1-034
    private void FanOut<T>(Action<T>? handler, T arg)
    {
        if (handler is null) return;
        foreach (var t in handler.GetInvocationList()) Isolated(() => ((Action<T>)t)(arg));
    }

    // fidelity: M1-034
    /// <summary>Policy M1-034: a handler that throws is reported and the rest of the dispatch goes on.</summary>
    private void Isolated(Action a)
    {
        try { a(); }
        catch (Exception e) { ReportFault(e); }
    }

    private void ReportFault(Exception e)
    {
        try { Faulted?.Invoke(e); } catch { }
    }

    // fidelity: M5-022
    /// <summary>
    /// Stops the 60 ms engine thread and waits for it (a production engine; the test seams have none), so no Robot::Update
    /// runs after it: ~Robot's AbortAll runs on the engine thread with no Update after it (M5 A37).
    /// </summary>
    internal void StopTick() => _runner?.Stop();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _runner?.Stop();
        _t.Received -= Rcm.Rcd.ReceiveData;
    }
}
