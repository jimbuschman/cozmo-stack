using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Behavior;

// fidelity: M15-089
// Ordinary and Spark configured CubeLiftWorkout: shared class 005D7E50..005D8A54; factory 0059CA12..22.
// Shared helper, action, carrying, mood, needs and public-state owners retain their own gaps.
// fidelity: M13-010
internal sealed class NativeCubeLiftWorkoutBehavior : SteppedBehavior, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly string? _needsAction;
    private long _generation;
    private int _retired;
    private bool _shouldCarry;
    private uint _strong, _weak;
    internal uint Target { get; private set; } = uint.MaxValue;
    internal NativePickupBlockHelper? PickupHelper { get; private set; }
    internal (uint Strong,uint Weak) Counts => (_strong,_weak);
    internal Action<IActionRunner>? ActionConstructedForTest;

    internal NativeCubeLiftWorkoutBehavior(BehaviorFactoryContext source, JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(), "CubeLiftWorkout")
    {
        _m=source.Manipulation!; _robot=source.NativeRobot!;
        _needsAction=config.TryGetProperty("needsActionID",out var needs)?needs.GetString():null;
    }
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_m.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version)=>Volatile.Read(ref _retired)==0&&Interlocked.Read(ref _generation)==version;
    protected override bool HostStopContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0&&Interlocked.Read(ref _generation)==unchecked(version+1);
    protected override bool HostResumeContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0&&(Interlocked.Read(ref _generation)==version||Interlocked.Read(ref _generation)==unchecked(version+1));
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>true;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    protected override int ResumeInternal()=>1;
    private bool Current(long epoch)=>HostExecutionVersionCurrent(epoch);
    private uint Carry()=>_robot.CarryingOwner is {} carrying?carrying.CarriedObjectId??uint.MaxValue:
        throw new NativeActionUnavailable("actual CarryingComponent unavailable");
    private WorkoutComponent Workout(BehaviorContext context)=>context.AI?.Workout is { } workout && workout.GetCurrentWorkout() is not null?workout:
        throw new NativeActionUnavailable("M1-034/M13-010 actual AI workout/current configuration unavailable; skip Init 0x005D7EE6 null-current read 0x00573B70");
    private WorkoutComponent Workout()=>Workout(Context);
    private WorkoutConfig Config()=>Workout().GetCurrentWorkout()!;
    private uint Best()=>_robot.NativeBehaviorRuntime?.PickupTargets?.GetBest(0)??
        throw new NativeActionUnavailable("actual intention 0 cache unavailable");
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context); long epoch=CaptureHostExecutionVersion();
        if(context.Robot.Animations.Library is null){MarkReadinessUnavailable();return false;}
        if(!Current(epoch)||!IsRunnableBase(context)||!Current(epoch))return false;
        return Runnable(context,epoch);
    }
    protected override bool IsRunnableInternal(BehaviorContext context)=>Runnable(context,CaptureHostExecutionVersion());
    private bool Runnable(BehaviorContext context,long epoch)
    {
        try { _=Workout(context); _=_robot.MoodOwner??throw new NativeActionUnavailable("M1-034/M13-010 actual MoodManager unavailable; count scoring 0x0067C9F0/0x0067CA02"); uint carried=Carry(); if(!Current(epoch))return false; if(carried!=uint.MaxValue)return true; uint best=Best();return Current(epoch)&&best!=uint.MaxValue; }
        catch(Exception error) when(error is NativeActionUnavailable or NativeConfigurationUnavailable)
        { if(Current(epoch)){MarkReadinessUnavailable();ReportMissing("CubeLiftWorkout readiness: "+error.Message);}return false; }
    }
    protected override void OnStart()
    {
        long epoch=CaptureHostExecutionVersion();
        try
        {
            if(!Scope.SmartDisableReactionsWithLockGuarded(Id,ReactionLockTables.CubeLiftWorkout,()=>Current(epoch)))return;
            var scheduler=_m.Robot.Animations.Scheduler;
            if(!Scope.SmartPushIdleAnimationGuarded(()=>scheduler.PushIdleAnimation((AnimationTrigger)0x23F,Id),
                ()=>scheduler.RemoveIdleAnimation(Id,_robot.Engine.Timer.TimeStampMs),()=>Current(epoch)))return;
            var workout=Config(); var mood=_robot.MoodOwner??throw new NativeActionUnavailable("M13-010 actual MoodManager unavailable");
            _strong=WorkoutConfig.NativeMoodScoreHelper(WorkoutConfig.EvaluateEmotionScore(workout.NumStrongLifts,e=>mood[e],e=>mood.GetHistoryValueTicksAgo(e,60)));
            _weak=WorkoutConfig.NativeMoodScoreHelper(WorkoutConfig.EvaluateEmotionScore(workout.NumWeakLifts,e=>mood[e],e=>mood.GetHistoryValueTicksAgo(e,60)));
            if(!Current(epoch))return;
            uint carried=Carry(); if(!Current(epoch))return;
            if(carried!=uint.MaxValue){_shouldCarry=true;Target=carried;PostLift(epoch);}
            else{_shouldCarry=false;uint target=Best();if(!Current(epoch))return;Target=target;PickingUp(epoch);}
        }
        catch(Exception error) when(error is NotSupportedException or NativeConfigurationUnavailable)
        { if(Current(epoch)){Unavailable(error.Message,epoch);if(Current(epoch))InitFailed=true;} }
    }
    private void Unavailable(string reason,long epoch)
    {
        if(!Current(epoch))return;
        uint tag=CurrentRunnerTag;
        ReportMissing("CubeLiftWorkout: "+reason+"; host cleanup is not native completion");
        if(!Current(epoch))return;
        _robot.ActionList.DeferHostCleanup(()=>
        {
            if(!Current(epoch)||CurrentRunnerTag!=tag)return;
            if(ReferenceEquals(_robot.BehaviorManagerOwner?.Current,this))_robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,_robot.Engine.Timer.SecondsF);
            else Stop(BehaviorStopReason.Interrupted);
        });
    }
    private void Invoke(long epoch,Action next)
    {
        if(!Current(epoch))return;
        try{next();}
        catch(Exception error) when(error is NotSupportedException or NativeConfigurationUnavailable)
        {if(Current(epoch))Unavailable(error.Message,epoch);}
    }
    private void Act(IActionRunner runner,Action next,long epoch)
    {
        if(!Current(epoch)){if(runner is ActionRunner discarded)discarded.RetireNeverAdmittedTree();return;}
        ActionConstructedForTest?.Invoke(runner);
        if(!Current(epoch)){if(runner is ActionRunner discarded)discarded.RetireNeverAdmittedTree();return;}
        StartActing(runner,_=>Invoke(epoch,next),()=>Current(epoch));
    }
    private TriggerAnimationAction Trigger(AnimationTrigger trigger,int loops)=>new(_m.Robot,new(trigger,loops,true,0,60f,false));
    private CompoundActionSequential Sequence(long epoch)=>new WorkoutSequence(this,epoch);
    private bool Add(CompoundActionSequential sequence,ActionRunner child,bool ignore,long epoch)
    {
        if(Current(epoch)&&sequence.AddActionGuarded(child,ignore,()=>Current(epoch)))return true;
        child.RetireNeverAdmittedTree();sequence.RetireNeverAdmittedTree();return false;
    }
    private void PickingUp(long epoch)
    {
        if(!Current(epoch))return;
        _m.Robot.Lights.Cubes.PlayBehaviorStateLight(Target,0x27,false,BehaviorStateLightInfo.Zero(),()=>Current(epoch));
        if(!Current(epoch))return;
        var config=Config(); var ai=Context.AI??throw new NativeActionUnavailable("actual AI helper owner unavailable");
        var helper=new NativePickupBlockHelper(_m,this,ai.Helpers.Factory,Target,0f,false,true,(int)config.PreLift,()=>Current(epoch));
        PickupHelper=helper; IHelper? raw=helper; var handle=ai.Helpers.AddHelperToComponent(ref raw);
        try{if(Current(epoch))SmartDelegateToHelper(handle,_=>Invoke(epoch,()=>PostLift(epoch)),null,()=>Current(epoch));}
        finally{handle.Release();}
    }
    private void PostLift(long epoch)
    {
        if(!Current(epoch))return;
        _shouldCarry=true; var config=Config(); var sequence=Sequence(epoch);sequence.SeedNativeEmptyCompletion();
        if(!Add(sequence,new DriveStraightAction(_m,-20f,100f,true),true,epoch))return;
        if((int)config.PostLift!=0x23F&&!Add(sequence,Trigger(config.PostLift,1),false,epoch))return;
        Act(sequence,()=>Strong(epoch),epoch);
        if(!Current(epoch))return;
        var workout=Workout();var mood=_robot.MoodOwner??throw new NativeActionUnavailable("M13-010 actual MoodManager unavailable");
        bool play=workout.ShouldPlayEightiesMusic(e=>mood[e],e=>mood.GetHistoryValueTicksAgo(e,60),()=>_robot.Engine.ContextRandom.RandDbl(1),()=>Current(epoch));
        if(!Current(epoch))return;
        if(play)_robot.BehaviorPublicState.UpdateBroadcastBehaviorStage(3,2,()=>Current(epoch));
    }
    private void Strong(long epoch)
    {
        if(!Current(epoch))return;
        if(_strong==0){WeakPose(epoch);return;}
        Act(Trigger(Config().StrongLift,unchecked((int)_strong)),()=>WeakPose(epoch),epoch);
    }
    private void WeakPose(long epoch)
    {
        if(!Current(epoch))return;
        var trigger=Config().Transition;
        if((int)trigger==0x23F){Weak(epoch);return;}
        Act(Trigger(trigger,1),()=>Weak(epoch),epoch);
    }
    private void Weak(long epoch)
    {
        if(!Current(epoch))return;
        if(_weak==0){PuttingDown(epoch);return;}
        Act(Trigger(Config().WeakLift,unchecked((int)_weak)),()=>PuttingDown(epoch),epoch);
    }
    private void PuttingDown(long epoch)
    {
        if(!Current(epoch))return;
        _shouldCarry=false;var trigger=Config().PutDown;
        if((int)trigger==0x23F){ManualPutDown(epoch);return;}
        Act(Trigger(trigger,1),()=>CheckPutDown(epoch),epoch);
    }
    private void ManualPutDown(long epoch)
    {
        if(!Current(epoch))return;
        if(Carry()==uint.MaxValue){EndIteration(epoch);return;}
        Act(new WorkoutGroundRunner(this,epoch),()=>EndIteration(epoch),epoch);
    }
    private void CheckPutDown(long epoch)
    {
        if(!Current(epoch))return;
        _m.Robot.Lights.Cubes.StopBehaviorStateLight(Target,0x27,true,()=>Current(epoch));
        if(!Current(epoch))return;
        if(Carry()==uint.MaxValue){EndIteration(epoch);return;}
        var sequence=Sequence(epoch);sequence.SeedNativeEmptyCompletion();
        if(!Add(sequence,_m.Robot.Motion.CreateGoToSleepLiftChild(),false,epoch))return;
        if(!Add(sequence,new NativeNearbySearchRunner(_m,Target,-20f,100f,BitConverter.UInt32BitsToSingle(0xBDB2B8C2)),false,epoch))return;
        Act(sequence,()=>EndIteration(epoch),epoch);
    }
    private void EndIteration(long epoch)
    {
        if(!Current(epoch))return;
        if(Carry()!=uint.MaxValue){_m.Docking.ReleaseCarriedObject(forget:false);if(!Current(epoch))return;}
        _robot.Engine.ReportBehaviorObjective(0x12,"PerformedWorkout",Id);if(!Current(epoch))return;
        if(_needsAction is null)throw new NativeActionUnavailable("configured needsActionID unavailable");
        _robot.Engine.Needs.RegisterNeedsActionCompleted(_needsAction);if(!Current(epoch))return;
        var workout=Workout();var config=workout.GetCurrentWorkout()!;
        if(config.AdditionalObjectiveOnComplete.Length!=0)
        {
            if(config.AdditionalObjectiveOnComplete!="PerformedStrongWorkout")
                throw new NativeActionUnavailable("configured additional workout objective numeric mapping unavailable: "+config.AdditionalObjectiveOnComplete);
            _robot.Engine.ReportBehaviorObjective(0x13,"PerformedStrongWorkout",Id);if(!Current(epoch))return;
        }
        workout.CompleteCurrentWorkout(()=>Current(epoch));
    }
    protected override void OnUpdate()
    {
        long epoch=CaptureHostExecutionVersion();
        try { if(_shouldCarry&&Carry()==uint.MaxValue&&Current(epoch))StopOnNextActionComplete(); }
        catch(NativeActionUnavailable error){if(Current(epoch))Unavailable(error.Message,epoch);}
    }
    protected override void OnStop(BehaviorStopReason reason)
    {
        long stopped=Interlocked.Increment(ref _generation);
        bool CurrentStop()=>Volatile.Read(ref _retired)==0&&Interlocked.Read(ref _generation)==stopped;
        _robot.BehaviorPublicState.UpdateBroadcastBehaviorStage(4,0,CurrentStop);if(!CurrentStop())return;
        _m.Robot.Lights.Cubes.StopBehaviorStateLight(Target,0x27,true,CurrentStop);if(!CurrentStop())return;
        Context.AI?.Workout?.ResetEightiesMusicDecision();
    }
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);RetireActionCompletionOwner();}

    private sealed class WorkoutSequence(NativeCubeLiftWorkoutBehavior owner,long epoch)
        :CompoundActionSequential(()=>owner._robot.Engine.Timer.SecondsF)
    {
        private bool _unavailable;
        public override uint Update()
        {
            if(_unavailable)return EngineActionResult.Running;
            int depth=Watcher?.HostUpdateDepth??0;
            try{return base.Update();}
            catch(Exception error) when(error is NativeActionUnavailable or NativeConfigurationUnavailable)
            {
                Watcher?.UnwindUnavailableHostUpdate(depth);_unavailable=true;
                owner.Unavailable(error.Message,epoch);return EngineActionResult.Running;
            }
        }
    }
    private sealed class WorkoutGroundRunner(NativeCubeLiftWorkoutBehavior owner,long epoch):NativePlaceGroundRunner(owner._m)
    {
        private bool _unavailable;
        public override uint Update()
        {
            if(_unavailable)return EngineActionResult.Running;
            int depth=Watcher?.HostUpdateDepth??0;
            try{return base.Update();}
            catch(Exception error) when(error is NativeActionUnavailable or NativeConfigurationUnavailable)
            {
                Watcher?.UnwindUnavailableHostUpdate(depth);_unavailable=true;
                owner.Unavailable(error.Message,epoch);return EngineActionResult.Running;
            }
        }
    }
}
