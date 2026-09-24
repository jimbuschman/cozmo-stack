using System.Text.Json;
using System.Text.RegularExpressions;
using Cozmo.Conformance;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Structural validation of the fidelity ids the <c>m1-link-check</c> hardware run names (AGENTS.md, "Tests and
/// manifests"): each exists in <c>re-analysis/fidelity_manifest.json</c>, every id written anywhere in the tool's
/// source is in <see cref="LinkCheck.CitedRecords"/> (so the result's <c>records</c> list covers it), and the
/// HARDWARE_ONLY record the run depends on (M1-033) is named. It does not prove that a check's rows are the right
/// evidence for it; that is the verifier's reading.
/// </summary>
public class LinkCheckFidelityTests
{
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

    private static Dictionary<string, string> StatusById()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "re-analysis", "fidelity_manifest.json")));
        return doc.RootElement.GetProperty("records").EnumerateArray()
            .ToDictionary(r => r.GetProperty("id").GetString()!, r => r.GetProperty("status").GetString()!);
    }

    [Fact]
    public void EveryCitedRecordExistsInTheManifest()
    {
        var byId = StatusById();
        var missing = LinkCheck.CitedRecords.Where(id => !byId.ContainsKey(id)).ToList();
        Assert.True(missing.Count == 0, "m1-link-check names records the manifest does not have: " + string.Join(", ", missing));
        Assert.Equal(LinkCheck.CitedRecords.Length, LinkCheck.CitedRecords.Distinct().Count());
    }

    [Fact]
    public void EveryIdInTheToolSourceIsACitedRecord()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "cozmo-stack", "src", "Cozmo.Conformance", "LinkCheck.cs"));
        var ids = Regex.Matches(src, @"\bM\d{1,2}-\d{3}\b").Select(m => m.Value).Distinct().ToList();
        Assert.NotEmpty(ids);
        var byId = StatusById();
        foreach (var id in ids)
        {
            Assert.True(byId.ContainsKey(id), $"LinkCheck.cs names {id}, which is not in the manifest");
            Assert.Contains(id, LinkCheck.CitedRecords);
        }
    }

    [Fact]
    public void TheHardwareOnlyRobotSideRecordIsNamed()
    {
        Assert.Contains("M1-033", LinkCheck.CitedRecords);
        Assert.Equal("HARDWARE_ONLY", StatusById()["M1-033"]);
    }
}
