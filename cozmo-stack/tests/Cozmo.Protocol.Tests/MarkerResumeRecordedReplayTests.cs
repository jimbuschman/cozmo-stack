using System.Collections.Concurrent;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class MarkerResumeRecordedReplayTests(ITestOutputHelper output)
{
    [Fact]
    public void CollisionObstacleUsesTheCheckedMainParentAndExistingMarkerlessPose()
    {
        using var fixture = new Fixture();
        var robot = fixture.Robot;
        var world = fixture.M.World;
        var origin = robot.GetNativeOrigin(robot.CurrentOriginId);
        using var intermediate = new Pose3d(Rotation3d.Identity, new Vec3(100, 0, 0), "intermediate", origin);
        using var main = new Pose3d(Rotation3d.Identity, Vec3.Zero, "MAIN", intermediate);
        robot.SetPose(main);

        using var input = new Pose3d(Rotation3d.Identity, new Vec3(300, 50, 0), "collision input", origin);
        var obstacle = world.AddCollisionObstacle(input);
        Assert.True(obstacle.IsLocated);
        Assert.Same(intermediate.Node, obstacle.NativePose.Node!.Parent);
        Assert.Equal(300, obstacle.Pose.Translation.X); // SetParent changes the node, keeping its transform.
        Assert.Equal(50, obstacle.Pose.Translation.Y);
        using var rooted = obstacle.NativePose.GetWithRespectToRoot();
        Assert.Equal(400, rooted.Translation.X);
        Assert.Same(obstacle, world.AddCollisionObstacle(input)); // same-type pose overlap stamps the original
        Assert.Single(world.LocatedObjects, o => o.Type == ObjectType.CollisionObstacle);
    }

    private const uint RejectedTime = 746025;
    private static readonly (uint Image, uint Timestamp, MarkerType? Code, ObjectType? Type)[] Checkpoints =
    [
        (12003, 743850, MarkerType.LightCubeJ_Top, null), // build the initial cube confirmer entry
        (12004, 743915, MarkerType.LightCubeJ_Top, null),
        (12005, 743985, MarkerType.LightCubeJ_Top, ObjectType.Block_LIGHTCUBE2), // last cube report before the cancel
        (12035, RejectedTime, null, null), // recorded frame-ID boundary 11 -> 12
        (12203, 757375, null, null), // cube body in view
        (12940, 807215, null, null),
        (13053, 814850, null, null), // charger in view
        (14275, 897430, null, null), // centred charger, stationary interval
        (14325, 900840, null, null),
        (14400, 905905, null, null),
        (14432, 908065, MarkerType.Charger, null), // isolated decode before any charger report
        (14985, 945420, MarkerType.Charger, ObjectType.Charger_Basic), // first charger report in the capture
        (15136, 955635, MarkerType.LightCubeI_Back, null),
        (15140, 955905, MarkerType.LightCubeI_Back, ObjectType.Block_LIGHTCUBE1),
        (15141, 955970, MarkerType.LightCubeI_Back, ObjectType.Block_LIGHTCUBE1),
        (15413, 974360, MarkerType.Charger, null),
        (15418, 974710, MarkerType.Charger, ObjectType.Charger_Basic),
    ];

    [Fact]
    public void CapturedMarkerScheduleContinuesAndRawImagesExplainTheObservationGap()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_MARKER_RESUME_BUNDLE");
        if (string.IsNullOrWhiteSpace(bundle)) return;

        var images = Checkpoints.ToDictionary(checkpoint => checkpoint.Image, _ => new List<ImageChunk>());
        var imus = new List<(double At, ImageImuData Sample)>();
        var states = new SortedDictionary<uint, RobotState>();
        var received = new Dictionary<uint, double>();
        byte[]? calibration = null;
        uint? body = null;
        foreach (var (at, packet) in RecordedRobotInput.ReadInbound(bundle,
            time => time <= 30000 || time is >= 735000 and <= 973000, 973000, [0xCD, 0xED, 0xF0, 0xF2, 0xF4]))
        {
            switch (packet)
            {
                case NVOpResult { Tag: CameraCalibration.NvEntryTag, Result: 0 } nv:
                    calibration = nv.Data;
                    break;
                case ManufacturingID id:
                    body = id.BodyHwVersion;
                    break;
                case RobotState state when at > 735000:
                    states[state.Timestamp] = state;
                    break;
                case ImageImuData imu when at > 735000:
                    imus.Add((at, imu));
                    break;
                case ImageChunk chunk when images.TryGetValue(chunk.ImageId, out var chunks):
                    chunks.Add(chunk);
                    received[chunk.ImageId] = at;
                    break;
            }
        }
        Assert.NotNull(calibration);
        Assert.NotNull(body);

        const uint chargerTime = 945420;
        var markerFrames = new HashSet<uint>();
        var reportedObjects = new List<uint>();
        bool sawOriginMismatch = false;
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            var at = row.GetProperty("atMs").GetDouble();
            if (at > 973000) break;
            if (at < 741000) continue;
            if (row.TryGetProperty("line", out var log) &&
                log.GetString()!.Contains("VisionComponent.SetNextImage.OriginMismatch: Could not get pose data for t=746025", StringComparison.Ordinal))
                sawOriginMismatch = true;
            if (row.GetProperty("kind").GetString() != "engine-to-game") continue;
            var tag = row.GetProperty("tag").GetString();
            var payload = Convert.FromHexString(row.GetProperty("bodyHex").GetString()!);
            if (tag == "0x43")
            {
                var reader = new CladReader(payload);
                uint time = reader.U32();
                int count = reader.U8();
                bool ranMarkers = false;
                for (int i = 0; i < count; ++i)
                    ranMarkers |= reader.U32() == (uint)VisionMode.DetectingMarkers;
                Assert.True(ranMarkers, $"missing marker mode at t={time}");
                markerFrames.Add(time);
            }
            if (tag == "0x44")
                reportedObjects.Add(BitConverter.ToUInt32(payload));
        }
        Assert.True(sawOriginMismatch);
        Assert.DoesNotContain(RejectedTime, markerFrames);
        Assert.True(markerFrames.Count(time => time > RejectedTime && time < chargerTime) > 2700);
        Assert.Contains(chargerTime, reportedObjects);
        Assert.DoesNotContain(reportedObjects, time => time > 743985 && time < chargerTime);

        using var fixture = new Fixture(sourceNvEmpty: true, bodyHwVersion: body.Value,
            sourceNvRaw: tag => tag == CameraCalibration.NvEntryTag ? calibration : null);
        var rig = fixture.Rig;
        var robot = fixture.Robot;
        var vision = fixture.M.Vision;
        rig.Robot.SetEnableFreeplayActivity(false);
        // The capture confirms marker mode is enabled for every selected frame. Keep only
        // that mode in the fixture so face/quality work and their queues do not affect the replay.
        vision.ModeEnableMask = 1 << (int)VisionMode.DetectingMarkers;
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        var results = new ConcurrentQueue<VisionFrameResult>();
        var observed = new ConcurrentQueue<NativeObservedObject>();
        var logs = new ConcurrentQueue<string>();
        rig.Engine.LogEntry += entry => logs.Enqueue(entry.Name + ": " + entry.Text);
        vision.World.Log += logs.Enqueue;
        var processed = new ConcurrentQueue<ProcessedImageReport>();
        using var processedReports = rig.Engine.AnimationMessages.SubscribeMessage(0x43, true,
            message => processed.Enqueue(Assert.IsType<ProcessedImageReport>(message)));
        vision.FrameProcessed += results.Enqueue;
        using var reports = rig.Engine.AnimationMessages.SubscribeMessage(0x44, true,
            message => observed.Enqueue(Assert.IsType<NativeObservedObject>(message)));

        foreach (var (image, time, code, type) in Checkpoints)
        {
            bool discarded = time == RejectedTime;
            Assert.Equal(!discarded, markerFrames.Contains(time));
            if (type.HasValue) Assert.Contains(time, reportedObjects);
            robot.PoseHistory.Clear();
            var around = states.Values.Where(state => state.Timestamp >= time - 120 && state.Timestamp <= time + 35).ToArray();
            Assert.NotEmpty(around);
            Assert.Contains(around, state => state.Timestamp <= time);
            Assert.Contains(around, state => state.Timestamp >= time);
            foreach (var state in around)
            {
                // Rebase the recorded origin ID to the fixture's owned root. Keep the firmware
                // frame IDs so the 11 -> 12 boundary still rejects the straddling image.
                state.PoseOriginId = robot.CurrentOriginId;
                using var pose = new Pose3d(Rotation3d.FromAngleAxis(state.Pose.Angle, new Vec3(0, 0, 1)),
                    new Vec3(state.Pose.X, state.Pose.Y, state.Pose.Z), parent: robot.GetNativeOrigin(robot.CurrentOriginId));
                Assert.Equal(0u, robot.PoseHistory.AddRawOdomState(state.Timestamp, pose, state));
            }
            using var context = new HistRobotState();
            uint resolved = 0;
            Assert.Equal(discarded ? 0x06000000u : 0u, robot.PoseHistory.GetRawStateAt(time, ref resolved, context, true));
            if (!discarded) robot.SetPose(context.Pose);

            var at = received[image];
            foreach (var (_, imu) in imus.Where(sample => sample.At > at - 280 && sample.At <= at))
                rig.Data(imu);
            foreach (var chunk in images[image]) rig.Data(chunk);
            rig.Tick();
            vision.WaitForProcessorIdle();
            using (rig.Engine.EnterSynchronousOwner())
                Assert.False(rig.Engine.VisionUpdateAllResultsFailed!());
            if (discarded)
            {
                Assert.Empty(results);
                Assert.DoesNotContain(processed, report => report.Timestamp == time);
                Assert.Contains(logs, line => line.Contains("VisionComponent.SetNextImage.OriginMismatch:", StringComparison.Ordinal) &&
                    line.Contains($"t={time}", StringComparison.Ordinal));
                output.WriteLine($"image {image}, timestamp {time}, discarded at the captured odometry frame boundary");
                continue;
            }
            Assert.True(results.TryDequeue(out var frame), $"no frame delivered for {image}/{time}");
            Assert.Equal(time, frame.Timestamp);
            Assert.Empty(results);
            Assert.Equal(code is { } marker ? new[] { marker } : [], frame.Markers.Select(m => m.Code));
            Assert.Contains(processed, report => report.Timestamp == time && report.Modes.Contains((uint)VisionMode.DetectingMarkers));
            var objectReports = observed.Where(r => r.Timestamp == time).ToArray();
            if (type is { } expected)
            {
                Assert.True(objectReports.Length == 1, $"image {image}/{time} produced {frame.Objects.Count} objects and {objectReports.Length} reports: {string.Join(" | ", logs.TakeLast(30))}");
                Assert.Equal((uint)expected, objectReports[0].Type);
            }
            else if (code is null || time == 908065)
                Assert.Empty(objectReports);
            output.WriteLine($"image {image}, timestamp {time}, markers {frame.Markers.Count}, observations {objectReports.Length}");
        }
    }
}
