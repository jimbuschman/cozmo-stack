using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The three tracks M5 left open: animation audio, backpack lights and arc body motion. Each was resolved
/// by reading the engine rather than by guessing, and each is tested here against what the engine does.
/// </summary>
public class AnimationGapTests
{
    private class Recorder : IAnimationSink
    {
        public readonly List<string> What = new();
        public readonly List<BodyKeyframe> Bodies = new();
        public readonly List<byte> Tags = new();
        public int Ends;
        public readonly List<LightsKeyframe> Lights_ = new();
        public readonly List<string> Events = new();
        public int BodyStops, AudioFrames, AudioWithSound;

        public virtual int? AudioFramesPlayed => null;
        public void Face(FaceBitmap bitmap) => What.Add("face");
        public void Audio(byte[]? mulawFrame)
        {
            AudioFrames++;
            if (mulawFrame is not null) AudioWithSound++;
            What.Add("audio");
        }
        public readonly List<(sbyte Deg, uint Dur)> Heads = new();
        public readonly List<(byte Mm, uint Dur)> Lifts = new();
        public void Head(sbyte angleDeg, uint durationMs) { Heads.Add((angleDeg, durationMs)); What.Add("head"); }
        public void Lift(byte heightMm, uint durationMs) { Lifts.Add((heightMm, durationMs)); What.Add("lift"); }
        public void Body(BodyKeyframe k) { Bodies.Add(k); What.Add("body"); }
        public void AnimationStarted(byte tag) { Tags.Add(tag); }
        public void AnimationEnded() { Ends++; }

        public void BodyStop() { BodyStops++; What.Add("bodystop"); }
        public void Lights(LightsKeyframe k) { Lights_.Add(k); What.Add("lights"); }
        public void Event(string eventId) { Events.Add(eventId); What.Add("event"); }
        public void Finished(string clipName, bool completed) => What.Add("finished");
    }

    private static AnimationClip Clip(string name, params Keyframe[] frames)
    {
        var list = frames.OrderBy(f => f.TriggerTimeMs).ToList();
        AnimationTrack tracks = 0;
        uint end = 0;
        foreach (var f in list) { tracks |= f.Track; end = Math.Max(end, f.EndTimeMs); }
        return new AnimationClip { Name = name, Keyframes = list, Tracks = tracks, DurationMs = end };
    }

    private static void Run(AnimationScheduler s, double fromMs, double toMs)
    {
        for (double t = fromMs; t <= toMs; t += 1000.0 / AnimationScheduler.FrameRateHz) s.Advance(t);
    }

    // ------------------------------------------------------- body motion encoding

    /// <summary>
    /// The engine resolves the radius token itself and sends a 16-bit radius to the robot, letting the
    /// firmware do the geometry. ProcessRadiusString at 0x004FB588 in libcozmoEngine.so defines this mapping.
    /// </summary>
    [Theory]
    [InlineData("STRAIGHT", 32767)]
    [InlineData("straight", 32767)]
    [InlineData("TURN_IN_PLACE", 0)]
    [InlineData("POINT_TURN", 0)]
    [InlineData("40", 40)]
    [InlineData("-40", -40)]
    [InlineData("99999", 32767)]
    [InlineData("-99999", -32768)]
    public void TheRadiusTokenIsEncodedExactlyAsTheEngineDoes(string token, int expected)
        => Assert.Equal((short)expected, new BodyKeyframe(0, 0, token, 0).EncodedRadius);

    [Fact]
    public void AnUnknownRadiusTokenIsRefusedRatherThanGuessedAt()
    {
        var k = new BodyKeyframe(0, 0, "SPIRAL", 50);
        Assert.Null(k.EncodedRadius);
        Assert.False(k.RadiusIsKnown);
    }

    [Fact]
    public void AnArcNowRunsBecauseTheRobotDoesTheGeometry()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("t", new BodyKeyframe(0, 100, "40", 60), new EventKeyframe(400, "end")), 0);
        Run(s, 0, 500);

        var body = Assert.Single(r.Bodies);
        Assert.Equal((short)40, body.EncodedRadius);
        Assert.Equal(1, r.BodyStops);          // an arc runs, so it is stopped when its duration expires
    }

    [Fact]
    public void TheWireMessageCarriesTheSpeedAndTheEncodedRadius()
    {
        var k = new BodyKeyframe(0, 100, "STRAIGHT", -75);
        var m = new Protocol.BodyMotion { Speed = k.Speed, RadiusMm = k.EncodedRadius!.Value };
        var bytes = m.ToBytes();
        Assert.Equal(5, bytes.Length);         // tag plus two 16-bit fields
        var back = (Protocol.BodyMotion)Protocol.RobotMessage.Parse(bytes);
        Assert.Equal((short)-75, back.Speed);
        Assert.Equal(BodyKeyframe.StraightRadius, back.RadiusMm);
    }

    // ------------------------------------------------------- animation bracketing

    /// <summary>
    /// The engine opens every animation with StartOfAnimation carrying a tag and closes it with
    /// EndOfAnimation. AnimationStreamer::SendStartOfAnimation at 0x0057C400 does the first. Keyframes that
    /// arrive outside an open animation are ignored by the robot, which is why body motion did nothing.
    /// </summary>
    [Fact]
    public void EveryAnimationIsOpenedWithATagAndClosedAgain()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("t", new EventKeyframe(0, "a"), new EventKeyframe(100, "b")), 0);

        // Setting an animation up does not open it. The engine buffers StartOfAnimation inside
        // UpdateStream, on the first frame that actually streams, not in InitStream.
        Assert.Empty(r.Tags);

        s.Advance(0);
        Assert.Single(r.Tags);
        Assert.NotEqual(0, r.Tags[0]);       // the engine never opens with zero either
        Assert.Equal(r.Tags[0], s.CurrentTag);
        Assert.Equal(0, r.Ends);
        // and the audio for that frame goes out ahead of the open
        Assert.Equal("audio", r.What[0]);

        Run(s, 0, 200);
        Assert.Equal(1, r.Ends);
        Assert.Equal(0, s.CurrentTag);       // nothing is running, so there is no tag
    }

    [Fact]
    public void EachAnimationGetsItsOwnTagAndNeverZero()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        for (int i = 0; i < 4; i++)
        {
            s.Play(Clip($"c{i}", new EventKeyframe(0, "x")), 0);
            s.Advance(0);                    // the tag only goes out once a frame streams
            s.Stop();
        }
        Assert.Equal(4, r.Tags.Count);
        Assert.Equal(4, r.Tags.Distinct().Count());
        // IncrementTagCtr stores neither 0x00 nor 0xFF, so the usable range is 1..0xFE.
        Assert.DoesNotContain((byte)0, r.Tags);
        Assert.DoesNotContain((byte)0xFF, r.Tags);
    }

    /// <summary>
    /// A sink that reports what the robot has played, so the scheduler's flow control can be exercised
    /// without a robot. <see cref="Played"/> stands in for <c>animState.numAudioFramesPlayed</c>.
    /// </summary>
    private sealed class PacedRecorder : Recorder
    {
        public int? Played;
        public override int? AudioFramesPlayed => Played;
    }

    /// <summary>
    /// UpdateAmountToSend at 0x0057C6F0 gives the engine 14 - (streamed - played) audio frames of room,
    /// and ShouldProcessAnimationFrame refuses a frame outright when there is none. Streaming past that
    /// only produces frames the robot drops.
    /// </summary>
    [Fact]
    public void StreamingStopsWhenTheRobotsAudioBufferIsFull()
    {
        var r = new PacedRecorder { Played = 0 };
        var s = new AnimationScheduler(r);
        s.Play(Clip("t", new EventKeyframe(10_000, "late")), 0);

        for (int i = 0; i < 100; i++) s.Advance(i * 33.0);
        Assert.Equal(CozmoAudio.RobotBufferFrames, r.AudioFrames);   // filled the buffer and stopped

        r.Played = 5;                                                // the robot drains five
        for (int i = 0; i < 100; i++) s.Advance(3300 + i * 33.0);
        Assert.Equal(CozmoAudio.RobotBufferFrames + 5, r.AudioFrames);
    }

    /// <summary>
    /// The engine's animation time is a count of streamed frames. UpdateStream at 0x0057C84C adds 33 to its
    /// stream time (this+0x84) only after a frame has been sent (0x0057CA94..0x0057CA9C), and
    /// ShouldProcessAnimationFrame at 0x0057CC6C ends the frame loop without touching it while the robot
    /// has no room. So during a stall the animation stands still, and when room returns it resumes exactly
    /// where it stopped, one 33 ms frame per audio frame, up to the budget. The old scheduler measured the
    /// timeline from the wall clock: after this stall it would have jumped to 1353 ms and fired all seven
    /// events in one frame with one audio message.
    /// </summary>
    [Fact]
    public void TheTimelineFreezesWhileTheRobotHasNoRoomAndResumesWhereItStopped()
    {
        var r = new PacedRecorder { Played = 0 };
        var s = new AnimationScheduler(r);
        var frames = new List<Keyframe>();
        for (uint k = 14; k <= 20; k++) frames.Add(new EventKeyframe(k * 33, (k * 33).ToString()));
        frames.Add(new EventKeyframe(5000, "end"));
        s.Play(Clip("t", frames.ToArray()), 0);

        for (int i = 0; i < 14; i++) s.Advance(i * 33.0);                // fills the robot's 14-frame budget
        Assert.Equal(CozmoAudio.RobotBufferFrames, r.AudioFrames);
        Assert.Empty(r.Events);
        // M3 A18: the 15th frame (t = 462) is built and its audio stops the drain; its stream-time step is taken anyway
        Assert.Equal(14 * 33, s.PositionMs);

        for (int i = 14; i <= 40; i++) s.Advance(i * 33.0);              // 27 frames of wall time with no room
        Assert.Empty(r.Events);
        Assert.Equal(14 * 33, s.PositionMs);                             // the timeline has not moved

        r.Played = 3;                                                    // the robot has played three frames
        s.Advance(41 * 33.0);
        Assert.Equal(new[] { "462", "495", "528" }, r.Events);           // three frames, three steps of 33
        Assert.Equal(CozmoAudio.RobotBufferFrames + 3, r.AudioFrames);
        Assert.Equal(17 * 33, s.PositionMs);                             // frame 561 built, waiting on the budget (A18)
    }

    /// <summary>
    /// A tick that arrives late owes several frames and streams each of them, with its own audio message
    /// and its own keyframes, rather than firing everything in one frame. The engine streams frame by
    /// frame to the budget on every update; a late wall-clock tick here is made up the same way.
    /// </summary>
    [Fact]
    public void ALateTickCatchesUpFrameByFrameNotByJumping()
    {
        var r = new PacedRecorder { Played = null };
        var s = new AnimationScheduler(r);
        s.Play(Clip("t", new EventKeyframe(0, "0"), new EventKeyframe(33, "33"), new EventKeyframe(66, "66"),
                         new EventKeyframe(99, "99"), new EventKeyframe(5000, "end")), 0);
        s.Advance(0);
        Assert.Equal(new[] { "0" }, r.Events);

        s.Advance(133);                                                  // 100 ms late: three frames owed
        Assert.Equal(new[] { "0", "33", "66", "99" }, r.Events);
        Assert.Equal(4, r.AudioFrames);                                  // the old timeline sent 2
        Assert.Equal(new[] { "audio", "event", "audio", "event", "audio", "event", "audio", "event" }, r.What);
        Assert.Equal(99, s.PositionMs);
    }

    /// <summary>A sink that reports nothing leaves the scheduler unpaced, so offline replay is unaffected.</summary>
    [Fact]
    public void ASinkThatReportsNothingIsNotPaced()
    {
        var r = new PacedRecorder { Played = null };
        var s = new AnimationScheduler(r);
        s.Play(Clip("t", new EventKeyframe(10_000, "late")), 0);
        for (int i = 0; i < 100; i++) s.Advance(i * 33.0);
        Assert.Equal(100, r.AudioFrames);
    }

    /// <summary>
    /// The engine's tag counter skips both ends of the byte: IncrementTagCtr at 0x0057B660 keeps
    /// incrementing while the value it came from was above 0xFD, so it stores 1..0xFE and never 0 or 0xFF.
    /// </summary>
    [Fact]
    public void TheTagCounterWrapsPastZeroAndFf()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        for (int i = 0; i < 600; i++)        // more than two full wraps
        {
            s.Play(Clip("c", new EventKeyframe(0, "x")), 0);
            s.Advance(0);
            s.Stop();
        }
        Assert.Equal(600, r.Tags.Count);
        Assert.All(r.Tags, t => Assert.InRange(t, (byte)1, (byte)0xFE));
    }

    [Fact]
    public void CancellingSendsTheRobotNoEndOfAnimation()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("t", new EventKeyframe(5000, "late")), 0);
        s.Advance(0);                        // opens it
        Assert.Equal(0, r.Ends);
        s.Stop();
        Assert.Equal(0, r.Ends);             // AnimationStreamer::Abort 0x0057B3E0 sends the robot nothing
    }

    /// <summary>
    /// The mirror of the above: an animation stopped before its first streamed frame was never opened on
    /// the robot, so closing it would close whatever else is open instead.
    /// </summary>
    [Fact]
    public void AnAnimationStoppedBeforeItStreamedIsNeverClosed()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("t", new EventKeyframe(5000, "late")), 0);
        s.Stop();
        Assert.Empty(r.Tags);
        Assert.Equal(0, r.Ends);
    }

    [Fact]
    public void ReplacingOpensTheSecondWithoutClosingTheFirst()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("first", new EventKeyframe(5000, "late")), 0);
        s.Advance(0);
        // the second outlasts the assertions, so the only close here is the first one being replaced
        s.Play(Clip("second", new EventKeyframe(0, "now"), new EventKeyframe(5000, "later")), 10);
        s.Advance(10);

        Assert.Equal(2, r.Tags.Count);
        Assert.Equal(0, r.Ends);             // Abort sends no EndOfAnimation; the second's StartOfAnimation follows
        Assert.NotEqual(r.Tags[0], r.Tags[1]);
    }

    // ------------------------------------------------------- the synthetic arc clip

    [Fact]
    public void TheSyntheticArcClipIsConservativeAndReturnsWhereItStarted()
    {
        var clip = Cozmo.Conformance.Anim.BuildArcClip(60, 30, 1.0);
        var bodies = clip.Keyframes.OfType<BodyKeyframe>().ToList();
        Assert.Equal(2, bodies.Count);
        Assert.Equal(AnimationTrack.Body, clip.Tracks);      // nothing but the body: no face, no audio

        // equal and opposite arcs at the same speed, so the robot ends roughly where it began
        Assert.Equal((short)60, bodies[0].EncodedRadius);
        Assert.Equal((short)-60, bodies[1].EncodedRadius);
        Assert.Equal(bodies[0].Speed, bodies[1].Speed);
        Assert.Equal(bodies[0].DurationTimeMs, bodies[1].DurationTimeMs);
        Assert.True(bodies[1].TriggerTimeMs > bodies[0].EndTimeMs, "the two arcs must not overlap");
    }

    [Theory]
    [InlineData(60, 9999, 1.0, 100)]      // speed is clamped well below what the robot accepts
    [InlineData(60, -9999, 1.0, -100)]
    public void TheSyntheticArcClampsSpeed(float radius, float speed, double seconds, int expected)
    {
        var clip = Cozmo.Conformance.Anim.BuildArcClip(radius, speed, seconds);
        Assert.Equal((short)expected, clip.Keyframes.OfType<BodyKeyframe>().First().Speed);
    }

    [Theory]
    [InlineData(0.01, 100)]               // and duration is clamped at both ends
    [InlineData(999.0, 5000)]
    public void TheSyntheticArcClampsDuration(double seconds, uint expected)
    {
        var clip = Cozmo.Conformance.Anim.BuildArcClip(60, 30, seconds);
        Assert.Equal(expected, clip.Keyframes.OfType<BodyKeyframe>().First().DurationTimeMs);
    }

    [Fact]
    public void TheSyntheticArcStopsAfterEachLeg()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        var clip = Cozmo.Conformance.Anim.BuildArcClip(60, 30, 1.0);
        s.Play(clip, 0);
        Run(s, 0, clip.DurationMs + 200);

        Assert.Equal(2, r.Bodies.Count);
        Assert.Equal(2, r.BodyStops);     // each leg is stopped when its own duration expires
    }

    // ------------------------------------------------------------------- audio

    private sealed class FixedAudio : IAnimationAudioSource
    {
        private readonly short[]? _pcm;
        public readonly List<(long Id, float Volume)> Asked = new();
        public FixedAudio(short[]? pcm) => _pcm = pcm;
        public short[]? GetPcm(long eventId, float volume) { Asked.Add((eventId, volume)); return _pcm; }
        public string? NameOf(long eventId) => $"event_{eventId}";
    }

    [Fact]
    public void AudioIsStreamedOnTheSchedulerTickNotByAPacerOfItsOwn()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        var source = new FixedAudio(CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(500)));
        s.AudioSource = source;

        s.Play(Clip("t", new AudioKeyframe(0, new long[] { 1234 }, 1f, Array.Empty<float>(), false),
                         new EventKeyframe(600, "end")), 0);
        Run(s, 0, 700);

        Assert.Equal((1234L, 1f), Assert.Single(source.Asked));
        Assert.InRange(r.AudioFrames, 18, 23);          // one frame per tick across the clip
        Assert.InRange(r.AudioWithSound, 13, 17);       // 500 ms of tone is about 15 frames of samples
        Assert.True(s.AudioFramesSent >= r.AudioFrames - 1);
    }

    [Fact]
    public void WithNoAudioSourceTheTrackIsSilentButTheTimelineIsUnchanged()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("t", new AudioKeyframe(0, new long[] { 1 }, 1f, Array.Empty<float>(), false),
                         new EventKeyframe(300, "end")), 0);
        Run(s, 0, 400);

        Assert.True(r.AudioFrames > 5, "silence still goes out every tick to keep the buffer fed");
        Assert.Equal(0, r.AudioWithSound);
        Assert.Equal(new[] { "end" }, r.Events);
    }

    [Fact]
    public void AClipWithNoAudioTrackStillSendsASilenceFrameEveryTick()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.AudioSource = new FixedAudio(CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(100)));
        s.Play(Clip("t", new EventKeyframe(0, "a"), new EventKeyframe(200, "b")), 0);
        int ticks = 0;
        for (double t = 0; t <= 300; t += 1000.0 / AnimationScheduler.FrameRateHz)
        {
            if (!s.IsPlaying) break;
            s.Advance(t);
            ticks++;
        }

        // UpdateStream buffers animAudioSample or animAudioSilence on every streamed frame with no way
        // past it (0x0057C992 / 0x0057C9AE), and SendBufferedMessages counts both against the robot's
        // audio budget. A clip with no audio track streams silence, not nothing: the silence frames are
        // what carry the animation forward on the robot.
        Assert.True(ticks > 0);
        Assert.Equal(1, r.Ends);
        // one per streamed frame, and nothing after SendEndOfAnimation (A20); the last tick only ends it (A13)
        Assert.Equal(ticks - 1, r.AudioFrames);
        Assert.Equal(0, r.AudioWithSound);   // silence, because no keyframe asked for a sound
    }

    /// <summary>
    /// The engine picks one alternative by probability: RobotAudioKeyFrame::GetAudioRefIndex(true) at
    /// 0x004F9AEC draws RandDbl(1.0) and walks the references' probabilities cumulatively, and
    /// SetMembersFromFlatBuf at 0x004F9E54 gives every alternative 1/n when the clip carries no usable
    /// probabilities. Before this the first alternative that decoded was always used, so a clip with
    /// three alternatives always played the same one.
    /// </summary>
    [Theory]
    [InlineData(new float[] { 0f, 1f, 0f }, 0.0, 1)]
    [InlineData(new float[] { 0f, 1f, 0f }, 0.99, 1)]
    [InlineData(new float[] { 1f, 0f, 0f }, 0.5, 0)]
    [InlineData(new float[] { 0.5f, 0.5f, 0f }, 0.25, 0)]
    [InlineData(new float[] { 0.5f, 0.5f, 0f }, 0.75, 1)]
    [InlineData(new float[] { 0.2f, 0.3f, 0.5f }, 0.45, 1)]
    [InlineData(new float[] { 0.2f, 0.3f, 0.5f }, 0.95, 2)]
    [InlineData(new float[] { 0.5f, 0.2f, 0f }, 0.9, -1)]    // probabilities sum below one: the draw falls in the gap, the engine plays nothing
    [InlineData(new float[] { }, 0.5, 1)]                    // no probabilities: 1/n each, so 0.5 lands in the second of three
    [InlineData(new float[] { 1f }, 0.7, 2)]                 // wrong count: also 1/n each
    public void TheAlternativeIsChosenByProbabilityAsTheEngineDoes(float[] probabilities, double draw, int expected)
    {
        Assert.Equal(expected, AnimationScheduler.ChooseAlternative(3, probabilities, draw));
    }

    [Fact]
    public void AClipWithProbabilitiesPlaysTheWeightedAlternativeNotTheFirst()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r, new Random(1));
        var source = new PickySource(wanted: null);           // can produce anything
        s.AudioSource = source;
        // Only the second alternative carries any probability. The old code asked for 11 first and played it.
        s.Play(Clip("t", new AudioKeyframe(0, new long[] { 11, 22, 33 }, 0.5f, new[] { 0f, 1f, 0f }, true),
                         new EventKeyframe(200, "end")), 0);
        Run(s, 0, 300);

        Assert.Equal(new long[] { 22 }, source.Asked);
    }

    /// <summary>
    /// The draw is the whole of the choice. When the alternative the engine would have chosen produces
    /// nothing, the keyframe is silent - <c>RobotAudioKeyFrame::GetAudioRef</c> 0x004F9E18 draws once
    /// through <c>GetAudioRefIndex</c> 0x004F9AEC and hands that one event to Wwise, and there is no
    /// second draw and no walk down the rest.
    ///
    /// This stack used to try the others in order, which turned a decoder gap of its own into a
    /// different alternative being heard than the app would have played.
    /// </summary>
    [Fact]
    public void AnUnproducibleChosenAlternativeIsSimplySilent()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r, new Random(1));
        var source = new PickySource(wanted: 33);        // will not produce 22, which is the drawn one
        s.AudioSource = source;
        s.Play(Clip("t", new AudioKeyframe(0, new long[] { 11, 22, 33 }, 0.5f, new[] { 0f, 1f, 0f }, true),
                         new EventKeyframe(200, "end")), 0);
        Run(s, 0, 300);

        Assert.Equal(new long[] { 22 }, source.Asked);   // asked for the drawn one, and only that one
    }

    // ------------------------------------------------------- head and lift keyframes

    /// <summary>
    /// The engine streams a head keyframe as animHeadAngle (0x93) and a lift keyframe as animLiftHeight
    /// (0x94): HeadAngleKeyFrame::GetStreamMessage at 0x004F8C08 and LiftHeightKeyFrame::GetStreamMessage
    /// at 0x004F8F80 build exactly those, from the duration and the whole-degree angle or whole-millimetre
    /// height. Before this the player sent SetHeadAngle and SetLiftHeight motor commands with invented
    /// speed and acceleration values. The motors moved on hardware, but that is not what the engine sends.
    /// </summary>
    [Fact]
    public void HeadAndLiftKeyframesGoOutAsAnimationKeyframesNotMotorCommands()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var clip = Clip("t", new HeadKeyframe(0, 120, -7, 0), new LiftKeyframe(0, 250, 60, 0), new EventKeyframe(300, "end"));
        robot.Animations.Scheduler.Play(clip, 0);
        Run(robot.Animations.Scheduler, 0, 400);
        // The engine spaces packets 2 ms apart and batches, so a message queued inside that window waits
        // for the next connection update. A live transport ticks on its own thread; here it is ticked
        // by hand so nothing is left pending when the outbound frames are read.
        for (int i = 0; i < 10; i++) { Thread.Sleep(3); robot.Transport.OfflineTick(); }

        // Nothing acknowledges the offline connection, so every tick re-sends the unacked reliable frames;
        // each message is counted once by its sequence id.
        var sent = robot.Transport.OfflineOutbound
            .SelectMany(f => f.Messages)
            .Where(m => m.Type is ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage)
            .DistinctBy(m => m.Seq)
            .Select(m => RobotMessage.Parse(m.Payload))
            .ToList();

        var head = Assert.Single(sent.OfType<Protocol.HeadAngle>());
        Assert.Equal((sbyte)-7, head.AngleDeg);
        Assert.Equal((ushort)120, head.DurationTimeMs);
        var lift = Assert.Single(sent.OfType<Protocol.LiftHeight>());
        Assert.Equal((byte)60, lift.HeightMm);
        Assert.Equal((ushort)250, lift.DurationTimeMs);
        Assert.Empty(sent.OfType<SetHeadAngle>());
        Assert.Empty(sent.OfType<SetLiftHeight>());
    }

    /// <summary>
    /// GetStreamMessage applies the keyframe's variability at stream time with
    /// RandIntInRange(value - var, value + var) from IKeyFrame::sRNG, and leaves the value alone when the
    /// variability is zero. The old player ignored variability entirely.
    /// </summary>
    [Fact]
    public void VariabilityIsAppliedToHeadAndLiftKeyframesAtStreamTime()
    {
        var exact = new Recorder();
        var s0 = new AnimationScheduler(exact, new Random(7));
        s0.Play(Clip("t", new HeadKeyframe(0, 100, 10, 0), new LiftKeyframe(0, 100, 40, 0), new EventKeyframe(200, "end")), 0);
        Run(s0, 0, 300);
        Assert.Equal((sbyte)10, Assert.Single(exact.Heads).Deg);
        Assert.Equal((byte)40, Assert.Single(exact.Lifts).Mm);

        var seen = new HashSet<sbyte>();
        for (int seed = 0; seed < 40; seed++)
        {
            var r = new Recorder();
            var s = new AnimationScheduler(r, new Random(seed));
            s.Play(Clip("t", new HeadKeyframe(0, 100, 10, 5), new LiftKeyframe(0, 100, 40, 8), new EventKeyframe(200, "end")), 0);
            Run(s, 0, 300);
            var head = Assert.Single(r.Heads);
            Assert.InRange(head.Deg, (sbyte)5, (sbyte)15);
            Assert.InRange(Assert.Single(r.Lifts).Mm, (byte)32, (byte)48);
            seen.Add(head.Deg);
        }
        Assert.True(seen.Count > 1, "variability must actually vary the angle");
    }

    private sealed class PickySource : IAnimationAudioSource
    {
        private readonly long? _wanted;
        public readonly List<long> Asked = new();
        /// <param name="wanted">The only event this source can produce, or null to produce any.</param>
        public PickySource(long? wanted) => _wanted = wanted;
        public short[]? GetPcm(long eventId, float volume)
        {
            Asked.Add(eventId);
            return _wanted is null || eventId == _wanted ? CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(50)) : null;
        }
        public string? NameOf(long eventId) => null;
    }

    [Fact]
    public void AudioStopsWhenTheAnimationIsCancelled()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.AudioSource = new FixedAudio(CozmoAudio.Tone(440, TimeSpan.FromSeconds(5)));
        s.Play(Clip("t", new AudioKeyframe(0, new long[] { 1 }, 1f, Array.Empty<float>(), false),
                         new EventKeyframe(9000, "end")), 0);
        Run(s, 0, 200);
        int before = r.AudioWithSound;
        s.Stop();
        Run(s, 200, 400);
        Assert.Equal(before, r.AudioWithSound);     // nothing more went out after the cancel
    }

    // ------------------------------------------------------------------- wav

    [Fact]
    public void AWavRoundTripsThroughTheAudioSource()
    {
        var pcm = CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(100));
        var decoded = WavAudioSource.ReadWav(BuildWav(pcm, CozmoAudio.SampleRate, 1));
        Assert.Equal(pcm.Length, decoded.Length);
        for (int i = 0; i < pcm.Length; i += 97) Assert.Equal(pcm[i], decoded[i]);

        var source = new WavAudioSource();
        source.Add(7, pcm);
        Assert.Equal(pcm, source.GetPcm(7, 1f));
        Assert.Null(source.GetPcm(8, 1f));

        int loud = pcm.Max(x => Math.Abs((int)x));
        int quiet = source.GetPcm(7, 0.5f)!.Max(x => Math.Abs((int)x));
        Assert.InRange(quiet, loud / 2 - 100, loud / 2 + 100);
    }

    [Fact]
    public void AStereoWavAtAnotherRateIsMixedAndResampled()
    {
        var stereo = new short[200];
        for (int i = 0; i < 100; i++) { stereo[i * 2] = 1000; stereo[i * 2 + 1] = 3000; }
        var decoded = WavAudioSource.ReadWav(BuildWav(stereo, CozmoAudio.SampleRate * 2, 2));
        Assert.Equal(50, decoded.Length);        // 100 frames at double rate becomes 50
        Assert.Equal(2000, decoded[0]);          // the two channels are averaged
    }

    [Fact]
    public void SomethingThatIsNotAWavIsRejectedRatherThanMisread()
        => Assert.Throws<InvalidDataException>(() => WavAudioSource.ReadWav(new byte[] { 1, 2, 3, 4 }));

    private static byte[] BuildWav(short[] samples, int rate, int channels)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int dataBytes = samples.Length * 2;
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + dataBytes);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
        w.Write((short)1); w.Write((short)channels); w.Write(rate);
        w.Write(rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(dataBytes);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return ms.ToArray();
    }

    // ------------------------------------------------------------ sound metadata

    [Fact]
    public void SoundEventsResolveToTheirNames()
    {
        const string xml = """
            <SoundBanksInfo>
              <SoundBanks>
                <SoundBank Id="1" Language="SFX">
                  <ShortName>SFX</ShortName>
                  <IncludedEvents>
                    <Event Id="1620542011" Name="Play__Robot_Sfx__Scrn_Sad_Long"/>
                  </IncludedEvents>
                </SoundBank>
              </SoundBanks>
              <File Id="9"><ShortName>a.wav</ShortName><Path>Voices/a.wem</Path></File>
            </SoundBanksInfo>
            """;
        var index = SoundBankIndex.Parse(xml);
        Assert.Equal(1, index.EventCount);
        Assert.Equal(1, index.FileCount);
        Assert.Equal("Play__Robot_Sfx__Scrn_Sad_Long", index.NameOf(1620542011));
        Assert.Equal("SFX", index.BankOf(1620542011));
        Assert.Null(index.NameOf(1));      // an id the metadata does not list
    }

    // ------------------------------------------------------------------ lights

    /// <summary>
    /// M5 C16..C18: the backpack track is loaded (the FlatBuffer converted to JSON) and streamed as 0x98 BackpackLights
    /// every frame while its keyframe is current, until counter ≥ duration: duration 100 gives 5 frames. Left
    /// [1, 0.7, 0, 0] is r 255, g trunc(178.5) = 178, b 0, a 0: 0x7C00 | 0x2C0 = 0x7EC0.
    /// </summary>
    [Fact]
    public void ALightsKeyframeIsStreamedEveryFrameWhileCurrent()
    {
        var r = new LightsRecorder();
        var s = new AnimationScheduler(r);
        var lights = new LightsKeyframe(0, 100,
            new[] { 1f, 0.7f, 0f, 0f }, new float[4], new float[4], new float[4], new float[4]);
        s.Play(Clip("t", lights, new EventKeyframe(300, "TAPPED_BLOCK")), 0);
        Run(s, 0, 400);

        Assert.Equal(5, r.Leds.Count);
        Assert.All(r.Leds, l => Assert.Equal(new ushort[] { 0x7EC0, 0, 0, 0, 0 }, l));
        Assert.Empty(r.Lights_);                               // the keyframe callback is not the wire path
    }

    private sealed class LightsRecorder : Recorder, IAnimationSink
    {
        public readonly List<ushort[]> Leds = new();
        public void BackpackLights(ushort[] leds) => Leds.Add(leds);
    }
}
