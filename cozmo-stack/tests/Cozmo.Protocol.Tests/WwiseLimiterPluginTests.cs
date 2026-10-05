using System.Runtime.InteropServices;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The Peak Limiter plug-in (<c>0x006E0003</c>, vptr <c>0x103DF58</c>; M6-013, correction C45, research 20261005-B-M6b-4-bus-fx-17.md sections 2.6..2.8 and 4 with the Verification's build spec) against the engine's own code under Unicorn (<c>re-analysis/tools/emu/emu_limiter.py</c> -> <see cref="WwiseLimiterOracle"/>): the parameter
/// object creator <c>0xAA2280</c> and its vtable (<c>0xAA20FC</c>, <c>0xAA2084</c>, <c>0xAA216C</c>, <c>0xAA1FC4</c>), Create <c>0xAA18F4</c>, Init <c>0xAA1B98</c>, Setup <c>0xAA19CC</c>, Reset <c>0xAA0940</c>, Term <c>0xAA0894</c>, GetPluginInfo <c>0xAA0904</c>, Execute <c>0xAA1BD8</c> with the unlinked / mono process P2
/// <c>0xAA0EB4</c>, the NoMoreData flush path and the output-gain stage. The expected values are the engine's, never this implementation's; the hand tests below take their numbers from the research sections named on each. The linked processes P1 <c>0xAA09B8</c> / P3 <c>0xAA1464</c> and P2's LFE swap are required stops
/// (the inventory reads them structurally only): their tests assert the <see cref="WwiseMissingBehaviourException"/>.
/// </summary>
public class WwiseLimiterPluginTests
{
    private static bool IsNaN(uint v) => (v & 0x7F800000) == 0x7F800000 && (v & 0x7FFFFF) != 0;

    private static string Canon(float f) { uint b = BitConverter.SingleToUInt32Bits(f); return (IsNaN(b) ? 0x7FC00000u : b).ToString("X8"); }

    private static string W(float? f) => f is null ? "-" : Canon(f.Value);

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

    private static readonly (int Off, int Size)[] ParamFields = { (4, 4), (8, 4), (0xC, 4), (0x10, 4), (0x14, 1), (0x18, 4), (0x1C, 1), (0x1D, 1), (0x1E, 1) };

    private static string Snap(WwiseLimiterParams p) =>
        string.Join(",", ParamFields.Select(f => f.Size == 4 ? p.Word(f.Off).ToString("X8") : p.Byte(f.Off).ToString("X2")));

    private static string ProcessName(WwiseLimiterPlugin.ProcessKind k) => k switch
    {
        WwiseLimiterPlugin.ProcessKind.P2 => "P2",
        WwiseLimiterPlugin.ProcessKind.P1 => "P1",
        WwiseLimiterPlugin.ProcessKind.P3 => "P3",
        _ => "0",                       // Create zeroes [+4] / [+8]
    };

    private static string InitString(WwiseLimiterPlugin lim, int rc) =>
        rc != 1 && rc != 0x34 ? rc.ToString() :
        $"{rc} {BitConverter.SingleToUInt32Bits(lim.PreviousGain):X8} {ProcessName(lim.Process)} {lim.Rate:X} {lim.ChannelWord:X} {lim.ProcessedChannels:X} {lim.DetectorSlots:X} {lim.LookAheadFrames:X} {lim.TailCounter:X} {lim.TailMax:X8} " +
        $"{W(lim.AttackCoefficientIfSet)} {(lim.RingSnapshot is null ? 0 : 1)} {(lim.DetectorSnapshot is null ? 0 : 1)} {(lim.WriteIndexIfSet is { } w ? w.ToString("X8") : "-")} {W(lim.ReleaseCoefficientIfSet)}";

    private static string Execute(WwiseLimiterPlugin lim, WwiseLimiterParams p, uint fmt, uint cfg, string op)
    {
        var f = op.Split('.');
        int frames = int.Parse(f[1]), stride = int.Parse(f[2]), kind = int.Parse(f[3]);
        ulong seed = ulong.Parse(f[4]);
        uint estate = Convert.ToUInt32(f[5], 16);
        int planes = Math.Max(Math.Max((int)(fmt & 0xFF), (int)(cfg & 0xFF)), 1);
        var words = WwiseCompressorTests.Input(kind, seed, planes * stride);
        var data = new float[words.Length];
        for (int i = 0; i < data.Length; i++) data[i] = BitConverter.UInt32BitsToSingle(words[i]);
        var s = new WwiseDecodeState { Data = data, ChannelConfig = cfg, Scratch08 = estate, MaxFrames = (ushort)stride, ValidFrames = (ushort)frames };
        lim.Execute(s);
        string det = "-";
        var d = lim.DetectorSnapshot;
        if (d is not null && d.Length < 64)
        {
            var dw = new List<uint>();
            foreach (var (gr, pk, hold) in d) { dw.Add(IsNaN(gr) ? 0x7FC00000 : gr); dw.Add(IsNaN(pk) ? 0x7FC00000 : pk); dw.Add(hold); }
            det = WwiseResamplerTests.Sha(MemoryMarshal.AsBytes<uint>(dw.ToArray()));
        }
        string ring = lim.RingSnapshot is { } r && r.Length < 1 << 20 ? Hash(r) : "-";
        return $"E:{Hash(data)}:{Canon(lim.PreviousGain)}:{(lim.WriteIndexIfSet is { } w ? w.ToString("X8") : "-")}:{lim.TailCounter:X8}:{lim.TailMax:X8}:{W(lim.ReleaseCoefficientIfSet)}:{det}:{ring}:{(lim.JustReset ? 1 : 0):X2}:{s.Scratch08:X8}:{s.ValidFrames}:{p.DirtyRelease:X2}:{p.DirtySetup:X2}";
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
        var p0 = WwiseLimiterParams.Create(alloc)!;
        if (block is null) p0.SetParamsBlockOrDefaults(null, 0); else p0.SetParamsBlockOrDefaults(block, 22);
        var p = clone ? p0.Clone(alloc)! : p0;
        var lim = WwiseLimiterPlugin.Create(alloc)!;
        int rc = lim.Init(alloc, null, p, new WwiseEffectFormat(rate, fmt));
        string init = InitString(lim, rc);

        var res = new List<string>();
        foreach (var op in ops)
        {
            switch (op[0])
            {
                case 'R': res.Add($"R:{lim.Reset()}"); break;
                case 'E': res.Add(Execute(lim, p, fmt, cfg, op)); break;
                case 'P':
                {
                    var f = op.Split('.');
                    int r = p.SetParam(uint.Parse(f[1]), BitConverter.GetBytes(Convert.ToUInt32(f[2], 16)));
                    res.Add($"P:{r}:{Snap(p)}");
                    break;
                }
                case 'B': res.Add($"B:{p.SetParamsBlockOrDefaults(Convert.FromHexString(op[2..]), 22)}:{Snap(p)}"); break;
                case 'D': res.Add($"D:{p.SetParamsBlockOrDefaults(null, 0)}:{Snap(p)}"); break;
                case 'I':
                {
                    int r = ((IWwiseEffectPlugin)lim).GetPluginInfo(out var info);
                    res.Add($"I:{r}:{info.Word0:X8},{info.Word4:X8},{info.Byte8:X2},{info.ByteA:X2}");
                    break;
                }
                case 'T':
                {
                    int n0 = alloc.Freed.Count;
                    int r = lim.Term(alloc);
                    res.Add($"T:{r}:{alloc.Freed.Count - n0}");
                    break;
                }
                default: throw new InvalidOperationException(op);
            }
        }
        return (init, string.Join(" ; ", res));
    }

    [Fact]
    public void M6_013_C45_4_Every_oracle_life_matches_the_engine_Init_0xAA1B98_Setup_0xAA19CC_Reset_0xAA0940_and_Execute_0xAA1BD8_with_P2_0xAA0EB4()
    {
        // 3176 lives of the engine's own code: the two shipped blocks (Robot_Bus_Peak_Limiter 0xDF2230FF, Cozmo_SFX_Bus_Limiter 0x3ABE7001) at nine rates with a gain change (the NEON ramp), random blocks on 1..6 channels (the unlinked path; the linked block with one processed channel selects P2), the prescan and the
        // ring wrap, SetParam ids 0..9, block / defaults operations, the NoMoreData flush path in every shape (V = 0, V small, V = L, V > L, M = V and M much larger, normal calls in between), denormal-scale, denormal, NaN / inf and random-pattern audio, every Init channel count,
        // the failing ring and detector allocations, the instance with and without a Clone of the parameter object, the coefficient extremes, GetPluginInfo and Term.
        Assert.True(WwiseLimiterOracle.Cases.Length >= 3000);
        int bad = 0;
        string? first = null;
        foreach (var row in WwiseLimiterOracle.Cases)
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
        Assert.True(bad == 0, $"{bad} of {WwiseLimiterOracle.Cases.Length} cases differ; first:\n{first}");
    }

    [Fact]
    public void M6_013_C45_4_The_oracle_covers_every_input_kind_the_flush_path_the_failing_allocations_and_every_SetParam_id()
    {
        // Guards the oracle itself: a regenerated file that lost a group would still pass the equality test above.
        var kinds = new HashSet<int>();
        var ids = new HashSet<int>();
        int flushCalls = 0, tailRemains = 0, validChanged = 0, failedRing = 0, failedDet = 0, multi = 0, lfeFlag = 0, linked = 0, noClone = 0, defaults = 0, normalAfterFlush = 0;
        foreach (var row in WwiseLimiterOracle.Cases)
        {
            var part = row.Split(" | ");
            var head = part[0].Split(' ');
            var ops = part[2] == "-" ? Array.Empty<string>() : part[2].Split(' ');
            var results = part[4].Split(" ; ");
            if (part[3].StartsWith("52 ") && part[3].Split(' ')[11] == "0") failedRing++;
            else if (part[3].StartsWith("52 ")) failedDet++;
            if ((Convert.ToUInt32(head[1], 16) & 0xFF) > 1) multi++;
            if ((Convert.ToUInt32(head[1], 16) & 0x8000) != 0) lfeFlag++;
            if (part[1] != "-" && Convert.FromHexString(part[1])[0x15] != 0) linked++;
            if (head[4] == "0") noClone++;
            if (part[1] == "-") defaults++;
            bool sawFlush = false;
            for (int i = 0; i < ops.Length && i < results.Length; i++)
            {
                var o = ops[i];
                if (o.StartsWith("E."))
                {
                    var f = o.Split('.');
                    kinds.Add(int.Parse(f[3]));
                    if (f[5] == "11")
                    {
                        flushCalls++;
                        sawFlush = true;
                        var r = results[i].Split(':');
                        if (r[10] == "0000002D") tailRemains++;
                        if (r[11] != f[1]) validChanged++;
                    }
                    else if (sawFlush) normalAfterFlush++;
                }
                else if (o.StartsWith("P.")) ids.Add(int.Parse(o.Split('.')[1]));
            }
        }
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, kinds.OrderBy(k => k).ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 9 }, ids.OrderBy(k => k).ToArray());
        Assert.True(flushCalls > 1000 && tailRemains > 250 && validChanged > 500 && failedRing >= 15 && failedDet >= 15 && multi > 400 && lfeFlag > 100 && linked > 500 && noClone > 400 && defaults >= 40 && normalAfterFlush > 100,
            $"{flushCalls} {tailRemains} {validChanged} {failedRing} {failedDet} {multi} {lfeFlag} {linked} {noClone} {defaults} {normalAfterFlush}");
    }

    [Fact]
    public void M6_013_The_host_expf_and_powf_are_the_correctly_rounded_float32_for_every_argument_the_limiter_produces()
    {
        // EQUIVALENT_IMPLEMENTATION of the phone's libm (PLT 0x4D0058 expf, 0x4D6778 powf): double precision rounded once equals the 200-bit correctly rounded binary32 for expf(-2.2f / (L / 2)) and expf(-2.2f / (rate * release)) of the shipped blocks and a grid at the realistic rates, and powf(10.0f, dB * 0.05f).
        Assert.True(WwiseLimiterOracle.ExpfProof.Length >= 100 && WwiseLimiterOracle.PowfProof.Length >= 8);
        foreach (var row in WwiseLimiterOracle.ExpfProof)
        {
            var f = row.Split(' ');
            uint arg = Convert.ToUInt32(f[0], 16), expected = Convert.ToUInt32(f[1], 16);
            uint got = BitConverter.SingleToUInt32Bits(WwiseHostMath.Expf(BitConverter.UInt32BitsToSingle(arg)));
            if (IsNaN(expected)) Assert.True(IsNaN(got), row);
            else Assert.True(expected == got, $"{row}: got {got:X8}");
        }
        foreach (var row in WwiseLimiterOracle.PowfProof)
        {
            var f = row.Split(' ');
            uint y = Convert.ToUInt32(f[0], 16), expected = Convert.ToUInt32(f[1], 16);
            uint got = BitConverter.SingleToUInt32Bits(WwiseHostMath.Powf(10f, BitConverter.UInt32BitsToSingle(y)));
            Assert.True(expected == got, $"{row}: got {got:X8}");
        }
    }

    // ------------------------------------------------------------------ hand tests from the research sections

    private static byte[] Block(float thr, float ratio, float look, float rel, float outDb, byte plfe, byte link)
    {
        var b = new byte[22];
        BitConverter.TryWriteBytes(b.AsSpan(0, 4), thr);
        BitConverter.TryWriteBytes(b.AsSpan(4, 4), ratio);
        BitConverter.TryWriteBytes(b.AsSpan(8, 4), look);
        BitConverter.TryWriteBytes(b.AsSpan(12, 4), rel);
        BitConverter.TryWriteBytes(b.AsSpan(16, 4), outDb);
        b[20] = plfe;
        b[21] = link;
        return b;
    }

    private static byte[] Robot => Block(-1f, 10.8f, 0.009f, 0.041f, 0f, 0, 0);

    private static void Clear(WwiseLimiterParams p)
    {
        p.ClearDirtyRelease();
        p.ClearDirtySetup();
    }

    private static (WwiseLimiterPlugin Lim, WwiseLimiterParams P, WwisePluginAllocator A) Make(byte[] block, uint rate = 48000, uint fmtWord = 0x4101, bool reset = true)
    {
        var alloc = new WwisePluginAllocator();
        var p0 = WwiseLimiterParams.Create(alloc)!;
        p0.SetParamsBlockOrDefaults(block, 22);
        var p = p0.Clone(alloc)!;
        var lim = WwiseLimiterPlugin.Create(alloc)!;
        Assert.Equal(1, lim.Init(alloc, null, p, new WwiseEffectFormat(rate, fmtWord)));
        if (reset) lim.Reset();
        return (lim, p, alloc);
    }

    private static WwiseDecodeState Mono(float[] data, int valid, uint cfg = 0x4101, int stride = -1, uint estate = 0x2B) =>
        new() { Data = data, ChannelConfig = cfg, Scratch08 = estate, MaxFrames = (ushort)(stride < 0 ? valid : stride), ValidFrames = (ushort)valid };

    [Fact]
    public void M6_013_C45_2_The_defaults_of_a_zero_size_block_are_the_words_0xAA2108_to_0xAA2154_store()
    {
        // research 2.7: [0xC]=0x3E4CCCCD (0.2), [0x18]=0x3C23D70A (0.01), [4]=0xC1400000 (-12.0), [8]=0x41200000 (10.0), [0x10]=0x3F800000 (1.0), bytes [0x14], [0x1C], [0x1D], [0x1E] = 1.
        var p = WwiseLimiterParams.Create(new WwisePluginAllocator())!;
        Assert.Equal(1, p.SetParamsBlockOrDefaults(null, 0));
        foreach (var (off, v) in new (int, uint)[] { (0xC, 0x3E4CCCCD), (0x18, 0x3C23D70A), (4, 0xC1400000), (8, 0x41200000), (0x10, 0x3F800000) }) Assert.Equal(v, p.Word(off));
        foreach (int off in new[] { 0x14, 0x1C, 0x1D, 0x1E }) Assert.Equal((byte)1, p.Byte(off));
    }

    [Fact]
    public void M6_013_C45_2_SetParamsBlock_0xAA2084_maps_the_block_and_stores_the_output_level_as_powf_10_v_times_0_05()
    {
        // research 2.6: b0->[4], b4->[8], b8->[0x18], b0xC->[0xC], powf(10.0f, (b0x10 as float) * 0.05f)->[0x10], byte b0x14->[0x1C], byte b0x15->[0x1D]; bytes [0x14] = [0x1E] = 1.
        var p = WwiseLimiterParams.Create(new WwisePluginAllocator())!;
        Assert.Equal(1, p.SetParamsBlockOrDefaults(Block(-1f, 10.8f, 0.009f, 0.041f, 6f, 1, 2), 22));
        Assert.Equal(0xBF800000u, p.Word(4));
        Assert.Equal(0x412CCCCDu, p.Word(8));
        Assert.Equal(0x3C1374BCu, p.Word(0x18));
        Assert.Equal(0x3D27EF9Eu, p.Word(0xC));
        Assert.Equal(0x3FFF64C2u, p.Word(0x10));           // 10^(6 * 0.05f) = 10^0.3 correctly rounded, computed independently at 200 bits
        Assert.Equal((byte)1, p.Byte(0x1C));
        Assert.Equal((byte)2, p.Byte(0x1D));
        Assert.Equal((byte)1, p.Byte(0x14));
        Assert.Equal((byte)1, p.Byte(0x1E));
        p.SetParamsBlockOrDefaults(Block(0f, 1f, 0.01f, 0.1f, 0f, 0, 0), 22);
        Assert.Equal(0x3F800000u, p.Word(0x10));           // powf(10, 0) is exactly 1.0 (the C# fast pow gave 0.999039: research V2)
        // a block shorter than 22 bytes is not an engine input
        Assert.Throws<WwiseMissingBehaviourException>(() => p.SetParamsBlockOrDefaults(new byte[10], 10));
    }

    [Fact]
    public void M6_013_C45_2_SetParam_0xAA216C_maps_ids_0_to_6_with_the_two_dirty_bytes()
    {
        // research 2.8: 0 -> [4] (dirty [0x14]); 1 -> [8] (dirty 0x14); 2 -> [0x18] (dirty [0x1E]); 3 -> [0xC] (dirty 0x14); 4 -> [0x10] = powf(10.0f, v * 0.05f) (dirty 0x14); 5 -> byte [0x1C] (dirty 0x1E); 6 -> byte [0x1D] (dirty 0x1E); otherwise 0x1F.
        var (lim, p, _) = Make(Robot);
        lim.Execute(Mono(new float[4], 4));                                           // clears [0x14] (step a) and [0x1E] (Setup in Init cleared it; the Clone set it again, so Execute's step b runs Setup)
        Assert.Equal((byte)0, p.DirtyRelease);
        Assert.Equal((byte)0, p.DirtySetup);
        static byte[] F(float f) => BitConverter.GetBytes(f);
        foreach (var (id, off, dirtyRelease) in new[] { (0u, 4, true), (1u, 8, true), (2u, 0x18, false), (3u, 0xC, true) })
        {
            Assert.Equal(1, p.SetParam(id, F(2.5f + id)));
            Assert.Equal(BitConverter.SingleToUInt32Bits(2.5f + id), p.Word(off));
            Assert.Equal((byte)(dirtyRelease ? 1 : 0), p.DirtyRelease);
            Assert.Equal((byte)(dirtyRelease ? 0 : 1), p.DirtySetup);
            Clear(p);
        }
        Assert.Equal(1, p.SetParam(4, F(6f)));
        Assert.Equal(0x3FFF64C2u, p.Word(0x10));
        Assert.Equal((byte)1, p.DirtyRelease);
        Clear(p);
        Assert.Equal(1, p.SetParam(5, new byte[] { 1 }));
        Assert.Equal((byte)1, p.Byte(0x1C));
        Assert.Equal((byte)1, p.DirtySetup);
        Clear(p);
        Assert.Equal(1, p.SetParam(6, new byte[] { 3 }));
        Assert.Equal((byte)3, p.Byte(0x1D));
        Assert.Equal((byte)1, p.DirtySetup);
        Assert.Equal(0x1F, p.SetParam(7, F(1f)));
        Assert.Equal(0x1F, p.SetParam(0xFFFFFFFF, F(1f)));
    }

    [Fact]
    public void M6_013_C45_2_The_creator_stores_only_the_vptr_and_Clone_0xAA1FC4_copies_the_words_and_sets_both_dirty_bytes()
    {
        var alloc = new WwisePluginAllocator();
        var p = WwiseLimiterParams.Create(alloc)!;
        Assert.Equal(new[] { 0x20 }, alloc.Sizes);
        Assert.Throws<WwiseMissingBehaviourException>(() => p.Threshold);              // uninitialised pool memory
        Assert.Throws<WwiseMissingBehaviourException>(() => p.DirtyRelease);
        p.SetParamsBlockOrDefaults(Block(-3f, 4f, 0.02f, 0.3f, 3f, 1, 0), 22);
        Clear(p);
        var q = p.Clone(alloc)!;
        Assert.Equal(0x20, alloc.Sizes[^1]);
        foreach (int off in new[] { 4, 8, 0xC, 0x10, 0x18 }) Assert.Equal(p.Word(off), q.Word(off));
        Assert.Equal((byte)1, q.Byte(0x1C));
        Assert.Equal((byte)1, q.DirtyRelease);
        Assert.Equal((byte)1, q.DirtySetup);
        Assert.Null(p.Clone(new WwisePluginAllocator { FailAt = 1 }));
        Assert.Equal(1, WwiseLimiterParams.Destroy(null, alloc));
        Assert.Equal(1, WwiseLimiterParams.Destroy(q, alloc));
        Assert.Same(q, alloc.Freed[^1]);
    }

    [Fact]
    public void M6_013_C45_4_Create_and_the_slots_return_what_the_binary_returns()
    {
        // Create 0xAA18F4: allocates 0x50 bytes; [0x3C] = -1; [0x40] = 0. Info 0xAA0904 = {3, 0x7E002, byte[8] = 1, byte[0xA] = 0}; vt+0x14 / vt+0x18 0x8DBF38 / 0x8DBF3C (return 0); vt+0x24 0xAA1FB8 (returns 0x2D); the plug-in id 0x006E0003, the vptr 0x103DF58.
        var alloc = new WwisePluginAllocator();
        var lim = WwiseLimiterPlugin.Create(alloc)!;
        Assert.Equal(new[] { 0x50 }, alloc.Sizes);
        Assert.Equal(0xFFFFFFFFu, lim.TailCounter);
        Assert.Equal(0u, lim.TailMax);
        Assert.Null(WwiseLimiterPlugin.Create(new WwisePluginAllocator { FailAt = 1 }));
        IWwiseEffectPlugin plug = lim;
        Assert.Equal(1, plug.GetPluginInfo(out var info));
        Assert.Equal(new WwisePluginInfo(3, 0x7E002, 1, 0), info);
        Assert.Equal(0, plug.Slot14());
        Assert.Equal(0, plug.Slot18());
        Assert.Equal(0x2D, plug.Slot24());
        Assert.Equal(0x006E0003u, WwiseLimiterPlugin.PluginId);
        Assert.Equal(0x103DF58u, WwiseLimiterPlugin.Vptr);
    }

    [Fact]
    public void M6_013_C45_4_Setup_0xAA19CC_computes_L_the_attack_coefficient_and_the_allocations_of_the_Robot_Bus_limiter()
    {
        // research 4.3 at 48000 Hz and 0.009 s: L = u32(float(48000) * 0.009f) = 431 (the float product truncates; 432 would be wrong); attack = expf(-2.2f / (L / 2)) = 0x3F7D665C (the engine's, the oracle's init rows);
        // the ring is byte [0x1C] * L * 4 bytes, the detectors [0x28] * 12; [0x38] = 0; [0x3C] = -1 (Create); param[0x1E] = 0.
        var alloc = new WwisePluginAllocator();
        var p = WwiseLimiterParams.Create(alloc)!;
        p.SetParamsBlockOrDefaults(Robot, 22);
        var lim = WwiseLimiterPlugin.Create(alloc)!;
        Assert.Equal(1, lim.Init(alloc, null, p, new WwiseEffectFormat(48000, 0x7103)));
        Assert.Equal(431u, lim.LookAheadFrames);
        Assert.Equal(0x3F7D665Cu, BitConverter.SingleToUInt32Bits(lim.AttackCoefficient));
        Assert.Equal(new[] { 0x20, 0x50, 3 * 431 * 4, 3 * 12 }, alloc.Sizes.ToArray());
        Assert.Equal(3u, lim.ProcessedChannels);
        Assert.Equal(3u, lim.DetectorSlots);
        Assert.Equal(0u, lim.WriteIndex);
        Assert.Equal(WwiseLimiterPlugin.ProcessKind.P2, lim.Process);
        Assert.Equal((byte)0, p.DirtySetup);
        Assert.Equal(0x3F800000u, BitConverter.SingleToUInt32Bits(lim.PreviousGain));   // param[0x10] = powf(10, 0 * 0.05f) = 1.0
    }

    [Fact]
    public void M6_013_C45_4_Setup_selects_the_process_function_by_ChannelLink_the_channel_count_and_the_LFE_flag()
    {
        // research 4.3: ChannelLink 0 -> P2; linked and one processed channel -> P2; linked and the LFE bit (u32 [0x1C] & 0x8000) clear -> P3; linked, LFE bit set and ProcessLFE != 0 -> P3; linked, LFE bit set and ProcessLFE == 0 -> P1.
        WwiseLimiterPlugin.ProcessKind Kind(byte plfe, byte link, uint word)
        {
            var (lim, _, _) = Make(Block(-1f, 10.8f, 0.009f, 0.041f, 0f, plfe, link), 48000, word, reset: false);
            return lim.Process;
        }
        Assert.Equal(WwiseLimiterPlugin.ProcessKind.P2, Kind(0, 0, 0x3102));
        Assert.Equal(WwiseLimiterPlugin.ProcessKind.P2, Kind(1, 1, 0x4101));
        Assert.Equal(WwiseLimiterPlugin.ProcessKind.P3, Kind(0, 1, 0x3102));
        Assert.Equal(WwiseLimiterPlugin.ProcessKind.P3, Kind(1, 1, 0x3102 | 0x8000));
        Assert.Equal(WwiseLimiterPlugin.ProcessKind.P1, Kind(0, 1, 0x7103 | 0x8000));
        Assert.Equal(WwiseLimiterPlugin.ProcessKind.P2, Kind(0, 1, 0x3102 | 0x8000));   // two channels minus the LFE plane = one processed channel
    }

    [Fact]
    public void M6_013_C45_4_The_linked_processes_P1_and_P3_and_the_LFE_swap_of_P2_are_required_stops()
    {
        // research 4.8 (structural read only): Execute throws when it reaches P1 / P3, or P2 with ProcessLFE 0 and the buffer's 0x8000 flag, instead of inventing their arithmetic.
        var (p3, _, _) = Make(Block(-1f, 10.8f, 0.009f, 0.041f, 0f, 0, 1), 48000, 0x3102);
        var ex3 = Assert.Throws<WwiseMissingBehaviourException>(() => p3.Execute(Mono(new float[2 * 8], 8, 0x3102)));
        Assert.Contains("P3", ex3.Message);
        var (p1, _, _) = Make(Block(-1f, 10.8f, 0.009f, 0.041f, 0f, 0, 1), 48000, 0x7103 | 0x8000);
        var ex1 = Assert.Throws<WwiseMissingBehaviourException>(() => p1.Execute(Mono(new float[3 * 8], 8, 0x7103 | 0x8000)));
        Assert.Contains("P1", ex1.Message);
        var (p2, _, _) = Make(Block(-1f, 10.8f, 0.009f, 0.041f, 0f, 0, 0), 48000, 0x3102 | 0x8000);   // ChannelLink 0: P2, one processed channel + the LFE plane
        var exs = Assert.Throws<WwiseMissingBehaviourException>(() => p2.Execute(Mono(new float[2 * 8], 8, 0x3102 | 0x8000)));
        Assert.Contains("LFE swap", exs.Message);
        // the same object without the buffer's flag runs P2 (ProcessLFE 0 and no flag: nothing to swap)
        p2.Execute(Mono(new float[2 * 8], 8, 0x3102));
    }

    [Fact]
    public void M6_013_C45_4_Execute_stores_the_release_coefficient_at_the_first_call_and_the_ring_delays_the_signal_by_L()
    {
        // research 4.5(a): release = expf(-2.2f / (float(rate) * param[0xC])) = 0x3F7FB6C7 at 48000 Hz and 0.041 s (the engine's, the oracle's rows); the byte [0x14] is cleared. Research 4.7: the signal is delayed by L = 431 frames (the ring starts zeroed by Reset);
        // an impulse well below the -1 dB threshold is limited by nothing, so it comes out at 431 times the detector gain at zero reduction (the fast power's polynomial value 0.9990390, not 1.0: the engine's fastpow10 at e = 0) and NOT times a second factor: the 0 dB output gain is powf(10, 0) = 1.0 exactly (research V2).
        var (lim, p, _) = Make(Robot);
        Assert.Null(lim.ReleaseCoefficientIfSet);
        Assert.Equal((byte)1, p.DirtyRelease);
        var data = new float[500];
        data[0] = 0.5f;
        lim.Execute(Mono(data, 500));
        Assert.Equal(0x3F7FB6C7u, BitConverter.SingleToUInt32Bits(lim.ReleaseCoefficient));
        Assert.Equal((byte)0, p.DirtyRelease);
        for (int i = 0; i < 431; i++) Assert.Equal(0f, data[i]);
        Assert.InRange(data[431], 0.5f * 0.9990389f - 1e-6f, 0.5f * 0.9990390f + 1e-6f);
        Assert.Equal(1f, lim.PreviousGain);
        Assert.Equal(69u, lim.WriteIndex);                                             // (500 mod 431): the ring wrapped once
    }

    [Fact]
    public void M6_013_C45_4_A_full_scale_impulse_is_pushed_back_by_the_detector()
    {
        // research 4.7: with the -1 dB threshold and 10.8:1 the target for a 0 dB sample is about 1 dB over: the delayed sample at L is attenuated. Bounds only (the exact bits are in the oracle rows).
        var (lim, _, _) = Make(Robot);
        var data = new float[432];
        data[0] = 1f;
        lim.Execute(Mono(data, 432));
        Assert.InRange(data[431], 0.85f, 0.999f);
    }

    [Fact]
    public void M6_013_C45_4_The_just_reset_prescan_sets_hold_to_the_remaining_count_at_the_last_tied_maximum()
    {
        // research 4.7 / V4: the prescan walks the first min(V, L) samples; a < peak keeps the peak, otherwise peak = a and hold = the remaining count (ties update). Ten samples of 0.5: the last tie is the tenth, remaining 1 -> hold 1.
        // The first sample's hold != 0 decrements it to 0; the second recomputes (hold = L = 431); the remaining eight samples decrement: 431 - 8 = 423. A prescan that set hold = L would end at 431 - 10 = 421.
        var (lim, _, _) = Make(Robot);
        var data = Enumerable.Repeat(0.5f, 10).ToArray();
        lim.Execute(Mono(data, 10));
        var det = lim.DetectorSnapshot!;
        Assert.Single(det);
        Assert.Equal(423u, det[0].Hold);
        Assert.Equal(BitConverter.SingleToUInt32Bits(0.5f), det[0].Peak);
        Assert.False(lim.JustReset);                                                  // cleared after the last channel
    }

    [Fact]
    public void M6_013_C45_4_Reset_zeroes_the_ring_and_the_detectors_and_leaves_the_gain_the_write_index_the_tail_and_the_release_alone()
    {
        // research 4.4: Reset does not reset [0x14], [0x38], [0x3C], [0x44]; byte [0x4C] = 1.
        var (lim, p, _) = Make(Block(-1f, 10.8f, 0.009f, 0.041f, 6f, 0, 0));
        var data = Enumerable.Repeat(0.9f, 100).ToArray();
        lim.Execute(Mono(data, 100));
        uint w = lim.WriteIndex, tail = lim.TailCounter;
        float rel = lim.ReleaseCoefficient, prev = lim.PreviousGain;
        Assert.False(lim.JustReset);
        Assert.NotEqual(0f, lim.RingSnapshot![0]);
        Assert.Equal(1, lim.Reset());
        Assert.True(lim.JustReset);
        Assert.All(lim.RingSnapshot!, v => Assert.Equal(0f, v));
        Assert.Equal((0u, 0u, 0u), lim.DetectorSnapshot![0]);
        Assert.Equal(w, lim.WriteIndex);
        Assert.Equal(tail, lim.TailCounter);
        Assert.Equal(rel, lim.ReleaseCoefficient);
        Assert.Equal(prev, lim.PreviousGain);
        Assert.NotNull(p);
    }

    [Fact]
    public void M6_013_C45_4_Init_does_not_zero_the_ring_or_the_detectors_so_Execute_before_Reset_stops_visibly()
    {
        var (lim, _, _) = Make(Robot, reset: false);
        Assert.Throws<WwiseMissingBehaviourException>(() => lim.Execute(Mono(new float[8], 8)));
    }

    [Fact]
    public void M6_013_C45_4_A_normal_call_with_no_valid_frames_sets_the_tail_counter_to_minus_1_and_returns()
    {
        // 0xAA1C4C..0xAA1C5C: ldrh r0,[r4,#0xe]; mvn r3,#0; str r3,[r5,#0x3c]; cmp r0,#0; beq (return). The release refresh (a) and the Setup (b) come before it.
        var (lim, _, _) = Make(Robot);
        var s = Mono(new float[8], 0, stride: 8);
        lim.Execute(s);
        Assert.Equal(0xFFFFFFFFu, lim.TailCounter);
        Assert.NotNull(lim.ReleaseCoefficientIfSet);
        Assert.Equal(0u, lim.WriteIndex);
    }

    [Fact]
    public void M6_013_C45_4_The_NoMoreData_flush_follows_the_tail_counter_arithmetic_and_stores_0x2D_only_while_the_tail_remains()
    {
        // research 4.6 / V7, L = 431. (1) T = -1 (after a normal call): the tail restarts ([0x3C] = [0x40] = L); room = M - V = 1024 > T = 431: the tail fits: [0x3C] = 0, 1024 frames of zeros are written, V = M, eState stays 0x11.
        var (lim, _, _) = Make(Robot);
        lim.Execute(Mono(Enumerable.Repeat(0.25f, 100).ToArray(), 100));
        Assert.Equal(0xFFFFFFFFu, lim.TailCounter);
        var s1 = Mono(Enumerable.Repeat(9f, 1024).ToArray(), 0, stride: 1024, estate: 0x11);
        lim.Execute(s1);
        Assert.Equal(0u, lim.TailCounter);
        Assert.Equal(431u, lim.TailMax);
        Assert.Equal(1024, s1.ValidFrames);
        Assert.Equal(0x11u, s1.Scratch08);
        // the 100 delayed samples come out after the 331 zeros of the ring (0.25 * detector gain), then zeros from the zero-fill
        Assert.True(s1.Data is float[]);
        var out1 = (float[])s1.Data!;
        for (int i = 0; i < 331; i++) Assert.Equal(0f, out1[i]);
        Assert.InRange(out1[331], 0.25f * 0.998f, 0.25f * 1.0f);
        for (int i = 431; i < 1024; i++) Assert.Equal(0f, out1[i]);

        // (2) T == 0 and V == 0: nothing to do, the valid count stays 0 and nothing is written.
        var s2 = Mono(Enumerable.Repeat(9f, 64).ToArray(), 0, stride: 64, estate: 0x11);
        lim.Execute(s2);
        Assert.Equal(0, s2.ValidFrames);
        Assert.All((float[])s2.Data!, v => Assert.Equal(9f, v));
        Assert.Equal(0u, lim.TailCounter);

        // (3) T == 0 and V != 0: restart; room = 600 - 100 = 500 > 431 fits again.
        var s3 = Mono(Enumerable.Repeat(0.25f, 600).ToArray(), 100, stride: 600, estate: 0x11);
        lim.Execute(s3);
        Assert.Equal(600, s3.ValidFrames);
        Assert.Equal(0u, lim.TailCounter);
        Assert.Equal(0x11u, s3.Scratch08);

        // (4) a normal call, then a flush with M = 300: room = 300 <= T = 431: [0x3C] = 431 - 300 = 131, 300 zeros are written, V = 300, eState = 0x2D (the tail remains).
        lim.Execute(Mono(Enumerable.Repeat(0.25f, 100).ToArray(), 100));
        var s4 = Mono(Enumerable.Repeat(9f, 300).ToArray(), 0, stride: 300, estate: 0x11);
        lim.Execute(s4);
        Assert.Equal(131u, lim.TailCounter);
        Assert.Equal(300, s4.ValidFrames);
        Assert.Equal(0x2Du, s4.Scratch08);

        // (5) the next flush, V == 0 and T = 131 (not -1): R = [0x40] = 431, L > R is false, T is kept; room = 300 > 131 fits: [0x3C] = 0, V = 300, eState stays 0x11.
        var s5 = Mono(Enumerable.Repeat(9f, 300).ToArray(), 0, stride: 300, estate: 0x11);
        lim.Execute(s5);
        Assert.Equal(0u, lim.TailCounter);
        Assert.Equal(300, s5.ValidFrames);
        Assert.Equal(0x11u, s5.Scratch08);
    }

    [Fact]
    public void M6_013_C45_4_A_flush_with_a_longer_L_after_the_tail_started_extends_the_tail_by_the_difference()
    {
        // research 4.6: T != -1, V == 0, R = [0x40]; L > R (unsigned) sets [0x40] = L and T = L - (R - T). Set up T = 131 with [0x40] = 431 (as above), then a SetParam(2, 0.02 s) makes L = 960: Setup runs in step (b)
        // and Reset zeroes the ring but not [0x3C] / [0x40]; the flush then has T = 131, R = 431, L = 960: [0x40] = 960, T = 960 - (431 - 131) = 660.
        var (lim, p, _) = Make(Robot);
        lim.Execute(Mono(Enumerable.Repeat(0.25f, 100).ToArray(), 100));
        lim.Execute(Mono(new float[300], 0, stride: 300, estate: 0x11));
        Assert.Equal(131u, lim.TailCounter);
        Assert.Equal(431u, lim.TailMax);
        p.SetParam(2, BitConverter.GetBytes(0.02f));
        var s = Mono(new float[200], 0, stride: 200, estate: 0x11);
        lim.Execute(s);
        Assert.Equal(960u, lim.LookAheadFrames);
        Assert.Equal(960u, lim.TailMax);
        // room = 200 <= T = 660: [0x3C] = 460, V = 200, eState = 0x2D
        Assert.Equal(460u, lim.TailCounter);
        Assert.Equal(200, s.ValidFrames);
        Assert.Equal(0x2Du, s.Scratch08);
    }

    [Fact]
    public void M6_013_C45_4_A_failing_ring_allocation_returns_0x34_and_a_failing_detector_allocation_does_too()
    {
        // research 4.3: the ring failure stores [0x34] = 0 and returns 0x34 before [0x38] is written; the detector failure stores [0x30] = 0 and returns 0x34 with param[0x1E] still set.
        var a1 = new WwisePluginAllocator();
        var p1 = WwiseLimiterParams.Create(a1)!;
        p1.SetParamsBlockOrDefaults(Robot, 22);
        var l1 = WwiseLimiterPlugin.Create(a1)!;
        a1.FailAt = a1.Sizes.Count + 1;
        Assert.Equal(0x34, l1.Init(a1, null, p1, new WwiseEffectFormat(48000, 0x4101)));
        Assert.Null(l1.RingSnapshot);
        Assert.Null(l1.WriteIndexIfSet);
        Assert.Equal(WwiseLimiterPlugin.ProcessKind.Unset, l1.Process);
        Assert.Equal((byte)1, p1.DirtySetup);

        var a2 = new WwisePluginAllocator();
        var p2 = WwiseLimiterParams.Create(a2)!;
        p2.SetParamsBlockOrDefaults(Robot, 22);
        var l2 = WwiseLimiterPlugin.Create(a2)!;
        a2.FailAt = a2.Sizes.Count + 2;
        Assert.Equal(0x34, l2.Init(a2, null, p2, new WwiseEffectFormat(48000, 0x4101)));
        Assert.NotNull(l2.RingSnapshot);
        Assert.Null(l2.DetectorSnapshot);
        Assert.Equal(WwiseLimiterPlugin.ProcessKind.P2, l2.Process);
        Assert.Equal((byte)1, p2.DirtySetup);
    }

    [Fact]
    public void M6_013_C45_4_Term_frees_the_ring_the_detectors_and_the_object_and_returns_1()
    {
        var (lim, _, alloc) = Make(Robot);
        int n0 = alloc.Freed.Count;
        Assert.Equal(1, lim.Term(alloc));
        Assert.Equal(3, alloc.Freed.Count - n0);
        Assert.Same(lim, alloc.Freed[^1]);
        Assert.Throws<WwiseMissingBehaviourException>(() => lim.Execute(Mono(new float[4], 4)));
    }

    [Fact]
    public void M6_013_C45_4_The_log10_approximation_and_the_fast_power_use_the_engines_constants()
    {
        // research 4.7a: log10approx(x) = ((e - 127.0f) * 0.6931472f + (z + z) * (1.0f + z*z*0.33333334f)) * 0.4342945f with z = (m - 1) / (m + 1); x = 1.0 -> exactly 0; x = 0 gives e = 0, m = 1.0: finite -127 * ln2 * log10(e) = -38.2308 (not -inf), clamped to 0 by the target.
        // The fast power at e = 0 is the polynomial at m = 1.0: 0.6530434489 + 0.0208057724 + 0.3251897693 = 0.99903899; the cutoff -37.0f applies only below it (e = -37.0 still takes the polynomial path, about 1e-37).
        Assert.Equal(0f, WwiseLimiterPlugin.Log10Approx(1f));
        Assert.InRange(WwiseLimiterPlugin.Log10Approx(0f), -38.2309f, -38.2307f);
        Assert.InRange(WwiseLimiterPlugin.Log10Approx(2f), 0.30102f, 0.30104f);
        Assert.True(float.IsFinite(WwiseLimiterPlugin.Log10Approx(0f)));
        Assert.InRange(WwiseLimiterPlugin.FastPow(0f), 0.99903f, 0.99904f);
        Assert.InRange(WwiseLimiterPlugin.FastPow(-37f), 0f, 1e-3f);
    }
}
