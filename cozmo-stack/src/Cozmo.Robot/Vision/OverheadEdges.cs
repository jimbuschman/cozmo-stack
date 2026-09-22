using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

/// <summary>
/// <c>Anki::Cozmo::GroundPlaneROI</c>: the patch of floor in front of the robot the overhead-edge
/// detector looks at. Its four static members are at 0x00C48F60..0x00C48F6C - <c>_dist</c> 40,
/// <c>_length</c> 150, <c>_widthFar</c> 150, <c>_widthClose</c> 40 - and <c>GetGroundQuad</c> 0x004F7774
/// builds the trapezoid they describe in robot coordinates: from 40 mm ahead, forty wide, out to 190 mm
/// ahead, a hundred and fifty wide.
/// </summary>
public static class GroundPlaneROI
{
    public const double DistMm = 40.0;
    public const double LengthMm = 150.0;
    public const double WidthFarMm = 150.0;
    public const double WidthCloseMm = 40.0;

    /// <summary>The trapezoid in robot coordinates (x forward, y left), far edge first: far left, close left, far right, close right - the engine's <c>Quadrilateral</c> order, with "up" being away from the robot.</summary>
    public static Vec2[] GroundQuad() => new[]
    {
        new Vec2(DistMm + LengthMm,  WidthFarMm / 2),
        new Vec2(DistMm,             WidthCloseMm / 2),
        new Vec2(DistMm + LengthMm, -WidthFarMm / 2),
        new Vec2(DistMm,            -WidthCloseMm / 2),
    };
}

/// <summary>
/// One point of a chain: where the ray through that image column meets the ground, in robot
/// coordinates, and the filter response that put it there. The engine's <c>OverheadEdgePoint</c> is the
/// same twenty bytes - a <c>Point2f</c> and a colour it fills with the derivative value three times over
/// (0x006AD21C).
/// </summary>
public readonly record struct OverheadEdgePoint(Vec2 Ground, double Gradient);

/// <summary>
/// <c>Anki::Cozmo::OverheadEdgePointChain</c>: a run of points that are all edge points or all clear
/// reports. <c>IsBorder</c> is the engine's flag at the chain's +0xC.
/// </summary>
public sealed class OverheadEdgePointChain
{
    public bool IsBorder { get; init; }
    public List<OverheadEdgePoint> Points { get; } = new();
}

/// <summary>
/// <c>Anki::Cozmo::OverheadEdgeFrame</c>: what one image of the ground produced. The layout the map
/// component reads is the timestamp at +0, the valid flag at +4 (<c>ProcessVisionOverheadEdges</c>
/// 0x0067F7AC does nothing but erase the last drawing when it is clear), the visible ground quad at
/// +8..+0x20 and the chains at +0x28.
/// </summary>
public sealed record OverheadEdgeFrame(uint Timestamp, bool GroundPlaneValid, Vec2[] GroundQuad,
                                       IReadOnlyList<OverheadEdgePointChain> Chains);

/// <summary>
/// <c>Anki::Cozmo::OverheadEdgesDetector::Detect</c> 0x006ABE34. The engine's three profiled stages are
/// EdgeDetection, GroundQuadEdgeMasking and FindingGroundEdgePoints, and this follows them:
///
/// 1. the ground ROI is projected into the image (<c>GroundPlaneROI::GetImageQuad</c>) and its bounding
///    rectangle taken (<c>Rectangle&lt;int&gt;::InitFromPointContainer</c>, 0x006ABEA4);
/// 2. that rectangle is filtered with the seven-by-five kernel the engine keeps as a function-local
///    static at 0x00C8E020 - a horizontal five-tap smoothing times a vertical difference with three
///    blank rows between its halves - through <c>cv::filter2D</c> (0x006AC650);
/// 3. everything outside the ground quad is masked away (<c>fillConvexPoly</c> then <c>SetMaskTo</c>,
///    0x006AC7AE);
/// 4. the result is transposed (0x006AD0BE) and each image column is walked from the bottom of the
///    rectangle upwards for the first response whose magnitude passes the detector's threshold
///    (0x006AD1FE). The first one found is an edge point; a column with none at all reports the top of
///    the rectangle as clear, but only where the far edge of the ROI is itself in frame - the two
///    comparisons at 0x006AD23E and 0x006AD24C, against the x of the ground quad's two far corners.
///    Either way the image point becomes a ground point through the homography (0x006AE0A8: multiply,
///    then divide by z, and refuse the point when z is not positive).
///
/// Consecutive points join a chain while they are of the same kind and no more than five millimetres
/// apart (<c>0x006AE1DE</c>); anything else starts a new one.
///
/// The threshold is 50 and the fifth constructor argument 3, both from the one construction site in
/// <c>VisionSystem::VisionSystem</c> (0x006B0120).
/// </summary>
public sealed class OverheadEdgesDetector
{
    /// <summary>The <c>float</c> argument <c>VisionSystem</c> constructs the detector with (0x006B0120), kept at the detector's +0xC and compared against the absolute filter response.</summary>
    public const double EdgeThreshold = 50.0;

    /// <summary>The largest gap between consecutive points of one chain, 5 mm (0x006AE1DE).</summary>
    public const double MaxChainGapMm = 5.0;

    /// <summary>
    /// The two points on the lift the detector projects to find out whether the arm is in the way. They
    /// are function-local statics of <c>Detect</c>, initialised under their guards at 0x006ABED4 and
    /// 0x006ABF1C, and they are taken relative to the lift pose - the pose <c>ComputeLiftPose</c> gives
    /// for the frame's lift angle, whose parent is the lift base at (-41, 0, 45).
    /// </summary>
    public static readonly Vec3 LiftPointA = new(4.0, 0.0, -20.0);
    public static readonly Vec3 LiftPointB = new(-1.0, 0.0, -36.5);

    /// <summary>
    /// The engine's edge kernel, the thirty-five floats at 0x00C8E020 laid out seven rows by five
    /// columns. The three zero rows in the middle are what makes it a difference across a gap rather
    /// than a plain derivative.
    /// </summary>
    public static readonly double[,] Kernel =
    {
        {  0.0168,  0.0754,  0.1242,  0.0754,  0.0168 },
        {  0.0377,  0.1689,  0.2784,  0.1689,  0.0377 },
        {  0.0,     0.0,     0.0,     0.0,     0.0    },
        {  0.0,     0.0,     0.0,     0.0,     0.0    },
        {  0.0,     0.0,     0.0,     0.0,     0.0    },
        { -0.0377, -0.1689, -0.2784, -0.1689, -0.0377 },
        { -0.0168, -0.0754, -0.1242, -0.0754, -0.0168 },
    };

    public OverheadEdgesDetector(double threshold = EdgeThreshold) => Threshold = threshold;

    public double Threshold { get; }

    /// <summary>
    /// Runs the detector over one frame. <paramref name="camera"/> is the camera in the same frame as
    /// <paramref name="robotPose"/>; the points come back in robot coordinates, which is what
    /// <see cref="MemoryMap.AddVisionOverheadEdges"/> expects - the engine's map component transforms
    /// them by the robot's historical pose before it uses them (0x0067FC06).
    /// </summary>
    public OverheadEdgeFrame Detect(GrayImage image, CameraModel camera, Pose3d robotPose, uint timestamp = 0,
                                    double? liftAngleRad = null)
    {
        var groundQuad = GroundPlaneROI.GroundQuad();
        var imageQuad = new Vec2[4];
        for (int i = 0; i < 4; i++)
        {
            var world = robotPose.Apply(new Vec3(groundQuad[i].X, groundQuad[i].Y, 0));
            if (camera.Project(world) is not { } px) return Empty(timestamp, groundQuad);
            imageQuad[i] = px;
        }

        // image to ground, the homography the engine carries in its pose data. A homography is only
        // fixed up to scale, and the engine's test is on the sign of the third component, so the scale is
        // chosen here to make it positive over the ROI - which is what the engine's own ground-plane
        // homography does.
        var toGround = Homography.FromPoints(imageQuad, groundQuad);
        var centre = new Vec2(imageQuad.Average(p => p.X), imageQuad.Average(p => p.Y));
        if (toGround.ApplyHomogeneous(centre.X, centre.Y).W < 0)
            toGround = new Homography(toGround.H.Select(v => -v).ToArray());

        int minX = (int)Math.Floor(imageQuad.Min(p => p.X)), maxX = (int)Math.Ceiling(imageQuad.Max(p => p.X));
        int minY = (int)Math.Floor(imageQuad.Min(p => p.Y)), maxY = (int)Math.Ceiling(imageQuad.Max(p => p.Y));
        minX = Math.Max(0, minX); minY = Math.Max(0, minY);
        maxX = Math.Min(image.Width - 1, maxX); maxY = Math.Min(image.Height - 1, maxY);
        if (maxX <= minX || maxY <= minY) return Empty(timestamp, groundQuad);
        if (liftAngleRad is { } lift && !LiftIsOutOfTheWay(camera, robotPose, lift, minY, maxY))
            return Empty(timestamp, groundQuad);

        var response = Filter(image, minX, minY, maxX, maxY, imageQuad);

        // the far edge's two corners bound the columns a clear report may be made for
        double clearMinX = Math.Min(imageQuad[0].X, imageQuad[2].X);
        double clearMaxX = Math.Max(imageQuad[0].X, imageQuad[2].X);

        var chains = new List<OverheadEdgePointChain>();
        for (int x = minX; x <= maxX; x++)
        {
            int found = -1;
            for (int y = maxY; y >= minY; y--)
                if (Math.Abs(response[x - minX, y - minY]) > Threshold) { found = y; break; }

            if (found >= 0)
            {
                if (ToGround(toGround, x, found) is { } g)
                    Append(chains, new OverheadEdgePoint(g, response[x - minX, found - minY]), true);
            }
            else if (x >= clearMinX && x <= clearMaxX)
            {
                if (ToGround(toGround, x, minY) is { } g)
                    Append(chains, new OverheadEdgePoint(g, 0), false);
            }
        }
        return new OverheadEdgeFrame(timestamp, true, groundQuad, chains);
    }

    private static OverheadEdgeFrame Empty(uint timestamp, Vec2[] quad) =>
        new(timestamp, false, quad, Array.Empty<OverheadEdgePointChain>());

    /// <summary>
    /// The lift test at 0x006AC4A2..0x006ACDC8, which is a gate and not a mask: the two lift points are
    /// projected, and unless the first of them falls below the bottom of the ROI rectangle - or the
    /// second falls above its top - the whole frame is abandoned. Nothing is drawn over the image; the
    /// engine simply does not look at ground the arm is standing in front of.
    /// </summary>
    public static bool LiftIsOutOfTheWay(CameraModel camera, Pose3d robotPose, double liftAngleRad, int top, int bottom)
    {
        var lift = robotPose.Compose(LiftGeometry.LiftPoseInRobotFrame(liftAngleRad));
        var a = camera.Project(lift.Apply(LiftPointA));
        var b = camera.Project(lift.Apply(LiftPointB));
        if (a is not { } pa) return true;              // nothing projected: the arm is not in frame
        if (pa.Y > bottom) return true;                // below the ROI
        if (b is not { } pb) return false;
        return pb.Y < top;                             // or the pair straddles it from above
    }

    /// <summary>
    /// <c>0x006AE0A8</c>: the image point taken through the homography as (x, y, 1), then divided by its
    /// third component - and refused outright when that is not positive, which is the case for a column
    /// whose ray never meets the ground in front of the robot.
    /// </summary>
    private static Vec2? ToGround(Homography h, double x, double y)
    {
        var (gx, gy, w) = h.ApplyHomogeneous(x, y);
        if (w <= 0) return null;
        return new Vec2(gx / w, gy / w);
    }

    /// <summary>
    /// The EdgeDetection and GroundQuadEdgeMasking stages together: the kernel over the ROI rectangle,
    /// with everything outside the ground quad left at zero so it can never raise an edge.
    /// </summary>
    private static double[,] Filter(GrayImage image, int minX, int minY, int maxX, int maxY, Vec2[] quad)
    {
        int w = maxX - minX + 1, h = maxY - minY + 1;
        var outp = new double[w, h];
        var poly = new[] { quad[0], quad[1], quad[3], quad[2] };   // the trapezoid, once round
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!InsidePolygon(poly, new Vec2(minX + x, minY + y))) continue;
                double acc = 0;
                for (int ky = 0; ky < 7; ky++)
                    for (int kx = 0; kx < 5; kx++)
                    {
                        int sx = Reflect(minX + x + kx - 2, image.Width);
                        int sy = Reflect(minY + y + ky - 3, image.Height);
                        acc += Kernel[ky, kx] * image.Pixels[sy * image.Width + sx];
                    }
                outp[x, y] = acc;
            }
        return outp;
    }

    private static int Reflect(int i, int n)
    {
        if (n == 1) return 0;
        if (i < 0) return -i;
        if (i >= n) return 2 * n - i - 2;
        return i;
    }

    internal static bool InsidePolygon(Vec2[] poly, Vec2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if (poly[i].Y > p.Y != poly[j].Y > p.Y &&
                p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
    }

    /// <summary>
    /// The engine's chain appender at 0x006AE1B0: a point joins the last chain when that chain is of the
    /// same kind and its last point is within five millimetres; otherwise a new chain starts.
    /// </summary>
    internal static void Append(List<OverheadEdgePointChain> chains, OverheadEdgePoint point, bool isBorder)
    {
        var last = chains.Count > 0 ? chains[^1] : null;
        bool join = last is not null && last.Points.Count > 0 && last.IsBorder == isBorder
                    && (point.Ground - last.Points[^1].Ground).Length <= MaxChainGapMm;
        if (last is null || (last.Points.Count > 0 && !join))
        {
            last = new OverheadEdgePointChain { IsBorder = isBorder };
            chains.Add(last);
        }
        last.Points.Add(point);
    }
}
