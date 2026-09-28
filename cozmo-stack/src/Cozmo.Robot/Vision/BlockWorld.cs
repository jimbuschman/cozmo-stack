using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

/// <summary>
/// <c>Anki::PoseState</c>: the engine prints "Known" and "Dirty" (<c>EnumToString(PoseState)</c>); Unknown is the
/// state of an object that has been marked unobserved often enough (<c>BlockWorld::MarkObjectUnknown</c>).
/// An object is <b>located</b> (<c>GetLocatedObjectByIdHelper</c> returns it) while its state is not Unknown.
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
/// The engine's <c>Cozmo::ObservableObject</c> for a light cube: identity, markers, pose and pose state, and the
/// visibility test the behaviours ask.
/// </summary>
public sealed class ObservableObject
{
    public ObservableObject(uint objectId, ObjectType type, IReadOnlyList<KnownMarker> markers)
    {
        ObjectId = objectId; Type = type; Markers = markers;
        Family = CubeGeometry.IsCube(type) ? ObjectFamily.LightCube : type == ObjectType.Charger_Basic ? ObjectFamily.Charger : ObjectFamily.Unknown;
    }

    public uint ObjectId { get; }
    public ObjectType Type { get; }
    public ObjectFamily Family { get; }
    public IReadOnlyList<KnownMarker> Markers { get; }
    /// <summary>Pose in the robot's world origin; meaningful while <see cref="PoseState"/> is not Unknown.</summary>
    public Pose3d Pose { get; internal set; } = Pose3d.Identity;
    public PoseState PoseState { get; internal set; } = PoseState.Unknown;
    public bool IsLocated => PoseState != PoseState.Unknown;
    /// <summary>Robot timestamp of the frame that last observed the object.</summary>
    public uint LastObservedTimestamp { get; internal set; }

    /// <summary>
    /// Whether the cube is reporting itself in motion: true between an <c>ObjectMoved</c> and the
    /// <c>ObjectStoppedMoving</c> that ends it. The engine asks the object this through a virtual on
    /// <c>ObservableObject</c> - <c>PickupObjectAction::Verify</c> 0x00553C8E calls it - so it lives on
    /// the object here too rather than in a tracker beside it.
    /// </summary>
    public bool IsMoving { get; internal set; }
    public int TimesObserved { get; internal set; }
    /// <summary>
    /// M11-007 / C3.4: the <c>ObjectPoseConfirmer</c> sighting count. A new entry starts at 1
    /// (<c>PoseConfirmation</c> ctor 0x5062E0 stores 1 at +0xc); a matching second sighting increments it
    /// (<c>AddVisualObservation</c> 0x506A04..0x506A26); a mismatching sighting resets it to 1
    /// (0x506A46..0x506A4E). <see cref="IsPoseConfirmed"/> is <c>count &gt; 1</c>
    /// (<c>IsReferencePoseConfirmed</c> 0x506340), so the first sighting does not confirm. MISSING: the
    /// inventory does not settle what the caller does with <c>AddVisualObservation</c>'s return, so this
    /// stack still sets the object's <c>PoseState</c> to Known on the first sighting and keeps the
    /// confirmation count separate; whether Known should require the count is not established.
    /// </summary>
    // fidelity: M11-007
    public int PoseConfirmationCount { get; internal set; }
    /// <summary>The pose the confirmation count is measured against (the entry's stored pose).</summary>
    public Pose3d ReferencePose { get; internal set; } = Pose3d.Identity;
    /// <summary><c>IsReferencePoseConfirmed</c> 0x506340: <c>count &gt; 1</c>.</summary>
    public bool IsPoseConfirmed => PoseConfirmationCount > 1;
    /// <summary><c>BlockWorld::MarkObjectUnobserved</c> counter: frames in which the object should have been seen but was not.</summary>
    public int UnobservedCount { get; internal set; }
    /// <summary>The marker codes seen in the last observation.</summary>
    public IReadOnlyList<MarkerType> LastObservedMarkers { get; internal set; } = Array.Empty<MarkerType>();
    /// <summary>Bounding box of the last observation in the image, for <c>RobotObservedObject.img_rect</c>.</summary>
    public (double X, double Y, double Width, double Height)? LastImageRect { get; internal set; }
    public double LastReprojectionRmsPx { get; internal set; }

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

/// <summary>
/// The engine's <c>BlockWorld</c> reduced to what cubes need: located objects, their pose states, and the
/// per-frame update <c>UpdateObservedMarkers</c> (C3.2): <c>ClearOccluders</c>, <c>AddLiftOccluder</c>,
/// <c>CreateObjectsFromMarkers</c>, <c>AddAndUpdateObjects</c>, then - only if that succeeds -
/// <c>CheckForUnobservedObjects</c>, <c>UpdatePoseOfStackedObjects</c>, <c>BlockConfigurationManager::Update</c>
/// and <c>UpdateMarkerlessObjects</c>. The empty-observed-list branch skips the object creation and the
/// stacked-pose update.
///
/// NATIVE rules transcribed: an unconnected observed active object warns and applies a 10 s cooldown and is
/// kept (H1); unobserved checks are skipped while the robot was moving (the <c>IS_MOVING</c> status bit) or
/// rotating faster than 0.174533 rad/s (head <c>rateY</c> / body <c>rateZ</c> from the ImuDataHistory);
/// an object that should be visible (<c>IsVisibleFrom(camera, 0.785398, ...)</c>) and is not gets
/// <c>MarkObjectUnobserved</c>, and after enough misses <c>MarkObjectUnknown</c>. The first sighting leaves
/// the <c>ObjectPoseConfirmer</c> count at 1; the second confirms (C3.4).
/// Pose clustering across an object's markers uses the engine's own tolerances, 5 mm and 5 degrees.
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
    /// <c>ObservableObject::ClampPoseToFlat</c> (<c>0x3EB2B8C2</c> at 0x00505F16), which is the path an
    /// observation of an object already in the world takes. On the creation path
    /// <c>CreateObjectsFromMarkers</c> asks the object itself for the angle in degrees and multiplies by
    /// 0.0174533 (0x00625566); no shipped object overrides it to anything this stack can see.
    /// </summary>
    // fidelity: M11-006
    public const double FlatClampAngleRad = 0.349066;

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
    /// Two misses before an object's pose is forgotten, which is the engine's count traced to its
    /// counter. <c>ObjectPoseConfirmer::MarkObjectUnobserved</c> 0x00506FBC reads the miss count at
    /// <c>PoseConfirmation+0x28</c>, writes back <c>count + 1</c> while zeroing the confirmed count at
    /// +0x24, and takes the forgetting branch only when the value it read was already at least 1
    /// (<c>cmp r3, #1 / blt</c> at 0x00506FDE). So the first miss only counts, and the second acts.
    /// The confirming side at 0x00506A04 is the mirror image of it.
    /// </summary>
    // fidelity: M11-007
    public int UnobservedMissesToUnknown { get; set; } = 2;

    /// <summary>
    /// 40 pixels: what <c>BlockWorld::CheckForUnobservedObjects</c> 0x006220F0 passes as the minimum
    /// projected marker size (0x42200000), alongside a face-normal angle of 0.785398. This had been 10.
    ///
    /// It is not the only value the engine uses for that argument - <c>SearchForBlockHelper</c> and the
    /// ghost-block check both pass 0 - but this is the call that decides whether a located object should
    /// have been seen, which is what this threshold is for here.
    /// </summary>
    // fidelity: M11-008
    public double MinVisibleMarkerSizePx { get; set; } = 40;
    /// <summary>Markers whose pose solve leaves more than this reprojection error are ignored (LOCAL, 3 px).</summary>
    public double MaxReprojectionRmsPx { get; set; } = 3.0;
    /// <summary>
    /// M11-013 (COMPATIBILITY_POLICY, offline tools only, off on the live path): when set, an observed
    /// active object with no connected counterpart is given an object id taken from its type
    /// (LIGHTCUBE1 → 1, ...). The native path does not drop the observation either (M11-004: warn +
    /// 10 s cooldown + continue), but it looks the connected counterpart up by ObjectID, and the
    /// inventory does not settle where an unconnected observation's own ObjectID comes from; this
    /// stack therefore keeps the type-derived id and records the choice as a policy.
    /// </summary>
    public bool AllowUnconnectedObjects { get; set; }

    private readonly Func<IEnumerable<(uint ObjectId, ObjectType Type)>> _connected;
    private readonly Dictionary<uint, ObservableObject> _objects = new();
    private readonly object _gate = new();

    /// <summary>
    /// M11-004: the native <c>unordered_map&lt;int,float&gt;</c> at 0x00620E9E..0x00620EDA that rate-limits
    /// the "not connected" warning. The key is the observed object's id; the value is the time in seconds
    /// until which the warning is suppressed (now + 10.0, the literal at 0x00620B1E and the global
    /// <c>kUnconnectedObservationCooldownDuration_sec</c> at 0x00C781EC).
    /// </summary>
    private readonly Dictionary<uint, double> _unconnectedWarnCooldown = new();
    private const double UnconnectedWarnCooldownSec = 10.0;

    /// <summary><paramref name="connectedActiveObjects"/> answers the engine's "connected active object of this type" question (from the cube radio).</summary>
    public BlockWorld(Func<IEnumerable<(uint ObjectId, ObjectType Type)>> connectedActiveObjects) => _connected = connectedActiveObjects;

    public event Action<ObjectObservation>? ObjectObserved;
    // fidelity: M11-038
    // BroadcastObjectObservation 0x0061FED8 is this observation event; BroadcastLocatedObjectStates
    // 0x0061E6C0, BroadcastConnectedObjects 0x0061E91C and VisionSystem::CheckMailbox 0x006B2AD4 have
    // no stack entry point yet (M11-038's unresolved: their bodies/layouts belong to M2/M10).
    public event Action<ObservableObject, PoseState, PoseState>? PoseStateChanged;
    /// <summary>Lines the world model would log, for the conformance tool.</summary>
    public event Action<string>? Log;

    public IReadOnlyList<ObservableObject> Objects { get { lock (_gate) return _objects.Values.ToList(); } }

    /// <summary>
    /// <c>BlockWorld::AddCollisionObstacle</c> 0x00624A16 is one call:
    /// <c>AddMarkerlessObject(pose, ObjectType::CollisionObstacle)</c>. That routine 0x00622380 makes a
    /// <c>MarkerlessObject</c> of the type, builds a local pose of no rotation about Z and translation
    /// (0, 0, size.z / 2) - the <c>vmov.f32 s0, #0.5</c> and the multiply at 0x006223CC, which stands the
    /// box on the ground - and multiplies the pose given by it before adding it to the world.
    ///
    /// The object id is this stack's (LOCAL): markerless objects are counted from
    /// <see cref="FirstMarkerlessObjectId"/> upwards, since they have no marker and no cube radio to take
    /// an id from.
    /// </summary>
    public ObservableObject AddMarkerlessObject(Pose3d pose, ObjectType type)
    {
        var size = MarkerlessObject.SizeByType(type) ?? throw new ArgumentException($"{type} is not a markerless object type", nameof(type));
        var standing = pose.Compose(new Pose3d(Mat3.Identity, new Vec3(0, 0, size.Z * 0.5)));
        lock (_gate)
        {
            uint id = _nextMarkerlessId++;
            var obj = new ObservableObject(id, type, Array.Empty<KnownMarker>()) { Pose = standing, PoseState = PoseState.Known };
            _objects[id] = obj;
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
    /// Back to the state right after construction, for a removed robot (CB33, CC26, CC27): no objects, markerless ids
    /// from <see cref="FirstMarkerlessObjectId"/> again. No event is raised. The tuning properties and subscribers are kept.
    /// </summary>
    internal void ResetToConstructed()
    {
        lock (_gate)
        {
            _objects.Clear();
            _nextMarkerlessId = FirstMarkerlessObjectId;
        }
    }
    public IReadOnlyList<ObservableObject> LocatedObjects { get { lock (_gate) return _objects.Values.Where(o => o.IsLocated).ToList(); } }

    /// <summary><c>BlockWorld::GetLocatedObjectByIdHelper</c>: the object when it has a located pose, else null.</summary>
    public ObservableObject? GetLocatedObjectById(uint objectId)
    {
        lock (_gate) return _objects.TryGetValue(objectId, out var o) && o.IsLocated ? o : null;
    }

    public ObservableObject? GetObjectById(uint objectId) { lock (_gate) return _objects.GetValueOrDefault(objectId); }

    /// <summary>
    /// <c>BlockWorld::UpdateObservedMarkers</c> 0x00624EE8 (C3.2): the per-frame sequence. <paramref name="camera"/>
    /// is the camera at the frame's timestamp and <paramref name="pd"/> the robot state paired with it.
    /// </summary>
    // fidelity: M11-036, M11-037, M11-006, M11-010
    public BlockWorldFrameResult UpdateObservedMarkers(IReadOnlyList<ObservedMarker> markers, CameraModel camera, uint timestamp, VisionPoseData pd)
    {
        // 0x624F98: Camera::ClearOccluders
        camera.Occluders.Clear();
        // 0x624FA4: VisionComponent::AddLiftOccluder
        AddLiftOccluder(camera, pd);
        var observations = new List<ObjectObservation>();
        var forgotten = new List<ObservableObject>();
        if (markers.Count == 0)
        {
            // 0x625020 empty-observed-list branch: no object creation; CheckForUnobservedObjects only,
            // then skip UpdatePoseOfStackedObjects (0x625052 -> 0x6250D4).
            forgotten.AddRange(CheckForUnobservedObjects(camera, timestamp, new HashSet<uint>(), pd.Moving, pd.RotatingTooFast));
        }
        else
        {
            // 0x624FC4 CreateObjectsFromMarkers -> 0x62505A AddAndUpdateObjects. Its return is a status
            // code: 0 is success, and only then does the normal path continue (0x625062 -> 0x62523A).
            observations.AddRange(CreateAndAddObjects(markers, camera, timestamp));
            forgotten.AddRange(CheckForUnobservedObjects(camera, timestamp,
                observations.Select(o => o.Object.ObjectId).ToHashSet(), pd.Moving, pd.RotatingTooFast));
            UpdatePoseOfStackedObjects();
        }
        // 0x6250D4: both paths reach here.
        BlockConfigurationManagerUpdate?.Invoke();
        UpdateMarkerlessObjects(timestamp);
        return new BlockWorldFrameResult(observations, forgotten);
    }

    /// <summary>
    /// <c>CreateObjectsFromMarkers</c> + <c>AddAndUpdateObjects</c>: one candidate pose per marker, grouped
    /// and clustered by object type; each cluster is added or updated and one occluder per observed marker is
    /// filed.
    /// </summary>
    // fidelity: M11-006, M11-010
    private IReadOnlyList<ObjectObservation> CreateAndAddObjects(IReadOnlyList<ObservedMarker> markers, CameraModel camera, uint timestamp)
    {
        var observations = new List<ObjectObservation>();
        // CreateObjectsFromMarkers: one candidate pose per marker, grouped by object type
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
                var obs = AddAndUpdateObject(group.Key, cluster.Select(c => c.Seen).ToList(), pose, rms, timestamp);
                if (obs is not null) observations.Add(obs);
            }
        }
        // AddAndUpdateObjects 0x006211CC: one occluder per observed marker, its projected quad at its
        // depth, so the next object's visibility test knows what the camera could actually see through.
        foreach (var o in observations)
            foreach (var m in o.Object.Markers)
            {
                if (!o.Markers.Contains(m.Code)) continue;
                var world = m.CornersInWorld(o.Object.Pose);
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

        return observations;
    }

    /// <summary>
    /// The occluder points <c>VisionComponent</c> carries at <c>+4</c> and <c>AddLiftOccluder</c> applies the
    /// lift transform to. MISSING: the inventory names the call
    /// (<c>Transform3d::ApplyTo&lt;float&gt;(vector&lt;Point3f&gt; const&amp;, vector&lt;Point3f&gt;&amp;)</c> at
    /// 0x65653E on <c>(this+4, sp+0x14)</c>) but not the <c>Point3f</c> values at <c>VisionComponent+4</c>,
    /// so the lift occluder cannot be filed exactly. Empty until that read.
    /// </summary>
    public IReadOnlyList<Vec3> LiftOccluderPoints { get; set; } = Array.Empty<Vec3>();

    // fidelity: M11-037
    /// <summary>
    /// <c>VisionComponent::AddLiftOccluder(unsigned int)</c> 0x6564D8 (C3.2/Q2.1): the raw robot state at the
    /// timestamp -> <c>Robot::GetLiftTransformWrtCamera(liftHeight, liftAngle)</c> 0x4BA6F8 ->
    /// <c>Transform3d::ApplyTo</c> the occluder points (0x65653E) -> <c>Camera::Project3dPoints</c> 0x4BA710
    /// -> <c>Camera::AddOccluder</c> 0x4BA71C with the transform's z scale
    /// <c>sqrt(t[0x20]^2+t[0x24]^2+t[0x28]^2)</c>. The point values are MISSING (see
    /// <see cref="LiftOccluderPoints"/>), so with the default empty list this files nothing.
    /// </summary>
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
        // the transform's z scale, sqrt(t[0x20]^2+t[0x24]^2+t[0x28]^2) (0x656554..0x656590)
        double scale = liftInCamera.Rotation.Row(2).Length;
        camera.Occluders.Add(px, scale);
    }

    /// <summary>
    /// <c>BlockConfigurationManager::Update(Robot)</c> 0x616D7C (C3.2/Q2.2). The engine gates it on
    /// <c>[this+0x24]</c>/<c>[this+0xc]</c> and <c>DidAnyObjectsMovePastThreshold</c>, then runs
    /// <c>UpdateAllBlockConfigs</c>, <c>PruneFullPyramids</c>, <c>UpdateLastConfigCheckBlockPoses</c> and
    /// <c>NotifyBroadcasterOfConfigurationManagerUpdate</c>. MISSING: the meanings of <c>[this+0x24]</c> and
    /// <c>[this+0xc]</c> (what sets them) are not in the inventory, so the gate cannot be reproduced; the
    /// stack's <c>BlockConfigurationManager.Update()</c> is hooked here (M12 sets it) without that gate.
    /// </summary>
    // fidelity: M11-037
    public Action? BlockConfigurationManagerUpdate { get; set; }

    // fidelity: M11-037
    /// <summary>
    /// <c>BlockWorld::UpdatePoseOfStackedObjects()</c> 0x621794 (C3.2/Q2.3). Cross-layer: its body needs the
    /// M10 pose/transform stack, <c>CarryingComponent</c>, <c>ObservableObject::InitPose</c> and the
    /// M11-007 confirmer's <c>AddObjectRelativeObservation</c> (0x6219C0). MISSING: those interfaces are not
    /// built here, so this is the wired entry point only.
    /// </summary>
    public void UpdatePoseOfStackedObjects()
    {
        // entry point wired at 0x6250D0; body MISSING (M10/M12).
    }

    // fidelity: M11-037
    /// <summary>
    /// <c>BlockWorld::UpdateMarkerlessObjects(unsigned int)</c> 0x625704 (C3.2/Q2.4). Cross-layer: its body
    /// needs the M10/M12 charger/dock pose, the robot bounding quad and the BlockWorld object lifecycle
    /// (<c>DeleteLocatedObjects</c>). MISSING: those interfaces are not built here, so this is the wired entry
    /// point only.
    /// </summary>
    public void UpdateMarkerlessObjects(uint timestamp)
    {
        // entry point wired at 0x62521A; body MISSING (M10/M12).
    }

    private static double QuadArea(Vec2[] c)
    {
        var q = new[] { c[0], c[2], c[3], c[1] };
        double a = 0;
        for (int i = 0; i < 4; i++) a += q[i].X * q[(i + 1) % 4].Y - q[(i + 1) % 4].X * q[i].Y;
        return Math.Abs(a) / 2;
    }

    /// <summary><c>AddAndUpdateObjects</c> for one observed object.</summary>
    // fidelity: M11-004
    private ObjectObservation? AddAndUpdateObject(ObjectType type, List<ObservedMarker> seen, Pose3d pose, double rms, uint timestamp)
    {
        // The native path looks the connected counterpart up by ObjectID (GetConnectedActiveObjectByIdHelper
        // 0x0061F58C, called at 0x00620E78). If it is absent the engine warns ("Observed active object of
        // type %s but it's not connected. Is the battery plugged in?", string 0xBF854B) and rate-limits the
        // warning with a 10 s cooldown (0x00620E9E..0x00620EDA), then continues to 0x00620EDE. It does
        // not drop the observation. The stack keeps the observation too.
        uint? id = null;
        pose = ClampPoseToFlat(pose);
        // When several connected objects share this type, the engine picks the one the observation matches
        // by pose (FindObjectMatchForObservation 0x5063CC); the connected lookup alone would take the first.
        var sameType = _connected().Where(c => c.Type == type).ToList();
        if (sameType.Count > 0)
        {
            id = sameType[0].ObjectId;
            if (sameType.Count > 1 && FindObjectMatchForObservation(type, pose) is { } match
                && sameType.Any(c => c.ObjectId == match.ObjectId))
                id = match.ObjectId;
        }
        if (id is null)
        {
            // passive objects (the charger) get fixed ids; an unconnected active observation gets its
            // id from its type (M11-013 policy; the native id source for it is not in the inventory).
            id = type switch { ObjectType.Block_LIGHTCUBE1 => 1u, ObjectType.Block_LIGHTCUBE2 => 2u, ObjectType.Block_LIGHTCUBE3 => 3u, ObjectType.Charger_Basic => ChargerGeometry.ObjectId, _ => 0u };
            if (CubeGeometry.IsActiveObjectType(type)) WarnUnconnectedObservation(id.Value, type, timestamp);
        }
        ObservableObject obj; bool isNew; Pose3d prevPose; PoseState prevState;
        lock (_gate)
        {
            isNew = !_objects.TryGetValue(id.Value, out obj!);
            if (isNew) { obj = new ObservableObject(id.Value, type, CubeGeometry.MarkersFor(type)); _objects[id.Value] = obj; }
            prevPose = obj.Pose; prevState = obj.PoseState;
            obj.Pose = pose;
            // M11-007: the confirmer's sighting count. A new entry starts at 1; a matching second sighting
            // makes 2, the first confirmation; a mismatch resets to 1. The pose state and this count are
            // separate engine concepts (the count is ObjectPoseConfirmer's; PoseState is the object's).
            AddVisualObservation(obj, pose, ObjectMatchToleranceMm(type), ObjectMatchAngleRad);
            obj.PoseState = PoseState.Known;
            obj.LastObservedTimestamp = timestamp;
            obj.TimesObserved++;
            obj.UnobservedCount = 0;
            obj.LastObservedMarkers = seen.Select(s => s.Code).ToList();
            obj.LastReprojectionRmsPx = rms;
            var xs = seen.SelectMany(s => s.Corners).Select(p => p.X).ToList();
            var ys = seen.SelectMany(s => s.Corners).Select(p => p.Y).ToList();
            obj.LastImageRect = (xs.Min(), ys.Min(), xs.Max() - xs.Min(), ys.Max() - ys.Min());
        }
        if (prevState != PoseState.Known) PoseStateChanged?.Invoke(obj, prevState, PoseState.Known);
        var observation = new ObjectObservation(obj, timestamp, obj.LastObservedMarkers, prevPose, prevState, isNew, rms);
        Log?.Invoke($"{(isNew ? "new" : "seen")} {obj} via {string.Join(",", obj.LastObservedMarkers)} rms={rms:F2}px");
        ObjectObserved?.Invoke(observation);
        return observation;
    }

    /// <summary>
    /// M11-004: the native warning path at 0x00620E7C..0x00620EDA. The warning is emitted only when the
    /// object's cooldown entry is absent or has elapsed; either way the entry is set to now + 10 s, so a
    /// cube that stays unconnected warns at most once per ten seconds. The observation is not affected.
    /// </summary>
    private void WarnUnconnectedObservation(uint objectId, ObjectType type, uint timestamp)
    {
        double now = timestamp / 1000.0;
        lock (_gate)
        {
            if (_unconnectedWarnCooldown.TryGetValue(objectId, out var until) && now < until) return;
            _unconnectedWarnCooldown[objectId] = now + UnconnectedWarnCooldownSec;
        }
        Log?.Invoke($"Observed active object of type {type} but it's not connected. Is the battery plugged in?");
    }

    // fidelity: M11-004
    /// <summary>
    /// The object-match translation tolerance: the object's stored extent scaled by 0.8 (thunk 0x4E025C,
    /// <c>ObservableObject</c> virtual <c>+0x30</c>; the extent virtual <c>+0x2c</c> returns <c>+0x88</c>).
    /// A light cube's extent is <c>(44,44,44)</c>, so the tolerance is <c>(35.2, 35.2, 35.2)</c> mm.
    /// </summary>
    public static Vec3 ObjectMatchToleranceMm(ObjectType type)
    {
        var size = CubeGeometry.SizeOf(type);
        return new Vec3(size.X * ObjectMatchDistanceFactor, size.Y * ObjectMatchDistanceFactor, size.Z * ObjectMatchDistanceFactor);
    }

    // fidelity: M11-004
    /// <summary>
    /// <c>ObjectPoseConfirmer::FindObjectMatchForObservation</c> 0x5063CC, reduced to the located objects of
    /// this stack's world: the primary match is the closest located object of the same <see cref="ObjectType"/>
    /// within <see cref="ObjectMatchToleranceMm"/> and <see cref="ObjectMatchAngleRad"/>
    /// (<c>FindLocatedClosestMatchingObjectHelper</c> 0x61FA68, predicate 0x6281DA, which narrows the
    /// captured tolerance to <c>abs(delta)</c> so the closest survives); if none, the fallback is the last
    /// located object of the type within tolerance (<c>ObservableObject::IsSameAs</c> 0x8769A8, the
    /// confirmer's list loop 0x5065AE..0x506674). The engine's exact <c>FindLocatedObjectHelper</c> return
    /// (0x61EB78) is a RECOVERABLE_GAP, so this is the closest/last rule and not a proven tie-break.
    /// </summary>
    public ObservableObject? FindObjectMatchForObservation(ObjectType type, Pose3d observedPose)
    {
        var tol = ObjectMatchToleranceMm(type);
        List<ObservableObject> located;
        lock (_gate) located = _objects.Values.Where(o => o.IsLocated && o.Type == type).ToList();
        ObservableObject? closest = null; double best = double.MaxValue;
        foreach (var o in located)
        {
            if (!o.Pose.IsSameAs(observedPose, tol, ObjectMatchAngleRad, out var delta)) continue;
            double d2 = delta.Dot(delta);
            if (d2 < best) { best = d2; closest = o; }
        }
        if (closest is not null) return closest;
        ObservableObject? last = null;
        foreach (var o in located)
            if (o.Pose.IsSameAs(observedPose, tol, ObjectMatchAngleRad, out _)) last = o;
        return last;
    }

    // fidelity: M11-007
    /// <summary>
    /// <c>ObjectPoseConfirmer::AddVisualObservation</c> 0x50684C: a new entry starts at count 1; a matching
    /// sighting increments the count; a mismatching sighting resets it to 1 and stores the new pose. Returns
    /// the new count; the caller's confirmation test is <c>count &gt; 1</c>.
    /// </summary>
    public int AddVisualObservation(ObservableObject obj, Pose3d observedPose, Vec3 tolMm, double tolRad)
    {
        if (obj.PoseConfirmationCount == 0) { obj.PoseConfirmationCount = 1; obj.ReferencePose = observedPose; }
        else if (observedPose.IsSameAs(obj.ReferencePose, tolMm, tolRad, out _)) obj.PoseConfirmationCount++;
        else { obj.PoseConfirmationCount = 1; obj.ReferencePose = observedPose; }
        return obj.PoseConfirmationCount;
    }

    // fidelity: M11-007
    /// <summary>
    /// <c>ObjectPoseConfirmer::IsObjectConfirmedAtObservedPose</c> 0x50634C..0x5063B8: the entry's count must
    /// be at least 2 and the observed pose must match the stored reference pose.
    /// </summary>
    public bool IsObjectConfirmedAtObservedPose(ObservableObject obj, Pose3d observedPose)
    {
        var tol = ObjectMatchToleranceMm(obj.Type);
        return obj.PoseConfirmationCount >= 2 && observedPose.IsSameAs(obj.ReferencePose, tol, ObjectMatchAngleRad, out _);
    }

    /// <summary>
    /// <c>ObservableObject::ClampPoseToFlat</c> 0x00877330: a cube resting on a surface has one axis
    /// vertical; when the solved pose is within <see cref="FlatClampAngleRad"/> of that, snap it. The
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

    /// <summary>
    /// <c>CheckForUnobservedObjects</c>: every located object not in <paramref name="observedIds"/> that the
    /// camera should have seen is marked unobserved, and forgotten after enough misses. Skipped while the robot
    /// is moving or rotating too fast (the engine's <c>WasMoving</c> / <c>WasRotatingTooFast</c> gates).
    /// </summary>
    // fidelity: M11-004, M11-007, M11-008, M11-010
    public IReadOnlyList<ObservableObject> CheckForUnobservedObjects(CameraModel camera, uint timestamp, ISet<uint> observedIds, bool robotMoving, bool rotatingTooFast)
    {
        var forgotten = new List<ObservableObject>();
        if (robotMoving || rotatingTooFast) return forgotten;
        List<ObservableObject> located;
        lock (_gate) located = _objects.Values.Where(o => o.IsLocated && !observedIds.Contains(o.ObjectId)).ToList();
        foreach (var o in located)
        {
            bool visible = o.IsVisibleFrom(camera, VisibilityNormalAngleRad, MinVisibleMarkerSizePx, 0, 0,
                                           out var reason, out bool hasNothingBehind);
            // The engine marks an object unobserved in two cases, and its own log names all three flags:
            // "Marking object %d unobserved, which should have been seen, but wasn't. (shouldBeVisible:%d
            // hasNothingBehind:%d isDirty:%d". Either the object should have been visible (0x00622132), or
            // it should not have been but nothing was behind it and its pose is already Dirty
            // (0x0062211E). This stack only had the first.
            bool dirtyWithNothingBehind = !visible && hasNothingBehind && o.PoseState == PoseState.Dirty;
            if (!visible && !dirtyWithNothingBehind) continue;
            PoseState prev;
            bool unknown;
            lock (_gate)
            {
                o.UnobservedCount++;
                prev = o.PoseState;
                unknown = o.UnobservedCount >= UnobservedMissesToUnknown;
                if (unknown) o.PoseState = PoseState.Unknown;
            }
            Log?.Invoke($"object {o.ObjectId} unobserved (shouldBeVisible:{visible} hasNothingBehind:{hasNothingBehind} " +
                        $"reason:{reason}): miss {o.UnobservedCount}{(unknown ? " -> Unknown" : "")}");
            if (unknown) { PoseStateChanged?.Invoke(o, prev, PoseState.Unknown); forgotten.Add(o); }
        }
        return forgotten;
    }

    /// <summary>
    /// A cube that reports movement over the radio has a Dirty pose until it is seen again.
    ///
    /// <c>RobotToEngineImplMessaging::HandleActiveObjectMoved</c> 0x00533E30 calls
    /// <c>ObjectPoseConfirmer::MarkObjectDirty(object, false)</c> at 0x0053413C behind one guard, at
    /// 0x00534116: the robot must not be carrying the object <em>and</em> its pose state must be exactly
    /// Known. Either condition failing skips the call, which is why a cube on the lift reporting its own
    /// motion does not dirty the pose the lift is holding it at.
    /// </summary>
    /// <summary>
    /// Places an object the robot is holding. The engine does not do this by assignment - the object is
    /// parented to the lift and follows it - but the effect on the world model is the same, and this is
    /// the only way anything moves an object here without having seen it.
    /// </summary>
    public void SetCarriedPose(uint objectId, Pose3d pose)
    {
        lock (_gate) { if (_objects.TryGetValue(objectId, out var o)) o.Pose = pose; }
    }

    /// <summary>
    /// An object the robot has just let go of: still located, at the pose it was released at, but Dirty.
    /// <c>SetCarriedObjectAsUnattached</c> 0x006333D4 re-registers it through
    /// <c>AddRobotRelativeObservation(object, poseWrtRobot, PoseState 2)</c>, and 2 is Dirty.
    /// </summary>
    public void MarkReleased(uint objectId)
    {
        ObservableObject? o; PoseState prev;
        lock (_gate)
        {
            if (!_objects.TryGetValue(objectId, out o)) return;
            prev = o.PoseState; o.PoseState = PoseState.Dirty;
            if (prev == PoseState.Dirty) return;
        }
        PoseStateChanged?.Invoke(o, prev, PoseState.Dirty);
    }

    /// <summary>Records what the cube's radio says about its own motion; see <see cref="ObservableObject.IsMoving"/>.</summary>
    public void SetMoving(uint objectId, bool moving)
    {
        lock (_gate) { if (_objects.TryGetValue(objectId, out var o)) o.IsMoving = moving; }
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
        lock (_gate) located = _objects.Values.Where(o => o.IsLocated).ToList();
        foreach (var o in located)
        {
            if (carriedObjectIds is not null && carriedObjectIds.Contains(o.ObjectId)) continue;
            PoseState prev;
            lock (_gate) { prev = o.PoseState; o.PoseState = PoseState.Unknown; o.UnobservedCount = 0; }
            PoseStateChanged?.Invoke(o, prev, PoseState.Unknown);
            forgotten.Add(o);
        }
        return forgotten;
    }

    public void MarkDirty(uint objectId)
    {
        ObservableObject? o; PoseState prev;
        lock (_gate)
        {
            // M11-009: HandleActiveObjectMoved 0x00533E30 dirties only a pose that is exactly Known
            // (MarkObjectDirty's own guard; the not-carrying guard is at the caller, 0x00534116).
            // fidelity: M11-009
            if (!_objects.TryGetValue(objectId, out o) || o.PoseState != PoseState.Known) return;
            prev = o.PoseState; o.PoseState = PoseState.Dirty;
        }
        PoseStateChanged?.Invoke(o, prev, PoseState.Dirty);
    }

    /// <summary><c>MarkObjectUnknown</c> on request (used when a cube disconnects, and by tests).</summary>
    public void MarkUnknown(uint objectId)
    {
        ObservableObject? o; PoseState prev;
        lock (_gate)
        {
            if (!_objects.TryGetValue(objectId, out o) || o.PoseState == PoseState.Unknown) return;
            prev = o.PoseState; o.PoseState = PoseState.Unknown;
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
