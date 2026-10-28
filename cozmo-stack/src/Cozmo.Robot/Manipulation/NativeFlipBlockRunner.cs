using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// fidelity: M13-028, M13-002
// K20-K23: own IAction, with embedded sequence and a separately queued carry lift.
internal sealed class NativeFlipBlockRunner:NativeVisionRunner
{
    internal uint Target {get;}
    internal bool CheckPreActionPose {get;set;}=true;
    internal uint QueuedLiftTag {get;private set;}=uint.MaxValue;
    internal CompoundActionSequential Sequence {get;}
    internal Action<NativeActionUnavailable>? Unavailable;
    private bool _unavailable;
    protected override string ActionName=>"FlipBlock";
    internal NativeFlipBlockRunner(ManipulationSystem m,uint id):base(m,0x0F,6)
    {Target=id;Sequence=new(()=>m.Robot.Engine.Timer.SecondsF){SourceLog=SourceLog};Sequence.SeedNativeEmptyCompletion();}
    internal override ActionWatcher? Watcher{get=>base.Watcher;set{base.Watcher=value;Sequence.Watcher=value;}}
    public override byte? GetCompletionVariant()=>6;
    public override uint Update()
    {
        if(_unavailable)return EngineActionResult.Running;
        int depth=Watcher?.HostUpdateDepth??0;
        try{return base.Update();}
        catch(NativeActionUnavailable error){Watcher?.UnwindUnavailableHostUpdate(depth);_unavailable=true;Unavailable?.Invoke(error);return EngineActionResult.Running;}
    }
    public override int Init()
    {
        Sequence.ClearActions();
        using var mutation=M.World.EnterPoseMutation();
        var obj=M.World.GetLocatedObjectByIdHelper(Target);
        if(obj is null||!CubeGeometry.IsCube(obj.Type))return 0x03000004;
        var owner=M.Robot.Engine.Robot!;var main=owner.GetCheckedMainPose();
        var pre=DockPreActionPoses.Get(new(obj,PreActionType.Flipping,CheckPreActionPose,FlipBlockAction.InitAngleToleranceRad,0,false,0),main,owner.CarryingOwner!.CarriedObjectId,()=>M.NativeObstacles(main));
        if(pre.Result!=ActionResult.Success)return unchecked((int)pre.Result);
        using var relative=new Pose3d("");obj.NativePose.TryWithRespectTo(main,relative); // native return ignored.
        Sequence.SuppressTrackLocking=true;
        owner.BehaviorManagerOwner?.DisableReactionsWithLock(ActionName,Enumerable.Range(0,21).Select(i=>i is 1 or 20).ToArray(),true);
        float distance=FlipBlockAction.Norm3(relative.Translation)+20f;
        // Construction is DRIVE then LIFT; execution is LIFT then DRIVE.
        var drive=new DriveStraightAction(M,distance,150,true);
        var lift=M.Robot.Motion.CreateNativeLiftChild(45,5);
        Sequence.AddAction(lift,false,false);Sequence.AddAction(drive,false,false);
        Sequence.Update();return 0; // Init deliberately ignores immediate embedded result.
    }
    public override uint CheckIfDone()
    {
        uint result=Sequence.Update();
        using var mutation=M.World.EnterPoseMutation();
        var obj=M.World.GetLocatedObjectByIdHelper(Target);
        if(result!=EngineActionResult.Running)
        {
            if(obj is not null)M.World.MarkObjectUnknown(obj,true);
            else M.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,"Actions","FlipBlockAction.CheckIfDone.NullObject","Object no longer located");
            return result;
        }
        if(obj is null)return 0x03000004;
        using var relative=new Pose3d("");obj.NativePose.TryWithRespectTo(M.Robot.Engine.Robot!.GetCheckedMainPose(),relative);
        if(FlipBlockAction.Norm3(relative.Translation)<40f&&QueuedLiftTag==uint.MaxValue)
        {
            var lift=M.Robot.Motion.CreateNativeLiftChild(LiftPresets.CarryMm,5);lift.SuppressTrackLocking=true;
            QueuedLiftTag=lift.Tag;
            M.Robot.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.InParallel,lift,0);
        }
        return EngineActionResult.Running;
    }
    public override void WatcherEnding()
    {
        Sequence.Prep();
        if(QueuedLiftTag!=uint.MaxValue)M.Robot.Engine.Robot?.ActionList.Cancel(QueuedLiftTag);
        M.Robot.Engine.Robot?.BehaviorManagerOwner?.RemoveDisableReactionsLock(ActionName);
        Sequence.WatcherEnding();base.WatcherEnding();
    }
}
