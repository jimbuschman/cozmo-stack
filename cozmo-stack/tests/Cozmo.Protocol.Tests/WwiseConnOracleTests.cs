// fidelity: M6-012, M6-022, M6-010
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-012 / M6-022 (C44.3, pass 16 and the pass-13 truth table): the connection gain and matrix chain and the bus gain stage against the engine's own code under Unicorn
/// (<c>emu_conn.py</c> -> <see cref="WwiseConnOracle"/>): <c>0xA4BC58</c> with <c>0xA5975C</c>, <c>0xA25FF8</c> / <c>0xA1F79C</c> / <c>0xA209BC</c>, <c>0xA67B9C</c> / <c>0xA67C58</c>, the voice-to-bus mix <c>0xA4FBEC</c> (<c>0xA45E9C</c>, <c>0xA46668</c>),
/// <c>0xA4D994</c> and <c>0xA4F9E0</c>. The expected values are the engine's, never this implementation's; the only thing decided here is which config pairs the inventory covers (C44.3 F7..F10); every other pair must stop.
/// </summary>
public class WwiseConnOracleTests
{
    private static uint H(string s) => Convert.ToUInt32(s, 16);

    private static float F(string s) => BitConverter.UInt32BitsToSingle(H(s));

    private static string X(float f) => BitConverter.SingleToUInt32Bits(f).ToString("X");

    private static string X(uint v) => v.ToString("X");

    private static string Words(ReadOnlySpan<float> half)
    {
        var sb = new StringBuilder();
        foreach (var f in half) { if (sb.Length != 0) sb.Append(' '); sb.Append(X(f)); }
        return sb.ToString();
    }

    private static uint Type(uint cfg) => (cfg >> 8) & 0xF;

    // ------------------------------------------------------------------------------------------------ group A: 0xA25FF8

    /// <summary>The config pairs C44.3 F7..F10 cover (the inventory's claim, not this code's behaviour): anonymous (type 0) into standard (type 1); mono to mono for flag 0 or 1; mono to stereo for flag 0, and for flag 1 only with the pan tuple the
    /// emulation covered (0.5, 0.5, 0); stereo to mono for flag 0 or 1. Everything else is a required stop.</summary>
    private static bool Covered(uint inCfg, uint outCfg, int flag, float p1, float p2, float p3)
    {
        if (Type(inCfg) == 0 && Type(outCfg) == 1) return true;                                        // F7: the diagonal 1.0
        if (inCfg == 0x4101 && outCfg == 0x4101) return flag <= 1;                                     // F8: [1, 0, 0, 0]
        if (inCfg == 0x4101 && outCfg == 0x3102) return flag == 0 || (flag == 1 && p1 == 0.5f && p2 == 0.5f && p3 == 0f);   // F9
        if (inCfg == 0x3102 && outCfg == 0x4101) return flag <= 1;                                     // F10: 0xFFA970 FL, FR
        return false;
    }

    [Fact]
    public void M6_012_F7_F10_The_channel_matrix_0xA25FF8_matches_the_engine_for_the_adopted_arms_and_stops_for_the_rest()
    {
        Assert.True(WwiseConnOracle.MatrixRows.Length >= 100);
        int covered = 0, stops = 0;
        foreach (var row in WwiseConnOracle.MatrixRows)
        {
            var parts = row.Split(" | ");
            var h = parts[0].Split(' ');                                      // A in out flag p1 p2 p3
            uint inCfg = H(h[1]), outCfg = H(h[2]);
            int flag = int.Parse(h[3]);
            float p1 = F(h[4]), p2 = F(h[5]), p3 = F(h[6]);
            var expected = parts[1].Split(' ').Select(H).ToArray();
            var matrix = new float[64];
            Array.Fill(matrix, float.NaN);                                    // a cell the engine row does not give must not be read or written
            if (Covered(inCfg, outCfg, flag, p1, p2, p3))
            {
                covered++;
                WwiseChannelMatrix.A25FF8(p1, p2, p3, (byte)flag, inCfg, outCfg, matrix);
                for (int i = 0; i < expected.Length; i++)
                    Assert.True(expected[i] == BitConverter.SingleToUInt32Bits(matrix[i]), $"{parts[0]}: word {i}: engine {expected[i]:X} C# {BitConverter.SingleToUInt32Bits(matrix[i]):X}");
                for (int i = expected.Length; i < matrix.Length; i++) Assert.True(float.IsNaN(matrix[i]), $"{parts[0]}: C# wrote cell {i} past numIn * rows");
            }
            else
            {
                stops++;
                Assert.Throws<WwiseMissingBehaviourException>(() => WwiseChannelMatrix.A25FF8(p1, p2, p3, (byte)flag, inCfg, outCfg, new float[64]));
            }
        }
        Assert.True(covered >= 60 && stops >= 10, $"covered {covered}, stops {stops}");
    }

    [Fact]
    public void M6_012_F8_The_shipped_shapes_give_the_inventory_values()
    {
        // F8 (mono to mono [1, 0, 0, 0]), F9 (mono to stereo [0.70710677, 0.70710677, 0, 0] = 0x3F3504F3), F10 (stereo to mono 0.70710677 twice, table 0xFFA970), F7 (anonymous: the diagonal): the values of C44.3 itself.
        var m = new float[16];
        WwiseChannelMatrix.A25FF8(0.5f, 0.5f, 0f, 0, 0x4101, 0x4101, m);
        Assert.Equal(new uint[] { 0x3F800000, 0, 0, 0 }, m.Take(4).Select(BitConverter.SingleToUInt32Bits).ToArray());
        m = new float[16];
        WwiseChannelMatrix.A25FF8(0.5f, 0.5f, 0f, 0, 0x4101, 0x3102, m);
        Assert.Equal(new uint[] { 0x3F3504F3, 0x3F3504F3, 0, 0 }, m.Take(4).Select(BitConverter.SingleToUInt32Bits).ToArray());
        m = new float[16];
        WwiseChannelMatrix.A25FF8(0.5f, 0.5f, 0f, 0, 0x3102, 0x4101, m);
        Assert.Equal(new uint[] { 0x3F3504F3, 0, 0, 0, 0x3F3504F3, 0, 0, 0 }, m.Take(8).Select(BitConverter.SingleToUInt32Bits).ToArray());
        m = new float[16];
        WwiseChannelMatrix.A25FF8(0.5f, 0.5f, 0f, 0, 0x0002, 0x3102, m);
        Assert.Equal(new uint[] { 0x3F800000, 0, 0, 0, 0, 0x3F800000, 0, 0 }, m.Take(8).Select(BitConverter.SingleToUInt32Bits).ToArray());   // stride rows + 1 = 5
    }

    [Theory]
    [InlineData(0x3102u, 0x3102u, 0, "0xA1F810")]      // stereo to stereo: the pan arithmetic of 0xA1F79C
    [InlineData(0x4101u, 0x3102u, 2, "0xA1F810")]      // a flag above 1
    [InlineData(0x4101u, 0x4101u, 2, "0xA1F810")]
    [InlineData(0x0202u, 0x4101u, 0, "0xA234FC")]      // an ambisonic input (type 2) into a standard output
    [InlineData(0x0101u, 0x2002u, 0, "0xA26200")]      // a type pair outside the three arms
    [InlineData(0x4101u | 0x8000u, 0x4101u, 0, "0xA1F79C")]   // an LFE channel
    public void M6_012_F11_Every_pair_outside_the_adopted_arms_is_a_required_stop(uint inCfg, uint outCfg, int flag, string names)
    {
        var ex = Assert.Throws<WwiseMissingBehaviourException>(() => WwiseChannelMatrix.A25FF8(0.5f, 0.5f, 0f, (byte)flag, inCfg, outCfg, new float[64]));
        Assert.Contains(names, ex.Message);
    }

    [Fact]
    public void M6_012_F9_A_non_default_pan_with_flag_1_and_more_than_one_output_is_a_required_stop()
    {
        // The manager's rule for C44.3: only the tuple the emulation covered (0.5, 0.5, 0) is built; (0.2, 0.9, 1.0) etc. take the cosf / sinf pan arithmetic (0xA1F810..0xA1F99C).
        foreach (var (p1, p2, p3) in new[] { (0.2f, 0.9f, 1.0f), (0f, 1f, 0.5f), (0.5f, 0.5f, 0.2f), (0.51f, 0.5f, 0f) })
            Assert.Throws<WwiseMissingBehaviourException>(() => WwiseChannelMatrix.A25FF8(p1, p2, p3, 1, 0x4101, 0x3102, new float[16]));
        WwiseChannelMatrix.A25FF8(0.2f, 0.9f, 1.0f, 1, 0x4101, 0x4101, new float[16]);   // mono out: the pan is not used
        WwiseChannelMatrix.A25FF8(0.2f, 0.9f, 1.0f, 1, 0x3102, 0x4101, new float[16]);
    }

    // ------------------------------------------------------------------------------------------------ group V: 0xA4BC58

    private sealed class VoiceScenario
    {
        public WwiseLiveVoice Voice = null!;
        public WwisePlayingInstance Pbi = null!;
        public WwiseVoiceSendEntry Entry = null!;
        public List<WwiseVoiceConnection> Conns = new();
        public string[] Ga = Array.Empty<string>();
        public int FramesN;
        public bool Mix;
    }

    private static WwisePlayingInstance NewPbi()
        => new(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, false);

    private static VoiceScenario Build(string[] h, string connSpec)
    {
        // V<seed> nconn mix cd0 vgain send dc0 frames lpf b8 bc c0 c4 hpf
        var sc = new VoiceScenario { Mix = h[2] == "1", FramesN = (int)H(h[7]) };
        int nconn = int.Parse(h[1]);
        sc.Voice = new WwiseLiveVoice(1, 8) { FlagsCD = (byte)H(h[3]), OutputGain = F(h[4]) };
        sc.Entry = new WwiseVoiceSendEntry { SendGain = F(h[5]) };
        sc.Voice.SendTable = new WwiseVoiceSendTable { Capacity = 1 };
        sc.Voice.SendTable.Entries.Add(sc.Entry);
        sc.Pbi = NewPbi();
        sc.Pbi.Flags0E8 = (byte)(H(h[6]) | 0x10);
        sc.Pbi.Lpf48 = F(h[8]);
        sc.Pbi.FieldC4 = F(h[9]);
        sc.Pbi.FieldC8 = F(h[10]);
        sc.Pbi.FieldCC = F(h[11]);
        sc.Pbi.FieldD0 = (byte)H(h[12]);
        sc.Pbi.Hpf4C = F(h[13]);
        var ga = new List<string>();
        if (nconn != 0)
        {
            foreach (var c in connSpec.Split(';'))
            {
                var t = c.Split(',');                                          // arg5,f6c,c08,c0c,c10,c14,ga0,ga1
                var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), sc.FramesN);
                var conn = new WwiseVoiceConnection(bus, 1, 1) { Arg68 = uint.Parse(t[0]), Flags6C = (byte)H(t[1]), C08 = F(t[2]), C0C = F(t[3]), C10 = F(t[4]), C14 = F(t[5]) };
                sc.Voice.Connections.Add(conn);
                sc.Conns.Add(conn);
                ga.Add(t[6] + " " + t[7]);
            }
        }
        sc.Ga = ga.ToArray();
        return sc;
    }

    private static string ConnText(WwiseVoiceConnection c)
    {
        var d = c.Descriptor;
        string text = string.Join(' ', new[] { X(c.C08), X(c.C0C), X(c.C10), X(c.C14), X(c.C50), X(c.C54), X(c.C58), X(c.C5C), X((uint)c.C64), X((uint)c.Flags6C) });
        bool alloc = d.IsAllocated;
        text += $" {(alloc ? d.Size : 0)}/{(alloc ? d.PtrA : 0)}/{(alloc ? d.PtrB : 0)} n:{(alloc ? Words(d.NextMatrix) : "-")} p:{(alloc ? Words(d.PrevMatrix) : "-")}";
        return text;
    }

    private static string MixText(VoiceScenario sc, string[] fr, uint word, string[] frameTokens)
    {
        // The engine's mix after the frame for each connection with a descriptor whose (flags & 6) != 6: dry g = (1, 1), aux g = the scenario's pair; the data from Xs(seed + k).
        var parts = new List<string>();
        int valid = int.Parse(frameTokens[15]);
        uint seed = H(frameTokens[16]);
        for (int k = 0; k < sc.Conns.Count; k++)
        {
            var conn = sc.Conns[k];
            if (!conn.HasDry || (conn.Flags6C & 6) == 6 || conn.C64 != (int)(word & 0xFF)) { parts.Add("-"); continue; }   // the oracle's filter: a descriptor sized for another input count
            var ga = sc.Ga[k].Split(' ');
            float g0 = conn.Arg68 == 0 ? 1f : F(ga[0]), g1 = conn.Arg68 == 0 ? 1f : F(ga[1]);
            int sch = (int)(word & 0xFF), n = sc.FramesN;
            var d = new Xs(seed + (ulong)k);
            var flat = new float[n * sch];
            for (int i = 0; i < flat.Length; i++) flat[i] = d.F32();
            var ddata = new float[n];
            for (int i = 0; i < n; i++) ddata[i] = d.F32();
            var bus = conn.Bus;
            bus.ReleaseBuffer();                                              // frames 0
            bus.State = 4;
            Array.Copy(ddata, bus.Buffer, n);
            var S = new WwiseDecodeState { Data = flat, ChannelConfig = word, MaxFrames = (ushort)n, ValidFrames = (ushort)valid };
            conn.MixA4FBEC(S, g0, g1);
            parts.Add($"{(uint)bus.EState:X} {bus.State:X} {bus.Frames:X} {WwiseMixKernelTests.CanonHash(bus.Buffer)} {S.ValidFrames:X} {WwiseMixKernelTests.CanonHash(flat)}");
        }
        return string.Join(" ; ", parts);
    }

    [Fact]
    public void M6_022_B1_B15_0xA4BC58_matches_the_engine_prelude_per_connection_state_machine_and_mix_over_700_scenarios()
    {
        // 700 scenarios of 1..4 frames over 0..3 connections (the S2E/S2F outcomes, the minima starting at 100.0f, the re-init, the swap, the unpredicated C08 / C10, the bit-1, bit-2 and ramp / flat endings, 0xA5975C's decision tree,
        // 0xA25FF8's arms, the B14 tail), with the voice-to-bus mix after each frame for the mono-line scenarios; STOP rows: the 3D branch and a non-null param_12 are required stops the C# must hit in the same frame.
        Assert.True(WwiseConnOracle.VoiceRows.Length >= 600);
        int frames = 0, stops3d = 0, stopsP12 = 0, mixes = 0;
        foreach (var row in WwiseConnOracle.VoiceRows)
        {
            var halves = row.Split(" => ");
            var inParts = halves[0].Split(" | ");
            var h = inParts[0].Split(' ');
            var sc = Build(h, inParts[1]);
            var frameSpecs = inParts[2].Split(" ;; ");
            var outs = halves[1].Split(" ;; ");
            Assert.Equal(frameSpecs.Length >= outs.Length, true);
            for (int fi = 0; fi < outs.Length; fi++)
            {
                var t = frameSpecs[fi].Split(' ');
                // vt3c argA gain p12 word cd3 cd2 send dc4 a8 ac b0 b4 lpf hpf valid seed bits
                var voice = sc.Voice; var pbi = sc.Pbi;
                uint word = H(t[4]);
                voice.FlagsCD = (byte)((voice.FlagsCD & ~0x0C) | (int.Parse(t[5]) << 3) | (int.Parse(t[6]) << 2));
                voice.Word0xF0 = word;
                sc.Entry.SendGain = F(t[7]);
                pbi.Flags0E8 = (byte)((pbi.Flags0E8 & ~0x10) | (int.Parse(t[8]) << 4));
                pbi.PanB4 = F(t[9]); pbi.PanB8 = F(t[10]); pbi.PanBC = F(t[11]);
                pbi.PanC0 = (byte)int.Parse(t[12]);
                pbi.Lpf48 = F(t[13]); pbi.Hpf4C = F(t[14]);
                pbi.Flags1BE = (byte)((pbi.Flags1BE & ~0x14) | (int.Parse(t[0]) << 4));
                var bitsLcfg = t[17] == "-" ? Array.Empty<string>() : t[17].Split(',');
                for (int k = 0; k < sc.Conns.Count; k++)
                {
                    var bl = bitsLcfg[k].Split(':');
                    sc.Conns[k].Flags6C = (byte)((sc.Conns[k].Flags6C & ~2) | (int.Parse(bl[0]) << 1));
                    sc.Conns[k].Bus.Format64 = H(bl[1]);
                }
                WwiseGainArg12? arg12 = int.Parse(t[3]) != 0 ? new WwiseGainArg12(0x1234, 0x5678) : null;
                float gain = F(t[2]);
                byte argA = (byte)int.Parse(t[1]);
                frames++;
                if (outs[fi].StartsWith("STOP:"))
                {
                    var ex = Assert.Throws<WwiseMissingBehaviourException>(() => WwiseVoiceBusPass.UpdateConnectionGains(voice, pbi, gain, argA, arg12));
                    if (outs[fi] == "STOP:3D") { Assert.Contains("3D", ex.Message); stops3d++; }
                    else { Assert.Contains("param_12", ex.Message); stopsP12++; }
                    Assert.Equal(fi, outs.Length - 1);
                    break;
                }
                bool s2f = WwiseVoiceBusPass.UpdateConnectionGains(voice, pbi, gain, argA, arg12);
                string got = string.Join(' ', new[] { voice.Run2E ? "1" : "0", s2f ? "1" : "0", X(voice.OutputMin50[0]), X(voice.OutputMin50[1]), X(voice.OutputMin50[2]), X(voice.OutputMin50[3]),
                    voice.FlagsCD.ToString("X"), pbi.Flags0E8.ToString("X"), X(pbi.FieldC4), X(pbi.FieldC8), X(pbi.FieldCC), pbi.FieldD0.ToString("X") });
                got += " | " + string.Join(" ; ", sc.Conns.Select(ConnText));
                if (sc.Mix) { got += " | " + MixText(sc, t, word, t); mixes++; }
                Assert.True(outs[fi] == got, $"{h[0]} frame {fi}: python re-analysis/tools/emu/emu_conn.py --case {h[0]}" + Environment.NewLine + $"engine [{outs[fi]}]" + Environment.NewLine + $"C#     [{got}]");
            }
        }
        Assert.True(frames > 1200 && stops3d >= 5 && stopsP12 >= 10 && mixes > 200, $"frames {frames}, 3D stops {stops3d}, P12 stops {stopsP12}, mixes {mixes}");
    }

    // ------------------------------------------------------------------------------------------------ group B: 0xA4D994 and 0xA4F9E0

    [Fact]
    public void M6_022_G1_G6_0xA4D994_and_0xA4F9E0_match_the_engine_over_400_bus_scenarios()
    {
        // B<seed> local out hasMatrix c0 initMat db g80 g84 p94 p98 p9c pa0 oA4 oA8 oAC oB0 pcfg 0 frames | calls => outcomes. Per call: the gain pair (the fast pow dBToLin of [bus+0x90]: 0.99903899 at 0 dB), the previous matrix taking the old next one,
        // the recompute decision on the parameters, the first-call flatten (bit 3 of [bus+0xC0]), then the parent mix 0xA4F9E0 (the matrix path for [child+0xC0] & 6 or differing cfgs, the identity ramp otherwise).
        Assert.True(WwiseConnOracle.BusRows.Length >= 300);
        int calls = 0, mixes = 0, hashes = 0;
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState());
        foreach (var row in WwiseConnOracle.BusRows)
        {
            var halves = row.Split(" => ");
            var inParts = halves[0].Split(" | ");
            var h = inParts[0].Split(' ');
            var callSpecs = inParts[1].Split(" ;; ");
            var outs = halves[1].Split(" ;; ");
            uint local = H(h[1]), outCfg = H(h[2]);
            int n = int.Parse(h[19]);
            var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), n)
            {
                Format64 = local, Config44 = outCfg, FlagsC0 = (byte)H(h[4]), VolumeDb90 = F(h[6]), Gain80 = F(h[7]), Gain84 = F(h[8]),
                Param94 = F(h[9]), Param98 = F(h[10]), Param9C = F(h[11]), ParamA0 = (byte)H(h[12]),
                OldParamA4 = F(h[13]), OldParamA8 = F(h[14]), OldParamAC = F(h[15]), OldParamB0 = (byte)H(h[16]),
            };
            if (h[3] == "1")
            {
                Assert.Equal(1, bus.MatrixDescriptor34.Reserve((int)(local & 0xFF), (int)(outCfg & 0xFF)));
                if ((bus.FlagsC0 & 8) != 0)
                {
                    var d = new Xs(H(h[5]));
                    var fl = MemoryMarshal.Cast<byte, float>(bus.MatrixDescriptor34.Data!.AsSpan());
                    for (int i = 0; i < fl.Length; i++) fl[i] = d.F32();
                }
            }
            var parent = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), n) { Format64 = H(h[17]) };
            for (int ci = 0; ci < outs.Length; ci++)
            {
                var t = callSpecs[ci].Split(' ');                              // db params c0xor valid seed pc0
                if (t[1] != "-")
                {
                    var p = t[1].Split(',');
                    bus.Param94 = F(p[0]); bus.Param98 = F(p[1]); bus.Param9C = F(p[2]); bus.ParamA0 = (byte)H(p[3]);
                }
                bus.VolumeDb90 = F(t[0]);
                bus.FlagsC0 = (byte)((bus.FlagsC0 ^ int.Parse(t[2])) & ~4);
                bus.GainStageA4D994();
                calls++;
                bool alloc = bus.MatrixDescriptor34.IsAllocated;
                var dsc = bus.MatrixDescriptor34;
                string got = string.Join(' ', new[] { X(bus.Gain80), X(bus.Gain84), X(bus.Param94), X(bus.Param98), X(bus.Param9C), bus.ParamA0.ToString("X"),
                    X(bus.OldParamA4), X(bus.OldParamA8), X(bus.OldParamAC), bus.OldParamB0.ToString("X"), bus.FlagsC0.ToString("X") });
                got += $" {(alloc ? dsc.Size : 0)}/{(alloc ? dsc.PtrA : 0)}/{(alloc ? dsc.PtrB : 0)} n:{(alloc ? Words(dsc.NextMatrix) : "-")} p:{(alloc ? Words(dsc.PrevMatrix) : "-")}";
                if (local == 0x4101 && H(h[17]) == 0x4101)
                {
                    int valid = int.Parse(t[3]);
                    var d = new Xs(H(t[4]));
                    parent.ReleaseBuffer();                                       // frames 0, eState 0x11 (the engine row starts so: [parent+0x68] = 0x11, [parent+0x6E] = 0)
                    parent.State = 4;                                             // [parent+0x1BC] = 4
                    for (int i = 0; i < n; i++) bus.Buffer[i] = d.F32();          // the child's buffer S (sdata), then the parent's (pdata): the oracle's draw order
                    for (int i = 0; i < n; i++) parent.Buffer[i] = d.F32();
                    bus.Frames = valid;
                    byte save = bus.FlagsC0;
                    bus.FlagsC0 = (byte)((save & ~6) | int.Parse(t[5]));
                    pass.MixOutputBus(parent, bus.Buffer, bus);
                    bus.FlagsC0 = save;
                    got += $" | {(uint)parent.EState:X} {parent.State:X} {parent.Frames:X} {WwiseMixKernelTests.CanonHash(parent.Buffer)} {bus.Frames:X} {WwiseMixKernelTests.CanonHash(bus.Buffer)}";
                    mixes++;
                }
                Assert.True(outs[ci] == got, $"{h[0]} call {ci}: python re-analysis/tools/emu/emu_conn.py --case {h[0]}" + Environment.NewLine + $"engine [{outs[ci]}]" + Environment.NewLine + $"C#     [{got}]");
                hashes++;
            }
        }
        Assert.True(calls > 600 && mixes > 300, $"calls {calls}, mixes {mixes}, hashes {hashes}");
    }

    [Theory]
    [InlineData(0f, 0x3F7FC105u)]          // C44.3 G3 (emulated 0xA4D994): 0 dB -> 0.9990389943
    [InlineData(-6f, 0x3F002BCEu)]
    [InlineData(-80f, 0x38D2306Au)]
    [InlineData(-96f, 0x3784B48Eu)]
    [InlineData(-3f, 0x3F3574FEu)]
    [InlineData(6f, 0x3FFED5D6u)]
    public void M6_022_G3_The_bus_gain_is_the_fast_pow_dBToLin_of_the_volume(float db, uint bits)
    {
        Assert.Equal(bits, BitConverter.SingleToUInt32Bits(WwiseMixBus.DbToLinA4D994(db)));
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8) { VolumeDb90 = db };
        bus.GainStageA4D994();
        Assert.Equal(bits, BitConverter.SingleToUInt32Bits(bus.Gain84));
        Assert.Equal(bits, BitConverter.SingleToUInt32Bits(bus.Gain80));            // the first call is flat (G6)
        Assert.Equal(bits, BitConverter.SingleToUInt32Bits(bus.OutGain14));
    }

    [Fact]
    public void M6_010_G3_The_bus_dBToLin_matches_the_engine_over_10800_inputs_0xA4D994()
    {
        // The engine's own 0xA4D994 gain ([bus+0x84]) for a grid of dB values (-820 .. 280 step 0.125), the cut at -37 (y = dB * 0.05f), the special values (NaN, infinities, denormals) and 2000 random bit patterns. (M6-010's own WwiseGain.DbToLinear uses decimal literals
        // for the polynomial constants and differs from the engine by one ulp at some inputs, e.g. -739.375: 0x211F470 against the engine's 0x211F46F; the bus uses the engine's bit constants.)
        Assert.True(WwiseConnOracle.GainRows.Length >= 10000);
        foreach (var row in WwiseConnOracle.GainRows)
        {
            var parts = row.Split(" | ");
            uint db = H(parts[0].Split(' ')[1]);
            uint expected = H(parts[1]);
            uint got = BitConverter.SingleToUInt32Bits(WwiseMixBus.DbToLinA4D994(BitConverter.UInt32BitsToSingle(db)));
            bool bothNan = ((expected >> 23) & 0xFF) == 0xFF && (expected & 0x7FFFFF) != 0 && ((got >> 23) & 0xFF) == 0xFF && (got & 0x7FFFFF) != 0;
            Assert.True(expected == got || bothNan, $"dB bits {db:X}: engine {expected:X} C# {got:X}");
        }
    }

    [Fact]
    public void M6_022_G4_G7_The_default_setter_allocates_the_matrices_and_the_first_call_computes_the_robot_bus_matrix()
    {
        // G7: Bus cfg 0x4101 into 0x4101: [1, 0, 0, 0]; into 0x3102: [0.7071068, 0.7071068, 0, 0] (emulated); the defaults [0x94] = 0.5, [0x98] = 1.0, [0x9C] = 100.0, [0xA0] = 0, [0xA4] = 101.0f.
        foreach (var (outCfg, expect) in new[] { (0x4101u, new uint[] { 0x3F800000, 0, 0, 0 }), (0x3102u, new uint[] { 0x3F3504F3, 0x3F3504F3, 0, 0 }) })
        {
            var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8) { Format64 = 0x4101, Config44 = outCfg };
            bus.GetResultingBuffer(_ => { });                                          // the lazy build 0xA4F754 -> the default setter 0xA4F894
            Assert.True(bus.IsFxInstantiated);
            Assert.Equal(new[] { 0x3F000000u, 0x3F800000u, 0x42C80000u, 0u, 0x42CA0000u }, new[] { BitConverter.SingleToUInt32Bits(bus.Param94), BitConverter.SingleToUInt32Bits(bus.Param98), BitConverter.SingleToUInt32Bits(bus.Param9C), bus.ParamA0, BitConverter.SingleToUInt32Bits(bus.OldParamA4) });
            Assert.Equal(0, bus.FlagsC0 & 2);
            Assert.True(bus.MatrixDescriptor34.IsAllocated);
            bus.GainStageA4D994();
            Assert.Equal(expect, bus.MatrixDescriptor34.NextMatrix[..4].ToArray().Select(BitConverter.SingleToUInt32Bits).ToArray());
            Assert.Equal(expect, bus.MatrixDescriptor34.PrevMatrix[..4].ToArray().Select(BitConverter.SingleToUInt32Bits).ToArray());   // G6: the first call flattens
            Assert.Equal(8, bus.FlagsC0 & 8);
        }
    }

    [Fact]
    public void M6_022_G9_The_dead_RTPC_callback_block_and_a_bank_parameter_bus_are_visible_stops()
    {
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8) { Format64 = 0x4101, Config44 = 0x4101 };
        bus.GetResultingBuffer(_ => { });
        bus.FlagsC0 |= 4;                                                              // bit 2 of [bus+0xC0] gates 0xA4DC8C (never set on Cozmo)
        Assert.Throws<WwiseMissingBehaviourException>(() => bus.GainStageA4D994());
        var node = new WwiseRoutingNode { Id = 5, IsBus = true, Byte46 = 0x80 };
        var withBits = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8) { Format64 = 0x4101, Config44 = 0x4101, Context = new WwiseBusContext(node, 0, 0) };
        Assert.Throws<WwiseMissingBehaviourException>(() => withBits.GetResultingBuffer(_ => { }));   // [node+0x46] bit 7: the other branch of the lazy build is not adopted
    }
}
