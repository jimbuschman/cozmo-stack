using Cozmo.Robot.Vision;
using Cozmo.Transport;
namespace Cozmo.Robot.Manipulation;

// fidelity: M12-029, M12-022, M12-011
// 55850C/5592C6/559650/559880: ordinary type0 native action; public type6 remains separate.
internal class NativeDriveToObjectRunner:NativeVisionRunner
{
    internal readonly DriveToObjectAction Action;
    private readonly CompoundActionSequential _sequence;
    private bool _light;
    private bool _hostUnavailable;
    private Func<bool>? _parentCurrent;
    internal Action<NativeActionUnavailable>? Unavailable;
    internal DriveToPoseAction? Drive {get;private set;}
    internal Action<IReadOnlyList<ActionRunner>>? BeforeInitialUpdateForTest;
    protected override string ActionName=>"DriveToObject";
    internal CompoundActionSequential Sequence=>_sequence;
    internal NativeDriveToObjectRunner(ManipulationSystem m,uint id):this(m,id,PreActionType.Docking){}
    internal NativeDriveToObjectRunner(ManipulationSystem m,EngineRobot owner,uint id,PreActionType type,Func<bool>? parentCurrent=null):this(m,id,type)
    {ScopedOwner=owner;Action.NativeOwner=owner;_parentCurrent=parentCurrent;}
    internal NativeDriveToObjectRunner(ManipulationSystem m,uint id,PreActionType type):base(m,0x0A,4)
    {
        Action=new(m,id,type){NativeInputs=true};
        _sequence=new(()=>m.Robot.Engine.Timer.SecondsF){SuppressTrackLocking=true,SourceLog=SourceLog};
        _sequence.SeedNativeEmptyCompletion();
    }
    internal override ActionWatcher? Watcher{get=>base.Watcher;set{base.Watcher=value;_sequence.Watcher=value;}}
    internal bool SetPosesFunction(PosesFunction function)
    {
        // G1: exactly RUNNING rejects assignment; every other state accepts it.
        if(State==EngineActionResult.Running)
        {
            M.Robot.Engine.EmitSourceLog(EngineLogLevel.Error,"Actions","DriveToActions.SetGetPossiblePosesFunc.TriedToSetWhileRunning","Cannot replace pose function while running");
            EngineErrorState.StoreAndMaybeBreak();return false;
        }
        Action.PosesFunction=function;return true;
    }
    public override uint Update()
    {
        if(!ExecutionCurrent())return EngineActionResult.Cancelled;
        if(_hostUnavailable&&(State==EngineActionResult.NotStarted||State==EngineActionResult.Running))return EngineActionResult.Running;
        int depth=Watcher?.HostUpdateDepth??0;
        try{return base.Update();}
        catch(NativeActionUnavailable error)
        {Watcher?.UnwindUnavailableHostUpdate(depth);_hostUnavailable=true;Unavailable?.Invoke(error);return EngineActionResult.Running;}
    }
    public override int Init()
    {
        if(!ExecutionCurrent())return unchecked((int)EngineActionResult.Cancelled);
        using var mutation=M.World.EnterPoseMutation();
        var obj=M.World.GetLocatedObjectByIdHelper(Action.ObjectId);
        if(obj is null||!CubeGeometry.IsCube(obj.Type))return 0x03000004;
        int result=InitHelper(obj);
        if(!ExecutionCurrent())return unchecked((int)EngineActionResult.Cancelled);
        if(CozmoCubes.IsLightCube(obj.Type)&&!_light)
        {
            if(ScopedOwner is null)M.Robot.Lights.Cubes.PlayNativeDrivingTo(Action.ObjectId);
            else M.Robot.Lights.Cubes.PlayNativeDrivingTo(Action.ObjectId,ExecutionCurrent);
            if(ExecutionCurrent())_light=true;
        }
        return result;
    }
    protected int InitHelper(ObservableObject obj)
    {
        if(!ExecutionCurrent())return unchecked((int)EngineActionResult.Cancelled);
        if((ScopedOwner??M.Robot.Engine.Robot)?.CarryingOwner is null)throw new NativeActionUnavailable("M12-022 actual carrying owner unavailable");
        _sequence.ClearActions();Drive=null;
        bool inPosition=false;
        var result=Action.PosesFunction(obj,out var poses,ref inPosition);
        if(!ExecutionCurrent())return unchecked((int)EngineActionResult.Cancelled);
        if(result!=ActionResult.Success)return unchecked((int)result);
        if(!inPosition)
        {
            Drive=ScopedOwner is {} owner ? new DriveToPoseAction(M,true,false,owner,ExecutionCurrent) : new DriveToPoseAction(M,true,false);
            Drive.SuppressTrackLocking=true;Drive.PreActionObjectPose=new Pose3d(obj.NativePose);
            Drive.SetNativeGoals(poses.Select(p=>p.WorldPose).ToArray());
            if(ScopedOwner is null)_sequence.AddAction(Drive,false,false);
            else if(!_sequence.AddActionGuarded(Drive,false,ExecutionCurrent))
            {
                if(Drive.CanDetachWatcherForRetirement())Drive.Watcher=null;
                if(Drive.CanRetireNeverAdmittedTree())Drive.RetireNeverAdmittedTree();
                else Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("M8-015 DriveToObject stale drive child could not retire");
                return unchecked((int)EngineActionResult.Cancelled);
            }
        }
        // 00559366..380: predicate receives supplied object's ID. IsCarryingObject
        // 00633F88 compares both stored slots as raw IDs, including empty -1.
        var carrying=(ScopedOwner??M.Robot.Engine.Robot)?.CarryingOwner ?? throw new NativeActionUnavailable("M12-022 actual carrying owner unavailable");
        uint bottom=carrying.CarriedObjectId??uint.MaxValue, top=carrying.CarriedOnTopId??uint.MaxValue;
        if(bottom!=obj.ObjectId&&top!=obj.ObjectId)
        {
            // 00559392: the turn takes the action's target, not the supplied geometry object's ID.
            var turn=new NativeTurnTowardsObjectRunner(M,Action.ObjectId,NativeTurnTowardsObjectRunner.AnyCode,0,true,false){SuppressTrackLocking=true,ScopedOwner=ScopedOwner,ScopedParentCurrent=ScopedOwner is null?null:ExecutionCurrent};
            if(ScopedOwner is null)_sequence.AddAction(turn,false,false);
            else if(!_sequence.AddActionGuarded(turn,false,ExecutionCurrent))
            {
                if(turn.CanDetachWatcherForRetirement())turn.Watcher=null;
                if(turn.CanRetireNeverAdmittedTree())turn.RetireNeverAdmittedTree();
                else Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("M8-015 DriveToObject stale turn child could not retire");
                return unchecked((int)EngineActionResult.Cancelled);
            }
        }
        BeforeInitialUpdateForTest?.Invoke(_sequence.Children);
        if(!ExecutionCurrent())return unchecked((int)EngineActionResult.Cancelled);
        uint started=_sequence.Update();
        if(!ExecutionCurrent())return unchecked((int)EngineActionResult.Cancelled);
        return started==0||started==EngineActionResult.Running?0:unchecked((int)started);
    }
    public override uint CheckIfDone()
    {
        if(!ExecutionCurrent())return EngineActionResult.Cancelled;
        uint result=_sequence.Update();if(!ExecutionCurrent())return EngineActionResult.Cancelled;if(result!=0)return result;
        if(!Action.CheckAtEnd)return 0;
        using var mutation=M.World.EnterPoseMutation();
        var obj=M.World.GetLocatedObjectByIdHelper(Action.ObjectId);
        if(obj is null)return 0x03000004;
        bool inPosition=false;var checkedResult=Action.PosesFunction(obj,out _,ref inPosition);
        if(!ExecutionCurrent())return EngineActionResult.Cancelled;
        return inPosition?(uint)checkedResult:0x04000001;
    }
    public override byte? GetCompletionVariant()=>0;
    public override ObjectInteractionCompletion GetObjectInteractionCompletion()=>new(Action.ObjectId,uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,1,false);
    internal override bool CanDetachWatcherForRetirement()=>base.CanDetachWatcherForRetirement()&&_sequence.CanDetachWatcherForRetirement();
    internal override bool CanRetireNeverAdmittedTree()=>base.CanRetireNeverAdmittedTree()&&_sequence.CanRetireNeverAdmittedTree();
    internal override bool RetireNeverAdmittedTree()
    {
        if(!CanRetireNeverAdmittedTree())return false;
        if(!_sequence.RetireNeverAdmittedTree())return false;
        return base.RetireNeverAdmittedTree();
    }    public override void WatcherEnding()
    {
        if(_light)
        {
            if(ScopedOwner is null)M.Robot.Lights.Cubes.StopNativeDrivingTo(Action.ObjectId);
            else
            {
                long generation=M.Robot.Motion.Path.ScopeGeneration;
                M.Robot.Lights.Cubes.StopNativeDrivingTo(Action.ObjectId,()=>ScopedCurrent&&M.Robot.Motion.Path.ScopeGeneration==generation);
            }
            _light=false;
        }
        _sequence.Prep();
        Action.PosesFunction=Action.GetPossiblePoses; // release helper capture after preparing the children.
        _sequence.WatcherEnding();base.WatcherEnding();
    }
    private bool ExecutionCurrent()=>ScopedOwner is null || ScopedCurrent&&!DestructionObserved&&(_parentCurrent?.Invoke()??true);
}
