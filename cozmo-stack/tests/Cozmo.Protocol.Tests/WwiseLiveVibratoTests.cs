using Cozmo.Robot;
using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// That the singing vibrato actually reaches a song that is already playing (fidelity manifest M9-016).
///
/// The engine posts <c>Cozmo_Singing_Vibrato</c> on every tick while Cozmo sings
/// (<c>BehaviorSinging::UpdateInternal</c> 0x005EF0C8) and in the banks that parameter drives the depth
/// of the LFO bound to the sampler's pitch. Before this was fixed the whole song was rendered up front
/// and its samples cached, so the parameter could change all it liked and the audio was already decided;
/// a test that set the parameter <em>before</em> rendering passed while the real path could not work.
/// These tests exercise the real path: prepare, begin, change the parameter, and look at what comes out
/// afterwards.
/// </summary>
public class WwiseLiveVibratoTests
{
    private static readonly Lazy<WwiseSoundLibrary?> Library = new(() => WwiseAssets.Library);

    private const uint Group80 = SingingBehavior.Group80, AbaDaba = 0x852F201Au;
    private const uint Vibrato = SingingBehavior.VibratoParameter;

    private static uint Song(WwiseSoundLibrary lib) => lib.IdOf("Play__Robot_VO__Cozmo_Singing_80bpm")!.Value;

    private static WwiseAudioSource Prepared(WwiseSoundLibrary lib, int seed = 1)
    {
        var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(seed));
        source.SetSwitch(Group80, AbaDaba);
        source.Prewarm(Song(lib)).Wait();
        return source;
    }

    /// <summary>
    /// The refactor invariant first: a song rendered a block at a time, with nothing changing, is the same
    /// song as one rendered in a single pass. If this ever stops holding, the streaming path has drifted
    /// away from the one the offline tools and the whole-library validation exercise.
    /// </summary>
    [Fact]
    public void RenderingASongInBlocksGivesTheSameSongAsRenderingItWhole()
    {
        if (Library.Value is not { } lib) return;

        using var whole = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(11));
        var reference = whole.RenderMusic(Song(lib), new Dictionary<uint, uint> { [Group80] = AbaDaba });

        using var streamed = Prepared(lib, seed: 11);
        var stream = streamed.StreamFor(Song(lib));
        Assert.NotNull(stream);
        var blocked = stream!.RenderAll();

        Assert.Equal(reference.Pcm.Length, blocked.Pcm.Length);
        Assert.Equal(reference.NotesPlayed, blocked.NotesPlayed);
        Assert.Equal(reference.Pcm, blocked.Pcm);
    }

    /// <summary>
    /// The one that matters. Prepare the song, let it begin, then post the vibrato the way the behaviour
    /// does — and the audio rendered after that point differs from the audio the same song produces with
    /// the parameter left alone, while the audio rendered before it is untouched.
    ///
    /// The split is <see cref="WwiseMusicStream.LeadMs"/>: preparation renders that much, so that is
    /// exactly the stretch a change cannot reach, and everything after it is.
    /// </summary>
    [Fact]
    public void AVibratoPostedAfterTheSongHasBegunChangesTheRestOfIt()
    {
        if (Library.Value is not { } lib) return;
        int lead = (int)Math.Round(WwiseMusicStream.LeadMs * CozmoAudio.SampleRate / 1000.0);

        // a reference run: the same song, the same draws, the parameter never posted
        using var quiet = Prepared(lib);
        var quietPcm = quiet.StreamFor(Song(lib))!.RenderAll().Pcm.ToArray();

        // and the real path: prepared, begun, then shaken
        using var shaken = Prepared(lib);
        var pcm = shaken.GetPcm(Song(lib), 1f);                  // what the scheduler is handed
        Assert.NotNull(pcm);
        var stream = shaken.StreamFor(Song(lib))!;
        Assert.Same(pcm, stream.Pcm);                            // the scheduler reads the buffer as it fills

        shaken.SetParameter(Vibrato, 1f);                        // BehaviorSinging posts this every tick
        stream.RenderAll();

        Assert.Equal(quietPcm.Length, pcm!.Length);
        // what was already rendered when the parameter arrived is untouched
        Assert.Equal(quietPcm.Take(lead), pcm.Take(lead));
        // and what came after it is not the same audio
        Assert.False(quietPcm.Skip(lead).SequenceEqual(pcm.Skip(lead)),
            "the song rendered after the vibrato was posted is identical to the one rendered without it");

        int different = 0;
        for (int i = lead; i < pcm.Length; i++) if (pcm[i] != quietPcm[i]) different++;
        Assert.True(different > pcm.Length / 10,
            $"only {different} of {pcm.Length - lead} samples after the change differ");
    }

    /// <summary>
    /// And that what changed is the vibrato, not merely something: with the parameter at full the pitch
    /// modulation reaches the binding's 580 cents, and with it left alone nothing moves the pitch at all.
    /// </summary>
    [Fact]
    public void TheChangeIsThePitchModulationTheBindingDescribes()
    {
        if (Library.Value is not { } lib) return;

        using var quiet = Prepared(lib);
        var quietReport = quiet.StreamFor(Song(lib))!.RenderAll();
        Assert.Equal(0.0, quietReport.ModulationPeakCents);

        using var shaken = Prepared(lib);
        shaken.GetPcm(Song(lib), 1f);
        shaken.SetParameter(Vibrato, 1f);
        var report = shaken.StreamFor(Song(lib))!.RenderAll();

        Assert.True(report.ModulationPeakCents > 500,
            $"the vibrato reached {report.ModulationPeakCents:F0} of the binding's 580 cents");
        Assert.True(report.ModulationPeakCents <= 580.0001);
    }

    /// <summary>
    /// A note that is already sounding when the shake arrives picks it up too, rather than keeping the
    /// depth it started with. That is the difference between rendering block by block and rendering each
    /// voice whole at its onset, and it is why the stream keeps a read position per voice.
    /// </summary>
    [Fact]
    public void AShakeReachesANoteThatHasAlreadyStarted()
    {
        if (Library.Value is not { } lib) return;
        var target = lib.Node(110896138)!;
        var binding = target.Params.Rtpcs.Single(r => r.SourceType == WwiseRtpc.ModulatorSource);
        var lfo = (WwiseModulatorNode)lib.Node(binding.SourceId)!;
        var bound = new WwiseBoundModulator(lfo, binding, lfo.Params.Rtpcs.Single());

        // one voice, two seconds long, with the vibrato binding on it
        var tone = new short[CozmoAudio.SampleRate];
        for (int i = 0; i < tone.Length; i++) tone[i] = (short)(8000 * Math.Sin(2 * Math.PI * 300 * i / CozmoAudio.SampleRate));
        var voice = new WwiseVoice(1, tone, 0, 0, 2000, 1.0, 1.0, 2000, 0, new[] { bound });

        var mix = new double[CozmoAudio.SampleRate * 2];
        var stats = new WwiseModulationStats();
        var still = new Dictionary<uint, float>();
        var shaking = new Dictionary<uint, float> { [Vibrato] = 1f };

        int half = mix.Length / 2;
        voice.RenderInto(mix, 0, half, still, stats);            // the first second: no shake
        Assert.Equal(0.0, stats.PeakCents);
        voice.RenderInto(mix, half, mix.Length, shaking, stats); // and then the cube is shaken
        Assert.True(stats.PeakCents > 500,
            $"a note already sounding did not pick the shake up: {stats.PeakCents:F0} cents");
    }
}
