using System.Diagnostics;

namespace Cozmo.Transport;

/// <summary>
/// Tunables of the Anki reliable transport as configured by the Cozmo engine.
/// Source of truth: libcozmoEngine.so RobotConnectionManager::Init / ConfigureReliableTransport (disassembly in
/// re-analysis/disassembly/dis_robotconn.txt). Values marked "engine" are set there; values marked "library default"
/// come from Anki's open-sourced reliableConnection.cpp/reliableTransport.cpp/udpTransport.cpp and are not
/// overridden by the engine.
/// </summary>
public sealed class TransportOptions
{
    /// <summary>Physical robot: 5551. Simulated (Webots) robot: 5552. MessageHandler::AddRobotConnection selects on ConnectToRobot.isSimulated.</summary>
    public int RobotPort { get; init; } = 5551;
    public const int SimulatedRobotPort = 5552;

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
    public int MaxFramePayloadBytes { get; init; } = 1406;

    /// <summary>How often the reliable connection is ticked. The engine's ReliableTransport schedules Update every 2 ms.</summary>
    public double UpdateIntervalMs { get; init; } = 2.0;

    public static TransportOptions EngineDefaults => new();
}

/// <summary>NetTimeStamp: milliseconds since first use, from a steady clock (official uses std::chrono::steady_clock).</summary>
public interface INetClock { double NowMs { get; } }

public sealed class StopwatchClock : INetClock
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    public double NowMs => _sw.Elapsed.TotalMilliseconds;
}

/// <summary>Deterministic clock for tests.</summary>
public sealed class ManualClock : INetClock
{
    public double NowMs { get; set; }
    public void Advance(double ms) => NowMs += ms;
}
