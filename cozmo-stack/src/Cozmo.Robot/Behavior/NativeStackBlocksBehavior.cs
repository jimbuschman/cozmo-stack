using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// Proposed owner: whole configured SparksStackBlock lifecycle (S1-S14), no assigned fidelity ID.
internal sealed class NativeStackBlocksBehavior:SteppedBehavior
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly bool _any,_always;
    private readonly string? _needs;
    private bool _hard;
    internal uint Top {get;private set;}=uint.MaxValue;
    internal uint Bottom {get;private set;}=uint.MaxValue;
    internal bool Replaced {get;private set;}
    internal int State {get;private set;}
    internal NativePickupBlockHelper? PickupHelper {get;private set;}
    internal NativePlaceRelObjectHelper? PlacementHelper {get;private set;}
    internal NativeStackBlocksBehavior(BehaviorFactoryContext source,JsonElement json):base(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString(),"StackBlocks")
    {
        _m=source.Manipulation!;_robot=source.NativeRobot!;
        _any=json.TryGetProperty("stackInAnyOrientation",out var any)&&any.GetBoolean();
        _always=json.TryGetProperty("alwaysStreamline",out var always)&&always.GetBoolean();
        _needs=json.TryGetProperty("needsActionID",out var need)?need.GetString():null;
    }
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>true;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    public override bool IsRunnable(BehaviorContext context)=>IsRunnableBase(context)&&IsRunnableInternal(context);
    internal IReadOnlySet<int> OwnSlot2C()=>new HashSet<int>{_any?0:1}; //005CA1EA; external consumer remains unbound.
    internal void OwnSlot7C()=>RefreshTargets(); //005CA224, not a new scheduling source.
    private NativePickupTargetCache Cache=>_robot.NativeBehaviorRuntime?.PickupTargets??throw new NativeConfigurationUnavailable("actual intention cache unavailable");
    private (int Top,int Bottom) Intentions()
    {
        if(_robot.Progression is not {} progression||!progression.TryIsUnlocked(9,out bool unlocked))throw new NativeConfigurationUnavailable("actual progression unlock9 state unavailable");
        return unlocked||_any?(5,3):(4,2);
    }
    internal void RefreshTargets()
    {
        using var mutation=_m.World.EnterPoseMutation();var intentions=Intentions();
        Top=Cache.GetBest(intentions.Top);Bottom=Cache.GetBest(intentions.Bottom);
    }
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        uint top=Top,bottom=Bottom;
        try{RefreshTargets();return Top!=uint.MaxValue&&Bottom!=uint.MaxValue;}
        catch(NativeConfigurationUnavailable error){Top=top;Bottom=bottom;ReportMissing("StackBlocks target refresh unavailable: "+error.Message);return false;}
    }
    private uint Carry=>_robot.CarryingOwner?.CarriedObjectId??uint.MaxValue;
    protected override void OnStart()
    {
        var manager=_robot.BehaviorManagerOwner??throw new InvalidOperationException("MISSING: StackBlocks actual manager absent");
        _hard=manager.ActiveSparkId!=0x55&&(manager.ActiveSparkSoft^1)!=0;
        if(Carry==Top)Stack();else Pickup();
    }
    private void Pickup()
    {
        State=0;SetStateName("PickingUpBlock");var ai=Context.AI??throw new InvalidOperationException("MISSING: StackBlocks actual AI helper owner");
        PickupHelper=new(_m,this,ai.Helpers.Factory,Top,_hard||_always?0f:BitConverter.UInt32BitsToSingle(0x42B40000),false,false);
        IHelper? raw=PickupHelper;var handle=ai.Helpers.AddHelperToComponent(ref raw);
        try{SmartDelegateToHelper(handle,_=>Stack(),null);}finally{handle.Release();}
        IncreaseScoreWhileActing(BitConverter.UInt32BitsToSingle(0x3F4CCCCD));
    }
    // fidelity: M12-028 — 005C9520..961A exact placement parameter producer.
    internal void Stack()
    {
        State=1;SetStateName("StackingBlock");
        if(Carry==uint.MaxValue||Carry!=Top)
        {_robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Unnamed","BehaviorStackBlocks.FailBackToPickup","wanted to stack, but we aren't carrying a block");Pickup();return;}
        var ai=Context.AI??throw new InvalidOperationException("MISSING: StackBlocks actual AI helper owner");
        PlacementHelper=new(_m,this,ai.Helpers.Factory,Bottom,false,0f,0f,true);
        IHelper? raw=PlacementHelper;var handle=ai.Helpers.AddHelperToComponent(ref raw);
        try{SmartDelegateToHelper(handle,_=>Final(),_=>Failed());}finally{handle.Release();}
        IncreaseScoreWhileActing(BitConverter.UInt32BitsToSingle(0x3F4CCCCD));
    }
    private void ClearTargets(){Top=Bottom=uint.MaxValue;Replaced=false;}
    protected override void OnStop(BehaviorStopReason reason)=>ClearTargets();
    internal void ResetBehavior()=>ClearTargets();
    protected override void OnUpdate()
    {
        try
        {
            using var mutation=_m.World.EnterPoseMutation();var intentions=Intentions();
            bool top=Cache.GetValid(intentions.Top).Contains(Top),bottom=Cache.GetValid(intentions.Bottom).Contains(Bottom);
            bool valid=top&&bottom;
            if(!bottom)
            {
                var bottomObject=_m.World.GetLocatedObjectByIdHelper(Bottom);var topObject=_m.World.GetLocatedObjectByIdHelper(Top);
                valid=bottomObject is not null&&topObject is not null&&ReferenceEquals(new NativeConfigurationGeometry(_m.World,_robot).Neighbor(bottomObject,true,15f,new BlockWorldFilter()),topObject);
            }
            if(!valid)
            {
                _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Unnamed","BehaviorStackBlocks.UpdateInternal.BlocksInvalid",$"Stopping due to invalid blocks topBlockValid:{(top?1:0)} bottomBlockValid:{(bottom?1:0)}");
                StopWithoutImmediateRepetitionPenalty();return;
            }
            if(State==1&&!Replaced)
            {
                uint next=ClosestBottom(intentions.Bottom);
                if(next!=Bottom){StopActing();Replaced=true;Bottom=next;Stack();}
            }
        }
        catch(NativeConfigurationUnavailable error)
        {ReportMissing("StackBlocks update unavailable: "+error.Message);Stop(BehaviorStopReason.Interrupted);}
    }
    private uint ClosestBottom(int intention)
    {
        uint best=Bottom;var current=_m.World.GetLocatedObjectByIdHelper(Bottom);if(current is null)return best;
        float distance=0;if(!PlacementProducerGeometry.TryDistanceSquared(_robot.GetCheckedMainPose(),current.NativePose,ref distance))return best;
        foreach(uint id in Cache.GetValid(intention))
        {
            var obj=_m.World.GetLocatedObjectByIdHelper(id);if(obj is null)continue;
            float value=0;if(PlacementProducerGeometry.TryDistanceSquared(_robot.GetCheckedMainPose(),obj.NativePose,ref value)&&value<distance){distance=value;best=id;}
        }
        return best;
    }
    private void Final()
    {
        State=2;SetStateName("PlayingFinalAnim");_robot.Engine.ReportBehaviorObjective(40,"StackedBlock",Id);
        if(!_hard&&!_always)
        {StartActing(new TriggerAnimationAction(_m.Robot,new((AnimationTrigger)0x21A,1,true,0,60f,false)),null);IncreaseScoreWhileActing(BitConverter.UInt32BitsToSingle(0x3F4CCCCD));}
        if(_needs is {} action)_robot.Engine.Needs.RegisterNeedsActionCompleted(action);
    }
    internal void Failed()
    {
        if(Carry==uint.MaxValue){RecoveryComplete();return;}
        var sequence=new RecoverySequence(this);sequence.SeedNativeEmptyCompletion();
        sequence.AddAction(new DriveStraightAction(_m,-40f,100f,true),false,false);
        sequence.AddAction(new NativePlaceGroundRunner(_m),false,false);StartActing(sequence,_=>RecoveryComplete());
    }
    private void RecoveryComplete()
    {
        try{RefreshTargets();if(Top!=uint.MaxValue&&Bottom!=uint.MaxValue)Pickup();}
        catch(NativeConfigurationUnavailable error){ReportMissing("StackBlocks recovery targets unavailable: "+error.Message);}
    }
    private sealed class RecoverySequence(NativeStackBlocksBehavior owner):CompoundActionSequential(()=>owner._robot.Engine.Timer.SecondsF)
    {
        private bool _unavailable;
        public override uint Update()
        {
            if(_unavailable)return EngineActionResult.Running;int depth=Watcher?.HostUpdateDepth??0;
            try{return base.Update();}
            catch(NativeActionUnavailable error)
            {
                Watcher?.UnwindUnavailableHostUpdate(depth);_unavailable=true;ReportMissing("StackBlocks recovery child unavailable: "+error.Message);
                owner._robot.ActionList.DeferHostCleanup(()=>
                {
                    if(owner.CurrentRunnerTag!=Tag)return;
                    var manager=owner._robot.BehaviorManagerOwner;
                    if(ReferenceEquals(manager?.Current,owner))manager.Stop(BehaviorStopReason.Interrupted,owner._robot.Engine.Timer.SecondsF);
                    else owner.Stop(BehaviorStopReason.Interrupted);
                });return EngineActionResult.Running;
            }
        }
    }
}
