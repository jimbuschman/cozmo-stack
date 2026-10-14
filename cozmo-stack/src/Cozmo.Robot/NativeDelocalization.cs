using Cozmo.Protocol;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot;

// fidelity: M11-069, M11-070, M11-053, M11-071
public sealed partial class EngineRobot
{
    private readonly Dictionary<uint,Pose3d> _nativeOrigins=new();
    internal uint NativeRobotId {get;}
    private static readonly Pose3d MissingOrigin=new(""); // MissingOrigin's native name is not a checked input in this job.
    internal Pose3d MainPose {get;}=new("");
    internal Pose3d ForwardPose {get;}=new("");
    internal Pose3d LiftPivot {get;}=new(Rotation3d.Identity,new(-41,0,45));
    internal Pose3d LiftPose {get;}=new(Rotation3d.Identity,new(66,0,0));
    internal RobotStateHistory PoseHistory {get;}=new();
    internal byte VisionPoseUpdated;
    internal bool HasExternalInterface=true;
    internal Pose3d GetNativeOrigin(uint id)
    {
        if(_nativeOrigins.TryGetValue(id,out var root))return root;
        PoseDiagnostics.Verify("PoseOriginList.GetOriginByID.BadID",FormattableString.Invariant($"MISSING: M11-053 checked BadID VERIFY condition/format at 0x00847C64; ID:{unchecked((int)id)}"));
        return MissingOrigin;
    }
    internal void Delocalize(bool suppliedCarrying)
    {
        using var scope=Pose3d.UseSourceLog((l,c,k,t)=>Engine.EmitSourceLog(l,c,k,t),()=>Engine.Das.ForceFlushNow());
        var world=Engine.DelocalizationWorld;
        if(world is not null){world.NativeOrigin=GetNativeOrigin;world.NativeForceFlush=()=>Engine.Das.ForceFlushNow();}
        world?.DelocalizeRobotFields(); // D1 before cliff statistics.
        Engine.ClearDelocalizationCliffs?.Invoke();
        uint old=CurrentOriginId;
        if(!AddOriginWithId(_nextOriginId))
        {Engine.EmitSourceLog(EngineLogLevel.Warning,null,"MISSING","M11-053 AddNewOrigin failure VERIFY 0x00847A92 text is not a checked row; previous origin retained");return;}
        var origin=GetNativeOrigin(CurrentOriginId);
        Engine.EmitSourceLog(EngineLogLevel.Event,null,"Robot.Delocalize",FormattableString.Invariant($"Delocalizing robot {unchecked((int)NativeRobotId)}. New origin: {origin.Name}. NumOrigins={_origins.Count}"));
        MainPose.Node!.Transform=new(Rotation3d.FromAngleAxis(0,new(0,0,1)),Vec3.Zero);MainPose.SetParent(origin);
        ForwardPose.Node!.Transform=new(Rotation3d.FromAngleAxis(0,new(0,0,1)),Vec3.Zero);ForwardPose.SetParent(origin);
        if(SetNewPose(MainPose)!=0)Engine.EmitSourceLog(EngineLogLevel.Warning,null,"Robot.Delocalize.SetNewPose","Failed to set new pose");
        uint timestamp=StoredState?.Timestamp??0;
        if(TimeSynced)
        {
            Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","Robot.Delocalize.SendingNewOriginID",FormattableString.Invariant($"Sending new localization update at t={timestamp}, with pose frame {PoseFrameId} and origin ID={CurrentOriginId}"));
            using var flat=MainPose.GetWithRespectToRoot();
            Send(new AbsoluteLocalizationUpdate{Timestamp=timestamp,PoseFrameId=PoseFrameId,PoseOriginId=CurrentOriginId,PoseX=(float)flat.Translation.X,PoseY=(float)flat.Translation.Y,PoseAngleRad=(float)flat.AngleAroundZ},"AbsoluteLocalizationUpdate");
        }
        // D10/D11: shipped VizManager calls produce no output.
        world?.ClearConfirmer();
        var ids=Engine.PublicationCarrying?.Invoke()??(-1,-1);
        bool carrying=ids.Item1!=-1;
        if(suppliedCarrying!=carrying)Engine.EmitSourceLog(EngineLogLevel.Warning,null,"Robot.Delocalize.IsCarryingObjectMismatch",FormattableString.Invariant($"Passed-in isCarryingObject={(suppliedCarrying?'Y':'N')}, IsCarryingObject()={(carrying?'Y':'N')}"));
        if(carrying&&world is not null)
        {
            var objects=new SortedSet<int>{ids.Item1};if(ids.Item2!=-1)objects.Add(ids.Item2);
            foreach(int id in objects)if(world.UpdateObjectOrigin(unchecked((uint)id),old)!=0)
                Engine.EmitSourceLog(EngineLogLevel.Warning,null,"Robot.Delocalize.UpdateObjectOriginFailed",FormattableString.Invariant($"Object {id}"));
        }
        world?.FinishDelocalization(CurrentOriginId);
        foreach(var stage in new[]{"FaceWorld","AIComponent","BehaviorManager","MovementComponent"})
            Engine.DelocalizationComponentHook?.Invoke(stage);
        if(HasExternalInterface)
        {
            Engine.DeliverDelocalizationToApp(); // E1–E4: app first, synchronous subscribers afterwards.
            Engine.DelocalizationConfigurationHook?.Invoke(); // M11-072 complete list/order unread; job orders (a), then (b).
            Engine.DelocalizationCubeHook?.Invoke();
            Engine.AnimationMessages.Deliver(0x32,true);
        }
    }
    internal void SetPose(Pose3d value)
    {
        if(!PoseTreeNode.HasSameRoot(value.Node,GetNativeOrigin(CurrentOriginId).Node))
        {PoseDiagnostics.Verify("Robot.SetPose.NewPoseOriginAndWorldOriginMismatch","VERIFY(newPose.HasSameRootAs(GetWorldOrigin())): ");return;}
        string name=MainPose.Name;MainPose.Assign(value);MainPose.Name=name;
        ForwardPose.Assign(MainPose);
        float offset=(Engine.PublicationCarrying?.Invoke()??(-1,-1)).Item1==-1?-20:0;
        float yaw=(float)MainPose.AngleAroundZ;
        // EQUIVALENT_IMPLEMENTATION M13-023(e): unshipped cosf/sinf, separate f32 mul/add.
        float x=(float)ForwardPose.Translation.X+offset*MathF.Cos(yaw);
        float y=(float)ForwardPose.Translation.Y+offset*MathF.Sin(yaw);
        ForwardPose.Node!.Transform=new(ForwardPose.Quaternion,new(x,y,0));
    }
    internal uint SetNewPose(Pose3d value)
    {
        using(var root=value.GetWithRespectToRoot())SetPose(root);
        VisionPoseUpdated=1;PoseFrameId=unchecked(PoseFrameId+1);
        return PoseHistory.AddVisionOnlyState(StoredState?.Timestamp??0,MainPose,PoseFrameId,StoredState?.HeadAngle??0,StoredState?.LiftAngle??0);
    }
    // fidelity: M12-027 — adopted OV01 rows L5–L8, needed by the carried tree's delocalization path.
    internal void ComputeLiftPose(float input)
    {
        var initial=Rotation3d.FromAngleAxis(new Radians(input).Value,new(0,1,0));
        var t=new Transform3d(initial,new(66,0,0));
        var vector=new RotationVector3d(new Radians(-input),new(0,1,0));
        LiftPose.Node!.Transform=t.RotateBy(Rotation3d.FromRotationVector(vector));
    }
}

public sealed partial class CozmoEngine
{
    internal BlockWorld? DelocalizationWorld;
    internal Action? ClearDelocalizationCliffs,DelocalizationConfigurationHook,DelocalizationCubeHook;
    internal Action<string>? DelocalizationComponentHook;
    internal uint NativeUiDeliveryCount;
    /// <summary>Engine-to-app union tag and body; RobotDelocalized is tag 0x32 with an empty body.</summary>
    public event Action<ushort,ReadOnlyMemory<byte>>? EngineToGameMessage;
    internal void DeliverDelocalizationToApp()
    {
        NativeUiDeliveryCount=unchecked(NativeUiDeliveryCount+1); // E3: UI+0xFC before delivery, including no connected app.
        if(EngineToGameMessage is not {} handlers)return;
        foreach(Action<ushort,ReadOnlyMemory<byte>> handler in handlers.GetInvocationList())
            RunIsolated(()=>handler(0x32,ReadOnlyMemory<byte>.Empty)); // M1-034 host handler isolation.
    }
}
