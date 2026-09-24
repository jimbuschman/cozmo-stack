using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Regressions for the pre-acceptance hardening pass. Each test here stands for one defect found by
/// review, and is written to fail against the code as it was.
/// </summary>
public class HardeningTests
{
    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "assets", "animationGroups")))
                return r;
            d = d.Parent;
        }
        return null;
    }

    // ================================================================ M5 concurrency

    private sealed class CountingSink : IAnimationSink
    {
        public readonly List<string> Events = new();
        private readonly object _gate = new();
        public void Face(FaceBitmap b) { }
        public void Audio(byte[]? f) { }
        public void Head(sbyte r, uint d) { }
        public void Lift(byte h, uint d) { }
        public void AnimationStarted(byte tag) { }
        public void AnimationEnded() { }
        public void Body(BodyKeyframe k) { }
        public void BodyStop() { }
        public void Lights(LightsKeyframe k) { }
        public void Event(string id) { lock (_gate) Events.Add(id); }
        public void Finished(string clip, bool completed) { }
    }

    private static AnimationClip Clip(string name, params Keyframe[] frames)
    {
        var list = frames.OrderBy(f => f.TriggerTimeMs).ToList();
        AnimationTrack tracks = 0;
        uint end = 0;
        foreach (var f in list) { tracks |= f.Track; end = Math.Max(end, f.EndTimeMs); }
        return new AnimationClip { Name = name, Keyframes = list, Tracks = tracks, DurationMs = end };
    }

    /// <summary>
    /// Advance collects due keyframes under the lock, then dispatches them outside it. If the animation is
    /// replaced in that window, the outgoing clip's keyframes were emitted into the incoming one — on
    /// hardware, one animation's motion appearing in the middle of another.
    ///
    /// The scheduler is driven directly here, and a keyframe callback replaces the animation mid-dispatch,
    /// which is exactly the interleaving the generation check has to catch. Deterministic: no threads.
    /// </summary>
    [Fact]
    public void KeyframesOfAReplacedAnimationAreNotEmittedIntoItsReplacement()
    {
        var sink = new CountingSink();
        var scheduler = new AnimationScheduler(sink);

        var replacement = Clip("second", new EventKeyframe(0, "second-A"));
        bool swapped = false;

        scheduler.KeyframeFired += _ =>
        {
            // Replace the animation from inside the dispatch loop, between two due keyframes.
            if (swapped) return;
            swapped = true;
            scheduler.Play(replacement, 0);
        };

        scheduler.Play(Clip("first",
            new EventKeyframe(0, "first-A"),
            new EventKeyframe(0, "first-B"),
            new EventKeyframe(0, "first-C")), 0);
        scheduler.Advance(0);

        // first-A fires and swaps the clip; B and C belonged to the animation that no longer exists.
        Assert.Contains("first-A", sink.Events);
        Assert.DoesNotContain("first-B", sink.Events);
        Assert.DoesNotContain("first-C", sink.Events);
    }

    /// <summary>
    /// StopIfCurrent must stop only the animation it names. A behaviour cleaning up after itself must not
    /// cancel an unrelated animation that replaced its own.
    /// </summary>
    [Fact]
    public void StoppingByTokenOnlyStopsTheAnimationThatTokenNames()
    {
        var scheduler = new AnimationScheduler(new CountingSink());
        scheduler.Play(Clip("first", new EventKeyframe(10_000, "late")), 0);
        long first = scheduler.Generation;

        scheduler.Play(Clip("second", new EventKeyframe(10_000, "late")), 0);
        Assert.False(scheduler.StopIfCurrent(first), "the first token must no longer stop anything");
        Assert.Equal("second", scheduler.Playing);

        Assert.True(scheduler.StopIfCurrent(scheduler.Generation));
        Assert.Null(scheduler.Playing);
    }

    /// <summary>
    /// The ticker used to break out of its loop and only then clear _running, so a Play arriving in that
    /// window started no ticker and its animation was never advanced. Hammering start/stop reproduces it.
    /// </summary>
    [Fact]
    public void RestartingAnimationsRepeatedlyAlwaysLeavesATickerRunning()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var lib = robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var name = lib.ClipNames.First();

        for (int i = 0; i < 200; i++)
        {
            robot.Animations.Play(name);
            robot.Animations.Stop();
        }

        // Start one more and give the ticker a moment: if the race left no ticker, position never advances.
        robot.Animations.Play(name);
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && robot.Animations.Scheduler.KeyframesFired == 0
               && robot.Animations.IsPlaying)
            Thread.Sleep(10);

        Assert.True(robot.Animations.Scheduler.KeyframesFired > 0 || !robot.Animations.IsPlaying,
            "an animation was left running with no ticker advancing it");
        robot.Animations.Stop();
    }

    // ================================================================ transport event isolation

    /// <summary>
    /// A throwing subscriber must not stop the ones registered after it. The old code invoked the whole
    /// multicast list as a single call, so one bad handler silently disabled the rest.
    /// </summary>
    [Fact]
    public void AThrowingSubscriberDoesNotPreventLaterSubscribersFromRunning()
    {
        using var t = ReliableTransport.CreateOffline();
        bool laterRan = false;
        int warnings = 0;

        t.Warning += _ => throw new InvalidOperationException("first handler throws");
        t.Warning += _ => laterRan = true;
        t.FrameTrace += _ => { };

        // A malformed datagram raises Warning.
        t.ProcessIncoming(new byte[] { 1, 2, 3, 4, 5 });
        _ = warnings;

        Assert.True(laterRan, "the second subscriber never ran because the first one threw");
        Assert.True(t.HandlerFaults > 0, "the fault should still be counted");
    }

    // ================================================================ motor readiness

    /// <summary>Feeds messages to an offline robot the way a real one would, over the framed transport.</summary>
    private sealed class Rig : IDisposable
    {
        public readonly CozmoRobot Robot = CozmoRobot.CreateOffline();
        private ushort _seq = 1;

        public Rig() => Deliver(new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), _seq++));

        public void Send(RobotMessage m) =>
            Deliver(new SubMessage(ReliableMessageType.SingleReliableMessage, m.ToBytes(), _seq++));

        private void Deliver(SubMessage sm)
        {
            var f = new Frame
            {
                Type = ReliableMessageType.MultipleMixedMessages,
                SeqMin = sm.Seq, SeqMax = sm.Seq, Ack = 0,
                Messages = new List<SubMessage> { sm },
            };
            Robot.Transport.ProcessIncoming(FrameCodec.Encode(f));
        }

        public void Dispose() => Robot.Dispose();
    }

    /// <summary>
    /// A RobotState arriving before any calibration message is not evidence of readiness. Motion in that
    /// window used to be allowed because nothing was "currently calibrating".
    /// </summary>
    [Fact]
    public async Task MotionIsRefusedInTheWindowBeforeCalibrationIsEvenReported()
    {
        using var rig = new Rig();
        rig.Send(new RobotState { Status = 0 });

        Assert.False(rig.Robot.State.CalibrationComplete);
        Assert.False(rig.Robot.State.CalibratingMotors);   // nothing is running, but nothing has finished

        var r = await rig.Robot.Motion.SetHeadAngleAsync(0.3f, timeout: TimeSpan.FromMilliseconds(200));
        Assert.Equal(MotionResult.Refused, r.Result);
    }

    /// <summary>
    /// Head and lift calibrate separately. One flag for both meant the lift finishing cleared it while the
    /// head was still moving, so motion was allowed too early.
    /// </summary>
    [Fact]
    public void OverlappingHeadAndLiftCalibrationAreTrackedIndependently()
    {
        using var rig = new Rig();
        rig.Send(new RobotState { Status = 0 });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true });
        Assert.True(rig.Robot.State.HeadCalibrating);
        Assert.True(rig.Robot.State.LiftCalibrating);

        // The lift finishes first. The head is still going, so nothing is ready.
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false });
        Assert.True(rig.Robot.State.LiftCalibrated);
        Assert.True(rig.Robot.State.HeadCalibrating);
        Assert.True(rig.Robot.State.CalibratingMotors);
        Assert.False(rig.Robot.State.CalibrationComplete);

        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false });
        Assert.True(rig.Robot.State.CalibrationComplete);
        Assert.False(rig.Robot.State.CalibratingMotors);
    }

    // ================================================================ wheel confirmation

    /// <summary>
    /// A wheel is only confirmed when it turns the way it was told. Accepting any motion meant a robot
    /// already rolling satisfied the check the instant the command went out.
    /// </summary>
    [Theory]
    [InlineData(50f, 50f, true)]        // as asked
    [InlineData(50f, -50f, false)]      // opposite direction
    [InlineData(50f, 5f, false)]        // far too slow
    [InlineData(50f, 40f, true)]        // still ramping, within tolerance
    [InlineData(0f, 2f, true)]          // coasting to a halt
    [InlineData(0f, 40f, false)]        // asked to stop, still moving
    public void AWheelIsConfirmedOnlyWhenItMatchesWhatWasAsked(float requested, float reported, bool ok)
        => Assert.Equal(ok, CozmoMotion.WheelMatches(reported, requested));

    /// <summary>
    /// The case the old check got wrong outright: the robot is already driving backwards when a forward
    /// command is sent, and reports motion throughout.
    /// </summary>
    [Fact]
    public async Task DrivingForwardIsNotConfirmedByMotionThatIsStillBackwards()
    {
        using var rig = new Rig();
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false });
        rig.Send(new RobotState
        {
            Status = (uint)RobotStatusFlag.AreWheelsMoving,
            LwheelSpeedMmps = -60f,
            RwheelSpeedMmps = -60f,
        });

        var r = await rig.Robot.Motion.DriveWheelsAsync(60f, 60f,
            confirmWithin: TimeSpan.FromMilliseconds(250));
        Assert.Equal(MotionResult.TimedOut, r.Result);
        Assert.Contains("asked for", r.Detail);
    }
}
