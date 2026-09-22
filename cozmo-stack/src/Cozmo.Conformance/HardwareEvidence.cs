using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cozmo.Conformance;

/// <summary>
/// What one check left behind: where its files are, the lines out of its output that explain the result, and
/// the verdict recorded against them.
/// </summary>
public sealed record EvidenceRecord
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Phase { get; init; }
    public required string Subsystem { get; init; }
    public required string Purpose { get; init; }
    public string? ExpectedTelemetry { get; init; }
    public required string ExpectedPhysical { get; init; }
    public IReadOnlyList<string> Prerequisites { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FidelityRecords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CoreRegressions { get; init; } = Array.Empty<string>();

    public required string CommandLine { get; init; }
    public DateTime? StartedUtc { get; init; }
    public DateTime? FinishedUtc { get; init; }
    public double? ElapsedSeconds => StartedUtc is { } a && FinishedUtc is { } b ? (b - a).TotalSeconds : null;

    public AutoOutcome Auto { get; init; }
    public HumanOutcome Human { get; init; }
    public required string Status { get; init; }
    public string? AutoDetail { get; init; }
    public string? HumanNote { get; init; }
    public string? InterruptedReason { get; init; }
    public string? BlockedReason { get; init; }
    public int Attempts { get; init; }

    /// <summary>The lines the check said were worth keeping, under the labels it gave them.</summary>
    public IReadOnlyDictionary<string, string[]> Captured { get; init; } = new Dictionary<string, string[]>();
    /// <summary>Anything the output called a warning, an exception or a refusal.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    /// <summary>The files in this check's directory, relative to it.</summary>
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
}

/// <summary>
/// The evidence side of the campaign: one directory per check, a machine-readable result file for the run,
/// and a summary a person can read.
///
/// The rule the whole thing is built around is that evidence is collected whatever the verdict. A check that
/// fails is worth nothing to anybody unless what it did is still there to look at afterwards, and a check
/// that passes is worth re-reading when a later one contradicts it. What is deliberately not done is
/// dumping everything: each check names the lines that explain its own result, and those are lifted into the
/// record while the whole transcript sits next to them in the log.
/// </summary>
public static class HardwareEvidence
{
    public const string ResultsFile = "results.json";
    public const string SummaryFile = "SUMMARY.md";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The directory a check's evidence goes in, created on demand.</summary>
    public static string Directory(string runDirectory, string id)
    {
        var d = Path.Combine(runDirectory, "tests", id);
        System.IO.Directory.CreateDirectory(d);
        return d;
    }

    /// <summary>
    /// Pulls the lines a check asked for out of its output. Each item keeps at most its own limit, so a
    /// per-frame line cannot bury the record; the count is kept when lines are dropped, because "300 of
    /// these" is itself evidence.
    /// </summary>
    public static Dictionary<string, string[]> Capture(HardwareCheck check, string output)
    {
        var found = new Dictionary<string, string[]>();
        if (check.Evidence.Count == 0) return found;
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r', ' ')).ToArray();
        foreach (var item in check.Evidence)
        {
            var hits = lines.Where(l => l.Contains(item.Match, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (hits.Length == 0) continue;
            found[item.Label] = hits.Length <= item.Limit
                ? hits
                : hits.Take(item.Limit).Append($"... {hits.Length - item.Limit} more line(s) matching '{item.Match}' in the log").ToArray();
        }
        return found;
    }

    /// <summary>
    /// Anything that looks like trouble, whatever the check asked for. A warning nobody thought to name in
    /// advance is exactly the thing worth having when a result is argued about later.
    /// </summary>
    public static string[] Warnings(string output) =>
        output.Split('\n')
              .Select(l => l.Trim())
              .Where(l => l.Length > 0 && (l.Contains("warning", StringComparison.OrdinalIgnoreCase)
                                        || l.Contains("exception", StringComparison.OrdinalIgnoreCase)
                                        || l.Contains("refusing", StringComparison.OrdinalIgnoreCase)
                                        || l.Contains("UNSAFE", StringComparison.Ordinal)
                                        || l.Contains("NOT READ", StringComparison.Ordinal)
                                        || l.Contains("could not", StringComparison.OrdinalIgnoreCase)))
              .Distinct()
              .Take(40)
              .ToArray();

    /// <summary>Writes one check's record beside its files and returns what was written.</summary>
    public static EvidenceRecord WriteRecord(HardwareCheck check, HardwareResult result, HardwareToolRun? run,
                                             HardwareRunOptions options, string directory)
    {
        var files = System.IO.Directory.Exists(directory)
            ? System.IO.Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                       .Select(f => Path.GetRelativePath(directory, f))
                       .Where(f => f != "record.json")
                       .OrderBy(f => f).ToArray()
            : Array.Empty<string>();

        var record = new EvidenceRecord
        {
            Id = check.Id,
            Name = check.Name,
            Phase = check.Phase,
            Subsystem = check.Subsystem,
            Purpose = check.Why,
            ExpectedTelemetry = check.ExpectedTelemetry ?? check.AutoRule,
            ExpectedPhysical = check.Success,
            Prerequisites = check.Prerequisites,
            FidelityRecords = check.FidelityRecords,
            CoreRegressions = check.CoreRegressions,
            CommandLine = check.CommandLine(options),
            StartedUtc = result.StartedUtc,
            FinishedUtc = result.FinishedUtc,
            Auto = result.Auto,
            Human = result.Human,
            Status = result.Status.ToString(),
            AutoDetail = result.AutoDetail,
            HumanNote = result.HumanNote,
            InterruptedReason = result.InterruptedReason,
            BlockedReason = result.BlockedReason,
            Attempts = result.Attempts,
            Captured = run is null ? new Dictionary<string, string[]>() : Capture(check, run.Output),
            Warnings = run is null ? Array.Empty<string>() : Warnings(run.Output),
            Files = files,
        };
        System.IO.Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "record.json"), JsonSerializer.Serialize(record, Json));
        return record;
    }

    /// <summary>The run as a whole, for a machine.</summary>
    public sealed record RunResults(string Robot, string? Obb, DateTime StartedUtc, DateTime? FinishedUtc,
                                    string EvidenceDirectory, HardwareSession.Summary Tally,
                                    EvidenceRecord[] Tests, string[] NotRun);

    /// <summary>
    /// Writes <c>results.json</c> and <c>SUMMARY.md</c> for the run. Called at the end and whenever the
    /// person asks for an export, so an interrupted campaign still has both.
    /// </summary>
    public static (string Results, string Summary) WriteRun(HardwareSession session, HardwareRunOptions options)
    {
        var dir = options.EvidenceDirectory;
        System.IO.Directory.CreateDirectory(dir);

        var records = new List<EvidenceRecord>();
        var notRun = new List<string>();
        foreach (var c in session.Selected())
        {
            var testDir = Path.Combine(dir, "tests", c.Id);
            var file = Path.Combine(testDir, "record.json");
            if (File.Exists(file))
            {
                var loaded = JsonSerializer.Deserialize<EvidenceRecord>(File.ReadAllText(file), Json);
                if (loaded is not null) { records.Add(loaded); continue; }
            }
            if (session.Results.TryGetValue(c.Id, out var r))
                records.Add(WriteRecord(c, r, null, options, testDir));
            else
                notRun.Add(c.Id);
        }

        var results = new RunResults(session.Ip, session.Obb, session.StartedUtc, session.FinishedUtc,
                                     dir, session.Tally(), records.ToArray(), notRun.ToArray());
        var resultsPath = Path.Combine(dir, ResultsFile);
        File.WriteAllText(resultsPath, JsonSerializer.Serialize(results, Json));

        var summaryPath = Path.Combine(dir, SummaryFile);
        File.WriteAllText(summaryPath, Summarise(session, results));
        return (resultsPath, summaryPath);
    }

    private static string Summarise(HardwareSession session, RunResults results)
    {
        var t = results.Tally;
        var sb = new StringBuilder();
        sb.AppendLine("# Cozmo hardware acceptance");
        sb.AppendLine();
        sb.AppendLine($"* robot: `{results.Robot}`");
        sb.AppendLine($"* started: {results.StartedUtc:u}" + (results.FinishedUtc is { } f ? $", finished {f:u}" : ", not finished"));
        sb.AppendLine($"* evidence: `{results.EvidenceDirectory}`");
        sb.AppendLine();
        sb.AppendLine($"**{t.Passed} passed, {t.Failed} failed, {t.Partial} inconclusive, {t.Unsure} unsure, "
                    + $"{t.Interrupted} interrupted, {t.Blocked} blocked, {t.Skipped} skipped, {t.Pending} not run** "
                    + $"of {t.Total}.");
        sb.AppendLine();
        sb.AppendLine("Hardware acceptance records what was observed. It does not change any fidelity record's");
        sb.AppendLine("status: a check that passes does not upgrade provenance, and one that fails is an");
        sb.AppendLine("investigation item, not a licence to tune a source-backed constant.");
        sb.AppendLine();

        string? phase = null;
        sb.AppendLine("| id | phase | check | status | detail |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var c in session.Selected())
        {
            var rec = results.Tests.FirstOrDefault(r => r.Id == c.Id);
            var status = session.StatusOf(c.Id);
            string detail = rec?.BlockedReason ?? rec?.InterruptedReason ?? rec?.HumanNote ?? rec?.AutoDetail ?? "";
            if (detail.Length > 110) detail = detail[..110] + "...";
            sb.AppendLine($"| {c.Id} | {c.Phase} | {c.Name} | {status.ToString().ToUpperInvariant()} | {detail.Replace('|', '/')} |");
            phase = c.Phase;
        }
        _ = phase;

        var failed = results.Tests.Where(r => r.Status is "Failed" or "Partial").ToList();
        if (failed.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Investigation items");
            sb.AppendLine();
            foreach (var r in failed)
            {
                sb.AppendLine($"### {r.Id} — {r.Name}");
                sb.AppendLine();
                sb.AppendLine($"* what it tests: {r.Subsystem}");
                sb.AppendLine($"* expected in the room: {r.ExpectedPhysical}");
                if (r.HumanNote is not null) sb.AppendLine($"* what was seen: {r.HumanNote}");
                if (r.AutoDetail is not null) sb.AppendLine($"* the tool said: {r.AutoDetail}");
                if (r.FidelityRecords.Count > 0) sb.AppendLine($"* related fidelity records (unchanged by this result): {string.Join(", ", r.FidelityRecords)}");
                if (r.CoreRegressions.Count > 0) sb.AppendLine($"* core-review corrections exercised: {string.Join(", ", r.CoreRegressions)}");
                sb.AppendLine($"* evidence: `tests/{r.Id}/`");
                sb.AppendLine();
            }
        }

        var blocked = results.Tests.Where(r => r.Status == "Blocked").ToList();
        if (blocked.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Blocked");
            sb.AppendLine();
            foreach (var r in blocked) sb.AppendLine($"* **{r.Id}** {r.Name} — {r.BlockedReason}");
        }
        return sb.ToString();
    }
}
