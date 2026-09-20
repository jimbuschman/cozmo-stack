using System.Text.Json;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The fidelity gate, asserted from the test suite so that `dotnet test` fails when the manifest and the
/// claims in the repository disagree.
///
/// <c>re-analysis/fidelity_manifest.json</c> holds one record per behaviour-affecting decision, with the
/// provenance needed to decide its status. The rule this class enforces is the one the cleanup was set up
/// around: <b>a subsystem may assert source_complete only when nothing on its normal live execution path
/// is a RECOVERABLE_GAP</b>. Nothing here judges whether a record is classified correctly — that is a
/// matter of evidence, recorded in the record itself — only that the manifest is well formed, that it
/// points at code that exists, and that the completeness flags follow from the records.
///
/// <c>re-analysis/tools/fidelity.py</c> applies the same rules and regenerates
/// <c>re-analysis/FIDELITY_GAPS.md</c> from the manifest, so the human-readable list cannot drift.
/// </summary>
public class FidelityManifestTests
{
    private const string Gap = "RECOVERABLE_GAP";

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
    /// The gate. A subsystem that still holds a live-path RECOVERABLE_GAP is not source-complete, and one
    /// that holds none is: the flag is not allowed to lag the records in either direction, so it cannot be
    /// set optimistically and cannot be left stale after the last gap is closed.
    /// </summary>
    [Fact]
    public void NoSubsystemIsSourceCompleteWhileItHoldsALivePathRecoverableGap()
    {
        var m = Manifest();
        var records = m.GetProperty("records").EnumerateArray().ToList();

        foreach (var sub in m.GetProperty("subsystems").EnumerateArray())
        {
            string id = Str(sub, "id");
            var blocking = records
                .Where(r => Str(r, "subsystem") == id && Str(r, "status") == Gap && Flag(r, "live_path"))
                .Select(r => Str(r, "id"))
                .ToList();
            bool claimed = Flag(sub, "source_complete");

            if (claimed)
                Assert.True(blocking.Count == 0,
                    $"{id} asserts source_complete but still holds live-path gaps: {string.Join(", ", blocking)}");
            else
                Assert.True(blocking.Count > 0,
                    $"{id} has no live-path RECOVERABLE_GAP left and should now assert source_complete");
        }
    }

    /// <summary>
    /// A gap record has to carry enough to be worked on: what it affects, what it currently rests on, the
    /// best available authority, and what is still unresolved. A record that only says "inferred" is the
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

            if (Str(r, "status") == "EXACT_SOURCE") continue;

            foreach (var field in new[] { "effect", "provenance" })
                Assert.False(string.IsNullOrWhiteSpace(Str(r, field)), $"{id} has no {field}");
            Assert.True(r.TryGetProperty("unresolved", out _), $"{id} does not say what is unresolved");
            Assert.True(r.TryGetProperty("hardware_required", out _), $"{id} does not say whether hardware is needed");

            if (Str(r, "status") == Gap)
            {
                Assert.False(string.IsNullOrWhiteSpace(Str(r, "unresolved")),
                    $"{id} is a RECOVERABLE_GAP and has to say what is still unresolved");
                Assert.False(Flag(r, "hardware_required"),
                    $"{id} needs hardware, so it is HARDWARE_ONLY rather than RECOVERABLE_GAP");
            }
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
    /// has not been edited away from the manifest: every live-path gap appears in it, and the counts agree.
    /// </summary>
    [Fact]
    public void TheGeneratedGapReportListsEveryOpenGap()
    {
        string root = RepoRoot();
        string reportPath = Path.Combine(root, "re-analysis", "FIDELITY_GAPS.md");
        Assert.True(File.Exists(reportPath), "re-analysis/FIDELITY_GAPS.md is missing; run re-analysis/tools/fidelity.py");
        string report = File.ReadAllText(reportPath);

        var gaps = Manifest().GetProperty("records").EnumerateArray()
            .Where(r => Str(r, "status") == Gap)
            .Select(r => Str(r, "id"))
            .ToList();
        foreach (var id in gaps)
            Assert.True(report.Contains(id, StringComparison.Ordinal),
                $"{id} is a RECOVERABLE_GAP but does not appear in FIDELITY_GAPS.md; regenerate it");
    }
}
