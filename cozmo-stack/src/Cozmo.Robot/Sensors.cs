using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>A three-axis reading.</summary>
public readonly record struct Vector3(float X, float Y, float Z)
{
    public float Magnitude => MathF.Sqrt(X * X + Y * Y + Z * Z);
    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}

/// <summary>
/// Which of the four cliff sensors saw a drop, as a bit per sensor.
///
/// The order is the engine's <c>CliffSensor</c> enum. <c>EnumToString(CliffSensor)</c> 0x007D292C indexes
/// the pointer table at 0x01034C50, which holds "CLIFF_FL", "CLIFF_FR", "CLIFF_BL", "CLIFF_BR" and
/// "CLIFF_COUNT" in that order, and the same index space reaches the raw values:
/// <c>CliffSensorComponent::UpdateRobotData</c> 0x00634016 copies the four 16-bit readings out of
/// <c>RobotState</c> at +0x50 and +0x54 as two words, in order, and <c>GetCliffDataRaw(i)</c> reads them
/// back. So bit 0 is the front-left sensor and bit 3 the back-right.
/// </summary>
[Flags]
public enum CliffSensors : byte
{
    None = 0,
    FrontLeft = 1 << 0,
    FrontRight = 1 << 1,
    BackLeft = 1 << 2,
    BackRight = 1 << 3,
}

/// <summary>A cliff the robot reported, with whether it stopped itself.</summary>
public sealed record CliffReport(uint Timestamp, CliffSensors Sensors, bool StoppedForCliff)
{
    public override string ToString() => $"cliff {Sensors} at {Timestamp}{(StoppedForCliff ? ", robot stopped" : "")}";
}

// fidelity: M2-013
/// <summary>
/// The end of a fall, as the robot reports it in <see cref="FallingStopped"/> (0xDE): how long it fell and
/// how hard it landed. The intensity's unit is not established; the engine compares it against 1000
/// (<c>BehaviorReactToImpact::AlwaysHandle</c> at 0x00606408) to decide whether the landing counts as an
/// impact worth reacting to.
///
/// The message is {u32 timestamp, u32 duration_ms, f32 impactIntensity}: <c>HandleFallingStopped</c> 0x00535040
/// logs "timestamp: %u, duration (ms): %u, intensity %.1f" from <c>ldrd r1,r2,[r5]</c> (0x0053506C) and
/// <c>vldr s0,[r5,#8]</c> (0x00535068). <see cref="Timestamp"/> is the robot's timestamp word.
/// </summary>
public sealed record FallingStoppedReport(uint DurationMs, float ImpactIntensity)
{
    /// <summary>The message's first word, the robot's timestamp (+0).</summary>
    public uint Timestamp { get; init; }

    public override string ToString() => $"fell for {DurationMs} ms, impact {ImpactIntensity:F0}";
}

/// <summary>
/// A settled view of everything the robot reports about itself.
///
/// Everything here comes from <see cref="RobotState"/>, which arrives about thirty times a second, except
/// the cliff events and the raw IMU bursts, which are separate messages. Reading a property is a snapshot
/// of the last state received; nothing here polls the robot.
/// </summary>
public sealed class CozmoSensors
{
    private readonly RobotStateTracker _state;
    private readonly CozmoRobot _robot;
    private readonly object _gate = new();
    private readonly List<CliffReport> _cliffs = new();

    internal CozmoSensors(CozmoRobot robot, RobotStateTracker state)
    {
        _robot = robot;
        _state = state;
        // fidelity: M10-001, M4-019
        // CheckAndUpdateTreadsState's commit consequences in the engine's order (A10, A13): the broadcast, then
        // SetOnChargerPlatform(false) for any state but OnTreads (C8 P6, with the contacts flag from the previous state).
        OffTreads.StateChanged += (from, to) => OffTreadsStateChanged?.Invoke(from, to);
        OffTreads.ClearOnChargerPlatform = () => SetOnChargerPlatform(false);
        OffTreads.Log += l => _robot.Engine.Log(l);
        UnexpectedMovement.Log += l => _robot.Engine.Log(l);
    }

    /// <summary>Raised when the robot reports a cliff.</summary>
    public event Action<CliffReport>? CliffDetected;
    /// <summary>Raised when the robot reports it has been picked up, or put back down.</summary>
    public event Action<bool>? PickedUpChanged;
    /// <summary>Raised when the robot arrives on, or leaves, the charger contacts.</summary>
    public event Action<bool>? OnChargerChanged;

    // fidelity: M4-019
    /// <summary>
    /// H4: <c>HandleRobotStopped</c>'s game-side <c>RobotStopped</c> broadcast (0x00535480), raised with the
    /// reason byte when the cliff sensor is enabled. The behaviour-end and action-cancel calls that precede it
    /// (<c>BehaviorManager::RequestCurrentBehaviorEndImmediately</c>, <c>ActionList::Cancel(-1)</c>) are the M7/M8
    /// interfaces and are not wired here.
    /// </summary>
    public event Action<byte>? RobotStopped;

    /// <summary>
    /// Raised when the robot starts or stops reporting that it is falling, derived from
    /// <c>RobotStatusFlag.IsFalling</c> the same way pick-up and charger transitions are. Without this the
    /// reaction table claimed a falling reaction that nothing could ever raise.
    /// </summary>
    public event Action<bool>? FallingChanged;

    // fidelity: M10-011
    /// <summary>
    /// C2: HandleFallingStopped's game broadcast FallingStopped{duration_ms, impactIntensity}, raised for every
    /// FallingStopped (0xDE) the robot sends, after <see cref="NeedsActionCompleted"/> and the DAS event.
    /// </summary>
    public event Action<FallingStoppedReport>? FallingStopped;

    // fidelity: M10-011
    /// <summary>C1: HandleFallingStarted's game broadcast FallingStarted{msg.timestamp}; no time-sync gate, no state change.</summary>
    public event Action<uint>? FallingStarted;

    // fidelity: M10-011
    /// <summary>
    /// C2: NeedsManager::RegisterNeedsActionCompleted(17) when the intensity is &gt; 1000.0. The needs manager is the M15
    /// interface (MD2); 17 is the engine's NeedsActionId ordinal ("Fall" by the Unity ordinal; the engine name was not
    /// checked). Nothing in this stack subscribes yet.
    /// </summary>
    public event Action<int>? NeedsActionCompleted;

    /// <summary>C2: the intensity above which the Fall needs action is registered (1000.0).</summary>
    public const float FallNeedsActionIntensity = 1000.0f;
    /// <summary>C2: the NeedsActionId registered for a hard fall.</summary>
    public const int FallNeedsActionId = 17;

    // ------------------------------------------------------------ derived state (M10)

    /// <summary>
    /// The engine's off-treads classifier, fed every robot state. Its thresholds and debounce are read
    /// from <c>Robot::CheckAndUpdateTreadsState</c>; see <see cref="OffTreadsClassifier"/>.
    /// </summary>
    public OffTreadsClassifier OffTreads { get; } = new();

    /// <summary>The last state whose treads commit involved OnTreads (r7 != 0 at 0x00512A94/0x00512A86), which the engine answers with Robot::Delocalize (M11-044); null before any.</summary>
    internal RobotState? DelocalizeTrigger { get; private set; }

    /// <summary>How the robot is sitting, as the engine would classify it.</summary>
    public OffTreadsState OffTreadsState => OffTreads.Current;

    /// <summary>
    /// Whether the classifier is running. It is gated on the head having reported a completed calibration,
    /// as the engine gates it (<c>Robot+0x314</c>); until then <see cref="OffTreadsState"/> stays OnTreads.
    /// </summary>
    public bool OffTreadsClassifierEnabled => OffTreads.HeadCalibrated;

    /// <summary>Raised when the classified off-treads state changes: the engine's <c>RobotOffTreadsStateChanged</c>.</summary>
    public event Action<OffTreadsState, OffTreadsState>? OffTreadsStateChanged;

    /// <summary>|accelerometer| after the engine's 0.95/0.05 filter, the value its shaken strategies compare.</summary>
    public float FilteredAccelMagnitude => OffTreads.FilteredAccelMagnitude;
    /// <summary>The pose pitch of the last state, radians, as the engine keeps it on Robot.</summary>
    public float? PitchRad => _state.Latest is { } s ? s.Pose.Pitch : null;

    /// <summary>
    /// The engine's unexpected-movement detector, fed every robot state; see
    /// <see cref="UnexpectedMovementDetector"/>.
    /// </summary>
    public UnexpectedMovementDetector UnexpectedMovement { get; } = new();

    /// <summary>Raised when the detector decides the body moved against its wheels: the engine's <c>UnexpectedMovement</c>.</summary>
    public event Action<UnexpectedMovementReport>? UnexpectedMovementDetected;

    /// <summary>
    /// Raised for a <see cref="MotorCalibration"/> report that says a calibration <b>started</b> and was
    /// <b>auto-started</b> by the robot. That pair is exactly what the engine's MotorCalibration reaction
    /// strategy filters on (factory lambda at 0x0060DCFA: <c>calibStarted &amp;&amp; autoStarted</c>).
    /// </summary>
    public event Action<MotorCalibration>? AutoCalibrationStarted;

    /// <summary>Raised for every MotorCalibration report, started or finished.</summary>
    public event Action<MotorCalibration>? MotorCalibrationReported;

    // fidelity: M4-012
    /// <summary>
    /// MA20: <c>HandleMotorCalibration</c> 0x00536BAE..0x00536C0E calls <c>SetCarriedObjectAsUnattached(true)</c>
    /// when the lift starts calibrating while an object is carried. The carrying component is M12's; this seam is
    /// invoked on a lift <c>CalibStarted</c> and the manipulation layer decides.
    /// </summary>
    public Action? UnattachCarriedObjectIfCarrying { get; set; }

    // ------------------------------------------------------------------- power

    /// <summary>Battery voltage in volts. A charged Cozmo reads about 4.1 V, a flat one about 3.5 V.</summary>
    public float? BatteryVolts => _state.Latest?.BatteryVoltage;
    /// <summary>True while the robot is sitting on the charger contacts.</summary>
    public bool OnCharger => Flag(RobotStatusFlag.IsOnCharger);
    /// <summary>True while the robot is actually drawing charge.</summary>
    public bool Charging => Flag(RobotStatusFlag.IsCharging);
    /// <summary>
    /// The robot's "charger out of spec" flag. What the firmware means by it is not established, so it is
    /// surfaced as the raw flag rather than interpreted.
    /// </summary>
    public bool ChargerOutOfSpec => Flag(RobotStatusFlag.IsChargerOos);

    // ------------------------------------------------------------------ motion

    /// <summary>Head angle in radians, as the robot reports it.</summary>
    public float? HeadAngleRad => _state.Latest?.HeadAngle;
    /// <summary>
    /// The lift arm angle in radians. The unit is settled by the engine: <c>Robot::UpdateFullRobotState</c>
    /// (0x0051291C) stores <c>RobotState.liftAngle</c> into the field <c>Robot::GetLiftHeight</c>
    /// (0x00516F64) converts with <c>45 + 66 sin(angle)</c>; see <see cref="RobotState.LiftAngleRad"/>.
    /// </summary>
    public float? LiftAngleRad => _state.Latest?.LiftAngle;
    // fidelity: M2-003
    /// <summary>
    /// The lift height in millimetres, converted from the angle as the engine converts it: 66 sin(angle) + 45
    /// with no clamp (GetLiftHeight 0x00516F64..0x00516F8E, RS7). The 32..92 clamp is only in the inverse,
    /// <see cref="RobotState.LiftAngleRadFromHeight"/>.
    /// </summary>
    public float? LiftHeightMm => _state.Latest?.LiftHeightMm;
    /// <summary>Left and right wheel speeds in mm/s.</summary>
    public (float Left, float Right)? WheelSpeedsMmps =>
        _state.Latest is { } s ? (s.LwheelSpeedMmps, s.RwheelSpeedMmps) : null;
    /// <summary>True while the robot says a wheel is turning.</summary>
    public bool WheelsMoving => Flag(RobotStatusFlag.AreWheelsMoving);
    /// <summary>True while the robot says any part of it is moving.</summary>
    public bool Moving => Flag(RobotStatusFlag.IsMoving);
    /// <summary>True once the head has reached the angle it was last told to go to.</summary>
    public bool HeadInPosition => Flag(RobotStatusFlag.HeadInPos);
    /// <summary>True once the lift has reached the height it was last told to go to.</summary>
    public bool LiftInPosition => Flag(RobotStatusFlag.LiftInPos);

    // -------------------------------------------------------------------- imu

    /// <summary>
    /// Accelerometer reading, in millimetres per second squared. The engine's own classifier says so: it
    /// takes gravity to be <see cref="OffTreadsClassifier.GravityAccel"/>, 9800 in these units, which is
    /// one g in mm/s^2 (<c>Robot::CheckAndUpdateTreadsState</c>). Nothing scales the values on the way in -
    /// <c>Robot::UpdateFullRobotState</c> 0x0051291C reads them at RobotState +0x30, +0x34 and +0x38 and
    /// filters them as they are (0x00512A36..0x00512A6E).
    /// </summary>
    public Vector3? Accelerometer =>
        _state.Latest is { } s ? new Vector3(s.Accel.X, s.Accel.Y, s.Accel.Z) : null;
    /// <summary>
    /// Gyroscope reading, in radians per second. <c>RobotGyroDriftDetector::DetectGyroDrift</c> 0x0052C568
    /// takes the z rate straight from RobotState +0x44 and calls the robot still only while its magnitude
    /// is at or under 0.174533 (0x0052C580), which is 10 degrees a second; the next gate compares
    /// successive samples against 0.0174533, one degree, and the pose check that follows uses 0.00872665,
    /// half a degree. Nothing scales the values on the way in.
    /// </summary>
    public Vector3? Gyroscope =>
        _state.Latest is { } s ? new Vector3(s.Gyro.X, s.Gyro.Y, s.Gyro.Z) : null;
    /// <summary>True while the robot reports it is off the ground.</summary>
    public bool PickedUp => Flag(RobotStatusFlag.IsPickedUp);
    /// <summary>True while the robot reports it is falling.</summary>
    public bool Falling => Flag(RobotStatusFlag.IsFalling);

    /// <summary>
    /// Asks the robot for a burst of raw IMU samples lasting this long, delivered as IMURawDataChunk
    /// messages. Read them from <see cref="CozmoRobot.Message"/>; this only starts the burst.
    /// </summary>
    public void RequestImuBurst(TimeSpan duration) =>
        _robot.SendMessage(new IMURequest { LengthMs = (uint)duration.TotalMilliseconds }, flush: true);

    // ------------------------------------------------------------------ cliffs

    /// <summary>True while the robot's own cliff detection is asserting.</summary>
    public bool CliffDetectedNow => Flag(RobotStatusFlag.CliffDetected);

    /// <summary>
    /// The four raw cliff sensor readings, in the order the robot sends them. A lower number means a darker
    /// or more distant surface. The threshold the firmware uses is not established.
    /// </summary>
    public ushort[]? CliffSensorsRaw => _state.Latest?.CliffDataRaw?.ToArray();

    /// <summary>Cliff events the robot has reported, oldest first.</summary>
    public IReadOnlyList<CliffReport> CliffHistory { get { lock (_gate) return _cliffs.ToArray(); } }

    // fidelity: M4-019
    /// <summary>
    /// The game EnableStopOnCliff (SC8): sent verbatim. The robot's own default is HARDWARE_ONLY; the engine sends
    /// nothing at connection.
    /// </summary>
    public void SetStopOnCliff(bool enable) =>
        _robot.SendMessage(new EnableStopOnCliff(enable), flush: true);

    // ------------------------------------------------------ CliffSensorComponent (M4-008, M4-019)

    // fidelity: M4-008, M4-019
    /// <summary>SC1: the constructor's +4 enabled = 1, +5 = 0, threshold cache 400, raw values 0xFFFF (0x00633FA0..0x00633FCE).</summary>
    public const ushort DefaultCliffThreshold = 400;
    /// <summary>SC4: the threshold sent on the first accepted state of the current frame and on the charger platform.</summary>
    public const ushort StartCliffThreshold = 50;

    private bool _cliffEnabled = true;                 // +4
    private bool _cliffDetectedByEvent;                // +5
    private bool _cliffDetectedFlag;                   // +6
    private uint _cliffTimestamp;                      // +8
    private ushort _cliffThresholdCache = DefaultCliffThreshold;   // +0xC
    private ushort[] _cliffRaw = { 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF };   // +0xE
    /// <summary>Robot+0x528 (−1.0 from the constructor) and +0x52C (0): the 50-mm schedule (SC4, SC4e).</summary>
    private float _cliffDistanceMm = -1.0f;
    private bool _cliffDistanceDone;
    /// <summary>
    /// Robot+0x298 as UpdateCurrPoseFromHistory leaves it (C7 D4): the pose of the last accepted state; the constructor's
    /// Delocalize puts it at the identity (SetNewPose, 0x00510B4C).
    /// </summary>
    private float _poseX, _poseY, _poseAngle;
    /// <summary>C7 D2: MoveRobotPoseForward's distance when not carrying (the drive-centre offset); 0.0 when carrying.</summary>
    internal const float DriveCenterOffsetMm = -20.0f;

    /// <summary>+0x338: on the charger contacts, 0 from the constructor (C8 P4, P5).</summary>
    private bool _onChargerContacts;
    /// <summary>+0x34A: on the charger platform (C8 P1).</summary>
    private bool _onChargerPlatform;
    /// <summary>Whether the robot is on the charger platform (+0x34A, C8).</summary>
    public bool OnChargerPlatform { get { lock (_gate) return _onChargerPlatform; } }
    // fidelity: M7-021
    /// <summary>
    /// robot+0x338: the on-charger-contacts boolean, set by <c>Robot::SetOnCharger</c> from the
    /// IS_ON_CHARGER status bit. It is distinct from <see cref="OnChargerPlatform"/> (+0x34A), which
    /// <c>SetOnChargerPlatform</c> derives as <c>(arg != 0) || (+0x338 != 0)</c>. C2c: the constructor
    /// zeroes it (0x005100C4), <c>SetOnCharger</c> is its only runtime writer (store 0x00511C14), the
    /// status bit is extracted at 0x00512AAE and <c>SetOnCharger</c> is called from
    /// <c>UpdateFullRobotState</c> at 0x00512AB4. The pickup reaction reads it at 0x00607BCC (C2h).
    /// </summary>
    public bool OnChargerContacts { get { lock (_gate) return _onChargerContacts; } }
    /// <summary>RobotOnChargerPlatformEvent (C8 P1): raised with the new value when it changes.</summary>
    public event Action<bool>? OnChargerPlatformChanged;

    // ------------------------------------------------ CliffSensorComponent running stats (M4-019)
    //
    // fidelity: M4-019
    // UpdateCliffRunningStats (0x00634630..0x00634724) and UpdateCliffDetectThreshold (0x006343B8..0x006344B0):
    // a 100-sample sliding Welford window over cliff[0] (comp+0x20 deque, mean +0x38, variance +0x3C, M2 +0x40);
    // HandleRobotStopped stores the last state timestamp into +0x1C, and the next state walks the raw-state history
    // from lower_bound(+0x1C) and sends 150 when the variance is over 10000 and a sample exceeds min+15.
    private readonly Queue<ushort> _cliffSamples = new();   // comp+0x20, window 100
    private float _cliffMean;                               // +0x38
    private float _cliffVariance;                           // +0x3C
    private float _cliffM2;                                 // +0x40
    private int _cliffSuspiciousCount;                      // +0x18
    private uint _cliffSuspiciousAt;                        // +0x1C
    /// <summary>The raw-state history the lower_bound walk reads (RobotStateHistory's raw map): timestamp and cliff[0].</summary>
    private readonly List<(uint Timestamp, ushort Cliff0)> _cliffHistory = new();

    // fidelity: M4-019
    /// <summary>SC4c W4: the constant divisor of the removal step, 100.0f (0x00634728), not the deque size.</summary>
    internal const float CliffWelfordWindow = 100.0f;
    /// <summary>SC4c W5: the variance above which IncrementSuspiciousCliffCount fires (10000.0f at 0x006344ec).</summary>
    internal const float CliffVarianceThreshold = 10000.0f;
    /// <summary>SC4a S7: the cache is reduced by 250, clamped up to 150 (0x00634562..0x00634568).</summary>
    internal const ushort CliffSuspiciousThreshold = 150;
    /// <summary>SC4a S7: the cache below which Increment returns, 151 (0x0063451c).</summary>
    internal const ushort CliffSuspiciousCacheMin = 151;

    // fidelity: M4-019
    /// <summary>The variance of the running cliff statistics (+0x3C).</summary>
    public float CliffVariance { get { lock (_gate) return _cliffVariance; } }
    /// <summary>The count of suspicious cliffs (+0x18).</summary>
    public int SuspiciousCliffCount { get { lock (_gate) return _cliffSuspiciousCount; } }
    /// <summary>+0x1C: the timestamp the suspicious-cliff walk starts from (0 when idle).</summary>
    internal uint CliffSuspiciousAt { get { lock (_gate) return _cliffSuspiciousAt; } }

    // fidelity: M4-019
    /// <summary>
    /// H3 EvaluateCliffSuspiciousnessWhenStopped (0x00634840..0x00634846): +0x1C := the Robot's last state timestamp.
    /// </summary>
    internal void EvaluateCliffSuspiciousnessWhenStopped(uint lastStateTimestamp)
    {
        lock (_gate) _cliffSuspiciousAt = lastStateTimestamp;
    }

    // fidelity: M4-019
    /// <summary>
    /// SC4c UpdateCliffRunningStats (0x00634630..0x00634724). W1 sample gate: the body is moving (MC+0xC), the
    /// committed off-treads state is OnTreads, and cliff[0] is greater than the threshold cache. W3/W4/W5: a
    /// 100-sample sliding Welford window; the removal step's N is the constant 100.0f, not the deque size.
    /// </summary>
    internal void UpdateCliffRunningStats(ushort cliff0)
    {
        bool take;
        lock (_gate)
        {
            take = _robot.Motion.BodyMoving && OffTreadsState == OffTreadsState.OnTreads && cliff0 > _cliffThresholdCache;
        }
        if (!take) return;
        lock (_gate)
        {
            _cliffSamples.Enqueue(cliff0);
            int n = _cliffSamples.Count;
            if (n < 101)
            {
                // W3 add-only: n = float(size); mean += (x - mean) / n; M2 += (x - mean_old) * (x - mean_new).
                float nf = n;
                float d = cliff0 - _cliffMean;
                float meanNew = _cliffMean + d / nf;
                _cliffM2 += d * (cliff0 - meanNew);
                _cliffMean = meanNew;
            }
            else
            {
                // W4 removal: N = 100.0f constant; pop the oldest first.
                ushort old = _cliffSamples.Dequeue();
                float meanOld = _cliffMean;
                float meanNew = meanOld + (cliff0 - old) / CliffWelfordWindow;
                _cliffM2 += (cliff0 - meanOld) * (cliff0 - meanNew) - (old - meanOld) * (old - meanNew);
                _cliffMean = meanNew;
                n = _cliffSamples.Count;   // 100
            }
            // W5: variance = M2 / (size - 1) once size >= 2.
            _cliffVariance = n >= 2 ? _cliffM2 / (n - 1) : 0f;
        }
    }

    // fidelity: M4-019
    /// <summary>
    /// SC4b UpdateCliffDetectThreshold (0x006343B8..0x006344B0). S2 gates: +0x1C != 0 and the body is not moving.
    /// S3/S4: a lower_bound on the history timestamp and an in-order successor walk to the end. S5: min(cliff[0]),
    /// and when the variance is over 10000 and min+15 &lt; cliff[0], IncrementSuspiciousCliffCount. S6: +0x1C := 0.
    /// </summary>
    private void UpdateCliffDetectThreshold()
    {
        List<ushort> cliffs;
        lock (_gate)
        {
            if (_cliffSuspiciousAt == 0 || _robot.Motion.BodyMoving) return;
            // S3: lower_bound(_cliffSuspiciousAt) - the first sample with timestamp >= the target.
            int i = _cliffHistory.FindIndex(e => e.Timestamp >= _cliffSuspiciousAt);
            if (i < 0) { _cliffSuspiciousAt = 0; return; }
            cliffs = new List<ushort>();
            for (; i < _cliffHistory.Count; i++) cliffs.Add(_cliffHistory[i].Cliff0);
            _cliffSuspiciousAt = 0;   // S6
        }
        // S5: min is a local (0xFFFF), the variance is the component's +0x3C.
        ushort min = 0xFFFF;
        foreach (var cliff in cliffs)
        {
            if (min > cliff) min = cliff;
            if (_cliffVariance > CliffVarianceThreshold && (uint)(min + 15) < cliff)
                IncrementSuspiciousCliffCount();
        }
    }

    // fidelity: M4-019
    /// <summary>
    /// SC4a S7 IncrementSuspiciousCliffCount (0x00634514..0x006345AE): returns while the cache is below 151;
    /// otherwise count++, the cache becomes cache − 250 clamped up to 150, that is sent, and count := 0. From 400
    /// this sends 150.
    /// </summary>
    internal void IncrementSuspiciousCliffCount()
    {
        ushort sent;
        lock (_gate)
        {
            ushort cache = _cliffThresholdCache;
            if (cache < CliffSuspiciousCacheMin) return;
            _cliffSuspiciousCount++;
            uint reduced = (uint)cache - 250u;
            if (reduced <= 0x96u) reduced = CliffSuspiciousThreshold;
            _cliffThresholdCache = (ushort)reduced;
            sent = (ushort)reduced;
            _cliffSuspiciousCount = 0;
        }
        SendCliffDetectThresholdToRobot(sent);
    }

    // fidelity: M4-019
    /// <summary>
    /// SC4d ClearCliffRunningStats (0x006347A4..0x0063480C): count = 0; if the threshold cache is not 400 it becomes
    /// 400, the event "RestoringCliffDetectThreshold" is logged and 400 is sent; then the deque, mean, variance and M2
    /// are cleared. +0x1C is not cleared. It is called only from <c>Robot::Delocalize</c> (0x00510A5A).
    /// </summary>
    internal void ClearCliffRunningStats()
    {
        bool send;
        lock (_gate)
        {
            _cliffSuspiciousCount = 0;
            send = _cliffThresholdCache != DefaultCliffThreshold;
            if (send) _cliffThresholdCache = DefaultCliffThreshold;
            _cliffSamples.Clear();
            _cliffMean = _cliffVariance = _cliffM2 = 0f;
        }
        if (!send) return;
        _robot.Engine.Log("info: CliffSensorComponent.ClearCliffRunningStats.RestoringCliffDetectThreshold");
        SendCliffDetectThresholdToRobot(DefaultCliffThreshold);
    }

    /// <summary>Whether the engine's cliff sensor component is enabled (+4); set by the game EnableCliffSensor (SC6).</summary>
    public bool CliffSensorEnabled { get { lock (_gate) return _cliffEnabled; } }
    /// <summary>+5: the last CliffEvent broadcast had flags ≠ 0 (SC5).</summary>
    public bool CliffDetectedByEvent { get { lock (_gate) return _cliffDetectedByEvent; } }
    /// <summary>+6: CLIFF_DETECTED from the last handled state (SC2).</summary>
    public bool CliffDetectedStored { get { lock (_gate) return _cliffDetectedFlag; } }
    /// <summary>+8: the timestamp of the last handled state (SC2).</summary>
    public uint CliffDataTimestamp { get { lock (_gate) return _cliffTimestamp; } }
    /// <summary>+0xE: the four raw values from the last handled state, 0xFFFF before any (SC1, SC2).</summary>
    public IReadOnlyList<ushort> CliffDataRawStored { get { lock (_gate) return _cliffRaw.ToArray(); } }
    /// <summary>+0xC: the cliff-detect threshold last stored (SC3).</summary>
    public ushort CliffDetectThreshold { get { lock (_gate) return _cliffThresholdCache; } }

    // fidelity: M4-019
    /// <summary>The game EnableCliffSensor (SC6, 0x00527E8E..0x00527EEA): logs "Setting to %s" and stores +4; nothing is sent.</summary>
    public void SetCliffSensorEnabled(bool enable)
    {
        _robot.Engine.Log($"info: EnableCliffSensor: Setting to {(enable ? "true" : "false")}");
        lock (_gate) _cliffEnabled = enable;
    }

    // fidelity: M4-019
    /// <summary>
    /// SendCliffDetectThresholdToRobot (SC3, 0x00634270..0x006342CE; 0x0063433C..0x00634368): stores the value when it
    /// differs from the cache and always sends SetCliffDetectThreshold {u16}, reliable.
    /// </summary>
    internal void SendCliffDetectThresholdToRobot(ushort t)
    {
        lock (_gate) if (t != _cliffThresholdCache) _cliffThresholdCache = t;
        _robot.SendMessage(new SetCliffDetectThreshold { Field0 = t });
    }

    // fidelity: M4-019
    /// <summary>
    /// MoveRobotPoseForward(pose, d) (C7 D3, 0x00517ED0..0x00517F3E): (x + d·cosθ, y + d·sinθ, 0).
    /// </summary>
    internal static (float X, float Y) MoveRobotPoseForward(float x, float y, float angle, float d) =>
        (x + d * MathF.Cos(angle), y + d * MathF.Sin(angle));

    // fidelity: M4-019
    /// <summary>
    /// SC4e: robot+0x2B0 is the current pose frame id; the schedule and the stats run on a state whose frame id
    /// matches (0x00512D7A, 0x00512EAC), and on a frame-mismatched state only while robot+0x2C0 &lt; 101 (S1).
    /// </summary>
    private bool FrameMatches(RobotState s) => _robot.Engine.Robot is { } er && s.PoseFrameId == er.PoseFrameId;

    // fidelity: M4-019
    /// <summary>
    /// Robot+0x2C0, the frame-mismatch run counter (S1, C12.3): 0 at construction (0x0050FF08), 0 on every frame-match
    /// state (0x00512EAE), 0 on the treads-change Delocalize path (0x00512B9C), old+1 on a frame-mismatch (0x00512F1C)
    /// and 0 once the count reaches 0x65 (0x00512F88). An origin-miss or a history-failure state leaves it untouched.
    /// </summary>
    private int _frameMismatchCount;

    /// <summary>robot+0x2C0, the frame-mismatch run counter (C12.3), for the regression tests.</summary>
    internal int FrameMismatchCount { get { lock (_gate) return _frameMismatchCount; } }

    // fidelity: M4-019
    /// <summary>
    /// The threshold schedule in UpdateFullRobotState, for an accepted state (SC4, SC4e, C7 D1..D5, 0x00512D48..0x00512EA6):
    /// the drive-centre point of the robot pose before the state's pose update and after it, MoveRobotPoseForward(pose,
    /// d) with z = 0, where d is −20 mm normally and 0.0 while carrying (C7 D2, CarryingComponent(+0x284)+8 != −1,
    /// read once at 0x00512D14); for a state whose frame id equals robot+0x2B0, the first time (+0x528 = −1) it sends
    /// 50 and falls through, then |after − before| is added, and past 50 mm it sends 400 once (+0x52C). Both happen once
    /// per Robot object. The pose update itself happens on every accepted state.
    /// The 150 send (SC4a..SC4c) is built in
    /// <see cref="UpdateCliffRunningStats"/>/<see cref="UpdateCliffDetectThreshold"/>/<see cref="IncrementSuspiciousCliffCount"/>;
    /// ClearCliffRunningStats is wired to the Delocalize path (<see cref="CozmoEngine.RobotDelocalized"/>).
    /// </summary>
    private void CliffThresholdSchedule(RobotState s)
    {
        if (_robot.Engine.Robot is null) return;
        var sends = new List<ushort>();
        lock (_gate)
        {
            // C7 D2: d = 0.0 while carrying, −20.0 otherwise; read once, the same value for both poses.
            float d = _robot.Motion.IsCarryingObject?.Invoke() == true ? 0f : DriveCenterOffsetMm;
            var before = MoveRobotPoseForward(_poseX, _poseY, _poseAngle, d);
            _poseX = s.Pose.X; _poseY = s.Pose.Y; _poseAngle = s.Pose.Angle;
            var after = MoveRobotPoseForward(_poseX, _poseY, _poseAngle, d);
            if (!FrameMatches(s)) return;
            if (_cliffDistanceMm < 0f)
            {
                _cliffDistanceMm = 0f;
                sends.Add(StartCliffThreshold);
            }
            float dx = after.X - before.X, dy = after.Y - before.Y;
            _cliffDistanceMm += MathF.Sqrt(dx * dx + dy * dy);
            if (_cliffDistanceMm > 50f && !_cliffDistanceDone)
            {
                _cliffDistanceDone = true;
                sends.Add(DefaultCliffThreshold);
            }
        }
        foreach (var t in sends) SendCliffDetectThresholdToRobot(t);
    }

    // fidelity: M4-019
    /// <summary>
    /// SetOnChargerPlatform(b) (C8 P1, 0x00511D4C..0x00511DB0): new = b, or the contacts flag when b is false; on a change
    /// RobotOnChargerPlatformEvent{new}, then SendCliffDetectThresholdToRobot(new ? 50 : 400). (The FreeplayDataTracker
    /// pause flag 3 that follows is M15's.)
    /// MISSING: Robot::Update also sets it false when no charger is located or the robot footprint no longer intersects
    /// the charger quad (C8 P7..P9, 0x00513C5C..0x00513E2A); that is M11 geometry and not built.
    /// </summary>
    private void SetOnChargerPlatform(bool b)
    {
        bool changed, now;
        lock (_gate)
        {
            now = b || _onChargerContacts;
            changed = now != _onChargerPlatform;
            _onChargerPlatform = now;
        }
        if (!changed) return;
        OnChargerPlatformChanged?.Invoke(now);
        SendCliffDetectThresholdToRobot(now ? StartCliffThreshold : DefaultCliffThreshold);
    }

    // fidelity: M4-019
    /// <summary>
    /// SetOnCharger(onContacts) (C8 P3..P5, 0x005119AA..0x00511C14): with the bit set and +0x338 still 0,
    /// SetOnChargerPlatform(true) (and the ChargerEvent); with it clear the platform flag is left alone; then +0x338 :=
    /// the bit. The located-charger creation and its dock-pose observation (P4 steps 1..4) are M11/M13's.
    /// </summary>
    /// <summary>The engine's <c>ChargerEvent{onCharger}</c> broadcast (tag 0x39) from <c>SetOnCharger</c>'s edges.</summary>
    // fidelity: M7-021
    public event Action<bool>? ChargerEvent;

    private void SetOnCharger(bool onContacts)
    {
        bool rising, falling;
        lock (_gate) { rising = onContacts && !_onChargerContacts; falling = !onContacts && _onChargerContacts; }
        if (rising) SetOnChargerPlatform(true);
        // ChargerEvent{1} on the rising edge (0x00511BF4..0x00511C0A), {0} on the falling edge (0x00511AAE..0x00511AC4)
        if (rising) ChargerEvent?.Invoke(true);
        else if (falling) ChargerEvent?.Invoke(false);
        lock (_gate) _onChargerContacts = onContacts;
    }

    // fidelity: M4-019
    /// <summary>
    /// PotentialCliff 0xC1 (SC7, 0x0053582C..0x00535998): ignored on the charger platform (+0x34A, C8); in drone mode a
    /// TriggerLiftSafeAnimationAction; otherwise, when not in SDK mode, StopAllMotors and EnableStopOnCliff{0}.
    /// This stack is not in SDK mode and has no drone mode (EnableDroneMode, SC8, is not offered).
    /// </summary>
    private void HandlePotentialCliff()
    {
        lock (_gate) if (_onChargerPlatform) return;
        _robot.Motion.StopAllMotors();
        _robot.SendMessage(new EnableStopOnCliff(false));
    }

    /// <summary>Waits for the robot to report a cliff, or gives up.</summary>
    public async Task<CliffReport?> WaitForCliffAsync(TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<CliffReport>(TaskCreationOptions.RunContinuationsAsynchronously);
        void watch(CliffReport c) => tcs.TrySetResult(c);
        CliffDetected += watch;
        try
        {
            return await Task.WhenAny(tcs.Task, Task.Delay(timeout)) == tcs.Task ? tcs.Task.Result : null;
        }
        finally { CliffDetected -= watch; }
    }

    // ----------------------------------------------------------------- plumbing

    // fidelity: M2-002
    /// <summary>
    /// A status bit of the latest RobotState. The flag names and values are the engine's (EnumToString 0x007D57C8,
    /// Unity RobotStatusFlag.cs:8-25); where the engine stores each consumed bit is recorded in M2-002, and what
    /// it does with it afterwards belongs to the layer that consumes it (M4).
    /// </summary>
    private bool Flag(RobotStatusFlag f) => _state.Latest?.Has(f) ?? false;

    private bool _lastFalling;
    private bool _lastPickedUp, _lastOnCharger, _haveBaseline;

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26, CC27): no cliff history, no
    /// transition baseline, and the classifier and the movement detector as built. Subscribers are kept.
    /// </summary>
    internal void ResetToConstructed()
    {
        lock (_gate)
        {
            _cliffs.Clear();
            _cliffEnabled = true;
            _cliffDetectedByEvent = false;
            _cliffDetectedFlag = false;
            _cliffTimestamp = 0;
            _cliffThresholdCache = DefaultCliffThreshold;
            _cliffRaw = new ushort[] { 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF };
            _cliffDistanceMm = -1.0f;
            _cliffDistanceDone = false;
            _poseX = _poseY = _poseAngle = 0f;
            _onChargerContacts = false;
            _onChargerPlatform = false;
            _cliffSamples.Clear();
            _cliffMean = _cliffVariance = _cliffM2 = 0f;
            _cliffSuspiciousCount = 0;
            _cliffSuspiciousAt = 0;
            _frameMismatchCount = 0;
            _cliffHistory.Clear();
        }
        _lastFalling = false;
        _lastPickedUp = false;
        _lastOnCharger = false;
        _haveBaseline = false;
        DelocalizeTrigger = null;
        OffTreads.ResetToConstructed();
        UnexpectedMovement.ResetToConstructed();
    }

    /// <summary>Fed every robot message by <see cref="CozmoRobot"/>.</summary>
    internal void Handle(RobotMessage m)
    {
        switch (m)
        {
            case CliffEvent c:
            {
                // fidelity: M4-019
                // SC5 (0x00535548..0x005356E6): enabled with flags ≠ 0 runs ComputeCliffPose and AddCliff (the M11/M13
                // map interface, not built here); disabled with flags ≠ 0 drops the event with no broadcast; otherwise
                // +5 = (flags ≠ 0) and the event goes to the game. Nothing is sent to the robot.
                bool any = c.DetectedFlags != 0;
                lock (_gate)
                {
                    if (!_cliffEnabled && any) break;
                    _cliffDetectedByEvent = any;
                }
                var report = new CliffReport(c.Timestamp, (CliffSensors)c.DetectedFlags, c.DidStopForCliff);
                lock (_gate)
                {
                    _cliffs.Add(report);
                    if (_cliffs.Count > 256) _cliffs.RemoveAt(0);
                }
                CliffDetected?.Invoke(report);
                break;
            }

            case PotentialCliff:
                HandlePotentialCliff();
                break;

            case Protocol.RobotStopped rs:
                // fidelity: M4-019
                // H1..H4 HandleRobotStopped tag 0xD4 (0x0053539C..0x00535480): H3 Evaluate sets comp+0x1C := the
                // Robot's last state timestamp (robot+0x2C); if the cliff sensor is enabled (+4 != 0), RobotStopped is
                // broadcast. The preceding BehaviorManager::RequestCurrentBehaviorEndImmediately and
                // ActionList::Cancel(-1) are the M7/M8 interfaces, named in M4-019's unresolved and not wired here.
                // Nothing is sent to the robot.
                EvaluateCliffSuspiciousnessWhenStopped(_robot.Engine.Robot?.StoredState?.Timestamp ?? _cliffTimestamp);
                bool stoppedEnabled;
                lock (_gate) stoppedEnabled = _cliffEnabled;
                if (stoppedEnabled) RobotStopped?.Invoke(rs.Field0);
                break;

            case Protocol.FallingStarted fs:
                // fidelity: M10-011
                // C1 (0x534F4C..0x534F9E): log, then the game FallingStarted{timestamp}.
                _robot.Engine.Log($"info: RobotImplMessaging.HandleFallingStarted: timestamp {fs.Unknown}");
                FallingStarted?.Invoke(fs.Unknown);
                break;

            case Protocol.FallingStopped f:
                // fidelity: M2-013, M10-011
                // C2 (0x5350AA..0x5350C2; 0x535186..0x53519C): intensity > 1000 registers NeedsAction 17, then the DAS
                // "robot.falling_event", then the game FallingStopped{duration, intensity}.
                if (f.ImpactIntensity > FallNeedsActionIntensity) NeedsActionCompleted?.Invoke(FallNeedsActionId);
                _robot.Engine.Log($"info: DAS robot.falling_event: duration {f.DurationMs} ms, intensity {f.ImpactIntensity:F1}");
                FallingStopped?.Invoke(new FallingStoppedReport(f.DurationMs, f.ImpactIntensity) { Timestamp = f.Timestamp });
                break;

            case MotorCalibration mc:
                MotorCalibrationReported?.Invoke(mc);
                if (mc.CalibStarted && mc.AutoStarted) AutoCalibrationStarted?.Invoke(mc);
                // fidelity: M4-012
                // MA20: a lift that starts calibrating while carrying detaches the carried object.
                if (mc.MotorID == MotorID.MOTOR_LIFT && mc.CalibStarted) UnattachCarriedObjectIfCarrying?.Invoke();
                break;

            case RobotState s:
                // fidelity: M4-008
                // SC2 UpdateRobotData (0x00634016..0x00634036): the raw values to +0xE, CLIFF_DETECTED to +6, the
                // timestamp to +8.
                lock (_gate)
                {
                    if (s.CliffDataRaw is { Length: 4 } raw) _cliffRaw = raw.ToArray();
                    _cliffDetectedFlag = s.Has(RobotStatusFlag.CliffDetected);
                    _cliffTimestamp = s.Timestamp;
                    // fidelity: M4-019
                    // The raw-state history the S3 lower_bound walk reads (RobotStateHistory's raw map, history+0):
                    // key = timestamp, value carries cliff[0]. C13.3: the engine's state-history retention is 3000 ms
                    // (RobotStateHistory::RobotStateHistory sets the window at history+0x3c, 0x0053088D..0x005308C6);
                    // the stack's parallel history uses the same window, which only bounds memory.
                    if (s.CliffDataRaw is { Length: 4 } hraw)
                    {
                        _cliffHistory.Add((s.Timestamp, hraw[0]));
                        while (_cliffHistory.Count > 0 && unchecked(s.Timestamp - _cliffHistory[0].Timestamp) > 3000)
                            _cliffHistory.RemoveAt(0);
                    }
                }
                // fidelity: M10-001, M10-005, M10-010
                // UFRS order (M4 C3): the IMU filters and CheckAndUpdateTreadsState (0x00512A72) on BaseStationTimer ms
                // (A2), gated by the head calibration (A1) and fed the physical flag (A4); the commit consequences run
                // inside (A8..A14, the platform clear through the hook set in the constructor).
                var er = _robot.Engine.Robot;
                OffTreads.HeadCalibrated = _state.HeadCalibrated;
                OffTreads.IsPhysical = er?.IsPhysicalRobot ?? false;
                bool onTreadsBefore = OffTreads.Current == OffTreadsState.OnTreads;
                OffTreads.Update(s, _robot.Engine.Timer.TimeStampMs);
                // fidelity: M4-019
                // C13.2 (0x00512B9C..0x00512BAA): a committed change to or from OnTreads stores robot+0x2C0 = 0 and
                // then branches straight to the stats/threshold body at 0x512FB4, past the origin check, the
                // pose/history, the threshold schedule, the frame compare, the frame-match reset and the mismatch
                // increment; the stats run unconditionally (the cmp r6,#0 gate at 0x512FB0 is not reached), so the
                // counter stays 0 for that state.
                bool treadsChanged = (OffTreads.Current == OffTreadsState.OnTreads) != onTreadsBefore;
                if (treadsChanged) _frameMismatchCount = 0;
                // fidelity: M11-044
                // r7 != 0 at 0x00512A94/0x00512A86 (a committed change to or from OnTreads) is what makes UpdateFullRobotState Delocalize at 0x00512B88..0x00512BA6 (VisionSystem does it, for this state, right after the +0x2BC OR).
                if (treadsChanged) DelocalizeTrigger = s;
                // fidelity: M4-019
                // C8 P3..P5: UpdateFullRobotState then feeds SetOnCharger with IS_ON_CHARGER (0x00512AAC..0x00512AB4).
                SetOnCharger(s.Has(RobotStatusFlag.IsOnCharger));
                // fidelity: M4-022
                // SC10 (0x00512AD0..0x00512B52): the body-radio-mode count comes after SetOnCharger (SetBodyRadioMode at
                // 0x00512B46 follows SetOnCharger's threshold send at 0x00512AB4) and before MovementComponent::Update.
                er?.CountBodyRadioMode(s);
                // fidelity: M10-002, M10-006
                // MovementComponent::CheckForUnexpectedMovement (0x0063E398) is tail-called from MovementComponent::Update
                // (0x0063E392), which UpdateFullRobotState runs before the origin check (B1, 0x00512B5C): every synced state.
                // B2: robot+0x14; B3: robot+0x248 (the AnimationState tag) and the BODY track's lock set.
                UnexpectedMovement.IsPhysical = er?.IsPhysicalRobot ?? false;
                UnexpectedMovement.AnimationStateTag = er?.AnimationStateTag ?? 0;
                UnexpectedMovement.BodyTrackLocked = (_robot.Motion.LockedTracks & CozmoMotion.BodyTrack) != 0;
                var movement = UnexpectedMovement.Update(s);
                if (movement is not null) UnexpectedMovementDetected?.Invoke(movement);
                // fidelity: M4-019
                // C13.2: the treads-change branch (0x00512BAA) jumps straight to the stats/threshold body at
                // 0x512FB4, so the stats run for a treads-change state whether or not its origin is accepted, and
                // the threshold schedule and the frame logic are skipped.
                if (treadsChanged)
                {
                    if (s.CliffDataRaw is { Length: 4 } traw) UpdateCliffRunningStats(traw[0]);
                    UpdateCliffDetectThreshold();
                }
                else
                {
                    // fidelity: M4-019, M4-020
                    // The schedule is after the origin check (0x00512D7A..0x00512EA6, C3), so after the treads check
                    // (0x00512A72), SetOnCharger (0x00512AB4) and MovementComponent::Update (0x00512B5C): when both send a
                    // threshold in one state the engine's order is platform first, schedule last. An origin-rejected state
                    // skips it.
                    if (_robot.Engine.Robot?.OriginAccepted(s) == true) CliffThresholdSchedule(s);
                    // fidelity: M4-019
                    // S1 (0x00512FB0..0x00512F22): UpdateCliffRunningStats (0x00512FCE) then UpdateCliffDetectThreshold
                    // (0x00512FD6) run when r6 == 0 - the frame-match path and the frame-mismatch path while robot+0x2C0
                    // < 0x65. C12.3: the counter is per-mismatch-run: 0 on a frame match, old+1 on a mismatch, 0 once the
                    // count reaches 0x65 (so the 101st mismatch still skips the stats). C12.4: r6 = 1 also on an
                    // AddRobotStateToHistory failure (0x00512C38) and a GetLastStateWithFrameID failure (0x00513110); the
                    // origin miss is the OriginAccepted gate below. MISSING: this layer has no history-add or pose-frame
                    // failure signal (the stack's RobotStateHistory.Add is void and lives in M11; there is no
                    // GetLastStateWithFrameID), so those two cannot be included.
                    if (_robot.Engine.Robot?.OriginAccepted(s) == true)
                    {
                        bool mismatch = !FrameMatches(s);
                        bool statsAllowed;
                        if (mismatch)
                        {
                            _frameMismatchCount++;
                            statsAllowed = _frameMismatchCount < 0x65;
                            if (_frameMismatchCount >= 0x65) _frameMismatchCount = 0;
                        }
                        else
                        {
                            _frameMismatchCount = 0;
                            statsAllowed = true;
                        }
                        if (statsAllowed)
                        {
                            if (s.CliffDataRaw is { Length: 4 } sraw) UpdateCliffRunningStats(sraw[0]);
                            UpdateCliffDetectThreshold();
                        }
                    }
                }

                bool picked = s.Has(RobotStatusFlag.IsPickedUp);
                bool charger = s.Has(RobotStatusFlag.IsOnCharger);
                bool falling = s.Has(RobotStatusFlag.IsFalling);
                if (!_haveBaseline)
                {
                    // The first state is a baseline, not a transition: reporting it as one would fire a
                    // reaction merely for connecting to a robot that was already on its charger.
                    _lastPickedUp = picked; _lastOnCharger = charger; _lastFalling = falling;
                    _haveBaseline = true;
                    break;
                }
                if (picked != _lastPickedUp) { _lastPickedUp = picked; PickedUpChanged?.Invoke(picked); }
                if (charger != _lastOnCharger) { _lastOnCharger = charger; OnChargerChanged?.Invoke(charger); }
                if (falling != _lastFalling) { _lastFalling = falling; FallingChanged?.Invoke(falling); }
                break;
        }
    }
}
