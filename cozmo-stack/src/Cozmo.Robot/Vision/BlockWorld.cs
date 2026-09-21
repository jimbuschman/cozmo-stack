using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

/// <summary>
/// <c>Anki::PoseState</c>: the engine prints "Known" and "Dirty" (<c>EnumToString(PoseState)</c>); Unknown is the
/// state of an object that has been marked unobserved often enough (<c>BlockWorld::MarkObjectUnknown</c>).
/// An object is <b>located</b> (<c>GetLocatedObjectByIdHelper</c> returns it) while its state is not Unknown.
/// </summary>
public enum PoseState { Unknown = 0, Known = 1, Dirty = 2 }

/// <summary>Why a marker or object is not visible, the engine's <c>KnownMarker::NotVisibleReason</c> (names from <c>NotVisibleReasonToString</c> usage).</summary>
public enum NotVisibleReason { IsVisible, NormalNotAligned, TooSmall, OutsideFieldOfView, Occluded, BehindCamera, NoMarkers }

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
    /// <c>ObservableObject::IsVisibleFromWithReason(camera, maxFaceNormalAngle, minMarkerImageSize, requireSomethingBehind,
    /// xBorderPad, yBorderPad)</c>: any of the object's markers passes <c>KnownMarker::IsVisibleFrom</c>: its normal
    /// faces the camera within the angle, its projected size is at least the minimum, all four corners are within
    /// the padded field of view. The engine's occluder list (lift, other objects) and <c>IsAnythingBehind</c> are
    /// not modelled (DEFERRED); a marker that passes the geometric tests counts as visible.
    /// </summary>
    public bool IsVisibleFrom(CameraModel camera, double maxFaceNormalAngleRad, double minMarkerImageSizePx, double xPad, double yPad, out NotVisibleReason reason)
    {
        reason = NotVisibleReason.NoMarkers;
        var worst = NotVisibleReason.NoMarkers;
        foreach (var m in Markers)
        {
            var r = MarkerVisibility(m, camera, maxFaceNormalAngleRad, minMarkerImageSizePx, xPad, yPad);
            if (r == NotVisibleReason.IsVisible) { reason = r; return true; }
            if (r > worst) worst = r;
        }
        reason = worst;
        return false;
    }

    public bool IsVisibleFrom(CameraModel camera, double maxFaceNormalAngleRad = 0.785398, double minMarkerImageSizePx = 10, double pad = 0)
        => IsVisibleFrom(camera, maxFaceNormalAngleRad, minMarkerImageSizePx, pad, pad, out _);

    /// <summary><c>KnownMarker::IsVisibleFrom</c> for one marker.</summary>
    public NotVisibleReason MarkerVisibility(KnownMarker m, CameraModel camera, double maxFaceNormalAngleRad, double minMarkerImageSizePx, double xPad, double yPad)
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
/// LOCAL: pose clustering across an object's markers uses 20 mm / 10 degrees.
/// </summary>
public sealed class BlockWorld
{
    /// <summary><c>CheckForUnobservedObjects</c>'s rotation gate: 0.174533 rad/s (10 deg/s).</summary>
    public const double MaxRotationRateRadPerSec = 0.174533;
    /// <summary>The visibility angle the world model and the cube-moved strategy use: 0.785398 rad (45 deg).</summary>
    public const double VisibilityNormalAngleRad = 0.785398;
    /// <summary>INFERRED: misses before an object's pose is forgotten.</summary>
    public int UnobservedMissesToUnknown { get; set; } = 2;
    /// <summary>INFERRED: minimum projected marker size for the visibility test, pixels.</summary>
    public double MinVisibleMarkerSizePx { get; set; } = 10;
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
                var cluster = remaining.Where(c => c.World.IsSameAs(seed.World, 20, 10 * Math.PI / 180)).ToList();
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
    /// <c>ClampPoseToFlat</c>: a cube resting on a surface has one axis vertical; when the solved pose is within
    /// a small angle of that, snap it (tolerance LOCAL: 8 degrees; the engine's value was not read).
    /// </summary>
    public static Pose3d ClampPoseToFlat(Pose3d pose, double toleranceRad = 8 * Math.PI / 180)
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
            if (!o.IsVisibleFrom(camera, VisibilityNormalAngleRad, MinVisibleMarkerSizePx, 0, 0, out var reason)) continue;
            PoseState prev;
            bool unknown;
            lock (_gate)
            {
                o.UnobservedCount++;
                prev = o.PoseState;
                unknown = o.UnobservedCount >= UnobservedMissesToUnknown;
                if (unknown) o.PoseState = PoseState.Unknown;
            }
            Log?.Invoke($"object {o.ObjectId} should be visible ({reason}) but was not: miss {o.UnobservedCount}{(unknown ? " -> Unknown" : "")}");
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
