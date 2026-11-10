using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class AiTurnToObjectControlTests
{
    [Fact]
    public async Task MissingTargetCompletesWithDefinedMetadataAndNoRawUndefinedBody()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var local = new List<RobotCompletedAction>();
        f.Robot.ActionList.RegisterActionEndedCallback(local.Add);
        var raw = new List<byte[]>();
        f.Rig.Engine.EngineToGameMessage += (tag, body) => { if (tag == 0x5A) raw.Add(body.ToArray()); };
        int mark = f.Rig.Mark();
        var task = f.Rig.Robot.Motion.QueueAiTurnToObjectAsync(99, Radians.Pi);
        var runner = Assert.IsType<NativeTurnTowardsObjectRunner>(f.Robot.ActionList.GetCurrentAction());
        f.Rig.Tick();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(0x03000004u, result.EngineResult);
        Assert.False(f.Robot.ActionList.ContainsRunner(runner));
        var interaction = Assert.IsType<ObjectInteractionCompletion>(result.ObjectInteraction);
        Assert.Equal(99u, interaction.Word0); Assert.Equal((byte)1, interaction.Count);
        Assert.False(interaction.Flag); Assert.False(interaction.IsPayloadDefined);
        var report = Assert.Single(local.Where(r => r.Tag == runner.Tag));
        Assert.Equal((byte)0, report.CompletionVariant);
        Assert.DoesNotContain(f.Rig.SentSince(mark), m => m is SetHeadAngle or SetBodyAngle);
        f.Rig.Engine.DrainAppDelivery();
        Assert.DoesNotContain(raw, body => BitConverter.ToUInt32(body, 0) == runner.Tag);
    }

    [Fact]
    public async Task LocatedObjectRunsRealMotorChildrenAndCompletesAfterRefinement()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var target = f.Add(7);
        target.Pose = new Pose3d(Rotation3d.Identity, new Vec3(150, 100, 80), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        int mark = f.Rig.Mark();
        var task = f.Rig.Robot.Motion.QueueAiTurnToObjectAsync(7, Radians.Pi);
        var runner = Assert.IsType<NativeTurnTowardsObjectRunner>(f.Robot.ActionList.GetCurrentAction());
        for (int i = 0; i < 60 && !task.IsCompleted; i++) f.Motors();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted); Assert.Equal(0u, result.EngineResult);
        Assert.Contains(f.Rig.SentSince(mark), m => m is SetHeadAngle or SetBodyAngle);
        Assert.Equal(7u, result.ObjectInteraction!.Word0);
        Assert.False(result.ObjectInteraction.IsPayloadDefined);
        Assert.False(f.Robot.ActionList.ContainsRunner(runner));
    }

    [Fact]
    public async Task PendingCancellationDoesNotCancelPredecessorAndInputsAreChecked()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        Assert.False((await f.Rig.Robot.Motion.QueueAiTurnToObjectAsync(uint.MaxValue, 1)).Admitted);
        Assert.False((await f.Rig.Robot.Motion.QueueAiTurnToObjectAsync(1, float.NaN)).Admitted);
        var predecessor = f.Rig.Robot.Motion.SetHeadAngleAsync(.5f); f.Rig.Tick();
        using var cancel = new CancellationTokenSource();
        var task = f.Rig.Robot.Motion.QueueAiTurnToObjectAsync(7, 1, cancel.Token);
        cancel.Cancel(); f.Rig.Tick();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted); Assert.Equal(0x02000001u, result.EngineResult);
        Assert.False(predecessor.IsCompleted);
        f.Robot.ActionList.Cancel(-1); f.Rig.Tick();
        await predecessor;
    }

    [Fact]
    public async Task AnyMarkerSkipsForeignRootsAndReselectsThePlanarNearestOnRefinement()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var target = f.Add(7);
        using var foreign = new Pose3d("foreign root");
        using var a = new Pose3d(Rotation3d.Identity, new Vec3(120, 60, 300), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        using var b = new Pose3d(Rotation3d.Identity, new Vec3(150, 0, 0), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        target.Markers[0].PoseOnObject.SetParent(foreign);
        target.Markers[1].PoseOnObject.Assign(a);
        target.Markers[2].PoseOnObject.Assign(b);
        for (int i = 3; i < target.Markers.Count; i++) target.Markers[i].PoseOnObject.SetParent(foreign);
        var task = f.Rig.Robot.Motion.QueueAiTurnToObjectAsync(7, Radians.Pi);
        var runner = Assert.IsType<NativeTurnTowardsObjectRunner>(f.Robot.ActionList.GetCurrentAction());
        f.Rig.Tick();
        Assert.Equal(120f, runner.GoalPose.Translation.X, 3);
        Assert.Equal(60f, runner.GoalPose.Translation.Y, 3);
        using var replacement = new Pose3d(Rotation3d.Identity, new Vec3(80, -60, 0), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        target.Markers[2].PoseOnObject.Assign(replacement);
        int mark = f.Rig.Mark();
        for (int i = 0; i < 60 && !task.IsCompleted; i++) f.Motors();
        Assert.Contains(f.Rig.SentSince(mark).OfType<SetBodyAngle>(), body => body.AngleRad < 0);
        Assert.Equal(0u, (await task.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
    }

    [Fact]
    public async Task AllForeignMarkersFailWithoutMotorTraffic()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var target = f.Add(7);
        using var foreign = new Pose3d("foreign root");
        foreach (var marker in target.Markers) marker.PoseOnObject.SetParent(foreign);
        bool prior = Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            int mark = f.Rig.Mark();
            var task = f.Rig.Robot.Motion.QueueAiTurnToObjectAsync(7, Radians.Pi);
            f.Rig.Tick();
            Assert.Equal(0x03000002u, (await task.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
            Assert.DoesNotContain(f.Rig.SentSince(mark), m => m is SetHeadAngle or SetBodyAngle);
        }
        finally { Cozmo.Transport.EngineErrorState.ErrorFlagSet = prior; }
    }

    [Fact]
    public async Task MaximumAngleSkipsBothPassesWithoutMotorTraffic()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var target = f.Add(7);
        target.Pose = new Pose3d(Rotation3d.Identity, new Vec3(150, 100, 80), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        int mark = f.Rig.Mark();
        var task = f.Rig.Robot.Motion.QueueAiTurnToObjectAsync(7, -.01f);
        for (int i = 0; i < 10 && !task.IsCompleted; i++) f.Rig.Tick();
        Assert.Equal(0u, (await task.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        Assert.DoesNotContain(f.Rig.SentSince(mark), m => m is SetHeadAngle or SetBodyAngle);
    }

    [Fact]
    public async Task ActiveCancellationStopsOnlyTheAdmittedRunner()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var target = f.Add(7);
        target.Pose = new Pose3d(Rotation3d.Identity, new Vec3(150, 100, 80), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        using var cancel = new CancellationTokenSource();
        var task = f.Rig.Robot.Motion.QueueAiTurnToObjectAsync(7, Radians.Pi, cancel.Token);
        var runner = Assert.IsType<NativeTurnTowardsObjectRunner>(f.Robot.ActionList.GetCurrentAction());
        f.Rig.Tick();
        cancel.Cancel(); f.Rig.Tick();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0x02000000u, result.EngineResult);
        Assert.False(f.Robot.ActionList.ContainsRunner(runner));
    }
}
