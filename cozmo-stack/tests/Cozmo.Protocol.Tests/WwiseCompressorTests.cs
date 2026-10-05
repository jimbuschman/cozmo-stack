using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The Compressor plug-in (<c>0x006C0003</c>, vptr <c>0x103DF28</c>; M6-013 / M6-022, C40.6, research live-bodies-10 T-F1, T-F4..T-F7) against the engine's own code under Unicorn (<c>re-analysis/tools/emu/emu_comp.py</c> -> <see cref="WwiseCompressorOracle"/>): the creators <c>0xAA0538</c> and
/// <c>0xAA0808</c>, the parameter object's block parse <c>0xAA07A0</c> / <c>0xAA0734</c> and SetParam <c>0xAA0660</c>, Init <c>0xA9FB28</c>, Reset <c>0xA9FA68</c> and the Execute wrapper <c>0xA9FC70</c> with the per-channel worker <c>0xAA0298</c>. The expected values are the engine's, never this
/// implementation's. A NaN is compared as NaN (its payload is not modelled); the phone's libm is the one host seam (<see cref="WwiseHostMath.Expf"/>, <see cref="WwiseHostMath.Powf"/>) and the proof tables show it equals the correctly rounded float32 for every argument the oracle uses.
/// </summary>
public class WwiseCompressorTests
{
    private static readonly Regex Word = new(@"\b[0-9A-F]{8}\b", RegexOptions.Compiled);

    private static bool IsNaN(uint v) => (v & 0x7F800000) == 0x7F800000 && (v & 0x7FFFFF) != 0;

    /// <summary>Every eight-digit uppercase hex word that is a NaN becomes <c>7FC00000</c> (the emulator side does the same).</summary>
    internal static string CanonWords(string s) => Word.Replace(s, m => IsNaN(Convert.ToUInt32(m.Value, 16)) ? "7FC00000" : m.Value);

    private static readonly uint[] Specials = { 0x7FC00000, 0xFFC12345, 0x7F800000, 0xFF800000, 0x00000000, 0x80000000, 0x00000001, 0x80400000, 0x7F7FFFFF, 0x00800000 };

    /// <summary>The generator of <c>gen_input</c> in emu_comp.py: kinds 0 audio, 1 loud, 2 quiet (x2^-66), 3 denormal patterns, 4 random patterns, 5 audio with specials, 6 zeros.</summary>
    internal static uint[] Input(int kind, ulong seed, int count)
    {
        var xs = new Xs(seed);
        var o = new uint[count];
        for (int i = 0; i < count; i++)
        {
            switch (kind)
            {
                case 0: o[i] = BitConverter.SingleToUInt32Bits(xs.F32()); break;
                case 1: o[i] = BitConverter.SingleToUInt32Bits(xs.F32() * 8f); break;
                case 2: o[i] = BitConverter.SingleToUInt32Bits(xs.F32() * BitConverter.Int32BitsToSingle(0x1E800000)); break;   // 2^-66
                case 3: o[i] = xs.U32() & 0x807FFFFF; break;
                case 4: o[i] = xs.U32(); break;
                case 5:
                {
                    uint u = xs.U32();
                    uint x = BitConverter.SingleToUInt32Bits(xs.F32());
                    o[i] = u % 13 == 0 ? Specials[(u >> 8) % (uint)Specials.Length] : x;
                    break;
                }
                default: o[i] = 0; break;
            }
        }
        return o;
    }

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

    private static string X(float f) => BitConverter.SingleToUInt32Bits(f).ToString("X");

    private static string Snap(WwiseCompressorParams p) =>
        $"{BitConverter.SingleToUInt32Bits(p.Threshold):X8},{BitConverter.SingleToUInt32Bits(p.Ratio):X8},{BitConverter.SingleToUInt32Bits(p.Attack):X8},{BitConverter.SingleToUInt32Bits(p.Release):X8}," +
        $"{p.MakeupBits:X8},{p.Byte18:X2}{p.Byte19:X2}";

    private static string InitString(WwiseCompressor c, int rc) =>
        $"{rc} {(c.Worker == WwiseCompressor.WorkerKind.Linked ? "A9FEEC" : "AA0298")} 0 {(rc == 1 ? X(c.PreviousMakeup) : "0")} {c.Channels:X} {c.Rate:X} {c.StateCount:X} " +
        $"{(rc == 1 ? X(c.PowerCoef) : "0")} {X(c.AttackSetting)} {X(c.AttackCoef)} {X(c.ReleaseSetting)} {X(c.ReleaseCoef)} {c.LfeFlag:X} {(c.StateSnapshot is null ? 0 : 1)}";

    private static string Execute(WwiseCompressor c, uint cfg, int ch, string op)
    {
        var f = op.Split('.');
        int frames = int.Parse(f[1]), stride = int.Parse(f[2]), kind = int.Parse(f[3]);
        ulong seed = ulong.Parse(f[4]);
        int planes = Math.Max(Math.Max(ch, (int)(cfg & 0xFF)), 1);
        var words = Input(kind, seed, planes * stride);
        var data = new float[words.Length];
        for (int i = 0; i < data.Length; i++) data[i] = BitConverter.UInt32BitsToSingle(words[i]);
        c.Execute(new WwiseDecodeState { Data = data, ChannelConfig = cfg, MaxFrames = (ushort)stride, ValidFrames = (ushort)frames });
        var st = c.StateSnapshot!;
        string pairs = string.Join(",", st.Select(v => BitConverter.SingleToUInt32Bits(v).ToString("X8")));
        return $"E:{Hash(data)}:{BitConverter.SingleToUInt32Bits(c.PreviousMakeup):X8}:{BitConverter.SingleToUInt32Bits(c.AttackSetting):X8}:{BitConverter.SingleToUInt32Bits(c.AttackCoef):X8}:" +
               $"{BitConverter.SingleToUInt32Bits(c.ReleaseSetting):X8}:{BitConverter.SingleToUInt32Bits(c.ReleaseCoef):X8}:{pairs}";
    }

    private static (string Init, string Results) RunCase(string row)
    {
        var part = row.Split(" | ");
        var head = part[0].Split(' ');
        uint rate = uint.Parse(head[0]);
        int ch = int.Parse(head[1]);
        uint cfg = Convert.ToUInt32(head[2], 16);
        int fail = int.Parse(head[3]);
        byte[]? block = part[1] == "-" ? null : Convert.FromHexString(part[1]);
        string[] ops = part[2] == "-" ? Array.Empty<string>() : part[2].Split(' ');

        var alloc = new WwisePluginAllocator { FailAt = fail == 0 ? null : fail };
        var p = WwiseCompressorParams.Create(alloc)!;                              // allocation 1
        if (block is null) p.SetParamsBlockOrDefaults(null, 0); else p.SetParamsBlockOrDefaults(block, 22);
        var c = WwiseCompressor.Create(alloc)!;                                    // allocation 2
        int rc = c.Init(alloc, null, p, new WwiseEffectFormat(rate, (uint)ch));    // allocation 3 (the state)
        string init = InitString(c, rc);
        if (rc != 1) return (init, "");

        var res = new List<string>();
        foreach (var op in ops)
        {
            switch (op[0])
            {
                case 'R': res.Add($"R:{c.Reset()}"); break;
                case 'E': res.Add(Execute(c, cfg, ch, op)); break;
                case 'P':
                {
                    var f = op.Split('.');
                    int r = p.SetParam(uint.Parse(f[1]), BitConverter.GetBytes(Convert.ToUInt32(f[2], 16)));
                    res.Add($"P:{r}:{Snap(p)}");
                    break;
                }
                case 'B':
                    res.Add($"B:{p.SetParamsBlockOrDefaults(Convert.FromHexString(op[2..]), 22)}:{Snap(p)}");
                    break;
                case 'D':
                    res.Add($"D:{p.SetParamsBlockOrDefaults(null, 0)}:{Snap(p)}");
                    break;
                default: throw new InvalidOperationException(op);
            }
        }
        return (init, string.Join(" ; ", res));
    }

    [Fact]
    public void M6_013_T_F5_T_F6_T_F7_Every_oracle_case_matches_the_engine_Init_0xA9FB28_Reset_0xA9FA68_and_Execute_0xA9FC70_with_worker_0xAA0298()
    {
        // 2796 lives: the shipped blocks at every rate, grid blocks, make-up changes (the NEON ramp and the equal multiply), denormal-scale and denormal inputs (NEON flush to zero), NaN / inf / random patterns, stereo and 3 channels with the LFE
        // flag, coefficient extremes, every Init worker choice and the failing state allocation, and the parameter-object ops. Each row is the engine's output.
        Assert.True(WwiseCompressorOracle.Cases.Length >= 2400);
        int bad = 0;
        string? first = null;
        foreach (var row in WwiseCompressorOracle.Cases)
        {
            var part = row.Split(" | ");
            var (init, res) = RunCase(row);
            if (CanonWords(init) != CanonWords(part[3]) || CanonWords(res) != CanonWords(part[4]))
            {
                bad++;
                first ??= $"{part[0]} | {part[2]}\n engine {part[3]} | {part[4]}\n   here {init} | {res}";
            }
        }
        Assert.True(bad == 0, $"{bad} of {WwiseCompressorOracle.Cases.Length} cases differ; first:\n{first}");
    }

    [Fact]
    public void M6_013_T_F5_The_oracle_covers_every_input_kind_both_gain_paths_and_every_worker_choice()
    {
        // Guards the oracle itself: a regenerated file that lost a group would still pass the equality test above.
        var kinds = new HashSet<int>();
        int ramp = 0, equalMultiply = 0, linked = 0, perChannel = 0, failed = 0, stereo = 0;
        foreach (var row in WwiseCompressorOracle.Cases)
        {
            var part = row.Split(" | ");
            foreach (var op in part[2].Split(' ')) if (op.StartsWith("E.")) kinds.Add(int.Parse(op.Split('.')[3]));
            var init = part[3].Split(' ');
            if (init[0] == "52") failed++;
            else if (init[1] == "A9FEEC") linked++;
            else perChannel++;
            if (part[0].Split(' ')[1] is "2" or "3") stereo++;
            if (part[2].Contains("P.4.")) ramp++;
            if (part[2].Contains("E.") && !part[2].Contains("P.4.") && !part[2].Contains("B.") && !part[2].Contains("D")) equalMultiply++;
        }
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, kinds.OrderBy(k => k).ToArray());
        Assert.True(ramp > 400 && equalMultiply > 1000 && linked >= 12 && perChannel > 2500 && failed >= 24 && stereo > 300, $"{ramp} {equalMultiply} {linked} {perChannel} {failed} {stereo}");
    }

    [Fact]
    public void M6_013_T_F1_The_plug_in_slots_return_what_the_binary_returns()
    {
        // 0xA9FAEC..0xA9FB10: [info+0] = 3, [info+4] = 0x7E002, byte [info+8] = 1, byte [info+0xA] = 0; vt+0x14 / vt+0x18 are 0x8DBF38 / 0x8DBF3C (movs r0,#0); vt+0x24 is 0xAA05B0 (movs r0,#0x2d).
        var c = WwiseCompressor.Create(new WwisePluginAllocator())!;
        IWwiseEffectPlugin plug = c;
        Assert.Equal(1, plug.GetPluginInfo(out var info));
        Assert.Equal(new WwisePluginInfo(3, 0x7E002, 1, 0), info);
        Assert.Equal(0, plug.Slot14());
        Assert.Equal(0, plug.Slot18());
        Assert.Equal(0x2D, plug.Slot24());
        Assert.Equal(0x006C0003u, WwiseCompressor.PluginId);
        Assert.Equal(0x103DF28u, WwiseCompressor.Vptr);
    }

    [Fact]
    public void M6_013_T_F1_The_creators_fail_with_null_when_the_pool_allocation_fails()
    {
        // 0xAA053C..0xAA0550 (alloc->vt+8(alloc, 0x3C), null returns null) and 0xAA080C..0xAA0820 (0x1C bytes).
        var a = new WwisePluginAllocator { FailAt = 1 };
        Assert.Null(WwiseCompressor.Create(a));
        Assert.Equal(new[] { 0x3C }, a.Sizes);
        var b = new WwisePluginAllocator { FailAt = 1 };
        Assert.Null(WwiseCompressorParams.Create(b));
        Assert.Equal(new[] { 0x1C }, b.Sizes);
        var okAlloc = new WwisePluginAllocator();
        Assert.NotNull(WwiseCompressor.Create(okAlloc));
        Assert.NotNull(WwiseCompressorParams.Create(okAlloc));
        Assert.Equal(new[] { 0x3C, 0x1C }, okAlloc.Sizes);
    }

    private static readonly byte[] ShippedBlock = Convert.FromHexString("6666bac1000020406f12833a3d0a573e0000e0400101");

    [Fact]
    public void M6_013_T_F4_The_shipped_block_0x89DDC03B_parses_to_the_engines_parameter_words()
    {
        // Init.bnk / Cozmo.bnk FxShareSet 0x89DDC03B (HIRC type 18, plug-in 0x6C0003, 22 bytes): -23.3, 2.5, 0.001, 0.21, 7.0, 1, 1. 0xAA0734 stores words 0..3 raw, +0x14 = powf(10, 7.0f * 0.05f) (the engine run: 0x400F4735, oracle row 0 init field +0x10)
        // and the bytes at block 0x14 / 0x15 at +0x18 / +0x19.
        var p = WwiseCompressorParams.Create(new WwisePluginAllocator())!;
        Assert.Equal(1, p.SetParamsBlock(ShippedBlock));
        Assert.Equal(0xC1BA6666u, BitConverter.SingleToUInt32Bits(p.Threshold));
        Assert.Equal(0x40200000u, BitConverter.SingleToUInt32Bits(p.Ratio));
        Assert.Equal(0x3A83126Fu, BitConverter.SingleToUInt32Bits(p.Attack));
        Assert.Equal(0x3E570A3Du, BitConverter.SingleToUInt32Bits(p.Release));
        Assert.Equal(0x400F4735u, p.MakeupBits);
        Assert.Equal((byte)1, p.Byte18);
        Assert.Equal((byte)1, p.Byte19);
        var row0 = WwiseCompressorOracle.Cases.First(r => r.StartsWith("48000 1 1 0 |") && r.Split(" | ")[1] == "6666bac1000020406f12833a3d0a573e0000e0400101");
        Assert.Equal("400F4735", row0.Split(" | ")[3].Split(' ')[3]);                                                       // the engine's [this+0x10] after Init: the make-up word
    }

    [Fact]
    public void M6_013_T_F4_A_zero_size_installs_the_defaults_0xAA07AC_and_a_size_forwards_to_0xAA0734()
    {
        // 0xAA07A0 cmp r3,#0: movw/movt constants 0xC1400000 (-12.0), 0x40800000 (4.0), 0x3C23D70A (0.01), 0x3DCCCCCD (0.1), 0x3F800000 (1.0) at +4, +8, +0xC, +0x10, +0x14 and bytes 1, 1 at +0x18, +0x19 (0xAA07AC..0xAA07EC).
        var p = WwiseCompressorParams.Create(new WwisePluginAllocator())!;
        Assert.Equal(1, p.SetParamsBlockOrDefaults(null, 0));
        Assert.Equal("C1400000,40800000,3C23D70A,3DCCCCCD,3F800000,0101", Snap(p));
        Assert.Equal(1, p.SetParamsBlockOrDefaults(ShippedBlock, 22));
        Assert.Equal("C1BA6666,40200000,3A83126F,3E570A3D,400F4735,0101", Snap(p));
    }

    [Fact]
    public void M6_013_T_F4_SetParam_0xAA0660_refuses_a_null_value_and_ids_above_6_with_0x1F()
    {
        // 0xAA0664 cmp r2,#0; beq 0xAA0690 (mov r3,#0x1f); 0xAA0668 cmp r1,#6; the jump table for ids 0..6 (0xAA0674..0xAA068C) and 0xAA0670 b 0xAA0690 for the rest.
        var p = WwiseCompressorParams.Create(new WwisePluginAllocator())!;
        Assert.Equal(0x1F, p.SetParam(0, null));
        Assert.Equal(0x1F, p.SetParam(7, new byte[4]));
        Assert.Equal(0x1F, p.SetParam(0xFFFFFFFF, new byte[4]));
        Assert.Equal(1, p.SetParam(0, BitConverter.GetBytes(0xC1400000u)));
        Assert.Equal(1, p.SetParam(1, BitConverter.GetBytes(0x40800000u)));
        Assert.Equal(1, p.SetParam(2, BitConverter.GetBytes(0x3C23D70Au)));
        Assert.Equal(1, p.SetParam(3, BitConverter.GetBytes(0x3DCCCCCDu)));
        Assert.Equal(1, p.SetParam(5, new byte[] { 0x07 }));
        Assert.Equal(1, p.SetParam(6, new byte[] { 0x09 }));
        Assert.Equal(0xC1400000u, BitConverter.SingleToUInt32Bits(p.Threshold));
        Assert.Equal((byte)7, p.Byte18);                                   // id 5 -> byte +0x18 (0xAA0720..0xAA0728)
        Assert.Equal((byte)9, p.Byte19);                                   // id 6 -> byte +0x19 (0xAA069C..0xAA06A4)
        Assert.Throws<WwiseMissingBehaviourException>(() => p.Makeup);     // id 4 not stored yet: the field is uninitialised pool memory
    }

    [Fact]
    public void M6_013_T_F4_A_block_shorter_than_22_bytes_is_not_an_engine_input_and_a_fresh_object_has_no_values()
    {
        // 0xAA0734 reads words 0..4 and the bytes 0x14, 0x15 whatever the size argument; 0xAA0808 stores only the vptr.
        var p = WwiseCompressorParams.Create(new WwisePluginAllocator())!;
        Assert.Throws<WwiseMissingBehaviourException>(() => p.SetParamsBlock(new byte[21]));
        Assert.Throws<WwiseMissingBehaviourException>(() => p.SetParamsBlock(null));
        Assert.Throws<WwiseMissingBehaviourException>(() => p.Threshold);
        Assert.Throws<WwiseMissingBehaviourException>(() => p.Byte19);
    }

    [Fact]
    public void M6_013_T_F4_The_clone_0xAA05BC_copies_the_24_bytes_and_fails_with_null_when_the_allocation_fails()
    {
        var a = new WwisePluginAllocator();
        var p = WwiseCompressorParams.Create(a)!;
        p.SetParamsBlock(ShippedBlock);
        var c = p.Clone(a)!;
        Assert.Equal(Snap(p), Snap(c));
        Assert.Equal(new[] { 0x1C, 0x1C }, a.Sizes);
        a.FailAt = 3;
        Assert.Null(p.Clone(a));
        Assert.Equal(1, WwiseCompressorParams.Destroy(c, a));
        Assert.Equal(1, WwiseCompressorParams.Destroy(null, a));
        Assert.Single(a.Freed);
    }

    [Fact]
    public void M6_013_T_F1_Reset_0xA9FA68_zeroes_every_state_pair_and_Term_0xA9FA14_frees_the_state_then_the_object()
    {
        var a = new WwisePluginAllocator();
        var p = WwiseCompressorParams.Create(a)!;
        p.SetParamsBlockOrDefaults(new byte[] { 0x33, 0x33, 0xF3, 0xC1, 0x33, 0x33, 0x13, 0x40, 0xCD, 0xCC, 0xCC, 0x3E, 0x5C, 0x8F, 0x42, 0x3E, 0x9A, 0x99, 0x59, 0x40, 0x00, 0x00 }, 22);   // link byte 0: three channels get three pairs
        var c = WwiseCompressor.Create(a)!;
        IWwiseEffectPlugin plug = c;
        Assert.Equal(1, plug.Init(a, null, p, new WwiseEffectFormat(48000, 3)));
        Assert.Equal(3u, c.StateCount);
        Assert.Equal(new[] { 0x1C, 0x3C, 0x18 }, a.Sizes);                // the state is channels * 8 bytes (0xA9FC50 lsleq r1,r3,#3)
        Assert.Equal(1, plug.Reset());
        Assert.All(c.StateSnapshot!, v => Assert.Equal(0u, BitConverter.SingleToUInt32Bits(v)));
        var data = new float[3 * 64];
        for (int i = 0; i < data.Length; i++) data[i] = (i % 7 - 3) * 0.1f;
        plug.Execute(new WwiseDecodeState { Data = data, ChannelConfig = 3, MaxFrames = 64, ValidFrames = 64 });
        Assert.Contains(c.StateSnapshot!, v => BitConverter.SingleToUInt32Bits(v) != 0);
        Assert.Equal(1, plug.Reset());
        Assert.All(c.StateSnapshot!, v => Assert.Equal(0u, BitConverter.SingleToUInt32Bits(v)));
        Assert.Equal(1, plug.Term(a));
        Assert.Equal(2, a.Freed.Count);
        Assert.Same(c, a.Freed[1]);
    }

    [Fact]
    public void M6_013_T_F1_Reset_before_Init_and_Execute_before_Reset_are_visible_stops()
    {
        // The engine reads uninitialised pool memory in both ([this+0x1C], the state pairs): the allocation of 0xA9FBEC is not zeroed.
        var a = new WwisePluginAllocator();
        var p = WwiseCompressorParams.Create(a)!;
        p.SetParamsBlock(ShippedBlock);
        var c = WwiseCompressor.Create(a)!;
        Assert.Throws<WwiseMissingBehaviourException>(() => c.Reset());
        Assert.Equal(1, c.Init(a, null, p, new WwiseEffectFormat(48000, 1)));
        var s = new WwiseDecodeState { Data = new float[16], ChannelConfig = 1, MaxFrames = 16, ValidFrames = 16 };
        Assert.Throws<WwiseMissingBehaviourException>(() => c.Execute(s));
        c.Reset();
        c.Execute(s);
    }

    [Fact]
    public void M6_013_T_F6_Execute_0xA9FC70_with_no_valid_frames_returns_before_anything_changes()
    {
        // 0xA9FC8C cmp r3,#0; beq 0xA9FDA0: neither the worker nor the make-up state runs, even when the make-up changed.
        var a = new WwisePluginAllocator();
        var p = WwiseCompressorParams.Create(a)!;
        p.SetParamsBlock(ShippedBlock);
        var c = WwiseCompressor.Create(a)!;
        c.Init(a, null, p, new WwiseEffectFormat(48000, 1));
        c.Reset();
        float before = c.PreviousMakeup;
        p.SetParam(4, BitConverter.GetBytes(BitConverter.SingleToUInt32Bits(12f)));
        var data = new float[8];
        c.Execute(new WwiseDecodeState { Data = data, ChannelConfig = 1, MaxFrames = 8, ValidFrames = 0 });
        Assert.Equal(BitConverter.SingleToUInt32Bits(before), BitConverter.SingleToUInt32Bits(c.PreviousMakeup));
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(1, 2)]
    public void M6_013_T_F8_The_linked_worker_0xA9FEEC_is_selected_by_Init_and_stops_Execute_visibly(int channels, int link)
    {
        // 0xA9FBB8 cmp r2,r6; bhs: m = (channels == 1), m < byte[params+0x19] picks 0xA9FEEC with an 8-byte state and +0x1C = 1. Its per-sample arithmetic is RECOVERABLE_GAP (not read).
        var a = new WwisePluginAllocator();
        var p = WwiseCompressorParams.Create(a)!;
        var blk = (byte[])ShippedBlock.Clone();
        blk[0x15] = (byte)link;
        p.SetParamsBlock(blk);
        var c = WwiseCompressor.Create(a)!;
        Assert.Equal(1, c.Init(a, null, p, new WwiseEffectFormat(48000, (uint)channels)));
        Assert.Equal(WwiseCompressor.WorkerKind.Linked, c.Worker);
        Assert.Equal(1u, c.StateCount);
        Assert.Equal(8, a.Sizes[^1]);
        c.Reset();
        var s = new WwiseDecodeState { Data = new float[2 * 16], ChannelConfig = (uint)channels, MaxFrames = 16, ValidFrames = 16 };
        Assert.Throws<WwiseMissingBehaviourException>(() => c.Execute(s));
        s.ValidFrames = 0;
        c.Execute(s);                                                      // 0xA9FC8C returns before the worker is reached
    }

    [Fact]
    public void M6_013_T_F5_Init_with_a_failing_state_allocation_returns_0x34_and_leaves_the_state_null()
    {
        // 0xA9FBF4..0xA9FC00: cmp r0,#0; str r0,[r4,#0x24]; moveq r3,#0x34. The coefficients stored before the allocation stay; +0x20 and +0x10 are not written.
        var a = new WwisePluginAllocator { FailAt = 1 };
        var p = WwiseCompressorParams.Create(new WwisePluginAllocator())!;
        p.SetParamsBlock(ShippedBlock);
        var obj = WwiseCompressor.Create(new WwisePluginAllocator())!;
        Assert.Equal(WwiseCompressor.AllocationFailed, obj.Init(a, null, p, new WwiseEffectFormat(48000, 1)));
        Assert.Equal(0x34, WwiseCompressor.AllocationFailed);
        Assert.Null(obj.StateSnapshot);
        Assert.NotEqual(0f, obj.AttackCoef);
        Assert.Throws<InvalidOperationException>(() => obj.Reset());
    }

    [Fact]
    public void M6_013_T_F7_The_worker_refreshes_a_changed_attack_and_release_coefficient_with_expf_of_minus_2_2_over_rate_times_time()
    {
        // 0xAA02C4..0xAA02E8 (the comparison of P[2], P[3] with [+0x28], [+0x30]) and 0xAA04A8..0xAA04D4. The refreshed words are the engine's: 8000 Hz, attack 0.001 -> 0.002 and release 0.21 -> 0.3 (emu_comp.py case rows with P ops).
        var a = new WwisePluginAllocator();
        var p = WwiseCompressorParams.Create(a)!;
        p.SetParamsBlock(ShippedBlock);
        var c = WwiseCompressor.Create(a)!;
        c.Init(a, null, p, new WwiseEffectFormat(8000, 1));
        c.Reset();
        float oldAttack = c.AttackCoef, oldRelease = c.ReleaseCoef;
        var s = new WwiseDecodeState { Data = new float[32], ChannelConfig = 1, MaxFrames = 32, ValidFrames = 32 };
        c.Execute(s);
        Assert.Equal(BitConverter.SingleToUInt32Bits(oldAttack), BitConverter.SingleToUInt32Bits(c.AttackCoef));        // unchanged times: no refresh
        p.SetParam(2, BitConverter.GetBytes(BitConverter.SingleToUInt32Bits(0.002f)));
        p.SetParam(3, BitConverter.GetBytes(BitConverter.SingleToUInt32Bits(0.3f)));
        c.Execute(s);
        Assert.Equal(BitConverter.SingleToUInt32Bits(0.002f), BitConverter.SingleToUInt32Bits(c.AttackSetting));
        Assert.Equal(BitConverter.SingleToUInt32Bits(0.3f), BitConverter.SingleToUInt32Bits(c.ReleaseSetting));
        Assert.NotEqual(BitConverter.SingleToUInt32Bits(oldAttack), BitConverter.SingleToUInt32Bits(c.AttackCoef));
        Assert.NotEqual(BitConverter.SingleToUInt32Bits(oldRelease), BitConverter.SingleToUInt32Bits(c.ReleaseCoef));
    }

    [Fact]
    public void M6_013_T_F6_The_byte_count_zero_with_the_LFE_flag_wraps_in_the_engine_and_is_a_visible_stop_here()
    {
        // 0xA9FD04..0xA9FD0C: byte [S+4] = 0 with u32 [S+4] & 0x8000 and the LFE byte 0 makes r0 = 0xFFFFFFFF: the loops run over unmapped memory.
        var a = new WwisePluginAllocator();
        var p = WwiseCompressorParams.Create(a)!;
        var blk = (byte[])ShippedBlock.Clone();
        blk[0x14] = 0;
        blk[0x15] = 0;
        p.SetParamsBlock(blk);
        var c = WwiseCompressor.Create(a)!;
        c.Init(a, null, p, new WwiseEffectFormat(48000, 1));
        c.Reset();
        var s = new WwiseDecodeState { Data = new float[16], ChannelConfig = 0x8000, MaxFrames = 16, ValidFrames = 16 };
        Assert.Throws<InvalidOperationException>(() => c.Execute(s));
    }

    [Fact]
    public void M6_013_The_host_expf_is_the_correctly_rounded_float32_for_every_argument_the_compressor_produces()
    {
        // EQUIVALENT_IMPLEMENTATION of the phone's expf (PLT 0x4D0058): double Math.Exp rounded once equals the 200-bit correctly rounded binary32 for every argument -2.2f / (rate * t) of the shipped blocks, the defaults and a grid at 12 rates,
        // -1.0f / (rate * 0.02322f), and every argument the oracle run passed. The smallest distance of an exact result to a binary32 midpoint is in the oracle's header (units of 2^-53 * result).
        Assert.True(WwiseCompressorOracle.ExpfProof.Length >= 300);
        foreach (var row in WwiseCompressorOracle.ExpfProof)
        {
            var f = row.Split(' ');
            uint arg = Convert.ToUInt32(f[0], 16), expected = Convert.ToUInt32(f[1], 16);
            uint got = BitConverter.SingleToUInt32Bits(WwiseHostMath.Expf(BitConverter.UInt32BitsToSingle(arg)));
            if (IsNaN(expected)) Assert.True(IsNaN(got), row);
            else Assert.True(expected == got, $"{row}: got {got:X8}");
        }
    }

    [Fact]
    public void M6_013_The_host_powf_is_the_correctly_rounded_float32_for_the_make_up_gains_of_the_shipped_blocks_and_a_grid()
    {
        // powf(10.0f, dB * 0.05f) at 0xAA070C / 0xAA077C (PLT 0x4D6778): WwiseHostMath.Powf (double Math.Pow rounded once) against the correctly rounded binary32.
        Assert.True(WwiseCompressorOracle.PowfProof.Length >= 20);
        foreach (var row in WwiseCompressorOracle.PowfProof)
        {
            var f = row.Split(' ');
            uint y = Convert.ToUInt32(f[0], 16), expected = Convert.ToUInt32(f[1], 16);
            uint got = BitConverter.SingleToUInt32Bits(WwiseHostMath.Powf(10f, BitConverter.UInt32BitsToSingle(y)));
            Assert.True(expected == got, $"{row}: got {got:X8}");
        }
    }

    [Fact]
    public void M6_013_T_F4_The_Compressor_blocks_in_the_shipped_banks_are_the_blocks_the_oracle_ran_when_the_unpacked_OBB_is_present()
    {
        // The seven distinct 22-byte blocks of the twelve plug-in 0x6C0003 ShareSets in Init.bnk, SFX.bnk and Cozmo.bnk (the oracle's SHIPPED_BLOCKS). Like the repo's other shipped-bank sweeps this returns when
        // re-analysis/obb/sound_meta is not on the machine (it is not tracked in git); the same blocks are in every oracle row group A.
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        string? meta = null;
        while (d is not null && meta is null)
        {
            var candidate = Path.Combine(d.FullName, "re-analysis", "obb", "sound_meta");
            if (Directory.Exists(candidate)) meta = candidate;
            d = d.Parent;
        }
        if (meta is null) return;
        var oracleBlocks = new HashSet<string>();
        foreach (var row in WwiseCompressorOracle.Cases.Take(168)) oracleBlocks.Add(row.Split(" | ")[1]);
        var found = new HashSet<string>();
        int count = 0;
        foreach (var f in Directory.EnumerateFiles(meta, "*.bnk", SearchOption.AllDirectories))
        {
            var bank = WwiseBank.Parse(File.ReadAllBytes(f), Path.GetFileName(f));
            foreach (var o in bank.Objects.Values)
            {
                var node = WwiseHierarchy.TryRead(o, out _);
                if (node is WwiseEffectNode e && e.PluginId == WwiseEffectNode.CompressorPlugin)
                {
                    count++;
                    Assert.Equal(22, e.Parameters.Length);
                    found.Add(Convert.ToHexString(e.Parameters.Span).ToLowerInvariant());
                }
            }
        }
        Assert.Equal(12, count);
        Assert.Equal(7, found.Count);
        Assert.True(found.SetEquals(oracleBlocks), "the shipped Compressor blocks differ from the oracle's group A blocks");
    }
}
