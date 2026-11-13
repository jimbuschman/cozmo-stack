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
[Collection("SteppedBehavior missing-report statics")]
// Polygon fixture calls explicitly supply raw carried ID0 for zero-shift test geometry (substituted tool input).
public class NavigationTests
{
    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;

    // Pilot source oracle: SetPose0051508C retains the no-carry forward center;
    // Straight54762A/3C/66/AA builds from that stored XY with f32 trig/product/add.
    // Firmware state feedback remains an explicit Rig input, not a physical claim.
    private static void AssertForwardLine(AppendPathSegmentLine line,Vec3 main,float yaw,float distance)
    {
        float cosine=MathF.Cos(yaw),sine=MathF.Sin(yaw);
        float x=(float)main.X-20f*cosine,y=(float)main.Y-20f*sine;
        float endX=x+cosine*distance,endY=y+sine*distance;
        // Existing host libm/quaternion boundary; no phone-last-bit identity claim.
        Assert.InRange(line.XStartMm,x-.01f,x+.01f);Assert.InRange(line.YStartMm,y-.01f,y+.01f);
        Assert.InRange(line.XEndMm,endX-.01f,endX+.01f);Assert.InRange(line.YEndMm,endY-.01f,endY+.01f);
    }


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

    // The engine-faithful ActionList path completes each move on a later tick than the old host-Task path, so it
    // needs more pump iterations; these guards are load-tolerant harness bounds, not source oracles.



    // Explicit positive firmware input for only the four pickup-dependent fixtures below.
    // C1 is already carried by the actual DockWithObject.Field8 request; DA timing is supplied,
    // not inferred from PickAndPlaceResult or claimed as reproduced firmware behavior.
    private static void SupplyRequestedPickupLoad(Rig rig)
    {
        rig.M.ClockSec=()=>100; // These tests assert no elapsed action time; transport keeps advancing.
        var previous=rig.OnDockResult;
        rig.OnDockResult=()=>
        {
            previous?.Invoke();
            var dock=rig.Sent.OfType<DockWithObject>().Last();
            if(dock.DockAction is (byte)DockAction.PickupLow or (byte)DockAction.PickupHigh)
            {
                Assert.True(dock.Field8);
                if(dock.Field8)rig.Send(new LiftLoad{Field0=true});
            }
        };
    }

    private static BehaviorContext Ctx(Rig rig) => new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };

    /// <summary><c>IsRunnable</c> also needs animation assets, which these tests do not load; this asks the class's own gate.</summary>
    private static bool Runnable(SteppedBehavior b, BehaviorContext ctx) =>
        (bool)typeof(SteppedBehavior).GetMethod("IsRunnableInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(b, new object[] { ctx })!;

    /// <summary>
    /// What the real robot does for a lift move and the rig's fake does not: acknowledge every SetLiftHeight the stack sent (MotorActionAck; repeated each tick, an ack for a finished move is ignored)
    /// and stream a state with the lift and head in position (the rig's own State() reports both still moving). FlipBlockAction's embedded compound awaits its 45 mm MoveLiftToHeightAction before it
    /// drives (M13-028).
    /// </summary>
    private static void Idle(Rig rig)
    {
        var lifts = rig.Sent.OfType<SetLiftHeight>().ToList();
        foreach (var l in lifts) rig.Send(new MotorActionAck { ActionId = l.ActionId });
        // the fake robot's lift reaches whatever height it was last told to go to (45 mm before any command: angle 0)
        double angle = lifts.Count == 0 ? 0.0 : Math.Asin((Math.Clamp(lifts[^1].HeightMm, 32f, 92f) - 45.0) / 66.0);
        rig.Send(new RobotState
        {
            Timestamp = rig.T += 33, PoseOriginId = rig.OriginId, Pose = new RobotPose { X = rig.X, Y = rig.Y, Angle = rig.Angle }, HeadAngle = rig.Head,
            Status = (uint)(RobotStatusFlag.LiftInPos | RobotStatusFlag.HeadInPos), LiftAngle = (float)angle, Accel = new AccelData { Z = 9800 }, Gyro = new GyroData(),
        });
    }

    // Load-tolerant harness bound: the engine-faithful ActionList path needs more pump iterations than the old
    // host-Task path, and this guard is not a source oracle.
    private static void RunToEnd(Rig rig, SteppedBehavior b, BehaviorContext ctx, Func<bool>? frames = null, int ms = 60000, double stepMs = 33)
    {
        double t = 0;
        rig.Robot.Animations.ManualTicking = true;
        rig.Robot.Animations.ClockMs = () => rig.Clock.NowMs;
        b.ActionTaskRunner = SignalTestContext.Schedule;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        int modelSteps = 0;
        while (b.Update(ctx, t))
        {
            rig.Pump();
            if (frames?.Invoke() ?? false) rig.Frame();
            t += stepMs;
            if (++modelSteps > 10000) throw new TimeoutException("behaviour did not finish: " + string.Join(" | ", b.Trace));
            SignalTestContext.AdvanceBehavior(b);
        }
        b.Stop(BehaviorStopReason.Completed);
    }

    // ------------------------------------------------------------------ motion primitives and the planner

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void TheShippedMotionPrimitivesParseAsTheEngineReadsThem()
    {
        var prims = Prims();
        if (prims is null) return;
        // the asset's own values (cozmo_mprim.json), read by asFloat (M13-004): resolution_mm 10.0, num_angles 16, angle_definitions[1] 0.4636476090008061
        Assert.Equal(10.0f, prims.ResolutionMm);
        Assert.Equal(1.0f / 10.0f, prims.InverseResolution);
        Assert.Equal(16, prims.NumAngles);
        Assert.Equal(16, prims.Angles.Count);
        Assert.Equal((float)0.4636476090008061, prims.Angles[1]);
        Assert.Equal(9, prims.Actions.Count);
        Assert.Equal("short straight", prims.Actions[0].Name); Assert.Equal(1.0001f, prims.Actions[0].ExtraCostFactor);
        Assert.True(prims.Actions[8].Reverse); Assert.Equal(1.2f, prims.Actions[8].ExtraCostFactor);
        Assert.Equal(2.0f, prims.Actions[6].ExtraCostFactor);                    // in-place turns cost double
        Assert.Equal(16, prims.ByAngle.Count);
        var longStraight = prims.ByAngle[0].Single(p => p.ActionIndex == 1);
        Assert.Equal((5, 0, 0), ((int)longStraight.EndX, (int)longStraight.EndY, longStraight.EndTheta));
        Assert.Equal(50.0f, longStraight.StraightLengthMm);
        Assert.Equal(101, longStraight.Intermediate.Count);                      // the asset lists 101 poses for the long straight
        var slightLeft = prims.ByAngle[0].Single(p => p.ActionIndex == 2);
        Assert.Equal((5, 1, 1), ((int)slightLeft.EndX, (int)slightLeft.EndY, slightLeft.EndTheta));
        // the engine's heading index is round(theta * (1 / float(2pi/16))) % 16 (0x00853F7E..0x00853FB4)
        Assert.Equal(4, prims.ThetaIndex((float)(Math.PI / 2)));
        Assert.Equal(15, prims.ThetaIndex(-0.4f));
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void AnEmptyWorldPlansAStraightRunOfLongStraights()
    {
        var prims = Prims();
        if (prims is null) return;
        var planner = new LatticePlanner(new LatticeEnvironment(prims));
        var plan = planner.ComputePath(new StateC(0, 0, 0), new[] { new StateC(300, 0, 0) });
        Assert.NotNull(plan);
        Assert.All(plan!.Actions, a => Assert.Equal(1, a.ActionIndex));            // six long straights, ties broken by the 1.0001 factor
        Assert.Equal(6, plan.Actions.Count);
        // M13-004: each long straight costs (float)(1/60.0 * 50.0) * 1.0f = 0.8333333f (0x00854050..0x00854378 with the file's 50 mm); the planner adds them in float
        float c = (float)(0.016666666666666666 * 50.0);
        float g = 0f;
        for (int i = 0; i < 6; i++) g = 0f + (g + c);                                // soft (0) + (g + prim.cost), 0x0085151A..0x0085151E
        Assert.Equal(g, plan.Cost);
        var path = planner.ToPath(plan, At(300, 0, 0), PathMotionProfile.Default);
        var line = Assert.IsType<PathSegment.Line>(Assert.Single(path));
        Assert.Equal((0f, 0f, 300f, 0f), (line.FromX, line.FromY, line.ToX, line.ToY));
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void TurningPrimitivesBecomeLineAndArcSegmentsThatEndOnTheLattice()
    {
        // A / reach: unit: old supplied world-pose inputs had parent=null; retain coordinates and use the production origin.
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
        Assert.Equal(prims.Angles[1], arc.SweepRad);
        // following the line then the arc lands exactly on the primitive's end cell with its heading
        double ex = arc.CenterX + arc.RadiusMm * Math.Cos(arc.StartAngleRad + arc.SweepRad), ey = arc.CenterY + arc.RadiusMm * Math.Sin(arc.StartAngleRad + arc.SweepRad);
        Assert.Equal(50.0, ex, 3); Assert.Equal(10.0, ey, 3);
        Assert.Equal(line.ToX, arc.CenterX + arc.RadiusMm * Math.Cos(arc.StartAngleRad), 4);          // the arc words are binary32: startRad -pi/2 rounds to -1.5707964, 4e-6 mm off
        // and a plan to a goal with a heading off the lattice ends with a point turn to it
        var full = planner.PlanTo(At(0, 0, 0), new[] { At(200, 100, 0.3) }, PathMotionProfile.Default);
        Assert.NotNull(full);
        Assert.IsType<PathSegment.PointTurn>(full!.Value.Path[^1]);
        Assert.Equal(0.3, ((PathSegment.PointTurn)full.Value.Path[^1]).TargetAngleRad, 6);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ThePlannerRoutesAroundACubeInTheWay()
    {
        var prims = Prims();
        if (prims is null) return;
        var env = new LatticeEnvironment(prims);
        env.AddRectangleObstacle(At(150, 0, 0), CubeGeometry.CubeSizeMm, CubeGeometry.CubeSizeMm, 0, "cube");
        Assert.Equal(1, env.ObstacleCount);
        // the obstacle is expanded by the obstacle padding and then into configuration space with the
        // robot's own quad at that heading, so the point tested is the robot's origin. Its penalty is the
        // shipped 0.1f, so it is soft: IsInSoftCollision sees it and IsInCollision (hard, >= 1000) does not.
        Assert.True(env.IsInSoftCollision(new LatticeState(15, 0, 0)));
        Assert.True(env.IsInSoftCollision(new LatticeState(15, 6, 0)));
        Assert.False(env.IsInSoftCollision(new LatticeState(15, 9, 0)));
        Assert.False(env.IsInCollision(150f, 0f, 0f));
        Assert.Equal(0.1f, env.PenaltyAt(new LatticeState(15, 0, 0)));
        Assert.Equal(0.0f, env.PenaltyAt(new LatticeState(15, 9, 0)));
        var planner = new LatticePlanner(env);
        var res = planner.PlanTo(At(0, 0, 0), new[] { At(300, 0, 0) }, PathMotionProfile.Default);
        Assert.NotNull(res);
        var (plan, path, _) = res!.Value;
        foreach (var s in plan.States()) Assert.False(env.IsInCollision(s), $"state {s} in hard collision");
        Assert.Contains(plan.Actions, a => a.EndTheta != a.StartTheta);            // it had to turn
        Assert.True(path.Count >= 3);
        // a hard obstacle (penalty >= 1000) rejects a goal inside it
        var hard = new LatticeEnvironment(prims);
        hard.AddRectangleObstacle(At(150, 0, 0), 20, 40, 0, "hard", 1000.0f);
        Assert.True(hard.IsInCollision(150f, 0f, 0f));
        Assert.Null(new LatticePlanner(hard).PlanTo(At(0, 0, 0), new[] { At(150, 0, 0) }, PathMotionProfile.Default));
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    [Trait("Fidelity","M13-035 HARDWARE_ONLY scheduling")]
    public void DriveToPoseUsesTheLatticePlannerAndTheFakeRobotArrives()
    {
        using var signals = SignalTestContext.Install();
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig(); rig.DriveCenterFollowing = true; // native drive: drive-center firmware input (RETIRE-LEGACY-DRIVE-API-1)
        Assert.True(rig.M.LoadPlanner(obb));
        using var goalInput=At(250, 120, Math.PI / 2);
        var drive = NativeDriveTestGoals.To(rig.M, goalInput);
        var task = drive.RunAsync(default);
        SignalTestContext.Run(task, () => rig.Pump());
        Assert.Equal(ActionResult.Success, task.Result);
        Assert.Equal(2,rig.M.NativePlanners!.Lattice!.CheckPlanningStatus);
        Assert.InRange(rig.X, 240, 260); Assert.InRange(rig.Y, 110, 130);
        Assert.InRange(Math.Abs(StraightLinePlanner.Wrap(rig.Angle - Math.PI / 2)), 0, 0.05);
        Assert.Contains(rig.Sent, m => m is AppendPathSegmentLine || m is AppendPathSegmentArc);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    [Trait("Fidelity","M13-035 HARDWARE_ONLY scheduling")]
    public void DriveToObjectWithThePlannerIncludesTheAdmittedTargetPolygon()
    {
        using var signals = SignalTestContext.Install();
        var obb = ObbRoot();
        if (obb is null || Lib is null) return;
        using var rig = new Rig(); rig.DriveCenterFollowing = true; // native drive: drive-center firmware input (RETIRE-LEGACY-DRIVE-API-1)
        Assert.True(rig.M.LoadPlanner(obb));
        rig.Cube = CubeAt(260, 0);
        Assert.Single(rig.Frame().Objects);
        // The native DriveToObjectAction (NativeDriveToObjectRunner on the ActionList; the legacy RunAsync, its single reduced goal, its IgnoreObstacleIds and
        // Chosen were retired, RETIRE-LEGACY-DRIVE-API-1): ALL pre-action poses are the goals (D5) and no ignore id is passed (D6, P13).
        var drive = new NativeDriveToObjectRunner(rig.M, 7);
        var task = NativeDriveTestGoals.RunOnActionList(rig.M, drive);
        SignalTestContext.Run(task, () => { rig.Pump(); rig.Frame(); }); // the native turn towards the object (D7) verifies on camera frames
        Assert.True(task.Result == ActionResult.Success, "result=" + task.Result + " trace: " + string.Join(" | ", drive.Action.Trace));
        // CM13-01: 0x00502424 selects all type3 map data; 0x004FDF64 reads its polygon with no target-ID filter.
        // The camera fixture admits this target to the map; soft obstacles still allow the pre-action drive.
        Assert.Equal(1, rig.M.Planner!.Env.ObstacleCount);
        // (The former arrival check rounded the legacy single goal to the 10 mm lattice; the native planner goal is the drive-center goal (P9/P10), and the
        // arrival itself is DriveToPoseAction's Ready-state test (M12-023), which the Success above already required.)
    }

    // ------------------------------------------------------------------ the charger

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void TheChargerIsAPassiveObjectWithOneRectangularMarker()
    {
        var lk = CubeGeometry.LookupMarker(MarkerType.Charger);
        Assert.NotNull(lk);
        Assert.Equal(ObjectType.Charger_Basic, lk!.Value.Type);
        var m = lk.Value.Marker;
        // M13-009: Point2 x = 27.0 (width) at sp+0x14, y = 20.0 (height) at sp+0x18.
        Assert.Equal(27.0, m.SizeMm); Assert.Equal(20.0, m.HeightMm);
        var corners = m.CornersOnObject();
        // R8: narrowed unit quaternion has ww=zz=0.5-2^-25, so point scale is 1-2^-24.
        Assert.Equal((double)BitConverter.UInt32BitsToSingle(0x41D7FFFF), (corners[2] - corners[0]).Length);                   // TL -> TR is the width
        // R29: +22 rounding gives upper z=32, lower z=12+2^-20.
        Assert.Equal(20.0-Math.ScaleB(1,-20), (corners[0] - corners[1]).Length);                   // TL -> BL is the height
        var normal = m.NormalOnObject;
        Assert.Equal(-1.0, normal.X, 6);                                            // facing out of the charger
        // 0x41B00000 in the charger constructor is 22, not 11
        Assert.Equal(86.0, m.PoseOnObject.Translation.X, 6); Assert.Equal(22.0, m.PoseOnObject.Translation.Z, 6);
        Assert.Equal(new Vec3(96, 80, 31), CubeGeometry.SizeOf(ObjectType.Charger_Basic));
        var docked = ChargerGeometry.DockedRobotPose(At(200, 0, 0));
        Assert.Equal(230.0, docked.Translation.X, 6);
        Assert.Equal(Math.PI, Math.Abs(docked.AngleAroundZ), 6);
        Assert.False(CubeGeometry.IsActiveObjectType(ObjectType.Charger_Basic));
    }

    /// <summary>
    /// The one pre-action pose the charger generates (<c>Charger::GeneratePreActionPoses</c> 0x004E9FB0):
    /// a file-static <c>Pose2d(0, 0, 250)</c> turned into
    /// <c>Pose3d(angle + pi/2, Z, (x, -y, -15.5))</c> on the marker's pose. Through the marker's own
    /// -pi/2 at (86, 0, 22) that is the identity rotation at (-164, 0, 6.5) in the charger's frame.
    /// </summary>
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void TheChargerGeneratesOnePreDockPoseOnItsAxisTwoHundredAndFiftyMillimetresOut()
    {
        Assert.Equal(250.0, ChargerGeometry.PreDockDistanceFromMarkerMm);
        Assert.Equal(-15.5, ChargerGeometry.PreDockZOffsetMm);

        var pre = ChargerGeometry.PreDockPose(At(200, 0, 0));
        // R8/R24: -250*(1-2^-24), then +86 and +200 at f32 widths.
        Assert.Equal((double)BitConverter.UInt32BitsToSingle(0x42100004), pre.Translation.X);
        // the marker pose is -pi/2 as the engine's float (0xBFC90FDB), so the 250 mm offset rotates to y = -250 * cosf(-1.5707964f) = 1.09278e-5 (not exactly 0): 0x004E9BBC and 0x004E9FE4..0x004EA018
        // R3/R4 gives equal-magnitude quaternion components; R8 diagonal is exactly zero.
        Assert.Equal(0.0, pre.Translation.Y);
        Assert.Equal((double)BitConverter.UInt32BitsToSingle(0x40D00002), pre.Translation.Z);
        Assert.Equal(0.0, pre.AngleAroundZ, 6);            // facing along the charger's +X, into it

        // and the docked pose is half a turn from it, 194 mm further on
        var docked = ChargerGeometry.DockedRobotPose(At(200, 0, 0));
        Assert.Equal(194.0-Math.ScaleB(1,-16), docked.Translation.X-pre.Translation.X);
    }

    /// <summary>
    /// The mount's numbers, all of them read: the align, the turn, the lift and the reverse
    /// (<c>ConfigureAlignWithChargerAction</c> 0x0054E1C4 and <c>ConfigureTurnAndMountAction</c>
    /// 0x0054E458), and the retry drive (0x0054E72C).
    /// </summary>
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void TheMountCarriesTheEnginesOwnNumbers()
    {
        static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);
        // every constant is the engine's binary32 word (M13-008, M13-012), asserted by its bits
        Assert.Equal(0x42F00000u, Bits(MountChargerAction.AlignDistanceMm));        // 120.0f, 0x0054E21C
        Assert.Equal(0x41F00000u, Bits(MountChargerAction.AlignSpeedMmps));         // 30.0f, 0x0054E22A
        Assert.Equal(0x3D0EFA35u, Bits(MountChargerAction.HeadToleranceRad));       // 0x0054E26A..0x0054E270 (not the rounded 0.0349066 = 0x3D0EFA39)
        Assert.Equal(0x42340000u, Bits(MountChargerAction.LiftHeightForMountMm));   // 45.0f, 0x0054E592
        Assert.Equal(0x40A00000u, Bits(MountChargerAction.LiftToleranceMm));        // 5.0f: the MoveLiftToHeightAction's tolerance, not a speed (0x0054E5BE)
        Assert.Equal(0xC2F00000u, Bits(MountChargerAction.MountDriveMm));           // -120.0f, 0x0054E5FC
        Assert.Equal(0x41F00000u, Bits(MountChargerAction.MountSpeedMmps));         // 30.0f, 0x0054E600
        Assert.Equal(0x3FDF66F3u, Bits(MountChargerAction.TurnMaxSpeedRadPerSec));  // 0x0054E550/0x0054E55A
        Assert.Equal(0x40A78D36u, Bits(MountChargerAction.TurnAccelRadPerSec2));    // 0x0054E55E/0x0054E568
        Assert.Equal(0xBE860A92u, Bits(MountChargerAction.MaxBackupPitchRad));      // 0x0054E7CC
        Assert.Equal(0x42F00000u, Bits(MountChargerAction.RetryDriveMm));           // 120.0f, 0x0054E742
        Assert.Equal(0x42C80000u, Bits(MountChargerAction.RetrySpeedMmps));         // 100.0f, 0x0054E748
        Assert.Equal(0x3FC90FDBu, Bits(MountChargerAction.RetryHeadingWindowRad));  // pi/2, 0x0054E374
        Assert.Equal(0x41F00000u, Bits(MountChargerAction.DockPointXMm));           // 30.0f, 0x0054E4C6
        // M13-008: the result codes
        Assert.Equal(0x04000006u, (uint)ActionResult.RetryDriveDone);
        Assert.Equal(0x0400000Au, (uint)ActionResult.BackupPitchedTooFar);
        Assert.Equal(0x04000009u, (uint)ActionResult.StillOnCharger);
    }

    /// <summary>
    /// An align that fails ends the mount: the align is the first sub-action of a sequence, so the
    /// turn-and-mount is never configured (0x0054E2FA is reached only when the align returned success).
    /// The action does not loop - it makes one attempt and hands back a result.
    /// </summary>
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void AFailedAlignEndsTheMountWithoutTurningOrReversing()
    {
        using var signals = SignalTestContext.Install();
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Head = -0.2f;
        rig.Charger = At(200, 0, 0);
        Assert.Single(rig.Frame().Objects);
        rig.DockSucceeds = false;                       // the align reports a failed dock
        var mount = new MountChargerAction(rig.M, ChargerGeometry.ObjectId) { Clock = () => 0f };
        var task = mount.RunAsync(default);
        SignalTestContext.Run(task, () => { rig.Pump(); rig.Frame(); });

        Assert.NotEqual(ActionResult.Success, task.Result);
        Assert.Equal(1, mount.Attempts);
        Assert.DoesNotContain(rig.Sent, m => m is AppendPathSegmentPointTurn);
        Assert.False(rig.OnCharger);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
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

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void MountChargerAlignsTurnsAndBacksOntoTheContacts()
    {
        using var signals = SignalTestContext.Install();
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Head = -0.2f;
        rig.Charger = At(200, 0, 0);
        Assert.Single(rig.Frame().Objects);
        rig.DockOutcome = BlockStatus.NoBlock;                                       // an align reports no block
        // This positive-path fixture drives firmware commands and contacts. The independently
        // checked 5 s/30 s timeout cases use explicit clocks in RFixBatch2Tests; wall scheduling
        // of the production 1 ms checker must not turn this contact test into a timeout test.
        var mount = new MountChargerAction(rig.M, ChargerGeometry.ObjectId) { Clock = () => 0f };
        // The align leaves the robot 93 mm from the marker - the engine's own 120 less the 27 mm finger
        // offset (the Custom branch at 0x00553402) - and the head is then commanded to 0, from where the
        // marker sits at the very bottom edge of the nominal camera's frame. The charger is dirty from
        // the dock, so on this camera model two frames of not seeing it forget it and the turn-and-mount
        // has no charger to turn to, which is what the engine does with a missing object as well
        // (BadObject at 0x0054E670). Whether the real camera still holds the marker there is a
        // field-of-view question the offline calibration cannot settle - it is a stand-in, not the
        // robot's - so the forgetting is taken out of this test and left as a hardware question. What is
        // under test here is the turn, the backup and the contacts.
        rig.M.World.UnobservedMissesToUnknown = int.MaxValue;
        var task = mount.RunAsync(default);
        SignalTestContext.Run(task, () => { rig.Pump(); if (!rig.OnCharger) rig.Frame(); });
        if (task.Result != ActionResult.Success)
            throw new Xunit.Sdk.XunitException($"Mount returned {task.Result}; fixture pose ({rig.X}, {rig.Y}, {rig.Angle}), contacts {rig.OnCharger}, sensor contacts {rig.Robot.Sensors.OnCharger}; "
                + $"robot pose {rig.M.RobotPose()}, charger pose {rig.M.World.GetLocatedObjectById(ChargerGeometry.ObjectId)?.Pose}; "
                + string.Join(" | ", mount.Trace)
                + " | paths: " + string.Join(" | ", rig.Sent.OfType<AppendPathSegmentLine>().Select(p => $"({p.XStartMm},{p.YStartMm})->({p.XEndMm},{p.YEndMm}) at {p.Speed.SpeedMmps}")));
        Assert.Equal(ActionResult.Success, task.Result);
        Assert.True(rig.OnCharger);
        Assert.True(rig.Robot.Sensors.OnCharger);
        var dock = rig.Sent.OfType<DockWithObject>().First();
        Assert.Equal((byte)DockAction.Align, dock.ToBytes()[17]);
        // BackupOntoChargerAction::CheckIfDone 0x0054E7A8 succeeds the moment the contacts report and
        // does not drive the rest of the 120 mm. The pi/2 heading test is on the failure path only.
        Assert.Contains(mount.Trace, l => l.Contains("the contacts"));
        Assert.DoesNotContain(mount.Trace, l => l.Contains("retry"));
        // the charger turn is TurnInPlaceAction -> SetBodyAngle (absolute, speed 0x3FDF66F3, accel 0x40A78D36, tolerance 0x3D0EFA35), not a path point turn (M13-008)
        Assert.DoesNotContain(rig.Sent, m => m is AppendPathSegmentPointTurn);
        var turn = Assert.Single(rig.Sent.OfType<SetBodyAngle>());
        Assert.True(turn.IsAbsolute);
        Assert.Equal(0x3FDF66F3u, BitConverter.SingleToUInt32Bits(turn.MaxSpeedRadPerSec));
        Assert.Equal(0x40A78D36u, BitConverter.SingleToUInt32Bits(turn.AccelRadPerSec2));
        Assert.Equal(0x3D0EFA35u, BitConverter.SingleToUInt32Bits(turn.ToleranceRad));
        var back = rig.Sent.OfType<AppendPathSegmentLine>().Last();
        Assert.Equal(-30f, back.Speed.SpeedMmps);                                  // backwards at 30 mm/s (0xC2F00000 mm at 0x41F00000 mm/s)
        Assert.Equal(1, mount.Attempts);
    }

    /// <summary>
    /// M13-008 / M4-009: MountChargerAction::Init 0x0054E0CC stores the action's charger ObjectID (action+0x7C) into robot+0x334
    /// (0x0054E11C ldr Robot*, 0x0054E11E ldr [action+0x7C], 0x0054E120 str.w [robot+0x334]) once the located-charger lookup and
    /// type check pass (0x0054E106..0x0054E11A), before ConfigureAlignWithChargerAction (0x0054E126). From then on the Moved handler
    /// (0x00533EA6 ldr.w [robot+0x334]; 0x00533EAA ldr [obj+0x18]; 0x00533EAC cmp -> Info "Robot.ActiveObjectMoved.Charger"
    /// "Charger sending garbage move messages", then the epilogue: no dirty, no IsMoving, no broadcast) and the Stopped handler
    /// (0x00534794..0x0053479A, the same drop) drop a connected object whose ObjectID equals it. UpAxisChanged
    /// (0x00534D50..0x00534F3C) has no 0x334 read: it logs and broadcasts (0x00534E14).
    /// </summary>
    // reach: unit. The mount enters through MountChargerAction.RunAsync (Init) on the ManipRig fixture, but the action itself is
    // constructed here: the robot runtime never constructs it (in the engine only app-sent game actions build it, RobotActionUnion
    // factory 0x0052A2D6 / DriveToAndMountChargerAction 0x0054E87A). The Moved/Stopped/UpAxisChanged reports do go through the robot's
    // real message dispatch. Substituted: the charger is connected by an ObjectConnectionState in slot 0 (factory 0xABD0, a fixture
    // value), and its ObjectID equals the located charger's only because the rig seeds the per-type unique ID (Charger_Basic = 100) and
    // the stack's AddConnectedActiveObject/SetID port gives both the same value. Whether the engine's connected and located chargers
    // share an ObjectID: connected-after-located (this case) copies the located ID (M11-041, 0x006237DA/0x006237DE); located-after-connected is M11-013's.
    [Fact]
    public void M13_008_MountInitStoresTheChargerIdAndTheMovementFilterDropsItsReports()
    {
        using var signals = SignalTestContext.Install();
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Head = -0.2f;
        rig.Charger = At(200, 0, 0);
        Assert.Single(rig.Frame().Objects);
        rig.DockOutcome = BlockStatus.NoBlock;
        Assert.Equal(-1, rig.Robot.Cubes.ChargerObjectId);                          // Robot ctor 0x00510096/0x005100AC: -1 before any mount
        var mount = new MountChargerAction(rig.M, ChargerGeometry.ObjectId) { Clock = () => 0f };
        rig.M.World.UnobservedMissesToUnknown = int.MaxValue;                        // as in MountChargerAlignsTurnsAndBacksOntoTheContacts
        var task = mount.RunAsync(default);
        // Init runs before the first await: the store precedes ConfigureAlignWithChargerAction (0x0054E120 before 0x0054E126).
        Assert.Equal(unchecked((int)ChargerGeometry.ObjectId), rig.Robot.Cubes.ChargerObjectId);
        SignalTestContext.Run(task, () => { rig.Pump(); if (!rig.OnCharger) rig.Frame(); });
        Assert.Equal(ActionResult.Success, task.Result);
        Assert.Equal(unchecked((int)ChargerGeometry.ObjectId), rig.Robot.Cubes.ChargerObjectId);   // never cleared (census section 2)

        rig.Send(new ObjectConnectionState { ObjectID = 0, FactoryID = 0xABD0, ObjectType = ObjectType.Charger_Basic, Connected = true });
        rig.Tick(); rig.Pump();
        var connected = Assert.Single(rig.Vision.World.ConnectedObjects, o => o.Type == ObjectType.Charger_Basic);
        if (connected.ObjectId != ChargerGeometry.ObjectId)
            throw new Xunit.Sdk.XunitException($"fixture: the connected charger has ObjectID {connected.ObjectId}, the located one {ChargerGeometry.ObjectId}");
        var located = rig.M.World.GetLocatedObjectById(ChargerGeometry.ObjectId)!;
        var poseStateBefore = located.PoseState;
        var cube = rig.Robot.Cubes.ByObjectId(0)!;
        bool movingBefore = cube.Moving, worldMovingBefore = connected.IsMoving;
        var movement = new List<CubeTimestampEvent>(); var moved = new List<Cube>(); var upAxis = new List<CubeUpAxisEvent>();
        rig.Robot.Cubes.MovementBroadcast += movement.Add;
        rig.Robot.Cubes.CubeMoved += moved.Add;
        rig.Robot.Cubes.UpAxisBroadcast += upAxis.Add;
        var engineLog = new List<string>();
        rig.Robot.Engine.LogEntry += e => { lock (engineLog) engineLog.Add($"{e.Name}: {e.Text}"); };

        rig.Send(new ObjectMoved { Timestamp = 5000, ObjectID = 0 });
        rig.Send(new ObjectStoppedMoving { Timestamp = 5001, ObjectID = 0 });
        rig.Tick(); rig.Pump();
        Assert.Empty(movement);                                                       // no ObjectMoved / ObjectStoppedMoving broadcast
        Assert.Empty(moved);
        Assert.Equal(movingBefore, cube.Moving);                                      // no SetIsMoving
        Assert.Equal(worldMovingBefore, connected.IsMoving);
        Assert.Equal(poseStateBefore, located.PoseState);                             // no dirty
        lock (engineLog)
        {
            Assert.Contains("Robot.ActiveObjectMoved.Charger: Charger sending garbage move messages", engineLog);    // 0x00533EE8
            Assert.Contains("Robot.ActiveObjectStopped.Charger: Charger sending garbage move messages", engineLog);  // 0x005347D6
            Assert.DoesNotContain(engineLog, l => l.Contains("ObjectMovedOrStopped"));
        }

        rig.Send(new ObjectUpAxisChanged { Timestamp = 5002, ObjectID = 0, UpAxis = UpAxis.ZPositive });
        rig.Tick(); rig.Pump();
        var ua = Assert.Single(upAxis);                                               // UpAxisChanged has no charger filter (0x00534D50..0x00534F3C)
        Assert.Equal(unchecked((int)ChargerGeometry.ObjectId), ua.ObjectID);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void DriveOffChargerDrivesTheChargerLengthPlusTheExtraAndFiresTheEvent()
    {
        using var signals = SignalTestContext.Install();
        using var rig = new Rig();
        rig.Charger = At(-30, 0, 0);                                                 // the robot sits docked, facing out of the lip
        rig.Angle = (float)Math.PI;
        rig.OnCharger = true; rig.State();
        var ctx = Ctx(rig);
        var b = new DriveOffChargerBehavior(rig.M, "DriveOffCharger", 60);
        Assert.True(Runnable(b, ctx));                                               // robot+0x34A: SetOnCharger's rising edge set the platform flag
        Assert.Equal(156f, b.DriveDistanceMm);                                       // 96.0f + 60.0f in binary32 (0x005C09E2)
        double t = 0;
        rig.Robot.Animations.ManualTicking = true;
        rig.Robot.Animations.ClockMs = () => rig.Clock.NowMs;
        b.ActionTaskRunner = SignalTestContext.Schedule;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        int modelSteps = 0;
        while (b.Update(ctx, t))
        {
            rig.Pump();
            // Robot::Update clears +0x34A when no charger is located or the footprint leaves the charger (0x00513CD8..0x00513E2A, M4-019): the engine tick runs that step
            rig.Tick();
            t += 33;
            SignalTestContext.AdvanceBehavior(b);
            if (++modelSteps > 10000) throw new TimeoutException("behaviour did not finish: " + string.Join(" | ", b.Trace));
        }
        var line = rig.Sent.OfType<AppendPathSegmentLine>().Single();
        Assert.Equal(156f, Math.Abs(line.XEndMm - line.XStartMm), 0);
        Assert.Equal(100f, line.Speed.SpeedMmps);                                    // this stack's DriveStraightAction default (the engine's two-argument constructor's is MISSING)
        Assert.False(rig.OnCharger);
        // the mood event belongs to the drive's own completion callback (0x005C0E9C), on result 0 only
        Assert.Contains(b.Trace, l => l.Contains("emotion event DriveOffCharger"));
        Assert.Contains(b.Trace, l => l.Contains("BehaviorObjectiveAchieved(4, true)"));
        Assert.True(b.LeftChargerOnTreads);
        Assert.NotNull(b.DroveOffAtSec);                                             // UpdateInternal's whiteboard stamp (0x005C0DFA..0x005C0E08)
        Assert.False(Runnable(b, ctx));
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ReactToOnChargerDoesNotFinishFromContactOrBehaviorClock()
    {
        using var signals = SignalTestContext.Install();
        using var rig = new Rig();
        rig.OnCharger = true; rig.State();
        var ctx = Ctx(rig);
        var b = new ReactToOnChargerBehavior("ReactToOnCharger", 300, 330);
        Assert.True(Runnable(b, ctx));
        rig.Robot.Animations.ManualTicking = true;
        rig.Robot.Animations.ClockMs = () => rig.Clock.NowMs;
        b.ActionTaskRunner = SignalTestContext.Schedule;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        double t = 0;
        Assert.True(b.Update(ctx, t));
        for (t = 33; t < 299_000; t += 1000) Assert.True(b.Update(ctx, t));
        Assert.Empty(b.Broadcasts);
        Assert.True(b.Update(ctx, 301_000));
        Assert.Empty(b.Broadcasts);
        Assert.True(b.Update(ctx, 331_000));
        Assert.Empty(b.Broadcasts);
        rig.OnCharger = false; rig.State();
        Assert.True(b.Update(ctx, 332_000));
        var vc = new ReactToOnChargerBehavior("VC_GoToSleep", 300, 330, triggeredFromVoiceCommand: true);
        Assert.True(Runnable(vc, ctx));
        vc.Requested = true;
        Assert.True(Runnable(vc, ctx));
    }

    // ------------------------------------------------------------------ the flip action

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void FlipDrivesThroughTheCubeRaisingTheLiftAndForgetsItsPose()
    {
        using var signals = SignalTestContext.Install();
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(200, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var sourceMain=rig.Robot.Engine.Robot!.MainPose.Translation;
        float sourceYaw=(float)rig.Robot.Engine.Robot.MainPose.AngleAroundZ;
        using var initialMain=rig.M.CopyPreActionRobotPose();
        using var relative=obj.NativePose.WithRespectTo(initialMain!);
        var v=relative.Translation;float vx=(float)v.X,vy=(float)v.Y,vz=(float)v.Z;
        float norm=vx*vx;norm=norm+vy*vy;norm=norm+vz*vz;
        float sourceDistance=MathF.Sqrt(norm)+20f; //0055EED6..EF1C
        var flip = new FlipBlockAction(rig.M, 7) { CheckPreActionPose = false };
        Idle(rig);
        var task = flip.RunAsync(default);
        SignalTestContext.Run(task, () => { Idle(rig); rig.Pump(); });   // M13-028: the drive waits for the 45 mm lift move; Idle() acks every lift and streams the lift in position
        Assert.Equal(ActionResult.Success, task.Result);
        var line = rig.Sent.OfType<AppendPathSegmentLine>().Single();
        Assert.InRange(line.XEndMm-line.XStartMm, 217, 224); // unchanged source action distance +20
        AssertForwardLine(line,sourceMain,sourceYaw,sourceDistance);
        Assert.Equal(150f, line.Speed.SpeedMmps);
        // the lift already reports 45 mm (angle 0), so the initial MoveLiftToHeightAction sends nothing (M4-016 MA15); whether a carry-height lift was queued depends on a poll tick catching the fake robot
        // between its instant jump and the path's completion, so only the deterministic CheckIfDoneTick tests (M13_028_*) assert it. The terminal helper removes the located object without rewriting its state (0x0055F186).
        Assert.DoesNotContain(45f, rig.LiftHeights);
        Assert.Equal(PoseState.Known, obj.PoseState);
        Assert.Null(rig.M.World.GetLocatedObjectById(obj.ObjectId));
        // with the check on, a robot away from every flipping pose is refused. The cube is at 300 mm
        // rather than 400: past about 350 mm its marker covers fewer than the hundred pixels
        // component_minimumNumPixels asks for, so the engine's own front end would not see it either.
        rig.Cube = CubeAt(300, 80); rig.X = 0; rig.Y = 0; rig.Angle = 0; rig.State(); rig.Frame();
        var strict = new FlipBlockAction(rig.M, 7);
        Assert.Equal(ActionResult.DidNotReachPreActionPose, SignalTestContext.Result(strict.RunAsync(default)));
    }

    // ------------------------------------------------------------------ block configurations

    private static ObservableObject Cube(uint id, ObjectType type, Pose3d pose) => new(id, type, CubeGeometry.CubeMarkers(type)) { Pose = pose, PoseState = PoseState.Known };

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void StacksBasesAndPyramidsAreRecognisedFromCubePoses()
    {
        var world = new BlockWorld(() => Array.Empty<(uint, ObjectType)>());
        var cubes = new[]
        {
            Cube(1, ObjectType.Block_LIGHTCUBE1, CubeAt(200, 0)), Cube(2, ObjectType.Block_LIGHTCUBE2, CubeAt(203, 2, 0.05, 66)), Cube(3, ObjectType.Block_LIGHTCUBE3, CubeAt(198, -1, 0, 110)),
        };
        var stack = BlockConfigurationManager.FindObjectOnTopOrUnderneath(cubes[0], cubes, onTop: true, BlockConfigurationManager.OnTopPlanarToleranceMm);
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

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void TheConfigurationManagerTracksWhatTheWorldSeesAndWhenItFirstSawIt()
    {
        using var signals = SignalTestContext.Install();
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
        // OnConfigSeen: runnable within 5 s of the base being seen, not after
        var ctx = Ctx(rig);
        var seen = OnConfigSeenBehavior.RespondToPyramidBase(rig.M);
        clock = 104.5; Assert.True(Runnable(seen, ctx));
        clock = 105.5; Assert.True(Runnable(seen, ctx)); // host clock cannot change native eligibility.
        rig.Clock.Advance(100);rig.Robot.Engine.Tick();
        Assert.False(Runnable(seen,ctx)); // native clock changed; no count growth on this evaluation.
        clock = 104.5;
        seen=OnConfigSeenBehavior.RespondToPyramidBase(rig.M); // fresh source ctor counts0.
        Assert.True(Runnable(seen,ctx));
        RunToEnd(rig, seen, ctx);
        Assert.Contains(seen.Trace, l => l.Contains("BuildPyramidReactToBase"));
        rig.Clock.Advance(100);rig.Robot.Engine.Tick();
        Assert.False(Runnable(seen, ctx)); // no new native count growth on later engine clock; host _reacted is diagnostic only.
        // the third cube on top of the midpoint completes a pyramid
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE3, CubeAt(260, 14, 0, 66)));
        clock = 106;
        rig.Frame();
        Assert.Single(rig.M.Configurations.Pyramids);
        Assert.Empty(rig.M.Configurations.PyramidBases); // OV10 K3 prunes bases belonging to complete pyramids.
        Assert.Equal(106, rig.M.Configurations.FirstSeenSec(rig.M.Configurations.Pyramids[0]));
        // the reactions to a pyramid arm a 100 s cooldown and do nothing else
        var react = ReactToConfigurationBehavior.ReactToPyramid(rig.M);
        Assert.True(Runnable(react, ctx));
        RunToEnd(rig, react, ctx);
        Assert.Equal(206, react.NextAllowedSec);
        Assert.False(Runnable(react, ctx));
        clock = 207; Assert.True(Runnable(react, ctx));
    }

    // ------------------------------------------------------------------ the whiteboard and the workouts

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
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

    /// <summary>
    /// The two emotion events a beacon placement fires, and the two constants the tall-stack and put-down
    /// behaviours were missing. FireEmotionEvents 0x005E002C picks between them on
    /// AIWhiteboard::AreAllCubesInBeacons; CreateLookAfterPlaceAction 0x005C8174 waits for two images
    /// (movs r2, #2 at 0x005C8242); TransitionToDisapointment 0x005ED0F0 plays trigger 0x1B.
    /// </summary>
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void TheBeaconAndStackConstantsAreTheEnginesOwn()
    {
        Assert.Equal("HikingBroughtCubeToBeacon", BringCubeToBeaconBehavior.CubeEmotionEvent);
        Assert.Equal("HikingBroughtLastCubeToBeacon", BringCubeToBeaconBehavior.LastCubeEmotionEvent);
        Assert.Equal(2, PutDownBlockBehavior.ImagesToWaitFor);
        Assert.Equal(0x1B, (int)AnimationTrigger.CantHandleTallStack);
    }

    /// <summary>
    /// The robot's side of a put-down: IS_PICKING_OR_PLACING (status bit 0x4) is reported while it lowers the lift once it has the PlaceObjectOnGround message, then it clears (firmware output order is
    /// HARDWARE_ONLY; the engine's PlaceObjectOnGroundAction::CheckIfDone waits for exactly this edge).
    /// </summary>
    private static void PlacingFirmware(Rig rig, ref int raisedAt, ref bool cleared, Func<bool> latched)
    {
        if (!rig.Sent.Any(m => m is PlaceObjectOnGround)) return;
        if (raisedAt == 0) { rig.State(flags: (uint)RobotStatusFlag.IsPickingOrPlacing); raisedAt = 1; rig.Cube = new Pose3d(Mat3.AboutZ(rig.Angle), new Vec3(rig.X + 100 * Math.Cos(rig.Angle), rig.Y + 100 * Math.Sin(rig.Angle), 22)); }
        else if (!cleared && latched()) { rig.State(flags: (uint)(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos)); cleared = true; }
        else if (!cleared) rig.State(flags: (uint)RobotStatusFlag.IsPickingOrPlacing);                  // a camera frame (rig.Frame) reports a fresh state: keep reporting the bit while the lift lowers
    }

    /// <summary>
    /// M15-011/M15-084, SelectNewBeacon 0x005E5F0C..0x005E5F36: Robot::GetPose (called at 0x005E5F1A), then GetWithRespectToRoot (0x004DF628, called at 0x005E5F24), handed to AddBeacon
    /// with the radius at [this+0x128]: the beacon sits at the whole robot pose w.r.t. its root, z included. Admission preserves the current pose's z rather than taking the packet's z.
    /// </summary>
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void M15_011_TheBeaconIsAddedAtTheFullRobotPose()
    {
        using var signals = SignalTestContext.Install();
        if (Lib is null) return;
        using var rig = new Rig();
        // Unit fixture supplies the native current height (its world/kinematic producer is outside this beacon test).
        var native=rig.Robot.Engine.Robot!;
        using(var pose=new Pose3d(native.MainPose))
        {pose.Node!.Transform=new Transform3d(pose.Quaternion,new Vec3(0,0,25));native.SetPose(pose);}
        rig.Send(new RobotState
        {
            Timestamp = rig.T += 33, PoseOriginId = rig.OriginId, Pose = new RobotPose { X = 100, Y = 50, Z = 25, Angle = 0.5f }, HeadAngle = rig.Head,
            Status = (uint)(RobotStatusFlag.LiftInPos | RobotStatusFlag.HeadInPos), Accel = new AccelData { Z = 9800 }, Gyro = new GyroData(),
        });
        var ctx = Ctx(rig);
        var think = new ThinkAboutBeaconsBehavior(rig.M, "Hiking_ThinkAboutBeacons", 175);
        think.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        var beacon = rig.M.Whiteboard.GetActiveBeacon()!;
        Assert.Equal(100, beacon.Pose.Translation.X, 3);
        Assert.Equal(50, beacon.Pose.Translation.Y, 3);
        Assert.Equal(25, beacon.Pose.Translation.Z, 3);
        Assert.Equal(0.5f, (float)beacon.Pose.AngleAroundZ, 5);
        Assert.Equal(175, beacon.RadiusMm);
        think.Stop(BehaviorStopReason.Cancelled);
    }

    /// <summary>
    /// M15-008, the TryToStackOn completion lambda 0x005E142C (category = top byte of the result, 0x005E1434..0x005E144A): 0 -> StackCube + FireEmotionEvents; 3 -> log and
    /// SetFailedToUse(target, StackOnObject) (0x005E156E -> 0x005E1660); 4 with the cube not carried (the retry test fails) -> the same failure (0x005E15D8); 1 (running) and 2 (cancelled) branch to
    /// 0x005E1684 and return: no log, no SetFailedToUse.
    /// </summary>
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Theory]
    [InlineData(0x00000000u, "success")]
    [InlineData(0x01000000u, "none")]
    [InlineData(0x02000000u, "none")]
    [InlineData(0x03000000u, "failed")]
    [InlineData(0x04000000u, "failed")]
    [InlineData(0x04000001u, "failed")]
    public void M15_008_TheStackCallbackActsOnlyOnTheEnginesCategories(uint result, string expected)
    {
        using var signals = SignalTestContext.Install();
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(300, -30);
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.Identity, new Vec3(230, 22, 22))));
        rig.Frame();
        Assert.Equal(2, rig.Vision.World.LocatedObjects.Count);
        var ctx = Ctx(rig);
        ctx.Needs = new NeedsManager(() => 0);
        var bring = new BringCubeToBeaconBehavior(rig.M, "Hiking_BringCubeToBeacon", 45);
        // A / reach: unit: the old manually supplied beacon root was unrelated to the production object origin.
        using var beaconInput=new Pose3d(Mat3.Identity,Vec3.Zero);
        beaconInput.SetParent(rig.Vision.World.GetNativeOrigin(rig.Vision.World.CurrentOriginId));
        rig.M.Whiteboard.AddBeacon(beaconInput,175);
        rig.Robot.Animations.ManualTicking = true;
        rig.Robot.Animations.ClockMs = () => rig.Clock.NowMs;
        bring.ActionTaskRunner = SignalTestContext.Schedule;
        bring.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        uint target = rig.Vision.World.LocatedObjects.Select(o => o.ObjectId).First(i => i != bring.Candidate);
        bring.StackCompleted((ActionResult)result, target, 1);
        bool marked = rig.M.Whiteboard.DidFailToUse(target, ObjectActionFailure.StackOnObject, 1e9);
        Assert.Equal(expected == "failed", marked);
        Assert.Equal(expected == "success", bring.Trace.Any(l => l == "needs action StackCube"));
        Assert.Equal(expected == "failed", bring.Trace.Any(l => l.Contains("stacking failed")));
        bring.Stop(BehaviorStopReason.Cancelled);
    }

    /// <summary>
    /// M15-008, AIBeacon::IsLocWithinBeacon(pose, margin) 0x0059C24C: the pose in the beacon's frame, its translation squared in THREE dimensions in binary32 (x*x, then + y*y, then + z*z),
    /// against (radius - margin)^2 + 1.0e-5f; within when the right side is at least the left. Radius 175, margin 54: limit 121^2 = 14641 (the 1e-5 is below a binary32 ulp of 14641).
    /// </summary>
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void M15_008_IsLocWithinBeaconIsThreeDimensionalAndMarginReduced()
    {
        using var root = new Pose3d();
        var beacon = new AIBeacon(new Pose3d(Rotation3d.Identity, new Vec3(0, 0, 0), parent: root), 175);
        Assert.True(PoseTreeNode.HasSameRoot(beacon.Pose.Node, root.Node));
        Pose3d At(double x, double y, double z)
        {
            var pose = new Pose3d(Rotation3d.Identity, new Vec3(x, y, z), parent: root);
            Assert.True(PoseTreeNode.HasSameRoot(pose.Node, root.Node));
            return pose;
        }
        Assert.True(beacon.IsLocWithinBeacon(At(121, 0, 0), 54f));                 // d2 = 14641 >= 14641 (limit)
        Assert.False(beacon.IsLocWithinBeacon(At(121.001, 0, 0), 54f));            // 14641.242 > 14641
        Assert.False(beacon.IsLocWithinBeacon(At(70, 0, 100), 54f));               // 4900 + 10000 = 14900 > 14641: z counts (a planar test says 70 <= 121)
        Assert.True(beacon.IsLocWithinBeacon(At(70, 0, 90), 54f));                 // 4900 + 8100 = 13000
        Assert.True(beacon.IsLocWithinBeacon(At(174, 0, 0), 0f));                  // margin 0: radius itself
        Assert.False(beacon.IsLocWithinBeacon(At(176, 0, 0), 0f));
        // the pose is taken with respect to the beacon's pose: a rotated, offset beacon sees the same distance
        var rotated = new AIBeacon(new Pose3d(Rotation3d.FromMatrix(Mat3.AboutZ(1.0)), new Vec3(500, -200, 10), parent: root), 175);
        Assert.True(PoseTreeNode.HasSameRoot(rotated.Pose.Node, root.Node));
        Assert.True(rotated.IsLocWithinBeacon(At(500 + 100, -200, 10), 54f));
        Assert.False(rotated.IsLocWithinBeacon(At(500 + 130, -200, 10), 54f));
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
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
        // The engine walks the list rather than picking: GetCurrentWorkout 0x00573DE8 returns the
        // pointer at +0xC and CompleteCurrentWorkout 0x00573DEC steps it on by one entry unless it is
        // already the last (0x00573E24), so the first is current and the last repeats.
        Assert.Equal(high, w.GetCurrentWorkout());
        Assert.Equal(0, w.CurrentIndex);
        // Supplied unit recipients: a non-empty configured event requires both mood and clock.
        w.TriggerEmotionEvent=(_,_)=>true;w.ClockSec=()=>0;
        w.CompleteCurrentWorkout();
        Assert.Equal(w.Workouts[1], w.GetCurrentWorkout());
        for (int i = 0; i < 10; i++) w.CompleteCurrentWorkout();
        Assert.Equal(w.Workouts[^1], w.GetCurrentWorkout());                         // and it sticks there
    }

    // ------------------------------------------------------------------ the cube behaviours

    private static void Stack(Rig rig, double x, double y)
    {
        rig.Head = 0.12f;
        rig.Cube = CubeAt(x, y);
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, CubeAt(x, y, 0, 66)));
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE3, CubeAt(x, y, 0, 110)));
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void RamIntoBlockChargesTheCubeAndBacksOff()
    {
        using var signals = SignalTestContext.Install();
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(250, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var sourceMain=rig.Robot.Engine.Robot!.MainPose.Translation;
        var relativeBlock=obj.Pose.Translation-sourceMain;
        float sourceYaw=(float)Math.Atan2(relativeBlock.Y,relativeBlock.X);
        float sourceDistance=(float)Math.Sqrt(relativeBlock.X*relativeBlock.X+relativeBlock.Y*relativeBlock.Y);
        var ctx = Ctx(rig);
        var b = new RamIntoBlockBehavior(rig.M);
        RunToEnd(rig, b, ctx);
        var lines = rig.Sent.OfType<AppendPathSegmentLine>().ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(100f, lines[0].Speed.SpeedMmps);
        Assert.Equal(-100f, lines[1].Speed.SpeedMmps);
        Assert.InRange(lines[0].XEndMm-lines[0].XStartMm, 245, 255); // unchanged source distance to block
        AssertForwardLine(lines[0],sourceMain,sourceYaw,sourceDistance);
        Assert.Contains(rig.LiftHeights, h => h == LiftPresets.LowDockMm);
        Assert.Contains(b.Trace, l => l.Contains("SoundOnlyRamIntoBlock"));
        Assert.Equal(PoseState.Dirty, obj.PoseState);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void CantHandleTallStackLooksDownThenUpThenSulks()
    {
        using var signals = SignalTestContext.Install();
        if (Lib is null) return;
        using var rig = new Rig();
        // M4-001 MA22 / M4-016 MA15: until the head is calibrated the engine reads it at -25 deg (Robot+0x2FC from the
        // constructor; RS6 ignores reported angles), so a move to -25 deg is already in position and sends nothing. A
        // robot reports its connection-time calibration; this rig does not, so the test reports it here.
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false });
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

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void CheckForStackAtIntervalLooksAboveTheBlockAndWaitsOutTheInterval()
    {
        using var signals = SignalTestContext.Install();
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

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void MajorFrustrationDrivesToARandomPoseInItsConfiguredRange()
    {
        using var signals = SignalTestContext.Install();
        using var rig = new Rig();
        var obb = ObbRoot();
        Assert.NotNull(obb);
        Assert.True(rig.M.LoadPlanner(obb!));
        rig.HoldPath = true;
        var ctx = Ctx(rig);
        var b = ReactToFrustrationBehavior.Major(rig.M);
        Assert.Equal("ReactToFrustrationMajor", b.Id);
        bool reported = false;
        using var objective = rig.Robot.Engine.AnimationMessages.SubscribeMessage(0x8B, true, payload =>
        {
            if (payload is not uint id || id != 0x1D) return;
            Assert.IsType<DriveToPoseAction>(b.OwnedRunner);
            reported = true;
        });
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        SignalTestContext.Until(() => reported, () => rig.Pump(1));
        var goal = Assert.IsType<Pose3d>(b.DriveGoal);
        Assert.InRange(goal.Translation.X, 150f, 400f);
        Assert.Equal(0f, goal.Translation.Y);
        Pose3d parent = null!;
        Assert.True(goal.TryGetParent(ref parent));
        using (parent)
        {
            Assert.True(parent.HasParent);
            double angle = Math.Abs(StraightLinePlanner.Wrap(parent.AngleAroundZ));
            Assert.InRange(angle, 80 * Math.PI / 180 - 1e-6, Math.PI + 1e-6);
        }
        SignalTestContext.Until(() => rig.Sent.OfType<ExecutePath>().Any(), () => rig.Pump(1));
        Assert.Contains(rig.Sent, m => m is ExecutePath);
        b.Stop(BehaviorStopReason.Cancelled);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void TheShippedNavigationSetHasThirteenBehavioursWithDistinctIds()
    {
        using var rig = new Rig();
        var set = ShippedBehaviors.Navigation(rig.M);
        // RETIRE-LEGACY-DRIVE-API-1 (operator 2026-11-13): the 11 legacy entries (KnockOverCubes x2, PopAWheelie x2, CubeLiftWorkout x2, BuildPyramidBase,
        // BuildPyramid, PyramidRespondPossiblyRoll, Hiking/Sparks BringCubeToBeacon) left the hand-built list; 24 - 11 = 13 remain.
        Assert.Equal(13, set.Count);
        Assert.Equal(13, set.Select(b => b.Id).Distinct().Count());
        Assert.Contains(set, b => b.Id == "Hiking_DriveOffCharger" && b is DriveOffChargerBehavior { ExtraDistanceMm: 45 });
        Assert.Contains(set, b => b.Id == "SparksThinkAboutBeacons" && b is ThinkAboutBeaconsBehavior { BeaconRadiusMm: 75 });
    }

    // ------------------------------------------------------------------ M13 fidelity (inventory-derived expected values)

    /// <summary>
    /// M13-016. The alignment-type table is by numeric type (0x005533E6 <c>tbb</c>, table 0x005533EA =
    /// 02 05 09 0c): 0 -&gt; 6.0, 1 -&gt; flag 2 and distance 0.0, 2 -&gt; -15.0, 3 -&gt; argument + (-27.0);
    /// the clamp threshold is 0xC1800005 = -16.000009536743164; and
    /// <c>GetPreActionTypeFromAlignmentType</c> 0x005532B8 maps 0-&gt;1, 1-&gt;0, 2-&gt;1, 3-&gt;1, invalid-&gt;1.
    /// </summary>
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void AlignWithObjectUsesTheEnginesAlignmentTypeTable()
    {
        static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);
        // every distance is the engine's binary32 value (the table 0x005533E6; the stored word at +0x9C)
        Assert.Equal(0x40C00000u, Bits(new AlignWithObjectAction(null!, 0, 123.0f, AlignmentType.LiftFinger).DockDistanceMm));   // 6.0f
        Assert.Equal(0x00000000u, Bits(new AlignWithObjectAction(null!, 0, 123.0f, AlignmentType.LiftPlate).DockDistanceMm));    // 0.0f
        Assert.Equal(0xC1700000u, Bits(new AlignWithObjectAction(null!, 0, 123.0f, AlignmentType.Body).DockDistanceMm));          // -15.0f
        Assert.Equal(0x42C00000u, Bits(new AlignWithObjectAction(null!, 0, 123.0f, AlignmentType.Custom).DockDistanceMm));        // 123.0f + -27.0f = 96.0f (vadd.f32)

        // the clamp: below the float 0xC1800005 (-16.000009536743164) the distance becomes 0.0f; the compare is strict float less-than, so -15.9999 stays
        Assert.Equal(0u, Bits(new AlignWithObjectAction(null!, 0, -100.0f, AlignmentType.Custom).DockDistanceMm));                // -127.0f -> 0.0f
        Assert.Equal(0xC1800005u, Bits(AlignWithObjectAction.ClampThresholdMm));
        // an argument whose float sum is exactly the threshold (10.999990463256836f + -27.0f = -16.000009536743164f) is NOT below it: the strict compare keeps it
        Assert.Equal(0xC1800005u, Bits(new AlignWithObjectAction(null!, 0, 10.999990463256836f, AlignmentType.Custom).DockDistanceMm));
        // an invalid type leaves the distance 0.0f (bhi at 0x005533E4)
        Assert.Equal(0u, Bits(new AlignWithObjectAction(null!, 0, 55.0f, (AlignmentType)9).DockDistanceMm));

        // +0xBB: type 1 writes DockingMethod.Method2 (2), the others leave it 0.
        Assert.Equal(DockingMethod.Method2, new AlignWithObjectAction(null!, 0, 0, AlignmentType.LiftPlate).AlignmentDockingMethod);
        Assert.Equal(DockingMethod.Default, new AlignWithObjectAction(null!, 0, 0, AlignmentType.LiftFinger).AlignmentDockingMethod);

        Assert.Equal(1, AlignWithObjectAction.GetPreActionTypeFromAlignmentType(0));
        Assert.Equal(0, AlignWithObjectAction.GetPreActionTypeFromAlignmentType(1));
        Assert.Equal(1, AlignWithObjectAction.GetPreActionTypeFromAlignmentType(2));
        Assert.Equal(1, AlignWithObjectAction.GetPreActionTypeFromAlignmentType(3));
        Assert.Equal(1, AlignWithObjectAction.GetPreActionTypeFromAlignmentType(9));
    }

    /// <summary>
    /// M13-019. The production <c>xythetaEnvironment::Init(Json const&amp;)</c> 0x00851F9E uses the JSON
    /// <c>num_angles</c>; the hard-coded 16 is on the uncalled <c>Init(char const*)</c> 0x008528A8. A file
    /// with a different <c>num_angles</c> therefore produces a different heading count.
    /// </summary>
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ThePlannerUsesTheJsonNumAngles()
    {
        var prims = Prims();
        if (prims is not null) Assert.Equal(16, prims.NumAngles);
        const string json = "{\"resolution_mm\":10.0,\"num_angles\":2,\"angle_definitions\":[0.0,3.141592653589793],\"actions\":[],\"angles\":[{\"prims\":[]},{\"prims\":[]}]}";
        var set = MotionPrimitiveSet.Parse(json);
        Assert.Equal(2, set.NumAngles);
        Assert.Equal(2, set.Angles.Count);
    }

    /// <summary>
    /// M13-018. <c>DoPlanning</c> 0x00500090 returns 0 when <c>Replan</c> is 0 (failure), 3 when it is
    /// non-zero with an empty segment list, and 2 on success; the expansion cap it passes is
    /// 0x01C9C380 = 30,000,000.
    /// </summary>
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void DoPlanningReturnsTheEngineResultCodes()
    {
        Assert.Equal(30_000_000, LatticePlanner.MaxExpansions);
        var prims = Prims();
        if (prims is null) return;
        var planner = new LatticePlanner(new LatticeEnvironment(prims));

        // failure: no goals at all -> ComputePath returns null -> Replan == 0
        Assert.Equal(LatticePlanner.PlanningResult.Failure,
            planner.DoPlanning(new StateC(0, 0, 0), Array.Empty<StateC>(), out var none));
        Assert.Null(none);
        Assert.Equal(0, (int)LatticePlanner.PlanningResult.Failure);

        // empty plan: the start is already a goal -> segment list empty -> 3
        Assert.Equal(LatticePlanner.PlanningResult.EmptyPlan,
            planner.DoPlanning(new StateC(0, 0, 0), new[] { new StateC(0, 0, 0) }, out var empty));
        Assert.NotNull(empty);
        Assert.Equal(0, empty!.Actions.Count);
        Assert.Equal(3, (int)LatticePlanner.PlanningResult.EmptyPlan);

        // success -> 2
        Assert.Equal(LatticePlanner.PlanningResult.Success,
            planner.DoPlanning(new StateC(0, 0, 0), new[] { new StateC(300, 0, 0) }, out var plan));
        Assert.NotNull(plan);
        Assert.NotEmpty(plan!.Actions);
        Assert.Equal(2, (int)LatticePlanner.PlanningResult.Success);
    }

    /// <summary>
    /// M13-013. <c>DriveOffChargerContactsAction</c> is a <c>DriveStraightAction(10, 20, false)</c> with
    /// +0x44 = 7; <c>Init</c> returns 0 when not on the contacts, and <c>CheckIfDone</c> fails 0x04000009
    /// while the robot is still on them.
    /// </summary>
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void DriveOffChargerContactsActionFailsWhileStillOnTheContacts()
    {
        using var signals = SignalTestContext.Install();
        static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);
        Assert.Equal(0x41200000u, Bits(DriveOffChargerContactsAction.ConstructorDistanceMm));   // 10.0f, 0x00558232
        Assert.Equal(0x41A00000u, Bits(DriveOffChargerContactsAction.ConstructorSpeedMmps));    // 20.0f, 0x00558236
        Assert.Equal(7, DriveOffChargerContactsAction.RobotActionTypeDriveOffChargerContacts);
        Assert.Equal(0x04000009u, (uint)ActionResult.StillOnCharger);

        using var rig = new Rig();
        // not on the contacts at Init -> Init returns 0 and CheckIfDone returns 0 without driving
        rig.OnCharger = false; rig.State();
        var notOn = new DriveOffChargerContactsAction(rig.M) { Clock = () => 0f };
        Assert.Equal(ActionResult.Success, SignalTestContext.Result(notOn.RunAsync(default)));
        Assert.False(notOn.WasOnContactsAtInit);
        Assert.DoesNotContain(rig.Sent, m => m is AppendPathSegmentLine);

        // on the contacts at Init and still there after the drive -> 0x04000009
        rig.OnCharger = true; rig.State();
        // Contacts/result test: advancing firmware while the captured checker is pending
        // must not select the separate 30 s timeout branch (checked with an explicit clock).
        var on = new DriveOffChargerContactsAction(rig.M) { Clock = () => 0f };
        var task = on.RunAsync(default);
        SignalTestContext.Run(task, () => rig.Pump());
        Assert.True(on.WasOnContactsAtInit);
        Assert.Equal(ActionResult.StillOnCharger, task.Result);
    }

}
