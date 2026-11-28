using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot;

/// <summary>The current configured activity, running behavior and reaction.</summary>
public sealed record RobotActivityObservation(string? Activity, string? HighLevelActivity, string? Behavior, string? Reaction);

internal sealed record RobotFreeplayObservation(bool Enabled, bool Active, bool PausedForCommand,
    string? Activity, string? Behavior, float? LastResumedAtEngineSeconds);

public sealed partial class CozmoRobot
{
    internal RobotFreeplayObservation? GetFreeplayObservation()
    {
        using var lease = Engine.EnterSynchronousOwner();
        return Engine.ConnectionState == 2 ? Engine.Robot?.NativeBehaviorRuntime?.ObserveHostFreeplay() : null;
    }

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
            throw new InvalidOperationException("Ground placement requires a connected robot with an active manipulation system.");
        return manipulation.PlaceObjectOnGroundAsync(owner, cancellationToken);
    }
}
