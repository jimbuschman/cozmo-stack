using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// Proposed coherent Roll own-path owner H1-H11 (005C8598..005C8E3C), not M12-016's attempt-only claim.
internal sealed class NativeRollBlockBehavior:SteppedBehavior
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly bool _always;
    private readonly string? _need;
    internal bool RotationImportant {get;}
    internal uint Target {get;private set;}=uint.MaxValue;
    internal int StartAxis {get;private set;}=1;
    internal int State {get;private set;}
    internal bool PreDockReached {get;private set;}
    internal bool HardSpark {get;private set;}
    internal NativeRollBlockHelper? RollHelper {get;private set;}
    internal NativeRollBlockBehavior(BehaviorFactoryContext source,JsonElement json):base(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString(),"RollBlock")
    {
        _m=source.Manipulation!;_robot=source.NativeRobot!;
        RotationImportant=!json.TryGetProperty("isBlockRotationImportant",out var rotation)||rotation.GetBoolean();
        _always=json.TryGetProperty("alwaysStreamline",out var always)&&always.GetBoolean();
        _need=json.TryGetProperty("needsActionID",out var need)?need.GetString():null;
    }
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>true;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction||HasLiveHelper;
    public override bool IsRunnable(BehaviorContext context)=>IsRunnableBase(context)&&IsRunnableInternal(context);
    internal IReadOnlySet<int> OwnSlot2C()=>new HashSet<int>{RotationImportant?7:6};
    internal void OwnSlot7C()=>RefreshTarget();
    internal void RefreshTarget()=>Target=(_robot.NativeBehaviorRuntime?.PickupTargets??throw new NativeConfigurationUnavailable("actual Roll intention owner absent")).GetBest(RotationImportant?7:6);
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        try{RefreshTarget();return Target!=uint.MaxValue||HasCurrentAction;}
        catch(NativeConfigurationUnavailable error){ReportMissing("Roll target refresh unavailable: "+error.Message);return false;}
    }
    protected override void OnStart()
    {
        PreDockReached=false;
        var manager=_robot.BehaviorManagerOwner??throw new NativeActionUnavailable("Roll actual spark manager absent");
        HardSpark=manager.ActiveSparkId!=0x55&&(manager.ActiveSparkSoft^1)!=0;
        using var mutation=_m.World.EnterPoseMutation();
        if(_m.World.GetLocatedObjectByIdHelper(Target) is not {} obj){InitFailed=true;return;}
        StartAxis=RotatedParentAxis.GetNativeZ(obj.NativePose.Rotation);Perform();
    }
    private void Perform()
    {
        State=0;SetStateName("PerformingAction");
        if(Target==uint.MaxValue){_robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorRollBlock.NoBlockID","No block ID");return;}
        var ai=Context.AI??throw new NativeActionUnavailable("Roll actual AI/helper owner absent");
        RollHelper=new(_m,this,ai.Helpers.Factory,Target,true,_always||HardSpark?0f:BitConverter.UInt32BitsToSingle(0x3FC90FDB),()=>PreDockReached=true);
        IHelper? raw=RollHelper;var handle=ai.Helpers.AddHelperToComponent(ref raw);
        try{SmartDelegateToHelper(handle,_=>Success(),_=>Mark());}finally{handle.Release();}
        IncreaseScoreWhileActing(BitConverter.UInt32BitsToSingle(0x3F4CCCCD));
        _robot.BehaviorManagerOwner!.SetBehaviorStateLights(BehaviorClass.RollBlock,new[]{new BehaviorStateLightInfo(Target,25,false,BehaviorStateLightInfo.Zero())},false);
    }
    private void Mark(){if(_m.World.GetLocatedObjectByIdHelper(Target) is {} obj)_m.Whiteboard.SetFailedToUse(obj,(ObjectActionFailure)3);}
    protected override void OnUpdate()
    {
        using var mutation=_m.World.EnterPoseMutation();
        var target=_m.World.GetLocatedObjectByIdHelper(Target);if(target is null||State!=0)return;
        int axis=RotatedParentAxis.GetNativeZ(target.NativePose.Rotation);if(axis==StartAxis)return;
        float distance=0;if(!PlacementProducerGeometry.TryDistanceSquared(_robot.GetCheckedMainPose(),target.NativePose,ref distance)||!(distance>14400f))return;
        StartAxis=axis;StopActing(false,false);
        if(PreDockReached){Success();return;}
        try{RefreshTarget();if(Target!=uint.MaxValue)Perform();else Finish();}
        catch(NativeConfigurationUnavailable error){ReportMissing("Roll retarget unavailable: "+error.Message);Stop(BehaviorStopReason.Interrupted);}
    }
    private void Success()
    {
        State=1;SetStateName("CelebratingRoll");
        if(Context.Mood is {} mood)mood.Trigger("RollSucceeded",_robot.Engine.Timer.SecondsF);
        else ReportMissing("RollSucceeded actual MoodManager unavailable");
        if(_m.World.GetLocatedObjectByIdHelper(Target) is {} obj)StartAxis=RotatedParentAxis.GetNativeZ(obj.NativePose.Rotation);
        if(!_always&&!HardSpark)StartActing(new TriggerAnimationAction(_m.Robot,new((AnimationTrigger)0x1FB,1,true,0,60f,false)),null);
        IncreaseScoreWhileActing(BitConverter.UInt32BitsToSingle(0x3F4CCCCD));
        _robot.Engine.ReportBehaviorObjective(2,"RolledBlock",Id);
        if(_need is {} need)_robot.Engine.Needs.RegisterNeedsActionCompleted(need);
    }
    protected override void OnStop(BehaviorStopReason reason)=>Target=uint.MaxValue;
    internal void ResetBehavior()=>Target=uint.MaxValue;
}
