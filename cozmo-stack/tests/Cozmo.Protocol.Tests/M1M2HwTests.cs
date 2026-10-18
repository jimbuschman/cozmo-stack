using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>Structural validation of the M1M2-HW run: its record ids exist in the manifest, the checks name exactly those, and the run refuses a needs device directory.</summary>
public class M1M2HwTests
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
    public void TheRecordsExistAndOnlyM1033IsHardwareOnly()
    {
        var warnings = new List<string>();
        var recs = M1M2Hw.LoadRecords(Manifest(), warnings);
        Assert.Equal(M1M2Hw.RecordIds, recs.Select(r => r["id"]!.GetValue<string>()).ToArray());
        Assert.Empty(warnings);
    }

    [Fact]
    public void EveryCheckNamesOnlyTheListedRecordsAndEachIsNamed() => Assert.Null(M1M2Hw.CheckDefinitionProblem());

    [Fact]
    public void AMissingRecordStopsTheRun()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, new JsonObject { ["records"] = new JsonArray(new JsonObject { ["id"] = "M1-033", ["status"] = "HARDWARE_ONLY" }) }.ToJsonString());
            var e = Assert.Throws<InvalidOperationException>(() => M1M2Hw.LoadRecords(path, new List<string>()));
            Assert.Contains("M1-054", e.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AStateDirectoryIsRefusedBeforeAnyNetworkUse() =>
        Assert.Equal(2, M1M2Hw.Run(new[] { "m1m2-hw", "--resources", ".", "--state-dir", "x" }));
}
