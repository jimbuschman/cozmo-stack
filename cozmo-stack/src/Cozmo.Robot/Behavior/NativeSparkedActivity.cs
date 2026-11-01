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
    private NativePlayArbitraryAnimBehavior _animation=null!;
    private readonly IReadOnlyDictionary<string,IBehavior> _bound;
    private readonly RetainedSubscription? _objective,_observed;
    private readonly object _dataGate=new();
    private readonly SortedSet<uint> _observedIds=new();
    private uint _count;
    private readonly SortedSet<string> _locks=new(StringComparer.Ordinal);
    private readonly Dictionary<string,ScopedAcquisition> _scopedLocks=new(StringComparer.Ordinal);
    private BodyLightComponent.Locator _lights=new();
    private ScopedAcquisition? _drivingLease,_idleLease;
    private bool _idle,_hardOutputs,_lightsOwned,_interrupted,_peekUnavailable,_updateUnavailable,_delegateUnknown;
    private long _generation;
    private volatile bool _retired;
    internal bool InputUnavailable { get; private set; }
    private bool Current(long generation)=>!_retired && generation==_generation;
    internal uint Count {get{lock(_dataGate)return _count;}private set{lock(_dataGate)_count=value;}}
    internal int State {get;private set;}
    internal float Started {get;private set;}
    internal float Minimum {get;set;}=-1f;
    internal float Maximum {get;set;}=-1f;
    internal int Repetitions {get;set;}=-1;
    internal bool WaitForAction {get;set;}=true;
    private bool _hasDelegate;
    private bool ScopedOwner=>true;
    private Activity? _delegate;
    private readonly Func<JsonElement,Activity?>? _createDelegate;
    internal Activity? Delegate => _delegate;
    private bool DelegateUnavailable=>_delegateUnknown||(_hasDelegate && _delegate?.GatherOwner is null);
    internal bool CanExecute=>!_retired && !DelegateUnavailable;
    internal bool IsOwnedBy(EngineRobot owner)=>!_retired && ReferenceEquals(_robot,owner);
    internal IReadOnlyCollection<uint> Observed {get{lock(_dataGate)return _observedIds.ToArray();}}
    internal AnimationTrigger Success {get;set;}=(AnimationTrigger)0x216;
    internal AnimationTrigger Failure {get;set;}=(AnimationTrigger)0x210;
    private AnimationTrigger _softTrigger=(AnimationTrigger)0x23F;
    internal AnimationTrigger SoftTrigger=>_softTrigger;
    private uint _objectiveId;
    private NativeWaitBehavior? _wait;
    private IBehavior? _acknowledge;
    private NativeScoredFields? _peek;
    private readonly JsonElement _peekConfig;
    private BehaviorManager Manager=>_manager;
    private CozmoRobot Facade=>_facade;
    private static readonly ReactionLockTable SelectedLock=new("ActivitySparked","0xC624BC",new[]{1,2,3,4,5,10});
    private static readonly ReactionLockTable FinalLock=new("finalAnimLockReactions","0xC624E6",new[]{1,2,4,8});
    internal NativeSparkedActivity(Activity activity,EngineRobot robot,JsonElement config,IReadOnlyDictionary<string,IBehavior> bound,Activity? nested=null,Func<JsonElement,Activity?>? createDelegate=null)
    {
        _activity=activity;_robot=robot;_bound=bound;_delegate=nested;_createDelegate=createDelegate;
        _manager=robot.BehaviorManagerOwner??throw new InvalidOperationException("MISSING: Sparked actual BehaviorManager owner unavailable");
        _facade=_manager.ContextRobot;
        _wait=bound.TryGetValue("Wait",out var waitOwner)?waitOwner as NativeWaitBehavior:null;
        _acknowledge=bound.TryGetValue("AcknowledgeObject",out var acknowledge)?acknowledge:null;
        if(robot.Engine.HostBehaviorMap?.TryGetValue(BehaviorID.SparksPeekABoo,out var peekConfig)==true &&
           peekConfig.TryGetProperty("behaviorClass",out var peekClass) && peekClass.ValueKind==JsonValueKind.String && peekClass.GetString()=="PeekABoo" &&
           robot.NativeConfiguredScoreFields?.TryGetValue("SparksPeekABoo",out var peek)==true)
        { _peek=peek;_peekConfig=peekConfig; }
        ReloadScalars(config);
        _hasDelegate=config.TryGetProperty("subActivityDelegate",out var delegateConfig)&&delegateConfig.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
        if(robot.HasExternalInterface)
        {
            _objective=robot.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint value)lock(_dataGate){if(!_retired&&value==_objectiveId)_count=unchecked(_count+1);}});
            _observed=robot.Engine.AnimationMessages.SubscribeMessage(0x44,true,p=>{if(p is NativeObservedObject value&&unchecked(value.Family-1)<=1)lock(_dataGate){if(!_retired)_observedIds.Add(value.ObjectId);}});
        }
    }
    // Reload 005B0EAC..1132: partial mutations precede any host config failure.
    private void ReloadScalars(JsonElement config)
    {
        _softTrigger=config.TryGetProperty("softSparkTrigger",out var soft)&&soft.ValueKind!=JsonValueKind.Null?ParseTrigger(soft.GetString()??""):(AnimationTrigger)0x23F;
        Success=Trigger(config,"sparksSuccessTrigger",(AnimationTrigger)0x216);
        Failure=Trigger(config,"sparksFailTrigger",(AnimationTrigger)0x210);
        if(!_bound.TryGetValue("PlayArbitraryAnim",out var arbitrary)||arbitrary is not NativePlayArbitraryAnimBehavior actual)
            throw new InvalidOperationException("MISSING: Sparked retained configured PlayArbitraryAnim 3D unavailable");
        _animation=actual;
        Minimum=config.GetProperty("minTimeSecs").GetSingle();Maximum=config.GetProperty("maxTimeSecs").GetSingle();
        int repetitions=config.GetProperty("numberOfRepetitions").GetInt32();
        if(repetitions is <0 or >255)throw new InvalidOperationException("MISSING: Sparked ParseUint8 invalid input");
        Repetitions=repetitions;
        if(config.TryGetProperty("maxTimeoutForActionComplete",out var wait))WaitForAction=wait.GetBoolean();
        uint objectiveId=FistBumpReactionStrategy.ObjectiveFromName(config.TryGetProperty("behaviorObjective",out var objective)?objective.GetString()??"Unknown":"Unknown");
        lock(_dataGate)_objectiveId=objectiveId;
    }
    internal bool Reload(JsonElement config)
    {
        using var owner=_robot.Engine.EnterSynchronousOwner();
        if(_retired){InputUnavailable=true;return false;}
        long generation=_generation;
        ReloadScalars(config);
        if(!config.TryGetProperty("subActivityDelegate",out var value)||value.ValueKind==JsonValueKind.Null)return true;
        var next=_createDelegate?.Invoke(value);
        if(!Current(generation))
        {next?.GatherOwner?.Dispose();next?.ObjectiveOwner?.Dispose();next?.Strategy.Dispose();return false;}
        if(next is null){_delegateUnknown=true;InputUnavailable=true;SteppedBehavior.ReportMissing("M15-059 configured delegated activity factory unavailable");return false;}
        var previous=_delegate;_delegate=next;_hasDelegate=true;_delegateUnknown=false;
        previous?.GatherOwner?.Dispose();previous?.ObjectiveOwner?.Dispose();previous?.Strategy.Dispose();
        return true;
    }
    private static AnimationTrigger Trigger(JsonElement config,string key,AnimationTrigger fallback)
    {
        if(!config.TryGetProperty(key,out var value))return fallback;
        string name=value.GetString()??"";
        return name.Length==0?(AnimationTrigger)0x23F:ParseTrigger(name);
    }
    private static AnimationTrigger ParseTrigger(string name)
    {
        if(Array.IndexOf(Enum.GetNames<AnimationTrigger>(),name)>=0 && Enum.TryParse<AnimationTrigger>(name,false,out var value))return value;
        BehaviorClassNames.WriteCerrError(name,"AnimationTrigger");return (AnimationTrigger)0;
    }
    internal void Selected()
    {
        if(_retired){InputUnavailable=true;return;}
        long generation=++_generation;InputUnavailable=false;_peekUnavailable=false;_updateUnavailable=false;
        if(DelegateUnavailable)
        {InputUnavailable=true;SteppedBehavior.ReportMissing("M15-059 configured delegated activity identity unavailable");return;}
        Started=_robot.Engine.Timer.SecondsF;lock(_dataGate){_count=0;_observedIds.Clear();}State=0;_interrupted=false;_hardOutputs=false;
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
            if(_robot.HasExternalInterface)_robot.Engine.ReportHardSparkStarted(Manager.RequestedSparkId);
            else SteppedBehavior.ReportMissing("M1-034/M15-059 HardSparkStarted 005B172E: missing external interface; skip null dereference");
            if(!Current(generation))return;
        }
        Lock(_activity.Id,SelectedLock);
        if(!Current(generation))return;
        _delegate?.OnSelected(_robot.Engine.Timer.SecondsF,_robot.Engine.EmitStackLog);
        if(Current(generation)&&Manager.RequestedSparkId==0x25)
        {
            if(_peek is not null && _robot.Engine.HostBehaviorMap?.TryGetValue(BehaviorID.SparksPeekABoo,out var peekConfig)==true && peekConfig.Equals(_peekConfig) &&
                _robot.NativeConfiguredScoreFields?.TryGetValue("SparksPeekABoo",out var peek)==true && ReferenceEquals(peek,_peek))
                _peek.SparkPeekDeadline=(_robot.Engine.Timer.SecondsF+Maximum)-2f;
            else {InputUnavailable=true;_peekUnavailable=true;SteppedBehavior.ReportMissing("M15-059 retained configured SparksPeekABoo +168 owner input unavailable");}
        }
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
        if(_retired||!_hardOutputs)return;
        long generation=_generation;
        if(ScopedOwner)
        {
            var result=Facade.Animations.DrivingAnimations.RemoveScoped(_activity.Id,()=>Current(generation));
            if(!result.Completed)return;
            // Native removes the first matching name; never later remove a different entry by identity.
            _drivingLease=null;
        }
        else Facade.Animations.DrivingAnimations.Remove(_activity.Id);
        if(!Current(generation))return;
        Facade.Lights.Body.StopLooping(_lights);
        if(!Current(generation))return;
        _lightsOwned=false;_hardOutputs=false;
    }
    internal void Deselected()
    {
        if(_retired)return;
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
        ResetLightsAndAnimations();
        if(!Current(generation))return;
        if(DelegateUnavailable){InputUnavailable=true;SteppedBehavior.ReportMissing("M15-059 delegated deselection owner unavailable");return;}
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
        if(_retired||!ScopedOwner)return null;
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
        detached.ResetHard();if(!Current(generation))return;
        if(DelegateUnavailable){InputUnavailable=true;SteppedBehavior.ReportMissing("M15-059 delegated deselection owner unavailable");return;}
        _delegate?.OnDeselected(_robot.Engine.Timer.SecondsF,_robot.Engine.EmitStackLog);
        if(!Current(generation))return;
        Facade.Lights.Cubes.StopAllAnimsOnLayer(CubeLightComponent.EngineLayer,null,()=>Current(generation));
    }
    internal static bool Deadline(float start,float window,float now)=>start+window+BitConverter.UInt32BitsToSingle(0xB727C5AC)<=now;
    internal void Update()
    {
        if(_retired)return;
        _updateUnavailable=false;
        long generation=_generation;
        bool reaction=Manager.CurrentReactionTrigger is not null;
        if(State is 0 or 1 && reaction)State=2;
        if(State is 2 or 3)CheckEnd();
        if(!Current(generation)||_updateUnavailable)return;
        if(State==3&&reaction){Complete();if(!Current(generation))return;ResetLightsAndAnimations();if(!Current(generation))return;State=5;}
        if(DelegateUnavailable){InputUnavailable=true;_updateUnavailable=true;SteppedBehavior.ReportMissing("M15-059 delegated Update owner unavailable");return;}
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
            else {InputUnavailable=true;_updateUnavailable=true;SteppedBehavior.ReportMissing("M15-059 CheckIfSparkShouldEnd current requiredUnlock input unavailable; no timeout predicate invented");return;}
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
    private bool TryCurrent(IBehavior? behavior,out bool running,out BehaviorClass cls)
    {
        running=false;cls=default;
        if(behavior is null)return true;
        if(behavior is not SteppedBehavior stepped || _robot.NativeBehaviorRuntime?.Bound.TryGetValue(behavior.Id,out var actual)!=true || !ReferenceEquals(actual,behavior) ||
            !Enum.TryParse<BehaviorID>(behavior.Id,false,out var id) || _robot.Engine.HostBehaviorMap?.TryGetValue(id,out var config)!=true)
        {InputUnavailable=true;SteppedBehavior.ReportMissing("M15-059 actual current behavior class/running owner unavailable");return false;}
        cls=BehaviorClassNames.ExtractFromConfig(config);running=stepped.EngineRunning;return true;
    }
    private IBehavior? Choose(IBehavior? current,double now,BehaviorContext context)
    {
        long generation=_generation;
        ChooserDecision? decision;
        if(_delegate is {} nested)
        {
            if(nested.GatherOwner is null)
            {InputUnavailable=true;SteppedBehavior.ReportMissing("M15-059 actual delegated activity desired wrapper unavailable");return null;}
            decision=nested.NativeGatherDesired(current,Manager.RunningDurationSec(now),context,now,()=>Current(generation));
        }
        else decision=_activity.Chooser?.GetDesiredActiveBehavior(current,Manager.RunningDurationSec(now),context,now);
        if(!Current(generation))return null;
        if(decision?.InputUnavailable==true) { InputUnavailable=true; return null; }
        var chosen=decision?.Behavior;
        if(!WaitForAction&&Deadline(Started,Maximum,_robot.Engine.Timer.SecondsF)&&!ReferenceEquals(current,chosen))
        {
            if(_wait is null){InputUnavailable=true;SteppedBehavior.ReportMissing("M15-059 retained configured Wait B2 unavailable");return null;}
            State=3;return _wait;
        }
        return chosen;
    }
    internal IBehavior? Desired(IBehavior? current,double now,BehaviorContext context)
    {
        if(_retired){InputUnavailable=true;return null;}
        InputUnavailable=_peekUnavailable||_updateUnavailable||DelegateUnavailable;
        if(InputUnavailable)return null;
        switch(State)
        {
            case 0:
                _animation.SetAnimationTrigger(Manager.ActiveSparkId!=0x55&&Manager.ActiveSparkSoft!=0?_softTrigger:(AnimationTrigger)0x211,1);State=1;return _animation;
            case 1:
                if(!TryCurrent(current,out var running1,out _))return null;
                if(running1)return _animation;
                State=2;return Choose(current,now,context);
            case 2:return Choose(current,now,context);
            case 3:
                if(!TryCurrent(current,out var running3,out var cls3))return null;
                if(current is not null&&cls3!=BehaviorClass.Wait&&running3)return Choose(current,now,context);
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
                if(!TryCurrent(current,out var running4,out _))return null;
                if(running4)return _animation;
                Complete();return null;
            default:return null;
        }
    }
    private void Complete()
    {
        long generation=_generation;
        uint count;int observed;lock(_dataGate){count=_count;observed=_observedIds.Count;}
        bool success=Repetitions==0||unchecked((int)count)>=Repetitions;
        if(!CompletionDiagnostics(success,generation,observed))return;
        if(Manager.SparkCancelLatch!=0||_interrupted)return;
        if(Manager.ActiveSparkId==Manager.RequestedSparkId){Manager.SetNativeRequestedSpark(0x55,0);if(!Current(generation))return;}
        if(Manager.ActiveSparkId==0x55||Manager.ActiveSparkSoft==0)
        {
            if(_robot.HasExternalInterface)_robot.Engine.ReportHardSparkEnded(success);
            else SteppedBehavior.ReportMissing("M1-034/M15-059 HardSparkEnded 005B20A4: missing external interface; skip null dereference");
        }
    }
    private bool CompletionDiagnostics(bool success,long generation,int observed)
    {
        bool canceled=Manager.SparkCancelLatch!=0||_interrupted;
        bool soft=Manager.ActiveSparkId!=0x55&&Manager.ActiveSparkSoft!=0;
        string name=Manager.SparkNameInput?.Invoke(Manager.ActiveSparkId)??$"unlock {Manager.ActiveSparkId}";
        if(!Current(generation))return false;
        if(!canceled&&!success&&observed!=0&&!soft)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Event,null,"meta.upgrade_replay_fail_cubes_observed",name,
                new Dictionary<string,string>{{"$data",observed.ToString(System.Globalization.CultureInfo.InvariantCulture)}});
            if(!Current(generation))return false;
        }
        string key=canceled?"meta.upgrade_replay_cancel":success?"meta.upgrade_replay_success":observed==0?"meta.upgrade_replay_timeout":"meta.upgrade_replay_fail";
        _robot.Engine.EmitSourceLog(EngineLogLevel.Event,null,key,name,new Dictionary<string,string>{{"$data",soft?"soft":"hard"}});
        return Current(generation);
    }
    public void Dispose()
    {
        using var owner=_robot.Engine.EnterSynchronousOwner();
        lock(_dataGate){if(_retired)return;_retired=true;_observedIds.Clear();}
        ++_generation;
        _animation=null!;_wait=null;_peek=null;_lights=null!;_lightsOwned=false;
        var nested=_delegate;_delegate=null;
        nested?.GatherOwner?.Dispose();nested?.ObjectiveOwner?.Dispose();nested?.Strategy.Dispose();
        _acknowledge=null;_drivingLease=null;_idleLease=null;_scopedLocks.Clear();_locks.Clear();
        _objective?.Dispose();_observed?.Dispose();
    }
}
