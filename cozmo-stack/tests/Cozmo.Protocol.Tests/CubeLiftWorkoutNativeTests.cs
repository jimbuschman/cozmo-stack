using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class CubeLiftWorkoutNativeTests
{
    private static NativeCubeLiftWorkoutBehavior Configured(Fixture f)
    {
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        return Assert.IsType<NativeCubeLiftWorkoutBehavior>(f.Runtime.Bound["CubeLiftWorkout"]);
    }
    private static void Begin(Fixture f,NativeCubeLiftWorkoutBehavior behavior,BehaviorScope scope)
        =>behavior.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
    private static void Complete(Fixture f,uint result)
    {
        var action=Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction());
        action.State=result; f.Robot.ActionList.Update();
    }
    private static void PreparePickup(Fixture f)
    {
        var cube=f.Add(7,150);
        var input=DockPreActionPoses.Get(new PreActionPoseInput(cube,PreActionType.Docking,false,DockActionBase.PreActionAngleToleranceRad,0,false,0),
            f.Robot.GetCheckedMainPose(),null,()=>f.M.GetObstacles(f.Robot.GetCheckedMainPose()));
        Assert.Equal(ActionResult.Success,input.Result);
        using var goal=input.Poses[input.ClosestIndex].WorldPose.GetWithRespectToRoot();
        f.X=(float)goal.Translation.X;f.Y=(float)goal.Translation.Y;f.Yaw=(float)goal.AngleAroundZ;f.Motors();
        cube.LastObservedTimestamp=f.Robot.StoredState!.Timestamp;
        foreach(var marker in cube.Markers)marker.LastObservedTimestamp=cube.LastObservedTimestamp;
    }
    // reach: supplied activity feeding the real StrictPriorityChooser and BehaviorManager; PlayAlone admission is unbound.
    private sealed class SuppliedChooserActivity(StrictPriorityChooser chooser, BehaviorContext context) : IManagedActivity
    {
        public int Decisions, Selections;
        public void Update(double nowSec) { }
        public IBehavior? GetDesiredActiveBehavior(IBehavior? current, double nowSec)
        {
            Decisions++;
            var decision=chooser.GetDesiredActiveBehavior(current,0,context,nowSec);
            if(decision.Behavior is not null)Selections++;
            return decision.Behavior;
        }
        public void BehaviorSwitched(IBehavior? desired,bool started,double nowSec) { }
    }

    // reach: configured ordinary and Spark aliases, direct readiness; no activity admission.
    [Fact]
    public void LibraryGateAndActualRunnablePathRemainAvailable()
    {
        using var f=new Fixture();f.Add(7);f.Robot.CarryingOwner!.SetCarrying(7);f.Robot.Progression!.Unlocked.Add(29);
        foreach(string id in new[]{"CubeLiftWorkout","SparksCubeLiftWorkout"})
        {
            var behavior=Assert.IsType<NativeCubeLiftWorkoutBehavior>(f.Runtime.Bound[id]);
            var readiness=behavior.EvaluateHostReadiness(f.Runtime.Context);
            Assert.False(readiness.Available);Assert.False(readiness.Runnable);
        }
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        foreach(string id in new[]{"CubeLiftWorkout","SparksCubeLiftWorkout"})
            Assert.True(Assert.IsType<NativeCubeLiftWorkoutBehavior>(f.Runtime.Bound[id]).IsRunnable(f.Runtime.Context));
    }

    // reach: unit/supplied activity, actual chooser and manager, 30 updates; not PlayAlone production admission.
    [Theory]
    [InlineData("CubeLiftWorkout",0)] [InlineData("CubeLiftWorkout",1)] [InlineData("CubeLiftWorkout",2)]
    [InlineData("SparksCubeLiftWorkout",0)] [InlineData("SparksCubeLiftWorkout",1)] [InlineData("SparksCubeLiftWorkout",2)]
    public void MissingInputsAreUnavailableWithoutInitOrReselection(string id,int missing)
    {
        using var f=new Fixture();Configured(f);f.Add(7);f.Robot.CarryingOwner!.SetCarrying(7);f.Robot.Progression!.Unlocked.Add(29);
        var behavior=Assert.IsType<NativeCubeLiftWorkoutBehavior>(f.Runtime.Bound[id]);
        Assert.NotNull(f.Runtime.AI.Workout!.GetCurrentWorkout());Assert.NotNull(f.Robot.MoodOwner);
        Assert.True(behavior.IsRunnableBase(f.Runtime.Context));
        var valid=behavior.EvaluateHostReadiness(f.Runtime.Context);
        Assert.True(valid.Available);Assert.True(valid.Runnable);
        var oldMood=f.Robot.MoodOwner;
        if(missing==0)f.Runtime.AI.Workout=null;
        if(missing==1)f.Runtime.AI.Workout=new WorkoutComponent(Array.Empty<WorkoutConfig>());
        if(missing==2)f.Robot.MoodOwner=null;
        var chooser=new StrictPriorityChooser(new[]{id},f.Runtime.Bound){StopOnUnavailable=true};
        var activity=new SuppliedChooserActivity(chooser,f.Runtime.Context);
        var manager=f.Robot.BehaviorManagerOwner!;manager.Activity=activity;
        var idleBefore=f.Rig.Robot.Animations.Scheduler.IdleStack.ToArray();
        var lights=new List<string>();f.Rig.Robot.Lights.Cubes.BehaviorStateCallObservedForTest=(objectId,call,trigger,layer,modifier,data,offset)=>lights.Add(call);
        var messages=new List<string>();SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=messages.Add;
        try
        {
            var readiness=behavior.EvaluateHostReadiness(f.Runtime.Context);
            Assert.False(readiness.Available);Assert.False(readiness.Runnable);
            for(int i=0;i<30;i++)manager.Update(i,i);
            Assert.Equal(30,activity.Decisions);Assert.Equal(0,activity.Selections);
            Assert.Null(manager.Current);Assert.False(behavior.InitFailed);Assert.Null(behavior.OwnedRunner);
            Assert.Empty(lights);Assert.Equal(idleBefore,f.Rig.Robot.Animations.Scheduler.IdleStack);
            for(int trigger=0;trigger<21;trigger++)Assert.False(manager.HasDisableLock((ReactionTrigger)trigger,id+"_behaviorLock"));
            Assert.Contains(messages,m=>m.Contains("M1-034/M13-010"));
        }
        finally { manager.Activity=null;f.Robot.MoodOwner=oldMood;SteppedBehavior.MissingReported-=messages.Add; }
    }

    // reach: public component unit call; native 0x00573E34 cache precedes the 0x00573E42 null dereference.
    [Fact]
    public void MusicNullCurrentSkipsWithoutMoodRngOrCacheAndRetainsCachedAnswer()
    {
        using var f=new Fixture();Configured(f);var config=f.Runtime.AI.Workout!.Workouts[0];
        var entries=new List<WorkoutConfig>{config};var workout=new WorkoutComponent(entries);
        Assert.True(workout.ShouldPlayEightiesMusic(_=>0,null,()=>0));
        entries.Clear();
        Assert.True(workout.ShouldPlayEightiesMusic(_=>throw new Exception("mood"),null,()=>throw new Exception("rng")));
        workout.ResetEightiesMusicDecision();
        var messages=new List<string>();SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=messages.Add;
        try
        {
            Assert.False(workout.ShouldPlayEightiesMusic(_=>throw new Exception("mood"),null,()=>throw new Exception("rng")));
            Assert.Contains(messages,m=>m.Contains("M1-034/M13-010")&&m.Contains("00573E42"));
            entries.Add(config);
            Assert.True(workout.ShouldPlayEightiesMusic(_=>0,null,()=>0));
        }
        finally{SteppedBehavior.MissingReported-=messages.Add;}
    }

    [Fact]
    public void OrdinaryFactoryUsesRobotOwnedShippedWorkoutAndSparkIsSeparate()
    {
        using var f=new Fixture(); var behavior=Configured(f);
        var workout=Assert.IsType<WorkoutComponent>(f.Runtime.AI.Workout);
        Assert.Equal(4,workout.Workouts.Count);
        Assert.Equal(AnimationTrigger.WorkoutStrongLift_highEnergy,workout.GetCurrentWorkout()!.StrongLift);
        Assert.NotSame(behavior,f.Runtime.Bound.GetValueOrDefault("SparksCubeLiftWorkout"));
        Assert.NotNull(f.Robot.EightiesMusicInput);
    }
    [Fact]
    public void MissingCanonicalWorkoutDoesNotInventCountsOrCompletion()
    {
        using var f=new Fixture();var behavior=Configured(f);f.Robot.CarryingOwner!.SetCarrying(7);
        f.Runtime.AI.Workout=null;
        using var scope=new BehaviorScope();Begin(f,behavior,scope);
        Assert.True(behavior.InitFailed);Assert.Null(behavior.OwnedRunner);Assert.Equal((0u,0u),behavior.Counts);
    }
    [Fact]
    public void MusicCacheIsSharedWithAudioCallerAndStopInvalidatesIt()
    {
        using var f=new Fixture();var behavior=Configured(f);f.Add();f.Robot.CarryingOwner!.SetCarrying(7);
        int draws=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{draws++;return 0;};
        try
        {
            Assert.True(f.Robot.EightiesMusicInput!());Assert.Equal(2,draws);
            using var scope=new BehaviorScope();Begin(f,behavior,scope);
            Assert.Equal(2,draws);Assert.Equal(3,f.Robot.BehaviorPublicState.State.StageTag);
            Assert.Equal((byte)2,f.Robot.BehaviorPublicState.State.Stage(3));
            behavior.Stop(BehaviorStopReason.Interrupted);
            Assert.Equal(4,f.Robot.BehaviorPublicState.State.StageTag);
            Assert.True(f.Robot.EightiesMusicInput!());Assert.Equal(4,draws);
        }
        finally{f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }
    [Fact]
    public void ActualAudioClientsUseRobotOwnedWorkoutDecisionAndPublishStage()
    {
        using var f=new Fixture();Configured(f);
        var owner=f.Runtime.AI.Workout;Assert.NotNull(owner);Assert.NotNull(f.Robot.EightiesMusicInput);
        int draws=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{draws++;return 0;};
        var activations=new List<string>();f.Robot.BehaviorAudioA!.ActivatingForTest=()=>activations.Add("A");
        f.Robot.BehaviorAudioB!.ActivatingForTest=()=>activations.Add("B");
        try
        {
            // Supplied app music-state and public-state input; clients and workout are the actual Robot owners.
            f.Rig.Robot.SetSparkedMusicState(0x1234);f.Rig.Tick();
            draws=0;
            f.Robot.BehaviorPublicState.Update(new(0x1D,0x1D,-1,0x16,null));
            Assert.Equal(new[]{"A","B","A"},activations);Assert.Equal(2,draws);
            Assert.Equal(3,f.Robot.BehaviorPublicState.State.StageTag);
            Assert.Equal((byte)1,f.Robot.BehaviorPublicState.State.Stage(3));
            Assert.Same(owner,f.Runtime.AI.Workout);
        }
        finally{f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }
    [Fact]
    public void ActualMoodHistoryFeedsWorkoutDeltaScorerAndSharedMusicDecision()
    {
        using var f=new Fixture();Configured(f);var mood=f.Robot.MoodOwner!;var workout=f.Runtime.AI.Workout!;
        var configs=Assert.IsType<List<WorkoutConfig>>(workout.Workouts);
        // Supplied scorer configuration exercises trackDelta; current and 60-tick history are the actual MoodState.
        var delta=new EmotionScorer(EmotionType.Confident,new Graph2d(new[]{(0d,0d),(1d,1d)}),true);
        configs[0]=configs[0] with{NumStrongLifts=new[]{delta}};
        float start=mood.LastUpdateSeconds;
        for(int i=1;i<=61;i++)mood.Advance(start+i);
        Assert.Equal(0f,mood.GetHistoryValueTicksAgo(EmotionType.Confident,60));
        Assert.True(mood.Trigger("StrongWorkoutCompleted",mood.LastUpdateSeconds));
        Assert.Equal(1d,mood[EmotionType.Confident]);
        int draws=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{draws++;return 0;};
        try{Assert.True(f.Robot.EightiesMusicInput!());Assert.Equal(2,draws);}
        finally{f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }
    [Fact]
    public void ActualBoundMoodCompletionOccursBeforeCurrentWorkoutAdvances()
    {
        using var f=new Fixture();Configured(f);var mood=f.Robot.MoodOwner!;var workout=f.Runtime.AI.Workout!;
        Assert.Equal(0d,mood[EmotionType.Confident]);int observedIndex=-1;double observedMood=0;
        workout.CompletionEventObservedForTest=()=>{observedIndex=workout.CurrentIndex;observedMood=mood[EmotionType.Confident];};
        try
        {
            workout.CompleteCurrentWorkout(); // actual runtime-bound MoodManager recipient and its native clock
            Assert.Equal(0,observedIndex);Assert.Equal(1d,observedMood);
            Assert.Equal(1,workout.CurrentIndex);Assert.Equal(1,workout.CompletedWorkouts);
        }
        finally{workout.CompletionEventObservedForTest=null;}
    }
    [Fact]
    public void CompletionPublicApiSkipsEmptyEventAndRequiresRealRecipientForNonemptyEvent()
    {
        var empty=new WorkoutComponent(Array.Empty<WorkoutConfig>());empty.CompleteCurrentWorkout();
        Assert.Equal(0,empty.CompletedWorkouts);
        Assert.False(empty.ShouldPlayEightiesMusic(_=>throw new Exception("mood read"),null,()=>throw new Exception("RNG read")));
        using var f=new Fixture();Configured(f);var native=f.Runtime.AI.Workout!.Workouts[0];
        var first=native with{EmotionEventOnComplete=""};var workout=new WorkoutComponent(new[]{first,native});
        workout.ClockSec=()=>throw new InvalidOperationException("empty event must skip clock");
        workout.TriggerEmotionEvent=(_,_)=>throw new InvalidOperationException("empty event must skip mood");
        workout.CompleteCurrentWorkout();Assert.Equal(1,workout.CurrentIndex);Assert.Equal(1,workout.CompletedWorkouts);
        workout.TriggerEmotionEvent=null;
        Assert.Throws<NativeActionUnavailable>(()=>workout.CompleteCurrentWorkout());
        Assert.Equal(1,workout.CurrentIndex);Assert.Equal(1,workout.CompletedWorkouts);
        workout.TriggerEmotionEvent=(_,_)=>true;workout.ClockSec=null;
        Assert.Throws<NativeActionUnavailable>(()=>workout.CompleteCurrentWorkout());
        Assert.Equal(1,workout.CurrentIndex);Assert.Equal(1,workout.CompletedWorkouts);
    }
    [Fact]
    public void FailedConfigurationCompletionSkipsNullCurrentAndRetainsPriorEntries()
    {
        const string valid="{\"strongLiftAnim\":\"WorkoutStrongLift_highEnergy\",\"weakLiftAnim\":\"WorkoutWeakLift_highEnergy\",\"numStrongLifts\":[],\"numWeakLifts\":[]}";
        const string missing="{\"strongLiftAnim\":\"WorkoutStrongLift_highEnergy\",\"numStrongLifts\":[],\"numWeakLifts\":[]}";
        var partial=Assert.Throws<WorkoutConfigurationException>(()=>WorkoutComponent.Parse("{\"workouts\":["+valid+","+missing+"]}")).Partial;
        var messages=new List<string>();SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=messages.Add;
        try
        {
            partial.CompleteCurrentWorkout();Assert.Null(partial.GetCurrentWorkout());Assert.Single(partial.Workouts);
            Assert.Equal(0,partial.CompletedWorkouts);
            Assert.Contains(messages,m=>m.Contains("M1-034/M13-010")&&m.Contains("00573DFC"));
        }
        finally{SteppedBehavior.MissingReported-=messages.Add;}
    }
    [Fact]
    public void CompletionReloadsCurrentAfterReentryAndGuardProtectsReplacement()
    {
        using var f=new Fixture();Configured(f);var config=f.Runtime.AI.Workout!.Workouts[0];
        var workout=new WorkoutComponent(new[]{config,config,config});int calls=0;
        workout.ClockSec=()=>9;workout.TriggerEmotionEvent=(_,_)=>{if(++calls==1)workout.CompleteCurrentWorkout();return true;};
        workout.CompleteCurrentWorkout();
        Assert.Equal(2,calls);Assert.Equal(2,workout.CurrentIndex);Assert.Equal(2,workout.CompletedWorkouts);
        var guarded=new WorkoutComponent(new[]{config,config});bool current=true;
        guarded.ClockSec=()=>9;guarded.TriggerEmotionEvent=(_,_)=>{current=false;return true;};
        guarded.CompleteCurrentWorkout(()=>current);
        Assert.Equal(1,guarded.CurrentIndex);Assert.Equal(1,guarded.CompletedWorkouts);
    }
    [Fact]
    public void PickupUsesActualIntentionTargetAndHelperParametersWithoutSyntheticSuccess()
    {
        using var f=new Fixture();var behavior=Configured(f);PreparePickup(f);
        Assert.True(f.Runtime.PickupTargets!.Refresh());Assert.Equal(7u,f.Runtime.PickupTargets.Best);
        var lights=new List<string>();f.Rig.Robot.Lights.Cubes.BehaviorStateCallObservedForTest=(id,call,trigger,layer,modifier,data,offset)=>lights.Add($"{id}:{call}:{trigger}");
        using var scope=new BehaviorScope(manager:f.Robot.BehaviorManagerOwner);Begin(f,behavior,scope);
        Assert.Contains(lights,x=>x.StartsWith("7:Play:"));
        Assert.Contains(f.Rig.Robot.Animations.Scheduler.IdleStack,x=>x.Lock==behavior.Id && (int)x.Trigger==0x23F);
        for(int trigger=0;trigger<21;trigger++)
            Assert.Equal(new[]{1,2,8,10,20}.Contains(trigger),f.Robot.BehaviorManagerOwner!.HasDisableLock((ReactionTrigger)trigger,behavior.Id+"_behaviorLock"));
        var helper=Assert.IsType<NativePickupBlockHelper>(behavior.PickupHelper);
        Assert.Equal(7u,helper.Target);Assert.Equal(0f,helper.MaxTurn);
        Assert.False(helper.Flag104);Assert.True(helper.Flag105);
        var pickup=Assert.IsAssignableFrom<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(AnimationTrigger.WorkoutPreLift_highEnergy,Assert.IsType<TriggerAnimationAction>(pickup.Children[0]).Request.Trigger);
        Assert.Equal(0x23F,helper.PreAnimationTrigger);
        int completed=f.Runtime.AI.Workout!.CompletedWorkouts;
        helper.Respond(new RobotCompletedAction(1,0x16,0x03000004,Array.Empty<uint>(),0));
        Assert.Equal(BehaviorStatus.Failure,helper.Status);
        f.Runtime.AI.Helpers.Update(f.Rig.Robot);
        Assert.Equal(completed,f.Runtime.AI.Workout.CompletedWorkouts);
        Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);
        behavior.Stop(BehaviorStopReason.Interrupted);
        scope.Dispose(); // native common cleanup releases the acquired scope locks and idle.
        Assert.Contains(lights,x=>x.StartsWith("7:StopResume:"));
        for(int trigger=0;trigger<21;trigger++)Assert.False(f.Robot.BehaviorManagerOwner!.HasDisableLock((ReactionTrigger)trigger,behavior.Id+"_behaviorLock"));
        Assert.DoesNotContain(f.Rig.Robot.Animations.Scheduler.IdleStack,x=>x.Lock==behavior.Id);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PickupHelperOnlyContinuesPostLiftOnSuccess(bool success)
    {
        using var f=new Fixture();var behavior=Configured(f);PreparePickup(f);
        Assert.True(f.Runtime.PickupTargets!.Refresh());
        using var scope=new BehaviorScope();Begin(f,behavior,scope);
        Assert.NotNull(behavior.PickupHelper);
        if(success)f.Robot.CarryingOwner!.SetCarrying(7); // supplied pickup result's carrying state
        Complete(f,success?0u:0x03000004u);
        if(!success)
        {
            Assert.IsType<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
            Complete(f,0);
        }
        f.Runtime.AI.Helpers.Update(f.Rig.Robot);
        if(success)
        {
            var post=Assert.IsAssignableFrom<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
            Assert.IsType<DriveStraightAction>(post.Children[0]);
        }
        else Assert.False(behavior.HasCurrentAction);
        Assert.Equal(0,f.Runtime.AI.Workout!.CompletedWorkouts);
        behavior.Stop(BehaviorStopReason.Interrupted);
    }
    [Fact]
    public void CarriedInitSnapshotsCountsAndFailureContinuesWithCurrentWorkoutTrigger()
    {
        using var f=new Fixture();var behavior=Configured(f);f.Add();f.Robot.CarryingOwner!.SetCarrying(7);
        var workout=f.Runtime.AI.Workout!;var mood=f.Robot.MoodOwner!;
        Assert.Equal(0d,mood[EmotionType.Confident]);
        const int expected=5; // shipped high-energy graph at Confident=0, 00573B64..92
        using var scope=new BehaviorScope();Begin(f,behavior,scope);
        Assert.Equal(7u,behavior.Target);Assert.Equal((5u,1u),behavior.Counts);
        var first=Assert.IsAssignableFrom<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(2,first.Children.Count);Assert.IsType<DriveStraightAction>(first.Children[0]);
        workout.CompleteCurrentWorkout();
        Complete(f,0x03000018);
        if(expected!=0)
        {
            var strong=Assert.IsType<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
            Assert.Equal(AnimationTrigger.WorkoutStrongLift_mediumEnergy,strong.Request.Trigger);
            Assert.Equal(expected,strong.Request.Argument2);
        }
        behavior.Stop(BehaviorStopReason.Interrupted);
    }
    [Fact]
    public void ShippedPositiveCountsDriveFullTransitionAndFinishAfterSuppliedFailures()
    {
        using var f=new Fixture();var behavior=Configured(f);f.Add();f.Robot.CarryingOwner!.SetCarrying(7);
        Assert.Equal(0d,f.Robot.MoodOwner![EmotionType.Confident]);
        var workout=f.Runtime.AI.Workout!;
        using var scope=new BehaviorScope();Begin(f,behavior,scope);
        Assert.Equal((5u,1u),behavior.Counts);
        Complete(f,0x03000018);
        var strong=Assert.IsType<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(AnimationTrigger.WorkoutStrongLift_highEnergy,strong.Request.Trigger);Assert.Equal(5,strong.Request.Argument2);
        Complete(f,0x03000018);
        var transition=Assert.IsType<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(AnimationTrigger.WorkoutTransition_highEnergy,transition.Request.Trigger);Assert.Equal(1,transition.Request.Argument2);
        Complete(f,0x03000018);
        var weak=Assert.IsType<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(AnimationTrigger.WorkoutWeakLift_highEnergy,weak.Request.Trigger);Assert.Equal(1,weak.Request.Argument2);
        Complete(f,0x03000018);
        var putdown=Assert.IsType<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(AnimationTrigger.WorkoutPutDown_highEnergy,putdown.Request.Trigger);Assert.Equal(1,putdown.Request.Argument2);
        f.Robot.CarryingOwner.UnsetCarrying();Complete(f,0x03000018);
        Assert.Equal(1,workout.CompletedWorkouts);Assert.Equal(1,workout.CurrentIndex);
        behavior.Stop(BehaviorStopReason.Interrupted);
    }
    [Fact]
    public void LostCarriedObjectStopsAtNextActionCompletionWithoutRewards()
    {
        using var f=new Fixture();var behavior=Configured(f);f.Add();f.Robot.CarryingOwner!.SetCarrying(7);
        using var scope=new BehaviorScope();Begin(f,behavior,scope);
        var workout=f.Runtime.AI.Workout!;int before=workout.CompletedWorkouts;
        f.Robot.CarryingOwner.UnsetCarrying();behavior.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        Complete(f,0x03000018);
        Assert.False(behavior.HasCurrentAction);Assert.Equal(before,workout.CompletedWorkouts);
        behavior.Stop(BehaviorStopReason.Interrupted);
    }
    [Fact]
    public void OrderedFloatAccumulationAffectsRoundedCount()
    {
        static EmotionScorer S(double y)=>new(EmotionType.Confident,new Graph2d(new[]{(0d,y)}),false);
        var score=WorkoutConfig.EvaluateEmotionScore(new[]{S(16777216),S(1),S(1),S(-16777216)},_=>0,null);
        Assert.Equal(0d,score);Assert.Equal(0,WorkoutConfig.MoodScoreHelper(score));
    }
    [Fact]
    public void PublicLiftCountUsesMeanAndRoundAwayFromZero()
    {
        using var f=new Fixture();Configured(f);var config=f.Runtime.AI.Workout!.Workouts[0];
        static EmotionScorer S(double y)=>new(EmotionType.Confident,new Graph2d(new[]{(0d,y)}),false);
        config=config with{NumStrongLifts=new[]{S(2),S(3)}};
        Assert.Equal(3,config.GetNumStrongLifts(_=>0)); // roundf((2+3)/2) = 3
        config=config with{NumStrongLifts=new[]{S(1) with{TrackDelta=true}}};
        Assert.Throws<NotSupportedException>(()=>config.GetNumStrongLifts(_=>0));
        Assert.Throws<NativeActionUnavailable>(()=>WorkoutConfig.MoodScoreHelper(2147483648d));
    }
    [Fact]
    public void NativeUnsignedCountPreservesHighBitThroughActualTriggerAction()
    {
        using var f=new Fixture();var behavior=Configured(f);f.Add();f.Robot.CarryingOwner!.SetCarrying(7);
        var workout=f.Runtime.AI.Workout!;var configs=Assert.IsType<List<WorkoutConfig>>(workout.Workouts);
        var high=new EmotionScorer(EmotionType.Confident,new Graph2d(new[]{(0d,2147483648d)}),false);
        configs[0]=configs[0] with{NumStrongLifts=new[]{high}}; // supplied valid finite scorer
        Assert.Equal(0u,WorkoutConfig.NativeMoodScoreHelper(0));
        Assert.Equal(0x80000000u,WorkoutConfig.NativeMoodScoreHelper(2147483648d));
        Assert.Throws<NativeActionUnavailable>(()=>WorkoutConfig.NativeMoodScoreHelper(4294967296d));
        using var scope=new BehaviorScope();Begin(f,behavior,scope);
        Assert.Equal(0x80000000u,behavior.Counts.Strong);
        Complete(f,0x03000018);
        var action=Assert.IsType<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(int.MinValue,action.Request.Argument2);
        var loops=typeof(TriggerAnimationAction).GetField("_loops",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Assert.Equal(0x80000000u,Assert.IsType<uint>(loops!.GetValue(action)));
        behavior.Stop(BehaviorStopReason.Interrupted);
    }
    [Fact]
    public void ParserRetainsSuccessfulPrefixWithoutPublishingCurrentOnRequiredTriggerFailure()
    {
        const string valid="{\"strongLiftAnim\":\"NoSuchTrigger\",\"weakLiftAnim\":\"WorkoutWeakLift_highEnergy\",\"numStrongLifts\":[],\"numWeakLifts\":[]}";
        const string missing="{\"strongLiftAnim\":\"WorkoutStrongLift_highEnergy\",\"numStrongLifts\":[],\"numWeakLifts\":[]}";
        var diagnostics=new List<string>();
        var error=Assert.Throws<WorkoutConfigurationException>(()=>WorkoutComponent.Parse("{\"workouts\":["+valid+","+missing+"]}",diagnostics.Add));
        Assert.Single(error.Partial.Workouts);Assert.Null(error.Partial.GetCurrentWorkout());
        Assert.Equal((AnimationTrigger)0,error.Partial.Workouts[0].StrongLift);
        Assert.Equal((AnimationTrigger)0x23F,error.Partial.Workouts[0].PostLift);
        Assert.Single(diagnostics);
    }
    [Fact]
    public void ActionConstructionReentryCannotReplaceNewRunWithOldSequence()
    {
        using var f=new Fixture();var behavior=Configured(f);f.Add(7);f.Add(8,300);
        f.Robot.CarryingOwner!.SetCarrying(7);
        using var firstScope=new BehaviorScope();using var secondScope=new BehaviorScope();
        IActionRunner? replacement=null;uint replacementTag=0;bool entered=false;
        behavior.ActionConstructedForTest=old=>
        {
            if(entered)return;entered=true;behavior.ActionConstructedForTest=null;
            behavior.Stop(BehaviorStopReason.Interrupted);f.Robot.CarryingOwner.SetCarrying(8);
            Begin(f,behavior,secondScope);replacement=behavior.OwnedRunner;replacementTag=behavior.CurrentRunnerTag;
        };
        Begin(f,behavior,firstScope);
        Assert.True(entered);Assert.NotNull(replacement);Assert.Same(replacement,behavior.OwnedRunner);
        Assert.Equal(replacementTag,behavior.CurrentRunnerTag);Assert.Equal(8u,behavior.Target);
        behavior.Stop(BehaviorStopReason.Interrupted);
    }
    [Fact]
    public void TriggerDiagnosticReentryCannotAppendOldChildToReplacement()
    {
        using var f=new Fixture();var behavior=Configured(f);f.Add(7);f.Add(8,300);
        f.Robot.CarryingOwner!.SetCarrying(7);
        using var firstScope=new BehaviorScope();using var secondScope=new BehaviorScope();
        IActionRunner? replacement=null;uint tag=0;bool entered=false;
        void Missing(string message)
        {
            if(entered||!message.Contains("M8-021 trigger DAS"))return;
            entered=true;behavior.Stop(BehaviorStopReason.Interrupted);f.Robot.CarryingOwner.SetCarrying(8);
            Begin(f,behavior,secondScope);replacement=behavior.OwnedRunner;tag=behavior.CurrentRunnerTag;
        }
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Missing;
        try
        {
            Begin(f,behavior,firstScope);
            Assert.True(entered);Assert.NotNull(replacement);Assert.Same(replacement,behavior.OwnedRunner);
            Assert.Equal(tag,behavior.CurrentRunnerTag);Assert.Equal(8u,behavior.Target);
            behavior.Stop(BehaviorStopReason.Interrupted);
        }
        finally{SteppedBehavior.MissingReported-=Missing;}
    }
    [Fact]
    public void PickupHelperDiagnosticReentryRetiresOldHelperBeforeReplacementRun()
    {
        using var f=new Fixture();var behavior=Configured(f);PreparePickup(f);
        Assert.True(f.Runtime.PickupTargets!.Refresh());
        using var firstScope=new BehaviorScope();using var secondScope=new BehaviorScope();
        IActionRunner? replacement=null;uint tag=0;bool entered=false;
        void Missing(string message)
        {
            if(entered||!message.Contains("M8-021 trigger DAS"))return;
            entered=true;behavior.Stop(BehaviorStopReason.Interrupted);f.Robot.CarryingOwner!.SetCarrying(7);
            Begin(f,behavior,secondScope);replacement=behavior.OwnedRunner;tag=behavior.CurrentRunnerTag;
        }
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Missing;
        try
        {
            Begin(f,behavior,firstScope);
            Assert.True(entered);Assert.NotNull(replacement);Assert.Same(replacement,behavior.OwnedRunner);
            Assert.Equal(tag,behavior.CurrentRunnerTag);Assert.Equal(0,f.Runtime.AI.Helpers.StackCount);
            behavior.Stop(BehaviorStopReason.Interrupted);
        }
        finally{SteppedBehavior.MissingReported-=Missing;}
    }
    [Fact]
    public void StageDiagnosticReentryCannotOverwriteReplacementStageOrAction()
    {
        using var f=new Fixture();var behavior=Configured(f);f.Add(7);f.Add(8,300);
        f.Robot.CarryingOwner!.SetCarrying(7);f.Rig.Engine.ContextRandom.WordSourceForTest=()=>0;
        using var firstScope=new BehaviorScope();using var secondScope=new BehaviorScope();
        IActionRunner? replacement=null;uint tag=0;bool entered=false;
        f.Robot.BehaviorPublicState.StageNameInput=value=>
        {
            if(!entered)
            {
                entered=true;behavior.Stop(BehaviorStopReason.Interrupted);f.Robot.CarryingOwner.SetCarrying(8);
                Begin(f,behavior,secondScope);replacement=behavior.OwnedRunner;tag=behavior.CurrentRunnerTag;
            }
            return value.ToString();
        };
        try
        {
            Begin(f,behavior,firstScope);
            Assert.True(entered);Assert.NotNull(replacement);Assert.Same(replacement,behavior.OwnedRunner);
            Assert.Equal(tag,behavior.CurrentRunnerTag);Assert.Equal(8u,behavior.Target);
            Assert.Equal(3,f.Robot.BehaviorPublicState.State.StageTag);
            behavior.Stop(BehaviorStopReason.Interrupted);
        }
        finally{f.Robot.BehaviorPublicState.StageNameInput=null;f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }
    [Fact]
    public void NestedDriveUnavailableDiagnosticCannotStopReplacement()
    {
        using var f=new Fixture();var behavior=Configured(f);f.Add(7,250);
        Assert.True(f.Runtime.PickupTargets!.Refresh());Assert.Equal(7u,f.Runtime.PickupTargets.Best);
        using var firstScope=new BehaviorScope();using var secondScope=new BehaviorScope();
        Begin(f,behavior,firstScope);
        var drive=Assert.IsType<NativeDriveToHelper>(behavior.PickupHelper!.DriveHelper);
        var runner=Assert.IsType<NativeDriveToObjectRunner>(drive.Runner);
        IActionRunner? replacement=null;uint tag=0;bool entered=false;
        void Missing(string message)
        {
            if(entered||!message.Contains("supplied DriveTo unavailability"))return;
            entered=true;behavior.Stop(BehaviorStopReason.Interrupted);f.Robot.CarryingOwner!.SetCarrying(7);
            Begin(f,behavior,secondScope);replacement=behavior.OwnedRunner;tag=behavior.CurrentRunnerTag;
        }
        SteppedBehavior.MissingReported+=Missing;
        try
        {
            runner.Unavailable!(new NativeActionUnavailable("supplied DriveTo unavailability"));
            Assert.True(entered);Assert.NotNull(replacement);Assert.Same(replacement,behavior.OwnedRunner);
            Assert.Equal(tag,behavior.CurrentRunnerTag);
            behavior.Stop(BehaviorStopReason.Interrupted);
        }
        finally{SteppedBehavior.MissingReported-=Missing;}
    }
    [Fact]
    public void NestedSearchPublicationReentryCannotQueueOverReplacement()
    {
        using var f=new Fixture();var behavior=Configured(f);f.Add(7,250);
        Assert.True(f.Runtime.PickupTargets!.Refresh());Assert.Equal(7u,f.Runtime.PickupTargets.Best);
        using var firstScope=new BehaviorScope();using var secondScope=new BehaviorScope();
        Begin(f,behavior,firstScope);
        var drive=Assert.IsType<NativeDriveToHelper>(behavior.PickupHelper!.DriveHelper);
        Assert.IsType<NativeDriveToObjectRunner>(drive.Runner);
        Complete(f,0x0300001D); // supplied terminal DriveTo result selects the real search helper
        var search=Assert.IsType<NativeSearchForBlockHelper>(drive.SearchHelper);
        IActionRunner? replacement=null;uint tag=0;bool entered=false;
        search.BeforeActionPublicationForTest=()=>
        {
            if(entered)return;entered=true;behavior.Stop(BehaviorStopReason.Interrupted);
            f.Robot.CarryingOwner!.SetCarrying(7);Begin(f,behavior,secondScope);
            replacement=behavior.OwnedRunner;tag=behavior.CurrentRunnerTag;
        };
        f.Runtime.AI.Helpers.Update(f.Rig.Robot);
        Assert.True(entered);Assert.NotNull(replacement);Assert.Same(replacement,behavior.OwnedRunner);
        Assert.Equal(tag,behavior.CurrentRunnerTag);Assert.True(search.Retired);
        behavior.Stop(BehaviorStopReason.Interrupted);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PutDownUsesSentinelForManualOtherwiseChecksAndSearches(bool sentinel)
    {
        using var f=new Fixture();var behavior=Configured(f);f.Add();f.Robot.CarryingOwner!.SetCarrying(7);
        var configured=f.Runtime.AI.Workout!.GetCurrentWorkout()! with {
            NumStrongLifts=Array.Empty<EmotionScorer>(),NumWeakLifts=Array.Empty<EmotionScorer>(),
            Transition=(AnimationTrigger)0x23F,PutDown=sentinel?(AnimationTrigger)0x23F:AnimationTrigger.WorkoutPutDown_highEnergy,
            EmotionEventOnComplete="",AdditionalObjectiveOnComplete="" };
        var workout=new WorkoutComponent(new[]{configured});f.Runtime.AI.Workout=workout;
        using var scope=new BehaviorScope();Begin(f,behavior,scope);
        Complete(f,0x03000018); // direct callback ignores failure, zero counts reach putdown
        if(sentinel)
            Assert.IsAssignableFrom<NativePlaceGroundRunner>(f.Robot.ActionList.GetCurrentAction());
        else
        {
            var putdown=Assert.IsType<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
            Assert.Equal(AnimationTrigger.WorkoutPutDown_highEnergy,putdown.Request.Trigger);
            Complete(f,0x03000018);
            var check=Assert.IsAssignableFrom<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
            Assert.Equal(2,check.Children.Count);
            var lift=check.Children[0];var liftType=lift.GetType();
            Assert.Equal("MoveAction",liftType.Name);
            Assert.Equal(32f,(float)liftType.GetField("RequestedHeight")!.GetValue(lift)!);
            Assert.Equal(5f,(float)liftType.GetField("Tolerance")!.GetValue(lift)!);
            Assert.IsType<NativeNearbySearchRunner>(check.Children[1]);
        }
        // Still carrying: EndIteration performs the source detach (0x005D89FA).
        Complete(f,0x03000018);
        Assert.Null(f.Robot.CarryingOwner.CarriedObjectId);
        Assert.Equal(1,workout.CompletedWorkouts);Assert.Equal(0,workout.CurrentIndex);
        behavior.Stop(BehaviorStopReason.Interrupted);
    }
    [Fact]
    public void FinishedActionReportsObjectiveNeedsOptionalObjectiveThenMoodAndAdvance()
    {
        using var f=new Fixture();var behavior=Configured(f);f.Add();f.Robot.CarryingOwner!.SetCarrying(7);
        var first=f.Runtime.AI.Workout!.GetCurrentWorkout()! with {
            NumStrongLifts=Array.Empty<EmotionScorer>(),NumWeakLifts=Array.Empty<EmotionScorer>(),
            Transition=(AnimationTrigger)0x23F,PutDown=(AnimationTrigger)0x23F };
        var workout=new WorkoutComponent(new[]{first,first});f.Runtime.AI.Workout=workout;
        var observed=new List<string>();
        workout.TriggerEmotionEvent=(name,_)=>{observed.Add("mood:"+name);return true;};
        workout.ClockSec=()=>f.Rig.Engine.Timer.SecondsF;
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>observed.Add("objective:"+p));
        void Need(string line){if(line.StartsWith("needs.action_completed Workout:"))observed.Add("needs");}
        f.Rig.Engine.Needs.Log+=Need;
        try
        {
            using var scope=new BehaviorScope();Begin(f,behavior,scope);
            Complete(f,0x03000018);Assert.IsAssignableFrom<NativePlaceGroundRunner>(f.Robot.ActionList.GetCurrentAction());
            f.Robot.CarryingOwner.UnsetCarrying();Complete(f,0x03000018);
            Assert.Equal(new[]{"objective:18","needs","objective:19","mood:StrongWorkoutCompleted"},observed);
            Assert.Equal(1,workout.CurrentIndex);Assert.Equal(1,workout.CompletedWorkouts);
            behavior.Stop(BehaviorStopReason.Interrupted);
        }
        finally{f.Rig.Engine.Needs.Log-=Need;}
    }
}
