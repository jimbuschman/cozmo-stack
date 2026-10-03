using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// R-BEH2 batch 3a: M7-003 (ReactToImpact), M7-014 (lock tables and install points), M7-015 / M7-019 / M7-021 (ReactToPickup, ReactToCliff,
/// the state-name helper). Every expected value is taken from a row of the approved inventory (M7-behaviour.md Correction A3 and the
/// gap-pass-1 extraction 20261002-R-BEH2-M7-gap1-extraction.md; cited per test), not from what the code returns.
/// </summary>
public class M7BatchThreeATests
{
    private static BehaviorContext Ctx(Rig rig, Func<NeedId?>? need = null) => new()
    {
        Robot = rig.Robot,
        Triggers = new AnimationTriggerMap(),
        Random = new Random(3),
        AiExpressedNeedValue = need,
    };

    /// <summary>The engine's float: <c>(raw bits)</c>.</summary>
    private static float F(int bits) => BitConverter.Int32BitsToSingle(bits);

    private static void SendState(Rig rig, uint flags, ushort cliff0 = 0)
    {
        rig.T += 33;
        rig.Send(new RobotState
        {
            Timestamp = rig.T, PoseOriginId = rig.OriginId, Pose = new RobotPose { X = rig.X, Y = rig.Y, Angle = rig.Angle }, HeadAngle = rig.Head,
            LiftAngle = 0f, Status = flags, Accel = new AccelData { Z = 9800 }, Gyro = new GyroData(),
            CliffDataRaw = new ushort[] { cliff0, 0, 0, 0 },
        });
    }

    /// <summary>The head reports calibrated, then pick-up states: the classifier commits InAir (robot+0x355 == 1), as M7BehaviorTests does.</summary>
    private static void PickUp(Rig rig, bool onChargerContacts = false)
    {
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false, AutoStarted = false });
        for (int i = 0; i < 40; i++) rig.State();
        uint flags = (uint)RobotStatusFlag.IsPickedUp | (onChargerContacts ? (uint)RobotStatusFlag.IsOnCharger : 0);
        for (int i = 0; i < 10; i++) SendState(rig, flags);
        Assert.Equal(OffTreadsState.InAir, rig.Robot.Sensors.OffTreadsState);
        Assert.Equal(onChargerContacts, rig.Robot.Sensors.OnChargerContacts);
    }


    // =============================================================== M7-014: the tables

    /// <summary>
    /// M7-014 (gap pass 1 section 3.3): the five tables and the Init/Resume table, each as the 21 value bytes in trigger order (CliffDetected,
    /// CubeMoved, FacePositionUpdated, FistBump, Frustration, Hiccup, MotorCalibration, NoPreDockPoses, ObjectPositionUpdated, PlacedOnCharger,
    /// PetInitialDetection, RobotFalling, RobotPickedUp, RobotPlacedOnSlope, ReturnedToTreads, RobotOnBack, RobotOnFace, RobotOnSide, RobotShaken,
    /// Sparked, UnexpectedMovement); a non-zero byte disables (0x005A283C..0x005A2846). The mask strings are the extraction's.
    /// </summary>
    public static readonly (string Name, string Address, string Mask)[] GapPassTables =
    {
        ("ReactToImpact", "0xC73D36", "111111111011111111011"),
        ("DriveOffCharger", "0xC672F0", "111001001100000000001"),
        ("Singing", "0xC6F590", "011000001010000000001"),
        ("AcknowledgeCubeMoved", "0xC72BB2", "000000001000000000000"),
        ("ReactToOnCharger", "0xC74182", "011111011010000000011"),
        ("SparkBehaviorDisables", "0xC65F90", "000000001000000000000"),
    };

    private static ReactionLockTable TableOf(string name) => name switch
    {
        "ReactToImpact" => ReactionLockTables.ReactToImpact,
        "DriveOffCharger" => ReactionLockTables.DriveOffCharger,
        "Singing" => ReactionLockTables.Singing,
        "AcknowledgeCubeMoved" => ReactionLockTables.AcknowledgeCubeMoved,
        "ReactToOnCharger" => ReactionLockTables.ReactToOnCharger,
        "SparkBehaviorDisables" => ReactionLockTables.SparkBehaviorDisables,
        "ReactToCliff" => ReactionLockTables.ReactToCliff,
        _ => throw new ArgumentException(name),
    };

    private static string MaskString(ReactionLockTable t) => new(t.ToMask().Select(b => b ? '1' : '0').ToArray());

    [Fact]
    public void TheFiveClassTablesAndTheSparkTableAreTheShippedBytes()
    {
        foreach (var (name, address, mask) in GapPassTables)
        {
            var t = TableOf(name);
            Assert.Equal(address, t.TableAddress);
            Assert.Equal(21, t.ToMask().Length);
            Assert.Equal(mask, MaskString(t));
        }
        // ReactToCliff's table (0x00C73746) is the one that already stood: 011000001000000000001
        Assert.Equal("011000001000000000001", MaskString(ReactionLockTables.ReactToCliff));
    }

    /// <summary>
    /// M7-014: a class takes its table through <c>SmartDisableReactionsWithLock</c> at its <c>InitInternal</c>: the manager holds the lock
    /// <c>name + "_behaviorLock"</c> on exactly the triggers whose byte is 1 (0x005BCE4C..0x005BCE62), and <c>IBehavior::Stop</c> releases it
    /// (0x005BD12A..0x005BD140). Driven through the manager's own <c>StartAsync</c>/<c>Stop</c>.
    /// </summary>
    private static void AssertLockedExactly(BehaviorManager m, string lockName, string mask)
    {
        for (int t = 0; t < 21; t++)
            Assert.Equal(mask[t] == '1', m.HasDisableLock((ReactionTrigger)t, lockName));
    }

    private static BehaviorManager NewManager(BehaviorContext ctx, params IBehavior[] behaviours)
    {
        var m = new BehaviorManager(ctx);
        foreach (var b in behaviours) m.Add(b);
        return m;
    }

    [Fact]
    public void EachBuiltClassTakesItsTableAtInitAndReleasesItAtStop()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig, () => null);
        var charger = new ReactToOnChargerBehavior("ReactToOnCharger");
        var impact = new ReactToImpactBehavior("ReactToImpact");
        var driveOff = new DriveOffChargerBehavior(rig.M, "DriveOffCharger");
        var singing = new SingingBehavior("Singing_Bingo", "Cozmo_Sings_100Bpm", "Cozmo_Sings_Bingo");
        var cubeMoved = new AcknowledgeCubeMovedBehavior(null) { TargetObjectId = 5 };
        var cliff = new ReactToCliffBehavior();
        using var m = NewManager(ctx, charger, impact, driveOff, singing, cubeMoved, cliff);

        var cases = new (IBehavior B, string Table)[]
        {
            (charger, "ReactToOnCharger"), (impact, "ReactToImpact"), (driveOff, "DriveOffCharger"),
            (singing, "Singing"), (cubeMoved, "AcknowledgeCubeMoved"), (cliff, "ReactToCliff"),
        };
        foreach (var (b, table) in cases)
        {
            Assert.True(m.StartAsync(b.Id, 0).GetAwaiter().GetResult(), b.Id);
            AssertLockedExactly(m, b.Id + "_behaviorLock", MaskString(TableOf(table)));
            m.Stop(BehaviorStopReason.Interrupted, 0);
            AssertLockedExactly(m, b.Id + "_behaviorLock", new string('0', 21));
            // the released lock re-enables every trigger
            Assert.All(Enum.GetValues<ReactionTrigger>(), t => Assert.True(m.IsReactionTriggerEnabled(t), $"{b.Id} {t}"));
        }
    }

    /// <summary>
    /// M7-014 (gap pass 1 section 3.3, "IBehavior::Init/Resume lock"): when this+0x70 is not 0x55 and equals the manager's active spark
    /// (+0x58), the lock "SparkBehaviorDisables" is taken with table 0x00C65F90 (only ObjectPositionUpdated); 0x55 or a mismatch takes
    /// nothing. The ids are the spark layer's (M15): unset, the gap is reported and nothing is locked.
    /// </summary>
    [Fact]
    public void TheSparkBehaviorDisablesLockFollowsTheTwoIdsAndReportsTheMissingSource()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var reported = new List<string>();
        void On(string s) => reported.Add(s);
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            var b = new ReactToImpactBehavior("ReactToImpact");
            using var m = NewManager(ctx, b);
            const string spark = "SparkBehaviorDisables_behaviorLock";
            // unset: reported, nothing locked
            m.StartAsync("ReactToImpact", 0).GetAwaiter().GetResult();
            Assert.Contains(reported, r => r.Contains("SparkBehaviorDisables"));
            AssertLockedExactly(m, spark, new string('0', 21));
            m.Stop(BehaviorStopReason.Interrupted, 0);

            // 0x55: no spark
            b.UnlockIdentifier = () => 0x55; b.ActiveSparkIdentifier = () => 0x55;
            m.StartAsync("ReactToImpact", 0).GetAwaiter().GetResult();
            AssertLockedExactly(m, spark, new string('0', 21));
            m.Stop(BehaviorStopReason.Interrupted, 0);

            // a different active spark: nothing
            b.UnlockIdentifier = () => 7; b.ActiveSparkIdentifier = () => 9;
            m.StartAsync("ReactToImpact", 0).GetAwaiter().GetResult();
            AssertLockedExactly(m, spark, new string('0', 21));
            m.Stop(BehaviorStopReason.Interrupted, 0);

            // the active spark: ObjectPositionUpdated only (000000001000000000000)
            b.UnlockIdentifier = () => 7; b.ActiveSparkIdentifier = () => 7;
            m.StartAsync("ReactToImpact", 0).GetAwaiter().GetResult();
            AssertLockedExactly(m, spark, "000000001000000000000");
            m.Stop(BehaviorStopReason.Interrupted, 0);
            AssertLockedExactly(m, spark, new string('0', 21));
        }
        finally { SteppedBehavior.MissingReported -= On; }
    }

    // =============================================================== M7-003: ReactToImpact

    [Fact]
    public void ReactToImpactSubscribesTheThreeTagsOfTheTable()
    {
        // 0x00C73D30: u16 {0x3a, 0x3b, 0x1e}
        Assert.Equal(0x1A0, (int)AnimationTrigger.ReactToImpact);   // movw r2,#0x1a0 at 0x0060637E
        Assert.Equal(5.0, ReactToImpactBehavior.CalibrationWaitSec);   // 0x40a00000
        Assert.Equal(60.0, ReactToImpactBehavior.AnimationTimeoutSec); // movt r0,#0x4270
        Assert.Equal(0x447A0000, BitConverter.SingleToInt32Bits(ReactionTable.ImpactIntensityThreshold));   // the 1000.0 literal at 0x00606470
    }

    /// <summary>M7-003 A3: AlwaysHandle's three tags, and the unconditional 0/1 store of +0x11e on every FallingStopped (0x0060643C..0x0060644E).</summary>
    [Fact]
    public void AlwaysHandleKeepsTheThreeBytesInEveryState()
    {
        var b = new ReactToImpactBehavior("ReactToImpact");
        b.HandleFallingStopped(1500f);                                // +0x11d = 1, +0x11e = 1
        Assert.True(b.FallingStoppedSeen); Assert.True(b.ImpactRecorded);
        b.HandleFallingStopped(999f);                                 // +0x11e stored 0: the store is unconditional
        Assert.True(b.FallingStoppedSeen); Assert.False(b.ImpactRecorded);
        b.HandleFallingStopped(1000f);                                // not greater than 1000.0f
        Assert.False(b.ImpactRecorded);
        b.HandleFallingStopped(float.NaN);                            // vcmpe with a NaN is not "gt"
        Assert.False(b.ImpactRecorded);
        b.HandleFallingStopped(1000.01f);
        Assert.True(b.ImpactRecorded);

        b.HandleMotorCalibration(true, false);                        // both must hold
        Assert.False(b.CalibratedRecorded);
        b.HandleMotorCalibration(false, true);
        Assert.False(b.CalibratedRecorded);
        b.HandleMotorCalibration(true, true);
        Assert.True(b.CalibratedRecorded);
        b.HandleMotorCalibration(false, false);                       // a later report does not clear +0x11c
        Assert.True(b.CalibratedRecorded);

        b.HandleFallingStarted();                                     // 0x00606422 strb +0x11e, 0x00606426 strh +0x11c/+0x11d
        Assert.False(b.ImpactRecorded); Assert.False(b.CalibratedRecorded); Assert.False(b.FallingStoppedSeen);
    }

    /// <summary>M7-003 A3: IsRunnableInternal returns 1 (0x00606486); the wait action is always built; its predicate is +0x11d &amp;&amp; +0x11c; the 5.0 s timeout.</summary>
    [Fact]
    public void ReactToImpactIsAlwaysRunnableAndAlwaysWaitsOnTheTwoFlags()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        // the stack's IsRunnable also needs the animation library; the gate under test is IsRunnableInternal = 1
        var obb = ObbRoot();
        if (obb is not null) rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var never = new ReactToImpactBehavior("ReactToImpact");
        if (obb is not null) Assert.True(never.IsRunnable(ctx));      // no FallingStopped, no impact, not calibrated: still runnable

        // +0x11d only (FallingStopped seen, not calibrated): the AND is false, so the wait runs to its 5 s
        var b = new ReactToImpactBehavior("ReactToImpact");
        b.HandleFallingStopped(2500f);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Contains(b.Trace, l => l.Contains("up to 5.0 s"));    // the wait action is built whatever the flags
        Assert.True(b.Update(ctx, 0));                                // arms the deadline at 0 + 5000
        Assert.True(b.Update(ctx, 4900));                             // predicate false, 5 s not up
        Assert.DoesNotContain(b.Trace, l => l.StartsWith("ReactToImpact:") || l.StartsWith("play ReactToImpact"));
        b.Update(ctx, 5100);                                          // timeout: the callback runs anyway; +0x11e is set, so the animation is started
        Assert.Contains(b.Trace, l => l.StartsWith("ReactToImpact:") || l.StartsWith("play ReactToImpact"));
        Assert.True(b.WaitedOut);

        // +0x11c only (calibrated, no FallingStopped): also false
        var c = new ReactToImpactBehavior("ReactToImpact");
        c.HandleMotorCalibration(true, true);
        c.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        c.Update(ctx, 0);
        Assert.True(c.Update(ctx, 4900));
        c.Update(ctx, 5100);
        Assert.True(c.WaitedOut);
    }

    /// <summary>
    /// M7-003 A3: both flags set, the wait ends at once; +0x11e clear means TransitionToPlayingAnim does nothing (no animation, no 5 s), set means
    /// TriggerAnimationAction 0x1a0.
    /// </summary>
    [Fact]
    public void ReactToImpactPlaysOnlyWhenTheImpactByteIsSet()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);

        var soft = new ReactToImpactBehavior("ReactToImpact");
        soft.HandleFallingStopped(500f); soft.HandleMotorCalibration(true, true);
        soft.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.False(soft.Update(ctx, 0));                            // the predicate holds on the first tick; +0x11e clear: nothing to play; the behaviour ends
        Assert.False(soft.WaitedOut);
        Assert.DoesNotContain(soft.Trace, l => l.StartsWith("ReactToImpact:") || l.StartsWith("play ReactToImpact"));

        var hard = new ReactToImpactBehavior("ReactToImpact");
        hard.HandleFallingStopped(2500f); hard.HandleMotorCalibration(true, true);
        hard.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        hard.Update(ctx, 0);
        Assert.Contains(hard.Trace, l => l.StartsWith("ReactToImpact:") || l.StartsWith("play ReactToImpact"));
        Assert.False(hard.WaitedOut);

        // StopInternal 0x006063FC clears +0x11c, +0x11d, +0x11e
        hard.Stop(BehaviorStopReason.Interrupted);
        Assert.False(hard.ImpactRecorded); Assert.False(hard.CalibratedRecorded); Assert.False(hard.FallingStoppedSeen);
    }

    /// <summary>
    /// M7-003 through the live entry: a soft landing fires the reaction (always runnable; the old test asserted that it did not), the manager's
    /// ticks run the wait, the head and lift report calibrated, +0x11e is clear so no animation plays, and the reaction ends.
    /// </summary>
    [Fact]
    public void ASoftLandingNowRunsTheReactionThroughTheManagerAndPlaysNothing()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = AnimationTriggerMap.Load(obb), Random = new Random(5) };
        using var manager = new BehaviorManager(ctx);
        var reg = ShippedBehaviors.Reactions(rig.Robot, clockSec: () => rig.Clock.NowMs / 1000.0, vision: rig.Vision, obbRoot: obb)
                                  .Single(r => r.Strategy.Trigger == ReactionTrigger.RobotFalling);
        manager.AddReaction(reg.Strategy, reg.Behavior);
        manager.RemoveDisableReactionsLock("sdk");          // open the C3 sticky gate
        var impact = (ReactToImpactBehavior)reg.Behavior;

        rig.Clock.Advance(4000);
        rig.Send(new FallingStarted { Unknown = 1000 });
        rig.Send(new FallingStopped { DurationMs = 100, ImpactIntensity = 500f });
        Assert.True(impact.FallingStoppedSeen);
        Assert.False(impact.ImpactRecorded);
        var fired = manager.CheckReactions(1);
        Assert.NotNull(fired);                                // the old test asserted Null: contradicted by 0x00606486
        Assert.Equal("ReactToImpact", fired!.Behavior);
        // the lock 0x00C73D36 is held for the reaction (0x00606216)
        AssertLockedExactly(manager, "ReactToImpact_behaviorLock", "111111111011111111011");

        manager.Update(0, 2);                                 // arms the wait's deadline; +0x11c not yet set
        Assert.Same(impact, manager.Current);
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false, AutoStarted = false });   // MotorCalibration (tag 0x1e) with both
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false, AutoStarted = false });   // motors calibrated: +0x11c = 1
        Assert.True(impact.CalibratedRecorded);
        manager.Update(100, 2.1);                             // predicate holds; +0x11e clear: nothing plays and the behaviour completes
        Assert.DoesNotContain(impact.Trace, l => l.StartsWith("play ReactToImpact"));
        Assert.Null(manager.Current);
        AssertLockedExactly(manager, "ReactToImpact_behaviorLock", new string('0', 21));
    }

    // =============================================================== M7-021: the state-name helper

    /// <summary>
    /// M7-021 (gap pass 1 section 1.1): the helper logs "Behavior:%s, FromState:%s ToState:%s" (channel Behaviors, event Behavior.TransitionToState)
    /// with the BehaviorID name, the previous +0x58 value and the new one, and then stores the new one. Driven through ReactToCliff's live
    /// path: PlayingStopReaction (0x00604F90), then PlayingCliffReaction (0x00605110).
    /// </summary>
    [Fact]
    public void TheStateNameHelperLogsTheTransitionAndStoresTheName()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig, () => null);                       // [[[robot+0x264]+0x30]+0x14] == 3: none
        var b = new ReactToCliffBehavior(rig.Robot);
        var lines = new List<string>();
        rig.Robot.Engine.LogLine += lines.Add;
        using var m = NewManager(ctx, b);
        Assert.True(m.StartAsync("ReactToCliff", 0).GetAwaiter().GetResult());
        Assert.Equal("", b.StateName);                        // empty from the constructor
        for (int t = 0; t < 8 && m.Current is not null; t++) m.Update(t * 100, t * 0.1);
        string transition(string from, string to) => $"info: [Behaviors] Behavior.TransitionToState: Behavior:ReactToCliff, FromState:{from} ToState:{to}";
        var states = b.Trace.Where(l => l.Contains("Behavior.TransitionToState")).ToList();
        Assert.Equal(new[] { transition("", "PlayingStopReaction"), transition("PlayingStopReaction", "PlayingCliffReaction") }, states);
        Assert.Contains(transition("", "PlayingStopReaction"), lines);
        Assert.Equal("PlayingCliffReaction", b.StateName);
    }

    /// <summary>
    /// M7-021: the other built callers' strings. AcknowledgeCubeMoved: "PlayingSenseReaction" (0x0060241C) then
    /// "TurningToLastLocationOfBlock" (0x00602298). DriveOffCharger: "DrivingForward" (0x005C0BE2).
    /// </summary>
    [Fact]
    public void TheOtherBuiltCallersPassTheirStrings()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var cube = new AcknowledgeCubeMovedBehavior(null) { TargetObjectId = 5 };
        using (var m = NewManager(ctx, cube))
        {
            m.StartAsync(cube.Id, 0).GetAwaiter().GetResult();
            for (int t = 0; t < 6 && m.Current is not null; t++) m.Update(t * 100, t * 0.1);
        }
        var s = cube.Trace.Where(l => l.Contains("Behavior.TransitionToState")).Select(l => l[(l.IndexOf("FromState:", StringComparison.Ordinal))..]).ToList();
        Assert.Equal(new[] { "FromState: ToState:PlayingSenseReaction", "FromState:PlayingSenseReaction ToState:TurningToLastLocationOfBlock" }, s);

        var drive = new DriveOffChargerBehavior(rig.M, "DriveOffCharger");
        using (var m = NewManager(ctx, drive))
        {
            m.StartAsync(drive.Id, 0).GetAwaiter().GetResult();
            m.Stop(BehaviorStopReason.Interrupted, 0);
        }
        Assert.Contains(drive.Trace, l => l.Contains("Behavior:DriveOffCharger, FromState: ToState:DrivingForward"));
    }

    /// <summary>M7-021 (R9): the Robot::Update reader of +0x58 (robot+0x4C, VizManager::SetText / SetSdkStatus) is M11/M12 and reported, not built.</summary>
    [Fact]
    public void TheRobotUpdateReaderOfTheStateNameIsReportedMissing()
    {
        using var rig = new Rig();
        var reported = new List<string>();
        void On(string s) => reported.Add(s);
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            var b = new ReactToCliffBehavior();
            b.StartAsync(Ctx(rig, () => null), new BehaviorScope(), default).GetAwaiter().GetResult();
            b.Update(Ctx(rig, () => null), 0);
            Assert.Contains(reported, r => r.Contains("robot+0x4C") && r.Contains("VizManager::SetText") && r.Contains("SetSdkStatus"));
        }
        finally { SteppedBehavior.MissingReported -= On; }
    }

    // =============================================================== M7-019: ReactToCliff

    [Fact]
    public void TheCliffConstantsAreTheEnginesBits()
    {
        Assert.Equal(0x42700000, BitConverter.SingleToInt32Bits(ReactToCliffBehavior.AnimationTimeout));          // 60.0f
        Assert.Equal(0x3F0CCCCD, BitConverter.SingleToInt32Bits(ReactToCliffBehavior.StopWaitTimeout));           // 0.55f
        Assert.Equal(unchecked((int)0xC2700000), BitConverter.SingleToInt32Bits(ReactToCliffBehavior.BackUpDistanceMm));   // -60.0f
        Assert.Equal(0x42C80000, BitConverter.SingleToInt32Bits(ReactToCliffBehavior.BackUpSpeedMmps));           // 100.0f
        Assert.Equal(0x7F7FFFFF, BitConverter.SingleToInt32Bits(ReactToCliffBehavior.InitWaitTimeout));           // FLT_MAX
        Assert.Equal(0x1C, ReactToCliffBehavior.ObjectiveReactedToCliff);
        Assert.Equal("CliffReact", ReactToCliffBehavior.EmotionEventName);
        // the animation triggers (movw immediates at 0x00604FE4, 0x0060518E, 0x0060519A, 0x006051A2)
        Assert.Equal(0x19E, (int)AnimationTrigger.ReactToCliffDetectorStop);
        Assert.Equal(0x19D, (int)AnimationTrigger.ReactToCliff);
        Assert.Equal(0x13D, (int)AnimationTrigger.NeedsSevereLowRepairCliffReact);
        Assert.Equal(0x131, (int)AnimationTrigger.NeedsSevereLowEnergyCliffReact);
    }

    /// <summary>
    /// B1: [[[robot+0x264]+0x30]+0x14] is the SevereNeedsComponent's current severe NeedId, 3 (none) from its constructor (0x00572A0E). This stack has no
    /// component with the engine's writers, so unset the value is 3 and reported once; it must never throw. Value 3 takes the stop reaction (sev >= 2)
    /// and then the else-branch trigger 0x19D.
    /// </summary>
    [Fact]
    public void WithoutTheSevereNeedSeamTheValueIsThreeAndReported()
    {
        using var rig = new Rig();
        var reported = new List<string>();
        void On(string x) => reported.Add(x);
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            var b = new ReactToCliffBehavior(rig.Robot);
            var ctx = Ctx(rig);                                     // AiExpressedNeedValue unset
            b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
            for (int t = 0; t < 10; t++) b.Update(ctx, t * 100);
            Assert.Contains(b.Trace, l => l.StartsWith("ReactToCliffDetectorStop:"));
            Assert.Contains(b.Trace, l => l.StartsWith("ReactToCliff:"));
            Assert.Single(reported, r => r.Contains("[[robot+0x264]+0x30]+0x14") && r.Contains("SevereNeedsComponent"));   // once, not per read
        }
        finally { SteppedBehavior.MissingReported -= On; }
    }

    private static FreeplayStack NewStack(Rig rig, string obb, BehaviorContext ctx, Func<double> clock) =>
        FreeplayStack.Create(obb, rig.Robot, ctx, clock, rig.Vision, rig.M, random: new Random(1));

    /// <summary>
    /// B1 through the production context (FreeplayStack.Create, nothing sets the seam): a cliff event fires through the manager and Tick runs the 0x19E
    /// stop reaction and then the 0x19D animation, with no exception.
    /// </summary>
    [Fact]
    public void ACliffEventThroughTheFreeplayStackPlaysTheStopReactionThenTheCliffAnimation()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        double clock = 0;
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Random = new Random(5) };
        using var stack = NewStack(rig, obb, ctx, () => clock);
        Assert.Null(ctx.AiExpressedNeedValue);
        stack.Manager.RemoveDisableReactionsLock("sdk");
        var cliff = (ReactToCliffBehavior)stack.Manager.Reactions.Single(r => r.Strategy.Trigger == ReactionTrigger.CliffDetected).Behavior;
        rig.Clock.Advance(4000);
        rig.Send(new CliffEvent { Timestamp = rig.T, DetectedFlags = 1 });
        rig.Send(new Cozmo.Protocol.RobotStopped { Field0 = 0 });          // the stop arrives: +0x11C = 0, so Init takes state 0
        for (int i = 1; i <= 12; i++)
        {
            clock = i * 0.1;
            stack.Tick(clock, clock * 1000, rig.Robot, rig.Vision, rig.M);
        }
        Assert.Contains(cliff.Trace, l => l.Contains("ToState:PlayingStopReaction"));
        int stop = cliff.Trace.ToList().FindIndex(l => l.StartsWith("ReactToCliffDetectorStop:"));
        int anim = cliff.Trace.ToList().FindIndex(l => l.StartsWith("ReactToCliff:"));
        Assert.True(stop >= 0 && anim > stop);
    }

    /// <summary>B1: with the seam at 0 and 1 the stop reaction is skipped (sev &lt; 2) and the trigger is 0x13D / 0x131 (the same through the stack's context).</summary>
    [Theory]
    [InlineData(NeedId.Repair, "NeedsSevereLowRepairCliffReact")]
    [InlineData(NeedId.Energy, "NeedsSevereLowEnergyCliffReact")]
    public void ASevereNeedSkipsTheStopReactionAndPicksItsTrigger(NeedId need, string trigger)
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        double clock = 0;
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Random = new Random(5), AiExpressedNeedValue = () => need };
        using var stack = NewStack(rig, obb, ctx, () => clock);
        stack.Manager.RemoveDisableReactionsLock("sdk");
        var cliff = (ReactToCliffBehavior)stack.Manager.Reactions.Single(r => r.Strategy.Trigger == ReactionTrigger.CliffDetected).Behavior;
        rig.Clock.Advance(4000);
        rig.Send(new Cozmo.Protocol.RobotStopped { Field0 = 0 });
        rig.Send(new CliffEvent { Timestamp = rig.T, DetectedFlags = 1 });
        for (int i = 1; i <= 12; i++)
        {
            clock = i * 0.1;
            stack.Tick(clock, clock * 1000, rig.Robot, rig.Vision, rig.M);
        }
        Assert.Contains(cliff.Trace, l => l.StartsWith(trigger + ":"));
        Assert.DoesNotContain(cliff.Trace, l => l.Contains("ToState:PlayingStopReaction"));
        Assert.DoesNotContain(cliff.Trace, l => l.StartsWith("ReactToCliffDetectorStop:"));
    }

    /// <summary>Q3: Sensors raises ChargerEvent{1} on SetOnCharger's rising edge and {0} on the falling edge; ReactToCliff's HandleWhileRunning sets +0x125 on {1}.</summary>
    [Fact]
    public void TheChargerEventComesFromTheContactsEdges()
    {
        using var rig = new Rig();
        var events = new List<bool>();
        rig.Robot.Sensors.ChargerEvent += events.Add;
        var b = new ReactToCliffBehavior(rig.Robot);
        b.StartAsync(Ctx(rig, () => null), new BehaviorScope(), default).GetAwaiter().GetResult();
        rig.State((uint)RobotStatusFlag.IsOnCharger);
        rig.State((uint)RobotStatusFlag.IsOnCharger);          // no edge
        Assert.Equal(new[] { true }, events);
        Assert.True(b.FinishRequested);
        rig.State(0);
        Assert.Equal(new[] { true, false }, events);
    }

    /// <summary>
    /// M7-019 (section 5.1, HandleWhileNotRunning 0x0060541C): RobotStopped clears +0x11C and +0x124; a CliffEvent with detected 0 does nothing;
    /// with detected non-zero and +0x124 clear it logs CliffWithoutStop and sets +0x120 = 1, +0x121 = detected, +0x11C = 1; with +0x124 set, nothing.
    /// </summary>
    [Fact]
    public void ThePreRunHandlerSetsTheStateFromTheCliffAndStopEvents()
    {
        using var rig = new Rig();
        var b = new ReactToCliffBehavior(rig.Robot);
        var lines = new List<string>();
        rig.Robot.Engine.LogLine += lines.Add;
        rig.Send(new CliffEvent { Timestamp = rig.T, DetectedFlags = 0 });
        Assert.Equal(0u, b.State); Assert.False(b.ReactNow);

        rig.Send(new CliffEvent { Timestamp = rig.T, DetectedFlags = 3 });
        Assert.Equal(1u, b.State); Assert.True(b.ReactNow); Assert.Equal(3, b.CliffDetectedCopy);
        Assert.Contains(lines, l => l.Contains("BehaviorReactToCliff.CliffWithoutStop") && l.Contains("bad latency"));

        rig.Send(new Cozmo.Protocol.RobotStopped { Field0 = 0 });          // tag 0x34: +0x11C = 0, +0x124 = 0
        Assert.Equal(0u, b.State);
    }

    /// <summary>
    /// M7-019 (Init 0x00604D50, lambda $_0 0x006056B8): state 0 saves the threshold, waits with FLT_MAX while MC+0xC is set, and when the
    /// threshold has changed quits (+0x124 = 1): the stop reaction then only sends RobotCliffEventFinished (0x00604FA2) and no animation plays.
    /// </summary>
    [Fact]
    public void ASuspiciousCliffQuitsTheStopReactionWithoutAnAnimation()
    {
        using var rig = new Rig();
        var b = new ReactToCliffBehavior(rig.Robot);
        var ctx = Ctx(rig, () => null);
        int finished = 0;
        b.RobotCliffEventFinished += () => finished++;
        var scope = new BehaviorScope();
        b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.Equal(rig.Robot.Sensors.CliffDetectThreshold, b.SavedThreshold);   // this+0x122 = [[robot+0x288]+0xC]

        // MC+0xC set (the wheels are moving): the lambda returns false, the wait goes on
        SendState(rig, (uint)(RobotStatusFlag.AreWheelsMoving | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos));
        Assert.True(rig.Robot.Motion.BodyMoving);
        Assert.True(b.Update(ctx, 0));
        Assert.True(b.Update(ctx, 1_000_000));                    // FLT_MAX seconds: no timeout in reach
        Assert.DoesNotContain(b.Trace, l => l.Contains("PlayingStopReaction"));

        // stopped, but the threshold changed under it: the lambda quits
        SendState(rig, (uint)(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos));
        rig.Robot.Sensors.SendCliffDetectThresholdToRobot(150);
        Assert.NotEqual(150, b.SavedThreshold);
        b.Update(ctx, 1_000_100);
        Assert.True(b.QuitDueToSuspiciousCliff);
        Assert.Contains(b.Trace, l => l.Contains("BehaviorReactToCliff.QuittingDueToSuspiciousCliff"));
        Assert.Contains(b.Trace, l => l.Contains("ToState:PlayingStopReaction"));
        Assert.Equal(1, finished);                                // SendFinishedReactToCliffMessage
        Assert.DoesNotContain(b.Trace, l => l.StartsWith("ReactToCliffDetectorStop:") || l.StartsWith("play ReactToCliffDetectorStop"));
        Assert.DoesNotContain(b.Trace, l => l.Contains("ToState:PlayingCliffReaction"));
    }

    /// <summary>
    /// M7-019 (0x00604DEC, 0x00605176..0x006051A2): the severe-need value picks the path and the trigger. 3 (none) and 2 take the stop reaction (0x19E)
    /// first and then 0x19D; 0 and 1 skip the stop reaction (the value is below 2) and play 0x13D / 0x131.
    /// </summary>
    [Theory]
    [InlineData(null, new[] { "ReactToCliffDetectorStop", "ReactToCliff" })]
    [InlineData(NeedId.Play, new[] { "ReactToCliffDetectorStop", "ReactToCliff" })]
    [InlineData(NeedId.Repair, new[] { "NeedsSevereLowRepairCliffReact" })]
    [InlineData(NeedId.Energy, new[] { "NeedsSevereLowEnergyCliffReact" })]
    public void TheSevereNeedValueChoosesThePathAndTheTrigger(NeedId? need, string[] triggers)
    {
        using var rig = new Rig();
        var b = new ReactToCliffBehavior(rig.Robot);
        var ctx = Ctx(rig, () => need);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        for (int t = 0; t < 10; t++) b.Update(ctx, t * 100);
        // with no animation assets each trigger fails at once and is traced as "<trigger>: ..."
        var seen = b.Trace.Where(l => l.Contains(':') && triggers.Any(t => l.StartsWith(t + ":"))).Select(l => l[..l.IndexOf(':')]).ToList();
        Assert.Equal(triggers, seen);
        bool stopFirst = need is null or NeedId.Play;
        Assert.Equal(stopFirst, b.Trace.Any(l => l.Contains("ToState:PlayingStopReaction")));
        Assert.Contains(b.Trace, l => l.Contains("ToState:PlayingCliffReaction"));
    }

    /// <summary>
    /// M7-019 (0x00605122..0x00605132): either streamline byte (+0xD9 alwaysStreamline, +0xD8 the hard-spark flag Init computes) skips the
    /// animation and goes straight to TransitionToBackingUp.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void EitherStreamlineByteSkipsTheCliffAnimation(bool alwaysStreamline, bool hardSpark)
    {
        using var rig = new Rig();
        var b = new ReactToCliffBehavior(rig.Robot) { AlwaysStreamline = alwaysStreamline };
        b.SparkGate = () => hardSpark;
        var ctx = Ctx(rig, () => Cozmo.Robot.Behavior.NeedId.Repair);   // with the severe value 0 the animation would be 0x13D if it played
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        for (int t = 0; t < 6; t++) b.Update(ctx, t * 100);
        Assert.Contains(b.Trace, l => l.Contains("ToState:PlayingCliffReaction"));
        Assert.DoesNotContain(b.Trace, l => l.StartsWith("NeedsSevereLowRepairCliffReact:"));
        Assert.DoesNotContain(b.Trace, l => l.Contains("robot.cliff_detected"));   // the DAS event is on the animation branch only
    }

    /// <summary>
    /// M7-019 (TransitionToBackingUp 0x00605318): with [[robot+0x288]+5] set, DriveStraightAction(-60.0f, 100.0f) and the finished message after it
    /// (lambda $_2), no objective; with it clear, the finished message and BehaviorObjectiveAchieved(0x1C). A CliffEvent while running sets +0x120.
    /// </summary>
    [Fact]
    public void BackingUpDrivesWhenTheCliffByEventByteIsSetAndOtherwiseAchievesTheObjective()
    {
        using var rig = new Rig();
        // byte clear
        var plain = new ReactToCliffBehavior(rig.Robot) { AlwaysStreamline = true };
        var ctx = Ctx(rig, () => null);
        int finished = 0;
        plain.RobotCliffEventFinished += () => finished++;
        plain.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.False(rig.Robot.Sensors.CliffDetectedByEvent);
        for (int t = 0; t < 4; t++) plain.Update(ctx, t * 100);
        Assert.Equal(1, finished);
        Assert.Contains(plain.Trace, l => l.Contains("BehaviorObjectiveAchieved(0x1C, true)"));
        Assert.True(plain.Trace.ToList().FindIndex(l => l.Contains("broadcast RobotCliffEventFinished")) < plain.Trace.ToList().FindIndex(l => l.Contains("BehaviorObjectiveAchieved")));

        // byte set: the drive, then the message, and no objective
        var drive = new ReactToCliffBehavior(rig.Robot) { AlwaysStreamline = true };
        var seen = new List<(float Distance, float Speed)>();
        var release = new TaskCompletionSource();
        drive.DriveStraight = (d, v, ct) => { seen.Add((d, v)); return release.Task; };
        int driveFinished = 0;
        drive.RobotCliffEventFinished += () => driveFinished++;
        drive.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        rig.Send(new CliffEvent { Timestamp = rig.T, DetectedFlags = 1 });       // while running: +0x120 = 1, and [[robot+0x288]+5] = 1
        Assert.True(rig.Robot.Sensors.CliffDetectedByEvent);
        Assert.True(drive.ReactNow);
        drive.Update(ctx, 0);                                                    // wait lambda ends; AlwaysStreamline goes to the back-up
        drive.Update(ctx, 100);
        SpinWait.SpinUntil(() => seen.Count == 1, 2000);
        Assert.Equal(new[] { (F(unchecked((int)0xC2700000)), F(0x42C80000)) }, seen);
        Assert.True(drive.Update(ctx, 200));                                     // the drive is the action in flight: the behaviour stays
        Assert.Equal(0, driveFinished);
        release.SetResult();
        for (int t = 3; t < 40 && driveFinished == 0; t++) { Thread.Sleep(10); drive.Update(ctx, t * 100); }
        Assert.Equal(1, driveFinished);
        Assert.DoesNotContain(drive.Trace, l => l.Contains("BehaviorObjectiveAchieved"));
    }

    /// <summary>M7-019 (HandleWhileRunning 0x00605580, UpdateInternal 0x00605408): a ChargerEvent with onCharger sets +0x125 and the next update ends the behaviour.</summary>
    [Fact]
    public void AChargerEventWhileRunningEndsTheBehaviourAtTheNextUpdate()
    {
        using var rig = new Rig();
        var b = new ReactToCliffBehavior(rig.Robot);
        var ctx = Ctx(rig, () => null);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        b.HandleChargerEvent(false);
        Assert.False(b.FinishRequested);
        b.HandleChargerEvent(true);
        Assert.True(b.FinishRequested);
        Assert.False(b.Update(ctx, 0));                                          // returns 2
        Assert.False(b.FinishRequested);                                         // cleared by UpdateInternal

        // not running: ChargerEvent does nothing
        var idle = new ReactToCliffBehavior(rig.Robot);
        idle.HandleChargerEvent(true);
        Assert.False(idle.FinishRequested);
    }

    /// <summary>M7-019 (StopInternal 0x006053FC): +0x11C, +0x120, +0x121 cleared; +0x122 and +0x124 are not.</summary>
    [Fact]
    public void StopClearsTheStateAndTheReactNowBytesOnly()
    {
        using var rig = new Rig();
        var b = new ReactToCliffBehavior(rig.Robot);
        rig.Send(new CliffEvent { Timestamp = rig.T, DetectedFlags = 1 });
        Assert.Equal(1u, b.State);
        var ctx = Ctx(rig, () => null);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.True(b.ReactNow);
        b.Stop(BehaviorStopReason.Interrupted);
        Assert.Equal(0u, b.State); Assert.False(b.ReactNow); Assert.Equal(0, b.CliffDetectedCopy);
    }

    // =============================================================== M7-015 / M7-019 / M7-021: ReactToPickup

    [Fact]
    public void ThePickupConstantsAreTheEnginesBits()
    {
        Assert.Equal(0x3F000000, BitConverter.SingleToInt32Bits(ReactToPickupBehavior.InitialWaitSec));              // 0.5f
        Assert.Equal(0x3FD51EB860000000L, BitConverter.DoubleToInt64Bits(ReactToPickupBehavior.RetryScaleStep));    // 0.33000001311302185
        Assert.Equal(0x4008000000000000L, BitConverter.DoubleToInt64Bits(ReactToPickupBehavior.RetryLowerFactor));   // 3.0
        Assert.Equal(0x4018000000000000L, BitConverter.DoubleToInt64Bits(ReactToPickupBehavior.RetryUpperFactor));   // 6.0
        Assert.Equal(0x42700000, BitConverter.SingleToInt32Bits(ReactToPickupBehavior.AnimationTimeout));            // 60.0f
        Assert.Equal(0x18, ReactToPickupBehavior.CliffRawLimit);
        Assert.Equal(500u, ReactToPickupBehavior.RecentFaceWindowMs);
        // the trigger immediates (0x00607958, 0x00607968, 0x006079A4, 0x006079B0, AcknowledgeFace*)
        Assert.Equal(0x1A9, (int)AnimationTrigger.ReactToPickup);
        Assert.Equal(0xE6, (int)AnimationTrigger.HiccupRobotPickedUp);
        Assert.Equal(0x184, (int)AnimationTrigger.PetDetectionShort_Cat);
        Assert.Equal(0x185, (int)AnimationTrigger.PetDetectionShort_Dog);
        Assert.Equal(1, (int)AnimationTrigger.AcknowledgeFaceNamed);
        Assert.Equal(2, (int)AnimationTrigger.AcknowledgeFaceUnnamed);
    }

    /// <summary>M7-019 (constructor 0x0060773C, InitInternal 0x0060775E): +0x11C = 0.0f and +0x120 = 1.0 from the constructor; InitInternal resets only +0x120.</summary>
    [Fact]
    public void ThePickupFieldsStartAndInitResetsOnlyTheScale()
    {
        using var rig = new Rig();
        var b = new ReactToPickupBehavior(rig.Vision, rng: new EngineRandom(7));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(b.RetryDeadline));
        Assert.Equal(0x3FF0000000000000L, BitConverter.DoubleToInt64Bits(b.RetryScale));
        var ctx = Ctx(rig);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        b.Update(ctx, 0); b.Update(ctx, 600);                     // the 0.5 s wait ends: StartAnim runs and grows the scale
        Assert.NotEqual(1.0, b.RetryScale);
        float deadline = b.RetryDeadline;
        b.Stop(BehaviorStopReason.Interrupted);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Equal(1.0, b.RetryScale);                          // +0x120 reset to 1.0
        Assert.Equal(deadline, b.RetryDeadline);                  // +0x11C is not reset
    }

    /// <summary>M7-019 (InitInternal): the 0.5 s WaitAction runs first, with the behaviour still alive whatever the robot's state (UpdateInternal gate 1: +0x84).</summary>
    [Fact]
    public void ThePickupWaitsHalfASecondBeforeItsFirstAnimationAndIgnoresTheGatesWhileItWaits()
    {
        using var rig = new Rig();                                // on treads: robot+0x355 is 0
        Assert.NotEqual(OffTreadsState.InAir, rig.Robot.Sensors.OffTreadsState);
        var b = new ReactToPickupBehavior(rig.Vision, rng: new EngineRandom(7));
        var ctx = Ctx(rig);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.True(b.Update(ctx, 0));
        Assert.True(b.Update(ctx, 400));                          // the wait is the action in flight: gate 2 is not evaluated
        Assert.DoesNotContain(b.Trace, l => l.StartsWith("ReactToPickup:"));
        // the wait ends and StartAnim runs (no faces, no pets: the fallback 0x1A9); then, with no action in flight, gate 2: robot+0x355 != 1 returns 2
        Assert.False(b.Update(ctx, 600));
        Assert.Contains(b.Trace, l => l.StartsWith("ReactToPickup:"));
    }

    /// <summary>M7-021 C2h / A3: gate 3 (robot+0x338) logs BehaviorReactToPickup.OnCharger and returns 2, but only once no action is in flight, after the first StartAnim ran.</summary>
    [Fact]
    public void ThePickupEndsOnTheChargerContactsOnlyOnceNoActionIsInFlight()
    {
        using var rig = new Rig();
        PickUp(rig, onChargerContacts: true);
        var lines = new List<string>();
        rig.Robot.Engine.LogLine += lines.Add;
        var b = new ReactToPickupBehavior(rig.Vision, rng: new EngineRandom(7));
        var ctx = Ctx(rig);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.True(b.Update(ctx, 0));
        Assert.True(b.Update(ctx, 100));
        Assert.DoesNotContain(lines, l => l.Contains("BehaviorReactToPickup.OnCharger"));
        Assert.False(b.Update(ctx, 600));                         // StartAnim ran at the wait's end; the gate then returned 2
        Assert.Contains(b.Trace, l => l.StartsWith("ReactToPickup:"));
        Assert.Contains(lines, l => l.Contains("BehaviorReactToPickup.OnCharger") && l.Contains("Stopping behavior because we are on the charger"));
    }

    /// <summary>
    /// M7-019 (section 5.2 step 7) and M7-015 (the strict float compare at 0x00607C38): the retry interval is now + RandDblInRange(3x, 6x), computed
    /// as (float)now + (float)r with x the double scale, which then grows by 0.33000001311302185 (0x3FD51EB860000000).
    /// </summary>
    [Fact]
    public void TheRetryDeadlineAndScaleFollowTheEnginesArithmetic()
    {
        using var rig = new Rig();
        PickUp(rig);
        var b = new ReactToPickupBehavior(rig.Vision, rng: new EngineRandom(11));
        var ctx = Ctx(rig);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        b.Update(ctx, 0);
        Assert.True(b.Update(ctx, 600));                           // StartAnim #1 at now = 0.6f

        // the expected draw, with the same generator state and the engine's formula a + u*(b-a) (M5 gap1): x = 1.0
        var oracle = new EngineRandom(11);
        double step = BitConverter.Int64BitsToDouble(0x3FD51EB860000000L);
        double x = 1.0;
        float now1 = (float)(600 / 1000.0);
        double r1 = 3.0 * x + oracle.GetNextDbl() * (6.0 * x - 3.0 * x);
        float expected1 = now1 + (float)r1;
        Assert.Equal(BitConverter.SingleToInt32Bits(expected1), BitConverter.SingleToInt32Bits(b.RetryDeadline));
        x += step;
        Assert.Equal(BitConverter.DoubleToInt64Bits(x), BitConverter.DoubleToInt64Bits(b.RetryScale));
        Assert.InRange(b.RetryDeadline, now1 + 3.0f, now1 + 6.0f);

        // strict compare: at now == deadline nothing happens; just past it the retry runs
        long traced = b.Trace.Count(l => l.StartsWith("ReactToPickup:"));
        double deadlineMs = (double)b.RetryDeadline * 1000.0;
        Assert.True(b.Update(ctx, deadlineMs - 1));
        Assert.Equal(traced, b.Trace.Count(l => l.StartsWith("ReactToPickup:")));
        Assert.True(b.Update(ctx, (double)(float)(b.RetryDeadline * 1000.0f)));   // now == deadline in float: the compare is strict, no retry
        Assert.Equal(traced, b.Trace.Count(l => l.StartsWith("ReactToPickup:")));
        Assert.True(b.Update(ctx, 10_000));                        // well past: StartAnim #2
        Assert.Equal(traced + 1, b.Trace.Count(l => l.StartsWith("ReactToPickup:")));
        double r2 = 3.0 * x + oracle.GetNextDbl() * (6.0 * x - 3.0 * x);
        float expected2 = (float)(10_000 / 1000.0) + (float)r2;
        Assert.Equal(BitConverter.SingleToInt32Bits(expected2), BitConverter.SingleToInt32Bits(b.RetryDeadline));
        x += step;
        Assert.Equal(BitConverter.DoubleToInt64Bits(x), BitConverter.DoubleToInt64Bits(b.RetryScale));
    }

    /// <summary>M7-019 (0x00607C3A..0x00607CA8): cliff raw[0] &gt;&gt; 4 &lt;= 0x18 retries StartAnim; above it, the head is recalibrated instead and the deadline is untouched.</summary>
    [Fact]
    public void TheRetryChecksCliffSensorZeroAgainstTheShiftedLimit()
    {
        foreach (var (raw, calibrates) in new[] { ((ushort)399, false), ((ushort)400, true) })   // 399 >> 4 = 24 = 0x18; 400 >> 4 = 25
        {
            using var rig = new Rig();
            PickUp(rig);
            var b = new ReactToPickupBehavior(rig.Vision, rng: new EngineRandom(3));
            var ctx = Ctx(rig);
            b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
            b.Update(ctx, 0); b.Update(ctx, 600);
            float deadline = b.RetryDeadline;
            long before = b.Trace.Count(l => l.StartsWith("ReactToPickup:"));
            SendState(rig, (uint)RobotStatusFlag.IsPickedUp, raw);
            Assert.Equal(raw, rig.Robot.Sensors.CliffDataRawStored[0]);
            Assert.True(b.Update(ctx, 10_000));
            if (calibrates)
            {
                Assert.Equal(before, b.Trace.Count(l => l.StartsWith("ReactToPickup:")));          // no StartAnim
                Assert.Contains(b.Trace, l => l.Contains("calibrate head"));                       // CalibrateMotorAction(robot, true, false)
                Assert.Equal(deadline, b.RetryDeadline);                                           // the calibration branch touches neither timer field
            }
            else
            {
                Assert.Equal(before + 1, b.Trace.Count(l => l.StartsWith("ReactToPickup:")));
                Assert.DoesNotContain(b.Trace, l => l.Contains("calibrate head"));
            }
        }
    }

    // ---- StartAnim's branches (faces and pets need the M14 worlds, SayTextAction is not built)

    private static TrackedFace MakeFace(int id, uint ts, string? name = null)
    {
        var cam = new CameraModel(CameraCalibration.Nominal(), HeadGeometry.CameraPoseInWorld(Pose3d.Identity, 0.2));
        var p = new Vec3(400, 0, 300);
        var px = cam.Project(p)!.Value; double dist = (p - cam.Pose.Translation).Length;
        double eye = 62 * cam.Calibration.FocalLengthX / dist, w = 2 * eye;
        var tf = new TrackedFace(new DetectedFace(id, new FaceRect(px.X - w / 2, px.Y + 0.125 * w - w / 2, w, w),
            new Vec2(px.X - eye / 2, px.Y), new Vec2(px.X + eye / 2, px.Y), Name: name, RollRad: 0.0), ts);
        tf.UpdateTranslation(cam);
        return tf;
    }

    private static string FirstTrigger(ReactToPickupBehavior b, params string[] names) =>
        b.Trace.Select(l => names.FirstOrDefault(n => l.StartsWith(n + ":"))).FirstOrDefault(n => n is not null) ?? "(none)";

    private static ReactToPickupBehavior RunStartAnim(Rig rig, Action<ReactToPickupBehavior>? configure = null)
    {
        var b = new ReactToPickupBehavior(rig.Vision, rng: new EngineRandom(5));
        configure?.Invoke(b);
        var ctx = Ctx(rig);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        b.Update(ctx, 0); b.Update(ctx, 600);
        return b;
    }

    [Fact]
    public void StartAnimWithNothingSeenPlaysThePickupReactionAndTheHiccupByteSelectsTheHiccupOne()
    {
        using var rig = new Rig();
        PickUp(rig);
        var plain = RunStartAnim(rig, b => b.WhiteboardHiccupByte = () => false);
        Assert.Equal("ReactToPickup", FirstTrigger(plain, "ReactToPickup", "HiccupRobotPickedUp"));   // 0x1A9
        var hiccup = RunStartAnim(rig, b => b.WhiteboardHiccupByte = () => true);
        Assert.Equal("HiccupRobotPickedUp", FirstTrigger(hiccup, "ReactToPickup", "HiccupRobotPickedUp"));   // 0xE6
    }

    [Fact]
    public void StartAnimPrefersTheLowestKeyPetsAcknowledgementAndAHardSparkSkipsFacesAndPets()
    {
        using var rig = new Rig();
        PickUp(rig);
        rig.Vision.Pets.Update(new[] { new DetectedPet(5, PetType.Dog, new FaceRect(0, 0, 5, 5)), new DetectedPet(3, PetType.Cat, new FaceRect(0, 0, 5, 5)) }, 1000, false);
        var cat = RunStartAnim(rig, b => b.WhiteboardHiccupByte = () => false);
        Assert.Equal("PetDetectionShort_Cat", FirstTrigger(cat, "PetDetectionShort_Cat", "PetDetectionShort_Dog", "ReactToPickup"));   // key 3, type 1: 0x184

        rig.Vision.Pets.Update(new[] { new DetectedPet(2, PetType.Dog, new FaceRect(0, 0, 5, 5)) }, 1100, false);
        rig.Vision.Pets.Update(Array.Empty<DetectedPet>(), 1200, false); rig.Vision.Pets.Update(Array.Empty<DetectedPet>(), 1300, false); rig.Vision.Pets.Update(Array.Empty<DetectedPet>(), 1400, false);
        // pets 3 and 5 were dropped (track lost); the Dog, key 2, was dropped too: add a dog alone
        rig.Vision.Pets.Update(new[] { new DetectedPet(9, PetType.Dog, new FaceRect(0, 0, 5, 5)) }, 1500, false);
        var dog = RunStartAnim(rig, b => b.WhiteboardHiccupByte = () => false);
        Assert.Equal("PetDetectionShort_Dog", FirstTrigger(dog, "PetDetectionShort_Cat", "PetDetectionShort_Dog", "ReactToPickup"));   // type 2: 0x185

        // hard spark: neither faces nor pets; the fallback
        var hard = RunStartAnim(rig, b => { b.SparkGate = () => true; b.WhiteboardHiccupByte = () => false; });
        Assert.Equal("ReactToPickup", FirstTrigger(hard, "PetDetectionShort_Cat", "PetDetectionShort_Dog", "ReactToPickup"));
    }

    [Fact]
    public void StartAnimOnAnUnnamedFaceAcknowledgesItAndOnANamedFaceReportsTheMissingSayTextAction()
    {
        using var rig = new Rig();
        PickUp(rig);
        rig.Vision.Faces.AddOrUpdateFace(MakeFace(1, 1000), Pose3d.Identity, false);
        var unnamed = RunStartAnim(rig);
        Assert.Equal("AcknowledgeFaceUnnamed", FirstTrigger(unnamed, "AcknowledgeFaceUnnamed", "ReactToPickup"));   // trigger 2

        var reported = new List<string>();
        void On(string s) => reported.Add(s);
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            rig.Vision.Faces.AddOrUpdateFace(MakeFace(2, 1100, "Jim"), Pose3d.Identity, false);
            var named = RunStartAnim(rig);
            Assert.Contains(reported, r => r.Contains("SayTextAction"));
            Assert.Equal("(none)", FirstTrigger(named, "AcknowledgeFaceNamed", "AcknowledgeFaceUnnamed", "ReactToPickup"));   // no stand-in animation
            Assert.NotEqual(0, BitConverter.SingleToInt32Bits(named.RetryDeadline));                                          // step 7 still ran
        }
        finally { SteppedBehavior.MissingReported -= On; }
    }

    // ---- through the live entry

    /// <summary>
    /// M7-015 through the live entry: the manager's RobotPickedUp strategy (robot+0x355 == 1, 0x0060DDCE) fires ReactToPickup; the manager's
    /// ticks then run Init's 0.5 s wait, StartAnim, and later the retry once the deadline has passed.
    /// </summary>
    [Fact]
    public void ThePickupReactionRunsItsWholeLifecycleThroughTheManager()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        // the stack's IsRunnable needs the animation library; the trigger map stays empty so every trigger fails at once (no clip plays in real time)
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Random = new Random(5) };
        using var manager = new BehaviorManager(ctx);
        var reg = ShippedBehaviors.Reactions(rig.Robot, clockSec: () => rig.Clock.NowMs / 1000.0, vision: rig.Vision, obbRoot: obb)
                                  .Single(r => r.Strategy.Trigger == ReactionTrigger.RobotPickedUp);
        Assert.IsType<ReactToPickupBehavior>(reg.Behavior);
        manager.AddReaction(reg.Strategy, reg.Behavior);
        manager.RemoveDisableReactionsLock("sdk");
        PickUp(rig);
        var b = (ReactToPickupBehavior)reg.Behavior;

        var fired = manager.CheckReactions(1);
        Assert.NotNull(fired);
        Assert.Equal(ReactionTrigger.RobotPickedUp, fired!.Trigger);
        manager.Update(0, 1.0);
        manager.Update(600, 1.6);                                  // the wait ends: StartAnim #1
        Assert.Same(b, manager.Current);
        long first = b.Trace.Count(l => l.StartsWith("ReactToPickup:"));
        Assert.Equal(1, first);
        manager.Update(1000, 2.0);                                 // before the deadline (>= 3 s away): nothing new
        Assert.Equal(first, b.Trace.Count(l => l.StartsWith("ReactToPickup:")));
        manager.Update(20_000, 20.0);                              // past the deadline: the retry
        Assert.Equal(first + 1, b.Trace.Count(l => l.StartsWith("ReactToPickup:")));
        // the robot is put down: gate 2 ends the reaction
        for (int i = 0; i < 40; i++) rig.State();
        Assert.NotEqual(OffTreadsState.InAir, rig.Robot.Sensors.OffTreadsState);
        manager.Update(20_100, 20.1);
        Assert.Null(manager.Current);
    }

    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "assets", "animationGroups"))) return r;
            d = d.Parent;
        }
        return null;
    }
}
