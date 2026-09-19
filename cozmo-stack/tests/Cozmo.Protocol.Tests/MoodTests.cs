using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Tests for the mood model. The headline result is a negative one and is asserted deliberately:
/// mood does not change animation selection in this build.
/// </summary>
public class MoodTests
{
    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (File.Exists(Path.Combine(r, "assets", "cozmo_resources", "config", "engine", "mood_config.json")))
                return r;
            d = d.Parent;
        }
        return null;
    }

    [Fact]
    public void TheShippedConfigFilesLoadDespiteTheirComments()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var m = MoodModel.Load(obb);
        Assert.NotEmpty(m.Events);
        Assert.NotEmpty(m.DecayGraphs);
        Assert.Empty(m.UnknownEmotions);
    }

    /// <summary>The shipped files use // comments, which JSON does not allow.</summary>
    [Fact]
    public void CommentsAreStrippedRatherThanRejected()
    {
        var text = "{ \"a\": 1, // a note\n  \"b\": 2 }";
        var clean = MoodModel.Decomment(text);
        Assert.DoesNotContain("a note", clean);
        Assert.Contains("\"b\"", clean);
    }

    [Fact]
    public void DecayInterpolatesBetweenNodesAndFlattensOutside()
    {
        var g = new DecayGraph("default", new (double, double)[] { (0, 1), (10, 1), (150, 0) });
        Assert.Equal(1, g.At(-5));
        Assert.Equal(1, g.At(0));
        Assert.Equal(1, g.At(10));
        Assert.Equal(0.5, g.At(80), 2);
        Assert.Equal(0, g.At(150));
        Assert.Equal(0, g.At(1000));
    }

    [Fact]
    public void AnEventMovesTheAxesItNamesAndThenFades()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var model = MoodModel.Load(obb);
        var ev = model.Events.FirstOrDefault(e => e.Affectors.Count > 0);
        Assert.NotNull(ev);

        var mood = new MoodState(model);
        Assert.True(mood.Trigger(ev!.Name, 0));
        var moved = ev.Affectors[0].Emotion;
        Assert.NotEqual(0, mood[moved]);

        // wind well past the longest shipped decay curve
        mood.Advance(1000);
        Assert.Equal(0, mood[moved], 3);
    }

    [Fact]
    public void AnUnknownEventChangesNothing()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var mood = new MoodState(MoodModel.Load(obb));
        Assert.False(mood.Trigger("NotAnEventThatExists", 0));
        Assert.All(mood.Snapshot().Values, v => Assert.Equal(0, v));
    }

    /// <summary>
    /// The finding that bounds the whole mood layer: every animation-group entry in this build is
    /// "Default", so no mood can change what is selected. If a future build adds mood-specific
    /// alternatives this test fails, which is exactly when the selector would need writing.
    /// </summary>
    [Fact]
    public void AnimationSelectionIsMoodInvariantInThisBuild()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var groups = Path.Combine(obb, "assets", "cozmo_resources", "assets", "animationGroups");
        if (!Directory.Exists(groups)) return;

        var moods = new HashSet<string>(StringComparer.Ordinal);
        int entries = 0;
        foreach (var f in Directory.EnumerateFiles(groups, "*.json", SearchOption.AllDirectories))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(f));
            if (!doc.RootElement.TryGetProperty("Animations", out var anims)) continue;
            foreach (var a in anims.EnumerateArray())
            {
                entries++;
                if (a.TryGetProperty("Mood", out var m) && m.GetString() is { } s) moods.Add(s);
            }
        }
        Assert.True(entries > 1000, $"only {entries} group entries were read");
        Assert.Equal(new[] { "Default" }, moods.OrderBy(x => x));
    }
}
