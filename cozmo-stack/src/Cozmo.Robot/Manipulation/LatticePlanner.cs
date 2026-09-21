using System.Text.Json;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// One action of the motion-primitive set (<c>cozmo_mprim.json</c> "actions"): its index, name, extra cost
/// factor and whether it drives backwards.
/// </summary>
public sealed record PrimitiveAction(int Index, string Name, double ExtraCostFactor, bool Reverse);

/// <summary>
/// One motion primitive (<c>Anki::Planning::MotionPrimitive</c>): from a start heading index, the action moves
/// the robot by (EndX, EndY) cells to heading EndTheta, through the listed intermediate poses (mm and radians,
/// relative to the start cell). <see cref="Cost"/> is the primitive's traversal cost in mm-equivalents.
/// </summary>
public sealed record MotionPrimitive(int ActionIndex, int StartTheta, int EndX, int EndY, int EndTheta,
                                     IReadOnlyList<(double X, double Y, double Theta)> Intermediate, double LengthMm, double Cost)
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
    public int NumAngles { get; private init; }
    public IReadOnlyList<double> Angles { get; private init; } = Array.Empty<double>();
    public IReadOnlyList<PrimitiveAction> Actions { get; private init; } = Array.Empty<PrimitiveAction>();
    /// <summary>Primitives by start heading index.</summary>
    public IReadOnlyList<IReadOnlyList<MotionPrimitive>> ByAngle { get; private init; } = Array.Empty<IReadOnlyList<MotionPrimitive>>();

    public static string ObbRelativePath => Path.Combine("assets", "cozmo_resources", "config", "engine", "cozmo_mprim.json");

    /// <summary>Loads the set from an OBB root; null when the file is not there.</summary>
    public static MotionPrimitiveSet? FromObb(string obbRoot)
    {
        var p = Path.Combine(obbRoot, ObbRelativePath);
        return File.Exists(p) ? Parse(File.ReadAllText(p)) : null;
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
        var byAngle = new List<IReadOnlyList<MotionPrimitive>>();
        foreach (var ang in root.GetProperty("angles").EnumerateArray())
        {
            int start = ang.GetProperty("starting_angle").GetInt32();
            var prims = new List<MotionPrimitive>();
            foreach (var p in ang.GetProperty("prims").EnumerateArray())
            {
                int ai = p.GetProperty("action_index").GetInt32();
                var end = p.GetProperty("end_pose");
                var inter = p.GetProperty("intermediate_poses").EnumerateArray()
                    .Select(q => (q.GetProperty("x_mm").GetDouble(), q.GetProperty("y_mm").GetDouble(), q.GetProperty("theta_rads").GetDouble())).ToArray();
                double len = 0;
                for (int i = 1; i < inter.Length; i++) len += Math.Sqrt(Sq(inter[i].Item1 - inter[i - 1].Item1) + Sq(inter[i].Item2 - inter[i - 1].Item2));
                // an in-place turn has no length: its cost is one cell times the action's factor (INFERRED; the
                // engine's exact turn cost was not read)
                double cost = Math.Max(len, res) * actions[ai].ExtraCostFactor;
                (double, double, double, double, double)? arc = null;
                if (p.TryGetProperty("arc", out var ja))
                    arc = (ja.GetProperty("centerPt_x_mm").GetDouble(), ja.GetProperty("centerPt_y_mm").GetDouble(),
                           ja.GetProperty("radius_mm").GetDouble(), ja.GetProperty("startRad").GetDouble(),
                           ja.GetProperty("sweepRad").GetDouble());
                prims.Add(new MotionPrimitive(ai, start, (int)Math.Round(end.GetProperty("x").GetDouble()), (int)Math.Round(end.GetProperty("y").GetDouble()),
                                              end.GetProperty("theta").GetInt32(), inter, len, cost)
                {
                    StraightLengthMm = p.TryGetProperty("straight_length_mm", out var sl) ? sl.GetDouble() : 0,
                    Arc = arc,
                    TurnInPlaceDirection = p.TryGetProperty("turn_in_place_direction", out var td) ? td.GetDouble() : null,
                });
            }
            while (byAngle.Count <= start) byAngle.Add(Array.Empty<MotionPrimitive>());
            byAngle[start] = prims;
        }
        return new MotionPrimitiveSet { ResolutionMm = res, NumAngles = n, Angles = angles, Actions = actions, ByAngle = byAngle };
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
/// <see cref="TightRobotPaddingMm"/> 2 and <see cref="TightObstaclePaddingMm"/> 1 when the planner's
/// tight flag is set (0x004FD4F6). Neither is a penalty: the penalty is the constant 0.1 the import
/// passes to every obstacle (0x3DCCCCCD at 0x004FE0CE).
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
    /// </summary>
    public void ImportBlockWorldObstacles(BlockWorld world, uint? carriedObjectId, IEnumerable<uint>? ignore = null)
    {
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

    /// <summary>Whether the robot's origin at this point and heading is inside any obstacle.</summary>
    public bool IsInCollision(double xMm, double yMm, int theta)
    {
        var p = new Vec2(xMm, yMm);
        foreach (var o in _obstacles) if (Inside(o.ByTheta[theta], p)) return true;
        return false;
    }

    /// <summary>The penalty of the first obstacle containing the point, or zero.</summary>
    public double PenaltyAt(double xMm, double yMm, int theta)
    {
        var p = new Vec2(xMm, yMm);
        foreach (var o in _obstacles) if (Inside(o.ByTheta[theta], p)) return o.Penalty;
        return 0;
    }

    /// <summary>
    /// The penalty for driving a primitive from a lattice state: null when it collides, else the sum of
    /// the penalties it picked up. Each intermediate pose is tested in its own heading's bucket, which
    /// is what indexing the obstacle list by the state's theta amounts to.
    /// </summary>
    public double? GetCollisionPenalty(LatticeState from, MotionPrimitive prim)
    {
        if (_obstacles.Count == 0) return 0;
        double res = Primitives.ResolutionMm;
        double x0 = from.X * res, y0 = from.Y * res;
        double penalty = 0;
        var inter = prim.Intermediate;
        // every fourth intermediate pose plus the last: the primitives are sampled every 0.5 mm
        for (int i = 0; i < inter.Count; i += 4)
        {
            var (x, y, th) = inter[i];
            int t = Primitives.ThetaIndex(th);
            if (IsInCollision(x0 + x, y0 + y, t)) return null;
            penalty += PenaltyAt(x0 + x, y0 + y, t);
        }
        var last = inter[^1];
        if (IsInCollision(x0 + last.X, y0 + last.Y, Primitives.ThetaIndex(last.Theta))) return null;
        return penalty;
    }

    /// <summary>The successors of a state: every primitive from its heading that does not collide.</summary>
    public IEnumerable<(LatticeState Next, MotionPrimitive Prim, double Cost)> GetSuccessors(LatticeState s)
    {
        foreach (var p in Primitives.ByAngle[s.Theta])
        {
            var pen = GetCollisionPenalty(s, p);
            if (pen is null) continue;
            yield return (new LatticeState(s.X + p.EndX, s.Y + p.EndY, p.EndTheta), p, p.Cost + pen.Value);
        }
    }

    public LatticeState ToState(Pose3d pose) =>
        new((int)Math.Round(pose.Translation.X / Primitives.ResolutionMm), (int)Math.Round(pose.Translation.Y / Primitives.ResolutionMm), Primitives.ThetaIndex(pose.AngleAroundZ));

    public Pose3d ToPose(LatticeState s) =>
        new(Mat3.AboutZ(Primitives.Angles[s.Theta]), new Vec3(s.X * Primitives.ResolutionMm, s.Y * Primitives.ResolutionMm, 0));

    /// <summary><c>ConvexPolygon::RadialExpand</c>: each vertex pushed out from the centroid so every edge moves out by the distance.</summary>
    public static Vec2[] RadialExpand(Vec2[] poly, double byMm)
    {
        if (byMm <= 0) return poly;
        double cx = poly.Average(p => p.X), cy = poly.Average(p => p.Y);
        var outp = new Vec2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            // move the vertex so that both adjacent edges move out by byMm: along the bisector by byMm / cos(half-angle)
            var prev = poly[(i + poly.Length - 1) % poly.Length]; var next = poly[(i + 1) % poly.Length];
            var d1 = Norm(new Vec2(poly[i].X - prev.X, poly[i].Y - prev.Y)); var d2 = Norm(new Vec2(next.X - poly[i].X, next.Y - poly[i].Y));
            var n1 = new Vec2(d1.Y, -d1.X); var n2 = new Vec2(d2.Y, -d2.X);           // outward normals for a counter-clockwise polygon
            if ((poly[i].X - cx) * n1.X + (poly[i].Y - cy) * n1.Y < 0) { n1 = new Vec2(-n1.X, -n1.Y); n2 = new Vec2(-n2.X, -n2.Y); }
            var bis = Norm(new Vec2(n1.X + n2.X, n1.Y + n2.Y));
            double cosHalf = Math.Max(0.3, bis.X * n1.X + bis.Y * n1.Y);
            outp[i] = new Vec2(poly[i].X + bis.X * byMm / cosHalf, poly[i].Y + bis.Y * byMm / cosHalf);
        }
        return outp;
    }

    private static Vec2 Norm(Vec2 v) { double l = Math.Sqrt(v.X * v.X + v.Y * v.Y); return l > 0 ? new Vec2(v.X / l, v.Y / l) : v; }

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
/// <c>ExpandState</c>, <c>InitializeHeuristic</c>, <c>CheckGoal</c>): an A* search over the lattice from the
/// start state to any of the goal states, with the Euclidean distance to the nearest goal as the heuristic
/// (INFERRED: the engine precomputes a heuristic table; distance is the admissible choice for mm-costed
/// primitives). Several goals are supported, as the engine's <c>GoalsAreValid</c> implies (a cube's four
/// pre-action poses). The search gives up after <see cref="MaxExpansions"/> expansions (LOCAL bound).
/// </summary>
public sealed class LatticePlanner
{
    public const int MaxExpansions = 60000;

    public LatticePlanner(LatticeEnvironment env) => Env = env;
    public LatticeEnvironment Env { get; }

    public bool StartIsValid(LatticeState s) { var p = Env.ToPose(s); return !Env.IsInCollision(p.Translation.X, p.Translation.Y, s.Theta); }
    public bool GoalsAreValid(IEnumerable<LatticeState> goals) => goals.Any(StartIsValid);

    public LatticePlan? ComputePath(LatticeState start, IReadOnlyList<LatticeState> goals)
    {
        if (goals.Count == 0) return null;
        var goalSet = new HashSet<LatticeState>(goals);
        if (goalSet.Contains(start)) return new LatticePlan(start, Array.Empty<MotionPrimitive>(), 0, 0);
        double res = Env.Primitives.ResolutionMm;
        double H(LatticeState s)
        {
            double best = double.MaxValue;
            foreach (var g in goals) best = Math.Min(best, Math.Sqrt((s.X - g.X) * (s.X - g.X) + (s.Y - g.Y) * (s.Y - g.Y)) * res);
            return best;
        }
        var open = new PriorityQueue<LatticeState, double>();
        var g = new Dictionary<LatticeState, double> { [start] = 0 };
        var parent = new Dictionary<LatticeState, (LatticeState From, MotionPrimitive Prim)>();
        var closed = new HashSet<LatticeState>();
        open.Enqueue(start, H(start));
        int expansions = 0;
        while (open.TryDequeue(out var s, out _))
        {
            if (!closed.Add(s)) continue;
            if (goalSet.Contains(s))
            {
                var actions = new List<MotionPrimitive>();
                var cur = s;
                while (parent.TryGetValue(cur, out var p)) { actions.Add(p.Prim); cur = p.From; }
                actions.Reverse();
                return new LatticePlan(start, actions, g[s], expansions);
            }
            if (++expansions > MaxExpansions) return null;
            double gs = g[s];
            foreach (var (next, prim, cost) in Env.GetSuccessors(s))
            {
                if (closed.Contains(next)) continue;
                double ng = gs + cost;
                if (g.TryGetValue(next, out var old) && old <= ng) continue;
                g[next] = ng; parent[next] = (s, prim);
                open.Enqueue(next, ng + H(next));
            }
        }
        return null;
    }

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

    /// <summary>Plans from a pose to one of several goal poses; null when no plan is found or the goals are all in collision.</summary>
    public (LatticePlan Plan, IReadOnlyList<PathSegment> Path, Pose3d Goal)? PlanTo(Pose3d start, IReadOnlyList<Pose3d> goals, PathMotionProfile profile)
    {
        var s = Env.ToState(start);
        var gs = goals.Select(Env.ToState).ToList();
        var valid = gs.Where(StartIsValid).ToList();
        if (valid.Count == 0) return null;
        var plan = ComputePath(s, valid);
        if (plan is null) return null;
        var endState = plan.States().Last();
        int gi = gs.IndexOf(endState);
        var goal = gi >= 0 ? goals[gi] : goals[0];
        return (plan, ToPath(plan, goal, profile), goal);
    }
}
