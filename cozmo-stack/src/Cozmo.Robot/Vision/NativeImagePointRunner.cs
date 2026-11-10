using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

// M14-005: TurnTowardsImagePointAction::Init 0x54B664.
internal sealed class NativeImagePointRunner : NativePanTiltRunner
{
    private readonly EngineRobot _owner;
    private readonly float _x, _y;
    private readonly uint _timestamp;
    internal readonly TaskCompletionSource<uint> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override string ActionName => "TurnTowardsImagePoint";

    internal NativeImagePointRunner(ManipulationSystem m, EngineRobot owner, float x, float y, uint timestamp)
        : base(m, new PanAndTiltAction { PanIsAbsolute = true, HeadIsAbsolute = true }, 0x2A)
    { _owner = owner; ScopedOwner = owner; _x = x; _y = y; _timestamp = timestamp; }

    public override int Init()
    {
        using (M.World.EnterPoseMutation())
        {
        if (!ScopedCurrent || DestructionObserved) return unchecked((int)EngineActionResult.Cancelled);
        var cal = M.Vision.NativeOuterCalibrationFor(_owner);
        if (cal is null)
        {
            Behavior.SteppedBehavior.ReportMissing("M1-034/M14-005: image-point turn has no original outer camera calibration");
            return unchecked((int)0x03000000);
        }
        using var historical = new HistRobotState();
        uint resolved = 0;
        uint result = _owner.PoseHistory.ComputeStateAt(_timestamp, ref resolved, historical, false);
        if (result != 0)
        {
            _owner.Engine.EmitSourceLog(EngineLogLevel.Warning, null,
                "Robot.ComputeTurnTowardsImagePointAngles.ComputeHistPoseFailed", $"t={_timestamp}");
            _owner.Engine.EmitSourceLog(EngineLogLevel.Warning, null,
                "TurnTowardsImagePointAction.Init.ComputeTurnTowardsImagePointAnglesFailed",
                FormattableString.Invariant($"({_x:F6},{_y:F6}) at t={_timestamp}"));
            return unchecked((int)0x03000000);
        }
        var (body, head) = TurnTowardsImagePoint.Angles(cal, _x, _y, historical.Pose.AngleAroundZ, historical.State.HeadAngle);
        if (!ScopedCurrent || DestructionObserved) return unchecked((int)EngineActionResult.Cancelled);
        Parameters.PanAngleRad = body;
        Parameters.HeadAngleRad = head;
        }
        if (!ScopedCurrent || DestructionObserved) return unchecked((int)EngineActionResult.Cancelled);
        return base.Init();
    }

    public override void WatcherEnding()
    {
        base.WatcherEnding();
        Completion.TrySetResult(State);
    }
}
