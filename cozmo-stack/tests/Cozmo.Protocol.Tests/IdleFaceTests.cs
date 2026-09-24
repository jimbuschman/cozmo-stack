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
        robot.Transport.OfflineAcceptConnection();
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

            // No accumulation. The gaze does not return to centre between darts - the dart layer is
            // persistent, so each dart ramps from the last one and holds - but it is always the base
            // face plus exactly one shift, so the face never wanders further than one dart's reach
            // (EyeDartMaxDistance, 6 pixels) from where idle started, in the last minute of a four
            // minute run as much as in the first.
            float reach = (float)IdleParameters.Default.EyeDartMaxDistancePix + 0.001f;
            foreach (var x in sent)
            {
                Assert.InRange(x.FaceCenterX, start.FaceCenterX - reach, start.FaceCenterX + reach);
                Assert.InRange(x.FaceCenterY, start.FaceCenterY - reach, start.FaceCenterY + reach);
            }
        }
    }

    /// <summary>
    /// The base pose is never corrupted: whatever idle has been doing, what is on the screen is that
    /// base with the layers of the moment composed onto it, and nothing more.
    ///
    /// The engine's eye-dart layer is persistent - it is added with <c>AddToPersistentLayer</c>, and
    /// <c>ApplyLayersToFrame</c> at 0x0058E644 queues only non-persistent layers for removal when they
    /// run out (0x0058E6A0), rewinding and trimming a persistent one instead - so the gaze does not come
    /// back to centre between darts. What must still hold, and is the drift regression, is that once the
    /// blink is over the face is the base shifted by exactly one gaze, never by a sum of them.
    /// </summary>
    [Fact]
    public void TheFaceIsAlwaysTheBasePlusOneGazeAndNeverASumOfThem()
    {
        var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        using (robot)
        {
            var start = robot.Face.Current.Clone();
            var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
            var idle = new IdleBehavior(robot, arbiter, random: new Random(2)) { ExecuteMotors = false };

            // quiet means: no blink up, and no dart still ramping
            double busyUntil = double.NegativeInfinity;
            idle.Acted += e =>
            {
                if (e.Suppressed is not null) return;
                if (e.Action == IdleAction.Blink) busyUntil = Math.Max(busyUntil, e.AtMs + IdleBehavior.BlinkTotalMs);
                if (e.Action == IdleAction.EyeDart)
                    busyUntil = Math.Max(busyUntil, e.AtMs + e.DurationMs + IdleBehavior.AnimationFrameMs);
            };
            float reach = (float)idle.Parameters.EyeDartMaxDistancePix + 0.001f;

            int checkedInstants = 0;
            for (double t = 0; t < 60_000; t += 20)
            {
                idle.Advance(t);
                if (t < 5_000 || t <= busyUntil + 40) continue;
                checkedInstants++;
                var now = robot.Face.Current;

                // the gaze the face is holding, and nothing beyond it
                float dx = now.FaceCenterX - start.FaceCenterX;
                float dy = now.FaceCenterY - start.FaceCenterY;
                Assert.InRange(dx, -reach, reach);
                Assert.InRange(dy, -reach, reach);

                // no blink is up, so the eye widths are untouched and the heights carry exactly the one
                // vertical factor LookAt produces for that gaze
                Assert.Equal(start.Left[(int)EyeParam.EyeScaleX], now.Left[(int)EyeParam.EyeScaleX], 3);
                var expected = IdleBehavior.LookAt(start.Clone(), dx, dy, 5f, 5f,
                                                   (float)idle.Parameters.EyeDartUpMaxScale,
                                                   (float)idle.Parameters.EyeDartDownMinScale,
                                                   (float)idle.Parameters.EyeDartOuterEyeScaleIncrease);
                Assert.Equal(expected.Left[(int)EyeParam.EyeScaleY], now.Left[(int)EyeParam.EyeScaleY], 3);
                Assert.Equal(expected.Right[(int)EyeParam.EyeScaleY], now.Right[(int)EyeParam.EyeScaleY], 3);
                Assert.Equal(expected.Left[(int)EyeParam.EyeCenterX], now.Left[(int)EyeParam.EyeCenterX], 3);
            }
            Assert.True(checkedInstants > 100, $"only {checkedInstants} quiet instants were checked");
        }
    }

    /// <summary>
    /// A blink must be measured from the stable face, not from whatever a dart left behind. Before the
    /// fix a blink captured the mutated pose and put that back, cementing the drift.
    ///
    /// A dart layer may well be up when a blink starts - the engine composes them, and every keep-alive
    /// timer starts at zero so the first tick raises both - so what is asserted is that the face at the
    /// start of a blink is the base plus at most one dart, not the base plus a hundred of them:
    /// EyeScaleX, which only a blink ever touches and which is still 1x at a blink's first instant, is
    /// exactly where it started, and EyeCenterX is inside the +/-2 pixels a single dart's convergence can
    /// reach.
    /// </summary>
    [Fact]
    public void ABlinkRestoresTheStableFaceRatherThanAMutatedDartPose()
    {
        var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
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
                Assert.InRange(blink.Left[(int)EyeParam.EyeCenterX],
                               start.Left[(int)EyeParam.EyeCenterX] - 2.001f,
                               start.Left[(int)EyeParam.EyeCenterX] + 2.001f);
                Assert.InRange(blink.Right[(int)EyeParam.EyeCenterX],
                               start.Right[(int)EyeParam.EyeCenterX] - 2.001f,
                               start.Right[(int)EyeParam.EyeCenterX] + 2.001f);
                Assert.Equal(start.Left[(int)EyeParam.EyeScaleX], blink.Left[(int)EyeParam.EyeScaleX], 3);
            }
        }
    }

    /// <summary>
    /// A dart ramps to its gaze over the duration it was given plus one frame, and then holds it.
    ///
    /// The drawn value is not how long the shifted gaze lasts: <c>AddToPersistentLayer</c> at 0x0058EAA0
    /// gives the new keyframe a trigger time of <c>lastKeyFrameTime + drawn + 0x21</c>, and
    /// <c>GetFaceHelper</c> at 0x0058CD80 interpolates from the keyframe before it
    /// (<c>GetInterpolatedFace</c>, 0x004F99E6) until that time is reached. After it, with no keyframe
    /// beyond, the layer applies its last face unchanged - for as long as the layer lives, which for a
    /// persistent layer is until something removes it.
    /// </summary>
    [Fact]
    public void ADartRampsToItsGazeOverItsDurationAndThenHoldsIt()
    {
        var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        using (robot)
        {
            var start = robot.Face.Current.Clone();
            var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
            // blinking parked and the darts well apart, so each ramp finishes long before the next
            var quiet = IdleParameters.Default with
            {
                BlinkSpacingMinMs = 600_000,
                BlinkSpacingMaxMs = 600_000,
                EyeDartSpacingMinMs = 1_500,
                EyeDartSpacingMaxMs = 1_500,
            };
            var idle = new IdleBehavior(robot, arbiter, quiet, new Random(1)) { ExecuteMotors = false };

            var darts = new List<IdleEvent>();
            idle.Acted += e => { if (e.Action == IdleAction.EyeDart && e.Suppressed is null) darts.Add(e); };

            var samples = new List<(double T, float X)>();
            for (double t = 0; t < 20_000; t += 10)
            {
                idle.Advance(t);
                samples.Add((t, robot.Face.Current.FaceCenterX));
            }
            float At(double t) => samples.Last(s => s.T <= t).X;

            // a pair of consecutive darts that look somewhere clearly different
            int i = Enumerable.Range(1, darts.Count - 1)
                              .First(k => Math.Abs(darts[k].Amount - darts[k - 1].Amount) >= 4
                                          && darts[k].AtMs > IdleBehavior.BlinkTotalMs);
            var prev = darts[i - 1];
            var dart = darts[i];
            double ramp = dart.DurationMs + IdleBehavior.AnimationFrameMs;

            // before it, the face is holding the gaze the last dart reached - not back at centre
            Assert.Equal(start.FaceCenterX + (float)prev.Amount, At(dart.AtMs - 10), 3);

            // during the ramp it is on its way, strictly between the two
            float lo = Math.Min((float)prev.Amount, (float)dart.Amount);
            float hi = Math.Max((float)prev.Amount, (float)dart.Amount);
            float mid = At(dart.AtMs + ramp / 2) - start.FaceCenterX;
            Assert.InRange(mid, lo, hi);
            Assert.NotEqual(lo, mid, 3);
            Assert.NotEqual(hi, mid, 3);

            // and once the ramp is done it holds there, for as long as nothing else darts
            Assert.Equal(start.FaceCenterX + (float)dart.Amount, At(dart.AtMs + ramp + 20), 3);
            Assert.Equal(start.FaceCenterX + (float)dart.Amount, At(dart.AtMs + 1_000), 3);
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
        robot.Transport.OfflineAcceptConnection();
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
