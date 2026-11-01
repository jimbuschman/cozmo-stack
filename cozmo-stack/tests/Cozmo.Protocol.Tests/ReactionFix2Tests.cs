using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class ReactionFix2Tests
{
    // Host ownership: a missing or nonmatching map must not retain an eagerly constructed charger hook.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnregisteredChargerFactoryHookIsReleased(bool omitted)
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var field = typeof(CozmoSensors).GetField("ChargerEventInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        int Count() => ((Delegate?)field.GetValue(rig.Robot.Sensors))?.GetInvocationList()
            .Count(d => d.Target is PlacedOnChargerStrategy) ?? 0;
        int before = Count();
        string root = Path.Combine(Path.GetTempPath(), "reaction-fix-" + Guid.NewGuid());
        try
        {
            if (omitted)
            {
                string path = Path.Combine(root, ReactionTriggerMap.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "{\"reactionTriggerBehaviorMap\":[{\"reactionTrigger\":\"RobotPickedUp\",\"behaviorID\":\"ReactToPickup\"}]}");
            }
            var registrations = ShippedBehaviors.Reactions(rig.Robot, obbRoot: root);
            if (omitted) Assert.Equal(ReactionTrigger.RobotPickedUp, Assert.Single(registrations).Strategy.Trigger);
            else Assert.Empty(registrations);
            Assert.DoesNotContain(registrations, r => r.Strategy.Trigger == ReactionTrigger.PlacedOnCharger);
            Assert.Equal(before, Count());
            foreach (var strategy in registrations.Select(r => r.Strategy).OfType<IDisposable>()) strategy.Dispose();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    // Unit: synthetic robot messages, actual sensor handlers and shipped strategy factory.
    // 0x60D6BE..CE subscribes tags 34 and 52; 0x60DC76 filters both without reading payload.
    [Fact]
    public void CliffAndStoppedFeedSameFilteredLatchAndDisposeUnhooksBoth()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        using var manager = new BehaviorManager(context);
        var strategy = ShippedReactionStrategies.Cliff(rig.Robot);
        strategy.Manager = manager;
        rig.Robot.Sensors.SetCliffSensorEnabled(true);
        rig.Data(new RobotStopped { Field0 = 7 }); rig.Tick();
        Assert.True(strategy.WantsToRun(rig.Robot));
        rig.Data(new CliffEvent { Timestamp = 4, DetectedFlags = 0 }); rig.Tick();
        Assert.True(strategy.WantsToRun(rig.Robot));
        strategy.Dispose();
        rig.Data(new RobotStopped { Field0 = 8 }); rig.Tick();
        rig.Data(new CliffEvent { Timestamp = 5, DetectedFlags = 0 }); rig.Tick();
        Assert.False(strategy.WantsToRun(rig.Robot));
    }

    // Unit: state packets are synthetic; SetOnCharger raises the real internal event. Handler 0x6144B8 copies
    // the byte into +0x15; WantsToRun 0x614474 clears it each call after checking the first-call deadline.
    [Fact]
    public void ChargerFactoryReceivesBothEdgesAndDisposeUnhooks()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var strategies = ShippedReactionStrategies.ForRobot(rig.Robot);
        var strategy = Assert.IsType<PlacedOnChargerStrategy>(strategies
            .Single(s => s.Trigger == ReactionTrigger.PlacedOnCharger));
        Assert.False(strategy.WantsToRun(0));
        rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger)); rig.Tick();
        Assert.True(strategy.WantsToRun(21));
        rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode)); rig.Tick();
        Assert.False(strategy.WantsToRun(22));
        strategy.Dispose();
        rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger)); rig.Tick();
        Assert.False(strategy.WantsToRun(23));
        foreach (var disposable in strategies.OfType<IDisposable>()) disposable.Dispose();
    }
}
