using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M13-015 - retry/result/reset only; proposed Pop lifecycle/cache/leaf complement is separate.
// POP-A-WHEELIE-1 P1-P13:005C7430..005C7F6E; configured instance, actual native action owner.
internal sealed class NativePopAWheelieBehavior:SteppedBehavior
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly bool _always;
    private readonly string? _need;
    private int _epoch;
    internal uint Target {get;private set;}=uint.MaxValue;
    internal uint Previous {get;private set;}=uint.MaxValue;
    internal int Retries {get;private set;}
    internal bool CliffChanged {get;private set;}
    internal bool HardSpark {get;private set;}
    internal NativeDriveToPopAWheelieRunner? Interaction {get;private set;}
    internal CompoundActionSequential? GetIn {get;private set;}
    private static readonly ReactionLockTable PreDockLock=new("PopAWheelie","0x00C6883D",new[]{0,1,3,11,12,14,15,20});
    internal NativePopAWheelieBehavior(BehaviorFactoryContext source,JsonElement json):base(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString(),"PopAWheelie")
    {
        _m=source.Manipulation!;_robot=source.NativeRobot!;
        _always=json.TryGetProperty("alwaysStreamline",out var always)&&always.GetBoolean();
        _need=json.TryGetProperty("needsActionID",out var need)?need.GetString():null;
    }
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    public override bool IsRunnable(BehaviorContext context)=>IsRunnableBase(context)&&IsRunnableInternal(context);
    internal void RefreshTarget()=>Target=(_robot.NativeBehaviorRuntime?.PickupTargets??throw new NativeConfigurationUnavailable("actual Pop intention owner absent")).GetBest(8);
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        try{RefreshTarget();return Target!=uint.MaxValue;}
        catch(NativeConfigurationUnavailable error){ReportMissing("Pop target refresh unavailable: "+error.Message);return false;}
    }
    protected override void OnStart()
    {
        Interlocked.Increment(ref _epoch);
        var manager=_robot.BehaviorManagerOwner??throw new NativeActionUnavailable("Pop actual spark manager absent");
        HardSpark=manager.ActiveSparkId!=0x55&&(manager.ActiveSparkSoft^1)!=0;
        if(_always||HardSpark||Target==Previous)Perform(false);else React();
    }
    private TriggerLiftSafeAnimationAction LiftSafe(int trigger)=>new(_m.Robot,new((AnimationTrigger)trigger,1,true,0,60f,false));
    private void React()
    {
        SetStateName("ReactingToBlock");
        GetIn=new PopSequence(this);GetIn.SeedNativeEmptyCompletion();
        GetIn.AddAction(new NativeTurnTowardsObjectRunner(_m,Target,NativeTurnTowardsObjectRunner.AnyCode,Radians.Pi,false,false),false,false);
        GetIn.AddAction(LiftSafe(0x18A),false,false);
        StartActing(GetIn,report=>{if(report.Result==0)Previous=Target;Perform(false);});
    }
    private void Perform(bool retry)
    {
        SetStateName("PerformingAction");
        if(Target==uint.MaxValue){_robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorPopAWheelie.NoBlockID","No block ID");return;}
        Retries=retry?unchecked(Retries+1):0;
        Interaction=new(_m,Target,_always||HardSpark?0f:BitConverter.UInt32BitsToSingle(0x3FC90FDB));
        Interaction.SetNameTriggers(0x18B,0x18C);Interaction.BeforeDock=BeforeDock;
        var interaction=Interaction;interaction.Unavailable=error=>Unavailable(error,interaction.Tag);
        StartActing(Interaction,report=>Respond(report.Result));
        IncreaseScoreWhileActing(BitConverter.UInt32BitsToSingle(0x3F4CCCCD));
    }
    private void BeforeDock()
    {
        Scope.SmartDisableReactionsWithLock(Id,PreDockLock);
        CliffChanged=true;_m.Robot.SendMessage(new EnableStopOnCliff{Enable=false},flush:true);
    }
    //005C7CBC: scalar retained in full, category only controls the source branches.
    internal void Respond(uint result)
    {
        if(result!=0)Scope.SmartRemoveDisableReactionsLock(Id);
        uint category=result>>24;
        if(category==4&&Retries<=0){Retry(result);return;}
        if(category is 3 or 4)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,null,"BehaviorPopAWheelie.FailedAbort",$"Failed to pop with {result:X8}, searching for block");
            using var mutation=_m.World.EnterPoseMutation();
            if(_m.World.GetLocatedObjectByIdHelper(Target) is {} target)_m.Whiteboard.SetFailedToUse(target,(ObjectActionFailure)3);
            return;
        }
        if(category==0)
        {
            Previous=uint.MaxValue;
            StartActing(new TriggerAnimationAction(_m.Robot,new((AnimationTrigger)0x21C,1,true,0,60f,false)),null);
            _robot.Engine.ReportBehaviorObjective(22,"PoppedWheelie",Id);
            if(_need is {} need)_robot.Engine.Needs.RegisterNeedsActionCompleted(need);
            return;
        }
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info,null,"BehaviorPopAWheelie.FailedPopAction",$"Action failed with {result:X8}");
    }
    private void Retry(uint result)
    {
        try{RefreshTarget();}
        catch(NativeConfigurationUnavailable error){Unavailable(new NativeActionUnavailable("Pop retry target refresh unavailable: "+error.Message),CurrentRunnerTag);return;}
        StartActing(LiftSafe(result==0x04000001?0x18D:0x18E),_=>Perform(true));
    }
    internal void ResetBehavior()
    {
        Target=uint.MaxValue;
        if(!CliffChanged)return;
        CliffChanged=false; //005C76D0: clear BEFORE reliable send; previous/retries retained.
        _m.Robot.SendMessage(new EnableStopOnCliff{Enable=true},flush:true);
    }
    protected override void OnStop(BehaviorStopReason reason){Interlocked.Increment(ref _epoch);ResetBehavior();}
    private void Unavailable(NativeActionUnavailable error,uint tag)
    {
        ReportMissing("POP-A-WHEELIE-1 native child unavailable: "+error.Message);
        int epoch=Volatile.Read(ref _epoch);
        _robot.ActionList.DeferHostCleanup(()=>
        {
            if(epoch!=Volatile.Read(ref _epoch)||CurrentRunnerTag!=tag)return;
            if(ReferenceEquals(_robot.BehaviorManagerOwner?.Current,this))_robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,_m.Robot.Engine.Timer.SecondsF);
            else Stop(BehaviorStopReason.Interrupted);
        });
    }
    private sealed class PopSequence(NativePopAWheelieBehavior owner):CompoundActionSequential(()=>owner._robot.Engine.Timer.SecondsF)
    {
        public override uint Update()
        {
            int depth=Watcher?.HostUpdateDepth??0;
            try{return base.Update();}
            catch(NativeActionUnavailable error){Watcher?.UnwindUnavailableHostUpdate(depth);owner.Unavailable(error,Tag);return EngineActionResult.Running;}
        }
    }
}
