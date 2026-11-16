using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

/// <summary>A snapshot from the engine's RobotPublicState broadcaster.</summary>
public sealed record RobotPublicStateReport(int UnlockId, bool IsCarrying, bool IsRequestingGame, byte StackHeight,
    float Energy, float Repair, float Play, byte ActivityId, byte ReactionTrigger, byte StageTag, byte Stage);

public sealed partial class CozmoEngine
{
    /// <summary>Published when the engine's public-state broadcaster emits a snapshot.</summary>
    public event Action<RobotPublicStateReport>? RobotPublicStateChanged;

    internal void DeliverRobotPublicState(BehaviorPublicState value)
    {
        var handlers = RobotPublicStateChanged;
        if (handlers is null) return;
        QueueAppEvent(handlers, new RobotPublicStateReport(value.UnlockId, value.Carrying != 0, value.Requesting != 0,
            value.StackHeight, value.Need0, value.Need1, value.Need2, value.Activity, value.Reaction,
            value.StageTag, value.Stage(value.StageTag)));
    }
}
