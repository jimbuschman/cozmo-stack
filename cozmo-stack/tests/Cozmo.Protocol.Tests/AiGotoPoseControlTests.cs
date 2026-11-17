using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class AiGotoPoseControlTests
{
    // fidelity: M12-023, M12-024
    [Fact]
    public async Task QueuesAnOwnedWorldOriginGoalWithTheHandlerDefaults()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        using var cancel = new CancellationTokenSource();
        var task = f.Rig.Robot.Motion.QueueAiGotoPoseAsync(170, -32, MathF.PI / 2, cancel.Token);
        try
        {
            var drive = Assert.IsType<DriveToPoseAction>(f.Robot.ActionList.GetCurrentAction());
            Assert.Equal(0x0C, drive.Type);
            Assert.Equal(4u, drive.RequiredTrackMask);
            Assert.False(drive.LowersHead);
            Assert.False(drive.UsesManualSpeed);
            Assert.Equal(10d, drive.DistanceToleranceMm);
            Assert.Equal((double)BitConverter.UInt32BitsToSingle(0x3E32B8C2), drive.AngleToleranceRad);
            Assert.Equal(4d, drive.PlanningTimeoutSec);
            var goal = Assert.Single(drive.Goals!);
            Assert.Equal("GotoPoseTarget_COPY", goal.Name);
            Assert.Equal(new Vec3(170, -32, 0), goal.Translation);
            Assert.Equal(MathF.PI / 2, (float)goal.AngleAroundZ, 5);
            Pose3d parent = null!;
            Assert.True(goal.TryGetParent(ref parent));
            using (parent)
                Assert.Same(f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node, parent.Node);

            cancel.Cancel();
            f.Rig.Tick();
            var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);
            Assert.Equal(EngineActionResult.NotStarted, result.EngineResult);
        }
        finally { f.Robot.ActionList.Clear(); }
    }

    [Fact]
    public async Task CompletedCustomDriveClearsSelectionAndNextDefaultDriveUsesTheChooser()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var motion = f.Rig.Robot.Motion;
        var path = motion.Path;
        var profile = PathMotionProfile.Default with { IsCustom = true, SpeedMmps = 53f, AccelMmps2 = 140f, DecelMmps2 = 190f };
        var count = (AnimationTrigger)0x23F;
        f.Rig.Robot.Animations.DrivingAnimations.Push(new(count, count, count), "ProfileRegression");
        f.Rig.Engine.AnimationMessages.Deliver(0xE1, false, new SetEnableSpeedChooserMessage(false));

        async Task CompleteDrive(Task<AiDriveActionResult> request, int mark, float destination, PathMotionProfile expected)
        {
            for (int i = 0; i < 70 && !f.Rig.SentSince(mark).OfType<ExecutePath>().Any(); i++)
                f.Motors();
            var sent = f.Rig.SentSince(mark);
            // ApplyMotionProfile 0x00508F48..0x0050906E splits the deceleration tail before the final turn.
            var lines = sent.OfType<AppendPathSegmentLine>().ToArray();
            Assert.Equal(2, lines.Length);
            var (cruise, tail) = (lines[0], lines[1]);
            Assert.Equal((expected.SpeedMmps, expected.AccelMmps2, expected.DecelMmps2),
                (cruise.Speed.SpeedMmps, cruise.Speed.AccelMmps2, cruise.Speed.DecelMmps2));
            Assert.Equal((20f, expected.AccelMmps2, expected.DecelMmps2),
                (tail.Speed.SpeedMmps, tail.Speed.AccelMmps2, tail.Speed.DecelMmps2));
            Assert.Equal((cruise.XEndMm, cruise.YEndMm), (tail.XStartMm, tail.YStartMm));
            Assert.InRange(MathF.Abs(tail.XEndMm - (destination - 20f)), 0, .001f);
            float tailLength = expected.SpeedMmps * expected.SpeedMmps / (2f * expected.DecelMmps2);
            Assert.InRange(MathF.Abs((tail.XEndMm - tail.XStartMm) - tailLength), 0, .001f);
            var execute = Assert.Single(sent.OfType<ExecutePath>());
            f.Rig.Data(new PathFollowingEvent { EventId = execute.EventId, EventType = (byte)PathEventType.Started });
            f.Motors();
            f.X = destination;
            f.Motors();
            f.Rig.Data(new PathFollowingEvent { EventId = execute.EventId, EventType = (byte)PathEventType.Completed });
            for (int i = 0; i < 100 && !request.IsCompleted; i++) f.Motors();
            var result = await request.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);
            Assert.Equal(EngineActionResult.Success, result.EngineResult);
        }

        int mark = f.Rig.Mark();
        var custom = motion.QueueAiGotoPoseAsync(170f, 0f, 0f, profile, CancellationToken.None);
        Assert.True(path.Field48); // SetForAction installs before Init.
        Assert.Equal(profile, path.NativeMotionProfile);
        await CompleteDrive(custom, mark, 170f, profile);
        Assert.False(path.Field48); // Destructor clears a byte, leaving the stored values.
        Assert.Equal(profile, path.NativeMotionProfile);

        mark = f.Rig.Mark();
        await CompleteDrive(motion.QueueAiGotoPoseAsync(360f, 0f, 0f), mark, 360f, PathMotionProfile.Default);
        Assert.False(path.Field48);
        Assert.Equal(PathMotionProfile.Default, path.NativeMotionProfile);
    }

    [Fact]
    public async Task PendingOwnerClearsSharedFlagEvenWhenAnotherActionInstalledNewValues()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var motion = f.Rig.Robot.Motion;
        var path = motion.Path;
        using var previous = new CancellationTokenSource();
        using var next = new CancellationTokenSource();
        _ = motion.SetHeadAngleAsync(.45f); // Keep both drives pending to isolate the destructor from Init.
        var oldProfile = PathMotionProfile.Default with { IsCustom = true, SpeedMmps = 40f };
        var newProfile = oldProfile with { SpeedMmps = 75f };
        var first = motion.QueueAiGotoPoseAsync(170f, 0f, 0f, oldProfile, previous.Token);
        var second = motion.QueueAiGotoPoseAsync(210f, 0f, 0f, newProfile, next.Token);
        Assert.True(path.Field48);
        Assert.Equal(newProfile, path.NativeMotionProfile);

        previous.Cancel();
        f.Rig.Tick();
        Assert.Equal(EngineActionResult.NotStarted, (await first.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        Assert.False(path.Field48);
        Assert.Equal(newProfile, path.NativeMotionProfile);
        Assert.False(second.IsCompleted);
        next.Cancel();
        f.Rig.Tick();
        Assert.Equal(EngineActionResult.NotStarted, (await second.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
    }

    [Theory]
    [InlineData(float.NaN, 0, 0)]
    [InlineData(0, float.PositiveInfinity, 0)]
    [InlineData(0, 0, float.NegativeInfinity)]
    public async Task RejectsNonfinitePoseBeforeAdmission(float x, float y, float angle)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var result = await f.Rig.Robot.Motion.QueueAiGotoPoseAsync(x, y, angle);
        Assert.False(result.Admitted);
        Assert.Null(result.EngineResult);
        Assert.Null(f.Robot.ActionList.GetCurrentAction());
    }
}
