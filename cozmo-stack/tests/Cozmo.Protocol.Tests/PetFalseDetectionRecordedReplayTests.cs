using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class PetFalseDetectionRecordedReplayTests(ITestOutputHelper output)
{
    [Theory]
    // A fresh OMCV session starts its search at a different phase from the long captures.
    // Shift the start of pet-mode warm-up by the listed number of frames to align
    // the search to the captured first report. Face and marker warm-up uses the full preamble.
    [InlineData("20261119-093050-LLM-SESSION", 348634.1063, 348685u, 2, false)]
    [InlineData("20261119-093050-LLM-SESSION", 803313.075, 805795u, 0, false)]
    [InlineData("20261119-094833-LLM-SESSION", 694593.2093, 696765u, 1, false)]
    [InlineData("20261119-182433-LLM-SESSION", 867116.7066, 869565u, 3, true)]
    [InlineData("20261119-182433-LLM-SESSION", 1018136.9626, 1021365u, 0, true)]
    public async Task StockDetectorReproducesThePetReactionFromCapturedPixels(
        string session, double firstReportAt, uint firstStamp, int petWarmupSkip, bool blueToy)
    {
        string? root = Environment.GetEnvironmentVariable("COZMO_PET_REPLAY_ROOT");
        if (string.IsNullOrWhiteSpace(root)) return;
        string bundle = Path.Combine(root, session);
        double begin = firstReportAt - 3500, end = firstReportAt + 450;
        var processed = new SortedDictionary<uint, int>();
        var recordedPets = new List<RobotObservedPetReport>();
        var recordedSwitches = new List<byte[]>();
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (at > end) break;
            if (at < begin || row.GetProperty("kind").GetString() != "engine-to-game") continue;
            switch (row.GetProperty("tag").GetString())
            {
                case "0x43":
                    var reader = new CladReader(Convert.FromHexString(row.GetProperty("bodyHex").GetString()!));
                    uint stamp = reader.U32();
                    int count = reader.U8();
                    int mask = 0;
                    for (int i = 0; i < count; i++) mask |= 1 << (int)reader.U32();
                    processed.Add(stamp, mask);
                    break;
                case "0x88":
                    var change = Convert.FromHexString(row.GetProperty("bodyHex").GetString()!);
                    if (change[1] == (byte)ReactionTrigger.PetInitialDetection) recordedSwitches.Add(change);
                    break;
                case "0x48":
                    var body = new CladReader(Convert.FromHexString(row.GetProperty("bodyHex").GetString()!));
                    int id = body.I32();
                    uint time = body.U32(), times = body.U32();
                    float score = body.F32();
                    var rect = new FaceRect(body.F32(), body.F32(), body.F32(), body.F32());
                    recordedPets.Add(new(id, time, times, score, rect, (PetType)body.U8()));
                    break;
            }
        }
        var first = Assert.Single(recordedPets, pet => pet.Timestamp == firstStamp && pet.TimesObserved == 1);
        var expected = recordedPets.Where(pet => pet.PetId == first.PetId && pet.Timestamp >= firstStamp).Take(3).ToArray();
        Assert.Equal(new[] { 1u, 2u, 3u }, expected.Select(pet => pet.TimesObserved));
        uint thirdStamp = expected[^1].Timestamp;
        var images = new Dictionary<uint, List<ImageChunk>>();
        var complete = new Dictionary<uint, (double At, ImageChunk[] Chunks)>();
        var imus = new List<(double At, ImageImuData Sample)>();
        var states = new SortedDictionary<uint, RobotState>();
        byte[]? calibration = null;
        uint? robotVersion = null;
        foreach (var (at, packet) in RecordedRobotInput.ReadInbound(bundle,
            time => time <= 30000 || time >= begin - 1000, end, [0xCD, 0xED, 0xF0, 0xF2, 0xF4]))
        {
            switch (packet)
            {
                case NVOpResult { Tag: CameraCalibration.NvEntryTag, Result: 0 } nv:
                    calibration = nv.Data;
                    break;
                case ManufacturingID id:
                    robotVersion = id.BodyHwVersion;
                    break;
                case RobotState state when at >= begin - 1000:
                    states[state.Timestamp] = state;
                    break;
                case ImageImuData imu when at >= begin - 1000:
                    imus.Add((at, imu));
                    break;
                case ImageChunk chunk when at >= begin - 1000:
                    if (!images.TryGetValue(chunk.ImageId, out var chunks)) images.Add(chunk.ImageId, chunks = []);
                    chunks.Add(chunk);
                    if (chunk.ImageChunkCount == 0) break;
                    if (processed.ContainsKey(chunk.FrameTimestamp))
                        complete[chunk.FrameTimestamp] = (at, chunks.ToArray());
                    images.Remove(chunk.ImageId);
                    break;
            }
        }
        Assert.Equal(processed.Keys, complete.Keys.Order());
        Assert.NotNull(calibration);
        Assert.NotNull(robotVersion);
        using var fixture = new Fixture(preloadAnimations: true, sourceNvEmpty: true, bodyHwVersion: robotVersion.Value,
            sourceNvRaw: tag => tag == CameraCalibration.NvEntryTag ? calibration : null);
        var rig = fixture.Rig;
        var robot = fixture.Robot;
        rig.Robot.SetEnableFreeplayActivity(false);
        var vision = fixture.M.Vision;
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        var observations = new List<RobotObservedPetReport>();
        rig.Engine.RobotObservedPet += observations.Add;
        var processedFrames = new HashSet<uint>();
        vision.FrameProcessed += frame => processedFrames.Add(frame.Timestamp);
        var manager = robot.BehaviorManagerOwner!;
        // Isolate trigger 10 in the short replay, while using the real manager,
        // configured behavior, first-action gate and robot action admission.
        manager.DisableReactionsWithLock("pet-replay", Enumerable.Range(0, BehaviorManager.TriggerCount)
            .Select(trigger => trigger != (int)ReactionTrigger.PetInitialDetection).ToArray());
        manager.HostReactionChecksEnabled = false;
        var switches = new List<byte[]>();
        rig.Engine.EngineToGameMessage += (tag, body) => { if (tag == 0x88) switches.Add(body.ToArray()); };
        int petBit = 1 << (int)VisionMode.DetectingPets;
        foreach (var (time, mask) in processed)
        {
            if (time > thirdStamp) break;
            vision.ModeEnableMask = mask & ((1 << (int)VisionMode.DetectingMarkers) |
                (1 << (int)VisionMode.DetectingFaces) | petBit);
            if ((mask & petBit) != 0 && petWarmupSkip > 0)
            {
                vision.ModeEnableMask &= ~petBit;
                petWarmupSkip--;
            }
            robot.PoseHistory.Clear();
            foreach (var state in states.Values.Where(state => state.Timestamp >= time - 120 && state.Timestamp <= time + 45))
            {
                state.PoseOriginId = robot.CurrentOriginId;
                using var pose = new Pose3d(Rotation3d.FromAngleAxis(state.Pose.Angle, new Vec3(0, 0, 1)),
                    new Vec3(state.Pose.X, state.Pose.Y, state.Pose.Z), parent: robot.GetNativeOrigin(robot.CurrentOriginId));
                Assert.Equal(0u, robot.PoseHistory.AddRawOdomState(state.Timestamp, pose, state));
            }
            using var context = new HistRobotState();
            uint resolved = 0;
            Assert.Equal(0u, robot.PoseHistory.GetRawStateAt(time, ref resolved, context, true));
            robot.SetPose(context.Pose);
            var (received, chunks) = complete[time];
            foreach (var (_, imu) in imus.Where(sample => sample.At > received - 280 && sample.At <= received))
                rig.Data(imu);
            foreach (var chunk in chunks) rig.Data(chunk);
            rig.Tick();
            vision.WaitForProcessorIdle();
            using (rig.Engine.EnterSynchronousOwner())
                Assert.False(rig.Engine.VisionUpdateAllResultsFailed!());
            Assert.Contains(time, processedFrames);
            if (time == processed.Keys.First())
            {
                robot.ActionList.Clear();
                Assert.Equal(0, robot.ActionList.QueueAction(QueueActionPosition.Now, new NativeTimedWaitRunner(fixture.M, 60f), 0));
            }
            manager.HostReactionChecksEnabled = true;
            var reaction = manager.CheckReactions(rig.Engine.Timer.Seconds);
            manager.HostReactionChecksEnabled = false;
            rig.Engine.DrainAppDelivery();
            if (time < thirdStamp) Assert.Null(reaction);
            else Assert.True(reaction?.Trigger == ReactionTrigger.PetInitialDetection,
                $"Trigger {reaction?.Trigger}, current {manager.Current?.Id}, action {robot.ActionList.GetCurrentAction()}, " +
                $"platform {rig.Robot.Sensors.OnChargerPlatform}, enabled {manager.IsReactionTriggerEnabled(ReactionTrigger.PetInitialDetection)}, " +
                $"pets {string.Join(", ", vision.Pets.Pets.Select(p => $"{p.Id}/{p.TimesObserved}"))}, reports {observations.Count}.");
        }

        // The guest allocates a new ID sequence for each replay, so compare all
        // fields after mapping the one selected ID back to the capture.
        int replayId = Assert.Single(observations.Select(pet => pet.PetId).Distinct());
        Assert.Equal(expected, observations.Select(pet => pet with { PetId = first.PetId }));
        Assert.Equal(Assert.Single(recordedSwitches), Assert.Single(switches));
        var behavior = Assert.IsType<NativeReactToPetBehavior>(manager.Current);
        Assert.Equal(replayId, Assert.Single(behavior.Targets));
        output.WriteLine($"Checked {processedFrames.Count} camera frames, {first.Type} score {first.Score}, and the third-observation reaction at t={thirdStamp}.");

        if (blueToy)
        {
            using var bridge = new CozmoBridge(rig.Robot);
            await using var client = new McpServerTests.Client(bridge);
            await client.Initialize();
            var reply = (await client.Tool("head", new { radians = 0.1 })).GetProperty("result");
            Assert.True(reply.GetProperty("isError").GetBoolean());
            var result = reply.GetProperty("structuredContent");
            Assert.Equal("not-admitted", result.GetProperty("status").GetString());
            var why = result.GetProperty("why").GetProperty("snapshot").GetProperty("reaction");
            Assert.Equal("PetInitialDetection", why.GetProperty("trigger").GetString());
        }
    }
}
