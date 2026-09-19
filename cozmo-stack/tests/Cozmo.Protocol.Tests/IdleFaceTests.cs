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
/// fidelity questions: position and scale stay bounded, and the base pose survives. The bounds are the
/// engine's own, from ProceduralFace::LookAt (0x00584158) for darts and the blink table at 0x00C5AAD8 for
/// blinks; see SourceFidelityTests for the values themselves.
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
    /// The headline regression. Over four minutes of idling — several hundred darts — the face must stay
    /// within one dart's reach of where it started. Before the fix this walked away without limit.
    ///
    /// A dart is a whole-face move of up to EyeDartMaxDistance pixels in each axis, and looking down turns
    /// the eyes inward by at most 2 px (LookAt, 0x00584158); those are the only positions it touches.
    /// </summary>
    [Fact]
    public void FacePositionStaysBoundedAcrossHundredsOfDarts()
    {
        var (sent, start, idle, robot) = RunIdle(240_000);
        using (robot)
        {
            Assert.True(idle.ActionCount > 200,
                $"only {idle.ActionCount} idle actions occurred; the test needs hundreds of darts");

            var p = IdleParameters.Default;
            float reach = (float)p.EyeDartMaxDistancePix + 0.001f;
            float startLeft = start.Left[(int)EyeParam.EyeCenterX];
            float startRight = start.Right[(int)EyeParam.EyeCenterX];

            foreach (var pose in sent)
            {
                Assert.InRange(pose.FaceCenterX, start.FaceCenterX - reach, start.FaceCenterX + reach);
                Assert.InRange(pose.FaceCenterY, start.FaceCenterY - reach, start.FaceCenterY + reach);
                Assert.InRange(pose.Left[(int)EyeParam.EyeCenterX], startLeft, startLeft + 2.001f);
                Assert.InRange(pose.Right[(int)EyeParam.EyeCenterX], startRight - 2.001f, startRight);
                Assert.Equal(start.Left[(int)EyeParam.EyeCenterY], pose.Left[(int)EyeParam.EyeCenterY]);
            }
        }
    }

    /// <summary>
    /// Scale must stay within what one blink or one dart can do to the base face, not grow by
    /// EyeDartOuterEyeScaleIncrease every time. This is what made the eyes merge into a rectangle on
    /// hardware.
    ///
    /// The bounds are the engine's: a blink multiplies EyeScaleX by at most 5.0 and EyeScaleY by as little
    /// as 0.05 (the closed frame of the table at 0x00C5AAD8); a dart multiplies EyeScaleY by at most
    /// 1.1 x 1.1 = 1.21 (looking fully up, on the near eye) and never touches EyeScaleX.
    /// </summary>
    [Fact]
    public void EyeScalesStayBoundedAndDoNotGrowCumulatively()
    {
        var (sent, start, _, robot) = RunIdle(240_000);
        using (robot)
        {
            float baseX = Math.Max(start.Left[(int)EyeParam.EyeScaleX], start.Right[(int)EyeParam.EyeScaleX]);
            float baseY = Math.Max(start.Left[(int)EyeParam.EyeScaleY], start.Right[(int)EyeParam.EyeScaleY]);
            float ceilingX = baseX * 5.0f + 0.001f;
            float ceilingY = baseY * 1.1f * 1.1f + 0.001f;

            Assert.True(Max(sent, EyeParam.EyeScaleX) <= ceilingX,
                $"EyeScaleX reached {Max(sent, EyeParam.EyeScaleX)}, above the blink's 5.0 x base");
            Assert.True(Max(sent, EyeParam.EyeScaleY) <= ceilingY,
                $"EyeScaleY reached {Max(sent, EyeParam.EyeScaleY)}, above the dart's 1.21 x base");
            float smallestBaseX = Math.Min(start.Left[(int)EyeParam.EyeScaleX], start.Right[(int)EyeParam.EyeScaleX]);
            Assert.True(Min(sent, EyeParam.EyeScaleX) >= smallestBaseX - 0.001f, "nothing in idle shrinks EyeScaleX");
            Assert.True(Min(sent, EyeParam.EyeScaleY) >= 0f);

            // No accumulation: the face keeps coming back to exactly the base pose in the second half of
            // the run as often as in the first, rather than settling somewhere it has drifted to.
            int half = sent.Count / 2;
            bool AtBase(ProceduralFacePose x) =>
                Math.Abs(x.Left[(int)EyeParam.EyeScaleX] - start.Left[(int)EyeParam.EyeScaleX]) < 1e-4f &&
                Math.Abs(x.Left[(int)EyeParam.EyeScaleY] - start.Left[(int)EyeParam.EyeScaleY]) < 1e-4f &&
                Math.Abs(x.FaceCenterX - start.FaceCenterX) < 1e-4f && Math.Abs(x.FaceCenterY - start.FaceCenterY) < 1e-4f;
            int firstHalf = sent.Take(half).Count(AtBase), secondHalf = sent.Skip(half).Count(AtBase);
            Assert.True(firstHalf > half / 4, $"the face was at base in only {firstHalf} of {half} first-half samples");
            Assert.True(secondHalf > half / 4, $"the face was at base in only {secondHalf} of {half} second-half samples");
        }
    }

    /// <summary>
    /// After the transient motion has expired, the face on screen must be the pose idle started from, not
    /// a mutated one. This is the "base pose is not corrupted" property.
    ///
    /// Idle keeps acting while it is observed, so the check is made at every instant that is more than one
    /// full transient (a 331 ms blink, or a dart of up to 200 ms) after the last action: at each of those
    /// the face must be exactly the base. Dart spacing runs 250-1000 ms, so such instants are plentiful.
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

            double lastActionAt = double.NegativeInfinity;
            idle.Acted += e => { if (e.Suppressed is null) lastActionAt = e.AtMs; };
            double quiet = IdleBehavior.BlinkTotalMs + 40;

            int checkedInstants = 0;
            for (double t = 0; t < 60_000; t += 20)
            {
                idle.Advance(t);
                if (t < 5_000 || t - lastActionAt <= quiet) continue;
                checkedInstants++;
                var now = robot.Face.Current;
                for (int i = 0; i < Eye.ParamCount; i++)
                {
                    Assert.Equal(start.Left[i], now.Left[i], 3);
                    Assert.Equal(start.Right[i], now.Right[i], 3);
                }
                Assert.Equal(start.FaceCenterX, now.FaceCenterX, 3);
                Assert.Equal(start.FaceCenterY, now.FaceCenterY, 3);
            }
            Assert.True(checkedInstants > 100, $"only {checkedInstants} quiet instants were checked");
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

            // A blink starts from the base pose: at the instant it begins (before its first 33 ms frame)
            // the multipliers are still 1, so the face is exactly where it started.
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

            // Immediately after, the face is shifted: LookAt always rescales EyeScaleY (0.975 x looking
            // level), so that is the parameter that reliably changes whatever the dart's direction.
            Assert.NotEqual(start.Left[(int)EyeParam.EyeScaleY], robot.Face.Current.Left[(int)EyeParam.EyeScaleY]);

            // Past the duration, it is back.
            for (double t = dartAt!.Value; t <= dartAt.Value + dartFor + 60; t += 10) idle.Advance(t);
            Assert.Equal(start.Left[(int)EyeParam.EyeScaleY], robot.Face.Current.Left[(int)EyeParam.EyeScaleY], 3);
            Assert.Equal(start.FaceCenterX, robot.Face.Current.FaceCenterX, 3);
            Assert.Equal(start.FaceCenterY, robot.Face.Current.FaceCenterY, 3);
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
            Assert.InRange(robot.Face.Current.FaceCenterX, newBase.FaceCenterX - allowance, newBase.FaceCenterX + allowance);
            Assert.InRange(robot.Face.Current.Left[(int)EyeParam.EyeCenterX],
                           newBase.Left[(int)EyeParam.EyeCenterX],
                           newBase.Left[(int)EyeParam.EyeCenterX] + 2.001f);
        }
    }
}
