using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

internal readonly record struct RoomOrigin(long Epoch, uint Id, Guid ArchiveId = default);
internal readonly record struct RoomPose(double XMm, double YMm, double HeadingRad)
{
    internal RoomPose Apply(RoomPose local)
    {
        double c = Math.Cos(HeadingRad), s = Math.Sin(HeadingRad);
        return new(XMm + c * local.XMm - s * local.YMm, YMm + s * local.XMm + c * local.YMm,
            Math.Atan2(Math.Sin(HeadingRad + local.HeadingRad), Math.Cos(HeadingRad + local.HeadingRad)));
    }

    internal RoomPose Inverse()
    {
        var rotation = new RoomPose(0, 0, -HeadingRad);
        return rotation.Apply(new(-XMm, -YMm, 0));
    }
}

internal sealed record RoomLandmark(uint WorldObjectId, ObjectType Type, uint FactoryId, RoomPose Pose, uint Timestamp);
internal sealed record RoomLayer(RoomOrigin Origin, bool Anchored, RoomPose? ToCurrent, DateTimeOffset? RejoinedAt,
    RoomLandmark[] Landmarks);

// Our C2 overlay records accepted observations at 0x00621146..0x006211DC.
// Never give an archived object a native action ID in the new origin.
internal sealed partial class C2RoomMap
{
    private sealed record Join(RoomOrigin To, RoomPose Transform, DateTimeOffset At);
    private sealed class Layer
    {
        internal Guid PersistentId { get; set; } = Guid.NewGuid();
        internal List<RememberedHazard> Hazards { get; } = new();
        internal Dictionary<uint, RoomLandmark> Objects { get; } = new();
        internal Dictionary<RoomOrigin, Join> Joins { get; } = new();
    }

    private readonly Dictionary<RoomOrigin, Layer> _layers = new();
    private readonly object _gate = new();
    private Layer GetLayer(RoomOrigin key)
    {
        if (!_layers.TryGetValue(key, out var layer)) _layers[key] = layer = new();
        return layer;
    }

    private static bool SameAnchor(RoomLandmark old, RoomLandmark sighting, bool sameOwner) =>
        old.Type == sighting.Type && (old.Type == ObjectType.Charger_Basic ||
        (old.FactoryId != 0 && sighting.FactoryId != 0 ? old.FactoryId == sighting.FactoryId :
            sameOwner && old.WorldObjectId == sighting.WorldObjectId));

    internal void Observe(EngineRobot owner, ObservableObject observed, ObservableObject located)
    {
        if (owner.Lifetime.Destroying || !ReferenceEquals(owner.Engine.Robot, owner) ||
            observed.OriginId != owner.CurrentOriginId || observed.LastObservedTimestamp == 0 ||
            !(CubeGeometry.IsCube(observed.Type) || observed.Type == ObjectType.Charger_Basic)) return;
        using var relative = new Pose3d();
        if (!Pose3d.GetWithRespectTo(observed.NativePose, owner.GetNativeOrigin(owner.CurrentOriginId), relative)) return;
        var position = relative.Translation;
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y) || !double.IsFinite(relative.AngleAroundZ)) return;
        var current = new RoomOrigin(owner.C2MapOwnerEpoch, owner.CurrentOriginId);
        var sighting = new RoomLandmark(located.ObjectId, observed.Type, located.FactoryId,
            new(position.X, position.Y, relative.AngleAroundZ), observed.LastObservedTimestamp);
        List<RoomOrigin> joined = new();
        lock (_gate)
        {
            var layer = GetLayer(current);
            if (layer.Objects.TryGetValue(located.ObjectId, out var previous) &&
                unchecked((int)(sighting.Timestamp - previous.Timestamp)) <= 0) return;
            layer.Objects[located.ObjectId] = sighting;
            var connected = PathsTo(current);
            foreach (var (origin, other) in _layers)
            {
                if (origin.Epoch != current.Epoch || connected.ContainsKey(origin)) continue;
                var match = other.Objects.Values.FirstOrDefault(item => SameAnchor(item, sighting, origin.ArchiveId == Guid.Empty && origin.Epoch == current.Epoch));
                if (match is null) continue;
                Link(origin, current, sighting.Pose.Apply(match.Pose.Inverse()));
                joined.Add(origin);
                connected = PathsTo(current);
            }
        }
        foreach (var old in joined)
            owner.Engine.EmitStackLog($"{CozmoC2Settings.MapReanchorName}: rejoined {old} to {current} using {sighting.Type} {sighting.WorldObjectId}");
    }

    // Native LocalizeToObject joins the pose roots before flattening them
    // (0x00515992..0x005159B2). Its measured transform takes precedence.
    internal void NativeJoin(EngineRobot owner, uint from, uint to, Pose3d transform)
    {
        if (from == to) return;
        var a = new RoomOrigin(owner.C2MapOwnerEpoch, from);
        var b = new RoomOrigin(owner.C2MapOwnerEpoch, to);
        lock (_gate)
        {
            if (!_layers.ContainsKey(a)) return;
            // Keep a single path between frames. A native measurement supersedes
            // the inferred path, including when it traversed an intermediate frame.
            var prior = PathsTo(b);
            if (prior.TryGetValue(a, out var old) && old.Parent is { } parent)
            {
                _layers[a].Joins.Remove(parent);
                _layers[parent].Joins.Remove(a);
            }
            Link(a, b, new(transform.Translation.X, transform.Translation.Y, transform.AngleAroundZ));
        }
        owner.Engine.EmitStackLog($"{CozmoC2Settings.MapReanchorName}: native origin join {a} to {b}");
    }

    private void Link(RoomOrigin from, RoomOrigin to, RoomPose transform, DateTimeOffset? joinedAt = null)
    {
        var at = joinedAt ?? DateTimeOffset.UtcNow;
        GetLayer(from).Joins[to] = new(to, transform, at);
        GetLayer(to).Joins[from] = new(from, transform.Inverse(), at);
    }

    private Dictionary<RoomOrigin, (RoomPose Pose, DateTimeOffset? At, RoomOrigin? Parent)> PathsTo(RoomOrigin current)
    {
        var result = new Dictionary<RoomOrigin, (RoomPose Pose, DateTimeOffset? At, RoomOrigin? Parent)> { [current] = (default, null, null) };
        var queue = new Queue<RoomOrigin>();
        queue.Enqueue(current);
        while (queue.TryDequeue(out var key))
        {
            if (!_layers.TryGetValue(key, out var layer)) continue;
            foreach (var edge in layer.Joins.Values)
            {
                var parent = result[key];
                var at = parent.At > edge.At ? parent.At.Value : edge.At;
                if (result.TryAdd(edge.To, (parent.Pose.Apply(edge.Transform.Inverse()), at, key))) queue.Enqueue(edge.To);
            }
        }
        return result;
    }

    internal RoomLayer[] Snapshot(EngineRobot? owner)
    {
        lock (_gate)
        {
            if (owner is null) return [];
            var paths = PathsTo(new(owner.C2MapOwnerEpoch, owner.CurrentOriginId));
            return _layers.Where(pair => pair.Key.Epoch == owner.C2MapOwnerEpoch).Select(pair =>
            {
                bool anchored = paths.TryGetValue(pair.Key, out var path);
                return new RoomLayer(pair.Key, anchored, anchored ? path.Pose : null, path.At,
                    pair.Value.Objects.Values.ToArray());
            }).ToArray();
        }
    }
}
