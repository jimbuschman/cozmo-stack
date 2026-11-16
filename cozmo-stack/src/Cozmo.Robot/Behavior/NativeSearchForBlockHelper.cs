using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M12-010
// 0x005BAC7C..0x005BB830: concrete search, including untargeted stages 0 and 1.
internal sealed class NativeSearchForBlockHelper:IHelper,IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly SteppedBehavior _parent;
    private readonly Func<bool>? _hostCurrent;
    private bool Current()=>_hostCurrent is null||_hostCurrent();
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
    internal CompoundActionSequential? Sequence {get;private set;}
    internal int MaxStage {get;}
    internal uint MinSeen {get;}
    private bool HasTarget => Target != uint.MaxValue;
    internal NativeSearchForBlockHelper(ManipulationSystem m,SteppedBehavior parent,BehaviorHelperFactory factory,uint target,int maxStage=0,Func<bool>? hostCurrent=null,uint minSeen=0)
        :base(m.Robot,"SearchForBlock",parent,factory)
    {
        _m=m;_parent=parent;_hostCurrent=hostCurrent;HostCurrent=hostCurrent;Target=target;MaxStage=maxStage;MinSeen=minSeen;
        if(m.Robot.Engine.Robot?.HasExternalInterface==true)
            _observed=m.Robot.Engine.AnimationMessages.SubscribeMessage(0x44,true,OnObserved);
    }
    public override bool ShouldCancelDelegates(CozmoRobot robot)=>false;
    protected override BehaviorStatus Init(CozmoRobot robot)
    {
        if(!Current())return Status;
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
        if(!Current())return;
        lock(_stateGate){if(_hostUnavailable||_retired)return;}
        var target=HasTarget?_m.World.GetLocatedObjectByIdHelper(Target):null; // 0x005BAF3E..0x005BAF56
        if(!Current())return;
        bool missing=HasTarget&&target is null;
        bool finish;
        lock(_stateGate)
        {
            if(_hostUnavailable||_retired)return;
            finish=missing||Stage>MaxStage;
        }
        if(finish)
        {
            // Checked SH7/A21 (5BB480..500): default filter intentionally invalidates the in-memory located world.
            if(target?.PoseState==PoseState.Known&&MaxStage>=1)_m.World.DeleteLocatedObjects(new BlockWorldFilter());
            if(!Current())return;
            // 0x005BB5BA..0x005BB5D0: a specific search fails; an untargeted search checks the count.
            lock(_stateGate)
            {
                if(!_hostUnavailable&&!_retired)
                    StoreStatus(!HasTarget && (uint)_seen.Count >= MinSeen ? BehaviorStatus.Complete : BehaviorStatus.Failure);
            }
            return;
        }
        NativeNearbySearchRunner NewNearby()=>new(_m,Target,-20f,100f,BitConverter.UInt32BitsToSingle(0xBDB2B8C2));
        NativeTurnInPlaceRunner Turn(float angle)=>new(_m,new TurnInPlaceSpec(angle,false,TurnInPlaceAction.MinToleranceRad,true,false,0,0,false));
        var sequence=new SearchSequence(this);
        if(!Current()){sequence.RetireNeverAdmittedTree();return;}
        Sequence=sequence;
        sequence.SeedNativeEmptyCompletion();
        bool Add(ActionRunner child,bool ignored)
        {
            if(_hostCurrent is null){sequence.AddAction(child,ignored,false);return true;}
            if(Current()&&sequence.AddActionGuarded(child,ignored,Current))return true;
            child.RetireNeverAdmittedTree();sequence.RetireNeverAdmittedTree();return false;
        }
        if(Stage==0)
        {
            var nearby=NewNearby();
            if(!Current()){nearby.RetireNeverAdmittedTree();sequence.RetireNeverAdmittedTree();return;}
            Nearby=nearby;
            // 0x005BAFD4..0x005BB03E: the initial turn requires a target. Nearby always runs.
            if(HasTarget&&!Add(new NativeTurnTowardsObjectRunner(_m,Target,NativeTurnTowardsObjectRunner.AnyCode,Radians.Pi,false,false),true)){nearby.RetireNeverAdmittedTree();return;}
            if(!Add(nearby,false))return;
        }
        else
        {
            if(!Add(new DriveStraightAction(_m,-20f,20f,false),true))return;
            if(!Add(Turn(Radians.Pi/4f),true))return;
            if(!Add(Turn(Radians.Pi/4f),true))return;
            if(HasTarget&&!Add(NewNearby(),true))return; // 0x005BB266..0x005BB26C
            if(!Add(new DriveStraightAction(_m,-20f,20f,false),true))return;
            if(!Add(Turn(-3f*Radians.Pi/4f),true))return;
            if(!Add(Turn(-Radians.Pi/4f),true))return;
            if(HasTarget&&!Add(NewNearby(),true))return; // 0x005BB364..0x005BB36A
        }
        BeforeActionPublicationForTest?.Invoke();
        if(!Current()){sequence.RetireNeverAdmittedTree();return;}
        bool started=_parent.StartActing(sequence,_=>Search(),_hostCurrent);
        if(!Current())return;
        bool unavailable;
        lock(_stateGate){if(!_retired)Stage++;unavailable=_retired||_hostUnavailable;}
        if(started&&unavailable)ScheduleCleanup(sequence.Tag); //StartActing BEFORE stage publication.
    }
    protected override BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot)
    {
        if(!Current())return Status;
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
        if(!Current())return;
        if(payload is not NativeObservedObject value)return;
        lock(_stateGate)
        {
            if(_retired||_hostUnavailable)return;
            // 0x005BB81C..0x005BB82C reads the current distinct count. The insert at
            // 0x005BB816 belongs to the specific-target branch.
            if(!HasTarget)
            {
                if(MinSeen!=0&&(uint)_seen.Count>=MinSeen)StoreStatus(BehaviorStatus.Complete);
                return;
            }
            if(value.ObjectId==Target){StoreStatus(BehaviorStatus.Complete);return;}
            if(_seen.Contains(value.ObjectId))return;
        }
        bool missing;
        using(var mutation=_m.World.EnterPoseMutation())missing=_m.World.GetLocatedObjectByIdHelper(Target) is null;
        if(!Current())return;
        NativeActionUnavailable error;
        lock(_stateGate)
        {
            if(_retired||_hostUnavailable||_seen.Contains(value.ObjectId))return;
            if(missing){StoreStatus(BehaviorStatus.Failure);_seen.Add(value.ObjectId);return;}
            _hostUnavailable=true;
            error=new NativeActionUnavailable("M11-042 SearchForBlock ShouldBeAbleToFindTarget005BB73C: exact INIT camera snapshot/frame/occlusion inputs unavailable (attempted="+SnapshotAttempted+", calibration="+(_snapshotCalibration is not null)+")");
        }
        SteppedBehavior.ReportMissing(error.ToString());
        if(!Current())return;
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
            if(!Current()||capturedTag==0||_parent.CurrentRunnerTag!=capturedTag)return;
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
