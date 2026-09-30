using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

/// <summary>
/// <c>Anki::PoseState</c>: the engine prints "Known" and "Dirty" (<c>EnumToString(PoseState)</c>); Unknown is the
/// engine's Invalid (0). An object is <b>located</b> (<c>GetLocatedObjectByIdHelper</c> returns it) while it is in the
/// world's located map; the engine never stores Invalid on a located object: <c>SetPoseStateHelper</c> 0x0050612C
/// refuses it and <c>MarkObjectUnknown</c> deletes the object instead (M11-007).
/// </summary>
public enum PoseState { Unknown = 0, Known = 1, Dirty = 2 }

/// <summary>
/// The markerless objects the engine can put in the world without having seen a marker: a proximity
/// obstacle, a cliff, and the collision obstacle an unexpected movement leaves behind.
///
/// <c>MarkerlessObject::GetSizeByType</c> 0x005026EC builds one static map on first use - three entries of
/// sixteen bytes, walked at 0x0050276A until the offset reaches 0x30 - and the sizes are the literals it
/// stores: ProxObstacle (10, 10, 50), CliffDetection (20, 40, 50) and CollisionObstacle
/// (20, 54.2, 67.7), in millimetres.
/// </summary>
public static class MarkerlessObject
{
    public static (float X, float Y, float Z)? SizeByType(ObjectType type) => type switch
    {
        ObjectType.ProxObstacle => (10f, 10f, 50f),
        ObjectType.CliffDetection => (20f, 40f, 50f),
        ObjectType.CollisionObstacle => (20f, 54.2f, 67.7f),
        _ => null,
    };
}

/// <summary>
/// Why a marker or object is not visible: the engine's <c>KnownMarker::NotVisibleReason</c>, with its own
/// names and its own values.
///
/// <c>Anki::Vision::NotVisibleReasonToString</c> 0x0087E96C is one indexed load from a table of nine
/// string pointers, so the enum is read rather than guessed: IS_VISIBLE, CAMERA_NOT_CALIBRATED,
/// POSE_PROBLEM, BEHIND_CAMERA, NORMAL_NOT_ALIGNED, TOO_SMALL, OUTSIDE_FOV, OCCLUDED, NOTHING_BEHIND.
/// This stack had seven values in a different order, which mattered as soon as anything compared them -
/// <c>SearchForBlockHelper::ShouldBeAbleToFindTarget</c> tests the reason against 7, Occluded.
/// </summary>
// fidelity: M11-010
public enum NotVisibleReason
{
    IsVisible = 0,
    CameraNotCalibrated = 1,
    PoseProblem = 2,
    BehindCamera = 3,
    NormalNotAligned = 4,
    TooSmall = 5,
    OutsideFieldOfView = 6,
    Occluded = 7,
    NothingBehind = 8,
}

/// <summary>
/// One entry of the engine's <c>Anki::Vision::OccluderList</c>: a quad in image space and the depth it
/// sits at. <c>BlockWorld::AddAndUpdateObjects</c> 0x006211CC adds one per marker actually observed in
/// the frame through <c>Camera::AddOccluder(KnownMarker)</c> 0x0085E76C, which takes the marker's 3D
/// corners with respect to the camera and projects them; <c>BlockWorld::UpdateObservedMarkers</c>
/// 0x00624F98 clears the list first. So the occluders are what the camera actually saw this frame.
/// </summary>
// fidelity: M11-010
public readonly record struct Occluder(Vec2[] Quad, double DepthMm);

/// <summary>
/// <c>Anki::ObjectID</c> as the engine keeps it process-wide (M11-013). <c>ObjectID::UniqueIDCounter</c> is one 4-byte
/// word in .bss at 0x0105DFF0 (zero at load, no initialiser, GOT slot 0x0103FE30); its only readers and writers are
/// <c>ObjectID::Set</c> 0x008412D8 (reads c, stores c + 1, hands out c: the first ID ever assigned is 0) and
/// <c>ResetObjectIDCounter</c> 0x008412C8, which stores 0 and has no caller in the engine. <c>ObservableObject::SetID</c>
/// 0x004EF468 hands a non-unique type the next counter value and a unique type (<c>IsUnique</c>, vtable +0x5C: ActiveCube and
/// Charger) the value stored for its type in a function-static <c>map&lt;ObjectType, ObjectID&gt;</c>, taking the next counter
/// value and storing it when the type has none. Both are static, so they outlive a Robot and its BlockWorld.
/// </summary>
// fidelity: M11-013
internal static class ObjectIdSpace
{
    private static readonly object Gate = new();
    private static uint s_counter;                                                   // Anki::ObjectID::UniqueIDCounter
    private static readonly Dictionary<ObjectType, uint> s_uniqueIds = new();       // SetID's static map<ObjectType, ObjectID>

    /// <summary><c>ObjectID::Set</c> 0x008412D8: the next value of the counter.</summary>
    internal static uint Set() { lock (Gate) return s_counter++; }

    /// <summary><c>Anki::ResetObjectIDCounter</c> 0x008412C8: the engine has no caller for it; the tests use it.</summary>
    internal static void ResetObjectIDCounter() { lock (Gate) s_counter = 0; }

    /// <summary><c>ObservableObject::SetID</c> 0x004EF468 for an object of <paramref name="type"/>.</summary>
    internal static uint SetIdFor(ObjectType type, bool isUnique)
    {
        lock (Gate)
        {
            if (!isUnique) return s_counter++;
            if (s_uniqueIds.TryGetValue(type, out var id)) return id;
            id = s_counter++;
            s_uniqueIds[type] = id;
            return id;
        }
    }

    /// <summary>The ID the unique-type map holds for a type, or <see cref="ObservableObject.UnassignedId"/> before its first <c>SetID</c>.</summary>
    internal static uint UniqueIdOrUnassigned(ObjectType type)
    {
        lock (Gate) return s_uniqueIds.TryGetValue(type, out var id) ? id : ObservableObject.UnassignedId;
    }

    /// <summary>Test seam: the engine's process starts with a zero counter and an empty map; this puts both back.</summary>
    internal static void ResetForTests()
    {
        lock (Gate) { s_counter = 0; s_uniqueIds.Clear(); }
        BlockWorld.ResetUnconnectedWarningsForTests();
    }

    /// <summary>Test seam: a fixture that needs a known ID for a unique type stores it in the map the way a first <c>SetID</c> would have.</summary>
    internal static void SeedUniqueIdForTests(ObjectType type, uint id)
    {
        lock (Gate) s_uniqueIds[type] = id;
    }
}

/// <summary>
/// One <c>ObjectPoseConfirmer</c> map entry (<c>PoseConfirmation</c>, keyed by ObjectID): the pose it is measured against
/// (+0x18), the pending instance it holds until it is confirmed, the sighting count (+0x24), the miss count (+0x28), the time
/// the pose was last updated (+0x2C, <c>GetLastPoseUpdatedTime</c> 0x0050781E) and the time of the last visual match (+0x30).
/// </summary>
// fidelity: M11-007
internal sealed class PoseConfirmation
{
    public Pose3d Pose = Pose3d.Identity;
    public ObservableObject? Stored;
    public int Count;
    public int Misses;
    public uint LastPoseUpdatedTime;
    public uint LastVisuallyMatchedTime;
}

/// <summary>The engine's <c>BlockWorldFilter</c> origin mode, the byte at filter+0x6D (M11-004).</summary>
public enum OriginMode : byte
{
    /// <summary>0: only the robot's current world origin.</summary>
    InRobotFrame = 0,
    /// <summary>1: everything except the robot's current world origin.</summary>
    NotInRobotFrame = 1,
    /// <summary>2: every origin.</summary>
    InAnyFrame = 2,
    /// <summary>Any other value: the origin must be absent from ignoreOrigins and, when allowedOrigins is not empty, present in it.</summary>
    Custom = 3,
}

/// <summary>
/// <c>BlockWorldFilter</c> as <c>FindLocatedObjectHelper</c> 0x0061EB78 copies and reads it: ignoreIDs +0, allowedIDs +0xC,
/// ignoreTypes +0x18, allowedTypes +0x24, ignoreFamilies +0x30, allowedFamilies +0x3C, ignoreOrigins +0x48, allowedOrigins +0x54,
/// the predicate list +0x60, <c>onlyLatest</c> byte +0x6C and <c>originMode</c> byte +0x6D. An empty allowed set passes everything.
/// </summary>
// fidelity: M11-004
public sealed class BlockWorldFilter
{
    public HashSet<uint> IgnoreIds { get; } = new();
    public HashSet<uint> AllowedIds { get; } = new();
    public HashSet<ObjectType> IgnoreTypes { get; } = new();
    public HashSet<ObjectType> AllowedTypes { get; } = new();
    public HashSet<ObjectFamily> IgnoreFamilies { get; } = new();
    public HashSet<ObjectFamily> AllowedFamilies { get; } = new();
    public HashSet<uint> IgnoreOrigins { get; } = new();
    public HashSet<uint> AllowedOrigins { get; } = new();
    public List<Func<ObservableObject, bool>> Predicates { get; } = new();
    /// <summary>filter+0x6C: only objects observed in the current frame (<c>obj[+0x1C] == BlockWorld[+0x90]</c>).</summary>
    public bool OnlyConsiderLatestUpdate { get; set; }
    /// <summary>filter+0x6D.</summary>
    public OriginMode OriginMode { get; set; } = OriginMode.InRobotFrame;

    /// <summary>The copy constructor <c>0x005C6A88</c> makes before the walk.</summary>
    public BlockWorldFilter Clone()
    {
        var f = new BlockWorldFilter { OnlyConsiderLatestUpdate = OnlyConsiderLatestUpdate, OriginMode = OriginMode };
        f.IgnoreIds.UnionWith(IgnoreIds); f.AllowedIds.UnionWith(AllowedIds);
        f.IgnoreTypes.UnionWith(IgnoreTypes); f.AllowedTypes.UnionWith(AllowedTypes);
        f.IgnoreFamilies.UnionWith(IgnoreFamilies); f.AllowedFamilies.UnionWith(AllowedFamilies);
        f.IgnoreOrigins.UnionWith(IgnoreOrigins); f.AllowedOrigins.UnionWith(AllowedOrigins);
        f.Predicates.AddRange(Predicates);
        return f;
    }
}

/// <summary>
/// The engine's <c>Cozmo::ObservableObject</c> for a light cube or the charger: identity, markers, pose and pose state, and the
/// visibility test the behaviours ask.
/// </summary>
public sealed class ObservableObject
{
    /// <summary>ObjectID -1: what <c>Vision::ObservableObject</c>'s constructor stores at +0x18 (0x0087660E), so a fresh instance has no ID until <see cref="SetID"/> or a match's ID is copied.</summary>
    public const uint UnassignedId = uint.MaxValue;

    public ObservableObject(uint objectId, ObjectType type, IReadOnlyList<KnownMarker> markers)
    {
        ObjectId = objectId; Type = type; Markers = markers;
        Family = FamilyOf(type);
    }

    /// <summary>A fresh instance, as <c>ActiveCube::Clone</c> 0x004E3954 builds one (<c>new ActiveCube(type)</c>): ObjectID -1, ActiveID -1, FactoryID 0.</summary>
    public ObservableObject(ObjectType type, IReadOnlyList<KnownMarker> markers) : this(UnassignedId, type, markers) { }

    internal static ObjectFamily FamilyOf(ObjectType type) =>
        CubeGeometry.IsCube(type) ? ObjectFamily.LightCube
        : type == ObjectType.Charger_Basic ? ObjectFamily.Charger
        : MarkerlessObject.SizeByType(type) is not null ? ObjectFamily.MarkerlessObject
        : ObjectFamily.Unknown;

    /// <summary>The ObjectID (obj+0x18). <see cref="UnassignedId"/> until assigned.</summary>
    public uint ObjectId { get; internal set; }
    public ObjectType Type { get; }
    public ObjectFamily Family { get; }
    public IReadOnlyList<KnownMarker> Markers { get; }
    /// <summary>obj+0x40: the radio slot of the connected cube this object was matched with, -1 by default (ActiveCube ctor 0x004E0C4C, Charger ctor 0x004E9CD4).</summary>
    public int ActiveId { get; internal set; } = -1;
    /// <summary>obj+0x44: the cube's factory ID, 0 by default (0x004E0C58).</summary>
    public uint FactoryId { get; internal set; }
    /// <summary>The pose origin this object's pose is expressed in (the located map's first key). LOCAL: this stack has one origin.</summary>
    public uint OriginId { get; internal set; }
    /// <summary>obj+0x10 <c>fromDistance</c>, stored by <c>SetPose</c>; -1 when set without one.</summary>
    public double FromDistance { get; internal set; } = -1.0;
    /// <summary>vtable +0x0C, <c>IsActive</c>: 1 for an ActiveCube (0x004E385C). NOTE (M11-042): the Charger's value is unread; this stack treats it as passive.</summary>
    public bool IsActive => CubeGeometry.IsActiveObjectType(Type);
    /// <summary>vtable +0x5C, <c>IsUnique</c>: 1 for ActiveCube (0x004E3882) and Charger (0x004E3883), 0 in the base (0x004E02AE).</summary>
    public bool IsUnique => CubeGeometry.IsCube(Type) || Type == ObjectType.Charger_Basic;
    /// <summary>Pose in the robot's world origin; meaningful while <see cref="PoseState"/> is not Unknown.</summary>
    public Pose3d Pose { get; internal set; } = Pose3d.Identity;
    public PoseState PoseState { get; internal set; } = PoseState.Unknown;
    public bool IsLocated => PoseState != PoseState.Unknown;
    /// <summary>Robot timestamp of the frame that last observed the object (obj+0x1C).</summary>
    public uint LastObservedTimestamp { get; internal set; }

    /// <summary>
    /// Whether the cube is reporting itself in motion: true between an <c>ObjectMoved</c> and the
    /// <c>ObjectStoppedMoving</c> that ends it. The engine asks the object this through a virtual on
    /// <c>ObservableObject</c> - <c>PickupObjectAction::Verify</c> 0x00553C8E calls it - so it lives on
    /// the object here too rather than in a tracker beside it.
    /// </summary>
    public bool IsMoving { get; internal set; }
    public int TimesObserved { get; internal set; }

    /// <summary>The <c>ObjectPoseConfirmer</c> entry for this object's ID, or null while it has none. <see cref="BlockWorld"/> links and unlinks it.</summary>
    internal PoseConfirmation? Confirmation { get; set; }

    /// <summary>
    /// M11-007 / C3.4: the <c>ObjectPoseConfirmer</c> sighting count (entry+0x24). A new entry starts at 1
    /// (<c>PoseConfirmation</c> ctor 0x5062E0 stores 1 at +0xc); a matching second sighting increments it
    /// (<c>AddVisualObservation</c> 0x506A04..0x506A26); a mismatching sighting resets it to 1
    /// (0x506A46..0x506A4E); <c>MarkObjectUnobserved</c> zeroes it (0x00506FE0). <see cref="IsPoseConfirmed"/> is <c>count &gt; 1</c>
    /// (<c>IsReferencePoseConfirmed</c> 0x506340).
    /// </summary>
    // fidelity: M11-007
    public int PoseConfirmationCount
    {
        get => Confirmation?.Count ?? 0;
        internal set => (Confirmation ??= new PoseConfirmation()).Count = value;
    }
    /// <summary>The pose the confirmation count is measured against (the entry's stored pose, entry+0x18).</summary>
    public Pose3d ReferencePose
    {
        get => Confirmation?.Pose ?? Pose3d.Identity;
        internal set => (Confirmation ??= new PoseConfirmation()).Pose = value;
    }
    /// <summary><c>IsReferencePoseConfirmed</c> 0x506340: <c>count &gt; 1</c>.</summary>
    public bool IsPoseConfirmed => PoseConfirmationCount > 1;
    /// <summary>The confirmer's miss count (entry+0x28): consecutive <c>MarkObjectUnobserved</c> calls since the last visual match.</summary>
    public int UnobservedCount
    {
        get => Confirmation?.Misses ?? 0;
        internal set => (Confirmation ??= new PoseConfirmation()).Misses = value;
    }
    /// <summary>The marker codes seen in the last observation.</summary>
    public IReadOnlyList<MarkerType> LastObservedMarkers { get; internal set; } = Array.Empty<MarkerType>();
    /// <summary>Bounding box of the last observation in the image, for <c>RobotObservedObject.img_rect</c>.</summary>
    public (double X, double Y, double Width, double Height)? LastImageRect { get; internal set; }
    public double LastReprojectionRmsPx { get; internal set; }

    /// <summary><c>ObservableObject::SetID()</c> 0x004EF468 (vtable +0x24): a non-unique type takes the next counter value, a unique type the one stored for its type.</summary>
    // fidelity: M11-013
    public void SetID() => ObjectId = ObjectIdSpace.SetIdFor(Type, IsUnique);

    /// <summary>
    /// <c>ObservableObject::SetPose(pose, fromDistance, poseState)</c> (vtable +0x28, body 0x004EF580/0x004EF590): stores the pose, the
    /// distance and the state byte at +0x24 (0x004EF5A8). <c>InitPose(pose, state)</c> 0x004EF562 is this with fromDistance -1.0.
    /// </summary>
    internal void SetPose(Pose3d pose, double fromDistance, PoseState state)
    {
        Pose = pose; FromDistance = fromDistance; PoseState = state;
    }

    /// <summary>
    /// <c>Clone</c> (vtable +0x00). <c>ActiveCube::Clone</c> 0x004E3954 is a fresh <c>ActiveCube(type)</c>, not a copy: no ID, no
    /// pose, no ActiveID/FactoryID. M11-042 (visible): the Clone bodies of Charger and of the non-active blocks are unread; the
    /// same construction is used for the Charger, which is UNVERIFIED.
    /// </summary>
    // fidelity: M11-042
    public ObservableObject Clone() => new(Type, Markers);

    /// <summary>
    /// The engine's <c>ObservableObject::GetTopFaceOrientation</c>-style up axis: the object axis nearest world +Z.
    /// </summary>
    public UpAxis UpAxisFromPose()
    {
        var r = Pose.Rotation;
        // world Z expressed in the object frame is the third row of R (R^T * ez)
        var zInObj = new Vec3(r[2, 0], r[2, 1], r[2, 2]);
        double ax = Math.Abs(zInObj.X), ay = Math.Abs(zInObj.Y), az = Math.Abs(zInObj.Z);
        if (az >= ax && az >= ay) return zInObj.Z > 0 ? UpAxis.ZPositive : UpAxis.ZNegative;
        if (ax >= ay) return zInObj.X > 0 ? UpAxis.XPositive : UpAxis.XNegative;
        return zInObj.Y > 0 ? UpAxis.YPositive : UpAxis.YNegative;
    }

    /// <summary>
    /// Whether the object is sitting squarely on one of its faces, within <paramref name="toleranceRad"/>.
    ///
    /// <c>ObservableObject::IsRestingFlat</c> 0x0087751C takes the object's rotation matrix, asks
    /// <c>GetRotatedParentAxis&lt;'Z'&gt;</c> which parent axis its own Z lies closest to and how closely
    /// (the dot product), and returns <c>acos(|dot|) &lt; tolerance</c>. The absolute value is what makes
    /// a cube resting on any of its six faces count: only one balanced on an edge or a corner fails.
    /// </summary>
    public bool IsRestingFlat(double toleranceRad)
    {
        var r = Pose.Rotation;
        // the object's own Z in the parent frame is the third column of R
        double x = Math.Abs(r[0, 2]), y = Math.Abs(r[1, 2]), z = Math.Abs(r[2, 2]);
        double dot = Math.Max(z, Math.Max(x, y));
        return Math.Acos(Math.Min(1.0, dot)) < toleranceRad;
    }

    /// <summary>
    /// <c>ObservableObject::IsVisibleFrom(camera, maxFaceNormalAngle, minMarkerImageSize, xBorderPad,
    /// yBorderPad, bool&amp; hasNothingBehind)</c> 0x00876774: any of the object's markers passes
    /// <c>KnownMarker::IsVisibleFrom</c>, and the out-flag is set whenever a marker comes back
    /// <see cref="NotVisibleReason.NothingBehind"/> - the overload's <c>requireSomethingBehind</c> is
    /// true (<c>movs r4, #1</c> at 0x0087679C).
    /// </summary>
    public bool IsVisibleFrom(CameraModel camera, double maxFaceNormalAngleRad, double minMarkerImageSizePx,
                              double xPad, double yPad, out NotVisibleReason reason, out bool hasNothingBehind)
    {
        reason = NotVisibleReason.PoseProblem;
        hasNothingBehind = false;
        var worst = NotVisibleReason.PoseProblem;
        foreach (var m in Markers)
        {
            var r = MarkerVisibility(m, camera, maxFaceNormalAngleRad, minMarkerImageSizePx, xPad, yPad,
                                     requireSomethingBehind: true);
            if (r == NotVisibleReason.NothingBehind) hasNothingBehind = true;
            if (r == NotVisibleReason.IsVisible) { reason = r; return true; }
            if (r > worst) worst = r;
        }
        reason = worst;
        return false;
    }

    /// <summary>
    /// <c>ObservableObject::IsVisibleFrom(camera, maxFaceNormalAngle, minMarkerImageSize,
    /// requireSomethingBehind, xBorderPad, yBorderPad)</c> 0x00876678, where the caller chooses. Only the
    /// out-parameter overload above hard-codes the flag, and only CheckForUnobservedObjects uses that
    /// one; a behaviour asking whether it can see a cube asks this.
    /// </summary>
    public bool IsVisibleFrom(CameraModel camera, double maxFaceNormalAngleRad, double minMarkerImageSizePx,
                              double xPad, double yPad, out NotVisibleReason reason,
                              bool requireSomethingBehind = false)
    {
        reason = NotVisibleReason.PoseProblem;
        var worst = NotVisibleReason.PoseProblem;
        foreach (var m in Markers)
        {
            var r = MarkerVisibility(m, camera, maxFaceNormalAngleRad, minMarkerImageSizePx, xPad, yPad,
                                     requireSomethingBehind);
            if (r == NotVisibleReason.IsVisible) { reason = r; return true; }
            if (r > worst) worst = r;
        }
        reason = worst;
        return false;
    }

    public bool IsVisibleFrom(CameraModel camera, double maxFaceNormalAngleRad = 0.785398, double minMarkerImageSizePx = 10, double pad = 0)
        => IsVisibleFrom(camera, maxFaceNormalAngleRad, minMarkerImageSizePx, pad, pad, out _);

    /// <summary>
    /// <c>KnownMarker::IsVisibleFrom</c> 0x0087E4A8 for one marker, in the engine's own order: pose, then
    /// the normal (4), then the projected size (5), then the field of view (6), then the occluders (7),
    /// and finally, when <paramref name="requireSomethingBehind"/> is set and nothing in the camera's
    /// occluder list lies behind the marker's quad, NOTHING_BEHIND (8, at 0x0087E87A). The depth it asks
    /// <c>IsAnythingBehind</c> about is the mean of the four corner depths (0x0087E850..0x0087E864).
    /// </summary>
    public NotVisibleReason MarkerVisibility(KnownMarker m, CameraModel camera, double maxFaceNormalAngleRad,
                                             double minMarkerImageSizePx, double xPad, double yPad,
                                             bool requireSomethingBehind = false)
    {
        var corners = m.CornersInWorld(Pose);
        var centre = (corners[0] + corners[1] + corners[2] + corners[3]) / 4;
        var normal = (Pose.Rotation * m.NormalOnObject).Normalized();
        var toCamera = (camera.Pose.Translation - centre);
        if (toCamera.Length < 1e-9) return NotVisibleReason.BehindCamera;
        double cos = normal.Dot(toCamera.Normalized());
        if (Math.Acos(Math.Clamp(cos, -1, 1)) > maxFaceNormalAngleRad) return NotVisibleReason.NormalNotAligned;
        var px = new Vec2[4];
        for (int i = 0; i < 4; i++)
        {
            var p = camera.Project(corners[i]);
            if (p is null) return NotVisibleReason.BehindCamera;
            px[i] = p.Value;
        }
        // projected size: sqrt of the quad's area (the engine takes sqrt of the projected area)
        var q = new[] { px[0], px[2], px[3], px[1] };
        double area = 0;
        for (int i = 0; i < 4; i++) area += q[i].X * q[(i + 1) % 4].Y - q[(i + 1) % 4].X * q[i].Y;
        if (Math.Sqrt(Math.Abs(area) / 2) < minMarkerImageSizePx) return NotVisibleReason.TooSmall;
        foreach (var p in px)
            if (p.X < -xPad || p.Y < -yPad || p.X > camera.Calibration.Columns + xPad || p.Y > camera.Calibration.Rows + yPad)
                return NotVisibleReason.OutsideFieldOfView;

        var depths = new double[4];
        for (int i = 0; i < 4; i++) depths[i] = camera.ToCamera(corners[i]).Z;
        for (int i = 0; i < 4; i++)
            if (camera.Occluders.IsOccluded(px[i], depths[i])) return NotVisibleReason.Occluded;

        if (requireSomethingBehind)
        {
            double mean = (depths[0] + depths[1] + depths[2] + depths[3]) * 0.25;
            if (!camera.Occluders.IsAnythingBehind(px, mean)) return NotVisibleReason.NothingBehind;
        }
        return NotVisibleReason.IsVisible;
    }

    public override string ToString() => $"object {ObjectId} {Type} {PoseState} at {Pose.Translation} yaw={Pose.AngleAroundZ * 180 / Math.PI:F0}deg";
}

/// <summary>What one frame did to the world model, for logs and tests.</summary>
public sealed record ObjectObservation(ObservableObject Object, uint Timestamp, IReadOnlyList<MarkerType> Markers, Pose3d PreviousPose, PoseState PreviousState, bool IsNew, double RmsPx);

/// <summary>What <c>BlockWorld::UpdateObservedMarkers</c> produced for one frame.</summary>
public sealed record BlockWorldFrameResult(IReadOnlyList<ObjectObservation> Objects, IReadOnlyList<ObservableObject> Forgotten);

/// <summary>One <c>PoseChange</c> list node (BlockWorld+0x68): the object's ID, its OLD pose (+0x10) and its OLD pose state (+0x1C).</summary>
internal readonly record struct PoseChange(uint Id, Pose3d Pose, PoseState State);

/// <summary>
/// The engine's <c>BlockWorld</c> for light cubes and the charger: the located-object map, the connected-object map, the
/// <c>ObjectPoseConfirmer</c> (M11-007) and the per-frame update <c>UpdateObservedMarkers</c> (M11-037).
///
/// A marker observation does not make an object. <c>CreateObjectsFromMarkers</c> builds an instance per cluster with a fresh
/// ObjectID (-1) and PoseState Dirty; <c>AddAndUpdateObjects</c> gives it the ID of the object it matches or a fresh one
/// (<c>SetID</c>, M11-013) and hands it to the confirmer, whose first sighting only records it and whose second matching sighting
/// decides Known or Dirty (<c>UpdatePoseInInstance</c>) and only then puts it in the located map (<c>AddLocatedObject</c>). Two
/// consecutive frames in which an object should have been seen and was not delete it and the objects stacked on it.
///
/// NATIVE rules transcribed: an unconnected observed active object warns with a 10 s cooldown and is kept (H1); unobserved checks
/// are skipped while the robot was moving (the <c>IS_MOVING</c> status bit) or rotating faster than 0.174533 rad/s (head
/// <c>rateY</c> / body <c>rateZ</c> from the ImuDataHistory). Pose clustering across an object's markers uses the engine's own
/// tolerances, 5 mm and 5 degrees.
///
/// Built: <c>PotentialObjectsForLocalizingTo</c> (<c>Insert</c>, <c>UseDiscardedObservation</c>, <c>CouldUseObjectForLocalization</c>), which is what refreshes a
/// confirmed object's pose (M11-044). Not built, each visible where it would run: <c>LocalizeRobot</c>'s <c>LocalizeToObject</c> and <c>Rejigger</c> (counted, M11-044),
/// the collision dirty pass (<c>CheckForCollisionWithRobot</c>'s vtable +0x58, M11-042), the game-side broadcasts (no EngineToGame message types in this stack, M11-038).
/// </summary>
public sealed class BlockWorld
{
    /// <summary><c>CheckForUnobservedObjects</c>'s rotation gate: 0.174533 rad/s (10 deg/s).</summary>
    // fidelity: M11-004
    public const double MaxRotationRateRadPerSec = 0.174533;

    /// <summary>
    /// 5 mm. <c>ObservableObjectLibrary::CreateObjectsFromMarkers</c> passes it to
    /// <c>ClusterObjectPoses(poses, object, distThreshold, angleThreshold, clusters)</c> as the third
    /// argument: <c>0x40A00000</c> built at 0x006254F4.
    /// </summary>
    // fidelity: M11-006
    public const double ClusterDistanceMm = 5.0;

    /// <summary>
    /// 0.0872665 rad, 5 degrees: the angle threshold of the same call, built from <c>0x3DB2B8C3</c> at
    /// 0x00625498 through the <c>Radians</c> constructor at 0x006254E0.
    /// </summary>
    // fidelity: M11-006
    public const double ClusterAngleRad = 0.0872665;

    /// <summary>
    /// 0.349066 rad, 20 degrees: the angle <c>ObjectPoseConfirmer::UpdatePoseInInstance</c> passes to
    /// <c>ObservableObject::ClampPoseToFlat</c> (<c>0x3EB2B8C2</c> at 0x00505F16) for the docking object.
    /// </summary>
    // fidelity: M11-006
    public const double FlatClampAngleRad = 0.349066;

    /// <summary>
    /// The angle <c>CreateObjectsFromMarkers</c> clamps an active instance's pose to: <c>Radians(vtable[+0x1C]() * 0.0174533)</c>
    /// (0x00625566), and <c>vtable[+0x1C]</c> returns 5.0 (0x004E024E..0x004E0250), i.e. 5 degrees; the clamp runs only when the
    /// instance is active (row 1.6, 0x00625578).
    /// </summary>
    // fidelity: M11-013
    // NOT SOURCED as a bit pattern: the decimal 0.0174533 as the C# has always evaluated it (double 0x3F91DF4722D4405F); the record cites no literal address for it here.
    private static readonly double DegToRadDecimal = BitConverter.Int64BitsToDouble(0x3F91DF4722D4405FL);
    public static readonly double CreationFlatClampAngleRad = 5.0 * DegToRadDecimal;

    /// <summary>
    /// M11-004 / C3.3: the object-match translation factor, 0.8 (thunk 0x4E025C, literal 0x3F4CCCCD at
    /// 0x4E028C). The thunk multiplies the object's stored extent (<c>+0x88</c>) by it, so a light cube's
    /// tolerance is <c>(35.2, 35.2, 35.2)</c> mm.
    /// </summary>
    // fidelity: M11-004
    public const double ObjectMatchDistanceFactor = 0.8;

    /// <summary>M11-004 / C3.3: the object-match rotation tolerance, pi/4 (thunk 0x4E0290, 0x3F490FDB).</summary>
    // fidelity: M11-004
    public const double ObjectMatchAngleRad = Math.PI / 4;
    /// <summary>The visibility angle the world model and the cube-moved strategy use: 0.785398 rad (45 deg).</summary>
    public const double VisibilityNormalAngleRad = 0.785398;

    /// <summary>
    /// <c>ObservableObject::GetMaxLocalizationDistance_mm</c> 0x004EF460: 250.0 (<c>0x437A0000</c>). <c>UpdatePoseInInstance</c> compares
    /// the observation distance with it plus 1e-5 (literal 0x3727C5AC at 0x00506024) in float32.
    /// </summary>
    // fidelity: M11-007
    public const double MaxLocalizationDistanceMm = 250.0;

    /// <summary>15.0: the tolerance <c>UpdatePoseOfStackedObjects</c> 0x00621908, <c>MarkObjectDirty</c> 0x0050765A and <c>MarkObjectUnknown</c> 0x00507258 pass to <c>FindObjectOnTopOrUnderneathHelper</c>.</summary>
    // fidelity: M11-037
    public const double StackToleranceMm = 15.0;

    /// <summary>
    /// The miss count at which <c>MarkObjectUnobserved</c> forgets an object: it acts when the count it read was already at least 1
    /// (<c>cmp r3, #1 / blt</c> at 0x00506FDE), i.e. on the second consecutive miss (M11-007).
    /// </summary>
    // fidelity: M11-007
    public int UnobservedMissesToUnknown { get; set; } = 2;

    /// <summary>
    /// 40 pixels: what <c>BlockWorld::CheckForUnobservedObjects</c> 0x006220F0 passes as the minimum
    /// projected marker size (0x42200000), alongside a face-normal angle of 0.785398.
    /// </summary>
    // fidelity: M11-008
    public double MinVisibleMarkerSizePx { get; set; } = 40;
    /// <summary>Markers whose pose solve leaves more than this reprojection error are ignored (LOCAL, 3 px).</summary>
    public double MaxReprojectionRmsPx { get; set; } = 3.0;

    /// <summary>
    /// The pose origin the robot is in (<c>[[Robot+0x294]+0x10]</c>, the id origin mode 0 compares with). LOCAL: this stack keeps every
    /// pose in the robot's one world origin (<see cref="Pose3d"/>), so objects are created in this origin and only
    /// <see cref="UpdateObjectOrigins"/> ever moves an object out of it.
    /// </summary>
    // fidelity: M11-004
    public uint CurrentOriginId { get; set; } = 1;

    private readonly object _gate = new();
    /// <summary>BlockWorld+0x3C: <c>map&lt;origin, map&lt;family, map&lt;type, map&lt;ObjectID, shared_ptr&gt;&gt;&gt;&gt;</c>, walked in ascending key order.</summary>
    private readonly SortedDictionary<(uint Origin, int Family, int Type, int Id), ObservableObject> _located = new();
    /// <summary>BlockWorld+0x30: <c>map&lt;family, map&lt;type, map&lt;ObjectID, shared_ptr&gt;&gt;&gt;</c> of connected active objects.</summary>
    private readonly SortedDictionary<(int Family, int Type, int Id), ObservableObject> _connectedObjects = new();
    /// <summary>
    /// LOCAL: objects the stack's own <see cref="MarkUnknown"/> and <see cref="OnRobotDelocalized"/> forgot (state Unknown) but whose
    /// reference callers still ask for by ID. They are not in the located map, so no engine query sees them.
    /// </summary>
    private readonly Dictionary<uint, ObservableObject> _forgotten = new();
    /// <summary>The <c>ObjectPoseConfirmer</c>'s <c>map&lt;ObjectID, PoseConfirmation&gt;</c> (Robot+0x26C).</summary>
    private readonly SortedDictionary<int, PoseConfirmation> _confirmations = new();

    // per-frame BlockWorld fields
    private uint _markerTimestamp;              // this+0x90: the first marker's timestamp, 0 for an empty list
    private bool _appendPoseChanges;            // this+0x74
    private bool _didObjectsChange;             // this+0x54
    private uint _lastImageTimestamp;           // Robot::GetLastImageTimeStamp
    private readonly List<PoseChange> _poseChanges = new();   // this+0x68

    /// <summary>
    /// M11-004: the native <c>static unordered_map&lt;int,float&gt;</c> at 0x0105B388 that rate-limits the "not connected"
    /// warning, keyed by the observed object's ID. It is a function-static, so it is shared by every BlockWorld in the process.
    /// The value is the time until which the warning is suppressed (now + 10.0, the literal at 0x00620B1E).
    /// </summary>
    private static readonly Dictionary<int, float> s_unconnectedWarnUntil = new();
    private static readonly object s_unconnectedWarnGate = new();
    private const double UnconnectedWarnCooldownSec = 10.0;

    internal static void ResetUnconnectedWarningsForTests()
    {
        lock (s_unconnectedWarnGate) s_unconnectedWarnUntil.Clear();
    }

    /// <summary>Hooks to state the engine reads from other components. Each is null until the owner wires it; the doc names the engine field.</summary>
    /// <summary><c>CarryingComponent::IsCarryingObject(id)</c> (Robot+0x284). Unwired means not carrying.</summary>
    public Func<uint, bool>? IsCarryingObject { get; set; }
    /// <summary><c>Robot+0x355 == OnTreads</c> (OffTreadsState 0). Unwired means OnTreads, the engine's zero default.</summary>
    public Func<bool>? OnTreads { get; set; }
    /// <summary>The ObjectID at <c>[Robot+0x280]+0xC</c> (the DockingComponent's object, name unread). Unwired means none.</summary>
    public Func<uint?>? DockingObjectId { get; set; }
    /// <summary><c>CarryingComponent::UnSetCarryObject(id)</c>, called by <c>UpdatePoseInInstance</c> 0x00505F5A..0x00505FBC. Unwired: the call is logged and counted, not made.</summary>
    public Action<uint>? UnSetCarryObject { get; set; }
    /// <summary>The <c>BlockConfigurationManager</c> byte [+0xC] set to 1 by <c>UpdateObjectOrigins</c> (0x006206EA). Unwired: the call is counted.</summary>
    public Action? BlockConfigurationManagerForceUpdate { get; set; }

    /// <summary>Counts of the calls made to an unwired hook or an unbuilt engine function, so that nothing pretends they happened.</summary>
    public int UnwiredCarryUnsetCalls { get; private set; }
    public int UnbuiltBroadcastLocatedObjectStatesCalls { get; private set; }
    public int UnbuiltDeleteIntersectingObjectsCalls { get; private set; }
    public int UnwiredForceUpdateCalls { get; private set; }
    /// <summary>
    /// How many times <c>LocalizeRobot</c> ran (once per <c>AddAndUpdateObjects</c>). <c>LocalizeToObject</c> and <c>Rejigger</c> exist here only as the <c>SetLocalizedTo</c>
    /// field writes (M11-044).
    /// </summary>
    public int UnbuiltLocalizeRobotCalls { get; internal set; }
    /// <summary>How many times <c>LocalizeToObject</c> ran with only its <c>SetLocalizedTo</c> writes: the robot pose and history update, Rejigger and the origin updates were not (M11-044).</summary>
    public int UnbuiltLocalizeToObjectCalls { get; internal set; }

    // ---- state the localization candidates (PotentialObjectsForLocalizingTo) read from the robot; each is null until the owner wires it (M11-044)
    /// <summary>
    /// <c>Robot+0x2B8</c>: the ObjectID the robot is localized to, null for none (-1). Written by <see cref="SetLocalizedTo"/> (an object's ID, 0x00512458; -1 for null, 0x005124F6) and,
    /// for OnTreads, by the tread-state commit (M10, +0x2B8 = -1).
    /// </summary>
    public uint? LocalizedToObjectId { get { lock (_gate) return _localizedTo == ObservableObject.UnassignedId ? null : _localizedTo; } }
    /// <summary><c>Robot+0x2BC</c>: the robot has moved since it last localized. <see cref="SetLocalizedTo"/> writes 0 for an object (0x00512468); <see cref="NoteRobotState"/> ORs in 1 (0x00512B56..0x00512B8E). 0 from construction (0x0050FF0C).</summary>
    public bool RobotMovedSinceLocalized { get { lock (_gate) return _movedSinceLocalized != 0; } }
    private uint _localizedTo = ObservableObject.UnassignedId;
    private byte _movedSinceLocalized;

    /// <summary>
    /// <c>Robot+0x2C4</c>: written 1 by <see cref="SetLocalizedTo"/> (0x0051245E, 0x005124EE) and the OnTreads commit (0x0051217C), 0 by <c>Delocalize</c> (0x00510A24). The engine has native readers
    /// (<c>LocalizeToObject</c> 0x0051570A, <c>LocalizeToMat</c> 0x00515F64, <c>GetRobotState</c> 0x00518210, <c>CubeLightComponent::Update</c> 0x00637930); none of them is built here, so nothing in this stack reads it yet.
    /// </summary>
    public byte Robot2C4 { get { lock (_gate) return _robot2C4; } }
    /// <summary><c>Robot+0x2C5</c>: written 0 by <c>Delocalize</c>. Nothing in this stack reads it.</summary>
    public byte Robot2C5 { get { lock (_gate) return _robot2C5; } }
    /// <summary>
    /// <c>Robot+0x2C8</c>: written -1.0f by <c>Delocalize</c>; <see cref="SetLocalizedTo"/> stores the minimum, over the object's markers, of the squared length of the marker pose's translation in the camera
    /// frame (0x005123AE..0x00512456, 0x0050240A..0x00512428: skip when [node+0x50] &lt; r8; skip when the stored value is >= 0 and d2 >= it). That store is NOT built (it needs the marker-pose-to-camera
    /// transform this stack does not have; counted in <see cref="UnbuiltSetLocalizedToMarkerLoops"/>). The engine has no reader of +0x2C8.
    /// </summary>
    public float Robot2C8 { get { lock (_gate) return _robot2C8; } }
    /// <summary><c>Robot+0x2C0</c>: the frame-id-mismatch counter of <c>UpdateFullRobotState</c> (M11-053: incremented on a state whose frame id differs from Robot+0x2B0, Delocalize at 101; not built here). The tread-boundary trigger writes 0 (0x00512B88..0x00512B92) before it calls Delocalize.</summary>
    public byte Robot2C0 { get { lock (_gate) return _robot2C0; } }
    private byte _robot2C0;
    // The Robot constructor calls Delocalize (M4-020), so these start where Delocalize leaves them.
    private byte _robot2C4;
    private byte _robot2C5;
    private float _robot2C8 = -1.0f;

    /// <summary>
    /// The marker loop of <c>SetLocalizedTo</c> (0x005123AE..0x00512456): for each marker of the object <c>GetWithRespectTo(marker pose, [Robot+0x258]+0x30)</c> (the camera pose); false when any
    /// of those calls fails. This stack has no pose tree, so the loop is not built: unwired it is treated as SUCCEEDING, counted in <see cref="UnbuiltSetLocalizedToMarkerLoops"/>. The wrong
    /// direction of that stand-in: a call the engine would fail (marker and camera poses in different origins) succeeds here and the fields are written where the engine writes nothing. The
    /// running minimum of squared marker distance the loop stores into Robot+0x2C8 is recovered (see <see cref="Robot2C8"/>) but not built for the same reason.
    /// </summary>
    public Func<ObservableObject, bool>? MarkerPosesRelativeToCamera { get; set; }
    /// <summary>How many times <see cref="SetLocalizedTo"/> ran an object through it without <see cref="MarkerPosesRelativeToCamera"/> (the loop was not run; M11-044).</summary>
    public int UnbuiltSetLocalizedToMarkerLoops { get; private set; }

    /// <summary>
    /// <c>Robot::SetLocalizedTo(obj)</c> 0x0051238C. An object whose ID is -1 is the IdNotSet error (0x005123A0 -> 0x005124FC): returns 1 and writes nothing. Otherwise the marker loop
    /// (0x005123AE..0x00512456, see <see cref="MarkerPosesRelativeToCamera"/>): a failed marker call is an error (0x0051254E) returning 1 with NO writes; then Robot+0x2B8 = the object's ID
    /// (0x00512458), Robot+0x2BC = 0 (0x00512468) and Robot+0x2C4 = 1 (0x0051245E). Null: Robot+0x2B8 = -1 (0x005124F6) and Robot+0x2C4 = 1 (0x005124EE), Robot+0x2BC untouched. Returns
    /// false where the engine returns 1. NOT BUILT: the running minimum stored into Robot+0x2C8 by the loop (0x00512442; see <see cref="Robot2C8"/>), the robot-side text,
    /// <c>AIComponent::OnRobotRelocalized</c> (0x0051246C) and the visualizer call.
    /// </summary>
    // fidelity: M11-044
    public bool SetLocalizedTo(ObservableObject? obj)
    {
        lock (_gate)
        {
            if (obj is null) { _localizedTo = ObservableObject.UnassignedId; _robot2C4 = 1; return true; }
            if (obj.ObjectId == ObservableObject.UnassignedId) { Log?.Invoke("Robot.SetLocalizedTo.IdNotSet"); return false; }
            if (MarkerPosesRelativeToCamera is { } loop)
            {
                if (!loop(obj))
                {
                    Log?.Invoke($"Robot.SetLocalizedTo: a marker pose of object {obj.ObjectId} could not be expressed relative to the camera; not localized");   // log text not sourced
                    return false;
                }
            }
            else UnbuiltSetLocalizedToMarkerLoops++;
            _localizedTo = obj.ObjectId;
            _movedSinceLocalized = 0;
            _robot2C4 = 1;
            return true;
        }
    }

    /// <summary>
    /// The robot-field writes of <c>Robot::Delocalize</c> 0x00510A24, which <c>UpdateFullRobotState</c> calls at 0x00512BA6 right after the Robot+0x2BC OR: Robot+0x2B8 = -1 (0x00510A46),
    /// +0x2C4 = 0, +0x2C8 = -1.0f, +0x2C5 = 0. It does not clear the state history and does not touch +0x2BC.
    /// </summary>
    // fidelity: M11-044
    internal void DelocalizeRobotFields()
    {
        lock (_gate) { _localizedTo = ObservableObject.UnassignedId; _robot2C4 = 0; _robot2C8 = -1.0f; _robot2C5 = 0; }
    }

    /// <summary>
    /// <c>BlockWorld::AnyRemainingLocalizableObjects()</c> 0x006270F4 -> 0x00626EF4: a filter whose origin mode byte is 3 (0x00626FAC; Custom) with no origin added for the no-argument overload
    /// (0x00626FB0), so EVERY origin; one predicate, the object's vtable+0x18 (<see cref="CanBeUsedForLocalization"/>, the slot <c>CouldUseObjectForLocalization</c> uses at 0x0050D17C);
    /// <c>FindLocatedObjectHelper(filter, empty modify, returnFirst = 1)</c> (0x00626FD2); the result is normalised to 0/1 (0x0062704C).
    /// </summary>
    // fidelity: M11-044
    public bool AnyRemainingLocalizableObjects()
    {
        var f = new BlockWorldFilter { OriginMode = (OriginMode)3 };
        f.Predicates.Add(CanBeUsedForLocalization);
        return FindLocatedObjectHelper(f, null, true) is not null;
    }

    /// <summary>
    /// <c>UpdateFullRobotState</c>'s tread-boundary Delocalize (0x00512B88..0x00512BA6, M11-044): Robot+0x2C0 = 0 and then <c>Robot::Delocalize</c>, whose robot fields and object handling are
    /// <see cref="OnRobotDelocalized"/>. NOT modelled (M11-053): the argument <c>(status &amp; 2) != 0</c>, which only gates a warning when it differs from ([[Robot+0x284]+8] != -1) (0x00510C6A..0x00510C98); the carried-object move, which the engine gates by [[+0x284]+8] != -1 (loop 0x00510CF0; here the carried set given by the caller decides); and the origin allocation (AddNewOrigin), so the RobotDelocalized event carries the OLD origin id.
    /// </summary>
    // fidelity: M11-044
    internal IReadOnlyList<ObservableObject> DelocalizeOnTreadBoundary(IReadOnlySet<uint>? carriedObjectIds = null)
    {
        lock (_gate) _robot2C0 = 0;
        return OnRobotDelocalized(carriedObjectIds);
    }

    /// <summary>
    /// The stores <c>CheckAndUpdateTreadsState</c> makes when nothing localizable remains after the commit to OnTreads: Robot+0x2C4 = 1 (0x0051217C), Robot+0x2B8 = -1 (0x00512184). The caller
    /// (the M10 classifier, 0x005120F6..0x00512188) runs them only when <see cref="AnyRemainingLocalizableObjects"/> is false; Robot+0x2BC is untouched.
    /// </summary>
    // fidelity: M11-044
    internal void ClearLocalizationOnTreads()
    {
        lock (_gate) { _robot2C4 = 1; _localizedTo = ObservableObject.UnassignedId; }
    }

    /// <summary>
    /// The store <c>Robot::UpdateFullRobotState</c> makes to Robot+0x2BC for every robot state (0x00512B56..0x00512B8E): it ORs in 1 when <c>MovementComponent</c> +0xA (head NOT in
    /// position) or +0xC (wheels moving) is non-zero, and otherwise ORs in whether the OffTreads byte (Robot+0x355) is not OnTreads.
    /// </summary>
    // fidelity: M11-044
    public void NoteRobotState(bool movementBytesNonZero, bool notOnTreads)
    {
        lock (_gate) _movedSinceLocalized |= (byte)(movementBytesNonZero || notOnTreads ? 1 : 0);
    }

    /// <summary><c>Robot::ShouldIgnoreMovementDueToDoubleTap</c> (BlockTapFilterComponent), which <c>LocalizeToObject</c> asks first (0x0051551A..0x00515580). Unwired: false.</summary>
    public Func<uint, bool>? ShouldIgnoreMovementDueToDoubleTap { get; set; }

    /// <summary>
    /// <c>Robot::LocalizeToObject(observed, existing)</c> 0x005154B0, the part this stack has. Preconditions (0x005154C0..0x00515580): a null object is an error returning 1; an object that
    /// cannot be used for localization (<c>vtable+0x18 != 1</c>) or whose movement the tap filter says to ignore is the UnlocalizedObject error returning 1. On success the engine derives the
    /// robot's pose from the observation, adds a vision-only state to the history, rejiggers origins when the object's origin differs, updates the object, map and face origins, sets the
    /// history pose, updates the current pose (0x005159C8..0x005159D8), then <c>SetLocalizedTo(existing)</c>, and returns 0; a failing <see cref="SetLocalizedTo"/> is returned as 1. NOT BUILT,
    /// counted in <see cref="UnbuiltLocalizeToObjectCalls"/>: everything but the <see cref="SetLocalizedTo"/> call (the robot pose and history update, <c>Rejigger</c>, the origin updates
    /// and the [Robot+0x2C6] store).
    /// </summary>
    // fidelity: M11-044
    internal uint LocalizeToObject(ObservableObject? observed, ObservableObject? existing)
    {
        if (existing is null) { Log?.Invoke("Robot.LocalizeToObject.ExistingObjectPieceNullPointer"); return 1; }
        if (!CanBeUsedForLocalization(existing) || (ShouldIgnoreMovementDueToDoubleTap?.Invoke(existing.ObjectId) ?? false))
        {
            Log?.Invoke($"Robot.LocalizeToObject.UnlocalizedObject: Refusing to localize to object {existing.ObjectId}, which claims not to be localizable.");
            return 1;
        }
        UnbuiltLocalizeToObjectCalls++;
        return SetLocalizedTo(existing) ? 0u : 1u;
    }

    /// <summary>The <c>MovementComponent</c> bytes +0xA (head NOT in position, status bit 0x200 clear) and +0xC (wheels moving, 0x8000), from the newest robot state. Unwired: both 0.</summary>
    public Func<(bool HeadNotInPosition, bool WheelsMoving)>? MovementBytes { get; set; }
    /// <summary>The ObjectID at <c>[MovementComponent+0x20]</c> (role unread); null for none. Unwired: none.</summary>
    public Func<uint?>? MovementComponentObjectId { get; set; }
    /// <summary><c>Robot::WasObjectTappedRecently(ObjectID)</c> (0x0050D19A). Unwired: false.</summary>
    public Func<uint, bool>? WasObjectTappedRecently { get; set; }
    /// <summary><c>RobotStateHistory::GetComputedStateAt(ts)</c>: the robot's pose at a time, null when that call fails. Unwired: always fails.</summary>
    public Func<uint, Pose3d?>? ComputedRobotPoseAt { get; set; }
    /// <summary><c>Robot::GetPose()</c> (Robot+0x298), the robot's CURRENT pose. Unwired: the frame's paired pose is used.</summary>
    public Func<Pose3d?>? CurrentRobotPose { get; set; }
    /// <summary><c>Robot::GetLastImageTimeStamp()</c>. Unwired: the timestamp of the frame being processed.</summary>
    public Func<uint>? LastImageTimestamp { get; set; }
    /// <summary><c>MemoryMap</c> vtable +0x38 (its time of last change), which <c>AddAndUpdateObjects</c> maxes into this+0x58. Unwired: 0.</summary>
    public Func<double>? MemoryMapTimeOfLastChange { get; set; }
    /// <summary><c>BaseStationTimer::GetCurrentTimeInSeconds()</c>. Unwired: the process's tick count in seconds.</summary>
    public Func<double>? BaseStationSeconds { get; set; }

    /// <summary>BlockWorld+0x54: whether any object changed in the last <c>UpdateObservedMarkers</c> (its readers are unread).</summary>
    public bool DidObjectsChange => _didObjectsChange;
    /// <summary>BlockWorld+0x58: <c>(u32)fmax((double)ts, MemoryMap.vt+0x38())</c> as <c>AddAndUpdateObjects</c> last stored it (0x00621214).</summary>
    public uint TimeOfLastChangeField => _timeOfLastChange;
    private uint _timeOfLastChange;

    /// <summary>Whether <c>CheckForUnobservedObjects</c> builds the xPad/yPad terms (0x0062206E..0x006220DA). It does not: the field names behind them are unread, so both pads are 0.</summary>
    public const bool PaddingIsBuilt = false;

    private bool Carrying(uint id) => IsCarryingObject?.Invoke(id) ?? false;
    internal void LogLine(string line) => Log?.Invoke(line);

    /// <summary>
    /// <c>PotentialObjectsForLocalizingTo::CouldUseObjectForLocalization(obj)</c> 0x0050D170..0x0050D1C6: <c>(obj.ID != [DockingComponent+0xC]) &amp;&amp; CanBeUsedForLocalization
    /// &amp;&amp; (obj.ID != [MovementComponent+0x20]) &amp;&amp; !WasObjectTappedRecently(obj.ID)</c>; the virtual and the tap test are both called before the branches (0x0050D19A).
    /// The roles of the two ID fields are unread (M11-042).
    /// </summary>
    // fidelity: M11-044
    internal bool CouldUseObjectForLocalization(ObservableObject obj)
    {
        bool usable = CanBeUsedForLocalization(obj);
        bool tapped = WasObjectTappedRecently?.Invoke(obj.ObjectId) ?? false;
        uint? dock = DockingObjectId?.Invoke();
        uint? mc = MovementComponentObjectId?.Invoke();
        return !(dock is { } d && d == obj.ObjectId) && usable && !(mc is { } m && m == obj.ObjectId) && !tapped;
    }
    private bool OnTreadsNow() => OnTreads?.Invoke() ?? true;

    /// <summary>Creates a world with no connected objects; they arrive through <see cref="AddConnectedActiveObject"/>.</summary>
    public BlockWorld() { }

    /// <summary>
    /// The earlier constructor took a delegate that answered "which cubes are connected". The engine keeps them in
    /// <c>m_connectedObjects</c>, filled by <c>AddConnectedActiveObject</c> (M11-041), so the delegate is not read.
    /// </summary>
    public BlockWorld(Func<IEnumerable<(uint ObjectId, ObjectType Type)>> connectedActiveObjects) { }

    public event Action<ObjectObservation>? ObjectObserved;
    // fidelity: M11-038
    // BroadcastObjectObservation 0x0061FED8 is this observation event. BroadcastLocatedObjectStates 0x0061E6C0, BroadcastConnectedObjects
    // 0x0061E91C, the RobotMarkedObjectPoseUnknown broadcast and VisionSystem::CheckMailbox 0x006B2AD4 have no message type or consumer in
    // this stack (there is no EngineToGame channel): MISSING (M11-038). PoseStateChanged is this stack's consumer channel for the
    // pose-state broadcasts and for a located object being deleted.
    public event Action<ObservableObject, PoseState, PoseState>? PoseStateChanged;
    /// <summary>Lines the world model would log, for the conformance tool.</summary>
    public event Action<string>? Log;

    /// <summary>Every object the world holds, located or forgotten.</summary>
    public IReadOnlyList<ObservableObject> Objects
    {
        get { lock (_gate) return _located.Values.Concat(_forgotten.Values).ToList(); }
    }

    /// <summary>
    /// <c>BlockWorld::AddCollisionObstacle</c> 0x00624A16 is one call:
    /// <c>AddMarkerlessObject(pose, ObjectType::CollisionObstacle)</c>. That routine 0x00622380 makes a
    /// <c>MarkerlessObject</c> of the type, builds a local pose of no rotation about Z and translation
    /// (0, 0, size.z / 2) - the <c>vmov.f32 s0, #0.5</c> and the multiply at 0x006223CC, which stands the
    /// box on the ground - and multiplies the pose given by it before adding it to the world.
    ///
    /// The object id is this stack's (LOCAL): markerless objects are counted from
    /// <see cref="FirstMarkerlessObjectId"/> upwards, since they have no marker and no cube radio to take
    /// an id from. The engine calls the virtual <c>SetID</c> there (M11-042, unread).
    /// </summary>
    public ObservableObject AddMarkerlessObject(Pose3d pose, ObjectType type)
    {
        var size = MarkerlessObject.SizeByType(type) ?? throw new ArgumentException($"{type} is not a markerless object type", nameof(type));
        var standing = pose.Compose(new Pose3d(Mat3.Identity, new Vec3(0, 0, size.Z * 0.5)));
        lock (_gate)
        {
            uint id = _nextMarkerlessId++;
            var obj = new ObservableObject(id, type, Array.Empty<KnownMarker>()) { Pose = standing, PoseState = PoseState.Known, OriginId = CurrentOriginId };
            _located[KeyOf(obj)] = obj;
            Log?.Invoke($"BlockWorld.AddMarkerlessObject: {type} {id} at {standing}");
            return obj;
        }
    }

    /// <summary>The collision obstacle an unexpected movement leaves where the robot was blocked.</summary>
    public ObservableObject AddCollisionObstacle(Pose3d pose) => AddMarkerlessObject(pose, ObjectType.CollisionObstacle);

    /// <summary>LOCAL: where this stack starts numbering markerless objects, clear of the cube ids.</summary>
    public const uint FirstMarkerlessObjectId = 1000;
    private uint _nextMarkerlessId = FirstMarkerlessObjectId;

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26, CC27): no objects, no connected objects, no
    /// confirmer entries, markerless ids from <see cref="FirstMarkerlessObjectId"/> again. No event is raised. The tuning properties and
    /// subscribers are kept. The process-static ObjectID counter, unique-type map and warning cooldowns are kept too: the engine's
    /// live in .bss and outlive the Robot.
    /// </summary>
    internal void ResetToConstructed()
    {
        lock (_gate)
        {
            _located.Clear();
            _forgotten.Clear();
            _connectedObjects.Clear();
            _confirmations.Clear();
            _poseChanges.Clear();
            _appendPoseChanges = false;
            _didObjectsChange = false;
            _markerTimestamp = 0;
            _lastImageTimestamp = 0;
            _nextMarkerlessId = FirstMarkerlessObjectId;
            _localizedTo = ObservableObject.UnassignedId;
            _movedSinceLocalized = 0;
            _robot2C4 = 0; _robot2C5 = 0; _robot2C8 = -1.0f;
            LiftOccluderPoints = Array.Empty<Vec3>();          // the VisionComponent that held them is deleted with the Robot
        }
    }

    private static (uint Origin, int Family, int Type, int Id) KeyOf(ObservableObject o) =>
        (o.OriginId, (int)o.Family, (int)o.Type, (int)o.ObjectId);

    public IReadOnlyList<ObservableObject> LocatedObjects
    {
        get { lock (_gate) return _located.Values.Where(o => o.IsLocated).ToList(); }
    }

    /// <summary>
    /// <c>BlockWorld::GetLocatedObjectByIdHelper(id, family = -1)</c>: the object with that ID in the robot's current origin (a default
    /// filter, origin mode 0), or null. It is the walk of <see cref="FindLocatedObjectHelper"/> with the ID allowed.
    /// </summary>
    // fidelity: M11-004
    public ObservableObject? GetLocatedObjectById(uint objectId)
    {
        var o = GetLocatedObjectByIdHelper(objectId);
        return o is { IsLocated: true } ? o : null;
    }

    internal ObservableObject? GetLocatedObjectByIdHelper(uint objectId)
    {
        var f = new BlockWorldFilter();
        f.AllowedIds.Add(objectId);
        return FindLocatedObjectHelper(f, null, true);
    }

    /// <summary>Any object of the world with that ID, located or forgotten (LOCAL: the stack's own forgotten objects stay readable).</summary>
    public ObservableObject? GetObjectById(uint objectId)
    {
        if (GetLocatedObjectByIdHelper(objectId) is { } o) return o;
        lock (_gate) return _forgotten.GetValueOrDefault(objectId);
    }

    // ------------------------------------------------------------------------------------------ the filter walk

    /// <summary>
    /// <c>BlockWorld::FindLocatedObjectHelper(filter, modifyFn, returnFirstOnly)</c> 0x0061EB78 (M11-004). The filter is copied
    /// (0x0061EB94); <c>onlyLatest</c> appends the predicate <c>obj[+0x1C] == BlockWorld[+0x90]</c> (0x0061EB98..0x0061EBCE,
    /// lambda 0x00627EAA); the walk is the located map in ascending origin, family, type, ObjectID order; the origin test is by
    /// <c>originMode</c> (0 the robot's origin, 1 everything else, 2 everything, otherwise the ignore/allowed origin sets); the
    /// family, type and ID tests skip a key in the ignore set or outside a non-empty allowed set; every predicate must pass; a
    /// passing object goes to the modify function (when set) and becomes the running result, and with
    /// <paramref name="returnFirstOnly"/> the walk stops there, else the result is the LAST passing object. There is no distance or
    /// recency comparison (0x0061ED82..0x0061ED8E, 0x0061EE12..0x0061EE36, return 0x0061EF1C).
    /// </summary>
    // fidelity: M11-004
    public ObservableObject? FindLocatedObjectHelper(BlockWorldFilter filter, Action<ObservableObject>? modifyFn, bool returnFirstOnly)
    {
        var f = filter.Clone();
        if (f.OnlyConsiderLatestUpdate)
        {
            uint stamp = _markerTimestamp;                                         // captured at 0x0061EBA4
            f.Predicates.Add(o => o.LastObservedTimestamp == stamp);
        }
        List<KeyValuePair<(uint Origin, int Family, int Type, int Id), ObservableObject>> snapshot;
        lock (_gate) snapshot = _located.ToList();
        ObservableObject? result = null;
        foreach (var (key, obj) in snapshot)
        {
            switch (f.OriginMode)
            {
                case OriginMode.InRobotFrame: if (key.Origin != CurrentOriginId) continue; break;
                case OriginMode.NotInRobotFrame: if (key.Origin == CurrentOriginId) continue; break;
                case OriginMode.InAnyFrame: break;
                default:
                    if (f.IgnoreOrigins.Contains(key.Origin)) continue;
                    if (f.AllowedOrigins.Count != 0 && !f.AllowedOrigins.Contains(key.Origin)) continue;
                    break;
            }
            if (!Passes(f, obj, key.Family, key.Type, key.Id)) continue;
            modifyFn?.Invoke(obj);
            result = obj;
            if (returnFirstOnly) break;
        }
        return result;
    }

    private static bool Passes(BlockWorldFilter f, ObservableObject obj, int family, int type, int id)
    {
        if (f.IgnoreFamilies.Contains((ObjectFamily)family)) return false;
        if (f.AllowedFamilies.Count != 0 && !f.AllowedFamilies.Contains((ObjectFamily)family)) return false;
        if (f.IgnoreTypes.Contains((ObjectType)type)) return false;
        if (f.AllowedTypes.Count != 0 && !f.AllowedTypes.Contains((ObjectType)type)) return false;
        uint objId = obj.ObjectId;
        if (f.IgnoreIds.Contains(objId)) return false;
        if (f.AllowedIds.Count != 0 && !f.AllowedIds.Contains(objId)) return false;
        foreach (var p in f.Predicates) if (!p(obj)) return false;
        return true;
    }

    /// <summary><c>BlockWorld::FindLocatedMatchingObjects</c> 0x006215CC: <c>FindLocatedObjectHelper</c> with returnFirstOnly false and a modify function that appends, so the list is every passing object in ascending order (0x006215EC).</summary>
    // fidelity: M11-004
    public List<ObservableObject> FindLocatedMatchingObjects(BlockWorldFilter filter)
    {
        var list = new List<ObservableObject>();
        FindLocatedObjectHelper(filter, list.Add, false);
        return list;
    }

    /// <summary><c>BlockWorld::FindConnectedObjectHelper</c> 0x0061F078: the connected map in ascending family, type, ID order with the ID, family, type and predicate tests and no origin test; the result is the last passing object, or the first with returnFirstOnly.</summary>
    // fidelity: M11-004
    public ObservableObject? FindConnectedObjectHelper(BlockWorldFilter filter, Action<ObservableObject>? modifyFn, bool returnFirstOnly)
    {
        List<KeyValuePair<(int Family, int Type, int Id), ObservableObject>> snapshot;
        lock (_gate) snapshot = _connectedObjects.ToList();
        ObservableObject? result = null;
        foreach (var (key, obj) in snapshot)
        {
            if (!Passes(filter, obj, key.Family, key.Type, key.Id)) continue;
            modifyFn?.Invoke(obj);
            result = obj;
            if (returnFirstOnly) break;
        }
        return result;
    }

    /// <summary><c>BlockWorld::GetConnectedActiveObjectByIdHelper(ObjectID)</c> 0x0061F58C: a pure lookup, <c>FindConnectedObjectHelper</c> with the ID allowed and returnFirstOnly (0x0061F61C).</summary>
    // fidelity: M11-004
    public ObservableObject? GetConnectedActiveObjectByIdHelper(uint objectId)
    {
        var f = new BlockWorldFilter();
        f.AllowedIds.Add(objectId);
        return FindConnectedObjectHelper(f, null, true);
    }

    /// <summary><c>GetConnectedActiveObjectByActiveIdHelper(activeId)</c>: the connected object whose activeID (obj+0x40) is that slot.</summary>
    // fidelity: M11-041
    public ObservableObject? GetConnectedActiveObjectByActiveIdHelper(int activeId)
    {
        var f = new BlockWorldFilter();
        f.Predicates.Add(o => o.ActiveId == activeId);
        return FindConnectedObjectHelper(f, null, true);
    }

    /// <summary>The ObjectID the connected object in radio slot <paramref name="activeId"/> has, or null when the slot is empty.</summary>
    public uint? ConnectedObjectIdForActiveId(uint activeId) =>
        activeId > int.MaxValue ? null : GetConnectedActiveObjectByActiveIdHelper((int)activeId)?.ObjectId;

    /// <summary>The connected objects, ascending family, type, ID.</summary>
    public IReadOnlyList<ObservableObject> ConnectedObjects { get { lock (_gate) return _connectedObjects.Values.ToList(); } }

    private BlockWorldFilter FamilyTypeFilter(ObservableObject o, OriginMode mode = OriginMode.InRobotFrame)
    {
        var f = new BlockWorldFilter { OriginMode = mode };
        f.AllowedFamilies.Add(o.Family);
        f.AllowedTypes.Add(o.Type);
        return f;
    }

    // ------------------------------------------------------------------------------------- the frame sequence

    /// <summary>
    /// <c>BlockWorld::UpdateObservedMarkers</c> 0x00624EE8 (M11-037): the per-frame sequence. <paramref name="camera"/>
    /// is the camera at the frame's timestamp and <paramref name="pd"/> the robot state paired with it.
    ///
    /// The prologue clears the PoseChange list and sets this+0x74 (0x00624F74..0x00624F7E); the play-area-size event that also runs
    /// there belongs to the map (M13) and is not built. An EMPTY list sets this+0x90 to 0 and, only when the last image timestamp is
    /// not 0, clears the occluders, adds the lift occluder and runs CheckForUnobservedObjects, then falls into the tail
    /// (0x00625020..0x00625052); it never creates objects and never updates stacked poses. A non-empty list sets this+0x90 to the first
    /// marker's timestamp, clears the occluders, adds the lift occluder, clears this+0x54, creates the objects, adds and updates them,
    /// and only when both succeed (r7 == 1, 0x006250C0) runs CheckForUnobservedObjects and UpdatePoseOfStackedObjects; a failure
    /// returns without the tail and leaves this+0x74 set. The tail clears this+0x74, runs the collision dirty pass,
    /// <c>BlockConfigurationManager::Update</c> (0x0062520C) and <c>UpdateMarkerlessObjects</c> (0x0062521A).
    /// </summary>
    // fidelity: M11-036, M11-037, M11-006, M11-010
    public BlockWorldFrameResult UpdateObservedMarkers(IReadOnlyList<ObservedMarker> markers, CameraModel camera, uint timestamp, VisionPoseData pd)
    {
        // Robot::GetLastImageTimeStamp: the empty-list branch (0x00625028), UpdatePoseOfStackedObjects and UpdateMarkerlessObjects (0x0062521A) use it
        uint lastImage = LastImageTimestamp?.Invoke() ?? timestamp;
        lock (_gate) { _poseChanges.Clear(); _appendPoseChanges = true; _lastImageTimestamp = lastImage; }
        var observations = new List<ObjectObservation>();
        var forgotten = new List<ObservableObject>();
        bool r7 = true;
        if (markers.Count == 0)
        {
            _markerTimestamp = 0;                                                  // 0x00625024
            if (lastImage != 0)                                                    // 0x00625030
            {
                camera.Occluders.Clear();                                          // 0x0062503A
                AddLiftOccluder(camera, pd);                                       // 0x00625046
                forgotten.AddRange(CheckForUnobservedObjects(camera, lastImage, pd.Moving, pd.RotatingTooFast));   // 0x0062504E
            }
        }
        else
        {
            uint ts = markers[0].Timestamp;                                        // 0x00624F8C
            _markerTimestamp = ts;                                                 // 0x00624F8E
            camera.Occluders.Clear();                                              // 0x00624F98
            AddLiftOccluder(camera, pd);                                           // 0x00624FA4
            _didObjectsChange = false;                                             // 0x00624FAC
            var instances = CreateObjectsFromMarkers(markers, camera);             // 0x00624FC4 (always succeeds, 0x00625616)
            uint status = AddAndUpdateObjects(instances, camera, ts, pd, observations);   // 0x0062505A
            if (status != 0)
            {
                Log?.Invoke("BlockWorld.UpdateObservedMarkers.AddAndUpdateFailed");
                r7 = false;
            }
            if (r7)
            {
                forgotten.AddRange(CheckForUnobservedObjects(camera, ts, pd.Moving, pd.RotatingTooFast));   // 0x006250CA
                UpdatePoseOfStackedObjects();                                      // 0x006250D0
            }
        }
        if (!r7) return new BlockWorldFrameResult(observations, forgotten);       // 0x00625220: the error return skips the tail
        lock (_gate) _appendPoseChanges = false;                                   // 0x006250D8
        CollisionDirtyPass();
        BlockConfigurationManagerUpdate?.Invoke();                                 // 0x0062520C
        UpdateMarkerlessObjects(lastImage, pd.RobotPose);                          // 0x0062521A
        return new BlockWorldFrameResult(observations, forgotten);
    }

    /// <summary>
    /// The tail's dirty pass (0x006250E8..0x0062519C): <c>FindLocatedObjectHelper</c> with the default filter, the predicate
    /// <c>BlockWorld::CheckForCollisionWithRobot</c> 0x0062593C and the modify function <c>MarkObjectDirty(obj, true)</c>, skipped when
    /// the byte at <c>DockingComponent+4</c> is non-zero. NOT BUILT (M11-042, RECOVERABLE_GAP): the predicate's first test is
    /// <c>ObservableObject</c> vtable +0x58 == 0, whose body is unread, and the byte's meaning is unread. This stub makes the missing
    /// pass visible through <see cref="UnbuiltCollisionDirtyPassRuns"/>.
    /// </summary>
    // fidelity: M11-042
    private void CollisionDirtyPass() => UnbuiltCollisionDirtyPassRuns++;

    /// <summary>How many frames reached the collision dirty pass that this stack does not build (M11-042).</summary>
    public int UnbuiltCollisionDirtyPassRuns { get; private set; }

    /// <summary>
    /// <c>BlockConfigurationManager::Update(Robot)</c> 0x616D7C (C3.2/Q2.2). The engine gates it on
    /// <c>[this+0x24]</c>/<c>[this+0xc]</c> and <c>DidAnyObjectsMovePastThreshold</c>, then runs
    /// <c>UpdateAllBlockConfigs</c>, <c>PruneFullPyramids</c>, <c>UpdateLastConfigCheckBlockPoses</c> and
    /// <c>NotifyBroadcasterOfConfigurationManagerUpdate</c>. The gate's inputs (the ids <c>SetObjectPoseChanged</c> collects, the
    /// forced flag) are the manager's; this stack's <c>BlockConfigurationManager.Update()</c> is hooked here (M12 sets it) and is
    /// always a full rebuild.
    /// </summary>
    // fidelity: M11-037
    public Action? BlockConfigurationManagerUpdate { get; set; }

    // -------------------------------------------------------------------------------- CreateObjectsFromMarkers

    /// <summary>
    /// <c>ObservableObjectLibrary::CreateObjectsFromMarkers</c> 0x0062539C: one candidate pose per marker, grouped and clustered by
    /// object type; each cluster becomes an instance, a fresh <c>Clone</c> of the library prototype (ObjectID -1, ActiveID -1,
    /// FactoryID 0), its pose clamped flat by <see cref="CreationFlatClampAngleRad"/> when it is active, <c>InitPose(pose, Dirty)</c>
    /// (0x0062554E..0x00625582), and the first marker's timestamp at +0x1C (0x006255AC). The instances are ordered by the squared
    /// distance of the cluster pose from the world origin, nearest first, ties in creation order (the multimap at 0x006255B2..0x006255BA).
    /// </summary>
    // fidelity: M11-006, M11-013
    private List<ObservableObject> CreateObjectsFromMarkers(IReadOnlyList<ObservedMarker> markers, CameraModel camera)
    {
        var made = new List<(double Key, ObservableObject Instance)>();
        // one candidate pose per marker, grouped by object type
        var candidates = new List<(ObjectType Type, KnownMarker Known, ObservedMarker Seen, Pose3d World, double Rms)>();
        foreach (var seen in markers)
        {
            if (CubeGeometry.LookupMarker(seen.Code) is not { } lk) continue;
            var (type, known) = lk;
            var res = PoseEstimation.Solve(camera, known.CornersOnObject(), seen.Corners);
            if (res is null) { Log?.Invoke($"marker {seen.Code}: pose solve failed"); continue; }
            if (res.RmsReprojectionPx > MaxReprojectionRmsPx) { Log?.Invoke($"marker {seen.Code}: reprojection {res.RmsReprojectionPx:F1} px too large"); continue; }
            var world = camera.Pose.Compose(res.ObjectInCamera);
            candidates.Add((type, known, seen, world, res.RmsReprojectionPx));
        }

        foreach (var group in candidates.GroupBy(c => c.Type))
        {
            // cluster the per-marker poses of this type; each cluster is one physical object
            var remaining = group.OrderByDescending(c => QuadArea(c.Seen.Corners)).ToList();
            while (remaining.Count > 0)
            {
                var seed = remaining[0];
                var cluster = remaining.Where(c => c.World.IsSameAs(seed.World, ClusterDistanceMm, ClusterAngleRad)).ToList();
                foreach (var c in cluster) remaining.Remove(c);
                var pose = seed.World; double rms = seed.Rms;
                if (cluster.Count > 1)
                {
                    // refine with every corner of every marker in the cluster
                    var obj = cluster.SelectMany(c => c.Known.CornersOnObject()).ToList();
                    var img = cluster.SelectMany(c => c.Seen.Corners).ToList();
                    var joint = PoseEstimation.Solve(camera, obj, img);
                    if (joint is not null && joint.RmsReprojectionPx <= MaxReprojectionRmsPx * 2) { pose = camera.Pose.Compose(joint.ObjectInCamera); rms = joint.RmsReprojectionPx; }
                }
                var seenMarkers = cluster.Select(c => c.Seen).ToList();
                var instance = new ObservableObject(group.Key, CubeGeometry.MarkersFor(group.Key)) { OriginId = CurrentOriginId };
                double key = pose.Translation.Dot(pose.Translation);
                var placed = instance.IsActive ? ClampPoseToFlat(pose, CreationFlatClampAngleRad) : pose;   // 0x00625578
                instance.SetPose(placed, -1.0, PoseState.Dirty);                                            // InitPose 0x00625580
                instance.LastObservedTimestamp = seenMarkers[0].Timestamp;                                   // 0x006255AC
                instance.LastObservedMarkers = seenMarkers.Select(s => s.Code).ToList();
                var xs = seenMarkers.SelectMany(s => s.Corners).Select(p => p.X).ToList();
                var ys = seenMarkers.SelectMany(s => s.Corners).Select(p => p.Y).ToList();
                instance.LastImageRect = (xs.Min(), ys.Min(), xs.Max() - xs.Min(), ys.Max() - ys.Min());
                instance.LastReprojectionRmsPx = rms;
                made.Add((key, instance));
            }
        }
        // multimap<float, ObservableObject*>: ascending key, an equal key goes after the ones already there
        var ordered = new List<(double Key, ObservableObject Instance)>();
        foreach (var m in made)
        {
            int i = ordered.Count;
            while (i > 0 && ordered[i - 1].Key > m.Key) i--;
            ordered.Insert(i, m);
        }
        return ordered.Select(o => o.Instance).ToList();
    }

    // -------------------------------------------------------------------------------- AddAndUpdateObjects

    /// <summary>
    /// <c>BlockWorld::AddAndUpdateObjects</c> 0x00620AD4 (M11-013, M11-004, M11-007, M11-044), per instance, nearest first. A
    /// <see cref="PotentialObjectsForLocalizingTo"/> is made first (0x00620B0C); then for each instance:
    /// <list type="number">
    /// <item>dist is the 3-D norm of the vector from the robot's CURRENT pose (<c>Robot::GetPose()</c>, 0x00620BE2) to the instance's pose (0x00620BD4..0x00620C34);</item>
    /// <item><c>IsObjectConfirmedAtObservedPose(instance, &amp;match)</c>; the instance's ObjectID is the match's when there is one, else
    ///   <c>SetID()</c> (0x00620C40..0x00620C5A), whether or not the pose was confirmed;</item>
    /// <item>when confirmed the Insert flag is 0 and <c>AddVisualObservation</c> is skipped; when not:
    ///   <c>AddVisualObservation(instance, GetLocatedObjectByIdHelper(id), WasCameraMoving(ts), dist)</c>; a false return drops the instance with the
    ///   NonConfirmingObservation log (0x00620C5C..0x00620D86); a true one makes the flag 1;</item>
    /// <item>an active instance with no connected counterpart (by ObjectID) warns at most once per 10 s and CONTINUES (0x00620DF0..0x00620EDE);</item>
    /// <item>the located object is found (a unique type: every located object of the type, a carried one in the current origin drops the observation, the one in the
    ///   current origin is used; else by ID) or the observation is skipped with an error (0x00620EDE..0x00621282);</item>
    /// <item>the located object takes the instance's timestamp and the marker observation times (0x00621146..0x0062114A);
    ///   <c>Insert(instance, located, dist, flag)</c> and, when it returns 1, <c>Insert(instance, other, dist, true)</c> for every other origin's object in ascending
    ///   origin order (0x0062115E..0x006211A4);</item>
    /// <item>one occluder per observed marker, the RobotObservedObject broadcast, this+0x54 = 1 and this+0x58 = (u32)fmax((double)ts, MemoryMap.vt+0x38())
    ///   (0x006211A6..0x00621214).</item>
    /// </list>
    /// After the loop <c>LocalizeRobot</c> runs and its result is the return (0x00621304, 0x00621320). NOT BUILT (visible): <c>Robot::LocalizeToObject</c> and
    /// <c>Rejigger</c> do not exist in this stack, so <c>LocalizeRobot</c> is a stub that counts its calls and the pairs it would have localized to, and returns 0.
    /// </summary>
    // fidelity: M11-013, M11-004, M11-007, M11-044
    private uint AddAndUpdateObjects(List<ObservableObject> instances, CameraModel camera, uint timestamp, VisionPoseData pd, List<ObjectObservation> observations)
    {
        var robotPos = (CurrentRobotPose?.Invoke() ?? pd.RobotPose).Translation;
        var potentials = new PotentialObjectsForLocalizingTo(this);                // 0x00620B0C
        foreach (var instance in instances)
        {
            float dx = (float)(instance.Pose.Translation.X - robotPos.X);
            float dy = (float)(instance.Pose.Translation.Y - robotPos.Y);
            float dz = (float)(instance.Pose.Translation.Z - robotPos.Z);
            float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);                  // 0x00620BF4..0x00620C18

            bool confirmed = IsObjectConfirmedAtObservedPose(instance, out var match);
            if (match is not null) instance.ObjectId = match.ObjectId; else instance.SetID();   // 0x00620C4E..0x00620C5A

            var before = GetLocatedObjectByIdHelper(instance.ObjectId);
            var prevPose = before?.Pose ?? Pose3d.Identity;
            var prevState = before?.PoseState ?? PoseState.Unknown;
            bool flag = false;                                                     // [sp+0x20]
            if (!confirmed)
            {
                var existing = before;                                             // GetLocatedObjectByIdHelper(id, -1)
                if (!AddVisualObservation(instance, existing, pd.CameraMoving, dist))
                {
                    Log?.Invoke($"BlockWorld.AddAndUpdateObjects.NonConfirmingObservation: Added non-confirming visual observation for {(int)instance.ObjectId}");
                    continue;
                }
                flag = true;
            }

            if (instance.IsActive) WarnIfNotConnected(instance);                   // 0x00620DF0..0x00620EDE

            var located = FindLocatedForObserved(instance, out var byOrigin, out bool drop);
            if (drop || located is null) continue;

            located.LastObservedTimestamp = instance.LastObservedTimestamp;        // 0x00621146
            UpdateMarkerObservationTimes(located, instance);                       // 0x0062114A
            if (potentials.Insert(instance, located, dist, flag, pd.CameraMoving) == 1)   // 0x0062115E
                foreach (var kv in byOrigin.OrderBy(k => k.Key))
                    if (!ReferenceEquals(kv.Value, located)) potentials.Insert(instance, kv.Value, dist, true, pd.CameraMoving);   // 0x00621182
            AddObservedMarkerOccluders(camera, located, instance.LastObservedMarkers);   // 0x006211CC
            var observation = new ObjectObservation(located, timestamp, located.LastObservedMarkers, prevPose, prevState, before is null, located.LastReprojectionRmsPx);
            Log?.Invoke($"{(before is null ? "new" : "seen")} {located} via {string.Join(",", located.LastObservedMarkers)} rms={located.LastReprojectionRmsPx:F2}px");
            observations.Add(observation);
            ObjectObserved?.Invoke(observation);                                   // BroadcastObjectObservation 0x0061FED8 (0x006211DC)
            _didObjectsChange = true;                                              // 0x006211E6
            _timeOfLastChange = unchecked((uint)Math.Max((double)timestamp, MemoryMapTimeOfLastChange?.Invoke() ?? 0.0));   // 0x00621214: AddAndUpdateObjects's own ts argument (0x00620B9C)
        }
        return potentials.LocalizeRobot();                                         // 0x00621304
    }

    /// <summary>
    /// <c>UpdateMarkerObservationTimes(instance)</c> 0x0062114A. M11-042: its body is unread. The marker codes, image rectangle, solve
    /// error and observation count this stack keeps on the located object for the docking code are the result it carries.
    /// </summary>
    // fidelity: M11-042
    private static void UpdateMarkerObservationTimes(ObservableObject located, ObservableObject instance)
    {
        located.LastObservedMarkers = instance.LastObservedMarkers;
        located.LastImageRect = instance.LastImageRect;
        located.LastReprojectionRmsPx = instance.LastReprojectionRmsPx;
        located.TimesObserved++;
    }

    /// <summary>
    /// One occluder per observed marker, its projected quad at its depth (<c>AddAndUpdateObjects</c> 0x006211CC), so the next object's
    /// visibility test knows what the camera could actually see through.
    /// </summary>
    // fidelity: M11-010
    private static void AddObservedMarkerOccluders(CameraModel camera, ObservableObject located, IReadOnlyList<MarkerType> observed)
    {
        foreach (var m in located.Markers)
        {
            if (!observed.Contains(m.Code)) continue;
            var world = m.CornersInWorld(located.Pose);
            var px = new Vec2[4];
            double depth = 0;
            bool ok = true;
            for (int i = 0; i < 4 && ok; i++)
            {
                var p = camera.Project(world[i]);
                if (p is null) { ok = false; break; }
                px[i] = p.Value;
                depth += camera.ToCamera(world[i]).Z;
            }
            if (ok) camera.Occluders.Add(new[] { px[0], px[2], px[3], px[1] }, depth / 4);
        }
    }

    /// <summary>
    /// M11-004 / M11-013: the connected-counterpart check at 0x00620DF0..0x00620EDE. Only an active instance gets it. The static
    /// <c>unordered_map&lt;int,float&gt;</c> is read at the instance's ObjectID (an absent key inserts 0.0); when now is before the entry
    /// nothing more happens; otherwise <c>GetConnectedActiveObjectByIdHelper(ObjectID)</c> runs and a null result warns "Observed active
    /// object of type %s but it's not connected. Is the battery plugged in?" and stores now + 10.0. Either way the observation goes on.
    /// <c>now</c> is <c>BaseStationTimer::GetCurrentTimeInSeconds()</c> (0x00620E42..0x00620E5E), the engine's wall clock in seconds, read through
    /// <see cref="BaseStationSeconds"/>, not the frame's timestamp.
    /// </summary>
    // fidelity: M11-013, M11-004
    private void WarnIfNotConnected(ObservableObject instance)
    {
        double now = BaseStationSeconds?.Invoke() ?? Environment.TickCount64 / 1000.0;
        int key = (int)instance.ObjectId;
        float until;
        lock (s_unconnectedWarnGate)
        {
            if (!s_unconnectedWarnUntil.TryGetValue(key, out until)) s_unconnectedWarnUntil[key] = until = 0f;   // operator[] inserts 0.0
        }
        if (now < until) return;                                                   // 0x00620E66..0x00620E6E
        if (GetConnectedActiveObjectByIdHelper(instance.ObjectId) is not null) return;   // 0x00620E78
        lock (s_unconnectedWarnGate) s_unconnectedWarnUntil[key] = (float)(now + UnconnectedWarnCooldownSec);   // 0x00620ED6..0x00620EDA
        Log?.Invoke($"Observed active object of type {instance.Type} but it's not connected. Is the battery plugged in?");
    }

    /// <summary>
    /// The located object <c>AddAndUpdateObjects</c> updates (0x00620EDE..0x00621282). A unique type (<c>IsUnique</c>, vtable +0x5C): the located objects
    /// of its family and type in every origin (origin mode 2) whose predicate passes (the plain function pointer 0x006215C4, which is <c>IsUnique()</c> and so
    /// always true there); an object in the current origin that the robot is carrying drops the observation with the SeeingCarriedObject warning; a second
    /// object in one origin warns and keeps the first; none in the current origin is the AddAndUpdateNoCurrentOriginMatch error and the instance is skipped.
    /// A non-unique type: <c>GetLocatedObjectByIdHelper(id, -1)</c> (0x00620FE0) with no other predicate, else the NonUnique variant of the error.
    /// <paramref name="byOrigin"/> is the engine's <c>map&lt;originRootID, object&gt;</c> the Insert loop walks.
    /// </summary>
    // fidelity: M11-013
    private ObservableObject? FindLocatedForObserved(ObservableObject instance, out Dictionary<uint, ObservableObject> byOrigin, out bool drop)
    {
        drop = false;
        byOrigin = new Dictionary<uint, ObservableObject>();
        if (instance.IsUnique)
        {
            var f = FamilyTypeFilter(instance, OriginMode.InAnyFrame);
            f.Predicates.Add(o => o.IsUnique);                                     // 0x006215C4
            var all = FindLocatedMatchingObjects(f);
            foreach (var o in all)
            {
                if (o.OriginId == CurrentOriginId && Carrying(o.ObjectId))
                {
                    Log?.Invoke($"Blockworld.AddAndUpdateObjects.SeeingCarriedObject: object {o.ObjectId} of type {o.Type} is being carried; the observation is dropped");
                    drop = true;
                    return null;
                }
                if (byOrigin.ContainsKey(o.OriginId))
                {
                    Log?.Invoke($"BlockWorld.AddAndUpdateObjects.MultipleMatchesForUniqueObjectInSameFrame: {o.Type} {o.ObjectId}; keeping the first");
                    continue;
                }
                byOrigin[o.OriginId] = o;
            }
            if (!byOrigin.TryGetValue(CurrentOriginId, out var located))
            {
                Log?.Invoke($"BlockWorld.UpdateObjectPoses.AddAndUpdateNoCurrentOriginMatch: Must find match in current origin, since object is confirmed ({instance.Type} {instance.ObjectId}).");
                return null;
            }
            return located;
        }
        var found = GetLocatedObjectByIdHelper(instance.ObjectId);
        if (found is null)
            Log?.Invoke($"BlockWorld.UpdateObjectPoses.AddAndUpdateNoCurrentOriginMatchNonUnique: Must find match in current origin, since object is confirmed ({instance.Type} {instance.ObjectId}).");
        else byOrigin[found.OriginId] = found;
        return found;
    }

    // ------------------------------------------------------------------------------ the ObjectPoseConfirmer

    internal PoseConfirmation? EntryFor(uint id)
    {
        lock (_gate) return _confirmations.TryGetValue((int)id, out var e) ? e : null;
    }

    private PoseConfirmation EntryForOrCreate(uint id)
    {
        lock (_gate)
        {
            if (!_confirmations.TryGetValue((int)id, out var e)) _confirmations[(int)id] = e = new PoseConfirmation();
            return e;
        }
    }

    /// <summary>The object-match tolerance: the object's stored extent scaled by 0.8 (thunk 0x004E025C; a light cube's extent is (44,44,44), so (35.2,35.2,35.2) mm).</summary>
    // fidelity: M11-004
    public static Vec3 ObjectMatchToleranceMm(ObjectType type)
    {
        var size = CubeGeometry.SizeOf(type);
        return new Vec3(size.X * ObjectMatchDistanceFactor, size.Y * ObjectMatchDistanceFactor, size.Z * ObjectMatchDistanceFactor);
    }

    /// <summary>
    /// <c>ObjectPoseConfirmer::FindObjectMatchForObservation</c> 0x005063CC (M11-004): the object an observation is the same as.
    /// A UNIQUE type: (a) the located objects of its family and type in the current origin, when there is exactly one, else (b) a
    /// pending instance a confirmer entry holds of that family and type, else (c) the first located object of that family and type in
    /// any other origin, else (d) the last connected object of that family and type, else none. A NON-UNIQUE type:
    /// <c>FindLocatedClosestMatchingObjectHelper</c> 0x0061FA68 (predicate 0x006281DA: the ObjectType and <c>Pose3d::IsSameAs</c> with the
    /// tolerances (translation AND angle) narrowed to abs(delta) after each pass, so the closest wins; the observed object's own ID and carried objects excluded), else the confirmer's pending
    /// instances through <c>ObservableObject::IsSameAs</c> 0x008769A8 with the same narrowing, the last accepted winning. IsSameAs
    /// ignores its angle tolerance (<c>mov r3,r4</c> at 0x008769D8: it passes pi), so only the translation narrows there.
    /// </summary>
    // fidelity: M11-004
    public ObservableObject? FindObjectMatchForObservation(ObservableObject observed)
    {
        if (observed.IsUnique)
        {
            var located = FindLocatedMatchingObjects(FamilyTypeFilter(observed));           // (a) 0x00506498
            if (located.Count == 1) return located[0];
            lock (_gate)                                                                    // (b) 0x005064E0..0x00506502
                foreach (var e in _confirmations.Values)
                    if (e.Stored is { } s && s.Family == observed.Family && s.Type == observed.Type) return s;
            var other = FindLocatedObjectHelper(FamilyTypeFilter(observed, OriginMode.NotInRobotFrame), null, true);   // (c) 0x00506524..0x00506540
            if (other is not null) return other;
            return FindConnectedObjectHelper(FamilyTypeFilter(observed), null, false);      // (d) 0x0050669C
        }
        var tol = ObjectMatchToleranceMm(observed.Type);
        var filter = new BlockWorldFilter();
        filter.AllowedTypes.Add(observed.Type);
        filter.IgnoreIds.Add(observed.ObjectId);                                            // 0x0061FAA6: the observed object's own ID
        filter.Predicates.Add(o => !Carrying(o.ObjectId));                                  // 0x005064CC
        var capturedT = tol;
        double capturedA = ObjectMatchAngleRad;
        filter.Predicates.Add(o =>                                                          // 0x006281DA
        {
            var delta = o.Pose.Translation - observed.Pose.Translation;
            double ang = o.Pose.Rotation.AngularDistance(observed.Pose.Rotation);
            if (Math.Abs(delta.X) > capturedT.X || Math.Abs(delta.Y) > capturedT.Y || Math.Abs(delta.Z) > capturedT.Z || ang > capturedA) return false;
            capturedT = new Vec3(Math.Abs(delta.X), Math.Abs(delta.Y), Math.Abs(delta.Z));  // abs(delta) goes back to the translation tolerance (0x00628248)
            capturedA = ang;                                                                 // and to the angle tolerance (0x00628250)
            return true;
        });
        var closest = FindLocatedObjectHelper(filter, null, false);
        if (closest is not null) return closest;
        ObservableObject? match = null;                                                     // 0x005065C4..0x005066B4
        var narrowed = tol;
        List<ObservableObject> pending;
        lock (_gate) pending = _confirmations.Values.Select(e => e.Stored).Where(s => s is not null && s.Type == observed.Type).Select(s => s!).ToList();
        foreach (var p in pending)
        {
            if (!p.Pose.IsSameAs(observed.Pose, narrowed, Math.PI, out var d)) continue;
            narrowed = new Vec3(Math.Abs(d.X), Math.Abs(d.Y), Math.Abs(d.Z));
            match = p;
        }
        return match;
    }

    /// <summary>
    /// <c>ObjectPoseConfirmer::IsObjectConfirmedAtObservedPose(instance, &amp;match)</c> 0x0050634C: fills <paramref name="match"/> from
    /// <see cref="FindObjectMatchForObservation"/>, then looks the confirmer entry up by the MATCH's ID and requires its count to be at
    /// least 2 (0x00506376) and the instance's pose to match the entry's pose within the instance's tolerances (0x005063B8).
    /// </summary>
    // fidelity: M11-007
    internal bool IsObjectConfirmedAtObservedPose(ObservableObject instance, out ObservableObject? match)
    {
        match = FindObjectMatchForObservation(instance);
        if (match is null) return false;
        var entry = EntryFor(match.ObjectId);
        if (entry is null || entry.Count < 2) return false;
        return instance.Pose.IsSameAs(entry.Pose, ObjectMatchToleranceMm(instance.Type), ObjectMatchAngleRad, out _);
    }

    /// <summary>
    /// <c>ObjectPoseConfirmer::AddVisualObservation(observed, existing, wasCameraMoving, dist)</c> 0x0050684C; the return is the entry's
    /// count &gt; 1 (0x00506AC2..0x00506ACC).
    /// <list type="bullet">
    /// <item>No entry for the ID, or an entry while neither an existing located object nor a stored one is there: the entry becomes
    ///   <c>PoseConfirmation(observed, 1, 0)</c> holding the instance itself; false (0x0050689A..0x00506982). Nothing else is written.</item>
    /// <item>Otherwise <c>Pose3d::IsSameAs(observed, entry pose)</c> (0x005069FC). Match: the count increments; an old count of 0 (after
    ///   a miss) updates only a stored instance; an old count of 1 or more runs <c>UpdatePoseInInstance(stored, ...)</c> then
    ///   <c>AddLocatedObject(stored)</c> then clears the stored pointer, or with none stored <c>UpdatePoseInInstance(existing, ...)</c>
    ///   (0x00506A04..0x00506A98). Mismatch: the count is 1 and the entry pose is the observed pose (0x00506A46..0x00506A4E), then a stored
    ///   instance is updated. Then the miss count is 0 and +0x30 is the observed timestamp (0x00506A9C..0x00506AA4).</item>
    /// </list>
    /// <c>MapComponent::ClearRobotToMarkers</c> (0x00506A98) is the map's (M13) and is not built.
    /// </summary>
    // fidelity: M11-007
    internal bool AddVisualObservation(ObservableObject observed, ObservableObject? existing, bool wasCameraMoving, double dist)
    {
        PoseConfirmation entry;
        bool remade;
        lock (_gate)
        {
            bool found = _confirmations.TryGetValue((int)observed.ObjectId, out entry!);
            remade = !found || (existing is null && entry!.Stored is null);
            if (remade)
                _confirmations[(int)observed.ObjectId] = entry = new PoseConfirmation { Pose = observed.Pose, Stored = observed, Count = 1, Misses = 0, LastVisuallyMatchedTime = observed.LastObservedTimestamp };   // the ctor stores observed[+0x1C] (0x00506310), copied to node+0x30 (0x005068BE, 0x00506990)
        }
        observed.Confirmation = entry;
        if (existing is not null) existing.Confirmation = entry;
        if (remade)
        {
            Log?.Invoke($"ObjectPoseConfirmer.AddVisualObservation.NewEntry: ObjectID:{(int)observed.ObjectId} at {observed.Pose.Translation}, currently {observed.PoseState}");
            return false;
        }

        var pose = observed.Pose;
        if (observed.Pose.IsSameAs(entry.Pose, ObjectMatchToleranceMm(observed.Type), ObjectMatchAngleRad, out _))     // 0x005069FC
        {
            int oldCount = entry.Count;
            entry.Count = oldCount + 1;                                                                                  // 0x00506A04..0x00506A0C
            if (oldCount >= 1)
            {
                if (entry.Stored is { } stored)
                {
                    UpdatePoseInInstance(stored, observed, existing, pose, wasCameraMoving, dist);                       // 0x00506A26
                    AddLocatedObject(stored);                                                                            // 0x00506A32
                    entry.Stored = null;                                                                                 // 0x00506A38..0x00506A44
                }
                else if (existing is not null)
                    UpdatePoseInInstance(existing, observed, existing, pose, wasCameraMoving, dist);                     // 0x00506A86
                // MapComponent::ClearRobotToMarkers(obj) (0x00506A98) belongs to the map (M13) and is not built.
            }
            else if (entry.Stored is { } pending)
                UpdatePoseInInstance(pending, observed, existing, pose, wasCameraMoving, dist);                          // 0x00506A52
        }
        else
        {
            entry.Count = 1; entry.Pose = observed.Pose;                                                                 // 0x00506A46..0x00506A4E
            if (entry.Stored is { } pending)
                UpdatePoseInInstance(pending, observed, existing, pose, wasCameraMoving, dist);                          // 0x00506A52
        }
        entry.Misses = 0;                                                                                                // 0x00506A9C..0x00506AA4
        entry.LastVisuallyMatchedTime = observed.LastObservedTimestamp;
        return entry.Count > 1;                                                                                          // 0x00506AC2..0x00506ACC
    }

    /// <summary>
    /// <c>ObjectPoseConfirmer::UpdatePoseInInstance(instance, observed, existing, pose, wasCameraMoving, dist)</c> 0x00505DE0..0x00505FEC:
    /// where Known or Dirty is decided (M11-007). <c>isCarried</c> is <c>IsCarryingObject(existing.ID)</c> when there is an existing object;
    /// <c>notMoving</c> is <c>!IsMoving</c> of the connected object with the instance's ID, else of the existing object, else true;
    /// <c>closeAndSteady = OnTreads &amp;&amp; (250.0 + 1e-5 &gt;= dist) &amp;&amp; !wasCameraMoving &amp;&amp; notMoving</c> in float32 (the comparison
    /// is true for a NaN distance, 0x00505E5E..0x00505E92); the new state is Known when closeAndSteady, else Dirty (0x00505EB4..0x00505EBA).
    /// The update is SKIPPED when the instance is not the docking object and not carried and either (there is an existing object and the
    /// camera was moving) or (instance is existing, the object is localizable, not closeAndSteady and Known) (0x00505E98..0x00505EFA).
    /// A different instance just takes <c>SetPose(pose, dist, state)</c> (0x00505EC4..0x00505ECE); the existing object goes through the write
    /// helper 0x0050609C with the pose clamped flat by 20 degrees when it is the docking object (0x00505F10..0x00505F56), and a carried one
    /// is then unset as carried; the entry's +0x2C is the observed timestamp (0x00505FC0..0x00505FE2).
    /// The second skip clause's <c>CanBeUsedForLocalization</c> is <see cref="CanBeUsedForLocalization"/>. UNREAD (M11-042): whether the "not docking and not carried" test guards both skip clauses or only the first (the row's
    /// wording is ambiguous; this reads it as guarding both).
    /// </summary>
    // fidelity: M11-007
    private void UpdatePoseInInstance(ObservableObject instance, ObservableObject observed, ObservableObject? existing, Pose3d pose, bool wasCameraMoving, double dist)
    {
        bool isCarried = existing is not null && Carrying(existing.ObjectId);                                            // 0x00505DFE..0x00505E0E
        var connected = GetConnectedActiveObjectByIdHelper(instance.ObjectId);                                           // 0x00505E2A
        bool notMoving = connected is not null ? !connected.IsMoving : existing is not null ? !existing.IsMoving : true; // 0x00505E36..0x00505E52
        float thresholdF = (float)MaxLocalizationDistanceMm + 1e-5f;
        bool closeEnough = !((float)dist > thresholdF);                                                                  // vcmpe + pl: true for NaN
        bool closeAndSteady = OnTreadsNow() && closeEnough && !wasCameraMoving && notMoving;
        var newState = closeAndSteady ? PoseState.Known : PoseState.Dirty;
        bool isDockObject = DockingObjectId?.Invoke() is { } dock && dock == instance.ObjectId;                          // 0x00505EA8
        if (!isDockObject && !isCarried)
        {
            if (existing is not null && wasCameraMoving) return;                                                          // first clause
            if (ReferenceEquals(instance, existing) && !closeAndSteady && instance.PoseState == PoseState.Known
                && CanBeUsedForLocalization(instance)) return;                                                            // second clause (vtable +0x18, 0x00505E1E)
        }
        if (!ReferenceEquals(instance, existing))
        {
            instance.SetPose(pose, dist, newState);                                                                       // 0x00505EC4..0x00505ECE
        }
        else
        {
            if (isDockObject) WritePoseHelper(instance, ClampPoseToFlat(pose, FlatClampAngleRad), dist, newState);        // "UpdateClampedPoseInInstance"
            else WritePoseHelper(instance, pose, dist, newState);                                                         // "UpdatePoseInInstance"
            if (isCarried)
            {
                Log?.Invoke($"ObjectPoseConfirmer.SeeingCarriedObject: We changed the pose of {existing!.ObjectId}, we must not be carrying it anymore. Unsetting as carried object.");
                if (UnSetCarryObject is { } unset) unset(existing!.ObjectId); else UnwiredCarryUnsetCalls++;
            }
        }
        var entry = EntryFor(instance.ObjectId);
        if (entry is not null) entry.LastPoseUpdatedTime = observed.LastObservedTimestamp;                                // 0x00505FC0..0x00505FE2
    }

    /// <summary>
    /// <c>ObservableObject</c> vtable +0x18, <c>CanBeUsedForLocalization</c>. An ActiveCube's is <c>ActiveObject::CanBeUsedForLocalization</c> 0x004E49AC (thunk 0x004E4ACC): the object's
    /// PoseState must be Known (<c>[+0x24] == 1</c>); then vptr+8 is called with (this, 0) (0x004E49C2..0x004E49CE), which in the ActiveObject group is <c>IsMoving(uint*)</c> 0x004E3844
    /// (it returns byte [this+4]), and a result of 1 warns (0x004E49DE) and returns 0: a cube reported as moving is not usable; then its activeID (<c>[+0x40]</c>) and its fromDistance
    /// (<c>[+0x10]</c>) must be at least 0 (0x004E4A06..0x004E4A28), and then it is <c>IsRestingFlat(Radians(5.0 * 0.0174533))</c> (vtable +0x1C returns 5.0, 0x004E4A30). The movement flag is
    /// the one this stack tracks from the cube's own moved and stopped reports (<see cref="ObservableObject.IsMoving"/>, M11-009). The Charger's slot (group vptr 0x0101E1C4, +0x18 =
    /// 0x0101E1DC) is 0x004EA66E, <c>movs r0,#0; bx lr</c>: false; every other non-cube type reaches the <c>ObservableObject</c> default 0x004E024A: false.
    /// </summary>
    // fidelity: M11-007, M11-044
    internal bool CanBeUsedForLocalization(ObservableObject o)
    {
        if (!CubeGeometry.IsCube(o.Type)) return false;
        if (o.PoseState != PoseState.Known) return false;
        if (o.IsMoving)
        {
            Log?.Invoke($"ActiveObject.CanBeUsedForLocalization: object {o.ObjectId} is moving");
            return false;
        }
        return o.ActiveId >= 0 && o.FromDistance >= 0 && o.IsRestingFlat(5.0 * DegToRadDecimal);
    }

    /// <summary>
    /// The write helper 0x0050609C(obj, pose, dist, newState): state 0 is <c>MarkObjectUnknown(obj, true)</c> (0x0050610A..0x00506116);
    /// state 2 (Dirty, 0x005060AC/0x005060AE) first calls <c>SetLocalizedTo(nullptr)</c> when [[Robot]+0x2B8] equals the object's ID (0x005060B2..0x005060C4); then the old pose and state are kept, <c>SetPose(pose, dist, state)</c> stores them, and
    /// <c>BroadcastObjectPoseChanged(obj, oldPose, oldState)</c> follows (0x005060E0..0x005060FA).
    /// </summary>
    // fidelity: M11-007
    private void WritePoseHelper(ObservableObject obj, Pose3d pose, double dist, PoseState newState)
    {
        if (newState == PoseState.Unknown) { MarkObjectUnknown(obj, true); return; }
        if (newState == PoseState.Dirty && LocalizedToObjectId == obj.ObjectId) SetLocalizedTo(null);
        var oldPose = obj.Pose; var oldState = obj.PoseState;
        obj.SetPose(pose, dist, newState);
        BroadcastObjectPoseChanged(obj, oldPose, oldState);
    }

    /// <summary>
    /// <c>ObjectPoseConfirmer::SetPoseStateHelper(obj, state)</c> 0x0050612C: Invalid is refused with the
    /// <c>CantSetInvalidPoseState</c> error and nothing is written (0x0050617A); otherwise the state byte is stored and, when the pose is in
    /// the world origin, <c>BroadcastObjectPoseStateChanged(obj, oldState)</c> follows (0x0050614E..0x00506176). A Dirty state first clears
    /// <c>SetLocalizedTo(nullptr)</c> when [[Robot]+0x2B8] equals the object's ID (0x0050613C..0x0050614A).
    /// </summary>
    // fidelity: M11-007
    internal bool SetPoseStateHelper(ObservableObject obj, PoseState state)
    {
        if (state == PoseState.Unknown)
        {
            Log?.Invoke("ObjectPoseConfirmer.SetPoseStateHelper.CantSetInvalidPoseState: cannot set PoseState to Invalid");
            return false;
        }
        if (state == PoseState.Dirty && LocalizedToObjectId == obj.ObjectId) SetLocalizedTo(null);
        var old = obj.PoseState;
        obj.PoseState = state;
        if (old != state) PoseStateChanged?.Invoke(obj, old, state);
        return true;
    }

    /// <summary>
    /// <c>ObjectPoseConfirmer::MarkObjectDirty(obj, propagate)</c> 0x005075A4: <c>SetPoseStateHelper(obj, Dirty)</c> (0x005075B2) and, with
    /// <paramref name="propagate"/>, the object on top within 15 mm (a filter that ignores the object's own ID and families 6 and 7, then
    /// <c>FindObjectOnTopOrUnderneathHelper(obj, 15.0, filter, true)</c>, 0x0050762C..0x00507664) is marked Dirty in turn unless it is carried
    /// (0x005076C2).
    /// </summary>
    // fidelity: M11-007
    internal void MarkObjectDirty(ObservableObject obj, bool propagate)
    {
        SetPoseStateHelper(obj, PoseState.Dirty);
        if (!propagate) return;
        if (FindObjectOnTopOrUnderneathHelper(obj, StackToleranceMm, true) is { } other)
        {
            if (Carrying(other.ObjectId)) Log?.Invoke($"ObjectPoseConfirmer.MarkObjectDirty: object {other.ObjectId} is carried; not marked dirty");
            else MarkObjectDirty(other, true);
        }
    }

    /// <summary>
    /// <c>ObjectPoseConfirmer::MarkObjectUnobserved(obj)</c> 0x00506FBC: the entry's count becomes 0 and its miss count the old value plus one
    /// (<c>strd r2,r1,[r0,#0x24]</c> at 0x00506FE0); when the miss count read was already 1 or more (0x00506FDE) it logs MakingUnknown and calls
    /// <c>MarkObjectUnknown(obj, true)</c> (0x0050702E). It returns 1 only when the object has no entry (the ObjectNotFound error). So two
    /// consecutive misses forget an object, and a sighting in between (miss count 0, +0x00506A9C) restarts the count.
    /// </summary>
    // fidelity: M11-007
    internal uint MarkObjectUnobserved(ObservableObject obj, List<ObservableObject>? forgotten = null)
    {
        var entry = EntryFor(obj.ObjectId);
        if (entry is null)
        {
            Log?.Invoke($"ObjectPoseConfirmer.MarkObjectUnobserved.ObjectNotFound: object {obj.ObjectId}");
            return 1;
        }
        int oldMisses = entry.Misses;
        entry.Count = 0;
        entry.Misses = oldMisses + 1;
        if (oldMisses + 1 >= UnobservedMissesToUnknown)
        {
            Log?.Invoke($"ObjectPoseConfirmer.MarkObjectUnobserved.MakingUnknown: object {obj.ObjectId}");
            var gone = MarkObjectUnknown(obj, true);
            forgotten?.AddRange(gone);
        }
        return 0;
    }

    /// <summary>
    /// <c>ObjectPoseConfirmer::MarkObjectUnknown(obj, propagate)</c> 0x00507128 (M11-007, verified in 20260929-R-VIS-verify-M11-batchA.md objection 1 and -batchA2.md objection 1):
    /// it writes NO PoseState. With propagate (0x00507162) the set starts as {the object's ID} (0x0050715E) and the walk goes UPWARD ONLY: each round builds a filter that ignores the
    /// current object's ID (0x00507220) and families 6 and 7 (0x00507224..0x00507240) and calls <c>FindObjectOnTopOrUnderneathHelper(current, 15.0, filter, onTop = true)</c>
    /// (0x00507252..0x00507258, the only call of that helper in the function). A result that is not carried joins the set and becomes the current object (0x005072C6). A carried result
    /// is warned about (0x00507288) and is NOT added, but it still becomes the current object (<c>mov r7, fp</c> at 0x0050728E, then <c>cmp r7,#0; bne 0x005071D2</c> at 0x0050731C..0x0050731E):
    /// the walk CONTINUES and the objects on top of the carried cube are found and deleted. The walk ends when nothing is found on top. Nothing underneath is ever looked at. Then
    /// <c>RobotMarkedObjectPoseUnknown</c> is broadcast once per set entry, each with the ORIGINAL object's ID (0x005073AA..0x005073B8), and <c>DeleteLocatedObjects</c> removes the set
    /// (0x005073F4); the confirmer entry is not erased. The deleted objects are returned. The broadcast has no message type in this stack (M11-038): <see cref="PoseStateChanged"/>
    /// reports each deletion.
    /// </summary>
    // fidelity: M11-007
    internal IReadOnlyList<ObservableObject> MarkObjectUnknown(ObservableObject obj, bool propagate)
    {
        var victims = new List<ObservableObject> { obj };
        if (propagate)
        {
            var current = obj;
            var visited = new HashSet<ObservableObject> { obj };
            while (true)
            {
                var above = FindObjectOnTopOrUnderneathHelper(current, StackToleranceMm, true);
                if (above is null) break;
                if (!visited.Add(above)) break;                      // stand-in: the engine has no revisit guard (a cycle of poses would spin); geometry does not allow one, so this never fires
                if (Carrying(above.ObjectId))
                    Log?.Invoke($"ObjectPoseConfirmer.MarkObjectUnknown: object {above.ObjectId} is carried; it is not marked unknown");
                else victims.Add(above);
                current = above;                                      // the carried object is the next current object too (0x0050728E)
            }
        }
        var f = new BlockWorldFilter();
        foreach (var v in victims) f.AllowedIds.Add(v.ObjectId);
        return DeleteLocatedObjects(f);
    }

    /// <summary>
    /// <c>BlockWorld::DeleteLocatedObjects(filter)</c>: every located object the filter passes leaves the located map (and reaches
    /// <see cref="PoseStateChanged"/> as Unknown, this stack's channel for the pose-changed broadcast at 0x0061D5C8). The deleted object's
    /// own fields are not written: in the engine the object is freed. The engine's other effects there (the map's RemoveObservableObject,
    /// the pose-changed broadcast's arguments) are unread (M11-042).
    /// </summary>
    // fidelity: M11-007, M11-042
    internal List<ObservableObject> DeleteLocatedObjects(BlockWorldFilter filter)
    {
        var victims = FindLocatedMatchingObjects(filter);
        var deleted = new List<ObservableObject>();
        foreach (var o in victims)
        {
            PoseState prev = o.PoseState;
            lock (_gate) { if (!_located.Remove(KeyOf(o))) continue; }
            deleted.Add(o);
            PoseStateChanged?.Invoke(o, prev, PoseState.Unknown);
        }
        return deleted;
    }

    /// <summary>
    /// <c>BlockWorld::AddLocatedObject(obj)</c> 0x00622ADE..0x00622E4E (M11-013): a connected object with the object's ID hands its ActiveID
    /// and FactoryID to an active object (else the NotActive warnings); with no connected object they stay -1 and 0, so there is NO
    /// connection requirement. A ghost cube is an error and is not added. <c>DeleteIntersectingObjects</c> runs, the object goes into the
    /// map under its origin, family, type and ID, "Adding new ..." is logged and <c>BroadcastObjectPoseChanged(obj, null, Invalid)</c>
    /// follows (0x00622E4E, so the old state is Invalid).
    /// </summary>
    // fidelity: M11-013
    internal void AddLocatedObject(ObservableObject obj)
    {
        var connected = GetConnectedActiveObjectByIdHelper(obj.ObjectId);
        if (connected is not null)
        {
            if (obj.IsActive) { obj.ActiveId = connected.ActiveId; obj.FactoryId = connected.FactoryId; }
            else Log?.Invoke("ObservableObject.SetActiveID.NotActive: the object is not active");
        }
        if (obj.Type == ObjectType.Block_LIGHTCUBE_GHOST)
        {
            Log?.Invoke("BlockWorld.AddLocatedObject.AddingGhostObject");
            return;
        }
        DeleteIntersectingObjects(obj);
        lock (_gate)
        {
            _located[KeyOf(obj)] = obj;
            _forgotten.Remove(obj.ObjectId);
        }
        var link = EntryFor(obj.ObjectId);
        if (link is not null) obj.Confirmation = link;
        Log?.Invoke($"Adding new {obj.Type} object and ID={(int)obj.ObjectId} ActID={obj.ActiveId} FacID=0x{obj.FactoryId:x} at {obj.Pose.Translation}");
        BroadcastObjectPoseChanged(obj, null, PoseState.Unknown);
    }

    /// <summary>
    /// <c>DeleteIntersectingObjects(obj, 0, filter)</c>, called by <c>AddLocatedObject</c> at 0x00622C84. NOT BUILT: its body is unread and is in
    /// no record; this stub makes the missing deletion visible through <see cref="UnbuiltDeleteIntersectingObjectsCalls"/>.
    /// </summary>
    // fidelity: M11-042
    private void DeleteIntersectingObjects(ObservableObject obj) => UnbuiltDeleteIntersectingObjectsCalls++;

    /// <summary>
    /// <c>ObjectPoseConfirmer::BroadcastObjectPoseChanged(obj, oldPose, oldState)</c> 0x00506F88 reaches <c>BlockWorld::OnObjectPoseChanged</c>
    /// 0x00624808 (0x00506F98), which appends a <c>PoseChange</c> (the object's ID, old pose, old state) to the list at this+0x68 only while
    /// this+0x74 is set (0x00624814) and passes the object's current state to the BlockConfigurationManager (0x0062488C..0x00624892). UNREAD
    /// (M11-042): the body between the gate and the append, so a null old pose (<c>AddLocatedObject</c> passes one) appends nothing here.
    /// <see cref="PoseStateChanged"/> carries the state change for this stack's consumers.
    /// </summary>
    // fidelity: M11-037, M11-042
    internal void BroadcastObjectPoseChanged(ObservableObject obj, Pose3d? oldPose, PoseState oldState)
    {
        lock (_gate)
            if (_appendPoseChanges && oldPose is { } p) _poseChanges.Add(new PoseChange(obj.ObjectId, p, oldState));
        if (oldState != obj.PoseState) PoseStateChanged?.Invoke(obj, oldState, obj.PoseState);
    }

    // ------------------------------------------------------------------------- CheckForUnobservedObjects

    /// <summary>
    /// <c>BlockWorld::CheckForUnobservedObjects(ts)</c> 0x00621C6C (M11-037, M11-004, M11-007, M11-008). It does nothing when the robot is not on
    /// its treads (Robot+0x355), was moving at the timestamp (<c>WasMoving</c>, the IS_MOVING bit), was rotating too fast
    /// (<c>WasRotatingTooFast(ts, 0.174533, 0.174533, 0)</c>) or the current origin holds no objects (0x00621C7C..0x00621CFA). Candidates are the
    /// objects of the current origin not visually matched this frame, not carried, not the docking object and not of family 4 (0x00621D54..0x00621E80);
    /// an object with no markers is skipped silently (0x00621EE8..0x00621EEC). Each is tested with <c>IsVisibleFrom(camera, 0.785398, 40.0, xPad,
    /// yPad, hasNothingBehind)</c> and <c>MarkObjectUnobserved</c> runs when it is visible, or when it is not, hasNothingBehind is set and its
    /// pose is Dirty (0x00622106..0x0062218E). The objects that leave the world are returned.
    ///
    /// NOT BUILT (visible): the xPad and yPad terms (0x0062206E..0x006220DA: the length of the camera-to-object translation, <c>GetMaxLocalizationDistance_mm</c>,
    /// <c>(2.0 - d/max)</c>, and fields at calib+4/+8 and node+0x18/+0x1C whose names are unread), so both pads are 0 (<see cref="PaddingIsBuilt"/> is false).
    /// </summary>
    // fidelity: M11-004, M11-007, M11-008, M11-010, M11-037, M11-042
    public IReadOnlyList<ObservableObject> CheckForUnobservedObjects(CameraModel camera, uint timestamp, bool robotMoving, bool rotatingTooFast)
    {
        var forgotten = new List<ObservableObject>();
        if (!OnTreadsNow()) return forgotten;
        if (robotMoving) return forgotten;
        if (rotatingTooFast) return forgotten;
        List<ObservableObject> current;
        lock (_gate) current = _located.Where(kv => kv.Key.Origin == CurrentOriginId).Select(kv => kv.Value).ToList();
        if (current.Count == 0) return forgotten;
        uint? dock = DockingObjectId?.Invoke();
        var candidateIds = new List<uint>();
        foreach (var o in current)
        {
            // GetLastVisuallyMatchedTime(ID) < ts (0x00621D64, cmp r0,r5; bhs skip at 0x00621D68): the getter reads [confirmer entry + 0x30], 0 without an entry
            // (0x00507824..0x0050783A). AddVisualObservation writes it (0x00506A9C..0x00506AA4), and a confirmed object reaches that every frame through Insert.
            if (!((EntryFor(o.ObjectId)?.LastVisuallyMatchedTime ?? 0) < timestamp)) continue;
            if (Carrying(o.ObjectId)) continue;
            if ((dock is { } d && d == o.ObjectId) || o.Family == ObjectFamily.Charger) continue;
            candidateIds.Add(o.ObjectId);
        }
        foreach (var id in candidateIds)
        {
            var o = GetLocatedObjectByIdHelper(id);
            if (o is null) continue;
            if (o.Markers.Count == 0) continue;
            bool visible = o.IsVisibleFrom(camera, VisibilityNormalAngleRad, MinVisibleMarkerSizePx, 0, 0,
                                           out var reason, out bool hasNothingBehind);
            bool dirtyWithNothingBehind = !visible && hasNothingBehind && o.PoseState == PoseState.Dirty;
            if (!visible && !dirtyWithNothingBehind) continue;
            Log?.Invoke($"object {o.ObjectId} unobserved (shouldBeVisible:{visible} hasNothingBehind:{hasNothingBehind} reason:{reason})");
            if (MarkObjectUnobserved(o, forgotten) != 0) Log?.Invoke("BlockWorld.CheckForUnobservedObjects.MarkObjectUnobservedFailed");
        }
        return forgotten;
    }

    // ----------------------------------------------------------------------------------- lift occluder

    /// <summary>
    /// The eight occluder points <c>VisionComponent</c> carries at +4 (M11-037). They start empty (the constructor zeroes the vector,
    /// 0x006500CA/0x006500D0) and are written only by <c>VisionComponent::SetPhysicalRobot</c> 0x00657F3C, see <see cref="SetPhysicalRobot"/>.
    /// </summary>
    // fidelity: M11-037
    public IReadOnlyList<Vec3> LiftOccluderPoints { get; set; } = Array.Empty<Vec3>();

    /// <summary>
    /// <c>VisionComponent::SetPhysicalRobot(bool)</c> 0x00657F3C (called by <c>Robot::SetPhysicalRobot</c> 0x0051397C, whose only caller is
    /// <c>HandleFirmwareVersion</c> 0x00536980 with <c>json["sim"].isNull()</c>): builds eight points (4,-15,-20), (4,15,-20), (-1,-15,-20),
    /// (-1,15,-20) and the same four with z = s, where s is -36.5 for a non-zero argument (0xC2120000 at 0x00658050) and -28.5 for zero
    /// (0xC1E40000 at 0x0065804C) (<c>cmp r1,#0; vmovne</c> at 0x00657F54..0x00657F5A).
    /// </summary>
    // fidelity: M11-037
    public void SetPhysicalRobot(bool physical)
    {
        double s = physical ? -36.5 : -28.5;
        LiftOccluderPoints = new[]
        {
            new Vec3(4, -15, -20), new Vec3(4, 15, -20), new Vec3(-1, -15, -20), new Vec3(-1, 15, -20),
            new Vec3(4, -15, s), new Vec3(4, 15, s), new Vec3(-1, -15, s), new Vec3(-1, 15, s),
        };
    }

    /// <summary>
    /// <c>VisionComponent::AddLiftOccluder(unsigned int)</c> 0x006564D8 (C3.2/Q2.1, Q8): the raw robot state at the timestamp ->
    /// <c>Robot::GetLiftTransformWrtCamera(liftAngle, headAngle)</c> 0x004BA6F8 -> <c>Transform3d::ApplyTo</c> the occluder points (0x0065653E) ->
    /// <c>Camera::Project3dPoints</c> (0x00656550) -> <c>Camera::AddOccluder(points, f)</c> (0x00656598) where f is the LENGTH of the transform's
    /// translation, <c>sqrt(x^2 + y^2 + z^2)</c> of the three floats at Transform3d+0x20..+0x28 (0x00656554..0x00656598), not a z scale. With no
    /// points (before <see cref="SetPhysicalRobot"/>) it files nothing.
    /// </summary>
    // fidelity: M11-037
    public void AddLiftOccluder(CameraModel camera, VisionPoseData pd)
    {
        if (LiftOccluderPoints.Count == 0) return;
        var liftInCamera = camera.Pose.Inverse().Compose(pd.RobotPose.Compose(LiftGeometry.LiftPoseInRobotFrame(pd.LiftAngleRad)));
        var px = new Vec2[LiftOccluderPoints.Count];
        for (int i = 0; i < LiftOccluderPoints.Count; i++)
        {
            var p = camera.Project(liftInCamera.Apply(LiftOccluderPoints[i]));
            if (p is null) return;
            px[i] = p.Value;
        }
        camera.Occluders.Add(px, liftInCamera.Translation.Length);
    }

    // ------------------------------------------------------------------------------ stacked and markerless

    /// <summary>
    /// <c>BlockWorld::UpdatePoseOfStackedObjects()</c> 0x00621794 (M11-037; it is BlockWorld's, not M10's or M12's). For each <c>PoseChange</c> (an
    /// object's old pose and state) whose object is still located: a clone (fresh instance) is given the OLD pose and state; the stacked object is
    /// the first located object of the current origin, not the object itself, not of family 6 or 7, not observed in the last 100 ms
    /// (<c>ts + 100 &lt; GetLastImageTimeStamp</c>, 0x00629BAE), that <c>FindObjectOnTopOrUnderneathHelper(clone, 15.0, filter, true)</c> (M13-007's)
    /// finds; it is skipped when none, when carried or when its own ID is in the list; its new pose is its pose with respect to the clone's old pose
    /// pre-composed with the object's new pose, written by <c>AddObjectRelativeObservation</c> (0x006219C0). The list is not cleared here.
    /// </summary>
    // fidelity: M11-037
    public void UpdatePoseOfStackedObjects()
    {
        List<PoseChange> changes;
        uint lastImage;
        lock (_gate) { changes = _poseChanges.ToList(); lastImage = _lastImageTimestamp; }
        foreach (var e in changes)
        {
            var obj = GetLocatedObjectByIdHelper(e.Id);
            if (obj is null)
            {
                Log?.Invoke($"BlockWorld.UpdateStacks: '{(int)e.Id}' does not exist in current frame. Ignoring change.");
                continue;
            }
            var clone = obj.Clone();
            clone.SetPose(e.Pose, -1.0, e.State);
            var stacked = FindObjectOnTopOrUnderneathHelper(clone, StackToleranceMm, true, obj.ObjectId,
                                                            c => unchecked(c.LastObservedTimestamp + 100) < lastImage);
            if (stacked is null || Carrying(stacked.ObjectId) || changes.Any(c => c.Id == stacked.ObjectId)) continue;
            var newPose = obj.Pose.Compose(stacked.Pose.WithRespectTo(e.Pose));
            if (AddObjectRelativeObservation(stacked, newPose, obj) != 0)
            {
                Log?.Invoke("BlockWorld.UpdateStacks.AddRelativeObservationFailed: Giving up on rest of stack");
                break;
            }
        }
    }

    /// <summary>
    /// <c>ObjectPoseConfirmer::AddObjectRelativeObservation(obj, pose, observed)</c> 0x00506CD8 (M11-037): an object that is not in the world is the
    /// NotABlockWorldObject error, and the rest still runs (0x00506D56); <c>SetPose(pose, -1.0, Dirty)</c> (0x00506D3C..0x00506D3E), the pose-changed
    /// broadcast, and the confirmer entry's pose and +0x2C (the observed object's timestamp). It returns 0. (The robot's
    /// <c>SetLocalizedTo(null)</c> when localized to the object is the robot's.)
    /// </summary>
    // fidelity: M11-037
    private uint AddObjectRelativeObservation(ObservableObject obj, Pose3d pose, ObservableObject observed)
    {
        bool inWorld;
        lock (_gate) inWorld = _located.ContainsKey(KeyOf(obj));
        if (!inWorld) Log?.Invoke($"ObjectPoseConfirmer.AddObjectRelativeObservation.NotABlockWorldObject: {obj.ObjectId}");
        var oldPose = obj.Pose; var oldState = obj.PoseState;
        if (inWorld) { obj.SetPose(pose, -1.0, PoseState.Dirty); BroadcastObjectPoseChanged(obj, oldPose, oldState); }
        var entry = EntryForOrCreate(obj.ObjectId);
        obj.Confirmation = entry;
        entry.Pose = pose;
        entry.LastPoseUpdatedTime = observed.LastObservedTimestamp;
        return 0;
    }

    /// <summary>
    /// The candidates <c>FindObjectOnTopOrUnderneathHelper(obj, tolerance, filter, onTop)</c> 0x0062601C is given: the located objects of the current
    /// origin, none of family 6 or 7, not the ignored ID, and whatever the extra predicate passes. The search itself is M13-007's
    /// <see cref="BlockConfigurationManager.FindObjectOnTopOrUnderneath(ObservableObject, IReadOnlyList{ObservableObject}, bool, double)"/>.
    /// </summary>
    // fidelity: M11-037
    private ObservableObject? FindObjectOnTopOrUnderneathHelper(ObservableObject obj, double toleranceMm, bool onTop, uint? ignoreId = null,
                                                                Func<ObservableObject, bool>? extra = null)
    {
        uint skip = ignoreId ?? obj.ObjectId;
        List<ObservableObject> candidates;
        lock (_gate)
            candidates = _located.Where(kv => kv.Key.Origin == CurrentOriginId).Select(kv => kv.Value)
                                 .Where(o => o.ObjectId != skip && o.Family != ObjectFamily.MarkerlessObject && o.Family != ObjectFamily.CustomObject)
                                 .ToList();
        if (extra is not null) candidates = candidates.Where(extra).ToList();
        return BlockConfigurationManager.FindObjectOnTopOrUnderneath(obj, candidates, onTop, toleranceMm);
    }

    /// <summary>
    /// <c>BlockWorld::UpdateMarkerlessObjects(ts)</c> 0x00625704 (M11-037): <c>DeleteLocatedObjects</c> over the objects of family 6 and type 15
    /// (CliffDetection) whose predicate 0x0062B764 is true: the object's timestamp + 30000 is before ts ("RemovingExpired"); else false when the
    /// object's timestamp is not before ts; else true when its z lies within [robotZ, robotZ + 67.7] (0x42876666) and its footprint intersects the
    /// robot's bounding quad ("RemovingIntersectWithRobot"). NOT BUILT, visible (M13-023): the markerless object's <c>GetBoundingQuadXY</c> is
    /// unread, so the last test throws <see cref="NotSupportedException"/> when it is reached.
    /// </summary>
    // fidelity: M11-037
    public void UpdateMarkerlessObjects(uint timestamp, Pose3d robotPose)
    {
        var f = new BlockWorldFilter();
        f.AllowedFamilies.Add(ObjectFamily.MarkerlessObject);
        f.AllowedTypes.Add(ObjectType.CliffDetection);
        float robotZ = (float)robotPose.Translation.Z;
        f.Predicates.Add(o =>
        {
            if (unchecked(o.LastObservedTimestamp + 30000) < timestamp)
            {
                Log?.Invoke($"BlockWorld.UpdateMarkerlessObjects.RemovingExpired: object {o.ObjectId}");
                return true;
            }
            if (o.LastObservedTimestamp >= timestamp) return false;
            float z = (float)o.Pose.Translation.Z;
            if (!(z >= robotZ && z <= robotZ + 67.7f)) return false;
            throw new NotSupportedException($"M13-023: the footprint (GetBoundingQuadXY) of a markerless {o.Type} is unread; UpdateMarkerlessObjects' RemovingIntersectWithRobot test (0x0062B764) cannot be evaluated");
        });
        DeleteLocatedObjects(f);
    }

    private static double QuadArea(Vec2[] c)
    {
        var q = new[] { c[0], c[2], c[3], c[1] };
        double a = 0;
        for (int i = 0; i < 4; i++) a += q[i].X * q[(i + 1) % 4].Y - q[(i + 1) % 4].X * q[i].Y;
        return Math.Abs(a) / 2;
    }

    /// <summary>
    /// <c>ObservableObject::ClampPoseToFlat</c> 0x00877330: a cube resting on a surface has one axis
    /// vertical; when the solved pose is within <paramref name="toleranceRad"/> of that, snap it. The
    /// engine takes the rotated parent Z axis, <c>acos</c> of the magnitude of its largest component, and
    /// compares that with the angle it was given (0x0087736A..0x0087737E).
    /// </summary>
    // fidelity: M11-006
    public static Pose3d ClampPoseToFlat(Pose3d pose, double toleranceRad = FlatClampAngleRad)
    {
        var r = pose.Rotation;
        // find the object axis closest to world Z
        var zRow = new Vec3(r[2, 0], r[2, 1], r[2, 2]);
        double[] comps = { zRow.X, zRow.Y, zRow.Z };
        int axis = 0;
        for (int i = 1; i < 3; i++) if (Math.Abs(comps[i]) > Math.Abs(comps[axis])) axis = i;
        double tilt = Math.Acos(Math.Clamp(Math.Abs(comps[axis]), 0, 1));
        if (tilt > toleranceRad || tilt < 1e-9) return pose;
        // rotate so that axis maps exactly onto ±Z: smallest rotation taking the current up vector to world Z
        var up = r.Column(axis) * Math.Sign(comps[axis]);
        var ez = new Vec3(0, 0, 1);
        var cross = up.Cross(ez);
        if (cross.Length < 1e-12) return pose;
        var fix = Mat3.AxisAngle(cross, Math.Asin(Math.Clamp(cross.Length, 0, 1)));
        return new Pose3d((fix * r).Orthonormalized(), pose.Translation);
    }

    // ------------------------------------------------------------------------------ connected objects

    /// <summary>
    /// <c>BlockWorld::AddConnectedActiveObject(activeID, factoryID, type)</c> 0x0062302C (M11-041), returning the ObjectID or -1.
    /// <list type="number">
    /// <item>A signed <c>activeID &gt;= 5</c> warns InvalidActiveID and returns -1 (0x00623040, 0x0062305A..0x0062309E); a negative one passes.</item>
    /// <item>The slot occupied: the same factoryID and type returns that object's ID; otherwise a ConflictingActiveID error and
    ///   <c>RemoveConnectedActiveObject(activeID)</c> (0x006230A6..0x0062319E).</item>
    /// <item>A connected object with the same factoryID only logs FactoryIDAlreadyUsed (0x006231A2..0x0062328C).</item>
    /// <item><c>CreateActiveObjectByType</c>; null returns -1 (0x00623296..0x00623348).</item>
    /// <item>The located objects of the type in EVERY origin with this activeID: the first, r6: the same factoryID gives the new object r6's ID;
    ///   a factoryID of 0 in r6 adopts (a non-zero given factoryID and the activeID are written to every such object) and takes r6's ID; any other
    ///   factoryID is the MismatchedFactoryID error, <c>DeleteLocatedObjects</c> of r6's ID in the current origin and a fresh <c>SetID</c>
    ///   (0x0062339A..0x00623474, 0x006237E4..0x006238E6, 0x00623B76..0x00623E04).</item>
    /// <item>None with that activeID: every located object of the type in every origin: none gives a fresh <c>SetID</c>; otherwise each is marked Dirty
    ///   (<c>MarkObjectDirty(o, false)</c>, 0x00623550) and updated (activeID -1: both set; the same factoryID: activeID set; otherwise both set when
    ///   the given factoryID is not 0) and the new object takes the first one's ID (0x00623476..0x00623956).</item>
    /// <item>The new object is registered in the connected map with no pose and no PoseState (0x006239D0..0x00623A8C).</item>
    /// </list>
    /// </summary>
    // fidelity: M11-041
    public int AddConnectedActiveObject(int activeId, uint factoryId, ObjectType type)
    {
        if (activeId >= 5)
        {
            Log?.Invoke($"BlockWorld.AddConnectedActiveObject.InvalidActiveID: activeID {activeId}");
            return -1;
        }
        var occupant = GetConnectedActiveObjectByActiveIdHelper(activeId);
        if (occupant is not null)
        {
            if (occupant.FactoryId == factoryId && occupant.Type == type)
            {
                Log?.Invoke($"BlockWorld.AddConnectedActiveObject.FoundMatchingObjectAtSameSlot: {occupant.Type} {occupant.ObjectId}");
                return (int)occupant.ObjectId;
            }
            Log?.Invoke($"BlockWorld.AddConnectedActiveObject.ConflictingActiveID: ActiveID:{activeId} found with factory 0x{occupant.FactoryId:x} {occupant.Type}. Disconnecting previous.");
            RemoveConnectedActiveObject(activeId);
        }
        var sameFactory = new BlockWorldFilter();
        sameFactory.Predicates.Add(o => o.FactoryId == factoryId);
        if (FindConnectedObjectHelper(sameFactory, null, true) is not null)
            Log?.Invoke($"BlockWorld.AddConnectedActiveObject.FactoryIDAlreadyUsed: 0x{factoryId:x}");
        var created = CreateActiveObjectByType(type, activeId, factoryId);
        if (created is null) return -1;

        var withActiveId = new BlockWorldFilter { OriginMode = OriginMode.InAnyFrame };
        withActiveId.AllowedTypes.Add(type);
        withActiveId.Predicates.Add(o => o.ActiveId == activeId);
        var v1 = FindLocatedMatchingObjects(withActiveId);
        if (v1.Count > 0)
        {
            Log?.Invoke($"BlockWorld.AddConnectedActiveObject.ConflictingActiveID: Objects with ActiveID:{activeId} were found");
            var first = v1[0];
            if (first.FactoryId == factoryId)
            {
                Log?.Invoke($"BlockWorld.AddConnectedActiveObject.FoundMatchingActiveObject: {first.Type} {first.ObjectId}");
                created.ObjectId = first.ObjectId;
            }
            else if (first.FactoryId == 0)
            {
                Log?.Invoke($"BlockWorld.AddConnectedActiveObject.FoundMatchingActiveObjectThatWasNeverConnected: {first.Type} {first.ObjectId}");
                if (factoryId != 0)
                {
                    foreach (var o in v1)
                    {
                        if (o.IsActive) { o.FactoryId = factoryId; o.ActiveId = activeId; }
                        else Log?.Invoke("ObservableObject.SetFactoryID.NotActive: the object is not active");
                    }
                    Log?.Invoke($"BlockWorld.AddConnectedActiveObject.UpdateExistingObjectsFactoryID: 0x{factoryId:x}");
                }
                created.ObjectId = first.ObjectId;
            }
            else
            {
                Log?.Invoke($"BlockWorld.AddConnectedActiveObject.MismatchedFactoryID: object {first.ObjectId} has factory 0x{first.FactoryId:x}, connecting 0x{factoryId:x}");
                var del = new BlockWorldFilter();
                del.AllowedIds.Add(first.ObjectId);
                DeleteLocatedObjects(del);
                created.SetID();
            }
        }
        else
        {
            var ofType = new BlockWorldFilter { OriginMode = OriginMode.InAnyFrame };
            ofType.AllowedTypes.Add(type);
            var v2 = FindLocatedMatchingObjects(ofType);
            if (v2.Count == 0) created.SetID();
            else
            {
                foreach (var o in v2)
                {
                    MarkObjectDirty(o, false);
                    if (o.ActiveId == -1)
                    {
                        if (o.IsActive) { o.ActiveId = activeId; o.FactoryId = factoryId; }
                        else Log?.Invoke("ObservableObject.SetActiveID.NotActive: the object is not active");
                        Log?.Invoke($"BlockWorld.AddConnectedActiveObject.FoundMatchingObjectWithNoActiveID: {o.Type} {o.ObjectId}");
                    }
                    else if (o.FactoryId == factoryId)
                    {
                        Log?.Invoke($"BlockWorld.AddConnectedActiveObject.FoundIdenticalObjectOnDifferentSlot: {o.Type} {o.ObjectId}");
                        o.ActiveId = activeId;
                    }
                    else
                    {
                        Log?.Invoke($"BlockWorld.AddConnectedActiveObject.FoundOtherActiveObjectOfSameType: {o.Type} {o.ObjectId}");
                        if (factoryId != 0) { o.ActiveId = activeId; o.FactoryId = factoryId; }
                    }
                }
                created.ObjectId = v2[0].ObjectId;
            }
        }
        lock (_gate) _connectedObjects[(((int)created.Family), (int)created.Type, (int)created.ObjectId)] = created;
        return (int)created.ObjectId;
    }

    /// <summary>
    /// <c>CreateActiveObjectByType(type, activeID, factoryID)</c> (called at 0x00623296). Its body is unread (M11-042): a light cube is an
    /// <c>ActiveCube(type)</c> with its ActiveID and FactoryID set; any other type throws <see cref="NotSupportedException"/> rather than guessing
    /// whether the engine returns null for it.
    /// </summary>
    // fidelity: M11-042
    private ObservableObject? CreateActiveObjectByType(ObjectType type, int activeId, uint factoryId)
    {
        if (!CubeGeometry.IsCube(type))
            throw new NotSupportedException($"M11-042: CreateActiveObjectByType (0x00623296) is unread for {type}");
        return new ObservableObject(type, CubeGeometry.MarkersFor(type)) { ActiveId = activeId, FactoryId = factoryId, OriginId = CurrentOriginId };
    }

    /// <summary>
    /// <c>BlockWorld::RemoveConnectedActiveObject(activeID)</c> 0x006243A0: erases the connected object of that slot from the connected map
    /// (0x00624432; H1.6). The rest of its body is unread (M11-042).
    /// </summary>
    // fidelity: M11-041, M11-042
    public void RemoveConnectedActiveObject(int activeId)
    {
        lock (_gate)
        {
            var key = _connectedObjects.Where(kv => kv.Value.ActiveId == activeId).Select(kv => kv.Key).Cast<(int, int, int)?>().FirstOrDefault();
            if (key is { } k) _connectedObjects.Remove(k);
        }
    }

    /// <summary>
    /// The connection half of <c>RobotToEngineImplMessaging::HandleActiveObjectConnectionState</c> 0x00533B3C (M11-041): the range test is
    /// unsigned (<c>cmp r7,#4; bhi</c> 0x00533B58), so an activeID above 4 (and a negative one, which the message cannot carry) never reaches
    /// <see cref="AddConnectedActiveObject"/>; a connection calls it and, for an ID other than -1, logs Connected (0x00533BF4) and calls
    /// <c>Robot::HandleConnectedToObject(activeID, factoryID, type)</c> (0x00533C34); a disconnection calls <see cref="RemoveConnectedActiveObject"/>
    /// (0x00533CA0). UNREAD (M11-042): the error branches at 0x00533B58 and 0x00533CB8 (logged here, nothing more), and
    /// <c>HandleConnectedToObject</c>, which stays this stack's cube connection code (<c>CozmoCubes.Handle</c>, M4) and is not repeated here.
    /// Returns the ObjectID, or -1.
    /// </summary>
    // fidelity: M11-041, M11-042
    public int HandleActiveObjectConnectionState(uint activeId, uint factoryId, ObjectType type, bool connected)
    {
        if (activeId > 4)
        {
            Log?.Invoke($"Robot.HandleActiveObjectConnectionState: activeID {activeId} is above 4; the error branch at 0x00533B58 is unread");
            return -1;
        }
        if (!connected)
        {
            RemoveConnectedActiveObject((int)activeId);
            return -1;
        }
        int id = AddConnectedActiveObject((int)activeId, factoryId, type);
        if (id == -1)
        {
            Log?.Invoke($"Robot.HandleActiveObjectConnectionState: AddConnectedActiveObject returned -1 for activeID {activeId}; the branch at 0x00533CB8 is unread");
            return -1;
        }
        Log?.Invoke($"Robot.HandleActiveObjectConnectionState.Connected: activeID {activeId} factory 0x{factoryId:x} {type} ObjectID {id}");
        return id;
    }

    // ------------------------------------------------------------------------------------ origins

    /// <summary>
    /// <c>BlockWorld::UpdateObjectOrigins(oldOrigin, newOrigin)</c> 0x00620534 (M11-043), which <c>Robot::LocalizeToObject</c> 0x005154B0 calls at
    /// 0x00515992 after <c>Rejigger</c>. It clears the confirmer (0x00620594), runs the first modify function over every object of the old origin
    /// (0x00628E18: an object's pose is its pose with respect to the new origin, unchanged when it is carried; the counterpart in the new origin
    /// is the located object with the same family, type and ID for a unique type; a found counterpart takes the moved object's ID, re-keyed when
    /// it differs, and <c>CopyWithNewPose</c> copies the pose and the SOURCE's PoseState; with none the object is cloned, the clone takes the ID
    /// and the pose and <c>AddLocatedObject</c> adds it), erases the old origin's entries only when the LAST object's result was 0 (0x00620642..
    /// 0x0062064C), runs the second modify function over the new origin's objects (<c>AddInExistingPose</c>, 0x00506F48: an entry with zero count
    /// and the current pose), then broadcasts the located states and sets the BlockConfigurationManager's byte [+0xC] (0x006206E6, 0x006206EA).
    ///
    /// <paramref name="oldOriginWrtNew"/> is the old origin's pose in the new origin's frame, which <c>GetWithRespectTo</c> would derive from the
    /// pose tree this stack does not have. NOT BUILT here: the caller <c>LocalizeToObject</c> and its <c>Rejigger</c> (M11-044 / M12 boundary), the
    /// <c>UnknownOriginID</c> guard (its value is unread), the broadcast (no message type) and <c>SetObservationTimes</c> (unread), and the
    /// non-unique counterpart search (<c>FindLocatedObjectClosestToHelper</c>, throws).
    /// </summary>
    // fidelity: M11-043
    public uint UpdateObjectOrigins(uint oldOriginId, uint newOriginId, Pose3d oldOriginWrtNew)
    {
        ClearConfirmer();                                                                                              // 0x00620594
        uint flag = 0;
        var first = new BlockWorldFilter { OriginMode = OriginMode.Custom };
        first.AllowedOrigins.Add(oldOriginId);
        FindLocatedObjectHelper(first, o => flag = MoveObjectToNewOrigin(o, newOriginId, oldOriginWrtNew), false);     // 0x0062060A..0x0062063E
        if (flag == 0)
            lock (_gate)                                                                                               // 0x00620642..0x0062064C
                foreach (var k in _located.Keys.Where(k => k.Origin == oldOriginId).ToList()) _located.Remove(k);
        FindLocatedObjectHelper(new BlockWorldFilter(), AddInExistingPose, false);                                     // 0x006206BE..0x006206E0
        UnbuiltBroadcastLocatedObjectStatesCalls++;                                                                    // 0x006206E6 (M11-038: no message type)
        if (BlockConfigurationManagerForceUpdate is { } force) force(); else UnwiredForceUpdateCalls++;                // 0x006206EA
        return 0;
    }

    private uint MoveObjectToNewOrigin(ObservableObject o, uint newOriginId, Pose3d oldOriginWrtNew)
    {
        var pose = Carrying(o.ObjectId) ? o.Pose : oldOriginWrtNew.Compose(o.Pose);                                    // 0x00628E2E..0x00628FB0
        if (!o.IsUnique)
            throw new NotSupportedException("M11-043: FindLocatedObjectClosestToHelper (the non-unique counterpart search, 0x0062902E..0x0062904E) is not built");
        var f = new BlockWorldFilter { OriginMode = OriginMode.Custom };
        f.AllowedOrigins.Add(newOriginId);
        f.AllowedFamilies.Add(o.Family);
        f.AllowedTypes.Add(o.Type);
        f.AllowedIds.Add(o.ObjectId);
        f.Predicates.Add(x => x.IsUnique);
        var counterpart = FindLocatedObjectHelper(f, null, true);
        if (counterpart is not null)
        {
            Log?.Invoke($"BlockWorld.UpdateObjectOrigins.ObjectOriginChanged: object {o.ObjectId} {o.Type}");
            if (counterpart.ObjectId != o.ObjectId)
            {
                lock (_gate)
                {
                    _located.Remove(KeyOf(counterpart));
                    counterpart.ObjectId = o.ObjectId;
                    _located[KeyOf(counterpart)] = counterpart;
                }
                Log?.Invoke("BlockWorld.UpdateObjectOrigins.MovedSharedPointerDueToIDChange");
            }
            // SetObservationTimes(o) 0x006292DC: unread, not built
            CopyWithNewPose(counterpart, pose, o);
        }
        else
        {
            Log?.Invoke($"BlockWorld.UpdateObjectOrigins.NoMatchFound: object {o.ObjectId} {o.Type}");
            var clone = o.Clone();
            clone.ObjectId = o.ObjectId;
            clone.OriginId = newOriginId;
            CopyWithNewPose(clone, pose, o);
            AddLocatedObject(clone);                                                                                   // "NoMatchingObjectInNewFrame"
        }
        return 0;                                                                                                      // 0x006292F6
    }

    /// <summary>
    /// <c>ObjectPoseConfirmer::CopyWithNewPose(obj, newPose, src)</c> 0x00506EF8: <c>obj->SetPose(newPose, src.fromDistance, src.PoseState)</c>
    /// (vtable +0x28, 0x00506F02..0x00506F16), so the moved or cloned object takes the SOURCE's state and nothing is set Dirty; then the entry for the
    /// object's ID is created when absent and its pose set. Returns 0.
    /// </summary>
    // fidelity: M11-043
    private uint CopyWithNewPose(ObservableObject obj, Pose3d newPose, ObservableObject src)
    {
        obj.SetPose(newPose, src.FromDistance, src.PoseState);
        var entry = EntryForOrCreate(obj.ObjectId);
        obj.Confirmation = entry;
        entry.Pose = newPose;
        return 0;
    }

    /// <summary><c>ObjectPoseConfirmer::AddInExistingPose(obj)</c> 0x00506F48: an entry keyed by the object's ID with count, misses, times and pointer all zero and the object's current pose; an entry already there is kept (emplace).</summary>
    // fidelity: M11-043
    private void AddInExistingPose(ObservableObject obj)
    {
        lock (_gate)
        {
            if (!_confirmations.TryGetValue((int)obj.ObjectId, out var entry))
                _confirmations[(int)obj.ObjectId] = entry = new PoseConfirmation { Pose = obj.Pose };
            obj.Confirmation = entry;
        }
    }

    /// <summary><c>ObjectPoseConfirmer::Clear()</c> 0x005077EC: every entry, with all its counts and misses, is dropped.</summary>
    // fidelity: M11-043
    internal void ClearConfirmer()
    {
        lock (_gate)
        {
            _confirmations.Clear();
            foreach (var o in _located.Values) o.Confirmation = null;
            foreach (var o in _forgotten.Values) o.Confirmation = null;
        }
    }

    // ----------------------------------------------------------------------- what the rest of the stack calls

    /// <summary>
    /// A cube that reports movement over the radio has a Dirty pose until it is seen again.
    ///
    /// <c>RobotToEngineImplMessaging::HandleActiveObjectMoved</c> 0x00533E30 calls
    /// <c>ObjectPoseConfirmer::MarkObjectDirty(object, false)</c> at 0x0053413C behind one guard, at
    /// 0x00534116: the robot must not be carrying the object <em>and</em> its pose state must be exactly
    /// Known. Either condition failing skips the call, which is why a cube on the lift reporting its own
    /// motion does not dirty the pose the lift is holding it at.
    /// </summary>
    // fidelity: M11-009
    public void MarkDirty(uint objectId)
    {
        var o = GetLocatedObjectByIdHelper(objectId);
        if (o is null || o.PoseState != PoseState.Known) return;
        MarkObjectDirty(o, false);
    }

    /// <summary>
    /// Places an object the robot is holding. The engine does not do this by assignment - the object is
    /// parented to the lift and follows it - but the effect on the world model is the same, and this is
    /// the only way anything moves an object here without having seen it.
    /// </summary>
    public void SetCarriedPose(uint objectId, Pose3d pose)
    {
        if (GetLocatedObjectByIdHelper(objectId) is { } o) lock (_gate) o.Pose = pose;
    }

    /// <summary>
    /// An object the robot has just let go of: still located, at the pose it was released at, but Dirty.
    /// <c>SetCarriedObjectAsUnattached</c> 0x006333D4 re-registers it through
    /// <c>AddRobotRelativeObservation(object, poseWrtRobot, PoseState 2)</c>, and 2 is Dirty.
    /// </summary>
    public void MarkReleased(uint objectId)
    {
        var o = GetObjectById(objectId);
        if (o is null) return;
        PoseState prev;
        lock (_gate) { prev = o.PoseState; o.PoseState = PoseState.Dirty; }
        if (prev == PoseState.Dirty) return;
        PoseStateChanged?.Invoke(o, prev, PoseState.Dirty);
    }

    /// <summary>
    /// The pose write of the two <c>ObjectPoseConfirmer</c> relative-observation entry points <c>SetObjectAsAttachedToLift</c> calls
    /// (<c>AddObjectRelativeObservation</c> 0x00506CD8, <c>AddLiftRelativeObservation</c> 0x00506E5C; M12-008): the pose, the pose state, the confirmer's
    /// reference pose and (when given) the sighting count are set under the world's lock, and the pose-changed broadcast follows (0x00506D48, 0x00506E9E;
    /// <see cref="BroadcastObjectPoseChanged"/>, which raises <see cref="PoseStateChanged"/> when the state changed, as <see cref="MarkReleased"/> and
    /// <see cref="MarkDirty"/> do).
    /// </summary>
    // fidelity: M12-008
    internal void ApplyRelativeObservation(ObservableObject obj, Pose3d pose, PoseState state, int? confirmationCount)
    {
        PoseState prev; Pose3d oldPose;
        lock (_gate)
        {
            prev = obj.PoseState; oldPose = obj.Pose;
            obj.Pose = pose; obj.PoseState = state;
            var entry = EntryForOrCreate(obj.ObjectId);
            obj.Confirmation = entry;
            entry.Pose = pose;
            if (confirmationCount is { } count) entry.Count = count;
        }
        BroadcastObjectPoseChanged(obj, oldPose, prev);
    }

    /// <summary>
    /// Records what the cube's radio says about its own motion; see <see cref="ObservableObject.IsMoving"/>. It is set on the connected object (the one
    /// <c>HandleActiveObjectMoved</c> finds, whose <c>IsMoving</c> <c>UpdatePoseInInstance</c> reads) and on the located objects with the ID.
    /// </summary>
    public void SetMoving(uint objectId, bool moving)
    {
        lock (_gate)
        {
            foreach (var o in _located.Values) if (o.ObjectId == objectId) o.IsMoving = moving;
            foreach (var o in _connectedObjects.Values) if (o.ObjectId == objectId) o.IsMoving = moving;
        }
    }

    /// <summary>
    /// The robot has been delocalized: it is in a new origin, and everything that was located in the old
    /// one is in a coordinate frame that no longer exists.
    ///
    /// <c>Robot::Delocalize</c> 0x00510A24 allocates a new origin, puts the robot at it, clears the pose
    /// confirmer, and moves only what the robot is carrying into the new origin
    /// (<c>BlockWorld::UpdateObjectOrigin</c> for each carried object, 0x00510CF0);
    /// <c>BlockWorld::OnRobotDelocalized</c> 0x006249C4 then deletes what is left in origins nobody
    /// references and asks the map component for a fresh map for the new origin
    /// (<c>CreateLocalizedMemoryMap</c>). So an object that is not being carried stops being located - its
    /// pose is not stale by a little, it is expressed in a frame that has gone.
    ///
    /// Returns the objects that stopped being located.
    /// </summary>
    // fidelity: M11-019
    public IReadOnlyList<ObservableObject> OnRobotDelocalized(IReadOnlySet<uint>? carriedObjectIds = null)
    {
        var forgotten = new List<ObservableObject>();
        List<ObservableObject> located;
        DelocalizeRobotFields();                                   // Robot::Delocalize's own writes (0x00510A46..)
        lock (_gate) located = _located.Values.Where(o => o.IsLocated).ToList();
        ClearConfirmer();
        foreach (var o in located)
        {
            if (carriedObjectIds is not null && carriedObjectIds.Contains(o.ObjectId)) continue;
            PoseState prev;
            lock (_gate)
            {
                prev = o.PoseState; o.PoseState = PoseState.Unknown;
                _located.Remove(KeyOf(o));
                _forgotten[o.ObjectId] = o;
            }
            PoseStateChanged?.Invoke(o, prev, PoseState.Unknown);
            forgotten.Add(o);
        }
        return forgotten;
    }

    /// <summary>
    /// <c>MarkObjectUnknown</c> on request (LOCAL: used when a cube disconnects, and by the manipulation code): the object leaves the located
    /// map with its state Unknown and stays readable by ID. NOT the engine's <c>MarkObjectUnknown</c> (see <see cref="MarkObjectUnknown"/>),
    /// which deletes the object and its stack.
    /// </summary>
    public void MarkUnknown(uint objectId)
    {
        ObservableObject? o; PoseState prev;
        lock (_gate)
        {
            o = _located.Values.FirstOrDefault(x => x.ObjectId == objectId && x.OriginId == CurrentOriginId);
            if (o is null || o.PoseState == PoseState.Unknown) return;
            prev = o.PoseState; o.PoseState = PoseState.Unknown;
            _located.Remove(KeyOf(o));
            _forgotten[o.ObjectId] = o;
        }
        PoseStateChanged?.Invoke(o, prev, PoseState.Unknown);
    }

    /// <summary>Distance between a located object and a robot pose, millimetres, or null.</summary>
    public double? DistanceFromRobotMm(uint objectId, Pose3d robotPose)
    {
        var o = GetLocatedObjectById(objectId);
        return o is null ? null : (o.Pose.Translation - robotPose.Translation).Length;
    }
}

/// <summary>
/// <c>PotentialObjectsForLocalizingTo</c> (M11-044): a stack local of <c>AddAndUpdateObjects</c> (constructed at 0x00620B0C, destroyed at 0x0062130E) holding a
/// <c>map&lt;rootOriginID, ObservedAndMatchedPair&gt;</c>. <see cref="Gate"/> (this+0x10) is <c>([Robot+0x2B8] != -1) ? byte [Robot+0x2BC] : 1</c>, computed once
/// (0x00512B56..0x00512B8E). <c>Insert</c> is 0x0050CE00..0x0050D120 and <c>UseDiscardedObservation</c> 0x0050CD90..0x0050CDFC; the rules are those of
/// 20260929-R-VIS-M11-gap3-insert.md P0..P8. This is the path that refreshes an already-confirmed object's pose (Case X1..X4 there): a sighting inside the match tolerance
/// skips <c>AddVisualObservation</c> in <c>AddAndUpdateObjects</c>, and <c>Insert</c> reaches it through <c>UseDiscardedObservation</c>.
/// </summary>
// fidelity: M11-044
internal sealed class PotentialObjectsForLocalizingTo
{
    /// <summary><c>ObservedAndMatchedPair</c>: the observed instance, the matched object, the distance (+0x14) and the flag byte (+0x18) that says <c>AddVisualObservation</c> was already called.</summary>
    internal sealed class Pair
    {
        public ObservableObject Observed = null!;
        public ObservableObject Matched = null!;
        public float Dist;
        public bool Flag;
    }

    private readonly BlockWorld _w;
    private readonly SortedDictionary<uint, Pair> _map = new();

    public PotentialObjectsForLocalizingTo(BlockWorld world)
    {
        _w = world;
        bool localized = _w.LocalizedToObjectId is not null;
        Gate = localized ? _w.RobotMovedSinceLocalized : true;
    }

    /// <summary>this+0x10: "the robot is not localized, or has moved since it last localized".</summary>
    public bool Gate { get; }

    public IReadOnlyDictionary<uint, Pair> Pairs => _map;

    private static readonly Vec3 SameRobotTolerance = new(1, 1, 1);
    private static readonly double SameRobotAngle = BitConverter.Int32BitsToSingle(0x3C8EFA35);         // the engine's float literal, widened

    /// <summary>
    /// <c>Insert(observed, matched, dist, flag)</c>; returns 1 when the pair was stored, kept against or replaced another, and 0 otherwise.
    /// <list type="number">
    /// <item>P2 discard: <c>(250.0f + 1e-5f) &lt; dist</c>, or <see cref="Gate"/> false, or <c>CouldUseObjectForLocalization(matched) == 0</c> (0x0050CE64..0x0050CE8E; a NaN
    ///   distance does not discard). P3: a discarded pair whose matched object is in the world origin goes to <c>UseDiscardedObservation(pair, wasCameraMoving)</c>; return 0.</item>
    /// <item>P4: return 0 when <c>MovementComponent+0xA</c> (head not in position) or +0xC (wheels moving) is set or the camera was moving (0x0050CE92..0x0050CEB0).</item>
    /// <item>P5, only for a matched object in the world origin: the robot poses at the matched object's last pose update and at ts (both <c>GetComputedStateAt</c> calls must succeed) the
    ///   same within (1,1,1) mm and 1 degree while the observed and matched poses are NOT: <c>UseDiscardedObservation(pair, false)</c>; return 0 (0x0050CEB4..0x0050CF86, 0x0050D122).</item>
    /// <item>P6: one pair per matched pose root: none: store, return 1; a stored pair at least as near as the new one (a NaN compares here): keep it, and a new pair in the world origin goes to
    ///   <c>UseDiscardedObservation(new, false)</c>; a nearer new pair replaces it and, when the root is the current origin, the old one goes to <c>UseDiscardedObservation(old, false)</c>.</item>
    /// </list>
    /// </summary>
    public int Insert(ObservableObject observed, ObservableObject matched, float dist, bool flag, bool wasCameraMoving)
    {
        var pair = new Pair { Observed = observed, Matched = matched, Dist = dist, Flag = flag };
        uint ts = observed.LastObservedTimestamp;
        bool inWorldOrigin = matched.OriginId == _w.CurrentOriginId;            // Robot::IsPoseInWorldOrigin (0x0050CE54)
        bool could = _w.CouldUseObjectForLocalization(matched);                  // 0x0050CE5E
        float threshold = (float)BlockWorld.MaxLocalizationDistanceMm + 1e-5f;
        if (threshold < dist || !Gate || !could)
        {
            if (inWorldOrigin) UseDiscardedObservation(pair, wasCameraMoving);   // 0x0050CFE2
            return 0;
        }
        var mc = _w.MovementBytes?.Invoke() ?? (false, false);
        if (mc.HeadNotInPosition || mc.WheelsMoving || wasCameraMoving) return 0;
        if (inWorldOrigin)
        {
            uint lastUpdated = _w.EntryFor(matched.ObjectId)?.LastPoseUpdatedTime ?? 0;   // GetLastPoseUpdatedTime 0x0050780C
            var a = _w.ComputedRobotPoseAt?.Invoke(lastUpdated);
            var b = a is null ? null : _w.ComputedRobotPoseAt?.Invoke(ts);
            if (a is { } pa && b is { } pb && pb.IsSameAs(pa, SameRobotTolerance, SameRobotAngle, out _)
                && !observed.Pose.IsSameAs(matched.Pose, SameRobotTolerance, SameRobotAngle, out _))
            {
                UseDiscardedObservation(pair, false);                            // 0x0050D122
                return 0;
            }
        }
        uint key = matched.OriginId;                                             // matched->GetPose().GetRootID()
        if (!_map.TryGetValue(key, out var stored)) { _map[key] = pair; return 1; }
        if (!(dist < stored.Dist))
        {
            if (inWorldOrigin) UseDiscardedObservation(pair, false);             // 0x0050D108..0x0050D120
            return 1;
        }
        if (key == _w.CurrentOriginId) UseDiscardedObservation(stored, false);   // 0x0050D030..0x0050D046
        _map[key] = pair;
        return 1;
    }

    /// <summary>
    /// <c>UseDiscardedObservation(pair, b)</c> 0x0050CD90: nothing when the pair's flag says <c>AddVisualObservation</c> already ran (0x0050CDE0); otherwise
    /// <c>AddVisualObservation(observed, matched, b, dist)</c> (0x0050CDF6) with the result ignored.
    /// </summary>
    public void UseDiscardedObservation(Pair pair, bool b)
    {
        if (pair.Flag) return;
        _w.AddVisualObservation(pair.Observed, pair.Matched, b, pair.Dist);
    }

    /// <summary>
    /// <c>LocalizeRobot()</c> 0x0050D1CC..0x0050D4DA. Empty map: 0. One entry whose key is the current origin: <c>LocalizeToObject(observed, matched)</c>; a non-zero result is the
    /// LocalizeFailure error and is returned (0x0050D2D4..0x0050D34E). Otherwise (more than one entry, or one in another origin): every pair is copied into a multimap ordered by distance,
    /// farthest first; when the first one's matched object is not in the current origin, the first whose matched object is has its ID value set to the matched object's and its matched
    /// pointer cleared (StoringMatchedObjectID, 0x0050D354..0x0050D3D6); each is then localized to in order, a cleared one through <c>GetLocatedObjectByIdHelper(id)</c> (a null one warns
    /// MissingMatchedObjectInCurrentFrame and is skipped), a failure warning CrossFrameLocalizeFailure; the result is 1 if any failed (0x0050D3DA..0x0050D4D2). It writes nothing but what
    /// <c>LocalizeToObject</c> writes. In this stack <c>LocalizeToObject</c> performs only the <c>SetLocalizedTo</c> field writes (<see cref="BlockWorld.LocalizeToObject"/>).
    /// </summary>
    public uint LocalizeRobot()
    {
        _w.UnbuiltLocalizeRobotCalls++;
        if (_map.Count == 0) return 0;
        uint cur = _w.CurrentOriginId;
        if (_map.Count == 1 && _map.Keys.First() == cur)
        {
            var only = _map.Values.First();
            uint r = _w.LocalizeToObject(only.Observed, only.Matched);
            if (r != 0) _w.LogLine("PotentialObjectsForLocalizingTo.LocalizeRobot.LocalizeFailure: Failed to localize to object " + only.Matched.ObjectId + ".");
            return r;
        }
        var list = _map.Values.OrderByDescending(p => p.Dist).Select(p => (Pair: p, Matched: (ObservableObject?)p.Matched, Id: p.Matched.ObjectId)).ToList();
        if (list[0].Pair.Matched.OriginId != cur)
        {
            int i = list.FindIndex(e => e.Pair.Matched.OriginId == cur);
            if (i >= 0)
            {
                _w.LogLine($"PotentialObjectsForLocalizingTo.LocalizeRobot.StoringMatchedObjectID: Match in current frame not farthest. Storing ID={list[i].Pair.Matched.ObjectId} to recheck");
                list[i] = (list[i].Pair, null, list[i].Pair.Matched.ObjectId);
            }
        }
        bool failed = false;
        foreach (var e in list)
        {
            var m = e.Matched ?? _w.GetLocatedObjectByIdHelper(e.Id);
            if (m is null) { _w.LogLine($"PotentialObjectsForLocalizingTo.LocalizeRobot.MissingMatchedObjectInCurrentFrame: {e.Id}"); continue; }
            if (_w.LocalizeToObject(e.Pair.Observed, m) != 0)
            {
                _w.LogLine($"PotentialObjectsForLocalizingTo.LocalizeRobot.CrossFrameLocalizeFailure: {m.ObjectId}");
                failed = true;
            }
        }
        return failed ? 1u : 0u;
    }
}
