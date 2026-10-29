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
    private readonly NativePlayArbitraryAnimBehavior _animation;
    private readonly RetainedSubscription? _objective,_observed;
    private readonly SortedSet<uint> _observedIds=new();
    private readonly SortedSet<string> _locks=new(StringComparer.Ordinal);
    private readonly BodyLightComponent.Locator _lights=new();
    private bool _idle,_hardOutputs,_interrupted;
    internal uint Count {get;private set;}
    internal int State {get;private set;}
    internal float Started {get;private set;}
    internal float Minimum {get;set;}
    internal float Maximum {get;set;}
    internal byte Repetitions {get;set;}
    internal bool WaitForAction {get;set;}=true;
    private readonly bool _hasDelegate;
    internal bool CanExecute=>WaitForAction&&!_hasDelegate;
    internal IReadOnlyCollection<uint> Observed=>_observedIds;
    internal AnimationTrigger Success {get;set;}=(AnimationTrigger)0x216;
    internal AnimationTrigger Failure {get;set;}=(AnimationTrigger)0x210;
    private readonly AnimationTrigger _softTrigger;
    private readonly uint _objectiveId;
    private BehaviorManager Manager=>_robot.BehaviorManagerOwner!;
    private CozmoRobot Facade=>Manager.ContextRobot;
    private static readonly ReactionLockTable SelectedLock=new("ActivitySparked","0xC624BC",new[]{1,2,3,4,5,10});
    private static readonly ReactionLockTable FinalLock=new("finalAnimLockReactions","0xC624E6",new[]{1,2,4,8});
    internal NativeSparkedActivity(Activity activity,EngineRobot robot,JsonElement config,IReadOnlyDictionary<string,IBehavior> bound)
    {
        _activity=activity;_robot=robot;
        _animation=(NativePlayArbitraryAnimBehavior)bound["PlayArbitraryAnim"];
        Minimum=config.GetProperty("minTimeSecs").GetSingle();Maximum=config.GetProperty("maxTimeSecs").GetSingle();
        Repetitions=unchecked((byte)config.GetProperty("numberOfRepetitions").GetInt32());
        _objectiveId=(activity.Id,config.GetProperty("behaviorObjective").GetString()) switch
        {
            ("SparksKnockOverCubes","KnockedOverBlocks")=>13u,
            ("SparksStackBlock","StackedBlock")=>40u, // S1 actual native objective40, not a generalized parser.
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
        Started=_robot.Engine.Timer.SecondsF;Count=0;State=0;_interrupted=false;_hardOutputs=false;_observedIds.Clear();
        if(Manager.RequestedSparkId==0x55||Manager.RequestedSparkSoft==0)
        {
            Facade.Animations.DrivingAnimations.Push(new((AnimationTrigger)0x20E,(AnimationTrigger)0x20D,(AnimationTrigger)0x20F),_activity.Id);
            PushIdle((AnimationTrigger)0x213);
            Facade.Lights.Body.StartLooping(HardLights(),0,_lights);_hardOutputs=true;
            _robot.Engine.ReportHardSparkStarted(Manager.RequestedSparkId);
        }
        Lock(_activity.Id,SelectedLock);
    }
    private static LedPattern[] HardLights()=>Enumerable.Range(0,5).Select(i=>new LedPattern(i is 0 or 4?LedColor.Off:LedColor.White,LedColor.Off,
        i is 0 or 4?0u:360u,i is 0 or 4?0u:1110u,0,0,i==2?120:i==3?240:0)).ToArray();
    private void Lock(string name,ReactionLockTable table)
    {Manager.DisableReactionsWithLock(name+"_activityLock",table,true);_locks.Add(name);}
    private void PushIdle(AnimationTrigger trigger)
    {
        if(_idle){_robot.Engine.EmitStackLog("VERIFY: IActivity.SmartPushIdleAnimation: idle already set for "+_activity.Id);return;}
        Facade.Animations.Scheduler.PushIdleAnimation(trigger,"Activity_"+_activity.Id);_idle=true;
    }
    internal void ResetLightsAndAnimations()
    {
        if(!_hardOutputs)return;
        Facade.Animations.DrivingAnimations.Remove(_activity.Id);
        Facade.Lights.Body.StopLooping(_lights);_hardOutputs=false;
    }
    internal void Deselected()
    {
        foreach(string name in _locks.ToArray()){Manager.RemoveDisableReactionsLock(name+"_activityLock");_locks.Remove(name);}
        if(_idle){Facade.Animations.Scheduler.RemoveIdleAnimation("Activity_"+_activity.Id,_robot.Engine.Timer.TimeStampMs);_idle=false;}
        // S27 005B3548: common pending reward precedes specialized virtual cleanup.
        // M15-071 still owns the missing native amount/raw award in this existing recipient.
        if(_robot.Engine.Needs.SparksRewardPending)
        {
            _robot.Engine.Needs.SparksRewardCommunicatedToUser();
            SteppedBehavior.ReportMissing("M15-071 SparksRewardCommunicatedToUser 0x00696E64/0x00696E90: pending flag and existing queued notification delivered; native reward amount +0x3DC and raw FreeplaySparksAwarded broadcast are unbuilt");
        }
        ResetLightsAndAnimations();
        // H12: StopAllAnims names only EngineLayer; user/state entries survive.
        Facade.Lights.Cubes.StopAllAnimsOnLayer(CubeLightComponent.EngineLayer,null);
    }
    internal static bool Deadline(float start,float window,float now)=>start+window+BitConverter.UInt32BitsToSingle(0xB727C5AC)<=now;
    internal void Update()
    {
        bool reaction=Manager.CurrentReactionTrigger is not null;
        if(State is 0 or 1 && reaction)State=2;
        if(State is 2 or 3)CheckEnd();
        if(State==3&&reaction){Complete();ResetLightsAndAnimations();State=5;}
    }
    private void CheckEnd()
    {
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
            ResetLightsAndAnimations();Manager.RequestCurrentBehaviorEndOnNextActionComplete();State=3;Lock("finalAnimLockReactions",FinalLock);
        }
        else if(Manager.RequestedSparkId!=0x55 && (Manager.ActiveSparkId!=Manager.RequestedSparkId ||
            Manager.ActiveSparkId!=0x55&&Manager.ActiveSparkSoft!=0&&Manager.RequestedSparkSoft==0))
        {
            Manager.RequestCurrentBehaviorEndImmediately("Sparks transition soft spark to soft spark");State=4;_interrupted=true;
        }
    }
    private static bool Running(IBehavior? behavior)=>behavior is SteppedBehavior {EngineRunning:true};
    private IBehavior? Choose(IBehavior? current,double now,BehaviorContext context)
    {
        var chosen=_activity.Chooser?.GetDesiredActiveBehavior(current,Manager.RunningDurationSec(now),context,now).Behavior;
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
    public void Dispose(){_objective?.Dispose();_observed?.Dispose();}
}
