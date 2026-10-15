using System.Collections.Concurrent;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class M5X13BuilderTests : IDisposable
{
    private readonly bool _error = EngineErrorState.ErrorFlagSet;
    public void Dispose() => EngineErrorState.ErrorFlagSet = _error;

    [Fact]
    public void ShutdownUnlinksRobotsThenAbortsAndJoinsLoaderBeforeHub()
    {
        string root = Directory.CreateTempSubdirectory("m513-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "animationGroups"));
        File.WriteAllText(Path.Combine(root, "animationGroups", "g.json"), "{\"Animations\":[{\"Name\":\"a\",\"Weight\":1}]}");
        using var collecting = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var order = new ConcurrentQueue<string>();
        using var robot = CozmoRobot.CreateForTest(new Port(), () => 0,
            beforeAnimationFileStat: _ => { collecting.Set(); Assert.True(resume.Wait(TimeSpan.FromSeconds(15))); });
        robot.Engine.Robots.AddRobot(1, withRic: false);
        var hub = robot.Engine.AnimationMessages;
        Assert.Equal(1, hub.Count(0x5A, true));
        Assert.Equal(1, hub.Count(0xB4, false));
        hub.ReleaseObserved = line => { if (line.StartsWith("unlink")) order.Enqueue(line); };
        robot.Engine.DestructionObserved = phase =>
        {
            if (phase == "robots") Assert.Equal(0, hub.Count(0x5A, true));
            order.Enqueue(phase);
        };
        robot.Animations.LoaderDestructionObserved = phase => { order.Enqueue(phase); if (phase == "abort") resume.Set(); };
        var load = Task.Run(() => robot.Animations.LoadFrom(root));
        try
        {
            Assert.True(collecting.Wait(TimeSpan.FromSeconds(15)));
            robot.Dispose();
            Assert.True(load.Wait(TimeSpan.FromSeconds(15)));
            Assert.Empty(load.Result.GroupNames); // +0x278 gate prevents group definition.
            // E07/E08/E09/E10/E11; subscription vectors release from the back.
            // NV +0x260 is released at 0x005112E4; its backup destructor (0x00643F48 -> 0x0051A5F0)
            // retires the constructor's 6B/6D/6C handle vector from the back before the streamer owners.
            Assert.Equal(new[] { "unlink G2E F7", "unlink G2E E0", "unlink G2E DF", "unlink G2E DE", "unlink G2E DD", "unlink G2E DC", "unlink G2E DB", "unlink G2E 6C", "unlink G2E 6D", "unlink G2E 6B", "unlink G2E B6", "unlink G2E B5", "unlink E2G 5A",
                "unlink G2E C4", "unlink G2E AD", "unlink G2E AF", "unlink G2E AE",
                "unlink G2E B4", "robots", "abort", "join", "loader", "hub" }, order.Where(line => line is not ("unlink G2E 95" or "unlink E2G 35")));
            // M7-028: managed subscription storage is retired; the native IBehavior destruction order remains unread.
            Assert.Equal(2, order.Count(line => line == "unlink G2E 95"));
            Assert.Equal(2, order.Count(line => line == "unlink E2G 35"));
            Assert.Throws<ObjectDisposedException>(() => hub.Subscribe(0x5A, true));
        }
        finally { resume.Set(); load.Wait(TimeSpan.FromSeconds(15)); Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ShutdownJoinsTickerWithoutHoldingRobotMembershipLock()
    {
        using var robot = CozmoRobot.CreateOffline();
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        robot.Animations.TickObserved += () =>
        {
            entered.Set();
            Assert.True(resume.Wait(TimeSpan.FromSeconds(15)));
            _ = robot.Engine.Robot; // ticker's live membership read must not be blocked by teardown.
        };
        robot.Engine.Robot!.Lifetime.Bind(-3, _ => resume.Set());
        robot.Animations.Play(new AnimationClip { Name = "running", Keyframes = new Keyframe[] { new HeadKeyframe(0, 100, 0, 0) } });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(15)));
        await Task.Run(robot.Dispose).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Null(robot.Engine.Robot);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SubscriptionLastReleaseDropsTokenBeforeIdentityUnlinkAndExpiredReleaseDoesNothing(bool e2g)
    {
        using var hub = new UiSubscriptionHub();
        int calls = 0;
        var handle = hub.Subscribe(0x5A, e2g, () => calls++);
        handle.Retain();
        var order = new List<string>();
        hub.ReleaseObserved = phase =>
        {
            order.Add(phase);
            if (phase == "token released") Assert.Equal(1, hub.Count(0x5A, e2g));
        };
        handle.Dispose();
        hub.Deliver(0x5A, e2g); Assert.Equal(1, calls); Assert.Empty(order);
        handle.Dispose(); handle.Dispose();
        Assert.Equal(new[] { "token released", $"unlink {(e2g ? "E2G" : "G2E")} 5A" }, order);
        hub.Deliver(0x5A, e2g); Assert.Equal(1, calls);
        var expired = hub.Subscribe(0x5A, e2g, () => calls++);
        hub.Dispose(); order.Clear(); expired.Dispose(); Assert.Empty(order);
    }

    [Fact]
    public void CustomSongIsRejectedBeforeLookupLocksOrReplacingExistingPlay()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Animations.ManualTicking = true;
        var stack = new List<string>(); var engine = new List<string>();
        robot.Engine.StackLogLine += stack.Add;
        robot.Engine.LogEntry += e => engine.Add(e.Name ?? "");
        var good = new AnimationClip { Name = "good", Keyframes = new Keyframe[] { new HeadKeyframe(0, 100, 0, 0) } };
        Assert.NotNull(robot.Animations.Play(good));
        long generation = robot.Animations.Scheduler.Generation;
        stack.Clear(); engine.Clear();
        Assert.Null(robot.Animations.Play("cozmo_sings_custom")); // even with no library loaded.
        Assert.Null(robot.Animations.PlayTracked("cozmo_sings_custom", lockTracks: AnimationTrack.Head));
        var custom = new AnimationClip { Name = "cozmo_sings_custom", Keyframes = good.Keyframes };
        Assert.Null(robot.Animations.Play(custom));
        Assert.Null(robot.Animations.PlayTracked(custom, lockTracks: AnimationTrack.Head));
        Assert.Null(robot.Animations.Scheduler.Play(custom, 0));
        Assert.Equal(generation, robot.Animations.Scheduler.Generation);
        Assert.Equal("good", robot.Animations.Playing);
        Assert.False(robot.Motion.AreAnyTracksLocked(CozmoMotion.MaskFor(AnimationTrack.Head)));
        Assert.Equal(5, stack.Count); Assert.All(stack, s => Assert.StartsWith("MISSING: M9-029 cozmo_sings_custom", s));
        Assert.Empty(engine);
        Assert.DoesNotContain(typeof(CozmoAnimations).GetMethods(), m => m.Name.Contains("ReplaceNotesInSong"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingNeutralSkipsResetDataWriteAndRetainsPriorReset(bool emptyGroup)
    {
        var scheduler = new AnimationScheduler(new Sink()) { Catalog = new MissingNeutral(emptyGroup) };
        var prior = ProceduralFacePose.ShippedNeutral();
        prior.Left[0] = 17f;
        scheduler.Layers.SetResetData(prior);
        scheduler.LoadNeutralFace();
        // 0x0057A1FA jumps to miss log, bypassing SetResetData at 0x0057A20E;
        // TLC Init then copies the retained reset data, not the default face.
        Assert.Equal(17f, scheduler.LayerBaseFace.Left[0]);
        Assert.Null(scheduler.NeutralFaceAnimation);
    }

    private sealed class MissingNeutral(bool empty) : IAnimationCatalog
    {
        public bool HasAnimationForTrigger(AnimationTrigger t) => true;
        public string GetAnimationForTrigger(AnimationTrigger t) => "g";
        public (string Name, int Count) GetFirstAnimationName(string g) => empty ? ("", 0) : ("absent", 1);
        public string GetAnimationNameFromGroup(string g, bool strict) => "absent";
        public AnimationClip? GetAnimation(string n) => null;
    }
    private sealed class Port : IEngineTransport
    {
        public bool TimedOut => false;
        public event Action<ReceiverEvent>? Received { add { } remove { } }
        public void Start() { } public void Connect(System.Net.IPAddress ip, bool simulated) { }
        public void Disconnect(System.Net.IPEndPoint address) { } public void SendData(byte[] data) { }
    }
    private sealed class Sink : IAnimationSink
    {
        public void Face(FaceBitmap b) { } public void Audio(byte[]? b) { } public void Head(sbyte a, uint d) { }
        public void Lift(byte h, uint d) { } public void Body(BodyKeyframe b) { } public void BodyStop() { }
        public void Lights(LightsKeyframe b) { } public void Event(string s) { } public void AnimationStarted(byte b) { }
        public void AnimationEnded() { } public void Finished(string s, bool b) { }
    }
}
