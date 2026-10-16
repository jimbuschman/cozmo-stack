using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class BehaviorRunnerOwnershipTests
{
    private sealed class Runner : ActionRunner
    {
        public int Deletes, Preps;
        public Action? Deleting;
        public Runner() : base(8, 0) { }
        protected override string ActionName => "owned-action";
        public override bool CanInterrupt() => false;
        public override uint CheckIfDone() => 0;
        public override void Prep() { Preps++; base.Prep(); }
        public override void WatcherEnding() { Deletes++; Deleting?.Invoke(); base.WatcherEnding(); }
    }
    private sealed class Probe : SteppedBehavior
    {
        public Action<Probe>? Starting;
        public Probe() : base("ownership-probe", "Probe") { }
        protected override bool KeepsRunningWithoutAction => true;
        protected override void OnStart() => Starting?.Invoke(this);
    }
    private static (CozmoRobot Robot, BehaviorContext Context, Probe Probe) Rig()
    {
        var robot = CozmoRobot.CreateOffline(); robot.Transport.OfflineAcceptConnection();
        var context = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap() };
        var probe = new Probe(); probe.AttachRun(context, new BehaviorScope());
        return (robot, context, probe);
    }
    private static RobotCompletedAction Completion(Runner runner, uint result = 0)
        => new(runner.Tag, runner.Type, result, Array.Empty<uint>(), 0);

    [Theory]
    [InlineData("A0")] [InlineData("NotRunning")] [InlineData("AlreadyActing")] [InlineData("QueueFailure")]
    public void EveryRejectDeletesIncomingWithoutQueuePrepAndPreservesTheNativeTagAndCallbackOrder(string branch)
    {
        var (robot, context, probe) = Rig(); using var dispose = robot;
        Runner? first = null; var rejected = new Runner(); int queues = 0;
        probe.QueueOwnedAction = (position, runner, retries) =>
        { Assert.Equal(QueueActionPosition.Now, position); Assert.Equal((byte)0, retries); queues++; return 0; };
        if (branch != "NotRunning") probe.StartAsync(context, new(), default).GetAwaiter().GetResult();
        if (branch == "A0") probe.StopOnNextActionComplete();
        if (branch == "AlreadyActing") { first = new Runner(); Assert.True(probe.StartActing(first, _ => { })); }
        int before = queues;
        uint oldTag = probe.CurrentRunnerTag;
        Action<RobotCompletedAction> callback = _ => { };
        if (branch == "QueueFailure")
        {
            probe.QueueOwnedAction = (_, runner, _) =>
            {
                Assert.Same(rejected, runner); Assert.Same(callback, probe.SharedHandle);
                Assert.Equal(rejected.Tag, probe.CurrentRunnerTag); Assert.Same(rejected, probe.OwnedRunner);
                Assert.Equal(0, rejected.Deletes); queues++; return 7;
            };
        }
        Assert.False(probe.StartActing(rejected, callback));
        Assert.Equal(1, rejected.Deletes); Assert.Equal(0, rejected.Preps);
        Assert.Equal(branch == "QueueFailure" ? before + 1 : before, queues);
        Assert.Equal(branch == "QueueFailure" ? rejected.Tag : oldTag, probe.CurrentRunnerTag);
        if (branch == "QueueFailure") { Assert.Same(callback, probe.SharedHandle); Assert.Null(probe.OwnedRunner); }
        if (branch == "AlreadyActing") { Assert.Same(first, probe.OwnedRunner); first!.WatcherEnding(); }
    }

    [Theory]
    [InlineData(0u)] [InlineData(0x02000000u)] [InlineData(0x02000001u)]
    public void CompletionClearsTagAndBonusBeforeCallingTheInstalledCallbackRegardlessOfResult(uint result)
    {
        var (robot, context, probe) = Rig(); using var dispose = robot;
        probe.StartAsync(context, new(), default).GetAwaiter().GetResult();
        probe.QueueOwnedAction = (_, _, _) => 0;
        var runner = new Runner(); int callbacks = 0;
        Assert.True(probe.StartActing(runner, completed =>
        {
            Assert.Equal(result, completed.Result); Assert.False(probe.HasCurrentAction);
            Assert.Null(probe.OwnedRunner); Assert.Equal(0, probe.RunningScoreBonus); callbacks++;
        }));
        probe.IncreaseScoreWhileActing(5);
        probe.HandleActionComplete(Completion(runner) with { Tag = runner.Tag + 1 });
        Assert.Equal(0, callbacks); Assert.True(probe.HasCurrentAction);
        probe.HandleActionComplete(Completion(runner, result));
        Assert.Equal(1, callbacks);
        probe.HandleActionComplete(Completion(runner, result)); Assert.Equal(1, callbacks);
        runner.WatcherEnding();
    }

    [Fact]
    public void NestedStartReplacesTheCallbackWithoutAnAfterCallRestoreOrClear()
    {
        var (robot, context, probe) = Rig(); using var dispose = robot;
        probe.StartAsync(context, new(), default).GetAwaiter().GetResult(); probe.QueueOwnedAction = (_, _, _) => 0;
        var first = new Runner(); var second = new Runner(); var calls = new List<int>();
        Action<RobotCompletedAction> next = _ => calls.Add(2);
        Assert.True(probe.StartActing(first, _ =>
        { calls.Add(1); Assert.True(probe.StartActing(second, next)); Assert.Same(next, probe.SharedHandle); }));
        probe.HandleActionComplete(Completion(first));
        Assert.Equal(second.Tag, probe.CurrentRunnerTag); Assert.Same(next, probe.SharedHandle);
        probe.HandleActionComplete(Completion(second)); Assert.Equal(new[] { 1, 2 }, calls);
        first.WatcherEnding(); second.WatcherEnding();
    }

    [Fact]
    public void StopOnNextActionCompleteClearsTheInstalledCallableButMatchingCompletionStillClearsTag()
    {
        var (robot, context, probe) = Rig(); using var dispose = robot;
        probe.StartAsync(context, new(), default).GetAwaiter().GetResult(); probe.QueueOwnedAction = (_, _, _) => 0;
        var runner = new Runner(); int calls = 0;
        Assert.True(probe.StartActing(runner, _ => calls++));
        probe.StopOnNextActionComplete(); probe.HandleActionComplete(Completion(runner));
        Assert.Equal(0, calls); Assert.False(probe.HasCurrentAction); Assert.Null(probe.SharedHandle);
        runner.WatcherEnding();
    }

    [Fact]
    public void AnAlreadyDestroyedQueueRejectionSkipsTheFreedNameAndSecondDeleteUnderM1_034()
    {
        var (robot, context, probe) = Rig(); using var dispose = robot;
        probe.StartAsync(context, new(), default).GetAwaiter().GetResult();
        var logs = new List<EngineLogEntry>(); robot.Engine.LogEntry += logs.Add;
        var runner = new Runner();
        // A queue-result fixture, not a claim that a shipped behavior survives teardown (M8-024).
        probe.QueueOwnedAction = (_, action, _) => { action.WatcherEnding(); return 1; };
        Assert.False(probe.StartActing(runner, null)); robot.Engine.FlushSourceLogger();
        Assert.Equal(1, runner.Deletes); Assert.Null(probe.OwnedRunner); Assert.Equal(runner.Tag, probe.CurrentRunnerTag);
        Assert.DoesNotContain(logs, e => e.Name == "IBehavior.StartActing.Failure.NotQueued");
    }

    [Fact]
    public void ActualBehaviorStartQueuesTheOwnedRunnerOnTheRealListAndCompletesThroughTheReceiver()
    {
        var (robot, context, probe) = Rig(); using var dispose = robot;
        var list = robot.Engine.Robot!.ActionList; var runner = new Runner(); int callbacks = 0;
        // Explicit message recipient for this fixture; the IBehavior subscription lifetime/census is separate.
        list.GameSend = probe.HandleActionComplete;
        probe.Starting = b => Assert.True(b.StartActing(runner, completed =>
        { Assert.Equal(0u, completed.Result); Assert.False(b.HasCurrentAction); callbacks++; }));
        probe.StartAsync(context, new(), default).GetAwaiter().GetResult();
        Assert.Same(runner, probe.OwnedRunner); Assert.Equal(0, runner.Deletes);
        list.Update();
        Assert.Equal(1, runner.Deletes); Assert.Equal(1, callbacks); Assert.False(probe.HasCurrentAction);
    }

    [Theory]
    [InlineData("NotRunning", "Behavior 'ownership-probe' can't start owned-action action because it is not running")]
    [InlineData("AlreadyActing", "Behavior 'ownership-probe' can't start owned-action action because it is already running an action in state ")]
    [InlineData("NotQueued", "Behavior 'ownership-probe' can't queue action 'owned-action' (error 7)")]
    public void RejectWarningsUseTheNativeFormatAndLevel(string branch, string expected)
    {
        // Native Warning calls 5BDB3A, 5BDBA4, 5BDBDE; empty vector and no channel.
        var (robot, context, probe) = Rig(); using var dispose = robot;
        var logs = new List<EngineLogEntry>(); robot.Engine.LogEntry += logs.Add;
        Runner? first = null;
        if (branch != "NotRunning") probe.StartAsync(context, new(), default).GetAwaiter().GetResult();
        if (branch == "AlreadyActing")
        {
            probe.QueueOwnedAction = (_, _, _) => 0;
            first = new Runner(); Assert.True(probe.StartActing(first, null));
        }
        if (branch == "NotQueued") probe.QueueOwnedAction = (_, _, _) => 7;
        Assert.False(probe.StartActing(new Runner(), null)); robot.Engine.FlushSourceLogger();
        var warning = Assert.Single(logs, e => e.Name == "IBehavior.StartActing.Failure." + branch);
        Assert.Equal(EngineLogLevel.Warning, warning.Level); Assert.Null(warning.Channel);
        Assert.Matches(@"^\(tc\d+\) : " + System.Text.RegularExpressions.Regex.Escape(expected) + "$", warning.Text);
        Assert.Empty(warning.Data);
        first?.WatcherEnding();
    }
}
