using System.Text.Json;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// One action of the motion-primitive set (<c>cozmo_mprim.json</c> "actions"): its index, name, extra cost
/// factor and whether it drives backwards.
/// </summary>
public sealed record PrimitiveAction(int Index, string Name, double ExtraCostFactor, bool Reverse);

/// <summary>
/// One sampled pose of a primitive (<c>Anki::Planning::IntermediatePosition</c>, 0x14 bytes): the pose in
/// mm/radians relative to the start cell, the step distance at +8 and the soft-collision reciprocal at +0x10
/// (M13-003, M13-004).
/// </summary>
public readonly record struct IntermediatePose(double X, double Y, double Theta, double Reciprocal);

/// <summary>
/// One motion primitive (<c>Anki::Planning::MotionPrimitive</c>): from a start heading index, the action moves
/// the robot by (EndX, EndY) cells to heading EndTheta, through the listed intermediate poses (mm and radians,
/// relative to the start cell). <see cref="Cost"/> is the primitive's traversal cost, computed by
/// <c>MotionPrimitive::Create</c> (M13-004).
/// </summary>
public sealed record MotionPrimitive(int ActionIndex, int StartTheta, int EndX, int EndY, int EndTheta,
                                     IReadOnlyList<IntermediatePose> Intermediate, double LengthMm, double Cost)
{
    /// <summary>
    /// The straight run before the arc, in millimetres, from the primitive's own <c>straight_length_mm</c>.
    /// Negative for the backwards primitive; zero for an in-place turn.
    /// </summary>
    public double StraightLengthMm { get; init; }

    /// <summary>
    /// The arc the primitive drives after that straight, exactly as <c>cozmo_mprim.json</c> gives it, in
    /// the start heading's frame: centre, radius, start angle and sweep. Null for the straights and the
    /// in-place turns, which carry no <c>arc</c>.
    /// </summary>
    public (double CenterX, double CenterY, double Radius, double StartRad, double SweepRad)? Arc { get; init; }

    /// <summary>+1 or -1 for the two in-place turns, from <c>turn_in_place_direction</c>; null otherwise.</summary>
    public double? TurnInPlaceDirection { get; init; }
}

/// <summary>
/// The engine's motion-primitive set, ASSET <c>config/engine/cozmo_mprim.json</c>: a 10 mm lattice with 16
/// headings (0, atan(1/2), 45°, atan(2), 90°, ... the lattice angles, not uniform steps) and nine actions per
/// heading: short straight (1 cell, cost factor 1.0001 so long straights win ties), long straight (5 cells),
/// slight left/right (5 cells forward, 1 sideways, ±1 heading step), hard left/right (3 forward, 1 sideways,
/// ±2 steps), in-place left/right (±1 step, factor 2.0) and a backwards short straight (factor 1.2, marked
/// reverse). <c>xythetaEnvironment::ParseMotionPrims</c> reads exactly these keys.
/// </summary>
public sealed class MotionPrimitiveSet
{
    public double ResolutionMm { get; private init; }
    /// <summary>
    /// The JSON <c>num_angles</c> (env+8). The production <c>xythetaEnvironment::Init(Json const&amp;)</c>
    /// 0x00851F9E does not overwrite it; the hard-coded-16 override is on the uncalled
    /// <c>Init(char const*)</c> 0x008528A8 (M13-019). The shipped asset's value is 16.
    /// </summary>
    // fidelity: M13-019
    public int NumAngles { get; private init; }
    public IReadOnlyList<double> Angles { get; private init; } = Array.Empty<double>();
    public IReadOnlyList<PrimitiveAction> Actions { get; private init; } = Array.Empty<PrimitiveAction>();
    /// <summary>Primitives by start heading index.</summary>
    public IReadOnlyList<IReadOnlyList<MotionPrimitive>> ByAngle { get; private init; } = Array.Empty<IReadOnlyList<MotionPrimitive>>();

/// <summary>
/// The <c>RobotActionParams</c> defaults the engine constructs at 0x00851EDE (ctor 0x0084EDE6): half wheel
/// base 24.0 mm, max velocity 60.0 mm/s, max reverse velocity 25.0 mm/s. <c>RobotActionParams::Import</c>
/// has no callers and the asset has no such keys, so these stand (M13-004).
/// </summary>
public const double HalfWheelBaseMm = 24.0;
public const double MaxVelocityMmps = 60.0;
public const double MaxReverseVelocityMmps = 25.0;

public static string ObbRelativePath => Path.Combine("assets", "cozmo_resources", "config", "engine", "cozmo_mprim.json");

/// <summary>Loads the set from an OBB root; null when the file is not there or the parse fails.</summary>
public static MotionPrimitiveSet? FromObb(string obbRoot)
{
    var p = Path.Combine(obbRoot, ObbRelativePath);
    if (!File.Exists(p)) return null;
    try { return Parse(File.ReadAllText(p)); }
    catch (InvalidDataException) { return null; }   // ReadMotionPrimitives returns 0 -> no planner
}

public static MotionPrimitiveSet Parse(string json)
{
    using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
    var root = doc.RootElement;
    double res = root.GetProperty("resolution_mm").GetDouble();
    int n = root.GetProperty("num_angles").GetInt32();
    var angles = root.GetProperty("angle_definitions").EnumerateArray().Select(a => a.GetDouble()).ToArray();
    var actions = root.GetProperty("actions").EnumerateArray()
        .Select(a => new PrimitiveAction(a.GetProperty("index").GetInt32(), a.GetProperty("name").GetString() ?? "",
                                         a.GetProperty("extra_cost_factor").GetDouble(),
                                         a.TryGetProperty("reverse_action", out var r) && r.GetBoolean()))
        .OrderBy(a => a.Index).ToArray();
    // fidelity: M13-001
    // ParseMotionPrims 0x00852014 aborts and returns 0 when either count differs from num_angles
    // (0x0085223C, 0x0085225A); the messages are the engine's.
    if (angles.Length != n)
        throw new InvalidDataException($"ERROR: numAngles is {n}, but we read {angles.Length} angle definitions");
    var angleEntries = root.GetProperty("angles").EnumerateArray().ToArray();
    if (angleEntries.Length != n)
        throw new InvalidDataException("error: could not find key 'angles' in motion primitives");

    var byAngle = new List<IReadOnlyList<MotionPrimitive>>();
    int start = 0;
    foreach (var ang in angleEntries)
    {
        var prims = new List<MotionPrimitive>();
        foreach (var p in ang.GetProperty("prims").EnumerateArray())
        {
            // a per-primitive extra_cost_factor is rejected (0x00853FFC, M13-004)
            if (p.TryGetProperty("extra_cost_factor", out _))
                throw new InvalidDataException("ERROR: individual primitives shouldn't have cost factors. Old file format?");
            int ai = p.GetProperty("action_index").GetInt32();
            var action = actions[ai];
            // State::Import 0x0084F8A4: "x"/"y" are shorts in grid cells, "theta" an unsigned byte heading
            // index (0x0084F920/0x0084F940/0x0084F962, M13-004).
            var end = p.GetProperty("end_pose");
            int ex = (int)end.GetProperty("x").GetDouble();
            int ey = (int)end.GetProperty("y").GetDouble();
            int eth = end.GetProperty("theta").GetInt32();
            var raw = p.GetProperty("intermediate_poses").EnumerateArray()
                .Select(q => (X: q.GetProperty("x_mm").GetDouble(), Y: q.GetProperty("y_mm").GetDouble(), Th: q.GetProperty("theta_rads").GetDouble())).ToArray();
            // IntermediatePosition+0x10: 1/(halfWheelBase*|dtheta|/maxVelocity + dist), the per-pose
            // soft-collision reciprocal (M13-003 / Appendix G R2-1..R2-3). dist is the Euclidean distance to
            // the previous intermediate pose, dtheta = wrap(cur.theta_rads - prev.theta_rads); the first pose
            // gets 0.0.
            var inter = new IntermediatePose[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                double recip = 0.0;
                if (i > 0)
                {
                    double dist = Math.Sqrt(Sq(raw[i].X - raw[i - 1].X) + Sq(raw[i].Y - raw[i - 1].Y));
                    double dth = Math.Abs(StraightLinePlanner.Wrap(raw[i].Th - raw[i - 1].Th));
                    double denom = HalfWheelBaseMm * dth / MaxVelocityMmps + dist;
                    recip = denom > 1e-9 ? 1.0 / denom : 0.0;
                }
                inter[i] = new IntermediatePose(raw[i].X, raw[i].Y, raw[i].Th, recip);
            }
            double len = 0;
            for (int i = 1; i < raw.Length; i++) len += Math.Sqrt(Sq(raw[i].X - raw[i - 1].X) + Sq(raw[i].Y - raw[i - 1].Y));
            (double CenterX, double CenterY, double Radius, double StartRad, double SweepRad)? arc = null;
            if (p.TryGetProperty("arc", out var ja))
                arc = (ja.GetProperty("centerPt_x_mm").GetDouble(), ja.GetProperty("centerPt_y_mm").GetDouble(),
                       ja.GetProperty("radius_mm").GetDouble(), ja.GetProperty("startRad").GetDouble(),
                       ja.GetProperty("sweepRad").GetDouble());
            double straight = p.TryGetProperty("straight_length_mm", out var sl) ? sl.GetDouble() : 0;
            double? turnDir = p.TryGetProperty("turn_in_place_direction", out var td) ? td.GetDouble() : null;
            // fidelity: M13-004
            // MotionPrimitive::Create 0x00853DD0: base = d8*|straight_length_mm|; if the primitive has an
            // arc, add d8*|sweepRad|*(|radius_mm| + halfWheelBase) and do NOT add the turn term (the arc
            // branch jumps straight to the extra-cost-factor multiply at 0x0085425E); else if it has
            // turn_in_place_direction, add d8*halfWheelBase*|dtheta|. d8 = 1/maxVelocity forward,
            // 1/maxReverseVelocity reverse (0x00854046..0x00854378).
            double d8 = action.Reverse ? 1.0 / MaxReverseVelocityMmps : 1.0 / MaxVelocityMmps;
            double baseCost = d8 * Math.Abs(straight);
            if (arc is { } arcValue)
                baseCost += d8 * Math.Abs(arcValue.SweepRad) * (Math.Abs(arcValue.Radius) + HalfWheelBaseMm);
            else if (turnDir is not null)
            {
                double dtheta = Math.Abs(StraightLinePlanner.Wrap(angles[eth] - angles[start]));
                baseCost += d8 * HalfWheelBaseMm * dtheta;
            }
            double cost = baseCost * action.ExtraCostFactor;
            if (baseCost < 1e-6 || cost < 1e-6)
            {
                // MotionPrimitive::Create returns 0 and ParseMotionPrims logs "Failed to import motion primitive".
                Console.Error.WriteLine($"ERROR: base action cost is {baseCost} for action {ai} '{action.Name}'");
                continue;
            }
            prims.Add(new MotionPrimitive(ai, start, ex, ey, eth, inter, len, cost)
            {
                StraightLengthMm = straight,
                Arc = arc,
                TurnInPlaceDirection = turnDir,
            });
        }
        while (byAngle.Count <= start) byAngle.Add(Array.Empty<MotionPrimitive>());
        byAngle[start] = prims;
        start++;
    }
    // M13-001/M13-004: the angle's index in the array is its starting heading; M13-019: NumAngles is the
    // JSON value (the production Init(Json const&) 0x00851F9E does not overwrite env+8).
    // fidelity: M13-001, M13-004, M13-019
    var set = new MotionPrimitiveSet { ResolutionMm = res, NumAngles = n, Angles = angles, Actions = actions, ByAngle = byAngle };
    set.Reflected = BuildReflected(byAngle, n);
    return set;
}

/// <summary>
/// <c>PopulateReverseMotionPrims</c> 0x008544C0: the reflected primitive set the heuristic expansion walks
/// (env+0x20). For each forward primitive it negates the end-pose x and y (16-bit), sets the end-pose theta
/// byte to the forward primitive's <b>start</b> heading index, stores the result in the bucket of the forward
/// <b>end</b> theta, and copies the cost (+4), the <c>Path</c> (+0x2C), the intermediate-pose vector (+0x10)
/// and the cached bbox (+0x1C..+0x28) unchanged (Appendix G R1-2..R1-4).
/// </summary>
// fidelity: M13-018
public IReadOnlyList<IReadOnlyList<MotionPrimitive>> Reflected { get; private set; } = Array.Empty<IReadOnlyList<MotionPrimitive>>();

private static IReadOnlyList<IReadOnlyList<MotionPrimitive>> BuildReflected(IReadOnlyList<IReadOnlyList<MotionPrimitive>> byAngle, int n)
{
    var buckets = new List<MotionPrimitive>[n];
    for (int i = 0; i < n; i++) buckets[i] = new List<MotionPrimitive>();
    for (int start = 0; start < byAngle.Count; start++)
        foreach (var p in byAngle[start])
            buckets[p.EndTheta].Add(p with { EndX = -p.EndX, EndY = -p.EndY, EndTheta = start });
    return buckets.Select(b => (IReadOnlyList<MotionPrimitive>)b).ToArray();
}

    /// <summary>The heading index nearest an angle.</summary>
    public int ThetaIndex(double angleRad)
    {
        int best = 0; double bestD = double.MaxValue;
        for (int i = 0; i < Angles.Count; i++)
        {
            double d = Math.Abs(StraightLinePlanner.Wrap(angleRad - Angles[i]));
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    private static double Sq(double v) => v * v;
}

/// <summary>A lattice state: cell x, cell y and heading index.</summary>
public readonly record struct LatticeState(int X, int Y, int Theta);

/// <summary>
/// The engine's <c>Anki::Planning::xythetaEnvironment</c>: the primitive set plus the obstacles the plan
/// must avoid.
///
/// <b>One obstacle list, expanded per heading.</b> The environment holds a vector of
/// <c>FastPolygon</c> per theta bucket (this+0x44), and every accessor reads that same list:
/// <c>IsInCollision(State)</c> 0x008515BC converts and tail-calls <c>IsInCollision(State_c)</c>
/// 0x008515F8, which indexes it by the state's theta; <c>IsInSoftCollision</c> 0x00851708 and
/// <c>GetCollisionPenalty</c> 0x008517B0 walk the same bucket, the latter returning the float at
/// entry+0x44. (An earlier reading here had the hard and soft rings as separate sets. They are not:
/// this+0x38, which <c>IsInCollision(State)</c> also reads, is the per-theta table of headings it
/// passes on as the angle.)
///
/// <b>What the import puts in it.</b> <c>LatticePlannerImpl::ImportBlockworldObstaclesIfNeeded</c>
/// 0x004FD4B8 logs its own numbers - "robot padding %f, obstacle padding %f, didBlocksChange %d" - and
/// they are <see cref="RobotPaddingMm"/> 7 and <see cref="ObstaclePaddingMm"/> 6, or
/// <see cref="TightRobotPaddingMm"/> 2 and <see cref="TightObstaclePaddingMm"/> 1 selected by the
/// <em>function's own first bool argument</em> (r4 = r1 at 0x004FD4D2; cmp r4,#0 at 0x004FD4E8; itt ne
/// at 0x004FD4F6). Its callers pass an immediate: <c>ComputePathHelper</c> and <c>PreloadObstacles</c>
/// pass 0, <c>StartPlanning</c> passes 1 (0x004FEC38). Neither is a penalty: the penalty is the constant
/// 0.1 the import passes to every obstacle (0x3DCCCCCD at 0x004FE0CE).
///
/// Each object's quad is radially expanded by the obstacle padding
/// (<c>ConvexPolygon::RadialExpand</c> at 0x004FDF9E), the robot's own bounding quad is taken at each
/// heading with the robot padding (<c>Robot::GetBoundingQuadXY(pose, padding)</c> at 0x004FE0A4), and
/// the two are handed to <c>xythetaEnvironment::AddObstacleWithExpansion(obstacle, robot, theta,
/// 0.1f)</c> 0x00855528, which calls <c>ExpandCSpace</c> and stores the result in that theta's bucket.
/// So the stored polygon is the configuration-space obstacle for that heading, and a plain point test
/// on the robot's origin is all the search ever does.
///
/// The robot's canonical footprint is the function-local static quad built in
/// <c>Robot::GetBoundingQuadXY</c> at 0x00514DB0: x from -55.9 to 22.1, y from -27.1 to 27.1. The
/// origin sits 22.1 mm behind the front and 55.9 mm ahead of the back, which is why a circle of any
/// radius was never going to stand in for it.
/// </summary>
public sealed class LatticeEnvironment
{
    /// <summary>The robot bounding quad's front edge, 22.1 mm ahead of the origin (0x41B0CCCC).</summary>
    public const double RobotFrontMm = 22.1;
    /// <summary>Its back edge, 55.9 mm behind the origin (0xC25F999A).</summary>
    public const double RobotBackMm = -55.9;
    /// <summary>Half its width, 27.1 mm each side (0x41D8CCCD and 0xC1D8CCCD).</summary>
    public const double RobotHalfWidthMm = 27.1;

    /// <summary>7 mm, the padding the import gives the robot's quad.</summary>
    public const double RobotPaddingMm = 7.0;
    /// <summary>6 mm, the radial expansion it gives each obstacle.</summary>
    public const double ObstaclePaddingMm = 6.0;
    /// <summary>2 mm, the robot padding in the planner's tight mode.</summary>
    public const double TightRobotPaddingMm = 2.0;
    /// <summary>1 mm, the obstacle padding in that mode.</summary>
    public const double TightObstaclePaddingMm = 1.0;
    /// <summary>0.1, the penalty the import gives every obstacle it adds.</summary>
    public const double ObstaclePenalty = 0.1;

    /// <summary>One obstacle: the polygon it was built from, and its C-space polygon per heading.</summary>
    public sealed record Obstacle(Vec2[] Polygon, Vec2[][] ByTheta, double Penalty, string Name);

    private readonly List<Obstacle> _obstacles = new();

    public LatticeEnvironment(MotionPrimitiveSet prims) => Primitives = prims;

    public MotionPrimitiveSet Primitives { get; }
    public int ObstacleCount => _obstacles.Count;
    public IReadOnlyList<Obstacle> Obstacles => _obstacles;

    /// <summary>Whether to use the tight padding pair, as the planner's own flag selects it.</summary>
    public bool TightPadding { get; set; }

    public double RobotPadding => TightPadding ? TightRobotPaddingMm : RobotPaddingMm;
    public double ObstaclePadding => TightPadding ? TightObstaclePaddingMm : ObstaclePaddingMm;

    public void ClearObstacles() => _obstacles.Clear();

    /// <summary>
    /// The robot's bounding quad at a heading, padded: <c>Robot::GetBoundingQuadXY(pose, padding)</c>
    /// with the canonical quad above.
    /// </summary>
    public static Vec2[] RobotQuad(double headingRad, double paddingMm)
    {
        double f = RobotFrontMm + paddingMm, b = RobotBackMm - paddingMm, w = RobotHalfWidthMm + paddingMm;
        var local = new[] { new Vec2(f, -w), new Vec2(f, w), new Vec2(b, w), new Vec2(b, -w) };
        double c = Math.Cos(headingRad), sn = Math.Sin(headingRad);
        return local.Select(v => new Vec2(v.X * c - v.Y * sn, v.X * sn + v.Y * c)).ToArray();
    }

    /// <summary>
    /// <c>xythetaEnvironment::ExpandCSpace(obstacle, robot)</c>: the set of robot origins that put the
    /// robot in the obstacle, which is the Minkowski difference - the convex hull of every obstacle
    /// vertex less every robot vertex.
    /// </summary>
    public static Vec2[] ExpandCSpace(Vec2[] obstacle, Vec2[] robot)
    {
        var pts = new List<Vec2>(obstacle.Length * robot.Length);
        foreach (var o in obstacle)
            foreach (var r in robot)
                pts.Add(new Vec2(o.X - r.X, o.Y - r.Y));
        return ConvexHull(pts);
    }

    /// <summary>Andrew's monotone chain, counter-clockwise.</summary>
    public static Vec2[] ConvexHull(List<Vec2> pts)
    {
        var p = pts.OrderBy(v => v.X).ThenBy(v => v.Y).ToList();
        if (p.Count < 3) return p.ToArray();
        static double Cross(Vec2 o, Vec2 a, Vec2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var hull = new List<Vec2>();
        foreach (var v in p)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], v) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(v);
        }
        int lower = hull.Count + 1;
        for (int i = p.Count - 2; i >= 0; i--)
        {
            var v = p[i];
            while (hull.Count >= lower && Cross(hull[^2], hull[^1], v) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(v);
        }
        hull.RemoveAt(hull.Count - 1);
        return hull.ToArray();
    }

    /// <summary>
    /// Adds one obstacle: the polygon is radially expanded by the obstacle padding and then expanded
    /// into configuration space once per heading, as the import does.
    /// </summary>
    public void AddObstacle(Vec2[] polygon, string name = "", double? penalty = null)
    {
        var padded = RadialExpand(polygon, ObstaclePadding);
        var byTheta = new Vec2[Primitives.NumAngles][];
        for (int t = 0; t < Primitives.NumAngles; t++)
            byTheta[t] = ExpandCSpace(padded, RobotQuad(Primitives.Angles[t], RobotPadding));
        _obstacles.Add(new Obstacle(padded, byTheta, penalty ?? ObstaclePenalty, name));
    }

    /// <summary>A rectangle at a pose, as an obstacle.</summary>
    public void AddRectangleObstacle(Pose3d pose, double lengthX, double widthY, string name = "")
    {
        var c = new[] { new Vec3(-lengthX / 2, -widthY / 2, 0), new Vec3(lengthX / 2, -widthY / 2, 0), new Vec3(lengthX / 2, widthY / 2, 0), new Vec3(-lengthX / 2, widthY / 2, 0) }
            .Select(p => { var w = pose.Apply(p); return new Vec2(w.X, w.Y); }).ToArray();
        AddObstacle(c, name);
    }

    /// <summary>
    /// <c>LatticePlannerImpl::ImportBlockworldObstaclesIfNeeded</c>: every located object except the one being
    /// carried becomes an obstacle from its bounding quad (<c>GetBoundingQuadXY</c>). The charger counts too.
    /// <paramref name="tightPadding"/> is the function's own first bool argument: true selects 2/1, false 7/6.
    /// <c>StartPlanning</c> calls it with a hard-coded true (0x004FEC38).
    /// </summary>
    // fidelity: M13-003
    public void ImportBlockWorldObstacles(BlockWorld world, uint? carriedObjectId, bool tightPadding, IEnumerable<uint>? ignore = null)
    {
        TightPadding = tightPadding;
        var skip = new HashSet<uint>(ignore ?? Array.Empty<uint>());
        if (carriedObjectId is { } c) skip.Add(c);
        ClearObstacles();
        foreach (var o in world.LocatedObjects)
        {
            if (skip.Contains(o.ObjectId)) continue;
            var size = CubeGeometry.SizeOf(o.Type);
            var flat = new Pose3d(Mat3.AboutZ(o.Pose.AngleAroundZ), o.Pose.Translation with { Z = 0 });
            if (o.Type == Cozmo.Protocol.ObjectType.Charger_Basic)
                flat = flat.Compose(new Pose3d(Mat3.Identity, new Vec3(size.X / 2, 0, 0)));      // the charger's origin is its front lip
            AddRectangleObstacle(flat, size.X, size.Y, $"object {o.ObjectId}");
        }
    }

    /// <summary>
    /// <c>xythetaEnvironment::IsInCollision(State_c)</c> 0x008515F8: hard collision only - a containing
    /// polygon whose penalty is <b>&gt;= 1000.0</b> (0x008516D0/0x008516DC).
    /// </summary>
    // fidelity: M13-003
    public bool IsInCollision(double xMm, double yMm, int theta)
    {
        var p = new Vec2(xMm, yMm);
        foreach (var o in _obstacles) if (o.Penalty >= 1000.0 && Inside(o.ByTheta[theta], p)) return true;
        return false;
    }

    /// <summary><c>xythetaEnvironment::IsInSoftCollision</c> 0x00851708: any containing polygon.</summary>
    // fidelity: M13-003
    public bool IsInSoftCollision(double xMm, double yMm, int theta)
    {
        var p = new Vec2(xMm, yMm);
        foreach (var o in _obstacles) if (Inside(o.ByTheta[theta], p)) return true;
        return false;
    }

    /// <summary><c>GetCollisionPenalty</c> 0x008517B0: the first containing polygon's penalty, or 0.0.</summary>
    // fidelity: M13-003
    public double PenaltyAt(double xMm, double yMm, int theta)
    {
        var p = new Vec2(xMm, yMm);
        foreach (var o in _obstacles) if (Inside(o.ByTheta[theta], p)) return o.Penalty;
        return 0;
    }

    /// <summary>
    /// <c>SuccessorIterator::Next</c> 0x0085110C's per-primitive collision test. A non-turning primitive
    /// (<c>end_pose.theta == +1</c> heading index) tests <b>every</b> intermediate pose in the end-pose
    /// bucket; a turning primitive walks the poses last to first, each in its own bucket. A containing
    /// polygon with penalty &lt; 1000.0 is soft and adds <c>base + penalty*reciprocal</c>; a penalty
    /// &gt;= 1000.0 rejects the primitive. <c>base</c> is 0.0 forward, 1000.0 reverse. Returns false on a
    /// hard collision.
    /// </summary>
    // fidelity: M13-003
    public bool TryGetSoftCost(LatticeState from, MotionPrimitive prim, out double softCost)
    {
        softCost = 0;
        if (_obstacles.Count == 0) return true;
        double res = Primitives.ResolutionMm;
        double x0 = from.X * res, y0 = from.Y * res;
        double baseCost = Primitives.Actions[prim.ActionIndex].Reverse ? 1000.0 : 0.0;
        var inter = prim.Intermediate;
        if (prim.EndTheta == prim.StartTheta)
        {
            for (int i = 0; i < inter.Count; i++)
                if (!AddSoftOrHard(x0 + inter[i].X, y0 + inter[i].Y, prim.EndTheta, baseCost, inter[i].Reciprocal, ref softCost)) return false;
        }
        else
        {
            for (int i = inter.Count - 1; i >= 0; i--)
                if (!AddSoftOrHard(x0 + inter[i].X, y0 + inter[i].Y, Primitives.ThetaIndex(inter[i].Theta), baseCost, inter[i].Reciprocal, ref softCost)) return false;
        }
        return true;
    }

    private bool AddSoftOrHard(double x, double y, int bucket, double baseCost, double reciprocal, ref double soft)
    {
        var p = new Vec2(x, y);
        foreach (var o in _obstacles)
        {
            if (!Inside(o.ByTheta[bucket], p)) continue;
            if (o.Penalty >= 1000.0) return false;
            soft += baseCost + o.Penalty * reciprocal;
        }
        return true;
    }

    /// <summary>The successors of a state: every primitive from its heading that is not in hard collision.</summary>
    public IEnumerable<(LatticeState Next, MotionPrimitive Prim, double Cost)> GetSuccessors(LatticeState s)
        => GetSuccessors(s, Primitives.ByAngle);

    /// <summary>Successors over an explicit primitive set (the reflected set for the heuristic).</summary>
    public IEnumerable<(LatticeState Next, MotionPrimitive Prim, double Cost)> GetSuccessors(LatticeState s, IReadOnlyList<IReadOnlyList<MotionPrimitive>> set)
    {
        foreach (var p in set[s.Theta])
        {
            if (!TryGetSoftCost(s, p, out var soft)) continue;
            yield return (new LatticeState(s.X + p.EndX, s.Y + p.EndY, p.EndTheta), p, p.Cost + soft);
        }
    }

    public LatticeState ToState(Pose3d pose) =>
        new((int)Math.Round(pose.Translation.X / Primitives.ResolutionMm), (int)Math.Round(pose.Translation.Y / Primitives.ResolutionMm), Primitives.ThetaIndex(pose.AngleAroundZ));

    public Pose3d ToPose(LatticeState s) =>
        new(Mat3.AboutZ(Primitives.Angles[s.Theta]), new Vec3(s.X * Primitives.ResolutionMm, s.Y * Primitives.ResolutionMm, 0));

    /// <summary>
    /// <c>ConvexPolygon::RadialExpand</c> 0x004FDF9E (body 0x00841580): each vertex moves to
    /// <c>v + d*(v-c)/|v-c|</c> with <c>c</c> the centroid computed once; no bisector, no cos, no clamp.
    /// A negative distance only warns (0x00841590..0x008415A8).
    /// </summary>
    // fidelity: M13-003
    public static Vec2[] RadialExpand(Vec2[] poly, double byMm)
    {
        if (byMm < 0) { Console.Error.WriteLine("called expand with a negative distance."); return poly; }
        double cx = poly.Average(p => p.X), cy = poly.Average(p => p.Y);
        var outp = new Vec2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            double dx = poly[i].X - cx, dy = poly[i].Y - cy;
            double l = Math.Sqrt(dx * dx + dy * dy);
            outp[i] = l > 0 ? new Vec2(poly[i].X + byMm * dx / l, poly[i].Y + byMm * dy / l) : poly[i];
        }
        return outp;
    }

    /// <summary>Point-in-convex-polygon by consistent cross-product sign.</summary>
    public static bool Inside(Vec2[] poly, Vec2 p)
    {
        bool? positive = null;
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Length];
            double cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            if (Math.Abs(cross) < 1e-9) continue;
            bool pos = cross > 0;
            if (positive is null) positive = pos; else if (positive != pos) return false;
        }
        return true;
    }
}

/// <summary>A plan: the start state and the primitives taken from it (<c>Anki::Planning::xythetaPlan</c>).</summary>
public sealed record LatticePlan(LatticeState Start, IReadOnlyList<MotionPrimitive> Actions, double Cost, int Expansions)
{
    public IEnumerable<LatticeState> States()
    {
        var s = Start; yield return s;
        foreach (var a in Actions) { s = new LatticeState(s.X + a.EndX, s.Y + a.EndY, a.EndTheta); yield return s; }
    }
}

/// <summary>
/// The engine's <c>Anki::Planning::xythetaPlanner</c> / <c>xythetaPlannerImpl</c> (<c>ComputePath</c>,
/// <c>ExpandState</c>, <c>InitializeHeuristic</c>, <c>CheckGoal</c>): a min-<c>f</c> search over the lattice
/// from the start state to any of the goal states. Several goals are supported, as the engine's
/// <c>GoalsAreValid</c> implies (a cube's four pre-action poses).
///
/// <b>The heuristic (M13-018).</b> <c>heur_internal</c> 0x0085A7B8 returns
/// <c>min_i( heurMap[i] + EuclideanDistance(state, goal_i)/maxVelocity )</c>, memoized in the map at
/// planner+0xAC by <c>heur</c> 0x0085A780. <c>heurMap</c> comes from <c>InitializeHeuristic</c> 0x008598DC:
/// for each goal, 0.0 when the goal is not in soft collision, else <c>ExpandCollisionStatesFromGoal</c>
/// (a Dijkstra over the <b>reflected</b> primitive set through soft-collision states, returning the
/// accumulated cost when free space is reached); a goal whose cost exceeds 1000.0 is dropped. The open list
/// is a min-<c>f</c> priority queue with <c>f = g + h</c> (0x0084E4E8, <c>ExpandState</c> 0x0085A34C).
///
/// The expansion cap is the engine's: <c>Replan</c> is called with 0x01C9C380 = 30,000,000 (M13-018).
/// </summary>
// fidelity: M13-018
public class LatticePlanner
{
    /// <summary>
    /// 30,000,000 = 0x01C9C380: the maximum number of state expansions, the argument <c>DoPlanning</c>
    /// passes to <c>Replan</c> (0x005000F2/0x005000FA; <c>ComputePath</c> warns "exceeded max expansions
    /// of %u, stopping" at 0x00858A96 and returns 0).
    /// </summary>
    // fidelity: M13-018
    public const int MaxExpansions = 30_000_000;

    public LatticePlanner(LatticeEnvironment env) => Env = env;
    public LatticeEnvironment Env { get; }

    /// <summary>
    /// impl+0x108: the pre-plan wait in ms. The ctor 0x004FCCF6 sets 0 and
    /// <c>LatticePlanner::SetArtificialPlannerDelay_ms</c> 0x004FFFEC sets it (M13-018).
    /// </summary>
    // fidelity: M13-018
    public int ArtificialPlannerDelayMs { get; set; }

    /// <summary>impl+0xA1: the bool <c>StartPlanning</c> stores (0x004FEB7E): true = force a replan.</summary>
    // fidelity: M13-018
    public bool ReplanFlag { get; private set; }

    /// <summary>
    /// impl+0xF2: the run/continue flag, not an abort flag. 1 = keep planning (ctor 0x004FCCCA and
    /// <c>StartPlanning</c> 0x004FF310), 0 = stop (<c>StopPlanning</c> 0x004FD1C8). <c>DoPlanning</c> reads it
    /// during the sleep and passes it to <c>Replan</c> (M13-018).
    /// </summary>
    // fidelity: M13-018
    public bool RunFlag { get; private set; } = true;

    /// <summary><c>LatticePlannerImpl::StopPlanning</c> 0x004FD1BA: clear the run flag.</summary>
    // fidelity: M13-018
    public void StopPlanning() => RunFlag = false;

    /// <summary>The engine's <c>DoPlanning</c> result: 0 failure, 3 empty plan, 2 success.</summary>
    // fidelity: M13-018
    public enum PlanningResult { Failure = 0, EmptyPlan = 3, Success = 2 }

    /// <summary>The result of the last <see cref="PlanTo"/>: the engine's <c>DoPlanning</c> codes.</summary>
    // fidelity: M13-018
    public PlanningResult LastPlanningResult { get; private set; } = PlanningResult.Failure;

    private readonly Dictionary<LatticeState, double> _heurMemo = new();
    private double[] _heurMap = Array.Empty<double>();
    private List<LatticeState> _heurGoals = new();

    /// <summary>
    /// <c>LatticePlannerImpl::StartPlanning</c> 0x004FEB44: store the bool at impl+0xA1, set the run flag to
    /// 1 (0x004FF310) and import the world's obstacles. <paramref name="forceReplan"/> true selects the
    /// 7.0/6.0 padding pair (bool 0 at 0x004FEEFE); false would first select 2.0/1.0 (bool 1 at 0x004FEC38)
    /// and then reuse a safe old plan through <c>FindClosestPlanSegmentToPose</c> 0x00856310 and
    /// <c>PlanIsSafe</c> 0x00850B78 (return 2 at 0x004FF42E). That false branch is <b>unreachable engine
    /// code</b>: all three recovered <c>ComputePath</c> callers pass true (<c>ComputePathHelper</c> 0x004FD330,
    /// <c>FaceAndApproachPlanner</c> 0x004F355C, <c>MinimalAnglePlanner</c> 0x00503BC2; Appendix G 4d), so
    /// only the true/live path is built here.
    /// </summary>
    // fidelity: M13-018
    public void StartPlanning(BlockWorld world, uint? carriedObjectId, bool forceReplan, IEnumerable<uint>? ignore = null)
    {
        ReplanFlag = forceReplan;
        RunFlag = true;
        Env.ImportBlockWorldObstacles(world, carriedObjectId, tightPadding: !forceReplan, ignore);
    }

    /// <summary>
    /// <c>LatticePlannerImpl::DoPlanning</c> 0x00500090: sleep in <c>min(remaining, 10) ms</c> chunks up to
    /// <see cref="ArtificialPlannerDelayMs"/> (impl+0x108) checking the run flag each iteration, then plan.
    /// Returns 0 when the plan failed, 3 when it succeeded with an empty segment list and 2 on success.
    /// </summary>
    // fidelity: M13-018
    public PlanningResult DoPlanning(LatticeState start, IReadOnlyList<LatticeState> goals, out LatticePlan? plan)
    {
        if (ArtificialPlannerDelayMs > 0)
        {
            int slept = 0;
            while (slept < ArtificialPlannerDelayMs)
            {
                if (!RunFlag) { plan = null; return PlanningResult.Failure; }
                int chunk = Math.Min(ArtificialPlannerDelayMs - slept, 10);
                Thread.Sleep(chunk);
                slept += chunk;
            }
        }
        plan = ComputePath(start, goals);
        if (plan is null) return PlanningResult.Failure;
        if (plan.Actions.Count == 0) return PlanningResult.EmptyPlan;
        return PlanningResult.Success;
    }

    public bool StartIsValid(LatticeState s) { var p = Env.ToPose(s); return !Env.IsInCollision(p.Translation.X, p.Translation.Y, s.Theta); }
    public bool GoalsAreValid(IEnumerable<LatticeState> goals) => goals.Any(StartIsValid);

    /// <summary>
    /// <c>xythetaPlannerImpl::ComputePath</c> 0x008586A0: min-<c>f</c> A* with the engine's heuristic. The
    /// goals are filtered by <see cref="InitializeHeuristic"/>; when none survive it returns null.
    /// </summary>
    // fidelity: M13-018
    public LatticePlan? ComputePath(LatticeState start, IReadOnlyList<LatticeState> goals)
    {
        if (goals.Count == 0) return null;
        if (!InitializeHeuristic(goals)) return null;
        var goalSet = new HashSet<LatticeState>(_heurGoals);
        if (goalSet.Contains(start)) return new LatticePlan(start, Array.Empty<MotionPrimitive>(), 0, 0);
        var open = new PriorityQueue<LatticeState, double>();
        var g = new Dictionary<LatticeState, double> { [start] = 0 };
        var parent = new Dictionary<LatticeState, (LatticeState From, MotionPrimitive Prim)>();
        var closed = new HashSet<LatticeState>();
        open.Enqueue(start, Heur(start));
        int expansions = 0;
        while (open.TryDequeue(out var s, out _))
        {
            if (!RunFlag) return null;                     // 0x00858886..0x00858890
            if (!closed.Add(s)) continue;
            if (goalSet.Contains(s))
            {
                var actions = new List<MotionPrimitive>();
                var cur = s;
                while (parent.TryGetValue(cur, out var p)) { actions.Add(p.Prim); cur = p.From; }
                actions.Reverse();
                return new LatticePlan(start, actions, g[s], expansions);
            }
            if (++expansions > MaxExpansions) return null; // 0x008588C8
            double gs = g[s];
            foreach (var (next, prim, cost) in Env.GetSuccessors(s))
            {
                if (closed.Contains(next)) continue;
                double ng = gs + cost;
                if (g.TryGetValue(next, out var old) && old <= ng) continue;
                g[next] = ng; parent[next] = (s, prim);
                open.Enqueue(next, ng + Heur(next));       // f = g + h, 0x0085A4AC
            }
        }
        return null;
    }

    /// <summary>
    /// <c>InitializeHeuristic</c> 0x008598DC: clear the memo, then for each goal 0.0 when it is not in soft
    /// collision, else <see cref="ExpandCollisionStatesFromGoal"/>; drop a goal whose cost exceeds 1000.0.
    /// Returns false when no goal survives.
    /// </summary>
    // fidelity: M13-018
    private bool InitializeHeuristic(IReadOnlyList<LatticeState> goals)
    {
        _heurMemo.Clear();
        var kept = new List<LatticeState>();
        var costs = new List<double>();
        foreach (var goal in goals)
        {
            var pose = Env.ToPose(goal);
            double cost = Env.IsInSoftCollision(pose.Translation.X, pose.Translation.Y, goal.Theta)
                ? ExpandCollisionStatesFromGoal(goal)
                : 0.0;
            if (cost > 1000.0) continue;                    // 0x008599E0 vcmpe/ble
            kept.Add(goal); costs.Add(cost);
        }
        _heurGoals = kept;
        _heurMap = costs.ToArray();
        return kept.Count > 0;
    }

    /// <summary>
    /// <c>heur_internal</c> 0x0085A7B8 memoized by <c>heur</c> 0x0085A780:
    /// <c>min_i( heurMap[i] + EuclideanDistance(state, goal_i)/maxVelocity )</c>.
    /// </summary>
    // fidelity: M13-018
    private double Heur(LatticeState s)
    {
        if (_heurMemo.TryGetValue(s, out var cached)) return cached;
        double best = double.MaxValue;
        double res = Env.Primitives.ResolutionMm;
        for (int i = 0; i < _heurGoals.Count; i++)
        {
            var goal = _heurGoals[i];
            double d = Math.Sqrt(Sq(s.X - goal.X) + Sq(s.Y - goal.Y)) * res;
            best = Math.Min(best, _heurMap[i] + d / MotionPrimitiveSet.MaxVelocityMmps);
        }
        _heurMemo[s] = best;
        return best;
    }

    /// <summary>
    /// <c>ExpandCollisionStatesFromGoal</c> 0x0085998E: Dijkstra from the goal over the <b>reflected</b>
    /// primitive set, through states in soft collision, edge weight = parent cost + primitive cost +
    /// soft-collision penalty. Returns the accumulated cost of the first popped state that is not in soft
    /// collision; an empty open list or a 0 run flag returns 0.0; after the 1000-expansion cap it warns and
    /// returns the last popped cost (M13-018).
    ///
    /// It seeds and updates the <b>same</b> map <see cref="Heur"/> memoizes in (planner+0xAC): the goal at 0
    /// and each expanded state's best cost (0x00859CB6..0x00859CF6). The map persists across goals, so
    /// <c>heur</c> returns those values directly (0x0085A78E/0x0085A798).
    /// </summary>
    // fidelity: M13-018
    private double ExpandCollisionStatesFromGoal(LatticeState goal)
    {
        var open = new PriorityQueue<LatticeState, double>();
        var visited = new HashSet<LatticeState>();
        _heurMemo[goal] = 0;
        open.Enqueue(goal, 0);
        int expansions = 0;
        double lastPopped = 0;
        while (open.TryDequeue(out var s, out var cost))
        {
            if (!RunFlag) return 0.0;                      // 0x00859C5E..0x00859C68
            lastPopped = cost;
            if (!visited.Add(s)) continue;
            var pose = Env.ToPose(s);
            if (!Env.IsInSoftCollision(pose.Translation.X, pose.Translation.Y, s.Theta)) return cost;
            if (++expansions > 1000)                       // 0x00859ECC cmp #0x3E8
            {
                Console.Error.WriteLine("exceeded max allowed expansions of 1000");
                return lastPopped;
            }
            foreach (var (next, prim, edge) in Env.GetSuccessors(s, Env.Primitives.Reflected))
            {
                if (visited.Contains(next)) continue;
                double ng = cost + edge;                   // parent + primitive + soft penalty
                if (_heurMemo.TryGetValue(next, out var old) && old <= ng) continue;
                _heurMemo[next] = ng;
                open.Enqueue(next, ng);
            }
        }
        return 0.0;                                        // 0x00859F4A
    }

    private static double Sq(double v) => v * v;

    /// <summary>
    /// <c>MotionPrimitive::AddSegmentsToPath</c> / <c>LatticePlannerImpl::GetCompletePath</c>: the plan as robot
    /// path segments. Straight primitives in a row merge into one line; an in-place turn is a point turn to the
    /// new lattice heading; a turning primitive is its own <c>straight_length_mm</c> followed by its own
    /// <c>arc</c>, both of which <c>cozmo_mprim.json</c> states outright - the slight turns are a 7.639 mm
    /// run into a 94.721 mm radius through 0.4636 rad, the hard ones 5.858 mm into 34.142 mm through
    /// 0.7854 - so the arc is rotated into the world rather than reconstructed from the end cell. A final
    /// point turn to the goal's exact heading is appended when the lattice heading differs from it.
    /// </summary>
    public IReadOnlyList<PathSegment> ToPath(LatticePlan plan, Pose3d? exactGoal, PathMotionProfile profile)
    {
        var p = profile;
        var path = new List<PathSegment>();
        double res = Env.Primitives.ResolutionMm;
        var states = plan.States().ToList();
        int i = 0;
        while (i < plan.Actions.Count)
        {
            var a = plan.Actions[i]; var s0 = states[i];
            var action = Env.Primitives.Actions[a.ActionIndex];
            bool straight = a.EndTheta == a.StartTheta && (a.EndX != 0 || a.EndY != 0);
            if (straight)
            {
                // merge consecutive straights of the same direction
                int j = i;
                while (j + 1 < plan.Actions.Count)
                {
                    var b = plan.Actions[j + 1]; var bAct = Env.Primitives.Actions[b.ActionIndex];
                    if (b.EndTheta == b.StartTheta && (b.EndX != 0 || b.EndY != 0) && bAct.Reverse == action.Reverse) j++; else break;
                }
                var s1 = states[j + 1];
                float speed = action.Reverse ? -p.ReverseSpeedMmps : p.SpeedMmps;
                path.Add(new PathSegment.Line(s0.X * res, s0.Y * res, s1.X * res, s1.Y * res, speed, p.AccelMmps2, p.DecelMmps2));
                i = j + 1;
                continue;
            }
            if (a.EndX == 0 && a.EndY == 0)
            {
                // in-place turn(s): merge a run into one point turn to the final heading
                int j = i;
                while (j + 1 < plan.Actions.Count && plan.Actions[j + 1].EndX == 0 && plan.Actions[j + 1].EndY == 0) j++;
                var s1 = states[j + 1];
                path.Add(new PathSegment.PointTurn(s0.X * res, s0.Y * res, Env.Primitives.Angles[s1.Theta], StraightLinePlanner.PointTurnToleranceRad,
                                                   p.PointTurnSpeedRadPerSec, p.PointTurnAccelRadPerSec2, p.PointTurnDecelRadPerSec2, true));
                i = j + 1;
                continue;
            }
            // A turning primitive: a straight run and then an arc, both of which the primitive file
            // states outright. There is nothing to derive - cozmo_mprim.json gives straight_length_mm
            // and an arc block with centre, radius, start angle and sweep, in the start heading's frame -
            // so they are rotated into the world and used as they stand.
            double th0 = Env.Primitives.Angles[a.StartTheta];
            double x0 = s0.X * res, y0 = s0.Y * res;

            // straight_length_mm is a distance along the heading; the arc block is already expressed for
            // this starting angle - at heading 90 the same slight-left primitive has its centre at
            // (-94.721, 7.639) with startRad 0 - so the centre only needs translating, never rotating.
            double line = a.StraightLengthMm;
            double xl = x0 + Math.Cos(th0) * line, yl = y0 + Math.Sin(th0) * line;
            if (Math.Abs(line) > 0.5) path.Add(new PathSegment.Line(x0, y0, xl, yl, p.SpeedMmps, p.AccelMmps2, p.DecelMmps2));

            if (a.Arc is { } arc)
                path.Add(new PathSegment.Arc(x0 + arc.CenterX, y0 + arc.CenterY, arc.Radius,
                                             arc.StartRad, arc.SweepRad, p.SpeedMmps, p.AccelMmps2, p.DecelMmps2));
            i++;
        }
        if (exactGoal is { } goal)
        {
            var end = states[^1];
            double latticeHeading = Env.Primitives.Angles[end.Theta];
            if (Math.Abs(StraightLinePlanner.Wrap(goal.AngleAroundZ - latticeHeading)) > StraightLinePlanner.PointTurnToleranceRad)
                path.Add(new PathSegment.PointTurn(end.X * res, end.Y * res, goal.AngleAroundZ, StraightLinePlanner.PointTurnToleranceRad,
                                                   p.PointTurnSpeedRadPerSec, p.PointTurnAccelRadPerSec2, p.PointTurnDecelRadPerSec2, true));
        }
        return path;
    }

    /// <summary>
    /// Plans from a pose to one of several goal poses; null when no plan is found or the goals are all in
    /// collision. <c>virtual</c> so the M13-005 failure-path test can force the no-plan result without
    /// needing a hard obstacle the shipped import never produces.
    /// </summary>
    // fidelity: M13-005, M13-018
    public virtual (LatticePlan Plan, IReadOnlyList<PathSegment> Path, Pose3d Goal)? PlanTo(Pose3d start, IReadOnlyList<Pose3d> goals, PathMotionProfile profile)
    {
        var s = Env.ToState(start);
        var gs = goals.Select(Env.ToState).ToList();
        var valid = gs.Where(StartIsValid).ToList();
        if (valid.Count == 0) { LastPlanningResult = PlanningResult.Failure; return null; }
        LastPlanningResult = DoPlanning(s, valid, out var plan);
        if (plan is null) return null;
        var endState = plan.States().Last();
        int gi = gs.IndexOf(endState);
        var goal = gi >= 0 ? goals[gi] : goals[0];
        return (plan, ToPath(plan, goal, profile), goal);
    }
}
