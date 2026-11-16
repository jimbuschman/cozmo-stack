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
    internal const string GotoSetup = "GO-TO ROUND. Clear the table around Cozmo and leave at least 30 cm ahead. After Enter, he will drive about 20 cm ahead and turn left 90 degrees.";
    internal const string ChargerSetup = "CHARGER ROUND. Clear the cubes away. Put the charger 20–40 cm directly in front of Cozmo,\n"
        + "marker facing him. Leave it powered and give him space to turn and back onto it. Press Enter when ready.";

    internal const string FlipSetup = "FLIP ROUND. Put one light cube about 20 cm ahead, upright with a side marker facing Cozmo. Press Enter when ready.";
    internal const string WheelieSetup = "WHEELIE ROUND. Reset one light cube about 20 cm ahead, upright with a side marker facing Cozmo. Press Enter when ready.";
    internal const string FreeplaySetup = "Put Cozmo on a clear table, well away from the edge and off his charger. He may move on his own.";
    internal const int FreeplaySeconds = 60;

    public const float PlaceHeightMm = 32f;
    /// <summary>Head angle for looking at a cube on the table 10..25 cm ahead (about -9 degrees). Run 1 (2026-11-09) kept the
    /// default -0.42 rad, which put the cube at the image's top edge with its marker cut off. Not a source value.</summary>
    public const float LookHeadRad = -0.15f;
    public const string SetupText = "Setup:\n"
        + "1. Put Cozmo on a clear table, well away from the edge, NOT on his charger. He will move a little.\n"
        + "2. Put ONE light cube on the table about 10 cm (4 in) straight in front of his face,\n"
        + "   UPRIGHT with its lights on top, one side marker facing him. Never on its side.\n"
        + "   Keep the other cubes out of his view.\n"
        + "3. Good, even light helps him see the marker.";
    public const string MoveText = "Now move the cube about 25 cm (10 in) in front of him and a little to one side,\n"
        + "still UPRIGHT (lights on top) with a side marker facing roughly toward him. He will drive to it. Press Enter when it is in place.";

    public static List<JsonObject> LoadRecords(string path, bool skills = false, bool freeplayControl = false, bool personality = false)
    {
        var all = JsonNode.Parse(File.ReadAllText(path))!["records"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => x["id"]!.GetValue<string>());
        var ids = personality ? PersonalityHw.RecordIds
            : freeplayControl ? new[] { "M15-031", "M15-058", "M8-012", "M8-013", "M8-015", "M7-019" }
            : skills ? RecordIds.Concat(new[] { "M13-008", "M13-009", "M13-016", "M12-035", "M13-028", "M13-041", "M12-023", "M12-024", "M12-032" }) : RecordIds;
        var missing = ids.Where(id => !all.ContainsKey(id)).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException("Missing fidelity records: " + string.Join(", ", missing));
        return ids.Select(id => (JsonObject)all[id].DeepClone()).ToList();
    }

    public static int Run(string[] args)
    {
        try
        {
            string? root = LinkCheck.FindRepoRoot(); if (root is null) throw new ArgumentException("Run inside the repository.");
            bool validate = false, yes = false, withDrive = false, skills = false, freeplayControl = false, personality = false;
            string? resources = null, output = null; IPAddress ip = RobotAddress.DefaultFor(false);
            for (int i = 1; i < args.Length; i++) switch (args[i])
                {
                    case "--validate": validate = true; break;
                    case "--yes": yes = true; break;
                    case "--with-drive": withDrive = true; break;
                    case "--skills": skills = true; break;
                    case "--freeplay-control": freeplayControl = true; break;
                    case "--personality": personality = true; break;
                    case "--resources": resources = args[++i]; break;
                    case "--out": output = args[++i]; break;
                    default: if (!IPAddress.TryParse(args[i], out var parsed)) throw new ArgumentException("Unknown argument " + args[i]); ip = parsed; break;
                }
            if ((freeplayControl || personality) && (skills || withDrive || freeplayControl && personality))
                throw new ArgumentException("Run the Freeplay control, personality, and cube rounds separately.");
            if (personality && yes && !validate)
                throw new ArgumentException("The personality round needs the operator's observations. Omit --yes.");
            string testId = personality ? PersonalityHw.TestId : TestId;
            var records = LoadRecords(Path.Combine(root, "re-analysis", "fidelity_manifest.json"), skills, freeplayControl, personality);
            if (validate)
            {
                Console.WriteLine($"{testId}: {records.Count} valid records; no network opened.");
                if (skills) Console.WriteLine($"Skill rounds: stack, roll, flip, wheelie, go to pose, mount charger.\n{FlipSetup}\n{WheelieSetup}\n{GotoSetup}\n{ChargerSetup}");
                if (freeplayControl) Console.WriteLine($"{FreeplaySetup}\nFreeplay for {FreeplaySeconds} s, queue CozmoSaysSpeakLoop once, then another {FreeplaySeconds} s of Freeplay.");
                if (personality) PersonalityHw.PrintPlan();
                return 0;
            }
            if (Robot.Emulation.ShippedLibraries.FindLibDir() is null)
                throw new ArgumentException("The shipped libraries (resources/lib/armeabi-v7a, libcozmoEngine.so) are not on this PC.");
            if (!Robot.Vision.MarkerLibrary.IsAvailable)
                throw new ArgumentException("The cube marker library is not on this PC, so Cozmo cannot read cube markers. With Python installed and resources/lib/armeabi-v7a/libcozmoEngine.so present, run: python re-analysis/tools/extract_marker_library.py resources/lib/armeabi-v7a/libcozmoEngine.so, then rebuild.");
            var options = EngineStartupResources.OptionsForRoot(resources);
            string bundle = Path.Combine(output ?? Path.Combine(root, "re-analysis", "acceptance", "hardware"), DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + testId + "-" + Guid.NewGuid().ToString("N")[..6]);
            return new M15CubeRun(root, bundle, ip, options.ResourcesPath!, records, args, yes, withDrive)
            { Skills = skills, FreeplayControl = freeplayControl, Personality = personality }.Execute();
        }
        catch (Exception e) { Console.Error.WriteLine("m15-cube-hw: " + e.Message + "\nusage: m15-cube-hw [robot-ip] --resources <dir> [--with-drive] [--skills] [--freeplay-control | --personality] [--yes] [--out <dir>] | --validate"); return 2; }
    }
}

internal sealed partial class M15CubeRun
{
    const uint WatchedFlags = (uint)(RobotStatusFlag.IsPickingOrPlacing | RobotStatusFlag.IsCarryingBlock | RobotStatusFlag.IsMoving | RobotStatusFlag.AreWheelsMoving | RobotStatusFlag.LiftInPos | RobotStatusFlag.IsOnCharger);
    static readonly RobotMessageId[] WatchedTags = { RobotMessageId.DockWithObject, RobotMessageId.PlaceObjectOnGround, RobotMessageId.DockingErrorSignal, RobotMessageId.PickAndPlaceResult };
    readonly string root, bundle, resources; readonly IPAddress ip; readonly List<JsonObject> records; readonly string[] argv; readonly bool yes, withDrive;
    readonly Stopwatch clock = Stopwatch.StartNew(); readonly ConcurrentQueue<JsonObject> events = new();
    readonly ConcurrentQueue<(double T, bool Out, byte[] Raw, Frame? Frame, string? Error)> frames = new();
    readonly ConcurrentQueue<(double T, uint Timestamp, uint Status)> transitions = new();
    readonly ConcurrentQueue<(double T, RobotObservedObjectReport Report)> observations = new();
    readonly ConcurrentQueue<(double T, uint Id, uint Origin, uint Timestamp)> chargerObservations = new();
    readonly ConcurrentQueue<string> errors = new(), stackLines = new(); readonly CancellationTokenSource cancel = new();
    readonly JsonObject answers = new(); readonly JsonArray phases = new();
    CozmoRobot? robot; RobotConnectionResponse? response; bool connected; HarnessStartup.Gate? startupGate; HarnessStartup.Report? startup; Task<string?>? pendingRead;
    uint? lastStatus;
    public bool Skills { get; init; }
    public bool FreeplayControl { get; init; }
    public bool Personality { get; init; }
    string RunName => Personality ? "Personality observations" : FreeplayControl ? "Freeplay control" : "Cube pickup and put-down";
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
        Console.WriteLine((Personality
            ? "PERSONALITY OBSERVATIONS (about 25 minutes)\n\n" + PersonalityHw.Setup
            : FreeplayControl
            ? "FREEPLAY CONTROL (about 3 minutes)\n\n" + M15CubeHw.FreeplaySetup
            : "CUBE PICKUP AND PUT-DOWN TEST (about 5 minutes)\n\n"
            + "What this is for: Cozmo picks up a cube and puts it down twice, through the controls an outside AI uses.\n"
            + "The first time the cube is right in front of him; the second time he drives to it.\n"
            + "Nothing passes or fails here; the recording and your answers are reviewed afterwards.\n\n"
            + M15CubeHw.SetupText) + "\n\nCtrl+C stops the run at any time; everything recorded so far is still saved.\nResults folder: " + bundle);
        ConsoleCancelEventHandler interrupt = (_, e) => { e.Cancel = true; cancel.Cancel(); }; Console.CancelKeyPress += interrupt;
        using var timer = new Timer(_ => cancel.Cancel(), null, TimeSpan.FromMinutes(Personality ? 40 : 15), Timeout.InfiniteTimeSpan);
        try
        {
            if (!yes) { Console.WriteLine("\nWhen setup is complete, press Enter to connect. Connecting takes up to a minute."); ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested(); }
            using var resolution = new HighResolutionTimer();
            var options = HardwareEngineOptions.Create(resources, log: errors.Enqueue, beforeStart: e =>
            {
                EngineEvidence.Subscribe(e, line => { }, Event);
                e.StackLogLine += stackLines.Enqueue;
                e.ActionCompleted += a => Event("action-completed", JsonSerializer.SerializeToNode(a)!.AsObject());
                e.RobotObservedObject += r => observations.Enqueue((Now, r));
                if (FreeplayControl || Personality) SubscribeBehaviorReports(e);
                if (Personality) SubscribePersonalityReports(e);
            });
            robot = CozmoRobot.Create(engineOptions: options);
            if (Skills)
                robot.Vision.World.ObjectObserved += o =>
                {
                    // Charger 0x44 is still MISSING. Use the stack's located visual observation (M13-025).
                    if (o.Object.Type == ObjectType.Charger_Basic)
                        chargerObservations.Enqueue((Now, o.Object.ObjectId, o.Object.OriginId, o.Timestamp));
                };
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
            // Run 2026-11-15: he started on the charger, so the pickup turn spun the treads in place and the unexpected-movement
            // reaction cancelled it. Wait for him to be taken off before anything moves.
            if (robot.State.Latest?.Has(RobotStatusFlag.IsOnCharger) == true)
            {
                Console.WriteLine(">>> Cozmo is ON HIS CHARGER. Lift him off and put him on the table, then press Enter. <<<");
                Event("on-charger-at-start", new());
                while (robot.State.Latest?.Has(RobotStatusFlag.IsOnCharger) == true && !yes)
                {
                    ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(1000);
                    if (robot.State.Latest?.Has(RobotStatusFlag.IsOnCharger) == true) Console.WriteLine(">>> He still reports being on the charger. Take him off, then press Enter. <<<");
                }
            }
            robot.StartCamera();
            Event("camera-started", new());
            if (Personality) RunPersonality(robot);
            else if (FreeplayControl) RunFreeplayControl(robot);
            else RunCubeRounds(robot);
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

    void SubscribeBehaviorReports(CozmoEngine engine)
    {
        // M8-012: 0x87 and 0x88 carry old/new endpoints. M7-019: 0x8B carries a uint objective.
        engine.BehaviorTransition += report =>
        {
            var data = JsonSerializer.SerializeToNode(report)!.AsObject();
            data["oldBehavior"] = ((BehaviorID)report.OldId).ToString();
            data["newBehavior"] = ((BehaviorID)report.NewId).ToString();
            Event("behavior-transition", data);
        };
        engine.ReactionTransition += report =>
        {
            var data = JsonSerializer.SerializeToNode(report)!.AsObject();
            data["oldReaction"] = ReactionName(report.OldTrigger);
            data["newReaction"] = ReactionName(report.NewTrigger);
            Event("reaction-transition", data);
        };
        engine.EngineToGameMessage += (tag, body) =>
        {
            if (tag == 0x8B && body.Length == 4)
                Event("behavior-objective", new() { ["objective"] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(body.Span) });
        };
    }

    static string ReactionName(byte id) => id == 0x16 ? "NoneTrigger" : ((ReactionTrigger)id).ToString();

    void RunFreeplayControl(CozmoRobot r)
    {
        Event("request-freeplay", new() { ["enabled"] = true });
        r.SetEnableFreeplayActivity(true);
        try
        {
            ObserveFreeplay("before-command");
            Console.WriteLine("\nQueuing one CozmoSaysSpeakLoop animation.");
            using var actionCancel = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
            phases.Add(Phase("freeplay-animation", () =>
            {
                var task = r.Motion.QueueAiAnimationAsync(AnimationTrigger.CozmoSaysSpeakLoop, cancellationToken: actionCancel.Token);
                return (task, () => (task.Result.Admitted, task.Result.EngineResult, task.Result.Detail, task.Result.OwnerRetired));
            }, 65000));
            actionCancel.Cancel();
            ObserveFreeplay("after-command");
        }
        finally
        {
            Event("request-freeplay", new() { ["enabled"] = false });
            r.SetEnableFreeplayActivity(false);
        }
        if (!yes) Ask("freeplay", "What did Cozmo do before, during, and after the animation?");
    }

    void ObserveFreeplay(string name)
    {
        double start = Now;
        Event("phase-start", new() { ["phase"] = name });
        Console.WriteLine($"\n[{name}] Watch Cozmo in Freeplay for {M15CubeHw.FreeplaySeconds} seconds.");
        bool interrupted = cancel.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(M15CubeHw.FreeplaySeconds));
        var phase = new JsonObject { ["phase"] = name, ["startMs"] = start, ["endMs"] = Now,
            ["detail"] = interrupted ? "Interrupted observation" : "Observation window ended" };
        phases.Add(phase);
        Event("phase-end", (JsonObject)phase.DeepClone());
        cancel.Token.ThrowIfCancellationRequested();
    }

    void RunCubeRounds(CozmoRobot robot)
    {
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
        if (Skills && !yes)
        {
            RunSkills(robot);
            RunTrick("flip", M15CubeHw.FlipSetup, robot.Motion.QueueAiFlipCubeAsync);
            RunTrick("wheelie", M15CubeHw.WheelieSetup, robot.Motion.QueueAiPopAWheelieAsync);
            RunGotoPose(robot);
            RunCharger(robot);
        }
        if (!yes)
        {
            Console.WriteLine("\nA few questions, one per round, in the order the rounds ran.");
            Ask("pickedFirst", "FIRST ROUND (cube right in front): did he pick the cube up? [yes/no/tried and missed]");
            Ask("placedFirst", "FIRST ROUND: did he put it down on the table? [yes/no/dropped it]");
            if (withDrive)
            {
                Ask("pickedSecond", "Second time (he drove): did he reach the cube and pick it up? [yes/no/tried and missed]");
                Ask("placedSecond", "Second time: did he put it down? [yes/no/dropped it]");
            }
            if (Skills)
            {
                Ask("stacked", "Stack round: did he pick up the first cube, drive to the second and set it on top? [yes/no/partly]");
                Ask("rolled", "Roll round: did he drive to the cube and roll it onto its side? [yes/no/partly]");
            }
            Ask("odd", "Anything odd (bumped the cube, wrong cube, lift jerky, face or sounds strange)? Type it, or just press Enter.");
        }
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

    // Stack, then roll, through the outside-AI commands (QueueAiStackOnCubeAsync / QueueAiRollCubeAsync).
    void RunSkills(CozmoRobot r)
    {
        Console.WriteLine("\nSTACK ROUND. Leave the first cube where he put it down. Put a SECOND cube about 25 cm (10 in) in front of him,\n"
            + "a little to one side, UPRIGHT with lights on top and a side marker facing him. Press Enter when it is in place.");
        ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested();
        var cubes = SeeCubes("stack", 2);
        if (cubes.Count < 2) { errors.Enqueue($"stack: saw {cubes.Count} cube(s), need 2; stack round skipped"); Console.WriteLine("    He did not report two cubes; skipping the stack round."); }
        else
        {
            var (first, second) = (cubes[0], cubes[1]);
            Console.WriteLine($">>> WATCH: Cozmo should pick up cube {first.ObjectId}, then drive to cube {second.ObjectId} and set it on top <<<");
            var pick = Phase("stack-pickup", () => { var t = r.Motion.QueueAiPickupCubeAsync(first.ObjectId, true, cancel.Token); return (t, () => t.IsCompletedSuccessfully ? (t.Result.Admitted, t.Result.EngineResult, t.Result.Detail, t.Result.OwnerRetired) : default); }, 90000);
            pick["objectId"] = first.ObjectId; phases.Add(pick); Console.WriteLine("    " + pick["detail"]);
            Thread.Sleep(1000);
            var stack = Phase("stack-place", () => { var t = r.Motion.QueueAiStackOnCubeAsync(second.ObjectId, cancel.Token); return (t, () => t.IsCompletedSuccessfully ? (t.Result.Admitted, t.Result.EngineResult, t.Result.Detail, t.Result.OwnerRetired) : default); }, 90000);
            stack["objectId"] = second.ObjectId; phases.Add(stack); Console.WriteLine("    " + stack["detail"]);
        }
        Console.WriteLine("\nROLL ROUND. Take the cubes apart. Put ONE cube about 20 cm (8 in) in front of him, UPRIGHT, a side marker facing him;\n"
            + "keep the other out of view. He will drive to it and roll it onto its side. Press Enter when it is in place.");
        ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested();
        var target = SeeCubes("roll", 1);
        if (target.Count == 0) { errors.Enqueue("roll: no cube seen; roll round skipped"); Console.WriteLine("    He did not report a cube; skipping the roll round."); return; }
        Console.WriteLine($">>> WATCH: Cozmo should drive to cube {target[0].ObjectId} and roll it <<<");
        var roll = Phase("roll", () => { var t = r.Motion.QueueAiRollCubeAsync(target[0].ObjectId, cancel.Token); return (t, () => t.IsCompletedSuccessfully ? (t.Result.Admitted, t.Result.EngineResult, t.Result.Detail, t.Result.OwnerRetired) : default); }, 120000);
        roll["objectId"] = target[0].ObjectId; phases.Add(roll); Console.WriteLine("    " + roll["detail"]);
    }

    void RunTrick(string name, string setup, Func<uint, CancellationToken, Task<AiPickupActionResult>> queue)
    {
        Console.WriteLine("\n" + setup);
        ReadLineBounded();
        cancel.Token.ThrowIfCancellationRequested();
        var targets = SeeCubes(name, 1);
        if (targets.Count == 0)
        {
            errors.Enqueue($"{name}: no fresh cube report");
            Console.WriteLine($"    He did not report a cube. Skipping the {name} round.");
            return;
        }
        Console.WriteLine($">>> WATCH: {name} on cube {targets[0].ObjectId} <<<");
        RunSkillAction(name, targets[0].ObjectId, queue);
    }

    JsonObject RunSkillAction(string name, uint id, Func<uint, CancellationToken, Task<AiPickupActionResult>> queue)
    {
        using var actionCancel = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
        Task<AiPickupActionResult>? queued = null;
        var phase = Phase(name, () =>
        {
            var task = queued = queue(id, actionCancel.Token);
            return (task, () => task.IsCompletedSuccessfully ?
                (task.Result.Admitted, task.Result.EngineResult, task.Result.Detail, task.Result.OwnerRetired) : default);
        }, 120000);
        phase["objectId"] = id;
        phases.Add(phase);
        Console.WriteLine("    " + phase["detail"]);
        if (queued is { IsCompleted: false })
        {
            actionCancel.Cancel();
            if (!Poll(() => queued.IsCompleted, 3000))
                throw new InvalidOperationException($"{name}: the action did not retire after cancellation");
        }
        return phase;
    }

    void RunGotoPose(CozmoRobot r)
    {
        Console.WriteLine("\n" + M15CubeHw.GotoSetup);
        ReadLineBounded();
        cancel.Token.ThrowIfCancellationRequested();
        if (r.Motion.GetAiBodyObservation().LastState is not { } start)
        {
            errors.Enqueue("go-to-pose: no published engine pose");
            return;
        }

        // The operator's relative setup is converted to the absolute world-origin API input.
        float x = start.Pose.X + 200f * MathF.Cos(start.PoseAngleRad);
        float y = start.Pose.Y + 200f * MathF.Sin(start.PoseAngleRad);
        float angle = Radians.Rescale(start.PoseAngleRad + MathF.PI / 2);
        using var actionCancel = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
        Task<AiDriveActionResult>? queued = null;
        var phase = Phase("go-to-pose", () =>
        {
            var task = queued = r.Motion.QueueAiGotoPoseAsync(x, y, angle, actionCancel.Token);
            return (task, () => task.IsCompletedSuccessfully ?
                (task.Result.Admitted, task.Result.EngineResult, task.Result.Detail, task.Result.OwnerRetired) : default);
        }, 35000);
        phase["goal"] = new JsonObject { ["xMm"] = x, ["yMm"] = y, ["angleRad"] = angle, ["originId"] = start.Pose.OriginId };
        var end = r.Motion.GetAiBodyObservation().LastState;
        phase["finalPose"] = end is null ? null : JsonSerializer.SerializeToNode(end.Pose);
        phases.Add(phase);
        Console.WriteLine("    " + phase["detail"]);
        if (queued is { IsCompleted: false })
        {
            actionCancel.Cancel();
            if (!Poll(() => queued.IsCompleted, 3000))
                throw new InvalidOperationException("go-to-pose: the action did not retire after cancellation");
        }
    }

    void RunCharger(CozmoRobot r)
    {
        Console.WriteLine("\n" + M15CubeHw.ChargerSetup);
        ReadLineBounded();
        cancel.Token.ThrowIfCancellationRequested();
        var world = r.Vision.World;
        double since = Now;
        uint? charger = null;
        bool seen = Poll(() =>
        {
            foreach (var sighting in Enumerable.Reverse(chargerObservations.ToArray()))
            {
                if (sighting.T < since) break;
                var located = world.GetLocatedObjectById(sighting.Id);
                if (located is null || located.OriginId != world.CurrentOriginId || sighting.Origin != located.OriginId) continue;
                charger = sighting.Id;
                Event("charger-observed", new() { ["objectId"] = sighting.Id, ["robotTimestamp"] = sighting.Timestamp });
                return true;
            }
            return false;
        }, 30000);
        if (!seen || charger is not { } id)
        {
            errors.Enqueue("mount-charger: no fresh located charger seen in the current origin");
            Console.WriteLine("    He did not locate the charger. Skipping this round.");
            return;
        }
        Console.WriteLine($">>> WATCH: Cozmo should approach charger {id}, turn and back onto it <<<");
        var mount = RunSkillAction("mount-charger", id, r.Motion.QueueAiMountChargerAsync);
        mount["onChargerAfter"] = r.State.Latest?.Has(RobotStatusFlag.IsOnCharger);
    }

    // Distinct cubes the engine reports seeing after this call, nearest first; waits up to 30 s for `want` of them.
    List<RobotObservedObjectReport> SeeCubes(string name, int want)
    {
        Thread.Sleep(1500);
        double since = Now;
        Console.WriteLine($"    Looking for {want} cube(s) (up to 30 s)...");
        List<RobotObservedObjectReport> Seen() => observations.Where(o => o.T >= since).Select(o => o.Report)
            .GroupBy(o => o.ObjectId).Select(g => g.Last()).OrderBy(o => o.Pose.X * o.Pose.X + o.Pose.Y * o.Pose.Y).ToList();
        Poll(() => Seen().Count >= want, 30000);
        var seen = Seen();
        Event("observe", new() { ["phase"] = name, ["want"] = want, ["seen"] = JsonSerializer.SerializeToNode(seen) });
        foreach (var c in seen) Console.WriteLine($"    Seen: object {c.ObjectId} at ({c.Pose.X:F0}, {c.Pose.Y:F0}) mm.");
        return seen;
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
            foreach (var p in phases)
            {
                var o = p!.AsObject();
                double start = o["startMs"]!.GetValue<double>(), end = o["endMs"]!.GetValue<double>();
                if (Personality) o["engineReports"] = PersonalityReports(start, end);
                else o["timeline"] = Timeline(wire, start, end);
            }
            var missing = stackLines.Where(l => l.Contains("MISSING", StringComparison.Ordinal)).Distinct().ToArray();
            File.WriteAllText(Path.Combine(bundle, "SUMMARY.txt"), RunName + " test. Nothing here passes or fails.\n\n"
                + string.Join("\n", phases.Select(p => Personality
                    ? $"{p!["phase"]}: observation={p["observed"]}, reports={p["engineReports"]!.AsArray().Count}"
                    : $"{p!["phase"]}: admitted={p["admitted"]} result={p["engineResult"]} carryingAfter={p["carryingAfter"]} ({p["detail"]})"))
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
            string[] sources = { "cozmo-stack/src/Cozmo.Conformance/M15CubeHw.cs", "cozmo-stack/src/Cozmo.Conformance/HardwareEngineOptions.cs",
                "cozmo-stack/src/Cozmo.Conformance/HarnessStartup.cs", "re-analysis/fidelity_manifest.json" };
            var runSources = Personality
                ? sources.Concat(new[] { "cozmo-stack/src/Cozmo.Conformance/PersonalityHw.cs", "re-analysis/acceptance/hardware/scripts/personality-hw-1.md" })
                : sources.Append("re-analysis/acceptance/hardware/scripts/m15-cube-hw-1.md");
            foreach (var relative in runSources)
            {
                string source = Path.Combine(root, relative); if (!File.Exists(source)) { errors.Enqueue("Missing provenance file " + relative); continue; }
                string dest = Path.Combine(bundle, "provenance", relative); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(source, dest, true);
                files.Add(new JsonObject { ["path"] = relative, ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant() });
            }
            string? commit = null; try { var p = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true }); commit = p!.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(); } catch (Exception e) { errors.Enqueue("git: " + e.Message); }
            var report = new JsonObject { ["testId"] = Personality ? PersonalityHw.TestId : M15CubeHw.TestId, ["createdUtc"] = DateTime.UtcNow.ToString("O"), ["commit"] = commit, ["argv"] = JsonSerializer.SerializeToNode(argv),
                ["resources"] = resources, ["connected"] = connected, ["startup"] = startup?.ToJson(), ["cubeReports"] = observations.Count, ["phases"] = phases.DeepClone(), ["operator"] = answers.DeepClone(),
                ["missingReported"] = JsonSerializer.SerializeToNode(missing), ["records"] = JsonSerializer.SerializeToNode(records), ["provenance"] = files, ["errors"] = JsonSerializer.SerializeToNode(errors),
                ["acceptance"] = Personality ? "OBSERVATIONS_ONLY" : "NOT_JUDGED: manager review required; hardware success does not raise provenance" };
            File.WriteAllText(Path.Combine(bundle, "result.json"), report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(bundle, "README.txt"), Personality
                ? "PERSONALITY-HW-1 observations. result.json has phase times, operator answers and native behavior, reaction, objective and face reports. events.jsonl retains all timestamped reports, including those between phases, and engine logs. Times are host capture times."
                : "M15-CUBE-HW evidence bundle. result.json holds each phase's action result and a timeline of the dock (0x42), place (0x44), correction (0x48) and PickAndPlaceResult (0xB8) messages interleaved with the robot's busy/carrying/moving status transitions; status.jsonl and observations.jsonl keep the full streams; messages.jsonl deduplicates reliable resends; frames.jsonl keeps every datagram. Times are host capture times. No acceptance or manifest change was made.");
        }
        catch (Exception e) { Console.Error.WriteLine("Could not write the results folder: " + e.Message); }
    }
}
