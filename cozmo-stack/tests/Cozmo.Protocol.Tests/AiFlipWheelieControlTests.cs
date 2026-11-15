using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class AiFlipWheelieControlTests
{
    // fidelity: M12-035, M13-041
    [Theory]
    [InlineData(false, PreActionType.Flipping)]
    [InlineData(true, PreActionType.Rolling)]
    public async Task QueuesTheHandlerCompoundForTheRequestedCube(bool wheelie, PreActionType type)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        f.Add();
        using var cancel = new CancellationTokenSource();
        var motion = f.Rig.Robot.Motion;
        var task = wheelie ? motion.QueueAiPopAWheelieAsync(7, cancel.Token) : motion.QueueAiFlipCubeAsync(7, cancel.Token);
        try
        {
            NativeDriveToObjectRunner drive;
            if (wheelie)
            {
                var runner = Assert.IsType<NativeDriveToPopAWheelieRunner>(f.Robot.ActionList.GetCurrentAction());
                Assert.Equal(7u, runner.Pop.Action.ObjectId);
                Assert.Null(runner.Face);
                Assert.Equal(0f, runner.MaximumFaceTurn);
                drive = Assert.IsType<NativeDriveToObjectRunner>(runner.Drive);
                Assert.Same(runner.Pop, runner.Children[^1]);
            }
            else
            {
                var runner = Assert.IsType<NativeDriveAndFlipRunner>(f.Robot.ActionList.GetCurrentAction());
                Assert.Equal(7u, runner.Flip.Target);
                Assert.Null(runner.Face);
                Assert.Equal(0f, runner.MaximumFaceTurn);
                drive = Assert.IsType<NativeDriveToObjectRunner>(runner.Drive);
                Assert.Same(runner.Flip, runner.Children[^1]);
            }
            Assert.Equal((7u, type, false, 0.0, false, 0.0), (drive.Action.ObjectId, drive.Action.ActionType,
                drive.Action.UseManualSpeed, drive.Action.DistanceFromMarkerMm, drive.Action.UseApproachAngle, drive.Action.ApproachAngleRad));

            cancel.Cancel();
            f.Rig.Tick();
            var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);
            Assert.Equal(EngineActionResult.NotStarted, result.EngineResult);
        }
        finally { f.Robot.ActionList.Clear(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingCubeReturnsTheTerminalActionResult(bool wheelie)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var motion = f.Rig.Robot.Motion;
        var task = wheelie ? motion.QueueAiPopAWheelieAsync(7) : motion.QueueAiFlipCubeAsync(7);
        try
        {
            for (int i = 0; i < 8 && !task.IsCompleted; i++) f.Rig.Tick();
            var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);
            Assert.Equal((uint)ActionResult.BadObject, result.EngineResult);
        }
        finally { f.Robot.ActionList.Clear(); }
    }

    [Fact]
    public async Task BothRejectAnIdOutsideTheSignedWorldRange()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var motion = f.Rig.Robot.Motion;
        var flip = await motion.QueueAiFlipCubeAsync(uint.MaxValue);
        var wheelie = await motion.QueueAiPopAWheelieAsync(uint.MaxValue);
        Assert.False(flip.Admitted);
        Assert.Null(flip.EngineResult);
        Assert.False(wheelie.Admitted);
        Assert.Null(wheelie.EngineResult);
    }
}
