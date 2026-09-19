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
    private sealed class Recorder : IAnimationSink
    {
        public readonly List<string> What = new();
        public readonly List<BodyKeyframe> Bodies = new();
        public readonly List<LightsKeyframe> Lights_ = new();
        public readonly List<string> Events = new();
        public int BodyStops, AudioFrames, AudioWithSound;

        public void Face(FaceBitmap bitmap) => What.Add("face");
        public void Audio(byte[]? mulawFrame)
        {
            AudioFrames++;
            if (mulawFrame is not null) AudioWithSound++;
            What.Add("audio");
        }
        public void Head(float radians, uint durationMs) => What.Add("head");
        public void Lift(float heightMm, uint durationMs) => What.Add("lift");
        public void Body(BodyKeyframe k) { Bodies.Add(k); What.Add("body"); }
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
    public void AClipWithNoAudioTrackSendsNoAudioAtAll()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        s.AudioSource = new FixedAudio(CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(100)));
        s.Play(Clip("t", new EventKeyframe(0, "a"), new EventKeyframe(200, "b")), 0);
        Run(s, 0, 300);
        Assert.Equal(0, r.AudioFrames);
    }

    [Fact]
    public void TheFirstAlternativeThatCanBeProducedIsUsed()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        var source = new PickySource(wanted: 22);
        s.AudioSource = source;
        s.Play(Clip("t", new AudioKeyframe(0, new long[] { 11, 22, 33 }, 0.5f, Array.Empty<float>(), true),
                         new EventKeyframe(200, "end")), 0);
        Run(s, 0, 300);

        Assert.Equal(new long[] { 11, 22 }, source.Asked);   // stops asking once one works
    }

    private sealed class PickySource : IAnimationAudioSource
    {
        private readonly long _wanted;
        public readonly List<long> Asked = new();
        public PickySource(long wanted) => _wanted = wanted;
        public short[]? GetPcm(long eventId, float volume)
        {
            Asked.Add(eventId);
            return eventId == _wanted ? CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(50)) : null;
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
    /// The shipping engine never implemented this track from animation assets: its SetMembersFromFlatBuf at
    /// 0x004FAAD4 is a stub that logs and returns failure. The keyframe is decoded and reported; nothing is
    /// invented.
    /// </summary>
    [Fact]
    public void ALightsKeyframeIsDecodedAndReportedButNotActedOn()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r);
        var lights = new LightsKeyframe(0, 100,
            new[] { 1f, 0.7f, 0f, 0f }, new float[4], new float[4], new float[4], new float[4]);
        s.Play(Clip("t", lights, new EventKeyframe(200, "end")), 0);
        Run(s, 0, 300);

        var got = Assert.Single(r.Lights_);
        Assert.Equal(new[] { 1f, 0.7f, 0f, 0f }, got.Left);   // the asset data survives decoding intact
    }
}
