using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class TrackLaserOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShippedSparkOwnsExposureChangeAndOnlyReportsAnAchievedObjective(bool observeLaser)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        var behavior = Assert.IsType<NativeTrackLaserBehavior>(f.Runtime.Bound["SparksTrackLaser"]);
        var activity = NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a => a.Id == behavior.Id);
        var owner = Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);
        var timer = f.Rig.Engine.Timer;
        var camera = f.Rig.Robot.CameraSettings;
        var before = camera.QualityState;
        var originalSchedule = f.M.Vision.Schedules;

        typeof(OffTreadsClassifier).GetProperty("Current")!.SetValue(f.Rig.Robot.Sensors.OffTreads, OffTreadsState.OnTreads);
        f.Robot.Progression!.SetUnlock(64, true);
        timer.UpdateTime(10_000_000_000);
        manager.SetNativeRequestedSpark(64, 0);
        manager.SwitchToNativeRequestedSpark();
        owner.Selected();
        Assert.NotSame(behavior, owner.Desired(null, timer.SecondsF, f.Runtime.Context)); // Shipped spark get-in.
        var desired = owner.Desired(null, timer.SecondsF, f.Runtime.Context);
        Assert.Same(behavior, desired);
        Assert.False(owner.InputUnavailable);
        var needs = new List<NeedsActionId>();
        f.Rig.Engine.Needs.NeedsStateSent += needs.Add;
        var objectives = new List<uint>();
        using var subscription = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true, p =>
        { if (p is uint objective) objectives.Add(objective); });
        int wireMark = f.Rig.SentSince(0).Count();
        Assert.True(manager.StartAsync(behavior.Id, timer.SecondsF).GetAwaiter().GetResult());
        Assert.False(camera.AutoExposureEnabled);
        Assert.True(camera.ColorImagesEnabled);
        Assert.Equal("WaitingForExposureChange", behavior.StateName);
        var exposure = Assert.IsType<CompoundActionParallel>(behavior.OwnedRunner);
        Assert.Equal(5, Assert.IsType<NativeWaitForImagesRunner>(exposure.Children[0]).RequiredFrames);
        Assert.Equal((AnimationTrigger)0x223, Assert.IsType<TriggerAnimationAction>(exposure.Children[1]).Request.Trigger);

        void Image(uint stamp, byte mean)
        {
            camera.LastProcessedImageTimestamp = stamp;
            f.Rig.Engine.AnimationMessages.Deliver(0x43, true, new ProcessedImageReport(stamp, new[] { 8u }, mean));
        }
        void Laser(uint stamp) => f.Rig.Engine.AnimationMessages.Deliver(NativeLaserObservation.Tag, true,
            new NativeLaserObservation(stamp, 1, 120, -10));
        void Update() => Assert.True(behavior.Update(f.Runtime.Context, timer.TimeStampMs));
        void Complete() => f.Rig.Engine.AnimationMessages.Deliver(0x5A, true,
            new RobotCompletedAction(behavior.CurrentRunnerTag, 0, 0, Array.Empty<uint>(), 0));

        void ProcessFrame(uint stamp)
        {
            var image = new GrayImage(320, 240);
            image.Fill(100);
            var poseData = new VisionPoseData(stamp, f.Robot.GetCheckedMainPose(), 0, 0, false, false);
            f.M.Vision.ProcessImage(image, stamp, stamp, poseData);
        }
        var missing = new List<string>();
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += missing.Add;
        try { ProcessFrame(99); }
        finally { SteppedBehavior.MissingReported -= missing.Add; }
        Assert.Equal(new[] { 8, 15 }, Enumerable.Range(0, AllVisionModesSchedule.ModeCount)
            .Where(i => f.M.Vision.Schedules![i].Frames.Single()));
        Assert.Contains(missing, text => text.Contains("M11-035 DetectingLaserPoints producer/handler unavailable"));

        Image(100, 100);
        Image(101, 50);
        Update();
        Assert.Equal("WaitingForExposureChange", behavior.StateName); // Native cutoff is strict.
        if (observeLaser) Laser(102);
        Image(102, 49);
        Update();
        Assert.True(exposure.DestructionObserved);
        if (observeLaser)
        {
            Assert.Equal("RespondToLaser", behavior.StateName);
            var response = Assert.IsType<CompoundActionSequential>(behavior.OwnedRunner);
            var turn = Assert.IsType<NativeTurnTowardsPoseRunner>(response.Children[0]);
            Assert.Equal(new Vec3(120, -10, 0), turn.GoalPose.Translation);
            Assert.Equal((AnimationTrigger)0x100, Assert.IsType<TriggerAnimationAction>(response.Children[1]).Request.Trigger);
            Complete(); // Deliver the source compound-completion signal to the retained behavior owner.
            Assert.Equal("TrackLaser", behavior.StateName);
            Assert.IsType<NativeTrackGroundPointRunner>(behavior.OwnedRunner);
            Laser(103);
            Update();
            timer.UpdateTime(12_500_000_000);
            Update();
            Assert.Empty(objectives);
            timer.UpdateTime(12_510_000_000);
            Update();
        }
        else
        {
            Assert.Equal("WaitingForLaser", behavior.StateName);
            Image(5102, 49);
            Update();
            Assert.Equal("WaitingForLaser", behavior.StateName);
            Image(5103, 49);
            Assert.False(behavior.Update(f.Runtime.Context, timer.TimeStampMs));
        }

        manager.Stop(BehaviorStopReason.Completed, timer.SecondsF);
        ProcessFrame(5200);
        Assert.Same(originalSchedule, f.M.Vision.Schedules);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(observeLaser ? new[] { 15u } : Array.Empty<uint>(), objectives);
        Assert.Equal(observeLaser ? 1u : 0u, owner.Count);
        Assert.Equal(observeLaser, needs.Contains(NeedsActionId.Laser_Sparked));
        Assert.True(camera.AutoExposureEnabled);
        Assert.False(camera.ColorImagesEnabled);
        var sent = f.Rig.SentSince(wireMark).ToArray();
        var settings = sent.OfType<SetCameraParams>().ToArray();
        Assert.NotEmpty(settings);
        Assert.Equal((ushort)1, settings[0].ExposureMs);
        Assert.Equal(0.1f, settings[0].Gain);
        Assert.Equal(unchecked((ushort)before.Exposure), settings[^1].ExposureMs);
        Assert.Equal(before.Gain, settings[^1].Gain);
        Assert.Equal(new[] { true, false }, sent.OfType<EnableColorImages>().Select(m => m.Enable));
    }
}
