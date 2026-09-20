using System.Net;
using System.Text;
using Cozmo.Robot;

namespace Cozmo.Conformance;

/// <summary>
/// The interactive hardware acceptance runner: one command that walks a person standing next to the robot
/// through every check in <c>HARDWARE_TEST_PLAN.md</c>, runs the existing conformance tool for each, collects
/// its evidence, and asks for a human verdict that is never inferred from the tool's own result.
///
/// <code>
/// hardware-test 172.31.1.1 --obb &lt;dir&gt;          run (or resume) the whole plan
/// hardware-test 172.31.1.1 --obb &lt;dir&gt; --resume continue after a disconnect or a reboot
/// hardware-test 172.31.1.1 --only N,O            just those checks, for debugging
/// hardware-test 172.31.1.1 --from Q              skip everything before Q
/// </code>
/// </summary>
public static class HardwareRunner
{
    public const string DefaultSessionFile = "hardware-session.json";

    public static async Task<int> Run(string[] a)
    {
        if (a.Length < 2 || a[1].StartsWith("--")) { Console.WriteLine(Usage); return 1; }
        string ip = a[1];
        string? obb = Arg(a, "--obb");
        bool resume = a.Contains("--resume");
        bool nominal = a.Contains("--nominal");
        string? from = Arg(a, "--from");
        var only = Arg(a, "--only")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string evidence = Arg(a, "--out") ?? Path.Combine(AcceptanceRoot(), $"hardware-{DateTime.Now:yyyyMMdd-HHmmss}");
        string sessionFile = Arg(a, "--session") ?? Path.Combine(AcceptanceRoot(), DefaultSessionFile);

        HardwareSession session;
        if (resume && HardwareSession.Load(sessionFile) is { } loaded)
        {
            session = loaded;
            var done = session.Results.Count;
            Console.WriteLine($"resuming the session in {sessionFile}: {done} check(s) already recorded");
        }
        else
        {
            if (resume) Console.WriteLine($"no session to resume at {sessionFile}; starting a new one");
            session = new HardwareSession(HardwareCatalog.All, ip, obb);
        }
        if (from is not null) session.From = from;
        if (only is { Length: > 0 }) session.Only = only;

        Directory.CreateDirectory(evidence);
        var options = new HardwareRunOptions { Ip = ip, Obb = obb, EvidenceDirectory = evidence, AllowNominalCalibration = nominal };

        using var quit = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            e.Cancel = true;                       // stop this check, not the process: the session must be saved
            if (!quit.IsCancellationRequested) quit.Cancel();
            Console.WriteLine("\n[Ctrl+C] stopping after this check; the robot's motors will be stopped.");
        };
        Console.CancelKeyPress += onCancel;

        try
        {
            Banner(session, options, sessionFile);
            while (session.Next() is { } check)
            {
                if (quit.IsCancellationRequested) break;

                if (session.BlockedBy(check) is { } block)
                {
                    Console.WriteLine();
                    Rule();
                    Console.WriteLine($"CHECK {check.Id} — {check.Name}   BLOCKED");
                    Console.WriteLine($"  {block.Reason}");
                    Console.WriteLine("  Not started. Nothing after it that depends on it will start either.");
                    session.RecordBlocked(check.Id, block.Reason);
                    session.Save(sessionFile);
                    continue;
                }

                var choice = Brief(check, options, session);
                if (choice == 'q') { quit.Cancel(); break; }
                if (choice == 's')
                {
                    session.Record(new HardwareResult { Id = check.Id, Auto = AutoOutcome.Skipped, Human = HumanOutcome.Skipped, FinishedUtc = DateTime.UtcNow });
                    session.Save(sessionFile);
                    continue;
                }

                bool again = true;
                while (again)
                {
                    again = false;
                    var started = DateTime.UtcNow;
                    var run = await Execute(check, options, quit.Token);
                    var auto = run.Error is not null ? AutoOutcome.Error : check.Judge(run);

                    Console.WriteLine();
                    Console.WriteLine($"  automated: {Describe(auto)}  ({check.AutoRule})");
                    if (run.Error is not null) Console.WriteLine($"  the tool threw: {run.Error.GetType().Name}: {run.Error.Message}");
                    if (run.AcceptanceRecord is not null) Console.WriteLine($"  evidence:  {run.AcceptanceRecord}");
                    Console.WriteLine($"  log:       {run.LogPath}");

                    if (check.MovesRobot) await StopMotors(ip);

                    if (quit.IsCancellationRequested)
                    {
                        Console.WriteLine("  interrupted before a verdict was given; the check stays pending.");
                        break;
                    }

                    var verdict = AskVerdict(check);
                    if (verdict == 'r') { Console.WriteLine("  retrying."); again = true; continue; }
                    if (verdict == 'q')
                    {
                        session.Record(Result(check, run, auto, HumanOutcome.NotAsked, started));
                        session.Save(sessionFile);
                        quit.Cancel();
                        break;
                    }
                    var human = verdict switch { 'y' => HumanOutcome.Pass, 'n' => HumanOutcome.Fail, _ => HumanOutcome.Skipped };
                    string? note = null;
                    if (human == HumanOutcome.Fail)
                    {
                        Console.Write("  what went wrong (one line, optional): ");
                        note = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(note)) note = null;
                    }
                    var result = session.Record(Result(check, run, auto, human, started) with { HumanNote = note });
                    session.Save(sessionFile);
                    Console.WriteLine($"  recorded:  {result.Status.ToString().ToUpperInvariant()}");
                }
            }

            if (session.Complete) session.Finish();
            session.Save(sessionFile);
            PrintSummary(session, sessionFile);
            var t = session.Tally();
            return t.Failed > 0 ? 1 : 0;
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    // ------------------------------------------------------------------ running one check

    /// <summary>
    /// Calls the conformance tool for a check in this process, teeing its output to the console and to a log
    /// file. The tools are the implementations the plan already names, so nothing here re-implements a check.
    /// </summary>
    private static async Task<HardwareToolRun> Execute(HardwareCheck check, HardwareRunOptions o, CancellationToken cancel)
    {
        var args = check.Command(o);
        string log = Path.Combine(o.EvidenceDirectory, $"{check.Id}.log");
        Console.WriteLine();
        Console.WriteLine($"  running: {string.Join(' ', args)}");
        Rule();

        var captured = new StringBuilder();
        var original = Console.Out;
        using var file = new StreamWriter(log, append: false) { AutoFlush = true };
        Console.SetOut(new TeeWriter(original, file, captured));
        int exit = -1;
        Exception? error = null;
        try { exit = await Invoke(args, cancel); }
        catch (OperationCanceledException) { error = null; }
        catch (Exception e) { error = e; }
        finally { Console.SetOut(original); }

        Rule();
        var text = captured.ToString();
        string? record = text.Split('\n')
            .FirstOrDefault(l => l.Contains("acceptance record:", StringComparison.OrdinalIgnoreCase))
            ?.Split("acceptance record:", StringSplitOptions.TrimEntries).ElementAtOrDefault(1)?.Trim();
        return new HardwareToolRun(exit, text, record, log, error);
    }

    /// <summary>
    /// The conformance commands the plan's checks use. This is a deliberate subset of the program's own
    /// dispatch: the runner calls these entry points directly rather than starting another process.
    /// </summary>
    private static async Task<int> Invoke(string[] args, CancellationToken cancel) => args[0] switch
    {
        "sensors" => await Control.Sensors(args),
        "cubes" => await Control.Cubes(args),
        "camera" => await Devices.Camera(args),
        "anim" => await Anim.Play(args),
        "behavior" => await BehaviorTool.Run(args),
        "sing" => await SingTool.Run(args),
        "offtreads" => await ReactionsTool.OffTreads(args),
        "reactions" => await ReactionsTool.Run(args),
        "vision" => await VisionTool.Run(args),
        "bodyangle" => await VisionTool.BodyAngle(args),
        "manip" => await ManipTool.Run(args),
        "freeplay" => await FreeplayTool.Run(args),
        _ => throw new InvalidOperationException($"the runner has no entry point for '{args[0]}'"),
    };

    /// <summary>
    /// Stops the motors after anything that moved the robot, so a cancelled or failed check never leaves him
    /// driving. The tools stop their own robot on the way out; this is the belt to that braces, on its own
    /// short-lived connection.
    /// </summary>
    private static async Task StopMotors(string ip)
    {
        try
        {
            using var robot = await CozmoRobot.ConnectAsync(IPAddress.Parse(ip), timeout: TimeSpan.FromSeconds(3));
            robot.EmergencyStop();
        }
        catch (Exception e)
        {
            Console.WriteLine($"  note: could not open a connection to confirm the motors are stopped ({e.GetType().Name}).");
        }
    }

    private static HardwareResult Result(HardwareCheck check, HardwareToolRun run, AutoOutcome auto, HumanOutcome human, DateTime started) =>
        new()
        {
            Id = check.Id,
            Auto = auto,
            Human = human,
            AcceptanceRecord = run.AcceptanceRecord,
            LogPath = run.LogPath,
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
        Console.WriteLine($"  checks:   {selected.Count} ({string.Join(" ", selected.Select(c => c.Id))})");
        Console.WriteLine($"  evidence: {o.EvidenceDirectory}");
        Console.WriteLine($"  session:  {sessionFile}  (saved after every check; --resume continues here)");
        if (o.Obb is null) Console.WriteLine("  note:     no --obb given; the checks that need the OBB assets will not run properly.");
        Console.WriteLine();
        Console.WriteLine("  At each check you get a briefing, then the tool runs, then you give the verdict.");
        Console.WriteLine("  Your verdict is what counts: the tool's own result never decides it for you.");
        Rule();
    }

    /// <summary>The briefing, and the choice to start, skip or quit.</summary>
    private static char Brief(HardwareCheck c, HardwareRunOptions o, HardwareSession s)
    {
        Console.WriteLine();
        Rule();
        Console.WriteLine($"CHECK {c.Id} — {c.Name}   [{c.Milestone}]");
        Console.WriteLine();
        Console.WriteLine($"  What this tests: {Wrap(c.Subsystem)}");
        Console.WriteLine($"  Why it matters:  {Wrap(c.Why)}");
        Console.WriteLine();
        Console.WriteLine($"  Set up:          {Wrap(c.Setup)}");
        Console.WriteLine($"  You do:          {Wrap(c.DoThis)}");
        Console.WriteLine($"  A pass looks like: {Wrap(c.Success, indent: 21)}");
        if (c.KnownIssue is not null)
        {
            Console.WriteLine();
            Console.WriteLine($"  KNOWN ISSUE:     {Wrap(c.KnownIssue)}");
        }
        if (s.UnprovenPrerequisites.TryGetValue(c.Id, out var unproven))
        {
            Console.WriteLine();
            Console.WriteLine($"  NOTE: {string.Join(", ", unproven)} normally runs first and has not been established "
                            + "in this session. You asked for this check by name, so it will run anyway.");
        }
        if (c.MovesRobot)
        {
            Console.WriteLine();
            Console.WriteLine("  *** HE WILL MOVE UNDER HIS OWN POWER. Clear the area and keep a hand ready. ***");
        }
        Console.WriteLine();
        var done = s.Tally();
        Console.WriteLine($"  progress: {done.Passed} passed, {done.Failed} failed, {done.Partial} inconclusive, {done.Blocked} blocked, {done.Skipped} skipped");
        Console.Write(c.MovesRobot
            ? "  [Enter] start (he will move)   [S] skip   [Q] stop and save: "
            : "  [Enter] start   [S] skip   [Q] stop and save: ");
        var line = Console.ReadLine();
        return (line ?? "q").Trim().ToLowerInvariant() switch
        {
            "s" => 's',
            "q" => 'q',
            _ => '\n',
        };
    }

    /// <summary>The human verdict. Never derived from the automated outcome.</summary>
    private static char AskVerdict(HardwareCheck c)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine($"  {c.Question}");
            Console.Write("  [y] pass   [n] fail   [r] retry   [s] skip   [q] stop and save: ");
            var line = (Console.ReadLine() ?? "q").Trim().ToLowerInvariant();
            if (line is "y" or "n" or "r" or "s" or "q") return line[0];
            Console.WriteLine("  answer y, n, r, s or q.");
        }
    }

    private static void PrintSummary(HardwareSession s, string sessionFile)
    {
        var t = s.Tally();
        Console.WriteLine();
        Rule();
        Console.WriteLine("SUMMARY");
        foreach (var c in s.Selected())
        {
            var status = s.StatusOf(c.Id);
            s.Results.TryGetValue(c.Id, out var r);
            string detail = status switch
            {
                CheckStatus.Blocked => r?.BlockedReason ?? "blocked",
                CheckStatus.Partial => $"tool said {Describe(r!.Auto)}, you said {r.Human}",
                CheckStatus.Failed => r?.HumanNote ?? "failed",
                _ => "",
            };
            Console.WriteLine($"  {c.Id,-3} {status.ToString().ToUpperInvariant(),-8} {c.Name}{(detail.Length > 0 ? "  — " + detail : "")}");
        }
        Console.WriteLine();
        Console.WriteLine($"  {t.Passed} passed, {t.Failed} failed, {t.Partial} inconclusive, {t.Blocked} blocked, {t.Skipped} skipped, {t.Pending} not run");
        Console.WriteLine($"  session: {sessionFile}");
        if (t.Pending > 0) Console.WriteLine("  resume with --resume to carry on where this stopped.");
        Rule();
    }

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
    private static string Wrap(string text, int width = 58, int indent = 19)
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
        "hardware-test <robot-ip> [--obb <dir>] [--resume] [--from <ID>] [--only <ID[,ID]>] [--nominal] [--session <file>] [--out <dir>]";

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
