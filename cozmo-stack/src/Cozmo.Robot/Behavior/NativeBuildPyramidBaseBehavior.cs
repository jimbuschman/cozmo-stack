using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
namespace Cozmo.Robot.Behavior;

// Proposed coherent Base lifecycle owner; no assigned fidelity ID. BUILD-PYRAMID-BASE-1 E1..G2.
internal sealed class NativeBuildPyramidBaseBehavior:SteppedBehavior
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    internal uint StaticId {get;private set;}=uint.MaxValue;
    internal uint BaseId {get;private set;}=uint.MaxValue;
    internal uint TopId {get;private set;}=uint.MaxValue;
    internal int State {get;private set;}
    internal int PreviousBaseCount {get;private set;}
    internal float Appeared {get;private set;}=-1f;
    internal float Lost {get;private set;}=-1f;
    internal bool StopWhenFull {get;set;}
    internal Action<EngineRobot>? ObservationFinished {get;set;}
    internal NativePickupBlockHelper? PickupHelper {get;private set;}
    internal NativePlaceRelObjectHelper? PlacementHelper {get;private set;}
    internal NativeBuildPyramidBaseBehavior(BehaviorFactoryContext source,JsonElement json)
        :base(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString(),"BuildPyramidBase")
    {_m=source.Manipulation!;_robot=source.NativeRobot!;}
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>true;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    public override bool IsRunnable(BehaviorContext context)=>IsRunnableBase(context)&&IsRunnableInternal(context);
    private NativeBehaviorRuntime Runtime=>_robot.NativeBehaviorRuntime??throw new NativeConfigurationUnavailable("BuildPyramidBase actual runtime unavailable");
    // R1:005DCAFA..CB4E. Input suppliers are explicit; no nearest/all-pairs replacement.
    internal bool RefreshTargets()
    {
        var input=Runtime.PyramidTargetInput??throw new NativeConfigurationUnavailable("BuildPyramidBase complete intention9/10/11 producer MISSING");
        uint basis=input(9),stat=input(10),top=input(11);
        bool changed=basis!=BaseId||stat!=StaticId||top!=TopId;
        BaseId=basis;StaticId=stat;TopId=top;return changed;
    }
    private IReadOnlyList<NativePyramidBase> Bases()=>Runtime.PyramidBaseStrongInput?.Invoke()
        ??throw new NativeConfigurationUnavailable("M12-049 BuildPyramidBase strong cache+48 producer MISSING (unavailable is not empty)");
    private IReadOnlyList<NativePyramidConfiguration> Full()=>Runtime.PyramidFullStrongInput?.Invoke()
        ??throw new NativeConfigurationUnavailable("M12-049 BuildPyramidBase strong cache+64 producer MISSING (unavailable is not empty)");
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        try{RefreshTargets();return StaticId!=uint.MaxValue&&BaseId!=uint.MaxValue&&TopId==uint.MaxValue&&Bases().Count==0;}
        catch(NativeConfigurationUnavailable error){ReportMissing(error.Message);return false;}
    }
    // I1:005DCB58..B90; preserves IDs, flag and optional derived callback.
    internal void ResetBehavior(){PreviousBaseCount=0;Appeared=Lost=-1f;}
    protected override void OnStart()
    {
        ResetBehavior();
        if(_robot.CarryingOwner is not {} carry)throw new NativeConfigurationUnavailable("BuildPyramidBase actual carry owner unavailable");
        if((carry.CarriedObjectId??uint.MaxValue)!=uint.MaxValue)Place();else Pickup();
    }
    private BehaviorManager Manager=>_robot.BehaviorManagerOwner??throw new NativeConfigurationUnavailable("BuildPyramidBase actual state-light manager unavailable");
    private AIComponent AI=>Context.AI??throw new NativeConfigurationUnavailable("BuildPyramidBase actual helper component unavailable");
    private void Pickup()
    {
        State=0;SetStateName("DrivingToBaseBlock");
        Manager.SetBehaviorStateLights(BehaviorClass.BuildPyramidBase,new[]{new BehaviorStateLightInfo(BaseId,0x20,false,BehaviorStateLightInfo.Zero())},false);
        // D2/D3:005DCC16..CC78 and checked B1 F8 one-use pre-animation.
        PickupHelper=new(_m,this,AI.Helpers.Factory,BaseId,0f,false,false,0x16);
        IHelper? raw=PickupHelper;var handle=AI.Helpers.AddHelperToComponent(ref raw);
        try{SmartDelegateToHelper(handle,_=>Place(),null);}finally{handle.Release();}
    }
    // fidelity: M12-028 — P1..P4,005DCDCC..CECA, B1..B7.
    internal void Place()
    {
        State=1;SetStateName("PlacingBaseBlock");
        Manager.SetBehaviorStateLights(BehaviorClass.BuildPyramidBase,Array.Empty<BehaviorStateLightInfo>(),false);
        if(_m.World.GetLocatedObjectByIdHelper(StaticId) is null)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorBuildPyramidBase.TransitionToPlacingBaseBlock.NullObject",$"Object {unchecked((int)StaticId)} is NULL");
            StaticId=uint.MaxValue;return;
        }
        float a=0,b=0;PlacementProducerGeometry.UpdateOffsets(_m,StaticId,BaseId,ref a,ref b);
        PlacementHelper=new(_m,this,AI.Helpers.Factory,StaticId,true,a,b,false);
        IHelper? raw=PlacementHelper;var handle=AI.Helpers.AddHelperToComponent(ref raw);
        try{SmartDelegateToHelper(handle,_=>Observe(),null);}finally{handle.Release();}
    }
    // O1..O3:005DD4AC..D56C,005DDC62..C74. Completion dispatch has no fabricated reward/state.
    internal void Observe()
    {
        State=2;SetStateName("ObservingBase");
        var sequence=new CompoundActionSequential(()=>_robot.Engine.Timer.SecondsF);
        sequence.AddAction(new DriveStraightAction(_m,-40f,40f,true),false,false);
        sequence.AddAction(new TriggerLiftSafeAnimationAction(_m.Robot,new((AnimationTrigger)20,1,true,0,60f,false)),false,false);
        StartActing(sequence,_=>ObservationFinished?.Invoke(_robot));
    }
    // U1..U5:005DD026..D12C. Two distinct actual f32 timer samples where source samples.
    protected override void OnUpdate()
    {
        try
        {
            int count=Bases().Count;int full=Full().Count;
            if(PreviousBaseCount==0&&count!=0)Appeared=_robot.Engine.Timer.SecondsF;
            if(PreviousBaseCount>0&&count==0&&full==0)Lost=_robot.Engine.Timer.SecondsF;
            float now=_robot.Engine.Timer.SecondsF,epsilon=BitConverter.UInt32BitsToSingle(0x3727C5AC);
            bool stop=count!=0&&State<=1&&(Appeared+2f)+epsilon<now;
            if(count==0&&full==0&&Lost>epsilon&&(Lost+2f)+epsilon<now)stop=true;
            if(StopWhenFull&&full!=0)stop=true;
            if(stop){StopWithoutImmediateRepetitionPenalty();return;}
            PreviousBaseCount=count;
            if(!HasCurrentAction)Finish(); // shared005BDA56 returns2 iff actual +84 pointer is null.
        }
        catch(NativeConfigurationUnavailable error){ReportMissing(error.Message);Stop(BehaviorStopReason.Interrupted);}
    }
    // G1/G2:005DD5F4..D64C /005DDA60..DAB6. Invalid getters preserve output.
    internal bool GetStatic(ref uint output)=>Get(StaticId,ref output);
    internal bool GetBase(ref uint output)=>Get(BaseId,ref output);
    internal bool GetTop(ref uint output)=>Get(TopId,ref output);
    private static bool Get(uint id,ref uint output){if(id==uint.MaxValue)return false;output=id;return true;}
    internal bool AreAllBlockIdsUnique()=>StaticId==uint.MaxValue||BaseId==uint.MaxValue
        ||(TopId==uint.MaxValue?StaticId!=BaseId:StaticId!=BaseId&&StaticId!=TopId&&BaseId!=TopId);
}
