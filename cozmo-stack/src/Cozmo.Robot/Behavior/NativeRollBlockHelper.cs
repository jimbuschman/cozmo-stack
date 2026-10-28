using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// Proposed Roll own-path R1-R8:005B98D4..005BAC54, actual native helper stack and action ownership.
internal sealed class NativeRollBlockHelper:IHelper
{
    private readonly ManipulationSystem _m;
    private readonly SteppedBehavior _parent;
    private readonly Action? _beforeDock;
    private Action<RobotCompletedAction>? _response;
    private bool _unavailable;
    private int _epoch;
    internal uint Target {get;}
    internal bool Upright {get;}
    internal float MaxFaceTurn {get;}
    internal bool First {get;private set;}=true;
    internal uint Attempts {get;private set;}
    internal NativeDriveToHelper? DriveHelper {get;private set;}
    internal NativePlaceBlockHelper? PlaceHelper {get;private set;}
    internal NativePickupBlockHelper? PickupHelper {get;private set;}
    internal NativeDriveToRollRunner? Runner {get;private set;}
    internal NativeRollBlockHelper(ManipulationSystem m,SteppedBehavior parent,BehaviorHelperFactory factory,uint target,bool upright,float maxFaceTurn,Action? beforeDock)
        :base(m.Robot,"RollBlock",parent,factory){_m=m;_parent=parent;Target=target;Upright=upright;MaxFaceTurn=maxFaceTurn;_beforeDock=beforeDock;}
    public override bool ShouldCancelDelegates(CozmoRobot robot)=>false;
    protected override void OnInitializeOnStackSlot20(){_unavailable=false;Interlocked.Increment(ref _epoch);}
    protected override void OnStopSlot24(bool first){Interlocked.Increment(ref _epoch);_response=null;}
    protected override BehaviorStatus Init(CozmoRobot robot){Attempts=0;First=true;Determine();return Status;}
    protected override BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot)=>Status;
    internal void Determine()
    {
        if(_unavailable)return;
        try
        {
            using var mutation=_m.World.EnterPoseMutation();var owner=_m.Robot.Engine.Robot!;
            if(owner.CarryingOwner!.CarriedObjectId is not null){PutDown();return;}
            var target=_m.World.GetLocatedObjectByIdHelper(Target);if(target is null){StoreStatus(BehaviorStatus.Failure);return;}
            var cache=owner.NativeBehaviorRuntime!.PickupTargets!;
            if(!cache.CanRollFromGround(target)){UnableToRoll();return;}
            if(owner.StoredState is not {} state||_m.Vision.Calibration is not {} calibration)throw new NativeActionUnavailable("Roll pre-action actual state/calibration unavailable");
            using var main=_m.NativeMainSnapshot();
            var pre=PlaceRelObjectOffsetPoses.IsAtPreActionPoseWithVisualVerificationNative(target,PreActionType.Rolling,0,0,state.Timestamp,main,owner.CarryingOwner.CarriedObjectId,calibration,ObjectPoseQueries.Engine,()=>_m.NativeObstacles(main),_m.World);
            if(pre==ActionResult.Success){StartRolling();return;}
            var drive=Factory.CreateNativeDriveTo(_m,_parent,Target,false,PreActionType.Rolling);DriveHelper=(NativeDriveToHelper)drive.Pointer;
            if(Upright&&NativeRollGeometry.TryApproach(_m,Target,out float angle))DriveHelper.SetApproachAngle(angle);
            Delegation.Delegate=drive;Delegation.OnSuccessHandler=_=>{StartRolling();return Status;};Delegation.OnFailureHandler=_=>{Determine();return Status;};
        }
        catch(NativeActionUnavailable error){Unavailable(error);}
        catch(NativeConfigurationUnavailable error){Unavailable(new NativeActionUnavailable(error.Message));}
    }
    private void PutDown()
    {
        PlaceHelper=new(_m,_parent,Factory);IHelper? raw=PlaceHelper;Delegation.Delegate=Factory.Component.AddHelperToComponent(ref raw);
        Delegation.OnSuccessHandler=_=>{First=true;Determine();return BehaviorStatus.Running;};Delegation.OnFailureHandler=_=>BehaviorStatus.Failure;
    }
    private void UnableToRoll()
    {
        var target=_m.World.GetLocatedObjectByIdHelper(Target);if(target is null){StoreStatus(BehaviorStatus.Failure);return;}
        var cache=_m.Robot.Engine.Robot!.NativeBehaviorRuntime!.PickupTargets!;
        bool pick=cache.GetValid(0).Contains(Target);
        var filter=new BlockWorldFilter();filter.AllowedFamilies.UnionWith(new[]{(ObjectFamily)2,(ObjectFamily)1});
        var top=new NativeConfigurationGeometry(_m.World,_m.Robot.Engine.Robot).Neighbor(target,true,30f,filter);
        uint id=pick?Target:top?.ObjectId??uint.MaxValue;
        if(id==uint.MaxValue)
        {
            _m.Robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"BehaviorHelpers","RollBlockHelper.UnableToRollDelegate.CantInteract","Cannot interact with target or object on top");
            return; //005BA332..37E: preserve Running; no invented failure, timeout or hang guard.
        }
        PickupHelper=new(_m,_parent,Factory,id,0,false,false);IHelper? raw=PickupHelper;Delegation.Delegate=Factory.Component.AddHelperToComponent(ref raw);
        Delegation.OnSuccessHandler=_=>{PutDown();return Status;};Delegation.OnFailureHandler=_=>BehaviorStatus.Failure;
    }
    // fidelity: M12-016 — only the unsigned three-attempt cap belongs to this existing exact record.
    internal void StartRolling()
    {
        if(_unavailable)return;
        try
        {
            if(Attempts>=3){Mark();StoreStatus(BehaviorStatus.Failure);return;}
            First=false;Attempts=unchecked(Attempts+1);
            Runner=new(_m,Target);if(Upright)Runner.RollToUpright();
            Runner.BeforeDock=_beforeDock;Runner.SetFaceParameters(MaxFaceTurn,0x1F6,0x1F7);Runner.Unavailable=Unavailable;
            StartWithResponse(Runner,Respond);
        }
        catch(NativeActionUnavailable error){Unavailable(error);}
    }
    internal void StartWithResponse(IActionRunner runner,Action<RobotCompletedAction> callback)
    {
        _response=callback;
        _parent.StartActing(runner,completed=>
        {
            var director=_m.Robot.Engine.Robot?.NativeBehaviorRuntime?.AI.PickupResponses;
            if(director is null){SteppedBehavior.ReportMissing("Roll actual AI+C response director unavailable; original callback retained");return;}
            byte mapped=completed.Result==0x04000003?(byte)1:completed.Result==0?(byte)2:(byte)0;
            int trigger=director.Trigger(mapped);
            void Complete(){var saved=_response;_response=null;saved?.Invoke(completed);}
            if(trigger==0x23F)Complete();
            else _parent.StartActing(new TriggerAnimationAction(_m.Robot,new((AnimationTrigger)trigger,1,true,0,60f,false)),_=>Complete());
        });
    }
    internal void Respond(RobotCompletedAction completed)
    {if(completed.Result==0x02000000)return;if(completed.Result==0){StoreStatus(BehaviorStatus.Complete);return;}StartRolling();}
    private void Mark(){if(_m.World.GetLocatedObjectByIdHelper(Target) is {} target)_m.Whiteboard.SetFailedToUse(target,(ObjectActionFailure)3);}
    private void Unavailable(NativeActionUnavailable error)
    {
        _unavailable=true;_response=null;SteppedBehavior.ReportMissing("Roll native child unavailable: "+error.Message);
        int epoch=Volatile.Read(ref _epoch);uint tag=_parent.CurrentRunnerTag;
        _m.Robot.Engine.Robot!.ActionList.DeferHostCleanup(()=>
        {
            if(epoch!=Volatile.Read(ref _epoch)||_parent.CurrentRunnerTag!=tag)return;
            var manager=_m.Robot.Engine.Robot!.BehaviorManagerOwner;
            if(ReferenceEquals(manager?.Current,_parent))manager.Stop(BehaviorStopReason.Interrupted,_m.Robot.Engine.Timer.SecondsF);else _parent.Stop(BehaviorStopReason.Interrupted);
        });
    }
}
