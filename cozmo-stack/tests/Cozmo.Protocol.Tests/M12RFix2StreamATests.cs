using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// R-FIX2 stream A: the calibration-audit repairs in M12 (001, 002, 007, 010, 012, 017, 019), M5 (005, 017: the entropy branch) and M15-012.
/// Every expected value is the engine's: a bit pattern read from the cited instruction, or an independent emulation of the cited arithmetic, never the code's own output.
/// </summary>
public class M12RFix2StreamATests
{
    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;

    private static uint Bits(double d) => BitConverter.SingleToUInt32Bits((float)d);
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    private static void Spin(Func<bool> done, Action tick, int ms = 8000)
    {
        SignalTestContext.Until(done, tick);
    }

    // ------------------------------------------------------------------ M12-019

    /// <summary>
    /// M12-019: the docking error signal's ClampPoseToFlat tolerance is binary32 0x3F32B8C2 (movw 0x0063C182 / movt 0x0063C188, passed at 0x0063C194), not the
    /// double 0.698132 (0x3F32B8C7). Through <see cref="BlockWorld.ClampPoseToFlat"/>, the function the error signal calls: a tilt of one binary32 step below the constant is
    /// snapped flat, one step above it is left as it is (the old decimal would have snapped it).
    /// </summary>
    [Fact]
    public void M12_019_TheErrorSignalClampIsTheBinary32FortyDegrees()
    {
        Assert.Equal(0x3F32B8C2u, Bits(DockingSystem.ClampToFlatAngleRad));
        Assert.Equal((double)F(0x3F32B8C2), DockingSystem.ClampToFlatAngleRad);
        double below = F(0x3F32B8C1), above = F(0x3F32B8C3);
        var snapped = BlockWorld.ClampPoseToFlat(new Pose3d(Mat3.AboutY(below), new Vec3(1, 2, 3)), DockingSystem.ClampToFlatAngleRad);
        Assert.Equal(1.0, snapped.Rotation[2, 2], 9);                         // clamped: the object's Z is now the vertical
        var kept = BlockWorld.ClampPoseToFlat(new Pose3d(Mat3.AboutY(above), new Vec3(1, 2, 3)), DockingSystem.ClampToFlatAngleRad);
        Assert.Equal(Math.Cos(above), kept.Rotation[2, 2], 9);                // beyond the tolerance: left alone
    }

    // ------------------------------------------------------------------ M12-001

    /// <summary>
    /// M12-001, Block::GeneratePreActionPoses: the Flipping corner is binary32 0x42624EEF (rodata 0x004E5C60 added to size.x * 0.5f with vmul.f32 / vadd.f32 at
    /// 0x004E592C/0x004E5934; the Y is the movw/movt 0xC2624EEF at 0x004E5CB2..0x004E5CBC), not the double 56.5771 (0x42624EF3).
    /// </summary>
    [Fact]
    public void M12_001_TheFlippingCornerIsTheBinary32AtTheEnginesAddress()
    {
        Assert.Equal(0x42624EEFu, Bits(CubePreActionPoses.FlippingCornerMm));
        Assert.NotEqual(0x42624EEFu, BitConverter.SingleToUInt32Bits(56.5771f));        // the old decimal is a different float
        var (translation, angle) = CubePreActionPoses.BuiltPoseFor(PreActionType.Flipping, new Vec3(44, 44, 44));
        Assert.Equal((double)(22.0f + F(0x42624EEF)), translation.X);                    // vadd.f32 of size.x*0.5f and the rodata float
        Assert.Equal(-(double)F(0x42624EEF), translation.Y);
        Assert.Equal(-22.0, translation.Z);
    }

    /// <summary>
    /// M12-001, ComputePreActionPoseDistThreshold 0x00550098: the arithmetic is binary32 (vmul.f32 0x0055010A / 0x00550118, vadd.f32 0x0055011C, vsqrt.f32 0x00550122, sinf
    /// 0x00550140, vmul.f32 0x0055014E, vadd.f32 0x00550164). A relative translation of (30, 40, 12) and a tolerance of 0.5 rad, emulated in float.
    /// </summary>
    [Fact]
    public void M12_001_TheThresholdIsComputedInBinary32()
    {
        var goal = new Pose3d(Mat3.Identity, new Vec3(0, 0, 0));
        var obj = new Pose3d(Mat3.Identity, new Vec3(30, 40, 12));
        Assert.True(CubePreActionPoses.DistanceThresholdMm(goal, obj, 0.5, out double twice, out double once));
        float sum = 30f * 30f; sum += 40f * 40f; sum += 12f * 12f;
        float dist = MathF.Sqrt(sum);
        float expectedOnce = dist * MathF.Sin(0.5f);
        Assert.Equal(BitConverter.SingleToUInt32Bits(expectedOnce), BitConverter.SingleToUInt32Bits((float)once));
        Assert.Equal(BitConverter.SingleToUInt32Bits(expectedOnce + expectedOnce), BitConverter.SingleToUInt32Bits((float)twice));
        // a double computation differs from the binary32 one: the result is a float, widened exactly
        Assert.Equal((double)(float)once, once);
        Assert.Equal((double)(float)twice, twice);
    }

    // ------------------------------------------------------------------ M5-005 / M5-017

    /// <summary>
    /// M5-005 / M5-017: <c>SetSeed(0)</c> reads the entropy word ONCE (0x0082F898 <c>blx 0x4CCEA8</c>) and seeds mt19937 with it even when it is zero
    /// (0x0082F89C <c>mov r5, r0</c> straight into the loop at 0x0082F8A4..0x0082F8C4). The standard mt19937 seeded 0 starts 2357136044; seeded 5489 it starts 3499211612, 581869302.
    /// </summary>
    [Fact]
    public void M5_005_SeedZeroTakesOneEntropyWordAndUsesItEvenWhenItIsZero()
    {
        int reads = 0;
        var rng = new EngineRandom(0u, () => { reads++; return 0u; });
        Assert.Equal(1, reads);                                  // one read, no retry
        double expected = (2357136044.0 + 2546248239.0 * 4294967296.0) / 18446744073709551616.0;    // mt19937(0): 2357136044, 2546248239
        Assert.Equal(expected, rng.GetNextDbl());
        Assert.Equal(1, reads);
    }

    [Fact]
    public void M5_017_ANonzeroEntropyWordIsTheSeedAndANonzeroSeedReadsNone()
    {
        int reads = 0;
        var rng = new EngineRandom(0u, () => { reads++; return 5489u; });
        Assert.Equal(1, reads);
        Assert.Equal((3499211612.0 + 581869302.0 * 4294967296.0) / 18446744073709551616.0, rng.GetNextDbl());
        int none = 0;
        _ = new EngineRandom(5489u, () => { none++; return 1u; });
        Assert.Equal(0, none);                                   // a non-zero seed is used as given (no entropy read)
    }

    // ------------------------------------------------------------------ M12-002

    /// <summary>
    /// M12-002, PathComponent::ExecutePath 0x0064A3C2..0x0064A3CA: <c>ldrh r1,[r4,#0x42]; adds r1,#1; strh r1,[r4,#0x42]</c>, a plain u16 increment with no zero case, so the path
    /// id after 0xFFFF is 0 (and the ExecutePath message carries it) and then 1.
    /// </summary>
    [Fact]
    public void M12_002_ThePathIdWrapsFromFFFFToZero()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        var sender = new PathSender(rig.Robot);
        var none = Array.Empty<PathSegment>();
        for (int i = 0; i < 0xFFFF; i++) sender.Execute(none);
        Assert.Equal((ushort)0xFFFF, sender.LastPathId);
        Assert.Equal((ushort)0, sender.Execute(none));
        Assert.Equal((ushort)0, Assert.IsType<ExecutePath>(sender.Sent[^1]).EventId);
        Assert.Equal((ushort)1, sender.Execute(none));
    }

    // ------------------------------------------------------------------ M12-007 and M12-017

    private static (Rig Rig, ObservableObject Cube) MovingCubeRig()
    {
        var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(150, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        rig.Send(new ObjectMoved { Timestamp = rig.T, ObjectID = rig.ActiveIdOf(obj.ObjectId) });      // the cube reports itself moving (virtual IsMoving, 0x00553C8E)
        Assert.True(obj.IsMoving);
        return (rig, obj);
    }

    private static int VerifyLines(PickupObjectAction a) => a.Trace.Count(l => l.StartsWith("Verify -> "));

    /// <summary>
    /// M12-007, PickupObjectAction::Verify 0x00553BE0: the first call stamps +0x10C (0x00553BF8); with the object still moving the compare at 0x00553C9E is
    /// <c>cmp r5, r0; bls</c> (unsigned lower-or-same) against stamp + 500 and branches to 0x00554056, which returns RUNNING (0x01000000). The action therefore stays running
    /// while now &lt;= stamp + 500 (the boundary itself included), and past it the pick-up has failed (0x00553CAA: SetCarriedObjectAsUnattached(true), Retry result).
    /// Driven through the action, with the robot's state timestamps as the clock.
    /// </summary>
    [Fact]
    public async Task M12_007_AStillMovingObjectKeepsVerifyRunningUntilTheAllowanceThenFails()
    {
        if (Lib is null) return;
        using var signals_rig = SignalTestContext.Install();
        var (rig, obj) = MovingCubeRig();
        using var _ = rig;
        var pick = new PickupObjectAction(rig.M, 7) { CheckPreActionPose = false };
        var task = pick.RunAsync(default);
        Spin(() => VerifyLines(pick) >= 1 || task.IsCompleted, () => { rig.Frame(); rig.Pump(); });
        Assert.False(task.IsCompleted);                                              // Verify returned RUNNING: the old code fell through to Success
        Assert.Equal("Verify -> Running", pick.Trace.Last(l => l.StartsWith("Verify -> ")));
        uint stamp = pick.VerifyStartedAt;                                            // the +0x10C stamp Verify took at its first call (0x00553BF8), not whatever state arrived since

        // 15 ms short of the boundary, then exactly on it: both still RUNNING
        foreach (uint stateTime in new[] { stamp + 485, stamp + 500 })
        {
            int before = VerifyLines(pick);
            rig.T = stateTime - 33;
            rig.State();
            Spin(() => VerifyLines(pick) > before, () => { });
            Assert.False(task.IsCompleted);
            Assert.Equal("Verify -> Running", pick.Trace.Last(l => l.StartsWith("Verify -> ")));
        }
        // the registration of the 0xC5/0xDA handlers (M12-017) lives as long as the running action
        var lifts = new List<bool>(); var posts = new List<bool>();
        rig.M.Docking.LiftLoad += lifts.Add; rig.M.Docking.MovingLiftPostDock += posts.Add;
        rig.Send(new LiftLoad { Field0 = true });
        rig.Send(new MovingLiftPostDock { Field0 = (byte)DockAction.PickupLow });
        Assert.Equal(new[] { true }, lifts);
        Assert.Equal(new[] { true }, posts);                                         // the byte equals the action's DockAction (+0x80): M12-005 compare

        // one millisecond past the boundary: failed
        rig.T = stamp + 501 - 33;
        rig.State();
        Spin(() => task.IsCompleted, () => { });
        Assert.Equal(ActionResult.PickupObjectStillMoving, await task);          // r4 (0x03000004) + 0x10, 0x00553CEA: ABORT category, not Retry
        Assert.Contains(pick.Trace, l => l == "PickupObjectAction.Verify.ObjectStillMoving");
        Assert.False(rig.M.Docking.Carrying.IsCarryingObject);                       // SetCarriedObjectAsUnattached(true)
        Assert.False(obj.IsLocated);                                                 // ... which deletes the located object

        // the action is gone: its handlers are unregistered, so the same messages reach nobody
        rig.Send(new LiftLoad { Field0 = true });
        rig.Send(new MovingLiftPostDock { Field0 = (byte)DockAction.PickupLow });
        Assert.Single(lifts); Assert.Single(posts);
    }

    /// <summary>
    /// M12-007, Verify's order: byte [[+0xCC]+5] (didSucceed, 0x00553C06) is 0 -> the moving/seen-recently block (0x00553C72..0x00553D70) is skipped (branch to 0x00553D72), and the carry check
    /// (0x00553DE4, 0x00553FC8) returns 0x04000005. A failed dock of a cube that reports itself moving is therefore 0x04000005, not ObjectStillMoving, and never 0x04000000.
    /// </summary>
    [Fact]
    public async Task M12_007_AFailedDockSkipsTheMovingBlockAndEndsAtTheCarryCheck()
    {
        if (Lib is null) return;
        using var signals_rig = SignalTestContext.Install();
        var (rig, _) = MovingCubeRig();
        using var _ = rig;
        rig.DockSucceeds = false;
        var pick = new PickupObjectAction(rig.M, 7) { CheckPreActionPose = false };
        var task = pick.RunAsync(default);
        Spin(() => task.IsCompleted, () => { rig.Frame(); rig.Pump(); });
        Assert.Equal(ActionResult.PickupRetry, await task);
        Assert.Equal(0x04000005u, (uint)ActionResult.PickupRetry);
        Assert.Contains(pick.Trace, l => l == "PickupObjectAction.Verify.ExpectedCarryingObject");
        Assert.DoesNotContain(pick.Trace, l => l.Contains("ObjectStillMoving"));
    }

    /// <summary>
    /// M12-017, IDockAction::Init registers the 0xC5 handler (0x005516EA) and the 0xDA handler (0x00551750) for the action: with no dock action there is no handler, so
    /// MovingLiftPostDock and LiftLoad reach nobody. (The old code subscribed once for the DockingSystem's lifetime and raised MovingLiftPostDock(false) with no action at all.)
    /// </summary>
    [Fact]
    public void M12_017_WithNoDockActionTheLiftMessagesHaveNoHandler()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        var raised = new List<string>();
        rig.M.Docking.LiftLoad += v => raised.Add("LiftLoad " + v);
        rig.M.Docking.MovingLiftPostDock += v => raised.Add("MovingLiftPostDock " + v);
        rig.Send(new LiftLoad { Field0 = true });
        rig.Send(new MovingLiftPostDock { Field0 = (byte)DockAction.PickupLow });
        Assert.Empty(raised);
        using (rig.M.Docking.RegisterActionHandlers(DockAction.PickupLow))
        {
            rig.Send(new MovingLiftPostDock { Field0 = (byte)DockAction.PickupLow });
            rig.Send(new MovingLiftPostDock { Field0 = (byte)DockAction.RollLow });
        }
        Assert.Equal(new[] { "MovingLiftPostDock True", "MovingLiftPostDock False" }, raised);
        rig.Send(new MovingLiftPostDock { Field0 = (byte)DockAction.PickupLow });
        Assert.Equal(2, raised.Count);
    }

    /// <summary>
    /// M12-017, IDockAction::CheckIfDone 0x005521AC: DockWithObject is called first (0x005522AE) and AddSquint(.., "DockSquint", ..) only after it, on the started-docking branch
    /// (0x00552394). Through the action: when the dock message is logged the squint layer does not exist yet; after the dock it does, and the action recorded the add.
    /// </summary>
    [Fact]
    public void M12_017_TheDockSquintIsAddedAfterDockWithObjectNotBefore()
    {
        if (Lib is null) return;
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(150, 0);
        Assert.Single(rig.Frame().Objects);
        bool SquintLayer() => rig.Robot.Animations.Scheduler.Layers.Face.AllLayers.Any(l => l.Name == "DockSquint");
        bool? presentAtDockLog = null;
        rig.M.Docking.Log += s => { if (s.StartsWith("Docking with marker")) presentAtDockLog = SquintLayer(); };
        var pick = new PickupObjectAction(rig.M, 7) { CheckPreActionPose = false };
        Assert.False(pick.DockSquintAdded);
        var task = pick.RunAsync(default);
        Spin(() => presentAtDockLog != null || task.IsCompleted, () => { rig.Frame(); });
        Assert.Equal(false, presentAtDockLog);                                       // not added before / when DockWithObject went out
        Assert.False(SquintLayer());
        Assert.False(pick.DockSquintAdded);
        // IDockAction+0x94 starts 0; the first state that reports IS_PICKING_OR_PLACING (bit 2, [[+0xCC]+4]) is the rising edge that adds the squint (0x0055234A..0x00552398)
        var pickingOrPlacing = (uint)(RobotStatusFlag.IsPickingOrPlacing | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
        rig.State(flags: (uint)(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos));
        Assert.False(SquintLayer());                                                 // a state with the bit clear adds nothing
        rig.State(flags: pickingOrPlacing);
        Assert.True(SquintLayer());
        Assert.True(pick.DockSquintAdded);
        int layers = rig.Robot.Animations.Scheduler.Layers.Face.AllLayers.Count(l => l.Name == "DockSquint");
        rig.State(flags: pickingOrPlacing);                                          // the bit stays set: no second squint
        Assert.Equal(layers, rig.Robot.Animations.Scheduler.Layers.Face.AllLayers.Count(l => l.Name == "DockSquint"));
        // 1 -> 0 -> 1: +0x94 never goes back to 0 within a dock (stored only on the prev == 0 path, 0x0055234C), so there is no second add
        rig.State(flags: (uint)(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos));
        rig.State(flags: pickingOrPlacing);
        Assert.Equal(layers, rig.Robot.Animations.Scheduler.Layers.Face.AllLayers.Count(l => l.Name == "DockSquint"));
        Assert.Equal(1, layers);
        Spin(() => task.IsCompleted, () => { rig.Frame(); rig.Pump(); });
    }

    // ------------------------------------------------------------------ M15-012

    private static BehaviorContext Ctx(Rig rig) => new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };

    private static void RunBehavior(Rig rig, PutDownBlockBehavior b, BehaviorContext ctx, Action<double>? each = null)
    {
        b.ActionTaskRunner = SignalTestContext.Schedule;
        if (SynchronizationContext.Current is SignalTestContext signals) b.WorkPosted += signals.Notify;
        double t = 0;

        while (b.Update(ctx, t))
        {
            each?.Invoke(t);
            rig.Pump(); t += 33;
            SignalTestContext.AdvanceBehavior(b);
        }
    }

    /// <summary>
    /// M15-012, CreateLookAfterPlaceAction 0x005C8174: the head angle is Radians binary32 0xBEB2B8C2 (0x005C81A6..0x005C81AC); head and the drive are ONE CompoundActionParallel
    /// (0x005C8196..0x005C8210) whose DriveStraightAction(robot, -30.0f) is the two-argument constructor: its default speed for a distance &lt; 0 is 0xC2A00000, -80 mm/s
    /// (0x00547268, `it ge; addge r1,#4` 0x00547174). The block is built only when the carried id is set (gate 0x005C818C..0x005C8194); in the live flow (InitInternal 0x005C7FD0:
    /// drive, 0x19A animation, StartActing(LookDownAtBlock)) it still is, and the release (0x005C8480..0x005C84CE) comes after the look-after-place action ends.
    /// </summary>
    [Fact]
    public void M15_012_WithACarriedObjectTheLookAfterPlaceBuildsHeadAndAMinusEightyDrive()
    {
        Assert.Equal(0xBEB2B8C2u, BitConverter.SingleToUInt32Bits(PutDownBlockBehavior.LookDownHeadAngleRad));
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.M.Docking.Carrying.SetCarrying(7);
        var ctx = Ctx(rig);
        rig.Robot.Animations.ManualTicking = true;
        rig.Robot.Animations.ClockMs = () => rig.Clock.NowMs;
        var b = new PutDownBlockBehavior(rig.M) { ActionTaskRunner = SignalTestContext.Schedule };
        b.WorkPosted += signals_rig.Notify;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        RunBehavior(rig, b, ctx);
        Assert.Equal(2, rig.Sent.OfType<ExecutePath>().Count());                     // the random back-up and the look-down drive
        var head = Assert.Single(rig.Sent.OfType<SetHeadAngle>());
        Assert.Equal(0xBEB2B8C2u, BitConverter.SingleToUInt32Bits(head.AngleRad));
        var line = rig.Sent.OfType<AppendPathSegmentLine>().Last();
        Assert.Equal(0xC2A00000u, BitConverter.SingleToUInt32Bits(line.Speed.SpeedMmps));
        int keepAlive = b.Trace.ToList().FindIndex(l => l.Contains("PutDownBlockKeepAlive"));
        int released = b.Trace.ToList().FindIndex(l => l.Contains("SetCarriedObjectAsUnattached(false)"));
        Assert.True(keepAlive >= 0 && released > keepAlive);                         // the release is the end of the look-after-place action
        Assert.False(rig.M.Docking.Carrying.IsCarryingObject);
    }

    /// <summary>M15-012 gate: with no carried object (-1 at [[robot+0x284]+8]) `beq 0x005C8264` skips the head+drive parallel and the WaitForImagesAction; only the keep-alive remains.</summary>
    [Fact]
    public void M15_012_WithNoCarriedObjectOnlyTheKeepAliveRemains()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        var ctx = Ctx(rig);
        rig.Robot.Animations.ManualTicking = true;
        rig.Robot.Animations.ClockMs = () => rig.Clock.NowMs;
        var b = new PutDownBlockBehavior(rig.M) { ActionTaskRunner = SignalTestContext.Schedule };
        b.WorkPosted += signals_rig.Notify;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        rig.Pump();
        rig.Sent.Clear();
        b.LookDownAtBlock();
        RunBehavior(rig, b, ctx);
        Assert.Empty(rig.Sent.OfType<SetHeadAngle>());
        Assert.Empty(rig.Sent.OfType<ExecutePath>());
        Assert.DoesNotContain(b.Trace, l => l.Contains("image(s) after the place"));
        Assert.Contains(b.Trace, l => l.Contains("PutDownBlockKeepAlive"));
    }

    /// <summary>
    /// M15-012: the look-after-place sequential compound adds the parallel with ignoreFailure = 0 (0x005C8220), so a failed child ends the parallel at once with its failure
    /// (0x0054FB36..0x0054FBCC) and CompoundActionSequential::UpdateInternal 0x0054F70C returns it: the image wait and the keep-alive do not run. The -80 mm/s drive is interrupted.
    /// </summary>
    [Fact]
    public void M15_012_AFailedLookDownDriveEndsTheCompoundBeforeTheImageWaitAndTheKeepAlive()
    {
        using var signals_rig = SignalTestContext.Install();
        using var rig = new Rig();
        rig.M.Docking.Carrying.SetCarrying(7);
        rig.HoldPath = true;
        var ctx = Ctx(rig);
        rig.Robot.Animations.ManualTicking = true;
        rig.Robot.Animations.ClockMs = () => rig.Clock.NowMs;
        var b = new PutDownBlockBehavior(rig.M) { ActionTaskRunner = SignalTestContext.Schedule };
        b.WorkPosted += signals_rig.Notify;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        b.LookDownAtBlock();
        int handled = 0;
        RunBehavior(rig, b, ctx, _ =>
        {
            var eps = rig.Sent.OfType<ExecutePath>().ToList();
            for (; handled < eps.Count; handled++)
                rig.Send(new PathFollowingEvent { EventId = eps[handled].EventId, EventType = (byte)PathEventType.Interrupted });
        });
        Assert.True(handled > 0);
        Assert.Contains(b.Trace, l => l.Contains("the look-after-place compound failed"));
        Assert.DoesNotContain(b.Trace, l => l.Contains("PutDownBlockKeepAlive"));
        Assert.DoesNotContain(b.Trace, l => l.Contains("image(s) after the place"));
        Assert.False(rig.M.Docking.Carrying.IsCarryingObject);                       // the callback runs for any result: 0x005C8480
    }
}
