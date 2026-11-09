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
/// Operator-run first robot speech check (VOICE-ROBOT-DELIVERY-1). Three sentences go through the public
/// <see cref="CozmoRobot.SayEnglishSentenceAsync"/>: one played to the end, one cancelled after its first audio message,
/// and one more after the cancel. Nothing is graded. What the operator hears, the latency and the robot's own timing
/// cannot be sourced, and the synthetic frame counts and hashes of the native oracle are not hardware expectations. The
/// bundle records the action results, every message sent per phase and the operator's answers for the manager to judge.
/// </summary>
public static class M14SpeechHw
{
    public const string TestId = "M14-SPEECH-HW";
    public static readonly string[] RecordIds = { "M14-006", "M14-013", "M6-013", "M6-016", "M6-017", "M6-018", "M6-022", "M8-020" };
    public static readonly (string Name, string Text, bool Cancel)[] Phases =
    {
        ("say", "Hello. I am Cozmo.", false),
        ("cancel", "This sentence is cut off partway through, so you should hear only the start of it.", true),
        ("after-cancel", "I can talk again.", false),
    };
    public const string SetupText = "Setup:\n"
        + "1. Put Cozmo on his charger, or on a table well away from the edge. He should not drive in this test.\n"
        + "2. Turn his volume up if you changed it, and keep the room quiet enough to hear him.\n"
        + "3. The PC's speakers stay silent: the PC only paces the voice; the sound comes from Cozmo.";

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
            bool validate = false, yes = false; string? resources = null, banks = null, output = null; IPAddress ip = RobotAddress.DefaultFor(false);
            for (int i = 1; i < args.Length; i++) switch (args[i])
                {
                    case "--validate": validate = true; break;
                    case "--yes": yes = true; break;
                    case "--resources": resources = args[++i]; break;
                    case "--banks": banks = args[++i]; break;
                    case "--out": output = args[++i]; break;
                    default: if (!IPAddress.TryParse(args[i], out var parsed)) throw new ArgumentException("Unknown argument " + args[i]); ip = parsed; break;
                }
            var records = LoadRecords(Path.Combine(root, "re-analysis", "fidelity_manifest.json"));
            if (validate) { Console.WriteLine($"{TestId}: {records.Count} valid records; no network opened."); return 0; }
            banks = Path.GetFullPath(banks ?? Path.Combine(root, "re-analysis", "obb", "sound_meta"));
            foreach (string bank in new[] { "Init.bnk", Path.Combine("English(US)", "Cozmo.bnk") })
                if (!File.Exists(Path.Combine(banks, bank))) throw new ArgumentException($"Sound bank {bank} not found under {banks}; pass --banks <folder holding Init.bnk>.");
            if (Robot.Emulation.ShippedLibraries.FindLibDir() is null)
                throw new ArgumentException("The shipped libraries (resources/lib/armeabi-v7a, libcozmoEngine.so and the Acapela library) are not on this PC.");
            var options = EngineStartupResources.OptionsForRoot(resources);
            string bundle = Path.Combine(output ?? Path.Combine(root, "re-analysis", "acceptance", "hardware"), DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + TestId + "-" + Guid.NewGuid().ToString("N")[..6]);
            return new M14SpeechRun(root, bundle, ip, options.ResourcesPath!, banks, records, args, yes).Execute();
        }
        catch (Exception e) { Console.Error.WriteLine("m14-speech-hw: " + e.Message + "\nusage: m14-speech-hw [robot-ip] --resources <dir> [--banks <dir>] [--yes] [--out <dir>] | --validate"); return 2; }
    }
}

internal sealed class M14SpeechRun
{
    readonly string root, bundle, resources, banks; readonly IPAddress ip; readonly List<JsonObject> records; readonly string[] argv; readonly bool yes;
    readonly Stopwatch clock = Stopwatch.StartNew(); readonly ConcurrentQueue<JsonObject> events = new();
    readonly ConcurrentQueue<(double T, bool Out, byte[] Raw, Frame? Frame, string? Error)> frames = new();
    readonly ConcurrentQueue<string> errors = new(), stackLines = new(); readonly CancellationTokenSource cancel = new();
    readonly JsonObject answers = new(); readonly JsonArray phases = new();
    CozmoRobot? robot; RobotConnectionResponse? response; bool connected; HarnessStartup.Gate? startupGate; HarnessStartup.Report? startup; Task<string?>? pendingRead;
    volatile bool watchAudio; volatile int audioSeen;
    double Now => clock.Elapsed.TotalMilliseconds;
    public M14SpeechRun(string root, string bundle, IPAddress ip, string resources, string banks, List<JsonObject> records, string[] argv, bool yes)
    { this.root = root; this.bundle = bundle; this.ip = ip; this.resources = resources; this.banks = banks; this.records = records; this.argv = argv; this.yes = yes; }
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
        Console.WriteLine("FIRST ROBOT SPEECH TEST (about 3 minutes)\n\n"
            + "What this is for: Cozmo says three short sentences in his own voice. The second one is cut off on purpose.\n"
            + "Nothing passes or fails here; the recording and your answers are reviewed afterwards.\n\n"
            + M14SpeechHw.SetupText + "\n\nCtrl+C stops the run at any time; everything recorded so far is still saved.\nResults folder: " + bundle);
        ConsoleCancelEventHandler interrupt = (_, e) => { e.Cancel = true; cancel.Cancel(); }; Console.CancelKeyPress += interrupt;
        using var timer = new Timer(_ => cancel.Cancel(), null, TimeSpan.FromMinutes(10), Timeout.InfiniteTimeSpan);
        try
        {
            if (!yes) { Console.WriteLine("\nWhen Cozmo is set up, press Enter to connect. Connecting takes up to a minute."); ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested(); }
            using var resolution = new HighResolutionTimer();
            // The banks are the operator's local copy, never committed. The route is English only: a non-English PC locale
            // (M15-052) makes the request report "not ready", which the bundle records.
            var options = HardwareEngineOptions.Create(resources, log: errors.Enqueue, speechSoundBankDirectory: banks, beforeStart: e =>
            {
                EngineEvidence.Subscribe(e, line => { }, Event);
                e.StackLogLine += stackLines.Enqueue;
                e.ActionCompleted += a => Event("action-completed", JsonSerializer.SerializeToNode(a)!.AsObject());
            });
            Event("speech-inputs", new() { ["banks"] = banks, ["labLocaleLanguage"] = options.LabLocaleLanguage });
            robot = CozmoRobot.Create(engineOptions: options);
            robot.Transport.FrameTrace += f =>
            {
                frames.Enqueue((Now, f.Outbound, (byte[])f.Raw.Clone(), f.Frame, f.Error));
                if (watchAudio && f.Outbound && f.Frame is { } frame && frame.Messages.Any(m => m.Payload.Length > 0 && m.Payload[0] == 0x8E)) Interlocked.Increment(ref audioSeen);
            };
            robot.Engine.ConnectionResponse += r => { response = r; Event("connection-response", new() { ["result"] = r.Result.ToString(), ["firmware"] = r.FwVersion }); };
            robot.HandlerFaulted += e => Event("handler-fault", new() { ["exception"] = e.ToString() });
            // fidelity: M2-021 — ConnectToRobot only after delivered readiness, as CozmoRobot.ConnectAsync does.
            startupGate = HarnessStartup.ConnectAfterReadiness(robot, () => robot.ConnectToRobot(ip), () => cancel.IsCancellationRequested);
            Event("startup-gate", startupGate.ToJson()); Console.WriteLine("Startup: " + startupGate.Describe());
            if (!startupGate.Ready) errors.Enqueue("startup: " + startupGate.Describe());
            connected = startupGate.Ready && Poll(() => response?.Result == RobotConnectionResult.Success && robot.Engine.Robot is { TimeSynced: true, FirstFullStateHandled: true }, 15000);
            if (connected) { bool assets = Poll(() => robot.Animations.ClipNames.Count > 0, 15000); bool calibrated = Poll(() => robot.State.CalibrationComplete, 20000); Event("check-readiness", new() { ["assets"] = assets, ["calibrated"] = calibrated }); connected = assets && calibrated; }
            startup = HarnessStartup.Analyse(EngineLogs(), startupGate, connected ? HarnessStartup.HasCachedNeutralFace(robot) : null, connected ? null : "connection or calibration did not complete");
            Event("startup-report", startup.ToJson()); Console.WriteLine("Startup: " + startup.Describe());
            if (!connected) throw new InvalidOperationException("Not connected; nothing ran.");
            foreach (var phase in M14SpeechHw.Phases)
            {
                cancel.Token.ThrowIfCancellationRequested();
                Thread.Sleep(2000);
                Console.WriteLine($"\n>>> LISTEN: Cozmo should say \"{phase.Text}\"{(phase.Cancel ? " (it will be cut off)" : "")} <<<");
                phases.Add(Say(robot, phase.Name, phase.Text, phase.Cancel));
                Console.WriteLine("    " + phases[^1]!["detail"]);
            }
            if (!yes)
            {
                Ask("heardFirst", "Did Cozmo say the first sentence, \"Hello. I am Cozmo.\"? [yes/no/partly]");
                Ask("voice", "What did it sound like? Cozmo's usual robot voice, a plain human voice, garbled, too fast or slow? Your own words.");
                Ask("cutOff", "Did the second sentence stop partway through? [yes/no/didn't hear it]");
                Ask("heardThird", "Did he say the third sentence, \"I can talk again.\"? [yes/no/partly]");
                Ask("odd", "Anything odd (clicks, gaps, his face or head doing something strange)? Type it, or just press Enter.");
            }
        }
        catch (OperationCanceledException) { errors.Enqueue("Stopped early with Ctrl+C or the time limit."); }
        catch (Exception e) { errors.Enqueue(e.ToString()); }
        finally
        {
            if (robot is not null) { var d = Task.Run(robot.Dispose); if (!d.Wait(5000)) errors.Enqueue("Dispose did not finish in 5 seconds"); }
            Console.CancelKeyPress -= interrupt;
        }
        WriteBundle();
        Console.WriteLine("\n=== TEST FINISHED ===\nNothing passes or fails here. Send this folder for review: " + bundle);
        return 0;
    }

    JsonObject Say(CozmoRobot r, string name, string text, bool cancelAfterFirstAudio)
    {
        double start = Now; Event("phase-start", new() { ["phase"] = name, ["text"] = text });
        using var cts = new CancellationTokenSource();
        audioSeen = 0; watchAudio = true;
        var task = r.SayEnglishSentenceAsync(text, cts.Token);
        double? cancelledAt = null;
        if (cancelAfterFirstAudio)
        {
            // About one second of audio first (30 messages at 30 per second), so the operator can hear where it is cut off (2026-11-09: cutting after the first message left nothing audible).
            if (Poll(() => audioSeen >= 30 || task.IsCompleted, 30000) && !task.IsCompleted) { cancelledAt = Now; cts.Cancel(); }
            else errors.Enqueue($"{name}: fewer than 30 0x8E audio messages seen before the action ended or 30 s passed; not cancelled");
        }
        bool finished = Poll(() => task.IsCompleted, 60000);
        watchAudio = false;
        var result = finished && task.IsCompletedSuccessfully ? task.Result : null;
        if (!finished) errors.Enqueue($"{name}: the speech action did not end within 60 s");
        else if (task.IsFaulted) errors.Enqueue($"{name}: {task.Exception}");
        var o = new JsonObject
        {
            ["phase"] = name, ["text"] = text, ["startMs"] = start, ["endMs"] = Now, ["cancelledAtMs"] = cancelledAt,
            ["admitted"] = result?.Admitted, ["engineResult"] = result?.EngineResult is { } er ? $"0x{er:X8}" : null,
            ["ownerRetired"] = result?.OwnerRetired, ["detail"] = result?.Detail ?? (finished ? "faulted" : "timed out"),
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

    // Per phase: what was sent in its window, by tag, with the payload lengths of each tag. Observations only.
    static JsonObject WireFacts(IEnumerable<M5Hw.WireMessage> wire, double from, double to)
    {
        var window = wire.Where(m => m.Outbound && m.Payload.Length > 0 && m.AtMs >= from && m.AtMs <= to).ToList();
        double? first(byte tag) => window.FirstOrDefault(m => m.Payload[0] == tag)?.AtMs;
        return new JsonObject
        {
            ["byTag"] = new JsonArray(window.GroupBy(m => m.Payload[0]).OrderBy(g => g.Key).Select(g => (JsonNode)new JsonObject
            {
                ["tag"] = $"0x{g.Key:X2}", ["count"] = g.Count(),
                ["payloadLengths"] = new JsonArray(g.Select(m => m.Payload.Length).Distinct().OrderBy(x => x).Select(x => (JsonNode)x).ToArray()),
                ["firstMs"] = g.First().AtMs, ["lastMs"] = g.Last().AtMs,
            }).ToArray()),
            ["first8E"] = first(0x8E), ["first8F"] = first(0x8F), ["first9B"] = first(0x9B), ["first8D"] = first(0x8D),
        };
    }

    void WriteBundle()
    {
        try
        {
            var wire = M5HwAnalysis.OrderedUnique(M5HwAnalysis.ReadWire(frames.ToArray().Select((f, index) => new M5HwAnalysis.CapturedFrame(index + 1, f.T, f.Out, f.Frame)))).ToList();
            foreach (var p in phases) { var o = p!.AsObject(); o["wire"] = WireFacts(wire, o["startMs"]!.GetValue<double>(), o["endMs"]!.GetValue<double>()); }
            var missing = stackLines.Where(l => l.Contains("MISSING", StringComparison.Ordinal)).Distinct().ToArray();
            File.WriteAllText(Path.Combine(bundle, "SUMMARY.txt"), "First robot speech test. Nothing here passes or fails.\n\n"
                + string.Join("\n", phases.Select(p => $"{p!["phase"]}: admitted={p["admitted"]} result={p["engineResult"]} ({p["detail"]})"))
                + "\n\nStartup: " + (startup?.Describe() ?? "not completed")
                + "\n\nYour notes: " + answers.ToJsonString()
                + "\n\nKnown gaps reported: " + missing.Length
                + "\n\nRun errors:\n" + (errors.IsEmpty ? "None." : string.Join("\n", errors)));
            File.WriteAllLines(Path.Combine(bundle, "frames.jsonl"), frames.Select(f => new JsonObject { ["atMs"] = f.T, ["outbound"] = f.Out, ["rawBase64"] = Convert.ToBase64String(f.Raw), ["error"] = f.Error }.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "messages.jsonl"), wire.Select(m => new JsonObject { ["atMs"] = m.AtMs, ["outbound"] = m.Outbound, ["tag"] = $"0x{m.Payload[0]:X2}", ["payloadHex"] = Convert.ToHexString(m.Payload), ["frameLine"] = m.FrameLine, ["submessageOrdinal"] = m.SubmessageOrdinal, ["reliableSequence"] = m.ReliableSequence, ["connectionEpoch"] = m.ConnectionEpoch }.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "events.jsonl"), events.Select(e => e.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "stack-log.txt"), stackLines);
            var files = new JsonArray();
            foreach (var relative in new[] { "cozmo-stack/src/Cozmo.Conformance/M14SpeechHw.cs", "cozmo-stack/src/Cozmo.Conformance/HardwareEngineOptions.cs", "cozmo-stack/src/Cozmo.Conformance/HarnessStartup.cs", "re-analysis/acceptance/hardware/scripts/m14-speech-hw-1.md", "re-analysis/fidelity_manifest.json" })
            {
                string source = Path.Combine(root, relative); if (!File.Exists(source)) { errors.Enqueue("Missing provenance file " + relative); continue; }
                string dest = Path.Combine(bundle, "provenance", relative); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(source, dest, true);
                files.Add(new JsonObject { ["path"] = relative, ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant() });
            }
            string? commit = null; try { var p = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true }); commit = p!.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(); } catch (Exception e) { errors.Enqueue("git: " + e.Message); }
            var report = new JsonObject { ["testId"] = M14SpeechHw.TestId, ["createdUtc"] = DateTime.UtcNow.ToString("O"), ["commit"] = commit, ["argv"] = JsonSerializer.SerializeToNode(argv),
                ["resources"] = resources, ["banks"] = banks, ["connected"] = connected, ["startup"] = startup?.ToJson(), ["phases"] = phases.DeepClone(), ["operator"] = answers.DeepClone(),
                ["missingReported"] = JsonSerializer.SerializeToNode(missing), ["records"] = JsonSerializer.SerializeToNode(records), ["provenance"] = files, ["errors"] = JsonSerializer.SerializeToNode(errors),
                ["acceptance"] = "NOT_JUDGED: manager review required; hardware success does not raise provenance" };
            File.WriteAllText(Path.Combine(bundle, "result.json"), report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(bundle, "README.txt"), "M14-SPEECH-HW evidence bundle. result.json holds each phase's action result and the messages sent in its window; messages.jsonl deduplicates reliable resends; frames.jsonl keeps every datagram; events.jsonl keeps engine logs and API events; stack-log.txt keeps the stack's own lines. Times are host capture times. No acceptance or manifest change was made.");
        }
        catch (Exception e) { Console.Error.WriteLine("Could not write the results folder: " + e.Message); }
    }
}
