using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class MeetCozmoAdmissionTests
{
    private sealed class GateAction : ActionRunner
    {
        internal GateAction() : base(8, 0) { }
        protected override string ActionName => "reaction gate fixture";
        public override bool CanInterrupt() => false;
        public override uint CheckIfDone() => 0;
    }
    private sealed class FaceDetector : IFaceDetector
    {
        public bool IsAvailable => true;
        public string Description => "supplied current camera face";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp) =>
            new[] { new DetectedFace(-7, new FaceRect(100, 70, 90, 90), new Vec2(120, 110), new Vec2(160, 110), RollRad: 0) { NativeNewEntryRotatingTooFast = false } };
    }
    [Fact]
    public void PublicMeetSelectsDistinctConfiguredScannerAndDuplicateKeepsRun()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        var meet = Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["MeetCozmo_FindFaces_Socialize"]);
        var freeplay = Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        Assert.NotSame(meet, freeplay);
        int mark = f.Rig.Mark();
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
        f.Rig.Tick();
        Assert.Equal("MeetCozmo", f.Runtime.Observe().Activity);
        Assert.Equal((byte)6, f.Robot.BehaviorPublicState.State.Activity);
        Assert.Contains(FreeplayPauseFlag.GameControl, f.Runtime.Tracker.PausedFlags);
        Assert.Same(meet, manager.Current);
        Assert.NotNull(meet.OwnedRunner);
        Assert.Contains(f.Rig.SentSince(mark), message => message is SetLiftHeight);
        float stamp = manager.ParentSwitchTime;
        uint tag = meet.CurrentRunnerTag;
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
        f.Rig.Tick();
        Assert.Equal(stamp, manager.ParentSwitchTime);
        Assert.Equal(tag, meet.CurrentRunnerTag);
        Assert.Equal(0x55, manager.ActiveSparkId);
        Assert.Equal(0x55, manager.RequestedSparkId);
        f.Rig.Robot.ActivateHighLevelActivity("Selection");
        f.Rig.Tick();
        Assert.Equal("Selection", f.Runtime.Observe().Activity);
        Assert.False(meet.EngineRunning);
    }

    [Fact]
    public void OnChargerSelectsRealDriveOffBeforeMeetScanner()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        f.Rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger | RobotStatusFlag.HeadInPos);
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
        f.Rig.Tick();
        Assert.True(f.Rig.Robot.Sensors.OnChargerPlatform);
        Assert.Equal("MeetCozmo", f.Runtime.Observe().Activity);
        Assert.IsType<DriveOffChargerBehavior>(f.Robot.BehaviorManagerOwner!.Current);
        Assert.False(((NativeFindFacesSocialize)f.Runtime.Bound["MeetCozmo_FindFaces_Socialize"]).EngineRunning);
    }

    [Fact]
    public void PositiveInteractLosesToRunningDriveOffButStillConsumesItsDraw()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        f.Rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger | RobotStatusFlag.HeadInPos, head: .1f);
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces"); f.Rig.Tick();
        var drive = Assert.IsType<DriveOffChargerBehavior>(f.Robot.BehaviorManagerOwner!.Current);
        f.Head = .1f;
        f.Rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, origin: f.Robot.CurrentOriginId, head: f.Head);
        f.Rig.Robot.Sensors.SetOnChargerPlatform(false); // native platform clear after contacts leave
        Assert.False(f.Rig.Robot.Sensors.OnChargerPlatform);
        Assert.Same(drive, f.Robot.BehaviorManagerOwner.Current);
        f.M.Vision.FaceDetector = new FaceDetector();
        f.M.Vision.Enabled = true; f.M.Vision.ModeEnableMask = 1 << 2;
        f.M.Vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        f.Image(902, modes: 1 << 2);
        Assert.NotNull(f.M.Vision.Faces.GetNativeFace(f.Robot, -7));
        Assert.Same(drive, f.Robot.BehaviorManagerOwner.Current);
        var chooser = Assert.IsType<ScoringChooser>(f.Runtime.Tree.Single(a => a.Id == "MeetCozmo").Chooser);
        f.Rig.Engine.ContextRandom.SetSeed(71, () => 71);
        var reference = new EngineRandom(); reference.SetSeed(71, () => 71);
        var decision = chooser.GetDesiredActiveBehavior(drive, 0, f.Runtime.Context, f.Rig.Engine.Timer.SecondsF);
        Assert.Same(drive, decision.Behavior);
        Assert.Null(decision.UnbuiltWinner);
        Assert.Contains(decision.Scores, score => score.Id == "MeetCozmo_InteractWithFaces" && score.Score > 1);
        _ = reference.GetNextDbl(); // nonrunning scanner
        _ = reference.GetNextDbl(); // nonrunning Interact, although it loses
        Assert.Equal(reference.GetNextDbl(), f.Rig.Engine.ContextRandom.GetNextDbl());
    }

    [Fact]
    public void CarryingSelectsConfiguredPutDownWhenSearchIsIneligible()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var cube = f.Add();
        using var relative = new Pose3d("");
        f.M.Docking.Carrying.AttachToLift(7, cube.Markers[0], relative, null, null);
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces"); f.Rig.Tick();
        Assert.Same(f.Runtime.Bound["PutDownBlock"], f.Robot.BehaviorManagerOwner!.Current);
        Assert.False(((NativeFindFacesSocialize)f.Runtime.Bound["MeetCozmo_FindFaces_Socialize"]).EngineRunning);
    }

    [Fact]
    public void MeetSelectionPreservesAnAlreadyRunningReaction()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        f.Robot.ActionList.Clear();
        Assert.Equal(0, f.Robot.ActionList.QueueAction(QueueActionPosition.Now, new GateAction(), 0));
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        f.Robot.ActionList.Clear();
        f.Rig.Data(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true, AutoStarted = true });
        f.Rig.Tick(); manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        var reaction = Assert.IsType<ReactToMotorCalibrationBehavior>(manager.Current);
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces"); f.Rig.Tick();
        Assert.Same(reaction, manager.Current);
        Assert.Equal(ReactionTrigger.MotorCalibration, manager.CurrentReactionTrigger);
        Assert.Equal("MeetCozmo", f.Runtime.Observe().Activity);
        Assert.False(((NativeFindFacesSocialize)f.Runtime.Bound["MeetCozmo_FindFaces_Socialize"]).EngineRunning);
    }

    [Fact]
    public void QueuedMeetRequestCannotSelectAReplacementRobot()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var old = f.Robot;
        using (f.Rig.Engine.EnterSynchronousOwner())
        {
            f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
            f.Rig.Engine.Robots.RemoveRobot(1, false);
            f.Rig.Engine.Robots.AddRobot(1, withRic: false);
        }
        var replacement = f.Rig.Engine.Robot!;
        Assert.NotSame(old, replacement);
        f.Rig.Tick();
        Assert.NotEqual("MeetCozmo", replacement.NativeBehaviorRuntime?.Observe().Activity);
        Assert.NotEqual((byte)6, replacement.BehaviorPublicState.State.Activity);
    }

    [Fact]
    public void CurrentOriginFaceStopsScannerAtUnavailableInteractWithoutReroll()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        f.Head = .1f;
        f.Rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, origin: f.Robot.CurrentOriginId, head: f.Head);
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces"); f.Rig.Tick();
        var manager = f.Robot.BehaviorManagerOwner!;
        var scanner = Assert.IsType<NativeFindFacesSocialize>(manager.Current);
        Assert.Equal("MeetCozmo_FindFaces_Socialize", scanner.Id);
        var missing = new List<string>();
        var runningAtFrontier = new List<bool>();
        void Capture(string text) { if (text.Contains("MEET-COZMO-ADMISSION-1")) { missing.Add(text); runningAtFrontier.Add(scanner.EngineRunning); } }
        SteppedBehavior.MissingReported += Capture;
        try
        {
            f.M.Vision.FaceDetector = new FaceDetector();
            f.M.Vision.Enabled = true;
            f.M.Vision.ModeEnableMask = 1 << 2;
            f.M.Vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
            f.Image(901, modes: 1 << 2);
            Assert.NotNull(f.M.Vision.Faces.GetNativeFace(f.Robot, -7));
            for (int i = 0; i < 4 && missing.Count == 0; i++) f.Rig.Tick();
            Assert.Single(missing);
            Assert.Contains("won the native score", missing[0]);
            Assert.Equal(new[] { false }, runningAtFrontier);
            Assert.False(scanner.EngineRunning);
            Assert.Null(manager.Current);
            f.Rig.Engine.ContextRandom.SetSeed(29, () => 29);
            var reference = new EngineRandom(); reference.SetSeed(29, () => 29);
            for (int i = 0; i < 3; i++) manager.Activity!.GetDesiredActiveBehavior(null, f.Rig.Engine.Timer.SecondsF);
            Assert.Equal(reference.GetNextDbl(), f.Rig.Engine.ContextRandom.GetNextDbl());
            Assert.Single(missing);
        }
        finally { SteppedBehavior.MissingReported -= Capture; }
    }
}
