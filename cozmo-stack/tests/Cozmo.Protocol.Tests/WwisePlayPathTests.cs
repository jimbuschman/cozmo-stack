using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-025 / M6-026 (C31.3, C32.1): the shipped Play path. Every expected value is the engine's own output from re-analysis/tools/emu/emu_play.py (0x9BEB30, CalcEffectiveParams 0x9FFAD4, 0x9FF368, 0x9BCA68,
/// 0x9FB9B8 / 0x9FAEE8 / 0x9FBE74, 0x9F6B94, 0x9EEDA4, 0x9F1F80, 0xA1E280, 0xA00618, 0xA36268, 0xA358EC, 0xA366AC / 0xA366D0 run under Unicorn on a PBI, nodes and a params block); the Python stand-ins
/// are the bodies the inventory does not adopt (node vt+0xAC, 0xA35D44, expf, and in emu_play.py the bus bodies 0x9C54E8 / 0x9C39DC and 0xA11590, 0x9E8224, 0x9E62AC, which the C# runs for real since batch 5c: their values are
/// checked against the real functions by WwiseBusWalkTests and WwiseModulatorListTests, and the bus values here are chosen so the real C# walk returns the oracle's stand-in value). Floats are compared as bits. Each
/// test names the oracle scenario (W, B, L, P, T) and the addresses it checks.
/// </summary>
public class WwisePlayPathTests
{
    private static readonly WwiseSourceDescriptor Source = new(WwiseSourceFactory.VorbisPlugin, 1, 12345, 0, 0);

    private static int Bits(float v) => BitConverter.SingleToInt32Bits(v);

    private static WwiseNodeParams NodeParams(uint parent = 0, byte bits = 0, Dictionary<byte, uint>? props = null, Dictionary<byte, (float, float)>? ranged = null,
        WwiseRtpc[]? rtpcs = null, byte pos = 0, byte adv0 = 0, byte adv1 = 1, byte adv3 = 0)
        => new(0, parent, bits, props ?? new Dictionary<byte, uint>(), ranged ?? new Dictionary<byte, (float, float)>(), rtpcs ?? Array.Empty<WwiseRtpc>(),
            Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>())
        { AdvancedByte0 = adv0, AdvancedByte1 = adv1, AdvancedByte3 = adv3, PositioningBits = pos };

    private static WwiseRtpc Rtpc(uint param) => new(0, 0, 0, param, 0, 0, Array.Empty<(float, float, uint)>());

    private sealed class Rig
    {
        public readonly Dictionary<uint, WwiseNode> Nodes = new();
        public readonly WwisePlaybackLimiter Limiter;
        public readonly WwisePlayPath Path;
        public readonly WwisePlaySeams Seams = new();
        public readonly List<string> Log = new();
        public Action<WwiseVtAcArgs>? Accumulate;
        public WwiseVtAcArgs? LastAc;
        public bool BusFlag = true;
        public float BusVolume;
        public float Rtpc;
        public bool HasBus = true;
        public readonly WwiseRtpcStore Store = new();
        public readonly WwiseModulatorManager Modulators = new();
        public readonly List<WwiseListRecord> Records34 = new();
        private readonly Dictionary<uint, WwiseRoutingNode> _runtime = new();
        private WwiseRoutingNode? _bus;
        public WwisePlayingInstance Pbi = null!;
        public WwisePlayInitParams Play = new() { PlayingId = 5, TargetNodeId = 1 };

        public Rig()
        {
            Limiter = new WwisePlaybackLimiter(id => Nodes.TryGetValue(id, out var n) ? n : null);
            Seams.NodeVtAC = a => { LastAc = a; Log.Add("AC"); Accumulate?.Invoke(a); };
            Seams.RecordsOf34 = _ => Records34;
            Seams.ModulatorCtxWords = _ => (0x11u, 0x22u, 0x33u);
            Modulators.A9DCE44 = (id, rec, ctx, w10, list) => { Log.Add("9DCE44:" + id); return 1; };      // 0x9DCE44 is not adopted: a stand-in
            Store.CurveA14E28 = (c, x) => { Log.Add("A11590:" + c.ParamId.ToString("X")); return WwiseRtpcCurveA14E28.Evaluate(c, x); };      // the REAL port of 0xA14E28 (C35); the wrapper only records the call. The curves below are one-point curves, for which the engine returns y_0 for every x
            Path = new WwisePlayPath(Limiter.ParentNode, Seams) { RuntimeNodeOf = RuntimeOf, Rtpc = Store, Modulators = Modulators };
        }

        /// <summary>The engine's node object behind a hierarchy node: every node's [+0x38] is the test's bus, a non-collapsed one (byte [+0x68] = 1, 0x9C54E8 returns 1) or a collapsed one whose 0x9C39DC is the Bus Volume (property 5).</summary>
        public WwiseRoutingNode? RuntimeOf(WwiseNode n)
        {
            if (_runtime.TryGetValue(n.Id, out var existing)) return existing;
            var node = new WwiseRoutingNode { Id = n.Id, SubscriptionKey10 = 0x1000 + n.Id * 0x10 };
            if (HasBus)
            {
                if (_bus is null)
                {
                    if (BusFlag) _bus = new WwiseRoutingNode { Id = 900, IsBus = true, Byte68 = 1 };
                    else
                    {
                        var master = new WwiseRoutingNode { Id = 901, IsBus = true, Byte68 = 1 };
                        _bus = new WwiseRoutingNode
                        {
                            Id = 900, IsBus = true, OutputBus = master,
                            BaseBundle3C = new WwiseParamBundle(new byte[] { 5 }, new[] { BitConverter.SingleToUInt32Bits(BusVolume) }),
                        };
                    }
                }
                node.OutputBus = _bus;
            }
            _runtime[n.Id] = node;
            uint serial = 700;
            foreach (var r in n.Params.Rtpcs)                                                                                  // the node's subscriptions: one curve per RTPC: a one-point curve whose y is the scenario value (0xA14E28 returns y_0 for any x, L5-22)
            {
                Store.Apply(new WwiseStmgParam(serial, Rtpc, 0, 0f, 0f, false));
                Store.AddSubscription(new WwiseRtpcSubscription
                {
                    Key1 = node.SubscriptionKey10, Param = r.ParamId, Type = 0, Accumulate = 1,
                    Curves = new[] { new WwiseRtpc(serial++, 0, 1, r.ParamId, 0, 0, new[] { (0f, Rtpc, 4u) }) },
                });
            }
            return node;
        }

        public WwiseSoundNode Add(uint id, WwiseNodeParams p) { var n = new WwiseSoundNode(id, "t.bnk", p, WwiseSourceFactory.VorbisPlugin, 1, 12345, 0, 0); Nodes[id] = n; return n; }

        public WwisePlayingInstance NewPbi(WwiseNode node, byte e8 = 0x5D, byte e9 = 0x01)
        {
            Pbi = new WwisePlayingInstance(Play, node.Id, Source, new byte[0x44], WwiseGainRtpcKey.Empty, continuous: false) { NodeE0 = node, Flags0E8 = e8, Flags0E9 = e9, FieldC4 = 0, Word64 = 0f };   // the oracle starts from zeroed memory
            return Pbi;
        }

        public Action<WwiseVtAcArgs> Fill(float vol, float pitch, float lpf, float hpf) => a => { a.Pbi.Volume3C = vol; a.Pbi.Pitch44 = pitch; a.Pbi.Lpf48 = lpf; a.Pbi.Hpf4C = hpf; };
    }

    // ------------------------------------------------------------------ W: the node walks and the priority

    [Fact]
    public void W2_0x9EEDA4_TheCodeAndIndexComeFromTheFirstAncestorWithBit4Of0x45OrTheRoot()
    {
        // emu_play.py W2: the walk climbs while [node+0x45] bit 4 (advanced byte 0 bit 4) is clear and a parent exists; the code is the fifth advanced byte & 0xF and the index byte 1 & 7 of the node it stops on.
        var rig = new Rig();
        var limiter = rig.Limiter;
        var root = rig.Add(1, NodeParams(adv1: 2, adv3: 9));
        var leaf = rig.Add(2, NodeParams(parent: 1, adv1: 1, adv3: 0));
        Assert.Equal(9, limiter.BehaviourCode9EEDA4(leaf, out int index));            // to the root: code 9, index 2
        Assert.Equal(2, index);
        var leaf3 = rig.Add(3, NodeParams(parent: 1, adv1: 5, adv3: 3));
        Assert.Equal(9, limiter.BehaviourCode9EEDA4(leaf3, out index));
        Assert.Equal(2, index);
        var top = rig.Add(10, NodeParams(adv1: 3, adv3: 0xC));
        var mid = rig.Add(11, NodeParams(parent: 10, adv0: 0x10, adv1: 4, adv3: 7));
        var low = rig.Add(12, NodeParams(parent: 11, adv1: 1, adv3: 2));
        Assert.Equal(7, limiter.BehaviourCode9EEDA4(low, out index));                 // stops at the flagged middle node: code 7, index 4
        Assert.Equal(4, index);
    }

    [Fact]
    public void W3_0x9F1F80_ReturnsGateZeroWithOutZeroForNodesWithoutAPositioningObject()
    {
        // emu_play.py W3: result 0 and *out = 0 whether the leaf carries positioning bit 0 or not ([node+0x2C] is 0 for every parsed node: the reader refuses bits 0 and 3 together).
        var rig = new Rig();
        var root = rig.Add(1, NodeParams());
        var leaf = rig.Add(2, NodeParams(parent: 1, pos: 0xC0));
        var leaf2 = rig.Add(3, NodeParams(parent: 1, pos: 0xC1));
        Assert.Equal((false, 0f), WwisePlayPath.NodeVt84A9F1F80(leaf));
        Assert.Equal((false, 0f), WwisePlayPath.NodeVt84A9F1F80(leaf2));
        Assert.Throws<WwiseMissingBehaviourException>(() => WwisePlayPath.NodeVt84A9F1F80(rig.Add(4, NodeParams(pos: 0x09))));
    }

    [Fact]
    public void W4_0x9F6B94_ThePriorityDefaultsPropertySevenAndEightAndTheParentAnswer()
    {
        // emu_play.py W4 (0x9F6B94, defaults 50.0f / -10.0f from the static constructor 0x4DDFF0 run in the oracle): property 7 is the priority; the offset is 0 unless [node+0x45] bit 7 (NodeBase bit 1) is set, then property 8
        // or -10.0f; a node without override bit 0 takes its parent's answer.
        var rig = new Rig();
        void Check(WwiseNode n, float prio, float offset)
        {
            rig.Limiter.Priority9F6B94(n, out float p, out float o);
            Assert.Equal((Bits(prio), Bits(offset)), (Bits(p), Bits(o)));
        }
        Check(rig.Add(1, NodeParams()), 50f, 0f);
        Check(rig.Add(2, NodeParams(props: new() { [7] = (uint)Bits(12.5f) })), 12.5f, 0f);
        Check(rig.Add(3, NodeParams(bits: 3, props: new() { [7] = (uint)Bits(12.5f), [8] = (uint)Bits(-3f) })), 12.5f, -3f);
        Check(rig.Add(4, NodeParams(bits: 2, props: new() { [8] = (uint)Bits(-3f) })), 50f, -3f);
        Check(rig.Add(5, NodeParams(bits: 2)), 50f, -10f);
        var parent = rig.Add(6, NodeParams(props: new() { [7] = (uint)Bits(77f) }));
        Check(rig.Add(7, NodeParams(parent: 6, props: new() { [7] = (uint)Bits(5f) })), 77f, 0f);              // without override: the parent's
        Check(rig.Add(8, NodeParams(parent: 6, bits: 1, props: new() { [7] = (uint)Bits(5f) })), 5f, 0f);      // with override bit 0: its own
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Limiter.Priority9F6B94(rig.Add(9, NodeParams(rtpcs: new[] { Rtpc(0x11) })), out _, out _));   // 0xA11590 is unread
    }

    // ------------------------------------------------------------------ B: 0x9BEB30 and CalcEffectiveParams

    private static void Build(Rig rig, out WwiseSoundNode leaf, byte pos = 0xC0, Dictionary<byte, uint>? rootProps = null, WwiseRtpc[]? rootRtpc = null, bool leafOwnPositioning = false)
    {
        rig.Add(10, NodeParams(props: rootProps, pos: pos, rtpcs: rootRtpc));
        leaf = rig.Add(11, NodeParams(parent: 10, pos: leafOwnPositioning ? pos : (byte)0));
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(0u)]
    public void B1_TheShippedPathStoresTheEffectiveParametersAndEnd0x74(uint flag)
    {
        // emu_play.py B1 (0x9BEB30 on E8 = 0x5D, E9 = 1, a node chain with an output bus): node vt+0xAC leaves volume -6, pitch 100, lpf 10, hpf 20; +0xA0 = 1.5, +0xA8 = 2.5, ranges volume 0.125, pitch 7, lpf 0.5,
        // hpf 0.25. Result 1. [+0xDC] ends 0x74 (0x5D: bits 2..3 := 1 and bits 0..1 := 0 by 0x9FB9B8's (b = 1, a = 0), cleared again at 0x9BEB7C, then |= 0x20 at 0x9FFC30), +0xE9 0, +0x1BC 1,
        // +0x3C -5.875, +0x40 1.0, +0x44 107, +0x48 12, +0x4C 22.75, +0x98 -6, +0x9C 10.5, +0xA4 20.25, +0xC4 101.0f, +0xE4 the priority 50.0f, 1CC/1C0 50.0f, 1D0 0. The 0x9C54E8 stand-in saw the bus; vt+0xAC saw the mask
        // 0xFFFFFFDF, the ranges at +0x118 and the Play params (E8 bit 6 is set).
        var rig = new Rig();
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf);
        rig.Accumulate = rig.Fill(-6f, 100f, 10f, 20f);
        pbi.FieldA0 = 1.5f; pbi.FieldA8 = 2.5f;
        pbi.Ranges118.Volume = 0.125f; pbi.Ranges118.Pitch = 7f; pbi.Ranges118.LowPass = 0.5f; pbi.Ranges118.HighPass = 0.25f;
        var r = rig.Path.InitContext9BEB30(pbi, 50f, flag, rig.Play, rig.Limiter);
        Assert.Equal(new WwisePlayPath.InitResult(1, false, 0), r);
        Assert.Equal(unchecked((int)0xC0BC0000), Bits(pbi.Volume3C));
        Assert.Equal(0x3F800000, Bits(pbi.MuteFade40));
        Assert.Equal(0x42D60000, Bits(pbi.Pitch44));
        Assert.Equal(0x41400000, Bits(pbi.Lpf48));
        Assert.Equal(0x41B60000, Bits(pbi.Hpf4C));
        Assert.Equal(0, Bits(pbi.ReadWord64()));
        Assert.Equal(unchecked((int)0xC0C00000), Bits(pbi.Field98));
        Assert.Equal(0x41280000, Bits(pbi.Field9C));
        Assert.Equal(0x41A20000, Bits(pbi.FieldA4));
        Assert.Equal(0x42CA0000, Bits(pbi.FieldC4));
        Assert.Equal(0x42480000u, pbi.FieldE4);
        Assert.Equal((0x74, 0, 1), (pbi.Flags0E8 & 0xFF, pbi.Flags0E9 & 0xFF, pbi.Flags1BC & 0xFF));
        Assert.Equal((0x42480000, 0, 0x42480000), (Bits(pbi.Field1CC), Bits(pbi.Field1D0), Bits(pbi.Priority1C0)));
        Assert.Equal((0, 0, 0), (Bits(pbi.PanB4), Bits(pbi.PanB8), Bits(pbi.PanBC)));
        Assert.Equal(new[] { "AC" }, rig.Log);
        Assert.Equal(0xFFFFFFDFu, rig.LastAc!.Mask);
        Assert.Same(pbi.Ranges118, rig.LastAc.Ranges);
        Assert.Same(rig.Play, rig.LastAc.Params);
        Assert.Null(rig.LastAc.Local);
        Assert.Equal((1u, 0u), (rig.LastAc.StackWord0C, rig.LastAc.StackWord10));              // C34.1 B18: the stack words [sp+0xC] = 1 and [sp+0x10] = r6 = 0 (0x9FFD00..0x9FFD08)
        Assert.Same(rig.Play.Block108, rig.LastAc.OutList);                                      // r7 = params+0x108 once E8 bit 6 is set (0x9FFCB4)
    }

    [Theory]
    [InlineData(0u, 1, 3, 0x29, true, 0x74)]
    [InlineData(1u, 1, 1, 0, true, 0x74)]
    [InlineData(0u, 5, 1, 0, false, 0x7C)]
    public void B2_BelowAudibilityFailsAPlayWithFlagZeroUnlessTheActionIsABusPlay(uint flag, int e9, int result, int code, bool below, int e8)
    {
        // emu_play.py B2 (0x9BED50..0x9BED84): volume -100 dB; r7 = (flag < r6) ? below : 0 with r6 = 1; a non-zero r7 with [pbi+0xE9] bit 2 clear returns 3 with *[sp+0x60] = 0x29. flag 1 returns 1 with below stored;
        // with E9 bit 2 (the bus play) the early exit leaves volume 0 and below false.
        var rig = new Rig();
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf, e9: (byte)e9);
        rig.Accumulate = rig.Fill(-100f, 0f, 0f, 0f);
        var r = rig.Path.InitContext9BEB30(pbi, 50f, flag, rig.Play, rig.Limiter);
        Assert.Equal(new WwisePlayPath.InitResult(result, below, code), r);
        Assert.Equal(e8, pbi.Flags0E8);
        Assert.Equal(e9 == 5 ? 5 : 0, pbi.Flags0E9);                                  // oracle: E9 bit 0 is cleared by the recompute, the bus play keeps 5
        Assert.Equal(e9 == 5 ? 0f : -100f, pbi.Volume3C);
        Assert.Equal(1f, pbi.MuteFade40);
        Assert.Equal(0f, pbi.ReadWord64());
    }

    [Fact]
    public void B3_ABusPlayZeroesThePanValuesAndCalcEffectiveParamsEndsAt0x9FFC30()
    {
        // emu_play.py B3 (E9 = 5): 0x9BEBA8 zeroes ctx+0xA8..+0xB0 and the byte at +0xB4, clears E8 bits 0..1; CalcEffectiveParams' reset stage runs, bit 2 ends it (1BC |= 1, E8 |= 0x20 = 0x7C); the volume is the reset 0, +0x40 1.0f,
        // the priority block did not run (1CC/1D0/1C0 0), vt+0xAC was not called; +0xE4 = 33.0f.
        var rig = new Rig();
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf, e9: 5);
        pbi.PanB4 = pbi.PanB8 = pbi.PanBC = 9f; pbi.PanC0 = 9;
        var r = rig.Path.InitContext9BEB30(pbi, 33f, 1, rig.Play, rig.Limiter);
        Assert.Equal(new WwisePlayPath.InitResult(1, false, 0), r);
        Assert.Equal((0, 0, 0, 0), (Bits(pbi.PanB4), Bits(pbi.PanB8), Bits(pbi.PanBC), (int)pbi.PanC0));
        Assert.Equal((0x7C, 5, 1), (pbi.Flags0E8 & 0xFF, pbi.Flags0E9 & 0xFF, pbi.Flags1BC & 0xFF));
        Assert.Equal(0x42040000u, pbi.FieldE4);
        Assert.Equal((0, 0x3F800000, 0), (Bits(pbi.Volume3C), Bits(pbi.MuteFade40), Bits(pbi.Priority1C0)));
        Assert.Empty(rig.Log);
    }

    [Theory]
    [InlineData((byte)0x01, 3.25f, 2.0f)]
    [InlineData((byte)0x00, 0f, 0f)]
    public void B4_WithE8Bit5SetTheRecomputeRunsOnlyWhenE9Bit0IsSet(byte e9, float volume, float mute)
    {
        // emu_play.py B4 (E8 = 0x7D): no CalcEffectiveParams; with E9 bit 0 set 0x9FF368 runs: records (1, 2, 0.5) and (2, 2, 4.0), +0x168 0.5, +0x16C 2.0 give product 2.0; +0x3C = +0x98 (3.0) + +0x118 (0.25) = 3.25; E9 bit 0
        // is cleared. With it clear nothing is recomputed: +0x3C and +0x40 stay 0 and below is true (product 0), which a flag of 1 ignores. E8 ends 0x74 in both.
        var rig = new Rig();
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf, e8: 0x7D, e9: e9);
        pbi.Transitions10C.Add(new WwiseTransitionRecord { Word0 = 1, Flags4 = 2, Value8 = 0.5f });
        pbi.Transitions10C.Add(new WwiseTransitionRecord { Word0 = 2, Flags4 = 2, Value8 = 4f });
        pbi.Field98 = 3f; pbi.Ranges118.Volume = 0.25f; pbi.Fade168 = 0.5f; pbi.Fade16C = 2f;
        pbi.Volume3C = 0f; pbi.MuteFade40 = 0f;                                      // the oracle's zeroed memory
        var r = rig.Path.InitContext9BEB30(pbi, 20f, 1, rig.Play, rig.Limiter);
        Assert.Equal(1, r.Result);
        Assert.Equal(e9 == 1, r.Below == false);
        Assert.Equal((Bits(volume), Bits(mute)), (Bits(pbi.Volume3C), Bits(pbi.MuteFade40)));
        Assert.Equal((0x74, 0, 0), (pbi.Flags0E8 & 0xFF, pbi.Flags0E9 & 0xFF, pbi.Flags1BC & 0xFF));
        Assert.Equal(3f, pbi.Field98);
        Assert.Empty(rig.Log);                                                       // no 0x9C54E8, no vt+0xAC
        Assert.Equal(2, pbi.Transitions10C.Count);
    }

    [Theory]
    [InlineData(false, -3.5f, 0)]
    [InlineData(true, -3.5f, 1)]
    public void B5_TheBusVolumeIsAddedToPbi64OnlyWhen0x9C54E8ReturnsZero(bool busFlag, float busVolume, int unused)
    {
        // emu_play.py B5 (0x9FFC08..0x9FFEBC, 0x9FFDFC): 0x9C54E8(bus) == 0 calls 0x9C39DC(bus, 0, 5) and adds the result to pbi+0x64 (-3.5f = 0xC0600000); a non-zero result adds 0.0f. r6 is 0 afterwards, so vt+0xAC gets bus 0.
        // Since batch 5c both bodies are the C# ports (WwiseBusWalk), run for real: the collapsed bus carries Bus Volume -3.5f in its base bundle (emu_bus.py p5_base returns 0xC0600000 for it); the other bus has byte [+0x68] = 1.
        var rig = new Rig { BusFlag = busFlag, BusVolume = busVolume };
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf);
        rig.Path.InitContext9BEB30(pbi, 20f, 1, rig.Play, rig.Limiter);
        Assert.Equal(busFlag ? 0 : unchecked((int)0xC0600000), Bits(pbi.ReadWord64()));
        Assert.Equal(new[] { "AC" }, rig.Log);
        Assert.Equal(0x42CA0000, Bits(pbi.FieldC4));
    }

    [Theory]
    [InlineData((byte)0xC0, 0, 0)]
    [InlineData((byte)0xC1, 0, 0)]
    [InlineData((byte)0xC3, 0, 0)]
    [InlineData((byte)0xC7, 1, 1)]
    public void B7_ThePanValuesComeFromTheTopNodeAndTheByteFromPositioningBit2(byte pos, int c0, int unused)
    {
        // emu_play.py B7 (0x9FB9B8 -> 0x9FAEE8; 0x9FBE74): the walk from the leaf (no positioning of its own) climbs to the root (the first node with [node+0x40] & 0xFFE, or the root); the root's properties 0xC, 0xD, 0xE
        // (0.5f, -0.25f, 0.75f) land in pbi+0xB4/+0xB8/+0xBC and [root+0x47] bit 0 in pbi+0xC0: it is 1 only for positioning byte 0xC7 (bits 0 and 1 set, bit 2 stored by 0x9ECF7C..0x9ECF8C).
        var rig = new Rig();
        Build(rig, out var leaf, pos: pos, rootProps: new() { [0xC] = (uint)Bits(0.5f), [0xD] = (uint)Bits(-0.25f), [0xE] = (uint)Bits(0.75f) });
        var pbi = rig.NewPbi(leaf);
        rig.Path.InitContext9BEB30(pbi, 20f, 1, rig.Play, rig.Limiter);
        Assert.Equal((0x3F000000, unchecked((int)0xBE800000), 0x3F400000, c0), (Bits(pbi.PanB4), Bits(pbi.PanB8), Bits(pbi.PanBC), (int)pbi.PanC0));
        Assert.Equal(0x74, pbi.Flags0E8);
    }

    [Theory]
    [InlineData(0x17u, 0x3F800000, 0, 0, 1)]
    [InlineData(0x18u, 0x3F800000, 0, 0x40400000, 2)]
    [InlineData(0x12u, 0x40400000, 0, 0, 2)]
    [InlineData(0x13u, 0, 0x40400000, 0, 2)]
    public void B8_AnRtpcParameterReplacesTheMatchingValueThroughTheUnread0xA11590(uint param, int b4, int b8, int bc, int calls)
    {
        // emu_play.py B8: parameter 0x17 changes the first code of 0x9FB9B8 (vcvt.u32.f32 of the result, no visible change here); 0x18 replaces the third pan value, 0x12 and 0x13 the first and second (the other of the two is
        // 0 and the properties 0xC and 0xD are not read); the stand-in returns 3.0f. 0x9FAEE8 runs twice (0x9FB9B8 and 0x9FBE74), so the call count is 2 for the pan parameters and 1 for 0x17.
        var rig = new Rig { Rtpc = 3f };
        rig.Add(10, NodeParams(props: new() { [0xC] = (uint)Bits(1f) }, rtpcs: new[] { Rtpc(param) }));
        var pbi = rig.NewPbi(rig.Nodes[10]);
        rig.Path.InitContext9BEB30(pbi, 20f, 1, rig.Play, rig.Limiter);
        Assert.Equal((b4, b8, bc), (Bits(pbi.PanB4), Bits(pbi.PanB8), Bits(pbi.PanBC)));
        Assert.Equal(calls, rig.Log.Count(l => l.StartsWith("A11590")));
        Assert.Equal(0x74, pbi.Flags0E8);
        rig.Path.Rtpc = null;                                                         // the RTPC manager is required once a bit is evaluated
        var again = rig.NewPbi(rig.Nodes[10]);
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Path.InitContext9BEB30(again, 20f, 1, rig.Play, rig.Limiter));
    }

    [Theory]
    [InlineData(0, 4.0f, 1.0f, 2.0f)]
    [InlineData(1, 4.0f, 1.0f, 1.0f)]
    [InlineData(2, 4.0f, 1.0f, 15.0f)]
    public void B9_CalcEffectiveParamsResetsPrunesAndComposes(int variant, float lpf, float unusedA, float mute)
    {
        // emu_play.py B9 (CalcEffectiveParams alone, r1 = 0, as AddSrc calls it): the reset stage zeroes the block (+0x58/+0x60 & 0xFC, +0x70..+0x8F, +0x90, +0x94..+0x97, +0x3C..), the prune keeps the records with flag bit 1
        // (swap-remove with the last record: [(1,0,9), (2,2,0.5), (3,0,7), (4,2,4)] becomes [(4,2,4), (2,2,0.5)]), vt+0xAC leaves (-3, 50, 4, 5), the compose gives +0x3C -3, +0x44 50, +0x48 4, +0x4C 5, +0x98 -3, +0x9C 4, +0xA4 5,
        // +0x40 the product of the kept records (2.0, 1.0 for none, 15.0 for (3, 5)), +0xC4 101.0f, +0x1BC 1, E8 0x7D, the priority block stores 50.0f.
        var records = variant switch
        {
            0 => new[] { (1u, (byte)0, 9f), (2u, (byte)2, 0.5f), (3u, (byte)0, 7f), (4u, (byte)2, 4f) },
            1 => new[] { (1u, (byte)0, 9f) },
            _ => new[] { (1u, (byte)2, 3f), (2u, (byte)2, 5f) },
        };
        var rig = new Rig();
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf);
        foreach (var (w, f, v) in records) pbi.Transitions10C.Add(new WwiseTransitionRecord { Word0 = w, Flags4 = f, Value8 = v });
        pbi.Volume3C = pbi.Pitch44 = pbi.Lpf48 = pbi.Hpf4C = pbi.Field50 = pbi.Field54 = pbi.Field5C = pbi.Field68 = pbi.Field6C = 9f;
        pbi.Word64 = 9f;
        pbi.Byte58 = 0xFF; pbi.Byte60 = 0xFF; pbi.Word90 = 0xFFFFFFFF; pbi.Byte94 = pbi.Byte95 = pbi.Byte96 = pbi.Byte97 = 0xFF;
        Array.Fill(pbi.Block70, (byte)0xFF); Array.Fill(pbi.Block80, (byte)0xFF);
        rig.Accumulate = rig.Fill(-3f, 50f, 4f, 5f);
        rig.Path.CalcEffectiveParams(pbi, null, rig.Limiter);
        Assert.Equal(unchecked((int)0xC0400000), Bits(pbi.Volume3C));
        Assert.Equal(Bits(mute), Bits(pbi.MuteFade40));
        Assert.Equal((0x42480000, 0x40800000, 0x40A00000), (Bits(pbi.Pitch44), Bits(pbi.Lpf48), Bits(pbi.Hpf4C)));
        Assert.Equal((unchecked((int)0xC0400000), 0x40800000, 0x40A00000, 0), (Bits(pbi.Field98), Bits(pbi.Field9C), Bits(pbi.FieldA4), Bits(pbi.ReadWord64())));
        Assert.Equal((0xFC, 0xFC, 0u), ((int)pbi.Byte58, (int)pbi.Byte60, pbi.Word90));
        Assert.All(pbi.Block70, x => Assert.Equal(0, x));
        Assert.All(pbi.Block80, x => Assert.Equal(0, x));
        Assert.Equal((0, 0, 0, 0), ((int)pbi.Byte94, (int)pbi.Byte95, (int)pbi.Byte96, (int)pbi.Byte97));
        Assert.Equal(0, Bits(pbi.Field50) | Bits(pbi.Field54) | Bits(pbi.Field5C) | Bits(pbi.Field68) | Bits(pbi.Field6C));
        Assert.Equal((0x7D, 1), (pbi.Flags0E8 & 0xFF, pbi.Flags1BC & 0xFF));
        Assert.Equal(0x42CA0000, Bits(pbi.FieldC4));
        Assert.Equal((0x42480000, 0, 0x42480000), (Bits(pbi.Field1CC), Bits(pbi.Field1D0), Bits(pbi.Priority1C0)));
        var kept = variant switch
        {
            0 => new[] { (4u, (byte)2, 4f), (2u, (byte)2, 0.5f) },
            1 => Array.Empty<(uint, byte, float)>(),
            _ => new[] { (1u, (byte)2, 3f), (2u, (byte)2, 5f) },
        };
        Assert.Equal(kept, pbi.Transitions10C.Select(t => (t.Word0, t.Flags4, t.Value8)).ToArray());
        Assert.Equal(new[] { "AC" }, rig.Log);
        Assert.Null(rig.LastAc!.Params);                                              // r1 = 0: no params block
        Assert.Null(rig.LastAc.Local);                                                // E8 0x5D has bit 6 set: no local block (oracle B1/B9 pass the pbi's own block)
    }

    [Theory]
    [InlineData(0, 0.0, 50.0, 50.0, 50.0, 0, 1, 0)]       // 1CC, 1D0 and 1C0 equal the new (50, 0): nothing
    [InlineData(1, 7.0, 0.0, 50.0, 50.0, 0, 0, 0)]        // 1CC differs: stored, and 1C0 equals the priority: no reposition
    [InlineData(2, 50.0, 3.0, 50.0, 50.0, 0, 0, 0)]       // 1D0 differs only: stored
    [InlineData(3, 50.0, 3.0, 20.0, 50.0, 0, 1, 2)]       // 1C0 differs: both lists repositioned and 1C0 stored
    public void B10_ThePriorityBlockStoresTheNewPairAndRepositionsWhenTheListedPriorityDiffers(int variant, double c1cc, double c1d0, double c1c0, double expected1cc, int expected1d0, int unusedA, int repositions)
    {
        // emu_play.py B10 (0x9FFE1C..0x9FFFE4): 1CC and 1D0 end 50.0f / 0.0f in every case; 0x9F36A0 ran for both array entries only when [pbi+0x1C0] differed (the log of the oracle: two calls with the new priority 50.0f).
        var rig = new Rig();
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf);
        rig.Accumulate = rig.Fill(0, 0, 0, 0);
        pbi.Field1CC = (float)c1cc; pbi.Field1D0 = (float)c1d0; pbi.Priority1C0 = (float)c1c0;
        var listA = new WwisePbiList(); var listB = new WwisePbiList();
        var low = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 8, TargetNodeId = 1 }, 1, Source, new byte[0x44], null, false) { Priority1C0 = 30f };
        rig.Limiter.InsertIntoList9F3274(listA, low); rig.Limiter.InsertIntoList9F3274(listA, pbi);
        rig.Limiter.InsertIntoList9F3274(listB, pbi);
        pbi.LimiterArray1EC.Items.Add(listA); pbi.LimiterArray1EC.Items.Add(listB);
        Assert.Equal(repositions == 2 ? new[] { low, pbi } : new[] { pbi, low }.OrderByDescending(p => p.Priority1C0).ToArray(), listA.Items.ToArray());
        rig.Path.InitContext9BEB30(pbi, 20f, 1, rig.Play, rig.Limiter);
        Assert.Equal((0x42480000, 0, 0x42480000), (Bits(pbi.Field1CC), Bits(pbi.Field1D0), Bits(pbi.Priority1C0)));
        Assert.Equal(new[] { pbi, low }, listA.Items.ToArray());                      // 50.0f above 30.0f in descending order
    }

    [Fact]
    public void B10_ANaNPreviousPriorityIsNotEqualAndRunsTheStore()
    {
        // emu_play.py B10 'NaN 1CC': vcmp is unordered, beq is not taken: 1CC/1D0 are stored (50.0f, 0.0f) and, 1C0 being 50.0f, nothing is repositioned.
        var rig = new Rig();
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf);
        rig.Accumulate = rig.Fill(0, 0, 0, 0);
        pbi.Field1CC = float.NaN; pbi.Priority1C0 = 50f;
        rig.Path.InitContext9BEB30(pbi, 20f, 1, rig.Play, rig.Limiter);
        Assert.Equal((0x42480000, 0), (Bits(pbi.Field1CC), Bits(pbi.Field1D0)));
    }

    [Theory]
    [InlineData(0u, false, false)]
    [InlineData(0x1111u, true, false)]
    [InlineData(0x1111u, true, true)]
    [InlineData(0u, false, true)]
    public void B11_TheFirstTimeBlockRunsOnlyWithE8Bit6ClearAndSetsIt(uint field34, bool e8224, bool e62ac)
    {
        // emu_play.py B11 (E8 = 0x1D, bit 6 clear): 0x9E8224 runs when [pbi+0x34] != 0; 0x9E62AC when the zero block vt+0xAC received (the local block at sp+0x2C, E8 bit 6 clear) has its word at +4 set by the call;
        // E8 ends 0x74 (bit 6 set, 0x9FFF9C..0x9FFFA4). The vt+0xAC stand-in receives the local block, not params+0x108. Since batch 5c 0x9E8224 and 0x9E62AC are the C# ports (WwiseModulatorManager): the first clears
        // [item+0xC] of every list item, the second visits the ids of the local block's records through the stand-in 0x9DCE44 (WwiseModulatorListTests checks them against the real functions).
        var rig = new Rig();
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf, e8: 0x1D);
        pbi.Field34 = field34;
        var item = new WwiseListRecord { Word0C = 7 };
        rig.Records34.Add(item);
        rig.Accumulate = a => { Assert.NotNull(a.Local); Assert.Null(a.Params); if (e62ac) { a.Local!.Word4 = 1; a.Local.Records.Add(new WwiseModulatorOutRecord { Word4 = 5, WordC = 6, Ids10 = new uint[] { 41, 42 } }); } };
        rig.Path.InitContext9BEB30(pbi, 20f, 1, rig.Play, rig.Limiter);
        Assert.Equal(0x74, pbi.Flags0E8);
        Assert.Equal(e8224 ? 0u : 7u, item.Word0C);
        Assert.Equal(e62ac ? new[] { "AC", "9DCE44:41", "9DCE44:42" } : new[] { "AC" }, rig.Log);
    }

    [Fact]
    public void B6_ANodeChainWithNoOutputBusSelectsTheUnadoptedCachedPathAndOtherwiseTheNormalOne()
    {
        // emu_play.py B6: with no output bus (r6 = 0) and [params+0x11C] == 0 the compare at 0x9FFB04 selects the cached-block path 0x9FFED4.. (not adopted: it throws here); a non-zero word runs the normal path; with E9 bit 2
        // the cached path's own test sends it to the reset (0x9FFEDC bne 0x9FFB10), which is the normal flow.
        var rig = new Rig();
        rig.HasBus = false;                                                            // 0x9F4BB8 finds no [+0x38] link on the chain: r6 = 0
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf);
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Path.InitContext9BEB30(pbi, 20f, 1, rig.Play, rig.Limiter));
        rig.Play.Word11C = 0x1234;
        var other = rig.NewPbi(leaf);
        Assert.Equal(1, rig.Path.InitContext9BEB30(other, 20f, 1, rig.Play, rig.Limiter).Result);
        Assert.Equal(new[] { "AC" }, rig.Log);
        var bus = rig.NewPbi(leaf, e9: 5);
        rig.Play.Word11C = 0;
        Assert.Equal(1, rig.Path.InitContext9BEB30(bus, 20f, 1, rig.Play, rig.Limiter).Result);
    }

    [Fact]
    public void B6_AContextObjectOrAnUnsetSeamIsAVisibleStop()
    {
        // 0x9BEBD8: [ctx+0xD0] != 0 goes to 0x9BE898 (unread); an unset seam of the shipped path throws instead of defaulting.
        var rig = new Rig();
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf);
        pbi.CtxD0 = new object();
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Path.InitContext9BEB30(pbi, 20f, 1, rig.Play, rig.Limiter));
        foreach (var unset in new Action<Rig>[] { r => r.Path.RuntimeNodeOf = null, r => r.Path.RuntimeNodeOf = _ => null, r => r.Seams.NodeVtAC = null })
        {
            var r = new Rig();
            Build(r, out var l);
            unset(r);
            Assert.Throws<WwiseMissingBehaviourException>(() => r.Path.InitContext9BEB30(r.NewPbi(l), 20f, 1, r.Play, r.Limiter));
        }
    }

    // ------------------------------------------------------------------ 0x9BCA68 and 0x9FF368

    [Theory]
    [InlineData((byte)0x5D, (byte)1, -100f, 1, 0.5f, 1)]
    [InlineData((byte)0x7D, (byte)1, -100f, 1, 0.5f, 0)]
    [InlineData((byte)0x7D, (byte)0, -100f, 1, 1.0f, 0)]
    [InlineData((byte)0x7D, (byte)0, 0f, 0, 1.0f, 0)]
    [InlineData((byte)0x7D, (byte)1, 0f, 0, 0.5f, 0)]
    public void B12_0x9BCA68RunsCalcEffectiveParamsOrTheRecomputeAndThenTheBelowTest(byte e8, byte e9, float volume, int result, float mute, int acCalls)
    {
        // emu_play.py B12: E8 bit 5 clear -> CalcEffectiveParams(r1 = 0) (one vt+0xAC call, the record (1, 2, 0.5f) gives +0x40 0.5); bit 5 set and E9 bit 0 -> 0x9FF368 (+0x40 0.5, +0x3C = +0x98 + +0x118); bit 5 set and
        // E9 bit 0 clear -> only the test over the stored fields. Results: 1 when the product is below 2^-16, else 0; E8 ends 0x7D or 0x7D (bit 5 set by CalcEffectiveParams), E9 bit 0 cleared by either recompute.
        var rig = new Rig();
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf, e8, e9);
        pbi.Transitions10C.Add(new WwiseTransitionRecord { Word0 = 1, Flags4 = 2, Value8 = 0.5f });
        rig.Accumulate = rig.Fill(volume, 0f, 0f, 0f);
        pbi.Field98 = volume; pbi.Volume3C = volume; pbi.MuteFade40 = 1f;
        Assert.Equal(result, rig.Path.A9BCA68(pbi, rig.Limiter));
        Assert.Equal((Bits(volume), Bits(mute), 0), (Bits(pbi.Volume3C), Bits(pbi.MuteFade40), Bits(pbi.ReadWord64())));
        Assert.Equal((0x7D, 0), (pbi.Flags0E8 & 0xFF, pbi.Flags0E9 & 0xFF));
        Assert.Equal(acCalls, rig.Log.Count(l => l == "AC"));
    }

    [Theory]
    [InlineData(-96f, 1f, 0f, 0)]
    [InlineData(-79.5f, 1f, 0f, 0)]
    [InlineData(-80f, 1f, 0f, 0)]
    [InlineData(0f, 0f, 0f, 1)]
    [InlineData(0f, 1f, -100f, 1)]
    [InlineData(float.NaN, 1f, 0f, 1)]
    [InlineData(0f, float.NaN, 0f, 0)]
    [InlineData(-740f, 1f, 0f, 1)]
    [InlineData(-740.1f, 1f, 0f, 1)]
    [InlineData(-739.9f, 1f, 0f, 1)]
    [InlineData(0f, 1f, -739.9f, 1)]
    [InlineData(-30f, 0.5f, -30f, 0)]
    [InlineData(-20f, 0.00001f, -20f, 1)]
    public void B12_TheBelowTestOver3C40And64MatchesTheEngine(float v3c, float f40, float v64, int expected)
    {
        // emu_play.py B12 'the below test over (3C, 40, 64)': (lin(+0x3C) * +0x40) * lin(+0x64) <= 0x37800000 (2^-16); a NaN volume gives lin 0 (vcvt.u32.f32 of NaN is 0), a NaN +0x40 makes the product unordered (0).
        var rig = new Rig();
        Build(rig, out var leaf);
        var pbi = rig.NewPbi(leaf, 0x7D, 0);
        pbi.Volume3C = v3c; pbi.MuteFade40 = f40; pbi.Word64 = v64;
        Assert.Equal(expected, rig.Path.A9BCA68(pbi, rig.Limiter));
    }

    // ------------------------------------------------------------------ L, P: 0xA1E280 and 0xA00618

    [Theory]
    [InlineData(null, null, 0UL, 1)]
    [InlineData(0u, null, 0UL, 0)]
    [InlineData(2u, null, 0UL, 2)]
    [InlineData(null, new[] { 3u, 3u }, 0UL, 4)]
    [InlineData(1u, new[] { 2u, 10u }, 0UL, 3)]
    [InlineData(1u, new[] { 2u, 10u }, 12345UL, 3)]
    [InlineData(null, new[] { 0xFFFFFFFBu, 5u }, 0x0123456789ABCDEFUL, -3)]
    [InlineData(0x7FFFu, new[] { 1u, 3u }, 0x0000000700000007UL, -32767)]
    [InlineData(0xFFFFFFFFu, null, 0UL, -1)]
    public void L1_0xA1E280TheLoopCountIsTheBasePlusTheMinimumPlusADrawSignExtendedTo16Bits(uint? baseValue, uint[]? range, ulong seed, int expected)
    {
        // emu_play.py L1: property 0x3A (default 1) plus, with a ranged property 0x3A, min + trunc_s32(0.5 + (hi31 / 2147483647.0) * (max - min)) from the global LCG (a zero span does not advance it), sxth.
        var rig = new Rig();
        var props = baseValue is { } b ? new Dictionary<byte, uint> { [0x3A] = b } : new Dictionary<byte, uint>();
        var ranged = range is { } r ? new Dictionary<byte, (float, float)> { [0x3A] = (BitConverter.UInt32BitsToSingle(r[0]), BitConverter.UInt32BitsToSingle(r[1])) } : new Dictionary<byte, (float, float)>();
        var node = rig.Add(1, NodeParams(props: props, ranged: ranged));
        rig.Path.Rng = new WwiseRng(seed);
        Assert.Equal(expected, rig.Path.LoopCountA1E280(node));
    }

    [Fact]
    public void L1_TheLoopCountAdvancesTheGlobalLcgOnlyForANonZeroSpan()
    {
        // emu_play.py L1: after seed 12345 and a span of 8 the engine's state is 0x0807DC72_1521C106; the span 0 cases leave it. A ranged Sound without the LCG is a visible stop.
        var rig = new Rig();
        var node = rig.Add(1, NodeParams(props: new() { [0x3A] = 1 }, ranged: new() { [0x3A] = (BitConverter.UInt32BitsToSingle(2), BitConverter.UInt32BitsToSingle(10)) }));
        var rng = new WwiseRng(12345);
        rig.Path.Rng = rng;
        Assert.Equal(3, rig.Path.LoopCountA1E280(node));
        Assert.Equal(new WwiseRng(0x0807DC721521C106UL).Next(), rng.Next());
        var flat = rig.Add(2, NodeParams(ranged: new() { [0x3A] = (BitConverter.UInt32BitsToSingle(3), BitConverter.UInt32BitsToSingle(3)) }));
        var rng2 = new WwiseRng(77);
        rig.Path.Rng = rng2;
        rig.Path.LoopCountA1E280(flat);
        Assert.Equal(new WwiseRng(77).Next(), rng2.Next());
        rig.Path.Rng = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Path.LoopCountA1E280(node));
    }

    [Fact]
    public void P9_0xA00618StoresTheLoopCountEveryCallSetsBit0OnceAndNeedsTheUnread0x9FF0D8ForAnEmitter()
    {
        // emu_play.py P9: first call: [pbi+0x1B8] = 5 (property 0x3A), 1BD 0x44 -> 0x45; a second call stores 9 and leaves the bit; with 1BD bit 0 clear again and [pbi+0xAC] != 0 the tail 0x9FF0D8 runs ([pbi+0xAC] is its argument).
        var rig = new Rig();
        var node = rig.Add(1, NodeParams(props: new() { [0x3A] = 5 }));
        var pbi = rig.NewPbi(node);
        pbi.Flags1BD = 0x44;
        rig.Path.BeforePlayA00618(pbi);
        Assert.Equal((5, 0x45), ((int)pbi.LoopCount1B8, (int)pbi.Flags1BD));
        rig.Nodes[1] = node with { Params = NodeParams(props: new() { [0x3A] = 9 }) };
        pbi.NodeE0 = rig.Nodes[1];
        rig.Path.BeforePlayA00618(pbi);
        Assert.Equal((9, 0x45), ((int)pbi.LoopCount1B8, (int)pbi.Flags1BD));
        pbi.Flags1BD = 0x44;
        pbi.Field0AC = new object();
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Path.BeforePlayA00618(pbi));
        var seen = new List<WwisePlayingInstance>();
        rig.Seams.A9FF0D8 = seen.Add;
        pbi.Flags1BD = 0x44;
        rig.Path.BeforePlayA00618(pbi);
        Assert.Equal(new[] { pbi }, seen);
    }

    // ------------------------------------------------------------------ T: the transition manager

    [Theory]
    [InlineData(500, 0x400, 0xBE5A740E, 0x3F4ED1AA)]
    [InlineData(1, 1, 0xBDD55555, 0x3F66ACDB)]
    [InlineData(100, 0x400, 0xBF888889, 0x3EB034EC)]
    [InlineData(1500, 0x400, 0xBD91A2B3, 0x3F6E6D81)]
    [InlineData(-500, 0x400, 0x3E5A740E, 0x3F9E7023)]
    [InlineData(500, 0, 0x0, 0x3F800000)]
    [InlineData(500, 0xFFFF, 0xC15A7333, 0x359DE1D7)]
    public void T1_0xA358ECTheFactorIsExpOfMinusFramesOverTheTimeInSamples(int timeMs, int frames, uint argumentBits, uint resultBits)
    {
        // emu_play.py T1: expf's argument is (float)(-frames) / ((float)t * 0.2f / 1000.0f * 48000.0f) in single precision (the bits above); expf is the phone's libm, so the result is checked to one ulp of the oracle's correctly
        // rounded value. With a zero time or [item+0x34] bit 2 clear nothing is stored.
        var item = new WwiseTransitionItem { Flags34 = 0x04, Factor38 = BitConverter.UInt32BitsToSingle(0x11111111) };
        float seen = float.NaN;
        WwiseTransitionManager.SetFactorA358EC(item, timeMs, (ushort)frames, x => { seen = x; return MathF.Exp(x); });
        Assert.Equal((int)argumentBits, Bits(seen));
        Assert.InRange(BitConverter.SingleToUInt32Bits(item.Factor38), resultBits - 1, resultBits + 1);
        var off = new WwiseTransitionItem { Flags34 = 0x00, Factor38 = BitConverter.UInt32BitsToSingle(0x11111111) };
        WwiseTransitionManager.SetFactorA358EC(off, 500, 0x400, x => 0f);
        Assert.Equal(0x11111111u, BitConverter.SingleToUInt32Bits(off.Factor38));
        var zero = new WwiseTransitionItem { Flags34 = 0x04, Factor38 = BitConverter.UInt32BitsToSingle(0x11111111) };
        WwiseTransitionManager.SetFactorA358EC(zero, 0, 0x400, x => 0f);
        Assert.Equal(0x11111111u, BitConverter.SingleToUInt32Bits(zero.Factor38));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 2, 1)]
    [InlineData(2, 2, 1)]
    [InlineData(3, 3, 4)]
    [InlineData(4, 3, 4)]
    [InlineData(5, 5, 5)]
    public void T2_TheStateTogglesA366ACAndA366D0(int state, int after366AC, int after366D0)
    {
        // emu_play.py T2: 0xA366AC maps 1 to 2 and 4 to 3; 0xA366D0 maps 3 to 4 and 2 to 1; every other state is left.
        var rig = new Rig();
        var manager = new WwiseTransitionManager(rig.Seams);
        rig.Seams.TransitionInit9A35D44 = (_, _, _) => 1;
        rig.Seams.TransitionApplyAtOnce = _ => { };
        var item = manager.Create0A36268(new WwiseTransitionInfo(rig.NewPbi(rig.Add(1, NodeParams())), 0x1000000, 0f, 1f, 1, 0, 0, 1, 0), false, 0)!;
        item.State30 = state;
        manager.Toggle366AC(item.Id);
        Assert.Equal(after366AC, item.State30);
        item.State30 = state;
        manager.Toggle366D0(item.Id);
        Assert.Equal(after366D0, item.State30);
        Assert.Throws<InvalidOperationException>(() => manager.Toggle366AC(0x77));
    }

    [Theory]
    [InlineData("ok, start, list A", 1, true, 0, -1, 0, 4, true, 1, 1, 4, 1)]
    [InlineData("ok, no start, list B", 1, false, 1, -1, 0, 4, true, 0, 1, 4, 1)]
    [InlineData("init returns 2", 2, true, 0, -1, 0, 4, false, 0, 0, 4, 1)]
    [InlineData("item alloc fails", 1, true, 0, 1, 0, 4, false, 0, 0, 4, 1)]
    [InlineData("vector full: grows by 0x80", 1, true, 0, -1, 4, 4, true, 1, 5, 132, 2)]
    [InlineData("vector full: growth alloc fails", 1, true, 0, 2, 4, 4, false, 0, 4, 4, 2)]
    [InlineData("count 3 of 4", 1, true, 0, -1, 3, 4, true, 1, 4, 4, 1)]
    public void T3_0xA36268CreatesAnItemAppendsItAndTakesTheFailureExitsInTheEnginesOrder(string label, int initResult, bool start, int list, int failAlloc, int count0, int cap0,
        bool created, int state, int count, int cap, int allocs)
    {
        // emu_play.py T3 (0xA36268 with the ctor 0xA35838, 0xA35D44 stood in, the vector growth and the failure exit): the result is an item (state 1 when the start argument is set, word 0 0x20000000) or 0; the init sees
        // (info, [mgr+0x4C]); an init result of 2 and a growth failure call the item teardown 0xA3587C and then the failure exit [info.target]->vt+0(id, to, 1); an item allocation failure goes to the failure exit directly;
        // the vector grows by 0x80 entries when full. The oracle's allocation count includes the item (1) and the growth (2).
        var rig = new Rig();
        var manager = new WwiseTransitionManager(rig.Seams) { Tick = 0x5555 };
        var calls = new List<string>();
        int allocCalls = 0;
        manager.AllocationFails = () => ++allocCalls == failAlloc;
        rig.Seams.TransitionInit9A35D44 = (_, info, tick) => { calls.Add("A35D44:" + tick.ToString("X")); return initResult; };
        rig.Seams.TransitionTeardownA3587C = _ => calls.Add("A3587C");
        rig.Seams.TransitionApplyAtOnce = info => calls.Add($"apply:{info.Id:X}:{Bits(info.To):X}");
        var target = rig.NewPbi(rig.Add(1, NodeParams()));
        var vector = list == 1 ? manager.ListB : manager.ListA;
        for (int i = 0; i < count0; i++) vector.Add(new WwiseTransitionItem());
        if (list == 1) manager.CapacityB = cap0; else manager.CapacityA = cap0;
        var item = manager.Create0A36268(new WwiseTransitionInfo(target, 0x1000000, 0f, 1f, 7, 3, 0, 1, 0), start, list);
        Assert.Equal(created, item is not null);
        if (item is not null)
        {
            Assert.Equal((state, 0x20000000u), (item.State30, item.Word0));
            Assert.Same(item, vector[^1]);
        }
        Assert.Equal(count, vector.Count);
        Assert.Equal(cap, list == 1 ? manager.CapacityB : manager.CapacityA);
        Assert.Equal(allocs, allocCalls);
        var expected = new List<string>();
        if (label == "item alloc fails") expected.Add("apply:1000000:3F800000");
        else if (!created) expected.AddRange(new[] { "A35D44:5555", "A3587C", "apply:1000000:3F800000" });
        else expected.Add("A35D44:5555");
        Assert.Equal(expected, calls);
    }

    // ------------------------------------------------------------------ 0xA0067C through the bridge

    [Fact]
    public void P10_0xA0067CAFadeTimeWordOfMinusZeroCountsAndAnExistingTransitionIsReaimedNotRecreated()
    {
        // 0xA00694 cmp r5,#0 compares the word, so -0.0f (0x80000000) runs the fade branch; with a transition at pbi+0x144 (0xA00780 cmp r2,#0) 0xA366F4 re-aims it with (0x1000000, 1.0f, time, curve, 0) and
        // pbi+0x144, 1BE bit 6 and the vt+0x50 call are not touched (the flow jumps to 0xA006A4); pbi+0x168 is zeroed in both.
        var rig = new Rig();
        var seams = rig.Seams;
        seams.TransitionInit9A35D44 = (_, _, _) => 1;
        var manager = new WwiseTransitionManager(seams);
        rig.Path.Transitions = manager;
        var list = new WwiseStartList();
        var pbi = rig.NewPbi(rig.Add(1, NodeParams()));
        var fade = new WwiseFadeInTransition { FadeInTime = -0f, FadeCurve = 3 };
        Assert.Equal(1, rig.Path.PbiPlayA0067C(pbi, fade, false, false, list, 5, rig.Limiter));
        Assert.Equal(0f, pbi.Fade168);
        Assert.NotEqual(0u, pbi.Field144);
        Assert.Equal(0x40, pbi.Flags1BE & 0x40);
        uint item = pbi.Field144;
        var reaim = new List<(uint, float, uint, int, int)>();
        rig.Limiter.ReaimTransitionA366F4 = (_, id, end, time, curve, mode) => reaim.Add((id, end, time, curve, mode));
        pbi.Fade168 = 0.25f;
        pbi.Flags1BE = 0;
        Assert.Equal(1, rig.Path.PbiPlayA0067C(pbi, new WwiseFadeInTransition { FadeInTime = 250f, FadeCurve = 6 }, false, false, list, 6, rig.Limiter));
        Assert.Equal(new[] { (0x1000000u, 1f, BitConverter.SingleToUInt32Bits(250f), 6, 0) }, reaim);
        Assert.Equal((item, 0f, 0), (pbi.Field144, pbi.Fade168, pbi.Flags1BE));
        Assert.Equal(new[] { 0, 0 }, list.Nodes.Select(n => n.Type).ToArray());
        // no fade: no transition touched
        var plain = rig.NewPbi(rig.Add(2, NodeParams()));
        Assert.Equal(1, rig.Path.PbiPlayA0067C(plain, new WwiseFadeInTransition { FadeInTime = 0f }, true, false, list, 7, rig.Limiter));
        Assert.Equal((0u, 1f, 0x80), (plain.Field144, plain.Fade168, plain.Flags1BC & 0x80));
        Assert.Equal(1, list.Nodes[^1].Type);
        Assert.True(list.Gate);
    }

    [Fact]
    public void P12_0x9D3558TakesFreeNodesFirstThenNeedsRoomUnderTheCapAndTheAllocator()
    {
        // 0x9D3558: a node from the free chain [+0xC]; else the node count [+0x18] must be below the cap [+0x14] and the 0x10-byte allocation succeed, else 2; the node count rises; kind <= 1 sets the gate byte [+0x28]
        // (unsigned compare, 0x9D35DC strbls); the node carries the key [B+0] (the tick).
        var list = new WwiseStartList { Capacity14 = 2 };
        var rig = new Rig();
        var pbi = rig.NewPbi(rig.Add(1, NodeParams()));
        Assert.Equal(1, list.Enqueue(2, pbi, 9));
        Assert.False(list.Gate);                                                      // kind 2: no gate
        Assert.Equal(1, list.Enqueue(1, pbi, 10));
        Assert.True(list.Gate);
        Assert.Equal(2u, list.Count18);
        Assert.Equal(2, list.Enqueue(0, pbi, 11));                                    // [+0x18] == [+0x14]: 0x9D35F4
        list.FreeChainCount = 1;
        Assert.Equal(1, list.Enqueue(0, pbi, 12));                                    // a free node is taken before the cap is looked at
        Assert.Equal(new long[] { 9, 10, 12 }, list.Nodes.Select(n => n.Tick).ToArray());
        var failing = new WwiseStartList { AllocationFails = () => true };
        Assert.Equal(2, failing.Enqueue(0, pbi, 1));
    }

    [Fact]
    public void P12_0x9E808CCountsEveryRecordAndResetsOnesThatReachTheirLimit_AndBda28NeedsTheUnread9FD8C0()
    {
        // 0x9E808C (with the verifier's correction): [rec+0x58]++ on every record; a state other than 3 whose counter reached [rec+0x50] (signed) with [rec+0x30] != 0 stores [[rec+0x30]+8] to [rec+0x4C] and clears
        // [rec+0x30]. 0x9BDA28 returns at once for [ctx+0xA0] == 0 (0x9BDA2C bxeq) and otherwise reaches 0x9FF2CC / 0x9FF290 -> 0x9FD8D0 / 0x9FD8C0 (unread).
        var rig = new Rig();
        var pbi = rig.NewPbi(rig.Add(1, NodeParams()));
        var records = new[]
        {
            new WwiseListRecord { State48 = 1, Counter58 = 0, Limit50 = 2, Object30Word8 = 0xAA },
            new WwiseListRecord { State48 = 3, Counter58 = 5, Limit50 = 1, Object30Word8 = 0xBB },
            new WwiseListRecord { State48 = 1, Counter58 = 5, Limit50 = 6, Object30Word8 = null },
            new WwiseListRecord { State48 = 2, Counter58 = int.MaxValue, Limit50 = int.MinValue, Object30Word8 = 0xCC },
        };
        rig.Seams.RecordsOf34 = _ => records;
        rig.Path.A9E808C(pbi);
        Assert.Equal(new[] { 1, 6, 6, int.MinValue }, records.Select(r => r.Counter58).ToArray());
        Assert.Equal(0u, records[0].Word4C);                                          // 1 < 2: untouched
        Assert.Equal(0xAAu, records[0].Object30Word8);
        Assert.Equal(0xBBu, records[1].Object30Word8);                                // state 3: never reset
        Assert.Null(records[2].Object30Word8);                                        // 6 >= 6 with no object: nothing to store
        Assert.Equal((0xCCu, null), (records[3].Word4C, records[3].Object30Word8));   // wrapped counter >= INT_MIN
        rig.Path.A9BDA28(pbi, true);
        pbi.Field0AC = new object();
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Path.A9BDA28(pbi, false));
    }
}
