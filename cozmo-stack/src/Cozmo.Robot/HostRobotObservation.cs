using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot;

/// <summary>The current configured activity, running behavior and reaction.</summary>
public sealed record RobotActivityObservation(string? Activity, string? HighLevelActivity, string? Behavior, string? Reaction);

public sealed partial class CozmoRobot
{
    /// <summary>Returns the current activity and behavior from the robot's own behavior manager.</summary>
    public RobotActivityObservation GetActivityObservation()
    {
        using var lease = Engine.EnterSynchronousOwner();
        var owner = Engine.Robot;
        var runtime = owner?.NativeBehaviorRuntime;
        var manager = owner?.BehaviorManagerOwner;
        var selected = runtime?.SelectedActivityId;
        return new(runtime?.Observe().Activity ?? selected, selected, manager?.Current?.Id, manager?.CurrentReactionTrigger?.ToString());
    }

    /// <summary>Queues the existing native ground-placement action on the current robot.</summary>
    public Task<ActionResult> PlaceObjectOnGroundAsync(CancellationToken cancellationToken = default)
    {
        using var lease = Engine.EnterSynchronousOwner();
        var owner = Engine.Robot;
        var manipulation = owner?.NativeBehaviorRuntime?.Manipulation;
        if (manipulation is null)
            throw new InvalidOperationException("MISSING: M15-022 original ground-placement owners unavailable");
        return manipulation.PlaceObjectOnGroundAsync(owner, cancellationToken);
    }
}
