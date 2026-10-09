using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// R-FIX batch 3 (M11): the corner line fit (M11-029), the decoder's missing contrast gate (M11-031), the calibration install result (M11-039), the origin-change delocalization stand-in
/// (M11-019, the corrected CORE008 tests in CoreReviewTests) and the producer path's gates in <c>UpdateVisionMarkers</c> (M11-036/M11-050). Expected values are never the implementation's
/// own output:
/// <list type="bullet">
/// <item>The M11-029 numbers come from running the shipped instructions. The vectors below were produced by emulating <c>JacobiSVDImpl_&lt;float&gt;</c> 0x00058FE8 and <c>SVBkSbImpl_&lt;float&gt;</c>
/// 0x00057110 of <c>libopencv_core.so</c> (and the intersection block 0x008A6AA8..0x008A6B66 and the s16 rounding block 0x008A6C30..0x008A6CF4 of <c>libcozmoEngine.so</c>) with Unicorn on the
/// shipped ARM code, <c>sqrt</c> and <c>ceilf</c>/<c>floorf</c> served exactly. The glue of <c>cv::solve</c> (transpose, argument order) is read from 0x00059B78 and is not emulated.</item>
/// <item>The gates are the cited instruction behaviours (<c>0x00654D60..0x006550AC</c>), driven through the live frame path (<c>VisionSystem.ProcessCapture</c>).</item>
/// </list>
/// </summary>
[Collection("ObjectID process statics")]
public class RFixBatch3Tests : IDisposable
{
    public RFixBatch3Tests() => ObjectIdSpace.ResetForTests();
    public void Dispose() => ObjectIdSpace.ResetForTests();

    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;
    private static readonly CameraCalibration Cal = CameraCalibration.Nominal();

    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);

    private static bool NeedsLibrary()
    {
        if (Lib is not null) return true;
        if (Environment.GetEnvironmentVariable("COZMO_TESTS_WITHOUT_ASSETS") == "1") return false;
        throw new Xunit.Sdk.XunitException("the marker library is missing, so this test cannot run; set COZMO_TESTS_WITHOUT_ASSETS=1 for a run that knowingly has none");
    }

    // ------------------------------------------------------------------ M11-029: the line fit (cv::solve, DECOMP_SVD, float)

    /// <summary>
    /// M11-029, 0x008A68E0 <c>cv::solve(A, b, x, 1)</c> on float matrices: A rows <c>(u, 1.0f)</c>, b <c>v</c>, <c>x</c> = (slope, intercept) as binary32, with the swapped flag
    /// 0x008A6714 choosing u. Each row is (case, swapped, slope bits, intercept bits, W0 bits, W1 bits) from the emulation of the shipped OpenCV instructions (see the class summary).
    /// "identical5" and "twoSame" are the rank-deficient cases: their second singular value is exactly zero, so they run the random-vector branch of JacobiSVDImpl (RNG 0x12345678,
    /// 0x00059428) and the solution is the one OpenCV returns, not a minimum-norm formula.
    /// </summary>
    private static readonly (string Name, bool Swapped, uint A, uint B, uint W0, uint W1)[] FitVectors =
    {
        ("horizontal", false, 0x32C21638U, 0x419FFFFFU, 0x43CB5485U, 0x3FEC4A1DU),
        ("vertical", true, 0x350E231CU, 0x429FFFFCU, 0x43A9180EU, 0x400E10B9U),
        ("diagonal", false, 0x3F7FB012U, 0x3EBB5183U, 0x4314E5B1U, 0x40508453U),
        ("noisy", false, 0x3EBD79F1U, 0xC1D402B2U, 0x44DF7FEAU, 0x4014C9E7U),
        ("steep", true, 0x3E4CB766U, 0x424670BEU, 0x44AF00E9U, 0x40C0EEF0U),
        ("identical5", false, 0x40341531U, 0x3E701C40U, 0x41D76815U, 0x00000000U),
        ("two", false, 0x3F7FFFFFU, 0x3F80000AU, 0x41192374U, 0x3F207B71U),
        ("twoSame", false, 0x3F7AE148U, 0x3E0F5C29U, 0x41200000U, 0x00000000U),
        ("large", false, 0x3E124B3AU, 0x44F9F21DU, 0x4624F1C4U, 0x3E8ED171U),
        ("scatter", false, 0x3CBEDEE0U, 0x42925114U, 0x44F939E3U, 0x410B56F3U),
        ("sameX", true, 0x3550C1DCU, 0x409FFFF2U, 0x41608DA7U, 0x3E32B8A0U),
        ("negcoords", false, 0x3EAD0608U, 0xC2BA04FDU, 0x44D2DFC9U, 0x3FEC8555U),
    };

    private static List<QuadCorners.BoundaryPoint> Points(string name)
    {
        var pts = new List<(int X, int Y)>();
        switch (name)
        {
            case "horizontal": for (int x = 30; x <= 80; x++) pts.Add((x, 20)); break;
            case "vertical": for (int y = 20; y <= 70; y++) pts.Add((80, y)); break;
            case "diagonal": for (int i = 0; i <= 40; i++) pts.Add((i, i + (i % 3 == 0 ? 1 : 0))); break;
            case "noisy": for (int i = 0; i < 120; i++) pts.Add((100 + i, 11 + (37 * i) / 100 + ((i * 7919) % 5) - 2)); break;
            case "steep": for (int y = 10; y <= 180; y++) pts.Add((50 + (y * 2) / 10, y)); break;
            case "identical5": for (int i = 0; i < 5; i++) pts.Add((12, 34)); break;
            case "two": pts.Add((3, 4)); pts.Add((9, 10)); break;
            case "twoSame": pts.Add((7, 7)); pts.Add((7, 7)); break;
            case "large": for (int i = 0; i < 101; i++) pts.Add((1000 + i, 2000 + (1000 + i) / 7)); break;
            case "scatter": for (int i = 0; i < 300; i++) pts.Add(((i * 37) % 200, (i * 53 + (i * i) % 11) % 150)); break;
            case "sameX": foreach (int y in new[] { 7, 8, 9 }) pts.Add((5, y)); break;
            case "negcoords": for (int i = 0; i < 60; i++) pts.Add((-300 + i * 3, -200 + (i * i) % 13 + i)); break;
            default: throw new ArgumentException(name);
        }
        return pts.Select(p => new QuadCorners.BoundaryPoint((short)p.X, (short)p.Y)).ToList();
    }

    [Fact]
    public void M11_029_TheLineFitIsTheShippedOpenCvFloatSvdSolve()
    {
        foreach (var v in FitVectors)
        {
            var boundary = Points(v.Name);
            var fit = QuadCorners.FitCluster(boundary, new int[boundary.Count], 0);
            Assert.NotNull(fit);
            Assert.Equal(v.Swapped, fit!.Value.Swapped);
            Assert.True(v.A == Bits(fit.Value.A), $"{v.Name}: slope 0x{Bits(fit.Value.A):X8}, binary 0x{v.A:X8}");
            Assert.True(v.B == Bits(fit.Value.B), $"{v.Name}: intercept 0x{Bits(fit.Value.B):X8}, binary 0x{v.B:X8}");
        }
    }

    /// <summary>M11-029: the singular values JacobiSVDImpl returns (the float conversion of the double W, 0x0005939C..0x000593C8) for the same systems.</summary>
    [Fact]
    public void M11_029_TheJacobiSingularValuesAreTheBinarys()
    {
        foreach (var v in FitVectors)
        {
            var pts = Points(v.Name);
            bool swapped = v.Swapped;
            int m = pts.Count;
            var at = new float[2 * m];
            for (int k = 0; k < m; k++)
            {
                at[k] = swapped ? pts[k].Y : pts[k].X;
                at[m + k] = 1.0f;
            }
            var w = new float[2]; var vt = new float[4];
            OpenCvSolveSvd.JacobiSvd(at, w, vt, m, 2, 2);
            Assert.True(v.W0 == Bits(w[0]), $"{v.Name}: W0 0x{Bits(w[0]):X8}, binary 0x{v.W0:X8}");
            Assert.True(v.W1 == Bits(w[1]), $"{v.Name}: W1 0x{Bits(w[1]):X8}, binary 0x{v.W1:X8}");
        }
    }

    /// <summary>M11-029: a cluster of fewer than two points makes the extraction fail (<c>cmp r1, #5</c> on the byte size, 0x008A66BA); an under-determined solve is cv::solve's CV_Error (0x00059D14).</summary>
    [Fact]
    public void M11_029_AClusterOfOnePointFailsAndTheSolveRefusesUnderDeterminedSystems()
    {
        var one = new List<QuadCorners.BoundaryPoint> { new(5, 6) };
        Assert.Null(QuadCorners.FitCluster(one, new[] { 0 }, 0));
        Assert.Throws<ArgumentException>(() => OpenCvSolveSvd.Solve(new[] { 1f, 1f }, new[] { 1f }, 1, 2));
    }

    /// <summary>
    /// M11-029, the engine words this build passes to JacobiSVDImpl and SVBkSb, as their bit patterns: eps = FLT_EPSILON*2 = 0x34800000 (0x0005A110), minval = FLT_MIN = 0x3810000000000000
    /// (literal 0x0005A3A8), the SVBkSb threshold factor 0x3CC0000000000000 (literal 0x000571A6), and the RNG seed 0x12345678 (0x000595B0).
    /// </summary>
    [Fact]
    public void M11_029_TheOpenCvConstantsAreTheirBitPatterns()
    {
        Assert.Equal(0x34800000u, Bits(OpenCvSolveSvd.JacobiEps));
        Assert.Equal(0x3810000000000000L, BitConverter.DoubleToInt64Bits(OpenCvSolveSvd.JacobiMinVal));
        Assert.Equal(0x3CC0000000000000L, BitConverter.DoubleToInt64Bits(OpenCvSolveSvd.SvbkEps));
    }

    /// <summary>
    /// M11-029, the line intersections 0x008A6AA8..0x008A6B66 in binary32. Each row is (a_i, b_i, swapped_i, a_j, b_j, swapped_j, y bits, x bits) as the instructions return them in r0/r1
    /// (emulated from the shipped code). The parallel rows divide by zero: the engine's binary32 division gives +inf (and NaN for 0/0), which the bounds test then rejects.
    /// </summary>
    private static readonly (uint Ai, uint Bi, bool Si, uint Aj, uint Bj, bool Sj, uint Y, uint X)[] IntersectVectors =
    {
        (0x3EBD70A4U, 0x4131999AU, false, 0xC02CCCCDU, 0x42BA999AU, false, 0x41A80E02U, 0x41D633B3U),
        (0x3EBD70A4U, 0x4131999AU, true, 0xC02CCCCDU, 0x42BA999AU, true, 0x41D633B3U, 0x41A80E02U),
        (0x3FA00000U, 0xC0900000U, false, 0x3A83126FU, 0x42490000U, false, 0x42492CE4U, 0x422F571DU),
        (0x3DCCCCCDU, 0x4222CCCDU, true, 0xBF4CCCCDU, 0x43484CCDU, false, 0x431B5098U, 0x4260ED0AU),
        (0xBCF5C28FU, 0x429A3333U, true, 0x3FF33333U, 0xC149999AU, false, 0x42FD56F0U, 0x4292998DU),
        (0x3F666666U, 0xC0533333U, false, 0x3D4CCCCDU, 0x42F1CCCDU, true, 0x42DCF69EU, 0x42FCD922U),
        (0xBFD9999AU, 0x43A6A666U, false, 0x3E4CCCCDU, 0x41780000U, true, 0x43651130U, 0x427540F3U),
        (0x3F000000U, 0x40400000U, false, 0x3F000000U, 0x40E00000U, false, 0x7F800000U, 0x7F800000U),
        (0x3F000000U, 0x40400000U, false, 0x3F000000U, 0x40400000U, false, 0x7FC00000U, 0x7FC00000U),
        (0x40000000U, 0x3F800000U, false, 0x3F000000U, 0x40800000U, true, 0x7F800000U, 0x7F800000U),
    };

    [Fact]
    public void M11_029_TheLineIntersectionsAreBinary32InTheEnginesOrder()
    {
        foreach (var v in IntersectVectors)
        {
            var (y, x) = QuadCorners.Intersect(new QuadCorners.LineFit(F(v.Ai), F(v.Bi), v.Si), new QuadCorners.LineFit(F(v.Aj), F(v.Bj), v.Sj));
            AssertSameFloat(F(v.Y), y, $"y of 0x{v.Ai:X8},0x{v.Bi:X8},{v.Si} / 0x{v.Aj:X8},0x{v.Bj:X8},{v.Sj}");
            AssertSameFloat(F(v.X), x, $"x of 0x{v.Ai:X8},0x{v.Bi:X8},{v.Si} / 0x{v.Aj:X8},0x{v.Bj:X8},{v.Sj}");
        }
    }

    private static void AssertSameFloat(float expected, float actual, string what)
    {
        if (float.IsNaN(expected)) Assert.True(float.IsNaN(actual), what);          // a NaN's payload and sign are the platform's, not the engine's
        else Assert.True(Bits(expected) == Bits(actual), $"{what}: 0x{Bits(actual):X8}, binary 0x{Bits(expected):X8}");
    }

    /// <summary>
    /// M11-029, the s16 rounding of each corner 0x008A6C1E..0x008A6C7C. Each row is (the binary32 coordinate bits, the s16 the shipped instructions store), emulated from the shipped block.
    /// 0.49999997f + 0.5f rounds to exactly 1.0f in binary32, so it becomes 1 (a double add would give 0); 40000 and -40000 clamp to 32767 and -32768.
    /// </summary>
    private static readonly (uint Bits, int S16)[] RoundVectors =
    {
        (0x3EFFFFFFU, 1), (0x3F000000U, 1), (0xBF000000U, -1), (0xBF000001U, -1), (0x3FC00000U, 2), (0xBFC00000U, -2),
        (0x40200000U, 3), (0xC0200000U, -3), (0x00000000U, 0), (0x80000000U, 0), (0x3ECCCCCDU, 0), (0xBECCCCCDU, 0),
        (0x471C4000U, 32767), (0xC71C4000U, -32768), (0x46FFFE00U, 32767), (0x46FFFECDU, 32767), (0xC7000000U, -32768),
        (0xC7000066U, -32768), (0x46FFFD00U, 32767), (0x0DA24260U, 0), (0x46FFFF00U, 32767), (0xC6FFFF00U, -32768),
    };

    [Fact]
    public void M11_029_TheS16RoundingIsBinary32()
    {
        foreach (var (bits, s16) in RoundVectors)
            Assert.Equal((float)s16, QuadCorners.RoundToS16(F(bits)));
        Assert.Equal(0xC7000000u, Bits(-32768.0f));
        Assert.Equal(0x46FFFE00u, Bits(32767.0f));
    }

    // ------------------------------------------------------------------ the live path: a frame through VisionSystem.ProcessCapture

    /// <summary>The robot, a calibrated vision system and a connected cube, fed states, image IMU samples and rendered frames as the camera path does.</summary>
    private sealed class LiveRig : IDisposable
    {
        public readonly CozmoRobot Robot = CozmoRobot.CreateOffline();
        public readonly VisionSystem Vision;
        public readonly List<string> Log = new();
        public uint T = 1000;
        private ushort _seq = 1;

        public LiveRig()
        {
            Deliver(new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), _seq++));
            Vision = new VisionSystem(Robot, Cal) { Enabled = false };
            Vision.Log += l => { lock (Log) Log.Add(l); };
            ObjectIdSpace.SeedUniqueIdForTests(ObjectType.Block_LIGHTCUBE1, 7);
            Send(new ObjectConnectionState { ObjectID = 1, FactoryID = 0xABCD, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true });
        }

        public void Send(RobotMessage m) => Deliver(new SubMessage(ReliableMessageType.SingleReliableMessage, m.ToBytes(), _seq++));

        private void Deliver(SubMessage sm)
        {
            var f = new Cozmo.Protocol.Frame { Type = ReliableMessageType.MultipleMixedMessages, SeqMin = sm.Seq, SeqMax = sm.Seq, Ack = 0, Messages = new List<SubMessage> { sm } };
            Robot.Transport.ProcessIncoming(FrameCodec.Encode(f));
        }

        /// <summary>One frame of the cube, with the image IMU sample the camera delivers with it (null: none), through the camera path (<c>ProcessCapture</c>).</summary>
        public VisionFrameResult? Frame(Pose3d? cube, float rateY = 0, float rateZ = 0, bool imu = true, uint originId = 0)
        {
            Send(new RobotState { Timestamp = T, PoseOriginId = originId, Pose = new RobotPose { X = 0, Y = 0, Angle = 0 }, HeadAngle = -0.15f, Status = (uint)RobotStatusFlag.HeadInPos,
                                  Accel = new AccelData { Z = 9800 }, Gyro = new GyroData() });
            if (imu) Send(new ImageImuData { ImageId = T, RateX = 0, RateY = rateY, RateZ = rateZ, Line2Number = 0 });
            var pd = Vision.History.At(T)!.Value;
            var frame = new GrayImage(Cal.Columns, Cal.Rows);
            frame.Fill(150);
            if (cube is { } c) MarkerRenderer.DrawCube(frame, Lib!, new CameraModel(Cal, pd.CameraPose), ObjectType.Block_LIGHTCUBE1, c);
            var r = Vision.ProcessCapture(frame, T, T);
            T += 33;
            return r;
        }

        public VisionFrameResult? Observe(Pose3d? cube, float rateY = 0, float rateZ = 0, bool imu = true, uint originId = 0)
        {
            Frame(cube, rateY, rateZ, imu, originId);
            return Frame(cube, rateY, rateZ, imu, originId);
        }

        public void Dispose() { Vision.Dispose(); Robot.Dispose(); }
    }

    private static Pose3d CubeAhead() => new(Mat3.Identity, new Vec3(150, 0, CubeGeometry.CubeSizeMm / 2));

    /// <summary>
    /// M11-023 / M11-029 / M11-031 through the live entry: a rendered cube in a frame goes through <c>ProcessCapture</c>, the marker front end (quad, line fit, corners, refinement, decode) and
    /// reaches the world; the located object is the cube, and the decoded markers are the rendered cube's.
    /// </summary>
    [Fact]
    public void M11_023_ARenderedCubeIsDetectedAndLocatedThroughTheCameraPath()
    {
        if (!NeedsLibrary()) return;
        using var rig = new LiveRig();
        var r = rig.Observe(CubeAhead());
        Assert.NotNull(r);
        Assert.NotEmpty(r!.Markers);
        Assert.All(r.Markers, m => Assert.Contains(m.Code, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1).Select(k => k.Code)));
        Assert.Single(rig.Vision.World.LocatedObjects, o => o.Type == ObjectType.Block_LIGHTCUBE1);
    }

    // ------------------------------------------------------------------ M11-036 / M11-050: the gates of UpdateVisionMarkers

    /// <summary>
    /// M11-050 (c), NOT BUILT (MISSING): the engine's rotation gate needs ImuDataHistory samples keyed as CalculateTimestampForImageIMU 0x00538C00 keys them (image timestamp, 65.0f, line2Number), which
    /// this stack cannot supply (image timestamp 0 on fw2457; its samples are keyed by ImageId). A stack-side limitation must not become a drop: frames with markers are kept whether or not an image IMU
    /// sample arrived and whatever its rates, so the cube is located.
    /// </summary>
    [Fact]
    public void M11_050_TheUnbuiltRotationGateNeverDropsAFrame()
    {
        if (!NeedsLibrary()) return;
        using var rig = new LiveRig();
        rig.Observe(CubeAhead(), imu: false);
        Assert.Single(rig.Vision.World.LocatedObjects);
    }

    /// <summary>
    /// M11-019 (0x00512F14..0x00512F96): the 101st consecutive state whose pose frame id differs from robot+0x2B0 (<c>cmp r0,#0x65; blo</c>) logs "Robot.UpdateFullRobotState.MismatchedFrameIDs",
    /// clears the counter and calls Robot::Delocalize, after the state went into the history. The first 100 mismatches delocalize nothing. Driven through the live state path (RobotState messages).
    /// </summary>
    [Fact]
    public void M11_019_The101stMismatchedFrameIdDelocalizes()
    {
        if (!NeedsLibrary()) return;
        using var rig = new LiveRig();
        rig.Observe(CubeAhead());
        Assert.Single(rig.Vision.World.LocatedObjects);
        int delocalized = 0;
        var engineLog = new List<string>();
        rig.Robot.Engine.LogLine += l => { lock (engineLog) engineLog.Add(l); };
        rig.Vision.RobotDelocalized += _ => delocalized++;
        void Mismatch() { rig.T += 33; rig.Send(new RobotState { Timestamp = rig.T, PoseFrameId = 5, Pose = new RobotPose(), HeadAngle = -0.15f, Status = (uint)RobotStatusFlag.HeadInPos, Accel = new AccelData { Z = 9800 }, Gyro = new GyroData() }); }
        for (int i = 0; i < 100; i++) Mismatch();
        Assert.Equal(0, delocalized);
        Assert.Single(rig.Vision.World.LocatedObjects);
        Mismatch();
        Assert.Equal(1, delocalized);
        Assert.Empty(rig.Vision.World.LocatedObjects);
        // 00512F40 calls sErrorF (not an unlevelled log); native format at00BE697E.
        Assert.Contains(engineLog, l => l == "error: Robot.UpdateFullRobotState.MismatchedFrameIDs: Robot[5] and engine[0] frameIDs are mismatched, delocalizing");
        for (int i = 0; i < 100; i++) Mismatch();                    // the counter was cleared: another 100 do nothing
        Assert.Equal(1, delocalized);
    }

    /// <summary>
    /// M11-050 (0x00654E56, <c>Robot::IsPoseInWorldOrigin</c> 0x0051290C compares the pose's root with <c>[[robot+0x294]+0x14]</c>, the world origin): a computed state taken from a state whose origin
    /// is not the robot's current one drops the frame, with the OldOrigin log (0x00654FDA..: "VisionComponent.UpdateVisionMarkers.OldOrigin"). The stack cannot allocate a second origin
    /// (<c>AddNewOrigin</c> is unbuilt, M11-053), so the origin list gets the second id the way <c>Robot::Delocalize</c> would add it; the offline link's accept-any-origin seam is switched off.
    /// </summary>
    [Fact]
    public void M11_050_AStateFromAnotherOriginThanTheWorldsIsDropped()
    {
        if (!NeedsLibrary()) return;
        using var rig = new LiveRig();
        var er = rig.Robot.Engine.Robot!;
        er.OfflineSeamAcceptsAnyOrigin = false;
        var origins = (List<uint>)typeof(EngineRobot).GetField("_origins", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(er)!;
        Assert.Equal(1u, er.CurrentOriginId);
        // the current origin: processed
        rig.Observe(CubeAhead(), originId: 1);
        Assert.Single(rig.Vision.World.LocatedObjects);
        ObjectIdSpace.ResetForTests();
        // another origin that the pose-origin list holds: dropped, and logged
        origins.Add(2);
        var r = rig.Frame(CubeAhead(), originId: 2);
        Assert.NotEmpty(r!.Markers);
        Assert.Empty(r.Objects);
        Assert.Contains(rig.Log, l => l.StartsWith("VisionComponent.UpdateVisionMarkers.OldOrigin"));
    }

    /// <summary>
    /// M11-050 (0x00654D96..0x00654DE6): when no raw state brackets the timestamp, ComputeAndInsertStateAt fails and the engine logs "VisionComponent.UpdateVisionMarkers.HistoricalPoseNotFound"
    /// with the frame timestamp and GetOldestTimeStamp/GetNewestTimeStamp (0x00532188, 0x00532196: 0 when the raw map is empty), then returns without calling UpdateObservedMarkers.
    /// </summary>
    [Fact]
    public void M11_050_AFailedComputedStateLogsTheHistoryRangeAndDropsTheFrame()
    {
        if (!NeedsLibrary()) return;
        using var rig = new LiveRig();
        // states at 1000, then a frame stamped 5000: nothing at or after 5000
        rig.Send(new RobotState { Timestamp = 1000, Pose = new RobotPose(), HeadAngle = -0.15f, Status = (uint)RobotStatusFlag.HeadInPos, Accel = new AccelData { Z = 9800 }, Gyro = new GyroData() });
        rig.Send(new ImageImuData { ImageId = 5000, RateX = 0, RateY = 0, RateZ = 0, Line2Number = 0 });
        var pd = rig.Vision.History.At(1000)!.Value;
        var frame = new GrayImage(Cal.Columns, Cal.Rows);
        frame.Fill(150);
        MarkerRenderer.DrawCube(frame, Lib!, new CameraModel(Cal, pd.CameraPose), ObjectType.Block_LIGHTCUBE1, CubeAhead());
        Assert.Equal(1000u, rig.Vision.History.OldestTimeStamp);
        Assert.Equal(1000u, rig.Vision.History.NewestTimeStamp);
        var r = rig.Vision.ProcessCapture(frame, 5000, 5000);
        // the pose pairing itself fails (no state near 5000 is not the engine's failure; History.At is the nearest), so the history range is what the failure logs
        Assert.NotNull(r);
        Assert.Contains(rig.Log, l => l == "VisionComponent.UpdateVisionMarkers.HistoricalPoseNotFound: Time: 5000, hist: 1000 to 1000");
        Assert.Empty(r!.Objects);
    }

    // ------------------------------------------------------------------ M11-039: the calibration install result

    /// <summary>
    /// M11-039, <c>VisionSystem::UpdateCameraCalibration</c> 0x006B1E3E: <c>Camera::SetCalibration</c> (defined at 0x0085DEDC) returns 1 only for a non-null pointer that is not the one already
    /// stored (0x0085DEE8..0x0085DEEC compare the two pointers, not their values), and only a non-zero result reaches <c>MarkerDetector::Init</c> (<c>cbz r6</c> 0x006B1E6C, call 0x006B1E78).
    /// A null pointer when one is already stored logs "Camera.SetCalibration.NullCalibration" (0x0085DF0C..0x0085DF12) and installs nothing.
    /// </summary>
    /// <summary>Puts non-shipped parameters back into the detector (the setter is private; MarkerDetector::Init is what restores them).</summary>
    private static void Dirty(VisionSystem vision) =>
        typeof(QuadDetector).GetProperty("Parameters")!.SetValue(vision.Detector.Quads, new QuadDetectorParameters { MinQuadArea = 999 });

    [Fact]
    public void M11_039_OnlyASuccessfulSetCalibrationReinitialisesTheDetector()
    {
        using var robot = CozmoRobot.CreateOffline();
        var modified = new QuadDetector(new QuadDetectorParameters { MinQuadArea = 999 });
        using var vision = new VisionSystem(robot, null, new MarkerDetector(modified, new MarkerDecoder()));
        var logs = new List<string>();
        vision.Log += logs.Add;
        Assert.Null(vision.Calibration);
        Assert.Equal(999, vision.Detector.Quads.Parameters.MinQuadArea);

        // null with nothing stored: SetCalibration returns 0 without the log (0x0085DEEE: cbz r6 -> return 0), Init is not run
        vision.UpdateCameraCalibration(null);
        Assert.Empty(logs);
        Assert.Equal(999, vision.Detector.Quads.Parameters.MinQuadArea);
        Assert.Null(vision.Calibration);

        // a new pointer: stored, Init runs (the shipped minQuadArea 25)
        var first = CameraCalibration.Nominal();
        vision.UpdateCameraCalibration(first);
        Assert.Same(first, vision.Calibration);
        Assert.Equal(25, vision.Detector.Quads.Parameters.MinQuadArea);

        // the SAME pointer: SetCalibration returns 0 (0x0085DEE8..0x0085DEEC), Init is not run
        Dirty(vision);
        vision.UpdateCameraCalibration(first);
        Assert.Equal(999, vision.Detector.Quads.Parameters.MinQuadArea);

        // a different pointer with the same values: stored, Init runs
        var second = CameraCalibration.Nominal();
        Assert.NotSame(first, second);
        vision.UpdateCameraCalibration(second);
        Assert.Same(second, vision.Calibration);
        Assert.Equal(25, vision.Detector.Quads.Parameters.MinQuadArea);

        // null with a calibration stored: the VERIFY log, nothing installed, Init not run
        Dirty(vision);
        vision.UpdateCameraCalibration(null);
        Assert.Contains(logs, l => l.StartsWith("Camera.SetCalibration.NullCalibration"));
        Assert.Same(second, vision.Calibration);
        Assert.Equal(999, vision.Detector.Quads.Parameters.MinQuadArea);
    }
}
