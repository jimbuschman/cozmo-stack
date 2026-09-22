using System.Text.Json;
using System.Text.RegularExpressions;
using Cozmo.Conformance;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Structural validation of the fidelity ids each hardware check names.
///
/// <see cref="HardwareCheck.FidelityRecords"/> and <see cref="HardwareCheck.CoreRegressions"/> are validated as
/// separate fields: a fidelity record has to exist in <c>re-analysis/fidelity_manifest.json</c>; a core
/// regression is a CORE-nnn id that is not a manifest record and must not be looked up as one.
///
/// This is structural validation only. It DOES NOT prove that a mapping is semantically correct - that a
/// named record is the evidence for what a check exercises, or that a check's whole production path is
/// covered. That is a matter of reading the records against the check, and passing these tests says nothing
/// about it.
/// </summary>
public class HardwareCatalogFidelityTests
{
    private static readonly Regex ManifestId = new(@"^(M([1-9]|1[0-5])|TOOL)-\d{3}$");
    private static readonly Regex CoreId = new(@"^CORE-\d{3}$");

    /// <summary>The 40 checks of the campaign. A check added or removed has to be added or removed here too.</summary>
    private static readonly string[] AllCheckIds =
    {
        "LINK", "D", "F", "FD", "AUD", "A", "A2", "A3", "MOV", "CR1", "IDL", "CR3", "CR2", "E", "B", "K", "V", "W",
        "G", "C", "H", "I", "J", "M", "CR8", "CR7", "L", "Q", "CR4", "X", "N", "O", "P", "R", "S", "T", "U", "Z",
        "Z2", "Y",
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

    private static List<(string Id, string Status)> Records()
    {
        var root = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "re-analysis", "fidelity_manifest.json"))).RootElement;
        return root.GetProperty("records").EnumerateArray()
            .Select(r => (r.GetProperty("id").GetString()!, r.GetProperty("status").GetString()!)).ToList();
    }

    private static HashSet<string> Statuses()
    {
        var root = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "re-analysis", "fidelity_manifest.json"))).RootElement;
        return root.GetProperty("statuses").EnumerateObject().Select(p => p.Name).ToHashSet();
    }

    private static Dictionary<string, string> StatusById() => Records().ToDictionary(r => r.Id, r => r.Status);

    [Fact]
    public void EveryOneOfTheFortyChecksIsValidated()
    {
        var ids = HardwareCatalog.All.Select(c => c.Id).ToList();
        Assert.Equal(40, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(AllCheckIds.OrderBy(x => x), ids.OrderBy(x => x));
    }

    [Fact]
    public void ManifestIdsAreUniqueWellFormedAndCarryAKnownStatus()
    {
        var records = Records();
        var statuses = Statuses();
        var dupes = records.GroupBy(r => r.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupes.Count == 0, "duplicate manifest ids: " + string.Join(", ", dupes));
        foreach (var (id, status) in records)
        {
            Assert.True(ManifestId.IsMatch(id), $"malformed manifest id {id}");
            Assert.True(statuses.Contains(status), $"{id}: unknown status {status}");
        }
    }

    [Fact]
    public void EveryFidelityRecordACheckNamesExistsInTheManifest()
    {
        var known = StatusById();
        var missing = new List<string>();
        foreach (var c in HardwareCatalog.All)
        {
            Assert.True(c.FidelityRecords.Count > 0, $"{c.Id} names no fidelity record");
            Assert.Equal(c.FidelityRecords.Count, c.FidelityRecords.Distinct().Count());
            foreach (var id in c.FidelityRecords)
            {
                Assert.True(ManifestId.IsMatch(id), $"{c.Id}: malformed fidelity id {id}");
                if (!known.ContainsKey(id)) missing.Add($"{c.Id}:{id}");
            }
        }
        Assert.True(missing.Count == 0, "fidelity records named by a check but absent from the manifest: " + string.Join(", ", missing));
    }

    [Fact]
    public void CoreIdsAreOnlyInCoreRegressionsAndManifestIdsOnlyInFidelityRecords()
    {
        foreach (var c in HardwareCatalog.All)
        {
            foreach (var id in c.FidelityRecords)
                Assert.False(CoreId.IsMatch(id), $"{c.Id}: {id} is a core regression, not a fidelity record");
            foreach (var id in c.CoreRegressions)
            {
                Assert.False(ManifestId.IsMatch(id), $"{c.Id}: {id} is a manifest record, not a core regression");
                Assert.True(CoreId.IsMatch(id), $"{c.Id}: malformed core regression id {id}");
            }
        }
    }

    /// <summary>
    /// Unresolved uncertainty a check runs into has to be visible in that check, not hidden behind the
    /// source-backed records beside it.
    /// </summary>
    [Theory]
    [InlineData("K", "M11-005", "RECOVERABLE_GAP")]
    [InlineData("E", "M3-016", "HARDWARE_ONLY")]
    [InlineData("A", "M9-013", "BLOCKED_EXTERNAL")]
    [InlineData("A", "M9-023", "HARDWARE_ONLY")]
    [InlineData("A2", "M9-022", "BLOCKED_EXTERNAL")]
    [InlineData("A3", "M9-026", "BLOCKED_EXTERNAL")]
    [InlineData("Y", "M11-016", "BLOCKED_EXTERNAL")]
    [InlineData("CR2", "M5-022", "RECOVERABLE_GAP")]
    [InlineData("CR3", "M5-023", "RECOVERABLE_GAP")]
    [InlineData("I", "M4-012", "RECOVERABLE_GAP")]
    [InlineData("I", "M4-013", "HARDWARE_ONLY")]
    [InlineData("CR1", "M7-017", "RECOVERABLE_GAP")]
    [InlineData("T", "M13-014", "RECOVERABLE_GAP")]
    [InlineData("U", "M13-015", "RECOVERABLE_GAP")]
    public void UnresolvedUncertaintyIsVisibleInTheCheckItAffects(string check, string record, string status)
    {
        Assert.Contains(record, HardwareCatalog.Find(check)!.FidelityRecords);
        Assert.Equal(status, StatusById()[record]);
    }

    [Fact]
    public void EverySingingCheckNamesEveryBlockedOrHardwareOnlySingingRecord()
    {
        var status = StatusById();
        var open = status.Where(kv => kv.Key.StartsWith("M9-") && kv.Value is "BLOCKED_EXTERNAL" or "HARDWARE_ONLY")
            .Select(kv => kv.Key).ToList();
        Assert.NotEmpty(open);
        foreach (var check in new[] { "A", "A2", "A3" })
            foreach (var id in open)
                Assert.Contains(id, HardwareCatalog.Find(check)!.FidelityRecords);
    }
}
