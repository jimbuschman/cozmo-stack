using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The correction pass of 2026-09-20, at the level each fault actually lived at: the reaction manager driving
/// real strategies, the vision system feeding them, the path lifecycle as one unit, and the freeplay stack
/// assembled the way a library consumer gets it. Component-level checks would have passed for most of these
/// before the fix, which is the point.
/// </summary>
public class CorrectionTests
{
    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "assets", "animationGroups"))) return r;
            d = d.Parent;
        }
        return null;
    }

    private static BehaviorContext Ctx(Rig rig) =>
        new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Random = new Random(5) };

    // ---------------------------------------------------------------- 1 + 2: target-producing strategies

    /// <summary>
    /// The cube-moved reaction end to end through the manager: a real camera frame locates the cube, the cube
    /// reports movement, the robot turns away, and the manager switches to ReactToCubeMoved. Before the
    /// two-phase contract the manager tested the behaviour first, which could never be runnable because only
    /// the strategy sets its target, so this reaction was unreachable however it was driven.
    /// </summary>
    [Fact]
    public void TheCubeMovedReactionFiresThroughTheManagerFromRealObservations()
    {
        var obb = ObbRoot();
        if (obb is null || MarkerLibrary.EmbeddedOrNull is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = Ctx(rig);
        using var manager = new BehaviorManager(ctx);
        var registrations = ShippedBehaviors.Reactions(rig.Robot, clockSec: () => rig.Clock.NowMs / 1000.0, vision: rig.Vision);
        var reg = registrations.Single(r => r.Strategy.Trigger == ReactionTrigger.CubeMoved);
        manager.AddReaction(reg.Strategy, reg.Behavior);
        var strategy = (CubeMovedReactionStrategy)reg.Strategy;

        // the cube is seen: the world locates it and the strategy learns of the sighting from BlockWorld,
        // not from a hand-written call
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(220, 0, 22));
        var seen = rig.Frame();
        Assert.Single(seen.Objects);
        uint cubeId = seen.Objects[0].Object.ObjectId;
        Assert.Contains(strategy.Tracker.Entries, e => e.ObjectId == cubeId && e.Observed);

        // it is picked up and moved for over a second, and the robot is no longer looking at it
        rig.Send(new ObjectMoved { ObjectID = cubeId, Timestamp = rig.T, AxisOfAccel = UpAxis.ZPositive });
        rig.T += 2000; rig.State();
        rig.Angle = (float)Math.PI; rig.State();

        var fired = manager.CheckReactions(rig.Clock.NowMs / 1000.0);
        Assert.NotNull(fired);
        Assert.Equal(ReactionTrigger.CubeMoved, fired!.Trigger);
        Assert.Equal("ReactToCubeMoved", fired.Behavior);
        Assert.Equal(cubeId, ((AcknowledgeCubeMovedBehavior)reg.Behavior).TargetObjectId);
    }

    /// <summary>
    /// The object-position-updated reaction, likewise: the strategy fills AcknowledgeObject's target list and
    /// only then is the behaviour asked whether it can run.
    /// </summary>
    [Fact]
    public void TheObjectPositionUpdatedReactionFiresThroughTheManager()
    {
        var obb = ObbRoot();
        if (obb is null || MarkerLibrary.EmbeddedOrNull is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = Ctx(rig);
        using var manager = new BehaviorManager(ctx);
        var reg = ShippedBehaviors.Reactions(rig.Robot, clockSec: () => rig.Clock.NowMs / 1000.0, vision: rig.Vision)
                                  .Single(r => r.Strategy.Trigger == ReactionTrigger.ObjectPositionUpdated);
        manager.AddReaction(reg.Strategy, reg.Behavior);

        // ObjectPositionUpdated drops an observation when the manager has no current behaviour (M10-003 gap2 4j:
        // only if a current behaviour exists and its class differs), so establish one before the frame. Its class
        // ("test") differs from the bound AcknowledgeObject class, so the sighting is not discarded as self-observation.
        var standIn = M10Support.RunnableBehaviour("m10-object-position-current");
        manager.Add(standIn);
        manager.StartAsync(standIn.Id, 0).GetAwaiter().GetResult();

        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(200, 0, 22));
        var seen = rig.Frame();
        uint cubeId = seen.Objects[0].Object.ObjectId;
        var behaviour = (AcknowledgeObjectBehavior)reg.Behavior;
        Assert.False(behaviour.IsRunnable(ctx));                  // nothing to acknowledge until the strategy says so

        var fired = manager.CheckReactions(rig.Clock.NowMs / 1000.0);
        Assert.NotNull(fired);
        Assert.Equal("AcknowledgeObject", fired!.Behavior);
        Assert.Equal(cubeId, behaviour.CurrentTarget);            // the strategy's target, taken up by the behaviour
    }

    /// <summary>
    /// The strategy only writes the behaviour's target when it actually has a candidate; a call with nothing
    /// located leaves the caller's target alone. The engine has one decision function (C6 over STBI), not a
    /// stage / commit / abandon contract, so there is nothing to put back.
    /// </summary>
    [Fact]
    public void ACallerTargetIsUntouchedWhenNothingIsLocated()
    {
        using var rig = new Rig();
        var behaviour = new AcknowledgeCubeMovedBehavior(rig.Vision.Locator) { TargetObjectId = 42 };
        using var strategy = new CubeMovedReactionStrategy(rig.Robot, behaviour, rig.Vision.Locator, rig.Vision.World);
        Assert.False(strategy.ShouldTrigger(Ctx(rig), null, 0, behaviour));   // nothing located, no candidate
        Assert.Equal(42u, behaviour.TargetObjectId);                           // the caller's target is untouched
    }

    // ---------------------------------------------------------------- 3: one reaction dispatcher

    /// <summary>
    /// A fall plays ReactToImpact through the manager: the RobotFalling strategy watches FallingStarted (C1, C12),
    /// WithTimeout 3000. Being put on the charger plays ReactToOnCharger once its ChargerEvent latch is set. Where
    /// the engine broadcasts ChargerEvent and with what onCharger value is not in the M4/M10 rows (gap2 1), so the
    /// latch is driven directly here rather than through an invented broadcaster.
    /// </summary>
    [Fact]
    public void TheWholeStackKeepsTheImpactAndChargerReactions()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        double clock = 0;
        var ctx = Ctx(rig);
        using var stack = FreeplayStack.Create(obb, rig.Robot, ctx, () => clock, rig.Vision, rig.M, random: new Random(1));

        var triggers = new List<ReactionTrigger>();
        stack.Manager.ReactionTriggered += r => triggers.Add(r.Trigger);
        // C3: the sticky gate opens on the first queued action or the "sdk" lock removal; with no ActionList here,
        // open it the second way (gap1 4c).
        stack.Manager.RemoveDisableReactionsLock("sdk");

        // a hard landing: FallingStarted latches the RobotFalling strategy (C1, C12); the FallingStopped over the 1000
        // threshold is what makes ReactToImpact runnable (its AlwaysHandle gate, M10 C2). Its 3000 ms WithTimeout
        // window compares BaseStationTimer ms, so let the run clock pass 3000 first, as a real robot's would have.
        rig.Clock.Advance(4000);
        rig.Send(new FallingStarted { Unknown = 1000 });
        rig.Send(new FallingStopped { DurationMs = 400, ImpactIntensity = 2500f });
        clock = 1;
        Assert.NotNull(stack.Manager.CheckReactions(clock));
        Assert.Equal(new[] { ReactionTrigger.RobotFalling }, triggers);
        stack.Manager.Stop(BehaviorStopReason.Interrupted, clock);

        // and no second one from the same fall
        clock = 2;
        Assert.Null(stack.Manager.CheckReactions(clock));

        // put on the charger: 20 s from the strategy's first wants-to-run call, then the ChargerEvent latch
        var charger = (PlacedOnChargerStrategy)stack.Manager.Reactions
            .Single(r => r.Strategy.Trigger == ReactionTrigger.PlacedOnCharger).Strategy;
        charger.WantsToRun(clock);              // the first call starts the 20 s deadline (gap2 1d)
        charger.HandleChargerEvent(true);       // vtable +0xC on tag 57 ChargerEvent
        clock = 30;
        Assert.NotNull(stack.Manager.CheckReactions(clock));
        Assert.Equal(new[] { ReactionTrigger.RobotFalling, ReactionTrigger.PlacedOnCharger }, triggers);
    }

    /// <summary>
    /// A fall whose impact is under the 1000 threshold: FallingStarted latches the RobotFalling strategy (C12), but
    /// ReactToImpact stays not runnable, so the reaction does not fire (M10 C2).
    /// </summary>
    [Fact]
    public void ASoftLandingDoesNotReact()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        double clock = 0;
        using var stack = FreeplayStack.Create(obb, rig.Robot, Ctx(rig), () => clock, rig.Vision, rig.M, random: new Random(1));
        stack.Manager.RemoveDisableReactionsLock("sdk");   // open the C3 sticky gate so the runnable gate is what is tested
        rig.Clock.Advance(4000);
        rig.Send(new FallingStarted { Unknown = 1000 });
        rig.Send(new FallingStopped { DurationMs = 100, ImpactIntensity = 500f });
        clock = 1;
        Assert.Null(stack.Manager.CheckReactions(clock));
    }

    /// <summary>The M7 dispatcher stands down when a manager already dispatches over the same arbiter.</summary>
    [Fact]
    public void TheOlderDispatcherStandsDownBesideTheManager()
    {
        using var rig = new Rig();
        var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Arbiter = arbiter };
        using var manager = new BehaviorManager(ctx);
        Assert.True(arbiter.ManagerDispatchesReactions);

        using var reactive = new ReactiveBehavior(rig.Robot, new AnimationTriggerMap(), arbiter: arbiter) { Asynchronous = false };
        var decisions = new List<BehaviorDecision>();
        reactive.Reacted += decisions.Add;
        reactive.Start();
        Assert.Contains(decisions, d => d.Reason.Contains("stands down"));

        // it is not subscribed, so a cliff report reaches nobody twice
        int before = decisions.Count;
        rig.Send(new CliffEvent { Timestamp = rig.T, DetectedFlags = 1 });
        Assert.Equal(before, decisions.Count);
    }

    // ---------------------------------------------------------------- 4: recalibration readiness

    [Fact]
    public void ARecalibrationInvalidatesReadinessUntilItFinishes()
    {
        using var rig = new Rig();
        var state = rig.Robot.State;
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true, AutoStarted = false });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false, AutoStarted = false });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true, AutoStarted = false });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false, AutoStarted = false });
        Assert.True(state.CalibrationComplete);

        // the head starts again: normal motion must be refused while it runs
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true, AutoStarted = true });
        Assert.False(state.HeadCalibrated);
        Assert.False(state.CalibrationComplete);
        var refused = rig.Robot.Motion.DriveWheelsAsync(50, 50, confirmWithin: TimeSpan.FromMilliseconds(1)).GetAwaiter().GetResult();
        Assert.Equal(MotionResult.Refused, refused.Result);
        Assert.Contains("calibrating", refused.Detail);

        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false, AutoStarted = true });
        Assert.True(state.CalibrationComplete);
        var allowed = rig.Robot.Motion.DriveWheelsAsync(50, 50, confirmWithin: TimeSpan.FromMilliseconds(1)).GetAwaiter().GetResult();
        Assert.NotEqual(MotionResult.Refused, allowed.Result);

        // the same for the lift
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true, AutoStarted = true });
        Assert.False(state.CalibrationComplete);
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false, AutoStarted = true });
        Assert.True(state.CalibrationComplete);
    }

    // ---------------------------------------------------------------- 8: the fw2457 timestamp

    /// <summary>
    /// A capture with timestamp 0, as real firmware 2457 reports, dates its observations by the robot state it
    /// was paired with. With the camera's zero passed through, every object was "observed" at time 0 and every
    /// age computed from it was wrong.
    /// </summary>
    [Fact]
    public void ACaptureWithoutATimestampIsDatedByTheStateItIsPairedWith()
    {
        if (MarkerLibrary.EmbeddedOrNull is null) return;
        using var rig = new Rig();
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(200, 0, 22));
        rig.T = 50_000;
        rig.State();
        uint stateTimestamp = rig.T;

        var pd = rig.Vision.History.Latest!.Value;
        var image = new GrayImage(rig.Cal.Columns, rig.Cal.Rows);
        image.Fill(150);
        var cam = new CameraModel(rig.Cal, pd.CameraPose);
        MarkerRenderer.DrawCube(image, MarkerLibrary.EmbeddedOrNull, cam, ObjectType.Block_LIGHTCUBE1, rig.Cube.Value);

        var result = rig.Vision.ProcessCapture(image, imageId: 7, cameraTimestamp: 0);
        Assert.NotNull(result);
        Assert.Equal(stateTimestamp, result!.Timestamp);
        Assert.Equal(0u, rig.Vision.LastRawFrameTimestamp);
        var observed = Assert.Single(result.Objects);
        Assert.Equal(stateTimestamp, observed.Timestamp);
        Assert.Equal(stateTimestamp, observed.Object.LastObservedTimestamp);
    }

    /// <summary>The same for a face: its entry ages from the state's clock, so it is not forgotten at once.</summary>
    [Fact]
    public void AFaceFromATimestamplessCaptureAgesFromTheStateClock()
    {
        using var rig = new Rig();
        rig.Vision.FaceDetector = rig.FaceDetector;
        rig.Head = 0.4f;                      // looking up enough to have the face in view
        rig.T = 90_000;
        rig.State();
        uint stateTimestamp = rig.T;
        rig.FaceDetector.Faces.Add((1, new Vec3(400, 0, 250), "Jim"));

        var pd = rig.Vision.History.Latest!.Value;
        rig.FaceDetector.Camera = new CameraModel(rig.Cal, pd.CameraPose);
        var image = new GrayImage(rig.Cal.Columns, rig.Cal.Rows);
        image.Fill(150);

        var result = rig.Vision.ProcessCapture(image, imageId: 1, cameraTimestamp: 0);
        Assert.NotNull(result);
        var face = Assert.Single(rig.Vision.Faces.Faces);
        Assert.Equal(stateTimestamp, face.LastObservedTimestamp);

        // a dated face survives its own observation moment; a face stamped 0 would already be 90 s stale
        rig.Vision.Faces.Update(stateTimestamp + 1000);
        Assert.Single(rig.Vision.Faces.Faces);
    }

    // ---------------------------------------------------------------- 7: a disconnected cube

    [Fact]
    public void ADisconnectedCubeStopsBeingLocatedAndActionable()
    {
        if (MarkerLibrary.EmbeddedOrNull is null) return;
        using var rig = new Rig();
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(200, 0, 22));
        var seen = rig.Frame();
        uint cubeId = seen.Objects[0].Object.ObjectId;
        Assert.True(rig.Vision.Locator.IsLocated(cubeId));
        rig.M.Configurations.Update();

        rig.Send(new ObjectConnectionState { ObjectID = cubeId, FactoryID = 0xABCD, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = false });

        Assert.False(rig.Vision.Locator.IsLocated(cubeId));
        Assert.Null(rig.Vision.World.GetLocatedObjectById(cubeId));
        Assert.DoesNotContain(rig.Vision.World.LocatedObjects, o => o.ObjectId == cubeId);
        rig.M.Configurations.Update();
        Assert.Empty(rig.M.Configurations.Stacks);
    }

    // ---------------------------------------------------------------- 9: the path lifecycle

    /// <summary>A terminal event that arrives before anyone waits is not lost.</summary>
    [Fact]
    public void AnImmediatePathCompletionIsNotLost()
    {
        using var rig = new Rig();
        using var run = rig.M.StartPath(new PathSegment[] { new PathSegment.Line(0, 0, 100, 0, 50f, 200f, 200f) });
        rig.Send(new PathFollowingEvent { EventId = run.PathId, EventType = (byte)PathEventType.Completed });
        var ev = run.WaitAsync(TimeSpan.FromSeconds(1), default).GetAwaiter().GetResult();
        Assert.Equal(PathEventType.Completed, ev);
        Assert.False(run.Aborted);
    }

    /// <summary>A drive cancelled while the robot is following clears the path instead of leaving it running.</summary>
    [Fact]
    public void ADriveCancelledMidPathAbortsItOnTheRobot()
    {
        using var rig = new Rig();
        using var cts = new CancellationTokenSource();
        // the path is never answered (nothing pumps the fake robot), so the drive is still following when the
        // behaviour owning it is cancelled
        var drive = new DriveStraightAction(rig.M, 300, 100f).RunAsync(cts.Token);
        SpinUntil(() => rig.M.Paths.Sent.OfType<ExecutePath>().Any());
        int clearsBefore = rig.M.Paths.Sent.OfType<ClearPath>().Count();

        cts.Cancel();
        var result = drive.GetAwaiter().GetResult();
        Assert.Equal(ActionResult.CancelledWhileRunning, result);
        Assert.True(rig.M.Paths.Sent.OfType<ClearPath>().Count() > clearsBefore,
                    "the cancelled drive must clear the robot's path rather than leave the firmware following it");
    }

    private static void SpinUntil(Func<bool> condition, int ms = 3000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < end && !condition()) Thread.Sleep(5);
    }

    // ---------------------------------------------------------------- 15: planner failure is not a blind drive

    /// <summary>
    /// With a lattice planner configured and an obstacle straight across the route, a goal it cannot plan to
    /// fails as a planning failure and sends nothing. Falling back to a straight line here drove through the
    /// obstacle the planner had just refused to go round.
    /// </summary>
    [Fact]
    public void ABlockedGoalFailsPlanningInsteadOfDrivingThroughTheObstacle()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        if (MarkerLibrary.EmbeddedOrNull is null) return;
        using var rig = new Rig();
        Assert.True(rig.M.LoadPlanner(obb));
        // a real cube sitting on the goal: DriveToPoseAction imports the world's obstacles itself, so this is
        // the obstacle the planner actually sees
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(250, 0, 22));
        var seen = rig.Frame();
        if (seen.Objects.Count == 0) return;           // the render did not resolve the marker
        rig.Pump(); rig.Sent.Clear(); rig.M.Paths.Sent.Clear();

        var drive = new DriveToPoseAction(rig.M) { Goal = new Pose3d(Mat3.Identity, new Vec3(250, 0, 22)) };
        var r = drive.RunAsync(default).GetAwaiter().GetResult();
        rig.Pump();

        Assert.Equal(ActionResult.PathPlanningFailedAbort, r);
        Assert.Contains(drive.Trace, l => l.Contains("no path sent"));
        // and nothing of this stack's own is substituted: the engine returns a planning failure and the
        // action fails (M13-005). The straight-line fallback that used to sit here is gone.
        Assert.DoesNotContain(drive.Trace, l => l.Contains("straight line"));
        Assert.DoesNotContain(rig.M.Paths.Sent, m => m is ExecutePath);
        Assert.DoesNotContain(rig.Sent, m => m is ExecutePath);
    }

    // ---------------------------------------------------------------- 13: the native lift presets

    [Fact]
    public void TheLiftPresetsAreTheNativeTable()
    {
        Assert.Equal(32f, LiftPresets.LowDockMm);
        Assert.Equal(76f, LiftPresets.HighDockMm);
        Assert.Equal(92f, LiftPresets.CarryMm);
    }

    // ---------------------------------------------------------------- 17: the configuration snapshot

    /// <summary>
    /// Rebuilding the configurations while behaviours read them must never show a half-built view. The reader
    /// checks the one invariant that a torn update breaks: every pyramid's base blocks are among the bases.
    /// </summary>
    [Fact]
    public void BlockConfigurationsStayConsistentWhileTheyAreRebuilt()
    {
        if (MarkerLibrary.EmbeddedOrNull is null) return;
        using var rig = new Rig();
        var world = rig.Vision.World;
        double clock = 0;
        var manager = new BlockConfigurationManager(world, () => clock);
        // two cubes side by side with a third on top: a pyramid base and a pyramid
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(230, -22, 22));
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.Identity, new Vec3(230, 22, 22))));
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE3, new Pose3d(Mat3.Identity, new Vec3(230, 0, 66))));
        rig.Head = 0.3f;
        rig.Frame(); rig.Frame();
        manager.Update();
        if (manager.PyramidBases.Count == 0) return;   // the render did not resolve all three markers

        bool stop = false;
        Exception? failure = null;
        var reader = Task.Run(() =>
        {
            try
            {
                while (!stop)
                {
                    var bases = manager.PyramidBases;
                    foreach (var p in manager.Pyramids)
                        Assert.Contains(bases, b => b.ContainsBlock(p.Base.BlockIds[0]) && b.ContainsBlock(p.Base.BlockIds[1]));
                }
            }
            catch (Exception e) { failure = e; }
        });
        for (int i = 0; i < 400; i++) { clock = i; manager.Update(); }
        stop = true;
        reader.Wait(TimeSpan.FromSeconds(10));
        Assert.Null(failure);
        Assert.NotEmpty(manager.PyramidBases);
    }

    // ---------------------------------------------------------------- 21: the stack owns its put-down wiring

    /// <summary>
    /// A library consumer that only builds the stack still gets the put-down re-pick. Before, only the
    /// conformance tool installed it, so the assembled stack quietly lacked the behaviour it advertises.
    /// </summary>
    [Fact]
    public void TheStackItselfHandlesBeingPutDown()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        double clock = 0;
        using var stack = FreeplayStack.Create(obb, rig.Robot, Ctx(rig), () => clock, rig.Vision, rig.M, withReactions: false, random: new Random(3));
        var log = new List<string>();
        stack.Freeplay.Log += log.Add;

        stack.Tick(0, 0, rig.Robot, rig.Vision, rig.M);
        var first = stack.Freeplay.Current?.Id;
        Assert.NotNull(first);

        // picked up and put down again, through the real classifier: the head must have reported its
        // calibration first, as the engine's gate requires
        clock = 5;
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false, AutoStarted = false });
        for (int i = 0; i < 40; i++) rig.State();                                   // settle the filters
        for (int i = 0; i < 10; i++) rig.State((uint)RobotStatusFlag.IsPickedUp);    // in the air
        Assert.Equal(OffTreadsState.InAir, rig.Robot.Sensors.OffTreadsState);
        for (int i = 0; i < 40; i++) rig.State();                                   // back on its treads
        Assert.Equal(OffTreadsState.OnTreads, rig.Robot.Sensors.OffTreadsState);
        Assert.Contains(log, l => l.Contains("on put down so we pick up a new one"));
    }

    // ---------------------------------------------------------------- 19: one repetition history

    /// <summary>
    /// The repetition penalty belongs to the behaviour: a behaviour that ran under one activity is still
    /// penalised when a second activity's chooser scores it. Two private dictionaries let it escape.
    /// </summary>
    [Fact]
    public void RepetitionHistoryIsSharedBetweenActivities()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        using var manager = new BehaviorManager(ctx);
        var shared = new Fake("shared");
        var bound = new Dictionary<string, IBehavior> { ["shared"] = shared };
        var penalty = manager.Penalty;
        var graph = new Graph2d(new[] { (0.0, 0.0), (30.0, 1.0) });
        var a = new ScoringChooser(new[] { Entry("shared", 10, graph) }, bound, penalty: penalty);
        var b = new ScoringChooser(new[] { Entry("shared", 10, graph) }, bound, penalty: penalty);

        Assert.Equal(10.0, a.GetDesiredActiveBehavior(null, 0, ctx, 0).Scores.Single().Score, 3);
        penalty.Ran("shared", 0);                                   // the manager records it once
        Assert.Equal(0.0, b.GetDesiredActiveBehavior(null, 0, ctx, 0).Scores.Single().Score, 3);
        Assert.Equal(10.0, b.GetDesiredActiveBehavior(null, 0, ctx, 30).Scores.Single().Score, 3);
    }

    /// <summary>An interrupted behaviour is not penalised; one that completed is.</summary>
    [Fact]
    public void OnlyACompletedBehaviourIsPenalisedForRepeating()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        using var manager = new BehaviorManager(ctx);
        var one = new Fake("one", ticks: 1);
        var two = new Fake("two", ticks: 99);
        manager.Add(one); manager.Add(two);

        manager.StartAsync("two", 0).GetAwaiter().GetResult();
        manager.Stop(BehaviorStopReason.Interrupted, 1);
        Assert.Null(manager.Penalty.LastRunSec("two"));

        manager.StartAsync("one", 2).GetAwaiter().GetResult();
        manager.Update(0, 2);            // one tick and it is done
        manager.Update(0, 3);
        Assert.Equal(2.0, manager.Penalty.LastRunSec("one"));
    }

    private static ScoredBehaviorEntry Entry(string id, double flat, Graph2d? repetition = null) =>
        new(id, flat, repetition, null, null, Array.Empty<EmotionScorer>());

    private sealed class Fake : IBehavior
    {
        private int _left;
        private readonly int _ticks;
        public Fake(string id, int ticks = 1) { Id = id; _ticks = ticks; }
        public string Id { get; }
        public string Class => "Fake";
        public int Started;
        public bool IsRunnable(BehaviorContext c) => true;
        public double EvaluateScore(BehaviorContext c) => 1;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) { Started++; _left = _ticks; return Task.CompletedTask; }
        public bool Update(BehaviorContext c, double nowMs) => --_left > 0;
        public void Stop(BehaviorStopReason r) { }
    }

    // ---------------------------------------------------------------- 20: the duration lifecycle

    [Fact]
    public void AnActivityCannotEndBeforeItsCanEndDuration()
    {
        var inputs = new FreeplayInputs();
        // singing's shape: can end and should end at the same 5 s
        var s = new ActivityStrategy { Type = "Simple", CanEndDurationSec = 5, ShouldEndDurationSec = 5 };
        Assert.False(s.WantsToEnd(inputs, 1, out var early));
        Assert.Contains("cannot end before", early);
        Assert.False(s.WantsToEnd(inputs, 5, out _));            // at the floor, not yet past the ceiling
        Assert.True(s.WantsToEnd(inputs, 6, out var late));
        Assert.Contains("should end", late);

        // the floor overrides a subclass that would otherwise want to end
        var needs = new NeedsManager(() => 0);
        var withNeeds = new FreeplayInputs { Needs = needs };
        var n = new ActivityStrategy { Type = "Needs", Need = NeedId.Energy, NeedBracket = NeedBracketId.Critical, CanEndDurationSec = 20, ShouldEndDurationSec = -1 };
        needs.SetLevel(NeedId.Energy, 1);                         // no longer Critical: the subclass wants to end
        Assert.False(n.WantsToEnd(withNeeds, 5, out var floored));
        Assert.Contains("cannot end before", floored);
        Assert.True(n.WantsToEnd(withNeeds, 25, out _));
    }

    [Fact]
    public void StartInCooldownHoldsTheActivityBackAndTheCooldownIsRandomisedOnStart()
    {
        var inputs = new FreeplayInputs();
        var s = new ActivityStrategy { Type = "Simple", CooldownBaseSec = 10, CooldownRandomnessSec = 0, StartInCooldown = true };
        Assert.False(s.WantsToStart(inputs, 5, out var cd));
        Assert.Contains("cooldown", cd);
        Assert.True(s.WantsToStart(inputs, 11, out _));

        // without startInCooldown an activity that has never run is free immediately
        var free = new ActivityStrategy { Type = "Simple", CooldownBaseSec = 10 };
        Assert.True(free.WantsToStart(inputs, 0, out _));

        // the cooldown is re-randomised when the check passes, not when the activity ends
        var random = new ActivityStrategy { Type = "Simple", CooldownBaseSec = 10, CooldownRandomnessSec = 100, Random = new Random(7) };
        Assert.Equal(10.0, random.EffectiveCooldownSec, 3);
        Assert.True(random.WantsToStart(inputs, 0, out _));
        Assert.True(random.EffectiveCooldownSec > 10);
    }
}
