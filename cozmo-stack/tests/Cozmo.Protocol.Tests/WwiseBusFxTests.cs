using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-013 / gapC 3.1, 4.1..4.9 and correction C45: the Robot_Bus FX chain — EQ <c>0x6767FC1F</c>, EQ <c>0x174901C6</c>, Peak Limiter <c>0xDF2230FF</c> and the Hijack, in slot order while bus state is 1 — and the thin mono adapters (<see cref="WwiseParametricEq"/>, <see cref="WwisePeakLimiter"/>, <see cref="WwiseRobotBusFx"/>) over the exact
/// plug-ins. The plug-ins themselves are proved against the engine's code under Unicorn in <see cref="WwiseEqPluginTests"/> and <see cref="WwiseLimiterPluginTests"/>; the values below are the bank's settings and the words the engine stores for them (the oracle rows of <c>emu_eq.py</c>), never a value read back from the adapters.
/// </summary>
public class WwiseBusFxTests
{
    // ------------------------------------------------------------------ chain order

    /// <summary>
    /// M6-013 / gapC 3.1, addendum: Robot_Bus_1 runs slot 0 EQ <c>0x6767FC1F</c>, slot 1 EQ <c>0x174901C6</c>, slot 2 Peak Limiter <c>0xDF2230FF</c>, slot 3 the Hijack.
    /// </summary>
    [Fact]
    public void TheChainRunsTheSlotsInOrderAndEndsWithTheHijack()
    {
        var chain = new WwiseRobotBusFx();
        Assert.Equal(4, chain.Slots.Count);
        Assert.Contains("0x6767FC1F", chain.Slots[0]);
        Assert.Contains("0x174901C6", chain.Slots[1]);
        Assert.Contains("0xDF2230FF", chain.Slots[2]);
        Assert.Contains("Hijack", chain.Slots[3]);

        // Each DSP stage is the shipped ShareSet, not a caller default.
        Assert.Equal(WwiseEqSettings.ShareSet1, chain.Eq1.Settings.ShareSetId);
        Assert.Equal(WwiseEqSettings.ShareSet2, chain.Eq2.Settings.ShareSetId);
        Assert.Equal(WwisePeakLimiterSettings.ShareSet, 0xDF2230FFu);
        Assert.Equal(WwisePeakLimiterSettings.RobotBus1.ThresholdDb, chain.Limiter.Settings.ThresholdDb);
    }

    /// <summary>
    /// M6-013 / gapC 3.1 / D2.4: the FX loop runs slots only when the bus state is 1. With state 0 the buffer is untouched; with state 1 the chain runs (the limiter's L = 431 frame delay makes the first 431 outputs zero).
    /// </summary>
    [Fact]
    public void ProcessIfActiveRunsOnlyWhenTheBusStateIsOne()
    {
        var buffer = new float[600];
        Array.Fill(buffer, 1f);

        var inactive = new WwiseRobotBusFx();
        inactive.ProcessIfActive(buffer, 0);
        Assert.Equal(1f, buffer[0]);
        Assert.Equal(1f, buffer[599]);

        var active = new WwiseRobotBusFx();
        active.ProcessIfActive(buffer, WwiseRobotBusFx.StateActive);
        // gapC 4.7: the limiter delays the bus by L = 431 samples, so the first 431 outputs are zero.
        Assert.Equal(0f, buffer[0]);
        Assert.Equal(0f, buffer[430]);
        Assert.NotEqual(0f, buffer[431]);
    }

    // ------------------------------------------------------------------ EQ settings

    /// <summary>M6-013 / gapC 4.2 and research bus-fx-17 section 1: the shipped ShareSet settings, exactly as the bank lists them (the LP/HP bands carry gain 0.0 and Q 1.0).</summary>
    [Fact]
    public void TheShippedEqShareSetsMatchTheBanksSettings()
    {
        var eq1 = WwiseEqSettings.Eq1;
        Assert.Equal(3, eq1.Bands.Count);
        Assert.Equal(WwiseEqFilterType.LowShelf, eq1.Bands[0].FilterType);
        Assert.Equal(2.0f, eq1.Bands[0].GainDb);
        Assert.Equal(835f, eq1.Bands[0].Frequency);
        Assert.Equal(2.1f, eq1.Bands[0].Q);
        Assert.True(eq1.Bands[0].On);
        Assert.Equal(WwiseEqFilterType.Peaking, eq1.Bands[1].FilterType);
        Assert.Equal(-2.5f, eq1.Bands[1].GainDb);
        Assert.Equal(1359f, eq1.Bands[1].Frequency);
        Assert.Equal(4.2f, eq1.Bands[1].Q);
        Assert.True(eq1.Bands[1].On);
        Assert.Equal(WwiseEqFilterType.Peaking, eq1.Bands[2].FilterType);
        Assert.Equal(-4.0f, eq1.Bands[2].GainDb);
        Assert.Equal(5091f, eq1.Bands[2].Frequency);
        Assert.Equal(1.5f, eq1.Bands[2].Q);
        Assert.True(eq1.Bands[2].On);
        Assert.Equal(1.5f, eq1.OutputLevelDb);
        Assert.False(eq1.ProcessLfe);

        var eq2 = WwiseEqSettings.Eq2;
        Assert.Equal(WwiseEqFilterType.HighPass, eq2.Bands[0].FilterType);
        Assert.Equal(0f, eq2.Bands[0].GainDb);
        Assert.Equal(333f, eq2.Bands[0].Frequency);
        Assert.Equal(1f, eq2.Bands[0].Q);
        Assert.True(eq2.Bands[0].On);
        Assert.Equal(WwiseEqFilterType.Peaking, eq2.Bands[1].FilterType);
        Assert.Equal(-4.5f, eq2.Bands[1].GainDb);
        Assert.Equal(1000f, eq2.Bands[1].Frequency);
        Assert.Equal(0.5f, eq2.Bands[1].Q);
        Assert.False(eq2.Bands[1].On);            // off
        Assert.Equal(WwiseEqFilterType.LowPass, eq2.Bands[2].FilterType);
        Assert.Equal(0f, eq2.Bands[2].GainDb);
        Assert.Equal(14298f, eq2.Bands[2].Frequency);
        Assert.Equal(1f, eq2.Bands[2].Q);
        Assert.True(eq2.Bands[2].On);
        Assert.Equal(0f, eq2.OutputLevelDb);
        Assert.False(eq2.ProcessLfe);
    }

    // ------------------------------------------------------------------ EQ coefficients (the words the engine stores)

    private static uint[] Bits(WwiseBiquadCoefficients c) => new[] { c.B0, c.B1, c.B2, c.NegA1, c.NegA2 }.Select(BitConverter.SingleToUInt32Bits).ToArray();

    /// <summary>
    /// M6-013 / research 3.6: the five words the engine's coefficient routine <c>0xAA25E0</c> stores for the six shipped bands (Unicorn run, correctly rounded libm; the Coefficients table of <c>WwiseEqOracle</c>). 48000 Hz is the Wwise mix rate; 22320 Hz is the stack's current render rate,
    /// where the 14298 Hz low-pass is capped to (fs * 0.5f) * 0.9f = 10044 Hz. The high pass at 333 Hz is the case the audit found one ulp off with the old association.
    /// </summary>
    [Fact]
    public void TheShippedBandsStoreTheWordsTheEngineStores()
    {
        var shipped = new (uint Type, float Gain, float Freq, float Q, uint Rate, uint[] Words)[]
        {
            (4, 2.0f, 835f, 2.1f, 48000, new uint[] { 0x3F812483, 0xBFED2F3F, 0x3F5B47C1, 0x3FED5953, 0xBF5D3CA0 }),
            (6, -2.5f, 1359f, 4.2f, 48000, new uint[] { 0x3F7E7ABC, 0xBFF5F9E8, 0x3F755C6E, 0x3FF5F9E8, 0xBF73D729 }),
            (6, -4.0f, 5091f, 1.5f, 48000, new uint[] { 0x3F6C8A53, 0xBF9FC789, 0x3F29FFDC, 0x3F9FC789, 0xBF168A30 }),
            (1, 0f, 333f, 1f, 48000, new uint[] { 0x3F783AD2, 0xBFF83AD2, 0x3F783AD2, 0x3FF81CA0, 0xBF70B208 }),
            (6, -4.5f, 1000f, 0.5f, 48000, new uint[] { 0x3F7106CB, 0xBFD91839, 0x3F44E8C9, 0x3FD91839, 0xBF35EF93 }),
            (0, 0f, 14298f, 1f, 48000, new uint[] { 0x3EC613E5, 0x3F4613E5, 0x3EC613E5, 0xBEB518EF, 0xBE466D46 }),
            (4, 2.0f, 835f, 2.1f, 22320, new uint[] { 0x3F8274C7, 0xBFD75E7D, 0x3F3783F0, 0x3FD812AF, 0xBF3B0519 }),
            (6, -2.5f, 1359f, 4.2f, 22320, new uint[] { 0x3F7CDFE4, 0xBFE1E698, 0x3F6A21F2, 0x3FE1E698, 0xBF6701D5 }),
            (6, -4.0f, 5091f, 1.5f, 22320, new uint[] { 0x3F644296, 0xBE468360, 0x3F0567D2, 0x3E468360, 0xBED354D0 }),
            (1, 0f, 333f, 1f, 22320, new uint[] { 0x3F6F94A6, 0xBFEF94A6, 0x3F6F94A6, 0x3FEF0DB6, 0xBF60372D }),
            (6, -4.5f, 1000f, 0.5f, 22320, new uint[] { 0x3F649A73, 0xBFB4D565, 0x3F13E204, 0x3FB4D565, 0xBEF0F8EE }),
            (0, 0f, 14298f, 1f, 22320, new uint[] { 0x3F4CF3A2, 0x3FCCF3A2, 0x3F4CF3A2, 0xBFC7CF74, 0xBF242FA1 }),
        };
        foreach (var (type, gain, freq, q, rate, words) in shipped)
            Assert.Equal(words, Bits(WwiseEqCoefficients.Compute(type, gain, freq, q, rate)));
    }

    /// <summary>
    /// M6-013 / research 3.6 and V3: the frequency cap is <c>f = lim</c> when <c>f &gt;= lim</c> with <c>lim = (fs * 0.5f) * 0.9f</c>, which equals <c>0.45f * fs</c> bit for bit for every integer rate 8000..200000 (the verifier checked all 192001). A 30 kHz peaking band at 48 kHz is the 21.6 kHz band.
    /// </summary>
    [Fact]
    public void TheEqFrequencyIsCappedAtZeroPointNineOfHalfTheRate()
    {
        for (uint rate = 8000; rate <= 200000; rate++)
        {
            float fs = rate;
            Assert.Equal(BitConverter.SingleToUInt32Bits(0.45f * fs), BitConverter.SingleToUInt32Bits(fs * 0.5f * 0.9f));
        }
        var high = WwiseEqCoefficients.Compute(6, -4f, 30000f, 1.5f, 48000);
        var capped = WwiseEqCoefficients.Compute(6, -4f, 21600f, 1.5f, 48000);
        Assert.Equal(capped, high);
        // a NaN frequency stays NaN (vcmp.f32 s18, s17 is unordered: the vmovge does not run)
        Assert.True(float.IsNaN(WwiseEqCoefficients.Compute(6, -4f, float.NaN, 1.5f, 48000).B0));
        Assert.Equal(0.45f, WwiseEqCoefficients.NyquistFraction);
    }

    /// <summary>
    /// M9-027 / gapC 4.3, at the stack's current render rate: this stack runs the exact chain at its 22320 Hz render rate, where the shipped <c>Robot_Bus_Eq_HiLowPass</c> low-pass at 14298 Hz is above 0.9 of Nyquist. The exact routine caps it at (fs * 0.5f) * 0.9f = 10044 Hz and applies it rather than skipping it; the coefficients are exactly
    /// those of a band authored at the cap. This is a stack-rate consequence: the engine runs the chain at the 48000 Hz Wwise mix rate, where 14298 Hz is in band, and the Hijack then resamples to 22320 (M6-017/M6-018). It is not a claim about the engine's low-pass.
    /// </summary>
    [Fact]
    public void AtTheStacksRenderRateTheShippedLowPassIsCappedToTheEnginesLimitAndApplied()
    {
        const uint stackRate = 22320;
        var capped = WwiseEqCoefficients.Compute(0, 0f, 14298f, 1f, stackRate);
        var atCap = WwiseEqCoefficients.Compute(0, 0f, 10044f, 1f, stackRate);
        Assert.Equal(atCap, capped);
        Assert.Equal(10044f, 0.45f * stackRate);
        // The shipped high-pass at 333 Hz is well below the cap, so the cap leaves it alone.
        Assert.NotEqual(WwiseEqCoefficients.Compute(1, 0f, 10044f, 1f, stackRate), WwiseEqCoefficients.Compute(1, 0f, 333f, 1f, stackRate));
    }

    // ------------------------------------------------------------------ the adapters over the plug-ins

    /// <summary>M6-013 / C45.3 through the adapter: a band that is off is not applied; the adapter's coefficients are the engine's words for the shipped HiLowPass band 1 even though it is off (research 3.9).</summary>
    [Fact]
    public void TheEqAdapterComputesTheOffBandButDoesNotApplyIt()
    {
        var eq = new WwiseParametricEq(WwiseEqSettings.Eq2, 48000f);
        var data = new float[16];
        data[0] = 1f;
        eq.Process(data);
        Assert.Equal(new uint[] { 0x3F7106CB, 0xBFD91839, 0x3F44E8C9, 0x3FD91839, 0xBF35EF93 }, Bits(eq.BandCoefficients(1)));
        Assert.All(eq.Plugin.StateSnapshot!.Skip(4).Take(4), v => Assert.Equal(0f, v));
        Assert.Equal(1f, eq.PreviousOutputGain);                                       // powf(10.0f, 0 * 0.05f) is exactly 1.0
    }

    /// <summary>M6-013 / research V2: MasterCurve's +1.5 dB output level is powf(10.0f, 1.5f * 0.05f) = 0x3F9820D7 (1.1885022), not the fast pow's 1.1864475.</summary>
    [Fact]
    public void TheEqAdapterOutputLevelIsPowfNotTheFastPow()
    {
        var eq = new WwiseParametricEq(WwiseEqSettings.Eq1, 48000f);
        Assert.Equal(0x3F9820D7u, BitConverter.SingleToUInt32Bits(eq.PreviousOutputGain));
    }

    // ------------------------------------------------------------------ limiter values

    /// <summary>M6-013 / gapC 4.5: the shipped ShareSet <c>0xDF2230FF</c> and the plug-in defaults.</summary>
    [Fact]
    public void TheLimiterSettingsPinTheShippedShareSetAndDefaults()
    {
        var shipped = WwisePeakLimiterSettings.RobotBus1;
        Assert.Equal(-1.0f, shipped.ThresholdDb);
        Assert.Equal(10.8f, shipped.Ratio);
        Assert.Equal(0.009f, shipped.LookAheadSeconds);
        Assert.Equal(0.041f, shipped.ReleaseSeconds);
        Assert.Equal(0f, shipped.OutputDb);
        Assert.False(shipped.ProcessLfe);
        Assert.Equal((byte)0, shipped.ChannelLink);

        var defaults = WwisePeakLimiterSettings.Default;
        Assert.Equal(-12f, defaults.ThresholdDb);
        Assert.Equal(10f, defaults.Ratio);
        Assert.Equal(0.01f, defaults.LookAheadSeconds);
        Assert.Equal(0.2f, defaults.ReleaseSeconds);
        Assert.Equal(1.0f, defaults.OutputDb);
        Assert.True(defaults.ProcessLfe);
        Assert.Equal((byte)1, defaults.ChannelLink);
    }

    /// <summary>
    /// M6-013 / research 4.3 and 4.5 through the adapter: <c>L = u32(float(sr) * lookahead)</c>; at 48 kHz and 0.009 s that is 431, not 432, because the float product truncates. The attack coefficient is <c>expf(-2.2f / (L / 2))</c> = 0x3F7D665C and, after the first Execute, the release is
    /// <c>expf(-2.2f / (rate * release))</c> = 0x3F7FB6C7 (the engine's words, the oracle's init and Execute rows).
    /// </summary>
    [Fact]
    public void TheLimiterAdapterLookaheadTruncatesTo431At48kAndTheCoefficientsAreTheEnginesWords()
    {
        var limiter = new WwisePeakLimiter(WwisePeakLimiterSettings.RobotBus1, 48000f);
        Assert.Equal(431, limiter.LookAheadSamples);
        Assert.Equal(0x3F7D665Cu, BitConverter.SingleToUInt32Bits(limiter.Attack));
        Assert.Throws<WwiseMissingBehaviourException>(() => limiter.Release);          // the engine computes it at the first Execute
        limiter.Process(new float[8]);
        Assert.Equal(0x3F7FB6C7u, BitConverter.SingleToUInt32Bits(limiter.Release));
    }

    /// <summary>
    /// M6-013 / research 4.7 and V2 through the adapter: an impulse well below the -1 dB threshold appears 431 samples later times the detector gain at zero reduction (the fast power's polynomial at e = 0: 0.9990390), and no second factor: the 0 dB output gain is powf(10, 0) = 1.0 exactly.
    /// </summary>
    [Fact]
    public void TheLimiterAdapterDelaysTheSignalByTheLookahead()
    {
        var limiter = new WwisePeakLimiter(WwisePeakLimiterSettings.RobotBus1, 48000f);
        var buffer = new float[500];
        buffer[0] = 0.5f;                       // 0.5 < 10^(-1/20) = 0.891, so no reduction
        limiter.Process(buffer);

        for (int i = 0; i < 431; i++) Assert.Equal(0f, buffer[i]);
        Assert.InRange(buffer[431], 0.5f * 0.9990389f - 1e-6f, 0.5f * 0.9990390f + 1e-6f);
        Assert.Equal(1f, limiter.PreviousOutputGain);
        Assert.InRange(limiter.MinGain, 0.9990389f, 0.9990391f);
    }

    /// <summary>M6-013 / research 4.7: a full-scale impulse is pushed back; the delayed sample at L is attenuated (bounds only; the exact bits are in the limiter oracle).</summary>
    [Fact]
    public void TheLimiterAdapterReducesAFullScaleImpulse()
    {
        var limiter = new WwisePeakLimiter(WwisePeakLimiterSettings.RobotBus1, 48000f);
        var buffer = new float[432];
        buffer[0] = 1.0f;                       // 0 dB, 1 dB over the -1 dB threshold
        limiter.Process(buffer);

        Assert.InRange(buffer[431], 0.85f, 0.999f);
    }

    /// <summary>
    /// M6-013 / research 4.3 (a linked limiter on a mono bus): the SFX bus limiter (<c>0x3ABE7001</c>: ChannelLink 1, ProcessLFE 1) with one processed channel selects P2, so it runs; the same block on two channels would select P3 (a required stop, tested in <see cref="WwiseLimiterPluginTests"/>).
    /// </summary>
    [Fact]
    public void ALinkedLimiterOnAMonoBusRunsP2()
    {
        var sfx = new WwisePeakLimiterSettings(-0.5f, 10f, 0.01f, 0.1f, 0f, true, 1);
        var limiter = new WwisePeakLimiter(sfx, 48000f);
        Assert.Equal(WwiseLimiterPlugin.ProcessKind.P2, limiter.Plugin.Process);
        var buffer = new float[600];
        buffer[0] = 0.25f;
        limiter.Process(buffer);
        Assert.Equal(480, limiter.LookAheadSamples);
        Assert.NotEqual(0f, buffer[480]);
    }

    /// <summary>The adapter cuts a span longer than a u16 buffer into engine-sized calls; a span of 70000 frames runs and the result is the same as two calls (the ring and the biquad state persist).</summary>
    [Fact]
    public void ALongSpanIsRunInPiecesOfAtMostU16Frames()
    {
        var a = new WwiseRobotBusFx(48000f);
        var b = new WwiseRobotBusFx(48000f);
        var x = new float[70000];
        for (int i = 0; i < x.Length; i++) x[i] = 0.3f * MathF.Sin(i * 0.05f);
        var one = (float[])x.Clone();
        a.Process(one);
        var two = (float[])x.Clone();
        b.Process(two.AsSpan(0, 0xFFFF));
        b.Process(two.AsSpan(0xFFFF));
        Assert.Equal(one, two);
    }
}
