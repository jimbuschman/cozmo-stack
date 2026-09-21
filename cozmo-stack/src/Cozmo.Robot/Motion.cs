using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>Why a motion call finished.</summary>
public enum MotionResult
{
    /// <summary>The robot acknowledged the action, or the state it reports matches what was asked for.</summary>
    Acknowledged,
    /// <summary>Nothing came back in time. The command was sent; whether it took effect is unknown.</summary>
    TimedOut,
    /// <summary>Refused before anything was sent, because the robot was not in a state to accept it.</summary>
    Refused,
}

/// <summary>Outcome of a motion call, with whatever the robot said about it.</summary>
public sealed record MotionOutcome(MotionResult Result, string Detail)
{
    public bool Ok => Result == MotionResult.Acknowledged;
    public override string ToString() => $"{Result}: {Detail}";
}

/// <summary>
/// Cozmo's motors: wheels, head and lift.
///
/// Positioning commands carry an action id and the robot answers with <see cref="MotorActionAck"/> carrying
/// the same id, so these wait for that rather than reporting success because a datagram left the socket.
/// Wheel commands have no acknowledgement, so they are confirmed against the wheel speeds and the
/// ARE_WHEELS_MOVING flag the robot reports in its state stream instead.
///
/// The robot recalibrates head and lift on every connect and ignores or fights motion commands while that
/// runs, so every method here refuses to move until calibration is done unless explicitly told otherwise.
/// </summary>
public sealed class CozmoMotion
{
    private readonly CozmoRobot _robot;
    private byte _nextActionId = 1;
    private readonly object _gate = new();

    internal CozmoMotion(CozmoRobot robot) => _robot = robot;

    /// <summary>Action ids cycle 1..255; 0 is left alone because the robot uses it for unsolicited acks.</summary>
    private byte NextActionId()
    {
        lock (_gate)
        {
            byte id = _nextActionId;
            _nextActionId = _nextActionId == 255 ? (byte)1 : (byte)(_nextActionId + 1);
            return id;
        }
    }

    /// <summary>
    /// The engine's own head limits. <c>Robot::SetHeadAngle(float const&amp;)</c> at 0x00513358 in
    /// libcozmoEngine.so clamps to -0.436332 rad (-25 degrees, literal 0xBEDF66F3) and 0.776672 rad
    /// (44.5 degrees, literal 0x3F46D3F2), warning first when the request is more than 3 degrees beyond
    /// either (-0.488692 and 0.829031). These match the values PyCozmo documents, which is where they were
    /// first taken from.
    /// </summary>
    public const float MinHeadAngleRad = -0.4363323f;   // -25 degrees
    public const float MaxHeadAngleRad = 0.7766715f;    // +44.5 degrees
    /// <summary>
    /// The engine's lift presets, from the table at 0x00C54688 in .rodata: 32 mm (id 1, the low dock
    /// height), 76 mm (id 2, high dock) and 92 mm (id 3, carry). The lowest and highest presets are the
    /// range clamped to here; the engine itself converts a height to an angle before commanding the
    /// motor (<c>Robot::SetLiftAngle</c> at 0x00513485).
    /// </summary>
    public const float MinLiftHeightMm = 32.0f;
    public const float MaxLiftHeightMm = 92.0f;
    /// <summary>
    /// Wheel speed the robot is documented to accept, from PyCozmo. Beyond this the robot clamps, it does
    /// not fault, so this is advisory. The engine's own ceiling was not located; 220.0 appears once in
    /// .rodata (0x00C48F14) without a resolved owner.
    /// </summary>
    public const float MaxWheelSpeedMmps = 200.0f;

    /// <summary>True while the robot says at least one wheel is turning.</summary>
    public bool WheelsMoving => _robot.State.Latest?.Has(RobotStatusFlag.AreWheelsMoving) ?? false;
    /// <summary>Left and right wheel speed in mm/s, as the robot reports them.</summary>
    public (float Left, float Right) WheelSpeeds =>
        _robot.State.Latest is { } s ? (s.LwheelSpeedMmps, s.RwheelSpeedMmps) : (0f, 0f);

    /// <summary>
    /// The readiness gate. The engine has no equivalent: nothing on its motion path consults
    /// <c>Robot::IsHeadCalibrated</c> 0x00512378 or <c>Robot::IsLiftCalibrated</c> 0x005151A6 - their only
    /// callers are the gyro drift detector, <c>CalibrateMotorAction::CheckIfDone</c>,
    /// <c>BehaviorReactToImpact</c> and <c>BehaviorReactToMotorCalibration</c> - and
    /// <c>MovementComponent::MoveHeadToAngle</c> and <c>MoveLiftToHeight</c> send whatever they are given.
    ///
    /// What the engine does instead is at the behaviour layer, and this stack has it:
    /// <c>HandleMotorCalibration</c> 0x00536A68 sets the two flags from the message (calibrated is
    /// <c>!calibStarted</c>, at 0x00536BCE and 0x00536BE4), unattaches a carried object when the lift
    /// starts calibrating, and broadcasts; <c>BehaviorReactToMotorCalibration::InitInternal</c> 0x006065F0
    /// then locks the reactions off and waits 5 seconds (<c>WaitAction</c> with 0x40A00000 at 0x00606650),
    /// ending early when both motors report calibrated. That is
    /// <see cref="Behavior.ReactToMotorCalibrationBehavior"/>.
    ///
    /// The gate below is this stack's own, because a caller here can command a motor directly where the
    /// engine's callers are behaviours that the reaction has already displaced. It is a default, not a
    /// rule: every caller can pass <c>requireCalibration: false</c>, and the resume path and the
    /// behaviours that move a motor deliberately do.
    /// </summary>
    private MotionOutcome? NotReady(bool requireCalibration)
    {
        if (_robot.State.Latest is null)
            return new MotionOutcome(MotionResult.Refused, "the robot has not sent any state yet");
        // Not merely "is not calibrating right now": a RobotState arriving before any MotorCalibration
        // message would otherwise look like readiness, and normal motion would be allowed during the
        // window before calibration has even been reported as started.
        if (requireCalibration && !_robot.State.CalibrationComplete)
            return new MotionOutcome(MotionResult.Refused,
                _robot.State.CalibratingMotors
                    ? "head and lift are still calibrating"
                    : "head and lift have not finished calibrating yet");
        return null;
    }

    // ------------------------------------------------------------------- wheels

    /// <summary>
    /// Drives the wheels at the given speeds in mm/s until told otherwise. Negative drives backwards.
    /// Confirmed against the wheel speeds the robot reports, not against the send succeeding.
    /// </summary>
    public async Task<MotionOutcome> DriveWheelsAsync(float leftMmps, float rightMmps,
                                                      float leftAccelMmps2 = 0f, float rightAccelMmps2 = 0f,
                                                      TimeSpan? confirmWithin = null,
                                                      bool requireCalibration = true)
    {
        if (NotReady(requireCalibration) is { } refused) return refused;
        _robot.Transport.Send(new DriveWheels(leftMmps, rightMmps, leftAccelMmps2, rightAccelMmps2), flush: true);

        // Confirmed against what was actually asked for, per wheel.
        //
        // This used to accept any wheel motion at all, which meant a robot already rolling satisfied the
        // check the instant the command was sent. Commanding forward while it was driving backwards would
        // report success without anything having changed. Each wheel now has to be turning the way it was
        // told to, at roughly the speed it was told.
        var outcome = await AwaitState(
            s => WheelMatches(s.LwheelSpeedMmps, leftMmps) && WheelMatches(s.RwheelSpeedMmps, rightMmps),
            confirmWithin ?? TimeSpan.FromSeconds(2));

        var (l, r) = WheelSpeeds;
        return outcome
            ? new MotionOutcome(MotionResult.Acknowledged,
                $"robot reports wheels at {l:F0}/{r:F0} mm/s, asked for {leftMmps:F0}/{rightMmps:F0}")
            : new MotionOutcome(MotionResult.TimedOut,
                $"robot reports wheels at {l:F0}/{r:F0} mm/s, asked for {leftMmps:F0}/{rightMmps:F0}");
    }

    /// <summary>
    /// Whether one wheel's reported speed matches what it was asked for.
    ///
    /// A stop must actually be stopped. A move must be turning the right way — sign matters, because the
    /// difference between forwards and backwards is the whole point — and be within a tolerance of the
    /// requested speed, generous enough to allow for the robot still ramping up and for its own reporting
    /// resolution.
    /// </summary>
    internal static bool WheelMatches(float reported, float requested)
    {
        const float stopped = 5f;          // mm/s the robot may still report while coasting to a halt
        if (Math.Abs(requested) <= 0.01f) return Math.Abs(reported) <= stopped;
        if (Math.Sign(reported) != Math.Sign(requested)) return false;
        float tolerance = Math.Max(stopped, Math.Abs(requested) * 0.35f);
        return Math.Abs(Math.Abs(reported) - Math.Abs(requested)) <= tolerance;
    }

    /// <summary>Stops the wheels by commanding zero speed. Does not touch head or lift.</summary>
    public Task<MotionOutcome> StopWheelsAsync(TimeSpan? confirmWithin = null)
        => DriveWheelsAsync(0f, 0f, confirmWithin: confirmWithin, requireCalibration: false);

    // --------------------------------------------------------------- head and lift

    /// <summary>
    /// The engine's default head speed, 15 rad/s. <c>MoveHeadToAngleAction</c>'s constructor writes the
    /// pair 15 and 20 into the action at 0x00547F14 (<c>movt r0, #0x41a0</c> and <c>movt r1, #0x4170</c>,
    /// stored by <c>strd r1, r0, [fp, #0x90]</c>), and <c>Init</c> 0x00548534 passes them straight on as
    /// <c>MovementComponent::MoveHeadToAngle(angle, speed, accel, duration, ...)</c> with the duration
    /// from +0x98, which the constructor leaves at zero.
    /// </summary>
    public const float DefaultHeadSpeedRadPerSec = 15f;

    /// <summary>The engine's default head acceleration, 20 rad/s^2, from the same pair.</summary>
    public const float DefaultHeadAccelRadPerSec2 = 20f;

    /// <summary>
    /// The engine's default lift speed, 10 rad/s. <c>MoveLiftToHeightAction</c>'s constructor writes 0, 10
    /// and 20 to +0x88, +0x8C and +0x90 (0x00548A68..0x00548A78) and <c>Init</c> 0x0054903C passes them as
    /// <c>MoveLiftToHeight(height, speed, accel, duration, ...)</c> at 0x00549306: <c>ldrd r2, r3,
    /// [r4, #0x8c]</c> for the speed and acceleration, the duration from +0x88.
    /// </summary>
    public const float DefaultLiftSpeedRadPerSec = 10f;

    /// <summary>The engine's default lift acceleration, 20 rad/s^2, from the same three.</summary>
    public const float DefaultLiftAccelRadPerSec2 = 20f;

    /// <summary>
    /// Moves the head to an absolute angle in radians and waits for the robot to acknowledge the action.
    /// The angle is clamped to the robot's documented range, and the speed and acceleration default to the
    /// engine's own (<see cref="DefaultHeadSpeedRadPerSec"/>).
    /// </summary>
    public Task<MotionOutcome> SetHeadAngleAsync(float radians,
                                                 float maxSpeedRadPerSec = DefaultHeadSpeedRadPerSec,
                                                 float accelRadPerSec2 = DefaultHeadAccelRadPerSec2,
                                                 float durationSec = 0f,
                                                 TimeSpan? timeout = null, bool requireCalibration = true)
    {
        float clamped = Math.Clamp(radians, MinHeadAngleRad, MaxHeadAngleRad);
        return ActAsync(id => new SetHeadAngle(clamped, maxSpeedRadPerSec, accelRadPerSec2, durationSec, id),
                        $"head to {clamped:F3} rad", timeout, requireCalibration);
    }

    /// <summary>
    /// Moves the lift to an absolute height and waits for the robot to acknowledge the action.
    ///
    /// The height is in millimetres, which is what the engine's own field name says. The robot reports the
    /// lift back as <c>liftAngle</c>, in radians; <see cref="RobotState.LiftHeightMm"/> converts it with the
    /// engine's <c>45 + 66 sin(angle)</c> (<c>Robot::GetLiftHeight</c> 0x00516F64), and the engine's inverse
    /// clamps to this same 32..92 mm range (<c>ConvertLiftHeightToLiftAngleRad</c> 0x005170B0).
    ///
    /// The speed and acceleration default to the engine's own
    /// (<see cref="DefaultLiftSpeedRadPerSec"/>); the same 32 and 92 mm appear in
    /// <c>MoveLiftToHeightAction::Init</c> as the bounds it warns about (0x0054905E and 0x0054906C).
    /// </summary>
    public Task<MotionOutcome> SetLiftHeightAsync(float heightMm,
                                                  float maxSpeedRadPerSec = DefaultLiftSpeedRadPerSec,
                                                  float accelRadPerSec2 = DefaultLiftAccelRadPerSec2,
                                                  float durationSec = 0f,
                                                  TimeSpan? timeout = null, bool requireCalibration = true)
    {
        float clamped = Math.Clamp(heightMm, MinLiftHeightMm, MaxLiftHeightMm);
        return ActAsync(id => new SetLiftHeight(clamped, maxSpeedRadPerSec, accelRadPerSec2, durationSec, id),
                        $"lift to {clamped:F1} mm", timeout, requireCalibration);
    }

    /// <summary>Turns the head at a speed in rad/s until stopped. No acknowledgement is defined for this one.</summary>
    public MotionOutcome MoveHead(float radPerSec, bool requireCalibration = true)
    {
        if (NotReady(requireCalibration) is { } refused) return refused;
        _robot.Transport.Send(new MoveHead { SpeedRadPerSec = radPerSec }, flush: true);
        return new MotionOutcome(MotionResult.Acknowledged, $"head moving at {radPerSec:F2} rad/s (no ack is defined)");
    }

    /// <summary>Raises or lowers the lift at a speed in rad/s until stopped. No acknowledgement is defined.</summary>
    public MotionOutcome MoveLift(float radPerSec, bool requireCalibration = true)
    {
        if (NotReady(requireCalibration) is { } refused) return refused;
        _robot.Transport.Send(new MoveLift { SpeedRadPerSec = radPerSec }, flush: true);
        return new MotionOutcome(MotionResult.Acknowledged, $"lift moving at {radPerSec:F2} rad/s (no ack is defined)");
    }

    // -------------------------------------------------------------- calibration

    /// <summary>
    /// Asks the robot to recalibrate its head and/or lift: the engine's <c>CalibrateMotorAction</c>
    /// sends <see cref="StartMotorCalibration"/> (0x58) with a flag per motor. The robot answers with
    /// <see cref="MotorCalibration"/> reports, started and then finished, which the state tracker records.
    /// The message's layout is known and the flags are named in the CLAD; that the robot honours them is
    /// hardware-pending (the plan's calibration item).
    /// </summary>
    public void RequestMotorCalibration(bool head, bool lift) =>
        _robot.Transport.Send(new StartMotorCalibration { Head = head, Lift = lift }, flush: true);

    // ------------------------------------------------------------------ stopping

    /// <summary>
    /// Stops every motor at once and confirms the robot reports itself stopped.
    ///
    /// This is the safe exit: it is never gated on calibration, because the whole point is to be able to
    /// stop whatever the robot is currently doing.
    /// </summary>
    public async Task<MotionOutcome> StopAllAsync(TimeSpan? confirmWithin = null)
    {
        _robot.Transport.Send(new StopAllMotors(), flush: true);
        // belt and braces: an explicit zero wheel command as well, in case StopAllMotors only halts actions
        _robot.Transport.Send(new DriveWheels(0f, 0f, 0f, 0f), flush: true);

        bool stopped = await AwaitState(
            s => !s.Has(RobotStatusFlag.AreWheelsMoving) && Math.Abs(s.LwheelSpeedMmps) < 1f && Math.Abs(s.RwheelSpeedMmps) < 1f,
            confirmWithin ?? TimeSpan.FromSeconds(2));

        var (l, r) = WheelSpeeds;
        return stopped
            ? new MotionOutcome(MotionResult.Acknowledged, "robot reports all wheels stopped")
            : new MotionOutcome(MotionResult.TimedOut, $"robot still reports wheels at {l:F0}/{r:F0} mm/s");
    }

    // ------------------------------------------------------------------ plumbing

    /// <summary>Sends an action carrying a fresh id and waits for the matching <see cref="MotorActionAck"/>.</summary>
    private async Task<MotionOutcome> ActAsync(Func<byte, RobotMessage> build, string what,
                                               TimeSpan? timeout, bool requireCalibration)
    {
        if (NotReady(requireCalibration) is { } refused) return refused;

        byte id = NextActionId();
        var acked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void watch(RobotMessage m)
        {
            if (m is MotorActionAck a && a.ActionId == id) acked.TrySetResult(true);
        }
        _robot.Message += watch;
        try
        {
            _robot.Transport.Send(build(id), flush: true);
            var t = timeout ?? TimeSpan.FromSeconds(5);
            if (await Task.WhenAny(acked.Task, Task.Delay(t)) == acked.Task)
                return new MotionOutcome(MotionResult.Acknowledged, $"{what}: robot acknowledged action {id}");
            return new MotionOutcome(MotionResult.TimedOut, $"{what}: no acknowledgement of action {id} within {t.TotalSeconds:F1}s");
        }
        finally { _robot.Message -= watch; }
    }

    /// <summary>Waits until the robot's reported state satisfies a condition.</summary>
    private async Task<bool> AwaitState(Func<RobotState, bool> condition, TimeSpan within)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void watch(RobotState s) { if (condition(s)) done.TrySetResult(true); }
        _robot.State.StateUpdated += watch;
        try
        {
            if (_robot.State.Latest is { } now && condition(now)) return true;
            return await Task.WhenAny(done.Task, Task.Delay(within)) == done.Task;
        }
        finally { _robot.State.StateUpdated -= watch; }
    }
}
