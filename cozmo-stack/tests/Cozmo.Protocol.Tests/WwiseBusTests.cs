using Cozmo.Robot;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The effect chain a robot's audio really passes through (fidelity manifest M9-011).
///
/// The bus is the one the engine's own <c>RobotAudioClient</c> constructor registers for a robot game
/// object, and the effects on it are read from the shipped <c>Init.bnk</c> with their parameters. What
/// these tests pin is the chain and its settings, and the level the chain produces — which is the point
/// of a bus limiter, and the thing the local peak normalisation it replaced could not have got right.
/// </summary>
public class WwiseBusTests
{
    private static readonly Lazy<WwiseSoundLibrary?> Library = new(() => WwiseAssets.Library);

    private const uint RobotBus1 = 2678428988, RobotBus2 = 2678428991, RobotBus3 = 2678428990, RobotBus4 = 2678428985;
    private const uint MasterCurve = 1734867999, HiLowPass = 390660550, PeakLimiter = 3743559935;
    private const uint CozmoRobotBus = 1723505802, CozmoRobotExternalBus = 2476517424, MasterCompressor = 2313011259;

    /// <summary>
    /// The four robot buses each carry the same three effects, in the same order, followed by their own
    /// Anki Hijack — and each hijack's single parameter is the robot index the engine passes alongside
    /// that bus when it registers the buffer (7 to bus 1, 8 to bus 2, 9 to bus 3, 10 to bus 4, with
    /// plug-in indices 1 to 4). The bank and the binary agree from two directions.
    /// </summary>
    [Theory]
    [InlineData(RobotBus1, 1u)]
    [InlineData(RobotBus2, 2u)]
    [InlineData(RobotBus3, 3u)]
    [InlineData(RobotBus4, 4u)]
    public void EveryRobotBusCarriesTheSameThreeEffectsAndItsOwnHijack(uint busId, uint hijackIndex)
    {
        if (Library.Value is not { } lib) return;
        var bus = Assert.IsType<WwiseBusNode>(lib.Node(busId));
        Assert.Equal(4, bus.Effects.Count);
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, bus.Effects.Select(e => e.Index).ToArray());
        Assert.Equal(MasterCurve, bus.Effects[0].EffectId);
        Assert.Equal(HiLowPass, bus.Effects[1].EffectId);
        Assert.Equal(PeakLimiter, bus.Effects[2].EffectId);

        var hijack = Assert.IsType<WwiseEffectNode>(lib.Node(bus.Effects[3].EffectId));
        Assert.Equal(WwiseEffectNode.AnkiHijackPlugin, hijack.PluginId);
        Assert.Equal(hijackIndex, hijack.HijackIndex());
    }

    /// <summary>The settings, read from the bank. These are the numbers the render is shaped by.</summary>
    [Fact]
    public void TheRobotBusEffectsReadTheirShippedSettings()
    {
        if (Library.Value is not { } lib) return;

        var curve = Assert.IsType<WwiseEffectNode>(lib.Node(MasterCurve));
        var (bands, output) = curve.ParametricEq()!.Value;
        Assert.Equal((4u, 2.0f, 835.0f, 2.1f, true), bands[0]);          // low shelf, +2 dB at 835 Hz
        Assert.Equal((6u, -2.5f, 1359.0f, 4.2f, true), bands[1]);        // peaking, -2.5 dB at 1359 Hz
        Assert.Equal((6u, -4.0f, 5091.0f, 1.5f, true), bands[2]);       // peaking, -4 dB at 5091 Hz
        Assert.Equal(1.5f, output);

        var hiLow = Assert.IsType<WwiseEffectNode>(lib.Node(HiLowPass));
        var (hlBands, hlOutput) = hiLow.ParametricEq()!.Value;
        Assert.Equal((1u, 0.0f, 333.0f, 1.0f, true), hlBands[0]);        // high pass at 333 Hz
        Assert.False(hlBands[1].On);                                     // the middle band is switched off
        Assert.Equal((0u, 0.0f, 14298.0f, 1.0f, true), hlBands[2]);      // low pass at 14298 Hz
        Assert.Equal(0.0f, hlOutput);

        var limiter = Assert.IsType<WwiseEffectNode>(lib.Node(PeakLimiter));
        var l = limiter.PeakLimiter()!.Value;
        Assert.Equal(-1.0f, l.ThresholdDb);
        Assert.Equal(10.8f, l.Ratio, 4);
        Assert.Equal(0.009f, l.LookAheadSeconds, 5);
        Assert.Equal(0.041f, l.ReleaseSeconds, 4);
        Assert.Equal(0.0f, l.OutputDb);
    }

    /// <summary>
    /// The correction this pass made to WWISE_MUSIC.md: the master compressor is not on the robot's path.
    /// It sits on Cozmo_Robot_External, the bus for the app's spoken text, and the bus the sampler routes
    /// to — Cozmo_Robot — carries no effects at all.
    /// </summary>
    [Fact]
    public void TheMasterCompressorIsOnTheExternalVoiceBusAndNotOnTheRobotsOwn()
    {
        if (Library.Value is not { } lib) return;
        var external = Assert.IsType<WwiseBusNode>(lib.Node(CozmoRobotExternalBus));
        var only = Assert.Single(external.Effects);
        Assert.Equal(MasterCompressor, only.EffectId);

        var robot = Assert.IsType<WwiseBusNode>(lib.Node(CozmoRobotBus));
        Assert.Empty(robot.Effects);

        foreach (uint bus in new[] { RobotBus1, RobotBus2, RobotBus3, RobotBus4 })
            Assert.DoesNotContain(MasterCompressor,
                ((WwiseBusNode)lib.Node(bus)!).Effects.Select(e => e.EffectId));
    }

    /// <summary>
    /// The low-pass at 14298 Hz is above Nyquist for the robot's 22320 Hz and cannot act. That is reported
    /// as a note rather than applied at a frequency it cannot have, and rather than passed over in silence.
    /// </summary>
    [Fact]
    public void TheLowPassAboveNyquistIsReportedRatherThanApplied()
    {
        if (Library.Value is not { } lib) return;
        var chain = WwiseBusChain.For(lib, RobotBus1, CozmoAudio.SampleRate);
        var report = chain.Process(new double[1000]);
        Assert.Empty(report.Problems);
        Assert.Contains(report.Notes, n => n.Contains("14298") && n.Contains("Nyquist"));
        Assert.Equal(new[] { "Robot_Bus_Eq_MasterCurve", "Robot_Bus_Eq_HiLowPass", "Robot_Bus_Peak_Limiter" },
            report.Stages.Take(3).ToArray());
        Assert.Contains("tap for robot 1", report.Stages[3]);
    }

    /// <summary>
    /// Every shipped recording is decimated by about two to one on the way to the robot's 22320 Hz, and
    /// taking the nearest sample folded everything above 11160 Hz in the source straight back into the
    /// band. A 15 kHz tone in a 48 kHz recording would have come out as a 7320 Hz one at nearly full
    /// strength — a tone that is not in the recording at all. The band-limited kernel leaves it where it
    /// belongs, which is nowhere, while passing a tone that is inside the band untouched.
    /// (Fidelity manifest M6-004.)
    /// </summary>
    [Fact]
    public void ResamplingDoesNotFoldWhatIsAboveTheRobotsNyquistBackIntoTheBand()
    {
        const int source = 48000, length = 48000;

        static short[] Tone(double hz, int rate, int n)
        {
            var s = new short[n];
            for (int i = 0; i < n; i++) s[i] = (short)Math.Round(20000 * Math.Sin(2 * Math.PI * hz * i / rate));
            return s;
        }
        static double Rms(short[] s, int skip)
        {
            double sum = 0;
            for (int i = skip; i < s.Length - skip; i++) sum += (double)s[i] * s[i];
            return Math.Sqrt(sum / Math.Max(1, s.Length - 2 * skip));
        }

        // a 15 kHz tone is above the robot's 11160 Hz Nyquist and has nowhere to go
        var above = WwiseAudioSource.ToRobotRate(Tone(15000, source, length), 1, source);
        // a 1 kHz tone is well inside the band and should come through at its own level
        var inside = WwiseAudioSource.ToRobotRate(Tone(1000, source, length), 1, source);

        double insideRms = Rms(inside, 200);
        Assert.InRange(insideRms, 20000 / Math.Sqrt(2) * 0.9, 20000 / Math.Sqrt(2) * 1.1);
        Assert.True(Rms(above, 200) < insideRms * 0.1,
            $"the out-of-band tone came through at {Rms(above, 200):F0} against {insideRms:F0} in band");
    }

    /// <summary>
    /// What the chain is for. Every one of the 39 shipped songs comes out of it at about the same level,
    /// just under full scale, however loud the sum that went in was — which is what a bus limiter with a
    /// -1 dB threshold does, and what the local peak normalisation this replaced could not do: that stage
    /// scaled each song by whatever its own loudest sample happened to be.
    /// </summary>
    [Fact]
    public void EverySongLeavesTheChainAtAboutTheSameLevel()
    {
        if (Library.Value is not { } lib || WwiseAssets.ObbRoot is not { } obb) return;
        var songs = Cozmo.Robot.Behavior.SingingBehavior.LoadShipped(obb);
        Assert.Equal(39, songs.Count);
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(3));

        foreach (var b in songs)
        {
            var ev = lib.IdOf("Play__Robot_VO__Cozmo_Singing_" + b.SwitchGroupName["Cozmo_Sings_".Length..].ToLowerInvariant())!.Value;
            var r = source.RenderMusic(ev, new Dictionary<uint, uint> { [b.SwitchGroupId] = b.SwitchId });
            Assert.NotNull(r.BusChain);
            Assert.Empty(r.BusChain!.Problems);
            Assert.Equal(0, r.ClippedSamples);
            Assert.InRange(r.Peak, 28000, short.MaxValue);
            Assert.True(r.BusChain.LimiterReductionDb <= 0);
        }
    }
}
