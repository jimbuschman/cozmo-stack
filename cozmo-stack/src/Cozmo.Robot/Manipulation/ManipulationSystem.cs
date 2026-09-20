using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// The cube-manipulation foundation: paths, docking and carrying over the M11 world model. This is what the
/// engine spreads over <c>PathComponent</c>, <c>DockingComponent</c>, <c>CarryingComponent</c> and the action
/// classes; the actions here are async methods rather than the engine's ticked <c>IActionRunner</c>s.
/// </summary>
public sealed class ManipulationSystem : IDisposable
{
    public ManipulationSystem(CozmoRobot robot, VisionSystem vision)
    {
        Robot = robot; Vision = vision;
        Paths = new PathSender(robot);
        Follower = new PathFollower(robot);
        Docking = new DockingSystem(robot, vision);
        Docking.Log += l => Log?.Invoke(l);
    }

    public CozmoRobot Robot { get; }
    public VisionSystem Vision { get; }
    public BlockWorld World => Vision.World;
    public PathSender Paths { get; }
    public PathFollower Follower { get; }
    public DockingSystem Docking { get; }
    public event Action<string>? Log;

    /// <summary>Replaceable turn, for tests without a robot (the locator's own override is used when set).</summary>
    public Func<uint, double, CancellationToken, Task<bool>>? TurnOverride { get; set; }

    public Pose3d? RobotPose() => Vision.History.Latest?.RobotPose;

    /// <summary><c>TurnTowardsObjectAction</c>: the located object's pose through <see cref="TurnTowardsPose"/>.</summary>
    public Task<bool> TurnTowardsObjectAsync(uint objectId, double maxTurnRad, CancellationToken cancel)
    {
        if (TurnOverride is not null) return TurnOverride(objectId, maxTurnRad, cancel);
        return Vision.Locator.TurnTowardsAsync(objectId, maxTurnRad, cancel);
    }

    /// <summary>
    /// <c>VisuallyVerifyObjectAction</c>: waits until a frame observes the object and returns the marker seen
    /// (the closest to the image centre when several). Null when nothing is seen in time.
    /// </summary>
    public async Task<KnownMarker?> WaitForVisibleMarkerAsync(uint objectId, TimeSpan timeout, CancellationToken cancel)
    {
        var tcs = new TaskCompletionSource<KnownMarker?>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnObserved(ObjectObservation o)
        {
            if (o.Object.ObjectId != objectId) return;
            var code = o.Markers.FirstOrDefault();
            tcs.TrySetResult(o.Object.Markers.FirstOrDefault(m => m.Code == code));
        }
        World.ObjectObserved += OnObserved;
        try
        {
            // a sighting in the frame just processed counts
            var obj = World.GetObjectById(objectId);
            if (obj is { IsLocated: true } && Vision.LastResult is { } last && last.Objects.Any(x => x.Object.ObjectId == objectId))
                return obj.Markers.FirstOrDefault(m => obj.LastObservedMarkers.Contains(m.Code));
            using var reg = cancel.Register(() => tcs.TrySetResult(null));
            var done = await Task.WhenAny(tcs.Task, Task.Delay(timeout, CancellationToken.None));
            return done == tcs.Task ? tcs.Task.Result : null;
        }
        finally { World.ObjectObserved -= OnObserved; }
    }

    public void Dispose()
    {
        Follower.Dispose();
        Docking.Dispose();
    }
}

/// <summary>
/// The engine's <c>PickupBlockHelper</c> (0x005B7B48..0x005B8610) and <c>RollBlockHelper</c>: drive to the
/// object's pre-action pose when not already there ("Cozmo is not at pre-action pose for cube %d, delegating to
/// driveToHelper"), then run the dock action without its own pre-action check ("Picking up target object %d");
/// on a failed dock, retry ("Failed dock attempt %d / %d"), giving up after the attempt limit ("Failing helper
/// because pickup was already attempted %d times"); a visual-observation failure that saw an unexpected
/// object marks the target as failed and tries another; other failures retry from a different pre-dock pose.
/// INFERRED: the attempt limit (3). The search-for-block fallback is DEFERRED.
/// </summary>
public sealed class DockHelper
{
    public const int MaxAttempts = 3;
    private readonly ManipulationSystem _m;

    public DockHelper(ManipulationSystem m) => _m = m;

    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();
    public int Attempts { get; private set; }

    /// <summary>Drive to the object (for the action's pre-action type) and run the dock action, with retries.</summary>
    public async Task<ActionResult> RunAsync(uint objectId, PreActionType type, Func<DockActionBase> makeAction, CancellationToken cancel)
    {
        ActionResult last = ActionResult.Abort;
        for (Attempts = 1; Attempts <= MaxAttempts; Attempts++)
        {
            if (cancel.IsCancellationRequested) return ActionResult.CancelledWhileRunning;
            var drive = new DriveToObjectAction(_m, objectId, type);
            var d = await drive.RunAsync(cancel);
            _trace.AddRange(drive.Trace);
            if (d != ActionResult.Success)
            {
                _trace.Add($"drive to pre-action pose: {d}");
                if (d is ActionResult.BadObject or ActionResult.NoPreActionPoses) return d;
                last = d;
                continue;
            }
            var action = makeAction();
            action.CheckPreActionPose = false;
            var r = await action.RunAsync(cancel);
            _trace.AddRange(action.Trace);
            if (r == ActionResult.Success) return r;
            last = r;
            if (r is ActionResult.BadObject or ActionResult.CancelledWhileRunning) return r;
            _trace.Add($"Failed dock attempt {Attempts} / {MaxAttempts}");
        }
        _trace.Add($"Failing helper because the action was already attempted {MaxAttempts} times");
        return last;
    }
}
