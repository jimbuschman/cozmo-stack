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
        if (!TryNativeAngles(M,_owner,_x,_y,_timestamp,()=>ScopedCurrent && !DestructionObserved,out var body,out var head))
        {
            if(!ScopedCurrent || DestructionObserved)return unchecked((int)EngineActionResult.Cancelled);
            _owner.Engine.EmitSourceLog(EngineLogLevel.Warning,null,
                "TurnTowardsImagePointAction.Init.ComputeTurnTowardsImagePointAnglesFailed",
                FormattableString.Invariant($"({_x:F6},{_y:F6}) at t={_timestamp}"));
            return unchecked((int)0x03000000);
        }
        Parameters.PanAngleRad=body;Parameters.HeadAngleRad=head;
        if (!ScopedCurrent || DestructionObserved) return unchecked((int)EngineActionResult.Cancelled);
        return base.Init();
    }

    internal static bool TryNativeAngles(ManipulationSystem m,EngineRobot owner,float x,float y,uint timestamp,Func<bool> current,out float body,out float head)
    {
        body=0;head=0;
        using (m.World.EnterPoseMutation())
        {
        if (!current()) return false;
        var cal = m.Vision.NativeOuterCalibrationFor(owner);
        if (cal is null)
        {
            Behavior.SteppedBehavior.ReportMissing("M1-034/M14-005: image-point turn has no original outer camera calibration");
            return false;
        }
        using var historical = new HistRobotState();
        uint resolved = 0;
        uint result = owner.PoseHistory.ComputeStateAt(timestamp, ref resolved, historical, false);
        if (result != 0)
        {
            owner.Engine.EmitSourceLog(EngineLogLevel.Warning, null,
                "Robot.ComputeTurnTowardsImagePointAngles.ComputeHistPoseFailed", $"t={timestamp}");
            return false;
        }
        var angles = TurnTowardsImagePoint.Angles(cal, x, y, historical.Pose.AngleAroundZ, historical.State.HeadAngle);
        body=(float)angles.BodyRad;head=(float)angles.HeadRad;
        }
        return current();
    }

    public override void WatcherEnding()
    {
        base.WatcherEnding();
        Completion.TrySetResult(State);
    }
}
