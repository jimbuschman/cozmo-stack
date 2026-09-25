using System.Globalization;
using System.Text.RegularExpressions;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The M4 control layer: motion, lights, sensors and cubes. Everything here runs against an offline robot
/// fed synthetic or captured traffic, so it needs no hardware and is deterministic.
/// </summary>
public class ControlTests
{
    /// <summary>Spins until a message of the wanted type has been sent, so tests never race the send.</summary>
    private static async Task<T> WaitFor<T>(Func<T?> get, int timeoutMs = 2000) where T : class
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (get() is { } v) return v;
            await Task.Delay(5);
        }
        throw new TimeoutException($"no {typeof(T).Name} was sent within {timeoutMs} ms");
    }

    // ------------------------------------------------------------------ harness

    private sealed class Rig
    {
        public readonly CozmoRobot Robot;
        private readonly ManualClock _clock = new() { NowMs = 1000 };
        private ushort _seq = 1;

        public Rig()
        {
            Robot = CozmoRobot.CreateOffline(TransportOptions.EngineDefaults, _clock);
            Deliver(new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), _seq++));
        }

        /// <summary>
        /// Runs the connection forward. The engine spaces outgoing packets by 2 ms and batches rather than
        /// sending immediately, so a queued message does not reach the wire until the connection is ticked.
        /// A live transport does that on its own thread; offline it is done here.
        /// </summary>
        public void Pump(int ticks = 6)
        {
            for (int i = 0; i < ticks; i++)
            {
                _clock.Advance(40);
                Robot.Transport.OfflineTick();
                Ack();
            }
        }

        /// <summary>
        /// Acknowledges everything sent so far, as a robot would. Without this the reliable messages are
        /// never retired and every tick resends them, which is correct behaviour but makes counting what
        /// was sent impossible.
        /// </summary>
        private void Ack()
        {
            ushort highest = 0;
            foreach (var f in Robot.Transport.OfflineOutbound)
                foreach (var m in f.Messages)
                    if (m.Seq > highest) highest = m.Seq;
            if (highest == 0 || highest == _lastAcked) return;
            _lastAcked = highest;
            Robot.Transport.ProcessIncoming(FrameCodec.Encode(
                Frame.Single(new SubMessage(ReliableMessageType.Ack, Array.Empty<byte>()), highest)));
        }

        private ushort _lastAcked;

        /// <summary>Hands the robot a message as though it had arrived over the wire.</summary>
        public void Send(RobotMessage m) => Deliver(new SubMessage(ReliableMessageType.SingleReliableMessage, m.ToBytes(), _seq++));

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

        /// <summary>Every CLAD message the robot sent, decoded. Ticks the connection first so nothing is in limbo.</summary>
        public List<RobotMessage> Sent
        {
            get
            {
                Pump();
                return Drain();
            }
        }

        private List<RobotMessage> Drain()
        {
            var seen = new HashSet<ushort>();
            var outp = new List<RobotMessage>();
            foreach (var sm in Robot.Transport.OfflineOutbound.SelectMany(f => f.Messages))
            {
                if (sm.Type is not (ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage)
                    || sm.Payload.Length == 0) continue;
                if (sm.Seq != 0 && !seen.Add(sm.Seq)) continue;   // a resend of one we already counted
                outp.Add(RobotMessage.Parse(sm.Payload));
            }
            return outp;
        }

        public T? LastSent<T>() where T : RobotMessage => Sent.OfType<T>().LastOrDefault();

        /// <summary>Outbound messages without ticking, for asserting that nothing was sent at all.</summary>
        public List<RobotMessage> SentSoFar => Drain();

        /// <summary>A plausible RobotState with the given flags and wheel speeds.</summary>
        public static RobotState StateWith(RobotStatusFlag flags = 0, float left = 0, float right = 0,
                                           float battery = 4.0f, float head = 0, ushort[]? cliffs = null) => new()
        {
            Timestamp = 1,
            Pose = new RobotPose(),
            LwheelSpeedMmps = left,
            RwheelSpeedMmps = right,
            HeadAngle = head,
            LiftAngle = 32f,
            Accel = new AccelData { X = 0, Y = 0, Z = 9800 },
            Gyro = new GyroData { X = 0, Y = 0, Z = 0 },
            BatteryVoltage = battery,
            Status = (uint)flags,
            CliffDataRaw = cliffs ?? new ushort[] { 500, 500, 500, 500 },
        };

        /// <summary>
        /// Gets the robot past calibration and into a ready state.
        ///
        /// Both motors, because the robot calibrates head and lift separately and reports each with its
        /// own MotorID. This used to calibrate only the head, which passed while readiness was a single
        /// flag that either motor could clear.
        /// </summary>
        public void MakeReady()
        {
            Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true });
            Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true });
            Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false });
            Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false });
            Send(StateWith());
        }
    }

    // ------------------------------------------------------------------- motion

    [Fact]
    public async Task MotionIsRefusedBeforeTheRobotHasReportedAnything()
    {
        var rig = new Rig();
        var r = await rig.Robot.Motion.DriveWheelsAsync(50, 50);
        Assert.Equal(MotionResult.Refused, r.Result);
        Assert.Contains("has not sent any state", r.Detail);
        Assert.Empty(rig.Sent.OfType<DriveWheels>());       // nothing was sent, not merely unconfirmed
        Assert.Empty(rig.SentSoFar.OfType<DriveWheels>());
    }

    [Fact]
    public async Task MotionIsRefusedWhileTheHeadAndLiftAreStillCalibrating()
    {
        var rig = new Rig();
        rig.Send(Rig.StateWith());
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true });

        var r = await rig.Robot.Motion.SetHeadAngleAsync(0.3f, timeout: TimeSpan.FromMilliseconds(200));
        Assert.Equal(MotionResult.Refused, r.Result);
        Assert.Contains("calibrating", r.Detail);
        Assert.Empty(rig.Sent.OfType<SetHeadAngle>());
    }

    [Fact]
    public async Task StopAllIsNeverGatedOnCalibration()
    {
        var rig = new Rig();
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true });
        rig.Send(Rig.StateWith());       // stopped, so the confirmation is already satisfied

        var r = await rig.Robot.Motion.StopAllAsync(TimeSpan.FromMilliseconds(500));
        Assert.True(r.Ok, r.Detail);
        Assert.Single(rig.Sent.OfType<StopAllMotors>());
    }

    /// <summary>
    /// M4-003 (MD1, MA10, MA11): the stack's head and lift API is the game-message path, whose speed, acceleration and
    /// duration are the caller's; its defaults are what the original app passes (Unity Robot.cs:1443-1450 head 10/20,
    /// 1638-1646 lift 10/20, duration 0). Updated for the M4 inventory: this test used to expect the action
    /// constructor's head 15/20 (MA9), which only engine-internal callers get.
    /// </summary>
    [Fact]
    public async Task TheHeadAndLiftDefaultsAreTheAppsGamePathValues()
    {
        Assert.Equal(10f, CozmoMotion.DefaultHeadSpeedRadPerSec);
        Assert.Equal(20f, CozmoMotion.DefaultHeadAccelRadPerSec2);
        Assert.Equal(10f, CozmoMotion.DefaultLiftSpeedRadPerSec);
        Assert.Equal(20f, CozmoMotion.DefaultLiftAccelRadPerSec2);

        var rig = new Rig();
        rig.MakeReady();
        _ = rig.Robot.Motion.SetHeadAngleAsync(0.3f, timeout: TimeSpan.FromMilliseconds(200));
        var head = await WaitFor(() => rig.LastSent<SetHeadAngle>());
        Assert.Equal(10f, head.MaxSpeedRadPerSec);
        Assert.Equal(20f, head.AccelRadPerSec2);
        Assert.Equal(0f, head.DurationSec);

        _ = rig.Robot.Motion.SetLiftHeightAsync(60f, timeout: TimeSpan.FromMilliseconds(200));
        var lift = await WaitFor(() => rig.LastSent<SetLiftHeight>());
        Assert.Equal(10f, lift.MaxSpeedRadPerSec);
        Assert.Equal(20f, lift.AccelRadPerSec2);
        Assert.Equal(0f, lift.DurationSec);
    }

    [Fact]
    public async Task SetHeadAngleWaitsForTheRobotToAcknowledgeThatExactAction()
    {
        var rig = new Rig();
        rig.MakeReady();

        var pending = rig.Robot.Motion.SetHeadAngleAsync(0.3f, timeout: TimeSpan.FromSeconds(5));
        var sent = await WaitFor(() => rig.LastSent<SetHeadAngle>());
        Assert.Equal(0.3f, sent.AngleRad, 3);

        rig.Send(new MotorActionAck { ActionId = (byte)(sent.ActionId + 1) });   // a different action
        Assert.False(pending.IsCompleted);

        rig.Send(new MotorActionAck { ActionId = sent.ActionId });               // the right one
        // M4-016 MA17: after the ack the move completes once the head is in position and stopped (HEAD_IN_POS).
        rig.Send(Rig.StateWith(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos, head: 0.3f));
        var r = await pending;
        Assert.True(r.Ok, r.Detail);
        Assert.Contains($"action {sent.ActionId}", r.Detail);
    }

    [Fact]
    public async Task AnUnacknowledgedActionTimesOutRatherThanReportingSuccess()
    {
        var rig = new Rig();
        rig.MakeReady();
        var r = await rig.Robot.Motion.SetHeadAngleAsync(0.2f, timeout: TimeSpan.FromMilliseconds(250));
        Assert.Equal(MotionResult.TimedOut, r.Result);
        Assert.False(r.Ok);
        Assert.Single(rig.Sent.OfType<SetHeadAngle>());     // it was sent; only the outcome is unknown
    }

    [Fact]
    public async Task ActionIdsAreDistinctPerAction()
    {
        var rig = new Rig();
        rig.MakeReady();
        var ids = new List<byte>();
        for (int i = 0; i < 6; i++)
        {
            _ = rig.Robot.Motion.SetHeadAngleAsync(0.1f, timeout: TimeSpan.FromMilliseconds(60));
            var sent = await WaitFor(() => rig.LastSent<SetHeadAngle>());
            if (ids.Count == 0 || sent.ActionId != ids[^1]) ids.Add(sent.ActionId);
            await Task.Delay(70);
        }
        Assert.True(ids.Count >= 3, "each action should carry its own id");
        // M4-005 MA8: the counter does reach 0 after 255, so "never zero" is no longer asserted (M4ControlTests).
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task HeadAngleAndLiftHeightAreClampedToTheRobotsRange()
    {
        var rig = new Rig();
        rig.MakeReady();

        _ = rig.Robot.Motion.SetHeadAngleAsync(99f, timeout: TimeSpan.FromMilliseconds(60));
        var head = await WaitFor(() => rig.LastSent<SetHeadAngle>());
        Assert.Equal(CozmoMotion.MaxHeadAngleRad, head.AngleRad, 4);

        // M4-002 MA13: a height in [0, 32) clamps to 32; a negative one goes to the nearer preset (M4ControlTests).
        _ = rig.Robot.Motion.SetLiftHeightAsync(10f, timeout: TimeSpan.FromMilliseconds(60));
        var lift = await WaitFor(() => rig.LastSent<SetLiftHeight>());
        Assert.Equal(CozmoMotion.MinLiftHeightMm, lift.HeightMm, 4);
    }

    [Fact]
    public async Task DrivingIsConfirmedFromTheReportedWheelSpeedsNotFromTheSend()
    {
        var rig = new Rig();
        rig.MakeReady();

        var pending = rig.Robot.Motion.DriveWheelsAsync(60, 60, confirmWithin: TimeSpan.FromSeconds(5));
        var sent = await WaitFor(() => rig.LastSent<DriveWheels>());
        Assert.Equal(60f, sent.LwheelSpeedMmps, 3);
        Assert.False(pending.IsCompleted, "nothing should be confirmed until the robot says the wheels turned");

        rig.Send(Rig.StateWith(RobotStatusFlag.AreWheelsMoving, left: 60, right: 60));
        var r = await pending;
        Assert.True(r.Ok, r.Detail);
        Assert.True(rig.Robot.Motion.WheelsMoving);
        Assert.Equal((60f, 60f), rig.Robot.Motion.WheelSpeeds);
    }

    [Fact]
    public async Task StopAllWaitsForTheRobotToReportTheWheelsStopped()
    {
        var rig = new Rig();
        rig.MakeReady();
        rig.Send(Rig.StateWith(RobotStatusFlag.AreWheelsMoving, left: 80, right: 80));

        var pending = rig.Robot.Motion.StopAllAsync(TimeSpan.FromSeconds(5));
        await WaitFor(() => rig.LastSent<StopAllMotors>());
        Assert.False(pending.IsCompleted);

        rig.Send(Rig.StateWith());
        var r = await pending;
        Assert.True(r.Ok, r.Detail);
        Assert.False(rig.Robot.Motion.WheelsMoving);
    }

    [Fact]
    public void EmergencyStopSendsStopAllAndAZeroWheelCommand()
    {
        var rig = new Rig();
        rig.Robot.EmergencyStop();
        Assert.Single(rig.Sent.OfType<StopAllMotors>());
        var wheels = Assert.Single(rig.Sent.OfType<DriveWheels>());
        Assert.Equal(0f, wheels.LwheelSpeedMmps);
        Assert.Equal(0f, wheels.RwheelSpeedMmps);
    }

    // ------------------------------------------------------------------- lights

    /// <summary>
    /// M4-017 LB5: SetBackpackLEDs loops the pattern at source 2 and BodyLightComponent::Update sends it at the next
    /// Robot::Update (LB1), as 0x03 then 0x11 (LB3). Updated for the M4 inventory: the send used to be immediate.
    /// </summary>
    [Fact]
    public void BackpackColoursArePackedAndRememberedAsSent()
    {
        var rig = new Rig();
        rig.Send(Rig.StateWith());
        rig.Robot.Lights.SetBackpack(LedColor.Red, LedColor.Green, LedColor.Blue);
        rig.Send(Rig.StateWith());                // the next Robot::Update

        var msg = rig.Sent.OfType<BackpackLightsMiddle>().Last();
        Assert.Equal(LedColor.Red.Packed, msg.Field0[0].OnColor);
        Assert.Equal(LedColor.Green.Packed, msg.Field0[1].OnColor);
        Assert.Equal(LedColor.Blue.Packed, msg.Field0[2].OnColor);
        Assert.Equal((LedColor.Red, LedColor.Green, LedColor.Blue), rig.Robot.Lights.Backpack);
    }

    [Fact]
    public void ABlinkCarriesTheRobotsOwnFrameTimingRatherThanALoopHere()
    {
        var rig = new Rig();
        rig.Send(Rig.StateWith());
        rig.Robot.Lights.BlinkBackpack(LedColor.Red, LedColor.Off, onFrames: 10, offFrames: 20);
        rig.Send(Rig.StateWith());                // M4-017: sent at the next Robot::Update
        var msg = rig.Sent.OfType<BackpackLightsMiddle>().Last();
        Assert.Equal((byte)10, msg.Field0[0].OnFrames);
        Assert.Equal((byte)20, msg.Field0[0].OffFrames);
        Assert.Equal(LedColor.Red.Packed, msg.Field0[0].OnColor);
        Assert.Equal(LedColor.Off.Packed, msg.Field0[0].OffColor);
    }

    [Fact]
    public void TheHeadlightTracksWhatItWasToldBecauseTheRobotNeverReportsIt()
    {
        var rig = new Rig();
        Assert.False(rig.Robot.Lights.HeadlightOn);
        rig.Robot.Lights.SetHeadlight(true);
        Assert.True(rig.Robot.Lights.HeadlightOn);
        Assert.True(Assert.Single(rig.Sent.OfType<SetHeadlight>()).Enable);
    }

    [Theory]
    [InlineData("#FF0000", 255, 0, 0)]
    [InlineData("00ff00", 0, 255, 0)]
    public void ColoursParseFromHex(string hex, byte r, byte g, byte b)
        => Assert.Equal(new LedColor(r, g, b), LedColor.FromHex(hex));

    [Fact]
    public void AMalformedColourIsRejected() => Assert.Throws<ArgumentException>(() => LedColor.FromHex("abc"));

    // ------------------------------------------------------------------ sensors

    [Fact]
    public void SensorsReadThroughToTheLatestReportedState()
    {
        var rig = new Rig();
        rig.Send(Rig.StateWith(RobotStatusFlag.IsOnCharger | RobotStatusFlag.IsCharging,
                               battery: 4.05f, head: 0.25f, cliffs: new ushort[] { 11, 22, 33, 44 }));

        var s = rig.Robot.Sensors;
        Assert.Equal(4.05f, s.BatteryVolts!.Value, 3);
        Assert.True(s.OnCharger);
        Assert.True(s.Charging);
        Assert.False(s.PickedUp);
        Assert.Equal(0.25f, s.HeadAngleRad!.Value, 3);
        Assert.Equal(new ushort[] { 11, 22, 33, 44 }, s.CliffSensorsRaw);
        Assert.Equal(9800f, s.Accelerometer!.Value.Z, 1);
        Assert.Equal(new Vector3(0, 0, 0), s.Gyroscope!.Value);
    }

    [Fact]
    public void ACliffEventIsRecordedWithWhichSensorsTrippedAndWhetherTheRobotStopped()
    {
        var rig = new Rig();
        var seen = new List<CliffReport>();
        rig.Robot.Sensors.CliffDetected += seen.Add;

        rig.Send(new CliffEvent { Timestamp = 4242, DetectedFlags = 0b0101, DidStopForCliff = true });

        var c = Assert.Single(seen);
        Assert.Equal(4242u, c.Timestamp);
        Assert.Equal(CliffSensors.FrontLeft | CliffSensors.BackLeft, c.Sensors);
        Assert.True(c.StoppedForCliff);
        Assert.Single(rig.Robot.Sensors.CliffHistory);
    }

    [Fact]
    public void PickedUpAndChargerChangesRaiseEventsOnlyWhenTheyActuallyChange()
    {
        var rig = new Rig();
        var picked = new List<bool>();
        var charger = new List<bool>();
        rig.Robot.Sensors.PickedUpChanged += picked.Add;
        rig.Robot.Sensors.OnChargerChanged += charger.Add;

        rig.Send(Rig.StateWith(RobotStatusFlag.IsOnCharger));    // baseline, no events
        rig.Send(Rig.StateWith(RobotStatusFlag.IsOnCharger));    // unchanged
        rig.Send(Rig.StateWith(RobotStatusFlag.IsPickedUp));     // left the charger and was lifted
        rig.Send(Rig.StateWith(RobotStatusFlag.IsPickedUp));     // unchanged

        Assert.Equal(new[] { true }, picked);
        Assert.Equal(new[] { false }, charger);
    }

    [Fact]
    public void StopOnCliffAndImuBurstsGoOutAsTheirOwnMessages()
    {
        var rig = new Rig();
        rig.Robot.Sensors.SetStopOnCliff(true);
        rig.Robot.Sensors.RequestImuBurst(TimeSpan.FromMilliseconds(750));

        Assert.True(Assert.Single(rig.Sent.OfType<EnableStopOnCliff>()).Enable);
        Assert.Equal(750u, Assert.Single(rig.Sent.OfType<IMURequest>()).LengthMs);
    }

    // -------------------------------------------------------------------- cubes

    [Fact]
    public void ACubeIsTrackedFromItsAdvertisementAndThenItsConnection()
    {
        var rig = new Rig();
        var discovered = new List<Cube>();
        var connections = new List<Cube>();
        rig.Robot.Cubes.CubeDiscovered += discovered.Add;
        rig.Robot.Cubes.ConnectionChanged += connections.Add;

        rig.Send(new ObjectAvailable { FactoryId = 0xAABBCCDD, ObjectType = ObjectType.Block_LIGHTCUBE1, Rssi = -55 });
        rig.Send(new ObjectAvailable { FactoryId = 0xAABBCCDD, ObjectType = ObjectType.Block_LIGHTCUBE1, Rssi = -50 });

        var cube = Assert.Single(discovered);                     // the second advertisement is not a new cube
        Assert.Equal(0xAABBCCDDu, cube.FactoryId);
        Assert.Equal((sbyte)-50, cube.Rssi!.Value);
        Assert.Equal(2, cube.Advertisements);
        Assert.False(cube.Connected);
        Assert.Empty(connections);

        rig.Send(new ObjectConnectionState
        {
            ObjectID = 2, FactoryID = 0xAABBCCDD, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true,
        });
        Assert.Single(connections);
        Assert.True(cube.Connected);
        Assert.Equal(2u, cube.ObjectId);
        Assert.Same(cube, rig.Robot.Cubes.ByObjectId(2));
        Assert.Single(rig.Robot.Cubes.ConnectedCubes);
    }

    /// <summary>
    /// The engine takes an advertisement only from something it can use. HandleActiveObjectAvailable
    /// 0x0053391C asks IsValidLightCube 0x007D1D08 - a jump table whose only true entries are the three
    /// light cube types, the ghost at 4 not among them - and then IsCharger 0x007D1D50, which is true for
    /// Charger_Basic, and returns without recording anything when both say no. The charger is kept in the
    /// same table as the cubes but is not one of them.
    /// </summary>
    [Fact]
    public void OnlyLightCubesAndTheChargerAreTrackedFromAnAdvertisement()
    {
        var rig = new Rig();
        rig.Send(new ObjectAvailable { FactoryId = 1, ObjectType = ObjectType.Block_LIGHTCUBE3, Rssi = -40 });
        rig.Send(new ObjectAvailable { FactoryId = 2, ObjectType = ObjectType.Block_LIGHTCUBE_GHOST, Rssi = -40 });
        rig.Send(new ObjectAvailable { FactoryId = 3, ObjectType = ObjectType.Charger_Basic, Rssi = -40 });
        rig.Send(new ObjectAvailable { FactoryId = 4, ObjectType = ObjectType.CustomType00, Rssi = -40 });

        var cube = Assert.Single(rig.Robot.Cubes.DiscoveredCubes);
        Assert.Equal(1u, cube.FactoryId);
        Assert.Null(rig.Robot.Cubes.ByFactoryId(2));
        Assert.Null(rig.Robot.Cubes.ByFactoryId(4));
        Assert.Equal(3u, rig.Robot.Cubes.Charger!.FactoryId);
    }

    /// <summary>
    /// The id on a connection report is a radio slot, one of the engine's MAX_NUM_ACTIVE_OBJECTS = 5, and
    /// HandleActiveObjectConnectionState 0x00533B3C drops a report above that at 0x00533B58 before it
    /// reaches BlockWorld. This stack uses that id as its world object id as well, so the bound is
    /// recorded and not enforced - see CozmoCubes.MaxActiveObjectSlot - and a cube keeps whichever id it
    /// arrived with.
    /// </summary>
    [Fact]
    public void TheConnectionIdIsKeptAsTheObjectIdWhateverItIs()
    {
        Assert.Equal(4u, CozmoCubes.MaxActiveObjectSlot);
        var rig = new Rig();
        rig.Send(new ObjectConnectionState
        {
            ObjectID = 7, FactoryID = 0x1234, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true,
        });
        var cube = Assert.Single(rig.Robot.Cubes.ConnectedCubes);
        Assert.Equal(7u, cube.ObjectId);
        Assert.Same(cube, rig.Robot.Cubes.ByObjectId(7));
    }

    [Fact]
    public void CubeTelemetryLandsOnTheConnectedCube()
    {
        var rig = new Rig();
        rig.Send(new ObjectConnectionState { ObjectID = 3, FactoryID = 1, ObjectType = ObjectType.Block_LIGHTCUBE2, Connected = true });

        rig.Send(new ObjectPowerLevel { ObjectID = 3, BatteryLevel = 140, MissedPackets = 9 });
        // M4-009 CD10a: the movement comes first, since M4-023 CD10g ignores movement inside a tap's 500 ms window.
        rig.Send(new ObjectMoved { ObjectID = 3, Timestamp = 6, Accel = new ActiveAccel { X = 1, Y = 2, Z = 3 }, AxisOfAccel = UpAxis.ZPositive });
        // M4-023 CD10e: a tap needs intensity (tapPos − tapNeg) above 60; the offline robot is not physical, so it is
        // broadcast at once. Updated for the M4 inventory: an intensity-0 tap used to count.
        rig.Send(new ObjectTapped { ObjectID = 3, Timestamp = 5, NumTaps = 1, TapNeg = -40, TapPos = 30 });

        var cube = rig.Robot.Cubes.ByObjectId(3)!;
        Assert.Equal((byte)140, cube.BatteryLevelRaw!.Value);
        // HandleObjectPowerLevel 0x00537130 reads the byte as hundredths of a volt and turns it into a
        // percentage that is flat at 100 above 1.5 V and at 0 below 1 V.
        Assert.Equal(1.4f, cube.BatteryVolts!.Value, 3);
        Assert.Equal(80f, cube.BatteryPercent!.Value, 3);
        Assert.Equal(9u, cube.MissedPackets);
        Assert.Equal(1, (int)cube.Taps);
        Assert.True(cube.Moving);
        Assert.Equal(new Vector3(1, 2, 3), cube.Accel);
        Assert.Equal(UpAxis.ZPositive, cube.UpAxis);

        rig.Send(new ObjectStoppedMoving { ObjectID = 3, Timestamp = 7 });
        Assert.False(cube.Moving);
    }

    [Fact]
    public void TelemetryForAnUnknownCubeIsIgnoredRatherThanInventingOne()
    {
        var rig = new Rig();
        rig.Send(new ObjectTapped { ObjectID = 99, Timestamp = 1, NumTaps = 1 });
        Assert.Empty(rig.Robot.Cubes.DiscoveredCubes);
    }

    [Fact]
    public void TurningDiscoveryOnSendsTheAccessoryDiscoveryMessage()
    {
        var rig = new Rig();
        rig.Robot.Cubes.SetDiscovery(true);
        Assert.True(Assert.Single(rig.Sent.OfType<SetAccessoryDiscovery>()).Enable);
        Assert.True(rig.Robot.Cubes.DiscoveryEnabled);
    }

    // ----------------------------------------------------------------- readiness

    [Fact]
    public async Task ReadinessReportsWhatItIsStillWaitingFor()
    {
        var rig = new Rig();
        var (ready, why) = await rig.Robot.WaitUntilReadyAsync(TimeSpan.FromMilliseconds(200), describe: true);
        Assert.False(ready);
        Assert.Contains("no telemetry", why);
        Assert.Contains("animation controller not running", why);
        Assert.Contains("motor calibration never reported", why);
    }

    [Fact]
    public async Task ReadinessSucceedsOnceTelemetryAnimationAndCalibrationAreAllIn()
    {
        var rig = new Rig();
        rig.Send(Rig.StateWith());
        rig.Send(new AnimationState { EnabledAnimTracks = 0xFF });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false });

        // The head alone is not readiness: the lift calibrates separately and has not reported yet.
        var (early, whyNot) = await rig.Robot.WaitUntilReadyAsync(TimeSpan.FromMilliseconds(200), describe: true);
        Assert.False(early);
        Assert.Contains("lift", whyNot);

        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false });

        var (ready, why) = await rig.Robot.WaitUntilReadyAsync(TimeSpan.FromSeconds(2), describe: true);
        Assert.True(ready, why);
    }

    // ------------------------------------------------------ replay against hardware capture

    private static readonly Regex Line = new(@"^(\S+) (TX|RX) ((?:[0-9a-f]{2} ?)+)$", RegexOptions.Compiled);

    /// <summary>
    /// The whole control layer driven by the 20 s capture from the firmware-2457 robot. Nothing is asserted
    /// here that the robot did not actually report.
    /// </summary>
    [Fact]
    public void TheControlLayerReadsTheHardwareCaptureCorrectly()
    {
        var clock = new ManualClock { NowMs = 1000 };
        var robot = CozmoRobot.CreateOffline(TransportOptions.EngineDefaults, clock);
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hw_fw2457_full.log");
        foreach (var l in File.ReadAllLines(path))
        {
            var m = Line.Match(l.Trim('﻿', ' '));
            if (!m.Success || m.Groups[2].Value != "RX") continue;
            clock.Advance(33);
            robot.Transport.ProcessIncoming(Hex.Parse(m.Groups[3].Value));
        }

        var s = robot.Sensors;
        Assert.True(s.OnCharger, "the robot was on its charger for this capture");
        Assert.InRange(s.BatteryVolts!.Value, 3.5f, 5.0f);
        Assert.False(s.PickedUp);
        Assert.False(s.WheelsMoving);
        Assert.Equal((0f, 0f), s.WheelSpeedsMmps!.Value);
        Assert.Equal(4, s.CliffSensorsRaw!.Length);
        Assert.NotNull(s.Accelerometer);
        Assert.NotNull(s.Gyroscope);
        Assert.True(robot.State.CalibrationSeen, "the robot calibrates its motors on every connect");
        Assert.False(robot.State.CalibratingMotors, "and finishes within the capture");
    }
}
