using System.Runtime.InteropServices;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-012 / M6-022 (C38.3, V2-05..V2-07): the voice-to-bus mix <c>0xA4FBEC</c>, the matrix mixer <c>0xA45E9C</c> and the lane-accumulated gain kernel <c>0xA46668</c> against the engine's own code under Unicorn
/// (<c>emu_pitch.py</c> -> <see cref="WwisePitchOracle"/>). The expected values are the engine's, never this implementation's; NaNs are compared as NaN.
/// </summary>
public class WwiseMixKernelTests
{
    /// <summary><c>gain_value</c> of emu_pitch.py (draw order included).</summary>
    internal static float GainValue(Xs x)
    {
        uint t = x.Below(12);
        return t switch
        {
            0 => 0f,
            1 => -0f,
            2 => 1f,
            3 => 0.5f,
            4 => x.F32() * 8f,
            _ => x.F32() * 2f,
        };
    }

    private static readonly uint[] Den = { 0x000116C2u, 0x80208F63u, 0x00000001u, 0x007FFFFFu };

    /// <summary><c>den_sample</c> of emu_pitch.py: the next draw, replaced by a denormal at every 3rd (phase 0) or 4th (phase 1) position when <paramref name="den"/> is set.</summary>
    private static float DenSample(Xs d, int i, bool den, int phase)
    {
        float v = d.F32();
        return den && i % (phase == 0 ? 3 : 4) == 0 ? BitConverter.UInt32BitsToSingle(Den[(i + phase) % 4]) : v;
    }

    internal static string CanonHash(ReadOnlySpan<float> values)
    {
        var bits = new uint[values.Length];
        for (int i = 0; i < bits.Length; i++)
        {
            uint v = BitConverter.SingleToUInt32Bits(values[i]);
            bits[i] = ((v >> 23) & 0xFF) == 0xFF && (v & 0x7FFFFF) != 0 ? 0x7FC00000u : v;
        }
        return WwiseResamplerTests.Sha(MemoryMarshal.AsBytes(bits.AsSpan()));
    }

    [Fact]
    public void M6_012_The_ramped_accumulate_0xA46668_matches_the_engine_bit_for_bit_with_lane_gains()
    {
        // V2-07: 400 engine runs: inc == 0 (+-0 included) with start 0 (returns) or not (dst += src * start), a NaN start, and the ramp: lanes {s, s+i, (i+i)+s, s+3i by vmla} then += 4i per half block and (4i + 4i) per block of 8 (accumulated, not start + k*inc).
        Assert.True(WwisePitchOracle.Ramp.Length >= 400);
        foreach (var row in WwisePitchOracle.Ramp)
        {
            var parts = row.Split(" | ");
            var h = parts[0].Split(' ');
            int frames = int.Parse(h[0]);
            float start = BitConverter.UInt32BitsToSingle(Convert.ToUInt32(h[1], 16)), inc = BitConverter.UInt32BitsToSingle(Convert.ToUInt32(h[2], 16));
            var d = new Xs(Convert.ToUInt32(h[3], 16));
            bool den = h[4] == "1";
            var src = new float[frames];
            var dst = new float[frames];
            for (int i = 0; i < frames; i++) src[i] = DenSample(d, i, den, 0);
            for (int i = 0; i < frames; i++) dst[i] = DenSample(d, i, den, 1);
            WwiseMixKernels.RampAccumulateA46668(src, dst, start, inc, frames);
            Assert.True(parts[1] == CanonHash(dst), $"{parts[0]}: engine {parts[1]} C# {CanonHash(dst)}");
        }
    }

    [Fact]
    public void M6_012_A_frame_count_that_is_not_a_multiple_of_8_is_a_visible_stop()
    {
        // V2-07: 0xA46668 processes ceil(frames / 8) * 8 frames, so the engine overruns the buffers: not modelled.
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseMixKernels.RampAccumulateA46668(new float[16], new float[16], 1f, 0.5f, 12));
    }

    private static (float[][] input, float[][] dst, float[] aVals, float[] bVals) BuildMix(uint seed, int sch, int dch, int frames, int smax, bool den, out float[] flat, out float[] ddata)
    {
        var d = new Xs(seed);
        flat = new float[smax * sch];
        for (int i = 0; i < flat.Length; i++) flat[i] = DenSample(d, i, den, 0);
        ddata = new float[frames * dch];
        for (int i = 0; i < ddata.Length; i++) ddata[i] = DenSample(d, i, den, 1);
        var a = new float[sch * dch];
        for (int i = 0; i < a.Length; i++) a[i] = GainValue(d);
        var b = new float[sch * dch];
        for (int i = 0; i < b.Length; i++) b[i] = GainValue(d);
        if (den) { a[0] = BitConverter.UInt32BitsToSingle(Den[1]); b[0] = BitConverter.UInt32BitsToSingle(Den[1]); }
        var input = new float[sch][];
        for (int c = 0; c < sch; c++) input[c] = flat.AsSpan(c * smax, smax).ToArray();
        var dst = new float[dch][];
        for (int c = 0; c < dch; c++) dst[c] = ddata.AsSpan(c * frames, frames).ToArray();
        return (input, dst, a, b);
    }

    [Fact]
    public void M6_012_The_matrix_mixer_0xA45E9C_matches_the_engine_for_1_and_2_channels_each_side()
    {
        // V2-06: 300 engine runs: the OUTER loop over the source channels, start = a * g0, inc = (1/frames) * ((b * g1) - start); the planar destination. The engine rows are padded to a multiple of 4 floats: a layout difference only.
        Assert.True(WwisePitchOracle.Matrix.Length >= 300);
        foreach (var row in WwisePitchOracle.Matrix)
        {
            var parts = row.Split(" | ");
            var h = parts[0].Split(' ');
            uint seed = Convert.ToUInt32(h[0], 16);
            int sch = int.Parse(h[1]), dch = int.Parse(h[2]), frames = int.Parse(h[3]);
            float g0 = BitConverter.UInt32BitsToSingle(Convert.ToUInt32(h[4], 16)), g1 = BitConverter.UInt32BitsToSingle(Convert.ToUInt32(h[5], 16));
            var (input, dst, a, b) = BuildMix(seed, sch, dch, frames, frames, h[6] == "1", out _, out _);
            var mixer = new WwiseMixerConnection(sch, dch);
            mixer.Refresh(a, g0);                                                    // the first update: prev = next, start = end = g0
            mixer.Refresh(b, g1);                                                    // prev = a, start = g0, next = b, end = g1
            mixer.ConsumeBuffer(input, dst, frames, frames);
            var all = dst.SelectMany(x => x).ToArray();
            string got = CanonHash(all);
            Assert.True(parts[1].StartsWith(got), $"{parts[0]}: engine [{parts[1]}] C# {got}");
        }
    }

    [Fact]
    public void M6_022_The_voice_mix_0xA4FBEC_matches_the_engine_gate_pad_state_and_gain_composition()
    {
        // V2-05, C38.3 (the voice's pass block S, a mono bus): 300 engine runs over 1..2 voice channels, bus frame counts 8..1024, the block's max above the bus frame count, valid 0 (returns, nothing touched), partial and full (zero pad from u16[S+0xE]
        // to u16[S+0xC], u16[S+0xE] = max), bus state 1 or 4 (4 becomes 1), [bus+0x68] = 0x2D, u16 [bus+0x6E] = u16 [bus+0x58], and start = (conn[0x10] * conn[8]) * g0, end = (conn[0x14] * conn[0xC]) * g1.
        Assert.True(WwisePitchOracle.Mix.Length >= 300);
        foreach (var row in WwisePitchOracle.Mix)
        {
            var parts = row.Split(" | ");
            var h = parts[0].Split(' ');
            uint seed = Convert.ToUInt32(h[0], 16);
            int sch = int.Parse(h[1]), frames = int.Parse(h[2]), smax = int.Parse(h[3]), valid = int.Parse(h[4]), state = int.Parse(h[5]);
            float F(int i) => BitConverter.UInt32BitsToSingle(Convert.ToUInt32(h[i], 16));
            float c8 = F(6), cC = F(7), c10 = F(8), c14 = F(9), g0 = F(10), g1 = F(11);
            var (input, dst, a, b) = BuildMix(seed, sch, 1, frames, smax, h[12] == "1", out var flat, out var ddata);
            var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), frames);
            if (state == 1) { bus.MixInput(); bus.ReleaseBuffer(); }                  // eState 0x11, state 1, frames 0 (the engine row starts the same)
            Array.Copy(ddata, bus.Buffer, frames);
            bus.Format64 = 0x4101;                                                    // the mono line: u8 [bus+0x64] = 1
            var conn = new WwiseVoiceConnection(bus, sch, 1) { C08 = c8, C0C = cC, C10 = c10, C14 = c14 };
            conn.Descriptor.Reserve(sch, 1);
            for (int i = 0; i < sch; i++) { conn.Descriptor.PrevMatrix[i * 4] = a[i]; conn.Descriptor.NextMatrix[i * 4] = b[i]; }   // prev = [conn+0x24] = a, next = [conn+0x20] = b (the engine's rows are padded to 4 floats)
            uint scfg = sch == 1 ? 0x4101u : 0x3102u;
            var S = new WwiseDecodeState { Data = flat, ChannelConfig = scfg, MaxFrames = (ushort)smax, ValidFrames = (ushort)valid };
            conn.MixA4FBEC(S, g0, g1);
            var e = parts[1].Split(' ');
            string got = $"{(uint)bus.EState:X} {bus.State:X} {bus.Frames:X4} {CanonHash(bus.Buffer)} {S.ValidFrames:X4} {CanonHash(flat)}";
            Assert.True(string.Join(" ", e) == got, $"{parts[0]}: engine [{parts[1]}] C# [{got}]");
        }
    }

    [Fact]
    public void M6_022_The_voice_mix_refuses_the_branches_the_inventory_does_not_settle()
    {
        // V2-05: a mixer plug-in on the bus ([bus+0x1A8] and [[bus+0x1A8]+0xC], vt+0x28 not adopted) and the LFE branch of 0xA45E9C (0xA45FD0..0xA46078, not adopted) are visible stops.
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        var conn = new WwiseVoiceConnection(bus, 1, 1);
        conn.Mixer.Refresh(new[] { 1f }, 1f);
        var S = new WwiseDecodeState { Data = new float[8], ChannelConfig = 0x4101, MaxFrames = 8, ValidFrames = 8 };
        var lfe = new WwiseDecodeState { Data = new float[8], ChannelConfig = 0x4101 | 0x8000, MaxFrames = 8, ValidFrames = 8 };
        Assert.Throws<WwiseMissingBehaviourException>(() => conn.MixA4FBEC(lfe, 1f, 1f));
        bus.OutputMixObject1A8 = () => { };
        bus.MixObject1A8C = new object();
        Assert.Throws<WwiseMissingBehaviourException>(() => conn.MixA4FBEC(S, 1f, 1f));
    }
}
