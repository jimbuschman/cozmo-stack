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
        using var scope=Pose3d.UseSourceLog((l,c,k,t)=>Source(l,c,k,t),NativeForceFlush);
        lock(_gate)
        {
            if(!_located.Keys.Any(k=>k.Origin==oldOrigin))
            {Source(EngineLogLevel.Info,"BlockWorld","BlockWorld.UpdateObjectOrigin.BadOrigin",FormattableString.Invariant($"Origin {unchecked((int)oldOrigin)} not found"));return 1;}
            var old=GetNativeOrigin(oldOrigin);
            var found=_located.FirstOrDefault(kv=>kv.Key.Origin==oldOrigin&&kv.Key.Id==unchecked((int)objectId));
            if(found.Value is not {} obj)
            {Source(EngineLogLevel.Info,"BlockWorld","BlockWorld.UpdateObjectOrigin.ObjectNotFound",FormattableString.Invariant($"Object {unchecked((int)objectId)} not found in origin {PoseDiagnostics.CString(old.Name)}"));return 1;}
            if(PoseTreeNode.HasSameRoot(obj.NativePose.Node,old.Node))return 0;
            using var root=obj.NativePose.FindRoot();
            Source(EngineLogLevel.Info,"BlockWorld","BlockWorld.UpdateObjectOrigin.ObjectFound",FormattableString.Invariant($"Updating ObjectID {unchecked((int)objectId)} from origin {PoseDiagnostics.CString(old.Name)} to {PoseDiagnostics.CString(root.Name)}"));
            _located[(root.Id,(int)obj.Family,(int)obj.Type,unchecked((int)objectId))]=obj;
            AddInExistingPose(obj);
            _located.Remove(found.Key); // Flattened sorted map naturally removes empty type/family/origin levels.
            obj.OriginId=root.Id; // Host cache of the pose's already-changed root; no native pose/state write.
            return 0;
        }
    }
    internal IReadOnlyList<ObservableObject> FinishDelocalization(uint origin)
    {
        var deleted=new List<ObservableObject>();
        lock(_gate)
        {
            CurrentOriginId=origin;
            foreach(var id in _located.Keys.Select(k=>k.Origin).Distinct().ToArray())
            {
                if(id==origin||AnyRemainingLocalizableObjects(id))
                    Source(EngineLogLevel.Debug,"BlockWorld","BlockWorld.DeleteObjectsFromZombieOrigins.KeepingOrigin",FormattableString.Invariant($"Origin {unchecked((int)id)} is still good (keeping objects)"));
                else
                {
                    Source(EngineLogLevel.Info,"BlockWorld","BlockWorld.DeleteObjectsFromZombieOrigins.DeletingOrigin",FormattableString.Invariant($"Deleting objects from Origin {unchecked((int)id)} because it was zombie"));
                    foreach(var key in _located.Keys.Where(k=>k.Origin==id).ToArray()){deleted.Add(_located[key]);_located.Remove(key);}
                }
            }
        }
        CreateLocalizedMemoryMap(origin);SelectedObjectId=null;
        UnbuiltBroadcastLocatedObjectStatesCalls++;
        Source(EngineLogLevel.Warning,null,"MISSING","M11-038 BroadcastLocatedObjectStates 0x006249EC -> 0x0061E6C0 payload/recipient remains unbuilt; tail call accounted");
        return deleted;
    }
}
