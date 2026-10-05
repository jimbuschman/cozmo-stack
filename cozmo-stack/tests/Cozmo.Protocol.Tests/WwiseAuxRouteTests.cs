using System.Globalization;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// B-M6b-4 batch 5i, from C40 (M6-006, M6-010, M6-016, M6-022, M6-025, M6-026): the PostEvent playing-id entry writer (<c>0x9A0EF8 -> 0xA03108</c>), the PBI context init stores (<c>0x9BC90C</c>), the limiter creation order (<c>0x9F29E8</c>), and the aux-send route
/// (<c>0xA0BA3C</c> the game object's values, <c>0x9BDA88</c> / <c>0x9BD368</c> the send builder, <c>0x9D4228</c> the merge, <c>0x9D4108</c> / <c>0xA43434</c> the connections, <c>0xA447D8..0xA44938</c> the aux walk).
/// Every expected value of the oracle tests is the output of the REAL engine function under Unicorn (<c>re-analysis/tools/emu/emu_aux.py</c> -> <see cref="WwiseAuxOracle"/>), never the C#'s; the tests that are not oracle-driven take their values from the cited addresses and say so.
/// </summary>
public class WwiseAuxRouteTests
{
    // ------------------------------------------------------------------ parsing

    private static uint H(string s) => uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static float F(string bits) => BitConverter.UInt32BitsToSingle(H(bits));

    private static IEnumerable<(string[] Input, string[] Output)> Cases(string kind)
    {
        foreach (var line in WwiseAuxOracle.Lines)
        {
            if (!line.StartsWith(kind + " ", StringComparison.Ordinal)) continue;
            var parts = line.Split(" => ");
            yield return (parts[0].Split(' ').Skip(1).ToArray(), parts.Length > 1 ? parts[1].Split(' ') : Array.Empty<string>());
        }
    }

    private static WwisePlayingInstance NewPbi(byte flags128 = 0)
        => new(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1, Flags128 = flags128 }, 1, new object(), new byte[0x44], null, continuous: false);

    // ------------------------------------------------------------------ 0x9BD368 (the send builder), 0x9BDA88

    /// <summary>
    /// C40.4 T-A5b (<c>0x9BD368..0x9BD8B4</c>), 4006 engine runs: the real builder's entries, kinds, gains (bit for bit: the engine fast power, <c>0.99903899</c> at 0 dB), the thresholds <c>[0x1052454]</c> / <c>[0x1052450]</c>, the zero-id stop of the game pairs, the independent user entries and
    /// the terminator (written only when the count is 7 or less) are the C#'s.
    /// </summary>
    [Fact]
    public void C40_4_TheSendBuilderMatchesTheEngineOn4000Runs_0x9BD368()
    {
        int n8 = 0, stops = 0, count = 0;
        foreach (var (inp, outp) in Cases("B"))
        {
            count++;
            var pbi = NewPbi();
            pbi.Byte94 = (byte)H(inp[0]);                                        // [ctx+0x88]
            pbi.Word90 = H(inp[1]);                                              // [ctx+0x84]
            var go = new WwiseGameObjectRef();
            for (int i = 0; i < 4; i++)
            {
                var pr = inp[2 + i].Split('.');
                go.Aux24[i] = (H(pr[0]), F(pr[1]));                              // [GO+0x24+8i], [GO+0x28+8i]
                BitConverter.GetBytes(H(inp[6 + i])).CopyTo(pbi.Block80, 4 * i); // [ctx+0x74+4i]
                BitConverter.GetBytes(H(inp[10 + i])).CopyTo(pbi.Block70, 4 * i);// [ctx+0x64+4i]
            }
            var thresholds = new WwiseSendGlobals { GameLinear = F(inp[14]), UserDb = F(inp[15]) };   // the two globals of the run (the oracle writes them directly)
            var block = WwiseAuxRoute.Build9BD368(pbi, go, thresholds);

            Assert.Equal(int.Parse(outp[0]), block.Count);
            Assert.Equal(outp[1] == "1", block.HasTerminator && block.Count < 8);
            if (outp[2] == "-") Assert.Empty(block.Items);
            else
            {
                var items = outp[2..];
                Assert.Equal(items.Length, block.Count);
                for (int k = 0; k < items.Length; k++)
                {
                    var w = items[k].Split('.');
                    Assert.Equal(H(w[0]), block.Items[k].BusId);
                    Assert.Equal(H(w[1]), BitConverter.SingleToUInt32Bits(block.Items[k].Gain));
                    Assert.Equal(H(w[2]), block.Items[k].Kind);
                }
            }
            if (block.Count == 8) n8++;
            if (inp[2 + 1].StartsWith("0.", StringComparison.Ordinal) && inp[2 + 2].Split('.')[0] != "0" && block.Count > 0) stops++;
        }
        Assert.True(count >= 3800, $"{count} builder runs");
        Assert.True(n8 > 200, $"the count-8 (no terminator) shape is in {n8} runs");
        Assert.True(stops > 20, $"the zero-id stop of the game pairs is in {stops} runs");
    }

    /// <summary>C40.4 correction 4 / T-A5b: <c>0x9BD368</c> at 0 dB gives the engine fast power <c>0.99903899</c> (<c>0x3F7FC105</c>), not 1.0, with one game pair of gain 1.0 and the post-STMG thresholds (the explicit run in the oracle).</summary>
    [Fact]
    public void C40_4_TheFastPowerAtZeroDbIsTheEnginesNotOne_0x9BD368()
    {
        var first = Cases("B").First();                                          // the explicit 0 dB run (emu_aux.py special_builder_cases)
        var pbi = NewPbi();
        pbi.Byte94 = 1;
        pbi.Word90 = 0;
        var go = new WwiseGameObjectRef();
        go.Aux24[0] = (0x1111, 1.0f);
        var block = WwiseAuxRoute.Build9BD368(pbi, go, new WwiseSendGlobals());
        Assert.Equal(1, block.Count);
        Assert.Equal(0x3F7FC105u, BitConverter.SingleToUInt32Bits(block.Items[0].Gain));
        Assert.Equal(H(first.Output[2].Split('.')[1]), BitConverter.SingleToUInt32Bits(block.Items[0].Gain));   // the engine's value for the same run
        Assert.Equal(WwiseSendGlobals.ImageGameLinearBits, BitConverter.SingleToUInt32Bits(new WwiseSendGlobals().GameLinear));   // [0x1052454] in the image, before the setter
        Assert.Equal(WwiseSendGlobals.ImageUserDbBits, BitConverter.SingleToUInt32Bits(new WwiseSendGlobals().UserDb));
        Assert.Equal(0x38D2306Au, BitConverter.SingleToUInt32Bits(WwiseSendGlobals.Shared.GameLinear));             // the shared state every reader uses: after the Init.bnk STMG setter(-80, 2)
        Assert.Equal(0xC2A00000u, BitConverter.SingleToUInt32Bits(WwiseSendGlobals.Shared.UserDb));
        Assert.Equal(2, WwiseSendGlobals.Shared.TypeGate);
    }

    /// <summary>
    /// The setter <c>0x9A080C(value, type)</c>, 404 engine runs (powf hooked correctly rounded, as <see cref="WwiseHostMath.Powf"/> is): the return code, <c>[0x1052454] = max(powf, fast power)</c>, <c>[0x1052450] = value</c> and the type gate. At -80 dB the engine gives 0xC2A00000 / 0x38D2306A
    /// (not 0x38D1B717). The STMG reader 0x9B0B14 that calls it in the engine is UNREAD, and the -80 input rests on C10; applying the setter is the host's step, never done here by itself.
    /// </summary>
    [Fact]
    public void C40_4_TheSendThresholdSetterMatchesTheEngine_0x9A080C()
    {
        int count = 0, stored = 0;
        foreach (var (inp, outp) in Cases("S"))
        {
            count++;
            var g = new WwiseSendGlobals { TypeGate = int.Parse(inp[2]) };
            int r = g.ApplySetterA9A080C(F(inp[0]), int.Parse(inp[1]));
            Assert.Equal(int.Parse(outp[0]), r);
            Assert.Equal(H(outp[1]), BitConverter.SingleToUInt32Bits(g.GameLinear));
            Assert.Equal(H(outp[2]), BitConverter.SingleToUInt32Bits(g.UserDb));
            Assert.Equal(int.Parse(outp[3]), g.TypeGate);
            if (r == 1 && H(outp[1]) != WwiseSendGlobals.ImageGameLinearBits) stored++;
        }
        Assert.True(count >= 400 && stored > 50, $"{count} runs, {stored} stored");
        var minus80 = new WwiseSendGlobals();
        Assert.Equal(1, minus80.ApplySetterA9A080C(-80f, 0));
        Assert.Equal(0x38D2306Au, BitConverter.SingleToUInt32Bits(minus80.GameLinear));
        Assert.Equal(0xC2A00000u, BitConverter.SingleToUInt32Bits(minus80.UserDb));
    }

    /// <summary><c>0x9BDA88(ctx)</c> (T-A5a, <c>0x9BDA88..0x9BDACC</c>): 1 when the byte <c>[ctx+0x88]</c> or any of the four user ids is non-zero.</summary>
    [Fact]
    public void C40_4_TheSendGateIsTheUseByteOrAnyUserId_0x9BDA88()
    {
        var pbi = NewPbi();
        Assert.False(WwiseAuxRoute.Continue9BDA88(pbi));
        pbi.Byte94 = 0x10;
        Assert.True(WwiseAuxRoute.Continue9BDA88(pbi));
        pbi.Byte94 = 0;
        for (int i = 0; i < 4; i++)
        {
            Array.Clear(pbi.Block80);
            BitConverter.GetBytes(0x80000000u).CopyTo(pbi.Block80, 4 * i);
            Assert.True(WwiseAuxRoute.Continue9BDA88(pbi));                      // [ctx+0x74], [+0x78], [+0x7C], [+0x80]
        }
    }

    // ------------------------------------------------------------------ 0x9D4228 (the merge)

    private static WwiseAuxEntry[] Entries(string[] tokens, int from, int n)
    {
        var arr = Enumerable.Range(0, 8).Select(_ => new WwiseAuxEntry()).ToArray();
        for (int i = 0; i < n; i++)
        {
            var w = tokens[from + i].Split('.');
            arr[i] = new WwiseAuxEntry { Target = F(w[0]), Current = F(w[1]), Handle = unchecked((int)H(w[2])), Id = H(w[3]), Kind = H(w[4]) };
        }
        return arr;
    }

    /// <summary>
    /// C40.4 T-A6 (<c>0x9D4228..0x9D46EC</c>), 1500 engine runs with the flag area of the stack filled from the run: the merged entries, the new count and whether the engine read an uninitialised flag byte. Without the stack bytes the C# throws
    /// <see cref="WwiseMissingBehaviourException"/> exactly for the runs where the engine read one; with them it gives the engine's entries.
    /// </summary>
    [Fact]
    public void C40_4_TheMergeMatchesTheEngineOn1500RunsAndStopsWhereTheStackIsUninitialised_0x9D4228()
    {
        int reads = 0, count = 0, readsThatMatter = 0;
        foreach (var (inp, outp) in Cases("M"))
        {
            count++;
            bool flag = inp[0] == "1";
            var fill = Enumerable.Range(0, 8).Select(i => Convert.ToByte(inp[1].Substring(2 * i, 2), 16)).ToArray();
            int nold = int.Parse(inp[2]);
            int p = 3;
            string[] oldTokens = inp[p..(p + (nold == 0 ? 1 : nold))];
            p += nold == 0 ? 1 : nold;
            int nblock = int.Parse(inp[p++]);
            var block = new WwiseAuxSendBlock();
            for (int k = 0; k < nblock; k++)
            {
                var w = inp[p + k].Split('.');
                block.Add(new WwiseAuxSendItem(H(w[0]), F(w[1]), H(w[2])));
            }
            int expectN = int.Parse(outp[0]);
            bool uninit = outp[1] == "1";
            var expect = outp[2..];

            void Check(WwiseAuxEntry[] arr, int n)
            {
                Assert.Equal(expectN, n);
                for (int i = 0; i < 8; i++)
                {
                    var w = expect[i].Split('.');
                    Assert.Equal(H(w[0]), BitConverter.SingleToUInt32Bits(arr[i].Target));
                    Assert.Equal(H(w[1]), BitConverter.SingleToUInt32Bits(arr[i].Current));
                    Assert.Equal(H(w[2]), unchecked((uint)arr[i].Handle));
                    Assert.Equal(H(w[3]), arr[i].Id);
                    Assert.Equal(H(w[4]), arr[i].Kind);
                }
            }

            if (uninit)
            {
                reads++;
                Assert.Throws<WwiseMissingBehaviourException>(() => WwiseAuxRoute.Merge9D4228(block, Entries(oldTokens, 0, nold), (byte)nold, flag));
            }
            else
            {
                var arr = Entries(oldTokens, 0, nold);
                Check(arr, WwiseAuxRoute.Merge9D4228(block, arr, (byte)nold, flag));          // no stack byte was read: no host input is needed
            }
            var arr2 = Entries(oldTokens, 0, nold);
            Check(arr2, WwiseAuxRoute.Merge9D4228(block, arr2, (byte)nold, flag, fill));    // with the engine's stack bytes the C# gives the engine's entries either way
            if (uninit && fill.Distinct().Count() > 1) readsThatMatter++;
        }
        Assert.True(count >= 1000, $"{count} merge runs");
        Assert.True(reads > 100, $"{reads} runs read an uninitialised flag byte");
        Assert.True(readsThatMatter > 50);
    }

    /// <summary>
    /// The terminator rule (C40.4 correction 4): <c>0x9D4228</c> stops at the first zero id word of the block, and a block of 8 entries has no word after it (the C# block refuses to give one rather than reading past it).
    /// </summary>
    [Fact]
    public void C40_4_ABlockOfEightEntriesHasNoTerminatorWord_0x9D4228()
    {
        var block = new WwiseAuxSendBlock();
        for (uint i = 0; i < 8; i++) block.Add(new WwiseAuxSendItem(0x10 + i, 0.5f, 1));
        Assert.False(block.HasTerminator);
        Assert.Equal(0x17u, block.IdAt(7));
        Assert.Throws<InvalidOperationException>(() => block.IdAt(8));
        var seven = new WwiseAuxSendBlock();
        for (uint i = 0; i < 7; i++) seven.Add(new WwiseAuxSendItem(0x10 + i, 0.5f, 1));
        Assert.True(seven.HasTerminator);
        Assert.Equal(0u, seven.IdAt(7));
    }

    // ------------------------------------------------------------------ 0x9D4108 (the dispatch) and 0xA43434 (the line and the connection)

    /// <summary>
    /// C40.4 T-A7a (<c>0x9D4108</c>), 600 engine runs: the registry lookup of the entry's bus, the device list walk with the (2,0) exclusion and the mask test, bit 6 of <c>[bus+0xCC]</c> selecting the main device only, the call <c>0xA43434</c> per selected device, and the null dereference when the
    /// list has no (2,0) device (the C# stops with an <see cref="InvalidOperationException"/>).
    /// </summary>
    [Fact]
    public void C40_4_TheDispatchSelectsTheDevicesAsTheEngineDoes_0x9D4108()
    {
        int crashes = 0, bit6Calls = 0, count = 0;
        foreach (var (inp, outp) in Cases("D"))
        {
            count++;
            bool bit6 = inp[0] == "1";
            byte mask = (byte)H(inp[1]);
            bool found = inp[2] == "1";
            var devices = inp[3] == "-" ? Array.Empty<string>() : inp[3..];
            var list = new WwiseOutputDeviceList();
            foreach (var d in devices)
            {
                var w = d.Split('.');
                list.Add(new WwiseDeviceId(H(w[0]), H(w[1])), 0, 0);
            }
            for (int i = 0; i < devices.Length; i++) list.Entries[i].ListenerMask = H(devices[i].Split('.')[2]);
            var bus = new WwiseRoutingNode { Id = 0x1234, IsBus = true, Bit6 = bit6 };
            var seams = new WwiseVoiceLinkSeams { BusLookup9A7EB0 = id => found ? bus : null };
            var linker = new WwiseVoiceLinker(new WwiseMixBusHierarchy(), list, new List<WwiseLiveVoice>(), _ => throw new InvalidOperationException(), seams);
            var calls = new List<string>();
            var voice = new WwiseLiveVoice(1, 8);
            var entry = new WwiseAuxEntry { Id = 0x1234 };
            bool crash = outp[^1] == "1";
            if (crash)
            {
                crashes++;
                Assert.Throws<InvalidOperationException>(() => linker.DispatchAuxEntry9D4108(voice, entry, mask, (b, e, d, v) => calls.Add($"{d.Lo:x}.{d.Hi:x}")));
            }
            else
                linker.DispatchAuxEntry9D4108(voice, entry, mask, (b, e, d, v) =>
                {
                    Assert.Same(bus, b); Assert.Same(entry, e); Assert.Same(voice, v);
                    calls.Add($"{d.Lo:x}.{d.Hi:x}");
                });
            // the engine's calls (the oracle prints them before the crash is known; a crash run's calls precede the null dereference)
            var expected = outp[0] == "-" ? Array.Empty<string>() : outp[..^2];
            Assert.Equal(expected, calls);
            Assert.Equal(found && !crash ? "1" : "0", outp[^2]);                  // the bus release bus->vt+0xC runs once after a lookup that found the bus, unless the null dereference came first
            if (bit6 && calls.Count > 0) bit6Calls++;
        }
        Assert.True(count >= 500);
        Assert.True(crashes > 5, $"{crashes} null-dereference runs");
        Assert.True(bit6Calls > 20);
    }

    /// <summary>
    /// C40.4 T-A7b (<c>0xA43434</c>), 900 engine runs: the line scan (the real <c>0xA68A2C</c> key compare, key2, device, state != 2), the create call when none matches (<c>0xA429F0</c> with the context <c>{bus, [entry+8], 0}</c>, flag 0), <c>[line+0x1CC]</c> bit 0, the early return when the voice already
    /// has a connection to the line, the Sound's output bus bit 6 compared with the aux bus's, and the connect call <c>0xA4C280</c> with <c>[entry+0x10]</c> and 4. The engine's create and connect calls are logged by stand-ins in the oracle and by spies here.
    /// </summary>
    [Fact]
    public void C40_4_TheLineAndConnectionOfAnAuxEntryMatchTheEngine_0xA43434()
    {
        int connects = 0, creates = 0, early = 0, crashes = 0, count = 0;
        foreach (var line in WwiseAuxOracle.Lines)
        {
            if (!line.StartsWith("L ", StringComparison.Ordinal)) continue;
            count++;
            var parts = line[2..].Split(" => ");
            var f = parts[0].Split(" | ");
            var busTokens = f[0].Split(' ').Select(b => b.Split('.')).ToArray();
            var buses = busTokens.Select(b => new WwiseRoutingNode { Id = H(b[0]), IsBus = true, Bit6 = b[1] == "1" }).ToArray();
            var lineTokens = f[1] == "-" ? Array.Empty<string[]>() : f[1].Split(' ').Select(l => l.Split('.')).ToArray();
            var a = f[2].Split(' ');
            int aux = int.Parse(a[0]);
            int key2 = unchecked((int)H(a[1]));
            uint kind = H(a[2]);
            var dev = a[3].Split('.');
            var conns = a[4..];
            var tail = f[3].Split(' ');
            string ctxBusToken = tail[0];
            bool ownerNull = tail[1] == "1";
            bool createOk = tail[2] == "1";
            var outs = parts[1].Split(" | ");
            var expectLog = outs[0] == "-" ? Array.Empty<string>() : outs[0].Replace(" C ", "|C ").Replace(" K ", "|K ").Split('|');
            var o2 = outs[1].Split(' ');
            bool crash = o2[^1] == "1";

            var hierarchy = new WwiseMixBusHierarchy();
            var lines = new List<WwiseMixBus>();
            for (int i = 0; i < lineTokens.Length; i++)
            {
                var t = lineTokens[i];
                int bi = int.Parse(t[0]);
                var l = new WwiseMixBus(new WwiseMixBusKey(i, 0, 0, 0), Array.Empty<WwiseBusFxSlot>(), 8)
                {
                    Context = new WwiseBusContext(bi >= 0 ? buses[bi] : null, unchecked((int)H(t[1])), (byte)H(t[2])),
                    Device = new WwiseDeviceId(H(t[3]), H(t[4])),
                };
                l.State = (int)H(t[5]);
                hierarchy.Append(l);
                lines.Add(l);
            }
            var created = new WwiseMixBus(new WwiseMixBusKey(99, 0, 0, 0), Array.Empty<WwiseBusFxSlot>(), 8);

            // the Sound's node: vt+0x88 returns the bus given by the case (null: no bus), compared by bit 6
            var soundNode = new WwiseRoutingNode { Id = 7, IsBus = false };
            if (ctxBusToken != "-") soundNode.OutputBus = new WwiseRoutingNode { Id = 8, IsBus = true, Bit6 = ctxBusToken == "1" };
            var owner = NewPbi();
            var voice = new WwiseLiveVoice(1, 8);
            if (!ownerNull) voice.BusOwner8 = owner;
            if (conns[0] != "-")
                for (int ci = 0; ci < conns.Length; ci++)
                {
                    int li = int.Parse(conns[ci]);
                    voice.Connections.Add(new WwiseVoiceConnection(li == -1 ? created : lines[li], 1, 1));
                }
            var linker = new WwiseVoiceLinker(hierarchy, new WwiseOutputDeviceList(), new List<WwiseLiveVoice>(), _ => new WwisePbiRouting { Node = soundNode }, new WwiseVoiceLinkSeams());
            var log = new List<string>();
            var entry = new WwiseAuxEntry { Handle = key2, Kind = kind };
            string LineName(WwiseMixBus l) => ReferenceEquals(l, created) ? "N" : lines.IndexOf(l).ToString();
            WwiseMixBus? Create(WwiseBusContext ctx, WwiseDeviceId d, bool flag)
            {
                int busIdx = ctx.Bus is null ? -1 : Array.IndexOf(buses, ctx.Bus);
                log.Add($"C {busIdx}.{unchecked((uint)ctx.Key2):x}.{ctx.Byte:x}.{d.Lo:x}.{d.Hi:x}.{(flag ? 1 : 0):x}");
                return createOk ? created : null;
            }
            void Connect(WwisePbiRouting r, WwiseLiveVoice v, WwiseMixBus l, WwiseDeviceId d, uint arg5) => log.Add($"K {LineName(l)}.{d.Lo:x}.{d.Hi:x}.{arg5:x}");

            var devId = new WwiseDeviceId(H(dev[0]), H(dev[1]));
            if (crash)
            {
                crashes++;
                Assert.True(Assert.ThrowsAny<Exception>(() => linker.LinkAuxA43434(buses[aux], entry, devId, voice, Create, Connect)) is InvalidOperationException, line);
            }
            else
                linker.LinkAuxA43434(buses[aux], entry, devId, voice, Create, Connect);
            Assert.Equal(expectLog, log);

            // [line+0x1CC] bit 0 of every line and of the created one: the engine's bytes (0 or 1)
            var expectFlags = o2[..^1].Select(x => H(x) & 1).ToArray();
            var actualFlags = lines.Select(l => l.Touched ? 1u : 0u).Append(created.Touched ? 1u : 0u).ToArray();
            Assert.Equal(expectFlags, actualFlags);
            if (log.Any(x => x.StartsWith("K "))) connects++;
            if (log.Any(x => x.StartsWith("C "))) creates++;
            if (!crash && log.Count == 0 && actualFlags.Any(x => x == 1)) early++;
        }
        Assert.True(count >= 800, $"{count} line runs");
        Assert.True(connects > 100 && creates > 100 && early > 20 && crashes > 5, $"connects {connects}, creates {creates}, early returns {early}, crashes {crashes}");
    }

    /// <summary>C40.4 T-A8 (<c>0xA68A2C..0xA68A40</c>), 300 engine runs: the line key is <c>[bus+8]</c> for a context with a bus pointer and <c>-(u8) byte</c> without one (<see cref="WwiseBusContext.Key"/>).</summary>
    [Fact]
    public void C40_4_TheLineKeyIsTheBusIdOrTheNegatedByte_0xA68A2C()
    {
        int count = 0;
        foreach (var (inp, outp) in Cases("K"))
        {
            count++;
            var bus = inp[0] == "1" ? new WwiseRoutingNode { Id = H(inp[1]), IsBus = true } : null;
            Assert.Equal(H(outp[0]), new WwiseBusContext(bus, 0, (byte)H(inp[2])).Key);
        }
        Assert.True(count >= 300);
    }

    // ------------------------------------------------------------------ 0xA447D8..0xA44938 (the aux walk)

    /// <summary>
    /// C40.4 T-A8 (<c>0xA447D8..0xA44938</c>), 800 engine runs: which connections the aux walk mixes (<c>[conn+0x68] != 0</c>, <c>[conn+0x18] != 0</c>, <c>([conn+0x6C] &amp; 6) != 6</c>) with the gains <c>{g0, g1}</c> = the sums of the current and the target of the entries whose id equals
    /// <c>0xA68A2C(line+0x4C)</c> (float32, from 0.0f, in entry order), then filter B once before the first dry mix and the dry mixes with {1.0f, 1.0f}.
    /// </summary>
    [Fact]
    public void C40_4_TheAuxWalkSumsTheMatchingEntriesAsTheEngineDoes_0xA447D8()
    {
        int count = 0, auxMixes = 0, nonzero = 0;
        foreach (var line in WwiseAuxOracle.Lines)
        {
            if (!line.StartsWith("W ", StringComparison.Ordinal)) continue;
            count++;
            var parts = line[2..].Split(" => ");
            var f = parts[0].Split(" | ");
            var buses = f[0].Split(' ').Select(b => new WwiseRoutingNode { Id = H(b), IsBus = true }).ToArray();
            var lineTok = f[1].Split(' ').Select(l => l.Split('.')).ToArray();
            var lines = lineTok.Select((t, i) =>
            {
                int bi = int.Parse(t[0]);
                return new WwiseMixBus(new WwiseMixBusKey(i, 0, 0, 0), Array.Empty<WwiseBusFxSlot>(), 8)
                {
                    Context = new WwiseBusContext(bi >= 0 ? buses[bi] : null, -1, (byte)H(t[1])),
                };
            }).ToArray();
            var cf = f[2] == "-" ? Array.Empty<string>() : f[2].Split(' ');
            var tail = f[3].Split(' ');
            int cnt = int.Parse(tail[0]);
            var voice = new WwiseLiveVoice(1, 8);
            foreach (var c in cf)
            {
                var t = c.Split('.');
                var conn = new WwiseVoiceConnection(lines[int.Parse(t[3])], 1, 1) { HasAux = t[0] != "0", Flags6C = (byte)H(t[2]) };
                if (t[1] != "0") conn.Descriptor.Reserve(1, 1);
                voice.Connections.Add(conn);
            }
            for (int i = 0; i < 8; i++)
            {
                var w = tail[1 + i].Split('.');
                voice.AuxEntries2C[i] = new WwiseAuxEntry { Target = F(w[0]), Current = F(w[1]), Handle = unchecked((int)H(w[2])), Id = H(w[3]), Kind = H(w[4]) };
            }
            voice.CountCC = (byte)cnt;
            voice.Buffer.State.ValidFrames = 0;                                   // 0xA4FBEC returns at once on u16 [state+0xE] == 0: the walk is observed, not mixed
            var trace = new List<string>();
            voice.WalkTrace = trace.Add;
            voice.MixConnections(engineOrder: true);
            Assert.Equal(parts[1] == "-" ? "-" : parts[1], trace.Count == 0 ? "-" : string.Join(' ', trace));
            auxMixes += trace.Count(x => x.StartsWith("M ", StringComparison.Ordinal));
            nonzero += trace.Count(x => x.StartsWith("M ", StringComparison.Ordinal) && !x.EndsWith(" 0 0", StringComparison.Ordinal) && !x.EndsWith(" 3f800000 3f800000", StringComparison.Ordinal));
        }
        Assert.True(count >= 800);
        Assert.True(auxMixes > 800 && nonzero > 200, $"{auxMixes} mixes, {nonzero} with summed gains");
    }

    // ------------------------------------------------------------------ the game object stores (0xA0BA3C, 0xA0BA28)

    /// <summary>C40.4 T-A1c (<c>0xA0BA3C..0xA0BBB4</c>), 400 engine runs: the compaction (an id of 0 or a gain that is not above 0, NaN included, is dropped; the kept pairs move down; the rest is zeroed; <c>n &gt; 4</c> changes nothing and returns 2).</summary>
    [Fact]
    public void C40_4_TheGameObjectAuxValuesCompactAsTheEngineDoes_0xA0BA3C()
    {
        int count = 0, dropped = 0;
        foreach (var (inp, outp) in Cases("G"))
        {
            count++;
            int n = int.Parse(inp[0]);
            bool isNull = inp[1] == "1";
            var vals = inp[2] == "-" ? Array.Empty<(uint, float)>() : inp[2..].Select(v => { var w = v.Split('.'); return (H(w[0]), F(w[1])); }).ToArray();
            var go = new WwiseGameObjectRef();
            for (int i = 0; i < 4; i++) go.Aux24[i] = (0xCCCCCCCCu, BitConverter.UInt32BitsToSingle(0xCCCCCCCC));
            int r = go.SetAuxValuesA0BA3C(isNull ? null : vals, n);
            Assert.Equal(int.Parse(outp[0]), r);
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(H(outp[1 + 2 * i]), go.Aux24[i].BusId);
                Assert.Equal(H(outp[2 + 2 * i]), BitConverter.SingleToUInt32Bits(go.Aux24[i].Gain));
            }
            if (n <= 4 && !isNull && vals.Length > go.Aux24.Count(a => a.BusId != 0)) dropped++;
        }
        Assert.True(count >= 400 && dropped > 50, $"{count} runs, {dropped} with a dropped pair");
    }

    /// <summary>C40.4 correction 5 (<c>0xA0BA28..0xA0BA38</c>): the listener mask store writes <c>[GO+0x22]</c> and ORs 0x40 into <c>[GO+0x7F]</c>; the constructor (<c>0xA0B340</c>) has mask 1, <c>[+0x60] = [+0x64] = 1.0f</c>, <c>[+0x78] = -1</c>, and the word <c>0xC0000001</c>.</summary>
    [Fact]
    public void C40_4_TheListenerMaskStoreAndTheConstructorValues_0xA0BA28_0xA0B340()
    {
        foreach (var (inp, outp) in Cases("H"))
        {
            var go = new WwiseGameObjectRef { Word7C = H(inp[0]) };
            go.SetListenerMaskA0BA28((byte)H(inp[1]));
            Assert.Equal(H(outp[0]), go.Word7C);
            Assert.Equal(H(outp[1]), go.Mask22);
        }
        var ctor = WwiseGameObjectRef.Create0A0B340(9);
        Assert.Equal(0xC0000001u, ctor.Word7C);                                  // 0xA0B34C bfi lr,r6,#0,#0x1e; 0xA0B3D0 strb r1,[r4,#0x7f] (r1 = (lr >> 24) | 0xC0)
        Assert.Equal((byte)1, ctor.Mask22);                                      // 0xA0B3CC strb r6,[r4,#0x22] (r6 = 1)
        Assert.Equal(0x3F800000u, BitConverter.SingleToUInt32Bits(ctor.Volume60));   // 0xA0B3D8 str r5,[r4,#0x60] (r5 = 0x3F800000)
        Assert.Equal(0x3F800000u, BitConverter.SingleToUInt32Bits(ctor.Value64));    // 0xA0B3DC
        Assert.Equal(-1, ctor.Key78);                                            // 0xA0B3C8 str r8,[r4,#0x78] (r8 = ~0)
        Assert.All(ctor.Aux24, a => Assert.Equal((0u, 0f), a));                  // 0xA0B3A8.. zero
    }

    /// <summary>
    /// C40.4 T-A1/T-A2b through the live entry (the runtime's queue and pump): <c>0x9A0354</c> refuses more than 4 pairs with 0x1F and queues nothing; a type 0x12 message is handled in the pump by the compaction, a type 0x13 stores <c>[GO+0x60]</c> and <c>[GO+0x78] = -1</c>; a message for an
    /// unregistered object does nothing (<c>0xA0CAFC</c> / <c>0xA0CB78</c> return 2); messages are handled in queue order with the event posts.
    /// </summary>
    [Fact]
    public void C40_4_TheAuxAndOutputBusMessagesReachTheGameObjectThroughThePump_0x9AEDE0_0x9AEDBC()
    {
        var runtime = new WwiseEventRuntime(new[] { OneEventBank(900) }, new WwiseRng(1));
        var go = runtime.RegisterGameObject(7);
        Assert.Equal(0xC0000001u, go.Word7C);
        Assert.Equal((byte)1, go.Mask22);                                         // Anki's registration passes mask 1 (0x8D8CA0 movs r2,#1)
        Assert.Equal(0x1F, runtime.SetGameObjectAuxSendValuesA9A0354(7, Enumerable.Range(1, 5).Select(i => ((uint)i, 1f)).ToArray()));
        Assert.Equal(0, runtime.QueuedMessageCount);                              // nothing queued for 5 pairs
        Assert.Equal(1, runtime.SetGameObjectAuxSendValuesA9A0354(7, new[] { (0xAAu, 1f), (0u, 1f), (0xBBu, 0f), (0xCCu, 0.5f) }));
        Assert.Equal(1, runtime.SetGameObjectOutputBusVolumeA9A044C(7, 0f));
        Assert.Equal(1, runtime.SetGameObjectAuxSendValuesA9A0354(99, new[] { (0xDDu, 1f) }));   // an unregistered object
        Assert.Equal(3, runtime.QueuedMessageCount);
        Assert.Equal(0, runtime.QueuedEventCount);
        Assert.Equal(0u, go.Aux24[0].BusId);                                      // nothing is stored until the pump
        Assert.Equal(3, runtime.PumpMessages());
        Assert.Equal((0xAAu, 1f), go.Aux24[0]);                                   // 0xA0BA3C: kept {0xAA, 1.0}; id 0 dropped; gain 0 dropped
        Assert.Equal((0xCCu, 0.5f), go.Aux24[1]);
        Assert.Equal((0u, 0f), go.Aux24[2]);
        Assert.Equal(0f, go.Volume60);                                            // 0xA0CBE8
        Assert.Equal(-1, go.Key78);                                               // 0xA0CBEC
        Assert.Null(runtime.FindGameObject(99));

        // the Anki wrappers 0x8D913E / 0x8D91BA (read from the Thumb code): an engine flag of 0 returns 0 and queues nothing; 5 pairs give the engine's 0x1F, which is not 1, so 0; a success gives 1
        var anki = new WwiseEventRuntime(new[] { OneEventBank(900) }, new WwiseRng(1));
        Assert.False(anki.SetGameObjectAuxSendValuesA8D913E(false, 7, new[] { (1u, 1f) }));
        Assert.False(anki.SetGameObjectOutputBusVolumeA8D91BA(false, 7, 0f));
        Assert.Equal(0, anki.QueuedMessageCount);
        Assert.False(anki.SetGameObjectAuxSendValuesA8D913E(true, 7, Enumerable.Range(1, 5).Select(i => ((uint)i, 1f)).ToArray()));
        Assert.Equal(0, anki.QueuedMessageCount);
        Assert.True(anki.SetGameObjectAuxSendValuesA8D913E(true, 7, Array.Empty<(uint, float)>()));     // an empty vector: 0x9A0354(goId, 0, 0): the handler zeroes the values
        Assert.True(anki.SetGameObjectOutputBusVolumeA8D91BA(true, 7, 0f));
        Assert.Equal(2, anki.QueuedMessageCount);
    }

    // ------------------------------------------------------------------ the voice tail 0xA4B93C..0xA4BAA0 through the live entry

    /// <summary>
    /// C40.4 T-A4 (<c>0xA4B9BC..0xA4BAA0</c>) through the voice pass's pre-pass entry: with a registered object whose aux values are set, the tail refreshes entry 0 of the 0x4C-byte table (<c>+0x44</c> = the object's mask byte, <c>+0x34</c> = <c>lin(pbi+0x64) * [GO+0x60]</c>), builds the sends,
    /// merges them into <c>voice+0x2C</c>, dispatches each through the required seam and latches bit 1 of <c>[voice+0xCD]</c>; the second pass merges with <c>flag = 1</c>.
    /// </summary>
    [Fact]
    public void C40_4_TheVoiceTailBuildsMergesAndDispatchesTheSends_0xA4B93C()
    {
        var runtime = new WwiseEventRuntime(new[] { OneEventBank(900) }, new WwiseRng(1));
        var go = runtime.RegisterGameObject(7);
        runtime.SetGameObjectAuxSendValuesA9A0354(7, new[] { (0xAAu, 1f) });
        runtime.SetGameObjectOutputBusVolumeA9A044C(7, 0f);
        runtime.PumpMessages();

        var pbi = NewPbi();
        pbi.Flags0E8 = 0x5C;                                                      // bits 0..1 cleared (0x9BEB30 for a non-3D sound): 0x9BE28C returns 0
        pbi.GameObjectRef14 = go;
        pbi.Byte94 = 1;
        pbi.Word90 = 0;
        pbi.Volume3C = 0f;
        pbi.Word64 = 0f;
        var voice = new WwiseLiveVoice(1, 8) { BusOwner8 = pbi, SendTable = new WwiseVoiceSendTable { Capacity = 1 } };
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState());
        var dispatched = new List<(uint Id, float Target, float Current, byte Mask)>();
        pass.AuxDispatch9D4108 = (v, e, mask) => dispatched.Add((e.Id, e.Target, e.Current, mask));

        pass.RefreshVoiceGainA4B93C(voice);
        Assert.Equal(1, voice.SendTable!.Count);                                  // 0xA4BB08 str r2,[r4,#0x14] (r2 = 1)
        Assert.Equal((byte)1, voice.SendTable.Entries[0].PerTargetByte);          // 0xA4B9E8 strb r2,[r3,#0x44] ([GO+0x22] = 1)
        Assert.Equal(0u, BitConverter.SingleToUInt32Bits(voice.SendTable.Entries[0].SendGain));   // 0xA4BA54: [GO+0x60] (0.0f, the muted dry path) * lin
        Assert.Equal(1, voice.CountCC);
        Assert.Equal((byte)2, (byte)(voice.FlagsCD & 2));                         // 0xA4BA94..0xA4BA9C
        var gain = BitConverter.UInt32BitsToSingle(0x3F7FC105);                   // 0.99903899: the fast power at 0 dB times the pair gain 1.0
        Assert.Equal((0xAAu, gain, gain, (byte)1), dispatched.Single());          // the first merge: flag 0, current = target
        dispatched.Clear();
        pass.RefreshVoiceGainA4B93C(voice);                                       // the second pass: [voice+0xCD] bit 1 is set, flag 1
        Assert.Equal((0xAAu, gain, gain, (byte)1), dispatched.Single());          // flag 1 sets current 0, then the old entry with the same id restores current = its old target
        Assert.Equal(1, voice.CountCC);

        // the unread bodies stay visible stops: a non-3D bit and a 3D context throw instead of defaulting
        pbi.Flags0E8 = 0x5D;
        Assert.Throws<WwiseMissingBehaviourException>(() => pass.RefreshVoiceGainA4B93C(voice));     // 0xA4BAB8: 0x9BF8E4 / 0xA5E694 unread
        pbi.Flags0E8 = 0x5C;
        pbi.Flags0E9 |= 2;
        Assert.Throws<WwiseMissingBehaviourException>(() => pass.RefreshVoiceGainA4B93C(voice));     // 0x9BE28C with [ctx+0xDD] bit 1: 0x9BDB18 unread
        pbi.Flags0E9 &= 0xFD;
        pass.AuxDispatch9D4108 = null;
        pbi.Byte94 = 1;
        voice.CountCC = 0;
        Assert.Throws<WwiseMissingBehaviourException>(() => pass.RefreshVoiceGainA4B93C(voice));     // the dispatch seam is required, not silently skipped
    }

    // ------------------------------------------------------------------ PostEvent: 0xA03108 and 0x9A0EF8

    private static void U32(List<byte> b, uint v) => b.AddRange(BitConverter.GetBytes(v));

    private static byte[] Chunk(string tag, byte[] body)
    {
        var b = new List<byte>();
        b.AddRange(System.Text.Encoding.ASCII.GetBytes(tag));
        U32(b, (uint)body.Length);
        b.AddRange(body);
        return b.ToArray();
    }

    /// <summary>A bank with the given events, each with no actions (the posting is what is tested).</summary>
    private static WwiseBank EventsBank(IEnumerable<uint> eventIds)
    {
        var bkhd = new List<byte>();
        U32(bkhd, 120); U32(bkhd, 1); U32(bkhd, 0); U32(bkhd, 0); U32(bkhd, 0);
        var h = new List<byte>();
        var ids = eventIds.ToList();
        U32(h, (uint)ids.Count);
        foreach (var id in ids)
        {
            var payload = new List<byte>();
            U32(payload, id);
            U32(payload, 0);
            h.Add(4);
            U32(h, (uint)payload.Count);
            h.AddRange(payload);
        }
        var file = new List<byte>();
        file.AddRange(Chunk("BKHD", bkhd.ToArray()));
        file.AddRange(Chunk("HIRC", h.ToArray()));
        return WwiseBank.Parse(file.ToArray(), "t.bnk");
    }

    private static WwiseBank OneEventBank(uint id) => EventsBank(new[] { id });

    /// <summary>
    /// C40.1 T-E4c (<c>0xA0428C..0xA04380</c>), 400 engine runs: no entry, a null callback or no 0x20 bit calls nothing; otherwise <c>callback(0x20, {cookie, game object, playing id, event id})</c> runs once with <c>[mgr+0x1C]</c> 0 during it and 1 after, and one broadcast. Anki's flags
    /// (0, 1, 5, 9, 13) never have 0x20, so on the Cozmo path it never calls.
    /// </summary>
    [Fact]
    public void C40_1_TheNodeNotificationCallsBackOnlyForTheBit0x20_0xA0428C()
    {
        int calls = 0, count = 0;
        foreach (var (inp, outp) in Cases("N"))
        {
            count++;
            uint pid = H(inp[0]);
            bool present = inp[1] == "1", hasCallback = inp[3] == "1";
            uint flags = H(inp[4]);
            var table = new WwisePlayingIdTable();
            var seen = new List<(int Type, object Info, bool Idle)>();
            if (present)
            {
                var item = table.GetOrCreate(pid);
                item.Flags48 = flags;
                item.GameObject24 = H(inp[5]);
                item.Cookie44 = H(inp[6]);
                item.EventId20 = H(inp[7]);
                if (hasCallback) item.Callback40 = (t, i) => seen.Add((t, i, table.CallbackIdle1C));
            }
            table.NodeNotificationA0428C(pid);
            if (outp[0] == "-")
            {
                Assert.Empty(seen);
                Assert.Equal(0, table.Broadcasts);
                Assert.Equal(0, int.Parse(outp[1]));
                continue;
            }
            calls++;
            var w = outp[0].Split('.');
            var call = Assert.Single(seen);
            Assert.Equal((int)H(w[0]), call.Type);
            Assert.Equal(new WwiseNodeNotifyInfo(H(w[1]), H(w[2]), H(w[3]), H(w[4])), call.Info);
            Assert.Equal(H(w[5]) != 0, call.Idle);                                // [mgr+0x1C] is 0 during the callback
            Assert.Equal(1, table.Broadcasts);
            Assert.True(table.CallbackIdle1C);                                    // and 1 after (the engine's final byte, outp[2])
            Assert.Equal(1u, H(outp[2]));
        }
        Assert.True(count >= 400 && calls > 40, $"{count} runs, {calls} callbacks");
        // Anki's five flag values never have 0x20: the Cozmo path never calls
        foreach (uint f in new uint[] { 0, 1, 5, 9, 13 }) Assert.Equal(0u, f & 0x20);
    }

    /// <summary>
    /// C40.1 T-E1d (<c>0xA03108..0xA03230</c>), 600 engine runs of the real entry writer: <c>[+0x18] = 0</c>, <c>[+0x1C] = 1</c>, the event id, the game object id, the holder with its reference incremented, the external-source words, the playing id, the cookie, the callback and the flags
    /// (<c>0xFD000 | 0xFF0 | 0xB</c> cleared when the callback is null); an allocation failure returns 2 and leaves no entry.
    /// </summary>
    [Fact]
    public void C40_1_TheEntryWriterMatchesTheEngineOn600Runs_0xA03108()
    {
        int count = 0, failures = 0, holders = 0, maskedFlags = 0;
        foreach (var (inp, outp) in Cases("E"))
        {
            count++;
            uint pid = H(inp[0]), eid = H(inp[1]), go = H(inp[2]);
            bool hasCallback = H(inp[3]) != 0;
            uint cookie = H(inp[4]), flags = H(inp[5]);
            var holder = inp[6] == "-" ? null : new WwiseExternalSourceHolder { RefCount = H(inp[6]) };
            var ext = inp[7].Split('.').Select(H).ToArray();
            bool allocOk = inp[8] == "1";
            var table = new WwisePlayingIdTable { AllocationFails = () => !allocOk };
            Action<int, object>? cb = hasCallback ? (_, _) => { } : null;
            int res = table.CreateEntryA03108(pid, eid, go, cb, cookie, flags, holder, (ext[0], ext[1], ext[2]));
            Assert.Equal(int.Parse(outp[0]), res);
            if (!allocOk)
            {
                failures++;
                Assert.Equal(2, res);
                Assert.Equal(0, table.Count0C);
                Assert.Null(table.Find(pid));
                continue;
            }
            var w = outp[1].Split('.').Select(H).ToArray();
            var item = table.Find(pid)!;
            Assert.Equal(1, table.Count0C);                                       // [mgr+0xC]++
            Assert.Equal(w[0], (uint)item.Count18);                               // +0x18
            Assert.Equal(w[1], (uint)item.Count1C);                               // +0x1C
            Assert.Equal(w[2], item.EventId20);                                   // +0x20
            Assert.Equal(w[3], item.GameObject24);                                // +0x24
            Assert.Equal(w[4] != 0, item.Object28 is not null);                   // +0x28 (the holder pointer)
            Assert.Equal(w[5], item.Word30);
            Assert.Equal(w[6], item.Word34);
            Assert.Equal(w[7], item.Word38);
            Assert.Equal(w[8], item.PlayingId);                                   // +0x3C
            Assert.Equal(w[9] != 0, item.Callback40 is not null);                 // +0x40
            Assert.Equal((object)w[10], item.Cookie44);                                   // +0x44
            Assert.Equal(w[11], item.Flags48);                                    // +0x48
            if (holder is not null) { holders++; Assert.Equal(H(outp[2]), holder.RefCount); Assert.Same(holder, item.Object28); }
            if (!hasCallback && (flags & WwisePlayingIdTable.CallbacklessFlagMask) != 0) maskedFlags++;
        }
        Assert.True(count >= 500 && failures > 5 && holders > 50 && maskedFlags > 100, $"{count} runs, {failures} failures, {holders} holders, {maskedFlags} masked");
    }

    /// <summary>
    /// C40.1 T-E1a/T-E1b/T-E1c through the live entry (<see cref="WwiseEventRuntime.PostEvent"/>), 400 engine runs of the real <c>0x9A0EF8</c> up to its call of <c>0xA03108</c>: an unknown event returns 0 and posts nothing; a known one passes the Post's callback, cookie, flags, event id, game
    /// object and the new playing id to the entry writer, and an entry failure returns 0. The entry the runtime holds is the writer's.
    /// </summary>
    [Fact]
    public void C40_1_PostEventWritesTheEntryWithTheArgumentsThe0x9A0EF8CorePasses()
    {
        var all = Cases("P").ToList();
        var foundIds = all.Where(c => c.Input[7] == "1").Select(c => H(c.Input[0])).Distinct().ToList();
        var runtime = new WwiseEventRuntime(new[] { EventsBank(foundIds) }, new WwiseRng(1));
        int posted = 0, missing = 0, failed = 0;
        foreach (var (inp, outp) in all)
        {
            uint eid = H(inp[0]), go = H(inp[1]), flags = H(inp[2]); object cookie = H(inp[4]);
            bool hasCallback = H(inp[3]) != 0;
            bool found = inp[7] == "1";
            bool entryOk = inp[8] == "1";
            if (!found && foundIds.Contains(eid)) continue;                       // a random id that collides with a known event
            runtime.PlayingIds.AllocationFails = entryOk ? null : () => true;
            int queued = runtime.QueuedEventCount;
            uint id = runtime.PostEvent(eid, go, H(inp[5]), flags, hasCallback ? (_, _) => { } : null, cookie);
            if (!found)
            {
                missing++;
                Assert.Equal(0u, id);                                             // 0x9A0F40 mov r0,#0 (the engine's result for the miss is 0 as well)
                Assert.Equal(queued, runtime.QueuedEventCount);
                Assert.Equal("0", outp[0]);
                continue;
            }
            if (!entryOk)
            {
                failed++;
                Assert.Equal(0u, id);                                             // 0x9A1048..0x9A1058
                Assert.Equal(queued, runtime.QueuedEventCount);
                Assert.Equal("0", outp[0]);
                continue;
            }
            posted++;
            Assert.NotEqual(0u, id);
            Assert.Equal(queued + 1, runtime.QueuedEventCount);
            var item = runtime.PlayingIds.Find(id)!;
            Assert.Equal(H(outp[3]) != 0, item.Callback40 is not null);           // the callback the core passed to 0xA03108
            Assert.Equal((object)H(outp[4]), item.Cookie44);
            Assert.Equal(hasCallback ? H(outp[5]) : H(outp[5]) & ~WwisePlayingIdTable.CallbacklessFlagMask, item.Flags48);   // the flags the core passed, then the writer's callback-less mask
            Assert.Equal(H(outp[6]), item.EventId20);
            Assert.Equal(H(outp[7]), item.GameObject24);
            Assert.Equal(id, item.PlayingId);
            Assert.Equal(1, item.Count1C);                                        // the in-flight message
            Assert.Equal(0, item.Count18);
        }
        Assert.True(posted > 150 && missing > 40 && failed > 10, $"{posted} posts, {missing} misses, {failed} entry failures");
    }

    /// <summary>
    /// C40.1 correction 11 (<c>0x8D8CE4..0x8D8D32</c>): the flags the Anki wrapper passes are 0 (no context) or <c>1 | (ctx&amp;2)&lt;&lt;1 | (ctx&amp;1)&lt;&lt;3</c>, so only 0, 1, 5, 9 and 13 reach the core for any context byte; therefore <c>[pbi+4]</c> never has
    /// <c>0x100000</c> (the play-position paths are unreachable on the Cozmo path), and the dispatch passes the callback and cookie with them, throwing when the Anki trampoline <c>0x8D8D41</c> is not supplied rather than letting the entry drop the EndOfEvent flag.
    /// </summary>
    [Fact]
    public void C40_1_TheAnkiWrapperOnlyPassesFiveFlagValuesAndAContextNeedsTheCallback()
    {
        // the REAL wrapper 0x8D8CE4 (Thumb, under Unicorn) for 256 context words and the null context: the flags and the callback it passes to 0x9A6704
        var engine = new HashSet<uint>();
        int words = 0;
        foreach (var (inp, outp) in Cases("T"))
        {
            if (outp[0] == "none") { Assert.Equal("0", inp[1]); continue; }          // this[0] == 0 returns before the call
            uint flags = H(outp[0]);
            engine.Add(flags);
            if (inp[2] == "0")
            {
                Assert.Equal(0u, flags);                                              // no context: flags 0, callback 0 (0x8D8D20..0x8D8D30)
                Assert.Equal(0u, H(outp[1]));
                Assert.Equal((byte)0, WwiseAudioCallbackContext.PostEventFlagsFor((WwiseAudioCallbackContext?)null));
                continue;
            }
            words++;
            Assert.Equal(flags, WwiseAudioCallbackContext.PostEventFlagsFor((byte)H(inp[0])));   // the C# formula on the word's low byte (only bits 0 and 1 matter)
            Assert.NotEqual(0u, H(outp[1]));                                          // a context carries the callback 0x8D8D41
            Assert.Equal("1", outp[2]);                                               // and the cookie is the context pointer
        }
        Assert.Equal(256, words);
        Assert.Equal(new uint[] { 0, 1, 5, 9, 13 }, engine.OrderBy(x => x).ToArray());   // the engine's own reachable set
        var reached = new HashSet<byte> { WwiseAudioCallbackContext.PostEventFlagsFor((WwiseAudioCallbackContext?)null) };
        for (int b = 0; b < 256; b++) reached.Add(WwiseAudioCallbackContext.PostEventFlagsFor((byte)b));
        Assert.Equal(new byte[] { 0, 1, 5, 9, 13 }, reached.OrderBy(x => x).ToArray());
        Assert.All(reached, f => Assert.Equal(0u, f & 0x100000u));

        var runtime = new WwiseEventRuntime(new[] { OneEventBank(900) }, new WwiseRng(1));
        runtime.RegisterGameObject(7);
        var dispatch = new WwiseAudioInputDispatch(runtime);
        var withContext = new WwisePostAudioEvent(900, 7, 5).ToBytes();
        Assert.Throws<WwiseMissingBehaviourException>(() => dispatch.HandleGameEvents((ushort)WwiseGameToEngineTag.PostAudioEvent, withContext));
        dispatch.Callback8D8D41 = (_, _) => { };
        var r = dispatch.HandleGameEvents((ushort)WwiseGameToEngineTag.PostAudioEvent, withContext);
        var item = runtime.PlayingIds.Find(r.PlayingId)!;
        Assert.Equal(13u, item.Flags48);                                          // the flags survive: the callback is non-null
        Assert.NotNull(item.Callback40);
        var cookieCtx = Assert.IsType<WwiseAudioCallbackContextObject>(item.Cookie44);                 // 0x8D8CF4 / 0x8D8D18..0x8D8D32: the cookie is the context itself
        Assert.Equal(r.Context!.Value, cookieCtx.Context);
        Assert.Equal((ushort)5, cookieCtx.Context.CallbackId);
        var none = dispatch.HandleGameEvents((ushort)WwiseGameToEngineTag.PostAudioEvent, new WwisePostAudioEvent(900, 7, 0).ToBytes());
        var item2 = runtime.PlayingIds.Find(none.PlayingId)!;
        Assert.Null(item2.Callback40);
        Assert.Null(item2.Cookie44);                                              // no context: cookie 0
        Assert.Equal(0u, item2.Flags48);
    }

    // ------------------------------------------------------------------ 0x9BC90C (the PBI context init)

    /// <summary>
    /// C40.3 (<c>0x9BC90C</c>), 500 engine runs: the node chain test (<c>[x+0x40] &amp; 0xE0000</c> on the node, then <c>[x+0x38]</c> else <c>[x+0x34]</c>) setting bit 3 of <c>[ctx+0xDD]</c> with bit 2 the fourth argument and bit 0 set, <c>[ctx+0xDC] = 0x5D</c>, and the
    /// <c>[GO+0x7C]</c> low 30 bits incremented (the top two kept, the carry dropped), the node's <c>vt+8</c> called once.
    /// </summary>
    [Fact]
    public void C40_3_TheContextInitMatchesTheEngineOn500Runs_0x9BC90C()
    {
        int count = 0, chain = 0, wraps = 0;
        foreach (var (inp, outp) in Cases("C"))
        {
            count++;
            var nodeTokens = inp[..^2].Select(n => n.Split('.')).ToArray();
            uint w7c = H(inp[^2]);
            int flag4 = int.Parse(inp[^1]);
            var nodes = nodeTokens.Select((t, i) => new WwiseRoutingNode { Id = (uint)i + 1, IsBus = i % 2 == 1 }).ToArray();
            for (int i = 0; i < nodes.Length; i++)
            {
                int oi = int.Parse(nodeTokens[i][0]), pi = int.Parse(nodeTokens[i][1]);
                uint w40 = H(nodeTokens[i][2]);
                if (nodes[i].IsBus) nodes[i].Word40 = w40; else nodes[i].Node40 = w40;     // a bus reads [bus+0x40] from its loader; any other node's word is the caller's
                nodes[i].OutputBus = oi >= 0 ? nodes[oi] : null;
                nodes[i].Parent = pi >= 0 ? nodes[pi] : null;
            }
            bool walked = WwisePbiContextInit.ChainFlag9BC9FC(nodes[0]);
            var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1, Flags128 = (byte)(flag4 << 3) }, 1, new object(), new byte[0x44], null, continuous: false, ctxNodeChainFlag: walked);
            Assert.Equal(H(outp[0]), pbi.Flags0E9);                               // [ctx+0xDD]
            Assert.Equal(H(outp[1]), pbi.Flags0E8);                               // [ctx+0xDC]
            var go = new WwiseGameObjectRef { Word7C = w7c };
            WwisePbiContextInit.AddGameObjectReference(go);
            Assert.Equal(H(outp[2]), go.Word7C);
            Assert.Equal("0", outp[3]);                                           // the node's vt+8 ran once, on node 0
            if (walked) chain++;
            if ((w7c & 0x3FFFFFFF) == 0x3FFFFFFF) wraps++;
        }
        Assert.True(count >= 500 && chain > 50 && wraps > 5, $"{count} runs, {chain} with the chain bit, {wraps} wraps");
    }

    /// <summary>
    /// C40.3 T-N1 on the shipped banks: all 15 buses of Init.bnk have byte C = 2 (bit 0 clear), so no bus stores <c>0xE0000</c> and the chain test is false for the chain of every Cozmo.bnk Sound (the bus's word is built the way the loader builds it: bit 0 of byte C gives <c>0xE0000</c>).
    /// </summary>
    [Fact]
    public void C40_3_NoShippedChainHasTheExtendedLineBit_0x9C6540()
    {
        var meta = FindMeta();
        if (meta is null) { Assert.Fail("re-analysis/obb/sound_meta (the unpacked shipped banks) was not found above the test binaries"); return; }
        var nodes = new Dictionary<uint, WwiseNode>();
        foreach (var name in new[] { "Init.bnk", "Cozmo.bnk" })
        {
            var path = Directory.EnumerateFiles(meta, name, SearchOption.AllDirectories).First();
            foreach (var o in WwiseBank.Parse(File.ReadAllBytes(path), name).Objects.Values)
                if (WwiseHierarchy.TryRead(o, out _) is { } n) nodes[n.Id] = n;
        }
        var buses = nodes.Values.OfType<WwiseBusNode>().ToList();
        Assert.Equal(15, buses.Count);
        Assert.All(buses, b => Assert.Equal((byte)2, b.ByteC));

        var routing = new Dictionary<uint, WwiseRoutingNode>();
        WwiseRoutingNode Route(uint id)
        {
            if (routing.TryGetValue(id, out var r)) return r;
            var n = nodes[id];
            r = new WwiseRoutingNode { Id = id, IsBus = n is WwiseBusNode };
            routing[id] = r;
            if (n is WwiseBusNode bus)
            {
                r.Word40 = (bus.ByteC & 1) != 0 ? 0xE0000u : 0u;                  // 0x9C6528..0x9C6544: bit 0 of byte C stores 0xE0000
                r.OutputBus = bus.ParentBusId != 0 ? Route(bus.ParentBusId) : null;
            }
            else
            {
                r.OutputBus = n.Params.BusId != 0 ? Route(n.Params.BusId) : null;
                r.Parent = n.Params.ParentId != 0 && nodes.ContainsKey(n.Params.ParentId) ? Route(n.Params.ParentId) : null;
            }
            return r;
        }
        int sounds = 0;
        foreach (var s in nodes.Values.OfType<WwiseSoundNode>().Where(s => s.Bank == "Cozmo.bnk"))
        {
            sounds++;
            Assert.False(WwisePbiContextInit.ChainFlag9BC9FC(Route(s.Id)));
        }
        Assert.Equal(2231, sounds);
        // the same chain with a bus whose byte C has bit 0 set is the true case (the walk is not a constant)
        var extended = new WwiseRoutingNode { Id = 1, IsBus = true, Word40 = 0xE0000 };
        var under = new WwiseRoutingNode { Id = 2, IsBus = false, OutputBus = extended };
        Assert.True(WwisePbiContextInit.ChainFlag9BC9FC(under));
    }

    // ------------------------------------------------------------------ the seams the §2 audit listed (C40 corrections 7, 8): required, never silent

    /// <summary>
    /// C40 correction 7: <c>WwisePlaybackBridge.Powf</c> (<c>0xA564D0 bl powf</c>) no longer defaults to <see cref="MathF.Pow"/>, the host runtime's own last bit, but to the stack's one host function <see cref="WwiseHostMath.Powf"/> (EQUIVALENT_IMPLEMENTATION, C38.2: the float32 correctly rounded
    /// result; the phone's libm is not shipped).
    /// </summary>
    [Fact]
    public void C40_5_ThePowfDefaultIsTheStacksOneHostFunction()
    {
        var bridge = new WwisePlaybackBridge();
        Assert.Equal(typeof(WwiseHostMath), bridge.Powf.Method.DeclaringType);
        Assert.Equal("Powf", bridge.Powf.Method.Name);
        foreach (float q in new[] { -0.6666667f, -0.5f, 0.1234f, 0.4166667f, 0.25f })
            Assert.Equal(BitConverter.SingleToUInt32Bits(WwiseHostMath.Powf(2f, q)), BitConverter.SingleToUInt32Bits(bridge.Powf(2f, q)));
    }

    /// <summary>
    /// C40 correction 7: the seams whose engine bodies are unread throw <see cref="WwiseMissingBehaviourException"/> instead of defaulting: the pass's <c>SourceOwner</c> (<c>[[voice+0xD4]+0xC]</c>, the first statement of <c>PrePassVoiceA55750</c>), the media resolver / source factory of <c>DefaultSource</c>, and the
    /// dispatch of <c>0x9D4228</c> (<c>0x9D4108</c>); the notification queue wiring (<c>WwiseVoiceBusPass.FlushPbiNotifications</c>) is covered by <c>WwiseNotificationQueueTests</c>.
    /// </summary>
    [Fact]
    public void C40_5_TheUnreadSeamsStopVisiblyInsteadOfDefaulting()
    {
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8);
        Assert.Throws<WwiseMissingBehaviourException>(() => pass.PrePassVoiceA55750(voice));          // no SourceOwner

        var bridge = new WwisePlaybackBridge();
        var defaultSource = typeof(WwisePlaybackBridge).GetMethod("DefaultSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(WwiseSourceFactory.VorbisPlugin, 1, 12345, 0, 0), new byte[0x44], null, continuous: false);
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => defaultSource.Invoke(bridge, new object[] { pbi }));
        Assert.IsType<WwiseMissingBehaviourException>(ex.InnerException);                             // no MediaFor and no SourceFactory is not the native null-source path
        var odd = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 2, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, continuous: false);
        ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => defaultSource.Invoke(bridge, new object[] { odd }));
        Assert.IsType<WwiseMissingBehaviourException>(ex.InnerException);
    }

    private static string? FindMeta()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var candidate = Path.Combine(d.FullName, "re-analysis", "obb", "sound_meta");
            if (Directory.Exists(candidate)) return candidate;
            d = d.Parent;
        }
        return null;
    }

    // ------------------------------------------------------------------ 0x9F29E8 (the limiter creation order) and the shipped limit nodes

    /// <summary>
    /// C40.2 T-L1 / correction 10 on the shipped banks: only two Cozmo.bnk nodes carry a max-instances limit, root ActorMixer 62050212 (max 1, global) and ActorMixer 66225135 (max 5, under 682998829); 1872 Sounds are under the first (1862 of them directly under the chain through ActorMixer 13023553 and the other 10 under RanSeq 461334142; only the 1872 total is asserted) and 11 under the second.
    /// The limiter's subscription key block is <c>{0, 0, 0, 0xFF, 0xFF, 0}</c> (<c>0x9F2A58..0x9F2A70</c>).
    /// </summary>
    [Fact]
    public void C40_2_TheShippedLimitNodesAreTheTwoTheInventoryNames_0x9F29E8()
    {
        var meta = FindMeta();
        if (meta is null) { Assert.Fail("re-analysis/obb/sound_meta (the unpacked shipped banks) was not found above the test binaries"); return; }
        var nodes = new Dictionary<uint, WwiseNode>();
        var path = Directory.EnumerateFiles(meta, "Cozmo.bnk", SearchOption.AllDirectories).First();
        foreach (var o in WwiseBank.Parse(File.ReadAllBytes(path), "Cozmo.bnk").Objects.Values)
            if (WwiseHierarchy.TryRead(o, out _) is { } n) nodes[n.Id] = n;
        var limited = nodes.Values.Where(n => n is not WwiseBusNode && (n.Params.AdvancedMaxInstancesRaw & 0x3FF) != 0).ToList();
        Assert.Equal(new uint[] { 62050212, 66225135 }, limited.Select(n => n.Id).OrderBy(x => x).ToArray());
        var root = nodes[62050212];
        Assert.Equal(1, root.Params.AdvancedMaxInstancesRaw & 0x3FF);
        Assert.NotEqual(0, root.Params.AdvancedByte0 & 4);                         // the global-limit bit (node+0x45 bit 6 via byte0 bit 2)
        Assert.Equal(5, nodes[66225135].Params.AdvancedMaxInstancesRaw & 0x3FF);
        uint RootOf(uint id) { while (nodes[id].Params.ParentId is var p && p != 0 && nodes.ContainsKey(p)) id = p; return id; }
        var sounds = nodes.Values.OfType<WwiseSoundNode>().ToList();
        Assert.Equal(1872, sounds.Count(s => RootOf(s.Id) == 62050212));
        Assert.Equal(new WwiseListenerKey(0, 0, 0, 0xFF, 0xFF, 0), new WwiseNodeLimiter().Key10);
    }
}
