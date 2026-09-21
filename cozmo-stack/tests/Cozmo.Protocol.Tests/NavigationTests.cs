using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M13: the lattice planner over the shipped motion primitives, the charger object and the mount / drive-off
/// actions, the flip action, block configurations, the whiteboard's beacons, the workouts, and the behaviours
/// built on them, all on the fake robot side of <see cref="Rig"/>.
/// </summary>
public class NavigationTests
{
    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;

    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (File.Exists(Path.Combine(r, MotionPrimitiveSet.ObbRelativePath))) return r;
            d = d.Parent;
        }
        return null;
    }

    private static MotionPrimitiveSet? Prims() { var obb = ObbRoot(); return obb is null ? null : MotionPrimitiveSet.FromObb(obb); }

    private static Pose3d CubeAt(double x, double y, double yaw = 0, double z = CubeGeometry.CubeSizeMm / 2) => new(Mat3.AboutZ(yaw), new Vec3(x, y, z));
    private static Pose3d At(double x, double y, double heading) => new(Mat3.AboutZ(heading), new Vec3(x, y, 0));

    private static void SpinUntil(Func<bool> cond, Action? tick = null, int ms = 8000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cond()) { tick?.Invoke(); if (sw.ElapsedMilliseconds > ms) throw new TimeoutException("condition not met"); Thread.Sleep(5); }
    }

    private static BehaviorContext Ctx(Rig rig) => new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };

    /// <summary><c>IsRunnable</c> also needs animation assets, which these tests do not load; this asks the class's own gate.</summary>
    private static bool Runnable(SteppedBehavior b, BehaviorContext ctx) =>
        (bool)typeof(SteppedBehavior).GetMethod("IsRunnableInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(b, new object[] { ctx })!;

    private static void RunToEnd(Rig rig, SteppedBehavior b, BehaviorContext ctx, Func<bool>? frames = null, int ms = 10000, double stepMs = 33)
    {
        double t = 0;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (b.Update(ctx, t))
        {
            rig.Pump();
            if (frames?.Invoke() ?? false) rig.Frame();
            t += stepMs;
            if (sw.ElapsedMilliseconds > ms) throw new TimeoutException("behaviour did not finish: " + string.Join(" | ", b.Trace));
            Thread.Sleep(2);
        }
        b.Stop(BehaviorStopReason.Completed);
    }

    // ------------------------------------------------------------------ motion primitives and the planner

    [Fact]
    public void TheShippedMotionPrimitivesParseAsTheEngineReadsThem()
    {
        var prims = Prims();
        if (prims is null) return;
        Assert.Equal(10.0, prims.ResolutionMm);
        Assert.Equal(16, prims.NumAngles);
        Assert.Equal(16, prims.Angles.Count);
        Assert.Equal(Math.Atan2(1, 2), prims.Angles[1], 9);
        Assert.Equal(9, prims.Actions.Count);
        Assert.Equal("short straight", prims.Actions[0].Name); Assert.Equal(1.0001, prims.Actions[0].ExtraCostFactor);
        Assert.True(prims.Actions[8].Reverse); Assert.Equal(1.2, prims.Actions[8].ExtraCostFactor);
        Assert.Equal(2.0, prims.Actions[6].ExtraCostFactor);                     // in-place turns cost double
        Assert.Equal(16, prims.ByAngle.Count);
        var longStraight = prims.ByAngle[0].Single(p => p.ActionIndex == 1);
        Assert.Equal((5, 0, 0), (longStraight.EndX, longStraight.EndY, longStraight.EndTheta));
        Assert.Equal(50.0, longStraight.LengthMm, 6);
        var slightLeft = prims.ByAngle[0].Single(p => p.ActionIndex == 2);
        Assert.Equal((5, 1, 1), (slightLeft.EndX, slightLeft.EndY, slightLeft.EndTheta));
        Assert.Equal(4, prims.ThetaIndex(Math.PI / 2));
        Assert.Equal(15, prims.ThetaIndex(-0.4));
    }

    [Fact]
    public void AnEmptyWorldPlansAStraightRunOfLongStraights()
    {
        var prims = Prims();
        if (prims is null) return;
        var planner = new LatticePlanner(new LatticeEnvironment(prims));
        var plan = planner.ComputePath(new LatticeState(0, 0, 0), new[] { new LatticeState(30, 0, 0) });
        Assert.NotNull(plan);
        Assert.All(plan!.Actions, a => Assert.Equal(1, a.ActionIndex));            // six long straights, ties broken by the 1.0001 factor
        Assert.Equal(6, plan.Actions.Count);
        Assert.Equal(300.0, plan.Cost, 3);
        var path = planner.ToPath(plan, At(300, 0, 0), PathMotionProfile.Default);
        var line = Assert.IsType<PathSegment.Line>(Assert.Single(path));
        Assert.Equal((0, 0, 300, 0), (line.FromX, line.FromY, line.ToX, line.ToY));
    }

    [Fact]
    public void TurningPrimitivesBecomeLineAndArcSegmentsThatEndOnTheLattice()
    {
        var prims = Prims();
        if (prims is null) return;
        var env = new LatticeEnvironment(prims);
        var planner = new LatticePlanner(env);
        // one slight-left primitive: (0,0,θ0) -> (5,1,θ1)
        var prim = prims.ByAngle[0].Single(p => p.ActionIndex == 2);
        var plan = new LatticePlan(new LatticeState(0, 0, 0), new[] { prim }, prim.Cost, 0);
        var path = planner.ToPath(plan, null, PathMotionProfile.Default);
        Assert.Equal(2, path.Count);
        var line = Assert.IsType<PathSegment.Line>(path[0]);
        var arc = Assert.IsType<PathSegment.Arc>(path[1]);
        Assert.Equal(prims.Angles[1], arc.SweepRad, 6);
        // following the line then the arc lands exactly on the primitive's end cell with its heading
        double ex = arc.CenterX + arc.RadiusMm * Math.Cos(arc.StartAngleRad + arc.SweepRad), ey = arc.CenterY + arc.RadiusMm * Math.Sin(arc.StartAngleRad + arc.SweepRad);
        Assert.Equal(50.0, ex, 3); Assert.Equal(10.0, ey, 3);
        Assert.Equal(line.ToX, arc.CenterX + arc.RadiusMm * Math.Cos(arc.StartAngleRad), 6);
        // and a plan to a goal with a heading off the lattice ends with a point turn to it
        var full = planner.PlanTo(At(0, 0, 0), new[] { At(200, 100, 0.3) }, PathMotionProfile.Default);
        Assert.NotNull(full);
        Assert.IsType<PathSegment.PointTurn>(full!.Value.Path[^1]);
        Assert.Equal(0.3, ((PathSegment.PointTurn)full.Value.Path[^1]).TargetAngleRad, 6);
    }

    [Fact]
    public void ThePlannerRoutesAroundACubeInTheWay()
    {
        var prims = Prims();
        if (prims is null) return;
        var env = new LatticeEnvironment(prims);
        env.AddRectangleObstacle(At(150, 0, 0), CubeGeometry.CubeSizeMm, CubeGeometry.CubeSizeMm, "cube");
        Assert.Equal(1, env.ObstacleCount);
        Assert.True(env.IsInCollision(150, 0));
        Assert.True(env.IsInCollision(150, 60));                                   // the robot's radius is added
        Assert.False(env.IsInCollision(150, 90));
        Assert.True(env.IsInSoftCollision(150, 75));
        var planner = new LatticePlanner(env);
        var res = planner.PlanTo(At(0, 0, 0), new[] { At(300, 0, 0) }, PathMotionProfile.Default);
        Assert.NotNull(res);
        var (plan, path, _) = res!.Value;
        foreach (var s in plan.States()) Assert.False(env.IsInCollision(s.X * 10, s.Y * 10), $"state {s} collides");
        Assert.Contains(plan.Actions, a => a.EndTheta != a.StartTheta);            // it had to turn
        Assert.True(path.Count >= 3);
        // the goal itself inside an obstacle is rejected
        Assert.Null(planner.PlanTo(At(0, 0, 0), new[] { At(150, 0, 0) }, PathMotionProfile.Default));
    }

    [Fact]
    public void DriveToPoseUsesTheLatticePlannerAndTheFakeRobotArrives()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        Assert.True(rig.M.LoadPlanner(obb));
        var drive = new DriveToPoseAction(rig.M) { Goal = At(250, 120, Math.PI / 2) };
        var task = drive.RunAsync(default);
        SpinUntil(() => task.IsCompleted, () => rig.Pump());
        Assert.Equal(ActionResult.Success, task.Result);
        Assert.Contains(drive.Trace, l => l.Contains("lattice plan"));
        Assert.InRange(rig.X, 240, 260); Assert.InRange(rig.Y, 110, 130);
        Assert.InRange(Math.Abs(StraightLinePlanner.Wrap(rig.Angle - Math.PI / 2)), 0, 0.05);
        Assert.Contains(rig.Sent, m => m is AppendPathSegmentArc);
    }

    [Fact]
    public void DriveToObjectWithThePlannerTreatsOtherCubesAsObstaclesButNotTheTarget()
    {
        var obb = ObbRoot();
        if (obb is null || Lib is null) return;
        using var rig = new Rig();
        Assert.True(rig.M.LoadPlanner(obb));
        rig.Cube = CubeAt(260, 0);
        Assert.Single(rig.Frame().Objects);
        var drive = new DriveToObjectAction(rig.M, 7, PreActionType.Docking);
        var task = drive.RunAsync(default);
        SpinUntil(() => task.IsCompleted, () => rig.Pump());
        Assert.Equal(ActionResult.Success, task.Result);
        Assert.Equal(0, rig.M.Planner!.Env.ObstacleCount);                         // the target is not an obstacle
        Assert.InRange(rig.X, 150, 175);                                            // the front pre-dock pose, 97 mm out
    }

    // ------------------------------------------------------------------ the charger

    [Fact]
    public void TheChargerIsAPassiveObjectWithOneRectangularMarker()
    {
        var lk = CubeGeometry.LookupMarker(MarkerType.Charger);
        Assert.NotNull(lk);
        Assert.Equal(ObjectType.Charger_Basic, lk!.Value.Type);
        var m = lk.Value.Marker;
        Assert.Equal(20.0, m.SizeMm); Assert.Equal(27.0, m.HeightMm);
        var corners = m.CornersOnObject();
        Assert.Equal(20.0, (corners[2] - corners[0]).Length, 6);                   // TL -> TR is the width
        Assert.Equal(27.0, (corners[0] - corners[1]).Length, 6);                   // TL -> BL is the height
        var normal = m.NormalOnObject;
        Assert.Equal(-1.0, normal.X, 6);                                            // facing out of the charger
        Assert.Equal(86.0, m.PoseOnObject.Translation.X, 6); Assert.Equal(11.0, m.PoseOnObject.Translation.Z, 6);
        Assert.Equal(new Vec3(96, 80, 31), CubeGeometry.SizeOf(ObjectType.Charger_Basic));
        var docked = ChargerGeometry.DockedRobotPose(At(200, 0, 0));
        Assert.Equal(230.0, docked.Translation.X, 6);
        Assert.Equal(Math.PI, Math.Abs(docked.AngleAroundZ), 6);
        Assert.False(CubeGeometry.IsActiveObjectType(ObjectType.Charger_Basic));
    }

    [Fact]
    public void AChargerInViewIsLocalisedWithoutBeingConnected()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Head = -0.2f;
        rig.Charger = At(200, 0, 0);                                                 // lip at 200, marker at 286 facing the robot
        var r = rig.Frame();
        var obs = Assert.Single(r.Objects);
        Assert.Equal(ObjectType.Charger_Basic, obs.Object.Type);
        Assert.Equal(ChargerGeometry.ObjectId, obs.Object.ObjectId);
        Assert.Equal(ObjectFamily.Charger, obs.Object.Family);
        Assert.InRange(obs.Object.Pose.Translation.X, 190, 210);
        Assert.InRange(Math.Abs(obs.Object.Pose.Translation.Y), 0, 8);
        Assert.InRange(Math.Abs(StraightLinePlanner.Wrap(obs.Object.Pose.AngleAroundZ)), 0, 0.1);
    }

    [Fact]
    public void MountChargerAlignsTurnsAndBacksOntoTheContacts()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Head = -0.2f;
        rig.Charger = At(200, 0, 0);
        Assert.Single(rig.Frame().Objects);
        rig.DockOutcome = BlockStatus.NoBlock;                                       // an align reports no block
        var mount = new MountChargerAction(rig.M, ChargerGeometry.ObjectId);
        var task = mount.RunAsync(default);
        SpinUntil(() => task.IsCompleted, () => { rig.Pump(); if (!rig.OnCharger) rig.Frame(); }, 15000);
        Assert.Equal(ActionResult.Success, task.Result);
        Assert.True(rig.OnCharger);
        Assert.True(rig.Robot.Sensors.OnCharger);
        var dock = rig.Sent.OfType<DockWithObject>().First();
        Assert.Equal((byte)DockAction.Align, dock.ToBytes()[17]);
        // the align distance is the custom 120 mm less the 27 mm finger-to-origin offset, in the error signal's x
        Assert.Contains(mount.Trace, l => l.Contains("on the charger"));
        Assert.Contains(rig.Sent, m => m is AppendPathSegmentPointTurn);
        var back = rig.Sent.OfType<AppendPathSegmentLine>().Last();
        Assert.Equal(-30f, back.Speed.SpeedMmps);                                  // backwards at 30 mm/s
        Assert.Equal(1, mount.Attempts);
    }

    [Fact]
    public void DriveOffChargerDrivesTheChargerLengthPlusTheExtraAndFiresTheEvent()
    {
        using var rig = new Rig();
        rig.Charger = At(-30, 0, 0);                                                 // the robot sits docked, facing out of the lip
        rig.Angle = (float)Math.PI;
        rig.OnCharger = true; rig.State();
        var ctx = Ctx(rig);
        var b = new DriveOffChargerBehavior(rig.M, "DriveOffCharger", 60);
        Assert.True(Runnable(b, ctx));
        Assert.Equal(156.0, b.DistanceMm);
        RunToEnd(rig, b, ctx);
        var line = rig.Sent.OfType<AppendPathSegmentLine>().Single();
        Assert.Equal(156f, Math.Abs(line.XEndMm - line.XStartMm), 0);
        Assert.Equal(20f, line.Speed.SpeedMmps);
        Assert.False(rig.OnCharger);
        Assert.Contains(b.Trace, l => l.Contains("emotion event DriveOffCharger"));
        Assert.False(Runnable(b, ctx));
    }

    [Fact]
    public void ReactToOnChargerPlaysThenAnnouncesSleepAndDisconnectOnItsTimers()
    {
        using var rig = new Rig();
        rig.OnCharger = true; rig.State();
        var ctx = Ctx(rig);
        var b = new ReactToOnChargerBehavior("ReactToOnCharger", 300, 330);
        Assert.True(Runnable(b, ctx));
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        double t = 0;
        Assert.True(b.Update(ctx, t));
        for (t = 33; t < 299_000; t += 1000) Assert.True(b.Update(ctx, t));
        Assert.Empty(b.Broadcasts);
        Assert.True(b.Update(ctx, 301_000));
        Assert.Equal(new[] { "GoingToSleep" }, b.Broadcasts);
        Assert.True(b.Update(ctx, 331_000));
        Assert.Equal(new[] { "GoingToSleep", "StartIdleTimeout" }, b.Broadcasts);
        rig.OnCharger = false; rig.State();
        Assert.False(b.Update(ctx, 332_000));
        Assert.Contains(b.Trace, l => l.Contains("PlacedOnCharger"));
        var vc = new ReactToOnChargerBehavior("VC_GoToSleep", 300, 330, triggeredFromVoiceCommand: true);
        Assert.False(Runnable(vc, ctx));
        vc.Requested = true;
        Assert.True(Runnable(vc, ctx));
    }

    // ------------------------------------------------------------------ the flip action

    [Fact]
    public void FlipDrivesThroughTheCubeRaisingTheLiftAndForgetsItsPose()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(200, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var flip = new FlipBlockAction(rig.M, 7) { CheckPreActionPose = false };
        var task = flip.RunAsync(default);
        SpinUntil(() => task.IsCompleted, () => rig.Pump());
        Assert.Equal(ActionResult.Success, task.Result);
        var line = rig.Sent.OfType<AppendPathSegmentLine>().Single();
        Assert.InRange(line.XEndMm, 217, 224);          // distance + 20
        Assert.Equal(150f, line.Speed.SpeedMmps);
        Assert.Equal(new[] { 40f, LiftPresets.CarryMm }, rig.LiftHeights);
        Assert.True(flip.LiftRaised);
        Assert.Equal(PoseState.Unknown, obj.PoseState);
        // with the check on, a robot away from every flipping pose is refused
        rig.Cube = CubeAt(400, 100); rig.X = 0; rig.Y = 0; rig.Angle = 0; rig.State(); rig.Frame();
        var strict = new FlipBlockAction(rig.M, 7);
        Assert.Equal(ActionResult.DidNotReachPreActionPose, strict.RunAsync(default).GetAwaiter().GetResult());
    }

    // ------------------------------------------------------------------ block configurations

    private static ObservableObject Cube(uint id, ObjectType type, Pose3d pose) => new(id, type, CubeGeometry.CubeMarkers(type)) { Pose = pose, PoseState = PoseState.Known };

    [Fact]
    public void StacksBasesAndPyramidsAreRecognisedFromCubePoses()
    {
        var world = new BlockWorld(() => Array.Empty<(uint, ObjectType)>()) { AllowUnconnectedObjects = true };
        var cubes = new[]
        {
            Cube(1, ObjectType.Block_LIGHTCUBE1, CubeAt(200, 0)), Cube(2, ObjectType.Block_LIGHTCUBE2, CubeAt(203, 2, 0.05, 66)), Cube(3, ObjectType.Block_LIGHTCUBE3, CubeAt(198, -1, 0, 110)),
        };
        var stack = BlockConfigurationManager.FindObjectOnTopOrUnderneath(cubes[0], cubes, onTop: true);
        Assert.Equal(2u, stack!.ObjectId);
        var mgr = new BlockConfigurationManager(world, () => 0);
        var built = mgr.BuildTallestStackForObject(cubes[1], cubes);
        Assert.Equal(new uint[] { 1, 2, 3 }, built.BlockIds);
        Assert.Equal(3, built.StackHeight);
        // a pyramid base: two upright cubes 50 mm apart on the ground; a third one cube up over the midpoint tops it
        var a = Cube(1, ObjectType.Block_LIGHTCUBE1, CubeAt(300, 25)); var b = Cube(2, ObjectType.Block_LIGHTCUBE2, CubeAt(300, -25));
        Assert.True(BlockConfigurationManager.BlocksFormPyramidBase(a, b, out var mid));
        Assert.Equal(new Vec3(300, 0, 22), mid);
        Assert.False(BlockConfigurationManager.BlocksFormPyramidBase(a, Cube(2, ObjectType.Block_LIGHTCUBE2, CubeAt(300, -50)), out _));   // 75 mm apart
        Assert.False(BlockConfigurationManager.BlocksFormPyramidBase(a, Cube(2, ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.AboutX(Math.PI / 2), new Vec3(300, -25, 22))), out _));  // on its side
    }

    [Fact]
    public void TheConfigurationManagerTracksWhatTheWorldSeesAndWhenItFirstSawIt()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        double clock = 100;
        rig.M.ClockSec = () => clock;
        rig.Head = 0.05f;
        rig.Cube = CubeAt(260, 40);
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, CubeAt(260, -12)));
        var r = rig.Frame();
        Assert.Equal(2, r.Objects.Count);
        var bases = rig.M.Configurations.PyramidBases;
        var pb = Assert.Single(bases);
        Assert.Equal(100, rig.M.Configurations.FirstSeenSec(pb));
        Assert.True(rig.M.Configurations.IsObjectPartOfConfigurationType(7, BlockConfigurationType.PyramidBase));
        Assert.Empty(rig.M.Configurations.Pyramids);
        // the third cube on top of the midpoint completes a pyramid
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE3, CubeAt(260, 14, 0, 66)));
        clock = 103;
        rig.Frame();
        Assert.Single(rig.M.Configurations.Pyramids);
        Assert.Equal(103, rig.M.Configurations.FirstSeenSec(rig.M.Configurations.Pyramids[0]));
        // OnConfigSeen: runnable within 5 s of the base being seen, not after
        var ctx = Ctx(rig);
        var seen = OnConfigSeenBehavior.RespondToPyramidBase(rig.M);
        clock = 104.5; Assert.True(Runnable(seen, ctx));
        clock = 105.5; Assert.False(Runnable(seen, ctx));
        clock = 104.5;
        RunToEnd(rig, seen, ctx);
        Assert.Contains(seen.Trace, l => l.Contains("BuildPyramidReactToBase"));
        Assert.False(Runnable(seen, ctx));                                          // reacted already
        // the reactions to a pyramid arm a 100 s cooldown and do nothing else
        var react = ReactToConfigurationBehavior.ReactToPyramid(rig.M);
        Assert.True(Runnable(react, ctx));
        RunToEnd(rig, react, ctx);
        Assert.Equal(204.5, react.NextAllowedSec);
        Assert.False(Runnable(react, ctx));
        clock = 205; Assert.True(Runnable(react, ctx));
    }

    // ------------------------------------------------------------------ the whiteboard and the workouts

    [Fact]
    public void BeaconsAndFailureMemoryDriveTheHikingBehaviours()
    {
        var world = new BlockWorld(() => Array.Empty<(uint, ObjectType)>());
        double clock = 0;
        var wb = new AIWhiteboard(world, () => clock);
        Assert.Null(wb.GetActiveBeacon());
        var beacon = wb.AddBeacon(At(0, 0, 0), 175);
        Assert.Same(beacon, wb.GetActiveBeacon());
        Assert.True(beacon.IsLocWithinBeacon(new Vec3(100, 100, 0)));
        Assert.False(beacon.IsLocWithinBeacon(new Vec3(180, 0, 0)));
        wb.SetFailedToUse(7, ObjectActionFailure.PickUpObject);
        Assert.True(wb.DidFailToUse(7, ObjectActionFailure.PickUpObject, 45));
        Assert.True(wb.DidFailToUse(7, ObjectActionFailure.Any, 45));
        Assert.False(wb.DidFailToUse(7, ObjectActionFailure.StackOnObject, 45));
        clock = 50;
        Assert.False(wb.DidFailToUse(7, ObjectActionFailure.PickUpObject, 45));
        wb.OnRobotDelocalized();
        Assert.Null(wb.GetActiveBeacon());
    }

    [Fact]
    public void ThinkAboutBeaconsThenBringCubeToBeaconPlacesTheCubeInside()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(330, -20);
        Assert.Single(rig.Frame().Objects);
        var ctx = Ctx(rig);
        var bring = new BringCubeToBeaconBehavior(rig.M, "Hiking_BringCubeToBeacon", 45);
        Assert.False(Runnable(bring, ctx));                                          // no beacon yet
        var think = new ThinkAboutBeaconsBehavior(rig.M, "Hiking_ThinkAboutBeacons", 175);
        Assert.True(Runnable(think, ctx));
        RunToEnd(rig, think, ctx);
        var beacon = rig.M.Whiteboard.GetActiveBeacon()!;
        Assert.Equal(175, beacon.RadiusMm);
        Assert.Contains(think.Trace, l => l.Contains("HikingReactToNewArea"));
        Assert.False(Runnable(think, ctx));
        Assert.True(Runnable(bring, ctx));                                           // the cube at 330 mm is outside the 175 mm beacon
        RunToEnd(rig, bring, ctx, frames: () => !rig.M.Docking.Carrying.IsCarryingObject);
        Assert.NotNull(bring.PlacedAt);
        Assert.True(beacon.IsLocWithinBeacon(bring.PlacedAt!.Value), $"placed at {bring.PlacedAt}");
        Assert.Contains(rig.Sent, m => m is PlaceObjectOnGround);
        Assert.False(rig.M.Docking.Carrying.IsCarryingObject);
    }

    [Fact]
    public void TheWorkoutConfigParsesAndScoresLiftsFromConfidence()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var w = WorkoutComponent.FromObb(obb);
        Assert.NotNull(w);
        Assert.Equal(4, w!.Workouts.Count);
        var high = w.Workouts[0];
        Assert.Equal(AnimationTrigger.WorkoutPreLift_highEnergy, high.PreLift);
        Assert.Equal("StrongWorkoutCompleted", high.EmotionEventOnComplete);
        Assert.Equal("PerformedStrongWorkout", high.AdditionalObjectiveOnComplete);
        Assert.Equal(5, high.GetNumStrongLifts(_ => 0));
        Assert.Equal(7, high.GetNumStrongLifts(_ => 0.3));
        Assert.Equal(2, high.GetNumStrongLifts(_ => -1));
        Assert.Equal(1, high.GetNumWeakLifts(_ => -0.2));
        Assert.Equal(w.Workouts[1], w.GetCurrentWorkout());                           // medium by default (LOCAL_POLICY)
        w.Selector = () => 0;
        Assert.Equal(high, w.GetCurrentWorkout());
    }

    [Fact]
    public void TheWorkoutBehaviourLiftsStrongThenWeakAndPutsTheCubeDown()
    {
        var obb = ObbRoot();
        if (obb is null || Lib is null) return;
        using var rig = new Rig();
        rig.M.Workouts = WorkoutComponent.FromObb(obb);
        rig.M.Workouts!.Selector = () => 0;
        rig.Cube = CubeAt(220, 10);
        Assert.Single(rig.Frame().Objects);
        var ctx = Ctx(rig);
        var b = new CubeLiftWorkoutBehavior(rig.M, "CubeLiftWorkout");
        Assert.True(Runnable(b, ctx));
        RunToEnd(rig, b, ctx, frames: () => !rig.M.Docking.Carrying.IsCarryingObject);
        Assert.Equal(5, b.StrongLiftsPlanned);                                        // Confident 0 without a mood
        Assert.Equal(1, b.WeakLiftsPlanned);
        Assert.Equal(5, b.Trace.Count(l => l.Contains("WorkoutStrongLift_highEnergy")));
        Assert.Equal(1, b.Trace.Count(l => l.Contains("WorkoutWeakLift_highEnergy")));
        Assert.Contains(b.Trace, l => l.Contains("WorkoutTransition_highEnergy"));
        Assert.Contains(b.Trace, l => l.Contains("WorkoutPutDown_highEnergy"));
        Assert.Contains(b.Trace, l => l.Contains("WorkoutPostLift_highEnergy"));
        Assert.Contains(b.Trace, l => l.Contains("PerformedWorkout, PerformedStrongWorkout"));
        Assert.Equal(1, rig.M.Workouts.CompletedWorkouts);
        Assert.False(rig.M.Docking.Carrying.IsCarryingObject);
    }

    // ------------------------------------------------------------------ the cube behaviours

    private static void Stack(Rig rig, double x, double y)
    {
        rig.Head = 0.12f;
        rig.Cube = CubeAt(x, y);
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, CubeAt(x, y, 0, 66)));
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE3, CubeAt(x, y, 0, 110)));
    }

    [Fact]
    public void KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        Stack(rig, 260, 0);
        var r = rig.Frame();
        Assert.Equal(3, r.Objects.Count);
        var stack = rig.M.Configurations.GetTallestStack();
        Assert.NotNull(stack); Assert.Equal(3, stack!.StackHeight);
        var ctx = Ctx(rig);
        var b = new KnockOverCubesBehavior(rig.M, "KnockOverCubes", 3);
        Assert.True(Runnable(b, ctx));
        Assert.False(Runnable(new KnockOverCubesBehavior(rig.M, "x", 4), ctx));
        RunToEnd(rig, b, ctx, frames: () => rig.M.World.GetLocatedObjectById(7) is not null && b.CurrentPhase == KnockOverCubesBehavior.Phase.KnockingOverStack);
        Assert.Contains(b.Trace, l => l.Contains("reach for block 7"));
        Assert.Contains(b.Trace, l => l.Contains("KnockOverGrabAttempt"));
        Assert.Contains(b.Trace, l => l.Contains("DriveAndFlipBlockAction(7)"));
        Assert.Contains(b.Trace, l => l.Contains("lift to carry height"));
        Assert.True(b.KnockedOver, string.Join(" | ", b.Trace));
        Assert.Contains(b.Trace, l => l.Contains("KnockOverSuccess"));
        Assert.Contains(b.Trace, l => l.Contains("KnockedOverBlocks"));
        // the reach: 85 mm short of the bottom block at 60 mm/s
        var reach = rig.Sent.OfType<AppendPathSegmentLine>().First();
        Assert.Equal(60f, reach.Speed.SpeedMmps);
        Assert.InRange(reach.XEndMm, 170, 180);
    }

    [Fact]
    public void PopAWheelieDrivesDocksAndReEnablesStopOnCliff()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(220, 0);
        Assert.Single(rig.Frame().Objects);
        rig.DockOutcome = BlockStatus.NoBlock;
        var ctx = Ctx(rig);
        var b = new PopAWheelieBehavior(rig.M, "PopAWheelie");
        Assert.True(Runnable(b, ctx));
        RunToEnd(rig, b, ctx, frames: () => true);
        Assert.True(b.Succeeded, string.Join(" | ", b.Trace));
        Assert.Contains(b.Trace, l => l.Contains("PopAWheelieInitial"));
        Assert.Contains(b.Trace, l => l.Contains("PoppedWheelie"));
        var dock = rig.Sent.OfType<DockWithObject>().Single();
        Assert.Equal((byte)DockAction.PopAWheelie, dock.ToBytes()[17]);
        rig.Pump();
        Assert.Contains(rig.Sent, m => m is EnableStopOnCliff { Enable: true });
        Assert.Equal(0, b.Retries);
    }

    [Fact]
    public void PopAWheelieRetriesWithTheRetryAnimationWhenTheDockFails()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(220, 0);
        Assert.Single(rig.Frame().Objects);
        rig.DockOutcome = BlockStatus.NoBlock; rig.DockSucceeds = false;
        var ctx = Ctx(rig);
        var b = new PopAWheelieBehavior(rig.M, "PopAWheelie");
        RunToEnd(rig, b, ctx, frames: () => true, ms: 20000);
        Assert.False(b.Succeeded);
        Assert.Equal(PopAWheelieBehavior.MaxRetries, b.Retries);
        Assert.Contains(b.Trace, l => l.Contains("Retry 1 of 3"));
        Assert.Contains(b.Trace, l => l.Contains("PopAWheelieRetry"));
    }

    [Fact]
    public void RamIntoBlockChargesTheCubeAndBacksOff()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(250, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var ctx = Ctx(rig);
        var b = new RamIntoBlockBehavior(rig.M);
        RunToEnd(rig, b, ctx);
        var lines = rig.Sent.OfType<AppendPathSegmentLine>().ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(100f, lines[0].Speed.SpeedMmps);
        Assert.Equal(-100f, lines[1].Speed.SpeedMmps);
        Assert.InRange(lines[0].XEndMm, 245, 255);   // to the block's centre
        Assert.Contains(rig.LiftHeights, h => h == LiftPresets.LowDockMm);
        Assert.Contains(b.Trace, l => l.Contains("SoundOnlyRamIntoBlock"));
        Assert.Equal(PoseState.Dirty, obj.PoseState);
    }

    [Fact]
    public void CantHandleTallStackLooksDownThenUpThenSulks()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        Stack(rig, 300, 0);
        Assert.Equal(3, rig.Frame().Objects.Count);
        var ctx = Ctx(rig);
        var b = new CantHandleTallStackBehavior(rig.M);
        Assert.True(Runnable(b, ctx));
        RunToEnd(rig, b, ctx, stepMs: 250);
        var heads = rig.Sent.OfType<SetHeadAngle>().Select(h => h.AngleRad).ToList();
        Assert.True(heads.Any(h => Math.Abs(h - (-0.436332f)) < 1e-4), "heads: " + string.Join(",", heads) + " trace: " + string.Join(" | ", b.Trace));
        Assert.Contains(heads, h => h > 0.77f);                                      // +45 deg asked; the head's range clamps it to 0.7767
        Assert.Contains(b.Trace, l => l.Contains("CantHandleTallStack"));
    }

    [Fact]
    public void CheckForStackAtIntervalLooksAboveTheBlockAndWaitsOutTheInterval()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        double clock = 0; rig.M.ClockSec = () => clock;
        rig.Cube = CubeAt(240, 60);
        Assert.Single(rig.Frame().Objects);
        var ctx = Ctx(rig);
        var b = new CheckForStackAtIntervalBehavior(rig.M, "SparksCheckForStackAtInterval", 15);
        Assert.True(Runnable(b, ctx));
        RunToEnd(rig, b, ctx, frames: () => true);
        Assert.Contains(b.Trace, l => l.Contains("ghost pose above block 7"));
        Assert.Contains(rig.Sent, m => m is SetHeadAngle sh && sh.AngleRad > 0.05f);   // the head came up to the ghost
        Assert.Contains(rig.Sent, m => m is AppendPathSegmentPointTurn);              // and the body turned back
        Assert.Equal(0, b.LastCheckSec);
        Assert.False(Runnable(b, ctx));
        clock = 15.5; Assert.True(Runnable(b, ctx));
    }

    [Fact]
    public void RespondPossiblyRollRespondsToAnUprightCubeAndRollsOneOnItsSide()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(220, 0);
        Assert.Single(rig.Frame().Objects);
        var ctx = Ctx(rig);
        var b = new RespondPossiblyRollBehavior(rig.M);
        Assert.True(Runnable(b, ctx));
        RunToEnd(rig, b, ctx);
        Assert.Contains(b.Trace, l => l.Contains("BuildPyramidFirstBlockUpright"));
        Assert.DoesNotContain(rig.Sent, m => m is DockWithObject);
        // a cube on its side: the negative response, then the roll helper docks with ROLL_LOW
        using var rig2 = new Rig();
        rig2.Cube = new Pose3d(Mat3.AboutX(Math.PI / 2), new Vec3(220, 0, 22));
        Assert.Single(rig2.Frame().Objects);
        rig2.DockOutcome = BlockStatus.NoBlock;
        var ctx2 = Ctx(rig2);
        var b2 = new RespondPossiblyRollBehavior(rig2.M);
        RunToEnd(rig2, b2, ctx2, frames: () => true);
        Assert.Contains(b2.Trace, l => l.Contains("BuildPyramidFirstBlockOnSide"));
        Assert.Contains(b2.Trace, l => l.Contains("RollBlockHelper"));
        var dock = rig2.Sent.OfType<DockWithObject>().FirstOrDefault();
        Assert.NotNull(dock);
        Assert.Equal((byte)DockAction.RollLow, dock!.ToBytes()[17]);
    }

    [Fact]
    public void BuildPyramidBasePicksUpOneCubeAndPlacesItBesideTheOther()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(240, 90);
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, CubeAt(240, -90)));
        Assert.Equal(2, rig.Frame().Objects.Count);
        var ctx = Ctx(rig);
        var b = new BuildPyramidBaseBehavior(rig.M, "BuildPyramidBase");
        Assert.True(Runnable(b, ctx));
        // when the carried cube is placed, the fake world moves it beside the static one
        rig.OnDockResult = () =>
        {
            if (rig.M.Docking.Carrying.IsCarryingObject && b.StaticBlockId is { } s)
            {
                var stat = rig.M.World.GetLocatedObjectById(s)!.Pose.Translation;
                var placed = CubeAt(stat.X, stat.Y - 56);
                if (b.BaseBlockId == 7) rig.Cube = placed; else rig.MoreCubes[0] = (ObjectType.Block_LIGHTCUBE2, placed);
            }
        };
        RunToEnd(rig, b, ctx, frames: () => true, ms: 20000);
        Assert.Contains(b.Trace, l => l.Contains("PickupBlockHelper"));
        Assert.True(b.Trace.Any(l => l.Contains("PlaceRelObjectHelper")), string.Join(" | ", b.Trace));
        Assert.True(b.Trace.Any(l => l.Contains("BuildPyramidReactToBase")), string.Join(" | ", b.Trace));
        var docks = rig.Sent.OfType<DockWithObject>().Select(d => (DockAction)d.ToBytes()[17]).ToList();
        Assert.Contains(DockAction.PickupLow, docks);
        Assert.Contains(DockAction.PlaceLow, docks);
        Assert.Equal(BuildPyramidBaseBehavior.Phase.Idle, b.CurrentPhase);
    }

    [Fact]
    public void MajorFrustrationDrivesToARandomPoseInItsConfiguredRange()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = ReactToFrustrationBehavior.Major(rig.M);
        Assert.Equal("ReactToFrustrationMajor", b.Id);
        RunToEnd(rig, b, ctx);
        Assert.NotNull(b.DriveGoal);
        var d = b.DriveGoal!.Value.Translation;
        double dist = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        Assert.InRange(dist, 150, 400);
        double ang = Math.Abs(StraightLinePlanner.Wrap(b.DriveGoal.Value.AngleAroundZ));
        Assert.InRange(ang, 80 * Math.PI / 180 - 1e-6, Math.PI + 1e-6);
        Assert.Contains(b.Trace, l => l.Contains("random drive ->"));
        Assert.Contains(b.Trace, l => l.Contains("FrustratedByFailureMajor"));
        Assert.Contains(rig.Sent, m => m is ExecutePath);
    }

    [Fact]
    public void TheShippedNavigationSetHasTwentyFiveBehavioursWithDistinctIds()
    {
        using var rig = new Rig();
        var set = ShippedBehaviors.Navigation(rig.M);
        Assert.Equal(25, set.Count);
        Assert.Equal(25, set.Select(b => b.Id).Distinct().Count());
        Assert.Contains(set, b => b.Id == "SparksKnockOverCubes" && b is KnockOverCubesBehavior { MinimumStackHeight: 2 });
        Assert.Contains(set, b => b.Id == "Hiking_DriveOffCharger" && b is DriveOffChargerBehavior { ExtraDistanceMm: 45 });
        Assert.Contains(set, b => b.Id == "SparksThinkAboutBeacons" && b is ThinkAboutBeaconsBehavior { BeaconRadiusMm: 75 });
    }
}
