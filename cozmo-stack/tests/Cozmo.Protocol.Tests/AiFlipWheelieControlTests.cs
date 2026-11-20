using Cozmo.Bridge;
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

    [Theory]
    [InlineData(80.5000076f, true)]  // Captured body Y, frame 10641.
    [InlineData(84.5f, false)]      // Same cube and X, beyond the 5-degree-derived Y limit.
    public void CapturedFlipCubePoseUsesTheNativeArrivalGuard(float robotY, bool accepted)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var origin = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        const ObjectType type = ObjectType.Block_LIGHTCUBE2;
        var cube = new ObservableObject(0, type, CubeGeometry.MarkersFor(type))
        {
            Pose = new Pose3d(new Rotation3d(.71344143f, 1.414938e-8f, -2.2599734e-7f, -.7007149f),
                new Vec3(228.92612f, -3.0567493f, 12.637633f), parent: origin),
            OriginId = f.Robot.CurrentOriginId, PoseState = PoseState.Known
        };
        f.M.World.AddLocatedObject(cube);
        f.X = 146.5683136f;
        f.Y = robotY;
        f.Yaw = -.7377589345f;
        f.Motors();
        using var lease = f.Rig.Engine.EnterSynchronousOwner();
        var logs = new List<EngineLogEntry>();
        f.Rig.Engine.LogEntry += logs.Add;
        var flip = new NativeFlipBlockRunner(f.M, cube.ObjectId);
        try
        {
            var result = accepted ? ActionResult.Success : ActionResult.DidNotReachPreActionPose;
            Assert.Equal(unchecked((int)result), flip.Init());
            Assert.Equal(cube.ObjectId, f.M.World.SelectedObjectId);
            var tooFar = logs.Where(entry => entry.Name == "IsCloseEnoughToPreActionPose.TooFarFromGoal");
            if (accepted) Assert.Empty(tooFar);
            else Assert.Contains("(2.4mm, 10.4mm)", Assert.Single(tooFar).Text);
        }
        finally
        {
            f.Rig.Engine.LogEntry -= logs.Add;
            flip.WatcherEnding();
        }
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

    [Fact]
    public async Task RecordedWheelieFirmwareCanReportSuccessWhenCozmoTipsBack()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_WHEELIE_CHARGER_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;
        var capturedDock = Assert.IsType<DockWithObject>(Assert.Single(
            RecordedRobotInput.ReadOutbound(bundle, at => at >= 930000, 930100, [0x42])).Message);
        var feedback = RecordedRobotInput.ReadInbound(bundle, at => at >= 930048, 934420,
            [0xB8, 0xC5, 0xF0], discardRetries: true).ToArray();
        var states = feedback.Select(row => row.Message).OfType<RobotState>().ToArray();
        uint captureStart = states[0].Timestamp;
        var last = states[^1];
        var liftStarted = Assert.Single(feedback.Where(row => row.Message is MovingLiftPostDock));
        var dockResult = Assert.Single(feedback.Where(row => row.Message is PickAndPlaceResult));
        Assert.Equal(9, Assert.IsType<MovingLiftPostDock>(liftStarted.Message).Field0);
        Assert.True(Assert.IsType<PickAndPlaceResult>(dockResult.Message).Field1);

        // The 0x42 request delegates the sequence to firmware. F0 shows the lift
        // rise while both wheels are stopped, then approach and the post-dock push.
        Assert.Contains(feedback, row => row.At < liftStarted.At && row.Message is RobotState state &&
            state.LiftAngle > .79f && state.LwheelSpeedMmps == 0 && state.RwheelSpeedMmps == 0);
        Assert.Contains(feedback, row => row.At > liftStarted.At && row.At < dockResult.At &&
            row.Message is RobotState state && state.LwheelSpeedMmps >= 150 && state.LiftAngle < 0);
        Assert.InRange(last.Pose.Pitch, 1.32f, 1.34f);
        Assert.Equal(0u, last.Status & (uint)(RobotStatusFlag.IsMoving | RobotStatusFlag.IsPickingOrPlacing));

        using var scene = new PopFirmwareScene();
        var f = scene.F;
        f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.Rig.Tick();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var mcp = new McpServerTests.Client(bridge);
        await mcp.Initialize();
        int mark = f.Rig.Mark();
        int id = await mcp.Send("tools/call", new { name = "wheelie", arguments = new { objectId = scene.Target } });
        var root = Assert.IsType<NativeDriveToPopAWheelieRunner>(f.Robot.ActionList.GetCurrentAction());
        bool SentDock() => f.Rig.SentSince(mark).OfType<DockWithObject>().Any();
        for (int tick = 0; tick < 150 && !SentDock() && !root.DestructionObserved; tick++)
            scene.Step(SentDock);

        var sent = Assert.Single(f.Rig.SentSince(mark).OfType<DockWithObject>());
        Assert.Equal((capturedDock.UnusedZero, capturedDock.SpeedMmps, capturedDock.AccelMmps2, capturedDock.DecelMmps2,
            capturedDock.DockAction, capturedDock.Field5, capturedDock.Field6, capturedDock.DockingMethod, capturedDock.Field8),
            (sent.UnusedZero, sent.SpeedMmps, sent.AccelMmps2, sent.DecelMmps2, sent.DockAction,
                sent.Field5, sent.Field6, sent.DockingMethod, sent.Field8));
        Assert.Equal(0, scene.Rolls); // replay replaces the firmware segment of the scene
        // The rendered marker approach uses the sim camera path. Restore the
        // recorded firmware input so treads classification uses the physical
        // 1.30027 rad center at 0x00511E80..0x00511E8C.
        var firmware = Assert.IsType<FirmwareVersion>(Assert.Single(RecordedRobotInput.ReadInbound(bundle,
            _ => true, 30000, [0xEE], discardRetries: true)).Message);
        f.Robot.HandleFirmwareVersion(firmware);
        Assert.True(f.Robot.IsPhysicalRobot);

        var trace = new List<string>();
        double replayAt = 0;
        f.Robot.ActionList.RegisterActionEndedCallback(r => trace.Add($"{replayAt:F3} ended {r.Tag} {r.ActionType:X} {r.Result:X8}"));
        f.Robot.BehaviorManagerOwner!.ReactionTriggered += r => trace.Add($"{replayAt:F3} {r.Trigger} {r.Behavior}");
        f.Rig.Engine.LogEntry += r =>
        {
            if (r.Name?.Contains("Cancel", StringComparison.Ordinal) == true ||
                r.Name?.Contains("PitchAngleTooSmall", StringComparison.Ordinal) == true ||
                r.Name?.Contains("Delocaliz", StringComparison.Ordinal) == true)
                trace.Add($"{replayAt:F3} log {r.Name} {r.Text}");
        };
        uint replayStart = f.Robot.StoredState!.Timestamp + 30;
        double previousAt = feedback[0].At;
        foreach (var (at, message) in feedback)
        {
            replayAt = at;
            if (message is RobotState state)
            {
                // Rebase IDs and clock. Preserve pitch, lift, wheel rates and movement bits.
                state.PoseOriginId = f.Robot.CurrentOriginId;
                state.PoseFrameId = f.Robot.PoseFrameId;
                state.Timestamp = replayStart + state.Timestamp - captureStart;
            }
            else if (message is PickAndPlaceResult dock)
            {
                dock.Field0 = replayStart + dock.Field0 - captureStart;
            }
            f.Rig.Data(message);
            f.Rig.Tick(at - previousAt);
            previousAt = at;
        }
        for (int tick = 0; tick < 4 && !root.DestructionObserved; tick++) f.Rig.Tick();
        Assert.True(root.DestructionObserved);
        var reply = (await mcp.Receive(id)).GetProperty("result").GetProperty("structuredContent");
        // Verify uses post-dock F0 pitch and B8 success, the inputs read at 0x0055296A–0x0055299E.
        Assert.True(EngineActionResult.Success == reply.GetProperty("engineResult").GetUInt32(),
            reply.GetProperty("engineResultName").GetString() + "\n" + string.Join("\n", trace));
        Assert.Equal("SUCCESS", reply.GetProperty("engineResultName").GetString());
        Assert.Equal(OffTreadsState.OnTreads, f.Rig.Robot.Sensors.OffTreadsState);

        foreach (var (at, message) in RecordedRobotInput.ReadInbound(bundle, at => at > 934420,
            935050, [0xF0]))
        {
            var state = Assert.IsType<RobotState>(message);
            state.PoseOriginId = f.Robot.CurrentOriginId;
            state.PoseFrameId = f.Robot.PoseFrameId;
            state.Timestamp = replayStart + state.Timestamp - captureStart;
            f.Rig.Data(state);
            f.Rig.Tick(at - previousAt);
            previousAt = at;
        }
        Assert.Equal(OffTreadsState.OnBack, f.Rig.Robot.Sensors.OffTreadsState);
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
