using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>
/// <c>m1-link-check</c>: the one bounded M1 hardware run (PROJECT_STATE.md, governing plan step 2). It drives
/// <see cref="ReliableTransport"/> directly through its public API, with transport-level frames only (no CLAD
/// message is ever sent), through a fixed sequence: Start, Connect, a 30 s idle hold, Disconnect, a 2 s wait,
/// a second Connect on the same transport, a 5 s hold, Disconnect, Stop, Dispose. It judges the checks it can
/// judge from what it measured, records robot-side behaviour (HARDWARE_ONLY record M1-033) without a verdict,
/// and writes one self-contained bundle. It never commits anything.
///
/// A pass here is hardware verification only: it does not raise the provenance of any record it cites.
/// </summary>
public static class LinkCheck
{
    public const string TestId = "M1-LINK";

    // The fixed timings of the run (operator's brief). They are not options, so every bundle ran the same script.
    internal const int StartWaitMs = 2000;
    internal const int ConnectWaitMs = 5000;
    internal const int Hold1Ms = 30000;
    internal const int DisconnectConfirmMs = 1000;
    internal const int AfterDisconnectMs = 2000;
    internal const int Hold2Ms = 5000;
    internal const int AfterStopMs = 1000;
    internal const int AfterDisposeMs = 500;
    /// <summary>
    /// A Disconnect is issued this long after an outbound frame, so that R25(a)'s 2.0 ms send-spacing gate cannot
    /// block the single type-3 send R38 allows, and well before the next idle ping (R32, 33.3 ms). Harness timing
    /// only: it changes nothing the transport does.
    /// </summary>
    internal const int DisconnectOffsetMs = 8;

    // R32 / R35 numbers the ping check is judged against.
    internal const double PingIntervalMs = 33.3;       // R32: now > lastSend + 33.3 and now >= lastPing + 33.3
    internal const double TickMs = 2.0;                // R35 / B14: the 2 ms update
    internal const double StampToleranceMs = 1.0;      // host timestamps are taken after sendto returns
    internal const double PingMedianMaxMs = PingIntervalMs + 2 * TickMs;   // 37.3
    internal const double PingCountFraction = 0.8;

    /// <summary>Every fidelity record the run's result names. Each must exist in re-analysis/fidelity_manifest.json.</summary>
    public static readonly string[] CitedRecords =
    {
        "M1-001", "M1-005", "M1-008", "M1-010", "M1-011", "M1-015", "M1-017", "M1-018", "M1-019", "M1-020",
        "M1-021", "M1-022", "M1-032", "M1-033", "M1-034",
    };

    public static int Run(string[] args)
    {
        IPAddress? ip = null; bool sim = false; string? outParent = null;
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--simulated": sim = true; break;
                case "--out":
                    if (i + 1 >= args.Length) return UsageError("--out needs a directory");
                    outParent = args[++i]; break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal)) return UsageError($"unknown option {args[i]}");
                    if (ip is not null) return UsageError($"unexpected argument {args[i]}");
                    if (!IPAddress.TryParse(args[i], out var parsed)) return UsageError($"not an IP address: {args[i]}");
                    ip = parsed; break;
            }
        }
        ip ??= RobotAddress.DefaultFor(sim);                      // M1-001 B1
        int port = RobotAddress.RemotePort(sim);                  // M1-001 B3

        string? repoRoot = FindRepoRoot();
        string? parent = outParent ?? (repoRoot is null ? null : Path.Combine(repoRoot, "re-analysis", "acceptance", "hardware"));
        if (parent is null) return UsageError("the repository root (a directory holding AGENTS.md and re-analysis/fidelity_manifest.json) was not found above the current directory or the tool; pass --out <dir>");

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var bundle = Path.GetFullPath(Path.Combine(parent, $"{stamp}-{TestId}"));
        Directory.CreateDirectory(bundle);
        Console.WriteLine($"m1-link-check: robot {ip}:{port}{(sim ? " (simulated)" : "")}; bundle {bundle}");
        Console.WriteLine($"  fixed sequence: start, connect (<= {ConnectWaitMs / 1000} s), hold {Hold1Ms / 1000} s, disconnect, wait {AfterDisconnectMs / 1000} s, connect again, hold {Hold2Ms / 1000} s, disconnect, stop, dispose. About 50 s.");
        var run = new LinkRun(ip, sim, port, bundle, repoRoot, args);
        return run.Execute();
    }

    private static int UsageError(string why)
    {
        Console.Error.WriteLine($"m1-link-check: {why}");
        Console.Error.WriteLine("usage: m1-link-check [robot-ip] [--simulated] [--out <dir>]");
        return 2;
    }

    /// <summary>The repository root: the first directory, above the working directory or the tool, holding AGENTS.md and re-analysis/fidelity_manifest.json.</summary>
    internal static string? FindRepoRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")) &&
                    File.Exists(Path.Combine(dir.FullName, "re-analysis", "fidelity_manifest.json")))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        return null;
    }

    // ------------------------------------------------------------------ analysis helpers (pure)

    internal static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0) return double.NaN;
        double rank = p * (sorted.Count - 1);
        int lo = (int)Math.Floor(rank), hi = (int)Math.Ceiling(rank);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
    }

    internal static JsonObject Stats(IEnumerable<double> values)
    {
        var s = values.OrderBy(v => v).ToList();
        return new JsonObject
        {
            ["count"] = s.Count,
            ["min"] = Num(s.Count == 0 ? double.NaN : s[0]),
            ["median"] = Num(Percentile(s, 0.5)),
            ["p95"] = Num(Percentile(s, 0.95)),
            ["max"] = Num(s.Count == 0 ? double.NaN : s[^1]),
            ["mean"] = Num(s.Count == 0 ? double.NaN : s.Average()),
        };
    }

    /// <summary>A JSON number rounded to 3 decimals, or null for NaN/infinity (which JSON cannot hold).</summary>
    internal static JsonNode? Num(double d) => double.IsFinite(d) ? JsonValue.Create(Math.Round(d, 3)) : null;
}

/// <summary>One run of the check. All times are milliseconds since the run began, from the host's UTC clock.</summary>
internal sealed class LinkRun
{
    private sealed record FrameRec(double T, DateTime Utc, bool Out, byte[] Raw, Frame? Frame, string? Error);
    private sealed record EventRec(double T, string Kind, JsonObject Data);

    private readonly IPAddress _ip; private readonly bool _sim; private readonly int _port;
    private readonly string _bundle; private readonly string? _repoRoot; private readonly string[] _argv;
    private readonly DateTime _t0Utc = DateTime.UtcNow;
    private readonly DateTimeOffset _t0Local = DateTimeOffset.Now;
    private readonly ConcurrentQueue<FrameRec> _frames = new();
    private readonly ConcurrentQueue<EventRec> _events = new();
    private readonly List<(string Name, double Start)> _phases = new();
    private readonly List<JsonObject> _checks = new();
    private readonly List<string> _exceptions = new();
    private readonly ManualResetEventSlim _connected = new();
    private readonly Dictionary<string, double> _marks = new();
    private readonly JsonObject _snapshots = new();
    private long _lastOutTicks; private int _outCount;
    private int _handlerFaultsBanked;
    private volatile bool _pollStop;
    private ReliableTransport? _t;
    private int _localPort = -1;
    private string _portMethod = "";
    private readonly JsonObject _portEvidence = new();
    private int _written;

    internal LinkRun(IPAddress ip, bool sim, int port, string bundle, string? repoRoot, string[] argv)
    {
        _ip = ip; _sim = sim; _port = port; _bundle = bundle; _repoRoot = repoRoot; _argv = argv;
    }

    private double Now() => (DateTime.UtcNow - _t0Utc).TotalMilliseconds;
    private double T(DateTime utc) => (utc - _t0Utc).TotalMilliseconds;
    private void Phase(string name) { lock (_phases) _phases.Add((name, Now())); Event("phase", new JsonObject { ["name"] = name }); }
    private void Mark(string name) => _marks[name] = Now();
    private void Event(string kind, JsonObject data) => _events.Enqueue(new EventRec(Now(), kind, data));

    private static bool WaitUntil(Func<bool> cond, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (!cond()) { if (sw.ElapsedMilliseconds >= timeoutMs) return cond(); Thread.Sleep(1); }
        return true;
    }

    private static void Hold(int ms)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms) Thread.Sleep(Math.Min(50, Math.Max(1, ms - (int)sw.ElapsedMilliseconds)));
    }

    private FrameRec[] Frames() => _frames.ToArray();

    // ------------------------------------------------------------------ the run

    internal int Execute()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            lock (_exceptions) _exceptions.Add("unhandled: " + e.ExceptionObject);
            Event("unhandled-exception", new JsonObject { ["exception"] = e.ExceptionObject?.ToString() });
            try { WriteBundle(crashed: true); } catch { }
        };

        bool connected1 = false;
        try
        {
            Phase("construct");
            var baseline = OwnUdpPorts("before-start");
            _t = new ReliableTransport();                       // production: async mode, engine defaults (M1-005, M1-020)
            Subscribe(_t);
            var poller = new Thread(PollState) { IsBackground = true, Name = "m1-link-check-poll" };
            poller.Start();

            // 1. Start: the socket opens (posted, R39) on the stored port 0, i.e. ephemeral (B12).
            Phase("start");
            Mark("startCall");
            _t.Start();
            List<IPEndPoint> opened = new();
            WaitUntil(() => (opened = NewPorts(baseline, "after-start")).Count > 0, LinkCheck.StartWaitMs);
            Mark("startSeen");
            JudgeSocket(opened, baseline);

            // 2. Connect; wait for the robot's ConnectionResponse (OnConnected).
            Phase("connect-1");
            connected1 = ConnectAndJudge("CONNECT_1", "connect1Call");

            if (connected1)
            {
                // 3. Idle hold: the engine's idle pings (R32) keep the link; every frame is recorded.
                Phase("hold-1");
                Mark("hold1Start");
                Hold(LinkCheck.Hold1Ms);
                Mark("hold1End");
                Snapshot("hold-1-end");
                JudgeHold();
                JudgePings();

                // 4. Disconnect: one type 3 goes out and the connection is deleted (R38); wait 2 s.
                Phase("disconnect-1");
                double d1 = TimedDisconnect("m1-link-check disconnect 1");
                _marks["disconnect1Call"] = d1;
                var d1Frame = JudgeDisconnect("DISCONNECT_1", d1);
                Phase("after-disconnect-1");
                double afterStart = Now();
                Hold(LinkCheck.AfterDisconnectMs);
                Mark("afterDisconnect1End");
                JudgeNoResend(d1, d1Frame, afterStart);

                // 5. Connect again on the same transport; same local port (no socket reopened); hold 5 s.
                Phase("connect-2");
                bool connected2 = ConnectAndJudge("RECONNECT", "connect2Call");
                JudgeSamePort(baseline);
                if (connected2)
                {
                    Phase("hold-2");
                    Mark("hold2Start");
                    Hold(LinkCheck.Hold2Ms);
                    Mark("hold2End");
                    Snapshot("hold-2-end");
                }
                // 6. Disconnect, then Stop.
                Phase("disconnect-2");
                double d2 = TimedDisconnect("m1-link-check disconnect 2");
                _marks["disconnect2Call"] = d2;
                if (connected2) JudgeDisconnect("DISCONNECT_2", d2);
                else Skipped("DISCONNECT_2", "Disconnect after the second connect sends type 3 and deletes the connection", "RECONNECT failed");
            }
            else
            {
                foreach (var (id, title) in new[]
                {
                    ("HOLD_NO_TIMEOUT", "No connection timeout during the 30 s idle hold"),
                    ("IDLE_PING_CADENCE", "Idle pings at the R32 cadence during the hold"),
                    ("DISCONNECT_1", "Disconnect sends type 3 and deletes the connection"),
                    ("DISCONNECT_1_NO_RESEND", "Nothing sent after the disconnect (type 3 never resent)"),
                    ("RECONNECT", "Second Connect on the same transport gets a ConnectionResponse within 5 s"),
                    ("RECONNECT_SAME_PORT", "Second Connect uses the same local port (socket not reopened)"),
                    ("DISCONNECT_2", "Disconnect after the second connect sends type 3 and deletes the connection"),
                })
                    Skipped(id, title, "CONNECT_1 failed");
                Phase("disconnect-after-failed-connect");
                _t.Disconnect("m1-link-check: connect failed");
                Hold(200);
            }

            Phase("stop");
            Mark("stopCall");
            _t.Stop();
            Phase("after-stop");
            Hold(LinkCheck.AfterStopMs);
            var afterStop = OwnUdpPorts("after-stop");
            Event("ports-after-stop", new JsonObject { ["ports"] = PortsJson(afterStop) });
            _snapshots["portsAfterStop"] = PortsJson(afterStop);

            Phase("dispose");
            Mark("disposeCall");
            _t.Dispose();
            Phase("after-dispose");
            Hold(LinkCheck.AfterDisposeMs);
            Mark("end");
            _pollStop = true;
            poller.Join(1000);
            JudgeNothingAfterStop();
        }
        catch (Exception e)
        {
            lock (_exceptions) _exceptions.Add(e.ToString());
            Event("exception", new JsonObject { ["exception"] = e.ToString() });
            try { _t?.Dispose(); } catch (Exception e2) { lock (_exceptions) _exceptions.Add("during dispose: " + e2); }
            _pollStop = true;
        }

        JudgeNoException();
        bool pass = WriteBundle(crashed: false);
        Console.WriteLine();
        foreach (var c in _checks)
            Console.WriteLine($"  {(c["pass"]!.GetValue<bool>() ? "PASS" : "FAIL"),-4}  {c["id"]}  {c["title"]}");
        Console.WriteLine($"M1-LINK: {(pass ? "PASS" : "FAIL")}  (robot-side observations for M1-033 are recorded without a verdict)");
        Console.WriteLine($"bundle: {_bundle}");
        Console.WriteLine("copy that whole folder back; nothing has been committed.");
        return pass ? 0 : 1;
    }

    private void Subscribe(ReliableTransport t)
    {
        t.FrameTrace += e =>
        {
            _frames.Enqueue(new FrameRec(T(e.Utc), e.Utc, e.Outbound, e.Raw, e.Frame, e.Error));
            if (e.Outbound) { Interlocked.Exchange(ref _lastOutTicks, e.Utc.Ticks); Interlocked.Increment(ref _outCount); }
        };
        t.Warning += w => Event("warning", new JsonObject { ["text"] = w });
        t.Received += r =>
        {
            var o = new JsonObject { ["marker"] = r.Marker.ToString(), ["address"] = r.Address?.ToString() };
            if (r.Data is { } d)
            {
                o["bytes"] = d.Length;
                if (d.Length > 0) { o["tag"] = $"0x{d[0]:X2}"; o["msg"] = TagName(d[0]); }
            }
            Event("receiver", o);
        };
        t.Connected += () => { Event("connected", new JsonObject()); _connected.Set(); };
        t.Disconnected += reason => Event("disconnected", new JsonObject { ["reason"] = reason });
    }

    /// <summary>Logs every change of the facade state, the timed-out flag and whether the peer has a connection.</summary>
    private void PollState()
    {
        string last = "";
        while (!_pollStop)
        {
            var t = _t;
            if (t is not null)
            {
                string now;
                try { now = $"{t.State}|{t.TimedOut}|{t.Connection is not null}"; } catch (Exception e) { now = "error " + e.GetType().Name; }
                if (now != last)
                {
                    var p = now.Split('|');
                    Event("state", p.Length == 3
                        ? new JsonObject { ["linkState"] = p[0], ["timedOut"] = p[1] == "True", ["hasConnection"] = p[2] == "True" }
                        : new JsonObject { ["error"] = now });
                    last = now;
                }
            }
            Thread.Sleep(2);
        }
    }

    /// <summary>Connect resets HandlerFaults (the host session reset), so the count so far is banked before each Connect.</summary>
    private void BankHandlerFaults()
    {
        if (_t is { } t) _handlerFaultsBanked += t.HandlerFaults;
    }

    private int HandlerFaultsTotal => _handlerFaultsBanked + (_t?.HandlerFaults ?? 0);

    private void Snapshot(string name)
    {
        var c = _t?.Connection;
        _snapshots[name] = c is null ? null : new JsonObject
        {
            ["note"] = "sampled without the transport lock; diagnostics only",
            ["framesSent"] = c.FramesSent, ["resendFrames"] = c.ResendFrames,
            ["duplicateReliableDropped"] = c.DuplicateReliableDropped,
            ["numPingsSent"] = c.NumPingsSent, ["numPingsReceived"] = c.NumPingsReceived,
            ["pingRepliesSeen"] = c.PingRepliesSeen, ["lastPingRoundTripMs"] = LinkCheck.Num(c.LastPingRoundTripMs),
            ["nextOutSeq"] = c.NextOutSeq, ["nextInSeq"] = c.NextInSeq, ["lastInAcked"] = c.LastInAcked,
            ["pendingCount"] = c.PendingCount,
        };
    }

    // ------------------------------------------------------------------ steps and checks

    private void AddCheck(string id, string title, bool pass, JsonObject measured, string expected, string[] rows, string[] records, string? note = null)
    {
        var c = new JsonObject
        {
            ["id"] = id, ["title"] = title, ["pass"] = pass, ["measured"] = measured, ["expected"] = expected,
            ["rows"] = new JsonArray(rows.Select(r => (JsonNode)r).ToArray()),
            ["records"] = new JsonArray(records.Select(r => (JsonNode)r).ToArray()),
        };
        if (note is not null) c["note"] = note;
        _checks.Add(c);
        Event("check", new JsonObject { ["id"] = id, ["pass"] = pass });
    }

    private void Skipped(string id, string title, string why) =>
        AddCheck(id, title, false, new JsonObject { ["skipped"] = why }, "not run", Array.Empty<string>(), Array.Empty<string>(), $"not run: {why}");

    private void JudgeSocket(List<IPEndPoint> opened, List<IPEndPoint> baseline)
    {
        int port = opened.Count == 1 ? opened[0].Port : -1;
        _localPort = port;
        var range = DynamicPortRange();
        bool inRange = range is { } r && port >= r.Start && port < r.Start + r.Count;
        bool pass = opened.Count == 1 && port > 0 && port != 0xBAC9;
        AddCheck("SOCKET_EPHEMERAL", "Start opens the UDP socket on an ephemeral (OS-assigned) local port", pass,
            new JsonObject
            {
                ["newLocalUdpEndpoints"] = PortsJson(opened),
                ["baselineEndpointsOfThisProcess"] = PortsJson(baseline),
                ["localPort"] = port,
                ["method"] = _portMethod,
                ["osDynamicRange"] = range is { } rr ? $"{rr.Start}..{rr.Start + rr.Count - 1}" : null,
                ["inOsDynamicRange"] = range is null ? null : inRange,
                ["openedAfterStartMs"] = LinkCheck.Num(_marks["startSeen"] - _marks["startCall"]),
            },
            "exactly one new UDP socket for this process, bound to INADDR_ANY on a port the OS chose: not 0 and not 47817 (the port every open after a close uses). The OS dynamic range is informational.",
            new[] { "B12", "B11", "R39" }, new[] { "M1-022", "M1-019" });
    }

    private bool ConnectAndJudge(string id, string markName)
    {
        BankHandlerFaults();
        _connected.Reset();
        Mark(markName);
        double call = _marks[markName];
        _t!.Connect(_ip, _sim);
        bool ok = _connected.Wait(LinkCheck.ConnectWaitMs);
        double at = Now();
        Mark(markName + "Done");
        // the ConnectionResponse frame, and our ConnectionRequest(s)
        WaitUntil(() => Frames().Any(f => !f.Out && f.T >= call && HasSub(f, ReliableMessageType.ConnectionResponse)), ok ? 200 : 0);
        var fr = Frames();
        var reqs = fr.Where(f => f.Out && f.T >= call && HasSub(f, ReliableMessageType.ConnectionRequest)).ToList();
        var resp = fr.FirstOrDefault(f => !f.Out && f.T >= call && HasSub(f, ReliableMessageType.ConnectionResponse));
        var measured = new JsonObject
        {
            ["connectedEventAfterMs"] = ok ? LinkCheck.Num(at - call) : null,
            ["connectionRequestSends"] = reqs.Count,
            ["firstConnectionRequestAtMs"] = reqs.Count > 0 ? LinkCheck.Num(reqs[0].T - call) : null,
            ["connectionRequestSeq"] = reqs.Count > 0 ? reqs[0].Frame?.SeqMin : null,
            ["connectionResponseFrameAtMs"] = resp is null ? null : LinkCheck.Num(resp.T - call),
            ["connectionResponseFrame"] = resp is null ? null : HeaderJson(resp),
            ["linkState"] = _t.State.ToString(),
        };
        string title = id == "CONNECT_1"
            ? "Connect gets the robot's ConnectionResponse within 5 s"
            : "Second Connect on the same transport gets a ConnectionResponse within 5 s";
        AddCheck(id, title, ok, measured,
            "the facade Connected event (the receiver's OnConnected for a type-2 ConnectionResponse on this link's connection) within 5000 ms of Connect. A new connection's ConnectionRequest is reliable, seq 1. An unanswered connection would time out 5000 ms after it was created.",
            new[] { "R38", "R13", "R12", "R40", "G2.1", "G2.2", "G2.7" }, new[] { "M1-019", "M1-018", "M1-032", "M1-033" },
            "whether and how fast the robot answers is robot-side behaviour (M1-033, HARDWARE_ONLY)");
        return ok;
    }

    private void JudgeHold()
    {
        double s = _marks["hold1Start"], e = _marks["hold1End"];
        var ev = _events.ToArray();
        var timeoutWarnings = ev.Count(x => x.Kind == "warning" && x.T >= s && x.T <= e + 5 && Text(x).Contains("TimedOut", StringComparison.Ordinal));
        var disc = ev.Where(x => x.Kind == "disconnected" && x.T >= s && x.T <= e + 5).Select(x => (JsonNode?)Text(x, "reason")).ToArray();
        var inbound = Frames().Where(f => !f.Out && f.T >= s && f.T <= e).Select(f => f.T).OrderBy(x => x).ToList();
        var gaps = new List<double>();
        double prev = s;
        foreach (var t in inbound) { gaps.Add(t - prev); prev = t; }
        gaps.Add(e - prev);
        bool timedOut = _t!.TimedOut;
        var state = _t.State;
        bool hasConn = _t.Connection is not null;
        bool pass = !timedOut && timeoutWarnings == 0 && disc.Length == 0 && state == LinkState.Connected && hasConn;
        AddCheck("HOLD_NO_TIMEOUT", "No connection timeout during the 30 s idle hold", pass,
            new JsonObject
            {
                ["holdMs"] = LinkCheck.Num(e - s),
                ["timedOutFlag"] = timedOut, ["timeoutWarnings"] = timeoutWarnings,
                ["disconnectedEvents"] = new JsonArray(disc),
                ["linkStateAtEnd"] = state.ToString(), ["connectionAtEnd"] = hasConn,
                ["inboundFrames"] = inbound.Count,
                ["maxInboundGapMs"] = LinkCheck.Num(gaps.Max()),
            },
            "no \"Disconnecting TimedOut Connection\", the +0xA1 timed-out flag clear, no Disconnected event, still Connected with a connection at the end. The timeout is now > lastRecv + 5000 ms, so this also needs the robot to send something at least every 5 s.",
            new[] { "R19", "B22", "R41", "B36" }, new[] { "M1-015", "M1-033" },
            "a robot that stays silent for 5 s, or sends a DisconnectRequest, fails this check for robot-side reasons (M1-033); see maxInboundGapMs and the events");
    }

    private void JudgePings()
    {
        double s = _marks["hold1Start"], e = _marks["hold1End"];
        var outAll = Frames().Where(f => f.Out).OrderBy(f => f.T).ToList();
        var inWin = outAll.Where(f => f.T >= s && f.T <= e).ToList();
        var pings = inWin.Where(f => HasSub(f, ReliableMessageType.Ping)).ToList();
        var nonPings = inWin.Where(f => !HasSub(f, ReliableMessageType.Ping)).ToList();
        var pingToPing = new List<double>();
        for (int i = 1; i < pings.Count; i++) pingToPing.Add(pings[i].T - pings[i - 1].T);
        var sendToPing = new List<double>();
        foreach (var p in pings)
        {
            int idx = outAll.IndexOf(p);
            if (idx > 0) sendToPing.Add(p.T - outAll[idx - 1].T);
        }
        int replies = 0;
        foreach (var p in pings)
            foreach (var m in p.Frame!.Messages)
                if (m.Type == ReliableMessageType.Ping && PingPayload.TryParse(m.Payload, out var pp) && pp.IsReply) replies++;
        double floor = LinkCheck.PingIntervalMs - LinkCheck.StampToleranceMs;
        int expectedMin = (int)Math.Floor(LinkCheck.PingCountFraction * (e - s) / LinkCheck.PingMedianMaxMs);
        var p2pSorted = pingToPing.OrderBy(x => x).ToList();
        double median = LinkCheck.Percentile(p2pSorted, 0.5);
        bool pass = pings.Count >= expectedMin
                    && pingToPing.Count > 0 && pingToPing.Min() >= floor
                    && sendToPing.Count > 0 && sendToPing.Min() >= floor
                    && median <= LinkCheck.PingMedianMaxMs
                    && replies == 0;
        var buckets = new JsonObject
        {
            ["<32.3"] = pingToPing.Count(x => x < floor),
            ["32.3-35.3"] = pingToPing.Count(x => x >= floor && x < 35.3),
            ["35.3-37.3"] = pingToPing.Count(x => x >= 35.3 && x < 37.3),
            ["37.3-50"] = pingToPing.Count(x => x >= 37.3 && x < 50),
            ["50-66.6"] = pingToPing.Count(x => x >= 50 && x < 66.6),
            [">=66.6 (a missed slot)"] = pingToPing.Count(x => x >= 66.6),
        };
        AddCheck("IDLE_PING_CADENCE", "Idle pings at the R32 cadence during the hold", pass,
            new JsonObject
            {
                ["windowMs"] = LinkCheck.Num(e - s),
                ["pingFrames"] = pings.Count, ["minimumPingFramesRequired"] = expectedMin,
                ["nonPingOutboundFrames"] = nonPings.Count,
                ["nonPingOutboundTypes"] = Histogram(nonPings.Select(f => TypeName(f.Frame?.Type))),
                ["pingToPingIntervalMs"] = LinkCheck.Stats(pingToPing),
                ["previousSendToPingIntervalMs"] = LinkCheck.Stats(sendToPing),
                ["pingToPingBucketsMs"] = buckets,
                ["pingsSentWithIsReply"] = replies,
            },
            $"every ping at least 33.3 ms after the previous outbound frame and the previous ping (R32; {LinkCheck.StampToleranceMs} ms allowed for host timestamps taken after sendto, so >= {floor}); median ping-to-ping interval <= {LinkCheck.PingMedianMaxMs} ms (the first 2 ms update after 33.3 ms, R35, with one update of slack); at least {LinkCheck.PingCountFraction:P0} of the pings that cadence gives over the window; every ping a request, isReply 0, since the engine never answers pings (R31, sSendSeparatePingMessages 0). Intervals above the median come from host scheduling; they are reported, not judged.",
            new[] { "R32", "R33", "R30", "R31", "R35", "B14", "B13", "B15", "G1.2", "G1.8" }, new[] { "M1-017", "M1-008", "M1-011", "M1-005", "M1-010", "M1-020", "M1-021" },
            "pings are sent only while nothing is pending, so a robot that never acks the ConnectionRequest shows up here as resends and no pings");
    }

    /// <summary>Waits for an outbound frame, then issues Disconnect <see cref="LinkCheck.DisconnectOffsetMs"/> after it. Returns the call time.</summary>
    private double TimedDisconnect(string reason)
    {
        int n0 = Volatile.Read(ref _outCount);
        WaitUntil(() => Volatile.Read(ref _outCount) > n0, 200);
        var target = new DateTime(Interlocked.Read(ref _lastOutTicks), DateTimeKind.Utc).AddMilliseconds(LinkCheck.DisconnectOffsetMs);
        while (DateTime.UtcNow < target) Thread.SpinWait(50);
        double call = Now();
        _t!.Disconnect(reason);
        return call;
    }

    private FrameRec? JudgeDisconnect(string id, double call)
    {
        bool sent = WaitUntil(() => Frames().Any(f => f.Out && f.T >= call && HasSub(f, ReliableMessageType.DisconnectRequest)), LinkCheck.DisconnectConfirmMs);
        bool deleted = WaitUntil(() => _t!.Connection is null && _t.State == LinkState.Disconnected, sent ? 200 : LinkCheck.DisconnectConfirmMs);
        var fr = Frames().Where(f => f.Out).OrderBy(f => f.T).ToList();
        var type3 = fr.Where(f => f.T >= call && HasSub(f, ReliableMessageType.DisconnectRequest)).ToList();
        var first = type3.FirstOrDefault();
        var before = fr.LastOrDefault(f => f.T < (first?.T ?? call) && !ReferenceEquals(f, first));
        var reason = _events.ToArray().LastOrDefault(x => x.Kind == "disconnected" && x.T >= call);
        bool pass = type3.Count == 1 && first!.Frame is { } f3 && f3.Type == ReliableMessageType.DisconnectRequest && f3.SeqMin != 0 && deleted;
        string? note = null;
        if (type3.Count == 0 && before is not null && call - before.T < LinkCheck.TickMs)
            note = "no type 3 was seen, and the previous outbound frame was under 2.0 ms before the call: the R25(a) spacing gate can block R38's single send. Rerun.";
        AddCheck(id, id == "DISCONNECT_1" ? "Disconnect sends type 3 and deletes the connection" : "Disconnect after the second connect sends type 3 and deletes the connection", pass,
            new JsonObject
            {
                ["disconnectRequestFrames"] = type3.Count,
                ["sentAfterCallMs"] = first is null ? null : LinkCheck.Num(first.T - call),
                ["frame"] = first is null ? null : HeaderJson(first),
                ["previousOutboundBeforeCallMs"] = before is null ? null : LinkCheck.Num(call - before.T),
                ["connectionDeleted"] = _t!.Connection is null,
                ["linkState"] = _t.State.ToString(),
                ["disconnectedEventReason"] = reason is null ? null : Text(reason, "reason"),
            },
            "exactly one frame carrying a DisconnectRequest: a single reliable type-3 frame (seq != 0, flush) to the robot, then that connection deleted at once (the peer has no connection; the link is Disconnected).",
            new[] { "R38", "B21", "R25(a)" }, new[] { "M1-019" }, note);
        return first;
    }

    private void JudgeNoResend(double call, FrameRec? d1Frame, double windowStart)
    {
        double end = _marks["afterDisconnect1End"];
        double from = d1Frame?.T ?? call;
        var after = Frames().Where(f => f.Out && f.T > from && f.T <= end && !ReferenceEquals(f, d1Frame)).ToList();
        var unconnected = _events.ToArray().Where(x => x.Kind == "warning" && x.T >= call && x.T <= end && Text(x).StartsWith("unconnected source", StringComparison.Ordinal)).ToList();
        AddCheck("DISCONNECT_1_NO_RESEND", "Nothing sent after the disconnect (type 3 never resent)", after.Count == 0 && d1Frame is not null,
            new JsonObject
            {
                ["outboundFramesAfterType3"] = after.Count,
                ["outboundTypes"] = Histogram(after.Select(f => TypeName(f.Frame?.Type))),
                ["windowMs"] = LinkCheck.Num(end - from),
                ["robotFramesDroppedAsUnconnectedSource"] = unconnected.Count,
            },
            "no outbound frame from the type-3 send to the end of the 2 s wait: the type 3 has at most one send and is never resent, and a deleted connection is no longer updated, so there is nothing to ping or resend. Robot frames arriving then are dropped as \"unconnected source\" (reported).",
            new[] { "R38", "R13", "R34" }, new[] { "M1-019", "M1-018" },
            d1Frame is null ? "no type 3 was seen, so this window starts at the Disconnect call" : null);
    }

    private void JudgeSamePort(List<IPEndPoint> baseline)
    {
        var now = NewPorts(baseline, "after-reconnect");
        int port = now.Count == 1 ? now[0].Port : -1;
        bool pass = _localPort > 0 && now.Count == 1 && port == _localPort;
        _snapshots["portsAfterReconnect"] = PortsJson(now);
        AddCheck("RECONNECT_SAME_PORT", "Second Connect uses the same local port (socket not reopened)", pass,
            new JsonObject { ["localPortAtStart"] = _localPort, ["localEndpointsAfterReconnect"] = PortsJson(now), ["method"] = _portMethod },
            "the one socket of this process still bound to the port Start opened: Connect opens no socket (R38), and StartClient opens only when there is none (B12).",
            new[] { "R38", "B12", "R39" }, new[] { "M1-019", "M1-022" });
    }

    private void JudgeNothingAfterStop()
    {
        double stop = _marks["stopCall"];
        var after = Frames().Where(f => f.Out && f.T > stop).ToList();
        AddCheck("NOTHING_AFTER_STOP", "Nothing sent after Stop (through Dispose)", after.Count == 0,
            new JsonObject
            {
                ["outboundFramesAfterStop"] = after.Count,
                ["outboundTypes"] = Histogram(after.Select(f => TypeName(f.Frame?.Type))),
                ["windowMs"] = LinkCheck.Num(_marks["end"] - stop),
                ["disposeAfterStopMs"] = LinkCheck.Num(_marks["disposeCall"] - stop),
            },
            "no outbound frame after the Stop call: Stop sends nothing and clears every connection (R39), and Dispose's disconnect then finds no connection (\"unconnected destination\", nothing sent).",
            new[] { "R39", "R13", "B33" }, new[] { "M1-019" });
    }

    private void JudgeNoException()
    {
        int faults = HandlerFaultsTotal;
        string[] ex; lock (_exceptions) ex = _exceptions.ToArray();
        AddCheck("NO_UNHANDLED_EXCEPTION", "No unhandled exception, and no handler fault", ex.Length == 0 && faults == 0,
            new JsonObject
            {
                ["exceptions"] = new JsonArray(ex.Select(x => (JsonNode)x).ToArray()),
                ["handlerFaults"] = faults,
            },
            "no exception from any transport call or thread, and none from this tool's event handlers (the transport counts those as HandlerFaults, handler isolation being policy M1-034).",
            new[] { "G3 (policy D6)" }, new[] { "M1-034" });
    }

    // ------------------------------------------------------------------ bundle

    private bool WriteBundle(bool crashed)
    {
        if (Interlocked.Exchange(ref _written, 1) == 1) return false;
        var frames = Frames().OrderBy(f => f.T).ToArray();
        var events = _events.ToArray().OrderBy(e => e.T).ToArray();
        (string Name, double Start)[] phases; lock (_phases) phases = _phases.ToArray();
        string PhaseAt(double t) { string n = "pre"; foreach (var p in phases) { if (p.Start <= t) n = p.Name; else break; } return n; }

        var records = ManifestStatuses();
        bool pass = !crashed && _checks.Count > 0 && _checks.All(c => c["pass"]!.GetValue<bool>());
        var endLocal = DateTimeOffset.Now;
        var opts = new JsonSerializerOptions { WriteIndented = true };

        var result = new JsonObject
        {
            ["test"] = LinkCheck.TestId,
            ["overall"] = crashed ? "FAIL (process crashed)" : pass ? "PASS" : "FAIL",
            ["tool"] = "cozmo-conformance m1-link-check",
            ["robot"] = new JsonObject { ["ip"] = _ip.ToString(), ["port"] = _port, ["simulated"] = _sim },
            ["localPort"] = _localPort,
            ["provenance"] = "hardware verification only: a PASS does not raise the provenance or status of any cited record (AGENTS.md: hardware success does not upgrade provenance). The manager judges the bundle.",
            ["checks"] = new JsonArray(_checks.Select(c => (JsonNode)c.DeepClone()).ToArray()),
            ["reports"] = new JsonArray(ErrorCountersReport()),
            ["observations"] = Observations(frames, events, PhaseAt),
            ["hardwareOnlyUncertainty"] = new JsonArray(
                (JsonNode)"M1-033 (HARDWARE_ONLY): robot-side transport behaviour. The robot's answers, idle traffic, acks and resends are inputs to CONNECT_1, HOLD_NO_TIMEOUT, RECONNECT and IDLE_PING_CADENCE; they are recorded under observations without a verdict."),
            ["records"] = records,
            ["phases"] = new JsonArray(phases.Select(p => (JsonNode)new JsonObject { ["name"] = p.Name, ["startMs"] = LinkCheck.Num(p.Start) }).ToArray()),
            ["marksMs"] = new JsonObject(_marks.Select(kv => KeyValuePair.Create(kv.Key, LinkCheck.Num(kv.Value)))),
        };
        File.WriteAllText(Path.Combine(_bundle, "result.json"), result.ToJsonString(opts));

        using (var w = new StreamWriter(Path.Combine(_bundle, "frames.jsonl")))
            foreach (var f in frames) { var o = FrameJson(f); o["phase"] = PhaseAt(f.T); w.WriteLine(o.ToJsonString()); }
        using (var w = new StreamWriter(Path.Combine(_bundle, "events.jsonl")))
            foreach (var e in events)
            {
                var o = new JsonObject { ["t"] = LinkCheck.Num(e.T), ["phase"] = PhaseAt(e.T), ["kind"] = e.Kind };
                foreach (var kv in e.Data) o[kv.Key] = kv.Value?.DeepClone();
                w.WriteLine(o.ToJsonString());
            }

        File.WriteAllText(Path.Combine(_bundle, "counters.json"), Counters(frames, events).ToJsonString(opts));
        File.WriteAllText(Path.Combine(_bundle, "env.json"), Env(endLocal).ToJsonString(opts));
        File.WriteAllText(Path.Combine(_bundle, "README.md"), Readme(result["overall"]!.GetValue<string>()));
        return pass;
    }

    private JsonNode ErrorCountersReport()
    {
        var t = _t;
        JsonObject Codes(ReceiveErrorCounts? c)
        {
            var o = new JsonObject();
            if (c is null) return o;
            for (int code = 0; code <= 10; code++) if (c[code] != 0) o[code.ToString(CultureInfo.InvariantCulture)] = c[code];
            return o;
        }
        return new JsonObject
        {
            ["id"] = "ERROR_COUNTERS",
            ["judged"] = false,
            ["title"] = "Receive and send error counters (reported, not judged)",
            ["measured"] = new JsonObject
            {
                ["udpReceiveErrorsByCode"] = Codes(t?.UdpReceiveErrors),
                ["reliableReceiveErrorsByCode"] = Codes(t?.ReliableReceiveErrors),
                ["udpSendErrorsByCode"] = Codes(t?.UdpSendErrors),
                ["lastSendErrorMs"] = t is null ? null : LinkCheck.Num(t.LastSendErrorMs),
            },
            ["codes"] = "UDP receive: 1 truncated (B17). Reliable receive: 0 under 10 bytes, 2 no RE\\x01, each then 4 (R3); 4 invalid sub-type, 1 size overrun (R10); 5 out-of-range reliable frame that is not type 9 (R16). UDP send: 6 failed sendto (B10). Codes with count 0 are omitted.",
            ["rows"] = new JsonArray("B17", "R3", "R10", "R16", "B10"),
            ["records"] = new JsonArray("M1-022"),
        };
    }

    private JsonObject Observations(FrameRec[] frames, EventRec[] events, Func<double, string> phaseAt)
    {
        var inbound = frames.Where(f => !f.Out).ToList();
        var ourPingTimes = new HashSet<double>();
        foreach (var f in frames.Where(f => f.Out && f.Frame is not null))
            foreach (var m in f.Frame!.Messages)
                if (m.Type == ReliableMessageType.Ping && PingPayload.TryParse(m.Payload, out var p)) ourPingTimes.Add(p.TimeSentMs);

        int pings = 0, replyTrue = 0, replyFalse = 0, echoes = 0, echoesReply = 0, own = 0, shortPings = 0;
        var samples = new JsonArray();
        foreach (var f in inbound.Where(f => f.Frame is not null))
            foreach (var m in f.Frame!.Messages.Where(m => m.Type == ReliableMessageType.Ping))
            {
                pings++;
                if (!PingPayload.TryParse(m.Payload, out var p)) { shortPings++; continue; }
                if (p.IsReply) replyTrue++; else replyFalse++;
                bool echo = ourPingTimes.Contains(p.TimeSentMs);
                if (echo) { echoes++; if (p.IsReply) echoesReply++; } else own++;
                if (samples.Count < 5)
                    samples.Add(new JsonObject { ["t"] = LinkCheck.Num(f.T), ["timeSentMs"] = LinkCheck.Num(p.TimeSentMs), ["numPingsSent"] = p.NumPingsSent, ["numPingsReceived"] = p.NumPingsReceived, ["isReply"] = p.IsReply, ["echoOfOurs"] = echo, ["size"] = m.Payload.Length });
            }

        // Robot resends: each reliable sub-message seq, per connection.
        var epochs = new List<(string Name, double From, double To)>();
        if (_marks.TryGetValue("connect1Call", out var c1)) epochs.Add(("connection-1", c1, _marks.GetValueOrDefault("disconnect1Call", double.MaxValue)));
        if (_marks.TryGetValue("connect2Call", out var c2)) epochs.Add(("connection-2", c2, _marks.GetValueOrDefault("disconnect2Call", double.MaxValue)));
        var resends = new JsonObject();
        foreach (var (name, from, to) in epochs)
        {
            var seen = new Dictionary<ushort, (string Type, List<double> Times)>();
            foreach (var f in inbound.Where(f => f.T >= from && f.T < to && f.Frame is not null))
                foreach (var m in f.Frame!.Messages.Where(m => m.IsReliable))
                {
                    if (!seen.TryGetValue(m.Seq, out var e)) seen[m.Seq] = e = (m.Type.ToString(), new List<double>());
                    e.Times.Add(f.T);
                }
            var repeated = seen.Where(kv => kv.Value.Times.Count > 1).OrderBy(kv => kv.Value.Times[0]).ToList();
            var intervals = repeated.SelectMany(kv => kv.Value.Times.Zip(kv.Value.Times.Skip(1), (a, b) => b - a)).ToList();
            resends[name] = new JsonObject
            {
                ["reliableSeqsSeen"] = seen.Count,
                ["seqsSeenMoreThanOnce"] = repeated.Count,
                ["maxTimesOneSeqSeen"] = seen.Count == 0 ? 0 : seen.Values.Max(v => v.Times.Count),
                ["repeatIntervalMs"] = LinkCheck.Stats(intervals),
                ["sample"] = new JsonArray(repeated.Take(20).Select(kv => (JsonNode)new JsonObject
                {
                    ["seq"] = kv.Key, ["subType"] = kv.Value.Type,
                    ["timesMs"] = new JsonArray(kv.Value.Times.Take(10).Select(x => LinkCheck.Num(x)).ToArray()),
                }).ToArray()),
            };
        }

        JsonObject Window(double from, double to)
        {
            var w = inbound.Where(f => f.T >= from && f.T <= to).ToList();
            var tags = new List<string>();
            foreach (var f in w.Where(f => f.Frame is not null))
                foreach (var m in f.Frame!.Messages.Where(m => m.Type is ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage))
                    tags.Add(m.Payload.Length == 0 ? "(empty)" : $"0x{m.Payload[0]:X2} {TagName(m.Payload[0]) ?? "?"}");
            return new JsonObject
            {
                ["windowMs"] = LinkCheck.Num(to - from),
                ["frames"] = w.Count,
                ["framesPerSecond"] = LinkCheck.Num(to > from ? w.Count * 1000.0 / (to - from) : double.NaN),
                ["frameTypes"] = Histogram(w.Select(f => HeaderType(f))),
                ["subMessageTypes"] = Histogram(w.Where(f => f.Frame is not null).SelectMany(f => f.Frame!.Messages).Select(m => m.Type.ToString())),
                ["robotMessages"] = Histogram(tags),
            };
        }

        var unconnected = events.Where(e => e.Kind == "warning" && Text(e).StartsWith("unconnected source", StringComparison.Ordinal)).ToList();
        var firstAck = new JsonObject();
        foreach (var (name, from, _) in epochs)
        {
            var f = inbound.FirstOrDefault(x => x.T >= from && ParseHeader(x.Raw) is { Ack: not 0 });
            firstAck[name] = f is null ? null : new JsonObject { ["ack"] = ParseHeader(f.Raw)!.Value.Ack, ["afterConnectCallMs"] = LinkCheck.Num(f.T - from), ["frameType"] = HeaderType(f) };
        }

        return new JsonObject
        {
            ["record"] = "M1-033",
            ["status"] = "HARDWARE_ONLY",
            ["verdict"] = null,
            ["note"] = "recorded without a verdict: the robot firmware is not in the package, so none of this has an expected value",
            ["frameTypesFromRobot"] = Histogram(inbound.Select(f => HeaderType(f))),
            ["containerFramesFromRobot"] = new JsonObject
            {
                ["7 MultipleReliableMessages"] = inbound.Count(f => ParseHeader(f.Raw)?.Type == ReliableMessageType.MultipleReliableMessages),
                ["8 MultipleUnreliableMessages"] = inbound.Count(f => ParseHeader(f.Raw)?.Type == ReliableMessageType.MultipleUnreliableMessages),
                ["9 MultipleMixedMessages"] = inbound.Count(f => ParseHeader(f.Raw)?.Type == ReliableMessageType.MultipleMixedMessages),
            },
            ["framesThisStackSentByType"] = Histogram(frames.Where(f => f.Out).Select(f => HeaderType(f))),
            ["pingsFromRobot"] = new JsonObject
            {
                ["count"] = pings, ["isReplySet"] = replyTrue, ["isReplyClear"] = replyFalse, ["under17Bytes"] = shortPings,
                ["echoesOfOurTimestamps"] = echoes, ["echoesWithIsReplySet"] = echoesReply, ["robotOwnTimestamps"] = own,
                ["sample"] = samples,
            },
            ["robotReliableResends"] = resends,
            ["firstRobotAckAfterConnect"] = firstAck,
            ["robotWhileIdle"] = _marks.ContainsKey("hold1End") ? Window(_marks["hold1Start"], _marks["hold1End"]) : null,
            ["robotDuringSecondHold"] = _marks.ContainsKey("hold2End") ? Window(_marks["hold2Start"], _marks["hold2End"]) : null,
            ["robotDisconnectRequests"] = new JsonArray(inbound.Where(f => HasSub(f, ReliableMessageType.DisconnectRequest)).Select(f => LinkCheck.Num(f.T)).ToArray()),
            ["robotFramesDroppedAsUnconnectedSource"] = new JsonObject
            {
                ["count"] = unconnected.Count,
                ["byPhase"] = Histogram(unconnected.Select(e => phaseAt(e.T))),
                ["byFrameType"] = Histogram(unconnected.Select(e => Text(e).Split(';').Last().Trim())),
                ["note"] = "the transport drops these before its frame trace, so only the warning (source and frame type) is in the bundle, not the bytes",
            },
        };
    }

    private JsonObject Counters(FrameRec[] frames, EventRec[] events)
    {
        var o = new JsonObject
        {
            ["framesOut"] = frames.Count(f => f.Out),
            ["framesIn"] = frames.Count(f => !f.Out),
            ["bytesOut"] = frames.Where(f => f.Out).Sum(f => (long)f.Raw.Length),
            ["bytesIn"] = frames.Where(f => !f.Out).Sum(f => (long)f.Raw.Length),
            ["inboundTraceErrors"] = frames.Count(f => !f.Out && f.Error is not null),
            ["warnings"] = events.Count(e => e.Kind == "warning"),
            ["warningsByKind"] = Histogram(events.Where(e => e.Kind == "warning").Select(e => WarningKind(Text(e)))),
            ["receiverEventsByMarker"] = Histogram(events.Where(e => e.Kind == "receiver").Select(e => Text(e, "marker"))),
            ["handlerFaults"] = HandlerFaultsTotal,
            ["timedOutFlagAtEnd"] = _t?.TimedOut,
            ["errorCounters"] = ErrorCountersReport()["measured"]!.DeepClone(),
            ["connectionSnapshots"] = _snapshots.DeepClone(),
        };
        return o;
    }

    private JsonObject Env(DateTimeOffset endLocal)
    {
        string? head = null, status = null;
        if (_repoRoot is not null)
        {
            head = RunProcess("git", $"-C \"{_repoRoot}\" rev-parse HEAD")?.Trim();
            status = RunProcess("git", $"-C \"{_repoRoot}\" status --porcelain");
        }
        var statusLines = status?.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        string? Src(string rel) => _repoRoot is null ? null : Sha256File(Path.Combine(_repoRoot, "cozmo-stack", "src", rel));
        var o = TransportOptions.EngineDefaults;
        return new JsonObject
        {
            ["test"] = LinkCheck.TestId,
            ["commandLine"] = "cozmo-conformance " + string.Join(' ', _argv),
            ["gitHead"] = head,
            ["gitDirty"] = statusLines is null ? null : statusLines.Length > 0,
            ["gitStatus"] = statusLines is null ? null : new JsonArray(statusLines.Take(100).Select(l => (JsonNode)l).ToArray()),
            ["toolSourceSha256"] = new JsonObject
            {
                ["Cozmo.Conformance/LinkCheck.cs"] = Src(Path.Combine("Cozmo.Conformance", "LinkCheck.cs")),
                ["Cozmo.Conformance/Program.cs"] = Src(Path.Combine("Cozmo.Conformance", "Program.cs")),
                ["Cozmo.Transport/ReliableTransport.cs"] = Src(Path.Combine("Cozmo.Transport", "ReliableTransport.cs")),
                ["Cozmo.Transport/ReliableConnection.cs"] = Src(Path.Combine("Cozmo.Transport", "ReliableConnection.cs")),
                ["Cozmo.Transport/TransportConstants.cs"] = Src(Path.Combine("Cozmo.Transport", "TransportConstants.cs")),
            },
            ["assemblySha256"] = new JsonObject
            {
                ["cozmo-conformance.dll"] = Sha256File(typeof(LinkCheck).Assembly.Location),
                ["Cozmo.Transport.dll"] = Sha256File(typeof(ReliableTransport).Assembly.Location),
                ["Cozmo.Protocol.dll"] = Sha256File(typeof(Frame).Assembly.Location),
            },
            ["os"] = RuntimeInformation.OSDescription,
            ["osArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["dotnet"] = RuntimeInformation.FrameworkDescription,
            ["processId"] = Environment.ProcessId,
            ["robotIp"] = _ip.ToString(), ["robotPort"] = _port, ["simulated"] = _sim,
            ["localPort"] = _localPort, ["localPortMethod"] = _portMethod,
            ["localPortEvidence"] = _portEvidence.DeepClone(),
            ["startLocal"] = _t0Local.ToString("o", CultureInfo.InvariantCulture),
            ["startUtc"] = _t0Utc.ToString("o", CultureInfo.InvariantCulture),
            ["endLocal"] = endLocal.ToString("o", CultureInfo.InvariantCulture),
            ["endUtc"] = endLocal.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            ["timingsMs"] = new JsonObject
            {
                ["startWait"] = LinkCheck.StartWaitMs, ["connectWait"] = LinkCheck.ConnectWaitMs, ["hold1"] = LinkCheck.Hold1Ms,
                ["disconnectConfirm"] = LinkCheck.DisconnectConfirmMs, ["afterDisconnect"] = LinkCheck.AfterDisconnectMs,
                ["hold2"] = LinkCheck.Hold2Ms, ["afterStop"] = LinkCheck.AfterStopMs, ["afterDispose"] = LinkCheck.AfterDisposeMs,
                ["disconnectOffsetAfterOutboundFrame"] = LinkCheck.DisconnectOffsetMs,
            },
            ["transportOptions"] = new JsonObject
            {
                ["timeBetweenPingsMs"] = o.TimeBetweenPingsMs, ["timeBetweenResendsMs"] = o.TimeBetweenResendsMs,
                ["maxTimeSinceLastSendMs"] = o.MaxTimeSinceLastSendMs, ["connectionTimeoutMs"] = o.ConnectionTimeoutMs,
                ["packetSeparationIntervalMs"] = o.PacketSeparationIntervalMs, ["updateIntervalMs"] = o.UpdateIntervalMs,
                ["sendSeparatePingMessages"] = o.SendSeparatePingMessages, ["maxFramePayloadBytes"] = o.MaxFramePayloadBytes,
            },
        };
    }

    private string Readme(string overall)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# M1-LINK hardware run: {overall}");
        sb.AppendLine();
        sb.AppendLine($"Robot {_ip}:{_port}{(_sim ? " (simulated)" : "")}, started {_t0Local:yyyy-MM-dd HH:mm:ss zzz}. Command: `cozmo-conformance {string.Join(' ', _argv)}`.");
        sb.AppendLine();
        sb.AppendLine("## What was run");
        sb.AppendLine();
        sb.AppendLine("`m1-link-check` (cozmo-stack/src/Cozmo.Conformance/LinkCheck.cs) drives `ReliableTransport` directly through its public API. It sends transport-level frames only: ConnectionRequest, DisconnectRequest and the transport's own idle pings. No CLAD message is sent, so the robot gets no SyncTime, no firmware check and no app-layer setup. The sequence and its timings are fixed:");
        sb.AppendLine();
        sb.AppendLine($"1. Construct the production transport (async mode, engine defaults) and `Start`. Record the local UDP port this process bound, from the OS (`{_portMethod}`).");
        sb.AppendLine($"2. `Connect`, then wait up to {LinkCheck.ConnectWaitMs} ms for the ConnectionResponse (the `Connected` event).");
        sb.AppendLine($"3. Hold the link idle for {LinkCheck.Hold1Ms} ms.");
        sb.AppendLine($"4. `Disconnect`, issued {LinkCheck.DisconnectOffsetMs} ms after an outbound frame so that the 2.0 ms send-spacing gate cannot block the single type-3 send. Confirm the type 3 and the deleted connection, then wait {LinkCheck.AfterDisconnectMs} ms.");
        sb.AppendLine($"5. `Connect` again on the same transport, wait up to {LinkCheck.ConnectWaitMs} ms, confirm the local port is unchanged, then hold {LinkCheck.Hold2Ms} ms.");
        sb.AppendLine($"6. `Disconnect` (timed as in step 4), then `Stop`, wait {LinkCheck.AfterStopMs} ms, then `Dispose`, and wait {LinkCheck.AfterDisposeMs} ms.");
        sb.AppendLine();
        sb.AppendLine("## Files");
        sb.AppendLine();
        sb.AppendLine("- `result.json`: the verdict. `overall` is PASS only if every entry in `checks` has `pass: true`. Each check has an `id`, `title`, `pass`, `measured` (the values it was judged on), `expected` (the rule, in words), `rows` (the M1 inventory rows it rests on, in re-analysis/inventory/M1-transport.md) and `records` (fidelity manifest ids). A skipped check (after a failed connect) is `pass: false` with `measured.skipped`. `reports` holds the error counters, which are reported and not judged. `observations` is M1-033 (HARDWARE_ONLY): robot-side behaviour recorded with `verdict: null`. `records` gives each cited record's manifest status at run time. `phases` and `marksMs` give the times, in ms since the run began.");
        sb.AppendLine("- `frames.jsonl`: one line per datagram the transport's frame trace reported, in both directions. Each line has the time `t` (ms since start), `phase`, `dir` (out/in), the header (type, seqMin, seqMax, ack), the sub-messages (type, seq, size, and the parsed ping or the CLAD tag), and the raw datagram as `hex`.");
        sb.AppendLine("- `events.jsonl`: transport warnings, receiver events (R40 markers, with the CLAD tag for data), Connected/Disconnected, state changes (link state, the timed-out flag, whether the peer has a connection; polled every 2 ms), phases, and check verdicts.");
        sb.AppendLine("- `counters.json`: frame, byte and warning counts, the transport's error counters by code, handler faults, and snapshots of the connection's own counters at the end of each hold.");
        sb.AppendLine("- `env.json`: git HEAD and dirty state, SHA-256 of the tool and transport sources and of the assemblies that ran, OS, .NET, robot IP/port, local port with the netstat lines it came from, start/end times, the fixed timings, and the transport options.");
        sb.AppendLine();
        sb.AppendLine("## Limits of what the bundle shows");
        sb.AppendLine();
        sb.AppendLine("- The transport drops a datagram from an address with no connection (\"unconnected source\") and a truncated datagram before its frame trace. Such datagrams appear only as warnings in `events.jsonl`, with the source and frame type but without the bytes. This matters after each Disconnect.");
        sb.AppendLine("- An outbound time is taken just after sendto returns. An inbound time is taken when the transport's update processed the datagram, up to one 2 ms update after it arrived.");
        sb.AppendLine("- Connection snapshots are read without the transport lock, and are diagnostics only.");
        sb.AppendLine("- A PASS is hardware verification. It does not raise the provenance of any record.");
        return sb.ToString();
    }

    private JsonArray ManifestStatuses()
    {
        var arr = new JsonArray();
        Dictionary<string, string>? byId = null;
        try
        {
            if (_repoRoot is not null)
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_repoRoot, "re-analysis", "fidelity_manifest.json")));
                byId = doc.RootElement.GetProperty("records").EnumerateArray()
                    .ToDictionary(r => r.GetProperty("id").GetString()!, r => r.GetProperty("status").GetString()!);
            }
        }
        catch { byId = null; }
        foreach (var id in LinkCheck.CitedRecords)
            arr.Add(new JsonObject { ["id"] = id, ["status"] = byId is null ? "manifest not read" : byId.GetValueOrDefault(id, "MISSING FROM MANIFEST") });
        return arr;
    }

    // ------------------------------------------------------------------ frames

    private static ReliableHeader? ParseHeader(byte[] raw) => ReliableHeader.TryParse(raw, out var h, out _) ? h : null;
    private static string HeaderType(FrameRec f) => ParseHeader(f.Raw) is { } h ? TypeName(h.Type) : "(no header)";
    private static string TypeName(ReliableMessageType? t) => t is { } v ? $"{(byte)v} {v}" : "(undecoded)";
    private static bool HasSub(FrameRec f, ReliableMessageType type) => f.Frame is { } fr && fr.Messages.Any(m => m.Type == type);

    private static string? TagName(byte tag) =>
        MessageCatalog.ById.TryGetValue((RobotMessageId)tag, out var info) ? info.Member : null;

    private static JsonObject HeaderJson(FrameRec f)
    {
        var h = ParseHeader(f.Raw);
        return h is not { } v ? new JsonObject { ["error"] = f.Error } : new JsonObject
        {
            ["type"] = (byte)v.Type, ["typeName"] = v.Type.ToString(), ["seqMin"] = v.SeqMin, ["seqMax"] = v.SeqMax, ["ack"] = v.Ack,
            ["reliable"] = v.IsReliable, ["len"] = f.Raw.Length,
        };
    }

    private JsonObject FrameJson(FrameRec f)
    {
        var o = new JsonObject
        {
            ["t"] = LinkCheck.Num(f.T),
            ["utc"] = f.Utc.ToString("o", CultureInfo.InvariantCulture),
            ["dir"] = f.Out ? "out" : "in",
            ["len"] = f.Raw.Length,
        };
        if (ParseHeader(f.Raw) is { } h)
        {
            o["type"] = (byte)h.Type; o["typeName"] = h.Type.ToString();
            o["seqMin"] = h.SeqMin; o["seqMax"] = h.SeqMax; o["ack"] = h.Ack; o["reliable"] = h.IsReliable;
        }
        if (f.Frame is { } fr)
        {
            var subs = new JsonArray();
            foreach (var m in fr.Messages)
            {
                var s = new JsonObject { ["type"] = (byte)m.Type, ["name"] = m.Type.ToString(), ["seq"] = m.Seq, ["size"] = m.Payload.Length };
                if (m.Type == ReliableMessageType.Ping && PingPayload.TryParse(m.Payload, out var p))
                    s["ping"] = new JsonObject { ["timeSentMs"] = LinkCheck.Num(p.TimeSentMs), ["numPingsSent"] = p.NumPingsSent, ["numPingsReceived"] = p.NumPingsReceived, ["isReply"] = p.IsReply };
                else if (m.Type is ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage && m.Payload.Length > 0)
                { s["tag"] = $"0x{m.Payload[0]:X2}"; s["msg"] = TagName(m.Payload[0]); }
                else if (m.Type == ReliableMessageType.MultiPartMessage && m.Payload.Length >= 2)
                { s["part"] = m.Payload[0]; s["parts"] = m.Payload[1]; }
                subs.Add(s);
            }
            o["subs"] = subs;
        }
        if (f.Error is not null) o["error"] = f.Error;
        o["hex"] = Convert.ToHexString(f.Raw);
        return o;
    }

    private static JsonObject Histogram(IEnumerable<string?> keys)
    {
        var o = new JsonObject();
        foreach (var g in keys.GroupBy(k => k ?? "(null)").OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
            o[g.Key] = g.Count();
        return o;
    }

    private static string Text(EventRec e, string key = "text") => e.Data[key]?.GetValue<string>() ?? "";

    private static string WarningKind(string w)
    {
        int cut = w.IndexOfAny(new[] { ':', ';' });
        var head = cut > 0 ? w[..cut] : w;
        foreach (var p in new[] { "unconnected source", "unconnected destination", "Disconnecting TimedOut Connection", "ReadFailed", "sendto failed" })
            if (w.StartsWith(p, StringComparison.Ordinal)) return p;
        return head.Length > 60 ? head[..60] : head;
    }

    // ------------------------------------------------------------------ local ports, from the OS

    private List<IPEndPoint> NewPorts(List<IPEndPoint> baseline, string label) =>
        OwnUdpPorts(label).Where(p => !baseline.Contains(p)).ToList();

    /// <summary>
    /// The UDP endpoints this process has bound, as the OS reports them. On Windows, <c>netstat -ano -p UDP</c>
    /// filtered by this process id; elsewhere the OS's UDP listeners for every process (the caller diffs them
    /// against a baseline taken before Start).
    /// </summary>
    private List<IPEndPoint> OwnUdpPorts(string label)
    {
        var list = new List<IPEndPoint>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _portMethod = "netstat -ano -p UDP, filtered by this process id";
            var text = RunProcess("netstat", "-ano -p UDP") ?? "";
            var lines = new List<string>();
            string pid = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
            foreach (var raw in text.Split('\n'))
            {
                var tok = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tok.Length < 4 || tok[0] != "UDP" || tok[^1] != pid) continue;
                lines.Add(raw.Trim());
                if (IPEndPoint.TryParse(tok[1], out var ep)) list.Add(ep);
            }
            _portEvidence[label] = new JsonArray(lines.Select(l => (JsonNode)l).ToArray());
        }
        else
        {
            _portMethod = "IPGlobalProperties.GetActiveUdpListeners, diffed against a baseline (all processes)";
            try { list.AddRange(IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()); } catch { }
            _portEvidence[label] = new JsonArray(list.Select(e => (JsonNode)e.ToString()).ToArray());
        }
        return list;
    }

    private static JsonArray PortsJson(IEnumerable<IPEndPoint> ps) => new(ps.Select(p => (JsonNode)p.ToString()).ToArray());

    /// <summary>The OS's dynamic (ephemeral) UDP port range, if it can be read; informational.</summary>
    private static (int Start, int Count)? DynamicPortRange()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var text = RunProcess("netsh", "int ipv4 show dynamicport udp");
                if (text is null) return null;
                var nums = text.Split('\n').Select(l => l.Split(':')).Where(p => p.Length == 2)
                    .Select(p => int.TryParse(p[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1)
                    .Where(n => n >= 0).ToList();
                return nums.Count >= 2 ? (nums[0], nums[1]) : null;
            }
            const string linux = "/proc/sys/net/ipv4/ip_local_port_range";
            if (File.Exists(linux))
            {
                var p = File.ReadAllText(linux).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                int a = int.Parse(p[0], CultureInfo.InvariantCulture), b = int.Parse(p[1], CultureInfo.InvariantCulture);
                return (a, b - a + 1);
            }
        }
        catch { }
        return null;
    }

    private static string? RunProcess(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(10000)) { try { p.Kill(); } catch { } return null; }
            return output.Result;
        }
        catch { return null; }
    }

    private static string? Sha256File(string? path)
    {
        try { return path is null || !File.Exists(path) ? null : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(); }
        catch { return null; }
    }
}
