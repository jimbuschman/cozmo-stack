using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The post-fidelity core review: integration and lifetime behaviour that the per-subsystem tests missed
/// because they drove the pieces directly instead of through the path production uses. Every test here
/// exercises the production path - the real robot object, the real tick loops, the real handlers - and
/// nothing drives a scheduler or a component by hand unless production does.
/// </summary>
public class CoreReviewTests
{
    /// <summary>Waits for a condition, polling, so a real background thread has time to do its work.</summary>
    private static bool Within(int ms, Func<bool> cond)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            if (cond()) return true;
            Thread.Sleep(5);
        }
        return cond();
    }

    /// <summary>
    /// Every CLAD message the robot has sent, decoded. The connection batches rather than sending as it
    /// goes, so it is ticked first - a live transport does that on its own thread.
    /// </summary>
    private static List<RobotMessage> Outbound(CozmoRobot robot)
    {
        try { robot.Transport.OfflineTick(); } catch { }     // nothing to tick once disconnected
        var seen = new HashSet<ushort>();
        var outp = new List<RobotMessage>();
        foreach (var sm in robot.Transport.OfflineOutbound.SelectMany(f => f.Messages))
        {
            if (sm.Type is not (ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage)
                || sm.Payload.Length == 0) continue;
            if (sm.Seq != 0 && !seen.Add(sm.Seq)) continue;
            outp.Add(RobotMessage.Parse(sm.Payload));
        }
        return outp;
    }

    // ================================================================ CORE-001

    /// <summary>
    /// CORE-001. A keep-alive body shuffle has to stop at its duration when idle is the only thing
    /// running.
    ///
    /// The scheduler has always known when to stop it - <c>Advance</c> serves the deadline whether or not
    /// a clip is playing - but nothing in production was calling <c>Advance</c>: the animation tick loop
    /// ran only while a clip was playing, and a live keyframe is not a clip. Idle would stream
    /// <c>animBodyMotion</c> with a speed and a duration and then never send the stop, so the wheels ran
    /// until something else countermanded them.
    ///
    /// This drives idle exactly as an application does - <c>idle.Advance(now)</c> on the wall clock, with
    /// <c>Execute</c> and <c>ExecuteMotors</c> on - and nothing here touches the scheduler.
    /// </summary>
    [Fact]
    public void CORE001_AnIdleBodyShuffleIsStoppedAtItsDurationWithNobodyDrivingTheScheduler()
    {
        using var robot = CozmoRobot.CreateOffline();
        // one shuffle, short, straight (a known radius, so a stop is owed), and nothing else moving
        var quiet = IdleParameters.Default with
        {
            TimeBeforeWiggleMotionsMs = 0,
            BlinkSpacingMinMs = 600_000,
            BlinkSpacingMaxMs = 600_000,
            EyeDartMaxDistancePix = 0,
            HeadMovementSpacingMinMs = 600_000,
            HeadMovementSpacingMaxMs = 600_000,
            LiftMovementSpacingMinMs = 600_000,
            LiftMovementSpacingMaxMs = 600_000,
            BodyMovementDurationMinMs = 200,
            BodyMovementDurationMaxMs = 200,
            BodyMovementSpacingMinMs = 600_000,
            BodyMovementSpacingMaxMs = 600_000,
            BodyMovementStraightFraction = 1,
        };
        var idle = new IdleBehavior(robot, new BehaviorArbiter { AutonomyEnabled = true }, quiet, new Random(6))
        {
            Execute = true,
            ExecuteMotors = true,
        };

        IdleEvent? shuffle = null;
        idle.Acted += e => { if (e.Action == IdleAction.BodyMove && e.Suppressed is null) shuffle ??= e; };

        // tick idle the way an application would, on the wall clock, until it shuffles with a real speed
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3_000 && (shuffle is null || shuffle.Amount == 0))
        {
            if (shuffle is { Amount: 0 }) shuffle = null;      // a zero-speed draw owes no stop; wait for a real one
            idle.Advance(Environment.TickCount64);
            Thread.Sleep(5);
        }
        Assert.NotNull(shuffle);
        Assert.NotEqual(0, shuffle!.Amount);

        var driving = Outbound(robot).OfType<BodyMotion>().Where(b => b.Speed != 0).ToList();
        Assert.NotEmpty(driving);

        // nothing else is ticking anything: if the stop arrives, the animation system brought it
        Assert.True(Within(2_000, () => Outbound(robot).OfType<BodyMotion>().Any(b => b.Speed == 0)),
                    "the keep-alive body keyframe was never stopped");

        // and it waited for the duration rather than stopping at once
        Assert.True(sw.ElapsedMilliseconds >= 200);
    }

    /// <summary>
    /// CORE-001, the other half: the deadline and the tick loop have to be on one clock. The keyframe's
    /// stop time is recorded against the clock <c>StreamLive</c> is given and served against the clock
    /// <c>Advance</c> is driven on, so an idle tick clock that starts at zero while the animation loop
    /// runs on <c>Environment.TickCount64</c> would stop the wheels on the first tick instead of at the
    /// duration. Going through the animation system rather than straight at the scheduler is what keeps
    /// the two the same.
    /// </summary>
    [Fact]
    public void CORE001_TheLiveKeyframeClockIsTheAnimationSystemsOwn()
    {
        using var robot = CozmoRobot.CreateOffline();
        var quiet = IdleParameters.Default with
        {
            TimeBeforeWiggleMotionsMs = 0,
            BlinkSpacingMinMs = 600_000,
            BlinkSpacingMaxMs = 600_000,
            EyeDartMaxDistancePix = 0,
            HeadMovementSpacingMinMs = 600_000,
            HeadMovementSpacingMaxMs = 600_000,
            LiftMovementSpacingMinMs = 600_000,
            LiftMovementSpacingMaxMs = 600_000,
            BodyMovementDurationMinMs = 400,
            BodyMovementDurationMaxMs = 400,
            BodyMovementSpacingMinMs = 600_000,
            BodyMovementSpacingMaxMs = 600_000,
            BodyMovementStraightFraction = 1,
        };
        var idle = new IdleBehavior(robot, new BehaviorArbiter { AutonomyEnabled = true }, quiet, new Random(6))
        {
            Execute = true,
            ExecuteMotors = true,
        };

        IdleEvent? shuffle = null;
        idle.Acted += e => { if (e.Action == IdleAction.BodyMove && e.Suppressed is null && e.Amount != 0) shuffle ??= e; };

        // an idle clock of its own, starting at zero - what a caller with a stopwatch would pass
        double t = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3_000 && shuffle is null)
        {
            idle.Advance(t);
            t += 20;
            Thread.Sleep(5);
        }
        Assert.NotNull(shuffle);

        // the stop must not be there yet: the keyframe has 400 ms to run
        Assert.False(Within(150, () => Outbound(robot).OfType<BodyMotion>().Any(b => b.Speed == 0)),
                     "the body was stopped immediately, so the deadline was read on the wrong clock");
        Assert.True(Within(1_500, () => Outbound(robot).OfType<BodyMotion>().Any(b => b.Speed == 0)),
                    "the keep-alive body keyframe was never stopped");
    }

    // ================================================================ CORE-002

    /// <summary>
    /// CORE-002. Disconnecting while an animation is playing must end the animation, not the process.
    ///
    /// The tick loop runs on a background thread and every frame it streams reaches the transport. Once
    /// the link is gone <c>SendData</c> throws "not connected", and an exception escaping a background
    /// thread takes the whole process with it - so the failure mode was not a stuck animation but a hard
    /// exit. The loop now treats a robot that went away as the end of the animation: whatever is awaiting
    /// <c>Play</c> completes, the loop stops, and the exception is offered to a <c>Faulted</c> subscriber
    /// rather than thrown at nobody.
    /// </summary>
    [Fact]
    public async Task CORE002_DisconnectingMidAnimationEndsItCleanlyAndLeavesNoTicker()
    {
        using var robot = CozmoRobot.CreateOffline();
        var clip = new AnimationClip
        {
            Name = "long-one",
            Keyframes = new List<Keyframe>
            {
                new BodyKeyframe(0, 10_000, IdleBehavior.StraightToken, 40),
                new AudioKeyframe(0, new long[] { 1 }, 1.0f, new[] { 1.0f }, false),
            },
            Tracks = AnimationTrack.Body | AnimationTrack.Audio,
            DurationMs = 10_000,
        };

        Exception? faulted = null;
        robot.Animations.Faulted += e => faulted = e;

        var playing = robot.Animations.Play(clip);
        Assert.NotNull(playing);
        Assert.True(Within(1_000, () => robot.Animations.IsTicking), "the animation never started ticking");

        // the robot goes away underneath it, exactly as a dropped link does
        robot.Transport.Disconnect("test");

        var finished = await Task.WhenAny(playing!, Task.Delay(3_000));
        Assert.Same(playing, finished);                       // it ended rather than hanging
        Assert.True(Within(2_000, () => !robot.Animations.IsTicking), "the ticker was still running");
        Assert.False(robot.Animations.IsPlaying);
        Assert.NotNull(faulted);                              // and the reason was reported, not swallowed silently

        // the process is still here to make these assertions, which is the other half of the claim
        Assert.True(true);
    }

    // ================================================================ CORE-003

    /// <summary>
    /// A sink that blocks inside one keyframe emission, so a test can be in the middle of a send while it
    /// does something else to the animation. Everything it is told is recorded in order.
    /// </summary>
    private sealed class BlockingSink : IAnimationSink
    {
        public readonly List<string> Log = new();
        public readonly ManualResetEventSlim Entered = new(false);
        public readonly ManualResetEventSlim Release = new(false);
        public string BlockOn = "";

        private void Note(string what)
        {
            // the block comes first and the record second, so the log says when the command actually
            // went out rather than when it was decided on
            if (what == BlockOn)
            {
                Entered.Set();
                Release.Wait(5_000);
            }
            lock (Log) Log.Add(what);
        }

        public void Face(FaceBitmap bitmap) { }
        public void Audio(byte[]? mulawFrame) { }
        public void Head(sbyte angleDeg, uint durationMs) => Note("head");
        public void Lift(byte heightMm, uint durationMs) => Note("lift");
        public void AnimationStarted(byte tag) => Note("started");
        public void AnimationEnded() => Note("ended");
        public void Body(BodyKeyframe keyframe) => Note("body");
        public void BodyStop() => Note("bodystop");
        public void Lights(LightsKeyframe keyframe) { }
        public void Event(string eventId) => Note("event:" + eventId);
        public void Finished(string clipName, bool completed) => Note("finished");

        public List<string> Snapshot() { lock (Log) return Log.ToList(); }
    }

    /// <summary>
    /// CORE-003. A keyframe and the ownership that entitles it to go out are one step.
    ///
    /// The old arrangement checked the generation under the lock, released it, and then emitted - so a
    /// cancellation landing in between let a command of the cancelled animation go out afterwards. The
    /// emission gate closes that: a replacement waits for the keyframe in flight, and everything after it
    /// belongs to the new owner. What this asserts is the ordering that follows - no command of the old
    /// animation appears after that animation's own ending.
    /// </summary>
    [Fact]
    public void CORE003_NoKeyframeOfACancelledAnimationIsEmittedAfterItsEnd()
    {
        var sink = new BlockingSink { BlockOn = "head" };
        var scheduler = new AnimationScheduler(sink, new Random(1));
        var clip = new AnimationClip
        {
            Name = "two-keyframes",
            Keyframes = new List<Keyframe>
            {
                new HeadKeyframe(0, 100, 10, 0),
                new LiftKeyframe(0, 100, 40, 0),
            },
            Tracks = AnimationTrack.Head | AnimationTrack.Lift,
            DurationMs = 5_000,
        };
        scheduler.Play(clip, 0);

        // one frame, on another thread, which will block inside the head keyframe's emission
        var streaming = Task.Run(() => scheduler.Advance(0));
        Assert.True(sink.Entered.Wait(2_000), "the sink was never reached");

        // cancel while that emission is in flight
        var stopping = Task.Run(() => scheduler.Stop());
        Thread.Sleep(50);                       // give the cancel every chance to get in front
        sink.Release.Set();
        Assert.True(streaming.Wait(2_000));
        Assert.True(stopping.Wait(2_000));

        var log = sink.Snapshot();
        Assert.Contains("head", log);            // the keyframe that was legitimately in flight went out
        int ended = log.IndexOf("finished");
        Assert.True(ended >= 0, "the animation never ended");
        // nothing of that animation after its ending. Which keyframes got out before the cancel landed is
        // a race and not a contract - a cancel is not instantaneous - but this is: once the animation has
        // ended, nothing of it may still be on its way out.
        Assert.DoesNotContain(log.Skip(ended + 1), e => e is "head" or "lift" or "body" or "started");
    }

    /// <summary>
    /// CORE-003, the tracked play. <c>PlayTracked</c> used to start the animation and then read
    /// <c>Generation</c> back as a separate step, so two callers racing could both come away with the
    /// same token - the later playback's - and stopping by it would stop somebody else's animation. The
    /// token now comes from the playback itself.
    /// </summary>
    [Fact]
    public void CORE003_ConcurrentTrackedPlaysEachGetTheirOwnToken()
    {
        using var robot = CozmoRobot.CreateOffline();
        var clip = new AnimationClip
        {
            Name = "short",
            Keyframes = new List<Keyframe> { new HeadKeyframe(0, 50, 5, 0) },
            Tracks = AnimationTrack.Head,
            DurationMs = 2_000,
        };

        const int n = 24;
        var tickets = new AnimationTicket?[n];
        var start = new ManualResetEventSlim(false);
        var threads = new List<Thread>();
        for (int i = 0; i < n; i++)
        {
            int me = i;
            var t = new Thread(() =>
            {
                start.Wait();
                tickets[me] = robot.Animations.PlayTracked(clip);
            });
            threads.Add(t);
            t.Start();
        }
        start.Set();
        foreach (var t in threads) Assert.True(t.Join(5_000));

        var got = tickets.Where(t => t is not null).Select(t => t!.Generation).ToList();
        Assert.Equal(n, got.Count);                       // every play was accepted (each replaces the last)
        Assert.Equal(got.Count, got.Distinct().Count());  // and no two callers were handed the same playback
        robot.Animations.Stop();
    }

    // ================================================================ CORE-004

    /// <summary>
    /// CORE-004. A path run that gives up must clear its own path and nobody else's.
    ///
    /// <c>PathRun</c> knows the id it installed, but its abort called the sender's unqualified
    /// <c>Abort</c>, which clears whatever path the sender last sent. Cleanup from a cancelled or
    /// timed-out wait is exactly the case that arrives late - after another action has installed its own
    /// path - and it would then stop that one instead. Ownership is the path id: an abort that no longer
    /// owns the robot's path sends nothing.
    ///
    /// The engine has one path component and one path, so its own <c>PathComponent::Abort</c> 0x00649100
    /// is unqualified; per-action ownership is this stack's own layer, and this is where it has to hold.
    /// </summary>
    [Fact]
    public void CORE004_AnOldRunsAbortDoesNotClearTheReplacementPath()
    {
        using var rig = new Rig();
        var straight = new List<PathSegment>
        {
            new PathSegment.Line(0, 0, 100, 0, 60, 200, 200),
        };

        var a = rig.M.StartPath(straight);          // path A
        var b = rig.M.StartPath(straight);          // B replaces it before A is cleaned up
        Assert.NotEqual(a.PathId, b.PathId);
        Assert.Equal(b.PathId, rig.M.Paths.LastPathId);

        int clearsBefore = rig.M.Paths.Sent.OfType<ClearPath>().Count();

        a.Abort();                                   // A's late cleanup
        Assert.True(a.Aborted);
        Assert.False(a.ClearedRobotPath);            // it no longer owned the robot's path
        Assert.Equal(clearsBefore, rig.M.Paths.Sent.OfType<ClearPath>().Count());
        Assert.Equal(b.PathId, rig.M.Paths.LastPathId);

        // and B, which does own it, still can
        b.Abort();
        Assert.True(b.ClearedRobotPath);
        Assert.Equal(clearsBefore + 1, rig.M.Paths.Sent.OfType<ClearPath>().Count());
    }

    /// <summary>
    /// CORE-004, the other half the report asked about: two callers installing paths at once must not
    /// interleave their clear, their segments and their execute. Each path is installed whole, under one
    /// id, in one order.
    /// </summary>
    [Fact]
    public void CORE004_ConcurrentInstallationsDoNotInterleave()
    {
        using var rig = new Rig();
        List<PathSegment> Path(int n) => new()
        {
            new PathSegment.Line(0, 0, n, 0, 60, 200, 200),
            new PathSegment.Line(n, 0, n, n, 60, 200, 200),
            new PathSegment.Line(n, n, 0, n, 60, 200, 200),
        };

        const int callers = 8;
        var start = new ManualResetEventSlim(false);
        var threads = new List<Thread>();
        for (int i = 0; i < callers; i++)
        {
            int me = i + 1;
            var t = new Thread(() => { start.Wait(); rig.M.Paths.Execute(Path(me)); });
            threads.Add(t);
            t.Start();
        }
        start.Set();
        foreach (var t in threads) Assert.True(t.Join(5_000));

        // every installation is a clear, its three segments and an execute, in that order and unbroken
        var sent = rig.M.Paths.Sent.ToList();
        int i2 = 0, installs = 0;
        while (i2 < sent.Count)
        {
            Assert.IsType<ClearPath>(sent[i2]);
            ushort id = ((ClearPath)sent[i2]).Unknown;
            i2++;
            int segs = 0;
            while (i2 < sent.Count && sent[i2] is AppendPathSegmentLine) { i2++; segs++; }
            Assert.Equal(3, segs);
            var exec = Assert.IsType<ExecutePath>(sent[i2++]);
            Assert.Equal(id, exec.EventId);          // installed and executed under one id
            installs++;
        }
        Assert.Equal(callers, installs);
    }
}
