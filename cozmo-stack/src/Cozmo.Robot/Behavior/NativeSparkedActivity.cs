using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// fidelity: M15-059
// S1-S28/H1-H12: actual configured Spark owner; raw parent picking remains M15-006.
internal sealed class NativeSparkedActivity : IDisposable
{
    private readonly Activity _activity;
    private readonly EngineRobot _robot;
    private readonly BehaviorManager _manager;
    private readonly CozmoRobot _facade;
    private readonly NativePlayArbitraryAnimBehavior _animation;
    private readonly RetainedSubscription? _objective,_observed;
    private readonly SortedSet<uint> _observedIds=new();
    private readonly SortedSet<string> _locks=new(StringComparer.Ordinal);
    private readonly Dictionary<string,ScopedAcquisition> _scopedLocks=new(StringComparer.Ordinal);
    private BodyLightComponent.Locator _lights=new();
    private ScopedAcquisition? _drivingLease,_idleLease;
    private bool _idle,_hardOutputs,_lightsOwned,_interrupted;
    private long _generation;
    private bool _retired;
    internal bool InputUnavailable { get; private set; }
    private bool Current(long generation)=>!_retired && generation==_generation;
    internal uint Count {get;private set;}
    internal int State {get;private set;}
    internal float Started {get;private set;}
    internal float Minimum {get;set;}
    internal float Maximum {get;set;}
    internal byte Repetitions {get;set;}
    internal bool WaitForAction {get;set;}=true;
    private readonly bool _hasDelegate;
    private bool ScopedOwner=>_delegate is not null || _activity.Id=="SparksWorkout";
    private readonly Activity? _delegate;
    internal Activity? Delegate => _delegate;
    internal bool CanExecute=>WaitForAction&&(!_hasDelegate || _delegate?.GatherOwner is not null);
    internal IReadOnlyCollection<uint> Observed=>_observedIds;
    internal AnimationTrigger Success {get;set;}=(AnimationTrigger)0x216;
    internal AnimationTrigger Failure {get;set;}=(AnimationTrigger)0x210;
    private readonly AnimationTrigger _softTrigger;
    private readonly uint _objectiveId;
    private BehaviorManager Manager=>_manager;
    private CozmoRobot Facade=>_facade;
    private static readonly ReactionLockTable SelectedLock=new("ActivitySparked","0xC624BC",new[]{1,2,3,4,5,10});
    private static readonly ReactionLockTable FinalLock=new("finalAnimLockReactions","0xC624E6",new[]{1,2,4,8});
    internal NativeSparkedActivity(Activity activity,EngineRobot robot,JsonElement config,IReadOnlyDictionary<string,IBehavior> bound,Activity? nested=null)
    {
        _activity=activity;_robot=robot;_delegate=nested;
        _manager=robot.BehaviorManagerOwner??throw new InvalidOperationException("MISSING: Sparked actual BehaviorManager owner unavailable");
        _facade=_manager.ContextRobot;
        _animation=(NativePlayArbitraryAnimBehavior)bound["PlayArbitraryAnim"];
        Minimum=config.GetProperty("minTimeSecs").GetSingle();Maximum=config.GetProperty("maxTimeSecs").GetSingle();
        Repetitions=unchecked((byte)config.GetProperty("numberOfRepetitions").GetInt32());
        _objectiveId=(activity.Id,config.GetProperty("behaviorObjective").GetString()) switch
        {
            ("SparksKnockOverCubes","KnockedOverBlocks")=>13u,
            ("SparksPopAWheelie","PoppedWheelie")=>22u, // Sparks Pop S2: exact native objective table01033198.
            ("SparksStackBlock","StackedBlock")=>40u, // S1 actual native objective40, not a generalized parser.
            ("SparksGatherCubes","GatheredCubes")=>11u,
            ("SparksWorkout","PerformedWorkout")=>18u, // Spark workout P1/A13: asset and 005B181E..32.
            ("SparksRollBlock","BlockRolled")=>2u, // Sparks Roll S2: actual objective table01033148.
            _=>throw new InvalidOperationException("MISSING: changed configured Spark objective is outside the checked instance mappings")
        };
        if(config.TryGetProperty("maxTimeoutForActionComplete",out var wait))WaitForAction=wait.GetBoolean();
        _hasDelegate=config.TryGetProperty("subActivityDelegate",out _);
        _softTrigger=config.TryGetProperty("softSparkTrigger",out var soft)?Enum.Parse<AnimationTrigger>(soft.GetString()!):(AnimationTrigger)0x23F;
        if(robot.HasExternalInterface)
        {
            _objective=robot.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint value&&value==_objectiveId)Count=unchecked(Count+1);});
            _observed=robot.Engine.AnimationMessages.SubscribeMessage(0x44,true,p=>{if(p is NativeObservedObject value&&unchecked(value.Family-1)<=1)_observedIds.Add(value.ObjectId);});
        }
    }
    internal void Selected()
    {
        long generation=++_generation;InputUnavailable=false;
        Started=_robot.Engine.Timer.SecondsF;Count=0;State=0;_interrupted=false;_hardOutputs=false;_observedIds.Clear();
        if(Manager.RequestedSparkId==0x55||Manager.RequestedSparkSoft==0)
        {
            if(ScopedOwner){_lights=new();_lightsOwned=true;}
            if(ScopedOwner)
            {
                var lease=new ScopedAcquisition();_drivingLease=lease;
                Facade.Animations.DrivingAnimations.PushOwned(new((AnimationTrigger)0x20E,(AnimationTrigger)0x20D,(AnimationTrigger)0x20F),_activity.Id,lease);
            }
            else Facade.Animations.DrivingAnimations.Push(new((AnimationTrigger)0x20E,(AnimationTrigger)0x20D,(AnimationTrigger)0x20F),_activity.Id);
            if(!Current(generation))return;
            PushIdle((AnimationTrigger)0x213);
            if(!Current(generation))return;
            if(!ScopedOwner){_lights=new();_lightsOwned=true;}
            Facade.Lights.Body.StartLooping(HardLights(),0,_lights);if(!Current(generation))return;
            _hardOutputs=true;
            _robot.Engine.ReportHardSparkStarted(Manager.RequestedSparkId);if(!Current(generation))return;
        }
        Lock(_activity.Id,SelectedLock);
        if(!Current(generation))return;
        _delegate?.OnSelected(_robot.Engine.Timer.SecondsF,_robot.Engine.EmitStackLog);
    }
    private static LedPattern[] HardLights()=>Enumerable.Range(0,5).Select(i=>new LedPattern(i is 0 or 4?LedColor.Off:LedColor.White,LedColor.Off,
        i is 0 or 4?0u:360u,i is 0 or 4?0u:1110u,0,0,i==2?120:i==3?240:0)).ToArray();
    private void Lock(string name,ReactionLockTable table)
    {
        if(!ScopedOwner){Manager.DisableReactionsWithLock(name+"_activityLock",table,true);_locks.Add(name);return;}
        var lease=new ScopedAcquisition();_scopedLocks[name]=lease;
        if(!Manager.DisableScopedReactions(name+"_activityLock",table,lease))
        { lease.Cancel();Manager.RemoveScopedReactions(name+"_activityLock",lease);return; }
    }
    private void PushIdle(AnimationTrigger trigger)
    {
        if(_idle){_robot.Engine.EmitStackLog("VERIFY: IActivity.SmartPushIdleAnimation: idle already set for "+_activity.Id);return;}
        if(ScopedOwner)
        {
            var lease=new ScopedAcquisition();_idleLease=lease;_idle=true;
            Facade.Animations.Scheduler.PushScopedIdle(trigger,"Activity_"+_activity.Id,lease);
        }
        else { Facade.Animations.Scheduler.PushIdleAnimation(trigger,"Activity_"+_activity.Id);_idle=true; }
    }
    internal void ResetLightsAndAnimations()
    {
        if(!_hardOutputs)return;
        long generation=_generation;
        if(ScopedOwner)
        {
            var result=Facade.Animations.DrivingAnimations.RemoveScoped(_activity.Id,()=>Current(generation));
            if(!result.Completed)return;
            if(ReferenceEquals(result.Owner,_drivingLease))_drivingLease=null;
        }
        else Facade.Animations.DrivingAnimations.Remove(_activity.Id);
        if(!Current(generation))return;
        Facade.Lights.Body.StopLooping(_lights);
        if(!Current(generation))return;
        _lightsOwned=false;_hardOutputs=false;
    }
    internal void Deselected()
    {
        if(ScopedOwner){var detached=DetachGather()!;try{DeselectedGather(detached);}finally{detached.ReleaseAll();}return;}
        long generation=++_generation;
        foreach(var (name,lease) in _scopedLocks.ToArray())
        { _scopedLocks.Remove(name);lease.Cancel();Manager.RemoveScopedReactions(name+"_activityLock",lease);if(!Current(generation))return; }
        foreach(string name in _locks.ToArray()){Manager.RemoveDisableReactionsLock(name+"_activityLock");if(!Current(generation))return;_locks.Remove(name);}
        if(_idle)
        {
            _idle=false;var lease=_idleLease;_idleLease=null;
            if(_delegate is not null && lease is not null){lease.Cancel();Facade.Animations.Scheduler.RemoveScopedIdle("Activity_"+_activity.Id,lease,_robot.Engine.Timer.TimeStampMs);}
            else Facade.Animations.Scheduler.RemoveIdleAnimation("Activity_"+_activity.Id,_robot.Engine.Timer.TimeStampMs);
        }
        if(!Current(generation))return;
        // S27 005B3548: common pending reward precedes specialized virtual cleanup.
        // M15-071 still owns the missing native amount/raw award in this existing recipient.
        if(_robot.Engine.Needs.SparksRewardPending)
        {
            _robot.Engine.Needs.SparksRewardCommunicatedToUser();
            if(!Current(generation))return;
            SteppedBehavior.ReportMissing("M15-071 SparksRewardCommunicatedToUser 0x00696E64/0x00696E90: pending flag and existing queued notification delivered; native reward amount +0x3DC and raw FreeplaySparksAwarded broadcast are unbuilt");
        }
        if(!Current(generation))return;
        ResetLightsAndAnimations();
        if(!Current(generation))return;
        _delegate?.OnDeselected(_robot.Engine.Timer.SecondsF,_robot.Engine.EmitStackLog);
        if(!Current(generation))return;
        // H12: StopAllAnims names only EngineLayer; user/state entries survive.
        Facade.Lights.Cubes.StopAllAnimsOnLayer(CubeLightComponent.EngineLayer,null,_delegate is null?null:()=>Current(generation));
    }
    internal sealed class GatherRetirement
    {
        private readonly NativeSparkedActivity owner;
        private readonly KeyValuePair<string,ScopedAcquisition>[] locks;
        private ScopedAcquisition? idle,driving;
        private BodyLightComponent.Locator? lights;
        private readonly bool hardOutputs;
        internal readonly long Generation;
        internal GatherRetirement(NativeSparkedActivity source,long generation,KeyValuePair<string,ScopedAcquisition>[] oldLocks,
            ScopedAcquisition? oldIdle,ScopedAcquisition? oldDriving,BodyLightComponent.Locator? oldLights,bool oldHardOutputs)
        {owner=source;Generation=generation;locks=oldLocks;idle=oldIdle;driving=oldDriving;lights=oldLights;hardOutputs=oldHardOutputs;}
        internal void CommonRemoved(object? removedOwner)
        { if(ReferenceEquals(removedOwner,driving))driving=null; }
        internal void ResetHard()
        {
            if(!hardOutputs){if(lights is null)driving=null;return;}
            var result=owner.Facade.Animations.DrivingAnimations.RemoveScoped(owner._activity.Id,()=>owner.Current(Generation));
            if(!result.Completed)return;
            if(!owner.Current(Generation))return;
            var locator=lights;lights=null;if(locator is not null)owner.Facade.Lights.Body.StopLooping(locator);
            if(owner.Current(Generation)){driving=null;owner._hardOutputs=false;}
        }
        internal void ReleaseLocks()
        {
            Exception? first=null;
            foreach(var (name,lease) in locks)
            {
                lease.Cancel();
                try { owner.Manager.RemoveScopedReactions(name+"_activityLock",lease); }
                catch(Exception error) { first??=error; }
            }
            if(first is not null)throw first;
        }
        internal void ReleaseIdle()
        {var lease=idle;idle=null;if(lease is null)return;lease.Cancel();owner.Facade.Animations.Scheduler.RemoveScopedIdle("Activity_"+owner._activity.Id,lease,owner._robot.Engine.Timer.TimeStampMs);}
        internal void ReleaseHard()
        {
            var lease=driving;driving=null;if(lease is not null){lease.Cancel();owner.Facade.Animations.DrivingAnimations.RemoveOwned(lease);}
            var locator=lights;lights=null;if(locator is not null)owner.Facade.Lights.Body.StopLooping(locator);
        }
        internal void ReleaseAll()
        {
            try { ReleaseLocks(); }
            finally { try { ReleaseIdle(); } finally { ReleaseHard(); } }
        }
    }
    internal GatherRetirement? DetachGather()
    {
        if(!ScopedOwner)return null;
        long generation=++_generation;
        var locks=_scopedLocks.ToArray();_scopedLocks.Clear();
        var idle=_idleLease;_idleLease=null;_idle=false;
        var driving=_drivingLease;_drivingLease=null;
        var lights=_lightsOwned?_lights:null;_lightsOwned=false;
        bool hardOutputs=_hardOutputs;
        return new(this,generation,locks,idle,driving,lights,hardOutputs);
    }
    internal void DeselectedGather(GatherRetirement detached)
    {
        long generation=detached.Generation;
        detached.ReleaseLocks();detached.ReleaseIdle();
        if(!Current(generation))return;
        if(_robot.Engine.Needs.SparksRewardPending)
        {
            _robot.Engine.Needs.SparksRewardCommunicatedToUser();
            if(!Current(generation))return;
            SteppedBehavior.ReportMissing("M15-071 SparksRewardCommunicatedToUser 0x00696E64/0x00696E90: pending flag and existing queued notification delivered; native reward amount +0x3DC and raw FreeplaySparksAwarded broadcast are unbuilt");
        }
        if(!Current(generation))return;
        detached.ResetHard();if(!Current(generation))return;
        _delegate?.OnDeselected(_robot.Engine.Timer.SecondsF,_robot.Engine.EmitStackLog);
        if(!Current(generation))return;
        Facade.Lights.Cubes.StopAllAnimsOnLayer(CubeLightComponent.EngineLayer,null,()=>Current(generation));
    }
    internal static bool Deadline(float start,float window,float now)=>start+window+BitConverter.UInt32BitsToSingle(0xB727C5AC)<=now;
    internal void Update()
    {
        long generation=_generation;
        bool reaction=Manager.CurrentReactionTrigger is not null;
        if(State is 0 or 1 && reaction)State=2;
        if(State is 2 or 3)CheckEnd();
        if(!Current(generation))return;
        if(State==3&&reaction){Complete();if(!Current(generation))return;ResetLightsAndAnimations();if(!Current(generation))return;State=5;}
        if(Current(generation))_delegate?.GatherOwner?.Update();
    }
    private void CheckEnd()
    {
        long generation=_generation;
        var current=Manager.Current;float now=_robot.Engine.Timer.SecondsF;
        bool repetitions=Deadline(Started,Minimum,now)&&Repetitions!=0&&unchecked((int)Count)>=Repetitions;
        bool timeout=false;
        if(WaitForAction&&Deadline(Started,Maximum,now)&&current is not null)
        {
            if(current is SteppedBehavior {NativeBaseInputs:{} native})timeout=native.RequiredUnlock!=unchecked((uint)Manager.ActiveSparkId);
            else SteppedBehavior.ReportMissing("M15-059 CheckIfSparkShouldEnd current requiredUnlock input unavailable; no timeout predicate invented");
        }
        if(State==2&&(repetitions||timeout||Manager.SparkCancelLatch!=0))
        {
            ResetLightsAndAnimations();if(!Current(generation))return;
            Manager.RequestCurrentBehaviorEndOnNextActionComplete();if(!Current(generation))return;
            State=3;Lock("finalAnimLockReactions",FinalLock);
        }
        else if(Manager.RequestedSparkId!=0x55 && (Manager.ActiveSparkId!=Manager.RequestedSparkId ||
            Manager.ActiveSparkId!=0x55&&Manager.ActiveSparkSoft!=0&&Manager.RequestedSparkSoft==0))
        {
            Manager.RequestCurrentBehaviorEndImmediately("Sparks transition soft spark to soft spark");if(!Current(generation))return;State=4;_interrupted=true;
        }
    }
    private static bool Running(IBehavior? behavior)=>behavior is SteppedBehavior {EngineRunning:true};
    private IBehavior? Choose(IBehavior? current,double now,BehaviorContext context)
    {
        var chooser=_delegate?.Chooser ?? _activity.Chooser;
        long generation=_generation;
        var decision=chooser?.GetDesiredActiveBehavior(current,Manager.RunningDurationSec(now),context,now);
        if(!Current(generation))return null;
        if(decision?.InputUnavailable==true) { InputUnavailable=true; return null; }
        var chosen=decision?.Behavior;
        if(!WaitForAction&&Deadline(Started,Maximum,_robot.Engine.Timer.SecondsF)&&!ReferenceEquals(current,chosen))
        {State=3;SteppedBehavior.ReportMissing("M15-059 waitfalse hard-max retained native Wait child is not built; no replacement behavior fabricated");return null;}
        return chosen;
    }
    internal IBehavior? Desired(IBehavior? current,double now,BehaviorContext context)
    {
        switch(State)
        {
            case 0:
                _animation.SetAnimationTrigger(Manager.ActiveSparkId!=0x55&&Manager.ActiveSparkSoft!=0?_softTrigger:(AnimationTrigger)0x211,1);State=1;return _animation;
            case 1:
                if(Running(current))return _animation;
                State=2;return Choose(current,now,context);
            case 2:return Choose(current,now,context);
            case 3:
                if(current is not null&&current.Class!="Wait"&&Running(current))return Choose(current,now,context);
                if(Manager.ActiveSparkId!=0x55&&Manager.ActiveSparkSoft!=0||Manager.SparkCancelLatch!=0)_animation.SetAnimationTrigger((AnimationTrigger)0x23F,1);
                else
                {
                    var triggers=new List<AnimationTrigger>();
                    if(unchecked((int)Count)<Repetitions){if(Failure!=(AnimationTrigger)0x23F&&Manager.ActiveSparkId!=0x41)triggers.Add(Failure);}
                    else{if(Success!=(AnimationTrigger)0x23F)triggers.Add(Success);_robot.MoodOwner?.Trigger("SuccessfulSpark",_robot.Engine.Timer.SecondsF);}
                    triggers.Add((AnimationTrigger)0x212);_animation.SetAnimationTriggers(triggers,1);
                }
                State=4;return _animation;
            case 4:
                if(Running(current))return _animation;
                Complete();return null;
            default:return null;
        }
    }
    private void Complete()
    {
        bool success=Repetitions==0||unchecked((int)Count)>=Repetitions;
        CompletionDiagnostics(success);
        if(Manager.SparkCancelLatch!=0||_interrupted)return;
        if(Manager.ActiveSparkId==Manager.RequestedSparkId)Manager.SetNativeRequestedSpark(0x55,0);
        if(Manager.ActiveSparkId==0x55||Manager.ActiveSparkSoft==0)_robot.Engine.ReportHardSparkEnded(success);
    }
    private void CompletionDiagnostics(bool success)
    {
        bool canceled=Manager.SparkCancelLatch!=0||_interrupted;
        bool soft=Manager.ActiveSparkId!=0x55&&Manager.ActiveSparkSoft!=0;
        string name=Manager.SparkNameInput?.Invoke(Manager.ActiveSparkId)??$"unlock {Manager.ActiveSparkId}";
        if(!canceled&&!success&&_observedIds.Count!=0&&!soft)
            _robot.Engine.EmitSourceLog(EngineLogLevel.Event,null,"meta.upgrade_replay_fail_cubes_observed",name,
                new Dictionary<string,string>{{"$data",_observedIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}});
        string key=canceled?"meta.upgrade_replay_cancel":success?"meta.upgrade_replay_success":_observedIds.Count==0?"meta.upgrade_replay_timeout":"meta.upgrade_replay_fail";
        _robot.Engine.EmitSourceLog(EngineLogLevel.Event,null,key,name,new Dictionary<string,string>{{"$data",soft?"soft":"hard"}});
    }
    public void Dispose()
    {
        if(_retired)return;
        var detached=DetachGather();_retired=true;++_generation;
        try { detached?.ReleaseAll(); }
        finally { _delegate?.GatherOwner?.Dispose();_objective?.Dispose();_observed?.Dispose(); }
    }
}
