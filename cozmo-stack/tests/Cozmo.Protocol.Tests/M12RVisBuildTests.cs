using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The R-VIS M12 build batch and its fix rounds (records M12-001, M12-005, M12-008, M12-011, M12-012, M12-017, M12-020, M12-022, M12-023, M12-024, M12-025, M12-026, M12-029, M12-030, M12-031, M12-033, M12-035, M12-036, M12-037). Every expected
/// value is taken from the record's citation (address in the test's summary), never from what the code returns: the numbers are
/// worked by hand from the cited constants and rows.
/// </summary>
public class M12RVisBuildTests
{
    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;
    private const double StackToleranceMm = 15.0;                     // 0x00632F0C
    private static Pose3d At(double x, double y, double z = 0, double yaw = 0) => new(Mat3.AboutZ(yaw), new Vec3(x, y, z));
    private static readonly Func<IReadOnlyList<PreActionObstacle>> NoObstacles = () => Array.Empty<PreActionObstacle>();

    // ------------------------------------------------------------------ M12-020

    /// <summary>
    /// M12-020, ComputePreActionPoseDistThreshold 0x00550098: the second pose in the first pose's frame
    /// (0x005500EE..0x005500F4) and the THREE-D norm (0x00550102..0x00550122); out0 = 2*d*sin(tol) (0x005501A4), out1 = d*sin(tol) (0x005501A8).
    /// The goal comes first and the object second (0x0055ACF0, 0x005561A0, 0x00550FF8).
    /// </summary>
    [Fact]
    public void M12_020_TheThresholdIsTheThreeDimensionalNormOfTheObjectInTheGoalsFrame()
    {
        var goal = At(0, 0, 0, 0.3);
        var obj = At(30, 40, 12);
        Assert.True(CubePreActionPoses.DistanceThresholdMm(goal, obj, 0.5, out double twice, out double once));
        // binary32 emulation of 0x00550102..0x00550164 (the z counts: 51.4198..); the relative translation of the identity-rotated goal at the origin is exact
        float d = MathF.Sqrt(30f * 30f + 40f * 40f + 12f * 12f);
        float eOnce = d * MathF.Sin(0.5f);
        // the goal is yawed 0.3, so the double relative components differ from (30, 40, 12) in the last place before they become float: 2 ulp
        Assert.True(Math.Abs(eOnce - (float)once) <= 2 * MathF.BitIncrement(eOnce) - 2 * eOnce);
        Assert.Equal((float)once + (float)once, (float)twice);
        // the second pose expressed in the first: a goal 10 mm along the world X, object at the goal's own origin
        Assert.True(CubePreActionPoses.DistanceThresholdMm(At(10, 0, 0), At(10, 0, 0), 0.5, out _, out double zero));
        Assert.Equal(0.0, zero, 12);
    }

    // ------------------------------------------------------------------ M12-022

    /// <summary>
    /// M12-022: the 7-arg ctor 0x0055850C stores the ActionType argument at +0x80 and -1.0 at +0x84 (0x005585A0, r1 = 0xBF800000); the 4-arg
    /// ctor 0x005587B8 stores 6 at +0x80 (0x0055883C) and the float at +0x84 (0x0055884C); +0x148 = 1 from both (inventory G3.9, row 11.1).
    /// </summary>
    [Fact]
    public void M12_022_TheSevenArgAndFourArgConstructorsDifferInActionTypeAndDistance()
    {
        using var rig = new Rig();
        var seven = new DriveToObjectAction(rig.M, 7, PreActionType.Rolling);
        Assert.Equal(PreActionType.Rolling, seven.ActionType);
        Assert.Equal(4, (int)seven.ActionType);
        Assert.Equal(-1.0f, seven.DistanceFromObjectOrigin);
        Assert.True(seven.CheckAtEnd);

        var four = new DriveToObjectAction(rig.M, 7, 123.5f, useManualSpeed: true);
        Assert.Equal(6, (int)four.ActionType);
        Assert.Equal(PreActionType.None, four.ActionType);
        Assert.Equal(123.5f, four.DistanceFromObjectOrigin);
        Assert.True(four.UseManualSpeed);
        Assert.True(four.CheckAtEnd);
    }

    /// <summary>
    /// M12-022, the RobotActionUnion GotoObject factory 0x0052A090: usePreDockPose ([msg+0x35], 0x0052A0BA) set builds the 7-arg ctor with
    /// ActionType 0 (0x0052A0C4..0x0052A0DA); clear builds the 4-arg ctor with distance [msg+0x30] and useManualSpeed [msg+0x34]
    /// (0x0052A0E0..0x0052A0EE).
    /// </summary>
    [Fact]
    public void M12_022_TheGotoObjectFactoryPicksTheConstructorByUsePreDockPose()
    {
        using var rig = new Rig();
        var pre = DriveToObjectAction.FromGotoObject(rig.M, 7, 120f, useManualSpeed: true, usePreDockPose: true);
        Assert.Equal(PreActionType.Docking, pre.ActionType);
        Assert.Equal(0, (int)pre.ActionType);
        Assert.Equal(-1.0f, pre.DistanceFromObjectOrigin);
        var raw = DriveToObjectAction.FromGotoObject(rig.M, 7, 120f, useManualSpeed: true, usePreDockPose: false);
        Assert.Equal(PreActionType.None, raw.ActionType);
        Assert.Equal(120f, raw.DistanceFromObjectOrigin);
        Assert.True(raw.UseManualSpeed);
    }

    /// <summary>M12-022: DriveToPlaceCarriedObjectAction ctor 0x00559CD4: r2 = 1, then 2 when r3 != 0 (0x00559CDE/0x00559CE4/0x00559CE8); its only caller passes 1 (0x00554B40/0x00554B42).</summary>
    [Fact]
    public void M12_022_DriveToPlaceCarriedObjectPassesActionTypeTwo()
    {
        Assert.Equal(2, (int)DriveToObjectAction.DriveToPlaceCarriedObjectActionType);
        Assert.Equal(PreActionType.PlaceOnGround, DriveToObjectAction.DriveToPlaceCarriedObjectActionType);
    }

    /// <summary>
    /// M12-022, the goal (0x00559200..0x00559278): delta = normalize(robot.xy - object.xy) * +0x84; goal = {obj.x + delta.x, obj.y + delta.y, robot.z};
    /// yaw = atan2f(-delta.y, -delta.x). Worked by hand: object (200, 100), robot (0, 0, 5), +0x84 = 100:
    /// delta = (-200, -100)/223.6068 * 100 = (-89.4427, -44.7214); goal = (110.5573, 55.2786, 5); yaw = atan2(44.7214, 89.4427) = 0.4636476.
    /// A robot closer to the object than +0x84 needs no goal (0x005591C2..0x005591D6).
    /// </summary>
    [Fact]
    public void M12_022_TheObjectDeltaGoalIsBuiltFromTheObjectTheRobotAndTheDistance()
    {
        Assert.True(DriveToObjectAction.TryBuildObjectDeltaGoal(At(200, 100, 22), At(0, 0, 5), 100f, out var goal));
        Assert.Equal(110.5573, goal.Translation.X, 3);
        Assert.Equal(55.2786, goal.Translation.Y, 3);
        Assert.Equal(5.0, goal.Translation.Z, 9);
        Assert.Equal(0.4636476, goal.AngleAroundZ, 6);
        // 223.6 < 300: the robot is inside the distance already
        Assert.False(DriveToObjectAction.TryBuildObjectDeltaGoal(At(200, 100, 22), At(0, 0, 5), 300f, out _));
        // exactly on the object: n2 = 0, d = 0, delta stays 0: the goal is the object's point, yaw atan2(-0, -0)
        Assert.True(DriveToObjectAction.TryBuildObjectDeltaGoal(At(50, 60, 22), At(50, 60, 0), 0f, out var onIt));
        Assert.Equal(50.0, onIt.Translation.X, 9);
        Assert.Equal(60.0, onIt.Translation.Y, 9);
    }

    /// <summary>M12-022 end to end: the type-6 drive sends a path to the object+delta goal, 150 mm short of a cube 300 mm ahead.</summary>
    [Fact]
    public async Task M12_022_TheTypeSixDriveGoesToThePointShortOfTheObject()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(300, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var drive = new DriveToObjectAction(rig.M, 7, 150f, useManualSpeed: false);
        var task = drive.RunAsync(default);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && sw.ElapsedMilliseconds < 60000) { rig.Pump(); Thread.Sleep(5); }
        await task;
        var lines = rig.Sent.OfType<AppendPathSegmentLine>().ToList();
        Assert.NotEmpty(lines);
        // the last line ends at obj.x - 150 (the goal is on the line from the object toward the robot)
        Assert.InRange(lines[^1].XEndMm, obj.Pose.Translation.X - 150 - 2, obj.Pose.Translation.X - 150 + 2);
        Assert.InRange(Math.Abs(lines[^1].YEndMm), 0, 3);
    }

    // ------------------------------------------------------------------ M12-011

    /// <summary>M12-011, CheckIfDone 0x00559880: +0x148 == 0 returns 0 before the object lookup (0x0055989C).</summary>
    [Fact]
    public void M12_011_ACheckAtEndOfZeroReturnsSuccessBeforeLookingForTheObject()
    {
        using var rig = new Rig();
        var drive = new DriveToObjectAction(rig.M, 99, PreActionType.Docking) { CheckAtEnd = false };
        Assert.Equal(ActionResult.Success, drive.CheckIfDone());
    }

    /// <summary>M12-011: an object that is not located is BadObject 0x03000004 (0x005598B6 + 0x005598BA).</summary>
    [Fact]
    public void M12_011_AMissingObjectIsBadObject()
    {
        using var rig = new Rig();
        var drive = new DriveToObjectAction(rig.M, 99, PreActionType.Docking);
        Assert.Equal(0x03000004u, (uint)drive.CheckIfDone());
    }

    /// <summary>
    /// M12-011, ActionType 6 (0x00559998..0x005599FE): the robot-to-object XY distance squared against +0x84 squared; beyond it the result is
    /// 0x04000001; within it 0. The cube is at (300, 0): a robot at x=160 is 140 away of 150, a robot at x=100 is 200 away.
    /// </summary>
    [Fact]
    public void M12_011_TypeSixComparesTheDistanceToTheObjectOriginAgainstPlus0x84()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(300, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var drive = new DriveToObjectAction(rig.M, 7, (float)150, useManualSpeed: false);
        rig.X = (float)(obj.Pose.Translation.X - 140); rig.Y = 0; rig.State();
        Assert.Equal(ActionResult.Success, drive.CheckIfDone());
        rig.X = (float)(obj.Pose.Translation.X - 200); rig.State();
        Assert.Equal(0x04000001u, (uint)drive.CheckIfDone());
    }

    /// <summary>
    /// M12-011, the production path 0x00559A86..0x00559AE6: every other ActionType calls the +0x150 function with a bool&amp; in-position flag
    /// (0x00559A9C); flag 0 gives 0x04000001 (0x00559AE4), otherwise the function's result is returned (0x00559AE6).
    /// </summary>
    [Fact]
    public void M12_011_TheProductionPathReadsTheInPositionFlagOfThePosesFunction()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(300, 0);
        Assert.Single(rig.Frame().Objects);
        var drive = new DriveToObjectAction(rig.M, 7, PreActionType.Docking);
        ActionResult fnResult = ActionResult.Success; bool flag = false; int calls = 0;
        drive.PosesFunction = (ObservableObject o, out IReadOnlyList<PreActionPose> poses, ref bool inPosition) =>
        { calls++; poses = Array.Empty<PreActionPose>(); inPosition = flag; return fnResult; };
        Assert.Equal(0x04000001u, (uint)drive.CheckIfDone());          // flag 0
        flag = true; fnResult = ActionResult.Retry;
        Assert.Equal(ActionResult.Retry, drive.CheckIfDone());         // flag set: the function's own result
        fnResult = ActionResult.Success;
        Assert.Equal(ActionResult.Success, drive.CheckIfDone());
        Assert.Equal(3, calls);
    }

    // ------------------------------------------------------------------ M12-023 / M12-024

    private sealed class FakeAnim : IDrivingAnimationHandler
    {
        public int StateValue; public int StateReads, StartCalls, EndCalls; public uint EndReturn;
        public int State { get { StateReads++; return StateValue; } }
        public void PlayStartAnim() => StartCalls++;
        public uint PlayEndAnim() { EndCalls++; return EndReturn; }
    }

    private static DriveToPoseTick Tick(int status, double now = 0, Pose3d? robot = null, double height = 70, ushort id42 = 1, ushort id44 = 1) =>
        new(status, now, robot ?? At(0, 0), height, id42, id44);

    private static (DriveToPoseAction Action, FakeAnim Anim, Func<int> Aborts) NewDrive(Rig rig, Pose3d goal)
    {
        var anim = new FakeAnim(); int aborts = 0;
        var d = new DriveToPoseAction(rig.M) { Goal = goal, DrivingAnimations = anim, AbortPath = () => aborts++ };
        return (d, anim, () => aborts);
    }

    /// <summary>M12-023, 0x0055AB5C..0x0055AB6A and 0x0055AB74: [[robot+0x24C]] == 3, or a path status above 4, returns RUNNING 0x01000000.</summary>
    [Fact]
    public void M12_023_TheEntryGatesReturnRunning()
    {
        using var rig = new Rig();
        var (d, anim, _) = NewDrive(rig, At(100, 0));
        anim.StateValue = 3;
        Assert.Equal(0x01000000u, (uint)d.CheckIfDone(Tick(4)));                 // even in the Ready state
        anim.StateValue = 0;
        foreach (int status in new[] { 5, 6, 7, 200 })
            Assert.Equal(0x01000000u, (uint)d.CheckIfDone(Tick(status)));
        Assert.Equal(0, anim.EndCalls);
    }

    /// <summary>M12-023, state 0 Failed (0x0055AB8E..0x0055ABDA): [+0xB0] = -1.0, result 0x03000013, no end animation.</summary>
    [Fact]
    public void M12_023_FailedIsPathPlanningFailedWithNoEndAnimation()
    {
        using var rig = new Rig();
        var (d, anim, _) = NewDrive(rig, At(100, 0));
        Assert.Equal(0x03000013u, (uint)d.CheckIfDone(Tick(0)));
        Assert.Equal(-1.0, d.DeadlineSec);
        Assert.Equal(0, anim.EndCalls);
    }

    /// <summary>
    /// M12-023, state 1 ComputingPath (0x0055ABDC..0x0055AD6A): an unset deadline becomes now + [+0xA8] (4.0, inventory G3.9) and returns RUNNING; before
    /// the deadline RUNNING; at or after it PathComponent::Abort (0x0055AD5C), [+0xB0] = -1.0 and 0x03000013 with no end animation.
    /// </summary>
    [Fact]
    public void M12_023_ComputingPathTimesOutAfterFourSecondsByAbortingThePath()
    {
        using var rig = new Rig();
        var (d, anim, aborts) = NewDrive(rig, At(100, 0));
        Assert.Equal(0x01000000u, (uint)d.CheckIfDone(Tick(1, now: 10.0)));
        Assert.Equal(14.0, d.DeadlineSec, 9);
        Assert.Equal(0x01000000u, (uint)d.CheckIfDone(Tick(1, now: 13.9)));
        Assert.Equal(0, aborts());
        Assert.Equal(0x03000013u, (uint)d.CheckIfDone(Tick(1, now: 14.0)));
        Assert.Equal(1, aborts());
        Assert.Equal(-1.0, d.DeadlineSec);
        Assert.Equal(0, anim.EndCalls);
    }

    /// <summary>M12-023: state 2 WaitingToBeginPath returns RUNNING (0x0055B0CE); state 3 FollowingPath calls PlayStartAnim (0x0055AC08), sets [+0xB0] = -1.0 and increments [+0xC4], RUNNING.</summary>
    [Fact]
    public void M12_023_WaitingReturnsRunningAndFollowingPlaysTheStartAnimation()
    {
        using var rig = new Rig();
        var (d, anim, _) = NewDrive(rig, At(100, 0));
        Assert.Equal(0x01000000u, (uint)d.CheckIfDone(Tick(2)));
        Assert.Equal(0, anim.StartCalls);
        Assert.Equal(0x01000000u, (uint)d.CheckIfDone(Tick(1, now: 1.0)));      // sets a deadline
        Assert.Equal(0x01000000u, (uint)d.CheckIfDone(Tick(3)));
        Assert.Equal(0x01000000u, (uint)d.CheckIfDone(Tick(3)));
        Assert.Equal(2, anim.StartCalls);
        Assert.Equal(2, d.FollowingPathTicks);
        Assert.Equal(-1.0, d.DeadlineSec);
    }

    /// <summary>
    /// M12-023, state 4 Ready (0x0055ACA2..0x0055B0A6) with [+0xC0] clear: the tolerance is {[+0x90], [+0x94] = 10.0, Robot::GetHeight}; arrival is IsSameAs
    /// on the full pose with Radians [+0x9C] = 0.174533; true gives 0; the tail (0x0055B0AA..0x0055B0CC) then calls PlayEndAnim and a non-zero return
    /// keeps RUNNING.
    /// </summary>
    [Fact]
    public void M12_023_ReadyAtTheGoalIsSuccessAndTheEndAnimationCanKeepItRunning()
    {
        using var rig = new Rig();
        var (d, anim, _) = NewDrive(rig, At(100, 0));
        Assert.Equal(0u, (uint)d.CheckIfDone(Tick(4, robot: At(105, -5))));          // inside (10, 10)
        Assert.Equal(1, anim.EndCalls);
        anim.EndReturn = 7;
        Assert.Equal(0x01000000u, (uint)d.CheckIfDone(Tick(4, robot: At(105, -5)))); // PlayEndAnim non-zero: RUNNING
        anim.EndReturn = 0;
        Assert.Equal(0x04000002u, (uint)d.CheckIfDone(Tick(4, robot: At(115, 0))));  // 15 mm out: outside 10
        Assert.Equal(0x04000002u, (uint)d.CheckIfDone(Tick(4, robot: At(100, 0, 0, 0.3))));   // rotation 0.3 > 0.174533: the full rotation counts
    }

    /// <summary>
    /// M12-023, not at the goal (0x0055AEC2..0x0055B0A6): equal path ids give 0x04000002 (string DoneNotInPlace, 0x0055B056); different ids keep the
    /// initial 0x03000008, which is neither RUNNING nor 0x03000013, so the end animation is still asked.
    /// </summary>
    [Fact]
    public void M12_023_NotAtTheGoalIsFailedTraversingOrTheInitialResultByThePathIds()
    {
        using var rig = new Rig();
        var (d, anim, _) = NewDrive(rig, At(100, 0));
        Assert.Equal(0x04000002u, (uint)d.CheckIfDone(Tick(4, robot: At(0, 0), id42: 5, id44: 5)));
        Assert.Equal(0x03000008u, (uint)d.CheckIfDone(Tick(4, robot: At(0, 0), id42: 5, id44: 4)));
        Assert.Equal(2, anim.EndCalls);
    }

    /// <summary>
    /// M12-023 with M12-020 caller 1, [+0xC0] set (0x0055ACC4): x and y are BOTH outputs of ComputePreActionPoseDistThreshold(out, goals[idx], this+0xB4, this+0x9C)
    /// (0x0055ACF0, 0x0055ACF4 ldrd), z stays Robot::GetHeight. Goal (100, 0, 0), object (200, 0, 22): d = sqrt(100^2 + 22^2) = 102.4109,
    /// out1 = d*sin(0.174533) = 17.7856, out0 = 35.5712. The robot 30 mm off in x is inside, 20 mm off in y is outside.
    /// </summary>
    [Fact]
    public void M12_023_TheThresholdPairReplacesTheXAndYTolerance()
    {
        using var rig = new Rig();
        var (d, _, _) = NewDrive(rig, At(100, 0));
        d.PreActionObjectPose = At(200, 0, 22);
        Assert.Equal(0u, (uint)d.CheckIfDone(Tick(4, robot: At(130, 0))));            // 30 <= 35.57
        Assert.Equal(0x04000002u, (uint)d.CheckIfDone(Tick(4, robot: At(100, 20))));  // 20 > 17.79
        Assert.Equal(0u, (uint)d.CheckIfDone(Tick(4, robot: At(100, 17))));           // 17 <= 17.79
        // the sentinel pair is used as it comes back: Radians <= 0 gives (-1, -1) and nothing is inside a tolerance of -1
        d.AngleToleranceRad = 0;
        Assert.Equal(0x04000002u, (uint)d.CheckIfDone(Tick(4, robot: At(100, 0))));
    }

    /// <summary>M12-023, GoalIndex: goals[idx] with stride 12 (0x0055ACCE..0x0055ACEA) is the goal tested.</summary>
    [Fact]
    public void M12_023_TheGoalTestedIsGoalsAtTheIndex()
    {
        using var rig = new Rig();
        var (d, _, _) = NewDrive(rig, At(100, 0));
        d.Goals = new[] { At(100, 0), At(500, 500) };
        d.GoalIndex = 1;
        Assert.Equal(0x04000002u, (uint)d.CheckIfDone(Tick(4, robot: At(100, 0))));
        Assert.Equal(0u, (uint)d.CheckIfDone(Tick(4, robot: At(500, 500))));
    }

    /// <summary>M12-024 (RECOVERABLE_GAP): the driving-animation handler is an explicit stub; the default throws instead of choosing an animation behaviour.</summary>
    [Fact]
    public void M12_024_TheDefaultDrivingAnimationHandlerIsAVisibleStub()
    {
        using var rig = new Rig();
        var d = new DriveToPoseAction(rig.M) { Goal = At(100, 0) };
        Assert.Throws<NotSupportedException>(() => d.CheckIfDone(Tick(4, robot: At(100, 0))));
        Assert.Throws<NotSupportedException>(() => UnreadDrivingAnimationHandler.Instance.PlayEndAnim());
        Assert.Throws<NotSupportedException>(() => UnreadDrivingAnimationHandler.Instance.PlayStartAnim());
    }

    /// <summary>M12-024: the wait-for-the-terminal-event flow says out loud that it plays no driving animations.</summary>
    [Fact]
    public async Task M12_024_TheLivePathTraceNamesTheMissingAnimations()
    {
        using var rig = new Rig();
        var drive = new DriveToPoseAction(rig.M) { Goal = At(150, -40, 0, 0.5) };
        var task = drive.RunAsync(default);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && sw.ElapsedMilliseconds < 60000) { rig.Pump(); Thread.Sleep(5); }
        Assert.Equal(ActionResult.Success, await task);
        Assert.Contains(drive.Trace, l => l.Contains("M12-024 RECOVERABLE_GAP"));
    }

    // ------------------------------------------------------------------ M12-025

    /// <summary>
    /// M12-025, ctor 0x00554BE0: A -> +0x108, B -> +0x10C, r3 -> +0xA8, ctor bool -> +0x110, +0xFC = -1, +0x100 = 0, +0xC0 = 0, +0xF0 = 0x208 or 0x209 if r3 != 0.
    /// </summary>
    [Fact]
    public void M12_025_TheConstructorStoresItsArguments()
    {
        using var rig = new Rig();
        var a = new PlaceRelObjectAction(rig.M, 7, flagAt0xA8: true, a: 12.5, b: -3.0, dockActionFlagAt0x95: true, skipOffsetTransform: false);
        Assert.Equal(12.5, a.OffsetA); Assert.Equal(-3.0, a.OffsetB);
        Assert.True(a.FlagAt0xA8); Assert.Equal(0x209, a.Field0xF0);
        Assert.False(a.SkipOffsetTransform);
        Assert.True(a.DockActionFlagAt0x95);
        Assert.Equal(-1, a.Field0xFC); Assert.Equal(0, a.Field0x100); Assert.Equal(0, a.Field0xC0);
        var b = new PlaceRelObjectAction(rig.M, 7, flagAt0xA8: false, a: 0, b: 0, dockActionFlagAt0x95: false, skipOffsetTransform: true);
        Assert.Equal(0x208, b.Field0xF0);
        Assert.True(b.SkipOffsetTransform);
    }

    /// <summary>
    /// M12-025, 0x00554C86..0x00554CDE (verifier A4): +0xB9 is cleared, with the log PlaceRelObjectAction.Constructor.WillNotCheckPreDockPoses, when |A| >= 9.99999975e-06
    /// OR |B| >= 9.99999975e-06; when both are below it the store is skipped.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0, true)]
    [InlineData(9.0e-06, -9.0e-06, true)]
    [InlineData(1.0e-05, 0.0, false)]
    [InlineData(0.0, -1.0e-05, false)]
    [InlineData(20.0, 20.0, false)]
    public void M12_025_TheConstructorStopsCheckingPreDockPosesForAnyNonTinyOffset(double a, double b, bool checksPreDockPoses)
    {
        using var rig = new Rig();
        var act = new PlaceRelObjectAction(rig.M, 7, flagAt0xA8: false, a: a, b: b, dockActionFlagAt0x95: false, skipOffsetTransform: true);
        Assert.Equal(checksPreDockPoses, act.CheckPreActionPose);
        Assert.Equal(!checksPreDockPoses, act.Trace.Contains("PlaceRelObjectAction.Constructor.WillNotCheckPreDockPoses"));
    }

    /// <summary>
    /// M12-025, the wire factory (0x0052A72E..0x0052A754), usePreDockPose false: PlaceRelObjectAction(robot, id, true, A = [msg+0x30], B = 0.0, [msg+0x3A], true),
    /// then 1 at +0xF7 and 0 at +0xB9. usePreDockPose true builds DriveToPlaceRelObjectAction, which this stack does not have: NotSupportedException.
    /// </summary>
    [Fact]
    public void M12_025_TheWireRoutes()
    {
        using var rig = new Rig();
        var act = PlaceRelObjectAction.FromWireMessage(rig.M, 7, usePreDockPose: false, placementOffsetXmm: 30.0, useManualSpeed: true);
        Assert.Equal(30.0, act.OffsetA); Assert.Equal(0.0, act.OffsetB);
        Assert.True(act.FlagAt0xA8); Assert.Equal(0x209, act.Field0xF0);
        Assert.True(act.DockActionFlagAt0x95);                      // [msg+0x3A]
        Assert.True(act.SkipOffsetTransform);                       // the ctor's last bool is true
        Assert.True(act.Field0xF7);
        Assert.False(act.CheckPreActionPose);                       // +0xB9 = 0
        Assert.Throws<NotSupportedException>(() => PlaceRelObjectAction.FromWireMessage(rig.M, 7, usePreDockPose: true, placementOffsetXmm: 30.0, useManualSpeed: false));
    }

    /// <summary>
    /// M12-025, InitInternal 0x00554DCC: with +0x110 set the transform is skipped (result 0) and +0x9C = A' (0.0 if A' &lt; -16.000009), +0xA0 = B'; +0xBB = 3 when |B'| &gt;= eps (B' is 5 here; the post-transform rule is M12_025_TheBBTestReadsThePostTransformB).
    /// </summary>
    [Fact]
    public void M12_025_InitInternalSkipsTheTransformAndStoresTheOffsets()
    {
        using var rig = new Rig();
        var act = new PlaceRelObjectAction(rig.M, 7, false, a: -20.0, b: 5.0, dockActionFlagAt0x95: false, skipOffsetTransform: true);
        Assert.Equal(ActionResult.Success, act.RunInitInternal());
        Assert.Equal(0.0, act.InitialisedPlacementOffset.X);             // -20 < -16.000009
        Assert.Equal(5.0, act.InitialisedPlacementOffset.Y);
        Assert.Equal(3, act.Field0xBB);
        var ok = new PlaceRelObjectAction(rig.M, 7, false, a: -16.0, b: 0.0, dockActionFlagAt0x95: false, skipOffsetTransform: true);
        ok.RunInitInternal();
        Assert.Equal(-16.0, ok.InitialisedPlacementOffset.X);            // -16.0 is not below -16.000009
        Assert.Equal(0, ok.Field0xBB);
    }

    private static ObjectPoseQueries CentreRotatedBy(double psi) =>
        new((o, z) => new Pose3d(Mat3.AboutZ(psi), o.Pose.Translation), (_, _) => MarkerType.LightCubeI_Front);

    /// <summary>
    /// M12-025, TransformPlacementOffsetsRelativeObject 0x00554E40 (verified by the verifier): psi (the z-rotated point above the centre, 0.5, with respect to
    /// the robot) within 0.2617994 of 0: (A', B') = (-A, B); of +pi/2: (B, A); of -pi/2: (-B, -A); of +-pi: (A, -B); none: 0x04000001; A' &lt; -16.000009: 0x03000000.
    /// A = 10, B = 5. The queries are inputs: their engine bodies are MISSING.
    /// </summary>
    [Theory]
    [InlineData(0.0, -10.0, 5.0)]
    [InlineData(0.2, -10.0, 5.0)]
    [InlineData(Math.PI / 2, 5.0, 10.0)]
    [InlineData(-Math.PI / 2 + 0.25, -5.0, -10.0)]
    [InlineData(Math.PI, 10.0, -5.0)]
    [InlineData(-Math.PI + 0.1, 10.0, -5.0)]
    public void M12_025_TheTransformMapsTheOffsetsByTheBlocksAlignment(double psi, double expectA, double expectB)
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        var act = new PlaceRelObjectAction(rig.M, 7, false, a: 10.0, b: 5.0, dockActionFlagAt0x95: false, skipOffsetTransform: false) { Queries = CentreRotatedBy(psi) };
        Assert.Equal(ActionResult.Success, act.TransformPlacementOffsetsRelativeObject());
        Assert.Equal(expectA, act.OffsetA, 9);
        Assert.Equal(expectB, act.OffsetB, 9);
    }

    [Fact]
    public void M12_025_TheTransformFailuresHaveTheirOwnResults()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        var none = new PlaceRelObjectAction(rig.M, 7, false, 10.0, 5.0, false, false) { Queries = CentreRotatedBy(0.4) };     // 0.4 rad is outside every 0.2617994 window
        Assert.Equal(0x04000001u, (uint)none.TransformPlacementOffsetsRelativeObject());
        var tooFar = new PlaceRelObjectAction(rig.M, 7, false, 17.0, 5.0, false, false) { Queries = CentreRotatedBy(0.0) };   // A' = -17 < -16.000009
        Assert.Equal(0x03000000u, (uint)tooFar.TransformPlacementOffsetsRelativeObject());
        Assert.Equal(17.0, tooFar.OffsetA);                                                                                   // nothing is stored
        var missing = new PlaceRelObjectAction(rig.M, 99, false, 10.0, 5.0, false, false) { Queries = CentreRotatedBy(0.0) };
        Assert.Equal(0x03000004u, (uint)missing.TransformPlacementOffsetsRelativeObject());
    }

    // ------------------------------------------------------------------ M12-026

    private static ObservableObject NewCube(Pose3d pose) =>
        new(7, ObjectType.Block_LIGHTCUBE1, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1)) { Pose = pose, PoseState = PoseState.Known };

    /// <summary>Facing-object element poses in the object-centre frame: +x side, -x side, -y side (yaw pi/2) and +y side (yaw -pi/2).</summary>
    private static Pose3d E1 => At(97, 0, 0, Math.PI);
    private static Pose3d E2 => At(-97, 0, 0, 0);
    private static Pose3d E3 => At(0, -97, 0, Math.PI / 2);
    private static Pose3d E4 => At(0, 97, 0, -Math.PI / 2);

    private static readonly ObjectPoseQueries IdentityCentre = new((_, _) => Pose3d.Identity, (_, _) => MarkerType.LightCubeI_Front);

    /// <summary>
    /// M12-026 rows 12.6-12.8 with A = B = 0: |A| and |B| are within 1.0, so nothing is erased on the axis tests; the facing axis (12.11) picks A or B; with
    /// zero offsets the shift is zero and the pose is unchanged (12.15). alreadyInPosition (12.16/12.17) is set when the robot is within {out0, out1, 100.0}
    /// of a kept element by Radians 0x3E060A92.
    /// </summary>
    [Fact]
    public void M12_026_ZeroOffsetsKeepEveryPoseAndFlagTheRobotAtOne()
    {
        var cal = CameraCalibration.Nominal();
        var obj = NewCube(At(0, 0, 22));
        var status = PlaceRelObjectOffsetPoses.Filter(obj, 0, 0, E1, cal, IdentityCentre, new[] { E1, E2, E3, E4 }, out var poses, out bool inPos);
        Assert.Equal(ActionResult.Success, status);
        Assert.Equal(4, poses.Count);
        for (int i = 0; i < 4; i++) Assert.True((poses[i].Translation - new[] { E1, E2, E3, E4 }[i].Translation).Length < 1e-9);
        Assert.True(inPos);                                         // the robot stands on E1
        PlaceRelObjectOffsetPoses.Filter(obj, 0, 0, At(0, 0, 0, 1.0), cal, IdentityCentre, new[] { E1 }, out _, out bool far);
        Assert.False(far);
    }

    /// <summary>
    /// M12-026, row 12.10 size condition and the (0,1) case: B = 30, A = 0 over [E1, E4]. E1 (y = 0): r7 = 0, r8 = 0: kept. E4 (y = +97): B and y agree, r7 = 1, r8 = 0,
    /// |x| = 0 &lt;= 1 but only two poses remain, so it is kept ("size >= 3"). With a third pose it is erased.
    /// </summary>
    [Fact]
    public void M12_026_TheAlignedPoseIsKeptWhenFewerThanThreePosesRemain()
    {
        var cal = CameraCalibration.Nominal();
        var obj = NewCube(At(0, 0, 22));
        PlaceRelObjectOffsetPoses.Filter(obj, 0, 30, At(1000, 1000), cal, IdentityCentre, new[] { E1, E4 }, out var two, out _);
        Assert.Equal(2, two.Count);
        PlaceRelObjectOffsetPoses.Filter(obj, 0, 30, At(1000, 1000), cal, IdentityCentre, new[] { E1, E2, E4 }, out var three, out _);
        Assert.Equal(2, three.Count);                               // E4 erased (r7 only, |x| <= 1, size 3); E1 and E2 kept
        Assert.DoesNotContain(three, p => Math.Abs(p.Translation.Y) > 90);
        // and a sign mismatch on B erases with no size condition (12.9): E3 (y = -97) against B = +30
        PlaceRelObjectOffsetPoses.Filter(obj, 0, 30, At(1000, 1000), cal, IdentityCentre, new[] { E3 }, out var one, out _);
        Assert.Empty(one);
    }

    /// <summary>
    /// M12-026, rows 12.12/12.13: a closest-marker query that fails or a marker count other than one erases the pose with no size condition; an empty result is 0x03000010 (12.18).
    /// </summary>
    [Fact]
    public void M12_026_AFailedMarkerQueryErasesThePose()
    {
        var cal = CameraCalibration.Nominal();
        var obj = NewCube(At(0, 0, 22));
        var noMarker = new ObjectPoseQueries((_, _) => Pose3d.Identity, (_, _) => null);
        Assert.Equal(0x03000010u, (uint)PlaceRelObjectOffsetPoses.Filter(obj, 0, 0, E1, cal, noMarker, new[] { E1, E2 }, out var p1, out _));
        Assert.Empty(p1);
        var wrongCode = new ObjectPoseQueries((_, _) => Pose3d.Identity, (_, _) => MarkerType.LightCubeJ_Front);   // not on cube 1: zero markers
        Assert.Equal(0x03000010u, (uint)PlaceRelObjectOffsetPoses.Filter(obj, 0, 0, E1, cal, wrongCode, new[] { E1 }, out _, out _));
        // and no poses at all
        Assert.Equal(0x03000010u, (uint)PlaceRelObjectOffsetPoses.Filter(obj, 0, 0, E1, cal, IdentityCentre, Array.Empty<Pose3d>(), out _, out _));
    }

    /// <summary>M12-026, IsAtPreActionPoseWithVisualVerification 0x005B6F5C: missing object 0x03000004; [obj+0x1C] + 1000.0 &lt; now gives 0x0300001D (0x005B7022).</summary>
    [Fact]
    public void M12_026_IsAtPreActionPoseWithVisualVerificationResults()
    {
        var cal = CameraCalibration.Nominal();
        var obj = NewCube(At(0, 0, 22));
        obj.LastObservedTimestamp = 5000;
        Assert.Equal(0x03000004u, (uint)PlaceRelObjectOffsetPoses.IsAtPreActionPoseWithVisualVerification(null, PreActionType.PlaceRelative, 0, 0, 5000, Pose3d.Identity, null, cal, IdentityCentre, NoObstacles));
        Assert.Equal(0x0300001Du, (uint)PlaceRelObjectOffsetPoses.IsAtPreActionPoseWithVisualVerification(obj, PreActionType.PlaceRelative, 0, 0, 6001, Pose3d.Identity, null, cal, IdentityCentre, NoObstacles));
        Assert.Equal(0x0300001Du, (uint)PlaceRelObjectOffsetPoses.IsAtPreActionPoseWithVisualVerification(obj, PreActionType.Docking, 0, 0, 6001, Pose3d.Identity, null, cal, IdentityCentre, NoObstacles));
    }

    /// <summary>
    /// M12-031, IsAtPreActionPoseWithVisualVerification 0x005B6F5C for ActionType != 1 (0x005B7060..0x005B70D2): GetPreActionPoses with {obj, type, flag A 0, tol 0x3E060A92 (7.5 deg), distance 0,
    /// no approach angle}; a non-zero result is returned; otherwise the result is (in-position byte == 0 ? 0x04000001 : 0), with flag A clear a too-far robot is still the byte 0.
    /// Hand-worked (cube (200, 0, 22) yaw 0, Front docking pose, Docking stored translation (-87, 0, -22), |t| = 89.7385, a = 75): robot (60, 0): dist2 = |60 - 113| = 53, b = 53, pose
    /// x = 200 - 0.9695*(89.7385 + 53) = 61.61, d to the object 142.74, thresholds (37.3, 18.6): |dx| 1.6 inside, yaw 0: in position, 0. Robot (-20, 0): b = 75, pose x = 40.29,
    /// d = 164.74, t0 = 42.9 &lt; |dx| 60.29: the byte is 0, so 0x04000001. A carried object is the non-zero 0x03000004 (M12-031 10.1) and no pose left is 0x03000010.
    /// </summary>
    [Fact]
    public void M12_031_IsAtPreActionPoseWithVisualVerificationMapsTheInPositionByteForNonPlaceTypes()
    {
        var cal = CameraCalibration.Nominal();
        var obj = NewCube(At(200, 0, 22));
        obj.LastObservedTimestamp = 5000;
        ActionResult Run(Pose3d robot, PreActionType type, uint? carried = null) =>
            PlaceRelObjectOffsetPoses.IsAtPreActionPoseWithVisualVerification(obj, type, 0, 0, 5000, robot, carried, cal, IdentityCentre, NoObstacles);
        Assert.Equal(0u, (uint)Run(At(60, 0), PreActionType.Docking));
        Assert.Equal(0x04000001u, (uint)Run(At(-20, 0), PreActionType.Docking));
        Assert.Equal(0x03000004u, (uint)Run(At(60, 0), PreActionType.Docking, carried: 7u));
        Assert.Equal(0x03000010u, (uint)Run(At(60, 0), PreActionType.Entry));                // the Entry type generates nothing (M12-001)
    }

    // ------------------------------------------------------------------ M12-008

    /// <summary>M12-008 step 6 (0x00632E3E..0x00632E9E): x = L + 4.0 with L the THREE-D length of the marker translation, y = 0, z = -12.5; the rotation is kept.</summary>
    [Fact]
    public void M12_008_TheObjectOnLiftUsesTheThreeDimensionalMarkerNormAndKeepsTheRotation()
    {
        var marker = new KnownMarker(MarkerType.LightCubeI_Front, BlockFace.Front, new Pose3d(Mat3.Identity, new Vec3(3, 4, 12)), 25);
        var rot = Mat3.AboutZ(0.6);
        var p = LiftGeometry.ObjectOnLift(marker, rot);
        Assert.Equal(13.0 + 4.0, p.Translation.X, 9);               // sqrt(9 + 16 + 144) = 13
        Assert.Equal(0.0, p.Translation.Y, 9);
        Assert.Equal(-12.5, p.Translation.Z, 9);
        Assert.Equal(0.6, p.AngleAroundZ, 9);
    }

    private static uint Attach(Rig rig, uint? id, MarkerType code) => rig.M.Docking.SetObjectAsAttachedToLift(id, code);

    /// <summary>M12-008 steps 1-4: no id, already carrying, a missing object and a marker the object does not have each return 1 without changing what is carried.</summary>
    [Fact]
    public void M12_008_TheEarlyStepsReturnOneAndAttachNothing()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        Assert.Equal(1u, Attach(rig, null, MarkerType.LightCubeI_Front));                 // step 1
        Assert.Equal(1u, Attach(rig, 99, MarkerType.LightCubeI_Front));                   // step 3
        Assert.Equal(1u, Attach(rig, 7, MarkerType.LightCubeJ_Front));                    // step 4: cube 1 has no such marker
        Assert.False(rig.M.Docking.Carrying.IsCarryingObject);
        rig.M.Docking.Carrying.SetCarrying(9);
        Assert.Equal(1u, Attach(rig, 7, MarkerType.LightCubeI_Front));                    // step 2
        Assert.True(rig.M.Docking.Carrying.IsCarrying(9));
    }

    /// <summary>
    /// M12-008 steps 5, 6 and 9: the cube (yaw 0.3) is put at (L + 4, 0, -12.5) = (26, 0, -12.5) in the lift pose's frame with its rotation kept, carried, Known
    /// (AddLiftRelativeObservation 0x00506E5C, PoseState 1) with confirmation count 1 (0x00506ECC), and the BlockConfigurationManager dirty flag is set
    /// (0x006331B2..0x006331BC). The lift pose is the pivot (-41, 0, 45) plus the 66 mm arm swung by the lift angle (0x0050FFE0, 0x00510038).
    /// </summary>
    [Fact]
    public void M12_008_ThePickedUpObjectHangsAtThePointBelowThePlateWithItsRotationKept()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0, 0.3);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        double yawBefore = obj.Pose.AngleAroundZ;
        var st = rig.Vision.History.Latest!.Value;
        rig.M.Configurations.ForceUpdate = false;
        Assert.Equal(0u, Attach(rig, 7, MarkerType.LightCubeI_Front));
        Assert.True(rig.M.Docking.Carrying.IsCarrying(7));
        Assert.Equal(PoseState.Known, obj.PoseState);
        Assert.Equal(1, obj.PoseConfirmationCount);
        Assert.True(rig.M.Configurations.ForceUpdate);
        double a = st.LiftAngleRad;
        var lift = st.RobotPose.Compose(new Pose3d(Mat3.Identity, new Vec3(-41 + 66 * Math.Cos(a), 0, 45 + 66 * Math.Sin(a))));
        var expected = lift.Apply(new Vec3(22.0 + 4.0, 0, -12.5));                  // L = 22 (half a 44 mm cube), +4.0, z -12.5
        Assert.Equal(expected.X, obj.Pose.Translation.X, 4);
        Assert.Equal(expected.Y, obj.Pose.Translation.Y, 4);
        Assert.Equal(expected.Z, obj.Pose.Translation.Z, 4);
        Assert.Equal(yawBefore, obj.Pose.AngleAroundZ, 6);
        Assert.True(ObjectPoseConfirmerRelative.UnreadBroadcasts > 0);              // M12-027: the broadcast is counted, not performed
        Assert.Null(rig.M.Docking.Carrying.CarriedOnTopId);                         // step 8: nothing on top, this+0x14 = -1
    }

    /// <summary>
    /// M12-008 step 8: a cube resting on the carried one (found with tolerance 15.0, onTop) is given AddObjectRelativeObservation (PoseState 2 Dirty, 0x00506D30..0x00506D3E),
    /// this+0x14 is its id, and it is parented to the carried object (SetParent 0x00633006..0x00633010), so it moves with it.
    /// </summary>
    [Fact]
    public void M12_008_ACubeOnTopIsRecordedDirtyAndFollowsTheCarriedOne()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Head = 0.05f;
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(260, 0, 22));
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.Identity, new Vec3(260, 0, 66))));
        rig.Frame(); rig.Frame();
        var bottom = rig.Vision.World.GetLocatedObjectById(7);
        var top = rig.Vision.World.GetLocatedObjectById(8);
        Assert.True(bottom is not null && top is not null, "located: " + string.Join(", ", rig.Vision.World.LocatedObjects.Select(o => o.ToString())));
        var relBefore = top!.Pose.WithRespectTo(bottom!.Pose).Translation;          // as observed, before the attach
        Assert.Equal(0u, Attach(rig, 7, MarkerType.LightCubeI_Front));
        Assert.Equal(8u, rig.M.Docking.Carrying.CarriedOnTopId);
        Assert.Equal(PoseState.Dirty, top.PoseState);
        // the pose tree moves the object on top with the carried one: it keeps its offset from it
        rig.M.Docking.UpdateCarriedObjectPose();
        var offset = top.Pose.WithRespectTo(bottom.Pose).Translation;
        Assert.Equal(relBefore.X, offset.X, 6); Assert.Equal(relBefore.Y, offset.Y, 6); Assert.Equal(relBefore.Z, offset.Z, 6);
        Assert.InRange(relBefore.Z, 44.0 - StackToleranceMm, 44.0 + StackToleranceMm);       // found by the 15.0 helper: one cube height up
        double bottomXBefore = bottom.Pose.Translation.X, topXBefore = top.Pose.Translation.X;
        rig.X = 40; rig.State();
        rig.M.Docking.UpdateCarriedObjectPose();
        Assert.Equal(bottomXBefore + 40, bottom.Pose.Translation.X, 3);            // the robot moved 40 mm: so did the carried cube
        Assert.Equal(topXBefore + 40, top.Pose.Translation.X, 3);                  // and the cube on top
    }

    // ================================================================== R-VIS M12 fix round: records M12-005, 008, 012, 017, 022, 025, 026, 029, 030, 031, 033
    // Every expected value below is worked by hand from the cited instruction or record text, never from what the code returned.

    // Load-tolerant harness bound: the engine-faithful ActionList path needs more pump iterations than the old
    // host-Task path, and this guard is not a source oracle.
    private static void Spin(Task t, Rig rig, Action? each = null, int ms = 60000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!t.IsCompleted && sw.ElapsedMilliseconds < ms) { rig.Pump(); each?.Invoke(); Thread.Sleep(5); }
    }

    private static PreActionPose FakePose(Pose3d pose) =>
        new(PreActionType.Docking, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1)[0], pose, 75.0);

    // ------------------------------------------------------------------ M12-033

    /// <summary>
    /// M12-033, Radians::rescale 0x0084C87C (research Q3.1): the range is (-pi, pi]; Radians(4.712389) "becomes about -1.570796"; -pi (the float 0xC0490FDB) becomes
    /// +pi; NaN passes through.
    /// </summary>
    [Fact]
    public void M12_033_RescaleKeepsAngleInMinusPiToPi()
    {
        Assert.Equal(-1.570796, EngineRadians.Rescale(4.712389), 5);
        Assert.Equal((double)BitConverter.UInt32BitsToSingle(0x40490FDB), EngineRadians.Rescale(BitConverter.UInt32BitsToSingle(0xC0490FDB)), 12);
        Assert.Equal(0.5, EngineRadians.Rescale(0.5));
        Assert.Equal(0.5, EngineRadians.Rescale(0.5 + 20 * Math.PI), 4);        // |v| >= 10: v - ceil(v/2pi - 0.5) * 2pi with the FLOAT 2pi, so 0.4999983, not 0.5 to 6 places
        Assert.True(double.IsNaN(EngineRadians.Rescale(double.NaN)));
    }

    /// <summary>
    /// M12-033, Radians::IsNear 0x0084CC0A (research Q3.2): <c>|rescale(this - other)| &lt; |tol|</c>, STRICT (0x0084CC4E vcmpe, 0x0084CC56 movmi), wrap-aware, the
    /// tolerance's sign ignored, NaN false.
    /// </summary>
    [Fact]
    public void M12_033_IsNearIsStrictWrapAwareAndIgnoresTheToleranceSign()
    {
        Assert.False(EngineRadians.IsNear(0.1, 0.0, 0.1));                      // equal to the tolerance: not near
        Assert.True(EngineRadians.IsNear(0.0999, 0.0, 0.1));
        Assert.True(EngineRadians.IsNear(3.14, -3.14, 0.01));                  // the difference 6.28 wraps to -0.0032
        Assert.True(EngineRadians.IsNear(0.0, 0.05, -0.1));                    // tol = -0.1 is |tol|
        Assert.False(EngineRadians.IsNear(double.NaN, 0.0, 0.1));
    }

    /// <summary>
    /// M12-033, Rotation3d::GetAngleAroundZaxis 0x0084AA1C (research Q3.4): <c>atan2f(R10, R00)</c> when R10^2 + R00^2 &gt; R01^2 + R11^2, else
    /// <c>atan2f(-R01, R11)</c>. R00 = 0.1, R10 = 0.2 (sum 0.05), R01 = -0.6, R11 = 0.8 (sum 1.0): the second branch, atan2(0.6, 0.8) = 0.6435011, not
    /// atan2(0.2, 0.1) = 1.1071487.
    /// </summary>
    [Fact]
    public void M12_033_GetAngleAroundZaxisPicksTheBetterConditionedPair()
    {
        var m = new Mat3(0.1, -0.6, 0, 0.2, 0.8, 0, 0, 0, 1);
        Assert.Equal(0.6435011, EngineRadians.GetAngleAroundZaxis(m), 6);
        // R00 = 0.8, R10 = 0.1 (sum 0.65) against R01 = -0.2, R11 = 0.5 (sum 0.29): the first branch, atan2(0.1, 0.8) = 0.1243550
        Assert.Equal(0.1243550, EngineRadians.GetAngleAroundZaxis(new Mat3(0.8, -0.2, 0, 0.1, 0.5, 0, 0, 0, 1)), 6);
    }

    /// <summary>
    /// M12-033, GetZRotatedPointAboveObjectCenter(f) 0x00877574 (research Q1.2-Q1.4): T = (this.pose.x, this.pose.y, L.z + dimZ * f), yaw = GetAngleAroundZaxis(L),
    /// result Pose3d(yaw, Z_AXIS, T); f = 0 is the centre height, 0.5 the top face; roll and pitch are dropped. A cube at (100, 50, 22) yawed 0.3 and pitched 0.1
    /// (44 mm on every axis): f = 0.5 gives z = 22 + 44 * 0.5 = 44, f = 0 gives 22; yaw 0.3, x, y unchanged, rotation flat.
    /// </summary>
    [Fact]
    public void M12_033_TheZRotatedPointKeepsXyAndYawAndAddsTheExtentTimesF()
    {
        var obj = new ObservableObject(7, ObjectType.Block_LIGHTCUBE1, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1))
        { Pose = new Pose3d(Mat3.AboutZ(0.3) * Mat3.AboutY(0.1), new Vec3(100, 50, 22)), PoseState = PoseState.Known };
        var top = ObjectPoseQueries.GetZRotatedPointAboveObjectCenter(obj, 0.5);
        Assert.Equal(100.0, top.Translation.X, 9); Assert.Equal(50.0, top.Translation.Y, 9); Assert.Equal(44.0, top.Translation.Z, 9);
        Assert.Equal(0.3, top.AngleAroundZ, 6);
        Assert.Equal(1.0, top.Rotation[2, 2], 9);                                // only yaw is kept
        Assert.Equal(22.0, ObjectPoseQueries.GetZRotatedPointAboveObjectCenter(obj, 0.0).Translation.Z, 9);
    }

    /// <summary>
    /// M12-033, GetClosestMarkerPose 0x00877774 (research Q2.2, Q2.4): the strictly smaller distance wins from FLT_MAX; planar is x^2 + y^2, else 3-D; the code is the
    /// marker's; an unusable pose (NaN, every distance NaN) gives no marker (0x06000000). A cube at the origin (centre z = 22) and a query pose at (0, 0, 100):
    /// planar, the top face (0, 0, 44) and the bottom face (0, 0, 0) both have d^2 = 0 and the side faces 22^2 = 484, so the FIRST of the two in the marker list wins
    /// (strictly smaller); 3-D, the top face is 56 away, the bottom 100 and the sides sqrt(22^2 + 78^2) = 81, so the top face wins.
    /// </summary>
    [Fact]
    public void M12_033_TheClosestMarkerIsPlanarOrThreeDimensionalAndStrictlySmaller()
    {
        var obj = NewCube(At(0, 0, 22));
        var pose = At(0, 0, 100);
        var planar = ObjectPoseQueries.GetClosestMarkerPose(obj, pose, planar: true, out _);
        var topBottom = obj.Markers.Where(m => m.Face is BlockFace.Top or BlockFace.Bottom).Select(m => m.Code).ToList();
        Assert.Equal(topBottom[0], planar);
        var threeD = ObjectPoseQueries.GetClosestMarkerPose(obj, pose, planar: false, out var wrt);
        Assert.Equal(obj.Markers.First(m => m.Face == BlockFace.Top).Code, threeD);
        Assert.Equal(56.0, wrt.Translation.Length, 6);
        Assert.Null(ObjectPoseQueries.GetClosestMarkerPose(obj, At(double.NaN, 0, 0), planar: true, out _));
    }

    // ------------------------------------------------------------------ M12-031 / M12-029

    /// <summary>
    /// M12-031, GetPreActionPoses 0x005508C8 (research 10.1): a null object, and an object the robot is carrying, are 0x03000004 (0x005508F0..0x0055096A).
    /// </summary>
    [Fact]
    public void M12_031_ANullOrCarriedObjectIsBadObject()
    {
        var obj = NewCube(At(200, 0, 22));
        Assert.Equal(0x03000004u, (uint)DockPreActionPoses.Get(new PreActionPoseInput(null, PreActionType.Docking, false, 0.13, 0, false, 0), At(0, 0), null, NoObstacles).Result);
        Assert.Equal(0x03000004u, (uint)DockPreActionPoses.Get(new PreActionPoseInput(obj, PreActionType.Docking, false, 0.13, 0, false, 0), At(0, 0), 7u, NoObstacles).Result);
        Assert.Equal(ActionResult.Success, DockPreActionPoses.Get(new PreActionPoseInput(obj, PreActionType.Docking, false, 0.13, 0, false, 0), At(0, 0), 8u, NoObstacles).Result);
    }

    /// <summary>M12-031 (research 10.5): no pose left is 0x03000010; the Entry type (3) generates none (M12-001).</summary>
    [Fact]
    public void M12_031_NoPosesIsNoPreActionPoses()
    {
        var obj = NewCube(At(200, 0, 22));
        Assert.Equal(0x03000010u, (uint)DockPreActionPoses.Get(new PreActionPoseInput(obj, PreActionType.Entry, false, 0.13, 0, false, 0), At(0, 0), null, NoObstacles).Result);
    }

    /// <summary>
    /// M12-031 approach-angle filter (research 10.4, literal 0x00550DC4 = 0x3F490F33 = 0.785388): only with useApproachAngle, a pose whose wrap-aware
    /// |yaw - approachAngle| is &gt;= 0.785388 is erased. The Front pose's own yaw as the angle keeps it (difference 0); 0.78 away keeps it; 0.79 away erases it.
    /// </summary>
    [Fact]
    public void M12_031_TheApproachAngleFilterErasesAtOrBeyondPointSevenEightFive()
    {
        var obj = NewCube(At(200, 0, 22));
        var robot = At(0, 0);
        var all = DockPreActionPoses.Get(new PreActionPoseInput(obj, PreActionType.Docking, false, 0.13, 0, false, 0), robot, null, NoObstacles).Poses;
        var front = all.First(p => p.Marker.Face == BlockFace.Front);
        double yaw = EngineRadians.GetAngleAroundZaxis(front.WorldPose.Rotation);
        bool Kept(double approach) => DockPreActionPoses.Get(new PreActionPoseInput(obj, PreActionType.Docking, false, 0.13, 0, true, approach), robot, null, NoObstacles)
                                       .Poses.Any(p => p.Marker.Face == BlockFace.Front);
        Assert.True(Kept(yaw));
        Assert.True(Kept(yaw + 0.78));
        Assert.False(Kept(yaw + 0.79));
        Assert.True(Kept(yaw - 0.78));
        Assert.False(Kept(yaw - 0.79));
        Assert.Equal(all.Count, DockPreActionPoses.Get(new PreActionPoseInput(obj, PreActionType.Docking, false, 0.13, 0, false, yaw + 3.0), robot, null, NoObstacles).Poses.Count);   // no filter without the flag
    }

    /// <summary>
    /// M12-031 closest pose (research 10.6): PLANAR strictly-smaller squared distance from FLT_MAX (0x00550DC8), so two poses at the same x, y keep the first even when
    /// the second is nearer in z. Poses at (100, 0, -18) and (100, 0, 4); the robot at (0, 0, 0): the second is nearer in 3-D (4 against 18), the first wins.
    /// </summary>
    [Fact]
    public void M12_031_TheClosestPoseIsPlanarAndTheFirstWinsATie()
    {
        var output = new PreActionPoseOutput();
        output.Poses.Add(FakePose(At(100, 0, -18))); output.Poses.Add(FakePose(At(100, 0, 4))); output.Poses.Add(FakePose(At(300, 0, 0)));
        DockPreActionPoses.Finish(output, At(200, 0, 22), At(0, 0, 0), 0.13, flagA: false);
        Assert.Equal(0, output.ClosestIndex);
        Assert.Equal(100.0, output.OffsetX, 6); Assert.Equal(0.0, output.OffsetY, 6);
    }

    /// <summary>
    /// M12-031 threshold and in-position (research 10.8-10.10; literal 0x005513B0 = -9.99999975e-06; M12-020 formula). Pose at (0, 0), object at (100, 0, 0), tolerance 0.1:
    /// d = 100, t1 = 100 sin 0.1 = 9.98334, t0 = 19.96668. Robot (15, 0) yaw 0: |dx| 15 &lt;= t0 and |dy| 0 &lt;= t1 and |yaw| 0 &lt; 0.1 - 1e-5: in position, result 0.
    /// Robot (25, 0): outside the box: flag A clear gives 0 with the byte 0, flag A set 0x04000001 (the "too far" log). Robot (15, 0) at yaw exactly 0.1: the angle test
    /// (0.1 &lt; 0.1 - 1e-5) fails, byte 0, result 0 -- a failed angle test never gives 0x04000001. Tolerance 0: the guard writes the -1.0 pair, result 0, byte 0.
    /// </summary>
    [Fact]
    public void M12_031_TheInPositionByteAndTheTooFarResult()
    {
        var obj = At(100, 0, 0);
        PreActionPoseOutput Run(Pose3d robot, double tol, bool flagA, out ActionResult r)
        {
            var o = new PreActionPoseOutput(); o.Poses.Add(FakePose(At(0, 0, 0)));
            r = DockPreActionPoses.Finish(o, obj, robot, tol, flagA).Result;
            return o;
        }
        var near = Run(At(15, 0), 0.1, false, out var r1);
        Assert.Equal(0u, (uint)r1); Assert.True(near.InPosition);
        Assert.Equal(19.96668, near.ThresholdX, 4); Assert.Equal(9.98334, near.ThresholdY, 4);
        var far = Run(At(25, 0), 0.1, false, out var r2);
        Assert.Equal(0u, (uint)r2); Assert.False(far.InPosition);
        Run(At(25, 0), 0.1, true, out var r3);
        Assert.Equal(0x04000001u, (uint)r3);
        var badAngle = Run(At(15, 0, 0, 0.1), 0.1, true, out var r4);
        Assert.Equal(0u, (uint)r4); Assert.False(badAngle.InPosition);
        var noTol = Run(At(15, 0), 0.0, true, out var r5);
        Assert.Equal(0u, (uint)r5); Assert.False(noTol.InPosition); Assert.Equal(-1.0, noTol.ThresholdX);
    }

    /// <summary>
    /// M12-029, GetPossiblePoses 0x00558C80 (research 9.3): a non-zero GetPreActionPoses result is returned (a carried object: 0x03000004); every pose is pushed in order
    /// (not just the closest); the in-position flag is the byte at out+0x1C. Cube (200, 0), robot (60, 0) facing +x: the Front docking pose is within the threshold pair
    /// (d ~ 160, so 2 d sin 7.5 deg = 42 and d sin 7.5 deg = 21 mm), in position; the robot at (-20, 0) is 60 mm from it in x, outside 42: not in position.
    /// </summary>
    [Fact]
    public void M12_029_TheDefaultFunctionReturnsEveryPoseAndTheInPositionByte()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        var obj = rig.M.World.GetLocatedObjectById(7)!;
        rig.X = 60; rig.Angle = 0; rig.State();
        var drive = new DriveToObjectAction(rig.M, 7, PreActionType.Docking);
        bool inPos = false;
        Assert.Equal(ActionResult.Success, drive.GetPossiblePoses(obj, out var poses, ref inPos));
        Assert.True(inPos);
        Assert.Equal(4, poses.Count);
        // Hand-worked from M12-001 / M12-021 / M12-036 (an independent numpy evaluation of the tables, see the M12_001 tests): a flat cube's Docking candidates are the
        // face-def vector [Front, Back, Left, Right, Top (mask 0), Bottom] x sb 0..3 gated by 0x05 on the sides and 0x0F on the bottom, and only the four side faces at sb 0 lie
        // inside the 30 degree cone (R22 = 1); sb 2 is upside down (R22 = -1), sb 1 and 3 are on their side (0) and the bottom face's poses are tilted. So the answer is exactly
        // Front, Back, Left, Right, in that order (ascending ActionType, then cache order, no sort).
        Assert.Equal(new[] { BlockFace.Front, BlockFace.Back, BlockFace.Left, BlockFace.Right }, poses.Select(p => p.Marker.Face));
        Assert.All(poses, p => Assert.Equal(0, p.RotationIndex));
        rig.X = -20; rig.State();
        bool farInPos = false;
        drive.GetPossiblePoses(obj, out _, ref farInPos);
        Assert.False(farInPos);
        rig.M.Docking.Carrying.SetCarrying(7);
        bool ignored = false;
        Assert.Equal(0x03000004u, (uint)drive.GetPossiblePoses(obj, out _, ref ignored));
    }

    /// <summary>
    /// M12-029, RemoveMatchingPredockPose 0x00551418 (research 9.6): a pose that IsSameAs the match within Point3(100, 100, 100) and Radians 0x3F060A92 (30 degrees) is
    /// erased and the same index is tested again; true if any was erased. Match (0, 0): (0, 0) and (50, 50) go, and so does a second copy of (0, 0) right behind the first;
    /// (150, 0) (150 &gt; 100), (0, 0) with dz = 101 and (0, 0) yawed 0.6 (&gt; 0.5236) stay.
    /// </summary>
    [Fact]
    public void M12_029_RemoveMatchingPredockPoseErasesEveryMatchInPlace()
    {
        var list = new List<PreActionPose>
        {
            FakePose(At(0, 0)), FakePose(At(0, 0)), FakePose(At(50, 50)), FakePose(At(150, 0)), FakePose(At(0, 0, 101)), FakePose(At(0, 0, 0, 0.6)),
        };
        Assert.True(DockPreActionPoses.RemoveMatchingPredockPose(At(0, 0), list));
        Assert.Equal(3, list.Count);
        Assert.Equal(150.0, list[0].WorldPose.Translation.X);
        Assert.Equal(101.0, list[1].WorldPose.Translation.Z);
        Assert.Equal(0.6, list[2].WorldPose.AngleAroundZ, 9);
        Assert.False(DockPreActionPoses.RemoveMatchingPredockPose(At(1000, 1000), list));
    }

    /// <summary>
    /// M12-029, the DriveToHelper functor 0x005B61A2: GetPossiblePoses, then (result 0) RemoveMatchingPredockPose and the in-position flag cleared when it removed
    /// something. The robot is in position at the Front pose; excluding the Front pose itself leaves the flag false and the Front pose gone.
    /// </summary>
    [Fact]
    public void M12_029_TheDriveToHelperFunctorRemovesTheMatchAndClearsInPosition()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        var obj = rig.M.World.GetLocatedObjectById(7)!;
        rig.X = 60; rig.Angle = 0; rig.State();
        var plain = new DriveToObjectAction(rig.M, 7, PreActionType.Docking);
        bool inPos = false;
        plain.GetPossiblePoses(obj, out var all, ref inPos);
        Assert.True(inPos);
        var front = all.First(p => p.Marker.Face == BlockFace.Front);
        var withMatch = new DriveToObjectAction(rig.M, 7, PreActionType.Docking) { DriveToHelperMatchPose = front.WorldPose };
        bool inPos2 = false;
        Assert.Equal(ActionResult.Success, withMatch.PosesFunction(obj, out var left, ref inPos2));
        Assert.False(inPos2);
        Assert.DoesNotContain(left, p => p.Marker.Face == BlockFace.Front);
        Assert.True(left.Count < all.Count);
    }

    // ------------------------------------------------------------------ M12-030

    /// <summary>M12-030, ctor 0x00559CD4 (research 13.1): the base type is 2 when the first bool is set (production), else 1; the remaining arguments are stored.</summary>
    [Fact]
    public void M12_030_TheConstructorPicksActionTypeTwoOrOne()
    {
        using var rig = new Rig();
        var two = new DriveToPlaceCarriedObjectAction(rig.M, At(300, 0, 22), true, false, false, true, 5.0);
        Assert.Equal(PreActionType.PlaceOnGround, two.ActionType); Assert.Equal(2, (int)two.ActionType);
        Assert.True(two.FlagAt0x179); Assert.False(two.FlagAt0x178); Assert.Equal(5.0, two.PaddingAt0x17C);
        var one = new DriveToPlaceCarriedObjectAction(rig.M, At(300, 0, 22), false, true, false, false, 0.0);
        Assert.Equal(PreActionType.PlaceRelative, one.ActionType); Assert.Equal(1, (int)one.ActionType);
        Assert.True(one.FlagAt0x178);
    }

    // ------------------------------------------------------------------ M12-017 / M12-025 / M12-005

    private static byte[] OnlyDock(Rig rig) => rig.Sent.OfType<DockWithObject>().Last().ToBytes();

    /// <summary>
    /// M12-017 step order (research 6.15): the pre-action check (step 4) comes BEFORE InitInternal (step 13). A place with A = 17 (A' = -17 &lt; -16.000009: InitInternal would give
    /// 0x03000000) and the pre-action check requested: the robot far from every pose gets 0x04000001 (step 4 returns first); the robot standing on a pose gets the
    /// 0x03000000 from InitInternal. Nothing is docked either time.
    /// </summary>
    [Fact]
    public async Task M12_017_InitInternalRunsAfterThePreActionCheck()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        var cube = Assert.Single(rig.Frame().Objects).Object;
        rig.M.Docking.Carrying.SetCarrying(8);
        PlaceRelObjectAction Make() => new(rig.M, 7, flagAt0xA8: true, a: 17.0, b: 5.0, dockActionFlagAt0x95: false, skipOffsetTransform: false) { CheckPreActionPose = true };
        rig.X = -400; rig.State();
        var far = Make();
        Assert.Equal(0x04000001u, (uint)await far.RunAsync(default));
        // stand on the Front PlaceRelative pose of the cube, facing it. Hand-worked (M12-001): stored = Front marker (Rz(-pi/2), (-22, 0, 0)) o built (Rz(pi/2), (0, -100, -22)) =
        // (I, (-122, 0, -22)), |t| = 123.965, a = 40; P0 = (78, 0, 0); a robot at the origin has dist2 = |0 - 78| = 78, b = min(78, 40) = 40, so the pose is
        // 200 - 122/123.965*(123.965 + 40) = 38.64 in x, y 0. From there b = 39.36 and the pose moves 0.6 mm: well inside the threshold pair (42.8, 21.4).
        rig.X = 38.64f; rig.Y = 0; rig.Angle = 0; rig.State();
        var near = Make();
        Assert.Equal(0x03000000u, (uint)await near.RunAsync(default));
        Assert.Empty(rig.Pump().OfType<DockWithObject>());
        Assert.Contains(near.Trace, l => l.Contains("not modelled") && l.Contains("M12-034"));
    }

    /// <summary>
    /// M12-017 step 5 / SelectDockAction 0x00555434..0x005555B5 (research 6.16 and the record text): not carrying is 0x03000011; +0xA8 non-zero selects DockAction 3 (PlaceLow);
    /// otherwise, if CanStackOnTopOfObject, DockAction 2 (PlaceHigh) and +0xBB = 0; otherwise 0x03000004. A cube on top of another (centre z = 66 &gt; 0.5 * 44 + 15 + 1e-5, IsPoseTooHigh
    /// 0x00877954) cannot be stacked on.
    /// </summary>
    [Fact]
    public async Task M12_017_SelectDockActionForAPlaceRel()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        PlaceRelObjectAction Make(bool low) => new(rig.M, 7, flagAt0xA8: low, a: 0, b: 0, dockActionFlagAt0x95: false, skipOffsetTransform: true) { CheckPreActionPose = false };
        Assert.Equal(0x03000011u, (uint)await Make(false).RunAsync(default));
        rig.M.Docking.Carrying.SetCarrying(8);
        var high = Make(false);
        var t = high.RunAsync(default); Spin(t, rig, () => rig.Frame());
        Assert.Equal(DockAction.PlaceHigh, high.SelectedDockAction);
        Assert.Equal(0, high.Field0xBB);
        rig.M.Docking.Carrying.SetCarrying(8);
        var low = Make(true);
        t = low.RunAsync(default); Spin(t, rig, () => rig.Frame());
        Assert.Equal(DockAction.PlaceLow, low.SelectedDockAction);
        // a target that is too high to stack on
        using var rig2 = new Rig();
        rig2.Cube = new Pose3d(Mat3.Identity, new Vec3(120, 0, 66)); rig2.Head = 0.1f;
        rig2.Frame();
        rig2.M.Docking.Carrying.SetCarrying(8);
        var tooHigh = new PlaceRelObjectAction(rig2.M, 7, flagAt0xA8: false, a: 0, b: 0, dockActionFlagAt0x95: false, skipOffsetTransform: true) { CheckPreActionPose = false };
        Assert.Equal(0x03000004u, (uint)await tooHigh.RunAsync(default));
    }

    /// <summary>
    /// M12-005 / M12-025, DockWithObject byte 7 = [+0xBB] and byte 5 = [+0x95] (research 6.16-6.18; writers 0x005554D0 -> 0, 0x00554E12 -> 3 iff |B'| &gt;= 1e-5): a place with B = 5 sends
    /// byte 7 = 3 (SelectDockAction stored 0, InitInternal then 3); with B = 0 it sends 0. The IDockAction ctor bool (+0x95) is byte 5. Message offsets: DockAction [17],
    /// +0x95 [18], +0xBA [19], +0xBB [20], +0xC1 [21] (the tag byte is [0]). Run twice on one action: the second Init's SelectDockAction stores 0 again before InitInternal, so a
    /// B that has dropped to 0 leaves +0xBB at 0, not at the stale 3.
    /// </summary>
    [Fact]
    public async Task M12_005_ThePlaceDockCarriesPlus0xBBAndPlus0x95()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        rig.M.Docking.Carrying.SetCarrying(8);
        var act = new PlaceRelObjectAction(rig.M, 7, flagAt0xA8: false, a: 0, b: 5, dockActionFlagAt0x95: true, skipOffsetTransform: true);
        var t = act.RunAsync(default); Spin(t, rig, () => rig.Frame());
        var b = OnlyDock(rig);
        Assert.Equal((byte)DockAction.PlaceHigh, b[17]);
        Assert.Equal(1, b[18]);                                    // +0x95
        Assert.Equal(0, b[19]);                                    // +0xBA
        Assert.Equal(3, b[20]);                                    // +0xBB = 3: |B'| = 5 >= 1e-5
        Assert.Equal(0, b[21]);                                    // +0xC1
        rig.M.Docking.Carrying.SetCarrying(8);
        act.Offsets = (0, 0, 0);
        t = act.RunAsync(default); Spin(t, rig, () => rig.Frame());
        Assert.Equal(0, OnlyDock(rig)[20]);                        // SelectDockAction's 0, and InitInternal's B' = 0 writes nothing
        rig.M.Docking.Carrying.SetCarrying(8);
        var plain = new PlaceRelObjectAction(rig.M, 7, flagAt0xA8: true, a: 0, b: 0, dockActionFlagAt0x95: false, skipOffsetTransform: true) { CheckPreActionPose = false };
        t = plain.RunAsync(default); Spin(t, rig, () => rig.Frame());
        var b2 = OnlyDock(rig);
        Assert.Equal((byte)DockAction.PlaceLow, b2[17]); Assert.Equal(0, b2[18]); Assert.Equal(0, b2[20]);
    }

    /// <summary>M12-005, the writers other than PlaceRel: RollObjectAction stores 0 in +0xBB (0x005565D6) and DockAction 5 in +0x80 (0x005565DC): byte 4 = 5, byte 7 = 0; PickupObjectAction writes 2 (0x005536EA).</summary>
    [Fact]
    public async Task M12_005_RollWritesZeroAndPickupWritesTwoInPlus0xBB()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(150, 0);
        Assert.Single(rig.Frame().Objects);
        var roll = new RollObjectAction(rig.M, 7) { CheckPreActionPose = false };
        var t = roll.RunAsync(default); Spin(t, rig, () => rig.Frame());
        var b = OnlyDock(rig);
        Assert.Equal(5, b[17]); Assert.Equal(0, b[20]);
        using var rig2 = new Rig();
        rig2.Cube = ManipulationTests.CubeAt(150, 0);
        Assert.Single(rig2.Frame().Objects);
        var pick = new PickupObjectAction(rig2.M, 7) { CheckPreActionPose = false };
        t = pick.RunAsync(default); Spin(t, rig2, () => rig2.Frame());
        Assert.Equal(2, OnlyDock(rig2)[20]);
    }

    /// <summary>
    /// M12-017 step 8 with [+0xB9] (research 6.8): the dock marker is the closest pre-action pose's marker and the log "Robot is within (%.1f,%.1f) of the nearest pre-action
    /// pose, proceeding with docking." is written. A robot at (60, 0) facing a cube at (200, 0) is nearest the Front pose, whose marker is the Front face (marker at -x).
    /// </summary>
    [Fact]
    public async Task M12_017_TheDockMarkerIsTheClosestPreActionPosesMarker()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        rig.X = 60; rig.Angle = 0; rig.State();
        var pick = new PickupObjectAction(rig.M, 7);
        var t = pick.RunAsync(default); Spin(t, rig, () => rig.Frame());
        Assert.Contains(pick.Trace, l => l.StartsWith("Robot is within (") && l.EndsWith("proceeding with docking."));
        Assert.Contains(pick.Trace, l => l.Contains("Docking with marker LightCubeI_Front"));
    }

    /// <summary>
    /// M12-025, InitInternal 0x00554DCC tests the POST-transform B' (0x00554DDC transform, then 0x00554DE0 vldr s0,[r4,#0x10c]): A = 10, B = 0 with psi ~ +pi/2 gives (A', B') = (B, A) =
    /// (0, 10) so +0xBB = 3 although B = 0; with psi ~ 0 (A', B') = (-A, B) = (-10, 0) and +0xBB stays 0 although A != 0. The test is movpl (0x00554E10/0x00554E12), so a NaN B' also
    /// sets 3.
    /// </summary>
    [Fact]
    public void M12_025_TheBBTestReadsThePostTransformB()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        var quarter = new PlaceRelObjectAction(rig.M, 7, false, a: 10.0, b: 0.0, dockActionFlagAt0x95: false, skipOffsetTransform: false) { Queries = CentreRotatedBy(Math.PI / 2) };
        Assert.Equal(ActionResult.Success, quarter.RunInitInternal());
        Assert.Equal(10.0, quarter.OffsetB, 9); Assert.Equal(3, quarter.Field0xBB);
        var zero = new PlaceRelObjectAction(rig.M, 7, false, a: 10.0, b: 0.0, dockActionFlagAt0x95: false, skipOffsetTransform: false) { Queries = CentreRotatedBy(0.0) };
        Assert.Equal(ActionResult.Success, zero.RunInitInternal());
        Assert.Equal(0.0, zero.OffsetB, 9); Assert.Equal(-10.0, zero.OffsetA, 9); Assert.Equal(0, zero.Field0xBB);
        var nan = new PlaceRelObjectAction(rig.M, 7, false, a: 0.0, b: double.NaN, dockActionFlagAt0x95: false, skipOffsetTransform: true);
        nan.RunInitInternal();
        Assert.Equal(3, nan.Field0xBB);
    }

    /// <summary>
    /// M12-025, the transform's three psi compares are raw STRICT compares against 0x3E860A92 (0x00554EC8..0x00554ED8, 0x00554F0A..0x00554F12, 0x00554F38..0x00554F40): a
    /// difference equal to the tolerance is not near, one just under it is; NaN is near nothing (vcmpe unordered, bpl taken). The four windows follow from that on either side.
    /// </summary>
    [Fact]
    public void M12_025_TheAlignmentCompareIsStrict()
    {
        double tol = BitConverter.UInt32BitsToSingle(0x3E860A92);
        Assert.False(PlaceRelObjectAction.IsWithinAlignmentTolerance(tol));
        Assert.False(PlaceRelObjectAction.IsWithinAlignmentTolerance(-tol));
        Assert.True(PlaceRelObjectAction.IsWithinAlignmentTolerance(tol - 1e-9));
        Assert.False(PlaceRelObjectAction.IsWithinAlignmentTolerance(double.NaN));
    }

    /// <summary>M12-025 with the engine's own queries (M12-033): a cube observed near yaw 0 gives (A', B') = (-A, B) = (-10, 5) (psi within 15 degrees of 0).</summary>
    [Fact]
    public void M12_025_TheTransformWorksWithTheEnginesQueries()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        var act = new PlaceRelObjectAction(rig.M, 7, false, a: 10.0, b: 5.0, dockActionFlagAt0x95: false, skipOffsetTransform: false);
        Assert.Equal(ActionResult.Success, act.TransformPlacementOffsetsRelativeObject());
        Assert.Equal(-10.0, act.OffsetA, 9); Assert.Equal(5.0, act.OffsetB, 9);
    }

    // ------------------------------------------------------------------ M12-026 (camera and marker fields, IsNear)

    private static CameraCalibration DistinctCal() => new()
    {
        FocalLengthX = 200, FocalLengthY = 100, CenterX = 160, CenterY = 120, Rows = 240, Columns = 320,
    };

    /// <summary>
    /// M12-026 row 12.14 with the engine's field names (CameraCalibration::CreateJson 0x0085F16C: nrows [c], ncols [c+2], focalLength_x [c+4]; research Q4.1-Q4.4): with
    /// ncols = 320, nrows = 240, fx = 200, fy = 100, t = tanf(atan2f(ncols*0.5, fx)) = 160/200 = 0.8 (a rows/fy reading would give 120/100 = 1.2). Marker width KnownMarker+0x10 = 25.0
    /// (M12-021, size 25.0f). E3/E4 have |y| = 97, o = B = 0: s4 = 97, minDist = 25/0.8 = 31.25, W = 97*0.8 - 25 = 52.6, W &gt; 20 so W - 20 = 32.6 (0x005560C8); A = 50 clamps to 32.6
    /// (a rows/fy mapping would leave 50, no width reduction 50 too). E1 (same-sign x, y = 0, three poses left) and E2 (sign mismatch) are erased.
    /// </summary>
    [Fact]
    public void M12_026_TheCameraFieldsAndTheTwentyMillimetreReductionGiveTheClampedShift()
    {
        var obj = NewCube(At(0, 0, 22));
        var status = PlaceRelObjectOffsetPoses.Filter(obj, 50, 0, At(1000, 1000), DistinctCal(), IdentityCentre, new[] { E1, E2, E3, E4 }, out var poses, out _);
        Assert.Equal(ActionResult.Success, status);
        Assert.Equal(2, poses.Count);
        Assert.Equal(32.6, poses[0].Translation.X, 6); Assert.Equal(-97.0, poses[0].Translation.Y, 6);
        Assert.Equal(32.6, poses[1].Translation.X, 6); Assert.Equal(97.0, poses[1].Translation.Y, 6);
    }

    /// <summary>
    /// M12-026 row 12.12 (research Q4.4): s4 &lt; minDist = width / t is 'InvalidDistance' and the pose is erased. With t = 0.8 and width 25 the limit is 31.25: a pose 30 out is erased (s4 = 30),
    /// one 32 out is kept (s4 = 32, W = 32*0.8 - 25 = 0.6, no reduction as 0.6 &lt; 20, and A = B = 0 leave it where it is).
    /// </summary>
    [Fact]
    public void M12_026_ThePoseIsErasedBelowWidthOverT()
    {
        var obj = NewCube(At(0, 0, 22));
        var close = At(30, 0, 0, Math.PI);
        Assert.Equal(0x03000010u, (uint)PlaceRelObjectOffsetPoses.Filter(obj, 0, 0, close, DistinctCal(), IdentityCentre, new[] { close }, out _, out _));
        var ok = At(32, 0, 0, Math.PI);
        Assert.Equal(ActionResult.Success, PlaceRelObjectOffsetPoses.Filter(obj, 0, 0, ok, DistinctCal(), IdentityCentre, new[] { ok }, out var kept, out _));
        Assert.Equal(32.0, Assert.Single(kept).Translation.X, 9);
    }

    /// <summary>
    /// M12-026 row 12.11 (research Q3.5): Radians::IsNear is a STRICT, wrap-aware compare with tolerance 2 degrees (0.0349066): a local yaw 0.03 from +pi/2 is facing (mode 1: dx = clamp(A), dy = B), and
    /// so is one 0.03 from -pi/2 (Radians(4.712389) rescales to about -pi/2); 0.04 from +pi/2 is not (mode 0: dx = A, dy = clamp(B, -W, W)). Pose at (0, -97), A = 50, B = 0, t = 0.8: mode 1 gives x = 32.6
    /// (W = 97 * 0.8 - 25 - 20); mode 0 has s4 = A + |x| = 50, W = 50 * 0.8 - 25 = 15, so x = A = 50 and y = clamp(0) = 0.
    /// </summary>
    [Fact]
    public void M12_026_TheFacingTestIsStrictAndWrapAware()
    {
        var obj = NewCube(At(0, 0, 22));
        Pose3d Run(double yaw)
        {
            var e = At(0, -97, 0, yaw);
            Assert.Equal(ActionResult.Success, PlaceRelObjectOffsetPoses.Filter(obj, 50, 0, At(1000, 1000), DistinctCal(), IdentityCentre, new[] { e }, out var p, out _));
            return Assert.Single(p);
        }
        Assert.Equal(32.6, Run(Math.PI / 2 + 0.03).Translation.X, 6);
        Assert.Equal(32.6, Run(-Math.PI / 2 - 0.03).Translation.X, 6);
        Assert.Equal(50.0, Run(Math.PI / 2 + 0.04).Translation.X, 6);
    }

    /// <summary>
    /// M12-026 request (research Q4.7, Q4.8): GetPreActionPoses(ActionType 1, flag A 0); a non-zero result (the object is carried: 0x03000004) leaves the vector empty and the exit returns
    /// 0x03000010, not GetPreActionPoses' code; with an ordinary cube the pose list is what GetPreActionPoses returned and the function succeeds.
    /// </summary>
    [Fact]
    public void M12_026_ComputeTakesItsPosesFromGetPreActionPoses()
    {
        var obj = NewCube(At(200, 0, 22));
        var cal = DistinctCal();
        Assert.Equal(0x03000010u, (uint)PlaceRelObjectOffsetPoses.Compute(obj, 0, 0, At(0, 0), 7u, cal, ObjectPoseQueries.Engine, NoObstacles, out var none, out _));
        Assert.Empty(none);
        Assert.Equal(ActionResult.Success, PlaceRelObjectOffsetPoses.Compute(obj, 0, 0, At(0, 0), null, cal, ObjectPoseQueries.Engine, NoObstacles, out var poses, out _));
        Assert.NotEmpty(poses);
    }

    // ------------------------------------------------------------------ M12-022 (NaN and zero)

    /// <summary>
    /// M12-022, InitHelper 0x00558FB4 (research 5.1, 5.2): a strictly negative +0x84 gives 0x0300000D (0x00558FE2 vcmpe s0,#0; 0x00558FEA bpl skips the error block 0x00558FEC..0x0055903E
    /// unless N is set, which only a negative value does).
    /// </summary>
    [Theory]
    [InlineData(-1.0f)]
    [InlineData(-0.001f)]
    public async Task M12_022_AStrictlyNegativeDistanceIsNoDistanceSet(float distance)
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(300, 0);
        rig.Frame();
        var drive = new DriveToObjectAction(rig.M, 7, distance, useManualSpeed: false);
        Assert.Equal(0x0300000Du, (uint)await drive.RunAsync(default));
        Assert.Empty(rig.Pump().OfType<ExecutePath>());
        var sevenArgNone = new DriveToObjectAction(rig.M, 7, PreActionType.None);          // the 7-arg ctor stores -1.0
        Assert.Equal(ActionResult.NoDistanceSet, await sevenArgNone.RunAsync(default));
    }

    /// <summary>
    /// M12-022 (research 5.2): +0.0 and -0.0 do NOT error: vcmpe s0,#0 sets Z (equal), N stays clear, bpl is taken. The drive is built to the object's own point (delta * 0 = 0) and a path is sent.
    /// </summary>
    [Theory]
    [InlineData(0.0f)]
    [InlineData(-0.0f)]
    public void M12_022_ZeroDistanceIsNotAnError(float distance)
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(300, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var drive = new DriveToObjectAction(rig.M, 7, distance, useManualSpeed: false);
        var task = drive.RunAsync(default);
        Spin(task, rig, ms: 60000);
        Assert.True(task.IsCompleted);
        Assert.NotEqual(0x0300000Du, (uint)task.Result);
        Assert.Contains(rig.Sent, m => m is AppendPathSegmentLine);
    }

    /// <summary>
    /// M12-022 (research 5.2, 5.3): a NaN +0x84 does NOT take the error path (unordered vcmpe: N = 0, bpl taken); the goal build multiplies delta by NaN (0x005591E4) so the goal is NaN, and
    /// InitHelper returns no result for it. This stack throws a visible NotSupportedException for the NaN goal (MISSING: what the planner does with it) instead of sending a path.
    /// </summary>
    [Fact]
    public async Task M12_022_ANanDistanceBuildsANanGoalAndIsAVisibleStub()
    {
        Assert.True(DriveToObjectAction.TryBuildObjectDeltaGoal(At(200, 100, 22), At(0, 0, 5), float.NaN, out var goal));
        Assert.True(double.IsNaN(goal.Translation.X) && double.IsNaN(goal.Translation.Y));
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(300, 0);
        rig.Frame();
        var drive = new DriveToObjectAction(rig.M, 7, float.NaN, useManualSpeed: false);
        await Assert.ThrowsAsync<NotSupportedException>(() => drive.RunAsync(default));
        Assert.Empty(rig.Pump().OfType<ExecutePath>());
    }

    // ------------------------------------------------------------------ M12-008 (BlockPickedUp, synchronisation)

    /// <summary>
    /// M12-008 step 8, SetParent 0x00633006..0x00633010: the cube on top is parented to the carried one AT ONCE. After a successful pick-up dock, with no frame processed since, the top cube
    /// still has the offset from the carried cube it had before (the carried one moved to the lift).
    /// </summary>
    [Fact]
    public void M12_008_TheCubeOnTopFollowsAtOnceWhenThePickupResultArrives()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Head = 0.05f;
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(260, 0, 22));
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.Identity, new Vec3(260, 0, 66))));
        rig.Frame(); rig.Frame();
        var bottom = rig.Vision.World.GetLocatedObjectById(7)!;
        var top = rig.Vision.World.GetLocatedObjectById(8)!;
        var relBefore = top.Pose.WithRespectTo(bottom.Pose).Translation;
        var dock = rig.M.Docking.DockAsync(bottom, bottom.Markers.First(k => k.Code == MarkerType.LightCubeI_Front), DockAction.PickupLow, PathMotionProfile.Default, timeout: TimeSpan.FromSeconds(5));
        Spin(dock, rig, ms: 60000);
        Assert.True(dock.IsCompleted);
        Assert.Equal(0u, rig.M.Docking.LastAttachResult);
        Assert.Equal(8u, rig.M.Docking.Carrying.CarriedOnTopId);
        var rel = top.Pose.WithRespectTo(bottom.Pose).Translation;
        Assert.Equal(relBefore.X, rel.X, 6); Assert.Equal(relBefore.Y, rel.Y, 6); Assert.Equal(relBefore.Z, rel.Z, 6);
        Assert.True(bottom.Pose.Translation.X < 200);               // the carried cube is on the lift, not where it stood (260)
    }

    /// <summary>
    /// M12-008 step 2 (0x00632CD8..0x00632D2E): already carrying returns 1. The result is recorded in LastAttachResult and logged; what HandlePickAndPlaceResult does with a non-zero
    /// result is not in the inventory (MISSING), so nothing else changes: the earlier carried object stays carried.
    /// </summary>
    [Fact]
    public void M12_008_ANonZeroAttachResultIsRecordedAndLoggedNotDecided()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(150, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        rig.M.Docking.Carrying.SetCarrying(9);
        var dock = rig.M.Docking.DockAsync(obj, obj.Markers.First(k => k.Code == MarkerType.LightCubeI_Front), DockAction.PickupLow, PathMotionProfile.Default, timeout: TimeSpan.FromSeconds(5));
        Spin(dock, rig, ms: 60000);
        Assert.True(dock.IsCompleted);
        Assert.Equal(1u, rig.M.Docking.LastAttachResult);
        Assert.True(rig.M.Docking.Carrying.IsCarrying(9));
        Assert.Contains(rig.Log, l => l.Contains("SetObjectAsAttachedToLift returned 1"));
    }

    /// <summary>
    /// M12-008, AddLiftRelativeObservation 0x00506E5C (SetPose Known, count 1) and AddObjectRelativeObservation 0x00506CD8 (SetPose Dirty): the write runs under the world's lock and raises
    /// PoseStateChanged when the state changes, as MarkReleased does; BroadcastObjectPoseChanged (M12-027, unread) is counted, and the counter is atomic (4 threads x 1000 calls add exactly 4000).
    /// </summary>
    [Fact]
    public void M12_008_TheConfirmerWritesAreSynchronisedAndTheBroadcastCounterIsAtomic()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(150, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var changes = new List<(PoseState From, PoseState To)>();
        rig.Vision.World.PoseStateChanged += (_, from, to) => { lock (changes) changes.Add((from, to)); };
        Assert.Equal(0u, ObjectPoseConfirmerRelative.AddObjectRelativeObservation(rig.Vision.World, obj, At(150, 0, 22)));
        Assert.Equal(PoseState.Dirty, obj.PoseState);
        Assert.Equal(0u, ObjectPoseConfirmerRelative.AddLiftRelativeObservation(rig.Vision.World, obj, At(20, 0, 30)));
        Assert.Equal(PoseState.Known, obj.PoseState); Assert.Equal(1, obj.PoseConfirmationCount);
        Assert.Equal(new[] { (PoseState.Known, PoseState.Dirty), (PoseState.Dirty, PoseState.Known) }, changes);
        int before = ObjectPoseConfirmerRelative.UnreadBroadcasts;
        var threads = Enumerable.Range(0, 4).Select(_ => new Thread(() => { for (int i = 0; i < 1000; i++) ObjectPoseConfirmerRelative.AddLiftRelativeObservation(rig.Vision.World, obj, At(20, 0, 30)); })).ToList();
        threads.ForEach(t => t.Start()); threads.ForEach(t => t.Join());
        Assert.Equal(before + 4000, ObjectPoseConfirmerRelative.UnreadBroadcasts);
    }

    // ================================================================== R-VIS M12 fix round 2: records M12-001, 008, 012, 017, 025, 030, 031, 035, 036, 037
    // Every expected value is worked by hand from the record text / citation (the pose tables worked with an independent numpy evaluation of M12-001's per-type
    // constants and M12-021's masks), never from what the code returned.

    private static ObservableObject CubeWith(uint id, ObjectType type, Pose3d pose) =>
        new(id, type, CubeGeometry.CubeMarkers(type)) { Pose = pose, PoseState = PoseState.Known };

    /// <summary>The hand-built Front docking pose of a cube at (200, 0, 22) for a robot at the origin: (40.289, 0, -18.387), rotation identity (see ManipulationTests, M12-001 E1).</summary>
    private static PreActionPose FrontDockingPoseOfTheCubeAt200(ObservableObject cube) =>
        new(PreActionType.Docking, cube.Markers.First(m => m.Face == BlockFace.Front), At(40.289, 0, -18.387), 75.0);

    private static PreActionObstacle Obstacle(uint id, double x, double y, double z = 22) =>
        PreActionObstacle.For(CubeWith(id, ObjectType.Block_LIGHTCUBE2, At(x, y, z)), 0f);

    // ------------------------------------------------------------------ M12-036

    /// <summary>
    /// M12-036, IsPreActionPoseValid 0x004DF2C0 (literal 0x3E0930A4 = 0.13397461 at 0x004DF61C; test 0x004DF2F2..0x004DF318): |R22 - 1| &lt; 0.13397461, i.e. R22 &gt; cos 30 degrees, NaN invalid.
    /// Tilting the pose about X by theta gives R22 = cos(theta): 29.9 degrees (0.86690, |R22 - 1| = 0.1331) is valid, 30.1 degrees (0.86515, 0.13485) is not, and 45 degrees (0.7071) is not
    /// (the retired UprightOnly rule R22 &gt; 0.5 would have kept it).
    /// </summary>
    [Theory]
    [InlineData(0.0, true)]
    [InlineData(29.9, true)]
    [InlineData(30.1, false)]
    [InlineData(45.0, false)]
    [InlineData(180.0, false)]
    public void M12_036_TheRotationConeIsThirtyDegrees(double tiltDegrees, bool valid)
    {
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22));
        var front = cube.Markers.First(m => m.Face == BlockFace.Front);
        var pose = new PreActionPose(PreActionType.Docking, front, new Pose3d(Mat3.AboutX(tiltDegrees * Math.PI / 180), new Vec3(40.289, 0, -18.387)), 75.0);
        Assert.Equal(valid, PreActionValidity.IsPreActionPoseValid(cube, pose, Array.Empty<PreActionObstacle>()));
        Assert.Equal(valid, PreActionValidity.IsPreActionPoseValid(cube, pose, new[] { Obstacle(9, 1000, 1000) }));   // the same rule runs before the obstacle test
        var nan = new PreActionPose(PreActionType.Docking, front, new Pose3d(new Mat3(double.NaN, 0, 0, 0, 1, 0, 0, 0, double.NaN), new Vec3(0, 0, 0)), 75.0);
        Assert.False(PreActionValidity.IsPreActionPoseValid(cube, nan, Array.Empty<PreActionObstacle>()));
    }

    /// <summary>
    /// M12-036, the swept footprint (0x004DF3xx..0x004DF506): pose p = (40.289, 0), marker m = the Front marker in the world (178, 0, 22): u = (1, 0), L = 137.711, n = (int)floorf((137.711 + 55.9)/10) = floor(19.361) = 19 (0x004DF468 is floorf, not ceilf),
    /// start = p - 55.9 u = (-15.611, 0), perp = (u.y, -u.x) * 27.1 = (0, -27.1); the sample centres are x = -15.611 + 10 i (i = 0..18, the last 164.389) with left/right at y = -27.1 / +27.1.
    /// A cube (44 mm) at (100, 0) is hit by the centre line; one at (100, 40) (y 18..62) by the right samples (27.1 is inside); one at (100, 50) (y 28..72) by none (27.1 &lt; 28); (100, -40) by the left
    /// samples; (-30, 0) (x -52..-8) by the first sample (-15.611); (-40, 0) (x -62..-18) by none (-15.611 &gt; -18); one at (300, 0) is past the last sample (164.389).
    /// </summary>
    [Theory]
    [InlineData(100, 0, false)]
    [InlineData(100, 40, false)]
    [InlineData(100, 50, true)]
    [InlineData(100, -40, false)]
    [InlineData(100, -50, true)]
    [InlineData(-30, 0, false)]
    [InlineData(-40, 0, true)]
    [InlineData(300, 0, true)]
    public void M12_036_TheSweptFootprintHitsAnObstacleOnItsSamples(double ox, double oy, bool valid)
    {
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22));
        Assert.Equal(valid, PreActionValidity.IsPreActionPoseValid(cube, FrontDockingPoseOfTheCubeAt200(cube), new[] { Obstacle(9, ox, oy) }));
    }

    /// <summary>
    /// M12-036, the two exemptions: an obstacle whose id equals this object's id is skipped ([base+0x18] vs [O+0x24]), and one whose centroid lies inside the object's OWN quad
    /// (Q = this-&gt;vtbl[0x50](this, obj+4, 0)) is skipped. A cube at (185, 0) (x 163..207) would be hit by the last samples (154.389, 164.389) but its centroid (185, 0) is inside the target's quad
    /// (x 178..222): skipped, valid. The same cube at (170, 0) (centroid outside the quad, still covering 164.389) is a hit. An obstacle carrying the target's own id 7 at (100, 0) is skipped
    /// while the same geometry with id 9 is a hit. An empty obstacle vector is valid (0x004DF31A..0x004DF320).
    /// </summary>
    [Fact]
    public void M12_036_TheOwnIdAndTheOwnQuadCentroidAreExempt()
    {
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22));
        var pose = FrontDockingPoseOfTheCubeAt200(cube);
        Assert.True(PreActionValidity.IsPreActionPoseValid(cube, pose, new[] { Obstacle(9, 185, 0) }));
        Assert.False(PreActionValidity.IsPreActionPoseValid(cube, pose, new[] { Obstacle(9, 170, 0) }));
        var ownId = new PreActionObstacle(Obstacle(9, 100, 0).Quad, new Point2f(100, 0), 7);
        Assert.True(PreActionValidity.IsPreActionPoseValid(cube, pose, new[] { ownId }));
        Assert.False(PreActionValidity.IsPreActionPoseValid(cube, pose, new[] { Obstacle(9, 100, 0) }));
        Assert.True(PreActionValidity.IsPreActionPoseValid(cube, pose, Array.Empty<PreActionObstacle>()));
    }

    /// <summary>
    /// M12-036, |m - p|^2 &lt;= 0: the direction is not normalised, the perpendicular is (0, 0) and every one of the n = floor(55.9/10) = 5 samples is p itself. A pose at the marker position
    /// (178, 0) is hit by an obstacle covering (178, 0) (a cube at (170, 0), x 148..192) and not by one at (178, 40).
    /// </summary>
    [Fact]
    public void M12_036_AZeroLengthSweepSamplesOnlyThePose()
    {
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22));
        var front = cube.Markers.First(m => m.Face == BlockFace.Front);
        var atMarker = new PreActionPose(PreActionType.Docking, front, At(178, 0, 22), 75.0);
        Assert.False(PreActionValidity.IsPreActionPoseValid(cube, atMarker, new[] { Obstacle(9, 170, 0) }));
        Assert.True(PreActionValidity.IsPreActionPoseValid(cube, atMarker, new[] { Obstacle(9, 178, 40) }));
    }

    /// <summary>
    /// M12-036, BlockWorld::GetObstacles 0x00626D44 / predicate 0x0062C1D4: minZ = the robot pose z, maxZ = minZ + Robot::GetHeight (here 100 from z 0); an object is excluded when
    /// (top &lt;= minZ AND bottom &lt;= minZ) OR (top &gt;= maxZ AND bottom &gt;= maxZ), the extent being [z - 0.5 rz, z + 0.5 rz] with rz = (R*size)[z] = 44 for an upright cube.
    /// z = 22 (0..44) is in; z = 0 (-22..22) is in; z = -22 (-44..0) is out (top &lt;= 0); z = 122 (100..144) is out (bottom &gt;= 100); z = 121 (99..143) is in (bottom &lt; 100);
    /// z = -21 (-43..1) is in. An upside-down cube has rz = -44, the same band. The ignored id is out.
    /// </summary>
    [Fact]
    public void M12_036_GetObstaclesKeepsTheObjectsThatOverlapTheRobotsHeightBand()
    {
        ObservableObject C(uint id, double z, Mat3? rot = null) => new(id, ObjectType.Block_LIGHTCUBE1, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1))
        { Pose = new Pose3d(rot ?? Mat3.Identity, new Vec3(id * 100.0, 0, z)), PoseState = PoseState.Known };
        var objects = new[] { C(1, 22), C(2, 0), C(3, -22), C(4, 122), C(5, 121), C(6, -21), C(7, 22, Mat3.AboutX(Math.PI)), C(8, 22), C(9, -22, Mat3.AboutX(Math.PI)) };
        var result = PreActionValidity.GetObstacles(objects, new uint[] { 8 }, Pose3d.Identity, 100.0, 0f);
        Assert.Equal(new uint[] { 1, 2, 5, 6, 7 }, result.Select(o => o.ObjectId));
        // the quad is the object's own footprint (M13-007): the cube at x = 100, y = 0 covers x 78..122, y -22..22
        var q = result[0].Quad!;
        Assert.True(q.Contains(new Point2f(100, 0)));
        Assert.True(q.Contains(new Point2f(79, 21)));
        Assert.False(q.Contains(new Point2f(77, 0)));
        Assert.False(result[0].IsStandIn);
    }

    /// <summary>
    /// M12-036 / M13-023 (labelled stand-in): an object whose footprint cannot be computed (here a cube pitched 0.3 rad, not yaw-only) is still an obstacle, as the M13-023 planar stand-in: it
    /// contains a point within half a cube (22 mm) of its centre in x, y. It is flagged so a caller can see it is not the engine's quad.
    /// </summary>
    [Fact]
    public void M12_036_ATiltedObstacleIsTheLabelledPlanarStandIn()
    {
        var tilted = new ObservableObject(9, ObjectType.Block_LIGHTCUBE2, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE2))
        { Pose = new Pose3d(Mat3.AboutY(0.3), new Vec3(100, 0, 22)), PoseState = PoseState.Known };
        var o = Assert.Single(PreActionValidity.GetObstacles(new[] { tilted }, Array.Empty<uint>(), Pose3d.Identity, 100.0, 0f));
        Assert.True(o.IsStandIn);
        Assert.True(o.Contains(new Point2f(100 + 21, 0)));
        Assert.False(o.Contains(new Point2f(100 + 23, 0)));
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22));
        Assert.False(PreActionValidity.IsPreActionPoseValid(cube, FrontDockingPoseOfTheCubeAt200(cube), new[] { o }));      // the sample (84.389, 0) is within 22 of (100, 0)
    }

    /// <summary>
    /// M12-036 with the rig: ManipulationSystem.GetObstacles ignores the carried ids (CarryingComponent::GetCarryingObjects) and uses Robot::GetHeight. A carried cube 7 is dropped from the list; the
    /// other located cube stays. Robot::GetHeight = max(66 sin(lift) + 45 + 5, 67.7) (0x00516F0C): at the rig's 32 mm lift the floor 67.7 applies.
    /// </summary>
    [Fact]
    public void M12_036_TheSystemsObstaclesIgnoreTheCarriedObject()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 60);
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.Identity, new Vec3(200, -20, 22))));
        rig.Frame();
        Assert.Equal(new uint[] { 7, 8 }, rig.M.GetObstacles(rig.M.RobotPose()!.Value).Select(o => o.ObjectId).OrderBy(i => i));
        rig.M.Docking.Carrying.SetCarrying(7);
        Assert.Equal(new uint[] { 8 }, rig.M.GetObstacles(rig.M.RobotPose()!.Value).Select(o => o.ObjectId));
        Assert.Equal((double)BitConverter.UInt32BitsToSingle(0x42876666), rig.M.RobotHeightMm(), 3);
    }

    // ------------------------------------------------------------------ M12-001

    private static List<PreActionPose> Current(ObservableObject cube, PreActionType[] withAction, MarkerType[]? withCode = null, IReadOnlyList<PreActionObstacle>? obstacles = null, Pose3d? robot = null)
    {
        var list = new List<PreActionPose>();
        CubePreActionPoses.GetCurrentPreActionPoses(cube, withAction, withCode ?? Array.Empty<MarkerType>(), robot ?? At(0, 0), 0, obstacles ?? Array.Empty<PreActionObstacle>(), list);
        return list;
    }

    /// <summary>
    /// M12-001, ActionableObject::GetCurrentPreActionPoses 0x004DF850: a flat cube's candidates are the face-def vector [Front, Back, Left, Right, Top (mask 0x00), Bottom] x sb 0..3 gated by
    /// 0x05 on the sides (sb 0 and 2) and 0x0F on the bottom (M12-021); an independent evaluation of the tables gives R22 = 1 only for the four side faces at sb 0 (sb 2 is upside down, -1; the
    /// bottom face's four poses are tilted), so for EVERY type (Docking, Flipping, Rolling, PlaceRelative, PlaceOnGround) exactly Front, Back, Left, Right at sb 0 survive, in that order.
    /// </summary>
    [Theory]
    [InlineData(PreActionType.Docking)]
    [InlineData(PreActionType.Flipping)]
    [InlineData(PreActionType.Rolling)]
    [InlineData(PreActionType.PlaceRelative)]
    [InlineData(PreActionType.PlaceOnGround)]
    public void M12_001_OnlyTheFourFlatSideFacePosesSurviveInFaceOrder(PreActionType type)
    {
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22));
        var poses = Current(cube, new[] { type });
        Assert.Equal(new[] { BlockFace.Front, BlockFace.Back, BlockFace.Left, BlockFace.Right }, poses.Select(p => p.Marker.Face));
        Assert.All(poses, p => { Assert.Equal(0, p.RotationIndex); Assert.Equal(type, p.Type); });
    }

    /// <summary>
    /// M12-001, ordering and the two filters (0x004DF88E..0x004DFDCA): the withAction set is walked in ASCENDING ActionType order whatever order it is given in, so {Rolling (4), Docking (0)}
    /// gives the four Docking poses then the four Rolling poses; a non-empty withCode set keeps only elements whose marker code is in it ({LightCubeI_Back} gives the Back pose alone); an
    /// empty withAction set collects nothing.
    /// </summary>
    [Fact]
    public void M12_001_TheTypesAreWalkedInAscendingOrderAndTheCodeFilterApplies()
    {
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22));
        var both = Current(cube, new[] { PreActionType.Rolling, PreActionType.Docking });
        Assert.Equal(new[] { PreActionType.Docking, PreActionType.Docking, PreActionType.Docking, PreActionType.Docking,
                             PreActionType.Rolling, PreActionType.Rolling, PreActionType.Rolling, PreActionType.Rolling }, both.Select(p => p.Type));
        var back = Assert.Single(Current(cube, new[] { PreActionType.Docking }, new[] { MarkerType.LightCubeI_Back }));
        Assert.Equal(BlockFace.Back, back.Marker.Face);
        Assert.Empty(Current(cube, Array.Empty<PreActionType>()));
    }

    /// <summary>
    /// M12-001, the return byte sb &amp; 1 (0x004DFDD4): true when some type's cache vector was empty and had to be generated during the call, NOT whether a pose survived. The first call for a
    /// type generates (true), a second call for the same object and type finds the cache full (false), a new type generates again (true); the Entry type (3) produces nothing so its cache is empty
    /// on every call (true); an empty withAction set touches no cache (false).
    /// </summary>
    [Fact]
    public void M12_001_TheReturnByteSaysACacheVectorWasGenerated()
    {
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22));
        bool Call(params PreActionType[] types) =>
            CubePreActionPoses.GetCurrentPreActionPoses(cube, types, Array.Empty<MarkerType>(), At(0, 0), 0, Array.Empty<PreActionObstacle>(), new List<PreActionPose>());
        Assert.True(Call(PreActionType.Docking));
        Assert.False(Call(PreActionType.Docking));
        Assert.True(Call(PreActionType.Docking, PreActionType.Rolling));
        Assert.False(Call(PreActionType.Docking, PreActionType.Rolling));
        Assert.True(Call(PreActionType.Entry));
        Assert.True(Call(PreActionType.Entry));
        Assert.False(Call());
        // a tilted cube generates too but nothing survives: the byte does not say "a pose survived"
        var tilted = CubeWith(8, ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.AboutY(35 * Math.PI / 180), new Vec3(200, 0, 22)));
        var none = new List<PreActionPose>();
        Assert.True(CubePreActionPoses.GetCurrentPreActionPoses(tilted, new[] { PreActionType.Docking }, Array.Empty<MarkerType>(), At(0, 0), 0, Array.Empty<PreActionObstacle>(), none));
        Assert.Empty(none);
    }

    /// <summary>
    /// M12-001 / M12-036: the cone decides on the composed pose's rotation. A cube pitched about Y by theta has R22 = cos(theta) for every side-face sb-0 pose (the stored rotation of a side face at sb 0 is
    /// a pure yaw): 25 degrees (0.906) keeps all four, 35 degrees (0.819, which the retired 0.5 rule kept) keeps none.
    /// </summary>
    [Theory]
    [InlineData(25.0, 4)]
    [InlineData(35.0, 0)]
    public void M12_001_ATiltedCubeIsFilteredByTheThirtyDegreeCone(double pitchDegrees, int survivors)
    {
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, new Pose3d(Mat3.AboutY(pitchDegrees * Math.PI / 180), new Vec3(200, 0, 22)));
        Assert.Equal(survivors, Current(cube, new[] { PreActionType.Docking }).Count);
    }

    /// <summary>
    /// M12-001 + M12-036 + M12-031 through GetPreActionPoses: an obstacle cube at (100, 0) is in the Front pose's sweep only. The Back pose (behind the cube) and the Left and Right poses (which sweep
    /// along x = 200, y from about 215 down to 25, with samples at x = 200 +- 27.1 clear of x 78..122) survive: the result is Success with Back, Left, Right in that order. A 35 degree pitched cube gives
    /// 0x03000010 (no pose left, 0x00550DC4 region), a carried one 0x03000004.
    /// </summary>
    [Fact]
    public void M12_031_GetPreActionPosesAppliesTheValidityFilterAndTheObstacles()
    {
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22));
        var obstacles = new[] { Obstacle(9, 100, 0), PreActionObstacle.For(cube, 0f) };       // the target itself is in the world too (its own id is skipped)
        var output = DockPreActionPoses.Get(new PreActionPoseInput(cube, PreActionType.Docking, false, 0.13, 0, false, 0), At(0, 0), null, () => obstacles);
        Assert.Equal(ActionResult.Success, output.Result);
        Assert.Equal(new[] { BlockFace.Back, BlockFace.Left, BlockFace.Right }, output.Poses.Select(p => p.Marker.Face));
        var tilted = CubeWith(8, ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.AboutY(35 * Math.PI / 180), new Vec3(200, 0, 22)));
        Assert.Equal(0x03000010u, (uint)DockPreActionPoses.Get(new PreActionPoseInput(tilted, PreActionType.Docking, false, 0.13, 0, false, 0), At(0, 0), null, NoObstacles).Result);
        Assert.Equal(0x03000004u, (uint)DockPreActionPoses.Get(new PreActionPoseInput(cube, PreActionType.Docking, false, 0.13, 0, false, 0), At(0, 0), 7u, NoObstacles).Result);
    }

    // ------------------------------------------------------------------ M12-031: FlipBlockAction::Init

    /// <summary>
    /// M12-031, FlipBlockAction::Init 0x0055EDC8 (0x0055EE10..0x0055EE66): GetPreActionPoses is called even when +0x140 is 0, flag A = the byte [this+0x140], tolerance 0x3DB2B8C2 (0.08727),
    /// and only the RESULT code is acted on, never the in-position byte. Hand-worked (cube (200, 0, 22) yaw 0, Flipping stored translation (-78.577, -78.577, -22) and yaw 45 degrees, a = 0 so b = 0):
    /// the Front flipping pose is (121.423, -78.577, 0); its distance to the cube is 113.28, so the thresholds are (19.75, 9.87). A robot standing exactly on it at yaw 0 is INSIDE the box with
    /// a failed yaw test (45 degrees &gt; 5): the byte is 0 and the result is 0, so Init continues (the retired C# refused with 0x04000001). A robot at the origin is outside the box: flag A
    /// set gives 0x04000001, flag A clear gives 0 and continues. A carried cube gives its result code 0x03000004 whatever flag A is.
    /// </summary>
    [Fact]
    public void M12_031_FlipBlockInitCallsGetPreActionPosesWithFlagAAndActsOnTheResultOnly()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        rig.X = 121.42f; rig.Y = -78.58f; rig.Angle = 0; rig.State((uint)(RobotStatusFlag.LiftInPos | RobotStatusFlag.HeadInPos));
        var inBox = new FlipBlockAction(rig.M, 7);
        var t = inBox.RunAsync(default); Spin(t, rig, () => rig.State((uint)(RobotStatusFlag.LiftInPos | RobotStatusFlag.HeadInPos)));
        Assert.NotEqual(0x04000001u, (uint)t.Result);
        Assert.DoesNotContain(inBox.Trace, l => l.Contains("GetPreActionPoses ->"));
        Assert.Contains(rig.Sent, m => m is AppendPathSegmentLine);                       // it went on to drive

        using var far = new Rig();
        far.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(far.Frame().Objects);
        var strict = new FlipBlockAction(far.M, 7);
        Assert.Equal(0x04000001u, (uint)strict.RunAsync(default).GetAwaiter().GetResult());
        far.State((uint)(RobotStatusFlag.LiftInPos | RobotStatusFlag.HeadInPos));
        var lenient = new FlipBlockAction(far.M, 7) { CheckPreActionPose = false };
        var t2 = lenient.RunAsync(default); Spin(t2, far, () => far.State((uint)(RobotStatusFlag.LiftInPos | RobotStatusFlag.HeadInPos)));
        Assert.NotEqual(0x04000001u, (uint)t2.Result);
        far.M.Docking.Carrying.SetCarrying(7);
        Assert.Equal(0x03000004u, (uint)new FlipBlockAction(far.M, 7) { CheckPreActionPose = false }.RunAsync(default).GetAwaiter().GetResult());
        Assert.Equal(0x03000004u, (uint)new FlipBlockAction(far.M, 7).RunAsync(default).GetAwaiter().GetResult());
    }

    // ------------------------------------------------------------------ M12-012

    /// <summary>
    /// M12-012, CanStackOnTopOfObject 0x0063C5C4 = CanInteractWithObjectHelper 0x0063C654 and !IsPoseTooHigh(pose, 1.0, 15.0, 0.5) (too high iff z &gt; 0.5*44 + 15 + 1e-5 = 37.00001). The helper
    /// (Q7.1): family 1 or 2, IsRestingFlat(10 degrees), not currently carried ([[robot+0x284]+8] != id, 0x0063C686..0x0063C698), GetWithRespectTo, and nothing found on top within 15.0; the helper reads NO PoseState (fix-round-2 verifier,
    /// 0x0063C654..0x0063C794). A flat cube at z 22 is stackable whatever its PoseState (Known, Dirty, even Unknown); carried, a charger-family object, one with a cube on top, and one at z 66 are not.
    /// IsPoseTooHigh is given the pose with respect to the robot pose (0x00877955); the rig's robot z is 0, so that cannot differ from the world z here.
    /// </summary>
    [Fact]
    public void M12_012_CanStackNeedsTheHelperChecksAndTheHeightRule()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        var cube = Assert.Single(rig.Frame().Objects).Object;
        var docking = rig.M.Docking;
        Assert.True(docking.CanStackOnTopOfObject(cube));                       // Known, flat, low, nothing on top, not carried
        cube.PoseState = PoseState.Dirty;
        Assert.True(docking.CanStackOnTopOfObject(cube));
        cube.PoseState = PoseState.Unknown;
        Assert.True(docking.CanStackOnTopOfObject(cube));                       // the unit reads no PoseState
        cube.PoseState = PoseState.Known;
        docking.Carrying.SetCarrying(7);
        Assert.False(docking.CanStackOnTopOfObject(cube));
        docking.Carrying.UnsetCarrying();
        Assert.True(docking.CanStackOnTopOfObject(cube));
        // the family test: a charger is not family 1 or 2
        var charger = new ObservableObject(30, ObjectType.Charger_Basic, ChargerGeometry.Markers) { Pose = At(200, 0, 22), PoseState = PoseState.Known };
        Assert.False(docking.CanStackOnTopOfObject(charger));

        using var rig2 = new Rig();
        rig2.Head = 0.05f;
        rig2.Cube = new Pose3d(Mat3.Identity, new Vec3(260, 0, 22));
        rig2.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.Identity, new Vec3(260, 0, 66))));
        rig2.Frame(); rig2.Frame();
        var bottom = rig2.M.World.GetLocatedObjectById(7)!;
        var top = rig2.M.World.GetLocatedObjectById(8)!;
        Assert.False(rig2.M.Docking.CanStackOnTopOfObject(bottom));             // a cube rests on it, within 15.0
        Assert.False(rig2.M.Docking.CanStackOnTopOfObject(top));                // z 66 > 37.00001
    }

    // ------------------------------------------------------------------ M12-008

    /// <summary>
    /// M12-008 (HandlePickAndPlaceResult 0x00533780, 0x00533790..0x0053379A): [msg+4] (success) is stored into DockingComponent+5 before the branch on blockStatus, and the attach return is
    /// DISCARDED (0x00533848..0x00533854). A successful BlockPickedUp stores true, a failed one stores false and attaches nothing; with another cube already carried the attach returns 1 and
    /// NOTHING else changes (the earlier cube stays carried, no message, no retry).
    /// </summary>
    [Fact]
    public void M12_008_TheSuccessByteIsStoredBeforeTheBranchAndTheAttachReturnIsDiscarded()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(150, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var marker = obj.Markers.First(k => k.Code == MarkerType.LightCubeI_Front);
        Assert.False(rig.M.Docking.DockingSuccessByte);
        var ok = rig.M.Docking.DockAsync(obj, marker, DockAction.PickupLow, PathMotionProfile.Default, timeout: TimeSpan.FromSeconds(5));
        Spin(ok, rig, ms: 60000);
        Assert.True(ok.IsCompleted);
        Assert.True(rig.M.Docking.DockingSuccessByte);
        Assert.True(rig.M.Docking.Carrying.IsCarrying(7));
        rig.M.Docking.ReleaseCarriedObject();
        rig.DockSucceeds = false;
        var bad = rig.M.Docking.DockAsync(obj, marker, DockAction.PickupLow, PathMotionProfile.Default, timeout: TimeSpan.FromSeconds(5));
        Spin(bad, rig, ms: 60000);
        Assert.True(bad.IsCompleted);
        Assert.False(rig.M.Docking.DockingSuccessByte);
        Assert.False(rig.M.Docking.Carrying.IsCarryingObject);                  // success false: no attach
    }

    // ------------------------------------------------------------------ M12-025 / M12-028: the legacy PlaceRelObjectAction(m, id, onTop)

    /// <summary>
    /// M12-025 / M12-028 (the legacy constructor, whose engine arguments' producers are unread): a legacy action is built with A = B = 0, so +0xB9 stays set (|A|, |B| &lt; 1e-5 skips the store,
    /// 0x00554C86..0x00554CDE) and +0xBB stays 0 (byte 7 = 0), whatever Offsets a behaviour sets: the Offsets are NOT A and B (OffsetB stays 0, CheckPreActionPose stays true, no
    /// WillNotCheckPreDockPoses log). onTop false is +0xA8 set: DockAction 3 (PlaceLow); onTop true is +0xA8 clear: DockAction 2 (PlaceHigh) when CanStackOnTopOfObject holds, else 0x03000004
    /// and nothing is sent (SelectDockAction 0x00555434, M12-017). The error signal's y carries the raw Offsets y: the signal with Offsets (0, 30, 0) is 30 mm more than with (0, 0, 0).
    /// </summary>
    [Fact]
    public void M12_025_TheLegacyConstructorKeepsThePreActionCheckAndTheDefaultDockingMethod()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        rig.M.Docking.Carrying.SetCarrying(8);
        var legacy = new PlaceRelObjectAction(rig.M, 7, onTop: true) { Offsets = (0, 30, 0) };
        Assert.True(legacy.CheckPreActionPose);
        Assert.Equal(0.0, legacy.OffsetA); Assert.Equal(0.0, legacy.OffsetB);
        Assert.DoesNotContain(legacy.Trace, l => l.Contains("WillNotCheckPreDockPoses"));
        Assert.Equal((0.0, 30.0, 0.0), legacy.Offsets);
        legacy.CheckPreActionPose = false;                                       // as DockHelper does
        var t = legacy.RunAsync(default); Spin(t, rig, () => rig.Frame());
        var b = OnlyDock(rig);
        Assert.Equal((byte)DockAction.PlaceHigh, b[17]);
        Assert.Equal(0, b[18]); Assert.Equal(0, b[20]);
        Assert.Equal(0, legacy.Field0xBB);
        double yWith = rig.Sent.OfType<DockingErrorSignal>().Last().YDist;

        rig.M.Docking.Carrying.SetCarrying(8);
        var zero = new PlaceRelObjectAction(rig.M, 7, onTop: true) { CheckPreActionPose = false };
        t = zero.RunAsync(default); Spin(t, rig, () => rig.Frame());
        double yWithout = rig.Sent.OfType<DockingErrorSignal>().Last().YDist;
        Assert.Equal(30.0, yWith - yWithout, 0);

        rig.M.Docking.Carrying.SetCarrying(8);
        var low = new PlaceRelObjectAction(rig.M, 7, onTop: false) { CheckPreActionPose = false };
        t = low.RunAsync(default); Spin(t, rig, () => rig.Frame());
        Assert.Equal((byte)DockAction.PlaceLow, OnlyDock(rig)[17]);

        // onTop true against a target that cannot be stacked on: 0x03000004 and no dock is sent
        using var rig2 = new Rig();
        rig2.Cube = new Pose3d(Mat3.Identity, new Vec3(120, 0, 66)); rig2.Head = 0.1f;
        rig2.Frame();
        rig2.M.Docking.Carrying.SetCarrying(8);
        var refused = new PlaceRelObjectAction(rig2.M, 7, onTop: true) { CheckPreActionPose = false };
        Assert.Equal(0x03000004u, (uint)refused.RunAsync(default).GetAwaiter().GetResult());
        Assert.Empty(rig2.Pump().OfType<DockWithObject>());
    }

    // ------------------------------------------------------------------ M12-037 / M12-017

    /// <summary>
    /// M12-037, GetObservedMarkers 0x00876C50: sinceTime == 0 is an empty vector; otherwise the markers of the list (in list order) observed at or after sinceTime. The object's last observation saw
    /// {Right, Front}: with sinceTime = its own last-observed time the answer is [Front, Right] (the list order Front, Back, Left, Right, Top, Bottom); one millisecond later is empty; zero is empty.
    /// </summary>
    [Fact]
    public void M12_037_GetObservedMarkersFollowsTheListOrderAndTheSinceTime()
    {
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22));
        cube.LastObservedTimestamp = 5000;
        cube.LastObservedMarkers = new[] { MarkerType.LightCubeI_Right, MarkerType.LightCubeI_Front };
        Assert.Equal(new[] { MarkerType.LightCubeI_Front, MarkerType.LightCubeI_Right }, DockActionBase.GetObservedMarkers(cube, 5000).Select(m => m.Code));
        Assert.Empty(DockActionBase.GetObservedMarkers(cube, 5001));
        Assert.Empty(DockActionBase.GetObservedMarkers(cube, 0));
    }

    /// <summary>
    /// M12-017 / M12-037, the marker choice when [+0xB9] is 0 (0x00551960..0x00551B8C): none observed is 0x0300001D; one observed is that marker; several are compared by the 3-D SQUARED distance of the
    /// marker's pose to Robot::GetPose() with a strict &lt; from FLT_MAX. Cube (200, 0, 22), robot at the origin: the Front marker is at (178, 0, 22), the Back marker at (222, 0, 22), Left at
    /// (200, 22, 22) and Right at (200, -22, 22): Front (d2 = 32168) beats Back (d2 = 49768); Left and Right are equally far (d2 = 40968 each), so the FIRST in the list (Left) wins (strict &lt;).
    /// Every distance NaN chooses nothing (null).
    /// </summary>
    [Fact]
    public void M12_037_TheObservedMarkerNearestTheRobotWinsWithAStrictCompare()
    {
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22));
        cube.LastObservedTimestamp = 5000;
        var trace = new List<string>();
        cube.LastObservedMarkers = Array.Empty<MarkerType>();
        Assert.Equal(0x0300001Du, (uint)DockActionBase.ChooseObservedMarker(cube, At(0, 0), out var none, trace));
        Assert.Null(none);
        cube.LastObservedMarkers = new[] { MarkerType.LightCubeI_Back };
        Assert.Equal(ActionResult.Success, DockActionBase.ChooseObservedMarker(cube, At(0, 0), out var single, trace));
        Assert.Equal(MarkerType.LightCubeI_Back, single!.Code);
        cube.LastObservedMarkers = new[] { MarkerType.LightCubeI_Back, MarkerType.LightCubeI_Front };
        DockActionBase.ChooseObservedMarker(cube, At(0, 0), out var front, trace);
        Assert.Equal(MarkerType.LightCubeI_Front, front!.Code);
        cube.LastObservedMarkers = new[] { MarkerType.LightCubeI_Right, MarkerType.LightCubeI_Left };
        DockActionBase.ChooseObservedMarker(cube, At(0, 0), out var tie, trace);
        Assert.Equal(MarkerType.LightCubeI_Left, tie!.Code);
        cube.LastObservedMarkers = new[] { MarkerType.LightCubeI_Left, MarkerType.LightCubeI_Right };
        Assert.Equal(ActionResult.Success, DockActionBase.ChooseObservedMarker(cube, At(double.NaN, 0), out var nan, trace));
        Assert.Null(nan);
    }

    private sealed class RecordingSubActions : IDockSubActionExecutor
    {
        public readonly List<DockSubAction> Ran = new();
        public ActionResult Result = ActionResult.Success;
        public ActionResult ResultOfTurn = ActionResult.Success;
        public Task<ActionResult> RunAsync(DockSubAction action, List<string> trace, CancellationToken cancel)
        {
            Ran.Add(action);
            return Task.FromResult(action is TurnTowardsObject ? ResultOfTurn : Result);
        }
    }

    /// <summary>
    /// M12-037, SetupTurnAndVerifyAction 0x00551F9C: the compound is [VisuallyVerifyNoObjectAtPoseAction, TurnTowardsObjectAction] when [+0xC0] and [+0xC8] are set (the IDockAction defaults 1);
    /// the verify action's pose is the target's pose with the translation raised by dz (44) to (x, y, z + 44), half size 0.5 * (44, 44, 44) = (22, 22, 22), the target's id ignored; the turn action
    /// is for the target with the code [+0x82] (the chosen marker's code: the Front marker, 6, the only one observed) or ANY_CODE when [+0xF7] is set. PlaceRelObjectAction stores [+0xC0] = 0
    /// (M12-025), so its compound is the turn action alone. The verify action runs first.
    /// </summary>
    [Fact]
    public async Task M12_037_TheCompoundIsVerifyThenTurnWithTheRecordedArguments()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        var cube = Assert.Single(rig.Frame().Objects).Object;
        Assert.Equal(new[] { MarkerType.LightCubeI_Front }, cube.LastObservedMarkers);
        var exec = new RecordingSubActions();
        var pick = new PickupObjectAction(rig.M, 7) { CheckPreActionPose = false, SubActions = exec };
        var now = cube.Pose.Translation;                                          // the pose the compound is built from (the pick-up then moves the cube onto the lift)
        var t = pick.RunAsync(default); Spin(t, rig, () => rig.Frame());
        Assert.Equal(new[] { typeof(VisuallyVerifyNoObjectAtPose), typeof(TurnTowardsObject) }, exec.Ran.Select(a => a.GetType()));
        var verify = (VisuallyVerifyNoObjectAtPose)exec.Ran[0];
        Assert.Equal(now.X, verify.Pose.Translation.X, 6); Assert.Equal(now.Y, verify.Pose.Translation.Y, 6); Assert.Equal(now.Z + 44.0, verify.Pose.Translation.Z, 6);
        Assert.Equal(new Vec3(22, 22, 22), verify.HalfSize);
        Assert.Equal(new uint[] { 7 }, verify.IgnoreIds);
        var turn = (TurnTowardsObject)exec.Ran[1];
        Assert.Equal(7u, turn.ObjectId);
        Assert.Equal(new DockTurnCode(6), turn.Code); Assert.Equal((short)6, pick.Field0x82); Assert.Equal((short)3, pick.Field0x84);
        Assert.True(turn.Field0x56 && verify.Field0x56 && pick.TurnAndVerify!.Field0x56);
        Assert.Equal(0.0, turn.RadiansArgument); Assert.True(turn.BoolArgument5); Assert.False(turn.BoolArgument6);

        rig.M.Docking.Carrying.UnsetCarrying();
        var any = new PickupObjectAction(rig.M, 7) { CheckPreActionPose = false, SubActions = new RecordingSubActions(), Field0xF7 = true };
        t = any.RunAsync(default); Spin(t, rig, () => rig.Frame());
        Assert.True(((TurnTowardsObject)any.TurnAndVerify!.Actions[1]).Code.IsAny);

        rig.M.Docking.Carrying.SetCarrying(8);
        var placeExec = new RecordingSubActions();
        var place = new PlaceRelObjectAction(rig.M, 7, flagAt0xA8: true, a: 0, b: 0, dockActionFlagAt0x95: false, skipOffsetTransform: true) { CheckPreActionPose = false, SubActions = placeExec };
        t = place.RunAsync(default); Spin(t, rig, () => rig.Frame());
        Assert.Equal(new[] { typeof(TurnTowardsObject) }, placeExec.Ran.Select(a => a.GetType()));
        await Task.CompletedTask;
    }

    /// <summary>
    /// M12-017 steps 13 and 14 (0x00551C0C..0x00551C4C): InitInternal runs BEFORE the compound's Update, so an InitInternal error is returned with the compound never run (a place with A = 17 gives
    /// A' = -17 &lt; -16.000009: 0x03000000); then the compound runs and a result other than {0, RUNNING} is returned with nothing docked (a turn result 0x04000000 comes back as it is);
    /// with success the dock is sent. The observed-marker choice fails first with 0x0300001D when nothing was observed (0x00551A30..0x00551A82), before InitInternal and the compound.
    /// </summary>
    [Fact]
    public async Task M12_017_InitInternalThenTheCompoundThenTheDock()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        rig.M.Docking.Carrying.SetCarrying(8);
        var neverRun = new RecordingSubActions();
        var bad = new PlaceRelObjectAction(rig.M, 7, flagAt0xA8: true, a: 17.0, b: 5.0, dockActionFlagAt0x95: false, skipOffsetTransform: false) { CheckPreActionPose = false, SubActions = neverRun };
        Assert.Equal(0x03000000u, (uint)await bad.RunAsync(default));
        Assert.Empty(neverRun.Ran);
        rig.M.Docking.Carrying.UnsetCarrying();
        var failing = new RecordingSubActions { ResultOfTurn = ActionResult.Retry };
        var pick = new PickupObjectAction(rig.M, 7) { CheckPreActionPose = false, SubActions = failing };
        Assert.Equal(ActionResult.Retry, await pick.RunAsync(default));
        Assert.Equal(2, failing.Ran.Count);
        Assert.Empty(rig.Pump().OfType<DockWithObject>());
        rig.M.World.GetObjectById(7)!.LastObservedMarkers = Array.Empty<MarkerType>();
        var none = new RecordingSubActions();
        Assert.Equal(0x0300001Du, (uint)await new PickupObjectAction(rig.M, 7) { CheckPreActionPose = false, SubActions = none }.RunAsync(default));
        Assert.Empty(none.Ran);
    }

    /// <summary>M12-037 (labelled stand-in): with the default sub-action executor the verify-no-object action is a COUNTED no-op stub, so the gap shows in the trace and the counter.</summary>
    [Fact]
    public void M12_037_TheDefaultVerifyNoObjectStubIsCountedAndTraced()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(150, 0);
        Assert.Single(rig.Frame().Objects);
        var pick = new PickupObjectAction(rig.M, 7) { CheckPreActionPose = false };
        var t = pick.RunAsync(default); Spin(t, rig, () => rig.Frame());
        Assert.Equal(1, ((StandInDockSubActions)pick.SubActions).UnreadVerifyCalls);
        Assert.Contains(pick.Trace, l => l.Contains("VisuallyVerifyNoObjectAtPoseAction body unread (M8)"));
    }

    // ------------------------------------------------------------------ M12-030

    /// <summary>
    /// M12-030, Init 0x00559DB8: nothing carried is 0x03000011; the constructor's object missing from the world is 0x03000004; +0x178 needs ComputePlacementApproachAngle 0x005504A0 whose body is not
    /// in the inventory (only its angle helper is), so it is a visible NotSupportedException. Clone (thunk 0x004E3954 -&gt; 0x004E38D4, 0x0087660E, 0x004E0C4C..0x004E0C58) keeps NOTHING: the clone's
    /// ObjectID is -1 (uint.MaxValue), so the inner GetPreActionPoses does NOT answer 0x03000004 for it (0x00550904 compares -1 with the carried id): the chain goes on to poses and a drive.
    /// </summary>
    [Fact]
    public async Task M12_030_TheCloneKeepsNothingSoTheInnerChainDrivesInsteadOfAnswering0x03000004()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        var notCarrying = new DriveToPlaceCarriedObjectAction(rig.M, At(300, 0, 22), true, false, false, false, 0.0);
        Assert.Equal(0x03000011u, (uint)await notCarrying.RunAsync(default));
        rig.M.Docking.Carrying.SetCarrying(7);
        var placement = At(300, 100, 22, 0.4);
        var act = new DriveToPlaceCarriedObjectAction(rig.M, placement, true, false, false, false, 0.0);
        var t = act.RunAsync(default); Spin(t, rig);
        Assert.True(t.IsCompleted);
        Assert.NotEqual(0x03000004u, (uint)t.Result);
        Assert.NotNull(act.Clone);
        Assert.Equal(uint.MaxValue, act.Clone!.ObjectId);                        // ObjectID -1
        Assert.Equal(PoseState.Known, act.Clone.PoseState);
        Assert.Equal(300.0, act.Clone.Pose.Translation.X, 9); Assert.Equal(100.0, act.Clone.Pose.Translation.Y, 9);
        Assert.Contains(rig.Sent, m => m is ExecutePath);                        // the inner InitHelper found poses and drove
        var approach = new DriveToPlaceCarriedObjectAction(rig.M, placement, true, true, false, false, 0.0);
        await Assert.ThrowsAsync<NotSupportedException>(() => approach.RunAsync(default));
        rig.M.Docking.Carrying.UnsetCarrying();
        var gone = new DriveToPlaceCarriedObjectAction(rig.M, placement, true, false, false, false, 0.0);      // built with nothing carried: the invalid id
        rig.M.Docking.Carrying.SetCarrying(7);
        Assert.Equal(0x03000004u, (uint)await gone.RunAsync(default));         // the constructor's id is not in the world
    }

    /// <summary>
    /// M12-030, CheckIfDone 0x00559FA4: the compound's result is replaced by 0x04000008 when +0x179 is set and the goal is not free; with +0x179 clear the query is not called; the query is given the
    /// carried object, the placement pose and the padding [+0x17C]. IsPlacementGoalFree 0x0055A070 returns true before any query when the carried object is not found.
    /// </summary>
    [Fact]
    public void M12_030_CheckIfDoneReportsPlacementGoalNotFree()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        rig.M.Docking.Carrying.SetCarrying(7);
        int calls = 0; bool free = false; double padding = -1; Pose3d seen = default;
        var act = new DriveToPlaceCarriedObjectAction(rig.M, At(300, 100, 22), true, false, false, true, 12.5)
        { FindLocatedIntersectingObjectsIsEmpty = (o, pose, pad) => { calls++; padding = pad; seen = pose; return free; } };
        Assert.Equal(0x04000008u, (uint)act.CheckIfDone(ActionResult.Running));
        Assert.Equal(12.5, padding); Assert.Equal(300.0, seen.Translation.X);
        free = true;
        Assert.Equal(ActionResult.Retry, act.CheckIfDone(ActionResult.Retry));       // the compound's own result passes through
        var off = new DriveToPlaceCarriedObjectAction(rig.M, At(300, 100, 22), true, false, false, false, 0.0)
        { FindLocatedIntersectingObjectsIsEmpty = (_, _, _) => { calls++; return false; } };
        int before = calls;
        Assert.Equal(ActionResult.Running, off.CheckIfDone(ActionResult.Running));
        Assert.Equal(before, calls);
        rig.M.Docking.Carrying.UnsetCarrying();
        int callsBefore = calls;
        Assert.True(act.IsPlacementGoalFree());                                      // nothing carried: true, the query is not called
        Assert.Equal(callsBefore, calls);
    }

    /// <summary>
    /// M12-030, FindLocatedIntersectingObjects 0x00626870 / predicate 0x0062750E (Q5): the reference quad is the carried cube's footprint at the placement pose (200, -60) with padding 0 (x 178..222,
    /// y -82..-38); the query is <c>q = o-&gt;vtbl[0x50](o, o-&gt;GetPose(), padding); q.Intersects(ref)</c>, so the PADDING inflates the CANDIDATE: a cube 8 at (200, -5) (y -27..17, an 11 mm gap) does not
    /// intersect with padding 0 (free) and does with padding 20 (its half extent becomes 42, y -47..37; the overlap with -82..-38 is 9 mm). A cube 8 at (200, -25) (y -47..-3, overlapping by 9 mm)
    /// blocks with padding 0. There is NO Z test: the same overlap at a height of 200 still blocks. The carried cube itself is ignored (id 7) though it is located.
    /// </summary>
    [Fact]
    public void M12_030_IsPlacementGoalFreeIntersectsThePaddedCandidateQuadWithoutAZTest()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 60);
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.Identity, new Vec3(200, -5, 22))));
        rig.Frame(); rig.Frame();
        var carried = rig.M.World.GetLocatedObjectById(7)!;
        var other = rig.M.World.GetLocatedObjectById(8)!;
        Assert.InRange(other.Pose.Translation.Y, -8.0, -2.0);                       // the vision estimate is within about a millimetre; the margins below are 9 mm and more
        rig.M.Docking.Carrying.SetCarrying(7);
        var placement = At(200, -60, 22);
        DriveToPlaceCarriedObjectAction Make(double padding) => new(rig.M, placement, true, false, false, true, padding);
        Assert.True(Make(0).IsPlacementGoalFree());
        Assert.False(Make(20).IsPlacementGoalFree());
        other.Pose = new Pose3d(other.Pose.Rotation, new Vec3(200, -25, 22));
        Assert.False(Make(0).IsPlacementGoalFree());
        other.Pose = new Pose3d(other.Pose.Rotation, new Vec3(200, -25, 200));
        Assert.False(Make(0).IsPlacementGoalFree());                           // no Z test
        other.Pose = new Pose3d(other.Pose.Rotation, new Vec3(200, -5, 22));
        other.PoseState = PoseState.Unknown;                                    // not located: not visited
        Assert.True(Make(20).IsPlacementGoalFree());
        // M13-023 labelled stand-in: a tilted candidate is decided by the planar 22 mm rule (centre 10 mm from the placement centre: blocks; 40 mm: clear)
        other.PoseState = PoseState.Known;
        other.Pose = new Pose3d(Mat3.AboutY(0.2), new Vec3(200, -70, 22));
        var act = Make(0);
        Assert.False(act.IsPlacementGoalFree());
        Assert.Contains(act.Trace, l => l.Contains("M13-023 planar stand-in"));
        other.Pose = new Pose3d(Mat3.AboutY(0.2), new Vec3(200, -100, 22));
        Assert.True(Make(0).IsPlacementGoalFree());
    }

    /// <summary>
    /// M12-030, the approach-angle helper 0x00550858: axis = GetRotatedParentAxis&lt;'Z'&gt;(R) over v = (R(2,0), R(2,1), R(2,2)): +-1 X, +-2 Y, +-3 Z by the dominant component with strict &gt; (ties
    /// prefer X, then Y, over Z), the sign + only when the dominant component is strictly &gt; 0; then tbb(axis + 3): -3 -Z, -2 -Y, -1 -X, +1 +X, +2 +Y, +3 +Z. The Z arms use
    /// GetAngleAroundZaxis (M12-033); X and Y arms call the (unread, MISSING) functions given. R = Rz(0.5): row 2 = (0, 0, 1): +3, the angle 0.5. R = Rx(pi) Rz(0.5) = [[c,-s,0],[-s,-c,0],[0,0,-1]]:
    /// row 2 = (0, 0, -1): -3, and GetAngleAroundZaxis takes the second branch (R10^2 + R00^2 = 1 is not &gt; R01^2 + R11^2 = 1): atan2(s, -c) = pi - 0.5, so -(pi - 0.5) = -2.6416.
    /// </summary>
    [Fact]
    public void M12_030_TheApproachAngleHelperPicksTheAxisArm()
    {
        Assert.Equal(0.5, PlacementApproachAngle.Helper(Mat3.AboutZ(0.5)), 9);
        var flipped = Mat3.AboutX(Math.PI) * Mat3.AboutZ(0.5);
        Assert.Equal(-(Math.PI - 0.5), PlacementApproachAngle.Helper(flipped), 6);
        // the axis rule with injected X/Y functions (rows 0 and 1 are irrelevant, only row 2 is read)
        double Arm(double x, double y, double z) => PlacementApproachAngle.Helper(new Mat3(1, 0, 0, 0, 1, 0, x, y, z), _ => 11, _ => 22);
        Assert.Equal(11.0, Arm(0.5, 0.5, 0.5));       // a three-way tie prefers X
        Assert.Equal(-11.0, Arm(-0.6, 0.1, 0.1));     // X dominant, negative: -X
        Assert.Equal(22.0, Arm(0.4, 0.5, 0.5));       // Y beats X strictly; Z does not beat Y (a tie)
        Assert.Equal(-22.0, Arm(0.1, -0.7, 0.2));
        Assert.Equal(11.0, Arm(0.4, 0.4, 0.0));       // (0.4, 0.4, 0): X (a tie with Y), positive
        Assert.Equal(-11.0, Arm(0, 0, 0));            // a zero component is not strictly > 0: the negative sign
        // the X and Y arms are visible stubs until Rotation3d::GetAngleAroundXaxis / Yaxis are read
        Assert.Throws<NotSupportedException>(() => PlacementApproachAngle.Helper(new Mat3(1, 0, 0, 0, 1, 0, 1, 0, 0)));
        Assert.Throws<NotSupportedException>(() => PlacementApproachAngle.Helper(new Mat3(1, 0, 0, 0, 1, 0, 0, 1, 0)));
    }

    // ------------------------------------------------------------------ M12-035

    private static (Rig Rig, ObservableObject Cube) FlipRig(float robotY)
    {
        var rig = new Rig();
        rig.X = 0; rig.Y = robotY; rig.Angle = 0; rig.State();
        return (rig, CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22)));
    }

    /// <summary>
    /// M12-035, DriveAndFlipBlockAction::GetPossiblePoses 0x0055E438 (Q3.6). The request is {obj, 5, flag A 0, 0x3DB2B8C2, 0, no approach angle}. For a cube at (200, 0, 22) the four valid Flipping poses are
    /// Front (121.423, -78.577), Back (278.577, 78.577), Left (121.423, 78.577), Right (278.577, -78.577) (stored translation (-78.577, -78.577, -22) etc., a = 0 so b = 0, R22 = 1). A robot at
    /// (0, -10): robot-relative distances Front 139.4, Left 150.3, Back 292.3, Right 286.9, so A = Front, B = Left; with no face A.y = -68.6 &gt;= B.y = 88.6 is false: B = Left. A robot at (0, 10):
    /// A = Left (139.4), B = Front (150.3): A.y = 68.6 &gt;= -88.6: A = Left. At (0, 0) Front and Left tie (144.63): whichever the strict &lt; makes A, the y rule gives Left both ways.
    /// closestOnly pushes the WORLD pose of poses[closestIndex], the PLANAR closest (M12-031): Front from (0, -10), Left from (0, 10).
    /// </summary>
    [Fact]
    public void M12_035_GetPossiblePosesKeepsTheNearestTwoAndTheYRule()
    {
        foreach (var (robotY, closest, chosen) in new (float, BlockFace?, BlockFace)[] { (-10f, BlockFace.Front, BlockFace.Left), (10f, BlockFace.Left, BlockFace.Left), (0f, null, BlockFace.Left) })
        {
            var (rig, cube) = FlipRig(robotY);
            using (rig)
            {
                Assert.Equal(ActionResult.Success, DriveAndFlipBlockAction.GetPossiblePoses(rig.M, cube, out var poses, closestOnly: false));
                var pose = Assert.Single(poses);
                Assert.Equal(chosen, pose.Marker.Face);
                Assert.Equal(PreActionType.Flipping, pose.Type);
                Assert.Equal(121.423, pose.WorldPose.Translation.X, 3);
                Assert.Equal(chosen == BlockFace.Left ? 78.577 : -78.577, pose.WorldPose.Translation.Y, 3);
                Assert.Equal(ActionResult.Success, DriveAndFlipBlockAction.GetPossiblePoses(rig.M, cube, out var one, closestOnly: true));
                if (closest is { } expectedClosest) Assert.Equal(expectedClosest, Assert.Single(one).Marker.Face);
            }
        }
    }

    /// <summary>
    /// M12-035, the face rule (Q3.6 (6)): with a known face and both poses expressed against it the answer is A when dist(A, face) &gt; dist(B, face), else B. Robot (0, -10): A = Front, B = Left. A NAMED
    /// face at (130, 80, 50) is 50.75 from Left and 166.49 from Front: dist(A) &gt; dist(B), so A = Front (the y rule alone would say Left); one at (130, -80, 50) is 50.75 from Front and 166.49 from Left: B = Left.
    /// </summary>
    [Fact]
    public void M12_035_AKnownFaceDecidesBetweenTheTwoNearestPoses()
    {
        var (rig, cube) = FlipRig(-10f);
        using (rig)
        {
            rig.M.Vision.Faces.AddOrUpdateFace(new TrackedFace(new DetectedFace(3, new FaceRect(0, 0, 10, 10), Name: "Bob"), 1000) { HeadPose = new Pose3d(Mat3.Identity, new Vec3(130, 80, 50)) }, Pose3d.Identity, false);
            DriveAndFlipBlockAction.GetPossiblePoses(rig.M, cube, out var nearLeft, closestOnly: false);
            Assert.Equal(BlockFace.Front, Assert.Single(nearLeft).Marker.Face);
            rig.M.Vision.Faces.AddOrUpdateFace(new TrackedFace(new DetectedFace(4, new FaceRect(0, 0, 10, 10), Name: "Amy"), 2000) { HeadPose = new Pose3d(Mat3.Identity, new Vec3(130, -80, 50)) }, Pose3d.Identity, false);
            DriveAndFlipBlockAction.GetPossiblePoses(rig.M, cube, out var nearFront, closestOnly: false);
            Assert.Equal(BlockFace.Left, Assert.Single(nearFront).Marker.Face);
        }
    }

    /// <summary>
    /// M12-035, the exits: a non-zero GetPreActionPoses result is returned (a carried cube 0x03000004; a cube pitched 35 degrees, whose poses all fall outside the 30 degree cone, is the
    /// 0x03000010 that GetPreActionPoses itself gives when no pose is left, which is the list-empty exit 0x03000010 too).
    /// </summary>
    [Fact]
    public void M12_035_GetPossiblePosesReturnsTheNonZeroResults()
    {
        var (rig, cube) = FlipRig(0f);
        using (rig)
        {
            var tilted = CubeWith(8, ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.AboutY(35 * Math.PI / 180), new Vec3(200, 0, 22)));
            Assert.Equal(0x03000010u, (uint)DriveAndFlipBlockAction.GetPossiblePoses(rig.M, tilted, out var none, closestOnly: false));
            Assert.Empty(none);
            rig.M.Docking.Carrying.SetCarrying(7);
            Assert.Equal(0x03000004u, (uint)DriveAndFlipBlockAction.GetPossiblePoses(rig.M, cube, out _, closestOnly: true));
        }
    }

    /// <summary>
    /// M12-035, L1 (0x0055F366) and its helper (0x0055F3F8): the function ends with GetPossiblePoses(closestOnly 0); the helper block runs only when *inPos is 0 and [this+0x100] &gt;= 0, and over the
    /// EMPTY vector the helper returns 0, leaving *inPos 0 and flip+0x140 = 1. [this+0x100] is indeterminate in the engine (never written) and NOT invented here (null): both readings leave
    /// +0x140 = 1, the FlipBlockAction constructor default. The helper is false for a negative threshold and for an empty vector; a non-empty one needs ComputeDistanceSQBetween (MISSING) and throws.
    /// L2 (ShouldDriveToClosestPreActionPose(b), 0x0055E3B0) is the same with closestOnly = b.
    /// </summary>
    [Fact]
    public void M12_035_TheInstalledFunctionLeavesTheFlipFlagAtOneAndSelectsClosestOnlyForL2()
    {
        var (rig, cube) = FlipRig(-10f);
        using (rig)
        {
            var act = new DriveAndFlipBlockAction(rig.M, 7);
            Assert.Null(act.Field0x100);
            Assert.True(act.Flip.CheckPreActionPose);                             // +0x140 = 1 from FlipBlockAction's constructor
            bool inPos = false;
            Assert.Equal(ActionResult.Success, act.PosesFunctionL1L2(cube, out var l1, ref inPos));
            Assert.False(inPos); Assert.True(act.Flip.CheckPreActionPose);
            Assert.Equal(BlockFace.Left, Assert.Single(l1).Marker.Face);            // the nearest-two rule (see the GetPossiblePoses test)
            act.Field0x100 = 5f;                                                     // a value only a test can supply: the helper block runs over the empty vector
            Assert.Equal(ActionResult.Success, act.PosesFunctionL1L2(cube, out _, ref inPos));
            Assert.False(inPos); Assert.True(act.Flip.CheckPreActionPose);          // helper 0 -> *inPos = 0, +0x140 = !0 = 1
            act.Flip.CheckPreActionPose = false;
            Assert.Equal(ActionResult.Success, act.PosesFunctionL1L2(cube, out _, ref inPos));
            Assert.True(act.Flip.CheckPreActionPose);                                // the block wrote +0x140 = 1
            inPos = true; act.Flip.CheckPreActionPose = false;
            act.PosesFunctionL1L2(cube, out _, ref inPos);
            Assert.False(act.Flip.CheckPreActionPose); Assert.True(inPos);           // *inPos already set: the block is skipped, GetPossiblePoses never touches the flag
            act.ShouldDriveToClosestPreActionPose(true);
            inPos = false;
            act.PosesFunctionL1L2(cube, out var l2, ref inPos);
            Assert.Equal(BlockFace.Front, Assert.Single(l2).Marker.Face);            // closestOnly: poses[closestIndex] (planar closest from (0, -10) is Front)
            Assert.False(DriveAndFlipBlockAction.PoseNearerThan(At(0, 0), Array.Empty<Pose3d>(), 5f));
            Assert.False(DriveAndFlipBlockAction.PoseNearerThan(At(0, 0), new[] { At(1, 0) }, -1f));
            Assert.Throws<NotSupportedException>(() => DriveAndFlipBlockAction.PoseNearerThan(At(0, 0), new[] { At(1, 0) }, 5f));
        }
    }

    /// <summary>
    /// M12-035, DriveToFlipBlockPoseAction ctor 0x0055EB04 (ActionType 5, [this+0x44] = 9) installs L3 (0x0055EB74: GetPossiblePoses with closestOnly 0, the in-position flag untouched);
    /// ShouldDriveToClosestPreActionPose(b) 0x0055EC04 installs L4 (closestOnly = b). Robot (0, -10): L3 gives the nearest-two answer (Left), L4 with true the planar closest (Front), L4 with false Left.
    /// </summary>
    [Fact]
    public void M12_035_TheDriveToFlipBlockPoseInstallersL3AndL4()
    {
        var (rig, cube) = FlipRig(-10f);
        using (rig)
        {
            var act = new DriveToFlipBlockPoseAction(rig.M, 7);
            Assert.Equal(9, act.Field0x44);
            Assert.Equal(PreActionType.Flipping, act.Drive.ActionType);
            bool inPos = true;
            Assert.Equal(ActionResult.Success, act.Drive.PosesFunction(cube, out var l3, ref inPos));
            Assert.True(inPos);
            Assert.Equal(BlockFace.Left, Assert.Single(l3).Marker.Face);
            act.ShouldDriveToClosestPreActionPose(true);
            act.Drive.PosesFunction(cube, out var l4, ref inPos);
            Assert.Equal(BlockFace.Front, Assert.Single(l4).Marker.Face);
            act.ShouldDriveToClosestPreActionPose(false);
            act.Drive.PosesFunction(cube, out var l4b, ref inPos);
            Assert.Equal(BlockFace.Left, Assert.Single(l4b).Marker.Face);
        }
    }

    /// <summary>
    /// M12-035 / M12-011, DriveToObjectAction::CheckIfDone 0x00559A86..0x00559AE6: the +0x150 function gets a FRESH EMPTY vector and an in-position bool of 0 (0x00559A86 movs r0,#0;
    /// 0x00559A8E strb.w r0,[sp,#0x10]); flag NON-ZERO returns the function's result, flag ZERO returns 0x04000001 (0x00559AE4). L1..L4 never write the bool, so a flip drive ends 0x04000001
    /// even though GetPossiblePoses succeeds; DriveAndFlipBlockAction ignores that failure (the outer compound's AddAction(inner, true, false), 0x0055B370) and runs the flip.
    /// </summary>
    [Fact]
    public void M12_035_TheFlipInstallersLeaveTheFlagZeroSoCheckIfDoneEnds0x04000001()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        // the default function DOES set the flag (M12-029: GetPossiblePoses fills the empty vector and the in-position byte): a robot in position gets the function's result
        rig.X = 60; rig.Angle = 0; rig.State();
        var docking = new DriveToObjectAction(rig.M, 7, PreActionType.Docking);
        Assert.Equal(ActionResult.Success, docking.CheckIfDone());
        rig.X = -20; rig.State();
        Assert.Equal(0x04000001u, (uint)docking.CheckIfDone());
        rig.X = 0; rig.State();
        var act = new DriveToFlipBlockPoseAction(rig.M, 7);
        Assert.Equal(ActionResult.Success, DriveAndFlipBlockAction.GetPossiblePoses(rig.M, rig.M.World.GetLocatedObjectById(7)!, out var poses, false));
        Assert.NotEmpty(poses);                                                     // the function itself succeeds ...
        Assert.Equal(0x04000001u, (uint)act.Drive.CheckIfDone());                   // ... and the check still ends 0x04000001
        act.ShouldDriveToClosestPreActionPose(true);
        Assert.Equal(0x04000001u, (uint)act.Drive.CheckIfDone());
        var flip = new DriveAndFlipBlockAction(rig.M, 7);
        rig.State((uint)(RobotStatusFlag.LiftInPos | RobotStatusFlag.HeadInPos));
        var t = flip.RunAsync(default); Spin(t, rig, () => rig.State((uint)(RobotStatusFlag.LiftInPos | RobotStatusFlag.HeadInPos)));
        // IDriveToInteractWithObject 0x0055B370 AddAction(inner, ignoreFailure = 1): the outer compound ignores the failed drive and goes on to the flip; the action's result is the flip's
        Assert.NotEqual(0x04000001u, (uint)t.Result);
        Assert.Contains(flip.Trace, l => l.Contains("FlipBlockAction.CheckIfDone: compound result"));   // the flip ran to its CheckIfDone (0x0055F074)
        Assert.Contains(flip.Trace, l => l.Contains("ignored by the outer compound"));
    }

    // ================================================================== R-VIS M12 fix round 3

    /// <summary>
    /// M12-036 (fix-round-2 verifier, 0x004DF45C..0x004DF48C: vdiv #10, blx floorf, vcvt.s32.f32): n = (int)floorf((L + 55.9)/10). The Front pose sweep has L = 137.711, n = floor(19.361) = 19, samples
    /// x = -15.611 + 10 i, i = 0..18, the last 164.389. A cube at (190, 40) (x 168..212, y 18..62) is hit only through a sample at x >= 168 with the right sample y = +27.1: the
    /// sample i = 19 (174.389) would exist for n = 20 (ceil) but not for n = 19, so with floor the pose is VALID; its centroid (190, 40) is outside the target's own quad (y -22..22), so no exemption.
    /// A cube at (180, 40) (x 158..202) is reached by the sample at 164.389 (i = 18) and is a hit.
    /// </summary>
    [Fact]
    public void M12_036_TheSweepStepCountIsFloorNotCeil()
    {
        var cube = CubeWith(7, ObjectType.Block_LIGHTCUBE1, At(200, 0, 22));
        Assert.True(PreActionValidity.IsPreActionPoseValid(cube, FrontDockingPoseOfTheCubeAt200(cube), new[] { Obstacle(9, 190, 40) }));
        Assert.False(PreActionValidity.IsPreActionPoseValid(cube, FrontDockingPoseOfTheCubeAt200(cube), new[] { Obstacle(9, 180, 40) }));
    }

    /// <summary>
    /// M12-025 / M12-028 (the legacy PlaceRelObjectAction(m, id, onTop) callers keep the pre-batch behaviour where the inventory is silent): a legacy action runs NO SetupTurnAndVerifyAction
    /// compound (TurnAndVerify stays null, the sub-action executor is never asked) and has NO 2 s visible-marker wait, so it docks even when the target's last observation saw no marker
    /// (the pre-batch flow docked on the marker facing the robot); an engine-shaped action still runs the compound (M12_037 tests).
    /// </summary>
    [Fact]
    public void M12_028_TheLegacyPlaceRelKeepsTheEarlierStandInAndNeverWaitsForAVisibleMarker()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        rig.M.World.GetObjectById(7)!.LastObservedMarkers = Array.Empty<MarkerType>();
        rig.M.Docking.Carrying.SetCarrying(8);
        var exec = new RecordingSubActions();
        var legacy = new PlaceRelObjectAction(rig.M, 7, onTop: false) { CheckPreActionPose = false, SubActions = exec };
        var t = legacy.RunAsync(default); Spin(t, rig, () => rig.Frame());
        Assert.Empty(exec.Ran);
        Assert.Null(legacy.TurnAndVerify);
        Assert.Contains(rig.Sent, m => m is DockWithObject);
        Assert.NotEqual(0x0300001Du, (uint)t.Result);
    }

    private static uint IdleFlags => (uint)(RobotStatusFlag.LiftInPos | RobotStatusFlag.HeadInPos);

    /// <summary>
    /// M13-028, FlipBlockAction::Init 0x0055EED6..0x0055EEFA: the drive distance is the THREE-D norm of the object pose with respect to the robot pose plus [this+0x130] (20). A cube at z 22 and
    /// 200 mm gives sqrt(200^2 + 22^2) = 201.2 mm, so the line ends at 221.2 (the planar norm would give 220). The expectation is computed from the world's observed pose (an input). Init's
    /// DisableReactionsWithLock (0x0055EEC6) and IActionRunner::Update (0x0055EF7E) are M8's: visible as a trace line and a counter, not modelled.
    /// </summary>
    [Fact]
    public void M13_028_TheFlipDriveDistanceIsTheThreeDimensionalNorm()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var t = obj.Pose.Translation;
        double expected = Math.Sqrt(t.X * t.X + t.Y * t.Y + t.Z * t.Z) + 20.0;
        double planar = Math.Sqrt(t.X * t.X + t.Y * t.Y) + 20.0;
        Assert.True(expected - planar > 0.5);                                       // the two readings are distinguishable for a cube at z 22
        rig.State(IdleFlags);
        var flip = new FlipBlockAction(rig.M, 7) { CheckPreActionPose = false };
        var task = flip.RunAsync(default); Spin(task, rig, () => rig.State(IdleFlags), ms: 60000);
        var line = rig.Sent.OfType<AppendPathSegmentLine>().First();
        Assert.Equal(expected, line.XEndMm, 0);
        Assert.Contains(flip.Trace, l => l.Contains("DisableReactionsWithLock (0x0055EEC6)") && l.Contains("not modelled"));
        Assert.Equal(3, flip.M8CallsNotModelled);                                   // Init: DisableReactionsWithLock, Update; destructor: RemoveDisableReactionsLock (0x0055ED88)
    }

    /// <summary>
    /// M13-028, the embedded CompoundActionSequential at this+0x80 (0x0055EF3A..0x0055EF70): MoveLiftToHeightAction first, DriveStraightAction second, so the drive starts only once the lift move has
    /// completed. The robot reports the lift at 32 mm (not at the 45 mm target): the lift command is sent and, while it is not acknowledged, NO path line is sent; after the ack and a state with the
    /// lift in position at 45 mm (angle 0) the drive line appears.
    /// </summary>
    [Fact]
    public void M13_028_TheFlipDrivesOnlyAfterTheLiftMoveCompletes()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.HoldLift = true;                                                        // the lift move stays RUNNING until the test acknowledges it
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        void LiftState(double liftAngle) => rig.Send(new RobotState
        {
            Timestamp = rig.T += 33, PoseOriginId = rig.OriginId, Pose = new RobotPose { X = rig.X, Y = rig.Y, Angle = rig.Angle }, HeadAngle = rig.Head,
            Status = IdleFlags, LiftAngle = (float)liftAngle, Accel = new AccelData { Z = 9800 }, Gyro = new GyroData(),
        });
        LiftState(Math.Asin(-13.0 / 66));                                           // 32 mm = 66 sin(a) + 45
        var flip = new FlipBlockAction(rig.M, 7) { CheckPreActionPose = false };
        var task = flip.RunAsync(default);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!rig.Sent.OfType<SetLiftHeight>().Any() && sw.ElapsedMilliseconds < 60000) { rig.Pump(); Thread.Sleep(5); }
        var lift = Assert.Single(rig.Sent.OfType<SetLiftHeight>());
        Assert.Equal(45f, lift.HeightMm);
        Assert.Empty(rig.Sent.OfType<AppendPathSegmentLine>());                     // the drive waits for the lift
        Assert.False(task.IsCompleted);
        rig.Send(new MotorActionAck { ActionId = lift.ActionId });
        LiftState(0.0);                                                             // 45 mm, in position
        sw.Restart();
        while (!rig.Sent.OfType<AppendPathSegmentLine>().Any() && sw.ElapsedMilliseconds < 60000) { LiftState(0.0); rig.Pump(); Thread.Sleep(5); }
        Assert.NotEmpty(rig.Sent.OfType<AppendPathSegmentLine>());
        Spin(task, rig, () => LiftState(0.0), ms: 60000);
    }

    /// <summary>
    /// M13-028, IDriveToInteractWithObject ctor 0x0055B258..0x0055B264: when the robot is carrying the object's id it warns and adds NO drive, wait or turn actions (branch to 0x0055B45E);
    /// only the FlipBlockAction the derived constructor added runs, and its Init answers 0x03000004 for a carried object (GetPreActionPoses). And maxTurn &gt; 0 is Radians::operator&gt; with the
    /// 1e-5 epsilon (0x0055B38C): 0.5e-5 is not positive, 2e-5 is, -1 and 0 are not.
    /// </summary>
    [Fact]
    public void M13_028_ACarriedObjectAddsNoDriveOrTurnsAndTheMaxTurnUsesTheEpsilon()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        rig.M.Docking.Carrying.SetCarrying(7);
        var flip = new DriveAndFlipBlockAction(rig.M, 7) { MaxTurnTowardsFaceRad = Math.PI / 2 };
        Assert.Equal(0x03000004u, (uint)flip.RunAsync(default).GetAwaiter().GetResult());
        Assert.Contains(flip.Trace, l => l.Contains("no drive, wait or turn actions are added"));
        Assert.DoesNotContain(flip.Trace, l => l.Contains("TurnTowards"));
        Assert.Empty(rig.Pump().OfType<ExecutePath>());
        Assert.False(DriveAndFlipBlockAction.MaxTurnIsPositive(0.5e-5));
        Assert.True(DriveAndFlipBlockAction.MaxTurnIsPositive(2e-5));
        Assert.False(DriveAndFlipBlockAction.MaxTurnIsPositive(-1.0));
        Assert.False(DriveAndFlipBlockAction.MaxTurnIsPositive(0.0));
    }

    // ================================================================== R-VIS M12 fix round 4 (FlipBlockAction, M13-028)

    private static void LiftReports(Rig rig, double liftAngle) => rig.Send(new RobotState
    {
        Timestamp = rig.T += 33, PoseOriginId = rig.OriginId, Pose = new RobotPose { X = rig.X, Y = rig.Y, Angle = rig.Angle }, HeadAngle = rig.Head,
        Status = IdleFlags, LiftAngle = (float)liftAngle, Accel = new AccelData { Z = 9800 }, Gyro = new GyroData(),
    });

    /// <summary>
    /// Holds the fake robot's lift and path, starts the flip with the cube 200 mm away and takes it to the point where its embedded compound is in the DRIVE: the 45 mm move is acknowledged and
    /// reported in position (the move's end unlocks the lift track, UnlockTracks 0x005408EC) and the ExecutePath is on the wire but not followed, so the DriveStraightAction is RUNNING.
    /// </summary>
    private static Task<ActionResult> StartFlipAtTheDrive(Rig rig, FlipBlockAction flip, out SetLiftHeight approach)
    {
        rig.HoldLift = true; rig.HoldPath = true;
        LiftReports(rig, Math.Asin(-13.0 / 66));                                     // the lift at 32 mm: the 45 mm move is sent and waits
        var task = flip.RunAsync(default);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!rig.Sent.OfType<SetLiftHeight>().Any() && sw.ElapsedMilliseconds < 60000) { rig.Pump(); Thread.Sleep(5); }
        approach = Assert.Single(rig.Sent.OfType<SetLiftHeight>());
        Assert.Equal(45f, approach.HeightMm);
        rig.Send(new MotorActionAck { ActionId = approach.ActionId });
        sw.Restart();
        while (!rig.Sent.OfType<ExecutePath>().Any() && sw.ElapsedMilliseconds < 60000) { LiftReports(rig, 0.0); rig.Pump(); Thread.Sleep(5); }
        Assert.NotEmpty(rig.Sent.OfType<ExecutePath>());
        Assert.False(task.IsCompleted);                                              // the drive is RUNNING
        return task;
    }

    /// <summary>
    /// M13-028, CheckIfDone 0x0055F074 (0x0055F0EA..0x0055F130): the carry-height lift is queued only on a tick where the embedded compound is still RUNNING and the norm of the object pose with respect
    /// to the robot pose is below [this+0x138] = 40, the norm being THREE-D ([T+0x20]^2, then [T+0x24]^2 and [T+0x28]^2, vsqrt). The robot stands at the origin and the cube's centre is 22 mm up:
    /// a cube at (39, 0, 22) is 39 away planar but sqrt(39^2 + 22^2) = 44.8 in 3-D: no lift; a cube at (30, 0, 22) is sqrt(30^2 + 22^2) = 37.2: the lift (92 mm) is queued, tolerance 5.0.
    /// The tick that queues it is one where the approach lift has ended and released the lift track (M4-003, 0x005408EC): otherwise the queued action fails with 0x03000019 (next test). The path is
    /// held so the compound stays RUNNING while the cube comes within range.
    /// </summary>
    [Theory]
    [InlineData(39.0, false)]
    [InlineData(30.0, true)]
    public void M13_028_TheCarryLiftIsQueuedOnlyWithinFortyMillimetresInThreeDimensions(double cubeX, bool queued)
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        var flip = new FlipBlockAction(rig.M, 7) { CheckPreActionPose = false };
        var task = StartFlipAtTheDrive(rig, flip, out _);
        Assert.False(flip.LiftRaised);                                               // 200 mm away: no tick so far queued it
        obj.Pose = new Pose3d(obj.Pose.Rotation, new Vec3(cubeX, 0, 22));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 500) { rig.Pump(); Thread.Sleep(5); }
        Assert.False(task.IsCompleted);
        Assert.Equal(queued, flip.LiftRaised);
        Assert.Equal(queued ? new[] { 45f, LiftPresets.CarryMm } : new[] { 45f }, rig.Sent.OfType<SetLiftHeight>().Select(m => m.HeightMm));
        // finish: the path completes and the moves are acknowledged
        rig.HoldLift = false;
        rig.ReleasePath();
        int acked = 1;
        Spin(task, rig, () =>
        {
            foreach (var l in rig.Sent.OfType<SetLiftHeight>().Skip(acked).ToList()) { rig.Send(new MotorActionAck { ActionId = l.ActionId }); acked++; }
            LiftReports(rig, 0.0);
        }, ms: 60000);
        Assert.Equal(PoseState.Unknown, obj.PoseState);
    }

    /// <summary>
    /// M13-028 with M4-003 (IActionRunner::Update 0x00540370): the carry lift FlipBlockAction::CheckIfDone queues has its byte +0x56 set to 1 (0x0055F152..0x0055F154), and Update branches from
    /// 0x00540434 (bne.w 0x540592) over both AreAnyTracksLocked (0x00540440) and LockTracks (0x0054058E) when that byte is non-zero; the action's end skips UnlockTracks the same way (0x005408EC..0x005408F0).
    /// So while the approach MoveLiftToHeightAction (the compound's first action) still holds the lift track, the carry lift is sent all the same (no 0x03000019), it takes no lock of its own (the
    /// track is still held by the approach move, and only that move's end frees it) and sends no DisableAnimTracks/EnableAnimTracks. The cube is within 40 mm from the first tick.
    /// </summary>
    [Fact]
    public void M13_028_TheCarryLiftRunsWhileTheApproachLiftHoldsTheTrackAndTakesNoLock()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        var flip = FlipWithCubeAt(rig, 30.0, out _);
        rig.HoldLift = true;
        LiftReports(rig, Math.Asin(-13.0 / 66));
        var task = flip.RunAsync(default);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        // M4-016: the lift move's timeout is on the engine clock, so stop pumping as soon as the carry lift is queued
        // rather than letting the engine clock run past the 30 s IAction timeout.
        while (rig.Sent.OfType<SetLiftHeight>().Count() < 2 && sw.ElapsedMilliseconds < 60000) { rig.Pump(); Thread.Sleep(5); }
        Assert.False(task.IsCompleted);
        Assert.True(flip.LiftRaised);
        Assert.Equal(new[] { 45f, LiftPresets.CarryMm }, rig.Sent.OfType<SetLiftHeight>().Select(m => m.HeightMm));   // both sent: the carry lift was not refused while the track is held
        Assert.Single(rig.Sent.OfType<DisableAnimTracks>());                          // the approach move's lock only (LockTracks 0x0054058E); the carry lift took none
        Assert.True(rig.Robot.Motion.AreAnyTracksLocked(CozmoMotion.LiftTrack));
        // the carry lift ends (ack + in position at 92 mm) while the approach move is still pending: it releases nothing, the approach move's lock stays
        var lifts = rig.Sent.OfType<SetLiftHeight>().ToList();
        rig.Send(new MotorActionAck { ActionId = lifts[1].ActionId });
        LiftReports(rig, (float)Math.Asin(1.0));                                      // 92 mm
        rig.Pump();
        Assert.True(rig.Robot.Motion.AreAnyTracksLocked(CozmoMotion.LiftTrack));
        Assert.Empty(rig.Sent.OfType<EnableAnimTracks>());
        rig.HoldLift = false;
        rig.Send(new MotorActionAck { ActionId = lifts[0].ActionId });
        Spin(task, rig, () => LiftReports(rig, 0.0), ms: 60000);
    }

    /// <summary>
    /// M13-028, CheckIfDone 0x0055F094 (cmp r5,#0x1000000; bne 0x0055F17A): a compound result that is NOT RUNNING marks the object Unknown (0x0055F186) and is returned WITHOUT queueing the carry lift
    /// (the retired fallback raised the lift after a failure; it has no source). The 45 mm lift move is never acknowledged: its engine-clock IAction timeout (the 30.0 s default, 0x0052B0C2) fires with
    /// 0x03000018 (0x00540E80), the compound ends with that code, no drive is sent, no carry lift is queued, and the object is Unknown. The cube is 200 mm away, so no RUNNING tick had it within 40 mm either.
    /// </summary>
    [Fact]
    public void M13_028_AFailedCompoundMarksTheObjectUnknownAndQueuesNoCarryLift()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        rig.HoldLift = true;                                                          // the 45 mm move is never acknowledged
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        LiftReports(rig, Math.Asin(-13.0 / 66));
        var flip = new FlipBlockAction(rig.M, 7) { CheckPreActionPose = false };
        var task = flip.RunAsync(default);
        // M4-016: the timeout is on the engine clock, so tick it (Pump alone stops ticking once the transport has acked
        // the command); the lift is never acknowledged, so the 30 s IAction timeout fires with 0x03000018.
        Spin(task, rig, () => rig.Tick(), ms: 60000);
        Assert.True(task.IsCompleted);
        Assert.Equal((ActionResult)0x03000018u, task.Result);                         // M4-016: IAction timeout, not the retired ActionResult.Timeout stand-in
        Assert.False(flip.LiftRaised);
        Assert.Equal(new[] { 45f }, rig.Sent.OfType<SetLiftHeight>().Select(m => m.HeightMm));
        Assert.Empty(rig.Sent.OfType<AppendPathSegmentLine>());
        Assert.Equal(PoseState.Unknown, obj.PoseState);
    }

    /// <summary>
    /// M13-028 / M4-016 MA17 (0x005493F6..0x00549508): a lift move that fails returns the engine's own code from MotionOutcome.EngineResult (0x04000004 StoppedMakingProgress, 0x03000016 send failed);
    /// an outcome with no engine code (only the unsourced C# wait ran out) is the stand-in ActionResult.Timeout.
    /// </summary>
    [Fact]
    public void M13_028_ALiftFailureReturnsTheEnginesResultCode()
    {
        Assert.Equal(0x04000004u, (uint)FlipBlockAction.LiftFailureResult(new MotionOutcome(MotionResult.Failed, "stopped") { EngineResult = 0x04000004 }));
        Assert.Equal(0x03000016u, (uint)FlipBlockAction.LiftFailureResult(new MotionOutcome(MotionResult.Failed, "send") { EngineResult = 0x03000016 }));
        Assert.Equal(ActionResult.Timeout, FlipBlockAction.LiftFailureResult(new MotionOutcome(MotionResult.TimedOut, "no ack")));
    }

    // ================================================================== R-VIS M12 fix round 5 (FlipBlockAction::CheckIfDone, M13-028)

    private static FlipBlockAction FlipWithCubeAt(Rig rig, double x, out ObservableObject obj)
    {
        rig.Cube = ManipulationTests.CubeAt(200, 0);
        obj = Assert.Single(rig.Frame().Objects).Object;
        obj.Pose = new Pose3d(obj.Pose.Rotation, new Vec3(x, 0, 22));
        return new FlipBlockAction(rig.M, 7) { CheckPreActionPose = false };
    }

    /// <summary>
    /// M13-028, CheckIfDone 0x0055F074, the carry lift: queued on a RUNNING tick (compoundResult null) iff the object pose with respect to the robot pose has a 3-D norm below [this+0x138] = 40 and
    /// [this+0x13C] == -1. The robot is at the origin, the cube's centre 22 mm up: a cube at x = 200 is 201.2 away and at x = 39 is sqrt(39^2 + 22^2) = 44.8 (planar 39): no lift; at x = 30 it is
    /// sqrt(30^2 + 22^2) = 37.2: the lift is queued on the robot's action list (a 92 mm SetLiftHeight) and the tick is RUNNING (null); a second RUNNING tick queues nothing more ([+0x13C] != -1).
    /// The queued action's byte +0x56 = 1 is modelled as suppressed track locking (see the test above and the one on the held approach lift).
    /// </summary>
    [Theory]
    [InlineData(200.0, false)]
    [InlineData(39.0, false)]
    [InlineData(30.0, true)]
    public void M13_028_TheCarryLiftIsQueuedByARunningTickWithin40mmInThreeDimensions(double cubeX, bool queued)
    {
        if (Lib is null) return;
        using var rig = new Rig();
        var flip = FlipWithCubeAt(rig, cubeX, out _);
        Assert.Null(flip.CheckIfDoneTick(null));
        Assert.Equal(queued, flip.LiftRaised);
        Assert.Null(flip.CheckIfDoneTick(null));
        Assert.Equal(queued ? new[] { LiftPresets.CarryMm } : Array.Empty<float>(), rig.Pump().OfType<SetLiftHeight>().Select(m => m.HeightMm));
    }

    /// <summary>
    /// M13-028, CheckIfDone 0x0055F074, the object lookup: GetLocatedObjectByIdHelper(id, -1) every tick, before the RUNNING test. RUNNING with the object not located (an Unknown pose state): warn and
    /// 0x03000004 BadObject (0x0055F09C cmp r0,#0; beq 0x55F18C; 0x0055F1C4), no carry lift. NOT RUNNING with no object: warn and return the compound's result (0x0055F17A cbz), nothing marked; NOT RUNNING with the
    /// object: MarkObjectUnknown(obj, true) (0x0055F186) and the compound's result, whatever it is (Retry 0x04000000 here).
    /// </summary>
    [Fact]
    public void M13_028_TheTickLooksTheObjectUpEveryTickAndReturnsTheCompoundsResult()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        var flip = FlipWithCubeAt(rig, 30.0, out var obj);
        obj.PoseState = PoseState.Unknown;                                             // lost
        Assert.Equal(ActionResult.BadObject, flip.CheckIfDoneTick(null));
        Assert.False(flip.LiftRaised);                                                 // BadObject before the 3-D test
        Assert.Contains(flip.Trace, l => l.Contains("not located while the compound is running"));
        Assert.Equal(ActionResult.Success, flip.CheckIfDoneTick(ActionResult.Success));  // not RUNNING, no object: the compound's result, warning
        Assert.Contains(flip.Trace, l => l.Contains("is not located (warning); compound result Success"));
        obj.PoseState = PoseState.Known;
        Assert.Equal(ActionResult.Retry, flip.CheckIfDoneTick(ActionResult.Retry));    // not RUNNING with the object: marked Unknown, the compound's result
        Assert.Equal(PoseState.Unknown, obj.PoseState);
        Assert.Contains(flip.Trace, l => l.Contains("compound result Retry; object 7 marked Unknown"));
        Assert.Empty(rig.Pump().OfType<SetLiftHeight>());                              // a finished compound never queues the carry lift
    }

    /// <summary>
    /// M13-028: the carry lift is independent of the flip (queued on the ROBOT'S action list, 0x0055F160; cancelled by the destructor, 0x0055ED6C..0x0055ED7A). The compound is in its drive (the approach
    /// lift ended and released the lift track) when the cube comes within 40 mm, so a RUNNING tick queues the carry lift and it is sent; the carry lift is never acknowledged. The flip still ends with the
    /// compound's result, Success (not Timeout, 0x04000004 or 0x03000016), without waiting for that lift, and the unfinished lift is counted as a cancel the stack cannot do (no cancel handle: MISSING).
    /// </summary>
    [Fact]
    public void M13_028_TheFlipNeverWaitsOnTheQueuedCarryLift()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        var flip = FlipWithCubeAt(rig, 200.0, out var obj);
        var task = StartFlipAtTheDrive(rig, flip, out _);
        obj.Pose = new Pose3d(obj.Pose.Rotation, new Vec3(30.0, 0, 22));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        // M4-016: stop pumping as soon as the carry lift is queued, so the engine clock stays well inside the 30 s IAction timeout.
        while (!flip.LiftRaised && sw.ElapsedMilliseconds < 60000) { rig.Pump(); Thread.Sleep(5); }
        Assert.True(flip.LiftRaised);
        // The flip runs on an async Task: CheckIfDone stores [this+0x13C] (LiftRaised) BEFORE it calls
        // ActionList::QueueAction (0x0055F15C before 0x0055F16A), so the flag can be observed before the carry
        // action is on the wire. Wait (bounded) for the 92 mm SetLiftHeight to actually appear instead of a
        // single pump.
        sw.Restart();
        while (rig.Sent.OfType<SetLiftHeight>().Count() < 2 && sw.ElapsedMilliseconds < 60000) { rig.Pump(); Thread.Sleep(5); }
        var lifts = rig.Sent.OfType<SetLiftHeight>().ToList();
        Assert.True(lifts.Count >= 2, "the queued carry lift (92 mm) must reach the wire");
        Assert.Equal(new[] { 45f, LiftPresets.CarryMm }, lifts.Select(l => l.HeightMm));
        rig.ReleasePath();                                                             // the drive completes; the carry lift (lifts[1]) is never acknowledged
        sw.Restart();
        Spin(task, rig, () => LiftReports(rig, 0.0), ms: 4000);
        Assert.True(task.IsCompleted, "the flip must not wait for the carry lift");
        Assert.True(sw.ElapsedMilliseconds < 4000);
        Assert.Equal(ActionResult.Success, task.Result);
        Assert.Equal(1, flip.QueuedLiftCancelsNotModelled);
        Assert.Contains(flip.Trace, l => l.Contains("ActionList::Cancel(id) is not modelled"));
    }

    /// <summary>
    /// M13-028: a cube lost while the compound is RUNNING ends the flip with 0x03000004 at the next tick (0x0055F1C4); the destructor's RemoveDisableReactionsLock (0x0055ED88) is counted with Init's two
    /// M8 calls (3 in all). The engine destroys the embedded compound in the destructor (0x0055ED68), so it never sends the drive: the 45 mm lift move is still pending when the flip ends, its
    /// acknowledgement and an in-position state arrive only AFTERWARDS, and no path message (line, arc, turn, execute, clear) and no lift command may go out after the flip returned.
    /// </summary>
    [Fact]
    public void M13_028_ALostObjectEndsARunningFlipWithBadObjectAndNoPathIsSentAfterwards()
    {
        if (Lib is null) return;
        using var rig = new Rig();
        var flip = FlipWithCubeAt(rig, 200.0, out var obj);
        rig.HoldLift = true;
        LiftReports(rig, Math.Asin(-13.0 / 66));                                       // the 45 mm move waits: the compound is RUNNING
        var task = flip.RunAsync(default);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        // M4-016: stop pumping as soon as the lift command is sent, so the engine clock stays inside the 30 s IAction timeout.
        while (!rig.Sent.OfType<SetLiftHeight>().Any() && sw.ElapsedMilliseconds < 60000) { rig.Pump(); Thread.Sleep(5); }
        Assert.False(task.IsCompleted);
        var lift = Assert.Single(rig.Sent.OfType<SetLiftHeight>());
        obj.PoseState = PoseState.Unknown;
        Spin(task, rig, ms: 60000);
        Assert.True(task.IsCompleted);
        Assert.Equal(ActionResult.BadObject, task.Result);
        Assert.Equal(3, flip.M8CallsNotModelled);
        rig.Pump();
        int sentAtReturn = rig.Sent.Count;
        // only now does the lift move complete: the compound would go on to its drive
        rig.Send(new MotorActionAck { ActionId = lift.ActionId });
        sw.Restart();
        while (!flip.Trace.Any(l => l.Contains("the flip has ended")) && sw.ElapsedMilliseconds < 60000) { LiftReports(rig, 0.0); rig.Pump(); Thread.Sleep(5); }
        var after = rig.Sent.Skip(sentAtReturn).ToList();
        Assert.DoesNotContain(after, m => m is AppendPathSegmentLine or AppendPathSegmentArc or AppendPathSegmentPointTurn or ExecutePath or ClearPath or SetLiftHeight);
        Assert.Contains(flip.Trace, l => l.Contains("the flip has ended; the embedded compound is not continued"));
    }
}
