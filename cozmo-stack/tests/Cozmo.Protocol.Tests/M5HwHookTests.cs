using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// B-M5-32: the read-only diagnostic hooks (AnimationScheduler.Diagnostic) and their join to the wire. Every expected
/// value is a native rule cited with it (M5-028 0x58D382..38C / 0x58D48C..490 / 0x58D540..550 / ctor 0x58CD78;
/// M5-029 0x64F204..22C / 0x57C63C..646 / 0x58D69E..D6D0 / 0x58CB8C..CD2A; M3-012 C9 0x57C6F6..7AC, C11/C12,
/// C14 0x57BF96..BFCC, direct End 0x57C464..496), never a value read back from the code under test.
/// </summary>
[Collection("M5 process statics")]       // the drawer/face-animation scan-line parity is a process static (M3 B3)
public class M5HwHookTests
{
    /// <summary>Serialises each sink call to the EngineToRobot message RobotAnimationSink sends, in send order.</summary>
    private sealed class WireSink : IAnimationSink
    {
        public readonly List<byte[]> Messages = new();
        public Action<byte[]>? OnSend;
        public bool Paced;
        public int Played, BytesPlayed;
        public int? AudioFramesPlayed => Paced ? Played : null;
        public int? AnimBytesPlayed => Paced ? BytesPlayed : null;
        private void Send(RobotMessage m) { var b = m.ToBytes(); Messages.Add(b); OnSend?.Invoke(b); }
        public void Face(FaceBitmap bitmap) => FaceImage(FaceBitmapCodec.Encode(bitmap));
        public void FaceImage(byte[] payload) => Send(new Cozmo.Protocol.FaceImage { Image = payload });
        public void Audio(byte[]? f) { if (f is null) Send(new AudioSilence()); else Send(new AudioSample { Samples = f }); }
        public void Head(sbyte deg, uint dur) => Send(new Cozmo.Protocol.HeadAngle { DurationTimeMs = (ushort)dur, AngleDeg = deg });
        public void Lift(byte mm, uint dur) => Send(new Cozmo.Protocol.LiftHeight { DurationTimeMs = (ushort)dur, HeightMm = mm });
        public void AnimationStarted(byte tag) => Send(new StartOfAnimation { AnimId = tag });
        public void AnimationEnded() => Send(new EndOfAnimation());
        public void Body(BodyKeyframe k) { if (k.EncodedRadius is { } r) Send(new BodyMotion { Speed = k.Speed, RadiusMm = r }); }
        public void BodyStop() => Send(new BodyMotion { Speed = 0, RadiusMm = BodyKeyframe.StraightRadius });
        public void Lights(LightsKeyframe keyframe) { }
        public void BackpackLights(ushort[] leds) => Send(new Cozmo.Protocol.BackpackLights { Field0 = (ushort[])leds.Clone() });
        public void AnimEvent(byte animEvent) => Send(new Cozmo.Protocol.Event { Field0 = animEvent });
        public void RecordHeading() => Send(new Cozmo.Protocol.RecordHeading());
        public void TurnToRecordedHeading(TurnToRecordedHeadingKeyframe k) => Send(RobotAnimationSink.ToMessage(k));
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }

    private sealed class FixedAudio : IAnimationAudioSource
    {
        private readonly short[] _pcm;
        public FixedAudio(short[] pcm) => _pcm = pcm;
        public short[]? GetPcm(long eventId, float volume) => _pcm;
        public string? NameOf(long eventId) => "tone";
    }

    /// <summary>
    /// A scheduler, its wire (one synthetic capture per sink send, 0.5 ms after the tick that sent it) and its hook
    /// observations stamped with the tick time. The modelled robot plays one audio frame per tick and every byte.
    /// </summary>
    private sealed class Run
    {
        public readonly WireSink Sink = new();
        public readonly AnimationScheduler S;
        public readonly List<M5HwAnalysis.Diagnostic> Hooks = new();
        public readonly List<M5Hw.WireMessage> Wire = new();
        public double Now;
        public Action<Run>? Robot;
        public Run(int seed, bool subscribe = true, bool paced = true, Action<AnimationDiagnostic>? handler = null)
        {
            Sink.Paced = paced;
            S = new AnimationScheduler(Sink, new Random(seed));
            Sink.OnSend = b => Wire.Add(new M5Hw.WireMessage(Now + 0.5, true, b, Wire.Count + 1, 0));
            if (subscribe) S.Diagnostic += d => Hooks.Add(new M5HwAnalysis.Diagnostic(Now, d));
            if (handler is not null) S.Diagnostic += handler;
            Robot = r => { r.Sink.Played = Math.Min(r.S.Stream.FramesStreamed, r.Sink.Played + 1); r.Sink.BytesPlayed = r.S.Stream.BytesStreamed; };
        }
        public void Tick(double t) { Now = t; Robot?.Invoke(this); S.Advance(t); }
        public void Ticks(double from, double to, double step = 1000.0 / AnimationScheduler.FrameRateHz) { for (double t = from; t <= to; t += step) Tick(t); }
        public IEnumerable<T> Of<T>() where T : AnimationDiagnostic => Hooks.Select(h => h.Observation).OfType<T>();
    }

    private static AnimationClip Clip(string name, params Keyframe[] frames)
    {
        var list = frames.OrderBy(f => f.TriggerTimeMs).ToList();
        AnimationTrack tracks = 0; uint end = 0;
        foreach (var f in list) { tracks |= f.Track; end = Math.Max(end, f.EndTimeMs); }
        return new AnimationClip { Name = name, Keyframes = list, Tracks = tracks, DurationMs = end };
    }

    private static FaceKeyframe FaceAt(uint t, float x)
    {
        var pose = ProceduralFaceRenderer.Nominal();
        pose.Left[EyeParam.EyeCenterX] = x;
        return new FaceKeyframe(t, pose);
    }

    private static AnimationClip ShortClip() => Clip("prime", new HeadKeyframe(0, 100, 5, 0), new EventKeyframe(200, "e"));

    private static AnimationClip RichClip(uint durationMs) => Clip("rich",
        new[] { (Keyframe)new AudioKeyframe(0, new long[] { 1 }, 1f, Array.Empty<float>(), false),
                new HeadKeyframe(100, 200, 10, 0), new LiftKeyframe(300, 200, 40, 0), new EventKeyframe(durationMs, "end") }
            .Concat(Enumerable.Range(0, (int)(durationMs / 99)).Select(i => (Keyframe)FaceAt((uint)(i * 99), i % 7))).ToArray());

    /// <summary>The finite layer scenario: a prime clip, idle ticks, then one distortion pulse until the layers end.</summary>
    private static (Run Run, double PulseAt, double EndAt) LayerPulse(int seed, bool subscribe = true, Action<AnimationDiagnostic>? handler = null)
    {
        var run = new Run(seed, subscribe, handler: handler);
        run.S.KeepAliveTimeoutSec = 600;                       // the harness's keep-alive suppression (LAYERS)
        run.S.Play(ShortClip(), 1000);
        run.Ticks(1000, 2000);
        Assert.False(run.S.IsPlaying);
        int pulse = 1;
        run.S.DesiredFaceDistortion = () => Interlocked.Exchange(ref pulse, 0) == 1 ? 0.8f : 0f;
        double at = 2100, t = at;
        for (; t < 6000 && (t == at || run.S.HaveLayersToSend || !run.S.Stream.IsEmpty || run.S.StartEndFlags.StartSent); t += 1000.0 / 30) run.Tick(t);
        return (run, at, t);
    }

    // ---------------------------------------------------------------- M5-028

    // reach: production (the live entry Advance, keep-alive block 0x57CF6A..CFF2 -> KeepFaceAlive; B4 through
    // HandleSetParameters, the retained handler's body) with an unpaced test sink.
    [Fact]
    public void KeepFaceAliveHookReportsSixtyPerCallAndAnInclusiveNewDeadline()
    {
        var run = new Run(3, paced: false);
        run.S.HandleSetParameters(new[] { (byte)LiveIdleParam.BlinkSpacingMinTime_ms, (byte)LiveIdleParam.BlinkSpacingMaxTime_ms }, new[] { 100f, 200f }, false);
        run.S.Play(ShortClip(), 1000);
        run.Ticks(1000, 4000, 60);
        var calls = run.Of<KeepFaceAliveObservation>().ToArray();
        Assert.True(calls.Length > 10);
        Assert.Equal(0, calls[0].BlinkTimerBeforeMs);            // manager+0x14 is 0 from the ctor (0x0058CD78)
        for (int i = 0; i < calls.Length; i++)
        {
            var c = calls[i];
            Assert.Equal(i + 1, c.CallNumber);
            if (i > 0) Assert.Equal(calls[i - 1].BlinkTimerAfterMs, c.BlinkTimerBeforeMs);
            bool due = c.BlinkTimerBeforeMs - 60 <= 0;           // 0x58D382..38C subtract 60; 0x58D48C..490 skip while positive
            Assert.Equal(due, c.BlinkGenerated);
            if (due)
            {
                Assert.Equal(100, c.DrawMinMs); Assert.Equal(200, c.DrawMaxMs);
                Assert.InRange(c.BlinkTimerAfterMs, 100, 200);   // 0x82FA96..FAC8: min + trunc(dbl * (max - min + 1))
            }
            else
            {
                Assert.Equal(c.BlinkTimerBeforeMs - 60, c.BlinkTimerAfterMs);
                Assert.Null(c.DrawMinMs); Assert.Null(c.DrawMaxMs);
            }
        }
        Assert.True(calls.Count(c => c.BlinkGenerated) >= 3);
    }

    // reach: production (Advance through the keep-alive block; B4 handler body sets max == min).
    [Fact]
    public void KeepFaceAliveHookReportsTheFallbackBoundsWhenMaxIsNotAboveMin()
    {
        var run = new Run(4, paced: false);
        run.S.HandleSetParameters(new[] { (byte)LiveIdleParam.BlinkSpacingMinTime_ms, (byte)LiveIdleParam.BlinkSpacingMaxTime_ms }, new[] { 500f, 500f }, false);
        run.S.Play(ShortClip(), 1000);
        run.Ticks(1000, 2000, 60);
        var first = run.Of<KeepFaceAliveObservation>().First();
        Assert.True(first.BlinkGenerated);
        Assert.Equal(7500, first.DrawMinMs); Assert.Equal(30000, first.DrawMaxMs);     // 0x58D540..544
        Assert.InRange(first.BlinkTimerAfterMs, 7500, 30000);
    }

    // ---------------------------------------------------------------- M5-029

    // reach: production (Advance -> no-animation path -> StreamLayers with a DesiredFaceDistortion pulse through
    // TLC.Update/AddGlitch) with a modelled robot sink in place of the transport.
    [Fact]
    public void StreamLayersHookFaceFlagMatchesWhatWasBufferedInStreamOrder()
    {
        var (run, _, _) = LayerPulse(5);
        var sent = run.Of<SendGateObservation>().Where(g => g.Sent).ToArray();
        Assert.Equal(run.Sink.Messages.Count, sent.Length);      // every successful send is one sink message
        for (int k = 0; k < sent.Length; k++)
        {
            Assert.Equal(run.Sink.Messages[k][0], sent[k].MessageId);
            Assert.Equal(run.Sink.Messages[k].Length, sent[k].Size);   // C11: tag plus the member's packed size
        }
        var index = new Dictionary<long, int>();
        for (int k = 0; k < sent.Length; k++) if (sent[k].BufferSequence != 0) index[sent[k].BufferSequence] = k;
        var frames = run.Of<LayerFrameObservation>().ToArray();
        Assert.NotEmpty(frames);
        Assert.False(frames[0].FaceFlag);       // first distortion face trigger is cumulative duration >= 33 (0x58D69E..D6D0): not due at 0
        Assert.True(frames[0].BackpackFlag);    // backpack layer's first keyframe at trigger 0 (0x58CB8C..CD2A)
        Assert.Contains(frames, f => f.FaceFlag);
        foreach (var f in frames)
        {
            int a = index[f.AudioBufferSequence];
            Assert.True(run.Sink.Messages[a][0] is 0x8E or 0x8F);
            int next = a + 1;
            while (next < run.Sink.Messages.Count && run.Sink.Messages[next][0] is not (0x8E or 0x8F or 0x9A)) next++;
            var faces = Enumerable.Range(a + 1, next - a - 1).Where(i => run.Sink.Messages[i][0] == 0x97).ToArray();
            // 0x57C63C..646: StreamLayers buffers the face iff the layered face flag (0x64F204..22C) is set
            if (f.FaceFlag) Assert.Equal(new[] { index[f.FaceBufferSequence!.Value] }, faces);
            else { Assert.Null(f.FaceBufferSequence); Assert.Empty(faces); }
        }
    }

    // ---------------------------------------------------------------- M3-012

    // reach: production (Advance -> UpdateStream/StreamLayers -> RefreshBudget -> Drain, and the direct End) with a
    // modelled robot whose played counters the test sets: substitutes the AnimationState handler (C10).
    [Fact]
    public void SendGateHookFollowsTheNativeAllowanceFormulaAndSuccessfulSendAccounting()
    {
        var run = new Run(6);
        run.S.AudioSource = new FixedAudio(CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(1500)));
        int tick = 0;
        run.Robot = r =>
        {
            tick++;
            r.Sink.Played = Math.Min(r.S.Stream.FramesStreamed, r.Sink.Played + 1);
            int streamed = r.S.Stream.BytesStreamed;
            r.Sink.BytesPlayed = tick < 20 ? streamed + 40000 : tick < 30 ? streamed - 9000 : streamed;   // cap, clamp, normal
        };
        run.S.Play(RichClip(2500), 1000);
        run.Ticks(1000, 5000);
        Assert.False(run.S.IsPlaying);

        var obs = run.Hooks.Select(h => h.Observation).ToArray();
        var refreshes = new Dictionary<long, BudgetRefreshObservation>();
        var spent = new Dictionary<long, (int Bytes, int Frames)>();
        int sentSoFar = 0; bool capSeen = false, clampSeen = false, frameStop = false, byteStop = false, directEnd = false;
        foreach (var o in obs)
        {
            if (o is BudgetRefreshObservation r)
            {
                var before = run.Wire.Take(sentSoFar).ToArray();
                Assert.Equal(before.Sum(m => m.Payload.Length), r.BytesStreamed);                     // C11: every send adds its Size
                Assert.Equal(before.Count(m => m.Payload[0] is 0x8E or 0x8F or 0x9A), r.FramesStreamed); // C11/C12
                int raw = r.BytesPlayed - r.BytesStreamed + 8192;                                      // C9
                Assert.Equal(raw < 0 ? 0 : Math.Min(raw, 30000), r.BytesToSend);
                Assert.Equal(Math.Max(0, r.FramesPlayed - r.FramesStreamed + 14), r.FramesToSend);
                capSeen |= raw > 30000; clampSeen |= raw < 0;
                refreshes[r.BudgetEpoch] = r;
            }
            else if (o is SendGateObservation g)
            {
                var epoch = refreshes[g.BudgetEpoch];
                var used = spent.GetValueOrDefault(g.BudgetEpoch);
                Assert.Equal(epoch.BytesToSend - used.Bytes, g.RemainingBytes);                      // C14 BFB0..BFCC
                Assert.Equal(epoch.FramesToSend - used.Frames, g.RemainingAudioFrames);
                if (g.DirectBypass)
                {
                    Assert.Equal((byte?)0x9A, g.MessageId); directEnd |= g.Sent;                              // 0x57C464..496
                }
                else
                {
                    bool audio = g.MessageId is 0x8E or 0x8F;
                    Assert.Equal(audio, g.CountsAsAudioFrame);
                    bool admit = !(g.Size > g.RemainingBytes) && !(audio && g.RemainingAudioFrames <= 0); // BF96/BF98, BF9A/BFA0
                    Assert.Equal(admit, g.Admitted);
                    if (!admit && g.Size > g.RemainingBytes) byteStop = true;
                    else if (!admit) frameStop = true;
                    if (g.Sent) spent[g.BudgetEpoch] = (used.Bytes + g.Size, used.Frames + (audio ? 1 : 0));
                }
                if (g.Sent)
                {
                    Assert.Equal(run.Wire[sentSoFar].Payload[0], g.MessageId);
                    Assert.Equal(run.Wire[sentSoFar].Payload.Length, g.Size);
                    sentSoFar++;
                }
            }
        }
        Assert.Equal(run.Wire.Count, sentSoFar);
        Assert.True(capSeen && clampSeen && frameStop && byteStop && directEnd,
            $"cap {capSeen} clamp {clampSeen} frameStop {frameStop} byteStop {byteStop} directEnd {directEnd}");
    }

    // ---------------------------------------------------------------- no behaviour change

    private static List<byte[]> FullScenario(bool subscribe, Action<AnimationDiagnostic>? handler = null, List<string>? log = null)
    {
        // The same process-static start for every run: scan-line parity (M3 B3) and the distorter's function-static RNG (G6).
        ScanLineState.Process.Drawer = 0; ScanLineState.Process.FaceAnimation = 0; ScanlineDistorter.Rng = new EngineRandom(1u);
        var run = new Run(11, subscribe, handler: handler);
        if (log is not null) run.S.Log = log.Add;
        run.S.AudioSource = new FixedAudio(CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(600)));
        run.S.Play(RichClip(1500), 1000);
        run.Ticks(1000, 3500);
        int pulse = 1;
        run.S.DesiredFaceDistortion = () => Interlocked.Exchange(ref pulse, 0) == 1 ? 0.8f : 0f;
        run.Ticks(3533, 5000);
        run.S.PushIdleAnimation(AnimationTrigger.ProceduralLive, "test");
        run.Ticks(5033, 11000, 60);
        return run.Sink.Messages;
    }

    // reach: production (Advance over a clip with audio, a layer pulse and the ProceduralLive idle with keep-alive),
    // with a modelled robot sink; compares the outbound bytes with and without subscribers.
    [Fact]
    public void ASubscriberChangesNoOutboundByteOrderOrRandomDraw()
    {
        var oldDistortionRng = ScanlineDistorter.Rng;
        List<byte[]> plain, again, observed, throwing; var log = new List<string>();
        try
        {
            plain = FullScenario(subscribe: false);
            again = FullScenario(subscribe: false, log: new List<string>());
            observed = FullScenario(subscribe: true);
            throwing = FullScenario(subscribe: false, handler: _ => throw new InvalidOperationException("boom"), log: log);
        }
        finally { ScanlineDistorter.Rng = oldDistortionRng; }
        // Precondition: the scenario is deterministic without any subscriber (and with a log attached).
        Assert.Equal(plain.Count, again.Count);
        for (int i = 0; i < plain.Count; i++) Assert.Equal(plain[i], again[i]);
        Assert.True(plain.Count > 200);
        Assert.Contains(plain, m => m[0] == 0x97);
        Assert.Contains(plain, m => m[0] == 0x8E);
        Assert.Contains(plain, m => m[0] == 0x93);
        Assert.Equal(plain.Count, observed.Count);
        for (int i = 0; i < plain.Count; i++) Assert.Equal(plain[i], observed[i]);
        Assert.Equal(plain.Count, throwing.Count);
        for (int i = 0; i < plain.Count; i++) Assert.Equal(plain[i], throwing[i]);
        Assert.Contains(log, l => l.Contains("Animation diagnostic handler threw") && l.Contains("M1-034"));
    }

    // ---------------------------------------------------------------- the graders with hook evidence

    // reach: unit (real scheduler hooks; the wire is the sink's send order with synthetic capture times, standing in
    // for the transport capture the hardware run supplies).
    [Fact]
    public void FiniteLayersIsPositiveWithJoinedHookFlagsAndInconclusiveWhenTheJoinBreaks()
    {
        var (run, pulseAt, endAt) = LayerPulse(5);
        double from = pulseAt - 1, to = endAt + 1;
        var join = M5HwAnalysis.JoinSends(run.Wire, run.Hooks, from, to);
        Assert.Null(join.Problem);
        var (activity, rows, problem) = M5HwAnalysis.LayerActivity(join, run.Hooks, from, to);
        Assert.Null(problem);
        var result = M5HwAnalysis.FiniteLayers(run.Wire, from, to, !run.S.HaveLayersToSend, activity, problem);
        Assert.Equal("OBSERVED_CONDITIONAL_LAYER_FRAMES", result["verdict"]!.GetValue<string>());
        Assert.True(result["faceActivityEstablished"]!.GetValue<bool>());

        // A foreign stream message inside the run: no contiguous match, so nothing is joined.
        var mid = run.Wire[run.Wire.Count - 5];
        var foreign = run.Wire.Append(new M5Hw.WireMessage(mid.AtMs, true, new byte[] { 0x8F }, mid.FrameLine, 1)).ToList();
        var broken = M5HwAnalysis.JoinSends(foreign, run.Hooks, from, to);
        Assert.NotNull(broken.Problem);
        var (noActivity, _, brokenProblem) = M5HwAnalysis.LayerActivity(broken, run.Hooks, from, to);
        Assert.Null(noActivity);
        var inconclusive = M5HwAnalysis.FiniteLayers(foreign, from, to, true, noActivity, brokenProblem);
        Assert.Equal("INCONCLUSIVE", inconclusive["verdict"]!.GetValue<string>());
        Assert.Contains("Send join unavailable", inconclusive["missing"]!.GetValue<string>());
    }

    // reach: unit (real scheduler KeepFaceAlive hook driven through B4 and Advance; synthetic window bounds).
    [Fact]
    public void BlinksIsPositiveWithHookCallsForThe44000To45000Request()
    {
        var run = new Run(8, paced: false);
        run.S.HandleSetParameters(new[] { (byte)LiveIdleParam.BlinkSpacingMinTime_ms, (byte)LiveIdleParam.BlinkSpacingMaxTime_ms }, new[] { 44000f, 45000f }, false);
        run.S.Play(ShortClip(), 1000);
        run.Ticks(1000, 50000, 60);
        var calls = M5HwAnalysis.BlinkCalls(run.Hooks, 0, 50000);
        Assert.True(calls.Count(c => c.Generated) >= 2);
        var yes = new JsonObject { ["answer"] = "yes" };
        var result = M5HwAnalysis.Blinks(run.Wire, 0, 50000, 44000, 45000, yes, calls);
        Assert.Equal("OBSERVED_BLINK_CALL_TRANSITIONS", result["verdict"]!.GetValue<string>());
        Assert.Null(result["missing"]);
        var none = M5HwAnalysis.Blinks(run.Wire, 0, 50000, 44000, 45000, yes, Array.Empty<M5HwAnalysis.BlinkCall>());
        Assert.Equal("INCONCLUSIVE", none["verdict"]!.GetValue<string>());
    }

    // reach: unit (real scheduler gate/refresh hooks over a 6 s audio clip; modelled robot played counters; the wire
    // and the AnimationState delivery snapshots are synthetic stand-ins for the hardware capture).
    [Fact]
    public void AudioIsPositiveWhenEveryAudioMessageJoinsAnObservedGateAndInconclusiveOtherwise()
    {
        var run = new Run(9);
        run.S.AudioSource = new FixedAudio(CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(4000)));
        var events = new List<JsonObject>();
        double playStart = 1000;
        run.S.Play(Clip("audio", new AudioKeyframe(0, new long[] { 1 }, 1f, Array.Empty<float>(), false), new EventKeyframe(6000, "end")), playStart);
        double t = playStart;
        for (; t < 9000 && run.S.IsPlaying; t += 1000.0 / 30)
        {
            run.Tick(t);
            if (((int)(t / 1000)) != ((int)((t - 1000.0 / 30) / 1000)))
                events.Add(new JsonObject { ["kind"] = "animation-state", ["atMs"] = t + 0.7, ["robotTimestamp"] = (uint)t,
                    ["audioPlayed"] = run.Sink.Played, ["bytesPlayed"] = run.Sink.BytesPlayed, ["drops"] = 0,
                    ["streamFrames"] = run.S.Stream.FramesStreamed, ["streamBytes"] = run.S.Stream.BytesStreamed });
        }
        double playEnd = t;
        var join = M5HwAnalysis.JoinSends(run.Wire, run.Hooks, playStart, playEnd);
        var result = M5HwAnalysis.AudioFromDiagnostics(run.Wire, events, playStart, playEnd, run.Hooks);
        Assert.Equal("OBSERVED_AUDIO_FRAMING_AND_COUNTERS", result["framingVerdict"]!.GetValue<string>());
        Assert.Equal("OBSERVED_AUDIO_FRAMING_AND_ADMISSION", result["verdict"]!.GetValue<string>());
        Assert.Equal("OBSERVED_FOR_JOINED_WINDOW", result["budgetCompliance"]!.GetValue<string>());
        Assert.Null(result["admissionEvidenceMissing"]);
        Assert.Equal(0, result["budgetEvidence"]!["allowanceAccountingMismatches"]!.GetValue<int>());

        var mid = run.Wire[run.Wire.Count / 2];
        var foreign = run.Wire.Append(new M5Hw.WireMessage(mid.AtMs, true, new byte[] { 0x8F }, mid.FrameLine, 1)).ToList();
        var broken = M5HwAnalysis.JoinSends(foreign, run.Hooks, playStart, playEnd);
        var inconclusive = M5HwAnalysis.AudioFromDiagnostics(foreign, events, playStart, playEnd, run.Hooks);
        Assert.Equal("INCONCLUSIVE", inconclusive["verdict"]!.GetValue<string>());
        Assert.Equal("NOT_JUDGED", inconclusive["budgetCompliance"]!.GetValue<string>());
        Assert.Contains("Send join unavailable", inconclusive["admissionEvidenceMissing"]!.GetValue<string>());
    }

    // reach: unit (synthetic gate rows and wire: two equally good runs must not be joined).
    [Fact]
    public void AnAmbiguousAlignmentIsNotJoined()
    {
        var gate = new SendGateObservation(0, 1, 1, 0x8F, 1, true, 8192, 14, true, true, false);
        var hooks = new List<M5HwAnalysis.Diagnostic> { new(10, gate), new(11, gate with { BufferSequence = 2 }) };
        var wire = new[] { 10.5, 11.5, 12.5 }.Select((at, i) => new M5Hw.WireMessage(at, true, new byte[] { 0x8F }, i + 1, 0)).ToList();
        var join = M5HwAnalysis.JoinSends(wire, hooks, 10, 12);
        Assert.Contains("ambiguous", join.Problem);
        Assert.Empty(join.BySequence);
        Assert.Equal(2, join.Evidence["matchingRuns"]!.GetValue<int>());
        var audio = M5HwAnalysis.AudioFromDiagnostics(wire, Array.Empty<JsonObject>(), 10, 12, hooks);
        Assert.Equal("INCONCLUSIVE", audio["verdict"]!.GetValue<string>());
        Assert.Contains("ambiguous", audio["admissionEvidenceMissing"]!.GetValue<string>());
        Assert.Equal("NOT_JUDGED", audio["budgetCompliance"]!.GetValue<string>());
        // A capture earlier than its gate observation cannot be that send.
        var early = M5HwAnalysis.JoinSends(new[] { new M5Hw.WireMessage(9, true, new byte[] { 0x8F }, 1, 0),
            new M5Hw.WireMessage(10.5, true, new byte[] { 0x8F }, 2, 0) }, hooks, 10, 12);
        Assert.NotNull(early.Problem);
    }
}
