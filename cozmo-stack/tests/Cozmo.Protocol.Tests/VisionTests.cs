using System.Text.RegularExpressions;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M11: marker detection, the nearest-neighbour decoder over the engine's own library, camera and cube
/// geometry, pose estimation, the world model's located/visible semantics and the real cube locator under the
/// M10 cube-moved reaction. Synthetic frames are rendered from the extracted library images, so every test is
/// offline; the hardware-facing pieces (NV calibration, SetBodyAngle) are exercised at the message level.
/// </summary>
public class VisionTests
{
    /// <summary>
    /// The marker library is Anki's data, extracted locally from the user's libcozmoEngine.so by the build;
    /// without it the tests that need it return early, like the OBB-dependent tests do.
    /// </summary>
    private static readonly MarkerLibrary? LibOrNull = MarkerLibrary.EmbeddedOrNull;
    private static MarkerLibrary Lib => LibOrNull!;
    private static bool NoLibrary => LibOrNull is null;

    private static double Deg(double rad) => rad * 180 / Math.PI;

    // ------------------------------------------------------------------ library

    [Fact]
    public void TheEmbeddedLibraryHasTheEnginesShape()
    {
        if (NoLibrary) return;
        Assert.Equal(598, Lib.NumImages);
        Assert.Equal(150, Lib.NumLabels);
        Assert.Equal(598 * 1024, Lib.Images.Length);
        // every label but INVALID has four images; INVALID has two
        for (int l = 0; l < Lib.NumLabels; l++)
            Assert.Equal(l == MarkerLibrary.InvalidLabel ? 2 : 4, Lib.RowsForLabel(l).Count());
        // every cube marker code has four labels, one per rotation
        foreach (var t in new[] { ObjectType.Block_LIGHTCUBE1, ObjectType.Block_LIGHTCUBE2, ObjectType.Block_LIGHTCUBE3 })
            foreach (var m in CubeGeometry.CubeMarkers(t))
            {
                var labels = Lib.LabelsForCode(m.Code).ToList();
                Assert.Equal(4, labels.Count);
                Assert.Equal(new[] { 0f, 90f, 180f, 270f }, labels.Select(l => Lib.OrientationDeg[l]).OrderBy(x => x));
            }
        // the probe grid is 32 equally spaced columns and rows inside the square
        Assert.Equal(7149, Lib.ProbeCentersX[0]);
        Assert.Equal(25619, Lib.ProbeCentersY[31]);
        Assert.Equal(new short[] { 0, 205, 0, -205, 0 }, Lib.ProbePointsX);
        Assert.Equal(new short[] { 0, 0, 205, 0, -205 }, Lib.ProbePointsY);
    }

    [Fact]
    public void TheLibraryFileMatchesTheToolsLayout()
    {
        // the committed blob is what the extraction tool writes; the loader must reject anything else
        Assert.Throws<FormatException>(() => MarkerLibrary.Parse(new byte[32]));
    }

    // ------------------------------------------------------------------ decoder

    [Theory]
    [InlineData(MarkerType.LightCubeI_Front)]
    [InlineData(MarkerType.LightCubeJ_Top)]
    [InlineData(MarkerType.LightCubeK_Left)]
    [InlineData(MarkerType.Charger)]
    [InlineData(MarkerType.Star5)]
    [InlineData(MarkerType.Sdk3Hexagons)]
    public void ARenderedMarkerDecodesToItsOwnCode(MarkerType code)
    {
        if (NoLibrary) return;
        foreach (var label in Lib.LabelsForCode(code))
        {
            int row = Lib.RowsForLabel(label).First();
            var img = MarkerRenderer.Render(Lib, row, 160);
            var corners = new[] { new Vec2(0, 0), new Vec2(0, 160), new Vec2(160, 0), new Vec2(160, 160) };
            var dec = new MarkerDecoder(Lib);
            var m = dec.Extract(img, corners, 1, out var reason);
            Assert.True(m is not null, $"label {label}: {reason}");
            Assert.Equal(code, m!.Code);
            Assert.Equal(Lib.OrientationDeg[label], m.OrientationDeg);
            Assert.True(m.Distance < 20, $"distance {m.Distance}");
        }
    }

    [Fact]
    public void EveryLibraryRowDecodesToItsLabel()
    {
        if (NoLibrary) return;
        var dec = new MarkerDecoder(Lib);
        int wrong = 0;
        for (int row = 0; row < Lib.NumImages; row++)
        {
            var img = MarkerRenderer.Render(Lib, row, 96);
            var probes = dec.GetProbeValues(img, Homography.FromUnitSquare(new[] { new Vec2(0, 0), new Vec2(0, 96), new Vec2(96, 0), new Vec2(96, 96) }));
            var m = dec.GetNearestNeighbor(probes);
            if (m.Label != Lib.Labels[row]) wrong++;
        }
        Assert.Equal(0, wrong);
    }

    [Fact]
    public void ARotatedMarkerDecodesWithTheRotatedLabelAndReorderedCorners()
    {
        if (NoLibrary) return;
        // draw the orientation-0 image of a code, but hand the decoder the quad rotated a quarter turn:
        // the library's 90/180/270 labels exist precisely so the code still decodes, and the corners come back
        // reordered so that TL is the marker's own top-left
        int row = MarkerRenderer.RowForCode(Lib, MarkerType.LightCubeI_Front, 0);
        var img = MarkerRenderer.Render(Lib, row, 160);
        var dec = new MarkerDecoder(Lib);
        var tl = new Vec2(0, 0); var bl = new Vec2(0, 160); var tr = new Vec2(160, 0); var br = new Vec2(160, 160);
        var straight = dec.Extract(img, new[] { tl, bl, tr, br }, 1, out _)!;
        foreach (var rotated in new[] { new[] { bl, br, tl, tr }, new[] { br, tr, bl, tl }, new[] { tr, tl, br, bl } })
        {
            var m = dec.Extract(img, rotated, 1, out var reason);
            Assert.True(m is not null, reason);
            Assert.Equal(MarkerType.LightCubeI_Front, m!.Code);
            Assert.Equal(straight.Corners, m.Corners);
        }
    }

    [Fact]
    public void ABlankQuadIsRejected()
    {
        if (NoLibrary) return;
        var img = new GrayImage(100, 100);
        img.Fill(200);
        var dec = new MarkerDecoder(Lib);
        var m = dec.Extract(img, new[] { new Vec2(0, 0), new Vec2(0, 99), new Vec2(99, 0), new Vec2(99, 99) }, 1, out var reason);
        Assert.Null(m);
        Assert.Contains("border", reason);
    }

    // ------------------------------------------------------------------ front end

    [Fact]
    public void TheQuadDetectorFindsARenderedMarkerToSubPixelAccuracy()
    {
        if (NoLibrary) return;
        var frame = new GrayImage(320, 240);
        frame.Fill(140);
        var corners = new[] { new Vec2(100.3, 60.6), new Vec2(96.2, 150.1), new Vec2(190.7, 70.4), new Vec2(200.5, 158.9) };
        MarkerRenderer.Draw(frame, Lib, MarkerRenderer.RowForCode(Lib, MarkerType.LightCubeI_Top), corners);
        var det = new QuadDetector();
        var quads = det.Detect(frame);
        Assert.True(quads.Count >= 1, det.LastStats.ToString());
        var best = quads.OrderBy(q => q.Corners.Zip(corners, (a, b) => (a - b).Length).Max()).First();
        for (int i = 0; i < 4; i++)
            Assert.True((best.Corners[i] - corners[i]).Length < 1.0, $"corner {i}: {best.Corners[i]} vs {corners[i]}");
    }

    [Fact]
    public void TheDetectorDecodesMarkersAtSeveralSizesAndPlaces()
    {
        if (NoLibrary) return;
        var det = new MarkerDetector(new QuadDetector(), new MarkerDecoder(Lib));
        foreach (var (code, corners) in new[]
        {
            (MarkerType.LightCubeI_Front, new[] { new Vec2(20, 20), new Vec2(22, 60), new Vec2(58, 18), new Vec2(60, 62) }),
            (MarkerType.LightCubeJ_Back, new[] { new Vec2(200, 100), new Vec2(195, 220), new Vec2(300, 110), new Vec2(305, 215) }),
            (MarkerType.LightCubeK_Right, new[] { new Vec2(120, 90), new Vec2(120, 130), new Vec2(160, 90), new Vec2(160, 130) }),
        })
        {
            var frame = new GrayImage(320, 240);
            frame.Fill(120);
            MarkerRenderer.Draw(frame, Lib, MarkerRenderer.RowForCode(Lib, code), corners);
            var markers = det.Detect(frame, 1);
            Assert.True(markers.Any(m => m.Code == code), $"{code}: found [{string.Join(",", markers.Select(m => m.Code))}]; " +
                                                            string.Join("; ", det.LastQuads.Select(q => q.Reason)) + " " + det.Quads.LastStats);
        }
    }

    [Fact]
    public void TwoMarkersInOneFrameAreBothFound()
    {
        if (NoLibrary) return;
        var frame = new GrayImage(320, 240);
        frame.Fill(150);
        MarkerRenderer.Draw(frame, Lib, MarkerRenderer.RowForCode(Lib, MarkerType.LightCubeI_Front), new[] { new Vec2(30, 60), new Vec2(30, 130), new Vec2(100, 60), new Vec2(100, 130) });
        MarkerRenderer.Draw(frame, Lib, MarkerRenderer.RowForCode(Lib, MarkerType.LightCubeJ_Front), new[] { new Vec2(200, 80), new Vec2(200, 140), new Vec2(260, 80), new Vec2(260, 140) });
        var det = new MarkerDetector(new QuadDetector(), new MarkerDecoder(Lib));
        var markers = det.Detect(frame, 1);
        Assert.Contains(markers, m => m.Code == MarkerType.LightCubeI_Front);
        Assert.Contains(markers, m => m.Code == MarkerType.LightCubeJ_Front);
    }

    [Fact]
    public void ARoomWithoutMarkersYieldsNothing()
    {
        if (NoLibrary) return;
        var frame = new GrayImage(320, 240);
        var rnd = new Random(7);
        for (int y = 0; y < 240; y++)
            for (int x = 0; x < 320; x++)
                frame[x, y] = (byte)Math.Clamp(120 + 60 * Math.Sin(x / 23.0) + 40 * Math.Cos(y / 17.0) + rnd.Next(-20, 20), 0, 255);
        var det = new MarkerDetector(new QuadDetector(), new MarkerDecoder(Lib));
        Assert.Empty(det.Detect(frame, 1));
    }

    // ------------------------------------------------------------------ geometry

    [Fact]
    public void CubeMarkerNormalsPointOutOfTheirFaces()
    {
        foreach (var m in CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1))
        {
            var centre = m.PoseOnObject.Translation;
            var normal = m.NormalOnObject;
            Assert.True(normal.Dot(centre.Normalized()) > 0.99, $"{m.Face}: normal {normal} vs centre {centre}");
            // the marker's corners lie on the face plane, 22 mm from the centre
            foreach (var c in m.CornersOnObject()) Assert.InRange(Math.Abs(c.Dot(centre.Normalized())), 21.9, 22.1);
        }
        Assert.Equal(MarkerType.LightCubeI_Front, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1).First(m => m.Face == BlockFace.Front).Code);
        Assert.Equal(MarkerType.LightCubeI_Bottom, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1).First(m => m.Face == BlockFace.Bottom).Code);
        Assert.Equal(MarkerType.LightCubeK_Top, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE3).First(m => m.Face == BlockFace.Top).Code);
    }

    [Fact]
    public void TheCameraLooksForwardAndSlightlyDownAtHeadAngleZero()
    {
        var cam = HeadGeometry.CameraPoseInRobot(0);
        var axis = cam.Rotation * new Vec3(0, 0, 1);       // optical axis in the robot frame
        Assert.InRange(axis.X, 0.99, 1.0);
        Assert.InRange(Deg(Math.Asin(-axis.Z)), 3.9, 4.1);  // 4 degrees down
        Assert.InRange(cam.Translation.X, 4.4, 4.6);         // -13 + 17.52
        Assert.InRange(cam.Translation.Z, 66.4, 66.6);       // 49 + 17.52
        // image right is the robot's right (-Y), image down is down
        var right = cam.Rotation * new Vec3(1, 0, 0);
        Assert.InRange(right.Y, -1.0, -0.99);
        var down = cam.Rotation * new Vec3(0, 1, 0);
        Assert.True(down.Z < -0.99);
        // raising the head 20 degrees lifts the optical axis by 20 degrees
        var up = HeadGeometry.CameraPoseInRobot(20 * Math.PI / 180).Rotation * new Vec3(0, 0, 1);
        Assert.InRange(Deg(Math.Asin(up.Z)), 15.9, 16.1);
    }

    [Fact]
    public void PoseEstimationRecoversAKnownCubePose()
    {
        var cal = CameraCalibration.Nominal();
        var robot = HeadGeometry.RobotPose(new RobotPose { X = 10, Y = -5, Angle = 0.2f });
        var camera = new CameraModel(cal, HeadGeometry.CameraPoseInWorld(robot, -0.1));
        var cube = new Pose3d(Mat3.AboutZ(0.35), new Vec3(150, 20, 22));
        var marker = CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1).First(m => m.Face == BlockFace.Front);
        var world = marker.CornersInWorld(cube);
        var px = world.Select(w => camera.Project(w)!.Value).ToList();
        var res = PoseEstimation.Solve(camera, marker.CornersOnObject(), px);
        Assert.NotNull(res);
        var recovered = camera.Pose.Compose(res!.ObjectInCamera);
        Assert.True((recovered.Translation - cube.Translation).Length < 1.0, $"{recovered.Translation} vs {cube.Translation}");
        Assert.True(Deg(recovered.Rotation.AngularDistance(cube.Rotation)) < 0.5, $"angle off by {Deg(recovered.Rotation.AngularDistance(cube.Rotation)):F2} deg");
        Assert.True(res.RmsReprojectionPx < 0.01);
    }

    [Fact]
    public void DistortionRoundTrips()
    {
        var cal = CameraCalibration.Nominal() with { DistortionCoefficients = new[] { -0.1, 0.02, 0.001, -0.001, 0.0, 0, 0, 0 } };
        var cam = new CameraModel(cal, Pose3d.Identity);
        foreach (var p in new[] { new Vec2(10, 10), new Vec2(160, 120), new Vec2(300, 230), new Vec2(50, 200) })
        {
            var n = cam.Undistort(p);
            var back = cam.Distort(n);
            Assert.True((back - p).Length < 1e-6, $"{p} -> {n} -> {back}");
        }
    }

    // ------------------------------------------------------------------ calibration

    [Fact]
    public void TheCalibrationStructRoundTripsAndChunkedNvResultsAssemble()
    {
        var cal = new CameraCalibration { FocalLengthX = 290.5, FocalLengthY = 291.2, CenterX = 158.7, CenterY = 121.3, Skew = 0, Rows = 240, Columns = 320,
                                          DistortionCoefficients = new[] { -0.05, 0.01, 0, 0, 0, 0, 0, 0 } };
        var bytes = cal.ToBytes();
        Assert.Equal(CameraCalibration.WireSize, bytes.Length);
        var back = CameraCalibration.Parse(bytes);
        Assert.Equal(cal.FocalLengthX, back.FocalLengthX, 3);
        Assert.Equal(240, back.Rows);
        Assert.Equal(-0.05, back.DistortionCoefficients[0], 6);

        using var robot = CozmoRobot.CreateOffline();
        using var reader = new NvCalibrationReader(robot);
        Assert.Null(reader.Handle(new NVOpResult { Tag = CameraCalibration.NvEntryTag, Op = 0, Result = NvCalibrationReader.ResultMore, Data = bytes[..20] }));
        var got = reader.Handle(new NVOpResult { Tag = CameraCalibration.NvEntryTag, Op = 0, Result = NvCalibrationReader.ResultOkay, Data = bytes[20..] });
        Assert.NotNull(got);
        Assert.Equal(291.2, got!.FocalLengthY, 3);
        // another tag is ignored
        Assert.Null(reader.Handle(new NVOpResult { Tag = 0x80000002, Result = 0, Data = bytes }));
        Assert.Throws<FormatException>(() => CameraCalibration.Parse(new byte[CameraCalibration.WireSize]));
    }

    /// <summary>
    /// The NV read request, as ProcessRequest 0x00644FD4 builds it: the engine's own tag, a non-zero
    /// length - the entry's maximum size, which for any tag the factory size table does not name is the
    /// 0x400 at 0x0064536A - the READ op, and a second byte the component's constructor zeroes and never
    /// writes again (0x006428AA). This stack sent a length of zero.
    /// </summary>
    [Fact]
    public void TheNvReadRequestAsksForTheEntrysMaximumSize()
    {
        Assert.Equal(0x80000001u, CameraCalibration.NvEntryTag);
        Assert.Equal(0x400, NvCalibrationReader.NvReadLength);

        using var robot = CozmoRobot.CreateOffline();
        using var reader = new NvCalibrationReader(robot);
        robot.Transport.OfflineOutbound.Clear();
        _ = reader.ReadAsync(TimeSpan.FromMilliseconds(1));
        robot.Transport.OfflineTick();

        List<NVCommand> Commands() => robot.Transport.OfflineOutbound
            .SelectMany(f => f.Messages)
            .Where(sm => sm.Type is ReliableMessageType.SingleReliableMessage
                                 or ReliableMessageType.SingleUnreliableMessage)
            .Select(sm => { try { return RobotMessage.Parse(sm.Payload); } catch { return null; } })
            .OfType<NVCommand>()
            .ToList();
        var end = DateTime.UtcNow.AddSeconds(2);
        while (Commands().Count == 0 && DateTime.UtcNow < end) { robot.Transport.OfflineTick(); Thread.Sleep(2); }
        var cmd = Commands()[0];
        Assert.Equal(CameraCalibration.NvEntryTag, cmd.Tag);
        Assert.Equal(NvCalibrationReader.NvReadLength, cmd.Length);
        Assert.Equal(NvCalibrationReader.OpRead, cmd.Op);
        Assert.Equal(0, cmd.Unknown);
        Assert.Empty(cmd.Data);
    }

    [Fact]
    public void TheSetBodyAngleMessagePacksTheEnginesFieldOrder()
    {
        var m = TurnTowardsPose.Message(1.5, 2.0, 3.0, 0.0349066, 0, true, 7);
        var bytes = m.ToBytes();
        Assert.Equal(1.5f, BitConverter.ToSingle(bytes, 1));
        Assert.Equal(2.0f, BitConverter.ToSingle(bytes, 5));
        Assert.Equal(3.0f, BitConverter.ToSingle(bytes, 9));
        Assert.Equal(0.0349066f, BitConverter.ToSingle(bytes, 13));
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 17));
        Assert.Equal(1, bytes[19]);
        Assert.Equal(7, bytes[20]);
        Assert.Equal(21, bytes.Length);
    }

    // ------------------------------------------------------------------ world model

    /// <summary>An offline robot with a connected cube 1, a calibration, and a vision system fed synthetic frames.</summary>
    private sealed class WorldRig : IDisposable
    {
        public readonly CozmoRobot Robot = CozmoRobot.CreateOffline();
        public readonly VisionSystem Vision;
        public readonly CameraCalibration Cal = CameraCalibration.Nominal();
        public uint T = 1000;
        private ushort _seq = 1;
        public readonly List<string> Log = new();

        public WorldRig(bool connectCube = true)
        {
            Deliver(new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), _seq++));
            Vision = new VisionSystem(Robot, Cal) { Enabled = false };
            Vision.World.Log += Log.Add;
            Vision.Log += Log.Add;
            if (connectCube) Send(new ObjectConnectionState { ObjectID = 7, FactoryID = 0xABCD, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true });
        }

        public void Send(RobotMessage m) => Deliver(new SubMessage(ReliableMessageType.SingleReliableMessage, m.ToBytes(), _seq++));

        private void Deliver(SubMessage sm)
        {
            var f = new Frame { Type = ReliableMessageType.MultipleMixedMessages, SeqMin = sm.Seq, SeqMax = sm.Seq, Ack = 0, Messages = new List<SubMessage> { sm } };
            Robot.Transport.ProcessIncoming(FrameCodec.Encode(f));
        }

        /// <summary>One robot state at the current time with a pose and head angle.</summary>
        public void State(float x = 0, float y = 0, float angle = 0, float head = 0, RobotStatusFlag flags = 0, float gz = 0)
        {
            Send(new RobotState { Timestamp = T, Pose = new RobotPose { X = x, Y = y, Angle = angle }, HeadAngle = head, Status = (uint)flags,
                                  Accel = new AccelData { Z = 9800 }, Gyro = new GyroData { Z = gz } });
        }

        /// <summary>Renders what the camera would see of a cube at <paramref name="cube"/> (or an empty frame) and processes it.</summary>
        public VisionFrameResult Frame(Pose3d? cube, float x = 0, float y = 0, float angle = 0, float head = 0, RobotStatusFlag flags = 0, float gz = 0)
        {
            State(x, y, angle, head, flags, gz);
            var pd = Vision.History.At(T)!.Value;
            var frame = new GrayImage(Cal.Columns, Cal.Rows);
            frame.Fill(150);
            if (cube is { } c) MarkerRenderer.DrawCube(frame, Lib, new CameraModel(Cal, pd.CameraPose), ObjectType.Block_LIGHTCUBE1, c);
            var r = Vision.ProcessImage(frame, T, T, pd);
            T += 33;
            return r;
        }

        public void Dispose() { Vision.Dispose(); Robot.Dispose(); }
    }

    /// <summary>A cube sitting on the floor in front of the robot, its front face towards the camera.</summary>
    private static Pose3d CubeAhead(double distanceMm = 150, double yMm = 0, double yawRad = 0) =>
        new(Mat3.AboutZ(yawRad), new Vec3(distanceMm, yMm, CubeGeometry.CubeSizeMm / 2));

    [Fact]
    public void ASeenConnectedCubeBecomesLocatedAtItsPose()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        var cube = CubeAhead(140, 10, 0.1);
        var r = rig.Frame(cube, head: -0.15f);
        Assert.True(r.Markers.Count >= 1, string.Join("; ", rig.Log));
        Assert.Contains(r.Markers, m => m.Code == MarkerType.LightCubeI_Front);
        Assert.Single(r.Objects);
        var o = r.Objects[0].Object;
        Assert.Equal(7u, o.ObjectId);
        Assert.Equal(PoseState.Known, o.PoseState);
        Assert.True(o.IsLocated);
        Assert.True((o.Pose.Translation - cube.Translation).Length < 5, $"{o.Pose.Translation} vs {cube.Translation} ({string.Join("; ", rig.Log)})");
        Assert.True(Deg(o.Pose.Rotation.AngularDistance(cube.Rotation)) < 3, $"yaw {Deg(o.Pose.AngleAroundZ):F1}");
        Assert.Equal(UpAxis.ZPositive, o.UpAxisFromPose());
        Assert.NotNull(rig.Vision.World.GetLocatedObjectById(7));
        Assert.True(rig.Vision.Locator.IsLocated(7));
        Assert.InRange(rig.Vision.Locator.DistanceFromRobotMm(7)!.Value, 130, 155);
        Assert.True(rig.Vision.Locator.IsVisibleFromCamera(7));
    }

    [Fact]
    public void AnUnconnectedCubeIsSeenButNotAdded()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig(connectCube: false);
        var r = rig.Frame(CubeAhead(), head: -0.15f);
        Assert.NotEmpty(r.Markers);
        Assert.Empty(r.Objects);
        Assert.Contains(rig.Log, l => l.Contains("not connected"));
        Assert.Empty(rig.Vision.World.Objects);
        // the offline switch the tools use
        rig.Vision.World.AllowUnconnectedObjects = true;
        var r2 = rig.Frame(CubeAhead(), head: -0.15f);
        Assert.Single(r2.Objects);
        Assert.Equal(1u, r2.Objects[0].Object.ObjectId);
    }

    /// <summary>
    /// A cube whose pose is already Dirty and which is not where it should be is forgotten after two
    /// misses. This is the first of the two cases in <c>CheckForUnobservedObjects</c>: not visible, with
    /// nothing behind it, and Dirty (the branch at 0x0062211E). An empty view is exactly "nothing
    /// behind" - the occluder list holds only what the camera saw this frame.
    /// </summary>
    [Fact]
    public void ADirtyCubeThatIsNotWhereItShouldBeIsForgottenAfterTwoMisses()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        Assert.True(o.IsLocated);
        rig.Vision.World.MarkDirty(7);                 // as an ObjectMoved from the cube does
        // same view, cube gone
        var r1 = rig.Frame(null, head: -0.15f);
        Assert.Empty(r1.Forgotten);
        Assert.Equal(1, o.UnobservedCount);
        Assert.True(o.IsLocated);
        var r2 = rig.Frame(null, head: -0.15f);
        Assert.Single(r2.Forgotten);
        Assert.Equal(PoseState.Unknown, o.PoseState);
        Assert.False(rig.Vision.Locator.IsLocated(7));
        Assert.Null(rig.Vision.World.GetLocatedObjectById(7));
    }

    /// <summary>
    /// A cube whose pose is still Known and which vanishes from an otherwise empty view is <b>not</b>
    /// forgotten. Neither case applies: it is not visible, because with nothing in the occluder list
    /// <c>KnownMarker::IsVisibleFrom</c> comes back NOTHING_BEHIND (0x0087E87A), and the other case
    /// wants a Dirty pose. The robot has to see through to something behind where the cube was, or have
    /// been told the cube moved, before it gives the cube up.
    /// </summary>
    [Fact]
    public void AKnownCubeThatVanishesFromAnEmptyViewIsKept()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        Assert.Equal(PoseState.Known, o.PoseState);

        for (int i = 0; i < 4; i++) Assert.Empty(rig.Frame(null, head: -0.15f).Forgotten);
        Assert.Equal(PoseState.Known, o.PoseState);
        Assert.Equal(0, o.UnobservedCount);
        Assert.True(rig.Vision.Locator.IsLocated(7));
    }

    [Fact]
    public void ACubeOutOfViewIsNotMarkedUnobserved()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        // robot turned 90 degrees away: the cube is behind the field of view, not "should be visible"
        for (int i = 0; i < 5; i++) rig.Frame(null, angle: 1.57f, head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        Assert.Equal(0, o.UnobservedCount);
        Assert.True(o.IsLocated);
        Assert.False(rig.Vision.Locator.IsVisibleFromCamera(7));
    }

    [Fact]
    public void UnobservedChecksAreSkippedWhileMovingOrRotatingFast()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        for (int i = 0; i < 4; i++) rig.Frame(null, head: -0.15f, flags: RobotStatusFlag.IsMoving);
        Assert.Equal(0, o.UnobservedCount);
        for (int i = 0; i < 4; i++) rig.Frame(null, head: -0.15f, gz: 0.5f);
        Assert.Equal(0, o.UnobservedCount);
        Assert.True(o.IsLocated);
    }

    [Fact]
    public void AMovedReportMakesTheCubeDirtyAndASightingMakesItKnownAgain()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        var changes = new List<(PoseState, PoseState)>();
        rig.Vision.World.PoseStateChanged += (_, a, b) => changes.Add((a, b));
        rig.Send(new ObjectMoved { Timestamp = rig.T, ObjectID = 7, AxisOfAccel = UpAxis.ZPositive });
        Assert.Equal(PoseState.Dirty, o.PoseState);
        Assert.True(o.IsLocated);                 // Dirty still counts as located, as the cube-moved strategy needs
        rig.Frame(CubeAhead(160, 0, 0.3), head: -0.15f);
        Assert.Equal(PoseState.Known, o.PoseState);
        Assert.Equal(new[] { (PoseState.Known, PoseState.Dirty), (PoseState.Dirty, PoseState.Known) }, changes);
        Assert.True((o.Pose.Translation - CubeAhead(160, 0, 0.3).Translation).Length < 6);
    }

    [Fact]
    public void ACubeSeenByTwoFacesIsOneObjectWithAJointPose()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        // yawed 40 degrees so the front and right faces both face the camera
        var cube = CubeAhead(130, -10, -0.7);
        var r = rig.Frame(cube, head: -0.2f);
        Assert.True(r.Markers.Count >= 2, $"{r.Markers.Count} markers: {string.Join(",", r.Markers.Select(m => m.Code))} {rig.Vision.Detector.Quads.LastStats}");
        Assert.Single(r.Objects);
        Assert.Equal(2, r.Objects[0].Markers.Count);
        Assert.True((r.Objects[0].Object.Pose.Translation - cube.Translation).Length < 5, $"{r.Objects[0].Object.Pose.Translation} vs {cube.Translation}");
    }

    [Fact]
    public void TheRealLocatorFiresTheCubeMovedReactionOnlyWhenTheCubeIsOutOfSight()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var behavior = new AcknowledgeCubeMovedBehavior(rig.Vision.Locator);
        using var strategy = new CubeMovedReactionStrategy(rig.Robot, behavior, rig.Vision.Locator);
        Assert.True(strategy.HasLocator);
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };

        // the world model saw the cube: the strategy's record is marked observed
        strategy.ObjectObserved(7);
        // the cube starts moving while the camera is still on it: no reaction (IsVisibleFrom is true)
        rig.Send(new ObjectMoved { Timestamp = rig.T, ObjectID = 7, AxisOfAccel = UpAxis.ZPositive });
        rig.T += 1200;
        rig.State(head: -0.15f);
        Assert.True(rig.Vision.Locator.IsVisibleFromCamera(7));
        Assert.False(strategy.ShouldTrigger(ctx, null, 0));

        // the robot has turned away: the located pose is outside the camera's view
        rig.T += 33;
        rig.State(angle: 1.6f, head: -0.15f);
        Assert.True(rig.Vision.Locator.IsLocated(7));
        Assert.False(rig.Vision.Locator.IsVisibleFromCamera(7));
        Assert.True(strategy.ShouldTrigger(ctx, null, 0));
        Assert.Equal(7u, behavior.TargetObjectId);      // the behaviour is now runnable once its animations resolve (DerivedStateTests)
    }

    [Fact]
    public void TurnTowardsPoseComputesTheEnginesAngleAndHeadTilt()
    {
        var robot = HeadGeometry.RobotPose(new RobotPose { X = 100, Y = 50, Angle = 0.5f });
        var target = new Pose3d(Mat3.Identity, robot.Apply(new Vec3(200, 100, 22)));
        double turn = TurnTowardsPose.RelativeTurnRad(robot, target);
        Assert.Equal(Math.Atan2(100, 200), turn, 6);
        var cal = CameraCalibration.Nominal();
        double head = TurnTowardsPose.HeadAngleToSee(cal, robot, target.Translation);
        Assert.InRange(head, HeadGeometry.MinHeadAngleRad, HeadGeometry.MaxHeadAngleRad);
        // with that head angle the target projects to the image's middle row
        var cam = new CameraModel(cal, HeadGeometry.CameraPoseInWorld(robot, head));
        var turned = HeadGeometry.RobotPose(new RobotPose { X = 100, Y = 50, Angle = (float)(0.5 + turn) });
        var cam2 = new CameraModel(cal, HeadGeometry.CameraPoseInWorld(turned, head));
        var p = cam2.Project(target.Translation);
        Assert.NotNull(p);
        Assert.InRange(p!.Value.X, 150, 170);
        _ = cam;
    }

    // ------------------------------------------------------------------ AcknowledgeObject

    [Fact]
    public void ObjectPositionUpdatedFiresOnAFirstSightingAndAgainOnlyAfterAnEightyMillimetreMove()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        var behavior = new AcknowledgeObjectBehavior(rig.Vision.World);
        using var strategy = new ObjectPositionUpdatedStrategy(rig.Vision.World, behavior);
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        Assert.False(strategy.ShouldTrigger(ctx, null, 0));

        rig.Frame(CubeAhead(150), head: -0.15f);
        Assert.True(strategy.ShouldTrigger(ctx, null, 0));
        Assert.Equal(new uint[] { 7 }, behavior.PendingTargets);
        // not while the acknowledgement itself is running
        Assert.False(strategy.ShouldTrigger(ctx, ReactionTrigger.ObjectPositionUpdated, 0));

        // reacted: the same pose no longer triggers, even when seen again
        strategy.ReactedToId(7);
        rig.Frame(CubeAhead(152, 3), head: -0.15f);
        Assert.False(strategy.ShouldTrigger(ctx, null, 0));
        // a 40 mm shift is within the 80 mm tolerance
        rig.Frame(CubeAhead(150, 40), head: -0.15f);
        Assert.False(strategy.ShouldTrigger(ctx, null, 0));
        // 100 mm away is a new position
        rig.Frame(CubeAhead(250, 0), head: -0.05f);
        Assert.True(strategy.ShouldTrigger(ctx, null, 0));
        // a forgotten object is not a target
        strategy.ReactedToId(7);
        rig.Frame(CubeAhead(250, 0, 1.2), head: -0.05f);   // turned 69 degrees: over the 45 degree angle tolerance
        Assert.True(strategy.ShouldTrigger(ctx, null, 0));
        rig.Vision.World.MarkUnknown(7);
        Assert.False(strategy.ShouldTrigger(ctx, null, 0));
    }

    [Fact]
    public void AcknowledgeObjectTurnsVerifiesWithTwoImagesAndPlaysTheReaction()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(150), head: -0.15f);
        var turns = new List<(uint, double)>();
        rig.Vision.Locator.TurnOverride = (id, max, ct) => { turns.Add((id, max)); return Task.FromResult(true); };
        var behavior = new AcknowledgeObjectBehavior(rig.Vision.World, rig.Vision.Locator) { Clock = () => 0 };
        behavior.SetTargets(new uint[] { 7 });
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var reacted = new List<uint>();
        behavior.ReactedTo += reacted.Add;
        Assert.Equal(new uint[] { 7 }, behavior.PendingTargets);      // runnable once the animation library is loaded (IsRunnable gates on it)
        behavior.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Equal(AcknowledgeObjectBehavior.Phase.Turning, behavior.CurrentPhase);
        Assert.Equal(new[] { (7u, AcknowledgeObjectBehavior.MaxTurnAngleRad) }, turns);

        // the turn's completion is posted; the next tick takes it and starts the visual verification
        SpinUntil(() => { behavior.Update(ctx, 0); return behavior.CurrentPhase == AcknowledgeObjectBehavior.Phase.Verifying; });
        Assert.Equal(7u, behavior.CurrentTarget);
        // one sighting is not enough, two are (NumImagesToWaitFor = 2)
        rig.Frame(CubeAhead(150), head: -0.15f);
        behavior.Update(ctx, 33);
        Assert.Equal(AcknowledgeObjectBehavior.Phase.Verifying, behavior.CurrentPhase);
        rig.Frame(CubeAhead(150), head: -0.15f);
        behavior.Update(ctx, 66);
        // no animation library offline: the trigger play fails and the iteration finishes; the target counts as reacted to
        SpinUntil(() => { behavior.Update(ctx, 99); return reacted.Count == 1; });
        Assert.Equal(new uint[] { 7 }, reacted);
        Assert.Contains(behavior.Trace, l => l.Contains("VisuallyVerifyObjectAction"));
        Assert.Contains(behavior.Trace, l => l.Contains("FinishIteration"));
        behavior.Stop(BehaviorStopReason.Completed);
    }

    [Fact]
    public void AcknowledgeObjectGivesUpOnATargetTheWorldModelCannotLocate()
    {
        using var rig = new WorldRig();                 // runs without the marker library: the world model needs no decoder here
        var behavior = new AcknowledgeObjectBehavior(rig.Vision.World) { Clock = () => 0 };
        behavior.SetTargets(new uint[] { 42 });
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var reacted = new List<uint>();
        behavior.ReactedTo += reacted.Add;
        behavior.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.False(behavior.Update(ctx, 0));
        Assert.Equal(new uint[] { 42 }, reacted);
        Assert.Contains(behavior.Trace, l => l.Contains("can't get it from blockworld"));
    }

    private static void SpinUntil(Func<bool> cond, int ms = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cond()) { if (sw.ElapsedMilliseconds > ms) throw new TimeoutException("condition not met"); Thread.Sleep(5); }
    }

    // ------------------------------------------------------------------ the captured robot

    /// <summary>
    /// The fw2457 probe capture carries 28 camera frames of a room with no marker in it. The decoder must run over
    /// real JPEGs without error and must not hallucinate cube markers.
    /// </summary>
    [Fact]
    public void TheCapturedRoomFramesDecodeAndCarryNoCubeMarkers()
    {
        // without the library the frames still decode and the detector finds quads but decodes none; the
        // no-cube-marker claim holds either way
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hw_fw2457_probe.log");
        if (!File.Exists(path)) return;
        var line = new Regex(@"^(\S+) (TX|RX) ((?:[0-9a-f]{2} ?)+)$");
        using var robot = CozmoRobot.CreateOffline();
        var frames = new List<CameraFrame>();
        robot.Camera.FrameReceived += frames.Add;
        foreach (var raw in File.ReadLines(path))
        {
            var m = line.Match(raw.Trim('﻿'));
            if (!m.Success || m.Groups[2].Value != "RX") continue;
            try { robot.Transport.ProcessIncoming(Hex.Parse(m.Groups[3].Value)); } catch (FormatException) { }
        }
        Assert.True(frames.Count >= 20, $"only {frames.Count} frames");
        var det = new MarkerDetector();
        int cubeMarkers = 0, decoded = 0;
        foreach (var f in frames)
        {
            var gray = GrayImage.FromFrame(f);
            Assert.Equal(f.Width, gray.Width);
            decoded++;
            cubeMarkers += det.Detect(gray, f.Timestamp).Count(m => CubeGeometry.CubeTypeForMarker(m.Code) is not null);
        }
        Assert.Equal(frames.Count, decoded);
        Assert.Equal(0, cubeMarkers);
    }

    /// <summary>
    /// The clustering and flat-snap tolerances are the engine's, not this stack's.
    /// <c>ObservableObjectLibrary::CreateObjectsFromMarkers</c> passes 5 mm (0x40A00000 at 0x006254F4)
    /// and 0.0872665 rad (0x3DB2B8C3 at 0x00625498) to <c>ClusterObjectPoses</c>, and
    /// <c>ObjectPoseConfirmer::UpdatePoseInInstance</c> passes 0.349066 rad (0x3EB2B8C2 at 0x00505F16)
    /// to <c>ClampPoseToFlat</c>. This stack had 20 mm, 10 degrees and 8 degrees.
    /// </summary>
    [Fact]
    public void TheClusteringAndFlatSnapTolerancesAreTheEngines()
    {
        Assert.Equal(5.0, BlockWorld.ClusterDistanceMm);
        Assert.Equal(0.0872665, BlockWorld.ClusterAngleRad, 6);
        Assert.Equal(0.349066, BlockWorld.FlatClampAngleRad, 6);

        // a pose tilted by 15 degrees snaps, one tilted by 25 does not
        var flat = new Pose3d(Mat3.AboutZ(0.3), new Vec3(100, 0, 22));
        var tilted15 = new Pose3d(Mat3.AxisAngle(new Vec3(1, 0, 0), 15 * Math.PI / 180) * flat.Rotation, flat.Translation);
        var tilted25 = new Pose3d(Mat3.AxisAngle(new Vec3(1, 0, 0), 25 * Math.PI / 180) * flat.Rotation, flat.Translation);
        var snapped = BlockWorld.ClampPoseToFlat(tilted15);
        Assert.Equal(1.0, Math.Abs(snapped.Rotation[2, 2]), 4);
        var kept = BlockWorld.ClampPoseToFlat(tilted25);
        Assert.True(Math.Abs(kept.Rotation[2, 2]) < 0.95, "a 25 degree tilt is beyond the engine's 20 and must not snap");
    }
}
