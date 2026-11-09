using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class GroundPlacementCallerTests
{
    [Fact]
    public void GroundWaitsForIsMovingRatherThanWheelMotion()
    {
        // Public adapter, real retained action, native busy latch and reaction owner.
        using var scene=new StackFirmwareScene(true);var f=scene.F;
        _=f.M.PlaceObjectOnGroundAsync();
        var runner=Assert.IsType<NativePlaceGroundRunner>(f.Robot.ActionList.GetCurrentAction());
        f.Rig.Tick();
        Assert.True(f.Robot.BehaviorManagerOwner!.HasDisableLock(ReactionTrigger.ObjectPositionUpdated,GroundPlacement.ReactionLockName));
        f.Rig.State(RobotStatusFlag.IsPickingOrPlacing|RobotStatusFlag.HeadInPos);
        Assert.True(runner.StatusLatched);
        f.Rig.State(RobotStatusFlag.IsMoving|RobotStatusFlag.HeadInPos);
        Assert.Equal(EngineActionResult.NotStarted,runner.VerifyChild!.State);
        f.Rig.State(RobotStatusFlag.AreWheelsMoving|RobotStatusFlag.HeadInPos);
        Assert.NotEqual(EngineActionResult.NotStarted,runner.VerifyChild.State);
    }
    [Fact]
    public void AnyMarkerTurnStillSkipsAnUnrelatedRootWhenAnotherMarkerIsValid()
    {
        // reach: retained turn recipient, with one invalid and other valid marker poses.
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var target = f.Add(7);
        using var otherRoot = new Pose3d("disconnected marker root");
        target.Markers[0].PoseOnObject.SetParent(otherRoot);
        var turn = new NativeTurnTowardsObjectRunner(f.M, 7, NativeTurnTowardsObjectRunner.AnyCode, 0, false, false);
        try { Assert.Equal(0, turn.Init()); }
        finally { turn.Prep(); turn.WatcherEnding(); }
    }

    [Fact]
    public void ConcreteMarkerFailureStopsEvenAfterAnEarlierMatchingCandidate()
    {
        // reach: concrete-code turn selection, with duplicate code supplied to exercise native iteration.
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var definition = CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1)[0];
        var target = new ObservableObject(7, ObjectType.Block_LIGHTCUBE1, new[] { definition, definition, definition })
        {
            Pose = new Pose3d(Rotation3d.Identity, new Vec3(150, 0, 22), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),
            OriginId = f.Robot.CurrentOriginId,
            PoseState = PoseState.Known,
        };
        f.M.World.AddLocatedObject(target);
        using var otherRoot = new Pose3d("disconnected marker root");
        target.Markers[1].PoseOnObject.SetParent(otherRoot);
        var turn = new NativeTurnTowardsObjectRunner(f.M, 7, unchecked((short)definition.Code), 0, false, false);
        bool priorError = Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        Cozmo.Transport.EngineErrorState.ErrorFlagSet = false;
        try
        {
            int mark = f.Rig.Mark();
            Assert.Equal(unchecked((int)0x03000005), turn.Init());
            Assert.True(Cozmo.Transport.EngineErrorState.ErrorFlagSet);
            Assert.DoesNotContain(f.Rig.SentSince(mark), message => message is SetHeadAngle or SetBodyAngle);
        }
        finally
        {
            turn.Prep(); turn.WatcherEnding();
            Cozmo.Transport.EngineErrorState.ErrorFlagSet = priorError;
        }
    }

    [Fact]
    public async Task GroundVerificationReturnsBadPoseWhenSelectedMarkerHasAnotherRoot()
    {
        // reach: public placement, retained Ground child, busy rise/fall and exact-marker turn.
        using var scene = new StackFirmwareScene(true);
        var f = scene.F;
        var owner = f.Robot;
        uint id = scene.Top;
        var target = f.M.World.GetLocatedObjectByIdHelper(id)!;
        ushort markerCode = owner.CarryingOwner!.NativeMarkerCode;
        var selected = Assert.Single(target.Markers.Where(marker => (ushort)marker.Code == markerCode));
        var logs = new System.Collections.Concurrent.ConcurrentQueue<EngineLogEntry>();
        f.Rig.Engine.LogEntry += logs.Enqueue;
        f.M.World.SelectedObjectId = id;
        using var otherRoot = new Pose3d("disconnected marker root");
        var task = f.M.PlaceObjectOnGroundAsync();
        var runner = Assert.IsType<NativePlaceGroundRunner>(owner.ActionList.GetCurrentAction());
        f.Rig.Tick();
        f.Rig.State(RobotStatusFlag.IsPickingOrPlacing | RobotStatusFlag.HeadInPos);
        Assert.True(runner.StatusLatched);
        Assert.NotNull(runner.VerifyChild);
        Assert.Equal(EngineActionResult.NotStarted, runner.VerifyChild!.State);
        f.Rig.Data(new PickAndPlaceResult { Field1 = true, Field3 = (byte)BlockStatus.BlockPlaced });
        f.Rig.Tick();
        selected.PoseOnObject.SetParent(otherRoot);
        int mark = f.Rig.Mark();
        bool priorError = Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        Cozmo.Transport.EngineErrorState.ErrorFlagSet = false;
        try
        {
            f.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
            Assert.Equal(ActionResult.BadPose, await task.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.True(Cozmo.Transport.EngineErrorState.ErrorFlagSet);
        }
        finally { Cozmo.Transport.EngineErrorState.ErrorFlagSet = priorError; }
        Assert.False(owner.ActionList.ContainsRunner(runner));
        var entry = Assert.Single(logs.Where(log => log.Name == "TurnTowardsObjectAction.Init.MarkerOriginProblem"));
        Assert.Equal(EngineLogLevel.Error, entry.Level);
        Assert.EndsWith($"Could not get pose of marker with code {unchecked((short)markerCode)} of object {unchecked((int)id)} w.r.t. robot pose.", entry.Text);
        Assert.Null(f.M.World.SelectedObjectId);
        Assert.Same(target, f.M.World.GetLocatedObjectByIdHelper(id));
        Assert.False(owner.BehaviorManagerOwner!.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, GroundPlacement.ReactionLockName));
        Assert.DoesNotContain(f.Rig.SentSince(mark), message => message is SetHeadAngle or SetBodyAngle);
    }

    [Fact]
    public async Task ManualTaskUsesTheRealGroundActionAndReturnsAfterUnlink()
    {
        using var scene = new StackFirmwareScene(true);
        var f = scene.F;
        f.X = 200; scene.Motors();
        int mark = f.Rig.Mark();
        var task = f.M.PlaceObjectOnGroundAsync();
        var runner = Assert.IsType<NativePlaceGroundRunner>(f.Robot.ActionList.GetCurrentAction());
        bool latched = false;
        for (int i = 0; i < 80 && !runner.DestructionObserved; i++)
        { scene.Step(); latched |= runner.StatusLatched; }
        Assert.Equal(ActionResult.Success, await task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(latched);
        Assert.False(f.Robot.ActionList.ContainsRunner(runner));
        var sent = f.Rig.SentSince(mark);
        var place = Assert.Single(sent.OfType<PlaceObjectOnGround>());
        Assert.Equal((0f, 0f, 0f, 100f, 200f, 500f, false),
            (place.RelX, place.RelY, place.RelAngle, place.SpeedMmps, place.AccelMmps2, place.DecelMmps2, place.Field6));
        Assert.True(sent.IndexOf(place) < sent.FindIndex(m => m is StopAllMotors));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualTaskReturnsNativeNoCarryOrSendFailure(bool sendFailure)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot;
        if (sendFailure)
        {
            f.Add(7); owner.CarryingOwner!.SetCarrying(7);
            owner.SendFault = message => message is PlaceObjectOnGround ? false : null;
        }
        try
        {
            int mark = f.Rig.Mark();
            var task = f.M.PlaceObjectOnGroundAsync();
            f.Rig.Tick();
            Assert.Equal(sendFailure ? ActionResult.SendPlaceObjectOnGroundFailed : ActionResult.NotCarryingObjectAbort,
                await task.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Single(f.Rig.SentSince(mark).OfType<StopAllMotors>());
            Assert.Empty(f.Rig.SentSince(mark).OfType<PlaceObjectOnGround>());
        }
        finally { owner.SendFault = null; }
    }

    [Fact]
    public void ConstructingStandaloneManipulationDoesNotReplaceMotionPlacementOwner()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var original = f.Rig.Robot.Motion.PlaceObjectOnGroundAsync;
        using var standalone = ManipulationSystem.CreateStandalone(f.Rig.Robot, f.M.Vision, () => 0);
        Assert.Same(original, f.Rig.Robot.Motion.PlaceObjectOnGroundAsync);
        Assert.Equal(new[] { 8 }, GroundPlacement.ReactionLockTable.ToMask().Select((enabled, i) => (enabled, i))
            .Where(pair => pair.enabled).Select(pair => pair.i));
    }

    [Fact]
    public async Task ManualTaskCancellationStopsItsExactActiveRunner()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        f.Add(7); f.Robot.CarryingOwner!.SetCarrying(7);
        using var cancel = new CancellationTokenSource();
        var task = f.M.PlaceObjectOnGroundAsync(cancel.Token);
        var runner = Assert.IsType<NativePlaceGroundRunner>(f.Robot.ActionList.GetCurrentAction());
        f.Rig.Tick();
        cancel.Cancel(); f.Rig.Tick();
        Assert.Equal(ActionResult.CancelledWhileRunning, await task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(f.Robot.ActionList.ContainsRunner(runner));
    }

    [Fact]
    public async Task ManualTaskCanceledBeforeFirstTickDoesNotSendPlacement()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        f.Add(7); f.Robot.CarryingOwner!.SetCarrying(7);
        using var cancel = new CancellationTokenSource();
        int mark = f.Rig.Mark();
        var task = f.M.PlaceObjectOnGroundAsync(cancel.Token);
        cancel.Cancel(); f.Rig.Tick();
        Assert.Equal((ActionResult)EngineActionResult.NotStarted, await task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.DoesNotContain(f.Rig.SentSince(mark), message => message is PlaceObjectOnGround or StopAllMotors);
    }

    [Fact]
    public async Task ManualTaskOwnerRetirementCannotSendToReplacement()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        f.Add(7); f.Robot.CarryingOwner!.SetCarrying(7);
        var original = f.Robot;
        var task = f.M.PlaceObjectOnGroundAsync();
        f.Rig.Tick();
        f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId, false);
        f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId, withRic: false);
        int mark = f.Rig.Mark();
        Assert.Equal(ActionResult.CancelledWhileRunning, await task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.NotSame(original, f.Rig.Engine.Robot);
        Assert.DoesNotContain(f.Rig.SentSince(mark), message => message is PlaceObjectOnGround or StopAllMotors or SetHeadAngle or SetBodyAngle or SetLiftHeight);
    }

    [Fact]
    public async Task ReleaseWithoutBusyStillTimesOutOnTheNativeClock()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        f.Add(7); f.Robot.CarryingOwner!.SetCarrying(7);
        var task = f.M.PlaceObjectOnGroundAsync();
        var runner = Assert.IsType<NativePlaceGroundRunner>(f.Robot.ActionList.GetCurrentAction());
        f.Rig.Tick();
        f.Rig.Data(new PickAndPlaceResult { Field1 = true, Field3 = (byte)BlockStatus.BlockPlaced });
        f.Rig.Tick(29000);
        Assert.False(runner.StatusLatched);
        Assert.False(task.IsCompleted);
        f.Rig.Tick(1100);
        Assert.Equal(ActionResult.Timeout, await task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(runner.StatusLatched);
    }

    [Fact]
    public async Task BusyThenMovingDoesNotStartVerification()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        f.Add(7); f.Robot.CarryingOwner!.SetCarrying(7);
        using var cancel = new CancellationTokenSource();
        var task = f.M.PlaceObjectOnGroundAsync(cancel.Token);
        var runner = Assert.IsType<NativePlaceGroundRunner>(f.Robot.ActionList.GetCurrentAction());
        f.Rig.Tick();
        f.Rig.State(RobotStatusFlag.IsPickingOrPlacing | RobotStatusFlag.HeadInPos);
        Assert.True(runner.StatusLatched);
        f.Rig.State(RobotStatusFlag.IsMoving | RobotStatusFlag.HeadInPos);
        Assert.False(task.IsCompleted);
        Assert.Equal(EngineActionResult.NotStarted, runner.VerifyChild!.State);
        cancel.Cancel(); f.Rig.Tick();
        Assert.Equal(ActionResult.CancelledWhileRunning, await task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Exact32LegacyLiftUsesTheNativeGroundRecipient()
    {
        using var scene = new StackFirmwareScene(true);
        var f = scene.F;
        f.X = 200; scene.Motors();
        int mark = f.Rig.Mark();
        var task = f.Rig.Robot.Motion.SetLiftHeightAsync(32f);
        var runner = Assert.IsType<NativePlaceGroundRunner>(f.Robot.ActionList.GetCurrentAction());
        bool latched = false;
        for (int i = 0; i < 80 && !runner.DestructionObserved; i++) { scene.Step(); latched |= runner.StatusLatched; }
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(MotionResult.Acknowledged, result.Result);
        Assert.True(latched);
        var sent = f.Rig.SentSince(mark);
        var place = Assert.Single(sent.OfType<PlaceObjectOnGround>());
        Assert.True(sent.IndexOf(place) < sent.FindIndex(message => message is StopAllMotors));
        Assert.DoesNotContain(sent, message => message is SetLiftHeight);
    }

    [Fact]
    public async Task Exact32CarryCallbackCannotRedirectPlacementToReplacement()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var motion = f.Rig.Robot.Motion;
        var previous = motion.IsCarryingObject;
        int mark = -1;
        motion.IsCarryingObject = () =>
        {
            f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId, false);
            f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId, withRic: false);
            mark = f.Rig.Mark();
            return true;
        };
        try
        {
            var result = await motion.SetLiftHeightAsync(32f).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(MotionResult.Failed, result.Result);
            Assert.True(mark >= 0);
            Assert.DoesNotContain(f.Rig.SentSince(mark), message => message is PlaceObjectOnGround or SetLiftHeight);
        }
        finally { motion.IsCarryingObject = previous; }
    }

    [Fact]
    public async Task ConfiguredFistBumpOwnsTheNativeGroundActionBeforeAdvancing()
    {
        using var scene = new StackFirmwareScene(true);
        var f = scene.F;
        f.X = 200; scene.Motors();
        var fist = Assert.IsType<FistBumpBehavior>(f.Runtime.Bound["FistBump"]);
        using var selection = new SelectionUnitScope(f.Robot.BehaviorManagerOwner!);
        Assert.True(await f.Robot.BehaviorManagerOwner!.StartAsync("FistBump", f.Rig.Engine.Timer.SecondsF));
        f.Rig.Tick();
        var runner = Assert.IsType<NativePlaceGroundRunner>(fist.OwnedRunner);
        Assert.Equal(1, fist.State);
        Assert.True(fist.HasCurrentAction);
        for (int i = 0; i < 80 && !runner.DestructionObserved; i++) scene.Step();
        Assert.True(runner.DestructionObserved);
        f.Rig.Tick();
        Assert.NotSame(runner, fist.OwnedRunner);
        Assert.NotEqual(runner.Tag, fist.CurrentRunnerTag);
        Assert.Equal(1, scene.Grounds);
        Assert.False(f.Robot.ActionList.ContainsRunner(runner));
    }
}
