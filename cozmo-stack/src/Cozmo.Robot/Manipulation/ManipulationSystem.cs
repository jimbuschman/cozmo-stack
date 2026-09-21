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
        Configurations = new BlockConfigurationManager(vision.World, () => ClockSec());
        Whiteboard = new AIWhiteboard(vision.World, () => ClockSec());
        vision.World.ObjectObserved += _ => Configurations.Update();
        vision.World.PoseStateChanged += (_, _, _) => Configurations.Update();
    }

    /// <summary>Seconds on the clock the components stamp with (the robot's clock by default; tests inject theirs).</summary>
    public Func<double> ClockSec { get; set; } = () => Environment.TickCount64 / 1000.0;

    /// <summary>
    /// The engine's lattice planner (M13), when the motion-primitive set is available: <see cref="DriveToPoseAction"/>
    /// plans with it around the world's located objects and falls back to <see cref="StraightLinePlanner"/> when
    /// it is null or finds no plan (the engine's <c>PathComponent::SelectPlanner</c> also keeps a simpler planner
    /// for short, clear goals; LOCAL_POLICY fallback).
    /// </summary>
    public LatticePlanner? Planner { get; set; }

    /// <summary>Attaches the lattice planner from an OBB root's <c>cozmo_mprim.json</c>; false when the file is missing.</summary>
    public bool LoadPlanner(string obbRoot)
    {
        var prims = MotionPrimitiveSet.FromObb(obbRoot);
        if (prims is null) return false;
        Planner = new LatticePlanner(new LatticeEnvironment(prims));
        return true;
    }

    /// <summary>The block configurations (stacks, pyramid bases, pyramids) over the world model, rebuilt on every observation.</summary>
    public BlockConfigurationManager Configurations { get; }
    /// <summary>Beacons and object-failure memory shared by the behaviours.</summary>
    public AIWhiteboard Whiteboard { get; }
    /// <summary>The workout configs, when loaded from the OBB.</summary>
    public WorkoutComponent? Workouts { get; set; }

    public CozmoRobot Robot { get; }
    public VisionSystem Vision { get; }
    public BlockWorld World => Vision.World;
    public PathSender Paths { get; }
    public PathFollower Follower { get; }

    /// <summary>
    /// Starts a path and returns its owner: the terminal-event reservation is taken before the first message
    /// goes out, and an unfinished wait clears the path instead of leaving the firmware driving.
    /// </summary>
    public PathRun StartPath(IReadOnlyList<PathSegment> path)
    {
        PathFollower.Reservation? reservation = null;
        ushort id = Paths.Execute(path, newId => reservation = Follower.Reserve(newId));
        return new PathRun(this, id, reservation!);
    }
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
/// <summary>
/// A path this process started and owns until it ends: the reservation is taken before the path is sent, the
/// wait cannot miss an immediate terminal event, and a wait that is cancelled or times out clears the path on
/// the robot instead of leaving firmware motion running behind an abandoned action.
/// </summary>
public sealed class PathRun : IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly PathFollower.Reservation _reservation;
    private int _ended;

    internal PathRun(ManipulationSystem m, ushort pathId, PathFollower.Reservation reservation)
    { _m = m; PathId = pathId; _reservation = reservation; }

    public ushort PathId { get; }
    /// <summary>Whether the path was aborted because the wait was cancelled or timed out.</summary>
    public bool Aborted { get; private set; }

    /// <summary>
    /// Waits for the path's terminal event. Null means the wait ended without one (timeout or cancellation),
    /// and the path is cleared on the robot before returning.
    /// </summary>
    public async Task<PathEventType?> WaitAsync(TimeSpan timeout, CancellationToken cancel)
    {
        var ev = await _reservation.WaitAsync(timeout, cancel);
        if (ev is null) Abort();
        else Interlocked.Exchange(ref _ended, 1);
        return ev;
    }

    /// <summary>Clears the path on the robot. Safe to call more than once.</summary>
    public void Abort()
    {
        if (Interlocked.Exchange(ref _ended, 1) != 0) return;
        Aborted = true;
        _m.Paths.Abort();
    }

    public void Dispose() => _reservation.Dispose();
}

public sealed class DockHelper
{
    /// <summary>
    /// Two, which is what <c>PickupBlockHelper::RespondToPickupResult</c> allows: at 0x005B8192 it reads
    /// the attempt count at +0x108 and takes the retry branch only while it is <c>&lt;= 1</c>, and the
    /// log beside it is built with a literal 2 (<c>movs r6, #2</c> at 0x005B814A), so it reads
    /// "attempt 1 / 2" and "attempt 2 / 2". This stack had three.
    ///
    /// The roll helper does not hard-code its limit - <c>StartRollingAction</c> 0x005B9F62 compares the
    /// count against a value carried in its <c>RollBlockParameters</c> - so a roll or a charger dock may
    /// well differ. Those are recorded separately rather than assumed to be this.
    /// </summary>
    public const int MaxAttempts = 2;
    private readonly ManipulationSystem _m;

    public DockHelper(ManipulationSystem m) => _m = m;

    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();
    public int Attempts { get; private set; }
    /// <summary>The pre-action poses already tried and failed, excluded from the next attempt.</summary>
    public IReadOnlyList<Pose3d> ExcludedPoses => _excluded;
    private readonly List<Pose3d> _excluded = new();

    /// <summary>
    /// Drive to the object (for the action's pre-action type) and run the dock action, with retries.
    ///
    /// A retry approaches from a different pre-dock pose, as <c>IBehavior::UseSecondClosestPreActionPose</c>
    /// (0x005BEE40) does: it asks <c>DriveToObjectAction::GetPossiblePoses</c> again and, while more than one
    /// pose remains (<c>cmp r0, #2</c> at 0x005BEE80), removes the one just used through
    /// <c>IDockAction::RemoveMatchingPredockPose</c>. Retrying from the identical geometry that just failed is
    /// what the engine avoids; the pose to exclude is the one the failed attempt actually drove to.
    /// </summary>
    public async Task<ActionResult> RunAsync(uint objectId, PreActionType type, Func<DockActionBase> makeAction, CancellationToken cancel)
    {
        ActionResult last = ActionResult.Abort;
        _excluded.Clear();
        for (Attempts = 1; Attempts <= MaxAttempts; Attempts++)
        {
            if (cancel.IsCancellationRequested) return ActionResult.CancelledWhileRunning;
            var drive = new DriveToObjectAction(_m, objectId, type) { ExcludePoses = _excluded.ToList() };
            var d = await drive.RunAsync(cancel);
            _trace.AddRange(drive.Trace);
            if (drive.Chosen is { } tried && !_excluded.Any(p => DriveToObjectAction.IsSamePredockPose(p, tried.WorldPose)))
                _excluded.Add(tried.WorldPose);
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
