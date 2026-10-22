using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public class M13DriveOffDrivingAnimationBindingTests
{
    private static BehaviorContext Ctx(Rig rig) => new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };

    // reach: unit (SevereNeeds substituted; production push/remove binding)
    // pre-B-M13-13 code (d67b73bd^): seams were null, so nothing was pushed and DrivingAnimationsFlag stayed 0;
    // the test fails on that code because the handler holds no entry and the flag is 0.
    [Fact]
    public void M13_017_SevereNeeds3_PushesTripletToRealHandlerBeforeStartReturns_AndFlagIs1_StopRemovesIt()
    {
        using var rig = new Rig();
        var b = ShippedBehaviors.Navigation(rig.M).OfType<DriveOffChargerBehavior>().Single(x => x.Id == "DriveOffCharger");
        b.SevereNeedsState = () => 3;
        var handler = rig.Robot.Animations.DrivingAnimations;
        var ctx = Ctx(rig);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Contains(handler.Entries, e => e.LockName == "DriveOffCharger"
            && e.Animations == new DrivingAnimations((AnimationTrigger)0x97, (AnimationTrigger)0x93, (AnimationTrigger)0x8F));
        Assert.Equal(1, b.DrivingAnimationsFlag);
        b.Stop(BehaviorStopReason.Completed);
        Assert.DoesNotContain(handler.Entries, e => e.LockName == "DriveOffCharger");
    }

    // reach: unit (SevereNeeds substituted; production push/remove binding)
    // pre-B-M13-13 code (d67b73bd^): seams were null, so nothing was pushed and the flag stayed 0;
    // the test fails on that code because (with the old null seam) Start could not push and the flag never set.
    [Fact]
    public void M13_017_SevereNeeds2_DoesNotPushAndFlagStays0_StopRemovesNothing()
    {
        using var rig = new Rig();
        var b = ShippedBehaviors.Navigation(rig.M).OfType<DriveOffChargerBehavior>().Single(x => x.Id == "DriveOffCharger");
        b.SevereNeedsState = () => 2;
        var handler = rig.Robot.Animations.DrivingAnimations;
        var before = handler.Entries.Count;
        var ctx = Ctx(rig);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.DoesNotContain(handler.Entries, e => e.LockName == "DriveOffCharger");
        Assert.Equal(0, b.DrivingAnimationsFlag);
        b.Stop(BehaviorStopReason.Completed);
        Assert.Equal(before, handler.Entries.Count);
    }
}