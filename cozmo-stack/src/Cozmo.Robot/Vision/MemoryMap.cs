using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

/// <summary>
/// <c>Anki::Cozmo::MemoryMapTypes::EContentType</c>, in the engine's order. The names are the engine's own
/// table at 0x00BFFAC8, read straight through: Unknown, ClearOfObstacle, ClearOfCliff, ObstacleObservable,
/// ObstacleCharger, ObstacleChargerRemoved, ObstacleProx, ObstacleUnrecognized, Cliff, InterestingEdge,
/// NotInterestingEdge.
/// </summary>
public enum MemoryMapContentType : byte
{
    Unknown = 0,
    ClearOfObstacle = 1,
    ClearOfCliff = 2,
    ObstacleObservable = 3,
    ObstacleCharger = 4,
    ObstacleChargerRemoved = 5,
    ObstacleProx = 6,
    ObstacleUnrecognized = 7,
    Cliff = 8,
    InterestingEdge = 9,
    NotInterestingEdge = 10,
}

/// <summary>What the engine puts in the map, and what it treats as being in the way.</summary>
// fidelity: M14-007
public static class MemoryMapTypes
{
    /// <summary>
    /// <c>ObjectFamilyToMemoryMapContentType(family, adding)</c> 0x0067F4C0, a <c>tbb</c> on the family less
    /// one: Block, LightCube and CustomObject give <see cref="MemoryMapContentType.ObstacleObservable"/>
    /// when the object is being added and <see cref="MemoryMapContentType.ClearOfObstacle"/> when it is
    /// being removed (0x0067F4D8); the charger gives <see cref="MemoryMapContentType.ObstacleCharger"/> and
    /// <see cref="MemoryMapContentType.ObstacleChargerRemoved"/> (0x0067F4E2); Ramp and Mat fall through to
    /// <see cref="MemoryMapContentType.Unknown"/>; and a markerless object is refused outright with
    /// "ContentType MarkerlessObject addition is not supported" (0x0067F4EC). So the collision obstacle an
    /// unexpected movement leaves behind is in the world model but never in the memory map.
    /// </summary>
    public static MemoryMapContentType ContentTypeForFamily(ObjectFamily family, bool adding) => family switch
    {
        ObjectFamily.Block or ObjectFamily.LightCube or ObjectFamily.CustomObject =>
            adding ? MemoryMapContentType.ObstacleObservable : MemoryMapContentType.ClearOfObstacle,
        ObjectFamily.Charger =>
            adding ? MemoryMapContentType.ObstacleCharger : MemoryMapContentType.ObstacleChargerRemoved,
        _ => MemoryMapContentType.Unknown,
    };

    /// <summary>
    /// The type mask <c>BehaviorInteractWithFaces::CanDriveIdealDistanceForward</c> hands the map, the
    /// eleven-entry <c>EnumToValueEntry&lt;EContentType, bool&gt;</c> array at 0x00C67962: pairs of
    /// (type, flag) reading 00 00, 01 00, 02 00, 03 01, 04 01, 05 00, 06 01, 07 01, 08 01, 09 01, 0a 01.
    /// So everything blocks except Unknown, ClearOfObstacle, ClearOfCliff and ObstacleChargerRemoved -
    /// the two edge types included, since an unexplored edge is not somewhere to drive into either.
    /// </summary>
    public static bool BlocksTheRobot(MemoryMapContentType type) => type switch
    {
        MemoryMapContentType.ObstacleObservable or MemoryMapContentType.ObstacleCharger
            or MemoryMapContentType.ObstacleProx or MemoryMapContentType.ObstacleUnrecognized
            or MemoryMapContentType.Cliff or MemoryMapContentType.InterestingEdge
            or MemoryMapContentType.NotInterestingEdge => true,
        _ => false,
    };
}

/// <summary>One region of the map: a polygon in the world plane and what is in it.</summary>
public sealed record MemoryMapRegion(MemoryMapContentType Type, Vec2[] Polygon, uint? ObjectId, uint Timestamp);

/// <summary>
/// The engine's memory map (<c>Anki::Cozmo::MemoryMap</c>, reached through
/// <c>MapComponent::GetCurrentMemoryMapHelper</c> 0x0067EA5C): what is known about the ground around the
/// robot, as regions with a content type.
///
/// The engine stores those regions in a quad tree (<c>QuadTree</c> 0x00684C08, nodes subdividing down to
/// <c>GetContentPrecisionMM</c>). Native insertion and ray queries use that tree. The legacy polygon list
/// remains for separately unported producers, transforms and region inspection. What is <b>not</b> here is the content the engine gets from
/// one place this stack has no source for: the overhead-edge processing that produces
/// <see cref="MemoryMapContentType.InterestingEdge"/> and <see cref="MemoryMapContentType.NotInterestingEdge"/>
/// (<c>MapComponent::AddVisionOverheadEdges</c> 0x0067F814, which needs the vision system's ground-plane
/// edge frames). The obstacles are here, and so is the ground the robot has been over
/// (<see cref="UpdateRobotPose"/>).
///
/// <b>What goes in.</b> <c>MapComponent::AddObservableObject</c> 0x0067ECFC takes the object's bounding
/// quad (its virtual <c>GetBoundingQuadXY(pose, 0)</c>, the vtable slot at +0x50), turns it into a polygon
/// (<c>Polygon2f::ImportQuad2d</c>) and inserts it with a <c>MemoryMapData_ObservableObject</c>, whose
/// constructor 0x00684A38 stamps content type 3. <c>BlockWorld::AddMarkerlessObject</c> 0x00622380 inserts
/// a <c>MemoryMapData_Cliff</c> (type 8, constructor 0x00684984) for a <c>CliffDetection</c> object and a
/// <c>MemoryMapData_ProxObstacle</c> (type 6, 0x00684B54) for a <c>ProxObstacle</c> one - and nothing at
/// all for a <c>CollisionObstacle</c> (the type test at 0x0062259A falls through to 0x006226EA).
/// </summary>
public sealed partial class MemoryMap
{
    internal readonly SourceQuadTree Tree = new();
    /// <summary>Read-only diagnostic snapshot of native leaf cells by content type, including Unknown.
    /// Counts cells, not source polygons or distinct payloads; does not change the map.</summary>
    public IReadOnlyDictionary<MemoryMapContentType,int> NativeContentCounts => Tree.ContentCounts();
    internal SourceLogSink? SourceLog { get => Tree.SourceLog; set => Tree.SourceLog = value; }
    internal Action<string>? Missing { get => Tree.Missing; set => Tree.Missing = value; }
    public double ExploredRegionAreaM2
    {
        get { Missing?.Invoke("MISSING: M11-045 explored-area helper+0x38: tree does not yet contain all map content (B-M3M4-20)");return double.NaN; }
    }
    internal uint LastObservedTime => Tree.Root.Content.Data.Last;
    // fidelity: M11-075, M11-046
    internal void TransformAging(SourceMapAging aging)
    {
        // The checked callback is SourceMapAging.Apply. Its real whole-map recipient first constructs
        // root geometry and runs Transform, not a linear walk over legacy Regions.
        Behavior.SteppedBehavior.ReportMissing("M11-075/M11-046 aging at 0x0067DCF4 -> whole-map TransformContent 0x0068140C/0x0068A414: root geometry, node Transform and subtype comparisons are not integrated; checked aging callback is not applied to legacy polygon regions");
    }
    // fidelity: M13-030 — native insertion; legacy region queries remain M11-046.
    internal Action<Vec2[],CliffMapData>? CliffInsertForTest;
    internal void InsertCliff(Vec2[] polygon, CliffMapData data)
    { CliffInsertForTest?.Invoke(polygon,data); Tree.Insert(polygon,data,1); }
    // fidelity: M11-074, M13-017 — concrete vt+8, 0x006817C6..0x006817E0.
    internal Action<Vec2[],SourceMapData>? ObjectInsertForTest; // observation only
    internal Action<MemoryMapContentType,uint,SourceMapData>? ObjectBorderForTest; // observation only
    internal Action<SourceObjectRemoval>? ObjectRemovalForTest; // observation only
    internal void InsertObject(Vec2[] polygon,SourceMapData data)
    {
        ObjectInsertForTest?.Invoke(polygon,data);
        Tree.Insert(polygon,data,1);
    }
    internal void FillObjectBorder(uint timestamp)
    {
        const uint mask=(1u<<3)|(1u<<4)|(1u<<5)|(1u<<6)|(1u<<7)|(1u<<10); // 0x00C87675
        var data=new SourceMapData(MemoryMapContentType.NotInterestingEdge,timestamp,timestamp);
        ObjectBorderForTest?.Invoke(MemoryMapContentType.InterestingEdge,mask,data);
        Tree.FillBorder(MemoryMapContentType.InterestingEdge,mask,data);
    }
    internal void RemoveObject(SourceObjectRemoval removal)
    {
        ObjectRemovalForTest?.Invoke(removal);
        Tree.TransformContent(removal.Apply);
    }
    private readonly List<MemoryMapRegion> _regions = new();
    private readonly object _gate = new();

    public IReadOnlyList<MemoryMapRegion> Regions { get { lock (_gate) return _regions.ToList(); } }

    /// <summary>
    /// <c>MemoryMap::Insert(polygon, data)</c> 0x006817C6. Two points are enough: the overhead-edge
    /// processing inserts lines that way, both the clear run that came out too short to be a triangle
    /// (0x006800BA) and every interesting edge (0x006802D2).
    /// </summary>
    public void Insert(Vec2[] polygon, MemoryMapContentType type, uint? objectId = null, uint timestamp = 0)
    {
        if (polygon.Length < 2) return;
        lock (_gate) _regions.Add(new MemoryMapRegion(type, polygon, objectId, timestamp));
    }

    /// <summary><c>MemoryMap::HasContentType</c> 0x006817A8.</summary>
    public bool HasContentType(MemoryMapContentType type)
    {
        return Tree.HasContentType(type);
    }

    public void Clear() { lock (_gate) _regions.Clear(); }

    public const double RobotPoseMoveMm = 8.0;
    public static readonly double RobotPoseTurnRad = BitConverter.UInt32BitsToSingle(0x3EB2B8C2);

    // fidelity: M11-074, M8-026, M11-094
    // 0x0067E224..0x0067E486: insertion/notification unit; SourceMapComponent owns admission and saved pose.
    public MemoryMapRegion? UpdateRobotPose(Pose3d robotPose, bool cliffDetected = false, uint timestamp = 0) =>
        UpdateRobotPose(robotPose,cliffDetected,timestamp,null);
    internal MemoryMapRegion? UpdateRobotPose(Pose3d robotPose,bool cliffDetected,uint timestamp,
        Action<Cozmo.Robot.Manipulation.WhiteboardClearQuad>? processClearQuad)
    {
        using var current=new Pose3d(robotPose);
        var size=MarkerlessObject.SizeByType(ObjectType.ProxObstacle)!.Value;
        float hx=(float)size.X*0.5f, hy=(float)size.Y*0.5f, hz=(float)size.Z*0.5f;
        var corners=new[]{new Vec3(hx,hy,hz),new Vec3(-hx,hy,hz),new Vec3(hx,-hy,hz),new Vec3(-hx,-hy,hz)};
        Vec2 XY(Vec3 v)=>new(v.X,v.Y);
        var points=corners.Select(p=>XY(current.Apply(p))).ToArray();
        var patch=new[]{points[0],points[1],points[3],points[2]}; // ImportQuad2d, no planner corner sorting
        SourceMapData data;
        if(cliffDetected)
        {
            var axis=current.Quaternion.Rotate(new Vec3(1,0,0));
            data=new CliffMapData(XY(axis),timestamp);
        }
        else data=new SourceMapData(MemoryMapContentType.ClearOfCliff,timestamp,timestamp);
        InsertObject(patch,data);
        var q=Cozmo.Robot.Manipulation.ObjectFootprint.RobotBoundingQuadXY(current,0);
        Vec2 Q(int i)=>new(q[i].X,q[i].Y);
        InsertObject(new[]{Q(0),Q(1),Q(3),Q(2)},new SourceMapData(MemoryMapContentType.ClearOfObstacle,timestamp,timestamp));
        var full=new Cozmo.Robot.Manipulation.WhiteboardClearQuad(Q(0),Q(1),Q(2),Q(3));
        if(processClearQuad is {} clear)clear(full);
        else Behavior.SteppedBehavior.ReportMissing("M8-026 ProcessClearQuad 0x0067E47E: native robot footprint is built; AIWhiteboard owner is unbound");
        return new MemoryMapRegion(data.Type,patch,null,timestamp);
    }

    /// <summary>
    /// <c>MemoryMap::HasCollisionRayWithTypes(from, to, types)</c> 0x0068176E: the types are folded into a
    /// flag mask (<c>EContentTypeToFlag</c> per entry whose bool is set) and handed to the quad tree's ray
    /// walk. <paramref name="blocks"/> defaults to
    /// <see cref="MemoryMapTypes.BlocksTheRobot"/>, the mask the face behaviour passes.
    /// </summary>
    public bool HasCollisionRayWithTypes(Vec2 from, Vec2 to, Func<MemoryMapContentType, bool>? blocks = null)
    {
        blocks ??= MemoryMapTypes.BlocksTheRobot;
        uint mask = 0;
        for (int i = 0; i < 11; i++)
            if (blocks((MemoryMapContentType)i)) mask |= 1u << i;
        return Tree.HasCollisionRayWithTypes(from,to,mask);
    }

    // ------------------------------------------------------------------ the overhead edges

    /// <summary>
    /// The four tuning constants of the overhead-edge processing, at 0x00C8764C..0x00C87658:
    /// <c>kOverheadEdgeCloseMaxLenForTriangle_mm</c> 15, <c>kOverheadEdgeFarMaxLenForLine_mm</c> 15,
    /// <c>kOverheadEdgeFarMinLenForClearReport_mm</c> 3 and <c>kOverheadEdgeSegmentNoiseLen_mm</c> 6.
    /// </summary>
    public const double OverheadEdgeCloseMaxLenForTriangleMm = 15.0;
    public const double OverheadEdgeFarMaxLenForLineMm = 15.0;
    public const double OverheadEdgeFarMinLenForClearReportMm = 3.0;
    public const double OverheadEdgeSegmentNoiseLenMm = 6.0;

    /// <summary>
    /// How far a run of edge points may bend before it is closed off: the dot product of one segment's
    /// unit direction with the last must reach 0.766 (0x3F441893 at 0x0067F980), the cosine of forty
    /// degrees.
    /// </summary>
    public static readonly double OverheadEdgeRunDirectionCos = BitConverter.UInt32BitsToSingle(0x3F441893);

    /// <summary>
    /// The squared length a run must exceed before anything is written for it: the literal 6.00001 the
    /// comparison at 0x0067FA14 uses - the noise length, taken against a squared length as the engine
    /// takes it.
    /// </summary>
    public static readonly double OverheadEdgeMinRunLengthSq = BitConverter.UInt32BitsToSingle(0x40C00015);

    /// <summary>
    /// The type mask of the first ray query (the table at 0x00C8768B): everything but Unknown,
    /// ClearOfObstacle and ClearOfCliff, so both edge types and a cliff stop a clear report.
    /// </summary>
    public static bool OverheadEdgeBlocksAll(MemoryMapContentType t) =>
        t >= MemoryMapContentType.ObstacleObservable;

    /// <summary>
    /// The type mask of the second ray query (the table at 0x00C876A1): the five obstacle types only, so
    /// neither a cliff nor an edge stops the clear report a point beyond the ROI would make.
    /// </summary>
    public static bool OverheadEdgeBlocksObstacles(MemoryMapContentType t) =>
        t >= MemoryMapContentType.ObstacleObservable && t <= MemoryMapContentType.ObstacleUnrecognized;

    /// <summary>
    /// The mask of the border pass (the table at 0x00C87675): the five obstacle types and
    /// NotInterestingEdge. An interesting edge that touches one of those is not a frontier worth going
    /// to look at, and the pass at the end of <see cref="AddVisionOverheadEdges"/> writes it off.
    /// </summary>
    public static bool OverheadEdgeBorderMask(MemoryMapContentType t) =>
        (t >= MemoryMapContentType.ObstacleObservable && t <= MemoryMapContentType.ObstacleUnrecognized)
        || t == MemoryMapContentType.NotInterestingEdge;

    /// <summary>
    /// <c>QuadTree::GetContentPrecisionMM</c> 0x00685000: ten millimetres, the size of the smallest node
    /// the map subdivides to, and so how close two pieces of content have to be to count as neighbours.
    /// </summary>
    public const double ContentPrecisionMm = 10.0;

    // fidelity: M11-017, M11-089
    // The second argument is CURRENT pose, used only by the minimum-distance state write.
    // Historical geometry must come from the timestamped history owner, never this argument.
    public double? AddVisionOverheadEdges(OverheadEdgeFrame frame, Pose3d robotPose) =>
        AddOverheadFromHistory(frame,robotPose);

    /// <summary>Native global border pass, 0x006813B0/0x00689FAC. Returns the number of centre-point inserts.</summary>
    public int FillBorder(MemoryMapContentType type,Func<MemoryMapContentType,bool> mask,
                          MemoryMapContentType write,uint timestamp)
    {
        uint flags=0;
        // 0x006813C6..D8: all eleven enum/bool pairs participate in stored order.
        for(int i=0;i<11;i++)if(mask((MemoryMapContentType)i))flags|=1u<<i;
        return Tree.FillBorder(type,flags,new SourceMapData(write,timestamp,timestamp));
    }

    /// <summary>
    /// <c>MapComponent::FlagQuadAsNotInterestingEdges</c> 0x0067E6B0, which
    /// <c>BehaviorVisitInterestingEdge</c> calls once it has been to an edge and once it has a goal:
    /// the quad goes in as <see cref="MemoryMapContentType.NotInterestingEdge"/>, stamped with the last
    /// image's timestamp (0x0067E6EA), so nothing sends the robot back to it.
    /// </summary>
    public void FlagQuadAsNotInterestingEdges(Vec2[] quad, uint timestamp = 0) =>
        Insert(quad, MemoryMapContentType.NotInterestingEdge, null, timestamp);

    /// <summary>
    /// <c>MapComponent::FlagGroundPlaneROIInterestingEdgesAsUncertain</c> 0x0067E50C, which
    /// <c>BehaviorVisitInterestingEdge::StartWaitingForEdges</c> calls before it waits: the ground ROI at
    /// the robot's pose is handed to <c>TransformContent</c> with a lambda (0x00680B54) that turns
    /// content of type InterestingEdge into content of type Unknown and leaves everything else alone -
    /// so what is about to be looked at again stops counting as an edge until the vision system says so.
    /// Returns how many regions changed.
    /// </summary>
    public int FlagGroundPlaneRoiInterestingEdgesAsUncertain(Pose3d robotPose, uint timestamp = 0)
    {
        var roi = GroundPlaneROI.GroundQuad();
        var world = new Vec2[4];
        for (int i = 0; i < 4; i++)
        {
            var w = robotPose.Apply(new Vec3(roi[i].X, roi[i].Y, 0));
            world[i] = new Vec2(w.X, w.Y);
        }
        var polygon = new[] { world[0], world[1], world[3], world[2] };

        int changed = 0;
        lock (_gate)
            for (int i = 0; i < _regions.Count; i++)
            {
                var r = _regions[i];
                if (r.Type != MemoryMapContentType.InterestingEdge) continue;
                if (!r.Polygon.All(pt => OverheadEdgesDetector.InsidePolygon(polygon, pt))) continue;
                _regions[i] = r with { Type = MemoryMapContentType.Unknown, Timestamp = timestamp };
                changed++;
            }
        return changed;
    }

    /// <summary>Whether two regions are neighbours: any pair of their edges within <paramref name="tolerance"/>, or one inside the other.</summary>
    internal static bool PolygonsTouch(Vec2[] a, Vec2[] b, double tolerance)
    {
        for (int i = 0; i < a.Length; i++)
        {
            var a0 = a[i];
            var a1 = a[(i + 1) % a.Length];
            for (int j = 0; j < b.Length; j++)
            {
                var b0 = b[j];
                var b1 = b[(j + 1) % b.Length];
                if (SegmentDistance(a0, a1, b0, b1) <= tolerance) return true;
            }
        }
        if (b.Length >= 3 && a.Any(p => OverheadEdgesDetector.InsidePolygon(b, p))) return true;
        if (a.Length >= 3 && b.Any(p => OverheadEdgesDetector.InsidePolygon(a, p))) return true;
        return false;
    }

    private static double SegmentDistance(Vec2 a0, Vec2 a1, Vec2 b0, Vec2 b1)
    {
        if (SegmentIntersection(a0, a1, b0, b1) is not null) return 0;
        return Math.Min(Math.Min(PointToSegment(a0, b0, b1), PointToSegment(a1, b0, b1)),
                        Math.Min(PointToSegment(b0, a0, a1), PointToSegment(b1, a0, a1)));
    }

    private static double PointToSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        double len = ab.LengthSq;
        if (len < 1e-12) return (p - a).Length;
        double t = Math.Clamp(((p - a).X * ab.X + (p - a).Y * ab.Y) / len, 0, 1);
        return (p - (a + ab * t)).Length;
    }

    /// <summary>Where two segments cross, or null when they do not - <c>kmSegment2WithSegmentIntersection</c>.</summary>
    internal static Vec2? SegmentIntersection(Vec2 a0, Vec2 a1, Vec2 b0, Vec2 b1)
    {
        var r = a1 - a0;
        var s = b1 - b0;
        double denom = r.X * s.Y - r.Y * s.X;
        if (Math.Abs(denom) < 1e-12) return null;
        var d = b0 - a0;
        double t = (d.X * s.Y - d.Y * s.X) / denom;
        double u = (d.X * r.Y - d.Y * r.X) / denom;
        if (t < 0 || t > 1 || u < 0 || u > 1) return null;
        return a0 + r * t;
    }

    /// <summary>
    /// The object's footprint in the world plane: <c>ObservableObject::GetBoundingQuadXY(pose, 0)</c>, the
    /// size from the object's type with the charger's origin at its front lip, as the planner's import
    /// builds it.
    /// </summary>
    public static Vec2[] BoundingQuad(ObservableObject o)
    {
        var size = CubeGeometry.SizeOf(o.Type);
        var flat = new Pose3d(Mat3.AboutZ(o.Pose.AngleAroundZ), o.Pose.Translation with { Z = 0 });
        if (o.Type == ObjectType.Charger_Basic) flat = flat.Compose(new Pose3d(Mat3.Identity, new Vec3(size.X / 2, 0, 0)));
        return Rectangle(flat, size.X, size.Y);
    }

    /// <summary>A rectangle of that length and width centred on a pose, counter-clockwise.</summary>
    public static Vec2[] Rectangle(Pose3d pose, double lengthX, double widthY)
    {
        var corners = new[]
        {
            new Vec3(-lengthX / 2, -widthY / 2, 0), new Vec3(lengthX / 2, -widthY / 2, 0),
            new Vec3(lengthX / 2, widthY / 2, 0), new Vec3(-lengthX / 2, widthY / 2, 0),
        };
        var result = new Vec2[4];
        for (int i = 0; i < 4; i++) { var w = pose.Apply(corners[i]); result[i] = new Vec2(w.X, w.Y); }
        return result;
    }

    /// <summary>Whether a segment touches a polygon: either end inside it, or any edge crossed.</summary>
    public static bool SegmentTouchesPolygon(Vec2 a, Vec2 b, Vec2[] poly)
    {
        if (Inside(poly, a) || Inside(poly, b)) return true;
        for (int i = 0; i < poly.Length; i++)
            if (SegmentsIntersect(a, b, poly[i], poly[(i + 1) % poly.Length])) return true;
        return false;
    }

    private static bool Inside(Vec2[] poly, Vec2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if (poly[i].Y > p.Y != poly[j].Y > p.Y &&
                p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
    }

    private static bool SegmentsIntersect(Vec2 p1, Vec2 p2, Vec2 p3, Vec2 p4)
    {
        static double Cross(Vec2 o, Vec2 a, Vec2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        double d1 = Cross(p3, p4, p1), d2 = Cross(p3, p4, p2), d3 = Cross(p1, p2, p3), d4 = Cross(p1, p2, p4);
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return true;
        static bool OnSegment(Vec2 a, Vec2 b, Vec2 p) =>
            Math.Min(a.X, b.X) - 1e-9 <= p.X && p.X <= Math.Max(a.X, b.X) + 1e-9 &&
            Math.Min(a.Y, b.Y) - 1e-9 <= p.Y && p.Y <= Math.Max(a.Y, b.Y) + 1e-9;
        if (Math.Abs(d1) < 1e-9 && OnSegment(p3, p4, p1)) return true;
        if (Math.Abs(d2) < 1e-9 && OnSegment(p3, p4, p2)) return true;
        if (Math.Abs(d3) < 1e-9 && OnSegment(p1, p2, p3)) return true;
        if (Math.Abs(d4) < 1e-9 && OnSegment(p1, p2, p4)) return true;
        return false;
    }
}
