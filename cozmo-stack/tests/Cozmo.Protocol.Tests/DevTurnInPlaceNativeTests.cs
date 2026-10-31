using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class DevTurnInPlaceNativeTests
{
    private static string Config()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "re-analysis", "obb", "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "behaviors", "devBehaviors", "devTurnInPlaceTest.json");
            if (File.Exists(p)) return File.ReadAllText(p);
        }
        throw new FileNotFoundException("shipped DevTurn config");
    }
    private static NativeDevTurnInPlaceTestBehavior Create(Fixture f, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var ctx = new BehaviorFactoryContext { Manipulation = f.M, NativeRobot = f.Robot };
        var factory = new BehaviorFactory();
        var container = new BehaviorContainer(factory, ctx, Array.Empty<KeyValuePair<BehaviorID, JsonElement>>());
        var b = Assert.IsType<NativeDevTurnInPlaceTestBehavior>(factory.CreateBehavior(container, BehaviorClass.DevTurnInPlaceTest, ctx, doc.RootElement, out var status));
        Assert.Equal(BehaviorCreateStatus.Created, status);
        return b;
    }
    // reach: unit; factory supplies actual Robot and shipped config, original app initiator remains UNKNOWN. 005CA550..756, 005CA8F6.
    [Fact]
    public void FactoryRetainsShippedTestsAndOwnGateRejects()
    {
        using var f = new Fixture(); using var b = Create(f, Config());
        Assert.Equal(new[] { 90f, -90f, 180f, -180f }, b.Tests.Select(t => t.Angle));
        Assert.All(b.Tests, t => { Assert.Equal(150f, t.Speed); Assert.Equal(300f, t.Accel); Assert.Equal(2f, t.Tolerance); Assert.Equal((byte)0, t.Runs); });
        var ctx = new BehaviorContext { Robot = f.Rig.Robot, Triggers = new AnimationTriggerMap() };
        Assert.False(b.IsRunnable(ctx));
    }
    // reach: unit; direct Init and completion reports substitute the manager entry and action execution. 005CA902..946, 005CAA1A..ACC, 005CABD4..CACCA.
    [Fact]
    public void InitBuildsTurnThenWaitAndResultFreeCallbacksAdvance()
    {
        using var f = new Fixture(); using var b = Create(f, Config());
        var created = new List<CompoundActionSequential>();
        b.ActionConstructedForTest = created.Add;
        b.QueueOwnedAction = (_, _, _) => 0;
        var ctx = new BehaviorContext { Robot = f.Rig.Robot, Triggers = new AnimationTriggerMap() };
        b.Tests[0].Runs = 7;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Equal((byte)0, b.Tests[0].Runs);
        for (int i = 0; i < 4; i++)
        {
            var seq = created[i]; Assert.Equal(2, seq.Children.Count);
            var turn = Assert.IsType<NativeTurnInPlaceRunner>(seq.Children[0]).Parameters;
            float factor = BitConverter.UInt32BitsToSingle(0x3C8EFA35);
            Assert.Equal(new[] { 90f, -90f, 180f, -180f }[i] * factor, turn.Angle0x88);
            Assert.Equal(150f * factor, turn.Speed0xC4); Assert.Equal(300f * factor, turn.Accel0xC8);
            Assert.Equal(BitConverter.UInt32BitsToSingle(0x3D0EFA35), turn.Tolerance0xB0);
            Assert.Equal(0f, turn.Variability0xB8);
            Assert.Equal(1f, Assert.IsType<NativeTimedWaitRunner>(seq.Children[1]).Duration);
            b.HandleActionComplete(new RobotCompletedAction(seq.Tag, 0, i == 1 ? 0x03000004u : 0, Array.Empty<uint>(), 0));
        }
        Assert.Equal(4, created.Count); Assert.Equal(4, b.Index);
        Assert.All(b.Tests, t => Assert.Equal((byte)1, t.Runs));
        b.ResetTests(); Assert.Equal(0, b.Index); Assert.Equal(4, b.Tests.Count); Assert.All(b.Tests, t => Assert.Equal((byte)0, t.Runs));
        b.Stop(BehaviorStopReason.Interrupted);
    }
    // reach: unit; direct Init and completion reports substitute the manager entry and action execution. Loop resets index only, 005CAC70..8E.
    [Fact]
    public void LoopPreservesRunCounters()
    {
        using var f = new Fixture(); using var b = Create(f, Config().Replace("\"loopForever\": false", "\"loopForever\": true"));
        var created = new List<CompoundActionSequential>(); b.ActionConstructedForTest = created.Add; b.QueueOwnedAction = (_, _, _) => 0;
        b.StartAsync(new BehaviorContext { Robot = f.Rig.Robot, Triggers = new AnimationTriggerMap() }, new BehaviorScope(), default).GetAwaiter().GetResult();
        for (int i = 0; i < 4; i++) b.HandleActionComplete(new RobotCompletedAction(created[i].Tag, 0, 0, Array.Empty<uint>(), 0));
        Assert.Equal(5, created.Count); Assert.Equal(0, b.Index); Assert.All(b.Tests, t => Assert.Equal((byte)1, t.Runs));
        b.Stop(BehaviorStopReason.Interrupted);
    }
    // reach: unit; direct Init substitutes the manager entry. Real configured base inputs and the three native gates are exercised.
    [Fact]
    public void FalseOwnGateAndBaseGatesRefuseAdmissionAndResume()
    {
        using var f = new Fixture(); using var b = Create(f, Config());
        var ctx = f.Runtime.Context;
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        Assert.NotNull(ctx.Robot.Animations.Library);
        Assert.True(b.IsRunnableBase(ctx)); Assert.False(b.IsRunnable(ctx));
        b.RobotState355 = () => true; Assert.False(b.IsRunnableBase(ctx)); b.RobotState355 = () => false;
        f.Rig.Robot.Sensors.SetOnChargerPlatform(true); Assert.False(b.IsRunnableBase(ctx));
        f.Rig.Robot.Sensors.SetOnChargerPlatform(false);
        f.Robot.CarryingOwner!.SetCarrying(7); Assert.False(b.IsRunnableBase(ctx));
        f.Robot.CarryingOwner.ClearNativeCarryingFields(false);
        using var scope = new BehaviorScope(); b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        b.Stop(BehaviorStopReason.Interrupted); scope.Dispose();
        using var next = new BehaviorScope(); b.AttachRun(ctx, next);
        Assert.True(b.Resume(null, f.Rig.Engine.Timer.SecondsF)); Assert.False(b.HasCurrentAction);
    }
    // reach: unit; direct Init substitutes the manager entry. Actual ActionList and manager reaction table; 005CA918..940, 005CAD30.
    [Fact]
    public void StopCancelsRealActionAndScopeReleaseAllowsRestart()
    {
        using var f = new Fixture(); using var b = Create(f, Config());
        var manager = f.Robot.BehaviorManagerOwner!;
        using var scope = new BehaviorScope(manager: manager);
        b.StartAsync(f.Runtime.Context, scope, default).GetAwaiter().GetResult();
        var first = Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        Assert.NotEqual(0u, b.CurrentRunnerTag);
        for (int i = 0; i < 21; i++) Assert.True(manager.HasDisableLock((ReactionTrigger)i, b.Id + "_behaviorLock"));
        b.Stop(BehaviorStopReason.Interrupted); Assert.Equal(0u, b.CurrentRunnerTag);
        Assert.NotSame(first, f.Robot.ActionList.GetCurrentAction());
        scope.Dispose();
        for (int i = 0; i < 21; i++) Assert.False(manager.HasDisableLock((ReactionTrigger)i, b.Id + "_behaviorLock"));
        using var again = new BehaviorScope(manager: manager);
        b.StartAsync(f.Runtime.Context, again, default).GetAwaiter().GetResult();
        Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        Assert.NotEqual(0u, b.CurrentRunnerTag); b.Stop(BehaviorStopReason.Interrupted);
    }
    // reach: unit; direct Init substitutes the manager entry; real turn, sequential compound and engine clock wait.
    [Fact]
    public void RealActionListRunsTurnThenTimedWait()
    {
        using var f = new Fixture(); using var b = Create(f, Config());
        using var scope = new BehaviorScope(manager: f.Robot.BehaviorManagerOwner);
        b.StartAsync(f.Runtime.Context, scope, default).GetAwaiter().GetResult();
        var seq = Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        var turn = Assert.IsType<NativeTurnInPlaceRunner>(seq.Children[0]);
        var wait = Assert.IsType<NativeTimedWaitRunner>(seq.Children[1]);
        f.Rig.Tick(); Assert.Equal(-1f, wait.Deadline);
        for (int i = 0; i < 40 && wait.Deadline < 0; i++) f.Motors();
        Assert.True(turn.DestructionObserved); Assert.True(wait.Deadline >= 0);
        Assert.Equal(0, b.Index); f.Rig.Tick(1100);
        Assert.Equal(1, b.Index); Assert.NotSame(seq, f.Robot.ActionList.GetCurrentAction());
        b.Stop(BehaviorStopReason.Interrupted);
    }
    // reach: unit; supplied completions and direct Init replace manager entry/action execution. 005CAC54..8E.
    [Fact]
    public void RepetitionsUseByteRemainderAndWrapBeforeAdvancing()
    {
        using var f = new Fixture(); using var b = Create(f, Config().Replace("\"nRunsPerTest\": 1", "\"nRunsPerTest\": 2").Replace("\"loopForever\": false", "\"loopForever\": true"));
        var actions = new List<CompoundActionSequential>(); b.ActionConstructedForTest = actions.Add; b.QueueOwnedAction = (_, _, _) => 0;
        b.StartAsync(f.Runtime.Context, new BehaviorScope(), default).GetAwaiter().GetResult();
        b.HandleActionComplete(new RobotCompletedAction(actions[0].Tag, 0, 0, Array.Empty<uint>(), 0));
        Assert.Equal(0, b.Index); Assert.Equal((byte)1, b.Tests[0].Runs);
        b.HandleActionComplete(new RobotCompletedAction(actions[1].Tag, 0, 0, Array.Empty<uint>(), 0));
        Assert.Equal(1, b.Index); Assert.Equal((byte)2, b.Tests[0].Runs);
        b.Tests[1].Runs = 255;
        b.HandleActionComplete(new RobotCompletedAction(actions[2].Tag, 0, 0, Array.Empty<uint>(), 0));
        Assert.Equal((byte)0, b.Tests[1].Runs); Assert.Equal(2, b.Index);
        b.Stop(BehaviorStopReason.Interrupted);
    }
    // reach: unit; supplied completion after disposal tests host retirement, not the native caller.
    [Fact]
    public void RetiredOwnerIgnoresLateCompletion()
    {
        using var f = new Fixture(); var b = Create(f, Config());
        var actions = new List<CompoundActionSequential>(); b.ActionConstructedForTest = actions.Add; b.QueueOwnedAction = (_, _, _) => 0;
        b.StartAsync(f.Runtime.Context, new BehaviorScope(), default).GetAwaiter().GetResult();
        uint tag = actions[0].Tag; b.Dispose();
        b.HandleActionComplete(new RobotCompletedAction(tag, 0, 0, Array.Empty<uint>(), 0));
        Assert.Single(actions); Assert.Equal((byte)0, b.Tests[0].Runs);
    }

    // reach: production manager entry and actual ActionList/watcher/typed completion; fixture supplies motor acknowledgements, state and clock.
    // Original app initiator remains UNKNOWN. Native 005A1E76..94, 005CAA32/8C/9A, 00540AC4..AF4 and 00541F10..38.
    [Fact]
    public void ManagerStartsNonRunnableAndPublishesCompoundTagAndDescendantResults()
    {
        using var f = new Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var manager = f.Robot.BehaviorManagerOwner!;
        var b = Assert.IsType<NativeDevTurnInPlaceTestBehavior>(f.Runtime.Bound["DevTurnInPlaceTest"]);
        Assert.True(b.IsRunnableBase(f.Runtime.Context)); Assert.False(b.IsRunnable(f.Runtime.Context));
        var reports = new List<ActionCompletionReport>();
        var local = new List<RobotCompletedAction>();
        f.Rig.Engine.ActionCompleted += reports.Add;
        using var subscription = f.Rig.Engine.AnimationMessages.SubscribeMessage(RobotCompletedAction.UnionTag, true,
            payload => { if (payload is RobotCompletedAction completion) local.Add(completion); });
        // Fixture precondition: no counter wrap or occupied tags, with xUnit's nonparallel collection.
        var counter = ActionRunnerTagCounter.Global;
        uint next = (uint)typeof(ActionRunnerTagCounter).GetField("_next", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(counter)!;
        Assert.InRange(next, 1u, uint.MaxValue - 2);
        Assert.False(counter.IsInUse(next)); Assert.False(counter.IsInUse(next + 1)); Assert.False(counter.IsInUse(next + 2));
        Assert.True(manager.StartAsync("DevTurnInPlaceTest", f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Same(b, manager.Current);
        var root = Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        var turn = Assert.IsType<NativeTurnInPlaceRunner>(root.Children[0]);
        var wait = Assert.IsType<NativeTimedWaitRunner>(root.Children[1]);
        Assert.Equal(next, turn.Tag); Assert.Equal(next + 1, wait.Tag); Assert.Equal(next + 2, root.Tag);
        uint tag = root.Tag;
        f.Rig.Tick();
        for (int i = 0; i < 40 && wait.Deadline < 0; i++) f.Motors();
        Assert.True(turn.DestructionObserved); Assert.True(wait.Deadline >= 0);
        f.Rig.Tick(1100);
        for (int i = 0; i < 8 && !reports.Any(r => r.Tag == tag); i++) f.Rig.Tick();
        Assert.True(wait.DestructionObserved);
        var report = Assert.Single(reports.Where(r => r.Tag == tag));
        Assert.Equal(0u, report.Result); Assert.Equal(new uint[] { 0 }, report.SubActionResults);
        var actual = Assert.Single(local.Where(r => r.Tag == tag));
        Assert.Equal(report.Tag, actual.Tag); Assert.Equal(0u, actual.Result); Assert.Equal(new uint[] { 0 }, actual.SubActionResults);
        manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
    }

}