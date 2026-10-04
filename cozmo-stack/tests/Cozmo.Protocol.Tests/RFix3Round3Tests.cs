using System.Net;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// R-FIX3 round 3. Every expected value is read from libcozmoEngine.so 3.4.0-1204, not from the code under test; each test names the record and the address it checks.
/// M7-014 (per-class reaction-lock tables and their install points), M7-008 (the streamer's clock), M8-001 (the Init/Resume spark lock inputs), M8-007/M8-009 (the unnamed track locks).
/// </summary>
public class RFix3Round3Tests
{
    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;

    private static BehaviorContext Ctx(Rig rig) => new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Random = new Random(3) };

    private static string MaskString(ReactionLockTable t) => new(t.ToMask().Select(b => b ? '1' : '0').ToArray());

    private static void AssertLockedExactly(BehaviorManager m, string lockName, string mask)
    {
        for (int t = 0; t < 21; t++)
            Assert.Equal(mask[t] == '1', m.HasDisableLock((ReactionTrigger)t, lockName));
    }

    private static BehaviorManager NewManager(BehaviorContext ctx, params IBehavior[] behaviours)
    {
        var m = new BehaviorManager(ctx);
        foreach (var b in behaviours) m.Add(b);
        // a reaction on every trigger, so a lock's effect on a trigger is visible
        var any = new Fake();
        foreach (var t in Enum.GetValues<ReactionTrigger>()) m.AddReaction(new GenericReactionStrategy(t, "R-FIX3 round 3", () => 0), any);
        return m;
    }

    private sealed class Fake : IBehavior
    {
        public string Id => "fake";
        public string Class => "Fake";
        public bool IsRunnable(BehaviorContext c) => true;
        public double EvaluateScore(BehaviorContext c) => 0;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) => Task.CompletedTask;
        public bool Update(BehaviorContext c, double nowMs) => true;
        public void Stop(BehaviorStopReason r) { }
    }

    private static bool WithoutAssets() => Environment.GetEnvironmentVariable("COZMO_TESTS_WITHOUT_ASSETS") == "1";

    /// <summary>These tests must not pass silently without the marker library (AssetPresenceTests): they fail with the reason unless the run says COZMO_TESTS_WITHOUT_ASSETS=1.</summary>
    private static bool NeedsLibrary()
    {
        if (Lib is not null) return true;
        if (WithoutAssets()) return false;
        throw new Xunit.Sdk.XunitException("the marker library is missing (Cozmo.Robot was built without Vision/Data/marker_nn_library.bin), so this test cannot run; provide it or set COZMO_TESTS_WITHOUT_ASSETS=1 for a run that knowingly has none");
    }

    private static string? AssetsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "re-analysis", "obb", "assets", "cozmo_resources", "assets");
            if (Directory.Exists(Path.Combine(candidate, "animations"))) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static Pose3d CubeAt(double x, double y, double yaw = 0, double z = Cozmo.Robot.Vision.CubeGeometry.CubeSizeMm / 2) => new(Mat3.AboutZ(yaw), new Vec3(x, y, z));

    private static BehaviorScope ScopeOf(SteppedBehavior b) =>
        (BehaviorScope)typeof(SteppedBehavior).GetProperty("Scope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(b)!;

    private static void Idle(Rig rig)
    {
        var lifts = rig.Sent.OfType<SetLiftHeight>().ToList();
        foreach (var l in lifts) rig.Send(new MotorActionAck { ActionId = l.ActionId });
        double angle = lifts.Count == 0 ? 0.0 : Math.Asin((Math.Clamp(lifts[^1].HeightMm, 32f, 92f) - 45.0) / 66.0);
        rig.Send(new RobotState
        {
            Timestamp = rig.T += 33, PoseOriginId = rig.OriginId, Pose = new RobotPose { X = rig.X, Y = rig.Y, Angle = rig.Angle }, HeadAngle = rig.Head,
            Status = (uint)(RobotStatusFlag.LiftInPos | RobotStatusFlag.HeadInPos), LiftAngle = (float)angle, Accel = new AccelData { Z = 9800 }, Gyro = new GyroData(),
        });
    }

    // =============================================================== M7-014: the tables

    /// <summary>
    /// M7-014: the four tables this round installs, as the 21 value bytes read from the binary (21 consecutive {ordinal, value} pairs; the first byte of pair i is i; a non-zero value byte
    /// disables, 0x005A283C..0x005A2846): KnockOverCubes 0x00C67CB2, the PrepareForKnockOverAttempt zero table 0x00C67CDC, BuildPyramid 0x00C6C691, RamIntoBlock 0x00C73520. The lock name
    /// literal at 0x005C3820 is "preparingToKnockOverDisable" (27 characters, the length passed at 0x005C37B8).
    /// </summary>
    [Fact]
    public void TheNewClassTablesAreTheShippedBytes()
    {
        (ReactionLockTable T, string Address, string Mask)[] cases =
        {
            (ReactionLockTables.KnockOverCubes, "0xC67CB2", "010001001000000000000"),
            (ReactionLockTables.KnockOverCubesPreparing, "0xC67CDC", "000000000000000000000"),
            (ReactionLockTables.BuildPyramid, "0xC6C691", "000000001000000000000"),
            (ReactionLockTables.RamIntoBlock, "0xC73520", "101000001000000000001"),
        };
        foreach (var (t, address, mask) in cases)
        {
            Assert.Equal(address, t.TableAddress);
            Assert.Equal(21, t.ToMask().Length);
            Assert.Equal(mask, MaskString(t));
        }
        Assert.Equal("preparingToKnockOverDisable", ReactionLockTables.KnockOverCubesPreparingName);
        Assert.Equal(0x1B, ReactionLockTables.KnockOverCubesPreparingName.Length);
    }

    /// <summary>
    /// M7-014: BehaviorKnockOverCubes::InitInternal 0x005C31A2 calls InitializeMemberVars (0x005C31D8) first; with the target stack it takes SmartDisableReactionsWithLock(own name, table
    /// 0x00C67CB2) (0x005C31FA); when PrepareForKnockOverAttempt runs (0x005C3780: TransitionToKnockingOverStack) it removes "preparingToKnockOverDisable" (0x005C37C2) and installs it with the all-zero
    /// table 0x00C67CDC (0x005C37F0). IBehavior::Stop releases both (0x005BD12C..0x005BD140). With no stack, InitializeMemberVars returns 0 with no lock.
    /// </summary>
    [Fact]
    public void KnockOverCubesTakesItsTableAtInitializeMemberVarsAndTheZeroTableAtPrepare()
    {
        if (!NeedsLibrary()) return;
        using var rig = new Rig();
        rig.Head = 0.12f;
        rig.Cube = CubeAt(260, 0);
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, CubeAt(260, 0, 0, 66)));
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE3, CubeAt(260, 0, 0, 110)));
        Assert.Equal(3, rig.Frame().Objects.Count);
        var ctx = Ctx(rig);
        var b = new KnockOverCubesBehavior(rig.M, "KnockOverCubes", 3) { AlwaysStreamline = true };   // 0x005C31B4: +0xD9 goes straight to TransitionToKnockingOverStack
        using var m = NewManager(ctx, b);
        Assert.True(m.StartAsync("KnockOverCubes", 0).GetAwaiter().GetResult());
        AssertLockedExactly(m, "KnockOverCubes_behaviorLock", "010001001000000000000");
        var held = ScopeOf(b).HeldReactionLocks;
        Assert.Equal(new[] { "KnockOverCubes", "preparingToKnockOverDisable" }, held);

        // a second attempt (PrepareForKnockOverAttempt again, as TransitionToBlindlyFlipping does): the name is removed and installed once more, the set still holds it once
        typeof(KnockOverCubesBehavior).GetMethod("PrepareForKnockOverAttempt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(b, null);
        Assert.Equal(new[] { "KnockOverCubes", "preparingToKnockOverDisable" }, ScopeOf(b).HeldReactionLocks);
        AssertLockedExactly(m, "KnockOverCubes_behaviorLock", "010001001000000000000");

        var scope = ScopeOf(b);
        m.Stop(BehaviorStopReason.Interrupted, 0);
        AssertLockedExactly(m, "KnockOverCubes_behaviorLock", new string('0', 21));
        Assert.Empty(scope.HeldReactionLocks);
        Assert.All(Enum.GetValues<ReactionTrigger>(), t => Assert.True(m.IsReactionTriggerEnabled(t)));
    }

    [Fact]
    public void KnockOverCubesWithNoStackTakesNoLock()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = new KnockOverCubesBehavior(rig.M, "KnockOverCubes", 3);
        using var m = NewManager(ctx, b);
        m.StartAsync("KnockOverCubes", 0).GetAwaiter().GetResult();
        AssertLockedExactly(m, "KnockOverCubes_behaviorLock", new string('0', 21));
        Assert.Empty(ScopeOf(b).HeldReactionLocks);
    }

    /// <summary>
    /// M7-014: BehaviorRamIntoBlock::InitInternal 0x00604764 takes no lock; TransitionToRammingIntoBlock's first call (0x00604A44) takes SmartDisableReactionsWithLock(own name, table
    /// 0x00C73520). Driven through the live manager's update until the class reaches the ram.
    /// </summary>
    [Fact]
    public void RamIntoBlockTakesItsTableWhenItReachesTheRam()
    {
        if (!NeedsLibrary()) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(250, 0);
        Assert.Single(rig.Frame().Objects);
        var ctx = Ctx(rig);
        var b = new RamIntoBlockBehavior(rig.M);
        using var m = NewManager(ctx, b);
        Assert.True(m.StartAsync("RamIntoBlock", 0).GetAwaiter().GetResult());
        Assert.Equal(RamIntoBlockBehavior.Phase.Aligning, b.CurrentPhase);
        AssertLockedExactly(m, "RamIntoBlock_behaviorLock", new string('0', 21));       // InitInternal: no lock
        Assert.Empty(ScopeOf(b).HeldReactionLocks);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        double t = 0;
        while (b.CurrentPhase == RamIntoBlockBehavior.Phase.Aligning)
        {
            rig.Pump(); Idle(rig);
            t += 33; m.Update(t, t / 1000.0);
            if (sw.ElapsedMilliseconds > 10000) throw new TimeoutException(string.Join(" | ", b.Trace));
            Thread.Sleep(2);
        }
        Assert.NotEqual(RamIntoBlockBehavior.Phase.Aligning, b.CurrentPhase);
        AssertLockedExactly(m, "RamIntoBlock_behaviorLock", "101000001000000000001");
        Assert.Equal(new[] { "RamIntoBlock" }, ScopeOf(b).HeldReactionLocks);
        m.Stop(BehaviorStopReason.Interrupted, t);
        AssertLockedExactly(m, "RamIntoBlock_behaviorLock", new string('0', 21));
    }

    /// <summary>
    /// M7-014: BehaviorBuildPyramid::TransitionToPlacingTopBlock 0x005DC08C takes SmartDisableReactionsWithLock(own name, table 0x00C6C691) (0x005DC0D6); the BuildPyramidBase class has no call site,
    /// so its start takes no lock. The method is entered here as the pyramid class's pick-up completion enters it.
    /// </summary>
    [Fact]
    public void BuildPyramidTakesItsTableAtPlacingTopBlockAndBuildPyramidBaseTakesNone()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var pyramid = new BuildPyramidBaseBehavior(rig.M, "BuildPyramid", buildTop: true);
        var baseOnly = new BuildPyramidBaseBehavior(rig.M, "BuildPyramidBase");
        using var m = NewManager(ctx, pyramid, baseOnly);

        m.StartAsync("BuildPyramidBase", 0).GetAwaiter().GetResult();
        Assert.Empty(ScopeOf(baseOnly).HeldReactionLocks);
        AssertLockedExactly(m, "BuildPyramidBase_behaviorLock", new string('0', 21));
        m.Stop(BehaviorStopReason.Interrupted, 0);

        m.StartAsync("BuildPyramid", 0).GetAwaiter().GetResult();
        Assert.Empty(ScopeOf(pyramid).HeldReactionLocks);                                // Start itself takes none
        typeof(BuildPyramidBaseBehavior).GetMethod("TransitionToPlacingTopBlock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(pyramid, null);
        AssertLockedExactly(m, "BuildPyramid_behaviorLock", "000000001000000000000");
        Assert.Equal(new[] { "BuildPyramid" }, ScopeOf(pyramid).HeldReactionLocks);
    }

    /// <summary>
    /// M7-014: the engine's caller list of IBehavior::SmartDisableReactionsWithLock (every call to PLT 0x004B28EC) has no site in BehaviorPickUpCube, RollBlock, StackBlocks, RespondPossiblyRoll,
    /// ExploreBringCubeToBeacon or AcknowledgeObject, so starting them takes no reaction lock (the arbiter-wide Scope.DisableReactions() is gone: ReactionsDisabled stays false).
    /// </summary>
    [Fact]
    public void ClassesWithNoEngineLockSiteTakeNoReactionLock()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var all = new SteppedBehavior[]
        {
            new PickUpCubeBehavior(rig.M), new RollBlockBehavior(rig.M), new StackBlocksBehavior(rig.M), new RespondPossiblyRollBehavior(rig.M),
            new BringCubeToBeaconBehavior(rig.M), new AcknowledgeObjectBehavior(rig.Vision.World),
        };
        foreach (var b in all)
        {
            var scope = new BehaviorScope(manager: null);
            b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
            Assert.False(scope.ReactionsDisabled, b.GetType().Name);
            Assert.Empty(scope.HeldReactionLocks);
            b.Stop(BehaviorStopReason.Interrupted);
        }
        // and through a live manager: no trigger is locked by any name while each one runs
        foreach (var b in all)
        {
            using var m = NewManager(ctx);
            var scope = new BehaviorScope(manager: m);
            b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
            Assert.Empty(scope.HeldReactionLocks);
            Assert.All(Enum.GetValues<ReactionTrigger>(), t => Assert.True(m.IsReactionTriggerEnabled(t), $"{b.GetType().Name} locked {t}"));
            b.Stop(BehaviorStopReason.Interrupted);
        }
    }

    private static void RunManaged(Rig rig, BehaviorManager m, IBehavior b, int ms = 20000)
    {
        double t = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (ReferenceEquals(m.Current, b))
        {
            rig.Pump(); rig.Frame();
            t += 33; m.Update(t, t / 1000.0);
            if (sw.ElapsedMilliseconds > ms) throw new TimeoutException(b is SteppedBehavior sb ? string.Join(" | ", sb.Trace) : b.Id);
            Thread.Sleep(2);
        }
    }

    /// <summary>
    /// M7-014: BehaviorPopAWheelie::InitInternal 0x005C7496 takes no lock. The lock is taken by the body 0x005C7BC8 (SmartDisableReactionsWithLock(own name, table 0x00C6883D) at 0x005C7BE8,
    /// [+0x130] = 1, EnableStopOnCliff(false)), which the lambda $_3 of IDriveToInteractWithObject::AddDockAction (operator() 0x0055E068) calls: a WaitForLambdaAction added before the dock action,
    /// so after the drive and before the pop. The completion lambda 0x005C7CBC removes it on a non-zero result (0x005C7CD4..0x005C7CDA); ResetBehavior 0x005C76B0 sends EnableStopOnCliff(true)
    /// only when +0x130 is set. Driven through the live manager.
    /// </summary>
    [Fact]
    public void PopAWheelieTakesItsTableInThePreDockCallbackAndReleasesItAtStop()
    {
        if (!NeedsLibrary()) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(220, 0);
        Assert.Single(rig.Frame().Objects);
        rig.DockOutcome = BlockStatus.NoBlock;
        var ctx = Ctx(rig);
        var b = new PopAWheelieBehavior(rig.M, "PopAWheelie");
        using var m = NewManager(ctx, b);
        var log = new System.Collections.Concurrent.ConcurrentQueue<string>();
        m.Log += log.Enqueue;
        Assert.True(m.StartAsync("PopAWheelie", 0).GetAwaiter().GetResult());
        Assert.Empty(ScopeOf(b).HeldReactionLocks);                                       // InitInternal: none
        AssertLockedExactly(m, "PopAWheelie_behaviorLock", new string('0', 21));
        // the lock must be held when the dock REQUEST goes out: at the moment the manager logs the lock line the dock request count must still be zero
        int docksAtLock = -1;
        m.Log += l =>
        {
            if (l == "BehaviorManager.DisableReactionsWithLock.DisablingWithLock: CliffDetected with lock PopAWheelie_behaviorLock" && docksAtLock < 0)
            {
                for (int tries = 0; tries < 5; tries++)
                    try { docksAtLock = rig.Sent.ToArray().Count(x => x is DockWithObject); break; } catch (InvalidOperationException) { }
            }
        };
        RunManaged(rig, m, b);
        rig.Pump();
        Assert.True(b.Succeeded, string.Join(" | ", b.Trace));
        Assert.Equal(0, docksAtLock);
        // the table 0xC6883D: CliffDetected, CubeMoved, FistBump, RobotFalling, RobotPickedUp, ReturnedToTreads, RobotOnBack, UnexpectedMovement
        foreach (var t in new[] { "CliffDetected", "CubeMoved", "FistBump", "RobotFalling", "RobotPickedUp", "ReturnedToTreads", "RobotOnBack", "UnexpectedMovement" })
            Assert.Contains(log, l => l == $"BehaviorManager.DisableReactionsWithLock.DisablingWithLock: {t} with lock PopAWheelie_behaviorLock");
        Assert.DoesNotContain(log, l => l.Contains("DisablingWithLock: ObjectPositionUpdated with lock PopAWheelie_behaviorLock"));
        // order on the wire: EnableStopOnCliff(false) before the dock, EnableStopOnCliff(true) after
        var sent = rig.Sent.ToList();
        int off = sent.FindIndex(x => x is EnableStopOnCliff { Enable: false });
        int dock = sent.FindIndex(x => x is DockWithObject);
        int on = sent.FindIndex(x => x is EnableStopOnCliff { Enable: true });
        Assert.True(off >= 0 && dock > off && on > dock, $"off {off}, dock {dock}, on {on}");
        // the stop released the lock
        AssertLockedExactly(m, "PopAWheelie_behaviorLock", new string('0', 21));
    }

    [Fact]
    public void PopAWheelieRemovesItsLockWhenTheActionFails()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = new PopAWheelieBehavior(rig.M, "PopAWheelie");
        using var m = NewManager(ctx, b);
        m.StartAsync("PopAWheelie", 0).GetAwaiter().GetResult();
        CallPreDock(b, ScopeOf(b), RunOf(b), CancellationToken.None);
        AssertLockedExactly(m, "PopAWheelie_behaviorLock", "110100000001101100001");
        Assert.Equal(new[] { "PopAWheelie" }, ScopeOf(b).HeldReactionLocks);
        typeof(PopAWheelieBehavior).GetMethod("OnActionComplete", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(b, new object[] { 7u, ActionResult.Abort });
        AssertLockedExactly(m, "PopAWheelie_behaviorLock", new string('0', 21));
        Assert.Empty(ScopeOf(b).HeldReactionLocks);
    }

    [Fact]
    public void PopAWheelieStopSendsEnableStopOnCliffOnlyWhenTheCallbackRan()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = new PopAWheelieBehavior(rig.M, "PopAWheelie");
        using var m = NewManager(ctx, b);
        m.StartAsync("PopAWheelie", 0).GetAwaiter().GetResult();
        m.Stop(BehaviorStopReason.Interrupted, 0);
        rig.Pump();
        Assert.DoesNotContain(rig.Sent, x => x is EnableStopOnCliff);                    // 0x005C76C8..0x005C76D0: +0x130 clear: nothing sent
    }

    private static void CallPreDock(PopAWheelieBehavior b, BehaviorScope scope, int run, CancellationToken ct) =>
        typeof(PopAWheelieBehavior).GetMethod("PreDockCallback", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(b, new object[] { scope, run, ct });

    private static int RunOf(PopAWheelieBehavior b) =>
        (int)typeof(PopAWheelieBehavior).GetField("_run", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(b)!;

    /// <summary>
    /// M7-014: the engine runs the pre-dock callback inside the tick-driven action list (WaitForLambdaAction, added at 0x0055B802 before the dock action at 0x0055B838), so a stop or a
    /// cancel means it never runs. A callback for a run that has been stopped, or with a cancelled token, sends no EnableStopOnCliff(false) and installs no lock.
    /// </summary>
    [Fact]
    public void AStoppedOrCancelledPreDockCallbackSendsNothingAndLocksNothing()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = new PopAWheelieBehavior(rig.M, "PopAWheelie");
        using var m = NewManager(ctx, b);
        m.StartAsync("PopAWheelie", 0).GetAwaiter().GetResult();
        var scope = ScopeOf(b);
        int run = RunOf(b);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        CallPreDock(b, scope, run, cancelled.Token);                                   // cancelled token
        m.Stop(BehaviorStopReason.Interrupted, 0);                                     // the run ends
        CallPreDock(b, scope, run, CancellationToken.None);                            // a stale run
        rig.Pump();
        Assert.DoesNotContain(rig.Sent, x => x is EnableStopOnCliff);
        AssertLockedExactly(m, "PopAWheelie_behaviorLock", new string('0', 21));
        Assert.Empty(scope.HeldReactionLocks);
    }

    /// <summary>M7-014: the manager's stop path (its monitor, then OnStop, then the scope's Dispose) running against the callback never deadlocks and never leaves EnableStopOnCliff(false) unrestored or a lock behind.</summary>
    [Fact]
    public void ManagerStopConcurrentWithThePreDockCallbackNeitherDeadlocksNorLeavesAnything()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = new PopAWheelieBehavior(rig.M, "PopAWheelie");
        using var m = NewManager(ctx, b);
        var work = Task.Run(() =>
        {
            for (int i = 0; i < 150; i++)
            {
                m.StartAsync("PopAWheelie", i).GetAwaiter().GetResult();
                var scope = ScopeOf(b);
                int run = RunOf(b);
                var cb = Task.Run(() => CallPreDock(b, scope, run, CancellationToken.None));
                m.Stop(BehaviorStopReason.Interrupted, i);
                cb.Wait();
            }
        });
        Assert.True(work.Wait(TimeSpan.FromSeconds(30)), "deadlock: the manager stop and the callback did not finish");
        rig.Pump();
        var sent = rig.Sent.ToList();
        Assert.Equal(sent.Count(x => x is EnableStopOnCliff { Enable: false }), sent.Count(x => x is EnableStopOnCliff { Enable: true }));
        AssertLockedExactly(m, "PopAWheelie_behaviorLock", new string('0', 21));
    }

    private sealed class FailAtOnce : IDockSubActionExecutor
    {
        public Task<ActionResult> RunAsync(DockSubAction action, List<string> trace, CancellationToken cancel) => Task.FromResult(ActionResult.Abort);
    }

    private sealed class PendingThenCancel : IDockSubActionExecutor
    {
        public async Task<ActionResult> RunAsync(DockSubAction action, List<string> trace, CancellationToken cancel)
        {
            await Task.Delay(Timeout.Infinite, cancel);
            return ActionResult.Success;
        }
    }

    private static void SetCurrentReaction(BehaviorManager m, ReactionTrigger? t) =>
        typeof(BehaviorManager).GetField("_currentReaction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(m, t);

    /// <summary>
    /// M7-014: IDockAction::Init 0x00551C16..0x00551C28 takes "dockActions" only when the compound's first Update returns 0 or RUNNING ((result | 0x01000000) == 0x01000000). A compound that
    /// fails at once takes no lock: no DisablingWithLock, no EnabledStateChanged, no stopCurrent.
    /// </summary>
    [Fact]
    public void ADockActionWhoseCompoundFailsAtOnceTakesNoLockAndStopsNothing()
    {
        if (!NeedsLibrary()) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(220, 0);
        Assert.Single(rig.Frame().Objects);
        using var m = NewManager(Ctx(rig));
        rig.M.ReactionLocks = m;
        var log = new System.Collections.Concurrent.ConcurrentQueue<string>();
        m.Log += log.Enqueue;
        SetCurrentReaction(m, ReactionTrigger.ObjectPositionUpdated);
        var pick = new PickupObjectAction(rig.M, 7) { CheckPreActionPose = false, SubActions = new FailAtOnce() };
        var task = pick.RunAsync(default);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted) { rig.Pump(); rig.Frame(); if (sw.ElapsedMilliseconds > 10000) throw new TimeoutException(string.Join(" | ", pick.Trace)); Thread.Sleep(2); }
        Assert.Equal(ActionResult.Abort, task.Result);
        Assert.DoesNotContain(log, l => l.Contains("dockActions"));
        Assert.DoesNotContain(log, l => l.Contains("stopping currently running one"));
        Assert.Equal(ReactionTrigger.ObjectPositionUpdated, m.CurrentReactionTrigger);
    }

    /// <summary>M7-014: a compound that is still running takes the lock with stopCurrent = 1 (0x00551C4C, movs r3,#1): a reaction running on ObjectPositionUpdated is stopped; the lock is removed after.</summary>
    [Fact]
    public void ADockActionWhoseCompoundRunsTakesTheLockStopsTheCurrentReactionAndRemovesItAfter()
    {
        if (!NeedsLibrary()) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(220, 0);
        Assert.Single(rig.Frame().Objects);
        using var m = NewManager(Ctx(rig));
        rig.M.ReactionLocks = m;
        var log = new System.Collections.Concurrent.ConcurrentQueue<string>();
        m.Log += log.Enqueue;
        SetCurrentReaction(m, ReactionTrigger.ObjectPositionUpdated);
        using var cts = new CancellationTokenSource();
        var pick = new PickupObjectAction(rig.M, 7) { CheckPreActionPose = false, SubActions = new PendingThenCancel() };
        var task = pick.RunAsync(cts.Token);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!m.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, "dockActions"))
        {
            rig.Pump(); rig.Frame();
            if (task.IsCompleted || sw.ElapsedMilliseconds > 10000) throw new TimeoutException(string.Join(" | ", pick.Trace));
            Thread.Sleep(2);
        }
        AssertLockedExactly(m, "dockActions", "000000001000000000000");
        Assert.Contains(log, l => l.Contains("stopping currently running one"));
        Assert.Null(m.CurrentReactionTrigger);
        cts.Cancel();
        try { task.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        AssertLockedExactly(m, "dockActions", new string('0', 21));
    }

    /// <summary>
    /// M7-014: the engine class behind MountCharger is BehaviorDockingTestSimple (the shipped devBehaviors/dockingTestSimple.json): InitInternal 0x005CB394 calls
    /// BehaviorManager::DisableReactionsWithLock directly (0x005CB434): the name "Docking test simple" (0x00BF358C, 0x13 characters, no suffix), the table from GetAffectAllArray (0x005A0404 =
    /// 0x00C60D40, all 21 bytes set), stopCurrent 1; StopInternal 0x005CDA38 removes it (0x005CDA5E).
    /// </summary>
    [Fact]
    public void MountChargerTakesTheDockingTestSimpleLockOnAllTriggersAndRemovesItAtStop()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var mount = new MountChargerBehavior(rig.M);
        using var m = NewManager(ctx, mount);
        Assert.Equal(0x13, MountChargerBehavior.DockingTestSimpleLockName.Length);
        Assert.Equal("Docking test simple", MountChargerBehavior.DockingTestSimpleLockName);
        m.StartAsync("DockingTestSimple", 0).GetAwaiter().GetResult();
        AssertLockedExactly(m, "Docking test simple", new string('1', 21));
        Assert.Empty(ScopeOf(mount).HeldReactionLocks);                                  // a direct manager lock: not in the +0xA4 set
        m.Stop(BehaviorStopReason.Interrupted, 0);
        AssertLockedExactly(m, "Docking test simple", new string('0', 21));
    }

    // =============================================================== M7-014: the dock actions' own locks

    /// <summary>
    /// M7-014: IDockAction::Init 0x005514FC removes "dockActions" first (0x00551540); after InitInternal and the compound's first Update (0 or RUNNING) it takes "dockActions" with the table
    /// 0x00C55A21 (only ObjectPositionUpdated), stopCurrent 1 (0x00551C4C); ~IDockAction 0x00557448 removes it when the action had started (0x0055753C). A pick-up is driven through the live
    /// manager's ManipulationSystem.ReactionLocks.
    /// </summary>
    [Fact]
    public void ADockActionTakesDockActionsOnObjectPositionUpdatedForItsRunAndRemovesItAfter()
    {
        if (!NeedsLibrary()) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(220, 0);
        Assert.Single(rig.Frame().Objects);
        var ctx = Ctx(rig);
        var b = new PickUpCubeBehavior(rig.M);
        using var m = NewManager(ctx, b);
        rig.M.ReactionLocks = m;
        var log = new System.Collections.Concurrent.ConcurrentQueue<string>();
        m.Log += log.Enqueue;
        // a stale lock of the same name is removed by Init before the new one is taken
        m.DisableReactionsWithLock("dockActions", new ReactionLockTable("stale", "0x0", new[] { (int)ReactionTrigger.CliffDetected }));
        Assert.True(m.HasDisableLock(ReactionTrigger.CliffDetected, "dockActions"));
        bool heldAtDock = false;
        bool staleGoneAtDock = false;
        rig.OnDockResult = () =>
        {
            heldAtDock = m.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, "dockActions");
            staleGoneAtDock = !m.HasDisableLock(ReactionTrigger.CliffDetected, "dockActions");
        };
        Assert.True(m.StartAsync("PickUpCube", 0).GetAwaiter().GetResult());
        RunManaged(rig, m, b);
        var pick = b;
        Assert.True(heldAtDock, string.Join(" | ", pick.Trace));
        Assert.True(staleGoneAtDock);
        AssertLockedExactly(m, "dockActions", new string('0', 21));                      // removed at the end of the run
        Assert.Contains(log, l => l == "BehaviorManager.DisableReactionsWithLock.DisablingWithLock: ObjectPositionUpdated with lock dockActions");
        Assert.Contains(log, l => l == "BehaviorManager.RemoveDisableReactionsLock: ObjectPositionUpdated lock dockActions removed");
        Assert.Equal("0xC55A21", Cozmo.Robot.Manipulation.DockActionBase.DockActionsLockTable.TableAddress);
        Assert.Equal("000000001000000000000", new string(Cozmo.Robot.Manipulation.DockActionBase.DockActionsLockTable.ToMask().Select(x => x ? '1' : '0').ToArray()));
    }

    /// <summary>
    /// M7-014: FlipBlockAction::Init 0x0055EDC8 takes DisableReactionsWithLock([this+0x48] = "FlipBlock" (0x0055ED3C), table 0x00C56AE0 = CubeMoved and UnexpectedMovement, stopCurrent 1) at
    /// 0x0055EEC6; ~FlipBlockAction 0x0055ED54 removes it (0x0055ED88).
    /// </summary>
    [Fact]
    public void FlipBlockActionTakesItsNamedLockAndRemovesItInItsDestructor()
    {
        if (!NeedsLibrary()) return;
        using var rig = new Rig();
        rig.Cube = CubeAt(200, 0);
        Assert.Single(rig.Frame().Objects);
        using var m = NewManager(Ctx(rig));
        rig.M.ReactionLocks = m;
        Idle(rig);
        var flip = new Cozmo.Robot.Manipulation.FlipBlockAction(rig.M, 7) { CheckPreActionPose = false };
        var task = flip.RunAsync(default);
        bool seen = false;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            Idle(rig);
            if (m.HasDisableLock(ReactionTrigger.CubeMoved, "FlipBlock"))
            {
                seen = true;
                AssertLockedExactly(m, "FlipBlock", "010000000000000000001");
            }
            if (sw.ElapsedMilliseconds > 20000) throw new TimeoutException(string.Join(" | ", flip.Trace));
            Thread.Sleep(2);
        }
        Assert.True(seen, string.Join(" | ", flip.Trace));
        AssertLockedExactly(m, "FlipBlock", new string('0', 21));                     // ~FlipBlockAction removed it
        Assert.Equal("FlipBlock", Cozmo.Robot.Manipulation.FlipBlockAction.ReactionLockName);
        Assert.Equal("0xC56AE0", Cozmo.Robot.Manipulation.FlipBlockAction.ReactionLockTable.TableAddress);
    }

    /// <summary>
    /// M8-003 / M7-014: PrepareForKnockOverAttempt 0x005C3780 calls IncreaseScoreWhileActing(10.0f) (PLT 0x4B01BC, r1 = 0x41200000) at 0x005C37A8, which adds to +0x104 while an action handle is
    /// set (0x005BF02C..0x005BF03E).
    /// </summary>
    [Fact]
    public void PrepareForKnockOverAttemptRaisesTheRunningScoreByTenWhileActing()
    {
        if (!NeedsLibrary()) return;
        using var rig = new Rig();
        rig.Head = 0.12f;
        rig.Cube = CubeAt(260, 0);
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, CubeAt(260, 0, 0, 66)));
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE3, CubeAt(260, 0, 0, 110)));
        Assert.Equal(3, rig.Frame().Objects.Count);
        var ctx = Ctx(rig);
        var b = new KnockOverCubesBehavior(rig.M, "KnockOverCubes", 3) { AlwaysStreamline = true };
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        // the first Prepare ran before any StartActing, with the handle clear (+0x84 == 0): nothing was added; now an action handle is set
        Assert.False(b.HasCurrentAction);
        Assert.NotEqual(0, (int)typeof(SteppedBehavior).GetMethod("StartActing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public, null, Type.EmptyTypes, null)!.Invoke(b, null)!);
        Assert.True(b.HasCurrentAction);
        double before = b.RunningScoreBonus;
        typeof(KnockOverCubesBehavior).GetMethod("PrepareForKnockOverAttempt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(b, null);
        Assert.Equal(before + (double)BitConverter.Int32BitsToSingle(0x41200000), b.RunningScoreBonus);
        b.Stop(BehaviorStopReason.Interrupted);
    }

    /// <summary>
    /// M7-014: IBehavior::SmartRemoveDisableReactionsLock 0x005BD470..0x005BD4AA has no held test and no VERIFY: it always asks the manager to remove name + "_behaviorLock" (0x005BD48C) and then
    /// erases the name. PrepareForKnockOverAttempt's first call removes a lock nothing has taken.
    /// </summary>
    [Fact]
    public void RemovingALockThatWasNeverTakenRaisesNoVerifyAndStillAsksTheManager()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        using var m = new BehaviorManager(ctx);
        var scope = new BehaviorScope(manager: m);
        var verify = new List<string>();
        scope.VerifyFailed += verify.Add;
        Assert.False(scope.SmartRemoveDisableReactionsLock("never"));
        Assert.Empty(verify);
        // a held lock placed on the manager under the name + "_behaviorLock" is removed by the call whether or not the scope's set held it
        var table = new ReactionLockTable("t", "0x0", new[] { (int)ReactionTrigger.CliffDetected });
        m.AddReaction(new GenericReactionStrategy(ReactionTrigger.CliffDetected, "t", () => 0), new Fake());
        m.DisableReactionsWithLock("never_behaviorLock", table);
        Assert.True(m.HasDisableLock(ReactionTrigger.CliffDetected, "never_behaviorLock"));
        scope.SmartRemoveDisableReactionsLock("never");
        Assert.False(m.HasDisableLock(ReactionTrigger.CliffDetected, "never_behaviorLock"));
    }

    // =============================================================== M8-001: the spark lock inputs

    /// <summary>
    /// M8-001 / M7-014: IBehavior::Init 0x005BCD16..0x005BCD44 compares this+0x70 (the behaviour's unlock id) with [[this+0x2C]+0x44]+0x58 (the BehaviorManager's active spark, an UnlockId written
    /// by SwitchToRequestedSpark 0x005A4220/0x005A4224 from the requested spark +0x60 and by 0x005A4FDE). This stack keeps the requested spark as a name string and keeps neither the UnlockId
    /// enum nor the behaviours' +0x70, so both inputs stay unset in production and the gap is reported, not answered.
    /// </summary>
    [Fact]
    public void TheSparkLockInputsHaveNoProductionSourceAndStayReported()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var reported = new List<string>();
        void On(string s) => reported.Add(s);
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            var b = new PickUpCubeBehavior(rig.M);
            Assert.Null(b.UnlockIdentifier);
            Assert.Null(b.ActiveSparkIdentifier);
            var scope = new BehaviorScope(manager: null);
            b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
            Assert.Contains(reported, r => r.Contains("SparkBehaviorDisables") && r.Contains("0x005BCD16"));
            Assert.DoesNotContain("SparkBehaviorDisables", scope.HeldReactionLocks);
        }
        finally { SteppedBehavior.MissingReported -= On; }
    }

    // =============================================================== M8-007 / M8-009: the unnamed track lock

    /// <summary>
    /// M8-007 / M8-009: BehaviorPlayArbitraryAnim is BehaviorPlayAnimSequence's subclass (constructor 0x005C0784 calls its constructor through PLT 0x4B2BF8); its InitInternal 0x005C0896 is
    /// [this+0x13C] = 0, BehaviorPlayAnimSequence::StartPlayingAnimations (PLT 0x4B2B38), return 0: no track lock of its own. Starting it claims no tracks on the scope.
    /// </summary>
    [Fact]
    public void PlayArbitraryAnimTakesNoTrackLockOfItsOwn()
    {
        var root = AssetsRoot();
        if (root is null) { if (WithoutAssets()) return; throw new Xunit.Sdk.XunitException("the unpacked OBB (re-analysis/obb) is missing; provide it or set COZMO_TESTS_WITHOUT_ASSETS=1 for a run that knowingly has none"); }
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var lib = rig.Robot.Animations.LoadFrom(root);
        string clipName = lib.ClipNames.OrderBy(n => n, StringComparer.Ordinal).First(n => lib.GetClip(n).Tracks != AnimationTrack.None);
        var b = new PlayArbitraryAnimBehavior { ClipName = clipName };
        var scope = new BehaviorScope(motion: rig.Robot.Motion);
        var reported = new List<string>();
        void On(string s) => reported.Add(s);
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
            Assert.Equal(AnimationTrack.None, scope.LockedTracks);
            Assert.Equal(0, rig.Robot.Motion.LockedTracks);
            Assert.Contains(reported, r => r.Contains("PlayArbitraryAnim") && r.Contains("0x005C0896"));
            Assert.DoesNotContain(reported, r => r.Contains("BehaviorScope.LockTracks"));
        }
        finally { SteppedBehavior.MissingReported -= On; b.Stop(BehaviorStopReason.Interrupted); scope.Dispose(); }
    }

    // =============================================================== M7-008: the streamer's clock

    private sealed class Port : IEngineTransport
    {
        public bool TimedOut { get; set; }
        public event Action<ReceiverEvent>? Received { add { } remove { } }
        public void Start() { }
        public void Connect(IPAddress ip, bool isSimulated) { }
        public void Disconnect(IPEndPoint address) { }
        public void SendData(byte[] clad) { }
    }

    /// <summary>
    /// M7-008: on a production engine the stream timeline's clock is <c>BaseStationTimer::GetCurrentTimeStamp</c> (PLT 0x4A7B58 -> 0x0084BCC0, called by InitStream 0x0057B696 and UpdateStream
    /// 0x0057C87A): whole milliseconds, (u32)(double seconds * 1000). The expected values are that formula applied by hand to chosen nanosecond values (the tick is stopped so the timer is ours):
    /// 1_999_999_999 ns = 1.999999999 s -> 1999 ms; 61_234_567_891 ns -> 61234 ms; 86_400_999_999_999 ns (a day) -> 86400999 ms.
    /// </summary>
    [Fact]
    public void OnAProductionEngineTheStreamClockIsTheBaseStationTimersWholeMilliseconds()
    {
        using var robot = CozmoRobot.CreateForTest(new Port(), EngineTickRunner.HostNowNs, new CozmoEngineOptions { BlockPoolPath = "" });
        robot.Engine.StartProduction();
        Assert.True(robot.Animations.EngineDriven);
        robot.Engine.StopTick();
        (long Ns, double Ms)[] cases = { (1_999_999_999L, 1999.0), (61_234_567_891L, 61234.0), (86_400_999_999_999L, 86400999.0) };
        foreach (var (ns, ms) in cases)
        {
            robot.Engine.Timer.UpdateTime(ns);
            Assert.Equal(ms, robot.Animations.ClockMs);
        }
    }

    /// <summary>
    /// M7-008: AnimationStreamer::Update's keep-alive gate and the +0x88 stamps use <c>GetCurrentTimeInSeconds</c> (a float, PLT 0x4A50B0; 0x0057D02E, 0x0057D43E, 0x0057CF86), not the
    /// integer-millisecond stream time: <c>Advance</c> keeps the float it is given.
    /// </summary>
    [Fact]
    public void TheSchedulerKeepsTheEnginesFloatSecondsForItsGateAlongsideTheIntegerMilliseconds()
    {
        using var rig = new Rig();
        var field = typeof(AnimationScheduler).GetField("_nowSecF", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        rig.Robot.Animations.Scheduler.Advance(1999, 1.5f);
        Assert.Equal(1.5f, (float)field.GetValue(rig.Robot.Animations.Scheduler)!);
        rig.Robot.Animations.Scheduler.Advance(2000);                 // offline: derived from the milliseconds
        Assert.Equal(2.0f, (float)field.GetValue(rig.Robot.Animations.Scheduler)!);
    }

    /// <summary>The offline seams have no engine tick (the timer never advances), so there the process clock stands in, as before.</summary>
    [Fact]
    public void OfflineTheAnimationClockIsTheProcessClock()
    {
        using var rig = new Rig();
        Assert.False(rig.Robot.Animations.EngineDriven);
        double before = Environment.TickCount64;
        double c = rig.Robot.Animations.ClockMs;
        double after = Environment.TickCount64;
        Assert.InRange(c, before, after);
    }
}
