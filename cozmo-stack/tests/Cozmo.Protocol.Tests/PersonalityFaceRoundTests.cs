using System.Net;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PersonalityFaceRoundTests
{
    private sealed class FaceDetector(string? name) : IFaceDetector
    {
        public bool IsAvailable => true;
        public string Description => "supplied native detector observation";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp) =>
            [new DetectedFace(7, new FaceRect(100, 70, 90, 90), new Vec2(120, 110), new Vec2(160, 110),
                Name: name, RollRad: 0) { NativeNewEntryRotatingTooFast = false }];
    }

    private sealed class PetDetector : IPetDetector
    {
        public bool IsAvailable => true;
        public bool ProvidesNativeResults => true;
        public IReadOnlyList<DetectedPet> Detect(GrayImage image, uint timestamp) =>
            [new DetectedPet(8, PetType.Dog, new FaceRect(100, 70, 60, 60)) { TrackerResultFlag = true }];
    }

    private sealed class ReactionGate() : ActionRunner(8, 0)
    {
        protected override string ActionName => "reaction-window idle action";
        public override bool CanInterrupt() => true;
        public override uint CheckIfDone() => EngineActionResult.Running;
    }

    private static void ShowFace(Fixture f, uint imageId, string? name)
    {
        var vision = f.M.Vision;
        vision.FaceDetector = new FaceDetector(name);
        vision.ModeEnableMask = 1 << 2;
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        f.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos, origin: f.Robot.CurrentOriginId, head: .1f);
        uint timestamp = f.Robot.StoredState!.Timestamp;
        f.Rig.Data(new ImageImuData { ImageId = imageId, Line2Number = 0 });
        f.Rig.Tick();
        f.Rig.CameraGrayFrame(imageId, timestamp, Enumerable.Repeat((byte)150, 320 * 240).ToArray());
        vision.WaitForProcessorIdle();
        f.Rig.Engine.VisionUpdateAllResultsFailed!();
        Assert.NotNull(vision.Faces.GetNativeFace(f.Robot, 7));
    }

    private static M15CubeRun StartWindow(Fixture f)
    {
        var run = new M15CubeRun("", "", IPAddress.Loopback, "", [], [], false, false) { Personality = true };
        run.SubscribeBehaviorReports(f.Rig.Engine);
        return run;
    }

    private static void AssertStartAndStop(M15CubeRun run, BehaviorID id)
    {
        var transitions = run.BehaviorTransitions(id, 0, double.MaxValue);
        Assert.Collection(transitions,
            start => Assert.True(PersonalityHw.IsBehaviorStart(start, id)),
            stop => Assert.True(PersonalityHw.IsBehaviorStop(stop, id)));
    }

    [Fact]
    public void ANewNamedFaceProducesAcknowledgeStartAndStopInTheHarness()
    {
        using var f = new Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var run = StartWindow(f);
        ShowFace(f, 901, "Ada");
        var manager = f.Robot.BehaviorManagerOwner!;
        if (manager.Current?.Id != "AcknowledgeFace")
        {
            f.Robot.ActionList.Clear();
            Assert.Equal(0, f.Robot.ActionList.QueueAction(QueueActionPosition.Now, new ReactionGate(), 0));
            manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        }
        Assert.Equal("AcknowledgeFace", manager.Current?.Id);
        manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
        f.Rig.Engine.DrainAppDelivery();
        AssertStartAndStop(run, BehaviorID.AcknowledgeFace);
    }

    [Fact]
    public async Task SocializeSelectsTheVisibleFaceAndReportsInteractWithFaces()
    {
        var options = new CozmoEngineOptions
        {
            ResourcesPath = PickupPyramidNativeExecutionTests.Resources(),
            SpeechSoundBankDirectory = GraphRig.FindMeta(),
            LabLocaleLanguage = "en",
            BlockPoolPath = "",
        };
        using var f = new Fixture(preloadAnimations: true, sourceNvEmpty: true, engineOptions: options);
        var run = StartWindow(f);
        ShowFace(f, 902, null);
        var manager = f.Robot.BehaviorManagerOwner!;
        var activity = f.Runtime.FreeplayObserver.Freeplay.SubActivities.Single(a => a.Id == "Socialize");
        activity.OnSelected(f.Rig.Engine.Timer.SecondsF);
        var chooser = Assert.IsType<NativeActivityObjectiveOwner>(activity.ObjectiveOwner);
        var neutral = new ChooserDecision(null, "idle", Array.Empty<(string, double, string)>());
        var selected = Assert.IsType<NativeInteractWithFaces>(chooser.Desired(null, neutral, f.Runtime.Context).Behavior);
        Assert.True(await manager.StartAsync(selected.Id, f.Rig.Engine.Timer.SecondsF));
        manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
        f.Rig.Engine.DrainAppDelivery();
        AssertStartAndStop(run, BehaviorID.InteractWithFaces);
    }

    [Fact]
    public void ThreePetObservationsProduceReactToPetStartAndStopInTheHarness()
    {
        using var f = new Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var run = StartWindow(f);
        f.M.Vision.PetDetector = new PetDetector();
        f.Rig.Robot.SetVisionWhileMoving(true);
        f.Rig.Tick();
        for (uint id = 903; id < 906; id++) f.Image(id, modes: 1 << 9);
        Assert.Equal(3, Assert.Single(f.M.Vision.Pets.Pets).TimesObserved);
        var manager = f.Robot.BehaviorManagerOwner!;
        if (manager.Current?.Id != "ReactToPet")
        {
            f.Robot.ActionList.Clear();
            Assert.Equal(0, f.Robot.ActionList.QueueAction(QueueActionPosition.Now, new ReactionGate(), 0));
            manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        }
        Assert.Equal("ReactToPet", manager.Current?.Id);
        manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
        f.Rig.Engine.DrainAppDelivery();
        AssertStartAndStop(run, BehaviorID.ReactToPet);
    }

    [Theory]
    [InlineData("search-for-face", BehaviorID.VC_SearchForFace)]
    [InlineData("drive-to-face", BehaviorID.VC_ComeHere)]
    public void ConfiguredVoiceCommandGapsCannotProduceAStartVerdict(string stepId, BehaviorID id)
    {
        using var f = new Fixture(sourceNvEmpty: true);
        var check = Assert.Single(PersonalityHw.SelectSteps(stepId)).Check!;
        Assert.Equal(id, check.Id);
        Assert.NotNull(check.Missing);
        Assert.True(f.Rig.Engine.HostBehaviorMap!.ContainsKey(id));
        Assert.False(f.Runtime.Bound.ContainsKey(id.ToString()));
        var run = StartWindow(f);
        f.Rig.Robot.ExecuteBehaviorById((byte)id, 1);
        f.Rig.Tick();
        f.Rig.Engine.DrainAppDelivery();
        Assert.Empty(run.BehaviorTransitions(id, 0, double.MaxValue));
    }
}
