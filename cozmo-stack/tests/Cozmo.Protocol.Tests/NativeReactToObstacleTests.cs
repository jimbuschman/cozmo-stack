using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeReactToObstacleTests
{
    [Theory]
    [InlineData(false,0u)] [InlineData(true,0x03000018u)]
    public void ExplicitStartUsesOriginalConfiguredConcreteActionDespiteFalseStrategy(bool carrying,uint result)
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var react=Assert.IsType<PlayAnimBehavior>(f.Runtime.Bound["ReactToObstacle"]);var manager=f.Robot.BehaviorManagerOwner!;
        Assert.Same(react,manager.Find("ReactToObstacle"));Assert.Equal(417,(int)Assert.Single(react.Triggers));
        Assert.False(f.Runtime.AI.ObstacleDetected);Assert.False(react.EvaluateHostReadiness(f.Runtime.Context).Runnable);
        if(carrying)f.Robot.CarryingOwner!.SetCarrying(7);
        Assert.True(manager.StartAsync(react.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var action=Assert.IsType<TriggerLiftSafeAnimationAction>(react.OwnedRunner);
        Assert.Equal(417,(int)action.Request.Trigger);Assert.Equal(1,action.Request.Argument2);Assert.True(action.Request.Argument3);
        Assert.Equal(0,action.Request.Argument4);Assert.Equal(60f,action.Request.Argument5);Assert.False(action.Request.Argument6);
        Assert.Equal(carrying?2u:0u,action.RequiredTrackMask);
        action.State=result;f.Robot.ActionList.Update(); // supplied terminal result, not firmware completion
        Assert.Null(react.OwnedRunner);Assert.True(react.EngineRunning);Assert.False(react.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF*1000));
        Assert.Null(react.OwnedRunner);manager.Stop(BehaviorStopReason.Completed,f.Rig.Engine.Timer.SecondsF);
        using var scope=new BehaviorScope();react.AttachRun(f.Runtime.Context,scope);Assert.True(react.Resume(null,f.Rig.Engine.Timer.SecondsF));Assert.Null(react.OwnedRunner);
    }
    [Fact]
    public void PositiveAiInputIsAnExplicitSeamAndActualChooserUsesSameOwner()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var react=Assert.IsType<PlayAnimBehavior>(f.Runtime.Bound["ReactToObstacle"]);
        Assert.False(react.IsRunnable(f.Runtime.Context));f.Runtime.AI.ObstacleDetected=true; // supplied input; not a detector claim
        Assert.True(react.IsRunnable(f.Runtime.Context));var chooser=new ScoringChooser(new[]{react.NativeScoring!},f.Runtime.Bound,native:true);
        var decision=chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        Assert.False(decision.InputUnavailable);Assert.Same(react,decision.Behavior);
    }
    [Fact]
    public void StopAndReplacementDoNotDeliverAStaleCompletion()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var original=f.Robot;var react=Assert.IsType<PlayAnimBehavior>(f.Runtime.Bound["ReactToObstacle"]);var manager=original.BehaviorManagerOwner!;
        Assert.True(manager.StartAsync(react.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var action=Assert.IsAssignableFrom<ActionRunner>(react.OwnedRunner);uint tag=action.Tag;
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);Assert.Null(react.OwnedRunner);
        f.Rig.Engine.Robots.RemoveRobot(1,false);var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
        var next=Assert.IsType<PlayAnimBehavior>(replacement.NativeBehaviorRuntime!.Bound["ReactToObstacle"]);int mark=f.Rig.Mark();
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(tag,action.Type,0,Array.Empty<uint>(),0));
        Assert.Null(react.OwnedRunner);Assert.Null(next.OwnedRunner);Assert.False(next.EngineRunning);Assert.Equal(mark,f.Rig.Mark());
    }
    [Fact]
    public void ChangedTriggerVectorIsUnavailableInsteadOfUsingLegacyPath()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var react=Assert.IsType<PlayAnimBehavior>(f.Runtime.Bound["ReactToObstacle"]);
        react.ReplaceTriggers(new[]{(AnimationTrigger)417,(AnimationTrigger)417}); // supplied changed-profile seam
        var ready=react.EvaluateHostReadiness(f.Runtime.Context);Assert.False(ready.Available);Assert.False(ready.Runnable);
        Assert.False(f.Robot.BehaviorManagerOwner!.StartAsync(react.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());Assert.Null(react.OwnedRunner);
    }
    [Fact]
    public void ConstructorDiagnosticReentrantStopCannotAdmitAction()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var react=Assert.IsType<PlayAnimBehavior>(f.Runtime.Bound["ReactToObstacle"]);bool stopped=false;
        void OnMissing(string text){if(stopped || !text.Contains("M8-021 trigger DAS"))return;stopped=true;react.Stop(BehaviorStopReason.Interrupted);}
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=OnMissing;
        try {using var scope=new BehaviorScope();react.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();}
        finally {SteppedBehavior.MissingReported-=OnMissing;}
        Assert.True(stopped);Assert.False(react.EngineRunning);Assert.Null(react.OwnedRunner);
    }
    [Fact]
    public void NoExternalInterfaceDoesNotInventOwnInitGate()
    {
        using var resources=new Fixture();using var rig=new M4ControlTests.Rig(resources.Rig.Engine.Options.ResourcesPath);
        rig.Engine.BeforeDerivedConstructionForTest=owner=>owner.HasExternalInterface=false;
        rig.Robot.Animations.LoadFrom(rig.Engine.Options.ResourcesPath!);rig.ToSynced();rig.Calibrate();rig.State();
        var owner=rig.Engine.Robot!;Assert.False(owner.HasExternalInterface);
        var react=Assert.IsType<PlayAnimBehavior>(owner.NativeBehaviorRuntime!.Bound["ReactToObstacle"]);
        Assert.True(owner.BehaviorManagerOwner!.StartAsync(react.Id,rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Equal(417,(int)Assert.IsType<TriggerLiftSafeAnimationAction>(react.OwnedRunner).Request.Trigger);
    }
}
