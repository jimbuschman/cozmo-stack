using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
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
    public async Task CubeMovedReactionReplacesFlipDuringItsFinalTurn()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        var (cube, slot, strategy) = LocateConnectedCube(f);
        var manager = f.Robot.BehaviorManagerOwner!;
        var switched = new List<BehaviorManager.ReactionSwitch>();
        manager.ReactionTriggered += switched.Add;
        int mark = f.Rig.Mark();
        var pending = f.Rig.Robot.Motion.QueueAiFlipCubeAsync(cube.ObjectId);
        var flip = Assert.IsType<NativeDriveAndFlipRunner>(f.Robot.ActionList.GetCurrentAction());
        var drive = Assert.IsType<NativeDriveToObjectRunner>(flip.Drive);
        for (int i = 0; i < 80 && !f.Rig.SentSince(mark).OfType<ExecutePath>().Any(); i++)
        {
            if (f.M.GetNativePlanners().Lattice is { CheckPlanningStatus: 1 } planner)
                Assert.True(SpinWait.SpinUntil(() => planner.CheckPlanningStatus != 1, TimeSpan.FromSeconds(10)));
            f.Motors(RobotStatusFlag.IsBodyAccMode);
        }

        var execute = Assert.Single(f.Rig.SentSince(mark).OfType<ExecutePath>());
        f.Rig.Data(new PathFollowingEvent { EventId = execute.EventId, EventType = (byte)PathEventType.Started });
        f.Motors(RobotStatusFlag.IsBodyAccMode);
        using (var goal = drive.Drive!.Goals[drive.Drive.GoalIndex].GetWithRespectToRoot())
        {
            f.X = (float)goal.Translation.X;
            f.Y = (float)goal.Translation.Y;
            f.Yaw = (float)goal.AngleAroundZ;
        }
        f.Motors(RobotStatusFlag.IsBodyAccMode);
        f.Rig.Data(new PathFollowingEvent { EventId = execute.EventId, EventType = (byte)PathEventType.Completed });
        var turn = Assert.Single(drive.Sequence.Children.OfType<NativeTurnTowardsObjectRunner>());
        int animationMark = mark;
        for (int i = 0; i < 25 && !turn.Initialized; i++)
        {
            foreach (var animation in f.Rig.SentSince(animationMark).OfType<StartOfAnimation>())
            {
                f.Rig.Data(new AnimationStarted { AnimId = animation.AnimId });
                f.Rig.Data(new AnimationEnded { AnimId = animation.AnimId });
            }
            animationMark = f.Rig.Mark();
            f.Motors(RobotStatusFlag.IsBodyAccMode);
        }

        Assert.True(turn.Initialized, $"Flip={flip.State:X8}, drive={drive.State:X8}, turn={turn.State:X8}");
        Assert.False(flip.Flip.Initialized);
        Assert.Same(flip, f.Robot.ActionList.GetCurrentAction());
        Assert.True(manager.IsReactionTriggerEnabled(ReactionTrigger.CubeMoved));
        Assert.False(f.Robot.ActionList.ExternalActionsDisabled);
        Assert.Null(f.M.Docking.DockTargetObjectId);
        Assert.True(f.M.Vision.Locator.DistanceFromRobotMm(cube.ObjectId) > 50);

        var lookingAway = f.Rig.MakeState(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos | RobotStatusFlag.IsBodyAccMode,
            origin: f.Robot.CurrentOriginId, frame: f.Robot.PoseFrameId, x: f.X, y: f.Y, head: f.Head, liftAngle: f.Lift);
        lookingAway.Pose.Angle = MathF.Atan2(-f.Y, 300 - f.X) + MathF.PI;
        f.Rig.Data(lookingAway);
        f.Rig.Tick();
        Assert.False(f.M.Vision.Locator.IsVisibleFromCamera(cube.ObjectId));

        // A changed axis after an observed start is an independent native CubeMoved candidate at 0x0060BD60..78.
        f.Rig.Data(new ObjectMoved { ObjectID = slot, Timestamp = lookingAway.Timestamp, AxisOfAccel = UpAxis.ZPositive });
        f.Rig.Data(new ObjectMoved { ObjectID = slot, Timestamp = lookingAway.Timestamp, AxisOfAccel = UpAxis.XPositive });
        for (int i = 0; i < 4 && !pending.IsCompleted; i++) f.Rig.Tick();

        var result = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(EngineActionResult.Cancelled, result.EngineResult);
        Assert.False(flip.Flip.Initialized);
        Assert.Contains(switched, reaction => reaction.Trigger == ReactionTrigger.CubeMoved);
        var registration = Assert.Single(manager.Reactions, entry => ReferenceEquals(entry.Strategy, strategy));
        var reaction = Assert.IsType<AcknowledgeCubeMovedBehavior>(registration.Behavior);
        Assert.Equal(cube.ObjectId, reaction.TargetObjectId);
        var sense = Assert.IsType<CompoundActionParallel>(reaction.OwnedRunner);
        Assert.Same(sense, f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(1f, Assert.Single(sense.Children.OfType<NativeTimedWaitRunner>()).Duration);
        Assert.True(f.Robot.ActionList.ExternalActionsDisabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CubeMovedConsumesTheWorldReportAfterCarryAndDockFilters(bool docking)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var (cube, slot, strategy) = LocateConnectedCube(f);
        var tags = new List<ushort>();
        f.Rig.Engine.EngineToGameMessage += (tag, _) => tags.Add(tag);
        uint stamp = f.Robot.StoredState!.Timestamp;
        var moved = new ObjectMoved { ObjectID = slot, Timestamp = stamp, AxisOfAccel = UpAxis.ZPositive };
        var stopped = new ObjectStoppedMoving { ObjectID = slot, Timestamp = stamp };

        void SetExcluded(bool excluded)
        {
            if (!docking) f.Robot.CarryingOwner!.SetCarrying(excluded ? cube.ObjectId : uint.MaxValue);
            else if (excluded)
                Assert.True(f.M.Docking.StartNativeDock(cube.ObjectId, unchecked((short)cube.Markers[0].Code),
                    DockAction.PickupLow, PathMotionProfile.Default));
            else f.M.Docking.ResetDockTargetAtActionEnd();
        }

        SetExcluded(true);
        f.Rig.Data(moved);
        f.Rig.Tick();
        Assert.DoesNotContain((ushort)0x0F, tags);
        Assert.False(Assert.Single(strategy.Tracker.Entries, entry => entry.ObjectId == cube.ObjectId).Moving);

        SetExcluded(false);
        f.Rig.Data(moved);
        f.Rig.Tick();
        Assert.Contains((ushort)0x0F, tags);
        Assert.True(Assert.Single(strategy.Tracker.Entries, entry => entry.ObjectId == cube.ObjectId).Moving);

        SetExcluded(true);
        f.Rig.Data(stopped);
        f.Rig.Tick();
        Assert.DoesNotContain((ushort)0x10, tags);
        Assert.True(Assert.Single(strategy.Tracker.Entries, entry => entry.ObjectId == cube.ObjectId).Moving);

        SetExcluded(false);
        f.Rig.Data(stopped);
        f.Rig.Tick();
        Assert.Contains((ushort)0x10, tags);
        Assert.False(Assert.Single(strategy.Tracker.Entries, entry => entry.ObjectId == cube.ObjectId).Moving);
    }

    private static (ObservableObject Cube, uint Slot, CubeMovedReactionStrategy Strategy)
        LocateConnectedCube(PickupPyramidNativeExecutionTests.Fixture f)
    {
        const uint slot = 1;
        f.Rig.Data(new ObjectConnectionState { ObjectID = slot, FactoryID = 0xABCD, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true });
        f.Rig.Tick();
        uint id = f.M.World.ConnectedObjectIdForActiveId(slot)!.Value;
        var cube = f.Add(id, x: 300);
        var strategy = Assert.IsType<CubeMovedReactionStrategy>(Assert.Single(f.Robot.BehaviorManagerOwner!.Reactions,
            entry => entry.Strategy.Trigger == ReactionTrigger.CubeMoved).Strategy);
        strategy.ObjectObserved(id);
        Assert.Contains(strategy.Tracker.Entries, entry => entry.ObjectId == id && entry.Observed);
        return (cube, slot, strategy);
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
