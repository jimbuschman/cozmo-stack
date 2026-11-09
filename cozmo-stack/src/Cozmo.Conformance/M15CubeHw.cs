using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>
/// Operator-run cube pickup and put-down check through the outside-AI controls (AI-BODY-CUBE-PICKUP-1,
/// GROUND-AT-POSE-SETTLEMENT-1). The engine's own light-cube observation report picks the target; the public
/// <see cref="CozmoMotion.QueueAiPickupCubeAsync"/> picks it up (first in place, then with a drive) and the public
/// 32 mm <see cref="CozmoMotion.QueueAiLiftHeightAsync"/> puts it down through the native ground action (M15-022).
/// Nothing is graded. The bundle records each action's engine result, the robot's IS_PICKING_OR_PLACING /
/// IS_CARRYING_BLOCK / IS_MOVING transitions, and the dock (0x42), place (0x44), docking-correction (0x48) and
/// PickAndPlaceResult (0xB8) messages in host capture order, for the manager to judge. The firmware's busy/release
/// order against PickAndPlaceResult is the M15-022 hardware observation.
/// </summary>
public static class M15CubeHw
{
    public const string TestId = "M15-CUBE-HW";
    public static readonly string[] RecordIds = { "M15-022", "M12-015", "M12-052", "M12-005", "M12-008", "M12-013", "M12-014", "M11-038", "M4-003", "M8-015" };
    public const float PlaceHeightMm = 32f;
    /// <summary>Head angle for looking at a cube on the table 10..25 cm ahead (about -9 degrees). Run 1 (2026-11-09) kept the
    /// default -0.42 rad, which put the cube at the image's top edge with its marker cut off. Not a source value.</summary>
    public const float LookHeadRad = -0.15f;
    public const string SetupText = "Setup:\n"
        + "1. Put Cozmo on a clear table, well away from the edge, NOT on his charger. He will move a little.\n"
        + "2. Put ONE light cube on the table about 10 cm (4 in) straight in front of his face, a marker side facing him.\n"
        + "   Keep the other cubes out of his view.\n"
        + "3. Good, even light helps him see the marker.";
    public const string MoveText = "Now move the cube about 25 cm (10 in) in front of him and a little to one side,\n"
        + "a marker side still facing roughly toward him. He will drive to it. Press Enter when it is in place.";

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
            bool validate = false, yes = false, withDrive = false; string? resources = null, output = null; IPAddress ip = RobotAddress.DefaultFor(false);
            for (int i = 1; i < args.Length; i++) switch (args[i])
                {
                    case "--validate": validate = true; break;
                    case "--yes": yes = true; break;
                    case "--with-drive": withDrive = true; break;
                    case "--resources": resources = args[++i]; break;
                    case "--out": output = args[++i]; break;
                    default: if (!IPAddress.TryParse(args[i], out var parsed)) throw new ArgumentException("Unknown argument " + args[i]); ip = parsed; break;
                }
            var records = LoadRecords(Path.Combine(root, "re-analysis", "fidelity_manifest.json"));
            if (validate) { Console.WriteLine($"{TestId}: {records.Count} valid records; no network opened."); return 0; }
            if (Robot.Emulation.ShippedLibraries.FindLibDir() is null)
                throw new ArgumentException("The shipped libraries (resources/lib/armeabi-v7a, libcozmoEngine.so) are not on this PC.");
            if (!Robot.Vision.MarkerLibrary.IsAvailable)
                throw new ArgumentException("The cube marker library is not on this PC, so Cozmo cannot read cube markers. With Python installed and resources/lib/armeabi-v7a/libcozmoEngine.so present, run: python re-analysis/tools/extract_marker_library.py resources/lib/armeabi-v7a/libcozmoEngine.so, then rebuild.");
            var options = EngineStartupResources.OptionsForRoot(resources);
            string bundle = Path.Combine(output ?? Path.Combine(root, "re-analysis", "acceptance", "hardware"), DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + TestId + "-" + Guid.NewGuid().ToString("N")[..6]);
            return new M15CubeRun(root, bundle, ip, options.ResourcesPath!, records, args, yes, withDrive).Execute();
        }
        catch (Exception e) { Console.Error.WriteLine("m15-cube-hw: " + e.Message + "\nusage: m15-cube-hw [robot-ip] --resources <dir> [--with-drive] [--yes] [--out <dir>] | --validate"); return 2; }
    }
}

internal sealed class M15CubeRun
{
    const uint WatchedFlags = (uint)(RobotStatusFlag.IsPickingOrPlacing | RobotStatusFlag.IsCarryingBlock | RobotStatusFlag.IsMoving | RobotStatusFlag.AreWheelsMoving | RobotStatusFlag.LiftInPos);
    static readonly RobotMessageId[] WatchedTags = { RobotMessageId.DockWithObject, RobotMessageId.PlaceObjectOnGround, RobotMessageId.DockingErrorSignal, RobotMessageId.PickAndPlaceResult };
    readonly string root, bundle, resources; readonly IPAddress ip; readonly List<JsonObject> records; readonly string[] argv; readonly bool yes, withDrive;
    readonly Stopwatch clock = Stopwatch.StartNew(); readonly ConcurrentQueue<JsonObject> events = new();
    readonly ConcurrentQueue<(double T, bool Out, byte[] Raw, Frame? Frame, string? Error)> frames = new();
    readonly ConcurrentQueue<(double T, uint Timestamp, uint Status)> transitions = new();
    readonly ConcurrentQueue<(double T, RobotObservedObjectReport Report)> observations = new();
    readonly ConcurrentQueue<string> errors = new(), stackLines = new(); readonly CancellationTokenSource cancel = new();
    readonly JsonObject answers = new(); readonly JsonArray phases = new();
    CozmoRobot? robot; RobotConnectionResponse? response; bool connected; HarnessStartup.Gate? startupGate; HarnessStartup.Report? startup; Task<string?>? pendingRead;
    uint? lastStatus;
    double Now => clock.Elapsed.TotalMilliseconds;
    public M15CubeRun(string root, string bundle, IPAddress ip, string resources, List<JsonObject> records, string[] argv, bool yes, bool withDrive)
    { this.root = root; this.bundle = bundle; this.ip = ip; this.resources = resources; this.records = records; this.argv = argv; this.yes = yes; this.withDrive = withDrive; }
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

    public int Execute()
    {
        Directory.CreateDirectory(bundle);
        Console.WriteLine("CUBE PICKUP AND PUT-DOWN TEST (about 5 minutes)\n\n"
            + "What this is for: Cozmo picks up a cube and puts it down twice, through the controls an outside AI uses.\n"
            + "The first time the cube is right in front of him; the second time he drives to it.\n"
            + "Nothing passes or fails here; the recording and your answers are reviewed afterwards.\n\n"
            + M15CubeHw.SetupText + "\n\nCtrl+C stops the run at any time; everything recorded so far is still saved.\nResults folder: " + bundle);
        ConsoleCancelEventHandler interrupt = (_, e) => { e.Cancel = true; cancel.Cancel(); }; Console.CancelKeyPress += interrupt;
        using var timer = new Timer(_ => cancel.Cancel(), null, TimeSpan.FromMinutes(15), Timeout.InfiniteTimeSpan);
        try
        {
            if (!yes) { Console.WriteLine("\nWhen Cozmo and the cube are set up, press Enter to connect. Connecting takes up to a minute."); ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested(); }
            using var resolution = new HighResolutionTimer();
            var options = HardwareEngineOptions.Create(resources, log: errors.Enqueue, beforeStart: e =>
            {
                EngineEvidence.Subscribe(e, line => { }, Event);
                e.StackLogLine += stackLines.Enqueue;
                e.ActionCompleted += a => Event("action-completed", JsonSerializer.SerializeToNode(a)!.AsObject());
                e.RobotObservedObject += r => observations.Enqueue((Now, r));
            });
            robot = CozmoRobot.Create(engineOptions: options);
            robot.Transport.FrameTrace += f => frames.Enqueue((Now, f.Outbound, (byte[])f.Raw.Clone(), f.Frame, f.Error));
            robot.State.StateUpdated += s =>
            {
                uint masked = s.Status & WatchedFlags;
                if (lastStatus != masked) { lastStatus = masked; transitions.Enqueue((Now, s.Timestamp, masked)); }
            };
            robot.Engine.ConnectionResponse += r => { response = r; Event("connection-response", new() { ["result"] = r.Result.ToString(), ["firmware"] = r.FwVersion }); };
            robot.HandlerFaulted += e => Event("handler-fault", new() { ["exception"] = e.ToString() });
            // fidelity: M2-021 — ConnectToRobot only after delivered readiness, as CozmoRobot.ConnectAsync does.
            startupGate = HarnessStartup.ConnectAfterReadiness(robot, () => robot.ConnectToRobot(ip), () => cancel.IsCancellationRequested);
            Event("startup-gate", startupGate.ToJson()); Console.WriteLine("Startup: " + startupGate.Describe());
            if (!startupGate.Ready) errors.Enqueue("startup: " + startupGate.Describe());
            connected = startupGate.Ready && Poll(() => response?.Result == RobotConnectionResult.Success && robot.Engine.Robot is { TimeSynced: true, FirstFullStateHandled: true }, 15000);
            if (connected)
            {
                bool assets = Poll(() => robot.Animations.ClipNames.Count > 0, 15000);
                bool calibrated = Poll(() => robot.State.CalibrationComplete, 20000);
                var cal = robot.Vision.WaitForConnectionCalibrationAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                Event("check-readiness", new() { ["assets"] = assets, ["calibrated"] = calibrated, ["cameraCalibration"] = cal is not null });
                if (cal is null) errors.Enqueue("The robot's camera calibration was not read; the engine cannot locate a cube without it.");
                connected = assets && calibrated && cal is not null;
            }
            startup = HarnessStartup.Analyse(EngineLogs(), startupGate, connected ? HarnessStartup.HasCachedNeutralFace(robot) : null, connected ? null : "connection or calibration did not complete");
            Event("startup-report", startup.ToJson()); Console.WriteLine("Startup: " + startup.Describe());
            if (!connected) throw new InvalidOperationException("Not connected; nothing ran.");
            robot.StartCamera();
            Event("camera-started", new());
            var look = robot.Motion.QueueAiHeadAngleAsync(M15CubeHw.LookHeadRad, cancel.Token);
            bool looked = Poll(() => look.IsCompleted, 10000);
            Event("look-head", new() { ["targetRad"] = M15CubeHw.LookHeadRad, ["finished"] = looked, ["result"] = looked && look.IsCompletedSuccessfully ? look.Result.ToString() : null });

            RunPair(robot, "direct", drive: false);
            // The drive round is opt-in (2026-11-09: the drive pickup approached the far side of the cube and took Cozmo off
            // the table, PICKUP-PREDOCK-SIDE-1). Run it with --with-drive once that is fixed.
            if (withDrive && !yes)
            {
                Console.WriteLine("\n" + M15CubeHw.MoveText); ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested();
                RunPair(robot, "drive", drive: true);
            }
            if (!yes)
            {
                Ask("pickedFirst", "Did he pick the cube up (cube right in front)? [yes/no/tried and missed]");
                Ask("placedFirst", "Did he put it down on the table? [yes/no/dropped it]");
                if (withDrive)
                {
                    Ask("pickedSecond", "Second time (he drove): did he reach the cube and pick it up? [yes/no/tried and missed]");
                    Ask("placedSecond", "Second time: did he put it down? [yes/no/dropped it]");
                }
                Ask("odd", "Anything odd (bumped the cube, wrong cube, lift jerky, face or sounds strange)? Type it, or just press Enter.");
            }
        }
        catch (OperationCanceledException) { errors.Enqueue("Stopped early with Ctrl+C or the time limit."); }
        catch (Exception e) { errors.Enqueue(e.ToString()); }
        finally
        {
            if (robot is not null) { try { if (connected) robot.StopCamera(); } catch (Exception e) { errors.Enqueue("StopCamera: " + e.Message); } var d = Task.Run(robot.Dispose); if (!d.Wait(5000)) errors.Enqueue("Dispose did not finish in 5 seconds"); }
            Console.CancelKeyPress -= interrupt;
        }
        WriteBundle();
        Console.WriteLine("\n=== TEST FINISHED ===\nNothing passes or fails here. Send this folder for review: " + bundle);
        return 0;
    }

    void RunPair(CozmoRobot r, string name, bool drive)
    {
        Thread.Sleep(1500);
        double since = Now;
        Console.WriteLine($"\n[{name}] Looking for the cube (up to 30 s)...");
        bool seen = Poll(() => observations.Any(o => o.T >= since), 30000);
        var report = observations.Where(o => o.T >= since).Select(o => o.Report).FirstOrDefault();
        Event("observe", new() { ["phase"] = name, ["seen"] = seen, ["report"] = report is null ? null : JsonSerializer.SerializeToNode(report) });
        if (report is null) { errors.Enqueue($"{name}: no RobotObservedObject report within 30 s; pickup not attempted"); Console.WriteLine("    He did not report seeing a cube; skipping this round."); return; }
        Console.WriteLine($"    Seen: object {report.ObjectId} at ({report.Pose.X:F0}, {report.Pose.Y:F0}) mm.");
        Console.WriteLine($">>> WATCH: Cozmo should {(drive ? "drive to and " : "")}pick up the cube <<<");
        // Seeing a new cube starts the engine's own AcknowledgeObject reaction, which disables outside actions while it
        // runs (run 2, 2026-11-09: the pickup was refused). Retry admission for up to 20 s, as an outside AI would.
        JsonObject pickup = new();
        double retryUntil = Now + 20000; int attempts = 0;
        while (true)
        {
            attempts++;
            pickup = Phase($"{name}-pickup", () =>
            {
                var t = r.Motion.QueueAiPickupCubeAsync(report.ObjectId, drive, cancel.Token);
                return (t, () => t.IsCompletedSuccessfully ? (t.Result.Admitted, t.Result.EngineResult, t.Result.Detail, t.Result.OwnerRetired) : default);
            }, 90000);
            bool refusedByReaction = pickup["admitted"]?.GetValue<bool>() == false && (pickup["detail"]?.GetValue<string>() ?? "").Contains("disabled by a reaction", StringComparison.Ordinal);
            if (!refusedByReaction || Now > retryUntil) break;
            Event("pickup-retry", new() { ["phase"] = name, ["attempt"] = attempts });
            Thread.Sleep(500);
        }
        pickup["admissionAttempts"] = attempts;
        pickup["objectId"] = report.ObjectId; phases.Add(pickup); Console.WriteLine("    " + pickup["detail"]);
        Thread.Sleep(1500);
        Console.WriteLine(">>> WATCH: Cozmo should put the cube down <<<");
        var place = Phase($"{name}-place", () =>
        {
            var t = r.Motion.QueueAiLiftHeightAsync(M15CubeHw.PlaceHeightMm, cancel.Token);
            return (t, () => t.IsCompletedSuccessfully ? (t.Result.Admitted, t.Result.EngineResult, t.Result.Detail, t.Result.OwnerRetired) : default);
        }, 60000);
        phases.Add(place); Console.WriteLine("    " + place["detail"]);
    }

    JsonObject Phase(string name, Func<(Task Task, Func<(bool Admitted, uint? Result, string? Detail, bool Retired)> Read)> start, int limitMs)
    {
        double begin = Now; Event("phase-start", new() { ["phase"] = name });
        var (task, read) = start();
        bool finished = Poll(() => task.IsCompleted, limitMs);
        var (admitted, result, detail, retired) = finished && task.IsCompletedSuccessfully ? read() : default;
        if (!finished) errors.Enqueue($"{name}: the action did not end within {limitMs / 1000} s");
        else if (task.IsFaulted) errors.Enqueue($"{name}: {task.Exception}");
        var o = new JsonObject
        {
            ["phase"] = name, ["startMs"] = begin, ["endMs"] = Now, ["admitted"] = finished && task.IsCompletedSuccessfully ? admitted : null,
            ["engineResult"] = result is { } er ? $"0x{er:X8}" : null, ["ownerRetired"] = finished && task.IsCompletedSuccessfully ? retired : null,
            ["detail"] = detail ?? (finished ? "faulted" : "timed out"),
            ["carryingAfter"] = robot?.State.Latest?.Has(RobotStatusFlag.IsCarryingBlock),
        };
        Event("phase-end", (JsonObject)o.DeepClone());
        return o;
    }

    void Ask(string field, string question)
    {
        Console.WriteLine("\n" + question);
        string? raw = ReadLineBounded(); answers[field] = raw;
        Event("operator-observation", new() { ["field"] = field, ["question"] = question, ["answer"] = raw });
    }

    static string Flags(uint s) => string.Join("|", Enum.GetValues<RobotStatusFlag>().Where(f => (s & (uint)f) != 0).Select(f => f.ToString()));

    // Per phase: the watched messages both ways and the status transitions, in host capture order. Observations only.
    JsonObject Timeline(IEnumerable<M5Hw.WireMessage> wire, double from, double to)
    {
        var items = wire.Where(m => m.Payload.Length > 0 && m.AtMs >= from && m.AtMs <= to && Array.IndexOf(WatchedTags, (RobotMessageId)m.Payload[0]) >= 0)
            .Select(m => (m.AtMs, (JsonNode)new JsonObject { ["atMs"] = m.AtMs, ["dir"] = m.Outbound ? "out" : "in", ["tag"] = $"0x{m.Payload[0]:X2}", ["payloadHex"] = Convert.ToHexString(m.Payload) }))
            .Concat(transitions.Where(t => t.T >= from && t.T <= to)
            .Select(t => (t.T, (JsonNode)new JsonObject { ["atMs"] = t.T, ["robotTimestamp"] = t.Timestamp, ["status"] = Flags(t.Status) })))
            .OrderBy(x => x.Item1).Select(x => x.Item2).ToArray();
        return new JsonObject { ["events"] = new JsonArray(items) };
    }

    void WriteBundle()
    {
        try
        {
            var wire = M5HwAnalysis.OrderedUnique(M5HwAnalysis.ReadWire(frames.ToArray().Select((f, index) => new M5HwAnalysis.CapturedFrame(index + 1, f.T, f.Out, f.Frame)))).ToList();
            foreach (var p in phases) { var o = p!.AsObject(); o["timeline"] = Timeline(wire, o["startMs"]!.GetValue<double>(), o["endMs"]!.GetValue<double>()); }
            var missing = stackLines.Where(l => l.Contains("MISSING", StringComparison.Ordinal)).Distinct().ToArray();
            File.WriteAllText(Path.Combine(bundle, "SUMMARY.txt"), "Cube pickup and put-down test. Nothing here passes or fails.\n\n"
                + string.Join("\n", phases.Select(p => $"{p!["phase"]}: admitted={p["admitted"]} result={p["engineResult"]} carryingAfter={p["carryingAfter"]} ({p["detail"]})"))
                + "\n\nCube reports seen: " + observations.Count
                + "\n\nStartup: " + (startup?.Describe() ?? "not completed")
                + "\n\nYour notes: " + answers.ToJsonString()
                + "\n\nKnown gaps reported: " + missing.Length
                + "\n\nRun errors:\n" + (errors.IsEmpty ? "None." : string.Join("\n", errors)));
            File.WriteAllLines(Path.Combine(bundle, "frames.jsonl"), frames.Select(f => new JsonObject { ["atMs"] = f.T, ["outbound"] = f.Out, ["rawBase64"] = Convert.ToBase64String(f.Raw), ["error"] = f.Error }.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "messages.jsonl"), wire.Select(m => new JsonObject { ["atMs"] = m.AtMs, ["outbound"] = m.Outbound, ["tag"] = $"0x{m.Payload[0]:X2}", ["payloadHex"] = Convert.ToHexString(m.Payload), ["frameLine"] = m.FrameLine, ["submessageOrdinal"] = m.SubmessageOrdinal, ["reliableSequence"] = m.ReliableSequence, ["connectionEpoch"] = m.ConnectionEpoch }.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "status.jsonl"), transitions.Select(t => new JsonObject { ["atMs"] = t.T, ["robotTimestamp"] = t.Timestamp, ["status"] = Flags(t.Status) }.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "observations.jsonl"), observations.Select(o => new JsonObject { ["atMs"] = o.T, ["report"] = JsonSerializer.SerializeToNode(o.Report) }.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "events.jsonl"), events.Select(e => e.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "stack-log.txt"), stackLines);
            var files = new JsonArray();
            foreach (var relative in new[] { "cozmo-stack/src/Cozmo.Conformance/M15CubeHw.cs", "cozmo-stack/src/Cozmo.Conformance/HardwareEngineOptions.cs", "cozmo-stack/src/Cozmo.Conformance/HarnessStartup.cs", "re-analysis/acceptance/hardware/scripts/m15-cube-hw-1.md", "re-analysis/fidelity_manifest.json" })
            {
                string source = Path.Combine(root, relative); if (!File.Exists(source)) { errors.Enqueue("Missing provenance file " + relative); continue; }
                string dest = Path.Combine(bundle, "provenance", relative); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(source, dest, true);
                files.Add(new JsonObject { ["path"] = relative, ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant() });
            }
            string? commit = null; try { var p = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true }); commit = p!.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(); } catch (Exception e) { errors.Enqueue("git: " + e.Message); }
            var report = new JsonObject { ["testId"] = M15CubeHw.TestId, ["createdUtc"] = DateTime.UtcNow.ToString("O"), ["commit"] = commit, ["argv"] = JsonSerializer.SerializeToNode(argv),
                ["resources"] = resources, ["connected"] = connected, ["startup"] = startup?.ToJson(), ["cubeReports"] = observations.Count, ["phases"] = phases.DeepClone(), ["operator"] = answers.DeepClone(),
                ["missingReported"] = JsonSerializer.SerializeToNode(missing), ["records"] = JsonSerializer.SerializeToNode(records), ["provenance"] = files, ["errors"] = JsonSerializer.SerializeToNode(errors),
                ["acceptance"] = "NOT_JUDGED: manager review required; hardware success does not raise provenance" };
            File.WriteAllText(Path.Combine(bundle, "result.json"), report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(bundle, "README.txt"), "M15-CUBE-HW evidence bundle. result.json holds each phase's action result and a timeline of the dock (0x42), place (0x44), correction (0x48) and PickAndPlaceResult (0xB8) messages interleaved with the robot's busy/carrying/moving status transitions; status.jsonl and observations.jsonl keep the full streams; messages.jsonl deduplicates reliable resends; frames.jsonl keeps every datagram. Times are host capture times. No acceptance or manifest change was made.");
        }
        catch (Exception e) { Console.Error.WriteLine("Could not write the results folder: " + e.Message); }
    }
}
