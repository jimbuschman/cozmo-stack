using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class LiftOccluderReplayTests
{
    [Fact]
    public void LiftUsesOneCameraProjectionAndTheDepthOfItsTransform()
    {
        var world = new BlockWorld();
        world.SetPhysicalRobot(true);
        using var robot = new Pose3d();
        var pose = new VisionPoseData(1, robot, -.4, 0, false, false);
        using var cameraPose = pose.CameraPose;
        var camera = new CameraModel(CameraCalibration.Nominal(), cameraPose);
        world.AddLiftOccluder(camera, pose);
        var occluder = Assert.Single(camera.Occluders.Entries);
        Assert.Equal(4, occluder.Quad.Length);
        // Pinhole projection of the eight source points and the camera matrix.
        Assert.InRange(occluder.Quad[0].X, -8.7, -8.3);
        Assert.InRange(occluder.Quad[0].Y, 77.9, 78.4);
        Assert.InRange(occluder.Quad[2].X, 328.3, 328.7);
        Assert.InRange(occluder.Quad[2].Y, 229.6, 230.1);
        Assert.InRange(occluder.DepthMm, 26.9, 27.1);
        Assert.True(camera.Occluders.IsOccluded(new Vec2(160, 200), 100));
        Assert.False(camera.Occluders.IsOccluded(new Vec2(160, 30), 100));
    }

    [Fact]
    public void OccluderConsumersRequireStrictDepthAndPositiveRectangleIntersection()
    {
        var list = new OccluderList();
        list.Add([new(10, 10), new(0, 0), new(10, 0), new(0, 10)], 25);
        list.Add([new(2, 2), new(4, 4), new(4, 2), new(2, 4)], 25);
        Assert.Single(list.Entries);
        Assert.True(list.IsOccluded(new Vec2(0, 0), 26));
        Assert.False(list.IsOccluded(new Vec2(10, 5), 26));
        Assert.False(list.IsOccluded(new Vec2(5, 5), 25));
        Vec2[] overlap = [new(8, 8), new(12, 8), new(12, 12), new(8, 12)];
        Assert.True(list.IsAnythingBehind(overlap, 24));
        Assert.False(list.IsAnythingBehind(overlap, 25));
        Assert.False(list.IsAnythingBehind(overlap, 26));
        Assert.False(list.IsAnythingBehind([new(10, 0), new(12, 0), new(12, 8), new(10, 8)], 24));
    }

    [Fact]
    public void LiftOcclusionKeepsTheDirtyPoseUntilTwoEligibleFrames()
    {
        var world = new BlockWorld();
        world.SetPhysicalRobot(true);
        using var robot = new Pose3d();
        using var target = new Pose3d(Rotation3d.Identity, new Vec3(125, 0, 22));
        var type = ObjectType.Block_LIGHTCUBE1;
        var facing = CubeGeometry.MarkersFor(type).Single(marker => marker.Face == BlockFace.Front);
        var cube = new ObservableObject(7, type, [facing])
            { Pose = new Pose3d(target), OriginId = world.CurrentOriginId, PoseState = PoseState.Dirty };
        world.AddLocatedObject(cube);
        world.ApplyRelativeObservation(cube, target, PoseState.Dirty, confirmationCount: 1);

        var raised = new VisionPoseData(10, robot, -.18, .15, false, false);
        using var cameraPose = raised.CameraPose;
        var camera = new CameraModel(CameraCalibration.Nominal(), cameraPose);
        world.UpdateObservedMarkers([], camera, 10, raised);
        Assert.False(cube.IsVisibleFrom(camera, BlockWorld.VisibilityNormalAngleRad, 40, 0, 0,
            out var reason, out bool nothingBehind));
        Assert.Equal(NotVisibleReason.Occluded, reason);
        Assert.False(nothingBehind);
        Assert.Equal(0, cube.UnobservedCount);

        var lowered = raised with { Timestamp = 11, LiftAngleRad = -.2 };
        world.UpdateObservedMarkers([], camera, 11, lowered);
        Assert.Equal(1, cube.UnobservedCount);
        world.UpdateObservedMarkers([], camera, 12, lowered with { Timestamp = 12 });
        Assert.Null(world.GetLocatedObjectById(7));
    }

    [Fact]
    public void RecordedPickupFrameProjectsTheRaisedPhysicalLift()
    {
        if (Environment.GetEnvironmentVariable("COZMO_WHEELIE_CHARGER_BUNDLE") is not { Length: > 0 } bundle) return;
        const uint timestamp = 350160;
        var state = NearestState(bundle, 349570, 349680, timestamp);
        Assert.Equal(350155u, state.Timestamp);
        Assert.True(state.Has(RobotStatusFlag.IsMoving));
        Assert.True(state.Has(RobotStatusFlag.IsPickingOrPlacing));
        Assert.Contains(Reports(bundle, 349500, 350120, 0x43), body => new CladReader(body).U32() == timestamp);
        var world = new BlockWorld();
        world.SetPhysicalRobot(true);
        using var robot = RobotPose(state);
        var exposure = new VisionPoseData(timestamp, robot, state.HeadAngle, state.LiftAngle, true, false);
        using var cameraPose = exposure.CameraPose;
        var camera = new CameraModel(Calibration(bundle), cameraPose);
        world.UpdateObservedMarkers([], camera, timestamp, exposure);
        var bounds = Assert.Single(camera.Occluders.Entries);
        Assert.InRange(bounds.Quad[0].X, -25.7, -25.3);
        Assert.InRange(bounds.Quad[0].Y, 113.0, 113.5);
        Assert.InRange(bounds.Quad[2].X, 311.6, 312.1);
        Assert.InRange(bounds.Quad[2].Y, 266.4, 266.9);
        Assert.InRange(bounds.DepthMm, 25.4, 25.6);
        Assert.True(camera.Occluders.IsOccluded(new Vec2(145, 200), 50));
        Assert.False(camera.Occluders.IsOccluded(new Vec2(145, 85), 50));
    }

    [Fact]
    public void RecordedDirtyCubeIsForgottenAfterItsTwoEligibleMisses()
    {
        if (Environment.GetEnvironmentVariable("COZMO_CHARGER_CLIFF_BUNDLE") is not { Length: > 0 } bundle) return;
        var observed = new CladReader(Assert.Single(Reports(bundle, 238020, 238030, 0x44)));
        uint observedTimestamp = observed.U32();
        Assert.Equal((uint)ObjectFamily.LightCube, observed.U32());
        var type = (ObjectType)observed.U32();
        uint id = observed.U32();
        for (int i = 0; i < 4; i++) observed.F32();
        Vec3 position = new(observed.F32(), observed.F32(), observed.F32());
        Rotation3d rotation = new(observed.F32(), observed.F32(), observed.F32(), observed.F32());
        byte[] lost = Assert.Single(Reports(bundle, 239120, 239135, 0x52));
        Assert.Equal(id, new CladReader(lost).U32());
        uint first = new CladReader(Assert.Single(Reports(bundle, 238998, 239005, 0x43))).U32();
        uint second = new CladReader(Assert.Single(Reports(bundle, 239126, 239135, 0x43))).U32();
        Assert.True(observedTimestamp < first && first < second);

        using var f = new Fixture(sourceNvEmpty: true);
        var world = f.M.World;
        world.SetPhysicalRobot(true);
        var origin = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        using var cubePose = new Pose3d(rotation, position, parent: origin);
        var cube = new ObservableObject(id, type, CubeGeometry.MarkersFor(type))
            { Pose = new Pose3d(cubePose), OriginId = world.CurrentOriginId, PoseState = PoseState.Dirty };
        world.AddLocatedObject(cube);
        world.ApplyRelativeObservation(cube, cubePose, PoseState.Dirty, confirmationCount: 1);
        world.EntryFor(id)!.LastVisuallyMatchedTime = observedTimestamp;
        var raw = new List<byte[]>();
        var typed = new List<uint>();
        f.Rig.Engine.EngineToGameMessage += (tag, body) => { if (tag == 0x52) raw.Add(body.ToArray()); };
        f.Rig.Engine.ObjectPoseUnknown += report => typed.Add(report.ObjectId);
        var calibration = Calibration(bundle);
        uint imageTime = first;
        world.LastImageTimestamp = () => imageTime;

        ReplayMiss(first);
        Assert.Same(cube, world.GetLocatedObjectById(id));
        Assert.Equal(1, cube.UnobservedCount);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Empty(typed);

        ReplayMiss(second);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Null(world.GetLocatedObjectById(id));
        Assert.Equal(lost, Assert.Single(raw));
        Assert.Equal(id, Assert.Single(typed));

        void ReplayMiss(uint timestamp)
        {
            var state = NearestState(bundle, 238680, 239130, timestamp);
            Assert.False(state.Has(RobotStatusFlag.IsMoving));
            Assert.False(state.Has(RobotStatusFlag.IsCarryingBlock));
            using var robotPose = RobotPose(state);
            var pose = new VisionPoseData(timestamp, robotPose, state.HeadAngle, state.LiftAngle, false, false);
            using var cameraPose = pose.CameraPose;
            imageTime = timestamp;
            world.UpdateObservedMarkers([], new CameraModel(calibration, cameraPose), timestamp, pose);
        }
    }

    private static RobotState NearestState(string bundle, double from, double through, uint imageTime) =>
        RecordedRobotInput.ReadInbound(bundle, at => at >= from, through, [0xF0], discardRetries: true)
            .Select(row => (RobotState)row.Message)
            .OrderBy(state => Math.Abs((long)state.Timestamp - imageTime))
            .ThenBy(state => state.Timestamp)
            .First();

    private static CameraCalibration Calibration(string bundle)
    {
        var nv = RecordedRobotInput.ReadInbound(bundle, _ => true, 30000, [0xCD], discardRetries: true)
            .Select(row => (NVOpResult)row.Message)
            .First(reply => reply.Tag == CameraCalibration.NvEntryTag && reply.Result == 0);
        // Body v4 takes the clear-distortion branch 0x0065AD54–0x0065AD9C.
        return CameraCalibration.Unpack(nv.Data) with { DistortionCoefficients = [] };
    }

    private static Pose3d RobotPose(RobotState state) => new(
        Rotation3d.FromAngleAxis(state.Pose.Angle, new Vec3(0, 0, 1)),
        new Vec3(state.Pose.X, state.Pose.Y, state.Pose.Z));

    private static IEnumerable<byte[]> Reports(string bundle, double from, double through, byte tag)
    {
        string wantedTag = $"0x{tag:X2}";
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (at > through) yield break;
            if (at < from || row.GetProperty("kind").GetString() != "engine-to-game" ||
                row.GetProperty("tag").GetString() != wantedTag) continue;
            yield return Convert.FromHexString(row.GetProperty("bodyHex").GetString()!);
        }
    }
}
