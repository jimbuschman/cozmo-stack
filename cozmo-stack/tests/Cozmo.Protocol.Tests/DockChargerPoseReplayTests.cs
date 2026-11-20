using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class DockChargerPoseReplayTests(ITestOutputHelper output)
{
    private static readonly uint[] ImageIds = [18174, 18318, 18322, 18327, 18332, 18440, 18441, 18442, 18443, 18444, 18445, 18446, 18447, 18448, 18451, 18453, 18454, 18459, 18460];

    [Fact]
    public void RawCameraMotionUsesTheCommitTimestampAndFailsClosedWithoutHistory()
    {
        using var fixture = new Fixture();
        var robot = fixture.Robot;
        robot.PoseHistory.Clear();
        var logs = new List<string>();
        void Log(EngineLogLevel level, string? channel, string key, string text) => logs.Add(key + " " + text);
        var state = fixture.Rig.MakeState(RobotStatusFlag.HeadInPos,
            origin: robot.CurrentOriginId, frame: robot.PoseFrameId);
        state.Timestamp = 1000;
        Assert.Equal(0u, robot.AddIncomingStateToHistory(state));
        var moving = fixture.Rig.MakeState(RobotStatusFlag.HeadInPos | RobotStatusFlag.AreWheelsMoving,
            origin: robot.CurrentOriginId, frame: robot.PoseFrameId);
        moving.Timestamp = 1030;
        Assert.Equal(0u, robot.AddIncomingStateToHistory(moving));

        // The history's interpolation copies the earlier status below its 0x3EFFFEB0 boundary.
        Assert.False(VisionSystem.NativeWasCameraMoving(robot, 1010, Log));
        Assert.True(VisionSystem.NativeWasCameraMoving(robot, 1025, Log));
        Assert.True(VisionSystem.NativeWasCameraMoving(robot, 1030, Log));
        Assert.True(VisionSystem.NativeWasCameraMoving(robot, 1035, Log));
        Assert.Equal("MovementComponent.WasCameraMoving.GetHistoricalPoseFailed t=1035", Assert.Single(logs));
    }

    [Fact]
    public async Task RecordedChargerImagesAndPathReportsExplainTheFailedMount()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_DOCK_POSE_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;

        var input = ReadCapture(bundle);
        using var fixture = new Fixture(sourceNvEmpty: true, preloadAnimations: true, pumpAnimations: true, bodyHwVersion: input.BodyVersion,
            sourceNvRaw: tag => tag == CameraCalibration.NvEntryTag ? input.Calibration : null);
        var robot = fixture.Robot;
        fixture.Rig.Robot.SetEnableFreeplayActivity(false);
        fixture.Motors();
        var world = fixture.M.World;
        var calibration = fixture.M.Vision.Calibration!;
        Assert.All(calibration.DistortionCoefficients, value => Assert.Equal(0, value));
        var detector = new MarkerDetector();
        var origin = robot.GetNativeOrigin(robot.CurrentOriginId);
        ObservableObject? charger = null;
        Vec3 frozen = default;
        Rotation3d frozenRotation = default;

        foreach (uint imageId in ImageIds)
        {
            var (frame, imu) = input.Images[imageId];
            Assert.Equal(MiniJpeg.EncodingJpegMinimizedColor, frame.Encoding);
            Assert.True(frame.TryDecodeRgb(out var rgb, out var decodeError), decodeError);
            var gray = ImageCache.FillGray(rgb!, frame.Width, frame.Height);
            var detected = detector.Detect(VisionSystem.SelectMarkerImage(gray), frame.Timestamp);
            var marker = detected.SingleOrDefault(m => m.Code == MarkerType.Charger);
            if (imageId == ImageIds[0])
            {
                // Native detect found no charger quad in this stationary frame.
                Assert.Null(marker);
                continue;
            }
            Assert.NotNull(marker);

            var shifts = NativeRollingShutter.Compute(frame.Timestamp, frame.Height, imu, false, out _);
            Assert.True(NativeRollingShutter.TryCorrect(marker, frame.Width, frame.Height, shifts, out var corrected, out _));
            var before = input.States.Last(s => s.Timestamp <= frame.Timestamp);
            var after = input.States.First(s => s.Timestamp >= frame.Timestamp);
            Assert.Equal(before.PoseOriginId, after.PoseOriginId);
            Assert.Equal(before.PoseFrameId, after.PoseFrameId);
            robot.PoseHistory.Clear();
            Admit(before);
            if (before.Timestamp != after.Timestamp) Admit(after);
            using var history = new HistRobotState();
            Assert.Equal(0u, robot.PoseHistory.ComputeAndInsertKeyedState(frame.Timestamp, history, out uint resolved));
            Assert.Equal(frame.Timestamp, resolved);
            using var nativeCamera = robot.CopyHistoricalCameraPose(history);
            robot.SetPose(history.Pose);
            var metadata = RobotStateHistory.PoseData(resolved, history);
            using var dataPose = metadata.RobotPose;
            var camera = new CameraModel(calibration, nativeCamera);
            bool expectedMotion = imageId <= 18454;
            bool IsMoving(uint at) => VisionSystem.NativeWasCameraMoving(robot, at, (_, _, key, text) => output.WriteLine(key + " " + text));
            Assert.Equal(expectedMotion, IsMoving(frame.Timestamp));
            // Oppose the worker's cached status deliberately. A stock native commit
            // asks raw history again. The worker approximation cannot decide this update.
            var worldResult = world.UpdateObservedMarkers([corrected], camera, frame.Timestamp,
                metadata with { CameraMoving = !expectedMotion }, null, null, robot.PoseHistory.GetComputedNativePoseAt, IsMoving);
            Assert.Equal(0u, worldResult.Status);
            if (imageId == 18318)
            {
                Assert.Empty(worldResult.Objects);
                continue;
            }
            var observed = Assert.Single(worldResult.Objects);
            charger = observed.Object;
            var pose = charger.NativePose;
            double tilt = Math.Acos(Math.Clamp(pose.Rotation[2, 2], -1, 1)) * 180 / Math.PI;
            output.WriteLine($"image {imageId}, camera moving={expectedMotion}, pose {pose.Translation}, tilt={tilt:F3}");

            if (imageId == 18322)
            {
                frozen = pose.Translation;
                frozenRotation = pose.Quaternion;
                Assert.InRange(tilt, 17.49, 17.52);
                Assert.InRange((frozen - new Vec3(346.12933, 24.070286, 7.287867)).Length, 0, .05);
            }
            else if (expectedMotion)
            {
                Assert.Equal(frozen, pose.Translation);
                Assert.Equal(frozenRotation, pose.Quaternion);
                Assert.Equal(PoseState.Dirty, charger.PoseState);
            }
            else
            {
                Assert.NotEqual(frozen, pose.Translation);
                Assert.InRange(tilt, 0, .02);
                if (imageId == 18459)
                    Assert.InRange((pose.Translation - new Vec3(349.29752, 13.788105, -7.534779)).Length, 0, .05);
            }
            if (imageId == 18332) await ReplayMount(fixture, input, charger.ObjectId);
        }
        Assert.NotNull(charger);

        void Admit(RobotState packet)
        {
            packet.PoseFrameId = robot.PoseFrameId;
            packet.PoseOriginId = robot.CurrentOriginId;
            Assert.Equal(0u, robot.AddIncomingStateToHistory(packet));
        }
    }

    private static async Task ReplayMount(Fixture f, Capture input, uint chargerId)
    {
        var start = input.States.Single(state => state.Timestamp == 1175930);
        var arrival = input.States.Single(state => state.Timestamp == 1179440);
        var recordedEnds = input.Paths.Where(e => e.EventId == 15).DistinctBy(e => e.EventType).ToArray();
        Assert.Equal(new[] { PathEventType.Started, PathEventType.Completed }, recordedEnds.Select(e => (PathEventType)e.EventType));
        f.Robot.PoseHistory.Clear();
        Place(start);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var mcp = new McpServerTests.Client(bridge);
        await mcp.Initialize();
        int sent = f.Rig.Mark();
        var trace = new List<string>();
        f.Rig.Engine.LogEntry += Log;
        try
        {
            int call = await mcp.Send("tools/call", new { name = "mount-charger", arguments = new { chargerId } });
            var root = Assert.IsType<NativeDriveToMountChargerRunner>(f.Robot.ActionList.GetCurrentAction());
            ushort? pathId = null;
            byte lastAnimation = 0;
            for (int tick = 0; tick < 150 && f.Robot.ActionList.ContainsRunner(root); tick++)
            {
                // The fixture has no firmware clock. Echo actual animation stream
                // counters and acknowledgements, and replace only the path ID in the
                // captured Started/Completed feedback with the fixture's path ID.
                var stream = f.Rig.Robot.Animations.Scheduler.Stream;
                f.Rig.Data(new AnimationState { NumAudioFramesPlayed = stream.FramesStreamed, NumAnimBytesPlayed = stream.BytesStreamed });
                f.Motors();
                var messages = f.Rig.SentSince(sent);
                sent += messages.Count;
                foreach (var message in messages)
                {
                    if (message is StartOfAnimation startAnim)
                    {
                        lastAnimation = startAnim.AnimId;
                        f.Rig.Data(new AnimationStarted { AnimId = lastAnimation });
                    }
                    if (message is EndOfAnimation) f.Rig.Data(new AnimationEnded { AnimId = lastAnimation });
                    if (message is ExecutePath execute)
                    {
                        Assert.Null(pathId);
                        pathId = execute.EventId;
                        f.Rig.Data(new PathFollowingEvent { EventId = execute.EventId, EventType = recordedEnds[0].EventType });
                        Place(arrival);
                        f.Rig.Data(new PathFollowingEvent { EventId = execute.EventId, EventType = recordedEnds[1].EventType });
                    }
                }
                await Task.Delay(2);
            }
            Assert.NotNull(pathId);
            Assert.Equal(-1, f.Rig.Robot.Cubes.ChargerObjectId); // Mount.Init never ran.
            var result = (await mcp.Receive(call)).GetProperty("result").GetProperty("structuredContent");
            Assert.Equal((uint)ActionResult.FailedTraversingPath, result.GetProperty("engineResult").GetUInt32());
            var why = result.GetProperty("why");
            var check = why.GetProperty("snapshot").GetProperty("pathArrival");
            Assert.Equal(pathId, check.GetProperty("pathId").GetUInt16());
            Assert.InRange(check.GetProperty("rotationRad").GetSingle(), .3054f, .3060f);
            Assert.Equal(DriveToPoseAction.GoalAngleToleranceRad, check.GetProperty("rotationLimitRad").GetDouble(), 7);
            Assert.InRange(check.GetProperty("actualMm").GetProperty("x").GetDouble(), 195.426, 195.430);
            Assert.InRange(check.GetProperty("goalMm").GetProperty("z").GetDouble(), 62.72, 62.82);
            Assert.Contains("rejected the arrival rotation", why.GetProperty("hint").GetString());
            Assert.Contains(trace, line => line.Contains("DoneNotInPlace"));
        }
        finally { f.Rig.Engine.LogEntry -= Log; }

        void Log(EngineLogEntry entry) => trace.Add(entry.Name + " " + entry.Text);
        void Place(RobotState source)
        {
            f.X = source.Pose.X;
            f.Y = source.Pose.Y;
            f.Yaw = source.Pose.Angle;
            f.Head = source.HeadAngle;
            f.Lift = source.LiftAngle;
            f.Motors();
        }
    }

    private sealed record Capture(byte[] Calibration, uint BodyVersion, Dictionary<uint, (CameraFrame Frame, NativeImageImu[] Imu)> Images,
        List<RobotState> States, List<PathFollowingEvent> Paths);

    private static Capture ReadCapture(string bundle)
    {
        var camera = new CozmoCamera();
        var imu = new NativeImageImuHistory();
        var images = new Dictionary<uint, (CameraFrame Frame, NativeImageImu[] Imu)>();
        camera.FrameReceived += frame =>
        {
            imu.Associate(frame.ImageId, frame.Timestamp, frame.Height);
            if (ImageIds.Contains(frame.ImageId)) images.Add(frame.ImageId, (frame, imu.Snapshot()));
        };

        byte[]? calibration = null;
        uint? bodyVersion = null;
        var states = new List<RobotState>();
        var paths = new List<PathFollowingEvent>();
        foreach (var (at, message) in RecordedRobotInput.ReadInbound(bundle,
            time => time <= 30000 || time is >= 1148000 and <= 1176500,
            1176500, [0xC6, 0xCD, 0xED, 0xF0, 0xF2, 0xF4]))
        {
            switch (message)
            {
                case NVOpResult { Tag: CameraCalibration.NvEntryTag, Result: 0 } nv when at <= 30000:
                    calibration = nv.Data;
                    break;
                case ManufacturingID id when at <= 30000:
                    bodyVersion = id.BodyHwVersion;
                    break;
                case RobotState packet when at >= 1148000:
                    states.Add(packet);
                    break;
                case PathFollowingEvent path when at >= 1171700:
                    paths.Add(path);
                    break;
                case ImageImuData sample when at >= 1148000:
                    imu.Add(sample);
                    break;
                case ImageChunk chunk when at >= 1148000:
                    camera.Handle(chunk);
                    break;
            }
        }
        Assert.NotNull(calibration);
        Assert.NotNull(bodyVersion);
        Assert.Equal(ImageIds.Length, images.Count);
        return new(calibration, bodyVersion.Value, images, states.DistinctBy(s => s.Timestamp).OrderBy(s => s.Timestamp).ToList(), paths);
    }
}
