using Cozmo.Robot.Vision;
using Cozmo.Transport;
namespace Cozmo.Robot.Manipulation;

// M12-030 00559DC6..0055A006: derived Init/Check bypass generic drive overrides.
internal sealed class NativeDriveToPlaceCarriedBeaconRunner:NativeDriveToObjectRunner
{
    private readonly ManipulationSystem _m;
    private readonly Pose3d _placement;
    private readonly EngineRobot _owner;
    protected override string ActionName=>"DriveToPlaceCarriedObject";
    internal NativeDriveToPlaceCarriedBeaconRunner(ManipulationSystem m,EngineRobot owner,Pose3d placement,Func<bool>? current=null)
        :base(m,owner,owner.CarryingOwner?.CarriedObjectId??uint.MaxValue,PreActionType.PlaceOnGround,current)
    {_m=m;_owner=owner;_placement=new Pose3d(placement);Type=0xD;}
    public override int Init()
    {
        using var mutation=_m.World.EnterPoseMutation();
        if(!ExecutionCurrent())return unchecked((int)EngineActionResult.Cancelled);
        uint id=_owner.CarryingOwner?.CarriedObjectId??uint.MaxValue;
        if(id==uint.MaxValue)
        {
            _m.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"DriveToPlaceCarriedObjectAction.CheckPreconditions.NotCarryingObject",$"Robot {unchecked((int)_owner.NativeRobotId)} cannot place an object because it is not carrying anything.");
            return 0x03000011;
        }
        Action.ObjectId=id;
        var original=_m.World.GetLocatedObjectByIdHelper(id);
        if(original is null||(!CubeGeometry.IsCube(original.Type) && original.Type!=Cozmo.Protocol.ObjectType.Charger_Basic))
        {
            _m.Robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"DriveToPlaceCarriedObjectAction.CheckPreconditions.NoObjectWithID",$"Robot {unchecked((int)_owner.NativeRobotId)}'s block world does not have an ActionableObject with ID={unchecked((int)id)}.");
            EngineErrorState.StoreAndMaybeBreak();return 0x03000004;
        }
        var clone=original.CloneNative();
        clone.InitPose(_placement,PoseState.Known);
        try{return InitHelper(clone);}finally{clone.NativePose.Dispose();}
    }
    public override uint CheckIfDone()
    {
        if(!ExecutionCurrent())return EngineActionResult.Cancelled;
        uint result=Sequence.Update();
        if(!ExecutionCurrent())return EngineActionResult.Cancelled;
        using var mutation=_m.World.EnterPoseMutation();
        uint carriedId=_owner.CarryingOwner?.CarriedObjectId??uint.MaxValue;
        var carried=_m.World.GetLocatedObjectByIdHelper(carriedId);
        if(carried is null)return result;
        if(!carried.TryObservableBoundingQuad50(_placement,0f,out var reference))
            throw new NativeActionUnavailable("M12-030/M13-007 actual carried placement footprint unavailable");
        var filter=new BlockWorldFilter();filter.IgnoreIds.Add(carriedId);
        bool blocked=false;
        foreach(var other in _m.World.FindLocatedMatchingObjects(filter))
        {
            if(!other.TryObservableBoundingQuad50(other.NativePose,10f,out var quad))
                throw new NativeActionUnavailable("M12-030/M13-007 actual obstacle footprint unavailable");
            if(!ExecutionCurrent())return EngineActionResult.Cancelled;
            if(quad!.Intersects(reference!))blocked=true;
        }
        if(blocked){_m.Robot.Engine.EmitSourceLog(EngineLogLevel.Info,null,"DriveToPlaceCarriedObjectAction.CheckIfDone.GoalNotFree","");return 0x04000008;}
        return result;
    }
    internal override bool RetireNeverAdmittedTree(){if(!base.RetireNeverAdmittedTree())return false;_placement.Dispose();return true;}
    public override void WatcherEnding(){_placement.Dispose();base.WatcherEnding();}
}

// 00554B0A..BAC: two real children, proxy the Ground leaf.
internal sealed class NativePlaceGroundAtBeaconRunner:CompoundActionSequential
{
    internal NativeDriveToPlaceCarriedBeaconRunner Drive {get;}=null!;
    internal NativePlaceGroundRunner Ground {get;}=null!;
    internal Action<NativeActionUnavailable>? Unavailable;
    private bool _hostUnavailable;
    protected override string ActionName=>"PlaceObjectOnGroundAtPose";
    public override string? NativeName=>ActionName;
    public override uint Update()
    {
        if(_hostUnavailable)return EngineActionResult.Running; // guarded owner cleanup is pending
        int depth=Watcher?.HostUpdateDepth??0;
        try{return base.Update();}
        catch(NativeActionUnavailable error)
        {
            Watcher?.UnwindUnavailableHostUpdate(depth);
            _hostUnavailable=true;Unavailable?.Invoke(error);
            return EngineActionResult.Running; // no fabricated native completion
        }
    }    internal NativePlaceGroundAtBeaconRunner(ManipulationSystem m,Pose3d pose,Func<bool>? current=null):this(m,m.Robot.Engine.Robot ?? throw new NativeActionUnavailable("Ground-at-pose Robot owner unavailable"),pose,current){}
    internal NativePlaceGroundAtBeaconRunner(ManipulationSystem m,EngineRobot owner,Pose3d pose,Func<bool>? current=null):base(()=>m.Robot.Engine.Timer.SecondsF)
    {
        var parent=current;current=()=>!owner.Lifetime.Destroying && ReferenceEquals(m.Robot.Engine.Robot,owner) && (parent?.Invoke()??true);SourceLog=(l,c,k,t)=>m.Robot.Engine.EmitSourceLog(l,c,k,t);SeedNativeEmptyCompletion();
        if(!current())return;
        Drive=new(m,owner,pose,current);
        if(!current()){Drive.RetireNeverAdmittedTree();return;}
        if(!AddActionGuarded(Drive,false,current)){Drive.RetireNeverAdmittedTree();return;}
        if(!current())return;
        try { Ground=new(m,owner){ScopedParentCurrent=current}; }
        catch { RetireNeverAdmittedTree(); throw; }
        if(!current()){Ground.RetireNeverAdmittedTree();return;}
        if(!AddActionGuarded(Ground,false,current)){Ground.RetireNeverAdmittedTree();return;}
        if(!current())return;
        SetNativeCompletionProxy(Ground.Tag);
    }
}
