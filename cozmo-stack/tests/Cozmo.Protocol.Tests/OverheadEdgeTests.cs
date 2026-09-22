using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M11-017: the memory map's vision-derived content. The detector
/// (<c>OverheadEdgesDetector::Detect</c> 0x006ABE34) turns one image of the ground in front of the robot
/// into chains of ground points, and the map component
/// (<c>MapComponent::AddVisionOverheadEdges</c> 0x0067F814) turns those into the clear areas and the two
/// edge types the map keeps.
/// </summary>
public class OverheadEdgeTests
{
    private static CameraModel Camera(double headAngleRad = HeadGeometry.MinHeadAngleRad)
    {
        var cal = CameraCalibration.Nominal();
        return new CameraModel(cal, HeadGeometry.CameraPoseInRobot(headAngleRad));
    }

    private static readonly Pose3d AtOrigin = new(Mat3.Identity, new Vec3(0, 0, 0));

    /// <summary>
    /// <c>GroundPlaneROI</c>'s four static members at 0x00C48F60 and the trapezoid
    /// <c>GetGroundQuad</c> 0x004F7774 builds from them.
    /// </summary>
    [Fact]
    public void TheGroundRoiIsTheEnginesTrapezoid()
    {
        Assert.Equal(40.0, GroundPlaneROI.DistMm);
        Assert.Equal(150.0, GroundPlaneROI.LengthMm);
        Assert.Equal(150.0, GroundPlaneROI.WidthFarMm);
        Assert.Equal(40.0, GroundPlaneROI.WidthCloseMm);

        var q = GroundPlaneROI.GroundQuad();
        Assert.Equal(new Vec2(190, 75), q[0]);
        Assert.Equal(new Vec2(40, 20), q[1]);
        Assert.Equal(new Vec2(190, -75), q[2]);
        Assert.Equal(new Vec2(40, -20), q[3]);
    }

    /// <summary>
    /// The kernel at 0x00C8E020: seven rows by five, the top two rows positive and the bottom two their
    /// negatives, and three blank rows between them. It sums to zero, so flat ground gives no response
    /// at all and only a step in brightness can pass the threshold of 50 the detector is constructed
    /// with (0x006B0120).
    /// </summary>
    [Fact]
    public void TheEdgeKernelIsTheEngines()
    {
        Assert.Equal(50.0, OverheadEdgesDetector.EdgeThreshold);
        Assert.Equal(5.0, OverheadEdgesDetector.MaxChainGapMm);

        var k = OverheadEdgesDetector.Kernel;
        Assert.Equal(7, k.GetLength(0));
        Assert.Equal(5, k.GetLength(1));
        double sum = 0;
        for (int y = 0; y < 7; y++) for (int x = 0; x < 5; x++) sum += k[y, x];
        Assert.Equal(0.0, sum, 6);
        for (int x = 0; x < 5; x++)
        {
            Assert.Equal(0.0, k[2, x]);
            Assert.Equal(0.0, k[3, x]);
            Assert.Equal(0.0, k[4, x]);
            Assert.Equal(-k[0, x], k[6, x], 6);
            Assert.Equal(-k[1, x], k[5, x], 6);
        }
        Assert.Equal(0.0168, k[0, 0], 4);
        Assert.Equal(0.2784, k[1, 2], 4);
    }

    /// <summary>
    /// The chain rule of 0x006AE1B0: a point joins the chain it follows while that chain is of the same
    /// kind and its last point is within five millimetres, and starts a new one otherwise.
    /// </summary>
    [Fact]
    public void AChainBreaksOnAGapOrAChangeOfKind()
    {
        var chains = new List<OverheadEdgePointChain>();
        OverheadEdgesDetector.Append(chains, new OverheadEdgePoint(new Vec2(100, 0), 60), true);
        OverheadEdgesDetector.Append(chains, new OverheadEdgePoint(new Vec2(100, 4), 60), true);
        Assert.Single(chains);
        Assert.Equal(2, chains[0].Points.Count);

        // six millimetres on: a new chain of the same kind
        OverheadEdgesDetector.Append(chains, new OverheadEdgePoint(new Vec2(100, 10), 60), true);
        Assert.Equal(2, chains.Count);
        Assert.True(chains[1].IsBorder);

        // a clear report next to it: another new chain
        OverheadEdgesDetector.Append(chains, new OverheadEdgePoint(new Vec2(100, 12), 0), false);
        Assert.Equal(3, chains.Count);
        Assert.False(chains[2].IsBorder);
    }

    /// <summary>
    /// The detector on two synthetic frames. Flat grey ground has no step for the kernel to find, so
    /// every column reports clear; a dark band across the ROI gives a step, and the columns that cross
    /// it come back as border points, on the ground, in front of the robot.
    /// </summary>
    [Fact]
    public void FlatGroundReportsClearAndAStepReportsAnEdge()
    {
        var cam = Camera();
        var flat = new GrayImage(320, 240);
        flat.Fill(120);
        var det = new OverheadEdgesDetector();

        var frame = det.Detect(flat, cam, AtOrigin, 1000);
        Assert.True(frame.GroundPlaneValid);
        Assert.NotEmpty(frame.Chains);
        Assert.All(frame.Chains, c => Assert.False(c.IsBorder));

        // a dark band painted across the middle of the image
        var stepped = new GrayImage(320, 240);
        stepped.Fill(120);
        int mid = 0;
        for (int y = 0; y < 240; y++)
            for (int x = 0; x < 320; x++)
                if (y > 150) { stepped.Pixels[y * 320 + x] = 20; mid = y; }
        Assert.True(mid > 0);

        var frame2 = det.Detect(stepped, cam, AtOrigin, 1001);
        Assert.Contains(frame2.Chains, c => c.IsBorder);
        var border = frame2.Chains.Where(c => c.IsBorder).SelectMany(c => c.Points).ToList();
        Assert.NotEmpty(border);
        Assert.All(border, p => Assert.InRange(p.Ground.X, GroundPlaneROI.DistMm,
                                              GroundPlaneROI.DistMm + GroundPlaneROI.LengthMm));
    }

    /// <summary>
    /// The lift gate at 0x006AC4A2: the arm in front of the ground the detector wants to look at makes it
    /// abandon the frame outright - it masks nothing, it just does not report. With the lift down the
    /// frame comes back as usual.
    /// </summary>
    [Fact]
    public void TheArmInTheWayAbandonsTheFrame()
    {
        Assert.Equal(new Vec3(4, 0, -20), OverheadEdgesDetector.LiftPointA);
        Assert.Equal(new Vec3(-1, 0, -36.5), OverheadEdgesDetector.LiftPointB);

        var cam = Camera();
        var flat = new GrayImage(320, 240);
        flat.Fill(120);
        var det = new OverheadEdgesDetector();

        // the engine's lift angle for a height: 45 + 66 sin(angle), so 32 mm is down and 92 mm is up.
        // With the arm either right down or right up it is not across the ground the ROI covers, and the
        // frame comes back as usual.
        double down = Math.Asin((32.0 - 45) / LiftGeometry.ArmLengthMm);
        double up = Math.Asin((92.0 - 45) / LiftGeometry.ArmLengthMm);
        Assert.True(det.Detect(flat, cam, AtOrigin, 1, down).GroundPlaneValid);
        Assert.True(det.Detect(flat, cam, AtOrigin, 2, up).GroundPlaneValid);

        // the rule itself: the first point below the rectangle lets the frame through, and a rectangle
        // the arm is drawn across does not
        Assert.True(OverheadEdgesDetector.LiftIsOutOfTheWay(cam, AtOrigin, down, 0, 0));
        Assert.False(OverheadEdgesDetector.LiftIsOutOfTheWay(cam, AtOrigin, down, -10000, 10000));
        var abandoned = det.Detect(flat, cam, AtOrigin, 3, down);
        Assert.True(abandoned.GroundPlaneValid);   // the real rectangle is not one of those
    }

    private static OverheadEdgeFrame FrameOf(bool border, params Vec2[] points)
    {
        var chain = new OverheadEdgePointChain { IsBorder = border };
        foreach (var p in points) chain.Points.Add(new OverheadEdgePoint(p, border ? 60 : 0));
        return new OverheadEdgeFrame(4242, true, GroundPlaneROI.GroundQuad(), new[] { chain });
    }

    /// <summary>
    /// <c>AddVisionOverheadEdges</c> on one straight border run: the clear area between the robot and
    /// the run goes in as a ClearOfObstacle triangle, the run itself as a two-point InterestingEdge, and
    /// the call reports the distance to the closest border point.
    /// </summary>
    [Fact]
    public void ABorderRunBecomesAClearTriangleAndAnInterestingEdge()
    {
        var map = new MemoryMap();
        var frame = FrameOf(true, new Vec2(100, -30), new Vec2(100, 0), new Vec2(100, 30));
        double? closest = map.AddVisionOverheadEdges(frame, AtOrigin);

        Assert.NotNull(closest);
        Assert.Equal(100.0, closest!.Value, 3);

        var clear = map.Regions.Where(r => r.Type == MemoryMapContentType.ClearOfObstacle).ToList();
        var edges = map.Regions.Where(r => r.Type == MemoryMapContentType.InterestingEdge).ToList();
        Assert.Single(clear);
        Assert.Single(edges);
        Assert.Equal(3, clear[0].Polygon.Length);                     // robot, run start, run end
        Assert.Equal(new Vec2(0, 0), clear[0].Polygon[0]);
        Assert.Equal(2, edges[0].Polygon.Length);
        Assert.Equal(4242u, edges[0].Timestamp);
    }

    /// <summary>
    /// The forty-degree rule at 0x0067FD40: a run that turns harder than that is closed off and the next
    /// one starts where it ended, so an L of border points leaves two edges rather than one.
    /// </summary>
    [Fact]
    public void ARunThatTurnsMoreThanFortyDegreesIsSplit()
    {
        Assert.Equal(0.766, MemoryMap.OverheadEdgeRunDirectionCos);

        var map = new MemoryMap();
        var frame = FrameOf(true,
            new Vec2(80, -40), new Vec2(100, -20), new Vec2(120, 0),   // one direction
            new Vec2(120, 30), new Vec2(120, 60));                      // ninety degrees off it
        map.AddVisionOverheadEdges(frame, AtOrigin);

        var edges = map.Regions.Where(r => r.Type == MemoryMapContentType.InterestingEdge).ToList();
        Assert.Equal(2, edges.Count);
    }

    /// <summary>
    /// A run shorter than the noise length leaves nothing at all (0x0067FA14), and a run between fifteen
    /// millimetres and that leaves a line rather than a triangle - the far-side rule at 0x0067FF5C.
    /// </summary>
    [Fact]
    public void AShortRunLeavesALineAndANoisyOneLeavesNothing()
    {
        Assert.Equal(15.0, MemoryMap.OverheadEdgeFarMaxLenForLineMm);
        Assert.Equal(6.0, MemoryMap.OverheadEdgeSegmentNoiseLenMm);
        Assert.Equal(6.00001, MemoryMap.OverheadEdgeMinRunLengthSq);

        var noise = new MemoryMap();
        noise.AddVisionOverheadEdges(FrameOf(false, new Vec2(100, 0), new Vec2(100, 2)), AtOrigin);
        Assert.Empty(noise.Regions);

        var shortRun = new MemoryMap();
        shortRun.AddVisionOverheadEdges(FrameOf(false, new Vec2(100, 0), new Vec2(100, 10)), AtOrigin);
        var clear = shortRun.Regions.Single();
        Assert.Equal(MemoryMapContentType.ClearOfObstacle, clear.Type);
        Assert.Equal(2, clear.Polygon.Length);
        Assert.Equal(new Vec2(100, 5), clear.Polygon[1]);
    }

    /// <summary>
    /// The border pass at the end (0x006803AA), which is <c>QuadTreeProcessor::FillBorder</c> 0x00689FAC:
    /// an interesting edge that touches one of the masked types - the five obstacles and
    /// NotInterestingEdge, the table at 0x00C87675 - is written off as NotInterestingEdge, because the
    /// frontier there is a thing the robot has already found rather than somewhere left to look. An edge
    /// out in the open is left alone.
    /// </summary>
    [Fact]
    public void AnEdgeAgainstAnObstacleStopsBeingInteresting()
    {
        var map = new MemoryMap();
        map.Insert(new[] { new Vec2(300, -22), new Vec2(300, -18) }, MemoryMapContentType.InterestingEdge, null, 1);
        map.Insert(new[] { new Vec2(600, 0), new Vec2(600, 20) }, MemoryMapContentType.InterestingEdge, null, 1);
        map.Insert(new[] { new Vec2(290, -40), new Vec2(310, -40), new Vec2(310, -20), new Vec2(290, -20) },
                   MemoryMapContentType.ObstacleObservable, 7, 1);

        map.AddVisionOverheadEdges(FrameOf(true, new Vec2(150, -30), new Vec2(150, 30)), AtOrigin);

        var against = map.Regions.Single(r => r.Polygon[0].X == 300);
        var faraway = map.Regions.Single(r => r.Polygon[0].X == 600);
        Assert.Equal(MemoryMapContentType.NotInterestingEdge, against.Type);
        Assert.Equal(MemoryMapContentType.InterestingEdge, faraway.Type);
        Assert.Equal(10.0, MemoryMap.ContentPrecisionMm);
        // and both types keep the robot out
        Assert.True(MemoryMapTypes.BlocksTheRobot(MemoryMapContentType.InterestingEdge));
        Assert.True(MemoryMapTypes.BlocksTheRobot(MemoryMapContentType.NotInterestingEdge));
    }

    /// <summary>
    /// The two entry points the visit-an-edge behaviour uses:
    /// <c>FlagQuadAsNotInterestingEdges</c> 0x0067E6B0 writes a quad off once the robot has been there,
    /// and <c>FlagGroundPlaneROIInterestingEdgesAsUncertain</c> 0x0067E50C takes the edges inside the
    /// ground ROI back to Unknown before it waits for the vision system to say what is there now (the
    /// lambda at 0x00680B54, which only touches content of type InterestingEdge).
    /// </summary>
    [Fact]
    public void TheVisitBehaviourCanWriteAnEdgeOffOrTakeItBackToUnknown()
    {
        var map = new MemoryMap();
        map.FlagQuadAsNotInterestingEdges(
            new[] { new Vec2(0, 0), new Vec2(20, 0), new Vec2(20, 20), new Vec2(0, 20) }, 55);
        var written = map.Regions.Single();
        Assert.Equal(MemoryMapContentType.NotInterestingEdge, written.Type);
        Assert.Equal(55u, written.Timestamp);

        var map2 = new MemoryMap();
        map2.Insert(new[] { new Vec2(100, -5), new Vec2(100, 5) }, MemoryMapContentType.InterestingEdge, null, 1);
        map2.Insert(new[] { new Vec2(600, 0), new Vec2(600, 10) }, MemoryMapContentType.InterestingEdge, null, 1);
        map2.Insert(new[] { new Vec2(100, 20), new Vec2(100, 30) }, MemoryMapContentType.ObstacleProx, null, 1);
        Assert.Equal(1, map2.FlagGroundPlaneRoiInterestingEdgesAsUncertain(AtOrigin, 77));
        Assert.Equal(MemoryMapContentType.Unknown, map2.Regions.Single(r => r.Polygon[0].X == 100 && r.Polygon[0].Y == -5).Type);
        Assert.Equal(MemoryMapContentType.InterestingEdge, map2.Regions.Single(r => r.Polygon[0].X == 600).Type);
        Assert.Equal(MemoryMapContentType.ObstacleProx, map2.Regions.Single(r => r.Polygon[0].Y == 20).Type);
    }

    /// <summary>
    /// The two masks the ray queries use, the tables at 0x00C8768B and 0x00C876A1: a point clamped to
    /// the far edge is refused by anything in the map but clear ground, while an unclamped one is
    /// refused only by the five obstacle types.
    /// </summary>
    [Fact]
    public void TheTwoRayMasksAreTheEngines()
    {
        Assert.True(MemoryMap.OverheadEdgeBlocksAll(MemoryMapContentType.Cliff));
        Assert.True(MemoryMap.OverheadEdgeBlocksAll(MemoryMapContentType.InterestingEdge));
        Assert.True(MemoryMap.OverheadEdgeBlocksAll(MemoryMapContentType.NotInterestingEdge));
        Assert.False(MemoryMap.OverheadEdgeBlocksAll(MemoryMapContentType.ClearOfCliff));

        Assert.True(MemoryMap.OverheadEdgeBlocksObstacles(MemoryMapContentType.ObstacleProx));
        Assert.False(MemoryMap.OverheadEdgeBlocksObstacles(MemoryMapContentType.Cliff));
        Assert.False(MemoryMap.OverheadEdgeBlocksObstacles(MemoryMapContentType.InterestingEdge));
    }

    /// <summary>
    /// A cube already in the map between the robot and the ground point stops the clear report: the
    /// engine drops the point rather than writing clear ground through something it knows is there
    /// (0x0067FCAA).
    /// </summary>
    [Fact]
    public void AnObstacleInTheWayStopsTheClearReport()
    {
        var map = new MemoryMap();
        map.Insert(new[] { new Vec2(50, -20), new Vec2(50, 20), new Vec2(70, 20), new Vec2(70, -20) },
                   MemoryMapContentType.ObstacleObservable, 7, 1);
        map.AddVisionOverheadEdges(FrameOf(false, new Vec2(100, -10), new Vec2(100, 10)), AtOrigin);
        Assert.Empty(map.Regions.Where(r => r.Type == MemoryMapContentType.ClearOfObstacle));
    }
}
