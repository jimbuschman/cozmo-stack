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
using StbImageSharp;

namespace Cozmo.Conformance;

/// <summary>
/// <c>m3m4-hw</c>: hardware acceptance run M3M4-HW for M3-device and M4-control: the seven HARDWARE_ONLY records
/// (M3-008, M3-016, M3-036, M3-050, M4-013, M4-021, M4-024) and, since 2026-10-19, the coverage list of settling pass B
/// (M3-012, M3-021, M3-055, M3-060, M4-018, M4-019, M4-027, M4-038, M4-047, M4-051; checks in M3M4HwSettle.cs).
/// Script document: re-analysis/acceptance/hardware/scripts/m3m4-hw-2.md. It connects through the production engine
/// (<see cref="CozmoRobot.Create"/> + ConnectToRobot, as <c>control-check</c> does), runs one observation procedure per
/// record, and writes one self-contained bundle. It never commits, never writes the manifest, and never writes the
/// robot's NV storage.
///
/// Verdict vocabulary: only OBSERVED_* (what was seen, with the rule that produced it) or INCONCLUSIVE (with what is
/// missing). There is no PASS, and no verdict settles a record: a hardware observation never raises provenance
/// (AGENTS.md, "Hardware runs"; "Hardware success does not upgrade provenance").
/// </summary>
public static class M3M4Hw
{
    public const string TestId = "M3M4-HW";

    /// <summary>The HARDWARE_ONLY records; a warning is given if one is no longer HARDWARE_ONLY.</summary>
    public static readonly string[] HardwareOnlyIds = { "M3-008", "M3-016", "M3-036", "M4-013", "M4-021", "M4-024", "M3-050" };
    /// <summary>Every record this run is about. All must exist in re-analysis/fidelity_manifest.json or the run stops before touching the network.</summary>
    public static readonly string[] RecordIds = HardwareOnlyIds.Concat(new[] { "M3-012", "M3-021", "M3-055", "M3-060", "M4-018", "M4-019", "M4-027", "M4-038", "M4-047", "M4-051" }).ToArray();

    public sealed record CheckDef(string Id, string Title, string[] Records, bool Moves, bool NeedsOperator, bool NeedsCube);

    /// <summary>The checks in run order: passive first, motion and long waits last.</summary>
    public static readonly CheckDef[] Checks =
    {
        new("ORIGIN", "Origin id and frame the robot reports in RobotState before and after the connection-time AbsoluteLocalizationUpdate", new[] { "M4-021" }, false, false, false),
        new("NV_READS", "Factory NV reads with Length = 1 (raw replies) and non-factory reads, looking for a Length = size+16 re-request; read-only", new[] { "M3-036" }, false, false, false),
        new("COLOR_IMAGES", "Camera frames with the colour flag off, with EnableColorImages on (and on again with an ImageRequest), then off: encoding, geometry, saved frames", new[] { "M3-016", "M3-050" }, false, true, false),
        new("FACE_ROWS", "Known face patterns that separate pair-bit to display-row mappings, plus face-frame sweeps for the playback period; the operator answers what the face showed", new[] { "M3-008" }, false, true, false),
        new("MOTOR_CAL", "StartMotorCalibration (head) after the connection-time calibration has finished: reports and head motion after the request only", new[] { "M4-013" }, true, true, false),
        new("CUBE_TELEM", "Cube telemetry before and after the cube connects, with and without StreamObjectAccel; what precedes it", new[] { "M4-024" }, false, true, true),
        new("DAS_EVENTS", "The head-ID and body-ID DAS events and the startup flush result event, from the engine log (passive)", new[] { "M3-055", "M3-060" }, false, false, false),
        new("CAMERA_PARAMS", "DefaultCameraParams and the SetCameraParams the engine sends; public SetCameraSettings with a valid and two out-of-range requests, then the original values", new[] { "M3-021" }, false, false, false),
        new("TRACK_UNLOCK", "LockTracks, UnlockTracks, then UnlockTracks again: the miss path's log and EnableAnimTracks re-send", new[] { "M4-047" }, false, false, false),
        new("ANIM_BUDGET", "A long audio animation (no wheel motion): the robot's AnimationState counters beside the engine's streamed totals; budget warnings and drops", new[] { "M3-012" }, true, false, false),
        new("CLIFF", "Cliff threshold sends since connect; with --allow-drive a slow drive to a table edge (RobotStopped/CliffEvent order, API CliffDetected, HandleCliffEvent logs); a hand lift", new[] { "M4-019", "M4-038" }, true, true, false),
        new("CUBE_LIGHTS", "Public PlayLightAnim(Flash) on the connected cube with a completion callback: SetCubeGamma/CubeID/CubeLights on the wire", new[] { "M4-018", "M4-051" }, false, true, true),
        new("DISCONNECT_QUIET", "After the run's teardown: no motor, light or other data message after the DisconnectRequest", new[] { "M4-027" }, false, false, false),
    };

    /// <summary>The order checks run in: passive first, motion and long waits last. DISCONNECT_QUIET is judged after teardown.</summary>
    public static readonly string[] RunOrder = { "DAS_EVENTS", "ORIGIN", "NV_READS", "CAMERA_PARAMS", "COLOR_IMAGES", "FACE_ROWS", "TRACK_UNLOCK", "ANIM_BUDGET", "MOTOR_CAL", "CLIFF", "CUBE_TELEM", "CUBE_LIGHTS" };

    // ---- the fixed timings (not options, so every bundle ran the same script)
    internal const int ConnectBudgetMs = 10000, ConnectedSettleMs = 3000;
    internal const int NvReadTimeoutMs = 8000, NvIdleWaitMs = 10000;
    internal const int ColorPhaseMs = 4000, ColorSavedPerPhase = 2;
    internal const int FacePatternHoldMs = 5000, FaceSweepFrames = 30;
    internal static readonly int[] FaceSweepIntervalsMs = { 34, 67, 133 };
    internal const int CalibrationWaitMs = 15000, CalBaselineMs = 1500, CalWatchMs = 10000;
    internal const int CubeQuietMs = 8000, CubeMoveMs = 8000, CubeAfterOffMs = 5000;
    internal const int WatchdogMs = 20 * 60 * 1000;

    /// <summary>Factory tags whose size-table value is 1 (the read goes out with Length = 1, M3-027): the tags this run reads.</summary>
    internal static readonly uint[] FactoryLen1Tags = { 0x80000001, 0x80000000, 0x80000004, 0x80000006, 0x80000011, 0x80000012 };
    /// <summary>The non-factory entry tags (size-table keys other than the 0x198000 sentinel). Read only.</summary>
    internal static readonly uint[] NonFactoryTags = { 0x180000, 0x181000, 0x182000, 0x183000, 0x184000, 0x194000, 0x195000, 0x196000, 0x197000 };

    /// <summary>
    /// Maps a typed answer to one of the offered choices: case-insensitive equal, the choice's number (1-based), or a unique
    /// prefix. Text that matches none (and a free-number prompt's answer) is kept as typed.
    /// </summary>
    public static string NormaliseAnswer(string typed, string[] choices)
    {
        var t = typed.Trim();
        var eq = choices.FirstOrDefault(c => string.Equals(c, t, StringComparison.OrdinalIgnoreCase));
        if (eq is not null) return eq;
        if (choices.Length > 1 && int.TryParse(t, out int k) && k >= 1 && k <= choices.Length) return choices[k - 1];
        var pre = choices.Where(c => c.StartsWith(t, StringComparison.OrdinalIgnoreCase)).ToList();
        return pre.Count == 1 && t.Length > 0 ? pre[0] : t;
    }


    // ------------------------------------------------------------------ record validation (fail fast)

    /// <summary>
    /// Reads the manifest and returns the seven records. Throws with every missing id named; a record whose status is no longer
    /// HARDWARE_ONLY is returned too and listed in <paramref name="warnings"/> (the run's premise has changed; the manager decides).
    /// </summary>
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
            if (HardwareOnlyIds.Contains(id) && r["status"]?.GetValue<string>() != "HARDWARE_ONLY") warnings.Add($"{id} is {r["status"]} in the manifest, not HARDWARE_ONLY: the manager must re-read the record before this run is used");
            list.Add(r);
        }
        return list;
    }

    /// <summary>Every record id the checks name must be one of <see cref="RecordIds"/>, and every id must be named by a check.</summary>
    public static string? CheckDefinitionProblem()
    {
        var named = Checks.SelectMany(c => c.Records).ToList();
        var stray = named.Except(RecordIds).ToList();
        if (stray.Count > 0) return "a check names a record outside RecordIds: " + string.Join(", ", stray);
        var unnamed = RecordIds.Except(named).ToList();
        return unnamed.Count > 0 ? "a record no check names: " + string.Join(", ", unnamed) : null;
    }

    // ------------------------------------------------------------------ entry point

    private static int Usage(string why)
    {
        if (why.Length > 0) Console.Error.WriteLine("m3m4-hw: " + why);
        Console.Error.WriteLine("usage: m3m4-hw [robot-ip] --resources <cozmo_resources> [--only " + string.Join(",", Checks.Select(c => c.Id)) + "] [--include-lift] [--allow-drive]\n"
                              + "               [--cube-wait <seconds, default 45>] [--photo <KEY>=<path> ...] [--operator <label>] [--out <dir>] [--yes]\n"
                              + "  KEY is a prompt key named in the draft document, e.g. FACE_ROWS.TOP_BAND; --yes asks no questions (answers recorded as null).");
        return 2;
    }

    public static int Run(string[] args)
    {
        IPAddress? ip = null; string? outParent = null, operatorLabel = null; bool yes = false, probe = false, lift = false, drive = false; int cubeWaitS = 45;
        HashSet<string>? only = null; var photos = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--resources": case "--obb": case "--assets":
                    if (++i >= args.Length) return Usage("resource option needs a directory");
                    break; // Retained in argv for EngineStartupResources.
                case "--yes": yes = true; break;
                case "--include-lift": lift = true; break;
                case "--allow-drive": drive = true; break;
                case "--only":
                    if (++i >= args.Length) return Usage("--only needs a list");
                    only = args[i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
                    var unknown = only.Except(Checks.Select(c => c.Id)).ToList();
                    if (unknown.Count > 0) return Usage("unknown check " + string.Join(",", unknown));
                    break;
                case "--cube-wait":
                    if (++i >= args.Length || !int.TryParse(args[i], out cubeWaitS) || cubeWaitS < 5) return Usage("--cube-wait needs seconds >= 5");
                    break;
                case "--photo":
                    if (++i >= args.Length || !args[i].Contains('=')) return Usage("--photo needs KEY=path");
                    var kv = args[i].Split('=', 2); photos[kv[0]] = kv[1]; break;
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

        if (CheckDefinitionProblem() is { } problem) { Console.Error.WriteLine("m3m4-hw: " + problem); return 2; }
        string? repoRoot = LinkCheck.FindRepoRoot();
        if (repoRoot is null && outParent is null) return Usage("the repository root was not found (the manifest is needed to validate the seven records); pass --out and run inside the repository");
        var warnings = new List<string>();
        List<JsonObject> records;
        try
        {
            string manifest = Path.Combine(repoRoot ?? ".", "re-analysis", "fidelity_manifest.json");
            records = LoadRecords(manifest, warnings);
        }
        catch (Exception e) { Console.Error.WriteLine("m3m4-hw: " + e.Message); return 2; }
        foreach (var w in warnings) Console.Error.WriteLine("m3m4-hw: WARNING " + w);

        string parent = outParent ?? Path.Combine(repoRoot!, "re-analysis", "acceptance", "hardware");
        string bundle = Path.GetFullPath(Path.Combine(parent, $"{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{TestId}"));
        var run = new M3M4Run(ip, bundle, repoRoot, args, records, warnings, only, yes, probe, lift, drive, cubeWaitS, photos, operatorLabel);
        return run.ExecuteAsync().GetAwaiter().GetResult();
    }
}

internal sealed partial class M3M4Run
{
    private sealed record FrameRec(double T, DateTime Utc, bool Out, byte[] Raw, Frame? Frame, string? Error);
    private sealed record Msg(double T, bool Out, byte Tag, byte[] Payload);
    private sealed record Operator(string Check, string Key, string Question, string[] Choices, string? Answer, string? Photo, string? PhotoSha256, double T);

    private readonly IPAddress _ip; private readonly string _bundle; private readonly string? _repoRoot; private readonly string[] _argv;
    private readonly List<JsonObject> _records; private readonly List<string> _warnings; private readonly HashSet<string>? _only;
    private readonly bool _yes, _probe, _lift, _allowDrive; private readonly int _cubeWaitS; private readonly Dictionary<string, string> _photos; private readonly string? _operator;

    private readonly DateTime _t0Utc = DateTime.UtcNow;
    private readonly ConcurrentQueue<FrameRec> _frames = new();
    private readonly ConcurrentQueue<(double T, string Kind, JsonObject Data)> _events = new();
    private readonly ConcurrentQueue<(double T, CameraFrame F)> _camFrames = new();
    private readonly ConcurrentQueue<(double T, string Line)> _engineLog = new();
    private readonly List<Operator> _operatorAnswers = new();
    private readonly List<string> _exceptions = new();
    private readonly List<JsonObject> _checkResults = new();
    private readonly List<(string Name, double From, double To)> _windows = new();
    private readonly JsonObject _robotInfo = new();
    private readonly List<string> _extraFiles = new();
    private CozmoRobot? _robot;
    private RobotConnectionResponse? _response;
    private volatile bool _cancelled;
    private int _written, _aborting;

    public M3M4Run(IPAddress ip, string bundle, string? repoRoot, string[] argv, List<JsonObject> records, List<string> warnings, HashSet<string>? only,
                   bool yes, bool probe, bool lift, bool allowDrive, int cubeWaitS, Dictionary<string, string> photos, string? operatorLabel)
    { _ip = ip; _bundle = bundle; _repoRoot = repoRoot; _argv = argv; _records = records; _warnings = warnings; _only = only; _yes = yes; _probe = probe; _lift = lift; _allowDrive = allowDrive; _cubeWaitS = cubeWaitS; _photos = photos; _operator = operatorLabel; }

    private double Now() => (DateTime.UtcNow - _t0Utc).TotalMilliseconds;
    private double T(DateTime utc) => (utc - _t0Utc).TotalMilliseconds;
    private void Event(string kind, JsonObject data) => _events.Enqueue((Now(), kind, data));
    private static JsonNode? Num(double d) => LinkCheck.Num(d);

    /// <summary>Sleeps in 5 ms steps so Ctrl+C is honoured; returns false when cancelled.</summary>
    private bool Wait(int ms)
    {
        var end = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < end) { if (_cancelled) return false; Thread.Sleep(5); }
        return !_cancelled;
    }

    private static bool Poll(Func<bool> cond, int timeoutMs, Func<bool>? cancelled = null)
    {
        var end = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < end) { if (cond()) return true; if (cancelled?.Invoke() == true) return false; Thread.Sleep(5); }
        return cond();
    }

    // ------------------------------------------------------------------ run

    internal async Task<int> ExecuteAsync()
    {
        using var hr = new HighResolutionTimer();
        Console.WriteLine($"m3m4-hw: robot {_ip}:{RobotAddress.RemotePort(false)}; bundle {_bundle}");
        PrintSetup();
        if (!_yes) { Console.Write("Press Enter to start (Ctrl+C aborts and writes the bundle) ... "); Console.ReadLine(); }
        Directory.CreateDirectory(_bundle);

        Console.CancelKeyPress += (_, e) =>
        {
            if (Volatile.Read(ref _aborting) == 1) return;
            e.Cancel = true; _cancelled = true;
            lock (_exceptions) _exceptions.Add("interrupted: Ctrl+C");
        };
        var watchdog = new Thread(() =>
        {
            Thread.Sleep(M3M4Hw.WatchdogMs);
            if (Volatile.Read(ref _written) == 1) return;
            lock (_exceptions) _exceptions.Add($"watchdog: the run exceeded {M3M4Hw.WatchdogMs} ms");
            Interlocked.Exchange(ref _aborting, 1);
            try { _robot?.Dispose(); } catch { }
            WriteBundle();
            Environment.Exit(3);
        }) { IsBackground = true, Name = "m3m4-watchdog" };
        watchdog.Start();

        bool connected = false;
        try
        {
            _robot = CozmoRobot.Create(engineOptions:EngineStartupResources.Options(_argv));
            Subscribe(_robot);
            connected = Connect(_robot);
            foreach (var def in M3M4Hw.RunOrder.Select(id => M3M4Hw.Checks.Single(c => c.Id == id)))
            {
                if (_only is not null && !_only.Contains(def.Id)) continue;
                if (_cancelled) { Skip(def, "run interrupted before this check"); continue; }
                if (!connected) { Skip(def, "no connection: " + (_response is null ? "no RobotConnectionResponse" : _response.Result.ToString())); continue; }
                if (_robot.Transport.State != LinkState.Connected) { Skip(def, "the link was lost before this check (see events.jsonl)"); continue; }
                Console.WriteLine($"\n=== {def.Id}: {def.Title}");
                double from = Now();
                JsonObject? res = null;
                try
                {
                    res = def.Id switch
                    {
                        "ORIGIN" => await OriginCheck(),
                        "NV_READS" => await NvReadsCheck(),
                        "COLOR_IMAGES" => ColorCheck(),
                        "FACE_ROWS" => FaceCheck(),
                        "MOTOR_CAL" => MotorCalCheck(),
                        "CUBE_TELEM" => await CubeCheck(),
                        "DAS_EVENTS" => DasEventsCheck(),
                        "CAMERA_PARAMS" => CameraParamsCheck(),
                        "TRACK_UNLOCK" => TrackUnlockCheck(),
                        "ANIM_BUDGET" => await AnimBudgetCheck(),
                        "CLIFF" => await CliffCheck(),
                        "CUBE_LIGHTS" => await CubeLightsCheck(),
                        _ => null,
                    };
                }
                catch (Exception e)
                {
                    lock (_exceptions) _exceptions.Add($"{def.Id}: {e}");
                    res = new JsonObject { ["verdict"] = "INCONCLUSIVE", ["verdictRule"] = "the check threw; see exceptions", ["missing"] = new JsonArray(e.Message) };
                }
                if (res is not null) AddResult(def, res, from);
            }
        }
        catch (Exception e)
        {
            lock (_exceptions) _exceptions.Add(e.ToString());
        }
        finally
        {
            Interlocked.Exchange(ref _aborting, 1);
            try { StopBounded(); } catch (Exception e) { lock (_exceptions) _exceptions.Add("dispose: " + e); }
        }
        var quiet = M3M4Hw.Checks.Single(c => c.Id == "DISCONNECT_QUIET");
        if (connected && (_only is null || _only.Contains(quiet.Id)))
        {
            try { AddResult(quiet, DisconnectQuietResult(), Now()); }
            catch (Exception e) { lock (_exceptions) _exceptions.Add("DISCONNECT_QUIET: " + e); }
        }
        // Checks never run: record them so the result names every selected check.
        string overall = WriteBundle();
        Console.WriteLine();
        foreach (var r in _checkResults) Console.WriteLine($"  {r["verdict"],-34} {r["id"],-13} {string.Join(",", r["records"]!.AsArray().Select(x => x!.GetValue<string>()))}");
        Console.WriteLine($"M3M4-HW: {overall}");
        Console.WriteLine($"bundle: {_bundle}\ncopy that whole folder back; nothing has been committed and no record has been changed.");
        return overall == "ERROR" ? 1 : 0;
    }

    private void PrintSetup()
    {
        Console.WriteLine("""

            M3M4-HW. Read re-analysis/acceptance/hardware/scripts/m3m4-hw-2.md first. Setup:
              - the computer is on the robot's Wi-Fi; the official app and every other client is closed;
              - Cozmo on its treads on a clear flat surface with 30 cm free all round (the head nods; ANIM_BUDGET moves head and
                lift; with --include-lift the lift calibrates; with --allow-drive CLIFF drives slowly to a table edge, hand ready);
              - nothing is written to the robot's storage; no wheel motion unless --allow-drive;
              - the face must be visible to the operator; for CUBE_TELEM one powered cube within 30 cm;
              - answers and photo paths are typed at the prompts; blank means "could not tell" (recorded as such).
            Ctrl+C stops the run and still writes the bundle.
            """);
    }

    private void Skip(M3M4Hw.CheckDef def, string why) =>
        AddResult(def, new JsonObject { ["verdict"] = "INCONCLUSIVE", ["verdictRule"] = "check not run", ["missing"] = new JsonArray(why) }, Now());

    private void AddResult(M3M4Hw.CheckDef def, JsonObject res, double from)
    {
        res["id"] = def.Id; res["title"] = def.Title;
        res["records"] = new JsonArray(def.Records.Select(r => (JsonNode)r).ToArray());
        res["moves"] = def.Moves; res["startMs"] = Num(from); res["endMs"] = Num(Now());
        res["settlesNoRecord"] = "an observation, not a verdict on the record: no verdict here changes the manifest or raises provenance";
        lock (_checkResults) _checkResults.Add(res);
        Console.WriteLine($"--- {def.Id}: {res["verdict"]}  ({res["verdictRule"]})");
    }

    private void StopBounded()
    {
        var r = _robot; if (r is null) return;
        var t = new Thread(() => { try { r.Dispose(); } catch (Exception e) { lock (_exceptions) _exceptions.Add("dispose: " + e); } }) { IsBackground = true };
        t.Start();
        if (!t.Join(5000)) lock (_exceptions) _exceptions.Add("Dispose did not return within 5000 ms");
        Wait2(800);
    }
    private static void Wait2(int ms) => Thread.Sleep(ms);

    // ------------------------------------------------------------------ connection and subscriptions

    private void Subscribe(CozmoRobot robot)
    {
        robot.Transport.FrameTrace += e => _frames.Enqueue(new FrameRec(T(e.Utc), e.Utc, e.Outbound, e.Raw, e.Frame, e.Error));
        robot.Transport.Warning += w => Event("transport-warning", new JsonObject { ["text"] = w });
        robot.Transport.Disconnected += r => Event("transport-disconnected", new JsonObject { ["reason"] = r });
        robot.Transport.Connected += () => Event("transport-connected", new JsonObject());
        EngineEvidence.Subscribe(robot.Engine, l => _engineLog.Enqueue((Now(), l)), (kind, value) => Event(kind, value));
        robot.Engine.ConnectionResponse += r =>
        {
            _response = r;
            Event("connection-response", new JsonObject { ["result"] = r.Result.ToString(), ["fwVersion"] = r.FwVersion, ["serial"] = $"0x{r.SerialNumber:x8}", ["bodyHwVersion"] = r.BodyHWVersion });
        };
        robot.Engine.RobotDisconnected += m => Event("robot-disconnected", new JsonObject { ["timeSinceLastMsgSec"] = m.TimeSinceLastMsgSec });
        robot.Engine.RobotErrorPassThrough += m => Event("robot-error", new JsonObject { ["code"] = m.Code });
        robot.HandlerFaulted += e => Event("handler-fault", new JsonObject { ["exception"] = e.ToString() });
        robot.Camera.FrameReceived += f => _camFrames.Enqueue((Now(), f));
        robot.Camera.FrameDropped += (id, why) => Event("camera-dropped", new JsonObject { ["imageId"] = id, ["why"] = why });
        robot.Sensors.MotorCalibrationReported += c => Event("motor-calibration", new JsonObject { ["motor"] = c.MotorID.ToString(), ["started"] = c.CalibStarted, ["auto"] = c.AutoStarted });
        robot.Cubes.CubeDiscovered += c => Event("cube-discovered", CubeJson(c));
        robot.Cubes.ConnectionChanged += c => Event("cube-connection", CubeJson(c));
    }

    private static JsonObject CubeJson(Cube c) => new()
    {
        ["factoryId"] = $"0x{c.FactoryId:x8}", ["objectId"] = c.ObjectId, ["type"] = c.Type.ToString(), ["rssi"] = c.Rssi, ["connected"] = c.Connected,
    };

    /// <summary>The ConnectToRobot game message and the engine's ConnectionResponse (the body of CozmoRobot.ConnectAsync), polled for time sync, first full state and ready-to-stream.</summary>
    private bool Connect(CozmoRobot robot)
    {
        double call = Now();
        robot.ConnectToRobot(_ip);
        Event("connect-call", new JsonObject());
        bool ok = Poll(() =>
        {
            var r = robot.Engine.Robot;
            if (_response is { Result: not RobotConnectionResult.Success }) return true;
            return _response is not null && r is { TimeSynced: true, FirstFullStateHandled: true, ReadyToStream: true };
        }, M3M4Hw.ConnectBudgetMs, () => _cancelled);
        bool success = _response?.Result == RobotConnectionResult.Success && ok;
        lock (_robotInfo)
        {
            _robotInfo["connectResult"] = _response?.Result.ToString();
            _robotInfo["fwVersion"] = _response?.FwVersion;
            _robotInfo["serial"] = _response is null ? null : $"0x{_response.SerialNumber:x8}";
            _robotInfo["bodyHwVersion"] = _response?.BodyHWVersion;
            _robotInfo["connectedAfterMs"] = Num(Now() - call);
            _robotInfo["firmwareJson"] = robot.State.Firmware?.SignatureJson;
        }
        if (success) Wait(M3M4Hw.ConnectedSettleMs);
        Console.WriteLine($"connect: {(success ? "Success, time synced, first state handled, ready to stream" : "FAILED: response " + (_response?.Result.ToString() ?? "none") + (_response?.Result == RobotConnectionResult.Success ? " but not synced / first state / ready to stream within the budget" : ""))}; firmware {_response?.FwVersion}");
        return success;
    }

    // ------------------------------------------------------------------ wire views

    private FrameRec[] Frames() => _frames.ToArray();

    /// <summary>
    /// CLAD messages seen on the wire in time order. Reliable messages count once per sequence id (resends dropped);
    /// the dedupe set resets at each outbound ConnectionRequest / inbound ConnectionResponse.
    /// </summary>
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
    private static byte Tag(RobotMessageId id) => (byte)id;

    private static JsonNode? Fields(byte[] payload, int hexBytes = 48)
    {
        switch (Parse(payload))
        {
            case NVCommand n: return new JsonObject { ["tag"] = $"0x{n.Tag:X8}", ["length"] = n.Length, ["op"] = n.Op, ["dataBytes"] = n.Data.Length };
            case NVOpResult r:
                return new JsonObject
                {
                    ["tag"] = $"0x{r.Tag:X8}", ["op"] = r.Op, ["result"] = r.Result, ["indexField"] = r.Length, ["dataBytes"] = r.Data.Length,
                    ["dataSha256"] = Convert.ToHexString(SHA256.HashData(r.Data)).ToLowerInvariant(),
                    ["dataHex"] = Convert.ToHexString(r.Data, 0, Math.Min(r.Data.Length, 4096)),
                    ["dataHexTruncated"] = r.Data.Length > 4096,
                };
            case RobotState s:
                return new JsonObject { ["ts"] = s.Timestamp, ["originId"] = s.PoseOriginId, ["frameId"] = s.PoseFrameId, ["head"] = Num(s.HeadAngle), ["lift"] = Num(s.LiftAngle), ["status"] = $"0x{s.Status:x}" };
            case AbsoluteLocalizationUpdate a:
                return new JsonObject { ["timestamp"] = a.Timestamp, ["frameId"] = a.PoseFrameId, ["originId"] = a.PoseOriginId, ["x"] = Num(a.PoseX), ["y"] = Num(a.PoseY), ["angleRad"] = Num(a.PoseAngleRad) };
            case MotorCalibration c: return new JsonObject { ["motor"] = c.MotorID.ToString(), ["started"] = c.CalibStarted, ["auto"] = c.AutoStarted };
            case AnimationState a:
                return new JsonObject { ["ts"] = a.Timestamp, ["animBytesPlayed"] = a.NumAnimBytesPlayed, ["audioFramesPlayed"] = a.NumAudioFramesPlayed, ["tracks"] = $"0x{a.EnabledAnimTracks:x2}", ["tag"] = a.Tag, ["drops"] = a.ClientDropCount };
            case ImageChunk i:
                return new JsonObject { ["imageId"] = i.ImageId, ["encoding"] = i.ImageEncoding, ["resolution"] = i.ImageResolution, ["chunkCount"] = i.ImageChunkCount, ["chunkId"] = i.ChunkId, ["status"] = i.Status, ["dataBytes"] = i.Data.Length, ["firstDataByte"] = i.Data.Length > 0 ? i.Data[0] : null };
            case EnableColorImages e: return new JsonObject { ["enable"] = e.Enable };
            case ImageRequest q: return new JsonObject { ["mode"] = q.Mode.ToString(), ["resolution"] = q.ImageResolution };
            case StreamObjectAccel sa: return new JsonObject { ["objectId"] = sa.ObjectID, ["enable"] = sa.Enable };
            case ObjectAccel oa: return new JsonObject { ["objectId"] = oa.ObjectID, ["robotTs"] = oa.Timestamp, ["hex"] = Convert.ToHexString(payload, 1, Math.Min(payload.Length - 1, 48)) };
            default: return payload.Length <= 1 ? null : Convert.ToHexString(payload, 1, Math.Min(payload.Length - 1, hexBytes));
        }
    }

    private static JsonObject MsgJson(Msg m, double rel) => new() { ["afterMs"] = Num(m.T - rel), ["dir"] = m.Out ? "out" : "in", ["tag"] = $"0x{m.Tag:X2}", ["msg"] = TagName(m.Tag), ["fields"] = Fields(m.Payload) };

    private record StateObs(double T, uint Ts, uint Origin, uint Frame, float Head, float Lift, uint Status);
    private List<StateObs> States(double from, double to) => Wire().Where(m => !m.Out && m.Tag == Tag(RobotMessageId.State) && m.T >= from && m.T <= to)
        .Select(m => (m, s: Parse(m.Payload) as RobotState)).Where(x => x.s is not null)
        .Select(x => new StateObs(x.m.T, x.s!.Timestamp, x.s.PoseOriginId, x.s.PoseFrameId, x.s.HeadAngle, x.s.LiftAngle, x.s.Status)).ToList();

    private static JsonObject Histogram(IEnumerable<string> keys)
    {
        var o = new JsonObject();
        foreach (var g in keys.GroupBy(k => k).OrderBy(g => g.Key, StringComparer.Ordinal)) o[g.Key] = g.Count();
        return o;
    }

    private string[] LogSince(double t) => _engineLog.ToArray().Where(l => l.T >= t).Select(l => l.Line).ToArray();

    // ------------------------------------------------------------------ operator prompts

    private string? Ask(string check, string key, string question, string[] choices, bool photo)
    {
        double t = Now();
        string? answer = null, photoRel = null, photoSha = null;
        string fullKey = $"{check}.{key}";
        if (!_yes)
        {
            Console.WriteLine();
            Console.WriteLine($"[{fullKey}] {question}");
            Console.Write($"  answer ({string.Join(" / ", choices)}; blank = could not tell): ");
            var line = Console.ReadLine()?.Trim();
            answer = string.IsNullOrEmpty(line) ? "could-not-tell" : M3M4Hw.NormaliseAnswer(line, choices);
        }
        if (photo)
        {
            string? path = _photos.TryGetValue(fullKey, out var p) ? p : null;
            if (path is null && !_yes)
            {
                Console.Write("  photo file path (blank = none): ");
                var l = Console.ReadLine()?.Trim().Trim('"');
                if (!string.IsNullOrEmpty(l) && Directory.Exists(l))
                {
                    Console.Write("  that is a folder; give the photo file itself (blank = none): ");
                    l = Console.ReadLine()?.Trim().Trim('"');
                }
                if (!string.IsNullOrEmpty(l) && File.Exists(l)) path = l;
                else if (!string.IsNullOrEmpty(l)) lock (_exceptions) _exceptions.Add($"photo {l}: not a file; none recorded");
            }
            if (path is not null)
            {
                try
                {
                    var bytes = File.ReadAllBytes(path);
                    Directory.CreateDirectory(Path.Combine(_bundle, "operator"));
                    photoRel = Path.Combine("operator", $"{fullKey}-{Path.GetFileName(path)}");
                    File.WriteAllBytes(Path.Combine(_bundle, photoRel), bytes);
                    photoSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                    _extraFiles.Add(photoRel);
                }
                catch (Exception e) { lock (_exceptions) _exceptions.Add($"photo {path}: {e.Message}"); photoRel = null; }
            }
        }
        lock (_operatorAnswers) _operatorAnswers.Add(new Operator(check, key, question, choices, answer, photoRel, photoSha, t));
        Event("operator-answer", new JsonObject { ["key"] = fullKey, ["answer"] = answer, ["photo"] = photoRel });
        return answer;
    }

    private JsonArray OperatorJson(string check) => new(_operatorAnswers.Where(o => o.Check == check).Select(o => (JsonNode)new JsonObject
    {
        ["key"] = $"{o.Check}.{o.Key}", ["question"] = o.Question, ["choices"] = new JsonArray(o.Choices.Select(c => (JsonNode)c).ToArray()),
        ["answer"] = o.Answer, ["photo"] = o.Photo, ["photoSha256"] = o.PhotoSha256, ["atMs"] = Num(o.T),
    }).ToArray());

    // ------------------------------------------------------------------ M4-021: ORIGIN

    private async Task<JsonObject> OriginCheck()
    {
        var robot = _robot!;
        // Passive: the engine sent AbsoluteLocalizationUpdate {timestamp 0, frame 0, origin 1} at SyncTime (CozmoEngine.SyncTime, M4-020).
        // Judged from the wire, so states the engine's own M4-020 gate dropped are still seen.
        var wire = Wire();
        var abs = wire.Where(m => m.Out && m.Tag == Tag(RobotMessageId.AbsLocalizationUpdate)).ToList();
        var res = new JsonObject();
        var missing = new JsonArray();
        if (abs.Count == 0)
        {
            res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "no AbsoluteLocalizationUpdate was sent on this connection";
            missing.Add("the connection path did not send AbsoluteLocalizationUpdate: nothing to attribute the origin to"); res["missing"] = missing;
            return res;
        }
        var upd = (AbsoluteLocalizationUpdate)Parse(abs[0].Payload)!;
        double tSend = abs[0].T;
        var states = States(0, Now());
        var before = states.Where(s => s.T < tSend).ToList();
        var settled = states.Where(s => s.T >= tSend + 500).ToList();
        string Pair(IEnumerable<StateObs> l) => string.Join(", ", l.GroupBy(s => (s.Origin, s.Frame)).Select(g => $"origin {g.Key.Origin} frame {g.Key.Frame} x{g.Count()}"));
        var phaseA = new JsonObject
        {
            ["update"] = Fields(abs[0].Payload), ["updateSentAfterConnectMs"] = Num(tSend),
            ["statesBeforeUpdate"] = before.Count, ["pairsBefore"] = Pair(before),
            ["statesAfterUpdatePlus500ms"] = settled.Count, ["pairsAfter"] = Pair(settled),
            ["firstStatesAfterUpdate"] = new JsonArray(states.Where(s => s.T >= tSend).Take(12).Select(s => (JsonNode)new JsonObject { ["afterUpdateMs"] = Num(s.T - tSend), ["robotTs"] = s.Ts, ["origin"] = s.Origin, ["frame"] = s.Frame }).ToArray()),
            ["note"] = "states are counted from the wire (before the engine's drain); the 500 ms margin leaves out states the robot built before the update reached it",
        };
        res["connectionUpdate"] = phaseA;
        bool sameAsUpdateBefore = before.Count > 0 && before.All(s => s.Origin == upd.PoseOriginId && s.Frame == upd.PoseFrameId);
        if (before.Count == 0 && settled.Count >= 20 && settled.All(s => s.Origin == upd.PoseOriginId && s.Frame == upd.PoseFrameId))
        {
            res["verdict"] = "OBSERVED_REPORTS_UPDATE_VALUES_FROM_FIRST_STATE";
            res["verdictRule"] = "the engine sends the update before the first RobotState, so there is no in-run before; every state from 500 ms after it reports the update's origin and frame. Baseline: 20260924-202748-CONTROL (no update sent: all 855 states origin 0 frame 0)";
        }
        else if (before.Count < 3 || settled.Count < 20)
        {
            res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "fewer than 3 states before the update or 20 after it";
            missing.Add($"states before: {before.Count}; after +500 ms: {settled.Count}: the first RobotStates may arrive after the update (the engine sends it at SyncTime, before the first state); then only the cross-bundle comparison with 20260924-202748-CONTROL (all 855 states origin 0 frame 0, no update sent) applies");
        }
        else if (sameAsUpdateBefore)
        {
            res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "the robot already reported the update's values before it";
            missing.Add("cannot attribute the origin to the update from this run; compare with 20260924-202748-CONTROL (no update sent: all states origin 0 frame 0)");
        }
        else if (settled.All(s => s.Origin == upd.PoseOriginId && s.Frame == upd.PoseFrameId))
        { res["verdict"] = "OBSERVED_REPORTS_UPDATE_VALUES"; res["verdictRule"] = "every state from 500 ms after the update reports the update's origin id and frame id, and the states before it did not"; }
        else if (settled.All(s => before.Any(b => b.Origin == s.Origin && b.Frame == s.Frame)))
        { res["verdict"] = "OBSERVED_UPDATE_NOT_REFLECTED"; res["verdictRule"] = "every state from 500 ms after the update carries an (origin, frame) pair also seen before it, and none is the update's"; }
        else
        { res["verdict"] = "OBSERVED_MIXED"; res["verdictRule"] = "the post-update states report more than one (origin, frame) pair; see pairsAfter"; }

        if (missing.Count > 0) res["missing"] = missing;
        await Task.CompletedTask;
        return res;
    }

    // ------------------------------------------------------------------ M3-036: NV_READS (read-only)

    private async Task<JsonObject> NvReadsCheck()
    {
        // CozmoEngine.NvStorage is internal; read it by reflection so the production library is untouched.
        var nv = typeof(CozmoEngine).GetProperty("NvStorage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(_robot!.Engine) as NvStorageComponent;
        var res = new JsonObject(); var missing = new JsonArray();
        if (nv is null) { res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "no NV component"; missing.Add("robot.Engine.NvStorage is null"); res["missing"] = missing; return res; }
        if (!Poll(() => nv.IsIdle, M3M4Hw.NvIdleWaitMs, () => _cancelled)) missing.Add("the NV component was still busy with the connection-time queue; reads queued behind it");

        JsonObject ReadOne(uint tag)
        {
            double t0 = Now(); int logFrom = nv.Log.Count;
            var task = nv.ReadAsync(tag, TimeSpan.FromMilliseconds(M3M4Hw.NvReadTimeoutMs));
            NvResult r; try { r = task.GetAwaiter().GetResult(); } catch (Exception e) { r = new NvResult(-3, Array.Empty<byte>()); lock (_exceptions) _exceptions.Add($"NV read 0x{tag:X}: {e.Message}"); }
            double t1 = Now();
            var wire = Wire().Where(m => m.T >= t0 - 1 && m.T <= t1 + 200 && (m.Tag == Tag(RobotMessageId.CommandNV) || m.Tag == Tag(RobotMessageId.NvOpResult))).ToList();
            var cmds = wire.Where(m => m.Out).Select(m => (m, c: Parse(m.Payload) as NVCommand)).Where(x => x.c is not null).ToList();
            var replies = wire.Where(m => !m.Out).Select(m => (m, r: Parse(m.Payload) as NVOpResult)).Where(x => x.r is not null).ToList();
            var logs = nv.Log.Skip(logFrom).ToArray();
            return new JsonObject
            {
                ["tag"] = $"0x{tag:X8}", ["name"] = NvStorageComponent.NvEntryTagName(tag), ["factory"] = NvStorageComponent.IsFactoryEntryTag(tag),
                ["terminalResult"] = r.Result, ["terminalResultName"] = NvStorageComponent.NvResultName(r.Result), ["terminalDataBytes"] = r.Data.Length, ["elapsedMs"] = Num(t1 - t0),
                ["commandsSent"] = new JsonArray(cmds.Select(x => (JsonNode)MsgJson(x.m, t0)).ToArray()),
                ["repliesReceived"] = new JsonArray(replies.Select(x => (JsonNode)MsgJson(x.m, t0)).ToArray()),
                ["componentLog"] = new JsonArray(logs.Select(l => (JsonNode)l).ToArray()),
                ["reRequestLogged"] = logs.Any(l => l.Contains("ReadingRestOfData", StringComparison.Ordinal)),
                ["commandLengths"] = new JsonArray(cmds.Select(x => (JsonNode)x.c!.Length).ToArray()),
            };
        }

        var a = new JsonArray(); var b = new JsonArray();
        foreach (var tag in M3M4Hw.FactoryLen1Tags) { if (_cancelled) break; a.Add(ReadOne(tag)); }
        foreach (var tag in M3M4Hw.NonFactoryTags) { if (_cancelled) break; b.Add(ReadOne(tag)); }
        res["factoryReads"] = a; res["nonFactoryReads"] = b;
        res["safety"] = "only NVStorageComponent.Read (op 0) is called; no Write, Erase, WipeAll or WipeFactory";

        bool Len1(JsonNode? n) => n!["commandLengths"]!.AsArray().Any(x => x!.GetValue<int>() == 1) && n["repliesReceived"]!.AsArray().Count > 0;
        var len1 = a.Where(Len1).ToList();
        var reReq = b.Where(n => n!["reRequestLogged"]!.GetValue<bool>()).ToList();
        res["factoryLength1Summary"] = new JsonArray(a.Select(n => (JsonNode)$"{n!["name"]}: Length {string.Join("/", n["commandLengths"]!.AsArray())} -> result {n["terminalResultName"]}, {n["terminalDataBytes"]} bytes, {n["repliesReceived"]!.AsArray().Count} replies").ToArray());
        res["nonFactorySummary"] = new JsonArray(b.Select(n => (JsonNode)$"{n!["name"]}: Length {string.Join("/", n["commandLengths"]!.AsArray())} -> result {n["terminalResultName"]}, {n["terminalDataBytes"]} bytes, {n["repliesReceived"]!.AsArray().Count} replies, re-request {n["reRequestLogged"]}").ToArray());

        if (len1.Count == 0) { res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "no factory read with Length = 1 got a reply"; missing.Add("no NVOpResult followed a Length = 1 factory read"); }
        else if (reReq.Count == 0)
        {
            res["verdict"] = "OBSERVED_FACTORY_LENGTH1_REPLIES_NO_REREQUEST_CASE";
            res["verdictRule"] = "at least one factory read sent Length = 1 and was answered (shape in factoryReads); no non-factory read needed a re-request, so the size+16 contract half is unanswered";
            missing.Add("a non-factory entry whose stored total exceeds the first blob (>~1008 bytes): needs data on the robot (e.g. an enrolled face album, 0x184000, written by an official app session earlier). This tool does not write NV, so it cannot create one");
        }
        else
        {
            res["verdict"] = "OBSERVED_FACTORY_LENGTH1_REPLIES_AND_REREQUEST";
            res["verdictRule"] = "a factory Length = 1 read was answered, and at least one non-factory read logged ReadingRestOfData; the re-request command length and every reply (index field, data bytes) are in nonFactoryReads";
        }
        if (missing.Count > 0) res["missing"] = missing;
        await Task.CompletedTask;
        return res;
    }

    // ------------------------------------------------------------------ M3-016: COLOR_IMAGES

    private JsonObject ColorCheck()
    {
        var robot = _robot!;
        var res = new JsonObject(); var missing = new JsonArray();
        var phases = new JsonArray();
        var colourSeen = new List<string>(); var grayFramesTotal = 0; var framesTotal = 0; var colourDecodeThree = 0;
        Directory.CreateDirectory(Path.Combine(_bundle, "camera"));

        JsonObject Phase(string name, string action, Action? act, bool restart)
        {
            double t0 = Now();
            act?.Invoke();
            double tAct = Now();
            Wait(M3M4Hw.ColorPhaseMs);
            double t1 = Now();
            lock (_windows) _windows.Add(("COLOR_IMAGES." + name, t0, t1));
            var frames = _camFrames.ToArray().Where(f => f.T >= tAct && f.T <= t1).ToList();
            // Warm-up frames after a Restart are torn (CozmoCamera.IsWarmUp); count them, judge the rest.
            var judged = frames.Where(f => !f.F.IsWarmUp).ToList();
            var per = new JsonArray();
            int three = 0, flagged = 0;
            foreach (var (t, f) in frames.Take(60))
            {
                string geo = "undecodable"; int comps = 0;
                try { var img = ImageResult.FromMemory(f.Jpeg, ColorComponents.Default); geo = $"{img.Width}x{img.Height}"; comps = (int)img.SourceComp; } catch { }
                per.Add(new JsonObject { ["afterMs"] = Num(t - t0), ["imageId"] = f.ImageId, ["encoding"] = f.Encoding, ["isColorFlag"] = f.IsColor, ["reported"] = $"{f.Width}x{f.Height}", ["jpegWidth"] = f.JpegWidth, ["chunks"] = f.ChunkCount, ["rawPayloadBytes"] = f.RawPayload.Length, ["jpegBytes"] = f.Jpeg.Length, ["decoded"] = geo, ["jpegComponents"] = comps, ["warmUp"] = f.IsWarmUp });
            }
            foreach (var (t, f) in judged)
            {
                try { if ((int)ImageResult.FromMemory(f.Jpeg, ColorComponents.Default).SourceComp == 3) three++; } catch { }
                if (f.IsColor || f.Encoding == 9) flagged++;
            }
            var chunks = Wire().Where(m => !m.Out && m.Tag == Tag(RobotMessageId.Image) && m.T >= tAct && m.T <= t1).Select(m => Parse(m.Payload) as ImageChunk).Where(c => c is not null).Select(c => c!).ToList();
            // A14: the engine turns encoding 8 with a non-zero first payload byte into 9; record the raw chunk facts it keys on.
            var firstChunks = chunks.Where(c => c.ChunkId == 0).ToList();
            // save up to 2 frames: jpg + raw payload
            var saved = new JsonArray();
            foreach (var (t, f) in judged.Take(M3M4Hw.ColorSavedPerPhase))
            {
                string stem = $"camera/{name}-{f.ImageId:D5}";
                File.WriteAllBytes(Path.Combine(_bundle, stem + ".jpg"), f.PresentationJpeg());
                File.WriteAllBytes(Path.Combine(_bundle, stem + ".raw.bin"), f.RawPayload);
                _extraFiles.Add(stem + ".jpg"); _extraFiles.Add(stem + ".raw.bin");
                saved.Add(new JsonObject { ["jpg"] = stem + ".jpg", ["raw"] = stem + ".raw.bin", ["imageId"] = f.ImageId, ["encoding"] = f.Encoding, ["isColorFlag"] = f.IsColor });
            }
            var other = Wire().Where(m => m.T >= t0 && m.T <= t1 && m.Tag != Tag(RobotMessageId.State) && m.Tag != Tag(RobotMessageId.Image) && m.Tag != Tag(RobotMessageId.AnimState)).ToList();
            framesTotal += judged.Count; colourDecodeThree += three;
            if (flagged > 0) colourSeen.Add(name);
            if (judged.Count > 0 && flagged == 0) grayFramesTotal += judged.Count;
            return new JsonObject
            {
                ["phase"] = name, ["action"] = action, ["actionAfterMs"] = Num(tAct - t0), ["windowMs"] = Num(t1 - tAct),
                ["framesCompleted"] = frames.Count, ["framesJudged"] = judged.Count, ["framesColourFlagOrEncoding9"] = flagged, ["framesWithThreeJpegComponents"] = three,
                ["encodingsAll"] = Histogram(frames.Select(f => f.F.Encoding.ToString())),
                ["chunkEncodingsOnWire"] = Histogram(chunks.Select(c => c.ImageEncoding.ToString())),
                ["firstChunkFirstDataByteOnWire"] = Histogram(firstChunks.Select(c => c.Data.Length > 0 ? $"0x{c.Data[0]:X2}" : "empty")),
                ["chunkStatusOnWire"] = Histogram(chunks.Select(c => c.Status.ToString())),
                ["frames"] = per, ["saved"] = saved,
                ["otherMessagesInWindow"] = Histogram(other.Select(m => $"{(m.Out ? "out" : "in")} {TagName(m.Tag)}")),
                ["engineLog"] = new JsonArray(LogSince(t0).Where(l => l.Contains("amera", StringComparison.Ordinal) || l.Contains("mage", StringComparison.Ordinal) || l.Contains("olor", StringComparison.Ordinal)).Take(20).Select(l => (JsonNode)l).ToArray()),
            };
        }

        phases.Add(Phase("P0-baseline", "none: the stream the engine requested at SyncTime", null, false));
        if (!_cancelled) phases.Add(Phase("P1-enable-color", "CameraSettings.EnableColorImages(true) only (the engine path, M3-023)", () => robot.CameraSettings.EnableColorImages(true), false));
        if (!_cancelled) phases.Add(Phase("P2-enable-color-and-request", "StartCamera(color: true): Restart + EnableColorImages(true) + ImageRequest Stream (tool-only, M3-044)", () => robot.StartCamera(color: true), true));
        if (!_cancelled) phases.Add(Phase("P3-disable-color", "EnableColorImages(false) then StartCamera(false) to leave the stream as the engine started it", () => { robot.CameraSettings.EnableColorImages(false); robot.StartCamera(false); }, true));
        res["phases"] = phases;

        // Operator: look at the saved frames (the colour question is also visible to the eye).
        string? ans = Ask("COLOR_IMAGES", "LOOKS_COLOUR", "Open the P1 and P2 saved .jpg files in camera/ in the bundle folder. Do they show colour? (a colourless scene, e.g. a white wall, cannot answer; point the robot at something coloured first if you can)", new[] { "colour", "grey", "cannot-tell" }, photo: true);
        res["operator"] = OperatorJson("COLOR_IMAGES");

        var p = phases.Select(x => x!.AsObject()).ToList();
        int p1 = p.Count > 1 ? p[1]["framesJudged"]!.GetValue<int>() : 0, p2 = p.Count > 2 ? p[2]["framesJudged"]!.GetValue<int>() : 0;
        if (p1 + p2 == 0) { res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "no frame completed in the colour phases"; missing.Add("the robot sent no complete frame after EnableColorImages (see otherMessagesInWindow, engine logs and camera-dropped events)"); }
        else if (colourSeen.Count > 0 || colourDecodeThree > 0)
        { res["verdict"] = "OBSERVED_COLOUR_FRAMES"; res["verdictRule"] = $"a frame with encoding 9 / the colour flag, or a three-component JPEG, was received in {string.Join(",", colourSeen)}; format details per frame in phases"; }
        else
        { res["verdict"] = "OBSERVED_NO_COLOUR_FRAMES"; res["verdictRule"] = "frames completed with the colour flag on, none with encoding 9 or a three-component JPEG"; }
        if (ans is null && !_yes) missing.Add("no operator answer");
        if (missing.Count > 0) res["missing"] = missing;
        return res;
    }

    // ------------------------------------------------------------------ M3-008: FACE_ROWS

    private static byte[] Canvas(Func<int, int, bool> lit)
    {
        var c = new byte[FaceBitmapCodec.CanvasRows * FaceBitmapCodec.CanvasColumns];
        for (int r = 0; r < FaceBitmapCodec.CanvasRows; r++) for (int x = 0; x < FaceBitmapCodec.CanvasColumns; x++) if (lit(r, x)) c[r * FaceBitmapCodec.CanvasColumns + x] = 1;
        return c;
    }

    /// <summary>The patterns, as canvas predicates (row 0..63, column 0..127). Pair k of a column is canvas rows 2k and 2k+1 (engine side: B10, B14).</summary>
    internal static readonly (string Key, string Question, string[] Choices, string EngineModel, Func<int, int, bool> Lit)[] FacePatterns =
    {
        ("TOP_BAND", "A solid band should span the whole width. Where on the face is it?", new[] { "top", "bottom", "middle", "two-bands", "other" },
            "top: canvas rows 0..7 (pairs 0..3) lit in both bits, the whole width", (r, x) => r < 8),
        ("BOTTOM_BAND", "A solid band should span the whole width. Where on the face is it?", new[] { "top", "bottom", "middle", "two-bands", "other" },
            "bottom: canvas rows 56..63 (pairs 28..31) lit in both bits", (r, x) => r >= 56),
        ("DIAGONAL_BLOCKS", "Two filled blocks. Which corners/halves are lit (face upright, wheels at the bottom)?", new[] { "top-left+bottom-right", "top-right+bottom-left", "top-left+top-right", "bottom-left+bottom-right", "other" },
            "top-left and bottom-right: canvas rows 0..31 lit in columns 0..63; rows 32..63 lit in columns 64..127", (r, x) => (r < 32 && x < 64) || (r >= 32 && x >= 64)),
        ("PARITY_STEP", "A horizontal stripe band across the middle, half-brightness (every other row). Compare its two halves closely (photo). Where does the lit row pattern begin higher?", new[] { "left-higher", "right-higher", "same", "cannot-tell" },
            "left half lights canvas rows 2k (pair bit 0), right half rows 2k+1 (bit 1), rows 16..47: the right half's lit rows sit one row lower than the left's", (r, x) => r is >= 16 and < 48 && ((x < 64) == (r % 2 == 0))),
        ("EVEN_ROWS_ONLY", "Every even canvas row lit across the whole face. Does it look like fine horizontal lines at half brightness over the whole face (as opposed to solid, or only part of the face)?", new[] { "fine-lines-whole-face", "solid", "partial", "blank", "other" },
            "pair bit 0 only: canvas rows 0,2,4..62 lit across the whole face", (r, x) => r % 2 == 0),
    };

    private static string CanvasArt(byte[] canvas)
    {
        var sb = new StringBuilder();
        for (int r = 0; r < FaceBitmapCodec.CanvasRows; r++)
        {
            for (int x = 0; x < FaceBitmapCodec.CanvasColumns; x++) sb.Append(canvas[r * FaceBitmapCodec.CanvasColumns + x] != 0 ? '#' : '.');
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private JsonObject FaceCheck()
    {
        var robot = _robot!;
        var res = new JsonObject(); var missing = new JsonArray();
        Directory.CreateDirectory(Path.Combine(_bundle, "face"));
        var patterns = new JsonArray(); int agree = 0, disagree = 0, unanswered = 0;
        foreach (var pat in FacePatterns)
        {
            if (_cancelled) break;
            var canvas = Canvas(pat.Lit);
            var payload = FaceBitmapCodec.EncodeCanvas(canvas, FaceBitmapCodec.CanvasRows, FaceBitmapCodec.CanvasColumns)!;
            File.WriteAllText(Path.Combine(_bundle, "face", pat.Key + ".txt"), CanvasArt(canvas));
            File.WriteAllBytes(Path.Combine(_bundle, "face", pat.Key + ".payload.bin"), payload);
            _extraFiles.Add($"face/{pat.Key}.txt"); _extraFiles.Add($"face/{pat.Key}.payload.bin");
            double t0 = Now(); int sent0 = robot.Display.FramesSent;
            Console.WriteLine($"\n[{pat.Key}] showing the pattern for {M3M4Hw.FacePatternHoldMs / 1000} s: look at the face now");
            var end = Environment.TickCount64 + M3M4Hw.FacePatternHoldMs;
            while (Environment.TickCount64 < end && !_cancelled) robot.Display.SendRaw(payload);
            double t1 = Now();
            // keep it on the face while the operator answers: the robot may revert to its idle face once frames stop, so re-send during the prompt
            using var keep = new CancellationTokenSource();
            var holder = Task.Run(() => { while (!keep.IsCancellationRequested) { try { robot.Display.SendRaw(payload); } catch { break; } } });
            string? ans = Ask("FACE_ROWS", pat.Key, pat.Question, pat.Choices, photo: true);
            keep.Cancel(); holder.GetAwaiter().GetResult();
            bool? matches = ans is null || ans == "could-not-tell" || ans == "cannot-tell" ? null : (pat.Key switch
            {
                "TOP_BAND" => ans == "top", "BOTTOM_BAND" => ans == "bottom", "DIAGONAL_BLOCKS" => ans == "top-left+bottom-right",
                "PARITY_STEP" => ans == "right-higher", "EVEN_ROWS_ONLY" => ans == "fine-lines-whole-face", _ => (bool?)null,
            });
            if (matches is null) unanswered++; else if (matches == true) agree++; else disagree++;
            patterns.Add(new JsonObject
            {
                ["key"] = pat.Key, ["engineModel"] = pat.EngineModel, ["payloadBytes"] = payload.Length, ["payloadSha256"] = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(),
                ["framesSentDuringHold"] = robot.Display.FramesSent - sent0, ["holdMs"] = Num(t1 - t0), ["operatorAnswer"] = ans, ["answerMatchesEngineModel"] = matches,
            });
        }
        res["patterns"] = patterns;

        // Playback period: a moving bar, 30 frames per sweep, at three host intervals; the robot's own AnimationState counters are recorded.
        var sweeps = new JsonArray();
        foreach (int interval in M3M4Hw.FaceSweepIntervalsMs)
        {
            if (_cancelled) break;
            var payloads = Enumerable.Range(0, M3M4Hw.FaceSweepFrames).Select(i => FaceBitmapCodec.EncodeCanvas(Canvas((r, x) => x >= i * 4 && x < i * 4 + 6), FaceBitmapCodec.CanvasRows, FaceBitmapCodec.CanvasColumns)!).ToArray();
            double t0 = Now(); int sent0 = robot.Display.FramesSent;
            Console.WriteLine($"\n[SWEEP {interval} ms] a vertical bar sweeps left to right in {M3M4Hw.FaceSweepFrames} frames at one frame per {interval} ms host time; time it with a stopwatch from when it starts to when it stops moving");
            var sw = Stopwatch.StartNew(); var sendTimes = new List<double>();
            for (int i = 0; i < payloads.Length && !_cancelled; i++)
            {
                var due = i * (double)interval;
                while (sw.Elapsed.TotalMilliseconds < due && !_cancelled) Thread.Sleep(1);
                robot.Display.SendRaw(payloads[i]); sendTimes.Add(sw.Elapsed.TotalMilliseconds);
            }
            double t1 = Now();
            Wait(1500);
            double t2 = Now();
            var anim = Wire().Where(m => !m.Out && m.Tag == Tag(RobotMessageId.AnimState) && m.T >= t0 && m.T <= t2).Select(m => Parse(m.Payload) as AnimationState).Where(a => a is not null).Select(a => a!).ToList();
            string? secs = Ask("FACE_ROWS", $"SWEEP_{interval}MS_SECONDS", $"About how many seconds from the bar starting to move until it stopped (host sent {M3M4Hw.FaceSweepFrames} frames over {M3M4Hw.FaceSweepFrames * interval / 1000.0:F1} s)? Type a number, or blank.", new[] { "seconds" }, photo: false);
            var first = anim.FirstOrDefault(); var last = anim.LastOrDefault();
            sweeps.Add(new JsonObject
            {
                ["hostIntervalMs"] = interval, ["frames"] = payloads.Length, ["framesSent"] = robot.Display.FramesSent - sent0, ["payloadBytesEach"] = payloads.Select(p => p.Length).Distinct().Order().ToArray().Length == 1 ? payloads[0].Length : null,
                ["hostSendSpanMs"] = Num(sendTimes.Count > 1 ? sendTimes[^1] - sendTimes[0] : double.NaN), ["operatorSeconds"] = secs,
                ["animationStates"] = anim.Count,
                ["animationStateSamples"] = new JsonArray(anim.Take(200).Select(a => (JsonNode)new JsonObject { ["robotTs"] = a.Timestamp, ["animBytesPlayed"] = a.NumAnimBytesPlayed, ["audioFrames"] = a.NumAudioFramesPlayed, ["tag"] = a.Tag, ["drops"] = a.ClientDropCount }).ToArray()),
                ["counterDeltaBytes"] = first is null || last is null ? null : last.NumAnimBytesPlayed - first.NumAnimBytesPlayed,
                ["counterDeltaRobotMs"] = first is null || last is null ? null : unchecked((long)(last.Timestamp - first.Timestamp)),
                ["note"] = "whether NumAnimBytesPlayed counts face-image bytes of a bare FaceImage stream (no StartOfAnimation) is not established (FACE check note in control-check); the counters are recorded, not interpreted",
            });
        }
        res["sweeps"] = sweeps;
        robot.Display.Clear();
        res["operator"] = OperatorJson("FACE_ROWS");

        if (_cancelled || patterns.Count < M3M4Hw.FaceSweepIntervalsMs.Length) { }
        if (patterns.Count == FacePatterns.Length && unanswered == 0)
        {
            res["verdict"] = disagree == 0 ? "OBSERVED_ROW_MAPPING_MATCHES_ENGINE_MODEL" : "OBSERVED_ROW_MAPPING_DIFFERS_FROM_ENGINE_MODEL";
            res["verdictRule"] = $"all {FacePatterns.Length} patterns answered: {agree} agree with the engine model (pair bit0 = row 2k, bit1 = row 2k+1), {disagree} do not; an operator reading, not a measurement";
        }
        else
        {
            res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = $"{unanswered} of {FacePatterns.Length} patterns unanswered or could-not-tell";
            missing.Add("operator answers (with photos where possible) for every pattern; with --yes nothing is asked");
        }
        missing.Add("playback period: the robot's per-frame display time is not derivable from the recorded counters without the unestablished byte-counting assumption; read sweeps[].operatorSeconds and animationStateSamples together");
        res["missing"] = missing;
        return res;
    }

    // ------------------------------------------------------------------ M4-013: MOTOR_CAL

    private JsonObject MotorCalCheck()
    {
        var robot = _robot!;
        var res = new JsonObject(); var missing = new JsonArray();
        bool done = Poll(() => robot.State.CalibrationComplete, M3M4Hw.CalibrationWaitMs, () => _cancelled);
        res["connectionTimeCalibration"] = new JsonObject
        {
            ["complete"] = done, ["seen"] = robot.State.CalibrationSeen, ["headCalibrated"] = robot.State.HeadCalibrated, ["liftCalibrated"] = robot.State.LiftCalibrated,
            ["motorCalibrationMessagesSoFar"] = robot.State.CountOf(RobotMessageId.MotorCalibration),
        };
        if (!done)
        {
            res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "connection-time calibration did not finish";
            missing.Add($"the robot's own head/lift calibration did not complete within {M3M4Hw.CalibrationWaitMs} ms, so a later report cannot be attributed to the request; no request was sent");
            res["missing"] = missing; return res;
        }
        Wait(M3M4Hw.CalBaselineMs);
        double baseT0 = Now() - M3M4Hw.CalBaselineMs;
        var baseline = States(baseT0, Now());
        int reportsBefore = Wire().Count(m => !m.Out && m.Tag == Tag(RobotMessageId.MotorCalibration));
        res["baseline"] = new JsonObject { ["states"] = baseline.Count, ["headRadFirst"] = baseline.Count > 0 ? Num(baseline[0].Head) : null, ["headRadLast"] = baseline.Count > 0 ? Num(baseline[^1].Head) : null, ["liftRadLast"] = baseline.Count > 0 ? Num(baseline[^1].Lift) : null, ["motorCalibrationReportsBefore"] = reportsBefore };

        JsonObject Request(string what, bool head, bool liftMotor)
        {
            Console.WriteLine($"\n{new string('=', 66)}\n  ASKING NOW: StartMotorCalibration head={(head ? 1 : 0)} lift={(liftMotor ? 1 : 0)}  -  WATCH {what}\n{new string('=', 66)}");
            double tReq = Now();
            robot.Motion.RequestMotorCalibration(head, liftMotor);
            Wait(M3M4Hw.CalWatchMs);
            double tEnd = Now();
            var wire = Wire();
            var reports = wire.Where(m => !m.Out && m.Tag == Tag(RobotMessageId.MotorCalibration) && m.T >= tReq && m.T <= tEnd).Select(m => (m, c: (MotorCalibration)Parse(m.Payload)!)).ToList();
            var sts = States(tReq, tEnd);
            var motor = head ? MotorID.MOTOR_HEAD : MotorID.MOTOR_LIFT;
            bool started = reports.Any(x => x.c.MotorID == motor && x.c.CalibStarted), finished = reports.Any(x => x.c.MotorID == motor && !x.c.CalibStarted);
            float span = sts.Count == 0 ? 0 : (head ? sts.Max(s => s.Head) - sts.Min(s => s.Head) : sts.Max(s => s.Lift) - sts.Min(s => s.Lift));
            string? ans = Ask("MOTOR_CAL", head ? "HEAD_MOVED" : "LIFT_MOVED", $"Did the {(head ? "head" : "lift")} visibly move (go to its stop and come back) after the line ASKING NOW?", new[] { "yes", "no", "cannot-tell" }, photo: false);
            string verdict = started && finished ? "OBSERVED_REPORTS_STARTED_AND_FINISHED" : started ? "OBSERVED_STARTED_NOT_FINISHED_IN_WINDOW" : span > 0.05f || ans == "yes" ? "OBSERVED_MOTION_WITHOUT_REPORTS" : sts.Count < 20 ? "INCONCLUSIVE" : "OBSERVED_NO_REPORT_NO_MOTION_IN_WINDOW";
            return new JsonObject
            {
                ["requestedAfterConnectMs"] = Num(tReq), ["watchMs"] = Num(tEnd - tReq), ["verdict"] = verdict,
                ["verdictRule"] = "started+finished MotorCalibration for the motor after the request; else started only; else a joint angle range > 0.05 rad or an operator 'yes' without reports; else nothing seen in the window (not a proof the request is ignored); fewer than 20 states = INCONCLUSIVE",
                ["reportsAfterRequest"] = new JsonArray(reports.Select(x => (JsonNode)new JsonObject { ["afterRequestMs"] = Num(x.m.T - tReq), ["motor"] = x.c.MotorID.ToString(), ["started"] = x.c.CalibStarted, ["auto"] = x.c.AutoStarted }).ToArray()),
                ["jointAngleRangeRadInWindow"] = Num(span), ["states"] = sts.Count,
                ["stateTrace"] = new JsonArray(sts.Where((s, i) => i % 3 == 0).Select(s => (JsonNode)new JsonObject { ["afterRequestMs"] = Num(s.T - tReq), ["robotTs"] = s.Ts, ["head"] = Num(s.Head), ["lift"] = Num(s.Lift), ["status"] = $"0x{s.Status:x}" }).ToArray()),
                ["operatorAnswer"] = ans,
            };
        }
        var headRes = Request("HIS HEAD", head: true, liftMotor: false);
        res["head"] = headRes;
        JsonObject? liftRes = null;
        if (_lift && !_cancelled) { Wait(1500); liftRes = Request("HIS LIFT (it travels to its stop; keep hands clear)", head: false, liftMotor: true); res["lift"] = liftRes; }
        else res["liftNote"] = "lift not requested (--include-lift not given): the lift request moves the lift through its full travel";
        res["operator"] = OperatorJson("MOTOR_CAL");
        string hv = headRes["verdict"]!.GetValue<string>();
        res["verdict"] = hv; res["verdictRule"] = "the head request's verdict (see head); the lift request, if run, has its own verdict under lift";
        if (hv == "INCONCLUSIVE") { missing.Add("too few RobotStates in the watch window to judge motion"); res["missing"] = missing; }
        return res;
    }

    // ------------------------------------------------------------------ M4-024: CUBE_TELEM

    private static readonly RobotMessageId[] CubeTelemetryTags =
    {
        RobotMessageId.ObjectAccel, RobotMessageId.ActiveObjectMoved, RobotMessageId.ActiveObjectStopped, RobotMessageId.ActiveObjectTapped,
        RobotMessageId.DataDump, RobotMessageId.PickAndPlaceResult, RobotMessageId.ObjectTappedFiltered, RobotMessageId.ObjectPowerLevel,
        RobotMessageId.ActiveObjectUpAxisChanged,
    };

    private async Task<JsonObject> CubeCheck()
    {
        var robot = _robot!;
        var res = new JsonObject(); var missing = new JsonArray();
        bool pool = robot.Cubes.Connections.AutoBlockPoolEnabled;
        res["autoBlockPoolEnabled"] = pool;
        Console.WriteLine($"waiting up to {_cubeWaitS} s for a cube to connect (wake it: tap it or put it on the charger and take it off)");
        double tStart = Now();
        var cube = await robot.Cubes.WaitForConnectionAsync(TimeSpan.FromSeconds(_cubeWaitS));
        res["cubesHeard"] = new JsonArray(robot.Cubes.DiscoveredCubes.Select(c => (JsonNode)CubeJson(c)).ToArray());
        if (cube?.ObjectId is not { } oid)
        {
            res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "no cube connected";
            missing.Add($"no cube connected within {_cubeWaitS} s ({robot.Cubes.DiscoveredCubes.Count} heard); the question needs a connected cube"); res["missing"] = missing;
            return res;
        }
        double tConn = Now();
        res["cube"] = CubeJson(cube); res["connectedAfterWaitStartMs"] = Num(tConn - tStart);
        var tele = CubeTelemetryTags.Select(t => (byte)t).ToHashSet();

        JsonObject Window(string name, double t0, double t1) {
            var w = Wire().Where(m => !m.Out && tele.Contains(m.Tag) && m.T >= t0 && m.T <= t1).ToList();
            lock (_windows) _windows.Add(("CUBE_TELEM." + name, t0, t1));
            var accel = w.Where(m => m.Tag == Tag(RobotMessageId.ObjectAccel)).ToList();
            return new JsonObject
            {
                ["window"] = name, ["ms"] = Num(t1 - t0), ["telemetryMessages"] = w.Count,
                ["byTag"] = Histogram(w.Select(m => $"0x{m.Tag:X2} {TagName(m.Tag)}")),
                ["objectAccelCount"] = accel.Count, ["objectAccelPerSecond"] = Num(accel.Count * 1000.0 / Math.Max(1, t1 - t0)),
                ["firstObjectAccelAfterWindowStartMs"] = accel.Count == 0 ? null : Num(accel[0].T - t0),
                ["first"] = new JsonArray(w.Take(5).Select(m => (JsonNode)MsgJson(m, t0)).ToArray()),
            };
        }
        // Everything between the connection and the first telemetry: what precedes it.
        var all = Wire();
        var firstTele = all.Where(m => !m.Out && tele.Contains(m.Tag)).Select(m => (double?)m.T).FirstOrDefault();
        double precedeEnd = firstTele ?? Now();
        res["messagesBeforeFirstTelemetry"] = new JsonObject
        {
            ["firstTelemetryAfterCubeConnectedMs"] = firstTele is null ? null : Num(firstTele.Value - tConn),
            ["fromCubeConnectedToFirstTelemetry"] = new JsonArray(all.Where(m => m.T >= tConn - 1000 && m.T <= precedeEnd && m.Tag != Tag(RobotMessageId.State) && m.Tag != Tag(RobotMessageId.Image) && m.Tag != Tag(RobotMessageId.AnimState)).Take(300).Select(m => (JsonNode)MsgJson(m, tConn)).ToArray()),
            ["note"] = "RobotState, image chunks and animation states are left out of this list; frames.jsonl has everything",
        };

        // Phase 1: cube connected, no StreamObjectAccel sent. Quiet, then moved.
        Console.WriteLine($"\nPHASE 1 ({M3M4Hw.CubeQuietMs / 1000} s): keep the cube STILL on the table. No accelerometer stream has been requested.");
        double a0 = Now(); Wait(M3M4Hw.CubeQuietMs); double a1 = Now();
        Console.WriteLine($"PHASE 1b ({M3M4Hw.CubeMoveMs / 1000} s): MOVE and TAP the cube now (lift it, tap it, put it down).");
        Wait(1500); double b0 = Now(); Wait(M3M4Hw.CubeMoveMs); double b1 = Now();
        var p1a = Window("P1a-still-no-stream", a0, a1); var p1b = Window("P1b-moved-no-stream", b0, b1);
        // Phase 2: production StreamObjectAccel path (CubeAccelStreams.AddListener, M9-017).
        var listener = new CubeShakeListener(CubeShakeListener.SingingFilterCoefficient, CubeShakeListener.SingingLowThreshold, CubeShakeListener.SingingHighThreshold, _ => { });
        double tAdd = Now();
        robot.CubeAccel.AddListener(oid, listener);
        Console.WriteLine($"\nPHASE 2a ({M3M4Hw.CubeQuietMs / 1000} s): StreamObjectAccel ON sent. Keep the cube STILL.");
        double c0 = Now(); Wait(M3M4Hw.CubeQuietMs); double c1 = Now();
        Console.WriteLine($"PHASE 2b ({M3M4Hw.CubeMoveMs / 1000} s): MOVE and TAP the cube now.");
        double d0 = Now(); Wait(M3M4Hw.CubeMoveMs); double d1 = Now();
        var p2a = Window("P2a-still-stream-on", c0, c1); var p2b = Window("P2b-moved-stream-on", d0, d1);
        // Phase 3: stream off.
        double tRem = Now();
        robot.CubeAccel.RemoveListener(oid, listener);
        Console.WriteLine($"\nPHASE 3 ({M3M4Hw.CubeAfterOffMs / 1000} s): StreamObjectAccel OFF sent. Keep MOVING the cube gently.");
        Wait(M3M4Hw.CubeAfterOffMs); double e1 = Now();
        var p3 = Window("P3-after-stream-off", tRem + 500, e1);
        res["streamObjectAccelSends"] = new JsonArray(Wire().Where(m => m.Out && m.Tag == Tag(RobotMessageId.StreamObjectAccel)).Select(m => (JsonNode)MsgJson(m, tConn)).ToArray());
        res["streamObjectAccelOnAfterCubeConnectedMs"] = Num(tAdd - tConn); res["streamObjectAccelOffAfterCubeConnectedMs"] = Num(tRem - tConn);
        res["windows"] = new JsonArray(p1a, p1b, p2a, p2b, p3);
        string? ans = Ask("CUBE_TELEM", "CUBE_MOVED_AS_PROMPTED", "Did you move/tap the cube during phases 1b, 2b and 3 as prompted?", new[] { "yes", "partly", "no" }, photo: false);
        res["operator"] = OperatorJson("CUBE_TELEM");

        int n1 = p1a["objectAccelCount"]!.GetValue<int>() + p1b["objectAccelCount"]!.GetValue<int>();
        int n2 = p2a["objectAccelCount"]!.GetValue<int>() + p2b["objectAccelCount"]!.GetValue<int>();
        int n3 = p3["objectAccelCount"]!.GetValue<int>();
        if (ans is "no") { res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "the operator did not move the cube as prompted"; missing.Add("a run with the cube moved in 1b/2b/3"); }
        else if (n1 > 0)
        { res["verdict"] = "OBSERVED_ACCEL_FORWARDED_WITHOUT_REQUEST"; res["verdictRule"] = $"{n1} ObjectAccel in phase 1 with no StreamObjectAccel sent; phase 2 {n2}, phase 3 {n3}"; }
        else if (n2 > 0 && n3 == 0)
        { res["verdict"] = "OBSERVED_ACCEL_ONLY_WITH_STREAM_ON"; res["verdictRule"] = $"no ObjectAccel in phase 1; {n2} in phase 2 (stream on); none after the stream was turned off"; }
        else if (n2 > 0)
        { res["verdict"] = "OBSERVED_ACCEL_ON_AFTER_STREAM_REQUEST_CONTINUES_AFTER_OFF"; res["verdictRule"] = $"no ObjectAccel in phase 1; {n2} in phase 2; {n3} still arrived 500 ms after the off request"; }
        else
        { res["verdict"] = "OBSERVED_NO_ACCEL_EVER"; res["verdictRule"] = "no ObjectAccel in any phase, stream on included (other telemetry counts are in windows)"; }
        if (missing.Count > 0) res["missing"] = missing;
        return res;
    }

    // ------------------------------------------------------------------ bundle

    // fidelity: M3-050 — every received ImageChunk's encoding is recorded per run (5/6/7 flagged).
    internal static JsonObject ImageEncodingObservation(ImageChunk chunk) => new()
    {
        ["record"] = "M3-050", ["imageId"] = chunk.ImageId, ["chunkId"] = chunk.ChunkId,
        ["encoding"] = chunk.ImageEncoding, ["flag567"] = chunk.ImageEncoding is 5 or 6 or 7,
    };

    private string WriteBundle()
    {
        if (Interlocked.Exchange(ref _written, 1) == 1) return "ERROR";
        Directory.CreateDirectory(_bundle);
        var inWindows = _windows.ToArray();
        var framesOut = new StringBuilder();
        foreach (var f in Frames().OrderBy(f => f.T))
        {
            var o = new JsonObject { ["t"] = Num(f.T), ["utc"] = f.Utc.ToString("o", CultureInfo.InvariantCulture), ["dir"] = f.Out ? "out" : "in", ["len"] = f.Raw.Length };
            bool imageOnly = false;
            if (f.Frame is { } fr)
            {
                o["type"] = (byte)fr.Type; o["typeName"] = fr.Type.ToString(); o["seqMin"] = fr.SeqMin; o["seqMax"] = fr.SeqMax; o["ack"] = fr.Ack;
                var subs = new JsonArray(); imageOnly = fr.Messages.Count > 0;
                foreach (var m in fr.Messages)
                {
                    var s = new JsonObject { ["type"] = (byte)m.Type, ["name"] = m.Type.ToString(), ["seq"] = m.Seq, ["size"] = m.Payload.Length };
                    bool data = m.Type is ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage && m.Payload.Length > 0;
                    if (data) { s["tag"] = $"0x{m.Payload[0]:X2}"; s["msg"] = TagName(m.Payload[0]); }
                    if (!(data && m.Payload[0] == Tag(RobotMessageId.Image))) imageOnly = false;
                    subs.Add(s);
                }
                o["subs"] = subs;
            }
            if (f.Error is not null) o["error"] = f.Error;
            bool inWin = inWindows.Any(w => w.Name.StartsWith("COLOR_IMAGES", StringComparison.Ordinal) && f.T >= w.From && f.T <= w.To);
            if (!f.Out && imageOnly && !inWin) o["hexOmitted"] = "image chunks outside the COLOR_IMAGES phases (their frames are saved under camera/)";
            else o["hex"] = Convert.ToHexString(f.Raw);
            framesOut.AppendLine(o.ToJsonString());
        }
        File.WriteAllText(Path.Combine(_bundle, "frames.jsonl"), framesOut.ToString(), Encoding.UTF8);
        var ev = new StringBuilder();
        foreach (var e in _events.ToArray().OrderBy(e => e.T)) ev.AppendLine(new JsonObject { ["t"] = Num(e.T), ["kind"] = e.Kind, ["data"] = e.Data.DeepClone() }.ToJsonString());
        File.WriteAllText(Path.Combine(_bundle, "events.jsonl"), ev.ToString(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(_bundle, "records.json"), new JsonArray(_records.Select(r => (JsonNode)r.DeepClone()).ToArray()).ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

        // M3-050: inspect every received ImageChunk in every phase, including retransmissions.
        var encodings = new StringBuilder();
        foreach (var frame in Frames().Where(f => !f.Out && f.Frame is not null).OrderBy(f => f.T))
            foreach (var message in frame.Frame!.Messages)
                // Only data sub-messages: other sub-message types whose first byte happens to equal the Image tag misparsed (2026-10-12 run).
                if (message.Type is ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage
                    && message.Payload.Length > 0 && message.Payload[0] == Tag(RobotMessageId.Image) && Parse(message.Payload) is ImageChunk chunk)
                {
                    var row = ImageEncodingObservation(chunk);
                    row["t"] = Num(frame.T);
                    row["phase"] = inWindows.FirstOrDefault(w => frame.T >= w.From && frame.T <= w.To).Name ?? "outside-check-windows";
                    encodings.AppendLine(row.ToJsonString());
                }
        File.WriteAllText(Path.Combine(_bundle, "image-encodings.jsonl"), encodings.ToString(), Encoding.UTF8);
        JsonObject[] checks; lock (_checkResults) checks = _checkResults.ToArray();
        string overall = _exceptions.Count > 0 && checks.Length == 0 ? "ERROR" : checks.Any(c => c["verdict"]!.GetValue<string>() == "INCONCLUSIVE") ? "REVIEW_REQUIRED_SOME_INCONCLUSIVE" : "REVIEW_REQUIRED";
        var result = new JsonObject
        {
            ["test"] = M3M4Hw.TestId, ["script"] = "m3m4-hw-2",
            ["overall"] = overall,
            ["overallRule"] = "never PASS: every check is OBSERVED_* or INCONCLUSIVE and the manager judges the bundle; no verdict settles a record or raises provenance",
            ["operatorLabel"] = _operator,
            ["records"] = new JsonArray(_records.Select(r => (JsonNode)new JsonObject { ["id"] = r["id"]!.DeepClone(), ["status"] = r["status"]!.DeepClone(), ["title"] = r["title"]!.DeepClone(), ["unresolved"] = r["unresolved"]?.DeepClone() }).ToArray()),
            ["robot"] = _robotInfo.DeepClone(),
            ["checks"] = new JsonArray(checks.Select(c => (JsonNode)c.DeepClone()).ToArray()),
            ["operatorAnswers"] = OperatorAll(),
            ["warnings"] = new JsonArray(_warnings.Select(w => (JsonNode)w).ToArray()),
            ["exceptions"] = new JsonArray(_exceptions.Select(x => (JsonNode)x).ToArray()),
            ["windowsMs"] = new JsonArray(inWindows.Select(w => (JsonNode)new JsonObject { ["name"] = w.Name, ["fromMs"] = Num(w.From), ["toMs"] = Num(w.To) }).ToArray()),
        };
        File.WriteAllText(Path.Combine(_bundle, "result.json"), result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        File.WriteAllText(Path.Combine(_bundle, "env.json"), Env().ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

        // The tool source and the draft document, so the bundle carries the exact script it ran.
        try
        {
            if (_repoRoot is not null)
            {
                foreach (var (src, name) in new[] { (Path.Combine(_repoRoot, "cozmo-stack", "src", "Cozmo.Conformance", "M3M4Hw.cs"), "M3M4Hw.cs"), (Path.Combine(_repoRoot, "cozmo-stack", "src", "Cozmo.Conformance", "M3M4HwSettle.cs"), "M3M4HwSettle.cs"), (Path.Combine(_repoRoot, "re-analysis", "acceptance", "hardware", "scripts", "m3m4-hw-2.md"), "m3m4-hw-2.md") })
                    if (File.Exists(src)) { File.Copy(src, Path.Combine(_bundle, name), overwrite: true); _extraFiles.Add(name); }
            }
        }
        catch (Exception e) { lock (_exceptions) _exceptions.Add("copying the script: " + e.Message); }
        var hashes = new JsonObject();
        foreach (var f in new[] { "result.json", "env.json", "records.json", "events.jsonl", "frames.jsonl" }.Concat(_extraFiles).Distinct())
        {
            var p = Path.Combine(_bundle, f);
            if (File.Exists(p)) hashes[f.Replace('\\', '/')] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant();
        }
        File.WriteAllText(Path.Combine(_bundle, "hashes.json"), hashes.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        return overall;
    }

    private JsonArray OperatorAll() => new(_operatorAnswers.Select(o => (JsonNode)new JsonObject
    { ["key"] = $"{o.Check}.{o.Key}", ["answer"] = o.Answer, ["photo"] = o.Photo, ["photoSha256"] = o.PhotoSha256 }).ToArray());

    private JsonObject Env()
    {
        string? head = null; string[]? status = null;
        if (_repoRoot is not null)
        {
            head = Run("git", $"-C \"{_repoRoot}\" rev-parse HEAD")?.Trim();
            status = Run("git", $"-C \"{_repoRoot}\" status --porcelain")?.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        }
        string? scriptSha = _repoRoot is null ? null : Sha(Path.Combine(_repoRoot, "cozmo-stack", "src", "Cozmo.Conformance", "M3M4Hw.cs"));
        return new JsonObject
        {
            ["test"] = M3M4Hw.TestId, ["commandLine"] = "cozmo-conformance " + string.Join(' ', _argv),
            ["gitHead"] = head, ["gitDirty"] = status?.Length > 0, ["gitStatus"] = status is null ? null : new JsonArray(status.Take(100).Select(l => (JsonNode)l).ToArray()),
            ["scriptSha256"] = new JsonObject
            {
                ["cozmo-stack/src/Cozmo.Conformance/M3M4Hw.cs"] = scriptSha,
                ["cozmo-stack/src/Cozmo.Conformance/M3M4HwSettle.cs"] = _repoRoot is null ? null : Sha(Path.Combine(_repoRoot, "cozmo-stack", "src", "Cozmo.Conformance", "M3M4HwSettle.cs")),
                ["re-analysis/acceptance/hardware/scripts/m3m4-hw-2.md"] = _repoRoot is null ? null : Sha(Path.Combine(_repoRoot, "re-analysis", "acceptance", "hardware", "scripts", "m3m4-hw-2.md")),
            },
            ["assemblySha256"] = new JsonObject
            {
                ["cozmo-conformance.dll"] = Sha(typeof(M3M4Hw).Assembly.Location), ["Cozmo.Robot.dll"] = Sha(typeof(CozmoRobot).Assembly.Location),
                ["Cozmo.Transport.dll"] = Sha(typeof(ReliableTransport).Assembly.Location), ["Cozmo.Protocol.dll"] = Sha(typeof(Frame).Assembly.Location),
            },
            ["robotIp"] = _ip.ToString(), ["robotPort"] = RobotAddress.RemotePort(false), ["only"] = _only is null ? null : new JsonArray(_only.Select(x => (JsonNode)x).ToArray()),
            ["originProbe"] = _probe, ["includeLift"] = _lift, ["allowDrive"] = _allowDrive, ["cubeWaitSeconds"] = _cubeWaitS, ["yes"] = _yes,
            ["os"] = RuntimeInformation.OSDescription, ["dotnet"] = RuntimeInformation.FrameworkDescription,
            ["startUtc"] = _t0Utc.ToString("o", CultureInfo.InvariantCulture), ["endUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            ["timings"] = new JsonObject
            {
                ["connectBudgetMs"] = M3M4Hw.ConnectBudgetMs, ["colorPhaseMs"] = M3M4Hw.ColorPhaseMs, ["facePatternHoldMs"] = M3M4Hw.FacePatternHoldMs,
                ["faceSweepFrames"] = M3M4Hw.FaceSweepFrames, ["faceSweepIntervalsMs"] = new JsonArray(M3M4Hw.FaceSweepIntervalsMs.Select(x => (JsonNode)x).ToArray()),
                ["calibrationWaitMs"] = M3M4Hw.CalibrationWaitMs, ["calWatchMs"] = M3M4Hw.CalWatchMs, ["cubeQuietMs"] = M3M4Hw.CubeQuietMs, ["cubeMoveMs"] = M3M4Hw.CubeMoveMs, ["watchdogMs"] = M3M4Hw.WatchdogMs,
            },
        };
    }

    private static string? Run(string file, string args)
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
