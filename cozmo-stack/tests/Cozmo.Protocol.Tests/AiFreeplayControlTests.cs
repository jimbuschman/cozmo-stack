using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class AiFreeplayControlTests
{
    [Fact]
    public void AFailedScoredChoiceFromAnEmptySlotReportsWaitToWaitOncePerTick()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
        f.Rig.Engine.DrainAppDelivery();
        var failing = new FailedInitProbe();
        var chooser = new FailedInitChooser { Desired = failing };
        manager.Activity = chooser;
        var reports = new List<BehaviorTransitionReport>();
        var body = new List<byte[]>();
        f.Rig.Engine.BehaviorTransition += reports.Add;
        f.Rig.Engine.EngineToGameMessage += (tag, payload) =>
        {
            if (tag == 0x87) body.Add(payload.ToArray());
        };

        // 0x005A2AB4 enters the switch once for the chosen pointer. The failed
        // Init at 0x005A1E94 stores null before sending at 0x005A1F1C.
        for (int tick = 1; tick <= 5; tick++)
        {
            f.Rig.Tick();
            f.Rig.Engine.DrainAppDelivery();
            Assert.Equal(tick, chooser.Choices);
            Assert.Equal(tick, failing.Starts);
            Assert.Null(manager.Current);
            Assert.Equal(tick, reports.Count);
            Assert.Equal(tick, body.Count);
            Assert.Equal(new BehaviorTransitionReport(0xB2, 0xB2, 0x39, 0x0B, 0x39, 0x0B, ""), reports[^1]);
            Assert.Equal(new byte[] { 0xB2, 0xB2, 0x39, 0x0B, 0x39, 0x0B, 0 }, body[^1]);
        }

        // 0x005A2A74..0x005A2A80 skips the switch when both pointers are null.
        chooser.Desired = null;
        f.Rig.Tick();
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(6, chooser.Choices);
        Assert.Equal(5, failing.Starts);
        Assert.Equal(5, reports.Count);
        Assert.Equal(5, body.Count);
    }

    private sealed class FailedInitProbe : SteppedBehavior
    {
        internal FailedInitProbe() : base("InteractWithFaces", "InteractWithFaces") { }
        internal int Starts { get; private set; }
        protected override void OnStart()
        {
            Starts++;
            InitFailed = true;
        }
    }

    private sealed class FailedInitChooser : IManagedActivity
    {
        internal IBehavior? Desired { get; set; }
        internal int Choices { get; private set; }

        public void Update(double nowSec) { }
        public IBehavior? GetDesiredActiveBehavior(IBehavior? current, double nowSec)
        {
            Assert.Null(current);
            Choices++;
            return Desired;
        }
        public void BehaviorSwitched(IBehavior? desired, bool started, double nowSec) => Assert.False(started);
    }

    [Fact]
    public void OrdinaryFreeplayContinuesChoosingAcrossThreeSimulatedMinutes()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(
            preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        var messages = new HashSet<string>();
        void Capture(string line)
        {
            if (line.Contains("MISSING", StringComparison.Ordinal))
                lock (messages) messages.Add(line);
        }
        var engine = f.Rig.Engine;
        var typed = new List<BehaviorTransitionReport>();
        var packed = new List<byte[]>();
        var publicState = new List<RobotPublicStateReport>();
        engine.BehaviorTransition += typed.Add;
        engine.EngineToGameMessage += (tag, body) => { if (tag == 0x87) packed.Add(body.ToArray()); };
        engine.RobotPublicStateChanged += publicState.Add;
        engine.StackLogLine += Capture;
        SteppedBehavior.MissingReported += Capture;
        try
        {
            var manager = f.Robot.BehaviorManagerOwner!;
            var freeplay = f.Runtime.FreeplayObserver;
            var hikingReports = new List<(byte Child, string Name)>();
            using var subscription = f.Robot.BehaviorPublicState.Subscribe(state =>
            {
                if (state.Activity == 5)
                    hikingReports.Add((freeplay.SelectedChildActivityId, freeplay.Freeplay.NativeActivityName));
            });
            var transitions = new List<(float Seconds, string Id)>();
            string? prior = null;
            long initialMotionFrames = f.M.Vision.MotionFrameInputs;
            int animationMark = f.Rig.Mark();
            long end = f.Rig.NowNs + 180_000_000_000L;
            int ticks = 0;
            f.Rig.Robot.SetEnableFreeplayActivity(true);
            while (f.Rig.NowNs < end)
            {
                var starts = f.Rig.SentSince(animationMark).OfType<StartOfAnimation>().ToArray();
                animationMark = f.Rig.Mark();
                foreach (var start in starts)
                {
                    f.Rig.Data(new AnimationStarted { AnimId = start.AnimId });
                    f.Rig.Data(new AnimationEnded { AnimId = start.AnimId });
                }
                f.Motors();
                if (++ticks % 20 == 0)
                    f.NativeCameraFrame((uint)ticks, 150);
                string? id = manager.Current?.Id;
                if (id != prior && id is not null) transitions.Add((f.Rig.Engine.Timer.SecondsF, id));
                prior = id;
            }
            Assert.Same(freeplay, manager.Activity);
            string[] captured;
            lock (messages) captured = messages.ToArray();
            string Diagnostic() => string.Join(" | ", freeplay.Decisions.TakeLast(20)) + "\n"
                + string.Join(" | ", transitions) + "\n" + string.Join("\n", captured);
            Assert.True(f.M.Vision.MotionFrameInputs > initialMotionFrames, Diagnostic());
            Assert.True(freeplay.Decisions.Any(d => d.Activity == "Hiking"), Diagnostic());
            Assert.True(!captured.Any(line => line.Contains("M8-013 scored chooser input unavailable")), Diagnostic());
            Assert.DoesNotContain(captured, line => line.Contains("SetActivityIDFromSubActivity"));
            float lastMinute = (end - 60_000_000_000L) / 1_000_000_000f;
            Assert.True(transitions.Select(t => t.Id).Distinct().Count() >= 2
                && transitions.Any(t => t.Seconds >= lastMinute), Diagnostic());

            // BehaviorTransition 0x005A23DA..0x005A2466 reports the new endpoint and its authored key.
            var transition = Assert.Single(typed.Where(t => t.DisplayKey.Length != 0).Take(1));
            var config = engine.HostBehaviorMap![(BehaviorID)transition.NewId];
            Assert.Equal(config.GetProperty("displayNameKey").GetString(), transition.DisplayKey);
            Assert.Equal(transition.NewClass, (byte)Enum.Parse<BehaviorClass>(config.GetProperty("behaviorClass").GetString()!));
            byte[] key = Encoding.UTF8.GetBytes(transition.DisplayKey);
            Assert.Equal(new byte[] { transition.OldId, transition.NewId, transition.OldClass, transition.OldExecutable,
                transition.NewClass, transition.NewExecutable, (byte)key.Length }.Concat(key),
                packed[typed.IndexOf(transition)]);
            Assert.Equal(typed.Count, packed.Count);

            // The native activity selection publishes Hiking (ActivityID 5). The tick copies needs 1,0,2
            // at 0x0064D7A2..0x0064D7BA, with the no-reaction sentinel from 0x005A19EC.
            Assert.Contains(publicState, state => state.ActivityId == 5 && state.ReactionTrigger == 0x16);
            Assert.Contains(((byte)5, "Freeplay_Hiking"), hikingReports);
            var state = publicState.Last();
            var needs = engine.Needs.State;
            Assert.InRange(MathF.Abs(state.Energy - (float)needs.GetNeedLevel(NeedId.Energy)), 0, 1e-5f);
            Assert.InRange(MathF.Abs(state.Repair - (float)needs.GetNeedLevel(NeedId.Repair)), 0, 1e-5f);
            Assert.InRange(MathF.Abs(state.Play - (float)needs.GetNeedLevel(NeedId.Play)), 0, 1e-5f);
            Assert.False(state.IsCarrying);
            Assert.Equal(0x55, state.UnlockId);
        }
        finally
        {
            SteppedBehavior.MissingReported -= Capture;
            f.Rig.Engine.StackLogLine -= Capture;
        }
    }

    [Fact]
    public void ConfiguredParentConsumersUseTheProjectedActivityName()
    {
        using var rig = new M4ControlTests.Rig(PickupPyramidNativeExecutionTests.Resources());
        rig.ToSynced();
        var robot = rig.Engine.Robot!;
        var analyzer = new AIInformationAnalyzer(null);
        const AnimationTrigger trigger = AnimationTrigger.HikingLookAround;
        var parent = new Activity
        {
            Id = "Freeplay", Type = "Freeplay", Strategy = new ActivityStrategy(),
            NativeDrivingFacade = rig.Robot, GuardNativeLifecycle = true,
            NativePublicStateSelected = () => robot.BehaviorPublicState.UpdateActivity(3),
            DriveStartAnim = trigger, IdleAnim = trigger, InfoAnalyzerProcess = 1, Analyzer = analyzer
        };
        parent.NativeCommonDrivingPush = () => parent.PushOwnedDriving(robot, new(trigger, trigger, trigger));
        parent.SetActivityIdFromSubActivity("Hiking");
        parent.OnSelected(10);

        Assert.Equal((byte)3, robot.BehaviorPublicState.State.Activity);
        Assert.Contains(rig.Robot.Animations.DrivingAnimations.Entries, entry => entry.LockName == "Freeplay_Hiking");
        Assert.Contains(rig.Robot.Animations.Scheduler.IdleStack, entry => entry.Lock == "Activity_Freeplay_Hiking");
        Assert.True(analyzer.IsProcessRunning(1));
        analyzer.RemoveEnableRequest(1, "Freeplay");
        Assert.True(analyzer.IsProcessRunning(1));

        parent.OnDeselected(11);
        Assert.DoesNotContain(rig.Robot.Animations.DrivingAnimations.Entries, entry => entry.LockName == "Freeplay_Hiking");
        Assert.DoesNotContain(rig.Robot.Animations.Scheduler.IdleStack, entry => entry.Lock == "Activity_Freeplay_Hiking");
        Assert.False(analyzer.IsProcessRunning(1));
    }

    [Fact]
    public void DisableSelectsSelectionAndRequestsIndefiniteWait()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var manager = f.Robot.BehaviorManagerOwner!;
        var selectionActivity = manager.Activity;
        var selection = Assert.IsType<SelectionChooser>(f.Runtime.Tree.Single(a => a.Id == "Selection").Chooser);
        f.Rig.Robot.SetEnableFreeplayActivity(true);
        f.Rig.Tick();
        Assert.Same(f.Runtime.FreeplayObserver, manager.Activity);
        Assert.NotSame(selectionActivity, manager.Activity);

        f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.Rig.Tick();
        Assert.NotSame(f.Runtime.FreeplayObserver, manager.Activity);
        Assert.Same(selection.Wait, selection.Requested);
        Assert.Equal(-1, selection.NumRuns);
        Assert.Same(selection.Wait, manager.Current);
    }

    [Fact]
    public async Task LowEnergyDuringOutsideAnimationWaitsForTheCommandAndFreeplayResumes()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        var engine = f.Rig.Engine;
        var manager = f.Robot.BehaviorManagerOwner!;
        engine.AnimationMessages.Deliver(0xC6, false, new[] { 1f, 1f, 1f });
        f.Rig.Robot.SetEnableFreeplayActivity(true);
        f.Rig.Tick(78_000);
        var freeplay = f.Runtime.FreeplayObserver;
        Assert.Same(freeplay, manager.Activity);
        var events = new List<string>();
        uint watchedTag = 0;
        engine.ActionCompleted += report => { if (report.Tag == watchedTag) events.Add($"complete:{report.Result}"); };
        engine.BehaviorTransition += report => events.Add($"behavior:{(BehaviorID)report.NewId}");
        int mark = f.Rig.Mark();
        var request = f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.CozmoSaysSpeakLoop);
        var runner = Assert.IsAssignableFrom<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
        watchedTag = runner.Tag;
        Assert.NotSame(freeplay, manager.Activity);
        Assert.Same(f.Runtime.Bound["Wait"], manager.Current);
        for (int i = 0; i < 20 && !f.Rig.SentSince(mark).OfType<StartOfAnimation>().Any(); i++) f.Rig.Tick();
        Assert.True(runner.Initialized);
        byte animationTag = Assert.Single(f.Rig.SentSince(mark).OfType<StartOfAnimation>()).AnimId;
        f.Rig.Data(new AnimationStarted { AnimId = animationTag });
        f.Rig.Tick();
        engine.AnimationMessages.Deliver(0xC6, false, new[] { 1f, 0.4f, 1f });
        f.Rig.Tick(62);
        Assert.Same(f.Runtime.Bound["Wait"], manager.Current);
        Assert.False(request.IsCompleted);
        f.Rig.Data(new AnimationEnded { AnimId = animationTag });
        f.Rig.Tick();

        var result = await request.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(0u, result.EngineResult);
        Assert.False(f.Robot.ActionList.ContainsRunner(runner));
        f.Rig.Tick();
        Assert.Same(freeplay, manager.Activity);
        int complete = events.IndexOf("complete:0");
        Assert.True(complete >= 0);
        Assert.Contains(events.Skip(complete + 1), message => message.StartsWith("behavior:") && message != "behavior:Wait");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASecondCommandHoldsSelectionAndExplicitDisableTakesPrecedence(bool disable)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        var freeplay = f.Runtime.FreeplayObserver;
        f.Rig.Robot.SetEnableFreeplayActivity(true);
        f.Rig.Tick();
        var first = f.Rig.Robot.Motion.QueueAiHeadAngleAsync(0);
        using var cancellation = new CancellationTokenSource();
        var second = f.Rig.Robot.Motion.QueueAiLiftHeightAsync(80, cancellation.Token);
        var head = Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction());
        head.State = EngineActionResult.Success;
        f.Robot.ActionList.Update();
        Assert.Equal(0u, (await first.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        Assert.NotSame(freeplay, manager.Activity);
        if (disable) f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.Rig.Tick();
        Assert.False(second.IsCompleted);
        Assert.Equal(EngineActionResult.Running, f.Robot.ActionList.GetCurrentAction()!.State);
        cancellation.Cancel();
        f.Rig.Tick();
        Assert.Equal(EngineActionResult.Cancelled, (await second.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        Assert.Equal(!disable, ReferenceEquals(freeplay, manager.Activity));
    }

    [Fact]
    public async Task FreeplayRequestPostedBeforeACommandTakesEffectAfterTheCommandRetires()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var manager = f.Robot.BehaviorManagerOwner!;
        var freeplay = f.Runtime.FreeplayObserver;
        f.Rig.Robot.SetEnableFreeplayActivity(true);
        using var cancellation = new CancellationTokenSource();
        var command = f.Rig.Robot.Motion.QueueAiLiftHeightAsync(80, cancellation.Token);
        f.Rig.Tick();
        Assert.NotSame(freeplay, manager.Activity);
        Assert.False(command.IsCompleted);
        cancellation.Cancel();
        f.Rig.Tick();
        Assert.Equal(EngineActionResult.Cancelled, (await command.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        Assert.Same(freeplay, manager.Activity);
    }
}
