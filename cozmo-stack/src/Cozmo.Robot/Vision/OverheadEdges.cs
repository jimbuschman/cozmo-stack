using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

/// <summary>
/// <c>Anki::Cozmo::GroundPlaneROI</c>: the patch of floor in front of the robot the overhead-edge
/// detector looks at. <c>GetGroundQuad</c> 0x004F7774..0x004F77C2 builds it from immediates (no static is read; inventory row 7):
/// TL (190, 75), BL (40, 20), TR (190, -75), BR (40, -20) in robot coordinates - from 40 mm ahead, forty wide, out to 190 mm
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
/// The parts of the frame's <c>VisionPoseData</c> Detect reads besides the poses: the ground-plane homography H (+0x7C, nine binary32,
/// ground (x, y, 1) in robot mm to pixels; the LUT entry LookupGroundPlaneHomography chose), the ground-plane-visible byte (+0x78) and the
/// RobotState status (+0x5C). Inventory rows 4, 5 and 6 (VisionPoseData::Set 0x006AF330..0x006AF38A).
/// </summary>
public readonly record struct OverheadEdgeGroundInput(float[] Homography, bool GroundPlaneVisible, uint Status);

/// <summary>
/// <c>Anki::Cozmo::OverheadEdgesDetector::Detect</c> 0x006ABE34, the stack's live detector (VisionSystem constructs and calls it for
/// <c>DetectingOverheadEdges</c>). The algorithm is <see cref="NativeOverheadEdges"/>, a transcription of the approved inventory
/// <c>re-analysis/research/20261103-visit-edge-detector-extraction.md</c> proved bit for bit against the engine's own Detect under the
/// emulator. Its inputs are the frame's <c>VisionPoseData</c>: the ground-plane homography (+0x7C), the ground-plane-visible byte (+0x78),
/// the RobotState status (+0x5C) and, for the lift gate, the lift angle (+0x3C) with the frame's camera and robot pose.
/// The threshold 50 and the minimum chain length 3 are the one construction site's (VisionSystem::VisionSystem 0x006B011E..0x006B012E).
/// </summary>
// fidelity: M11-017
public sealed class OverheadEdgesDetector
{
    /// <summary>The <c>float</c> argument <c>VisionSystem</c> constructs the detector with (0x006B0120), kept at the detector's +0xC and compared against the absolute filter response.</summary>
    public const double EdgeThreshold = 50.0;

    /// <summary>The fewest points a chain may have to be kept, the detector's +0x10.</summary>
    public const int MinChainPoints = 3;

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

    /// <summary>The engine's edge kernel as binary32, seven rows by five, from its bit patterns at 0xC8E0AC (inventory row 13).</summary>
    public static float[,] Kernel
    {
        get
        {
            var k = new float[NativeOverheadEdges.KernelRows, NativeOverheadEdges.KernelCols];
            for (int y = 0; y < NativeOverheadEdges.KernelRows; y++)
                for (int x = 0; x < NativeOverheadEdges.KernelCols; x++)
                    k[y, x] = BitConverter.UInt32BitsToSingle(NativeOverheadEdges.KernelBits[y * NativeOverheadEdges.KernelCols + x]);
            return k;
        }
    }

    /// <summary>
    /// Runs Detect over one frame. Returns the frame it appended, or null when it appended none: the status gate (row 5) and the lift
    /// gate (row 9) return 0 with no frame. Its only failure return (row 9's pose-tree error) cannot arise in the stack, so a call always
    /// succeeds and the caller sets the processed-mode bit (row 2). The points come back in robot coordinates, which is what
    /// <see cref="MemoryMap.AddVisionOverheadEdges"/> expects.
    /// </summary>
    public OverheadEdgeFrame? Detect(GrayImage image, OverheadEdgeGroundInput ground, CameraModel camera, Pose3d robotPose,
                                     uint timestamp = 0, double? liftAngleRad = null)
    {
        NativeOverheadEdges.LiftGate? gate = liftAngleRad is { } lift
            ? (top, bottom) => LiftIsOutOfTheWay(camera, robotPose, lift, top, bottom)
            : null;
        NativeOverheadEdges.Detect(image, ground.Homography, ground.GroundPlaneVisible, ground.Status, timestamp, gate, out var frame);
        return frame;
    }

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

    internal static bool InsidePolygon(Vec2[] poly, Vec2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if (poly[i].Y > p.Y != poly[j].Y > p.Y &&
                p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
    }

    /// <summary>The engine's chain appender <c>AddPoint</c> 0x006AE1B0 (inventory row 27).</summary>
    internal static void Append(List<OverheadEdgePointChain> chains, OverheadEdgePoint point, bool isBorder) =>
        NativeOverheadEdges.AddPoint(chains, point, isBorder);
}
