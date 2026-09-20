using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// <c>Anki::Cozmo::AIBeacon</c>: a circular area (pose and radius) the hiking/sparks behaviours gather cubes
/// into. <c>IsLocWithinBeacon</c> is a planar distance test; <c>FailedToFindLocation</c> records that no free
/// pose could be found inside it.
/// </summary>
public sealed class AIBeacon
{
    public AIBeacon(Pose3d pose, double radiusMm) { Pose = pose; RadiusMm = radiusMm; }
    public Pose3d Pose { get; }
    public double RadiusMm { get; }
    public int FailedToFindLocationCount { get; private set; }
    public bool IsLocWithinBeacon(Vec3 loc)
    {
        var d = loc - Pose.Translation;
        return Math.Sqrt(d.X * d.X + d.Y * d.Y) <= RadiusMm;
    }
    public void FailedToFindLocation() => FailedToFindLocationCount++;
}

/// <summary>What a behaviour failed to do with an object (<c>AIWhiteboard::ObjectActionFailure</c>).</summary>
public enum ObjectActionFailure { PickUpObject, StackOnObject, PlaceObjectAt, RollOrPopAWheelie, Any }

/// <summary>
/// The engine's <c>AIWhiteboard</c> (exports <c>AddBeacon</c>, <c>GetActiveBeacon</c>, <c>ClearAllBeacons</c>,
/// <c>FindCubesInBeacon</c>, <c>FindUsableCubesOutOfBeacons</c>, <c>AreAllCubesInBeacons</c>, <c>SetFailedToUse</c>,
/// <c>DidFailToUse</c>, <c>GetObjectFailureTable</c>, <c>OnRobotDelocalized</c>): shared scratch state between
/// behaviours. One beacon is active at a time (INFERRED from <c>GetActiveBeacon</c> being singular); failures
/// are stamped with the clock so <c>DidFailToUse(object, failure, withinSec)</c> can implement the behaviours'
/// recent-failure cooldowns. Possible objects (<c>ConsiderNewPossibleObject</c>) belong to the explorer and are
/// not modelled here.
/// </summary>
public sealed class AIWhiteboard
{
    private readonly BlockWorld _world;
    private readonly Func<double> _clockSec;
    private readonly List<AIBeacon> _beacons = new();
    private readonly List<(uint ObjectId, ObjectActionFailure Failure, double AtSec)> _failures = new();

    public AIWhiteboard(BlockWorld world, Func<double> clockSec) { _world = world; _clockSec = clockSec; }

    public IReadOnlyList<AIBeacon> Beacons => _beacons;
    public AIBeacon? GetActiveBeacon() => _beacons.Count > 0 ? _beacons[^1] : null;
    public AIBeacon AddBeacon(Pose3d pose, double radiusMm) { var b = new AIBeacon(pose, radiusMm); _beacons.Add(b); return b; }
    public void ClearAllBeacons() => _beacons.Clear();

    public IReadOnlyList<ObservableObject> FindCubesInBeacon(AIBeacon beacon) =>
        _world.LocatedObjects.Where(o => CubeGeometry.IsCube(o.Type) && beacon.IsLocWithinBeacon(o.Pose.Translation)).ToList();

    /// <summary>Located cubes outside every beacon that have not recently failed (<paramref name="failureCooldownSec"/>) for the given failure.</summary>
    public IReadOnlyList<ObservableObject> FindUsableCubesOutOfBeacons(ObjectActionFailure failure, double failureCooldownSec) =>
        _world.LocatedObjects.Where(o => CubeGeometry.IsCube(o.Type)
                                          && !_beacons.Any(b => b.IsLocWithinBeacon(o.Pose.Translation))
                                          && !DidFailToUse(o.ObjectId, failure, failureCooldownSec)).ToList();

    public bool AreAllCubesInBeacons() =>
        _world.LocatedObjects.Where(o => CubeGeometry.IsCube(o.Type)).All(o => _beacons.Any(b => b.IsLocWithinBeacon(o.Pose.Translation)));

    public void SetFailedToUse(uint objectId, ObjectActionFailure failure) => _failures.Add((objectId, failure, _clockSec()));

    public bool DidFailToUse(uint objectId, ObjectActionFailure failure, double withinSec)
    {
        double now = _clockSec();
        return _failures.Any(f => f.ObjectId == objectId && (failure == ObjectActionFailure.Any || f.Failure == failure) && now - f.AtSec <= withinSec);
    }

    public IReadOnlyList<(uint ObjectId, ObjectActionFailure Failure, double AtSec)> GetObjectFailureTable() => _failures;

    /// <summary><c>OnRobotDelocalized</c>: beacons live in the old origin, so they are dropped.</summary>
    public void OnRobotDelocalized() { _beacons.Clear(); _failures.Clear(); }
}
