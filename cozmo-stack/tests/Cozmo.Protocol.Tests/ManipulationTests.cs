using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M12: pre-action poses, the path sender and planner, drive-to-pose and drive-to-object, the firmware docking
/// exchange, carrying state, the dock actions and the transcribed manipulation behaviours. A fake robot side
/// answers the messages the way the firmware does (path events, pick-and-place results) and moves the fake
/// robot's pose, so every flow runs offline.
/// </summary>
public class ManipulationTests
{
    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;

    private static Pose3d CubeAt(double x, double y, double yaw = 0) => new(Mat3.AboutZ(yaw), new Vec3(x, y, CubeGeometry.CubeSizeMm / 2));

    private static void SpinUntil(Func<bool> cond, Action? tick = null, int ms = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cond()) { tick?.Invoke(); if (sw.ElapsedMilliseconds > ms) throw new TimeoutException("condition not met"); Thread.Sleep(5); }
    }

    // ------------------------------------------------------------------ pre-action poses

    [Fact]
    public void ACubeHasFourDockingPosesSeventyFiveMillimetresOutFacingItsSides()
    {
        var cube = new ObservableObject(7, ObjectType.Block_LIGHTCUBE1, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1)) { Pose = CubeAt(200, 50, 0.3), PoseState = PoseState.Known };
        var poses = CubePreActionPoses.For(cube, PreActionType.Docking);
        Assert.Equal(4, poses.Count);
        foreach (var p in poses)
        {
            Assert.Equal(0, p.WorldPose.Translation.Z, 6);
            var toCentre = cube.Pose.Translation - p.WorldPose.Translation;
            double dist = Math.Sqrt(toCentre.X * toCentre.X + toCentre.Y * toCentre.Y);
            Assert.InRange(dist, 75 + 22 - 0.01, 75 + 22 + 0.01);                       // 75 mm from the face, 22 mm half-cube
            Assert.InRange(Math.Abs(StraightLinePlanner.Wrap(Math.Atan2(toCentre.Y, toCentre.X) - p.WorldPose.AngleAroundZ)), 0, 1e-6);   // facing the cube
            Assert.DoesNotContain(p.Marker.Face, new[] { BlockFace.Top, BlockFace.Bottom });
        }
        Assert.Equal(4, CubePreActionPoses.For(cube, PreActionType.Rolling).Count);
        Assert.Equal(8, CubePreActionPoses.For(cube, PreActionType.Flipping).Count);
        Assert.Empty(CubePreActionPoses.For(cube, PreActionType.None));
        var robot = new Pose3d(Mat3.Identity, new Vec3(0, 40, 0));
        var closest = CubePreActionPoses.Closest(poses, robot)!;
        Assert.Equal(BlockFace.Front, closest.Marker.Face);                                 // the face towards the origin
        // the "close enough" box grows with the distance to the object: 97 mm * sin(7.5 deg)
        Assert.InRange(CubePreActionPoses.DistanceThresholdMm(cube.Pose, closest.WorldPose, DriveToObjectAction.PreActionAngleToleranceRad), 12.5, 12.8);
    }

    // ------------------------------------------------------------------ paths

    [Fact]
    public void ThePlannerTurnsDrivesAndTurnsAndTheSenderPacksTheEnginesSegments()
    {
        var robot = new Pose3d(Mat3.AboutZ(0), new Vec3(0, 0, 0));
        var goal = new Pose3d(Mat3.AboutZ(Math.PI / 2), new Vec3(100, 100, 0));
        var path = StraightLinePlanner.Plan(robot, goal);
        Assert.Equal(3, path.Count);
        var t1 = Assert.IsType<PathSegment.PointTurn>(path[0]);
        Assert.Equal(Math.PI / 4, t1.TargetAngleRad, 6);
        var line = Assert.IsType<PathSegment.Line>(path[1]);
        Assert.Equal((0, 0, 100, 100), (line.FromX, line.FromY, line.ToX, line.ToY));
        Assert.Equal(100f, line.SpeedMmps);
        var t2 = Assert.IsType<PathSegment.PointTurn>(path[2]);
        Assert.Equal(Math.PI / 2, t2.TargetAngleRad, 6);

        using var rig = new Rig();
        ushort id = rig.M.Paths.Execute(path);
        var sent = rig.Pump();
        Assert.Equal(1, id);
        Assert.True(sent.Count >= 5, $"only {sent.Count} messages decoded from {rig.Robot.Transport.OfflineOutbound.Count} frames: " +
            string.Join(" | ", rig.Robot.Transport.OfflineOutbound.Select(f => f.Type + ":" + string.Join(",", f.Messages.Select(m => m.Type + "/" + m.Seq + "/" + m.Payload.Length)))));
        Assert.Collection(sent.Where(m => m is ClearPath or AppendPathSegmentPointTurn or AppendPathSegmentLine or ExecutePath),
            m => Assert.Equal(1, Assert.IsType<ClearPath>(m).Unknown),
            m => { var pt = Assert.IsType<AppendPathSegmentPointTurn>(m); Assert.Equal((float)(Math.PI / 4), BitConverter.UInt32BitsToSingle(pt.Field2)); Assert.Equal(2f, pt.Field4.SpeedMmps); Assert.Equal(1, pt.Field5); },
            m => { var l = Assert.IsType<AppendPathSegmentLine>(m); Assert.Equal(100f, BitConverter.UInt32BitsToSingle(l.Field2)); Assert.Equal(200f, l.Field4.AccelMmps2); Assert.Equal(500f, l.Field4.DecelMmps2); Assert.Equal(28, l.ToBytes().Length - 1); },
            m => Assert.IsType<AppendPathSegmentPointTurn>(m),
            m => { var e = Assert.IsType<ExecutePath>(m); Assert.Equal(1, e.EventId); Assert.False(e.Unknown); });
        // the fake robot followed it
        Assert.Equal(100f, rig.X); Assert.Equal(100f, rig.Y); Assert.Equal((float)(Math.PI / 2), rig.Angle);
    }

    [Fact]
    public void DriveToPoseSucceedsWhenThePathCompletesAtTheGoal()
    {
        using var rig = new Rig();
        var drive = new DriveToPoseAction(rig.M) { Goal = new Pose3d(Mat3.AboutZ(0.5), new Vec3(150, -40, 0)) };
        var task = drive.RunAsync(default);
        SpinUntil(() => task.IsCompleted, () => rig.Pump());
        Assert.Equal(ActionResult.Success, task.Result);
        Assert.Contains(drive.Trace, l => l.Contains("Success"));
        // the head went to the path-following angle
        Assert.Contains(rig.Sent, m => m is SetHeadAngle sh && Math.Abs(sh.AngleRad - (-0.261799f)) < 1e-4);
    }

    // ------------------------------------------------------------------ docking

    /// <summary>
    /// Every byte of <c>DockWithObject</c>, where the engine puts it (fidelity manifest M12-005).
    ///
    /// The three speeds start at the <em>second</em> word, not the first: the builder at 0x0063BD50
    /// dereferences a local the caller has just set to zero into word 0, then speed, acceleration and
    /// deceleration from <c>IDockAction</c> +0xAC, +0xB0 and +0xB4. This stack used to write them one
    /// word early, so every dock it sent carried its speed in word 0 and no deceleration at all.
    /// </summary>
    [Fact]
    public void TheDockMessagePutsEveryFieldWhereTheEngineDoes()
    {
        var m = DockingSystem.Message(60f, 200f, 500f, DockAction.PickupLow,
                                      unlockLiftTrack: true, method: DockingMethod.Method2, flag8: true);
        var b = m.ToBytes();
        Assert.Equal(22, b.Length);                        // tag + 21
        Assert.Equal(0u, BitConverter.ToUInt32(b, 1));     // the engine's literal zero, not a placeholder
        Assert.Equal(60f, BitConverter.ToSingle(b, 5));    // speed        IDockAction +0xAC
        Assert.Equal(200f, BitConverter.ToSingle(b, 9));   // accel        +0xB0
        Assert.Equal(500f, BitConverter.ToSingle(b, 13));  // decel        +0xB4
        Assert.Equal((byte)DockAction.PickupLow, b[17]);   // DockAction   +0x80
        Assert.Equal(1, b[18]);                            // ctor bool    +0x95
        Assert.Equal(0, b[19]);                            //              +0xBA, never written
        Assert.Equal(2, b[20]);                            // DockingMethod +0xBB
        Assert.Equal(1, b[21]);                            //              +0xC1
    }

    /// <summary>
    /// And that field 6 stays zero however the dock is asked for: nothing in the engine writes
    /// <c>IDockAction</c> +0xBA after the constructor clears it, so zero there is the engine's value.
    /// </summary>
    [Theory]
    [InlineData(DockAction.PickupLow)]
    [InlineData(DockAction.PlaceLow)]
    [InlineData(DockAction.RollLow)]
    [InlineData(DockAction.PopAWheelie)]
    public void FieldSixIsZeroForEveryDockAction(DockAction action)
    {
        var b = DockingSystem.Message(1f, 2f, 3f, action, true, DockingMethod.Method2, true).ToBytes();
        Assert.Equal(0, b[19]);
        Assert.Equal((byte)action, b[17]);
    }

    /// <summary>
    /// The action classes carry the values their engine counterparts write: a pickup sends docking
    /// method 2 and sets field 8, where the base constructor leaves both at zero.
    /// </summary>
    [Fact]
    public void APickupSendsTheDockingMethodAndFlagTheEnginesPickupSets()
    {
        var b = DockingSystem.Message(60f, 200f, 500f, DockAction.PickupLow,
                                      method: DockingMethod.Method2, flag8: true).ToBytes();
        Assert.Equal(2, b[20]);
        Assert.Equal(1, b[21]);

        var plain = DockingSystem.Message(60f, 200f, 500f, DockAction.PlaceLow).ToBytes();
        Assert.Equal(0, plain[20]);
        Assert.Equal(0, plain[21]);
    }

    [Fact]
    public void DockingStreamsTheErrorSignalFromEachFrameAndPicksUpOnTheRobotsResult()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(150, 0);
        var r = rig.Frame();
        var obj = Assert.Single(r.Objects).Object;
        var marker = obj.Markers.First(k => k.Code == MarkerType.LightCubeI_Front);
        rig.ErrorSignalsBeforeResult = 2;
        var dock = rig.M.Docking.DockAsync(obj, marker, DockAction.PickupLow, PathMotionProfile.Default, flag8: true, timeout: TimeSpan.FromSeconds(5));
        rig.Pump();
        Assert.Contains(rig.Sent, m => m is DockWithObject);
        Assert.Equal(PoseState.Dirty, obj.PoseState);
        // one signal from the frame already in hand, another from the next frame
        Assert.Equal(1, rig.M.Docking.ErrorSignalsSent);
        rig.Frame(); rig.Pump();
        SpinUntil(() => dock.IsCompleted, () => rig.Pump());
        Assert.Equal(2, rig.M.Docking.ErrorSignalsSent);
        var signal = rig.Sent.OfType<DockingErrorSignal>().First();
        Assert.InRange(signal.XDist, 120, 135);            // the front face is 128 mm ahead
        Assert.InRange(Math.Abs(signal.YDist), 0, 5);
        Assert.InRange(Math.Abs(StraightLinePlanner.Wrap(signal.Angle)), 0, 0.1);   // yaw + pi/2 of the marker frame: facing squarely
        Assert.Equal(rig.T - 33, signal.Timestamp);        // the frame's timestamp, and it comes first
        // the timestamp is word 0 and the geometry follows it: UpdateDockingErrorSignal 0x0063C14A
        var bytes = signal.ToBytes();
        Assert.Equal(23, bytes.Length);                    // tag + 22
        Assert.Equal(rig.T - 33, BitConverter.ToUInt32(bytes, 1));
        Assert.Equal(signal.XDist, BitConverter.ToSingle(bytes, 5));
        Assert.Equal(signal.Angle, BitConverter.ToSingle(bytes, 17));
        var result = dock.Result!;
        Assert.True(result.Succeeded);
        Assert.Equal(BlockStatus.BlockPickedUp, result.Status);
        Assert.True(rig.M.Docking.Carrying.IsCarrying(7));
    }

    [Fact]
    public void AFailedDockReportsAndNothingIsCarried()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(150, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        rig.DockSucceeds = false;
        var dock = rig.M.Docking.DockAsync(obj, obj.Markers.First(k => k.Code == MarkerType.LightCubeI_Front), DockAction.PickupLow, PathMotionProfile.Default, timeout: TimeSpan.FromSeconds(5));
        SpinUntil(() => dock.IsCompleted, () => rig.Pump());
        Assert.False(dock.Result!.Succeeded);
        Assert.False(rig.M.Docking.Carrying.IsCarryingObject);
    }

    // ------------------------------------------------------------------ actions

    [Fact]
    public void DriveToObjectGoesToTheClosestPreDockPoseThenPickupSucceeds()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(200, 30, 0.2);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var drive = new DriveToObjectAction(rig.M, 7, PreActionType.Docking);
        var task = drive.RunAsync(default);
        SpinUntil(() => task.IsCompleted, () => rig.Pump());
        Assert.Equal(ActionResult.Success, task.Result);
        var chosen = drive.Chosen!;
        Assert.InRange(Math.Abs(rig.X - chosen.WorldPose.Translation.X), 0, 0.5);
        Assert.InRange(Math.Abs(rig.Y - chosen.WorldPose.Translation.Y), 0, 0.5);
        // now pick it up from there: the cube is 97 mm ahead, so the frame sees its front face
        rig.Frame();
        var pickup = new PickupObjectAction(rig.M, 7);
        var pt = pickup.RunAsync(default);
        SpinUntil(() => pt.IsCompleted, () => { rig.Pump(); });
        Assert.Equal(ActionResult.Success, pt.Result);
        Assert.Equal(DockAction.PickupLow, pickup.SelectedDockAction);
        Assert.True(rig.M.Docking.Carrying.IsCarrying(7));
        Assert.Contains(pickup.Trace, l => l.Contains("SUCCEEDED"));
    }

    [Fact]
    public void ADockActionRefusesWhenNotNearAPreActionPose()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(300, 60);                                          // its nearest pre-dock pose is 200 mm away
        Assert.Single(rig.Frame().Objects);
        var pickup = new PickupObjectAction(rig.M, 7);
        var r = pickup.RunAsync(default).GetAwaiter().GetResult();
        Assert.Equal(ActionResult.DidNotReachPreActionPose, r);
        Assert.DoesNotContain(rig.Pump(), m => m is DockWithObject);
    }

    [Fact]
    public void PickupSelectsHighDockForACubeOnTopOfAnother()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(120, 0, 66));       // sitting on another cube
        rig.Head = 0.1f;
        var r = rig.Frame();
        Assert.Single(r.Objects);
        rig.X = 120 - 97; rig.State();                                       // at the pre-dock pose
        var pickup = new PickupObjectAction(rig.M, 7) { CheckPreActionPose = false };
        var t = pickup.RunAsync(default);
        SpinUntil(() => t.IsCompleted, () => rig.Pump());
        Assert.Equal(DockAction.PickupHigh, pickup.SelectedDockAction);
    }

    [Fact]
    public void PlaceOnGroundNeedsACarriedObjectAndReleasesIt()
    {
        using var rig = new Rig();
        var place = new PlaceObjectOnGroundAction(rig.M);
        Assert.Equal(ActionResult.NotCarryingObjectAbort, place.RunAsync(default).GetAwaiter().GetResult());
        rig.M.Docking.Carrying.SetCarrying(7);
        var t = new PlaceObjectOnGroundAction(rig.M).RunAsync(default);
        SpinUntil(() => t.IsCompleted, () => rig.Pump());
        Assert.Equal(ActionResult.Success, t.Result);
        Assert.False(rig.M.Docking.Carrying.IsCarryingObject);
        var msg = rig.Sent.OfType<PlaceObjectOnGround>().Single();
        Assert.Equal(60f, BitConverter.UInt32BitsToSingle(msg.Field0));
    }

    // ------------------------------------------------------------------ behaviours

    private BehaviorContext Ctx(Rig rig) => new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };

    /// <summary>Runs a behaviour to completion, pumping the fake robot and ticking, with fresh frames when asked.</summary>
    private static void RunToEnd(Rig rig, SteppedBehavior b, BehaviorContext ctx, Func<bool>? frames = null, int ms = 8000)
    {
        double t = 0;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (b.Update(ctx, t))
        {
            rig.Pump();
            if (frames?.Invoke() ?? false) rig.Frame();
            t += 33;
            if (sw.ElapsedMilliseconds > ms) throw new TimeoutException("behaviour did not finish: " + string.Join(" | ", b.Trace));
            Thread.Sleep(5);
        }
        b.Stop(BehaviorStopReason.Completed);
    }

    [Fact]
    public void PickUpCubeReactsDrivesDocksAndCelebrates()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(220, -20, 0.1);
        Assert.Single(rig.Frame().Objects);
        var ctx = Ctx(rig);
        var b = new PickUpCubeBehavior(rig.M);
        Assert.Equal(7u, ClosestId(rig));
        RunToEnd(rig, b, ctx, frames: () => true);
        Assert.True(rig.M.Docking.Carrying.IsCarrying(7), string.Join(" | ", b.Trace));
        Assert.Contains(b.Trace, l => l.Contains("SparkPickupInitialCubeReaction"));
        Assert.Contains(b.Trace, l => l.Contains("PickupBlockHelper"));
        Assert.Contains(b.Trace, l => l.Contains("ReactToBlockPickupSuccess"));
        Assert.Contains(b.Trace, l => l.Contains("objective achieved"));
        Assert.Equal(1, b.Helper!.Attempts);
    }

    private static uint? ClosestId(Rig rig) => rig.Vision.World.LocatedObjects.FirstOrDefault()?.ObjectId;

    [Fact]
    public void PutDownBlockBacksUpPlaysThePutDownAndLooksDown()
    {
        using var rig = new Rig();
        rig.M.Docking.Carrying.SetCarrying(7);
        var ctx = Ctx(rig);
        var b = new PutDownBlockBehavior(rig.M);
        Assert.True(b.IsRunnable(ctx) || rig.Robot.Animations.Library is null);   // runnable gates on the animation library offline
        RunToEnd(rig, b, ctx, frames: () => true);
        Assert.InRange(b.BackUpMm, -75, -45);
        Assert.False(rig.M.Docking.Carrying.IsCarryingObject);
        Assert.Contains(b.Trace, l => l.Contains("PutDownBlockPutDown"));          // "play X" with assets, "X: no animation assets" without
        Assert.Contains(b.Trace, l => l.Contains("PutDownBlockKeepAlive"));
        // two straight drives: the random back-up and the 30 mm look-down drive, both as line paths
        Assert.Equal(2, rig.Sent.OfType<ExecutePath>().Count());
        Assert.Contains(rig.Sent, m => m is SetHeadAngle sh && Math.Abs(sh.AngleRad + 0.349066f) < 1e-4);
        // a behaviour that only makes sense while carrying
        var again = new PutDownBlockBehavior(rig.M);
        Assert.False(again.IsRunnable(ctx));
    }

    [Fact]
    public void RollBlockSucceedsWhenTheUpAxisChanges()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        // a cube on its side: X axis up
        rig.Cube = new Pose3d(Mat3.AboutY(-Math.PI / 2), new Vec3(200, 0, 22));
        var r = rig.Frame();
        Assert.Single(r.Objects);
        Assert.NotEqual(UpAxis.ZPositive, r.Objects[0].Object.UpAxisFromPose());
        rig.DockOutcome = BlockStatus.NoBlock;
        var ctx = Ctx(rig);
        var b = new RollBlockBehavior(rig.M);
        // when the dock reports, the cube has been rolled upright; the following frames see it that way
        rig.OnDockResult = () => rig.Cube = CubeAt(rig.Cube!.Value.Translation.X, rig.Cube.Value.Translation.Y);
        RunToEnd(rig, b, ctx, frames: () => true);
        Assert.Equal(1, rig.DockResults);
        Assert.Equal(RollBlockBehavior.Phase.Idle, b.CurrentPhase);
        Assert.Contains(b.Trace, l => l.Contains("RollBlockSuccess"));
        Assert.Contains(b.Trace, l => l.Contains("RollSucceeded"));
    }

    [Fact]
    public void TheShippedManipulationSetHasThirteenBehavioursWithTheConfigIds()
    {
        using var rig = new Rig();
        var set = ShippedBehaviors.Manipulation(rig.M);
        Assert.Equal(13, set.Count);
        Assert.Equal(13, set.Select(b => b.Id).Distinct().Count());
        Assert.Equal(2, set.Count(b => b.Class == "PickUpCube"));
        Assert.Equal(4, set.Count(b => b.Class == "PutDownBlock"));
        Assert.Equal(4, set.Count(b => b.Class == "RollBlock"));
        Assert.Equal(2, set.Count(b => b.Class == "StackBlocks"));
        Assert.Single(set, b => b.Class == "PickUpAndPutDownCube");
    }
}
