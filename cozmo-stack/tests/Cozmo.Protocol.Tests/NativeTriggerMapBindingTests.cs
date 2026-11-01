using System.Reflection;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeTriggerMapBindingTests
{
    [Fact]
    public void LateLoadUpdatesOriginalContextForNewRequestsButNotConstructedAction()
    {
        using var f=new Fixture();var context=f.Runtime.Context;var before=context.Triggers;
        Assert.False(before.IsAvailable);
        Assert.Null(before.GroupFor(AnimationTrigger.HikingIntro));
        var oldAction=new TriggerLiftSafeAnimationAction(f.Rig.Robot,new(AnimationTrigger.HikingIntro,1,true,0,60f,false));
        var group=typeof(TriggerAnimationAction).GetField("_group",BindingFlags.Instance|BindingFlags.NonPublic)!;
        Assert.Null(group.GetValue(oldAction));
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        Assert.NotSame(before,context.Triggers);Assert.Same(f.Rig.Robot.Animations.DrivingTriggerMap,context.Triggers);
        Assert.True(context.Triggers.IsAvailable);
        Assert.Equal("ag_hiking_getin",context.Triggers.GroupFor(AnimationTrigger.HikingIntro));
        Assert.Null(group.GetValue(oldAction));
        var newAction=new TriggerLiftSafeAnimationAction(f.Rig.Robot,new(AnimationTrigger.HikingIntro,1,true,0,60f,false));
        Assert.Equal("ag_hiking_getin",group.GetValue(newAction));
        var loaded=context.Triggers;f.Rig.Robot.Animations.DrivingTriggerMap=null;
        Assert.False(context.Triggers.IsAvailable);Assert.Null(context.Triggers.GroupFor(AnimationTrigger.HikingIntro));
        Assert.Contains("unavailable",context.Triggers.Resolve(AnimationTrigger.HikingIntro,f.Rig.Robot.Animations.Library!).Problem);
        f.Rig.Robot.Animations.DrivingTriggerMap=loaded;
        Assert.True(context.Triggers.IsAvailable);
        // The shipped AnimationTriggerMap.json omits ReactToMotorCalibration (M7-002 map inventory).
        Assert.Contains("names no group",context.Triggers.Resolve(AnimationTrigger.ReactToMotorCalibration,f.Rig.Robot.Animations.Library!).Problem);
        oldAction.RetireNeverAdmittedTree();newAction.RetireNeverAdmittedTree();
    }
    [Fact]
    public void ExplicitStandaloneMapRemainsStatic()
    {
        using var f=new Fixture();var map=new AnimationTriggerMap();
        var context=new BehaviorContext{Robot=f.Rig.Robot,Triggers=map};
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        Assert.Same(map,context.Triggers);Assert.Null(context.Triggers.GroupFor(AnimationTrigger.HikingIntro));
        Assert.True(context.Triggers.IsAvailable);
    }
}
