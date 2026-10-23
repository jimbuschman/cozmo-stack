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
    /// <summary>
    /// <c>[beacon+0x10]</c>: an f32 time stamp, 0 from <c>AddBeacon</c> (<c>movs r2,#0; str r2,[r0,#0x10]</c> 0x0056C3BA..0x0056C3BC) until
    /// <c>AIBeacon::FailedToFindLocation</c> 0x0059C314 stores <c>BaseStationTimer::GetCurrentTimeInSeconds()</c> there (0x0059C318..0x0059C320).
    /// <c>IsRunnableInternal</c> 0x005DF148 reads it as a float. (M13-006 text says "an int"; the reads and the store say f32.)
    /// </summary>
    // fidelity: M15-023
    public float FailedToFindLocationTimeSec { get; private set; }
    public bool IsLocWithinBeacon(Vec3 loc)
    {
        var d = loc - Pose.Translation;
        return Math.Sqrt(d.X * d.X + d.Y * d.Y) <= RadiusMm;
    }
    /// <summary><c>AIBeacon::FailedToFindLocation()</c> 0x0059C314: the stamp, in f32 seconds.</summary>
    // fidelity: M15-023
    public void FailedToFindLocation(float nowSec) => FailedToFindLocationTimeSec = nowSec;

    /// <summary>binary32 0x3727C5AC (1.0e-5f), the literal <c>IsLocWithinBeacon</c> adds (<c>vldr s4, [pc]</c> 0x0059C2C2).</summary>
    private static readonly float WithinEpsilon = BitConverter.Int32BitsToSingle(unchecked((int)0x3727C5AC));

    // fidelity: M15-008
    /// <summary>
    /// <c>AIBeacon::IsLocWithinBeacon(pose, margin)</c> 0x0059C24C as <c>FindFreeCubeToStackOn</c> calls it (0x005E1D38): the pose with respect to the beacon pose
    /// (<c>GetWithRespectTo</c> 0x0059C288; a failure answers false, which cannot occur with one pose origin), its translation squared in three dimensions in binary32
    /// (<c>vmul.f32 x*x</c>, then <c>+ y*y</c> and <c>+ z*z</c>, 0x0059C2A0..0x0059C2B2), against <c>(radius - margin)^2 + 1.0e-5f</c> (<c>vsub</c>, <c>vmul</c>, <c>vadd</c>, 0x0059C2BE..0x0059C2CA):
    /// within when the right side is at least the left (<c>vcmpe.f32 s2, s0; it ge</c>, 0x0059C2CE..0x0059C2D8).
    /// </summary>
    public bool IsLocWithinBeacon(Pose3d pose, float margin)
    {
        var t = pose.WithRespectTo(Pose).Translation;
        float x = (float)t.X, y = (float)t.Y, z = (float)t.Z;
        float d2 = x * x;
        d2 += y * y;
        d2 += z * z;
        float r = (float)RadiusMm - margin;
        float limit = r * r;
        limit += WithinEpsilon;
        return limit >= d2;
    }
}

/// <summary>What a behaviour failed to do with an object (<c>AIWhiteboard::ObjectActionFailure</c>).</summary>
public enum ObjectActionFailure { PickUpObject, StackOnObject, PlaceObjectAt, RollOrPopAWheelie, Any }

/// <summary>
/// The robot's external interface as <c>AIWhiteboard::Init</c> sees it. The engine asks
/// <c>HasExternalInterface</c> (0x0056a39c) and, when there is one, registers three MessageEngineToGame
/// subscriptions (0x0056a3b8/0x0056a3be/0x0056a3c4). The handler bodies belong to M11/M12 and are not
/// modelled here; this is the subscription seam those layers implement.
/// </summary>
public interface IWhiteboardExternalInterface
{
    /// <summary>Subscribe the whiteboard's handler for one MessageEngineToGame tag.</summary>
    void SubscribeWhiteboardHandler(int tag, AIWhiteboard whiteboard);
}

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
// fidelity: M13-006
// fidelity: M8-014
// fidelity: M15-020, M15-023, M15-024
public sealed partial class AIWhiteboard
{
    private readonly BlockWorld _world;
    private readonly Func<double> _clockSec;
    private readonly List<AIBeacon> _beacons = new();
    // fidelity: M15-024
    // AIWhiteboard+0x14/+0x20/+0x2C/+0x38: one std::map<int objectId, std::list<FailureInfo>> per ObjectActionFailure 0..3 (GetObjectFailureTable 0x0056B8F4, tbb {2,38,41,44}).
    private readonly SortedDictionary<uint, List<FailureInfo>>[] _failureTables =
        { new(), new(), new(), new() };
    private readonly object _failureGate = new();

    public AIWhiteboard(BlockWorld world, Func<double> clockSec) { _world = world; _clockSec = clockSec; }

    // fidelity: M11-017 - native +0x4C/+0x50 stores, 0x006803E8/0x006803EC.
    internal (float Timestamp,float Distance)? OverheadEdgeState {get;private set;}
    internal void SetOverheadEdgeState(float timestamp,float distance)=>OverheadEdgeState=(timestamp,distance);

    public IReadOnlyList<AIBeacon> Beacons => _beacons;
    /// <summary>The first beacon, as <c>GetActiveBeacon</c> 0x0056C404 returns begin rather than back.</summary>
    public AIBeacon? GetActiveBeacon() => _beacons.Count > 0 ? _beacons[0] : null;

    /// <summary>
    /// <c>AIWhiteboard::Init</c> 0x0056a394: when the robot has an external interface it subscribes the
    /// three MessageEngineToGame handlers; otherwise it warns "Initialized whiteboard with no external
    /// interface. Will miss events." (0x0056a3d2). The handler bodies are M11/M12/unowned; this seam only
    /// makes the three subscriptions.
    /// </summary>
    public void Init()
    {
        if (ExternalInterface is { } external)
        {
            external.SubscribeWhiteboardHandler(TagRobotObservedObject, this);
            external.SubscribeWhiteboardHandler(TagRobotObservedPossibleObject, this);
            external.SubscribeWhiteboardHandler(TagRobotOffTreadsStateChanged, this);
        }
        else Warn("Initialized whiteboard with no external interface. Will miss events.");
    }

    /// <summary>MessageEngineToGame tag 0x44 = 68, <c>RobotObservedObject</c> (0x0056a44c).</summary>
    public const int TagRobotObservedObject = 0x44;
    /// <summary>MessageEngineToGame tag 0x45 = 69, <c>RobotObservedPossibleObject</c> (0x0056a50c).</summary>
    public const int TagRobotObservedPossibleObject = 0x45;
    /// <summary>MessageEngineToGame tag 0x35 = 53, <c>RobotOffTreadsStateChanged</c> (0x0056a5cc).</summary>
    public const int TagRobotOffTreadsStateChanged = 0x35;

    /// <summary>
    /// AIWhiteboard +0x48: the time the tag-53 handler recorded when the robot went off treads
    /// (<c>str r0,[r4,#0x48]</c> 0x0056ccf6). The handler body is M11/M12; this is where it writes.
    /// </summary>
    public double OffTreadsStateChangedAtSec { get; private set; }

    /// <summary>The tag-53 handler's write (BaseStationTimer::GetCurrentTimeInSeconds at 0x0056ccf2).</summary>
    public void RecordOffTreadsStateChanged(double nowSec) => OffTreadsStateChangedAtSec = nowSec;

    /// <summary><c>AIWhiteboard::Update</c> 0x0056a684 is a no-op (<c>bx lr</c>).</summary>
    public void Update() { }

    /// <summary>
    /// <c>AIWhiteboard::AddBeacon(pose, radius)</c> 0x0056c39c: append the beacon to the vector at +0x60
    /// (<c>str r0,[r4,#0x64]</c> 0x0056c3ce) and call <c>UpdateBeaconRender</c> (0x0056c3de).
    /// </summary>
    public AIBeacon AddBeacon(Pose3d pose, double radiusMm)
    {
        var b = new AIBeacon(pose, radiusMm);
        _beacons.Add(b);
        UpdateBeaconRender();
        return b;
    }

    /// <summary>
    /// <c>AIWhiteboard::UpdateBeaconRender</c> 0x0056aa3c..0x0056abff is a real 452-byte body that erases
    /// the previous segments through <c>VizManager::EraseSegments</c> and draws each beacon at +0x60 as
    /// three XY circles through <c>VizManager::DrawXYCircleAsSegments&lt;float&gt;</c>. <c>VizManager</c>
    /// is unowned by any record, so this raises the render seam rather than drawing.
    /// </summary>
    private void UpdateBeaconRender() => BeaconRenderUpdated?.Invoke();

    /// <summary>The VizManager render seam; <c>AddBeacon</c> raises it. VizManager is unowned.</summary>
    public event Action? BeaconRenderUpdated;

    /// <summary>The external interface <c>Init</c> registers handlers through, or null (the warning path).</summary>
    public IWhiteboardExternalInterface? ExternalInterface { get; set; }

    /// <summary>Warnings, for the no-external-interface path.</summary>
    public event Action<string>? Log;
    private void Warn(string message) => Log?.Invoke("warning: AIWhiteboard." + message);

    public void ClearAllBeacons() => _beacons.Clear();

    /// <summary>
    /// <c>CarryingComponent::IsCarryingObject(id)</c>, the first test of the <c>FindCubesInBeacon</c> predicate (0x0056D142). Wired by <see cref="ManipulationSystem"/>; null: nothing is carried.
    /// </summary>
    // fidelity: M15-020
    public Func<uint, bool>? IsCarryingObject { get; set; }

    /// <summary>
    /// <c>AIWhiteboard::FindCubesInBeacon(beacon, &amp;vec)</c> 0x0056B200..0x0056B3A2 (M15-020): the located objects of family Block (1) or LightCube (2) (the table at 0x00C57E18 = {2, 1},
    /// walked by <c>FindLocatedObjectHelper</c> in its map order: family, type, id) that the predicate 0x0056D12C accepts: not carried (<c>IsCarryingObject</c> 0x0056D142) and
    /// <c>AIBeacon::IsLocWithinBeacon(pose, 0.0f)</c> (0x0056D152..0x0056D156). The engine returns whether the vector is non-empty.
    /// </summary>
    // fidelity: M15-020
    public IReadOnlyList<ObservableObject> FindCubesInBeacon(AIBeacon beacon) =>
        _world.LocatedObjects.Where(o => o.Family is ObjectFamily.Block or ObjectFamily.LightCube)
            .OrderBy(o => (int)o.Family).ThenBy(o => (int)o.Type).ThenBy(o => o.ObjectId)
            .Where(o => !(IsCarryingObject?.Invoke(o.ObjectId) ?? false) && beacon.IsLocWithinBeacon(o.Pose, 0.0f))
            .ToList();

    /// <summary>
    /// <c>CarryingComponent::GetCarriedObjectId</c> as the whiteboard reads it ([[robot+0x284]+8], -1 = null here). Wired by <see cref="ManipulationSystem"/>; null: nothing is carried.
    /// </summary>
    // fidelity: M15-020
    public Func<uint?>? CarriedObjectId { get; set; }

    /// <summary><c>DockingComponent::CanPickUpObject(obj)</c> 0x0063C7F0, the second test of the <c>FindUsableCubesOutOfBeacons</c> predicate (0x0056D03A). Wired by <see cref="ManipulationSystem"/>.</summary>
    // fidelity: M15-020
    public Func<ObservableObject, bool>? CanPickUpObject { get; set; }

    /// <summary>
    /// <c>AIWhiteboard::FindUsableCubesOutOfBeacons(vec)</c> 0x0056AE58..0x0056B0AE (M15-020): the vector is cleared; with no beacon (begin == end of the vector at +0x60, 0x0056AE96) it stays empty; while a
    /// cube is carried ([[robot+0x284]+8] != -1, 0x0056AEA6) it holds just that carried object when <c>GetLocatedObjectByIdHelper(carried id)</c> finds it (0x0056AEB4..0x0056AEF2; not found: an error
    /// log and an empty vector); otherwise it is the located objects of family Block or LightCube (the table at 0x00C57E10 = {2, 1}, walked in the located map's order) that the predicate 0x0056D028 accepts:
    /// <c>DockingComponent::CanPickUpObject(obj)</c> == 1 and no beacon's <c>IsLocWithinBeacon(obj pose, 0.0f)</c> (0x0056D05A). The engine has NO failure memory in this function: the failure filter is
    /// the caller's (<c>BehaviorExploreBringCubeToBeacon::IsRunnableInternal</c> 0x005DF1BA..0x005DF250).
    /// </summary>
    // fidelity: M15-020
    public IReadOnlyList<ObservableObject> FindUsableCubesOutOfBeacons()
    {
        if (_beacons.Count == 0) return Array.Empty<ObservableObject>();
        if (CarriedObjectId?.Invoke() is { } carried)
            return _world.GetLocatedObjectById(carried) is { } carriedObject ? new[] { carriedObject } : Array.Empty<ObservableObject>();
        if (CanPickUpObject is null)
            Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("AIWhiteboard::FindUsableCubesOutOfBeacons predicate 0x0056D03A: DockingComponent::CanPickUpObject is not wired to this whiteboard (AIWhiteboard.CanPickUpObject), so no cube is usable");
        return _world.LocatedObjects.Where(o => o.Family is ObjectFamily.Block or ObjectFamily.LightCube)
            .OrderBy(o => (int)o.Family).ThenBy(o => (int)o.Type).ThenBy(o => o.ObjectId)
            .Where(o => (CanPickUpObject?.Invoke(o) ?? false) && !_beacons.Any(b => b.IsLocWithinBeacon(o.Pose, 0.0f)))
            .ToList();
    }

    /// <summary>
    /// <c>AIWhiteboard::AreAllCubesInBeacons()</c> 0x0056B450..0x0056B61A (M15-020): false with no beacon (0x0056B466) and false while a cube is carried (0x0056B476 <c>bne.w</c> to the return of 0); else the
    /// located objects of family Block or LightCube (table 0x00C57E10) are walked with the predicate 0x0056D20E, which counts those whose pose state byte (+0x24) is 1 (Known) and that lie within some beacon
    /// (<c>IsLocWithinBeacon(pose, 0.0f)</c>), and the answer is whether that count equals the 4-byte value the BlockWorld's map at +0x48 holds for family 2 (LightCube; 0 when there is no entry,
    /// 0x0056B5C4..0x0056B5F8). The map's identity is NOT read (MISSING): the stack compares with the number of located LightCubes, which is a stand-in.
    /// </summary>
    // fidelity: M15-020
    public bool AreAllCubesInBeacons()
    {
        if (_beacons.Count == 0) return false;
        if (CarriedObjectId?.Invoke() is not null) return false;
        int inBeacons = _world.LocatedObjects.Count(o => o.Family is ObjectFamily.Block or ObjectFamily.LightCube
                                                          && o.PoseState == PoseState.Known && _beacons.Any(b => b.IsLocWithinBeacon(o.Pose, 0.0f)));
        Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("AIWhiteboard::AreAllCubesInBeacons 0x0056B5C4..0x0056B5F8: the value of the BlockWorld map at +0x48 for family 2 that the count of cubes in beacons is compared with is unidentified (its writers are not read); the stack compares with the number of located LightCubes");
        int lightCubes = _world.LocatedObjects.Count(o => o.Family == ObjectFamily.LightCube);
        return inBeacons == lightCubes;
    }

    /// <summary>
    /// AIWhiteboard+0x70: an object an action could not reach for want of pre-dock poses, -1 (null here) when
    /// there is none. <c>BehaviorKnockOverCubes</c> writes it (0x005C3DEA); its one reader,
    /// <c>ReactionTriggerStrategyNoPreDockPoses::ShouldTriggerBehaviorInternal</c> 0x00610E32, resets it to -1
    /// and gives the object to RamIntoBlock (<see cref="Cozmo.Robot.Behavior.NoPreDockPosesStrategy"/>).
    /// </summary>
    public uint? NoPreDockPosesObjectId { get; set; }

    /// <summary>The per-failure cap of <c>SetFailedToUse</c>: the word table at 0x0056B8A4 = {1, 1, 10, 1}; a failure above 3 gives 0.</summary>
    // fidelity: M15-024
    public static int FailureCap(ObjectActionFailure failure) => (int)failure switch { 0 => 1, 1 => 1, 2 => 10, 3 => 1, _ => 0 };

    // fidelity: M15-024
    private SortedDictionary<uint, List<FailureInfo>>? TableOf(ObjectActionFailure failure) => (int)failure is >= 0 and <= 3 ? _failureTables[(int)failure] : null;

    /// <summary>
    /// <c>AIWhiteboard::SetFailedToUse(obj, failure)</c> 0x0056B6B0: the three-argument form with <c>obj.GetPose()</c> (0x0056B6B0..0x0056B6CA). The stack looks the object up by id; an id that is
    /// not located (the engine always has the object) records the identity pose.
    /// </summary>
    // fidelity: M15-024
    public void SetFailedToUse(uint objectId, ObjectActionFailure failure) =>
        SetFailedToUse(objectId, failure, _world.GetLocatedObjectById(objectId)?.Pose ?? Pose3d.Identity);

    /// <summary><c>SetFailedToUse(obj, failure)</c> with the object itself.</summary>
    // fidelity: M15-024
    public void SetFailedToUse(ObservableObject obj, ObjectActionFailure failure) => SetFailedToUse(obj.ObjectId, failure, obj.Pose);

    /// <summary><c>SetFailedToUse(obj, failure, pose)</c> with the object itself.</summary>
    // fidelity: M15-024
    public void SetFailedToUse(ObservableObject obj, ObjectActionFailure failure, Pose3d pose) => SetFailedToUse(obj.ObjectId, failure, pose);

    /// <summary>
    /// <c>AIWhiteboard::SetFailedToUse(obj, failure, pose)</c> 0x0056B6D0..0x0056B868: key = the ObjectID value; <c>list = table(failure)[key]</c> (created empty, 0x0056B702); when
    /// <c>list.size() &gt;= cap</c> (unsigned, 0x0056B71C) the front entry is removed (0x0056B7BE); then <c>emplace_back(pose, (float)BaseStationTimer::GetCurrentTimeInSeconds())</c>
    /// (0x0056B7C2..0x0056B7D2). The "Removed failure ..." and "Added failure ..." log lines (channel "AIWhiteboard", tag "SetFailedToUse") are not reproduced. A failure above 3 has a cap of
    /// 0 and no table: the engine's function-local static empty map receives the entry; nothing is kept here.
    /// </summary>
    // fidelity: M15-024
    public void SetFailedToUse(uint objectId, ObjectActionFailure failure, Pose3d pose)
    {
        var table = TableOf(failure);
        if (table is null) return;
        lock (_failureGate)
        {
            if (!table.TryGetValue(objectId, out var list)) table[objectId] = list = new List<FailureInfo>();
            if ((uint)list.Count >= (uint)FailureCap(failure)) list.RemoveAt(0);
            list.Add(new FailureInfo(pose.DeepCopy(), (float)_clockSec())); // Native emplace copies the Pose3d through R30/tree copy ctor.
        }
    }

    /// <summary>
    /// The shorter <c>DidFailToUse(id, failure(s), time)</c> overloads (0x0056BA04, 0x0056BB2C, 0x0056BBE0): the pose is not read (distance -1.0f = 0xBF800000, a default Radians). An entry
    /// matches while it is younger than <paramref name="withinSec"/> (strict, with the 1e-5f slack of <c>EntryMatches</c>). <see cref="ObjectActionFailure.Any"/> asks every table.
    /// </summary>
    // fidelity: M15-024
    public bool DidFailToUse(uint objectId, ObjectActionFailure failure, double withinSec) =>
        DidFailToUse(unchecked((int)objectId), failure, (float)withinSec, Pose3d.Identity, BitConverter.Int32BitsToSingle(unchecked((int)0xBF800000)), 0.0f);

    /// <summary>
    /// <c>AIWhiteboard::DidFailToUse(id, failure, time, pose, dist, radians)</c> 0x0056BC40 (a one-element set into the core 0x0056BAB4, which tries each failure's table with
    /// <c>FindMatchingEntry</c> 0x0056BF98 and stops at the first match). <paramref name="objectId"/> -1 walks every id's list.
    /// </summary>
    // fidelity: M15-024
    public bool DidFailToUse(int objectId, ObjectActionFailure failure, float timeSec, Pose3d pose, float distMm, float angleRad) =>
        DidFailToUse(objectId, failure == ObjectActionFailure.Any
            ? new[] { ObjectActionFailure.PickUpObject, ObjectActionFailure.StackOnObject, ObjectActionFailure.PlaceObjectAt, ObjectActionFailure.RollOrPopAWheelie }
            : new[] { failure }, timeSec, pose, distMm, angleRad);

    /// <summary>
    /// The set form <c>DidFailToUse(id, std::set&lt;ObjectActionFailure&gt;, time, pose, dist, radians)</c> 0x0056BAB4: each failure's table in the set's (ascending) order, the first match wins.
    /// <c>IsRunnableInternal</c> 0x005DF250 calls it with the set {0 PickUpObject, 3 RollOrPopAWheelie} (the first two words of the table at 0x00C6D6A0 = {0, 3, 1, 2}).
    /// </summary>
    // fidelity: M15-024
    public bool DidFailToUse(int objectId, IEnumerable<ObjectActionFailure> failures, float timeSec, Pose3d pose, float distMm, float angleRad)
    {
        foreach (var f in failures.Distinct().OrderBy(f => (int)f))
            if (FindMatchingEntry(TableOf(f), objectId, timeSec, pose, distMm, angleRad)) return true;
        return false;
    }

    // fidelity: M15-024
    private bool FindMatchingEntry(SortedDictionary<uint, List<FailureInfo>>? table, int objectId, float timeSec, Pose3d pose, float distMm, float angleRad)
    {
        if (table is null) return false;
        lock (_failureGate)
        {
            if (objectId == -1)
            {
                foreach (var list in table.Values)
                    foreach (var f in list)
                        if (EntryMatches(f, timeSec, pose, distMm, angleRad)) return true;
                return false;
            }
            if (!table.TryGetValue(unchecked((uint)objectId), out var l)) return false;
            foreach (var f in l) if (EntryMatches(f, timeSec, pose, distMm, angleRad)) return true;
            return false;
        }
    }

    /// <summary>-1.0e-5f = 0xB727C5AC (0x0056C0F8 literal): the floor of <c>time</c> and <c>dist</c> in <c>EntryMatches</c>.</summary>
    public static readonly float NegSlack = BitConverter.Int32BitsToSingle(unchecked((int)0xB727C5AC));

    /// <summary>
    /// <c>EntryMatches</c> 0x0056C070..0x0056C10A: (1) with <c>time &gt;= -1e-5f</c> the entry is too old when <c>time + (-1e-5f) &lt;= now - entry.time</c> (all f32; <c>bls</c>, so NaN is not too
    /// old); (2) <c>dist &lt; -1e-5f</c> (or NaN) matches without the pose; (3) else <c>Pose3d::IsSameAs(entry.pose, pose, Point3f(dist,dist,dist), radians)</c> (0x0056C0F2).
    /// </summary>
    // fidelity: M15-024
    private bool EntryMatches(FailureInfo f, float timeSec, Pose3d pose, float distMm, float angleRad)
    {
        if (timeSec >= NegSlack)
        {
            float now = (float)_clockSec();
            float limit = timeSec + NegSlack;
            float age = now - f.TimeSec;
            if (limit <= age) return false;
        }
        if (!(distMm >= NegSlack)) return true;
        return IsSameAs(f.Pose, pose, distMm, angleRad);
    }

    /// <summary>
    /// <c>Pose3d::IsSameAs(other, Point3f(d,d,d), Radians, &amp;diff, &amp;angleDiff)</c> 0x00846EA4 = <c>IsSameAs_WithAmbiguity</c> 0x00846F3C with the empty static <c>RotationAmbiguities</c>, for two poses
    /// that share the world-origin parent (the case here; the parent-chain re-expression at 0x00846F7A..0x00846FCE is then the identity, <c>tmp = other</c>): <c>v = other.t - this.t</c> per axis
    /// (<c>vsub.f32</c> 0x00847040); thresholds equal within 1e-5f (0x00847072..0x0084709E, literal 0x00847270 = 0x3727C5AC) -> a SPHERICAL test <c>x*x + y*y + z*z &gt; d*d</c> fails (f32, <c>ble</c> 0x008470D2,
    /// so NaN passes); then <c>radians &gt;= pi</c> (<c>operator&gt;=</c> 0x0084CCDC) accepts any rotation; else <c>this.rot.GetAngleDiffFrom(other.rot) &lt;= radians</c> (<c>operator&lt;=</c> 0x0084CD1C)
    /// accepts, and with no ambiguities the answer is false. The angle difference is taken from the rotation matrices (geodesic angle = acos((trace(A^T B) - 1) / 2)), where the engine takes it from its
    /// quaternions (<c>GetAngleDiffFrom</c> 0x0084A694: acos(2c^2 - 1) of the clamped dot product): the same quantity up to rounding.
    /// </summary>
    // fidelity: M15-024
    internal static bool IsSameAs(Pose3d entry, Pose3d query, float distMm, float angleRad)
    {
        return entry.IsSameAs(query,distMm,angleRad);
    }

    /// <summary>
    /// <c>AIWhiteboard::GetObjectFailureTable(failure)</c> 0x0056B8F4: the objects' failure lists of one failure kind; any other value gives the engine's static empty map.
    /// </summary>
    // fidelity: M15-024
    public IReadOnlyDictionary<uint, IReadOnlyList<FailureInfo>> GetObjectFailureTable(ObjectActionFailure failure)
    {
        var table = TableOf(failure);
        if (table is null) return new Dictionary<uint, IReadOnlyList<FailureInfo>>();
        lock (_failureGate) return table.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<FailureInfo>)kv.Value.ToList());
    }

    /// <summary>
    /// <c>AIWhiteboard::FailedToFindLocationInBeacon(beacon)</c> 0x0056C3F0..0x0056C400: <c>AIBeacon::FailedToFindLocation()</c> (the f32 time stamp), then <c>UpdateBeaconRender()</c>.
    /// </summary>
    // fidelity: M15-023
    public void FailedToFindLocationInBeacon(AIBeacon beacon)
    {
        beacon.FailedToFindLocation((float)_clockSec());
        UpdateBeaconRender();
        Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("AIBeacon [beacon+0x10] (the FailedToFindLocation stamp): only BehaviorExploreBringCubeToBeacon::IsRunnableInternal 0x005DF148 reads it here; other readers (e.g. BehaviorThinkAboutBeacons) were not scanned in the extraction, so none is built");
    }

    /// <summary><c>OnRobotDelocalized</c>: beacons live in the old origin, so they are dropped.</summary>
    public void OnRobotDelocalized() { _beacons.Clear(); lock (_failureGate) foreach (var t in _failureTables) t.Clear(); }
}


/// <summary><c>AIWhiteboard::FailureInfo</c> (the list node payload at +8): the <see cref="Pose3d"/> at +0 and the f32 time at +0xC (0x0056C098).</summary>
// fidelity: M15-024
public sealed record FailureInfo(Pose3d Pose, float TimeSec);

/// <summary>
/// The binary32 <c>Radians</c> operations <c>IsSameAs_WithAmbiguity</c> uses (M15-024): <c>Radians::rescale</c> 0x0084C87C (constants 0x0084C938..0x0084C944 = -pi, pi, 2pi, -2pi as
/// 0xC0490FDB, 0x40490FDB, 0x40C90FDB, 0xC0C90FDB), <c>IsNear</c> 0x0084CC0A (rescale this, subtract, rescale, strict <c>|d| &lt; |tol|</c>), <c>operator&gt;</c> 0x0084CC90 (<c>a - b &gt; 0</c> and not
/// near within 1e-5f = 0x3727C5AC), <c>operator&gt;=</c> 0x0084CCDC (<c>a &gt; b</c> or near), <c>operator&lt;=</c> 0x0084CD1C (the arguments swapped into <c>&gt;=</c>).
/// </summary>
// fidelity: M15-024
public static class EngineRadians32
{
    // API facade; the source arithmetic is owned by the shared pose port.
    public static float Rescale(float v)=>Radians.Rescale(v);
    public static bool IsNear(float a,float b,float tol)=>Radians.IsNear(a,b,tol);
    public static bool Gt(float a,float b)=>Radians.Greater(a,b);
    public static bool Ge(float a,float b)=>Radians.GreaterEqual(a,b);
    public static bool Le(float a,float b)=>Radians.LessEqual(a,b);
    public static float AngleDiff(Mat3 a,Mat3 b)=>Rotation3d.FromMatrix(a).AngleDiff(Rotation3d.FromMatrix(b));
}
