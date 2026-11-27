using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

internal sealed record SavedLayer(Guid Id, uint Origin, RoomLandmark[] Landmarks, RememberedHazard[] Hazards);
internal sealed record SavedJoin(Guid From, Guid To, RoomPose Transform, DateTimeOffset At);
internal sealed record SavedRoom(int Version, uint Serial, SavedLayer[] Layers, SavedJoin[] Joins, Guid Revision)
{
    private static bool Finite(RoomPose p) => double.IsFinite(p.XMm) && double.IsFinite(p.YMm) && double.IsFinite(p.HeadingRad);
    internal bool ValidFor(uint serial)
    {
        if (Version != 1 || Serial != serial || Revision == Guid.Empty || Layers is null || Joins is null) return false;
        var ids = new HashSet<Guid>();
        foreach (var layer in Layers)
        {
            if (layer is null || layer.Id == Guid.Empty || !ids.Add(layer.Id) || layer.Landmarks is null ||
                layer.Hazards is null || !layer.Landmarks.All(l => l is not null && Finite(l.Pose) &&
                (l.Type == ObjectType.Charger_Basic || CubeGeometry.IsCube(l.Type))) ||
                !layer.Hazards.All(h => h is not null && Enum.IsDefined(h.Type) && MemoryMapTypes.BlocksTheRobot(h.Type) &&
                    h.Polygon is { Length: >= 2 } && h.Polygon.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y)))) return false;
        }
        return Joins.All(j => j is not null && j.From != j.To && ids.Contains(j.From) && ids.Contains(j.To) && Finite(j.Transform));
    }

    internal SavedRoom Merge(SavedRoom other)
    {
        var layers = Layers.Concat(other.Layers).DistinctBy(l => l.Id).ToArray();
        var edges = Joins.Concat(other.Joins).OrderByDescending(j => j.At).DistinctBy(j => (j.From,j.To)).ToArray();
        return this with { Layers = layers, Joins = edges };
    }
}

internal sealed partial class C2RoomMap
{
    internal SavedRoom Capture(long epoch, uint serial)
    {
        lock (_gate)
        {
            var selected = _layers.Where(pair => pair.Key.Epoch == epoch).ToArray();
            var layers = new List<SavedLayer>();
            var joins = new List<SavedJoin>();
            foreach (var (origin, layer) in selected)
            {
                layers.Add(new(layer.PersistentId, origin.Id, layer.Objects.Values.ToArray(),
                    layer.Hazards.Where(h => DateTimeOffset.UtcNow - h.SeenAt <= HazardLifetime).ToArray()));
                foreach (var edge in layer.Joins.Values)
                {
                    var other = _layers[edge.To].PersistentId;
                    if (layer.PersistentId.CompareTo(other)<0) joins.Add(new(layer.PersistentId,other,edge.Transform,edge.At));
                }
            }
            return new(1,serial,layers.ToArray(),joins.ToArray(),Guid.NewGuid());
        }
    }

    internal void Restore(long epoch, SavedRoom file)
    {
        lock (_gate)
        {
            var keys = new Dictionary<Guid,RoomOrigin>();
            foreach (var stored in file.Layers)
            {
                var key = new RoomOrigin(epoch,stored.Origin,stored.Id);
                keys.Add(stored.Id,key);
                var layer = GetLayer(key);
                layer.PersistentId = stored.Id;
                foreach (var item in stored.Landmarks) layer.Objects[item.WorldObjectId] = item;
                layer.Hazards.Clear();
                layer.Hazards.AddRange(stored.Hazards.Where(h => DateTimeOffset.UtcNow-h.SeenAt <= HazardLifetime));
            }
            // A saved transform connects two saved maps only. A fresh live
            // landmark must join that component to the new robot origin.
            foreach (var edge in file.Joins.OrderByDescending(j => j.At))
            {
                var from = keys[edge.From]; var to = keys[edge.To];
                if (!PathsTo(from).ContainsKey(to)) Link(from,to,edge.Transform,edge.At);
            }
        }
    }
}
