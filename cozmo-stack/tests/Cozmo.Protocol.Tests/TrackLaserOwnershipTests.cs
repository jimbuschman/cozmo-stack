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
    [Fact]
    public async Task RenderedLaserReachesGroundTrackingAndPounceThroughTheVisionWorker()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var vision = f.M.Vision;
        var manager = f.Robot.BehaviorManagerOwner!;
        var behavior = Assert.IsType<NativeTrackLaserBehavior>(f.Runtime.Bound["SparksTrackLaser"]);
        var timer = f.Rig.Engine.Timer;
        vision.Enabled = true;
        vision.SetNativeOuterCalibration(vision.Calibration!);
        f.Head = CozmoMotion.MinHeadAngleRad;
        f.Motors();
        typeof(OffTreadsClassifier).GetProperty("Current")!.SetValue(f.Rig.Robot.Sensors.OffTreads, OffTreadsState.OnTreads);
        f.Robot.Progression!.SetUnlock(64, true);
        manager.SetNativeRequestedSpark(64, 0);
        f.Runtime.AdmitActivity(behavior.Id);
        manager.SwitchToNativeRequestedSpark();
        var activity = NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a => a.Id == behavior.Id);
        var owner = Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);
        Assert.NotSame(behavior, owner.Desired(null, timer.SecondsF, f.Runtime.Context));
        Assert.Same(behavior, owner.Desired(null, timer.SecondsF, f.Runtime.Context));
        Assert.True(await manager.StartAsync(behavior.Id, timer.SecondsF));

        float[]? projection = null;
        vision.NativeGroundFrameConsumedForTest = frame =>
        {
            if (frame.GroundValid) projection = frame.Homography;
        };
        var points = new List<NativeLaserObservation>();
        var deliveryOrder = new List<(ushort Tag, uint Timestamp)>();
        var completed = new List<ProcessedImageReport>();
        using var laserSubscription = f.Rig.Engine.AnimationMessages.SubscribeMessage(NativeLaserObservation.Tag, true, p =>
        {
            var point = Assert.IsType<NativeLaserObservation>(p);
            points.Add(point);
            deliveryOrder.Add((NativeLaserObservation.Tag, point.Timestamp));
        });
        using var imageSubscription = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x43, true, p =>
        {
            var image = Assert.IsType<ProcessedImageReport>(p);
            deliveryOrder.Add((0x43, image.Timestamp));
            completed.Add(image);
        });
        uint id = 4000;
        void Frame(byte background, (float X, float Y)? target)
        {
            var pixels = Enumerable.Repeat(background, 320 * 240).ToArray();
            if (target is { } point)
            {
                var h = Assert.IsType<float[]>(projection);
                float inv = 1f / (h[6] * point.X + h[7] * point.Y + h[8]);
                int x = (int)MathF.Round((h[0] * point.X + h[1] * point.Y + h[2]) * inv / 2f) * 2;
                int y = (int)MathF.Round((h[3] * point.X + h[4] * point.Y + h[5]) * inv / 2f) * 2;
                Assert.InRange(x, 4, 315);
                Assert.InRange(y, 4, 235);
                for (int row = y - 3; row <= y + 3; row++)
                    for (int col = x - 3; col <= x + 3; col++)
                        pixels[row * 320 + col] = 255;
            }

            // Keep the captured head/pose fixed so this test controls where the laser lands on the ground.
            f.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos, origin: f.Robot.CurrentOriginId, head: CozmoMotion.MinHeadAngleRad);
            f.NativeCameraFrame(++id, pixels);
            f.Rig.Engine.VisionUpdateAllResultsFailed!();
            f.Rig.Tick();
        }

        Frame(110, null);
        Assert.NotNull(projection);
        Assert.Empty(points);
        Frame(30, (100f, -18f));
        var found = Assert.Single(points);
        Assert.InRange(found.X, (short)97, (short)103);
        Assert.InRange(found.Y, (short)-21, (short)-15);
        Assert.True(found.Value > 0);
        Assert.Equal(new[] { 8u, 15u }, Assert.Single(completed, frame => frame.Timestamp == found.Timestamp).Modes);
        Assert.Collection(deliveryOrder.Where(item => item.Timestamp == found.Timestamp),
            item => Assert.Equal(NativeLaserObservation.Tag, item.Tag),
            item => Assert.Equal((ushort)0x43, item.Tag));
        if (behavior.StateName == "WaitingForExposureChange")
            Assert.True(behavior.Update(f.Runtime.Context, timer.TimeStampMs));
        Assert.Equal("RespondToLaser", behavior.StateName);
        Assert.IsType<CompoundActionSequential>(behavior.OwnedRunner);
        // The response compound has its own tested firmware/animation lifecycle; provide its completion edge.
        f.Rig.Engine.AnimationMessages.Deliver(0x5A, true,
            new RobotCompletedAction(behavior.CurrentRunnerTag, 0, 0, Array.Empty<uint>(), 0));
        Assert.Equal("TrackLaser", behavior.StateName);
        var tracking = Assert.IsType<NativeTrackGroundPointRunner>(behavior.OwnedRunner);
        f.Rig.Tick();
        int start = f.Rig.Mark();
        Frame(30, (100f, 18f));
        Frame(30, (100f, -18f));
        Assert.Contains(f.Rig.SentSince(start).OfType<DriveWheelsCurvature>(), move => move.Direction < 0 && move.WheelSpeedMmps > 0);
        Assert.Equal("TrackLaser", behavior.StateName);
        int observations = points.Count;
        int prediction = f.Rig.Mark();
        Frame(30, null);
        Assert.Equal(observations, points.Count);
        Assert.Contains(f.Rig.SentSince(prediction).OfType<DriveWheelsCurvature>(), move => move.Direction < 0 && move.WheelSpeedMmps > 0);
        Assert.Equal("TrackLaser", behavior.StateName);

        for (int i = 0; i < 12 && behavior.StateName == "TrackLaser"; i++)
            Frame(30, (53f, 0f));
        Assert.True(tracking.DestructionObserved);
        Assert.Equal("Pouncing", behavior.StateName);
        Assert.Equal((AnimationTrigger)0x106, Assert.IsType<TriggerLiftSafeAnimationAction>(behavior.OwnedRunner).Request.Trigger);
    }

    [Fact]
    public void LaserDetectorUsesTheBinaryAreaAndMinimumExposureThresholds()
    {
        var ground = new OverheadEdgeGroundInput(new float[] { 0, 1, 160, -1, 0, 240, 0, 0, 1 }, true, 0);
        var image = new NativeMotionImage(120, 160, 1, 160, Enumerable.Repeat((byte)30, 120 * 160).ToArray(), 123);
        for (int y = 94; y <= 96; y++)
            for (int x = 85; x <= 87; x++)
                image.Pixels[y * 160 + x] = 161;
        NativeLaserObservation? Detect(bool minimum) =>
            NativeLaserPointDetector.Detect(image, ground, 320, 240, minimum, 0, (key, message) => Assert.Fail($"{key} {message}"));

        Assert.Null(Detect(false));
        var expected = new NativeLaserObservation(123, 9f / 3562.5f, 50, 12);
        Assert.Equal(expected, Detect(true));
        for (int y = 94; y <= 96; y++)
            for (int x = 85; x <= 87; x++)
                image.Pixels[y * 160 + x] = 160;
        Assert.Null(Detect(true)); // The peak comparison at 0x006A7308 is strictly greater.

        var rgb = new NativeMotionImage(120, 160, 3, 480, Enumerable.Repeat((byte)30, 120 * 480).ToArray(), 123);
        void ColorSpot(byte green, byte blue)
        {
            for (int y = 94; y <= 96; y++)
                for (int x = 85; x <= 87; x++)
                {
                    int offset = y * rgb.Stride + x * 3;
                    rgb.Pixels[offset] = 255;
                    rgb.Pixels[offset + 1] = green;
                    rgb.Pixels[offset + 2] = blue;
                }
        }
        NativeLaserObservation? DetectColor() =>
            NativeLaserPointDetector.Detect(rgb, ground, 320, 240, true, 0, (key, message) => Assert.Fail($"{key} {message}"));
        ColorSpot(30, 30);
        Assert.Equal(expected, DetectColor());
        ColorSpot(255, 255);
        Assert.Null(DetectColor()); // 0x006A8390..0x006A83C6 requires red or green dominance.
    }

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
            f.M.Vision.ProcessImage(image, stamp, stamp, poseData, new OverheadEdgeGroundInput(new float[9], false, 0));
        }
        ProcessFrame(99);
        Assert.Equal(new[] { 8, 15 }, Enumerable.Range(0, AllVisionModesSchedule.ModeCount)
            .Where(i => f.M.Vision.Schedules![i].Frames.Single()));

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
