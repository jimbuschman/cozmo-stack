using System.Text.Json;
using System.Text.RegularExpressions;
using Cozmo.Conformance;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Structural validation of the fidelity ids the <c>control-check</c> hardware run names (AGENTS.md, "Tests and
/// manifests"): every id a check names exists in <c>re-analysis/fidelity_manifest.json</c>, every id written anywhere
/// in the tool's source is in <see cref="ControlCheck.CitedRecords"/>, and the HARDWARE_ONLY records the run depends on
/// are named by a check and listed in its hardware-only uncertainty. It does not prove that a check's records are the
/// right evidence for it; that is the verifier's reading.
/// </summary>
public class ControlCheckFidelityTests
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
    public void EveryCheckNamesRecordsThatExistInTheManifest()
    {
        var byId = StatusById();
        foreach (var c in ControlCheck.Checks)
        {
            Assert.NotEmpty(c.Records);
            Assert.Equal(c.Records.Length, c.Records.Distinct().Count());
            var missing = c.Records.Where(id => !byId.ContainsKey(id)).ToList();
            Assert.True(missing.Count == 0, $"control-check {c.Id} names records the manifest does not have: " + string.Join(", ", missing));
        }
        Assert.All(ControlCheck.CitedRecords, id => Assert.True(byId.ContainsKey(id), $"{id} is not in the manifest"));
    }

    [Fact]
    public void TheChecksAreTheSpecifiedOnesInOrder()
    {
        Assert.Equal(new[] { "CONNECT", "STATE", "HEAD", "LIFT", "DRIVE", "FACE", "AUDIO", "ANIM", "ANIM_CANCEL", "CUBES", "CAMERA", "DISCONNECT" },
                     ControlCheck.Checks.Select(c => c.Id).ToArray());
    }

    [Fact]
    public void ConnectAndCubesNameTheirRequiredRecords()
    {
        var connect = ControlCheck.Checks.Single(c => c.Id == "CONNECT").Records;
        foreach (var id in new[] { "M1-025", "M1-028", "M1-030", "M1-040", "M1-041" }) Assert.Contains(id, connect);
        var cubes = ControlCheck.Checks.Single(c => c.Id == "CUBES").Records;
        Assert.Contains("M1-042", cubes);
        Assert.Contains(cubes, id => id.StartsWith("M4-", StringComparison.Ordinal));
    }

    /// <summary>
    /// The second-run update (after M3/M4, 826d942 and 1dec497): each check names the records its new criteria and
    /// observations bear on.
    /// </summary>
    [Theory]
    [InlineData("CONNECT", new[] { "M3-019", "M3-021", "M3-022", "M4-020", "M4-021" })]
    [InlineData("CUBES", new[] { "M4-018", "M4-023", "M4-024", "M9-017" })]
    [InlineData("DRIVE", new[] { "M4-014", "M4-019" })]
    [InlineData("HEAD", new[] { "M4-003", "M4-016" })]
    [InlineData("LIFT", new[] { "M4-003", "M4-016" })]
    [InlineData("ANIM", new[] { "M5-023", "M5-026" })]
    [InlineData("ANIM_CANCEL", new[] { "M5-023", "M5-026" })]
    public void EachCheckNamesTheRecordsOfItsNewCriteriaAndObservations(string check, string[] ids)
    {
        var records = ControlCheck.Checks.Single(c => c.Id == check).Records;
        foreach (var id in ids) Assert.Contains(id, records);
    }

    [Fact]
    public void TheNewHardwareOnlyUncertaintyIsListed()
    {
        var byId = StatusById();
        foreach (var id in new[] { "M4-021", "M4-024" })
        {
            Assert.Equal("HARDWARE_ONLY", byId[id]);
            Assert.Contains(ControlCheck.HardwareOnly, h => h.Id == id);
        }
        Assert.Contains("M4-021", ControlCheck.Checks.Single(c => c.Id == "CONNECT").Records);
        Assert.Contains("M4-024", ControlCheck.Checks.Single(c => c.Id == "CUBES").Records);
    }

    [Fact]
    public void TheCancelCriterionAllowsOnlyTheBufferedFrame()
    {
        // M5 inventory A24/A25: the cancel keeps the send buffer, which holds at most one built frame
        Assert.Equal(1, ControlCheck.CancelMaxLeftoverAudioFrames);
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "cozmo-stack", "src", "Cozmo.Conformance", "ControlCheck.cs"));
        Assert.DoesNotContain("CancelGraceMs", src);
        Assert.Contains("noNewFrameAfterCancel", src);
        Assert.Contains("noEndOfAnimationAfterCancel", src);
        Assert.Contains("abortAnimationSent", src);
    }

    [Fact]
    public void EveryIdInTheToolSourceIsACitedRecord()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "cozmo-stack", "src", "Cozmo.Conformance", "ControlCheck.cs"));
        var ids = Regex.Matches(src, @"\bM\d{1,2}-\d{3}\b").Select(m => m.Value).Distinct().ToList();
        Assert.NotEmpty(ids);
        var byId = StatusById();
        foreach (var id in ids)
        {
            Assert.True(byId.ContainsKey(id), $"ControlCheck.cs names {id}, which is not in the manifest");
            Assert.Contains(id, ControlCheck.CitedRecords);
        }
    }

    [Fact]
    public void TheHardwareOnlyUncertaintyIsNamedAndReallyHardwareOnly()
    {
        var byId = StatusById();
        Assert.NotEmpty(ControlCheck.HardwareOnly);
        foreach (var (id, note) in ControlCheck.HardwareOnly)
        {
            Assert.Contains(id, ControlCheck.CitedRecords);
            Assert.Equal("HARDWARE_ONLY", byId[id]);
            Assert.False(string.IsNullOrWhiteSpace(note));
        }
        // every HARDWARE_ONLY record a check names is listed as uncertainty the run depends on
        foreach (var id in ControlCheck.CitedRecords.Where(id => byId[id] == "HARDWARE_ONLY"))
            Assert.Contains(ControlCheck.HardwareOnly, h => h.Id == id);
        Assert.Contains(ControlCheck.HardwareOnly, h => h.Id == "M3-016");
        Assert.Contains(ControlCheck.HardwareOnly, h => h.Id == "M1-033");
        Assert.Contains(ControlCheck.HardwareOnly, h => h.Id == "M4-021");
        Assert.Contains(ControlCheck.HardwareOnly, h => h.Id == "M4-024");
    }

    [Fact]
    public void TheMeasurementHelpersComputeWhatTheCriteriaSay()
    {
        // along the starting heading: heading 0 is the x change; heading pi/2 is the y change; backwards is negative
        Assert.Equal(30.0, ControlCheck.AlongHeading(10f, 5f, 0f, 40f, 9f), 3);
        Assert.Equal(-25.0, ControlCheck.AlongHeading(0f, 0f, MathF.PI / 2, 3f, -25f), 3);
        // a byte counter that wraps
        Assert.Equal(0, ControlCheck.ByteCounterDelta(7, 7));
        Assert.Equal(3, ControlCheck.ByteCounterDelta(254, 1));
        // the largest gap
        Assert.Equal(40.0, ControlCheck.MaxGap(new double[] { 0, 33, 73, 100 }), 6);
        Assert.True(double.IsNaN(ControlCheck.MaxGap(new double[] { 5 })));
    }

    [Fact]
    public void TheStateRateIsTakenFromTheRobotTimestamps()
    {
        // 31 states 30 ms apart span 900 ms: 30 intervals / 0.9 s
        var ts = Enumerable.Range(0, 31).Select(i => (uint)(1000 + 30 * i)).ToList();
        Assert.Equal(30 * 1000.0 / 900, ControlCheck.RateFromRobotTimestamps(ts), 6);
        // the span is taken modulo 2^32, so a wrap between first and last still gives the rate
        Assert.Equal(1000.0 / 30, ControlCheck.RateFromRobotTimestamps(new uint[] { uint.MaxValue - 9, 20 }), 6);
        Assert.True(double.IsNaN(ControlCheck.RateFromRobotTimestamps(new uint[] { 7 })));
        Assert.True(double.IsNaN(ControlCheck.RateFromRobotTimestamps(new uint[] { 7, 7 })));
    }

    [Fact]
    public void TheObbPreflightNamesTheClipFilesTheLoaderReads()
    {
        var root = Path.Combine(Path.GetTempPath(), "control-check-obb-" + Guid.NewGuid().ToString("N"));
        try
        {
            var expected = ControlCheck.ObbExpectedFiles(root);
            Assert.Equal(new[]
            {
                Path.Combine(root, "assets", "cozmo_resources", "assets", "animations", "anim_bored_01.bin"),
                Path.Combine(root, "assets", "cozmo_resources", "assets", "animations", "anim_codelab_staring_loop.bin"),
            }, expected);

            var missing = ControlCheck.ObbPreflight(root, given: true, repoRoot: null);
            Assert.NotNull(missing);
            Assert.Contains(expected[0], missing);
            Assert.Contains(expected[1], missing);
            Assert.Contains("--obb", missing);
            Assert.Contains("--no-obb", missing);

            Directory.CreateDirectory(Path.GetDirectoryName(expected[0])!);
            File.WriteAllBytes(expected[0], Array.Empty<byte>());
            var one = ControlCheck.ObbPreflight(root, given: true, repoRoot: null);
            Assert.NotNull(one);
            Assert.DoesNotContain(expected[0], one);
            Assert.Contains(expected[1], one);

            File.WriteAllBytes(expected[1], Array.Empty<byte>());
            Assert.Null(ControlCheck.ObbPreflight(root, given: true, repoRoot: null));

            var none = ControlCheck.ObbPreflight(null, given: false, repoRoot: root);
            Assert.NotNull(none);
            Assert.Contains("anim_bored_01.bin", none);
            Assert.Contains("anim_codelab_staring_loop.bin", none);
            Assert.Contains(Path.Combine(root, "re-analysis", "obb"), none);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
