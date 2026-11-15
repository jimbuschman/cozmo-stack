using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The outside-AI roll and stack commands queue what the engine's RollObject / PlaceOnObject handlers build for the
/// app's default fields: DriveToRollObjectAction (0x0052AA52) and DriveToPlaceOnObjectAction (0x0052A5F2), both with
/// usePreDockPose true. reach: unit (offline rig; the runners' own paths are covered by the roll and stack tests).
/// </summary>
[Collection("M5 process statics")]
public sealed class AiRollStackControlTests
{
    // fidelity: M15-077
    [Fact]
    public async Task RollQueuesTheDriveToRollCompoundForTheRequestedCube()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        f.Add();
        using var cancel=new CancellationTokenSource();
        var task=f.Rig.Robot.Motion.QueueAiRollCubeAsync(7,cancel.Token);
        try
        {
            var compound=Assert.IsType<NativeDriveToRollRunner>(f.Robot.ActionList.GetCurrentAction());
            Assert.Equal(7u,compound.Roll.Action.ObjectId);
            Assert.NotNull(compound.Drive);                 // not carrying the cube: the drive child is built (0x0055C9E6..)
            Assert.Equal(0f,compound.RequestedFaceTurn);   // Radians(0) from the handler (0x0052AA2E): no face turn
            cancel.Cancel();f.Rig.Tick();
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);Assert.NotEqual(0u,result.EngineResult);
        }
        finally {f.Robot.ActionList.Clear();}
    }

    // fidelity: M15-008
    [Fact]
    public async Task StackQueuesTheDriveToPlaceOnCompoundForTheRequestedCube()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        f.Add();
        using var cancel=new CancellationTokenSource();
        var task=f.Rig.Robot.Motion.QueueAiStackOnCubeAsync(7,cancel.Token);
        try
        {
            var compound=Assert.IsType<NativeDriveToPlaceOnBeaconRunner>(f.Robot.ActionList.GetCurrentAction());
            Assert.NotNull(compound.Drive);
            Assert.NotNull(compound.Place);
            cancel.Cancel();f.Rig.Tick();
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);Assert.NotEqual(0u,result.EngineResult);
        }
        finally {f.Robot.ActionList.Clear();}
    }

    [Fact]
    public async Task BothRejectAnIdOutsideTheSignedWorldRange()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var roll=await f.Rig.Robot.Motion.QueueAiRollCubeAsync(uint.MaxValue);
        var stack=await f.Rig.Robot.Motion.QueueAiStackOnCubeAsync(uint.MaxValue);
        Assert.False(roll.Admitted);Assert.Null(roll.EngineResult);
        Assert.False(stack.Admitted);Assert.Null(stack.EngineResult);
    }
}
