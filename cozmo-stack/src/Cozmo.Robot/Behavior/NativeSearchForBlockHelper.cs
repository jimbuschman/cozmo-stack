using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M12-010
//5BAC7C..5BB830: admitted specific-target minSeen0/maxStage0 helper only.
internal sealed class NativeSearchForBlockHelper:IHelper,IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly SteppedBehavior _parent;
    private readonly HashSet<uint> _seen=new();
    private readonly object _stateGate=new();
    private RetainedSubscription? _observed;
    private CameraCalibration? _snapshotCalibration;
    private bool _hostUnavailable,_retired;
    internal Action? BeforeActionPublicationForTest; // Effect-free observer/barrier; never replaces execution.
    internal bool Retired{get{lock(_stateGate)return _retired;}}
    internal uint Target {get;}
    internal int Stage {get;private set;}
    internal bool SnapshotAttempted {get;private set;}
    internal NativeNearbySearchRunner? Nearby {get;private set;}
    internal NativeSearchForBlockHelper(ManipulationSystem m,SteppedBehavior parent,BehaviorHelperFactory factory,uint target)
        :base(m.Robot,"SearchForBlock",parent,factory)
    {
        _m=m;_parent=parent;Target=target;
        if(m.Robot.Engine.Robot?.HasExternalInterface==true)
            _observed=m.Robot.Engine.AnimationMessages.SubscribeMessage(0x44,true,OnObserved);
    }
    public override bool ShouldCancelDelegates(CozmoRobot robot)=>false;
    protected override BehaviorStatus Init(CozmoRobot robot)
    {
        var calibration=_m.Vision.Calibration;
        lock(_stateGate)
        {
            if(_retired)return Status;
            Stage=0;_seen.Clear();SnapshotAttempted=true;_snapshotCalibration=calibration;
        }
        //The actual INIT snapshot frame/pose/FOV+occlusion input is not recovered. It is consumed only at S7.
        Search();
        return Status;
    }
    private void Search()
    {
        lock(_stateGate){if(_hostUnavailable||_retired)return;}
        bool missing=_m.World.GetLocatedObjectByIdHelper(Target) is null;
        lock(_stateGate)
        {
            if(_hostUnavailable||_retired)return;
            if(missing||Stage>0){StoreStatus(BehaviorStatus.Failure);return;} //maxStage0 specific target normal finish5BB478.
        }
        Nearby=new(_m,Target,-20f,100f,BitConverter.UInt32BitsToSingle(0xBDB2B8C2));
        var sequence=new SearchSequence(this);
        sequence.SeedNativeEmptyCompletion();
        sequence.AddAction(new NativeTurnTowardsObjectRunner(_m,Target,NativeTurnTowardsObjectRunner.AnyCode,Radians.Pi,false,false),true,false);
        sequence.AddAction(Nearby,false,false);
        BeforeActionPublicationForTest?.Invoke();
        bool started=_parent.StartActing(sequence,_=>Search());
        bool unavailable;
        lock(_stateGate){if(!_retired)Stage=1;unavailable=_retired||_hostUnavailable;}
        if(started&&unavailable)ScheduleCleanup(sequence.Tag); //StartActing BEFORE stage publication.
    }
    protected override BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot)
    {
        bool stop;
        lock(_stateGate){stop=!_retired&&!_hostUnavailable&&Status!=BehaviorStatus.Running;}
        if(stop)
        {
            StopActing(false); // Common native route cancels the captured owned ActionList tag before callback/retry.
        }
        return Status;
    }
    // All native status changes are explicit writes under _stateGate. Do not overwrite a concurrent44
    // with the stale value returned just before IHelper's normal commit point.
    protected override void CommitReturnedStatus(BehaviorStatus value) { }
    private void OnObserved(object? payload)
    {
        if(payload is not NativeObservedObject value)return;
        lock(_stateGate)
        {
            if(_retired||_hostUnavailable)return;
            if(value.ObjectId==Target){StoreStatus(BehaviorStatus.Complete);return;}
            if(_seen.Contains(value.ObjectId))return;
        }
        bool missing;
        using(var mutation=_m.World.EnterPoseMutation())missing=_m.World.GetLocatedObjectByIdHelper(Target) is null;
        NativeActionUnavailable error;
        lock(_stateGate)
        {
            if(_retired||_hostUnavailable||_seen.Contains(value.ObjectId))return;
            if(missing){StoreStatus(BehaviorStatus.Failure);_seen.Add(value.ObjectId);return;}
            _hostUnavailable=true;
            error=new NativeActionUnavailable("M11-042 SearchForBlock ShouldBeAbleToFindTarget005BB73C: exact INIT camera snapshot/frame/occlusion inputs unavailable (attempted="+SnapshotAttempted+", calibration="+(_snapshotCalibration is not null)+")");
        }
        SteppedBehavior.ReportMissing(error.ToString());
        ScheduleCleanup(_parent.CurrentRunnerTag);
    }
    private bool Unavailable{get{lock(_stateGate)return _retired||_hostUnavailable;}}
    private sealed class SearchSequence(NativeSearchForBlockHelper helper):CompoundActionSequential(()=>helper._m.Robot.Engine.Timer.SecondsF)
    {
        public override uint Update()=>helper.Unavailable&&(State==EngineActionResult.NotStarted||State==EngineActionResult.Running)?EngineActionResult.Running:base.Update(); //Host teardown pending; never a native result or child motion.
    }
    private void ScheduleCleanup(uint capturedTag)
    {
        var owner=_m.Robot.Engine.Robot;
        owner?.ActionList.DeferHostCleanup(()=>
        {
            if(capturedTag==0||_parent.CurrentRunnerTag!=capturedTag)return;
            var manager=owner.BehaviorManagerOwner;
            if(ReferenceEquals(manager?.Current,_parent))manager.Stop(BehaviorStopReason.Interrupted,_m.Robot.Engine.Timer.SecondsF);
            else _parent.Stop(BehaviorStopReason.Interrupted);
            if(capturedTag!=0)owner.ActionList.Cancel(capturedTag);
        });
    }
    public void Dispose()
    {
        RetainedSubscription? observed;
        lock(_stateGate)
        {
            if(_retired)return;_retired=true;_seen.Clear();_snapshotCalibration=null;
            observed=_observed;_observed=null;
        }
        observed?.Dispose();
    }
}
