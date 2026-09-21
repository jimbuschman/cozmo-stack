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

/// <summary>
/// The end of a fall, as the robot reports it in <see cref="FallingStopped"/> (0xDE): how long it fell and
/// how hard it landed. The intensity's unit is not established; the engine compares it against 1000
/// (<c>BehaviorReactToImpact::AlwaysHandle</c> at 0x00606408) to decide whether the landing counts as an
/// impact worth reacting to.
/// </summary>
public sealed record FallingStoppedReport(uint DurationMs, float ImpactIntensity)
{
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
    }

    /// <summary>Raised when the robot reports a cliff.</summary>
    public event Action<CliffReport>? CliffDetected;
    /// <summary>Raised when the robot reports it has been picked up, or put back down.</summary>
    public event Action<bool>? PickedUpChanged;
    /// <summary>Raised when the robot arrives on, or leaves, the charger contacts.</summary>
    public event Action<bool>? OnChargerChanged;

    /// <summary>
    /// Raised when the robot starts or stops reporting that it is falling, derived from
    /// <c>RobotStatusFlag.IsFalling</c> the same way pick-up and charger transitions are. Without this the
    /// reaction table claimed a falling reaction that nothing could ever raise.
    /// </summary>
    public event Action<bool>? FallingChanged;

    /// <summary>
    /// Raised when the robot reports the end of a fall with <see cref="FallingStopped"/> (0xDE). This is
    /// the message the engine's impact reaction keys off, not the status flag: it carries the impact
    /// intensity the reaction is gated on.
    /// </summary>
    public event Action<FallingStoppedReport>? FallingStopped;

    // ------------------------------------------------------------ derived state (M10)

    /// <summary>
    /// The engine's off-treads classifier, fed every robot state. Its thresholds and debounce are read
    /// from <c>Robot::CheckAndUpdateTreadsState</c>; see <see cref="OffTreadsClassifier"/>.
    /// </summary>
    public OffTreadsClassifier OffTreads { get; } = new();

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
    /// <summary>The lift height in millimetres, converted from the angle as the engine converts it.</summary>
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
        _robot.Transport.Send(new IMURequest { LengthMs = (uint)duration.TotalMilliseconds }, flush: true);

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

    /// <summary>
    /// Turns the robot's own stop-on-cliff reflex on or off. With it on the robot halts itself when a cliff
    /// sensor trips, which is what makes driving on a table safe.
    /// </summary>
    public void SetStopOnCliff(bool enable) =>
        _robot.Transport.Send(new EnableStopOnCliff(enable), flush: true);

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

    private bool Flag(RobotStatusFlag f) => _state.Latest?.Has(f) ?? false;

    private bool _lastFalling;
    private bool _lastPickedUp, _lastOnCharger, _haveBaseline;

    /// <summary>Fed every robot message by <see cref="CozmoRobot"/>.</summary>
    internal void Handle(RobotMessage m)
    {
        switch (m)
        {
            case CliffEvent c:
                var report = new CliffReport(c.Timestamp, (CliffSensors)c.DetectedFlags, c.DidStopForCliff);
                lock (_gate)
                {
                    _cliffs.Add(report);
                    if (_cliffs.Count > 256) _cliffs.RemoveAt(0);
                }
                CliffDetected?.Invoke(report);
                break;

            case Protocol.FallingStopped f:
                FallingStopped?.Invoke(new FallingStoppedReport(f.DurationMs, f.ImpactIntensity));
                break;

            case MotorCalibration mc:
                MotorCalibrationReported?.Invoke(mc);
                if (mc.CalibStarted && mc.AutoStarted) AutoCalibrationStarted?.Invoke(mc);
                break;

            case RobotState s:
                // The engine runs its IMU filters and the off-treads classifier inside UpdateFullRobotState
                // before it looks at the status flags; the same order here. The classifier's gate is the
                // head calibration the tracker has already recorded from the robot's own report.
                OffTreads.HeadCalibrated = _state.HeadCalibrated;
                var before = OffTreads.Current;
                if (OffTreads.Update(s, s.Timestamp))
                    OffTreadsStateChanged?.Invoke(before, OffTreads.Current);
                var movement = UnexpectedMovement.Update(s);
                if (movement is not null) UnexpectedMovementDetected?.Invoke(movement);

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
