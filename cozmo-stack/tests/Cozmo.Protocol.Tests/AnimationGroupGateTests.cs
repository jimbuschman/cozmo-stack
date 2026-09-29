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
    /// soonest. C5 item 4.11/4.12: only an entry with UseHeadAngle has a defined head window, so the
    /// entries here carry one and the head angle is inside it.
    /// </summary>
    [Fact]
    public void WhenEverythingIsOnCooldownTheSoonestIsTaken()
    {
        var slow = new AnimationGroupEntry("slow", 1f, 100f, "Default")
            { UseHeadAngle = true, HeadAngleMinDeg = -30f, HeadAngleMaxDeg = 30f };
        var quick = new AnimationGroupEntry("quick", 1f, 5f, "Default")
            { UseHeadAngle = true, HeadAngleMinDeg = -30f, HeadAngleMaxDeg = 30f };
        var g = Group(slow, quick);

        // draw until both are on cooldown (each draw sets now + cooldown)
        var rng = new Random(2);
        for (int i = 0; i < 10; i++) g.Choose(rng, nowSec: 0, headAngleDeg: 0);

        // now everything is on cooldown: the quick one is next off it
        var picked = g.Choose(rng, nowSec: 1, headAngleDeg: 0);
        Assert.Equal("quick", picked!.Name);
    }

    /// <summary>
    /// C5 item 4.11/4.12 (forced policy SD2): an entry without UseHeadAngle has no defined head window, so it
    /// never qualifies for the Default-mood backup and the backup falls to the first entry.
    /// </summary>
    [Fact]
    public void AnEntryWithoutUseHeadAngleNeverQualifiesForTheBackup()
    {
        var first = new AnimationGroupEntry("first", 1f, 100f, "Default");
        var soon = new AnimationGroupEntry("soon", 1f, 50f, "Default");
        var g = Group(first, soon);

        // put both on cooldown explicitly, so both are filtered out of the main draw
        first.Container.SetCooldown("first", 100);
        soon.Container.SetCooldown("soon", 50);

        // "soon" comes off cooldown first, but it has no window, so the backup takes the first entry
        Assert.Equal("first", g.Choose(new Random(4), nowSec: 1, headAngleDeg: 0)!.Name);
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
