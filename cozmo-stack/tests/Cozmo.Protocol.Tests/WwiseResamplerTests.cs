using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>The test generator of <c>re-analysis/tools/emu/emu_pitch.py</c> (the class XS there): xorshift32, <c>x ^= x &lt;&lt; 13; x ^= x &gt;&gt; 17; x ^= x &lt;&lt; 5</c>. The oracle stores seeds, parameters and the engine's results.</summary>
internal sealed class Xs
{
    private uint _x;

    public Xs(ulong seed)
    {
        _x = unchecked((uint)(seed * 2654435761UL + 12345UL));
        if (_x == 0) _x = 1;
    }

    public uint U32()
    {
        uint x = _x;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _x = x;
        return x;
    }

    /// <summary><c>int32 / 2^31</c>, exact after the (round-to-nearest) int32 to float32 conversion.</summary>
    public float F32() => (float)unchecked((int)U32()) / 2147483648f;

    public short S16() => unchecked((short)(U32() >> 16));

    public uint Below(uint n) => U32() % n;
}

/// <summary>
/// M6-004 (C38.2): the resampler's every state and kernel against the engine's own code under Unicorn (<c>emu_pitch.py</c> -> <see cref="WwisePitchOracle"/>): the constructor <c>0xA46D70</c>, <c>Init</c> <c>0xA47038</c>, <c>SetPitch</c> <c>0xA47384</c>
/// and its sibling <c>0xA4776C</c>, the format change <c>0xA47528</c> and <c>Execute</c> <c>0xA47178</c> with every kernel of the table <c>0x103C0B8</c> (modes 0, 1, 2; int16 mono and stereo, float mono, stereo and 3 channels in mode 0). The expected
/// values are the engine's, never this implementation's. Floats are compared as bit patterns.
/// </summary>
public class WwiseResamplerTests
{
    internal static int FmtWord(bool isFloat, int ch) => (isFloat ? 0x20 : 0x10) | ((ch * (isFloat ? 4 : 2)) << 6);

    internal static string Snap(WwiseResampler r) => string.Join(" ", new uint[]
    {
        r.InputOffset24, r.OutputOffset28, r.Phase2C, r.Step30, r.Target34, r.Counter38, r.StepScale3C, r.Limit40, r.Mode48,
        BitConverter.SingleToUInt32Bits(r.Ratio4C), BitConverter.SingleToUInt32Bits(r.Pitch50), r.PoolHistory44, r.KernelType54, r.Channels55, r.Byte56, r.Flag57,
    }.Select(v => v.ToString("X8")));

    internal static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexString(b).ToLowerInvariant();

    internal static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..16];

    internal static float Bits(string hex) => BitConverter.UInt32BitsToSingle(Convert.ToUInt32(hex, 16));

    [Fact]
    public void M6_004_Init_0xA47038_matches_the_engine_for_every_channel_count_format_and_pool_failure()
    {
        // C38.2 / P1-13 (0xA47038 = R2.3): 300 engine runs: channel counts 0..12 (more than 8 allocate the history, a failure returns 2), the formats 0x10, 0x20 and others, the kernel type tables, 48000 / outRate, rate / outRate as float32.
        Assert.NotEmpty(WwisePitchOracle.Init);
        foreach (var row in WwisePitchOracle.Init)
        {
            var parts = row.Split(" | ");
            var h = parts[0].Split(' ');
            int rate = int.Parse(h[0]), ch = int.Parse(h[1]), outRate = int.Parse(h[3]);
            uint word = Convert.ToUInt32(h[2], 16);
            bool fail = h[4] == "1";
            var r = new WwiseResampler { AllocationFails = fail ? () => true : null };
            int rc = r.Init(new WwiseResamplerFormat(unchecked((int)word), ch, rate), (uint)outRate);
            Assert.True($"rc={rc}" == parts[1] && Snap(r) == parts[2], $"{row} | C# rc={rc} {Snap(r)}");
        }
    }

    [Fact]
    public void M6_004_SetPitch_0xA47384_and_the_sibling_0xA4776C_match_the_engine_on_3000_random_sequences()
    {
        // C38.2 / P1-12: 3000 sequences of 4..12 calls (SetPitch with flag 0 / 1 and the sibling), cents drawn from integers, fractions, repeats, +-0, +-inf, huge, tiny and NaN, ratios from 8 input rates over 6 output rates: the current / target step,
        // the ramp counter, the mode, the last pitch and the flag byte after every call. powf is the float32 correctly rounded function on both sides (the host seam WwiseHostMath.Powf).
        Assert.True(WwisePitchOracle.SetPitch.Length >= 3000);
        foreach (var row in WwisePitchOracle.SetPitch)
        {
            var parts = row.Split(" | ");
            var h = parts[0].Split(' ');
            int rate = int.Parse(h[0]), outRate = int.Parse(h[1]), ch = int.Parse(h[2]);
            bool isFloat = h[3] == "1";
            var r = new WwiseResampler();
            int rc = r.Init(new WwiseResamplerFormat(FmtWord(isFloat, ch), ch, rate), (uint)outRate);
            Assert.Equal(parts[1], $"rc={rc}");
            var ops = parts[2].Split(' ');
            var results = parts[3].Split(' ');
            Assert.Equal(ops.Length, results.Length);
            for (int k = 0; k < ops.Length; k++)
            {
                float cents = Bits(ops[k].Substring(1, 8));
                bool flag = ops[k][9] == '1';
                if (ops[k][0] == 's') r.SetPitch(cents, flag);
                else r.SetPitchSibling4776C(cents);
                string got = $"{r.Step30:X8}{r.Target34:X8}{r.Counter38:X3}{r.Mode48:X}{BitConverter.SingleToUInt32Bits(r.Pitch50):X8}{r.Flag57}";
                Assert.True(results[k] == got, $"{row} | call {k}: engine {results[k]} C# {got}");
            }
        }
    }

    [Fact]
    public void M6_004_FormatChange_0xA47528_matches_the_engine_including_the_history_conversions()
    {
        // C38.2 / P1-13: 400 runs of the real 0xA47528 on random histories (int16 halfwords or floats), a ratio change or not, the three formats and an unchanged channel count: R's fields and the history after.
        Assert.NotEmpty(WwisePitchOracle.FormatChange);
        foreach (var row in WwisePitchOracle.FormatChange)
        {
            var parts = row.Split(" | ");
            var h = parts[0].Split(' ');
            int rate = int.Parse(h[0]), outRate = int.Parse(h[1]), ch = int.Parse(h[2]);
            bool isFloat = h[3] == "1";
            int flag0 = int.Parse(h[5]);
            var hist = Convert.FromHexString(h[6]);
            int rate2 = int.Parse(h[7]);
            uint word2 = Convert.ToUInt32(h[8], 16);
            int ch2 = int.Parse(h[9]);
            float c1 = Bits(h[10]);
            var r = new WwiseResampler();
            r.Init(new WwiseResamplerFormat(FmtWord(isFloat, ch), ch, rate), (uint)outRate);
            if (h[4] != "N") r.SetPitch(Bits(h[4]), flag0 == 1);
            hist.AsSpan(0, r.History.Length).CopyTo(r.History);
            r.FormatChangeA47528(new WwiseResamplerFormat(unchecked((int)word2), ch2, rate2), c1, (uint)outRate);
            string got = Snap(r) + " " + Hex(r.History);
            Assert.True(parts[1] == got, $"{row} | C# {got}");
        }
    }

    internal static (WwiseDecodeState input, WwiseDecodeState output, float[]? outF) BuildKernelCase(string[] h, WwiseResampler r, int ch, bool isFloat)
    {
        int V = int.Parse(h[2]), A = int.Parse(h[3]), inMax = int.Parse(h[4]), L = int.Parse(h[5]), B = int.Parse(h[6]), outMax = int.Parse(h[7]), outV = int.Parse(h[8]);
        uint P = Convert.ToUInt32(h[9], 16), S = Convert.ToUInt32(h[10], 16), T = Convert.ToUInt32(h[11], 16), C = Convert.ToUInt32(h[12], 16), fp = uint.Parse(h[13]), cfg = Convert.ToUInt32(h[14], 16), dseed = Convert.ToUInt32(h[15], 16);
        int mode = int.Parse(h[0]);
        var d = new Xs(dseed);
        var hist = new byte[32];
        for (int i = 0; i < 8; i++)
        {
            var w = isFloat ? BitConverter.GetBytes(d.F32()) : BitConverter.GetBytes(d.U32());
            w.CopyTo(hist, 4 * i);
        }
        hist.AsSpan(0, r.History.Length).CopyTo(r.History);
        Array data;
        if (isFloat) { var f = new float[inMax * ch]; for (int i = 0; i < f.Length; i++) f[i] = d.F32(); data = f; }
        else { var s16 = new short[inMax * ch]; for (int i = 0; i < s16.Length; i++) s16[i] = d.S16(); data = s16; }
        var outF = new float[outMax * ch];
        Array.Fill(outF, BitConverter.Int32BitsToSingle(0x7FC01234));
        var input = new WwiseDecodeState { Data = data, ChannelConfig = cfg, Scratch08 = 0x2D, MaxFrames = (ushort)inMax, ValidFrames = (ushort)V };
        var output = new WwiseDecodeState { Data = outF, ChannelConfig = (uint)ch, Scratch08 = 0x2D, MaxFrames = (ushort)outMax, ValidFrames = (ushort)outV };
        r.InputOffset24 = (uint)A; r.OutputOffset28 = (uint)B; r.Phase2C = P; r.Step30 = S; r.Target34 = mode == 2 ? T : S; r.Counter38 = C; r.StepScale3C = fp; r.Limit40 = (uint)L; r.Mode48 = (uint)mode;
        return (input, output, outF);
    }

    [Fact]
    public void M6_004_Execute_0xA47178_with_every_kernel_matches_the_engine_bit_for_bit()
    {
        // C38.2 / P1-14, P1-16: 400 + 1600 + 1600 engine runs (modes 0, 1, 2 over the int16 mono and stereo and float mono and stereo kernels; mode 0 also the float 3-channel run of 0xA479F4): the result, the offsets, the phase, the steps and the counter,
        // the mode after the Execute loop's switch at 0x400, the valid counts, the history words and a hash of the whole output block (the unwritten frames keep their sentinel). Includes the int16-stereo +1 LSB quirk of 0xA48C98 and the
        // NEON / scalar split 4 * ((n2 - 5) / 4 + 1) of the int16 mode-1 kernels.
        Assert.True(WwisePitchOracle.Kernels.Length >= 3600);
        foreach (var row in WwisePitchOracle.Kernels)
        {
            var parts = row.Split(" | ");
            var h = parts[0].Split(' ');
            string kind = h[1];
            bool isFloat = kind[0] == 'f';
            int ch = kind[1] - '0';
            var r = new WwiseResampler();
            r.Init(new WwiseResamplerFormat(FmtWord(isFloat, ch), ch, 48000), 48000);
            var (input, output, outF) = BuildKernelCase(h, r, ch, isFloat);
            int ret;
            try { ret = r.Execute(input, output); }
            catch (Exception e) { throw new Exception($"{parts[0]}: {e.GetType().Name} {e.Message} at {e.StackTrace}", e); }
            string got = $"{ret:X2} {r.InputOffset24:X8} {r.OutputOffset28:X8} {r.Phase2C:X8} {r.Step30:X8} {r.Target34:X8} {r.Counter38:X8} {r.Mode48:X} {input.ValidFrames:X4} {output.ValidFrames:X4} {Hex(r.History)} {Sha(MemoryMarshal.AsBytes(outF.AsSpan()))}";
            Assert.True(parts[1] == got, $"{parts[0]}: engine [{parts[1]}] C# [{got}]");
        }
    }

    [Fact]
    public void M6_004_An_Execute_the_engine_never_leaves_is_a_visible_stop_not_a_hang()
    {
        // The engine's Execute loop 0xA471D0..0xA471CC repeats while the input holds frames and the output is below the limit; a mode 2 kernel that produces nothing and consumes nothing (the counter step does not divide 0x400 - c) never ends
        // (the engine run hit its instruction limit). The C# stops with an exception instead of hanging.
        Assert.NotEmpty(WwisePitchOracle.KernelHangs);
        foreach (var row in WwisePitchOracle.KernelHangs)
        {
            var h = row.Split(' ');
            string kind = h[1];
            bool isFloat = kind[0] == 'f';
            int ch = kind[1] - '0';
            var r = new WwiseResampler();
            r.Init(new WwiseResamplerFormat(FmtWord(isFloat, ch), ch, 48000), 48000);
            var (input, output, _) = BuildKernelCase(h, r, ch, isFloat);
            Assert.Throws<InvalidOperationException>(() => r.Execute(input, output));
        }
    }

    [Fact]
    public void M6_004_The_host_powf_is_the_float32_correctly_rounded_result_for_every_integer_cents()
    {
        // C38.2: the phone's libm is not shipped (EQUIVALENT_IMPLEMENTATION): the host function must equal the float32 power for the 4801 integer cents -2400..2400 (numpy float32, independent of .NET).
        Assert.Equal(4801, WwisePitchOracle.PowfTable.Length);
        foreach (var row in WwisePitchOracle.PowfTable)
        {
            var p = row.Split(' ');
            float q = Bits(p[0]);
            Assert.Equal(Convert.ToUInt32(p[1], 16), BitConverter.SingleToUInt32Bits(WwiseHostMath.Powf(2f, q)));
        }
    }

    [Fact]
    public void M6_004_Only_the_listed_cents_change_the_step_when_powf_is_one_ulp_off()
    {
        // C38.2: the sensitive cents inside [-800,-100] U [-250,250] U [100,600] are -795, -759, -149, -94, -80, 16, 20, 172, 218, 316, 411, 455, 486, 507, 525; the shipped values -800, -750, -600, -500, -230 are not sensitive.
        // The engine's step with the powf result exact / one ulp up / one ulp down (emulator runs), and ours with the host powf equal to the exact one.
        var sensitive = new HashSet<int> { -795, -759, -149, -94, -80, 16, 20, 172, 218, 316, 411, 455, 486, 507, 525 };
        Assert.Equal(20, WwisePitchOracle.PowfSensitivity.Length);
        foreach (var row in WwisePitchOracle.PowfSensitivity)
        {
            var p = row.Split(' ');
            int cents = int.Parse(p[0]);
            uint exact = Convert.ToUInt32(p[1], 16), up = Convert.ToUInt32(p[2], 16), down = Convert.ToUInt32(p[3], 16);
            Assert.Equal(sensitive.Contains(cents), up != exact || down != exact);
            var r = new WwiseResampler();
            r.Init(new WwiseResamplerFormat(FmtWord(false, 1), 1, 48000), 48000);
            r.SetPitch(cents, false);
            Assert.Equal(exact, r.Step30);
        }
    }

    [Fact]
    public void M6_004_The_Hijack_stage_is_step_140938_mode_1_float_mono_with_ratio_bits_0x4009A269()
    {
        // C38.2 / P1-18 (the real Thumb Init under Unicorn, section 7 of the verification): 48000 -> 22320: ratio 48000/22320 = 0x4009A269, step 140938 = 0x2268A, mode 1, kernel type 4 (float mono), R+0x3C = 2.
        var r = new WwiseResampler();
        Assert.Equal(1, r.Init(new WwiseResamplerFormat(FmtWord(true, 1), 1, 48000), 22320));
        r.SetPitch(0f, false);
        Assert.Equal(0x4009A269u, BitConverter.SingleToUInt32Bits(r.Ratio4C));
        Assert.Equal(0x2268Au, r.Step30);
        Assert.Equal(0x2268Au, r.Target34);
        Assert.Equal(1u, r.Mode48);
        Assert.Equal((byte)4, r.KernelType54);
        Assert.Equal(2u, r.StepScale3C);
        // the other shipped rates at cents 0 (P1-18): 44100 -> 0xEB33, 32000 -> 0xAAAB, 24000 -> 0x8000, 48000 -> 0x10000 (mode 0)
        foreach (var (rate, step, mode) in new[] { (44100, 0xEB33u, 1u), (32000, 0xAAABu, 1u), (24000, 0x8000u, 1u), (48000, 0x10000u, 0u) })
        {
            var q = new WwiseResampler();
            q.Init(new WwiseResamplerFormat(FmtWord(false, 1), 1, rate), 48000);
            q.SetPitch(0f, false);
            Assert.Equal(step, q.Step30);
            Assert.Equal(mode, q.Mode48);
        }
    }

    [Fact]
    public void M6_004_A_step_that_rounds_to_zero_becomes_one_or_0xFFFFFFFF_by_the_sign_of_the_cents()
    {
        // 0xA4742C..0xA47438 (the same instructions as 0xA46E18..0xA46E1C): a zero step is cents > 0 ? 0xFFFFFFFF : 1, a NaN gives 1. ratio 1 / 2e9 = 5e-10: ratio * 65536 + 0.5 truncates to 0.
        var down = new WwiseResampler();
        down.Init(new WwiseResamplerFormat(FmtWord(false, 1), 1, 1), 2_000_000_000);
        down.SetPitch(0f, false);
        Assert.Equal(1u, down.Step30);
        var up = new WwiseResampler();
        up.Init(new WwiseResamplerFormat(FmtWord(false, 1), 1, 1), 2_000_000_000);
        up.SetPitch(100f, false);
        Assert.Equal(0xFFFFFFFFu, up.Step30);
    }

    [Fact]
    public void M6_004_The_resampler_refuses_what_the_inventory_does_not_settle()
    {
        // an Execute before Init (the constructor leaves R+0x54..+0x56 unset) and a 3+ channel kernel (0xA47824 and the others are unread: no shipped medium has more than two channels) are visible stops
        var fresh = new WwiseResampler();
        var input = new WwiseDecodeState { Data = new float[8], ChannelConfig = 1, MaxFrames = 8, ValidFrames = 4 };
        var output = new WwiseDecodeState { Data = new float[8], ChannelConfig = 1, MaxFrames = 8 };
        Assert.Throws<WwiseMissingBehaviourException>(() => fresh.Execute(input, output));
        var three = new WwiseResampler();
        three.Init(new WwiseResamplerFormat(FmtWord(false, 3), 3, 48000), 48000);
        three.Limit40 = 8;
        three.Mode48 = 0;
        var in3 = new WwiseDecodeState { Data = new short[24], ChannelConfig = 3, MaxFrames = 8, ValidFrames = 4 };
        var out3 = new WwiseDecodeState { Data = new float[24], ChannelConfig = 3, MaxFrames = 8 };
        Assert.Throws<WwiseMissingBehaviourException>(() => three.Execute(in3, out3));
        Assert.Equal(WwiseResampler.NoMoreData, three.Execute(new WwiseDecodeState { Data = new short[24], ValidFrames = 0 }, out3));
    }
}
