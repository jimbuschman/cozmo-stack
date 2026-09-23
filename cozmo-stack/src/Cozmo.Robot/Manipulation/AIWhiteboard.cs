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
///
/// The beacons are a list, not a single slot: <c>AddBeacon</c> 0x0056C39C appends to a
/// <c>vector&lt;AIBeacon&gt;</c> at whiteboard+0x60, twenty bytes an entry, and <c>ClearAllBeacons</c>
/// 0x0056AA08 pops them all. <c>GetActiveBeacon</c> 0x0056C404 returns the <b>first</b> - it compares
/// begin against end and hands back begin, or null when they are equal - so the active beacon is the
/// oldest one still standing, not the newest.
/// </summary>
public sealed class AIWhiteboard
{
    private readonly BlockWorld _world;
    private readonly Func<double> _clockSec;
    private readonly List<AIBeacon> _beacons = new();
    private readonly List<(uint ObjectId, ObjectActionFailure Failure, double AtSec)> _failures = new();

    public AIWhiteboard(BlockWorld world, Func<double> clockSec) { _world = world; _clockSec = clockSec; }

    public IReadOnlyList<AIBeacon> Beacons => _beacons;
    /// <summary>The first beacon, as <c>GetActiveBeacon</c> 0x0056C404 returns begin rather than back.</summary>
    public AIBeacon? GetActiveBeacon() => _beacons.Count > 0 ? _beacons[0] : null;
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

    /// <summary>
    /// AIWhiteboard+0x70: the object <c>BehaviorKnockOverCubes</c> last failed to knock over for want of pre-action
    /// poses (written at 0x005C3DEA). What reads it was not traced.
    /// </summary>
    public uint? KnockOverNoPreActionPosesObjectId { get; set; }

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
