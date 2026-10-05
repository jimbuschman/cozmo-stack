// fidelity: M6-011, M6-022
using System.Runtime.InteropServices;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The voice filter against the engine's own code under Unicorn (C43; <c>re-analysis/tools/emu/emu_filter.py</c> -> <see cref="WwiseVoiceFilterOracle"/>): the ctor <c>0xA76280</c>, the init <c>0xA764D4</c>, the Reset <c>0xA7666C</c>, the E1 entry
/// <c>0xA766B8</c> (the LPF <c>0xA766F0</c> and HPF <c>0xA77480</c>: first apply, ramp, steady state, countdown, bypass history copy, the NEON block kernel with flush-to-zero and the VFP scalar head and tail) and the two cutoff maps. The expected values
/// are the engine's (never this implementation's). A NaN is compared as NaN (its payload is not modelled); the phone's libm <c>tanf</c> is the one host seam (<see cref="WwiseHostMath.Tanf"/>), proved equal to the correctly rounded float32 for every argument
/// the oracle uses.
/// </summary>
public partial class WwiseVoiceFilterTests
{
    private static bool IsNaNBits(uint v) => (v & 0x7F800000) == 0x7F800000 && (v & 0x7FFFFF) != 0;

    private static uint CanonBits(uint v) => IsNaNBits(v) ? 0x7FC00000u : v;

    private static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);

    private static float FromBits(uint b) => BitConverter.UInt32BitsToSingle(b);

    private static string Sha(IEnumerable<float> words)
    {
        var w = words.Select(f => CanonBits(Bits(f))).ToArray();
        return WwiseResamplerTests.Sha(MemoryMarshal.AsBytes<uint>(w));
    }

    private static string BandString(WwiseVoiceFilterBand b) =>
        $"{CanonBits(Bits(b.Current)):X8}:{CanonBits(Bits(b.Target)):X8}:{b.Steps:X}:{(byte)b.Countdown:X}:{b.Dirty:X}:{b.First:X}:{b.Bypass:X}";

    private static void SetBand(WwiseVoiceFilterBand b, string s)
    {
        var p = s.Split(':');
        b.Current = FromBits(Convert.ToUInt32(p[0], 16));
        b.Target = FromBits(Convert.ToUInt32(p[1], 16));
        b.Steps = Convert.ToUInt16(p[2], 16);
        b.Countdown = unchecked((sbyte)Convert.ToByte(p[3], 16));
        b.Dirty = Convert.ToByte(p[4], 16);
        b.First = Convert.ToByte(p[5], 16);
        b.Bypass = Convert.ToByte(p[6], 16);
    }

    private static float[] Floats(int kind, ulong seed, int count) => WwiseCompressorTests.Input(kind, seed, count).Select(FromBits).ToArray();

    private static string Snapshot(WwiseVoiceFilter f, string? dataHash)
        => (dataHash is null ? "" : dataHash + ":") + BandString(f.LowPass) + ":" + BandString(f.HighPass) + ":" +
           Sha(f.LowPass.F.Concat(f.HighPass.F)) + ":" + Sha((f.LowPass.History ?? Array.Empty<float>()).Concat(f.HighPass.History ?? Array.Empty<float>()));

    private static string RunScenario(string row, Action<WwiseVoiceFilter>? tweak = null)
    {
        var part = row.Split(" | ");
        var head = part[0].Split(' ');
        uint rate = uint.Parse(head[0]), chunk = uint.Parse(head[1]);
        int ch = int.Parse(head[2]);
        uint cfghi = Convert.ToUInt32(head[3], 16);
        int stride = int.Parse(head[4]), align = int.Parse(head[5]);
        ulong fseed = ulong.Parse(head[6]);
        var hs = part[3].Split(':');
        int hk = int.Parse(hs[0]);
        ulong hseed = ulong.Parse(hs[1]);

        var f = new WwiseVoiceFilter(WwiseVoiceFilterRole.A) { MixRate = rate, RampChunk = chunk };
        Assert.Equal(1, f.InitA764D4((uint)ch, 0, null));
        tweak?.Invoke(f);
        Floats(0, fseed, 37).CopyTo(f.LowPass.F, 0);
        Floats(0, fseed + 1, 37).CopyTo(f.HighPass.F, 0);
        if (ch > 0)
        {
            Floats(hk, hseed, 4 * ch).CopyTo(f.LowPass.History!, 0);
            Floats(hk, hseed + 1, 4 * ch).CopyTo(f.HighPass.History!, 0);
        }
        SetBand(f.LowPass, part[1]);
        SetBand(f.HighPass, part[2]);

        var res = new List<string>();
        foreach (var op in part[4].Split(' '))
        {
            var o = op.Split('.');
            switch (o[0])
            {
                case "P":
                {
                    int frames = int.Parse(o[1]), kind = int.Parse(o[2]);
                    ulong seed = ulong.Parse(o[3]);
                    var data = Floats(kind, seed, ch * stride);
                    var st = new WwiseDecodeState { Data = data, ChannelConfig = (uint)ch | (cfghi << 8), MaxFrames = (ushort)stride, ValidFrames = (ushort)frames };
                    f.ProcessA4C60C(st, (uint)align);
                    res.Add("P:" + Snapshot(f, Sha(data)));
                    break;
                }
                case "B":
                    SetBand(f.LowPass, o[1]);
                    SetBand(f.HighPass, o[2]);
                    res.Add("B");
                    break;
                case "R":
                    f.ResetA7666C();
                    res.Add("R:" + Snapshot(f, null));
                    break;
                default: throw new InvalidOperationException(op);
            }
        }
        return string.Join(" ; ", res);
    }

    [Fact]
    public void M6_011_C43_Every_oracle_scenario_matches_the_engines_0xA766B8_with_0xA766F0_and_0xA77480()
    {
        // Citation: C43.2 / C43.3 rows A1..A9, K1..K3, D1, section 5; the engine's own 0xA766B8 under Unicorn (emu_filter.py), 2953 scenarios.
        Assert.True(WwiseVoiceFilterOracle.Cases.Length >= 2900);
        int bad = 0;
        string first = "";
        foreach (var row in WwiseVoiceFilterOracle.Cases)
        {
            var part = row.Split(" | ");
            string got = RunScenario(row);
            if (got != part[5])
            {
                if (++bad == 1) first = row + "\n engine  " + part[5] + "\n managed " + got;
            }
        }
        Assert.True(bad == 0, $"{bad} of {WwiseVoiceFilterOracle.Cases.Length} scenarios differ; first:\n{first}");
    }

    [Fact]
    public void M6_011_C43_5_The_ctor_0xA76280_state_is_the_unity_matrix_with_null_histories()
    {
        // The verifier's item 5 (and the ctor under Unicorn): F_L and F_H hold R0 = splat(1.0), R1..R3 = 0, R4 = (0,0,-0,0), R5 = (0,-0,0,0), R6 = R7 = (-0,0,0,0), B0 = 1.0, B1 = B2 = 0, A1n = A2n = -0; the history pointer and size words are 0;
        // the band blocks are not written by the ctor.
        var row = Assert.Single(WwiseVoiceFilterOracle.Ctor);
        var part = row.Split(" | ");
        var f = new WwiseVoiceFilter(WwiseVoiceFilterRole.A);
        foreach (var band in new[] { f.LowPass, f.HighPass })
            Assert.Equal(string.Join(" ", band.F.Select(x => Bits(x).ToString("X8"))), part[1]);
        Assert.Equal(part[1], part[2]);
        Assert.Equal("00000000 00000000 00000000 00000000", part[3]);
        Assert.Null(f.LowPass.History);
        Assert.Null(f.HighPass.History);
        Assert.Equal("1", part[4]);                                  // the ctor does not write the band blocks 0x170..0x18F (the init does)
        Assert.Equal("00000000", part[6]);                           // it writes [node+0x190] = 0 (the init's word)
        Assert.Equal(0u, f.Word190);
    }

    [Fact]
    public void M6_022_C43_The_init_0xA764D4_matches_the_engine_for_every_channel_word_flag_and_allocation_failure()
    {
        // C42.3 / I1: the band blocks go to the init state, the byte flag and the word are stored, two zeroed (byte word) << 4 blocks are allocated, a failure of the 1st or 2nd allocation returns 2 with both pointers null.
        int n = 0;
        foreach (var row in WwiseVoiceFilterOracle.Init)
        {
            var part = row.Split(" | ");
            if (part[0] == "Z")
            {
                var f0 = new WwiseVoiceFilter(WwiseVoiceFilterRole.A);
                f0.ResetA7666C();
                Assert.Equal("1 1", $"{f0.LowPass.First:X} {f0.HighPass.First:X}");
                continue;
            }
            var h = part[0].Split(' ');
            uint word = Convert.ToUInt32(h[1], 16);
            byte flag = Convert.ToByte(h[2], 16);
            int fail = int.Parse(h[3]);
            var f = new WwiseVoiceFilter(WwiseVoiceFilterRole.A);
            int calls = 0;
            int rc = f.InitA764D4(word, flag, fail == 0 ? null : () => ++calls == fail);
            Assert.Equal(part[1], rc.ToString());
            Assert.Equal(part[2], BandString(f.LowPass) + " " + BandString(f.HighPass));
            Assert.Equal(part[3], $"{(f.LowPass.History is null ? 0 : 1)} {(f.HighPass.History is null ? 0 : 1)}");
            Assert.Equal(part[5], $"{f.Word190:X} {f.Byte194:X}");
            Assert.Equal(part[6], f.LowPass.History is null ? "-" : Sha(f.LowPass.History));
            if (f.HighPass.History is not null) Assert.Equal((int)(word & 0xFF) * 4, f.HighPass.History.Length);
            n++;
        }
        Assert.True(n >= 81, $"{n} init rows");
    }

    [Fact]
    public void M6_011_C43_3_The_cutoff_maps_match_the_engines_0xA7A3D8_and_0xA7A4AC_on_the_grid_the_boundaries_and_special_values()
    {
        // M1: v < 30 vcmpe / bpl (a NaN takes the pow path), vcvt.u32.f32 saturating (a negative or NaN gives 0), the cap 0.45f * (float)u32[0x105243C], the HPF on 100 - v.
        Assert.True(WwiseVoiceFilterOracle.Map.Length >= 4900);
        foreach (var row in WwiseVoiceFilterOracle.Map)
        {
            var f = row.Split(' ');
            uint rate = uint.Parse(f[1]), v = Convert.ToUInt32(f[2], 16);
            uint lpf = CanonBits(Bits(WwiseVoiceFilterCutoff.LowPass(FromBits(v), rate)));
            uint hpf = CanonBits(Bits(WwiseVoiceFilterCutoff.HighPass(FromBits(v), rate)));
            Assert.True(Convert.ToUInt32(f[3], 16) == lpf && Convert.ToUInt32(f[4], 16) == hpf, $"{row}: got {lpf:X8} {hpf:X8}");
        }
    }

    [Fact]
    public void M6_011_Tanf_is_the_correctly_rounded_float32_for_every_argument_the_filter_produces()
    {
        // EQUIVALENT_IMPLEMENTATION of the phone's tanf (PLT 0x4AB038): double Math.Tan rounded once equals the 200-bit correctly rounded binary32 for every argument (fc / (float)rate) * pi of both maps on the 0..100 grid at seven rates,
        // the ramps 0 -> 15 and others, the shipped Sound's 0x3F6231D6 and every argument the oracle run passed. The smallest distance to a binary32 midpoint is in the oracle's header.
        Assert.True(WwiseVoiceFilterOracle.TanfProof.Length >= 10000);
        foreach (var row in WwiseVoiceFilterOracle.TanfProof)
        {
            var f = row.Split(' ');
            uint arg = Convert.ToUInt32(f[0], 16), expected = Convert.ToUInt32(f[1], 16);
            uint got = Bits(WwiseHostMath.Tanf(FromBits(arg)));
            Assert.True(CanonBits(expected) == CanonBits(got), $"{row}: got {got:X8}");
        }
        Assert.Contains(WwiseVoiceFilterOracle.TanfProof, r => r == "3F6231D6 3F9BF7EC");   // the shipped Sound (report section 4)
    }
}
