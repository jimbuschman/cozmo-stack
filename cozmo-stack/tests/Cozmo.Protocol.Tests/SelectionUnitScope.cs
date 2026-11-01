using Cozmo.Robot.Behavior;

namespace Cozmo.Protocol.Tests;

// Explicit local isolation for invented helper-test parents. Actual configured behavior tests use queued 94 instead.
internal sealed class SelectionUnitScope : IDisposable
{
    private readonly BehaviorManager _manager;
    private readonly IManagedActivity? _prior;
    internal SelectionUnitScope(BehaviorManager manager)
    {_manager=manager;_prior=manager.Activity;AssertSelection();manager.Activity=null;}
    private void AssertSelection()
    {if(_prior is null || _prior.GetType().Name!="ConfiguredActivity")throw new InvalidOperationException("Expected actual boot Selection before isolated helper unit");}
    public void Dispose()=>_manager.Activity=_prior;
}

internal static class QueuedSelection
{
    // Keep the actual configured owner through the next real Selection tick using its native ID request.
    internal static void Keep(Cozmo.Robot.CozmoRobot robot,BehaviorManager manager,string id)
    {
        if(manager.Activity?.GetType().Name!="ConfiguredActivity" || !Enum.TryParse<BehaviorID>(id,out var nativeId) ||
           !ReferenceEquals(manager.Current,robot.Engine.Robot?.NativeBehaviorRuntime?.Bound.GetValueOrDefault(id)))
            throw new InvalidOperationException("Expected selected configured owner before queued 94: "+id);
        robot.ExecuteBehaviorById((byte)nativeId,-1);
    }
}
