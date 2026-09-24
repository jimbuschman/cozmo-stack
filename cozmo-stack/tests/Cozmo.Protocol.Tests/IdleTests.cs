using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Tests for the keep-alive layer. Timing is driven by the caller, so these run instantly and
/// deterministically rather than waiting out real blink spacings.
/// </summary>
public class IdleTests
{
    private static (IdleBehavior Idle, BehaviorArbiter Arb, CozmoRobot Robot) Make(int seed = 3)
    {
        var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var arb = new BehaviorArbiter { AutonomyEnabled = true };
        var idle = new IdleBehavior(robot, arb, random: new Random(seed)) { Execute = false };
        return (idle, arb, robot);
    }

    /// <summary>The values are the engine's, read out of SetDefaultParams. Pinning them catches a drift.</summary>
    [Fact]
    public void TheDefaultsAreTheEnginesOwn()
    {
        var p = IdleParameters.Default;
        Assert.Equal(3000, p.BlinkSpacingMinMs);
        Assert.Equal(4000, p.BlinkSpacingMaxMs);
        Assert.Equal(1000, p.TimeBeforeWiggleMotionsMs);
        Assert.Equal(10, p.BodyMovementSpeedMmps);
        Assert.Equal(35, p.LiftHeightMeanMm);
        Assert.Equal(6, p.HeadAngleVariabilityDeg);
        Assert.Equal(6, p.EyeDartMaxDistancePix);
        Assert.Equal(0.85, p.EyeDartDownMinScale, 3);
        Assert.Equal(1.1, p.EyeDartUpMaxScale, 3);
    }

    [Fact]
    public void NothingHappensUntilAutonomyIsEnabled()
    {
        var (idle, arb, robot) = Make();
        using (robot)
        {
            arb.AutonomyEnabled = false;
            for (double t = 0; t < 30_000; t += 100) Assert.Empty(idle.Advance(t));
            Assert.Equal(0, idle.ActionCount);
        }
    }

    [Fact]
    public void AnIdleRobotBlinksAndDartsItsEyes()
    {
        var (idle, _, robot) = Make();
        using (robot)
        {
            var seen = new List<IdleEvent>();
            idle.Acted += seen.Add;
            for (double t = 0; t < 30_000; t += 50) idle.Advance(t);

            var blinks = seen.Count(e => e.Action == IdleAction.Blink && e.Suppressed is null);
            var darts = seen.Count(e => e.Action == IdleAction.EyeDart && e.Suppressed is null);
            // 30 s at 3-4 s spacing is 7 to 10 blinks; at 250-1000 ms it is 30 to 120 darts
            Assert.InRange(blinks, 6, 11);
            Assert.InRange(darts, 25, 125);
        }
    }

    /// <summary>
    /// TimeBeforeWiggleMotions_ms exists so the robot does not twitch the instant it goes idle. The face
    /// carries on from the first moment; the motors wait.
    /// </summary>
    [Fact]
    public void MovementWaitsOutTheSettlingTimeButTheFaceDoesNot()
    {
        var (idle, _, robot) = Make();
        using (robot)
        {
            var seen = new List<IdleEvent>();
            idle.Acted += seen.Add;
            for (double t = 0; t < 999; t += 10) idle.Advance(t);

            Assert.DoesNotContain(seen, e => e.Action is IdleAction.HeadMove or IdleAction.LiftMove or IdleAction.BodyMove);
            // and once past it, the motors start
            for (double t = 1000; t < 6000; t += 10) idle.Advance(t);
            Assert.Contains(seen, e => e.Action is IdleAction.HeadMove or IdleAction.LiftMove);
        }
    }

    [Fact]
    public void IdleStandsDownWhileSomethingElseIsRunning()
    {
        var (idle, arb, robot) = Make();
        using (robot)
        {
            arb.Request(BehaviorPriority.Caller, "caller animation");
            var seen = new List<IdleEvent>();
            idle.Acted += seen.Add;
            for (double t = 0; t < 20_000; t += 100) idle.Advance(t);

            Assert.Equal(0, idle.ActionCount);
            Assert.All(seen, e => Assert.NotNull(e.Suppressed));
            Assert.Contains(seen, e => e.Suppressed!.Contains("Caller"));
        }
    }

    /// <summary>
    /// The settling time restarts when a caller finishes, so the robot does not lurch straight back into
    /// wiggling the moment an animation ends.
    /// </summary>
    [Fact]
    public void TheSettlingTimeRestartsAfterSomethingElseRan()
    {
        var (idle, arb, robot) = Make();
        using (robot)
        {
            for (double t = 0; t < 3000; t += 10) idle.Advance(t);   // settled, motors active
            arb.Request(BehaviorPriority.Caller, "anim");
            idle.Advance(3100);
            arb.Finished(BehaviorPriority.Caller);

            var seen = new List<IdleEvent>();
            idle.Acted += seen.Add;
            for (double t = 3200; t < 4100; t += 10) idle.Advance(t);
            Assert.DoesNotContain(seen, e =>
                e.Suppressed is null && e.Action is IdleAction.HeadMove or IdleAction.LiftMove or IdleAction.BodyMove);
        }
    }

    [Fact]
    public void IdleIsReproducibleForAGivenSeed()
    {
        var a = Make(11); var b = Make(11);
        using (a.Robot)
        using (b.Robot)
        {
            var ea = new List<IdleEvent>(); var eb = new List<IdleEvent>();
            a.Idle.Acted += ea.Add; b.Idle.Acted += eb.Add;
            for (double t = 0; t < 20_000; t += 100) { a.Idle.Advance(t); b.Idle.Advance(t); }
            Assert.Equal(ea.Select(x => (x.Action, x.AtMs)), eb.Select(x => (x.Action, x.AtMs)));
        }
    }
}
