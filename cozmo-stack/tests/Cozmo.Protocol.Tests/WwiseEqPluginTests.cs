using System.Runtime.InteropServices;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The Parametric EQ plug-in (<c>0x00690003</c>, vptr <c>0x103DF88</c>; M6-013, correction C45, research 20261005-B-M6b-4-bus-fx-17.md sections 2 and 3 with the Verification's build spec) against the engine's own code under Unicorn (<c>re-analysis/tools/emu/emu_eq.py</c> -> <see cref="WwiseEqOracle"/>): the parameter object
/// creator <c>0xAA3178</c> and its vtable (<c>0xAA30CC</c>, <c>0xAA2E8C</c>, <c>0xAA2F44</c>, <c>0xAA2DF4</c>), Create <c>0xAA257C</c>, Init <c>0xAA24B8</c>, Reset <c>0xAA2488</c>, Term <c>0xAA23F0</c>, GetPluginInfo <c>0xAA244C</c>, Execute <c>0xAA2A84</c> with the biquad <c>0xAA2324</c>, the coefficient routine
/// <c>0xAA25E0</c> and the output-gain stage. The expected values are the engine's, never this implementation's; the hand tests below take their numbers from the research sections named on each. A NaN is compared as NaN (its payload is not modelled); the phone's libm is the one host seam
/// (<see cref="WwiseHostMath.Tanf"/>, <see cref="WwiseHostMath.Sinf"/>, <see cref="WwiseHostMath.Cosf"/>, <see cref="WwiseHostMath.Powf"/>) and the proof tables show it equals the correctly rounded float32 for every argument the oracle uses.
/// </summary>
public class WwiseEqPluginTests
{
    private static bool IsNaN(uint v) => (v & 0x7F800000) == 0x7F800000 && (v & 0x7FFFFF) != 0;

    private static string X(float f) => BitConverter.SingleToUInt32Bits(f).ToString("X8");

    private static string Canon(float f) { uint b = BitConverter.SingleToUInt32Bits(f); return (IsNaN(b) ? 0x7FC00000u : b).ToString("X8"); }

    private static string Hash(float[] data)
    {
        var words = new uint[data.Length];
        for (int i = 0; i < words.Length; i++)
        {
            uint b = BitConverter.SingleToUInt32Bits(data[i]);
            words[i] = IsNaN(b) ? 0x7FC00000 : b;
        }
        return WwiseResamplerTests.Sha(MemoryMarshal.AsBytes<uint>(words));
    }

    private static readonly (int Off, int Size)[] ParamFields =
    {
        (4, 4), (8, 4), (0xC, 4), (0x10, 4), (0x14, 1), (0x18, 4), (0x1C, 4), (0x20, 4), (0x24, 4), (0x28, 1), (0x2C, 4), (0x30, 4), (0x34, 4), (0x38, 4), (0x3C, 1), (0x40, 4), (0x44, 1), (0x48, 1), (0x49, 1), (0x4A, 1),
    };

    private static string Snap(WwiseEqParams p) =>
        string.Join(",", ParamFields.Select(f => f.Size == 4 ? p.Word(f.Off).ToString("X8") : p.Byte(f.Off).ToString("X2")));

    private static string Coefs(WwiseEqPlugin eq)
    {
        var l = new List<string>();
        for (int b = 0; b < 3; b++)
        {
            var c = eq.Coefficients(b);
            l.AddRange(new[] { Canon(c.B0), Canon(c.B1), Canon(c.B2), Canon(c.NegA1), Canon(c.NegA2) });
        }
        return string.Join(",", l);
    }

    private static string InitString(WwiseEqPlugin eq, int rc) =>
        $"{rc} {eq.Channels:X} {eq.Rate:X} {(eq.StateSnapshot is null ? 0 : 1)} {(rc == 1 ? Canon(eq.PreviousGain) : "0")} {(rc == 1 ? Coefs(eq) : "0")}";

    private static string Execute(WwiseEqPlugin eq, WwiseEqParams p, uint fmt, uint cfg, string op)
    {
        var f = op.Split('.');
        int frames = int.Parse(f[1]), stride = int.Parse(f[2]), kind = int.Parse(f[3]);
        ulong seed = ulong.Parse(f[4]);
        int planes = Math.Max(Math.Max((int)(fmt & 0xFF), (int)(cfg & 0xFF)), 1);
        var words = WwiseCompressorTests.Input(kind, seed, planes * stride);
        var data = new float[words.Length];
        for (int i = 0; i < data.Length; i++) data[i] = BitConverter.UInt32BitsToSingle(words[i]);
        var s = new WwiseDecodeState { Data = data, ChannelConfig = cfg, Scratch08 = 0xCDCDCDCD, MaxFrames = (ushort)stride, ValidFrames = (ushort)frames };
        eq.Execute(s);
        var st = eq.StateSnapshot;
        string stHash = "-";
        if (st is not null && eq.Channels < 64)
            stHash = Hash(st);
        string dirty = $"{p.Dirty(0):X2}{p.Dirty(1):X2}{p.Dirty(2):X2}";
        return $"E:{Hash(data)}:{Canon(eq.PreviousGain)}:{Coefs(eq)}:{stHash}:{dirty}:{(s.Scratch08 != 0xCDCDCDCD ? 1 : 0)}";
    }

    private static (string Init, string Results) RunCase(string row)
    {
        var part = row.Split(" | ");
        var head = part[0].Split(' ');
        uint rate = uint.Parse(head[0]);
        uint fmt = Convert.ToUInt32(head[1], 16), cfg = Convert.ToUInt32(head[2], 16);
        int fail = int.Parse(head[3]);
        bool clone = head[4] == "1";
        byte[]? block = part[1] == "-" ? null : Convert.FromHexString(part[1]);
        string[] ops = part[2] == "-" ? Array.Empty<string>() : part[2].Split(' ');

        var alloc = new WwisePluginAllocator { FailAt = fail == 0 ? null : fail };
        var p0 = WwiseEqParams.Create(alloc)!;
        if (block is null) p0.SetParamsBlockOrDefaults(null, 0); else p0.SetParamsBlockOrDefaults(block, 56);
        var p = clone ? p0.Clone(alloc)! : p0;
        var eq = WwiseEqPlugin.Create(alloc)!;
        int rc = eq.Init(alloc, null, p, new WwiseEffectFormat(rate, fmt));
        string init = InitString(eq, rc);

        var res = new List<string>();
        foreach (var op in ops)
        {
            switch (op[0])
            {
                case 'R': res.Add($"R:{eq.Reset()}"); break;
                case 'E': res.Add(Execute(eq, p, fmt, cfg, op)); break;
                case 'P':
                {
                    var f = op.Split('.');
                    int r = p.SetParam(uint.Parse(f[1]), BitConverter.GetBytes(Convert.ToUInt32(f[2], 16)));
                    res.Add($"P:{r}:{Snap(p)}");
                    break;
                }
                case 'N': res.Add($"N:{p.SetParam(uint.Parse(op.Split('.')[1]), null)}:{Snap(p)}"); break;
                case 'B': res.Add($"B:{p.SetParamsBlockOrDefaults(Convert.FromHexString(op[2..]), 56)}:{Snap(p)}"); break;
                case 'D': res.Add($"D:{p.SetParamsBlockOrDefaults(null, 0)}:{Snap(p)}"); break;
                case 'I':
                {
                    int r = ((IWwiseEffectPlugin)eq).GetPluginInfo(out var info);
                    res.Add($"I:{r}:{info.Word0:X8},{info.Word4:X8},{info.Byte8:X2},{info.ByteA:X2}");
                    break;
                }
                case 'T':
                {
                    int n0 = alloc.Freed.Count;
                    int r = eq.Term(alloc);
                    res.Add($"T:{r}:{alloc.Freed.Count - n0}:{(eq.StateSnapshot is null ? 1 : 0)}");
                    break;
                }
                default: throw new InvalidOperationException(op);
            }
        }
        return (init, string.Join(" ; ", res));
    }

    [Fact]
    public void M6_013_C45_3_Every_oracle_life_matches_the_engine_Init_0xAA24B8_Reset_0xAA2488_Execute_0xAA2A84_and_the_parameter_object()
    {
        // 1902 lives of the engine's own code: the two shipped blocks at nine rates on 1..3 channels with a gain change between Executes (the NEON ramp), random blocks of all seven filter types on 1..6 channels with and without the LFE flag, SetParam ids 0..16 and the invalid ones,
        // block / defaults / null-value operations between Executes, denormal-scale, denormal, NaN / inf and random-pattern audio, every Init channel count, the failing state allocation, the instance with and without a Clone of the parameter object, the coefficient extremes, GetPluginInfo and Term.
        Assert.True(WwiseEqOracle.Cases.Length >= 1800);
        int bad = 0;
        string? first = null;
        foreach (var row in WwiseEqOracle.Cases)
        {
            var part = row.Split(" | ");
            (string init, string res) r;
            try { r = RunCase(row); }
            catch (Exception ex) { throw new InvalidOperationException($"row {part[0]} | {part[1]} | {part[2]}: {ex.GetType().Name}: {ex.Message}", ex); }
            var (init, res) = r;
            if (WwiseCompressorTests.CanonWords(init) != WwiseCompressorTests.CanonWords(part[3]) || WwiseCompressorTests.CanonWords(res) != WwiseCompressorTests.CanonWords(part[4]))
            {
                bad++;
                first ??= $"{part[0]} | {part[2]}\n engine {part[3]} | {part[4]}\n   here {init} | {res}";
            }
        }
        Assert.True(bad == 0, $"{bad} of {WwiseEqOracle.Cases.Length} cases differ; first:\n{first}");
    }

    [Fact]
    public void M6_013_C45_3_The_coefficient_routine_0xAA25E0_matches_the_engine_for_all_seven_types_the_cap_and_the_rates()
    {
        // 3505 rows of the routine on its own (research 3.6): every type 0..6, three band slots (the store address this+4+0x14*band), 17 rates including 0 and 1000000, frequencies around and above (fs * 0.5f) * 0.9f, the shipped bands, and NaN / inf / zero / huge gain, frequency and Q.
        Assert.True(WwiseEqOracle.Coefficients.Length >= 3000);
        int bad = 0;
        string? first = null;
        var types = new HashSet<int>();
        foreach (var row in WwiseEqOracle.Coefficients)
        {
            var part = row.Split(" | ");
            var h = part[0].Split(' ');
            int band = int.Parse(h[0]), type = int.Parse(h[1]);
            uint g = Convert.ToUInt32(h[2], 16), f = Convert.ToUInt32(h[3], 16), q = Convert.ToUInt32(h[4], 16), rate = uint.Parse(h[5]);
            types.Add(type);
            var c = WwiseEqCoefficients.Compute((uint)type, BitConverter.UInt32BitsToSingle(g), BitConverter.UInt32BitsToSingle(f), BitConverter.UInt32BitsToSingle(q), rate);
            string got = string.Join(",", new[] { c.B0, c.B1, c.B2, c.NegA1, c.NegA2 }.Select(Canon));
            if (WwiseCompressorTests.CanonWords(got) != WwiseCompressorTests.CanonWords(part[1]))
            {
                bad++;
                first ??= $"{row}\n   here {got}";
            }
        }
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, types.OrderBy(t => t).ToArray());
        Assert.True(bad == 0, $"{bad} of {WwiseEqOracle.Coefficients.Length} rows differ; first:\n{first}");
    }

    [Fact]
    public void M6_013_C45_3_The_oracle_covers_every_input_kind_both_gain_paths_every_filter_type_and_the_failing_allocation()
    {
        // Guards the oracle itself: a regenerated file that lost a group would still pass the equality tests above.
        var kinds = new HashSet<int>();
        int ramp = 0, equal = 0, failed = 0, lfe = 0, multi = 0, defaults = 0, setParam = 0, noClone = 0;
        var typesInBlocks = new HashSet<int>();
        foreach (var row in WwiseEqOracle.Cases)
        {
            var part = row.Split(" | ");
            var head = part[0].Split(' ');
            foreach (var op in part[2].Split(' ')) if (op.StartsWith("E.")) kinds.Add(int.Parse(op.Split('.')[3]));
            if (part[3].StartsWith("52 ")) failed++;
            if ((Convert.ToUInt32(head[1], 16) & 0x8000) != 0) lfe++;
            if ((Convert.ToUInt32(head[1], 16) & 0xFF) > 1) multi++;
            if (part[1] == "-") defaults++;
            if (part[2].Contains("P.15.")) ramp++;
            if (part[2].Contains(" P.") || part[2].Contains(" N.")) setParam++;
            if (head[4] == "0") noClone++;
            if (part[1] != "-")
            {
                var block = Convert.FromHexString(part[1]);
                for (int b = 0; b < 3; b++) typesInBlocks.Add((int)BitConverter.ToUInt32(block, 0x11 * b));
            }
            if (part[2].Contains("E.") && !part[2].Contains("P.15.") && !part[2].Contains("B.") && !part[2].Contains(" D")) equal++;
        }
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, kinds.OrderBy(k => k).ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, typesInBlocks.OrderBy(k => k).ToArray());
        Assert.True(ramp > 300 && equal > 500 && failed >= 15 && lfe > 100 && multi > 300 && defaults >= 60 && setParam > 500 && noClone > 400, $"{ramp} {equal} {failed} {lfe} {multi} {defaults} {setParam} {noClone}");
    }

    [Fact]
    public void M6_013_The_host_tanf_sinf_cosf_and_powf_are_the_correctly_rounded_float32_for_every_argument_the_EQ_produces()
    {
        // EQUIVALENT_IMPLEMENTATION of the phone's libm (PLT 0x4AB038 tanf, 0x4A4168 sinf, 0x4A415C cosf, 0x4D6778 powf): double precision rounded once equals the 200-bit correctly rounded binary32 for the shipped bands at the rates the stack runs (48000, 22320) and other realistic rates,
        // a grid of frequencies, gains and output levels, and every argument the oracle run passed (the smallest distance of an exact result to a binary32 midpoint is in the oracle's header).
        Assert.True(WwiseEqOracle.TanfProof.Length >= 150 && WwiseEqOracle.SinfProof.Length >= 150 && WwiseEqOracle.CosfProof.Length >= 150 && WwiseEqOracle.PowfProof.Length >= 20);
        void Check(string[] table, Func<float, float> fn, string name)
        {
            foreach (var row in table)
            {
                var f = row.Split(' ');
                uint arg = Convert.ToUInt32(f[0], 16), expected = Convert.ToUInt32(f[1], 16);
                uint got = BitConverter.SingleToUInt32Bits(fn(BitConverter.UInt32BitsToSingle(arg)));
                if (IsNaN(expected)) Assert.True(IsNaN(got), $"{name} {row}");
                else Assert.True(expected == got, $"{name} {row}: got {got:X8}");
            }
        }
        Check(WwiseEqOracle.TanfProof, WwiseHostMath.Tanf, "tanf");
        Check(WwiseEqOracle.SinfProof, WwiseHostMath.Sinf, "sinf");
        Check(WwiseEqOracle.CosfProof, WwiseHostMath.Cosf, "cosf");
        Check(WwiseEqOracle.PowfProof, y => WwiseHostMath.Powf(10f, y), "powf");
    }

    // ------------------------------------------------------------------ hand tests from the research sections

    private static byte[] Block(params (uint Type, float Gain, float Freq, float Q, byte On)[] bands) => BlockWith(0f, 0, bands);

    private static byte[] BlockWith(float outDb, byte plfe, params (uint Type, float Gain, float Freq, float Q, byte On)[] bands)
    {
        var b = new byte[56];
        for (int i = 0; i < 3; i++)
        {
            var (type, gain, freq, q, on) = bands[i];
            BitConverter.TryWriteBytes(b.AsSpan(0x11 * i, 4), type);
            BitConverter.TryWriteBytes(b.AsSpan(0x11 * i + 4, 4), gain);
            BitConverter.TryWriteBytes(b.AsSpan(0x11 * i + 8, 4), freq);
            BitConverter.TryWriteBytes(b.AsSpan(0x11 * i + 12, 4), q);
            b[0x11 * i + 0x10] = on;
        }
        BitConverter.TryWriteBytes(b.AsSpan(0x33, 4), outDb);
        b[0x37] = plfe;
        return b;
    }

    private static (WwiseEqPlugin Eq, WwiseEqParams P, WwisePluginAllocator A) Make(byte[] block, uint rate = 48000, uint fmtWord = 0x4101, bool reset = true)
    {
        var alloc = new WwisePluginAllocator();
        var p0 = WwiseEqParams.Create(alloc)!;
        p0.SetParamsBlockOrDefaults(block, 56);
        var p = p0.Clone(alloc)!;
        var eq = WwiseEqPlugin.Create(alloc)!;
        Assert.Equal(1, eq.Init(alloc, null, p, new WwiseEffectFormat(rate, fmtWord)));
        if (reset) eq.Reset();
        return (eq, p, alloc);
    }

    private static WwiseDecodeState Mono(float[] data, int valid, uint cfg = 0x4101, int stride = -1) =>
        new() { Data = data, ChannelConfig = cfg, MaxFrames = (ushort)(stride < 0 ? valid : stride), ValidFrames = (ushort)valid };

    [Fact]
    public void M6_013_C45_2_The_defaults_of_a_zero_size_block_are_the_words_0xAA30E4_to_0xAA3160_store()
    {
        // research 2.3: [4]=4, [8]=0, [0xC]=120.0 (0x42F00000), [0x10]=5.0 (0x40A00000), byte [0x14]=1; [0x18]=6, [0x1C]=0, [0x20]=2000.0 (0x44FA0000), [0x24]=5.0, byte [0x28]=1; [0x2C]=5, [0x30]=0, [0x34]=5000.0 (0x459C4000), [0x38]=5.0, byte [0x3C]=1; [0x40]=0, byte [0x44]=1; dirty bytes 1.
        var p = WwiseEqParams.Create(new WwisePluginAllocator())!;
        Assert.Equal(1, p.SetParamsBlockOrDefaults(null, 0));
        var words = new (int Off, uint V)[] { (4, 4), (8, 0), (0xC, 0x42F00000), (0x10, 0x40A00000), (0x18, 6), (0x1C, 0), (0x20, 0x44FA0000), (0x24, 0x40A00000), (0x2C, 5), (0x30, 0), (0x34, 0x459C4000), (0x38, 0x40A00000), (0x40, 0) };
        foreach (var (off, v) in words) Assert.Equal(v, p.Word(off));
        foreach (int off in new[] { 0x14, 0x28, 0x3C, 0x44, 0x48, 0x49, 0x4A }) Assert.Equal((byte)1, p.Byte(off));
    }

    [Fact]
    public void M6_013_C45_2_SetParamsBlock_0xAA2E8C_maps_the_block_offsets_including_the_unaligned_reads()
    {
        // research 2.2: block 0->+4, 4->+8, 8->+0xC, 0xC->+0x10, byte 0x10->+0x14; 0x11->+0x18, 0x15->+0x1C, 0x19->+0x20, 0x1D->+0x24, byte 0x21->+0x28; 0x22->+0x2C, 0x26->+0x30, 0x2A->+0x34, 0x2E->+0x38, byte 0x32->+0x3C; 0x33->+0x40 (raw), byte 0x37->+0x44; dirty bytes +0x48..+0x4A = 1.
        // Reads at +0x11, +0x15, ... +0x33 are unaligned; the block below has a distinct byte at every offset.
        var block = new byte[56];
        for (int i = 0; i < 56; i++) block[i] = (byte)(i * 7 + 3);
        var p = WwiseEqParams.Create(new WwisePluginAllocator())!;
        p.Clone(new WwisePluginAllocator());                                       // (the creator's object is usable without it; no effect)
        Assert.Equal(1, p.SetParamsBlockOrDefaults(block, 56));
        var words = new (int ParamOff, int BlockOff)[]
        {
            (4, 0), (8, 4), (0xC, 8), (0x10, 0xC), (0x18, 0x11), (0x1C, 0x15), (0x20, 0x19), (0x24, 0x1D), (0x2C, 0x22), (0x30, 0x26), (0x34, 0x2A), (0x38, 0x2E), (0x40, 0x33),
        };
        foreach (var (po, bo) in words)
            Assert.Equal((uint)(block[bo] | block[bo + 1] << 8 | block[bo + 2] << 16 | block[bo + 3] << 24), p.Word(po));
        foreach (var (po, bo) in new[] { (0x14, 0x10), (0x28, 0x21), (0x3C, 0x32), (0x44, 0x37) }) Assert.Equal(block[bo], p.Byte(po));
        foreach (int off in new[] { 0x48, 0x49, 0x4A }) Assert.Equal((byte)1, p.Byte(off));
    }

    [Fact]
    public void M6_013_C45_2_The_creator_stores_only_the_dirty_bytes_and_a_field_read_before_any_store_stops()
    {
        // research 2.1 / 0xAA3178: the creator sets the three dirty bytes (and the vptr); the rest is uninitialised pool memory.
        var p = WwiseEqParams.Create(new WwisePluginAllocator())!;
        Assert.Equal(new byte[] { 1, 1, 1 }, new[] { p.Dirty(0), p.Dirty(1), p.Dirty(2) });
        Assert.Throws<WwiseMissingBehaviourException>(() => p.BandType(0));
        Assert.Throws<WwiseMissingBehaviourException>(() => p.OutputDb);
        Assert.Throws<WwiseMissingBehaviourException>(() => p.ProcessLfe);
        // a block shorter than 56 bytes is not an engine input: the engine reads 56 whatever the size says
        Assert.Throws<WwiseMissingBehaviourException>(() => p.SetParamsBlockOrDefaults(new byte[20], 20));
        Assert.Null(WwiseEqParams.Create(new WwisePluginAllocator { FailAt = 1 }));
        var counting = new WwisePluginAllocator();
        WwiseEqParams.Create(counting);
        Assert.Equal(new[] { 0x4C }, counting.Sizes);
    }

    [Fact]
    public void M6_013_C45_2_SetParam_0xAA2F44_maps_ids_0_to_16_and_returns_0x1F_for_the_rest()
    {
        // research 2.4: band = id / 5; id % 5: 0 float -> int (vcvt.s32.f32) -> type, 1 gain, 2 freq, 3 Q (raw words), 4 on = (value != 0.0); each sets the band's dirty byte; id 15 -> +0x40 raw (no dirty); id 16 -> byte +0x44; otherwise 0x1F; a null value pointer returns 0x1F; success 1.
        var (eq, p, _) = Make(BlockWith(0f, 0, (6, 0f, 1000f, 1f, 1), (6, 0f, 1000f, 1f, 1), (6, 0f, 1000f, 1f, 1)));
        eq.Execute(Mono(new float[4], 4));                                           // clears the three dirty bytes (research 3.4)
        Assert.Equal(new byte[] { 0, 0, 0 }, new[] { p.Dirty(0), p.Dirty(1), p.Dirty(2) });

        static byte[] F(float f) => BitConverter.GetBytes(f);
        // ids 0..14: band id / 5
        for (uint id = 0; id <= 14; id++)
        {
            int band = (int)(id / 5), field = (int)(id % 5);
            int off = 4 + 0x14 * band;
            byte[] value = field switch { 0 => F(5.9f), 4 => F(2f), _ => F(100f + id) };
            Assert.Equal(1, p.SetParam(id, value));
            switch (field)
            {
                case 0: Assert.Equal(5u, p.Word(off)); break;                         // 5.9 -> 5 (toward zero)
                case 1: Assert.Equal(BitConverter.SingleToUInt32Bits(100f + id), p.Word(off + 4)); break;
                case 2: Assert.Equal(BitConverter.SingleToUInt32Bits(100f + id), p.Word(off + 8)); break;
                case 3: Assert.Equal(BitConverter.SingleToUInt32Bits(100f + id), p.Word(off + 0xC)); break;
                default: Assert.Equal((byte)1, p.Byte(off + 0x10)); break;
            }
            Assert.Equal((byte)1, p.Dirty(band));
        }
        // type conversion: toward zero, saturating, NaN gives 0; on: a NaN counts as on, -0.0 is off
        p.SetParam(0, F(-1.5f)); Assert.Equal(0xFFFFFFFFu, p.Word(4));
        p.SetParam(0, F(float.NaN)); Assert.Equal(0u, p.Word(4));
        p.SetParam(4, F(float.NaN)); Assert.Equal((byte)1, p.Byte(0x14));
        p.SetParam(4, F(-0f)); Assert.Equal((byte)0, p.Byte(0x14));
        p.SetParam(0, F(5f));                                                        // back to a type the coefficient routine knows (a type above 6 is a required stop)
        // id 15 and 16 set no dirty byte
        eq.Execute(Mono(new float[4], 4));
        Assert.Equal(new byte[] { 0, 0, 0 }, new[] { p.Dirty(0), p.Dirty(1), p.Dirty(2) });
        Assert.Equal(1, p.SetParam(15, F(3f)));
        Assert.Equal(BitConverter.SingleToUInt32Bits(3f), p.Word(0x40));
        Assert.Equal(1, p.SetParam(16, new byte[] { 7, 0, 0, 0 }));
        Assert.Equal((byte)7, p.Byte(0x44));
        Assert.Equal(new byte[] { 0, 0, 0 }, new[] { p.Dirty(0), p.Dirty(1), p.Dirty(2) });
        // the invalid ones
        Assert.Equal(0x1F, p.SetParam(17, F(1f)));
        Assert.Equal(0x1F, p.SetParam(0xFFFFFFFF, F(1f)));
        Assert.Equal(0x1F, p.SetParam(3, null));
    }

    [Fact]
    public void M6_013_C45_2_Clone_0xAA2DF4_copies_the_0x44_bytes_including_ProcessLFE_and_sets_the_dirty_bytes()
    {
        var alloc = new WwisePluginAllocator();
        var p = WwiseEqParams.Create(alloc)!;
        p.SetParamsBlockOrDefaults(BlockWith(1.5f, 1, (4, 2f, 835f, 2.1f, 1), (6, -2.5f, 1359f, 4.2f, 1), (6, -4f, 5091f, 1.5f, 1)), 56);
        var q = p.Clone(alloc)!;
        Assert.Equal(0x4C, alloc.Sizes[^1]);
        Assert.Equal((byte)1, q.Byte(0x44));                                         // ProcessLFE (+0x44) is within the 0x44 bytes copied from +4
        Assert.Equal(p.Word(0x40), q.Word(0x40));
        foreach (int off in new[] { 4, 8, 0xC, 0x10, 0x18, 0x2C, 0x34, 0x38 }) Assert.Equal(p.Word(off), q.Word(off));
        Assert.Equal(new byte[] { 1, 1, 1 }, new[] { q.Dirty(0), q.Dirty(1), q.Dirty(2) });
        Assert.Null(p.Clone(new WwisePluginAllocator { FailAt = 1 }));
        Assert.Equal(1, WwiseEqParams.Destroy(null, alloc));
        Assert.Equal(1, WwiseEqParams.Destroy(q, alloc));
        Assert.Same(q, alloc.Freed[^1]);
    }

    [Fact]
    public void M6_013_The_host_sqrtf_is_the_IEEE_square_root_and_the_engines_literals_are_the_values_it_names()
    {
        // 0xAA26A0 vsqrt.f32 is IEEE; sqrtf (PLT 0x4A4078) is only called when that produced a NaN (0xAA26AC, 0xAA26D4, 0xAA29C0, 0xAA29E8), so the host seam only ever returns a NaN for a NaN or a negative argument.
        Assert.Equal(0x3FB504F3u, BitConverter.SingleToUInt32Bits(WwiseHostMath.Sqrtf(2f)));              // the literal at 0xAA294C is sqrt(2) correctly rounded
        Assert.Equal(2f, WwiseHostMath.Sqrtf(4f));
        Assert.True(float.IsNaN(WwiseHostMath.Sqrtf(-1f)));
        Assert.True(float.IsNaN(WwiseHostMath.Sqrtf(float.NaN)));
    }

    [Fact]
    public void M6_013_C45_3_The_slots_return_what_the_binary_returns()
    {
        // Info 0xAA244C = {3, 0x7E002, byte[8] = 1, byte[0xA] = 0}; vt+0x14 / vt+0x18 are 0x8DBF38 / 0x8DBF3C (movs r0,#0); vt+0x24 is 0xAA2DE8 (mov r0,#0x2d); the plug-in id is 0x00690003 (HIRC key) and the vptr 0x103DF88.
        var (eq, _, _) = Make(Block((4, 0, 100, 1, 1), (4, 0, 100, 1, 1), (4, 0, 100, 1, 1)));
        IWwiseEffectPlugin plug = eq;
        Assert.Equal(1, plug.GetPluginInfo(out var info));
        Assert.Equal(new WwisePluginInfo(3, 0x7E002, 1, 0), info);
        Assert.Equal(0, plug.Slot14());
        Assert.Equal(0, plug.Slot18());
        Assert.Equal(0x2D, plug.Slot24());
        Assert.Equal(0x00690003u, WwiseEqPlugin.PluginId);
        Assert.Equal(0x103DF88u, WwiseEqPlugin.Vptr);
    }

    [Fact]
    public void M6_013_C45_3_Init_sets_the_fields_allocates_channels_times_0x30_and_does_not_zero_the_state()
    {
        // research 3.2: [0x40]=param, [0x44]=u8 fmt[4], [0x48]=u32 fmt[0]; with u32 fmt[4] & 0x8000 and param byte [0x44] == 0 the count is decremented; a non-zero count allocates count * 0x30 bytes (failure returns 0x34); the state is NOT zeroed by Init; coefficients [4..0x40) zeroed;
        // dirty bytes 1; [0x50] = powf(10.0f, param[0x40] * 0.05f) (0x3D4CCCCD).
        var alloc = new WwisePluginAllocator();
        var p = WwiseEqParams.Create(alloc)!;
        p.SetParamsBlockOrDefaults(BlockWith(1.5f, 0, (4, 2f, 835f, 2.1f, 1), (6, -2.5f, 1359f, 4.2f, 1), (6, -4f, 5091f, 1.5f, 1)), 56);
        var eq = WwiseEqPlugin.Create(alloc)!;
        Assert.Equal(0x54, alloc.Sizes[^1]);
        Assert.Equal(1, eq.Init(alloc, null, p, new WwiseEffectFormat(22320, 0x7103)));
        Assert.Equal(3u * 0x30, (uint)alloc.Sizes[^1]);
        Assert.Equal(3u, eq.Channels);
        Assert.Equal(22320u, eq.Rate);
        Assert.Same(p, eq.Params);
        Assert.Equal(0x3F9820D7u, BitConverter.SingleToUInt32Bits(eq.PreviousGain));  // the engine's powf(10.0f, 1.5f * 0.05f) = 1.1885022 (the Unicorn run, oracle init rows)
        for (int b = 0; b < 3; b++) Assert.Equal(new WwiseBiquadCoefficients(0, 0, 0, 0, 0), eq.Coefficients(b));
        Assert.Throws<WwiseMissingBehaviourException>(() => eq.Execute(Mono(new float[4], 4, 0x7103)));   // the state was not zeroed: the owner's Reset comes first
        eq.Reset();
        eq.Execute(Mono(new float[12], 4, 0x7103, 4));

        // the LFE flag (u32 fmt[4] & 0x8000): the count loses the LFE channel unless ProcessLFE is set
        var eq2 = WwiseEqPlugin.Create(alloc)!;
        Assert.Equal(1, eq2.Init(alloc, null, p, new WwiseEffectFormat(48000, 0x8003)));
        Assert.Equal(2u, eq2.Channels);
        p.SetParam(16, new byte[] { 1, 0, 0, 0 });
        var eq3 = WwiseEqPlugin.Create(alloc)!;
        Assert.Equal(1, eq3.Init(alloc, null, p, new WwiseEffectFormat(48000, 0x8003)));
        Assert.Equal(3u, eq3.Channels);

        // an allocation failure returns 0x34 with nothing else stored
        var failing = new WwisePluginAllocator();
        var fp = WwiseEqParams.Create(failing)!;
        fp.SetParamsBlockOrDefaults(null, 0);
        var feq = WwiseEqPlugin.Create(failing)!;
        failing.FailAt = failing.Sizes.Count + 1;
        Assert.Equal(0x34, feq.Init(failing, null, fp, new WwiseEffectFormat(48000, 0x3102)));
        Assert.Null(feq.StateSnapshot);
    }

    [Fact]
    public void M6_013_C45_3_A_mono_format_with_the_LFE_flag_and_ProcessLFE_0_has_no_channel_to_process()
    {
        // 0xAA24E4..0xAA24F4: [0x44] = 1 - 1 = 0: nothing is allocated and Execute returns at once (0xAA2A98..0xAA2AAC) without touching the buffer, the coefficients or [0x50].
        var (eq, p, alloc) = Make(BlockWith(1.5f, 0, (4, 2f, 835f, 2.1f, 1), (6, -2.5f, 1359f, 4.2f, 1), (6, -4f, 5091f, 1.5f, 1)), fmtWord: 0x4101 | 0x8000, reset: false);
        Assert.Equal(0u, eq.Channels);
        Assert.Null(eq.StateSnapshot);
        var data = new[] { 0.25f, -0.5f, 0.75f };
        var s = Mono(data, 3, 0x4101 | 0x8000);
        eq.Execute(s);
        Assert.Equal(new[] { 0.25f, -0.5f, 0.75f }, data);
        Assert.Equal(new byte[] { 1, 1, 1 }, new[] { p.Dirty(0), p.Dirty(1), p.Dirty(2) });
        Assert.Equal(0x3F9820D7u, BitConverter.SingleToUInt32Bits(eq.PreviousGain));
    }

    [Fact]
    public void M6_013_C45_3_Execute_with_no_valid_frames_changes_nothing()
    {
        // 0xAA2AB0..0xAA2AB8: u16 [S+0xE] == 0 returns before the dirty recompute, the gain and the history.
        var (eq, p, _) = Make(BlockWith(1.5f, 0, (4, 2f, 835f, 2.1f, 1), (6, -2.5f, 1359f, 4.2f, 1), (6, -4f, 5091f, 1.5f, 1)));
        var s = Mono(new float[8], 0, stride: 8);
        eq.Execute(s);
        Assert.Equal(new byte[] { 1, 1, 1 }, new[] { p.Dirty(0), p.Dirty(1), p.Dirty(2) });
        Assert.Equal(new WwiseBiquadCoefficients(0, 0, 0, 0, 0), eq.Coefficients(0));
        Assert.Equal(0u, s.Scratch08);                                                // [S+8] is never written
    }

    [Fact]
    public void M6_013_C45_3_The_shipped_MasterCurve_bands_at_48000_have_the_coefficients_the_engine_stores()
    {
        // The words 0xAA25E0 stores for Robot_Bus_Eq_MasterCurve (research section 1 block 0x6767FC1F) at the 48000 Hz mix rate: the first row of the oracle's life 0 (Unicorn run of the real routine with correctly rounded libm).
        var blk = BlockWith(1.5f, 0, (4, 2.0f, 835f, 2.1f, 1), (6, -2.5f, 1359f, 4.2f, 1), (6, -4.0f, 5091f, 1.5f, 1));
        var (eq, p, _) = Make(blk);
        eq.Execute(Mono(new float[4], 4));
        var expected = new uint[][]
        {
            new uint[] { 0x3F812483, 0xBFED2F3F, 0x3F5B47C1, 0x3FED5953, 0xBF5D3CA0 },
            new uint[] { 0x3F7E7ABC, 0xBFF5F9E8, 0x3F755C6E, 0x3FF5F9E8, 0xBF73D729 },
            new uint[] { 0x3F6C8A53, 0xBF9FC789, 0x3F29FFDC, 0x3F9FC789, 0xBF168A30 },
        };
        for (int b = 0; b < 3; b++)
        {
            var c = eq.Coefficients(b);
            Assert.Equal(expected[b], new[] { c.B0, c.B1, c.B2, c.NegA1, c.NegA2 }.Select(BitConverter.SingleToUInt32Bits).ToArray());
        }
    }

    [Fact]
    public void M6_013_C45_3_A_band_that_is_off_still_has_its_coefficients_computed_when_dirty_and_its_biquad_skipped()
    {
        // research 3.9 / 3.4: HiLowPass band 1 (peaking -4.5 dB @1000 Hz, Q 0.5) is off but its dirty flag is set, so its coefficients are computed (powf argument -4.5f * 0.025f = -0.112500004); only the on bands run the biquad.
        var blk = BlockWith(0f, 0, (1, 0f, 333f, 1f, 1), (6, -4.5f, 1000f, 0.5f, 0), (0, 0f, 14298f, 1f, 1));
        var (eq, p, _) = Make(blk);
        var data = new float[16];
        data[0] = 1f;
        eq.Execute(Mono(data, 16));
        var c1 = eq.Coefficients(1);
        Assert.NotEqual(0f, c1.B0);                                                    // computed although off
        var st = eq.StateSnapshot!;
        Assert.All(st.Skip(4).Take(4), v => Assert.Equal(0f, v));                      // band 1's history (state at +channels*16) untouched
        Assert.NotEqual(0f, st[2]);                                                    // band 0 (on) ran: y1 = the last output of the high pass
        Assert.Equal(new byte[] { 0, 0, 0 }, new[] { p.Dirty(0), p.Dirty(1), p.Dirty(2) });
    }

    [Fact]
    public void M6_013_C45_3_A_filter_type_above_6_is_a_required_stop()
    {
        // research 3.6 / 3.10 (unread): type > 6 jumps to 0xAA272C with whatever the registers hold; it throws instead of inventing coefficients.
        var (eq, _, _) = Make(Block((7, 0, 1000, 1, 1), (4, 0, 100, 1, 1), (4, 0, 100, 1, 1)));
        Assert.Throws<WwiseMissingBehaviourException>(() => eq.Execute(Mono(new float[4], 4)));
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseEqCoefficients.Compute(0xFFFFFFFF, 0f, 1000f, 1f, 48000));
    }

    // ------------------------------------------------------------------ the output-gain stage (research 3.7, 3.8)

    private static float[] Ramp(float oldGain, float newGain, float[] input, uint cfg = 0x4101, bool processLfe = false, int stride = -1)
    {
        var data = (float[])input.Clone();
        WwiseOutputGainStage.Apply(Mono(data, input.Length, cfg, stride), processLfe, oldGain, newGain);
        return data;
    }

    [Fact]
    public void M6_013_C45_3_The_ramp_runs_NEON_lane_gains_over_the_aligned_part_and_the_scalar_tail_restarts_at_the_old_gain()
    {
        // research 3.7: valid = 6, old = 1.0, new = 2.0: A = 4, d = (2 - 1) / 4 = 0.25, lanes {1.0, 1.25, 1.5, 1.75} (sample 0 gets exactly `old`); the tail (samples 4, 5) restarts at g = old with d2 = (2 - 1) / 6: 1.0, then 1.0 + d2.
        var o = Ramp(1f, 2f, new[] { 1f, 1f, 1f, 1f, 1f, 1f });
        Assert.Equal(new[] { 1f, 1.25f, 1.5f, 1.75f, 1f, 1f + 1f / 6f }, o);
        Assert.Equal(0x3F955555u, BitConverter.SingleToUInt32Bits(o[5]));
        // fewer than four frames: no NEON part, the whole buffer is the tail with d2 = (new - old) / valid
        var small = Ramp(1f, 2f, new[] { 1f, 1f, 1f });
        Assert.Equal(new[] { 1f, 1f + 1f / 3f, 1f + 1f / 3f + 1f / 3f }, small);
        // two blocks of four: lanes advance by d * 4.0f per block (d = 1 / 8)
        var two = Ramp(1f, 2f, Enumerable.Repeat(1f, 8).ToArray());
        Assert.Equal(new[] { 1f, 1.125f, 1.25f, 1.375f, 1.5f, 1.625f, 1.75f, 1.875f }, two);
    }

    [Fact]
    public void M6_013_C45_3_The_lane_gains_are_built_by_single_precision_adds_in_the_engines_order()
    {
        // lanes {old, old + d, d + (old + d), d + (d + (old + d))} (0xAA2D38..0xAA2D44), not old + k * d: with old = 0.1f, new = 0.9f, d = 0.2f the lane 3 is d + (d + (old + d)), which differs from 0.1f + 3 * 0.2f in the last bit.
        float old = 0.1f, nw = 0.9f;
        float d = (nw - old) / 4f;
        float l1 = old + d, l2 = d + l1, l3 = d + l2;
        var o = Ramp(old, nw, new[] { 1f, 1f, 1f, 1f });
        Assert.Equal(new[] { old, l1, l2, l3 }, o);
        Assert.NotEqual(BitConverter.SingleToUInt32Bits(old + 3 * d), BitConverter.SingleToUInt32Bits(o[3]));
    }

    [Fact]
    public void M6_013_C45_3_The_equal_gain_path_skips_a_gain_of_exactly_1_and_multiplies_by_the_old_gain_otherwise()
    {
        Assert.Equal(new[] { 1f, 0.5f, -0.25f }, Ramp(1f, 1f, new[] { 1f, 0.5f, -0.25f }));
        Assert.Equal(new[] { 2f, 1f, -0.5f, 4f, 8f }, Ramp(2f, 2f, new[] { 1f, 0.5f, -0.25f, 2f, 4f }));
    }

    [Fact]
    public void M6_013_C45_3_The_NEON_part_flushes_denormals_to_zero_and_the_scalar_tail_does_not()
    {
        // research 3.8: vmul.f32 q8 / vadd.f32 q9 are always flush-to-zero; the scalar VFP tail follows FPSCR (IEEE here). Equal gains (2.0): the first 4 * (n / 4) = 4 frames are NEON, the fifth is scalar.
        float den = BitConverter.UInt32BitsToSingle(0x00000400);                      // a denormal
        var o = Ramp(2f, 2f, new[] { den, den, den, den, den });
        Assert.Equal(new[] { 0u, 0u, 0u, 0u, 0x00000800u }, o.Select(BitConverter.SingleToUInt32Bits).ToArray());
        // the unequal path: the NEON blocks flush a denormal input, the restarting tail does not
        var u = Ramp(1f, 2f, new[] { den, den, den, den, den });
        Assert.Equal(new[] { 0u, 0u, 0u, 0u }, u.Take(4).Select(BitConverter.SingleToUInt32Bits).ToArray());
        Assert.Equal(0x00000400u, BitConverter.SingleToUInt32Bits(u[4]));            // tail g = old = 1.0: x * 1.0 keeps the denormal
        // a negative denormal flushes to -0.0
        var n = Ramp(2f, 2f, new[] { -den, -den, -den, -den });
        Assert.Equal(0x80000000u, BitConverter.SingleToUInt32Bits(n[0]));
    }

    [Fact]
    public void M6_013_C45_3_The_stage_runs_over_the_channels_the_flag_and_ProcessLFE_leave()
    {
        // research 3.7: ch = byte [S+4], minus 1 when ProcessLFE == 0 and u32 [S+4] & 0x8000 (the LFE plane, the last, is left alone); planes are u16 [S+0xC] frames apart.
        var d3 = new float[15];                                                       // 3 planes of stride 5 (4 valid)
        Array.Fill(d3, 1f, 0, 4); Array.Fill(d3, 1f, 5, 4); Array.Fill(d3, 1f, 10, 4);
        var s = new WwiseDecodeState { Data = d3, ChannelConfig = 0x7103 | 0x8000, MaxFrames = 5, ValidFrames = 4 };
        WwiseOutputGainStage.Apply(s, processLfe: false, 2f, 2f);
        Assert.Equal(new float[] { 2, 2, 2, 2, 0, 2, 2, 2, 2, 0, 1, 1, 1, 1, 0 }, d3);   // planes 0 and 1 doubled over 4 frames, plane 2 (LFE) untouched, the stride gaps untouched
        var d3b = new float[15];
        Array.Fill(d3b, 1f, 0, 4); Array.Fill(d3b, 1f, 5, 4); Array.Fill(d3b, 1f, 10, 4);
        WwiseOutputGainStage.Apply(new WwiseDecodeState { Data = d3b, ChannelConfig = 0x7103 | 0x8000, MaxFrames = 5, ValidFrames = 4 }, processLfe: true, 2f, 2f);
        Assert.Equal(new float[] { 2, 2, 2, 2, 0, 2, 2, 2, 2, 0, 2, 2, 2, 2, 0 }, d3b);
    }

    // ------------------------------------------------------------------ through the Execute entry (research 3.4, 3.7, V2)

    [Fact]
    public void M6_013_C45_3_Execute_applies_the_output_gain_with_powf_so_a_0_dB_level_is_exactly_1_and_leaves_the_samples_alone()
    {
        // research V2: the output-gain stage is powf(10.0f, dB * 0.05f), not the fast pow of the voice volumes: at 0 dB the engine's gain is exactly 1.0 (the C# fast pow gave 0.999039) and the equal-gain path skips the multiply.
        // All three bands off: the output is the input bit for bit.
        var (eq, _, _) = Make(BlockWith(0f, 0, (6, 0f, 1000f, 1f, 0), (6, 0f, 1000f, 1f, 0), (6, 0f, 1000f, 1f, 0)));
        Assert.Equal(1f, eq.PreviousGain);
        var data = new[] { 0.1f, -0.3f, 0.7f, 0.9f, -1f };
        eq.Execute(Mono(data, 5));
        Assert.Equal(new[] { 0.1f, -0.3f, 0.7f, 0.9f, -1f }, data);
        Assert.Equal(1f, eq.PreviousGain);
    }

    [Fact]
    public void M6_013_C45_3_A_changed_output_level_ramps_across_the_buffer_through_Execute()
    {
        // +6 dB written by SetParam id 15 after the dirty bytes are clear: new = powf(10.0f, 6.0f * 0.05f) = 1.9952624 (0x3FFF64C2: 10^0.3 correctly rounded, computed independently at 200 bits); old = 1.0 (0 dB at Init). valid = 5: lanes over A = 4, d = (new - 1) / 4, the tail sample restarts at old = 1.0.
        var (eq, p, _) = Make(BlockWith(0f, 0, (6, 0f, 1000f, 1f, 0), (6, 0f, 1000f, 1f, 0), (6, 0f, 1000f, 1f, 0)));
        eq.Execute(Mono(new float[2], 2));
        p.SetParam(15, BitConverter.GetBytes(6f));
        var data = new[] { 1f, 1f, 1f, 1f, 1f };
        eq.Execute(Mono(data, 5));
        float nw = BitConverter.UInt32BitsToSingle(0x3FFF64C2);
        float d = (nw - 1f) / 4f;
        float l1 = 1f + d, l2 = d + l1, l3 = d + l2;
        Assert.Equal(new[] { 1f, l1, l2, l3, 1f }, data);                              // sample 4 = the restarted tail: x * old
        Assert.Equal(BitConverter.SingleToUInt32Bits(nw), BitConverter.SingleToUInt32Bits(eq.PreviousGain));
    }

    [Fact]
    public void M6_013_C45_3_Term_frees_the_state_then_the_object_and_returns_1()
    {
        var (eq, _, alloc) = Make(Block((4, 0, 100, 1, 1), (4, 0, 100, 1, 1), (4, 0, 100, 1, 1)), fmtWord: 0x3102);
        var state = eq.StateSnapshot;
        Assert.NotNull(state);
        int n0 = alloc.Freed.Count;
        Assert.Equal(1, eq.Term(alloc));
        Assert.Equal(2, alloc.Freed.Count - n0);
        Assert.Same(eq, alloc.Freed[^1]);
        Assert.Null(eq.StateSnapshot);
        Assert.Throws<WwiseMissingBehaviourException>(() => eq.Execute(Mono(new float[4], 4, 0x3102)));
    }
}
