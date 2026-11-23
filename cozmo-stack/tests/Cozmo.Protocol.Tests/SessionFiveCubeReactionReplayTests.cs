using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

public sealed partial class ReactionLockMcpTests
{
    internal const string SessionFiveCubeCapture = "COZMO_SESSION5_CUBE_REACTION_BUNDLE";
    internal const double SessionFivePutDownSighting = 73267.9437;

    [Fact]
    public async Task SessionFiveCubeReactionReplayFollowsTheCurrentOriginObservationAndVisibilityGates()
    {
        string? bundle = Environment.GetEnvironmentVariable(SessionFiveCubeCapture);
        if (string.IsNullOrEmpty(bundle)) return;
        const double firstMove = 204013.3801, reacquired = 1133525.0, lastMove = 1135445.1045, stop = 1137064.856;
        var rows = ReadSessionFiveCubeReports(bundle, 187000, 1137100).ToArray();
        var cameraNv = RecordedRobotInput.ReadInbound(bundle, _ => true, 30000, [0xCD], discardRetries: true)
            .Select(row => (NVOpResult)row.Message)
            .First(reply => reply.Tag == CameraCalibration.NvEntryTag && reply.Result == 0);
        var hardware = Assert.IsType<ManufacturingID>(Assert.Single(
            RecordedRobotInput.ReadInbound(bundle, _ => true, 30000, [0xED], discardRetries: true)).Message);
        using var f = new Fixture(preloadAnimations: true, sourceNvEmpty: true,
            sourceNvRaw: tag => tag == CameraCalibration.NvEntryTag ? cameraNv.Data : null,
            bodyHwVersion: hardware.BodyHwVersion);
        await SetUpBehavior(f);
        var manager = f.Robot.BehaviorManagerOwner!;
        manager.HostReactionChecksEnabled = false;
        var entry = manager.Reactions.Single(r => r.Strategy.Trigger == ReactionTrigger.CubeMoved);
        var strategy = (CubeMovedReactionStrategy)entry.Strategy;
        var behavior = (AcknowledgeCubeMovedBehavior)entry.Behavior;
        bool WantsReaction() => strategy.ShouldTrigger(f.Runtime.Context, null, f.Rig.Engine.Timer.Seconds, behavior);
        bool Located() => f.M.World.GetLocatedObjectByIdHelper(1) is not null;

        void Sighting(Captured report)
        {
            var reader = new CladReader(report.Body);
            uint timestamp = reader.U32(), family = reader.U32(), type = reader.U32(), id = reader.U32();
            for (int i = 0; i < 4; i++) reader.F32();
            var point = new Vec3(reader.F32(), reader.F32(), reader.F32());
            var rotation = new Rotation3d(reader.F32(), reader.F32(), reader.F32(), reader.F32());
            uint captureOrigin = reader.U32();
            Assert.Equal(report.At < reacquired ? 1u : 27u, captureOrigin);
            var cube = f.M.World.GetLocatedObjectByIdHelper(id);
            using var pose = new Pose3d(rotation, point, parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
            if (cube is null)
            {
                var objectType = (ObjectType)type;
                cube = new ObservableObject(id, objectType, CubeGeometry.MarkersFor(objectType))
                {
                    Pose = pose, OriginId = f.Robot.CurrentOriginId, PoseState = PoseState.Known,
                };
                f.M.World.AddLocatedObject(cube);
            }
            else cube.Pose = pose;
            cube.LastObservedTimestamp = timestamp;
            f.Rig.Engine.AnimationMessages.Deliver(0x44, true, new NativeObservedObject(timestamp, family, type, id));
        }

        void Radio(Captured report, uint clockShift = 0)
        {
            var reader = new CladReader(report.Body);
            switch (report.Tag)
            {
                case "0x0F":
                    var moved = ObjectMoved.Read(reader);
                    moved.Timestamp = unchecked(moved.Timestamp + clockShift);
                    f.Rig.Engine.AnimationMessages.Deliver(0x0F, true, moved);
                    break;
                case "0x10":
                    f.Rig.Engine.AnimationMessages.Deliver(0x10, true, ObjectStoppedMoving.Read(reader));
                    break;
                case "0x11":
                    f.Rig.Engine.AnimationMessages.Deliver(0x11, true, ObjectUpAxisChanged.Read(reader));
                    break;
                // 0x13 is outside the native CubeMoved strategy's tag set at 0x00C759F0.
            }
        }

        uint InstallState(RobotState state)
        {
            uint shift = unchecked(f.Robot.StoredState!.Timestamp + 30 - state.Timestamp);
            state.Timestamp = unchecked(state.Timestamp + shift);
            FeedCapturedState(f, state);
            f.Rig.Tick();
            return shift;
        }

        Assert.True(manager.IsReactionTriggerEnabled(ReactionTrigger.CubeMoved));
        Assert.True(manager.IsReactionTriggerEnabled(ReactionTrigger.ObjectPositionUpdated));
        Sighting(rows.Last(r => r.At < firstMove && r.Tag == "0x44"));
        var beforeFirst = ReadStateBefore(bundle, firstMove);
        Assert.Equal(203880u, beforeFirst.Timestamp);
        uint firstShift = InstallState(beforeFirst);
        var movement = rows.Single(r => r.At == firstMove);
        Assert.Equal(203900u, ObjectMoved.Read(new CladReader(movement.Body)).Timestamp);
        Radio(movement, firstShift);
        var motion = Assert.Single(strategy.Tracker.Entries);
        Assert.True(strategy.Tracker.HasMovedLongEnough(motion, Located(), beforeFirst.Timestamp));
        Assert.Equal(beforeFirst.Timestamp, f.Robot.StoredState!.Timestamp);
        var firstTarget = f.M.World.GetLocatedObjectByIdHelper(1)!;
        float firstDistanceSquared = 0;
        Assert.True(PlacementProducerGeometry.TryDistanceSquared(firstTarget.NativePose, f.Robot.GetCheckedMainPose(), ref firstDistanceSquared));
        using (var firstCameraPose = f.Robot.CopyCurrentCameraPose(f.Rig.Robot.Motion.ObservationHeadAngleRad))
        {
            var firstCamera = Assert.IsType<CameraModel>(f.M.Vision.BindOuterCameraForVisibility(f.Robot, firstCameraPose.Pose));
            var firstVisibility = firstTarget.IsVisibleFromWithReason(firstCamera, MathF.PI / 4f);
            using var flat = firstCameraPose.Pose.GetWithRespectToRoot();
            using var relative = firstTarget.NativePose.WithRespectTo(firstCameraPose.Pose);
            Assert.True(firstDistanceSquared > 2500 && firstVisibility != NotVisibleReason.IsVisible,
                $"Distance {MathF.Sqrt(firstDistanceSquared)}, target {firstTarget.NativePose.Translation}, visibility {firstVisibility}, " +
                $"camera {flat.Translation}, relative {relative.Translation}, " +
                $"calibration {firstCamera.Calibration.FocalLengthX}, yaw {f.Robot.GetCheckedMainPose().AngleAroundZ}.");
        }
        // 0x0060BE68 unsigned subtraction wraps on the captured 20 ms lead.
        Assert.True(WantsReaction(), $"Target {behavior.TargetObjectId}, treads {f.Rig.Robot.Sensors.OffTreadsState}, " +
            $"charger {f.Rig.Robot.Sensors.OnChargerPlatform}, robot {f.Robot.GetCheckedMainPose().Translation}, " +
            $"head {f.Rig.Robot.Motion.ObservationHeadAngleRad}.");
        Assert.Equal(1u, behavior.TargetObjectId);
        Assert.False(Assert.Single(strategy.Tracker.Entries).Observed);
        Assert.Contains(rows, r => r.Tag == "0x88" && r.At - firstMove is >= 0 and < 1 &&
            r.Body.SequenceEqual(new byte[] { 0x16, 1 }));

        var later = rows.Where(r => r.At is >= 226380 and <= 250745 && r.Tag != "0x88").ToArray();
        Assert.DoesNotContain(later, r => r.Tag == "0x44");
        Assert.Equal(new[] { "0x13", "0x13", "0x11", "0x10", "0x0F", "0x10" }, later.Select(r => r.Tag));
        uint startTime = ObjectMoved.Read(new CladReader(later[^2].Body)).Timestamp;
        uint endTime = ObjectStoppedMoving.Read(new CladReader(later[^1].Body)).Timestamp;
        Assert.Equal(985u, endTime - startTime);
        foreach (var row in later)
        {
            Radio(row);
            Assert.False(WantsReaction()); // lookup/stop in the first window, Observed cleared in the second.
        }

        Assert.Equal(1u, ReadStateBefore(bundle, 250745).PoseOriginId);
        Assert.Equal(27u, ReadStateBefore(bundle, 1066200).PoseOriginId);
        f.Robot.Delocalize(suppliedCarrying: false);
        Assert.False(Located());
        var unlocated = rows.Where(r => r.At >= 1065600 && r.At < reacquired && r.Tag != "0x88").ToArray();
        Assert.Equal(3, unlocated.Count(r => r.Tag == "0x0F"));
        Assert.DoesNotContain(unlocated, r => r.Tag == "0x44");
        foreach (var row in unlocated)
        {
            Radio(row);
            Assert.All(strategy.Tracker.Entries, e => Assert.False(e.Moving));
            Assert.False(WantsReaction());
            Assert.Empty(strategy.Tracker.Entries);
        }

        foreach (var row in rows.Where(r => r.At >= reacquired && r.At < lastMove))
        {
            if (row.Tag == "0x44") Sighting(row);
            else Radio(row);
            Assert.False(WantsReaction()); // observation resets motion even before the stop at 15:31:35.87.
        }
        Assert.True(Located());
        Assert.True(Assert.Single(strategy.Tracker.Entries).Observed);

        var threshold = ReadStateBefore(bundle, 1136500);
        Assert.Equal(27u, threshold.PoseOriginId);
        var finalMove = rows.Single(r => r.At == lastMove);
        uint finalStamp = ObjectMoved.Read(new CladReader(finalMove.Body)).Timestamp;
        Assert.True(threshold.Timestamp - finalStamp > 1000);
        uint lastShift = InstallState(threshold);
        Radio(finalMove, lastShift);
        var tracked = Assert.Single(strategy.Tracker.Entries);
        Assert.True(strategy.Tracker.HasMovedLongEnough(tracked, Located(), threshold.Timestamp));
        using var cameraPose = f.Robot.CopyCurrentCameraPose(f.Rig.Robot.Motion.ObservationHeadAngleRad);
        var camera = Assert.IsType<CameraModel>(f.M.Vision.BindOuterCameraForVisibility(f.Robot, cameraPose.Pose));
        var target = f.M.World.GetLocatedObjectByIdHelper(1)!;
        var reason = target.IsVisibleFromWithReason(camera, MathF.PI / 4f);
        Assert.True(reason == NotVisibleReason.IsVisible, $"Visibility {reason}, robot {f.Robot.GetCheckedMainPose().Translation}, " +
            $"head {f.Rig.Robot.Motion.ObservationHeadAngleRad}, target {target.NativePose.Translation}.");
        Assert.False(WantsReaction());
        Assert.True(tracked.Moving); // visibility veto does not consume the record.

        // Change only the robot heading in a positive control to distinguish visibility from distance and runnable.
        threshold.Pose.Angle += MathF.PI;
        InstallState(threshold);
        Assert.True(WantsReaction());
        Assert.Equal(1u, behavior.TargetObjectId);
        var terminal = Assert.Single(rows.Where(r => r.At > lastMove && Math.Abs(r.At - stop) < 2 && r.Tag == "0x10"));
        Radio(terminal);
        Assert.DoesNotContain(rows, r => r.Tag == "0x88" && r.At > firstMove + 1 && r.Body is [_, 1]);
    }

    private static IEnumerable<Captured> ReadSessionFiveCubeReports(string bundle, double start, double end)
    {
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (at < start) continue;
            if (at > end) yield break;
            if (row.GetProperty("kind").GetString() != "engine-to-game") continue;
            string tag = row.GetProperty("tag").GetString()!;
            if (tag is not ("0x44" or "0x0F" or "0x10" or "0x11" or "0x13" or "0x88")) continue;
            byte[] body = Convert.FromHexString(row.GetProperty("bodyHex").GetString()!);
            int idOffset = tag == "0x44" ? 12 : 4;
            if (tag == "0x88" || new CladReader(body.AsSpan(idOffset).ToArray()).U32() == 1)
                yield return new Captured(at, tag, body);
        }
    }
}
