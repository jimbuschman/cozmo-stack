using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// R-FIX batch 1: the binary32 constants and widths of M11-002/003/006/008/010/015/020/022, M13-002 and M14-001/003/004/005.
/// Every expected value here is read from the cited instruction or literal in libcozmoEngine.so, or worked out from the cited
/// arithmetic in IEEE binary32 with an independent emulation (numpy float32, not this code); none is the code's own output.
/// Where the engine calls a libm function (acosf, cosf, atan2f) the stack uses MathF as a stand-in, so those comparisons carry a
/// few-ulp tolerance and no claim of bit equality.
/// </summary>
public class RFixBatch1Tests
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static uint Bits(double d) => BitConverter.SingleToUInt32Bits((float)d);

    // ------------------------------------------------------------------ M11-003

    /// <summary>
    /// M11-003, Block::AddFace 0x004E5442..0x004E55D6: the angle words 0xBFC90FDB (movt 0x004E5448), 0x3FC90FDB (0x004E54F0), 0x40490FDB (0x004E5582),
    /// 0x40060A92 (0x004E5496 and 0x004E55DC) and the axis word 0x3F13CD3A (0x004E54A0..0x004E54A4). The rotation entries below are the double cosines/sines of those
    /// binary32 angles, worked independently: cos(1.5707963705) = -4.371139e-08, sin(3.1415927410) = -8.742278e-08, and for the 120 degree faces
    /// R00 = c + (1 - c)/3 with c = cos(2.0943951607) = -3.36490e-08. The exact pi/2, pi and 2pi/3 give 6e-17, 1.2e-16 and 1.7e-16.
    /// </summary>
    [Fact]
    public void M11_003_BlockAddFaceUsesTheEnginesBinary32Constants()
    {
        Assert.Equal(0xBFC90FDBu, CubeGeometry.MinusHalfPiBits);
        Assert.Equal(0x3FC90FDBu, CubeGeometry.HalfPiBits);
        Assert.Equal(0x40490FDBu, CubeGeometry.PiBits);
        Assert.Equal(0x40060A92u, CubeGeometry.TwoPiOverThreeBits);
        Assert.Equal(0x3F13CD3Au, CubeGeometry.AxisBits);

        var markers = CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1);
        var front = markers.First(m => m.Face == BlockFace.Front).PoseOnObject.Rotation;
        var back = markers.First(m => m.Face == BlockFace.Back).PoseOnObject.Rotation;
        var left = markers.First(m => m.Face == BlockFace.Left).PoseOnObject.Rotation;
        var top = markers.First(m => m.Face == BlockFace.Top).PoseOnObject.Rotation;
        var bottom = markers.First(m => m.Face == BlockFace.Bottom).PoseOnObject.Rotation;
        Assert.Equal(-4.371139000186241e-08, front[0, 0], 12);
        Assert.Equal(-4.371139000186241e-08, back[0, 0], 12);
        Assert.Equal(-8.742278000372475e-08, left[1, 0], 12);
        Assert.Equal(-3.3649042996408696e-08, top[0, 0], 12);
        Assert.Equal(-3.3649042996408696e-08, bottom[0, 0], 12);
    }

    // ------------------------------------------------------------------ M11-008 / M11-010

    private static KnownMarker Square() => new(MarkerType.LightCubeI_Front, BlockFace.Front, Pose3d.Identity, 25.0);

    private static ObservableObject ObjectAt(Pose3d pose) =>
        new(7, ObjectType.Block_LIGHTCUBE1, new[] { Square() }) { Pose = pose };

    private static readonly CameraModel Camera = new(CameraCalibration.Nominal(), Pose3d.Identity);

    private static NotVisibleReason Visibility(Pose3d pose, double maxAngle, double minSize)
    {
        var o = ObjectAt(pose);
        return o.MarkerVisibility(o.Markers[0], Camera, maxAngle, minSize, 0, 0);
    }

    /// <summary>
    /// M11-008, CheckForUnobservedObjects 0x006220E2..0x006220EC and the cube-moved strategy 0x0060BCF0..0x0060BCFA both build the face-normal angle as the binary32
    /// word 0x3F490FDB (0.78539819), not 0x3F490FD8 (0.785398), which does not occur in the binary.
    /// </summary>
    [Fact]
    public void M11_008_TheFaceNormalAngleIsTheWord0x3F490FDB()
    {
        Assert.Equal(0x3F490FDBu, BlockWorld.VisibilityNormalAngleBits);
        Assert.Equal(0x3F490FDBu, Bits(BlockWorld.VisibilityNormalAngleRad));
        Assert.Equal((double)F(0x3F490FDB), BlockWorld.VisibilityNormalAngleRad);
    }

    /// <summary>
    /// M11-010, KnownMarker::IsVisibleFrom 0x0087E4A8: with the 25 mm marker facing the camera 300 mm ahead (the marker rotated 90 degrees about X so its -Y normal points back at
    /// the camera) the camera-frame corners are (+-12.5, +-12.5, 300); with the nominal focal length 290 they project 24.17 px apart on each side, so the two DIAGONALS the
    /// engine measures (|p3 - p0| then |p1 - p2|, 0x0087E6F2..0x0087E792) are 24.1667 * sqrt 2 = 34.18 px. 33 px is under that (visible), 35 is over it (TooSmall); the old
    /// square-root-of-area test measured 24.17 and refused 33.
    /// </summary>
    [Fact]
    public void M11_010_TheProjectedSizeIsTheDiagonalsLength()
    {
        var facing = new Pose3d(Mat3.AboutX(Math.PI / 2), new Vec3(0, 0, 300));
        Assert.Equal(NotVisibleReason.IsVisible, Visibility(facing, 0.785398, 33));
        Assert.Equal(NotVisibleReason.TooSmall, Visibility(facing, 0.785398, 35));
    }

    /// <summary>
    /// M11-010, 0x0087E5E8..0x0087E646: the dot of the corner-frame normal with Z_AXIS_3D (0x00842CF4..0x00842D08, (0, 0, 1)) being positive is NormalNotAligned
    /// whatever the angle limit is; <c>acosf(-dot)</c> is only reached for a non-positive dot. A marker turned its back (rotation -90 degrees about X) has n.z = +1, so it is
    /// refused even with a limit of 4.0 rad, above pi (the old cosine test accepted it, acos = pi).
    /// </summary>
    [Fact]
    public void M11_010_ABackFacingMarkerIsRefusedWhateverTheAngleLimitIs()
    {
        var away = new Pose3d(Mat3.AboutX(-Math.PI / 2), new Vec3(0, 0, 300));
        Assert.Equal(NotVisibleReason.NormalNotAligned, Visibility(away, 4.0, 0));
    }

    /// <summary>
    /// M11-010, 0x0087E514..0x0087E520: BehindCamera (3) is decided from the marker pose's camera-frame Z (Transform3d+0x28) BEFORE the normal is looked at. The marker at z = -300
    /// that faces the camera's +Z back (n.z = -1, dot -1) is BehindCamera; the old code tested the normal first.
    /// </summary>
    [Fact]
    public void M11_010_BehindTheCameraIsTestedBeforeTheNormal()
    {
        var behind = new Pose3d(Mat3.AboutX(Math.PI / 2), new Vec3(0, 0, -300));
        Assert.Equal(NotVisibleReason.BehindCamera, Visibility(behind, 0.785398, 0));
    }

    /// <summary>
    /// M11-010, 0x0087E622..0x0087E646: the angle is acosf(-dot) compared with the limit. A marker turned 0.7 rad about Y has n = (-sin 0.7, 0, -cos 0.7), so acosf(cos 0.7) = 0.7:
    /// refused by a 0.69 limit, accepted by 0.71 (tolerance of the libm stand-in is far below 0.01).
    /// </summary>
    [Fact]
    public void M11_010_TheNormalAngleIsAcosOfTheNegatedDot()
    {
        var turned = new Pose3d(Mat3.AboutY(0.7) * Mat3.AboutX(Math.PI / 2), new Vec3(0, 0, 300));
        Assert.Equal(NotVisibleReason.NormalNotAligned, Visibility(turned, 0.69, 0));
        Assert.Equal(NotVisibleReason.IsVisible, Visibility(turned, 0.71, 0));
    }

    /// <summary>
    /// M11-010, Point3f::MakeUnitLength 0x0050E0C0 in binary32 (numpy float32 emulation of the instructions): (3, 4, 12): s = 169, inverse 1/13 = 0.07692308, components
    /// 0x3E6C4EC6, 0x3E9D89D9, 0x3F6C4EC6; (1, 1, 1) gives 0x3F13CD3A each; a zero (or NaN) sum leaves the point alone.
    /// </summary>
    [Fact]
    public void M11_010_MakeUnitLengthIsBinary32()
    {
        float x = 3, y = 4, z = 12;
        ObservableObject.MakeUnitLength(ref x, ref y, ref z);
        Assert.Equal(new[] { 0x3E6C4EC6u, 0x3E9D89D9u, 0x3F6C4EC6u }, new[] { BitConverter.SingleToUInt32Bits(x), BitConverter.SingleToUInt32Bits(y), BitConverter.SingleToUInt32Bits(z) });
        x = y = z = 1;
        ObservableObject.MakeUnitLength(ref x, ref y, ref z);
        Assert.Equal(0x3F13CD3Au, BitConverter.SingleToUInt32Bits(x));
        Assert.Equal(0x3F13CD3Au, BitConverter.SingleToUInt32Bits(z));
        x = y = z = 0;
        ObservableObject.MakeUnitLength(ref x, ref y, ref z);
        Assert.Equal((0f, 0f, 0f), (x, y, z));
        x = float.NaN; y = 1; z = 1;
        ObservableObject.MakeUnitLength(ref x, ref y, ref z);
        Assert.True(float.IsNaN(x) && y == 1f && z == 1f);
    }

    // ------------------------------------------------------------------ M11-015

    /// <summary>
    /// M11-015, TurnInPlaceAction constructor 0x005459D4: max speed movw/movt 0x00545A28/0x00545A32 = 0x40A78D36 stored to +0x78 (0x00545A44) and tolerance 0x3D0EFA35
    /// (0x00545A78/0x00545A80, Radians(float) 0x00545A84). They are what reaches SetBodyAngle on the wire (the old words were 0x40A78D3B and 0x3D0EFA39).
    /// </summary>
    [Fact]
    public void M11_015_TheBodyTurnWordsOnTheWireAreTheConstructorsWords()
    {
        Assert.Equal(0x40A78D36u, TurnTowardsPose.MaxSpeedBits);
        Assert.Equal(0x3D0EFA35u, TurnTowardsPose.ToleranceBits);
        var msg = TurnTowardsPose.Message(1.0, TurnTowardsPose.MaxSpeedRadPerSec, TurnTowardsPose.AccelRadPerSec2, TurnTowardsPose.ToleranceRad, 0, true, 3);
        Assert.Equal(0x40A78D36u, BitConverter.SingleToUInt32Bits(msg.MaxSpeedRadPerSec));
        Assert.Equal(0x3D0EFA35u, BitConverter.SingleToUInt32Bits(msg.ToleranceRad));
        Assert.Equal(0x41200000u, BitConverter.SingleToUInt32Bits(msg.AccelRadPerSec2));
    }

    // ------------------------------------------------------------------ M11-020

    /// <summary>
    /// M11-020, DetectFiducialMarkers 0x008992BA..0x0089938E: the kernel factor is the literal 0x3FB50481 (0x00899258, loaded by vldr 0x00898F98), all arithmetic binary32,
    /// k = (int)roundf(((fx + fy) * 0.5f * K) * (|p0 - p3| + |p2 - p1|)). For the 100 px square (the diagonals 141.42136 each) and fx = fy = 0.10125 the binary32 product is
    /// 40.49961 -> 40 (numpy float32 emulation); with the old 1.4142135f it was 40.5 and rounded to 41. With the default 0.1 it is 39.999615 -> 40.
    /// </summary>
    [Fact]
    public void M11_020_TheKernelFactorIs0x3FB50481AndTheArithmeticBinary32()
    {
        Assert.Equal(0x3FB50481u, CornerRefinement.KernelFactorBits);
        var square = new[] { new Vec2(0, 0), new Vec2(0, 100), new Vec2(100, 0), new Vec2(100, 100) };
        Assert.Equal(40, CornerRefinement.KernelSize(square, new QuadDetectorParameters { RefineInnerFraction = 0.10125 }));
        Assert.Equal(40, CornerRefinement.KernelSize(square, new QuadDetectorParameters()));
    }

    /// <summary>
    /// M11-020, 0x008993D2: the rounded size goes to cv::boxFilter UNCLAMPED. A 1 px square gives 0.1 * 1.4142 * 2.83 = 0.4 -> 0, and the clamp to 1 this stack had is gone: it stops
    /// visibly (OpenCV's own result for ksize 0 is not in the inventory) instead of inventing a kernel.
    /// </summary>
    [Fact]
    public void M11_020_AKernelUnderOneIsNotClampedToOne()
    {
        var tiny = new[] { new Vec2(0, 0), new Vec2(0, 1), new Vec2(1, 0), new Vec2(1, 1) };
        var ex = Assert.Throws<NotSupportedException>(() => CornerRefinement.KernelSize(tiny, new QuadDetectorParameters()));
        Assert.Contains("MISSING", ex.Message);
        var two = new[] { new Vec2(0, 0), new Vec2(0, 2), new Vec2(2, 0), new Vec2(2, 2) };      // 0.8 -> 1: no throw, 1 comes from the arithmetic
        Assert.Equal(1, CornerRefinement.KernelSize(two, new QuadDetectorParameters()));
    }

    // ------------------------------------------------------------------ M11-022

    /// <summary>
    /// M11-022, Rectangle&lt;int&gt;::InitFromPointContainer 0x006ABB38: x = min (truncated), y = min, width = maxX - minX, height = maxY - minY (0x006ABBCA sub.w r2,lr,r3;
    /// 0x006ABBCE subs r1,r4,r1; strd r1,r2 at +8): no +1.
    /// </summary>
    [Fact]
    public void M11_022_TheBoundingRectangleHasNoPlusOne()
    {
        var corners = new[] { new Vec2(10.9, 20.9), new Vec2(10.2, 50.7), new Vec2(40.5, 20.1), new Vec2(40.9, 50.9) };
        Assert.Equal((10, 20, 30, 30), MarkerDetector.BoundingRect(corners));
    }

    // ------------------------------------------------------------------ M11-002

    /// <summary>
    /// M11-002, NearestNeighborLibrary::GetNearestNeighbor 0x008C0938: the distance out-parameter is seeded with the caller's threshold (0x008C098A..0x008C098C) and a row is
    /// taken only when its distance is STRICTLY smaller (0x008C0B72 cmp r1,r0 / bgt); with no such row the label stays -1 (0x008C0944). A marker rendered from its own library
    /// row at threshold equal to its distance is therefore no match, and at one more it is.
    /// </summary>
    [Fact]
    public void M11_002_TheBestDistanceIsSeededWithTheThresholdAndTakenOnlyWhenStrictlySmaller()
    {
        var lib = MarkerLibrary.EmbeddedOrNull;
        if (lib is null) return;
        var dec = new MarkerDecoder(lib);
        int row = MarkerRenderer.RowForCode(lib, MarkerType.LightCubeI_Front, 0);
        var img = MarkerRenderer.Render(lib, row, 96);
        var h = Homography.FromUnitSquare(new[] { new Vec2(0, 0), new Vec2(0, 96), new Vec2(96, 0), new Vec2(96, 96) });
        var probes = dec.GetProbeValues(img, h);
        var best = dec.GetNearestNeighbor(probes);
        Assert.False(best.Rejected);
        int d = best.Distance;
        Assert.True(d < MarkerLibrary.MatchThreshold);

        var atDistance = dec.GetNearestNeighbor(probes, d);               // best row's d is not < d
        Assert.True(atDistance.Rejected);
        Assert.Equal(MarkerLibrary.InvalidLabel, atDistance.Label);
        Assert.Equal(d, atDistance.Distance);                              // the seed survives

        var above = dec.GetNearestNeighbor(probes, d + 1);
        Assert.False(above.Rejected);
        Assert.Equal(d, above.Distance);
        Assert.Equal(best.Label, above.Label);
    }

    // ------------------------------------------------------------------ M13-002

    /// <summary>
    /// M13-002, FlipBlockAction::Init 0x0055EED6..0x0055EF1C and CheckIfDone 0x0055F0E6..0x0055F10A: the three-D norm is ((x*x) + y*y) + z*z, vsqrt.f32, in binary32 (numpy float32 for
    /// (100.3, 50.7, 0.1): 112.3859 = 0x42E0C595), and the drive distance is that norm plus [this+0x130] = 20.0f as one vadd.f32 (132.3859).
    /// </summary>
    [Fact]
    public void M13_002_TheFlipDistanceIsABinary32Norm()
    {
        float n = FlipBlockAction.Norm3(new Vec3(100.3, 50.7, 0.1));
        Assert.Equal(0x42E0C595u, BitConverter.SingleToUInt32Bits(n));
        Assert.Equal(132.3859f, n + 20f, 3);
    }

    /// <summary>
    /// M13-002 through the live entry: MoveLiftToHeightAction(45, 5, 0)'s 5 is the TOLERANCE (r3 = 0x40A00000 at 0x0055EF42), so the lift command carries the action's own
    /// constructor speed and acceleration, 10.0 and 20.0 (+0x8C, +0x90 at 0x00548A6E..0x00548A78), never 5.
    /// </summary>
    [Fact]
    public void M13_002_TheApproachLiftMoveIsSentWithTheActionsOwnSpeedNotTheTolerance()
    {
        if (MarkerLibrary.EmbeddedOrNull is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        uint flags = (uint)(RobotStatusFlag.LiftInPos | RobotStatusFlag.HeadInPos);
        float lowLift = (float)Math.Asin((32.0 - 45.0) / 66.0);           // the lift at 32 mm, so the 45 mm move is not a no-op (M4 MA15)
        rig.State(flags, lowLift);
        var flip = new FlipBlockAction(rig.M, 7) { CheckPreActionPose = false };   // flag A = 0: a robot 200 mm away is not refused (M12-031)
        using var cts = new CancellationTokenSource();
        var t = flip.RunAsync(cts.Token);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!rig.Sent.OfType<SetLiftHeight>().Any(l => l.HeightMm == 45f) && !t.IsCompleted && sw.ElapsedMilliseconds < 8000) { rig.Pump(); rig.State(flags, lowLift); Thread.Sleep(5); }
        cts.Cancel();
        Assert.True(rig.Sent.OfType<SetLiftHeight>().Any(l => l.HeightMm == 45f), $"{(t.IsCompleted ? t.Result.ToString() : "running")} | {string.Join(" | ", flip.Trace)}");
        var lift = rig.Sent.OfType<SetLiftHeight>().First(l => l.HeightMm == 45f);   // the approach move
        Assert.Equal(45f, lift.HeightMm);
        Assert.Equal(10f, lift.MaxSpeedRadPerSec);
        Assert.Equal(20f, lift.AccelRadPerSec2);
    }

    // ------------------------------------------------------------------ M14-001

    /// <summary>
    /// M14-001, TrackedFace::GetIntraEyeDistance 0x0087DC68, numpy float32 emulation of its instructions (epsilon 0x3727C5AC): eyes (100,100) and (162.5,100.25), roll 0: dx = -62.5,
    /// dy = -0.25, sqrtf(3906.3125) = 62.5005 = 0x427A0083, cos 1; roll pi/2 (0x3FC90FDB): |cos| = 4.4e-8 is under epsilon so the divisor is 1.0 and the answer 62.0; the same point
    /// gives 6.0 (0x0087DD86); roll 0.5 gives 62 / cosf(0.5) = 70.64863 and roll pi gives 62 / -1.
    /// </summary>
    [Fact]
    public void M14_001_TheEyeDistanceIsBinary32()
    {
        Assert.Equal(0x427A0083u, BitConverter.SingleToUInt32Bits(TrackedFace.GetIntraEyeDistanceF(100, 100, 162.5f, 100.25f, 0f)));
        Assert.Equal(62.0f, TrackedFace.GetIntraEyeDistanceF(100, 100, 162, 100, F(0x3FC90FDB)));
        Assert.Equal(6.0f, TrackedFace.GetIntraEyeDistanceF(100, 100, 100, 100, 0f));
        Assert.Equal(70.64863f, TrackedFace.GetIntraEyeDistanceF(100, 100, 162, 100, 0.5f), 3);       // cosf stand-in tolerance
        Assert.Equal(-62.0f, TrackedFace.GetIntraEyeDistanceF(100, 100, 162, 100, (float)Math.PI), 4);
        Assert.Equal(0x3727C5ACu, TrackedFace.MinCosOrDistanceBits);
        Assert.Equal(0x42780000u, TrackedFace.InterPupilBits);
    }

    /// <summary>
    /// M14-001, TrackedFace::UpdateTranslation 0x0087DE24 (numpy float32 emulation of the instructions, nominal 290/290/160/120 calibration with the identity camera pose): the 124 x 124 box
    /// gives the distance 62.0 (0x0087DEAA..0x0087DF1C), the zeroed eye slots give the pixel (0,0), invK*(0,0,1) = (-160/290, -120/290, 1) (0x0085EF3A, no distortion), unit-length, times
    /// (290 * 62.0) / 62 = 290: (-131.71404, -98.78553, 238.73169). A calibration with radial distortion changes nothing (the engine's ray is plain invK). The parts branch with eyes (150,118)
    /// and (170,118) is distance 20, scale 899.0, translation (0, -6.1998563, 898.9787). The 123.9 x 77.7 box at (100.3, 200.7) gives the binary32 distance 61.950012 = 0x4277CCD0.
    /// </summary>
    [Fact]
    public void M14_001_TheFaceTranslationIsBinary32AndUsesTheInverseCalibrationRay()
    {
        var plain = new CameraModel(CameraCalibration.Nominal(), Pose3d.Identity);
        var distorted = new CameraModel(CameraCalibration.Nominal() with { DistortionCoefficients = new double[] { 0.4, 0.1, 0, 0, 0, 0, 0, 0 } }, Pose3d.Identity);
        foreach (var cam in new[] { plain, distorted })
        {
            var box = new TrackedFace(new DetectedFace(3, new FaceRect(100, 200, 124, 124)), 1000);
            box.UpdateTranslation(cam);
            Assert.Equal(290.0f, (float)box.DistanceMm);
            var t = box.HeadPose.Translation;
            Assert.Equal(new[] { 0xC303B6CBu, 0xC2C59231u, 0x436EBB50u }, new[] { Bits(t.X), Bits(t.Y), Bits(t.Z) });

            var parts = new TrackedFace(new DetectedFace(4, new FaceRect(140, 100, 40, 40), new Vec2(150, 118), new Vec2(170, 118), RollRad: 0.0), 1000);
            parts.UpdateTranslation(cam);
            Assert.Equal(899.0f, (float)parts.DistanceMm);
            Assert.Equal(0.0, parts.HeadPose.Translation.X, 4);
            Assert.Equal(-6.1998563, parts.HeadPose.Translation.Y, 3);
            Assert.Equal(898.9787, parts.HeadPose.Translation.Z, 3);
        }
        var odd = new TrackedFace(new DetectedFace(5, new FaceRect(100.3, 200.7, 123.9, 77.7)), 1000);
        odd.UpdateTranslation(plain);
        // (290 * 62) / 61.950012 in binary32
        Assert.Equal((290f * 62f) / F(0x4277CCD0), (float)odd.DistanceMm);
    }

    // ------------------------------------------------------------------ M11-010 camera

    /// <summary>
    /// M11-010, Camera::IsWithinFieldOfView 0x0085E13C..0x0085E1C6: an inside test with u16 insets, NaN refused. 320 x 240: x == 320 and y == 240 are outside (strict upper bounds,
    /// the u16 at +2 = columns, at +0 = rows), 0 is inside, a pad of 5 removes a 5 px band on every side (x &gt;= 5, x &lt; 315), NaN in either coordinate is outside.
    /// </summary>
    [Fact]
    public void M11_010_TheFieldOfViewTestIsAnInsideTestWithInsets()
    {
        Assert.True(ObservableObject.IsWithinFieldOfView(0, 0, 0, 0, 320, 240));
        Assert.True(ObservableObject.IsWithinFieldOfView(319.5f, 239.5f, 0, 0, 320, 240));
        Assert.False(ObservableObject.IsWithinFieldOfView(320f, 100, 0, 0, 320, 240));
        Assert.False(ObservableObject.IsWithinFieldOfView(100, 240f, 0, 0, 320, 240));
        Assert.False(ObservableObject.IsWithinFieldOfView(-0.5f, 100, 0, 0, 320, 240));
        Assert.False(ObservableObject.IsWithinFieldOfView(float.NaN, 100, 0, 0, 320, 240));
        Assert.False(ObservableObject.IsWithinFieldOfView(100, float.NaN, 0, 0, 320, 240));
        Assert.False(ObservableObject.IsWithinFieldOfView(4.9f, 100, 5, 5, 320, 240));
        Assert.True(ObservableObject.IsWithinFieldOfView(5f, 5f, 5, 5, 320, 240));
        Assert.True(ObservableObject.IsWithinFieldOfView(314.9f, 234.9f, 5, 5, 320, 240));
        Assert.False(ObservableObject.IsWithinFieldOfView(315f, 100, 5, 5, 320, 240));
        Assert.False(ObservableObject.IsWithinFieldOfView(100, 235f, 5, 5, 320, 240));
    }

    /// <summary>
    /// M11-010, Camera::Project3dPoint 0x0085E264: z &lt;= 0 gives (NaN, NaN); (29, -14.5, 290) on the nominal calibration with no coefficients is (189, 105.5) = 0x433D0000, 0x42D30000;
    /// with 5 coefficients [0.4, 0.1, 0.01, -0.02, 0.05] 0x433CED8E, 0x42D31271; with all 8 [..., 0.02, 0.03, 0.04] 0x433CEBA8, 0x42D31458 (numpy float32 emulation of the
    /// instructions, the engine's operation order, no skew).
    /// </summary>
    [Fact]
    public void M11_010_TheProjectionIsTheEnginesBinary32Sequence()
    {
        var cal = CameraCalibration.Nominal();
        ObservableObject.ProjectPoint(cal, 1, 1, 0, out float nx, out float ny);
        Assert.True(float.IsNaN(nx) && float.IsNaN(ny));
        ObservableObject.ProjectPoint(cal, 1, 1, -3, out nx, out ny);
        Assert.True(float.IsNaN(nx) && float.IsNaN(ny));
        void Check(double[] k, uint bx, uint by)
        {
            ObservableObject.ProjectPoint(cal with { DistortionCoefficients = k }, 29, -14.5f, 290, out float px, out float py);
            Assert.Equal((bx, by), (BitConverter.SingleToUInt32Bits(px), BitConverter.SingleToUInt32Bits(py)));
        }
        Check(new double[0], 0x433D0000, 0x42D30000);
        Check(new double[8], 0x433D0000, 0x42D30000);
        Check(new[] { 0.4, 0.1, 0.01, -0.02, 0.05 }, 0x433CED8E, 0x42D31271);
        Check(new[] { 0.4, 0.1, 0.01, -0.02, 0.05, 0.02, 0.03, 0.04 }, 0x433CEBA8, 0x42D31458);
        var missing = new List<string>();
        void Hook(string m) => missing.Add(m);
        SteppedBehavior.MissingReported += Hook;
        try
        {
            SteppedBehavior.ResetMissingForTests();
            ObservableObject.ProjectPoint(cal with { DistortionCoefficients = new[] { 0.1, 0.2 } }, 1, 1, 10, out _, out _);   // undefined in the engine: reported, not thrown
            Assert.Contains(missing, m => m.Contains("0x0085E2AE"));
        }
        finally { SteppedBehavior.MissingReported -= Hook; }
    }

    /// <summary>
    /// M11-010, through ObservableObject.MarkerVisibility: a marker whose ORIGIN is in front of the camera (z = 5) but one of whose corners is behind it (the marker turned 80 degrees
    /// about Y) projects that corner to (NaN, NaN) (0x0085E284 bls), which neither fails the size test nor passes the field-of-view test: reason 6, not BehindCamera (3).
    /// </summary>
    [Fact]
    public void M11_010_ACornerBehindTheCameraGivesTheFieldOfViewReasonNotBehindCamera()
    {
        var pose = new Pose3d(Mat3.AboutY(80 * Math.PI / 180) * Mat3.AboutX(Math.PI / 2), new Vec3(0, 0, 5));
        Assert.Equal(NotVisibleReason.OutsideFieldOfView, Visibility(pose, 2.0, 0));
    }

    /// <summary>
    /// M11-008, the cube-moved strategy's IsVisibleFrom call (0x0060BCF0..0x0060BD08) passes minimum size 0 (movs r3,#0 at 0x0060BCFE), not the 40 of CheckForUnobservedObjects.
    /// A cube 700 mm ahead has marker diagonals of about 14.6 px: visible to the locator (size 0), but TooSmall under 40.
    /// </summary>
    [Fact]
    public void M11_008_TheCubeMovedLocatorUsesMinimumSizeZero()
    {
        Assert.True(MarkerLibrary.EmbeddedOrNull is not null, "the embedded marker library is required for this test");
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        var o = rig.Vision.World.GetLocatedObjectById(7)!;
        var p = o.Pose;
        o.Pose = new Pose3d(p.Rotation, new Vec3(700, 0, p.Translation.Z));
        Assert.True(rig.Vision.Locator.IsVisibleFromCamera(7));
        var cam = rig.Vision.CurrentCamera()!;
        Assert.False(o.IsVisibleFrom(cam, BlockWorld.VisibilityNormalAngleRad, 40, 0, 0, out var reason));
        Assert.Equal(NotVisibleReason.TooSmall, reason);
    }

    // ------------------------------------------------------------------ M14-004

    /// <summary>
    /// M14-004, RecentlyReacted 0x00611DD0..0x00611E14: [this+0x40] is a binary32 time (ctor -1); not recent when last &lt;= -1 (vmov.f32 s2,#-1; vcmpe; ble); else
    /// vadd.f32 (last + 60.0f, 0x42700000) greater than now in binary32. For last = 0.1f the f32 sum is 60.1f (0x42700CCD... = 60.099998), which is NOT greater than the
    /// clock reading 60.099998 (as a float), where double arithmetic (0.1f widened + 60 = 60.1000000015) said it was.
    /// </summary>
    [Fact]
    public void M14_004_RecentlyReactedComparesInBinary32()
    {
        Assert.False(PetInitialDetectionStrategy.RecentlyReactedF(-1f, -100f));
        Assert.True(PetInitialDetectionStrategy.RecentlyReactedF(0f, 59.9f));
        Assert.False(PetInitialDetectionStrategy.RecentlyReactedF(0f, 60f));
        Assert.False(PetInitialDetectionStrategy.RecentlyReactedF(0.1f, 60.1f));
        Assert.True(PetInitialDetectionStrategy.RecentlyReactedF(0.1f, 60.0999f));
        Assert.False(PetInitialDetectionStrategy.RecentlyReactedF(float.NaN, 0f));

        using var strategy = new PetInitialDetectionStrategy(new PetWorld());
        Assert.False(strategy.RecentlyReacted(0));                                  // ctor -1.0f
        strategy.BehaviorDidReact(Array.Empty<int>(), 0.1);
        Assert.False(strategy.RecentlyReacted((double)60.1f));
        Assert.True(strategy.RecentlyReacted(60.0));
    }

    // ------------------------------------------------------------------ M14-005

    /// <summary>
    /// M14-005, Robot::ComputeTurnTowardsImagePointAngles 0x0051879C (numpy float32 emulation, nominal 290/290/160/120): du = 200.5 - 160 = 40.5, dv = 33.25 - 120 = -86.75,
    /// head = atan2f(86.75, 290) + 0.1 = 0.3906657 (vadd.f32 0x00518878) and body = atan2f(-40.5, 290) + 0.4 = 0.2612423 (0x0084C99C); atan2f is the libm stand-in so the tolerance is 2e-6.
    /// </summary>
    [Fact]
    public void M14_005_TheImagePointAnglesAreBinary32()
    {
        var cal = CameraCalibration.Nominal();
        var (body, head) = TurnTowardsImagePoint.Angles(cal, 200.5, 33.25, 0.4, 0.1);
        // the float and double worlds are kept apart: the result is exactly a binary32 value, and its bits are those of the instruction-by-instruction float emulation
        // (head 0x3EC80556, body 0x3E85C18C) to within one ulp for the libm atan2f stand-in
        Assert.Equal(body, (double)(float)body);
        Assert.Equal(head, (double)(float)head);
        Assert.InRange(Math.Abs((long)BitConverter.SingleToUInt32Bits((float)head) - 0x3EC80556L), 0, 1);
        Assert.InRange(Math.Abs((long)BitConverter.SingleToUInt32Bits((float)body) - 0x3E85C18CL), 0, 1);
    }

    // ------------------------------------------------------------------ M14-003

    /// <summary>
    /// M14-003, ITrackAction/TrackFaceAction constructor 0x005646BC..0x0056473A: tolerance 0x3D0EFA35 (+0x84, +0x8C), max head 0x3F46D3F2 (+0x94), sound threshold 0x3E32B8C2 (+0xC0, +0xC8).
    /// The 0.01 speed clamp is removed; the wall-clock Task.Delay(60) loop is NOT the engine's mechanism (no ActionList) and is reported MISSING.
    /// </summary>
    [Fact]
    public void M14_003_TheTrackFaceConstantsAreTheConstructorsWords()
    {
        Assert.Equal(0x3D0EFA35u, TrackFaceAction.MinToleranceBits);
        Assert.Equal(0x3F46D3F2u, TrackFaceAction.MaxHeadAngleBits);
        Assert.Equal(0x3E32B8C2u, TrackFaceAction.MinAngleForSoundBits);
        Assert.Equal((double)F(0x3F46D3F2), TrackFaceAction.MaxHeadAngleRad);
        using var rig = new Rig();
        var track = new TrackFaceAction(rig.Vision, 1);
        Assert.Equal((double)F(0x3D0EFA35), track.PanToleranceRad);
        Assert.Equal((double)F(0x3D0EFA35), track.TiltToleranceRad);
        track.Dispose();
    }

    /// <summary>
    /// M14-003, ITrackAction::CheckIfDone 0x00565672..0x005656B4: the body speed is min(|pan| / [+0xD4] (0.4), 0x40A78D36); 1.0 rad gives 2.5, 3.0 gives the cap 5.2359877 (0x40A78D36).
    /// The TurnInPlace call carries accel 0x436DFFDB and tolerance [+0x84], which PanAndTilt now sends when given them.
    /// </summary>
    [Fact]
    public void M14_003_TheTrackingBodyTurnIsCappedAndCarriesItsOwnAccelAndTolerance()
    {
        Assert.Equal(2.5f, TrackFaceAction.PanSpeed(1.0));
        Assert.Equal(0x40A78D36u, BitConverter.SingleToUInt32Bits(TrackFaceAction.PanSpeed(3.0)));
        Assert.Equal(0x40A78D36u, BitConverter.SingleToUInt32Bits(TrackFaceAction.PanSpeed(double.NaN)));
        Assert.Equal(0x436DFFDBu, TrackFaceAction.BodyTurnAccelBits);
        using var rig = new Rig();
        rig.Vision.PanTiltOverride = null;                       // the rig's seam would swallow the messages
        var t = PanAndTilt.RunAsync(rig.Vision, 0.5, 0.1, 2.5, default, 1, 10000, waitForSettle: false,
                                    bodyAccelRadPerSec2: F(0x436DFFDB), bodyToleranceRad: 0.07);
        Assert.True(t.Result);
        rig.Pump();
        var m = rig.Sent.OfType<SetBodyAngle>().Last();
        Assert.Equal(0x436DFFDBu, BitConverter.SingleToUInt32Bits(m.AccelRadPerSec2));
        Assert.Equal(0.07f, m.ToleranceRad);
        Assert.Equal(2.5f, m.MaxSpeedRadPerSec);
    }
}
