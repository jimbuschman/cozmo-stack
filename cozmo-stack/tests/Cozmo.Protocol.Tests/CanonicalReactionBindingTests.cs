using System.Net;
using System.Reflection;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class CanonicalReactionBindingTests
{
    private static string Resources()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {
            var root=Path.Combine(d.FullName,"re-analysis","obb","assets","cozmo_resources");
            if(Directory.Exists(Path.Combine(root,"config","engine","behaviorSystem"))) return root;
        }
        throw new DirectoryNotFoundException("shipped behavior resources");
    }
    private sealed class ImmediateAction : ActionRunner
    {
        internal ImmediateAction():base(8,0) { }
        protected override string ActionName=>"reaction gate fixture";
        public override bool CanInterrupt()=>false;
        public override uint CheckIfDone()=>0;
    }
    private static void OpenGate(EngineRobot native,BehaviorManager manager)
    {
        native.ActionList.Clear();
        Assert.Equal(0,native.ActionList.QueueAction(QueueActionPosition.Now,new ImmediateAction(),0));
        manager.CheckReactions(native.Engine.Timer.Seconds);
        native.ActionList.Clear();
    }
    private static void SetCalibrated(CozmoRobot robot,string property,bool value)
        =>typeof(RobotStateTracker).GetProperty(property)!.SetValue(robot.State,value);
    private static void LoadAnimations(M4ControlTests.Rig rig)
    {
        rig.Robot.Animations.LoadFrom(Path.Combine(Resources(),"assets"));
        Assert.NotNull(rig.Robot.Animations.Library);
    }

    [Fact]
    public void ConstructorPopulatesRetainedRowsAfterDelocalizeWithoutReplayingAnEmptyMapLock()
    {
        using var rig = new M4ControlTests.Rig(Resources());
        int before = -1, retained = 0;
        rig.Engine.BeforeConstructorDelocalizeForTest = owner =>
        {
            var manager = owner.BehaviorManagerOwner!;
            retained = owner.Engine.HostReactionMap.Count;
            before = manager.Reactions.Count;
            manager.DisableReactionsWithLock("before-map", Enumerable.Repeat(true, BehaviorManager.TriggerCount).ToArray(), false);
        };
        rig.ToSynced();

        var native = rig.Engine.Robot!;
        var reactions = native.BehaviorManagerOwner!;
        Assert.Equal(22, retained); // the shipped reactionTriggerBehaviorMap array
        Assert.Equal(0, before); // 0x00510324 before 0x00510396
        Assert.True(reactions.IsReactionTriggerEnabled(ReactionTrigger.CliffDetected)); // 0x005A2824: empty map returns
        Assert.False(reactions.HasDisableLock(ReactionTrigger.CliffDetected, "before-map"));
        Assert.Contains(reactions.Reactions, r => r.Strategy.Trigger == ReactionTrigger.CliffDetected);
        Assert.Contains(reactions.Reactions, r => r.Strategy.Trigger == ReactionTrigger.RobotPickedUp);
    }

    [Theory]
    [InlineData(ReactionTrigger.RobotPickedUp, "ReactToPickup")]
    [InlineData(ReactionTrigger.CliffDetected, "ReactToCliff")]
    public void FirstPhysicalEventAfterConnectionRunsTheConstructorReaction(ReactionTrigger trigger, string id)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        var registration = Assert.Single(manager.Reactions.Where(r => r.Strategy.Trigger == trigger));
        Assert.Equal(id, registration.Behavior.Id);
        // The native first-action gate (0x005A3558..0x005A356C) opens when ActionList becomes nonempty.
        Assert.Equal(0, f.Robot.ActionList.QueueAction(QueueActionPosition.Now, new ImmediateAction(), 0));
        f.Rig.Tick();
        var selected = new List<BehaviorManager.ReactionSwitch>();
        var transitions = new List<BehaviorTransitionReport>();
        f.Rig.Engine.BehaviorTransition += transitions.Add;
        manager.ReactionTriggered += selected.Add;
        if (trigger == ReactionTrigger.CliffDetected)
        {
            // Source strategy 0x0060DC7C consumes this C0 event on the next Robot::Update.
            f.Rig.Data(new CliffEvent { Timestamp = f.Rig.Robot.State.Latest!.Timestamp, DetectedFlags = 1 });
            f.Rig.Tick();
        }
        else
        {
            // Source strategy 0x0060DDCE classifies InAir after the off-treads debounce.
            for (int i = 0; i < 12 && selected.Count == 0; i++) f.Motors(RobotStatusFlag.IsPickedUp);
        }
        var first = Assert.Single(selected);
        Assert.Equal((trigger, id), (first.Trigger, first.Behavior));
        Assert.Same(registration.Behavior, manager.Current);
        Assert.Contains(transitions, transition => ((BehaviorID)transition.NewId).ToString() == id);
    }

    // 0x005A1842..1898: the actual map retains per-trigger JSON order and registrations.
    // 0x00610E50..6C: the whiteboard ID is consumed and handed to this very Ram instance.
    [Fact]
    public void CanonicalMapUsesShippedOrderAndSameRamRecipientAcrossRebindingAndObserver()
    {
        using var rig=new M4ControlTests.Rig(Resources()); rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var manager=native.BehaviorManagerOwner!;var runtime=native.NativeBehaviorRuntime!;
        var before=manager.Reactions.ToArray();
        var actual=before.Select(r=>(r.Strategy.Trigger,r.Behavior.Id)).ToArray();
        var supported=actual.ToHashSet();
        var expected=ReactionTriggerMap.Load(Resources()).Where(r=>supported.Contains((r.Trigger,r.BehaviorId)))
            .OrderBy(r=>r.Trigger).Select(r=>(r.Trigger,r.BehaviorId)).ToArray();
        Assert.Equal(expected,actual);
        Assert.Equal(new[]{"ReactToFrustrationMinor","ReactToFrustrationMajor"},before.Where(r=>r.Strategy.Trigger==ReactionTrigger.Frustration).Select(r=>r.Behavior.Id));
        Assert.Single(before.Where(r=>r.Strategy.Trigger==ReactionTrigger.MotorCalibration));
        Assert.Single(before.Where(r=>r.Strategy.Trigger==ReactionTrigger.CliffDetected));
        var reg=Assert.Single(before.Where(r=>r.Strategy.Trigger==ReactionTrigger.NoPreDockPoses));
        var ram=Assert.IsType<RamIntoBlockBehavior>(reg.Behavior);
        runtime.Manipulation!.Whiteboard.NoPreDockPosesObjectId=123;
        reg.Strategy.ShouldTriggerBehavior(new ReactionContext(runtime.Context,rig.Engine.Timer.Seconds),ram);
        Assert.Null(runtime.Manipulation.Whiteboard.NoPreDockPosesObjectId);Assert.Equal(123u,ram.PendingTarget);
        manager.BindReactionDependencies(runtime.Manipulation,runtime.Bound);
        using(var observer=FreeplayStack.CreateNative(rig.Robot))
        { Assert.Same(manager,observer.Manager);Assert.Same(runtime.Context,observer.Context); }
        Assert.Equal(before,manager.Reactions);
        Assert.Same(runtime,native.NativeBehaviorRuntime);
        Assert.Same(reg,manager.Reactions.Single(r=>r.Strategy.Trigger==ReactionTrigger.NoPreDockPoses));
    }

    // 0x0060654E..66E and 0x00606742..98: the real ingress selects a five-second
    // wait; the two calibrated bytes are sampled only on the calibration message.
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AutomaticCalibrationStartsCanonicalWaitAndUsesBothFlagsOnTag30(bool observer)
    {
        using var rig=new M4ControlTests.Rig(Resources());LoadAnimations(rig);rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var manager=native.BehaviorManagerOwner!;
        Assert.Same(rig.Robot.Animations.DrivingTriggerMap,native.NativeBehaviorRuntime!.Context.Triggers);
        var behavior=Assert.IsType<ReactToMotorCalibrationBehavior>(manager.Reactions.Single(r=>r.Strategy.Trigger==ReactionTrigger.MotorCalibration).Behavior);
        var strategy=Assert.IsType<GenericReactionStrategy>(manager.Reactions.Single(r=>r.Strategy.Trigger==ReactionTrigger.MotorCalibration).Strategy);
        var diagnostics=new List<string>();
        string Snapshot(string label)=>$"{label}: current={manager.Current?.Id ?? "null"}, trigger={manager.CurrentReactionTrigger}, latched={strategy.Latched}, enabled={manager.IsReactionTriggerEnabled(ReactionTrigger.MotorCalibration)}, initialized={manager.Initialized}, library={rig.Robot.Animations.Library is not null}, initFailed={behavior.InitFailed}, running={behavior.EngineRunning}, tag={behavior.CurrentRunnerTag}, owned={behavior.OwnedRunner?.GetType().Name ?? "null"}, listEmpty={native.ActionList.IsEmpty}, trace=[{string.Join(" | ",behavior.Trace)}]";
        manager.Log+=line=>diagnostics.Add("manager: "+line);
        manager.ReactionTriggered+=sw=>diagnostics.Add(Snapshot("triggered "+sw.Trigger));
        OpenGate(native,manager);
        diagnostics.Add(Snapshot("before ingress"));
        using var handle=observer?FreeplayStack.CreateNative(rig.Robot):null;
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=true,AutoStarted=true});
        rig.Tick(0);
        diagnostics.Add(Snapshot("after tick"));
        manager.CheckReactions(rig.Engine.Timer.Seconds);
        diagnostics.Add(Snapshot("after check"));
        Assert.True(ReferenceEquals(behavior,manager.Current),string.Join(Environment.NewLine,diagnostics));
        var wait=Assert.IsType<NativeTimedWaitRunner>(behavior.OwnedRunner);Assert.Equal(5f,wait.Duration);
        handle?.Dispose();
        SetCalibrated(rig.Robot,nameof(RobotStateTracker.HeadCalibrated),true);
        SetCalibrated(rig.Robot,nameof(RobotStateTracker.LiftCalibrated),true);
        Assert.Same(wait,behavior.OwnedRunner);
        SetCalibrated(rig.Robot,nameof(RobotStateTracker.LiftCalibrated),false);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=false});
        rig.Tick(0);
        Assert.Same(wait,behavior.OwnedRunner);
        SetCalibrated(rig.Robot,nameof(RobotStateTracker.HeadCalibrating),true);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_LIFT,CalibStarted=false});
        rig.Tick(0);
        Assert.True(rig.Robot.State.HeadCalibrating);
        Assert.True(behavior.Completed);Assert.Null(behavior.OwnedRunner);
    }

    [Theory]
    [InlineData(MotorID.MOTOR_LIFT, false, true, true)]
    [InlineData(MotorID.MOTOR_LIFT, false, false, true)]
    [InlineData(MotorID.MOTOR_LIFT, true, true, true)]
    [InlineData(MotorID.MOTOR_LIFT, true, false, true)]
    [InlineData(MotorID.MOTOR_HEAD, false, true, true)]
    [InlineData(MotorID.MOTOR_LIFT, false, true, false)]
    public void MotorAutoEnabledPublishesAfterTheNativeDetachGate(MotorID motor, bool enabled, bool carrying, bool hasInterface)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var engine = f.Rig.Engine;
        var cube = f.Add();
        var carry = f.Robot.CarryingOwner!;
        if (carrying)
        {
            using var liftPose = new Pose3d(Rotation3d.Identity, new Vec3(2, 3, 4), parent: f.Robot.LiftPose);
            cube.Pose = new Pose3d(liftPose);
            cube.NativeCarryingTree = true;
            carry.AttachToLift(cube.ObjectId, cube.Markers[0], new Pose3d(liftPose), null, null);
        }

        var order = new List<string>();
        var logs = new List<EngineLogEntry>();
        var wire = new List<byte[]>();
        var app = new List<MotorAutoEnabledReport>();
        var local = new List<(MotorAutoEnabledReport Report, uint? Carried)>();
        engine.LogEntry += entry =>
        {
            if (entry.Name?.StartsWith("HandleMotorAutoEnabled.", StringComparison.Ordinal) != true) return;
            logs.Add(entry);
            if (entry.Level == EngineLogLevel.Event) order.Add("event");
        };
        using var delete = engine.AnimationMessages.SubscribeMessage(0x4C, true, payload =>
        {
            Assert.Equal(cube.ObjectId, Assert.IsType<uint>(payload));
            order.Add("delete");
        });
        using var reports = engine.AnimationMessages.SubscribeMessage(0x1F, true, payload =>
        {
            local.Add((Assert.IsType<MotorAutoEnabledReport>(payload), carry.CarriedObjectId));
            order.Add("report");
        });
        engine.EngineToGameMessage += (tag, payload) => { if (tag == 0x1F) wire.Add(payload.ToArray()); };
        f.Rig.Robot.Sensors.MotorAutoEnabledReported += app.Add;

        f.Robot.HasExternalInterface = hasInterface;
        bool calibrated = f.Rig.Robot.State.LiftCalibrated;
        int mark = f.Rig.Mark();
        f.Rig.Data(new MotorAutoEnabled { MotorID = motor, Enabled = enabled });
        f.Rig.Tick(0);
        engine.DrainAppDelivery();

        bool detached = carrying && motor == MotorID.MOTOR_LIFT && !enabled;
        uint? carriedAfter = carrying && !detached ? cube.ObjectId : null;
        Assert.Equal(carriedAfter, carry.CarriedObjectId);
        if (detached) Assert.Null(f.M.World.GetLocatedObjectByIdHelper(cube.ObjectId));
        else Assert.Same(cube, f.M.World.GetLocatedObjectByIdHelper(cube.ObjectId));
        Assert.Equal(detached ? 1 : 0, f.Rig.SentSince(mark).OfType<CarryStateUpdate>().Count(m => m.State == CarryState.CARRY_NONE));
        Assert.Equal(calibrated, f.Rig.Robot.State.LiftCalibrated);
        Assert.Collection(logs,
            info => Assert.Equal($"Motor {(byte)motor}, enabled {(enabled ? 1 : 0)}", info.Text),
            evt =>
            {
                Assert.Equal(enabled ? "HandleMotorAutoEnabled.MotorEnabled" : "HandleMotorAutoEnabled.MotorDisabled", evt.Name);
                Assert.Equal(motor.ToString(), evt.Text);
            });
        if (hasInterface)
        {
            Assert.Equal(detached ? new[] { "event", "delete", "report" } : new[] { "event", "report" }, order);
            var expected = new MotorAutoEnabledReport(motor, enabled);
            Assert.Equal((expected, carriedAfter), Assert.Single(local));
            Assert.Equal(expected, Assert.Single(app));
            Assert.Equal(new byte[] { (byte)motor, enabled ? (byte)1 : (byte)0 }, Assert.Single(wire));
        }
        else
        {
            Assert.Empty(local);
            Assert.Empty(app);
            Assert.Empty(wire);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiftCalibrationReportsBeforeDeletingCarriedCubeAndStartingReaction(bool carrying)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var native = f.Robot;
        var manager = native.BehaviorManagerOwner!;
        var cube = f.Add();
        var unrelated = f.Add(8, x: 400);
        var carry = f.M.Docking.Carrying;
        Assert.Same(native.CarryingOwner, carry);
        if (carrying)
        {
            using var liftPose = new Pose3d(Rotation3d.Identity, new Vec3(2, 3, 4), parent: native.LiftPose);
            cube.Pose = new Pose3d(liftPose);
            cube.NativeCarryingTree = true;
            carry.AttachToLift(cube.ObjectId, cube.Markers[0], new Pose3d(liftPose), null, null);
        }

        OpenGate(native, manager);
        Assert.True(f.Rig.Robot.State.LiftCalibrated);
        var order = new List<string>();
        var events = new List<EngineLogEntry>();
        var appReports = new List<MotorCalibration>();
        (uint? Carried, bool Calibrated)? atStart = null;
        (uint? Carried, bool Calibrated)? atReport = null;
        f.Rig.Engine.LogEntry += entry =>
        {
            if (entry.Name is not ("HandleMotorCalibration.Start" or "HandleMotorCalibration.Complete")) return;
            events.Add(entry);
            if (entry.Name == "HandleMotorCalibration.Start")
            {
                atStart = (carry.CarriedObjectId, f.Rig.Robot.State.LiftCalibrated);
                order.Add("start");
            }
        };
        var deletedIds = new List<uint>();
        using var delete = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4C, true, payload =>
        {
            if (payload is uint id) deletedIds.Add(id);
            order.Add("delete");
        });
        f.Rig.Robot.Sensors.MotorCalibrationReportedInternal += report =>
        {
            if (report.CalibStarted)
            {
                atReport = (carry.CarriedObjectId, f.Rig.Robot.State.LiftCalibrated);
                order.Add("report");
            }
        };
        f.Rig.Robot.Sensors.MotorCalibrationReported += appReports.Add;
        int mark = f.Rig.Mark();

        f.Rig.Data(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true, AutoStarted = true });
        f.Rig.Tick(0);
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        f.Rig.Engine.DrainAppDelivery();

        Assert.Equal((carrying ? cube.ObjectId : (uint?)null, true), atStart);
        Assert.Equal(((uint?)null, false), atReport);
        Assert.Equal(carrying ? new[] { "start", "delete", "report" } : new[] { "start", "report" }, order);
        Assert.Equal(carrying ? new[] { cube.ObjectId } : Array.Empty<uint>(), deletedIds);
        Assert.Null(carry.CarriedObjectId);
        if (carrying) Assert.Null(f.M.World.GetLocatedObjectByIdHelper(cube.ObjectId));
        else Assert.Same(cube, f.M.World.GetLocatedObjectByIdHelper(cube.ObjectId));
        Assert.Same(unrelated, f.M.World.GetLocatedObjectByIdHelper(unrelated.ObjectId));
        Assert.Equal(carrying ? 1 : 0, f.Rig.SentSince(mark).OfType<CarryStateUpdate>().Count(m => m.State == CarryState.CARRY_NONE));
        var report = Assert.Single(appReports);
        Assert.Equal(MotorID.MOTOR_LIFT, report.MotorID);
        Assert.True(report.CalibStarted && report.AutoStarted);
        var start = Assert.Single(events);
        Assert.Equal(EngineLogLevel.Event, start.Level);
        Assert.Equal("HandleMotorCalibration.Start", start.Name);
        Assert.Equal("MOTOR_LIFT", start.Text);
        Assert.Equal("1", start.Data["$data"]);

        var behavior = Assert.IsType<ReactToMotorCalibrationBehavior>(manager.Current);
        Assert.Equal(5f, Assert.IsType<NativeTimedWaitRunner>(behavior.OwnedRunner).Duration);
        f.Rig.Data(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false, AutoStarted = false });
        f.Rig.Tick(0);
        Assert.True(behavior.Completed);
        Assert.True(f.Rig.Robot.State.LiftCalibrated);
        Assert.Equal(2, events.Count);
        Assert.Equal("HandleMotorCalibration.Complete", events[1].Name);
        Assert.Equal("MOTOR_LIFT", events[1].Text);
        Assert.Equal("0", events[1].Data["$data"]);
    }

    [Fact]
    public void FistBumpObjectiveRunsSearchVerifiedFaceRequestAndSuccess()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var fist = StartFistBumpReaction(f, out var strategy);
        var manager = f.Robot.BehaviorManagerOwner!;
        Assert.Equal(FistBumpBehavior.TurnToFace, fist.State);
        Assert.Contains((FistBumpBehavior.IdleTriggerCount, "Behavior_FistBump"), f.Rig.Robot.Animations.Scheduler.IdleStack);
        Assert.True(manager.HasDisableLock(ReactionTrigger.CubeMoved, "FistBump_behaviorLock"));
        var objectives = new List<uint>();
        using var reports = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true, payload =>
        {
            if (payload is uint objective) objectives.Add(objective);
        });
        var needs = new List<NeedsActionId>();
        f.Rig.Engine.Needs.NeedsStateSent += needs.Add;
        int powerMark = f.Rig.Mark();
        void Update() => fist.Update(f.Runtime.Context, f.Rig.Engine.Timer.TimeStampMs);
        void Complete(uint result = EngineActionResult.Success)
        {
            Assert.NotEqual(0u, fist.CurrentRunnerTag);
            f.Rig.Engine.AnimationMessages.Deliver(0x5A, true,
                new RobotCompletedAction(fist.CurrentRunnerTag, 0, result, Array.Empty<uint>(), 0));
        }
        void Animation(AnimationTrigger trigger) =>
            Assert.Equal(trigger, Assert.IsType<TriggerAnimationAction>(fist.OwnedRunner).Request.Trigger);

        Update();
        Assert.True(Assert.IsType<NativeLastFaceRunner>(fist.OwnedRunner).RequireVerifiedFace);
        Complete(FistBumpBehavior.NoFaceResult);
        Assert.Equal(FistBumpBehavior.SearchState, fist.State);
        Update();
        var scan = Assert.IsType<NativePanTiltRunner>(fist.OwnedRunner);
        Assert.Equal(FistBumpBehavior.ScanPanRad[0], scan.Parameters.PanAngleRad);
        Assert.False(scan.Parameters.PanIsAbsolute);
        Assert.True(scan.Parameters.HeadIsAbsolute);
        Complete();

        uint when = f.Robot.StoredState!.Timestamp;
        var origin = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        var position = new Vec3(150, 0, 40);
        using var head = new Pose3d(Rotation3d.Identity, position);
        var face = new TrackedFace(new DetectedFace(7, new FaceRect(0,0,10,10), new Vec2(1,1), new Vec2(2,1), Name: "Taylor"), when)
        {
            HeadPose = new Pose3d(head), NativeTrackingTranslation = position, NativeTrackingRotation = Rotation3d.Identity
        };
        Assert.NotNull(f.M.Vision.Faces.AddOrUpdateNativeFace(face, f.Robot.GetCheckedMainPose(),
            false, origin.Node!, true, requireNativePose: true, expectedOwner: f.Robot));
        Update();
        Assert.False(Assert.IsType<NativeLastFaceRunner>(fist.OwnedRunner).RequireVerifiedFace);
        Assert.Equal(FistBumpBehavior.Request, fist.State);
        Complete();

        Update(); Animation(AnimationTrigger.FistBumpRequestOnce);
        Complete();
        Update(); Animation(AnimationTrigger.FistBumpIdle);
        Update();
        Assert.Equal(FistBumpBehavior.DetectBump, fist.State);
        Assert.Equal(new[] { (2, 0), (3, 0) }, MotorPower());

        // A recorded lift delta of 0.06 radians crosses the native half-degree threshold.
        var bump = f.Rig.MakeState(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos,
            origin: f.Robot.CurrentOriginId, frame: f.Robot.PoseFrameId, head: f.Head, liftAngle: 0.06f);
        f.Rig.Data(bump);
        f.Rig.Tick(0);
        if (fist.State == FistBumpBehavior.DetectBump) Update();
        Assert.Equal(FistBumpBehavior.Success, fist.State);
        Animation(AnimationTrigger.FistBumpSuccess);
        Assert.Equal(new[] { (2, 0), (3, 0), (2, 1), (3, 1) }, MotorPower());
        Assert.Empty(objectives);
        Complete();
        Update();
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(new uint[] { 8, 7 }, objectives);
        Assert.Contains(NeedsActionId.FistBump, needs);
        Assert.False(strategy.Pending);
        Assert.Equal(f.Rig.Engine.Timer.SecondsF, strategy.LastCompletion);
        Assert.True(strategy.LastCompletion > 0);
        manager.Stop(BehaviorStopReason.Completed, f.Rig.Engine.Timer.Seconds);
        Assert.DoesNotContain(f.Rig.Robot.Animations.Scheduler.IdleStack, entry => entry.Lock == "Behavior_FistBump");
        Assert.False(manager.HasDisableLock(ReactionTrigger.CubeMoved, "FistBump_behaviorLock"));
        f.Rig.Engine.ReportBehaviorObjective(3, "BuiltPyramid", "fixture");
        Assert.False(strategy.Pending); // Within the shipped 180-second cooldown.

        (int, int)[] MotorPower() => f.Rig.SentSince(powerMark).OfType<EnableMotorPower>()
            .Select(m => ((int)m.Field0, (int)m.Field1)).ToArray();
    }

    [Fact]
    public void FistBumpReactionReadsCarriedIdAndQueuesNativeGroundPlacement()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var cube = f.Add();
        using var liftPose = new Pose3d(Rotation3d.Identity, new Vec3(2, 3, 4), parent: f.Robot.LiftPose);
        cube.Pose = new Pose3d(liftPose);
        cube.NativeCarryingTree = true;
        f.M.Docking.Carrying.AttachToLift(cube.ObjectId, cube.Markers[0], new Pose3d(liftPose), null, null);
        var fist = StartFistBumpReaction(f, out _);
        Assert.Equal(FistBumpBehavior.PutDown, fist.State);
        int mark = f.Rig.Mark();
        fist.Update(f.Runtime.Context, f.Rig.Engine.Timer.TimeStampMs);
        var ground = Assert.IsType<NativePlaceGroundRunner>(fist.OwnedRunner);
        f.Robot.ActionList.Update();
        Assert.Equal(cube.ObjectId, ground.SavedObject);
        Assert.Contains(f.Rig.SentSince(mark), message => message is PlaceObjectOnGround);
        Assert.Equal(FistBumpBehavior.TurnToFace, fist.State);
        Assert.Same(ground, fist.OwnedRunner);
    }

    private static FistBumpBehavior StartFistBumpReaction(PickupPyramidNativeExecutionTests.Fixture f,
        out FistBumpReactionStrategy strategy)
    {
        var manager = f.Robot.BehaviorManagerOwner!;
        typeof(OffTreadsClassifier).GetProperty("Current")!.SetValue(f.Rig.Robot.Sensors.OffTreads, OffTreadsState.OnTreads);
        f.Robot.Progression!.SetUnlock(35, true);
        OpenGate(f.Robot, manager);
        var registration = Assert.Single(manager.Reactions, r => r.Strategy.Trigger == ReactionTrigger.FistBump);
        strategy = Assert.IsType<FistBumpReactionStrategy>(registration.Strategy);
        f.Rig.Engine.ContextRandom.WordSourceForTest = () => 0;
        f.Rig.Engine.ReportBehaviorObjective(3, "BuiltPyramid", "fixture");
        Assert.True(strategy.Pending);
        Assert.Equal(f.Rig.Engine.Timer.SecondsF + 3f, strategy.Expiry);
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        Assert.Same(registration.Behavior, manager.Current);
        Assert.Equal(ReactionTrigger.FistBump, manager.CurrentReactionTrigger);
        return Assert.IsType<FistBumpBehavior>(manager.Current);
    }

    [Fact]
    public void AutomaticCalibrationWaitExpiresAtFiveSeconds()
    {
        using var rig=new M4ControlTests.Rig(Resources());LoadAnimations(rig);rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var manager=native.BehaviorManagerOwner!;OpenGate(native,manager);
        Assert.Same(rig.Robot.Animations.DrivingTriggerMap,native.NativeBehaviorRuntime!.Context.Triggers);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=true,AutoStarted=true});
        rig.Tick(0);
        manager.CheckReactions(rig.Engine.Timer.Seconds);
        var behavior=Assert.IsType<ReactToMotorCalibrationBehavior>(manager.Current);
        var wait=Assert.IsType<NativeTimedWaitRunner>(behavior.OwnedRunner);
        rig.Tick(1);rig.Tick(4999);Assert.Same(wait,behavior.OwnedRunner);
        rig.Tick(2);Assert.Null(behavior.OwnedRunner);Assert.False(behavior.Completed);
    }

    [Fact]
    public void CapturedPickupAndFallDeferImpactUntilTheRobotReturnsToTreads()
    {
        using var fixture = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var rig = fixture.Rig;
        var native = fixture.Robot;
        var manager = native.BehaviorManagerOwner!;
        OpenGate(native, manager);
        rig.Tick(4000); // Native fall latch compares unsigned absolute stamps against now - 3000.
        var capture = new M15CubeRun("", "", IPAddress.Loopback, "", [], [], false, false) { Personality = true };
        capture.SubscribeBehaviorReports(rig.Engine);
        var transitions = new List<(ReactionTrigger Trigger, OffTreadsState Treads)>();
        manager.ReactionTriggered += sw => transitions.Add((sw.Trigger, rig.Robot.Sensors.OffTreadsState));
        var falling = (GenericReactionStrategy)manager.Reactions.Single(r => r.Strategy.Trigger == ReactionTrigger.RobotFalling).Strategy;
        var impact = (ReactToImpactBehavior)manager.Reactions.Single(r => r.Strategy.Trigger == ReactionTrigger.RobotFalling).Behavior;
        var cliff = (ReactToCliffBehavior)manager.Reactions.Single(r => r.Strategy.Trigger == ReactionTrigger.CliffDetected).Behavior;

        // 20261116-193450-PERSONALITY-HW-1-59ef28. Keep the captured pitch, accelerometer and gyro
        // fields at the status changes, plus both fall and calibration bursts. Group by host tick.
        (int Tick, string Hex)[] replay =
        {
            (1, "F0564500000000000001000000DC0E39C147C89F410000000076079C3F421208BD0000000000000000863C3F3F45B641BEAD5F0F463CBFA9C463F72D46D0BAB3BE39ECE23ECC961BBF00F37D401003000045020000000000000000FF"), // frame 1998
            (231, "F00A4600000000000001000000DC0E39C147C89F4100000000D67F713F8F6589BD0000000000000000869C3A3F45B641BEB974A845001579416445CE45AF9C903E89D8E3BDD71198BF00F37D40194100000B000000000000000000FF"), // frame 2014
            (406, "DD37460000"), // frame 2033
            (406, "F0DC460000010000000200000000000000000000000000000090ED86BDF683A6BD000000000000000086FC353F45B641BEF64F814589DCC644A931A8451AF6DABE9DAC31BE3DF991BE00B87E4039C0000009000000000000000000FF"), // frame 2035
            (646, "DED147000004010000D07AB443"), // frame 2060
            (646, "D1020101"), // frame 2060
            (646, "D1030101"), // frame 2060
            (701, "F0EA47000001000000020000000000000000000000000000009887ADBD000000000000000000000000F466DFBE45064BBED98784C52DD7A0442A20ED45214BA2BE451A8BBE59BE88BE00F37D401140000009000000000000000000FF"), // frame 2064
            (1246, "D1030000"), // frame 2151
            (1246, "F0424A0000010000000200000000000000000000000000000080489DBD9EE037BC0000000000000000F366DFBE455654BE40E98AC51038B742AC30F545693264BD499F0CBE74F2CE3D00F37D401142000009000000000000000000FF"), // frame 2159
            (1966, "F0F44C00000200000003000000000000000000000000000000A0AB53BD663069BD0000000000000000F366DFBE45064BBEA2528BC5373B8DC41A310B4638DD693D548278BE99B3F6BE00B87E401042000009000000000000000000FF"), // frame 2272
            (1966, "D1020000"), // frame 2272
            (1966, "F0124D0000020000000300000000000000000000000000000050B291BD843E2FBD0000000000000000F366DFBE45064BBEC09686C5DA756BC4E2770746CBA7603E9F26DFBE129831BF00ED7C401042000009000000000000000000FF"), // frame 2275
            (2086, "F06C4D00000200000003000000000000000000000000000000CC600FBEF18406BC0000000000000000F366DFBE45064BBEEFA18CC5F8781DC3F0DF0B46B98A1F3E74643ABEE1CB46BF00B87E401843000009000000000000000000FF"), // frame 2288
            (3346, "F07652000003000000040000000000000000000000000000001807923DDC6518BE0000000000000000F366DFBE45064BBE467E8DC5701518C3646EE14546DA1DBE0570493D8AE10EBF00B87E40384300000A000000000000000000FF"), // frame 2486
            (3346, "DDDB510000"), // frame 2486
            (3586, "DE75530000BE00000080AB6A43"), // frame 2517
            (3586, "D1020101"), // frame 2517
            (3586, "D1030101"), // frame 2517
            (3586, "F0845300000300000004000000000000000000000000000000A058DCBC000000000000000000000000F366DFBE45064BBE19385BC5A67B30C4613D3046C647DA3D647359BE6C81BABE00B87E40104300008C000000000000000000FF"), // frame 2518
            (4126, "D1030000"), // frame 2575
            (4126, "F0825500000300000004000000000000000000000000000000D053DBBC2E11233B0000000000000000F366DFBE455654BE986958C5F63553C497870C468CE0AA3B95E1CF3BBB55C53A00ED7C401003000079030000000000000000FF"), // frame 2576
            (4366, "F07256000004000000050000000000000000000000000000000000D037E418043D0000000000000000F366DFBE45064BBE5D7E67C542A70EC4F5950C462842C3B9E0A66B390D2582B800B87E401103000079030000000000000000FF"), // frame 2604
            (4366, "D1020000"), // frame 2604
        };
        int elapsed = 0;
        foreach (var batch in replay.GroupBy(p => p.Tick))
        {
            foreach (var packet in batch)
            {
                var message = RobotMessage.Parse(Convert.FromHexString(packet.Hex));
                if (message is RobotState state)
                {
                    state.PoseOriginId = native.CurrentOriginId;
                    state.PoseFrameId = native.PoseFrameId;
                }
                rig.Data(message);
            }
            rig.Tick(batch.Key - elapsed);
            manager.CheckReactions(rig.Engine.Timer.Seconds);
            elapsed = batch.Key;
            if (elapsed is 406 or 3346)
            {
                Assert.Equal(OffTreadsState.Falling, rig.Robot.Sensors.OffTreadsState);
                Assert.True(falling.Latched);
                Assert.False(impact.IsRunnable(fixture.Runtime.Context));
                Assert.False(cliff.IsRunnable(fixture.Runtime.Context));
            }
        }
        Assert.True(transitions.Count != 0, string.Join(" | ", rig.Log.TakeLast(30)));
        Assert.Equal(ReactionTrigger.RobotPickedUp, transitions[0].Trigger);
        Assert.Equal(OffTreadsState.InAir, transitions[0].Treads);
        Assert.Collection(transitions.Where(s => s.Trigger == ReactionTrigger.RobotFalling),
            first => Assert.Equal(OffTreadsState.OnTreads, first.Treads),
            second => Assert.Equal(OffTreadsState.OnTreads, second.Treads));
        Assert.False(impact.ImpactRecorded); // 361 and 235 are below the animation threshold of 1000.
        rig.Engine.DrainAppDelivery();
        var check = PersonalityHw.SelectSteps("react-to-impact").Single().Check!;
        Assert.Equal(2, capture.StepTransitions(check, 0, double.MaxValue).Count(check.Starts));
    }

    [Theory]
    [InlineData("react-to-placed-on-slope")]
    [InlineData("react-to-unexpected-movement")]
    [InlineData("react-to-motor-calibration")]
    [InlineData("react-to-on-charger")]
    [InlineData("react-to-frustration")]
    public async Task PersonalityReactionRoundTests_ReplayedTriggerPublishesTheExpectedEndpoint(string stepId)
    {
        using var fixture = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var rig = fixture.Rig;
        var native = fixture.Robot;
        var manager = native.BehaviorManagerOwner!;
        OpenGate(native, manager);
        var capture = new M15CubeRun("", "", IPAddress.Loopback, "", [], [], false, false) { Personality = true };
        capture.SubscribeBehaviorReports(rig.Engine);
        var check = PersonalityHw.SelectSteps(stepId).Single().Check!;
        RobotState State(RobotStatusFlag extra = 0) => rig.MakeState(
            RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos | extra,
            origin: native.CurrentOriginId, frame: native.PoseFrameId);
        void Tick(double milliseconds = 33)
        {
            rig.Tick(milliseconds);
            manager.CheckReactions(rig.Engine.Timer.Seconds);
            rig.Engine.DrainAppDelivery();
        }
        bool Started() => capture.StepTransitions(check, 0, double.MaxValue).Any(check.Starts);

        switch (check.Trigger)
        {
            case ReactionTrigger.RobotPlacedOnSlope:
                var tilted = State(RobotStatusFlag.IsPickedUp);
                tilted.Pose.Pitch = 0.35f;
                tilted.Gyro = new GyroData { X = 0.1f };
                rig.Data(tilted);
                Tick();
                Assert.False(Started());
                tilted = State(RobotStatusFlag.IsPickedUp);
                tilted.Pose.Pitch = 0.35f;
                rig.Data(tilted);
                Tick(450);
                break;
            case ReactionTrigger.UnexpectedMovement:
                Assert.True(native.IsPhysicalRobot);
                for (int i = 0; i < 11; ++i)
                {
                    var turning = State(RobotStatusFlag.AreWheelsMoving);
                    turning.LwheelSpeedMmps = -35f;
                    turning.RwheelSpeedMmps = 35f;
                    rig.Data(turning);
                    Tick();
                    if (i < 10) Assert.False(Started());
                }
                break;
            case ReactionTrigger.MotorCalibration:
                rig.Data(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true });
                Tick();
                Assert.False(Started());
                rig.Data(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true, AutoStarted = true });
                Tick();
                break;
            case ReactionTrigger.PlacedOnCharger:
                rig.Data(State(RobotStatusFlag.IsOnCharger));
                Tick();
                Assert.False(Started());
                rig.Data(State());
                Tick(22_000);
                rig.Data(State(RobotStatusFlag.IsOnCharger));
                Tick();
                break;
            case ReactionTrigger.Frustration:
                for (int i = 0; i < 12 && !Started(); ++i)
                {
                    var failedDrive = PersonalityHw.QueueAbsentObjectDrive(rig.Engine, CancellationToken.None);
                    Tick();
                    Assert.Equal((uint)ActionResult.BadObject, await failedDrive.WaitAsync(TimeSpan.FromSeconds(2)));
                    Tick();
                    if (i == 0) Assert.False(Started());
                    if (!Started()) Tick(30_000);
                }
                break;
        }

        Assert.True(Started(), string.Join(" | ", rig.Log.TakeLast(25)));
    }

    [Fact]
    public void FreeplayAdmissionRetainsRunningReaction()
    {
        using var rig=new M4ControlTests.Rig(Resources());LoadAnimations(rig);rig.ToSynced();rig.Calibrate();
        rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger);
        var native=rig.Engine.Robot!;var manager=native.BehaviorManagerOwner!;OpenGate(native,manager);
        rig.Robot.ActivateHighLevelActivity("PutDownDispatch");rig.Tick(10);
        var previous=Assert.IsType<DriveOffChargerBehavior>(manager.Current);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=true,AutoStarted=true});
        rig.Tick(0);manager.CheckReactions(rig.Engine.Timer.Seconds);
        var reaction=Assert.IsType<ReactToMotorCalibrationBehavior>(manager.Current);
        Assert.Same(previous,manager.ParkedBehavior);
        rig.Robot.ActivateHighLevelActivity("Freeplay");rig.Tick(0);
        Assert.Same(reaction,manager.Current);
        Assert.Equal(ReactionTrigger.MotorCalibration,manager.CurrentReactionTrigger);
        Assert.Null(manager.ParkedBehavior);
        Assert.Same(native.NativeBehaviorRuntime!.FreeplayObserver,manager.Activity);
    }
    // 0x0060532A..53A0: the retained canonical behavior queues the real drive, or
    // broadcasts finished before the objective when the event byte is clear.
    [Theory] [InlineData(false)] [InlineData(true)]
    public void RetainedCliffUsesActualDriveOwnerOrReportsFinish(bool cliff)
    {
        using var rig=new M4ControlTests.Rig(Resources());LoadAnimations(rig);rig.ToSynced();rig.State();rig.Calibrate();rig.Tick(0);
        var native=rig.Engine.Robot!;var runtime=native.NativeBehaviorRuntime!;
        Assert.Same(rig.Robot.Animations.DrivingTriggerMap,runtime.Context.Triggers);
        var manager=native.BehaviorManagerOwner!;
        var behavior=Assert.IsType<ReactToCliffBehavior>(manager.Reactions.Single(r=>r.Strategy.Trigger==ReactionTrigger.CliffDetected).Behavior);
        OpenGate(native,manager);
        var order=new List<string>();
        behavior.RobotCliffEventFinishedInternal+=()=>order.Add("finished");
        using var objective=rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint id && id==0x1C)order.Add("objective");});
        // Unit ingress boundary: run the actual handler without an intervening ActionList
        // tick, so cancellation below is deterministic and does not depend on audio startup.
        rig.Engine.InvokeEngineRequest(()=>{rig.Robot.Sensors.Handle(new CliffEvent{Timestamp=123,DetectedFlags=1},native);return true;});
        manager.CheckReactions(rig.Engine.Timer.Seconds);
        Assert.Same(behavior,manager.Current);
        if(!cliff)rig.Engine.InvokeEngineRequest(()=>{rig.Robot.Sensors.Handle(new CliffEvent{Timestamp=124,DetectedFlags=0},native);return true;});
        // 6051BC's resultless completion runs even for an unstarted cancellation. Use
        // the real native queue and synchronous RobotCompletedAction, not a legacy ticket.
        var animation=Assert.IsType<TriggerLiftSafeAnimationAction>(behavior.OwnedRunner);
        Assert.Same(animation,native.ActionList.GetCurrentAction());
        native.ActionList.Cancel(animation.Tag);
        if(cliff)
        {
            var drive=Assert.IsType<DriveStraightAction>(native.ActionList.GetCurrentAction());
            Assert.Equal(-60d,drive.DistanceMm);Assert.Equal(100f,drive.SpeedMmps);Assert.True(drive.ShouldPlayAnimation);
            Assert.Empty(order);
        }
        else Assert.Equal(new[]{"finished","objective"},order);
        behavior.Stop(BehaviorStopReason.Cancelled);
    }

    // 0x006113F2..13FC / 0x006114D8..151A. Source-derived observation inputs
    // are delivered through the world's real event to its retained strategy.
    [Fact]
    public void CanonicalObjectStrategyReadsLiveCarryAndRetainedDockTarget()
    {
        using var rig=new M4ControlTests.Rig(Resources());rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var manager=native.BehaviorManagerOwner!;var runtime=native.NativeBehaviorRuntime!;
        var m=runtime.Manipulation!;var world=rig.Robot.Vision.World;
        var strategy=Assert.IsType<ObjectPositionUpdatedStrategy>(manager.Reactions.Single(r=>r.Strategy.Trigger==ReactionTrigger.ObjectPositionUpdated).Strategy);
        strategy.LastImageTimestamp=()=>100;
        var previous=M10Support.RunnableBehaviour("object-binding-previous");manager.Add(previous);
        Assert.True(manager.StartAsync(previous.Id,0).GetAwaiter().GetResult());
        var publish=(Action<Cozmo.Robot.Vision.ObjectObservation>)typeof(Cozmo.Robot.Vision.BlockWorld)
            .GetField("ObjectObserved",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(world)!;
        var pose=new Cozmo.Robot.Vision.Pose3d(Cozmo.Robot.Vision.Mat3.Identity,new Cozmo.Robot.Vision.Vec3(200,0,22));
        void Observe(uint id)
        {
            var obj=new Cozmo.Robot.Vision.ObservableObject(id,ObjectType.Block_LIGHTCUBE1,Cozmo.Robot.Vision.CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1)){Pose=pose};
            publish(new Cozmo.Robot.Vision.ObjectObservation(obj,100,Array.Empty<Cozmo.Robot.Vision.MarkerType>(),pose,Cozmo.Robot.Vision.PoseState.Known,false,0));
        }
        m.Docking.Carrying.SetCarrying(7);Observe(7);
        Assert.False(strategy.ShouldReactToTarget(7));
        Assert.False(strategy.ShouldTriggerBehavior(new ReactionContext(runtime.Context,0),manager.Reactions.Single(r=>r.Strategy==strategy).Behavior));
        m.Docking.Carrying.ClearNativeCarryingFields(false);Observe(7);Assert.False(strategy.ShouldReactToTarget(7));
        native.DockingOwner!.TargetId=8;Observe(8);native.DockingOwner.TargetId=null;
        Observe(8);Assert.False(strategy.ShouldReactToTarget(8));
        Observe(9);Assert.True(strategy.ShouldReactToTarget(9));
        Assert.True(strategy.ShouldTriggerBehavior(new ReactionContext(runtime.Context,0),manager.Reactions.Single(r=>r.Strategy==strategy).Behavior));
    }

    [Fact]
    public void RebindingChangesLiveWhiteboardWithoutReplacingRegistrationsAndRetirementStaysRetired()
    {
        using var rig=new M4ControlTests.Rig(Resources());LoadAnimations(rig);rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var runtime=native.NativeBehaviorRuntime!;var manager=native.BehaviorManagerOwner!;
        Assert.Same(rig.Robot.Animations.DrivingTriggerMap,runtime.Context.Triggers);
        var before=manager.Reactions.ToArray();
        var reg=before.Single(r=>r.Strategy.Trigger==ReactionTrigger.NoPreDockPoses);
        using var other=ManipulationSystem.CreateStandalone(rig.Robot,rig.Robot.Vision,()=>rig.Engine.Timer.Seconds);
        manager.BindReactionDependencies(other,runtime.Bound);
        other.Whiteboard.NoPreDockPosesObjectId=456;
        reg.Strategy.ShouldTriggerBehavior(new ReactionContext(runtime.Context,0),reg.Behavior);
        Assert.Null(other.Whiteboard.NoPreDockPosesObjectId);
        Assert.Equal(456u,Assert.IsType<RamIntoBlockBehavior>(reg.Behavior).PendingTarget);
        manager.BindReactionDependencies(runtime.Manipulation,runtime.Bound);
        Assert.Equal(before,manager.Reactions);
        OpenGate(native,manager);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=true,AutoStarted=true});
        rig.Tick(0);
        manager.CheckReactions(rig.Engine.Timer.Seconds);
        var motor=Assert.IsType<ReactToMotorCalibrationBehavior>(manager.Current);
        Assert.IsType<NativeTimedWaitRunner>(motor.OwnedRunner);
        manager.Dispose();
        Assert.False(motor.EngineRunning);Assert.Null(motor.OwnedRunner);
        manager.BindReactionDependencies(runtime.Manipulation,runtime.Bound);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_LIFT,CalibStarted=false});
        rig.Tick(0);
        Assert.Empty(manager.Reactions);Assert.Null(manager.CheckReactions(rig.Engine.Timer.Seconds));
    }

    [Fact]
    public void StandaloneFreeplayHasItsOwnBoundCalibrationAndDoesNotAlterCanonicalMap()
    {
        using var rig=new M4ControlTests.Rig(Resources());rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var runtime=native.NativeBehaviorRuntime!;var manager=native.BehaviorManagerOwner!;
        var before=manager.Reactions.ToArray();
        using var m=ManipulationSystem.CreateStandalone(rig.Robot,rig.Robot.Vision,()=>rig.Engine.Timer.Seconds);
        var context=new BehaviorContext{Robot=rig.Robot,Triggers=new AnimationTriggerMap()};
        string obbRoot=Directory.GetParent(Directory.GetParent(Resources())!.FullName)!.FullName;
        using var freeplay=FreeplayStack.CreateStandalone(obbRoot,rig.Robot,context,()=>rig.Engine.Timer.Seconds,rig.Robot.Vision,m);
        Assert.NotSame(manager,freeplay.Manager);Assert.Equal(before,manager.Reactions);
        var motor=Assert.IsType<ReactToMotorCalibrationBehavior>(freeplay.Manager.Reactions.Single(r=>r.Strategy.Trigger==ReactionTrigger.MotorCalibration).Behavior);
        motor.StartAsync(context,new BehaviorScope(),default).GetAwaiter().GetResult();
        Assert.Equal(5f,Assert.IsType<NativeTimedWaitRunner>(motor.OwnedRunner).Duration);
        motor.Stop(BehaviorStopReason.Cancelled);
        Assert.Same(runtime,native.NativeBehaviorRuntime);Assert.Equal(before,manager.Reactions);
    }

    // Host callback safety: ReportMissing invokes listeners synchronously. These tests
    // use the actual loaded map and real owners; no native destructor claim is made.
    [Fact]
    public void RamFallbackDiagnosticCanRetireManagerWithoutResurrectingIt()
    {
        using var rig=new M4ControlTests.Rig(Resources());rig.ToSynced();rig.State();
        var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;
        var context=new BehaviorContext{Robot=rig.Robot,Triggers=new AnimationTriggerMap()};
        using var manager=new BehaviorManager(context,null,false);
        manager.InitializeRobotReactions(ReactionTriggerMap.Load(Resources()),rig.Robot.Vision);
        int calls=0;
        void OnMissing(string message)
        {
            if(!message.Contains("RamIntoBlock configured factory owner unavailable"))return;
            calls++;manager.Dispose();
        }
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=OnMissing;
        try
        {
            manager.BindReactionDependencies(runtime.Manipulation,new Dictionary<string,IBehavior>());
            Assert.Equal(1,calls);Assert.Empty(manager.Reactions);
            manager.BindReactionDependencies(runtime.Manipulation,new Dictionary<string,IBehavior>());
            Assert.Empty(manager.Reactions);
        }
        finally { SteppedBehavior.MissingReported-=OnMissing;SteppedBehavior.ResetMissingForTests(); }
    }

    [Fact]
    public void RamFallbackDiagnosticNestedBindingWinsWithoutDuplicateOrStaleOwner()
    {
        using var rig=new M4ControlTests.Rig(Resources());rig.ToSynced();rig.State();
        var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;
        using var other=ManipulationSystem.CreateStandalone(rig.Robot,rig.Robot.Vision,()=>rig.Engine.Timer.Seconds);
        var context=new BehaviorContext{Robot=rig.Robot,Triggers=new AnimationTriggerMap()};
        using var manager=new BehaviorManager(context,null,false);
        manager.InitializeRobotReactions(ReactionTriggerMap.Load(Resources()),rig.Robot.Vision);
        var before=manager.Reactions.ToArray();int calls=0;
        void OnMissing(string message)
        {
            if(!message.Contains("RamIntoBlock configured factory owner unavailable"))return;
            calls++;manager.BindReactionDependencies(other,new Dictionary<string,IBehavior>());
        }
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=OnMissing;
        try
        {
            manager.BindReactionDependencies(runtime.Manipulation,new Dictionary<string,IBehavior>());
            Assert.Equal(1,calls);
            var reg=Assert.Single(manager.Reactions.Where(r=>r.Strategy.Trigger==ReactionTrigger.NoPreDockPoses));
            Assert.All(before,r=>Assert.Contains(r,manager.Reactions));
            other.Whiteboard.NoPreDockPosesObjectId=789;
            runtime.Manipulation!.Whiteboard.NoPreDockPosesObjectId=790;
            reg.Strategy.ShouldTriggerBehavior(new ReactionContext(context,0),reg.Behavior);
            Assert.Null(other.Whiteboard.NoPreDockPosesObjectId);
            Assert.Equal(790u,runtime.Manipulation.Whiteboard.NoPreDockPosesObjectId);
            Assert.Equal(789u,Assert.IsType<RamIntoBlockBehavior>(reg.Behavior).PendingTarget);
        }
        finally { SteppedBehavior.MissingReported-=OnMissing;SteppedBehavior.ResetMissingForTests(); }
    }

}
