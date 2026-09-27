using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-011 / gapE 1.1..1.9: the voice filter A/B (Butterworth LPF/HPF biquad, value-to-cutoff map, the
/// eight-chunk ramp and the ≤ 0.1 bypass, and the composition).
///
/// Every expected value below is the row's own arithmetic applied by hand in an independent calculation, not a
/// value read back from <see cref="WwiseVoiceFilter"/> and friends. The addresses are the rows' citations.
/// </summary>
public class WwiseVoiceFilterTests
{
    // Independently computed from gapE 1.6 at fc = 1000 Hz, fs = 48000 Hz.
    private const float LpfB0 = 0.003916126675903797f;
    private const float LpfB1 = 0.007832253351807594f;
    private const float LpfB2 = 0.003916126675903797f;
    private const float LpfNegA1 = 1.8153409957885742f;
    private const float LpfNegA2 = -0.8310055732727051f;

    private const float HpfB0 = 0.9115866422653198f;
    private const float HpfB1 = -1.8231732845306396f;
    private const float HpfB2 = 0.9115866422653198f;

    private static void Close(float expected, float actual, float tolerance = 1e-5f)
    {
        Assert.True(MathF.Abs(expected - actual) <= tolerance,
            $"expected {expected:R}, got {actual:R} (tolerance {tolerance:R})");
    }

    /// <summary>
    /// M6-011 / gapE 1.6 (LPF 0x00A7678C): <c>c = 1/tan(π·fc/fs)</c>, <c>b0 = 1/(1+√2c+c²)</c>,
    /// <c>b1 = 2b0</c>, <c>b2 = b0</c>, <c>−a1 = −2(1−c²)b0</c>, <c>−a2 = −(c²−√2c+1)b0</c>.
    /// </summary>
    [Fact]
    public void TheLowPassBiquadUsesTheButterworthFormulas()
    {
        var c = WwiseVoiceBiquadCoefficients.LowPass(1000f, 48000f);
        Close(LpfB0, c.B0);
        Close(LpfB1, c.B1);
        Close(LpfB2, c.B2);
        Close(LpfNegA1, c.NegA1);
        Close(LpfNegA2, c.NegA2);

        // The row's own relations, independent of the transcendentals.
        Close(2f * c.B0, c.B1);
        Close(c.B0, c.B2);
    }

    /// <summary>
    /// M6-011 / gapE 1.6 (HPF 0x00A77500): <c>c = tan(π·fc/fs)</c>, <c>b0 = 1/(c²+√2c+1)</c>,
    /// <c>b1 = −2b0</c>, <c>b2 = b0</c>, <c>−a1 = −2b0(c²−1)</c>, <c>−a2 = −(c²−√2c+1)b0</c>.
    /// </summary>
    [Fact]
    public void TheHighPassBiquadUsesTheButterworthFormulas()
    {
        var c = WwiseVoiceBiquadCoefficients.HighPass(1000f, 48000f);
        Close(HpfB0, c.B0);
        Close(HpfB1, c.B1);
        Close(HpfB2, c.B2);
        Close(LpfNegA1, c.NegA1);
        Close(LpfNegA2, c.NegA2);

        Close(-2f * c.B0, c.B1);
        Close(c.B0, c.B2);
    }

    /// <summary>
    /// M6-011 / gapE 1.6 (scalar form 0x00A76C50..0x00A76C7C):
    /// <c>y = b2·x2 + x·b0 + b1·x1 + (−a2)·y2 + (−a1)·y1</c>. With the LPF literals above, an impulse of 1
    /// at sample 0 gives y0 = b0, y1 = b1 + (−a1)·b0, and so on.
    /// </summary>
    [Fact]
    public void TheScalarDirectFormOneIsTheRowsDifferenceEquation()
    {
        var biquad = new WwiseVoiceBiquad();
        biquad.SetCoefficients(new WwiseVoiceBiquadCoefficients(
            LpfB0, LpfB1, LpfB2, LpfNegA1, LpfNegA2));

        // Independently evaluated (float32) from the row's equation.
        float[] expected =
        {
            0.003916126675903797f,
            0.01494135893881321f,
            0.027785465121269226f,
            0.03802374377846718f,
            0.045936182141304016f,
            0.05179189145565033f,
        };

        Assert.Equal(expected[0], biquad.Process(1f));
        for (int i = 1; i < expected.Length; i++) Assert.Equal(expected[i], biquad.Process(0f));
    }

    /// <summary>
    /// M6-011 / gapE 1.7 (LPF 0x00A7A3D8): <c>v &lt; 30 → 7000 + (30−v)·433.333344</c>, otherwise the
    /// <c>16.7974434·fastpow2(...)</c> branch, capped at <c>0.45·rate</c>. The inventory's 48 kHz examples:
    /// 15 → 13500, 34 → ≈4949, 49 → ≈1360.
    /// </summary>
    [Fact]
    public void TheLowPassCutoffMapPinsTheShippedExamples()
    {
        Close(13500f, WwiseVoiceFilterCutoff.LowPass(15f, 48000), 0.05f);
        Close(4948.7f, WwiseVoiceFilterCutoff.LowPass(34f, 48000), 1f);
        Close(1360.4f, WwiseVoiceFilterCutoff.LowPass(49f, 48000), 1f);
    }

    /// <summary>
    /// M6-011 / gapE 1.7: the cap is <c>min(fc, 0.45·rate)</c>. value 0 gives 20000 Hz, above the 14400 Hz cap
    /// at a 32 kHz rate.
    /// </summary>
    [Fact]
    public void TheCutoffMapCapsAtFortyFivePercentOfTheRate()
    {
        Close(14400f, WwiseVoiceFilterCutoff.LowPass(0f, 32000));
    }

    /// <summary>
    /// M6-011 / gapE 1.7 (HPF 0x00A7A4AC): the same map applied to <c>(100−v)</c>. The inventory's 48 kHz
    /// example HPF 15 → ≈61 agrees with the formula.
    ///
    /// The inventory's other HPF example, "40 → ≈445", is <b>not</b> pinned: the stated formula on <c>(100−40)</c>
    /// gives ≈527, and no input reproduces both examples under this map. That contradiction is reported as a
    /// MISSING rather than silently resolved here.
    /// </summary>
    [Fact]
    public void TheHighPassCutoffMapUsesTheComplement()
    {
        Close(61.24f, WwiseVoiceFilterCutoff.HighPass(15f, 48000), 1f);
    }

    /// <summary>
    /// M6-011 / gapE 1.8: the chunk is <c>N = floor(rate·128/48000)</c>, 128 at the 48 kHz policy rate and 256
    /// at 96 kHz.
    /// </summary>
    [Fact]
    public void TheRampChunkIsOneTwentyEightAtFortyEightK()
    {
        Assert.Equal(128, new WwiseVoiceFilterBand(WwiseVoiceFilterKind.LowPass).ChunkSamples);
        Assert.Equal(256, new WwiseVoiceFilterBand(WwiseVoiceFilterKind.LowPass, 96000).ChunkSamples);
    }

    /// <summary>
    /// M6-011 / gapE 1.8: the first apply sets <c>current = target</c> with no ramp, so a target set before the
    /// first buffer is in force immediately and <c>steps</c> is left at its ctor value 8.
    /// </summary>
    [Fact]
    public void TheFirstApplySetsCurrentWithoutRamping()
    {
        var band = new WwiseVoiceFilterBand(WwiseVoiceFilterKind.LowPass);
        band.SetTarget(50f);
        band.Process(new float[1024]);

        Assert.Equal(50f, band.Current);
        Assert.Equal(50f, band.Target);
        Assert.Equal(8, band.Steps);
        Assert.False(band.Bypassed);
    }

    /// <summary>
    /// M6-011 / gapE 1.8: on a change <c>steps = 0</c>; each N-sample chunk takes one step and the coefficients
    /// are recomputed from <c>current + steps/8·(target−current)</c>; after eight chunks <c>current = target</c>.
    /// </summary>
    [Fact]
    public void TheRampTakesOneStepPerChunkAndFinishesAtTheTarget()
    {
        var band = new WwiseVoiceFilterBand(WwiseVoiceFilterKind.LowPass);
        band.SetTarget(50f);
        band.Process(new float[1024]);           // first apply: current = 50
        band.SetTarget(100f);

        band.Process(new float[128]);            // chunk 1
        Assert.Equal(1, band.Steps);
        Assert.Equal(50f, band.Current);         // current only moves when the eight chunks complete
        Close(56.25f, 50f + 1f / 8f * (100f - 50f));   // the chunk-1 interpolated value

        for (int i = 0; i < 7; i++) band.Process(new float[128]);   // chunks 2..8
        Assert.Equal(8, band.Steps);
        Assert.Equal(100f, band.Current);
        Assert.Equal(100f, band.Target);
    }

    /// <summary>
    /// M6-011 / gapE 1.4: the target setter folds the instantaneous value in with
    /// <c>current += (oldTarget−current)·0.125·steps</c> before taking the new target. At <c>steps = 8</c> the
    /// factor is 1, so <c>current</c> jumps to the old target.
    /// </summary>
    [Fact]
    public void TheTargetSetterFoldsTheOldTargetIn()
    {
        var band = new WwiseVoiceFilterBand(WwiseVoiceFilterKind.LowPass);
        band.SetTarget(40f);
        band.Process(new float[1024]);           // current = 40, steps = 8
        band.SetTarget(80f);

        // 40 + (40−40)·1 = 40; the ramp has not started, so Current is still the old target.
        Assert.Equal(40f, band.Current);
        Assert.Equal(80f, band.Target);
    }

    /// <summary>
    /// M6-011 / gapE 1.8: a target ≤ 0.1 means bypassed, but a ramp down to it keeps the filter running four
    /// more buffers (<c>+0xA = 4</c>) before bypassing.
    /// </summary>
    [Fact]
    public void ARampDownToPointOneFiltersFiveBuffersThenBypasses()
    {
        var band = new WwiseVoiceFilterBand(WwiseVoiceFilterKind.LowPass);
        band.SetTarget(50f);
        band.Process(new float[1024]);           // running
        Assert.False(band.Bypassed);

        band.SetTarget(0.05f);
        band.Process(new float[1024]);           // the ramp buffer: filtered, +0xA armed to 4 at completion
        Assert.False(band.Bypassed);
        Assert.Equal(4, band.Countdown);

        for (int i = 0; i < 4; i++)              // four more buffers
        {
            band.Process(new float[1024]);
            Assert.False(band.Bypassed);
        }
        Assert.Equal(0, band.Countdown);

        band.Process(new float[1024]);           // the sixth: bypassed
        Assert.True(band.Bypassed);
    }

    /// <summary>
    /// M6-011 / gapE 1.8 (0x00A769EC..0x00A76A2C): a target change while <c>current</c> and the new target are
    /// both <c>≤ 0.1</c> bypasses at once — <c>steps = 8</c>, <c>current = target</c>, the history copied, and
    /// nothing filtered. Here the band is mid-countdown at <c>current = 0.05</c> (after a ramp from 50), so a
    /// change to 0.02 must not start another ramp nor filter any buffer.
    /// </summary>
    [Fact]
    public void ATargetChangeBetweenTwoSubPointOneValuesBypassesImmediately()
    {
        var band = new WwiseVoiceFilterBand(WwiseVoiceFilterKind.LowPass);
        band.SetTarget(50f);
        band.Process(new float[1024]);           // running, current = 50
        Assert.False(band.Bypassed);

        band.SetTarget(0.05f);
        band.Process(new float[1024]);           // ramp buffer: current = 0.05, countdown armed to 4
        Assert.False(band.Bypassed);
        Assert.Equal(0.05f, band.Current);

        band.SetTarget(0.02f);
        var buffer = new float[] { 5f, 6f, 7f, 8f };
        band.Process(buffer);

        Assert.True(band.Bypassed);
        Assert.Equal(0.02f, band.Current);
        Assert.Equal(8, band.Steps);
        Assert.Equal(0, band.Countdown);
        Assert.Equal(buffer, new float[] { 5f, 6f, 7f, 8f });   // not filtered
        var h = band.Biquad.History;
        Assert.Equal((8f, 7f, 8f, 7f), (h.X1, h.X2, h.Y1, h.Y2));
    }

    /// <summary>
    /// M6-011 / gapE 1.8: while bypassed, the last two samples are still written into all four history slots
    /// (0x00A76DB4/0x00A76DBC/0x00A76DC4/0x00A76DCC): <c>x1 = y1 = last</c>, <c>x2 = y2 = second-last</c>.
    /// </summary>
    [Fact]
    public void ABypassedFilterStillCopiesTheLastTwoSamplesIntoAllFourHistorySlots()
    {
        var band = new WwiseVoiceFilterBand(WwiseVoiceFilterKind.LowPass);
        band.SetTarget(0.05f);
        band.Process(new float[1024]);
        Assert.True(band.Bypassed);

        var buffer = new float[] { 1f, 2f, 3f, 4f };
        band.Process(buffer);

        var h = band.Biquad.History;
        Assert.Equal((4f, 3f, 4f, 3f), (h.X1, h.X2, h.Y1, h.Y2));   // x1=y1=last, x2=y2=second-last
        Assert.Equal(buffer, new float[] { 1f, 2f, 3f, 4f });   // bypassed: not filtered
    }

    /// <summary>
    /// M6-011 / gapE 1.1: the effective LPF/HPF is node-chain + randomizer (+ pbi+0xA0/+0xA8, zero in the
    /// shipped data). gapE 1.2: A is the minimum over the connections, starting at 100.
    /// </summary>
    [Fact]
    public void TheCompositionSumsTheNodeChainAndRandomizerAndStartsAtOneHundred()
    {
        var none = WwiseVoiceFilterComposer.Compose(0f, 0f, 0f, 0f, Array.Empty<WwiseVoiceFilterConnection>());
        Assert.Equal(100f, none.LowPassA);
        Assert.Equal(100f, none.HighPassA);

        var twoD = new[] { new WwiseVoiceFilterConnection(Is3D: false) };
        var composed = WwiseVoiceFilterComposer.Compose(15f, 0f, 0f, 0f, twoD);
        Assert.Equal(15f, composed.LowPassA);
        Assert.Equal(0f, composed.HighPassA);

        // The randomizer draw is added to the node-chain sum (0x9FFD14..0x9FFD74).
        var withRandom = WwiseVoiceFilterComposer.Compose(15f, 0f, 5f, 2f, twoD);
        Assert.Equal(20f, withRandom.LowPassA);
        Assert.Equal(2f, withRandom.HighPassA);
    }

    /// <summary>
    /// M6-011 / gapE 1.2: the 3D path takes <c>max(ctx, attenuation)</c>
    /// (0x00A5C740..0x00A5C774). The attenuation's own source is outside this record, so it is supplied by the
    /// caller here.
    /// </summary>
    [Fact]
    public void TheThreeDConnectionTakesTheGreaterOfContextAndAttenuation()
    {
        var threeD = new[] { new WwiseVoiceFilterConnection(Is3D: true, AttenuationLpf: 40f, AttenuationHpf: 10f) };
        var composed = WwiseVoiceFilterComposer.Compose(15f, 20f, 0f, 0f, threeD);
        Assert.Equal(40f, composed.LowPassA);    // max(15, 40)
        Assert.Equal(20f, composed.HighPassA);   // max(20, 10)
    }

    /// <summary>
    /// M6-011 / gapE 1.2, 1.3: A is the minimum over connections; B is <c>max(min(B), outputBus)</c> and is
    /// clamped to 0..100.
    /// </summary>
    [Fact]
    public void TheCompositionTakesTheMinimumAndClampsTheOutputBus()
    {
        var connections = new[]
        {
            new WwiseVoiceFilterConnection(Is3D: true, AttenuationLpf: 40f, AttenuationHpf: 0f),
            new WwiseVoiceFilterConnection(Is3D: false),
        };
        var composed = WwiseVoiceFilterComposer.Compose(15f, 30f, 0f, 0f, connections,
            outputBusLpf: 20f, outputBusHpf: 200f);
        Assert.Equal(15f, composed.LowPassA);    // min(max(15,40), 15)
        Assert.Equal(30f, composed.HighPassA);   // min(max(30,0), 30)
        Assert.Equal(20f, composed.LowPassB);    // max(0, 20)
        Assert.Equal(100f, composed.HighPassB);  // max(0, 200) clamped
    }

    /// <summary>
    /// M6-011 / gapE 1.3: filter B is bypassed for every shipped sound. A default filter (target 0) is bypassed
    /// on its first buffer and leaves the signal alone.
    /// </summary>
    [Fact]
    public void TheDefaultFilterIsBypassedAndLeavesTheSignalAlone()
    {
        var filterA = new WwiseVoiceFilter(WwiseVoiceFilterRole.A);
        var buffer = new float[] { 0.25f, -0.5f, 0.75f, -1f };
        var original = (float[])buffer.Clone();

        filterA.Process(buffer);

        Assert.True(filterA.LowPass.Bypassed);
        Assert.True(filterA.HighPass.Bypassed);
        Assert.Equal(original, buffer);
    }
}
