using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Behavior;

// fidelity: M10-003
/// <summary>
/// <c>ReactionTriggerStrategyNoPreDockPoses::ShouldTriggerBehaviorInternal</c> (gap1 8, 0x610E32..0x610E82); flags (0, 1, 0):
/// looks up RamIntoBlock (class 0x3C); reads id = AIWhiteboard+0x70 (gap2 5a: [[robot+0x264]+0x18]+0x70); if −1, false;
/// otherwise resets that field to −1, sets RamIntoBlock+0x11C = id and returns IsRunnable. So an id that finds the
/// behaviour not runnable is consumed all the same. EnabledStateChanged is a no-op (0x60B73A). The +0x70 writers are the
/// M7/M12 behaviour helpers (gap2 5b).
/// </summary>
public sealed class NoPreDockPosesStrategy : ReactionTriggerStrategy
{
    private readonly AIWhiteboard _whiteboard;
    private readonly RamIntoBlockBehavior _behavior;

    public NoPreDockPosesStrategy(AIWhiteboard whiteboard, RamIntoBlockBehavior behavior) { _whiteboard = whiteboard; _behavior = behavior; }

    public override ReactionTrigger Trigger => ReactionTrigger.NoPreDockPoses;
    public override string Basis => "ReactionTriggerStrategyNoPreDockPoses::ShouldTriggerBehaviorInternal 0x610E32..0x610E82: AIWhiteboard+0x70 != -1 -> reset to -1, RamIntoBlock+0x11C = it, IsRunnable";
    public override bool ShouldResumeLast => false;
    public override bool CanInterruptOtherTriggeredBehavior => true;
    public override bool CanInterruptSelf => false;

    protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior)
    {
        if (_whiteboard.NoPreDockPosesObjectId is not { } id) return false;
        _whiteboard.NoPreDockPosesObjectId = null;
        _behavior.PendingTarget = id;
        return behavior.IsRunnable(rc.Context);
    }

    public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }
}
