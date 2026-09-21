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
/// <c>GetContentPrecisionMM</c>); this keeps the polygons themselves, which answers the same questions
/// exactly rather than to the tree's precision. What is <b>not</b> here is the content the engine gets from
/// places this stack has no source for: the overhead-edge processing that produces
/// <see cref="MemoryMapContentType.InterestingEdge"/> and <see cref="MemoryMapContentType.NotInterestingEdge"/>
/// (<c>MapComponent::AddVisionOverheadEdges</c> 0x0067F814, which needs the vision system's ground-plane
/// edge frames), and the explored/clear regions the robot's own passage leaves behind
/// (<c>MapComponent::UpdateRobotPose</c> 0x0067E224). The obstacles are here, and those are what the ray
/// queries ask about.
///
/// <b>What goes in.</b> <c>MapComponent::AddObservableObject</c> 0x0067ECFC takes the object's bounding
/// quad (its virtual <c>GetBoundingQuadXY(pose, 0)</c>, the vtable slot at +0x50), turns it into a polygon
/// (<c>Polygon2f::ImportQuad2d</c>) and inserts it with a <c>MemoryMapData_ObservableObject</c>, whose
/// constructor 0x00684A38 stamps content type 3. <c>BlockWorld::AddMarkerlessObject</c> 0x00622380 inserts
/// a <c>MemoryMapData_Cliff</c> (type 8, constructor 0x00684984) for a <c>CliffDetection</c> object and a
/// <c>MemoryMapData_ProxObstacle</c> (type 6, 0x00684B54) for a <c>ProxObstacle</c> one - and nothing at
/// all for a <c>CollisionObstacle</c> (the type test at 0x0062259A falls through to 0x006226EA).
/// </summary>
public sealed class MemoryMap
{
    private readonly List<MemoryMapRegion> _regions = new();
    private readonly object _gate = new();

    public IReadOnlyList<MemoryMapRegion> Regions { get { lock (_gate) return _regions.ToList(); } }

    /// <summary><c>MemoryMap::Insert(polygon, data)</c> 0x006817C6.</summary>
    public void Insert(Vec2[] polygon, MemoryMapContentType type, uint? objectId = null, uint timestamp = 0)
    {
        if (polygon.Length < 3) return;
        lock (_gate) _regions.Add(new MemoryMapRegion(type, polygon, objectId, timestamp));
    }

    /// <summary><c>MemoryMap::HasContentType</c> 0x006817A8.</summary>
    public bool HasContentType(MemoryMapContentType type)
    {
        lock (_gate) return _regions.Any(r => r.Type == type);
    }

    public void Clear() { lock (_gate) _regions.Clear(); }

    /// <summary>
    /// <c>MemoryMap::HasCollisionRayWithTypes(from, to, types)</c> 0x0068176E: the types are folded into a
    /// flag mask (<c>EContentTypeToFlag</c> per entry whose bool is set) and handed to the quad tree's ray
    /// walk. Here the same question is asked of the polygons: does the segment touch a region of one of
    /// those types. <paramref name="blocks"/> defaults to
    /// <see cref="MemoryMapTypes.BlocksTheRobot"/>, the mask the face behaviour passes.
    /// </summary>
    public bool HasCollisionRayWithTypes(Vec2 from, Vec2 to, Func<MemoryMapContentType, bool>? blocks = null)
    {
        blocks ??= MemoryMapTypes.BlocksTheRobot;
        lock (_gate)
            foreach (var r in _regions)
                if (blocks(r.Type) && SegmentTouchesPolygon(from, to, r.Polygon)) return true;
        return false;
    }

    /// <summary>
    /// Refreshes the object content from the world model, which is what the engine does one object at a
    /// time through <c>MapComponent::AddObservableObject</c> and <c>RemoveObservableObject</c> as the world
    /// changes: every located object of a family the map takes is in, at its bounding quad, and everything
    /// else is out. Regions that came from <see cref="Insert"/> (a cliff, say) are left alone.
    /// </summary>
    public void SyncFromWorld(BlockWorld world, uint timestamp = 0)
    {
        var keep = new List<MemoryMapRegion>();
        lock (_gate)
        {
            foreach (var r in _regions) if (r.ObjectId is null) keep.Add(r);
            foreach (var o in world.LocatedObjects)
            {
                var type = MemoryMapTypes.ContentTypeForFamily(o.Family, adding: true);
                if (type == MemoryMapContentType.Unknown) continue;
                keep.Add(new MemoryMapRegion(type, BoundingQuad(o), o.ObjectId, timestamp));
            }
            _regions.Clear();
            _regions.AddRange(keep);
        }
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
