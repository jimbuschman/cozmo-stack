using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-004 / the rows M6 0.11 and gapB P1, P2, P8 and gapE 3.5, 3.6: Wwise's <c>CAkResampler</c>.
///
/// Every expected value below is the row's own arithmetic applied by hand, not a value read back from
/// <see cref="WwiseResampler"/>. The addresses are the rows' native citations.
/// </summary>
public class WwiseResamplerTests
{
    private static WwiseResamplerFormat Int16Mono(int rate) => new(WwiseResampler.FormatInt16, 1, rate);
    private static WwiseResamplerFormat FloatMono(int rate) => new(WwiseResampler.FormatFloat, 1, rate);

    /// <summary>
    /// M6-004 / gapB P2 (0x00A4913C): the int16 mono interpolating kernel is linear interpolation in Q16,
    /// <c>out = (float)((x0&lt;&lt;16) + (x1−x0)·frac16)·2^−31</c>. With x0=0, x1=32767 and frac=0x8000 that is
    /// 32767·32768/2^31 = 32767/65536.
    /// </summary>
    [Fact]
    public void TheInt16KernelInterpolatesInQ16()
    {
        float mid = WwiseResampler.InterpolateInt16(0, 32767, 0x8000);
        Assert.Equal(32767f / 65536f, mid);                 // 32767·2^15 / 2^31, exact in binary
        Assert.Equal(0f, WwiseResampler.InterpolateInt16(0, 32767, 0));
        Assert.Equal(32767f / 131072f, WwiseResampler.InterpolateInt16(0, 32767, 0x4000));
    }

    /// <summary>
    /// M6-004 / gapB P2 (0x00A48F5C, constant 0x38000000 = 1/32768): the bypass kernel scales int16 to float.
    /// </summary>
    [Fact]
    public void TheBypassKernelScalesByOneOver32768()
    {
        Assert.Equal(32767f / 32768f, WwiseResampler.BypassInt16(32767));
        Assert.Equal(-1f, WwiseResampler.BypassInt16(short.MinValue));
        Assert.Equal(1f / 32768f, WwiseResampler.BypassInt16(1));
    }

    /// <summary>
    /// M6-004 / gapE 3.5 (0x00A473E0..0x00A4741C): <c>step = u32(ratio·2^(cents/1200)·65536 + 0.5)</c>. One
    /// octave doubles the unity step 0x10000.
    /// </summary>
    [Fact]
    public void ThePitchStepDoublesOverAnOctave()
    {
        Assert.Equal(0x10000u, WwiseResampler.ComputeStep(1f, 0));
        Assert.Equal(0x20000u, WwiseResampler.ComputeStep(1f, 1200));
    }

    /// <summary>
    /// M6-004 / gapB P8: the Hijack's 48 kHz to 22,320 Hz step is <c>round(float(mix/22320)·65536)</c>, which
    /// the row gives as about 140938.
    /// </summary>
    [Fact]
    public void TheHijackStepIsTheMixToRobotRatio()
    {
        Assert.Equal(140938u, WwiseResampler.ComputeStep(48000f / 22320f, 0));

        var r = new WwiseResampler();
        r.Init(FloatMono(48000), 22320);
        r.SetPitch(0);
        Assert.Equal(140938u, r.TargetStep);
        // +0x3C is __aeabi_uidiv(48000, outRate), an integer division (0xA470B0): 48000/22320 = 2, not 2.15.
        Assert.Equal(2, r.StepScale);
    }

    /// <summary>
    /// M6-004 / gapE 3.5: a step that rounds to zero becomes <c>cents &gt; 0 ? 0xFFFFFFFF : 1</c>.
    /// </summary>
    [Fact]
    public void AZeroStepSaturatesBySignOfCents()
    {
        // ratio 5e-10: ratio·65536 = 3.3e-5, far below the 0.5 that rounds to 1
        var down = new WwiseResampler();
        down.Init(Int16Mono(1), 2_000_000_000);
        down.SetPitch(0);
        Assert.Equal(1u, down.TargetStep);

        var up = new WwiseResampler();
        up.Init(Int16Mono(1), 2_000_000_000);
        up.SetPitch(100);
        Assert.Equal(0xFFFFFFFFu, up.TargetStep);
    }

    /// <summary>
    /// M6-004 / M6 0.11 (0x00A49E40): the mono float kernel is
    /// <c>out = prev + (phase &amp; 0xFFFF)/65536·(next − prev)</c>.
    /// </summary>
    [Fact]
    public void TheMonoFloatKernelInterpolatesAtThePhase()
    {
        Assert.Equal(4f, WwiseResampler.InterpolateFloat(2f, 6f, 0x8000));
        Assert.Equal(3f, WwiseResampler.InterpolateFloat(2f, 6f, 0x4000));
        Assert.Equal(2f, WwiseResampler.InterpolateFloat(2f, 6f, 0));
    }

    /// <summary>
    /// M6-004 / gapB P1: the row table <c>[0,1,2,2 | 4,5,6,6]</c> indexed by <c>(format, channels−1)</c> plus
    /// <c>mode·8</c> selects the kernel. Mono int16 bypass is row 0; mono float fixed pitch is row 4+8 = 12;
    /// mono int16 ramp is row 0+16 = 16 (the row names table 0x0103C0B8[16] = 0xA4A2D8).
    /// </summary>
    [Fact]
    public void TheKernelRowIsTableByFormatAndChannels()
    {
        var int16 = new WwiseResampler();
        int16.Init(Int16Mono(48000), 48000);
        int16.SetPitch(0);
        Assert.Equal(0, int16.Mode);            // step == 0x10000 -> bypass
        Assert.Equal(0, int16.KernelIndex);

        var flt = new WwiseResampler();
        flt.Init(FloatMono(48000), 22320);
        flt.SetPitch(0);
        Assert.Equal(1, flt.Mode);              // fixed pitch
        Assert.Equal(12, flt.KernelIndex);      // float mono (4) + mode 1 (8)

        var ramp = new WwiseResampler();
        ramp.Init(Int16Mono(48000), 48000);
        ramp.SetPitch(0);
        ramp.SetPitch(1200);
        Assert.Equal(2, ramp.Mode);
        Assert.Equal(16, ramp.KernelIndex);     // int16 mono (0) + mode 2 (16)
    }

    /// <summary>
    /// M6-004 / gapB P1: the format field is <c>fmt &amp; 0x3F</c>, 16 or 32, and the channel count is 1..4.
    /// Anything else has no row and is refused rather than defaulted.
    /// </summary>
    [Fact]
    public void AnUnreadKernelOrFormatIsRefusedNotDefaulted()
    {
        var bad = new WwiseResampler();
        Assert.Throws<NotSupportedException>(() => bad.Init(new WwiseResamplerFormat(99, 1, 48000), 48000));
        Assert.Throws<NotSupportedException>(() => bad.Init(new WwiseResamplerFormat(WwiseResampler.FormatInt16, 5, 48000), 48000));

        // The stereo int16 kernel 0x00A49634 was not read (gapB P2): Init accepts the row, Execute refuses.
        var stereo = new WwiseResampler();
        stereo.Init(new WwiseResamplerFormat(WwiseResampler.FormatInt16, 2, 44100), 48000);
        stereo.SetPitch(0);
        Assert.Throws<NotSupportedException>(() => stereo.Execute(new short[16], new float[16], 16));
    }

    /// <summary>
    /// M6-004 / M6 0.11: <c>Execute</c> returns 45 (DataReady) when it reaches <c>+0x40</c> output frames and
    /// 43 (DataNeeded) when the input runs out first. With ratio 1 the bypass mode reproduces the input from
    /// input[0]: N outputs need N input samples, and the interpolating modes need one more for the next sample.
    /// </summary>
    [Fact]
    public void ExecuteReportsDataReadyAtTheTargetAndDataNeededOtherwise()
    {
        var r = new WwiseResampler();
        r.Init(Int16Mono(48000), 48000);
        r.SetPitch(0);

        short[] input = [1000, 2000, 3000, 4000, 5000, 6000];
        var output = new float[4];
        Assert.Equal(WwiseResampler.DataReady, r.Execute(input, output, 4));
        Assert.Equal(new[] { 1000f, 2000f, 3000f, 4000f }, output.Select(s => s * 32768f).ToArray());

        var short2 = new WwiseResampler();
        short2.Init(Int16Mono(48000), 48000);
        short2.SetPitch(0);
        var small = new float[8];
        Assert.Equal(WwiseResampler.DataNeeded, short2.Execute([1000, 2000, 3000], small, 8));
        Assert.Equal(1000f / 32768f, small[0]);      // input[0], and three samples give three outputs
    }

    /// <summary>
    /// M6-004 / the ctor 0x00A46D70 stores <c>+0x2C = 0x10000</c> (0x00A46D80..0x00A46D88), but the native
    /// index is <c>(phase&gt;&gt;16)-1</c> (0x00A49F7C: r7=input-4, r4=phase&gt;&gt;16=1 -> input[0]), so the first
    /// output is still input[0] and no sample is skipped.
    /// </summary>
    [Fact]
    public void TheInitialPhaseIsOneSampleIn()
    {
        Assert.Equal(0x10000, WwiseResampler.InitialPhase);

        var r = new WwiseResampler();
        r.Init(Int16Mono(48000), 48000);
        r.SetPitch(0);
        var output = new float[1];
        Assert.Equal(WwiseResampler.DataReady, r.Execute([1000, 2000, 3000], output, 1));
        Assert.Equal(1000f / 32768f, output[0]);     // 0x10000 maps to input[0], not input[1]
    }

    /// <summary>
    /// M6-004 / M6 0.11 (0x00A49E40): the float kernel steps by the pitch step and interpolates the fraction.
    /// At ratio 1.5 the step is 1.5·65536, so from input[0] the outputs walk 0, 1.5, 3.
    /// </summary>
    [Fact]
    public void TheFloatExecuteWalksTheStep()
    {
        var r = new WwiseResampler();
        r.Init(FloatMono(72000), 48000);            // ratio 1.5
        r.SetPitch(0);
        Assert.Equal(98304u, r.TargetStep);         // 1.5 * 0x10000

        float[] input = [0f, 1f, 2f, 3f, 4f, 5f, 6f];
        var output = new float[3];
        Assert.Equal(WwiseResampler.DataReady, r.Execute(input, output, 3));
        Assert.Equal(0f, output[0]);
        Assert.Equal(1.5f, output[1]);              // interp(1, 2, 0x8000)
        Assert.Equal(3f, output[2]);
    }

    /// <summary>
    /// M6-004 / 0x00A4717C..0x00A47188: <c>Execute</c> returns 17 (NoMoreData) when the input has zero frames,
    /// before the kernel loop. It applies to the int16 path, the float path and the in-place float path.
    /// </summary>
    [Fact]
    public void ExecuteReturnsNoMoreDataOnEmptyInput()
    {
        var i16 = new WwiseResampler();
        i16.Init(Int16Mono(48000), 48000);
        i16.SetPitch(0);
        Assert.Equal(WwiseResampler.NoMoreData, i16.Execute(ReadOnlySpan<short>.Empty, new float[4], 4));

        var flt = new WwiseResampler();
        flt.Init(FloatMono(48000), 22320);
        flt.SetPitch(0);
        Assert.Equal(WwiseResampler.NoMoreData, flt.Execute(ReadOnlySpan<float>.Empty, new float[4], 4));
        Assert.Equal(WwiseResampler.NoMoreData, flt.ExecuteInPlace(Span<float>.Empty, 4));
    }

    /// <summary>
    /// M6-004 / gapE 3.6: only the float mono mode-1 interpolating kernel 0x00A49E40 was read. The float mono
    /// bypass 0x00A479F4 and ramp 0x00A4A958 were not, so a float resampler in mode 0 or 2 refuses to run.
    /// </summary>
    [Fact]
    public void TheFloatPathRefusesUnreadModes()
    {
        var bypass = new WwiseResampler();          // ratio 1 -> step 0x10000 -> mode 0
        bypass.Init(FloatMono(48000), 48000);
        bypass.SetPitch(0);
        Assert.Equal(0, bypass.Mode);
        Assert.Throws<NotSupportedException>(() => bypass.Execute(new float[4], new float[4], 4));

        var ramp = new WwiseResampler();
        ramp.Init(FloatMono(48000), 48000);
        ramp.SetPitch(0);
        ramp.SetPitch(1200);                        // changed cents -> mode 2
        Assert.Equal(2, ramp.Mode);
        Assert.Throws<NotSupportedException>(() => ramp.Execute(new float[4], new float[4], 4));
    }

    /// <summary>
    /// M6-004 / gapE 3.5, 3.6: a changed pitch ramps over <c>0x400</c> phase units. The first Execute advances
    /// the progress by its frame count; a second reaches the end, at which point current becomes target. The
    /// step is the row's <c>(current·1024 + diff·(pos0 + inc·(k+1)))&gt;&gt;10</c>.
    /// </summary>
    [Fact]
    public void ThePitchRampRunsOver400PhaseUnits()
    {
        var r = new WwiseResampler();
        r.Init(Int16Mono(48000), 48000);            // stepScale = 48000/48000 = 1
        r.SetPitch(0);
        r.SetPitch(1200);                           // target 0x20000, ramping from 0x10000
        Assert.Equal(2, r.Mode);
        Assert.Equal(0x10000u, r.CurrentStep);
        Assert.Equal(0x20000u, r.TargetStep);
        Assert.Equal(0, r.RampPosition);

        var input = new short[4096];
        var output = new float[512];
        Assert.Equal(WwiseResampler.DataReady, r.Execute(input, output, 512));
        Assert.Equal(2, r.Mode);                    // half way: still ramping
        Assert.Equal(WwiseResampler.RampSpan / 2, r.RampPosition);

        Assert.Equal(WwiseResampler.DataReady, r.Execute(input, output, 512));
        Assert.Equal(1, r.Mode);                    // the ramp reached 0x400
        Assert.Equal(WwiseResampler.RampSpan, r.RampPosition);
        Assert.Equal(r.TargetStep, r.CurrentStep);
    }
}
