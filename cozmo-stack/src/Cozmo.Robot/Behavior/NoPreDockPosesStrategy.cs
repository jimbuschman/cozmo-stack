using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// <c>ReactionTriggerStrategyNoPreDockPoses::ShouldTriggerBehaviorInternal</c> 0x00610E32: the shipped map sends
/// NoPreDockPoses to RamIntoBlock (behaviour id 0x3C, looked up by <c>FindBehaviorByIDAndDowncast</c> at 0x00610E4C).
/// When AIWhiteboard+0x70 holds an object (not -1), the strategy resets it to -1 (0x00610E60), hands the object to
/// the behaviour as its target (+0x11C, 0x00610E64) and triggers when the behaviour is runnable (0x00610E6C). The
/// reset happens before the runnable test, so an object that finds the behaviour not runnable is dropped all the
/// same: <see cref="AbandonTarget"/> consumes it too.
/// </summary>
public sealed class NoPreDockPosesStrategy : IReactionTriggerStrategy, ITargetPreparingStrategy
{
    private readonly AIWhiteboard _whiteboard;
    private readonly RamIntoBlockBehavior _behavior;

    public NoPreDockPosesStrategy(AIWhiteboard whiteboard, RamIntoBlockBehavior behavior) { _whiteboard = whiteboard; _behavior = behavior; }

    public ReactionTrigger Trigger => ReactionTrigger.NoPreDockPoses;
    public string Basis => "ReactionTriggerStrategyNoPreDockPoses::ShouldTriggerBehaviorInternal 0x00610E32: AIWhiteboard+0x70 != -1 -> reset to -1, RamIntoBlock+0x11C = it, IsRunnable";

    public bool ShouldTrigger(BehaviorContext context, ReactionTrigger? current, double nowSec)
    {
        if (!PrepareTarget(context, current, nowSec)) return false;
        CommitTarget();
        return true;
    }

    public bool PrepareTarget(BehaviorContext context, ReactionTrigger? current, double nowSec)
    {
        if (_whiteboard.NoPreDockPosesObjectId is not { } id) return false;
        _behavior.PendingTarget = id;
        return true;
    }

    public void CommitTarget() => _whiteboard.NoPreDockPosesObjectId = null;

    public void AbandonTarget() => _whiteboard.NoPreDockPosesObjectId = null;
}
