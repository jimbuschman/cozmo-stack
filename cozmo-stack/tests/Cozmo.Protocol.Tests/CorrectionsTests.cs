using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Regressions for the M9/M10 corrections of 2026-09-19: a Wwise Stop event ends the streaming sound; an
/// active reaction is not scored away; a latched strategy is not consumed while its behaviour is unrunnable;
/// the frustration cooldown lives on one clock; replaced strategies are disposed and stale resume state is
/// cleared when a reaction is stopped from outside.
/// </summary>
public class CorrectionsTests
{
    // ------------------------------------------------------------------ fakes

    private sealed class NullSink : IAnimationSink
    {
        public int AudioFrames, SilentFrames;
        public void Face(FaceBitmap bitmap) { }
        public void Audio(byte[]? mulawFrame) { if (mulawFrame is null) SilentFrames++; else AudioFrames++; }
        public void Head(sbyte angleDeg, uint durationMs) { }
        public void Lift(byte heightMm, uint durationMs) { }
        public void Body(BodyKeyframe k) { }
        public void AnimationStarted(byte tag) { }
        public void AnimationEnded() { }
        public void BodyStop() { }
        public void Lights(LightsKeyframe k) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }

    /// <summary>A source with one long sound (event 1), a Stop that covers it (event 2) and a Stop that does not (event 3).</summary>
    private sealed class StopAwareSource : IAnimationAudioSource
    {
        public int Renders;
        public short[]? GetPcm(long eventId, float volume)
        {
            if (eventId != 1) return null;
            Renders++;
            var pcm = new short[CozmoAudio.SampleRate * 5];          // five seconds of tone
            for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)(3000 * Math.Sin(i * 0.05));
            return pcm;
        }
        public string? NameOf(long eventId) => eventId switch { 1 => "Play", 2 => "Stop", 3 => "StopOther", _ => null };
        public bool IsStopEvent(long eventId) => eventId is 2 or 3;
        public bool StopAffects(long stopEventId, long playingEventId) => stopEventId == 2;
    }

    private static AnimationClip AudioClip(params (uint At, long Event)[] events) => new()
    {
        Name = "audio-test",
        Keyframes = events.Select(e => (Keyframe)new AudioKeyframe(e.At, new[] { e.Event }, 1f, Array.Empty<float>(), false)).OrderBy(k => k.TriggerTimeMs).ToList(),
        Tracks = AnimationTrack.Audio,
        DurationMs = 2000,
    };

    private sealed class FakeStrategy : IReactionTriggerStrategy, IDisposable
    {
        public FakeStrategy(ReactionTrigger t) => Trigger = t;
        public ReactionTrigger Trigger { get; }
        public string Basis => "test";
        public bool Fire { get; set; }
        public int Disposed;
        public bool ShouldResumeLast { get; set; }
        public bool CanInterruptOtherTriggeredBehavior => true;
        public bool CanInterruptSelf => false;
        public BehaviorManager? Manager { get; set; }
        public bool ShouldTriggerBehavior(ReactionContext rc, IBehavior behavior) => Fire;
        public void EnabledStateChanged(BehaviorContext context, bool enabled) { }
        public void Dispose() => Disposed++;
    }

    private sealed class Behavior : IBehavior
    {
        public Behavior(string id, double score = 1) { Id = id; Score = score; }
        public string Id { get; }
        public string Class => "test";
        public double Score;
        public bool Runnable = true;
        public int Starts, Stops;
        public bool Running;
        public BehaviorStopReason? LastStop;
        public bool IsRunnable(BehaviorContext c) => Runnable;
        public double EvaluateScore(BehaviorContext c) => Score;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) { Starts++; Running = true; return Task.CompletedTask; }
        public bool Update(BehaviorContext c, double nowMs) => Running;
        public void Stop(BehaviorStopReason r) { Stops++; Running = false; LastStop = r; }
    }

    private static BehaviorContext Context(CozmoRobot robot, MoodState? mood = null) =>
        new() { Robot = robot, Triggers = new AnimationTriggerMap(), Mood = mood };

    // ------------------------------------------------------------------ Wwise Stop

    [Fact]
    public void AStopEventEndsTheStreamingSoundAndAnUnrelatedStopDoesNot()
    {
        var sink = new NullSink();
        var s = new AnimationScheduler(sink) { AudioSource = new StopAwareSource() };
        // the sound starts at 0 and its Stop fires at 200 ms
        s.Play(AudioClip((0, 1), (200, 2)), 0);
        s.Advance(0);
        Assert.True(s.AudioStreaming);
        for (double t = 33; t < 200; t += 33) s.Advance(t);
        Assert.True(s.AudioStreaming);
        Assert.True(sink.AudioFrames >= 5);
        for (double t = 200; t < 300; t += 33) s.Advance(t);
        Assert.False(s.AudioStreaming);
        Assert.Equal(1, s.AudioStops);
        int framesAtStop = sink.AudioFrames;
        for (double t = 300; t < 600; t += 33) s.Advance(t);
        Assert.Equal(framesAtStop, sink.AudioFrames);        // silence from the stop on

        // a Stop whose target does not cover the playing sound leaves it alone
        s.Stop();
        s.Play(AudioClip((0, 1), (200, 3)), 1000);
        for (double t = 1000; t < 1400; t += 33) s.Advance(t);
        Assert.True(s.AudioStreaming);
        Assert.Equal(0, s.AudioStops);
    }

    [Fact]
    public void AStopEventWithNothingPlayingIsNotASilentAlternative()
    {
        var sink = new NullSink();
        var s = new AnimationScheduler(sink) { AudioSource = new StopAwareSource() };
        // keyframe with the Stop listed first and the sound second: the Stop is honoured and the sound is not started as a fallback
        var k = new AudioKeyframe(0, new long[] { 2, 1 }, 1f, new[] { 1f, 0f }, true);
        s.Play(new AnimationClip { Name = "k", Keyframes = new Keyframe[] { k }, Tracks = AnimationTrack.Audio, DurationMs = 500 }, 0);
        s.Advance(0);
        Assert.False(s.AudioStreaming);
        Assert.Equal(0, s.AudioStops);
    }

    // ------------------------------------------------------------------ manager

    [Fact]
    public void AnActiveReactionIsNotReplacedByScoring()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Context(robot);
        using var manager = new BehaviorManager(ctx);
        var idle = new Behavior("Idle", 1);
        var eager = new Behavior("Eager", 100);
        manager.Add(idle);
        manager.Add(eager);
        var strategy = new FakeStrategy(ReactionTrigger.RobotOnBack) { Fire = true };
        var reaction = new Behavior("ReactToRobotOnBack");
        manager.AddReaction(strategy, reaction);

        // the scored behaviour runs, the reaction preempts it
        Assert.Equal("Eager", manager.ChooseAndSwitch(0).Chosen);
        Assert.NotNull(manager.CheckReactions(1));
        Assert.Same(reaction, manager.Current);
        strategy.Fire = false;

        // ordinary scoring while the reaction is active: the reaction stays current
        var held = manager.ChooseAndSwitch(2);
        Assert.Equal("ReactToRobotOnBack", held.Chosen);
        Assert.Contains("scoring waits", held.Reason);
        Assert.Same(reaction, manager.Current);
        Assert.Equal(ReactionTrigger.RobotOnBack, manager.CurrentReactionTrigger);
        Assert.Equal(1, eager.Stops);
        Assert.Equal(0, reaction.Stops);

        // once it finishes, scoring resumes
        reaction.Running = false;
        manager.Update(3000, 3);
        Assert.Null(manager.Current);
        Assert.Equal("Eager", manager.ChooseAndSwitch(4).Chosen);
    }

    /// <summary>
    /// M10-003, inventory row C14: Generic's WantsToRun is called only when the behaviour is runnable, so the latch
    /// survives while it is not (0x60F3BC..0x60F454).
    /// </summary>
    [Fact]
    public void ALatchedStrategyIsNotConsumedWhileItsBehaviourIsUnrunnable()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Context(robot);
        using var manager = new BehaviorManager(ctx);
        var latch = new GenericReactionStrategy(ReactionTrigger.CliffDetected, "test latch", () => 0, new[] { 34 });
        var reaction = new Behavior("ReactToCliff") { Runnable = false };
        manager.AddReaction(latch, reaction);

        latch.AlwaysHandle(34, null);
        Assert.Null(manager.CheckReactions(0));
        Assert.True(latch.Latched);                // C14: not runnable, so WantsToRun was not called

        reaction.Runnable = true;
        var sw = manager.CheckReactions(1);
        Assert.NotNull(sw);
        Assert.Equal("ReactToCliff", sw!.Behavior);
        Assert.False(latch.Latched);               // C13: cleared by the call
    }

    [Fact]
    public void MinorFrustrationBecomesEligibleAgainAfterSixtySecondsOnOneClock()
    {
        var obb = Obb();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var model = MoodModel.Load(obb);
        var mood = new MoodState(model);
        var down = model.Events.First(e => e.Affectors.Any(a => a.Emotion == EmotionType.Confident && a.Value < 0));
        for (int i = 0; i < 500 && mood[EmotionType.Confident] >= -0.6; i++) mood.Trigger(down.Name, 0);
        Assert.True(mood[EmotionType.Confident] < -0.6);
        var ctx = Context(robot, mood);
        using var manager = new BehaviorManager(ctx);

        double clock = 0;                          // the one time base: strategy stamp and manager evaluation
        var strategy = new FrustrationStrategy(-0.6f, 60f, () => clock);
        var reaction = new Behavior("ReactToFrustrationMinor");
        manager.AddReaction(strategy, reaction);

        Assert.NotNull(manager.CheckReactions(clock));
        Assert.Same(reaction, manager.Current);
        // the animation completes 5 s in (what ReactToFrustrationBehavior does when its clip ends)
        clock = 5;
        strategy.AnimationComplete();
        reaction.Running = false;
        manager.Update(clock * 1000, clock);
        Assert.Null(manager.Current);

        clock = 30;
        Assert.Null(manager.CheckReactions(clock));           // cooling down
        clock = 64;
        Assert.Null(manager.CheckReactions(clock));           // 59 s since completion
        clock = 65.5;
        Assert.NotNull(manager.CheckReactions(clock));        // > 60 s: eligible again
        Assert.Equal(2, reaction.Starts);
    }

    /// <summary>
    /// M10-004, inventory row C4: each trigger holds a (strategy, behaviour) vector in JSON order (AddStrategyMapping
    /// 0x5A1864), so a second entry for a trigger is appended, not a replacement. The manager owns the strategies and
    /// releases each one once.
    /// </summary>
    [Fact]
    public void ReplacedAndOwnedStrategiesAreDisposed()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Context(robot);
        var manager = new BehaviorManager(ctx);
        var first = new FakeStrategy(ReactionTrigger.RobotOnBack);
        var second = new FakeStrategy(ReactionTrigger.RobotOnBack);
        var other = new FakeStrategy(ReactionTrigger.RobotOnFace);
        manager.AddReaction(first, new Behavior("ReactToRobotOnBack"));
        manager.AddReaction(second, new Behavior("ReactToRobotOnBack"));
        Assert.Equal(0, first.Disposed);
        manager.AddReaction(other, new Behavior("ReactToRobotOnFace"));
        Assert.Equal(new IReactionTriggerStrategy[] { first, second, other }, manager.Reactions.Select(r => r.Strategy));
        manager.Dispose();
        Assert.Equal(1, first.Disposed);
        Assert.Equal(1, second.Disposed);
        Assert.Equal(1, other.Disposed);
        Assert.Empty(manager.Reactions);
    }

    [Fact]
    public void StaleResumeStateIsClearedWhenAReactionIsStoppedFromOutside()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Context(robot);
        using var manager = new BehaviorManager(ctx);
        var idle = new Behavior("Idle", 1);
        manager.Add(idle);
        var strategy = new FakeStrategy(ReactionTrigger.UnexpectedMovement) { Fire = true, ShouldResumeLast = true };
        var reaction = new Behavior("ReactToUnexpectedMovement");
        manager.AddReaction(strategy, reaction);

        manager.ChooseAndSwitch(0);
        var sw = manager.CheckReactions(1);
        Assert.True(sw!.WillResume);
        strategy.Fire = false;

        // the reaction is stopped from outside (the arbiter, a caller): nothing is parked for resumption
        manager.Stop(BehaviorStopReason.Cancelled, 2);
        Assert.Null(manager.Current);
        Assert.Equal(1, idle.Starts);

        // a later behaviour completing must not resurrect Idle
        var other = new Behavior("Other", 5);
        manager.Add(other);
        Assert.Equal("Other", manager.ChooseAndSwitch(3).Chosen);
        other.Running = false;
        manager.Update(4000, 4);
        Assert.Null(manager.Current);
        Assert.Equal(1, idle.Starts);

        // and a reaction interrupted by a caller's StartAsync drops it too
        manager.ChooseAndSwitch(5);
        Assert.Same(idle, manager.Current);
        strategy.Fire = true;
        Assert.NotNull(manager.CheckReactions(6));
        strategy.Fire = false;
        Assert.True(manager.StartAsync("Other", 7).GetAwaiter().GetResult());
        other.Running = false;
        manager.Update(8000, 8);
        Assert.Null(manager.Current);
        Assert.Equal(2, idle.Starts);
    }

    private static string? Obb()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "config"))) return r;
            d = d.Parent;
        }
        return null;
    }
}
