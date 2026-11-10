using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class LocalizationMarkerCameraTests
{
    private static float DistanceSquared(Pose3d marker, EngineRobot owner, float head)
    {
        // Independently flatten the checked neck/head transform into MAIN for this fixture.
        using var flat = HeadGeometry.CameraPoseInRobot(head);
        using var camera = new Pose3d(flat.Quaternion, flat.Translation, parent: owner.MainPose);
        using var relative = new Pose3d();
        Assert.True(marker.TryWithRespectTo(camera, relative));
        var t = relative.Translation;
        float x = (float)t.X, y = (float)t.Y, z = (float)t.Z;
        float squared = x * x; squared += y * y; squared += z * z;
        return squared;
    }

    [Fact]
    public void AttachedCallUsesCurrentAcceptedHeadAndMainPose()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot; var obj = f.Add(7);
        uint earlierTime = owner.StoredState!.Timestamp;
        using var earlier = new HistRobotState(); uint resolved = 0;
        Assert.Equal(0u, owner.PoseHistory.GetRawStateAt(earlierTime, ref resolved, earlier, false));
        Assert.Equal(-.15f, earlier.State.HeadAngle, 4);
        f.Head = .3f; f.Motors();
        Assert.Equal(.3f, f.Rig.Robot.Motion.ObservationHeadAngleRad, 4);
        using (var pose = new Pose3d(Rotation3d.FromAngleAxis(.4f, new Vec3(0, 0, 1)), new Vec3(25, -12, 0), parent: owner.GetNativeOrigin(owner.CurrentOriginId)))
            owner.SetPose(pose);
        foreach (var marker in obj.Markers) marker.LastObservedTimestamp = 5;
        float expected = obj.Markers.Min(m => DistanceSquared(m.PoseOnObject, owner, f.Rig.Robot.Motion.ObservationHeadAngleRad));
        float olderHead = obj.Markers.Min(m => DistanceSquared(m.PoseOnObject, owner, earlier.State.HeadAngle));
        Assert.True(Math.Abs(expected - olderHead) > 1f); // prior historical head angle differs from accepted current head
        f.M.World.DelocalizeRobotFields();
        Assert.True(f.M.World.SetLocalizedTo(obj));
        Assert.Equal(expected, f.M.World.Robot2C8, 2);
        Assert.Equal(7u, f.M.World.LocalizedToObjectId);
        Assert.Equal((byte)1, f.M.World.Robot2C4);
    }

    [Fact]
    public void EmptyAndNullInputsDoNotRequestCamera()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var world = f.M.World; var prior = world.NativeCurrentCamera;
        world.NativeCurrentCamera = _ => throw new InvalidOperationException("camera must not be requested");
        try
        {
            var empty = new ObservableObject(9, ObjectType.Block_LIGHTCUBE1, Array.Empty<KnownMarker>());
            Assert.True(world.SetLocalizedTo(empty)); Assert.Equal(9u, world.LocalizedToObjectId);
            Assert.True(world.SetLocalizedTo(null)); Assert.Null(world.LocalizedToObjectId);
        }
        finally { world.NativeCurrentCamera = prior; }
    }

    [Fact]
    public void ForeignRootAfterWinningMarkerKeepsEarlierMinimumAndDoesNotCommit()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot; var obj = f.Add(7);
        foreach (var marker in obj.Markers) marker.LastObservedTimestamp = 1;
        float expected = DistanceSquared(obj.Markers[0].PoseOnObject, owner, f.Rig.Robot.Motion.ObservationHeadAngleRad);
        using var foreign = new Pose3d("same numeric ID, different root") { Id = owner.CurrentOriginId };
        obj.Markers[1].PoseOnObject.SetParent(foreign);
        f.M.World.DelocalizeRobotFields(); f.M.World.NoteRobotState(true, false);
        bool previous = Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            Assert.False(f.M.World.SetLocalizedTo(obj));
            Assert.Equal(expected, f.M.World.Robot2C8, 2);
            Assert.Null(f.M.World.LocalizedToObjectId);
            Assert.True(f.M.World.RobotMovedSinceLocalized);
            Assert.True(Cozmo.Transport.EngineErrorState.ErrorFlagSet);
        }
        finally { Cozmo.Transport.EngineErrorState.ErrorFlagSet = previous; }
    }
}
