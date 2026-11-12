namespace Cozmo.Robot.Vision;

public sealed partial class BlockWorld
{
    internal Func<EngineRobot?>? NativeSnapshotOwner;
    // fidelity: M11-038 — BroadcastLocatedObjectStates 0x0061E6C0 (tag 0x53; current-origin compare 0x0061EC22; isConnected = 1 ^ ([obj+0x40] >> 31)).
    internal void BroadcastLocatedObjectStates(EngineRobot owner,Func<bool>? current=null)
    {
        using var mutation=EnterPoseMutation();
        using var diagnostics=Pose3d.UseSourceLog((l,c,k,t)=>owner.Engine.EmitSourceLog(l,c,k,t),()=>owner.Engine.Das.ForceFlushNow());
        var filter=new BlockWorldFilter { OriginMode=OriginMode.Custom };
        filter.AllowedOrigins.Add(owner.CurrentOriginId);
        var rows=new List<LocatedObjectState>();
        FindLocatedObjectHelper(filter,obj=>
        {
            if(NativeObjectPose.TryConvert(obj.NativePose,owner,out var pose))
                rows.Add(new(obj.ObjectId,obj.LastObservedTimestamp,(uint)obj.Family,(uint)obj.Type,pose,(byte)obj.PoseState,obj.ActiveId>=0));
        },false);
        if(owner.HasExternalInterface && !owner.Lifetime.Destroying && (current?.Invoke()??true))owner.Engine.PublishLocatedObjectStates(new(Array.AsReadOnly(rows.ToArray())));
    }
    // fidelity: M11-038 — BroadcastConnectedObjects 0x0061E91C (tag 0x54 via FindConnectedObjectHelper 0x0061F078).
    internal void BroadcastConnectedObjects(EngineRobot owner,Func<bool>? current=null)
    {
        if(!owner.HasExternalInterface)return;
        using var mutation=EnterPoseMutation();
        var rows=new List<ConnectedObjectState>();
        FindConnectedObjectHelper(new BlockWorldFilter(),o=>rows.Add(new(o.ObjectId,(uint)o.Family,(uint)o.Type)),false);
        if(!owner.Lifetime.Destroying && (current?.Invoke()??true))owner.Engine.PublishConnectedObjects(new(Array.AsReadOnly(rows.ToArray())));
    }
}
