using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M5X15BuilderTests : IDisposable
{
    private readonly bool _error = EngineErrorState.ErrorFlagSet;
    public void Dispose() => EngineErrorState.ErrorFlagSet = _error;
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Theory]
    [InlineData("file")] [InlineData("directory")] [InlineData("missing")]
    public void CacheCleanupRunsAtConstructionAndBeforeRobotDestruction(string kind)
    {
        string cache = Directory.CreateTempSubdirectory("m515-").FullName;
        string path = Path.Combine(cache, "TestAnim.json");
        try
        {
            if (kind == "file") File.WriteAllText(path, "x");
            if (kind == "directory") Directory.CreateDirectory(path);
            using var robot = CozmoRobot.CreateForTest(new Port(), () => 0, new CozmoEngineOptions { AnimationCachePath = cache });
            Assert.False(File.Exists(path)); Assert.Equal(kind == "directory", Directory.Exists(path));
            robot.Engine.Robots.AddRobot(1, false);
            if (kind == "file") File.WriteAllText(path, "late");
            robot.Engine.DestructionObserved = phase => { if (phase == "robots") Assert.False(File.Exists(path)); };
            robot.Dispose();
            Assert.False(File.Exists(path)); Assert.Equal(kind == "directory", Directory.Exists(path));
            Assert.Equal("cache/TestAnim.json", CozmoEngine.AnimationCacheFile("cache", "TestAnim.json"));
            Assert.Equal("cache/TestAnim.json", CozmoEngine.AnimationCacheFile("cache", "/TestAnim.json"));
        }
        finally { Directory.Delete(cache, true); }
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ParameterMessageUsesBaseSetterAndPreservesOrderDefaultsAndMismatchTail()
    {
        using var robot = CozmoRobot.CreateForTest(new Port(), () => 0);
        robot.Engine.Robots.AddRobot(1, false);
        var p = robot.Animations.Scheduler.LiveIdleParameters;
        robot.Animations.SetLiveIdleAnimationParameters(new[] { LiveIdleParam.BlinkSpacingMaxTime_ms, LiveIdleParam.BlinkSpacingMaxTime_ms, (LiveIdleParam)255 }, new[] { 35000f, 45000f, 17f }, true);
        Assert.False(p.DefaultsSet); robot.Engine.Tick();
        Assert.Equal(45000f, p[LiveIdleParam.BlinkSpacingMaxTime_ms]); Assert.Equal(17f, p[(LiveIdleParam)255]);
        Assert.Equal(3000f, p[LiveIdleParam.BlinkSpacingMinTime_ms]);
        robot.Animations.SetLiveIdleAnimationParameters(Array.Empty<LiveIdleParam>(), Array.Empty<float>(), true); robot.Engine.Tick();
        Assert.Equal(4000f, p[LiveIdleParam.BlinkSpacingMaxTime_ms]);
        var entries = new List<EngineLogEntry>(); bool? flagAtError = null; EngineErrorState.ErrorFlagSet = false;
        robot.Engine.LogEntry += e => { if (e.Name == "AnimationStreamer.HandleSetLiveAnimationParameter.MismatchedLengths") { entries.Add(e); flagAtError = EngineErrorState.ErrorFlagSet; } };
        robot.Animations.SetLiveIdleAnimationParameters(new[] { LiveIdleParam.BlinkSpacingMaxTime_ms }, Array.Empty<float>(), true); robot.Engine.Tick();
        var error = Assert.Single(entries); Assert.Equal("AnimationStreamer.HandleSetLiveAnimationParameter.MismatchedLengths", error.Name);
        Assert.Equal("(tc0002) : ParamNames and ParamValues not the same length (1 & 0)", error.Text);
        Assert.Equal(false, flagAtError); Assert.True(EngineErrorState.ErrorFlagSet);
        Assert.Equal(4000f, p[LiveIdleParam.BlinkSpacingMaxTime_ms]);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void SubscriptionDirectionsOwnersAndQuietUnfedMembersMatch()
    {
        using var robot = CozmoRobot.CreateOffline(); var hub = robot.Engine.AnimationMessages;
        Assert.Equal(1, hub.Count(0x5A, true)); Assert.Equal(0, hub.Count(0x5A, false));
        Assert.Equal(1, hub.Count(0xB4, false)); Assert.Equal(1, hub.Count(0xAD, false));
        foreach (ushort tag in new ushort[] { 0xF7, 0xE0, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF }) Assert.Equal(1, hub.Count(tag, false));
        var lines = new List<string>(); robot.Engine.StackLogLine += lines.Add;
        robot.Engine.Robots.RemoveRobot(1, false); robot.Engine.Robots.AddRobot(2, false);
        Assert.Contains(lines, l => l.Contains("M5-027/M5-057 subscription 0xAE"));
        Assert.Contains(lines, l => l.Contains("M5-061 subscription 0xAD"));
        Assert.Contains(lines, l => l.Contains("M9-029 subscription 0xC4"));
        Assert.DoesNotContain(lines, l => l.Contains("subscription 0xB4") || l.Contains("owned vector +8"));
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void DrivingStackErasesFirstMatchAndChangesCurrentOnlyOnInit()
    {
        using var robot = CozmoRobot.CreateOffline(); var handler = robot.Animations.DrivingAnimations;
        var a = new DrivingAnimations((AnimationTrigger)0x94, (AnimationTrigger)0x90, (AnimationTrigger)0x8C);
        var b = new DrivingAnimations((AnimationTrigger)0x96, (AnimationTrigger)0x92, (AnimationTrigger)0x8E);
        robot.Animations.PushDrivingAnimations(a.Start, a.Loop, a.End, "same");
        Assert.Equal(new DrivingAnimations((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D), handler.Current);
        robot.Animations.PushDrivingAnimations(b.Start, b.Loop, b.End, "same");
        Assert.Equal(new DrivingAnimations((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D), handler.Current); Assert.True(handler.Init(4, 42, false, false)); Assert.Equal(b, handler.Current);
        robot.Animations.RemoveDrivingAnimations("same"); Assert.Single(handler.Entries); Assert.Equal(b, handler.Entries[0].Animations);
        Assert.Equal(b, handler.Current); robot.Animations.RemoveDrivingAnimations("same"); Assert.Empty(handler.Entries); Assert.Equal(b, handler.Current);
        var keys = new List<string>(); robot.Engine.LogEntry += e => keys.Add(e.Name!);
        robot.Animations.RemoveDrivingAnimations("absent");
        Assert.Equal(new[] { "DrivingAnimationHandler.PopDrivingAnimations", "DrivingAnimationHandler.PopDrivingAnimations" }, keys);
        handler.ActionIsBeingDestroyed();
        for (int i = 0; i < 1001; ++i) handler.Push(a, "prefix");
        keys.Clear(); EngineErrorState.ErrorFlagSet = false; handler.Remove("absent");
        Assert.Equal(new[] { "LoopBoundOverflow" }, keys); Assert.Equal(1001, handler.Entries.Count); Assert.True(EngineErrorState.ErrorFlagSet);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Theory]
    [InlineData(1, false, 0x94, 0x90, 0x8C)] [InlineData(0, true, 0x96, 0x92, 0x8E)] [InlineData(0, false, 0x95, 0x91, 0x8D)]
    public void DrivingInitSelectsMoodBeforeNeedsThenDefault(int mood, bool needs, int start, int loop, int end)
    {
        using var robot = CozmoRobot.CreateOffline(); var handler = robot.Animations.DrivingAnimations;
        var order = new List<string>();
        handler.GetSimpleMoodForTest = () => { order.Add("mood"); return mood; };
        handler.AreNeedsMetForTest = () => { order.Add("needs"); return needs; };
        Assert.True(handler.Init(4, 43, false, false)); Assert.Equal(0, handler.State);
        Assert.Equal(new DrivingAnimations((AnimationTrigger)start, (AnimationTrigger)loop, (AnimationTrigger)end), handler.Current);
        Assert.Equal(mood == 1 ? new[] { "mood" } : new[] { "mood", "needs" }, order);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void CompletedLoopDoubleUnlocksThenEndCompletionLocksAndDestroyCancelsInOrder()
    {
        using var robot = CozmoRobot.CreateOffline(); var handler = robot.Animations.DrivingAnimations;
        handler.Push(new((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D), "test");
        Assert.True(handler.Init(4, 99, false, false));
        var requests = new List<DrivingAnimationRequest>(); uint id = 400;
        handler.CreateActionForTest = r => { requests.Add(r); return new FakeAction { Tag = ++id, OriginalTag = id }; };
        var calls = new List<(string Name, uint Id)>(); handler.CallObserved = (n, i) => calls.Add((n, i));
        robot.Engine.AnimationMessages.Deliver(0x5A, true, new RobotCompletedAction(0, 0, 0, Array.Empty<uint>(), 0));
        Assert.Equal(2, handler.State); Assert.Equal(401u, handler.LoopId);
        var request = Assert.Single(requests); Assert.Equal(new DrivingAnimationRequest((AnimationTrigger)0x91, 1, true, 0, 60f, false), request);
        Assert.Equal(1, robot.Engine.Robot!.ActionList.QueueCount);
        handler.HasPathToFollowForTest = () => false;
        calls.Clear(); robot.Engine.AnimationMessages.Deliver(0x5A, true, new RobotCompletedAction(401, 0, 0, Array.Empty<uint>(), 0));
        Assert.Equal(new[] { ("UnlockTracks", 99u), ("Cancel", 0u), ("Cancel", 401u), ("UnlockTracks", 99u) }, calls);
        Assert.Equal(3, handler.State); Assert.Equal(402u, handler.EndId);
        calls.Clear(); robot.Engine.AnimationMessages.Deliver(0x5A, true, new RobotCompletedAction(402, 0, 123, Array.Empty<uint>(), 0));
        Assert.Equal(4, handler.State); Assert.Equal(new[] { ("LockTracks", 99u) }, calls);
        Assert.True(robot.Motion.AreAnyTracksLocked(4));
        calls.Clear(); handler.ActionIsBeingDestroyed(); Assert.Equal(5, handler.State);
        Assert.Equal(new[] { ("Cancel", 0u), ("Cancel", 401u), ("Cancel", 402u) }, calls);
        Assert.Equal(0u, handler.PlayEndAnim());
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void MissingMoodAndNeedsFallThroughToDefaultAndQueueARealRunner()
    {
        using var robot = CozmoRobot.CreateOffline(); var handler = robot.Animations.DrivingAnimations;
        var lines = new List<string>(); robot.Engine.StackLogLine += lines.Add;
        Assert.True(handler.Init(4, 99, false, false)); Assert.Equal(0, handler.State); Assert.True(handler.InputsReady);
        Assert.Contains(lines, l => l.Contains("M5-063 M7-026 GetSimpleMood"));
        handler.Push(new((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D), "test");
        Assert.True(handler.Init(4, 99, false, false)); handler.PlayDrivingLoopAnim();
        Assert.NotEqual(0u, handler.LoopId); Assert.Equal(1, robot.Engine.Robot!.ActionList.QueueCount);
        Assert.Contains(lines, l => l.Contains("M8-021/M8-005 concrete TriggerLiftSafeAnimationAction"));
        robot.Engine.Robot.ActionList.Update(); // No map/library: terminal NO_ANIM_NAME still emits E2G 5A.
        robot.Engine.Robot.ActionList.Update();
        Assert.DoesNotContain(lines, l => l.Contains("M12-032 PathComponent.HasPathToFollow")); // P30 now reads the real constructor's Ready status.
        Assert.Equal(0, robot.Engine.Robot!.ActionList.QueueCount);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void DrivingTrackOwnerUsesSignedToStringOfTagBits()
    {
        using var robot = CozmoRobot.CreateOffline(); var handler = robot.Animations.DrivingAnimations;
        handler.Push(new((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D), "test");
        Assert.True(handler.Init(4, 0x80000001, false, true));
        uint id = 500; handler.CreateActionForTest = _ => new FakeAction { Tag = ++id, OriginalTag = id };
        handler.PlayDrivingEndAnim(); handler.HandleActionCompleted(501, 7);
        Assert.True(robot.Motion.AreAllTracksLockedBy(4, "-2147483647"));
        Assert.False(robot.Motion.AreAllTracksLockedBy(4, "2147483649"));
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ActionListCompletionReachesTheEngineToGameSignal()
    {
        using var robot = CozmoRobot.CreateOffline(); RobotCompletedAction? completed = null;
        using var subscription = robot.Engine.AnimationMessages.SubscribeMessage(0x5A, true, p => completed = (RobotCompletedAction?)p);
        robot.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.InParallel, new TerminalRunner(601));
        robot.Engine.Tick();
        Assert.NotNull(completed); Assert.Equal(601u, completed.Value.Tag); Assert.Equal(0u, completed.Value.Result);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void CompletedLoopQueuesAndUpdatesItsEndDuringTheSameActionListTick()
    {
        using var robot = CozmoRobot.CreateOffline(); var handler = robot.Animations.DrivingAnimations;
        handler.Push(new((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D), "test");
        Assert.True(handler.Init(4, 99, true, false));
        handler.HasPathToFollowForTest = () => false;
        var loop = new TerminalRunner(701);
        var end = new TerminalRunner(702) { UpdateResult = EngineActionResult.Running };
        handler.CreateActionForTest = r => r.Trigger == (AnimationTrigger)0x91 ? loop : end;
        var faults = new List<Exception>(); robot.Engine.Faulted += faults.Add;
        handler.PlayDrivingLoopAnim(); robot.Engine.Tick();
        Assert.Empty(faults); Assert.Equal(3, handler.State);
        Assert.Equal(701u, handler.LoopId); Assert.Equal(702u, handler.EndId);
        Assert.Equal(1, loop.UpdateCalls); Assert.Equal(1, end.UpdateCalls); // native successor is taken after Queue.Update.
    }

    private sealed class TerminalRunner : ActionRunner
    {
        public TerminalRunner(uint tag) : base(7, 0) { Assert.True(SetTag(tag)); }
        public uint UpdateResult { get; set; }
        public int UpdateCalls;
        public override uint Update() { ++UpdateCalls; return State = UpdateResult; }
        public override bool CanInterrupt() => true;
        public override uint CheckIfDone() => 0;
    }
    private sealed class FakeAction : IActionRunner
    {
        public uint State { get; set; } = EngineActionResult.NotStarted;
        public byte RetriesRemain { get; set; } public uint OriginalTag { get; set; } public uint Tag { get; set; }
        public int Type { get; set; } public uint RequiredTrackMask { get; set; } public bool SuppressTrackLocking { get; set; }
        public uint UpdateResult { get; set; } = EngineActionResult.Running;
        public uint Update() => State = UpdateResult;
        public void Cancel() { if (State != EngineActionResult.NotStarted) State = EngineActionResult.Cancelled; }
        public void Reset(bool unlockTracks) {} public bool CanInterrupt() => true; public void Prep() {} public void WatcherEnding() {} public void UnlockTracks() {}
        public bool SetTag(uint tag) { Tag = tag; return true; }
    }
    private sealed class Port : IEngineTransport
    {
        public bool TimedOut => false; public event Action<ReceiverEvent>? Received { add {} remove {} }
        public void Start() {} public void Connect(System.Net.IPAddress ip, bool simulated) {}
        public void Disconnect(System.Net.IPEndPoint address) {} public void SendData(byte[] data) {}
    }
}
