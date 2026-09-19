using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Regressions for the idle face defect found on hardware: over time the two eyes grew and drifted
/// together until they formed one large rectangle.
///
/// The cause was that each dart read the live face, offset it, and wrote it back as the new persistent
/// face, so every dart compounded the last — positions random-walked and EyeScale gained another 0.1 each
/// time. The engine instead keeps the base face untouched and adds a named, time-limited layer over it
/// (TrackLayerComponent::AddOrUpdateEyeShift -> FaceLayerManager::GenerateEyeShift -> AddPersistentLayer),
/// removing it afterwards.
///
/// These tests run hundreds of darts and assert the two properties that must hold whatever the remaining
/// fidelity questions: position and scale stay bounded, and the base pose survives.
/// </summary>
public class IdleFaceTests
{
    /// <summary>Drives idle long enough to produce hundreds of darts and blinks, recording every face sent.</summary>
    private static (List<ProceduralFacePose> Sent, ProceduralFacePose Start, IdleBehavior Idle, CozmoRobot Robot)
        RunIdle(double forMs, int seed = 9)
    {
        var robot = CozmoRobot.CreateOffline();
        var start = robot.Face.Current.Clone();
        var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
        var idle = new IdleBehavior(robot, arbiter, random: new Random(seed)) { Execute = true, ExecuteMotors = false };

        // Sampled rather than hooked: what matters is what is actually on the face at each moment, and
        // this needs no change to the frozen M5 facade.
        var sent = new List<ProceduralFacePose>();
        for (double t = 0; t < forMs; t += 20)
        {
            idle.Advance(t);
            sent.Add(robot.Face.Current.Clone());
        }
        return (sent, start, idle, robot);
    }

    private static float Max(IEnumerable<ProceduralFacePose> poses, EyeParam p) =>
        poses.SelectMany(x => new[] { x.Left[(int)p], x.Right[(int)p] }).Max();

    private static float Min(IEnumerable<ProceduralFacePose> poses, EyeParam p) =>
        poses.SelectMany(x => new[] { x.Left[(int)p], x.Right[(int)p] }).Min();

    /// <summary>
    /// The headline regression. Over four minutes of idling — several hundred darts — the eyes must stay
    /// within one dart's distance of where they started. Before the fix this walked away without limit.
    /// </summary>
    [Fact]
    public void EyeCentresStayBoundedAcrossHundredsOfDarts()
    {
        var (sent, start, idle, robot) = RunIdle(240_000);
        using (robot)
        {
            Assert.True(idle.ActionCount > 200,
                $"only {idle.ActionCount} idle actions occurred; the test needs hundreds of darts");

            var p = IdleParameters.Default;
            float allowance = (float)p.EyeDartMaxDistancePix + 0.001f;
            float startLeft = start.Left[(int)EyeParam.EyeCenterX];
            float startRight = start.Right[(int)EyeParam.EyeCenterX];

            foreach (var pose in sent)
            {
                Assert.InRange(pose.Left[(int)EyeParam.EyeCenterX], startLeft - allowance, startLeft + allowance);
                Assert.InRange(pose.Right[(int)EyeParam.EyeCenterX], startRight - allowance, startRight + allowance);
            }
        }
    }

    /// <summary>
    /// Scale must be bounded by the shipped parameters, not grow by EyeDartOuterEyeScaleIncrease every
    /// time. This is what made the eyes merge into a rectangle on hardware.
    /// </summary>
    [Fact]
    public void EyeScalesStayBoundedAndDoNotGrowCumulatively()
    {
        var (sent, _, _, robot) = RunIdle(240_000);
        using (robot)
        {
            var p = IdleParameters.Default;
            float ceiling = (float)p.EyeDartMaxScale + 0.001f;
            float floor = (float)p.EyeDartMinScale - 0.001f;

            Assert.True(Max(sent, EyeParam.EyeScaleX) <= ceiling,
                $"EyeScaleX reached {Max(sent, EyeParam.EyeScaleX)}, above the {p.EyeDartMaxScale} maximum");
            Assert.True(Max(sent, EyeParam.EyeScaleY) <= ceiling,
                $"EyeScaleY reached {Max(sent, EyeParam.EyeScaleY)}, above the {p.EyeDartMaxScale} maximum");
            Assert.True(Min(sent, EyeParam.EyeScaleX) >= floor);

            // No accumulation: the largest scale reached in the second half of the run is the same as in
            // the first, rather than creeping up. Comparing individual samples would only say whether a
            // dart happened to be active at that instant.
            int half = sent.Count / 2;
            var firstHalf = sent.Take(half).Select(x => x.Left[(int)EyeParam.EyeScaleX]).Max();
            var secondHalf = sent.Skip(half).Select(x => x.Left[(int)EyeParam.EyeScaleX]).Max();
            Assert.Equal(firstHalf, secondHalf, 3);
        }
    }

    /// <summary>
    /// After the transient motion has expired, the face on screen must be the pose idle started from, not
    /// a mutated one. This is the "base pose is not corrupted" property.
    /// </summary>
    [Fact]
    public void TheFaceReturnsToItsBasePoseAfterTheDartExpires()
    {
        var robot = CozmoRobot.CreateOffline();
        using (robot)
        {
            var start = robot.Face.Current.Clone();
            var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
            var idle = new IdleBehavior(robot, arbiter, random: new Random(2)) { ExecuteMotors = false };

            // Long enough for many darts, then quiet long enough for the last one to expire.
            for (double t = 0; t < 60_000; t += 20) idle.Advance(t);
            for (double t = 60_000; t < 60_400; t += 20) idle.Advance(t);

            var now = robot.Face.Current;
            for (int i = 0; i < Eye.ParamCount; i++)
            {
                Assert.Equal(start.Left[i], now.Left[i], 3);
                Assert.Equal(start.Right[i], now.Right[i], 3);
            }
        }
    }

    /// <summary>
    /// A blink must restore the stable face, not whatever a dart left behind. Before the fix a blink
    /// captured the mutated pose and put that back, cementing the drift.
    /// </summary>
    [Fact]
    public void ABlinkRestoresTheStableFaceRatherThanAMutatedDartPose()
    {
        var robot = CozmoRobot.CreateOffline();
        using (robot)
        {
            var start = robot.Face.Current.Clone();
            var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
            var idle = new IdleBehavior(robot, arbiter, random: new Random(5)) { ExecuteMotors = false };

            var afterBlinks = new List<ProceduralFacePose>();
            idle.Acted += e => { if (e.Action == IdleAction.Blink && e.Suppressed is null) afterBlinks.Add(robot.Face.Current.Clone()); };

            for (double t = 0; t < 120_000; t += 20) idle.Advance(t);
            Assert.True(afterBlinks.Count >= 10, $"only {afterBlinks.Count} blinks occurred");

            // Every blink is the base pose with the lids shut: the eyes are where they started.
            foreach (var blink in afterBlinks)
            {
                Assert.Equal(start.Left[(int)EyeParam.EyeCenterX], blink.Left[(int)EyeParam.EyeCenterX], 3);
                Assert.Equal(start.Right[(int)EyeParam.EyeCenterX], blink.Right[(int)EyeParam.EyeCenterX], 3);
                Assert.Equal(start.Left[(int)EyeParam.EyeScaleX], blink.Left[(int)EyeParam.EyeScaleX], 3);
            }
        }
    }

    /// <summary>
    /// A dart lasts for the duration it was given. The duration used to be computed and then ignored, so
    /// each dart simply stayed until the next one displaced it.
    /// </summary>
    [Fact]
    public void ADartLastsForItsDurationAndThenTheFaceReturns()
    {
        var robot = CozmoRobot.CreateOffline();
        using (robot)
        {
            var start = robot.Face.Current.Clone();
            var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
            var idle = new IdleBehavior(robot, arbiter, random: new Random(1)) { ExecuteMotors = false };

            double? dartAt = null;
            double dartFor = 0;
            idle.Acted += e =>
            {
                if (dartAt is null && e.Action == IdleAction.EyeDart && e.Suppressed is null)
                { dartAt = e.AtMs; dartFor = e.DurationMs; }
            };

            for (double t = 0; t < 5_000 && dartAt is null; t += 10) idle.Advance(t);
            Assert.NotNull(dartAt);
            Assert.True(dartFor > 0, "the dart carried no duration");

            // Immediately after, the face is shifted.
            Assert.NotEqual(start.Left[(int)EyeParam.EyeCenterX], robot.Face.Current.Left[(int)EyeParam.EyeCenterX]);

            // Past the duration, it is back.
            for (double t = dartAt!.Value; t <= dartAt.Value + dartFor + 60; t += 10) idle.Advance(t);
            Assert.Equal(start.Left[(int)EyeParam.EyeCenterX],
                         robot.Face.Current.Left[(int)EyeParam.EyeCenterX], 3);
        }
    }

    /// <summary>
    /// When an animation owns the face, idle must forget the base it captured: what is on screen is no
    /// longer what it recorded, so measuring the next dart from it would drag the face somewhere wrong.
    /// </summary>
    [Fact]
    public void IdleForgetsItsBaseWhenSomethingElseTakesTheFace()
    {
        var robot = CozmoRobot.CreateOffline();
        using (robot)
        {
            var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
            var idle = new IdleBehavior(robot, arbiter, random: new Random(6)) { ExecuteMotors = false };
            for (double t = 0; t < 5_000; t += 20) idle.Advance(t);

            // A different face arrives from elsewhere, as an animation would set it.
            var moved = robot.Face.Current.Clone();
            moved.Left[(int)EyeParam.EyeCenterX] += 20f;
            moved.Right[(int)EyeParam.EyeCenterX] += 20f;
            robot.Face.SetParameters(moved);
            idle.ForgetBaseFace();

            var newBase = robot.Face.Current.Clone();
            for (double t = 5_000; t < 30_000; t += 20) idle.Advance(t);

            // Darts are now measured from the new face, still bounded.
            float allowance = (float)IdleParameters.Default.EyeDartMaxDistancePix + 0.001f;
            Assert.InRange(robot.Face.Current.Left[(int)EyeParam.EyeCenterX],
                           newBase.Left[(int)EyeParam.EyeCenterX] - allowance,
                           newBase.Left[(int)EyeParam.EyeCenterX] + allowance);
        }
    }
}
