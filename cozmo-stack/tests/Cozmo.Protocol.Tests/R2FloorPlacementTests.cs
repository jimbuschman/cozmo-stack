using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Job R-FIX2 batch C: the floor-placement path of <c>BehaviorExploreBringCubeToBeacon</c> (records M15-019..M15-025). Expected values come from the extraction report
/// (<c>re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md</c>, rows cited per test) and from the independent binary32 emulation of the disassembly in
/// <c>re-analysis/evidence/m15-floor-placement/emulate_candidates.py</c>, never from running the C#.
/// </summary>
public class R2FloorPlacementTests
{
    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;

    /// <summary>The tests that render markers must not pass silently without the marker library: they fail with the reason unless the run says COZMO_TESTS_WITHOUT_ASSETS=1.</summary>
    private static bool NeedsAssets()
    {
        if (Lib is not null) return true;
        if (Environment.GetEnvironmentVariable("COZMO_TESTS_WITHOUT_ASSETS") == "1") return false;
        throw new Xunit.Sdk.XunitException("the marker library is missing (Cozmo.Robot was built without Vision/Data/marker_nn_library.bin), so this test cannot run; provide it or set COZMO_TESTS_WITHOUT_ASSETS=1");
    }

    private static float F(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));
    private static uint B(float f) => unchecked((uint)BitConverter.SingleToInt32Bits(f));

    private static Pose3d At(double x, double y, double z = 0, double yaw = 0) => new(Mat3.AboutZ(yaw), new Vec3(x, y, z));

    private static ObservableObject Cube(Rig rig, uint id, Pose3d pose, ObjectType type = ObjectType.Block_LIGHTCUBE1, bool locate = true)
    {
        var o = new ObservableObject(id, type, CubeGeometry.MarkersFor(type)) { Pose = pose, PoseState = PoseState.Known, OriginId = rig.M.World.CurrentOriginId };
        if (locate) rig.M.World.AddLocatedObject(o);
        return o;
    }

    private static BehaviorContext Ctx(Rig rig) => new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };

    private static void SpinUntil(Func<bool> cond, Action? tick = null, int ms = 8000)
    {
        SignalTestContext.Until(cond, tick);
    }

    private static bool Runnable(SteppedBehavior b, BehaviorContext ctx) =>
        (bool)typeof(SteppedBehavior).GetMethod("IsRunnableInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(b, new object[] { ctx })!;

    // ------------------------------------------------------------------------------------------- constants

    /// <summary>
    /// Every constant as the engine's bits (extraction section 6): 10.0f 0x41200000 (0x005E0564, 0x005E09F8, 0x005E0C1C, 0x005DFF02), 1e-5f 0x3727C5AC (0x005E0446, 0x0059C2C2), 100.0f 0x42C80000
    /// (0x005E0B68), pi 0x40490FDB (0x005E0B4A), FLT_MAX 0x7F7FFFFF (0x005E0998), 20.0f 0x41A00000 and pi/8 0x3EC90FDB (0x005E1D14, 0x005E1CFC), -1e-5f 0xB727C5AC (0x0056C07A), the IAction timeout
    /// 0x41F00000 (0x0052B0C4: <c>movt r0,#0x41f0</c>), the double literal 0x3E56A09E19D672B6 of UnitQuaternion_&lt;double&gt;::Normalize (0x00849E58), the caps {1,1,10,1} (0x0056B8A4) and the
    /// PlaceObjectOnGround message words (0x00632B88: 0, 0, 0, 0x42C80000, 0x43480000, 0x43FA0000 from 0x00C7CD90).
    /// </summary>
    [Fact]
    public void M15_Constants_AreTheEnginesBitPatterns()
    {
        Assert.Equal(0x41200000u, B(BeaconFloorGeometry.Ten));
        Assert.Equal(0x3727C5ACu, B(BeaconFloorGeometry.OneEm5));
        Assert.Equal(0x42C80000u, B(BeaconFloorGeometry.FarDistance));
        Assert.Equal(0x40490FDBu, B(BeaconFloorGeometry.Pi));
        Assert.Equal(0x7F7FFFFFu, B(BeaconFloorGeometry.FltMax));
        Assert.Equal(0x41A00000u, B(BringCubeToBeaconBehavior.StackFilterDistMm));
        Assert.Equal(0x3EC90FDBu, B(BringCubeToBeaconBehavior.StackFilterAngleRad));
        Assert.Equal(0xB727C5ACu, B(AIWhiteboard.NegSlack));
        Assert.Equal(0x41F00000u, B(PlaceObjectOnGroundAction.TimeoutSec));
        Assert.Equal(0x3E56A09E19D672B6UL, unchecked((ulong)BitConverter.DoubleToInt64Bits(BeaconFloorGeometry.NormalizeTolerance)));
        Assert.Equal(new[] { 1, 1, 10, 1 }, new[] { 0, 1, 2, 3 }.Select(i => AIWhiteboard.FailureCap((ObjectActionFailure)i)).ToArray());
        Assert.Equal(0, AIWhiteboard.FailureCap(ObjectActionFailure.Any));                 // a failure above 3 has a cap of 0 (0x0056B8A4 table, "failure > 3 -> 0")
        var b = PlaceObjectOnGroundAction.Message().ToBytes();
        Assert.Equal(0u, BitConverter.ToUInt32(b, 1)); Assert.Equal(0u, BitConverter.ToUInt32(b, 5)); Assert.Equal(0u, BitConverter.ToUInt32(b, 9));
        Assert.Equal(0x42C80000u, BitConverter.ToUInt32(b, 13)); Assert.Equal(0x43480000u, BitConverter.ToUInt32(b, 17)); Assert.Equal(0x43FA0000u, BitConverter.ToUInt32(b, 21));
    }

    // ------------------------------------------------------------------------------------------- M15-019: the arithmetic

    private static readonly uint[] Z0 = { 0 };

    /// <summary>
    /// M15-019, 0x005E03D0..0x005E0512 (the frame from the robot) and 0x005E09E0..0x005E0A66 (the candidate): the translation of six candidates in three frames, bit for bit against the binary32 emulation
    /// of the disassembly (emulate_candidates.py: Rotation3d(Radians, Z) 0x0084A506, Normalize 0x00849DB8, operator* 0x00849C1C, S = 44.0f + 10.0f, off = (S*i, S*j, 0), t = p + B).
    /// </summary>
    [Theory]
    [InlineData(0, 50, 0, 0x42C80000u, 0x42480000u, 0x42C80000u, 0xC0800000u, 0x42C80000u, 0x42D00000u, 0x431A0000u, 0x42480000u, 0x42380000u, 0x42D00000u, 0x43500000u, 0xC0800000u)]   // beacon +x of the robot: angle 0
    [InlineData(100, -50, 0, 0x42C80000u, 0x42480000u, 0x431A0000u, 0x42480000u, 0x42380001u, 0x42480000u, 0x42C80000u, 0x42D00000u, 0x42380001u, 0xC07FFFF0u, 0x431A0000u, 0x431E0000u)]   // +y: acosf(0) = pi/2
    [InlineData(300, 50, 0, 0x42C80000u, 0x42480000u, 0x42C7FFFFu, 0x42D00000u, 0x42C80001u, 0xC0800000u, 0x42380000u, 0x4247FFFFu, 0x431A0000u, 0xC07FFFF0u, 0xC1000008u, 0x42CFFFFFu)]   // -x: acosf(-1) = pi, negated, rescaled to +pi
    public void M15_019_TheCandidateTranslationsAreTheDisassemblysBinary32Arithmetic(double rx, double ry, double rz,
        uint t00x, uint t00y, uint t0m1x, uint t0m1y, uint t01x, uint t01y, uint t10x, uint t10y, uint tm11x, uint tm11y, uint t2m1x, uint t2m1y)
    {
        float bx = 100f, by = 50f, bz = 0f;
        var rot = BeaconFloorGeometry.FrameFromRobot(bx, by, bz, (float)rx, (float)ry, (float)rz, BeaconFloorGeometry.RotationAboutAxis(0.0f, 0.0f, 0.0f, 1.0f));
        float spacing = 44.0f + 10.0f;
        (int, int, uint, uint)[] expected = { (0, 0, t00x, t00y), (0, -1, t0m1x, t0m1y), (0, 1, t01x, t01y), (1, 0, t10x, t10y), (-1, 1, tm11x, tm11y), (2, -1, t2m1x, t2m1y) };
        foreach (var (i, j, ex, ey) in expected)
        {
            var c = BeaconFloorGeometry.CandidateTranslation(rot, spacing, i, j, bx, by, bz);
            Assert.True(ex == B(c.X) && ey == B(c.Y) && B(c.Z) == 0u, $"i={i} j={j}: got ({B(c.X):X8}, {B(c.Y):X8}, {B(c.Z):X8}), expected ({ex:X8}, {ey:X8}, 0)");
        }
    }

    /// <summary>The spacing is size.x + 10.0f where size.x is 44.0f for LIGHTCUBE1..3 (Block::LookupBlockInfo 0x004E4C8C: four entries, keys 1..4, each size 0x42300000 at 0x004E4CD6, 0x004E4D90, 0x004E4E46, 0x004E4EFA).</summary>
    [Fact]
    public void M15_019_TheCubeSizeTheSpacingUsesIs44ForEveryLightCube()
    {
        foreach (var t in new[] { ObjectType.Block_LIGHTCUBE1, ObjectType.Block_LIGHTCUBE2, ObjectType.Block_LIGHTCUBE3 })
            Assert.Equal(0x42300000u, B((float)CubeGeometry.SizeOf(t).X));
    }

    /// <summary>vcvt.s32.f32: toward zero, saturating, NaN to 0 (0x005E0574): N = trunc(radius / S).</summary>
    [Fact]
    public void M15_019_TruncationOfTheGridHalfWidth()
    {
        Assert.Equal(3, BeaconFloorGeometry.TruncToInt(175f / 54f));                       // 3.24
        Assert.Equal(0, BeaconFloorGeometry.TruncToInt(0.9999999f));
        Assert.Equal(0, BeaconFloorGeometry.TruncToInt(-0.9f));                             // toward zero, not floor
        Assert.Equal(-1, BeaconFloorGeometry.TruncToInt(-1.5f));
        Assert.Equal(0, BeaconFloorGeometry.TruncToInt(float.NaN));
        Assert.Equal(int.MaxValue, BeaconFloorGeometry.TruncToInt(float.PositiveInfinity));
    }

    // ------------------------------------------------------------------------------------------- M15-019: the search

    private BringCubeToBeaconBehavior NewBring(Rig rig, double cooldown = 45) => new(rig.M, "Hiking_BringCubeToBeacon", cooldown);

    /// <summary>
    /// M15-019 row 1.6 (0x005E0588..0x005E0626): the order. Phase A: i = 0, -1, ..., -N; phase B: i = 1..N; for each i: j = 0, then -1, +1, -2, +2, ..., -N, +N. Every candidate is tested (here every
    /// one is rejected, by the failure memory or by the radius, so the whole order shows) and the out pose is the LAST tested candidate (row 1.7: it is written before any test).
    /// N = trunc(radius / 54): 54 -> 1, 107.99999 -> 1, 108 -> 2, 53.99999 -> 0, a negative radius tests nothing (0x005E0580 blt 0x005E0628).
    /// </summary>
    [Fact]
    public void M15_019_TheGridOrderAndTheOutPoseHoldingTheLastTestedCandidate()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        double clock = 0; rig.M.ClockSec = () => clock;
        var bring = NewBring(rig);
        var carried = Cube(rig, 7, At(500, 500, 22), locate: false);
        var robot = At(-300, 0);
        // nine failures, each blocking everything within 100 mm of itself: every grid point (54 i, 54 j), |i|,|j| <= 2, is within 100 mm of one of them
        uint id = 100;
        foreach (var (x, y) in new[] { (0, 0), (108, 0), (-108, 0), (0, 108), (0, -108), (108, 108), (108, -108), (-108, 108), (-108, -108) })
            rig.M.Whiteboard.SetFailedToUse(id++, ObjectActionFailure.PlaceObjectAt, At(x, y));
        List<(int, int)> Run(double radius, out bool found, out Pose3d pose)
        {
            var beacon = new AIBeacon(At(0, 0), radius);
            bring.CandidateLog = new List<(int I, int J)>();
            found = bring.FindFreePoseInBeacon(carried, beacon, robot, 45f, out pose);
            return bring.CandidateLog.Select(c => (c.I, c.J)).ToList();
        }
        var n2 = Run(120, out var f2, out var last2);
        Assert.False(f2);
        var order = new List<(int, int)>();
        foreach (var i in new[] { 0, -1, -2, 1, 2 })
            foreach (var j in new[] { 0, -1, 1, -2, 2 }) order.Add((i, j));
        Assert.Equal(order, n2);
        Assert.Equal(108.0, last2.Translation.X, 3);                                        // (i, j) = (2, 2): off = (108, 108), identity frame
        Assert.Equal(108.0, last2.Translation.Y, 3);
        Assert.Equal(new[] { (0, 0), (0, -1), (0, 1), (-1, 0), (-1, -1), (-1, 1), (1, 0), (1, -1), (1, 1) }.ToList(), Run(54, out _, out _));              // N = 1
        Assert.Equal(9, Run(107.99999, out _, out _).Count);                                 // float(107.99999) < 108: N = 1
        Assert.Equal(25, Run(108, out _, out _).Count);                                      // N = 2
        Assert.Equal(new[] { (0, 0) }.ToList(), Run(53.99999, out _, out _));              // N = 0: only the centre
        Assert.Empty(Run(-60, out var fneg, out _));                                         // N = trunc(-60 / 54) = -1 < 0: nothing is tested
        Assert.Equal(new[] { (0, 0) }, Run(-1, out _, out _));                               // trunc(-0.0185) = 0 (toward zero): the centre is tested
        Assert.False(fneg);
    }

    /// <summary>
    /// M15-019 row 1.8: the radius test is on the ROTATED OFFSET, strictly greater rejects (<c>vcmpe s0,s2; ble</c> 0x005E0ADE..0x005E0AE6), no epsilon: a candidate at exactly the radius is accepted.
    /// Row 1.9: the recent-failure skip is <c>DidFailToUse(-1, 2, cooldown, *out, 100.0f, pi)</c>: ANY object's PlaceObjectAt failure younger than the cooldown within 100 mm (SPHERICAL, inclusive) of the candidate.
    /// A failure at the beacon centre blocks (0,0), (0,-54) and (0,54) (54 mm away); (0,-108) is 108 mm away and is accepted, at d2 = 108^2 = radius^2.
    /// </summary>
    [Fact]
    public void M15_019_TheRadiusTestIsStrictAndTheFailureSkipIsASphereOf100Millimetres()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        double clock = 0; rig.M.ClockSec = () => clock;
        var bring = NewBring(rig);
        var carried = Cube(rig, 7, At(500, 500, 22), locate: false);
        var robot = At(-300, 0);
        rig.M.Whiteboard.SetFailedToUse(50, ObjectActionFailure.PlaceObjectAt, At(0, 0));
        bring.CandidateLog = new();
        Assert.True(bring.FindFreePoseInBeacon(carried, new AIBeacon(At(0, 0), 108), robot, 45f, out var pose));
        Assert.Equal(new[] { (0, 0), (0, -1), (0, 1), (0, -2) }, bring.CandidateLog.Select(c => (c.I, c.J)).ToArray());
        Assert.Equal(0xC2D80000u, B((float)pose.Translation.Y));                              // 54.0f * -2.0f
        Assert.Equal(0u, B((float)pose.Translation.X) & 0x7FFFFFFFu);
        // the same search with a failure 100 mm from (0,-108) (at (0,-8)): the distance is exactly 100.0f, d2 <= d*d accepts the match (inclusive), so (0,-108) is rejected; (0, 108) is 116 mm away
        rig.M.Whiteboard.OnRobotDelocalized();
        rig.M.Whiteboard.SetFailedToUse(50, ObjectActionFailure.PlaceObjectAt, At(0, -8));
        bring.CandidateLog = new();
        Assert.True(bring.FindFreePoseInBeacon(carried, new AIBeacon(At(0, 0), 175), robot, 45f, out pose));
        Assert.Equal(108.0, pose.Translation.Y, 3);
        Assert.Equal((0, 2), bring.CandidateLog[^1]);
        // a spherical test: (80, 80) apart per axis is 113 mm, outside 100 although every axis is within 100 (a per-axis box would match)
        rig.M.Whiteboard.OnRobotDelocalized();
        rig.M.Whiteboard.SetFailedToUse(50, ObjectActionFailure.PlaceObjectAt, At(54 - 80, 0 - 80));            // 80 mm from (54, 0) on each of x and y
        Assert.False(rig.M.Whiteboard.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, 45f, At(54, 0), 100.0f, BeaconFloorGeometry.Pi));
        Assert.True(rig.M.Whiteboard.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, 45f, At(54 - 60, 0 - 80 + 80), 100.0f, BeaconFloorGeometry.Pi));
        // the failure expires with the cooldown: a candidate that failed 45 s ago is free again (row 4.3.6: strict, 1e-5f slack)
        rig.M.Whiteboard.OnRobotDelocalized();
        rig.M.Whiteboard.SetFailedToUse(50, ObjectActionFailure.PlaceObjectAt, At(0, 0));
        clock = 44.9;
        Assert.True(bring.FindFreePoseInBeacon(carried, new AIBeacon(At(0, 0), 108), robot, 45f, out pose));
        Assert.Equal(-108.0, pose.Translation.Y, 3);
        clock = 45.0;
        bring.CandidateLog = new();
        Assert.True(bring.FindFreePoseInBeacon(carried, new AIBeacon(At(0, 0), 108), robot, 45f, out pose));
        Assert.Equal((0, 0), bring.CandidateLog[0]);
        Assert.Single(bring.CandidateLog);                                                        // accepted at once: the failure is 45.0 s old, not younger
    }

    /// <summary>
    /// M15-019/M15-020 rows 1.2, 1.2a..c and 1.10: the beacon holds three cubes: the CARRIED one at the centre (rejected by the FindCubesInBeacon predicate, 0x0056D142), the nearer of the other two (id 8, yaw
    /// pi/2, 10 mm from the centre) and a farther one (id 9, yaw 1.0). The frame is the nearest cube's rotation about Z (CalculateDirectionalityClosest), so the frame is pi/2 (the emulation's frame B) and
    /// not the carried cube's 0. The candidates (0,0) and (0,-1) overlap cube 8 (the other cube's quad is padded by 10.0f, no Z test) and (0,1), at (46, 50), is the first free one:
    /// emulate_candidates.py gives its translation as (0x42380001, 0x42480000).
    /// </summary>
    [Fact]
    public void M15_019_M15_020_TheFrameFollowsTheNearestNonCarriedCubeAndTheObstacleTestUsesTheCarriedFootprint()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        var bring = NewBring(rig);
        var carried = Cube(rig, 7, At(100, 50, 22, 0));
        Cube(rig, 8, At(110, 50, 22, Math.PI / 2));
        Cube(rig, 9, At(120, 90, 22, 1.0), ObjectType.Block_LIGHTCUBE2);
        rig.M.Docking.Carrying.SetCarrying(7);
        var beacon = new AIBeacon(At(100, 50, 0), 175);
        // the predicate: 8 and 9 are in the beacon (3-D: z = 22 counts), the carried 7 is not
        Assert.Equal(new uint[] { 8, 9 }, rig.M.Whiteboard.FindCubesInBeacon(beacon).Select(o => o.ObjectId).ToArray());
        bring.CandidateLog = new();
        Assert.True(bring.FindFreePoseInBeacon(carried, beacon, At(0, 0), 45f, out var pose));
        Assert.Equal(new[] { (0, 0), (0, -1), (0, 1) }, bring.CandidateLog.Select(c => (c.I, c.J)).ToArray());
        Assert.Equal(0x42380001u, B((float)pose.Translation.X));
        Assert.Equal(0x42480000u, B((float)pose.Translation.Y));
        Assert.Equal(0u, B((float)pose.Translation.Z));
    }

    /// <summary>
    /// M15-020 row 1.2b, IsLocWithinBeacon (0x0059C24C..0x0059C2E8) as FindCubesInBeacon calls it with extra 0.0f: x*x + y*y + z*z in f32 against radius^2 + 1e-5f, inclusive; z counts.
    /// FindCubesInBeacon also takes families Block (1) and LightCube (2) only, so a charger is not a cube here.
    /// </summary>
    [Fact]
    public void M15_020_FindCubesInBeaconIsThreeDimensionalAndSkipsTheCarriedCube()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        Cube(rig, 8, At(10, 0, 22));
        Cube(rig, 9, At(0, 0, 80));                                                        // z = 80 > 30: outside a 30 mm beacon although planar-inside
        var beacon = new AIBeacon(At(0, 0, 0), 30);
        Assert.Equal(new uint[] { 8 }, rig.M.Whiteboard.FindCubesInBeacon(beacon).Select(o => o.ObjectId).ToArray());
        rig.M.Docking.Carrying.SetCarrying(8);
        Assert.Empty(rig.M.Whiteboard.FindCubesInBeacon(beacon));
    }

    // ------------------------------------------------------------------------------------------- M15-024: the failure memory

    /// <summary>
    /// M15-024 (4.3.3): SetFailedToUse keeps, per object id, the last cap entries (the word table 0x0056B8A4 = {1, 1, 10, 1}: PickUp 1, StackOn 1, PlaceObjectAt 10, RollOrPopAWheelie 1), dropping the oldest
    /// first (0x0056B7BE); each table is separate (GetObjectFailureTable 0x0056B8F4, tbb {2,38,41,44}); FailureInfo carries the pose and the f32 time.
    /// </summary>
    [Fact]
    public void M15_024_TheCapsKeepTheLastEntriesPerObjectAndPerFailureKind()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        double clock = 0; rig.M.ClockSec = () => clock;
        var wb = rig.M.Whiteboard;
        for (int k = 0; k < 11; k++) { clock = k; wb.SetFailedToUse(5, ObjectActionFailure.PlaceObjectAt, At(k, 0)); }
        var list = wb.GetObjectFailureTable(ObjectActionFailure.PlaceObjectAt)[5];
        Assert.Equal(10, list.Count);
        Assert.Equal(1.0, list[0].Pose.Translation.X);                                       // the first (k = 0) was removed to add the eleventh
        Assert.Equal(1.0f, list[0].TimeSec);
        Assert.Equal(10.0f, list[^1].TimeSec);
        clock = 20; wb.SetFailedToUse(6, ObjectActionFailure.PickUpObject, At(1, 0)); wb.SetFailedToUse(6, ObjectActionFailure.PickUpObject, At(2, 0));
        var pick = wb.GetObjectFailureTable(ObjectActionFailure.PickUpObject)[6];
        Assert.Single(pick); Assert.Equal(2.0, pick[0].Pose.Translation.X);                  // cap 1
        wb.SetFailedToUse(6, ObjectActionFailure.StackOnObject, At(3, 0)); wb.SetFailedToUse(6, ObjectActionFailure.StackOnObject, At(4, 0));
        wb.SetFailedToUse(6, ObjectActionFailure.RollOrPopAWheelie, At(5, 0)); wb.SetFailedToUse(6, ObjectActionFailure.RollOrPopAWheelie, At(6, 0));
        Assert.Equal(4.0, Assert.Single(wb.GetObjectFailureTable(ObjectActionFailure.StackOnObject)[6]).Pose.Translation.X);
        Assert.Equal(6.0, Assert.Single(wb.GetObjectFailureTable(ObjectActionFailure.RollOrPopAWheelie)[6]).Pose.Translation.X);
        Assert.False(wb.GetObjectFailureTable(ObjectActionFailure.PickUpObject).ContainsKey(5));
        Assert.Empty(wb.GetObjectFailureTable(ObjectActionFailure.Any));                      // any other value: the static empty map
    }

    /// <summary>
    /// M15-024 (4.3.5, 4.3.6, EntryMatches 0x0056C070): (1) with time >= -1e-5f an entry is too old when <c>time + (-1e-5f) &lt;= now - entry.time</c> (strict expiry, f32): an entry exactly 45.0 s old is expired
    /// for a 45.0 cooldown, one 44.0 s old is not; the engine's NEGATIVE time skips the expiry test. (2) dist &lt; -1e-5f matches without the pose (the shorter overloads' -1.0f). id -1 walks every object's
    /// list; a given id only its own. (3) IsSameAs: spherical distance (<= inclusive) then the angle: radians >= pi accepts any rotation, otherwise the rotation angle difference must be &lt;= radians.
    /// </summary>
    [Fact]
    public void M15_024_EntryMatchesExpiryDistanceAndAngle()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        double clock = 0; rig.M.ClockSec = () => clock;
        var wb = rig.M.Whiteboard;
        float noDist = F(0xBF800000);
        float pi = BeaconFloorGeometry.Pi;
        wb.SetFailedToUse(5, ObjectActionFailure.PlaceObjectAt, At(0, 0, 0, 0.0));
        clock = 44.0;
        Assert.True(wb.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, 45f, At(0, 0), noDist, 0f));
        clock = 45.0;
        Assert.False(wb.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, 45f, At(0, 0), noDist, 0f));                 // 45.0 - 1e-5f <= 45.0: too old
        Assert.True(wb.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, -1f, At(0, 0), noDist, 0f));                  // a negative time never expires
        clock = 0;
        Assert.True(wb.DidFailToUse(5, ObjectActionFailure.PlaceObjectAt, 45f, At(1000, 1000), noDist, 0f));             // dist -1.0f: the pose is not read
        Assert.False(wb.DidFailToUse(6, ObjectActionFailure.PlaceObjectAt, 45f, At(0, 0), noDist, 0f));                  // another id
        Assert.True(wb.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, 45f, At(0, 0), noDist, 0f));                  // -1: any id
        Assert.False(wb.DidFailToUse(-1, ObjectActionFailure.StackOnObject, 45f, At(0, 0), noDist, 0f));                 // another failure kind
        // the distance: (60, 80, 0) is exactly 100.0f away: inclusive; 100.1 is not; z counts
        Assert.True(wb.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, 45f, At(60, 80), 100f, pi));
        Assert.False(wb.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, 45f, At(60.1, 80), 100f, pi));
        Assert.False(wb.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, 45f, At(0, 0, 100.5), 100f, pi));
        // the angle: with 20.0f and pi/8 (the stack-on filter's arguments) a yaw difference of 0.3 matches (0.3 < 0.3927), 0.4 does not; with pi any rotation matches
        float stackDist = BringCubeToBeaconBehavior.StackFilterDistMm, stackAngle = BringCubeToBeaconBehavior.StackFilterAngleRad;
        Assert.True(wb.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, 45f, At(0, 0, 0, 0.3), stackDist, stackAngle));
        Assert.False(wb.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, 45f, At(0, 0, 0, 0.4), stackDist, stackAngle));
        Assert.True(wb.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, 45f, At(0, 0, 0, 3.0), 20f, pi));
    }

    /// <summary>
    /// M15-024 (4.4): the stack-on candidate filter (the lambda 0x005E1CC6, its DidFailToUse at 0x005E1D24) asks for failure 1 with the cooldown, the CANDIDATE'S pose, 20.0f and pi/8: a failure at the
    /// same spot of another yaw outside pi/8 does not block it, one inside does. The floor path's own call is (-1, 2, ..., 100.0f, pi) (0x005E0B78).
    /// </summary>
    [Fact]
    public void M15_024_TheStackOnFilterUsesThePoseAwareMemory()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        double clock = 0; rig.M.ClockSec = () => clock;
        var bring = NewBring(rig);
        var beacon = new AIBeacon(At(0, 0), 175);
        var carried = Cube(rig, 7, At(500, 500, 22), locate: false);
        var stackable = Cube(rig, 8, At(0, 0, 22));
        rig.M.Docking.Carrying.SetCarrying(7);
        bring.IsUnlocked = _ => true;
        Assert.Same(stackable, bring.FindFreeCubeToStackOn(carried, beacon));
        rig.M.Whiteboard.SetFailedToUse(8, ObjectActionFailure.StackOnObject, At(10, 0, 22, 0.1));                    // 10 mm and 0.1 rad away: within 20.0f and pi/8
        Assert.Null(bring.FindFreeCubeToStackOn(carried, beacon));
        rig.M.Whiteboard.OnRobotDelocalized();
        rig.M.Whiteboard.SetFailedToUse(8, ObjectActionFailure.StackOnObject, At(10, 0, 22, 1.0));                    // 1.0 rad away: outside pi/8
        Assert.Same(stackable, bring.FindFreeCubeToStackOn(carried, beacon));
        rig.M.Whiteboard.OnRobotDelocalized();
        rig.M.Whiteboard.SetFailedToUse(8, ObjectActionFailure.StackOnObject, At(30, 0, 22, 0.0));                    // 30 mm away: outside 20.0f
        Assert.Same(stackable, bring.FindFreeCubeToStackOn(carried, beacon));
    }

    // ------------------------------------------------------------------------------------------- M15-023

    /// <summary>
    /// M15-023 (4.1.5, 4.1.6): AIBeacon::FailedToFindLocation stamps [beacon+0x10] with the f32 time (0x0059C314..0x0059C322; AddBeacon stores 0 there, 0x0056C3BC); AIWhiteboard::FailedToFindLocationInBeacon
    /// (0x0056C3F0) then UpdateBeaconRender. IsRunnableInternal (0x005DF0FC): with |t| >= 1e-5f it is not runnable while <c>(t + cooldown) + (-1e-5f) &gt; now</c> (f32). Hiking 45.0f, sparks 5.0f.
    /// </summary>
    [Fact]
    public void M15_023_TheFailureStampBlocksTheBehaviourForTheCooldown()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        double clock = 1000; rig.M.ClockSec = () => clock;
        var ctx = Ctx(rig);
        Cube(rig, 8, At(500, 0, 22));                                                       // a usable cube outside every beacon
        var hiking = NewBring(rig, 45);
        var sparks = new BringCubeToBeaconBehavior(rig.M, "SparksBringCubeToBeacon", 5);
        Assert.False(Runnable(hiking, ctx));                                                // no beacon: not runnable (0x005DF144)
        var beacon = rig.M.Whiteboard.AddBeacon(At(0, 0), 175);
        Assert.Equal(0u, B(beacon.FailedToFindLocationTimeSec));                            // movs r2,#0; str r2,[r0,#0x10]
        Assert.True(Runnable(hiking, ctx));
        int renders = 0; rig.M.Whiteboard.BeaconRenderUpdated += () => renders++;
        rig.M.Whiteboard.FailedToFindLocationInBeacon(beacon);
        Assert.Equal(1, renders);
        Assert.Equal(1000.0f, beacon.FailedToFindLocationTimeSec);
        clock = 1044.0;
        Assert.False(Runnable(hiking, ctx));
        Assert.True(Runnable(sparks, ctx));                                                 // 5.0f: 1005 <= 1044
        clock = 1004.0;
        Assert.False(Runnable(sparks, ctx));
        clock = 1045.0;                                                                     // (1000 + 45) + (-1e-5f) = 1045.0f in f32, not > 1045
        Assert.True(Runnable(hiking, ctx));
        clock = 1044.9999;                                                                  // float(1044.9999) = 1044.99988 < 1045
        Assert.False(Runnable(hiking, ctx));
    }

    /// <summary>
    /// M15-023 NoFreePoses, 0x005DF69C..0x005DF73A, through the live behaviour: the carried cube is picked up, the beacon is too small for a free pose (radius 30: N = 0, and the one candidate at the centre
    /// overlaps another cube there), so, in order: the beacon is stamped (f32 time), "...TransitionToObjectPickedUp.NoFreePoses" is logged, the "HikingNoLocationAtBeacon" (24 characters) event is
    /// triggered; NO action is started (no PlaceObjectOnGround, no further path), and the carried cube stays carried. NeedActionCompleted(PickupCube) was reported BEFORE the pose search (M15-025 row 4.1.3).
    /// </summary>
    [Fact]
    public void M15_023_M15_025_NoFreePosesStampsLogsTriggersTheEventAndStartsNothing()
    {
        if (!NeedsAssets()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        double clock = 500; rig.M.ClockSec = () => clock;
        rig.Cube = At(330, -20, 22);
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, At(255, 90, 22)));
        Assert.Equal(2, rig.Frame().Objects.Count);
        var ctx = Ctx(rig);
        ctx.Needs = new NeedsManager(() => 0);
        var beacon = rig.M.Whiteboard.AddBeacon(At(250, 90), 30);
        var bring = NewBring(rig);
        bring.IsUnlocked = _ => false;                                                       // FindFreeCubeToStackOn answers null: the floor branch
        bring.ActionTaskRunner = SignalTestContext.Schedule;
        bring.WorkPosted += signals_rig.Notify;
        double t = 0;
        bring.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        uint target = bring.Candidate!.Value;
        while (bring.Update(ctx, t += 33)) { rig.Pump(); if (!rig.M.Docking.Carrying.IsCarryingObject) rig.Frame(); SignalTestContext.AdvanceBehavior(bring); }
        Assert.True(rig.M.Docking.Carrying.IsCarrying(target));
        Assert.Equal(500.0f, beacon.FailedToFindLocationTimeSec);
        var tr = bring.Trace.ToList();
        int needs = tr.FindIndex(l => l == "needs action PickupCube");
        int noFree = tr.FindIndex(l => l.Contains("TransitionToObjectPickedUp.NoFreePoses: Could not decide where to drop the cube in the beacon (all poses failed)"));
        int mood = tr.FindIndex(l => l.Contains("emotion event HikingNoLocationAtBeacon"));
        Assert.True(needs >= 0 && noFree > needs && mood > noFree, string.Join(" | ", bring.Trace));
        Assert.Equal(24, BringCubeToBeaconBehavior.NoLocationEmotionEvent.Length);
        Assert.DoesNotContain(rig.Sent, m => m is PlaceObjectOnGround);
        Assert.DoesNotContain(bring.Trace, l => l.Contains("start PlaceObjectOnGroundAtPose"));
        Assert.False(Runnable(bring, ctx));                                                  // carrying (and inside the 45 s cooldown)
    }

    // ------------------------------------------------------------------------------------------- M15-021: the callback

    private (BringCubeToBeaconBehavior Bring, uint Target) StartedBring(Rig rig, BehaviorContext ctx)
    {
        Cube(rig, 8, At(500, 0, 22));
        rig.M.Whiteboard.AddBeacon(At(0, 0), 175);
        var bring = NewBring(rig);
        bring.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        return (bring, bring.Candidate!.Value);
    }

    /// <summary>
    /// M15-021/M15-025 (4.2.1..4.2.5), the callback 0x005E188C by result category. 0: "Successfully placed cube" and FireEmotionEvents, NO NeedActionCompleted, NO SetFailedToUse. 4 (RETRY) with the carried id
    /// equal to the target and attempt &lt;= 2: the SAME pose again with attempt + 1 (CanRetry). 4 with attempt 3, or not carrying the target: CannotRetry "(attempt=%d/3) (carrying=yes|no)" and the failure tail.
    /// 1, 2 and 3: NoRetryAllowed and the failure tail. The tail records SetFailedToUse(obj, 2 = PlaceObjectAt, the CANDIDATE pose) (0x005E1B56) when the target is still located.
    /// </summary>
    [Fact]
    public void M15_021_TheCallbackActsPerResultCategory()
    {
        if (!NeedsAssets()) return;
        var pose = At(12.5, -30.25, 0, 0.0);
        void Case(uint result, int attempt, bool carrying, Action<BringCubeToBeaconBehavior, Rig, string[]> check)
        {
            using var signals_rig = SignalTestContext.Install();
            using var rig = new Rig();
            var ctx = Ctx(rig); ctx.Needs = new NeedsManager(() => 0);
            var (bring, target) = StartedBring(rig, ctx);
            if (carrying) rig.M.Docking.Carrying.SetCarrying(target);
            int before = bring.Trace.Count;
            bring.PlaceCompleted((ActionResult)result, pose, attempt);
            check(bring, rig, bring.Trace.Skip(before).ToArray());
            bring.Stop(BehaviorStopReason.Cancelled);
        }
        // success: no needs call, no failure recorded
        Case(0, 1, true, (b, rig, log) =>
        {
            Assert.Contains(log, l => l.Contains("onPlaceActionResult.Done: Successfully placed cube"));
            Assert.Contains(log, l => l.Contains("emotion event Hiking"));
            Assert.DoesNotContain(log, l => l.StartsWith("needs action"));
            Assert.Empty(rig.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PlaceObjectAt));
        });
        // retry while carrying the target and attempt <= 2: the same pose, attempt + 1, no failure yet
        foreach (int attempt in new[] { 1, 2 })
            Case(0x04000008, attempt, true, (b, rig, log) =>
            {
                Assert.Contains(log, l => l.Contains("onPlaceActionResult.Done.CanRetry: Failed to place") && l.Contains("[12.50,-30.25,0.00]"));
                Assert.Contains(log, l => l.Contains($"PlaceObjectOnGroundAtPose(12.50,-30.25,0.00) attempt {attempt + 1}"));
                Assert.Empty(rig.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PlaceObjectAt));
            });
        // attempt 3 is the last: CannotRetry (max 3) and the failure tail
        Case(0x04000008, 3, true, (b, rig, log) =>
        {
            Assert.Contains(log, l => l.Contains("onPlaceActionResult.CannotRetry:") && l.Contains("(attempt=3/3) (carrying=yes)"));
            var entry = Assert.Single(Assert.Single(rig.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PlaceObjectAt)).Value);
            Assert.Equal(12.5, entry.Pose.Translation.X); Assert.Equal(-30.25, entry.Pose.Translation.Y);               // the candidate pose, not the cube's
        });
        // not carrying the target: CannotRetry with carrying=no, whatever the attempt
        Case(0x04000008, 1, false, (b, rig, log) =>
        {
            Assert.Contains(log, l => l.Contains("(attempt=1/3) (carrying=no)"));
            Assert.Single(rig.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PlaceObjectAt));
        });
        // categories 1 (running), 2 (cancelled) and 3 (abort): NoRetryAllowed and the tail
        foreach (uint r in new[] { 0x01000000u, 0x02000000u, 0x03000011u })
            Case(r, 1, true, (b, rig, log) =>
            {
                Assert.Contains(log, l => l.Contains("onPlaceActionResult.NoRetryAllowed: Failed to place (no retry allowed by action)"));
                Assert.Single(rig.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PlaceObjectAt));
            });
    }

    // ------------------------------------------------------------------------------------------- M15-022: the action

    /// <summary>
    /// M15-022 (2.6, 2.7, 2.8): Init sends the PlaceObjectOnGround message (0x44) FIRST and then calls StopAllMotors (0x00554894); without a carried object it fails with 0x03000011 and still runs StopAllMotors.
    /// CheckIfDone never reads DockingComponent+5 or the carry state: with the IS_PICKING_OR_PLACING bit never raised the action stays RUNNING until the IAction timeout (30.0f, 0x41F00000) and fails with
    /// 0x03000018, even though the robot's BLOCK_PLACED result arrived (and released the carried object: HandlePickAndPlaceResult 0x00533780 is the only release).
    /// </summary>
    [Fact]
    public async Task M15_022_InitSendsTheMessageThenStopsMotorsAndTheActionWaitsForTheStatusGate()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        double clock = 100; rig.M.ClockSec = () => clock;
        var notCarrying = new PlaceObjectOnGroundAction(rig.M);
        Assert.Equal(ActionResult.NotCarryingObjectAbort, await notCarrying.RunAsync(default));
        Assert.Contains(notCarrying.Trace, l => l.Contains("CheckPreconditions.NotCarryingObject"));
        rig.Pump();
        Assert.Contains(rig.Sent, m => m is StopAllMotors);
        Assert.DoesNotContain(rig.Sent, m => m is PlaceObjectOnGround);
        rig.Sent.Clear();
        var obj = Cube(rig, 7, At(100, 0, 22));
        rig.M.Docking.Carrying.SetCarrying(7);
        var action = new PlaceObjectOnGroundAction(rig.M) { SubActions = new FixedSubActions(ActionResult.Success) };
        var task = action.RunAsync(default);
        SpinUntil(() => rig.Sent.Any(m => m is StopAllMotors), () => rig.Pump());
        Assert.True(rig.Sent.FindIndex(m => m is PlaceObjectOnGround) < rig.Sent.FindIndex(m => m is StopAllMotors));       // the message, then StopAllMotors
        Assert.NotNull(action.VerifyAction);
        Assert.Equal(7u, action.VerifyAction!.ObjectId);
        SpinUntil(() => !rig.M.Docking.Carrying.IsCarryingObject, () => rig.Pump());           // the robot's BLOCK_PLACED result released it
        Assert.False(rig.M.Docking.DockingSuccessByte == false);                              // the success byte is the message's (stored by HandlePickAndPlaceResult), nothing here reads it
        SignalTestContext.StepContinuation();
        Assert.False(task.IsCompleted);                                                       // not carrying any more, status gate never opened: still RUNNING
        clock = 100 + 29.9;
        SignalTestContext.StepContinuation();
        Assert.False(task.IsCompleted);
        clock = 100 + 30.0;                                                                   // now >= start + 30.0f
        SignalTestContext.Run(task);
        Assert.Equal(ActionResult.Timeout, await task);
        Assert.Equal(0x03000018u, (uint)ActionResult.Timeout);
    }

    private sealed class FixedSubActions : IDockSubActionExecutor
    {
        private readonly ActionResult _r;
        public int Calls;
        public FixedSubActions(ActionResult r) => _r = r;
        public Task<ActionResult> RunAsync(DockSubAction action, List<string> trace, CancellationToken cancel) { Calls++; return Task.FromResult(_r); }
    }

    private static void Firmware(Rig rig, ref int raisedAt, ref bool cleared, Func<bool>? latched = null)
    {
        // the robot's side of a put-down: IS_PICKING_OR_PLACING is reported while it lowers the lift, then it clears
        if (rig.Sent.Any(m => m is PlaceObjectOnGround))
        {
            if (raisedAt == 0) { rig.State(flags: (uint)RobotStatusFlag.IsPickingOrPlacing); raisedAt = 1; rig.Cube = new Pose3d(Mat3.AboutZ(rig.Angle), new Vec3(rig.X + 100 * Math.Cos(rig.Angle), rig.Y + 100 * Math.Sin(rig.Angle), 22)); }
            else if (!cleared && (latched?.Invoke() ?? true)) { rig.State(flags: (uint)(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos)); cleared = true; }
        else if (!cleared) rig.State(flags: (uint)RobotStatusFlag.IsPickingOrPlacing);                  // a camera frame (rig.Frame) reports a fresh state: keep reporting the bit while the lift lowers
        }
    }

    /// <summary>
    /// M15-022 (2.7): the status gate. With the bit raised the action latches (+0x84) and stays RUNNING; with the bit clear and the latch set it runs its verify action and ITS result is the action's: a failure
    /// logs "PlaceObjectOnGroundAction.CheckIfDone.FaceAndVerifyFailed" and is returned (the object clear, ClearLocatedObjectByIDInCurOrigin, is MISSING). The result never depends on whether the cube was
    /// released.
    /// </summary>
    [Fact]
    public async Task M15_022_TheActionsResultIsTheVerifyActionsAfterTheStatusGate()
    {
        foreach (var verify in new[] { ActionResult.Success, ActionResult.VisualObservationFailed })
        {
            using var signals_rig = SignalTestContext.Install();
            using var rig = new Rig();
            Cube(rig, 7, At(100, 0, 22));
            rig.M.Docking.Carrying.SetCarrying(7);
            var sub = new FixedSubActions(verify);
            var action = new PlaceObjectOnGroundAction(rig.M) { SubActions = sub };
            int raised = 0; bool cleared = false;
            var task = action.RunAsync(default);
            SignalTestContext.Run(task, () => { rig.Pump(); Firmware(rig, ref raised, ref cleared, () => action.StatusLatched); });
            Assert.True(task.IsCompleted, $"{verify}: raised={raised} cleared={cleared} latched={action.StatusLatched} sent={string.Join(",", rig.Sent.Select(m => m.GetType().Name).Distinct())}");
            Assert.Equal(verify, task.Result);
            Assert.True(action.StatusLatched);
            Assert.Equal(1, sub.Calls);
            Assert.Equal(verify != ActionResult.Success, action.Trace.Any(l => l.Contains("FaceAndVerifyFailed")));
        }
    }

    private sealed class ObjectPositionStrategy : ReactionTriggerStrategy
    {
        public override ReactionTrigger Trigger => ReactionTrigger.ObjectPositionUpdated;
        public override string Basis => "test";
        public override bool ShouldResumeLast => false;
        public override bool CanInterruptOtherTriggeredBehavior => false;
        public override bool CanInterruptSelf => false;
        protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior) => false;
        public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }
    }

    private sealed class IdleBehavior : IBehavior
    {
        public string Id => "react"; public string Class => "Idle";
        public bool IsRunnable(BehaviorContext c) => true;
        public double EvaluateScore(BehaviorContext c) => 1;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) => Task.CompletedTask;
        public bool Update(BehaviorContext c, double nowMs) => false;
        public void Stop(BehaviorStopReason r) { }
    }

    /// <summary>
    /// M15-022 (2.6 step 6, 0x005548C6; destructor 0x005546D0 -> 0x00554708): with result 0 Init takes DisableReactionsWithLock("placeOnGroundAction", table 0x00C55A21, true): of the 21 triggers
    /// only index 8 (ObjectPositionUpdated) is set; the destructor removes it. With a failed Init (not carrying) the lock is never taken.
    /// </summary>
    [Fact]
    public async Task M15_022_InitTakesTheReactionLockOnObjectPositionUpdatedAndTheEndRemovesIt()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        var ctx = Ctx(rig);
        using var manager = new BehaviorManager(ctx);
        manager.AddReaction(new ObjectPositionStrategy(), new IdleBehavior());
        manager.AddReaction(new AlwaysOffStrategy(ReactionTrigger.CubeMoved), new IdleBehavior());
        Assert.Equal(21, PlaceObjectOnGroundAction.ReactionLockTable.ToMask().Length);
        Assert.Equal(new[] { 8 }, PlaceObjectOnGroundAction.ReactionLockTable.ToMask().Select((v, i) => (v, i)).Where(p => p.v).Select(p => p.i).ToArray());
        Cube(rig, 7, At(100, 0, 22));
        rig.M.Docking.Carrying.SetCarrying(7);
        var action = new PlaceObjectOnGroundAction(rig.M) { ReactionLocks = manager, SubActions = new FixedSubActions(ActionResult.Success) };
        var task = action.RunAsync(default);
        Assert.True(manager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, "placeOnGroundAction"));
        Assert.False(manager.HasDisableLock(ReactionTrigger.CubeMoved, "placeOnGroundAction"));
        int raised = 0; bool cleared = false;
        SignalTestContext.Run(task, () => { rig.Pump(); Firmware(rig, ref raised, ref cleared, () => action.StatusLatched); });
        Assert.Equal(ActionResult.Success, await task);
        Assert.False(manager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, "placeOnGroundAction"));
        // not carrying: Init fails with 0x03000011 and never takes the lock
        rig.M.Docking.Carrying.UnsetCarrying();
        var failing = new PlaceObjectOnGroundAction(rig.M) { ReactionLocks = manager };
        Assert.Equal(ActionResult.NotCarryingObjectAbort, await failing.RunAsync(default));
        Assert.False(manager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, "placeOnGroundAction"));
    }

    private sealed class AlwaysOffStrategy : ReactionTriggerStrategy
    {
        private readonly ReactionTrigger _t;
        public AlwaysOffStrategy(ReactionTrigger t) => _t = t;
        public override ReactionTrigger Trigger => _t;
        public override string Basis => "test";
        public override bool ShouldResumeLast => false;
        public override bool CanInterruptOtherTriggeredBehavior => false;
        public override bool CanInterruptSelf => false;
        protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior) => false;
        public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }
    }

    /// <summary>
    /// M15-022 (2.3, 2.3a, 2.4): TryToPlaceAt's DriveToPlaceCarriedObjectAction(placeOnGround = true, +0x178 = 0, useManualSpeed = 0, +0x179 = 1, padding 10.0f = 0x41200000). A placement goal
    /// that is not free (another cube on the candidate, +0x179 set) fails with RETRY 0x04000008, and the compound ends with that sub-action's result (a RETRY category only retries when the compound's own
    /// RetriesRemain is 1; the default byte is 0, 0x0053FDD8, so it never does): the PlaceObjectOnGround message is never sent.
    /// </summary>
    [Fact]
    public void M15_022_ABlockedPlacementGoalFailsWithRetry()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        Cube(rig, 7, At(300, 0, 22));
        var blocker = Cube(rig, 20, At(0, 0, 22), ObjectType.Block_LIGHTCUBE2);
        rig.M.Docking.Carrying.SetCarrying(7);
        var drive = new DriveToPlaceCarriedObjectAction(rig.M, At(0, 0), true, false, false, true, BeaconFloorGeometry.Ten);
        Assert.False(drive.FlagAt0x178); Assert.True(drive.FlagAt0x179); Assert.Equal(PreActionType.PlaceOnGround, drive.ActionType);
        Assert.Equal(0x41200000u, B((float)drive.PaddingAt0x17C));
        Assert.Equal(ActionResult.PlacementGoalNotFree, drive.CheckIfDone(ActionResult.Running));
        Assert.Equal(0x04000008u, (uint)ActionResult.PlacementGoalNotFree);
        blocker.Pose = At(300, 300, 22);
        Assert.Equal(ActionResult.Running, drive.CheckIfDone(ActionResult.Running));
        Assert.DoesNotContain(rig.Sent, m => m is PlaceObjectOnGround);
    }

    // ------------------------------------------------------------------------------------------- the placement end to end

    /// <summary>
    /// The same behaviour through the live entry: the BringCubeToBeacon the shipped binding builds (FreeplayStack.Bound, "Hiking_BringCubeToBeacon", cooldown 45), started and ticked by the
    /// BehaviorManager (SwitchToBehaviorBase, Update): pick up, place on the floor, FireEmotionEvents.
    /// </summary>
    [Fact]
    public void TheShippedBindingPlacesTheCubeThroughTheBehaviorManager()
    {
        if (!NeedsAssets()) return;
        var obb = ObbRoot();
        if (obb is null)
        {
            if (Environment.GetEnvironmentVariable("COZMO_TESTS_WITHOUT_ASSETS") == "1") return;
            throw new Xunit.Sdk.XunitException("re-analysis/obb (the shipped config and animation assets) is missing, so the live-entry test cannot run; provide it or set COZMO_TESTS_WITHOUT_ASSETS=1");
        }
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.Robot.Animations.ManualTicking = true;
        rig.Robot.Animations.ClockMs = () => rig.Clock.NowMs;
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        double clock = 0;
        var ctx = Ctx(rig);
        using var stack = FreeplayStack.Create(obb, rig.Robot, ctx, () => clock, rig.Vision, rig.M, withReactions: false, random: new Random(1));
        var bring = Assert.IsType<BringCubeToBeaconBehavior>(stack.Bound["Hiking_BringCubeToBeacon"]);
        bring.ActionTaskRunner = SignalTestContext.Schedule;
        bring.WorkPosted += signals_rig.Notify;
        Assert.Equal(45.0, bring.RecentFailureCooldownSec);
        Assert.Equal(5.0, Assert.IsType<BringCubeToBeaconBehavior>(stack.Bound["SparksBringCubeToBeacon"]).RecentFailureCooldownSec);
        rig.Cube = At(330, -20, 22);
        Assert.Single(rig.Frame().Objects);
        rig.M.Whiteboard.AddBeacon(At(0, 0), 175);
        stack.Manager.Activity = null;                                                       // the activity chooser is not under test: the manager ticks only the behaviour it was switched to
        // Placement's running checks resume only after an explicit modeled firmware pump.
        var firmwareTicks = new System.Collections.Concurrent.ConcurrentQueue<TaskCompletionSource>();
        rig.M.Wait = (_, cancel) =>
        {
            if (cancel.IsCancellationRequested) return Task.FromCanceled(cancel);
            var tick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            firmwareTicks.Enqueue(tick);
            signals_rig.MarkFirmwareWorkPending();
            return tick.Task;
        };
        Assert.True(stack.Manager.SwitchToBehaviorBase(bring, 0).GetAwaiter().GetResult());
        int raised = 0; bool cleared = false;

        while (stack.Manager.IsRunning(bring))
        {
            clock += 0.033;
            stack.Manager.Update(clock * 1000, clock);
            rig.Pump();
            while (firmwareTicks.TryDequeue(out var tick)) tick.TrySetResult();
            if (!rig.M.Docking.Carrying.IsCarryingObject) rig.Frame(); Firmware(rig, ref raised, ref cleared, () => bring.LastPlaceAction?.StatusLatched ?? false);
            SignalTestContext.AdvanceBehavior(bring);
        }
        Assert.False(stack.Manager.IsRunning(bring), string.Join(" | ", bring.Trace));
        Assert.Contains(bring.Trace, l => l.Contains("onPlaceActionResult.Done: Successfully placed cube"));
        Assert.Single(rig.Sent.OfType<PlaceObjectOnGround>());
        Assert.False(rig.M.Docking.Carrying.IsCarryingObject);
    }

    // ------------------------------------------------------------------------------------------- the candidate filter (M15-020, M15-024)

    /// <summary>
    /// IsRunnableInternal 0x005DF1AA..0x005DF296 builds the candidate set from the word table 0x00C6D6A0 = {0, 3, 1, 2} (the first two words: PickUpObject and RollOrPopAWheelie) and keeps a cube unless
    /// <c>DidFailToUse(id, set, [this+0x130], its pose, 20.0f, Radians pi/8)</c>: a failed floor placement (PlaceObjectAt) or stack-on does NOT exclude it; a pick-up failure recorded for that cube within 20 mm and pi/8 of its
    /// pose does (the call passes the cube's id, so another object's failures are not consulted); farther than 20 mm, or turned more than pi/8, does not; the cooldown (strict) ends it.
    /// </summary>
    [Fact]
    public void M15_020_TheCandidateFilterIsPickUpAndRollFailuresMatchedByPose()
    {
        if (!NeedsAssets()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        double clock = 100; rig.M.ClockSec = () => clock;
        var ctx = Ctx(rig);
        rig.Cube = At(300, 0, 22);
        Assert.Single(rig.Frame().Objects);
        var obj = rig.M.World.LocatedObjects.Single();
        rig.M.Whiteboard.AddBeacon(At(0, 0), 175);
        var bring = NewBring(rig);
        void Reset() { rig.M.Whiteboard.OnRobotDelocalized(); rig.M.Whiteboard.AddBeacon(At(0, 0), 175); }       // delocalization drops the beacons too
        Assert.True(Runnable(bring, ctx));
        var pose = obj.Pose;
        rig.M.Whiteboard.SetFailedToUse(obj, ObjectActionFailure.PlaceObjectAt, pose);
        rig.M.Whiteboard.SetFailedToUse(obj, ObjectActionFailure.StackOnObject, pose);
        Assert.True(Runnable(bring, ctx));                                                  // neither kind is in the set {0, 3}
        rig.M.Whiteboard.SetFailedToUse(obj.ObjectId, ObjectActionFailure.PickUpObject, new Pose3d(pose.Rotation, pose.Translation + new Vec3(30, 0, 0)));
        Assert.True(Runnable(bring, ctx));                                                  // 30 mm away: outside 20.0f
        Reset();
        rig.M.Whiteboard.SetFailedToUse(obj.ObjectId, ObjectActionFailure.PickUpObject, new Pose3d(Mat3.AboutZ(1.0) * pose.Rotation, pose.Translation + new Vec3(5, 0, 0)));
        Assert.True(Runnable(bring, ctx));                                                  // 1.0 rad away: outside pi/8 = 0.3927
        Reset();
        rig.M.Whiteboard.SetFailedToUse(99, ObjectActionFailure.PickUpObject, new Pose3d(pose.Rotation, pose.Translation));
        Assert.True(Runnable(bring, ctx));                                                  // the call passes the candidate's id (0x005DF24E): another object's failure is not consulted
        Reset();
        rig.M.Whiteboard.SetFailedToUse(obj.ObjectId, ObjectActionFailure.PickUpObject, new Pose3d(Mat3.AboutZ(0.1) * pose.Rotation, pose.Translation + new Vec3(5, 0, 0)));
        Assert.False(Runnable(bring, ctx));                                                 // 5 mm and 0.1 rad: within 20.0f and pi/8 of the cube's pose
        clock = 100 + 44.9;
        Assert.False(Runnable(bring, ctx));
        clock = 100 + 45.0;
        Assert.True(Runnable(bring, ctx));                                                  // strictly younger than the cooldown only
        clock = 100;
        Reset();
        rig.M.Whiteboard.SetFailedToUse(obj, ObjectActionFailure.RollOrPopAWheelie, pose);
        Assert.False(Runnable(bring, ctx));                                                 // kind 3 is in the set
        // after a failed floor placement through the live callback the same cube is still a candidate
        Reset();
        ctx.Needs = new NeedsManager(() => 0);
        bring.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        bring.PlaceCompleted((ActionResult)0x04000008u, At(0, 0), 3);
        Assert.Single(rig.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PlaceObjectAt));
        Assert.True(Runnable(bring, ctx));
    }

    /// <summary>
    /// FindUsableCubesOutOfBeacons 0x0056AE58 and AreAllCubesInBeacons 0x0056B450 (M15-020): no beacon is empty/false; a carried cube makes the usable set the carried cube (0x0056AEA6..0x0056AEF2) and the
    /// all-in-beacons answer false (0x0056B476); otherwise a cube is usable when CanPickUpObject accepts it and it is in no beacon by the 3-D IsLocWithinBeacon (z counts).
    /// CarryingComponent::IsCarryingObject(id) 0x00633F88 is also true for the id resting on the carried one ([+0x14]).
    /// </summary>
    [Fact]
    public void M15_020_FindUsableAndAreAllFollowTheEngine()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        var wb = rig.M.Whiteboard;
        wb.CanPickUpObject = o => o.ObjectId != 12;
        Cube(rig, 10, At(10, 0, 22));                                                       // in a 30 mm beacon at the origin (3-D d2 = 584 <= 900)
        Cube(rig, 11, At(200, 0, 22));                                                      // outside
        Cube(rig, 12, At(300, 0, 22));                                                      // CanPickUpObject false
        Cube(rig, 13, At(0, 0, 80), ObjectType.Block_LIGHTCUBE2);                           // planar-inside but 80 high: outside a 30 mm beacon in 3-D
        Assert.Empty(wb.FindUsableCubesOutOfBeacons());
        Assert.False(wb.AreAllCubesInBeacons());                                            // no beacon
        wb.AddBeacon(At(0, 0, 0), 30);
        Assert.Equal(new uint[] { 11, 13 }, wb.FindUsableCubesOutOfBeacons().Select(o => o.ObjectId).ToArray());
        Assert.False(wb.AreAllCubesInBeacons());
        rig.M.Docking.Carrying.SetCarrying(11);
        Assert.Equal(new uint[] { 11 }, wb.FindUsableCubesOutOfBeacons().Select(o => o.ObjectId).ToArray());   // the carried cube
        Assert.False(wb.AreAllCubesInBeacons());
        Assert.True(rig.M.Docking.Carrying.IsCarryingObjectId(11));
        Assert.False(rig.M.Docking.Carrying.IsCarryingObjectId(13));
        rig.M.Docking.Carrying.AttachToLift(11, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1)[0], At(0, 0), 13, At(0, 0));
        Assert.True(rig.M.Docking.Carrying.IsCarryingObjectId(13));                         // [+0x14], the on-top id (0x00633F88)
        Assert.False(rig.M.Docking.Carrying.IsCarrying(13));                                // the stack's own strict test is unchanged
        Assert.False(rig.M.Docking.Carrying.IsCarryingObjectId(10));
        // the angle difference: identical rotations are 0 (GetAngleDiffFrom 0x0084A694); acos(2c*c - 1) of the quaternion dot otherwise
        Assert.Equal(0u, B(EngineRadians32.AngleDiff(Mat3.AboutZ(0.3), Mat3.AboutZ(0.3))));
        Assert.Equal(0.3f, EngineRadians32.AngleDiff(Mat3.AboutZ(0.3), Mat3.AboutZ(0.0)), 5);
        Assert.Equal(0.3f, EngineRadians32.AngleDiff(Mat3.AboutZ(0.0), Mat3.AboutZ(0.3)), 5);
    }

    /// <summary>
    /// CanPickUpObject 0x0063C7F0: CanInteractWithObjectHelper and then NOT IsPoseTooHigh(pose wrt robot, 2.0f, 15.0f, 0.5f) (<c>D*2 + 15 + 1e-5 &lt; D*0.5 + z</c>, D = 44): a cube on the ground is
    /// pickable, one 100 mm up is too high (103 &lt; 122), one 70 mm up is not (103 &gt;= 92).
    /// </summary>
    [Fact]
    public void M15_020_CanPickUpObjectRejectsACubeThatIsTooHigh()
    {
        if (!NeedsAssets()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.Cube = At(300, 0, 22);
        Assert.Single(rig.Frame().Objects);
        var obj = rig.M.World.LocatedObjects.Single();
        Assert.True(rig.M.Docking.CanPickUpObject(obj));
        obj.Pose = At(300, 0, 100);
        Assert.False(rig.M.Docking.CanPickUpObject(obj));
        obj.Pose = At(300, 0, 70);
        Assert.True(rig.M.Docking.CanPickUpObject(obj));
    }

    /// <summary>
    /// PlaceObjectOnGroundAction::Init 0x005548C6 takes the lock for EVERY action: a bare action built by any caller (PutDown, FistBump, Motion's lift-height-32 path, ManipTool) takes it through the
    /// ManipulationSystem's manager (FreeplayStack.Create wires it); with none attached the lock is reported MISSING once and not taken.
    /// </summary>
    [Fact]
    public async Task M15_022_ABareActionTakesTheLockThroughTheManipulationSystemsManager()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        var ctx = Ctx(rig);
        using var manager = new BehaviorManager(ctx);
        manager.AddReaction(new ObjectPositionStrategy(), new IdleBehavior());
        Cube(rig, 7, At(100, 0, 22));
        var missing = new List<string>();
        void OnMissing(string s) => missing.Add(s);
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += OnMissing;
        try
        {
            rig.M.Docking.Carrying.SetCarrying(7);
            var bare = new PlaceObjectOnGroundAction(rig.M) { SubActions = new FixedSubActions(ActionResult.Success) };
            var t1 = bare.RunAsync(default);
            Assert.False(manager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, "placeOnGroundAction"));      // no manager attached: not taken
            Assert.Contains(missing, m => m.Contains("placeOnGroundAction"));
            int raised1 = 0; bool cleared1 = false;

            SignalTestContext.Run(t1, () => { rig.Pump(); Firmware(rig, ref raised1, ref cleared1, () => bare.StatusLatched); });
            Assert.Equal(ActionResult.Success, await t1);
        }
        finally { SteppedBehavior.MissingReported -= OnMissing; }
        rig.Sent.Clear();
        rig.M.Docking.Carrying.SetCarrying(7);
        rig.M.ReactionLocks = manager;
        var action = new PlaceObjectOnGroundAction(rig.M) { SubActions = new FixedSubActions(ActionResult.Success) };
        var task = action.RunAsync(default);
        Assert.True(manager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, "placeOnGroundAction"));
        int raised = 0; bool cleared = false;
        SignalTestContext.Run(task, () => { rig.Pump(); Firmware(rig, ref raised, ref cleared, () => action.StatusLatched); });
        Assert.Equal(ActionResult.Success, await task);
        Assert.False(manager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, "placeOnGroundAction"));
    }

    /// <summary>
    /// M15-022: the verify child is the engine's TurnTowardsObjectAction (Init 0x0054A1F0): an object the world does not hold is 0x03000004 (0x0054A44A); with the marker code ANY_CODE the pose is the closest
    /// marker's (GetClosestMarkerPose, 0x0054A27C); a code the object has is chosen by the nearest such marker; a code it lacks is 0x03000002 (the base value, 0x0054A2F6 / 0x0054A582). The action's
    /// maxTurn is Radians(0) and its visual-verify byte +0x192 is clear, so it ends with the TurnTowardsPoseAction's result (CheckIfDone 0x0054ADF2..0x0054AF60): Success here.
    /// </summary>
    [Fact]
    public async Task M15_022_TheVerifyChildIsTheEnginesTurnTowardsObjectAction()
    {
        if (!NeedsAssets()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.Cube = At(300, 0, 22);
        Assert.Single(rig.Frame().Objects);
        var obj = rig.M.World.LocatedObjects.Single();
        var exec = new TurnTowardsObjectExecutor(rig.M);
        var trace = new List<string>();
        Assert.Equal(0x03000004u, (uint)await exec.RunAsync(new TurnTowardsObject(999, DockTurnCode.Any), trace, default));
        Assert.Equal(ActionResult.Success, await exec.RunAsync(new TurnTowardsObject(obj.ObjectId, DockTurnCode.Any), trace, default));
        short code = unchecked((short)(int)obj.Markers[0].Code);
        Assert.Equal(ActionResult.Success, await exec.RunAsync(new TurnTowardsObject(obj.ObjectId, new DockTurnCode(code)), trace, default));
        Assert.Equal(0x03000002u, (uint)await exec.RunAsync(new TurnTowardsObject(obj.ObjectId, new DockTurnCode(0x7000)), trace, default));
        Assert.Equal(0.0, new TurnTowardsObject(obj.ObjectId, DockTurnCode.Any).RadiansArgument);
        Assert.True(rig.FaceTurns >= 2);                                                    // the turn ran for the two successful calls
    }

    /// <summary>
    /// ObservableObject::IsPoseTooHigh 0x00877954 is binary32: <c>(D*f1 + f2) + 1e-5f &lt; D*f3 + z</c> with each operation rounded to f32 (vmul.f32 0x00877990/0x00877994, vadd.f32 0x00877998/0x008779AA/0x008779AE,
    /// literal 0x3727C5AC at 0x008779C8). For D = 44, f = (1, 15, 0.5) and z = 37 + 3*2^-18 the f32 sum on the left is 59.0000114 and the right 22 + z = 59.0000114: NOT less, so not too high; a double
    /// evaluation (59.00001 &lt; 59.0000114) says too high. The width is pinned by that case, for both CanStackOnTopOfObject's (1, 15, 0.5) and CanPickUpObject's (2, 15, 0.5) shapes.
    /// </summary>
    [Fact]
    public void M12_012_IsPoseTooHighIsBinary32()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        var cube = Cube(rig, 7, At(0, 0, 22), locate: false);
        double z = 37.0 + 3.0 * Math.Pow(2, -18);
        Assert.True(44.0 * 1.0 + 15.0 + 1e-5 < 44.0 * 0.5 + z);                                       // the double evaluation differs
        Assert.False(CubeGeometry.IsPoseTooHigh(cube, At(0, 0, z), 1.0f, 15.0f, 0.5f));                // binary32: equal, not less
        Assert.True(CubeGeometry.IsPoseTooHigh(cube, At(0, 0, z + 1e-4), 1.0f, 15.0f, 0.5f));           // one more ulp or so is
        Assert.False(CubeGeometry.IsPoseTooHigh(cube, At(0, 0, 70), 2.0f, 15.0f, 0.5f));               // 103 + 1e-5 < 92 no
        Assert.True(CubeGeometry.IsPoseTooHigh(cube, At(0, 0, 100), 2.0f, 15.0f, 0.5f));               // 103 + 1e-5 < 122
    }

    /// <summary>
    /// TurnTowardsPoseAction::GetAbsoluteHeadAngleToLookAtPose 0x0054B428..0x0054B564, binary32 bits from the emulation of the disassembly (emulate_head_angle.py; the same reading of the same function, with
    /// atan2f from Python's libm).
    /// </summary>
    [Theory]
    [InlineData(100, 0, 22, 0xBD9EA3DFu)]
    [InlineData(200, 30, 80, 0x3EC94056u)]
    [InlineData(50, 0, 0, 0xBF0103C1u)]
    [InlineData(400, 0, -10, 0xBD93A0B7u)]
    [InlineData(280, 0, 49, 0x3D975156u)]
    public void M13_021_TheAbsoluteHeadAngleToLookAtAPoseIsTheEnginesBinary32Arithmetic(double x, double y, double z, uint expected)
    {
        Assert.Equal(expected, B((float)TurnTowardsPoseCompound.AbsoluteHeadAngleToLookAtPose(new Vec3(x, y, z))));
    }

    private static Pose3d Near(Rig rig, uint id) => rig.M.World.GetLocatedObjectById(id)!.Pose;

    /// <summary>
    /// InitInternal 0x005DF348: carrying (<c>[[robot+0x284]+8] != -1</c>) the target is <c>vector[0].id</c> and TransitionToObjectPickedUp runs DIRECTLY (0x005DF372): the behaviour is RUNNABLE while carrying
    /// (IsRunnableInternal has no carrying test; FindUsableCubesOutOfBeacons answers with the carried cube) and resumes at the placement phase with no pick-up helper; not carrying, TransitionToPickUpObject(1)
    /// (0x005DF37C) picks the candidate nearest the robot by f32 (x*x + y*y) + z*z (0x005DFB3A..0x005DFB5E; no sqrt) and picks it up. With no candidate the "NoCandidates" error and InitInternal's non-zero
    /// result (no action started and the beacon not stamped now, 0x005DF382..0x005DF3DA); a NoFreePoses stamp made in the call itself is zero.
    /// </summary>
    [Fact]
    public void M15_020_InitInternalResumesAtThePlacementPhaseWhileCarryingAndPicksTheNearestOtherwise()
    {
        if (!NeedsAssets()) return;
        // not carrying: two usable cubes, the one at (250, 90) is nearer (72500 against 109000) though the other is listed first by id
        using (var rig = new Rig())
        {
            rig.Cube = At(330, -20, 22);
            rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, At(250, 90, 22)));
            Assert.Equal(2, rig.Frame().Objects.Count);
            var far = rig.M.World.LocatedObjects.OrderByDescending(o => o.Pose.Translation.X).First();
            var near = rig.M.World.LocatedObjects.OrderBy(o => o.Pose.Translation.X).First();
            rig.M.Whiteboard.AddBeacon(At(0, -300), 175);
            var ctx = Ctx(rig); ctx.Needs = new NeedsManager(() => 0);
            var bring = NewBring(rig);
            Assert.True(Runnable(bring, ctx));
            bring.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
            Assert.Equal(near.ObjectId, bring.Candidate);
            Assert.NotEqual(far.ObjectId, bring.Candidate);
            Assert.Contains(bring.Trace, l => l.Contains("TransitionToPickUpObject.Selected: Going to pick up '" + near.ObjectId + "'"));
            Assert.Contains(bring.Trace, l => l.StartsWith("start DriveToPickupObject("));
            Assert.False(bring.InitFailed);
            bring.Stop(BehaviorStopReason.Cancelled);
        }
        // carrying the target: runnable, resumes at the placement phase, no pick-up
        using (var rig = new Rig())
        {
            rig.Cube = At(330, -20, 22);
            Assert.Single(rig.Frame().Objects);
            var target = rig.M.World.LocatedObjects.Single();
            rig.M.Whiteboard.AddBeacon(At(0, 0), 175);
            rig.M.Docking.Carrying.SetCarrying(target.ObjectId);
            var ctx = Ctx(rig); ctx.Needs = new NeedsManager(() => 0);
            var bring = NewBring(rig);
            bring.IsUnlocked = _ => false;
            Assert.True(Runnable(bring, ctx));                                                  // no carrying test in IsRunnableInternal
            bring.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
            Assert.Equal(target.ObjectId, bring.Candidate);
            Assert.DoesNotContain(bring.Trace, l => l.StartsWith("start DriveToPickupObject("));
            Assert.Contains(bring.Trace, l => l == "needs action PickupCube");
            Assert.Contains(bring.Trace, l => l.Contains("Decided to place '" + target.ObjectId + "' on the floor"));
            Assert.False(bring.InitFailed);                                                      // an action (the placement) was started
            bring.Stop(BehaviorStopReason.Cancelled);
        }
        // no candidate: the NoCandidates error, nothing started, InitFailed
        using (var rig = new Rig())
        {
            double clock = 500; rig.M.ClockSec = () => clock;
            var ctx = Ctx(rig);
            var bring = NewBring(rig);
            bring.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
            Assert.Contains(bring.Trace, l => l.Contains("TransitionToPickUpObject.NoCandidates: Can't run with no selected objects"));
            Assert.True(bring.InitFailed);
        }
        // carrying with a beacon too small for a free pose: NoFreePoses stamps the beacon in this very call, so InitInternal answers 0
        using (var rig = new Rig())
        {
            double clock = 500; rig.M.ClockSec = () => clock;
            rig.Cube = At(330, -20, 22);
            rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, At(255, 90, 22)));
            Assert.Equal(2, rig.Frame().Objects.Count);
            var target = rig.M.World.LocatedObjects.OrderByDescending(o => o.Pose.Translation.X).First();
            rig.M.Whiteboard.AddBeacon(At(250, 90), 30);
            rig.M.Docking.Carrying.SetCarrying(target.ObjectId);
            var ctx = Ctx(rig); ctx.Needs = new NeedsManager(() => 0);
            var bring = NewBring(rig);
            bring.IsUnlocked = _ => false;
            bring.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
            Assert.Contains(bring.Trace, l => l.Contains("TransitionToObjectPickedUp.NoFreePoses"));
            Assert.False(bring.InitFailed);
        }
    }

    /// <summary>
    /// The pick-up completion lambda 0x005E0FD8 by result category: 0 logs "Picked up" and runs TransitionToObjectPickedUp; 4 with the TARGET carried (strict [+8]) is "RetryOk" and goes on to
    /// TransitionToObjectPickedUp; 4 otherwise with attempt &lt;= 2 is "RetryMaybe ... (n tries out of 3)" and TransitionToPickUpObject(attempt + 1), which on the already-set target logs ".Retry"
    /// "Trying to pick up '%d' again" (0x005DF8E6) and starts the action again; 4 with three attempts spent is ".Fail"; 3 is ".NoRetry"; both failures end in SetFailedToUse(obj, PickUpObject = 0) with the
    /// cube's own pose; categories 1 and 2 do nothing.
    /// </summary>
    [Fact]
    public void M15_020_ThePickUpCompletionLambdaActsPerResultCategory()
    {
        if (!NeedsAssets()) return;
        void Case(uint result, int attempt, bool carrying, Action<BringCubeToBeaconBehavior, Rig, string[], uint> check)
        {
            using var signals_rig = SignalTestContext.Install();
            using var rig = new Rig();
            var ctx = Ctx(rig); ctx.Needs = new NeedsManager(() => 0);
            var (bring, target) = StartedBring(rig, ctx);
            Assert.Equal(target, bring.Candidate);
            if (carrying) rig.M.Docking.Carrying.SetCarrying(target);
            bring.IsUnlocked = _ => false;
            int before = bring.Trace.Count;
            bring.PickUpCompleted((ActionResult)result, attempt);
            check(bring, rig, bring.Trace.Skip(before).ToArray(), target);
            bring.Stop(BehaviorStopReason.Cancelled);
        }
        Case(0, 1, true, (b, rig, log, t) =>
        {
            Assert.Contains(log, l => l.Contains($"onPickUpActionResult.Done: Picked up '{t}'"));
            Assert.Contains(log, l => l == "needs action PickupCube");                       // TransitionToObjectPickedUp ran
        });
        Case(0x04000001, 1, true, (b, rig, log, t) =>
        {
            Assert.Contains(log, l => l.Contains($"onPickUpActionResult.RetryOk: We do have '{t}' picked up, so pretend we are fine"));
            Assert.Contains(log, l => l == "needs action PickupCube");
        });
        foreach (int attempt in new[] { 1, 2 })
            Case(0x04000001, attempt, false, (b, rig, log, t) =>
            {
                Assert.Contains(log, l => l.Contains($"onPickUpActionResult.RetryMaybe: Let's try to pick up '{t}' again ({attempt} tries out of 3)"));
                Assert.Contains(log, l => l.Contains($"TransitionToPickUpObject.Retry: Trying to pick up '{t}' again"));
                Assert.Contains(log, l => l.StartsWith($"start DriveToPickupObject({t}) attempt {attempt + 1}"));
                Assert.Empty(rig.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PickUpObject));
            });
        Case(0x04000001, 3, false, (b, rig, log, t) =>
        {
            Assert.Contains(log, l => l.Contains($"onPickUpActionResult.Fail: Not trying to pick up '{t}' again. Failing"));
            var entry = Assert.Single(Assert.Single(rig.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PickUpObject)).Value);
            Assert.Equal(rig.M.World.GetLocatedObjectById(t)!.Pose.Translation, entry.Pose.Translation);      // the cube's own pose
        });
        Case(0x03000000, 1, false, (b, rig, log, t) =>
        {
            Assert.Contains(log, l => l.Contains($"onPickUpActionResult.NoRetry: Failed to pick up '{t}', action does not retry."));
            Assert.Single(rig.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PickUpObject));
        });
        foreach (uint r in new[] { 0x01000000u, 0x02000000u })
            Case(r, 1, false, (b, rig, log, t) =>
            {
                Assert.Empty(log);
                Assert.Empty(rig.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PickUpObject));
            });
    }

    /// <summary>
    /// Robot::ComputeHeadAngleToSeePose 0x00518344 results (0x00518344..0x00518620): no calibration is "NullCamera" and 1; a target that projects at or behind the camera ("BadProjectedZ") is 1; a converged
    /// angle is 0 with the target within the threshold (<c>rows * tol + 1e-5f</c>) of the principal point's row; after 25 non-converged iterations the function returns 0 with the angle UNASSIGNED
    /// (the counter compared at 0x005185C6 is then 26): a negative tolerance never converges. TurnTowardsPoseAction::Init falls back to GetAbsoluteHeadAngleToLookAtPose only on a non-zero result, with the
    /// warning "TurnTowardsPoseAction.Init.FailedToComputedHeadAngle: PoseWrtRobot translation=(%f,%f,%f)" (0x0054AB66).
    /// </summary>
    [Fact]
    public void M13_021_ComputeHeadAngleToSeePoseHasTheEnginesResults()
    {
        var cal = CameraCalibration.Nominal();
        var log = new List<string>();
        var ahead = new Pose3d(Mat3.Identity, new Vec3(200, 0, 60));
        Assert.Equal(1u, TurnTowardsPose.ComputeHeadAngleToSeePose(null, ahead, 0.01f, out _, log.Add));
        Assert.Contains("Robot.ComputeHeadAngleToSeePose.NullCamera", log);
        var behind = new Pose3d(Mat3.Identity, new Vec3(-13, 0, 400));      // straight above the neck: P = (0, 0, 351), at or behind the camera for the 4-degrees-down optical axis
        Assert.Equal(1u, TurnTowardsPose.ComputeHeadAngleToSeePose(cal, behind, 0.01f, out _, log.Add));
        Assert.Contains("Robot.ComputeHeadAngleToSeePose.BadProjectedZ", log);
        // fixed engine results: a Unicorn emulation of 0x00518344 (the verifier's harness) with the head cam at Robot::Robot's (17.52, 0, -8.0). The stack's pose algebra is double against the engine's
        // binary32, so the angle is compared within 1e-4 rad (the observed difference is a few ulp).
        (double x, double y, double z, uint bits)[] fixedResults = { (200, 0, 60, 0x3E1A42BBu), (150, -30, 120, 0x3F013CE0u), (400, 0, 0, 0xBCDB34EDu) };
        foreach (var (x, y, z, bits) in fixedResults)
        {
            Assert.Equal(0u, TurnTowardsPose.ComputeHeadAngleToSeePose(cal, new Pose3d(Mat3.Identity, new Vec3(x, y, z)), 0.01f, out float angle, log.Add));
            Assert.True(Math.Abs(angle - F(bits)) <= 1e-4, $"({x},{y},{z}): got {angle} ({B(angle):X8}), engine {F(bits)} ({bits:X8})");
        }
        // (300, 40, 20): the engine returns 0 with the angle unassigned (0)
        Assert.Equal(0u, TurnTowardsPose.ComputeHeadAngleToSeePose(cal, new Pose3d(Mat3.Identity, new Vec3(300, 40, 20)), 0.01f, out float none, log.Add));
        Assert.Equal(0u, B(none));
        // a negative tolerance never converges: 25 iterations, then 0 with the angle unassigned
        Assert.Equal(0u, TurnTowardsPose.ComputeHeadAngleToSeePose(cal, ahead, -1f, out float unassigned, log.Add));
        Assert.Equal(0u, B(unassigned));
        // the fallback only on a non-zero result, with the engine's warning text
        var texts = new List<string>();
        var target = new Pose3d(Mat3.Identity, new Vec3(400, 0, 0));
        var noCal = new TurnTowardsPoseEnv(Pose3d.Identity, 0, p => TurnTowardsPose.ComputeHeadAngleToSeePose(null, p, 0.01f, out var a, texts.Add) == 0 ? a : null, null, null, texts.Add);
        var compound = new TurnTowardsPoseCompound(target, Math.PI);
        Assert.Equal(0u, compound.InitPose(noCal, out _));
        Assert.Contains("Robot.ComputeHeadAngleToSeePose.NullCamera", texts);
        Assert.Contains(texts, t => t.StartsWith("TurnTowardsPoseAction.Init.FailedToComputedHeadAngle: PoseWrtRobot translation=(400.000000,0.000000,0.000000)"));
        Assert.Equal(0xBD45C00Eu, B((float)compound.HeadAngleRad));
        texts.Clear();
        var withCal = new TurnTowardsPoseEnv(Pose3d.Identity, 0, p => TurnTowardsPose.ComputeHeadAngleToSeePose(cal, p, 0.01f, out var a, texts.Add) == 0 ? a : null, null, null, texts.Add);
        var c2 = new TurnTowardsPoseCompound(new Pose3d(Mat3.Identity, new Vec3(200, 0, 60)), Math.PI);
        Assert.Equal(0u, c2.InitPose(withCal, out _));
        Assert.DoesNotContain(texts, t => t.Contains("FailedToComputedHeadAngle"));
    }

    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (File.Exists(Path.Combine(r, "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "activities_config.json"))) return r;
            d = d.Parent;
        }
        return null;
    }
}
