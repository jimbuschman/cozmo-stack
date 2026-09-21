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

    // ---------------------------------------------------------------- M7-013

    /// <summary>
    /// A value saturates at plus or minus one: <c>Emotion::Add</c> clamps the sum between the two
    /// literals it loads at 0x00679622 and 0x00679630 before storing it.
    /// </summary>
    [Fact]
    public void AnAxisSaturatesAtOne()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var model = MoodModel.Load(obb);
        var ev = model.Events.FirstOrDefault(e => e.Affectors.Count > 0 && Math.Abs(e.Affectors[0].Value) > 0.1);
        Assert.NotNull(ev);
        var axis = ev!.Affectors[0].Emotion;
        double sign = Math.Sign(ev.Affectors[0].Value);

        var mood = new MoodState(model);
        for (int i = 0; i < 100; i++) mood.Trigger(ev.Name, 0);      // no time passes, so nothing decays
        Assert.Equal(sign, mood[axis], 6);
    }

    /// <summary>
    /// The decay curve is flat outside its nodes and linear between them, which is the whole of
    /// <c>GraphEvaluator2d::EvaluateY</c> at 0x00804BD0.
    /// </summary>
    [Fact]
    public void TheDecayCurveIsFlatOutsideItsNodesAndLinearBetween()
    {
        var g = new DecayGraph("test", new[] { (10.0, 1.0), (20.0, 0.5), (30.0, 0.0) });
        Assert.Equal(1.0, g.At(-100));      // below the first node
        Assert.Equal(1.0, g.At(0));
        Assert.Equal(1.0, g.At(10));
        Assert.Equal(0.75, g.At(15), 6);    // linear between
        Assert.Equal(0.5, g.At(20), 6);
        Assert.Equal(0.0, g.At(30), 6);
        Assert.Equal(0.0, g.At(1_000));     // above the last node

        // a single node is that node's value at every x, as EvaluateY returns before the loop
        Assert.Equal(1.0, new DecayGraph("one", new[] { (0.0, 1.0) }).At(10_000));
    }

    /// <summary>
    /// The decay clock is only restarted by a change that is bigger than 0.05 and pushes the value
    /// further from zero without flipping its sign - the three tests at 0x006796A8, 0x0067968A and
    /// 0x006796AE. A change that fails any of them moves the value and leaves it decaying on the
    /// schedule it was already on.
    /// </summary>
    [Fact]
    public void OnlyAChangeThatDrivesTheValueOnwardsRestartsTheDecayClock()
    {
        Assert.Equal(0.05, MoodState.DecayResetThreshold);

        // Confident decays to zero 70 s after its clock was last restarted.
        var model = new MoodModel();
        model.AddDecayGraph(new DecayGraph("Confident", new[] { (0.0, 1.0), (30.0, 1.0), (60.0, 0.5), (70.0, 0.0) }));
        model.AddEvent(new EmotionEvent("Big", new[] { new EmotionAffector(EmotionType.Confident, 0.8) }));
        model.AddEvent(new EmotionEvent("Nudge", new[] { new EmotionAffector(EmotionType.Confident, 0.02) }));
        model.AddEvent(new EmotionEvent("Back", new[] { new EmotionAffector(EmotionType.Confident, -0.2) }));

        // a nudge at 40 s does not restart the clock, so the value is gone by 70 s
        var a = new MoodState(model);
        a.Trigger("Big", 0);
        a.Trigger("Nudge", 40);
        a.Advance(70);
        Assert.Equal(0, a[EmotionType.Confident], 6);

        // nor does a change back towards zero
        var b = new MoodState(model);
        b.Trigger("Big", 0);
        b.Trigger("Back", 40);
        b.Advance(70);
        Assert.Equal(0, b[EmotionType.Confident], 6);

        // but a second big push in the same direction does, so there is still something left at 70 s
        var c = new MoodState(model);
        c.Trigger("Big", 0);
        c.Trigger("Big", 40);
        c.Advance(70);
        Assert.True(c[EmotionType.Confident] > 0.4,
                    $"the clock was not restarted: {c[EmotionType.Confident]}");
        c.Advance(110);
        Assert.Equal(0, c[EmotionType.Confident], 6);
    }
}
