using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

// fidelity: M8-008, M4-012
// CalibrateMotorAction configured by ReturnedToTreads at 0x00608652..0x00608658.
internal sealed class NativeHeadCalibrationRunner : NativeVisionRunner
{
    private readonly EngineRobot _owner;
    private bool _started;
    private bool _subscribed;
    protected override string ActionName => "CalibrateMotor-Head";

    internal NativeHeadCalibrationRunner(ManipulationSystem manipulation, EngineRobot owner)
        : base(manipulation, 2, 1) => ScopedOwner = _owner = owner; // 0x00547B38..0x00547B46

    public override byte? GetCompletionVariant() => 6;

    public override int Init()
    {
        // 0x00547C2E..0x00547C6C and 0x006405EA..0x006405EE: clear the
        // started latch, send reliable 0x58, then subscribe to MotorCalibration.
        _started = false;
        bool Current() => ScopedOwnerCurrent && !DestructionObserved;
        if (!Current()) return unchecked((int)EngineActionResult.Cancelled);
        if (!_owner.SendCheckedOwned(new StartMotorCalibration { Head = true, Lift = false }, Current))
            return Current() ? 0x03000016 : unchecked((int)EngineActionResult.Cancelled);
        if (!Current()) return unchecked((int)EngineActionResult.Cancelled);
        if (_owner.HasExternalInterface)
        {
            M.Robot.Sensors.MotorCalibrationReportedInternal += OnCalibration;
            _subscribed = true;
        }
        return 0;
    }

    private void OnCalibration(MotorCalibration report)
    {
        // 0x00547E20..0x00547E2C latches the head-start report.
        if (ScopedOwnerCurrent && !DestructionObserved && report.MotorID == MotorID.MOTOR_HEAD && report.CalibStarted)
            _started = true;
    }

    public override uint CheckIfDone()
    {
        // 0x00547D38..0x00547D88 gates on the start latch and the
        // robot's calibrated flag. IAction owns the 30-second timeout.
        if (!ScopedOwnerCurrent) return EngineActionResult.Cancelled;
        if (!_started || !M.Robot.State.HeadCalibrated) return EngineActionResult.Running;
        EmitSource(EngineLogLevel.Info, "Unnamed", "CalibrateMotorAction.CheckIfDone.Done", "");
        return 0;
    }

    public override void WatcherEnding()
    {
        if (_subscribed)
        {
            M.Robot.Sensors.MotorCalibrationReportedInternal -= OnCalibration;
            _subscribed = false;
        }
        base.WatcherEnding();
    }
}
