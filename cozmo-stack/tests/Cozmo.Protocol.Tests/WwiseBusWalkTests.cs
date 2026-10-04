using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>A node of a bus-walk scenario (the same fields emu_bus.py builds in emulated memory).</summary>
internal sealed class BusNodeSpec
{
    public int Parent = -1, Bus = -1, Cat;
    public uint[]? Fx;
    public byte B68, B46;
    public uint W40, W54;
    public (byte Id, uint Bits)[]? Base, Ranged;
    public ulong? Mask;
    public uint[] Duck8C = Array.Empty<uint>(), DuckA8 = Array.Empty<uint>();
    public uint MaxDuck = 0xC2C0999A;
    public (byte Id, uint Bits)[]?[] States = Array.Empty<(byte, uint)[]?>();
}

/// <summary>Doubles for the node graph the Play path walks: every hierarchy node gets a runtime node whose output bus is a non-collapsed bus (byte [+0x68] = 1), so 0x9C54E8 holds and the bus contributes nothing.</summary>
internal static class RuntimeDoubles
{
    public static WwiseRoutingNode? NodeWithBus(WwiseNode n)
        => new WwiseRoutingNode { Id = n.Id, SubscriptionKey10 = 0x1000 + n.Id * 0x10, OutputBus = new WwiseRoutingNode { Id = 900, IsBus = true, Byte68 = 1 } };
}

internal sealed record RtpcValueSpec(uint Id, uint DefaultBits, bool RootValid, uint RootBits);

internal sealed record RtpcSubSpec(int Node, uint Param, uint Type, uint Accum, (uint Id, byte Scaling, (uint X, uint Y, uint Interp)[] Points)[] Curves);

/// <summary>
/// M6-025 / M6-010 / M6-009 (C34.1 B2, B3, B7, B8, B9, B4; C34.2 R1, R2, R3): the bus walk and the RTPC evaluation. Every expected value is the engine's own output from re-analysis/tools/emu/emu_bus.py (the real
/// 0x9F4BB8, 0x9BDA6C, 0x9C54E8 with the shipped vt+0x44 slots, 0x9F9CDC, 0x9C39DC, 0xA11590 with 0xA17878 / 0xA17724 / 0xA17280, and the bus constructor 0x9C3620 run under Unicorn on nodes and an RTPC manager built in
/// emulated memory); the generated WwiseBusWalkOracle.cs holds the inputs and the outputs. The curve evaluation 0xA14E28 is the engine's real code in the oracle (batch 5d, C35) and the real port in the C#; the Python stand-in is the body C34 does not adopt, 0x9E6748. Floats are
/// compared as bits. A scenario whose engine run reached the stand-in 0x9E6748 is asserted to stop in the C# (WwiseMissingBehaviourException), not to return a value.
/// </summary>
public class WwiseBusWalkTests
{
    private static WwiseRoutingNode[] Build(BusNodeSpec[] specs)
    {
        var nodes = new WwiseRoutingNode[specs.Length];
        for (int i = 0; i < specs.Length; i++)
            nodes[i] = new WwiseRoutingNode { Id = (uint)(100 + i), IsBus = true, SubscriptionKey10 = (uint)(0x1000 + 0x100 * i + 0x10) };
        for (int i = 0; i < specs.Length; i++)
        {
            var s = specs[i];
            var n = nodes[i];
            n.Parent = s.Parent >= 0 ? nodes[s.Parent] : null;
            n.OutputBus = s.Bus >= 0 ? nodes[s.Bus] : null;
            n.Category44 = s.Cat;
            if (s.Fx is not null)
            {
                var chunk = new WwiseFxChunk();
                for (int k = 0; k < 4; k++) chunk.Ids[k] = s.Fx[k];
                n.Fx28 = chunk;
            }
            n.Byte68 = s.B68;
            n.Byte46 = s.B46;
            n.Word40 = s.W40;
            n.Word54 = s.W54;
            if (s.Base is not null) n.BaseBundle3C = new WwiseParamBundle(s.Base.Select(x => x.Id).ToArray(), s.Base.Select(x => x.Bits).ToArray());
            if (s.Ranged is not null) n.RangedBundle24 = new WwiseParamBundle(s.Ranged.Select(x => x.Id).ToArray(), s.Ranged.Select(x => x.Bits).ToArray());
            n.SubscriptionMask14 = s.Mask;
            foreach (uint d in s.Duck8C) n.Duck8C.Add(BitConverter.UInt32BitsToSingle(d));
            foreach (uint d in s.DuckA8) n.DuckA8.Add(BitConverter.UInt32BitsToSingle(d));
            n.MaxDuck6C = BitConverter.UInt32BitsToSingle(s.MaxDuck);
            if (s.States.Length > 0)
                n.States18 = s.States.Select(b => new WwiseStateListItem
                {
                    Bundle10 = b is null ? null : new WwiseParamBundle(b.Select(x => x.Id).ToArray(), b.Select(x => x.Bits).ToArray()),
                }).ToList();
        }
        return nodes;
    }

    private static (WwiseRtpcStore Store, List<string> Log) BuildStore(WwiseRoutingNode[] nodes, RtpcValueSpec[] values, RtpcSubSpec[] subs)
    {
        var store = new WwiseRtpcStore();
        foreach (var v in values)
        {
            store.Apply(new WwiseStmgParam(v.Id, BitConverter.UInt32BitsToSingle(v.DefaultBits), 0, 0f, 0f, false));
            if (v.RootValid) store.SetParameter(v.Id, BitConverter.UInt32BitsToSingle(v.RootBits), 0);
        }
        foreach (var s in subs)
        {
            var curves = new List<WwiseRtpc>();
            foreach (var (id, scaling, pts) in s.Curves)
                curves.Add(new WwiseRtpc(id, 0, 0, 0, 0, scaling,
                    pts.Select(p => (BitConverter.UInt32BitsToSingle(p.X), BitConverter.UInt32BitsToSingle(p.Y), p.Interp)).ToArray()));
            store.AddSubscription(new WwiseRtpcSubscription { Key1 = nodes[s.Node].SubscriptionKey10, Param = s.Param, Type = s.Type, Accumulate = s.Accum, Curves = curves });
        }
        var log = new List<string>();
        // the REAL port of 0xA14E28 (the store's default); the wrapper only records each call, as the oracle's code hook does
        store.CurveA14E28 = (c, x) => { log.Add("A14E28"); return WwiseRtpcCurveA14E28.Evaluate(c, x); };
        return (store, log);
    }

    private static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);

    // ------------------------------------------------------------------ B2, B3

    [Theory]
    [InlineData("leaf_to_bus")]
    [InlineData("self_has_bus")]
    [InlineData("no_bus")]
    [InlineData("nearest_wins")]
    public void B3_0x9F4BB8_IsTheNearestAncestorOrSelfWithAnOutputBusLink(string name)
    {
        // C34.1 B3, emu_bus.py case_9f4bb8: the loop 0x9F4BB8..0x9F4BD8 returns [node+0x38] of the first node that has one, walking [node+0x34]; none gives 0.
        var (specs, start, expected) = BusWalkOracle.F4BB8[name];
        var nodes = Build(specs);
        var r = WwiseBusWalk.A9F4BB8(nodes[start]);
        Assert.Equal(expected, r is null ? -1 : Array.IndexOf(nodes, r));
    }

    [Theory]
    [InlineData("e9_0")]
    [InlineData("e9_1")]
    [InlineData("e9_4")]
    [InlineData("e9_5")]
    public void B2_0x9BDA6C_ReturnsNoBusWhenE9Bit2IsSetElseTheWalkOfTheNode(string name)
    {
        // C34.1 B2, emu_bus.py case_9bda6c: [ctx+0xDD] & 4 (= [pbi+0xE9] & 4) returns 0 without reading the node; else 0x9F4BB8([ctx+0xD4]).
        var (specs, e9, expected) = BusWalkOracle.BDA6C[name];
        var nodes = Build(specs);
        var r = WwiseBusWalk.FirstOutputBus9BDA6C(e9, nodes[0]);
        Assert.Equal(expected, r is null ? -1 : Array.IndexOf(nodes, r));
    }

    // ------------------------------------------------------------------ B7

    [Theory]
    [InlineData("all_clear")]
    [InlineData("fx_slot0")]
    [InlineData("fx_slot3")]
    [InlineData("fx_chunk_all_zero")]
    [InlineData("audio_device_0c")]
    [InlineData("byte68")]
    [InlineData("b46_bit7")]
    [InlineData("b46_bit6_only")]
    [InlineData("no_output_bus")]
    [InlineData("w40_e0000")]
    [InlineData("w40_c0000")]
    [InlineData("w40_other")]
    [InlineData("w54")]
    [InlineData("actor_mixer_cat1")]
    public void B7_0x9C54E8_IsOneWhenAnyOfTheSevenTestsHolds(string name)
    {
        // C34.1 B7, emu_bus.py case_9c54e8 (the real function with the shipped vt+0x44 slots): FX id in any of the four slots (a chunk of zeros is not one), vt+0x44 == 0xC, byte [+0x68], [+0x46] bit 7, no
        // output bus, [+0x40] & 0xE0000 (0xC0000 overlaps that mask, 0x1F000 does not), [+0x54]; otherwise 0 ("collapsed").
        var (specs, start, expected) = BusWalkOracle.C54E8[name];
        var nodes = Build(specs);
        Assert.Equal(expected == 1, nodes[start].A9C54E8());
    }

    [Fact]
    public void B7_TheVt88PredicateOfTheLinkerIsTheSameBody()
    {
        // The seven tests of 0x9C2A30 (C23) and 0x9C54E8 (C34.1 B7) are one predicate: Vt88 returns the bus itself exactly when A9C54E8 holds.
        foreach (var (name, (specs, start, expected)) in BusWalkOracle.C54E8)
        {
            var nodes = Build(specs);
            Assert.Equal(expected == 1, ReferenceEquals(nodes[start].Vt88(), nodes[start]));
        }
    }

    // ------------------------------------------------------------------ B8

    [Theory]
    [InlineData("mask1")]
    [InlineData("mask2")]
    [InlineData("mask4")]
    [InlineData("mask8")]
    [InlineData("mask10")]
    [InlineData("mask1f")]
    [InlineData("gate_off")]
    [InlineData("mask0")]
    [InlineData("no_list")]
    [InlineData("duplicate_id_first_wins")]
    public void B8_0x9F9CDC_AddsTheFirstFloatOfTheMatchingEntryOfEveryStateItem(string name)
    {
        // C34.1 B8, emu_bus.py case_9f9cdc: mask bit 1 adds property 0 to out[0] and property 6 to out[6] (+0x18); bit 2 property 2 (+8); bit 4 property 3 (+0xC); bit 8 property 4 (+0x10); bit 0x10 property 5 (+0x14);
        // a node without [+0x46] bit 0 or with no list adds nothing; a null bundle is skipped; the first of two entries with one id is read.
        var (specs, mask, expected) = BusWalkOracle.F9CDC[name];
        var nodes = Build(specs);
        var block = new float[7];
        WwiseBusWalk.A9F9CDC(nodes[0], block, mask);
        Assert.Equal(expected, block.Select(Bits).ToArray());
    }

    // ------------------------------------------------------------------ B9

    [Fact]
    public void B9_0x9C39DC_EveryOracleScenarioMatchesTheEnginesResultBitForBit()
    {
        // C34.1 B9, emu_bus.py case_9c39dc: base bundle, ranged first float, state list, ducking lists with the [+0x6C] floor (vcmp / vmovgt: a NaN sum stays), the RTPC bit (T[p]) through 0xA11590, and the flag semantics
        // of the recursion (flag 1 always recurses; any other flag stops at a parent that 0x9C54E8 holds for). A scenario that reached the stand-in 0x9E6748 in the engine stops in the C#.
        Assert.NotEmpty(BusWalkOracle.C39DC);
        foreach (var (name, (specs, start, flag, p, values, subs, expected, log)) in BusWalkOracle.C39DC)
        {
            var nodes = Build(specs);
            var (store, curveLog) = BuildStore(nodes, values, subs);
            if (log.Contains("9E6748"))
            {
                Assert.Throws<WwiseMissingBehaviourException>(() => WwiseBusWalk.A9C39DC(nodes[start], flag, p, store));
                continue;
            }
            float r = WwiseBusWalk.A9C39DC(nodes[start], flag, p, store);
            Assert.True(expected == Bits(r), $"{name}: engine 0x{expected:X8}, C# 0x{Bits(r):X8}");
            Assert.True(log.SequenceEqual(curveLog), $"{name}: the curve evaluations differ");
        }
    }

    [Fact]
    public void B9_AParameterOtherThanZeroAndFiveFaultsInTheEngineAndThrowsHere()
    {
        // 0x9C3B68..0x9C3BF0: p not in {0, 5} loads through a null pointer (udf).
        var nodes = Build(new[] { new BusNodeSpec() });
        Assert.Throws<InvalidOperationException>(() => WwiseBusWalk.A9C39DC(nodes[0], 0, 3, null));
    }

    [Fact]
    public void B9_ASubscriptionBitWithoutTheRtpcManagerIsAVisibleStop()
    {
        var nodes = Build(new[] { new BusNodeSpec { Mask = 1UL << 5 } });
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseBusWalk.A9C39DC(nodes[0], 0, 5, null));
    }

    [Fact]
    public void B9_ABundleWithCountZeroIsNotModelled()
    {
        // The scan loop reads the byte after the count byte for a count of 0 (indeterminate memory); the C# stops.
        var nodes = Build(new[] { new BusNodeSpec { Base = Array.Empty<(byte, uint)>() } });
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseBusWalk.A9C39DC(nodes[0], 0, 5, null));
    }

    // ------------------------------------------------------------------ R1, R2, R3

    [Fact]
    public void R1_R2_R3_0xA11590_EveryOracleScenarioMatchesTheEnginesResultBitForBit()
    {
        // C34.2 R1 (the hash by (key1, param); empty table, not found: 0.0f), R2 (0xA17878 sums from 0.0f, 0xA17724 multiplies from 1.0f when [e+0x28] == 2; the entry's STMG default is the input when no value is valid), R3 (an id
        // absent from the store: parameter 0 or 7 with type != 1 skips the curve; otherwise 0x9E6748, unread, stops the C#). emu_bus.py case_a11590.
        Assert.NotEmpty(BusWalkOracle.A11590);
        foreach (var (name, (specs, param, values, subs, expected, log)) in BusWalkOracle.A11590)
        {
            var nodes = Build(specs);
            var (store, curveLog) = BuildStore(nodes, values, subs);
            var key = WwiseGainRtpcKey.Empty;
            if (log.Contains("9E6748"))
            {
                Assert.Throws<WwiseMissingBehaviourException>(() => store.A11590(nodes[0].SubscriptionKey10, param, key));
                continue;
            }
            float r = store.A11590(nodes[0].SubscriptionKey10, param, key);
            Assert.True(expected == Bits(r), $"{name}: engine 0x{expected:X8}, C# 0x{Bits(r):X8}");
            Assert.True(log.SequenceEqual(curveLog), $"{name}: the curve evaluations differ");
        }
    }

    [Fact]
    public void R3_TheLegacyEvaluateKeepsItsHeadBehaviourForAnIdNotInTheStore()
    {
        // The legacy Evaluate (used by WwiseGain) is a parallel copy of 0xA17878 and keeps its HEAD behaviour: every not-in-store id throws NotSupportedException. The engine's R3 flow (0xA17280: skip for
        // type != 1 with parameter 0 or 7, 0x9E6748 otherwise) lives only in the A11590 path, checked against the engine by the BusWalk oracle tests.
        var store = new WwiseRtpcStore();
        foreach (var (type, param) in new[] { (0, 0), (0, 6), (0, 2), (1, 0) })
        {
            var rtpc = new WwiseRtpc(900, (byte)type, 1, (byte)param, 0, 0, Array.Empty<(float, float, uint)>());
            Assert.Throws<NotSupportedException>(() => store.Evaluate(new[] { rtpc }, 0, 0, out _));
        }
    }

    // ------------------------------------------------------------------ B4

    [Theory]
    [InlineData(0)]
    [InlineData(0xC)]
    [InlineData(0xA)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void B4_TheBusConstructorStoresTheFieldsOfTheEnginesConstructor(int category)
    {
        // C34.1 B4, emu_bus.py case_ctor: 0x9C3620 on memory filled with 0xAA, the callees 0x9F402C / 0xA19F94 / 0x9F40F4 stood in: [+0x68] word 0, [+0x6C] 0xC2C0999A, [+0x54] 0, [+0xCC] 0x30, [+0x46] bit 2 set
        // for vt+0x44 in {0, 0xA, 0xC} (0xAA -> 0xAE) and left alone for 1, 3, 5 (0xAA).
        var (word68, maxDuck, word54, b46, bcc, id, calls) = BusWalkOracle.Ctor[category];
        var log = new List<string>();
        var seams = new WwiseBusCtorSeams
        {
            BaseCtor9F402C = n => { log.Add("9F402C"); n.Word68 = 0xAAAAAAAA; n.Word54 = 0xAAAAAAAA; n.Byte46 = 0xAA; n.ByteCC = 0xAA; n.MaxDuck6C = BitConverter.UInt32BitsToSingle(0xAAAAAAAA); n.Duck8C.Add(1f); n.DuckA8.Add(2f); },
            Init9A19F94 = _ => log.Add("A19F94"),
            Post9F40F4 = _ => log.Add("9F40F4"),
        };
        var bus = WwiseBusWalk.ConstructBus9C3620(0x1234ABCD, category, seams);
        Assert.Equal((word68, maxDuck, word54, b46, bcc), (bus.Word68, Bits(bus.MaxDuck6C), bus.Word54, bus.Byte46, bus.ByteCC));
        Assert.Equal(id, bus.Id);
        Assert.Equal(calls, log);
        Assert.Empty(bus.Duck8C);
        Assert.Empty(bus.DuckA8);
        Assert.True(bus.IsBus);
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseBusWalk.ConstructBus9C3620(1, 0, new WwiseBusCtorSeams()));
    }

    // ------------------------------------------------------------------ B6

    [Fact]
    public void B6_ChannelConfigAndFlagByteC_EveryOracleScenarioMatchesTheEnginesBusReader()
    {
        // C34.1 B6, emu_bus.py case_reader (the whole reader 0x9C6420 under Unicorn with 0x9F627C, 0x9F68D8, 0x9F6DB4, 0xA4454C, 0x9C62AC recorded): the channel-config word stores [+0x68] (a kind of 1 stores the popcount
        // of (cfg >> 12) & 0x3FF3F with nibble 1; any other kind the byte and the nibble as they are), 0xA4454C runs when the byte, the nibble or bits 12..31 changed, [+0x46] bit 0 is set; byte C bit 0 sets or clears
        // [+0x40] & 0xE0000, bit 1 is [+0xCC] bit 3, and flag F bit 3 runs 0x9C62AC.
        Assert.NotEmpty(BusWalkOracle.Reader);
        foreach (var (name, (cfg, old, f, c, w40In, word68, b46, w40, bcc, changed, bit3)) in BusWalkOracle.Reader)
        {
            var bus = new WwiseRoutingNode { Id = 0x1234, IsBus = true, Word68 = old, Byte46 = 0x20, Word40 = w40In, ByteCC = 0x30 };
            bool changedCalled = false, bit3Called = false;
            var seams = new WwiseBusReaderSeams { ChannelConfigChangedA4454C = _ => changedCalled = true, FlagBit3A9C62AC = _ => bit3Called = true };
            WwiseBusWalk.ApplyChannelConfig(bus, cfg, seams);
            WwiseBusWalk.ApplyFlagByteC(bus, c, f, seams);
            Assert.True((word68, b46, w40, bcc, changed, bit3) == (bus.Word68, bus.Byte46, bus.Word40, bus.ByteCC, changedCalled, bit3Called),
                $"{name}: engine ({word68:X8}, {b46:X2}, {w40:X8}, {bcc:X2}, {changed}, {bit3}) C# ({bus.Word68:X8}, {bus.Byte46:X2}, {bus.Word40:X8}, {bus.ByteCC:X2}, {changedCalled}, {bit3Called})");
        }
        // 0xA4454C and 0x9C62AC are not adopted: reaching them unset is a visible stop.
        var b = new WwiseRoutingNode { Id = 1, IsBus = true };
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseBusWalk.ApplyChannelConfig(b, 0x4101, new WwiseBusReaderSeams()));
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseBusWalk.ApplyFlagByteC(b, 0, 8, new WwiseBusReaderSeams()));
    }

    [Fact]
    public void B6_0x9F5760_EveryOracleScenarioFillsTheFxChunkAsTheEngineDoes()
    {
        // C34.1 B6, emu_bus.py case_fx9f5760: a slot above 3 returns 0x1F; the 0x28-byte chunk is allocated on first use (a failure returns 0x34); a chunk version above the argument returns 1 untouched; otherwise the version is
        // stored and a changed id or share (id at +4+8*slot, share byte at +9+8*slot) stores both and runs bus vt+0xC4 then vt+0x8C(slot).
        Assert.NotEmpty(BusWalkOracle.Fx);
        foreach (var (name, (slot, id, share, version, chunk, fail, ret, after, log)) in BusWalkOracle.Fx)
        {
            var bus = new WwiseRoutingNode { Id = 1, IsBus = true };
            if (chunk is { } c)
            {
                var existing = new WwiseFxChunk { Version0 = c.Version };
                for (int i = 0; i < 4; i++) { existing.Ids[i] = c.Ids[i]; existing.Share[i] = c.Share[i]; }
                bus.Fx28 = existing;
            }
            var calls = new List<string>();
            var seams = new WwiseBusReaderSeams
            {
                FxSlotChangedVtC4 = _ => calls.Add("vtC4"), FxSlotChangedVt8C = (_, sl) => calls.Add("vt8C:" + sl), AllocationFails = () => fail,
            };
            uint r = (uint)WwiseBusWalk.RegisterFx9F5760(bus, slot, id, share, version, seams);
            Assert.True(ret == r, $"{name}: engine {ret}, C# {r}");
            Assert.True(log.SequenceEqual(calls), $"{name}: callbacks");
            if (after is null) Assert.Null(bus.Fx28);
            else
            {
                Assert.NotNull(bus.Fx28);
                Assert.True((after.Value.Version, string.Join(",", after.Value.Ids), string.Join(",", after.Value.Share)) == (bus.Fx28!.Version0, string.Join(",", bus.Fx28.Ids), string.Join(",", bus.Fx28.Share)), $"{name}: the chunk");
            }
        }
    }

    [Fact]
    public void B6_0x9C0D08_EveryOracleScenarioReadsTheFxListAndTheMixerRecord()
    {
        // C34.1 B6, emu_bus.py case_fxlist: u8 count, a bypass byte and 7-byte entries when the count is non-zero (an entry with id 0 is skipped, a rejected slot runs 0x9F5C30(bus, bypass, -1) and ends with its
        // code), 0x9F5C30 after the loop, then the mixer {u32 id, u8 flag} through bus vt+0xE0 (a required seam: 0x9C0FC0 is open in C34.1); every exit has [+0x40] |= 0x1F000.
        Assert.NotEmpty(BusWalkOracle.FxList);
        foreach (var (name, (data, mixerRet, ret, consumed, w40, log, chunk)) in BusWalkOracle.FxList)
        {
            var bus = new WwiseRoutingNode { Id = 1, IsBus = true, Word40 = 0x100 };
            var calls = new List<string>();
            var seams = new WwiseBusReaderSeams
            {
                FxSlotChangedVtC4 = _ => calls.Add("vtC4"), FxSlotChangedVt8C = (_, sl) => calls.Add("vt8C:" + sl),
                FxBypassA9F5C30 = (_, bypass) => calls.Add($"9F5C30:{bypass}/4294967295"),
                MixerVtE0 = (_, id, flag) => { calls.Add($"mixer:{id}/{(flag ? 1 : 0)}/0"); return mixerRet; },
            };
            int pos = 0;
            uint r = (uint)WwiseBusWalk.ReadFxList9C0D08(bus, data, ref pos, seams);
            Assert.True((ret, consumed, w40) == (r, pos, bus.Word40), $"{name}: engine ({ret}, {consumed}, {w40:X}) C# ({r}, {pos}, {bus.Word40:X})");
            Assert.True(log.SequenceEqual(calls), $"{name}: calls engine [{string.Join(",", log)}] C# [{string.Join(",", calls)}]");
            if (chunk is null) Assert.Null(bus.Fx28);
            else
            {
                Assert.NotNull(bus.Fx28);
                Assert.Equal(chunk.Value.Ids, bus.Fx28!.Ids);
                Assert.Equal(chunk.Value.Share, bus.Fx28.Share);
            }
        }
        // The mixer record's body is open: unset it is a visible stop.
        var b = new WwiseRoutingNode { Id = 1, IsBus = true };
        int p = 0;
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseBusWalk.ReadFxList9C0D08(b, new byte[] { 0, 0, 0, 0, 0, 0 }, ref p, new WwiseBusReaderSeams()));
    }

    // ------------------------------------------------------------------ R10

    [Fact]
    public void R10_0x9BE898_WithNoContextObjectAlwaysReturns2AndWritesNoOutPointer()
    {
        // C34.2 R10, emu_bus.py case_be898 (the real function under Unicorn; *out keeps 0xDEADBEEF in every scenario): [ctx+0xDD] bit 2 clears the pan values, [ctx+0xB4] and [ctx+0xDC] & ~3; with bit 2 clear and
        // [ctx+0xDC] & 3 == 1 it runs 0x9FB9B8 (pan properties 0.5 / -0.25 / 0.75 of the root; [ctx+0xDC] bits 2..3 := 1, bits 0..1 := 0); anything else does nothing. The return is always 2.
        Assert.NotEmpty(BusWalkOracle.Be898);
        foreach (var (name, (e8, e9, panIn, c0In, ret, e8Out, panOut, c0Out, outUnchanged)) in BusWalkOracle.Be898)
        {
            Assert.True(outUnchanged, name);
            var props = new Dictionary<byte, uint> { [0xC] = Bits(0.5f), [0xD] = Bits(-0.25f), [0xE] = Bits(0.75f) };
            var rootParams = new WwiseNodeParams(0, 0, 0, props, new Dictionary<byte, (float, float)>(), Array.Empty<WwiseRtpc>(), Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>());
            var leafParams = new WwiseNodeParams(0, 10, 0, new Dictionary<byte, uint>(), new Dictionary<byte, (float, float)>(), Array.Empty<WwiseRtpc>(), Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>());
            var root = new WwiseSoundNode(10, "t.bnk", rootParams, WwiseSourceFactory.VorbisPlugin, 1, 1, 0, 0);
            var leaf = new WwiseSoundNode(11, "t.bnk", leafParams, WwiseSourceFactory.VorbisPlugin, 1, 1, 0, 0);
            var path = new WwisePlayPath(n => n.Id == 11 ? root : null, new WwisePlaySeams());
            var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 5, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(WwiseSourceFactory.VorbisPlugin, 1, 5, 0, 0), new byte[0x44], WwiseGainRtpcKey.Empty, continuous: false)
            {
                NodeE0 = leaf, Flags0E8 = e8, Flags0E9 = e9, PanB4 = BitConverter.UInt32BitsToSingle(panIn[0]), PanB8 = BitConverter.UInt32BitsToSingle(panIn[1]), PanBC = BitConverter.UInt32BitsToSingle(panIn[2]), PanC0 = c0In,
            };
            int r = path.A9BE898(pbi);
            Assert.True((ret, e8Out, panOut[0], panOut[1], panOut[2], c0Out) == ((uint)r, pbi.Flags0E8, Bits(pbi.PanB4), Bits(pbi.PanB8), Bits(pbi.PanBC), pbi.PanC0),
                $"{name}: engine ({ret}, {e8Out:X2}, {panOut[0]:X8}, {panOut[1]:X8}, {panOut[2]:X8}, {c0Out}) C# ({r}, {pbi.Flags0E8:X2}, {Bits(pbi.PanB4):X8}, {Bits(pbi.PanB8):X8}, {Bits(pbi.PanBC):X8}, {pbi.PanC0})");
        }
        // A context object ([ctx+0xD0] != 0) takes 0x9BE8D0, which is unread.
        var p2 = new WwisePlayPath(_ => null, new WwisePlaySeams());
        var withObject = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 5, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(0, 1, 5, 0, 0), new byte[0x44], null, continuous: false) { CtxD0 = new object() };
        Assert.Throws<WwiseMissingBehaviourException>(() => p2.A9BE898(withObject));
    }

    // ------------------------------------------------------------------ B5

    [Fact]
    public void B5_TheCategoriesAreTheVt44ResultsOfTheShippedVtables()
    {
        // C34.1 B5: read from the vtable slots [vptr + 0x44] of the .so (vptrs 0x103ACE0, 0x103D100, 0x1039EA8, 0x103A018, 0x103A198, 0x103A470, 0x103B050, 0x103B860, 0x103BAD0, 0x103BDC8, 0x103CF88; each body is mov r0,#n; bx lr).
        var actual = new Dictionary<string, int>
        {
            ["Bus"] = WwiseNodeCategory44.Bus, ["AudioDeviceBus"] = WwiseNodeCategory44.AudioDeviceBus, ["Class1039EA8"] = WwiseNodeCategory44.Class1039EA8, ["Class103A018"] = WwiseNodeCategory44.Class103A018,
            ["Class103A198"] = WwiseNodeCategory44.Class103A198, ["Class103A470"] = WwiseNodeCategory44.Class103A470, ["Layer"] = WwiseNodeCategory44.Layer, ["RandomSequence"] = WwiseNodeCategory44.RandomSequence,
            ["Sound"] = WwiseNodeCategory44.Sound, ["Switch"] = WwiseNodeCategory44.Switch, ["ActorMixer"] = WwiseNodeCategory44.ActorMixer,
        };
        Assert.Equal(BusWalkOracle.Vt44.OrderBy(k => k.Key).ToArray(), actual.OrderBy(k => k.Key).ToArray());
    }
}
