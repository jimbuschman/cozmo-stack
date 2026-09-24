using System.Diagnostics;
using System.Net;

namespace Cozmo.Transport;

// fidelity: M1-001
/// <summary>
/// Where the robot is. B1: Unity picks the IP, 172.31.1.1 for a physical robot and 127.0.0.1 for the simulator
/// (unity/scripts/csharp/ConnectionFlowController.cs:199, :204, :673; RobotEngineManager.cs:524-526), and sends
/// one ConnectToRobot(ipAddress, isSimulated). B3: the engine takes the IP from that message and picks the
/// remote port from isSimulated alone, 5552 when simulated, else 5551 (0x0069DE84 movw r2,#0x15b0; 0x0069DE92
/// ldrb r0,[r1,#0x10]; 0x0069DE98 movweq r2,#0x15af; 0x0069DE9E TransportAddress(char const*, int)). There is
/// no port override.
/// B4 (kP_ROBOT_ADVERTISING_PORT is only logged; Init refuses a value &gt;= 0x10000) has no counterpart here:
/// this stack has no advertising-port configuration input.
/// </summary>
public static class RobotAddress
{
    /// <summary>B1: the physical robot's IP.</summary>
    public static IPAddress Physical => IPAddress.Parse("172.31.1.1");
    /// <summary>B1: the simulator's IP.</summary>
    public static IPAddress Simulator => IPAddress.Loopback;
    /// <summary>B3: 0x0069DE98 movweq r2,#0x15af.</summary>
    public const int PhysicalPort = 5551;
    /// <summary>B3: 0x0069DE84 movw r2,#0x15b0.</summary>
    public const int SimulatedPort = 5552;

    /// <summary>B1: the IP Unity uses when the caller gives none.</summary>
    public static IPAddress DefaultFor(bool isSimulated) => isSimulated ? Simulator : Physical;

    /// <summary>B3: the remote port, from isSimulated alone.</summary>
    public static int RemotePort(bool isSimulated) => isSimulated ? SimulatedPort : PhysicalPort;
}

/// <summary>
/// Tunables of the Anki reliable transport as configured by the Cozmo engine.
/// Source of truth: libcozmoEngine.so RobotConnectionManager::Init / ConfigureReliableTransport (disassembly in
/// re-analysis/disassembly/dis_robotconn.txt). Values marked "engine" are set there; values marked "library default"
/// come from Anki's open-sourced reliableConnection.cpp/reliableTransport.cpp/udpTransport.cpp and are not
/// overridden by the engine.
/// </summary>
public sealed class TransportOptions
{
    /// <summary>engine: 33.3 ms (library default 250). Note the engine has SendSeparatePingMessages=false, so pings are only sent while the pending list is empty.</summary>
    public double TimeBetweenPingsMs { get; init; } = 33.3;
    /// <summary>engine: 33.3 ms (library default 50).</summary>
    public double TimeBetweenResendsMs { get; init; } = 33.3;
    /// <summary>engine: 32.3 ms (library default resend-1).</summary>
    public double MaxTimeSinceLastSendMs { get; init; } = 32.3;
    /// <summary>engine: 5000 ms. Connection is dropped when nothing was received for this long.</summary>
    public double ConnectionTimeoutMs { get; init; } = 5000.0;
    /// <summary>engine: 2.0 ms minimum spacing between outgoing packets (library default 0).</summary>
    public double PacketSeparationIntervalMs { get; init; } = 2.0;
    /// <summary>library default 1.0 ms (not set by engine).</summary>
    public double MinExpectedPacketAckTimeMs { get; init; } = 1.0;
    /// <summary>engine: 10.</summary>
    public int MaxPingRoundTripsToTrack { get; init; } = 10;
    /// <summary>engine: 1 (library default 3).</summary>
    public int MaxPacketsToReSendOnUpdate { get; init; } = 1;
    /// <summary>engine: 0 (library default 1) — no resend triggered by an incoming ack.</summary>
    public int MaxPacketsToReSendOnAck { get; init; } = 0;
    /// <summary>engine: 1.</summary>
    public int MaxPacketsToSendOnSendMessage { get; init; } = 1;
    /// <summary>library default 0.</summary>
    public int MaxBytesFreeInAFullPacket { get; init; } = 0;
    /// <summary>engine: false — the engine does NOT echo robot pings and only pings when idle (library default true).</summary>
    public bool SendSeparatePingMessages { get; init; } = false;
    /// <summary>engine: false — packets are batched, not sent immediately (library default true).</summary>
    public bool SendPacketsImmediately { get; init; } = false;
    /// <summary>engine: false — no standalone ACK (type 10) frames (library default true).</summary>
    public bool SendAckOnReceipt { get; init; } = false;
    /// <summary>engine: false — unreliable messages are queued with the others (library default true).</summary>
    public bool SendUnreliableMessagesImmediately { get; init; } = false;
    /// <summary>engine: true.</summary>
    public bool TrackAckLatency { get; init; } = true;

    /// <summary>
    /// The engine's own bound, read rather than guessed at: <c>RobotConnectionManager::Init</c> calls
    /// <c>UDPTransport::SetMaxNetMessageSize(0x58C)</c> = 1420 at 0x0062EFC6, and the reliable payload per
    /// frame is that less the 4-byte prefix and the 10-byte header, so 1406.
    ///
    /// This had been 1037, a figure from PyCozmo's robot-side measurement. Nothing in the package supports
    /// it and the shipped app plainly sends up to 1406, so the engine's number is the one to match; what
    /// PyCozmo was measuring is not established and is not authority over the app's own transport.
    /// </summary>
    // fidelity: M1-012
    public int MaxFramePayloadBytes { get; init; } = 1406;

    /// <summary>How often the reliable connection is ticked. The engine's ReliableTransport schedules Update every 2 ms.</summary>
    public double UpdateIntervalMs { get; init; } = 2.0;

    public static TransportOptions EngineDefaults => new();
}

/// <summary>NetTimeStamp: milliseconds since first use, from a steady clock (official uses std::chrono::steady_clock).</summary>
public interface INetClock { double NowMs { get; } }

// fidelity: M1-005
/// <summary>
/// <c>GetCurrentNetTimeStamp</c> 0x008355F8 (M1-005 evidence; G2.4): milliseconds as a double on a monotonic
/// clock, measured from one static, process-wide epoch taken the first time the clock is read
/// (0x0083561A/0x00835628). The elapsed nanoseconds are divided by 0x3E8 as an integer (0x00835644), so
/// the reading is truncated to whole microseconds, and only then converted and multiplied by 0.001
/// (0x0083564C, literal 0x3F50624DD2F1A9FC). The host's monotonic clock is <see cref="Stopwatch"/>.
/// </summary>
public static class NetTimeStamp
{
    /// <summary>The process-wide epoch, taken at the first read of <see cref="NowMs"/> and never again.</summary>
    private static readonly Lazy<long> Epoch = new(Stopwatch.GetTimestamp, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Milliseconds since the process-wide epoch, in whole microseconds.</summary>
    public static double NowMs
    {
        get
        {
            long epoch = Epoch.Value;
            return FromElapsedTicks(Stopwatch.GetTimestamp() - epoch, Stopwatch.Frequency);
        }
    }

    /// <summary>
    /// The conversion on its own: elapsed host ticks to nanoseconds, then ns / 1000 as an integer
    /// (0x00835644), then that many microseconds * 0.001 (0x0083564C).
    /// </summary>
    internal static double FromElapsedTicks(long ticks, long frequency)
    {
        long ns = (long)((Int128)ticks * 1_000_000_000 / frequency);
        long us = ns / 1000;
        return us * 0.001;
    }
}

/// <summary>
/// The production clock: reads <see cref="NetTimeStamp"/>, so every instance shares its process-wide epoch
/// (M1-005, G2.4). The type is kept for existing callers; it no longer has an epoch of its own.
/// </summary>
public sealed class StopwatchClock : INetClock
{
    public double NowMs => NetTimeStamp.NowMs;
}

/// <summary>Deterministic clock for tests.</summary>
public sealed class ManualClock : INetClock
{
    public double NowMs { get; set; }
    public void Advance(double ms) => NowMs += ms;
}
