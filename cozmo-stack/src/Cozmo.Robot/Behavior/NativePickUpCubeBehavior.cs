using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

//005C64D4..005C732A: actual configured instance, separate from the public manual behavior adapter.
internal sealed class NativePickUpCubeBehavior:SteppedBehavior,IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly BlockConfigurationType[] _ignored;
    private readonly bool _alwaysStreamline;
    private bool _streamline;
    private RetainedSubscription? _objectEvent;
    internal uint Target {get;private set;}=uint.MaxValue;
    internal NativePickupBlockHelper? Helper {get;private set;}
    internal NativePickUpCubeBehavior(BehaviorFactoryContext source,JsonElement json)
        :base(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString(),"PickUpCube")
    {
        _m=source.Manipulation??throw new InvalidOperationException("Pickup factory actual manipulation absent");
        _robot=source.NativeRobot??throw new InvalidOperationException("Pickup factory actual Robot absent");
        _ignored=json.TryGetProperty("ignoreCubesInBlockConfigTypes",out var values)
            ?values.EnumerateArray().Select(v=>Enum.Parse<BlockConfigurationType>(v.GetString()!,false)).ToArray():Array.Empty<BlockConfigurationType>();
        _alwaysStreamline=json.TryGetProperty("alwaysStreamline",out var flag)&&flag.GetBoolean();
        if(_robot.HasExternalInterface)_objectEvent=_m.Robot.Engine.AnimationMessages.SubscribeMessage(0x44,true,_=>{}); //0059EC40/46/4C BX LR; retained through Stop.
    }
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction; //parent84 only, never helper presence.
    public override bool IsRunnable(BehaviorContext context)=>IsRunnableBase(context)&&IsRunnableInternal(context);
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        using var mutation=_m.World.EnterPoseMutation();
        uint previous=Target;
        try
        {
        Target=uint.MaxValue;
        var cache=_robot.NativeBehaviorRuntime?.PickupTargets;
        if(cache is null||!cache.Refresh()){Target=previous;return false;}
        bool Pass(ObservableObject obj)=>!_ignored.Any(type=>_m.NativeCache(type).Any(c=>c.ContainsBlock(obj.ObjectId)));
        if(_ignored.Length==0)Target=cache.Best;
        else
        {
            var filter=cache.Filter(Pass);var admitted=_m.World.FindLocatedMatchingObjects(filter);
            var prior=_m.World.GetLocatedObjectByIdHelper(cache.Best);
            if(prior is not null&&admitted.Any(obj=>ReferenceEquals(obj,prior)))Target=cache.Best;
            else Target=NativeClosestSpatialQuery.Find(_m.World,_robot.GetCheckedMainPose(),new Vec3(float.MaxValue,float.MaxValue,float.MaxValue),filter)?.ObjectId??uint.MaxValue;
        }
        return Target!=uint.MaxValue;
        }
        catch(NativeConfigurationUnavailable error)
        {Target=previous;ReportMissing("Pickup target refresh unavailable; prior target retained: "+error.Message);return false;}
    }
    internal bool CheckExecutionAvailability()
    {
        if(!_robot.HasExternalInterface||_robot.CarryingOwner is null||_robot.DockingOwner is null||_robot.StoredState is null||_m.Vision.Calibration is null)
        {ReportMissing("PickUpCube execution requires actual carry/dock/raw-state/calibration owners");return false;}
        using var mutation=_m.World.EnterPoseMutation();
        var target=_m.World.GetLocatedObjectByIdHelper(Target);
        if(target is null||!CubeGeometry.IsCube(target.Type)){ReportMissing("PickUpCube passive/absent target recipient unavailable");return false;}
        using var main=new Pose3d(_robot.GetCheckedMainPose());
        var pre=PlaceRelObjectOffsetPoses.IsAtPreActionPoseWithVisualVerification(target,PreActionType.Docking,0,0,_robot.StoredState.Timestamp,main,
            _robot.CarryingOwner.CarriedObjectId,_m.Vision.Calibration,ObjectPoseQueries.Engine,()=>_m.GetObstacles(main));
        if(pre!=ActionResult.Success){ReportMissing("PickUpCube direct pre-action input unavailable; required DriveToHelper recipient remains MISSING; prior selection retained");return false;}
        return true;
    }
    public override Task StartAsync(BehaviorContext context,BehaviorScope scope,CancellationToken cancel)
    {
        if(!CheckExecutionAvailability())throw new NotSupportedException("MISSING: native pickup visual child recipient unavailable");
        return base.StartAsync(context,scope,cancel);
    }
    protected override void OnStart()
    {
        var manager=_robot.BehaviorManagerOwner;
        if(manager is null){ReportMissing("Pickup Init manager active-spark owner unavailable");return;}
        _streamline=manager.ActiveSparkId!=0x55&&(manager.ActiveSparkSoft^1)!=0;
        if(_streamline||_alwaysStreamline)StartPickup();
        else StartActing(new TriggerLiftSafeAnimationAction(_m.Robot,new(AnimationTrigger.SparkPickupInitialCubeReaction,1,true,0,60f,false)),_=>StartPickup());
    }
    private void StartPickup()
    {
        if(Context.AI is not {} ai){ReportMissing("Pickup helper actual AI owner unavailable");return;}
        SetStateName("PickingUpCube");
        Helper=new NativePickupBlockHelper(_m,this,ai.Helpers.Factory,Target,
            _streamline||_alwaysStreamline?0f:BitConverter.UInt32BitsToSingle(0x3FC90FDB),!(_streamline||_alwaysStreamline),true);
        IHelper? raw=Helper;var handle=ai.Helpers.AddHelperToComponent(ref raw);
        try{SmartDelegateToHelper(handle,_=>Success(),null);}finally{handle.Release();}
    }
    private void Success()
    {
        if(!_streamline&&!_alwaysStreamline)
            StartActing(new TriggerAnimationAction(_m.Robot,new(AnimationTrigger.ReactToBlockPickupSuccess,1,true,0,60f,false)),null);
    }
    protected override void OnUpdate()
    {
        if(_ignored.Any(type=>_m.NativeCache(type).Any(c=>c.ContainsBlock(Target))))
            StopWithoutImmediateRepetitionPenalty();
    }
    public void Dispose(){_objectEvent?.Dispose();_objectEvent=null;}
}
