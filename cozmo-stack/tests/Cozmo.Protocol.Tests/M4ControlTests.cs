using System.Net;
using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The M4 control batch (motion, sensors, lights, cubes; M4-001..M4-023, with M1-041 and M2-015). Every expected value
/// comes from a row of re-analysis/inventory/M4-control.md (Appendix A MA/LB/LC/SC/CD, Appendix B, Appendix C), named
/// in each test. The engine runs over a fake transport port on the production path (CozmoRobot.CreateForTest), ticked
/// by hand, so the per-tick order and the origin gate are the production ones.
/// </summary>
public class M4ControlTests
{
    // ------------------------------------------------------------------ rig

    private sealed class FakePort : IEngineTransport
    {
        public readonly List<byte[]> Sent = new();
        public bool TimedOut { get; set; }
        public event Action<ReceiverEvent>? Received;
        public void Start() { }
        public void Connect(IPAddress ip, bool isSimulated) { }
        public void Disconnect(IPEndPoint address) { }
        public void SendData(byte[] clad) { lock (Sent) Sent.Add(clad); }
        public void Raise(ReceiverMarker m, IPEndPoint? a, byte[]? d = null) => Received?.Invoke(new ReceiverEvent(m, a, d));
    }

    private sealed class Rig : IDisposable
    {
        public long NowNs = 1_000_000_000;
        public readonly FakePort Port = new();
        public readonly CozmoRobot Robot;
        public CozmoEngine Engine => Robot.Engine;
        public readonly List<string> Log = new();
        public static readonly IPAddress RobotIp = IPAddress.Parse("172.31.1.1");
        public static readonly IPEndPoint RobotEp = new(RobotIp, 5551);
        private uint _ts = 100;

        public Rig(string? resources = null)
        {
            Robot = CozmoRobot.CreateForTest(Port, () => NowNs, new CozmoEngineOptions { BlockPoolPath = "", ResourcesPath = resources });
            Engine.LogLine += l => { lock (Log) Log.Add(l); };
        }

        public void Tick(double ms = 60) { NowNs += (long)(ms * 1_000_000); Engine.Tick(); }
        public void Data(RobotMessage m) => Port.Raise(ReceiverMarker.Data, RobotEp, m.ToBytes());

        /// <summary>Connected, validated, Success, SyncTimeAck: the robot is time synced (no state yet).</summary>
        public void ToSynced()
        {
            Engine.ConnectToRobot(RobotIp); Tick();
            Port.Raise(ReceiverMarker.OnConnected, RobotEp); Tick();
            Data(new RobotAvailable { SerialNumberHead = 1, HwVersion = 5 });
            Data(new FirmwareVersion { RobotId = 1, Signature = Encoding.UTF8.GetBytes("{\"version\": 2381, \"time\": 1546972025, \"build\": \"DEVELOPMENT\"}") });
            Tick();
            Data(new ManufacturingID { SerialNumber = 2, BodyHwVersion = 7, BodyColor = 2 });
            Tick();
            Data(new SyncTimeAck());
            Tick();
        }

        /// <summary>Both motors calibrated (M4-004 policy gate), as MotorCalibration reports.</summary>
        public void Calibrate()
        {
            Data(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true });
            Data(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true });
            Data(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false });
            Data(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false });
        }

        public RobotState MakeState(RobotStatusFlag flags = RobotStatusFlag.IsBodyAccMode, uint origin = 1, float x = 0, float y = 0,
                                    float head = 0, float liftAngle = 0, float battery = 4.0f, uint frame = 0) => new()
        {
            Timestamp = _ts += 33, PoseFrameId = frame, PoseOriginId = origin, Pose = new RobotPose { X = x, Y = y },
            HeadAngle = head, LiftAngle = liftAngle, BatteryVoltage = battery, Status = (uint)flags,
            Accel = new AccelData { Z = 9800 }, Gyro = new GyroData(), CliffDataRaw = new ushort[] { 500, 500, 500, 500 },
        };

        /// <summary>One RobotState, then a tick.</summary>
        public void State(RobotStatusFlag flags = RobotStatusFlag.IsBodyAccMode, uint origin = 1, float x = 0, float y = 0,
                          float head = 0, float liftAngle = 0, float battery = 4.0f, double tickMs = 60)
        {
            Data(MakeState(flags, origin, x, y, head, liftAngle, battery));
            Tick(tickMs);
        }

        public int Mark() { lock (Port.Sent) return Port.Sent.Count; }
        public List<byte[]> RawSince(int mark) { lock (Port.Sent) return Port.Sent.Skip(mark).ToList(); }
        public List<RobotMessage> SentSince(int mark) => RawSince(mark).Select(b => RobotMessage.Parse(b)).ToList();
        public bool Logged(string part) { lock (Log) return Log.Any(l => l.Contains(part)); }
        public void Dispose() => Robot.Dispose();
    }

    private static byte[] Hex(string s) => Convert.FromHexString(s.Replace(" ", ""));
    private static byte[] Body(byte[] clad) => clad[1..];

    private static string? ObbResources()
    {
        var env = Environment.GetEnvironmentVariable("COZMO_RESOURCES");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var c = Path.Combine(d.FullName, "re-analysis", "obb", "assets", "cozmo_resources");
            if (File.Exists(Path.Combine(c, "config", "engine", "lights", "backpackLights", "backpackLightPatterns.json"))) return c;
        }
        return null;
    }

    /// <summary>
    /// A resources directory holding the WakeUp content exactly as LC3 states it (the trigger map's WakeUp → wakeUp,
    /// CubeAnimationTriggerMap.json:156-157, and cubeLights/wakeUp.json's two patterns), nothing else.
    /// </summary>
    private static string WakeUpResources()
    {
        var root = Path.Combine(Path.GetTempPath(), "m4-cube-lights-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "assets", "cubeAnimationGroupMaps"));
        Directory.CreateDirectory(Path.Combine(root, "config", "engine", "lights", "cubeLights"));
        File.WriteAllText(Path.Combine(root, "assets", "cubeAnimationGroupMaps", "CubeAnimationTriggerMap.json"),
            "{ \"Pairs\": [ { \"CladEvent\": \"WakeUp\", \"AnimName\": \"wakeUp\" } ] }");
        File.WriteAllText(Path.Combine(root, "config", "engine", "lights", "cubeLights", "wakeUp.json"), """
            { "wakeUp": [
              { "pattern": { "onColors": [[0,255,0,255],[0,255,0,255],[0,255,0,255],[0,255,0,255]],
                             "offColors": [[0,0,0,255],[0,0,0,255],[0,0,0,255],[0,0,0,255]],
                             "onPeriod_ms": [10,10,10,10], "offPeriod_ms": [380,380,380,380],
                             "transitionOnPeriod_ms": [200,200,200,200], "transitionOffPeriod_ms": [50,50,50,50],
                             "offset": [0,100,200,300], "rotationPeriod_ms": 0 },
                "duration_ms": 1280, "patternDebugName": "wakeUp_spin" },
              { "pattern": { "onColors": [[0,255,0,255],[0,255,0,255],[0,255,0,255],[0,255,0,255]],
                             "offColors": [[0,0,0,255],[0,0,0,255],[0,0,0,255],[0,0,0,255]],
                             "onPeriod_ms": [1000,1000,1000,1000], "offPeriod_ms": [3000,3000,3000,3000],
                             "transitionOnPeriod_ms": [100,100,100,100], "transitionOffPeriod_ms": [1000,1000,1000,1000],
                             "offset": [0,0,0,0], "rotationPeriod_ms": 0 },
                "duration_ms": 5100, "canBeOverridden": false, "patternDebugName": "wakeUp_fadeOut" } ] }
            """);
        return root;
    }

    // ------------------------------------------------------------------ M4-020, M1-041

    /// <summary>
    /// M4-020 SC4f/SC4g: off a ramp, a state whose origin id is not in the pose-origin list gets the warning "Received
    /// RobotState with originID" and its later steps are skipped (0x00512C3E..0x00512C4A, 0x00512EC4..0x00512F12); the
    /// list starts with origin 1 from the constructor's Delocalize and 0 is never in it. C3: the first full state
    /// (+0x34E) is marked right after the time-sync gate, before the origin check (0x0051293C..0x00512948), so a
    /// rejected state marks it too.
    /// </summary>
    [Fact]
    public void M4_020_SC4f_SC4g_AStateWithAnUnknownOriginIsDroppedWithAWarning()
    {
        using var rig = new Rig();
        rig.ToSynced();
        Assert.Equal(new uint[] { 1 }, rig.Engine.Robot!.PoseOriginIds);
        Assert.False(rig.Engine.Robot.FirstFullStateHandled);
        var s0 = rig.MakeState(origin: 0);
        rig.Data(s0); rig.Tick();
        Assert.True(rig.Engine.Robot.FirstFullStateHandled);
        Assert.Null(rig.Engine.Robot.AcceptedState);                    // s0 was rejected at the origin check
        Assert.True(rig.Logged("Received RobotState with originID 0"));
        var s2 = rig.MakeState(origin: 2);
        rig.Data(s2); rig.Tick();
        Assert.Null(rig.Engine.Robot.AcceptedState);                    // s2 was rejected at the origin check
        var s1 = rig.MakeState(origin: 1);
        rig.Data(s1); rig.Tick();
        Assert.Equal(s1.Timestamp, rig.Engine.Robot.AcceptedState?.Timestamp);
    }

    /// <summary>
    /// M4-020, M1 CD18 (MD5): SendSyncTime sends SyncTime, InitController, ImageRequest and then AbsoluteLocalizationUpdate
    /// {timestamp 0, frameId robot+0x2B0 = 0, originId the current origin = 1, identity pose 0, 0, 0}
    /// (0x0051526E..0x005153AE, 0x00512734..0x005127B6; SC4e, SC4g, SC4h); +0x520 is stamped once it is sent.
    /// </summary>
    [Fact]
    public void M4_020_CD18_SyncTimeSendsAbsoluteLocalizationUpdateAfterImageRequest()
    {
        using var rig = new Rig();
        rig.Engine.ConnectToRobot(Rig.RobotIp); rig.Tick();
        rig.Port.Raise(ReceiverMarker.OnConnected, Rig.RobotEp); rig.Tick();
        rig.Data(new RobotAvailable());
        rig.Data(new FirmwareVersion { RobotId = 1, Signature = Encoding.UTF8.GetBytes("{\"version\": 2381, \"time\": 1}") });
        rig.Tick();
        int mark = rig.Mark();
        rig.Data(new ManufacturingID { SerialNumber = 2, BodyColor = 2 });
        rig.Tick();
        var ids = rig.RawSince(mark).Select(b => (RobotMessageId)b[0]).ToList();
        int img = ids.IndexOf(RobotMessageId.ImageRequest), abs = ids.IndexOf(RobotMessageId.AbsLocalizationUpdate);
        Assert.True(img >= 0 && abs == img + 1, string.Join(",", ids));
        Assert.Equal(Hex("45 00000000 00000000 01000000 00000000 00000000 00000000"), rig.RawSince(mark)[abs]);
        Assert.True(rig.Engine.Robot!.SyncTimeSentAt > 0);
        Assert.False(rig.Logged("MISSING: AbsoluteLocalizationUpdate"));
    }

    /// <summary>M1-041 CD18: Robot::SyncTime runs RobotStateHistory::Clear before SendSyncTime (0x0051521E..).</summary>
    [Fact]
    public void M1_041_CD18_SyncTimeClearsTheRobotStateHistory()
    {
        using var rig = new Rig();
        using var vision = new VisionSystem(rig.Robot, CameraCalibration.Nominal()) { Enabled = false };
        rig.ToSynced();
        rig.State(); rig.State();
        Assert.True(vision.History.Count > 0);
        rig.Engine.Robot!.SyncTime();
        Assert.Equal(0, vision.History.Count);
    }

    /// <summary>
    /// M4-020 / C3: an origin-rejected but synced state still has everything UpdateFullRobotState stores before the
    /// origin check applied - the status bits and the stored state, the RS6 head angle, the lift angle, the moving flags
    /// (MovementComponent::Update), the SC2 cliff data - and Robot::Update's components run (the backpack lights); only
    /// the later steps are skipped: the history and the cliff threshold schedule. The one SetCliffDetectThreshold {50}
    /// is SetOnCharger's (C8, before the origin check), not the schedule's.
    /// </summary>
    [Fact]
    public void M4_020_C3_AnOriginRejectedStateStillAppliesThePartBeforeTheOriginCheck()
    {
        using var rig = new Rig();
        using var vision = new VisionSystem(rig.Robot, CameraCalibration.Nominal()) { Enabled = false };
        rig.ToSynced();
        rig.Calibrate();
        int mark = rig.Mark();
        var st = rig.MakeState(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger | RobotStatusFlag.CliffDetected,
                               origin: 0, head: 0.3f, liftAngle: 0.6f);
        st.CliffDataRaw = new ushort[] { 7, 8, 9, 10 };
        rig.Data(st); rig.Tick();
        Assert.Equal(st.Timestamp, rig.Robot.State.Latest?.Timestamp);
        Assert.True(rig.Robot.Sensors.OnCharger);
        Assert.Equal(0.3f, rig.Robot.Motion.HeadAngleRad);
        Assert.True(rig.Robot.Motion.HeadMoving && rig.Robot.Motion.LiftMoving);   // HEAD_IN_POS and LIFT_IN_POS clear
        Assert.Equal(new ushort[] { 7, 8, 9, 10 }, rig.Robot.Sensors.CliffDataRawStored);
        Assert.True(rig.Robot.Sensors.CliffDetectedStored);
        Assert.Equal(2, rig.Robot.Lights.Body.ChargingState);                     // on the charger, not charging
        Assert.Contains(rig.RawSince(mark), b => b[0] == 0x03);                   // Robot::Update ran the body lights
        Assert.Equal(0, vision.History.Count);
        var thresholds = rig.RawSince(mark).Where(b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold).ToList();
        Assert.Equal(Hex("3200"), Body(Assert.Single(thresholds)));
        Assert.True(rig.Robot.Sensors.OnChargerPlatform);
    }

    // ------------------------------------------------------------------ M4-022

    /// <summary>
    /// M4-022 SC10 / M1 CD25 / M2 App. B: a synced state without IS_BODY_ACC_MODE counts (+0x34D), and at 16 the engine
    /// sends SetBodyRadioMode {0x01, 0x00} and restarts the count; a state with the bit does not reset it. The count is
    /// before the origin check (0x00512AD0..0x00512B52), so a dropped state counts too.
    /// </summary>
    [Fact]
    public void M4_022_SC10_SetBodyRadioModeAfter16StatesWithoutTheBit()
    {
        using var rig = new Rig();
        rig.ToSynced();
        int mark = rig.Mark();
        for (int i = 0; i < 8; i++) rig.State(flags: 0, origin: 0);
        rig.State(flags: RobotStatusFlag.IsBodyAccMode);
        for (int i = 0; i < 7; i++) rig.State(flags: 0);
        Assert.DoesNotContain(rig.RawSince(mark), b => b[0] == (byte)RobotMessageId.SetBodyRadioMode);
        rig.State(flags: 0);                                         // the 16th without the bit
        var radio = rig.RawSince(mark).Where(b => b[0] == (byte)RobotMessageId.SetBodyRadioMode).ToList();
        Assert.Equal(Hex("01 00"), Body(Assert.Single(radio)));
        for (int i = 0; i < 15; i++) rig.State(flags: 0);
        Assert.Single(rig.RawSince(mark), b => b[0] == (byte)RobotMessageId.SetBodyRadioMode);
    }

    // ------------------------------------------------------------------ M4-019, M4-008

    /// <summary>
    /// M4-019 SC1, SC5, SC6: the cliff component starts enabled with a threshold cache of 400 and raw values 0xFFFF; the
    /// game EnableCliffSensor only stores +4; with the sensor disabled a CliffEvent with flags ≠ 0 is dropped with no
    /// broadcast, and one with flags 0 still goes out.
    /// </summary>
    [Fact]
    public void M4_019_SC1_SC5_SC6_CliffDefaultsAndTheDisabledDrop()
    {
        using var rig = new Rig();
        var s = rig.Robot.Sensors;
        Assert.True(s.CliffSensorEnabled);
        Assert.Equal(400, s.CliffDetectThreshold);
        Assert.Equal(new ushort[] { 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF }, s.CliffDataRawStored);
        rig.ToSynced();
        var seen = new List<CliffReport>();
        s.CliffDetected += seen.Add;
        int mark = rig.Mark();
        s.SetCliffSensorEnabled(false);
        Assert.Empty(rig.RawSince(mark));
        rig.Data(new CliffEvent { Timestamp = 5, DetectedFlags = 1 }); rig.Tick();
        Assert.Empty(seen);
        rig.Data(new CliffEvent { Timestamp = 6, DetectedFlags = 0 }); rig.Tick();
        Assert.Single(seen);
        s.SetCliffSensorEnabled(true);
        rig.Data(new CliffEvent { Timestamp = 7, DetectedFlags = 2 }); rig.Tick();
        Assert.Equal(2, seen.Count);
        Assert.True(s.CliffDetectedByEvent);
    }

    /// <summary>M4-008 SC2: UpdateRobotData stores the raw values (+0xE), CLIFF_DETECTED (+6) and the timestamp (+8).</summary>
    [Fact]
    public void M4_008_SC2_TheCliffDataOfAHandledStateIsStored()
    {
        using var rig = new Rig();
        rig.ToSynced();
        var st = rig.MakeState(RobotStatusFlag.CliffDetected);
        st.CliffDataRaw = new ushort[] { 11, 22, 33, 44 };
        rig.Data(st); rig.Tick();
        Assert.Equal(new ushort[] { 11, 22, 33, 44 }, rig.Robot.Sensors.CliffDataRawStored);
        Assert.True(rig.Robot.Sensors.CliffDetectedStored);
        Assert.Equal(st.Timestamp, rig.Robot.Sensors.CliffDataTimestamp);
    }

    /// <summary>
    /// M4-019 SC7: PotentialCliff off the charger platform, not in drone or SDK mode: StopAllMotors (0x3B) and
    /// EnableStopOnCliff {0} (0x0053582C..0x00535998).
    /// </summary>
    [Fact]
    public void M4_019_SC7_PotentialCliffStopsAllMotorsAndDisablesStopOnCliff()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        int mark = rig.Mark();
        rig.Data(new PotentialCliff()); rig.Tick();
        var ids = rig.RawSince(mark).Select(b => b[0]).Where(b => b is 0x3B or (byte)RobotMessageId.EnableStopOnCliff).ToList();
        Assert.Equal(new byte[] { 0x3B, (byte)RobotMessageId.EnableStopOnCliff }, ids);
        Assert.Equal(Hex("00"), Body(rig.RawSince(mark).First(b => b[0] == (byte)RobotMessageId.EnableStopOnCliff)));
    }

    /// <summary>
    /// M4-019 SC4, SC4e, SC4f: the first accepted state of the current frame (robot+0x2B0 = 0) sends
    /// SetCliffDetectThreshold {50}; after more than 50 mm it sends {400} once. A dropped state reaches neither; SC8:
    /// nothing sends EnableStopOnCliff at connection.
    /// </summary>
    [Fact]
    public void M4_019_SC4_SC8_TheThresholdSchedule()
    {
        using var rig = new Rig();
        int all = rig.Mark();
        rig.ToSynced();
        int mark = rig.Mark();
        rig.State(origin: 0);
        Assert.DoesNotContain(rig.RawSince(mark), b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold);
        List<byte[]> Thresholds() => rig.RawSince(mark).Where(b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold).ToList();
        rig.State(x: 0);
        Assert.Equal(Hex("3200"), Body(Assert.Single(Thresholds())));
        rig.State(x: 30);
        Assert.Single(Thresholds());
        rig.State(x: 60);                                             // 60 mm in all
        Assert.Equal(2, Thresholds().Count);
        Assert.Equal(Hex("9001"), Body(Thresholds()[1]));
        rig.State(x: 200); rig.State(x: 0);
        Assert.Equal(2, Thresholds().Count);
        Assert.Equal(400, rig.Robot.Sensors.CliffDetectThreshold);
        Assert.DoesNotContain(rig.RawSince(all), b => b[0] == (byte)RobotMessageId.EnableStopOnCliff);
    }

    /// <summary>
    /// M4-019 C7 (rows D1..D5): the distance behind the 400 threshold is the XY displacement of the drive centre,
    /// MoveRobotPoseForward(pose, −20 mm) = (x − 20 cosθ, y − 20 sinθ), between the pose before and after each matching
    /// state, counted from the first. A pure turn in place moves the drive centre: turning by π moves it 40 mm.
    /// </summary>
    [Fact]
    public void M4_019_C7_TheDistanceIsTheDriveCentreDisplacement()
    {
        var (x, y) = CozmoSensors.MoveRobotPoseForward(10f, 5f, MathF.PI / 2, -20f);
        Assert.Equal(10f, x, 4);
        Assert.Equal(-15f, y, 4);
        using var rig = new Rig();
        rig.ToSynced();
        int mark = rig.Mark();
        List<byte[]> Thresholds() => rig.RawSince(mark).Where(b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold).ToList();
        var st = rig.MakeState(); st.Pose = new RobotPose { X = 0, Y = 0, Angle = 0 };
        rig.Data(st); rig.Tick();
        Assert.Equal(Hex("3200"), Body(Assert.Single(Thresholds())));
        st = rig.MakeState(); st.Pose = new RobotPose { X = 0, Y = 0, Angle = MathF.PI };      // 40 mm of drive centre
        rig.Data(st); rig.Tick();
        Assert.Single(Thresholds());
        st = rig.MakeState(); st.Pose = new RobotPose { X = 11, Y = 0, Angle = MathF.PI };     // 51 mm in all
        rig.Data(st); rig.Tick();
        Assert.Equal(Hex("9001"), Body(Thresholds()[1]));
    }

    /// <summary>
    /// M4-019 C8 (rows P1..P6) and SC7: the first state with IS_ON_CHARGER, the contacts flag being 0, sets the platform
    /// flag (RobotOnChargerPlatformEvent, SetCliffDetectThreshold 50); leaving the contacts does not clear it; a
    /// PotentialCliff on the platform is ignored; a committed off-treads change to anything but OnTreads clears it once
    /// the contacts flag is 0 (400).
    /// </summary>
    [Fact]
    public void M4_019_C8_SC7_TheChargerPlatform()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        var events = new List<bool>();
        rig.Robot.Sensors.OnChargerPlatformChanged += events.Add;
        int mark = rig.Mark();
        List<string> Thresholds() => rig.RawSince(mark).Where(b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold)
                                        .Select(b => Convert.ToHexString(Body(b))).ToList();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger);
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger);
        Assert.Equal(new[] { true }, events);
        Assert.True(rig.Robot.Sensors.OnChargerPlatform);
        rig.State();                                                               // off the contacts: still on the platform
        Assert.True(rig.Robot.Sensors.OnChargerPlatform);
        int before = rig.Mark();
        rig.Data(new PotentialCliff()); rig.Tick();
        Assert.DoesNotContain(rig.RawSince(before), b => b[0] is 0x3B or (byte)RobotMessageId.EnableStopOnCliff);
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsPickedUp);   // committed InAir
        Assert.Equal(OffTreadsState.InAir, rig.Robot.Sensors.OffTreadsState);
        Assert.False(rig.Robot.Sensors.OnChargerPlatform);
        Assert.Equal(new[] { true, false }, events);
        Assert.Contains("3200", Thresholds());
        Assert.Equal("9001", Thresholds()[^1]);
    }

    // ------------------------------------------------------------------ M4-001, M4-002, M4-003

    /// <summary>
    /// M4-001 MA9: a commanded head angle is clipped to [−0.436332, 0.776672] with a warning; M4-003 MD1/MA11: the
    /// defaults are head 10 rad/s, 20 rad/s², duration 0; the action id is the next of the shared counter.
    /// </summary>
    [Fact]
    public void M4_001_M4_003_MA9_MA11_HeadIsClippedAndCarriesTheAppDefaults()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        int mark = rig.Mark();
        _ = rig.Robot.Motion.SetHeadAngleAsync(99f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        var h = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        Assert.Equal(0.776672f, h.AngleRad);
        Assert.Equal(10f, h.MaxSpeedRadPerSec);
        Assert.Equal(20f, h.AccelRadPerSec2);
        Assert.Equal(0f, h.DurationSec);
        Assert.True(rig.Logged("MoveHeadToAngleAction.Constructor.AngleTooHigh"));
        mark = rig.Mark();
        _ = rig.Robot.Motion.SetHeadAngleAsync(-99f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        // before calibration the head reads −25° (MA22), so a clipped −25° target is already in position: nothing sent
        Assert.DoesNotContain(rig.SentSince(mark), m => m is SetHeadAngle);
    }

    /// <summary>
    /// M4-001 MA22/MA23 (RS6): robot+0x2FC is −0.436332 until the head is calibrated; then a report below −28°
    /// (−0.488692) is stored as −25°, above 47.5° (0.829031) as 44.5°, anything else as reported.
    /// </summary>
    [Fact]
    public void M4_001_MA22_RS6_TheHeadAngleIsMinus25UntilCalibratedThenClamped()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State(head: 0.3f);
        Assert.Equal(-0.436332f, rig.Robot.Motion.HeadAngleRad);
        rig.Calibrate();
        rig.State(head: 0.3f);
        Assert.Equal(0.3f, rig.Robot.Motion.HeadAngleRad);
        rig.State(head: 1.0f);
        Assert.Equal(0.776672f, rig.Robot.Motion.HeadAngleRad);
        rig.State(head: -0.47f);
        Assert.Equal(-0.47f, rig.Robot.Motion.HeadAngleRad);
        rig.State(head: -0.6f);
        Assert.Equal(-0.436332f, rig.Robot.Motion.HeadAngleRad);
        Assert.True(rig.Logged("HeadAngleOOB"));
    }

    /// <summary>
    /// M4-002 MA13/MA14: the presets are 32, 76, 92 and −1; a height in [0, ∞) outside [32, 92] is clamped with a
    /// warning; a negative height goes to whichever of 32 and 92 is nearer the current height; defaults 10/20/0 (MA11).
    /// </summary>
    [Fact]
    public void M4_002_MA13_MA14_LiftClampAndTheNegativeHeightGoesToTheNearerPreset()
    {
        Assert.Equal((32f, 76f, 92f, -1f), (CozmoMotion.LowDockHeightMm, CozmoMotion.HighDockHeightMm,
                                             CozmoMotion.CarryHeightMm, CozmoMotion.OutOfFovHeightMm));
        using var rig = new Rig();
        rig.ToSynced();
        float SentHeight(float ask, float liftAngle)
        {
            rig.State(liftAngle: liftAngle);
            int mark = rig.Mark();
            _ = rig.Robot.Motion.SetLiftHeightAsync(ask, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
            var l = Assert.IsType<SetLiftHeight>(rig.SentSince(mark).Single(m => m is SetLiftHeight));
            Assert.Equal((10f, 20f, 0f), (l.MaxSpeedRadPerSec, l.AccelRadPerSec2, l.DurationSec));
            return l.HeightMm;
        }
        // height = 66 sin(angle) + 45 (RS7): angle −0.1 is about 38.4 mm, angle 0.6 about 82.3 mm
        Assert.Equal(32f, SentHeight(10f, 0.6f));
        Assert.True(rig.Logged("MoveLiftToHeightAction.Init.InvalidHeight"));
        Assert.Equal(92f, SentHeight(150f, -0.1f));
        Assert.Equal(32f, SentHeight(-1f, -0.1f));
        Assert.Equal(92f, SentHeight(-1f, 0.6f));
        // C5 (0x00549104..0x0054913C): 32 only when strictly nearer; at 62 mm, halfway, it is 92
        Assert.Equal(92f, CozmoMotion.NegativeHeightTarget(62f));
        Assert.Equal(32f, CozmoMotion.NegativeHeightTarget(61.99f));
    }

    /// <summary>M2-015 (settled by M4 MD1): the MessageExtras defaults are the app's, head 10/20 and lift 10/20, duration 0.</summary>
    [Fact]
    public void M2_015_MD1_TheBuilderDefaultsAreTheAppsValues()
    {
        var h = new SetHeadAngle(0.1f);
        Assert.Equal((10f, 20f, 0f), (h.MaxSpeedRadPerSec, h.AccelRadPerSec2, h.DurationSec));
        var l = new SetLiftHeight(50f);
        Assert.Equal((10f, 20f, 0f), (l.MaxSpeedRadPerSec, l.AccelRadPerSec2, l.DurationSec));
    }

    // ------------------------------------------------------------------ M4-005, M4-016

    /// <summary>
    /// M4-005 MA8: one u8 counter shared by head and lift, 0 at construction and pre-incremented, so the ids run
    /// 1..255, 0, 1.
    /// </summary>
    [Fact]
    public void M4_005_MA8_ActionIdsRun1To255Then0AndAreShared()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        int mark = rig.Mark();
        for (int i = 0; i < 257; i++)
        {
            // before calibration the head reads −25° and the lift 45 mm, so neither target is in position
            if (i % 2 == 0) _ = rig.Robot.Motion.SetHeadAngleAsync(0.3f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
            else _ = rig.Robot.Motion.SetLiftHeightAsync(80f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        }
        var ids = rig.SentSince(mark).Select(m => m switch { SetHeadAngle h => (int?)h.ActionId, SetLiftHeight l => l.ActionId, _ => null })
                     .Where(x => x is not null).Select(x => x!.Value).ToList();
        var expected = Enumerable.Range(1, 255).Append(0).Append(1).ToList();
        Assert.Equal(expected, ids);
    }

    /// <summary>
    /// M4-016 MA15: nothing is sent when the head is within tolerance + 1e-5 of the target (the game tolerance
    /// 0.0349066, at least 2°), nor when the lift is within 5 mm and not moving (MC+0xB = !LIFT_IN_POS); the move
    /// then succeeds.
    /// </summary>
    [Fact]
    public async Task M4_016_MA15_NothingIsSentWhenAlreadyInPosition()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos, head: 0.3f, liftAngle: 0f);
        int mark = rig.Mark();
        var r = await rig.Robot.Motion.SetHeadAngleAsync(0.3f + 0.034f);
        Assert.True(r.Ok, r.Detail);
        var l = await rig.Robot.Motion.SetLiftHeightAsync(48f);                  // 45 mm now
        Assert.True(l.Ok, l.Detail);
        Assert.DoesNotContain(rig.SentSince(mark), m => m is SetHeadAngle or SetLiftHeight);
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.3f, liftAngle: 0f);   // the lift is moving
        _ = rig.Robot.Motion.SetLiftHeightAsync(48f, timeout: TimeSpan.FromMilliseconds(1));
        Assert.Contains(rig.SentSince(mark), m => m is SetLiftHeight);
    }

    /// <summary>
    /// M4-016 MA16/MA17: the ack counts only for the sent action's id; after it, the move succeeds once the head is in
    /// position and not moving (HEAD_IN_POS set). A state in position before the ack does not complete it.
    /// </summary>
    [Fact]
    public async Task M4_016_MA16_MA17_CompletionIsTheAckThenInPositionAndStopped()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos);
        int mark = rig.Mark();
        var pending = rig.Robot.Motion.SetHeadAngleAsync(0.3f, timeout: TimeSpan.FromSeconds(5));
        var sent = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.3f);
        Assert.False(pending.IsCompleted);                               // no ack yet
        rig.Data(new MotorActionAck { ActionId = (byte)(sent.ActionId + 1) }); rig.Tick();
        Assert.False(pending.IsCompleted);                               // another id
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        var r = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(r.Ok, r.Detail);
    }

    /// <summary>M4-016 MA17: after the ack, a head that moved and then stopped out of position fails with 0x04000004.</summary>
    [Fact]
    public async Task M4_016_MA17_StoppingOutOfPositionIsStoppedMakingProgress()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos);
        int mark = rig.Mark();
        var pending = rig.Robot.Motion.SetHeadAngleAsync(0.5f, timeout: TimeSpan.FromSeconds(5));
        var sent = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode, head: 0.1f);                              // moving
        Assert.False(pending.IsCompleted);
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.1f);  // stopped short
        var r = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(MotionResult.Failed, r.Result);
        Assert.Equal(0x04000004u, r.EngineResult);
    }

    /// <summary>
    /// M4-016 C1 (0x005485E4..0x005485F4, 0x0054872E..0x00548734, 0x00548738..0x005488AC): the head's in-position is
    /// latched, so a head that reached the target while still moving and then stopped out of tolerance succeeds;
    /// StoppedMakingProgress needs not in position, not moving and having moved.
    /// </summary>
    [Fact]
    public async Task M4_016_C1_TheHeadInPositionIsLatched()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos);
        int mark = rig.Mark();
        var pending = rig.Robot.Motion.SetHeadAngleAsync(0.5f, timeout: TimeSpan.FromSeconds(5));
        var sent = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode, head: 0.5f);                               // at the target, moving
        Assert.False(pending.IsCompleted);
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.4f);   // stopped outside 2 deg
        var r = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(r.Ok, r.Detail);
    }

    /// <summary>
    /// M4-016 R1/C1 (0x005485D8..0x005485E2 before the latch at 0x005485E4..0x005485F4): while the command is sent and not
    /// acked, CheckIfDone returns Running before it touches the latch, so a head passing through the target before the
    /// ack does not latch; after the ack, moving and then stopping short is StoppedMakingProgress.
    /// </summary>
    [Fact]
    public async Task M4_016_R1_PassingTheTargetBeforeTheAckDoesNotLatch()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos);
        int mark = rig.Mark();
        var pending = rig.Robot.Motion.SetHeadAngleAsync(0.5f, timeout: TimeSpan.FromSeconds(5));
        var sent = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        rig.State(flags: RobotStatusFlag.IsBodyAccMode, head: 0.5f);                               // at the target, before the ack
        rig.State(flags: RobotStatusFlag.IsBodyAccMode, head: 0.2f);                               // and past it, still before the ack
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode, head: 0.1f);                               // moving
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.1f);   // stopped short
        var r = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(MotionResult.Failed, r.Result);
        Assert.Equal(0x04000004u, r.EngineResult);
    }

    /// <summary>
    /// M4-016 C6 (rows L1..L6, 0x0054904C..0x0054932C, 0x005493F6..0x00549508): the lift waits for its ack (an in-position
    /// state before it does not complete it); after the ack it succeeds once in position and not moving (LIFT_IN_POS set,
    /// MC+0xB == 0); moving and then stopping out of position after having moved is 0x04000004.
    /// </summary>
    [Fact]
    public async Task M4_016_C6_TheLiftCompletion()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        var inPos = RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos;
        rig.State(flags: inPos, liftAngle: 0f);                                  // 45 mm
        int mark = rig.Mark();
        var ok = rig.Robot.Motion.SetLiftHeightAsync(80f, timeout: TimeSpan.FromSeconds(5));
        var sent = Assert.IsType<SetLiftHeight>(rig.SentSince(mark).Single(m => m is SetLiftHeight));
        float at80 = MathF.Asin((80f - 45f) / 66f);
        rig.State(flags: inPos, liftAngle: at80);
        Assert.False(ok.IsCompleted);                                              // sent, not acked: Running
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        Assert.True((await ok.WaitAsync(TimeSpan.FromSeconds(2))).Ok);

        mark = rig.Mark();
        var fail = rig.Robot.Motion.SetLiftHeightAsync(40f, timeout: TimeSpan.FromSeconds(5));
        sent = Assert.IsType<SetLiftHeight>(rig.SentSince(mark).Single(m => m is SetLiftHeight));
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, liftAngle: 0.3f);   // moving
        Assert.False(fail.IsCompleted);
        rig.State(flags: inPos, liftAngle: 0.3f);                                                     // stopped at 64 mm
        var r = await fail.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(MotionResult.Failed, r.Result);
        Assert.Equal(0x04000004u, r.EngineResult);
    }

    // ------------------------------------------------------------------ M4-007, M4-014, M4-015

    /// <summary>
    /// Queue item Q3 / M4-007 MA5: EmergencyStop runs StopAllMotors' unlock preamble (EnableAnimTracks for a track
    /// direct drive holds) before its StopAllMotors and this stack's extra DriveWheels(0) (the shutdown decision note).
    /// </summary>
    [Fact]
    public void M4_007_MA5_EmergencyStopUnlocksAHeldTrackFirst()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        _ = rig.Robot.Motion.DriveWheelsAsync(20f, 20f, confirmWithin: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        int mark = rig.Mark();
        rig.Robot.EmergencyStop();
        Assert.Equal(new[] { "9E04", "3B", Convert.ToHexString(new DriveWheels(0f, 0f, 0f, 0f).ToBytes()) },
                     rig.RawSince(mark).Select(Convert.ToHexString).ToList());
        Assert.Equal(0, rig.Robot.Motion.LockedTracks);
    }

    /// <summary>
    /// M4-014 MA1..MA3: direct drive locks its track while the speed is non-zero, sending DisableAnimTracks 0x9D {mask}
    /// once, and unlocks it at zero with EnableAnimTracks 0x9E {mask}; BODY 4 (speed |l| + |r|), HEAD 1, LIFT 2; the
    /// command itself follows verbatim. |speed| &lt; 1e-5 counts as zero.
    /// </summary>
    [Fact]
    public void M4_014_MA1_MA2_MA3_DirectDriveLocksAndUnlocksTheTracks()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var m = rig.Robot.Motion;
        int mark = rig.Mark();
        _ = m.DriveWheelsAsync(10f, -5f, confirmWithin: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        _ = m.DriveWheelsAsync(0f, 0f, confirmWithin: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        m.MoveLift(1f, requireCalibration: false);
        m.MoveLift(2f, requireCalibration: false);
        m.MoveLift(0f, requireCalibration: false);
        m.MoveHead(5e-6f, requireCalibration: false);
        var got = rig.RawSince(mark).Select(b => Convert.ToHexString(b)).ToList();
        Assert.Equal(new[]
        {
            "9D04", "32" + Convert.ToHexString(new DriveWheels(10f, -5f).ToBytes()[1..]),
            "9E04", "32" + Convert.ToHexString(new DriveWheels(0f, 0f).ToBytes()[1..]),
            "9D02", Convert.ToHexString(new MoveLift { SpeedRadPerSec = 1f }.ToBytes()),
            Convert.ToHexString(new MoveLift { SpeedRadPerSec = 2f }.ToBytes()),
            "9E02", Convert.ToHexString(new MoveLift { SpeedRadPerSec = 0f }.ToBytes()),
            Convert.ToHexString(new MoveHead { SpeedRadPerSec = 5e-6f }.ToBytes()),
        }, got);
    }

    /// <summary>
    /// M4-015 MA4 and M4-007 MA5: StopHead/StopLift/StopBody run the unlock preamble for a track direct drive holds, then
    /// send MoveHead{0}, MoveLift{0} or DriveWheels{0,0,0,0}; StopAllMotors runs the preamble for all three, then sends
    /// only StopAllMotors 0x3B (no DriveWheels).
    /// </summary>
    [Fact]
    public void M4_015_M4_007_MA4_MA5_TheStopsRunTheUnlockPreamble()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var m = rig.Robot.Motion;
        m.MoveHead(1f, requireCalibration: false);
        int mark = rig.Mark();
        m.StopHead();
        m.StopLift();
        m.StopBody();
        Assert.Equal(new[]
        {
            "9E01", Convert.ToHexString(new MoveHead { SpeedRadPerSec = 0f }.ToBytes()),
            Convert.ToHexString(new MoveLift { SpeedRadPerSec = 0f }.ToBytes()),
            Convert.ToHexString(new DriveWheels(0f, 0f, 0f, 0f).ToBytes()),
        }, rig.RawSince(mark).Select(Convert.ToHexString).ToList());

        m.MoveLift(1f, requireCalibration: false);
        _ = m.DriveWheelsAsync(5f, 5f, confirmWithin: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        mark = rig.Mark();
        m.StopAllMotors();
        var ids = rig.RawSince(mark).Select(b => b[0]).ToList();
        Assert.Equal(0x3B, ids[^1]);
        Assert.DoesNotContain((byte)0x32, ids);
        Assert.Equal(2, ids.Count(b => b == 0x9E));                    // BODY and LIFT unlocked first
        Assert.Equal(0, m.LockedTracks);
    }

    // ------------------------------------------------------------------ M4-017

    /// <summary>
    /// M4-017 LB1/LB2/LB3/LB4h: after the first state, with no source, every Robot::Update sends the Off lights: 0x03
    /// with LEDs 1..3 then 0x11 with LEDs 0 and 4, every LED `00 80 00 80 00 00 00 00 00 00`, then u8 0.
    /// </summary>
    [Fact]
    public void M4_017_LB1_LB2_LB3_TheOffLightsGoOutEveryTickWhileThereIsNoSource()
    {
        using var rig = new Rig();
        rig.ToSynced();
        int mark = rig.Mark();
        rig.Tick();
        Assert.DoesNotContain(rig.RawSince(mark), b => b[0] == 0x03);    // Robot::Update returns before the first full state
        rig.State();
        rig.Tick(); rig.Tick();
        const string led = "0080008000000000 0000";
        var middle = rig.RawSince(mark).Where(b => b[0] == 0x03).ToList();
        var turn = rig.RawSince(mark).Where(b => b[0] == 0x11).ToList();
        Assert.Equal(3, middle.Count);
        Assert.Equal(3, turn.Count);
        Assert.All(middle, b => Assert.Equal(Hex("03" + led + led + led + "00"), b));
        Assert.All(turn, b => Assert.Equal(Hex("11" + led + led + "00"), b));
        var seq = rig.RawSince(mark).Select(b => b[0]).Where(b => b is 0x03 or 0x11).ToList();
        Assert.Equal(new byte[] { 0x03, 0x11, 0x03, 0x11, 0x03, 0x11 }, seq);
    }

    /// <summary>
    /// M4-017 LB4, LB4c, LB4i, LB5: the charging state machine (on charger with IS_CHARGING → 1, off it with a good
    /// battery → 0 OffCharger, which the engine adds itself) shares one locator at source 2 with SetBackpackLEDs, so a
    /// change of charging state replaces the game's LEDs. Without the shipped JSON "Charging" does not resolve: a warning,
    /// the state still stored, nothing played. After OffCharger is looped the per-tick Off resend stops (LB4h).
    /// </summary>
    [Fact]
    public void M4_017_LB4_LB4i_LB5_TheChargingStateSharesTheLocatorWithSetBackpack()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger | RobotStatusFlag.IsCharging);
        Assert.Equal(1, rig.Robot.Lights.Body.ChargingState);
        Assert.True(rig.Logged("no pattern \"Charging\""));
        rig.Robot.Lights.SetBackpack(LedColor.Red);
        int mark = rig.Mark();
        rig.Tick();
        var red = Assert.Single(rig.RawSince(mark), b => b[0] == 0x03);
        Assert.Equal(new[] { LedColor.Red.Packed, LedColor.Red.Packed, LedColor.Red.Packed },
                     ((BackpackLightsMiddle)RobotMessage.Parse(red)).Field0.Select(l => l.OnColor));
        rig.Tick();
        Assert.Single(rig.RawSince(mark), b => b[0] == 0x03);            // unchanged: not resent
        mark = rig.Mark();
        rig.State(battery: 4.0f);                                          // off the charger: OffCharger replaces red
        Assert.Equal(0, rig.Robot.Lights.Body.ChargingState);
        var off = Assert.Single(rig.RawSince(mark), b => b[0] == 0x03);
        Assert.Equal(Hex("03" + string.Concat(Enumerable.Repeat("00800080000000000000", 3)) + "00"), off);
        rig.Tick(); rig.Tick();
        Assert.Single(rig.RawSince(mark), b => b[0] == 0x03);
        rig.State(battery: 3.4f);                                          // LB4: off the charger below 3.5 V → 3
        Assert.Equal(3, rig.Robot.Lights.Body.ChargingState);
    }

    /// <summary>
    /// M4-017 LB4b/LB4d/LB4f/LB4g with the shipped backpackLightPatterns.json: Charging on the wire is L1
    /// `E0 81 00 80 0A 1E 0A 0A E3 FF`, L2 `E0 81 00 80 1E 0A 0A 0A F7 FF`, L3 `E0 81 E0 81 00 00 0A 0A 00 00`, LEDs 0
    /// and 4 unlit. Skipped visibly when the OBB is not on this machine.
    /// </summary>
    [Fact]
    public void M4_017_LB4g_ChargingOnTheWireFromTheShippedPatterns()
    {
        var res = ObbResources();
        if (res is null) { Console.WriteLine("SKIPPED: no OBB (re-analysis/obb) on this machine"); return; }
        using var rig = new Rig(res);
        Assert.Contains("Charging", rig.Robot.Lights.Body.Animations.Names);
        rig.ToSynced();
        int mark = rig.Mark();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger | RobotStatusFlag.IsCharging);
        var sent = rig.RawSince(mark).Where(b => b[0] is 0x03 or 0x11).ToList();
        Assert.Equal(Hex("03 E0810080 0A1E0A0A E3FF  E0810080 1E0A0A0A F7FF  E081E081 00000A0A 0000  00"), sent[0]);
        Assert.Equal(Hex("11 00800080000000000000 00800080000000000000 00"), sent[1]);
    }

    // ------------------------------------------------------------------ M4-018

    /// <summary>
    /// M4-018 LC1..LC6 (S5..S8): when a light cube connects the engine plays WakeUp on layer 2 and sends, reliable and in
    /// this order, SetCubeGamma {0x80} (the cache starts at 0), CubeID {activeID = the slot, rotation 0} and CubeLights
    /// with each LED `E0 83 00 80 01 0D 07 02 oo 00`, oo = 00, 04, 07, 0A. When the 1280 ms spin expires the fadeOut
    /// pattern follows (CubeID + CubeLights `E0 83 00 80 22 64 04 22 00 00` ×4, no gamma); 5100 ms later the animation
    /// ends and the default layer-2 animation (Connected) is asked for.
    /// </summary>
    [Fact]
    public void M4_018_LC1_LC6_WakeUpOnConnection()
    {
        var res = WakeUpResources();
        try
        {
            using var rig = new Rig(res);
            rig.ToSynced();
            rig.State();
            int mark = rig.Mark();
            rig.Data(new ObjectConnectionState { ObjectID = 2, FactoryID = 0x241A8EEC, ObjectType = ObjectType.Block_LIGHTCUBE2, Connected = true });
            rig.Tick();
            var cube = rig.RawSince(mark).Where(b => b[0] is 0x0C or 0x10 or 0x04).ToList();
            Assert.Equal(3, cube.Count);
            Assert.Equal(Hex("0C 80"), cube[0]);
            Assert.Equal(Hex("10 02000000 00"), cube[1]);
            Assert.Equal(Hex("04 E083008001 0D070200 00 E083008001 0D070204 00 E083008001 0D070207 00 E083008001 0D07020A 00"), cube[2]);

            mark = rig.Mark();
            for (int i = 0; i < 20; i++) rig.Tick();                     // 1200 ms: the spin has not expired
            Assert.DoesNotContain(rig.RawSince(mark), b => b[0] is 0x10 or 0x04);
            rig.Tick(); rig.Tick();                                       // past 1280 ms
            var fade = rig.RawSince(mark).Where(b => b[0] is 0x0C or 0x10 or 0x04).ToList();
            Assert.Equal(2, fade.Count);
            Assert.Equal(Hex("10 02000000 00"), fade[0]);
            Assert.Equal(Hex("04" + string.Concat(Enumerable.Repeat("E0830080226404220000", 4))), fade[1]);

            Assert.False(rig.Logged("NoAnimForTrigger Connected"));
            for (int i = 0; i < 86; i++) rig.Tick();                     // 5160 ms more
            Assert.True(rig.Logged("NoAnimForTrigger Connected"));        // LC3: the default layer-2 pick is Connected
        }
        finally { Directory.Delete(res, true); }
    }

    /// <summary>
    /// M4-018 LC2 (c), LC3 with the shipped assets: trigger WakeUp maps to "wakeUp" (CubeAnimationTriggerMap.json), whose
    /// two patterns are 1280 ms (overridable by default) and 5100 ms (canBeOverridden false); the default-layer triggers
    /// resolve too. Skipped visibly when the OBB is not on this machine.
    /// </summary>
    [Fact]
    public void M4_018_LC3_TheShippedWakeUpAnimationLoads()
    {
        var res = ObbResources();
        if (res is null) { Console.WriteLine("SKIPPED: no OBB (re-analysis/obb) on this machine"); return; }
        var anims = CubeLightAnimations.Load(res);
        var wake = anims.ForTrigger("WakeUp")!;
        Assert.Equal(new uint[] { 1280, 5100 }, wake.Select(p => p.DurationMs));
        Assert.Equal(new[] { true, false }, wake.Select(p => p.CanBeOverridden));
        Assert.Equal(new[] { "wakeUp_spin", "wakeUp_fadeOut" }, wake.Select(p => p.DebugName));
        Assert.NotNull(anims.ForTrigger("Connected"));
        Assert.NotNull(anims.ForTrigger("Visible"));
        Assert.NotNull(anims.ForTrigger("Carrying"));
    }

    /// <summary>
    /// M4-018 LC8/LC1: a disconnection sends nothing and keeps the ObjectInfo; the reconnection plays WakeUp again, which
    /// is sent at once, without SetCubeGamma since the cache already holds 0x80.
    /// </summary>
    [Fact]
    public void M4_018_LC8_AReconnectReplaysWakeUpWithoutGamma()
    {
        var res = WakeUpResources();
        try
        {
            using var rig = new Rig(res);
            rig.ToSynced();
            rig.State();
            var conn = new ObjectConnectionState { ObjectID = 0, FactoryID = 0x241A8EEC, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true };
            rig.Data(conn); rig.Tick();
            int mark = rig.Mark();
            rig.Data(new ObjectConnectionState { ObjectID = 0, FactoryID = 0x241A8EEC, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = false });
            rig.Tick();
            Assert.DoesNotContain(rig.RawSince(mark), b => b[0] is 0x0C or 0x10 or 0x04);
            rig.Data(conn); rig.Tick();
            var again = rig.RawSince(mark).Where(b => b[0] is 0x0C or 0x10 or 0x04).Select(b => b[0]).ToList();
            Assert.Equal(new byte[] { 0x10, 0x04 }, again);
        }
        finally { Directory.Delete(res, true); }
    }

    /// <summary>
    /// M4-018 LC4/LC5: WhiteBalanceColor scales G and B by 0.6, truncating, when R ≠ 0; frames are u8((ms + 29) / 30)
    /// with 0xFFFFFFFF → 0xFF and a solid 0x7FFFFFFF → 0x45; LB4f's backpack offset maps −1 to 0x00FF.
    /// </summary>
    [Fact]
    public void M4_018_LC4_LC5_LB4f_TheColourAndFrameConversions()
    {
        Assert.Equal(new LedColor(255, 153, 153), CubeLightComponent.WhiteBalance(new LedColor(255, 255, 255)));
        Assert.Equal(new LedColor(0, 255, 255), CubeLightComponent.WhiteBalance(new LedColor(0, 255, 255)));
        Assert.Equal(new LedColor(10, 60, 0), CubeLightComponent.WhiteBalance(new LedColor(10, 101, 0)));
        Assert.Equal(0x45, LightWire.Frames(0x7FFFFFFF));
        Assert.Equal(0xFF, LightWire.Frames(0xFFFFFFFF));
        Assert.Equal(13, LightWire.Frames(380));
        Assert.Equal((short)0x00FF, LightWire.BackpackOffset(-1));
        Assert.Equal((short)-29, LightWire.BackpackOffset(-900));
        Assert.Equal((short)10, LightWire.CubeOffset(300));
    }

    // ------------------------------------------------------------------ M4-009, M4-023

    /// <summary>
    /// M4-023 CD10g (0x006311A2..0x006313D8) and M4-009 CD10a step 3 (0x00533E4C..0x005341BA): a Moved inside the
    /// double-tap window returns before SetIsMoving and MarkObjectDirty, so a located object stays Known; when the
    /// pending entry ends, BTF Update marks the located copy dirty. (The located object here is a markerless one whose
    /// id is reused as the cube's slot: this stack's world ids are the slots, and a Known object is what MarkDirty acts on.)
    /// </summary>
    [Fact]
    public void M4_023_CD10g_TheDoubleTapWindowReachesTheWorld()
    {
        using var rig = new Rig();
        using var vision = new VisionSystem(rig.Robot, CameraCalibration.Nominal()) { Enabled = false };
        rig.ToSynced();
        rig.State();
        var located = vision.World.AddMarkerlessObject(Pose3d.Identity, ObjectType.CollisionObstacle);
        uint id = located.ObjectId;
        ConnectCube(rig, slot: id);
        Assert.Equal(PoseState.Known, located.PoseState);
        rig.Robot.Cubes.SetBlockTapFilterEnabled(false);
        rig.Data(new ObjectTapped { Timestamp = 1, ObjectID = id, TapNeg = -40, TapPos = 40 }); rig.Tick();
        rig.Data(new ObjectMoved { Timestamp = 2, ObjectID = id }); rig.Tick();
        Assert.Equal(PoseState.Known, located.PoseState);                // the Moved inside the window stopped early
        Assert.False(located.IsMoving);
        rig.Tick(300); rig.Tick(300);                                     // the pending entry ends
        Assert.Equal(PoseState.Dirty, located.PoseState);
    }

    private static void ConnectCube(Rig rig, uint slot = 0, ObjectType type = ObjectType.Block_LIGHTCUBE1)
    {
        rig.Data(new ObjectConnectionState { ObjectID = slot, FactoryID = 0x1000 + slot, ObjectType = type, Connected = true });
        rig.Tick();
    }

    /// <summary>
    /// M4-009 CD10a/CD10b/CD10c: Moved and Stopped are broadcast for every message (SetIsMoving only on a change), an
    /// unknown active id is dropped with a warning, and UpAxisChanged for an unknown id is an error.
    /// </summary>
    [Fact]
    public void M4_009_CD10a_CD10b_CD10c_MovedAndStoppedAreBroadcastPerMessage()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        ConnectCube(rig);
        var moved = new List<bool>();
        rig.Robot.Cubes.CubeMoved += c => moved.Add(c.Moving);
        rig.Data(new ObjectMoved { Timestamp = 10, ObjectID = 0 });
        rig.Data(new ObjectMoved { Timestamp = 11, ObjectID = 0 });
        rig.Data(new ObjectStoppedMoving { Timestamp = 12, ObjectID = 0 });
        rig.Data(new ObjectMoved { Timestamp = 13, ObjectID = 3 });
        rig.Data(new ObjectUpAxisChanged { Timestamp = 14, ObjectID = 3 });
        rig.Tick();
        Assert.Equal(new[] { true, true, false }, moved);
        Assert.Equal(12u, rig.Robot.Cubes.ByObjectId(0)!.MovingChangedAt);
        Assert.True(rig.Logged("HandleActiveObjectMoved: unknown active id 3"));
        Assert.True(rig.Logged("HandleActiveObjectUpAxisChanged: unknown active id 3"));
    }

    /// <summary>
    /// M4-023 CD10e/CD10f: intensity = tapPos − tapNeg; ≤ 60 is dropped; on a physical robot (HandleFirmwareVersion with
    /// no "sim", SetPhysicalRobot) with the filter on, taps queue for 75 ms from the first and only the strongest is
    /// broadcast, the earliest winning a tie.
    /// </summary>
    [Fact]
    public void M4_023_CD10e_CD10f_TapsAreFilteredAndOnlyTheStrongestIsBroadcast()
    {
        using var rig = new Rig();
        rig.ToSynced();
        Assert.True(rig.Engine.Robot!.IsPhysicalRobot);
        rig.State();
        ConnectCube(rig);
        var taps = new List<ObjectTapped>();
        rig.Robot.Cubes.CubeTapped += c => taps.Add(c.LastTap!);
        rig.Data(new ObjectTapped { Timestamp = 1, ObjectID = 0, TapNeg = -30, TapPos = 30 });      // 60: dropped
        rig.Tick(10);
        rig.Data(new ObjectTapped { Timestamp = 2, ObjectID = 0, TapNeg = -40, TapPos = 30 });      // 70: queued, deadline +75
        rig.Tick(10);
        rig.Data(new ObjectTapped { Timestamp = 3, ObjectID = 0, TapNeg = -50, TapPos = 40 });      // 90
        rig.Tick(10);
        rig.Data(new ObjectTapped { Timestamp = 4, ObjectID = 0, TapNeg = -45, TapPos = 45 });      // 90, later
        rig.Tick(10);
        Assert.Empty(taps);
        rig.Tick(60);
        Assert.Equal(3u, Assert.Single(taps).Timestamp);
        Assert.True(rig.Logged("Tap ignored 60 <= 60"));
    }

    /// <summary>
    /// M4-023 CD10d/CD10e/CD10g: with the filter off a tap is broadcast at once; the tap opens a 500 ms window in which a
    /// Moved message is ignored; after it movement is reported again.
    /// </summary>
    [Fact]
    public void M4_023_CD10d_CD10g_TheDoubleTapWindowSuppressesMovement()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        ConnectCube(rig);
        rig.Robot.Cubes.SetBlockTapFilterEnabled(false);
        var taps = 0; var moves = 0;
        rig.Robot.Cubes.CubeTapped += _ => taps++;
        rig.Robot.Cubes.CubeMoved += _ => moves++;
        rig.Data(new ObjectTapped { Timestamp = 1, ObjectID = 0, TapNeg = -40, TapPos = 40 });
        rig.Tick();
        Assert.Equal(1, taps);
        rig.Data(new ObjectMoved { Timestamp = 2, ObjectID = 0 });
        rig.Tick(300);
        Assert.Equal(0, moves);
        Assert.False(rig.Robot.Cubes.ByObjectId(0)!.Moving);
        rig.Tick(300);                                                     // past the 500 ms window
        rig.Data(new ObjectMoved { Timestamp = 3, ObjectID = 0 });
        rig.Tick();
        Assert.Equal(1, moves);
        Assert.True(rig.Robot.Cubes.ByObjectId(0)!.Moving);
    }
}
