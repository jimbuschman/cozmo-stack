using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Tests for the reactive layer: arbitration, the reaction table, and the dispatcher.
///
/// Arbitration is the part worth testing hardest. Its whole job is to stop the robot fighting itself, and
/// a hierarchy that is subtly wrong shows up on hardware as a robot that ignores the application or
/// twitches over its own animations.
/// </summary>
public class BehaviorTests
{
    // ------------------------------------------------------------------ arbitration

    private static BehaviorArbiter Arb(bool autonomy = true) =>
        new() { AutonomyEnabled = autonomy, ReactionCooldown = TimeSpan.Zero };

    [Fact]
    public void NothingAutonomousRunsUntilAutonomyIsSwitchedOn()
    {
        var a = Arb(autonomy: false);
        Assert.Equal(BehaviorOutcome.Disabled, a.Request(BehaviorPriority.Reaction, "r").Outcome);
        Assert.Equal(BehaviorOutcome.Disabled, a.Request(BehaviorPriority.Idle, "i").Outcome);
        // the caller is never gated by it: direct control always works
        Assert.True(a.Request(BehaviorPriority.Caller, "c").Started);
    }

    [Fact]
    public void ACallerRequestInterruptsAReaction()
    {
        var a = Arb();
        Assert.Equal(BehaviorOutcome.Played, a.Request(BehaviorPriority.Reaction, "react").Outcome);
        var d = a.Request(BehaviorPriority.Caller, "caller");
        Assert.Equal(BehaviorOutcome.Interrupted, d.Outcome);
        Assert.Equal("react", d.Displaced);
    }

    [Fact]
    public void AReactionInterruptsIdleButNeverTheCaller()
    {
        var a = Arb();
        a.Request(BehaviorPriority.Idle, "idle");
        Assert.Equal(BehaviorOutcome.Interrupted, a.Request(BehaviorPriority.Reaction, "react").Outcome);

        var b = Arb();
        b.Request(BehaviorPriority.Caller, "caller");
        var d = b.Request(BehaviorPriority.Reaction, "react");
        Assert.Equal(BehaviorOutcome.Suppressed, d.Outcome);
        Assert.Contains("caller", d.Reason);
    }

    [Fact]
    public void AnEqualPriorityRequestIsSuppressedRatherThanInterleaved()
    {
        var a = Arb();
        a.Request(BehaviorPriority.Reaction, "first");
        Assert.Equal(BehaviorOutcome.Suppressed, a.Request(BehaviorPriority.Reaction, "second").Outcome);
    }

    [Fact]
    public void FinishingReleasesTheSlot()
    {
        var a = Arb();
        a.Request(BehaviorPriority.Caller, "c");
        a.Finished(BehaviorPriority.Caller);
        Assert.Null(a.Running);
        Assert.True(a.Request(BehaviorPriority.Idle, "i").Started);
    }

    /// <summary>
    /// A completion arriving after something else took over must not clear the newcomer. Without this a
    /// slow reaction finishing would silently unlock the slot a caller animation is holding.
    /// </summary>
    [Fact]
    public void ALateCompletionDoesNotClearWhatTookOver()
    {
        var a = Arb();
        a.Request(BehaviorPriority.Idle, "idle");
        a.Request(BehaviorPriority.Caller, "caller");
        a.Finished(BehaviorPriority.Idle);          // the idle animation finally reports in
        Assert.Equal(BehaviorPriority.Caller, a.Running);
    }

    [Fact]
    public void ARepeatedReactionIsHeldOffByItsCooldown()
    {
        var a = new BehaviorArbiter { AutonomyEnabled = true, ReactionCooldown = TimeSpan.FromSeconds(5) };
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(a.Request(BehaviorPriority.Reaction, "x", ReactionTrigger.CliffDetected, t0).Started);
        a.Finished(BehaviorPriority.Reaction);

        var d = a.Request(BehaviorPriority.Reaction, "x", ReactionTrigger.CliffDetected, t0.AddSeconds(1));
        Assert.Equal(BehaviorOutcome.OnCooldown, d.Outcome);

        Assert.True(a.Request(BehaviorPriority.Reaction, "x", ReactionTrigger.CliffDetected,
                              t0.AddSeconds(6)).Started);
    }

    [Fact]
    public void ADifferentReactionIsNotHeldOffByAnothersCooldown()
    {
        var a = new BehaviorArbiter { AutonomyEnabled = true, ReactionCooldown = TimeSpan.FromSeconds(5) };
        var t0 = DateTime.UtcNow;
        a.Request(BehaviorPriority.Reaction, "x", ReactionTrigger.CliffDetected, t0);
        a.Finished(BehaviorPriority.Reaction);
        Assert.True(a.Request(BehaviorPriority.Reaction, "y", ReactionTrigger.RobotPickedUp, t0).Started);
    }

    [Fact]
    public void EveryDecisionIsReportedIncludingTheOnesThatPlayedNothing()
    {
        var a = Arb();
        var seen = new List<BehaviorDecision>();
        a.Decided += seen.Add;
        a.Request(BehaviorPriority.Caller, "c");
        a.Request(BehaviorPriority.Reaction, "r");     // suppressed
        Assert.Equal(2, seen.Count);
        Assert.Contains(seen, d => d.Outcome == BehaviorOutcome.Suppressed);
    }

    // ------------------------------------------------------------------ the reaction table

    /// <summary>
    /// Every mapped reaction must carry its evidence. The table's honesty is the point: if an entry ever
    /// appears without a basis, the layer has started inventing behaviour.
    /// </summary>
    [Fact]
    public void EveryReactionRecordsWhatItIsBasedOn()
    {
        Assert.NotEmpty(ReactionTable.Default.Entries);
        Assert.All(ReactionTable.Default.Entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Basis));
            Assert.Contains("ReactionTrigger." + e.Trigger, e.Basis);
            Assert.Contains("AnimationTrigger." + e.Animation, e.Basis);
        });
    }

    /// <summary>
    /// The reactions this build claims must be ones M4 can actually detect. Adding one the sensors cannot
    /// see would make the layer claim a capability it does not have.
    /// </summary>
    [Fact]
    public void OnlyReactionsTheSensorsCanDetectAreMapped()
    {
        var detectable = new[]
        {
            ReactionTrigger.CliffDetected, ReactionTrigger.RobotPickedUp,
            ReactionTrigger.PlacedOnCharger, ReactionTrigger.RobotFalling,
        };
        Assert.Equal(detectable.OrderBy(x => x),
                     ReactionTable.Default.Entries.Select(e => e.Trigger).OrderBy(x => x));
    }

    [Fact]
    public void AnUnmappedReactionReturnsNothingRatherThanSomethingPlausible()
    {
        Assert.Null(ReactionTable.Default.For(ReactionTrigger.CubeMoved));
        Assert.Null(ReactionTable.Default.For(ReactionTrigger.FacePositionUpdated));
        Assert.Null(ReactionTable.Default.For(ReactionTrigger.MotorCalibration));
    }

    // ------------------------------------------------------------------ the dispatcher

    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "assets", "animationGroups")))
                return r;
            d = d.Parent;
        }
        return null;
    }

    [Fact]
    public void EveryMappedReactionResolvesToARealAnimation()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var map = AnimationTriggerMap.Load(obb);
        var lib = AnimationLibrary.Open(Path.Combine(obb, "assets", "cozmo_resources", "assets"));

        foreach (var e in ReactionTable.Default.Entries)
        {
            var r = map.Resolve(e.Animation, lib, new Random(7));
            Assert.True(r.Resolved, $"{e.Trigger} -> {e.Animation}: {r.Problem}");
            Assert.True(lib.HasClip(r.Selected!), $"{e.Trigger} chose '{r.Selected}', which is not in the assets");
        }
    }

    [Fact]
    public void ADispatcherWithNoAssetsReportsRatherThanThrows()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        var reactive = new ReactiveBehavior(robot, AnimationTriggerMap.Load(obb));
        reactive.Arbiter.AutonomyEnabled = true;

        var d = reactive.Fire(ReactionTrigger.CliffDetected);
        Assert.Equal(BehaviorOutcome.Unresolved, d.Outcome);
        Assert.Contains("no animation assets", d.Reason);
    }

    [Fact]
    public void AnUnmappedReactionIsReportedByTheDispatcher()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        var reactive = new ReactiveBehavior(robot, AnimationTriggerMap.Load(obb));
        reactive.Arbiter.AutonomyEnabled = true;

        var d = reactive.Fire(ReactionTrigger.CubeMoved);
        Assert.Equal(BehaviorOutcome.Unresolved, d.Outcome);
        Assert.Contains("maps no animation", d.Reason);
    }

    [Fact]
    public void AReactionIsDisabledUntilAutonomyIsEnabled()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var reactive = new ReactiveBehavior(robot, AnimationTriggerMap.Load(obb));

        var d = reactive.Fire(ReactionTrigger.CliffDetected);
        Assert.Equal(BehaviorOutcome.Disabled, d.Outcome);
        Assert.False(robot.Animations.IsPlaying);
    }
}
