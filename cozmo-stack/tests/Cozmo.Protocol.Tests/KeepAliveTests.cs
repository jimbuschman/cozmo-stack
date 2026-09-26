using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The keep-alive as the engine runs it: a 60 ms tick, whole-millisecond draws, timers that keep
/// counting while they are held off, and head, lift and body as keyframes of the live animation rather
/// than motor commands (fidelity manifest M7-008, M7-009, M7-010).
/// </summary>
public class KeepAliveTests
{
    /// <summary>Records what the live animation actually streams.</summary>
    private sealed class Recorder : IAnimationSink
    {
        public readonly List<(sbyte Deg, uint Dur)> Heads = new();
        public readonly List<(byte Mm, uint Dur)> Lifts = new();
        public readonly List<BodyKeyframe> Bodies = new();
        public int BodyStops;

        public void Face(FaceBitmap bitmap) { }
        public void Audio(byte[]? mulawFrame) { }
        public void Head(sbyte angleDeg, uint durationMs) => Heads.Add((angleDeg, durationMs));
        public void Lift(byte heightMm, uint durationMs) => Lifts.Add((heightMm, durationMs));
        public void Body(BodyKeyframe k) => Bodies.Add(k);
        public void AnimationStarted(byte tag) { }
        public void AnimationEnded() { }
        public void BodyStop() => BodyStops++;
        public void Lights(LightsKeyframe k) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }

    // ---------------------------------------------------------------- M7-008: the tick

    /// <summary>
    /// The timers move in 60 ms steps and nothing else. <c>CozmoInstanceRunner::Run</c> at 0x0065B3A8
    /// deadlines each iteration at now + 0x03938700 ns, and every idle countdown is decremented by
    /// exactly 60 per iteration, so an idle event can only land on a tick boundary.
    /// </summary>
    [Fact]
    public void EveryIdleEventLandsOnAnEngineTick()
    {
        Assert.Equal(60, IdleBehavior.EngineTickMs);

        using var robot = SimulatedFacePacing.Use(CozmoRobot.CreateOffline());
        robot.Transport.OfflineAcceptConnection();
        var idle = new IdleBehavior(robot, new BehaviorArbiter { AutonomyEnabled = true },
                                    random: new Random(7)) { Execute = false };
        var seen = new List<IdleEvent>();
        idle.Acted += seen.Add;

        // advanced at 10 ms, which is not a multiple of the tick
        for (double t = 0; t < 20_000; t += 10) idle.Advance(t);

        Assert.NotEmpty(seen);
        foreach (var e in seen)
            Assert.Equal(0, e.AtMs % IdleBehavior.EngineTickMs);
    }

    /// <summary>
    /// Every spacing and duration is a whole number of milliseconds: the engine draws them with
    /// <c>RandomGenerator::RandIntInRange</c>, casting the float tunable to int first.
    /// </summary>
    [Fact]
    public void EverySpacingAndDurationIsAWholeNumberOfMilliseconds()
    {
        using var robot = SimulatedFacePacing.Use(CozmoRobot.CreateOffline());
        robot.Transport.OfflineAcceptConnection();
        var idle = new IdleBehavior(robot, new BehaviorArbiter { AutonomyEnabled = true },
                                    random: new Random(11)) { Execute = false };
        var seen = new List<IdleEvent>();
        idle.Acted += seen.Add;
        for (double t = 0; t < 30_000; t += 20) idle.Advance(t);

        var moves = seen.Where(e => e.Suppressed is null
                                    && e.Action is IdleAction.HeadMove or IdleAction.LiftMove
                                                or IdleAction.BodyMove or IdleAction.EyeDart).ToList();
        Assert.NotEmpty(moves);
        foreach (var e in moves)
            Assert.Equal(Math.Round(e.DurationMs), e.DurationMs);

        // and a body shuffle's speed is a whole mm/s in [-10, 10], drawn by RandIntInRange(-p7, p7)
        foreach (var e in moves.Where(e => e.Action == IdleAction.BodyMove))
        {
            Assert.Equal(Math.Round(e.Amount), e.Amount);
            Assert.InRange(e.Amount, -10, 10);
        }
    }

    /// <summary>
    /// A timer that could not fire is not rescheduled. The engine decrements the countdown and leaves it,
    /// so the movement happens on the first tick the track comes free; this stack used to reschedule as
    /// though it had fired, which silently dropped everything that came due during a caller animation.
    /// </summary>
    [Fact]
    public void AMovementHeldOffByAnOwnedTrackHappensAsSoonAsTheTrackIsFree()
    {
        using var robot = SimulatedFacePacing.Use(CozmoRobot.CreateOffline());
        robot.Transport.OfflineAcceptConnection();
        var clip = new AnimationClip
        {
            Name = "holds-the-head",
            Keyframes = new List<Keyframe> { new HeadKeyframe(0, 60_000, 0, 0) },
            Tracks = AnimationTrack.Head,
            DurationMs = 60_000,
        };
        var idle = new IdleBehavior(robot, new BehaviorArbiter { AutonomyEnabled = true },
                                    random: new Random(3)) { Execute = false };
        var seen = new List<IdleEvent>();
        idle.Acted += seen.Add;

        robot.Animations.Scheduler.Play(clip, 0);
        for (double t = 0; t < 6_000; t += 20) idle.Advance(t);

        Assert.Contains(seen, e => e.Action == IdleAction.HeadMove && e.Suppressed == "the head track is owned");
        Assert.DoesNotContain(seen, e => e.Action == IdleAction.HeadMove && e.Suppressed is null);

        robot.Animations.Scheduler.Stop();
        seen.Clear();
        idle.Advance(6_020);
        Assert.Contains(seen, e => e.Action == IdleAction.HeadMove && e.Suppressed is null);
    }

    /// <summary>
    /// A movement is followed by its own duration and then the gap: the countdown is set to the
    /// movement's duration when it starts (0x0057D6F2 for the body) and the gap is drawn separately
    /// (0x0057D978), and the next one is due when countdown + gap runs out.
    /// </summary>
    [Fact]
    public void TheNextMovementComesADurationAndAGapLater()
    {
        using var robot = SimulatedFacePacing.Use(CozmoRobot.CreateOffline());
        robot.Transport.OfflineAcceptConnection();
        // one duration and one gap, so the cadence is arithmetic rather than a distribution
        var fixedTimes = IdleParameters.Default with
        {
            LiftMovementDurationMinMs = 300,
            LiftMovementDurationMaxMs = 300,
            LiftMovementSpacingMinMs = 600,
            LiftMovementSpacingMaxMs = 600,
        };
        var idle = new IdleBehavior(robot, new BehaviorArbiter { AutonomyEnabled = true },
                                    fixedTimes, new Random(4)) { Execute = false };
        var lifts = new List<double>();
        idle.Acted += e => { if (e.Action == IdleAction.LiftMove && e.Suppressed is null) lifts.Add(e.AtMs); };

        for (double t = 0; t < 10_000; t += 20) idle.Advance(t);

        Assert.True(lifts.Count >= 5, $"only {lifts.Count} lift movements");
        for (int i = 1; i < lifts.Count; i++)
        {
            // 300 + 600 = 900, rounded up to the tick the sum actually crosses zero on
            double gap = lifts[i] - lifts[i - 1];
            Assert.InRange(gap, 900, 900 + IdleBehavior.EngineTickMs);
        }
    }

    // ---------------------------------------------------------------- M7-009: head and lift

    /// <summary>
    /// Head and lift go out as animation keyframes of the live animation, not as SetHeadAngle and
    /// SetLiftHeight motor commands: <c>UpdateLiveAnimation</c> builds
    /// <c>HeadAngleKeyFrame(currentAngle, variability, duration)</c> at 0x0057D85C and
    /// <c>LiftHeightKeyFrame(35, 8, duration)</c> at 0x0057D9C0 and appends them to the streamer's own
    /// live <c>Animation</c>. The movement comes from the variability, which is drawn at stream time.
    /// </summary>
    [Fact]
    public void TheHeadAndLiftMoveAsKeyframesOfTheLiveAnimation()
    {
        var sink = new Recorder();
        var scheduler = new AnimationScheduler(sink, new Random(2));

        scheduler.StreamLive(new HeadKeyframe(0, 250, 12, 6), 0);
        scheduler.StreamLive(new LiftKeyframe(0, 120, 35, 8), 0);
        // M5 A29: the keyframes go out in the streamer's Updates (the first re-inits the live idle, the next streams it)
        scheduler.Advance(0);
        scheduler.Advance(60);

        var (deg, headDur) = Assert.Single(sink.Heads);
        Assert.Equal(250u, headDur);
        Assert.InRange(deg, 12 - 6, 12 + 6);          // the variability is applied at stream time

        var (mm, liftDur) = Assert.Single(sink.Lifts);
        Assert.Equal(120u, liftDur);
        Assert.InRange(mm, 35 - 8, 35 + 8);
    }

    /// <summary>
    /// And the keep-alive really uses that path: the angle it sends is the head angle the robot is
    /// already at, so a still robot gets a keyframe of its own current angle with the variability doing
    /// the moving.
    /// </summary>
    [Fact]
    public void TheKeepAliveSendsTheHeadAngleTheRobotIsAlreadyAt()
    {
        using var robot = SimulatedFacePacing.Use(CozmoRobot.CreateOffline());
        robot.Transport.OfflineAcceptConnection();
        var idle = new IdleBehavior(robot, new BehaviorArbiter { AutonomyEnabled = true },
                                    random: new Random(9));
        var heads = new List<double>();
        idle.Acted += e => { if (e.Action == IdleAction.HeadMove && e.Suppressed is null) heads.Add(e.Amount); };

        for (double t = 0; t < 10_000; t += 20) idle.Advance(t);

        Assert.NotEmpty(heads);
        // an offline robot reports no head angle, so the keyframe carries 0 degrees every time: the
        // keep-alive never invents an angle of its own
        Assert.All(heads, a => Assert.Equal(0, a));
    }

    /// <summary>A live keyframe is refused while a clip streams, whatever its track: the streamer reaches the live
    /// animation only in the no-animation path (M5 A28, A29).</summary>
    [Fact]
    public void ALiveKeyframeIsRefusedWhileAClipStreams()
    {
        var sink = new Recorder();
        var scheduler = new AnimationScheduler(sink, new Random(2));
        var clip = new AnimationClip
        {
            Name = "owns-the-head",
            Keyframes = new List<Keyframe> { new HeadKeyframe(0, 5_000, 0, 0) },
            Tracks = AnimationTrack.Head,
            DurationMs = 5_000,
        };
        scheduler.Play(clip, 0);

        Assert.False(scheduler.StreamLive(new HeadKeyframe(0, 250, 12, 6), 0));
        Assert.False(scheduler.StreamLive(new LiftKeyframe(0, 120, 35, 8), 0));   // a different track: refused all the same
    }

    // ---------------------------------------------------------------- M7-010: the body shuffle

    /// <summary>
    /// The shuffle is driven, and it is a <c>BodyMotionKeyFrame</c>: straight carries the engine's
    /// STRAIGHT radius 0x7FFF (0x0057D8DA) and a turn carries 0 (0x0057D80E), with the speed a whole
    /// mm/s in [-10, 10] and the duration in [250, 1500].
    /// </summary>
    [Fact]
    public void TheBodyShuffleIsDrivenAsAStraightOrATurnInPlace()
    {
        using var robot = SimulatedFacePacing.Use(CozmoRobot.CreateOffline());
        robot.Transport.OfflineAcceptConnection();
        var idle = new IdleBehavior(robot, new BehaviorArbiter { AutonomyEnabled = true },
                                    random: new Random(6));
        var body = new List<IdleEvent>();
        idle.Acted += e => { if (e.Action == IdleAction.BodyMove && e.Suppressed is null) body.Add(e); };

        for (double t = 0; t < 60_000; t += 20) idle.Advance(t);

        Assert.True(body.Count >= 20, $"only {body.Count} shuffles");
        foreach (var e in body)
        {
            Assert.InRange(e.DurationMs, 250, 1500);
            Assert.InRange(e.Amount, -10, 10);
            Assert.Contains(e.BodyRadius, new[] { IdleBehavior.StraightToken, IdleBehavior.TurnInPlaceToken });
        }
        // BodyMovementStraightFraction is 0.5, so both kinds turn up
        Assert.Contains(body, e => e.BodyRadius == IdleBehavior.StraightToken);
        Assert.Contains(body, e => e.BodyRadius == IdleBehavior.TurnInPlaceToken);
    }

    /// <summary>The two radius tokens are the two 16-bit values the engine puts on the wire.</summary>
    [Fact]
    public void TheShuffleTokensEncodeToTheEnginesRadiusValues()
    {
        Assert.Equal((short)0x7FFF,
                     new BodyKeyframe(0, 250, IdleBehavior.StraightToken, 5).EncodedRadius);
        Assert.Equal((short)0,
                     new BodyKeyframe(0, 250, IdleBehavior.TurnInPlaceToken, 5).EncodedRadius);
    }

    /// <summary>
    /// A live body keyframe is stopped by its own stop message (M5 C5): on the first frame of the live animation with its
    /// counter at the duration, 500 ms of frames after it started, and only once.
    /// </summary>
    [Fact]
    public void ALiveBodyKeyframeStopsWhenItsDurationRunsOut()
    {
        var sink = new Recorder();
        var scheduler = new AnimationScheduler(sink, new Random(2));

        scheduler.StreamLive(new BodyKeyframe(0, 500, IdleBehavior.TurnInPlaceToken, 10), 1_000);
        scheduler.Advance(1_000);                 // InitStream(live, 0xFF)
        scheduler.Advance(1_033);                 // the first live frame: the body starts
        Assert.Single(sink.Bodies);

        for (int i = 2; i <= 15; i++) scheduler.Advance(1_000 + 33 * i);   // frames at 33 .. 462 of stream time
        Assert.Equal(0, sink.BodyStops);
        scheduler.Advance(1_000 + 33 * 16);       // the frame at 495: counter 495 < 500, still running
        scheduler.Advance(1_000 + 33 * 17);       // the frame at 528: counter 528 ≥ 500, the stop
        Assert.Equal(1, sink.BodyStops);
        for (int i = 18; i <= 30; i++) scheduler.Advance(1_000 + 33 * i);
        Assert.Equal(1, sink.BodyStops);          // and only once
    }

    /// <summary>
    /// A turn carries an eye shift and a straight takes it away again: the turn branch calls
    /// <c>AddOrUpdateEyeShift</c> with the <c>LiveIdleTurn</c> layer at 0x0057D7FC and the straight one
    /// calls <c>RemoveEyeShift</c> at 0x0057D8D2. Its constants are the call's own - a 33 ms shift over
    /// 64 x 32 with 1.1 / 0.85 / 0.1 - not the eye-dart tunables.
    /// </summary>
    [Fact]
    public void ATurnLeadsWithTheEyesAndAStraightSettlesThemAgain()
    {
        Assert.Equal(21, IdleBehavior.TurnShiftMaxXPix);
        Assert.Equal(10, IdleBehavior.TurnShiftMaxYPix);
        Assert.Equal(33u, IdleBehavior.TurnShiftDurationMs);

        using var robot = SimulatedFacePacing.Use(CozmoRobot.CreateOffline());
        robot.Transport.OfflineAcceptConnection();
        // nothing else touching the face, so what moves it is the turn, and the turns well apart so
        // each one's shift has expired before the next begins
        var quiet = IdleParameters.Default with
        {
            BlinkSpacingMinMs = 600_000,
            BlinkSpacingMaxMs = 600_000,
            EyeDartMaxDistancePix = 0,          // the engine skips the dart entirely at zero distance
            BodyMovementStraightFraction = 0,   // every shuffle is a turn
            BodyMovementSpacingMinMs = 3_000,
            BodyMovementSpacingMaxMs = 3_000,
        };
        var start = robot.Face.Current.Clone();
        var idle = new IdleBehavior(robot, new BehaviorArbiter { AutonomyEnabled = true },
                                    quiet, new Random(8));

        var turns = new List<double>();
        idle.Acted += e => { if (e.Action == IdleAction.BodyMove && e.Suppressed is null) turns.Add(e.AtMs); };

        bool shiftedAtLeastOnce = false;
        for (double t = 0; t < 40_000; t += 20)
        {
            int before = turns.Count;
            idle.Advance(t);
            if (turns.Count > before
                && Math.Abs(robot.Face.Current.FaceCenterX - start.FaceCenterX) > 0.5f)
                shiftedAtLeastOnce = true;
        }

        Assert.True(turns.Count >= 5, $"only {turns.Count} turns");
        Assert.True(shiftedAtLeastOnce, "no turn ever moved the eyes");

        // Two ticks past the last turn - the shift lasts 33 ms and the next turn is seconds away - the
        // face is exactly the one idle started from: the shift is a layer, not an edit of the base.
        idle.Advance(turns[^1] + 2 * IdleBehavior.EngineTickMs);
        Assert.Equal(start.FaceCenterX, robot.Face.Current.FaceCenterX, 3);
        Assert.Equal(start.FaceCenterY, robot.Face.Current.FaceCenterY, 3);
    }
}
