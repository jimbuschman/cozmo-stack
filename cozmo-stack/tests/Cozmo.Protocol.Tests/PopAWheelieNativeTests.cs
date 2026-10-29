using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PopAWheelieNativeTests
{
    private static NativePopAWheelieBehavior Pop(Fixture f)=>Assert.IsType<NativePopAWheelieBehavior>(f.Runtime.Bound["PopAWheelie"]);
    private static NativePopAWheelieBehavior Admit(Fixture f,bool hard=false)
    {
        f.Robot.Progression!.Unlocked.Add(13);var manager=f.Robot.BehaviorManagerOwner!;
        if(hard){manager.SparkNameInput=id=>id==9?"PopAWheelieAction":"Count";manager.SetNativeRequestedSpark(9,0);manager.SwitchToNativeRequestedSpark();}
        // Supplied actual manager admission, not a claim that PlayAlone's parent chose this child.
        Assert.True(manager.StartAsync("PopAWheelie",f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());return Pop(f);
    }
    private static void StopOwned(NativePopAWheelieBehavior pop)=>typeof(SteppedBehavior).GetMethod("StopActing",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(bool),typeof(bool)},null)!.Invoke(pop,new object[]{false,false});
    private static void Cleanup(ActionRunner runner){runner.Prep();runner.WatcherEnding();}
    // P1/P4/P5/P6: shipped config, cache8/00570928 and shared0063C880, not closest-upright.
    [Fact]
    public void FactoryBindsExactOrdinaryConfigAndIndependentCache8UsesGroundPredicate()
    {
        using var f=new Fixture();var pop=Pop(f);Assert.Equal(13u,pop.NativeBaseInputs!.RequiredUnlock);
        var side=f.Add(7,100);side.Pose=new Pose3d(Rotation3d.FromAngleAxis(Radians.Pi*.5f,new(0,1,0)),new(100,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        f.Add(8,300);f.Add(9,10,70);var cache=f.Runtime.PickupTargets!;
        Assert.Equal(new uint[]{7,8},cache.GetValid(8));Assert.Equal(7u,cache.GetBest(8));
        Assert.False(pop.IsRunnable(f.Runtime.Context));f.Robot.Progression.Unlocked.Add(13);Assert.True(pop.IsRunnable(f.Runtime.Context));Assert.Equal(7u,pop.Target);
        f.M.Whiteboard.SetFailedToUse(side,(ObjectActionFailure)3);Assert.Equal(7u,cache.GetBest(8));f.Rig.Tick();Assert.Equal(8u,cache.GetBest(8));
        f.Robot.CarryingOwner!.SetCarrying(8);Assert.False(pop.IsRunnable(f.Runtime.Context));
    }
    // P7/P8 and D2: get-in turn+lift-safe; ordinary ctor creates optional face AND object children.
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeGetInAndHardSparkSkipRetainActualCompositeChildren(bool hard)
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);f.Add();var pop=Admit(f,hard);
        if(!hard)
        {
            var getIn=Assert.IsAssignableFrom<CompoundActionSequential>(pop.OwnedRunner);
            Assert.Collection(getIn.Children,c=>Assert.IsType<NativeTurnTowardsObjectRunner>(c),c=>Assert.Equal((AnimationTrigger)0x18A,Assert.IsType<TriggerLiftSafeAnimationAction>(c).Request.Trigger));
            Assert.Empty(f.Rig.SentSince(0).OfType<EnableStopOnCliff>());Assert.Equal(uint.MaxValue,pop.Previous);
            // Actual cancellation of get-in must still invoke its result-insensitive continuation.
            f.Robot.ActionList.Cancel(getIn.Tag);f.Robot.ActionList.Watcher.Update();for(int i=0;i<5&&pop.Interaction is null;i++)f.Rig.Tick();
        }
        var interaction=Assert.IsType<NativeDriveToPopAWheelieRunner>(pop.OwnedRunner);
        Assert.Equal(hard?0:BitConverter.UInt32BitsToSingle(0x3FC90FDB),interaction.MaximumFaceTurn);
        Assert.Equal(PreActionType.Rolling,interaction.Drive!.Action.ActionType);
        Assert.Equal(2,interaction.DriveAndWait!.Children.Count);Assert.Equal(0x1D,interaction.Pop.Type);
        if(hard)Assert.Null(interaction.Face);else{Assert.NotNull(interaction.Face);Assert.True(interaction.Face.SayName);Assert.Contains(interaction.Children,c=>c is NativeTurnTowardsObjectRunner);}
        Assert.Equal(.8f,(float)pop.RunningScoreBonus);Assert.False(pop.CliffChanged);
    }
    // D3: orderedheight >33.85 rejects, not a 30mm threshold, and carries only after that test.
    [Fact]
    public void SelectHeightAndRawCarryUseActualMainParentAndOrderedBranch()
    {
        using var f=new Fixture();var obj=f.Add(z:33);var runner=new NativePopAWheelieRunner(f.M,7);
        try
        {
            Assert.Equal(0u,runner.Select(obj));f.Robot.CarryingOwner!.SetCarrying(99);Assert.Equal(0x03000017u,runner.Select(obj));
            obj.Pose=new Pose3d(Rotation3d.Identity,new(150,0,34),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));Assert.Equal(0x03000004u,runner.Select(obj));
            obj.Pose=new Pose3d(Rotation3d.Identity,new(150,0,float.NaN),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));Assert.Equal(0x03000017u,runner.Select(obj));
            f.Robot.CarryingOwner.UnsetCarrying();Assert.Equal(0u,runner.Select(obj));
        }
        finally{Cleanup(runner);}
    }
    // D4:55295C..52A72 and Radians84CD12/84CC90 actual RobotState/Docking owner inputs.
    [Theory]
    [InlineData(1f,0u)] [InlineData(.999995f,0u)] [InlineData(.99998f,0x0400000Au)]
    [InlineData(1.2f,0u)] [InlineData(-1.2f,0x0400000Au)] [InlineData(float.NaN,0u)]
    public void VerifyUsesDockFlagAndNativeRadiansEpsilonNotAbsoluteOrRawComparison(float pitch,uint expected)
    {
        using var f=new Fixture();f.Add();var runner=new NativePopAWheelieRunner(f.M,7);
        try
        {
            Assert.Equal(0x04000003u,runner.Verify());
            var state=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,frame:f.Robot.PoseFrameId);state.Pose.Pitch=pitch;f.Rig.Data(state);f.Rig.Tick();
            f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=0});f.Rig.Tick();
            Assert.Equal(expected,runner.Verify());runner.Action.SetSelectedDockAction(DockAction.Align);Assert.Equal(0x0300001Au,runner.Verify());
        }
        finally{Cleanup(runner);}
    }
    // D5 guard: exact local scalar/defined fields survive actual leaf proxy; undefined fullbody withheld.
    [Fact]
    public void ActualLeafProxyKeepsScalarTagTypeAndDefinedLocalFieldsWhileGuardingUndefinedFullBody()
    {
        using var f=new Fixture();var local=new List<RobotCompletedAction>();var watched=new List<RobotCompletedAction>();var app=new List<ActionCompletionReport>();
        int watch=f.Robot.ActionList.RegisterActionEndedCallback(watched.Add);
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)local.Add(r);});f.Rig.Engine.ActionCompleted+=app.Add;
        var missing=new List<string>();void Missing(string line)=>missing.Add(line);SteppedBehavior.MissingReported+=Missing;
        SteppedBehavior.ResetMissingForTests(); // test owns the serial missing-report collection; reset only diagnostic deduplication.
        var runner=new NativeDriveToPopAWheelieRunner(f.M,404,0);
        try
        {
            Assert.Equal(0,f.Robot.ActionList.QueueAction(QueueActionPosition.Now,runner,0));for(int i=0;i<5&&!runner.DestructionObserved;i++)f.Rig.Tick();
            var leaf=Assert.Single(watched.Where(r=>r.Tag==runner.Pop.Tag));Assert.Equal(0x1D,leaf.ActionType);Assert.Equal(0x03000004u,leaf.Result);
            var proxy=Assert.Single(local.Where(r=>r.Tag==runner.Tag));Assert.Equal(leaf.Result,proxy.Result);Assert.Equal(leaf.ActionType,proxy.ActionType);
            var payload=Assert.IsType<ObjectInteractionCompletion>(proxy.ObjectInteraction);Assert.Equal(404u,payload.Word0);Assert.Equal((byte)1,payload.Count);Assert.False(payload.Flag);Assert.False(payload.IsPayloadDefined);Assert.Throws<InvalidOperationException>(()=>payload.PackBody());
            f.Rig.Engine.DrainAppDelivery();Assert.DoesNotContain(app,r=>r.ActionType==0x1D);Assert.Contains(missing,l=>l.Contains("M1-034")&&l.Contains("POP-A-WHEELIE-1 D5"));
        }
        finally{SteppedBehavior.MissingReported-=Missing;f.Robot.ActionList.UnregisterActionEndedCallback(watch);f.Robot.ActionList.Clear();}
    }
    // P9/P12/P13 + D1-D5: real manager/cache/get-in/IDrive/IDock, fixed scene and firmware feedback.
    [Fact]
    public void ConfiguredOrdinaryPipelinePredockFirmwarePitchTypedResultObjectiveNeedsAndConditionalStop()
    {
        using var scene=new PopFirmwareScene();var f=scene.F;var order=new List<string>();var local=new List<RobotCompletedAction>();
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint id&&id==22)order.Add("objective22");});
        f.Rig.Engine.Needs.Log+=line=>{if(line.StartsWith("needs.action_completed PopAWheelie:")){Assert.Equal(new[]{"objective22"},order);Assert.Equal((AnimationTrigger)0x21C,Assert.IsType<TriggerAnimationAction>(Pop(f).OwnedRunner).Request.Trigger);order.Add("needs33");}};
        using var complete=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r&&r.ActionType==0x1D)local.Add(r);});
        var pop=Admit(f);for(int i=0;i<250&&order.Count<2;i++)scene.Step();
        Assert.Equal(new[]{"objective22","needs33"},order);Assert.Equal(1,scene.Rolls);Assert.True(pop.CliffChanged);Assert.Equal(uint.MaxValue,pop.Previous);
        var dock=Assert.Single(f.Rig.SentSince(0).OfType<DockWithObject>());Assert.Equal((byte)9,dock.DockAction);
        Assert.Contains(local,r=>r.Result==0&&r.ObjectInteraction is {Word0:var target,Count:1,Flag:false,IsPayloadDefined:false}&&target==scene.Target);
        for(int i=0;i<21;i++)Assert.Equal(new[]{0,1,3,11,12,14,15,20}.Contains(i),f.Robot.BehaviorManagerOwner!.HasDisableLock((ReactionTrigger)i,"PopAWheelie_behaviorLock"));
        Assert.Contains(f.Rig.SentSince(0),m=>m is EnableStopOnCliff{Enable:false});
        f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Rig.Tick();
        Assert.False(pop.CliffChanged);Assert.Equal(uint.MaxValue,pop.Target);Assert.Single(f.Rig.SentSince(0).OfType<EnableStopOnCliff>().Where(m=>m.Enable));
    }
    // P10-P11: isolated full scalar callback inputs (not fake native outcomes), actual retry action/refresh.
    [Theory]
    [InlineData(0x04000001u,0x18D)] [InlineData(0x04000003u,0x18E)]
    public void RetryRefreshesTargetAndCapsOneAttemptIgnoringRetryAnimationResult(uint result,int trigger)
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);var first=f.Add();var pop=Admit(f,true);
        f.M.Whiteboard.SetFailedToUse(first,(ObjectActionFailure)3);f.Add(8,300);f.Rig.Tick();StopOwned(pop);
        pop.Respond(result);Assert.Equal(8u,pop.Target);var retry=Assert.IsType<TriggerLiftSafeAnimationAction>(pop.OwnedRunner);Assert.Equal((AnimationTrigger)trigger,retry.Request.Trigger);
        f.Robot.ActionList.Cancel(retry.Tag);f.Robot.ActionList.Watcher.Update();for(int i=0;i<5&&pop.Retries==0;i++)f.Rig.Tick();
        Assert.Equal(1,pop.Retries);Assert.Equal(8u,pop.Interaction!.Pop.Action.ObjectId);
        StopOwned(pop);pop.Respond(0x04000003);Assert.True(f.M.Whiteboard.DidFailToUse(8,(ObjectActionFailure)3,30f));Assert.Null(pop.OwnedRunner);
    }
    // P10/P12: explicit scalar completion boundary, real source recipients and native inherited terminal update.
    [Theory]
    [InlineData(0x03000004u,true)] [InlineData(0x02000000u,false)]
    public void OnlyAbortCategoryMarksFailureAndNoActionEndsThroughInheritedUpdate(uint result,bool marked)
    {
        using var f=new Fixture();f.Add();var pop=Admit(f,true);StopOwned(pop);pop.Respond(result);
        Assert.Equal(marked,f.M.Whiteboard.DidFailToUse(7,(ObjectActionFailure)3,30f));Assert.Null(pop.OwnedRunner);
        Assert.True(pop.EngineRunning);f.Rig.Tick();Assert.Null(f.Robot.BehaviorManagerOwner!.Current);
    }
    [Fact]
    public void NonzeroSuccessCategoryQueuesCelebrationBeforeObjectiveAndConfiguredNeeds()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);f.Add();var pop=Admit(f,true);StopOwned(pop);var order=new List<string>();
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint id&&id==22){Assert.Equal((AnimationTrigger)0x21C,Assert.IsType<TriggerAnimationAction>(pop.OwnedRunner).Request.Trigger);order.Add("objective22");}});
        f.Rig.Engine.Needs.Log+=line=>{if(line.StartsWith("needs.action_completed PopAWheelie:")){Assert.Equal(new[]{"objective22"},order);order.Add("needs33");}};
        pop.Respond(0x00000002);Assert.Equal(new[]{"objective22","needs33"},order);Assert.Equal(uint.MaxValue,pop.Previous);Assert.True(pop.EngineRunning);
    }
    [Fact]
    public void SuccessfulGetInCachesPreviousAndNextAdmissionSkipsItButFailureDoesNot()
    {
        using var scene=new PopFirmwareScene();var f=scene.F;var pop=Admit(f);
        for(int i=0;i<100&&pop.Interaction is null;i++)scene.Step(()=>pop.Interaction is not null);
        Assert.NotNull(pop.Interaction);Assert.Equal(scene.Target,pop.Previous);Assert.False(pop.CliffChanged);
        f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        var again=Admit(f);Assert.Same(pop,again);Assert.IsType<NativeDriveToPopAWheelieRunner>(again.OwnedRunner);
    }
    [Fact]
    public void RetryWithNoRemainingCacheTargetStillRunsRetryAnimationThenWarnsWithoutNewDrive()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);var obj=f.Add();var pop=Admit(f,true);
        f.M.Whiteboard.SetFailedToUse(obj,(ObjectActionFailure)3);f.Rig.Tick();StopOwned(pop);pop.Respond(0x04000003);
        Assert.Equal(uint.MaxValue,pop.Target);var retry=Assert.IsType<TriggerLiftSafeAnimationAction>(pop.OwnedRunner);Assert.Equal((AnimationTrigger)0x18E,retry.Request.Trigger);
        f.Robot.ActionList.Cancel(retry.Tag);f.Robot.ActionList.Watcher.Update();for(int i=0;i<5&&pop.OwnedRunner is not null;i++)f.Rig.Tick();
        Assert.Null(pop.OwnedRunner);Assert.Equal(0,pop.Retries); // invalid-target early return precedes increment.
        Assert.Empty(f.Rig.SentSince(0).OfType<DockWithObject>());
    }
    [Fact]
    public void CancelBeforePredockDoesNotSendCliffRestore()
    {
        using var f=new Fixture();f.Add();Admit(f);f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Rig.Tick();Assert.Empty(f.Rig.SentSince(0).OfType<EnableStopOnCliff>());
    }
}
