using System.Net;
using System.Text;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Batch 3, the M1 app layer (CozmoEngine): M1-024..M1-031, M1-015 (app part), M1-041, and the policies M1-040 and
/// M1-042. Every expected value comes from a row of re-analysis/inventory/M1-transport.md, named in each test.
/// The engine runs over a fake transport port, ticked by hand, so the per-tick order is observable.
/// </summary>
public class EngineAppLayerTests
{
    // ------------------------------------------------------------------ rig

    private sealed class FakePort : IEngineTransport
    {
        public readonly List<string> Calls = new();
        public readonly List<byte[]> Sent = new();
        public bool TimedOut { get; set; }
        public event Action<ReceiverEvent>? Received;
        public void Start() => Calls.Add("start");
        public void Connect(IPAddress ip, bool isSimulated) => Calls.Add($"connect {ip} {isSimulated}");
        public void Disconnect(IPEndPoint address) => Calls.Add($"disconnect {address}");
        // locked: the animation tick loop sends from its own thread (M1-025 tests below)
        public void SendData(byte[] clad) { lock (Sent) { Calls.Add($"send 0x{clad[0]:X2}"); Sent.Add(clad); } }
        public void Raise(ReceiverMarker m, IPEndPoint? a, byte[]? d = null) => Received?.Invoke(new ReceiverEvent(m, a, d));
        public List<RobotMessageId> SentIds => Sent.Select(b => (RobotMessageId)b[0]).ToList();
        public RobotMessage SentMessage(int i) => RobotMessage.Parse(Sent[i]);
    }

    private sealed class Rig : IDisposable
    {
        public long NowNs = 1_000_000_000;
        public readonly FakePort Port = new();
        public readonly CozmoRobot Robot;
        public CozmoEngine Engine => Robot.Engine;
        public readonly List<string> Log = new();
        public readonly List<RobotConnectionResponse> Responses = new();
        public readonly List<RobotDisconnectedMessage> Disconnects = new();
        public readonly List<RobotErrorPassThrough> PassThroughs = new();
        public readonly List<RobotMessage> Messages = new();
        public static readonly IPAddress RobotIp = IPAddress.Parse("172.31.1.1");
        public static readonly IPEndPoint RobotEp = new(RobotIp, 5551);

        public Rig(CozmoEngineOptions? options = null)
        {
            Robot = CozmoRobot.CreateForTest(Port, () => NowNs, options ?? new CozmoEngineOptions { BlockPoolPath = "" });
            Engine.LogLine += l => { lock (Log) Log.Add(l); };
            Engine.ConnectionResponse += Responses.Add;
            Engine.RobotDisconnected += Disconnects.Add;
            Engine.RobotErrorPassThrough += PassThroughs.Add;
            Robot.Message += Messages.Add;
        }

        public void Tick(double advanceMs = 60) { NowNs += (long)(advanceMs * 1_000_000); Engine.Tick(); }
        public void Data(RobotMessage m, IPEndPoint? from = null) => Port.Raise(ReceiverMarker.Data, from ?? RobotEp, m.ToBytes());
        public void Raw(byte[] b) => Port.Raise(ReceiverMarker.Data, RobotEp, b);
        public void Connected(IPEndPoint? from = null) => Port.Raise(ReceiverMarker.OnConnected, from ?? RobotEp);
        public void Disconnected(IPEndPoint? from = null) => Port.Raise(ReceiverMarker.OnDisconnected, from ?? RobotEp);

        /// <summary>ConnectToRobot handled, then the transport's OnConnected handled: RCD state 2.</summary>
        public void Connect()
        {
            Engine.ConnectToRobot(RobotIp);
            Tick();
            Connected();
            Tick();
        }

        public static FirmwareVersion Fw(string json) => new() { RobotId = 1, Signature = Encoding.UTF8.GetBytes(json) };
        public const string ShippedFw = "{\"version\": 2381, \"time\": 1546972025, \"build\": \"DEVELOPMENT\"}";

        /// <summary>Connected, robotAvailable and an accepted firmwareVersion handled: validated, GetManufacturingInfo sent.</summary>
        public void ToValidated(string fwJson = ShippedFw)
        {
            Connect();
            Data(new RobotAvailable { SerialNumberHead = 0x1234, HwVersion = 5 });
            Data(Fw(fwJson));
            Tick();
        }

        /// <summary>Then the mfgId: the Success response and its engine subscribers (M1-041).</summary>
        public void ToSuccess(string fwJson = ShippedFw)
        {
            ToValidated(fwJson);
            Data(new ManufacturingID { SerialNumber = 0xABCD, BodyHwVersion = 7, BodyColor = 2 });
            Tick();
        }

        public bool Logged(string part) { lock (Log) return Log.Any(l => l.Contains(part)); }
        public void Dispose() => Robot.Dispose();
    }

    private static string TempResources(uint version, uint time)
    {
        var dir = Path.Combine(Path.GetTempPath(), "cozmo-hdr-" + Guid.NewGuid().ToString("N"));
        var fw = Path.Combine(dir, "config", "engine", "firmware");
        Directory.CreateDirectory(fw);
        var bytes = new byte[0x900];
        Encoding.UTF8.GetBytes($"{{\"version\": {version}, \"time\": {time}, \"build\": \"DEVELOPMENT\"}}").CopyTo(bytes, 0);
        File.WriteAllBytes(Path.Combine(fw, "cozmo.safe"), bytes);
        return dir;
    }

    /// <summary><paramref name="expected"/> occurs in <paramref name="actual"/> in this order, other items allowed between.</summary>
    private static void AssertSubsequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual)
    {
        int j = 0;
        foreach (var a in actual) if (j < expected.Count && EqualityComparer<T>.Default.Equals(a, expected[j])) j++;
        Assert.True(j == expected.Count, $"expected the subsequence [{string.Join(", ", expected)}] in [{string.Join(", ", actual)}]");
    }

    private static Rig RigWithHeader(uint version, uint time)
    {
        var rig = new Rig(new CozmoEngineOptions { ResourcesPath = TempResources(version, time), BlockPoolPath = "" });
        Assert.True(SpinWait.SpinUntil(() => rig.Engine.ExpectedFirmwareVersion == version && rig.Engine.ExpectedFirmwareTime == time, 5000),
                    "the header loader never delivered the header");
        return rig;
    }

    // ================================================================== M1-024: the 60 ms tick

    private sealed class FakeRunner
    {
        public long T;
        public readonly List<long> Starts = new();
        public readonly List<long> Sleeps = new();
        public readonly List<string> Log = new();

        /// <summary>Runs the loop with each tick taking <paramref name="durations"/>[i] ns; tick <paramref name="ticks"/> returns 1.</summary>
        public void Run(int ticks, Func<int, long> durations)
        {
            var r = new EngineTickRunner(() => T, ns => { Sleeps.Add(ns); T += ns; }, el =>
            {
                int i = Starts.Count;
                Starts.Add(el);
                if (i == ticks) return 1;
                T += durations(i);
                return 0;
            }, Log.Add);
            r.Run();
        }
    }

    private const long Ms = 1_000_000;

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-024 B24/CD1/CD3: the period is 0x3938700 ns = 60 ms (0x0065B3D2/0x0065B3D8); the
    /// first tick runs at once and the first target is runStart + 60 ms (0x0065B3E0..0x0065B3EA); after each tick it
    /// sleeps to the target, and the next target is the old + 60 ms (0x0065B5E0..0x0065B5F0). CD2: the tick is given
    /// the ns since runStart, and a nonzero result stops the loop (0x0065B704..0x0065B712).
    /// </summary>
    [Fact]
    public void M1_024_CD1_CD2_CD3_FirstTickAtOnceThenEveryPeriodAndANonzeroResultStops()
    {
        Assert.Equal(60 * Ms, EngineTickRunner.PeriodNs);
        var f = new FakeRunner();
        f.Run(4, _ => 0);
        Assert.Equal(new[] { 0, 60 * Ms, 120 * Ms, 180 * Ms, 240 * Ms }, f.Starts);
        Assert.Equal(new[] { 60 * Ms, 60 * Ms, 60 * Ms, 60 * Ms }, f.Sleeps);   // none after the tick that returned 1
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-024 CD3: fixed rate (next target = old target + 60 ms), so ticks that overran run
    /// back to back until caught up (0x0065B432..0x0065B45E, 0x0065B5E0..0x0065B5F0). A 150 ms tick at 0 leaves the
    /// targets 60/120/180: no sleep at 150 (rem −90 ms, then −30 ms), a 30 ms sleep to 180, then 240.
    /// </summary>
    [Fact]
    public void M1_024_CD3_OverrunTicksRunBackToBack()
    {
        var f = new FakeRunner();
        f.Run(4, i => i == 0 ? 150 * Ms : 0);
        Assert.Equal(new[] { 0, 150 * Ms, 150 * Ms, 180 * Ms, 240 * Ms }, f.Starts);
        Assert.Equal(new[] { 30 * Ms, 60 * Ms }, f.Sleeps);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-024 CD3: it sleeps rem_us only if rem_ns ≥ 1000 (0x0065B576..0x0065B5D2): 999 ns
    /// left is no sleep; 1999 ns left is a 1 µs sleep (whole microseconds).
    /// </summary>
    [Fact]
    public void M1_024_CD3_ItSleepsWholeMicrosecondsAndOnlyFrom1us()
    {
        var a = new FakeRunner();
        a.Run(1, _ => 60 * Ms - 999);
        Assert.Empty(a.Sleeps);
        Assert.Equal(60 * Ms - 999, a.Starts[1]);

        var b = new FakeRunner();
        b.Run(1, _ => 60 * Ms - 1999);
        Assert.Equal(new[] { 1000L }, b.Sleeps);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-024 CD5: when rem ≤ −240 ms the target moves on by floor(−rem / 60 ms) × 60 ms, in
    /// addition to CD3's +60 ms (+= period at 0x0065B5EE, then += n × period at 0x0065B63C; batch 3 verifier reading of
    /// 0x0065B5DC..0x0065B63E). A 300 ms tick at 0 leaves rem = −240 ms: target 60 + 240 + 60 = 360, so the next tick
    /// runs at once at 300 ms and the one after sleeps 60 ms to 360 — never the four back-to-back ticks a plain
    /// fixed-rate loop would run.
    /// </summary>
    [Fact]
    public void M1_024_CD5_AFarBehindLoopSkipsWholePeriodsInsteadOfRunningExtraTicks()
    {
        var f = new FakeRunner();
        f.Run(5, i => i == 0 ? 300 * Ms : 0);
        Assert.Equal(new[] { 0, 300 * Ms, 360 * Ms, 420 * Ms, 480 * Ms, 540 * Ms }, f.Starts);
        Assert.Equal(new[] { 60 * Ms, 60 * Ms, 60 * Ms, 60 * Ms }, f.Sleeps);
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-024 CD4: "overtime" is a log only: a debug line below −10 ms, a warning below −200 ms (0x0065B478..0x0065B54E).</summary>
    [Fact]
    public void M1_024_CD4_OvertimeIsOnlyLogged()
    {
        var a = new FakeRunner();
        a.Run(1, _ => 71 * Ms);                    // rem = −11 ms
        Assert.Contains(a.Log, l => l.StartsWith("debug: overtime"));
        var b = new FakeRunner();
        b.Run(1, _ => 230 * Ms);                   // rem = −170 ms: debug, not warning
        Assert.DoesNotContain(b.Log, l => l.StartsWith("warning: overtime"));
        var c = new FakeRunner();
        c.Run(1, _ => 270 * Ms);                   // rem = −210 ms
        Assert.Contains(c.Log, l => l.StartsWith("warning: overtime"));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-024 B25/CD10: robot messages reach their handlers only in the tick's
    /// UpdateRobotConnection → ProcessMessages drain (0x0069D870, 0x0062F1FA, 0x0052F850..0x0052F856), never when
    /// the transport hands them over.
    /// </summary>
    [Fact]
    public void M1_024_B25_CD10_ArrivalsReachHandlersOnlyInTheTickDrain()
    {
        using var rig = new Rig();
        rig.Connect();
        rig.Data(new RobotAvailable { SerialNumberHead = 7, HwVersion = 1 });
        Assert.Null(rig.Robot.State.Available);
        Assert.Empty(rig.Messages);
        rig.Tick();
        Assert.Equal(7u, rig.Robot.State.Available!.SerialNumberHead);
        Assert.Single(rig.Messages);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-024 B20: RCD::ReceiveData drops the OnConnectRequest marker (0x0062E4BE) and queues
    /// the rest in arrival order; B25: ProcessArrivedMessages drains FIFO (tbb 0x0062F434); B26: data while the state
    /// is not 2 is dropped "Connection not yet valid" (0x0062F728). So in one tick: data (dropped, state 1),
    /// OnConnected (→ 2), data (delivered).
    /// </summary>
    [Fact]
    public void M1_024_B20_B25_B26_ArrivalsAreHandledInArrivalOrder()
    {
        using var rig = new Rig();
        rig.Engine.ConnectToRobot(Rig.RobotIp);
        rig.Tick();
        rig.Port.Raise(ReceiverMarker.OnConnectRequest, new IPEndPoint(IPAddress.Parse("10.0.0.2"), 5551));
        rig.Data(new RobotAvailable { SerialNumberHead = 1 });
        rig.Connected();
        rig.Data(new RobotAvailable { SerialNumberHead = 2 });
        rig.Tick();
        Assert.Equal(2, rig.Engine.ConnectionState);
        Assert.True(rig.Logged("Connection not yet valid, dropping message"));
        Assert.Equal(2u, Assert.Single(rig.Messages.OfType<RobotAvailable>()).SerialNumberHead);
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-024 B26: data whose source is not the robot address (RCD+0x30) is dropped "Expected messages from %s ... Dropping" (0x0062F744).</summary>
    [Fact]
    public void M1_024_B26_DataFromAnotherAddressIsDropped()
    {
        using var rig = new Rig();
        rig.Connect();
        rig.Data(new RobotAvailable { SerialNumberHead = 3 }, new IPEndPoint(IPAddress.Parse("10.0.0.9"), 5551));
        rig.Tick();
        Assert.Empty(rig.Messages);
        Assert.True(rig.Logged("Expected messages from"));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-024 CD6..CD8: game messages are dispatched in UiMessageHandler::Update before the
    /// state switch, and BaseStationTimer::UpdateTime runs after them (0x004ED4DE..0x004ED5EA), then
    /// UpdateRobotConnection, then UpdateAllRobots. So a StartIdleTimeout handled in a tick reads the previous tick's
    /// clock (CC3), and its deadline can expire in the same tick's Robot::Update (CC6).
    /// </summary>
    [Fact]
    public void M1_024_CD7_CD8_GameMessagesRunBeforeTheClockUpdateAndRobotUpdateAfterIt()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        double before = rig.Engine.Timer.Seconds;
        rig.Engine.StartIdleTimeout(-1f, 0.05f);          // deadline = previous tick's clock + 0.05 s
        int calls = rig.Port.Calls.Count;
        rig.Tick(60);                                      // clock moves 0.06 s after the game message
        Assert.Equal(0.0f, rig.Robot.Engine.Robot!.Idle.DisconnectDeadline);   // expired and cleared in this tick
        Assert.Contains($"disconnect {Rig.RobotEp}", rig.Port.Calls.Skip(calls));
        Assert.True(rig.Engine.Timer.Seconds > before + 0.05);
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-024 CD9: GetCurrentTimeStamp = (u32)(seconds × 1000), and every timestamp in a tick is the tick start (0x0084BC38..0x0084BCD4).</summary>
    [Fact]
    public void M1_024_CD9_TheEngineClockIsTheTickStart()
    {
        using var rig = new Rig();
        rig.Tick(1234.5678);
        Assert.Equal(1.2345678, rig.Engine.Timer.Seconds, 9);
        Assert.Equal(1234u, rig.Engine.Timer.TimeStampMs);
        rig.NowNs += 500 * Ms;                             // time passes within no tick
        Assert.Equal(1234u, rig.Engine.Timer.TimeStampMs);
    }

    // ================================================================== M1-025: connect, response, DisconnectCurrent

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-025 B2/CB2/CB4/CB5: ConnectToRobot runs AddRobotConnection — RCM::Connect: RCD Clear,
    /// the address, RT Disconnect(addr), RT Connect(addr), state 1 (0x0062F55E..0x0062F58A) — and then AddRobot(1) at
    /// once, before any transport exchange (0x004ED074, 0x004ED07C); the port is 5551 for a physical robot (CB3,
    /// 0x0069DE84..0x0069DEA6); nothing is sent to the robot and nothing goes to the game.
    /// </summary>
    [Fact]
    public void M1_025_B2_CB4_ConnectToRobotBuildsTheRobotBeforeAnyExchange()
    {
        using var rig = new Rig();
        rig.Engine.ConnectToRobot(Rig.RobotIp);
        Assert.Empty(rig.Port.Calls);                      // a game message: handled at the next tick (CD7)
        rig.Tick();
        Assert.Equal(new[] { $"disconnect {Rig.RobotEp}", $"connect {Rig.RobotIp} False" }, rig.Port.Calls);
        Assert.Equal(1, rig.Engine.ConnectionState);
        Assert.NotNull(rig.Engine.Robot);
        Assert.False(rig.Engine.RobotValidated);
        Assert.Empty(rig.Port.Sent);
        Assert.Empty(rig.Responses);
        Assert.True(rig.Logged("Connected to robot!"));
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-025 CB3/B3: a simulated robot is 127.0.0.1:5552 (0x0069DE84..0x0069DEA6).</summary>
    [Fact]
    public void M1_025_CB3_ASimulatedRobotUsesPort5552()
    {
        using var rig = new Rig();
        rig.Engine.ConnectToRobot(IPAddress.Loopback, isSimulated: true);
        rig.Tick();
        Assert.Equal(new[] { "disconnect 127.0.0.1:5552", "connect 127.0.0.1 True" }, rig.Port.Calls);
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-025 B2/CB36: while robot 1 exists, ConnectToRobot logs "Robot already connected" and does nothing (0x004ED026..0x004ED02C).</summary>
    [Fact]
    public void M1_025_B2_CB36_ASecondConnectToRobotIsIgnoredWhileTheRobotExists()
    {
        using var rig = new Rig();
        rig.Connect();
        int calls = rig.Port.Calls.Count;
        rig.Engine.ConnectToRobot(IPAddress.Parse("10.1.1.1"));
        rig.Tick();
        Assert.Equal(calls, rig.Port.Calls.Count);
        Assert.True(rig.Logged("Robot already connected"));
        Assert.Equal(Rig.RobotEp, rig.Engine.RobotAddressInUse);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-025 B23/CB10: OnConnected in state 1 → state 2 and reason 0 (0x0062F954..0x0062F95E);
    /// in any other state the error "Got connection response at unexpected time"; nothing is sent either way.
    /// </summary>
    [Fact]
    public void M1_025_B23_TheConnectionResponseMovesState1To2AndResetsTheReason()
    {
        using var rig = new Rig();
        rig.Engine.SetRobotDisconnectReason(RobotDisconnectReason.SleepPlacedOnCharger);
        rig.Engine.ConnectToRobot(Rig.RobotIp);
        rig.Tick();
        Assert.Equal(RobotDisconnectReason.SleepPlacedOnCharger, rig.Engine.DisconnectReason);   // CC19: Connect does not reset it
        rig.Connected();
        rig.Tick();
        Assert.Equal(2, rig.Engine.ConnectionState);
        Assert.Equal(RobotDisconnectReason.Unknown, rig.Engine.DisconnectReason);
        rig.Connected();
        rig.Tick();
        Assert.True(rig.Logged("Got connection response at unexpected time"));
        Assert.Equal(2, rig.Engine.ConnectionState);
        Assert.Empty(rig.Port.Sent);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-025 CC29/CC30: DisconnectCurrent calls RT Disconnect(RCD+0x30) and pushes one
    /// OnDisconnected marker; for a game message it is handled in the same tick's UpdateRobotConnection. After the
    /// response was sent, the disconnect gives exactly one RobotDisconnected{0.0} and removes the robot (CB33).
    /// </summary>
    [Fact]
    public void M1_025_CC29_CC30_DisconnectCurrentDisconnectsOnceAndRemovesTheRobot()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        int calls = rig.Port.Calls.Count;
        rig.Engine.DisconnectCurrent();
        rig.Tick();
        Assert.Equal(new[] { $"disconnect {Rig.RobotEp}" }, rig.Port.Calls.Skip(calls).Where(c => c.StartsWith("disconnect") || c.StartsWith("connect")));
        Assert.Equal(0.0f, Assert.Single(rig.Disconnects).TimeSinceLastMsgSec);
        Assert.Null(rig.Engine.Robot);
        Assert.Equal(0, rig.Engine.ConnectionState);
        Assert.Single(rig.Responses);                      // no second response
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-025 CC27/CB36: there is no automatic reconnect; once robot 1 is gone a new
    /// ConnectToRobot builds everything afresh, and the reason byte carries over (RCM::Connect does not reset it,
    /// CC19 0x0062F554..0x0062F58A).
    /// </summary>
    [Fact]
    public void M1_025_CC27_NoAutomaticReconnectAndALaterConnectToRobotBuildsAfresh()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Disconnected();
        rig.Tick();
        Assert.Null(rig.Engine.Robot);
        int calls = rig.Port.Calls.Count;
        for (int i = 0; i < 5; i++) rig.Tick();
        Assert.Equal(calls, rig.Port.Calls.Count);
        rig.Engine.SetRobotDisconnectReason(RobotDisconnectReason.SleepBackground);
        rig.Engine.ConnectToRobot(Rig.RobotIp);
        rig.Tick();
        Assert.NotNull(rig.Engine.Robot);
        Assert.False(rig.Engine.RobotValidated);
        Assert.Equal(1, rig.Engine.ConnectionState);
        Assert.Equal(RobotDisconnectReason.SleepBackground, rig.Engine.DisconnectReason);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-025 CB38: connection events are not filtered by address (tbb 0x0062F434;
    /// 0x0062F490/0x0062F498/0x0062F4A0): an OnDisconnected for another connection removes robot 1.
    /// </summary>
    [Fact]
    public void M1_025_CB38_AConnectionEventFromAnotherAddressIsHandledAsTheRobots()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Disconnected(new IPEndPoint(IPAddress.Parse("10.9.9.9"), 40000));
        rig.Tick();
        Assert.Null(rig.Engine.Robot);
        Assert.Single(rig.Disconnects);
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-025 CC18: RobotDisconnectReason 0..11 (0x00775BEC, table 0x01033970).</summary>
    [Fact]
    public void M1_025_CC18_DisconnectReasonValues()
    {
        var expected = new[] { "Unknown", "WifiTimeout", "SleepSettings", "SleepEraseCozmo", "SleepPlacedOnCharger", "SleepBackground",
                               "ExitSDKMode", "OutdatedFirmware", "OutdatedApp", "DebugForceDisconnect", "DebugDataPersistenceReset", "AppTerminated" };
        for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], ((RobotDisconnectReason)i).ToString());
    }

    // ================================================================== M1-015 (app layer)

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-015 CB32/CC24 [corrected C6]: a disconnect while the RCD state is 1 (the transport
    /// connect was never answered) gives 2 ConnectionRejected (0x0062FAEE..0x0062FAF4); RIC::HandleDisconnect answers
    /// with {result, 0, 0, -1, -1} and RemoveRobot then skips RobotDisconnected (CC25, CB33 0x0052F248).
    /// </summary>
    [Fact]
    public void M1_015_CC24_ANeverAnsweredConnectIsRejected()
    {
        using var rig = new Rig();
        rig.Engine.ConnectToRobot(Rig.RobotIp);
        rig.Tick();
        rig.Disconnected();
        rig.Tick();
        Assert.Equal(new RobotConnectionResponse(RobotConnectionResult.ConnectionRejected, 0, 0, -1, -1), Assert.Single(rig.Responses));
        Assert.Empty(rig.Disconnects);
        Assert.Null(rig.Engine.Robot);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-015 CC24/CB32: a drop during the handshake (state 2) is 1 ConnectionFailure; B31/CC23:
    /// the +0xA1 timed-out flag gives reason 1 WifiTimeout for the DAS event (0x0062FA34, 0x0062FA3C), and the reason
    /// is reset to 0 after it (0x0062FAE2).
    /// </summary>
    [Fact]
    public void M1_015_CC23_CC24_ADropDuringTheHandshakeIsAFailureAndATimeoutIsReason1()
    {
        using var rig = new Rig();
        rig.Connect();
        rig.Port.TimedOut = true;
        rig.Disconnected();
        rig.Tick();
        Assert.Equal(new RobotConnectionResponse(RobotConnectionResult.ConnectionFailure, 0, 0, -1, -1), Assert.Single(rig.Responses));
        Assert.True(rig.Logged("disconnect_reason WifiTimeout"));
        Assert.Equal(RobotDisconnectReason.Unknown, rig.Engine.DisconnectReason);
        Assert.Empty(rig.Disconnects);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-015 CC23: HandleDisconnectMessage runs even with state 0 and no robot; it only
    /// removes a robot that exists (0x0062FA2E..0x0062FAF8). CB33: once the response has been sent the game gets
    /// RobotDisconnected and no RobotConnectionResponse.
    /// </summary>
    [Fact]
    public void M1_015_CB33_AfterTheResponseTheGameGetsRobotDisconnected()
    {
        using var rig = new Rig();
        rig.Disconnected();
        rig.Tick();                                        // no robot, state 0: nothing to remove
        Assert.Empty(rig.Responses);
        Assert.Empty(rig.Disconnects);
        rig.ToSuccess();
        rig.Disconnected();
        rig.Tick();
        Assert.Single(rig.Responses);
        Assert.Single(rig.Disconnects);
    }

    // ================================================================== M1-026: the send path

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-026 B28/CB29: MessageHandler::SendMessage returns failure silently — no exception, no
    /// send — unless the connection is in state 2 (0x0069DCFE..0x0069DD2C, 0x0062F5A6).
    /// </summary>
    [Fact]
    public void M1_026_B28_CB29_ARefusedSendReturnsFailureSilently()
    {
        using var rig = new Rig();
        Assert.False(rig.Robot.SendMessage(new DriveWheels(1, 1, 0, 0)));
        rig.Engine.ConnectToRobot(Rig.RobotIp);
        rig.Tick();
        Assert.False(rig.Robot.SendMessage(new ShutdownRobot()));            // state 1
        Assert.Empty(rig.Port.Sent);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-026 CD13: no per-tick batching; each send reaches the transport at once
    /// (0x005134A4..0x005134B8; 0x00836B42..0x00836B66).
    /// </summary>
    [Fact]
    public void M1_026_CD13_ASendGoesToTheTransportAtOnce()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        int sent = rig.Port.Sent.Count;
        Assert.True(rig.Robot.SendMessage(new DriveWheels(1, 1, 0, 0)));
        Assert.Equal(sent + 1, rig.Port.Sent.Count);        // before any tick
        Assert.Equal(new DriveWheels(0, 0, 0, 0).Id, rig.Port.SentIds[^1]);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-026 B28/R38: whatever the caller asks, the app sends every robot message reliable
    /// (RT->SendData reliable = 1, 0x0062F5CE), which R38 sends as type 4 SingleReliableMessage (0x008370EC).
    /// </summary>
    [Fact]
    public void M1_026_B28_R38_EveryRobotMessageGoesOutReliable()
    {
        var clock = new ManualClock { NowMs = 1000 };
        using var robot = CozmoRobot.CreateOffline(clock: clock);
        robot.Transport.OfflineAcceptConnection();
        robot.Transport.OfflineOutbound.Clear();
        Assert.True(robot.SendMessage(new DriveWheels(1, 1, 0, 0), reliable: false, flush: true));
        clock.Advance(40);                                    // past sMaxTimeSinceLastSend 32.3 ms (the unflushed message waits for it)
        robot.Transport.OfflineTick();
        var subs = robot.Transport.OfflineOutbound.SelectMany(f => f.Messages).ToList();
        Assert.Contains(subs, m => m.Type == ReliableMessageType.SingleReliableMessage && m.Payload[0] == (byte)new DriveWheels(0, 0, 0, 0).Id);
        Assert.DoesNotContain(subs, m => m.Type == ReliableMessageType.SingleUnreliableMessage);
    }

    // ================================================================== M1-027: filters and robotError

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-027 CC15/CC30: a fatal robotError (0xD9 {code, fatal 1}) is broadcast, the rest of
    /// RCM+0x20 is cleared (ClearData), DisconnectCurrent runs and the loop exits (0x0069D938..0x0069DAEA); no
    /// RobotErrorPassThrough. The OnDisconnected marker is handled at the next tick.
    /// </summary>
    [Fact]
    public void M1_027_CC15_AFatalRobotErrorDisconnects()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Messages.Clear();
        int calls = rig.Port.Calls.Count;
        rig.Data(new RobotErrorReport { Field0 = 2, Field1 = 1 });
        rig.Data(new RobotAvailable { SerialNumberHead = 9 });
        rig.Tick();
        Assert.IsType<RobotErrorReport>(Assert.Single(rig.Messages));
        Assert.Empty(rig.PassThroughs);
        Assert.Contains($"disconnect {Rig.RobotEp}", rig.Port.Calls.Skip(calls));
        Assert.NotNull(rig.Engine.Robot);                  // the marker waits for the next tick
        rig.Tick();
        Assert.Null(rig.Engine.Robot);
        Assert.Single(rig.Disconnects);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-027 CC13/CC16: fatal is the byte only (0x0069D930); a non-fatal robotError sends
    /// RobotErrorPassThrough{code} to the game, is broadcast, and the loop continues (0x0069DA36..0x0069DAAE).
    /// </summary>
    [Fact]
    public void M1_027_CC16_ANonFatalRobotErrorIsPassedThroughAndTheLoopGoesOn()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Messages.Clear();
        rig.Data(new RobotErrorReport { Field0 = 5, Field1 = 0 });
        rig.Data(new RobotAvailable { SerialNumberHead = 9 });
        rig.Tick();
        Assert.Equal(5u, Assert.Single(rig.PassThroughs).Code);
        Assert.Equal(2, rig.Messages.Count);
        Assert.NotNull(rig.Engine.Robot);
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-027 CC14: before validation robotError is filtered before unpack: no event, no disconnect (0x0069D8EE; 0x0052DC6C..0x0052DC9A).</summary>
    [Fact]
    public void M1_027_CC14_ARobotErrorBeforeValidationIsFiltered()
    {
        using var rig = new Rig();
        rig.Connect();
        int calls = rig.Port.Calls.Count;
        rig.Data(new RobotErrorReport { Field0 = 2, Field1 = 1 });
        rig.Tick();
        Assert.Empty(rig.Messages);
        Assert.Equal(calls, rig.Port.Calls.Count);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-027 B27/CC32: size 0 is an error (0x0069D8E0); an unpack size mismatch is an error
    /// and a drop (0x0069D910). CC35: a tag outside 0xB0..0xF5 unpacks as 1 byte, so a 1-byte one is broadcast and a
    /// longer one is a size error (0x007B1ADC..0x007B1BCA).
    /// </summary>
    [Fact]
    public void M1_027_B27_CC35_SizeChecks()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Messages.Clear();
        rig.Raw(Array.Empty<byte>());
        var avail = new RobotAvailable { SerialNumberHead = 1 }.ToBytes();
        rig.Raw(avail.Concat(new byte[] { 0 }).ToArray());      // one byte too many
        rig.Raw(new byte[] { 0x10 });                            // outside the range, 1 byte
        rig.Raw(new byte[] { 0x10, 0x00 });                      // outside the range, 2 bytes
        rig.Tick();
        Assert.True(rig.Logged("message of size 0"));
        var raw = Assert.IsType<RawRobotMessage>(Assert.Single(rig.Messages));
        Assert.Equal((RobotMessageId)0x10, raw.Id);
        Assert.Empty(raw.Body);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-027 CC31/CC32: RCM::Update handles every arrival first — the data goes to RCM+0x20,
    /// the marker removes the robot — and only then does PopData run; with no robot the data is dropped silently
    /// (0x0069D8D2..0x0069D8D8).
    /// </summary>
    [Fact]
    public void M1_027_CC31_DataLeftAfterTheRobotIsRemovedIsDropped()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Messages.Clear();
        rig.Data(new RobotAvailable { SerialNumberHead = 9 });
        rig.Disconnected();
        rig.Tick();
        Assert.Empty(rig.Messages);
        Assert.Null(rig.Engine.Robot);
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-025 CC30: RCD::Clear drops what was queued after the OnDisconnected marker (0x0062F3BC..0x0062F4BC).</summary>
    [Fact]
    public void M1_025_CC30_ArrivalsQueuedAfterTheMarkerAreDropped()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Disconnected();
        rig.Connected();
        rig.Tick();
        Assert.Equal(0, rig.Engine.ConnectionState);
        Assert.False(rig.Logged("Got connection response at unexpected time"));
    }

    // ================================================================== M1-028 / M1-029 / M1-030 / M1-040: handshake

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-028 CB16/CD15: an accepted firmwareVersion validates the robot (+0x2D = 1) and sends
    /// GetManufacturingInfo (0x25), nothing else, with no timer (0x0052DDD0..0x0052DE7C). CB18/CB19: the mfgId gives
    /// RobotConnectionResponse{Success, fw, serial, hw, colour} (0x0052E304..0x0052E3A2, 0x0052DF2E..0x0052DF64).
    /// M1-029 G5.12: E_v == v is 0 (0x0052D8DA..0x0052D8E0).
    /// </summary>
    [Fact]
    public void M1_028_CB16_CB18_TheHandshakeToSuccess()
    {
        using var rig = RigWithHeader(2381, 1546972025);
        rig.ToValidated();
        Assert.True(rig.Engine.RobotValidated);
        Assert.Equal(new[] { RobotMessageId.GetMfgInfo }, rig.Port.SentIds);
        Assert.Empty(rig.Responses);
        rig.Data(new ManufacturingID { SerialNumber = 0xABCD, BodyHwVersion = 7, BodyColor = 0x0102 });   // low byte 2
        rig.Tick();
        Assert.Equal(new RobotConnectionResponse(RobotConnectionResult.Success, 2381, 0xABCD, 7, 2), Assert.Single(rig.Responses));
        Assert.DoesNotContain(rig.Log, l => l.Contains("policy M1-040"));
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-028 CB18: the colour is kept only if the low byte of word 2 is in {0,2,3,4}; otherwise an error and 0xFF (0x0052E304..0x0052E3B2).</summary>
    [Fact]
    public void M1_028_CB18_AnUnknownBodyColourIsReportedAs0xFF()
    {
        using var rig = new Rig();
        rig.ToValidated();
        rig.Data(new ManufacturingID { SerialNumber = 1, BodyHwVersion = 2, BodyColor = 1 });
        rig.Tick();
        Assert.Equal(-1, Assert.Single(rig.Responses).BodyColor);
        Assert.True(rig.Logged("bad body colour"));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-041 CD17/CD18/CD22 and policy M1-042: on Success the engine subscribers run in order —
    /// RobotEventHandler's SyncTime {BaseStationTimer ms, 0xC1A00000}, InitController, ImageRequest {Stream 1, QVGA 4}
    /// (0x0051521E..0x005153AE), then TracePrinter's SetAppRunID (all 0xFF) and RequestCrashReports{0}
    /// (0x0053D398..0x0053D430) — and the app's defaults (SetAudioVolume {u16 1.0 × 65535}, CD27) follow at the next
    /// tick as a game message. The AbsoluteLocalizationUpdate is MISSING (frameId/originId) and not sent.
    /// </summary>
    [Fact]
    public void M1_041_CD17_CD18_CD22_M1_042_PostSuccessSendsInOrder()
    {
        using var rig = new Rig(new CozmoEngineOptions { BlockPoolPath = "" });
        rig.ToValidated();
        uint tickMs = rig.Engine.Timer.TimeStampMs;
        rig.Data(new ManufacturingID { SerialNumber = 1, BodyHwVersion = 2, BodyColor = 3 });
        rig.Tick();
        uint successTickMs = rig.Engine.Timer.TimeStampMs;
        Assert.True(successTickMs > tickMs);
        // The row-backed order, as a subsequence: GetManufacturingInfo (CB16), then RobotEventHandler's chain (CD18),
        // then TracePrinter (CD22). Where the unsent AbsoluteLocalizationUpdate would sit is not asserted.
        var ids = rig.Port.SentIds;
        var order = new[] { RobotMessageId.GetMfgInfo, RobotMessageId.SyncTime, RobotMessageId.InitAnimController,
                            RobotMessageId.ImageRequest, RobotMessageId.AppRunID, RobotMessageId.RequestCrashReports };
        AssertSubsequence(order, ids);
        RobotMessage Sent(RobotMessageId id) => rig.Port.SentMessage(ids.IndexOf(id));
        var sync = Assert.IsType<SyncTime>(Sent(RobotMessageId.SyncTime));
        Assert.Equal(successTickMs, sync.Timestamp);
        Assert.Equal(0xC1A00000u, sync.Unknown);
        var img = Assert.IsType<ImageRequest>(Sent(RobotMessageId.ImageRequest));
        Assert.Equal(ImageSendMode.Stream, img.Mode);
        Assert.Equal(4, img.ImageResolution);
        Assert.All(Assert.IsType<SetAppRunID>(Sent(RobotMessageId.AppRunID)).Field0, w => Assert.Equal(0xFFFFFFFFu, w));
        Assert.Equal(0u, Assert.IsType<RequestCrashReports>(Sent(RobotMessageId.RequestCrashReports)).Field0);
        Assert.DoesNotContain(RobotMessageId.SetAudioVolume, ids);          // M1-042 runs as a game message, next tick
        Assert.False(rig.Robot.Cubes.Connections.AutoBlockPoolEnabled);
        rig.Tick();
        ids = rig.Port.SentIds;
        Assert.True(ids.IndexOf(RobotMessageId.SetAudioVolume) > ids.IndexOf(RobotMessageId.RequestCrashReports));
        Assert.Equal(0xFFFF, Assert.IsType<SetAudioVolume>(Sent(RobotMessageId.SetAudioVolume)).Level);
        Assert.True(rig.Robot.Cubes.Connections.AutoBlockPoolEnabled);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-028 CB14/G5.9: a firmwareVersion before robotAvailable (not sim) gives
    /// SendConnectionResponse(1, 0) directly, with +0x2D set only around it (0x0052D7B8..0x0052D850); CB34: the link
    /// and robot 1 stay; CB36: a retry ConnectToRobot is then ignored.
    /// </summary>
    [Fact]
    public void M1_028_CB14_CB34_NoRobotAvailableIsAFailureThatKeepsTheLink()
    {
        using var rig = new Rig();
        rig.Connect();
        rig.Data(Rig.Fw(Rig.ShippedFw));
        rig.Tick();
        Assert.Equal(new RobotConnectionResponse(RobotConnectionResult.ConnectionFailure, 0, 0, -1, -1), Assert.Single(rig.Responses));
        Assert.False(rig.Engine.RobotValidated);
        Assert.NotNull(rig.Engine.Robot);
        Assert.Equal(2, rig.Engine.ConnectionState);
        Assert.Empty(rig.Port.Sent);
        rig.Engine.ConnectToRobot(Rig.RobotIp);
        rig.Tick();
        Assert.True(rig.Logged("Robot already connected"));
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-029 G5.6/G5.10: v = t = 0 with "sim" set is result 0 even without robotAvailable (0x0052D7C6..0x0052D7C8).</summary>
    [Fact]
    public void M1_029_G5_10_ASimulatorIsAcceptedWithoutRobotAvailable()
    {
        using var rig = new Rig();
        rig.Connect();
        rig.Data(Rig.Fw("{\"sim\": true}"));
        rig.Tick();
        Assert.Equal(new[] { RobotMessageId.GetMfgInfo }, rig.Port.SentIds);
        Assert.DoesNotContain(rig.Log, l => l.Contains("policy M1-040: the original"));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-029 G5.11/G5.12 (0x0052D7DE..0x0052D9AC): robotDev = (v == t), appDev = (E_v == E_t);
    /// different → 3; else E_v == v → 0, E_v &gt; v → 3, E_v &lt; v → 4. POLICY M1-040: 3 and 4 do not refuse — the
    /// handshake proceeds as Success (GetManufacturingInfo sent, no reason 7/8), and a version that is not 2381 is
    /// warned about.
    /// </summary>
    [Theory]
    [InlineData(2457u, 200u, "OutdatedApp")]         // E_v 2381 < v
    [InlineData(2000u, 200u, "OutdatedFirmware")]    // E_v 2381 > v
    [InlineData(2381u, 2381u, "OutdatedFirmware")]   // robot dev (v == t), app not dev
    public void M1_029_G5_11_G5_12_M1_040_TheVersionOutcomeIsComputedAndThenNotApplied(uint v, uint t, string original)
    {
        using var rig = RigWithHeader(2381, 1546972025);
        rig.Connect();
        rig.Data(new RobotAvailable());
        rig.Data(Rig.Fw($"{{\"version\": {v}, \"time\": {t}}}"));
        rig.Tick();
        Assert.True(rig.Logged($"the original's result {original} is not applied"), string.Join("\n", rig.Log));
        Assert.Equal(new[] { RobotMessageId.GetMfgInfo }, rig.Port.SentIds);
        Assert.True(rig.Engine.RobotValidated);
        Assert.Equal(RobotDisconnectReason.Unknown, rig.Engine.DisconnectReason);
        Assert.Equal(v != 2381, rig.Logged($"robot firmware {v} is not 2381"));
        rig.Data(new ManufacturingID { SerialNumber = 5, BodyHwVersion = 1, BodyColor = 0 });
        rig.Tick();
        Assert.Equal(new RobotConnectionResponse(RobotConnectionResult.Success, v, 5, 1, 0), Assert.Single(rig.Responses));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-029 G5.15/G5.31/G5.30: with no header loaded E_v = E_t = 0, so appDev is true and a
    /// release robot (v ≠ t) is 3; policy M1-040 accepts it.
    /// </summary>
    [Fact]
    public void M1_029_G5_31_WithNoHeaderTheExpectedValuesAre0AndTheRobotIsStillAccepted()
    {
        using var rig = new Rig();
        Assert.Equal(0u, rig.Engine.ExpectedFirmwareVersion);
        Assert.Equal(0u, rig.Engine.ExpectedFirmwareTime);
        rig.ToValidated();
        Assert.True(rig.Logged("the original's result OutdatedFirmware is not applied"));
        Assert.Equal(new[] { RobotMessageId.GetMfgInfo }, rig.Port.SentIds);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-029 G5.2 (parse failure → OnNotified(3, 0), 0x0052D59C..0x0052D5A2), G5.3/G5.4
    /// ("build" FACTORY → OnNotified(3, 0), 0x0052D4C2..0x0052D506) and CB13 (factoryFirmwareVersion 0xD2 →
    /// OnNotified(3, 0), 0x0052D346..0x0052D3C2); under policy M1-040 each proceeds as Success with fw 0.
    /// </summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("{\"build\": \"FACTORY\", \"version\": \"F1\"}")]
    [InlineData(null)]
    public void M1_029_G5_2_G5_4_CB13_FactoryAndUnparsableFirmwareProceedUnderM1_040(string? json)
    {
        using var rig = new Rig();
        rig.Connect();
        if (json is null) rig.Data(new FWVersionInfo()); else rig.Data(Rig.Fw(json));
        rig.Tick();
        Assert.True(rig.Logged("the original's result OutdatedFirmware is not applied"));
        Assert.Equal(new[] { RobotMessageId.GetMfgInfo }, rig.Port.SentIds);
        rig.Data(new ManufacturingID { SerialNumber = 5, BodyHwVersion = 1, BodyColor = 0 });
        rig.Tick();
        Assert.Equal(0u, Assert.Single(rig.Responses).FwVersion);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-028 E1/E4/E5: a second firmwareVersion before mfgId sends a second
    /// GetManufacturingInfo, but the first mfgId yields exactly one response (SendConnectionResponse unlinks the
    /// second lambda during the emit), and a second mfgId or a later firmwareVersion changes nothing.
    /// </summary>
    [Fact]
    public void M1_028_E1_E4_E5_DuplicatesGiveExactlyOneResponse()
    {
        using var rig = new Rig();
        rig.ToValidated();
        rig.Data(Rig.Fw(Rig.ShippedFw));
        rig.Tick();
        Assert.Equal(2, rig.Port.SentIds.Count(i => i == RobotMessageId.GetMfgInfo));
        rig.Data(new ManufacturingID { SerialNumber = 1 });
        rig.Tick();
        rig.Data(new ManufacturingID { SerialNumber = 2 });
        rig.Data(Rig.Fw(Rig.ShippedFw));
        rig.Tick();
        Assert.Equal(1u, Assert.Single(rig.Responses).SerialNumber);
        Assert.Equal(2, rig.Port.SentIds.Count(i => i == RobotMessageId.GetMfgInfo));
        Assert.Equal(2u, rig.Robot.State.Manufacturing!.SerialNumber);     // E5: HandleRobotSetBodyID still runs
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-030 B30/CB25/CB26: before validation robot → engine passes only 0xC9, 0xD2, 0xEE and
    /// 0xEF (0x0052DC6C..0x0052DC9A), so mfgId and RobotState are dropped; engine → robot passes only 0xA9 and 0xAF
    /// (0x0052DC9C..0x0052DCB4), a filtered send returning failure silently (CB29).
    /// </summary>
    [Fact]
    public void M1_030_B30_BeforeValidationOnlyTheHandshakeMessagesPass()
    {
        using var rig = new Rig();
        rig.Connect();
        rig.Data(new ManufacturingID { SerialNumber = 1 });
        rig.Data(new RobotState { Timestamp = 1 });
        rig.Data(new Ack { ByteCount = 1 });
        rig.Data(new RobotAvailable());
        rig.Tick();
        Assert.Equal(new[] { RobotMessageId.OtaAck, RobotMessageId.RobotAvailable }, rig.Messages.Select(m => m.Id));
        Assert.False(rig.Robot.SendMessage(new DriveWheels(1, 1, 0, 0)));
        Assert.True(rig.Robot.SendMessage(new ShutdownRobot()));
        Assert.Equal(new[] { RobotMessageId.ShutdownRobot }, rig.Port.SentIds);
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-030 CB27: with no RobotInitialConnection nothing is filtered (0x0052FA90..0x0052FAD2) — the offline seam's state.</summary>
    [Fact]
    public void M1_030_CB27_WithNoRicNothingIsFiltered()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        Assert.Null(robot.Engine.RobotValidated);
        Assert.True(robot.SendMessage(new DriveWheels(1, 1, 0, 0)));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-029 G5.21: the shipped header re-analysis/obb/assets/cozmo_resources/config/engine/
    /// firmware/cozmo.safe has "version": 2381 and "time": 1546972025 (offsets 0-445); G5.19/G5.20: the JSON in the
    /// first 0x800 bytes up to the first NUL. Skipped when the gitignored OBB is not present.
    /// </summary>
    [Fact]
    public void M1_029_G5_21_TheShippedHeaderParsesTo2381()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        string? path = null;
        while (d is not null && path is null)
        {
            var p = Path.Combine(d.FullName, "re-analysis", "obb", "assets", "cozmo_resources", "config", "engine", "firmware", "cozmo.safe");
            if (File.Exists(p)) path = p;
            d = d.Parent;
        }
        if (path is null) return;
        var h = FirmwareHeader.Parse(File.ReadAllBytes(path));
        Assert.NotNull(h);
        Assert.Equal(2381u, h!.Value.Version);
        Assert.Equal(1546972025u, h.Value.Time);
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-029 G5.19/G5.31: a file shorter than 0x800 bytes gives no header (0x00677C44..0x00677C54); a missing file leaves 0/0.</summary>
    [Fact]
    public void M1_029_G5_19_G5_31_AShortOrMissingFileGivesNoHeader()
    {
        var shortFile = new byte[0x7FF];
        Encoding.UTF8.GetBytes("{\"version\": 1, \"time\": 2}").CopyTo(shortFile, 0);
        Assert.Null(FirmwareHeader.Parse(shortFile));
        using var rig = new Rig(new CozmoEngineOptions { ResourcesPath = Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid().ToString("N")) });
        Thread.Sleep(200);
        Assert.Equal(0u, rig.Engine.ExpectedFirmwareVersion);
        Assert.Equal(0u, rig.Engine.ExpectedFirmwareTime);
    }

    /// <summary>PRIMARY-SOURCE ORACLE. M1-029 G5.14/G5.27: AddRobot copies the expected values at that moment (0x0052EEF2/0x0052EEF8); a header loaded later does not change a RIC already made.</summary>
    [Fact]
    public void M1_029_G5_14_TheExpectedValuesAreCopiedWhenTheRobotIsAdded()
    {
        using var rig = RigWithHeader(2381, 1546972025);
        rig.Engine.ConnectToRobot(Rig.RobotIp);
        rig.Tick();
        rig.Engine.Robots.ParseFirmwareHeader(9999, 9999);   // a later value: not seen by this RIC
        Assert.Equal(2381u, rig.Engine.Robots.Ric!.ExpectedVersion);
        Assert.Equal(1546972025u, rig.Engine.Robots.Ric.ExpectedTime);
    }

    // ================================================================== M1-041: after Success

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-041 CD23/CC4: a RobotState before SyncTimeAck is dropped by the Robot
    /// (0x0051293C..0x0051294E) — the devices do not see it; after SyncTimeAck (CD19, 0x005366A4..0x005366AC) the next
    /// state is handled and sets the first-full-state flag.
    /// </summary>
    [Fact]
    public void M1_041_CD23_RobotStateIsDroppedUntilSyncTimeAck()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Data(new RobotState { Timestamp = 1 });
        rig.Tick();
        Assert.Equal(0, rig.Robot.State.StateCount);
        Assert.False(rig.Engine.Robot!.FirstFullStateHandled);
        rig.Data(new SyncTimeAck());
        rig.Data(new RobotState { Timestamp = 2 });
        rig.Tick();
        Assert.True(rig.Engine.Robot.TimeSynced);
        Assert.Equal(1, rig.Robot.State.StateCount);
        Assert.True(rig.Engine.Robot.FirstFullStateHandled);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-041 CD20/CB22: RobotEventHandler adds the ready-to-stream callback with
    /// AddOneShotOnIdleCallback, which appends it and then runs ProcessOnIdleCallbacks (0x00645C20..0x00645C32,
    /// 0x00645B08..0x00645B26): with no NV request pending it runs at once, so +0x2A is set inside the Success
    /// broadcast. CD12: Robot::Update returns before the AnimationStreamer until the first full state, and the
    /// streamer runs only when synced and ready (0x00513BF2..0x00514470), so streaming opens in the Robot::Update of
    /// the tick that handles SyncTimeAck and the first state.
    /// </summary>
    [Fact]
    public void M1_041_CD12_CD20_ReadyIsSetInTheSuccessBroadcastAndStreamingOpensWithTheFirstSyncedState()
    {
        using var rig = new Rig();
        rig.ToValidated();
        rig.Data(new ManufacturingID { SerialNumber = 1, BodyHwVersion = 2, BodyColor = 3 });
        bool? readyInBroadcast = null;
        rig.Robot.Message += m => { if (m is ManufacturingID) readyInBroadcast = rig.Engine.Robot?.ReadyToStream; };
        rig.Tick();
        Assert.True(readyInBroadcast);                              // set during the mfgId emit, before the public event ran
        Assert.False(rig.Robot.AnimationStreamingOpen);             // no first full state yet (CD12)
        rig.Data(new SyncTimeAck());
        rig.Data(new RobotState { Timestamp = 2 });
        rig.Tick();
        Assert.True(rig.Robot.AnimationStreamingOpen);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-041 CD19: SyncTime is never retried; once +0x520 &gt; 0 and now &gt; +0x520 + 5.0 s the
    /// warning "SyncTimeAckNotReceived" is given once and +0x520 = 0 (0x00513BF6..0x00513C5A).
    /// </summary>
    [Fact]
    public void M1_041_CD19_AMissingSyncTimeAckIsWarnedOnceAndNeverRetried()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        int syncs = rig.Port.SentIds.Count(i => i == RobotMessageId.SyncTime);
        Assert.True(rig.Engine.Robot!.SyncTimeSentAt > 0);
        rig.Tick(4900);
        Assert.False(rig.Logged("SyncTimeAckNotReceived"));
        rig.Tick(200);
        Assert.True(rig.Logged("SyncTimeAckNotReceived"));
        Assert.Equal(0, rig.Engine.Robot.SyncTimeSentAt);
        rig.Tick(10_000);
        Assert.Equal(1, rig.Log.Count(l => l.Contains("SyncTimeAckNotReceived")));
        Assert.Equal(syncs, rig.Port.SentIds.Count(i => i == RobotMessageId.SyncTime));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-041 CD22: RequestCrashReports{0} after Success, then each crashReport received asks
    /// for the next index, indices 0..3 inclusive (0x0053BC42..0x0053BC48, 0x0053D3CA..0x0053D3DC,
    /// 0x0053CA88..0x0053CA9E; batch 3 verifier reading).
    /// </summary>
    [Fact]
    public void M1_041_CD22_EachCrashReportAsksForTheNextUpToIndex3()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        for (int i = 0; i < 6; i++) { rig.Data(new CrashReport()); rig.Tick(); }
        var asked = rig.Port.Sent.Select(b => RobotMessage.Parse(b)).OfType<RequestCrashReports>().Select(r => r.Field0).ToList();
        Assert.Equal(new uint[] { 0, 1, 2, 3 }, asked);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-042 CD27 (0x0059A22A..0x0059A244, batch 3 verifier reading): vol × 65535.0f
    /// (literal 0x0059A2B0), vcvt.u32.f32 (truncate toward zero, saturate to [0, 2^32 − 1], NaN → 0), then strh (the
    /// low 16 bits).
    /// </summary>
    [Theory]
    [InlineData(1.0f, 0xFFFF)]
    [InlineData(0.5f, 32767)]        // 32767.5 truncated
    [InlineData(1.5f, 0x7FFE)]       // 98302 = 0x17FFE, low 16 bits
    [InlineData(2.0f, 0xFFFE)]       // 131070 = 0x1FFFE
    [InlineData(0.0f, 0)]
    [InlineData(-1.0f, 0)]           // negative saturates to 0
    [InlineData(float.NaN, 0)]
    [InlineData(1e6f, 0xFFFF)]       // above 2^32 − 1: saturates to 0xFFFFFFFF, low 16 bits 0xFFFF
    [InlineData(float.PositiveInfinity, 0xFFFF)]
    public void M1_042_CD27_TheVolumeConversionIsTheEngines(float volume, int expected)
        => Assert.Equal((ushort)expected, CozmoEngine.AudioVolumeLevel(volume));

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-025 CC23: RobotConnectionData::Clear resets the address to the default
    /// TransportAddress (0x0062E4CC..0x0062E50A; 0x0062E4E2..0x0062E4F2, batch 3 verifier reading), so a
    /// DisconnectCurrent after a handled disconnect addresses an unset address and sends nothing (CA14).
    /// </summary>
    [Fact]
    public void M1_025_CC23_ClearResetsTheAddressSoALaterDisconnectCurrentSendsNothing()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Disconnected();
        rig.Tick();
        Assert.Null(rig.Engine.RobotAddressInUse);
        int calls = rig.Port.Calls.Count;
        rig.Engine.DisconnectCurrent();
        rig.Tick();
        Assert.DoesNotContain(rig.Port.Calls.Skip(calls), c => c.StartsWith("disconnect"));
        Assert.Single(rig.Disconnects);                   // the marker finds no robot to remove
    }

    // ================================================================== M1-031: idle timeout

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-031 CC3: faceOff is taken only after the first full state (robot+0x34E); disconnect
    /// whenever ≥ 0; the earlier deadline wins (0x0052CFE4..0x0052D066). CC6/CC9: on expiry the deadline becomes 0.0
    /// and MessageHandler::Disconnect runs, with no reason written (0x0052CE6E..0x0052CE98); CC30: on the idle path
    /// the marker is handled at the next tick.
    /// </summary>
    [Fact]
    public void M1_031_CC3_CC6_TheDisconnectDeadline()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        var idle = rig.Engine.Robot!.Idle;
        float now = rig.Engine.Timer.SecondsF;
        rig.Engine.StartIdleTimeout(1f, 2f);
        rig.Tick(1);
        Assert.Equal(-1.0f, idle.FaceOffDeadline);          // no first full state yet
        Assert.Equal(now + 2f, idle.DisconnectDeadline, 3);
        rig.Engine.StartIdleTimeout(-1f, 5f);               // later: kept
        rig.Tick(1);
        Assert.Equal(now + 2f, idle.DisconnectDeadline, 3);
        rig.Engine.StartIdleTimeout(-1f, 0.5f);             // earlier: taken
        rig.Tick(1);
        Assert.True(idle.DisconnectDeadline < now + 1f);
        int calls = rig.Port.Calls.Count;
        rig.Tick(600);
        Assert.Equal(0.0f, idle.DisconnectDeadline);
        Assert.Contains($"disconnect {Rig.RobotEp}", rig.Port.Calls.Skip(calls));
        Assert.NotNull(rig.Engine.Robot);
        Assert.Equal(RobotDisconnectReason.Unknown, rig.Engine.DisconnectReason);
        rig.Tick();
        Assert.Null(rig.Engine.Robot);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-031 CC6/CC7: sleep fires before disconnect in the same update (0x0052CE3C..0x0052CE98);
    /// a 0.0 deadline does nothing and blocks re-arming until a Cancel (CC3 + CC6); Cancel sets −1 (CC5,
    /// 0x0052D0BE..0x0052D0C4). The go-to-sleep sequence itself is MISSING (CC8, M5 interface).
    /// </summary>
    [Fact]
    public void M1_031_CC6_SleepFiresBeforeDisconnectInOneUpdate()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Data(new SyncTimeAck());
        rig.Data(new RobotState { Timestamp = 2 });
        rig.Tick();
        var order = new List<string>();
        rig.Engine.GoToSleepRequested += () => order.Add(rig.Port.Calls.Any(c => c.StartsWith("disconnect")) ? "sleep after disconnect" : "sleep first");
        rig.Port.Calls.Clear();
        rig.Engine.StartIdleTimeout(0.01f, 0.01f);
        rig.Tick(1);
        rig.Tick(60);
        Assert.Equal(new[] { "sleep first" }, order);
        Assert.Contains($"disconnect {Rig.RobotEp}", rig.Port.Calls);
        Assert.True(rig.Logged("MISSING: CreateGoToSleepAnimSequence"));
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-031 CC7: an expired deadline is 0.0, which does nothing (Update needs &gt; 0) and blocks
    /// re-arming until a Cancel (CC3 + CC6); Cancel sets −1 (CC5, 0x0052D0BE..0x0052D0C4).
    /// </summary>
    [Fact]
    public void M1_031_CC7_AnExpiredDeadlineBlocksReArmingUntilCancel()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Data(new SyncTimeAck());
        rig.Data(new RobotState { Timestamp = 2 });
        rig.Tick();
        var idle = rig.Engine.Robot!.Idle;
        rig.Engine.StartIdleTimeout(0.01f, -1f);
        rig.Tick(1);
        rig.Tick(60);
        Assert.Equal(0.0f, idle.FaceOffDeadline);
        rig.Engine.StartIdleTimeout(1f, -1f);
        rig.Tick(1);
        Assert.Equal(0.0f, idle.FaceOffDeadline);            // blocked
        rig.Engine.CancelIdleTimeout();
        rig.Tick(1);
        Assert.Equal(-1.0f, idle.FaceOffDeadline);
        Assert.Equal(-1.0f, idle.DisconnectDeadline);
        rig.Engine.StartIdleTimeout(1f, -1f);
        rig.Tick(1);
        Assert.True(idle.FaceOffDeadline > 0);              // armed again after the Cancel
    }

    /// <summary>POLICY M1-042: no idle timeout is armed automatically after a Success response.</summary>
    [Fact]
    public void M1_042_NoIdleTimeoutIsArmedAutomatically()
    {
        using var rig = new Rig(new CozmoEngineOptions { BlockPoolPath = "" });
        rig.ToSuccess();
        rig.Tick();
        Assert.Equal(-1.0f, rig.Engine.Robot!.Idle.FaceOffDeadline);
        Assert.Equal(-1.0f, rig.Engine.Robot.Idle.DisconnectDeadline);
    }

    // ================================================================== M1-025, M1-015: RemoveRobot and the devices

    /// <summary>
    /// Every instance field of <paramref name="actual"/>, recursively, equals the same field of <paramref name="fresh"/>, a
    /// newly constructed object of the same type. Not compared: delegates (event subscribers and the hooks the owner
    /// installs), plain lock objects, the wiring back to the robot, the engine and the transport, random sources, and the
    /// field paths in <paramref name="kept"/>. Collections are compared element by element.
    /// </summary>
    private static void AssertAsConstructed(object? fresh, object? actual, string path, ISet<string> kept,
                                            HashSet<object>? seen = null)
    {
        seen ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (fresh is null || actual is null)
        {
            Assert.True(fresh is null && actual is null, $"{path}: as constructed {fresh ?? "null"}, after removal {actual ?? "null"}");
            return;
        }
        var type = actual.GetType();
        Assert.True(type == fresh.GetType(), $"{path}: type {fresh.GetType().Name} as constructed, {type.Name} after removal");
        if (type.IsPrimitive || type.IsEnum || actual is string or decimal or DateTime or TimeSpan)
        {
            Assert.True(Equals(fresh, actual), $"{path}: as constructed {fresh}, after removal {actual}");
            return;
        }
        if (actual is Delegate || type == typeof(object) || actual is CozmoRobot or CozmoEngine or ReliableTransport or Random
            or Thread or Task)
            return;
        if (!type.IsValueType && !seen.Add(actual)) return;
        if (actual is System.Collections.IEnumerable items)
        {
            var a = items.Cast<object?>().ToList();
            var f = ((System.Collections.IEnumerable)fresh).Cast<object?>().ToList();
            Assert.True(f.Count == a.Count, $"{path}: {f.Count} item(s) as constructed, {a.Count} after removal");
            for (int i = 0; i < a.Count; i++) AssertAsConstructed(f[i], a[i], $"{path}[{i}]", kept, seen);
            return;
        }
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
            foreach (var field in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                              System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
            {
                string name = $"{t.Name}.{field.Name}";
                if (kept.Contains(name)) continue;
                AssertAsConstructed(field.GetValue(fresh), field.GetValue(actual), $"{path}.{field.Name}", kept, seen);
            }
    }

    /// <summary>
    /// What the stack keeps across a removal on purpose: the animation generation is a token callers hold, and restarting
    /// it could let a token from before the removal stop an animation started after it; the audio and vision removal
    /// counts, which are what end a Play or discard a frame begun before a removal; the audio pacing clock and its frame
    /// count, which every Play restarts; and VisionSystem.Enabled, a caller's switch.
    /// </summary>
    private static readonly HashSet<string> KeptAcrossRemoval = new()
    {
        "AnimationScheduler._generation", "CozmoAudio._removals", "CozmoAudio._clock", "CozmoAudio._scheduled",
        "VisionSystem._removals", "VisionSystem.<Enabled>k__BackingField",
    };

    /// <summary>Puts state into every device, as a connected robot would.</summary>
    private static void UseEveryDevice(Rig rig, Cozmo.Robot.Vision.VisionSystem vision)
    {
        var robot = rig.Robot;
        rig.Data(new SyncTimeAck());
        rig.Data(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true, AutoStarted = true });
        rig.Data(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false });
        rig.Data(new AnimationState { Timestamp = 5, NumAudioFramesPlayed = 3, Tag = 2 });
        rig.Data(new ObjectAvailable { FactoryId = 0xAABBCCDD, ObjectType = ObjectType.Block_LIGHTCUBE1, Rssi = 40 });
        rig.Data(new ObjectAvailable { FactoryId = 0x11223344, ObjectType = ObjectType.Charger_Basic, Rssi = 60 });
        rig.Data(new RobotState { Timestamp = 10, PoseOriginId = 1 });          // the pool asks for the cube: SetPropSlot
        rig.Data(new ObjectConnectionState { ObjectID = 0, FactoryID = 0xAABBCCDD, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true });
        rig.Data(new RobotState { Timestamp = 43, PoseOriginId = 1, Status = (uint)RobotStatusFlag.IsPickedUp });
        rig.Data(new CliffEvent { Timestamp = 44, DetectedFlags = 1, DidStopForCliff = true });
        rig.Data(new ImageChunk { ImageId = 7, ImageResolution = 4, ImageEncoding = 8, ImageChunkCount = 3, ChunkId = 0, Data = new byte[] { 0, 1, 2 } });
        rig.Tick();                                        // the app defaults (policy M1-042) run first, then these
        rig.Tick();

        robot.Lights.SetBackpack(LedColor.Red);
        robot.Lights.SetHeadlight(true);
        robot.Face.ShowExpression(Cozmo.Robot.Animation.Expression.Happy);
        robot.Audio.SendSilence();
        _ = robot.Motion.SetHeadAngleAsync(0.1f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        lock (rig.Port.Sent) Assert.Contains(rig.Port.Sent, b => b[0] == (byte)new SetHeadAngle(0.1f).Id);
        robot.CubeAccel.AddListener(0, new CubeShakeListener(0.5f, 2.5f, 3.9f, _ => { }));
        Assert.True(robot.Animations.StreamLive(new Cozmo.Robot.Animation.HeadKeyframe(0, 100, 5, 0)));
        vision.World.AddMarkerlessObject(Cozmo.Robot.Vision.Pose3d.Identity, ObjectType.CollisionObstacle);
        vision.Faces.AddOrUpdateFace(new Cozmo.Robot.Vision.TrackedFace(new Cozmo.Robot.Vision.DetectedFace(0, new(0, 0, 10, 10)), 50),
                                     Cozmo.Robot.Vision.Pose3d.Identity, rotatingTooFast: false);
        vision.Pets.Update(new[] { new Cozmo.Robot.Vision.DetectedPet(1, Cozmo.Robot.Vision.PetType.Dog, new(0, 0, 5, 5)) }, 50, false);

        // the rig really did put state everywhere the comparison will look
        Assert.True(robot.State.StateCount > 0 && robot.State.HeadCalibrated && robot.State.TimeSynced);
        Assert.True(robot.Sensors.OffTreads.HeadCalibrated && robot.Sensors.CliffHistory.Count == 1);
        Assert.NotNull(robot.Cubes.Charger);
        Assert.Single(robot.Cubes.ConnectedCubes);
        Assert.True(robot.Cubes.Connections.AutoBlockPoolEnabled);
        Assert.NotEmpty(robot.Cubes.Connections.SlotRequestsSent);
        Assert.Contains(robot.Cubes.Connections.Slots, x => x.State == ActiveObjectSlotState.Connected);
        Assert.True(robot.Camera.ChunksReceived > 0);
        Assert.True(robot.Display.FramesSent > 0 && robot.Audio.FramesSent > 0);
        Assert.True(robot.Lights.HeadlightOn);
        Assert.NotEmpty(robot.CubeAccel.Streaming);
        Assert.True(robot.Animations.Scheduler.LiveStreamActive);
        Assert.True(vision.History.Count > 0 && vision.World.Objects.Count == 1 && vision.Faces.Count == 1 && vision.Pets.Pets.Count == 1);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-025, M1-015: CB33/CC26 — RemoveRobot deletes the Robot, and with it every Robot component
    /// (0x0052F248..0x0052F364); CC27 — a later ConnectToRobot builds everything afresh (0x004ED026..0x004ED07C). The
    /// devices here stand for those components and stay on the CozmoRobot, so after a handled disconnect each of them —
    /// State, Camera, Display, Audio, Motion, Lights, Sensors (with the off-treads classifier and the movement detector),
    /// Cubes (with the connection path and its advertisement table), CubeAccel, Animations (scheduler and sink), Face and
    /// the vision system built on the robot (state history, BlockWorld, faces, pets, and the calibration, which belongs
    /// to the VisionComponent the Robot deletes: Robot::Robot 0x0050FBF1, SetCameraCalibration 0x006516FC, ~VisionComponent
    /// 0x0065258E) — equals, field for field, the same device of a newly constructed robot. The objects are the same ones
    /// callers held before. VisionSystem.Enabled is a caller's switch and is kept.
    /// </summary>
    [Fact]
    public void M1_025_M1_015_CB33_CC26_CC27_RemoveRobotLeavesEveryDeviceAsConstructed()
    {
        using var rig = new Rig();
        var constructedCal = Cozmo.Robot.Vision.CameraCalibration.Nominal();
        using var vision = new Cozmo.Robot.Vision.VisionSystem(rig.Robot, constructedCal) { Enabled = false };
        var robot = rig.Robot;
        var held = new object[] { robot.State, robot.Camera, robot.Display, robot.Audio, robot.Motion, robot.Lights, robot.Sensors,
                                  robot.Cubes, robot.CubeAccel, robot.Animations, robot.Face, vision.History, vision.World };
        rig.ToSuccess();
        UseEveryDevice(rig, vision);
        vision.Calibration = Cozmo.Robot.Vision.CameraCalibration.Nominal(160, 120);   // as a read from the robot would
        vision.Enabled = true;

        rig.Disconnected();
        rig.Tick();
        Assert.Null(rig.Engine.Robot);
        Assert.True(SpinWait.SpinUntil(() => !robot.Animations.IsTicking, 3000), "the animation tick loop kept running");

        Assert.Same(constructedCal, vision.Calibration);
        Assert.True(vision.Enabled);

        using var fresh = new Rig();
        using var freshVision = new Cozmo.Robot.Vision.VisionSystem(fresh.Robot, constructedCal) { Enabled = false };
        Assert.Equal(held, new object[] { robot.State, robot.Camera, robot.Display, robot.Audio, robot.Motion, robot.Lights, robot.Sensors,
                                          robot.Cubes, robot.CubeAccel, robot.Animations, robot.Face, vision.History, vision.World });
        AssertAsConstructed(fresh.Robot.State, robot.State, "State", KeptAcrossRemoval);
        AssertAsConstructed(fresh.Robot.Camera, robot.Camera, "Camera", KeptAcrossRemoval);
        AssertAsConstructed(fresh.Robot.Display, robot.Display, "Display", KeptAcrossRemoval);
        AssertAsConstructed(fresh.Robot.Audio, robot.Audio, "Audio", KeptAcrossRemoval);
        AssertAsConstructed(fresh.Robot.Motion, robot.Motion, "Motion", KeptAcrossRemoval);
        AssertAsConstructed(fresh.Robot.Lights, robot.Lights, "Lights", KeptAcrossRemoval);
        AssertAsConstructed(fresh.Robot.Sensors, robot.Sensors, "Sensors", KeptAcrossRemoval);
        AssertAsConstructed(fresh.Robot.Cubes, robot.Cubes, "Cubes", KeptAcrossRemoval);
        AssertAsConstructed(fresh.Robot.CubeAccel, robot.CubeAccel, "CubeAccel", KeptAcrossRemoval);
        AssertAsConstructed(fresh.Robot.Animations, robot.Animations, "Animations", KeptAcrossRemoval);
        AssertAsConstructed(fresh.Robot.Face, robot.Face, "Face", KeptAcrossRemoval);
        AssertAsConstructed(freshVision, vision, "Vision", KeptAcrossRemoval);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-025 CC27 (0x004ED026..0x004ED07C; 0x0062F554..0x0062F58A) with CB33/CC26: after the
    /// removal a new ConnectToRobot connects a fresh robot. The handshake reaches Success again, the devices start counting
    /// from nothing (the first synced state is state 1), the block pool is loaded and enabled again by the app defaults,
    /// and the live animation is opened again with its StartOfAnimation (tag 0xFF), as a newly built AnimationStreamer
    /// opens it.
    /// </summary>
    [Fact]
    public void M1_025_M1_015_CC27_AfterRemovalASecondConnectStartsFromAFreshRobot()
    {
        using var rig = new Rig();
        using var vision = new Cozmo.Robot.Vision.VisionSystem(rig.Robot) { Enabled = false };
        rig.ToSuccess();
        UseEveryDevice(rig, vision);
        rig.Disconnected();
        rig.Tick();
        Assert.Null(rig.Engine.Robot);
        Assert.True(SpinWait.SpinUntil(() => !rig.Robot.Animations.IsTicking, 3000), "the animation tick loop kept running");
        Assert.Equal(0, rig.Robot.State.StateCount);
        Assert.False(rig.Robot.Cubes.Connections.AutoBlockPoolEnabled);

        rig.ToSuccess();
        Assert.Equal(2, rig.Responses.Count);
        Assert.Equal(RobotConnectionResult.Success, rig.Responses[1].Result);
        rig.Tick();                                        // the app defaults again (policy M1-042)
        Assert.True(rig.Robot.Cubes.Connections.AutoBlockPoolEnabled);
        rig.Data(new SyncTimeAck());
        rig.Data(new RobotState { Timestamp = 7, PoseOriginId = 1 });
        rig.Tick();
        Assert.True(rig.Robot.AnimationStreamingOpen);
        Assert.Equal(1, rig.Robot.State.StateCount);
        Assert.Equal(1, vision.History.Count);
        Assert.Empty(rig.Robot.Sensors.CliffHistory);

        int sent;
        lock (rig.Port.Sent) sent = rig.Port.Sent.Count;
        Assert.True(rig.Robot.Animations.StreamLive(new Cozmo.Robot.Animation.HeadKeyframe(0, 100, 5, 0)));
        List<RobotMessage> after;
        lock (rig.Port.Sent) after = rig.Port.Sent.Skip(sent).Select(b => RobotMessage.Parse(b)).ToList();
        var start = Assert.Single(after.OfType<StartOfAnimation>());
        Assert.Equal(Cozmo.Robot.Animation.AnimationScheduler.LiveAnimationTag, start.AnimId);
    }

    /// <summary>A face detector that blocks inside Detect until released, then reports one face.</summary>
    private sealed class BlockingFaceDetector : Cozmo.Robot.Vision.IFaceDetector
    {
        public readonly ManualResetEventSlim Entered = new(false);
        public readonly ManualResetEventSlim Release = new(false);
        public bool IsAvailable => true;
        public string Description => "test: blocks until released";
        public IReadOnlyList<Cozmo.Robot.Vision.DetectedFace> Detect(Cozmo.Robot.Vision.GrayImage image, uint timestamp)
        {
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(10));
            return new[] { new Cozmo.Robot.Vision.DetectedFace(0, new(10, 10, 40, 40)) };
        }
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-025, M1-015 CB33/CC26: the Robot's VisionComponent is deleted with it, and ~VisionComponent
    /// stops and joins its processing thread before the teardown (0x00652570..0x0065257C), so nothing of a frame in flight
    /// reaches the next robot. Here a frame is blocked inside face detection when the robot is removed: the removal waits
    /// for it, the frame is discarded, and afterwards no face, object, FrameProcessed or frame count is left or raised.
    /// </summary>
    [Fact]
    public void M1_025_M1_015_CB33_AFrameInFlightAtTheRemovalLeavesNothingBehind()
    {
        using var rig = new Rig();
        var detector = new BlockingFaceDetector();
        using var vision = new Cozmo.Robot.Vision.VisionSystem(rig.Robot, Cozmo.Robot.Vision.CameraCalibration.Nominal())
        {
            Enabled = false, FaceDetector = detector,
        };
        rig.ToSuccess();
        rig.Data(new SyncTimeAck());
        rig.Data(new RobotState { Timestamp = 10, PoseOriginId = 1 });
        rig.Tick();
        Assert.Equal(1, vision.History.Count);
        int processedEvents = 0, faceEvents = 0, objectEvents = 0;
        vision.FrameProcessed += _ => Interlocked.Increment(ref processedEvents);
        vision.Faces.FaceObserved += _ => Interlocked.Increment(ref faceEvents);
        vision.World.ObjectObserved += _ => Interlocked.Increment(ref objectEvents);

        var frame = Task.Run(() => vision.ProcessCapture(new Cozmo.Robot.Vision.GrayImage(320, 240), 1, 0));
        Assert.True(detector.Entered.Wait(TimeSpan.FromSeconds(10)), "the frame never reached face detection");
        var releaser = Task.Run(async () => { await Task.Delay(300); detector.Release.Set(); });
        rig.Disconnected();
        Assert.False(frame.IsCompleted);                   // the frame is in flight when the removal runs
        rig.Tick();                                        // RemoveRobot: the vision reset waits for the frame
        Assert.Null(rig.Engine.Robot);
        Assert.True(frame.Wait(TimeSpan.FromSeconds(5)));
        Assert.Null(frame.Result);                         // discarded
        Assert.True(releaser.Wait(TimeSpan.FromSeconds(5)));

        Assert.Equal(0, vision.Faces.Count);
        Assert.Empty(vision.LastFaces);
        Assert.Empty(vision.World.Objects);
        Assert.Equal(0, vision.History.Count);
        Assert.Equal(0, vision.FramesProcessed);
        Assert.Null(vision.LastResult);
        Assert.Equal(0, processedEvents);
        Assert.Equal(0, faceEvents);
        Assert.Equal(0, objectEvents);
    }

    /// <summary>
    /// PRIMARY-SOURCE ORACLE. M1-025, M1-015 CB33/CC26: the AudioComponent is destroyed with the Robot (~Robot 0x0052F2F6),
    /// so no further frame of what it was playing goes out. A Play running on another thread, with the robot's
    /// played-frames feedback (the production path) and with clock pacing, ends promptly when the robot is removed,
    /// well inside the 1 s stall escape, and sends no audio frame after the removal.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void M1_025_M1_015_CB33_APlayRunningAtTheRemovalEndsAndSendsNoMoreFrames(bool feedback)
    {
        using var rig = new Rig();
        var audio = rig.Robot.Audio;
        rig.ToSuccess();
        if (!feedback) audio.PlayedFrames = null;
        int framesAfterRemoval = 0;
        int removed = 0;
        audio.OnFrameSent += () => { if (Volatile.Read(ref removed) != 0) Interlocked.Increment(ref framesAfterRemoval); };

        var pcm = CozmoAudio.Tone(440, TimeSpan.FromSeconds(10));
        var play = new Thread(() => audio.Play(pcm)) { IsBackground = true };
        play.Start();
        Assert.True(SpinWait.SpinUntil(() => audio.FramesSent >= audio.TargetInFlight, 5000), "the Play never filled the buffer");

        rig.Disconnected();
        rig.Tick();                                        // RemoveRobot
        Volatile.Write(ref removed, 1);
        Assert.Null(rig.Engine.Robot);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(play.Join(TimeSpan.FromSeconds(3)), "Play did not end after the removal");
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(500), $"Play took {sw.ElapsedMilliseconds} ms to end");
        Assert.Equal(0, audio.FramesSent);                 // reset to 0 at the removal, and nothing sent since
        Assert.Equal(0, framesAfterRemoval);
    }

    // ================================================================== the offline seam

    /// <summary>
    /// The offline test seam (CozmoRobot.CreateOffline) still drains through the engine: a fed datagram reaches the
    /// devices through ProcessMessages before the feeding call returns, and CB33's robot removal ends a playing
    /// animation.
    /// </summary>
    [Fact]
    public void OfflineSeam_FedDatagramsGoThroughTheEngineDrainSynchronously()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        Assert.Equal(2, robot.Engine.ConnectionState);
        Assert.True(robot.AnimationStreamingOpen);
        robot.Disconnect();
        Assert.Null(robot.Engine.Robot);
        Assert.False(robot.SendMessage(new DriveWheels(1, 1, 0, 0)));
    }
}
