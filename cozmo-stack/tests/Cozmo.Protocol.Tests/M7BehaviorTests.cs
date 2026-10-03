using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M7-014: the reaction-lock manager side and the 13 per-class 21-byte lock tables. The expected values are
/// taken from the M7 inventory Appendix F ("Reaction-lock tables read"), which quotes the table addresses
/// in <c>.rodata</c> and the trigger ordinals whose byte is 1, not from the code under test.
/// </summary>
public class M7BehaviorTests
{
    private sealed class FakeBehavior : IBehavior
    {
        public string Id => "fake";
        public string Class => "Fake";
        public bool IsRunnable(BehaviorContext c) => true;
        public double EvaluateScore(BehaviorContext c) => 0;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) => Task.CompletedTask;
        public bool Update(BehaviorContext c, double nowMs) => true;
        public void Stop(BehaviorStopReason r) { }
    }

    /// <summary>
    /// Every table the engine ships: name, <c>.rodata</c> address and the ordinals marked 1, exactly as
    /// Appendix F records them.
    /// </summary>
    public static readonly (string Name, string Address, int[] Triggers)[] AppendixF =
    {
        ("ReactToCliff",           "0xC73746", new[] { 1, 2, 8, 20 }),
        ("ReactToMotorCalibration","0xC74032", new[] { 0, 5, 12, 14, 15, 16, 17 }),
        ("ReactToPlacedOnSlope",   "0xC745E0", new[] { 0, 12, 14 }),
        ("ReactToRobotShaken",     "0xC750E0", new[] { 0, 1, 2, 3, 4, 5, 8, 10, 11, 12, 13, 14, 15, 16, 17, 20 }),
        ("CubeLiftWorkout",        "0xc6b7e0", new[] { 1, 2, 8, 10, 20 }),
        ("PeekABoo",               "0xc70960", new[] { 1, 2, 3, 8, 10 }),
        ("PutDownBlock",           "0xc68b20", Array.Empty<int>()),
        ("FistBump",               "0xc6fdc8", new[] { 1, 2, 8, 10, 11, 12, 14, 20 }),
        ("Bouncer",                "0xc6fb90", new[] { 1, 2, 3, 4, 5, 8, 10, 20 }),
        ("GuardDog",               "0xc6ff06", new[] { 1, 2, 3, 4, 5, 8, 10, 20 }),
        ("Dance",                  "0xc6f1c8", new[] { 1, 2, 3, 8, 10, 20 }),
        ("EnrollFace",             "0xc71b6c", new[] { 0, 1, 2, 3, 4, 5, 8, 11, 12, 14, 15, 16, 17, 20 }),
        ("OnboardingShowCube",     "0xc72454", new[] { 1, 2, 3, 4, 5, 8, 10, 20 }),
    };

    [Fact]
    public void TheThirteenReactionLockTablesMatchTheShippedBytes()
    {
        Assert.Equal(13, ReactionLockTables.All.Count);
        foreach (var e in AppendixF)
        {
            var t = ReactionLockTables.For(e.Name);
            Assert.NotNull(t);
            Assert.Equal(e.Address, t!.TableAddress);
            Assert.Equal(e.Triggers.OrderBy(x => x).ToArray(), t.LockedTriggers.OrderBy(x => x).ToArray());
        }
    }

    /// <summary>
    /// <c>BehaviorManager::DisableReactionsWithLock</c> reads <c>[table + trigger*2 + 1]</c> and disables
    /// only a non-zero byte (0x005A283E/0x005A2842/0x005A2846), so the mask is exactly the marked set.
    /// </summary>
    [Fact]
    public void ATableMarksExactlyItsTriggersInTheManagerMask()
    {
        var mask = ReactionLockTables.ReactToCliff.ToMask();
        Assert.Equal(ReactionLockTables.TriggerCount, mask.Length);
        Assert.Equal(new[] { 1, 2, 8, 20 }, Enumerable.Range(0, mask.Length).Where(i => mask[i]).ToArray());

        var empty = ReactionLockTables.PutDownBlock.ToMask();
        Assert.DoesNotContain(true, empty);
    }

    /// <summary>
    /// The manager side of <c>SmartDisableReactionsWithLock</c>: a class's table disables exactly its
    /// triggers (<c>IsReactionTriggerEnabled</c> is the node's empty lock set) and removing the lock
    /// re-enables them.
    /// </summary>
    [Fact]
    public void TheManagerDisablesOnlyTheTriggersAClassTableNames()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = null! };
        using var m = new BehaviorManager(ctx);
        var b = new FakeBehavior();
        foreach (var t in Enum.GetValues<ReactionTrigger>())
            m.AddReaction(new GenericReactionStrategy(t, "M7-014 test", () => 0), b);

        const string lockName = "ReactToCliff_behaviorLock";
        m.DisableReactionsWithLock(lockName, ReactionLockTables.ReactToCliff);

        foreach (var t in Enum.GetValues<ReactionTrigger>())
        {
            bool locked = ReactionLockTables.ReactToCliff.LockedTriggers.Contains((int)t);
            Assert.Equal(locked, m.HasDisableLock(t, lockName));
            Assert.Equal(!locked, m.IsReactionTriggerEnabled(t));
        }

        m.RemoveDisableReactionsLock(lockName);
        Assert.All(Enum.GetValues<ReactionTrigger>(), t => Assert.True(m.IsReactionTriggerEnabled(t)));
    }

    // ---------------------------------------------------------------- M7-002

    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "config", "engine", "behaviorSystem")))
                return r;
            d = d.Parent;
        }
        return null;
    }

    /// <summary>
    /// M7-002: the shipped reaction map has a row for all 21 triggers and the Appendix C row 6
    /// associations and params. (The file has 22 rows: Frustration appears twice, Minor and Major, so the
    /// inventory's "21 entries" is 21 distinct triggers over 22 rows.) The expected values come from the
    /// inventory row, not the loader.
    /// </summary>
    [Fact]
    public void TheReactionMapLoadsTheTwentyOneShippedEntries()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var map = ReactionTriggerMap.Load(obb);
        Assert.Equal(22, map.Count);
        Assert.Equal(21, map.Select(e => e.Trigger).Distinct().Count());
        Assert.Equal("ReactToCliff", map.Single(e => e.Trigger == ReactionTrigger.CliffDetected).BehaviorId);
        Assert.Equal("AcknowledgeFace", map.Single(e => e.Trigger == ReactionTrigger.FacePositionUpdated).BehaviorId);
        Assert.Equal(2, map.Count(e => e.Trigger == ReactionTrigger.Frustration));
        var minor = map.Single(e => e.BehaviorId == "ReactToFrustrationMinor");
        Assert.Equal(-0.6, minor.FrustrationMaxConfidence!.Value, 3);
        Assert.Equal(60.0, minor.FrustrationCooldownSec!.Value, 3);
        Assert.Null(map.Single(e => e.BehaviorId == "ReactToFrustrationMajor").FrustrationCooldownSec);
        Assert.False(map.Single(e => e.Trigger == ReactionTrigger.RobotFalling).ShouldResumeLast!.Value);
        Assert.True(map.Single(e => e.Trigger == ReactionTrigger.UnexpectedMovement).ShouldResumeLast!.Value);
        Assert.Equal("PlacedOnCharger", map.Single(e => e.BehaviorId == "ReactToOnCharger").StrategyType);
        Assert.Equal(6, map.Single(e => e.BehaviorId == "FistBump").FistBumpObjectiveParams.Count);
    }

    /// <summary>M7-002: the shipped animation-trigger pair counts (inventory Appendix C rows 7-8).</summary>
    [Fact]
    public void TheShippedAnimationMapsHaveTheInventoryPairCounts()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var at = Cozmo.Robot.Behavior.AnimationTriggerMap.Load(obb);
        Assert.Equal(573, at.Count);

        var cube = Path.Combine(obb, "assets", "cozmo_resources", "assets", "cubeAnimationGroupMaps", "CubeAnimationTriggerMap.json");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(cube));
        Assert.Equal(40, doc.RootElement.GetProperty("Pairs").GetArrayLength());
    }

    /// <summary>
    /// M7-002: the binding is driven by the map, not a hard-coded list. The three behaviour ids the stack
    /// does not build are reported unbound and the rest are registered.
    /// </summary>
    [Fact]
    public void TheReactionBindingFollowsTheMapAndReportsUnbuiltClasses()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var unbound = new List<string>();
        var regs = ShippedBehaviors.Reactions(robot, obbRoot: obb, unbound: unbound);

        Assert.NotEmpty(regs);
        // The map's FistBump, Hiccup and Sparked behaviour classes are not built by the stack.
        Assert.Contains("FistBump -> FistBump", unbound);
        Assert.Contains("Hiccup -> Hiccup", unbound);
        Assert.Contains("Sparked -> ReactToSparked", unbound);
        Assert.DoesNotContain(unbound, u => u.StartsWith("CliffDetected"));
        Assert.DoesNotContain(unbound, u => u.StartsWith("RobotFalling"));
    }

    // ---------------------------------------------------------------- M7-012 / M7-020

    /// <summary>
    /// M7-012: the shipped mood config's action-result map (Appendix D row 4) and default repetition
    /// penalty graph (row 3). Expected values are the config's, not the loader's.
    /// </summary>
    [Fact]
    public void TheMoodConfigLoadsTheActionResultMapAndDefaultPenalty()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var m = MoodModel.Load(obb);
        Assert.Equal(12, m.ActionResultEvents.Keys.Select(k => k.ActionType).Distinct().Count());
        Assert.Equal("DrivingActionFailedWithRetry", m.ActionResultEvents[("DRIVE_TO_POSE", "RETRY")]);
        Assert.Equal("StackSucceeded", m.ActionResultEvents[("PLACE_OBJECT_HIGH", "SUCCESS")]);
        Assert.Equal("RollSucceeded", m.ActionResultEvents[("POP_A_WHEELIE", "SUCCESS")]);
        Assert.NotNull(m.DefaultRepetitionPenalty);
        Assert.Equal(0.0, m.DefaultRepetitionPenalty!.At(0), 6);
        Assert.Equal(1.0, m.DefaultRepetitionPenalty!.At(30), 6);
    }

    /// <summary>M7-020: HandleActionEnded resolves the map and honours the completion-enable set.</summary>
    [Fact]
    public void HandleActionEndedUsesTheActionResultMapAndCompletionEnableSet()
    {
        var model = new MoodModel();
        model.AddDecayGraph(new DecayGraph("Confident", new[] { (0.0, 1.0) }));
        model.AddEvent(new EmotionEvent("DrivingActionFailedWithAbort",
            new[] { new EmotionAffector(EmotionType.Confident, -0.2) }));
        model.AddActionResultEvent("DRIVE_TO_POSE", "ABORT", "DrivingActionFailedWithAbort");

        var mood = new MoodState(model);
        Assert.True(mood.HandleActionEnded("DRIVE_TO_POSE", "ABORT", "action1", 0));
        Assert.Equal(-0.2, mood[EmotionType.Confident], 6);

        var disabled = new MoodState(model);
        disabled.SetMoodEventOnCompletionEnabled("action2", false);
        Assert.False(disabled.HandleActionEnded("DRIVE_TO_POSE", "ABORT", "action2", 0));
        Assert.Equal(0.0, disabled[EmotionType.Confident], 6);
    }

    /// <summary>M7-020: the nine-value game output raises the stack's seam (gap1 G1).</summary>
    [Fact]
    public void SendEmotionsToGameRaisesTheNineValues()
    {
        var mood = new MoodState(new MoodModel());
        IReadOnlyList<double>? seen = null;
        mood.EmotionsBroadcast += v => seen = v;
        mood.SendEmotionsToGame();
        Assert.NotNull(seen);
        Assert.Equal(9, seen!.Count);
    }

    // M7-009 / M7-010 head-angle conversion and turn-shift sign: the retired IdleBehavior's helpers are gone (R-BEH2 batch 2);
    // the streamer's UpdateLiveAnimation port is tested through the live entry in KeepAliveTests.

    // ---------------------------------------------------------------- M7-021 (Appendix I C2c, C2h)

    /// <summary>
    /// M7-021 C2c: robot+0x338 is the on-charger-contacts boolean, distinct from the platform flag
    /// (+0x34A). It starts 0 from the constructor (0x005100C4) and <c>Robot::SetOnCharger</c> stores
    /// IS_ON_CHARGER into it (extract 0x00512AAE, call 0x00512AB4, store 0x00511C14). Expected from the
    /// inventory, not the code.
    /// </summary>
    [Fact]
    public void TheOnChargerContactsFlagTracksIsOnCharger()
    {
        using var rig = new Rig();
        Assert.False(rig.Robot.Sensors.OnChargerContacts);
        rig.State(flags: (uint)RobotStatusFlag.IsOnCharger);
        Assert.True(rig.Robot.Sensors.OnChargerContacts);
        rig.State(flags: 0);
        Assert.False(rig.Robot.Sensors.OnChargerContacts);
    }

    /// <summary>
    /// M7-021 C2h (as corrected by A3 / gap pass 1 section 4): <c>BehaviorReactToPickup::UpdateInternal</c> evaluates robot+0x355 and robot+0x338 only
    /// when no action is in flight (<c>this+0x84 == 0</c>, 0x00607BBA). So with the contacts set the first <c>StartAnim</c> (after Init's 0.5 s wait) still
    /// runs; the behaviour then logs <c>BehaviorReactToPickup.OnCharger</c> (0x00607BE2) and returns 2. With the contacts clear and the robot still
    /// InAir, it stays alive. (This test used to assert that the reaction "completes without playing"; the engine plays first.) The strategy still
    /// triggers on InAir alone (factory lambda 0x0060DDCE). Expected from the inventory, not the code.
    /// </summary>
    [Fact]
    public void ThePickupReactionCompletesWithoutPlayingOnTheChargerContacts()
    {
        using var rig = new Rig();
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Random = new Random(3) };

        // the head must have reported its calibration for the classifier to run (A1), as CorrectionTests does
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false, AutoStarted = false });
        for (int i = 0; i < 40; i++) rig.State();
        for (int i = 0; i < 10; i++) rig.State((uint)(RobotStatusFlag.IsPickedUp | RobotStatusFlag.IsOnCharger));
        Assert.Equal(OffTreadsState.InAir, rig.Robot.Sensors.OffTreadsState);
        Assert.True(rig.Robot.Sensors.OnChargerContacts);

        var gated = (ReactToPickupBehavior)ShippedBehaviors.Implementable().Single(b => b.Id == "ReactToPickup");
        var lines = new List<string>();
        rig.Robot.Engine.LogLine += lines.Add;
        gated.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        gated.Update(ctx, 0);
        Assert.DoesNotContain(lines, l => l.Contains("BehaviorReactToPickup.OnCharger"));   // the wait is the action in flight
        Assert.False(gated.Update(ctx, 600));                                                // StartAnim ran, then gate 3 returned 2
        Assert.Contains(gated.Trace, l => l.StartsWith("ReactToPickup:"));                   // the fallback 0x1A9 was attempted before the gate
        Assert.Contains(lines, l => l.Contains("BehaviorReactToPickup.OnCharger"));

        // the contacts clear but the robot is still InAir: the same reaction stays alive
        rig.State((uint)RobotStatusFlag.IsPickedUp);
        Assert.False(rig.Robot.Sensors.OnChargerContacts);
        Assert.Equal(OffTreadsState.InAir, rig.Robot.Sensors.OffTreadsState);
        var stays = new ReactToPickupBehavior(rig.Vision);
        stays.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        stays.Update(ctx, 0);
        Assert.True(stays.Update(ctx, 600));
        Assert.True(stays.Update(ctx, 700));
        stays.Stop(BehaviorStopReason.Cancelled);
    }
}
