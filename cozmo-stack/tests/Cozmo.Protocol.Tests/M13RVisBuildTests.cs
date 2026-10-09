using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The R-VIS M13 build batch (records M13-007, M13-010, M13-014, M13-020, M13-021, M13-022, M13-023). Every expected value is taken from the
/// record's citation (address in each test's summary), never from what the code returns.
/// </summary>
public class M13RVisBuildTests
{
    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;

    /// <summary>
    /// The tests that need the marker library must not pass silently without it (AssetPresenceTests): they fail with the reason, or are
    /// skipped only when the run says so with COZMO_TESTS_WITHOUT_ASSETS=1. Returns true when the test may go on.
    /// </summary>
    private static bool NeedsLibrary()
    {
        if (Lib is not null) return true;
        if (Environment.GetEnvironmentVariable("COZMO_TESTS_WITHOUT_ASSETS") == "1") return false;
        throw new Xunit.Sdk.XunitException("the marker library is missing (Cozmo.Robot was built without Vision/Data/marker_nn_library.bin), so this test cannot run; " +
                                           "provide it or set COZMO_TESTS_WITHOUT_ASSETS=1 for a run that knowingly has none");
    }

    private static Rig FaceRig(params (int Id, Vec3 Head, string? Name)[] faces)
    {
        var rig = new Rig();
        rig.Vision.FaceDetector = rig.FaceDetector;
        rig.FaceDetector.Faces.AddRange(faces);
        rig.Head = 0.3f;
        return rig;
    }

    /// <summary>Runs a face action to its end, feeding frames as the vision loop would.</summary>
    private static FaceActionResult RunWithFrames(Rig rig, Task<FaceActionResult> task)
    {
        SignalTestContext.Run(task, () => { rig.Pump(); rig.Frame(); });
        Assert.True(task.IsCompleted, "the action did not finish");
        return task.Result;
    }
    private static ObservableObject Cube(uint id, double x, double y, double z) =>
        new(id, ObjectType.Block_LIGHTCUBE1, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1))
        { Pose = new Pose3d(Mat3.Identity, new Vec3(x, y, z)), PoseState = PoseState.Known };

    // ------------------------------------------------------------------ M13-007

    /// <summary>
    /// M13-007, helper 0x0062601C and its lambda 0x0062B9FC: the reference z is <c>ref.z + dimZ * (onTop ? +0.5 : -0.5)</c>
    /// (0x0062607E..0x006260A0), and a candidate passes the height test when
    /// <c>|targetZ - (cand.z + dimZ * (onTop ? -0.5 : +0.5))| &lt;= tol + 9.99999975e-06</c> (0x0062BAC2..0x0062BB0C).
    /// For 44 mm cubes: ref z 22, on top -> targetZ 44; a candidate at z=22+44+20=86 has cand.z-22 = 64, |44-64| = 20.
    /// </summary>
    [Fact]
    public void M13_007_TheFoundObjectTestUsesTheCallersToleranceAndHalfTheDimensions()
    {
        var bottom = Cube(1, 200, 0, 22);
        var upper = Cube(2, 200, 0, 86);                   // 20 mm above the exactly-stacked height of 66
        var cubes = new[] { bottom, upper };
        // 30.0 (0x41F00000 at 0x0061928C / 0x00619344 / 0x005BA12C): found
        Assert.Same(upper, BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, cubes, true, 30.0));
        // 15.0 (0x41700000 at 0x00632F0C and the seven other callers): not found
        Assert.Null(BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, cubes, true, 15.0));
        // the tolerance is inclusive with the 9.99999975e-06 epsilon (0x0062BB5C literal 0x3727C5AC)
        Assert.Same(upper, BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, cubes, true, 20.0));
        Assert.Null(BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, cubes, true, 19.9999));
    }

    /// <summary>
    /// M13-007, 0x00619358 (onTop = false, 30.0): the underneath search takes target z = ref.z - 22 and accepts the
    /// candidate whose top face (cand.z + 22) is within tolerance. Upper at z=66; lower at z=22-15.
    /// </summary>
    [Fact]
    public void M13_007_TheUnderneathSearchLooksBelowAndTheOnTopSearchDoesNotFindTheCubeBelow()
    {
        var upper = Cube(2, 200, 0, 66);
        var below = Cube(1, 200, 0, 7);                    // 15 mm too low: |44-... | = |66-22 - (7+22)| = 15
        var cubes = new[] { upper, below };
        Assert.Same(below, BlockConfigurationManager.FindObjectOnTopOrUnderneath(upper, cubes, false, 30.0));   // 0x00619358
        Assert.Same(below, BlockConfigurationManager.FindObjectOnTopOrUnderneath(upper, cubes, false, 15.0));   // 15 + eps
        Assert.Null(BlockConfigurationManager.FindObjectOnTopOrUnderneath(upper, cubes, false, 14.0));
        Assert.Null(BlockConfigurationManager.FindObjectOnTopOrUnderneath(upper, cubes, true, 30.0));           // below is not on top
    }

    /// <summary>M13-007, 0x006260AC..0x006260B8: the reference object's own id is added to the ignore set.</summary>
    [Fact]
    public void M13_007_TheReferenceObjectIsIgnored()
    {
        var only = Cube(1, 200, 0, 22);
        Assert.Null(BlockConfigurationManager.FindObjectOnTopOrUnderneath(only, new[] { only }, true, 30.0));
    }

    /// <summary>
    /// M13-007 / C-R1: BuildTallestStackForObject passes 30 for both searches (0x0061929E on top, 0x00619358 underneath).
    /// A three-cube column whose middle cube is 25 mm off its exact height is still one stack of three.
    /// </summary>
    [Fact]
    public void M13_007_BuildTallestStackUsesThirtyBothWays()
    {
        var world = new BlockWorld(() => Array.Empty<(uint, ObjectType)>());
        var a = Cube(1, 200, 0, 22); var b = Cube(2, 200, 0, 22 + 44 + 25); var c = Cube(3, 200, 0, 22 + 44 + 25 + 44);
        var mgr = new BlockConfigurationManager(world, () => 0);
        Assert.Equal(new uint[] { 1, 2, 3 }, mgr.BuildTallestStackForObject(b, new[] { a, b, c }).BlockIds);
        Assert.Equal(new uint[] { 1, 2, 3 }, mgr.BuildTallestStackForObject(c, new[] { a, b, c }).BlockIds);
    }

    /// <summary>M13-007: the caller constants named by the record (30.0 = 0x41F00000, 15.0 = 0x41700000).</summary>
    [Fact]
    public void M13_007_TheCallerToleranceConstants()
    {
        Assert.Equal(30.0, BlockConfigurationManager.OnTopPlanarToleranceMm);
        Assert.Equal(15.0, BlockConfigurationManager.RestingOnToleranceMm);
    }

    private static ObservableObject CubeYaw(uint id, double x, double y, double z, double yaw) =>
        new(id, ObjectType.Block_LIGHTCUBE1, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1))
        { Pose = new Pose3d(Mat3.AboutZ(yaw), new Vec3(x, y, z)), PoseState = PoseState.Known };

    /// <summary>
    /// M13-007, Block::GetBoundingQuadXY 0x004E62A2 and GetBoundingQuad/SortCornersClockwise 0x004E7EFC..0x004E7F9A: the corners are sorted by ascending
    /// atan2 about the centroid (s0..s3) and stored [0]=s0, [1]=s3, [2]=s1, [3]=s2, translated by the pose x, y (0x004E68CC). A 44 mm cube at (100, 50):
    /// the corners about the centroid are (-22,-22) -0.75pi, (22,-22) -0.25pi, (22,22) 0.25pi, (-22,22) 0.75pi.
    /// </summary>
    [Fact]
    public void M13_007_TheBoundingQuadIsSortedByAtan2AndStoredS0S3S1S2()
    {
        var q = Footprint.GetBoundingQuadXY(CubeYaw(1, 100, 50, 22, 0), new Pose3d(Mat3.Identity, new Vec3(100, 50, 22)), 0f);
        Assert.Equal(new Point2f(78, 28), q[0]);      // s0 (-22,-22)
        Assert.Equal(new Point2f(78, 72), q[1]);      // s3 (-22, 22)
        Assert.Equal(new Point2f(122, 28), q[2]);     // s1 (22,-22)
        Assert.Equal(new Point2f(122, 72), q[3]);     // s2 (22, 22)
        // 2*padding is added to the size (0x004E62A2): padding 3 -> 50 mm
        var padded = Footprint.GetBoundingQuadXY(CubeYaw(1, 0, 0, 22, 0), new Pose3d(Mat3.Identity, new Vec3(0, 0, 22)), 3f);
        Assert.Equal(new Point2f(-25, -25), padded[0]);
        Assert.Equal(new Point2f(25, 25), padded[3]);
    }

    private static Quadrilateral Rect(float x0, float y0, float x1, float y1) => new(new Point2f(x0, y0), new Point2f(x0, y1), new Point2f(x1, y0), new Point2f(x1, y1));

    /// <summary>
    /// M13-007, Quadrilateral::Intersects 0x00514800: a corner of either quad contained by the other, or any of the 16 edge pairs intersecting; edges
    /// cross inclusively (0 &lt;= t, u &lt;= 1, kmSegment2WithSegmentIntersection 0x008F6320) and a corner on an edge is contained (barycentric bounds
    /// -1.1920929e-07 and 1.0000001, 0x004E0480/0x004E0484).
    /// </summary>
    [Fact]
    public void M13_007_QuadrilateralIntersectsCornersEdgesAndTouching()
    {
        var a = Rect(0, 0, 10, 10);
        Assert.True(a.Intersects(Rect(5, 5, 15, 15)));         // corners contained
        Assert.True(a.Intersects(Rect(2, 2, 8, 8)));           // the other is contained
        Assert.True(Rect(2, 2, 8, 8).Intersects(a));           // this is contained by the other
        Assert.True(a.Intersects(Rect(10, 0, 20, 10)));        // shared edge: touching counts
        Assert.True(a.Intersects(Rect(10, 10, 20, 20)));       // shared corner: touching counts
        Assert.False(a.Intersects(Rect(10.5f, 0, 20, 10)));    // a gap
        Assert.False(a.Intersects(Rect(20, 20, 30, 30)));
        // a plus sign: no corner of either is inside the other, only the edges cross
        Assert.True(Rect(-50, -5, 50, 5).Intersects(Rect(-5, -50, 5, 50)));
        Assert.Equal(-1.1920929e-07f, Quadrilateral.BaryLower);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x3F800001), Quadrilateral.BaryUpper);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0xB4000000), Quadrilateral.BaryLower);
    }

    /// <summary>
    /// M13-007 (C-R5): the found-object test is footprint intersection, not a planar 22 mm centre test. A cube exactly on top (z = 66) and 44 mm to the
    /// side touches the reference's footprint edge, and is found (inclusive); 44.5 mm away is not. A cube yawed 45 degrees has corners at 22*sqrt(2) = 31.1127.
    /// </summary>
    [Fact]
    public void M13_007_TheFoundObjectTestIsFootprintIntersection()
    {
        var bottom = Cube(1, 200, 0, 22);
        var touching = Cube(2, 244, 0, 66);
        var gap = Cube(3, 244.5, 0, 66);
        Assert.Same(touching, BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, new[] { bottom, touching }, true, 15.0));
        Assert.Null(BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, new[] { bottom, gap }, true, 15.0));
        double diag = 22 * Math.Sqrt(2);
        var overlapDiamond = CubeYaw(4, 200 + 22 + diag - 1, 0, 66, Math.PI / 4);      // its left corner is 1 mm inside the reference
        var farDiamond = CubeYaw(5, 200 + 22 + diag + 0.5, 0, 66, Math.PI / 4);        // its left corner is 0.5 mm clear
        Assert.Same(overlapDiamond, BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, new[] { bottom, overlapDiamond }, true, 15.0));
        Assert.Null(BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, new[] { bottom, farDiamond }, true, 15.0));
        // and the height test still applies to a footprint that overlaps: 20 mm too high for 15
        Assert.Null(BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, new[] { bottom, CubeYaw(6, 200, 0, 86, 0.3) }, true, 15.0));
    }

    /// <summary>
    /// M13-023 (RECOVERABLE_GAP; record text: cv::minAreaRect and the other GetBoundingQuadXY overrides are unread): Footprint.GetBoundingQuadXY itself
    /// stays a visible stub (NotSupportedException 'M13-023') for a tilted cube and for a non-cube; a cube on its side (an own axis vertical) is a plain
    /// rectangle and is built.
    /// </summary>
    [Fact]
    public void M13_023_TheFootprintStubStillThrowsForTiltedCubesAndOtherClasses()
    {
        var tilted = new ObservableObject(9, ObjectType.Block_LIGHTCUBE1, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1))
        { Pose = new Pose3d(Mat3.AboutX(0.5), new Vec3(200, 0, 22)), PoseState = PoseState.Known };
        Assert.Contains("M13-023", Assert.Throws<NotSupportedException>(() => Footprint.GetBoundingQuadXY(tilted, tilted.Pose, 0f)).Message);
        var charger = new ObservableObject(20, ObjectType.Charger_Basic, Array.Empty<KnownMarker>()) { Pose = new Pose3d(Mat3.Identity, new Vec3(200, 0, 22)), PoseState = PoseState.Known };
        Assert.Contains("M13-023", Assert.Throws<NotSupportedException>(() => Footprint.GetBoundingQuadXY(charger, charger.Pose, 0f)).Message);
        var onItsSide = new Pose3d(Mat3.AboutX(Math.PI / 2), new Vec3(200, 0, 22));
        Assert.NotNull(Footprint.GetBoundingQuadXY(Cube(1, 200, 0, 22), onItsSide, 0f));
        Assert.False(Footprint.TryGetBoundingQuadXY(tilted, tilted.Pose, 0f, out _));
        Assert.False(Footprint.TryGetBoundingQuadXY(charger, charger.Pose, 0f, out _));
    }

    /// <summary>
    /// M13-023 / M13-007: where the exact footprint cannot be computed (a tilted cube, a non-cube) FindObjectOnTopOrUnderneath does not throw; it uses the
    /// labelled planar stand-in and says so (usedStandIn). The stand-in is the planar test the unresolved text names: centre within half a cube
    /// (22 mm) in x, y and the height within the tolerance of one cube height. A yaw-only pair does not use it.
    /// </summary>
    [Fact]
    public void M13_023_ATiltedOrNonCubeFootprintUsesTheLabelledPlanarStandInAndDoesNotThrow()
    {
        var bottom = Cube(1, 200, 0, 22);
        var tiltedOnTop = new ObservableObject(9, ObjectType.Block_LIGHTCUBE1, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1))
        { Pose = new Pose3d(Mat3.AboutX(0.5), new Vec3(210, 0, 66)), PoseState = PoseState.Known };   // centre 10 mm off, exactly one cube height up
        var found = BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, new[] { bottom, tiltedOnTop }, true, 15.0, out bool used);
        Assert.Same(tiltedOnTop, found);
        Assert.True(used);
        // centre 30 mm off (more than 22): not found, still no exception, still the stand-in
        var tiltedFar = new ObservableObject(10, ObjectType.Block_LIGHTCUBE1, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1))
        { Pose = new Pose3d(Mat3.AboutX(0.5), new Vec3(230, 0, 66)), PoseState = PoseState.Known };
        Assert.Null(BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, new[] { bottom, tiltedFar }, true, 15.0, out used));
        Assert.True(used);
        // height: 20 mm too high for 15, found for 30 (the tolerance is the caller's)
        var tiltedHigh = new ObservableObject(11, ObjectType.Block_LIGHTCUBE1, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1))
        { Pose = new Pose3d(Mat3.AboutX(0.5), new Vec3(200, 0, 86)), PoseState = PoseState.Known };
        Assert.Null(BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, new[] { bottom, tiltedHigh }, true, 15.0));
        Assert.Same(tiltedHigh, BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, new[] { bottom, tiltedHigh }, true, 30.0));
        // the underneath direction
        Assert.Same(tiltedOnTop, BlockConfigurationManager.FindObjectOnTopOrUnderneath(Cube(2, 205, 0, 110), new[] { tiltedOnTop }, false, 15.0));
        // a tilted REFERENCE
        var tiltedRef = new ObservableObject(12, ObjectType.Block_LIGHTCUBE1, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1))
        { Pose = new Pose3d(Mat3.AboutY(0.5), new Vec3(200, 0, 22)), PoseState = PoseState.Known };
        var above = Cube(13, 200, 0, 66);
        Assert.Same(above, BlockConfigurationManager.FindObjectOnTopOrUnderneath(tiltedRef, new[] { tiltedRef, above }, true, 15.0, out used));
        Assert.True(used);
        // a non-cube candidate
        var charger = new ObservableObject(20, ObjectType.Charger_Basic, Array.Empty<KnownMarker>()) { Pose = new Pose3d(Mat3.Identity, new Vec3(200, 0, 66)), PoseState = PoseState.Known };
        Assert.Same(charger, BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, new[] { bottom, charger }, true, 15.0, out used));
        Assert.True(used);
        // yaw-only pairs never use it
        BlockConfigurationManager.FindObjectOnTopOrUnderneath(bottom, new[] { bottom, Cube(3, 244, 0, 66) }, true, 15.0, out used);
        Assert.False(used);
    }

    /// <summary>
    /// M13-023 / M13-007 / M12-008 / M12-012: a tilted located cube must not abort the two call sites that take every located cube as a candidate
    /// (SetObjectAsAttachedToLift, CanStackOnTopOfObject); an exception there would unwind the robot-message handler. Called at the call site only.
    /// </summary>
    [Fact]
    public void M13_023_ATiltedLocatedCubeDoesNotAbortTheLiftAttachOrTheStackCheck()
    {
        if (Lib is null) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        obj.Pose = new Pose3d(Mat3.AboutX(0.5), new Vec3(200, 0, 66));                       // the located cube is tilted by more than 0.349066 rad
        var flat = Cube(50, 200, 0, 22);
        var stack = rig.M.Docking.CanStackOnTopOfObject(flat);                                // the tilted cube is a candidate; no exception
        Assert.False(stack);                                                                  // the stand-in finds it one cube height above (disclosed reading: an object on top makes it invalid)
        var ex = Record.Exception(() => rig.M.Docking.SetObjectAsAttachedToLift(7, MarkerType.LightCubeI_Front));
        Assert.Null(ex);
        var ex2 = Record.Exception(() => rig.M.Configurations.Update());
        Assert.Null(ex2);
    }

    // ------------------------------------------------------------------ M13-010

    private static WorkoutConfig WorkoutWith(params EmotionScorer[] strong) =>
        new(AnimationTrigger.WorkoutPreLift_highEnergy, AnimationTrigger.WorkoutPreLift_highEnergy, AnimationTrigger.WorkoutPreLift_highEnergy,
            AnimationTrigger.WorkoutPreLift_highEnergy, AnimationTrigger.WorkoutPreLift_highEnergy, AnimationTrigger.WorkoutPreLift_highEnergy,
            strong, Array.Empty<EmotionScorer>(), "", "");

    /// <summary>
    /// M13-010, ShouldPlayEightiesMusic 0x00573E30 / MoodScoreHelper 0x00573B70 (max(0, round(score))): a score that rounds to 0
    /// skips the roll (0x00573E48); otherwise true iff RandDbl(1.0) &lt; 0.1 (0x00573E58, threshold double 0.1 at 0x00573E80).
    /// The answer is cached at +0x10 with +0x11 set (0x00573E70..0x00573E74) and returned without rescoring.
    /// </summary>
    [Fact]
    public void M13_010_ShouldPlayEightiesMusicScoresRollsAndCaches()
    {
        var pass = new EmotionScorer(EmotionType.Happy, new Graph2d(new[] { (0.0, 1.0), (1.0, 1.0) }), false);   // y = 1 -> score 1
        var zero = new EmotionScorer(EmotionType.Happy, new Graph2d(new[] { (0.0, 0.0), (1.0, 0.0) }), false);   // |y| < 1e-5 -> 0.0
        int rolls = 0;
        var w0 = new WorkoutComponent(new[] { WorkoutWith(zero) });
        Assert.False(w0.ShouldPlayEightiesMusic(_ => 0.5, null, () => { rolls++; return 0.0; }));
        Assert.Equal(0, rolls);                                                     // score 0: no roll
        var w1 = new WorkoutComponent(new[] { WorkoutWith(pass) });
        Assert.True(w1.ShouldPlayEightiesMusic(_ => 0.5, null, () => { rolls++; return 0.09; }));
        Assert.Equal(1, rolls);
        Assert.True(w1.ShouldPlayEightiesMusic(_ => 0.5, null, () => { rolls++; return 0.99; }));              // cached
        Assert.Equal(1, rolls);
        var w2 = new WorkoutComponent(new[] { WorkoutWith(pass) });
        Assert.False(w2.ShouldPlayEightiesMusic(_ => 0.5, null, () => 0.1));       // 0.1 < 0.1 is false
        Assert.Equal(1, WorkoutConfig.MoodScoreHelper(0.5));                        // roundf(0.5) = 1
        Assert.Equal(0, WorkoutConfig.MoodScoreHelper(-3.0));                       // max(0, ...)
    }

    /// <summary>
    /// M13-010, EvaluateEmotionScore 0x0067C9B8: a trackDelta entry reads Emotion::GetHistoryValueTicksAgo(emotion, 0x3C)
    /// (0x0067C9EC..0x0067C9FC), the M7-mood ring buffer, which is not built: with no source the call is refused.
    /// </summary>
    [Fact]
    public void M13_010_TrackDeltaWithoutTheHistoryRingBufferIsRefused()
    {
        var delta = new EmotionScorer(EmotionType.Happy, new Graph2d(new[] { (0.0, 1.0) }), true);
        var ex = Assert.Throws<NotSupportedException>(() => WorkoutConfig.EvaluateEmotionScore(new[] { delta }, _ => 0.5, null));
        Assert.Contains("GetHistoryValueTicksAgo", ex.Message);
        // and with a source the entry is x = current - ago (0x0067C9FC): 0.75 - 0.25 = 0.5 -> y = 0.5 on a line y = x
        var line = new EmotionScorer(EmotionType.Happy, new Graph2d(new[] { (0.0, 0.0), (1.0, 1.0) }), true);
        Assert.Equal(0.5, WorkoutConfig.EvaluateEmotionScore(new[] { line }, _ => 0.75, _ => 0.25), 9);
    }

    /// <summary>
    /// M13-010, the second copy of MoodScorer::EvaluateEmotionScore 0x0067C9B8 (the behaviour chooser ScoredBehaviorEntry): a trackDelta entry reads
    /// Emotion::GetHistoryValueTicksAgo (0x006794F8) from the M7-013 history ring (R-BEH2 batch 2 built it; it used to be refused). The ring of a fresh
    /// mood holds only the constructor sample {0.0f, 0.0f}, so 60 ticks ago is 0.0f and x = 0 - 0 = 0 -> the flat-1 graph gives 1.0.
    /// </summary>
    [Fact]
    public void M13_010_TheChoosersScorerSubtractsTheHistoryRingForTrackDelta()
    {
        if (!NeedsLibrary()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Mood = new MoodState(new MoodModel()) };
        var graph = new Graph2d(new[] { (0.0, 1.0), (1.0, 1.0) });
        var delta = new ScoredBehaviorEntry("a", 7.0, null, null, null, new[] { new EmotionScorer(EmotionType.Happy, graph, true) });
        Assert.Equal(1.0, delta.Evaluate(null!, ctx, 0, null, 0.0, null), 6);
        var level = new ScoredBehaviorEntry("a", 7.0, null, null, null, new[] { new EmotionScorer(EmotionType.Happy, graph, false) });
        Assert.Equal(1.0, level.Evaluate(null!, ctx, 0, null, 0.0, null), 6);
    }

    // ------------------------------------------------------------------ M13-014

    /// <summary>
    /// M13-014 / C-R3: TurnTowardsLastFacePoseAction is a TurnTowardsFaceAction (constructor 0x0055B3C0..0x0055B3CC builds one and
    /// overwrites the vtable), +0x193 is zero from the constructor (0x0054B7B6), and the face id it passes is 0 (0x0055B3BC movs r2,#0).
    /// </summary>
    [Fact]
    public void M13_014_TheLastFacePoseActionIsATurnTowardsFaceActionWithFaceIdZero()
    {
        Assert.True(typeof(TurnTowardsFaceAction).IsAssignableFrom(typeof(TurnTowardsLastFacePoseAction)));
        Assert.NotEqual(typeof(TurnTowardsFaceAction), typeof(TurnTowardsLastFacePoseAction));
        Assert.Equal(0, TurnTowardsLastFacePoseAction.EngineFaceIdArgument);           // 0x0055B3BC: face id 0
        if (!NeedsLibrary()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        using var a = new TurnTowardsLastFacePoseAction(rig.M.Vision, Math.PI, sayName: false);
        Assert.False(a.RequireVerifiedFace);
        Assert.False(a.SayName);
        Assert.Equal(0, a.FaceId.Id);                                                  // the SmartFaceID stores 0 as given (0x0053B1C0)
    }

    /// <summary>
    /// M13-014, Init failure with +0x193 CLEAR (0x0054BEB6..0x0054BEBC): +0x190 = 3 and return 0 (SUCCESS), no log, never 0x0300000B
    /// (MISMATCHED_UP_AXIS); state 3 with no child ends at its first CheckIfDone (0x0054C606..0x0054C63E), without SetTurnedTowardsFace.
    /// </summary>
    [Fact]
    public void M13_014_NoFacePoseWithTheFlagClearIsSuccessInStateThree()
    {
        if (!NeedsLibrary()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = FaceRig();
        using var a = new TurnTowardsLastFacePoseAction(rig.Vision, Math.PI, false);
        Assert.Equal(FaceActionResult.Success, a.Init());
        Assert.Equal(3, a.State);
        Assert.DoesNotContain(a.Trace, l => l.Contains("Required face pose"));
        using var b = new TurnTowardsLastFacePoseAction(rig.Vision, Math.PI, false);
        var r = b.RunAsync(default).GetAwaiter().GetResult();
        Assert.Equal(0u, (uint)r);
        Assert.NotEqual(0x0300000Bu, (uint)r);
        Assert.Empty(b.NeedsActionsRegistered);
        Assert.False(b.TracksLocked);
        // a valid id with no such face takes the same silent failure (0x0054BD66..0x0054BD9A)
        using var c = new TurnTowardsFaceAction(rig.Vision, 99);
        Assert.Equal(FaceActionResult.Success, c.RunAsync(default).GetAwaiter().GetResult());
        Assert.Empty(c.Trace);
    }

    /// <summary>
    /// M13-014, Init failure with +0x193 SET (0x0054BE6C..0x0054BEB4): logs "TurnTowardsFaceAction.Init.NoFacePose" / "Required face pose, don't have
    /// one, failing" and returns NO_FACE 0x0300000E (ActionResult.cs; the stack's old NoFace 0x0300000B is MISMATCHED_UP_AXIS).
    /// </summary>
    [Fact]
    public void M13_014_NoFacePoseWithTheFlagSetIsNoFace0x0300000E()
    {
        if (!NeedsLibrary()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = FaceRig();
        using var flagged = new TurnTowardsLastFacePoseAction(rig.Vision, Math.PI, false) { RequireVerifiedFace = true };
        Assert.Equal(0x0300000Eu, (uint)flagged.Init());
        Assert.Contains(flagged.Trace, l => l == "TurnTowardsFaceAction.Init.NoFacePose: Required face pose, don't have one, failing");
        Assert.Equal(0x0300000Eu, (uint)FaceActionResult.NoFaceRequired);
        Assert.Equal(0x0300000Bu, (uint)FaceActionResult.NoFace);                   // a different code, and no path returns it
        Assert.Equal(0x0300000Eu, (uint)new TurnTowardsLastFacePoseAction(rig.Vision, Math.PI, false) { RequireVerifiedFace = true }.RunAsync(default).GetAwaiter().GetResult());
    }

    /// <summary>
    /// M13-014, Init with a pose (0x0054BDBA..0x0054BE34): the invalid id (0) takes the last observed face, state 0, tracks locked, verified id cleared;
    /// a valid id takes FaceWorld::GetFace.
    /// </summary>
    [Fact]
    public void M13_014_InitWithAPoseStartsStateZeroAndLocksTracks()
    {
        if (!NeedsLibrary()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = FaceRig((7, new Vec3(400, 150, 250), null));
        rig.Frame();
        Assert.Single(rig.Vision.Faces.Faces);
        using var last = new TurnTowardsLastFacePoseAction(rig.Vision, Math.PI, false);
        Assert.Equal(FaceActionResult.Success, last.Init());
        Assert.Equal(0, last.State);
        Assert.True(last.TracksLocked);
        Assert.Null(last.VerifiedFaceId);
        using var byId = new TurnTowardsFaceAction(rig.Vision, 7);
        Assert.Equal(FaceActionResult.Success, byId.Init());
        Assert.Equal(0, byId.State);
    }

    /// <summary>
    /// M13-014, the RobotObservedFace handler 0x0054C050..0x0054C19E: with an invalid id the face becomes the verified id only when its 3-D distance^2 to
    /// the robot pose is STRICTLY below the best so far; with a valid id only that face matches; and it ignores everything once the state is above 1.
    /// </summary>
    [Fact]
    public void M13_014_TheObservedFaceHandlerChoosesTheStrictlyClosestFaceWhileTheStateIsAtMostOne()
    {
        if (!NeedsLibrary()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = FaceRig((7, new Vec3(400, 150, 250), null), (8, new Vec3(300, 20, 250), null));
        rig.Frame();
        Assert.Equal(2, rig.Vision.Faces.Count);
        // the robot is at the origin facing +x, so 8 (about 390 mm) is closer than 7 (about 500 mm)
        using var a = new TurnTowardsLastFacePoseAction(rig.Vision, Math.PI, false);
        Assert.Equal(FaceActionResult.Success, a.Init());
        a.HandleRobotObservedFace(7);
        Assert.Equal(7, a.VerifiedFaceId);
        a.HandleRobotObservedFace(8);
        Assert.Equal(8, a.VerifiedFaceId);                       // closer: replaces
        a.HandleRobotObservedFace(7);
        Assert.Equal(8, a.VerifiedFaceId);                       // farther: kept
        a.HandleRobotObservedFace(8);
        Assert.Equal(2, a.Trace.Count(l => l.StartsWith("TurnTowardsFaceAction.ObservedFaceCallback: Observed ID=")));   // equal distance: not strictly below, no new log
        a.HandleRobotObservedFace(12345);                        // no such face: ignored
        Assert.Equal(8, a.VerifiedFaceId);
        // a valid id: only that face matches
        using var byId = new TurnTowardsFaceAction(rig.Vision, 7);
        Assert.Equal(FaceActionResult.Success, byId.Init());
        byId.HandleRobotObservedFace(8);
        Assert.Null(byId.VerifiedFaceId);
        byId.HandleRobotObservedFace(7);
        Assert.Equal(7, byId.VerifiedFaceId);
    }

    /// <summary>
    /// M13-014, the state gate 0x0054C062 and the final step 0x0054C622: once the action has left states 0 and 1 the handler changes nothing, and a
    /// verified face gets SetTurnedTowardsFace(id, true). Observations that arrive during the turn count.
    /// </summary>
    [Fact]
    public void M13_014_AfterTheWaitTheHandlerIsClosedAndAVerifiedFaceIsMarkedTurnedTowards()
    {
        if (!NeedsLibrary()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = FaceRig((7, new Vec3(400, 150, 250), null), (8, new Vec3(300, 20, 250), null));
        rig.Frame();
        using var a = new TurnTowardsLastFacePoseAction(rig.Vision, Math.PI, false);
        Assert.Equal(FaceActionResult.Success, RunWithFrames(rig, a.RunAsync(default)));
        Assert.True(a.State >= 2);
        int? verified = a.VerifiedFaceId;
        Assert.NotNull(verified);
        Assert.True(rig.Vision.Faces.HasTurnedTowardsFace(verified!.Value));
        int other = verified.Value == 7 ? 8 : 7;
        a.HandleRobotObservedFace(other);
        Assert.Equal(verified, a.VerifiedFaceId);
        Assert.Contains(a.NeedsActionsRegistered, id => id == 0x2E);              // RegisterNeedsActionCompleted(SeeFace 0x2E), 0x0054C2E0..0x0054C2E6
    }

    /// <summary>
    /// M13-014, state 1 ending with no verified face (0x0054C5DA..0x0054C604): +0x193 set returns 0x0300000E, +0x193 clear is the final step (success)
    /// and, with no verified id, no SetTurnedTowardsFace. The wait is the WaitForImagesAction of 10 frames (0x0054C640..0x0054C6B8); the 2 s it also
    /// gives up at is the earlier implementation's local guard.
    /// </summary>
    [Fact]
    public void M13_014_ANoFrameWaitEndsNoFaceWhenFlaggedAndSuccessWhenNot()
    {
        if (!NeedsLibrary()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = FaceRig((7, new Vec3(400, 150, 250), null));
        rig.Frame();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using var flagged = new TurnTowardsFaceAction(rig.Vision, 7) { RequireVerifiedFace = true, UtcNow = () => now };
        var flaggedTask = flagged.RunAsync(default);
        SignalTestContext.Until(() => flagged.State == 1, () => rig.Pump());
        now = now.AddSeconds(2);
        SignalTestContext.Run(flaggedTask);
        Assert.Equal(0x0300000Eu, (uint)flaggedTask.Result);
        Assert.Contains(flagged.Trace, l => l.Contains("Will wait no more than 10 frames"));
        using var plain = new TurnTowardsFaceAction(rig.Vision, 7) { UtcNow = () => now };
        var plainTask = plain.RunAsync(default);
        SignalTestContext.Until(() => plain.State == 1, () => rig.Pump());
        now = now.AddSeconds(2);
        SignalTestContext.Run(plainTask);
        Assert.Equal(FaceActionResult.Success, plainTask.Result);
        Assert.False(rig.Vision.Faces.HasTurnedTowardsFace(7));
        Assert.Null(plain.Reaction);
    }

    /// <summary>
    /// M13-014, TurnTowardsPoseAction::Init (M13-020) as the face action runs it: a pan beyond the maximum turn sets +0x179, Init returns 0 and
    /// NOTHING moves (no body turn, no head move), for the first turn and for the fine tune; and CreateFineTuneAction gives the fine tune
    /// min(|maxTurn|, 0.7853982) (0x0054C4E4 = 0x3F490FDB).
    /// </summary>
    [Fact]
    public void M13_014_TheFineTuneMaxTurnIsTheSmallerOfTheMaxAndFortyFiveDegreesAndAnOverlargePanMovesNothing()
    {
        if (!NeedsLibrary()) return;
        foreach (var (max, expected) in new[] { (0.5, new[] { 0.5, 0.5 }), (Math.PI, new[] { (double)MathF.PI, (double)0.7853982f }) })
        {
            using var signals_rig = SignalTestContext.Install();
            using var rig = FaceRig((7, new Vec3(400, 150, 250), null));
            rig.Frame();
            var maxes = new List<double>();
            var orig = rig.Vision.TurnOverride!;
            rig.Vision.TurnOverride = (t, m, c) => { maxes.Add(m); return orig(t, m, c); };
            using var turn = new TurnTowardsFaceAction(rig.Vision, 7, max);
            Assert.Equal(FaceActionResult.Success, RunWithFrames(rig, turn.RunAsync(default)));
            Assert.Equal(2, maxes.Count);
            Assert.Equal(expected[0], maxes[0], 6);
            Assert.Equal(expected[1], maxes[1], 6);
        }
        using var signals_rig2 = SignalTestContext.Install();
        using var rig2 = FaceRig((7, new Vec3(400, 150, 250), null));
        rig2.Frame();
        var moved = new List<double>();
        var orig2 = rig2.Vision.TurnOverride!;
        rig2.Vision.TurnOverride = (t, m, c) => { moved.Add(m); return orig2(t, m, c); };
        int headBefore = rig2.FaceTurns;
        using var skipped = new TurnTowardsFaceAction(rig2.Vision, 7, 0.3);                 // the face is at atan2(150, 400) = 0.359 rad
        Assert.Equal(FaceActionResult.Success, RunWithFrames(rig2, skipped.RunAsync(default)));
        Assert.Empty(moved);
        Assert.Equal(headBefore, rig2.FaceTurns);
        Assert.Contains(skipped.Trace, l => l.Contains("turn skipped"));
        Assert.Equal(7, skipped.VerifiedFaceId);
    }

    /// <summary>
    /// M13-014 / Q6, the four setters (0x0054B978, 0x0054BA84, 0x0054BB8C, 0x0054BC6C): each installs its function regardless of the sayName flag and
    /// only logs "...WithoutSayingName" when the flag is 0; state 2 calls the function with the verified id, and 0x23F (none) plays no animation. The
    /// name-said path registers SayName 0x29 (0x0054C616..0x0054C61E).
    /// </summary>
    [Fact]
    public void M13_014_TheCallbackSettersInstallRegardlessOfSayNameAndOnlyLogWhenItIsZero()
    {
        if (!NeedsLibrary()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = FaceRig((7, new Vec3(400, 150, 250), "Jim"));
        rig.Frame();
        using var quiet = new TurnTowardsFaceAction(rig.Vision, 7, Math.PI, sayName: false);
        quiet.SetSayNameTriggerCallback(_ => AnimationTrigger.AcknowledgeFaceNamed);
        quiet.SetNoNameTriggerCallback(_ => AnimationTrigger.AcknowledgeFaceUnnamed);
        quiet.SayNameTrigger = AnimationTrigger.AcknowledgeFaceNamed;
        quiet.NoNameTrigger = AnimationTrigger.AcknowledgeFaceUnnamed;
        Assert.Equal(new[]
        {
            "TurnTowardsFaceAction.SetSayNameTriggerCallbackWithoutSayingName", "TurnTowardsFaceAction.SetNoNameTriggerCallbackWithoutSayingName",
            "TurnTowardsFaceAction.SetSayNameTriggerWithoutSayingName", "TurnTowardsFaceAction.SetNoNameTriggerWithoutSayingName",
        }, quiet.Trace.ToArray());
        Assert.Equal(AnimationTrigger.AcknowledgeFaceNamed, quiet.SayNameTrigger);
        Assert.Equal(FaceActionResult.Success, RunWithFrames(rig, quiet.RunAsync(default)));
        Assert.Null(quiet.Reaction);                                                        // sayName 0: state 2 ends at the final step

        var seen = new List<int>();
        using var named = new TurnTowardsFaceAction(rig.Vision, 7, Math.PI, sayName: true);
        named.SetSayNameTriggerCallback(id => { seen.Add(id.Id); return AnimationTrigger.AcknowledgeFaceNamed; });
        Assert.DoesNotContain(named.Trace, l => l.Contains("WithoutSayingName"));
        Assert.Equal(FaceActionResult.Success, RunWithFrames(rig, named.RunAsync(default)));
        Assert.Equal((AnimationTrigger.AcknowledgeFaceNamed, "Jim"), named.Reaction!.Value);
        Assert.Equal(new[] { 7 }, seen);
        Assert.Equal(new uint[] { 0x2E, 0x29 }, named.NeedsActionsRegistered.ToArray());

        // 0x23F (null here) from the say-name function: the name is still said, with no animation
        using var noAnim = new TurnTowardsFaceAction(rig.Vision, 7, Math.PI, sayName: true);
        noAnim.SetSayNameTriggerCallback(_ => null);
        Assert.Equal(FaceActionResult.Success, RunWithFrames(rig, noAnim.RunAsync(default)));
        Assert.Equal(((AnimationTrigger?)null, (string?)"Jim"), noAnim.Reaction!.Value);
    }

    /// <summary>
    /// M13-014, state 2 for an unnamed face (0x0054C6C2..0x0054C70A): no no-name function (pointer 0) or 0x23F ends at the final step; a trigger is played
    /// through TriggerLiftSafeAnimationAction and registers SayName 0x29.
    /// </summary>
    [Fact]
    public void M13_014_AnUnnamedFaceUsesTheNoNameFunction()
    {
        if (!NeedsLibrary()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = FaceRig((7, new Vec3(400, 150, 250), null));
        rig.Frame();
        using var none = new TurnTowardsFaceAction(rig.Vision, 7, Math.PI, sayName: true);
        Assert.Equal(FaceActionResult.Success, RunWithFrames(rig, none.RunAsync(default)));
        Assert.Null(none.Reaction);
        Assert.Equal(new uint[] { 0x2E }, none.NeedsActionsRegistered.ToArray());
        using var some = new TurnTowardsFaceAction(rig.Vision, 7, Math.PI, sayName: true);
        some.SetNoNameTriggerCallback(_ => AnimationTrigger.AcknowledgeFaceUnnamed);
        Assert.Equal(FaceActionResult.Success, RunWithFrames(rig, some.RunAsync(default)));
        Assert.Equal(((AnimationTrigger?)AnimationTrigger.AcknowledgeFaceUnnamed, (string?)null), some.Reaction!.Value);
        Assert.Equal(new uint[] { 0x2E, 0x29 }, some.NeedsActionsRegistered.ToArray());
    }

    /// <summary>M13-014 sayName: DriveAndFlipBlockAction forwards the caller's bool (0x0055E23E); BehaviorKnockOverCubes passes 0 (0x005C3532).</summary>
    [Fact]
    public void M13_014_DriveAndFlipBlockForwardsTheCallersSayNameAndKnockOverPassesFalse()
    {
        if (!NeedsLibrary()) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        Assert.False(new DriveAndFlipBlockAction(rig.M, 1).SayName);
        Assert.True(new DriveAndFlipBlockAction(rig.M, 1) { SayName = true }.SayName);
    }

    // ------------------------------------------------------------------ M13-020

    private static PanAndTiltAction Pan(bool headAbs = true, bool hasMax = false, double maxAccel = 0, bool hasHead = false) => new()
    {
        PanAngleRad = 0.5, PanIsAbsolute = true, HeadAngleRad = 0.25, HeadIsAbsolute = headAbs, Byte0x126 = true, Byte0x57 = 9,
        PanToleranceRad = 0.1, MaxSpeedRadPerSec = 2.0, MaxAccelRadPerSec2 = maxAccel, HeadToleranceRad = 0.2,
        HasMaxSpeed = hasMax, HasHeadSpeedAccel = hasHead, HeadSpeedAccelWord0 = 0xAAAA0001, HeadSpeedAccelWord1 = 0xBBBB0002,
    };

    /// <summary>
    /// M13-020, PanAndTiltAction::Init 0x00549C48..0x00549D60: [+0xCF] = [+0x57] (0x00549C58); TurnInPlaceAction(robot, [+0x114], [+0x124])
    /// with tolerance [+0x140] and [tia+0xD8] = [+0x126]; MoveHeadToAngleAction(robot, angle, [+0x150], Radians(0)) with [mh+0x9D] = 0
    /// and [mh+0x9C] = [+0x126]; [+0xCE] = 1; the head angle is Radians([+0x11C]) when [+0x125] else robot[+0x2FC] + [+0x11C]
    /// (0x00549CD0..0x00549CEE).
    /// </summary>
    [Fact]
    public void M13_020_InitBuildsTheTurnAndTheHeadMoveFromTheFields()
    {
        IReadOnlyList<object>? seen = null;
        var a = Pan(headAbs: false); a.CompoundUpdate = c => { seen = c; return PanAndTiltAction.Running; };
        Assert.Equal(0u, a.Init(robotHeadAngleRad: 0.1));                         // RUNNING is mapped to 0 (0x00549D52 / 0x00549D56)
        var turn = Assert.IsType<TurnInPlaceSpec>(seen![0]);
        var head = Assert.IsType<MoveHeadToAngleSpec>(seen![1]);
        Assert.Equal((0.5, true, 0.1, true), (turn.AngleRad, turn.IsAbsolute, turn.ToleranceRad, turn.Byte0xD8));
        Assert.False(turn.MaxSpeedSet);
        Assert.Equal(TurnTowardsPose.AccelRadPerSec2, turn.AccelRadPerSec2);        // [tia+0x7C] stays [tia+0xC8]
        Assert.Equal(0.1 + 0.25, head.AngleRad, 6);                               // relative: robot[+0x2FC] + [+0x11C]
        Assert.Equal((0.2, 0.0, true, false, false), (head.ToleranceRad, head.VarianceRad, head.Byte0x9C, head.Byte0x9D, head.Bytes0x90Set));
        Assert.Equal((byte)9, a.Byte0xCF);
        Assert.True(a.Byte0xCE);
        var abs = Pan(headAbs: true); abs.CompoundUpdate = _ => 0; abs.Init(99.0);
        Assert.Equal(0.25, Assert.IsType<MoveHeadToAngleSpec>(abs.Children[1]).AngleRad);
    }

    /// <summary>
    /// M13-020, 0x00549C7C..0x00549CB4: with [+0x160] the turn gets the max speed [+0x148]; if [+0x14C] == 0 the acceleration stays [tia+0x7C],
    /// else [tia+0xCC] = 1 and [tia+0xC8] = [+0x14C]; with [+0x161] the eight bytes [+0x158..0x15F] go to [mh+0x90..0x97] (0x00549D14..0x00549D2C).
    /// </summary>
    [Fact]
    public void M13_020_InitCopiesTheMaxSpeedAccelerationAndHeadSpeedBytes()
    {
        var a = Pan(hasMax: true, maxAccel: 7.5, hasHead: true); a.CompoundUpdate = _ => 0; a.Init(0);
        var turn = Assert.IsType<TurnInPlaceSpec>(a.Children[0]);
        Assert.Equal((true, 2.0, 7.5, true), (turn.MaxSpeedSet, turn.MaxSpeedRadPerSec, turn.AccelRadPerSec2, turn.Byte0xCC));
        var head = Assert.IsType<MoveHeadToAngleSpec>(a.Children[1]);
        Assert.Equal((true, 0xAAAA0001u, 0xBBBB0002u), (head.Bytes0x90Set, head.Word0x90, head.Word0x94));
        var zeroAccel = Pan(hasMax: true, maxAccel: 0); zeroAccel.CompoundUpdate = _ => 0; zeroAccel.Init(0);
        var t2 = Assert.IsType<TurnInPlaceSpec>(zeroAccel.Children[0]);
        Assert.Equal((TurnTowardsPose.AccelRadPerSec2, false), (t2.AccelRadPerSec2, t2.Byte0xCC));
    }

    /// <summary>
    /// M13-020, 0x00549D52 (orr r1,r0,#0x1000000) / 0x00549D56 (cmp.w r1,#0x1000000): Init returns 0 for SUCCESS and RUNNING and the compound's result
    /// otherwise; CheckIfDone 0x00549D73 returns the compound's Update() unchanged.
    /// </summary>
    [Fact]
    public void M13_020_InitMapsSuccessAndRunningToZeroAndCheckIfDoneIsUnchanged()
    {
        var a = Pan(); a.CompoundUpdate = _ => 0x03000018u;
        Assert.Equal(0x03000018u, a.Init(0));
        a.CompoundUpdate = _ => PanAndTiltAction.Running;
        Assert.Equal(PanAndTiltAction.Running, a.CheckIfDone());
        a.CompoundUpdate = _ => 0x04000001u;
        Assert.Equal(0x04000001u, a.CheckIfDone());
    }

    /// <summary>M13-020 / M13-021: the children's bodies are not read, so with no compound the action refuses rather than guessing.</summary>
    [Fact]
    public void M13_020_WithoutTheChildrenBodiesInitIsRefused()
    {
        Assert.Throws<NotSupportedException>(() => Pan().Init(0));
        Assert.Throws<NotSupportedException>(() => Pan().CheckIfDone());
    }

    /// <summary>
    /// M13-020, WaitForImagesAction ctor 0x0054CA64 / Init 0x0054CB68 / handler 0x0054DD88 / CheckIfDone 0x0054CC1C: count only messages whose
    /// timestamp is greater than [+0x7C] (0x0054DD9A) and whose modes contain the wanted mode, or any when the mode is 0x10; RUNNING until the
    /// count reaches numFrames, then drop the subscription and return 0.
    /// </summary>
    [Fact]
    public void M13_020_WaitForImagesCountsMatchingImagesAfterTheTimestamp()
    {
        var w = new WaitForImagesAction(2, VisionMode.DetectingFaces, afterTimestamp: 100);
        Assert.Equal(0x32, WaitForImagesAction.ActionType);
        Assert.Equal(0x43, WaitForImagesAction.RobotProcessedImageTag);
        Assert.Equal(0u, w.Init());
        Assert.True(w.IsSubscribed);
        w.HandleRobotProcessedImage(100, new[] { VisionMode.DetectingFaces });       // not greater than +0x7C
        w.HandleRobotProcessedImage(101, new[] { VisionMode.DetectingMarkers });     // mode missing
        Assert.Equal(0, w.Count);
        w.HandleRobotProcessedImage(101, new[] { VisionMode.DetectingMarkers, VisionMode.DetectingFaces });
        Assert.Equal(PanAndTiltAction.Running, w.CheckIfDone());
        w.HandleRobotProcessedImage(102, new[] { VisionMode.DetectingFaces });
        Assert.Equal(0u, w.CheckIfDone());
        Assert.False(w.IsSubscribed);
        var any = new WaitForImagesAction(1, VisionMode.Count, 0); any.Init();
        any.HandleRobotProcessedImage(1, Array.Empty<VisionMode>());               // mode 0x10 counts any image
        Assert.Equal(0u, any.CheckIfDone());
    }

    // ------------------------------------------------------------------ M13-020 (TurnTowardsPoseAction::Init)

    private static TurnTowardsPoseEnv PoseEnv(double robotX = 0, double robotY = 0, Func<Pose3d, double?>? see = null, Func<Vec3, double>? absolute = null,
                                              Func<Pose3d, Pose3d?>? wrt = null) =>
        new(new Pose3d(Mat3.Identity, new Vec3(robotX, robotY, 0)), 0.1, see ?? (_ => 0.2), absolute, wrt);

    /// <summary>
    /// M13-020, TurnTowardsPoseAction ctors 0x00549F10 / 0x0054B344 and PanAndTiltAction ctor 0x0054962C: pan relative, tilt absolute, +0x170 = abs(maxTurn),
    /// +0x178 = pose set, +0x179 = 0; defaults +0x140/+0x150 = Radians(5 deg) (0x3DB2B8C2 = 0.0872665), +0x148 = 5.235988 (0x40A78D36), +0x14C = 10.0,
    /// +0x158 = 15.0 (0x41700000), +0x15C = 20.0 (0x41A00000), +0x126 = 1.
    /// </summary>
    [Fact]
    public void M13_020_TheConstructorsSetTheDocumentedFieldsAndDefaults()
    {
        var t = new TurnTowardsPoseCompound(-1.5);
        Assert.False(t.PanIsAbsolute);
        Assert.True(t.HeadIsAbsolute);
        Assert.Equal(1.5, t.MaxTurnAbsRad, 6);
        Assert.False(t.PoseSet);
        Assert.False(t.Byte0x179);
        Assert.True(t.Byte0x126);
        Assert.Equal(0.0872665, t.PanToleranceRad, 6);
        Assert.Equal(0.0872665, t.HeadToleranceRad, 6);
        Assert.Equal((double)BitConverter.UInt32BitsToSingle(0x40A78D36), t.MaxSpeedRadPerSec, 6);
        Assert.Equal(10.0, t.MaxAccelRadPerSec2, 6);
        Assert.Equal(0x41700000u, t.HeadSpeedAccelWord0);
        Assert.Equal(0x41A00000u, t.HeadSpeedAccelWord1);
        Assert.False(t.HasMaxSpeed);
        Assert.False(t.HasHeadSpeedAccel);
        var withPose = new TurnTowardsPoseCompound(new Pose3d(Mat3.Identity, new Vec3(1, 2, 3)), 1.0);
        Assert.True(withPose.PoseSet);
    }

    /// <summary>
    /// M13-020, TurnTowardsPoseAction::Init 0x0054A8FC step (2) and (3): no pose set returns BAD_POSE 0x03000005 (0x0054A9B8); a parented pose whose
    /// GetWithRespectTo(Robot::GetPose()) fails returns BAD_POSE (0x0054A952). Init returns before PanAndTiltAction::Init.
    /// </summary>
    [Fact]
    public void M13_020_TurnTowardsPoseInitReturnsBadPoseForAnUnsetOrUnreachablePose()
    {
        var unset = new TurnTowardsPoseCompound(Math.PI);
        Assert.Equal(0x03000005u, unset.InitPose(PoseEnv(), out bool go));
        Assert.False(go);
        var withPose = new TurnTowardsPoseCompound(new Pose3d(Mat3.Identity, new Vec3(100, 0, 0)), Math.PI);
        Assert.Equal(0x03000005u, withPose.InitPose(PoseEnv(wrt: _ => null), out go));
        Assert.False(go);
        Assert.Equal(0x03000005u, TurnTowardsPoseCompound.BadPose);
    }

    /// <summary>
    /// M13-020, Init step (3): a parentless pose is only re-parented to the world origin (its translation is used as it is); a parented pose is taken
    /// with respect to Robot::GetPose(). Step (4): pan = atan2f(y, x) of that translation. Robot at (100, 0) facing +x, target at (0, 100):
    /// parented pan = atan2(100, -100) = 3pi/4; parentless pan = atan2(100, 0) = pi/2.
    /// </summary>
    [Fact]
    public void M13_020_TurnTowardsPoseInitTakesAParentedPoseWithRespectToTheRobot()
    {
        var target = new Pose3d(Mat3.Identity, new Vec3(0, 100, 0));
        var parented = new TurnTowardsPoseCompound(target, Math.PI);
        Assert.Equal(0u, parented.InitPose(PoseEnv(100, 0), out bool go));
        Assert.True(go);
        Assert.Equal(3 * Math.PI / 4, parented.PanAngleRad, 5);
        var parentless = new TurnTowardsPoseCompound(target, Math.PI) { PoseHasParent = false };
        Assert.Equal(0u, parentless.InitPose(PoseEnv(100, 0), out go));
        Assert.Equal(Math.PI / 2, parentless.PanAngleRad, 5);
    }

    /// <summary>
    /// M13-020, Init step (4) 0x0054AC8A: with +0x170 &gt; 0 and |pan| &lt;= +0x170 the pan is +0x114; with |pan| above it +0x179 = 1, Init returns 0 without
    /// PanAndTiltAction::Init and without the head angle, and CheckIfDone 0x0054B011 returns 0; with +0x170 &lt;= 0 the pan stays 0 and the head is still computed.
    /// </summary>
    [Fact]
    public void M13_020_TurnTowardsPoseInitPanWithinBeyondAndWithoutAMaxTurn()
    {
        var target = new Pose3d(Mat3.Identity, new Vec3(400, 150, 0));           // pan = atan2(150, 400) = 0.35877
        double pan = Math.Atan2(150, 400);
        bool seen = false;
        var within = new TurnTowardsPoseCompound(target, 0.5) { CompoundUpdate = _ => PanAndTiltAction.Running };
        Assert.Equal(0u, within.InitPose(PoseEnv(see: _ => { seen = true; return 0.2; }), out bool go));
        Assert.True(go); Assert.True(seen);
        Assert.Equal(pan, within.PanAngleRad, 5);
        Assert.False(within.Byte0x179);
        Assert.Equal(PanAndTiltAction.Running, within.CheckIfDone());          // the compound's Update()

        seen = false;
        var beyond = new TurnTowardsPoseCompound(target, 0.3) { CompoundUpdate = _ => PanAndTiltAction.Running };
        Assert.Equal(0u, beyond.InitPose(PoseEnv(see: _ => { seen = true; return 0.2; }), out go));
        Assert.False(go); Assert.False(seen);
        Assert.True(beyond.Byte0x179);
        Assert.Equal(0.0, beyond.PanAngleRad);
        Assert.Equal(0.0, beyond.HeadAngleRad);
        Assert.Equal(0u, beyond.CheckIfDone());                               // 0x0054B011: 0 when +0x179 is set

        var noMax = new TurnTowardsPoseCompound(target, 0.0);
        Assert.Equal(0u, noMax.InitPose(PoseEnv(), out go));
        Assert.True(go);
        Assert.Equal(0.0, noMax.PanAngleRad);
        Assert.Equal(0.2, noMax.HeadAngleRad, 6);
        // +0x179 is cleared again by the next Init (step 1)
        beyond.SetPose(new Pose3d(Mat3.Identity, new Vec3(400, 50, 0)));
        Assert.Equal(0u, beyond.InitPose(PoseEnv(), out go));
        Assert.False(beyond.Byte0x179);
    }

    /// <summary>
    /// M13-020, Init step (5) 0x0054ABA6..0x0054ACBC: the head angle is clamped to [-0.4363323 (0xBEDF66F3), 0.7766715 (0x3F46D3F2)]; a failing
    /// ComputeHeadAngleToSeePose(pose, 0.01) uses GetAbsoluteHeadAngleToLookAtPose (0x0054B428) and clamps that.
    /// </summary>
    [Fact]
    public void M13_020_TurnTowardsPoseInitClampsTheHeadAngleAndFallsBackOnFailure()
    {
        var target = new Pose3d(Mat3.Identity, new Vec3(400, 0, 0));
        var hi = new TurnTowardsPoseCompound(target, Math.PI); hi.InitPose(PoseEnv(see: _ => 1.0), out _);
        Assert.Equal(0.7766715, hi.HeadAngleRad, 6);
        var lo = new TurnTowardsPoseCompound(target, Math.PI); lo.InitPose(PoseEnv(see: _ => -1.0), out _);
        Assert.Equal(-0.4363323, lo.HeadAngleRad, 6);
        var fallback = new TurnTowardsPoseCompound(target, Math.PI);
        Assert.Equal(0u, fallback.InitPose(PoseEnv(see: _ => null, absolute: v => v.X > 100 ? 0.3 : 0.0), out bool go));
        Assert.True(go);
        Assert.Equal(0.3, fallback.HeadAngleRad, 6);
        Assert.Equal(0.01f, TurnTowardsPoseCompound.SeePoseToleranceRad);       // 0x3C23D70A
    }

    /// <summary>
    /// M13-021 (RECOVERABLE_GAP): Robot::ComputeHeadAngleToSeePose is unread, so with no seam supplied Init refuses instead of choosing an angle. GetAbsoluteHeadAngleToLookAtPose 0x0054B428 is built
    /// (R-FIX2): when the see-pose lookup fails (0x0054AB1A), Init falls back to it with the robot-relative translation (400, 0, 0): its binary32 result 0xBD45C00E (emulate_head_angle.py), inside the head range.
    /// </summary>
    [Fact]
    public void M13_021_TheUnreadHeadAngleLookupsAreRefusedWhenNoSeamIsSupplied()
    {
        var target = new Pose3d(Mat3.Identity, new Vec3(400, 0, 0));
        var noSee = new TurnTowardsPoseEnv(Pose3d.Identity, 0, null, null);
        Assert.Contains("M13-021", Assert.Throws<NotSupportedException>(() => new TurnTowardsPoseCompound(target, Math.PI).InitPose(noSee, out _)).Message);
        var failingSee = new TurnTowardsPoseEnv(Pose3d.Identity, 0, _ => null, null);
        var compound = new TurnTowardsPoseCompound(target, Math.PI);
        Assert.Equal(0u, compound.InitPose(failingSee, out _));
        Assert.Equal(0xBD45C00Eu, BitConverter.SingleToUInt32Bits((float)compound.HeadAngleRad));
    }

    /// <summary>
    /// M13-020, PanAndTiltAction::Init 0x00549CD0..0x00549CEE: a relative head angle is Anki::operator+(float, Radians const&amp;) of robot[+0x2FC] and
    /// [+0x11C], which normalises: 3.0 + 0.5 = 3.5 is 3.5 - 2pi = -2.7832 rad.
    /// </summary>
    [Fact]
    public void M13_020_TheRelativeHeadAngleIsNormalisedByRadiansOperatorPlus()
    {
        var a = Pan(headAbs: false); a.CompoundUpdate = _ => 0;
        a.HeadAngleRad = 0.5;
        a.Init(robotHeadAngleRad: 3.0);
        Assert.Equal(3.5 - 2 * Math.PI, Assert.IsType<MoveHeadToAngleSpec>(a.Children[1]).AngleRad, 5);
    }

    // ------------------------------------------------------------------ M13-022

    private sealed class FakeRobot : IPanTiltRobot
    {
        public bool OnTreads { get; set; } = true;
        public uint OriginId { get; set; } = 1;
        public float PoseAngleAroundZ { get; set; }
        public float HeadAngle { get; set; }
        public bool BodyMoving { get; set; }
        public bool HeadMoving { get; set; }
        public uint TurnResult, HeadResult;
        public readonly List<(float Target, float Speed, float Accel, float Tol, ushort HalfRevs, bool Abs)> Turns = new();
        public readonly List<(float Angle, float Speed, float Accel, float Duration)> HeadMoves = new();
        public uint TurnInPlace(float t, float s, float a, float tol, ushort h, bool abs, out byte id) { Turns.Add((t, s, a, tol, h, abs)); id = 7; return TurnResult; }
        public uint MoveHeadToAngle(float angle, float s, float a, float d, out byte id) { HeadMoves.Add((angle, s, a, d)); id = 9; return HeadResult; }
    }

    /// <summary>
    /// M13-022, TurnInPlaceAction ctor 0x005459D4: type 0x28, tracks 4, +0x78 = 5.235988 (0x40A78D36), +0x7C = 10.0 (0x41200000), +0x80 = 25.0 (0x41C80000),
    /// +0xB0 = Radians(2 deg) (0x3D0EFA35), +0xC4 = [+0x78], +0xC8 = [+0x7C], +0xCC = 0, +0xD8 = 1, motorActionAck tag 0xC4.
    /// </summary>
    [Fact]
    public void M13_022_TurnInPlaceConstructorAndSetters()
    {
        var t = new TurnInPlaceAction(new FakeRobot(), 1.0f, false);
        Assert.Equal((0x28, 4, 0xC4), (TurnInPlaceAction.ActionType, TurnInPlaceAction.TracksToLock, TurnInPlaceAction.MotorActionAckTag));
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x40A78D36), TurnInPlaceAction.DefaultMaxSpeed);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x41200000), TurnInPlaceAction.DefaultAccel);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x41C80000), TurnInPlaceAction.MaxRevolutions);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x3D0EFA35), t.Tolerance0xB0);
        Assert.Equal((TurnInPlaceAction.DefaultMaxSpeed, TurnInPlaceAction.DefaultAccel, false, true), (t.Speed0xC4, t.Accel0xC8, t.Byte0xCC, t.Byte0xD8));
        // SetMaxSpeed 0x00545C08: above the limit -> +0xCC and the limit with the caller's sign; 0 restores +0x78 and leaves +0xCC; else +0xCC = 1, +0xC4 = speed
        t.SetMaxSpeed(0); Assert.False(t.Byte0xCC); Assert.Equal(TurnInPlaceAction.DefaultMaxSpeed, t.Speed0xC4);
        t.SetMaxSpeed(6.0f); Assert.True(t.Byte0xCC); Assert.Equal(TurnInPlaceAction.DefaultMaxSpeed, t.Speed0xC4);
        t.SetMaxSpeed(-6.0f); Assert.Equal(-TurnInPlaceAction.DefaultMaxSpeed, t.Speed0xC4);
        t.SetMaxSpeed(2.0f); Assert.Equal(2.0f, t.Speed0xC4);
        t.SetMaxSpeed(0); Assert.Equal(TurnInPlaceAction.DefaultMaxSpeed, t.Speed0xC4);
        // SetAccel 0x00545D14
        t.SetAccel(3.0f); Assert.Equal(3.0f, t.Accel0xC8);
        t.SetAccel(0); Assert.Equal(TurnInPlaceAction.DefaultAccel, t.Accel0xC8);
        // SetTolerance 0x00545D50: |tol|, raised to 2 degrees when below
        var logs = new List<string>(); t.Log += logs.Add;
        t.SetTolerance(-0.2f); Assert.Equal(0.2f, t.Tolerance0xB0);
        t.SetTolerance(0.01f); Assert.Equal(TurnInPlaceAction.MinToleranceRad, t.Tolerance0xB0); Assert.DoesNotContain(logs, l => l.Contains("UseDefault"));
        t.SetTolerance(0f); Assert.Equal(TurnInPlaceAction.MinToleranceRad, t.Tolerance0xB0); Assert.Contains(logs, l => l.Contains("UseDefault"));
    }

    /// <summary>
    /// M13-022, TurnInPlaceAction::Init 0x00545FA0: invalid off-treads -> 0x0300000A; relative |angle| above +0x80*2pi (25 * 6.2831855) -> 0x03000000; relative
    /// target = current + angle (Radians), halfRevs = floor(|angle|/pi), the speed's sign bit is the angle's sign; absolute target = Radians(angle), halfRevs 0;
    /// in position -> 0 without sending; TurnInPlace failing -> 0x03000016.
    /// </summary>
    [Fact]
    public void M13_022_TurnInPlaceInit()
    {
        var robot = new FakeRobot { OnTreads = false };
        Assert.Equal(0x0300000Au, new TurnInPlaceAction(robot, 1.0f, false).Init());
        robot.OnTreads = true; robot.PoseAngleAroundZ = 0.5f;
        Assert.Equal(0x03000000u, new TurnInPlaceAction(robot, 160f, false).Init());          // 160 > 25 * 2pi = 157.08
        Assert.Empty(robot.Turns);
        // relative +1.0: target 1.5, half revolutions floor(1.0/pi) = 0
        Assert.Equal(0u, new TurnInPlaceAction(robot, 1.0f, false).Init());
        Assert.Equal((1.5f, TurnInPlaceAction.DefaultMaxSpeed, TurnInPlaceAction.DefaultAccel, TurnInPlaceAction.MinToleranceRad, (ushort)0, false), robot.Turns[0]);
        // relative -7.0: target 0.5 - 7.0 = -6.5 -> -6.5 + 2pi = -0.21681, half revolutions floor(7/pi) = 2, the speed carries the sign bit
        Assert.Equal(0u, new TurnInPlaceAction(robot, -7.0f, false).Init());
        var (target, speed, _, _, halfRevs, isAbs) = robot.Turns[1];
        Assert.Equal(0.5 - 7.0 + 2 * Math.PI, target, 5);
        Assert.Equal(-TurnInPlaceAction.DefaultMaxSpeed, speed);
        Assert.Equal((ushort)2, halfRevs); Assert.False(isAbs);
        // absolute 4.0 -> Radians(4.0) = 4.0 - 2pi, half revolutions 0
        Assert.Equal(0u, new TurnInPlaceAction(robot, 4.0f, true).Init());
        Assert.Equal(4.0 - 2 * Math.PI, robot.Turns[2].Target, 5);
        Assert.Equal((ushort)0, robot.Turns[2].HalfRevs); Assert.True(robot.Turns[2].Abs);
        // already in position: 0, nothing sent
        Assert.Equal(0u, new TurnInPlaceAction(robot, 0.5f, true).Init());
        Assert.Equal(3, robot.Turns.Count);
        // the send fails
        robot.TurnResult = 1;
        Assert.Equal(0x03000016u, new TurnInPlaceAction(robot, 1.0f, false).Init());
    }

    /// <summary>
    /// M13-022, TurnInPlaceAction::CheckIfDone 0x0054642C: RUNNING until the ack of the sent TurnInPlace (0x0054D3F8); then in position -> 0; the body having
    /// moved and then stopped short -> 0x04000004 (0x00546818); an invalid off-treads state at the tail -> 0x0300000A (0x00545EC4).
    /// </summary>
    [Fact]
    public void M13_022_TurnInPlaceCheckIfDone()
    {
        var robot = new FakeRobot { PoseAngleAroundZ = 0f };
        var t = new TurnInPlaceAction(robot, 1.0f, false);
        Assert.Equal(0u, t.Init());
        Assert.Equal(PanTiltResult.Running, t.CheckIfDone());                 // no ack yet
        t.HandleMotorActionAck(3);                                             // another action's ack
        Assert.Equal(PanTiltResult.Running, t.CheckIfDone());
        t.HandleMotorActionAck(7);                                             // the FakeRobot's actionId
        robot.BodyMoving = true; robot.PoseAngleAroundZ = 0.4f;
        Assert.Equal(PanTiltResult.Running, t.CheckIfDone());                 // moving: +0x85 set
        robot.BodyMoving = false;                                              // stopped at 0.4 of 1.0
        Assert.Equal(PanTiltResult.MotorStoppedMakingProgress, t.CheckIfDone());
        // a second run that reaches the angle
        var r2 = new FakeRobot { PoseAngleAroundZ = 0f };
        var t2 = new TurnInPlaceAction(r2, 1.0f, false); t2.Init(); t2.HandleMotorActionAck(7);
        r2.PoseAngleAroundZ = 1.0f;
        Assert.Equal(PanTiltResult.Success, t2.CheckIfDone());
        r2.OnTreads = false;
        Assert.Equal(0x0300000Au, t2.CheckIfDone());                          // the tail check
    }

    /// <summary>
    /// M13-022, MoveHeadToAngleAction ctor 0x00547E40: type 0x12, tracks 1, +0x90 = 15.0, +0x94 = 20.0, +0x9C = 1, +0x9D = 0; the angle is clamped to
    /// [-0.4363323, 0.7766715] and the tolerance raised to 0.0349066 (2 degrees).
    /// </summary>
    [Fact]
    public void M13_022_MoveHeadConstructorClampsTheAngleAndTolerance()
    {
        var m = new MoveHeadToAngleAction(new FakeRobot(), 1.0f, 0.001f, 0f);
        Assert.Equal((0x12, 1), (MoveHeadToAngleAction.ActionType, MoveHeadToAngleAction.TracksToLock));
        Assert.Equal(0.7766715f, m.Angle0x78);
        Assert.Equal(BitConverter.Int32BitsToSingle(0x3D0EFA35), m.Tolerance0x80);      // the literal at 0x00548338
        Assert.Equal((15.0f, 20.0f, true, false), (m.Speed0x90, m.Accel0x94, m.Byte0x9C, m.Byte0x9D));
        Assert.Equal(BitConverter.Int32BitsToSingle(unchecked((int)0xBEDF66F3)), new MoveHeadToAngleAction(new FakeRobot(), -1.0f, 0.1f, 0f).Angle0x78);   // the engine's -0.4363323 literal
        var ok = new MoveHeadToAngleAction(new FakeRobot(), 0.3f, 0.1f, 0f);
        Assert.Equal((0.3f, 0.1f), (ok.Angle0x78, ok.Tolerance0x80));
        // a variability above 0 draws the engine's RNG, which is unread: it is refused unless one is supplied
        Assert.Throws<NotSupportedException>(() => new MoveHeadToAngleAction(new FakeRobot(), 0.3f, 0.1f, 0.1f));
        Assert.Equal(0.4f, new MoveHeadToAngleAction(new FakeRobot(), 0.3f, 0.1f, 0.1f, v => 0.1f).Angle0x78, 5);
    }

    /// <summary>
    /// M13-022, MoveHeadToAngleAction::Init 0x00548534 and CheckIfDone 0x005485CC: in position -> 0 without sending; else MoveHeadToAngle(angle, 15.0, 20.0,
    /// 0); a failing send -> 0x03000016 and the +0xA0 write (0.5 * |target - head|, when +0x9C and not +0x9D) also happens on that path; CheckIfDone is RUNNING
    /// until the ack (0x0054D624), 0 when in position and still, 0x04000004 when the head moved, stopped and is not in position.
    /// </summary>
    [Fact]
    public void M13_022_MoveHeadInitAndCheckIfDone()
    {
        var robot = new FakeRobot { HeadAngle = 0.3f };
        var inPos = new MoveHeadToAngleAction(robot, 0.3f, 0.1f, 0f);
        Assert.Equal(0u, inPos.Init());
        Assert.Empty(robot.HeadMoves);
        var m = new MoveHeadToAngleAction(robot, 0.6f, 0.1f, 0f);
        Assert.Equal(0u, m.Init());
        Assert.Equal((0.6f, 15.0f, 20.0f, 0f), robot.HeadMoves[0]);
        Assert.Equal(0.5f * MathF.Abs(0.6f - 0.3f), m.Float0xA0, 5);
        Assert.Equal(PanTiltResult.Running, m.CheckIfDone());                 // no ack
        m.HandleMotorActionAck(9);
        robot.HeadMoving = true; robot.HeadAngle = 0.4f;
        Assert.Equal(PanTiltResult.Running, m.CheckIfDone());
        robot.HeadMoving = false;                                              // stopped short of 0.6
        Assert.Equal(PanTiltResult.MotorStoppedMakingProgress, m.CheckIfDone());
        var r2 = new FakeRobot { HeadAngle = 0.0f };
        var m2 = new MoveHeadToAngleAction(r2, 0.5f, 0.1f, 0f); m2.Init(); m2.HandleMotorActionAck(9);
        r2.HeadAngle = 0.5f; r2.HeadMoving = true;
        Assert.Equal(PanTiltResult.Running, m2.CheckIfDone());                // in position but still moving
        r2.HeadMoving = false;
        Assert.Equal(PanTiltResult.Success, m2.CheckIfDone());
        var failing = new FakeRobot { HeadAngle = 0.0f, HeadResult = 1 };
        var m3 = new MoveHeadToAngleAction(failing, 0.5f, 0.1f, 0f);
        Assert.Equal(0x03000016u, m3.Init());
        Assert.Equal(0.25f, m3.Float0xA0, 5);                                  // written on the failure path too
        Assert.False(m3.Byte0xAA);                                             // but the sent flag is not
    }

}
