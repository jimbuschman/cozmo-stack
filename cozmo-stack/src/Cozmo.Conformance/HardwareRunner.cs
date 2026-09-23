using System.Net;
using System.Text;
using Cozmo.Robot;

namespace Cozmo.Conformance;

/// <summary>
/// The interactive hardware acceptance runner: one command that walks a person standing next to the robot
/// through the whole campaign, runs the existing conformance tool for each check, keeps its evidence, and
/// asks for a human verdict that is never inferred from the tool's own result.
///
/// Three rules shape it.
///
/// <b>The person decides.</b> The automated half and the human half are recorded separately and neither
/// overrides the other. A tool that is happy while the observer is not is the interesting case and it stays
/// visible; an observer who cannot tell answers unsure, which is a real answer and not a failure.
///
/// <b>Nothing here upgrades provenance.</b> A check that passes says the behaviour was observed on hardware.
/// It does not change a fidelity record's status, and a check that fails is an investigation item - not a
/// licence to tune a source-backed constant until the robot agrees.
/// </summary>
/// <remarks>
/// <code>
/// hardware-test 172.31.1.1 --obb &lt;dir&gt;            run, or resume, the whole campaign
/// hardware-test 172.31.1.1 --obb &lt;dir&gt; --resume    carry on after a stop, a reboot or a flat battery
/// hardware-test 172.31.1.1 --rerun V                run one check again, keeping everything else
/// hardware-test 172.31.1.1 --rerun-failed           run everything that failed, was unsure or was cut short
/// hardware-test 172.31.1.1 --status                 what has been done so far, and what is next
/// hardware-test 172.31.1.1 --export                 write results.json and SUMMARY.md and stop
/// hardware-test --list                              the campaign, in order, with what each needs
/// </code>
/// </remarks>
public static class HardwareRunner
{
    public const string DefaultSessionFile = "hardware-session.json";

    public static async Task<int> Run(string[] a)
    {
        if (a.Contains("--list")) { List(); return 0; }
        if (a.Contains("--plan")) { Plan(); return 0; }
        if (a.Length < 2 || a[1].StartsWith("--")) { Console.WriteLine(Usage); return 1; }

        string ip = a[1];
        string? obb = Arg(a, "--obb");
        bool nominal = a.Contains("--nominal");
        string? from = Arg(a, "--from");
        string? rerun = Arg(a, "--rerun");
        bool rerunFailed = a.Contains("--rerun-failed");
        bool statusOnly = a.Contains("--status");
        bool exportOnly = a.Contains("--export");
        var only = Arg(a, "--only")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string sessionFile = Arg(a, "--session") ?? Path.Combine(AcceptanceRoot(), DefaultSessionFile);

        // Resuming keeps the run directory the session was started with, so one campaign's evidence stays in
        // one place however many sittings it takes.
        var loaded = HardwareSession.Load(sessionFile);
        bool continuing = loaded is not null && (a.Contains("--resume") || rerun is not null || rerunFailed || statusOnly || exportOnly);
        string evidence = Arg(a, "--out")
                       ?? (continuing ? loaded!.EvidenceDirectory : null)
                       ?? Path.Combine(AcceptanceRoot(), $"hardware-{DateTime.Now:yyyyMMdd-HHmmss}");

        HardwareSession session;
        if (continuing)
        {
            session = loaded!;
            Console.WriteLine($"session {sessionFile}: {session.Results.Count} check(s) already recorded");
            AnnounceMigration(session);
        }
        else
        {
            if (loaded is not null && !a.Contains("--resume") && only is null && from is null)
            {
                Console.WriteLine($"a session already exists at {sessionFile} with {loaded.Results.Count} recorded check(s).");
                Console.Write("  [R] resume it   [N] start a new campaign   [S] status   [Q] quit: ");
                var choice = (Console.ReadLine() ?? "q").Trim().ToLowerInvariant();
                if (choice == "q") return 0;
                if (choice is "r" or "s")
                {
                    session = loaded;
                    AnnounceMigration(session);
                    evidence = Arg(a, "--out") ?? loaded.EvidenceDirectory ?? evidence;
                    session.EvidenceDirectory = evidence;
                    if (choice == "s") { Status(session, new HardwareRunOptions { Ip = ip, Obb = obb, EvidenceDirectory = evidence }, sessionFile); return 0; }
                }
                else session = new HardwareSession(HardwareCatalog.All, ip, obb) { EvidenceDirectory = evidence };
            }
            else session = new HardwareSession(HardwareCatalog.All, ip, obb) { EvidenceDirectory = evidence };
        }
        session.EvidenceDirectory = evidence;
        if (from is not null) session.From = from;
        if (only is { Length: > 0 }) session.Only = only;

        var options = new HardwareRunOptions { Ip = ip, Obb = obb, EvidenceDirectory = evidence, AllowNominalCalibration = nominal };
        Directory.CreateDirectory(evidence);

        if (statusOnly) { Status(session, options, sessionFile); return 0; }
        if (exportOnly)
        {
            var (results, summary) = HardwareEvidence.WriteRun(session, options);
            Console.WriteLine($"results: {results}");
            Console.WriteLine($"summary: {summary}");
            return 0;
        }
        if (rerun is not null)
        {
            foreach (var id in rerun.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (HardwareCatalog.Find(id) is null) { Console.WriteLine($"no check called '{id}'"); return 1; }
                session.Clear(id);
                Console.WriteLine($"{id} cleared; it will run again.");
            }
            session.Only = rerun.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        if (rerunFailed)
        {
            var again = session.Unresolved().Select(c => c.Id).ToArray();
            if (again.Length == 0) { Console.WriteLine("nothing failed, was unsure or was cut short: there is nothing to run again."); return 0; }
            foreach (var id in again) session.Clear(id);
            session.Only = again;
            Console.WriteLine($"running again: {string.Join(", ", again)}");
        }

        using var quit = new CancellationTokenSource();
        var stopping = new CancellationTokenSource();       // cancels the check that is running
        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            e.Cancel = true;                                // stop this check, not the process: the session must be saved
            Console.WriteLine("\n*** EMERGENCY STOP: stopping the robot and ending this check. ***");
            EmergencyStop(ip);
            if (!stopping.IsCancellationRequested) stopping.Cancel();
            if (!quit.IsCancellationRequested) quit.Cancel();
        };
        Console.CancelKeyPress += onCancel;

        try
        {
            Banner(session, options, sessionFile);
            string? phase = null;

            while (session.Next() is { } check)
            {
                if (quit.IsCancellationRequested) break;
                if (check.Phase != phase) { PhaseHeading(check.Phase, session); phase = check.Phase; }

                if (session.StaticBlock(check) is { } block)
                {
                    Console.WriteLine();
                    Rule();
                    Console.WriteLine($"CHECK {check.Id} — {check.Name}   BLOCKED");
                    Console.WriteLine($"  {Wrap(block.Reason)}");
                    Console.WriteLine("  You will not be asked to perform it. Nothing that depends on it will start either.");
                    var blocked = session.RecordBlocked(check.Id, block.Reason);
                    HardwareEvidence.WriteRecord(check, blocked, null, options, options.TestDirectory(check.Id));
                    session.Save(sessionFile);
                    continue;
                }

                var pick = Brief(check, options, session);
                if (pick == 'q') break;
                if (pick == 's')
                {
                    var skipped = session.Record(new HardwareResult
                    {
                        Id = check.Id, Auto = AutoOutcome.Skipped, Human = HumanOutcome.Skipped,
                        EvidenceDirectory = options.TestDirectory(check.Id), FinishedUtc = DateTime.UtcNow,
                    });
                    HardwareEvidence.WriteRecord(check, skipped, null, options, options.TestDirectory(check.Id));
                    session.Save(sessionFile);
                    continue;
                }

                bool again = true;
                while (again)
                {
                    again = false;
                    if (stopping.IsCancellationRequested) { stopping.Dispose(); stopping = new CancellationTokenSource(); }

                    if (check.MovesRobot && !await Preflight(check, options))
                    {
                        Console.Write("  [R] check again   [S] skip this check   [Q] stop the campaign: ");
                        var answer = (Console.ReadLine() ?? "q").Trim().ToLowerInvariant();
                        if (answer == "r") { again = true; continue; }
                        if (answer == "q") { quit.Cancel(); break; }
                        var skipped = session.Record(new HardwareResult
                        {
                            Id = check.Id, Auto = AutoOutcome.Skipped, Human = HumanOutcome.Skipped,
                            HumanNote = "skipped: the robot was not ready for a movement check",
                            EvidenceDirectory = options.TestDirectory(check.Id), FinishedUtc = DateTime.UtcNow,
                        });
                        HardwareEvidence.WriteRecord(check, skipped, null, options, options.TestDirectory(check.Id));
                        session.Save(sessionFile);
                        break;
                    }

                    var started = DateTime.UtcNow;
                    var run = await Execute(check, options, stopping.Token);
                    var auto = Evaluate(check, run);

                    Console.WriteLine();
                    Console.WriteLine("  WHAT THE SOFTWARE SAW");
                    Console.WriteLine($"    automated: {Describe(auto)}  ({check.AutoRule})");
                    if (run.Error is not null) Console.WriteLine($"    the tool threw: {run.Error.GetType().Name}: {run.Error.Message}");
                    if (run.InterruptedReason is not null) Console.WriteLine($"    interrupted: {run.InterruptedReason}");
                    ShowEvidence(check, run);
                    Console.WriteLine($"    evidence:  {options.TestDirectory(check.Id)}");

                    if (check.MovesRobot) EmergencyStop(options.Ip);

                    // A check that did not finish is not a check that failed, unless the disconnect was the
                    // thing being tested. Nothing was observed, so nothing is recorded but the interruption.
                    if (run.InterruptedReason is not null && !check.ExpectsDisconnect)
                    {
                        var cut = session.Record(new HardwareResult
                        {
                            Id = check.Id, Auto = auto, Human = HumanOutcome.NotAsked,
                            InterruptedReason = run.InterruptedReason, LogPath = run.LogPath,
                            AcceptanceRecord = run.AcceptanceRecord, EvidenceDirectory = options.TestDirectory(check.Id),
                            StartedUtc = started, FinishedUtc = DateTime.UtcNow,
                        });
                        HardwareEvidence.WriteRecord(check, cut, run, options, options.TestDirectory(check.Id));
                        session.Save(sessionFile);
                        Console.WriteLine($"  recorded:  INTERRUPTED — {run.InterruptedReason}");
                        Console.WriteLine("  It stays to be run again. Movement checks will not continue until the robot is ready.");
                        if (quit.IsCancellationRequested) break;
                        Console.Write("  [R] try it again now   [Enter] move on and come back to it   [Q] stop: ");
                        var answer = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                        if (answer == "r") { again = true; continue; }
                        if (answer == "q") quit.Cancel();
                        break;
                    }

                    var verdict = AskVerdict(check);
                    if (verdict == 'r') { Console.WriteLine("  running it again."); session.Clear(check.Id); again = true; continue; }
                    if (verdict == 'q')
                    {
                        var stopped = session.Record(Result(check, run, auto, HumanOutcome.NotAsked, started, options) with
                        {
                            InterruptedReason = "the campaign was stopped before a verdict was given",
                        });
                        HardwareEvidence.WriteRecord(check, stopped, run, options, options.TestDirectory(check.Id));
                        session.Save(sessionFile);
                        quit.Cancel();
                        break;
                    }

                    var human = verdict switch
                    {
                        'p' => HumanOutcome.Pass,
                        'f' => HumanOutcome.Fail,
                        'u' => HumanOutcome.Unsure,
                        _ => HumanOutcome.Skipped,
                    };
                    string? note = null;
                    if (human is HumanOutcome.Fail or HumanOutcome.Unsure)
                    {
                        Console.Write(human == HumanOutcome.Fail
                            ? "  what went wrong (one line, it goes in the investigation list): "
                            : "  what was unclear (one line): ");
                        note = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(note)) note = null;
                    }
                    var result = session.Record(Result(check, run, auto, human, started, options) with { HumanNote = note });
                    HardwareEvidence.WriteRecord(check, result, run, options, options.TestDirectory(check.Id));
                    session.Save(sessionFile);
                    HardwareEvidence.WriteRun(session, options);
                    Console.WriteLine($"  recorded:  {result.Status.ToString().ToUpperInvariant()}");
                    if (result.Status is CheckStatus.Failed or CheckStatus.Partial)
                        Console.WriteLine("  This is now an investigation item. Nothing is tuned to make it pass; the evidence is kept.");
                }
            }

            if (session.Complete) session.Finish();
            session.Save(sessionFile);
            var (resultsFile, summaryFile) = HardwareEvidence.WriteRun(session, options);
            PrintSummary(session, sessionFile, resultsFile, summaryFile);
            return session.Tally().Failed > 0 ? 1 : 0;
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
            stopping.Dispose();
        }
    }

    // ------------------------------------------------------------------ safety

    /// <summary>
    /// Before anything moves: is he there, is he talking, and has he finished calibrating? The robot
    /// recalibrates head and lift on every connection and a motor command during that is both meaningless
    /// and unkind to the mechanism, so a movement check does not start until it is done.
    /// </summary>
    private static async Task<bool> Preflight(HardwareCheck check, HardwareRunOptions o)
    {
        Console.WriteLine();
        Console.WriteLine("  SAFETY CHECK before he moves");
        try
        {
            using var robot = await CozmoRobot.ConnectAsync(IPAddress.Parse(o.Ip), timeout: TimeSpan.FromSeconds(6));
            Console.WriteLine($"    connection:  ok ({robot.State.FirmwareVersionNumber})");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (robot.State.StateCount < 20 && sw.Elapsed.TotalSeconds < 4) await Task.Delay(50);
            bool telemetry = robot.State.StateCount >= 20;
            Console.WriteLine($"    telemetry:   {(telemetry ? "ok" : "NOT ARRIVING")} ({robot.State.StateCount} states, {robot.State.StateRateHz:F1} Hz)");

            await robot.WaitForMotorCalibrationAsync(TimeSpan.FromSeconds(12));
            bool calibrated = robot.State.CalibrationComplete;
            Console.WriteLine($"    head/lift:   {(calibrated ? "calibration complete" : "STILL CALIBRATING or never reported")}");

            robot.EmergencyStop();
            robot.Disconnect();

            if (telemetry && calibrated) { Console.WriteLine("    ready."); return true; }
            Console.WriteLine("    NOT READY. A movement check will not start until telemetry is flowing and the motors have calibrated.");
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine($"    connection:  FAILED ({e.GetType().Name}: {e.Message})");
            Console.WriteLine("    NOT READY. Check that he is awake, charged and on the network.");
            return false;
        }
    }

    /// <summary>
    /// Stops the motors, on a connection of its own. Used after anything that moved him and by the
    /// emergency stop, so a cancelled or failed check never leaves him driving.
    /// </summary>
    private static void EmergencyStop(string ip)
    {
        try
        {
            using var robot = CozmoRobot.ConnectAsync(IPAddress.Parse(ip), timeout: TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            robot.EmergencyStop();
            robot.Disconnect();
        }
        catch (Exception e)
        {
            Console.WriteLine($"  note: could not open a connection to confirm the motors are stopped ({e.GetType().Name}). "
                            + "If he is still moving, pick him up or put him on his back.");
        }
    }

    // ------------------------------------------------------------------ running one check

    /// <summary>
    /// Calls the conformance tool for a check in this process, teeing its output to the console and to the
    /// check's own log. The tools are the implementations the plan already names, so nothing here
    /// re-implements a check.
    /// </summary>
    public static async Task<HardwareToolRun> Execute(HardwareCheck check, HardwareRunOptions o, CancellationToken cancel,
                                                      Func<string[], CancellationToken, Task<int>>? invoke = null)
    {
        var dir = o.TestDirectory(check.Id);
        var args = check.Command(o);
        string log = Path.Combine(dir, "console.log");
        Console.WriteLine();
        Console.WriteLine($"  running: {string.Join(' ', args)}");
        Console.WriteLine($"  (Ctrl+C stops the robot and ends this check at any time)");
        Rule();

        var captured = new StringBuilder();
        var original = Console.Out;
        using var file = new StreamWriter(log, append: false) { AutoFlush = true };
        Console.SetOut(new TeeWriter(original, file, captured));
        int exit = -1;
        Exception? error = null;
        RunEnding ending;
        try
        {
            var work = (invoke ?? Invoke)(args, cancel);
            var done = await Task.WhenAny(work, Task.Delay(check.Timeout, cancel));
            if (ReferenceEquals(done, work)) { exit = await work; ending = RunEnding.Completed; }
            else ending = cancel.IsCancellationRequested ? RunEnding.Cancelled : RunEnding.TimedOut;
        }
        catch (OperationCanceledException) { ending = RunEnding.Cancelled; }
        catch (Exception e) { error = e; ending = RunEnding.Faulted; }
        finally { Console.SetOut(original); }

        Rule();
        var text = captured.ToString();
        string? record = text.Split('\n')
            .FirstOrDefault(l => l.Contains("acceptance record:", StringComparison.OrdinalIgnoreCase))
            ?.Split("acceptance record:", StringSplitOptions.TrimEntries).ElementAtOrDefault(1)?.Trim();

        var run = new HardwareToolRun(exit, text, record, log, error) { Ending = ending };
        return run with { InterruptedReason = Interruption(run, check) };
    }

    /// <summary>
    /// Whether the check was cut short, decided by how its run ended rather than by anything it printed.
    ///
    /// The distinction the runner got wrong is ownership. Every one of these tools opens its own connection
    /// and closes it again on the way out, and the closing is announced - "disconnected: requested" - so a
    /// runner that reads the transcript for the word sees a lost link at the end of every successful check.
    /// It is the tool's own cleanup, performed after the check has already done its work, and it says nothing
    /// about whether the robot was there while the work happened.
    ///
    /// So the question is not what was printed but whether the tool was still running when the trouble
    /// arrived. A tool that returned owned its connection for the whole check and then let it go: nothing was
    /// interrupted, whatever the last line says, and its result stands for the person to judge. A tool that
    /// never returned - stopped by the person, or still going when its allowance ran out - was interrupted,
    /// because the work did not finish. A tool that threw is judged by what it threw: the exception types the
    /// transport raises when the robot has gone (<c>SendData</c> on a closed link throws
    /// <see cref="InvalidOperationException"/>; a disposed one throws <see cref="ObjectDisposedException"/>;
    /// the socket layer and the connect handshake have their own) mean the link went while the check was
    /// live, and anything else is an error for the judge and the person to weigh.
    /// </summary>
    public static string? Interruption(HardwareToolRun run, HardwareCheck check) => run.Ending switch
    {
        RunEnding.Completed => null,
        RunEnding.Cancelled => "stopped part way (emergency stop or Ctrl+C)",
        RunEnding.TimedOut => "no result within the check's allowance of "
                            + (check.Timeout.TotalMinutes >= 1
                                ? $"{check.Timeout.TotalMinutes:F0} minute(s)"
                                : $"{check.Timeout.TotalSeconds:F0} second(s)"),
        RunEnding.Faulted when !check.ExpectsDisconnect && IsLostLink(run.Error) =>
            "the link to the robot was lost while the check was running",
        _ => null,
    };

    /// <summary>
    /// What the stack throws when the robot is no longer there. The animation tick loop treats the same two
    /// types as the robot having gone away (the CORE-002 correction), and the socket and handshake failures
    /// are the two ways it can go before a check ever gets started.
    /// </summary>
    private static bool IsLostLink(Exception? e) => e is InvalidOperationException
                                                      or ObjectDisposedException
                                                      or System.Net.Sockets.SocketException
                                                      or TimeoutException;

    /// <summary>
    /// The conformance commands the checks use. A deliberate subset of the program's own dispatch: the
    /// runner calls these entry points directly rather than starting another process.
    /// </summary>
    private static async Task<int> Invoke(string[] args, CancellationToken cancel) => args[0] switch
    {
        "connect" => await SmokeTest.Run(args),
        "sensors" => await Control.Sensors(args),
        "cubes" => await Control.Cubes(args),
        "calibrate" => await Control.Calibrate(args),
        "drive" => await Control.Drive(args),
        "camera" => await Devices.Camera(args),
        "face" => await Devices.Face(args),
        "tone" => await Devices.Tone(args),
        "anim" => await Anim.Play(args),
        "behavior" => await BehaviorTool.Run(args),
        "sing" => await SingTool.Run(args),
        "offtreads" => await ReactionsTool.OffTreads(args),
        "reactions" => await ReactionsTool.Run(args),
        "vision" => await VisionTool.Run(args),
        "bodyangle" => await VisionTool.BodyAngle(args),
        "manip" => await ManipTool.Run(args),
        "freeplay" => await FreeplayTool.Run(args),
        "core" => await CoreChecks.Run(args),
        _ => throw new InvalidOperationException($"the runner has no entry point for '{args[0]}'"),
    };

    private static HardwareResult Result(HardwareCheck check, HardwareToolRun run, AutoOutcome auto, HumanOutcome human,
                                         DateTime started, HardwareRunOptions o) =>
        new()
        {
            Id = check.Id,
            Auto = auto,
            Human = human,
            AcceptanceRecord = run.AcceptanceRecord,
            LogPath = run.LogPath,
            EvidenceDirectory = o.TestDirectory(check.Id),
            AutoDetail = run.Error?.Message ?? (auto == AutoOutcome.Pass ? null : $"exit {run.ExitCode}"),
            StartedUtc = started,
            FinishedUtc = DateTime.UtcNow,
        };

    // ------------------------------------------------------------------ the person's side

    private static void Banner(HardwareSession s, HardwareRunOptions o, string sessionFile)
    {
        var selected = s.Selected().ToList();
        Console.WriteLine();
        Rule();
        Console.WriteLine("COZMO HARDWARE ACCEPTANCE");
        Console.WriteLine($"  robot:    {s.Ip}");
        Console.WriteLine($"  checks:   {selected.Count} ({selected.Count(c => c.Runnable)} runnable, {selected.Count(c => !c.Runnable)} blocked on this build)");
        Console.WriteLine($"  evidence: {o.EvidenceDirectory}   (one directory per check, plus results.json and SUMMARY.md)");
        Console.WriteLine($"  session:  {sessionFile}   (saved after every check; --resume carries on here)");
        if (o.Obb is null) Console.WriteLine("  note:     no --obb given; the checks that need the OBB assets will not run properly.");
        Console.WriteLine();
        Console.WriteLine("  Each check tells you what it tests, what to set up, what it will do and what you should");
        Console.WriteLine("  see. Then you give the verdict: your answer is what counts, and the tool's own result");
        Console.WriteLine("  never decides it for you.");
        Console.WriteLine();
        Console.WriteLine("  *** Ctrl+C at any time is the EMERGENCY STOP: it stops the robot's motors and ends the");
        Console.WriteLine("      check. Nothing recorded so far is lost. ***");
        Rule();
    }

    private static void PhaseHeading(string phase, HardwareSession s)
    {
        var inPhase = s.Selected().Where(c => c.Phase == phase).ToList();
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine($"PHASE {phase.ToUpperInvariant()}   ({inPhase.Count} check(s): {string.Join(" ", inPhase.Select(c => c.Id))})");
        Console.WriteLine(new string('=', 78));
    }

    /// <summary>The briefing, and the choice to start, skip or stop.</summary>
    private static char Brief(HardwareCheck c, HardwareRunOptions o, HardwareSession s)
    {
        Console.WriteLine();
        Rule();
        Console.WriteLine($"TEST {c.Id} — {c.Name}   [{c.Milestone}]");
        Console.WriteLine();
        Console.WriteLine($"  WHAT IS BEING TESTED   {Wrap(c.Subsystem, indent: 25)}");
        Console.WriteLine($"  WHY IT EXISTS          {Wrap(c.Why, indent: 25)}");
        if (c.Prerequisites.Count > 0)
        {
            Console.WriteLine("  BEFORE YOU START");
            foreach (var p in c.Prerequisites) Console.WriteLine($"                         - {Wrap(p, indent: 27)}");
        }
        Console.WriteLine($"  SET UP                 {Wrap(c.Setup, indent: 25)}");
        Console.WriteLine($"  WHAT YOU DO            {Wrap(c.DoThis, indent: 25)}");
        Console.WriteLine($"  WHAT THE PROGRAM DOES  {Wrap(c.CommandLine(o), indent: 25)}");
        Console.WriteLine($"  EXPECTED IN SOFTWARE   {Wrap(c.ExpectedTelemetry ?? c.AutoRule, indent: 25)}");
        Console.WriteLine($"  EXPECTED IN THE ROOM   {Wrap(c.Success, indent: 25)}");
        Console.WriteLine($"  AFTERWARDS             {Wrap(c.Cleanup, indent: 25)}");
        Console.WriteLine($"  ALLOWED                {c.Timeout.TotalMinutes:F0} minutes");
        if (c.NeedsCube || c.NeedsCharger || c.NeedsHandling || c.NeedsObb)
        {
            var needs = new List<string>();
            if (c.NeedsCube) needs.Add("a cube");
            if (c.NeedsCharger) needs.Add("the charger");
            if (c.NeedsHandling) needs.Add("you to handle or move him");
            if (c.NeedsObb) needs.Add("the OBB assets");
            Console.WriteLine($"  NEEDS                  {string.Join(", ", needs)}");
        }
        if (c.FidelityRecords.Count > 0)
            Console.WriteLine($"  FIDELITY RECORDS       {string.Join(", ", c.FidelityRecords)} (this result does not change their status)");
        if (c.CoreRegressions.Count > 0)
            Console.WriteLine($"  CORE-REVIEW COVERAGE   {string.Join(", ", c.CoreRegressions)}");
        if (c.KnownIssue is not null)
        {
            Console.WriteLine();
            Console.WriteLine($"  KNOWN ISSUE            {Wrap(c.KnownIssue, indent: 25)}");
        }
        if (s.UnprovenPrerequisites.TryGetValue(c.Id, out var unproven))
        {
            Console.WriteLine();
            Console.WriteLine($"  NOTE: {string.Join(", ", unproven)} normally runs first and has not been established in "
                            + "this session. You asked for this check by name, so it will run anyway.");
        }
        if (c.MovesRobot)
        {
            Console.WriteLine();
            Console.WriteLine("  *** HE WILL MOVE UNDER HIS OWN POWER. Clear the area and keep a hand ready. ***");
        }
        Console.WriteLine();
        var done = s.Tally();
        Console.WriteLine($"  progress: {done.Passed} passed, {done.Failed} failed, {done.Partial} inconclusive, {done.Unsure} unsure, "
                        + $"{done.Interrupted} cut short, {done.Blocked} blocked, {done.Skipped} skipped, {done.Pending} to go");
        Console.Write(c.MovesRobot
            ? "  [Enter] start (he will move)   [S] skip   [Q] stop and save: "
            : "  [Enter] start   [S] skip   [Q] stop and save: ");
        return (Console.ReadLine() ?? "q").Trim().ToLowerInvariant() switch
        {
            "s" => 's',
            "q" => 'q',
            _ => '\n',
        };
    }

    /// <summary>What the tool's output said, under the labels the check asked for.</summary>
    private static void ShowEvidence(HardwareCheck check, HardwareToolRun run)
    {
        var captured = HardwareEvidence.Capture(check, run.Output);
        foreach (var (label, lines) in captured)
        {
            Console.WriteLine($"    {label}:");
            foreach (var l in lines.Take(6)) Console.WriteLine($"      {l.Trim()}");
            if (lines.Length > 6) Console.WriteLine($"      ... {lines.Length - 6} more in the record");
        }
        var warnings = HardwareEvidence.Warnings(run.Output);
        if (warnings.Length > 0)
        {
            Console.WriteLine("    warnings:");
            foreach (var w in warnings.Take(4)) Console.WriteLine($"      {w}");
        }
    }

    /// <summary>The human verdict. Never derived from the automated outcome.</summary>
    private static char AskVerdict(HardwareCheck c)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("  WHAT YOU SHOULD HAVE SEEN");
            Console.WriteLine($"    {Wrap(c.Success, indent: 4)}");
            Console.WriteLine();
            Console.WriteLine($"  {c.Question}");
            Console.Write("  [P] pass   [F] fail   [U] unsure   [S] skip   [R] run it again   [Q] stop and save: ");
            var line = (Console.ReadLine() ?? "q").Trim().ToLowerInvariant();
            if (line is "p" or "f" or "u" or "s" or "r" or "q") return line[0];
            Console.WriteLine("  answer p, f, u, s, r or q.");
        }
    }

    /// <summary>
    /// Says what an older session lost on the way in. Only entries written when a dependency gate was
    /// mistaken for a permanent block are cleared, and the person is told which, because a check quietly
    /// changing from blocked to pending between sittings is exactly the sort of thing that should not happen
    /// silently.
    /// </summary>
    private static void AnnounceMigration(HardwareSession s)
    {
        if (s.Migrated.Count == 0) return;
        Console.WriteLine($"  {s.Migrated.Count} check(s) had been recorded as blocked by a prerequisite that had not");
        Console.WriteLine($"  been established: {string.Join(", ", s.Migrated)}.");
        Console.WriteLine("  Those are not observations, so they have been cleared. Each one is pending again and");
        Console.WriteLine("  becomes eligible as soon as what it waits for passes. Nothing anybody observed was touched.");
    }

    /// <summary>What a check with no result is waiting for, for the status and summary lines.</summary>
    private static string Waiting(HardwareSession s, HardwareCheck c) =>
        s.GatedBy(c) is { } gate ? "waiting: " + gate.Reason : "";

    private static void Status(HardwareSession s, HardwareRunOptions o, string sessionFile)
    {
        var t = s.Tally();
        Console.WriteLine();
        Rule();
        Console.WriteLine("STATUS");
        string? phase = null;
        foreach (var c in s.Selected())
        {
            if (c.Phase != phase) { Console.WriteLine($"  -- {c.Phase}"); phase = c.Phase; }
            var status = s.StatusOf(c.Id);
            s.Results.TryGetValue(c.Id, out var r);
            string detail = r?.BlockedReason ?? r?.InterruptedReason ?? r?.HumanNote ?? Waiting(s, c);
            if (detail.Length > 60) detail = detail[..60] + "...";
            Console.WriteLine($"     {c.Id,-4} {status.ToString().ToUpperInvariant(),-11} {c.Name}{(detail.Length > 0 ? "  — " + detail : "")}");
        }
        Console.WriteLine();
        Console.WriteLine($"  {t.Passed} passed, {t.Failed} failed, {t.Partial} inconclusive, {t.Unsure} unsure, "
                        + $"{t.Interrupted} cut short, {t.Blocked} blocked, {t.Skipped} skipped, {t.Pending} not run");
        if (s.Next() is { } next) Console.WriteLine($"  next: {next.Id} — {next.Name}");
        Console.WriteLine($"  session:  {sessionFile}");
        Console.WriteLine($"  evidence: {o.EvidenceDirectory}");
        Rule();
    }

    /// <summary>The campaign itself, without a robot: what it covers and what each check needs.</summary>
    private static void List()
    {
        string? phase = null;
        Console.WriteLine();
        Console.WriteLine($"COZMO HARDWARE ACCEPTANCE — {HardwareCatalog.All.Count} checks");
        foreach (var c in HardwareCatalog.All)
        {
            if (c.Phase != phase) { Console.WriteLine(); Console.WriteLine($"-- {c.Phase}"); phase = c.Phase; }
            var needs = new List<string>();
            if (c.NeedsCube) needs.Add("cube");
            if (c.NeedsCharger) needs.Add("charger");
            if (c.NeedsHandling) needs.Add("handling");
            if (c.NeedsObb) needs.Add("obb");
            if (c.MovesRobot) needs.Add("moves");
            Console.WriteLine($"  {c.Id,-4} {c.Name,-52} {(c.Runnable ? string.Join("+", needs) : "BLOCKED")}");
            if (!c.Runnable) Console.WriteLine($"       {Wrap(c.BlockedReason!, indent: 7)}");
        }
        Console.WriteLine();
        Console.WriteLine($"  {HardwareCatalog.All.Count(c => c.Runnable)} runnable, {HardwareCatalog.All.Count(c => !c.Runnable)} blocked.");
        Console.WriteLine($"  cubes: {string.Join(" ", HardwareCatalog.All.Where(c => c.NeedsCube).Select(c => c.Id))}");
        Console.WriteLine($"  charger: {string.Join(" ", HardwareCatalog.All.Where(c => c.NeedsCharger).Select(c => c.Id))}");
        Console.WriteLine($"  you handle him: {string.Join(" ", HardwareCatalog.All.Where(c => c.NeedsHandling).Select(c => c.Id))}");
    }

    /// <summary>
    /// The campaign as a markdown table, so <c>HARDWARE_TEST_PLAN.md</c> and the executable campaign can be
    /// kept in step by regenerating rather than by remembering.
    /// </summary>
    private static void Plan()
    {
        string? phase = null;
        foreach (var c in HardwareCatalog.All)
        {
            if (c.Phase != phase)
            {
                phase = c.Phase;
                Console.WriteLine();
                Console.WriteLine($"### {c.Phase}");
                Console.WriteLine();
                Console.WriteLine("| # | check | what it tests | you need | after | automated verdict | human verdict |");
                Console.WriteLine("| --- | --- | --- | --- | --- | --- | --- |");
            }
            var needs = new List<string>();
            if (c.NeedsCube) needs.Add("cube");
            if (c.NeedsCharger) needs.Add("charger");
            if (c.NeedsHandling) needs.Add("you handle him");
            if (c.NeedsObb) needs.Add("`--obb`");
            if (c.MovesRobot) needs.Add("**he moves**");
            Console.WriteLine($"| {c.Id} | **{c.Name}** | {Cell(c.Subsystem)} | {(needs.Count == 0 ? "-" : string.Join(", ", needs))} "
                            + $"| {(c.Requires.Count == 0 ? "-" : string.Join(", ", c.Requires))} "
                            + $"| {(c.Runnable ? Cell(c.AutoRule) : "not run")} "
                            + $"| {(c.Runnable ? Cell(c.Success) : Cell(c.BlockedReason!))} |");
        }
    }

    private static string Cell(string s) => s.Replace('|', '/').Replace(Environment.NewLine, " ");

    private static void PrintSummary(HardwareSession s, string sessionFile, string resultsFile, string summaryFile)
    {
        var t = s.Tally();
        Console.WriteLine();
        Rule();
        Console.WriteLine("SUMMARY");
        string? phase = null;
        foreach (var c in s.Selected())
        {
            if (c.Phase != phase) { Console.WriteLine($"  -- {c.Phase}"); phase = c.Phase; }
            var status = s.StatusOf(c.Id);
            s.Results.TryGetValue(c.Id, out var r);
            string detail = status switch
            {
                CheckStatus.Blocked => r?.BlockedReason ?? "blocked",
                CheckStatus.Interrupted => r?.InterruptedReason ?? "cut short",
                CheckStatus.Partial => $"the tool said {Describe(r!.Auto)}, you said {r.Human}",
                CheckStatus.Unsure => r?.HumanNote ?? "could not tell",
                CheckStatus.Failed => r?.HumanNote ?? "failed",
                CheckStatus.Pending => Waiting(s, c),
                _ => "",
            };
            if (detail.Length > 70) detail = detail[..70] + "...";
            Console.WriteLine($"     {c.Id,-4} {status.ToString().ToUpperInvariant(),-11} {c.Name}{(detail.Length > 0 ? "  — " + detail : "")}");
        }
        Console.WriteLine();
        Console.WriteLine($"  {t.Passed} passed, {t.Failed} failed, {t.Partial} inconclusive, {t.Unsure} unsure, "
                        + $"{t.Interrupted} cut short, {t.Blocked} blocked, {t.Skipped} skipped, {t.Pending} not run");
        Console.WriteLine($"  results: {resultsFile}");
        Console.WriteLine($"  summary: {summaryFile}");
        Console.WriteLine($"  session: {sessionFile}");
        if (t.Outstanding > 0) Console.WriteLine("  --resume carries on where this stopped; --rerun-failed runs the unresolved ones again.");
        var waiting = s.Gated().ToList();
        if (waiting.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  {waiting.Count} check(s) are waiting on something else and were not offered. They are not");
            Console.WriteLine("  blocked and nothing has been recorded against them: settle what they wait for and they");
            Console.WriteLine("  become eligible by themselves.");
            foreach (var (c, reason) in waiting.Take(12)) Console.WriteLine($"     {c.Id,-4} {reason}");
            if (waiting.Count > 12) Console.WriteLine($"     ... and {waiting.Count - 12} more");
        }
        if (t.Failed + t.Partial > 0)
            Console.WriteLine("  Failures are investigation items. Nothing source-backed is tuned to make them pass.");
        Rule();
    }

    /// <summary>
    /// Applies runner-wide completion invariants before a check may interpret its own output. A completed
    /// conformance command that returned nonzero failed, regardless of any success-looking text it printed.
    /// </summary>
    public static AutoOutcome Evaluate(HardwareCheck check, HardwareToolRun run) =>
        run.InterruptedReason is not null ? AutoOutcome.NotRun
      : run.Error is not null ? AutoOutcome.Error
      : run.ExitCode != 0 ? AutoOutcome.Fail
      : check.Judge(run);

    private static string Describe(AutoOutcome o) => o switch
    {
        AutoOutcome.Pass => "PASS",
        AutoOutcome.Fail => "FAIL",
        AutoOutcome.Error => "ERROR (the tool did not complete)",
        AutoOutcome.Skipped => "not run",
        _ => "not run",
    };

    // ------------------------------------------------------------------ plumbing

    private static void Rule() => Console.WriteLine(new string('-', 78));

    /// <summary>Wraps a long field under the label column so a briefing stays readable in a terminal.</summary>
    private static string Wrap(string text, int width = 52, int indent = 19)
    {
        var words = text.Split(' ');
        var sb = new StringBuilder();
        int line = 0;
        foreach (var w in words)
        {
            if (line > 0 && line + w.Length + 1 > width) { sb.Append('\n').Append(new string(' ', indent)); line = 0; }
            else if (line > 0) { sb.Append(' '); line++; }
            sb.Append(w); line += w.Length;
        }
        return sb.ToString();
    }

    private static string? Arg(string[] a, string name)
    {
        for (int i = 1; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }

    private static string AcceptanceRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "acceptance");
            if (Directory.Exists(Path.Combine(d.FullName, "re-analysis"))) { Directory.CreateDirectory(r); return r; }
            d = d.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    public const string Usage =
        "hardware-test <robot-ip> [--obb <dir>] [--resume] [--from <ID>] [--only <ID[,ID]>] [--rerun <ID[,ID]>]\n" +
        "              [--rerun-failed] [--status] [--export] [--nominal] [--session <file>] [--out <dir>]\n" +
        "hardware-test --list";

    /// <summary>Writes to the console and to the check's log at once, keeping a copy for the judge.</summary>
    private sealed class TeeWriter : TextWriter
    {
        private readonly TextWriter _console, _file;
        private readonly StringBuilder _capture;
        public TeeWriter(TextWriter console, TextWriter file, StringBuilder capture) { _console = console; _file = file; _capture = capture; }
        public override Encoding Encoding => _console.Encoding;
        public override void Write(char value) { _console.Write(value); _file.Write(value); _capture.Append(value); }
        public override void Write(string? value) { _console.Write(value); _file.Write(value); _capture.Append(value); }
        public override void WriteLine(string? value) { _console.WriteLine(value); _file.WriteLine(value); _capture.Append(value).Append('\n'); }
        public override void Flush() { _console.Flush(); _file.Flush(); }
    }
}
