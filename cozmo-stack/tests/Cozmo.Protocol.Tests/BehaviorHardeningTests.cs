using System.Diagnostics;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Regressions for the behaviour-layer defects found by review. Each names the hole it closes.
/// </summary>
public class BehaviorHardeningTests
{
    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "assets", "animationGroups")))
                return r;
            d = d.Parent;
        }
        return null;
    }

    private static string Assets(string obb) =>
        Path.Combine(obb, "assets", "cozmo_resources", "assets");

    // ================================================================ caller outranks reaction

    /// <summary>
    /// The arbiter claimed caller beats reaction, but only ever saw requests made through itself. An
    /// application calling robot.Animations.Play — the ordinary public path — was invisible to it, so a
    /// reaction happily replaced the caller's animation.
    ///
    /// This starts an animation the application-facing way, then raises a reaction, and proves the
    /// reaction cannot take over.
    /// </summary>
    [Fact]
    public void AReactionCannotReplaceAnAnimationTheApplicationStartedDirectly()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var lib = robot.Animations.LoadFrom(Assets(obb));

        // The application-facing path: no arbiter involved.
        var longClip = lib.ClipNames.Select(lib.GetClip)
            .OrderByDescending(c => c.DurationMs).First();
        robot.Animations.Play(longClip.Name);
        Assert.True(robot.Animations.IsPlaying);
        var callersAnimation = robot.Animations.Playing;

        using var reactive = new ReactiveBehavior(robot, AnimationTriggerMap.Load(obb)) { Asynchronous = false };
        reactive.Arbiter.AutonomyEnabled = true;

        var decision = reactive.Fire(ReactionTrigger.CliffDetected);

        Assert.False(decision.Started);
        Assert.Equal(BehaviorOutcome.Suppressed, decision.Outcome);
        Assert.Contains("caller", decision.Reason);
        Assert.Equal(callersAnimation, robot.Animations.Playing);
        robot.Animations.Stop();
    }

    // ================================================================ the reaction lock

    /// <summary>
    /// SmartDisableReactionsWithLock was modelled but never connected: a behaviour could take the lock and
    /// reactions carried on regardless. Taking it must suppress a reaction, and releasing the scope must
    /// restore them.
    /// </summary>
    [Fact]
    public void ABehaviourHoldingTheReactionLockSuppressesReactionsUntilItsScopeIsReleased()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        robot.Animations.LoadFrom(Assets(obb));

        var arbiter = new BehaviorArbiter { AutonomyEnabled = true, ReactionCooldown = TimeSpan.Zero };
        using var reactive = new ReactiveBehavior(robot, AnimationTriggerMap.Load(obb), arbiter: arbiter)
        { Asynchronous = false };

        var scope = new BehaviorScope(arbiter);
        scope.DisableReactions();
        Assert.True(arbiter.ReactionsDisabled);

        var blocked = reactive.Fire(ReactionTrigger.CliffDetected);
        Assert.False(blocked.Started);
        Assert.Equal(BehaviorOutcome.Suppressed, blocked.Outcome);
        Assert.Contains("reaction lock", blocked.Reason);

        scope.Dispose();
        Assert.False(arbiter.ReactionsDisabled);

        var allowed = reactive.Fire(ReactionTrigger.CliffDetected);
        Assert.True(allowed.Started, allowed.Reason);
        robot.Animations.Stop();
    }

    // ================================================================ behaviours release their animation

    /// <summary>
    /// Stopping a behaviour must end the animation it started; it used to just drop the task and leave the
    /// robot animating.
    /// </summary>
    [Fact]
    public void StoppingABehaviourStopsTheAnimationItStarted()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        robot.Animations.LoadFrom(Assets(obb));
        var ctx = new BehaviorContext
        {
            Robot = robot,
            Triggers = AnimationTriggerMap.Load(obb),
            Random = new Random(4),
        };

        var b = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup });
        using var scope = new BehaviorScope();
        b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.True(robot.Animations.IsPlaying);

        b.Stop(BehaviorStopReason.Cancelled);
        Assert.False(robot.Animations.IsPlaying);
    }

    /// <summary>
    /// The other half: stopping a behaviour must not cancel an unrelated animation that replaced its own.
    /// Ownership is by token, so once something else has taken over the behaviour's stop is a no-op.
    /// </summary>
    [Fact]
    public void StoppingABehaviourDoesNotCancelAnAnimationThatReplacedIts()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var lib = robot.Animations.LoadFrom(Assets(obb));
        var ctx = new BehaviorContext
        {
            Robot = robot,
            Triggers = AnimationTriggerMap.Load(obb),
            Random = new Random(4),
        };

        var b = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup });
        using var scope = new BehaviorScope();
        b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.True(robot.Animations.IsPlaying);

        // Something else takes over, the way a caller animation would.
        var other = lib.ClipNames.First(n => n != b.LastSelected);
        robot.Animations.Play(other);
        var nowPlaying = robot.Animations.Playing;

        b.Stop(BehaviorStopReason.Cancelled);

        Assert.True(robot.Animations.IsPlaying, "the replacement animation was cancelled by an unrelated behaviour");
        Assert.Equal(nowPlaying, robot.Animations.Playing);
        robot.Animations.Stop();
    }

    // ================================================================ idle: face vs motors

    /// <summary>
    /// With every track locked (AreAnyTracksLocked, 0x0057D626..) the streamer UpdateLiveAnimation (0x0057D5F8) generates no head,
    /// lift or body keyframe, however long it runs. The face keep-alive (FaceLayerManager::KeepFaceAlive 0x0058D374) is not gated by
    /// the locks, but its block needs +0x88 &gt; 0 (0x0057CF6A..0x0057CF76) - something must have streamed - and an empty live
    /// animation never streams (it is ended from its InitStream), so a fully locked idle has no face keep-alive either; once a
    /// frame has streamed (here a keyframe appended through the StreamLive seam) it blinks and darts, and the locks still hold the
    /// motors back.
    /// </summary>
    [Fact]
    public void LockedTracksGenerateNoMotorKeyframesAndTheFaceKeepAliveNeedsAStreamedFrame()
    {
        var sink = new IdleSink();
        var s = new Cozmo.Robot.Animation.AnimationScheduler(sink, new Random(3));
        s.PushLiveQuietly();                      // ProceduralLive on top, all tracks locked
        for (int i = 0; i < 100; i++) s.Advance(60.0 * i);
        Assert.Equal(0, sink.Moves);
        Assert.Empty(s.FaceLayerNames);           // nothing streamed: gate A of the keep-alive block (+0x88 is still -FLT_MAX)

        Assert.True(s.StreamLive(new Cozmo.Robot.Animation.HeadKeyframe(0, 100, 0, 0), 6000));
        bool blink = false, dart = false;
        for (int i = 100; i < 500; i++)
        {
            s.Advance(60.0 * i);
            blink |= s.FaceLayerNames.Contains("Blink");
            dart |= s.FaceLayerNames.Contains("KeepAliveEyeDart");
        }
        Assert.True(blink && dart);
        Assert.Equal(1, sink.Moves);              // only the appended keyframe: the locked tracks generated nothing
    }

    private sealed class IdleSink : Cozmo.Robot.Animation.IAnimationSink
    {
        public int Moves;
        public void Face(Cozmo.Robot.FaceBitmap bitmap) { }
        public void Audio(byte[]? mulawFrame) { }
        public void Head(sbyte angleDeg, uint durationMs) => Moves++;
        public void Lift(byte heightMm, uint durationMs) => Moves++;
        public void Body(Cozmo.Robot.Animation.BodyKeyframe k) => Moves++;
        public void AnimationStarted(byte tag) { }
        public void AnimationEnded() { }
        public void BodyStop() { }
        public void Lights(Cozmo.Robot.Animation.LightsKeyframe k) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }

    // ================================================================ falling

    /// <summary>
    /// ReactionTable mapped RobotFalling but nothing ever watched for it, so the claim that falling was
    /// dispatched was untrue. The transition is now derived from IsFalling like pick-up and charger.
    /// </summary>
    [Fact]
    public void AFallingTransitionIsDerivedAndRaised()
    {
        using var rig = new SensorRig();
        var seen = new List<bool>();
        rig.Robot.Sensors.FallingChanged += seen.Add;

        rig.Send(new RobotState { Status = 0 });                                  // baseline
        rig.Send(new RobotState { Status = (uint)RobotStatusFlag.IsFalling });     // starts falling
        rig.Send(new RobotState { Status = (uint)RobotStatusFlag.IsFalling });     // still falling, no event
        rig.Send(new RobotState { Status = 0 });                                   // landed

        Assert.Equal(new[] { true, false }, seen);
    }

    // ================================================================ behaviour threading

    /// <summary>
    /// Sensor callbacks arrive on the transport's dispatch thread, which also carries robot state. Running
    /// animation selection there stalls telemetry for everything else. Reaction work is queued onto the
    /// behaviour layer's own thread, so a slow reaction consumer must not hold up later robot state.
    /// </summary>
    [Fact]
    public void ASlowReactionDoesNotBlockSubsequentRobotStateProcessing()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new SensorRig();
        rig.Robot.Animations.LoadFrom(Assets(obb));

        using var reactive = new ReactiveBehavior(rig.Robot, AnimationTriggerMap.Load(obb));
        reactive.Arbiter.AutonomyEnabled = true;

        var slowStarted = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        reactive.Reacted += _ =>
        {
            slowStarted.Set();
            release.Wait(TimeSpan.FromSeconds(5));       // a deliberately slow consumer
        };
        reactive.Start();

        rig.Send(new RobotState { Status = 0 });
        rig.Send(new RobotState { Status = (uint)RobotStatusFlag.IsPickedUp });   // raises a reaction
        Assert.True(slowStarted.Wait(TimeSpan.FromSeconds(5)), "the reaction never ran");

        // While the reaction is still stuck, more robot state must keep being processed.
        int before = rig.Robot.State.StateCount;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++) rig.Send(new RobotState { Status = (uint)RobotStatusFlag.IsPickedUp });
        sw.Stop();

        Assert.True(rig.Robot.State.StateCount >= before + 20,
            $"state processing stalled: {rig.Robot.State.StateCount - before} of 20 arrived");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2),
            $"state delivery was blocked by the slow reaction for {sw.Elapsed.TotalSeconds:F1}s");

        release.Set();
    }

    /// <summary>Reactions are handled one at a time, in the order the sensors reported them.</summary>
    [Fact]
    public void QueuedReactionsKeepTheirOrder()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        robot.Animations.LoadFrom(Assets(obb));
        using var reactive = new ReactiveBehavior(robot, AnimationTriggerMap.Load(obb))
        { Asynchronous = false };
        reactive.Arbiter.AutonomyEnabled = true;
        reactive.Arbiter.ReactionCooldown = TimeSpan.Zero;

        var order = new List<ReactionTrigger?>();
        reactive.Reacted += d => order.Add(d.Reaction);

        reactive.Fire(ReactionTrigger.CliffDetected);
        robot.Animations.Stop();
        reactive.Fire(ReactionTrigger.RobotPickedUp);
        robot.Animations.Stop();

        Assert.Equal(new ReactionTrigger?[] { ReactionTrigger.CliffDetected, ReactionTrigger.RobotPickedUp },
                     order);
    }

    /// <summary>Feeds an offline robot over the framed transport, as a real one would.</summary>
    private sealed class SensorRig : IDisposable
    {
        public readonly CozmoRobot Robot = CozmoRobot.CreateOffline();
        private ushort _seq = 1;

        public SensorRig() =>
            Deliver(new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), _seq++));

        public void Send(RobotMessage m) =>
            Deliver(new SubMessage(ReliableMessageType.SingleReliableMessage, m.ToBytes(), _seq++));

        private void Deliver(SubMessage sm)
        {
            var f = new Frame
            {
                Type = ReliableMessageType.MultipleMixedMessages,
                SeqMin = sm.Seq, SeqMax = sm.Seq, Ack = 0,
                Messages = new List<SubMessage> { sm },
            };
            Robot.Transport.ProcessIncoming(FrameCodec.Encode(f));
        }

        public void Dispose() => Robot.Dispose();
    }
}
