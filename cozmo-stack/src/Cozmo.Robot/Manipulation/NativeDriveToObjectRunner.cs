using Cozmo.Robot.Vision;
using Cozmo.Transport;
namespace Cozmo.Robot.Manipulation;

// fidelity: M12-029, M12-022, M12-011
// 55850C/5592C6/559650/559880: ordinary type0 native action; public type6 remains separate.
internal sealed class NativeDriveToObjectRunner:NativeVisionRunner
{
    internal readonly DriveToObjectAction Action;
    private readonly CompoundActionSequential _sequence;
    private bool _light;
    private bool _hostUnavailable;
    internal Action<NativeActionUnavailable>? Unavailable;
    internal DriveToPoseAction? Drive {get;private set;}
    protected override string ActionName=>"DriveToObject";
    internal CompoundActionSequential Sequence=>_sequence;
    internal NativeDriveToObjectRunner(ManipulationSystem m,uint id):this(m,id,PreActionType.Docking){}
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
        if(_hostUnavailable&&(State==EngineActionResult.NotStarted||State==EngineActionResult.Running))return EngineActionResult.Running;
        int depth=Watcher?.HostUpdateDepth??0;
        try{return base.Update();}
        catch(NativeActionUnavailable error)
        {Watcher?.UnwindUnavailableHostUpdate(depth);_hostUnavailable=true;Unavailable?.Invoke(error);return EngineActionResult.Running;}
    }
    public override int Init()
    {
        using var mutation=M.World.EnterPoseMutation();
        var obj=M.World.GetLocatedObjectByIdHelper(Action.ObjectId);
        if(obj is null||!CubeGeometry.IsCube(obj.Type))return 0x03000004;
        int result=InitHelper(obj);
        if(CozmoCubes.IsLightCube(obj.Type)&&!_light){M.Robot.Lights.Cubes.PlayNativeDrivingTo(Action.ObjectId);_light=true;}
        return result;
    }
    private int InitHelper(ObservableObject obj)
    {
        _sequence.ClearActions();Drive=null;
        bool inPosition=false;
        var result=Action.PosesFunction(obj,out var poses,ref inPosition);
        if(result!=ActionResult.Success)return unchecked((int)result);
        if(!inPosition)
        {
            Drive=new DriveToPoseAction(M,true,false){SuppressTrackLocking=true,PreActionObjectPose=new Pose3d(obj.NativePose)};
            Drive.SetNativeGoals(poses.Select(p=>p.WorldPose).ToArray());
            _sequence.AddAction(Drive,false,false);
        }
        if(M.Robot.Engine.Robot!.CarryingOwner!.CarriedObjectId!=obj.ObjectId)
            _sequence.AddAction(new NativeTurnTowardsObjectRunner(M,obj.ObjectId,NativeTurnTowardsObjectRunner.AnyCode,0,true,false){SuppressTrackLocking=true},false,false);
        uint started=_sequence.Update();
        return started==0||started==EngineActionResult.Running?0:unchecked((int)started);
    }
    public override uint CheckIfDone()
    {
        uint result=_sequence.Update();if(result!=0)return result;
        if(!Action.CheckAtEnd)return 0;
        using var mutation=M.World.EnterPoseMutation();
        var obj=M.World.GetLocatedObjectByIdHelper(Action.ObjectId);
        if(obj is null)return 0x03000004;
        bool inPosition=false;var checkedResult=Action.PosesFunction(obj,out _,ref inPosition);
        return inPosition?(uint)checkedResult:0x04000001;
    }
    public override byte? GetCompletionVariant()=>0;
    public override ObjectInteractionCompletion GetObjectInteractionCompletion()=>new(Action.ObjectId,uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,1,false);
    public override void WatcherEnding()
    {
        if(_light){M.Robot.Lights.Cubes.StopNativeDrivingTo(Action.ObjectId);_light=false;}
        _sequence.Prep();
        Action.PosesFunction=Action.GetPossiblePoses; // release helper capture after preparing the children.
        _sequence.WatcherEnding();base.WatcherEnding();
    }
}
