using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>
/// <c>m1m2-hw</c>: the M1+M2 acceptance run (M1M2-HW). It starts the production engine with the shipped resources,
/// observes the public startup (states, progress, the animation list, readiness), connects the way
/// <see cref="CozmoRobot.ConnectAsync"/> does (wait for delivered readiness, then ConnectToRobot), watches the link,
/// the reports delivered to the C# API and the wire, disconnects, and finally connects once more through the real
/// <see cref="CozmoRobot.ConnectAsync"/>. It writes one self-contained bundle
/// (re-analysis/acceptance/hardware/scripts/m1m2-hw-draft.md). It never commits, never edits the manifest, never moves
/// a motor and never writes the robot's NV storage.
///
/// Verdicts are OBSERVED_* (what was seen, with the rule) or INCONCLUSIVE (with what is missing). There is no PASS:
/// the manager judges the bundle, and a hardware observation never raises provenance (AGENTS.md "Hardware runs").
/// </summary>
public static class M1M2Hw
{
    public const string TestId = "M1M2-HW";

    /// <summary>Records this run observes. All must exist in the manifest or the run stops before touching the network; M1-033 is the HARDWARE_ONLY one.</summary>
    public static readonly string[] RecordIds =
        { "M1-033", "M1-054", "M1-055", "M1-056", "M1-058", "M1-040", "M1-042", "M2-021", "M3-033", "M1-061", "M1-062", "M2-024", "M1-019", "M1-044" };

    public sealed record CheckDef(string Id, string Title, string[] Records, bool NeedsOperator);

    public static readonly CheckDef[] Checks =
    {
        new("STARTUP", "Public startup through BeforeStart: UpdateEngineState 0->1, 1->2, loading progress, the animation list, EndOfMessage, 2->3, delivered readiness", new[] { "M1-054", "M1-055", "M1-056", "M1-058" }, false),
        new("CONNECT", "ConnectToRobot after delivered readiness: ConnectionResponse, firmware, time sync, first full state, ready to stream, then BlockPoolData after the response", new[] { "M1-040", "M1-042", "M2-021" }, false),
        new("NEEDS_READ", "The connection-time reads of NV 0x194000: the backup sweep and the needs read (two READs), no ERASE or WRITE of 0x194000 for the whole run, the robot's blob saved", new[] { "M3-033", "M1-054" }, false),
        new("LATENCY", "LatencyMessage on the C# API: cadence, constant and empty fields, receive-queue and image values; nothing while the game is paused", new[] { "M1-061", "M1-062" }, false),
        new("WIRE", "The link over the whole run: frame sizes and types, packing, sequence range, resends, and any 0xBA/0xBB/0xC3/0xB0/0xEC/0xB1/0xB2/0xB7/0xBC/0xBD arrivals; the operator taps the robot once", new[] { "M1-033", "M2-024" }, true),
        new("DISCONNECT", "Dispose: the DisconnectRequest frame, the end of inbound traffic, a bounded return", new[] { "M1-019", "M1-044" }, false),
        new("CONNECT_ASYNC", "A second engine through the real CozmoRobot.ConnectAsync with BeforeStart observers: success, readiness before the connect, then Dispose", new[] { "M2-021", "M1-056" }, false),
    };

    internal const uint NeedsTag = 0x194000;
    internal const int ShippedAnimationNames = 997; // independent census of the shipped tree (research/20261018-b-m1-5-check-readiness.md, F1)
    internal const int StartupBudgetMs = 60000, ConnectBudgetMs = 15000, SettleMs = 3000;
    internal const int LatencyWindowMs = 12000, PausedWindowMs = 4000, TapWindowMs = 10000, DisconnectWatchMs = 2000;
    internal const double ExpectedLatencyPeriodMs = 11 * 60.0; // every 11th eligible 60 ms tick (M1-061)
    internal const int WatchdogMs = 10 * 60 * 1000;
    internal static readonly byte[] WatchedTags = { 0xBA, 0xBB, 0xC3, 0xB0, 0xEC, 0xB1, 0xB2, 0xB7, 0xBC, 0xBD };

    public static List<JsonObject> LoadRecords(string manifestPath, List<string> warnings)
    {
        var root = JsonNode.Parse(File.ReadAllText(manifestPath, Encoding.UTF8))!.AsObject();
        var byId = root["records"]!.AsArray().Select(n => n!.AsObject()).ToDictionary(o => o["id"]!.GetValue<string>(), o => o, StringComparer.Ordinal);
        var missing = RecordIds.Where(id => !byId.ContainsKey(id)).ToList();
        if (missing.Count > 0) throw new InvalidOperationException("fidelity_manifest.json has no record " + string.Join(", ", missing) + ": the run is stale; stop");
        var list = new List<JsonObject>();
        foreach (var id in RecordIds)
        {
            var r = (JsonObject)byId[id].DeepClone();
            string status = r["status"]!.GetValue<string>();
            if (id == "M1-033" && status != "HARDWARE_ONLY") warnings.Add($"M1-033 is {status}, not HARDWARE_ONLY: the manager must re-read it before this run is used");
            if (id != "M1-033" && status is not ("EXACT_SOURCE" or "COMPATIBILITY_POLICY" or "EQUIVALENT_IMPLEMENTATION"))
                warnings.Add($"{id} is {status}: the run expected a settled or policy record");
            list.Add(r);
        }
        return list;
    }

    public static string? CheckDefinitionProblem()
    {
        var named = Checks.SelectMany(c => c.Records).Distinct().ToList();
        var stray = named.Except(RecordIds).ToList();
        if (stray.Count > 0) return "a check names a record outside the list: " + string.Join(", ", stray);
        var unnamed = RecordIds.Except(named).ToList();
        return unnamed.Count > 0 ? "a record no check names: " + string.Join(", ", unnamed) : null;
    }

    private static int Usage(string why)
    {
        if (why.Length > 0) Console.Error.WriteLine("m1m2-hw: " + why);
        Console.Error.WriteLine("usage: m1m2-hw [robot-ip] --resources <cozmo_resources> [--skip-connect-async] [--operator <label>] [--out <dir>] [--yes]\n"
                              + "  --resources is required (or --obb/--assets); --state-dir is refused. --yes asks no questions (the tap is skipped).");
        return 2;
    }

    public static int Run(string[] args)
    {
        IPAddress? ip = null; string? outParent = null, operatorLabel = null; bool yes = false, skipAsync = false;
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--resources": case "--obb": case "--assets":
                    if (++i >= args.Length) return Usage("resource option needs a directory");
                    break;
                case "--state-dir": return Usage("--state-dir is refused: this run must not read or write a needs device file");
                case "--yes": yes = true; break;
                case "--skip-connect-async": skipAsync = true; break;
                case "--operator": if (++i >= args.Length) return Usage("--operator needs a label"); operatorLabel = args[i]; break;
                case "--out": if (++i >= args.Length) return Usage("--out needs a directory"); outParent = args[i]; break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal)) return Usage($"unknown option {args[i]}");
                    if (ip is not null) return Usage($"unexpected argument {args[i]}");
                    if (!IPAddress.TryParse(args[i], out var parsed)) return Usage($"not an IP address: {args[i]}");
                    ip = parsed; break;
            }
        }
        ip ??= RobotAddress.DefaultFor(false);
        if (CheckDefinitionProblem() is { } problem) { Console.Error.WriteLine("m1m2-hw: " + problem); return 2; }
        CozmoEngineOptions baseOptions;
        try { baseOptions = EngineStartupResources.Options(args); } catch (ArgumentException e) { return Usage(e.Message); }
        string? repoRoot = LinkCheck.FindRepoRoot();
        if (repoRoot is null && outParent is null) return Usage("the repository root was not found (the manifest is needed); pass --out and run inside the repository");
        var warnings = new List<string>();
        List<JsonObject> records;
        try { records = LoadRecords(Path.Combine(repoRoot ?? ".", "re-analysis", "fidelity_manifest.json"), warnings); }
        catch (Exception e) { Console.Error.WriteLine("m1m2-hw: " + e.Message); return 2; }
        foreach (var w in warnings) Console.Error.WriteLine("m1m2-hw: WARNING " + w);
        string parent = outParent ?? Path.Combine(repoRoot!, "re-analysis", "acceptance", "hardware");
        string bundle = Path.GetFullPath(Path.Combine(parent, $"{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{TestId}"));
        return new M1M2Run(ip, bundle, repoRoot, args, baseOptions.ResourcesPath!, records, warnings, yes, skipAsync, operatorLabel).Execute();
    }
}

internal sealed class M1M2Run
{
    private sealed record FrameRec(double T, DateTime Utc, bool Out, byte[] Raw, Frame? Frame, string? Error);
    private sealed record Msg(double T, bool Out, byte Tag, byte[] Payload);
    private sealed record HostEvent(double T, string Kind, JsonObject Data);

    private readonly IPAddress _ip; private readonly string _bundle; private readonly string? _repoRoot; private readonly string[] _argv;
    private readonly string _resources; private readonly List<JsonObject> _records; private readonly List<string> _warnings;
    private readonly bool _yes, _skipAsync; private readonly string? _operator;
    private readonly DateTime _t0Utc = DateTime.UtcNow;
    private readonly ConcurrentQueue<FrameRec> _frames = new();
    private readonly ConcurrentQueue<HostEvent> _host = new();   // events delivered to the C# API, in delivery order
    private readonly ConcurrentQueue<(double T, string Kind, JsonObject Data)> _events = new();
    private readonly ConcurrentQueue<(double T, string Line)> _log = new();
    private readonly List<string> _exceptions = new();
    private readonly List<JsonObject> _results = new();
    private readonly List<(string Name, double From, double To)> _windows = new();
    private readonly JsonObject _robotInfo = new();
    private readonly List<string> _extraFiles = new();
    private CozmoRobot? _robot;
    private RobotConnectionResponse? _response;
    private double _disposeCall = double.NaN, _disposeReturn = double.NaN;
    private volatile bool _cancelled;
    private int _written;

    public M1M2Run(IPAddress ip, string bundle, string? repoRoot, string[] argv, string resources, List<JsonObject> records, List<string> warnings, bool yes, bool skipAsync, string? op)
    { _ip = ip; _bundle = bundle; _repoRoot = repoRoot; _argv = argv; _resources = resources; _records = records; _warnings = warnings; _yes = yes; _skipAsync = skipAsync; _operator = op; }

    private double Now() => (DateTime.UtcNow - _t0Utc).TotalMilliseconds;
    private double T(DateTime utc) => (utc - _t0Utc).TotalMilliseconds;
    private void Event(string kind, JsonObject data) => _events.Enqueue((Now(), kind, data));
    private void Host(string kind, JsonObject data) => _host.Enqueue(new HostEvent(Now(), kind, data));
    private static JsonNode? Num(double d) => LinkCheck.Num(d);
    private bool Wait(int ms) { var end = Environment.TickCount64 + ms; while (Environment.TickCount64 < end) { if (_cancelled) return false; Thread.Sleep(5); } return !_cancelled; }
    private bool Poll(Func<bool> cond, int timeoutMs) { var end = Environment.TickCount64 + timeoutMs; while (Environment.TickCount64 < end) { if (cond()) return true; if (_cancelled) return false; Thread.Sleep(5); } return cond(); }

    internal int Execute()
    {
        using var hr = new HighResolutionTimer();
        Console.WriteLine($"m1m2-hw: robot {_ip}:{RobotAddress.RemotePort(false)}; resources {_resources}; bundle {_bundle}");
        Console.WriteLine("""

            M1M2-HW. Read re-analysis/acceptance/hardware/scripts/m1m2-hw-draft.md first. Setup:
              - the computer is on the robot's Wi-Fi; the official app and every other client is closed;
              - Cozmo on its treads, off the charger, battery not low; nothing moves in this run;
              - nothing is written to the robot's storage; no --state-dir;
              - during WIRE you are asked to tap the top of Cozmo's head once (Enter when done; blank skips).
            Ctrl+C stops the run and still writes the bundle.
            """);
        if (!_yes) { Console.Write("Press Enter to start ... "); Console.ReadLine(); }
        Directory.CreateDirectory(_bundle);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; _cancelled = true; lock (_exceptions) _exceptions.Add("interrupted: Ctrl+C"); };
        var watchdog = new Thread(() =>
        {
            Thread.Sleep(M1M2Hw.WatchdogMs);
            if (Volatile.Read(ref _written) == 1) return;
            lock (_exceptions) _exceptions.Add($"watchdog: the run exceeded {M1M2Hw.WatchdogMs} ms");
            try { _robot?.Dispose(); } catch { }
            WriteBundle(); Environment.Exit(3);
        }) { IsBackground = true, Name = "m1m2-watchdog" };
        watchdog.Start();

        var stateRes = new Dictionary<string, JsonObject>();
        try
        {
            double createAt = Now();
            _robot = CozmoRobot.Create(engineOptions: new CozmoEngineOptions { ResourcesPath = _resources, BeforeStart = e => SubscribeHost(e, "main") });
            SubscribeRobot(_robot);
            Window("STARTUP", createAt, () =>
            {
                bool ready = Poll(() => _robot!.Engine.IsReadyDelivered || _robot.Engine.StartupFailed, M1M2Hw.StartupBudgetMs);
                return StartupResult(createAt, ready, "main");
            });
            bool connected = false;
            Window("CONNECT", Now(), () => { var (ok, res) = ConnectCheck(); connected = ok; return res; });
            if (connected)
            {
                Window("LATENCY", Now(), LatencyCheck);
                Window("WIRE_TAP", Now(), TapWindow);
            }
            else { Skip("LATENCY", "no connection"); }
            Window("DISCONNECT", Now(), DisconnectCheck);
            Window("NEEDS_READ", Now(), NeedsCheck);
            Window("WIRE", Now(), WireCheck);
            if (_skipAsync) Skip("CONNECT_ASYNC", "--skip-connect-async");
            else if (!connected) Skip("CONNECT_ASYNC", "the first connection did not succeed");
            else Window("CONNECT_ASYNC", Now(), ConnectAsyncCheck);
        }
        catch (Exception e) { lock (_exceptions) _exceptions.Add(e.ToString()); }
        finally { if (double.IsNaN(_disposeCall)) StopBounded(); }
        string overall = WriteBundle();
        Console.WriteLine();
        foreach (var r in _results) Console.WriteLine($"  {r["verdict"],-40} {r["id"]}");
        Console.WriteLine($"M1M2-HW: {overall}\nbundle: {_bundle}\ncopy that whole folder back; nothing has been committed and no record has been changed.");
        return overall == "ERROR" ? 1 : 0;
    }

    // ------------------------------------------------------------------ subscriptions

    private void SubscribeHost(CozmoEngine e, string engineLabel)
    {
        e.EngineStateChanged += t => Host("state", new JsonObject { ["engine"] = engineLabel, ["old"] = (int)t.OldState, ["new"] = (int)t.NewState, ["isReady"] = e.IsReady, ["isReadyDelivered"] = e.IsReadyDelivered });
        e.EngineLoading += p => Host("progress", new JsonObject { ["engine"] = engineLabel, ["ratio"] = Num(p.Ratio), ["bits"] = $"0x{BitConverter.SingleToInt32Bits(p.Ratio):X8}" });
        e.EngineStartupFailed += f => Host("startup-failed", new JsonObject { ["engine"] = engineLabel, ["reason"] = f.Reason });
        e.AnimationAvailable += a => Host("animation", new JsonObject { ["engine"] = engineLabel, ["name"] = Encoding.UTF8.GetString(a.Name.Span) });
        e.AnimationAnnouncementsEnded += m => Host("animations-end", new JsonObject { ["engine"] = engineLabel, ["value"] = (int)m.Value });
        e.ConnectionResponse += r => Host("connection-response", new JsonObject { ["engine"] = engineLabel, ["result"] = r.Result.ToString(), ["fw"] = r.FwVersion });
        e.BlockPoolData += b => Host("block-pool", new JsonObject { ["engine"] = engineLabel, ["enabled"] = b.Enabled, ["blocks"] = b.Blocks.Count });
        e.Latency += l => Host("latency", new JsonObject { ["engine"] = engineLabel, ["fields"] = LatencyJson(l) });
        e.RobotPoked += _ => Host("robot-poked", new JsonObject { ["engine"] = engineLabel });
        e.RobotDisconnected += m => Host("robot-disconnected", new JsonObject { ["engine"] = engineLabel, ["timeSinceLastMsgSec"] = m.TimeSinceLastMsgSec });
        e.RobotErrorPassThrough += m => Host("robot-error", new JsonObject { ["engine"] = engineLabel, ["code"] = m.Code });
        EngineEvidence.Subscribe(e, l => _log.Enqueue((Now(), l)), (kind, value) => Event(kind, value));
        if (engineLabel == "main") e.ConnectionResponse += r => _response = r;
    }

    private void SubscribeRobot(CozmoRobot robot)
    {
        robot.Transport.FrameTrace += f => _frames.Enqueue(new FrameRec(T(f.Utc), f.Utc, f.Outbound, f.Raw, f.Frame, f.Error));
        robot.Transport.Warning += w => Event("transport-warning", new JsonObject { ["text"] = w });
        robot.Transport.Disconnected += r => Event("transport-disconnected", new JsonObject { ["reason"] = r });
        robot.Transport.Connected += () => Event("transport-connected", new JsonObject());
        robot.HandlerFaulted += ex => Event("handler-fault", new JsonObject { ["exception"] = ex.ToString() });
    }

    private static JsonObject LatencyJson(LatencyMessage l)
    {
        static JsonArray Tri(LatencyTimingInfo t) => new(F(t.Average), F(t.Minimum), F(t.Maximum));
        return new JsonObject
        {
            ["wifi"] = Tri(l.Wifi), ["extSend"] = Tri(l.ExternalSend), ["send"] = Tri(l.Send), ["recvQueue"] = Tri(l.ReceiveQueue),
            ["unity"] = Tri(l.Unity), ["sdk"] = Tri(l.Sdk),
            ["image"] = new JsonArray(F(l.Image.Average), F(l.Image.Minimum), F(l.Image.Maximum), F(l.Image.CurrentTimeMs)),
        };
    }
    private static JsonNode F(float f) => float.IsFinite(f) ? JsonValue.Create(f)! : JsonValue.Create(float.IsPositiveInfinity(f) ? "+inf" : float.IsNegativeInfinity(f) ? "-inf" : "nan")!;

    // ------------------------------------------------------------------ check plumbing

    private void Window(string id, double from, Func<JsonObject> body)
    {
        if (_cancelled) { Skip(id, "run interrupted before this check"); return; }
        Console.WriteLine($"\n=== {id}");
        JsonObject res;
        try { res = body(); }
        catch (Exception e)
        {
            lock (_exceptions) _exceptions.Add($"{id}: {e}");
            res = new JsonObject { ["verdict"] = "INCONCLUSIVE", ["verdictRule"] = "the check threw; see exceptions", ["missing"] = new JsonArray(e.Message) };
        }
        lock (_windows) _windows.Add((id, from, Now()));
        if (id == "WIRE_TAP") return; // a phase of WIRE, reported there
        Add(id, res, from);
    }

    private void Skip(string id, string why) => Add(id, new JsonObject { ["verdict"] = "INCONCLUSIVE", ["verdictRule"] = "check not run", ["missing"] = new JsonArray(why) }, Now());

    private void Add(string id, JsonObject res, double from)
    {
        var def = M1M2Hw.Checks.FirstOrDefault(c => c.Id == id);
        res["id"] = id; res["title"] = def?.Title;
        res["records"] = new JsonArray((def?.Records ?? Array.Empty<string>()).Select(r => (JsonNode)r).ToArray());
        res["startMs"] = Num(from); res["endMs"] = Num(Now());
        res["settlesNoRecord"] = "an observation, not a verdict on a record: nothing here changes the manifest or raises provenance";
        lock (_results) _results.Add(res);
        Console.WriteLine($"--- {id}: {res["verdict"]}  ({res["verdictRule"]})");
    }

    private HostEvent[] HostEvents(string engine) => _host.ToArray().Where(h => h.Data["engine"]?.GetValue<string>() == engine).ToArray();

    // ------------------------------------------------------------------ STARTUP

    private JsonObject StartupResult(double from, bool settled, string engine)
    {
        var ev = HostEvents(engine);
        var states = ev.Where(h => h.Kind == "state").Select(h => $"{h.Data["old"]}->{h.Data["new"]}").ToList();
        var progress = ev.Where(h => h.Kind == "progress").Select(h => h.Data["ratio"]!.GetValue<double>()).ToList();
        var names = ev.Where(h => h.Kind == "animation").Select(h => h.Data["name"]!.GetValue<string>()).ToList();
        int idxEnd = Array.FindIndex(ev, h => h.Kind == "animations-end");
        int idxReady = Array.FindIndex(ev, h => h.Kind == "state" && h.Data["new"]!.GetValue<int>() == 3);
        int lastName = Array.FindLastIndex(ev, h => h.Kind == "animation");
        bool failed = ev.Any(h => h.Kind == "startup-failed");
        bool monotonic = progress.Zip(progress.Skip(1)).All(p => p.Second >= p.First);
        var problems = new List<string>();
        if (failed) problems.Add("EngineStartupFailed: " + ev.First(h => h.Kind == "startup-failed").Data["reason"]);
        if (!states.SequenceEqual(new[] { "0->1", "1->2", "2->3" })) problems.Add("state sequence " + string.Join(",", states) + " is not 0->1,1->2,2->3");
        if (names.Count != M1M2Hw.ShippedAnimationNames) problems.Add($"{names.Count} animation names, expected {M1M2Hw.ShippedAnimationNames} for the shipped tree");
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Count) problems.Add("duplicate animation names");
        if (names.Contains("_PROCEDURAL_")) problems.Add("_PROCEDURAL_ was announced");
        if (!(lastName < idxEnd && idxEnd < idxReady)) problems.Add("order is not names, then EndOfMessage, then 2->3");
        if (!monotonic) problems.Add("progress decreased");
        if (progress.Count == 0 || progress[^1] != 1.0) problems.Add("the last progress value is not 1.0");
        bool delivered = engine == "main" ? _robot!.Engine.IsReadyDelivered : true;
        if (!delivered) problems.Add("IsReadyDelivered is false at the end of the startup window");
        return new JsonObject
        {
            ["verdict"] = !settled ? "INCONCLUSIVE" : problems.Count == 0 ? "OBSERVED_STARTUP_AS_SOURCE" : "OBSERVED_STARTUP_DIFFERS",
            ["verdictRule"] = "0->1,1->2,2->3 in that order; 997 distinct names without _PROCEDURAL_; names, then EndOfMessage, then 2->3; progress non-decreasing ending at 1.0; delivered readiness",
            ["missing"] = settled ? new JsonArray() : new JsonArray($"neither ready nor failed within {M1M2Hw.StartupBudgetMs} ms"),
            ["problems"] = new JsonArray(problems.Select(p => (JsonNode)p).ToArray()),
            ["states"] = new JsonArray(states.Select(s => (JsonNode)s).ToArray()),
            ["startupMs"] = Num(Now() - from),
            ["progressCount"] = progress.Count, ["progressFirst"] = progress.Count > 0 ? Num(progress[0]) : null, ["progressLast"] = progress.Count > 0 ? Num(progress[^1]) : null,
            ["progressValues"] = new JsonArray(progress.Distinct().Take(64).Select(p => Num(p)).ToArray()),
            ["animationNames"] = names.Count, ["firstNames"] = new JsonArray(names.Take(5).Select(n => (JsonNode)n).ToArray()), ["lastNames"] = new JsonArray(names.TakeLast(3).Select(n => (JsonNode)n).ToArray()),
            ["endOfMessageValue"] = idxEnd >= 0 ? ev[idxEnd].Data["value"]!.DeepClone() : null,
        };
    }

    // ------------------------------------------------------------------ CONNECT

    private (bool, JsonObject) ConnectCheck()
    {
        var robot = _robot!;
        if (!robot.Engine.IsReadyDelivered) return (false, new JsonObject { ["verdict"] = "INCONCLUSIVE", ["verdictRule"] = "not connected", ["missing"] = new JsonArray("startup did not reach delivered readiness") });
        double call = Now();
        robot.ConnectToRobot(_ip); // what ConnectAsync does once readiness is delivered (M2-021)
        bool ok = Poll(() => _response is { Result: not RobotConnectionResult.Success } || _response is not null && robot.Engine.Robot is { TimeSynced: true, FirstFullStateHandled: true, ReadyToStream: true }, M1M2Hw.ConnectBudgetMs);
        bool success = ok && _response?.Result == RobotConnectionResult.Success;
        if (success) Wait(M1M2Hw.SettleMs);
        var ev = HostEvents("main");
        int iResp = Array.FindIndex(ev, h => h.Kind == "connection-response");
        int iPool = Array.FindIndex(ev, h => h.Kind == "block-pool");
        lock (_robotInfo)
        {
            _robotInfo["connectResult"] = _response?.Result.ToString(); _robotInfo["fwVersion"] = _response?.FwVersion;
            _robotInfo["serial"] = _response is null ? null : $"0x{_response.SerialNumber:x8}"; _robotInfo["bodyHwVersion"] = _response?.BodyHWVersion;
            _robotInfo["firmwareJson"] = robot.State.Firmware?.SignatureJson;
        }
        var problems = new List<string>();
        if (!success) problems.Add("no Success with time sync, first full state and ready to stream within the budget");
        if (iPool < 0) problems.Add("no BlockPoolData delivered");
        else if (iPool < iResp) problems.Add("BlockPoolData before the ConnectionResponse");
        if (ev.Count(h => h.Kind == "block-pool") > 1) problems.Add("more than one BlockPoolData after one connection");
        var outAfter = Wire().Where(m => m.Out && m.T >= call && m.T <= call + M1M2Hw.ConnectBudgetMs).Select(m => TagName(m.Tag)).ToList();
        Console.WriteLine($"connect: {_response?.Result.ToString() ?? "no response"}; firmware {_response?.FwVersion}");
        return (success, new JsonObject
        {
            ["verdict"] = _response is null ? "INCONCLUSIVE" : problems.Count == 0 ? "OBSERVED_CONNECT_AS_SOURCE" : "OBSERVED_CONNECT_DIFFERS",
            ["verdictRule"] = "Success, time synced, first full state, ready to stream; exactly one BlockPoolData, after the ConnectionResponse (M1-042)",
            ["missing"] = _response is null ? new JsonArray("no RobotConnectionResponse") : new JsonArray(),
            ["problems"] = new JsonArray(problems.Select(p => (JsonNode)p).ToArray()),
            ["connectedAfterMs"] = Num(Now() - call),
            ["firstOutboundMessages"] = new JsonArray(outAfter.Take(40).Select(s => (JsonNode)s).ToArray()),
            ["blockPool"] = iPool >= 0 ? ev[iPool].Data.DeepClone() : null,
        });
    }

    // ------------------------------------------------------------------ LATENCY

    private JsonObject LatencyCheck()
    {
        var engine = _robot!.Engine;
        double from = Now();
        Wait(M1M2Hw.LatencyWindowMs);
        double mid = Now();
        engine.SetGameBeingPaused(true);
        Wait(M1M2Hw.PausedWindowMs);
        double resume = Now();
        engine.SetGameBeingPaused(false);
        Wait(M1M2Hw.LatencyWindowMs / 2);
        double to = Now();
        var lat = HostEvents("main").Where(h => h.Kind == "latency").ToList();
        var running = lat.Where(h => h.T >= from && h.T < mid).ToList();
        // allow one in-flight report around each pause edge (the app input is applied at the next tick's game-message point)
        var paused = lat.Where(h => h.T >= mid + 200 && h.T < resume).ToList();
        var after = lat.Where(h => h.T >= resume && h.T < to).ToList();
        var gaps = running.Zip(running.Skip(1), (a, b) => b.T - a.T).OrderBy(g => g).ToList();
        double median = gaps.Count > 0 ? gaps[gaps.Count / 2] : double.NaN;
        var problems = new List<string>();
        if (running.Count < 5) problems.Add($"only {running.Count} LatencyMessages in {M1M2Hw.LatencyWindowMs} ms");
        if (!double.IsNaN(median) && Math.Abs(median - M1M2Hw.ExpectedLatencyPeriodMs) > 120) problems.Add($"median period {median:F0} ms, expected about {M1M2Hw.ExpectedLatencyPeriodMs} ms");
        if (paused.Count > 0) problems.Add($"{paused.Count} LatencyMessages while paused");
        if (after.Count == 0) problems.Add("none after resuming");
        // Compare values, not serialized JSON (System.Text.Json escapes the '+' of "+inf").
        static string V(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? str) ? str! : double.Parse(n!.ToJsonString(), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        static bool Empty(JsonArray a) => V(a[0]) == "0" && V(a[1]) == "+inf" && V(a[2]) == "-inf";
        foreach (var h in running)
        {
            var f = h.Data["fields"]!.AsObject();
            foreach (var k in new[] { "wifi", "extSend", "send" })
                if (f[k]!.AsArray().Any(v => V(v) != "0")) problems.Add($"{k} not all 0");
            foreach (var k in new[] { "unity", "sdk" })
                if (!Empty(f[k]!.AsArray())) problems.Add($"{k} is not (0,+inf,-inf)");
            if (!Empty(f["image"]!.AsArray())) problems.Add("image avg/min/max not empty");
        }
        problems = problems.Distinct().ToList();
        return new JsonObject
        {
            ["verdict"] = running.Count == 0 ? "INCONCLUSIVE" : problems.Count == 0 ? "OBSERVED_LATENCY_AS_SOURCE" : "OBSERVED_LATENCY_DIFFERS",
            ["verdictRule"] = $"median period within 120 ms of {M1M2Hw.ExpectedLatencyPeriodMs} ms; wifi/extSend/send all 0; unity/sdk and image avg/min/max = (0,+inf,-inf); none from 200 ms after SetGameBeingPaused(true) until resume; some after resume",
            ["missing"] = running.Count == 0 ? new JsonArray("no LatencyMessage delivered") : new JsonArray(),
            ["problems"] = new JsonArray(problems.Select(p => (JsonNode)p).ToArray()),
            ["countRunning"] = running.Count, ["countPaused"] = paused.Count, ["countAfterResume"] = after.Count,
            ["medianPeriodMs"] = Num(median), ["minPeriodMs"] = gaps.Count > 0 ? Num(gaps[0]) : null, ["maxPeriodMs"] = gaps.Count > 0 ? Num(gaps[^1]) : null,
            ["samples"] = new JsonArray(running.Take(6).Select(h => (JsonNode)h.Data["fields"]!.DeepClone()).ToArray()),
            ["pauseAtMs"] = Num(mid), ["resumeAtMs"] = Num(resume),
        };
    }

    private JsonObject TapWindow()
    {
        if (_yes) { Event("operator", new JsonObject { ["prompt"] = "tap", ["answer"] = null }); Wait(2000); return new JsonObject(); }
        Console.Write($"Tap the top of Cozmo's head once now, then press Enter (blank Enter if you could not): ");
        double t = Now();
        string? a = ReadLineTimeout(M1M2Hw.TapWindowMs * 3);
        Event("operator", new JsonObject { ["prompt"] = "tap", ["answer"] = a, ["atMs"] = Num(t) });
        Wait(1500);
        return new JsonObject();
    }

    private static string? ReadLineTimeout(int ms)
    {
        var task = Task.Run(Console.ReadLine);
        return task.Wait(ms) ? task.Result : null;
    }

    // ------------------------------------------------------------------ DISCONNECT

    private JsonObject DisconnectCheck()
    {
        _disposeCall = Now();
        StopBounded();
        _disposeReturn = Now();
        Wait(M1M2Hw.DisconnectWatchMs);
        var frames = Frames();
        var discOut = frames.Where(f => f.Out && f.T >= _disposeCall && f.Frame is { } fr && (fr.Type == ReliableMessageType.DisconnectRequest || fr.Messages.Any(m => m.Type == ReliableMessageType.DisconnectRequest))).ToList();
        var lastIn = frames.Where(f => !f.Out).Select(f => f.T).DefaultIfEmpty(double.NaN).Max();
        var outBefore = Wire().Where(m => m.Out && m.T >= _disposeCall && m.T <= _disposeReturn).Select(m => TagName(m.Tag)).ToList();
        bool bounded = _disposeReturn - _disposeCall < 5000;
        var problems = new List<string>();
        if (discOut.Count == 0) problems.Add("no outbound DisconnectRequest after Dispose");
        if (!bounded) problems.Add("Dispose took 5 s or more");
        return new JsonObject
        {
            ["verdict"] = problems.Count == 0 ? "OBSERVED_DISCONNECT_REQUEST_SENT" : "OBSERVED_DISCONNECT_DIFFERS",
            ["verdictRule"] = "an outbound DisconnectRequest frame after Dispose; Dispose returns within 5 s",
            ["problems"] = new JsonArray(problems.Select(p => (JsonNode)p).ToArray()),
            ["disposeMs"] = Num(_disposeReturn - _disposeCall),
            ["disconnectRequestAfterMs"] = discOut.Count > 0 ? Num(discOut[0].T - _disposeCall) : null,
            ["lastInboundAfterDisposeMs"] = double.IsNaN(lastIn) ? null : Num(lastIn - _disposeCall),
            ["messagesSentDuringDispose"] = new JsonArray(outBefore.Select(s => (JsonNode)s).ToArray()),
            ["note"] = "the stack sends StopAllMotors and DriveWheels(0) before the DisconnectRequest (PROJECT_STATE decision note 'Motor stop on shutdown'); the original sends only the DisconnectRequest",
        };
    }

    // ------------------------------------------------------------------ NEEDS_READ

    private JsonObject NeedsCheck()
    {
        var wire = Wire();
        var cmds = wire.Where(m => m.Out && Parse(m.Payload) is NVCommand n && n.Tag == M1M2Hw.NeedsTag).Select(m => (m, n: (NVCommand)Parse(m.Payload)!)).ToList();
        var results = wire.Where(m => !m.Out && Parse(m.Payload) is NVOpResult r && r.Tag == M1M2Hw.NeedsTag).Select(m => (m, r: (NVOpResult)Parse(m.Payload)!)).ToList();
        int reads = cmds.Count(c => c.n.Op == NvStorageComponent.OpRead);
        int writes = cmds.Count(c => c.n.Op is NvStorageComponent.OpWrite or NvStorageComponent.OpErase or NvStorageComponent.OpWipeAll);
        var blob = results.Where(x => x.r.Op == NvStorageComponent.OpRead).SelectMany(x => x.r.Data).ToArray();
        string? blobFile = null, blobSha = null;
        if (blob.Length > 0)
        {
            Directory.CreateDirectory(Path.Combine(_bundle, "needs"));
            blobFile = "needs/nv-0x194000.bin";
            File.WriteAllBytes(Path.Combine(_bundle, blobFile), blob);
            blobSha = Convert.ToHexString(SHA256.HashData(blob)).ToLowerInvariant();
            _extraFiles.Add(blobFile);
        }
        bool missingSeen = _log.ToArray().Any(l => l.Line.Contains("M15-030", StringComparison.Ordinal));
        var problems = new List<string>();
        // M3-033: 0x194000 is read twice per connection: once in the RobotDataBackupManager sweep (reads #5..#12, 0x51AD0A)
        // and once by the needs manager in the mfgId lambda (0x6944CC). (Corrected after the first run, 20261018-170546.)
        if (reads % 2 != 0 || reads == 0) problems.Add($"{reads} READs of 0x194000, expected 2 per connection (M3-033: backup sweep, then the needs read)");
        if (writes != 0) problems.Add($"{writes} WRITE/ERASE/WIPE commands touching 0x194000: the B-M1-10 gate failed");
        if (!missingSeen) problems.Add("the needs-persistence MISSING (naming M15-030) was not seen in the log");
        return new JsonObject
        {
            ["verdict"] = cmds.Count == 0 ? "INCONCLUSIVE" : problems.Count == 0 ? "OBSERVED_ONE_READ_NO_WRITE" : "OBSERVED_NEEDS_DIFFERS",
            ["verdictRule"] = "two outbound READs of NV 0x194000 per connection (backup sweep and needs read, M3-033) and no WRITE/ERASE/WIPE of it over the whole run; the gate's MISSING logged",
            ["missing"] = cmds.Count == 0 ? new JsonArray("no NVCommand for 0x194000 was sent (did the connection get past the mfgId handler?)") : new JsonArray(),
            ["problems"] = new JsonArray(problems.Select(p => (JsonNode)p).ToArray()),
            ["commands"] = new JsonArray(cmds.Select(c => (JsonNode)new JsonObject { ["t"] = Num(c.m.T), ["op"] = c.n.Op, ["length"] = c.n.Length, ["dataBytes"] = c.n.Data.Length }).ToArray()),
            ["results"] = new JsonArray(results.Select(x => (JsonNode)new JsonObject { ["t"] = Num(x.m.T), ["op"] = x.r.Op, ["result"] = x.r.Result, ["indexField"] = x.r.Length, ["dataBytes"] = x.r.Data.Length }).ToArray()),
            ["blobBytes"] = blob.Length, ["blobFile"] = blobFile, ["blobSha256"] = blobSha,
            ["blobHeadHex"] = blob.Length > 0 ? Convert.ToHexString(blob, 0, Math.Min(64, blob.Length)) : null,
            ["note"] = "read-only: the blob is the robot's own needs record (expected from the official app), kept to check the hand-encoded v5 fixture (B-M1-10 check A4)",
        };
    }

    // ------------------------------------------------------------------ WIRE (M1-033)

    private JsonObject WireCheck()
    {
        var frames = Frames().Where(f => f.Frame is not null).ToList();
        JsonObject Side(bool outbound)
        {
            var fs = frames.Where(f => f.Out == outbound).ToList();
            var byType = Histogram(fs.Select(f => f.Frame!.Type.ToString()));
            var packed = fs.Where(f => f.Frame!.Messages.Count > 1).ToList();
            var seqs = fs.SelectMany(f => f.Frame!.Messages.Where(m => m.IsReliable).Select(m => (int)m.Seq)).ToList();
            var reliable = fs.SelectMany(f => f.Frame!.Messages.Where(m => m.IsReliable).Select(m => m.Seq)).ToList();
            int resends = reliable.Count - reliable.Distinct().Count();
            var sizes = fs.Select(f => f.Raw.Length).OrderBy(x => x).ToList();
            var biggest = fs.OrderByDescending(f => f.Raw.Length).FirstOrDefault();
            return new JsonObject
            {
                ["frames"] = fs.Count, ["byType"] = byType,
                ["maxFrameBytes"] = sizes.Count > 0 ? sizes[^1] : 0, ["p99FrameBytes"] = sizes.Count > 0 ? sizes[(int)(sizes.Count * 0.99)] : 0,
                ["framesOver1000Bytes"] = sizes.Count(s => s > 1000),
                ["biggestFrame"] = biggest is null ? null : new JsonObject { ["t"] = Num(biggest.T), ["bytes"] = biggest.Raw.Length, ["type"] = biggest.Frame!.Type.ToString(), ["subs"] = biggest.Frame.Messages.Count },
                ["packedFrames"] = packed.Count, ["maxSubsInAFrame"] = fs.Count > 0 ? fs.Max(f => f.Frame!.Messages.Count) : 0,
                ["packedSubTypeMix"] = Histogram(packed.SelectMany(f => f.Frame!.Messages.Select(m => m.Type.ToString()))),
                ["reliableSeqMin"] = seqs.Count > 0 ? seqs.Min() : null, ["reliableSeqMax"] = seqs.Count > 0 ? seqs.Max() : null,
                ["reliableMessages"] = reliable.Count, ["resentReliableMessages"] = resends,
                ["frameErrors"] = Frames().Count(f => f.Out == outbound && f.Error is not null),
            };
        }
        var inbound = Wire().Where(m => !m.Out).ToList();
        var watched = inbound.Where(m => M1M2Hw.WatchedTags.Contains(m.Tag)).ToList();
        var unknown = inbound.Where(m => !MessageCatalog.ById.ContainsKey((RobotMessageId)m.Tag)).Select(m => $"0x{m.Tag:X2}").ToList();
        var pokes = _host.ToArray().Count(h => h.Kind == "robot-poked" && h.Data["engine"]?.GetValue<string>() == "main");
        var tap = _events.ToArray().LastOrDefault(e => e.Kind == "operator" && e.Data["prompt"]?.GetValue<string>() == "tap");
        bool wrapped = frames.SelectMany(f => f.Frame!.Messages.Where(m => m.IsReliable).Select(m => (int)m.Seq)).DefaultIfEmpty(0).Max() >= 65000;
        var notes = new JsonArray(
            wrapped ? "a reliable sequence id reached 65000+: wraparound may have been exercised, see frames.jsonl" : "sequence wraparound not exercised (it needs about 65534 reliable messages)",
            "maximum-size frames are exercised only as far as maxFrameBytes shows (bound 1406, M1-006/M1-012)");
        return new JsonObject
        {
            ["verdict"] = frames.Count == 0 ? "INCONCLUSIVE" : "OBSERVED_LINK",
            ["verdictRule"] = "an observation for M1-033: frame sizes, types, packing, sequence range and resends in each direction; watched robot-message tags and RobotPoked deliveries",
            ["missing"] = frames.Count == 0 ? new JsonArray("no frames recorded") : new JsonArray(),
            ["outbound"] = Side(true), ["inbound"] = Side(false),
            ["watchedArrivals"] = Histogram(watched.Select(m => $"0x{m.Tag:X2} {TagName(m.Tag)}")),
            ["watchedFirst"] = new JsonArray(watched.Take(20).Select(m => (JsonNode)new JsonObject { ["t"] = Num(m.T), ["tag"] = $"0x{m.Tag:X2}", ["hex"] = Convert.ToHexString(m.Payload, 0, Math.Min(m.Payload.Length, 64)) }).ToArray()),
            ["robotPokedDelivered"] = pokes,
            ["tapAnswer"] = tap.Data?["answer"]?.DeepClone(), ["tapAtMs"] = tap.Data?["atMs"]?.DeepClone(),
            ["unknownInboundTags"] = Histogram(unknown),
            ["notExercised"] = notes,
        };
    }

    // ------------------------------------------------------------------ CONNECT_ASYNC

    private JsonObject ConnectAsyncCheck()
    {
        Console.WriteLine("connecting a second engine through CozmoRobot.ConnectAsync ...");
        double from = Now();
        bool readyBeforeConnect = false;
        using var cts = new CancellationTokenSource(M1M2Hw.StartupBudgetMs + M1M2Hw.ConnectBudgetMs);
        CozmoRobot? r2 = null; string? error = null;
        try
        {
            r2 = CozmoRobot.ConnectAsync(_ip, engineOptions: new CozmoEngineOptions
            {
                ResourcesPath = _resources,
                BeforeStart = e =>
                {
                    SubscribeHost(e, "async");
                    e.ConnectionResponse += _ => readyBeforeConnect = e.IsReadyDelivered;
                },
            }, cancellationToken: cts.Token).GetAwaiter().GetResult();
        }
        catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
        double connected = Now();
        if (r2 is not null) Wait(M1M2Hw.SettleMs);
        var startup = StartupResult(from, r2 is not null, "async");
        var ev = HostEvents("async");
        double t0 = Now();
        try { if (r2 is not null) { var t = new Thread(() => r2.Dispose()) { IsBackground = true }; t.Start(); if (!t.Join(5000)) error ??= "Dispose did not return within 5 s"; } }
        catch (Exception e) { error ??= "dispose: " + e.Message; }
        var problems = new List<string>();
        if (r2 is null) problems.Add("ConnectAsync failed: " + error);
        if (r2 is not null && !readyBeforeConnect) problems.Add("the ConnectionResponse arrived before delivered readiness");
        if (startup["problems"]!.AsArray().Count > 0) problems.Add("startup through BeforeStart differs: " + string.Join("; ", startup["problems"]!.AsArray().Select(p => p!.GetValue<string>())));
        if (!ev.Any(h => h.Kind == "block-pool")) problems.Add("no BlockPoolData on the second connection");
        return new JsonObject
        {
            ["verdict"] = r2 is null ? "OBSERVED_CONNECT_ASYNC_FAILED" : problems.Count == 0 ? "OBSERVED_CONNECT_ASYNC_AS_SOURCE" : "OBSERVED_CONNECT_ASYNC_DIFFERS",
            ["verdictRule"] = "ConnectAsync returns Success; readiness was delivered before the connection response; the BeforeStart observers saw the full startup; BlockPoolData delivered; Dispose within 5 s",
            ["problems"] = new JsonArray(problems.Select(p => (JsonNode)p).ToArray()),
            ["error"] = error, ["connectAsyncMs"] = Num(connected - from), ["disposeMs"] = Num(Now() - t0),
            ["startup"] = startup,
            ["note"] = "frames of this second connection are not traced (the engine does not expose its transport to BeforeStart); wire evidence comes from the first connection",
        };
    }

    // ------------------------------------------------------------------ wire views

    private FrameRec[] Frames() => _frames.ToArray();

    private List<Msg> Wire()
    {
        var list = new List<Msg>();
        var seenOut = new HashSet<ushort>(); var seenIn = new HashSet<ushort>();
        foreach (var f in Frames().Where(f => f.Frame is not null).OrderBy(f => f.T))
            foreach (var m in f.Frame!.Messages)
            {
                var seen = f.Out ? seenOut : seenIn;
                if (m.Type == (f.Out ? ReliableMessageType.ConnectionRequest : ReliableMessageType.ConnectionResponse)) { seen.Clear(); continue; }
                if (m.Type is not (ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage) || m.Payload.Length == 0) continue;
                if (m.IsReliable && !seen.Add(m.Seq)) continue;
                list.Add(new Msg(f.T, f.Out, m.Payload[0], m.Payload));
            }
        return list;
    }

    private static RobotMessage? Parse(byte[] payload) { try { return RobotMessage.Parse(payload); } catch { return null; } }
    private static string TagName(byte tag) => MessageCatalog.ById.TryGetValue((RobotMessageId)tag, out var info) ? info.Member : $"0x{tag:X2}";
    private static JsonObject Histogram(IEnumerable<string> keys)
    {
        var o = new JsonObject();
        foreach (var g in keys.GroupBy(k => k).OrderBy(g => g.Key, StringComparer.Ordinal)) o[g.Key] = g.Count();
        return o;
    }

    private void StopBounded()
    {
        var r = _robot; if (r is null) return;
        var t = new Thread(() => { try { r.Dispose(); } catch (Exception e) { lock (_exceptions) _exceptions.Add("dispose: " + e); } }) { IsBackground = true };
        t.Start();
        if (!t.Join(5000)) lock (_exceptions) _exceptions.Add("Dispose did not return within 5000 ms");
    }

    // ------------------------------------------------------------------ bundle

    private string WriteBundle()
    {
        if (Interlocked.Exchange(ref _written, 1) == 1) return "ERROR";
        Directory.CreateDirectory(_bundle);
        var framesOut = new StringBuilder();
        foreach (var f in Frames().OrderBy(f => f.T))
        {
            var o = new JsonObject { ["t"] = Num(f.T), ["utc"] = f.Utc.ToString("o", CultureInfo.InvariantCulture), ["dir"] = f.Out ? "out" : "in", ["len"] = f.Raw.Length };
            if (f.Frame is { } fr)
            {
                o["type"] = (byte)fr.Type; o["typeName"] = fr.Type.ToString(); o["seqMin"] = fr.SeqMin; o["seqMax"] = fr.SeqMax; o["ack"] = fr.Ack;
                var subs = new JsonArray();
                foreach (var m in fr.Messages)
                {
                    var s = new JsonObject { ["type"] = (byte)m.Type, ["name"] = m.Type.ToString(), ["seq"] = m.Seq, ["size"] = m.Payload.Length };
                    if (m.Type is ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage && m.Payload.Length > 0) { s["tag"] = $"0x{m.Payload[0]:X2}"; s["msg"] = TagName(m.Payload[0]); }
                    subs.Add(s);
                }
                o["subs"] = subs;
            }
            if (f.Error is not null) o["error"] = f.Error;
            o["hex"] = Convert.ToHexString(f.Raw);
            framesOut.AppendLine(o.ToJsonString());
        }
        File.WriteAllText(Path.Combine(_bundle, "frames.jsonl"), framesOut.ToString(), Encoding.UTF8);
        var ev = new StringBuilder();
        foreach (var e in _events.ToArray().OrderBy(e => e.T)) ev.AppendLine(new JsonObject { ["t"] = Num(e.T), ["kind"] = e.Kind, ["data"] = e.Data.DeepClone() }.ToJsonString());
        File.WriteAllText(Path.Combine(_bundle, "events.jsonl"), ev.ToString(), Encoding.UTF8);
        var host = new StringBuilder();
        foreach (var h in _host.ToArray()) host.AppendLine(new JsonObject { ["t"] = Num(h.T), ["kind"] = h.Kind, ["data"] = h.Data.DeepClone() }.ToJsonString());
        File.WriteAllText(Path.Combine(_bundle, "host-events.jsonl"), host.ToString(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(_bundle, "records.json"), new JsonArray(_records.Select(r => (JsonNode)r.DeepClone()).ToArray()).ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        JsonObject[] checks; lock (_results) checks = _results.ToArray();
        string overall = _exceptions.Count > 0 && checks.Length == 0 ? "ERROR"
            : checks.Any(c => c["verdict"]!.GetValue<string>() is var v && (v == "INCONCLUSIVE" || v.Contains("DIFFERS") || v.Contains("FAILED"))) ? "REVIEW_REQUIRED_SOME_NOT_AS_SOURCE" : "REVIEW_REQUIRED";
        var result = new JsonObject
        {
            ["test"] = M1M2Hw.TestId, ["overall"] = overall,
            ["overallRule"] = "never PASS: every check is OBSERVED_* or INCONCLUSIVE and the manager judges the bundle",
            ["operatorLabel"] = _operator,
            ["records"] = new JsonArray(_records.Select(r => (JsonNode)new JsonObject { ["id"] = r["id"]!.DeepClone(), ["status"] = r["status"]!.DeepClone(), ["title"] = r["title"]!.DeepClone() }).ToArray()),
            ["robot"] = _robotInfo.DeepClone(),
            ["checks"] = new JsonArray(checks.Select(c => (JsonNode)c.DeepClone()).ToArray()),
            ["warnings"] = new JsonArray(_warnings.Select(w => (JsonNode)w).ToArray()),
            ["exceptions"] = new JsonArray(_exceptions.Select(x => (JsonNode)x).ToArray()),
            ["windowsMs"] = new JsonArray(_windows.Select(w => (JsonNode)new JsonObject { ["name"] = w.Name, ["fromMs"] = Num(w.From), ["toMs"] = Num(w.To) }).ToArray()),
        };
        File.WriteAllText(Path.Combine(_bundle, "result.json"), result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        File.WriteAllText(Path.Combine(_bundle, "env.json"), Env().ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        try
        {
            if (_repoRoot is not null)
                foreach (var (src, name) in new[] { (ToolPath(_repoRoot), "M1M2Hw.cs"), (DocPath(_repoRoot), "m1m2-hw-draft.md") })
                    if (File.Exists(src)) { File.Copy(src, Path.Combine(_bundle, name), overwrite: true); _extraFiles.Add(name); }
        }
        catch (Exception e) { lock (_exceptions) _exceptions.Add("copying the script: " + e.Message); }
        var hashes = new JsonObject();
        foreach (var f in new[] { "result.json", "env.json", "records.json", "events.jsonl", "host-events.jsonl", "frames.jsonl" }.Concat(_extraFiles).Distinct())
        {
            var p = Path.Combine(_bundle, f);
            if (File.Exists(p)) hashes[f.Replace('\\', '/')] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant();
        }
        File.WriteAllText(Path.Combine(_bundle, "hashes.json"), hashes.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        return overall;
    }

    private static string ToolPath(string root) => Path.Combine(root, "cozmo-stack", "src", "Cozmo.Conformance", "M1M2Hw.cs");
    private static string DocPath(string root) => Path.Combine(root, "re-analysis", "acceptance", "hardware", "scripts", "m1m2-hw-draft.md");

    private JsonObject Env()
    {
        string? head = null; string[]? status = null;
        if (_repoRoot is not null)
        {
            head = RunProc("git", $"-C \"{_repoRoot}\" rev-parse HEAD")?.Trim();
            status = RunProc("git", $"-C \"{_repoRoot}\" status --porcelain")?.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        }
        return new JsonObject
        {
            ["test"] = M1M2Hw.TestId, ["commandLine"] = "cozmo-conformance " + string.Join(' ', _argv),
            ["gitHead"] = head, ["gitDirty"] = status?.Length > 0, ["gitStatus"] = status is null ? null : new JsonArray(status.Take(100).Select(l => (JsonNode)l).ToArray()),
            ["scriptSha256"] = new JsonObject
            {
                ["cozmo-stack/src/Cozmo.Conformance/M1M2Hw.cs"] = _repoRoot is null ? null : Sha(ToolPath(_repoRoot)),
                ["re-analysis/acceptance/hardware/scripts/m1m2-hw-draft.md"] = _repoRoot is null ? null : Sha(DocPath(_repoRoot)),
            },
            ["assemblySha256"] = new JsonObject
            {
                ["cozmo-conformance.dll"] = Sha(typeof(M1M2Hw).Assembly.Location), ["Cozmo.Robot.dll"] = Sha(typeof(CozmoRobot).Assembly.Location),
                ["Cozmo.Transport.dll"] = Sha(typeof(ReliableTransport).Assembly.Location), ["Cozmo.Protocol.dll"] = Sha(typeof(Frame).Assembly.Location),
            },
            ["resources"] = _resources, ["robotIp"] = _ip.ToString(), ["robotPort"] = RobotAddress.RemotePort(false), ["yes"] = _yes, ["skipConnectAsync"] = _skipAsync,
            ["os"] = RuntimeInformation.OSDescription, ["dotnet"] = RuntimeInformation.FrameworkDescription,
            ["startUtc"] = _t0Utc.ToString("o", CultureInfo.InvariantCulture), ["endUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            ["timings"] = new JsonObject
            {
                ["startupBudgetMs"] = M1M2Hw.StartupBudgetMs, ["connectBudgetMs"] = M1M2Hw.ConnectBudgetMs, ["settleMs"] = M1M2Hw.SettleMs,
                ["latencyWindowMs"] = M1M2Hw.LatencyWindowMs, ["pausedWindowMs"] = M1M2Hw.PausedWindowMs, ["expectedLatencyPeriodMs"] = M1M2Hw.ExpectedLatencyPeriodMs,
                ["disconnectWatchMs"] = M1M2Hw.DisconnectWatchMs, ["watchdogMs"] = M1M2Hw.WatchdogMs,
            },
        };
    }

    private static string? RunProc(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi); if (p is null) return null;
            var output = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(10000)) { try { p.Kill(); } catch { } return null; }
            return output.Result;
        }
        catch { return null; }
    }

    private static string? Sha(string? path)
    {
        try { return path is null || !File.Exists(path) ? null : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(); } catch { return null; }
    }
}
