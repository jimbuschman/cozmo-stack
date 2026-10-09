using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// R-FIX batch 2 (M13): tests whose expected values come from the binary's instructions, from the shipped asset, or from independent exact geometry. Each test names the record and the
/// citation it checks. A float32 emulation here is written from the cited instruction listing in the test itself, not read back from the implementation.
/// </summary>
public class RFixBatch2Tests
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);

    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (File.Exists(Path.Combine(r, MotionPrimitiveSet.ObbRelativePath))) return r;
            d = d.Parent;
        }
        return null;
    }

    private static MotionPrimitiveSet? Prims() { var obb = ObbRoot(); return obb is null ? null : MotionPrimitiveSet.FromObb(obb); }
    private static Pose3d At(double x, double y, double heading) => new(Mat3.AboutZ(heading), new Vec3(x, y, 0));

    // ------------------------------------------------------------------ M13-003: the obstacle polygon pipeline

    /// <summary>
    /// M13-003, ConvexPolygon::RadialExpand 0x00841580..0x00841654. For a 44 mm square about (150, 0) every corner is 22*sqrt(2) from the centroid; the engine moves each by
    /// <c>by / hypotf(d)</c> times d, so a 6 mm expansion lands each corner on (150 +- (22 + 6/sqrt2), +-(22 + 6/sqrt2)) = +-26.2426407. Exact geometry is the oracle, and the float words are
    /// the emulation of the listing: centroid by <c>inv * p</c> accumulation (0x0050238A), division first, then multiply, then add.
    /// </summary>
    [Fact]
    public void M13_003_RadialExpandMovesEveryCornerOutBySixAlongItsOwnRay()
    {
        var square = new[] { new P2f(128, -22), new P2f(128, 22), new P2f(172, 22), new P2f(172, -22) };
        var expanded = PolygonF.RadialExpand(square, 6.0f);
        double ex = 22 + 6 / Math.Sqrt(2);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(150 + Math.Sign(square[i].X - 150) * ex, expanded[i].X, 4);
            Assert.Equal(Math.Sign(square[i].Y) * ex, expanded[i].Y, 4);
        }
        // the float words, from an independent numpy float32 emulation of the listing (centroid by inv*p accumulation, division first, then multiply, then add; scratch generator gen_prims.py)
        var words = new[] { 0x42F783C5u, 0xC1D1F0EEu, 0x42F783C5u, 0x41D1F0EEu, 0x43303E1Eu, 0x41D1F0EEu, 0x43303E1Eu, 0xC1D1F0EEu };
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(words[2 * i], Bits(expanded[i].X));
            Assert.Equal(words[2 * i + 1], Bits(expanded[i].Y));
        }
        // a negative distance only warns and leaves the polygon alone (0x00841590..0x008415A8)
        var warned = new List<string>();
        var same = PolygonF.RadialExpand(square, -1.0f, warned.Add);
        Assert.Equal(square, same);
        Assert.Single(warned);
    }

    /// <summary>
    /// M13-003, ConvexPolygon constructor 0x008412F0 and Polygon::ImportQuad2d 0x0050096C: a quad is imported clockwise (corners s0, s3, s2, s1 of the ascending-angle order), a
    /// counter-clockwise polygon is reversed by the constructor (the left turn gives fmod(a1 - a0 + 2pi, 2pi) below pi, 0x0084139C..0x008413A8) and a clockwise one is left as it is.
    /// </summary>
    [Fact]
    public void M13_003_ConvexPolygonLeavesPolygonsClockwise()
    {
        var ccw = new[] { new P2f(0, 0), new P2f(10, 0), new P2f(10, 10), new P2f(0, 10) };
        var cw = new[] { new P2f(0, 0), new P2f(0, 10), new P2f(10, 10), new P2f(10, 0) };
        Assert.Equal(cw, PolygonF.ConvexPolygon(cw));                                          // a clockwise polygon is unchanged
        Assert.Equal(Enumerable.Reverse(ccw).ToArray(), PolygonF.ConvexPolygon(ccw));                    // the counter-clockwise one is reversed into clockwise order
        // ImportQuad2d of the scrambled corners of the 10 mm square about the origin: ascending atan2f order is (-,-), (+,-), (+,+), (-,+) = s0..s3 and the polygon is s0, s3, s2, s1
        var quad = new[] { new P2f(5, 5), new P2f(-5, -5), new P2f(5, -5), new P2f(-5, 5) };
        var polygon = PolygonF.ImportQuad2d(quad);
        Assert.Equal(new[] { new P2f(-5, -5), new P2f(-5, 5), new P2f(5, 5), new P2f(5, -5) }, polygon);
    }

    /// <summary>
    /// M13-003, ExpandCSpace 0x008550E8..0x008552F4 and the robot quad of Robot::GetBoundingQuadXY 0x00514D74. The C-space of an obstacle against the robot's padded quad is the Minkowski
    /// difference, so for a 44 mm cube (expanded by the obstacle padding 6 to +-28) and the robot at heading 0 with robot padding 7 (front 22.1+7, back -55.9-7, half width 27.1+7) the
    /// polygon is the rectangle x in [-28-29.1, 28+62.9], y in [-28-34.1, 28+34.1]. The exact rectangle is the oracle; the heading's other buckets are checked against the convex hull of every
    /// obstacle-corner minus robot-corner difference (independent of the engine's merge).
    /// </summary>
    [Fact]
    public void M13_003_ExpandCSpaceIsTheMinkowskiDifferenceOfTheObstacleAndTheRobotQuad()
    {
        var prims = Prims();
        if (prims is null) return;
        var env = new LatticeEnvironment(prims);
        env.AddRectangleObstacle(At(0, 0, 0), 44, 44, "cube");
        // heading 0: the exact rectangle; the radial expansion moves each corner of the 44 mm square 6 mm along its own ray, so the obstacle's half side is 22 + 6/sqrt2
        double half = 22 + 6 / Math.Sqrt(2);
        var poly0 = env.Bucket(0)[0].Poly;
        Assert.Equal(-half - (22.1 + 7), poly0.MinX, 3);
        Assert.Equal(half + (55.9 + 7), poly0.MaxX, 3);
        Assert.Equal(-half - (27.1 + 7), poly0.MinY, 3);
        Assert.Equal(half + (27.1 + 7), poly0.MaxY, 3);
        Assert.Equal(8, poly0.Points.Count);                                    // the merge adds every edge of both polygons: 4 + 4 points, the parallel ones collinear
        for (int t = 0; t < prims.NumAngles; t++)
        {
            var poly = env.Bucket(t)[0].Poly;
            // the oracle: the convex hull of {o - r} for the radially expanded obstacle corners and the padded robot corners at this heading, in double
            var obstacle = new[] { (-half, -half), (half, -half), (half, half), (-half, half) };
            var robot = LatticeEnvironment.RobotQuad(prims.Angles[t], 7.0f);
            var diffs = new List<(double X, double Y)>();
            foreach (var o in obstacle) foreach (var r in robot) diffs.Add((o.Item1 - r.X, o.Item2 - r.Y));
            var hull = Hull(diffs);
            // every hull vertex is a vertex of the engine's polygon (within float error) and the engine's polygon has no vertex off the hull
            var polyPts = poly.Points.Select(p => ((double)p.X, (double)p.Y)).ToList();
            foreach (var hv in hull) Assert.True(OnBoundary(polyPts, hv.X, hv.Y, 0.05), $"heading {t}: hull vertex ({hv.X}, {hv.Y}) is off the engine's polygon");
            Assert.Equal(Area(hull), Area(polyPts), 1);
            foreach (var p in poly.Points) Assert.True(OnBoundary(hull, p.X, p.Y, 0.05), $"heading {t}: vertex ({p.X}, {p.Y}) is off the Minkowski hull");
            // the polygon is clockwise about its centre: Contains(centre) holds, which is the orientation FastPolygon::Contains needs (left normals, dot <= 0 inside)
            Assert.True(poly.Contains(poly.CenterX, poly.CenterY), $"heading {t}: the C-space polygon does not contain its own centre");
        }
    }

    private static double Area(List<(double X, double Y)> p)
    {
        double a = 0;
        for (int i = 0; i < p.Count; i++) { var u = p[i]; var v = p[(i + 1) % p.Count]; a += u.X * v.Y - v.X * u.Y; }
        return Math.Abs(a) / 2;
    }

    private static bool OnBoundary(List<(double X, double Y)> hull, double x, double y, double tol)
    {
        for (int i = 0; i < hull.Count; i++)
        {
            var a = hull[i]; var b = hull[(i + 1) % hull.Count];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double t = Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
            if (Math.Sqrt(Math.Pow(x - (a.X + t * dx), 2) + Math.Pow(y - (a.Y + t * dy), 2)) <= tol) return true;
        }
        return false;
    }

    private static List<(double X, double Y)> Hull(List<(double X, double Y)> pts)
    {
        var p = pts.OrderBy(v => v.X).ThenBy(v => v.Y).ToList();
        static double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var h = new List<(double X, double Y)>();
        foreach (var v in p) { while (h.Count >= 2 && Cross(h[^2], h[^1], v) <= 1e-9) h.RemoveAt(h.Count - 1); h.Add(v); }
        int lower = h.Count + 1;
        for (int i = p.Count - 2; i >= 0; i--) { while (h.Count >= lower && Cross(h[^2], h[^1], p[i]) <= 1e-9) h.RemoveAt(h.Count - 1); h.Add(p[i]); }
        h.RemoveAt(h.Count - 1);
        return h;
    }

    /// <summary>
    /// M13-003, FastPolygon::Contains 0x00841BA0: the bounding box first (strict), then the circumscribed and inscribed radii squared, then every left-normal dot at or below zero. A point on the
    /// boundary line is inside (dot = 0 continues); one past the box is out.
    /// </summary>
    [Fact]
    public void M13_003_FastPolygonContainsFollowsTheEnginesChecks()
    {
        // a clockwise 20 x 10 rectangle about (5, 0)
        var poly = new FastPolygonF(new[] { new P2f(-5, -5), new P2f(-5, 5), new P2f(15, 5), new P2f(15, -5) });
        Assert.Equal(F(0xC0A00000), poly.MinX);        // -5.0f
        Assert.Equal(F(0x41700000), poly.MaxX);        // 15.0f
        Assert.True(poly.Contains(5, 0));              // inside the inscribed circle
        Assert.True(poly.Contains(14.99f, 4.99f));     // outside the inscribed circle, inside every half plane
        Assert.True(poly.Contains(15, 0));             // on the edge: the box test is strict, the dot is 0 and 0 <= 0 continues
        Assert.False(poly.Contains(15.001f, 0));       // past the box
        Assert.False(poly.Contains(20, 20));
        // the circumscribed radius squared is (double-widened) 100 + 25 = 125 about the centre (5, 0): the corner (15, 5) is exactly on it
        Assert.Equal(125.0f, poly.CircumscribedRadiusSquared);
        Assert.True(poly.Contains(15, 5));             // d2 == 125 is not greater than it, and every dot is 0
    }

    /// <summary>
    /// M13-003, the primitive collision test of SuccessorIterator::Next 0x0085110C..0x00851556 and IsInCollision(State_c) 0x008515F8: a soft obstacle (0.1f) adds base + penalty * reciprocal per
    /// containing pose and never rejects; a hard one (1000.0f) rejects the primitive; the soft cost sums in float.
    /// </summary>
    [Fact]
    public void M13_003_SuccessorsAddSoftPenaltiesAndDropHardCollisions()
    {
        var prims = Prims();
        if (prims is null) return;
        var free = new LatticeEnvironment(prims);
        var start = new LatticeState(0, 0, 0);
        var all = free.Successors(start, 0f, reverse: false).ToList();
        Assert.Equal(9, all.Count);
        foreach (var s in all) Assert.Equal(0f, s.SoftPenalty);
        // the total is soft + (startCost + primitive cost) (0x0085150A..0x0085151E)
        var longStraight = all.Single(s => s.ActionIndex == 1);
        Assert.Equal(Bits(0f + (2.5f + longStraight.Prim.Cost)), Bits(free.Successors(start, 2.5f, reverse: false).Single(s => s.ActionIndex == 1).Cost));
        // a soft cube across the path (its C-space starts near x = 36.7): the long straight (50 mm, poses every 0.5 mm) picks up penalties; the short one (10 mm) does not
        var soft = new LatticeEnvironment(prims);
        soft.AddRectangleObstacle(At(80, 0, 0), 20, 20, "soft");
        var longSoft = soft.Successors(start, 0f, reverse: false).Single(s => s.ActionIndex == 1);
        Assert.True(longSoft.SoftPenalty > 0f);
        float expectedSoft = 0f;
        foreach (var pose in longSoft.Prim.Intermediate)
            foreach (var (poly, penalty) in soft.Bucket(0))
                if (poly.Contains(pose.X, pose.Y)) expectedSoft = expectedSoft + (0f + penalty * pose.Reciprocal);
        Assert.Equal(Bits(expectedSoft), Bits(longSoft.SoftPenalty));
        // a hard obstacle at the end of the long straight rejects it but not the short one
        var hard = new LatticeEnvironment(prims);
        hard.AddRectangleObstacle(At(80, 0, 0), 20, 20, "hard", 1000.0f);
        var left = hard.Successors(start, 0f, reverse: false).Select(s => s.ActionIndex).ToList();
        Assert.DoesNotContain(1, left);
        Assert.Contains(0, left);
    }

    // ------------------------------------------------------------------ the planner's constants

    /// <summary>
    /// M13-003 / M13-004 / M13-018, every planner constant as the engine's word: the robot quad's corners (0x00514DB0: movw/movt 0xCCCC/0x41B0, 0xCCCD/0xC1D8, 0xCCCD/0x41D8, 0x999A/0xC25F), the
    /// paddings 7.0f/6.0f and 2.0f/1.0f (vmov.f32 immediates at 0x004FD4E4..0x004FD4F2), the obstacle penalty 0x3DCCCCCD (0x004FE0CE), the hard threshold 0x447A0000 (0x00851382), the heading
    /// step (float)(2pi/16) and the RobotActionParams doubles (0x0084EDE6..0x0084EE18), and the angle words the loops add.
    /// </summary>
    [Fact]
    public void M13_003_ThePlannerConstantsAreTheEnginesWords()
    {
        Assert.Equal(0x41B0CCCCu, Bits(LatticeEnvironment.RobotFrontMm));
        Assert.Equal(0xC25F999Au, Bits(LatticeEnvironment.RobotBackMm));
        Assert.Equal(0x41D8CCCDu, Bits(LatticeEnvironment.RobotHalfWidthMm));
        Assert.Equal(0x40E00000u, Bits(LatticeEnvironment.RobotPaddingMm));              // 7.0f
        Assert.Equal(0x40C00000u, Bits(LatticeEnvironment.ObstaclePaddingMm));           // 6.0f
        Assert.Equal(0x40000000u, Bits(LatticeEnvironment.TightRobotPaddingMm));         // 2.0f
        Assert.Equal(0x3F800000u, Bits(LatticeEnvironment.TightObstaclePaddingMm));      // 1.0f
        Assert.Equal(0x3DCCCCCDu, Bits(LatticeEnvironment.ObstaclePenalty));
        Assert.Equal(0x447A0000u, Bits(LatticeEnvironment.HardPenalty));
        Assert.Equal(24.0, MotionPrimitiveSet.HalfWheelBaseMm);                          // 0x4038000000000000
        Assert.Equal(60.0, MotionPrimitiveSet.MaxVelocityMmps);                          // 0x404E000000000000
        Assert.Equal(25.0, MotionPrimitiveSet.MaxReverseVelocityMmps);                   // 0x4039000000000000
        Assert.Equal(0x3F91111111111111L, BitConverter.DoubleToInt64Bits(MotionPrimitiveSet.InverseMaxVelocity));
        Assert.Equal(0x40490FDBu, Bits(EngineF.Pi));
        Assert.Equal(0xC0490FDBu, Bits(EngineF.NegPi));
        Assert.Equal(0x40C90FDBu, Bits(EngineF.TwoPi));
        Assert.Equal(0xC0C90FDBu, Bits(EngineF.NegTwoPi));
        Assert.Equal(0x401921FB54442D18L, BitConverter.DoubleToInt64Bits(EngineF.TwoPiD));
        Assert.Equal(0x400921FB54442D18L, BitConverter.DoubleToInt64Bits(EngineF.PiD));
        Assert.Equal(30_000_000, LatticePlanner.MaxExpansions);                          // 0x01C9C380
        Assert.Equal(0x01C9C380, LatticePlanner.MaxExpansions);
        Assert.Equal(0x3D0EFA35u, Bits((float)StraightLinePlanner.PointTurnToleranceRad));
    }

    /// <summary>
    /// M13-003 / M13-018, Radians::rescale 0x0084C87C and angularDistance 0x0084CD74 in binary32: the values the planner's headings go through. In range is unchanged; a small value steps by the float
    /// 2pi; a large one uses the ceilf form; the distance adds 2pi to a negative difference with the flag clear and subtracts it from a positive one with the flag set.
    /// </summary>
    [Fact]
    public void M13_018_RadiansRescaleAndAngularDistanceAreTheEnginesFloatFunctions()
    {
        Assert.Equal(1.0f, EngineF.Rescale(1.0f));
        Assert.Equal(Bits(4.0f + F(0xC0C90FDB)), Bits(EngineF.Rescale(4.0f)));                                       // one step of -2pi (0x0084C8EA..0x0084C8FC)
        Assert.Equal(Bits(-4.0f + F(0x40C90FDB)), Bits(EngineF.Rescale(-4.0f)));                                     // one step of +2pi
        float big = 100.0f;
        Assert.Equal(Bits(big - (float)(int)MathF.Ceiling(big / F(0x40C90FDB) + -0.5f) * F(0x40C90FDB)), Bits(EngineF.Rescale(big)));   // 0x0084C902..0x0084C92E
        Assert.Equal(Bits(-0.5f + F(0x40C90FDB)), Bits(EngineF.AngularDistance(1.0f, 0.5f, false)));                 // other - this = -0.5 < 0, flag clear: + 2pi
        Assert.Equal(0.5f, EngineF.AngularDistance(0.5f, 1.0f, false));
        Assert.Equal(Bits(0.5f + F(0xC0C90FDB)), Bits(EngineF.AngularDistance(0.5f, 1.0f, true)));                   // positive with the flag set: - 2pi
        Assert.Equal(-0.5f, EngineF.AngularDistance(1.0f, 0.5f, true));                                              // negative with the flag set: unchanged
        // the heading index: 0.4636476f scaled by 1/(float)(2pi/16) rounds to 1, -0.4 wraps to 5.883 and rounds to 15
        Assert.Equal(1, EngineF.ThetaIndex(0.4636476f, 1.0f / (float)(2 * Math.PI / 16.0), 16));
        Assert.Equal(15, EngineF.ThetaIndex(-0.4f, 1.0f / (float)(2 * Math.PI / 16.0), 16));
    }

    // ------------------------------------------------------------------ M13-004: the primitive import

    /// <summary>
    /// M13-004, MotionPrimitive::Create 0x00853DD0..0x008543B8 over the shipped asset (cozmo_mprim.json, sha256 4C79...431D): the straight cost is <c>(float)(d8 * |len| + 0)</c> with d8 the double
    /// 1/60 (env+0x78, 0x3F91111111111111) forward and 1/25 for the reverse action, times the action's float factor; the arc adds <c>(float)(d8 * (|sweep| * (|radius| + 24.0)))</c>; the in-place
    /// turn <c>(float)(d8 * (24.0 * |angularDistance|))</c>. Expected values are computed here from the asset's numbers in double, then narrowed where the instructions narrow.
    /// </summary>
    [Fact]
    public void M13_004_PrimitiveCostsAreTheEnginesBinary32Values()
    {
        var prims = Prims();
        if (prims is null) return;
        double d8 = BitConverter.Int64BitsToDouble(0x3F91111111111111);
        Assert.Equal(0x3F91111111111111L, BitConverter.DoubleToInt64Bits(MotionPrimitiveSet.InverseMaxVelocity));
        var a0 = prims.ByAngle[0];
        // The expected words are an independent numpy float32/double emulation of the cited instruction sequences over the asset's own numbers (scratch generator gen_prims.py), not this
        // implementation's expressions: long straight 50 mm x 1.0; short straight 10 mm x 1.0001f; backwards short straight at 1/25 x 1.2f; slight left (straight 7.639... then the arc term);
        // in-place turn left (term from angle[1], x 2.0).
        Assert.Equal(0x3F555555u, Bits(a0.Single(p => p.ActionIndex == 1).Cost));
        Assert.Equal(0x3E2AAF0Au, Bits(a0.Single(p => p.ActionIndex == 0).Cost));
        Assert.Equal(0x3EF5C290u, Bits(a0.Single(p => p.ActionIndex == 8).Cost));
        Assert.Equal(0x3F85B9EDu, Bits(a0.Single(p => p.ActionIndex == 2).Cost));
        Assert.Equal(0x3EBDE8FAu, Bits(a0.Single(p => p.ActionIndex == 6).Cost));
        // the reciprocal of a long-straight pose: dist 0.5, dtheta 0 -> 1.0f / (float)(0 + 0.5) = 2.0f (0x00853F3A..0x00853F46); the first pose has none
        var longStraight = a0.Single(p => p.ActionIndex == 1);
        Assert.Equal(0f, longStraight.Intermediate[0].Reciprocal);
        Assert.Equal(2.0f, longStraight.Intermediate[1].Reciprocal);
        // an arc pose's reciprocal carries the heading change: (float)(1/60 * (24 * |dth|) + (double)dist), then 1.0f / that
        var slightLeft = a0.Single(p => p.ActionIndex == 2);
        int k = slightLeft.Intermediate.ToList().FindIndex(q => q.Theta != 0f);
        Assert.Equal(17, k);                                                          // the asset's 18th pose is the first with a non-zero heading
        Assert.Equal(0x3FFEEC90u, Bits(slightLeft.Intermediate[k].Reciprocal));       // numpy float32/double emulation of 0x00853EBA..0x00853F46 over the asset's poses 16 and 17
        Assert.Equal(0x3FFEEC7Cu, Bits(slightLeft.Intermediate[k + 1].Reciprocal));
        // the heading index byte: round(theta * (1 / float(2pi/16))) % 16 with 2pi/16 = (float)(2pi/16.0) (0x00851EF2..0x00851F10): the 90 degree pose of the in-place turn at angle 3..4
        Assert.Equal(0x3EC90FDBu, Bits(prims.AngleStep));
    }

    /// <summary>
    /// M13-004, the failure gates of MotionPrimitive::Create (0x00854322..0x008543B2) and ParseMotionPrims (0x00852368..0x008523B4, 0x00852402): a base cost below 1e-6 fails the primitive and the
    /// parse fails with "Failed to import motion primitive"; a per-primitive extra_cost_factor is rejected (0x00853FFC).
    /// </summary>
    [Fact]
    public void M13_004_ABadPrimitiveFailsTheWholeParseAsTheEnginesDoes()
    {
        const string head = "{\"resolution_mm\":10.0,\"num_angles\":1,\"angle_definitions\":[0.0],\"actions\":[{\"index\":0,\"name\":\"a\",\"extra_cost_factor\":1.0}],\"angles\":[{\"prims\":[";
        const string tail = "]}]}";
        // a straight of 1e-9 mm: cost = 1/60 * 1e-9 = 1.7e-11 < 1e-6
        var tiny = head + "{\"action_index\":0,\"end_pose\":{\"theta\":0,\"x\":1,\"y\":0},\"intermediate_poses\":[],\"straight_length_mm\":1e-9}" + tail;
        Assert.Throws<InvalidDataException>(() => MotionPrimitiveSet.Parse(tiny));
        var perPrim = head + "{\"action_index\":0,\"extra_cost_factor\":2.0,\"end_pose\":{\"theta\":0,\"x\":1,\"y\":0},\"intermediate_poses\":[],\"straight_length_mm\":10.0}" + tail;
        Assert.Throws<InvalidDataException>(() => MotionPrimitiveSet.Parse(perPrim));
        // and a good one parses, with a real-valued x/y like the asset's 1.0
        var good = head + "{\"action_index\":0,\"end_pose\":{\"theta\":0,\"x\":1.0,\"y\":0.0},\"intermediate_poses\":[],\"straight_length_mm\":10.0}" + tail;
        var set = MotionPrimitiveSet.Parse(good);
        Assert.Equal((short)1, set.ByAngle[0][0].EndX);
        Assert.Equal(Bits((float)(0.016666666666666666 * 10.0)), Bits(set.ByAngle[0][0].Cost));
    }

    // ------------------------------------------------------------------ M13-018: the search

    /// <summary>
    /// M13-018, OpenList 0x0084E4DA..0x0084E67C is a std::multimap&lt;float, StateID&gt; whose __emplace_multi inserts at the upper bound: equal keys pop in insertion order, the smallest key first.
    /// </summary>
    [Fact]
    public void M13_018_TheOpenListIsAMultimapWithFifoTies()
    {
        var open = new LatticePlanner.OpenList();
        open.Insert(11, 5.0f); open.Insert(22, 5.0f); open.Insert(33, 3.0f); open.Insert(44, 5.0f);
        Assert.Equal(3.0f, open.TopF());
        Assert.Equal(new uint[] { 33, 11, 22, 44 }, new[] { open.Pop(), open.Pop(), open.Pop(), open.Pop() });
        Assert.True(open.Empty);
        // remove(iterator) erases one entry: the replaced state is gone from its old key
        var a = open.Insert(1, 2.0f); open.Insert(2, 2.0f);
        open.Remove(a);
        Assert.Equal(2u, open.Pop());
    }

    /// <summary>
    /// M13-018, StateID packing (State::GetStateID 0x0084FC58: <c>(y &lt;&lt; 18) | ((x &amp; 0x3FFF) &lt;&lt; 4) | (theta &amp; 0xF)</c>; State::State(StateID) 0x0084F88C: x = sbfx(id, 4, 14),
    /// y = id &gt;&gt; 18; SuccessorIterator::Next 0x00851500..0x00851514): theta in bits 0..3, x in bits 4..17 and y in bits 18..31, both signed 14-bit.
    /// </summary>
    [Fact]
    public void M13_018_TheStateIdPacksXYAndThetaAsTheEngineDoes()
    {
        var s = new LatticeState(5, -3, 7);
        Assert.Equal((unchecked((uint)-3) << 18) | (5u << 4) | 7u, s.Id);
        Assert.Equal(s, LatticeState.FromId(s.Id));
        Assert.Equal((100u << 4) | 3u, new LatticeState(100, 0, 3).Id);               // x alone lands in bits 4..17
        Assert.Equal((100u << 18) | 3u, new LatticeState(0, 100, 3).Id);              // y alone lands in bits 18..31
        var neg = new LatticeState(-8191, 8191, 15);
        Assert.Equal(neg, LatticeState.FromId(neg.Id));
    }

    /// <summary>
    /// M13-018, ComputePath 0x008586A0 through the live planner entry. With goals in free space the search expands only the heuristic's straight line; the goal's rounded cell is the plan's end;
    /// the engine's rounding is <c>roundf(x * (1/10))</c> (0x00859000..0x00859030). A goal in a hard collision is dropped (0x00858FB2..0x00858FBA) and the search then has no goal.
    /// </summary>
    [Fact]
    public void M13_018_ComputePathRoundsTheGoalAndDropsGoalsInHardCollision()
    {
        var prims = Prims();
        if (prims is null) return;
        var env = new LatticeEnvironment(prims);
        var planner = new LatticePlanner(env);
        var plan = planner.ComputePath(new StateC(0, 0, 0), new[] { new StateC(104.9f, 0.4f, 0f) });
        Assert.NotNull(plan);
        var end = plan!.States().Last();
        Assert.Equal((short)10, end.X); Assert.Equal((short)0, end.Y);          // roundf(10.49) = 10, roundf(0.04) = 0
        Assert.Equal(planner.LastFinalCost, plan.Cost);
        // a goal in a hard obstacle is dropped; with no goal left the planner fails
        var hard = new LatticeEnvironment(prims);
        hard.AddRectangleObstacle(At(150, 0, 0), 20, 40, "hard", 1000.0f);
        Assert.Null(new LatticePlanner(hard).ComputePath(new StateC(0, 0, 0), new[] { new StateC(150, 0, 0) }));
    }

    /// <summary>
    /// M13-018, InitializeHeuristic 0x008598DC / ExpandCollisionStatesFromGoal 0x00859BF0 / heur 0x0085A780: a goal inside a soft obstacle writes the shared best-cost map while it expands
    /// outwards, and the planner's own <c>heur</c> returns those values for those states.
    /// </summary>
    [Fact]
    public void M13_018_AGoalInsideASoftObstacleSharesItsExpansionWithTheHeuristic()
    {
        var prims = Prims();
        if (prims is null) return;
        var env = new LatticeEnvironment(prims);
        env.AddRectangleObstacle(At(150, 0, 0), 44, 44, "cube");
        var planner = new LatticePlanner(env);
        var goal = new LatticeState(15, 0, 0);
        Assert.True(env.IsInSoftCollision(goal));
        var plan = planner.ComputePath(new StateC(0, 0, 0), new[] { new StateC(150, 0, 0) });
        Assert.NotNull(plan);
        Assert.NotEmpty(planner.HeuristicMapForTests);
        Assert.Equal(0f, planner.HeuristicMapForTests[goal.Id]);                    // hm[goal] = 0.0f (0x00859C0C..0x00859C12)
        // every state in the map is either the goal or a state popped from the reflected expansion while in soft collision
        foreach (var (id, cost) in planner.HeuristicMapForTests)
        {
            Assert.True(env.IsInSoftCollision(LatticeState.FromId(id)));
            Assert.True(cost >= 0f);
        }
    }

    // ------------------------------------------------------------------ M13-005 / M13-018: the planning failure through DriveToPoseAction

    /// <summary>
    /// M13-005 / M13-018, DoPlanning 0x00500090: the sleep loop checks the run flag every 10 ms chunk and returns 0 when it is cleared (0x005000A2..0x005000EA); a failed plan sends no path and
    /// ends the action (0x00500202). Driven through DriveToPoseAction's live entry: the planner is made to wait, StopPlanning clears its flag, and nothing goes on the wire.
    /// </summary>
    [Fact]
    public void M13_005_AStoppedPlanSendsNoPathThroughDriveToPoseAction()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        Assert.True(rig.M.LoadPlanner(obb));
        rig.M.Planner!.ArtificialPlannerDelayMs = 3000;
        using var planning = new ManualResetEventSlim();
        rig.M.Planner.PlanningStarted += planning.Set;
        var drive = new DriveToPoseAction(rig.M) { Goal = At(250, 120, Math.PI / 2) };
        var task = Task.Run(() => drive.RunAsync(default));         // the planning runs inside the first call, so it needs its own thread for StopPlanning to reach it
        Assert.True(planning.Wait(TimeSpan.FromMinutes(2)), "the planning worker never entered DoPlanning");
        rig.M.Planner.StopPlanning();
        SignalTestContext.Run(task, () => { rig.Pump(); });
        Assert.True(task.IsCompleted);
        Assert.Equal(ActionResult.PathPlanningFailedAbort, task.Result);
        Assert.Equal(LatticePlanner.PlanningResult.Failure, rig.M.Planner.LastPlanningResult);
        Assert.DoesNotContain(rig.Sent, m => m is ExecutePath or AppendPathSegmentLine or AppendPathSegmentArc or AppendPathSegmentPointTurn);
        Assert.Contains(drive.Trace, l => l.Contains("PlanningFailed"));
    }

    // ------------------------------------------------------------------ M13-011: the segment words

    /// <summary>
    /// M13-011, PathDolerOuter::Dole 0x00507E4C..0x00508056: the wire words are the PathSegment's binary32 words copied as they are. A line, an arc and a point turn built by the planner go out with
    /// exactly the float bits the segments hold (no second rounding), and a double-valued producer is rounded once, where it hands its values over.
    /// </summary>
    [Fact]
    public void M13_011_TheSegmentFieldsAreBinary32WordsCopiedToTheWire()
    {
        var line = new PathSegment.Line(1.0 / 3.0, 2.0 / 3.0, 4.0 / 3.0, 5.0 / 3.0, 60f, 200f, 500f);
        Assert.Equal(Bits((float)(1.0 / 3.0)), Bits(line.FromX));                    // the double constructor narrows once
        var arc = new PathSegment.Arc(7.639320225002111, 94.72135954999578, 94.72135954999578, -1.5707963267948966, 0.4636476090008061, 100f, 200f, 500f);
        Assert.Equal(Bits(-1.5707964f), Bits(arc.StartAngleRad));
        var turn = new PathSegment.PointTurn(1.5, 2.5, 3.0, 0.0349066, 2f, 10f, 10f, true);
        Assert.Equal(Bits((float)0.0349066), Bits(turn.AngleToleranceRad));
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.M.StartPath(new PathSegment[] { line, arc, turn }).Dispose();
        rig.Pump();
        var l = rig.Sent.OfType<AppendPathSegmentLine>().Single();
        Assert.Equal(Bits(line.FromX), Bits(l.XStartMm)); Assert.Equal(Bits(line.ToY), Bits(l.YEndMm));
        var a = rig.Sent.OfType<AppendPathSegmentArc>().Single();
        Assert.Equal(Bits(arc.CenterX), Bits(a.XCenterMm)); Assert.Equal(Bits(arc.StartAngleRad), Bits(a.StartRad)); Assert.Equal(Bits(arc.SweepRad), Bits(a.SweepRad));
        var t = rig.Sent.OfType<AppendPathSegmentPointTurn>().Single();
        Assert.Equal(Bits(turn.TargetAngleRad), Bits(t.TargetAngleRad)); Assert.Equal(Bits(turn.AngleToleranceRad), Bits(t.AngleToleranceRad));
    }

    // ------------------------------------------------------------------ M13-009: the charger geometry

    /// <summary>
    /// M13-009, Charger::Charger 0x004E9B6C..0x004EA202: the literals as binary32 words, asserted by their bits (and the two angles are the engine's Radians(float) values, not the doubles).
    /// </summary>
    [Fact]
    public void M13_009_TheChargerLiteralsAreTheEnginesWords()
    {
        Assert.Equal(0x42C00000u, ChargerGeometry.LengthBits); Assert.Equal(96.0f, F(ChargerGeometry.LengthBits));            // 0x004E9B9A
        Assert.Equal(0x42A00000u, ChargerGeometry.WidthBits);                                                                    // 0x004E9BAC
        Assert.Equal(0x41F80000u, ChargerGeometry.HeightBits);                                                                   // 0x004E9BB0
        Assert.Equal(0xBFC90FDBu, ChargerGeometry.MarkerAngleBits);                                                              // 0x004E9BBC..0x004E9BC2
        Assert.Equal(0x42AC0000u, ChargerGeometry.MarkerXBits);                                                                  // 0x004E9BD6: 86.0f
        Assert.Equal(0x41B00000u, ChargerGeometry.MarkerZBits);                                                                  // 0x004E9BE2: 22.0f
        Assert.Equal(0x41D80000u, ChargerGeometry.MarkerWidthBits);                                                              // 0x004E9C2C: 27.0f
        Assert.Equal(0x41A00000u, ChargerGeometry.MarkerHeightBits);                                                             // 0x004E9C22: 20.0f
        Assert.Equal(0x40490FDBu, ChargerGeometry.DockedAngleBits);                                                              // 0x004EA1AC..0x004EA1B2
        Assert.Equal(0x41F00000u, ChargerGeometry.DockedXBits);                                                                  // 0x004EA1C6: 30.0f
        Assert.Equal(0x437A0000u, ChargerGeometry.PreDockDistanceBits);                                                          // 0x004D6BD6: 250.0f
        Assert.Equal(0xC1780000u, ChargerGeometry.PreDockZBits);                                                                 // 0x004EA018: -15.5f
        Assert.Equal(Bits(0f + F(0x3FC90FDB)), Bits((float)ChargerGeometry.PreDockAngleRad));                                    // vadd.f32 at 0x004E9FF2
        Assert.Equal(0xBFC90FDBu, Bits((float)ChargerGeometry.MarkerAngleRad));
        Assert.Equal(0x40490FDBu, Bits((float)ChargerGeometry.DockedAngleRad));
    }

    /// <summary>
    /// M13-009, through the live entry: the charger is created by BlockWorld from a rendered view of its marker, and Charger::GeneratePreActionPoses 0x004E9FB0 (types 0 and 1 only, one pose) is asked
    /// for the located object. The expected pose is the float32 emulation of the instructions at 0x004E9FE4..0x004EA058: Pose3d(Radians(0 + pi/2), Z, (0, -250, -15.5)) on the marker's pose
    /// (-pi/2 at (86, 0, 22)) on the charger's pose; GetRobotDockedPose 0x004EA1A0 is Pose3d(Radians(pi), Z, (30, 0, 0)) on the charger.
    /// </summary>
    [Fact]
    public void M13_009_APreDockPoseIsGeneratedForALocatedChargerForActionTypesZeroAndOneOnly()
    {
        if (MarkerLibrary.EmbeddedOrNull is null) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.Head = -0.2f;
        rig.Charger = At(200, 0, 0);
        var obs = Assert.Single(rig.Frame().Objects);
        var charger = obs.Object;
        Assert.Equal(ObjectType.Charger_Basic, charger.Type);
        Assert.Empty(ChargerGeometry.GeneratePreActionPoses(2, charger.Pose));               // cmp r5,#1; bhi (0x004E9FD4)
        Assert.Empty(ChargerGeometry.GeneratePreActionPoses(7, charger.Pose));
        foreach (uint type in new uint[] { 0, 1 })
        {
            var pose = Assert.Single(ChargerGeometry.GeneratePreActionPoses(type, charger.Pose));
            // emulate: marker = Rz(a_m) at (86, 0, 22); local = Rz(a_p) at (0, -250, -15.5); world = charger * marker * local
            float am = F(0xBFC90FDB), ap = 0f + F(0x3FC90FDB);
            float cm = MathF.Cos(am), sm = MathF.Sin(am);
            float lx = 0f, ly = -250f, lz = -15.5f;
            float mx = 86f + (lx * cm + ly * -sm), my = 0f + (lx * sm + ly * cm), mz = 22f + lz;      // local -> charger frame
            var c = charger.Pose;
            Assert.Equal(c.Translation.X + c.Rotation[0, 0] * mx + c.Rotation[0, 1] * my + c.Rotation[0, 2] * mz, pose.Translation.X, 3);
            Assert.Equal(c.Translation.Y + c.Rotation[1, 0] * mx + c.Rotation[1, 1] * my + c.Rotation[1, 2] * mz, pose.Translation.Y, 3);
            Assert.Equal(c.Translation.Z + c.Rotation[2, 0] * mx + c.Rotation[2, 1] * my + c.Rotation[2, 2] * mz, pose.Translation.Z, 3);
            // the heading: a_m + a_p about Z (0 within float error) on the charger's own heading
            Assert.Equal(c.AngleAroundZ, pose.AngleAroundZ, 4);
        }
        var docked = ChargerGeometry.DockedRobotPose(charger.Pose);
        var cc = charger.Pose;
        Assert.Equal(cc.Translation.X + cc.Rotation[0, 0] * 30.0, docked.Translation.X, 4);
        Assert.Equal(cc.Translation.Y + cc.Rotation[1, 0] * 30.0, docked.Translation.Y, 4);
        Assert.Equal(cc.Translation.Z + cc.Rotation[2, 0] * 30.0, docked.Translation.Z, 4);
        Assert.Equal(Math.PI, Math.Abs(docked.AngleAroundZ - cc.AngleAroundZ), 4);
    }

    // ------------------------------------------------------------------ M13-013: DriveOffChargerContactsAction on its ticks

    private sealed class FakeDrive : IDriveStraightTick
    {
        public int Inits, Cancels; public readonly Queue<uint> Results = new();
        public uint Init() { Inits++; return 0; }
        public uint CheckIfDone() => Results.Count > 0 ? Results.Dequeue() : (uint)ActionResult.Running;
        public void Cancel() => Cancels++;
    }

    /// <summary>
    /// M13-013, DriveOffChargerContactsAction ctor 0x00558228, Init 0x005582D0, CheckIfDone 0x005582E4: Init copies robot+0x338 and only starts the drive when it is set; CheckIfDone returns 0 when
    /// it was clear, RUNNING while the drive runs, and once the drive is over - whatever its result - 0x04000009 if the robot is still on the contacts and 0 if it is not.
    /// </summary>
    [Fact]
    public void M13_013_TheContactsActionTicksInitAndCheckIfDoneAsTheEngineDoes()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        // not on the contacts: Init returns 0 without starting the drive; CheckIfDone returns 0
        rig.OnCharger = false; rig.State();
        var idle = new FakeDrive();
        var notOn = new DriveOffChargerContactsAction(rig.M, drive: idle);
        Assert.Equal(0u, notOn.Init());
        Assert.Equal(0, idle.Inits);
        Assert.Equal(0u, notOn.CheckIfDone());

        // on the contacts: the drive starts, RUNNING passes through, a FAILED drive is ignored and the contacts decide
        rig.OnCharger = true; rig.State();
        var drive = new FakeDrive();
        drive.Results.Enqueue((uint)ActionResult.Running);
        drive.Results.Enqueue((uint)ActionResult.Abort);            // the drive failed...
        var on = new DriveOffChargerContactsAction(rig.M, drive: drive);
        Assert.Equal(0u, on.Init());
        Assert.Equal(1, drive.Inits);
        Assert.True(on.WasOnContactsAtInit);
        Assert.Equal((uint)ActionResult.Running, on.CheckIfDone());
        Assert.Equal((uint)ActionResult.StillOnCharger, on.CheckIfDone());   // ...still on the contacts: 0x04000009 (0x00558344), not the drive's own result
        Assert.Contains(on.Trace, l => l.Contains(DriveOffChargerContactsAction.StillOnChargerWarning));
        // the same failed drive with the robot off the contacts returns 0 (0x0055830C..0x0055830E)
        rig.OnCharger = false; rig.State();
        drive.Results.Enqueue((uint)ActionResult.Abort);
        Assert.Equal(0u, on.CheckIfDone());
    }

    /// <summary>
    /// M13-013, the constructor's SDK-only SetTracksToLock(0) (CozmoContext::IsInSdkMode 0x0055827C, bne 0x00558282, call 0x00558288). With no SDK-mode state in this stack the missing seam is
    /// reported, not silently answered; with a seam the call is made only when it says SDK.
    /// </summary>
    [Fact]
    public void M13_013_TheTrackLockClearIsMadeOnlyInSdkMode()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        Assert.False(new DriveOffChargerContactsAction(rig.M, isInSdkMode: () => false, drive: new FakeDrive()).TracksToLockCleared);
        Assert.True(new DriveOffChargerContactsAction(rig.M, isInSdkMode: () => true, drive: new FakeDrive()).TracksToLockCleared);
        var reported = new List<string>();
        void On(string s) => reported.Add(s);
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try { Assert.False(new DriveOffChargerContactsAction(rig.M, drive: new FakeDrive()).TracksToLockCleared); }
        finally { SteppedBehavior.MissingReported -= On; }
        Assert.Contains(reported, r => r.Contains("IsInSdkMode"));
    }

    // ------------------------------------------------------------------ M13-016: AlignWithObjectAction::Verify's results

    /// <summary>
    /// M13-016, AlignWithObjectAction::Verify 0x005534E0..0x0055359E, read from the instructions: a dock action other than 0xA/0xB fails 0x0300001A (0x00553596); `ldrb [[+0xCC]+4]; cmp r0,#0; mov r0,r5;
    /// bne 0x0055359C` returns 0x04000003 when byte +4 is NON-ZERO; byte +4 zero falls to PathComponent::IsActive (0x0055350A), a non-zero result returning 0x04000002 (r6); then byte +5 zero
    /// (`beq 0x005535A0`) returns 0x04000003; otherwise it logs "Align with object SUCCEEDED!" and returns 0.
    /// </summary>
    [Fact]
    public void M13_016_VerifyReturnsTheEnginesResultCodes()
    {
        Assert.Equal(0x0300001Au, AlignWithObjectAction.VerifyResult(0x05, 1, 1, false, out var log0)); Assert.Equal("AlignWithObjectAction.Verify.ReachedDefaultCase", log0);
        Assert.Equal(0x04000003u, AlignWithObjectAction.VerifyResult(0x0A, 1, 1, false, out _));      // byte +4 non-zero
        Assert.Equal(0x04000003u, AlignWithObjectAction.VerifyResult(0x0B, 1, 1, false, out _));
        Assert.Equal(0x04000003u, AlignWithObjectAction.VerifyResult(0x0B, 1, 0, true, out _));       // byte +4 is tested before the path
        Assert.Equal(0x04000002u, AlignWithObjectAction.VerifyResult(0x0A, 0, 1, true, out _));      // byte +4 zero, the path active
        Assert.Equal(0x04000003u, AlignWithObjectAction.VerifyResult(0x0A, 0, 0, false, out _));     // byte +5 zero
        Assert.Equal(0u, AlignWithObjectAction.VerifyResult(0x0B, 0, 1, false, out var ok)); Assert.Equal("Align with object SUCCEEDED!", ok);
    }

    // ------------------------------------------------------------------ M13-008 / M13-012: BackupOntoChargerAction::CheckIfDone

    /// <summary>
    /// M13-008, BackupOntoChargerAction::CheckIfDone 0x0054E7A8..0x0054E7EE: on the contacts it returns 0 (after Robot::SetPoseOnCharger); a pitch below the float 0xBE860A92 returns 0x0400000A;
    /// otherwise the drive's own CheckIfDone result when it is not 0 and 0x04000006 when it is.
    /// </summary>
    [Fact]
    public void M13_008_BackupCheckIfDoneReturnsTheEnginesCodesInTheEnginesOrder()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        var mount = new MountChargerAction(rig.M, 1);
        var drive = new FakeDrive();
        // off the contacts, level, drive running -> RUNNING
        rig.OnCharger = false; rig.State();
        Assert.Equal((uint)ActionResult.Running, mount.BackupCheckIfDone(drive));
        // the drive failed with something: that result (movne r4, r0)
        drive.Results.Enqueue((uint)ActionResult.Abort);
        Assert.Equal((uint)ActionResult.Abort, mount.BackupCheckIfDone(drive));
        // the drive ended with 0 off the contacts: 0x04000006
        drive.Results.Enqueue(0u);
        Assert.Equal(0x04000006u, mount.BackupCheckIfDone(drive));
        // pitched below -15 degrees: 0x0400000A, before the drive is asked
        rig.Pitch = F(0xBE860A93);                                       // one ulp below the threshold
        rig.State();
        drive.Results.Enqueue(0u);
        Assert.Equal(0x0400000Au, mount.BackupCheckIfDone(drive));
        rig.Pitch = F(0xBE860A92); rig.State();                          // exactly the threshold is not below it (vcmpe; bpl)
        Assert.Equal(0x04000006u, mount.BackupCheckIfDone(drive));       // the queued 0 is now consumed
        // on the contacts: 0 first, whatever else holds
        rig.Pitch = F(0xBF000000); rig.OnCharger = true; rig.State();
        Assert.Equal(0u, mount.BackupCheckIfDone(drive));
    }

    /// <summary>
    /// M13-008, MountChargerAction::Init 0x0054E0CC..0x0054E170: a missing charger (or one of another type) warns "MountChargerAction.Init.InvalidCharger" and fails 0x03000004 without any
    /// message to the robot.
    /// </summary>
    [Fact]
    public void M13_008_AMissingChargerFailsInitWithBadObject()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        var mount = new MountChargerAction(rig.M, 424242);
        Assert.Equal(ActionResult.BadObject, mount.RunAsync(default).GetAwaiter().GetResult());
        Assert.Contains(mount.Trace, l => l.Contains("MountChargerAction.Init.InvalidCharger"));
        Assert.Empty(rig.Sent.OfType<SetBodyAngle>());
    }

    /// <summary>
    /// M13-012, ConfigureTurnAndMountAction 0x0054E58A..0x0054E5C6: the lift move is added only when Robot::GetLiftHeight is strictly below 45.0f (0x42340000, bpl at 0x0054E59E) and it is
    /// MoveLiftToHeightAction(45, 5, 0): the 5 is the tolerance, so the lift goes out with the action's own default speed, not 5.
    /// Driven through MountChargerAction.RunAsync's live entry.
    /// </summary>
    [Fact]
    public void M13_012_TheMountRaisesTheLiftOnlyWhenItIsBelowFortyFiveAndNotAtFortyFive()
    {
        if (MarkerLibrary.EmbeddedOrNull is null) return;
        foreach (bool low in new[] { false, true })
        {
            using var signals_rig = SignalTestContext.Install();
            using var rig = new Rig();
            rig.Head = -0.2f;
            rig.Charger = At(200, 0, 0);
            Assert.Single(rig.Frame().Objects);
            rig.DockOutcome = BlockStatus.NoBlock;
            // the lift reports 45 mm (angle 0, the rig's default) or 32 mm
            rig.LiftAngleReported = low ? (float)Math.Asin((32.0 - 45.0) / 66.0) : 0f;
            rig.State();
            rig.M.World.UnobservedMissesToUnknown = int.MaxValue;
            // This case checks the queued lift's strict threshold and speed, not action deadlines.
            var mount = new MountChargerAction(rig.M, ChargerGeometry.ObjectId) { Clock = () => 0f };
            var task = mount.RunAsync(default);
            SignalTestContext.Run(task, () => { rig.Pump(); if (!rig.OnCharger) rig.Frame(); });
            Assert.True(task.IsCompleted);
            var lifts = rig.Sent.OfType<SetLiftHeight>().Where(l => l.HeightMm == 45f).ToList();
            if (!low) Assert.Empty(lifts);                                            // 45.0 is not below 45.0
            else
            {
                var l = Assert.Single(lifts);
                Assert.NotEqual(5f, l.MaxSpeedRadPerSec);                              // the 5 is the tolerance, not the speed
            }
        }
    }

    // ------------------------------------------------------------------ M13-017: BehaviorDriveOffCharger

    private static BehaviorContext Ctx(Rig rig) => new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };

    private static bool Runnable(SteppedBehavior b, BehaviorContext ctx) =>
        (bool)typeof(SteppedBehavior).GetMethod("IsRunnableInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(b, new object[] { ctx })!;

    private static ObservableObject WorldCharger(Rig rig, double x, double y)
    {
        var o = new ObservableObject(100, ObjectType.Charger_Basic, ChargerGeometry.Markers) { Pose = At(x, y, 0), PoseState = PoseState.Known, OriginId = rig.Vision.World.CurrentOriginId };
        rig.Vision.World.AddLocatedObject(o);
        return o;
    }

    /// <summary>
    /// M13-017, IsRunnableInternal 0x005C0B10 returns robot+0x34A (OnChargerPlatform), not the +0x338 contacts flag: SetOnCharger's rising edge sets the platform flag and its falling edge leaves
    /// it set (0x005119AA..0x00511C14), so a robot that has just lost the contacts is still runnable until Robot::Update's platform step (0x00513CD8..0x00513E2A, M4-019) clears +0x34A: here the
    /// robot's footprint still intersects the located charger's quad after the contacts drop, so the engine tick leaves the flag set, and it clears once the footprint is clear of it.
    /// </summary>
    [Fact]
    public void M13_017_TheBehaviourIsRunnableOnThePlatformFlagNotTheContacts()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        WorldCharger(rig, 0, 0);                                        // the charger quad covers x in [0, 96], y in [-40, 40] (0x004E9A50)
        rig.X = 30; rig.Y = 0; rig.Angle = 0;
        var ctx = Ctx(rig);
        var b = new DriveOffChargerBehavior(rig.M, "DriveOffCharger", 60) { ActionTaskRunner = SignalTestContext.Schedule };
        b.WorkPosted += signals_rig.Notify;
        Assert.False(Runnable(b, ctx));                                 // neither flag
        rig.OnCharger = true; rig.State();
        Assert.True(rig.Robot.Sensors.OnCharger); Assert.True(rig.Robot.Sensors.OnChargerPlatform);
        Assert.True(Runnable(b, ctx));
        rig.OnCharger = false; rig.State(); rig.Tick();                 // the contacts drop; the footprint still intersects the charger: +0x34A stays set
        Assert.False(rig.Robot.Sensors.OnCharger); Assert.True(rig.Robot.Sensors.OnChargerPlatform);
        Assert.True(Runnable(b, ctx));                                  // the contacts-gated behaviour would say false here
        rig.X = -80; rig.State(); rig.Tick();                           // the footprint (x in [-135.9, -57.9]) is clear of the charger: Robot::Update clears the flag
        Assert.False(rig.Robot.Sensors.OnChargerPlatform);
        Assert.False(Runnable(b, ctx));
    }

    /// <summary>
    /// M13-017, UpdateInternal 0x005C0DA8 and TransitionToDrivingForward 0x005C0BB8: while +0x34A stays set and no action is current, UpdateInternal starts the drive again
    /// (0x005C0E0E..0x005C0E18) and returns 1; there is no timeout of the behaviour's own (the 5 s host wait is gone). Driven through the behaviour's live Start/Update entry: with the platform flag never
    /// cleared the behaviour is still running after several drives and a minute of its own clock.
    /// </summary>
    [Fact]
    public void M13_017_WhileThePlatformFlagStaysSetTheBehaviourKeepsDrivingAndNeverTimesOut()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.Angle = (float)Math.PI;
        rig.OnCharger = true; rig.State();                               // no charger in the rig's physics: the contacts stay on, so SetOnChargerPlatform(false) leaves +0x34A set
        var ctx = Ctx(rig);
        var b = new DriveOffChargerBehavior(rig.M, "DriveOffCharger", 60) { ActionTaskRunner = SignalTestContext.Schedule };
        b.WorkPosted += signals_rig.Notify;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        double t = 0;

        while (rig.Sent.OfType<AppendPathSegmentLine>().Count() < 2)
        {
            Assert.True(b.Update(ctx, t));
            rig.Pump();
            t += 33;
            SignalTestContext.AdvanceBehavior(b);
        }
        Assert.True(rig.Sent.OfType<AppendPathSegmentLine>().Count() >= 2, "the behaviour did not start a second drive while +0x34A stayed set");
        t = 60_000;                                                      // a minute of the behaviour's own clock
        Assert.True(b.Update(ctx, t));                                   // still running: no host timeout ends it
        Assert.Contains(b.Trace, l => l.Contains("ToState:DrivingForward"));
    }

    // ------------------------------------------------------------------ M13-015: the completion lambda

    private static void Complete(PopAWheelieBehavior b, uint objectId, ActionResult r) =>
        typeof(PopAWheelieBehavior).GetMethod("OnActionComplete", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(b, new object[] { objectId, r });

    /// <summary>
    /// M13-015, the completion lambda 0x005C7CBC: category 3 (Abort), or category 4 with the retry used, logs "BehaviorPopAWheelie.FailedAbort" and calls
    /// <c>SetFailedToUse(obj, 3)</c> only when the object at +0x11C is still in the world (<c>GetLocatedObjectByIdHelper</c>, 0x005C7D8C..0x005C7D9E); any other category logs
    /// "BehaviorPopAWheelie.FailedPopAction" and marks nothing; category 4 with no retry used goes to SetupRetryAction.
    /// </summary>
    [Fact]
    public void M13_015_FailureMarksTheCubeOnlyWhenItIsStillInTheWorldAndOnlyForAbortAndSpentRetry()
    {
        if (MarkerLibrary.EmbeddedOrNull is null) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.Cube = new Pose3d(Mat3.AboutZ(0), new Vec3(220, 0, 22));
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var ctx = Ctx(rig);
        PopAWheelieBehavior Fresh() { var f = new PopAWheelieBehavior(rig.M, "PopAWheelie"); f.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult(); return f; }
        // category 3 (Abort) with the cube present: marked, kind 3
        var a = Fresh();
        Complete(a, obj.ObjectId, ActionResult.Abort);
        Assert.True(rig.M.Whiteboard.DidFailToUse(obj.ObjectId, ObjectActionFailure.RollOrPopAWheelie, 1000));
        Assert.Contains(a.Trace, l => l.Contains("BehaviorPopAWheelie.FailedAbort"));
        // the same result for an object that is not in the world: logged, not marked
        Assert.False(rig.M.Whiteboard.DidFailToUse(4242, ObjectActionFailure.Any, 1000));
        Complete(Fresh(), 4242, ActionResult.Abort);
        Assert.False(rig.M.Whiteboard.DidFailToUse(4242, ObjectActionFailure.Any, 1000));
        // category 2 (cancelled) goes to the third arm: no marking
        var id = obj.ObjectId;
        var c = Fresh();
        rig.M.Whiteboard.OnRobotDelocalized();                                  // clears the failure table
        Complete(c, id, ActionResult.CancelledWhileRunning);
        Assert.False(rig.M.Whiteboard.DidFailToUse(id, ObjectActionFailure.Any, 1000));
        Assert.Contains(c.Trace, l => l.Contains("BehaviorPopAWheelie.FailedPopAction"));
        // category 4 with no retry used goes to SetupRetryAction (a retry animation, not a marking)
        var d = Fresh();
        Complete(d, id, ActionResult.DidNotReachPreActionPose);
        Assert.False(rig.M.Whiteboard.DidFailToUse(id, ObjectActionFailure.Any, 1000));
        Assert.Equal(PopAWheelieBehavior.Phase.Retrying, d.CurrentPhase);
    }

    /// <summary>
    /// M13-015, the success arm 0x005C7CF6..0x005C7E14: +0x128 = -1, the 0x21C animation is started first, THEN BehaviorObjectiveAchieved(0x16) and NeedActionCompleted. The trace order shows it.
    /// </summary>
    [Fact]
    public void M13_015_SuccessStartsTheAnimationBeforeTheObjectiveAndTheNeedsAction()
    {
        if (MarkerLibrary.EmbeddedOrNull is null) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.Cube = new Pose3d(Mat3.AboutZ(0), new Vec3(220, 0, 22));
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var ctx = Ctx(rig);
        var b = new PopAWheelieBehavior(rig.M, "PopAWheelie");
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Complete(b, obj.ObjectId, ActionResult.Success);
        Assert.True(b.Succeeded);
        Assert.Equal(-1, b.Field0x128);
        int anim = b.Trace.ToList().FindIndex(l => l.Contains("SuccessfulWheelie"));
        int objective = b.Trace.ToList().FindIndex(l => l.Contains("PoppedWheelie"));
        Assert.True(anim >= 0 && objective > anim, string.Join(" | ", b.Trace));
    }

    // ------------------------------------------------------------------ M4-019 / M13-025 / M13-024: Robot::Update's charger-platform step

    private static Point2f[] Pts(params uint[] bits) { var p = new Point2f[bits.Length / 2]; for (int i = 0; i < p.Length; i++) p[i] = new Point2f(F(bits[2 * i]), F(bits[2 * i + 1])); return p; }

    /// <summary>
    /// M13-024: cv::minAreaRect + RotatedRect::points over float points, bit for bit. Expected words come from re-analysis/evidence/m13-minarearect/port.py (the float32 port the inventory cites,
    /// validated bit for bit against the real code), run on fixed points (random.seed(11) draws), not from this implementation.
    /// </summary>
    [Fact]
    public void M13_024_MinAreaRectAndItsPointsMatchTheValidatedFloat32PortBitForBit()
    {
        var pts1 = Pts(0xBEF3D112, 0x3F19046E, 0x4087BF55, 0xBEAFDF24, 0x3DA096DB, 0x3F5FB485, 0xC049D140, 0x3DF3E391, 0x3FA63FF8, 0x403B8155);
        var r1 = MinAreaRectF.Compute(pts1);
        Assert.Equal(new[] { 0x3F23FDC8u, 0x3FB692E3u, 0x40ED1E5Cu, 0x40455232u, 0xC0651109u }, new[] { Bits(r1.Cx), Bits(r1.Cy), Bits(r1.W), Bits(r1.H), Bits(r1.AngleDeg) });
        var p1 = MinAreaRectF.Points(r1);
        Assert.Equal(new[] { 0xC03D7FC7u, 0x404C8EC8u, 0xC049D141u, 0x3DF3E390u, 0x4087BF56u, 0xBEAFDF28u, 0x408DE812u, 0x402EF3C6u }, p1.SelectMany(p => new[] { Bits(p.X), Bits(p.Y) }).ToArray());

        var pts2 = Pts(0xC081E168, 0xBFFBA579, 0xC082FC45, 0x40462C29, 0x3FF799EC, 0xC092992A, 0x409A4D49, 0x4094B8F5, 0x3FC50556, 0x3F93EB96, 0xC05B342B, 0xC09B3324, 0x3E914FE5, 0xC08CF193);
        var r2 = MinAreaRectF.Compute(pts2);
        Assert.Equal(new[] { 0x3EE8B6B0u, 0xBDE85300u, 0x41112929u, 0x41133699u, 0xC2AE41F4u }, new[] { Bits(r2.Cx), Bits(r2.Cy), Bits(r2.W), Bits(r2.H), Bits(r2.AngleDeg) });
        var p2 = MinAreaRectF.Points(r2);
        Assert.Equal(new[] { 0x409A4D48u, 0x4094B8F6u, 0xC08BC14Cu, 0x4085F978u, 0xC07A6CE4u, 0xC09BFB8Eu, 0x40A8D822u, 0xC08D3C10u }, p2.SelectMany(p => new[] { Bits(p.X), Bits(p.Y) }).ToArray());
    }

    /// <summary>
    /// M13-025: the charger's GetBoundingQuadXY (slot +0x50 = Vision::ObservableObject::GetBoundingQuadXY 0x0087713A over Charger::GetCanonicalCorners 0x004E9A50, padding 0): the eight corners
    /// (x 0/96, y -40/40, z 0/31) rotated x' = (x*m0 + y*m1) + z*m2, y' = (x*m3 + y*m4) + z*m5, cv::minAreaRect, RotatedRect::points, SortCornersClockwise, then the pose's x, y. Expected words are
    /// the independent float32 emulation in the scratch generator of the same formulas on top of port.py (the cited M13-024 port), for four yaws.
    /// </summary>
    [Fact]
    public void M13_025_TheChargerQuadIsTheMinAreaRectOfItsRotatedCanonicalCornersBitForBit()
    {
        var cases = new (double Yaw, double Tx, double Ty, uint[] Quad)[]
        {
            (0.0, 100.0, 50.0, new[] { 0x42C80000u, 0x41200000u, 0x42C80000u, 0x42B40000u, 0x43440000u, 0x41200000u, 0x43440000u, 0x42B40000u }),
            (0.3, -20.0, 10.0, new[] { 0xC102DDF8u, 0xC1E1B528u, 0xC1FE9104u, 0x4240DA95u, 0x42A710F3u, 0x3E203F00u, 0x426F90E2u, 0x42992AB4u }),
            (3.0, 250.0, -75.0, new[] { 0x431550E0u, 0xC2CA1AB8u, 0x43209B03u, 0xC1AED284u, 0x43745AEEu, 0xC2E5330Cu, 0x437FA511u, 0xC20D99EAu }),
            (-1.2, 0.0, 0.0, new[] { 0xC01FB1A0u, 0xC2CFF0AAu, 0xC2152054u, 0xC167E8BCu, 0x429022C4u, 0xC295F67Eu, 0x4215204Eu, 0x4167E8ACu }),
        };
        foreach (var (yaw, tx, ty, expected) in cases)
        {
            var q = ObjectFootprint.ChargerBoundingQuadXY(new Pose3d(Mat3.AboutZ(yaw), new Vec3(tx, ty, 0)), 0f);
            var got = Enumerable.Range(0, 4).SelectMany(i => new[] { Bits(q[i].X), Bits(q[i].Y) }).ToArray();
            Assert.True(expected.SequenceEqual(got), $"yaw {yaw}: " + string.Join(",", got.Select(g => "0x" + g.ToString("X8"))));
        }
        // the canonical corners are the listing's words: x 0x42C00000 (96), y 0xC2200000 / 0x42200000 (-40, 40), z 0x41F80000 (31), in 0x004E9A50's order
        Assert.Equal(8, ObjectFootprint.ChargerCanonicalCorners.Count);
        Assert.Equal(new[] { 0x42C00000u, 0xC2200000u, 0u }, new[] { Bits(ObjectFootprint.ChargerCanonicalCorners[0].X), Bits(ObjectFootprint.ChargerCanonicalCorners[0].Y), Bits(ObjectFootprint.ChargerCanonicalCorners[0].Z) });
        Assert.Equal(new[] { 0x42C00000u, 0x42200000u, 0x41F80000u }, new[] { Bits(ObjectFootprint.ChargerCanonicalCorners[7].X), Bits(ObjectFootprint.ChargerCanonicalCorners[7].Y), Bits(ObjectFootprint.ChargerCanonicalCorners[7].Z) });
        // the padding rule (0x0087716C..0x008771F6): each coordinate gets +padding, or -padding when its sign bit is set
        var square = new (float X, float Y, float Z)[] { (1f, 1f, 1f), (1f, -1f, 1f), (-1f, 1f, -1f), (-1f, -1f, -1f) };
        var padded = ObjectFootprint.ObservableObjectBoundingQuadXY(square, new Pose3d(Mat3.Identity, new Vec3(0, 0, 0)), 0.5f);
        for (int i = 0; i < 4; i++) { Assert.Equal(1.5f, Math.Abs(padded[i].X)); Assert.Equal(1.5f, Math.Abs(padded[i].Y)); }
    }

    /// <summary>
    /// M4-019 / M13-003: Robot::GetBoundingQuadXY(pose, 0) 0x00514D74: the canonical quad (22.1, -27.1), (22.1, 27.1), (-55.9, -27.1), (-55.9, 27.1) (0x41B0CCCC, 0xC1D8CCCD, 0x41D8CCCD, 0xC25F999A) rotated by
    /// RotationMatrix2d(GetAngleAroundZaxis) and translated by the pose's x, y (0x004E68CC, float adds). At heading 0 the words are exact.
    /// </summary>
    [Fact]
    public void M4_019_TheRobotQuadAtHeadingZeroIsTheCanonicalQuadPlusThePose()
    {
        var q = ObjectFootprint.RobotBoundingQuadXY(At(30, 5, 0), 0f);
        float x1 = F(0x41B0CCCC), x2 = F(0xC25F999A), y1 = F(0xC1D8CCCD), y2 = F(0x41D8CCCD);
        var expected = new[] { (30f + x1, 5f + y1), (30f + x1, 5f + y2), (30f + x2, 5f + y1), (30f + x2, 5f + y2) };
        for (int i = 0; i < 4; i++) { Assert.Equal(Bits(expected[i].Item1), Bits(q[i].X)); Assert.Equal(Bits(expected[i].Item2), Bits(q[i].Y)); }
    }

    /// <summary>
    /// M4-019, Robot::Update 0x00513CD8..0x00513E2A through the engine tick (Engine.Tick, the live entry): with the platform flag set, no located charger clears it (0x00513E26), a located charger
    /// whose quad the robot's footprint intersects leaves it (0x00513E1A cbnz), and one it does not intersect clears it (0x00513E20). The boundary is the quad geometry: the charger covers x in [0, 96],
    /// the robot's front edge is x + 22.1f (0x41B0CCCC), so a robot at x = -22.0 touches it and one at x = -22.3 does not.
    /// </summary>
    [Fact]
    public void M4_019_TheEngineTickClearsThePlatformFlagByTheEnginesRule()
    {
        // no located charger: cleared (the contacts are off, so SetOnChargerPlatform(false) gives false)
        using (var rig = new Rig())
        {
            rig.OnCharger = true; rig.State();
            Assert.True(rig.Robot.Sensors.OnChargerPlatform);
            rig.OnCharger = false; rig.State(); rig.Tick();
            Assert.False(rig.Robot.Sensors.OnChargerPlatform);
        }
        // the boundary: intersecting leaves the flag, clear of it clears
        foreach (var (x, stays) in new[] { (-22.0, true), (-22.3, false), (50.0, true), (90.0, true) })
        {
            using var signals_rig = SignalTestContext.Install();
            using var rig = new Rig();
            WorldCharger(rig, 0, 0);
            rig.X = (float)x; rig.Y = 0; rig.Angle = 0;
            rig.OnCharger = true; rig.State();
            Assert.True(rig.Robot.Sensors.OnChargerPlatform);
            rig.OnCharger = false; rig.State(); rig.Tick();
            Assert.Equal(stays, rig.Robot.Sensors.OnChargerPlatform);
        }
        // the contacts flag still wins: SetOnChargerPlatform(false) is (arg || +0x338), so a robot on the contacts keeps the platform flag whatever the geometry says
        using (var rig = new Rig())
        {
            WorldCharger(rig, 0, 0);
            rig.X = -300; rig.Y = 0; rig.Angle = 0;
            rig.OnCharger = true; rig.State(); rig.Tick();
            Assert.True(rig.Robot.Sensors.OnCharger); Assert.True(rig.Robot.Sensors.OnChargerPlatform);
        }
    }

    /// <summary>
    /// M4-019, the gate 0x00513CE2 (<c>ldrb [+0x355]; bne 0x00513E84</c>): while the robot is not OnTreads the step does not run, so a set platform flag is left alone even with no charger located.
    /// </summary>
    [Fact]
    public void M4_019_TheStepDoesNotRunWhileTheRobotIsOffItsTreads()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.Robot.Sensors.SetOnChargerPlatform(true);
        Assert.True(rig.Robot.Sensors.OnChargerPlatform);
        var sensors = rig.Robot.Sensors;
        // the classifier's committed state: settle, then lift the robot
        for (int i = 0; i < 40; i++) rig.State();
        for (int i = 0; i < 10; i++) rig.State((uint)RobotStatusFlag.IsPickedUp);
        Assert.Equal(OffTreadsState.InAir, sensors.OffTreadsState);
        sensors.SetOnChargerPlatform(true);                              // (the classifier's own clear on leaving OnTreads ran when it committed)
        rig.Tick();
        Assert.True(sensors.OnChargerPlatform);                          // no located charger, but not on the treads: the step is skipped
        for (int i = 0; i < 40; i++) rig.State();
        Assert.Equal(OffTreadsState.OnTreads, sensors.OffTreadsState);
    }

    /// <summary>
    /// M4-019 / M13-017: the whole loop through the live entries. The behaviour keeps redriving while the engine tick leaves the platform flag set (the robot still overlaps the charger) and stops once
    /// the tick clears it: the redrive-forever hazard ends with Robot::Update's own rule.
    /// </summary>
    [Fact]
    public void M4_019_M13_017_TheBehaviourStopsRedrivingOnceTheEngineTickClearsThePlatformFlag()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        WorldCharger(rig, 0, 0);
        rig.X = 30; rig.Y = 0; rig.Angle = 0;
        rig.OnCharger = true; rig.State();
        var ctx = Ctx(rig);
        var b = new DriveOffChargerBehavior(rig.M, "DriveOffCharger", 60) { ActionTaskRunner = SignalTestContext.Schedule };
        b.WorkPosted += signals_rig.Notify;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        rig.OnCharger = false; rig.State(); rig.Tick();                  // contacts gone, the footprint still overlaps: flag set
        Assert.True(rig.Robot.Sensors.OnChargerPlatform);
        Assert.True(b.Update(ctx, 0));                                   // still driving
        rig.X = -80; rig.State(); rig.Tick();                            // the robot is clear of the charger: the engine tick clears the flag
        Assert.False(rig.Robot.Sensors.OnChargerPlatform);

        double t = 33;
        while (b.Update(ctx, t)) { rig.Pump(); SignalTestContext.AdvanceBehavior(b); t += 33; }
        Assert.False(b.Update(ctx, t));                                  // finished: UpdateInternal's platform-clear branch (0x005C0DB0..0x005C0DF2) ends it, not a timeout
    }

    /// <summary>
    /// M13-016: AlignWithObjectAction::SelectDockAction 0x005534CC..0x005534DE: the dock action byte is 0xB when the alignment type byte at +0xF8 is 1, else 0xA. The mount's own alignment (type 3, Custom)
    /// is 0xA.
    /// </summary>
    [Fact]
    public void M13_016_SelectDockActionIsAlignSpecialForAlignmentTypeOneAndAlignOtherwise()
    {
        static DockAction? Select(AlignmentType t) =>
            (DockAction?)typeof(AlignWithObjectAction).GetMethod("SelectDockAction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(new AlignWithObjectAction(null!, 0, 0f, t), new object?[] { null });
        Assert.Equal((byte)0xB, (byte)Select(AlignmentType.LiftPlate)!.Value);
        Assert.Equal((byte)0xA, (byte)Select(AlignmentType.LiftFinger)!.Value);
        Assert.Equal((byte)0xA, (byte)Select(AlignmentType.Body)!.Value);
        Assert.Equal((byte)0xA, (byte)Select(AlignmentType.Custom)!.Value);
        Assert.Equal((byte)0xA, (byte)Select((AlignmentType)9)!.Value);
    }

    // ------------------------------------------------------------------ the verifier's blocking findings

    /// <summary>
    /// M13-018, ComputePath 0x008586A0..: the goal-id hash (planner+0x10) is filled from the CheckContextGoals output (0x00858760..0x00858780) BEFORE InitializeHeuristic, which prunes only planner+4,
    /// +0x24 and +0xA0 (0x008598DC..0x00859AB6). A goal whose collision expansion costs more than 1000.0f is pruned from the goal list, but its state still ends the search when popped: the search
    /// starts on that very state, which is popped first, so the plan is empty and the chosen goal is the pruned one's id (0).
    /// </summary>
    [Fact]
    public void M13_018_APrunedGoalStillEndsTheSearchWhenItIsPopped()
    {
        var prims = Prims();
        if (prims is null) return;
        var env = new LatticeEnvironment(prims);
        env.AddRectangleObstacle(At(150, 0, 0), 44, 44, "cube", 999f);                  // a soft obstacle (< 1000): the goal inside it is not dropped by CheckContextGoals
        var planner = new LatticePlanner(env);
        var plan = planner.ComputePath(new StateC(150, 0, 0), new[] { new StateC(150, 0, 0), new StateC(0, 60, 0) });
        Assert.Equal(2, planner.GoalIdMapForTests.Count);                                 // both goals are in the hash although the heuristic pruned the first
        var goalStates = (System.Collections.ICollection)typeof(LatticePlanner).GetField("_goalStates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(planner)!;
        Assert.Equal(1, goalStates.Count);                                                // InitializeHeuristic really pruned the soft goal (cost > 1000.0f)
        Assert.NotNull(plan);
        Assert.Empty(plan!.Actions);
        Assert.Equal(0, planner.ChosenGoalId);
    }

    /// <summary>
    /// M13-018, BuildPlan 0x0085A5D8: the loop's counter is tested after each step (0x0085A610..0x0085A616, <c>cmp r5,#0x3e8; blo</c>), so the 1000th step falls to the error
    /// (0x0085A634, sErrorF) and the plan built so far is still reversed and kept (0x0085A672..): a 6000-cell straight line (1200 long-straight steps) gives a plan of exactly 1000 steps and the log line.
    /// </summary>
    [Fact]
    public void M13_018_BuildPlanLogsAfterTheThousandthStepAndKeepsThePlan()
    {
        var prims = Prims();
        if (prims is null) return;
        var env = new LatticeEnvironment(prims);
        var lines = new List<string>();
        env.Log += lines.Add;
        var plan = new LatticePlanner(env).ComputePath(new StateC(0, 0, 0), new[] { new StateC(60000, 0, 0) });
        Assert.NotNull(plan);
        Assert.Equal(1000, plan!.Actions.Count);
        Assert.Contains(lines, l => l.Contains("BuildPlan"));
    }

    /// <summary>
    /// M13-018, DoPlanning 0x00500090: a cleared run flag ends the sleep loop (0x005000BE <c>cbz r0, 0x005000EC</c>) and Replan is still called, so the planner runs its goal validation (the goal hash is
    /// built) and then fails at the first pop's flag test (0x00858886..0x00858890) with result 0.
    /// </summary>
    [Fact]
    public void M13_018_ADoPlanningWithTheRunFlagClearedStillReachesReplan()
    {
        var prims = Prims();
        if (prims is null) return;
        var planner = new LatticePlanner(new LatticeEnvironment(prims)) { ArtificialPlannerDelayMs = 50 };
        planner.StopPlanning();
        var result = planner.DoPlanning(new StateC(0, 0, 0), new[] { new StateC(100, 0, 0) }, out var plan);
        Assert.Equal(LatticePlanner.PlanningResult.Failure, result);
        Assert.Null(plan);
        Assert.Single(planner.GoalIdMapForTests);                                         // ComputePath was reached
    }

    /// <summary>
    /// M13-003, FastPolygon::Contains 0x00841BA4..0x00841C66: the edge loop's <c>ble 0x00841C24</c> is taken for an unordered compare too, so an edge whose normal is NaN (a zero-length edge from a
    /// duplicated vertex: 0/0) is skipped and the point is judged by the other edges.
    /// </summary>
    [Fact]
    public void M13_003_FastPolygonContainsContinuesPastANaNDot()
    {
        var poly = new FastPolygonF(new[] { new P2f(-5, -5), new P2f(-5, 5), new P2f(-5, 5), new P2f(15, 5), new P2f(15, -5) });
        Assert.True(poly.Contains(5f, 0f));
        Assert.False(poly.Contains(5f, 8f));
    }

    private sealed class RunningDrive : IDriveStraightTick
    {
        public uint Init() => 0;
        public uint CheckIfDone() => (uint)ActionResult.Running;
        public void Cancel() { }
    }

    /// <summary>
    /// M13-008 / M4-016: the IAction timeouts on the engine clock, tested before CheckIfDone (IAction::UpdateInternal 0x00540D90..0x00540DAA, failure 0x03000018 at 0x00540E7C): BackupOntoChargerAction's
    /// vtable slot +0x2c is 0x0054E97B (body 0x0054E97A <c>movt r0,#0x40A0</c> = 5.0f); the rest of the mount (turn, lift, align, drive straight, MountChargerAction itself) uses 0x0052B0C2 = 30.0f.
    /// </summary>
    [Fact]
    public void M13_008_TheReverseTimesOutAtFiveSecondsAndTheOthersAtThirtyOnTheEngineClock()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        float now = 0f;
        var mount = new MountChargerAction(rig.M, 100) { Clock = () => now, DriveFactory = (d, s, c) => new RunningDrive() };
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var backup = (Task<uint>)typeof(MountChargerAction).GetMethod("RunBackupAsync", flags)!.Invoke(mount, new object[] { CancellationToken.None })!;
        SignalTestContext.StepContinuation();
        Assert.False(backup.IsCompleted);
        now = 4.9f; SignalTestContext.StepContinuation();
        Assert.False(backup.IsCompleted);                                                 // 4.9 < 5.0
        now = 5.0f;
        SignalTestContext.Run(backup);
        Assert.True(backup.IsCompleted);
        Assert.Equal(0x03000018u, backup.Result);                                         // now >= start + 5.0f
        Assert.Equal(5.0f, F(0x40A00000)); Assert.Equal(MountChargerAction.BackupTimeoutSec, F(0x40A00000));
        Assert.Equal(30.0f, F(0x41F00000)); Assert.Equal(MountChargerAction.DefaultActionTimeoutSec, F(0x41F00000));
        // the others: 30.0f (the same RunToEnd with the default slot)
        now = 0f;
        var run = (Task<uint>)typeof(MountChargerAction).GetMethod("RunToEnd", flags)!.Invoke(mount, new object[] { new Func<uint>(() => (uint)ActionResult.Running), CancellationToken.None, MountChargerAction.DefaultActionTimeoutSec })!;
        now = 29.9f; SignalTestContext.StepContinuation();
        Assert.False(run.IsCompleted);
        now = 30.0f;
        SignalTestContext.Run(run);
        Assert.True(run.IsCompleted);
        Assert.Equal(0x03000018u, run.Result);
    }

    /// <summary>
    /// M13-013 / M4-016: DriveOffChargerContactsAction's vtable +0x2c is 0x0052B0C2 (30.0f): while its drive is RUNNING the action fails 0x03000018 once the engine clock reaches start + 30.0f.
    /// </summary>
    [Fact]
    public void M13_013_TheContactsActionTimesOutAtThirtySeconds()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.OnCharger = true; rig.State();
        float now = 0f;
        var action = new DriveOffChargerContactsAction(rig.M, () => false, new RunningDrive()) { Clock = () => now };
        var task = action.RunAsync(CancellationToken.None);
        now = 29.9f; SignalTestContext.StepContinuation();
        Assert.False(task.IsCompleted);
        now = 30.0f;
        SignalTestContext.Run(task);
        Assert.True(task.IsCompleted);
        Assert.Equal((ActionResult)0x03000018, task.Result);
    }

    /// <summary>
    /// M13-017: whiteboard +0x44 is a float written only by BehaviorDriveOffCharger::UpdateInternal (0x005C0E02..0x005C0E08) and read by IsRunnableBase 0x005BD93C. The reader in float
    /// (0x005BD826..0x005BD962): a window below -1e-5f (0xB727C5AC) passes, a never-stamped board (-1.0f, here null) fails, and otherwise (window + stamp) + 1e-5f (0x3727C5AC) must be >= now.
    /// </summary>
    [Fact]
    public void M13_017_TheDriveOffStampIsAFloatReadByTheFloatRule()
    {
        var f = typeof(Cozmo.Robot.Behavior.SteppedBehavior).Assembly.GetTypes().SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)).Single(x => x.Name == "DriveOffWithin");
        bool Within(double? window, float? stamp, double now) => (bool)f.Invoke(null, new object?[] { window, stamp, now })!;
        Assert.True(Within(null, null, 100));                                              // no window
        Assert.True(Within(-1.0, null, 100));                                              // -1 < -1e-5f
        Assert.False(Within(10.0, null, 100));                                             // never stamped: -1.0f on the whiteboard
        Assert.True(Within(10.0, 100f, 110.0));                                            // (10 + 100) + 1e-5 >= 110
        Assert.False(Within(10.0, 100f, 110.001));
        Assert.True(Within(0.0, 100f, 100.0));                                             // a zero window is not below -1e-5f
        // the float word of the added epsilon: 110 + 1e-5f is still 110.00001 in single precision
        Assert.Equal(0x3727C5ACu, Bits(1e-5f));
        Assert.Equal(typeof(float?), typeof(BehaviorContext).GetProperty("LastDriveOffChargerSec")!.PropertyType);
    }

    /// <summary>
    /// M13-008 / M4-016, the nested IAction timeouts through the mount's own RunAsync on the engine clock seam: IAction::UpdateInternal tests <c>now &gt;= start + slot</c> on every Update before anything
    /// else (0x00540D90..0x00540DAA), and the slot (vtable +0x2c, entry 0x0052B0C3) is 30.0f for MountChargerAction, AlignWithObjectAction, MoveHeadToAngleAction and MoveLiftToHeightAction. A stage
    /// that has not finished when the clock reaches start + 30 s ends the mount with 0x03000018, whichever stage it is in: the align (a 40 s align), the head move and the lift move.
    /// </summary>
    [Theory]
    [InlineData("align")]
    [InlineData("head")]
    [InlineData("lift")]
    public void M13_008_EachStageOfTheMountEndsWithTheTimeoutWhenItOutlastsTheThirtySecondSlot(string stage)
    {
        if (MarkerLibrary.EmbeddedOrNull is null) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.Head = -0.2f;
        rig.Charger = At(200, 0, 0);
        Assert.Single(rig.Frame().Objects);
        rig.DockOutcome = BlockStatus.NoBlock;
        rig.M.World.UnobservedMissesToUnknown = int.MaxValue;
        if (stage == "head") rig.HoldHead = true;
        if (stage == "lift") { rig.HoldLift = true; rig.LiftAngleReported = (float)Math.Asin((32.0 - 45.0) / 66.0); rig.State(); }
        float now = 0f;
        var mount = new MountChargerAction(rig.M, ChargerGeometry.ObjectId) { Clock = () => now };
        var task = mount.RunAsync(default);

        bool Reached() => stage switch
        {
            "align" => true,
            "head" => rig.Sent.OfType<SetHeadAngle>().Any(h => h.AngleRad == 0f),
            _ => rig.LiftHeights.Contains(45f),
        };
        if (stage == "align") SignalTestContext.StepContinuation();                                     // nothing is pumped: the align is still in flight
        else SignalTestContext.Until(() => !(!Reached() && !task.IsCompleted), () => { rig.Pump(); if (!rig.OnCharger) rig.Frame(); });
        Assert.False(task.IsCompleted, $"the mount ended before the {stage} stage: {task.Status} {string.Join(" | ", mount.Trace)}");
        now = 29.9f; SignalTestContext.StepContinuation();
        Assert.False(task.IsCompleted);                                                // 29.9 < 30.0: still inside the slot
        now = 40f;                                                                     // a 40 s stage
        SignalTestContext.Run(task);
        Assert.True(task.IsCompleted, $"no timeout in the {stage} stage");
        Assert.Equal((ActionResult)0x03000018, task.Result);
    }
}
