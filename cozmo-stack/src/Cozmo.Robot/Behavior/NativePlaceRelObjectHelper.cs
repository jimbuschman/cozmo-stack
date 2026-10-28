using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M12-042 — actual native parent/component binding, P1-P3 exact Stack tuple.
internal sealed class NativePlaceRelObjectHelper: IHelper
{
    private readonly ManipulationSystem _m;
    private readonly SteppedBehavior _parent;
    internal uint Target {get;}
    internal bool Low {get;}
    internal float A {get;}
    internal float B {get;}
    internal bool SkipTransform {get;}
    internal uint Attempts {get;private set;}
    internal NativeDriveToHelper? DriveHelper {get;private set;}
    internal NativePlaceRelRunner? Runner {get;private set;}
    private Action<RobotCompletedAction>? _response;
    private bool _hostUnavailable;
    private int _epoch;
    internal NativePlaceRelObjectHelper(ManipulationSystem m,SteppedBehavior parent,BehaviorHelperFactory factory,uint target,bool low,float a,float b,bool skip)
        :base(m.Robot,"PlaceRelObject",parent,factory){_m=m;_parent=parent;Target=target;Low=low;A=a;B=b;SkipTransform=skip;}
    public override bool ShouldCancelDelegates(CozmoRobot robot)=>false;
    protected override void OnInitializeOnStackSlot20(){_hostUnavailable=false;Interlocked.Increment(ref _epoch);}
    protected override void OnStopSlot24(bool first)=>Interlocked.Increment(ref _epoch);
    protected override BehaviorStatus Init(CozmoRobot robot)
    {Attempts=0;try{if(Precheck()==0)StartPlace();else Drive();}catch(NativeActionUnavailable error){Unavailable(error);}return Status;}
    protected override BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot)=>Status;
    private uint Precheck()
    {
        var owner=_m.Robot.Engine.Robot!;
        if(owner.StoredState is not {} state||_m.Vision.Calibration is not {} calibration)throw new NativeActionUnavailable("M12-042 placement pre-action actual raw-state/calibration unavailable");
        using var main=_m.NativeMainSnapshot();
        return (uint)PlaceRelObjectOffsetPoses.IsAtPreActionPoseWithVisualVerificationNative(_m.World.GetLocatedObjectByIdHelper(Target),PreActionType.PlaceRelative,A,B,state.Timestamp,main,
            owner.CarryingOwner!.CarriedObjectId,calibration,ObjectPoseQueries.Engine,()=>_m.NativeObstacles(main),_m.World);
    }
    private void Drive()
    {
        if(A!=0f||B!=0f)throw new NativeActionUnavailable("M12-042/M12-022 nonzero placement Drive1 tuple not built; no ordinary drive substitution");
        var drive=Factory.CreateNativeDriveTo(_m,_parent,Target,false,PreActionType.PlaceRelative);DriveHelper=(NativeDriveToHelper)drive.Pointer;
        Delegation.Delegate=drive;Delegation.OnSuccessHandler=_=>{StartPlace();return Status;};Delegation.OnFailureHandler=null;
    }
    internal void StartPlace()
    {
        if(_hostUnavailable)return;
        try{StartPlaceAvailable();}catch(NativeActionUnavailable error){Unavailable(error);}
    }
    private void StartPlaceAvailable()
    {
        if(Attempts>=3){Mark();StoreStatus(BehaviorStatus.Failure);return;}
        Attempts=unchecked(Attempts+1);if(Precheck()!=0){Drive();return;}
        if(Low||A!=0f||B!=0f||!SkipTransform)throw new NativeActionUnavailable("M12-042 placement parameter tuple outside checked native Stack leaf; no manual adapter substituted");
        Runner=new NativePlaceRelRunner(_m,Target);
        Runner.Unavailable=Unavailable;
        _response=Respond;
        _parent.StartActing(Runner,completed=>
        {
            // P2's empty converter has no response-animation lookup. Saved scalar callback remains source-owned.
            var callback=_response;_response=null;callback?.Invoke(completed);
        });
    }
    private void Unavailable(NativeActionUnavailable error)
    {
        _hostUnavailable=true;_response=null;SteppedBehavior.ReportMissing("M12-042 native PlaceRel input unavailable: "+error.Message);
        int epoch=Volatile.Read(ref _epoch);uint tag=_parent.CurrentRunnerTag;
        _m.Robot.Engine.Robot?.ActionList.DeferHostCleanup(()=>
        {
            if(epoch!=Volatile.Read(ref _epoch)||!_parent.HasLiveHelper||!ReferenceEquals(_parent.HelperWeakReference?.Pointer,this)||_parent.CurrentRunnerTag!=tag)return;
            var manager=_m.Robot.Engine.Robot?.BehaviorManagerOwner;
            if(ReferenceEquals(manager?.Current,_parent))manager.Stop(BehaviorStopReason.Interrupted,_m.Robot.Engine.Timer.SecondsF);
            else _parent.Stop(BehaviorStopReason.Interrupted);
        });
    }
    internal void Respond(RobotCompletedAction completed)
    {
        switch(completed.Result)
        {
            case 0:StoreStatus(BehaviorStatus.Complete);break;
            case 0x02000000:break;
            case 0x03000000:case 0x03000004:StoreStatus(BehaviorStatus.Failure);break;
            case 0x03000010:_m.Whiteboard.NoPreDockPosesObjectId=Target;break;
            default:StartPlace();break;
        }
    }
    private void Mark()
    {
        var target=_m.World.GetLocatedObjectByIdHelper(Target);uint id=_m.Robot.Engine.Robot!.CarryingOwner!.CarriedObjectId??uint.MaxValue;
        var carried=_m.World.GetLocatedObjectByIdHelper(id);if(target is null||carried is null)return;
        if(!Low)_m.Whiteboard.SetFailedToUse(target,ObjectActionFailure.StackOnObject);
        else throw new NativeActionUnavailable("M12-042 low placement MarkFailed pose branch not built in Stack tuple");
    }
}
