using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>
/// Operator-run idle Freeplay check. Cozmo sits off the charger with nothing near him, Freeplay starts through the public
/// input and nothing else is asked of him. Nothing here is graded: real timing, what the camera sees and what the
/// operator sees cannot be sourced, so the whole run is an observation for the manager to judge from the bundle.
/// </summary>
public static class M13IdleHw
{
    public const string TestId = "M13-IDLE-HW";
    public const double DefaultMinutes = 4;
    public static readonly string[] RecordIds = { "M15-006", "M15-102", "M15-103", "M15-106", "M15-107", "M15-084", "M8-001", "M8-003", "M8-012", "M8-013", "M8-019" };
    public const string SetupText = "Setup:\n"
        + "1. Put Cozmo on the floor, OFF the charger, in the middle of a clear area at least 1 m across.\n"
        + "2. Put his cubes well away or out of sight. No people or pets close in front of him if you can help it.\n"
        + "3. Don't touch him from the start until TEST FINISHED. If he heads for an edge or gets stuck, press Ctrl+C.";

    public sealed record Segment(double FromMs, double ToMs, string Activity, string Behaviour);

    /// <summary>Totals a run's behaviour segments. Time with no behaviour is reported under "-".</summary>
    public static JsonObject Summarise(IReadOnlyList<Segment> segments)
    {
        double total = segments.Sum(s => s.ToMs - s.FromMs), none = segments.Where(s => s.Behaviour == "-").Sum(s => s.ToMs - s.FromMs);
        static JsonArray Table(IEnumerable<IGrouping<string, Segment>> groups) => new(groups
            .Select(g => new { g.Key, Ms = g.Sum(s => s.ToMs - s.FromMs), Count = g.Count() }).OrderByDescending(x => x.Ms)
            .Select(x => (JsonNode)new JsonObject { ["name"] = x.Key, ["seconds"] = Math.Round(x.Ms / 1000, 1), ["entered"] = x.Count }).ToArray());
        return new JsonObject
        {
            ["seconds"] = Math.Round(total / 1000, 1),
            ["secondsWithNoBehaviour"] = Math.Round(none / 1000, 1),
            ["behaviours"] = Table(segments.GroupBy(s => s.Behaviour)),
            ["activities"] = Table(segments.GroupBy(s => s.Activity)),
        };
    }

    public static string DescribeSummary(JsonObject s) =>
        $"{s["seconds"]} s recorded, {s["secondsWithNoBehaviour"]} s with no behaviour running.\nBehaviours:\n"
        + string.Join("\n", s["behaviours"]!.AsArray().Select(b => $"  {b!["name"]}: {b["seconds"]} s, started {b["entered"]} time(s)"));

    public static List<JsonObject> LoadRecords(string path)
    {
        var all = JsonNode.Parse(File.ReadAllText(path))!["records"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => x["id"]!.GetValue<string>());
        var missing = RecordIds.Where(id => !all.ContainsKey(id)).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException("Missing fidelity records: " + string.Join(", ", missing));
        return RecordIds.Select(id => (JsonObject)all[id].DeepClone()).ToList();
    }

    public static int Run(string[] args)
    {
        try
        {
            string? root = LinkCheck.FindRepoRoot(); if (root is null) throw new ArgumentException("Run inside the repository.");
            bool validate = false, yes = false; string? resources = null, output = null; double minutes = DefaultMinutes; IPAddress ip = RobotAddress.DefaultFor(false);
            for (int i = 1; i < args.Length; i++) switch (args[i])
                {
                    case "--validate": validate = true; break;
                    case "--yes": yes = true; break;
                    case "--resources": resources = args[++i]; break;
                    case "--out": output = args[++i]; break;
                    case "--minutes": minutes = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                    default: if (!IPAddress.TryParse(args[i], out var parsed)) throw new ArgumentException("Unknown argument " + args[i]); ip = parsed; break;
                }
            var records = LoadRecords(Path.Combine(root, "re-analysis", "fidelity_manifest.json"));
            if (validate) { Console.WriteLine($"{TestId}: {records.Count} valid records; no network opened."); return 0; }
            var options = EngineStartupResources.OptionsForRoot(resources);
            string bundle = Path.Combine(output ?? Path.Combine(root, "re-analysis", "acceptance", "hardware"), DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + TestId + "-" + Guid.NewGuid().ToString("N")[..6]);
            return new M13IdleRun(root, bundle, ip, options.ResourcesPath!, records, args, yes, minutes).Execute();
        }
        catch (Exception e) { Console.Error.WriteLine("m13-idle-hw: " + e.Message + "\nusage: m13-idle-hw [robot-ip] --resources <dir> [--minutes <n>] [--yes] [--out <dir>] | --validate"); return 2; }
    }
}

internal sealed class M13IdleRun
{
    readonly string root, bundle, resources; readonly IPAddress ip; readonly List<JsonObject> records; readonly string[] argv; readonly bool yes; readonly double minutes;
    readonly Stopwatch clock = Stopwatch.StartNew(); readonly ConcurrentQueue<JsonObject> events = new();
    readonly ConcurrentQueue<(double T, bool Out, byte[] Raw, Frame? Frame, string? Error)> frames = new();
    readonly ConcurrentQueue<string> errors = new(), stackLines = new(); readonly CancellationTokenSource cancel = new();
    readonly List<M13IdleHw.Segment> segments = new(); readonly JsonObject answers = new();
    CozmoRobot? robot; RobotConnectionResponse? response; bool connected; HarnessStartup.Gate? startupGate; HarnessStartup.Report? startup; Task<string?>? pendingRead;
    double Now => clock.Elapsed.TotalMilliseconds;
    public M13IdleRun(string root, string bundle, IPAddress ip, string resources, List<JsonObject> records, string[] argv, bool yes, double minutes)
    { this.root = root; this.bundle = bundle; this.ip = ip; this.resources = resources; this.records = records; this.argv = argv; this.yes = yes; this.minutes = minutes; }
    void Event(string kind, JsonObject data) { data["atMs"] = Now; data["kind"] = kind; events.Enqueue(data); }
    bool Poll(Func<bool> predicate, int ms) { double end = Now + ms; while (Now < end) { if (predicate()) return true; if (cancel.Token.WaitHandle.WaitOne(10)) cancel.Token.ThrowIfCancellationRequested(); } return predicate(); }
    JsonObject[] EngineLogs() => events.Where(e => e["kind"]?.GetValue<string>() == "engine-log").ToArray();
    string? ReadLineBounded()
    {
        if (pendingRead is { IsCompleted: true }) pendingRead = null;
        var read = pendingRead ??= Task.Run(Console.ReadLine);
        while (!read.IsCompleted) { if (cancel.IsCancellationRequested) return null; Thread.Sleep(50); }
        pendingRead = null; return read.GetAwaiter().GetResult();
    }

    private CozmoEngineOptions? engineOptions;

    public int Execute()
    {
        Directory.CreateDirectory(bundle);
        Console.WriteLine($"LEFT-ALONE FREEPLAY TEST ({minutes:0.#} minutes)\n\n"
            + "What this is for: Cozmo is started in Freeplay and left alone. The program records what he chooses to do\n"
            + "and everything sent to him. Nothing passes or fails here; the recording is reviewed afterwards.\n\n"
            + M13IdleHw.SetupText + "\n\nCtrl+C stops the run at any time; everything recorded so far is still saved.\nResults folder: " + bundle);
        ConsoleCancelEventHandler interrupt = (_, e) => { e.Cancel = true; cancel.Cancel(); }; Console.CancelKeyPress += interrupt;
        using var timer = new Timer(_ => cancel.Cancel(), null, TimeSpan.FromMinutes(minutes + 3), Timeout.InfiniteTimeSpan);
        try
        {
            if (!yes) { Console.WriteLine("\nWhen Cozmo is set up, press Enter to connect. Connecting takes up to a minute."); ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested(); }
            using var resolution = new HighResolutionTimer();
            robot = CozmoRobot.Create(engineOptions: engineOptions = HardwareEngineOptions.Create(resources, log: errors.Enqueue, beforeStart: e =>
            {
                // What the robot had stored before this run changes anything.
                e.Needs.RobotReadObserved += (result, data) => { var body = new JsonObject { ["result"] = result, ["hex"] = Convert.ToHexString(data) }; try { File.WriteAllText(Path.Combine(bundle, "needs-nv-read.json"), body.ToJsonString()); } catch (Exception x) { errors.Enqueue("needs NV read not saved: " + x.Message); } };
                EngineEvidence.Subscribe(e, line => { }, Event);
                e.StackLogLine += stackLines.Enqueue;
                e.ActionCompleted += a => Event("action-completed", JsonSerializer.SerializeToNode(a)!.AsObject());
            }));
            robot.Transport.FrameTrace += f => frames.Enqueue((Now, f.Outbound, (byte[])f.Raw.Clone(), f.Frame, f.Error));
            robot.Engine.ConnectionResponse += r => { response = r; Event("connection-response", new() { ["result"] = r.Result.ToString(), ["firmware"] = r.FwVersion }); };
            robot.HandlerFaulted += e => Event("handler-fault", new() { ["exception"] = e.ToString() });
            robot.Sensors.OnChargerPlatformChanged += on => Event("platform-changed", new() { ["onPlatform"] = on });
            robot.Sensors.ChargerEvent += on => Event("charger-contacts", new() { ["onContacts"] = on });
            robot.Sensors.CliffDetected += c => Event("cliff-detected", JsonSerializer.SerializeToNode(c)!.AsObject());
            robot.Sensors.RobotStopped += () => Event("robot-stopped", new());
            robot.Sensors.OffTreadsStateChanged += (from, to) => Event("off-treads", new() { ["from"] = from.ToString(), ["to"] = to.ToString() });
            // fidelity: M2-021 — ConnectToRobot only after delivered readiness, as CozmoRobot.ConnectAsync does.
            startupGate = HarnessStartup.ConnectAfterReadiness(robot, () => robot.ConnectToRobot(ip), () => cancel.IsCancellationRequested);
            Event("startup-gate", startupGate.ToJson()); Console.WriteLine("Startup: " + startupGate.Describe());
            if (!startupGate.Ready) errors.Enqueue("startup: " + startupGate.Describe());
            connected = startupGate.Ready && Poll(() => response?.Result == RobotConnectionResult.Success && robot.Engine.Robot is { TimeSynced: true, FirstFullStateHandled: true }, 15000);
            if (connected) { bool assets = Poll(() => robot.Animations.ClipNames.Count > 0, 15000); bool calibrated = Poll(() => robot.State.CalibrationComplete, 20000); Event("check-readiness", new() { ["assets"] = assets, ["calibrated"] = calibrated }); connected = assets && calibrated; }
            startup = HarnessStartup.Analyse(EngineLogs(), startupGate, connected ? HarnessStartup.HasCachedNeutralFace(robot) : null, connected ? null : "connection or calibration did not complete");
            Event("startup-report", startup.ToJson()); Console.WriteLine("Startup: " + startup.Describe());
            if (!connected) throw new InvalidOperationException("Not connected; nothing ran.");
            bool onContacts = robot.State.Latest?.Has(RobotStatusFlag.IsOnCharger) == true;
            Event("start-position", new() { ["onContacts"] = onContacts, ["platform"] = robot.Sensors.OnChargerPlatform });
            if (onContacts) Console.WriteLine("Cozmo is on the charger, so he will start by driving off it. That is fine; the run carries on.");
            Console.WriteLine($"\n>>> WATCH COZMO for {minutes:0.#} minutes. He starts right away. Don't touch him. <<<");
            LeftAlone(robot);
            Console.WriteLine(">>> STOP WATCHING. <<<");
            if (!yes)
            {
                Ask("keptBusy", "Did Cozmo keep doing things (looking around, moving, animating) for most of the run? [yes/no/partly]");
                Ask("longestStill", "About how long was the longest time he just sat still? (seconds, or 'don't know')");
                Ask("odd", "Anything odd? Type it in your own words, or just press Enter.");
            }
        }
        catch (OperationCanceledException) { errors.Enqueue("Stopped early with Ctrl+C or the time limit."); }
        catch (Exception e) { errors.Enqueue(e.ToString()); }
        finally
        {
            if (robot is not null)
            {
                try { if (connected) robot.Motion.StopAllMotors(); } catch (Exception e) { errors.Enqueue("StopAllMotors: " + e.Message); }
                var d = Task.Run(robot.Dispose); if (!d.Wait(5000)) errors.Enqueue("Dispose did not finish in 5 seconds");
            }
            Console.CancelKeyPress -= interrupt;
        }
        var summary = M13IdleHw.Summarise(segments);
        WriteBundle(summary);
        Console.WriteLine("\n=== TEST FINISHED ===\n" + M13IdleHw.DescribeSummary(summary) + "\n\nNothing passes or fails here. Send this folder for review: " + bundle);
        return 0;
    }

    void Ask(string field, string question)
    {
        Console.WriteLine("\n" + question);
        string? raw = ReadLineBounded(); answers[field] = raw;
        Event("operator-observation", new() { ["field"] = field, ["question"] = question, ["answer"] = raw });
    }

    /// <summary>Public inputs only: ordinary Freeplay, no Spark, no forced behaviour. What the engine chooses is recorded.</summary>
    void LeftAlone(CozmoRobot r)
    {
        using var stack = FreeplayStack.CreateNative(r);
        r.ActivateHighLevelActivity("Freeplay");
        double end = Now + minutes * 60000, from = Now; string activity = "-", behaviour = "-";
        try
        {
            while (Now <= end && !cancel.IsCancellationRequested)
            {
                var seen = stack.Tick(0, 0, r, null, null);
                string a = seen.Activity ?? "-", b = seen.Behavior ?? "-";
                if (a != activity || b != behaviour)
                {
                    segments.Add(new(from, Now, activity, behaviour));
                    from = Now; activity = a; behaviour = b;
                    Event("freeplay-state", new() { ["activity"] = a, ["behaviour"] = b });
                    Console.WriteLine($"  {Now / 1000,6:F1}s  {a} / {b}");
                }
                Thread.Sleep(33);
            }
        }
        finally { segments.Add(new(from, Now, activity, behaviour)); }
    }

    void WriteBundle(JsonObject summary)
    {
        try
        {
            var wire = M5HwAnalysis.OrderedUnique(M5HwAnalysis.ReadWire(frames.ToArray().Select((f, index) => new M5HwAnalysis.CapturedFrame(index + 1, f.T, f.Out, f.Frame)))).ToList();
            var missing = stackLines.Where(l => l.Contains("MISSING", StringComparison.Ordinal)).Distinct().ToArray();
            var sent = wire.Where(m => m.Outbound && m.Payload.Length > 0).GroupBy(m => $"0x{m.Payload[0]:X2}").OrderByDescending(g => g.Count())
                .Select(g => (JsonNode)new JsonObject { ["tag"] = g.Key, ["count"] = g.Count() }).ToArray();
            File.WriteAllText(Path.Combine(bundle, "SUMMARY.txt"), "Left-alone Freeplay test. Nothing here passes or fails.\n\n" + M13IdleHw.DescribeSummary(summary)
                + "\n\nStartup: " + (startup?.Describe() ?? "not completed")
                + "\n\nYour notes: " + answers.ToJsonString()
                + "\n\nKnown gaps reported: " + missing.Length
                + "\n\nRun errors:\n" + (errors.IsEmpty ? "None." : string.Join("\n", errors)));
            File.WriteAllLines(Path.Combine(bundle, "frames.jsonl"), frames.Select(f => new JsonObject { ["atMs"] = f.T, ["outbound"] = f.Out, ["rawBase64"] = Convert.ToBase64String(f.Raw), ["error"] = f.Error }.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "messages.jsonl"), wire.Select(m => new JsonObject { ["atMs"] = m.AtMs, ["outbound"] = m.Outbound, ["tag"] = $"0x{m.Payload[0]:X2}", ["payloadHex"] = Convert.ToHexString(m.Payload), ["frameLine"] = m.FrameLine, ["submessageOrdinal"] = m.SubmessageOrdinal, ["reliableSequence"] = m.ReliableSequence, ["connectionEpoch"] = m.ConnectionEpoch }.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "events.jsonl"), events.Select(e => e.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "stack-log.txt"), stackLines);
            if (engineOptions?.NeedsDeviceDirectory is { } nurture && Directory.Exists(nurture))
            {
                string copy = Directory.CreateDirectory(Path.Combine(bundle, "nurture")).FullName;
                foreach (string file in Directory.GetFiles(nurture))
                    try { File.Copy(file, Path.Combine(copy, Path.GetFileName(file)), true); } catch (IOException x) { errors.Enqueue("nurture file not copied: " + x.Message); }
            }
            var files = new JsonArray();
            foreach (var relative in new[] { "cozmo-stack/src/Cozmo.Conformance/M13IdleHw.cs", "cozmo-stack/src/Cozmo.Conformance/HardwareEngineOptions.cs", "cozmo-stack/src/Cozmo.Conformance/HarnessStartup.cs", "re-analysis/acceptance/hardware/scripts/m13-idle-hw-1.md", "re-analysis/fidelity_manifest.json" })
            {
                string source = Path.Combine(root, relative); if (!File.Exists(source)) { errors.Enqueue("Missing provenance file " + relative); continue; }
                string dest = Path.Combine(bundle, "provenance", relative); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(source, dest, true);
                files.Add(new JsonObject { ["path"] = relative, ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant() });
            }
            string? commit = null; try { var p = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true }); commit = p!.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(); } catch (Exception e) { errors.Enqueue("git: " + e.Message); }
            var report = new JsonObject { ["testId"] = M13IdleHw.TestId, ["createdUtc"] = DateTime.UtcNow.ToString("O"), ["commit"] = commit, ["argv"] = JsonSerializer.SerializeToNode(argv),
                ["resources"] = resources, ["connected"] = connected, ["startup"] = startup?.ToJson(), ["summary"] = summary, ["operator"] = answers.DeepClone(), ["sentByTag"] = new JsonArray(sent),
                ["missingReported"] = JsonSerializer.SerializeToNode(missing), ["records"] = JsonSerializer.SerializeToNode(records), ["provenance"] = files, ["errors"] = JsonSerializer.SerializeToNode(errors),
                ["acceptance"] = "NOT_JUDGED: manager review required; hardware success does not raise provenance" };
            File.WriteAllText(Path.Combine(bundle, "result.json"), report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(bundle, "README.txt"), "M13-IDLE-HW evidence bundle. result.json holds the summary and source records; messages.jsonl deduplicates reliable resends; frames.jsonl keeps every datagram; events.jsonl keeps engine logs and API events; stack-log.txt keeps the stack's own lines. Times are host capture times. No acceptance or manifest change was made.");
        }
        catch (Exception e) { Console.Error.WriteLine("Could not write the results folder: " + e.Message); }
    }
}
