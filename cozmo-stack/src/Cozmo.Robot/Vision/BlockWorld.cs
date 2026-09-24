using Cozmo.Protocol;

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

/// <summary>
/// The engine's <c>BlockWorld</c> reduced to what cubes need: located objects, their pose states, and the
/// per-frame update <c>UpdateObservedMarkers</c> → <c>CreateObjectsFromMarkers</c> → <c>AddAndUpdateObjects</c> →
/// <c>CheckForUnobservedObjects</c>.
///
/// NATIVE rules transcribed: an observed active object must match a connected active object of its type or it
/// is dropped ("Observed active object of type %s but it's not connected"); unobserved checks are skipped while
/// the robot was moving or rotating faster than 0.174533 rad/s; an object that should be visible
/// (<c>IsVisibleFrom(camera, 0.785398, ...)</c>) and is not gets <c>MarkObjectUnobserved</c>, and after enough
/// misses <c>MarkObjectUnknown</c>. INFERRED: the miss threshold (read as <c>cmp r3, #1</c>, taken as 2 misses);
/// a first observation makes the pose Known at once (the <c>ObjectPoseConfirmer</c>'s confirmation counting is
/// not transcribed); an <c>ObjectMoved</c> report marks a located cube Dirty (the engine's use of Dirty).
/// Pose clustering across an object's markers uses the engine's own tolerances, 5 mm and 5 degrees.
/// </summary>
public sealed class BlockWorld
{
    /// <summary><c>CheckForUnobservedObjects</c>'s rotation gate: 0.174533 rad/s (10 deg/s).</summary>
    public const double MaxRotationRateRadPerSec = 0.174533;

    /// <summary>
    /// 5 mm. <c>ObservableObjectLibrary::CreateObjectsFromMarkers</c> passes it to
    /// <c>ClusterObjectPoses(poses, object, distThreshold, angleThreshold, clusters)</c> as the third
    /// argument: <c>0x40A00000</c> built at 0x006254F4.
    /// </summary>
    public const double ClusterDistanceMm = 5.0;

    /// <summary>
    /// 0.0872665 rad, 5 degrees: the angle threshold of the same call, built from <c>0x3DB2B8C3</c> at
    /// 0x00625498 through the <c>Radians</c> constructor at 0x006254E0.
    /// </summary>
    public const double ClusterAngleRad = 0.0872665;

    /// <summary>
    /// 0.349066 rad, 20 degrees: the angle <c>ObjectPoseConfirmer::UpdatePoseInInstance</c> passes to
    /// <c>ObservableObject::ClampPoseToFlat</c> (<c>0x3EB2B8C2</c> at 0x00505F16), which is the path an
    /// observation of an object already in the world takes. On the creation path
    /// <c>CreateObjectsFromMarkers</c> asks the object itself for the angle in degrees and multiplies by
    /// 0.0174533 (0x00625566); no shipped object overrides it to anything this stack can see.
    /// </summary>
    public const double FlatClampAngleRad = 0.349066;
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
    public int UnobservedMissesToUnknown { get; set; } = 2;

    /// <summary>
    /// 40 pixels: what <c>BlockWorld::CheckForUnobservedObjects</c> 0x006220F0 passes as the minimum
    /// projected marker size (0x42200000), alongside a face-normal angle of 0.785398. This had been 10.
    ///
    /// It is not the only value the engine uses for that argument - <c>SearchForBlockHelper</c> and the
    /// ghost-block check both pass 0 - but this is the call that decides whether a located object should
    /// have been seen, which is what this threshold is for here.
    /// </summary>
    public double MinVisibleMarkerSizePx { get; set; } = 40;
    /// <summary>Markers whose pose solve leaves more than this reprojection error are ignored (LOCAL, 3 px).</summary>
    public double MaxReprojectionRmsPx { get; set; } = 3.0;
    /// <summary>
    /// LOCAL_POLICY for offline tools and tests without a cube radio: observed cube types that are not connected
    /// still get an object, with the object id taken from the type (LIGHTCUBE1 → 1, ...). Off by default, which is
    /// the engine's behaviour.
    /// </summary>
    public bool AllowUnconnectedObjects { get; set; }

    private readonly Func<IEnumerable<(uint ObjectId, ObjectType Type)>> _connected;
    private readonly Dictionary<uint, ObservableObject> _objects = new();
    private readonly object _gate = new();

    /// <summary><paramref name="connectedActiveObjects"/> answers the engine's "connected active object of this type" question (from the cube radio).</summary>
    public BlockWorld(Func<IEnumerable<(uint ObjectId, ObjectType Type)>> connectedActiveObjects) => _connected = connectedActiveObjects;

    public event Action<ObjectObservation>? ObjectObserved;
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
    /// <c>UpdateObservedMarkers</c>: turn a frame's markers into object observations. <paramref name="camera"/>
    /// is the camera at the frame's timestamp. Returns the objects observed in this frame.
    /// </summary>
    public IReadOnlyList<ObjectObservation> UpdateObservedMarkers(IReadOnlyList<ObservedMarker> markers, CameraModel camera, uint timestamp)
    {
        var observations = new List<ObjectObservation>();
        // BlockWorld::UpdateObservedMarkers 0x00624F98 clears the camera's occluder list at the top of
        // the frame; AddAndUpdateObjects fills it again from the markers this frame actually saw.
        camera.Occluders.Clear();
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

    private static double QuadArea(Vec2[] c)
    {
        var q = new[] { c[0], c[2], c[3], c[1] };
        double a = 0;
        for (int i = 0; i < 4; i++) a += q[i].X * q[(i + 1) % 4].Y - q[(i + 1) % 4].X * q[i].Y;
        return Math.Abs(a) / 2;
    }

    /// <summary><c>AddAndUpdateObjects</c> for one observed object.</summary>
    private ObjectObservation? AddAndUpdateObject(ObjectType type, List<ObservedMarker> seen, Pose3d pose, double rms, uint timestamp)
    {
        uint? id = null;
        foreach (var (oid, t) in _connected()) if (t == type) { id = oid; break; }
        if (id is null)
        {
            if (!AllowUnconnectedObjects && CubeGeometry.IsActiveObjectType(type))
            {
                Log?.Invoke($"Observed active object of type {type} but it's not connected");
                return null;
            }
            // passive objects (the charger) get fixed ids; unconnected cubes theirs by type (LOCAL)
            id = type switch { ObjectType.Block_LIGHTCUBE1 => 1u, ObjectType.Block_LIGHTCUBE2 => 2u, ObjectType.Block_LIGHTCUBE3 => 3u, ObjectType.Charger_Basic => ChargerGeometry.ObjectId, _ => 0u };
        }
        pose = ClampPoseToFlat(pose);
        ObservableObject obj; bool isNew; Pose3d prevPose; PoseState prevState;
        lock (_gate)
        {
            isNew = !_objects.TryGetValue(id.Value, out obj!);
            if (isNew) { obj = new ObservableObject(id.Value, type, CubeGeometry.MarkersFor(type)); _objects[id.Value] = obj; }
            prevPose = obj.Pose; prevState = obj.PoseState;
            obj.Pose = pose;
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
    /// <c>ObservableObject::ClampPoseToFlat</c> 0x00877330: a cube resting on a surface has one axis
    /// vertical; when the solved pose is within <see cref="FlatClampAngleRad"/> of that, snap it. The
    /// engine takes the rotated parent Z axis, <c>acos</c> of the magnitude of its largest component, and
    /// compares that with the angle it was given (0x0087736A..0x0087737E).
    /// </summary>
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
