using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// HIKING-BRING-CUBE-TO-BEACON-1 R1-R5/P1-P2: cached ObjectInfo values,
// not references to objects which may disappear between admission and Init.
internal sealed class NativeBringBeaconCandidates(ManipulationSystem manipulation, EngineRobot robot, float cooldown)
{
    private readonly List<(uint Id, ObjectFamily Family)> _entries = new();
    private static readonly ObjectActionFailure[] Failures = { (ObjectActionFailure)0, (ObjectActionFailure)3 };
    private static readonly float Epsilon = BitConverter.UInt32BitsToSingle(0x3727C5AC);
    private static readonly float NegativeEpsilon = BitConverter.UInt32BitsToSingle(0xB727C5AC);
    private static readonly float Angle = BitConverter.UInt32BitsToSingle(0x3EC90FDB);
    internal IReadOnlyList<(uint Id, ObjectFamily Family)> Entries => _entries;

    // fidelity: M15-020, M15-023, M15-024
    internal bool Refresh(Func<bool>? current = null)
    {
        current ??= () => true;
        if(!current())return false;
        _entries.Clear();
        var next = new List<(uint Id, ObjectFamily Family)>();
        using var mutation = manipulation.World.EnterPoseMutation();
        var whiteboard = manipulation.Whiteboard;
        var beacon = whiteboard.GetActiveBeacon();
        if (beacon is null) return false;
        float stamp = beacon.FailedToFindLocationTimeSec;
        if (!(MathF.Abs(stamp) < Epsilon) && (stamp + cooldown) + NegativeEpsilon > robot.Engine.Timer.SecondsF) return false;
        var carry = robot.CarryingOwner ?? throw new NativeConfigurationUnavailable("BringCubeToBeacon strict carrying owner unavailable");
        IEnumerable<ObservableObject> usable;
        if (carry.CarriedObjectId is { } carried)
        {
            var obj = manipulation.World.GetLocatedObjectByIdHelper(carried);
            if(!current())return false;
            usable = obj is null ? Array.Empty<ObservableObject>() : new[] { obj };
        }
        else
        {
            var cache = robot.NativeBehaviorRuntime?.PickupTargets ?? throw new NativeConfigurationUnavailable("BringCubeToBeacon native pickup predicate owner unavailable");
            var filter = new BlockWorldFilter();
            filter.AllowedFamilies.Add(ObjectFamily.Block);
            filter.AllowedFamilies.Add(ObjectFamily.LightCube);
            filter.Predicates.Add(obj => {
                if(!current())return false;bool can=cache.CanPick(obj);if(!current()||!can)return false;
                foreach(var b in whiteboard.Beacons){bool inside=b.IsLocWithinBeacon(obj.NativePose,0f);if(!current())return false;if(inside)return false;}return true; });
            usable = manipulation.World.FindLocatedMatchingObjects(filter);
            if(!current())return false;
        }
        foreach (var item in usable)
        {
            var obj = manipulation.World.GetLocatedObjectByIdHelper(item.ObjectId);
            if(!current())return false;
            if (obj is null || obj.Family != item.Family) continue;
            bool failed=whiteboard.DidFailToUse(unchecked((int)obj.ObjectId), Failures, cooldown, obj.NativePose, 20f, Angle);
            if(!current())return false;
            if(!failed)next.Add((obj.ObjectId, obj.Family));
        }
        if(!current())return false;
        _entries.AddRange(next);
        return _entries.Count != 0;
    }

    // R1 refreshes this vector; selection never reconstructs it in Init.
    internal uint Select(Func<bool>? current = null)
    {
        current??=()=>true;
        using var mutation = manipulation.World.EnterPoseMutation();
        float best = float.MaxValue;
        uint selected = uint.MaxValue;
        foreach (var entry in _entries.ToArray())
        {
            if(!current())return uint.MaxValue;
            float distance = float.MaxValue;
            var obj = manipulation.World.GetLocatedObjectByIdHelper(entry.Id);
            if(!current())return uint.MaxValue;
            if (obj is not null && obj.Family == entry.Family)
            {
                using var relative = new Pose3d();
                if (Pose3d.GetWithRespectTo(obj.NativePose, robot.GetCheckedMainPose(), relative))
                {
                    var p = relative.Translation;
                    float x = (float)p.X, y = (float)p.Y, z = (float)p.Z;
                    distance = (x * x + y * y) + z * z;
                }
            }
            if(!current())return uint.MaxValue;
            if (best + NegativeEpsilon > distance) { best = distance; selected = entry.Id; }
        }
        return best < float.MaxValue ? selected : uint.MaxValue;
    }
}
