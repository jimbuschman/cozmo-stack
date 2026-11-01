using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Protocol;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeExpressNeedsAdmissionTests
{
    private static NativeExpressNeedsBehavior Get(Fixture f,string id)=>Assert.IsType<NativeExpressNeedsBehavior>(f.Runtime.Bound[id]);
    private static void Clock(Fixture f,long ns)=>f.Rig.Engine.Timer.UpdateTime(ns);
    private static CompoundActionSequential Start(Fixture f,NativeExpressNeedsBehavior behavior,BehaviorScope scope)
    {
        behavior.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        return Assert.IsType<CompoundActionSequential>(behavior.OwnedRunner);
    }
    [Fact]
    public void SixConfiguredOwnersShareTheActualContainerAndParseShippedValues()
    {
        using var f=new Fixture();
        var ids=new[]{"Needs_MildLowEnergyRequest","Needs_MildLowPlayRequest","Needs_MildLowRepairRequest",
            "Needs_SevereLowPlayBored","Needs_SevereLowPlayRequest","Needs_SevereLowEnergyForcedGetOut"};
        foreach(string id in ids)
        {
            var behavior=Get(f,id);Assert.True(behavior.ParametersAvailable);
            Assert.Same(behavior,f.Robot.BehaviorManagerOwner!.Find(id));
            Assert.Same(behavior.NativeScoredFields,f.Robot.NativeConfiguredScoreFields![id]);
        }
        Assert.Equal(new[]{329,327,330},Get(f,"Needs_SevereLowPlayBored").Triggers.Select(t=>(int)t));
        Assert.Equal(NeedId.Energy,Get(f,"Needs_SevereLowEnergyForcedGetOut").Need);
        Assert.Equal(NeedBracketId.Critical,Get(f,"Needs_SevereLowEnergyForcedGetOut").Bracket);
    }
    [Fact]
    public void ActualCommonSevereGatePrecedesOwnBracketAndCooldown()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var behavior=Get(f,"Needs_SevereLowPlayRequest");
        int ownCalls=0;behavior.BeforeOwnPredicateForTest=()=>ownCalls++;
        f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Play,0.01);Clock(f,100_000_000_000);
        Assert.True(f.Rig.Engine.Needs.State.IsNeedAtBracket(NeedId.Play,NeedBracketId.Critical));
        f.Rig.Engine.Needs.SevereNeeds.Clear();Assert.False(behavior.IsRunnable(f.Runtime.Context));Assert.Equal(0,ownCalls);
        f.Rig.Engine.Needs.SevereNeeds.Set(NeedId.Play);Assert.True(behavior.IsRunnable(f.Runtime.Context));Assert.Equal(1,ownCalls);
    }
    [Fact]
    public void CanonicalInterludeUsesActualOwnerAndStrictF32Cooldown()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var needs=f.Rig.Engine.Needs;needs.State.SetNeedLevel(NeedId.Energy,0.55);
        var behavior=Get(f,"Needs_MildLowEnergyRequest");
        Clock(f,60_000_000_000);Assert.False(behavior.IsRunnable(f.Runtime.Context));
        Clock(f,60_000_004_000);Assert.True(behavior.IsRunnable(f.Runtime.Context));
        var social=f.Runtime.FreeplayObserver.Freeplay.SubActivities.Single(a=>a.Id=="Socialize");
        var chooser=Assert.IsType<StrictPriorityChooser>(social.InterludeChooser);
        var decision=chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        Assert.False(decision.InputUnavailable);Assert.Same(behavior,decision.Behavior);
    }
    [Fact]
    public void NativeCompoundUsesRawZeroFaceThenPlainTriggersAndStampsOnlySuccess()
    {
        using var f=new Fixture();var behavior=Get(f,"Needs_SevereLowPlayBored");using var scope=new BehaviorScope();
        var sequence=Start(f,behavior,scope);Assert.Equal(4,sequence.Children.Count);
        var face=Assert.IsType<NativeLastFaceRunner>(sequence.Children[0]);Assert.Equal(0,face.RequestedId);Assert.False(face.SayName);
        Assert.Equal(0x40490FDBu,BitConverter.SingleToUInt32Bits(face.MaximumTurn));
        Assert.Equal(new[]{329,327,330},sequence.Children.Skip(1).Select(a=>(int)Assert.IsType<TriggerAnimationAction>(a).Request.Trigger));
        Clock(f,100_000_000_000);sequence.State=0;f.Robot.ActionList.Update();Assert.Equal(100f,behavior.LastSuccessSec);
        behavior.Stop(BehaviorStopReason.Interrupted);using var nextScope=new BehaviorScope();
        var failed=Start(f,behavior,nextScope);Clock(f,110_000_000_000);failed.State=0x03000018;f.Robot.ActionList.Update();
        Assert.Equal(100f,behavior.LastSuccessSec);behavior.Stop(BehaviorStopReason.Interrupted);
    }
    [Fact]
    public void LateCompletionAfterStopOrRetirementCannotStamp()
    {
        using var f=new Fixture();var behavior=Get(f,"Needs_MildLowPlayRequest");using var scope=new BehaviorScope();
        var sequence=Start(f,behavior,scope);uint old=sequence.Tag;behavior.Stop(BehaviorStopReason.Interrupted);
        Clock(f,100_000_000_000);f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(old,0x16,0,Array.Empty<uint>(),0));
        Assert.Equal(0f,behavior.LastSuccessSec);behavior.Dispose();
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(old,0x16,0,Array.Empty<uint>(),0));Assert.Equal(0f,behavior.LastSuccessSec);
    }
    [Fact]
    public void ReplacementRobotCannotReceiveOldCompletionOrOldStamp()
    {
        using var f=new Fixture();var original=f.Robot;var behavior=Get(f,"Needs_MildLowPlayRequest");using var scope=new BehaviorScope();
        var sequence=Start(f,behavior,scope);uint old=sequence.Tag;
        f.Rig.Engine.Robots.RemoveRobot(1,false);var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
        Assert.NotSame(original,replacement);
        var next=Assert.IsType<NativeExpressNeedsBehavior>(replacement.NativeBehaviorRuntime!.Bound["Needs_MildLowPlayRequest"]);
        Clock(f,100_000_000_000);f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(old,0x16,0,Array.Empty<uint>(),0));
        Assert.Equal(0f,behavior.LastSuccessSec);Assert.Equal(0f,next.LastSuccessSec);Assert.Null(next.OwnedRunner);
    }
    [Fact]
    public void ForcedGetOutStopClearsTheSameActualSevereOwner()
    {
        using var f=new Fixture();var behavior=Get(f,"Needs_SevereLowEnergyForcedGetOut");
        f.Rig.Engine.Needs.SevereNeeds.Set(NeedId.Energy);Assert.Equal((int)NeedId.Energy,f.Runtime.AI.SevereNeeds.Selected);
        using var scope=new BehaviorScope();Start(f,behavior,scope);behavior.Stop(BehaviorStopReason.Interrupted);
        Assert.Equal(3,f.Runtime.AI.SevereNeeds.Selected);
    }
    [Fact]
    public void NoExternalInterfaceDoesNotInstallCompletionRecipient()
    {
        using var seed=new Fixture();using var rig=new M4ControlTests.Rig(seed.Rig.Engine.Options.ResourcesPath);
        rig.Engine.BeforeDerivedConstructionForTest=robot=>robot.HasExternalInterface=false;rig.ToSynced();
        var owner=rig.Engine.Robot!;var behavior=Assert.IsType<NativeExpressNeedsBehavior>(owner.NativeBehaviorRuntime!.Bound["Needs_MildLowEnergyRequest"]);
        int before=rig.Engine.AnimationMessages.SubscriberCountForTest(0x5A,true);
        behavior.BindActionCompletionOwner(rig.Robot,owner);
        Assert.Equal(before,rig.Engine.AnimationMessages.SubscriberCountForTest(0x5A,true));
        behavior.Dispose();Assert.Equal(before,rig.Engine.AnimationMessages.SubscriberCountForTest(0x5A,true));
    }
    [Fact]
    public void OriginalOwnerIsCapturedBeforePublicationAndCompletionLeaseRetiresOnce()
    {
        using var seed=new Fixture();using var rig=new M4ControlTests.Rig(seed.Rig.Engine.Options.ResourcesPath);
        EngineRobot? original=null;bool unpublished=false;
        rig.Engine.BeforeDerivedConstructionForTest=robot=>{original=robot;unpublished=rig.Engine.Robot is null;};
        rig.ToSynced();Assert.True(unpublished);Assert.Same(original,rig.Engine.Robot);
        var behavior=Assert.IsType<NativeExpressNeedsBehavior>(original!.NativeBehaviorRuntime!.Bound["Needs_MildLowPlayRequest"]);
        var field=typeof(NativeExpressNeedsBehavior).GetField("_robot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        Assert.Same(original,field.GetValue(behavior));int before=rig.Engine.AnimationMessages.SubscriberCountForTest(0x5A,true);
        behavior.BindActionCompletionOwner(rig.Robot,original);Assert.Equal(before,rig.Engine.AnimationMessages.SubscriberCountForTest(0x5A,true));
        behavior.Dispose();Assert.Equal(before-1,rig.Engine.AnimationMessages.SubscriberCountForTest(0x5A,true));
    }
    [Fact]
    public void RealConstructorMissingCallbackStopRetiresUnqueuedFaceSubscriptions()
    {
        using var f=new Fixture();var behavior=Get(f,"Needs_MildLowPlayRequest");using var scope=new BehaviorScope();
        int beforeChanged=f.Rig.Engine.AnimationMessages.SubscriberCountForTest(0x47,true);
        int beforeDeleted=f.Rig.Engine.AnimationMessages.SubscriberCountForTest(0x4B,true);int callbacks=0;
        void Missing(string line){if(line.Contains("M8-021 trigger DAS") && callbacks++==0)behavior.Stop(BehaviorStopReason.Interrupted);}
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Missing;
        try
        {
            behavior.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
            Assert.Equal(1,callbacks);Assert.False(behavior.EngineRunning);Assert.Null(behavior.OwnedRunner);
            Assert.Equal(beforeChanged,f.Rig.Engine.AnimationMessages.SubscriberCountForTest(0x47,true));
            Assert.Equal(beforeDeleted,f.Rig.Engine.AnimationMessages.SubscriberCountForTest(0x4B,true));
        }
        finally {SteppedBehavior.MissingReported-=Missing;}
    }
    [Fact]
    public void ActualOrdinaryParentSelectsEligibleExpressInterlude()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        f.Rig.Tick(61_000);f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Energy,0.55);
        Assert.True(f.Rig.Engine.Needs.State.IsNeedAtBracket(NeedId.Energy,NeedBracketId.Warning));
        var express=Get(f,"Needs_MildLowEnergyRequest");f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
        for(int i=0;i<30 && !ReferenceEquals(f.Robot.BehaviorManagerOwner!.Current,express);i++)f.Rig.Tick(50);
        Assert.Same(f.Runtime.FreeplayObserver,f.Robot.BehaviorManagerOwner!.Activity);
        Assert.Same(express,f.Robot.BehaviorManagerOwner.Current);Assert.IsType<CompoundActionSequential>(express.OwnedRunner);
    }
    // Supplied child terminal failures exercise the actual ActionList and compound progression, not firmware success.
    [Fact]
    public void IgnoredFaceFailureAdvancesButTriggerFailureSuppressesLaterTriggers()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var behavior=Get(f,"Needs_SevereLowPlayBored");using var scope=new BehaviorScope();
        var sequence=Start(f,behavior,scope);var face=Assert.IsType<NativeLastFaceRunner>(sequence.Children[0]);
        var first=Assert.IsType<TriggerAnimationAction>(sequence.Children[1]);var later=sequence.Children.Skip(2).ToArray();
        face.State=0x03000018;f.Robot.ActionList.Update();
        Assert.True(face.DestructionObserved);Assert.Contains(first,sequence.Children);
        first.State=0x03000018;f.Robot.ActionList.Update();
        Assert.True(sequence.DestructionObserved);Assert.All(later,a=>Assert.False(a.Initialized));
        Assert.Equal(0f,behavior.LastSuccessSec);behavior.Stop(BehaviorStopReason.Interrupted);
    }
}
