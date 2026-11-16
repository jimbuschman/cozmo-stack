using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PeekABooNativeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShippedSocializeObjectiveReceivesPeekResult(bool returns)
    {
        if (!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null) return;
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true, engineOptions: SpeechOptions());
        var manager = f.Robot.BehaviorManagerOwner!;
        var behavior = Assert.IsType<NativePeekABooBehavior>(f.Runtime.Bound["FPPeekABoo"]);
        Assert.Same(behavior.NativeScoredFields, f.Robot.NativeConfiguredScoreFields![behavior.Id]);
        f.Rig.Engine.Timer.UpdateTime(10_000_000_000);
        typeof(OffTreadsClassifier).GetProperty("Current")!.SetValue(f.Rig.Robot.Sensors.OffTreads, OffTreadsState.OnTreads);
        f.Robot.Progression!.SetUnlock(NativeUnlockNames.FromBytes("PounceOnMotionAction"u8.ToArray()), false);
        f.Robot.Progression.SetUnlock(37, true); // PeekABoo unlock required by both shipped configs.
        f.Rig.Engine.ContextRandom.WordSourceForTest = () => 0; // Select the shipped one-peek minimum.
        try
        {
            var activity = f.Runtime.FreeplayObserver.Freeplay.SubActivities.Single(a => a.Id == "Socialize");
            var owner = Assert.IsType<NativeActivityObjectiveOwner>(activity.ObjectiveOwner);
            activity.OnSelected(f.Rig.Engine.Timer.SecondsF);
            Assert.Equal(16u, Assert.Single(owner.Counts).Key);
            Assert.Equal(1u, owner.Counts[16]);
            var neutral = new ChooserDecision(null, "idle", Array.Empty<(string, double, string)>());
            var scan = Assert.IsType<NativeFindFacesSocialize>(owner.Desired(null, neutral, f.Runtime.Context).Behavior);
            Assert.Equal(1, owner.Phase);
            Assert.True(manager.StartAsync(scan.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            for (int i = 0; i < 16 && scan.SearchIterations == 0; ++i)
            {
                // Complete the simulated robot work at each native runner boundary.
                var action = Assert.IsAssignableFrom<ActionRunner>(scan.OwnedRunner);
                action.State = 0x03000018;
                f.Robot.ActionList.Update();
            }
            Assert.Equal(1u, scan.SearchIterations);
            Assert.Same(scan, owner.Desired(scan, neutral, f.Runtime.Context).Behavior);
            f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp = 100;
            SeeFace(f, 100, true);
            var interact = Assert.IsType<NativeInteractWithFaces>(owner.Desired(scan, neutral, f.Runtime.Context).Behavior);
            Assert.Equal(2, owner.Phase);
            Assert.Same(f.Runtime.Bound["InteractWithFaces"], interact);

            var faceObjectives = new List<uint>();
            using var faceReports = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true,
                p => { if (p is uint id) faceObjectives.Add(id); });
            var faceNeeds = new List<NeedsActionId>();
            f.Rig.Engine.Needs.NeedsStateSent += faceNeeds.Add;
            Assert.True(manager.StartAsync(interact.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            void CompleteInteractionAction()
            {
                Assert.NotEqual(0u, interact.CurrentRunnerTag);
                f.Rig.Engine.AnimationMessages.Deliver(0x5A, true,
                    new RobotCompletedAction(interact.CurrentRunnerTag, 0, 0, Array.Empty<uint>(), 0));
            }

            var verify = Assert.IsType<CompoundActionSequential>(interact.OwnedRunner);
            Assert.True(Assert.IsType<NativeLastFaceRunner>(Assert.Single(verify.Children)).RequireVerifiedFace);
            CompleteInteractionAction();
            Assert.IsType<CompoundActionParallel>(interact.OwnedRunner);
            Assert.Equal(3, interact.Phase);
            CompleteInteractionAction();
            Assert.Equal(4, interact.Phase);
            Assert.IsType<CompoundActionParallel>(interact.OwnedRunner);
            Assert.Equal(18f, interact.Deadline);
            f.Rig.Engine.Timer.UpdateTime(17_999_000_000);
            Assert.True(interact.Update(f.Runtime.Context, f.Rig.Engine.Timer.TimeStampMs));
            Assert.Empty(faceObjectives);
            f.Rig.Engine.Timer.UpdateTime(18_000_000_000);
            interact.Update(f.Runtime.Context, f.Rig.Engine.Timer.TimeStampMs);
            f.Rig.Engine.DrainAppDelivery();
            Assert.Equal(new[] { (uint)BehaviorObjective.InteractedWithFace }, faceObjectives);
            Assert.Contains(NeedsActionId.SeeFace, faceNeeds);
            Assert.Equal(3, owner.Phase);
            Assert.True(owner.InputAvailable);
            manager.Stop(BehaviorStopReason.Completed, f.Rig.Engine.Timer.SecondsF);

            Assert.Same(behavior, owner.Desired(null, neutral, f.Runtime.Context).Behavior);
            Assert.Equal(4, owner.Phase);
            var objectives = new List<uint>();
            var needs = new List<NeedsActionId>();
            using var reports = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true,
                p => { if (p is uint id) objectives.Add(id); });
            f.Rig.Engine.Needs.NeedsStateSent += needs.Add;
            Assert.True(manager.StartAsync(behavior.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());

            void Update() => Assert.True(behavior.Update(f.Runtime.Context, f.Rig.Engine.Timer.TimeStampMs));
            void Complete(uint result = 0)
            {
                Assert.NotEqual(0u, behavior.CurrentRunnerTag);
                f.Rig.Engine.AnimationMessages.Deliver(0x5A, true,
                    new RobotCompletedAction(behavior.CurrentRunnerTag, 0, result, Array.Empty<uint>(), 0));
            }
            void Animation(IActionRunner? runner, int trigger) =>
                Assert.Equal((AnimationTrigger)trigger, Assert.IsAssignableFrom<TriggerAnimationAction>(runner).Request.Trigger);

            Animation(behavior.OwnedRunner, 0x177);
            Complete();
            Assert.False(Assert.IsType<NativeLastFaceRunner>(behavior.OwnedRunner).RequireVerifiedFace);
            Complete();
            var request = Assert.IsType<CompoundActionSequential>(behavior.OwnedRunner);
            Animation(request.Children[0], 0x17C);
            Assert.IsType<NativeLastFaceRunner>(request.Children[1]);
            Complete();
            Assert.Equal("WaitingToHideFace", behavior.StateName);
            Complete();
            var parallel = Assert.IsType<CompoundActionParallel>(behavior.OwnedRunner);
            Assert.IsType<NativeTrackFaceRunner>(parallel.Children[0]);
            var idle = Assert.IsType<CompoundActionSequential>(parallel.Children[1]);
            Assert.Equal(5, idle.Children.Count);
            Assert.Equal(new[] { 0x17B, 0x17F, 0x17B, 0x17F, 0x17B },
                idle.Children.Select(a => (int)Assert.IsAssignableFrom<TriggerAnimationAction>(a).Request.Trigger));
            Assert.Equal(2, Assert.IsAssignableFrom<TriggerAnimationAction>(idle.Children[0]).Request.Argument4);
            Assert.Equal(1, Assert.IsAssignableFrom<TriggerAnimationAction>(idle.Children[4]).Request.Argument4);

            if (returns)
            {
                for (uint when = 101; when <= 106; when++)
                {
                    f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp = when;
                    SeeFace(f, when, false); // Eye centres remain supplied, matching an empty contour path.
                    Update();
                }
                Assert.Equal("WaitingToHideFace", behavior.StateName);
                Update(); // A repeat tick for the same image never increments the count.
                Assert.Equal("WaitingToHideFace", behavior.StateName);
                f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp = 107;
                Update();
                Assert.Equal("WaitingToSeeFace", behavior.StateName);
                Assert.True(parallel.DestructionObserved);
                Complete();
                var waiting = Assert.IsType<CompoundActionSequential>(behavior.OwnedRunner);
                Assert.Equal(3, Assert.IsAssignableFrom<TriggerAnimationAction>(waiting.Children[0]).Request.Argument4);
                f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp = 108;
                SeeFace(f, 108, true);
                Update();
                Animation(behavior.OwnedRunner, 0x178);
                Assert.Equal(new uint[] { 16, 17 }, objectives);
            }
            else
            {
                Complete();
                Animation(Assert.IsType<TriggerAnimationAction>(behavior.OwnedRunner), 0x17E);
                Complete();
                Complete();
                Animation(Assert.IsType<CompoundActionSequential>(behavior.OwnedRunner).Children[0], 0x17F);
                Complete();
                Complete();
                Complete();
                Animation(behavior.OwnedRunner, 0x179);
                Assert.Equal(new uint[] { 16 }, objectives);
            }

            f.Rig.Engine.DrainAppDelivery();
            Assert.Equal(returns, needs.Contains(NeedsActionId.PeekAboo));
            Assert.Empty(owner.Counts);
            Assert.Equal(5, owner.Phase);
            Complete();
            Assert.Equal(2, behavior.UpdateStatus());
            float stop = f.Rig.Engine.Timer.SecondsF;
            manager.Stop(BehaviorStopReason.Completed, stop);
            Assert.False(behavior.EvaluateHostReadiness(f.Runtime.Context).Runnable);
        }
        finally
        {
            manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
            f.Rig.Engine.ContextRandom.WordSourceForTest = null;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShippedSocializeSelectsPounceOnlyWithTheUnlock(bool unlocked)
    {
        if (!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null) return;
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true, engineOptions: SpeechOptions());
        var engine = f.Rig.Engine;
        var manager = f.Robot.BehaviorManagerOwner!;
        var behavior = Assert.IsType<NativePounceOnMotion>(f.Runtime.Bound["PounceOnMotion_Socialize"]);
        uint unlockId = NativeUnlockNames.FromBytes("PounceOnMotionAction"u8.ToArray());
        f.Robot.Progression!.SetUnlock(unlockId, unlocked);
        f.Robot.Progression.SetUnlock(37, false); // PeekABoo
        f.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos, origin: f.Robot.CurrentOriginId, liftAngle: -0.2f);
        engine.Timer.UpdateTime(10_000_000_000);
        typeof(OffTreadsClassifier).GetProperty("Current")!.SetValue(f.Rig.Robot.Sensors.OffTreads, OffTreadsState.OnTreads);
        f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp = 100;
        SeeFace(f, 100, true);
        engine.ContextRandom.WordSourceForTest = () => 0; // Require the optional objective, with its minimum count.
        try
        {
            var activity = f.Runtime.FreeplayObserver.Freeplay.SubActivities.Single(a => a.Id == "Socialize");
            var owner = Assert.IsType<NativeActivityObjectiveOwner>(activity.ObjectiveOwner);
            activity.OnSelected(engine.Timer.SecondsF);
            uint objective = (uint)BehaviorObjective.PouncedAndCaught;
            if (unlocked) Assert.Equal((objective, 1u), (Assert.Single(owner.Counts).Key, owner.Counts[objective]));
            else Assert.Empty(owner.Counts);

            var idle = new ChooserDecision(null, "idle", Array.Empty<(string, double, string)>());
            var interact = Assert.IsType<NativeInteractWithFaces>(owner.Desired(null, idle, f.Runtime.Context).Behavior);
            Assert.Equal(2, owner.Phase);
            Assert.True(manager.StartAsync(interact.Id, engine.Timer.SecondsF).GetAwaiter().GetResult());
            for (int step = 0; step < 2; step++)
            {
                Assert.NotEqual(0u, interact.CurrentRunnerTag);
                engine.AnimationMessages.Deliver(0x5A, true,
                    new RobotCompletedAction(interact.CurrentRunnerTag, 0, 0, Array.Empty<uint>(), 0));
            }
            Assert.Equal(4, interact.Phase);
            engine.Timer.UpdateTime(18_000_000_000);
            interact.Update(f.Runtime.Context, engine.Timer.TimeStampMs);
            Assert.Equal(3, owner.Phase);
            manager.Stop(BehaviorStopReason.Completed, engine.Timer.SecondsF);

            var choice = owner.Desired(null, idle, f.Runtime.Context);
            Assert.False(choice.InputUnavailable);
            if (!unlocked)
            {
                Assert.Null(choice.Behavior);
                Assert.Equal(6, owner.Phase);
                return;
            }
            Assert.Same(behavior, choice.Behavior);
            Assert.Equal(4, owner.Phase);
            Assert.Equal(3f, behavior.NotRunningBoredSeconds);
            Assert.True(manager.StartAsync(behavior.Id, engine.Timer.SecondsF).GetAwaiter().GetResult());
            var start = Assert.IsType<CompoundActionSequential>(behavior.OwnedRunner);
            Assert.Equal((AnimationTrigger)0x196, Assert.IsType<TriggerLiftSafeAnimationAction>(Assert.Single(start.Children)).Request.Trigger);

            void FinishAction()
            {
                var runner = Assert.IsAssignableFrom<ActionRunner>(behavior.OwnedRunner);
                runner.State = 0x03000018; // Simulated terminal action result.
                f.Robot.ActionList.Update();
            }
            FinishAction(); // Initial pounce -> relax lift.
            FinishAction(); // Relax lift -> initial search.
            Assert.Equal(3, behavior.NativeState);
            FinishAction(); // Initial search -> wait for motion.
            Assert.Equal(6, behavior.NativeState);
            FinishAction(); // First eligible rotate. The 0.0 draw takes the 0.35 turn-pounce branch.
            var turnPounce = Assert.IsType<CompoundActionSequential>(behavior.OwnedRunner);
            Assert.IsType<NativePanTiltRunner>(turnPounce.Children[0]);
            Assert.Equal((AnimationTrigger)0x196, Assert.IsType<TriggerLiftSafeAnimationAction>(turnPounce.Children[1]).Request.Trigger);
            FinishAction();
            FinishAction();
            engine.Timer.UpdateTime(24_000_000_000);
            FinishAction();
            Assert.Equal(6, behavior.NativeState); // Strict six-second rotation threshold.
            engine.Timer.UpdateTime(24_001_000_000);
            FinishAction();
            Assert.Equal(5, behavior.NativeState);
            FinishAction();
            FinishAction();
            engine.Timer.UpdateTime(38_001_000_000);
            FinishAction();
            Assert.Equal(13, behavior.NativeState); // 20 seconds since the start with no ground motion.
            Assert.Equal((AnimationTrigger)0x194, Assert.IsType<TriggerLiftSafeAnimationAction>(behavior.OwnedRunner).Request.Trigger);
        }
        finally
        {
            manager.Stop(BehaviorStopReason.Interrupted, engine.Timer.SecondsF);
            engine.ContextRandom.WordSourceForTest = null;
        }
    }

    [Fact]
    public void ShippedHardSparkUsesStrictDeadlineAndQueuesItsTimeoutAnimationWithoutAFace()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        var behavior = Assert.IsType<NativePeekABooBehavior>(f.Runtime.Bound["SparksPeekABoo"]);
        var activity = NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a => a.Id == "SparksPeekABoo");
        var owner = Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);
        typeof(OffTreadsClassifier).GetProperty("Current")!.SetValue(f.Rig.Robot.Sensors.OffTreads, OffTreadsState.OnTreads);
        f.Robot.Progression!.SetUnlock(37, true);
        f.Rig.Engine.Timer.UpdateTime(10_000_000_000);
        manager.SetNativeRequestedSpark(37, 0);
        manager.SwitchToNativeRequestedSpark();
        owner.Selected();
        Assert.Equal(38f, behavior.NativeScoredFields.SparkPeekDeadline);
        f.Rig.Engine.Timer.UpdateTime(38_000_000_000);
        Assert.False(behavior.EvaluateHostReadiness(f.Runtime.Context).Runnable);
        f.Rig.Engine.Timer.UpdateTime(38_010_000_000);
        Assert.True(behavior.EvaluateHostReadiness(f.Runtime.Context).Runnable);
        Assert.Equal(-1f, behavior.NativeScoredFields.SparkPeekDeadline);
        Assert.True(manager.StartAsync(behavior.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var timeout = Assert.IsType<TriggerAnimationAction>(behavior.OwnedRunner);
        Assert.Equal((AnimationTrigger)0x210, timeout.Request.Trigger);
        Assert.Equal(0f, behavior.NativeScoredFields.SparkPeekDeadline);
        manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
    }

    private static CozmoEngineOptions SpeechOptions() => new()
    {
        ResourcesPath = PickupPyramidNativeExecutionTests.Resources(),
        SpeechSoundBankDirectory = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing"),
        SpeechAudioArchivePath = Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"), "AudioAssets.zip"),
        LabLocaleLanguage = "en",
        BlockPoolPath = ""
    };

    private static void SeeFace(PickupPyramidNativeExecutionTests.Fixture f, uint time, bool contour)
    {
        var features = NativeFaceFeatures.Empty;
        if (contour) features = features with { LeftEye = Array.AsReadOnly(new[] { new Vec2(1, 1) }) };
        var detection = new DetectedFace(7, new FaceRect(0, 0, 10, 10), new Vec2(1, 1), new Vec2(2, 1))
            { NativeFeatures = features };
        var position = new Vec3(150, 0, 40);
        var tracked = new TrackedFace(detection, time)
        {
            HeadPose = new Pose3d(Rotation3d.Identity, position),
            NativeTrackingTranslation = position,
            NativeTrackingRotation = Rotation3d.Identity
        };
        var origin = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        Assert.NotNull(f.M.Vision.Faces.AddOrUpdateNativeFace(tracked, f.Robot.GetCheckedMainPose(),
            false, origin.Node!, true, requireNativePose: true, expectedOwner: f.Robot));
    }
}
