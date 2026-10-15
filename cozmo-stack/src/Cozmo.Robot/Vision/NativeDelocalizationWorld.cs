namespace Cozmo.Robot.Vision;

// fidelity: M11-069, M11-070, M11-019
public sealed partial class BlockWorld
{
    internal Func<uint,Pose3d>? NativeOrigin;
    internal Action? DelocalizeRuntime;
    internal Action? NativeForceFlush;
    private readonly Dictionary<uint,Pose3d> _fixtureOrigins=new();
    internal void ResetPoseFrameMismatch(){lock(_gate)_robot2C0=0;}
    internal Pose3d GetNativeOrigin(uint id)
    {
        if(NativeOrigin is {} lookup)return lookup(id);
        if(!_fixtureOrigins.TryGetValue(id,out var pose))_fixtureOrigins[id]=pose=new Pose3d(PoseOriginNames.NameOf(id)){Id=id};
        return pose; // Standalone host fixture's origin owner, not a native lookup-failure policy.
    }
    public uint UpdateObjectOrigin(uint objectId,uint oldOrigin)
    {
        using var frameMutation=EnterPoseMutation();
        using var scope=Pose3d.UseSourceLog((l,c,k,t)=>Source(l,c,k,t),NativeForceFlush);
        bool hasOrigin;KeyValuePair<(uint Origin,int Family,int Type,int Id),ObservableObject> found;
        lock(_gate)
        {hasOrigin=_located.Keys.Any(k=>k.Origin==oldOrigin);found=_located.FirstOrDefault(kv=>kv.Key.Origin==oldOrigin&&kv.Key.Id==unchecked((int)objectId));}
            if(!hasOrigin)
            {Source(EngineLogLevel.Info,"BlockWorld","BlockWorld.UpdateObjectOrigin.BadOrigin",FormattableString.Invariant($"Origin {unchecked((int)oldOrigin)} not found"));return 1;}
            var old=GetNativeOrigin(oldOrigin);
            if(found.Value is not {} obj)
            {Source(EngineLogLevel.Info,"BlockWorld","BlockWorld.UpdateObjectOrigin.ObjectNotFound",FormattableString.Invariant($"Object {unchecked((int)objectId)} not found in origin {PoseDiagnostics.CString(old.Name)}"));return 1;}
            if(PoseTreeNode.HasSameRoot(obj.NativePose.Node,old.Node))return 0;
            using var root=obj.NativePose.FindRoot();
            Source(EngineLogLevel.Info,"BlockWorld","BlockWorld.UpdateObjectOrigin.ObjectFound",FormattableString.Invariant($"Updating ObjectID {unchecked((int)objectId)} from origin {PoseDiagnostics.CString(old.Name)} to {PoseDiagnostics.CString(root.Name)}"));
            lock(_gate)_located[(root.Id,(int)obj.Family,(int)obj.Type,unchecked((int)objectId))]=obj;
            AddInExistingPose(obj);
            lock(_gate)
            {_located.Remove(found.Key);obj.OriginId=root.Id;} // Host root cache; flattened map removes empty levels.
            return 0;
    }
    internal IReadOnlyList<ObservableObject> FinishDelocalization(uint origin)=>FinishDelocalization(origin,origin);
    /// <summary>
    /// BlockWorld::OnRobotDelocalized(arg) 0x006249C4. <paramref name="currentOrigin"/> is the robot's PoseOriginList current
    /// origin ID: DeleteObjectsFromZombieOrigins 0x00620920 keeps the bucket equal to [[this]+0x294]+0x10 (0x00620948..0x00620952).
    /// <paramref name="mapOrigin"/> is the argument, AddNewOrigin's return, which goes only to CreateLocalizedMemoryMap (r5,
    /// 0x006249D6). The two differ only after a failed AddOriginWithID, when the current origin is unchanged (B-M11-8 round 2).
    /// </summary>
    internal IReadOnlyList<ObservableObject> FinishDelocalization(uint currentOrigin,uint mapOrigin)
    {
        using var frameMutation=EnterPoseMutation();
        var deleted=new List<ObservableObject>();
        uint[] origins;
        lock(_gate){CurrentOriginId=currentOrigin;origins=_located.Keys.Select(k=>k.Origin).Distinct().ToArray();}
        foreach(var id in origins)
        {
                if(id==currentOrigin||AnyRemainingLocalizableObjects(id))
                    Source(EngineLogLevel.Debug,"BlockWorld","BlockWorld.DeleteObjectsFromZombieOrigins.KeepingOrigin",FormattableString.Invariant($"Origin {unchecked((int)id)} is still good (keeping objects)"));
                else
                {
                    Source(EngineLogLevel.Info,"BlockWorld","BlockWorld.DeleteObjectsFromZombieOrigins.DeletingOrigin",FormattableString.Invariant($"Deleting objects from Origin {unchecked((int)id)} because it was zombie"));
                    lock(_gate)foreach(var key in _located.Keys.Where(k=>k.Origin==id).ToArray()){deleted.Add(_located[key]);_located.Remove(key);}
                }
        }
        CreateLocalizedMemoryMap(mapOrigin);SelectedObjectId=null;
        UnbuiltBroadcastLocatedObjectStatesCalls++;
        Behavior.SteppedBehavior.ReportMissing("M11-038 BroadcastLocatedObjectStates 0x006249EC -> 0x0061E6C0 payload/recipient remains unbuilt; tail call accounted"); // B-M11-8 item 6: stack channel, not the engine log stream.
        return deleted;
    }
}
