using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M10: the engine's derived robot state and the reactions built on it. The classifier and detector tests
/// pin the thresholds, filters and debounce read from <c>Robot::CheckAndUpdateTreadsState</c>,
/// <c>Robot::UpdateFullRobotState</c> and <c>MovementComponent::CheckForUnexpectedMovement</c>; the strategy
/// and behaviour tests pin the transcribed reaction logic; the whole-config tests check every reaction this
/// stack claims against the shipped map and assets.
/// </summary>
public class DerivedStateTests
{
    // ------------------------------------------------------------------ helpers

    private static RobotState State(uint t, float ax = 0, float ay = 0, float az = 9800, float pitch = 0,
                                    RobotStatusFlag flags = 0, float gx = 0, float gy = 0, float gz = 0,
                                    float left = 0, float right = 0, ushort cliff0 = 0)
    {
        var s = new RobotState
        {
            Timestamp = t,
            Status = (uint)flags,
            Accel = new AccelData { X = ax, Y = ay, Z = az },
            Gyro = new GyroData { X = gx, Y = gy, Z = gz },
            Pose = new RobotPose { Pitch = pitch },
            LwheelSpeedMmps = left,
            RwheelSpeedMmps = right,
        };
        s.CliffDataRaw[0] = cliff0;
        return s;
    }

    /// <summary>A classifier already past its head-calibration gate, on a physical robot.</summary>
    private static OffTreadsClassifier Classifier() => new() { HeadCalibrated = true };

    /// <summary>Feeds level, at-rest states for a while so the accelerometer filters settle.</summary>
    private static uint Settle(OffTreadsClassifier c, uint t, float ay = 0, float az = 9800, float pitch = 0,
                               RobotStatusFlag flags = 0, int ticks = 120)
    {
        for (int i = 0; i < ticks; i++, t += 33) c.Update(State(t, ay: ay, az: az, pitch: pitch, flags: flags), t);
        return t;
    }

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

    /// <summary>Feeds an offline robot over the framed transport, as a real one would.</summary>
    private sealed class Rig : IDisposable
    {
        public readonly CozmoRobot Robot = CozmoRobot.CreateOffline();
        private ushort _seq = 1;
        public uint T = 1000;

        public Rig() => Deliver(new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), _seq++));

        public void Send(RobotMessage m) =>
            Deliver(new SubMessage(ReliableMessageType.SingleReliableMessage, m.ToBytes(), _seq++));

        /// <summary>The robot's own head-calibration report, which is what enables the classifier.</summary>
        public void CalibrateMotors()
        {
            Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false, AutoStarted = false });
            Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false, AutoStarted = false });
        }

        /// <summary>Streams states for a number of 33 ms ticks.</summary>
        public void Stream(int ticks, float ax = 0, float ay = 0, float az = 9800, float pitch = 0,
                           RobotStatusFlag flags = 0, float gz = 0, float left = 0, float right = 0)
        {
            for (int i = 0; i < ticks; i++, T += 33)
                Send(State(T, ax, ay, az, pitch, flags, gz: gz, left: left, right: right));
        }

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

        public BehaviorContext Context(string? obb = null, MoodState? mood = null)
        {
            if (obb is not null && Robot.Animations.Library is null)
                Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
            return new BehaviorContext
            {
                Robot = Robot,
                Triggers = obb is not null ? AnimationTriggerMap.Load(obb) : new AnimationTriggerMap(),
                Random = new Random(3),
                Mood = mood,
            };
        }

        public void Dispose() => Robot.Dispose();
    }

    // ------------------------------------------------------------------ the IMU filters

    /// <summary>
    /// UpdateFullRobotState: per axis new = 0.9·old + 0.1·sample (0x3F666666, 0x3DCCCCD0); magnitude
    /// new = 0.95·old + 0.05·|sample| (0x3F733333, 0x3D4CCCD0). The first state from rest gives exactly
    /// one step of each.
    /// </summary>
    [Fact]
    public void TheAccelerometerFiltersMatchTheEngineConstants()
    {
        var c = Classifier();
        c.Update(State(0, az: 9800), 0);
        Assert.Equal(980f, c.FilteredAccel.Z, 1e-3);
        Assert.Equal(490f, c.FilteredAccelMagnitude, 1e-3);
        Assert.Equal(9800f, c.RawAccelMagnitude, 1e-3);
        c.Update(State(33, az: 9800), 33);
        Assert.Equal(0.9f * 980f + 980f, c.FilteredAccel.Z, 1e-2);
        Assert.Equal(0.95f * 490f + 490f, c.FilteredAccelMagnitude, 1e-2);
    }

    /// <summary>The filters run whether or not the head is calibrated; only the classification waits.</summary>
    [Fact]
    public void TheClassifierWaitsForTheHeadCalibrationGate()
    {
        var c = new OffTreadsClassifier { HeadCalibrated = false };
        uint t = Settle(c, 0);
        Assert.True(c.FilteredAccelMagnitude > 9000);
        c.Update(State(t, flags: RobotStatusFlag.IsPickedUp), t);
        Assert.Equal(OffTreadsState.OnTreads, c.Current);

        c.HeadCalibrated = true;
        c.Update(State(t + 33, flags: RobotStatusFlag.IsPickedUp), t + 33);
        Assert.Equal(OffTreadsState.InAir, c.Current);
    }

    // ------------------------------------------------------------------ the classifier

    [Fact]
    public void AnUprightRobotAtRestStaysOnTreads()
    {
        var c = Classifier();
        int changes = 0;
        c.StateChanged += (_, _) => changes++;
        Settle(c, 0);
        Assert.Equal(OffTreadsState.OnTreads, c.Current);
        Assert.Equal(0, changes);
    }

    /// <summary>
    /// 0x00512244..0x00512258: a picked-up robot whose candidate was OnTreads gets candidate InAir with its
    /// time set to now − 250, so the 250 ms debounce is already satisfied: the change is immediate. This is
    /// the state the engine's RobotPickedUp reaction fires on.
    /// </summary>
    [Fact]
    public void PickingUpBecomesInAirOnTheSameState()
    {
        var c = Classifier();
        uint t = Settle(c, 0);
        var seen = new List<(OffTreadsState, OffTreadsState)>();
        c.StateChanged += (a, b) => seen.Add((a, b));
        Assert.True(c.Update(State(t, flags: RobotStatusFlag.IsPickedUp), t));
        Assert.Equal(OffTreadsState.InAir, c.Current);
        Assert.Equal(new[] { (OffTreadsState.OnTreads, OffTreadsState.InAir) }, seen);
    }

    /// <summary>
    /// 0x00511F48: with the flag clear and no orientation test holding, the candidate drops straight to
    /// OnTreads with time now − 250: putting the robot down changes the state immediately.
    /// </summary>
    [Fact]
    public void PuttingDownReturnsToTreadsImmediately()
    {
        var c = Classifier();
        uint t = Settle(c, 0);
        c.Update(State(t, flags: RobotStatusFlag.IsPickedUp), t);
        t += 33;
        Assert.True(c.Update(State(t), t));
        Assert.Equal(OffTreadsState.OnTreads, c.Current);
    }

    /// <summary>
    /// On a physical robot the on-back band is 1.30027 ± 0.261799 rad (table entry 0x005122A8), and the
    /// candidate time is set to now + 750, so the change lands 1000 ms after the pitch enters the band
    /// (0x00511FBE..0x00511FCC, then the 250 ms debounce at 0x00511FD0).
    /// </summary>
    [Fact]
    public void OnBackTakesOneSecondAndUsesThePhysicalCentre()
    {
        var c = Classifier();
        uint t = Settle(c, 0);
        // On its back the accelerometer is along X, not Y, so the on-side test does not fire.
        float pitch = (float)OffTreadsClassifier.OnBackCentrePhysicalRad;
        for (uint dt = 0; dt < 1000; dt += 33)
        {
            c.Update(State(t + dt, ax: 9800, az: 0, pitch: pitch, flags: RobotStatusFlag.IsPickedUp), t + dt);
            // The on-back branch comes before the in-air test, so the candidate is OnBack from the first
            // state and nothing else is classified while it waits.
            Assert.Equal(OffTreadsState.OnTreads, c.Current);
            Assert.Equal(OffTreadsState.OnBack, c.Candidate);
        }
        Assert.True(c.Update(State(t + 1000, ax: 9800, az: 0, pitch: pitch, flags: RobotStatusFlag.IsPickedUp), t + 1000));
        Assert.Equal(OffTreadsState.OnBack, c.Current);
        Assert.Equal(1.30027, c.OnBackCentreRad, 5);

        // 96.4 degrees is the simulator's centre, outside the physical band: not on back.
        var sim = Classifier();
        uint ts = Settle(sim, 0);
        for (uint dt = 0; dt <= 1500; dt += 33)
            sim.Update(State(ts + dt, ax: 9800, az: 0, pitch: (float)OffTreadsClassifier.OnBackCentreSimulatedRad, flags: RobotStatusFlag.IsPickedUp), ts + dt);
        Assert.NotEqual(OffTreadsState.OnBack, sim.Current);
        sim.IsPhysical = false;
        for (uint dt = 1533; dt <= 3000; dt += 33)
            sim.Update(State(ts + dt, ax: 9800, az: 0, pitch: (float)OffTreadsClassifier.OnBackCentreSimulatedRad, flags: RobotStatusFlag.IsPickedUp), ts + dt);
        Assert.Equal(OffTreadsState.OnBack, sim.Current);
    }

    /// <summary>
    /// | |accelY| − 9800 | &lt; 3000 is a side (0x00511E92..0x00511F30); the sign of the filtered Y picks
    /// right (positive) or left, and the plain 250 ms debounce applies.
    /// </summary>
    [Theory]
    [InlineData(9800f, OffTreadsState.OnRightSide)]
    [InlineData(-9800f, OffTreadsState.OnLeftSide)]
    public void LyingOnASideIsReadFromTheFilteredYAxis(float ay, OffTreadsState expected)
    {
        var c = Classifier();
        uint t = Settle(c, 0);
        // let the Y filter cross the band: 0.9^n decay from 0 towards 9800 passes 6800 after ~12 states
        uint first = 0;
        for (uint dt = 0; dt < 3000 && c.Current != expected; dt += 33)
        {
            c.Update(State(t + dt, ay: ay, az: 0, flags: RobotStatusFlag.IsPickedUp), t + dt);
            if (c.Candidate == expected && first == 0) first = t + dt;
        }
        Assert.Equal(expected, c.Current);
        Assert.True(first > 0);
        Assert.Equal(first, (uint)c.CandidateTimeMs);
    }

    /// <summary>Pitch above 110° or below −80° is on the face (0x00511E66, 0x00511EE6), debounced 250 ms.</summary>
    [Theory]
    [InlineData(2.0f)]
    [InlineData(-1.5f)]
    public void OnFaceNeedsThePitchOutsideTheFaceLimits(float pitch)
    {
        var c = Classifier();
        uint t = Settle(c, 0);
        c.Update(State(t, ax: -9800, az: 0, pitch: pitch, flags: RobotStatusFlag.IsPickedUp), t);
        Assert.Equal(OffTreadsState.OnFace, c.Candidate);
        Assert.NotEqual(OffTreadsState.OnFace, c.Current);
        c.Update(State(t + 249, ax: -9800, az: 0, pitch: pitch, flags: RobotStatusFlag.IsPickedUp), t + 249);
        Assert.NotEqual(OffTreadsState.OnFace, c.Current);
        c.Update(State(t + 250, ax: -9800, az: 0, pitch: pitch, flags: RobotStatusFlag.IsPickedUp), t + 250);
        Assert.Equal(OffTreadsState.OnFace, c.Current);
    }

    /// <summary>
    /// IS_FALLING alone is not enough: the final test at 0x00511F48 drops to OnTreads unless picked-up or an
    /// orientation test also holds. With IS_PICKED_UP the state is Falling at once, and clearing the flags
    /// raises FallingStopped with the duration from the state timestamps.
    /// </summary>
    [Fact]
    public void FallingNeedsTheFlagAndSomethingElse()
    {
        var c = Classifier();
        uint t = Settle(c, 0);
        c.Update(State(t, flags: RobotStatusFlag.IsFalling), t);
        Assert.Equal(OffTreadsState.OnTreads, c.Current);

        uint? duration = null;
        bool started = false;
        c.FallingStarted += () => started = true;
        c.FallingStopped += d => duration = d;
        c.Update(State(t + 33, flags: RobotStatusFlag.IsFalling | RobotStatusFlag.IsPickedUp), t + 33);
        Assert.Equal(OffTreadsState.Falling, c.Current);
        Assert.True(started);
        Assert.Equal(t + 33, c.FallingStartedTimestamp);

        c.Update(State(t + 533), t + 533);
        Assert.Equal(OffTreadsState.OnTreads, c.Current);
        Assert.Equal(500u, duration);
    }

    /// <summary>
    /// 0x00512228: level (|pitch| ≤ 45°) with the candidate on back, a side or face goes to InAir when still
    /// held, and straight to OnTreads when not.
    /// </summary>
    [Fact]
    public void RightingFromTheBackGoesThroughInAirOnlyWhileHeld()
    {
        var c = Classifier();
        uint t = Settle(c, 0);
        float back = (float)OffTreadsClassifier.OnBackCentrePhysicalRad;
        for (uint dt = 0; dt <= 1100; dt += 33)
            c.Update(State(t + dt, ax: 9800, az: 0, pitch: back, flags: RobotStatusFlag.IsPickedUp), t + dt);
        Assert.Equal(OffTreadsState.OnBack, c.Current);
        t += 1133;

        // levelled while still held: InAir after the debounce
        uint held = t;
        for (uint dt = 0; dt < 250; dt += 33) c.Update(State(held + dt, flags: RobotStatusFlag.IsPickedUp), held + dt);
        Assert.Equal(OffTreadsState.InAir, c.Candidate);
        c.Update(State(held + 264, flags: RobotStatusFlag.IsPickedUp), held + 264);
        Assert.Equal(OffTreadsState.InAir, c.Current);

        // put down: OnTreads at once
        c.Update(State(held + 297), held + 297);
        Assert.Equal(OffTreadsState.OnTreads, c.Current);
    }

    // ------------------------------------------------------------------ the detector

    /// <summary>
    /// A turn in place (wheels opposing) with no gyro rotation counts one per state; the eleventh state
    /// crosses the threshold of 10 and the side is the one the left wheel's direction implies.
    /// </summary>
    [Fact]
    public void ATurnThatDoesNotTurnIsDetectedAfterElevenStates()
    {
        var d = new UnexpectedMovementDetector();
        UnexpectedMovementReport? report = null;
        d.Detected += r => report = r;
        for (uint i = 0; i < 10; i++)
        {
            Assert.Null(d.Update(State(100 + i * 33, left: -50, right: 50)));
            Assert.Equal((int)i + 1, d.Count);
        }
        var r = d.Update(State(430, left: -50, right: 50));
        Assert.NotNull(r);
        Assert.Same(r, report);
        Assert.Equal(UnexpectedMovementType.TurnedButStopped, r!.Type);
        Assert.Equal(UnexpectedMovementSide.Left, r.Side);
        Assert.Equal(100u, r.Timestamp);
        Assert.Equal(0, d.Count);
    }

    /// <summary>
    /// Rotating against the command counts two per state (0x0063E524..0x0063E536), so six states suffice,
    /// and both wheels forward puts the obstacle in front.
    /// </summary>
    [Fact]
    public void BeingSpunAgainstTheCommandCountsDouble()
    {
        var d = new UnexpectedMovementDetector();
        UnexpectedMovementReport? r = null;
        for (uint i = 0; i < 6 && r is null; i++) r = d.Update(State(i * 33, left: 50, right: 60, gz: -1.0f));
        Assert.NotNull(r);
        Assert.Equal(UnexpectedMovementType.TurnedInOppositeDirection, r!.Type);
        Assert.Equal(UnexpectedMovementSide.Front, r.Side);
        Assert.Equal(12, r.Count);
    }

    [Fact]
    public void TheDetectorResetsWhenPickedUpAndDecaysWhenStill()
    {
        var d = new UnexpectedMovementDetector();
        for (uint i = 0; i < 5; i++) d.Update(State(i, left: -50, right: 50));
        Assert.Equal(5, d.Count);
        d.Update(State(10, left: -50, right: 50, flags: RobotStatusFlag.IsPickedUp));
        Assert.Equal(0, d.Count);

        for (uint i = 0; i < 5; i++) d.Update(State(20 + i, left: -50, right: 50));
        d.Update(State(30));                       // wheels below 20 mm/s: decay
        Assert.Equal(4, d.Count);
        d.Update(State(31, left: 50, right: 50, gz: 0.5f));   // turning with the command: decay
        Assert.Equal(3, d.Count);
        d.Update(State(32, left: 50, right: 50, flags: RobotStatusFlag.IsPickingOrPlacing));
        Assert.Equal(3, d.Count);                  // skipped, not reset
    }

    /// <summary>The wheel-versus-gyro test only fires when the mismatch exceeds 0.2 rad/s.</summary>
    [Fact]
    public void DrivingStraightWithMatchingGyroIsNotUnexpected()
    {
        var d = new UnexpectedMovementDetector();
        // right − left = 46 mm/s → 1 rad/s commanded, half is 0.5; a gyro of 0.45 is within 0.2, but below
        // the 0.1745 turning threshold it would have to be compared: use 0.16 to stay in the "not turning" branch
        for (uint i = 0; i < 20; i++) d.Update(State(i, left: 27, right: 73, gz: 0.16f));
        Assert.True(d.Count <= 20);
        var d2 = new UnexpectedMovementDetector();
        for (uint i = 0; i < 20; i++) d2.Update(State(i, left: 100, right: 100, gz: 0.0f));
        Assert.Equal(0, d2.Count);                 // straight and not turning: expected 0, measured 0
    }

    // ------------------------------------------------------------------ the sensors plumbing

    [Fact]
    public void TheSensorsRunTheClassifierOnceTheRobotReportsItsHeadCalibrated()
    {
        using var rig = new Rig();
        var seen = new List<OffTreadsState>();
        rig.Robot.Sensors.OffTreadsStateChanged += (_, to) => seen.Add(to);
        rig.Stream(60);
        Assert.False(rig.Robot.Sensors.OffTreadsClassifierEnabled);
        rig.Stream(3, flags: RobotStatusFlag.IsPickedUp);
        Assert.Empty(seen);

        rig.CalibrateMotors();
        rig.Stream(1, flags: RobotStatusFlag.IsPickedUp);
        Assert.True(rig.Robot.Sensors.OffTreadsClassifierEnabled);
        Assert.Equal(new[] { OffTreadsState.InAir }, seen);
        rig.Stream(1);
        Assert.Equal(new[] { OffTreadsState.InAir, OffTreadsState.OnTreads }, seen);
    }

    [Fact]
    public void AutoStartedCalibrationIsReportedSeparately()
    {
        using var rig = new Rig();
        int auto = 0, all = 0;
        rig.Robot.Sensors.AutoCalibrationStarted += _ => auto++;
        rig.Robot.Sensors.MotorCalibrationReported += _ => all++;
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true, AutoStarted = false });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true, AutoStarted = true });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false, AutoStarted = true });
        Assert.Equal(1, auto);
        Assert.Equal(3, all);
    }

    // ------------------------------------------------------------------ the strategies

    [Fact]
    public void TheStateCallbacksFireOnTheEngineStates()
    {
        using var rig = new Rig();
        rig.CalibrateMotors();
        rig.Stream(60);
        var ctx = rig.Context();
        var strategies = ShippedReactionStrategies.ForRobot(rig.Robot).ToDictionary(s => s.Trigger);

        Assert.False(strategies[ReactionTrigger.RobotPickedUp].ShouldTrigger(ctx, null, 0));
        rig.Stream(1, flags: RobotStatusFlag.IsPickedUp);
        Assert.True(strategies[ReactionTrigger.RobotPickedUp].ShouldTrigger(ctx, null, 0));
        Assert.False(strategies[ReactionTrigger.RobotOnBack].ShouldTrigger(ctx, null, 0));

        rig.Stream(40, ay: 9800, az: 0, flags: RobotStatusFlag.IsPickedUp);
        Assert.Equal(OffTreadsState.OnRightSide, rig.Robot.Sensors.OffTreadsState);
        Assert.True(strategies[ReactionTrigger.RobotOnSide].ShouldTrigger(ctx, null, 0));
        Assert.False(strategies[ReactionTrigger.RobotPickedUp].ShouldTrigger(ctx, null, 0));
    }

    [Fact]
    public void ReturnedToTreadsLatchesOnceAndClears()
    {
        using var rig = new Rig();
        rig.CalibrateMotors();
        rig.Stream(60);
        var ctx = rig.Context();
        var s = ShippedReactionStrategies.ForRobot(rig.Robot).First(x => x.Trigger == ReactionTrigger.ReturnedToTreads);
        Assert.False(s.ShouldTrigger(ctx, null, 0));
        rig.Stream(1, flags: RobotStatusFlag.IsPickedUp);
        Assert.False(s.ShouldTrigger(ctx, null, 0));
        rig.Stream(1);
        Assert.True(s.ShouldTrigger(ctx, null, 0));
        Assert.False(s.ShouldTrigger(ctx, null, 0));
    }

    [Fact]
    public void ShakenNeedsTheFilteredMagnitudeAbove16000()
    {
        using var rig = new Rig();
        var ctx = rig.Context();
        var s = new RobotShakenStrategy();
        rig.Stream(60);
        Assert.False(s.ShouldTrigger(ctx, null, 0));
        rig.Stream(80, az: 30000);      // 0.95^80 leaves 1.6 %, so the filter reaches ~29500
        Assert.True(rig.Robot.Sensors.FilteredAccelMagnitude > RobotShakenStrategy.ShakenAccelThreshold);
        Assert.True(s.ShouldTrigger(ctx, null, 0));
    }

    [Fact]
    public void PlacedOnSlopeWantsAStillTiltedRobotJustPutDown()
    {
        using var rig = new Rig();
        rig.CalibrateMotors();
        var ctx = rig.Context();
        var s = new PlacedOnSlopeStrategy();
        // held at 28.6 degrees, gyro quiet: wants to run once quiet for more than 0.4 s
        rig.Stream(1, pitch: 0.5f, flags: RobotStatusFlag.IsPickedUp);
        Assert.False(s.ShouldTrigger(ctx, null, 0.3));
        Assert.True(s.ShouldTrigger(ctx, null, 1.0));
        // put down: still wants for 1.5 s
        rig.Stream(1, pitch: 0.5f);
        Assert.True(s.ShouldTrigger(ctx, null, 2.0));
        Assert.False(s.ShouldTrigger(ctx, null, 2.6));
        // level: never
        rig.Stream(1, pitch: 0.1f, flags: RobotStatusFlag.IsPickedUp);
        Assert.False(s.ShouldTrigger(ctx, null, 3.0));
        // tilted but rotating: the gyro resets the quiet timer
        rig.Stream(1, pitch: 0.5f, flags: RobotStatusFlag.IsPickedUp, gz: 0.5f);
        Assert.False(s.ShouldTrigger(ctx, null, 4.0));
        rig.Stream(1, pitch: 0.5f, flags: RobotStatusFlag.IsPickedUp);
        Assert.False(s.ShouldTrigger(ctx, null, 4.3));
        Assert.True(s.ShouldTrigger(ctx, null, 4.5));
    }

    [Fact]
    public void FrustrationWatchesConfidenceAndTheCooldown()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        var mood = new MoodState(MoodModel.Load(obb));
        var ctx = rig.Context(mood: mood);
        var s = new FrustrationStrategy(-0.6f, 60f);
        Assert.False(s.ShouldTrigger(ctx, null, 0));
        // drive Confident down with a shipped event
        var model = MoodModel.Load(obb);
        var down = model.Events.FirstOrDefault(e => e.Affectors.Any(a => a.Emotion == EmotionType.Confident && a.Value < 0));
        Assert.NotNull(down);
        for (int i = 0; i < 500 && mood[EmotionType.Confident] >= -0.6; i++) mood.Trigger(down!.Name, 0);
        Assert.True(mood[EmotionType.Confident] < -0.6);
        Assert.True(s.ShouldTrigger(ctx, null, 10));
        Assert.False(s.ShouldTrigger(ctx, ReactionTrigger.Frustration, 10));
        s.AnimationComplete(10);
        Assert.False(s.ShouldTrigger(ctx, null, 50));
        Assert.True(s.ShouldTrigger(ctx, null, 71));
    }

    // ------------------------------------------------------------------ the behaviours

    [Fact]
    public void ReturnedToTreadsCalibratesOnlyWhenThePitchReadsHigh()
    {
        using var rig = new Rig();
        rig.CalibrateMotors();
        rig.Stream(3);
        var ctx = rig.Context();

        var level = new ReactToReturnedToTreadsBehavior();
        using (var scope = new BehaviorScope())
        {
            level.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
            Assert.True(level.Update(ctx, 0));
            Assert.True(level.Update(ctx, 499));
            Assert.False(level.Update(ctx, 500));
            Assert.False(level.Recalibrated);
        }

        rig.Stream(3, pitch: 0.3f);   // 17 degrees
        var tilted = new ReactToReturnedToTreadsBehavior();
        using (var scope = new BehaviorScope())
        {
            tilted.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
            Assert.True(tilted.Update(ctx, 0));
            Assert.True(tilted.Update(ctx, 500));
            Assert.True(tilted.Recalibrated);
            Assert.Contains(tilted.Trace, l => l.StartsWith("calibrate head"));
            // the robot reports the calibration it was asked for
            rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true, AutoStarted = false });
            Assert.True(tilted.Update(ctx, 600));
            rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false, AutoStarted = false });
            Assert.False(tilted.Update(ctx, 700));
            Assert.Contains(tilted.Trace, l => l.Contains("reported complete"));
        }
    }

    [Fact]
    public void MotorCalibrationReactionEndsWhenBothMotorsReportOrOnTimeout()
    {
        using var rig = new Rig();
        var ctx = rig.Context();
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true, AutoStarted = true });
        var b = new ReactToMotorCalibrationBehavior();
        using var scope = new BehaviorScope();
        b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.True(scope.ReactionsDisabled);
        Assert.True(b.Update(ctx, 0));
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false, AutoStarted = true });
        Assert.True(b.Update(ctx, 100));            // the lift has never reported
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false, AutoStarted = false });
        Assert.False(b.Update(ctx, 200));
        Assert.True(b.Completed);

        var slow = new ReactToMotorCalibrationBehavior();
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true, AutoStarted = true });
        slow.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.True(slow.Update(ctx, 0));
        Assert.True(slow.Update(ctx, 4999));
        Assert.False(slow.Update(ctx, 5000));
        Assert.False(slow.Completed);
        Assert.Contains(slow.Trace, l => l.StartsWith("Calibration didn't complete"));
    }

    /// <summary>The shaken state machine against the shipped assets, driven by the fed accelerometer.</summary>
    [Fact]
    public void TheShakenMachineChoosesByHowLongTheShakeLasted()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.CalibrateMotors();
        var ctx = rig.Context(obb);
        rig.Stream(80, az: 30000, flags: RobotStatusFlag.IsPickedUp);
        Assert.True(rig.Robot.Sensors.FilteredAccelMagnitude > 16000);

        var b = new ReactToRobotShakenBehavior();
        using var scope = new BehaviorScope();
        b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.Equal(ReactToRobotShakenBehavior.Phase.Shaking, b.CurrentPhase);
        double t = 0;
        // keep shaking for 3 s of manager time
        for (; t < 3000; t += 33) { rig.Stream(1, az: 30000, flags: RobotStatusFlag.IsPickedUp); Assert.True(b.Update(ctx, t)); }
        Assert.Equal(ReactToRobotShakenBehavior.Phase.Shaking, b.CurrentPhase);
        // stop shaking, still held
        for (int i = 0; i < 80; i++, t += 33) { rig.Stream(1, flags: RobotStatusFlag.IsPickedUp); b.Update(ctx, t); }
        Assert.Equal(ReactToRobotShakenBehavior.Phase.Waiting, b.CurrentPhase);
        Assert.InRange(b.ShakenDurationSec, 3.0, 6.0);
        // put down
        rig.Stream(1);
        Assert.Equal(OffTreadsState.OnTreads, rig.Robot.Sensors.OffTreadsState);
        b.Update(ctx, t);      // Waiting -> Choosing
        b.Update(ctx, t + 33); // Choosing: plays and moves to Finishing
        Assert.Equal(ReactToRobotShakenBehavior.Reaction.Medium, b.Played);
        Assert.Equal(ReactToRobotShakenBehavior.Phase.Finishing, b.CurrentPhase);
        Assert.Contains(b.Trace, l => l.Contains("dizzy reaction Medium"));
        Assert.Contains(b.Trace, l => l.StartsWith("play DizzyReactionMedium"));
        // the reaction clip ends: the behaviour completes
        t += 66;
        Assert.False(Step(rig, b, ctx, ref t));
        Assert.Contains(b.Trace, l => l.Contains("ReactedToRobotShaken"));
    }

    [Fact]
    public void OnBackFlipsWhileOnBackAndStopsWhenRighted()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.CalibrateMotors();
        rig.Stream(60);
        float back = (float)OffTreadsClassifier.OnBackCentrePhysicalRad;
        rig.Stream(40, ax: 9800, az: 0, pitch: back, flags: RobotStatusFlag.IsPickedUp);
        Assert.Equal(OffTreadsState.OnBack, rig.Robot.Sensors.OffTreadsState);

        var ctx = rig.Context(obb);
        var b = new ReactToRobotOnBackBehavior();
        using var scope = new BehaviorScope();
        b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.Contains(b.Trace, l => l.StartsWith("play FlipDownFromBack"));
        Assert.True(rig.Robot.Animations.IsPlaying);
        Assert.True(b.Update(ctx, 0));

        // the flip worked: the robot lands on its treads while the clip plays
        rig.Stream(2);
        double t = 0;
        Assert.False(Step(rig, b, ctx, ref t));
        Assert.Contains(b.Trace, l => l.Contains("ReactedToRobotOnBack"));
    }

    [Fact]
    public void OnSideAsksToBeRightedAndWaits()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.CalibrateMotors();
        rig.Stream(60);
        rig.Stream(40, ay: -9800, az: 0, flags: RobotStatusFlag.IsPickedUp);
        Assert.Equal(OffTreadsState.OnLeftSide, rig.Robot.Sensors.OffTreadsState);

        var ctx = rig.Context(obb);
        var b = new ReactToRobotOnSideBehavior();
        using var scope = new BehaviorScope();
        b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.Contains(b.Trace, l => l.StartsWith("play ReactToOnLeftSide"));
        double t = 0;
        Step(rig, b, ctx, ref t);
        Assert.Contains(b.Trace, l => l.StartsWith("play AskToBeRightedLeft"));
        Step(rig, b, ctx, ref t);
        Assert.Contains(b.Trace, l => l.StartsWith("play WaitOnSideLoop"));
        // sixteen seconds of manager time later the bored sequence plays
        t += 16000;
        Step(rig, b, ctx, ref t);
        Assert.Contains(b.Trace, l => l.StartsWith("play NothingToDoBoredIntro"));
        Assert.Equal(1, b.BoredSequences);
        b.Stop(BehaviorStopReason.Cancelled);
        SpinUntil(() => !rig.Robot.Animations.IsPlaying);
    }

    /// <summary>
    /// Ends the running clip and ticks the behaviour until it has taken the completion (its trace grows or
    /// it finishes), so a slow thread pool under a parallel test run cannot make the step flaky.
    /// </summary>
    private static bool Step(Rig rig, IBehavior b, BehaviorContext ctx, ref double t)
    {
        int before = (b as SteppedBehavior)?.Trace.Count ?? 0;
        rig.Robot.Animations.Stop();
        SpinUntil(() => !rig.Robot.Animations.IsPlaying);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool running = true;
        while (sw.ElapsedMilliseconds < 8000)     // generous: one run of the full suite in parallel starved this at 3 s
        {
            t += 33;
            running = b.Update(ctx, t);
            if (!running || ((b as SteppedBehavior)?.Trace.Count ?? 0) > before) break;
            Thread.Sleep(5);
        }
        return running;
    }

    private static void SpinUntil(Func<bool> cond)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cond() && sw.ElapsedMilliseconds < 2000) Thread.Sleep(5);
        Assert.True(cond());
    }

    // ------------------------------------------------------------------ the manager

    private sealed class FakeStrategy : IReactionTriggerStrategy
    {
        public FakeStrategy(ReactionTrigger t) => Trigger = t;
        public ReactionTrigger Trigger { get; }
        public string Basis => "test";
        public bool Fire { get; set; }
        public bool ShouldTrigger(BehaviorContext context, ReactionTrigger? current, double nowSec) => Fire;
    }

    private sealed class CountingBehavior : IBehavior
    {
        public CountingBehavior(string id) => Id = id;
        public string Id { get; }
        public string Class => "test";
        public int Starts, Stops;
        public bool Running;
        public bool IsRunnable(BehaviorContext c) => true;
        public double EvaluateScore(BehaviorContext c) => 1;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) { Starts++; Running = true; return Task.CompletedTask; }
        public bool Update(BehaviorContext c, double nowMs) => Running;
        public void Stop(BehaviorStopReason r) { Stops++; Running = false; }
    }

    [Fact]
    public void AReactionInterruptsAndResumesTheBehaviourItTookOverFrom()
    {
        using var rig = new Rig();
        var ctx = rig.Context();
        var manager = new BehaviorManager(ctx);
        var idle = new CountingBehavior("Idle");
        manager.Add(idle);
        var strategy = new FakeStrategy(ReactionTrigger.UnexpectedMovement);
        var reaction = new CountingBehavior("ReactToUnexpectedMovement");
        manager.AddReaction(strategy, reaction, resumeLast: true);

        manager.ChooseAndSwitch(0);
        Assert.Same(idle, manager.Current);
        Assert.Null(manager.CheckReactions(1));

        strategy.Fire = true;
        var sw = manager.CheckReactions(2);
        Assert.NotNull(sw);
        Assert.Equal("Idle", sw!.Interrupted);
        Assert.True(sw.WillResume);
        Assert.Same(reaction, manager.Current);
        Assert.Equal(ReactionTrigger.UnexpectedMovement, manager.CurrentReactionTrigger);
        Assert.Equal(1, idle.Stops);

        Assert.Null(manager.CheckReactions(3));    // already reacting to it
        strategy.Fire = false;

        reaction.Running = false;
        manager.Update(4000, 4);
        Assert.Same(idle, manager.Current);
        Assert.Equal(2, idle.Starts);
        Assert.Null(manager.CurrentReactionTrigger);
    }

    [Fact]
    public void ADisabledTriggerAndAReactionLockBothHoldReactionsBack()
    {
        using var rig = new Rig();
        var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Arbiter = arbiter };
        var manager = new BehaviorManager(ctx);
        var strategy = new FakeStrategy(ReactionTrigger.RobotOnBack) { Fire = true };
        manager.AddReaction(strategy, new CountingBehavior("ReactToRobotOnBack"));

        manager.SetTriggerEnabled(ReactionTrigger.RobotOnBack, false);
        Assert.Null(manager.CheckReactions(0));
        manager.SetTriggerEnabled(ReactionTrigger.RobotOnBack, true);

        var holder = new object();
        arbiter.DisableReactions(holder);
        Assert.Null(manager.CheckReactions(0));
        arbiter.EnableReactions(holder);
        Assert.NotNull(manager.CheckReactions(0));
    }

    // ------------------------------------------------------------------ the cube path

    private sealed class FakeLocator : ICubeLocator
    {
        public HashSet<uint> Located = new();
        public float Distance = 200;
        public bool Visible;
        public int Turns;
        private TaskCompletionSource<bool>? _turn;
        public bool IsLocated(uint id) => Located.Contains(id);
        public float? DistanceFromRobotMm(uint id) => Located.Contains(id) ? Distance : null;
        public bool IsVisibleFromCamera(uint id) => Visible;
        /// <summary>The turn runs until the test ends it with <see cref="CompleteTurn"/>, so its timing is the test's.</summary>
        public Task<bool> TurnTowardsAsync(uint id, CancellationToken ct)
        {
            Turns++;
            _turn = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ct.Register(() => _turn.TrySetResult(false));
            return _turn.Task;
        }
        public void CompleteTurn() => _turn?.TrySetResult(true);
    }

    [Fact]
    public void TheCubeMovedReactionCannotFireWithoutALocatedCube()
    {
        using var rig = new Rig();
        var ctx = rig.Context();
        var behavior = new AcknowledgeCubeMovedBehavior();
        using var strategy = new CubeMovedReactionStrategy(rig.Robot, behavior, locator: null);
        rig.Send(new ObjectMoved { Timestamp = 1000, ObjectID = 7, AxisOfAccel = UpAxis.ZPositive });
        rig.Stream(1);
        Assert.False(strategy.HasLocator);
        Assert.False(strategy.ShouldTrigger(ctx, null, 0));
        Assert.False(behavior.IsRunnable(ctx));
        Assert.Single(strategy.Tracker.Entries);
        Assert.False(strategy.Tracker.Entries[0].Moving);   // unlocated: the record is marked not moving
    }

    [Fact]
    public void ALocatedCubeThatMovedForASecondOutOfViewFiresTheReaction()
    {
        using var rig = new Rig();
        var ctx = rig.Context();
        var locator = new FakeLocator { Located = { 7 } };
        var behavior = new AcknowledgeCubeMovedBehavior(locator);
        using var strategy = new CubeMovedReactionStrategy(rig.Robot, behavior, locator);

        strategy.ObjectObserved(7);                                       // the engine needs a sighting first
        rig.Send(new ObjectMoved { Timestamp = 1000, ObjectID = 7, AxisOfAccel = UpAxis.ZPositive });
        rig.T = 1500; rig.Stream(1);
        Assert.False(strategy.ShouldTrigger(ctx, null, 0));               // moved 500 ms: not long enough
        rig.T = 2100; rig.Stream(1);
        locator.Visible = true;
        Assert.False(strategy.ShouldTrigger(ctx, null, 0));               // in view: no reaction
        locator.Visible = false;
        Assert.True(strategy.ShouldTrigger(ctx, null, 0));
        Assert.Equal(7u, behavior.TargetObjectId);
        Assert.False(strategy.Tracker.Entries[0].Moving);                 // reset after firing

        // within 50 mm it is ignored
        strategy.ObjectObserved(7);
        rig.Send(new ObjectMoved { Timestamp = 3000, ObjectID = 7, AxisOfAccel = UpAxis.ZPositive });
        rig.T = 4200; rig.Stream(1);
        locator.Distance = 30;
        Assert.False(strategy.ShouldTrigger(ctx, null, 0));
        locator.Distance = 100;
        Assert.True(strategy.ShouldTrigger(ctx, null, 0));

        // an up-axis change fires without the second
        strategy.ObjectObserved(7);
        rig.Send(new ObjectUpAxisChanged { Timestamp = 5000, ObjectID = 7, UpAxis = UpAxis.XPositive });
        Assert.True(strategy.ShouldTrigger(ctx, null, 0));

        // a cube the world model loses is dropped
        locator.Located.Clear();
        rig.Send(new ObjectMoved { Timestamp = 6000, ObjectID = 7, AxisOfAccel = UpAxis.ZPositive });
        Assert.False(strategy.ShouldTrigger(ctx, null, 0));
        Assert.Empty(strategy.Tracker.Entries);
    }

    [Fact]
    public void TheCubeMovedBehaviourSensesThenTurnsThenReacts()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        var ctx = rig.Context(obb);
        var locator = new FakeLocator { Located = { 7 } };
        var b = new AcknowledgeCubeMovedBehavior(locator) { TargetObjectId = 7 };
        Assert.True(b.IsRunnable(ctx));
        using var scope = new BehaviorScope();
        b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.Equal(AcknowledgeCubeMovedBehavior.Phase.PlayingSenseReaction, b.CurrentPhase);
        Assert.Contains(b.Trace, l => l.StartsWith("play CubeMovedSense"));

        // the sense clip ends and the 0.5 s wait runs out: the turn starts
        double t = 0;
        b.Update(ctx, t);
        rig.Robot.Animations.Stop();
        SpinUntil(() => !rig.Robot.Animations.IsPlaying);
        SpinUntil(() => { t += 33; b.Update(ctx, t); return b.CurrentPhase == AcknowledgeCubeMovedBehavior.Phase.TurningToLastLocation; });
        Assert.Equal(1, locator.Turns);

        // the turn's 0.5 s companion wait passes; the turn itself is still going, so nothing changes
        t += 600;
        b.Update(ctx, t);
        Assert.Equal(AcknowledgeCubeMovedBehavior.Phase.TurningToLastLocation, b.CurrentPhase);

        // the turn ends without a sighting: absence
        locator.CompleteTurn();
        SpinUntil(() => { t += 33; b.Update(ctx, t); return b.CurrentPhase == AcknowledgeCubeMovedBehavior.Phase.ReactingToBlockAbsence; });
        Assert.Contains(b.Trace, l => l.StartsWith("play CubeMovedUpset"));
        Assert.False(Step(rig, b, ctx, ref t));
        b.Stop(BehaviorStopReason.Completed);      // what the manager does when Update returns false
        Assert.Null(b.TargetObjectId);

        // seen while turning: the presence branch
        var seen = new AcknowledgeCubeMovedBehavior(locator) { TargetObjectId = 7 };
        seen.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        t = 0;
        seen.Update(ctx, t);
        rig.Robot.Animations.Stop();
        SpinUntil(() => !rig.Robot.Animations.IsPlaying);
        SpinUntil(() => { t += 33; seen.Update(ctx, t); return seen.CurrentPhase == AcknowledgeCubeMovedBehavior.Phase.TurningToLastLocation; });
        seen.ObjectObserved(7);
        seen.Update(ctx, t + 33);
        Assert.Equal(AcknowledgeCubeMovedBehavior.Phase.ReactingToBlockPresence, seen.CurrentPhase);
        Assert.Contains(seen.Trace, l => l.StartsWith("play AcknowledgeObject"));
        seen.Stop(BehaviorStopReason.Cancelled);
        SpinUntil(() => !rig.Robot.Animations.IsPlaying);
    }

    /// <summary>
    /// Replayed over the committed fw2457 captures (a robot sitting on its treads while its head moves), the
    /// classifier is enabled by the robot's own calibration reports, classifies every state, never leaves
    /// OnTreads, and the detector never fires. Gravity reads about 10500 in the robot's units on this
    /// hardware, which the engine's 9800 +/- 3000 side band still accommodates.
    /// </summary>
    [Theory]
    [InlineData("hw_fw2457_full.log", 600)]
    [InlineData("hw_fw2457_probe.log", 300)]
    public void TheCapturedRobotStaysOnTreadsThroughTheWholeLog(string file, int atLeastStates)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", file);
        if (!File.Exists(path)) return;
        var line = new System.Text.RegularExpressions.Regex(@"^(\S+) (TX|RX) ((?:[0-9a-f]{2} ?)+)$");
        using var robot = CozmoRobot.CreateOffline();
        int states = 0, transitions = 0, movements = 0;
        robot.Message += m => { if (m is RobotState) states++; };
        robot.Sensors.OffTreadsStateChanged += (_, _) => transitions++;
        robot.Sensors.UnexpectedMovementDetected += _ => movements++;
        foreach (var raw in File.ReadLines(path))
        {
            var m = line.Match(raw.Trim('\uFEFF'));
            if (!m.Success || m.Groups[2].Value != "RX") continue;
            try { robot.Transport.ProcessIncoming(Hex.Parse(m.Groups[3].Value)); } catch (FormatException) { }
        }
        Assert.True(states >= atLeastStates, $"only {states} states");
        Assert.True(robot.Sensors.OffTreadsClassifierEnabled, "the log carries the head calibration report");
        Assert.Equal(states, robot.Sensors.OffTreads.Updates);
        Assert.Equal(0, transitions);
        Assert.Equal(0, movements);
        Assert.Equal(OffTreadsState.OnTreads, robot.Sensors.OffTreadsState);
        Assert.InRange(robot.Sensors.FilteredAccelMagnitude, 9500, 11500);
    }

    // ------------------------------------------------------------------ whole-config validation

    private static Dictionary<string, string> ShippedReactionMap(string obb)
    {
        var path = Path.Combine(obb, "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "reactionTrigger_behavior_map.json");
        var text = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(path), "//[^\n\r]*", "");
        using var doc = System.Text.Json.JsonDocument.Parse(text);
        var map = new Dictionary<string, string>();
        foreach (var e in doc.RootElement.GetProperty("reactionTriggerBehaviorMap").EnumerateArray())
        {
            var trigger = e.GetProperty("reactionTrigger").GetString()!;
            if (!map.ContainsKey(trigger)) map[trigger] = e.GetProperty("behaviorID").GetString()!;
        }
        return map;
    }

    /// <summary>Every registered reaction runs the behaviour the shipped map names for its trigger.</summary>
    [Fact]
    public void EveryRegisteredReactionMatchesTheShippedMap()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var map = ShippedReactionMap(obb);
        using var rig = new Rig();
        var regs = ShippedBehaviors.Reactions(rig.Robot, new FakeLocator());
        Assert.Equal(12, regs.Count);
        foreach (var r in regs)
        {
            Assert.True(map.TryGetValue(r.Strategy.Trigger.ToString(), out var id), $"{r.Strategy.Trigger} is not in the shipped map");
            Assert.Equal(id, r.Behavior.Id);
            Assert.False(string.IsNullOrWhiteSpace(r.Strategy.Basis));
            Assert.Contains("0x", r.Strategy.Basis);
        }
        // shouldResumeLast as the shipped map states it
        Assert.True(regs.Single(r => r.Strategy.Trigger == ReactionTrigger.UnexpectedMovement).ResumeLast);
        Assert.True(regs.Single(r => r.Strategy.Trigger == ReactionTrigger.MotorCalibration).ResumeLast);
        Assert.True(regs.Single(r => r.Strategy.Trigger == ReactionTrigger.CliffDetected).ResumeLast);
        Assert.False(regs.Single(r => r.Strategy.Trigger == ReactionTrigger.RobotOnBack).ResumeLast);
        Assert.False(regs.Single(r => r.Strategy.Trigger == ReactionTrigger.RobotPickedUp).ResumeLast);
    }

    /// <summary>Every animation trigger the M10 behaviours can ask for resolves against the shipped assets.</summary>
    [Fact]
    public void EveryTriggerTheDerivedStateReactionsPlayResolves()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var map = AnimationTriggerMap.Load(obb);
        var lib = AnimationLibrary.Open(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var triggers = new[]
        {
            AnimationTrigger.FlipDownFromBack, AnimationTrigger.FacePlantRoll, AnimationTrigger.FacePlantRollArmUp,
            AnimationTrigger.FailedToRightFromFace, AnimationTrigger.ReactToOnLeftSide, AnimationTrigger.ReactToOnRightSide,
            AnimationTrigger.AskToBeRightedLeft, AnimationTrigger.AskToBeRightedRight, AnimationTrigger.WaitOnSideLoop,
            AnimationTrigger.NothingToDoBoredIntro, AnimationTrigger.NothingToDoBoredEvent, AnimationTrigger.NothingToDoBoredOutro,
            AnimationTrigger.ReactToPerchedOnBlock, AnimationTrigger.DizzyShakeLoop, AnimationTrigger.DizzyShakeStop,
            AnimationTrigger.DizzyStillPickedUp, AnimationTrigger.DizzyReactionHard, AnimationTrigger.DizzyReactionMedium,
            AnimationTrigger.DizzyReactionSoft, AnimationTrigger.ReactToUnexpectedMovement, AnimationTrigger.FrustratedByFailure,
            AnimationTrigger.CubeMovedSense, AnimationTrigger.CubeMovedUpset, AnimationTrigger.AcknowledgeObject,
            AnimationTrigger.FeedingReactToShake_Normal, AnimationTrigger.FeedingReactToShake_Severe,
            AnimationTrigger.FeedingReactToFullCube_Normal, AnimationTrigger.FeedingReactToFullCube_Severe,
            AnimationTrigger.FeedingReactToSeeCube_Normal, AnimationTrigger.FeedingReactToSeeCube_Severe,
        };
        var unresolved = new List<string>();
        foreach (var t in triggers)
        {
            var r = map.Resolve(t, lib, new Random(1));
            if (!r.Resolved || !lib.HasClip(r.Selected!)) unresolved.Add($"{t}: {r.Problem}");
        }
        Assert.True(unresolved.Count == 0, string.Join("\n", unresolved));
    }

    /// <summary>
    /// The inventory files every PlayAnim config with animTriggers as implementable; the loader must build
    /// exactly those, every trigger must be known to the enum, and every one must resolve to a real clip.
    /// </summary>
    [Fact]
    public void EveryShippedPlayAnimConfigLoadsAndResolves()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var problems = new List<string>();
        var built = ShippedBehaviors.PlayAnims(obb, problems);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        Assert.Equal(15, built.Count);
        Assert.Contains(built, b => b.Id == "Hiccup");
        Assert.Contains(built, b => b.Id == "FeedingReactCubeShake");
        Assert.Contains(built, b => b.Id == "NothingToDo_Idle");
        Assert.DoesNotContain(built, b => b.Id == "VC_AlrightyResponse");     // PlayAnimWithFace: needs a face

        var inventory = Path.Combine(Path.GetDirectoryName(obb)!, "behavior_inventory.json");
        if (File.Exists(inventory))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(inventory));
            var filed = doc.RootElement.EnumerateArray()
                .Where(e => e.GetProperty("behaviorClass").GetString() == "PlayAnim"
                            && e.GetProperty("category").GetString()!.StartsWith("implementable"))
                .Select(e => e.GetProperty("behaviorID").GetString()!).OrderBy(x => x, StringComparer.Ordinal).ToList();
            Assert.Equal(filed, built.Select(b => b.Id).OrderBy(x => x, StringComparer.Ordinal).ToList());
        }

        using var rig = new Rig();
        var ctx = rig.Context(obb);
        // the gated configs: ReactToObstacle (ObstacleDetected), Hiking_FirstLookWakeUp (1 s off the charger), Hiking_FirstLookIntro (0.25 s into the activity)
        foreach (var gated in new[] { "ReactToObstacle", "Hiking_FirstLookWakeUp", "Hiking_FirstLookIntro" })
            Assert.False(built.Single(b => b.Id == gated).IsRunnable(ctx), gated);
        ctx.ObstacleDetected = () => true; ctx.ClockSec = () => 10; ctx.LastDriveOffChargerSec = 9.5; ctx.LastActivitySwitchSec = 9.9;
        foreach (var b in built)
        {
            Assert.True(b.IsRunnable(ctx), b.Id);
            using var scope = new BehaviorScope();
            b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
            Assert.False(string.IsNullOrEmpty(((PlayAnimBehavior)b).LastSelected), $"{b.Id} resolved to no clip");
            b.Stop(BehaviorStopReason.Cancelled);
        }
        SpinUntil(() => !rig.Robot.Animations.IsPlaying);
    }

    /// <summary>The emotion events the M10 behaviours fire exist in the shipped mood model.</summary>
    [Fact]
    public void TheEmotionEventsTheReactionsFireAreShipped()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var model = MoodModel.Load(obb);
        Assert.NotNull(model.Event(ReactToUnexpectedMovementBehavior.EmotionEventName));
        Assert.NotNull(model.Event("FinishedMinorFrustration"));
    }

    /// <summary>The M7 dispatcher now fires the pick-up reaction from the derived InAir state, once the classifier runs.</summary>
    [Fact]
    public void TheDispatcherFiresPickUpFromInAirOnceTheClassifierRuns()
    {
        using var rig = new Rig();
        var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
        var decisions = new List<BehaviorDecision>();
        using var reactive = new ReactiveBehavior(rig.Robot, new AnimationTriggerMap(), arbiter: arbiter) { Asynchronous = false };
        reactive.Reacted += decisions.Add;
        reactive.Start();

        rig.Stream(3);
        rig.Stream(1, flags: RobotStatusFlag.IsPickedUp);          // classifier off: the flag fires it
        Assert.Contains(decisions, d => d.Reaction == ReactionTrigger.RobotPickedUp && d.Reason.Contains("no animation assets"));
        decisions.Clear();
        rig.Stream(1);

        rig.CalibrateMotors();
        rig.Stream(3);
        rig.Stream(1, flags: RobotStatusFlag.IsPickedUp);
        var fired = decisions.Where(d => d.Reaction == ReactionTrigger.RobotPickedUp).ToList();
        Assert.Contains(fired, d => d.Reason.Contains("derived InAir"));   // the flag only reports
        Assert.Contains(fired, d => d.Reason.Contains("no animation assets"));   // InAir fires it
    }
}
