using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class AiGotoPoseControlTests
{
    // fidelity: M12-023, M12-024
    [Fact]
    public async Task QueuesAnOwnedWorldOriginGoalWithTheHandlerDefaults()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        using var cancel = new CancellationTokenSource();
        var task = f.Rig.Robot.Motion.QueueAiGotoPoseAsync(170, -32, MathF.PI / 2, cancel.Token);
        try
        {
            var drive = Assert.IsType<DriveToPoseAction>(f.Robot.ActionList.GetCurrentAction());
            Assert.Equal(0x0C, drive.Type);
            Assert.Equal(4u, drive.RequiredTrackMask);
            Assert.False(drive.LowersHead);
            Assert.False(drive.UsesManualSpeed);
            Assert.Equal(10d, drive.DistanceToleranceMm);
            Assert.Equal((double)BitConverter.UInt32BitsToSingle(0x3E32B8C2), drive.AngleToleranceRad);
            Assert.Equal(4d, drive.PlanningTimeoutSec);
            var goal = Assert.Single(drive.Goals!);
            Assert.Equal("GotoPoseTarget_COPY", goal.Name);
            Assert.Equal(new Vec3(170, -32, 0), goal.Translation);
            Assert.Equal(MathF.PI / 2, (float)goal.AngleAroundZ, 5);
            Pose3d parent = null!;
            Assert.True(goal.TryGetParent(ref parent));
            using (parent)
                Assert.Same(f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node, parent.Node);

            cancel.Cancel();
            f.Rig.Tick();
            var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);
            Assert.Equal(EngineActionResult.NotStarted, result.EngineResult);
        }
        finally { f.Robot.ActionList.Clear(); }
    }

    [Theory]
    [InlineData(float.NaN, 0, 0)]
    [InlineData(0, float.PositiveInfinity, 0)]
    [InlineData(0, 0, float.NegativeInfinity)]
    public async Task RejectsNonfinitePoseBeforeAdmission(float x, float y, float angle)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var result = await f.Rig.Robot.Motion.QueueAiGotoPoseAsync(x, y, angle);
        Assert.False(result.Admitted);
        Assert.Null(result.EngineResult);
        Assert.Null(f.Robot.ActionList.GetCurrentAction());
    }
}
