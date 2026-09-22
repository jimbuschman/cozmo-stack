using System.Text.Json.Serialization;

namespace Cozmo.Conformance;

/// <summary>What the automated part of a check concluded, on its own.</summary>
public enum AutoOutcome { NotRun, Pass, Fail, Error, Skipped }

/// <summary>
/// What the person standing next to the robot concluded. Never inferred from <see cref="AutoOutcome"/>.
/// <c>Unsure</c> is a first-class answer: a campaign that forces a yes or no out of an observer who could not
/// tell records something that was never observed.
/// </summary>
public enum HumanOutcome { NotAsked, Pass, Fail, Unsure, Skipped }

/// <summary>The status of a check once both halves are in.</summary>
public enum CheckStatus
{
    Pending,
    /// <summary>Both halves agree it worked.</summary>
    Passed,
    /// <summary>Both halves agree it did not.</summary>
    Failed,
    /// <summary>The halves disagree, or one could not be taken: the check needs a human to read the evidence.</summary>
    Partial,
    /// <summary>The observer could not tell. Evidence is kept; the check is neither passed nor failed.</summary>
    Unsure,
    /// <summary>
    /// The check did not finish: the link dropped, it timed out, or it was stopped part way. Not a failure -
    /// nothing was observed either way - and it stays to be run again.
    /// </summary>
    Interrupted,
    /// <summary>A prerequisite failed, or the check cannot run on this build at all.</summary>
    Blocked,
    Skipped,
}

/// <summary>
/// One thing worth keeping out of a check's output, and how to find it. The runner pulls the matching lines
/// into the check's evidence record rather than storing the whole transcript twice: what explains a result is
/// a handful of lines, and the full log sits beside them anyway.
/// </summary>
public sealed record EvidenceItem(string Label, string Match)
{
    /// <summary>At most this many matching lines are kept, so a per-frame line cannot fill the record.</summary>
    public int Limit { get; init; } = 40;
}

/// <summary>
/// One hardware acceptance test: everything a person standing next to the robot needs to carry it out, and
/// everything the runner needs to run it, judge its automated half and keep its evidence.
///
/// The command is the argument vector of an existing conformance tool. The runner calls that tool directly
/// rather than starting another process, so there is one implementation of each check and no duplicated
/// protocol logic - and adding a case is a new entry here, not a change to the runner.
/// </summary>
public sealed record HardwareCheck
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>The milestone this check accepts (M4, M9, ...), or the core-review finding it exercises.</summary>
    public required string Milestone { get; init; }
    /// <summary>
    /// The ordering group: connection and telemetry first, movement later, the long autonomy run last. Shown
    /// as a heading so a person can see where in the campaign they are.
    /// </summary>
    public required string Phase { get; init; }
    /// <summary>The part of the stack under test, in one line.</summary>
    public required string Subsystem { get; init; }
    /// <summary>Why the check exists: what would go unnoticed without it.</summary>
    public required string Why { get; init; }
    /// <summary>The physical arrangement needed before starting.</summary>
    public required string Setup { get; init; }
    /// <summary>What the person does while it runs.</summary>
    public required string DoThis { get; init; }
    /// <summary>What a pass looks or sounds like, in the room.</summary>
    public required string Success { get; init; }
    /// <summary>The question the runner asks afterwards.</summary>
    public required string Question { get; init; }

    /// <summary>What has to be true or to hand before this can start, in words.</summary>
    public IReadOnlyList<string> Prerequisites { get; init; } = Array.Empty<string>();
    /// <summary>What the software should report, as distinct from what the room should show.</summary>
    public string? ExpectedTelemetry { get; init; }
    /// <summary>How long the check is given before the runner stops it and records it interrupted.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(6);
    /// <summary>What the runner does afterwards, and anything the person has to undo.</summary>
    public string Cleanup { get; init; } = "The runner stops the motors and closes its own connection.";
    /// <summary>What to keep from the run so a failure can be investigated without repeating it.</summary>
    public IReadOnlyList<EvidenceItem> Evidence { get; init; } = Array.Empty<EvidenceItem>();
    /// <summary>The fidelity manifest records this check bears on. Hardware never changes their status.</summary>
    public IReadOnlyList<string> FidelityRecords { get; init; } = Array.Empty<string>();
    /// <summary>Core-review corrections this check exercises on the robot.</summary>
    public IReadOnlyList<string> CoreRegressions { get; init; } = Array.Empty<string>();

    /// <summary>A cube has to be present and connected.</summary>
    public bool NeedsCube { get; init; }
    /// <summary>The charger has to be placed for this check.</summary>
    public bool NeedsCharger { get; init; }
    /// <summary>The person has to pick the robot up, move it, or rearrange the scene while it runs.</summary>
    public bool NeedsHandling { get; init; }
    /// <summary>The OBB assets are required; without them the check cannot run.</summary>
    public bool NeedsObb { get; init; }

    /// <summary>False when the check cannot run on this build at all; <see cref="BlockedReason"/> says why.</summary>
    public bool Runnable => BlockedReason is null;

    /// <summary>Checks that must have passed first. A failed prerequisite blocks this one.</summary>
    public IReadOnlyList<string> Requires { get; init; } = Array.Empty<string>();
    /// <summary>Whether the robot drives or moves its lift or head under its own power.</summary>
    public bool MovesRobot { get; init; }
    /// <summary>
    /// The link is expected to go during this check, because the check is about what happens when it does.
    /// The runner then judges the result normally instead of recording an interruption.
    /// </summary>
    public bool ExpectsDisconnect { get; init; }
    /// <summary>Set when the check cannot run on this build at all; the check is recorded Blocked.</summary>
    public string? BlockedReason { get; init; }
    /// <summary>A known discrepancy to show in the brief, so a person is not surprised by it.</summary>
    public string? KnownIssue { get; init; }

    /// <summary>The conformance command line, without the leading program name.</summary>
    public required Func<HardwareRunOptions, string[]> Command { get; init; }
    /// <summary>
    /// Reads the tool's console output and decides the automated half. Substrings come from the plan's
    /// "automated verdict" column; the rule is stated in <see cref="AutoRule"/> for the brief.
    /// </summary>
    public required Func<HardwareToolRun, AutoOutcome> Judge { get; init; }
    /// <summary>The automated rule in words, shown before the check runs.</summary>
    public required string AutoRule { get; init; }

    public string CommandLine(HardwareRunOptions o) => string.Join(' ', Command(o));
}

/// <summary>
/// How a check's run ended. This, and not anything the tool printed, is what decides whether the check was
/// interrupted: every tool closes its own connection on the way out and says so, so the transcript cannot
/// tell a cleanup disconnect from a robot that went away.
/// </summary>
public enum RunEnding
{
    /// <summary>The tool ran to its own end and returned. Whatever it printed on the way out is its cleanup.</summary>
    Completed,
    /// <summary>The person stopped it - Ctrl+C, which is also the emergency stop.</summary>
    Cancelled,
    /// <summary>It never returned within the check's allowance.</summary>
    TimedOut,
    /// <summary>It threw. Whether that means the link went is decided by what it threw.</summary>
    Faulted,
}

/// <summary>What the runner captured from one invocation of a conformance tool.</summary>
public sealed record HardwareToolRun(int ExitCode, string Output, string? AcceptanceRecord, string LogPath, Exception? Error)
{
    /// <summary>How the run ended, which is what the interruption verdict is taken from.</summary>
    public RunEnding Ending { get; init; } = RunEnding.Completed;

    /// <summary>Set when the run did not finish: the link dropped, it timed out, or it was stopped.</summary>
    public string? InterruptedReason { get; init; }
    public bool Contains(string s) => Output.Contains(s, StringComparison.OrdinalIgnoreCase);
    public bool ContainsAll(params string[] all) => all.All(Contains);
    public bool ContainsAny(params string[] any) => any.Any(Contains);
    /// <summary>The first line matching a substring, for the summary.</summary>
    public string? Line(string s) =>
        Output.Split('\n').FirstOrDefault(l => l.Contains(s, StringComparison.OrdinalIgnoreCase))?.Trim();
}

/// <summary>Everything the runner needs to build a command line.</summary>
public sealed record HardwareRunOptions
{
    public required string Ip { get; init; }
    public string? Obb { get; init; }
    /// <summary>The run's directory: one subdirectory per check, plus the results file and the summary.</summary>
    public required string EvidenceDirectory { get; init; }
    /// <summary>Allows the nominal camera calibration stand-in where a tool offers it.</summary>
    public bool AllowNominalCalibration { get; init; }

    /// <summary>This check's own directory. Everything it produces goes here and nowhere else.</summary>
    public string TestDirectory(string id) => HardwareEvidence.Directory(EvidenceDirectory, id);
    /// <summary>Where a tool writes its acceptance record for this check.</summary>
    public string Acceptance(string id) => Path.Combine(TestDirectory(id), "acceptance.json");
    /// <summary>Where a tool writes images or frames for this check.</summary>
    public string Frames(string id) => Path.Combine(TestDirectory(id), "frames");
    /// <summary>Where a tool writes a raw protocol frame log for this check.</summary>
    public string FrameLog(string id) => Path.Combine(TestDirectory(id), "frames.log");

    public string[] WithObb(params string[] head) =>
        Obb is null ? head : head.Concat(new[] { "--obb", Obb }).ToArray();
}

/// <summary>One check's result, as persisted.</summary>
public sealed record HardwareResult
{
    public required string Id { get; init; }
    public AutoOutcome Auto { get; init; }
    public HumanOutcome Human { get; init; }
    /// <summary>Set when the check was blocked rather than run.</summary>
    public string? BlockedReason { get; init; }
    public string? AcceptanceRecord { get; init; }
    public string? LogPath { get; init; }
    public int Attempts { get; init; }
    public string? AutoDetail { get; init; }
    public string? HumanNote { get; init; }
    /// <summary>Set when the check did not finish: a dropped link, a timeout, or a stop part way.</summary>
    public string? InterruptedReason { get; init; }
    /// <summary>The check's own evidence directory.</summary>
    public string? EvidenceDirectory { get; init; }
    public DateTime? StartedUtc { get; init; }
    public DateTime? FinishedUtc { get; init; }

    [JsonIgnore]
    public CheckStatus Status => HardwareSession.StatusOf(this);
}
