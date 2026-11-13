using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// Where the lift is, and where a carried object sits on it.
///
/// The engine keeps this as a pose tree, built in <c>Robot::Robot</c> 0x0050FBF0. Two links matter:
///
/// <list type="bullet">
/// <item>the <b>lift pivot</b> at robot+0x2E4, a child of the robot origin at translation
///   <c>(-41, 0, 45)</c> with no rotation — <c>0xC2240000</c>, 0, <c>0x42340000</c> at 0x0050FFE0;</item>
/// <item>the <b>lift pose</b> at robot+0x2F0, a child of the pivot at <c>(66, 0, 0)</c>
///   (<c>0x42840000</c> at 0x00510038), rotated about Y by the lift angle. <c>Robot::ComputeLiftPose</c>
///   0x005151AC does that rotation and then rotates the orientation back by the same angle, so the
///   plate stays level however high the arm is.</item>
/// </list>
///
/// The 45 and the 66 are the numbers the height conversion already used; the <c>-41</c> is the one that
/// was missing, and it is why a carried cube cannot be placed from the lift height alone.
///
/// <c>CarryingComponent::SetObjectAsAttachedToLift</c> 0x00632CC4 then puts the object <em>with respect
/// to the lift pose</em> at <c>(|dockMarkerOffset| + 4, 0, -12.5)</c>: it takes the norm of the dock
/// marker's translation on the object (0x00632E42 onwards), adds 4, and pairs it with -12.5
/// (<c>0xC1480000</c> at 0x00632E82). So the object hangs below the plate with its dock marker four
/// millimetres in front of it.
/// </summary>
public static class LiftGeometry
{
    // fidelity: M12-008
    /// <summary>The lift pivot in the robot frame: robot+0x2E4's translation.</summary>
    public static readonly Vec3 PivotInRobotFrame = new(-41, 0, 45);
    /// <summary>The arm from the pivot to the plate: robot+0x2F0's translation.</summary>
    public const double ArmLengthMm = 66;
    /// <summary>How far in front of the plate the dock marker is held.</summary>
    public const double MarkerClearanceMm = 4;
    /// <summary>How far below the plate the object's centre hangs.</summary>
    public const double ObjectDropMm = -12.5;

    /// <summary>
    /// The lift pose in the robot's frame for a given lift angle: the arm swung about Y, with the plate
    /// kept level. <c>ComputeLiftPose</c> rotates by the angle and un-rotates the orientation, which
    /// leaves a pure translation.
    /// </summary>
    public static Pose3d LiftPoseInRobotFrame(double liftAngleRad) => new(
        Mat3.Identity,
        new Vec3(PivotInRobotFrame.X + ArmLengthMm * Math.Cos(liftAngleRad),
                 PivotInRobotFrame.Y,
                 PivotInRobotFrame.Z + ArmLengthMm * Math.Sin(liftAngleRad)));

    /// <summary>
    /// Where a carried object sits relative to the lift pose, given the dock marker it was picked up by.
    /// The engine uses the norm of the marker's translation on the object, so which face it is does not
    /// matter, only how far out from the centre it sits. The norm is THREE-dimensional,
    /// <c>sqrt(mx² + my² + mz²)</c> (the two-iteration loop over +0x24 and +0x28 at 0x00632E3E..0x00632E62; verifier A2), and
    /// only the translation is replaced: the rotation is the one <c>GetWithRespectTo</c> returned
    /// (<paramref name="rotationWrtLift"/>).
    /// </summary>
    // fidelity: M12-008
    public static Pose3d ObjectOnLift(KnownMarker dockMarker, Mat3 rotationWrtLift) => new(
        rotationWrtLift,
        new Vec3(dockMarker.PoseOnObject.Translation.Length + MarkerClearanceMm, 0, ObjectDropMm));

    /// <summary><see cref="ObjectOnLift(KnownMarker, Mat3)"/> for an object whose rotation is the lift pose's own.</summary>
    public static Pose3d ObjectOnLift(KnownMarker dockMarker) => ObjectOnLift(dockMarker, Mat3.Identity);

    /// <summary>The carried object's pose in the world: robot, pivot, arm, object, composed in order.</summary>
    public static Pose3d CarriedObjectWorldPose(Pose3d robotPose, double liftAngleRad, KnownMarker dockMarker) =>
        robotPose.Compose(LiftPoseInRobotFrame(liftAngleRad)).Compose(ObjectOnLift(dockMarker));

    /// <summary>The carried object's pose in the world from the pose it was attached at (<c>objWrtLift</c>, rotation kept).</summary>
    public static Pose3d CarriedObjectWorldPose(Pose3d robotPose, double liftAngleRad, Pose3d objectWrtLift) =>
        robotPose.Compose(LiftPoseInRobotFrame(liftAngleRad)).Compose(objectWrtLift);

    /// <summary>The return value of <c>SetObjectAsAttachedToLift</c> for every failure before the confirmer is reached (steps 1-5).</summary>
    public const uint AttachFailed = 1;

    /// <summary>
    /// The 15.0 <c>FindObjectOnTopOrUnderneathHelper</c> tolerance <c>SetObjectAsAttachedToLift</c> passes (0x00632F0C); the helper itself is
    /// M13-007's <see cref="BlockConfigurationManager.FindObjectOnTopOrUnderneath"/>.
    /// </summary>
    public const double StackToleranceMm = 15.0;

    /// <summary>
    /// <c>CarryingComponent::SetObjectAsAttachedToLift</c> 0x00632CC4, steps 1-9 (M12-008; verified in
    /// 20260929-R-VIS-verify-M12-M13-gap1.md):
    /// <list type="number">
    /// <item>no object id (-1): warn, return 1 (0x00632CD2..0x00632D34);</item>
    /// <item>already carrying: error, return 1 (0x00632CD8..0x00632D2E);</item>
    /// <item>the located object is missing: error, return 1 (0x00632D7A..0x00632D8E);</item>
    /// <item>no marker with the code: error, return 1; two or more: warn and use the first (0x00632D98..0x00632DCA);</item>
    /// <item><c>objWrtLift = obj.pose.GetWithRespectTo(lift pose)</c>, failure: error, return 1 (0x00632E24..0x00632E38);</item>
    /// <item>its translation becomes <c>(L + 4.0, 0, -12.5)</c>, L the 3-D length of the marker's translation; the rotation is kept (0x00632E3E..0x00632E9E);</item>
    /// <item><c>FindObjectOnTopOrUnderneathHelper(obj, 15.0, default filter, onTop = true)</c> (0x00632F06..0x00632F14);</item>
    /// <item>none: <c>this+0x14 = -1</c>; else <c>top.pose</c> with respect to the object (failure: warn, <c>this+0x14</c> unchanged, go on to step 9,
    ///   0x00633154..0x00633198), then <c>AddObjectRelativeObservation(top, tmp, obj)</c>;
    ///   non-zero returns that result WITHOUT <c>SetCarryingObject</c>, zero sets <c>this+0x14 = top.ID</c> (0x00632FA2..0x0063318C);</item>
    /// <item><c>SetCarryingObject</c>, the BlockConfigurationManager dirty flag (+0xC) = 1, and the result of
    ///   <c>AddLiftRelativeObservation(obj, objWrtLift)</c> (0x006331AE..0x006331CC).</item>
    /// </list>
    /// This helper retains the existing flat-input calculation (M12-008/M11-066) for the lift pose, which produces the world pose the parent chain would
    /// give. The live DockingSystem supplies the shared lift node and parents the argument before its broadcast; a missing robot state (no pose to derive the lift pose from)
    /// takes step 5's failure return. The object placed on top follows the carried object because its own pose node is parented to the carried
    /// object's (<c>GetWithRespectTo</c> 0x00845DFA..0x00845DFE, <c>SetParent(obj.pose)</c> 0x00633006..0x00633010) before
    /// <c>AddObjectRelativeObservation</c> stores it; <see cref="CarryingComponent.TopWrtCarried"/> is stack machinery holding the same value.
    /// </summary>
    // fidelity: M12-008
    public static uint SetObjectAsAttachedToLift(CarryingComponent carrying, BlockWorld world,
                                                 BlockConfigurationManager? configurations, VisionPoseData? state,
                                                 uint? objectId, MarkerType markerCode, Action<string>? log = null, Pose3d? liveLiftPose = null)
        => SetObjectAsAttachedToLiftCore(carrying,world,configurations,state,objectId,markerCode,log,liveLiftPose,null,null);

    internal static uint SetObjectAsAttachedToLift(CarryingComponent carrying, BlockWorld world,
        BlockConfigurationManager? configurations, VisionPoseData? state, uint? objectId, MarkerType markerCode,
        Action<string>? log, Pose3d? liveLiftPose, EngineRobot owner, Func<bool> current)
        => SetObjectAsAttachedToLiftCore(carrying,world,configurations,state,objectId,markerCode,log,liveLiftPose,owner,current);

    private static uint SetObjectAsAttachedToLiftCore(CarryingComponent carrying, BlockWorld world,
        BlockConfigurationManager? configurations, VisionPoseData? state, uint? objectId, MarkerType markerCode,
        Action<string>? log, Pose3d? liveLiftPose, EngineRobot? owner, Func<bool>? current)
    {
        using var frameMutation=world.EnterPoseMutation();
        bool Current()=>owner is null || !owner.Lifetime.Destroying && ReferenceEquals(owner.Engine.Robot,owner) && ReferenceEquals(owner.CarryingOwner,carrying) && (current?.Invoke() ?? true);
        if(!Current())return AttachFailed;
        if(owner is not null)liveLiftPose=owner.LiftPose;
        if (objectId is not { } id) { log?.Invoke("SetObjectAsAttachedToLift: no object id (-1)"); return AttachFailed; }
        if (carrying.IsCarryingObject) { log?.Invoke("SetObjectAsAttachedToLift: already carrying an object"); return AttachFailed; }
        var obj = world.GetLocatedObjectById(id);
        if (obj is null) { log?.Invoke($"SetObjectAsAttachedToLift: no located object {id}"); return AttachFailed; }
        var markers = obj.Markers.Where(m => m.Code == markerCode).ToList();
        if (markers.Count == 0) { log?.Invoke($"SetObjectAsAttachedToLift: object {id} has no marker {markerCode}"); return AttachFailed; }
        if (markers.Count >= 2) log?.Invoke($"SetObjectAsAttachedToLift: object {id} has {markers.Count} markers {markerCode}; using the first");
        if(!Current())return AttachFailed;
        var marker = markers[0];
        if (state is not { } st) { log?.Invoke("SetObjectAsAttachedToLift: no robot pose to take the lift pose from"); return AttachFailed; }
        using var liftLocal=LiftPoseInRobotFrame(st.LiftAngleRad);
        using var liftWorld=st.RobotPose.Compose(liftLocal);
        if(st.RobotPose.HasParent){Pose3d parent=null!;if(st.RobotPose.TryGetParent(ref parent))using(parent)liftWorld.SetParent(parent);}
        // 0x00632E24..0x00632E38 uses the existing live lift node when supplied by DockingSystem.
        using var relativeToLift=obj.NativePose.WithRespectTo(liveLiftPose??liftWorld);
        var objWrtLift = ObjectOnLift(marker, relativeToLift.Rotation);

        // fidelity: M12-008, M13-007 — C10 0x00632F14: tol 15 (0x00632F0C), onTop 1, the DEFAULT filter (0x00632E86..0x00632F02): the current origin
        // only, every family, obj excluded, the same-root predicate, the first match in the walk order (census FIND-ON-TOP-SAME-ROOT-1 C10).
        var top = world.FindObjectOnTopOrUnderneathDefaultFilter(obj, StackToleranceMm, onTop: true, "SetObjectAsAttachedToLift 0x00632F14");
        uint? topId = null; Pose3d? topWrtCarried = null;                    // none found: this+0x14 = -1 (0x0063314C..0x00633150)
        if (top is not null)
        {
            // fidelity: M12-008 — tmp = Pose3d("") (0x00632F76..0x00632F90); static GetWithRespectTo(top.GetPose(), obj.GetPose(), tmp) at 0x00632FB6,
            // non-throwing (M11-066 port), against the objects' own pose nodes. On success GetWithRespectTo has already parented tmp to the
            // carried object's node (0x00845DFA..0x00845DFE).
            var source = SourceLogDispatch.GetSink(world.SourceLog, log);
            var tmp = new Pose3d("");
            if (Pose3d.GetWithRespectTo(top.NativePose, obj.NativePose, tmp))
            {
                source?.Invoke(EngineLogLevel.Info, "Unnamed", "Robot.SetObjectAsAttachedToLift",                 // 0x00632FC0..0x00632FE0
                    FormattableString.Invariant($"Setting object {unchecked((int)top.ObjectId)} on top of carried object as also being carried."));
                tmp.SetParent(obj.NativePose);                                                                     // 0x00633006..0x00633010 (same parent)
                var r = ObjectPoseConfirmerRelative.AddObjectRelativeObservation(world, top, tmp, obj);           // 0x00633014..0x00633020
                if(!Current())return AttachFailed;
                // Dead in the original: AddObjectRelativeObservation's only return is 0 (0x00506DEC). Kept as the engine's shape (0x00633028..0x0063306E).
                if (r != 0) { log?.Invoke($"SetObjectAsAttachedToLift: AddObjectRelativeObservation returned {r}"); return r; }
                // this+0x14 = [top+0x18] (0x0063318C..0x00633190). TopWrtCarried is stack machinery (the engine keeps the relation only in the
                // top's own pose node, parented to the carried object's): it holds the same value as tmp.
                topId = top.ObjectId; topWrtCarried = tmp;
            }
            else
            {
                // 0x00633154..0x0063318A: warn; no AddObjectRelativeObservation; this+0x14 not written (keeps its previous value); the attach continues.
                tmp.Dispose();
                source?.Invoke(EngineLogLevel.Warning, null, "Robot.SetObjectAsAttachedToLift",
                    "Found object on top of carried object, but could not get its pose w.r.t. the carried object.");
                topId = carrying.CarriedOnTopId; topWrtCarried = carrying.TopWrtCarried;
            }
        }
        if(owner is null)carrying.AttachToLiftChecked(world, id, marker, objWrtLift, topId, topWrtCarried);
        else if(!carrying.AttachToLiftChecked(world,id,marker,objWrtLift,topId,topWrtCarried,owner,Current))return AttachFailed;
        if(!Current())return AttachFailed;
        if(world.NativeConfigurations is { } nativeConfigurations)nativeConfigurations.ForceUpdate();
        else Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("M12-008 0x006331BC: native configuration owner unavailable; attachment native-force write skipped");
        if(!Current())return AttachFailed;
        if (configurations is not null) configurations.ForceUpdate = true;
        var argument = liftWorld.Compose(objWrtLift);
        if (liveLiftPose is not null)
        {
            // The existing raw-pose attachment calculation remains M12-008's candidate; the checked live argument has the lift parent before SetPose/broadcast.
            argument = new Pose3d(objWrtLift);
            argument.SetParent(liveLiftPose);
            obj.NativeCarryingTree = true;
        }
        if(!Current())return AttachFailed;
        return ObjectPoseConfirmerRelative.AddLiftRelativeObservation(world, obj, argument);
    }
}

/// <summary>
/// The two <c>ObjectPoseConfirmer</c> entry points <c>SetObjectAsAttachedToLift</c> calls (M12-008). <c>AddObjectRelativeObservation</c> stores the
/// pose it is given, parent included (the top cube's pose with respect to the carried object, parented to the carried object's own node), and
/// unlocalizes the robot when it is localized to that object (<c>[robot+0x2B8] == id</c> to <c>SetLocalizedTo(nullptr)</c>, 0x00506D02..0x00506D0E); the
/// confirmer entry's +0x2C is the observing (carried) object's <c>[+0x1C]</c> (0x00506DDC/0x00506DEA).
/// </summary>
public static class ObjectPoseConfirmerRelative
{
    /// <summary>
    /// <c>BroadcastObjectPoseChanged</c> 0x00506F88 is M12-027 (RECOVERABLE_GAP): whether and how it reaches
    /// <c>BlockWorld::OnObjectPoseChanged</c> 0x00624808 was not read. This counts the calls that would broadcast, so that the
    /// gap is visible and nothing pretends the broadcast happened.
    /// </summary>
    // fidelity: M12-027
    public static int UnreadBroadcasts => Volatile.Read(ref _unreadBroadcasts);
    private static int _unreadBroadcasts;

    /// <summary>
    /// <c>AddObjectRelativeObservation(obj, pose, objSeen)</c> 0x00506CD8: <c>SetLocalizedTo(nullptr)</c> when localized to <paramref name="obj"/>
    /// (0x00506D02..0x00506D0E), <c>obj->SetPose(pose, -1.0f, Dirty)</c> with the pose as given, parent included (vtable +0x28, 0x00506D30..0x00506D3E),
    /// the broadcast (0x00506D48, M12-027), the confirmer entry's pose (+0x18) and time (+0x2C = <paramref name="objSeen"/>'s <c>[+0x1C]</c>); returns 0
    /// (0x00506DEC). It runs on the robot-message thread, so the write goes through <see cref="BlockWorld.ApplyObjectRelativeObservation"/> (the world's
    /// lock, and <see cref="BlockWorld.PoseStateChanged"/> when the state changed).
    /// </summary>
    // fidelity: M12-008
    public static uint AddObjectRelativeObservation(BlockWorld world, ObservableObject obj, Pose3d pose, ObservableObject objSeen)
    {
        world.ApplyObjectRelativeObservation(obj, pose, objSeen);
        Interlocked.Increment(ref _unreadBroadcasts);          // M12-027: 0x00506D48 BroadcastObjectPoseChanged not read
        return 0;
    }

    /// <summary>
    /// <c>AddLiftRelativeObservation</c> 0x00506E5C: <c>obj->SetPose(pose, -1.0f, Known)</c> (0x00506E80..0x00506E94), the broadcast (0x00506E9E, M12-027),
    /// the confirmer entry with count 1 (0x00506ECC) and the pose (+0x18); returns 0. Synchronised as <see cref="AddObjectRelativeObservation"/>.
    /// </summary>
    // fidelity: M12-008
    public static uint AddLiftRelativeObservation(BlockWorld world, ObservableObject obj, Pose3d worldPose)
    {
        world.ApplyLiftObservation(obj, worldPose);
        return 0;
    }
}
