using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Manipulation;

// Owner and scalar completion join for the public commands. Configured behaviours keep their own unavailable callback.
internal abstract class AiCubeTrickCompound(ManipulationSystem manipulation, EngineRobot? owner, Func<bool>? current, bool retainConfiguredUnavailable = false)
    : CompoundActionSequential(() => manipulation.Robot.Engine.Timer.SecondsF)
{
    internal EngineRobot? AiOwner => owner;
    internal TaskCompletionSource<(uint Result, bool OwnerRetired)>? AiNativeEnded;
    internal Action<NativeActionUnavailable>? Unavailable;
    internal string? HostUnavailable { get; private set; }
    private bool _ending, _updateUnavailable;

    protected bool AiExecutionCurrent() => !_ending && !DestructionObserved && (current?.Invoke() ?? true);

    protected void ReportUnavailable(NativeActionUnavailable error)
    {
        if (owner is null)
        {
            Unavailable?.Invoke(error);
            return;
        }
        if (HostUnavailable is not null) return;
        HostUnavailable = error.Message;
        SteppedBehavior.ReportMissing(error.Message);
        owner.ActionList.DeferHostCleanup(() => { if (owner.ActionList.ContainsRunner(this)) owner.ActionList.Cancel(Tag); });
    }

    public override uint Update()
    {
        if (owner is not null && !AiExecutionCurrent()) return EngineActionResult.Cancelled;
        if (_updateUnavailable || HostUnavailable is not null) return EngineActionResult.Running;
        int depth = Watcher?.HostUpdateDepth ?? 0;
        try { return base.Update(); }
        catch (NativeActionUnavailable error) when (owner is not null || retainConfiguredUnavailable)
        {
            Watcher?.UnwindUnavailableHostUpdate(depth);
            _updateUnavailable = true;
            ReportUnavailable(error);
            return EngineActionResult.Running;
        }
    }

    public override void WatcherEnding()
    {
        if (_ending || DestructionObserved) return;
        _ending = true;
        base.WatcherEnding();
        if (owner is not null)
            AiNativeEnded?.TrySetResult((State, owner.Lifetime.Destroying || !ReferenceEquals(owner.Engine.Robot, owner)));
    }
}
