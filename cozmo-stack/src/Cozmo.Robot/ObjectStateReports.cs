using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot;

public readonly record struct LocatedObjectState(uint ObjectId,uint Timestamp,uint Family,uint Type,EngineGamePose Pose,byte PoseState,bool Connected);
public readonly record struct ConnectedObjectState(uint ObjectId,uint Family,uint Type);
public sealed record LocatedObjectStatesReport(IReadOnlyList<LocatedObjectState> Objects);
public sealed record ConnectedObjectsReport(IReadOnlyList<ConnectedObjectState> Objects);

public sealed partial class CozmoEngine
{
    public event Action<LocatedObjectStatesReport>? LocatedObjectStates;
    public event Action<ConnectedObjectsReport>? ConnectedObjects;
    internal void PublishLocatedObjectStates(LocatedObjectStatesReport report)
    {
        var w=new CladWriter().U8(unchecked((byte)report.Objects.Count));
        foreach(var o in report.Objects)
        {
            var p=o.Pose;
            w.U32(o.ObjectId).U32(o.Timestamp).U32(o.Family).U32(o.Type)
             .F32(p.X).F32(p.Y).F32(p.Z).F32(p.Q0).F32(p.Q1).F32(p.Q2).F32(p.Q3).U32(p.OriginId)
             .U8(o.PoseState).U8(o.Connected?(byte)1:(byte)0);
        }
        DeliverAnimationReportToApp(0x53,w.ToArray(),CaptureAppEvent(LocatedObjectStates,report));
        AnimationMessages.Deliver(0x53,true,report);
    }
    internal void PublishConnectedObjects(ConnectedObjectsReport report)
    {
        var w=new CladWriter().U8(unchecked((byte)report.Objects.Count));
        foreach(var o in report.Objects)w.U32(o.ObjectId).U32(o.Family).U32(o.Type);
        DeliverAnimationReportToApp(0x54,w.ToArray(),CaptureAppEvent(ConnectedObjects,report));
        AnimationMessages.Deliver(0x54,true,report);
    }
}

public sealed partial class CozmoRobot
{
    /// <summary>Request the engine's current-origin located object states.</summary>
    public void RequestLocatedObjectStates()=>RequestObjectSnapshot(false);
    /// <summary>Request the engine's connected-object states.</summary>
    public void RequestConnectedObjects()=>RequestObjectSnapshot(true);
    private void RequestObjectSnapshot(bool connected)
    {
        // Empty G2E A7/A8 route directly to their retained BlockWorld recipients (61E6BC/61E918).
        // The queued closure captures the owning robot before admission; no firmware message is sent.
        var owner=Engine.Robot;
        Engine.PostGameMessage(()=>
        {
            if(owner is null || owner.Lifetime.Destroying || !ReferenceEquals(Engine.Robot,owner) || !owner.HasExternalInterface)return;
            bool Current()=>!owner.Lifetime.Destroying && ReferenceEquals(Engine.Robot,owner);
            if(connected)LocalizationWorld.BroadcastConnectedObjects(owner,Current);
            else LocalizationWorld.BroadcastLocatedObjectStates(owner,Current);
        });
    }
}
