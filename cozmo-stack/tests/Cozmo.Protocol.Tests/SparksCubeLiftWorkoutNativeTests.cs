using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class SparksCubeLiftWorkoutNativeTests
{
    private static NativeCubeLiftWorkoutBehavior Spark(Fixture f)
    {
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        return Assert.IsType<NativeCubeLiftWorkoutBehavior>(f.Runtime.Bound["SparksCubeLiftWorkout"]);
    }
    private static Activity Activity(Fixture f)=>NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksWorkout");
    private static void Begin(Fixture f,NativeCubeLiftWorkoutBehavior b,BehaviorScope scope)=>b.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
    private static void Complete(Fixture f,uint result)
    {
        var action=Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction());
        action.State=result;f.Robot.ActionList.Update();
    }
    [Fact]
    public void ShippedAliasAndParentUseDistinctIdentityAndSharedWorkoutOwner()
    {
        // A1-A7/P1-P3: shipped alias/parent assets and native factory 0059CA12..22.
        using var f=new Fixture();var spark=Spark(f);var ordinary=Assert.IsType<NativeCubeLiftWorkoutBehavior>(f.Runtime.Bound["CubeLiftWorkout"]);
        var activity=Activity(f);var owner=Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);
        Assert.NotSame(ordinary,spark);Assert.Equal(142,(int)BehaviorID.SparksCubeLiftWorkout);
        Assert.Equal(29u,spark.NativeBaseInputs!.RequiredUnlock);Assert.Equal(41,(int)spark.BehaviorObjective);
        Assert.Equal(new[]{"DriveOffCharger","KnockOverCubes","SparksCubeLiftWorkout","SparksLookInPlace"},activity.Chooser!.BehaviorIds);
        Assert.Equal(0f,owner.Minimum);Assert.Equal(60f,owner.Maximum);Assert.Equal((byte)1,owner.Repetitions);Assert.True(owner.WaitForAction);
        Assert.True(Assert.IsType<StrictPriorityChooser>(activity.Chooser).StopOnUnavailable);
        Assert.Equal(45,(int)NeedsActionId.Workout_Sparked);
        var delta=f.Rig.Engine.Needs.Actions["Workout_Sparked"];
        Assert.Equal(.05,delta.RepairDelta);Assert.Equal(-.01,delta.EnergyDelta);Assert.Equal(.35,delta.PlayDelta);Assert.Equal(.01,delta.PlayRange);Assert.Equal(0,delta.FreeplaySparksRewardWeight);
        var shared=f.Runtime.AI.Workout!;Assert.Equal(4,shared.Workouts.Count);
        shared.CompleteCurrentWorkout();Assert.Same(shared,f.Runtime.AI.Workout);Assert.Equal(1,shared.CurrentIndex);
    }
    [Fact]
    public void SuppliedParentAdmissionAndChildResultsReportSparkNeedsAndObjectiveToActualOwner()
    {
        // A12-A13/P1-P7: supplied admission, carrying and terminal action results; not raw parent selection or hardware.
        using var f=new Fixture();var spark=Spark(f);var activity=Activity(f);var owner=activity.SparkedOwner!;var manager=f.Robot.BehaviorManagerOwner!;
        manager.SparkNameInput=id=>id==29?"Workout":"Count";manager.SetNativeRequestedSpark(29,1);
        f.Runtime.AdmitActivity("SparksWorkout");manager.SwitchToNativeRequestedSpark();
        Assert.Equal((byte)28,f.Robot.BehaviorPublicState.State.Activity);
        var getIn=Assert.IsType<NativePlayArbitraryAnimBehavior>(owner.Desired(null,f.Rig.Engine.Timer.SecondsF,f.Runtime.Context));
        Assert.Equal(new[]{(AnimationTrigger)0x205},getIn.Triggers);
        f.Add();f.Robot.CarryingOwner!.SetCarrying(7);
        var first=f.Runtime.AI.Workout!.GetCurrentWorkout()! with{NumStrongLifts=Array.Empty<EmotionScorer>(),NumWeakLifts=Array.Empty<EmotionScorer>(),Transition=(AnimationTrigger)0x23F,PutDown=(AnimationTrigger)0x23F,EmotionEventOnComplete="",AdditionalObjectiveOnComplete=""};
        var workout=new WorkoutComponent(new[]{first,first});f.Runtime.AI.Workout=workout;
        var order=new List<string>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>order.Add("objective:"+p));
        void Need(string line){if(line.StartsWith("needs.action_completed Workout_Sparked:"))order.Add("needs45");}
        f.Rig.Engine.Needs.Log+=Need;
        try
        {
            using var scope=new BehaviorScope();Begin(f,spark,scope);
            Complete(f,0x03000018);Assert.IsAssignableFrom<NativePlaceGroundRunner>(f.Robot.ActionList.GetCurrentAction());
            f.Robot.CarryingOwner.UnsetCarrying();Complete(f,0x03000018);
            Assert.Equal(new[]{"objective:18","needs45"},order);Assert.Equal(1u,owner.Count);
            Assert.Same(workout,f.Runtime.AI.Workout);Assert.Equal(1,workout.CurrentIndex);Assert.Equal(1,workout.CompletedWorkouts);
            spark.Stop(BehaviorStopReason.Interrupted);
        }
        finally{f.Rig.Engine.Needs.Log-=Need;}
    }
    [Fact]
    public void SparkActionConstructionReentryKeepsReplacementOwnerAndTag()
    {
        // A7-A9: managed callback ownership guard; supplied carrying inputs.
        using var f=new Fixture();var spark=Spark(f);f.Add(7);f.Add(8,300);f.Robot.CarryingOwner!.SetCarrying(7);
        using var first=new BehaviorScope();using var second=new BehaviorScope();IActionRunner? replacement=null;uint tag=0;bool entered=false;
        spark.ActionConstructedForTest=_=>{if(entered)return;entered=true;spark.ActionConstructedForTest=null;spark.Stop(BehaviorStopReason.Interrupted);f.Robot.CarryingOwner.SetCarrying(8);Begin(f,spark,second);replacement=spark.OwnedRunner;tag=spark.CurrentRunnerTag;};
        Begin(f,spark,first);Assert.True(entered);Assert.NotNull(replacement);Assert.Same(replacement,spark.OwnedRunner);Assert.Equal(tag,spark.CurrentRunnerTag);Assert.Equal(8u,spark.Target);
        spark.Stop(BehaviorStopReason.Interrupted);
    }
    [Fact]
    public void SuppliedParentReadmissionKeepsReplacementResourcesOnReentry()
    {
        // P4/P11/P12: supplied selection; reentry occurs during old lock removal.
        using var f=new Fixture();Spark(f);var manager=f.Robot.BehaviorManagerOwner!;
        manager.SetNativeRequestedSpark(29,0);f.Runtime.AdmitActivity("SparksWorkout");bool replaced=false;
        manager.Log+=line=>
        {
            if(replaced||!line.StartsWith("BehaviorManager.RemoveDisableReactionsLock:"))return;
            replaced=true;f.Runtime.AdmitActivity("SparksWorkout");
        };
        f.Runtime.AdmitActivity("SparksWorkout");Assert.True(replaced);
        Assert.Single(f.Rig.Robot.Animations.DrivingAnimations.Entries.Where(e=>e.LockName=="SparksWorkout"));
        Assert.Single(f.Rig.Robot.Animations.Scheduler.IdleStack.Where(e=>e.Lock=="Activity_SparksWorkout"));
        Assert.Equal("SparksWorkout",f.Runtime.Observe().Activity);
    }

    [Fact]
    public void ActualManagerRetiresUnavailableEarlierCandidateThenReadmissionReachesAlias()
    {
        // P3/P13: supplied parent state 2 and physical carrying input; actual earlier native gates.
        using var f=new Fixture();var spark=Spark(f);var owner=Activity(f).SparkedOwner!;var manager=f.Robot.BehaviorManagerOwner!;
        manager.SparkNameInput=id=>id==29?"Workout":"Count";manager.SetNativeRequestedSpark(29,1);
        f.Robot.Progression!.Unlocked.Add(29);f.Add();f.Robot.CarryingOwner!.SetCarrying(7);
        f.Runtime.AdmitActivity("SparksWorkout");manager.SwitchToNativeRequestedSpark();
        var state=typeof(NativeSparkedActivity).GetProperty("State",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        state.SetValue(owner,2);
        var carried=f.Robot.CarryingOwner;var reports=new List<bool>();var missing=new List<string>();
        using var subscription=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x84,true,p=>{if(p is bool value)reports.Add(value);});
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=missing.Add;
        try
        {
            f.Robot.CarryingOwner=null;manager.Update(0,0);
            Assert.True(owner.InputUnavailable);Assert.Null(manager.Activity);Assert.Null(manager.Current);Assert.Empty(reports);Assert.Equal(0u,owner.Count);
            Assert.Contains(missing,line=>line.Contains("M15-059 SparksWorkout chooser unavailable input"));
            f.Robot.CarryingOwner=carried;
            f.Runtime.AdmitActivity("SparksWorkout");state.SetValue(owner,2);
            var drive=Assert.IsType<DriveOffChargerBehavior>(f.Runtime.Bound["DriveOffCharger"]);
            var knock=Assert.IsType<NativeKnockOverCubesBehavior>(f.Runtime.Bound["KnockOverCubes"]);
            var driveGate=drive.EvaluateHostReadiness(f.Runtime.Context);var knockGate=knock.EvaluateHostReadiness(f.Runtime.Context);
            Assert.True(driveGate.Available);Assert.False(driveGate.Runnable);Assert.True(knockGate.Available);Assert.False(knockGate.Runnable);
            manager.Update(0,0);Assert.False(owner.InputUnavailable);Assert.Same(spark,manager.Current);
            Assert.Empty(reports);Assert.Equal(0u,owner.Count);
        }
        finally{f.Robot.CarryingOwner=carried;SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact]
    public void BothConfiguredLeavesUseSameCurrentWorkoutAndMusicCacheAcrossRealCompletions()
    {
        // A3/A9/A12/A15: supplied action results, actual configured instances and shared AI owner.
        using var f=new Fixture();var spark=Spark(f);var ordinary=Assert.IsType<NativeCubeLiftWorkoutBehavior>(f.Runtime.Bound["CubeLiftWorkout"]);
        f.Add();f.Robot.CarryingOwner!.SetCarrying(7);
        var workout=f.Runtime.AI.Workout!;var original=workout.GetCurrentWorkout()!;
        var oneLift=new EmotionScorer(EmotionType.Confident,new Graph2d(new[]{(0d,1d)}),false);
        var first=original with{NumStrongLifts=new[]{oneLift},NumWeakLifts=Array.Empty<EmotionScorer>(),Transition=(AnimationTrigger)0x23F,PutDown=(AnimationTrigger)0x23F,EmotionEventOnComplete="",AdditionalObjectiveOnComplete=""};
        var second=first with{PostLift=(AnimationTrigger)0x23F};var configs=Assert.IsType<List<WorkoutConfig>>(workout.Workouts);configs[0]=first;configs[1]=second;workout.ResetEightiesMusicDecision();
        int draws=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{draws++;return 0;};
        var order=new List<string>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>order.Add("objective:"+p));
        void Need(string line){if(line.StartsWith("needs.action_completed Workout:"))order.Add("needs44");else if(line.StartsWith("needs.action_completed Workout_Sparked:"))order.Add("needs45");}
        f.Rig.Engine.Needs.Log+=Need;
        try
        {
            int beforeMusic=draws;Assert.True(f.Robot.EightiesMusicInput!());Assert.Equal(2,draws-beforeMusic);
            beforeMusic=draws;Assert.True(f.Robot.EightiesMusicInput!());Assert.Equal(0,draws-beforeMusic);
            using var one=new BehaviorScope();beforeMusic=draws;Begin(f,ordinary,one);Assert.Equal(0,draws-beforeMusic);
            Complete(f,0x03000018);Complete(f,0x03000018);f.Robot.CarryingOwner.UnsetCarrying();Complete(f,0x03000018);
            Assert.Equal(1,workout.CurrentIndex);Assert.Same(workout,f.Runtime.AI.Workout);ordinary.Stop(BehaviorStopReason.Interrupted);
            beforeMusic=draws;Assert.True(f.Robot.EightiesMusicInput!());Assert.Equal(2,draws-beforeMusic);
            f.Robot.CarryingOwner.SetCarrying(7);using var two=new BehaviorScope();beforeMusic=draws;Begin(f,spark,two);
            Assert.Equal(0,draws-beforeMusic);beforeMusic=draws;Assert.True(f.Robot.EightiesMusicInput!());Assert.Equal(0,draws-beforeMusic);var post=Assert.IsAssignableFrom<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());Assert.Single(post.Children);
            Complete(f,0x03000018);Complete(f,0x03000018);f.Robot.CarryingOwner.UnsetCarrying();Complete(f,0x03000018);
            Assert.Equal(new[]{"objective:18","needs44","objective:18","needs45"},order);
            Assert.Equal(2,workout.CurrentIndex);Assert.Equal(2,workout.CompletedWorkouts);Assert.Same(workout,f.Runtime.AI.Workout);
            spark.Stop(BehaviorStopReason.Interrupted);
            beforeMusic=draws;Assert.True(f.Robot.EightiesMusicInput!());Assert.Equal(2,draws-beforeMusic);
        }
        finally{f.Rig.Engine.Needs.Log-=Need;f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }

}
