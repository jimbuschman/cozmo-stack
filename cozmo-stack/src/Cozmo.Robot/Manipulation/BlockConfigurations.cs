using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary><c>Anki::Cozmo::BlockConfigurations::ConfigurationType</c>: the arrangements the engine recognises (names from <c>BlockConfigurationFromString</c>).</summary>
public enum BlockConfigurationType { StackOfCubes, PyramidBase, Pyramid }

/// <summary>A recognised arrangement of cubes (<c>BlockConfigurations::BlockConfiguration</c>).</summary>
public abstract record BlockConfiguration(BlockConfigurationType Type, IReadOnlyList<uint> BlockIds)
{
    /// <summary><c>BlockConfiguration::ContainsBlock</c>.</summary>
    public bool ContainsBlock(uint id) => BlockIds.Contains(id);
}

/// <summary>
/// <c>BlockConfigurations::StackOfCubes</c> (<c>blockConfigurationStack.cpp</c>): cubes resting on one another,
/// bottom first. <c>GetStackHeight</c> is the count.
/// </summary>
public sealed record StackOfCubes(IReadOnlyList<uint> BlockIds) : BlockConfiguration(BlockConfigurationType.StackOfCubes, BlockIds)
{
    public uint BottomBlockId => BlockIds[0];
    public uint TopBlockId => BlockIds[^1];
    public int StackHeight => BlockIds.Count;
}

/// <summary><c>BlockConfigurations::PyramidBase</c>: two cubes side by side on the ground.</summary>
public sealed record PyramidBase(uint LeftBlockId, uint RightBlockId, Vec3 InteriorMidpoint) : BlockConfiguration(BlockConfigurationType.PyramidBase, new[] { LeftBlockId, RightBlockId });

/// <summary><c>BlockConfigurations::Pyramid</c>: a base with a third cube on top of its midpoint.</summary>
public sealed record Pyramid(PyramidBase Base, uint TopBlockId) : BlockConfiguration(BlockConfigurationType.Pyramid, new[] { Base.LeftBlockId, Base.RightBlockId, TopBlockId });

/// <summary>
/// The engine's <c>BlockConfigurationManager</c> over the world model: on each update it rebuilds the stacks
/// (<c>StackOfCubes::BuildTallestStackForObject</c> walking <c>BlockWorld::FindObjectOnTopOrUnderneath</c>, loop
/// bound 8), the pyramid bases (<c>PyramidBase::BlocksFormPyramidBase</c>) and the pyramids
/// (<c>Pyramid::BuildAllPyramidsForBlock</c> with <c>PyramidBase::ObjectIsOnTopOfBase</c>), keeps a cache per
/// type (<c>GetCacheByType</c>), and reports when a configuration is first seen (<c>ConfigurationSeen</c>,
/// stamped with the clock for the behaviours' "seen within 5 s" checks). Rules, NATIVE where the constant was
/// read: a block is on top of another when their planar centres are within half a cube and the upper sits one
/// cube height (44) above. The vertical tolerance is <c>FindObjectOnTopOrUnderneathHelper</c>'s second
/// argument, and it is not one number: <c>BuildTallestStackForObject</c> passes <b>30</b> (0x41F00000 at
/// 0x0061928C and 0x00619344) while <c>UpdatePoseOfStackedObjects</c>, <c>CanInteractWithObjectHelper</c>
/// and <c>SetObjectAsAttachedToLift</c> all pass 15 (0x41700000). Building a stack is the loose one.
/// Two blocks form
/// a base when both are on the ground at the same height (within 10, 0x41200000) and their centres are at
/// most 60 mm apart (3600 mm², 0x45610000) with both upright; a block is on top of a base when it is one
/// cube up and within 15 mm (0x41700000) of the base's interior midpoint.
/// </summary>
public sealed class BlockConfigurationManager
{
    /// <summary>
    /// 30 mm: what <c>StackOfCubes::BuildTallestStackForObject</c> hands
    /// <c>FindObjectOnTopOrUnderneathHelper</c> (0x41F00000 at 0x0061928C and 0x00619344). This had been
    /// 15, which is the tolerance the engine's <em>other</em> three callers of that helper use.
    /// </summary>
    public const double OnTopPlanarToleranceMm = 30.0;

    /// <summary>
    /// 15 mm: the tolerance <c>UpdatePoseOfStackedObjects</c> 0x00621908,
    /// <c>CanInteractWithObjectHelper</c> 0x0063C728 and <c>SetObjectAsAttachedToLift</c> 0x00632F0C
    /// pass to the same helper, for deciding whether a cube is resting on another rather than for
    /// building the stack list.
    /// </summary>
    public const double RestingOnToleranceMm = 15.0;
    public const double SameHeightToleranceMm = 10.0;
    public const double PyramidBaseMaxDistanceMm2 = 3600.0;
    public const double OnTopOfBaseToleranceMm = 15.0;
    public const int MaxStackHeight = 8;

    private readonly BlockWorld _world;
    private readonly Func<double> _clockSec;
    /// <summary>
    /// One consistent view of every configuration. <see cref="Update"/> runs on the vision worker (it is
    /// driven from <c>BlockWorld.ObjectObserved</c> / <c>PoseStateChanged</c>) while behaviours and the
    /// freeplay loop read from their own threads, so a new view is built off to the side and swapped in whole
    /// rather than mutated in place.
    /// </summary>
    private sealed record Snapshot(IReadOnlyDictionary<BlockConfigurationType, IReadOnlyList<BlockConfiguration>> Cache,
                                   IReadOnlyDictionary<string, double> FirstSeenSec)
    {
        public static readonly Snapshot Empty =
            new(new Dictionary<BlockConfigurationType, IReadOnlyList<BlockConfiguration>>(), new Dictionary<string, double>());
    }

    private Snapshot _snapshot = Snapshot.Empty;
    private readonly object _updateGate = new();

    public BlockConfigurationManager(BlockWorld world, Func<double> clockSec) { _world = world; _clockSec = clockSec; }

    public event Action<BlockConfiguration, double>? ConfigurationSeen;
    public IReadOnlyList<StackOfCubes> Stacks => Cache(BlockConfigurationType.StackOfCubes).Cast<StackOfCubes>().ToList();
    public IReadOnlyList<PyramidBase> PyramidBases => Cache(BlockConfigurationType.PyramidBase).Cast<PyramidBase>().ToList();
    public IReadOnlyList<Pyramid> Pyramids => Cache(BlockConfigurationType.Pyramid).Cast<Pyramid>().ToList();

    /// <summary><c>GetCacheByType</c>.</summary>
    public IReadOnlyList<BlockConfiguration> Cache(BlockConfigurationType t) =>
        Volatile.Read(ref _snapshot).Cache.TryGetValue(t, out var l) ? l : Array.Empty<BlockConfiguration>();

    /// <summary>When a configuration with these blocks was first seen (seconds on the manager's clock), or null.</summary>
    public double? FirstSeenSec(BlockConfiguration c) =>
        Volatile.Read(ref _snapshot).FirstSeenSec.TryGetValue(Key(c), out var t) ? t : (double?)null;

    /// <summary><c>IsObjectPartOfConfigurationType</c>.</summary>
    public bool IsObjectPartOfConfigurationType(uint objectId, BlockConfigurationType t) => Cache(t).Any(c => c.ContainsBlock(objectId));

    /// <summary><c>StackConfigurationContainer::GetTallestStack</c>.</summary>
    public StackOfCubes? GetTallestStack() => Stacks.OrderByDescending(s => s.StackHeight).FirstOrDefault();

    /// <summary>
    /// Rebuilds every configuration from the located cubes and publishes the result as one snapshot, so a
    /// reader on another thread never sees stacks from one pass beside pyramids from the next.
    /// </summary>
    public void Update()
    {
        List<BlockConfiguration> newlySeen;
        double now;
        lock (_updateGate) (newlySeen, now) = BuildLocked();
        // the event goes out after the swap, so a handler that reads the manager sees the new view
        foreach (var c in newlySeen) ConfigurationSeen?.Invoke(c, now);
    }

    private (List<BlockConfiguration> NewlySeen, double Now) BuildLocked()
    {
        var cubes = _world.LocatedObjects.Where(o => CubeGeometry.IsCube(o.Type)).ToList();
        var stacks = new List<BlockConfiguration>();
        var seenBottoms = new HashSet<uint>();
        foreach (var c in cubes)
        {
            var s = BuildTallestStackForObject(c, cubes);
            if (s.StackHeight >= 2 && seenBottoms.Add(s.BottomBlockId)) stacks.Add(s);
        }
        var bases = new List<BlockConfiguration>();
        for (int i = 0; i < cubes.Count; i++)
            for (int j = i + 1; j < cubes.Count; j++)
                if (BlocksFormPyramidBase(cubes[i], cubes[j], out var mid)) bases.Add(new PyramidBase(cubes[i].ObjectId, cubes[j].ObjectId, mid));
        var pyramids = new List<BlockConfiguration>();
        foreach (PyramidBase b in bases)
            foreach (var top in cubes)
                if (!b.ContainsBlock(top.ObjectId) && ObjectIsOnTopOfBase(b, top)) pyramids.Add(new Pyramid(b, top.ObjectId));
        var cache = new Dictionary<BlockConfigurationType, IReadOnlyList<BlockConfiguration>>
        {
            [BlockConfigurationType.StackOfCubes] = stacks,
            [BlockConfigurationType.PyramidBase] = bases,
            [BlockConfigurationType.Pyramid] = pyramids,
        };
        double now = _clockSec();
        var previous = Volatile.Read(ref _snapshot).FirstSeenSec;
        var firstSeen = new Dictionary<string, double>();
        var newlySeen = new List<BlockConfiguration>();
        foreach (var c in stacks.Concat(bases).Concat(pyramids))
        {
            var k = Key(c);
            if (firstSeen.ContainsKey(k)) continue;
            // an arrangement that is gone is forgotten, so a rebuilt one counts as newly seen
            if (previous.TryGetValue(k, out var seen)) firstSeen[k] = seen;
            else { firstSeen[k] = now; newlySeen.Add(c); }
        }
        Volatile.Write(ref _snapshot, new Snapshot(cache, firstSeen));
        return (newlySeen, now);
    }

    /// <summary><c>StackOfCubes::BuildTallestStackForObject</c>: walk down to the bottom, then up, bound 8 each way.</summary>
    public StackOfCubes BuildTallestStackForObject(ObservableObject obj, IReadOnlyList<ObservableObject>? cubes = null)
    {
        cubes ??= _world.LocatedObjects.Where(o => CubeGeometry.IsCube(o.Type)).ToList();
        var bottom = obj;
        for (int i = 0; i < MaxStackHeight; i++)
        {
            var under = FindObjectOnTopOrUnderneath(bottom, cubes, onTop: false);
            if (under is null) break;
            bottom = under;
        }
        var ids = new List<uint> { bottom.ObjectId };
        var cur = bottom;
        for (int i = 0; i < MaxStackHeight; i++)
        {
            var above = FindObjectOnTopOrUnderneath(cur, cubes, onTop: true);
            if (above is null || ids.Contains(above.ObjectId)) break;
            ids.Add(above.ObjectId); cur = above;
        }
        return new StackOfCubes(ids);
    }

    /// <summary><c>BlockWorld::FindObjectOnTopOrUnderneathHelper</c>: the cube one height up (or down) with its centre over this one.</summary>
    public static ObservableObject? FindObjectOnTopOrUnderneath(ObservableObject obj, IReadOnlyList<ObservableObject> cubes, bool onTop)
    {
        double targetZ = obj.Pose.Translation.Z + (onTop ? CubeGeometry.CubeSizeMm : -CubeGeometry.CubeSizeMm);
        foreach (var c in cubes)
        {
            if (c.ObjectId == obj.ObjectId) continue;
            var d = c.Pose.Translation - obj.Pose.Translation;
            if (Math.Abs(c.Pose.Translation.Z - targetZ) > OnTopPlanarToleranceMm) continue;
            if (Math.Sqrt(d.X * d.X + d.Y * d.Y) > CubeGeometry.CubeSizeMm / 2) continue;
            return c;
        }
        return null;
    }

    /// <summary><c>PyramidBase::BlocksFormPyramidBase</c>.</summary>
    public static bool BlocksFormPyramidBase(ObservableObject a, ObservableObject b, out Vec3 interiorMidpoint)
    {
        interiorMidpoint = default;
        if (a.UpAxisFromPose() is not (UpAxis.ZPositive or UpAxis.ZNegative) || b.UpAxisFromPose() is not (UpAxis.ZPositive or UpAxis.ZNegative)) return false;
        if (Math.Abs(a.Pose.Translation.Z - b.Pose.Translation.Z) > SameHeightToleranceMm) return false;
        var d = b.Pose.Translation - a.Pose.Translation;
        double d2 = d.X * d.X + d.Y * d.Y;
        if (d2 > PyramidBaseMaxDistanceMm2 || d2 < (CubeGeometry.CubeSizeMm * 0.8) * (CubeGeometry.CubeSizeMm * 0.8)) return false;
        interiorMidpoint = (a.Pose.Translation + b.Pose.Translation) * 0.5;
        return true;
    }

    /// <summary><c>PyramidBase::ObjectIsOnTopOfBase</c>.</summary>
    public bool ObjectIsOnTopOfBase(PyramidBase b, ObservableObject top)
    {
        var left = _world.GetLocatedObjectById(b.LeftBlockId);
        if (left is null) return false;
        double targetZ = left.Pose.Translation.Z + CubeGeometry.CubeSizeMm;
        if (Math.Abs(top.Pose.Translation.Z - targetZ) > OnTopOfBaseToleranceMm) return false;
        var d = top.Pose.Translation - b.InteriorMidpoint;
        return Math.Sqrt(d.X * d.X + d.Y * d.Y) <= OnTopOfBaseToleranceMm;
    }

    /// <summary><c>CheckForPyramidBaseBelowObject</c>: the base (if any) this carried/held object could top.</summary>
    public PyramidBase? CheckForPyramidBaseBelowObject(uint objectId) => PyramidBases.FirstOrDefault(b => !b.ContainsBlock(objectId));

    /// <summary><c>DidAnyObjectsMovePastThreshold</c>: whether any block of the configuration is further than the threshold from where it was.</summary>
    public bool DidAnyObjectsMovePastThreshold(BlockConfiguration c, IReadOnlyDictionary<uint, Pose3d> posesAtStart, double thresholdMm)
    {
        foreach (var id in c.BlockIds)
        {
            var o = _world.GetObjectById(id);
            if (o is null || !o.IsLocated) return true;
            if (!posesAtStart.TryGetValue(id, out var was)) continue;
            var d = o.Pose.Translation - was.Translation;
            if (Math.Sqrt(d.X * d.X + d.Y * d.Y) > thresholdMm) return true;
        }
        return false;
    }

    private static string Key(BlockConfiguration c) => c.Type + ":" + string.Join(",", c.BlockIds.OrderBy(i => i));
}
