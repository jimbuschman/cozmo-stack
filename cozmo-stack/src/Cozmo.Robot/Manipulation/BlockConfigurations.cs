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
/// read: a block is on top of another when their footprints (bounding quads) intersect and the upper sits one
/// cube height above within the tolerance. The vertical tolerance is <c>FindObjectOnTopOrUnderneathHelper</c>'s second
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
    /// building the stack list. (C-R1: the other eight callers of the helper all pass 15 with onTop true,
    /// except <c>RollBlockHelper::UnableToRollDelegate</c> 0x005BA136, which passes 30.)
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
    /// BlockConfigurationManager+0xC (M13-014): set to 1 to force all block configurations to recompute on
    /// the next <see cref="Update"/> (the engine's <c>BlockConfigurationManager::Update</c> 0x00616D7C
    /// returns early when it is 0 and nothing moved, and runs <c>UpdateAllBlockConfigs</c> when it is 1).
    /// This stack's Update is always a full rebuild, so the flag is set for fidelity and cleared here.
    /// </summary>
    // fidelity: M13-014
    public bool ForceUpdate { get; set; }

    /// <summary>
    /// Rebuilds every configuration from the located cubes and publishes the result as one snapshot, so a
    /// reader on another thread never sees stacks from one pass beside pyramids from the next.
    /// </summary>
    public void Update()
    {
        ForceUpdate = false;
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
            var under = FindObjectOnTopOrUnderneath(bottom, cubes, onTop: false, OnTopPlanarToleranceMm);   // 0x00619358: 30, underneath
            if (under is null) break;
            bottom = under;
        }
        var ids = new List<uint> { bottom.ObjectId };
        var cur = bottom;
        for (int i = 0; i < MaxStackHeight; i++)
        {
            var above = FindObjectOnTopOrUnderneath(cur, cubes, onTop: true, OnTopPlanarToleranceMm);      // 0x0061929E: 30, on top
            if (above is null || ids.Contains(above.ObjectId)) break;
            ids.Add(above.ObjectId); cur = above;
        }
        return new StackOfCubes(ids);
    }

    /// <summary>
    /// <c>BlockWorld::FindObjectOnTopOrUnderneathHelper(obj, tolerance, filter, onTop)</c> 0x0062601C (M13-007).
    /// The reference height is <c>obj.z + dimZ(obj) * (onTop ? +0.5 : -0.5)</c> (0x0062607E..0x006260A0); the helper
    /// ignores <c>obj</c> itself (0x006260AC..0x006260B8) and returns the first candidate its lambda
    /// (0x0062B9FC) accepts: the candidate's bounding quad (padding 0, <see cref="Footprint.GetBoundingQuadXY"/>)
    /// intersecting the reference's own quad (<see cref="Quadrilateral.Intersects"/>), and
    /// <c>|targetZ - (cand.z + dimZ(cand) * (onTop ? -0.5 : +0.5))| &lt;= tolerance + 9.99999975e-06</c>
    /// (0x0062BAC2..0x0062BB0C), all in float32 as the engine does. The tolerance is the second argument and has no
    /// default: the callers pass <see cref="OnTopPlanarToleranceMm"/> (30: <c>BuildTallestStackForObject</c> both ways,
    /// and <c>RollBlockHelper::UnableToRollDelegate</c> 0x005BA136) or <see cref="RestingOnToleranceMm"/> (15: the other
    /// eight, all with <c>onTop</c> true).
    ///
    /// Not built, and visible (M13-023): only <c>Block::GetBoundingQuadXY</c> is read and <c>cv::minAreaRect</c> is not, so
    /// when the reference or the candidate is not a cube, or its pose is not yaw-only, the exact footprint cannot be computed.
    /// For that pair, and only that pair, this method uses <see cref="PlanarStandIn"/>: the earlier planar test (centres within
    /// half a cube, 22 mm, and the height within the tolerance of one cube height). It is a REDUCTION THAT DIFFERS FROM THE ENGINE
    /// (which intersects min-area-rectangle footprints of any pose); <c>usedStandIn</c> is set when it decided any
    /// candidate, so a caller and a test can see it. No exception escapes. This stack has one pose origin, so the
    /// lambda's "candidate with respect to the reference's parent" step (0x0062BA0E..0x0062BA48) always succeeds and
    /// the poses are the world poses; the candidate's z overwrite (0x0062BA50..0x0062BA78) does not reach the quad,
    /// which reads only x and y.
    /// </summary>
    // fidelity: M13-007
    public static ObservableObject? FindObjectOnTopOrUnderneath(ObservableObject obj, IReadOnlyList<ObservableObject> cubes, bool onTop,
                                                                double toleranceMm)
        => FindObjectOnTopOrUnderneath(obj, cubes, onTop, toleranceMm, out _);

    /// <summary>The same search that also reports whether the labelled <see cref="PlanarStandIn"/> (M13-023) decided any candidate.</summary>
    // fidelity: M13-007, M13-023
    public static ObservableObject? FindObjectOnTopOrUnderneath(ObservableObject obj, IReadOnlyList<ObservableObject> cubes, bool onTop,
                                                                double toleranceMm, out bool usedStandIn)
    {
        usedStandIn = false;
        float dimZ = (float)CubeGeometry.DimInParentFrameZ(obj);
        float targetZ = (float)obj.Pose.Translation.Z + dimZ * (onTop ? 0.5f : -0.5f);                // 0x0062607E..0x006260A0
        float s2 = onTop ? -0.5f : 0.5f;
        float tol = (float)toleranceMm;
        Footprint.TryGetBoundingQuadXY(obj, obj.Pose, 0f, out var refQuad);                          // 0x0062604C..0x0062605C
        foreach (var c in cubes)
        {
            if (c.ObjectId == obj.ObjectId) continue;                                                // 0x006260AC..0x006260B8
            Quadrilateral? candQuad = null;
            if (refQuad is null || !Footprint.TryGetBoundingQuadXY(c, c.Pose, 0f, out candQuad))     // 0x0062BA7A..0x0062BA86
            {
                // M13-023: the exact footprint is not computable for this pair; the labelled stand-in decides it
                usedStandIn = true;
                if (PlanarStandIn(obj, c, onTop, toleranceMm)) return c;
                continue;
            }
            if (!refQuad.Intersects(candQuad!)) continue;                                             // 0x0062BA50..0x0062BAA2
            float cz = (float)c.Pose.Translation.Z + (float)CubeGeometry.DimInParentFrameZ(c) * s2;
            if (!(MathF.Abs(targetZ - cz) <= tol + 9.99999975e-06f)) continue;                       // 0x0062BAC2..0x0062BB0C (literal 0x3727C5AC)
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

    /// <summary>
    /// EXPLICIT STAND-IN, a reduction that differs from the engine (M13-023, M13-007): the planar test used before the footprint
    /// was built. A candidate is found when its centre is within half a cube (22 mm) of the reference centre in x, y and its z is
    /// within the tolerance of the reference z plus or minus one cube height. Used ONLY when the exact footprint
    /// cannot be computed (a cube pose that is not yaw-only, or an object that is not a cube: <c>cv::minAreaRect</c> and the other
    /// <c>GetBoundingQuadXY</c> overrides are unread). The engine intersects min-area-rectangle footprints of any pose.
    /// </summary>
    // fidelity: M13-023
    public static bool PlanarStandIn(ObservableObject obj, ObservableObject c, bool onTop, double toleranceMm)
    {
        double targetZ = obj.Pose.Translation.Z + (onTop ? CubeGeometry.CubeSizeMm : -CubeGeometry.CubeSizeMm);
        var d = c.Pose.Translation - obj.Pose.Translation;
        if (Math.Abs(c.Pose.Translation.Z - targetZ) > toleranceMm) return false;
        if (Math.Sqrt(d.X * d.X + d.Y * d.Y) > CubeGeometry.CubeSizeMm / 2) return false;
        return true;
    }

    private static string Key(BlockConfiguration c) => c.Type + ":" + string.Join(",", c.BlockIds.OrderBy(i => i));
}

/// <summary>A float32 point, the engine's <c>Point2f</c>.</summary>
public readonly record struct Point2f(float X, float Y);

/// <summary>
/// <c>Anki::Quadrilateral</c> (M13-007): four corners in the engine's storage order, [0]=s0, [1]=s3, [2]=s1, [3]=s2
/// where s0..s3 are the corners sorted by ascending atan2 about the centroid (<c>SortCornersClockwise</c>,
/// 0x004E7EFC..0x004E7F9A; the constructor stores its four arguments in order). The perimeter walked by
/// <see cref="Intersects"/> is P0-&gt;P2-&gt;P3-&gt;P1-&gt;P0 (s0-&gt;s1-&gt;s2-&gt;s3).
///
/// Choices the inventory does not settle (rounding only, the maths is fixed): the operation order of the
/// barycentric expression in <c>IsPointWithinTriangleHelper</c> 0x004E03B8 and of the t/u expressions in
/// <c>kmLine2WithLineIntersection</c> 0x008F6220 (the standard forms, in float32).
/// </summary>
// fidelity: M13-007
public sealed class Quadrilateral
{
    private readonly Point2f[] _p;
    public Quadrilateral(Point2f p0, Point2f p1, Point2f p2, Point2f p3) { _p = new[] { p0, p1, p2, p3 }; }
    public Point2f this[int i] => _p[i];

    /// <summary>The two barycentric bounds <c>IsPointWithinTriangleHelper</c> compares with: 0xB4000000 and 0x3F800001 (0x004E0480, 0x004E0484).</summary>
    public const float BaryLower = -1.1920929e-07f;
    public const float BaryUpper = 1.0000001f;

    /// <summary><c>Contains</c>: the triangles (P0,P1,P3) and (P0,P2,P3), each barycentric coordinate strictly inside the widened bounds (0x004DF764..0x004DF7BC).</summary>
    public bool Contains(Point2f pt) => InTriangle(pt, _p[0], _p[1], _p[3]) || InTriangle(pt, _p[0], _p[2], _p[3]);

    private static bool InTriangle(Point2f p, Point2f a, Point2f b, Point2f c)
    {
        float denom = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
        float alpha = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / denom;
        float beta = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / denom;
        float gamma = 1f - alpha - beta;
        return alpha > BaryLower && alpha < BaryUpper && beta > BaryLower && beta < BaryUpper && gamma > BaryLower && gamma < BaryUpper;   // NaN: false
    }

    /// <summary><c>Quadrilateral::Intersects</c> 0x00514800: any corner of either quad contained by the other, or any of the 16 edge pairs intersect.</summary>
    public bool Intersects(Quadrilateral other)
    {
        for (int i = 0; i < 4; i++) if (Contains(other._p[i])) return true;
        for (int i = 0; i < 4; i++) if (other.Contains(_p[i])) return true;
        int[] perimeter = { 0, 2, 3, 1 };
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                if (SegmentsIntersect(_p[perimeter[i]], _p[perimeter[(i + 1) % 4]], other._p[perimeter[j]], other._p[perimeter[(j + 1) % 4]])) return true;
        return false;
    }

    /// <summary>
    /// <c>kmSegment2WithSegmentIntersection</c> 0x008F6320 over <c>kmLine2WithLineIntersection</c> 0x008F6220: each ray is (start, end - start) in float,
    /// its direction recomputed as (p + v) - p; the cross is a float compared in double against +-1e-4 (0x008F6310/0x008F6318, parallel band
    /// = no intersection); then 0 &lt;= t &lt;= 1 and 0 &lt;= u &lt;= 1 inclusive (NaN false).
    /// </summary>
    private static bool SegmentsIntersect(Point2f a0, Point2f a1, Point2f b0, Point2f b1)
    {
        float avx = a1.X - a0.X, avy = a1.Y - a0.Y, bvx = b1.X - b0.X, bvy = b1.Y - b0.Y;      // kmRay2FillWithEndpoints
        float x1 = a0.X, y1 = a0.Y, x2 = x1 + avx, y2 = y1 + avy;                              // 0x008F624E..0x008F627A
        float x3 = b0.X, y3 = b0.Y, x4 = x3 + bvx, y4 = y3 + bvy;
        float denom = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
        double d = denom;
        if (d > -0.0001 && d < 0.0001) return false;
        float t = ((x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)) / denom;
        float u = ((x2 - x1) * (y1 - y3) - (y2 - y1) * (x1 - x3)) / denom;
        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }
}

/// <summary>
/// <c>Block::GetBoundingQuadXY(pose, padding)</c> 0x004E62A2 (M13-007): the 8 canonical corners (+-0.5 on each axis,
/// <c>Block::GetCanonicalCorners</c> 0x004E60B8) scaled by the size plus 2*padding, rotated by the pose, xy kept,
/// <c>cv::minAreaRect</c> of the 8 points, the rectangle's corners sorted by atan2 about the centroid and stored
/// [0]=s0, [1]=s3, [2]=s1, [3]=s2, then translated by the pose's x, y (0x004E68CC). Read for the Block (and the
/// cube sub-vtables through thunk 0x004E690C) only.
///
/// <c>cv::minAreaRect</c> is unread (M13-023). When one of the object's own axes is vertical (a yaw-only pose, which is
/// what <c>ClampPoseToFlat</c> leaves a resting cube with) the 8 points are the 4 corners of a rectangle twice over, and
/// the rectangle is the min-area rectangle by definition, so its corners are used; any other pose throws
/// <see cref="NotSupportedException"/> (visible stub). CHOICE: "one axis is vertical" is |R[2,k]| &gt;= 1 - 1e-6 (float32-scale
/// noise, not from the inventory). Corners are computed in double and rounded to float32.
/// </summary>
// fidelity: M13-007, M13-023
public static class Footprint
{
    public static Quadrilateral GetBoundingQuadXY(ObservableObject o, Pose3d atPose, float padding)
    {
        if (!CubeGeometry.IsCube(o.Type))
            throw new NotSupportedException($"M13-023: GetBoundingQuadXY of {o.Type} is unread (only Block::GetBoundingQuadXY 0x004E62A2 is)");
        if (!TryGetBoundingQuadXY(o, atPose, padding, out var q))
            throw new NotSupportedException("M13-023: cv::minAreaRect (libopencv_imgproc.so) is unread; a footprint that is not a plain rectangle (a tilted pose) is not built");
        return q!;
    }

    /// <summary>The same footprint without the throw: false when it cannot be computed (not a cube, or a pose that is not yaw-only; M13-023).</summary>
    public static bool TryGetBoundingQuadXY(ObservableObject o, Pose3d atPose, float padding, out Quadrilateral? quad)
    {
        quad = null;
        if (!CubeGeometry.IsCube(o.Type)) return false;
        var size = CubeGeometry.SizeOf(o.Type);
        var r = atPose.Rotation;
        int k = -1;
        for (int c = 0; c < 3; c++) if (Math.Abs(r[2, c]) >= 1 - 1e-6) k = c;
        if (k < 0) return false;
        int i = (k + 1) % 3, j = (k + 2) % 3;
        double[] dim = { size.X + 2 * padding, size.Y + 2 * padding, size.Z + 2 * padding };
        var corners = new List<Point2f>(4);
        foreach (double si in new[] { -0.5, 0.5 })
            foreach (double sj in new[] { -0.5, 0.5 })
                corners.Add(new Point2f((float)(r[0, i] * si * dim[i] + r[0, j] * sj * dim[j]), (float)(r[1, i] * si * dim[i] + r[1, j] * sj * dim[j])));
        float cx = (corners[0].X + corners[1].X + corners[2].X + corners[3].X) / 4f, cy = (corners[0].Y + corners[1].Y + corners[2].Y + corners[3].Y) / 4f;
        var s = corners.OrderBy(p => MathF.Atan2(p.Y - cy, p.X - cx)).ToArray();                   // SortCornersClockwise: ascending atan2
        float tx = (float)atPose.Translation.X, ty = (float)atPose.Translation.Y;
        Point2f T(Point2f p) => new(p.X + tx, p.Y + ty);
        quad = new Quadrilateral(T(s[0]), T(s[3]), T(s[1]), T(s[2]));                             // stored [0]=s0, [1]=s3, [2]=s1, [3]=s2
        return true;
    }
}
