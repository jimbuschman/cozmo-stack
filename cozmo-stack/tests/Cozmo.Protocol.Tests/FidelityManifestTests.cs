using System.Text.Json;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The fidelity gates, asserted from the test suite so that `dotnet test` fails when the manifest and the
/// claims in the repository disagree.
///
/// <c>re-analysis/fidelity_manifest.json</c> holds one record per behaviour-affecting decision, with the
/// provenance needed to decide its status. There are two gates, and neither may be answered by the other:
///
/// <list type="bullet">
/// <item>a subsystem has <b>exhausted its source investigation</b> only when nothing on its normal live
/// execution path is a RECOVERABLE_GAP — nothing is left that reading the original would settle;</item>
/// <item>it has <b>complete implementation fidelity</b> only when nothing there is an IMPLEMENTATION_GAP —
/// nothing the original is known to do is knowingly not done here.</item>
/// </list>
///
/// Neither says the behaviour is faithfully reproduced. BLOCKED_EXTERNAL and HARDWARE_ONLY records can
/// remain in both cases, which is why they are counted separately rather than folded into one word.
/// Nothing here judges whether a record is classified correctly — that is a matter of evidence, recorded in
/// the record itself — only that the manifest is well formed, that it points at code that exists, and that
/// the two flags follow from the records.
///
/// <c>re-analysis/tools/fidelity.py</c> applies the same rules and regenerates
/// <c>re-analysis/FIDELITY_GAPS.md</c> from the manifest, so the human-readable list cannot drift.
/// </summary>
public class FidelityManifestTests
{
    private const string Recoverable = "RECOVERABLE_GAP";
    private const string Implementation = "IMPLEMENTATION_GAP";

    /// <summary>The two gates: the flag, the status that blocks it, and what the outstanding work is.</summary>
    public static TheoryData<string, string, string> Gates() => new()
    {
        { "source_investigation_exhausted", Recoverable, "reading the original" },
        { "implementation_fidelity_complete", Implementation, "building what the original does" },
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "re-analysis", "fidelity_manifest.json"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("re-analysis/fidelity_manifest.json was not found above " + AppContext.BaseDirectory);
    }

    private static JsonElement Manifest()
    {
        var path = Path.Combine(RepoRoot(), "re-analysis", "fidelity_manifest.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    private static bool Flag(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Each gate, checked in both directions. A subsystem that still holds a live-path record of the
    /// blocking status may not assert the flag, and one that holds none must: the flag is not allowed to lag
    /// the records either way, so it cannot be set optimistically and cannot be left stale after the last
    /// one is closed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Gates))]
    public void ASubsystemAssertsAGateExactlyWhenItsRecordsSayIt(string flag, string blockingStatus, string work)
    {
        var m = Manifest();
        var records = m.GetProperty("records").EnumerateArray().ToList();

        foreach (var sub in m.GetProperty("subsystems").EnumerateArray())
        {
            string id = Str(sub, "id");
            Assert.True(sub.TryGetProperty(flag, out _), $"{id} does not say whether {flag}");
            var blocking = records
                .Where(r => Str(r, "subsystem") == id && Str(r, "status") == blockingStatus && Flag(r, "live_path"))
                .Select(r => Str(r, "id"))
                .ToList();

            if (Flag(sub, flag))
                Assert.True(blocking.Count == 0,
                    $"{id} asserts {flag} but still holds live-path {blockingStatus}: {string.Join(", ", blocking)}");
            else
                Assert.True(blocking.Count > 0,
                    $"{id} has no live-path {blockingStatus} left, so {flag} should now be true — that flag " +
                    $"tracks outstanding {work}, and nothing else");
        }
    }

    /// <summary>
    /// The two gates stay two. A subsystem is allowed to have read everything and still not have built it,
    /// so neither flag may stand in for the other: this checks the manifest keeps the distinction visible
    /// rather than collapsing back into the single word it used to carry.
    /// </summary>
    [Fact]
    public void NothingInTheManifestClaimsASubsystemIsSimplyComplete()
    {
        var m = Manifest();
        foreach (var sub in m.GetProperty("subsystems").EnumerateArray())
            Assert.False(sub.TryGetProperty("source_complete", out _),
                $"{Str(sub, "id")} still carries the old source_complete flag, which said neither of the two things");

        var statuses = m.GetProperty("statuses").EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Contains(Implementation, statuses);
        Assert.Contains(Recoverable, statuses);

        // and the gate text has to keep them apart, in the manifest itself, for anyone reading it directly
        string rule = Str(m.GetProperty("gate"), "rule");
        Assert.Contains("source investigation", rule, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("implementation fidelity", rule, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A gap record has to carry enough to be worked on: what it affects, what it currently rests on, the
    /// best available authority, and what is still outstanding. A record that only says "inferred" is the
    /// thing this manifest exists to stop.
    /// </summary>
    [Fact]
    public void EveryNonExactRecordSaysWhatItRestsOnAndWhatIsUnresolved()
    {
        var m = Manifest();
        var statuses = m.GetProperty("statuses").EnumerateObject().Select(p => p.Name).ToHashSet();
        var subsystems = m.GetProperty("subsystems").EnumerateArray().Select(s => Str(s, "id")).ToHashSet();
        var seen = new HashSet<string>();

        foreach (var r in m.GetProperty("records").EnumerateArray())
        {
            string id = Str(r, "id");
            Assert.True(seen.Add(id), $"duplicate record id {id}");
            Assert.Contains(Str(r, "status"), statuses);
            Assert.Contains(Str(r, "subsystem"), subsystems);
            Assert.False(string.IsNullOrWhiteSpace(Str(r, "title")), $"{id} has no title");
            Assert.False(string.IsNullOrWhiteSpace(Str(r, "location")), $"{id} has no location");
            Assert.False(string.IsNullOrWhiteSpace(Str(r, "authority")), $"{id} names no authority");

            string status = Str(r, "status");
            if (status == "EXACT_SOURCE") continue;

            foreach (var field in new[] { "effect", "provenance" })
                Assert.False(string.IsNullOrWhiteSpace(Str(r, field)), $"{id} has no {field}");
            Assert.True(r.TryGetProperty("unresolved", out _), $"{id} does not say what is unresolved");
            Assert.True(r.TryGetProperty("hardware_required", out _), $"{id} does not say whether hardware is needed");

            if (status is Recoverable or Implementation)
                Assert.False(string.IsNullOrWhiteSpace(Str(r, "unresolved")),
                    $"{id} is a {status} and has to say what work is still outstanding");

            if (status == Recoverable)
                Assert.False(Flag(r, "hardware_required"),
                    $"{id} needs hardware, so it is HARDWARE_ONLY rather than RECOVERABLE_GAP");

            // An IMPLEMENTATION_GAP is work to do in this repository. If a robot were needed to settle it,
            // the native behaviour would not be established, and it would not be an IMPLEMENTATION_GAP.
            if (status == Implementation)
                Assert.False(Flag(r, "hardware_required"),
                    $"{id} is an IMPLEMENTATION_GAP, so the behaviour is already known and hardware cannot be what it waits on");
        }
    }

    /// <summary>Every record points at a file that is really there, so the manifest cannot outlive the code it describes.</summary>
    [Fact]
    public void EveryRecordPointsAtAFileThatExists()
    {
        string root = RepoRoot();
        foreach (var r in Manifest().GetProperty("records").EnumerateArray())
        {
            string location = Str(r, "location");
            string path = location.Split(':')[0];
            Assert.True(File.Exists(Path.Combine(root, path)) || Directory.Exists(Path.Combine(root, path)),
                $"{Str(r, "id")} points at {path}, which does not exist");
        }
    }

    /// <summary>
    /// The generated gap report is the manifest rendered, not a second hand-maintained list. This checks it
    /// has not been edited away from the manifest: every open record of either kind appears in it, and it
    /// no longer speaks the single word the two gates replaced.
    /// </summary>
    [Fact]
    public void TheGeneratedGapReportListsEveryOpenGap()
    {
        string root = RepoRoot();
        string reportPath = Path.Combine(root, "re-analysis", "FIDELITY_GAPS.md");
        Assert.True(File.Exists(reportPath), "re-analysis/FIDELITY_GAPS.md is missing; run re-analysis/tools/fidelity.py");
        string report = File.ReadAllText(reportPath);

        foreach (var r in Manifest().GetProperty("records").EnumerateArray())
        {
            string status = Str(r, "status");
            if (status is not (Recoverable or Implementation)) continue;
            Assert.True(report.Contains(Str(r, "id"), StringComparison.Ordinal),
                $"{Str(r, "id")} is a {status} but does not appear in FIDELITY_GAPS.md; regenerate it");
        }

        Assert.Contains(Implementation, report, StringComparison.Ordinal);
        Assert.DoesNotContain("source_complete", report, StringComparison.Ordinal);
    }
}
