// fidelity: M6-001, M6-009, M6-025
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The shipped chain and the row-level branches of the HIRC load, with expected values from the inventory's Correction C44.1 and the verification section of
/// research/20261005-B-M6b-4-hirc-graph-15.md (not from the C# and not from the oracle): the field table of section 2, the abort codes, the seams that must stop, the reference counts.
/// </summary>
public class WwiseRuntimeGraphChainTests
{
    private const uint Sound957475640 = 957475640, ActorMixer13023553 = 13023553, Root62050212 = 62050212, CozmoRobot = 1723505802, MasterBus = 3803692087, SecondaryBus = 805203703;

    private static GraphRig.Rig LoadShipped(string meta, params string[] banks)
    {
        var rig = GraphRig.NewRig(0x00, 48000, 0);
        foreach (var b in banks) Assert.Equal(1, rig.Graph.LoadBank(b, GraphRig.ReadBank(meta, b)));
        return rig;
    }

    [Fact]
    public void M6_001_TheShippedChainHasTheFieldsOfTheVerificationTable_SoundActorMixerRootEventAction()
    {
        // Verification section 2 table (re-derived by the verifier): S = Sound 957475640, A = ActorMixer 13023553, R = root 62050212.
        // [+0xC] S 1, A 1+9 = 10, R 1+298 = 299; [+0x34] S->A, A->R, R->0; [+0x38] R = Cozmo_Robot; [+0x40] R = 0xFFE | 0x1C000000; [+0x44] low 10 bits 0, 0, 1; [+0x45] 0x00, 0x00, 0x40; [+0x46] 0x21; [+0x47] 0;
        // [+0x58] low 6 bits 0x09; [+0x59] 0, 0, 0x10; [+0x3C] R {2; ids 0, 3; -2.0, 15.0}; [+0x28] R FX chunk version 0, slot 0 id 2313011259, share 1, rendered 0, bypass 0; [+0x14] R mask 1;
        // Sound [+0x5C..0x70] 0x1B2E17C5 / 0x1B2E17C5 / 2786 / 0x87 / 0 / 0x00040001; category vt+0x44 3, 1, 1; table A for all.
        var meta = GraphRig.FindMeta();
        if (meta is null) { Assert.Fail("re-analysis/obb/sound_meta (the unpacked shipped banks) was not found above the test binaries"); return; }
        var rig = LoadShipped(meta, "Init.bnk", "English(US)/Cozmo.bnk");
        var reg = rig.Graph.Registry;
        var s = (WwiseRoutingNode)reg.Find(WwiseRegistryTable.A, Sound957475640)!;
        var a = (WwiseRoutingNode)reg.Find(WwiseRegistryTable.A, ActorMixer13023553)!;
        var r = (WwiseRoutingNode)reg.Find(WwiseRegistryTable.A, Root62050212)!;
        Assert.Equal((1, 10, 299), (s.RefCount0C, a.RefCount0C, r.RefCount0C));
        Assert.Equal(new[] { ActorMixer13023553, Root62050212 }, new[] { s.Parent!.Id, a.Parent!.Id });
        Assert.Null(r.Parent);
        Assert.Equal(9, a.Children5C.Count);
        Assert.Contains(s, a.Children5C);
        Assert.Equal(298, r.Children5C.Count);
        Assert.Contains(a, r.Children5C);
        Assert.Null(s.OutputBus); Assert.Null(a.OutputBus);
        Assert.Equal(CozmoRobot, r.OutputBus!.Id);
        Assert.Equal(new uint[] { 0, 0, 0x1C000FFE }, new[] { s.Word40, a.Word40, r.Word40 });
        Assert.Equal(new uint[] { 0, 0, 1 }, new[] { (uint)(s.Word44 & 0x3FF), (uint)(a.Word44 & 0x3FF), (uint)(r.Word44 & 0x3FF) });
        Assert.Equal(new byte[] { 0x00, 0x00, 0x40 }, new[] { s.Byte45, a.Byte45, r.Byte45 });
        Assert.Equal(new byte[] { 0x21, 0x21, 0x21 }, new[] { s.Byte46, a.Byte46, r.Byte46 });
        Assert.Equal(new byte[] { 0, 0, 0 }, new[] { s.Byte47, a.Byte47, r.Byte47 });
        Assert.Equal(new byte[] { 0x09, 0x09, 0x09 }, new[] { (byte)(s.Byte58 & 0x3F), (byte)(a.Byte58 & 0x3F), (byte)(r.Byte58 & 0x3F) });
        Assert.Equal(new byte[] { 0x00, 0x00, 0x10 }, new[] { s.Byte59, a.Byte59, r.Byte59 });
        Assert.Equal(new[] { WwiseNodeCategory44.Sound, WwiseNodeCategory44.ActorMixer, WwiseNodeCategory44.ActorMixer }, new[] { s.Category44, a.Category44, r.Category44 });
        Assert.Equal(WwiseRegistryTable.A, s.Table); Assert.Equal(WwiseRegistryTable.A, a.Table); Assert.Equal(WwiseRegistryTable.A, r.Table);
        Assert.Null(s.BaseBundle3C); Assert.Null(a.BaseBundle3C);
        Assert.Equal(new byte[] { 0, 3 }, r.BaseBundle3C!.Ids);
        Assert.Equal(new[] { BitConverter.SingleToUInt32Bits(-2.0f), BitConverter.SingleToUInt32Bits(15.0f) }, r.BaseBundle3C.FirstWords);
        Assert.Null(r.RangedBundle4C); Assert.Null(r.AuxIds54);
        Assert.Null(s.Fx28); Assert.Null(a.Fx28);
        Assert.Equal((0, 2313011259u, (byte)1, (byte)0, (byte)0), (r.Fx28!.Version0, r.Fx28.Ids[0], r.Fx28.Share[0], r.Fx28.Rendered[0], r.Fx28.Bypass));
        Assert.Equal(new uint[] { 0, 0, 0 }, new[] { r.Fx28.Ids[1], r.Fx28.Ids[2], r.Fx28.Ids[3] });
        Assert.Equal(1UL, r.Registry14!.MaskA);
        Assert.Equal(0, r.Registry14.Byte1C);
        Assert.Null(s.Registry14); Assert.Null(a.Registry14);
        Assert.Equal((0x1B2E17C5u, 0x1B2E17C5u, 2786u, 0x87u, 0u, 0x00040001u), (s.SourceId5C, s.SourceId60, s.InMemorySize64, s.Word68, s.Field6C, s.Plugin70));
        // D12, D13: the Event's one action is the Play action 859129412 (fade curve 4, bank 0x8E39A50B); the action's count is 1 + the event's reference
        var ev = (WwiseRuntimeEvent)reg.Find(WwiseRegistryTable.Event, 188399711)!;
        var act = (WwiseRuntimeAction)reg.Find(WwiseRegistryTable.Action, 859129412)!;
        Assert.Same(act, ev.FirstAction10);
        Assert.Null(act.Next10);
        Assert.Equal((0x403, 4, 0x20, 0x8E39A50Bu, 2, 1), (act.Type20, act.Byte22 & 0x1F, act.Byte22 & 0x20, act.BankId24, act.RefCount0C, ev.RefCount0C));
        // the production walks over the graph: the first output bus of the Sound is the root's bus (0x9F4BB8: the nearest ancestor-or-self with [+0x38]); [pbi+0xE9] bit 2 returns 0 (0x9BDA6C)
        Assert.Equal(CozmoRobot, WwiseBusWalk.A9F4BB8(s)!.Id);
        Assert.Equal(CozmoRobot, WwiseBusWalk.FirstOutputBus9BDA6C(0, s)!.Id);
        Assert.Null(WwiseBusWalk.FirstOutputBus9BDA6C(4, s));
        // the linker's vt+0x88 (0x9F1E3C) over the graph: Sound -> ActorMixer -> root -> Cozmo_Robot, which 0x9C54E8 holds for ([+0x68] byte 1)
        Assert.Equal(CozmoRobot, s.Vt88()!.Id);
        var cozmoRobot = r.OutputBus!;
        Assert.True(cozmoRobot.A9C54E8());
        var priority = (WwiseRoutingNode)reg.Find(WwiseRegistryTable.B, 2459405053)!;
        Assert.False(priority.A9C54E8());                       // no FX id, [+0x68] byte 0, [+0x46] bit 7 clear, an output bus, [+0x40] & 0xE0000 == 0, [+0x54] == 0 (0x9C5524..0x9C5584)
        Assert.True(((WwiseRoutingNode)reg.Find(WwiseRegistryTable.B, MasterBus)!).A9C54E8());   // [bus+0x38] == 0
    }

    [Fact]
    public void M6_001_TheFifteenShippedBusesHaveTheFieldsOfTheVerificationTable()
    {
        // Verification section 2 (buses): [+0x38] Master Audio Bus for Cozmo_Robot and Robot_Bus_x, none for the two parentless; [+0x34] 0; [+0x40] 0x1C01F000 / 0x0001F000; [+0x46] 0x25; [+0x47] 0;
        // [+0x44] low 10 bits 0; [+0x68] 0x4101 (Cozmo_Robot and Robot_Bus_x) else 0; [+0xCC] 0x78 (Secondary 0x38); [+0x3C] {27: 0.0, 28: 100.0, 29: 0.0, 32: 100.0}; Robot_Bus_x FX chunk slots 0..3 ids
        // {0x6767FC1F, 0x174901C6, 0xDF2230FF, custom 0x4FC11BD / 0x18955E1F / 0x397369E7 / 0xFA2C884}, share {1,1,1,0}, bypass 0; [+0x6C] = -96.0 (0xC2C00000); [+0x14] mask 0x20 (robot_volume 0x637C1240, param 5) for
        // Cozmo_Robot; [+0xC]: Cozmo_Robot 5 (6 with Dev_Debug's ActorMixer 121198006), Master 10, Robot_Bus 1.
        var meta = GraphRig.FindMeta();
        if (meta is null) { Assert.Fail("re-analysis/obb/sound_meta (the unpacked shipped banks) was not found above the test binaries"); return; }
        var rig = LoadShipped(meta, "Init.bnk", "English(US)/Cozmo.bnk");
        var reg = rig.Graph.Registry;
        Assert.Equal(15, reg.Count(WwiseRegistryTable.B));
        var buses = reg.Objects(WwiseRegistryTable.B).Cast<WwiseRoutingNode>().ToDictionary(b => b.Id);
        var robotBuses = new uint[] { 2678428985, 2678428988, 2678428990, 2678428991 };
        foreach (var b in buses.Values)
        {
            Assert.True(b.IsBus);
            Assert.Null(b.Parent);                                                    // 0x9C0A6C is bx lr: a bus's [+0x34] is never written
            Assert.Equal(WwiseNodeCategory44.Bus, b.Category44);
            bool parentless = b.Id is MasterBus or SecondaryBus;
            Assert.Equal(parentless ? 0x0001F000u : 0x1C01F000u, b.Word40);
            Assert.Equal(parentless, b.OutputBus is null);
            Assert.Equal((byte)0x25, b.Byte46);
            Assert.Equal((byte)0, b.Byte47);
            Assert.Equal((ushort)0, (ushort)(b.Word44 & 0x3FF));
            Assert.Equal(b.Id == CozmoRobot || robotBuses.Contains(b.Id) ? 0x4101u : 0u, b.Word68);
            Assert.Equal(b.Id == SecondaryBus ? (byte)0x38 : (byte)0x78, b.ByteCC);
            Assert.Equal(0xC2C00000u, BitConverter.SingleToUInt32Bits(b.MaxDuck6C));
            Assert.Equal(new byte[] { 27, 28, 29, 32 }, b.BaseBundle3C!.Ids);
            Assert.Equal(new[] { 0u, 0x42C80000u, 0u, 0x42C80000u }, b.BaseBundle3C.FirstWords);
            Assert.Equal(48000u, b.RecoverySamples64);                                // recovery 1000 ms at the host's 48000 Hz
        }
        Assert.Equal(MasterBus, buses[CozmoRobot].OutputBus!.Id);
        foreach (var id in robotBuses) Assert.Equal(MasterBus, buses[id].OutputBus!.Id);
        Assert.Equal(CozmoRobot, buses[2476517424].OutputBus!.Id);
        Assert.Same(buses[MasterBus], rig.Graph.MasterBuses.Master);
        Assert.Same(buses[SecondaryBus], rig.Graph.MasterBuses.Secondary);
        Assert.Equal(0xFFFFFFFFu, rig.Graph.MasterBuses.MasterField8);
        Assert.Equal(0xFFFFFFFFu, rig.Graph.MasterBuses.SecondaryField14);
        Assert.Equal(5, buses[CozmoRobot].RefCount0C);
        Assert.Equal(10, buses[MasterBus].RefCount0C);
        foreach (var id in robotBuses) Assert.Equal(1, buses[id].RefCount0C);
        Assert.Equal(0x20UL, buses[CozmoRobot].Registry14!.MaskA);
        var customs = new List<uint>();
        foreach (var id in robotBuses)
        {
            var fx = buses[id].Fx28!;
            Assert.Equal(new uint[] { 0x6767FC1F, 0x174901C6, 0xDF2230FF }, fx.Ids.Take(3).ToArray());
            customs.Add(fx.Ids[3]);
            Assert.Equal(new byte[] { 1, 1, 1, 0 }, fx.Share);
            Assert.Equal(new byte[] { 0, 0, 0, 0 }, fx.Rendered);
            Assert.Equal((byte)0, fx.Bypass);
        }
        Assert.Equal(new uint[] { 0x4FC11BD, 0x18955E1F, 0x397369E7, 0xFA2C884 }.OrderBy(x => x), customs.OrderBy(x => x));
        // Cozmo_Robot_External carries the Compressor 2313011259 (plug-in 0x6C0003) in slot 0 and has Cozmo_Robot as its parent bus
        Assert.Equal(2313011259u, buses[2476517424].Fx28!.Ids[0]);
        // Dev_Debug adds one more ActorMixer under Cozmo_Robot
        Assert.Equal(1, rig.Graph.LoadBank("Dev_Debug.bnk", GraphRig.ReadBank(meta, "Dev_Debug.bnk")));
        Assert.Equal(6, buses[CozmoRobot].RefCount0C);
    }

    [Fact]
    public void M6_001_TheParsedHierarchyKeepsWhatTheEngineKeeps_FxEntriesBusFieldsDucksAndOrderedBundles()
    {
        // C44.1 section 3: the parse used to discard the node's overrideFX byte, FX entries (slot, id, share, rendered, bypass), attach byte, aux byte and ids; and the bus's A byte, channel config, maxDuck, duck curve and
        // target-property bytes, mixer id and flag, bypass byte and attach byte.
        var meta = GraphRig.FindMeta();
        if (meta is null) { Assert.Fail("re-analysis/obb/sound_meta (the unpacked shipped banks) was not found above the test binaries"); return; }
        var cozmo = WwiseBank.Parse(GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), "Cozmo.bnk");
        var root = WwiseHierarchy.TryRead(cozmo.Objects[Root62050212], out var problem);
        Assert.NotNull(root);
        Assert.Null(problem);
        var p = root!.Params;
        Assert.Equal(0, p.OverrideFxByte);
        Assert.Equal(new[] { new WwiseFxEntry(0, 2313011259u, 1, 0) }, p.FxEntries);
        Assert.Equal(0, p.FxBypass);
        Assert.Equal(0, p.OverrideAttach);
        Assert.Equal(0x02, p.AuxBits);
        Assert.Empty(p.AuxIds);
        Assert.Equal(new (byte, uint)[] { (0, BitConverter.SingleToUInt32Bits(-2.0f)), (3, BitConverter.SingleToUInt32Bits(15.0f)) }, p.PropEntries);
        var init = WwiseBank.Parse(GraphRig.ReadBank(meta, "Init.bnk"), "Init.bnk");
        var robot = (WwiseBusNode)WwiseHierarchy.TryRead(init.Objects[CozmoRobot], out _)!;
        Assert.Equal((byte)0, robot.ByteA);
        Assert.Equal(0x4101u, robot.ChannelConfig);
        Assert.Equal(-96.0f, robot.MaxDuck);
        Assert.Equal((byte)0, robot.FxBypass);
        Assert.Equal((0u, (byte)0, (byte)0), (robot.MixerId, robot.MixerFlag, robot.AttachByte));
        var bus1 = (WwiseBusNode)WwiseHierarchy.TryRead(init.Objects[2678428985], out _)!;
        Assert.Equal(4, bus1.Effects.Count);
        Assert.Equal(new[] { (byte)0, (byte)1, (byte)2, (byte)3 }, bus1.Effects.Select(e => e.Index));
        Assert.Equal((byte)0, bus1.FxBypass);
        // Priority_SFX 2459405053 ducks Music 3991942870 (the pass-15 text: -18 dB); the duck entry keeps its curve byte and target-property byte (the oracle row of the engine's list: curve 5, property 5)
        var priority = (WwiseBusNode)WwiseHierarchy.TryRead(init.Objects[2459405053], out _)!;
        var duck = Assert.Single(priority.DuckEntries);
        Assert.Equal((3991942870u, -18.0f, (byte)5, (byte)5), (duck.TargetBusId, duck.Volume, duck.Curve, duck.TargetProperty));
        Assert.Equal(priority.Ducks[0], (duck.TargetBusId, duck.Volume, duck.FadeOutMs, duck.FadeInMs));
    }

    [Fact]
    public void M6_001_A1_TheEngineDoesNotCheckThatABodyWasConsumed_TheParseKeepsItsStricterCheckByDefault()
    {
        // C44.1 A1 (V-section HOLDS): the walker advances by the object's size; no handler tests the end position. TryRead's whole-body check is stricter than the engine's, kept by default (it caught every wrong layout
        // while the readers were recovered) and switched off for the graph loader. A bus's B byte: bits 0..2 are plain stores (0x9F627C, 0x9F68D8, [+0x47] bit 6); bit 3 needs 0x9C62AC (not read) and is refused.
        var am = Hb.ActorMixer(20, 10, 11);
        var padded = new WwiseObject { Id = 20, Type = WwiseObjectType.ActorMixer, Payload = Hb.Concat(am.Body, new byte[] { 0xAA, 0xBB }), Bank = "t" };
        Assert.Null(WwiseHierarchy.TryRead(padded, out var problem));
        Assert.Contains("2 bytes left", problem);
        var n = Assert.IsType<WwiseActorMixerNode>(WwiseHierarchy.TryRead(padded, out _, requireWholeBody: false));
        Assert.Equal(new uint[] { 10, 11 }, n.Children);
        var (g, _) = Fresh();
        Assert.Equal(1, g.LoadBank("t", Hb.Bank(1, Hb.Sound(10), Hb.Sound(11), (7, padded.Payload.ToArray()))));
        foreach (byte b in new byte[] { 1, 2, 4, 7 })
        {
            var ok = new WwiseObject { Id = 100, Type = WwiseObjectType.AudioBus, Payload = Hb.Bus(100, b: b).Body, Bank = "t" };
            Assert.Equal(b, Assert.IsType<WwiseBusNode>(WwiseHierarchy.TryRead(ok, out _)).ByteB);
        }
        var refused = new WwiseObject { Id = 100, Type = WwiseObjectType.AudioBus, Payload = Hb.Bus(100, b: 8).Body, Bank = "t" };
        Assert.Null(WwiseHierarchy.TryRead(refused, out var why));
        Assert.Contains("0x9C62AC", why);
    }

    // ------------------------------------------------------------------ rows of C44.1 that need no engine run

    [Fact]
    public void M6_025_B2_TheBaseConstructorLeaves0x4000_0x21_AndOnlyBit7OfByte47FromThePool()
    {
        // C44.1 B2 with V1: [+0x44..45] = 0x4000, [+0x45] = 0x40, [+0x46] = 0x21, [+0x47] = old & 0x80; the words +0x24..+0x40 are 0.
        foreach (byte pool in new byte[] { 0x00, 0x7F, 0x80, 0xFF })
        {
            var n = new WwiseRoutingNode { Id = 5, Word40 = 0xFFFFFFFF, Node30 = new object(), Parent = new WwiseRoutingNode { Id = 6 } };
            WwiseBusWalk.BaseCtor9F402C(n, pool);
            Assert.Equal(0x4000u, n.Dword44 & 0xFFFF);
            Assert.Equal((byte)0x40, n.Byte45);
            Assert.Equal((byte)0x21, n.Byte46);
            Assert.Equal((byte)(pool & 0x80), n.Byte47);
            Assert.Equal(0u, n.Word40);
            Assert.Null(n.Parent); Assert.Null(n.OutputBus); Assert.Null(n.Fx28); Assert.Null(n.Node30); Assert.Null(n.BaseBundle3C);
        }
    }

    [Fact]
    public void M6_025_D6_TheRealBusConstructorSetsCC30_BusRegistrationAndTheCategoryBit()
    {
        // C44.1 D6: [+0xCC] ends at exactly 0x30, vt+0x44 = 0 gives [+0x46] bit 2 = 1 so the bus is in table B (0x9F40F4), [+0x68] byte 0, [+0x6C] = 0xC2C0999A until the bus init overwrites it, count 1.
        var reg = new WwiseRuntimeRegistry();
        var bus = WwiseBusWalk.ConstructBus9C3620(77, reg, new WwiseGraphHostInputs(0xFF, 48000, 0, false, true, true), 0x1000);
        Assert.Equal(0x30, bus.ByteCC);
        Assert.Equal((byte)0x25 | 0, bus.Byte46);
        Assert.Equal(0x80, bus.Byte47);
        Assert.Equal(0u, bus.Word68);
        Assert.Equal(0xC2C0999Au, BitConverter.SingleToUInt32Bits(bus.MaxDuck6C));
        Assert.Equal(1, bus.RefCount0C);
        Assert.Same(bus, reg.Find(WwiseRegistryTable.B, 77));
        Assert.Null(reg.Find(WwiseRegistryTable.A, 77));
        Assert.True(bus.IsBus);
        Assert.Equal(0x1000u, bus.SubscriptionKey10);
    }

    [Fact]
    public void M6_001_B4_B5_TheRegistryInsertsAtTheHeadAndTheLookupIncrementsTheCountForBothFlags()
    {
        // C44.1 B4 (head insertion, 0x9F41B8..0x9F41C8), B5 (0x9A7EB0: both flags increment [obj+0xC] on a hit, 0x9A7F14..0x9A7F1C and 0x9A7F7C..0x9A7F88; flag 0 is table A, any other flag table B).
        var reg = new WwiseRuntimeRegistry();
        var first = new WwiseRoutingNode { Id = 9 };
        var second = new WwiseRoutingNode { Id = 9 };
        reg.Register9F40F4(first);
        reg.Register9F40F4(second);
        Assert.Same(second, reg.Find(WwiseRegistryTable.A, 9));
        Assert.Equal(2, reg.Count(WwiseRegistryTable.A));
        var bus = new WwiseRoutingNode { Id = 9, IsBus = true, Byte46 = 4 };
        reg.Register9F40F4(bus);
        Assert.Same(bus, reg.Lookup9A7EB0(9, 1));
        Assert.Same(bus, reg.Lookup9A7EB0(9, 7));
        Assert.Equal(3, bus.RefCount0C);
        Assert.Same(second, reg.Lookup9A7EB0(9, 0));
        Assert.Equal(2, second.RefCount0C);
        Assert.Null(reg.Lookup9A7EB0(10, 0));
        Assert.True(reg.Remove(second));
        Assert.Same(first, reg.Find(WwiseRegistryTable.A, 9));
        Assert.False(reg.Remove(second));
    }

    [Fact]
    public void M6_025_B6_OneReferenceCountIsSharedByTheLoaderThePbiTableAndTheBusAddRef()
    {
        // C44.1 B1/B6: [obj+0xC] is ONE field; WwisePlaybackBridge.NodeRefs (the PBI's AddRef 0x9F1CBC) and the voice linker's BusAddRefVt8 (0xA4F1B4..0xA4F1C0) used to keep two separate counts.
        var rig = GraphRig.NewRig(0, 48000, 0);
        var bytes = Hb.Bank(1, Hb.Bus(100), Hb.Sound(10), Hb.ActorMixer(20, 10));
        Assert.Equal(1, rig.Graph.LoadBank("t", bytes));
        var sound = (WwiseRoutingNode)rig.Graph.Registry.Find(WwiseRegistryTable.A, 10)!;
        var am = (WwiseRoutingNode)rig.Graph.Registry.Find(WwiseRegistryTable.A, 20)!;
        var bus = (WwiseRoutingNode)rig.Graph.Registry.Find(WwiseRegistryTable.B, 100)!;
        Assert.Equal((1, 2, 1), (sound.RefCount0C, am.RefCount0C, bus.RefCount0C));
        var refs = new WwiseNodeRefTable();
        var linker = new WwiseVoiceLinkSeams();
        rig.Graph.BindReferenceCounts(refs, linker);
        // the PBI AddRef of a parsed node (WwiseNode by id) lands on the registry's count; the linker's bus AddRef lands on the same field as the loader's links
        refs.AddRef9F1CBC(new WwiseActorMixerNode(20, "t", new WwiseNodeParams(0, 0, 0, new Dictionary<byte, uint>(), new Dictionary<byte, (float, float)>(), Array.Empty<WwiseRtpc>(), Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>()), Array.Empty<uint>()));
        Assert.Equal(3, am.RefCount0C);
        Assert.Equal(1, refs.ReferencesOf(20));
        linker.BusAddRefVt8!(bus);
        Assert.Equal(2, bus.RefCount0C);
    }

    // ------------------------------------------------------------------ the abort rows and the seams (visible stops)

    private static (WwiseRuntimeGraph Graph, GraphRig.Rig Rig) Fresh(Action<WwiseGraphSeams>? tweak = null, bool manager = true)
    {
        var rig = GraphRig.NewRig(0, 48000, 0, manager, tweak);
        return (rig.Graph, rig);
    }

    [Fact]
    public void M6_001_C4_C44_1_AnOverrideBusThatIsNotLoadedAbortsTheLoadWithError2_AndTheSoundIsReleased()
    {
        // C44.1 C4: ABSENT returns error 2 (0x9F7214); A4: the failed Sound is released through vt+0xC (count 1 -> 0: out of table A, the unread tail runs).
        var (g, rig) = Fresh();
        Assert.Equal(2, g.LoadBank("t", Hb.Bank(1, Hb.Sound(10, bus: 999))));
        Assert.Null(g.Registry.Find(WwiseRegistryTable.A, 10));
        var (obj, code) = Assert.Single(rig.Destroyed);
        Assert.Equal((10u, 2), (obj.Id, code));
        // without the seam the stop is visible
        var (g2, _) = Fresh(sm => sm.DestroyTail = null);
        var ex = Assert.Throws<WwiseMissingBehaviourException>(() => g2.LoadBank("t", Hb.Bank(1, Hb.Sound(10, bus: 999))));
        Assert.Contains("0x9F500C", ex.Message);
    }

    [Fact]
    public void M6_001_D4_AnActorMixerChildThatIsNotLoadedReturns0xF_AndZeroReturns0xE()
    {
        // C44.1 D4: AddChildByID: id 0 returns 0xE, an absent id 0xF (0x9816B8); the walker stops with that code (A1).
        var (g, _) = Fresh();
        Assert.Equal(0xF, g.LoadBank("a", Hb.Bank(1, Hb.Sound(10), Hb.ActorMixer(20, 10, 99))));
        var (g2, _) = Fresh();
        Assert.Equal(0xE, g2.LoadBank("a", Hb.Bank(1, Hb.Sound(10), Hb.ActorMixer(20, 10, 0))));
        var (g3, _) = Fresh();
        Assert.Equal(0x15, g3.LoadBank("a", Hb.Bank(1, Hb.Sound(10), Hb.ActorMixer(20, 10), Hb.ActorMixer(21, 10))));   // a child that already has a parent: 0x15 (0xA6689C..0xA668A4)
    }

    [Fact]
    public void M6_009_E2_AnRtpcEntryWithNoManagerFailsTheLoadWithError2_AndAnEmptyCurveWith0x1F()
    {
        // C44.1 E2/V6: 0xA1A338 returns 2 when the global manager pointer is null (0xA1A35C..0xA1A36C, 0xA1A420); L5-06: a curve with no points returns 0x1F (0xA11888..0xA118B0).
        var rtpc = Hb.Rtpc(0xAAAA, 0, 1, 5, 7, 0, (0f, 0f, 4u), (1f, 1f, 4u));
        var (g, _) = Fresh(manager: false);
        Assert.Equal(2, g.LoadBank("t", Hb.Bank(1, Hb.Sound(10, rtpcs: rtpc))));
        var (g2, _) = Fresh();
        Assert.Equal(0x1F, g2.LoadBank("t", Hb.Bank(1, Hb.Sound(10, rtpcs: Hb.Rtpc(0xAAAA, 0, 1, 5, 7, 0)))));
        // a repeated curve id under one (node, parameter) and a parameter beyond the 64-bit mask are not read: visible stops
        var (g3, _) = Fresh();
        Assert.Throws<WwiseMissingBehaviourException>(() => g3.LoadBank("t", Hb.Bank(1, Hb.Sound(10, rtpcs: Hb.Concat(rtpc, Hb.Rtpc(0xBBBB, 0, 1, 5, 7, 0, (0f, 0f, 4u), (1f, 1f, 4u)))))));
        var (g4, _) = Fresh();
        Assert.Throws<WwiseMissingBehaviourException>(() => g4.LoadBank("t", Hb.Bank(1, Hb.Sound(10, rtpcs: Hb.Rtpc(0xAAAA, 0, 1, 64, 7, 0, (0f, 0f, 4u), (1f, 1f, 4u))))));
    }

    [Fact]
    public void M6_009_E2_TheRtpcMaskHasTheParameterBit_AndByte1CIsSetOnlyForAModulatorSource()
    {
        // C44.1 E2: 0xA1A160 sets mask-A bit = paramId; [reg+0x1C] = 1 only when srcType == 2 (0xA1A408..0xA1A418); the subscription (key node+0x10, param, type 2, accumulate) is in the manager's table (L5-04).
        var (g, rig) = Fresh();
        var ptsA = new (float, float, uint)[] { (0f, 0f, 4u), (1f, 1f, 4u) };
        Assert.Equal(1, g.LoadBank("t", Hb.Bank(1, Hb.Sound(10, rtpcs: Hb.Concat(Hb.Rtpc(0xA1, 0, 1, 3, 1, 0, ptsA), Hb.Rtpc(0xA2, 0, 0, 3, 2, 0, ptsA), Hb.Rtpc(0xA3, 0, 1, 40, 3, 0, ptsA))),
                                                      Hb.Sound(11, rtpcs: Hb.Rtpc(0xB1, 2, 1, 0, 4, 0, ptsA)))));
        var s10 = (WwiseRoutingNode)g.Registry.Find(WwiseRegistryTable.A, 10)!;
        var s11 = (WwiseRoutingNode)g.Registry.Find(WwiseRegistryTable.A, 11)!;
        Assert.Equal((1UL << 3) | (1UL << 40), s10.Registry14!.MaskA);
        Assert.Equal(0, s10.Registry14.Byte1C);
        Assert.Equal(1UL, s11.Registry14!.MaskA);
        Assert.Equal(1, s11.Registry14.Byte1C);
        Assert.Equal(ulong.MaxValue, s10.Registry14.Cache);
        Assert.NotEqual(s10.SubscriptionKey10, s11.SubscriptionKey10);
        // the store has the subscription: parameter 3 of node 10 holds two curves in bank order; the A11590 of a key without one is 0
        Assert.Equal(0f, rig.Rtpc!.A11590(s10.SubscriptionKey10, 7, WwiseGainRtpcKey.Empty));
    }

    [Fact]
    public void M6_001_TheUnreadCalleesAreRequiredSeams_EachOneStopsTheLoadVisibly()
    {
        // C44.1 section 5 / the task: the creators and inits of RanSeq, Switch, Layer, FxCustom, the FX init 0x9CE3B8, the State chunk, the RTPC after-add step (L5-08), the non-Play action classes, the bus callbacks
        // 0xA4454C, 0x9C62AC (B bit 3), 0xA40FF8, vt+0xE4 and the unlisted types all throw WwiseMissingBehaviourException when their seam is unset.
        void Stops(Action<WwiseGraphSeams> unset, byte[] bank, string mentions)
        {
            var (g, _) = Fresh(unset);
            var ex = Assert.Throws<WwiseMissingBehaviourException>(() => g.LoadBank("t", bank));
            Assert.Contains(mentions, ex.Message);
        }
        Stops(s => s.UnreadType = null, Hb.Bank(1, (10, new byte[] { 1, 0, 0, 0 })), "handler of HIRC type 10");
        Stops(s => s.CreateRanSeq = null, Hb.Bank(1, (5, new byte[] { 1, 0, 0, 0 })), "RanSeq");
        Stops(s => s.CreateSwitch = null, Hb.Bank(1, (6, new byte[] { 1, 0, 0, 0 })), "Switch");
        Stops(s => s.CreateLayer = null, Hb.Bank(1, (9, new byte[] { 1, 0, 0, 0 })), "Layer");
        Stops(s => s.InitFx = null, Hb.Bank(1, (18, new byte[] { 1, 0, 0, 0 })), "0x9CE3B8");
        Stops(s => s.CreateFxCustom = null, Hb.Bank(1, (19, new byte[] { 1, 0, 0, 0 })), "0x9CF038");
        Stops(s => s.CreateAction = null, Hb.Bank(1, Hb.Action(5, 0x0102)), "0xA60C1C");
        Stops(s => s.ActionClassInit = null, Hb.Bank(1, Hb.Action(5, 0x0102)), "vt+0x28");
        Stops(s => s.StateChunk = null, Hb.Bank(1, Hb.Sound(10, states: true)), "State chunk");
        Stops(s => s.RtpcAfterAdd = null, Hb.Bank(1, Hb.Sound(10, rtpcs: Hb.Rtpc(1, 0, 1, 0, 1, 0, (0f, 0f, 4u), (1f, 1f, 4u)))), "L5-08");
        Stops(s => s.A4454C = null, Hb.Bank(1, Hb.Bus(100)), "0xA4454C");
        Stops(s => s.BusVtE4 = null, Hb.Bank(1, Hb.Bus(100), Hb.Bus(101, 100, fx: true)), "0x9C0A70");
        Stops(s => s.A40FF8 = null, Hb.Bank(1, Hb.Bus(100), Hb.Bus(101, 100, fx: true)), "0xA40FF8");
        Stops(s => s.DestroyTail = null, Hb.Bank(1, Hb.Bus(100), Hb.Bus(101, 999)), "0x9F500C");
        // the bus B byte's bit 3 (0x9C62AC) cannot be parsed: the parse refuses it; the seam is reached only through the byte-level helpers
        Assert.Throws<WwiseMissingBehaviourException>(() => Fresh().Graph.LoadBank("t", Hb.Bank(1, Hb.Bus(100, b: 8))));
        // the host states the global lists empty (0xA40E6C, 0xA4131C return at once, V3); when it does not, the seams are required
        var rig = GraphRig.NewRig(0, 48000, 0, true, null, listsEmpty: false);
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Graph.LoadBank("t", Hb.Bank(1, Hb.Bus(100), Hb.Bus(101, 100, fx: true))));
    }

    [Fact]
    public void M6_025_C1_TheNodeFxReaderRunsRenderedThenRegisterThenBypass_AndAPartialInitRunsOnlyTheRenderedStep()
    {
        // C44.1 C1 / the FX reader 0x9ECAD8: per entry 0x9F5B24(node, slot, rendered != 0), then when not rendered and the id is set 0x9F5760(node, slot, id, share != 0, 0); after the loop 0x9F5C30(node, bypass, -1).
        // partial = 1 (an existing source-plug-in Sound loaded again): only 0x9F5B24 runs.
        var (g, _) = Fresh();
        var first = Hb.Sound(10, plugin: 0x00640002, stream: 0, fx: new[] { (0, 0x5555u, 1, 0), (1, 0x6666u, 0, 1) }, fxBypass: 3);
        Assert.Equal(1, g.LoadBank("a", Hb.Bank(1, first)));
        var n = (WwiseRoutingNode)g.Registry.Find(WwiseRegistryTable.A, 10)!;
        Assert.Equal(new uint[] { 0x5555, 0, 0, 0 }, n.Fx28!.Ids);
        Assert.Equal(new byte[] { 0, 1, 0, 0 }, n.Fx28.Rendered);
        Assert.Equal((byte)3, n.Fx28.Bypass);
        // loaded again with other FX: the Sound's source struct is the source-plug-in form (bit 7 of [+0x68] never set), so the init runs with partial = 1: slot 0 rendered clears its id, the bypass byte stays
        var again = Hb.Sound(10, plugin: 0x00640002, stream: 0, fx: new[] { (0, 0x5555u, 1, 1), (2, 0x7777u, 1, 0) }, fxBypass: 9);
        Assert.Equal(1, g.LoadBank("b", Hb.Bank(2, again)));
        Assert.Equal(2, n.RefCount0C);
        Assert.Equal(new uint[] { 0, 0, 0, 0 }, n.Fx28!.Ids);
        Assert.Equal(new byte[] { 1, 1, 0, 0 }, n.Fx28.Rendered);
        Assert.Equal((byte)3, n.Fx28.Bypass);
    }

    [Fact]
    public void M6_001_A4_ASoundWithTheCodecFormAndBit7IsSkippedWhenLoadedAgain_AndTheCountStillGoesUp()
    {
        // C44.1 A4: present and [node+0x68] & 0x7C != 0 with bit 7 set means skip the init (0x9B3E30..0x9B3E3C, 0x9B3FB0..0x9B3FB8): the second bank's different node block changes nothing.
        var (g, _) = Fresh();
        Assert.Equal(1, g.LoadBank("a", Hb.Bank(1, Hb.Sound(10, flags: 1, pos: 0xC3))));
        var n = (WwiseRoutingNode)g.Registry.Find(WwiseRegistryTable.A, 10)!;
        uint w40 = n.Word40;
        Assert.Equal(1, g.LoadBank("b", Hb.Bank(2, Hb.Sound(10, flags: 0, pos: 0xC0))));
        Assert.Equal(2, n.RefCount0C);
        Assert.Equal(w40, n.Word40);
        Assert.Equal(2, g.Banks.Sum(b => b.Objects.Count(o => ReferenceEquals(o, n))));
        Assert.Equal(1, g.Banks[0].Objects.Count);
        Assert.Equal(1, g.Banks[0].Capacity);
    }

    [Fact]
    public void M6_001_A8_APlayActionWithoutTheInitMarkIsInitialisedAgainWhenFound()
    {
        // C44.1 A8: found: refcount++; a Play action (0x403) with [act+0x22] & 0x20 == 0 is re-initialised (0x9B3088..0x9B30E8).
        var (g, _) = Fresh();
        var stale = new WwiseRuntimeAction { Id = 5, Type20 = 0x403 };
        g.Registry.Insert(stale);
        Assert.Equal(0, stale.Byte22 & 0x20);
        Assert.Equal(1, g.LoadBank("t", Hb.Bank(1, Hb.Action(5, 0x403, fade: 9, bank: 0xABCD))));
        Assert.Equal(2, stale.RefCount0C);
        Assert.Equal((9, 0x20, 0xABCDu), (stale.Byte22 & 0x1F, stale.Byte22 & 0x20, stale.BankId24));
        // with the mark set nothing is initialised again
        var done = new WwiseRuntimeAction { Id = 6, Type20 = 0x403, Byte22 = 0x24, BankId24 = 77 };
        g.Registry.Insert(done);
        Assert.Equal(1, g.LoadBank("u", Hb.Bank(2, Hb.Action(6, 0x403, fade: 9, bank: 0xABCD))));
        Assert.Equal((0x24, 77u, 2), (done.Byte22 & 0xFF, done.BankId24, done.RefCount0C));
    }

    [Fact]
    public void M6_001_D13_TheActionDelayIsConvertedToSamplesAsASigned64BitProduct()
    {
        // C44.1 D13: property 0xF is rewritten to (ms * [0x105243C]) / 1000 as a signed 64-bit divide; the host rate is an explicit input.
        foreach (var (rate, ms, expected) in new (uint, uint, uint)[] { (48000, 200, 9600), (44100, 200, 8820), (48000, 1000000, 48000000), (44100, 0xFFFFFFF0, unchecked((uint)-705)), (22050, 7, 154) })
        {
            var rig = GraphRig.NewRig(0, rate, 0);
            Assert.Equal(1, rig.Graph.LoadBank("t", Hb.Bank(1, Hb.Action(5, 0x403, props: new[] { (0x0Fu, ms) }))));
            var act = (WwiseRuntimeAction)rig.Graph.Registry.Find(WwiseRegistryTable.Action, 5)!;
            Assert.Equal(new byte[] { 0x0F }, act.BaseBundle14!.Ids);
            Assert.Equal(expected, act.BaseBundle14.FirstWords[0]);
        }
        // the ranged delay (0xA615DC..0xA61670, engine-checked in emu_graph.py action_ranged_delay): both u32 words of the first ranged entry with id 0xF, signed 64-bit (word * rate) / 1000
        var r = GraphRig.NewRig(0, 48000, 0);
        Assert.Equal(1, r.Graph.LoadBank("t", Hb.Bank(1, Hb.Action(5, 0x403, ranged: new[] { (0x0Fu, 1f, 2f) }))));
        var ra = (WwiseRuntimeAction)r.Graph.Registry.Find(WwiseRegistryTable.Action, 5)!;
        Assert.Equal(unchecked((uint)(int)(0x3F800000L * 48000 / 1000)), ra.RangedBundle18!.FirstWords[0]);
        Assert.Equal(unchecked((uint)(int)(0x40000000L * 48000 / 1000)), ra.RangedBundle18.SecondWords![0]);
    }

    [Fact]
    public void M6_001_B2_TheHostPoolByteIsAnExplicitInput_AndItShowsInTheUninitialisedBitsOnly()
    {
        // C44.1 B2/V1, B3: [+0x47] bit 7 and [+0x58] bits 6..7 are the pool's; everything else is deterministic.
        foreach (byte pool in new byte[] { 0x00, 0xFF, 0x40 })
        {
            var rig = GraphRig.NewRig(pool, 48000, 0);
            Assert.Equal(1, rig.Graph.LoadBank("t", Hb.Bank(1, Hb.Sound(10))));
            var n = (WwiseRoutingNode)rig.Graph.Registry.Find(WwiseRegistryTable.A, 10)!;
            Assert.Equal((byte)(pool & 0x80), n.Byte47);
            Assert.Equal((byte)(pool & 0xC0), (byte)(n.Byte58 & 0xC0));
            Assert.Equal((byte)0x09, (byte)(n.Byte58 & 0x3F));
            Assert.Equal((byte)0x00, n.Byte59);
        }
    }
}

/// <summary>A small builder of HIRC objects for the targeted tests (the oracle scenarios carry their banks as hex).</summary>
internal static class Hb
{
    public static byte[] U32(uint v) => BitConverter.GetBytes(v);
    public static byte[] U16(ushort v) => BitConverter.GetBytes(v);
    public static byte[] F32(float v) => BitConverter.GetBytes(v);
    public static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

    public static byte[] Rtpc(uint id, byte type, byte acc, uint param, uint curve, byte scaling, params (float X, float Y, uint Interp)[] pts)
    {
        var b = new List<byte>();
        b.AddRange(U32(id)); b.Add(type); b.Add(acc);
        var groups = new List<byte>();
        uint v = param;
        do { groups.Add((byte)(v & 0x7F)); v >>= 7; } while (v != 0);
        groups.Reverse();
        for (int i = 0; i < groups.Count; i++) b.Add((byte)(groups[i] | (i < groups.Count - 1 ? 0x80 : 0)));
        b.AddRange(U32(curve)); b.Add(scaling); b.AddRange(U16((ushort)pts.Length));
        foreach (var (x, y, i) in pts) { b.AddRange(F32(x)); b.AddRange(F32(y)); b.AddRange(U32(i)); }
        return b.ToArray();
    }

    /// <summary>The NodeBaseParams block in the runtime's order (0x9F6EF8): FX block, attach byte, bus, parent, flags, two bundles, positioning, aux, advanced, states, RTPCs.</summary>
    public static byte[] NodeParams(byte fxOverride = 0, (int Slot, uint Id, int Share, int Rendered)[]? fx = null, byte fxBypass = 0, uint bus = 0, uint parent = 0, byte flags = 0, byte pos = 0xC0,
                                    byte aux = 0, byte[]? adv = null, bool states = false, byte[]? rtpcs = null, int rtpcCount = -1)
    {
        var b = new List<byte> { fxOverride, (byte)(fx?.Length ?? 0) };
        if (fx is { Length: > 0 })
        {
            b.Add(fxBypass);
            foreach (var e in fx) { b.Add((byte)e.Slot); b.AddRange(U32(e.Id)); b.Add((byte)e.Share); b.Add((byte)e.Rendered); }
        }
        b.Add(0); b.AddRange(U32(bus)); b.AddRange(U32(parent)); b.Add(flags);
        b.Add(0); b.Add(0);                                       // no properties, no ranged properties
        b.Add(pos); b.Add(aux);
        b.AddRange(adv ?? new byte[] { 0, 1, 0, 0, 0, 0 });
        if (states) { b.AddRange(U32(1)); b.AddRange(U32(0x1000)); b.Add(1); b.AddRange(U16(1)); b.AddRange(U32(1)); b.AddRange(U32(2)); }
        else b.AddRange(U32(0));
        int count = rtpcCount >= 0 ? rtpcCount : (rtpcs is null ? 0 : 1);
        b.AddRange(U16((ushort)count));
        if (rtpcs is not null) b.AddRange(rtpcs);
        return b.ToArray();
    }

    public static (byte Type, byte[] Body) Sound(uint id, uint plugin = 0x00040001, byte stream = 1, byte flags = 0, byte pos = 0xC0, (int, uint, int, int)[]? fx = null, byte fxBypass = 0,
                                                 uint bus = 0, bool states = false, byte[]? rtpcs = null)
    {
        var b = new List<byte>();
        b.AddRange(U32(id)); b.AddRange(U32(plugin)); b.Add(stream); b.AddRange(U32(id + 5)); b.AddRange(U32(0x1234)); b.Add(0);
        if ((plugin & 0xF) is 2 or 5) b.AddRange(U32(0));
        int count = rtpcs is null ? 0 : CountRtpcs(rtpcs);
        b.AddRange(NodeParams(fx: fx, fxBypass: fxBypass, bus: bus, flags: flags, pos: pos, states: states, rtpcs: rtpcs, rtpcCount: count));
        return (2, b.ToArray());
    }

    private static int CountRtpcs(byte[] rtpcs)
    {
        // each entry: u32 id, u8, u8, varint, u32, u8, u16 n, n * 12
        int p = 0, n = 0;
        while (p < rtpcs.Length)
        {
            p += 6;
            while ((rtpcs[p] & 0x80) != 0) p++;
            p++;
            p += 5;
            int pts = BitConverter.ToUInt16(rtpcs, p);
            p += 2 + 12 * pts;
            n++;
        }
        return n;
    }

    public static (byte Type, byte[] Body) ActorMixer(uint id, params uint[] children)
        => (7, Concat(U32(id), NodeParams(), U32((uint)children.Length), children.SelectMany(U32).ToArray()));

    public static (byte Type, byte[] Body) Bus(uint id, uint parent = 0, byte b = 0, bool fx = false)
    {
        var l = new List<byte>();
        l.AddRange(U32(id)); l.AddRange(U32(parent)); l.Add(0);   // no properties
        l.Add(0); l.Add(b); l.AddRange(U16(0)); l.AddRange(U32(0x4101)); l.Add(2); l.AddRange(U32(1000)); l.AddRange(F32(-96f));
        l.AddRange(U32(0));                                       // no ducks
        if (fx) { l.Add(1); l.Add(0); l.Add(0); l.AddRange(U32(0x1111)); l.Add(1); l.Add(0); } else l.Add(0);
        l.AddRange(U32(0)); l.Add(0); l.Add(0);                   // mixer id and flag, attach byte
        l.AddRange(U16(0)); l.AddRange(U32(0));                   // no RTPCs, no states
        return (8, l.ToArray());
    }

    public static (byte Type, byte[] Body) Action(uint id, ushort type, byte fade = 4, uint bank = 0x8E39A50B, (uint Id, uint Value)[]? props = null, (uint Id, float Min, float Max)[]? ranged = null)
    {
        var l = new List<byte>();
        l.AddRange(U32(id)); l.AddRange(U16(type)); l.AddRange(U32(1)); l.Add(0);
        l.Add((byte)(props?.Length ?? 0));
        foreach (var p in props ?? Array.Empty<(uint, uint)>()) l.Add((byte)p.Id);
        foreach (var p in props ?? Array.Empty<(uint, uint)>()) l.AddRange(U32(p.Value));
        l.Add((byte)(ranged?.Length ?? 0));
        foreach (var p in ranged ?? Array.Empty<(uint, float, float)>()) l.Add((byte)p.Id);
        foreach (var p in ranged ?? Array.Empty<(uint, float, float)>()) { l.AddRange(F32(p.Min)); l.AddRange(F32(p.Max)); }
        l.Add(fade); l.AddRange(U32(bank));
        return (3, l.ToArray());
    }

    public static byte[] Bank(uint bankId, params (byte Type, byte[] Body)[] objs)
    {
        var body = new List<byte>();
        body.AddRange(U32((uint)objs.Length));
        foreach (var (t, b) in objs) { body.Add(t); body.AddRange(U32((uint)b.Length)); body.AddRange(b); }
        var bkhd = Concat(U32(0x78), U32(bankId), U32(0), U16(0), U16(0), U32(77));
        return Concat("BKHD"u8.ToArray(), U32((uint)bkhd.Length), bkhd, "HIRC"u8.ToArray(), U32((uint)body.Count), body.ToArray());
    }
}
