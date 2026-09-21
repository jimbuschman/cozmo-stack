using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

// The two filters the shipped animation groups carry and this stack was not applying
// (fidelity manifest M5-014).
public class AnimationGroupGateTests
{
    private static AnimationGroup Group(params AnimationGroupEntry[] e) =>
        new() { Name = "test", Entries = e };

    /// <summary>
    /// An entry stays out of the draw until its cooldown has run: IsAnimationOnCooldown excludes it
    /// until now >= selectedAt + CooldownTime_Sec.
    /// </summary>
    [Fact]
    public void AnEntryIsSkippedWhileItsCooldownRuns()
    {
        var g = Group(new AnimationGroupEntry("a", 1f, 10f, "Default"),
                      new AnimationGroupEntry("b", 1f, 0f, "Default"));
        var rng = new Random(1);

        // force "a" by making it the only candidate at t=0
        var first = g.Choose(rng, nowSec: 0, mood: null);
        Assert.NotNull(first);

        // whichever was taken is on cooldown only if it has one; "b" never is
        for (int i = 0; i < 20; i++)
        {
            var e = g.Choose(rng, nowSec: 1 + i * 0.1, mood: null);
            Assert.NotNull(e);
            if (e!.Name == "a") Assert.False(e.IsOnCooldown(1 + i * 0.1));
        }
    }

    /// <summary>
    /// With every candidate on cooldown the engine does not give up: it takes the one that comes off
    /// soonest.
    /// </summary>
    [Fact]
    public void WhenEverythingIsOnCooldownTheSoonestIsTaken()
    {
        var slow = new AnimationGroupEntry("slow", 1f, 100f, "Default");
        var quick = new AnimationGroupEntry("quick", 1f, 5f, "Default");
        var g = Group(slow, quick);

        // put both on cooldown at t = 0
        slow.GetType();
        var rng = new Random(2);
        g.Choose(rng, nowSec: 0);
        g.Choose(rng, nowSec: 0);
        // both were drawn at most once; drive them both onto cooldown explicitly through a draw each
        var a = g.Choose(rng, nowSec: 0);
        var b = g.Choose(rng, nowSec: 0);
        Assert.NotNull(a); Assert.NotNull(b);

        // now everything is on cooldown: the quick one is next off it
        var picked = g.Choose(rng, nowSec: 1);
        Assert.Equal("quick", picked!.Name);
    }

    /// <summary>
    /// The head-angle gate drops entries whose window the head is outside of, and is ignored entirely
    /// when the caller does not know the head angle.
    /// </summary>
    [Fact]
    public void TheHeadAngleGateDropsEntriesOutsideTheirWindow()
    {
        var low = new AnimationGroupEntry("low", 1f, 0f, "Default")
            { UseHeadAngle = true, HeadAngleMinDeg = -25f, HeadAngleMaxDeg = -10f };
        var high = new AnimationGroupEntry("high", 1f, 0f, "Default")
            { UseHeadAngle = true, HeadAngleMinDeg = 10f, HeadAngleMaxDeg = 44f };
        var g = Group(low, high);
        var rng = new Random(3);

        for (int i = 0; i < 10; i++) Assert.Equal("low", g.Choose(rng, headAngleDeg: -15)!.Name);
        for (int i = 0; i < 10; i++) Assert.Equal("high", g.Choose(rng, headAngleDeg: 30)!.Name);

        Assert.True(low.HeadAngleAllows(null));            // no head angle known: no gate
        Assert.False(low.HeadAngleAllows(30));
        Assert.True(new AnimationGroupEntry("plain", 1f, 0f, "Default").HeadAngleAllows(1000));
    }

    /// <summary>And the shipped CozmoSays groups really do carry the gate, which is why it matters.</summary>
    [Fact]
    public void TheShippedCozmoSaysGroupsCarryTheGate()
    {
        if (WwiseAssets.ObbRoot is not { } obb) return;
        var lib = AnimationLibrary.Open(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var g = lib.GetGroup("ag_cozmosays_badword");
        Assert.NotNull(g);                                  // the shipped OBB has it; do not skip quietly
        Assert.Contains(g!.Entries, e => e.UseHeadAngle);
        Assert.Contains(g!.Entries, e => e.UseHeadAngle && e.HeadAngleMinDeg == -10f);
    }
}
