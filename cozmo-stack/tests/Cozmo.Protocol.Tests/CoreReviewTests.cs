using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Robot.Animation.Wwise;
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

    // ================================================================ CORE-005

    /// <summary>
    /// CORE-005. The docking feedback has to solve the charger's marker with the shape the charger's
    /// marker actually has.
    ///
    /// <c>DockingSystem.OnFrame</c> built its object points from the canonical corners times the marker's
    /// <em>width</em> in both directions, so a marker that is not square was solved as though it were.
    /// The charger's is 20 x 27 (<c>ChargerGeometry.MarkerWidthMm</c> and <c>MarkerHeightMm</c>, from the
    /// charger's own constructor), a 35 per cent error in one axis, and what comes out of it is the
    /// <c>DockingErrorSignal</c> the robot steers by. The world model never had the bug - it goes through
    /// the marker's own geometry - so the two paths disagreed about where the same marker was.
    ///
    /// This drives a real dock against a charger at a known pose and checks the signal against ground
    /// truth rather than against itself.
    /// </summary>
    [Fact]
    public void CORE005_TheChargerDockingSignalUsesTheMarkersRealShape()
    {
        using var rig = new Rig();
        if (rig.NoLibrary) return;
        rig.Head = -0.2f;
        rig.Charger = new Pose3d(Mat3.AboutZ(0), new Vec3(200, 0, 0));   // lip at 200, marker 86 mm further on
        var r = rig.Frame();
        var obs = Assert.Single(r.Objects);
        var charger = obs.Object;
        var marker = Assert.Single(charger.Markers);
        Assert.Equal(20.0, marker.SizeMm);
        Assert.Equal(27.0, marker.HeightMm);

        rig.DockOutcome = BlockStatus.NoBlock;
        var dock = rig.M.Docking.DockAsync(charger, marker, DockAction.Align, PathMotionProfile.Default,
                                           timeout: TimeSpan.FromSeconds(5));
        rig.Pump();
        var signal = Assert.Single(rig.Sent.OfType<DockingErrorSignal>());

        // ground truth: the marker sits 286 mm ahead of the robot (200 + 86), 22 mm up, square on
        Assert.InRange(signal.XDist, 286 - 12, 286 + 12);
        Assert.InRange(Math.Abs(signal.YDist), 0, 6);
        Assert.InRange(signal.ZDist, 22 - 8, 22 + 8);
        Assert.InRange(Math.Abs(StraightLinePlanner.Wrap(signal.Angle)), 0, 0.12);

        rig.M.Docking.Abort();
        rig.Pump();
    }

    /// <summary>
    /// CORE-005, the geometry itself: a marker that is not square keeps its shape, and the corners the
    /// docking solve uses are the same ones the world model uses, less the marker's placement on the
    /// object. There is no second charger constant anywhere.
    /// </summary>
    [Fact]
    public void CORE005_TheMarkersOwnCornersAreWidthByHeight()
    {
        var marker = Assert.Single(ChargerGeometry.Markers);
        var c = marker.Corners3d();
        Assert.Equal(4, c.Length);
        Assert.Equal(ChargerGeometry.MarkerWidthMm, c[2].X - c[0].X, 6);    // TR - TL across
        Assert.Equal(ChargerGeometry.MarkerHeightMm, c[0].Z - c[1].Z, 6);   // TL - BL up
        Assert.NotEqual(c[2].X - c[0].X, c[0].Z - c[1].Z);

        // and the object-frame corners are these put through the marker's pose on the object
        var onObject = marker.CornersOnObject();
        for (int i = 0; i < 4; i++)
        {
            var expected = marker.PoseOnObject.Apply(c[i]);
            Assert.Equal(expected.X, onObject[i].X, 6);
            Assert.Equal(expected.Y, onObject[i].Y, 6);
            Assert.Equal(expected.Z, onObject[i].Z, 6);
        }
    }

    // ================================================================ CORE-006

    /// <summary>
    /// A source whose buffer is only partly rendered, standing in for a song being rendered as it plays.
    /// Nothing about Wwise is involved: what is under test is the contract between a producer that fills
    /// a buffer from the front and the scheduler that reads it.
    /// </summary>
    private sealed class PartialSource : IAnimationAudioSource
    {
        public readonly short[] Buffer;
        public int ReadyCount;
        public int LastConsumedSeen = -1;
        public int Underruns;

        public PartialSource(int samples)
        {
            Buffer = new short[samples];
            for (int i = 0; i < samples; i++) Buffer[i] = (short)(1000 + (i % 100));   // never zero
        }

        public short[]? GetPcm(long eventId, float volume) => Buffer;
        public string? NameOf(long eventId) => "partial";
        public int ReadySamples(short[] pcm, int consumed)
        {
            LastConsumedSeen = consumed;
            if (consumed >= ReadyCount && consumed < pcm.Length) Underruns++;
            return ReadyCount;
        }
    }

    /// <summary>
    /// CORE-006, the readiness half. The scheduler was handed the whole buffer of a song that renders as
    /// it plays and read it regardless of how much had been committed, so anything the renderer had not
    /// reached yet went to the robot as the zeros it was initialised with - silence in place of music,
    /// and unrecoverable, because a frame the robot has been given cannot be taken back.
    ///
    /// The frame now goes out as the animation's own silence while nothing new is ready, and the sound
    /// keeps its place, so what was not rendered in time is heard late instead of being lost.
    /// </summary>
    [Fact]
    public void CORE006_TheSchedulerNeverSendsSamplesThatWereNotRenderedYet()
    {
        var sink = new BlockingSink();
        var scheduler = new AnimationScheduler(sink, new Random(4));
        var source = new PartialSource(CozmoAudio.SamplesPerFrame * 8);
        scheduler.AudioSource = source;

        var clip = new AnimationClip
        {
            Name = "song",
            Keyframes = new List<Keyframe> { new AudioKeyframe(0, new long[] { 7 }, 1.0f, new[] { 1.0f }, false) },
            Tracks = AnimationTrack.Audio,
            DurationMs = 5_000,
        };

        var emitted = new List<byte[]?>();
        var recording = new RecordingSink(emitted);
        scheduler = new AnimationScheduler(recording, new Random(4)) { AudioSource = source };
        scheduler.Play(clip, 0);

        // nothing rendered yet: every frame is silence and the sound does not move
        source.ReadyCount = 0;
        for (int i = 0; i < 3; i++) scheduler.Advance(i * 33);
        Assert.All(emitted, f => Assert.Null(f));
        Assert.True(source.Underruns > 0);

        // two frames' worth committed: two frames of real audio, then silence again
        source.ReadyCount = CozmoAudio.SamplesPerFrame * 2;
        for (int i = 3; i < 8; i++) scheduler.Advance(i * 33);
        var real = emitted.Where(f => f is not null).ToList();
        Assert.Equal(2, real.Count);
        Assert.All(real, f => Assert.Contains(f!, b => b != AnkiMuLaw.Encode(0)));

        // and the rest arrives once it is rendered, rather than having been skipped
        source.ReadyCount = source.Buffer.Length;
        for (int i = 8; i < 20; i++) scheduler.Advance(i * 33);
        Assert.Equal(8, emitted.Count(f => f is not null));
    }

    /// <summary>Records every audio frame the scheduler emits, and does nothing else.</summary>
    private sealed class RecordingSink : IAnimationSink
    {
        private readonly List<byte[]?> _frames;
        public RecordingSink(List<byte[]?> frames) => _frames = frames;
        public void Face(FaceBitmap bitmap) { }
        public void Audio(byte[]? mulawFrame) => _frames.Add(mulawFrame);
        public void Head(sbyte angleDeg, uint durationMs) { }
        public void Lift(byte heightMm, uint durationMs) { }
        public void AnimationStarted(byte tag) { }
        public void AnimationEnded() { }
        public void Body(BodyKeyframe keyframe) { }
        public void BodyStop() { }
        public void Lights(LightsKeyframe keyframe) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }

    /// <summary>
    /// CORE-006, the pacing half. The renderer ran on the wall clock from the moment playback began, so a
    /// robot with no room for another audio frame - which is the normal back-pressure, not a fault - let
    /// the render run on to the end of the song while almost none of it had been heard. Every parameter
    /// posted after that point was arriving at audio that was already decided, which is the very thing
    /// the streaming render exists to prevent.
    ///
    /// The render now follows what has been taken: a lead ahead of consumption and no further.
    /// </summary>
    [Fact]
    public void CORE006_TheRenderStaysWithinALeadOfWhatHasBeenHeard()
    {
        if (WwiseAssets.Library is not { } lib) return;
        uint song = lib.IdOf("Play__Robot_VO__Cozmo_Singing_80bpm")!.Value;
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(5));
        source.SetSwitch(SingingBehavior.Group80, 0x852F201Au);
        source.Prewarm(song).Wait();

        var pcm = source.GetPcm(song, 1f);                    // hands the buffer over and begins playback
        Assert.NotNull(pcm);
        var stream = source.StreamFor(song)!;
        int lead = (int)Math.Round(WwiseMusicStream.LeadMs * CozmoAudio.SampleRate / 1000.0);
        Assert.True(pcm!.Length > 8 * lead, "the song is too short to tell running ahead from finishing");

        // nobody takes any samples: the render must stop a lead in rather than running the song out
        Thread.Sleep(400);
        Assert.True(stream.Ready <= 3 * lead,
                    $"rendered {stream.Ready} samples with nothing consumed (a lead is {lead})");
        Assert.True(stream.Ready < pcm.Length);

        // the scheduler reports what it has taken, the way it does once a frame
        int consumed = 4 * lead;
        source.ReadySamples(pcm, consumed);
        Assert.True(Within(2_000, () => stream.Ready >= consumed),
                    "the render did not follow consumption");
        Assert.True(stream.Ready <= consumed + 3 * lead,
                    $"rendered {stream.Ready} against {consumed} consumed");
    }

    /// <summary>
    /// CORE-006, and what the pacing is for: with the robot stalled, a vibrato posted afterwards still
    /// reaches the audio, because the audio it would reach has not been rendered yet. The same test on
    /// the old arrangement would find the song already finished and the parameter with nothing left to
    /// change.
    /// </summary>
    [Fact]
    public void CORE006_AParameterPostedWhileTheRobotIsStalledStillReachesTheAudio()
    {
        if (WwiseAssets.Library is not { } lib) return;
        uint song = lib.IdOf("Play__Robot_VO__Cozmo_Singing_80bpm")!.Value;

        WwiseAudioSource Prepared(int seed)
        {
            var s = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(seed));
            s.SetSwitch(SingingBehavior.Group80, 0x852F201Au);
            s.Prewarm(song).Wait();
            return s;
        }

        using var quiet = Prepared(21);
        var quietPcm = quiet.StreamFor(song)!.RenderAll().Pcm.ToArray();

        using var stalled = Prepared(21);
        var pcm = stalled.GetPcm(song, 1f)!;
        var stream = stalled.StreamFor(song)!;

        // the robot has no room: nothing is consumed for a while
        Thread.Sleep(300);
        int rendered = stream.Ready;
        Assert.True(rendered < pcm.Length, "the render ran the whole song out while nothing was heard");
        var before = pcm.Take(rendered).ToArray();

        // the behaviour shakes the cube; the song is still mostly unrendered, so this can still reach it
        stalled.SetParameter(SingingBehavior.VibratoParameter, 1f);
        stream.RenderAll();

        Assert.Equal(quietPcm.Length, pcm.Length);
        Assert.Equal(before, pcm.Take(rendered));                       // what was already committed stands
        int different = 0;
        for (int i = rendered; i < pcm.Length; i++) if (pcm[i] != quietPcm[i]) different++;
        Assert.True(different > (pcm.Length - rendered) / 10,
                    $"only {different} of {pcm.Length - rendered} unrendered samples changed");
    }

    // ================================================================ CORE-007

    /// <summary>
    /// CORE-007. The overhead-edge detector and the map's side of it both existed and nothing joined
    /// them: <c>VisionSystem.ProcessImage</c> never ran the detector and no stack ever handed a frame to
    /// <c>AddVisionOverheadEdges</c>, so the map never held an edge however much ground the robot covered.
    ///
    /// The join is the one the engine makes - <c>VisionComponent::UpdateOverheadEdges</c> 0x006553FC into
    /// <c>MapComponent::ProcessVisionOverheadEdges</c> 0x0067F7AC - and the frame relationship matters:
    /// the detector runs on the frame's own pose data, and the frame goes into the map against the robot
    /// pose of that same frame, which is what the engine looks up by the frame's timestamp
    /// (<c>RobotStateHistory::ComputeAndInsertStateAt</c> at 0x0067F8A2).
    ///
    /// This drives a calibrated synthetic frame - flat ground with a dark band across it, which is what a
    /// step or an obstacle edge looks like - through <c>ProcessImage</c> and reads the map afterwards.
    /// </summary>
    [Fact]
    public void CORE007_AFrameWithAGroundEdgeReachesTheMapThroughTheProductionPath()
    {
        using var robot = CozmoRobot.CreateOffline();
        var cal = CameraCalibration.Nominal();
        var vision = new VisionSystem(robot) { Calibration = cal };
        var map = new MemoryMap();

        // the wiring FreeplayStack makes
        vision.OverheadEdges = new OverheadEdgesDetector();
        vision.FrameProcessed += r =>
        {
            if (r.OverheadEdges is { } edges) map.AddVisionOverheadEdges(edges, r.PoseData.RobotPose);
        };

        var pd = new VisionPoseData(1000, new Pose3d(Mat3.Identity, new Vec3(0, 0, 0)),
                                    HeadGeometry.MinHeadAngleRad, 0, false, false);
        var frame = new GrayImage(cal.Columns, cal.Rows);
        frame.Fill(150);
        for (int y = 0; y < cal.Rows; y++)
            for (int x = 0; x < cal.Columns; x++)
                if (y > cal.Rows * 3 / 5) frame.Pixels[y * cal.Columns + x] = 30;   // a dark band across the floor

        var result = vision.ProcessImage(frame, 1, 1000, pd);

        Assert.NotNull(result.OverheadEdges);
        Assert.True(result.OverheadEdges!.GroundPlaneValid);
        Assert.Contains(result.OverheadEdges.Chains, c => c.IsBorder);

        var regions = map.Regions;
        Assert.Contains(regions, r => r.Type == MemoryMapContentType.InterestingEdge);
        Assert.Contains(regions, r => r.Type == MemoryMapContentType.ClearOfObstacle);
        // the edge is in front of the robot, inside the ROI the detector looks at
        foreach (var e in regions.Where(r => r.Type == MemoryMapContentType.InterestingEdge))
            foreach (var p in e.Polygon)
                Assert.InRange(p.X, GroundPlaneROI.DistMm - 1, GroundPlaneROI.DistMm + GroundPlaneROI.LengthMm + 1);
        // and it carries the frame's timestamp, not the wall clock
        Assert.All(regions.Where(r => r.Type == MemoryMapContentType.InterestingEdge), r => Assert.Equal(1000u, r.Timestamp));
    }

    /// <summary>
    /// CORE-007, the other half: a frame with nothing in it must not put an edge in the map, so that what
    /// the first test sees is the band and not the wiring inventing content.
    /// </summary>
    [Fact]
    public void CORE007_FlatGroundLeavesNoEdgeInTheMap()
    {
        using var robot = CozmoRobot.CreateOffline();
        var cal = CameraCalibration.Nominal();
        var vision = new VisionSystem(robot) { Calibration = cal, OverheadEdges = new OverheadEdgesDetector() };
        var map = new MemoryMap();
        vision.FrameProcessed += r =>
        {
            if (r.OverheadEdges is { } edges) map.AddVisionOverheadEdges(edges, r.PoseData.RobotPose);
        };

        var pd = new VisionPoseData(1000, new Pose3d(Mat3.Identity, new Vec3(0, 0, 0)),
                                    HeadGeometry.MinHeadAngleRad, 0, false, false);
        var frame = new GrayImage(cal.Columns, cal.Rows);
        frame.Fill(150);
        vision.ProcessImage(frame, 1, 1000, pd);

        Assert.DoesNotContain(map.Regions, r => r.Type == MemoryMapContentType.InterestingEdge);
    }
}
