using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cozmo.Conformance;

/// <summary>Why the runner will not start a check.</summary>
public sealed record HardwareBlock(string Id, string Reason);

/// <summary>
/// The run's state: which checks are done, what each concluded, and what may run next. Deliberately free of
/// console and robot: the ordering, the dependency gating, the verdict arithmetic and the resume file are the
/// parts that must keep working after a disconnect or a reboot, so they are testable on their own.
/// </summary>
public sealed class HardwareSession
{
    private readonly Dictionary<string, HardwareResult> _results = new(StringComparer.OrdinalIgnoreCase);

    public HardwareSession(IReadOnlyList<HardwareCheck> catalog, string ip, string? obb)
    {
        Catalog = catalog; Ip = ip; Obb = obb;
        StartedUtc = DateTime.UtcNow;
    }

    public IReadOnlyList<HardwareCheck> Catalog { get; }
    public string Ip { get; }
    public string? Obb { get; }
    public DateTime StartedUtc { get; private set; }
    public DateTime? FinishedUtc { get; private set; }
    public IReadOnlyDictionary<string, HardwareResult> Results => _results;

    /// <summary>Only these ids will run, when set by --only.</summary>
    public IReadOnlyCollection<string>? Only { get; set; }
    /// <summary>Skip everything before this id, when set by --from.</summary>
    public string? From { get; set; }

    /// <summary>
    /// True when --only or --from picked the checks by hand. A prerequisite that has simply not run is then a
    /// warning rather than a block, because the point of those flags is to run one check on its own. A
    /// prerequisite that actually failed still blocks: that is the case the runner must never walk past.
    /// </summary>
    public bool DebugSelection => Only is { Count: > 0 } || From is not null;

    // ------------------------------------------------------------------ verdict arithmetic

    /// <summary>
    /// The status of a check from its two halves. The automated half and the human half are kept apart on
    /// purpose: a check whose tooling is happy and whose observer is not is neither a pass nor a plain
    /// failure, it is the interesting case, and it stays visible as Partial.
    /// </summary>
    public static CheckStatus StatusOf(HardwareResult r)
    {
        if (r.BlockedReason is not null) return CheckStatus.Blocked;
        if (r.Human == HumanOutcome.Skipped) return CheckStatus.Skipped;
        if (r.Auto == AutoOutcome.Skipped && r.Human == HumanOutcome.NotAsked) return CheckStatus.Skipped;
        if (r.Human == HumanOutcome.NotAsked) return CheckStatus.Pending;
        return (r.Auto, r.Human) switch
        {
            (AutoOutcome.Pass, HumanOutcome.Pass) => CheckStatus.Passed,
            (AutoOutcome.Fail or AutoOutcome.Error, HumanOutcome.Fail) => CheckStatus.Failed,
            _ => CheckStatus.Partial,
        };
    }

    public CheckStatus StatusOf(string id) => _results.TryGetValue(id, out var r) ? StatusOf(r) : CheckStatus.Pending;

    /// <summary>A check counts as satisfied for a dependant when it passed outright.</summary>
    public bool Satisfied(string id) => StatusOf(id) == CheckStatus.Passed;

    // ------------------------------------------------------------------ what runs next

    /// <summary>The checks this run covers, in catalog order, after --only and --from.</summary>
    public IEnumerable<HardwareCheck> Selected()
    {
        bool started = From is null;
        foreach (var c in Catalog)
        {
            if (!started && string.Equals(c.Id, From, StringComparison.OrdinalIgnoreCase)) started = true;
            if (!started) continue;
            if (Only is { Count: > 0 } && !Only.Contains(c.Id, StringComparer.OrdinalIgnoreCase)) continue;
            yield return c;
        }
    }

    /// <summary>
    /// Why a check cannot start now, or null. A check the build cannot run at all is blocked by its own
    /// reason; one whose prerequisite has not passed is blocked by that, and the runner never proceeds past
    /// it silently.
    /// </summary>
    public HardwareBlock? BlockedBy(HardwareCheck c)
    {
        if (c.BlockedReason is null && DebugSelection)
        {
            // the caller asked for this check by name; say what has not been established rather than refusing
            var unproven = c.Requires.Where(n => StatusOf(n) == CheckStatus.Pending).ToList();
            if (unproven.Count > 0) UnprovenPrerequisites[c.Id] = unproven;
        }
        if (c.BlockedReason is not null) return new HardwareBlock(c.Id, c.BlockedReason);
        foreach (var need in c.Requires)
        {
            var status = StatusOf(need);
            if (status == CheckStatus.Passed) continue;
            if (status == CheckStatus.Pending && DebugSelection) continue;   // hand-picked: warn, do not block
            var name = HardwareCatalog.Find(need)?.Name ?? need;
            return new HardwareBlock(c.Id, status switch
            {
                CheckStatus.Failed => $"{need} ({name}) failed, and {c.Id} depends on it",
                CheckStatus.Partial => $"{need} ({name}) was inconclusive, and {c.Id} depends on it",
                CheckStatus.Skipped => $"{need} ({name}) was skipped, and {c.Id} depends on it",
                CheckStatus.Blocked => $"{need} ({name}) is blocked, and {c.Id} depends on it",
                _ => $"{need} ({name}) has not run yet, and {c.Id} depends on it",
            });
        }
        return null;
    }

    /// <summary>
    /// Prerequisites that have not run for a hand-picked check, filled in by <see cref="BlockedBy"/> so the
    /// runner can warn about them without refusing to start.
    /// </summary>
    public Dictionary<string, List<string>> UnprovenPrerequisites { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The next check with no recorded result, or null when the run is complete.</summary>
    public HardwareCheck? Next() => Selected().FirstOrDefault(c => !_results.ContainsKey(c.Id));

    /// <summary>True once every selected check has a result.</summary>
    public bool Complete => Next() is null;

    // ------------------------------------------------------------------ recording

    /// <summary>Records a result, counting this as one more attempt at the check.</summary>
    public HardwareResult Record(HardwareResult result)
    {
        _results.TryGetValue(result.Id, out var existing);
        var merged = result with { Attempts = (existing?.Attempts ?? 0) + 1 };
        _results[result.Id] = merged;
        return merged;
    }

    /// <summary>Records a check the runner refused to start.</summary>
    public HardwareResult RecordBlocked(string id, string reason) =>
        Record(new HardwareResult { Id = id, Auto = AutoOutcome.NotRun, Human = HumanOutcome.NotAsked, BlockedReason = reason, FinishedUtc = DateTime.UtcNow });

    /// <summary>Drops a result so the check runs again (the retry verdict).</summary>
    public void Clear(string id) => _results.Remove(id);

    public void Finish() => FinishedUtc = DateTime.UtcNow;

    // ------------------------------------------------------------------ the summary

    public sealed record Summary(int Passed, int Failed, int Partial, int Blocked, int Skipped, int Pending)
    {
        public int Total => Passed + Failed + Partial + Blocked + Skipped + Pending;
    }

    public Summary Tally()
    {
        int pass = 0, fail = 0, part = 0, block = 0, skip = 0, pend = 0;
        foreach (var c in Selected())
            switch (StatusOf(c.Id))
            {
                case CheckStatus.Passed: pass++; break;
                case CheckStatus.Failed: fail++; break;
                case CheckStatus.Partial: part++; break;
                case CheckStatus.Blocked: block++; break;
                case CheckStatus.Skipped: skip++; break;
                default: pend++; break;
            }
        return new Summary(pass, fail, part, block, skip, pend);
    }

    // ------------------------------------------------------------------ persistence

    private sealed record Persisted(string Ip, string? Obb, DateTime StartedUtc, DateTime? FinishedUtc,
                                    string? From, string[]? Only, HardwareResult[] Results);

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Writes the session. Called after every check, so a disconnect, a crash or a flat battery costs at most
    /// the check that was running. The write goes to a temporary file and is moved into place, so an
    /// interruption mid-write cannot leave an unreadable session behind.
    /// </summary>
    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var p = new Persisted(Ip, Obb, StartedUtc, FinishedUtc, From, Only?.ToArray(),
                              Catalog.Select(c => _results.TryGetValue(c.Id, out var r) ? r : null).Where(r => r is not null).ToArray()!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(p, Json));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Reads a session back, keeping every recorded verdict. Returns null when there is no file.</summary>
    public static HardwareSession? Load(string path, IReadOnlyList<HardwareCheck>? catalog = null)
    {
        if (!File.Exists(path)) return null;
        var p = JsonSerializer.Deserialize<Persisted>(File.ReadAllText(path), Json);
        if (p is null) return null;
        var s = new HardwareSession(catalog ?? HardwareCatalog.All, p.Ip, p.Obb)
        {
            StartedUtc = p.StartedUtc,
            FinishedUtc = p.FinishedUtc,
            From = p.From,
            Only = p.Only is { Length: > 0 } ? p.Only : null,
        };
        foreach (var r in p.Results) s._results[r.Id] = r;
        return s;
    }
}
