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
        LogCallbackDeferral.RunGated(PoseTreeGate,()=>
        {
            // fidelity: M11-055 — one gated body: event first, then flag/reset/restore/tree clear.
            Engine.EmitSourceLog(EngineLogLevel.Event,null,"poseoriginlist.destructor",_nativeOrigins.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            // Separate host robot instances must not interleave saves/restores of this process static.
            lock(Pose3d.UnownedParentsGate)
            {
                bool prior=Pose3d.UnownedParentsAllowed;Pose3d.UnownedParentsAllowed=true;
                try{foreach(var entry in _nativeOrigins.OrderBy(e=>e.Key))entry.Value.Dispose();}
                finally{Pose3d.UnownedParentsAllowed=prior;}
            }
            _nativeOrigins.Clear();
        });
    }
    //00513AB8..B16: readonly Robot ctor geometry reconstructed with checked P2 operations.
    //The final parent is this result's SAME owned computed history pose, never MAIN/current origin.
    internal Pose3d CopyHistoricalCameraPose(HistRobotState state)
    {
        using var neck=new Pose3d(Rotation3d.Identity,HeadGeometry.NeckPositionMm);
        using var headcam=new Pose3d(HeadGeometry.DefaultHeadCamRotation,HeadGeometry.EngineHeadCamPositionMm);
        var camera=HeadGeometry.GetCameraPose(headcam,state.State.HeadAngle);
        camera.PreComposeWith(neck);camera.SetParent(state.Pose);return camera;
    }
    // Current outer camera uses accepted head angle and the live MAIN node, not history.
    internal sealed class CurrentCameraScope : IDisposable
    {
        private readonly Pose3d _neck;
        internal Pose3d Pose { get; }
        internal CurrentCameraScope(EngineRobot owner, float acceptedHeadAngle)
        {
            _neck = new Pose3d(Rotation3d.Identity, HeadGeometry.NeckPositionMm, "RobotNeck", owner.GetCheckedMainPose());
            using var headcam = new Pose3d(HeadGeometry.DefaultHeadCamRotation, HeadGeometry.EngineHeadCamPositionMm);
            headcam.SetParent(_neck);
            Pose = HeadGeometry.GetCameraPose(headcam, acceptedHeadAngle);
        }
        public void Dispose() { Pose.Dispose(); _neck.Dispose(); }
    }
    internal CurrentCameraScope CopyCurrentCameraPose(float acceptedHeadAngle) => new(this, acceptedHeadAngle);
    internal uint NativeRobotId {get;}
    internal uint NativeBodyId {get;private set;} // Robot+0x1C, constructor zero 0x0050FC3C
    private static readonly Pose3d MissingOrigin=new(""); // MissingOrigin's native name is not a checked input in this job.
    internal Pose3d MainPose {get;}=new("");
    internal Pose3d ForwardPose {get;}=new("");
    internal float? NativeLiftAngle;
    internal Pose3d GetCheckedForwardPose()
    {
        var current=GetNativeOrigin(CurrentOriginId);
        if(!PoseTreeNode.HasSameRoot(ForwardPose.Node,current.Node))
            PoseDiagnostics.Verify("Robot.GetDriveCenterPose.PoseOriginNotWorldOrigin","Stored drive center does not share the current world origin");
        return ForwardPose; //5477A0 diagnostic does not change the returned pointer.
    }
    // B-M11-8 queued: the ctor's names, "RobotLiftBase" (13 characters, 0x0050FFF8 -> 0x005109CC) for the pivot at +0x2E4 and
    // "RobotLift" (9 characters, 0x0051004A -> 0x005109DC) for the lift at +0x2F0.
    internal Pose3d LiftPivot {get;}=new(Rotation3d.Identity,new(-41,0,45),"RobotLiftBase");
    internal Pose3d LiftPose {get;}=new(Rotation3d.Identity,new(66,0,0),"RobotLift");
    // B-M11-8 item 4: Robot::Update runs Delocalize, ComputeLiftPose and the carried-pose write on one thread. Here the
    // vision worker's UpdateCarriedObjectPose (DockingSystem) also writes MainPose, so the three writers share this gate.
    internal readonly object PoseTreeGate=new();
    internal RobotStateHistory PoseHistory {get;}=new();
    internal byte VisionPoseUpdated;
    private long _visionPoseVersion;
    internal void MarkVisionPoseUpdated() { VisionPoseUpdated = 1; _visionPoseVersion++; }
    internal void SendPendingVisionLocalization()
    {
        using var frame = Engine.DelocalizationWorld?.EnterPoseMutation();
        using var gate = LogCallbackDeferral.EnterGated(PoseTreeGate);
        if (VisionPoseUpdated == 0 || Lifetime.Destroying || !ReferenceEquals(Engine.Robot, this)) return;
        long version = _visionPoseVersion;
        using var sample = new HistRobotState();
        if (!PoseHistory.CopyLatestVisionOnly(sample, out uint timestamp))
            Engine.EmitSourceLog(EngineLogLevel.Warning, null, "Robot.SendAbsLocUpdate.NoVizPoseFound", "");
        else
        {
            using var flat = sample.Pose.GetWithRespectToRoot();
            if (!flat.HasParent)
                Behavior.SteppedBehavior.ReportMissing("M1-034/M11-044 SendAbsLocalizationUpdate: missing flattened pose parent");
            else
            {
                using var root = flat.FindRoot();
                if (!TryGetNativeOriginOwner(root.Id, out _))
                {
                    Engine.EmitSourceLog(EngineLogLevel.Error, null, "Robot.SendAbsLocUpdate.InvalidPoseOriginID", $"{unchecked((int)root.Id)}");
                    Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                }
                else if (!Lifetime.Destroying && ReferenceEquals(Engine.Robot, this))
                    SendCheckedOwned(new AbsoluteLocalizationUpdate { Timestamp = timestamp, PoseFrameId = sample.State.PoseFrameId,
                        PoseOriginId = root.Id, PoseX = (float)flat.Translation.X, PoseY = (float)flat.Translation.Y, PoseAngleRad = (float)flat.AngleAroundZ });
            }
        }
        if (_visionPoseVersion == version && !Lifetime.Destroying && ReferenceEquals(Engine.Robot, this)) VisionPoseUpdated = 0;
    }
    internal bool HasExternalInterface=true;
    internal bool TryGetNativeOriginOwner(uint id,out Pose3d? origin)
    {
        lock(PoseTreeGate)
        {
            if(_nativeOrigins.TryGetValue(id,out var owner)&&owner.Node is not null){origin=owner;return true;}
            origin=null;return false; // Never substitute MissingOrigin for an actual producer input.
        }
    }
    internal Pose3d GetNativeOrigin(uint id)
    {
        if(_nativeOrigins.TryGetValue(id,out var root))return root;
        // 0x00847C5C..0x00847C64: sVerifyFailedReturnFalse(key 0x00847D00, "VERIFY(%s): %d" 0x00C29749, condition 0x00847D24, id).
        PoseDiagnostics.Verify("PoseOriginList.GetOriginByID.BadID",FormattableString.Invariant($"VERIFY(iter != _origins.end()): {unchecked((int)id)}"));
        return MissingOrigin;
    }
    /// <summary>
    /// <c>PoseOriginList::Rejigger(newOrigin, oldWrtNew)</c> 0x00847D7C..0x00847E84 (census LOCALIZE-CROSS-ORIGIN-1 X6/X7). Guards, both VERIFY and return 1 with
    /// nothing changed (0x00847DD2): the new origin's ID not in the list (UnknownOrigin), then the new ID equal to the current one (NewOriginIsCurrent).
    /// Success: the supplied transform copied into the OLD current origin's node (0x00847E1A..0x00847E40), SetParent(old, stored new) (0x00847E48), old
    /// name + "_REJ" (0x00847E58..0x00847E66), current pose and ID := new (0x00847E7A / 0x00847E7E), return 0. No origin is erased.
    /// </summary>
    // fidelity: M11-044
    internal uint RejiggerOrigins(Pose3d newOrigin, Transform3d oldWrtNew)
    {
        uint newId = newOrigin.Id;                                               // 0x00847D8A GetID(new)
        if (!ContainsOriginId(newId))                                            // 0x00847D94..0x00847D98
        {
            PoseDiagnostics.Verify("PoseOriginList.Rejigger.UnknownOrigin", FormattableString.Invariant(
                $"VERIFY(ContainsOriginID(newOriginID)): Pose ID:{unchecked((int)newId)} ({PoseDiagnostics.CString(newOrigin.Name)}) not known to PoseOriginList"));
            return 1;
        }
        if (CurrentOriginId == newId)                                            // 0x00847D9C..0x00847DA0
        {
            PoseDiagnostics.Verify("PoseOriginList.Rejigger.NewOriginIsCurrent", FormattableString.Invariant($"VERIFY(_current.originID != newOriginID): ID:{unchecked((int)newId)}"));
            return 1;
        }
        uint oldId = CurrentOriginId;
        var old = _nativeOrigins[oldId];                                         // 0x00847DD8 find(current ID)
        var stored = _nativeOrigins[newId];                                      // 0x00847DE0..0x00847E0E tree search by ID
        old.Node!.Transform = oldWrtNew;                                         // 0x00847E1A..0x00847E40
        old.SetParent(stored);                                                   // 0x00847E48
        old.Name = old.Name + "_REJ";                                            // 0x00847E58..0x00847E66 ("_REJ" at 0x00847F20)
        OriginNames.Rejigger(oldId, newId);                                      // the M4-037 name tree follows the same node edit
        CurrentOriginId = newId;                                                 // 0x00847E7A / 0x00847E7E
        return 0;                                                                // 0x00847E80
    }

    /// <summary>
    /// <c>PoseOriginList::Flatten(targetID)</c> 0x00847F34..0x00847FBE (census X13): GetOriginByID(target), then every origin in ascending ID order; one with
    /// a parent (0x00847F58) that is not an immediate child of the target (IsChildOf, 0x00847F64 / 0x00844FD4) is converted in place with
    /// <c>GetWithRespectTo(P, target, P)</c> (0x00847F70); only a success appends "_FLT" (0x00847F8C, string 0x00847FD8). Void; nothing erased.
    /// </summary>
    // fidelity: M11-044
    internal void FlattenOrigins(uint targetId)
    {
        var target = GetNativeOrigin(targetId);                                  // 0x00847F3C
        foreach (var (id, origin) in _nativeOrigins.OrderBy(e => e.Key).ToArray())
        {
            if (!origin.HasParent) continue;                                     // 0x00847F58
            if (ReferenceEquals(origin.Node!.Parent, target.Node)) continue;     // 0x00847F64..0x00847F68: IsChildOf, immediate parent
            if (!Pose3d.GetWithRespectTo(origin, target, origin)) continue;      // 0x00847F70, in place
            origin.Name = origin.Name + "_FLT";                                  // 0x00847F8C
            OriginNames.Flatten(id, targetId);
        }
    }
    internal void Delocalize(bool suppliedCarrying)
    {
        using var frameMutation=Engine.DelocalizationWorld?.EnterPoseMutation();
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
        // Constructor delocalization runs before the new owner is inserted into Robots.
        bool Current()=>!Lifetime.Destroying && (Engine.Robot is null || ReferenceEquals(Engine.Robot,this));
        world?.FinishDelocalization(CurrentOriginId,newId,this,Current);
        if(!Current())return;
        foreach(var stage in new[]{"FaceWorld","AIComponent","BehaviorManager","MovementComponent"})
        {
            Engine.DelocalizationComponentHook?.Invoke(stage); // FaceWorld gets newId too (0x00510D68); its body has no observable output.
            if(!Current())return;
        }
        if(HasExternalInterface)
        {
            Engine.DeliverDelocalizationToApp(CurrentOriginId); // E1–E4: app first, synchronous subscribers afterwards.
            // fidelity: M11-072 — R2..R8 are insertion-ordered UI subscriptions, not separate pre-emit hooks.
            Engine.AnimationMessages.Deliver(0x32,true);
        }
    }
    // fidelity: M12-039 — direct dependency Robot::GetPose 0x004EA398..0x004EA472, OV05.
    internal Pose3d GetCheckedMainPose()
    {
        using var root = MainPose.FindRoot();
        var origin = GetNativeOrigin(CurrentOriginId);
        if (root.Id != CurrentOriginId)
            PoseDiagnostics.Verify("Robot.GetPose.BadPoseRootOrWorldOriginID", FormattableString.Invariant($"VERIFY(_pose.GetRootID() == GetWorldOriginID()): WorldOriginID:{unchecked((int)CurrentOriginId)}({origin.Name}), RootID:{unchecked((int)root.Id)}"));
        if (!PoseTreeNode.HasSameRoot(MainPose.Node, origin.Node))
            PoseDiagnostics.Verify("Robot.GetPose.PoseOriginNotWorldOrigin", $"VERIFY(_pose.HasSameRootAs(GetWorldOrigin())): WorldOrigin: {origin.GetNamedPathToRoot(false)}, Pose: {MainPose.GetNamedPathToRoot(false)}");
        return MainPose; // Both VERIFY results are ignored by the original.
    }
    internal void SetPose(Pose3d value)
    {
        if(!PoseTreeNode.HasSameRoot(value.Node,GetNativeOrigin(CurrentOriginId).Node))
        {PoseDiagnostics.Verify("Robot.SetPose.NewPoseOriginAndWorldOriginMismatch","VERIFY(newPose.HasSameRootAs(GetWorldOrigin())): ");return;}
        string name=MainPose.Name;MainPose.Assign(value);MainPose.Name=name;
        ForwardPose.Assign(MainPose);
        // Robot::SetPose 0x0051508C: read the actual raw carrying ID at this write,
        // not a publication callback or a later DriveStraight Init input.
        if(CarryingOwner is null)
            throw new InvalidOperationException("MISSING: Robot.SetPose actual CarryingOwner");
        float offset=(CarryingOwner.CarriedObjectId??uint.MaxValue)==uint.MaxValue?-20f:0f;
        float yaw=(float)MainPose.AngleAroundZ;
        // EQUIVALENT_IMPLEMENTATION M13-023(e): unshipped cosf/sinf, separate f32 mul/add.
        float x=(float)ForwardPose.Translation.X+offset*MathF.Cos(yaw);
        float y=(float)ForwardPose.Translation.Y+offset*MathF.Sin(yaw);
        ForwardPose.Node!.Transform=new(ForwardPose.Quaternion,new(x,y,0));
    }
    internal uint SetNewPose(Pose3d value)
    {
        using(var root=value.GetWithRespectToRoot())SetPose(root);
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
        // The head and lift arguments are read before the call (identical writes and order of the history/frame/flag fields).
        return AddVisionOnlyStateToHistory(StoredState?.Timestamp??0,MainPose,head,StoredState?.LiftAngle??0);
    }
    // fidelity: M11-044, M11-070 — Robot::AddVisionOnlyStateToHistory(t, pose, head, lift) 0x0051513C..0x0051518A:
    // [Robot+0x2C6] := 1; [Robot+0x2B0] += 1; a default HistRobotState given SetPose(new frame, pose, head, lift);
    // RobotStateHistory::AddVisionOnlyState(t, state), whose result is returned (1 for a non-root parent, TooOld or a failed
    // emplace). The flag and frame writes persist when the add fails.
    internal uint AddVisionOnlyStateToHistory(uint timestamp,Pose3d pose,float head,float lift)
    {
        MarkVisionPoseUpdated();PoseFrameId=unchecked(PoseFrameId+1);
        return PoseHistory.AddVisionOnlyState(timestamp,pose,PoseFrameId,head,lift);
    }
    // fidelity: M12-027 — adopted OV01 rows L5–L8, needed by the carried tree's delocalization path.
    internal void ComputeLiftPose(float input)
    {
        using var frameMutation=Engine.DelocalizationWorld?.EnterPoseMutation(); // LiftPose is also the carried objects' parent.
        LogCallbackDeferral.RunGated(PoseTreeGate,()=>ComputeLiftPoseLocked(input));
    }
    private void ComputeLiftPoseLocked(float input)
    {
        NativeLiftAngle=input; //51296A/513484 raw Robot+300 write before the canonical lift transform.
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
    internal event Action<uint>? DelocalizationReported;
    internal void DeliverDelocalizationToApp(uint origin=0)
    {
        var typed=CaptureAppEvent(DelocalizationReported,origin);
        DeliverAnimationReportToApp(0x32,ReadOnlyMemory<byte>.Empty,typed); // E3 count/enqueue precedes synchronous local delivery.
    }
}
