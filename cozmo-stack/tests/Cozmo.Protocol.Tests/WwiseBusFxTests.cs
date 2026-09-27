using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-013 / gapC 3.1, 4.1..4.9 and the addendum: the Robot_Bus FX chain — EQ <c>0x6767FC1F</c>, EQ
/// <c>0x174901C6</c>, Peak Limiter <c>0xDF2230FF</c> and the Hijack, in slot order while bus state is 1.
///
/// Every expected value below is the row's own arithmetic evaluated independently (by hand / a separate
/// calculator), never a value read back from <see cref="WwiseRobotBusFx"/> and friends. The addresses are the
/// rows' citations.
/// </summary>
public class WwiseBusFxTests
{
    private static void Close(float expected, float actual, float tolerance = 1e-5f)
    {
        Assert.True(MathF.Abs(expected - actual) <= tolerance,
            $"expected {expected:R}, got {actual:R} (tolerance {tolerance:R})");
    }

    // ------------------------------------------------------------------ chain order

    /// <summary>
    /// M6-013 / gapC 3.1, addendum: Robot_Bus_1 runs slot 0 EQ <c>0x6767FC1F</c>, slot 1 EQ
    /// <c>0x174901C6</c>, slot 2 Peak Limiter <c>0xDF2230FF</c>, slot 3 the Hijack.
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
    /// M6-013 / gapC 3.1 / D2.4: the FX loop runs slots only when the bus state is 1. With state 0 the buffer
    /// is untouched; with state 1 the chain runs (the limiter's L-sample delay makes sample 0 zero).
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

    /// <summary>M6-013 / gapC 4.2: the shipped ShareSet settings, exactly as the row lists them.</summary>
    [Fact]
    public void TheShippedEqShareSetsMatchTheRowsSettings()
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

        var eq2 = WwiseEqSettings.Eq2;
        Assert.Equal(WwiseEqFilterType.HighPass, eq2.Bands[0].FilterType);
        Assert.Equal(333f, eq2.Bands[0].Frequency);
        Assert.True(eq2.Bands[0].On);
        Assert.Equal(WwiseEqFilterType.Peaking, eq2.Bands[1].FilterType);
        Assert.Equal(-4.5f, eq2.Bands[1].GainDb);
        Assert.Equal(1000f, eq2.Bands[1].Frequency);
        Assert.Equal(0.5f, eq2.Bands[1].Q);
        Assert.False(eq2.Bands[1].On);            // the row's "off"
        Assert.Equal(WwiseEqFilterType.LowPass, eq2.Bands[2].FilterType);
        Assert.Equal(14298f, eq2.Bands[2].Frequency);
        Assert.True(eq2.Bands[2].On);
        Assert.Equal(0f, eq2.OutputLevelDb);
    }

    // ------------------------------------------------------------------ EQ coefficients

    /// <summary>
    /// M6-013 / gapC 4.3: peaking (type 6) with <c>w0 = 2π fc/fs</c>, <c>α = sin/(2Q)</c>,
    /// <c>A = 10^(gain/40)</c>. Independently computed for the shipped band 2 of <c>0x6767FC1F</c>:
    /// −2.5 dB @1359 Hz Q 4.2 at fs = 48000 (fc is below the 0.45·fs cap).
    /// </summary>
    [Fact]
    public void ThePeakingEqCoefficientsMatchTheRowFormulas()
    {
        var band = new WwiseEqBand((uint)WwiseEqFilterType.Peaking, -2.5f, 1359f, 4.2f, true);
        var c = WwiseEqCoefficients.Design(band, 48000f);

        Close(0.9940602f, c.B0, 1e-4f);
        Close(-1.9216889f, c.B1, 1e-4f);
        Close(0.9584416f, c.B2, 1e-4f);
        Close(1.9216889f, c.NegA1, 1e-4f);
        Close(-0.9525018f, c.NegA2, 1e-4f);
    }

    /// <summary>
    /// M6-013 / gapC 4.3: RBJ low shelf (type 4) with S = 1 and the row's <c>α = sin·√2/2</c>. Independently
    /// computed for the shipped band 1 of <c>0x6767FC1F</c>: +2.0 dB @835 Hz at fs = 48000.
    /// </summary>
    [Fact]
    public void TheLowShelfEqCoefficientsMatchTheRowFormulas()
    {
        var band = new WwiseEqBand((uint)WwiseEqFilterType.LowShelf, 2.0f, 835f, 2.1f, true);
        var c = WwiseEqCoefficients.Design(band, 48000f);

        Close(1.0089267f, c.B0, 1e-4f);
        Close(-1.8530042f, c.B1, 1e-4f);
        Close(0.8565635f, c.B2, 1e-4f);
        Close(1.8542882f, c.NegA1, 1e-4f);
        Close(-0.8642062f, c.NegA2, 1e-4f);
    }

    /// <summary>
    /// M6-013 / gapE 1.6 + gapC 4.3: Butterworth low pass (type 0) — <c>c = 1/tan(π fc/fs)</c>,
    /// <c>b0 = 1/(1+√2c+c²)</c>, <c>b1 = 2b0</c>, <c>b2 = b0</c>, and the <b>stored</b>
    /// <c>−a1 = −2(1−c²)b0</c> and <c>−a2 = −(c²−√2c+1)b0</c> (gapC 4.4 multiplies <c>y1</c> by the stored
    /// <c>−a1</c> directly). At fc = 1000, fs = 48000.
    /// </summary>
    [Fact]
    public void TheButterworthLowPassUsesTheRowFormulas()
    {
        var band = new WwiseEqBand((uint)WwiseEqFilterType.LowPass, 0f, 1000f, 0f, true);
        var c = WwiseEqCoefficients.Design(band, 48000f);

        Close(0.003916127f, c.B0, 1e-4f);
        Close(0.007832253f, c.B1, 1e-4f);
        Close(0.003916127f, c.B2, 1e-4f);
        Close(1.8153411f, c.NegA1, 1e-4f);
        Close(-0.8310056f, c.NegA2, 1e-4f);
    }

    /// <summary>
    /// M6-013 / gapE 1.6 + gapC 4.3: Butterworth high pass (type 1) — <c>c = tan(π fc/fs)</c>,
    /// <c>b0 = 1/(1+√2c+c²)</c>, <c>b1 = −2b0</c>, <c>b2 = b0</c>, and the <b>stored</b>
    /// <c>−a1 = −2b0(c²−1)</c>, <c>−a2 = −(c²−√2c+1)b0</c>. This is the shipped band 1 of
    /// <c>0x174901C6</c> (333 Hz, on): the row's stored signs give a stable pole (|z| &lt; 1), where the
    /// negated pair would not.
    /// </summary>
    [Fact]
    public void TheButterworthHighPassUsesTheRowFormulas()
    {
        var band = new WwiseEqBand((uint)WwiseEqFilterType.HighPass, 0f, 333f, 0f, true);
        var c = WwiseEqCoefficients.Design(band, 48000f);
        Assert.True(band.On);

        Close(0.9696476f, c.B0, 1e-4f);
        Close(-1.9392951f, c.B1, 1e-4f);
        Close(0.9696476f, c.B2, 1e-4f);
        Close(1.9383736f, c.NegA1, 1e-4f);
        Close(-0.9402166f, c.NegA2, 1e-4f);

        // gapC 4.4: the difference equation multiplies y1 by the stored −a1 and y2 by the stored −a2, so the
        // denominator is z² + (stored −a1)z + (stored −a2). |z| = sqrt(stored −a2) = 0.96965 < 1, stable.
        float a1 = -c.NegA1;
        float a2 = -c.NegA2;
        Assert.True(c.NegA2 < 0f && c.NegA2 > -1f);
        float poleMagnitude = MathF.Sqrt(a2);   // complex poles, so |z|² = a2
        Close(0.9696477f, poleMagnitude, 1e-4f);
        Assert.True(a1 * a1 - 4f * a2 < 0f, "the shipped HP poles are a complex pair");
    }

    /// <summary>M6-013 / gapC 4.3: <c>fc = min(freq, 0.45·fs)</c>, so a 30 kHz band at 48 kHz is the 21.6 kHz band.</summary>
    [Fact]
    public void TheEqFrequencyIsCappedAtFortyFivePercentOfTheRate()
    {
        var high = WwiseEqCoefficients.Design(
            new WwiseEqBand((uint)WwiseEqFilterType.Peaking, -4f, 30000f, 1.5f, true), 48000f);
        var capped = WwiseEqCoefficients.Design(
            new WwiseEqBand((uint)WwiseEqFilterType.Peaking, -4f, 0.45f * 48000f, 1.5f, true), 48000f);

        Close(capped.B0, high.B0);
        Close(capped.B1, high.B1);
        Close(capped.B2, high.B2);
        Close(capped.NegA1, high.NegA1);
        Close(capped.NegA2, high.NegA2);
        Assert.Equal(0.45f, WwiseEqCoefficients.NyquistFraction);
    }

    // ------------------------------------------------------------------ EQ execute

    /// <summary>
    /// M6-013 / gapC 4.4: only a band that is on runs. A custom block with bands 0 and 2 off and band 1 on
    /// leaves the off bands' history untouched and drives the on band's.
    /// </summary>
    [Fact]
    public void TheEqAppliesOnlyTheBandsThatAreOn()
    {
        var settings = WwiseEqSettings.For(0, new[]
        {
            new WwiseEqBand((uint)WwiseEqFilterType.Peaking, -2.5f, 1359f, 4.2f, false),
            new WwiseEqBand((uint)WwiseEqFilterType.Peaking, -2.5f, 1359f, 4.2f, true),
            new WwiseEqBand((uint)WwiseEqFilterType.Peaking, -2.5f, 1359f, 4.2f, false),
        }, 0f);

        var eq = new WwiseParametricEq(settings, 48000f);
        var buffer = new float[] { 1f, 0f, 0f, 0f };
        eq.Process(buffer);

        Assert.Equal((0f, 0f, 0f, 0f), eq.BandHistory(0));
        Assert.Equal((0f, 0f, 0f, 0f), eq.BandHistory(2));
        Assert.NotEqual(0f, eq.BandHistory(1).Y1);
    }

    /// <summary>
    /// M6-013 / gapC 4.4: the output-gain ramp. The native ramps in NEON 4-sample blocks, then its scalar tail
    /// <b>restarts</b> the ramp from the previous gain. Init puts the previous gain at the target, so this
    /// exercises the internal ramp with prev = 1.0 and target = 1.2 over six samples: the first four continue
    /// the ramp, the last two start over from prev.
    /// </summary>
    [Fact]
    public void TheEqOutputGainRampRestartsWithItsScalarTail()
    {
        var ramp = new WwiseOutputGainRamp();
        ramp.Init(1.0f);
        var buffer = new float[6];
        Array.Fill(buffer, 1f);
        ramp.Apply(buffer, 1.2f);

        Close(1.0333333f, buffer[0]);
        Close(1.0666667f, buffer[1]);
        Close(1.1f, buffer[2]);
        Close(1.1333333f, buffer[3]);
        Close(1.0333333f, buffer[4]);   // restart from prev, not the continuing 1.1666
        Close(1.0666667f, buffer[5]);
        Close(1.2f, ramp.PreviousGain);
    }

    /// <summary>
    /// M6-013 / gapC 4.4: when the target equals the previous gain the gain is applied flat, and it is skipped
    /// when it is 1.0.
    /// </summary>
    [Fact]
    public void TheEqOutputGainIsFlatWhenTheTargetMatches()
    {
        var doubled = new WwiseOutputGainRamp();
        doubled.Init(2f);
        var buffer = new float[] { 1f, 0.5f, -0.25f };
        doubled.Apply(buffer, 2f);
        Assert.Equal(2f, buffer[0]);
        Assert.Equal(1f, buffer[1]);
        Assert.Equal(-0.5f, buffer[2]);

        var unity = new WwiseOutputGainRamp();
        unity.Init(1f);
        var untouched = new float[] { 1f, 0.5f, -0.25f };
        unity.Apply(untouched, 1f);
        Assert.Equal(1f, untouched[0]);
        Assert.Equal(0.5f, untouched[1]);
        Assert.Equal(-0.25f, untouched[2]);
    }

    // ------------------------------------------------------------------ limiter values

    /// <summary>
    /// M6-013 / gapC 4.5: the shipped ShareSet <c>0xDF2230FF</c> and the plug-in defaults.
    /// </summary>
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
    /// M6-013 / gapC 4.6: <c>L = u32(float(sr)·lookahead)</c>. At 48 kHz the float product truncates to
    /// <b>431</b>, not 432. <c>attack = expf(−2.2/(L/2))</c> and <c>release = expf(−2.2/(sr·release))</c>.
    /// </summary>
    [Fact]
    public void TheLimiterLookaheadTruncatesTo431At48k()
    {
        var limiter = new WwisePeakLimiter(WwisePeakLimiterSettings.RobotBus1, 48000f);
        Assert.Equal(431, limiter.LookAheadSamples);
        Close(0.9898431164f, limiter.Attack, 1e-6f);
        Close(0.9988827384f, limiter.Release, 1e-6f);
    }

    /// <summary>
    /// M6-013 / gapC 4.7: the row's fast log — <c>(e−127)ln2 + 2s(1+s²/3)</c>, <c>s = (m−1)/(m+1)</c>,
    /// then <c>×log10 e</c>. Exact at powers of two, approximate elsewhere.
    /// </summary>
    [Fact]
    public void TheFastLogMatchesTheRowsExpansion()
    {
        Assert.Equal(0f, WwisePeakLimiter.FastLog10(1f));
        Close(0.30103f, WwisePeakLimiter.FastLog10(2f), 1e-6f);
        Close(-0.30103f, WwisePeakLimiter.FastLog10(0.5f), 1e-6f);
        Close(0.999997f, WwisePeakLimiter.FastLog10(10f), 1e-5f);
        Close(2.0f, WwisePeakLimiter.FastLog10(100f), 1e-3f);
    }

    /// <summary>
    /// M6-013 / gapC 4.7: the limiter delays the signal by <c>L</c>. An impulse well below the −1 dB threshold
    /// is not limited (gain 1), so it appears at sample 431 unchanged.
    /// </summary>
    [Fact]
    public void TheLimiterDelaysTheSignalByTheLookahead()
    {
        var limiter = new WwisePeakLimiter(WwisePeakLimiterSettings.RobotBus1, 48000f);
        var buffer = new float[500];
        buffer[0] = 0.5f;                       // 0.5 < 10^(−1/20) = 0.891, so no reduction
        limiter.Process(buffer);

        Assert.Equal(0f, buffer[0]);
        Assert.Equal(0f, buffer[430]);
        // The engine's fast pow is 0.999039 at 0 dB (M6-010/gapC 1.11), so the detector gain and the
        // 0 dB output gain each carry that factor: 0.5 · 0.999039².
        Close(0.49903946f, buffer[431], 1e-5f);
        Close(0.999039f, limiter.Gain, 1e-5f);
    }

    /// <summary>
    /// M6-013 / gapC 4.7: a full-scale impulse is pushed back; the delayed sample at <c>L</c> is attenuated.
    /// The exact figure is the row's detector evaluated by the rows' own formulas; only a bound is pinned here
    /// because the fast log and fast pow are approximations (MD3).
    /// </summary>
    [Fact]
    public void TheLimiterReducesAFullScaleImpulse()
    {
        var limiter = new WwisePeakLimiter(WwisePeakLimiterSettings.RobotBus1, 48000f);
        var buffer = new float[432];
        buffer[0] = 1.0f;                       // 0 dB, 1 dB over the −1 dB threshold
        limiter.Process(buffer);

        Assert.InRange(buffer[431], 0.85f, 0.95f);
        Assert.True(buffer[431] < 1.0f);
    }

    /// <summary>
    /// M6-013 / gapC 4.7: NoMoreData extends the output with the L-sample tail still in the delay line. Three
    /// samples in, the tail is 428 zeros then the three inputs, and the drain reports completion.
    /// </summary>
    [Fact]
    public void TheLimiterTailIsTheDelayedSamples()
    {
        var limiter = new WwisePeakLimiter(WwisePeakLimiterSettings.RobotBus1, 48000f);
        var buffer = new float[] { 0.1f, 0.2f, 0.3f };
        limiter.Process(buffer);
        Assert.Equal(0f, buffer[0]);

        var tail = new float[431];
        int result = limiter.DrainTail(tail);

        Assert.Equal(0, result);
        for (int i = 0; i < 428; i++) Assert.Equal(0f, tail[i]);
        // Each tail sample passes the last detector gain and the 0 dB output gain: the M6-010 fast pow's
        // 0.999039 at 0 dB, squared.
        Close(0.09980789f, tail[428], 1e-5f);
        Close(0.19961578f, tail[429], 1e-5f);
        Close(0.29942368f, tail[430], 1e-5f);
        Assert.Equal(WwisePeakLimiter.TailRemaining, 0x2D);
        Assert.Equal(WwisePeakLimiter.NoMoreData, 0x11);
    }

    /// <summary>
    /// M6-013 / gapC 4.6: only the unlinked/mono process path (<c>0xAA0EB4</c>) is built; the linked detector
    /// (<c>0xAA1464</c>) has no arithmetic in the rows, so it is refused rather than guessed.
    /// </summary>
    [Fact]
    public void TheLimiterRefusesTheLinkedDetector()
    {
        var linked = WwisePeakLimiterSettings.RobotBus1 with { ChannelLink = 1 };
        Assert.Throws<NotSupportedException>(() => new WwisePeakLimiter(linked, 48000f));
    }
}