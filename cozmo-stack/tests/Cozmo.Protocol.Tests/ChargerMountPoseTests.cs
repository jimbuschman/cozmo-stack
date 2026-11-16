using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class ChargerMountPoseTests
{
    private static ObservableObject AddCharger(PickupPyramidNativeExecutionTests.Fixture f, uint id)
    {
        using var pose = new Pose3d(Rotation3d.Identity, new Vec3(180, 0, 0),
            parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        var charger = new ObservableObject(id, ObjectType.Charger_Basic, ChargerGeometry.Markers)
        {
            Pose = pose,
            OriginId = f.Robot.CurrentOriginId,
            PoseState = PoseState.Known,
        };
        f.M.World.AddLocatedObject(charger);
        return charger;
    }

    [Fact]
    public void BackupContactSetsPoseBumpsFrameAndSendsVisionUpdateOnTheNextTick()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var charger = AddCharger(f, 27);
        var mount = new MountChargerAction(f.M, charger.ObjectId);
        Assert.Equal(ActionResult.Success, mount.Initialize());
        Assert.Equal(27, f.Rig.Robot.Cubes.ChargerObjectId);
        f.Rig.State(RobotStatusFlag.IsOnCharger | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
        Assert.True(f.Rig.Robot.Sensors.OnCharger);

        // Supply the located charger's pose at the action check, after the contact observation.
        // At +90 degrees its local (30,0,0) becomes a +30 mm world Y offset; its docked yaw is -90.
        using var pose = new Pose3d(Rotation3d.FromAngleAxis(MathF.PI / 2, new Vec3(0, 0, 1)),
            new Vec3(180, -50, 8), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        charger.Pose = pose;
        var origin = f.Robot.CurrentOriginId;
        var frame = unchecked(f.Robot.PoseFrameId + 1);
        var timestamp = f.Robot.StoredState!.Timestamp;
        var localizedTo = f.M.World.LocalizedToObjectId;
        int mark = f.Rig.Mark();

        Assert.Equal(0u, mount.BackupCheckIfDone(() => throw new InvalidOperationException("Contacts should win")));
        Assert.Equal(180, f.Robot.MainPose.Translation.X, 3);
        Assert.Equal(-20, f.Robot.MainPose.Translation.Y, 3);
        Assert.Equal(8, f.Robot.MainPose.Translation.Z, 3);
        Assert.Equal(-Math.PI / 2, f.Robot.MainPose.AngleAroundZ, 4);
        Assert.Equal(pose.Translation, charger.NativePose.Translation);
        Assert.Equal(frame, f.Robot.PoseFrameId);
        Assert.Equal(origin, f.Robot.CurrentOriginId);
        Assert.Equal(localizedTo, f.M.World.LocalizedToObjectId);
        Assert.Equal((byte)1, f.Robot.VisionPoseUpdated);
        Assert.DoesNotContain(f.Rig.SentSince(mark), m => m is AbsoluteLocalizationUpdate);

        f.Rig.Tick();
        var sent = Assert.Single(f.Rig.SentSince(mark).OfType<AbsoluteLocalizationUpdate>());
        Assert.Equal((timestamp, frame, origin), (sent.Timestamp, sent.PoseFrameId, sent.PoseOriginId));
        Assert.Equal(180f, sent.PoseX, 3);
        Assert.Equal(-20f, sent.PoseY, 3);
        Assert.Equal(-MathF.PI / 2, sent.PoseAngleRad, 4);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BackupStillSucceedsWhenItsSavedIdCannotResolveToACharger(bool resolvesToCube)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        AddCharger(f, 27);
        f.Rig.State(RobotStatusFlag.IsOnCharger | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
        const uint id = 43;
        if (resolvesToCube) f.Add(id);
        f.Rig.Robot.Cubes.SetChargerObjectIdFromMountInit(id);
        var mount = new MountChargerAction(f.M, 27);
        var logs = new List<EngineLogEntry>();
        f.Rig.Engine.LogEntry += logs.Add;
        var frame = f.Robot.PoseFrameId;
        var translation = f.Robot.MainPose.Translation;
        int mark = f.Rig.Mark();

        Assert.Equal(0u, mount.BackupCheckIfDone(() => throw new InvalidOperationException("Contacts should win")));
        Assert.Equal(frame, f.Robot.PoseFrameId);
        Assert.Equal(translation, f.Robot.MainPose.Translation);
        Assert.Contains(logs, e => e.Name == "Robot.SetPoseOnCharger.NoChargerWithID" && e.Text.Contains("ID=43"));
        f.Rig.Tick();
        Assert.DoesNotContain(f.Rig.SentSince(mark), m => m is AbsoluteLocalizationUpdate);
    }
}
