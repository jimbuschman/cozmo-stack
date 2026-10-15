using Cozmo.Protocol;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot;

// fidelity: M11-069, M11-070, M11-053, M11-071
public sealed partial class EngineRobot
{
    private readonly Dictionary<uint,Pose3d> _nativeOrigins=new();
    // fidelity: M11-077 — ~PoseOriginList 0x00847944..0x008479DE.
    // Delete each ordered-map unique_ptr while unowned parents are allowed; restore before tree-node storage release.
    internal void DestroyPoseOriginOwners()
    {
        using var scope=Pose3d.UseSourceLog((l,c,k,t)=>Engine.EmitSourceLog(l,c,k,t),Engine.Das.ForceFlushNow);
        // fidelity: M11-055 — event first, before the flag and origin resets (0x0084795E).
        // Snapshot under the pose gate, then emit with callbacks outside it. The origin list is engine-thread owned.
        int count;lock(PoseTreeGate)count=_nativeOrigins.Count;
        Engine.EmitSourceLog(EngineLogLevel.Event,null,"poseoriginlist.destructor",count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        LogCallbackDeferral.RunGated(PoseTreeGate,()=>
        {
            bool prior=Pose3d.UnownedParentsAllowed;Pose3d.UnownedParentsAllowed=true;
            try{foreach(var entry in _nativeOrigins.OrderBy(e=>e.Key))entry.Value.Dispose();}
            finally{Pose3d.UnownedParentsAllowed=prior;}
            _nativeOrigins.Clear();
        });
    }
    internal uint NativeRobotId {get;}
    private static readonly Pose3d MissingOrigin=new(""); // MissingOrigin's native name is not a checked input in this job.
    internal Pose3d MainPose {get;}=new("");
    internal Pose3d ForwardPose {get;}=new("");
    // B-M11-8 queued: the ctor's names, "RobotLiftBase" (13 characters, 0x0050FFF8 -> 0x005109CC) for the pivot at +0x2E4 and
    // "RobotLift" (9 characters, 0x0051004A -> 0x005109DC) for the lift at +0x2F0.
    internal Pose3d LiftPivot {get;}=new(Rotation3d.Identity,new(-41,0,45),"RobotLiftBase");
    internal Pose3d LiftPose {get;}=new(Rotation3d.Identity,new(66,0,0),"RobotLift");
    // B-M11-8 item 4: Robot::Update runs Delocalize, ComputeLiftPose and the carried-pose write on one thread. Here the
    // vision worker's UpdateCarriedObjectPose (DockingSystem) also writes MainPose, so the three writers share this gate.
    internal readonly object PoseTreeGate=new();
    internal RobotStateHistory PoseHistory {get;}=new();
    internal byte VisionPoseUpdated;
    internal bool HasExternalInterface=true;
    internal Pose3d GetNativeOrigin(uint id)
    {
        if(_nativeOrigins.TryGetValue(id,out var root))return root;
        // 0x00847C5C..0x00847C64: sVerifyFailedReturnFalse(key 0x00847D00, "VERIFY(%s): %d" 0x00C29749, condition 0x00847D24, id).
        PoseDiagnostics.Verify("PoseOriginList.GetOriginByID.BadID",FormattableString.Invariant($"VERIFY(iter != _origins.end()): {unchecked((int)id)}"));
        return MissingOrigin;
    }
    internal void Delocalize(bool suppliedCarrying)
    {
        using var scope=Pose3d.UseSourceLog((l,c,k,t)=>Engine.EmitSourceLog(l,c,k,t),()=>Engine.Das.ForceFlushNow());
        var world=Engine.DelocalizationWorld;
        if(world is not null){world.NativeOrigin=GetNativeOrigin;world.NativeForceFlush=()=>Engine.Das.ForceFlushNow();}
        world?.DelocalizeRobotFields(); // D1 before cliff statistics.
        Engine.ClearDelocalizationCliffs?.Invoke();
        uint newId=0;
        // B-M11-8 item 4: D3..D17 write the shared pose nodes and the origin list under the gate. The log callbacks raised in
        // it are queued and run, in order, after the gate is released (LogCallbackDeferral.RunGated).
        LogCallbackDeferral.RunGated(PoseTreeGate,()=>
        {
        uint old=CurrentOriginId;
        // B-M11-8 item 7: AddNewOrigin (0x00847A84..0x00847AA0) never aborts. A failed AddOriginWithID logs the VERIFY and
        // returns the attempted ID; Delocalize stores it (0x00510A66..0x00510A70) and carries on with the unchanged current origin.
        newId=_nextOriginId;
        if(!AddOriginWithId(newId))
            PoseDiagnostics.Verify("PoseOriginList.AddNewOrigin.AddWithIDFailed",FormattableString.Invariant($"VERIFY(success): ID:{unchecked((int)newId)}")); // key 0x00847AA8, "VERIFY(%s): ID:%d" 0x00C29737, condition 0x00847AD4
        var origin=GetNativeOrigin(CurrentOriginId); // fp = PoseOriginList+0x14 (0x00510A70), the current origin either way.
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
        });
        // 0x00510D5A..0x00510D62: OnRobotDelocalized(AddNewOrigin's return). The zombie keep compares with the list's current origin
        // (0x00620948..0x00620952), unchanged on a failure; the argument reaches only CreateLocalizedMemoryMap (0x006249D6).
        world?.FinishDelocalization(CurrentOriginId,newId);
        foreach(var stage in new[]{"FaceWorld","AIComponent","BehaviorManager","MovementComponent"})
            Engine.DelocalizationComponentHook?.Invoke(stage); // FaceWorld gets newId too (0x00510D68); its body has no observable output.
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
        // B-M11-8 item 8: the head angle is Robot+0x2FC (Motion.HeadAngleRad: ctor 0xBEDF66F3 at 0x0051007C..0x00510086;
        // SetHeadAngle 0x00513358 writes it only when +0x314 is set, clamping below 0xBEFA35DD to 0xBEDF66F3 and above
        // 0x3F543B67 to 0x3F46D3F2), not the last state's report.
        float head;
        if(Engine.PublicationHeadAngle is {} headAngle)head=headAngle();
        else
        {
            Behavior.SteppedBehavior.ReportMissing("M11-070 SetNewPose: no Robot+0x2FC owner (Motion) is bound to this engine; the ctor value 0xBEDF66F3 is used");
            head=BitConverter.Int32BitsToSingle(unchecked((int)0xBEDF66F3));
        }
        return PoseHistory.AddVisionOnlyState(StoredState?.Timestamp??0,MainPose,PoseFrameId,head,StoredState?.LiftAngle??0);
    }
    // fidelity: M12-027 — adopted OV01 rows L5–L8, needed by the carried tree's delocalization path.
    internal void ComputeLiftPose(float input)
    {
        LogCallbackDeferral.RunGated(PoseTreeGate,()=>ComputeLiftPoseLocked(input));
    }
    private void ComputeLiftPoseLocked(float input)
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
        // Noted (B-M11-8 queued): the native DeliverToGame (0x00661594) hands the message to the app connection's buffer
        // (E4/E5); the app reads it later. Under the app boundary (AGENTS.md) the stack calls its C# handlers here, inline,
        // before the in-engine subscribers, so an app handler runs inside Delocalize where the native app would not.
        if(EngineToGameMessage is not {} handlers)return;
        foreach(Action<ushort,ReadOnlyMemory<byte>> handler in handlers.GetInvocationList())
            RunIsolated(()=>handler(0x32,ReadOnlyMemory<byte>.Empty)); // M1-034 host handler isolation.
    }
}
