using Cozmo.Conformance;
using Xunit;

namespace Cozmo.Protocol.Tests;

// The idle hardware run grades nothing, so these cover only its bookkeeping and its record references.
public class M13IdleHwTests
{
    // reach: unit (synthetic segments through the harness summary)
    [Fact]
    public void SummaryTotalsBehaviourTimeAndCountsEachStart()
    {
        var s = M13IdleHw.Summarise(new M13IdleHw.Segment[]
        {
            new(0, 1000, "-", "-"), new(1000, 3000, "Socialize", "FindFaces_socialize"),
            new(3000, 3500, "PlayAlone", "-"), new(3500, 5500, "Socialize", "FindFaces_socialize"),
        });
        Assert.Equal(5.5, s["seconds"]!.GetValue<double>());
        Assert.Equal(1.5, s["secondsWithNoBehaviour"]!.GetValue<double>());
        var top = s["behaviours"]!.AsArray()[0]!;
        Assert.Equal("FindFaces_socialize", top["name"]!.GetValue<string>());
        Assert.Equal(4, top["seconds"]!.GetValue<double>());
        Assert.Equal(2, top["entered"]!.GetValue<int>());
    }

    // reach: unit (empty run)
    [Fact]
    public void SummaryOfAnEmptyRunIsZero()
    {
        var s = M13IdleHw.Summarise(Array.Empty<M13IdleHw.Segment>());
        Assert.Equal(0, s["seconds"]!.GetValue<double>());
        Assert.Empty(s["behaviours"]!.AsArray());
    }

    // reach: manifest (every named record exists)
    [Fact]
    public void EveryNamedRecordIsReal()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "re-analysis", "fidelity_manifest.json"))) root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("repository root not found");
        var records = M13IdleHw.LoadRecords(Path.Combine(root, "re-analysis", "fidelity_manifest.json"));
        Assert.Equal(M13IdleHw.RecordIds.Length, records.Count);
    }
}
