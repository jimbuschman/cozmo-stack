using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeNeedsExpressionAdmissionTests
{
    private static NativeNeedsGetInBehavior Get(Fixture f,string id)=>Assert.IsType<NativeNeedsGetInBehavior>(f.Runtime.Bound[id]);
    [Fact]
    public void ActualFreeplayPrioritizesCriticalRepairOverCriticalEnergy()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Repair,0.01);f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Energy,0.01);
        Assert.True(f.Rig.Engine.Needs.State.IsNeedAtBracket(NeedId.Repair,NeedBracketId.Critical));
        Assert.True(f.Rig.Engine.Needs.State.IsNeedAtBracket(NeedId.Energy,NeedBracketId.Critical));
        f.Rig.Robot.ActivateHighLevelActivity("Freeplay");var expected=Get(f,"Needs_SevereLowRepairGetIn");
        for(int i=0;i<40 && !ReferenceEquals(f.Robot.BehaviorManagerOwner!.Current,expected);i++)f.Rig.Tick(50);
        Assert.Equal("NeedsSevereLowRepair",f.Runtime.FreeplayObserver.Current?.Id);Assert.Same(expected,f.Robot.BehaviorManagerOwner!.Current);
    }
    [Theory]
    [InlineData(NeedId.Repair,"NeedsSevereLowRepair","Needs_SevereLowRepairGetIn")]
    [InlineData(NeedId.Energy,"NeedsSevereLowEnergy","Needs_SevereLowEnergyGetIn")]
    [InlineData(NeedId.Play,"NeedsSevereLowPlayGetIn","Needs_SevereLowPlayGetIn")]
    public void ActualFreeplaySelectsShippedSevereActivityAndGetIn(NeedId need,string activityId,string behaviorId)
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var trace=new List<RobotCompletedAction>();using var completed=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction a)trace.Add(a);});
        f.Rig.Engine.Needs.State.SetNeedLevel(need,0.01);
        Assert.True(f.Rig.Engine.Needs.State.IsNeedAtBracket(need,NeedBracketId.Critical));
        var getIn=Get(f,behaviorId);Assert.Same(getIn,f.Robot.BehaviorManagerOwner!.Find(behaviorId));
        f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
        for(int i=0;i<40 && !ReferenceEquals(f.Robot.BehaviorManagerOwner.Current,getIn);i++)f.Rig.Tick(50);
        Assert.Equal(activityId,f.Runtime.FreeplayObserver.Current?.Id);Assert.Same(getIn,f.Robot.BehaviorManagerOwner.Current);
        var faceCompletion=Assert.Single(trace.Where(a=>a.ActionType==0x29)); Assert.NotEqual(faceCompletion.Tag,getIn.CurrentRunnerTag);
        Assert.IsType<TriggerLiftSafeAnimationAction>(getIn.OwnedRunner);
    }
    [Theory]
    [InlineData(NeedId.Energy,"Needs_SevereLowEnergyGetIn","Needs_SevereLowEnergyState")]
    [InlineData(NeedId.Repair,"Needs_SevereLowRepairGetIn","Needs_SevereLowRepairState")]
    public void CompletedGetInEntersActualSevereStateBeforeWait(NeedId need,string getInId,string stateId)
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var trace=new List<RobotCompletedAction>();using var completed=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction a)trace.Add(a);});
        f.Rig.Engine.Needs.State.SetNeedLevel(need,0.01);f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
        var getIn=Get(f,getInId);for(int i=0;i<40 && !ReferenceEquals(f.Robot.BehaviorManagerOwner!.Current,getIn);i++)f.Rig.Tick(50);
        Assert.Same(getIn,f.Robot.BehaviorManagerOwner!.Current);
        // The face completion above is real; only the animation terminal failure below is supplied.
        var faceCompletion=Assert.Single(trace.Where(a=>a.ActionType==0x29)); Assert.NotEqual(faceCompletion.Tag,getIn.CurrentRunnerTag);
        var animation=Assert.IsType<TriggerLiftSafeAnimationAction>(getIn.OwnedRunner);animation.State=0x03000018;f.Robot.ActionList.Update();
        var expected=Assert.IsType<NativeDriveInDesperationBehavior>(f.Runtime.Bound[stateId]);
        for(int i=0;i<30 && !ReferenceEquals(f.Robot.BehaviorManagerOwner.Current,expected);i++)f.Rig.Tick(50);
        Assert.Equal((int)need,f.Runtime.AI.SevereNeeds.Selected);Assert.Same(expected,f.Robot.BehaviorManagerOwner.Current);
    }
    [Fact]
    public void CompletedPlayGetInSetsPlayAndTheTransitionEndsWithoutNeedsWait()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var trace=new List<RobotCompletedAction>();using var completed=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction a)trace.Add(a);});
        f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Play,0.01);f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
        var getIn=Get(f,"Needs_SevereLowPlayGetIn");for(int i=0;i<40 && !ReferenceEquals(f.Robot.BehaviorManagerOwner!.Current,getIn);i++)f.Rig.Tick(50);
        Assert.Same(getIn,f.Robot.BehaviorManagerOwner!.Current);
        var faceCompletion=Assert.Single(trace.Where(a=>a.ActionType==0x29)); Assert.NotEqual(faceCompletion.Tag,getIn.CurrentRunnerTag);
        var animation=Assert.IsType<TriggerLiftSafeAnimationAction>(getIn.OwnedRunner);animation.State=0x03000018;f.Robot.ActionList.Update();
        for(int i=0;i<30 && f.Runtime.FreeplayObserver.Current?.Id=="NeedsSevereLowPlayGetIn";i++)f.Rig.Tick(50);
        Assert.Equal((int)NeedId.Play,f.Runtime.AI.SevereNeeds.Selected);
        Assert.NotEqual("NeedsSevereLowPlayGetIn",f.Runtime.FreeplayObserver.Current?.Id);
        Assert.NotSame(f.Runtime.Bound["Needs_Wait"],f.Robot.BehaviorManagerOwner.Current);
    }
    [Fact]
    public void SelectedSnapshotStillRefreshesBracketWhenAlreadySelected()
    {
        using var f=new Fixture();var getIn=Get(f,"Needs_SevereLowPlayGetIn");
        f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Play,0.01);f.Rig.Engine.Needs.SevereNeeds.Set(NeedId.Play);
        int calls=0;getIn.BeforeBracketForTest=()=>calls++;
        Assert.False(getIn.IsRunnable(f.Runtime.Context));Assert.Equal(1,calls);
    }
    [Fact]
    public void FailedFaceStillStartsLiftSafeAnimationAndStopSetsActualSevereOwner()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Play,0.01);f.Rig.Engine.Needs.SevereNeeds.Clear();
        var getIn=Get(f,"Needs_SevereLowPlayGetIn");using var scope=new BehaviorScope();
        getIn.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        Assert.Equal(3,f.Runtime.AI.SevereNeeds.Selected);
        // Supplied terminal face failure, delivered through the real ActionList completion.
        var face=Assert.IsType<NativeLastFaceRunner>(getIn.OwnedRunner);face.State=0x03000018;f.Robot.ActionList.Update();
        var animation=Assert.IsType<TriggerLiftSafeAnimationAction>(getIn.OwnedRunner);
        Assert.Equal(AnimationTrigger.NeedsSevereLowPlayGetIn,animation.Request.Trigger);Assert.Equal(3,f.Runtime.AI.SevereNeeds.Selected);
        animation.State=0x03000018;f.Robot.ActionList.Update();Assert.Equal(3,f.Runtime.AI.SevereNeeds.Selected);
        getIn.Stop(BehaviorStopReason.Interrupted);Assert.Equal((int)NeedId.Play,f.Runtime.AI.SevereNeeds.Selected);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StopDoesNotSetWhenNeedRecoveredOrAlreadySelected(bool alreadySelected)
    {
        using var f=new Fixture();var getIn=Get(f,"Needs_SevereLowPlayGetIn");
        f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Play,0.01);f.Rig.Engine.Needs.SevereNeeds.Clear();
        using var scope=new BehaviorScope();getIn.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        if(alreadySelected)f.Rig.Engine.Needs.SevereNeeds.Set(NeedId.Play);else f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Play,1.0);
        int before=f.Runtime.AI.SevereNeeds.Selected;getIn.Stop(BehaviorStopReason.Interrupted);Assert.Equal(before,f.Runtime.AI.SevereNeeds.Selected);
    }
    [Fact]
    public void EnergyGetInStopUsesActualSevereDrivingAndReactionRecipients()
    {
        using var f=new Fixture();var getIn=Get(f,"Needs_SevereLowEnergyGetIn");
        f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Energy,0.01);f.Rig.Engine.Needs.SevereNeeds.Clear();
        using var scope=new BehaviorScope();getIn.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        getIn.Stop(BehaviorStopReason.Interrupted);
        Assert.Equal((int)NeedId.Energy,f.Runtime.AI.SevereNeeds.Selected);
        Assert.Contains(f.Rig.Robot.Animations.DrivingAnimations.Entries,e=>e.LockName=="severe_need_component_lock");
        Assert.True(f.Robot.BehaviorManagerOwner!.HasDisableLock(ReactionTrigger.RobotPickedUp,"severe_need_component_lock"));
    }
    [Fact]
    public void EmptyTriggerVectorFailsBeforeOwnPredicateAndResumeIsRefused()
    {
        using var f=new Fixture();var getIn=Get(f,"Needs_SevereLowPlayGetIn");
        var field=typeof(NativeNeedsGetInBehavior).GetField("_triggers",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        ((List<AnimationTrigger>)field.GetValue(getIn)!).Clear();int calls=0;getIn.BeforeBracketForTest=()=>calls++;
        Assert.False(getIn.IsRunnable(f.Runtime.Context));Assert.Equal(0,calls);
        using var scope=new BehaviorScope();getIn.AttachRun(f.Runtime.Context,scope);
        Assert.True(getIn.Resume(null,f.Rig.Engine.Timer.SecondsF));Assert.Null(getIn.OwnedRunner);
    }
    [Fact]
    public void LateOldFaceCompletionCannotStartAnimationAfterStop()
    {
        using var f=new Fixture();var getIn=Get(f,"Needs_SevereLowEnergyGetIn");using var scope=new BehaviorScope();
        getIn.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();uint tag=getIn.CurrentRunnerTag;
        getIn.Stop(BehaviorStopReason.Interrupted);
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(tag,0x29,0,Array.Empty<uint>(),0));
        Assert.Null(getIn.OwnedRunner);Assert.False(getIn.EngineRunning);
    }
    [Fact]
    public void ReplacedOwnerIgnoresLateOldTurnCompletion()
    {
        using var f=new Fixture();var getIn=Get(f,"Needs_SevereLowEnergyGetIn");using var scope=new BehaviorScope();
        getIn.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();uint tag=getIn.CurrentRunnerTag;
        var old=Assert.IsAssignableFrom<ActionRunner>(getIn.OwnedRunner);
        f.Rig.Engine.Robots.RemoveRobot(1,false);Assert.True(old.DestructionObserved);var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
        var next=Assert.IsType<NativeNeedsGetInBehavior>(replacement.NativeBehaviorRuntime!.Bound["Needs_SevereLowEnergyGetIn"]);
        // AbortAll may synchronously complete the face and construct a child before retirement.
        // The broader pending-survivor cleanup boundary remains M8-024.
        var retiredRunner=getIn.OwnedRunner;uint retiredTag=getIn.CurrentRunnerTag;int selected=f.Rig.Engine.Needs.SevereNeeds.Selected;
        int sent=f.Rig.SentSince(0).Count();
        var hub=f.Rig.Engine.AnimationMessages;
        var map=(System.Collections.IDictionary)hub.GetType().GetField("_e2g",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(hub)!;
        var signal=map[(ushort)0x5A]!;
        var nodes=(System.Collections.IEnumerable)signal.GetType().GetField("Nodes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public)!.GetValue(signal)!;
        Assert.DoesNotContain(nodes.Cast<object?>().OfType<Delegate>(),callback=>ReferenceEquals(callback.Target,getIn));
        // Supplied terminal completion after real owner replacement.
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(tag,0x29,0,Array.Empty<uint>(),0));
        if(retiredRunner is not null)
            f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(retiredTag,retiredRunner.Type,0,Array.Empty<uint>(),0));
        Assert.Same(retiredRunner,getIn.OwnedRunner);Assert.Equal(retiredTag,getIn.CurrentRunnerTag);
        Assert.Null(next.OwnedRunner);Assert.False(next.EngineRunning);Assert.Equal(selected,f.Rig.Engine.Needs.SevereNeeds.Selected);Assert.Equal(sent,f.Rig.SentSince(0).Count());
    }
    [Fact]
    public void ConfiguredGateAllowsCarryingButDeniesOffTreads()
    {
        using var f=new Fixture();var getIn=Get(f,"Needs_SevereLowPlayGetIn");
        f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Play,0.01);f.Rig.Engine.Needs.SevereNeeds.Clear();
        f.Robot.CarryingOwner!.SetCarrying(7);
        try
        {
            Assert.True(getIn.IsRunnable(f.Runtime.Context));
            getIn.RobotState355=()=>true;Assert.False(getIn.IsRunnable(f.Runtime.Context));
            getIn.RobotState355=()=>false;f.Rig.Robot.Sensors.SetOnChargerPlatform(true);
            Assert.True(f.Rig.Robot.Sensors.OnChargerPlatform);Assert.False(getIn.IsRunnable(f.Runtime.Context));
            f.Rig.Robot.Sensors.SetOnChargerPlatform(false);Assert.False(f.Rig.Robot.Sensors.OnChargerPlatform);Assert.True(getIn.IsRunnable(f.Runtime.Context));
        }
        finally {getIn.RobotState355=null;f.Rig.Robot.Sensors.SetOnChargerPlatform(false);f.Robot.CarryingOwner.UnsetCarrying();}
    }
    [Fact]
    public void ActualConstructorMissingCallbackCannotQueueAnimationAfterStop()
    {
        using var f=new Fixture();var getIn=Get(f,"Needs_SevereLowPlayGetIn");using var scope=new BehaviorScope();
        getIn.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        var face=Assert.IsType<NativeLastFaceRunner>(getIn.OwnedRunner);int callbacks=0;
        void Missing(string line){if(line.Contains("M8-021 trigger DAS") && callbacks++==0)getIn.Stop(BehaviorStopReason.Interrupted);}
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Missing;
        try
        {
            face.State=0x03000018;f.Robot.ActionList.Update();
            Assert.Equal(1,callbacks);Assert.False(getIn.EngineRunning);Assert.Null(getIn.OwnedRunner);
        }
        finally {SteppedBehavior.MissingReported-=Missing;}
    }
    [Fact]
    public void NoExternalInterfaceInstallsNoNewGetInCompletionLease()
    {
        using var seed=new Fixture();using var rig=new M4ControlTests.Rig(seed.Rig.Engine.Options.ResourcesPath);
        rig.Engine.BeforeDerivedConstructionForTest=robot=>robot.HasExternalInterface=false;rig.ToSynced();
        var owner=rig.Engine.Robot!;var getIn=Assert.IsType<NativeNeedsGetInBehavior>(owner.NativeBehaviorRuntime!.Bound["Needs_SevereLowPlayGetIn"]);
        int before=rig.Engine.AnimationMessages.SubscriberCountForTest(0x5A,true);
        getIn.BindActionCompletionOwner(rig.Robot,owner);Assert.Equal(before,rig.Engine.AnimationMessages.SubscriberCountForTest(0x5A,true));
        getIn.Dispose();Assert.Equal(before,rig.Engine.AnimationMessages.SubscriberCountForTest(0x5A,true));
    }
}
