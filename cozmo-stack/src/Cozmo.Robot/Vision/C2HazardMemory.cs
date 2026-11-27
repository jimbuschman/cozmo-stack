namespace Cozmo.Robot.Vision;

internal sealed record RememberedHazard(MemoryMapContentType Type, Vec2[] Polygon, uint? ObjectId,
    uint Timestamp, DateTimeOffset SeenAt);
internal sealed record MappedHazard(RoomOrigin Origin, [property: System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<MemoryMapContentType>))] MemoryMapContentType Type, uint? ObjectId,
    Vec2[] LocalPolygon, Vec2[]? CurrentPolygon, long AgeMs);

internal sealed partial class C2RoomMap
{
    // C2 policy. SourceMapAging 0x006808F4 clears cliffs after 30 s and edges
    // after 120 s. Observable obstacles can instead disappear on object removal
    // (0x006810F2..0x00681172). Keep the original producer's footprint for 24 h.
    internal static readonly TimeSpan HazardLifetime = TimeSpan.FromHours(24);

    internal void RememberHazard(EngineRobot owner, uint origin, Vec2[] polygon, SourceMapData data)
    {
        if (!ReferenceEquals(owner.Engine.Robot, owner) || owner.Lifetime.Destroying || origin != owner.CurrentOriginId ||
            !MemoryMapTypes.BlocksTheRobot(data.Type) || polygon.Length < 2 ||
            polygon.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) return;
        uint? id = (data as ObservableMapData)?.ObjectId;
        var key = new RoomOrigin(owner.C2MapOwnerEpoch, origin);
        var now = DateTimeOffset.UtcNow;
        bool added;
        lock (_gate)
        {
            var records = GetLayer(key).Hazards;
            records.RemoveAll(h => now - h.SeenAt > HazardLifetime);
            // New observations of a movable object replace its old footprint.
            // For anonymous hazards, coalesce repeated observations of the same geometry.
            int index = records.FindIndex(h => h.Type == data.Type &&
                (id.HasValue ? id == h.ObjectId : !h.ObjectId.HasValue && SameFootprint(h.Polygon, polygon)));
            added = index < 0;
            var item = new RememberedHazard(data.Type, polygon.ToArray(), id, data.Last, now);
            if (added) records.Add(item);
            else if (unchecked((int)(data.Last - records[index].Timestamp)) > 0) records[index] = item;
        }
        if (added) owner.Engine.EmitStackLog($"{CozmoC2Settings.ObstacleMemoryName}: retained {data.Type} in origin {origin}");
    }

    private static bool SameFootprint(Vec2[] left, Vec2[] right) =>
        left.Length == right.Length && left.All(a => right.Any(b => (a-b).LengthSq <= 100)) &&
        right.All(b => left.Any(a => (a-b).LengthSq <= 100));

    internal MappedHazard[] Hazards(EngineRobot? owner)
    {
        if (owner is null || owner.Lifetime.Destroying) return [];
        var now = DateTimeOffset.UtcNow;
        var current = new RoomOrigin(owner.C2MapOwnerEpoch, owner.CurrentOriginId);
        var result = new List<MappedHazard>();
        lock (_gate)
        {
            var paths = PathsTo(current);
            foreach (var (source, layer) in _layers)
            {
                if (source.Epoch != current.Epoch) continue;
                layer.Hazards.RemoveAll(h => now - h.SeenAt > HazardLifetime);
                var anchored = paths.TryGetValue(source, out var path);
                foreach (var h in layer.Hazards)
                {
                    Vec2[]? polygon = anchored ? h.Polygon.Select(p =>
                    {
                        var position = path.Pose.Apply(new(p.X, p.Y, 0));
                        return new Vec2(position.XMm, position.YMm);
                    }).ToArray() : null;
                    result.Add(new(source, h.Type, h.ObjectId, h.Polygon.ToArray(), polygon,
                        Math.Max(0, (long)(now - h.SeenAt).TotalMilliseconds)));
                }
            }
        }
        return result.ToArray();
    }
}
