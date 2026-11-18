using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    private sealed record FeedEvent(long Cursor, string Type, DateTimeOffset TimestampUtc, uint? RobotTimestamp, JsonElement Payload);

    private CozmoRobot Robot { get; }
    private readonly int eventCapacity;
    private readonly object eventGate = new();
    private readonly Queue<FeedEvent> events = new();
    private long lastCursor;
    private bool disposed;

    /// <summary>Subscribes to public reports. The caller retains ownership of the robot and connection.</summary>
    public CozmoBridge(CozmoRobot robot, int eventCapacity = 1024)
    {
        Robot = robot ?? throw new ArgumentNullException(nameof(robot));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventCapacity);
        this.eventCapacity = eventCapacity;
        var engine = Robot.Engine;
        engine.RobotObservedFace += OnFace;
        engine.RobotChangedObservedFaceId += OnFaceIdChanged;
        engine.RobotDeletedFace += OnFaceLost;
        engine.RobotObservedObject += OnObject;
        Robot.Cubes.MovementBroadcast += OnCubeMoved;
        Robot.Cubes.TapBroadcast += OnCubeTapped;
        engine.ObjectPoseUnknown += OnObjectPoseLost;
        engine.BehaviorTransition += OnBehavior;
        engine.ReactionTransition += OnReaction;
        engine.ActionCompleted += OnActionCompleted;
    }

    /// <summary>Reads events strictly after a cursor. Zero is the initial cursor. Gap indicates older events were evicted.</summary>
    public JsonElement ReadEvents(long since = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(since);
        FeedEvent[] page;
        long next;
        long oldest;
        lock (eventGate)
        {
            ThrowIfDisposed();
            if (since > lastCursor) throw new ArgumentOutOfRangeException(nameof(since), "Cursor belongs to a different or newer event feed.");
            page = events.Where(e => e.Cursor > since).ToArray();
            next = lastCursor;
            oldest = events.TryPeek(out var first) ? first.Cursor : 1;
        }
        return JsonSerializer.SerializeToElement(new { cursor = next, oldestCursor = oldest, gap = since < oldest - 1, events = page }, Json);
    }

    /// <summary>Returns the latest connection, device, activity, face and cube state exposed by the robot.</summary>
    public JsonElement GetState()
    {
        ThrowIfDisposed();
        bool connected = Robot.Engine.ConnectionState == 2;
        var raw = connected ? Robot.State.Latest : null;
        uint? carried = connected ? Robot.Carrying.CarriedObjectId : null;
        var activity = connected ? Robot.GetActivityObservation() : null;
        var world = Robot.Vision.World;
        var faces = connected
            ? Robot.Vision.Faces.Faces.Select(f => new KnownFace(f.Id, f.Name, f.LastObservedTimestamp)).ToDictionary(f => f.FaceId)
            : new Dictionary<int, KnownFace>();
        if (connected)
        {
            foreach (var face in Robot.Vision.Recognizer.EnrolledFaces.Where(f => !string.IsNullOrEmpty(f.Name)))
                faces[face.FaceID] = new(face.FaceID, face.Name, faces.GetValueOrDefault(face.FaceID)?.LastObservedTimestamp, true);
        }
        var cubes = new List<KnownCube>();
        if (connected)
        {
            uint origin = world.CurrentOriginId;
            var located = world.LocatedObjects.Where(o => o.OriginId == origin && CubeGeometry.IsCube(o.Type))
                .ToDictionary(o => o.ObjectId);
            foreach (var cube in Robot.Cubes.DiscoveredCubes.OrderBy(c => c.FactoryId))
            {
                uint? worldId = cube.ObjectId is { } slot && cube.Connected ? world.ConnectedObjectIdForActiveId(slot) : null;
                ObservableObject? observed = null;
                if (worldId is { } id) located.Remove(id, out observed);
                cubes.Add(new(cube.FactoryId, cube.ObjectId, worldId, cube.Type.ToString(), cube.Connected,
                    observed?.PoseState.ToString(), observed?.LastObservedTimestamp));
            }
            foreach (var cube in located.Values.OrderBy(o => o.ObjectId))
                cubes.Add(new(cube.FactoryId == 0 ? null : cube.FactoryId, cube.ActiveId < 0 ? null : (uint)cube.ActiveId,
                    cube.ObjectId, cube.Type.ToString(), false, cube.PoseState.ToString(), cube.LastObservedTimestamp));
        }

        return JsonSerializer.SerializeToElement(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            connection = new { connected, ready = connected && Robot.Engine.IsReadyDelivered, link = Robot.Transport.State.ToString() },
            batteryVolts = raw?.BatteryVoltage,
            isCarrying = connected ? carried.HasValue : (bool?)null,
            carriedObjectId = carried,
            isOnCharger = raw?.Has(RobotStatusFlag.IsOnCharger),
            activity = activity?.Activity,
            highLevelActivity = activity?.HighLevelActivity,
            behavior = activity?.Behavior,
            reaction = activity?.Reaction,
            needs = Robot.Engine.GetNeedsStatus(),
            knownFaces = faces.Values.OrderBy(f => f.FaceId),
            knownCubes = cubes,
        }, Json);
    }

    private sealed record KnownFace(int FaceId, string? Name, uint? LastObservedTimestamp, bool Enrolled = false);
    private sealed record KnownCube(uint? FactoryId, uint? RadioObjectId, uint? WorldObjectId, string Type, bool Connected,
        string? PoseState, uint? LastObservedTimestamp);

    /// <summary>Authored animation triggers and clip names loaded by the current robot.</summary>
    public JsonElement ListAnimations()
    {
        ThrowIfDisposed();
        var library = Robot.Animations.Library;
        return JsonSerializer.SerializeToElement(new
        {
            loaded = library is not null,
            triggers = Enum.GetValues<Cozmo.Robot.Behavior.AnimationTrigger>()
                .Where(trigger => library?.HasAnimationForTrigger(trigger) == true).Select(trigger => trigger.ToString()),
            clips = library?.ClipNames ?? Array.Empty<string>(),
        }, Json);
    }

    private void OnFace(RobotObservedFaceReport report)
    {
        var payload = JsonSerializer.SerializeToElement(report, Json);
        lock (eventGate)
        {
            Append("face-seen", report.Timestamp, payload);
            if (!string.IsNullOrEmpty(report.Name)) Append("face-recognised", report.Timestamp, payload);
        }
    }
    private void OnFaceIdChanged(RobotChangedObservedFaceIdReport report) => Add("face-id-changed", report);
    private void OnFaceLost(RobotDeletedFaceReport report) => Add("face-lost", report);

    private void OnObject(RobotObservedObjectReport report)
    {
        if (report.Family == (uint)ObjectFamily.LightCube) Add("cube-seen", report, report.Timestamp);
        else if (report.Type == (uint)ObjectType.Charger_Basic) Add("charger-seen", report, report.Timestamp);
    }

    private void OnCubeMoved(CubeTimestampEvent report) => AddCubeEvent("cube-moved", report);
    private void OnCubeTapped(CubeTimestampEvent report) => AddCubeEvent("cube-tapped", report);

    private void AddCubeEvent(string type, CubeTimestampEvent report)
    {
        var cube = report.Cube;
        Add(type, new
        {
            report.Timestamp,
            Cube = new
            {
                cube.FactoryId, cube.ObjectId, cube.WorldObjectId, cube.Type, cube.Connected,
                cube.Moving, cube.Accel,
                LastTap = cube.LastTap is { } tap
                    ? new { tap.Timestamp, ObjectId = tap.ObjectID, tap.NumTaps, tap.TapTime, tap.TapNeg, tap.TapPos }
                    : null,
            },
        }, report.Timestamp);
    }
    private void OnObjectPoseLost(ObjectPoseUnknownReport report) => Add("object-pose-lost", report);
    private void OnBehavior(BehaviorTransitionReport report) => Add("behavior-changed", report);
    private void OnReaction(ReactionTransitionReport report) => Add("reaction-changed", report);
    private void OnActionCompleted(ActionCompletionReport report) =>
        Add("action-completed", new { completion = report, resultName = Enum.GetName((NativeActionResultName)report.Result) });

    private void Add<T>(string type, T payload, uint? robotTimestamp = null)
    {
        var json = JsonSerializer.SerializeToElement(payload, Json);
        lock (eventGate) Append(type, robotTimestamp, json);
    }

    private void Append(string type, uint? robotTimestamp, JsonElement payload)
    {
        if (disposed) return;
        if (events.Count == eventCapacity) events.Dequeue();
        events.Enqueue(new(++lastCursor, type, DateTimeOffset.UtcNow, robotTimestamp, payload));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed), this);

    public void Dispose()
    {
        lock (eventGate)
        {
            if (disposed) return;
            Volatile.Write(ref disposed, true);
            events.Clear();
        }

        var engine = Robot.Engine;
        engine.RobotObservedFace -= OnFace;
        engine.RobotChangedObservedFaceId -= OnFaceIdChanged;
        engine.RobotDeletedFace -= OnFaceLost;
        engine.RobotObservedObject -= OnObject;
        Robot.Cubes.MovementBroadcast -= OnCubeMoved;
        Robot.Cubes.TapBroadcast -= OnCubeTapped;
        engine.ObjectPoseUnknown -= OnObjectPoseLost;
        engine.BehaviorTransition -= OnBehavior;
        engine.ReactionTransition -= OnReaction;
        engine.ActionCompleted -= OnActionCompleted;
    }
}
