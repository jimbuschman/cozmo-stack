using System.Reflection;
using Cozmo.Robot;
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
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
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
            owner.OnSelectedInternal();
            Assert.Equal(16u, Assert.Single(owner.Counts).Key);
            Assert.Equal(1u, owner.Counts[16]);
            // Start at the interaction phase and send the checked 0x8B objective that admits the special behavior.
            typeof(NativeActivityObjectiveOwner).GetField("_phase", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, 2);
            f.Rig.Engine.AnimationMessages.Deliver(0x8B, true, 12u);
            Assert.Equal(3, owner.Phase);

            f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp = 100;
            SeeFace(f, 100, true);
            var ready = behavior.EvaluateHostReadiness(f.Runtime.Context);
            Assert.True(ready.Available && ready.Runnable,
                $"readiness={ready}, base={behavior.IsRunnableBase(f.Runtime.Context)}, clock={f.Rig.Engine.Timer.SecondsF}, faces={string.Join(",", f.M.Vision.Faces.GetNativeFaceIds(f.Robot))}");
            var neutral = new ChooserDecision(null, "idle", Array.Empty<(string, double, string)>());
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
