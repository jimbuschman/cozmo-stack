using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>Structural validation of the DRAFT M3M4-HW run: its seven record ids exist in the manifest and the checks name exactly those.</summary>
public class M3M4HwTests
{
    private static string Manifest()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var p = Path.Combine(dir.FullName, "re-analysis", "fidelity_manifest.json");
            if (File.Exists(p)) return p;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("fidelity_manifest.json not found");
    }

    [Fact]
    public void TheSevenRecordsExistInTheManifest()
    {
        var warnings = new List<string>();
        var recs = M3M4Hw.LoadRecords(Manifest(), warnings);
        Assert.Equal(new[] { "M3-008", "M3-016", "M3-036", "M4-013", "M4-021", "M4-024", "M3-050" }, M3M4Hw.RecordIds);
        Assert.Equal(M3M4Hw.RecordIds, recs.Select(r => r["id"]!.GetValue<string>()).ToArray());
    }

    [Fact]
    public void EveryCheckNamesOnlyTheSevenAndEachIsNamed() => Assert.Null(M3M4Hw.CheckDefinitionProblem());

    [Fact]
    public void AMissingRecordStopsTheRun()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, new JsonObject { ["records"] = new JsonArray(new JsonObject { ["id"] = "M3-008", ["status"] = "HARDWARE_ONLY" }) }.ToJsonString());
            var ex = Assert.Throws<InvalidOperationException>(() => M3M4Hw.LoadRecords(path, new List<string>()));
            Assert.Contains("M4-024", ex.Message);
            Assert.Contains("M3-050", ex.Message);
        }
        finally { File.Delete(path); }
    }
}
