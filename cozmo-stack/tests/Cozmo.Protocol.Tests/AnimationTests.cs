using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The M5 animation system: asset decoding, timeline scheduling, cancellation, track ownership and the
/// procedural face. Everything runs off a manual clock and a recording sink, so the timing is exact rather
/// than approximate and nothing needs a robot.
/// </summary>
public class AnimationTests
{
    // ------------------------------------------------------------------ harness

    /// <summary>Records everything the scheduler asks for, so a test can assert on the timeline.</summary>
    private sealed class Recorder : IAnimationSink
    {
        public readonly List<(string What, double At)> Calls = new();
        public readonly List<string> Events = new();
        public readonly List<FaceBitmap> Faces = new();
        public readonly List<(sbyte Deg, uint Dur)> Heads = new();
        public readonly List<(byte Mm, uint Dur)> Lifts = new();
        public readonly List<BodyKeyframe> Bodies = new();
        public readonly List<byte> Tags = new();
        public int Ends;
        public readonly List<double> BodyStops = new();
        public readonly List<LightsKeyframe> Lights_ = new();
        public readonly List<(string Clip, bool Completed)> Finishes = new();
        public double Now;

        public void Face(FaceBitmap bitmap) { Faces.Add(bitmap); Calls.Add(("face", Now)); }
        public void Audio(byte[]? mulawFrame) => Calls.Add(("audio", Now));
        public void Head(sbyte angleDeg, uint durationMs) { Heads.Add((angleDeg, durationMs)); Calls.Add(("head", Now)); }
        public void Lift(byte heightMm, uint durationMs) { Lifts.Add((heightMm, durationMs)); Calls.Add(("lift", Now)); }
        public void Body(BodyKeyframe k) { Bodies.Add(k); Calls.Add(("body", Now)); }
        public void AnimationStarted(byte tag) { Tags.Add(tag); }
        public void AnimationEnded() { Ends++; }

        public void BodyStop() { BodyStops.Add(Now); Calls.Add(("bodystop", Now)); }
        public void Lights(LightsKeyframe k) { Lights_.Add(k); Calls.Add(("lights", Now)); }
        public void Event(string eventId) { Events.Add(eventId); Calls.Add(("event", Now)); }
        public void Finished(string clipName, bool completed) { Finishes.Add((clipName, completed)); Calls.Add(("finished", Now)); }
    }

    private static AnimationClip Clip(string name, params Keyframe[] frames)
    {
        var list = frames.OrderBy(f => f.TriggerTimeMs).ToList();
        AnimationTrack tracks = 0;
        uint end = 0;
        foreach (var f in list) { tracks |= f.Track; end = Math.Max(end, f.EndTimeMs); }
        return new AnimationClip { Name = name, Keyframes = list, Tracks = tracks, DurationMs = end };
    }

    private static FaceKeyframe FaceAt(uint t, float scaleY = 1f)
    {
        var pose = ProceduralFaceRenderer.Nominal();
        pose.Left[EyeParam.EyeScaleY] = scaleY;
        pose.Right[EyeParam.EyeScaleY] = scaleY;
        return new FaceKeyframe(t, pose);
    }

    /// <summary>Runs a scheduler forward one 30 Hz frame at a time, exactly.</summary>
    private static void Run(AnimationScheduler s, Recorder r, double fromMs, double toMs)
    {
        for (double t = fromMs; t <= toMs; t += 1000.0 / AnimationScheduler.FrameRateHz)
        {
            r.Now = t;
            s.Advance(t);
        }
    }

    // ---------------------------------------------------------------- scheduling

    [Fact]
    public void KeyframesFireInOrderAtTheirTriggerTimes()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        var clip = Clip("t",
            new HeadKeyframe(0, 100, 10, 0),
            new EventKeyframe(200, "middle"),
            new LiftKeyframe(400, 100, 60, 0));

        s.Play(clip, 0);
        Run(s, r, 0, 600);

        Assert.Single(r.Heads);
        Assert.Single(r.Lifts);
        Assert.Equal(new[] { "middle" }, r.Events);

        // each fired no earlier than its trigger time, and within one frame of it. The timeline steps in
        // the engine's whole 33 ms frames, so a trigger that falls between two frame times (200 sits
        // between 198 and 231) fires on the next frame; the tolerance carries a rounding margin for that.
        double frame = 1000.0 / AnimationScheduler.FrameRateHz;
        var head = r.Calls.First(c => c.What == "head").At;
        var evt = r.Calls.First(c => c.What == "event").At;
        var lift = r.Calls.First(c => c.What == "lift").At;
        Assert.InRange(head, 0, frame + 0.01);
        Assert.InRange(evt, 200, 200 + frame + 0.01);
        Assert.InRange(lift, 400, 400 + frame + 0.01);
        Assert.True(head < evt && evt < lift, "keyframes must fire in timeline order");
    }

    [Fact]
    public void AnAnimationCompletesOnceAtTheEndOfItsTimeline()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        var handle = s.Play(Clip("t", new EventKeyframe(100, "only")), 0)!;

        Run(s, r, 0, 300);
        Assert.True(handle.Completion.IsCompleted);
        Assert.Equal(AnimationEndReason.Completed, handle.Completion.Result);
        Assert.Equal(("t", true), Assert.Single(r.Finishes));
        Assert.False(s.IsPlaying);

        Run(s, r, 300, 600);                    // ticking on does nothing more
        Assert.Single(r.Finishes);
    }

    /// <summary>
    /// A late tick streams the frames it owes one after another, so nothing is skipped and the order holds.
    /// The frames are real: the clip here ends on its second frame, and both frames carry an audio message,
    /// where the old wall-clock timeline collapsed the whole late interval into one frame with one audio
    /// message.
    /// </summary>
    [Fact]
    public void ATickThatArrivesLateFiresEverythingItMissedInOrder()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("t",
            new EventKeyframe(10, "a"), new EventKeyframe(20, "b"), new EventKeyframe(30, "c")), 0);

        r.Now = 1000;
        s.Advance(1000);                        // one very late tick

        Assert.Equal(new[] { "a", "b", "c" }, r.Events);
        // frame 0 (nothing due) and frame 1 (all three); nothing follows EndOfAnimation (M3 inventory A20)
        Assert.Equal(2, r.Calls.Count(c => c.What == "audio"));
        Assert.False(s.IsPlaying);
    }

    [Fact]
    public void PositionAndKeyframeCountTrackTheTimeline()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("t", new EventKeyframe(0, "a"), new EventKeyframe(500, "b")), 0);

        Run(s, r, 0, 300);
        Assert.InRange(s.PositionMs, 280, 320);
        Assert.Equal(1, s.KeyframesFired);
    }

    // ------------------------------------------------------------- body duration

    /// <summary>
    /// DriveWheels runs until countermanded, so a body keyframe has to be stopped when its own duration
    /// expires. On anim_bored_01 the wheels ran 800 ms against the 264 ms the asset asked for, because the
    /// only stop came from the animation ending.
    /// </summary>
    [Fact]
    public void ABodyKeyframeStopsWhenItsOwnDurationExpiresNotWhenTheClipEnds()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        // the shape of anim_bored_01: a short backward move early in a much longer clip
        s.Play(Clip("t",
            new BodyKeyframe(297, 264, "STRAIGHT", -75),
            new EventKeyframe(1089, "end")), 0);

        Run(s, r, 0, 1200);

        Assert.Single(r.Bodies);
        var stop = Assert.Single(r.BodyStops);
        double frame = 1000.0 / AnimationScheduler.FrameRateHz;
        Assert.InRange(stop, 297 + 264, 297 + 264 + frame);   // stopped at 561 ms, not at 1089
    }

    [Fact]
    public void CancellingMidBodyMotionSendsNoBodyStop()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("t", new BodyKeyframe(0, 5000, "STRAIGHT", 60)), 0);

        Run(s, r, 0, 200);
        Assert.Empty(r.BodyStops);                // still within its duration
        s.Stop();
        Assert.Empty(r.BodyStops);                // AnimationStreamer::Abort 0x0057B3E0 sends no body stop
    }

    [Fact]
    public void ReplacingAnAnimationMidBodyMotionSendsNoBodyStop()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("first", new BodyKeyframe(0, 5000, "STRAIGHT", 60)), 0);
        Run(s, r, 0, 100);
        s.Play(Clip("second", new EventKeyframe(0, "x")), 100);
        Assert.Empty(r.BodyStops);                // a replacement is an Abort too
    }

    /// <summary>
    /// An arc used to be refused because it seemed to need the wheel base. The engine in fact sends the
    /// radius to the robot and lets the firmware do the geometry, so arcs run and therefore need stopping
    /// like any other body move. A token the engine does not recognise still never starts anything.
    /// </summary>
    [Fact]
    public void AnArcIsStoppedLikeAnyOtherBodyMoveButAnUnknownTokenIsNot()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("arc", new BodyKeyframe(0, 100, "40", 60), new EventKeyframe(500, "end")), 0);
        Run(s, r, 0, 600);
        Assert.Single(r.Bodies);
        Assert.Single(r.BodyStops);

        var r2 = new Recorder();
        var s2 = new AnimationScheduler(r2);
        s2.Play(Clip("nonsense", new BodyKeyframe(0, 100, "SPIRAL", 60), new EventKeyframe(500, "end")), 0);
        Run(s2, r2, 0, 600);
        Assert.Single(r2.Bodies);                 // still reported to the sink, which refuses it
        Assert.Empty(r2.BodyStops);               // nothing started, so nothing to stop
    }

    [Fact]
    public void AStationaryOrZeroLengthBodyKeyframeIsNotScheduledForAStop()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("t",
            new BodyKeyframe(0, 0, "STRAIGHT", 60),      // no duration
            new BodyKeyframe(100, 200, "STRAIGHT", 0),   // no speed
            new EventKeyframe(500, "end")), 0);
        Run(s, r, 0, 600);
        Assert.Empty(r.BodyStops);
    }

    // -------------------------------------------------------------- cancellation

    [Fact]
    public void StopEndsTheAnimationAndReportsItWasCancelled()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        var handle = s.Play(Clip("t", new EventKeyframe(0, "a"), new EventKeyframe(5000, "late")), 0)!;

        Run(s, r, 0, 100);
        Assert.True(s.Stop());
        Assert.Equal(AnimationEndReason.Cancelled, handle.Completion.Result);
        Assert.Equal(("t", false), Assert.Single(r.Finishes));

        Run(s, r, 100, 6000);
        Assert.Equal(new[] { "a" }, r.Events);   // the late keyframe never fires
    }

    [Fact]
    public void StoppingNothingIsNotAnError()
    {
        var s = new AnimationScheduler(new Recorder());
        Assert.False(s.Stop());
    }

    // ------------------------------------------------------------ track ownership

    [Fact]
    public void AClipIsRefusedWhenItNeedsATrackSomeoneElseOwns()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        var first = s.Play(Clip("first", new HeadKeyframe(0, 5000, 10, 0)), 0)!;

        var refused = s.Play(Clip("second", new HeadKeyframe(0, 100, -10, 0)), 10, replaceRunning: false);
        Assert.Null(refused);
        Assert.True(first.IsRunning);
        Assert.Equal("first", s.Playing);
        Assert.Empty(r.Finishes);
    }

    /// <summary>
    /// AnimationStreamer::SetStreamingAnimation at 0x0057B174 holds one streaming animation. When one is
    /// streaming and the caller does not ask to interrupt, the newcomer is turned away with "Already
    /// streaming %s, will not interrupt with %s" and nothing changes, whatever tracks it uses; there is no
    /// side-by-side streaming on disjoint tracks. Before this fix a clip on a free track was let through and
    /// replaced the running one even though the caller had asked not to replace anything.
    /// </summary>
    [Fact]
    public void AClipOnAFreeTrackIsStillRefusedBecauseOnlyOneAnimationStreamsAtATime()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        var first = s.Play(Clip("head", new HeadKeyframe(0, 5000, 10, 0)), 0)!;
        var second = s.Play(Clip("lift", new LiftKeyframe(0, 100, 60, 0)), 10, replaceRunning: false);

        Assert.Null(second);                     // no clash, refused all the same
        Assert.True(first.IsRunning);
        Assert.Equal("head", s.Playing);
        Assert.Empty(r.Finishes);

        // Asked to interrupt, the same clip takes over, as the engine's interruptRunning path does.
        var third = s.Play(Clip("lift", new LiftKeyframe(0, 100, 60, 0)), 20, replaceRunning: true);
        Assert.NotNull(third);
        Assert.Equal("lift", s.Playing);
        Assert.Equal(AnimationEndReason.Replaced, first.Completion.Result);
    }

    [Fact]
    public void ReplacingAnAnimationEndsTheFirstAsReplaced()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        var first = s.Play(Clip("first", new HeadKeyframe(0, 5000, 10, 0)), 0)!;
        var second = s.Play(Clip("second", new HeadKeyframe(0, 100, -10, 0)), 10)!;

        Assert.Equal(AnimationEndReason.Replaced, first.Completion.Result);
        Assert.True(second.IsRunning);
        Assert.Equal("second", s.Playing);
    }

    [Fact]
    public void OwnedTracksReportWhatTheRunningClipTouches()
    {
        var s = new AnimationScheduler(new Recorder());
        Assert.Equal(AnimationTrack.None, s.OwnedTracks);
        s.Play(Clip("t", new HeadKeyframe(0, 10, 5, 0), FaceAt(0)), 0);
        Assert.Equal(AnimationTrack.Head | AnimationTrack.Face, s.OwnedTracks);
        s.Stop();
        Assert.Equal(AnimationTrack.None, s.OwnedTracks);
    }

    // --------------------------------------------------------------- face blending

    [Fact]
    public void TheFaceIsBlendedBetweenKeyframesRatherThanStepped()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("t", FaceAt(0, 1.0f), FaceAt(300, 0.1f)), 0);

        Run(s, r, 0, 300);

        Assert.True(r.Faces.Count > 5, "the face should be redrawn every frame");
        int first = Lit(r.Faces[0]);
        int mid = Lit(r.Faces[r.Faces.Count / 2]);
        int last = Lit(r.Faces[^1]);
        Assert.True(first > mid && mid > last,
            $"the eyes should shrink steadily: {first} then {mid} then {last} lit pixels");

        static int Lit(FaceBitmap b)
        {
            int n = 0;
            for (int y = 0; y < FaceBitmap.Height; y++)
                for (int x = 0; x < FaceBitmap.Width; x++) if (b[x, y] != 0) n++;
            return n;
        }
    }

    [Fact]
    public void TheLastFaceIsHeldRatherThanBlanked()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.Play(Clip("t", FaceAt(0), new EventKeyframe(500, "later")), 0);
        Run(s, r, 0, 400);
        Assert.True(r.Faces.Count > 5);
        Assert.Equal(r.Faces[0].ToText(), r.Faces[^1].ToText());
    }

    // ------------------------------------------------------------ procedural face

    [Fact]
    public void AnEyeHasExactlyNineteenParametersInTheEnginesOrder()
    {
        Assert.Equal(19, Eye.ParamCount);
        Assert.Equal(19, Enum.GetValues<EyeParam>().Length);
        // the order is the one the engine's own .rodata block holds
        Assert.Equal(0, (int)EyeParam.EyeCenterX);
        Assert.Equal(4, (int)EyeParam.EyeAngle);
        Assert.Equal(13, (int)EyeParam.UpperLidY);
        Assert.Equal(18, (int)EyeParam.LowerLidBend);
    }

    [Fact]
    public void AnEyeRejectsTheWrongNumberOfParameters()
    {
        Assert.Throws<ArgumentException>(() => new Eye(new float[18]));
        Assert.Throws<ArgumentException>(() => new Eye(new float[20]));
        Assert.Equal(19, new Eye(new float[19]).ToArray().Length);
    }

    [Fact]
    public void BlendingAPoseMovesEveryParameterProportionally()
    {
        var a = ProceduralFaceRenderer.Nominal();
        var b = ProceduralFaceRenderer.Nominal();
        b.Left[EyeParam.EyeCenterX] = 10f;
        b.FaceScaleX = 2f;

        var half = a.BlendTo(b, 0.5f);
        Assert.Equal(5f, half.Left[EyeParam.EyeCenterX], 3);
        Assert.Equal(1.5f, half.FaceScaleX, 3);
        Assert.Equal(0f, a.BlendTo(b, 0f).Left[EyeParam.EyeCenterX], 3);
        Assert.Equal(10f, a.BlendTo(b, 1f).Left[EyeParam.EyeCenterX], 3);
    }

    [Fact]
    public void ANeutralFaceDrawsTwoSeparateEyes()
    {
        var bmp = ProceduralFaceRenderer.Render(ProceduralFaceRenderer.Nominal());
        int left = 0, right = 0, middle = 0;
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = 0; x < FaceBitmap.Width; x++)
            {
                if (bmp[x, y] == 0) continue;
                if (x < 60) left++; else if (x > 68) right++; else middle++;
            }
        Assert.True(left > 100, $"the left eye should be substantial, got {left} pixels");
        Assert.True(right > 100, $"the right eye should be substantial, got {right} pixels");
        Assert.Equal(0, middle);      // and there should be a gap between them
    }

    [Fact]
    public void ClosingAnEyeDrawsNothingForIt()
    {
        var pose = ProceduralFaceRenderer.Nominal();
        pose.Left[EyeParam.EyeScaleY] = 0f;
        var bmp = ProceduralFaceRenderer.Render(pose);
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = 0; x < 60; x++)
                Assert.Equal(0, bmp[x, y]);
    }

    [Theory]
    [InlineData(Expression.Neutral)]
    [InlineData(Expression.Happy)]
    [InlineData(Expression.Sad)]
    [InlineData(Expression.Angry)]
    [InlineData(Expression.Surprised)]
    [InlineData(Expression.Sleepy)]
    [InlineData(Expression.Squinting)]
    [InlineData(Expression.LookingLeft)]
    [InlineData(Expression.LookingRight)]
    public void EveryExpressionRendersSomethingThatFitsOneMessage(Expression e)
    {
        var bmp = ProceduralFaceRenderer.Render(Expressions.Get(e));
        int lit = 0;
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = 0; x < FaceBitmap.Width; x++) if (bmp[x, y] != 0) lit++;
        Assert.True(lit > 40, $"{e} drew only {lit} pixels");
        Assert.True(FaceBitmapCodec.Encode(bmp).Length <= CozmoDisplay.DefaultMaxPayload,
            $"{e} does not fit one message");
    }

    [Fact]
    public void ABlinkClosesTheEyes()
    {
        var open = ProceduralFaceRenderer.Render(Expressions.Get(Expression.Neutral));
        var shut = ProceduralFaceRenderer.Render(Expressions.Get(Expression.Blinking));
        Assert.True(Count(shut) < Count(open) / 4, "a blink should close the eyes almost completely");

        static int Count(FaceBitmap b)
        {
            int n = 0;
            for (int y = 0; y < FaceBitmap.Height; y++)
                for (int x = 0; x < FaceBitmap.Width; x++) if (b[x, y] != 0) n++;
            return n;
        }
    }

    [Fact]
    public void LookingLeftAndRightMoveTheEyesOppositeWays()
    {
        int LeftMost(Expression e)
        {
            var b = ProceduralFaceRenderer.Render(Expressions.Get(e));
            for (int x = 0; x < FaceBitmap.Width; x++)
                for (int y = 0; y < FaceBitmap.Height; y++) if (b[x, y] != 0) return x;
            return -1;
        }
        Assert.True(LeftMost(Expression.LookingLeft) < LeftMost(Expression.LookingRight));
    }

    // ------------------------------------------------------------- flatbuffers

    [Fact]
    public void AFlatBufferThatIsTruncatedIsRejectedRatherThanMisread()
    {
        Assert.Throws<InvalidDataException>(() => FlatTable.Root(new byte[] { 1, 2, 3 }));
        Assert.ThrowsAny<Exception>(() => AnimationLibrary.Parse(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F, 0, 0, 0, 0 }));
    }

    [Fact]
    public void AGroupPicksByWeightAndFallsBackWhenAMoodMatchesNothing()
    {
        var g = new AnimationGroup
        {
            Name = "g",
            Entries = new[]
            {
                new AnimationGroupEntry("always", 1f, 0f, "Default"),
                new AnimationGroupEntry("never", 0f, 0f, "Default"),
            },
        };
        var rnd = new Random(1);
        for (int i = 0; i < 50; i++) Assert.Equal("always", g.Choose(rnd)!.Name);
        Assert.NotNull(g.Choose(rnd, "NoSuchMood"));       // an unmatched mood falls back rather than failing
    }
}
