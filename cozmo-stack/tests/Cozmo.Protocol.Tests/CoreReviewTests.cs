using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The post-fidelity core review: integration and lifetime behaviour that the per-subsystem tests missed
/// because they drove the pieces directly instead of through the path production uses. Every test here
/// exercises the production path - the real robot object, the real tick loops, the real handlers - and
/// nothing drives a scheduler or a component by hand unless production does.
/// </summary>
public class CoreReviewTests
{
    /// <summary>Waits for a condition, polling, so a real background thread has time to do its work.</summary>
    private static bool Within(int ms, Func<bool> cond)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            if (cond()) return true;
            Thread.Sleep(5);
        }
        return cond();
    }

    /// <summary>Every CLAD message the robot has queued or sent, decoded.</summary>
    private static List<RobotMessage> Outbound(CozmoRobot robot)
    {
        var seen = new HashSet<ushort>();
        var outp = new List<RobotMessage>();
        foreach (var sm in robot.Transport.OfflineOutbound.SelectMany(f => f.Messages))
        {
            if (sm.Type is not (ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage)
                || sm.Payload.Length == 0) continue;
            if (sm.Seq != 0 && !seen.Add(sm.Seq)) continue;
            outp.Add(RobotMessage.Parse(sm.Payload));
        }
        return outp;
    }

    // ================================================================ CORE-001

    /// <summary>
    /// CORE-001. A keep-alive body shuffle has to stop at its duration when idle is the only thing
    /// running.
    ///
    /// The scheduler has always known when to stop it - <c>Advance</c> serves the deadline whether or not
    /// a clip is playing - but nothing in production was calling <c>Advance</c>: the animation tick loop
    /// ran only while a clip was playing, and a live keyframe is not a clip. Idle would stream
    /// <c>animBodyMotion</c> with a speed and a duration and then never send the stop, so the wheels ran
    /// until something else countermanded them.
    ///
    /// This drives idle exactly as an application does - <c>idle.Advance(now)</c> on the wall clock, with
    /// <c>Execute</c> and <c>ExecuteMotors</c> on - and nothing here touches the scheduler.
    /// </summary>
    [Fact]
    public void CORE001_AnIdleBodyShuffleIsStoppedAtItsDurationWithNobodyDrivingTheScheduler()
    {
        using var robot = CozmoRobot.CreateOffline();
        // one shuffle, short, straight (a known radius, so a stop is owed), and nothing else moving
        var quiet = IdleParameters.Default with
        {
            TimeBeforeWiggleMotionsMs = 0,
            BlinkSpacingMinMs = 600_000,
            BlinkSpacingMaxMs = 600_000,
            EyeDartMaxDistancePix = 0,
            HeadMovementSpacingMinMs = 600_000,
            HeadMovementSpacingMaxMs = 600_000,
            LiftMovementSpacingMinMs = 600_000,
            LiftMovementSpacingMaxMs = 600_000,
            BodyMovementDurationMinMs = 200,
            BodyMovementDurationMaxMs = 200,
            BodyMovementSpacingMinMs = 600_000,
            BodyMovementSpacingMaxMs = 600_000,
            BodyMovementStraightFraction = 1,
        };
        var idle = new IdleBehavior(robot, new BehaviorArbiter { AutonomyEnabled = true }, quiet, new Random(6))
        {
            Execute = true,
            ExecuteMotors = true,
        };

        IdleEvent? shuffle = null;
        idle.Acted += e => { if (e.Action == IdleAction.BodyMove && e.Suppressed is null) shuffle ??= e; };

        // tick idle the way an application would, on the wall clock, until it shuffles with a real speed
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3_000 && (shuffle is null || shuffle.Amount == 0))
        {
            if (shuffle is { Amount: 0 }) shuffle = null;      // a zero-speed draw owes no stop; wait for a real one
            idle.Advance(Environment.TickCount64);
            Thread.Sleep(5);
        }
        Assert.NotNull(shuffle);
        Assert.NotEqual(0, shuffle!.Amount);

        var driving = Outbound(robot).OfType<BodyMotion>().Where(b => b.Speed != 0).ToList();
        Assert.NotEmpty(driving);

        // nothing else is ticking anything: if the stop arrives, the animation system brought it
        Assert.True(Within(2_000, () => Outbound(robot).OfType<BodyMotion>().Any(b => b.Speed == 0)),
                    "the keep-alive body keyframe was never stopped");

        // and it waited for the duration rather than stopping at once
        Assert.True(sw.ElapsedMilliseconds >= 200);
    }

    /// <summary>
    /// CORE-001, the other half: the deadline and the tick loop have to be on one clock. The keyframe's
    /// stop time is recorded against the clock <c>StreamLive</c> is given and served against the clock
    /// <c>Advance</c> is driven on, so an idle tick clock that starts at zero while the animation loop
    /// runs on <c>Environment.TickCount64</c> would stop the wheels on the first tick instead of at the
    /// duration. Going through the animation system rather than straight at the scheduler is what keeps
    /// the two the same.
    /// </summary>
    [Fact]
    public void CORE001_TheLiveKeyframeClockIsTheAnimationSystemsOwn()
    {
        using var robot = CozmoRobot.CreateOffline();
        var quiet = IdleParameters.Default with
        {
            TimeBeforeWiggleMotionsMs = 0,
            BlinkSpacingMinMs = 600_000,
            BlinkSpacingMaxMs = 600_000,
            EyeDartMaxDistancePix = 0,
            HeadMovementSpacingMinMs = 600_000,
            HeadMovementSpacingMaxMs = 600_000,
            LiftMovementSpacingMinMs = 600_000,
            LiftMovementSpacingMaxMs = 600_000,
            BodyMovementDurationMinMs = 400,
            BodyMovementDurationMaxMs = 400,
            BodyMovementSpacingMinMs = 600_000,
            BodyMovementSpacingMaxMs = 600_000,
            BodyMovementStraightFraction = 1,
        };
        var idle = new IdleBehavior(robot, new BehaviorArbiter { AutonomyEnabled = true }, quiet, new Random(6))
        {
            Execute = true,
            ExecuteMotors = true,
        };

        IdleEvent? shuffle = null;
        idle.Acted += e => { if (e.Action == IdleAction.BodyMove && e.Suppressed is null && e.Amount != 0) shuffle ??= e; };

        // an idle clock of its own, starting at zero - what a caller with a stopwatch would pass
        double t = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3_000 && shuffle is null)
        {
            idle.Advance(t);
            t += 20;
            Thread.Sleep(5);
        }
        Assert.NotNull(shuffle);

        // the stop must not be there yet: the keyframe has 400 ms to run
        Assert.False(Within(150, () => Outbound(robot).OfType<BodyMotion>().Any(b => b.Speed == 0)),
                     "the body was stopped immediately, so the deadline was read on the wrong clock");
        Assert.True(Within(1_500, () => Outbound(robot).OfType<BodyMotion>().Any(b => b.Speed == 0)),
                    "the keep-alive body keyframe was never stopped");
    }
}
