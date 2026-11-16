using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class DockSchedulerNativeTests
{
    [Theory]
    [InlineData("success", 0u)]
    [InlineData("failure", 0x04000003u)]
    [InlineData("cancel", EngineActionResult.Cancelled)]
    public void DockTicksAndRetiresNativeOwnersInWireOrder(string outcome, uint expected)
    {
        using var scene = new PopFirmwareScene();
        var f = scene.F;
        var dock = f.Robot.DockingOwner!;
        var action = new NativePopAWheelieRunner(f.M, scene.Target)
        {
            CheckPreActionPose = false,
            ScopedOwner = f.Robot
        };
        var completed = new List<RobotCompletedAction>();
        using var results = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A, true, payload =>
        {
            if (payload is RobotCompletedAction result && result.Tag == action.Tag) completed.Add(result);
        });
        int start = f.Rig.Mark();
        Assert.Equal(0, f.Robot.ActionList.QueueAction(QueueActionPosition.Now, action));
        bool DockSent() => f.Rig.SentSince(start).OfType<DockWithObject>().Any();
        for (int i = 0; i < 30 && !DockSent(); i++) scene.Step(DockSent);
        Assert.Equal(9, Assert.Single(f.Rig.SentSince(start).OfType<DockWithObject>()).DockAction);
        Assert.Equal(scene.Target, dock.TargetId);

        // 0x00552344..0x00552398 requires the active edge before verification is allowed.
        scene.Motors();
        Assert.Empty(completed);
        scene.Motors(RobotStatusFlag.IsPickingOrPlacing);
        Assert.Equal(1, dock.Active);
        Assert.True(f.Robot.BehaviorManagerOwner!.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, "dockActions"));
        f.M.World.SelectedObjectId = scene.Target;

        if (outcome == "cancel")
        {
            // With a path active during destruction, 0x00557464..0x0055747C clears that path,
            // then aborts the still-active dock. The action never consumes a synthetic final result.
            var path = f.M.Paths.ExecuteWithResult(new[] { new PathSegment.Line(0, 0, 30, 0, 25, 50, 50) });
            Assert.Equal(0, path.Result);
            Assert.True(f.Rig.Robot.Motion.Path.IsActive);
            int cancelStart = f.Rig.Mark();
            f.Robot.ActionList.Cancel(action.Tag);
            var cleanup = f.Rig.SentSince(cancelStart).Where(m => m is ClearPath or AbortDocking).ToArray();
            Assert.Collection(cleanup, m => Assert.IsType<ClearPath>(m), m => Assert.IsType<AbortDocking>(m));
            Assert.False(f.Rig.Robot.Motion.Path.IsActive);
            Assert.Equal(1, dock.Active); // 0x0063BE10 sends an abort without changing the firmware mirror.
        }
        else
        {
            scene.Pitch = 1.2f;
            f.Rig.Data(new PickAndPlaceResult { Field1 = outcome == "success", Field3 = 0 });
            // MovementComponent+9 is status bit 0, even with wheels stationary (0x0063E31A).
            scene.Motors(RobotStatusFlag.IsMoving);
            Assert.False(f.Rig.Robot.Motion.BodyMoving);
            Assert.Equal(0, dock.Active);
            Assert.Empty(completed);
            Assert.Equal(scene.Target, dock.TargetId);

            // With bit 0 clear, wheels by themselves do not hold verification.
            scene.Motors(RobotStatusFlag.AreWheelsMoving);
            Assert.Empty(f.Rig.SentSince(start).OfType<AbortDocking>());
            Assert.Equal(outcome == "success", dock.Success);
        }

        Assert.Equal(expected, Assert.Single(completed).Result);
        Assert.True(action.DestructionObserved);
        Assert.Null(f.M.World.SelectedObjectId);
        Assert.Null(dock.TargetId);
        Assert.Equal(3, dock.MarkerCode);
        Assert.False(f.Robot.BehaviorManagerOwner.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, "dockActions"));
        byte retiredLoad = action.Action.LiftLoadState;
        f.Rig.Data(new LiftLoad { Field0 = retiredLoad != 1 });
        f.Rig.Tick();
        Assert.Equal(retiredLoad, action.Action.LiftLoadState); // +0xC2 subscriber is gone after 0x00557590.
    }
}
