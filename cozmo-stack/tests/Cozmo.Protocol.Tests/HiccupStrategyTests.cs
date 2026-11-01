using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class HiccupStrategyTests
{
    private static string Resources()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var root = Path.Combine(d.FullName, "re-analysis", "obb", "assets", "cozmo_resources");
            if (File.Exists(Path.Combine(root, ReactionTriggerMap.RelativePath))) return root;
        }
        throw new DirectoryNotFoundException("shipped resources");
    }

    [Fact]
    public void CanonicalMapRetainsConfiguredTargetAndManagerOwnsStrategyLifetime()
    {
        // reach: canonical robot construction, map 0x005A1842 and dtor 0x005A47FE.
        using var rig = new M4ControlTests.Rig(Resources()); rig.ToSynced(); rig.State();
        var native = rig.Engine.Robot!; var runtime = native.NativeBehaviorRuntime!;
        var manager = native.BehaviorManagerOwner!;
        var registration = Assert.Single(manager.Reactions.Where(r => r.Strategy.Trigger == ReactionTrigger.Hiccup));
        var strategy = Assert.IsType<HiccupReactionStrategy>(registration.Strategy);
        Assert.Same(runtime.Bound["Hiccup"], registration.Behavior);
        Assert.Same(strategy, HiccupReactionStrategy.NativeGlobal);
        Assert.False(runtime.Manipulation!.Whiteboard.HiccupsActive);
        manager.Dispose();
        Assert.Null(HiccupReactionStrategy.NativeGlobal);
        Assert.False(strategy.ShouldTriggerBehavior(new ReactionContext(runtime.Context, 0), registration.Behavior));
    }

    [Fact]
    public void ReportQueuesRawThenTypedAfterSynchronousLocalDelivery()
    {
        using var rig = new M4ControlTests.Rig(); var order = new List<string>();
        rig.Engine.EngineToGameMessage += (tag, body) => { if (tag == 0x38) { Assert.Equal(new byte[] { 1 }, body.ToArray()); order.Add("raw"); } };
        rig.Robot.Sensors.RobotHiccupsChanged += value => { Assert.True(value); order.Add("typed"); };
        using var sub = rig.Engine.AnimationMessages.SubscribeMessage(0x38, true, value => { Assert.Equal(true, value); order.Add("local"); });
        rig.Robot.Sensors.BroadcastHiccupsChanged(true);
        Assert.Equal(new[] { "local" }, order);
        rig.Engine.DrainAppDelivery(); Assert.Equal(new[] { "local", "raw", "typed" }, order);
    }

    [Fact]
    public void HiccupSensorPhasePrecedesReturnedAndWhiteboard()
    {
        using var rig = new M4ControlTests.Rig(); var order = new List<string>();
        rig.Robot.Sensors.FreeplayOffTreadsStateChanged += (_, _) => order.Add("freeplay");
        rig.Robot.Sensors.HiccupOffTreadsStateChanged += (_, _) => order.Add("hiccup");
        rig.Robot.Sensors.ReturnedToTreadsStateChanged += (_, _) => order.Add("returned");
        rig.Robot.Sensors.WhiteboardOffTreadsStateChanged += (_, _) => order.Add("whiteboard");
        rig.Robot.Sensors.DispatchOffTreadsStateChanged(OffTreadsState.OnTreads, OffTreadsState.OnBack);
        Assert.Equal(new[] { "freeplay", "hiccup", "returned", "whiteboard" }, order);
    }

    [Fact]
    public void CanonicalSupportedConstructorDrawsPrecedeHiccupReset()
    {
        // reach: canonical startup; two configured DriveInDesperation draws, then Hiccup count/occurrence.
        using var rig = new M4ControlTests.Rig(Resources()); var owners = new List<string>();
        rig.Engine.ContextRandom.WordSourceForTest = () => {
            string stack = Environment.StackTrace;
            if (stack.Contains("HiccupReactionStrategy")) owners.Add("hiccup");
            else if (stack.Contains("NativeDriveInDesperationBehavior")) owners.Add("drive");
            return 0;
        };
        rig.ToSynced(); rig.State();
        Assert.Equal(new[] { "drive", "drive", "drive", "drive", "hiccup", "hiccup", "hiccup", "hiccup" }, owners);
    }

    [Fact]
    public void Tag53And164RespectManagerGateAndPlayerCureExtendsOnlyOccurrence()
    {
        // reach: isolated strategy with real manager gate; 0x6109CC..610ABE and 0x61098C..9C8.
        using var rig = new M4ControlTests.Rig();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, ObjectType)>()), () => 0);
        uint now = 0; var keep = new List<float>(); var actions = new List<int>(); var das = new List<(bool,uint)>(); var diagnostics = new List<string>();
        var target = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup });
        using var manager = new BehaviorManager(context);
        var s = new HiccupReactionStrategy(new HiccupParams(1, 1, 1, 1, 10, 10, 3, ""), wb,
            new EngineRandom(1), () => now, () => true, () => true, () => 3, _ => 0,
            actions.Add, _ => { }, keep.Add, (p,t) => das.Add((p,t)), diagnostics.Add);
        manager.AddReaction(s,target);
        var mask = new bool[BehaviorManager.TriggerCount]; mask[(int)ReactionTrigger.Hiccup] = true;
        manager.DisableReactionsWithLock("test",mask,false);
        now = 10; s.Receive164(); s.OffTreadsChanged(2); Assert.Empty(keep); Assert.Single(diagnostics);
        manager.RemoveDisableReactionsLock("test");
        s.Receive164(); s.OffTreadsChanged(2); Assert.Empty(keep); // equality does not pass
        now = 11; s.OffTreadsChanged(2); Assert.Equal(new[] { 5f }, keep);
        now = 12; s.OffTreadsChanged(0); Assert.Equal(new[] { 0x19 }, actions); Assert.Equal(new[] { (true,0u) }, das);
        Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context,0),target));
        Assert.Equal(AnimationTrigger.HiccupPlayerCure, Assert.Single(target.Triggers));
        Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context,0),target)); // non-runnable retains cure
        Assert.Equal(AnimationTrigger.HiccupPlayerCure, Assert.Single(target.Triggers));
    }

    [Fact]
    public void ExecuteReactionInputMatchesBothBytesAndFaultingFallbackIsSkipped()
    {
        using var rig = new M4ControlTests.Rig(); var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, ObjectType)>()), () => 0);
        var logs = new List<string>();
        using var s = new HiccupReactionStrategy(new HiccupParams(1, 1, 1, 1, 1, 1, 0, ""), wb,
            new EngineRandom(1), () => 0, () => true, () => true, () => 3, _ => 0,
            _ => { }, _ => { }, _ => { }, (_, _) => { }, logs.Add);
        var target = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup });
        s.ReceiveExecuteReactionTrigger((byte)ReactionTrigger.Hiccup, 3, 4); Assert.False(s.Forced);
        s.ReceiveExecuteReactionTrigger((byte)ReactionTrigger.Hiccup, 4, 4); Assert.True(s.Forced);
        s.ReceiveExecuteReactionTrigger(1, 4, 4); Assert.True(s.Forced);
        Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context,0),target)); Assert.False(s.Forced);
        Assert.Contains(logs, l => l.Contains("M1-034/M10-003") && l.Contains("0x00610412"));
        Assert.Equal(AnimationTrigger.Hiccup, Assert.Single(target.Triggers));
        s.ReceiveExecuteReactionTrigger((byte)ReactionTrigger.Hiccup, 4, 4);
        s.ReceiveExecuteReactionTrigger(0x16, 0, 4); Assert.False(s.Forced);
    }

    [Fact]
    public void RealRobotTeardownReportsActiveHiccupsAndRetiresSubscriptions()
    {
        // reach: actual Robot lifetime and owned reaction map; 0x00511150 -> 0x005A47FE -> 0x0061024C.
        var rig = new M4ControlTests.Rig(Resources()); rig.ToSynced(); rig.State();
        var native = rig.Engine.Robot!; var runtime = native.NativeBehaviorRuntime!;
        var reg = Assert.Single(native.BehaviorManagerOwner!.Reactions.Where(r => r.Strategy.Trigger == ReactionTrigger.Hiccup));
        var s = Assert.IsType<HiccupReactionStrategy>(reg.Strategy);
        native.Progression!.Unlocked.Add(14); // DroneModeGame: real progression recipient
        var logs = new List<EngineLogEntry>(); rig.Engine.LogEntry += entry => logs.Add(entry);
        var unlinks = new List<string>(); rig.Engine.AnimationMessages.ReleaseObserved = unlinks.Add;
        Assert.Equal(1, rig.Engine.AnimationMessages.Count(0x9E,false));
        Assert.Equal(1, rig.Engine.AnimationMessages.Count(164,false));
        rig.Engine.AnimationMessages.Deliver(164,false,null);
        rig.Engine.Timer.UpdateTime(rig.Engine.Timer.Nanoseconds + 1_000_000);
        s.ShouldTriggerBehavior(new ReactionContext(runtime.Context,rig.Engine.Timer.Seconds),reg.Behavior);
        Assert.True(runtime.Manipulation!.Whiteboard.HiccupsActive);
        rig.Engine.Robots.RemoveRobot(native.NativeRobotId, false);
        var ended = Assert.Single(logs.Where(e => e.Name == "robot.hiccups.ended"));
        Assert.Equal("SELF_CURED", ended.Text); Assert.Equal("0", ended.Data["$data"]);
        Assert.Null(HiccupReactionStrategy.NativeGlobal);
        Assert.Contains("unlink G2E 9E", unlinks); Assert.Contains("unlink G2E A4", unlinks);
        Assert.Equal(0, rig.Engine.AnimationMessages.Count(0x9E,false));
        Assert.Equal(0, rig.Engine.AnimationMessages.Count(164,false));
        Assert.False(s.Forced);
        rig.Engine.AnimationMessages.Deliver(0x9E,false,new byte[] { (byte)ReactionTrigger.Hiccup, (byte)BehaviorID.Hiccup });
        Assert.False(s.Forced);
        rig.Dispose();
    }

    [Fact]
    public void RealRobotTeardownBeforeDeadlineDoesNotEmitHiccupEnd()
    {
        using var rig = new M4ControlTests.Rig(Resources()); rig.ToSynced(); rig.State();
        var native = rig.Engine.Robot!; var logs = new List<EngineLogEntry>();
        rig.Engine.LogEntry += entry => logs.Add(entry);
        rig.Engine.Robots.RemoveRobot(native.NativeRobotId,false);
        Assert.DoesNotContain(logs, e => e.Name == "robot.hiccups.ended");
        Assert.Null(HiccupReactionStrategy.NativeGlobal);
    }

    [Fact]
    public void RealRobotTeardownReportsElapsedFromFirstRunnableHiccup()
    {
        using var rig = new M4ControlTests.Rig(Resources());
        rig.Robot.Animations.LoadFrom(Path.Combine(Resources(), "assets"));
        rig.ToSynced(); rig.State();
        var native = rig.Engine.Robot!; var runtime = native.NativeBehaviorRuntime!;
        var reg = Assert.Single(native.BehaviorManagerOwner!.Reactions.Where(r => r.Strategy.Trigger == ReactionTrigger.Hiccup));
        native.Progression!.Unlocked.Add(14);
        var logs = new List<EngineLogEntry>(); rig.Engine.LogEntry += entry => logs.Add(entry);
        rig.Engine.AnimationMessages.Deliver(164,false,null);
        rig.Engine.Timer.UpdateTime(rig.Engine.Timer.Nanoseconds + 1_000_000);
        Assert.True(reg.Strategy.ShouldTriggerBehavior(new ReactionContext(runtime.Context,rig.Engine.Timer.Seconds),reg.Behavior));
        rig.Engine.Timer.UpdateTime(rig.Engine.Timer.Nanoseconds + 5_000_000);
        rig.Engine.Robots.RemoveRobot(native.NativeRobotId,false);
        var ended = Assert.Single(logs.Where(e => e.Name == "robot.hiccups.ended"));
        Assert.Equal("SELF_CURED", ended.Text); Assert.Equal("5", ended.Data["$data"]);
    }
    [Fact]
    public void StrictDeadlinesAndNonRunnableTargetStillWriteStateAndReplaceVector()
    {
        // reach: isolated core, source 0x6106CE..61085C. The rig lacks an animation library.
        using var rig = new M4ControlTests.Rig();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, ObjectType)>()), () => 0);
        uint now = 10; var reports = new List<bool>(); var needs = new List<int>();
        var p = new HiccupParams(1, 1, 2, 2, 100, 100, 3, "DroneModeGame");
        var target = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup, AnimationTrigger.Hiccup });
        using var strategy = new HiccupReactionStrategy(p, wb, new EngineRandom(1), () => now,
            () => true, () => true, () => 3, _ => 0, needs.Add, reports.Add, _ => { }, (_, _) => { }, _ => { });
        now = 1010; Assert.False(strategy.ShouldTriggerBehavior(new ReactionContext(context, 0), target));
        Assert.False(wb.HiccupsActive); Assert.Empty(reports);
        now = 1011; Assert.False(strategy.ShouldTriggerBehavior(new ReactionContext(context, 0), target));
        Assert.True(wb.HiccupsActive); Assert.Equal(new[] { true }, reports);
        Assert.Equal(new[] { AnimationTrigger.HiccupGetIn }, target.Triggers); Assert.Empty(needs);
        now = 1111; Assert.False(strategy.ShouldTriggerBehavior(new ReactionContext(context, 0), target));
        Assert.Equal(AnimationTrigger.HiccupGetIn, Assert.Single(target.Triggers));
        now = 1112; Assert.False(strategy.ShouldTriggerBehavior(new ReactionContext(context, 0), target));
        Assert.Equal(AnimationTrigger.Hiccup, Assert.Single(target.Triggers));
        now = 1213; Assert.False(strategy.ShouldTriggerBehavior(new ReactionContext(context, 0), target));
        Assert.False(wb.HiccupsActive); Assert.Equal(new[] { true, false }, reports);
        Assert.Equal(new[] { 0x18 }, needs);
    }

    [Fact]
    public void DisabledLatchConsumesSpacingButNotCount()
    {
        // reach: isolated core, 0x6107A2..6107CC and 0x610AF8..610B42.
        using var rig = new M4ControlTests.Rig();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, ObjectType)>()), () => 0);
        uint now = 0; var reports = new List<bool>();
        var p = new HiccupParams(0, 0, 1, 1, 10, 10, 0, "DroneModeGame");
        var target = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup });
        using var strategy = new HiccupReactionStrategy(p, wb, new EngineRandom(1), () => now,
            () => true, () => true, () => 3, _ => 0, _ => { }, reports.Add, _ => { }, (_, _) => { }, _ => { });
        strategy.EnabledStateChanged(context, false); now = 1;
        Assert.False(strategy.ShouldTriggerBehavior(new ReactionContext(context, 0), target));
        Assert.True(wb.HiccupsActive); Assert.Empty(reports);
        now = 11; Assert.False(strategy.ShouldTriggerBehavior(new ReactionContext(context, 0), target)); Assert.Empty(reports);
        now = 12; Assert.False(strategy.ShouldTriggerBehavior(new ReactionContext(context, 0), target));
        Assert.Equal(new[] { true }, reports); Assert.Equal(AnimationTrigger.HiccupGetIn, Assert.Single(target.Triggers));
    }

    [Fact]
    public void FeatureAndUnlockGatesControlResetAndDrawOrder()
    {
        // reach: isolated core; 0x6101B2..6101E2, 0x610694..610704, 0x6107AC.
        using var rig = new M4ControlTests.Rig();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, ObjectType)>()), () => 0);
        uint now = 0; int words = 0, missing = 0; bool feature = true; bool? unlock = true;
        var rng = new EngineRandom(1) { WordSourceForTest = () => { words++; return 0; } };
        var target = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup });
        var p = new HiccupParams(1, 2, 2, 3, 10, 20, 0, "DroneModeGame");
        using var s = new HiccupReactionStrategy(p, wb, rng, () => now, () => feature, () => unlock,
            () => 3, _ => 0, _ => { }, _ => { }, _ => { }, (_, _) => { }, _ => missing++);
        Assert.Equal(4, words); // count then occurrence, two MT words each
        feature = false; now = 1001; Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, 0), target)); Assert.Equal(4, words);
        feature = true; unlock = null; Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, 0), target)); Assert.Equal(4, words); Assert.Equal(1, missing);
        unlock = false; Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, 0), target)); Assert.Equal(8, words);
        unlock = true; now = 2001; Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, 0), target)); Assert.Equal(8, words);
        now = 2002; s.ShouldTriggerBehavior(new ReactionContext(context, 0), target); Assert.Equal(10, words);
        Assert.Equal(AnimationTrigger.HiccupGetIn, Assert.Single(target.Triggers));
    }

    [Fact]
    public void WrapUsesUnsignedComparisonAndDestructorClearsGlobalWithoutReset()
    {
        // reach: isolated core; unsigned deadline 0x6106D8 and dtor 0x61024C..280.
        using var rig = new M4ControlTests.Rig();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, ObjectType)>()), () => 0);
        uint now = uint.MaxValue - 500; var events = new List<(bool, uint)>(); int words = 0;
        var rng = new EngineRandom(1) { WordSourceForTest = () => { words++; return 0; } };
        var p = new HiccupParams(1, 1, 1, 1, 10, 10, 0, "DroneModeGame");
        var target = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup });
        var s = new HiccupReactionStrategy(p, wb, rng, () => now, () => true, () => true, () => 3, _ => 0,
            _ => { }, _ => { }, _ => { }, (player, elapsed) => events.Add((player, elapsed)), _ => { });
        s.FinishConstruction();
        Assert.Same(s, HiccupReactionStrategy.NativeGlobal);
        now = 499; Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, 0), target)); Assert.False(wb.HiccupsActive);
        now = 500; s.ShouldTriggerBehavior(new ReactionContext(context, 0), target); Assert.True(wb.HiccupsActive);
        int beforeDispose = words; s.Dispose(); Assert.Null(HiccupReactionStrategy.NativeGlobal);
        Assert.Equal(beforeDispose, words); Assert.True(wb.HiccupsActive); Assert.Equal(new[] { (false, 0u) }, events);
    }

    [Fact]
    public void DisabledCriticalNeedReadsBothBracketsAndSelfCures()
    {
        // reach: isolated core; 0x610AF8..610B42.
        using var rig = new M4ControlTests.Rig();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, ObjectType)>()), () => 0);
        uint now = 0; var reads = new List<int>(); var actions = new List<int>();
        using var s = new HiccupReactionStrategy(new HiccupParams(0, 0, 1, 1, 10, 10, 0, ""), wb,
            new EngineRandom(1), () => now, () => true, () => true, () => 3,
            n => { reads.Add(n); return n == 1 ? 3 : 0; }, actions.Add, _ => { }, _ => { }, (_, _) => { }, _ => { });
        now = 1; s.EnabledStateChanged(context, false);
        Assert.Equal(new[] { 1, 0 }, reads); Assert.Equal(new[] { 0x18 }, actions);
        var target = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup });
        Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, 0), target));
        Assert.Equal(AnimationTrigger.HiccupSelfCure, Assert.Single(target.Triggers));
        Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, 0), target));
        Assert.Equal(AnimationTrigger.HiccupSelfCure, Assert.Single(target.Triggers));
    }

    [Fact]
    public void ReportReentryRetiresBeforeTargetReplacementAndLaterWrites()
    {
        using var rig = new M4ControlTests.Rig();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, ObjectType)>()), () => 0);
        uint now = 0; var actions = new List<int>(); HiccupReactionStrategy? s = null;
        var target = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup });
        s = new HiccupReactionStrategy(new HiccupParams(0, 0, 1, 1, 1, 1, 0, ""), wb,
            new EngineRandom(1), () => now, () => true, () => true, () => 3, _ => 0,
            actions.Add, value => { if (value) s!.Dispose(); }, _ => { }, (_, _) => { }, _ => { });
        now = 1; Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, 0), target));
        Assert.True(wb.HiccupsActive); Assert.Equal(AnimationTrigger.Hiccup, Assert.Single(target.Triggers));
        Assert.Empty(actions); Assert.Null(HiccupReactionStrategy.NativeGlobal);
    }

    [Fact]
    public void ResetReportRetirementDoesNotClearSharedWhiteboard()
    {
        using var rig = new M4ControlTests.Rig();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, ObjectType)>()), () => 0);
        uint now = 0; bool unlock = true; HiccupReactionStrategy? s = null;
        var target = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup });
        s = new HiccupReactionStrategy(new HiccupParams(0, 0, 1, 1, 1, 1, 0, ""), wb,
            new EngineRandom(1), () => now, () => true, () => unlock, () => 3, _ => 0,
            _ => { }, value => { if (!value) s!.Dispose(); }, _ => { }, (_, _) => { }, _ => { });
        now = 1; s.ShouldTriggerBehavior(new ReactionContext(context, 0), target); Assert.True(wb.HiccupsActive);
        unlock = false; Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, 0), target));
        Assert.True(wb.HiccupsActive); Assert.Null(HiccupReactionStrategy.NativeGlobal);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void RunnableOrNeedsReentryCannotReturnSelectionAfterRetirement(bool retireInRunnable)
    {
        using var rig = new M4ControlTests.Rig(Resources());
        rig.Robot.Animations.LoadFrom(Path.Combine(Resources(), "assets"));
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, ObjectType)>()), () => 0);
        uint now = 0; int actions = 0; HiccupReactionStrategy? s = null;
        var target = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup }) {
            RobotStateAllowsRun = () => { if (retireInRunnable) s!.Dispose(); return true; },
            UnlockAllowsRun = () => true, RobotState34a = () => false, RobotState355 = () => false };
        s = new HiccupReactionStrategy(new HiccupParams(0, 0, 1, 1, 1, 1, 0, ""), wb,
            new EngineRandom(1), () => now, () => true, () => true, () => 3, _ => 0,
            _ => { actions++; if (!retireInRunnable) s!.Dispose(); }, _ => { }, _ => { }, (_, _) => { }, _ => { });
        now = 1; Assert.False(s.ShouldTriggerBehavior(new ReactionContext(context, 0), target));
        Assert.Equal(retireInRunnable ? 0 : 1, actions);
    }

}
