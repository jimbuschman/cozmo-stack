using Cozmo.Robot.Vision;
using Cozmo.Robot.Behavior;
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
    private bool _unavailable, _ending;
    private ScopedAcquisition? _reactionLease;
    private static readonly ReactionLockTable FlipReactions = new("FlipBlock", "0x00C56AE0", new[] { 1, 20 });
    private bool Live() => ScopedCurrent && !_ending && !DestructionObserved;
    protected override string ActionName=>"FlipBlock";
    public override string? NativeName=>ActionName;
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
        if (!Live()) return unchecked((int)EngineActionResult.Cancelled);
        Sequence.ClearActions();
        using var mutation=M.World.EnterPoseMutation();
        var obj=M.World.GetLocatedObjectByIdHelper(Target);
        if(obj is null||!CubeGeometry.IsCube(obj.Type))return 0x03000004;
        var owner=ScopedOwner??M.Robot.Engine.Robot!;var main=owner.GetCheckedMainPose();
        var pre=DockPreActionPoses.Get(new(obj,PreActionType.Flipping,CheckPreActionPose,FlipBlockAction.InitAngleToleranceRad,0,false,0),main,owner.CarryingOwner!.CarriedObjectId,()=>M.NativeObstacles(main,ScopedOwner));
        if (!Live()) return unchecked((int)EngineActionResult.Cancelled);
        if(pre.Result!=ActionResult.Success)return unchecked((int)pre.Result);
        using var relative=new Pose3d("");obj.NativePose.TryWithRespectTo(main,relative); // native return ignored.
        Sequence.SuppressTrackLocking=true;
        if (ScopedOwner is null) owner.BehaviorManagerOwner?.DisableReactionsWithLock(ActionName, FlipReactions, true);
        else if (owner.BehaviorManagerOwner is { } manager)
        {
            var reactions = _reactionLease = new ScopedAcquisition();
            if (!manager.DisableScopedReactions(ActionName, FlipReactions, reactions) || !Live())
                return unchecked((int)EngineActionResult.Cancelled);
            reactions.Completed = true;
        }
        float distance=FlipBlockAction.Norm3(relative.Translation)+20f;
        // Construction is DRIVE then LIFT; execution is LIFT then DRIVE.
        var drive=ScopedOwner is { } scoped ? new DriveStraightAction(M,scoped,distance,150,true) : new DriveStraightAction(M,distance,150,true);
        var lift=M.Robot.Motion.CreateNativeLiftChild(45,5,ScopedOwner, ScopedOwner is null ? null : Live);
        Sequence.AddAction(lift,false,false);Sequence.AddAction(drive,false,false);
        Sequence.Update();return 0; // Init deliberately ignores immediate embedded result.
    }
    public override uint CheckIfDone()
    {
        if (!Live()) return EngineActionResult.Cancelled;
        var owner=ScopedOwner??M.World.NativeSnapshotOwner?.Invoke();
        uint result=Sequence.Update();
        using var mutation=M.World.EnterPoseMutation();
        bool Current() => Live() && ReferenceEquals(M.World.NativeSnapshotOwner?.Invoke(),owner) &&
            (owner is null || (!owner.Lifetime.Destroying && ReferenceEquals(owner.Engine.Robot,owner)));
        if(!Current())return EngineActionResult.Cancelled;
        var obj=M.World.GetLocatedObjectByIdHelper(Target);
        if(result!=EngineActionResult.Running)
        {
            if(obj is not null)M.World.MarkObjectUnknown(obj,true,owner,Current);
            else M.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,"Actions","FlipBlockAction.CheckIfDone.NullObject","Object no longer located");
            return result;
        }
        if(obj is null)return 0x03000004;
        using var relative=new Pose3d("");obj.NativePose.TryWithRespectTo((ScopedOwner??M.Robot.Engine.Robot)!.GetCheckedMainPose(),relative);
        if(FlipBlockAction.Norm3(relative.Translation)<40f&&QueuedLiftTag==uint.MaxValue)
        {
            var lift=M.Robot.Motion.CreateNativeLiftChild(LiftPresets.CarryMm,5,ScopedOwner, ScopedOwner is null ? null : Live);
            lift.SuppressTrackLocking=true;
            QueuedLiftTag=lift.Tag;
            if (ScopedOwner is null) M.Robot.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.InParallel,lift,0);
            else if (ScopedOwner.ActionList.QueueActionGuardedInParallel(lift, 0, Live) != 0)
            {
                if (!lift.DestructionObserved && lift.CanDetachWatcherForRetirement()) lift.RetireNeverAdmittedTree();
                if (Live()) QueuedLiftTag = uint.MaxValue;
            }
        }
        return EngineActionResult.Running;
    }
    internal override bool CanDetachWatcherForRetirement() =>
        base.CanDetachWatcherForRetirement() && Sequence.CanDetachWatcherForRetirement();

    internal override bool CanRetireNeverAdmittedTree() =>
        base.CanRetireNeverAdmittedTree() && Sequence.CanRetireNeverAdmittedTree();

    internal override bool RetireNeverAdmittedTree() =>
        CanRetireNeverAdmittedTree() && Sequence.RetireNeverAdmittedTree() && base.RetireNeverAdmittedTree();

    public override void WatcherEnding()
    {
        if (_ending || DestructionObserved) return;
        _ending = true;
        Sequence.Prep();
        var owner = ScopedOwner ?? M.Robot.Engine.Robot;
        if (QueuedLiftTag != uint.MaxValue) owner?.ActionList.Cancel(QueuedLiftTag);
        if (_reactionLease is { } reactions)
        {
            _reactionLease = null;
            reactions.Cancel();
            owner?.BehaviorManagerOwner?.RemoveScopedReactions(ActionName, reactions);
        }
        else if (ScopedOwner is null) owner?.BehaviorManagerOwner?.RemoveDisableReactionsLock(ActionName);
        Sequence.WatcherEnding();base.WatcherEnding();
    }
}
