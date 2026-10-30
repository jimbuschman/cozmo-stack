using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// M12-030 00559DC6..0055A006: derived Init/Check bypass generic drive overrides.
internal sealed class NativeDriveToPlaceCarriedBeaconRunner:NativeDriveToObjectRunner
{
    private readonly ManipulationSystem _m;
    private readonly Pose3d _placement;
    protected override string ActionName=>"DriveToPlaceCarriedObject";
    internal NativeDriveToPlaceCarriedBeaconRunner(ManipulationSystem m,Pose3d placement)
        :base(m,m.Robot.Engine.Robot!.CarryingOwner!.CarriedObjectId??uint.MaxValue,PreActionType.PlaceOnGround)
    {_m=m;_placement=new Pose3d(placement);Type=0xD;}
    public override int Init()
    {
        using var mutation=_m.World.EnterPoseMutation();
        uint id=_m.Robot.Engine.Robot!.CarryingOwner!.CarriedObjectId??uint.MaxValue;
        if(id==uint.MaxValue)return 0x03000011;
        Action.ObjectId=id;
        var original=_m.World.GetLocatedObjectByIdHelper(id);
        if(original is null||!CubeGeometry.IsCube(original.Type))return 0x03000004;
        var clone=original.CloneNative();
        clone.InitPose(_placement,PoseState.Known);
        try{return InitHelper(clone);}finally{clone.NativePose.Dispose();}
    }
    public override uint CheckIfDone()
    {
        uint result=Sequence.Update();
        using var mutation=_m.World.EnterPoseMutation();
        var carried=_m.World.GetLocatedObjectByIdHelper(Action.ObjectId);
        if(carried is null)return result;
        if(!Footprint.TryGetBoundingQuadXY(carried,_placement,0f,out var reference))
            throw new NativeActionUnavailable("M12-030/M13-007 actual carried placement footprint unavailable");
        var filter=new BlockWorldFilter();filter.IgnoreIds.Add(Action.ObjectId);
        foreach(var other in _m.World.FindLocatedMatchingObjects(filter))
        {
            if(!Footprint.TryGetBoundingQuadXY(other,other.NativePose,10f,out var quad))
                throw new NativeActionUnavailable("M12-030/M13-007 actual obstacle footprint unavailable");
            if(quad!.Intersects(reference!))return 0x04000008;
        }
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
    }    internal NativePlaceGroundAtBeaconRunner(ManipulationSystem m,Pose3d pose,Func<bool>? current=null):base(()=>m.Robot.Engine.Timer.SecondsF)
    {
        current??=()=>true;SourceLog=(l,c,k,t)=>m.Robot.Engine.EmitSourceLog(l,c,k,t);SeedNativeEmptyCompletion();
        if(!current())return;
        Drive=new(m,pose);
        if(!current()){Drive.RetireNeverAdmittedTree();return;}
        AddAction(Drive,false,false);
        if(!current())return;
        Ground=new(m);
        if(!current()){Ground.RetireNeverAdmittedTree();return;}
        AddAction(Ground,false,false);
        if(!current())return;
        SetNativeCompletionProxy(Ground.Tag);
    }
}
