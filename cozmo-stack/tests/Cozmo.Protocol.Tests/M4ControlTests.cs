using System.Net;
using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
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
    // Opus M4-001: literal strings BEA11C/BEA169; getDegrees f32 0084CD40.
    [Fact]
    public void M4_001_CheckedClipWarningsUseRescaledDegreesAndInvariantOneDecimal()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            var commaCulture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
            commaCulture.NumberFormat.NumberDecimalSeparator = ",";
            System.Globalization.CultureInfo.CurrentCulture = commaCulture;
            _ = rig.Robot.Motion.SetHeadAngleAsync(-1f);
            Assert.True(rig.Logged("warning: MoveHeadToAngleAction.Constructor.AngleTooLow: Requested head angle (-57.3deg) less than min head angle (-25.0deg). Clipping."));
            _ = rig.Robot.Motion.SetHeadAngleAsync(1f);
            Assert.True(rig.Logged("warning: MoveHeadToAngleAction.Constructor.AngleTooHigh: Requested head angle (57.3deg) more than max head angle (44.5deg). Clipping."));
            _ = rig.Robot.Motion.SetHeadAngleAsync(4f); // rescaled into (-pi,pi]: -2.28318548 => -130.8deg
            Assert.True(rig.Logged("Requested head angle (-130.8deg) less than min head angle (-25.0deg). Clipping."), string.Join("\n", rig.Log));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = saved; }
    }

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

    /// <summary>
    /// A resources directory with one single-pattern animation per default-layer trigger (S1/S2) and the trigger map
    /// that names them, so PickNextAnimForDefaultLayer can be observed by the pattern's debug name.
    /// </summary>
    private static string CubeLightResources()
    {
        var root = Path.Combine(Path.GetTempPath(), "m4-cube-sleep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "assets", "cubeAnimationGroupMaps"));
        Directory.CreateDirectory(Path.Combine(root, "config", "engine", "lights", "cubeLights"));
        File.WriteAllText(Path.Combine(root, "assets", "cubeAnimationGroupMaps", "CubeAnimationTriggerMap.json"),
            """
            { "Pairs": [
              { "CladEvent": "WakeUp", "AnimName": "wakeUp" },
              { "CladEvent": "Sleep", "AnimName": "sleep" },
              { "CladEvent": "SleepNoFade", "AnimName": "sleepNoFade" },
              { "CladEvent": "Connected", "AnimName": "connected" },
              { "CladEvent": "Carrying", "AnimName": "carrying" },
              { "CladEvent": "Visible", "AnimName": "visible" } ] }
            """);
        static string Anim(string name, string debug) => $$"""
              "{{name}}": [
                { "pattern": { "onColors": [[0,255,0,255],[0,255,0,255],[0,255,0,255],[0,255,0,255]],
                               "offColors": [[0,0,0,255],[0,0,0,255],[0,0,0,255],[0,0,0,255]],
                               "onPeriod_ms": [10,10,10,10], "offPeriod_ms": [10,10,10,10],
                               "transitionOnPeriod_ms": [0,0,0,0], "transitionOffPeriod_ms": [0,0,0,0],
                               "offset": [0,0,0,0], "rotationPeriod_ms": 0 },
                  "duration_ms": 100, "patternDebugName": "{{debug}}" }
              ]
            """;
        File.WriteAllText(Path.Combine(root, "config", "engine", "lights", "cubeLights", "anims.json"),
            "{\n" + string.Join(",\n", Anim("wakeUp", "wakeUp"), Anim("sleep", "sleep"), Anim("sleepNoFade", "sleepNoFade"),
                Anim("connected", "connected"), Anim("carrying", "carrying"), Anim("visible", "visible")) + "\n}");
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

    /// <summary>
    /// M4-008 SC2 / U1..U4: UpdateRobotData runs after the +0x29 time-sync gate and before the origin check, so the
    /// raw values (+0xE), CLIFF_DETECTED (+6) and the timestamp (+8) are stored for every time-synced state, including
    /// one the origin check later drops (origin 0 is never in the pose-origin list, SC4g).
    /// </summary>
    [Fact]
    public void M4_008_SC2_TheCliffDataOfAHandledStateIsStored()
    {
        using var rig = new Rig();
        rig.ToSynced();
        var st = rig.MakeState(RobotStatusFlag.CliffDetected, origin: 0);   // rejected at the origin check
        st.CliffDataRaw = new ushort[] { 11, 22, 33, 44 };
        rig.Data(st); rig.Tick();
        Assert.Null(rig.Engine.Robot!.AcceptedState);                       // the state was dropped at the origin check
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
    /// M11-044 / M11-053 (0x00512B88..0x00512BA6, 0x00512FB4): a state that triggers Delocalize (a treads commit involving OnTreads) jumps over 0x00512D7A..0x00512EA6, so it sends no
    /// SetCliffDetectThreshold (0x00512DA2, 0x00512EA0); the next ordinary state does send the first {50}.
    /// </summary>
    [Fact]
    public void M11_044_ADelocalizingStateSendsNoCliffThresholdAndTheNextOrdinaryOneDoes()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        int mark = rig.Mark();
        List<byte[]> Thresholds() => rig.RawSince(mark).Where(b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold).ToList();
        rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsPickedUp);          // OnTreads -> InAir: r7 = 1
        Assert.Equal(OffTreadsState.InAir, rig.Robot.Sensors.OffTreadsState);
        Assert.Empty(Thresholds());
        rig.State(RobotStatusFlag.IsBodyAccMode);                                        // InAir -> OnTreads: r7 = 1
        Assert.Equal(OffTreadsState.OnTreads, rig.Robot.Sensors.OffTreadsState);
        Assert.Empty(Thresholds());
        rig.State(RobotStatusFlag.IsBodyAccMode);                                        // no commit
        Assert.Equal(Hex("3200"), Body(Assert.Single(Thresholds())));
    }

    /// <summary>
    /// M11-044 (0x00512A62..0x00512A94): a commit where neither the old nor the new state is OnTreads (InAir to OnBack) gives r7 = 0: no Delocalize, the history takes the state and the
    /// threshold schedule runs (the first accepted ordinary state sends {50}).
    /// </summary>
    [Fact]
    public void M11_044_ACommitBetweenTwoOffTreadsStatesDoesNotDelocalize()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        using var vision = new VisionSystem(rig.Robot, CameraCalibration.Nominal());
        int mark = rig.Mark();
        rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsPickedUp);          // OnTreads -> InAir (a Delocalize state)
        Assert.Equal(OffTreadsState.InAir, rig.Robot.Sensors.OffTreadsState);
        var o = new ObservableObject(42, ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1));
        vision.World.SetLocalizedTo(o);
        int historyBefore = vision.History.Count;
        float back = (float)OffTreadsClassifier.OnBackCentrePhysicalRad;
        int fed = 0;
        for (; fed < 200 && rig.Robot.Sensors.OffTreadsState != OffTreadsState.OnBack; fed++)
        {
            var st = rig.MakeState(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsPickedUp);
            st.Pose = new RobotPose { Pitch = back };
            rig.Data(st); rig.Tick(60);
            Assert.Equal(42u, vision.World.LocalizedToObjectId);                           // never delocalized on the way, nor at the commit
        }
        Assert.Equal(OffTreadsState.OnBack, rig.Robot.Sensors.OffTreadsState);
        Assert.Equal(historyBefore + fed, vision.History.Count);                            // every one of those states reached the history
        Assert.Contains(rig.RawSince(mark), b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold);   // the schedule ran
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
    /// M4-001 MA9 (2026-09-29 audit): the Radians ctor rescales a commanded angle into (−π, π] first
    /// (0x0084C832 → 0x0084C87C), then MoveHeadToAngleAction clips the rescaled angle against the engine's float
    /// limits, 0xBEDF66F3 min and 0x3F46D3F2 max (0x00547F44/0x00547FC2), with a warning. So 99 rad rescales to
    /// 99 − 16·2π = −1.5310 and clips to the min with AngleTooLow, and −99 rad rescales to +1.5310 and clips to
    /// the max with AngleTooHigh. M4-003 MD1/MA11: the defaults are head 10 rad/s, 20 rad/s², duration 0.
    /// </summary>
    [Fact]
    public async Task M4_001_M4_003_MA9_MA11_HeadIsClippedAndCarriesTheAppDefaults()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        // 0.3 rad with HEAD_IN_POS: not in position for either clipped target, so both moves send.
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.3f);
        int mark = rig.Mark();
        var first = rig.Robot.Motion.SetHeadAngleAsync(99f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        rig.Tick();                                                    // M4-003/Q4: NOW is Init'd on the ActionList tick
        var h = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        Assert.Equal(0xBEDF66F3u, BitConverter.SingleToUInt32Bits(h.AngleRad));   // 99 rad → −1.5310 → min, 0xBEDF66F3
        Assert.Equal(10f, h.MaxSpeedRadPerSec);
        Assert.Equal(20f, h.AccelRadPerSec2);
        Assert.Equal(0f, h.DurationSec);
        Assert.True(rig.Logged("MoveHeadToAngleAction.Constructor.AngleTooLow"));
        rig.Tick();                                                    // M4-016: the 1 ms timeout fires on the engine clock
        await first;
        mark = rig.Mark();
        var second = rig.Robot.Motion.SetHeadAngleAsync(-99f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        rig.Tick();
        h = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        Assert.Equal(0x3F46D3F2u, BitConverter.SingleToUInt32Bits(h.AngleRad));   // −99 rad → +1.5310 → max, 0x3F46D3F2
        Assert.True(rig.Logged("MoveHeadToAngleAction.Constructor.AngleTooHigh"));
        rig.Tick();
        await second;
    }

    /// <summary>
    /// M4-001 (0x0084CD12, 0x0084CC90..0x0084CCD0, IsNear 0x0084CC0A): the clip is <c>operator&lt;</c>/<c>&gt;</c>,
    /// true only when a − b &gt; 0 and not IsNear(a, b, 1e-5 bits 0x3727C5AC). A target within 1e-5 past a limit is
    /// therefore sent unclipped and without a warning; past it, the limit is sent with the warning.
    /// </summary>
    [Fact]
    public async Task M4_001_M4_016_TheClipUsesTheOneEpsilonNearTest()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.3f);
        float min = CozmoMotion.MinHeadAngleRad, max = CozmoMotion.MaxHeadAngleRad;
        int mark = rig.Mark();
        var a = rig.Robot.Motion.SetHeadAngleAsync(min - 1e-6f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        rig.Tick();
        var h = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        Assert.Equal(BitConverter.SingleToUInt32Bits(min - 1e-6f), BitConverter.SingleToUInt32Bits(h.AngleRad));
        Assert.False(rig.Logged("MoveHeadToAngleAction.Constructor.AngleTooLow"));
        rig.Tick(); await a;
        mark = rig.Mark();
        var b = rig.Robot.Motion.SetHeadAngleAsync(max + 1e-6f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        rig.Tick();
        h = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        Assert.Equal(BitConverter.SingleToUInt32Bits(max + 1e-6f), BitConverter.SingleToUInt32Bits(h.AngleRad));
        Assert.False(rig.Logged("MoveHeadToAngleAction.Constructor.AngleTooHigh"));
        rig.Tick(); await b;
        mark = rig.Mark();
        var c = rig.Robot.Motion.SetHeadAngleAsync(min - 1e-4f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        rig.Tick();
        h = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        Assert.Equal(BitConverter.SingleToUInt32Bits(min), BitConverter.SingleToUInt32Bits(h.AngleRad));
        Assert.True(rig.Logged("MoveHeadToAngleAction.Constructor.AngleTooLow"));
        rig.Tick(); await c;
    }

    /// <summary>
    /// M4-001 (0x0084C91E..0x0084C922): the rescale shortcut's <c>vcvt.s32.f32</c>/<c>vcvt.f32.s32</c> round trip
    /// saturates the ceil turn count, so ±inf stays ±inf (not inf − inf = NaN) and a huge finite value keeps its
    /// magnitude; the clip then sends the limit with the warning instead of NaN.
    /// </summary>
    [Fact]
    public async Task M4_001_M4_016_InfinityAndHugeAnglesClipInsteadOfSendingNaN()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.3f);
        float min = CozmoMotion.MinHeadAngleRad, max = CozmoMotion.MaxHeadAngleRad;
        int mark = rig.Mark();
        var a = rig.Robot.Motion.SetHeadAngleAsync(float.PositiveInfinity, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        rig.Tick();
        var h = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        Assert.Equal(BitConverter.SingleToUInt32Bits(max), BitConverter.SingleToUInt32Bits(h.AngleRad));
        Assert.True(rig.Logged("MoveHeadToAngleAction.Constructor.AngleTooHigh"));
        rig.Tick(); await a;
        mark = rig.Mark();
        var b = rig.Robot.Motion.SetHeadAngleAsync(float.NegativeInfinity, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        rig.Tick();
        h = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        Assert.Equal(BitConverter.SingleToUInt32Bits(min), BitConverter.SingleToUInt32Bits(h.AngleRad));
        Assert.True(rig.Logged("MoveHeadToAngleAction.Constructor.AngleTooLow"));
        rig.Tick(); await b;
        mark = rig.Mark();
        var c = rig.Robot.Motion.SetHeadAngleAsync(1e30f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        rig.Tick();
        h = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        Assert.Equal(BitConverter.SingleToUInt32Bits(max), BitConverter.SingleToUInt32Bits(h.AngleRad));
        Assert.True(rig.Logged("MoveHeadToAngleAction.Constructor.AngleTooHigh"));
        rig.Tick(); await c;
    }

    /// <summary>
    /// M4-001 MA22/MA23 (RS6): robot+0x2FC is the engine float 0xBEDF66F3 (−25°) until the head is calibrated;
    /// then a report below 0xBEFA35DD (−28°) is stored as −25°, above 0x3F543B67 (47.5°) as 44.5°, anything else
    /// as reported.
    /// </summary>
    [Fact]
    public void M4_001_MA22_RS6_TheHeadAngleIsMinus25UntilCalibratedThenClamped()
    {
        using var rig = new Rig();
        float min = BitConverter.Int32BitsToSingle(unchecked((int)0xBEDF66F3));
        float max = BitConverter.Int32BitsToSingle(unchecked((int)0x3F46D3F2));
        rig.ToSynced();
        rig.State(head: 0.3f);
        Assert.Equal(min, rig.Robot.Motion.HeadAngleRad);
        rig.Calibrate();
        rig.State(head: 0.3f);
        Assert.Equal(0.3f, rig.Robot.Motion.HeadAngleRad);
        rig.State(head: 1.0f);
        Assert.Equal(max, rig.Robot.Motion.HeadAngleRad);
        rig.State(head: -0.47f);
        Assert.Equal(-0.47f, rig.Robot.Motion.HeadAngleRad);
        rig.State(head: -0.6f);
        Assert.Equal(min, rig.Robot.Motion.HeadAngleRad);
        Assert.True(rig.Logged("HeadAngleOOB"));
    }

    /// <summary>
    /// M4-002 MA13/MA14: the presets are 32, 76, 92 and −1; a height in [0, ∞) outside [32, 92] is clamped with a
    /// warning; a negative height goes to whichever of 32 and 92 is nearer the current height; defaults 10/20/0 (MA11).
    /// </summary>
    [Fact]
    public async Task M4_002_MA13_MA14_LiftClampAndTheNegativeHeightGoesToTheNearerPreset()
    {
        Assert.Equal((32f, 76f, 92f, -1f), (CozmoMotion.LowDockHeightMm, CozmoMotion.HighDockHeightMm,
                                             CozmoMotion.CarryHeightMm, CozmoMotion.OutOfFovHeightMm));
        using var rig = new Rig();
        rig.ToSynced();
        async Task<float> SentHeight(float ask, float liftAngle)
        {
            rig.State(liftAngle: liftAngle);
            int mark = rig.Mark();
            // M4-003: the action holds the LIFT track lock until it ends; M4-016: the 1 ms timeout is on the engine
            // clock, so tick to end it before the next move.
            var pending = rig.Robot.Motion.SetLiftHeightAsync(ask, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
            rig.Tick();
            var l = Assert.IsType<SetLiftHeight>(rig.SentSince(mark).Single(m => m is SetLiftHeight));
            Assert.Equal((10f, 20f, 0f), (l.MaxSpeedRadPerSec, l.AccelRadPerSec2, l.DurationSec));
            rig.Tick();
            await pending;
            return l.HeightMm;
        }
        // height = 66 sin(angle) + 45 (RS7): angle −0.1 is about 38.4 mm, angle 0.6 about 82.3 mm
        Assert.Equal(32f, await SentHeight(10f, 0.6f));
        Assert.True(rig.Logged("MoveLiftToHeightAction.Init.InvalidHeight"));
        Assert.Equal(92f, await SentHeight(150f, -0.1f));
        Assert.Equal(32f, await SentHeight(-1f, -0.1f));
        Assert.Equal(92f, await SentHeight(-1f, 0.6f));
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
    public async Task M4_005_MA8_ActionIdsRun1To255Then0AndAreShared()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        int mark = rig.Mark();
        for (int i = 0; i < 257; i++)
        {
            // before calibration the head reads −25° and the lift 45 mm, so neither target is in position.
            // M4-003: each move holds its track lock until it ends; M4-016: the 1 ms timeout is on the engine
            // clock, so tick to end it before the next. The counter still advances once per accepted action.
            Task<MotionOutcome> pending;
            if (i % 2 == 0) pending = rig.Robot.Motion.SetHeadAngleAsync(0.3f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
            else pending = rig.Robot.Motion.SetLiftHeightAsync(80f, timeout: TimeSpan.FromMilliseconds(1), requireCalibration: false);
            rig.Tick();                                     // the ActionList tick sends
            rig.Tick();                                     // the next tick fires the 1 ms timeout
            await pending;
        }
        var ids = rig.SentSince(mark).Select(m => m switch { SetHeadAngle h => (int?)h.ActionId, SetLiftHeight l => l.ActionId, _ => null })
                     .Where(x => x is not null).Select(x => x!.Value).ToList();
        var expected = Enumerable.Range(1, 255).Append(0).Append(1).ToList();
        Assert.Equal(expected, ids);
    }

    /// <summary>
    /// M4-003 (0x00540572..0x0054057C): IActionRunner::Update refuses a move whose required track is already
    /// locked and fails it with the engine result 0x03000019, sending nothing.
    /// </summary>
    [Fact]
    public async Task M4_003_MA_ALockedTrackFailsTheHeadMoveWith03000019()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.3f);
        rig.Robot.Motion.LockTracks(CozmoMotion.HeadTrack, "someone");   // DisableAnimTracks 0x9D {1}, before the mark
        int mark = rig.Mark();
        var r = rig.Robot.Motion.SetHeadAngleAsync(0.5f, timeout: TimeSpan.FromMilliseconds(50));
        rig.Tick();                                       // L5: the locked required track fails 0x03000019
        Assert.Equal(MotionResult.Failed, (await r).Result);
        Assert.Equal(0x03000019u, (await r).EngineResult);
        Assert.DoesNotContain(rig.SentSince(mark), m => m is SetHeadAngle);
    }

    /// <summary>
    /// M4-003 (lock 0x0054058E, unlock inline in ~IActionRunner 0x0054121E..0x0054122A; 0x005408EC is
    /// IActionRunner::UnlockTracks from the IAction ctor/Reset): a head move that runs takes the HEAD track lock, which sends
    /// DisableAnimTracks 0x9D {1}, before its SetHeadAngle, and releases it at the action's end, which sends
    /// EnableAnimTracks 0x9E {1}. The in-position move takes and releases it too (M4-016 unresolved).
    /// </summary>
    [Fact]
    public async Task M4_003_MA_TheHeadMoveLocksThenUnlocksItsTrack()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.3f);
        int mark = rig.Mark();
        var pending = rig.Robot.Motion.SetHeadAngleAsync(0.5f, timeout: TimeSpan.FromSeconds(5));
        rig.Tick();                                       // the ActionList tick takes the lock and sends
        var sent = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        byte setHead = (byte)sent.Id;
        var ids = rig.RawSince(mark).Select(b => b[0]).ToList();
        Assert.True(ids.IndexOf(0x9D) < ids.IndexOf(setHead), "DisableAnimTracks must precede SetHeadAngle");
        Assert.DoesNotContain((byte)0x9E, ids);                       // still locked while the move runs
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.5f);
        var r = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(r.Ok, r.Detail);
        ids = rig.RawSince(mark).Select(b => b[0]).ToList();
        Assert.Equal(1, ids.Count(b => b == 0x9D));
        Assert.Equal(1, ids.Count(b => b == 0x9E));
        Assert.True(ids.IndexOf(0x9D) < ids.IndexOf(setHead));
        Assert.True(ids.IndexOf(setHead) < ids.IndexOf(0x9E), "EnableAnimTracks must follow the completed SetHeadAngle");
        Assert.Equal(0, rig.Robot.Motion.LockedTracks);
    }

    /// <summary>
    /// M4-016 (0x0052B0C2): the IAction timeout slot's default is 30.0 s, used when the caller passes no timeout.
    /// The old 5 s is wrong.
    /// </summary>
    [Fact]
    public void M4_016_MA17_TheDefaultActionTimeoutIs30Seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), CozmoMotion.DefaultActionTimeout);
    }

    /// <summary>
    /// IActionRunner::Interrupt 0x00540250 calls virtual +0x14 and acts only on a 1. MoveLiftToHeightAction's
    /// vtable `_ZTVN4Anki5Cozmo22MoveLiftToHeightActionE` = 0x10219B4 (object vptr 0x10219BC, +0x14 = 0x10219D0)
    /// resolves to 0x0052B0B2 (Thumb 0x0052B0B3): `movs r0,#0; bx lr`, which returns 0. Q14 NOW_AND_RESUME
    /// therefore refuses to interrupt a lift move and falls back to QueueNow (Q15): the running move is
    /// Cancelled and Deleted (0x02000000), not Interrupted (0x03000009), and the incoming move is queued.
    /// </summary>
    [Fact]
    public async Task M4_CanInterrupt_A_LiftMoveRefusesQ14InterruptionAndFallsBackToQueueNow()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.LiftInPos, liftAngle: 0f);
        int mark = rig.Mark();

        var first = rig.Robot.Motion.SetLiftHeightAsync(60f, timeout: TimeSpan.FromSeconds(5));
        rig.Tick();                                            // first is current, RUNNING (nothing acks it)

        var second = rig.Robot.Motion.SetLiftHeightAsync(90f, position: QueueActionPosition.NowAndResume,
                                                         timeout: TimeSpan.FromSeconds(5));
        rig.Tick();                                            // Q15: Cancel+Delete first, then promote second

        var firstOutcome = await first.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(MotionResult.Failed, firstOutcome.Result);
        Assert.Equal(0x02000000u, firstOutcome.EngineResult);   // CANCELLED, not INTERRUPTED 0x03000009
        Assert.Contains(rig.SentSince(mark).OfType<SetLiftHeight>(), m => m.HeightMm == 90f);
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
        var r = rig.Robot.Motion.SetHeadAngleAsync(0.3f + 0.034f);
        rig.Tick();                                                              // NOW: Init sees in-position and sends nothing
        var ro = await r;
        Assert.True(ro.Ok, ro.Detail);
        var l = rig.Robot.Motion.SetLiftHeightAsync(48f);                        // 45 mm now
        rig.Tick();
        var lo = await l;
        Assert.True(lo.Ok, lo.Detail);
        Assert.DoesNotContain(rig.SentSince(mark), m => m is SetHeadAngle or SetLiftHeight);
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.3f, liftAngle: 0f);   // the lift is moving
        _ = rig.Robot.Motion.SetLiftHeightAsync(48f, timeout: TimeSpan.FromMilliseconds(1));
        rig.Tick();
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
        rig.Tick();
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
        rig.Tick();
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
        rig.Tick();
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
        rig.Tick();
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
        rig.Tick();
        var sent = Assert.IsType<SetLiftHeight>(rig.SentSince(mark).Single(m => m is SetLiftHeight));
        float at80 = MathF.Asin((80f - 45f) / 66f);
        rig.State(flags: inPos, liftAngle: at80);
        Assert.False(ok.IsCompleted);                                              // sent, not acked: Running
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        Assert.True((await ok.WaitAsync(TimeSpan.FromSeconds(2))).Ok);

        mark = rig.Mark();
        var fail = rig.Robot.Motion.SetLiftHeightAsync(40f, timeout: TimeSpan.FromSeconds(5));
        rig.Tick();
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
        Assert.Equal(new[] { NativeOracleFixtures.Packed(0xFF0000FF), NativeOracleFixtures.Packed(0xFF0000FF), NativeOracleFixtures.Packed(0xFF0000FF) },
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
    /// pending entry ends, BTF Update marks the located copy dirty. (M11-041: the cube is named by its radio slot, and the world's ObjectID
    /// is the connected object's, which AddConnectedActiveObject gave it; the located object here is a cube with that ID, and a Known object
    /// is what MarkDirty acts on.)
    /// </summary>
    [Fact]
    public void M4_023_CD10g_TheDoubleTapWindowReachesTheWorld()
    {
        using var rig = new Rig();
        using var vision = new VisionSystem(rig.Robot, CameraCalibration.Nominal()) { Enabled = false };
        rig.ToSynced();
        rig.State();
        const uint id = 1;                                                // the radio slot
        ConnectCube(rig, slot: id);
        uint worldId = vision.World.ConnectedObjectIdForActiveId(id)!.Value;
        var located = new ObservableObject(worldId, ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
        { Pose = Pose3d.Identity, PoseState = PoseState.Known, OriginId = vision.World.CurrentOriginId };
        vision.World.AddLocatedObject(located);
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

    // ------------------------------------------------------------------ M4-018 P3

    /// <summary>
    /// M4-018 W4/P2/P3 with the shipped WakeUp content: a reconnect pushes a second WakeUp on layer 2. When the top
    /// one finishes and pops, the animation below it is resent immediately with its current pattern (the spin), with
    /// no timer or iterator reset; its own expired timer then advances it to fadeOut on the next tick.
    /// </summary>
    [Fact]
    public void M4_018_P3_APopResendsTheLowerAnimationsCurrentPattern()
    {
        var res = WakeUpResources();
        try
        {
            using var rig = new Rig(res);
            rig.ToSynced();
            rig.State();
            var conn = new ObjectConnectionState { ObjectID = 0, FactoryID = 0x241A8EEC, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true };
            rig.Data(conn); rig.Tick();                    // WakeUp#1: spin
            rig.Data(conn); rig.Tick();                    // reconnect: WakeUp#2: spin, sent immediately
            int mark = rig.Mark();
            for (int i = 0; i < 110; i++) rig.Tick();      // 6600 ms: WakeUp#2's spin + fadeOut end, and the resend
            var lights = rig.RawSince(mark).Where(b => b[0] == 0x04).Select(Convert.ToHexString).ToList();
            // P3: WakeUp#1's current pattern (the spin) is resent after WakeUp#2's fadeOut.
            Assert.Contains(lights, h => h.Contains("E0830080010D070200"));
            // Its own expired timer then advances it to the fadeOut.
            Assert.Contains(lights, h => h.Contains("E0830080226404220000"));
        }
        finally { Directory.Delete(res, true); }
    }

    // ------------------------------------------------------------------ M4-018 C12 (EnableGameLayerOnly enable=1)

    /// <summary>
    /// M4-018 C12.1/C12.2 (EnableGameLayerOnly 0x006399C0..0x00639B36; the static at 0x0105B4A0, _INIT_36
    /// 0x004D7EB4..0x004D7EE5): the enable = 1 branch sends the static off ObjectLights through SetObjectLights
    /// (eight u32 0x000000FF, every period/transition/offset 0, rotation 0; through SetLEDs it is solid off with gamma
    /// 0x80, so each LED is 00 00 00 00 45 45 00 00 00 00), then stops layers 1 and 2. The app's
    /// SetEnableFreeplayLightStates(enable = false) is the engine's enable = 1. After it the layer is empty (P5 sends
    /// the static off when it empties) and the cube is game-layer-only, so a WakeUp is refused.
    /// </summary>
    [Fact]
    public void M4_018_C12_1_C12_2_EnableGameLayerOnlySendsTheStaticOffLights()
    {
        var res = CubeLightResources();
        try
        {
            using var rig = new Rig(res);
            rig.ToSynced();
            rig.State();
            var type = ObjectType.Block_LIGHTCUBE1;
            ConnectCube(rig, slot: 0, type: type);
            var cubes = rig.Robot.Lights.Cubes;
            rig.Tick(); rig.Tick();                                  // WakeUp ends; the default layer is Connected
            Assert.Equal("connected", cubes.TopPatternName(type));
            int mark = rig.Mark();
            rig.Robot.SetEnableFreeplayLightStates(enable: false, objectID: -1);   // app false -> engine enable 1
            var cube = rig.RawSince(mark).Where(b => b[0] is 0x0C or 0x10 or 0x04).ToList();
            Assert.DoesNotContain(cube, b => b[0] == 0x0C);          // gamma is cached at 0x80 after WakeUp
            string off = string.Concat(Enumerable.Repeat("00000000454500000000", 4));
            Assert.Contains(cube, b => b[0] == 0x10 && Convert.ToHexString(Body(b)) == "0000000000");
            Assert.Contains(cube, b => b[0] == 0x04 && Convert.ToHexString(Body(b)) == off);
            Assert.Null(cubes.TopPatternName(type));                 // P5: the layer is empty
            Assert.False(cubes.PlayLightAnim(type, "WakeUp", CubeLightComponent.StateLayer));
            Assert.True(rig.Logged("OnlyGameLayerEnabled"));
        }
        finally { Directory.Delete(res, true); }
    }

    /// <summary>
    /// M4-018 C12.2: the app enable = true is the engine's enable = 0 (game tag 0xBB EnableLightStates passes
    /// enable = (msg.byte0 == 0); Robot.cs:1902-1907). It clears the game-layer-only flag and picks the default
    /// layer-2 animation again, so a WakeUp is allowed once more.
    /// </summary>
    [Fact]
    public void M4_018_C12_2_AppEnableTrueRestoresTheDefaultLayer()
    {
        var res = CubeLightResources();
        try
        {
            using var rig = new Rig(res);
            rig.ToSynced();
            rig.State();
            var type = ObjectType.Block_LIGHTCUBE1;
            ConnectCube(rig, slot: 0, type: type);
            var cubes = rig.Robot.Lights.Cubes;
            rig.Tick(); rig.Tick();
            rig.Robot.SetEnableFreeplayLightStates(enable: false, objectID: -1);   // engine enable = 1
            Assert.Null(cubes.TopPatternName(type));
            rig.Robot.SetEnableFreeplayLightStates(enable: true, objectID: -1);    // engine enable = 0
            Assert.Equal("connected", cubes.TopPatternName(type));
            Assert.True(cubes.PlayLightAnim(type, "WakeUp", CubeLightComponent.StateLayer));
        }
        finally { Directory.Delete(res, true); }
    }

    /// <summary>
    /// M4-018 C13.4 (0x006399DC..0x006399E0 single-object, 0x00639A16..0x00639A1C all-objects): EnableGameLayerOnly
    /// branches to the epilogue without acting when the target is already in the requested state - comp+0x22 == enable
    /// for id -1, ObjectInfo+0x1C == enable for a named object. A repeated SetEnableFreeplayLightStates with the same
    /// value therefore sends nothing, on either direction and on both paths.
    /// </summary>
    [Fact]
    public void M4_018_C13_4_ARepeatedEnableGameLayerOnlySendsNothing()
    {
        var res = CubeLightResources();
        try
        {
            using var rig = new Rig(res);
            rig.ToSynced();
            rig.State();
            var type = ObjectType.Block_LIGHTCUBE1;
            ConnectCube(rig, slot: 0, type: type);
            var cubes = rig.Robot.Lights.Cubes;
            rig.Tick(); rig.Tick();                                  // WakeUp ends; the default layer is Connected

            // all-objects enable = 1 (app false): the first call acts, the second is the already-in-state return.
            rig.Robot.SetEnableFreeplayLightStates(enable: false, objectID: -1);
            int mark = rig.Mark();
            rig.Robot.SetEnableFreeplayLightStates(enable: false, objectID: -1);
            Assert.Empty(rig.RawSince(mark));

            // all-objects enable = 0 (app true): same.
            rig.Robot.SetEnableFreeplayLightStates(enable: true, objectID: -1);
            mark = rig.Mark();
            rig.Robot.SetEnableFreeplayLightStates(enable: true, objectID: -1);
            Assert.Empty(rig.RawSince(mark));

            // single-object ObjectInfo+0x1C (cube 0, app false -> engine enable 1): same.
            rig.Robot.SetEnableFreeplayLightStates(enable: false, objectID: 0);
            mark = rig.Mark();
            rig.Robot.SetEnableFreeplayLightStates(enable: false, objectID: 0);
            Assert.Empty(rig.RawSince(mark));
        }
        finally { Directory.Delete(res, true); }
    }

    /// <summary>
    /// M4-018 C12.1: the static off ObjectLights at 0x0105B4A0 (_INIT_36 0x004D7EB4..0x004D7EE5) is eight u32
    /// 0x000000FF (rev(NamedColors::BLACK) at 0x00C9744F), all periods/transitions/offsets 0 and rotation 0.
    /// This asserts the citation's bytes directly: the expected pattern is written out from C12.1, not composed
    /// from BodyLightComponent.OffLights. Through SetLEDs with gamma 0x80 it is the solid off LED
    /// `00 00 00 00 45 45 00 00 00 00`, which the production enable=1 path puts on the wire.
    /// </summary>
    [Fact]
    public void M4_018_C12_1_TheStaticOffLightsAreTheCitedBytes()
    {
        // C12.1: eight u32 0x000000FF = four LEDs, each onColor and offColor (0,0,0,255); +0x20..+0x7F zero.
        var cited = new[]
        {
            new LedPattern(new LedColor(0, 0, 0, 0xFF), new LedColor(0, 0, 0, 0xFF), 0, 0, 0, 0, 0),
            new LedPattern(new LedColor(0, 0, 0, 0xFF), new LedColor(0, 0, 0, 0xFF), 0, 0, 0, 0, 0),
            new LedPattern(new LedColor(0, 0, 0, 0xFF), new LedColor(0, 0, 0, 0xFF), 0, 0, 0, 0, 0),
            new LedPattern(new LedColor(0, 0, 0, 0xFF), new LedColor(0, 0, 0, 0xFF), 0, 0, 0, 0, 0),
        };
        Assert.Equal(4, CubeLightComponent.OffLights.Leds.Length);
        Assert.Equal(0u, CubeLightComponent.OffLights.RotationPeriodMs);
        for (int i = 0; i < 4; i++)
        {
            var actual = CubeLightComponent.OffLights.Leds[i];
            Assert.Equal(cited[i].OnColor, actual.OnColor);
            Assert.Equal(cited[i].OffColor, actual.OffColor);
            Assert.Equal(0u, actual.OnMs);
            Assert.Equal(0u, actual.OffMs);
            Assert.Equal(0u, actual.TransitionOnMs);
            Assert.Equal(0u, actual.TransitionOffMs);
            Assert.Equal(0, actual.OffsetMs);
        }

        // and through SetLEDs (both periods 0 -> colours 0, periods 0x7FFFFFFF) with gamma 0x80, solid off.
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        ConnectCube(rig);
        int mark = rig.Mark();
        rig.Robot.SetEnableFreeplayLightStates(enable: false, objectID: -1);   // app false -> engine enable 1
        string off = string.Concat(Enumerable.Repeat("00000000454500000000", 4));
        Assert.Contains(rig.RawSince(mark), b => b[0] == 0x04 && Convert.ToHexString(Body(b)) == off);
    }

    // ------------------------------------------------------------------ M4-019 SC4a, SC4c, H1..H4

    /// <summary>
    /// M4-019 SC4c W1..W5: a sample is taken only while the body is moving, off the treads and above the threshold
    /// cache; the sliding window's removal step uses N = the constant 100.0f, not the deque size, so 100 samples of
    /// 500 then one of 1500 give a variance of exactly 10000.0 (mean' = 500 + 1000/100, M2 = 1000*990, /99).
    /// </summary>
    [Fact]
    public void M4_019_SC4c_TheWelfordWindowUsesTheConstant100()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.AreWheelsMoving);   // moving, OnTreads
        var s = rig.Robot.Sensors;
        for (int i = 0; i < 100; i++) s.UpdateCliffRunningStats(500);
        Assert.Equal(0f, s.CliffVariance);
        s.UpdateCliffRunningStats(1500);                       // the 101st: the removal branch
        Assert.Equal(10000f, s.CliffVariance);
    }

    /// <summary>
    /// M4-019 SC4a S7: IncrementSuspiciousCliffCount returns while the cache is below 151; from 400 it stores 150
    /// and sends SetCliffDetectThreshold{150}, so a second call sends nothing.
    /// </summary>
    [Fact]
    public void M4_019_SC4a_IncrementSends150FromTheCache()
    {
        using var rig = new Rig();
        rig.ToSynced();
        var s = rig.Robot.Sensors;
        Assert.Equal(400, s.CliffDetectThreshold);
        int mark = rig.Mark();
        s.IncrementSuspiciousCliffCount();
        Assert.Equal(150, s.CliffDetectThreshold);
        var sent = Assert.Single(rig.RawSince(mark).Where(b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold));
        Assert.Equal(Hex("9600"), Body(sent));
        mark = rig.Mark();
        s.IncrementSuspiciousCliffCount();                     // 150 < 151: returns
        Assert.DoesNotContain(rig.RawSince(mark), b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold);
    }

    /// <summary>
    /// M4-019 SC4b S2..S6: with +0x1C set and the body stopped, the walk over the history from lower_bound(+0x1C)
    /// triggers IncrementSuspiciousCliffCount when the running variance is over 10000 and a sample exceeds min+15,
    /// sending 150 (from the cache of 400); +0x1C is then cleared.
    /// </summary>
    [Fact]
    public void M4_019_SC4b_TheSuspiciousWalkSends150()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        var s = rig.Robot.Sensors;
        rig.State(x: 0); rig.State(x: 100);                    // > 50 mm: the cache becomes 400
        Assert.Equal(400, s.CliffDetectThreshold);
        for (int i = 0; i < 110; i++)
        {
            var st = rig.MakeState(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.AreWheelsMoving, x: 100);
            st.CliffDataRaw = new ushort[] { (ushort)(i % 2 == 0 ? 500 : 1500), 500, 500, 500 };
            rig.Data(st); rig.Tick();
        }
        Assert.True(s.CliffVariance > 10000f);
        s.EvaluateCliffSuspiciousnessWhenStopped(1);           // start the walk from the beginning
        int mark = rig.Mark();
        var trig = rig.MakeState(RobotStatusFlag.IsBodyAccMode, x: 100);   // body stopped
        trig.CliffDataRaw = new ushort[] { 1500, 500, 500, 500 };
        rig.Data(trig); rig.Tick();
        var thresholds = rig.RawSince(mark).Where(b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold).ToList();
        Assert.Contains(thresholds, b => Convert.ToHexString(Body(b)) == "9600");
        Assert.Equal(0u, s.CliffSuspiciousAt);                 // S6: cleared by the walk
    }

    /// <summary>
    /// M4-019 H1..H4 (tag 0xD4, 0x0053539C..0x00535480): H3 Evaluate stores the Robot's last state timestamp into
    /// +0x1C; H4 broadcasts RobotStopped only while the cliff sensor is enabled. Nothing is sent to the robot.
    /// </summary>
    [Fact]
    public void M4_019_H1_H4_RobotStoppedEvaluatesAndBroadcasts()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var s = rig.Robot.Sensors;
        var seen = new List<byte>();
        s.RobotStopped += seen.Add;
        s.SetCliffSensorEnabled(true);
        int mark = rig.Mark();
        rig.Data(new RobotStopped { Field0 = 7 }); rig.Tick();
        Assert.Equal(7, Assert.Single(seen));
        Assert.Equal(rig.Engine.Robot!.StoredState!.Timestamp, s.CliffSuspiciousAt);
        Assert.DoesNotContain(rig.RawSince(mark), b => b[0] == 0x3B);      // no robot send
        seen.Clear();
        s.SetCliffSensorEnabled(false);
        rig.Data(new RobotStopped { Field0 = 8 }); rig.Tick();
        Assert.Empty(seen);                                                // disabled: no broadcast
    }

    // ------------------------------------------------------------------ M4-003, M4-005, M4-009, M4-012 (M12 wiring)

    /// <summary>
    /// M4-003 MA12: on the game path, a lift height of exactly 32.0 mm while carrying runs PlaceObjectOnGroundAction
    /// instead of MoveLiftToHeight; any other height still sends SetLiftHeight.
    /// </summary>
    [Fact]
    public async Task M4_003_MA12_A32mmLiftWhileCarryingRunsPlaceObjectOnGround()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        int placed = 0;
        rig.Robot.Motion.IsCarryingObject = () => true;
        rig.Robot.Motion.PlaceObjectOnGroundAsync = () =>
        {
            placed++;
            return Task.FromResult(new MotionOutcome(MotionResult.Acknowledged, "PlaceObjectOnGround"));
        };
        var r = await rig.Robot.Motion.SetLiftHeightAsync(32f);
        Assert.True(r.Ok);
        Assert.Equal(1, placed);
        Assert.DoesNotContain(rig.SentSince(0), m => m is SetLiftHeight);
        int mark = rig.Mark();
        _ = rig.Robot.Motion.SetLiftHeightAsync(40f, timeout: TimeSpan.FromMilliseconds(1));
        rig.Tick();
        Assert.Equal(1, placed);                                           // not 32: the ordinary lift path
        Assert.Contains(rig.SentSince(mark), m => m is SetLiftHeight);
    }

    /// <summary>
    /// M4-012 MA20: a MotorCalibration that starts the lift invokes the SetCarriedObjectAsUnattached(true) seam; the
    /// head starting or the lift finishing does not.
    /// </summary>
    [Fact]
    public void M4_012_MA20_ALiftCalibrationWhileCarryingDetaches()
    {
        using var rig = new Rig();
        rig.ToSynced();
        int detached = 0;
        rig.Robot.Sensors.UnattachCarriedObjectIfCarrying = () => detached++;
        rig.Data(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true }); rig.Tick();
        Assert.Equal(1, detached);
        rig.Data(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true }); rig.Tick();
        Assert.Equal(1, detached);
        rig.Data(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false }); rig.Tick();
        Assert.Equal(1, detached);
    }

    /// <summary>
    /// M4-009 CD10a: the carried-object exclusion suppresses the ObjectMoved broadcast (the manipulation layer wires
    /// the M12 CarryingComponent into this seam).
    /// </summary>
    [Fact]
    public void M4_009_CD10a_TheCarriedObjectIsExcludedFromTheMovedBroadcast()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        ConnectCube(rig);
        var moved = new List<Cube>();
        rig.Robot.Cubes.CubeMoved += moved.Add;
        rig.Robot.Cubes.ExcludeFromMovedBroadcast = c => c.ObjectId == 0;
        rig.Data(new ObjectMoved { Timestamp = 1, ObjectID = 0 }); rig.Tick();
        Assert.Empty(moved);
        rig.Data(new ObjectMoved { Timestamp = 2, ObjectID = 0 }); rig.Tick();
        Assert.Empty(moved);
    }

    /// <summary>
    /// M4-009 C11.1 D1..D3 (0x0063BA1E/0x0063BA2A, 0x0063BAAC/0x0063BAB6, 0x0063BE10): DockingComponent+0xC is
    /// the dock target ObjectID. It defaults to −1 (none), DockWithObject writes it, and AbortDocking does not
    /// reset it.
    /// </summary>
    [Fact]
    public void M4_009_C11_1_DockWithObjectSetsAndAbortKeepsTheDockTarget()
    {
        using var rig = new Rig();
        using var vision = new VisionSystem(rig.Robot, CameraCalibration.Nominal()) { Enabled = false };
        using var docking = new DockingSystem(rig.Robot, vision);
        rig.ToSynced();
        rig.State();
        Assert.Null(docking.DockTargetObjectId);                       // D1: default -1
        var target = vision.World.AddMarkerlessObject(Pose3d.Identity, ObjectType.CollisionObstacle);
        var marker = new KnownMarker(MarkerType.LightCubeI_Front, BlockFace.Front, Pose3d.Identity, 30);
        _ = docking.DockAsync(target, marker, DockAction.PickupLow, PathMotionProfile.Default,
                              timeout: TimeSpan.FromMilliseconds(50));
        Assert.Equal(target.ObjectId, docking.DockTargetObjectId);     // D2: DockWithObject writes +0xC
        docking.Abort();
        Assert.Equal(target.ObjectId, docking.DockTargetObjectId);     // D3: AbortDocking does not reset it
    }

    /// <summary>
    /// M4-005 MA8: a direct SetHeadAngle outside M4 (the M11 face-turn path) takes the shared u8 counter, so its ids
    /// run 1, 2, ... instead of the fixed 3.
    /// </summary>
    [Fact]
    public async Task M4_005_MA8_TheDirectHeadSendSharesTheCounter()
    {
        using var rig = new Rig();
        using var vision = new VisionSystem(rig.Robot, CameraCalibration.Nominal()) { Enabled = false };
        rig.ToSynced();
        rig.State();
        Assert.NotNull(vision.History.Latest);
        int mark = rig.Mark();
        await FaceTurns.TurnAsync(vision, Pose3d.Identity, 0, CancellationToken.None);
        var h = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        Assert.Equal(1, h.ActionId);
        mark = rig.Mark();
        await FaceTurns.TurnAsync(vision, Pose3d.Identity, 0, CancellationToken.None);
        h = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        Assert.Equal(2, h.ActionId);
    }

    // ------------------------------------------------------------------ M4-018 S1..S5 (cube-sleep flags)

    /// <summary>
    /// M4-018 S1..S5: the cube-sleep flags (comp+0x23/+0x24) are written only by the game EnableCubeSleep message
    /// (S3); when set, the default layer plays Sleep (0x21) or SleepNoFade (0x22) on layer 2 and ignores the carried
    /// object (S1); enable = 0 stops both on layer 2 and calls EnableGameLayerOnly(all, false) (S4). That call returns
    /// at the epilogue because comp+0x22 is already 0 (C13.4), so it does not re-pick the default layer; the stops'
    /// own Update pops Sleep and SleepNoFade and leaves the still-running WakeUp on top. No game-message id is
    /// invented; the public API mirrors Robot.cs:2090.
    /// </summary>
    [Fact]
    public void M4_018_S1_S3_S4_S5_EnableCubeSleepPicksSleepAndStopsIt()
    {
        var res = CubeLightResources();
        try
        {
            using var rig = new Rig(res);
            rig.ToSynced();
            rig.State();
            var type = ObjectType.Block_LIGHTCUBE1;
            rig.Data(new ObjectConnectionState { ObjectID = 0, FactoryID = 0x241A8EEC, ObjectType = type, Connected = true });
            rig.Tick();
            var cubes = rig.Robot.Lights.Cubes;
            Assert.Equal("wakeUp", cubes.TopPatternName(type));                 // LC1: WakeUp on connection
            cubes.IsCarried = _ => true;                                        // S1 must ignore this
            cubes.EnableCubeSleep(true, skipAnimation: false);
            cubes.PickNextAnimForDefaultLayer(type);
            Assert.Equal("sleep", cubes.TopPatternName(type));                  // 0x21 Sleep, not Carrying
            cubes.EnableCubeSleep(true, skipAnimation: true);
            cubes.PickNextAnimForDefaultLayer(type);
            Assert.Equal("sleepNoFade", cubes.TopPatternName(type));            // 0x22 SleepNoFade
            cubes.EnableCubeSleep(false);                                       // S4: stop both, EnableGameLayerOnly(all, false)
            Assert.Equal("wakeUp", cubes.TopPatternName(type));                 // C13.4: EnableGameLayerOnly(all, false) is a no-op (comp+0x22 == 0), so WakeUp survives
        }
        finally { Directory.Delete(res, true); }
    }

    // ------------------------------------------------------------------ M4-018 S7/C11.2 (robot-is-localized gate)

    /// <summary>
    /// M4-018 S7/C11.2 L6 (0x0063A3A8, 0x00637924..0x00637940): a RobotDelocalized refresh request (+0x21) is
    /// held while robot+0x2C4 is 0 and applied on the first Update after it returns to 1, re-picking the default
    /// layer for every object.
    /// </summary>
    [Fact]
    public void M4_018_S7_C11_2_TheDelocalizeRefreshWaitsForRelocalization()
    {
        var res = CubeLightResources();
        try
        {
            using var rig = new Rig(res);
            rig.ToSynced();
            rig.State();
            var type = ObjectType.Block_LIGHTCUBE1;
            ConnectCube(rig, slot: 0, type: type);
            var cubes = rig.Robot.Lights.Cubes;
            rig.Tick(); rig.Tick();                                   // WakeUp (100 ms) ends; the default layer is Connected
            Assert.Equal("connected", cubes.TopPatternName(type));
            cubes.IsCarried = _ => true;                              // the next default pick would be Carrying
            cubes.IsLocalized = () => false;                          // robot+0x2C4 = 0: delocalized
            cubes.OnRobotDelocalized();                               // the RobotDelocalized handler sets +0x21
            cubes.Update();                                           // held: the default layer is not re-picked
            Assert.Equal("connected", cubes.TopPatternName(type));
            cubes.IsLocalized = () => true;                           // robot+0x2C4 returns to 1
            cubes.Update();                                           // applied: +0x21 cleared, every object re-picked
            Assert.Equal("carrying", cubes.TopPatternName(type));
        }
        finally { Directory.Delete(res, true); }
    }

    /// <summary>
    /// M4-018 S7/C11.2 L2/L4/L6: the stack's Delocalize path (CozmoEngine.RobotDelocalized, wired by CozmoRobot)
    /// clears robot+0x2C4 (OffTreadsClassifier.Robot2C4) and sets the cube-light refresh request, and the gate
    /// reads that same flag. The only writer back to 1 here is a commit to OnTreads; SetLocalizedTo is M11 and
    /// not built.
    /// </summary>
    [Fact]
    public void M4_018_S7_TheDelocalizePathFeedsTheCubeLightGate()
    {
        var res = CubeLightResources();
        try
        {
            using var rig = new Rig(res);
            rig.ToSynced();
            rig.State();
            var type = ObjectType.Block_LIGHTCUBE1;
            ConnectCube(rig, slot: 0, type: type);
            var cubes = rig.Robot.Lights.Cubes;
            rig.Tick(); rig.Tick();
            Assert.Equal("connected", cubes.TopPatternName(type));

            // A commit to OnTreads sets robot+0x2C4 = 1 (M10-001 A11 / C11.2 L4).
            var off = rig.Robot.Sensors.OffTreads;
            off.HeadCalibrated = true; off.IsPhysical = true;
            off.Update(rig.MakeState(), 0);
            off.Update(rig.MakeState(RobotStatusFlag.IsPickedUp), 0);  // InAir
            off.Update(rig.MakeState(), 33);                           // back OnTreads: commit, flag = 1
            Assert.Equal(1, off.Robot2C4);

            cubes.IsCarried = _ => true;
            rig.Engine.RobotDelocalized!.Invoke();                     // C11.2 L2: flag = 0; S7: +0x21 set
            Assert.Equal(0, off.Robot2C4);
            cubes.Update();                                            // the wired IsLocalized reads 0: held
            Assert.Equal("connected", cubes.TopPatternName(type));

            off.Update(rig.MakeState(RobotStatusFlag.IsPickedUp), 33); // InAir
            off.Update(rig.MakeState(), 66);                           // OnTreads commit: flag = 1
            Assert.Equal(1, off.Robot2C4);
            cubes.Update();                                            // first Update after it returns to 1: applied
            Assert.Equal("carrying", cubes.TopPatternName(type));
        }
        finally { Directory.Delete(res, true); }
    }

    // ------------------------------------------------------------------ M4-019 SC4d, C7, S1

    /// <summary>
    /// M4-019 SC4d: ClearCliffRunningStats sets the count to 0, restores the cache to 400 and sends 400 when it was
    /// not already 400, and clears the window; a second call with the cache already at 400 sends nothing. The
    /// constructor's Delocalize runs it (M4-020 ConstructorDelocalize) with the cache at 400.
    /// </summary>
    [Fact]
    public void M4_019_SC4d_ClearCliffRunningStatsRestores400()
    {
        using var rig = new Rig();
        rig.ToSynced();
        var s = rig.Robot.Sensors;
        s.IncrementSuspiciousCliffCount();                     // 400 -> 150, sends 150
        Assert.Equal(150, s.CliffDetectThreshold);
        int mark = rig.Mark();
        s.ClearCliffRunningStats();
        Assert.Equal(400, s.CliffDetectThreshold);
        Assert.Equal(0, s.SuspiciousCliffCount);
        Assert.Equal(0f, s.CliffVariance);
        var sent = rig.RawSince(mark).Where(b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold).ToList();
        Assert.Equal(Hex("9001"), Body(Assert.Single(sent)));
        mark = rig.Mark();
        s.ClearCliffRunningStats();                            // the cache is already 400: nothing is sent
        Assert.DoesNotContain(rig.RawSince(mark), b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold);
    }

    /// <summary>
    /// M4-019 SC4d: the Delocalize path (<see cref="CozmoEngine.RobotDelocalized"/>, run by the constructor's
    /// Delocalize, M4-020 ConstructorDelocalize) is wired to ClearCliffRunningStats. With the cache at 150 it restores
    /// 400 and sends 400.
    /// </summary>
    [Fact]
    public void M4_019_SC4d_TheDelocalizePathRestoresTheThreshold()
    {
        using var rig = new Rig();
        rig.ToSynced();
        var s = rig.Robot.Sensors;
        s.IncrementSuspiciousCliffCount();                     // 400 -> 150
        Assert.Equal(150, s.CliffDetectThreshold);
        int mark = rig.Mark();
        rig.Engine.RobotDelocalized?.Invoke();                 // Robot::Delocalize's first step
        Assert.Equal(400, s.CliffDetectThreshold);
        var sent = rig.RawSince(mark).Where(b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold).ToList();
        Assert.Equal(Hex("9001"), Body(Assert.Single(sent)));
    }

    /// <summary>
    /// M4-019 C7 D2: MoveRobotPoseForward's distance is 0.0 while carrying and −20.0 otherwise, so a pure turn in place
    /// moves the drive centre by d·Δθ: 0 while carrying, and 40 mm for a π turn when not. The 400 follows the drive
    /// centre, not the robot origin.
    /// </summary>
    [Fact]
    public void M4_019_C7_TheDistanceIsZeroWhileCarrying()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Robot.Motion.IsCarryingObject = () => true;
        int mark = rig.Mark();
        List<byte[]> Thresholds() => rig.RawSince(mark).Where(b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold).ToList();
        var st = rig.MakeState(); st.Pose = new RobotPose { X = 0, Y = 0, Angle = 0 };
        rig.Data(st); rig.Tick();
        Assert.Equal(Hex("3200"), Body(Assert.Single(Thresholds())));           // the first accepted state sends 50
        st = rig.MakeState(); st.Pose = new RobotPose { X = 0, Y = 0, Angle = MathF.PI };
        rig.Data(st); rig.Tick();
        st = rig.MakeState(); st.Pose = new RobotPose { X = 0, Y = 0, Angle = 2 * MathF.PI };
        rig.Data(st); rig.Tick();
        Assert.Single(Thresholds());                                          // d = 0: the drive centre did not move
    }

    /// <summary>
    /// M4-019 S1 (0x00512FB0..0x00512F22): UpdateCliffRunningStats and UpdateCliffDetectThreshold also run on the
    /// frame-mismatch path while robot+0x2C0 &lt; 0x65; the 101st mismatch skips them. C12.3: the counter is
    /// per-mismatch-run, so the 101st also resets it to 0 (0x00512F88).
    /// </summary>
    [Fact]
    public void M4_019_S1_TheStatsRunForTheFirst100FrameMismatches()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        var s = rig.Robot.Sensors;
        for (int i = 0; i < 100; i++)
        {
            var st = rig.MakeState(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.AreWheelsMoving, frame: 1);
            st.CliffDataRaw = new ushort[] { (ushort)(i % 2 == 0 ? 500 : 1500), 500, 500, 500 };
            rig.Data(st); rig.Tick();
        }
        Assert.True(s.CliffVariance > 10000f);                 // the stats ran on the mismatched states
        Assert.Equal(100, s.FrameMismatchCount);
        float v = s.CliffVariance;
        var last = rig.MakeState(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.AreWheelsMoving, frame: 1);
        last.CliffDataRaw = new ushort[] { 500, 500, 500, 500 };
        rig.Data(last); rig.Tick();
        Assert.Equal(v, s.CliffVariance);                      // the 101st mismatch: r6 = 1, the stats do not run
        Assert.Equal(0, s.FrameMismatchCount);                 // C12.3: 0 after the count reaches 0x65
    }

    /// <summary>
    /// M4-019 C12.3 (0x00512F1C, 0x00512EAE): robot+0x2C0 is per-mismatch-run. A frame-mismatched accepted state
    /// increments it; the next frame-matching state resets it to 0. (Origin-rejected states leave it untouched.)
    /// </summary>
    [Fact]
    public void M4_019_C12_3_TheFrameMismatchCounterResetsOnAFrameMatch()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        var s = rig.Robot.Sensors;
        rig.Data(rig.MakeState(frame: 1)); rig.Tick();
        Assert.Equal(1, s.FrameMismatchCount);
        rig.Data(rig.MakeState(frame: 1)); rig.Tick();
        Assert.Equal(2, s.FrameMismatchCount);
        rig.Data(rig.MakeState(frame: 0)); rig.Tick();          // frame match: reset
        Assert.Equal(0, s.FrameMismatchCount);
        rig.Data(rig.MakeState(origin: 0, frame: 1)); rig.Tick();   // origin miss: untouched
        Assert.Equal(0, s.FrameMismatchCount);
    }

    /// <summary>
    /// M4-019 C12.3 (0x00512F1C, 0x00512F88): a run of frame mismatches accumulates, the 101st (0x65) skips the
    /// stats and resets the counter, and the next mismatch starts a new run at 1.
    /// </summary>
    [Fact]
    public void M4_019_C12_3_TheFrameMismatchCounterResetsAfter0x65()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        var s = rig.Robot.Sensors;
        for (int i = 0; i < 100; i++) { rig.Data(rig.MakeState(frame: 1)); rig.Tick(); }
        Assert.Equal(100, s.FrameMismatchCount);
        rig.Data(rig.MakeState(frame: 1)); rig.Tick();          // the 101st: 0x65 -> 0
        Assert.Equal(0, s.FrameMismatchCount);
        rig.Data(rig.MakeState(frame: 1)); rig.Tick();          // a new run
        Assert.Equal(1, s.FrameMismatchCount);
    }

    /// <summary>
    /// M4-019 C13.2 (0x00512B9C..0x00512BAA): a committed treads change stores robot+0x2C0 = 0 and branches straight
    /// to the stats/threshold body at 0x512FB4, skipping the frame compare, the frame-match reset and the mismatch
    /// increment. The counter therefore stays 0 for the committing state, even though its frame id is mismatched.
    /// </summary>
    [Fact]
    public void M4_019_C13_2_TheTreadsChangeLeavesTheFrameMismatchCounterAtZero()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        var s = rig.Robot.Sensors;
        rig.Data(rig.MakeState(frame: 1)); rig.Tick();
        rig.Data(rig.MakeState(frame: 1)); rig.Tick();
        Assert.Equal(2, s.FrameMismatchCount);
        rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsPickedUp, frame: 1)); rig.Tick();
        Assert.Equal(OffTreadsState.InAir, s.OffTreadsState);   // the commit
        Assert.Equal(0, s.FrameMismatchCount);                  // C13.2: the mismatch increment is skipped
    }
}
