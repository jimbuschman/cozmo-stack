using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M12-029
// 5B56B0/5B5800/5B5838/5B5C94: ordinary type0 helper, owned by the real AI helper stack.
internal sealed class NativeDriveToHelper:IHelper,IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly SteppedBehavior _parent;
    private bool _useApproach,_specialRetry;
    private float _approach;
    internal void SetApproachAngle(float angle){_useApproach=true;_approach=new Radians(angle).Value;_specialRetry=true;}
    private Pose3d? _snapshot;
    private readonly object _snapshotGate=new();
    internal uint Target {get;}
    internal bool Force {get;}
    internal PreActionType ActionType {get;}
    internal uint Attempts {get;private set;}
    internal NativeDriveToObjectRunner? Runner {get;private set;}
    internal NativeSearchForBlockHelper? SearchHelper {get;private set;}
    internal NativeDriveToHelper(ManipulationSystem m,SteppedBehavior parent,BehaviorHelperFactory factory,uint target,bool force,PreActionType type=PreActionType.Docking)
        :base(m.Robot,"DriveTo",parent,factory){_m=m;_parent=parent;Target=target;Force=force;ActionType=type;}
    public override bool ShouldCancelDelegates(CozmoRobot robot)=>false;
    protected override BehaviorStatus Init(CozmoRobot robot)
    {
        var snapshot=_m.NativeMainSnapshot();lock(_snapshotGate){_snapshot?.Dispose();_snapshot=snapshot;}
        Drive();return Status;
    }
    protected override BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot)=>Status;
    private void Drive()
    {
        if(Attempts>=3){StoreStatus(BehaviorStatus.Failure);return;}
        Attempts=unchecked(Attempts+1);
        var runner=new NativeDriveToObjectRunner(_m,Target,ActionType);Runner=runner;
        runner.Action.UseApproachAngle=_useApproach;runner.Action.ApproachAngleRad=_approach;
        if(Force)
        {
            runner.SetPosesFunction((ObservableObject obj,out IReadOnlyList<PreActionPose> poses,ref bool inPosition)=>
            {
                var result=runner.Action.GetPossiblePoses(obj,out poses,ref inPosition);
                if(result!=ActionResult.Success)return result;
                var list=poses.ToList();
                Pose3d snapshot;
                lock(_snapshotGate)
                    snapshot=_snapshot is {} live?new Pose3d(live):throw new NativeActionUnavailable("M1-034 M12-029 force callback5B61A2 reached a retired helper snapshot114; skip owned action");
                using var invocationSnapshot=snapshot; // invoke-time lifetime pin, never a replacement/cached callback input.
                if(DockPreActionPoses.RemoveMatchingPredockPose(snapshot,list))inPosition=false;
                poses=list;return result;
            });
        }
        runner.Unavailable=error=>Unavailable(runner.Tag,error);
        _parent.StartActing(runner,Respond);
    }
    internal void Respond(RobotCompletedAction completed)
    {
        uint result=completed.Result;
        switch(result)
        {
            case 0:StoreStatus(BehaviorStatus.Complete);return;
            case 0x02000000:return;
            case 0x02000001:case 0x03000004:StoreStatus(BehaviorStatus.Failure);return;
            case 0x03000010:
                if(!Force)_m.Whiteboard.NoPreDockPosesObjectId=Target;StoreStatus(BehaviorStatus.Failure);return;
            case 0x03000013:
                if(ActionType==PreActionType.Rolling&&_specialRetry){_specialRetry=false;_useApproach=false;Drive();}return; //005B5C94..5EFF type4 retries once only while104 set.
            case 0x0300001D:
                if(_m.World.GetLocatedObjectByIdHelper(Target) is null){StoreStatus(BehaviorStatus.Failure);return;}
                var search=Factory.CreateNativeSearchForBlock(_m,_parent,Target,1);SearchHelper=(NativeSearchForBlockHelper)search.Pointer;
                Delegation.Delegate=search;
                Delegation.OnSuccessHandler=_=>{Drive();return Status;};
                Delegation.OnFailureHandler=_=>BehaviorStatus.Failure;
                return;
        }
        if((result>>24)==4){Drive();return;}
        StoreStatus(BehaviorStatus.Failure);
    }
    private void Unavailable(uint tag,NativeActionUnavailable error)
    {
        SteppedBehavior.ReportMissing(error.ToString());
        _m.Robot.Engine.Robot!.ActionList.DeferHostCleanup(()=>
        {
            if(_parent.CurrentRunnerTag!=tag)return;
            var manager=_m.Robot.Engine.Robot!.BehaviorManagerOwner;
            if(ReferenceEquals(manager?.Current,_parent))manager.Stop(BehaviorStopReason.Interrupted,_m.Robot.Engine.Timer.SecondsF);
            else _parent.Stop(BehaviorStopReason.Interrupted);
            _m.Robot.Engine.Robot.ActionList.Cancel(tag);
        });
    }
    public void Dispose(){lock(_snapshotGate){_snapshot?.Dispose();_snapshot=null;}}
}
