using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class AiFreeplayControlTests
{
    [Fact]
    public void DisableSelectsSelectionAndRequestsIndefiniteWait()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var manager = f.Robot.BehaviorManagerOwner!;
        var selectionActivity = manager.Activity;
        var selection = Assert.IsType<SelectionChooser>(f.Runtime.Tree.Single(a => a.Id == "Selection").Chooser);
        f.Rig.Robot.SetEnableFreeplayActivity(true);
        f.Rig.Tick();
        Assert.Same(f.Runtime.FreeplayObserver, manager.Activity);
        Assert.NotSame(selectionActivity, manager.Activity);

        f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.Rig.Tick();
        Assert.NotSame(f.Runtime.FreeplayObserver, manager.Activity);
        Assert.Same(selection.Wait, selection.Requested);
        Assert.Equal(-1, selection.NumRuns);
        Assert.Same(selection.Wait, manager.Current);
    }

    [Fact]
    public async Task OutsideAnimationLeavesFreeplayActiveThroughCompletion()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        f.Rig.Robot.SetEnableFreeplayActivity(true);
        f.Rig.Tick();
        Assert.Same(f.Runtime.FreeplayObserver, manager.Activity);
        var freeplay = manager.Activity;

        var earlier = Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction());
        var request = f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.CozmoSaysSpeakLoop);
        Assert.Same(earlier, f.Robot.ActionList.GetCurrentAction());
        var runner = Assert.Single(f.Robot.ActionList.QueueAt(0)!.Pending
            .OfType<TriggerAnimationAction>(), a => a.AiNativeEnded is not null);
        Assert.False(request.IsCompleted);
        // End this competing behavior so its repeated Now requests cannot take the queue's front.
        manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
        Assert.Same(runner, f.Robot.ActionList.GetCurrentAction());
        f.Robot.ActionList.Update();
        Assert.True(runner.Initialized);
        Assert.Same(freeplay, manager.Activity);
        // Supply a terminal result to exercise the real action-list delivery. Firmware playback is
        // covered independently by AiAuthoredAnimationTests.
        runner.State = EngineActionResult.Success;
        f.Robot.ActionList.Update();

        var result = await request.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(0u, result.EngineResult);
        f.Rig.Tick();
        Assert.Same(freeplay, manager.Activity);
    }
}
