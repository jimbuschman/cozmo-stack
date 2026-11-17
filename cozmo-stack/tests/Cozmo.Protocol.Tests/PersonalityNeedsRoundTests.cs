using System.Net;
using Cozmo.Conformance;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PersonalityNeedsRoundTests
{
    private static M15CubeRun Observe(Fixture f)
    {
        var run = new M15CubeRun("", "", IPAddress.Loopback, "", [], [], false, false) { Personality = true };
        run.SubscribeBehaviorReports(f.Rig.Engine);
        return run;
    }

    private static float[] Levels(Fixture f) => Enum.GetValues<NeedId>()
        .Select(id => (float)f.Rig.Engine.Needs.State.GetNeedLevel(id)).ToArray();

    private static IBehavior ChooseSocializeInterlude(Fixture f)
    {
        var social = f.Runtime.FreeplayObserver.Freeplay.SubActivities.Single(a => a.Id == "Socialize");
        var chooser = Assert.IsType<StrictPriorityChooser>(social.InterludeChooser);
        var decision = chooser.GetDesiredActiveBehavior(null, 0, f.Runtime.Context, f.Rig.Engine.Timer.SecondsF);
        Assert.False(decision.InputUnavailable);
        return Assert.IsAssignableFrom<IBehavior>(decision.Behavior);
    }

    [Theory]
    [InlineData("express-needs", BehaviorID.Needs_MildLowEnergyRequest, NeedBracketId.Warning)]
    [InlineData("needs-change-animation", BehaviorID.Needs_SevereLowEnergyGetIn, NeedBracketId.Critical)]
    [InlineData("drive-in-desperation", BehaviorID.Needs_SevereLowEnergyState, NeedBracketId.Critical)]
    public void LevelOverrideStartsConfiguredBehaviorAndRestoresPriorValues(string stepId, BehaviorID target, NeedBracketId bracket)
    {
        using var f = new Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var run = Observe(f);
        var original = Levels(f);
        var step = Assert.Single(PersonalityHw.SelectSteps(stepId));
        Assert.Equal(target, step.Check!.Id);
        var levels = Assert.IsType<PersonalityHw.NeedsLevels>(step.Needs);
        var manager = f.Robot.BehaviorManagerOwner!;
        levels.WithOverride(f.Rig.Engine, () =>
        {
            Assert.Equal(bracket, f.Rig.Engine.Needs.State.GetNeedBracket(NeedId.Energy));
            if (bracket == NeedBracketId.Warning)
            {
                // Expire the configured request cooldown, then let the genuine interlude choose.
                f.Rig.Engine.Timer.UpdateTime(75_000_000_000L);
                var selected = ChooseSocializeInterlude(f);
                Assert.Equal(target.ToString(), selected.Id);
                Assert.True(manager.StartAsync(selected.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            }
            else
            {
                f.Rig.Robot.SetEnableFreeplayActivity(true);
                var getIn = Assert.IsType<NativeNeedsGetInBehavior>(f.Runtime.Bound["Needs_SevereLowEnergyGetIn"]);
                for (int i = 0; i < 40 && !ReferenceEquals(manager.Current, getIn); i++) f.Rig.Tick(50);
                Assert.Same(getIn, manager.Current);
                if (target == BehaviorID.Needs_SevereLowEnergyState)
                {
                    // Simulate the GetIn animation reaching a terminal result through the action list.
                    var animation = Assert.IsType<TriggerLiftSafeAnimationAction>(getIn.OwnedRunner);
                    animation.State = 0x03000018;
                    f.Robot.ActionList.Update();
                    for (int i = 0; i < 30 && manager.Current?.Id != target.ToString(); i++) f.Rig.Tick(50);
                    Assert.Equal(target.ToString(), manager.Current?.Id);
                }
            }
            f.Rig.Engine.DrainAppDelivery();
            Assert.Contains(run.BehaviorTransitions(target, 0, double.MaxValue),
                e => PersonalityHw.IsBehaviorStart(e, target));
        });
        Assert.Equal(original, Levels(f));
    }

    [Fact]
    public void CancellationRestoresLevelsThroughTheSameMessage()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        var engine = f.Rig.Engine;
        engine.AnimationMessages.Deliver(0xC6, false, new[] { .71f, .86f, .92f });
        var original = Levels(f);
        var inputs = new List<float[]>();
        using var observed = engine.AnimationMessages.SubscribeMessage(0xC6, false,
            value => inputs.Add(Assert.IsAssignableFrom<IReadOnlyList<float>>(value).ToArray()));
        var levels = new PersonalityHw.NeedsLevels(1, .1f, 1);
        Assert.Throws<OperationCanceledException>(() => levels.WithOverride(engine, () => throw new OperationCanceledException()));
        Assert.Equal(original, Levels(f));
        Assert.Collection(inputs, target => Assert.Equal(new[] { 1f, .1f, 1f }, target),
            restore => Assert.Equal(original, restore));
    }

    [Fact]
    public async Task EarnedSparksUsesPendingRewardAndLeavesLevelsAlone()
    {
        using var f = new Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var run = Observe(f);
        var step = Assert.Single(PersonalityHw.SelectSteps("earned-sparks"));
        Assert.Null(step.Needs);
        var needs = f.Rig.Engine.Needs;
        var levels = new PersonalityHw.NeedsLevels(1, .1f, 1);
        levels.WithOverride(f.Rig.Engine, () =>
        {
            Assert.True(needs.TryGetNativePending(out bool pending));
            Assert.False(pending);
            Assert.False(Assert.IsType<NativeEarnedSparksBehavior>(f.Runtime.Bound["EarnedSparks"])
                .EvaluateHostReadiness(f.Runtime.Context).Runnable);
        });
        var original = Levels(f);
        // Supply the flag the shipped reward producer owns. This tests admission and reporting.
        needs.SupplyNativeRewardForTest(true, 4, "A");
        var earned = ChooseSocializeInterlude(f);
        Assert.Equal("EarnedSparks", earned.Id);
        Assert.True(await f.Robot.BehaviorManagerOwner!.StartAsync(earned.Id, f.Rig.Engine.Timer.SecondsF));
        f.Rig.Engine.DrainAppDelivery();
        Assert.Contains(run.BehaviorTransitions(BehaviorID.EarnedSparks, 0, double.MaxValue),
            e => PersonalityHw.IsBehaviorStart(e, BehaviorID.EarnedSparks));
        Assert.Equal(original, Levels(f));
    }
}
