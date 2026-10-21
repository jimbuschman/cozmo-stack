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
    public void TheRecordsExistInTheManifest()
    {
        var warnings = new List<string>();
        var recs = M3M4Hw.LoadRecords(Manifest(), warnings);
        Assert.Equal(new[] { "M3-008", "M3-016", "M3-036", "M4-013", "M4-021", "M4-024", "M3-050" }, M3M4Hw.HardwareOnlyIds);
        Assert.Equal(new[] { "M3-012", "M3-021", "M3-055", "M3-060", "M4-018", "M4-019", "M4-027", "M4-038", "M4-047", "M4-051", "M2-021", "M5-010" }, M3M4Hw.RecordIds.Skip(7));
        Assert.Equal(M3M4Hw.RecordIds, recs.Select(r => r["id"]!.GetValue<string>()).ToArray());
        Assert.Empty(warnings);
    }

    [Fact]
    public void EveryCheckNamesOnlyKnownRecordsAndEachIsNamed() => Assert.Null(M3M4Hw.CheckDefinitionProblem());

    [Fact]
    public void RunOrderCoversEveryCheckExceptTheTeardownJudgement()
    {
        Assert.Equal(M3M4Hw.Checks.Select(c => c.Id).Where(id => id != "DISCONNECT_QUIET").OrderBy(x => x), M3M4Hw.RunOrder.OrderBy(x => x));
        Assert.Equal(M3M4Hw.RunOrder.Length, M3M4Hw.RunOrder.Distinct().Count());
    }

    [Theory]
    [InlineData("Top", "top")]
    [InlineData("2", "bottom")]
    [InlineData("bot", "bottom")]
    [InlineData("t", "t")]
    [InlineData("sideways", "sideways")]
    public void OperatorAnswersMapToTheOfferedChoice(string typed, string expected) =>
        Assert.Equal(expected, M3M4Hw.NormaliseAnswer(typed, new[] { "top", "bottom", "middle", "two-bands", "other" }));

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
