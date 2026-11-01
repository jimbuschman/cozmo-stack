using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class FistBumpStrategyTests
{
    private static FistBumpObjectiveParams Row(string name, double cooldown = 180, double probability = .5, double expiration = 3)
        => new(name, cooldown, probability, expiration);

    [Fact]
    public void ConverterAndParameterValidationFollowNativeTable()
    {
        // reach: unit; native enum table 0x1033140 and loader 0x60E20E..422.
        string[] names = { "Unknown", "AnimationPlayed", "BlockRolled", "BuiltPyramid", "DroveAsIntended", "EnrolledFaceWithName", "FireTruckAlarmPerformed", "FistBumpComplete", "FistBumpSuccess", "FistBumpLeftHanging", "FoundFace", "GatheredCubes", "InteractedWithFace", "KnockedOverBlocks", "LaserPounced", "LaserTracked", "PeekABooComplete", "PeekABooSuccess", "PerformedWorkout", "PerformedStrongWorkout", "PickedupBlock", "PickedUpAndPutDownBlock", "PoppedWheelie", "PouncedAndCaught", "PutDownBlock", "ReactedAcknowledgedCubeMoved", "ReactedAcknowledgedFace", "ReactedAcknowledgedObject", "ReactedToCliff", "ReactedToFrustration", "ReactedToOnCharger", "ReactedToPickup", "ReactedToPoke", "ReactedToRobotOnBack", "ReactedToRobotOnFace", "ReactedToRobotOnSide", "ReactedToRobotShaken", "ReactedToUnexpectedMovement", "RequestedGame", "Singing", "StackedBlock", "Count" };
        for (uint i = 0; i < names.Length; i++) { Assert.Equal(names[i], FistBumpReactionStrategy.ObjectiveName(i)); Assert.Equal(i, FistBumpReactionStrategy.ObjectiveFromName(names[i])); }
        Assert.Null(FistBumpReactionStrategy.ObjectiveName(42));
        var diagnostics = new List<string>();
        foreach (string bad in new[] { "22", "poppedWheelie", " PoppedWheelie" }) Assert.Equal  (0u, FistBumpReactionStrategy.ObjectiveFromName(bad, diagnostics.Add));
        Assert.Equal(3, diagnostics.Count);
        int draws = 0;
        using var s = new FistBumpReactionStrategy(new[] { Row("Count"), Row("FoundFace"), Row("bad"), Row("Singing", -1), Row("LaserPounced", 0, 0), Row("LaserTracked", 0, 1.01), Row("GatheredCubes", 0, 1, 0) }, () => 1, () => { draws++; return 0; }, diagnostics.Add);
        foreach (uint id in new uint[] { 41, 39, 14, 15, 11 }) s.Receive(0x8B, id);
        Assert.Equal(0, draws);
        s.Receive(0x8B, 10u); s.Receive(0x8B, 0u); Assert.Equal(2, draws);
    }

    [Fact]
    public void LatchCooldownProbabilityAndResetUseFloatBoundaries()
    {
        // reach: unit; 0x60E8C0..98E, 0x60E9FA..EA14.
        float now = 0; int draws = 0; double draw = 0;
        using var s = new FistBumpReactionStrategy(new[] { Row("BuiltPyramid", 2, .2, 3) }, () => now, () => { draws++; return draw; });
        s.Receive(1, 3u); s.Receive(0x8B, 999u); Assert.Equal(0, draws);
        s.Receive(0x8B, 3u); Assert.True(s.Pending); Assert.Equal(3f, s.Expiry); Assert.Equal(1, draws);
        now = 1; draw = (double)(float).2; s.Receive(0x8B, 3u); Assert.Equal(2, draws); Assert.Equal(3f, s.Expiry);
        draw = 0; s.Receive(0x8B, 3u); Assert.Equal(4f, s.Expiry);
        s.ResetTrigger(false); Assert.False(s.Pending); Assert.Equal(0f, s.LastCompletion);
        s.ResetTrigger(true); Assert.Equal(1f, s.LastCompletion);
        now = 3; s.Receive(0x8B, 3u); Assert.Equal(3, draws);
        now = MathF.BitIncrement(3f); s.Receive(0x8B, 3u); Assert.Equal(4, draws); Assert.True(s.Pending);
        now = 0; s.ResetTrigger(true); s.Receive(0x8B, 3u); Assert.Equal(5, draws);
        Assert.False(s.ShouldResumeLast); Assert.False(s.CanInterruptOtherTriggeredBehavior); Assert.False(s.CanInterruptSelf);
    }

    [Fact]
    public void EvaluationPreservesOrClearsPendingAtNativeBoundariesAndListenerRetires()
    {
        // reach: unit with stand-in target; no production reaction claimed.
        using var rig = new M4ControlTests.Rig(); float now = 10;
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var target = M10Support.RunnableBehaviour(); var blocked = M10Support.RunnableBehaviour(runnable: false);
        using var s = new FistBumpReactionStrategy(new[] { Row("BuiltPyramid", 0, 1, 3) }, () => now, () => 0);
        Action<bool>? listener = null; s.BindListener(callback => listener = callback);
        s.Receive(0x8B, 3u); now = 13;
        typeof(Cozmo.Robot.OffTreadsClassifier).GetProperty("Current")!.SetValue(rig.Robot.Sensors.OffTreads, OffTreadsState.InAir);
        Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, now), target)); Assert.True(s.Pending);
        typeof(Cozmo.Robot.OffTreadsClassifier).GetProperty("Current")!.SetValue(rig.Robot.Sensors.OffTreads, OffTreadsState.OnTreads);
        Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, now), blocked)); Assert.True(s.Pending);
        Assert.True(s.ShouldTriggerBehavior(new ReactionContext(context, now), target)); Assert.True(s.Pending);
        s.EnabledStateChanged(context, false); Assert.True(s.Pending);
        Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, now, isRunning: _ => true), target)); Assert.False(s.Pending);
        s.Receive(0x8B, 3u); now = MathF.BitIncrement(16f);
        Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, now), target)); Assert.False(s.Pending);
        now = 20; s.Receive(0x8B, 3u); listener!(true); Assert.False(s.Pending); Assert.Equal(20f, s.LastCompletion);
        s.Dispose(); now = 25; listener(true); s.Receive(0x8B, 3u); Assert.Equal(20f, s.LastCompletion); Assert.False(s.Pending);
    }

    private static string Resources()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            string root = Path.Combine(d.FullName, "re-analysis", "obb", "assets", "cozmo_resources");
            if (File.Exists(Path.Combine(root, ReactionTriggerMap.RelativePath))) return root;
        }
        throw new DirectoryNotFoundException("shipped behavior resources");
    }
    [Fact]
    public void ActualTargetRunningByteWinsOverManagerIdentityProbe()
    {
        // reach: unit, real SteppedBehavior +0xA1 with deliberately conflicting manager probe (0x60E800).
        using var rig = new M4ControlTests.Rig();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var target = new WaitBehavior();
        using var strategy = new FistBumpReactionStrategy(new[] { Row("BuiltPyramid", 0, 1) }, () => 1, () => 0);
        strategy.Receive(0x8B, 3u);
        Assert.False(target.EngineRunning);
        strategy.ShouldTriggerBehavior(new ReactionContext(context, 1, isRunning: _ => true), target);
        Assert.True(strategy.Pending);
        target.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.True(target.EngineRunning);
        Assert.False(strategy.ShouldTriggerBehavior(new ReactionContext(context, 1, isRunning: _ => false), target));
        Assert.False(strategy.Pending);
        target.Stop(BehaviorStopReason.Completed);
    }
    [Fact]
    public void ShippedSixObjectiveRowsFeedNativeValues()
    {
        // reach: unit over shipped data, not a live reaction.
        var row = Assert.Single(ReactionTriggerMap.Load(Resources()).Where(r => r.Trigger == ReactionTrigger.FistBump));
        var expected = new[] { ("StackedBlock", 40u, 180d, .2d, 3d), ("BuiltPyramid", 3u, 180d, .5d, 3d),
            ("InteractedWithFace", 12u, 300d, .75d, 3d), ("PeekABooSuccess", 17u, 300d, .2d, 3d),
            ("PerformedStrongWorkout", 19u, 180d, .5d, 3d), ("PoppedWheelie", 22u, 180d, .2d, 6d) };
        Assert.Equal(6, row.FistBumpObjectiveParams.Count);
        foreach (var (name, id, cooldown, probability, expiration) in expected)
        {
            var item = Assert.Single(row.FistBumpObjectiveParams.Where(p => p.BehaviorObjective == name));
            Assert.Equal((cooldown, probability, expiration), (item.TriggerCooldownTimeSec, item.TriggerProbability, item.TriggerExpirationSec));
            int draws = 0; using var strategy = new FistBumpReactionStrategy(new[] { item }, () => 10f, () => { draws++; return 0; });
            strategy.Receive(0x8B, id); Assert.Equal(1, draws); Assert.Equal(10f + (float)expiration, strategy.Expiry);
        }
    }
    [Fact]
    public void CanonicalRobotLeavesUnsafeTargetUnboundAcrossObjectiveAndRebinding()
    {
        // reach: actual CozmoRobot construction and local objective producer; availability gap, not live behavior.
        var missing = new List<string>(); SteppedBehavior.ResetMissingForTests(); SteppedBehavior.MissingReported += missing.Add;
        M4ControlTests.Rig rig;
        try { rig = new M4ControlTests.Rig(Resources()); rig.ToSynced(); }
        finally { SteppedBehavior.MissingReported -= missing.Add; }
        using var ownedRig = rig;
        Assert.Contains(missing, m => m.Contains("M7-018 FistBump configured target"));
        var native = rig.Engine.Robot!; var manager = native.BehaviorManagerOwner!; var runtime = native.NativeBehaviorRuntime!;
        Assert.Contains(ReactionTriggerMap.Load(Resources()), r => r.Trigger == ReactionTrigger.FistBump && r.BehaviorId == "FistBump");
        Assert.IsType<FistBumpBehavior>(runtime.Bound["FistBump"]);
        Assert.DoesNotContain(manager.Reactions, r => r.Strategy.Trigger == ReactionTrigger.FistBump);
        int before = rig.Engine.AnimationMessages.SubscriberCountForTest(0x8B, true);
        rig.Engine.ReportBehaviorObjective(3, "BuiltPyramid", "fixture");
        manager.BindReactionDependencies(runtime.Manipulation, runtime.Bound);
        Assert.DoesNotContain(manager.Reactions, r => r.Strategy.Trigger == ReactionTrigger.FistBump);
        Assert.Equal(before, rig.Engine.AnimationMessages.SubscriberCountForTest(0x8B, true));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FutureTargetSubscriptionRespectsExternalInterfaceAndDisposes(bool external)
    {
        // reach: unit; test target only. Constructor gate 0x60B4B0 and base destructor 0x60B702.
        using var rig = new M4ControlTests.Rig(); rig.ToSynced(); var native = rig.Engine.Robot!;
        native.HasExternalInterface = external;
        int before = rig.Engine.AnimationMessages.SubscriberCountForTest(0x8B, true);
        Action<bool>? listener = null;
        var strategy = FistBumpReactionStrategy.CreateForTarget(native, new[] { Row("BuiltPyramid", 0, 1) }, l => listener = l);
        Assert.Equal(before + (external ? 1 : 0), rig.Engine.AnimationMessages.SubscriberCountForTest(0x8B, true));
        rig.Engine.AnimationMessages.Deliver(0x8B, true, 3u);
        Assert.Equal(external, strategy.Pending);
        strategy.Dispose(); listener!(true);
        Assert.Equal(before, rig.Engine.AnimationMessages.SubscriberCountForTest(0x8B, true));
    }

    [Fact]
    public void MapRowDoesNotLeaveAnOrphanAtConstruction()
    {
        // reach: real constructors with matched minimal maps; no body is supplied in either fixture.
        string root = Path.Combine(Path.GetTempPath(), "fist-reaction-" + Guid.NewGuid().ToString("N"));
        string empty = Path.Combine(root, "empty"), fist = Path.Combine(root, "fist");
        Directory.CreateDirectory(empty); Directory.CreateDirectory(fist);
        try
        {
            File.WriteAllText(Path.Combine(empty, ReactionTriggerMap.FileName), "{\"reactionTriggerBehaviorMap\":[]}");
            File.WriteAllText(Path.Combine(fist, ReactionTriggerMap.FileName), "{\"reactionTriggerBehaviorMap\":[{\"reactionTrigger\":\"FistBump\",\"behaviorID\":\"FistBump\"}]}");
            Assert.Contains(ReactionTriggerMap.Load(fist), row => row.Trigger == ReactionTrigger.FistBump);
            using var a = new M4ControlTests.Rig(empty); using var b = new M4ControlTests.Rig(fist);
            a.ToSynced(); b.ToSynced();
            Assert.True(a.Engine.Robot!.HasExternalInterface); Assert.True(b.Engine.Robot!.HasExternalInterface);
            Assert.Equal(a.Engine.AnimationMessages.SubscriberCountForTest(0x8B, true), b.Engine.AnimationMessages.SubscriberCountForTest(0x8B, true));
            Assert.DoesNotContain(b.Engine.Robot!.BehaviorManagerOwner!.Reactions, r => r.Strategy.Trigger == ReactionTrigger.FistBump);
        }
        finally { Directory.Delete(root, true); }
    }
}
