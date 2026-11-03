using Cozmo.Protocol;
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
    /// A ground-plane homography for a camera, in the shape of the engine's LUT entries: H = K*[r0 r1 t] in binary32, ground (x, y, 1) in
    /// the robot frame of <paramref name="robotPose"/> to pixels (M11-017 inventory row 6; PopulateGroundPlaneHomographyLUT 0x0065176A..
    /// 0x00651BA6). Only a way to make the detector's input for a test camera; no distortion, as the LUT has none.
    /// </summary>
    internal static float[] GroundHomography(CameraModel cam, Pose3d robotPose)
    {
        var c = cam.Calibration;
        Vec3 t = cam.ToCamera(robotPose.Apply(new Vec3(0, 0, 0)));
        Vec3 r0 = cam.ToCamera(robotPose.Apply(new Vec3(1, 0, 0))) - t;
        Vec3 r1 = cam.ToCamera(robotPose.Apply(new Vec3(0, 1, 0))) - t;
        float[] k = { (float)c.FocalLengthX, (float)c.Skew * (float)c.FocalLengthX, (float)c.CenterX, 0, (float)c.FocalLengthY, (float)c.CenterY, 0, 0, 1 };
        float[] m = { (float)r0.X, (float)r1.X, (float)t.X, (float)r0.Y, (float)r1.Y, (float)t.Y, (float)r0.Z, (float)r1.Z, (float)t.Z };
        var h = new float[9];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                float v = 0f;
                for (int q = 0; q < 3; q++) v += k[i * 3 + q] * m[q * 3 + j];
                h[i * 3 + j] = v;
            }
        return h;
    }

    /// <summary>The detector's inputs for a test camera: its homography, ground plane visible, LIFT_IN_POS (rows 4-6).</summary>
    internal static OverheadEdgeGroundInput Ground(CameraModel cam, Pose3d robotPose) =>
        new(GroundHomography(cam, robotPose), true, (uint)RobotStatusFlag.LiftInPos);

    /// <summary>
    /// <c>GroundPlaneROI::GetGroundQuad</c> 0x004F7774..0x004F77C2 builds the trapezoid from immediates (M11-017 inventory row 7):
    /// TL (190, 75), BL (40, 20), TR (190, -75), BR (40, -20).
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
    /// The kernel, the function-local static Matx&lt;float,7,5&gt; copied from 0xC8E0AC (M11-017 inventory row 13): rows 0 and 1 as
    /// their bit patterns, rows 2..4 zero, row 5 = -row 1, row 6 = -row 0. The threshold 50.0f is the construction site's
    /// (0x006B0120 movt r1,#0x4248).
    /// </summary>
    [Fact]
    public void TheEdgeKernelIsTheEngines()
    {
        Assert.Equal(50.0, OverheadEdgesDetector.EdgeThreshold);
        Assert.Equal(0x42480000u, BitConverter.SingleToUInt32Bits(NativeOverheadEdges.Threshold));
        Assert.Equal(5.0, OverheadEdgesDetector.MaxChainGapMm);

        var k = OverheadEdgesDetector.Kernel;
        Assert.Equal(7, k.GetLength(0));
        Assert.Equal(5, k.GetLength(1));
        uint[] row0 = { 0x3C89A027, 0x3D9A6B51, 0x3DFE5C92, 0x3D9A6B51, 0x3C89A027 };
        uint[] row1 = { 0x3D1A6B51, 0x3E2CF41F, 0x3E8E8A72, 0x3E2CF41F, 0x3D1A6B51 };
        for (int x = 0; x < 5; x++)
        {
            Assert.Equal(row0[x], BitConverter.SingleToUInt32Bits(k[0, x]));
            Assert.Equal(row1[x], BitConverter.SingleToUInt32Bits(k[1, x]));
            Assert.Equal(0f, k[2, x]);
            Assert.Equal(0f, k[3, x]);
            Assert.Equal(0f, k[4, x]);
            Assert.Equal(row1[x] | 0x80000000u, BitConverter.SingleToUInt32Bits(k[5, x]));
            Assert.Equal(row0[x] | 0x80000000u, BitConverter.SingleToUInt32Bits(k[6, x]));
        }
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
    /// The detector on two oracle frames (the engine's Detect under the emulator, NativeOverheadEdgesOracleTests' fixture). Flat grey
    /// ground has no step for the kernel to find, so every column reports clear (kind 0, rows 26-27); a step across the ROI gives border
    /// points (kind 1, rows 23-25) on the ground, in front of the robot. Expected: the oracle's frames. reach: unit.
    /// </summary>
    [Fact]
    public void FlatGroundReportsClearAndAStepReportsAnEdge()
    {
        var d = NativeOverheadEdgesOracleTests.Fixture_.Value;
        var det = new OverheadEdgesDetector();
        var flat = d.Cases.Single(c => c.Name == "probe_clamp__flat120");
        var frame = det.Detect(d.Images[flat.Image], new OverheadEdgeGroundInput(flat.H, true, flat.Status), Camera(), AtOrigin, flat.Ts);
        Assert.Null(NativeOverheadEdgesOracleTests.Compare(flat, frame));
        Assert.True(frame!.GroundPlaneValid);
        Assert.NotEmpty(frame.Chains);
        Assert.All(frame.Chains, c => Assert.False(c.IsBorder));

        var step = d.Cases.Single(c => c.Name == "probe_clamp__step_probe");
        var frame2 = det.Detect(d.Images[step.Image], new OverheadEdgeGroundInput(step.H, true, step.Status), Camera(), AtOrigin, step.Ts);
        Assert.Null(NativeOverheadEdgesOracleTests.Compare(step, frame2));
        var border = frame2!.Chains.Where(c => c.IsBorder).SelectMany(c => c.Points).ToList();
        Assert.NotEmpty(border);
        Assert.All(border, p => Assert.InRange(p.Ground.X, GroundPlaneROI.DistMm,
                                              GroundPlaneROI.DistMm + GroundPlaneROI.LengthMm));
    }

    /// <summary>
    /// The lift gate at 0x006AC4A2: the arm in front of the ground the detector wants to look at makes it
    /// abandon the frame outright - it masks nothing and appends no frame (inventory row 9: "Else return 0, no frame"). With the lift down
    /// the frame comes back as usual. The gate's projection (Project3dPoint polynomial) is a RECOVERABLE_GAP and outside the oracle proof;
    /// the rect it is given is the unclipped truncated one of row 10. reach: unit.
    /// </summary>
    [Fact]
    public void TheArmInTheWayAbandonsTheFrame()
    {
        Assert.Equal(new Vec3(4, 0, -20), OverheadEdgesDetector.LiftPointA);
        Assert.Equal(new Vec3(-1, 0, -36.5), OverheadEdgesDetector.LiftPointB);

        // The head VisitInterestingEdge holds while it reads edges: preset 0xBE860A92 (M11-092, 0x005E79FC..0x005E7AD6).
        var cam = Camera(BitConverter.UInt32BitsToSingle(0xBE860A92));
        var flat = new GrayImage(320, 240);
        flat.Fill(120);
        var det = new OverheadEdgesDetector();

        // the engine's lift angle for a height: 45 + 66 sin(angle), so 32 mm is down and 92 mm is up.
        // With the arm either right down or right up it is not across the ground the ROI covers, and the
        // frame comes back as usual. Down, point A projects to row 253.6, below the ROI rectangle's bottom (row 239, the image's last).
        double down = Math.Asin((32.0 - 45) / LiftGeometry.ArmLengthMm);
        double up = Math.Asin((92.0 - 45) / LiftGeometry.ArmLengthMm);
        Assert.True(det.Detect(flat, Ground(cam, AtOrigin), cam, AtOrigin, 1, down)!.GroundPlaneValid);
        Assert.True(det.Detect(flat, Ground(cam, AtOrigin), cam, AtOrigin, 2, up)!.GroundPlaneValid);

        // the rule itself: the first point below the rectangle lets the frame through, and a rectangle
        // the arm is drawn across does not
        Assert.True(OverheadEdgesDetector.LiftIsOutOfTheWay(cam, AtOrigin, down, 0, 0));
        Assert.False(OverheadEdgesDetector.LiftIsOutOfTheWay(cam, AtOrigin, down, -10000, 10000));
        // The head at its lowest (M11-054, camera z -8): the camera sits at (-0.5, 0, 34.35) looking 29 degrees down. Lift-down point A (27.7, 0, 12)
        // is 38.4 degrees down, so row 120 + 290 tan 9.4 = 168; the ROI's near edge (40, 0, 0) is 40.3 degrees down, row 178; point B (22.7, 0, -4.5)
        // is row 288, not above the rectangle's top. The arm is across the rectangle, so the rule above abandons a real frame.
        var abandoned = det.Detect(flat, Ground(Camera(), AtOrigin), Camera(), AtOrigin, 3, down);
        Assert.Null(abandoned);
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
    // reach: unit (supplied frame/history/current/image clock; actual native geometry).
    // Old code: zero native writes; an unclamped legacy triangle starts at robot origin.
    public void ABorderRunBecomesAClearTriangleAndAnInterestingEdge()
    {
        using var f=new M13OverheadMapWriterTests.Fixture();
        var closest=f.Run(true,new(60,-30),new(60,0),new(60,30));
        Assert.Equal(60d,closest);Assert.Equal(2,f.Writes.Count);
        Assert.Equal(MemoryMapContentType.ClearOfObstacle,f.Writes[0].Data.Type);
        Assert.Equal(4,f.Writes[0].Polygon.Length); // far=60, near=40, both widths exceed threshold
        Assert.Equal(MemoryMapContentType.InterestingEdge,f.Writes[1].Data.Type);
        Assert.Equal(4242u,f.Writes[1].Data.Last);Assert.Empty(f.Map.Regions);
    }

    /// <summary>
    /// The forty-degree rule at 0x0067FD40: a run that turns harder than that is closed off and the next
    /// one starts where it ended, so an L of border points leaves two edges rather than one.
    /// </summary>
    [Fact]
    // reach: unit (supplied history/frame inputs; actual native run state machine).
    // Old code: two legacy edges; zero native edges. Checked objection A requires one edge.
    public void ARunThatTurnsMoreThanFortyDegreesIsSplit()
    {
        using var f=new M13OverheadMapWriterTests.Fixture();
        Assert.Equal(0x3F441893u,BitConverter.SingleToUInt32Bits((float)MemoryMap.OverheadEdgeRunDirectionCos));
        f.Run(true,new(60,-20),new(60,0),new(70,0));
        Assert.Single(f.Writes.Where(w=>w.Data.Type==MemoryMapContentType.InterestingEdge));
        Assert.Empty(f.Map.Regions); // final direction-break tail is lost, not unconditionally flushed
    }

    /// <summary>
    /// A run shorter than the noise length leaves nothing at all (0x0067FA14), and a run between fifteen
    /// millimetres and that leaves a line rather than a triangle - the far-side rule at 0x0067FF5C.
    /// </summary>
    [Fact]
    // reach: unit (supplied history/frame; actual clamp, noise threshold and native insertion).
    // Old code: zero native writes and a line starting at robot (0,0), not near midpoint.
    public void AShortRunLeavesALineAndANoisyOneLeavesNothing()
    {
        using var f=new M13OverheadMapWriterTests.Fixture();
        Assert.Equal(0x40C00015u,BitConverter.SingleToUInt32Bits((float)MemoryMap.OverheadEdgeMinRunLengthSq));
        f.Run(false,new(60,0),new(60,2));Assert.Empty(f.Writes);
        f.Run(false,new(60,0),new(60,10));
        var clear=Assert.Single(f.Writes);Assert.Equal(2,clear.Polygon.Length);
        Assert.InRange(clear.Polygon[0].X,39.99999,40.00001);Assert.Equal(60,clear.Polygon[1].X);
        Assert.Equal(5,clear.Polygon[1].Y);Assert.Empty(f.Map.Regions);
    }

    /// <summary>
    /// The border pass at the end (0x006803AA), which is <c>QuadTreeProcessor::FillBorder</c> 0x00689FAC:
    /// an interesting edge that touches one of the masked types - the five obstacles and
    /// NotInterestingEdge, the table at 0x00C87675 - is written off as NotInterestingEdge, because the
    /// frontier there is a thing the robot has already found rather than somewhere left to look. An edge
    /// out in the open is left alone.
    /// </summary>
    [Fact]
    // reach: unit (supplied history/fresh clock; observes native FillBorderInternal wrapper).
    // Old code: 0 native border calls; runs legacy polygon adjacency instead.
    public void AnEdgeAgainstAnObstacleStopsBeingInteresting()
    {
        using var f=new M13OverheadMapWriterTests.Fixture();
        f.Map.Insert(new[]{new Vec2(60,-20),new Vec2(60,20)},MemoryMapContentType.InterestingEdge,null,1);
        f.Run(true,new(70,-20),new(70,20));
        var border=Assert.Single(f.Borders);Assert.Equal(777u,border.Data.Last);
        // Native border consumer remains MISSING at 0x00689FAC. Do not assert legacy adjacency as native behavior.
        Assert.Equal(MemoryMapContentType.InterestingEdge,Assert.Single(f.Map.Regions).Type);
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
    // reach: unit (native tree obstacle and supplied history/frame; real native ray admission).
    // Old native ray already blocks this obstacle; old writer has 0 final whiteboard state writes (new: 1).
    public void AnObstacleInTheWayStopsTheClearReport()
    {
        using var f=new M13OverheadMapWriterTests.Fixture();
        f.Map.InsertObject(new[]{new Vec2(50,-20),new Vec2(50,20),new Vec2(70,20),new Vec2(70,-20)},new SourceMapData(MemoryMapContentType.ObstacleCharger,1,1));
        f.Writes.Clear();f.Map.AddVisionOverheadEdges(FrameOf(false,new(100,-10),new(100,10)),f.Current);
        Assert.Empty(f.Writes);Assert.Empty(f.Clears);Assert.Empty(f.Map.Regions);Assert.Single(f.States);
    }
}
