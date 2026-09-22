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
public sealed class MemoryMap
{
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
        lock (_gate) return _regions.Any(r => r.Type == type);
    }

    public void Clear() { lock (_gate) _regions.Clear(); }

    /// <summary>How far the robot must move before it writes itself into the map again: 8 mm on any axis
    /// (0x41000000) or 0.349066 rad, 20 degrees (0x3EB2B8C2), the thresholds
    /// <c>MapComponent::UpdateRobotPose</c> 0x0067E23A gives <c>Pose3d::IsSameAs</c>.</summary>
    public const double RobotPoseMoveMm = 8.0, RobotPoseTurnRad = 0.349066;

    private Pose3d? _lastRobotPose;

    /// <summary>
    /// <c>MapComponent::UpdateRobotPose</c> 0x0067E224: the ground the robot itself has been over.
    ///
    /// It does nothing while the robot is within 8 mm and 20 degrees of where it last wrote itself
    /// (<c>IsSameAs</c> at 0x0067E27C). Otherwise it takes the <c>ProxObstacle</c> markerless size halved
    /// - (5, 5, 25) from the (10, 10, 50) at <c>GetSizeByType</c> - builds the square those half-extents
    /// describe, puts it at the robot's pose (0x0067E2F6..0x0067E32C), and inserts it: as
    /// <see cref="MemoryMapContentType.ClearOfCliff"/> when nothing is reporting a cliff (the type byte 2
    /// written at 0x0067E3C2) and as <see cref="MemoryMapContentType.Cliff"/> when something is, with the
    /// robot's own X axis as the cliff data's direction (0x0067E342..0x0067E358).
    ///
    /// Returns the region it laid down, or null when there was none to lay: the robot had not moved far
    /// enough, or the ground it is on is recorded already.
    /// </summary>
    public MemoryMapRegion? UpdateRobotPose(Pose3d robotPose, bool cliffDetected = false, uint timestamp = 0)
    {
        if (_lastRobotPose is { } last && last.IsSameAs(robotPose, RobotPoseMoveMm, RobotPoseTurnRad)) return null;
        _lastRobotPose = robotPose;
        // the half-extents are (5, 5, 25); the square they describe is 10 by 10
        var size = MarkerlessObject.SizeByType(ObjectType.ProxObstacle)!.Value;
        var quad = Rectangle(new Pose3d(Mat3.AboutZ(robotPose.AngleAroundZ), robotPose.Translation with { Z = 0 }),
                             size.X, size.Y);
        var type = cliffDetected ? MemoryMapContentType.Cliff : MemoryMapContentType.ClearOfCliff;
        lock (_gate)
        {
            // The engine's quad tree absorbs a repeat: inserting the same content where that content
            // already is changes no node. Here the regions are a list, so the same thing is said by not
            // adding a square whose centre is already inside one of its own type - otherwise a long drive
            // would leave thousands of overlapping squares behind and slow every query down.
            var centre = new Vec2(robotPose.Translation.X, robotPose.Translation.Y);
            foreach (var r in _regions)
                if (r.Type == type && r.ObjectId is null && SegmentTouchesPolygon(centre, centre, r.Polygon))
                    return null;
            var region = new MemoryMapRegion(type, quad, null, timestamp);
            _regions.Add(region);
            return region;
        }
    }

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
    public const double OverheadEdgeRunDirectionCos = 0.766;

    /// <summary>
    /// The squared length a run must exceed before anything is written for it: the literal 6.00001 the
    /// comparison at 0x0067FA14 uses - the noise length, taken against a squared length as the engine
    /// takes it.
    /// </summary>
    public const double OverheadEdgeMinRunLengthSq = 6.00001;

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

    /// <summary>
    /// <c>MapComponent::AddVisionOverheadEdges</c> 0x0067F814. Every point of the frame is put in world
    /// coordinates through the robot's pose at the frame's timestamp, and then:
    ///
    /// <list type="bullet">
    /// <item>the segment from the robot to the point is intersected with the segment through the ground
    /// quad's second and fourth corners - its near edge, the two corners
    /// <c>GetGroundQuad</c> 0x004F7774 puts second and fourth (<c>kmSegment2WithSegmentIntersection</c>
    /// at 0x0067FC54). Where they meet, the ray is asked about in two halves rather than one;</item>
    /// <item>the point is dropped when the map already has something in the way: everything but clear
    /// ground stops the half between the robot and the near edge (the mask at 0x00C8768B), and the five
    /// obstacle types stop the rest of it (0x00C876A1), so neither a cliff nor an edge already recorded
    /// out in the ROI stops a point being used;</item>
    /// <item>consecutive points accumulate into a run while the run keeps its direction to within forty
    /// degrees (0x0067FD40). A turn sharper than that, a point the map blocked, or the end of the chain
    /// closes the run off (0x0067FE6C..0x0067FE92).</item>
    /// </list>
    ///
    /// A closed run whose squared length passes <see cref="OverheadEdgeMinRunLengthSq"/> becomes the
    /// triangle between the robot and the run's two ends - the engine stores it as a quad with the robot
    /// twice over (0x0067FA54) - and, if the run came from a border chain, also a segment. Then:
    ///
    /// <list type="bullet">
    /// <item>each triangle goes in as <see cref="MemoryMapContentType.ClearOfObstacle"/>, and its shape
    /// is decided by the two fifteen-millimetre constants, compared as squared lengths against 225
    /// (0x0067FF5C and 0x0067FFA8): the full quad when both sides are longer, the triangle when the
    /// close side is shorter - which it always is, being the robot twice - and a two-point line when the
    /// far side is shorter too;</item>
    /// <item>each border segment goes in as a two-point
    /// <see cref="MemoryMapContentType.InterestingEdge"/> polygon (the type byte 9 at 0x006802AC);</item>
    /// <item>and finally, if any border segment was inserted, <see cref="FillBorder"/> runs over the
    /// map: an interesting edge that touches an obstacle - or an edge already written off - becomes
    /// NotInterestingEdge (<c>FillBorderInternal(9, mask, 10, lastImageTimestamp)</c> at 0x006803AA,
    /// whose mask is the table at 0x00C87675).</item>
    /// </list>
    ///
    /// Returns the distance from the robot to the closest border point, which is what the engine hands
    /// the whiteboard (0x006803E8), or null when the frame carried no border points.
    /// </summary>
    public double? AddVisionOverheadEdges(OverheadEdgeFrame frame, Pose3d robotPose)
    {
        if (!frame.GroundPlaneValid || frame.Chains.Count == 0) return null;

        Vec2 ToWorld(Vec2 p)
        {
            var w = robotPose.Apply(new Vec3(p.X, p.Y, 0));
            return new Vec2(w.X, w.Y);
        }

        var robot = new Vec2(robotPose.Translation.X, robotPose.Translation.Y);
        var edgeA = ToWorld(frame.GroundQuad[1]);
        var edgeB = ToWorld(frame.GroundQuad[3]);

        var runs = new List<(Vec2 Start, Vec2 End, bool Border)>();
        double? closestSq = null;

        foreach (var chain in frame.Chains)
        {
            Vec2 runStart = default, prev = default, dir = default;
            bool hasRun = false, hasDir = false;

            for (int i = 0; i < chain.Points.Count; i++)
            {
                var p = ToWorld(chain.Points[i].Ground);
                var from = robot;
                bool blocked = false;
                if (SegmentIntersection(robot, p, edgeA, edgeB) is { } hit)
                {
                    // the ray reaches the point through the near edge of what the camera can see:
                    // ask about the robot's side of it with the wider mask, and about the rest from there
                    blocked = HasCollisionRayWithTypes(robot, hit, OverheadEdgeBlocksAll);
                    from = hit;
                }
                blocked = blocked || HasCollisionRayWithTypes(from, p, OverheadEdgeBlocksObstacles);

                if (chain.IsBorder && !blocked)
                {
                    double dsq = (p - robot).LengthSq;
                    if (closestSq is null || dsq < closestSq.Value) closestSq = dsq;
                }

                if (blocked)
                {
                    if (hasRun) Close(runs, runStart, prev, chain.IsBorder);
                    hasRun = false; hasDir = false;
                    continue;
                }

                if (!hasRun) { runStart = p; prev = p; hasRun = true; hasDir = false; continue; }

                var d = p - prev;
                double len = d.Length;
                if (len > 0) d = d * (1 / len);
                if (hasDir && d.X * dir.X + d.Y * dir.Y < OverheadEdgeRunDirectionCos)
                {
                    Close(runs, runStart, prev, chain.IsBorder);
                    runStart = prev;
                }
                dir = d; hasDir = true; prev = p;
                if (i == chain.Points.Count - 1) { Close(runs, runStart, p, chain.IsBorder); hasRun = false; }
            }
        }

        bool anyBorder = false;
        foreach (var run in runs)
        {
            InsertClearRun(robot, run.Start, run.End, frame.Timestamp);
            if (!run.Border) continue;
            anyBorder = true;
            Insert(new[] { run.Start, run.End }, MemoryMapContentType.InterestingEdge, null, frame.Timestamp);
        }

        if (anyBorder)
            FillBorder(MemoryMapContentType.InterestingEdge, OverheadEdgeBorderMask,
                       MemoryMapContentType.NotInterestingEdge, frame.Timestamp);

        return closestSq is null ? null : Math.Sqrt(closestSq.Value);

        static void Close(List<(Vec2, Vec2, bool)> into, Vec2 start, Vec2 end, bool border)
        {
            if ((end - start).LengthSq <= OverheadEdgeMinRunLengthSq) return;
            into.Add((start, end, border));
        }
    }

    /// <summary>
    /// One run's clear area, in the three shapes the engine chooses between at
    /// 0x0067FF48..0x0068016E. The quad it works on is (runStart, robot, runEnd, robot), so its close
    /// side - the robot against itself - is always zero and never reaches the fifteen millimetres that
    /// would keep the quad whole; what is left is the triangle when the run is longer than fifteen and
    /// the line from the robot to the run's midpoint when it is not.
    /// </summary>
    private void InsertClearRun(Vec2 robot, Vec2 start, Vec2 end, uint timestamp)
    {
        double farSq = (start - end).LengthSq;
        if (farSq <= OverheadEdgeFarMaxLenForLineMm * OverheadEdgeFarMaxLenForLineMm)
        {
            var mid = new Vec2((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            Insert(new[] { robot, mid }, MemoryMapContentType.ClearOfObstacle, null, timestamp);
            return;
        }
        Insert(new[] { robot, start, end }, MemoryMapContentType.ClearOfObstacle, null, timestamp);
    }

    /// <summary>
    /// <c>MemoryMap::FillBorderInternal(type, mask, borderType, timestamp)</c> (the vtable's +0x4C slot),
    /// which is <c>QuadTreeProcessor::FillBorder</c> 0x00689FAC over the tree:
    /// <c>RefreshBorderCombination(type, mask)</c> finds the nodes of <paramref name="type"/> that
    /// neighbour a node of one of the masked types, and the data of <paramref name="write"/> is written
    /// at each of them. Here the same question is asked of the polygons - a region of that type touching
    /// one of the masked ones, to within the map's own content precision - and the region takes the new
    /// type. Returns how many changed.
    /// </summary>
    public int FillBorder(MemoryMapContentType type, Func<MemoryMapContentType, bool> mask,
                          MemoryMapContentType write, uint timestamp)
    {
        int changed = 0;
        lock (_gate)
            for (int i = 0; i < _regions.Count; i++)
            {
                var r = _regions[i];
                if (r.Type != type) continue;
                bool borders = false;
                foreach (var q in _regions)
                {
                    if (ReferenceEquals(q, r) || !mask(q.Type)) continue;
                    if (PolygonsTouch(r.Polygon, q.Polygon, ContentPrecisionMm)) { borders = true; break; }
                }
                if (!borders) continue;
                _regions[i] = r with { Type = write, Timestamp = timestamp };
                changed++;
            }
        return changed;
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
