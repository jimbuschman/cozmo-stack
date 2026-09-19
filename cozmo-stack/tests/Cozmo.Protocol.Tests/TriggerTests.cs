using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Tests for the shipped animation-trigger system.
///
/// The synthetic ones run everywhere. The ones that read the OBB skip themselves when it is not unpacked,
/// as the other asset tests do.
/// </summary>
public class TriggerTests
{
    private static IEnumerable<string> AssetRoots()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            yield return Path.Combine(d.FullName, "re-analysis", "obb");
            d = d.Parent;
        }
    }

    private static string? ObbRoot() =>
        AssetRoots().FirstOrDefault(r =>
            Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "assets", "animationGroups")));

    private static string TempMap(params (string Event, string Anim)[] pairs)
    {
        var dir = Directory.CreateTempSubdirectory("trigmap").FullName;
        var body = string.Join(",\n", pairs.Select(p =>
            $"    {{ \"CladEvent\": \"{p.Event}\", \"AnimName\": \"{p.Anim}\" }}"));
        File.WriteAllText(Path.Combine(dir, "AnimationTriggerMap.json"), "{\n  \"Pairs\": [\n" + body + "\n  ]\n}");
        return dir;
    }

    // ------------------------------------------------------------------ the map itself

    [Fact]
    public void TheMapReadsPairsAndResolvesTriggerNames()
    {
        var dir = TempMap(("AcknowledgeObject", "ag_x"), ("BlockReact", "ag_y"));
        try
        {
            var map = AnimationTriggerMap.Load(dir);
            Assert.Equal(2, map.Count);
            Assert.Equal("ag_x", map.GroupFor(AnimationTrigger.AcknowledgeObject));
            Assert.Equal("ag_y", map.GroupFor(AnimationTrigger.BlockReact));
            Assert.Empty(map.UnknownTriggers);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// A name the enum does not define is recorded rather than dropped, because that means the map and the
    /// enum came from different builds — something worth knowing, not something to swallow.
    /// </summary>
    [Fact]
    public void ANameThatIsNotATriggerIsRecordedRatherThanDropped()
    {
        var dir = TempMap(("BlockReact", "ag_y"), ("NotARealTrigger", "ag_z"));
        try
        {
            var map = AnimationTriggerMap.Load(dir);
            Assert.Equal(1, map.Count);
            Assert.Equal("NotARealTrigger", Assert.Single(map.UnknownTriggers));
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>Nothing is invented: a trigger the shipped map does not name resolves to nothing, and says why.</summary>
    [Fact]
    public void AnUnmappedTriggerResolvesToNothingAndSaysWhy()
    {
        var dir = TempMap(("BlockReact", "ag_y"));
        try
        {
            var map = AnimationTriggerMap.Load(dir);
            var lib = AnimationLibrary.Open(EmptyAssets());
            var r = map.Resolve(AnimationTrigger.AcknowledgeObject, lib);
            Assert.False(r.Resolved);
            Assert.Null(r.GroupName);
            Assert.Contains("names no group", r.Problem);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void AGroupTheAssetsDoNotDefineIsReportedRatherThanSubstituted()
    {
        var dir = TempMap(("BlockReact", "ag_missing"));
        try
        {
            var map = AnimationTriggerMap.Load(dir);
            var lib = AnimationLibrary.Open(EmptyAssets());
            var r = map.Resolve(AnimationTrigger.BlockReact, lib);
            Assert.False(r.Resolved);
            Assert.Equal("ag_missing", r.GroupName);
            Assert.Contains("do not define", r.Problem);
        }
        finally { Directory.Delete(dir, true); }
    }

    private static string EmptyAssets()
    {
        var dir = Directory.CreateTempSubdirectory("emptyassets").FullName;
        Directory.CreateDirectory(Path.Combine(dir, "animations"));
        Directory.CreateDirectory(Path.Combine(dir, "animationGroups"));
        return dir;
    }

    // ------------------------------------------------------------------ against the shipped assets

    /// <summary>
    /// The shipped table must be complete: every trigger it names must reach an animation that actually
    /// exists. This is what says the reactive layer has real behaviour underneath rather than a table of
    /// dangling names.
    /// </summary>
    [Fact]
    public void EveryShippedTriggerResolvesToAnAnimationThatExists()
    {
        var obb = ObbRoot();
        if (obb is null) return;

        var map = AnimationTriggerMap.Load(obb);
        var lib = AnimationLibrary.Open(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        Assert.Empty(map.UnknownTriggers);
        Assert.Equal(573, map.Count);

        var random = new Random(1);
        int mapped = 0, resolved = 0;
        var broken = new List<string>();
        foreach (var t in Enum.GetValues<AnimationTrigger>())
        {
            var r = map.Resolve(t, lib, random);
            if (r.GroupName is null) continue;
            mapped++;
            if (r.Resolved && lib.HasClip(r.Selected!)) resolved++;
            else if (broken.Count < 5) broken.Add($"{t} -> {r.GroupName}: {r.Problem ?? "clip absent"}");
        }
        Assert.Equal(573, mapped);
        Assert.True(broken.Count == 0, $"{mapped - resolved} triggers do not reach a clip, e.g. {string.Join("; ", broken)}");
        Assert.Equal(mapped, resolved);
    }

    /// <summary>
    /// Exactly two shipped triggers name no group. Pinning them means a future build that changes the
    /// table is noticed rather than silently absorbed.
    /// </summary>
    [Fact]
    public void OnlyTheTwoKnownTriggersNameNoGroup()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var map = AnimationTriggerMap.Load(obb);
        var unmapped = Enum.GetValues<AnimationTrigger>().Where(t => map.GroupFor(t) is null).ToList();
        Assert.Equal(
            new[] { AnimationTrigger.ProceduralLive, AnimationTrigger.ReactToMotorCalibration }.OrderBy(x => x),
            unmapped.OrderBy(x => x));
    }

    /// <summary>Selection is seeded, so a run can be reproduced when explaining what was chosen.</summary>
    [Fact]
    public void SelectionIsReproducibleForAGivenSeed()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var map = AnimationTriggerMap.Load(obb);
        var lib = AnimationLibrary.Open(Path.Combine(obb, "assets", "cozmo_resources", "assets"));

        var first = Enum.GetValues<AnimationTrigger>()
            .Select(t => map.Resolve(t, lib, new Random(42)).Selected).ToList();
        var again = Enum.GetValues<AnimationTrigger>()
            .Select(t => map.Resolve(t, lib, new Random(42)).Selected).ToList();
        Assert.Equal(first, again);
    }
}
