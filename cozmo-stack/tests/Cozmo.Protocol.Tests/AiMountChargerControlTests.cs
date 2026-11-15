using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class AiMountChargerControlTests
{
    // fidelity: M13-008, M13-009
    [Fact]
    public async Task QueuesPredockThenMountWithTheHandlerDefaults()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        using var cancel = new CancellationTokenSource();
        var task = f.Rig.Robot.Motion.QueueAiMountChargerAsync(27, cancel.Token);
        try
        {
            var compound = Assert.IsType<NativeDriveToMountChargerRunner>(f.Robot.ActionList.GetCurrentAction());
            Assert.Collection(compound.Children,
                drive => Assert.Same(compound.Drive, drive),
                mount => Assert.Equal(0x11, mount.Type));
            Assert.Equal((27u, PreActionType.Docking, 0.0, false, 0.0, false),
                (compound.Drive.Action.ObjectId, compound.Drive.Action.ActionType, compound.Drive.Action.DistanceFromMarkerMm,
                compound.Drive.Action.UseApproachAngle, compound.Drive.Action.ApproachAngleRad, compound.Drive.Action.UseManualSpeed));
            Assert.Equal((27u, false, false), (compound.Mount.ChargerId, compound.Mount.Flag80, compound.Mount.Flag81));

            cancel.Cancel();
            f.Rig.Tick();
            var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);
            Assert.Equal(EngineActionResult.NotStarted, result.EngineResult);
        }
        finally { f.Robot.ActionList.Clear(); }
    }

    [Fact]
    public async Task MissingTargetEndsInTheDriveWithoutStoringAChargerId()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var task = f.Rig.Robot.Motion.QueueAiMountChargerAsync(27);
        try
        {
            f.Rig.Tick();
            var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);
            Assert.Equal((uint)ActionResult.BadObject, result.EngineResult);
            Assert.Equal(-1, f.Rig.Robot.Cubes.ChargerObjectId);
        }
        finally { f.Robot.ActionList.Clear(); }
    }

    [Fact]
    public async Task LocatedChargerReachesTheSharedNativePredockPath()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true);
        var charger = new ObservableObject(27, ObjectType.Charger_Basic, ChargerGeometry.Markers)
        {
            Pose = new Pose3d(Rotation3d.Identity, new Vec3(400, 0, 0), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),
            OriginId = f.Robot.CurrentOriginId,
            PoseState = PoseState.Known,
        };
        f.M.World.AddLocatedObject(charger);
        var task = f.Rig.Robot.Motion.QueueAiMountChargerAsync(27);
        try
        {
            var compound = Assert.IsType<NativeDriveToMountChargerRunner>(f.Robot.ActionList.GetCurrentAction());
            f.Rig.Tick();
            Assert.NotNull(compound.Drive.Drive);
            Assert.All(compound.Drive.Drive!.Goals, p => Assert.NotNull(p.Node));
            Assert.NotEmpty(compound.Drive.Drive.Goals);
            Assert.False(task.IsCompleted);
        }
        finally { f.Robot.ActionList.Clear(); }
        await task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RejectsAnIdOutsideTheSignedWorldRange()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var result = await f.Rig.Robot.Motion.QueueAiMountChargerAsync(uint.MaxValue);
        Assert.False(result.Admitted);
        Assert.Null(result.EngineResult);
    }
}
