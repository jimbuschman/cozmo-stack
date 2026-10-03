using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>Test doubles for the seams M6-026 leaves required (each is an unread body or an unsettled row).</summary>
internal static class WwisePlaybackLimiterTestDoubles
{
    public static WwisePlaybackLimiter Create(Func<uint, WwiseNode?> lookup)
    {
        var l = new WwisePlaybackLimiter(lookup);
        l.RtpcSubscribeA19ECC = (_, _, _) => { };      // double: 0x9F7390..0x9F82EC is a RECOVERABLE_GAP ("changes nothing on shipped data", L5)
        l.RegisterPlayingIdA04D48 = _ => { };          // double: the playing-id table belongs to the event runtime (the live rig wires it)
        l.ReleasePlayingIdA04DE8 = _ => { };           // double: 0xA04DE8 -> 0xA03618 is a RECOVERABLE_GAP
        l.MediaTable = new WwiseMediaTable(new WwiseBankMemory());   // C31.1: 0xA1EC54 looks the source id up in the media table (an empty table gives the pair (0, 0))
        l.TermSteps6And7 = _ => { };                   // double: 0xA1C660 and the [pbi+0x10C] free are unread
        l.TermSteps10To13 = _ => { };                  // double: 0xA3E27C, 0x9BDC8C, 0xA1E8F4 are unread
        l.RtpcUnsubscribeA19F60 = _ => { };            // double: 0xA19F60 is unread
        return l;
    }
}

/// <summary>
/// M6-026, the playback-limit walker. Every expected value comes from the rows of correction C28 and the report
/// re-analysis/research/20260929-B-M6b-4-batch4b-limits.md (row ids in each test name), from the shipped banks, or from the
/// citation check; none comes from running the implementation. Floats are compared as the engine's bit patterns.
/// </summary>
public class WwisePlaybackLimiterTests
{
    // ------------------------------------------------------------------ builders

    private static WwiseNodeParams P(uint bus = 0, uint parent = 0, byte bits = 0, byte adv0 = 0, ushort max = 0,
        byte adv1 = 0, byte adv3 = 0, IReadOnlyDictionary<byte, uint>? props = null, IReadOnlyList<WwiseRtpc>? rtpcs = null)
        => new(bus, parent, bits, props ?? new Dictionary<byte, uint>(), new Dictionary<byte, (float, float)>(),
            rtpcs ?? Array.Empty<WwiseRtpc>(), Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>())
        {
            AdvancedByte0 = adv0, AdvancedByte1 = adv1, AdvancedMaxInstancesRaw = max, AdvancedByte3 = adv3,
        };

    private static WwiseSoundNode Snd(uint id, WwiseNodeParams p) => new(id, "t.bnk", p, 0x00040001, 1, 12345, 0, 0);

    private static WwiseActorMixerNode Mix(uint id, WwiseNodeParams p) => new(id, "t.bnk", p, Array.Empty<uint>());

    private static WwiseBusNode Bus(uint id, uint parent, ushort max = 0, byte b = 0,
        IReadOnlyList<(uint BusId, float DuckVolumeDb, uint FadeOutMs, uint FadeInMs)>? ducks = null, uint recoveryMs = 0)
        => new(id, "t.bnk", P(parent: parent), Array.Empty<WwiseBusEffect>(), ducks ?? Array.Empty<(uint, float, uint, uint)>())
        { MaxInstances = max, ByteB = b, RecoveryMs = recoveryMs };

    private sealed class Graph
    {
        private readonly Dictionary<uint, WwiseNode> _nodes = new();
        public Graph(params WwiseNode[] nodes) { foreach (var n in nodes) _nodes[n.Id] = n; }
        public WwiseNode? Lookup(uint id) => _nodes.TryGetValue(id, out var n) ? n : null;
        public WwiseNode this[uint id] => _nodes[id];
    }

    private static WwisePlaybackLimiter Limiter(Graph g) => WwisePlaybackLimiterTestDoubles.Create(g.Lookup);

    private static uint _playingId = 1000;

    /// <summary>A PBI with the given priority; its keys are the ctor's creation counters unless <paramref name="chain"/>/<paramref name="k4"/> place it.</summary>
    private static readonly WwiseSourceDescriptor TestSource = new(WwiseSourceFactory.VorbisPlugin, 1, 12345, 0, 0);   // a Sound's source block (0xA1EA68): the media table is keyed by its source id

    private static WwisePlayingInstance Pbi(float prio, uint? go = null, uint chain = 0, uint? k4 = null)
    {
        var p = new WwisePlayInitParams { PlayingId = ++_playingId, TargetNodeId = 1, ChainId = chain };
        var pbi = new WwisePlayingInstance(p, 1, TestSource, new byte[0x44], null, continuous: false)
        {
            Priority1C0 = prio, GameObject14 = go,
        };
        if (chain != 0) pbi.Flags1BE = (byte)(pbi.Flags1BE & ~8);          // params+0x7C != 0 also sets 1BE bit3 (8.6); a test key must stay countable
        if (k4 is { } k) pbi.Key1C4 = k;
        return pbi;
    }

    private static WwisePlayingInstance PbiWithId(uint playingId)
        => new(new WwisePlayInitParams { PlayingId = playingId, TargetNodeId = 1 }, 1, TestSource, new byte[0x44], null, continuous: false);

    private static WwiseLimitBlock Block(WwiseLimiterArray? array, float prio = 50f, uint? go = null, byte b11 = 1)
        => new() { Priority = prio, GameObject = go, Array = array, Word0C = 3, B11 = b11 };

    private static float F(int bits) => BitConverter.Int32BitsToSingle(bits);

    /// <summary>The shipped banks. A missing OBB fails the test (a silent pass would prove nothing) unless COZMO_ALLOW_MISSING_ASSETS is set.</summary>
    private static WwiseSoundLibrary RequireLibrary()
        => WwiseAssets.Library ?? throw new Xunit.Sdk.XunitException(
            "the shipped banks (re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip) are not present; these tests check the binary's data");

    // ------------------------------------------------------------------ the engine's floats (V1, P2a, C3)

    [Fact]
    public void M6_026_V1_P2a_C3_TheEnginesFloatsAreTheirBitPatterns()
    {
        // V1: s17 = 101.0f (0xA373F0). Report header: 101.0f = 0x42CA0000, 50.0f = 0x42480000, -10.0f = 0xC1200000, 1.0f = 0x3F800000.
        Assert.Equal(0x42CA0000, BitConverter.SingleToInt32Bits(WwisePlaybackLimiter.VictimInitialPriority));
        Assert.Equal(0x42480000, WwisePlaybackLimiter.DefaultPriorityBits);
        Assert.Equal(unchecked((int)0xC1200000), WwisePlaybackLimiter.DefaultPriorityOffsetBits);
        Assert.Equal(0x3F800000, WwisePlaybackLimiter.OneBits);
        var l = new WwisePlaybackLimiter(_ => null);
        Assert.Equal(0x3F800000, BitConverter.SingleToInt32Bits(l.MemoryThreshold1));      // C3: 0x99DCB8, both defaults 1.0f
        Assert.Equal(0x3F800000, BitConverter.SingleToInt32Bits(l.MemoryThreshold2));      // C3: 0xA571C0
        Assert.Equal(0x100, WwisePlaybackLimiter.StaticCtorMaxVoices);                     // 8.9: 0x4DE33C
    }

    // ------------------------------------------------------------------ P2a: 0x9F6B94

    [Fact]
    public void M6_026_P2a_TheDefaultPriorityIs50AndTheOffsetIsZeroWithoutBit7()
    {
        // P2a: property 7 default [0x108DB20+0x1C] = 50.0f; out[1] = 0.0f when [node+0x45]&0x80 == 0 (NodeBase bits b1).
        var g = new Graph(Snd(1, P(parent: 2)), Mix(2, P()));
        var l = Limiter(g);
        l.Priority9F6B94(g[1], out float prio, out float offset);
        Assert.Equal(0x42480000, BitConverter.SingleToInt32Bits(prio));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(offset));
    }

    [Fact]
    public void M6_026_P2a_ABit0NodeUsesItsOwnPriorityAndABit0ClearNodeTakesItsParents()
    {
        // P2a: with a parent and [node+0x40]&1 == 0 the parent answers; with bit 0 set the node's own property 7 is read.
        // P2b: SFX 153471530 sets prop 7 = 0x42C60000 (99.0f) with NodeBase bit 0.
        uint p99 = 0x42C60000, p70 = 0x428C0000;
        var g = new Graph(
            Snd(1, P(parent: 2, bits: 1, props: new Dictionary<byte, uint> { [7] = p99 })),        // own priority
            Snd(3, P(parent: 2, bits: 0, props: new Dictionary<byte, uint> { [7] = p99 })),        // bit 0 clear: the parent's, not its own
            Mix(2, P(bits: 1, props: new Dictionary<byte, uint> { [7] = p70 })));
        var l = Limiter(g);
        l.Priority9F6B94(g[1], out float own, out _);
        l.Priority9F6B94(g[3], out float inherited, out _);
        Assert.Equal((int)p99, BitConverter.SingleToInt32Bits(own));
        Assert.Equal((int)p70, BitConverter.SingleToInt32Bits(inherited));
    }

    [Fact]
    public void M6_026_P2a_Bit7WithoutProperty8GivesTheDefaultMinus10()
    {
        // P2a: out[1] = property 8, default [0x108DB20+0x20] = -10.0f (0xC1200000), when [node+0x45]&0x80 (NodeBase bits b1).
        var g = new Graph(Snd(1, P(bits: 2)), Snd(2, P(bits: 2, props: new Dictionary<byte, uint> { [8] = 0x40A00000 })));
        var l = Limiter(g);
        l.Priority9F6B94(g[1], out _, out float defaulted);
        l.Priority9F6B94(g[2], out _, out float set);
        Assert.Equal(unchecked((int)0xC1200000), BitConverter.SingleToInt32Bits(defaulted));
        Assert.Equal(0x40A00000, BitConverter.SingleToInt32Bits(set));
    }

    [Fact]
    public void M6_026_P2a_ARtpcOnParameter0x11IsAVisibleStop()
    {
        // P2a: mask bit 17 goes through 0xA11590 -> 0xA17724/0xA17878 (RECOVERABLE_GAP); no shipped node has one (P2b).
        var rtpc = new WwiseRtpc(1, 0, 0, 0x11, 0, 0, Array.Empty<(float, float, uint)>());
        var g = new Graph(Snd(1, P(rtpcs: new[] { rtpc })));
        Assert.Throws<WwiseMissingBehaviourException>(() => Limiter(g).Priority9F6B94(g[1], out _, out _));
    }

    [Fact]
    public void M6_026_P2b_ShippedPriorityProperty7ValuesAreTheBankValues()
    {
        // P2b (HIRC census): SFX 153471530 and 506971044 carry prop 7 = 0x42C60000, SFX 571039165 carries 0x428C0000; every other node resolves to 50.0f.
        var lib = RequireLibrary();
        var l = new WwisePlaybackLimiter(lib.Node);
        l.Priority9F6B94(lib.Node(153471530)!, out float a, out _);
        l.Priority9F6B94(lib.Node(571039165)!, out float b, out _);
        Assert.Equal(0x42C60000, BitConverter.SingleToInt32Bits(a));
        Assert.Equal(0x428C0000, BitConverter.SingleToInt32Bits(b));
        l.Priority9F6B94(lib.Node(62050212)!, out float mixer, out _);
        Assert.Equal(0x42480000, BitConverter.SingleToInt32Bits(mixer));
    }

    // ------------------------------------------------------------------ P1a: 0x9EEDA4

    [Fact]
    public void M6_026_P1a_TheBehaviourCodeIsTheLowNibbleOfTheFoundNodesByte3()
    {
        // P1a: climb from the node while [node+0x45]&0x10 == 0 (advanced byte0 bit 4) and a parent exists; return [found+0x59]&0xF (byte 3, low nibble),
        // out = ([found+0x58]>>3)&7 (byte 1 & 7).
        var g = new Graph(
            Snd(1, P(parent: 2, adv3: 0x2F, adv1: 0x0B)),                    // no bit 4: not the found node
            Mix(2, P(parent: 3, adv0: 0x10, adv3: 0x1D, adv1: 0x0E)),        // bit 4: found
            Mix(3, P(adv3: 0x05)));
        var l = Limiter(g);
        Assert.Equal(0xD, l.BehaviourCode9EEDA4(g[1], out int idx));
        Assert.Equal(0x0E & 7, idx);
        Assert.Equal(0xD, l.BehaviourCode9EEDA4(g[2], out _));                // a node with the bit answers for itself
    }

    [Fact]
    public void M6_026_P1a_WithNoBit4AnywhereTheRootAnswers()
    {
        var g = new Graph(Snd(1, P(parent: 2, adv3: 0x2F)), Mix(2, P(adv3: 0x03, adv1: 0x05)));
        Assert.Equal(3, Limiter(g).BehaviourCode9EEDA4(g[1], out int idx));
        Assert.Equal(5, idx);
    }

    [Fact]
    public void M6_026_C28_8_EveryShippedSoundsBehaviourCodeIsZero()
    {
        // C28.8: advanced-settings byte 4 (the vt+0x130 argument, the fifth byte) is 0 in all 2886 nodes, so 0x9EEDA4 returns 0 for every Sound (P1a).
        var lib = RequireLibrary();
        var l = new WwisePlaybackLimiter(lib.Node);
        int checkedSounds = 0;
        foreach (uint id in lib.AllNodeIds)
        {
            if (lib.Node(id) is not WwiseSoundNode s) continue;
            Assert.Equal(0, l.BehaviourCode9EEDA4(s, out _));
            checkedSounds++;
        }
        Assert.Equal(2360, checkedSounds);                                    // the census: 2360 Sounds
    }

    // ------------------------------------------------------------------ W6, L1..L6: the count and the limiter object

    [Fact]
    public void M6_026_W6_L2_L3_L6_TheFirstWalkCreatesTheChainAndCountsEveryNode()
    {
        // S(1) -> M(2, bus 10, max 1, global) ; bus 10 -> bus 11.
        var g = new Graph(
            Snd(1, P(parent: 2)),
            Mix(2, P(bus: 10, adv0: 0x04, max: 1)),
            Bus(10, 11), Bus(11, 0));
        var l = Limiter(g);
        var pbi = Pbi(50f);
        var block = Block(pbi.LimiterArray1EC);

        int r = l.Walk(g[1], block, count: true, skipGlobal: false);

        Assert.Equal(1, r);                                                                // W5: 1, 2 or 0x50; first play under max 1 gives 1
        // L6: vt+0x11C creates the bus and parent limiters at the node's creation; the bus chain follows.
        foreach (uint id in new uint[] { 1, 2, 10, 11 }) Assert.NotNull(l.LimiterOf(id));
        // W6: (u16)+0x60++ for every walk; +0x62++ when block+0xC bit0 (no bus-carrying node passed yet):
        // S and M (at and below the first bus-carrying node, M) count, and so do both buses (isBus = 1).
        Assert.Equal((short)1, l.LimiterOf(1)!.Count60);
        Assert.Equal((short)1, l.LimiterOf(1)!.Count62);
        Assert.Equal((short)1, l.LimiterOf(2)!.Count60);
        Assert.Equal((short)1, l.LimiterOf(2)!.Count62);
        Assert.Equal((short)1, l.LimiterOf(10)!.Count60);
        Assert.Equal((short)1, l.LimiterOf(10)!.Count62);
        Assert.Equal((short)1, l.LimiterOf(11)!.Count60);
        // W4: the bus recursion clears block+0xC bit 0.
        Assert.Equal(2, block.Word0C);
        // W7: a bus whose +0x60 is 1 gets [bus+0xCC] bits 0..2 = 1.
        Assert.Equal(1, l.BusCC(10) & 7);
        Assert.Equal(1, l.BusCC(11) & 7);
        // G2: the mixer's list (max 1, global) is appended to the PBI's array, grown to cap+3.
        Assert.Same(l.LimiterOf(2)!.List, Assert.Single(pbi.LimiterArray1EC.Items));
        Assert.Equal(3, pbi.LimiterArray1EC.Capacity);
    }

    [Fact]
    public void M6_026_L2_TheLimiterObjectFieldsComeFromTheAdvancedSettings()
    {
        // L2 / B8: +0x44 = u16 & 0x3FF; +0x46 = byte0 bit 0 (KillNewest); +0x47 = bit 1 (virtual); +0x68 bit0 = bit 2 (global).
        var g = new Graph(
            Mix(1, P(adv0: 0x07, max: 0xFC05)),                              // the top 6 bits of the u16 are not the max
            Mix(2, P(adv0: 0x00, max: 5)));
        var l = Limiter(g);
        l.Count9FAC54(g[1], isBus: false);
        l.Count9FAC54(g[2], isBus: false);
        var a = l.LimiterOf(1)!;
        Assert.Equal((ushort)5, a.List.Max);
        Assert.True(a.List.KillNewest);
        Assert.True(a.List.Virtual);
        Assert.True(a.Global68);
        var b = l.LimiterOf(2)!;
        Assert.Equal((ushort)5, b.List.Max);
        Assert.False(b.List.KillNewest);
        Assert.False(b.List.Virtual);
        Assert.False(b.Global68);
    }

    [Fact]
    public void M6_026_L3_5_1_TheKeyIsTheNodeIdAndTheAncestorCountWithTheCategoryFlag()
    {
        // L3: lo = node id; hi = number of ancestors | 0x40000000; 5.1: | 0x20000000 instead when [node+0x46] bit2, i.e. the category is a bus (0, 0xA, 0xC); the five
        // Sound-path classes (categories 3, 2, 4, 1, 5) have it clear. A bus has no [node+0x34] parent, so its count is 0.
        var g = new Graph(Snd(1, P(parent: 2, bus: 10)), Mix(2, P(parent: 3)), Mix(3, P()), Bus(10, 11), Bus(11, 0));
        var l = Limiter(g);
        l.Count9FAC54(g[1], false);
        Assert.Equal(1u, l.LimiterOf(1)!.List.KeyLo);
        Assert.Equal(0x40000002u, l.LimiterOf(1)!.List.KeyHi);
        Assert.Equal(0x40000001u, l.LimiterOf(2)!.List.KeyHi);
        Assert.Equal(0x40000000u, l.LimiterOf(3)!.List.KeyHi);
        Assert.Equal(10u, l.LimiterOf(10)!.List.KeyLo);
        Assert.Equal(0x20000000u, l.LimiterOf(10)!.List.KeyHi);
        Assert.Equal(0x20000000u, l.LimiterOf(11)!.List.KeyHi);
    }

    [Fact]
    public void M6_026_5_1_TheCategoryOfEachClass()
    {
        Assert.False(WwisePlaybackLimiter.Node46Bit2(Snd(1, P())));                  // Sound 3
        Assert.False(WwisePlaybackLimiter.Node46Bit2(Mix(1, P())));                  // ActorMixer 1
        Assert.True(WwisePlaybackLimiter.Node46Bit2(Bus(1, 0)));                     // Bus 0
        // RanSeq 2, Switch 4 and Layer 5 are also clear; a music class has no category in the inventory.
        var segment = new WwiseMusicSegmentNode(9, "t.bnk", P(), Array.Empty<uint>(), 0, default, 0, Array.Empty<(uint, double, string)>());
        Assert.Throws<WwiseMissingBehaviourException>(() => WwisePlaybackLimiter.Node46Bit2(segment));
    }

    [Fact]
    public void M6_026_L1_W6_AnAllocationFailureLeavesNoLimiterAndReturns2()
    {
        // L1: allocation failure -> [node+0x30] = 0, return 0. W6: the limiter still does not exist -> 2.
        var g = new Graph(Snd(1, P()));
        var l = Limiter(g);
        l.AllocationFails = () => true;
        Assert.Equal(2, l.Count9FAC54(g[1], isBus: false));
        Assert.Null(l.LimiterOf(1));
    }

    [Fact]
    public void M6_026_W6_AJustCreatedLimiterWhoseChainFailedReturns2OnlyWithoutTheBusFlag()
    {
        // W6: "if it was just created, 0x9F29E8 returned 0 and isBus == 0" -> 2, else 1. L6: 0x9F29E8 returns 0 when the parent's creation failed.
        // Allocation sequence: the node's limiter succeeds, its parent's fails.
        var g = new Graph(Snd(1, P(parent: 2)), Mix(2, P()));
        foreach (bool isBus in new[] { false, true })
        {
            var l = Limiter(g);
            int calls = 0;
            l.AllocationFails = () => ++calls == 2;
            int r = l.Count9FAC54(g[1], isBus);
            Assert.Equal(isBus ? 1 : 2, r);
            Assert.NotNull(l.LimiterOf(1));                                   // created, counted
            Assert.Null(l.LimiterOf(2));
            Assert.Equal((short)1, l.LimiterOf(1)!.Count60);                  // the counts were made either way
        }
    }

    [Fact]
    public void M6_026_W6_ACountWithNoBusFlagDoesNotTouchPlus62()
    {
        var g = new Graph(Snd(1, P()));
        var l = Limiter(g);
        l.Count9FAC54(g[1], isBus: false);
        Assert.Equal((short)1, l.LimiterOf(1)!.Count60);
        Assert.Equal((short)0, l.LimiterOf(1)!.Count62);
    }

    [Fact]
    public void M6_026_L4_ARtpcOn0x10WithANonzeroMaxIsAVisibleStop()
    {
        // L4/G1: RTPC parameter 0x10 with a bank max != 0 needs 0xA11590 -> 0xA17724/0xA17878 (RECOVERABLE_GAP); no shipped node has one (C28.8).
        var rtpc = new WwiseRtpc(1, 0, 0, 0x10, 0, 0, Array.Empty<(float, float, uint)>());
        var g = new Graph(Mix(1, P(adv0: 4, max: 1, rtpcs: new[] { rtpc })));
        Assert.Throws<WwiseMissingBehaviourException>(() => Limiter(g).Count9FAC54(g[1], false));
    }

    [Fact]
    public void M6_026_L5_ALimiterWithANonzeroMaxSubscribesToRtpcChangesOnMask0x10000()
    {
        // L5: if max != 0, 0xA19ECC(obj+0x10, node, {0x10000, 0}, 1); [this+0x20] = node. Max 0: no call.
        var g = new Graph(Mix(1, P(adv0: 4, max: 1)), Mix(2, P(adv0: 4, max: 0)));
        var l = Limiter(g);
        var calls = new List<(string, uint, ulong)>();
        l.RtpcSubscribeA19ECC = (kind, node, mask) => calls.Add((kind, node.Id, mask));
        l.Count9FAC54(g[1], false);
        l.Count9FAC54(g[2], false);
        var call = Assert.Single(calls);
        Assert.Equal(("limiter", 1u, 0x10000UL), call);
        Assert.Equal(1u, l.LimiterOf(1)!.SubscriberNode20);
        Assert.Null(l.LimiterOf(2)!.SubscriberNode20);
    }

    // ------------------------------------------------------------------ W2..W4: the walk order and the merge

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 0x50, 0x50)]      // "0x50 turns r5==1 into 0x50"
    [InlineData(2, 0x50, 2)]         // a 2 stays
    [InlineData(0x50, 0x50, 0x50)]
    [InlineData(0x50, 1, 0x50)]      // "1 keeps r5"
    [InlineData(2, 1, 2)]
    [InlineData(1, 2, 2)]            // "anything else replaces r5"
    [InlineData(0x50, 2, 2)]
    public void M6_026_W4_TheMergeRule(int r5, int child, int expected)
        => Assert.Equal(expected, WwisePlaybackLimiter.Merge(r5, child));

    [Fact]
    public void M6_026_W4_ABusIsVisitedOnlyForTheFirstBusCarryingNode()
    {
        // W4: the bus recursion runs only while block+0xC bit 0 is set and clears it, so the parent's own bus is not walked.
        var g = new Graph(
            Snd(1, P(parent: 2, bus: 10)), Mix(2, P(bus: 12)), Bus(10, 0), Bus(12, 0));
        var l = Limiter(g);
        var pbi = Pbi(50f);
        l.Walk(g[1], Block(pbi.LimiterArray1EC), true, false);
        Assert.Equal((short)1, l.LimiterOf(10)!.Count60);
        Assert.True(l.LimiterOf(12) is null || l.LimiterOf(12)!.Count60 == 0);
        // The parent M is above the first bus-carrying node, so its +0x62 was not incremented (bit0 already cleared).
        Assert.Equal((short)1, l.LimiterOf(1)!.Count62);
        Assert.Equal((short)0, l.LimiterOf(2)!.Count62);
        Assert.Equal((short)1, l.LimiterOf(2)!.Count60);
    }

    [Fact]
    public void M6_026_W3_ABlock0x10ByteSkipsTheLimitCheckOfEveryAncestor()
    {
        // W3: block+0x10 != 0 -> r5 = 1, no check; block+0x10 = [node+0x47] bit 6 (advanced byte0 bit 3) after a node's own check, restored for the parent.
        var g = new Graph(
            Snd(1, P(parent: 2, adv0: 0x08)),                                // bit 3: stop further checks
            Mix(2, P(adv0: 0x04, max: 1)));
        var l = Limiter(g);
        var existing = Pbi(50f);
        l.Count9FAC54(g[2], false);
        l.InsertIntoList9F3274(l.LimiterOf(2)!.List, existing);              // the mixer is already full
        var pbi = Pbi(50f);
        var block = Block(pbi.LimiterArray1EC);
        Assert.Equal(1, l.Walk(g[1], block, true, false));
        Assert.Empty(pbi.LimiterArray1EC.Items);                             // the mixer's check never ran: nothing appended
        Assert.Equal(0, block.Count0E);
        Assert.Equal(0, existing.Flags1BD & 2);                              // and nothing was killed
    }

    [Fact]
    public void M6_026_W3_TheSkipGlobalFlagSkipsTheGlobalCheckOnly()
    {
        // W3: r3 != 0 -> r5 = 1, skip (the global variant), for a node whose limiter is global.
        var g = new Graph(Mix(1, P(adv0: 0x04, max: 1)));
        var l = Limiter(g);
        var pbi = Pbi(50f);
        Assert.Equal(1, l.Walk(g[1], Block(pbi.LimiterArray1EC), count: true, skipGlobal: true));
        Assert.Empty(pbi.LimiterArray1EC.Items);
        var pbi2 = Pbi(50f);
        Assert.Equal(1, l.Walk(g[1], Block(pbi2.LimiterArray1EC), count: true, skipGlobal: false));
        Assert.Single(pbi2.LimiterArray1EC.Items);
    }

    [Fact]
    public void M6_026_W2_W3_WithoutTheCountFlagTheCheckStillRunsAndAMissingLimiterTakesTheGlobalPath()
    {
        // W2: r2 == 0 -> r5 = 1 with no count; W3: L == 0 -> the global variant (r3 == 0). 0x9FA01C creates the limiter itself (G1).
        var g = new Graph(Mix(1, P(adv0: 0x04, max: 1)));
        var l = Limiter(g);
        var pbi = Pbi(50f);
        Assert.Equal(1, l.Walk(g[1], Block(pbi.LimiterArray1EC), count: false, skipGlobal: false));
        Assert.NotNull(l.LimiterOf(1));
        Assert.Equal((short)0, l.LimiterOf(1)!.Count60);                     // no count
        Assert.Single(pbi.LimiterArray1EC.Items);                            // but the list was appended
    }

    [Fact]
    public void M6_026_W2_ACountFailureSkipsTheCheckAndCarries2()
    {
        // W2: 0x9FAC54 != 1 -> the limit check is skipped and r5 = 2 is carried to the merge.
        var g = new Graph(Mix(1, P(adv0: 0x04, max: 1)));
        var l = Limiter(g);
        l.AllocationFails = () => true;                                      // the limiter object cannot be allocated
        var pbi = Pbi(50f);
        Assert.Equal(2, l.Walk(g[1], Block(pbi.LimiterArray1EC), true, false));
        Assert.Empty(pbi.LimiterArray1EC.Items);
    }

    [Fact]
    public void M6_026_W4_AParentsResultIsMergedIntoTheChilds()
    {
        // W4: a parent's 0x50 turns the child's 1 into 0x50; the parent (M, virtual, max 1) holds a higher-priority PBI, so the new priority is rejected (V3).
        var g = new Graph(Snd(1, P(parent: 2)), Mix(2, P(adv0: 0x06, max: 1)));       // bit1 virtual, bit2 global
        var l = Limiter(g);
        l.Count9FAC54(g[2], false);
        l.InsertIntoList9F3274(l.LimiterOf(2)!.List, Pbi(60f));
        var pbi = Pbi(50f);
        Assert.Equal(0x50, l.Walk(g[1], Block(pbi.LimiterArray1EC, 50f), true, false));
        // and a parent's 2 replaces the child's 1
        var g2 = new Graph(Snd(1, P(parent: 2)), Mix(2, P(adv0: 0x05, max: 1)));       // bit0 KillNewest, bit2 global
        var l2 = Limiter(g2);
        l2.Count9FAC54(g2[2], false);
        l2.InsertIntoList9F3274(l2.LimiterOf(2)!.List, Pbi(50f));
        Assert.Equal(2, l2.Walk(g2[1], Block(Pbi(50f).LimiterArray1EC, 50f), true, false));
    }

    [Fact]
    public void M6_026_G3_OnlyOneVictimCheckRunsPerWalkForTheGlobalVariant()
    {
        // G3: [block+0xE] == 0 is required and 0xA37100's caller then does [block+0xE]++, so a second global check in the same walk is skipped.
        var g = new Graph(Snd(1, P(parent: 2, adv0: 0x04, max: 1)), Mix(2, P(adv0: 0x04, max: 1)));
        var l = Limiter(g);
        l.Count9FAC54(g[1], false);
        l.Count9FAC54(g[2], false);
        var childOld = Pbi(50f);
        var parentOld = Pbi(50f);
        l.InsertIntoList9F3274(l.LimiterOf(1)!.List, childOld);
        l.InsertIntoList9F3274(l.LimiterOf(2)!.List, parentOld);
        var block = Block(Pbi(50f).LimiterArray1EC, 50f);
        Assert.Equal(1, l.Walk(g[1], block, true, false));
        Assert.Equal(1, block.Count0E);
        Assert.NotEqual(0, childOld.Flags1BD & 2);                           // the child's check killed its oldest
        Assert.Equal(0, parentOld.Flags1BD & 2);                             // the parent's check did not run
    }

    // ------------------------------------------------------------------ G1..G3: 0x9FA01C

    [Fact]
    public void M6_026_G1_AMaxOfZeroCreatesTheLimiterAndReturns1WithoutAppending()
    {
        // G1: max == 0 -> return 1 (after the creation, whose result is unchecked); nothing is appended (G2 comes after).
        var g = new Graph(Mix(1, P(adv0: 0x04, max: 0)));
        var l = Limiter(g);
        var pbi = Pbi(50f);
        Assert.Equal(1, l.Walk(g[1], Block(pbi.LimiterArray1EC), count: false, skipGlobal: false));
        Assert.NotNull(l.LimiterOf(1));
        Assert.Empty(pbi.LimiterArray1EC.Items);
    }

    [Fact]
    public void M6_026_G3_TheCheckRunsOnlyWhenMaxIsAtMostCountMinusVirtual()
    {
        // G3: max <= (u16)+0x58 - (u16)+0x5A. Boundary: max 2 with one PBI (1): no check; with two: check; with two of which one virtual: no check.
        var g = new Graph(Mix(1, P(adv0: 0x04, max: 2)));
        var l = Limiter(g);
        l.Count9FAC54(g[1], false);
        var list = l.LimiterOf(1)!.List;
        var a = Pbi(50f); var b = Pbi(50f);
        l.InsertIntoList9F3274(list, a);
        var block1 = Block(Pbi(50f).LimiterArray1EC);
        Assert.Equal(1, l.Walk(g[1], block1, true, false));
        Assert.Equal(0, block1.Count0E);                                     // 2 <= 1 is false: no 0xA37100
        l.InsertIntoList9F3274(list, b);
        list.Virtual22 = 1;                                                  // one of the two counts as virtual: 2 <= 2 - 1 is false
        var block2 = Block(Pbi(50f).LimiterArray1EC);
        l.Walk(g[1], block2, true, false);
        Assert.Equal(0, block2.Count0E);
        list.Virtual22 = 0;                                                  // 2 <= 2: the check runs
        var block3 = Block(Pbi(50f).LimiterArray1EC);
        l.Walk(g[1], block3, true, false);
        Assert.Equal(1, block3.Count0E);
    }

    [Fact]
    public void M6_026_G3_ABlock0x11ByteOfZeroSkipsTheCheck()
    {
        var g = new Graph(Mix(1, P(adv0: 0x05, max: 1)));
        var l = Limiter(g);
        l.Count9FAC54(g[1], false);
        l.InsertIntoList9F3274(l.LimiterOf(1)!.List, Pbi(50f));
        var block = Block(Pbi(50f).LimiterArray1EC, 50f, b11: 0);
        Assert.Equal(1, l.Walk(g[1], block, true, false));                    // KillNewest with an equal priority would have been 2
        Assert.Equal(0, block.Count0E);
    }

    [Fact]
    public void M6_026_G2_TheArrayGrowsByThreeAndAnAllocationFailureSkipsTheAppend()
    {
        // G2: at count >= cap the array grows to cap+3; a failed allocation skips the append and the function goes on.
        var g = new Graph(Mix(1, P(adv0: 0x04, max: 1)), Mix(2, P(adv0: 0x04, max: 1)),
            Mix(3, P(adv0: 0x04, max: 1)), Mix(4, P(adv0: 0x04, max: 1)));
        var l = Limiter(g);
        var pbi = Pbi(50f);
        var block = Block(pbi.LimiterArray1EC);
        foreach (uint id in new uint[] { 1, 2, 3, 4 }) l.Walk(g[id], block, true, false);
        Assert.Equal(4, pbi.LimiterArray1EC.Count);
        Assert.Equal(6, pbi.LimiterArray1EC.Capacity);                       // 0 -> 3 -> 6

        var l2 = Limiter(g);
        var pbi2 = Pbi(50f);
        l2.Count9FAC54(g[1], false);                                         // create first so only the append allocates
        l2.AllocationFails = () => true;
        Assert.Equal(1, l2.Walk(g[1], Block(pbi2.LimiterArray1EC), count: false, skipGlobal: false));
        Assert.Empty(pbi2.LimiterArray1EC.Items);
    }

    // ------------------------------------------------------------------ V1..V4: 0xA37100

    private static (WwisePlaybackLimiter L, WwisePbiList List, WwisePlayingInstance[] Pbis) ListOf(bool killNewest, ushort max, params float[] prios)
    {
        var g = new Graph();
        var l = Limiter(g);
        var list = new WwisePbiList { Max = max, KillNewest = killNewest };
        var pbis = prios.Select(p => Pbi(p)).ToArray();
        foreach (var p in pbis) l.InsertIntoList9F3274(list, p);
        return (l, list, pbis);
    }

    [Fact]
    public void M6_026_V1_ANullListReturns1AndAnEmptyListWithMaxAboveZeroReturns1()
    {
        var l = Limiter(new Graph());
        Assert.Equal(1, l.Victim0A37100(null, 1, 50f, null, false, false, 1));       // V1: list == 0 -> 1
        var empty = new WwisePbiList { Max = 1 };
        Assert.Equal(1, l.Victim0A37100(empty, 1, 50f, null, false, false, 1));      // V3: max > count (1 > 0) -> 1
    }

    [Fact]
    public void M6_026_V3_AMaxOfZeroWithNoCandidateRejectsWith2()
    {
        // V1: an empty list gives count 0, cand 0. V3: max > count is 0 > 0 = false; cand == 0 -> reject, 2 (not virtual). (0x9FA01C returns 1 for max 0 before this, G1.)
        var l = Limiter(new Graph());
        Assert.Equal(2, l.Victim0A37100(new WwisePbiList(), 0, 50f, null, false, false, 1));
    }

    [Fact]
    public void M6_026_V3_TheBoundaryOfMaxAgainstTheEligibleCount()
    {
        // V3: max > count -> 1 (no kill). max 3 with two PBIs: 3 > 2. With three PBIs: 3 > 3 is false -> a kill (equal priority, KillNewest 0).
        var (l, list, pbis) = ListOf(false, 3, 50f, 50f);
        Assert.Equal(1, l.Victim0A37100(list, 3, 50f, null, false, false, 1));
        Assert.All(pbis, p => Assert.Equal(0, p.Flags1BD & 2));
        var (l3, list3, pbis3) = ListOf(false, 3, 50f, 50f, 50f);
        Assert.Equal(1, l3.Victim0A37100(list3, 3, 50f, null, false, false, 1));
        Assert.Equal(1, pbis3.Count(p => (p.Flags1BD & 2) != 0));
    }

    [Fact]
    public void M6_026_V1_V3_ALowerPriorityNewcomerFailsWith2AndAHigherOneKills()
    {
        // V1: cand = the last PBI with prio >= pbi.1C0 (the new priority is not below it). A newcomer below every PBI has no candidate -> reject 2 (V3).
        var (l, list, pbis) = ListOf(false, 1, 50f);
        Assert.Equal(2, l.Victim0A37100(list, 1, 40f, null, false, false, 1));
        Assert.Equal(0, pbis[0].Flags1BD & 2);
        Assert.Equal(1, l.Victim0A37100(list, 1, 60f, null, false, false, 1));
        Assert.NotEqual(0, pbis[0].Flags1BD & 2);
    }

    [Fact]
    public void M6_026_V4_TheVictimIsTheLowestPriorityPbiAtOrBelowTheNewOne()
    {
        // C28.1/V4: the list is descending; the victim is the last eligible PBI with priority <= the new one. PBIs 70, 60, 50; new 65 -> 60 and 50 qualify, the last is 50.
        var (l, list, pbis) = ListOf(false, 3, 70f, 60f, 50f);
        Assert.Equal(new[] { 70f, 60f, 50f }, list.Items.Select(p => p.Priority1C0).ToArray());
        Assert.Equal(1, l.Victim0A37100(list, 3, 65f, null, false, false, 1));
        Assert.Equal(0, pbis[0].Flags1BD & 2);
        Assert.Equal(0, pbis[1].Flags1BD & 2);
        Assert.NotEqual(0, pbis[2].Flags1BD & 2);
    }

    [Fact]
    public void M6_026_V3_AnEqualPriorityKillsTheOldestUnlessKillNewestRejectsTheNewcomer()
    {
        // C28.1: KillNewest == 0: the list holds the newest first among equal priorities, so the last is the oldest and it is killed.
        var (l, list, pbis) = ListOf(false, 2, 50f, 50f, 50f);
        Assert.Equal(pbis[2], list.Items[0]);                                        // newest first
        Assert.Equal(pbis[0], list.Items[2]);                                        // oldest last
        Assert.Equal(1, l.Victim0A37100(list, 2, 50f, null, false, false, 1));
        Assert.NotEqual(0, pbis[0].Flags1BD & 2);
        Assert.Equal(0, pbis[1].Flags1BD & 2);
        Assert.Equal(0, pbis[2].Flags1BD & 2);
        // V3: prio == s17 && killNewest -> reject 2, nothing killed.
        var (lk, listk, pbisk) = ListOf(true, 2, 50f, 50f, 50f);
        Assert.Equal(2, lk.Victim0A37100(listk, 2, 50f, null, true, false, 1));
        Assert.All(pbisk, p => Assert.Equal(0, p.Flags1BD & 2));
    }

    [Fact]
    public void M6_026_V3_KillNewestWithAHigherPriorityNewcomerStillKills()
    {
        var (l, list, pbis) = ListOf(true, 1, 50f);
        Assert.Equal(1, l.Victim0A37100(list, 1, 51f, null, true, false, 1));
        Assert.NotEqual(0, pbis[0].Flags1BD & 2);
    }

    [Fact]
    public void M6_026_V1_ADeadOrMarkedPbiIsNotCounted()
    {
        // V1: skip a PBI if 1BD & 2, or 1BE & 0x2C. Two dead PBIs leave count 1 (< max 2) -> return 1 with no kill.
        var (l, list, pbis) = ListOf(false, 2, 50f, 50f, 50f);
        pbis[0].Flags1BD |= 2;                                                       // already dead
        pbis[1].Flags1BE |= 0x20;                                                    // virtual-counted bit
        Assert.Equal(1, l.Victim0A37100(list, 2, 50f, null, false, false, 1));
        Assert.Equal(0, pbis[2].Flags1BD & 2);
        foreach (byte bit in new byte[] { 0x04, 0x08, 0x20 })
        {
            var (l2, list2, p2) = ListOf(false, 2, 50f, 50f);
            p2[0].Flags1BE |= bit;                                                   // 0x2C = bits 2, 3, 5
            Assert.Equal(1, l2.Victim0A37100(list2, 2, 50f, null, false, false, 1)); // count 1 < 2
            Assert.Equal(0, p2[1].Flags1BD & 2);
        }
    }

    [Fact]
    public void M6_026_V1_ANaNPriorityNeverQualifies()
    {
        // V1: "vcmp/vmovge; NaN false": a NaN newcomer has no candidate.
        var (l, list, pbis) = ListOf(false, 1, 50f);
        Assert.Equal(2, l.Victim0A37100(list, 1, float.NaN, null, false, false, 1));
        Assert.Equal(0, pbis[0].Flags1BD & 2);
    }

    [Fact]
    public void M6_026_V1_ThePerObjectVariantOnlyCountsPbisOfTheGameObject()
    {
        // V1: with go != 0 a PBI whose [pbi+0x14] != go is skipped (not counted).
        var g = new Graph();
        var l = Limiter(g);
        var list = new WwisePbiList { Max = 1 };
        var other = Pbi(50f, go: 8);
        var mine = Pbi(50f, go: 7);
        l.InsertIntoList9F3274(list, other);
        l.InsertIntoList9F3274(list, mine);
        Assert.Equal(1, l.Victim0A37100(list, 1, 50f, 7, false, false, 1));
        Assert.Equal(0, other.Flags1BD & 2);
        Assert.NotEqual(0, mine.Flags1BD & 2);
    }

    [Fact]
    public void M6_026_V1_5_3_AGlobalScopePbiHasGameObjectZeroAndIsNotCountedForAnObject()
    {
        // 5.3: [pbi+0x14] is the registered object of an object-scope action and 0 otherwise; V1 skips a PBI whose word differs from go. A PBI made with
        // params.GameObjectId == null (global scope) carries 0, so it is not counted for object 7.
        var l = Limiter(new Graph());
        var list = new WwisePbiList { Max = 1 };
        var globalScope = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 5, TargetNodeId = 1 }, 1, TestSource, new byte[0x44], null, false) { Priority1C0 = 50f };
        Assert.Null(globalScope.GameObject14);
        l.InsertIntoList9F3274(list, globalScope);
        Assert.Equal(1, l.Victim0A37100(list, 1, 50f, 7, false, false, 1));          // count 0: max 1 > 0
        Assert.Equal(0, globalScope.Flags1BD & 2);
        var scoped = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 6, TargetNodeId = 1, GameObjectId = 7 }, 1, TestSource, new byte[0x44], null, false);
        Assert.Equal(7u, scoped.GameObject14);                                       // 0xA00104/0xA00144: the ctor copies [params+8]
    }

    [Fact]
    public void M6_026_V2_TheVirtualPassUsesTheNextSourceCodeAndRejectsWith0x50()
    {
        // V2/V3: virtual list. A PBI above the newcomer is skipped (count 1, no candidate, ip 0): prio < 101 -> return 0x50 (virtual reject).
        var (l, list, pbis) = ListOf(false, 1, 60f);
        l.NextSourceCodeA01768 = _ => throw new InvalidOperationException("the code is asked only for a PBI at or below the newcomer");
        Assert.Equal(0x50, l.Victim0A37100(list, 1, 50f, null, false, true, 1));
        // c != 0 makes the PBI the candidate with that code; code != 1 -> return 1 without a kill (V3).
        l.NextSourceCodeA01768 = _ => 2;
        Assert.Equal(1, l.Victim0A37100(list, 1, 60f, null, false, true, 1));
        Assert.Equal(0, pbis[0].Flags1BD & 2);
        // code 1 -> the kill.
        l.NextSourceCodeA01768 = _ => 1;
        Assert.Equal(1, l.Victim0A37100(list, 1, 60f, null, false, true, 1));
        Assert.NotEqual(0, pbis[0].Flags1BD & 2);
    }

    [Fact]
    public void M6_026_V2_AZeroCodeSetsIpWhenTheCountIsWithinMax()
    {
        // V2: c == 0 -> ip = 1 if count <= max. With ip set, a reject (prio < s17 = 101, no candidate) returns 1 instead of 0x50 (V3).
        var (l, list, _) = ListOf(false, 1, 50f);
        l.NextSourceCodeA01768 = _ => 0;
        Assert.Equal(1, l.Victim0A37100(list, 1, 50f, null, false, true, 1));
    }

    [Fact]
    public void M6_026_V3_TheReasonArgumentIsWrittenIntoBits2To4()
    {
        // K1: pbi.1BD bits 2..4 = reason & 7 and bit 1 set. The ctor's 0x44 (0xA00288) keeps bit 6; bits 2..4 are replaced.
        foreach (int reason in new[] { 1, 2, 3, 7 })
        {
            var (l, list, pbis) = ListOf(false, 1, 50f);
            l.Victim0A37100(list, 1, 50f, null, false, false, reason);
            Assert.Equal((0x40 | (reason << 2) | 2), pbis[0].Flags1BD);
        }
    }

    // ------------------------------------------------------------------ K1..K6: 0xA01CA4 and 0x9FF7B8

    [Fact]
    public void M6_026_K1_K3_APbiWithNoFadeStateIsMarkedAndStoppedThroughRouteB()
    {
        // K1: reason in bits 2..4 and bit 1, once. K3: 1BC |= 0x40; 1BA & 0x78 == 0 -> vt+0(pbi,0,1). K6 (0x9FF7B8): 1BC bit5 set, bit1 cleared, bit3 set.
        var l = Limiter(new Graph());
        var pbi = Pbi(50f);
        pbi.Flags1BC = 0x02;                                                         // bit1 set so its clearing is visible
        l.Kill0A01CA4(pbi, 1);
        Assert.Equal(0x46, pbi.Flags1BD);
        Assert.Equal(0x40 | 0x20 | 0x08, pbi.Flags1BC);                              // 0x40 (K3), 0x20 and 0x08 set, 0x02 cleared (K6)
        Assert.Equal(1f, pbi.Fade168);                                               // route (b) does not touch +0x168/+0x40
        Assert.Equal(1f, pbi.MuteFade40);
    }

    [Fact]
    public void M6_026_K1_ADeadMarkedPbiKeepsItsOldReason()
    {
        // K1: "an already dead-marked PBI keeps its old reason".
        var l = Limiter(new Graph());
        var pbi = Pbi(50f);
        l.Kill0A01CA4(pbi, 1);
        l.Kill0A01CA4(pbi, 3);
        Assert.Equal(0x46, pbi.Flags1BD);
    }

    [Fact]
    public void M6_026_K2_APausedPbiTakesRouteAWithoutTheFadeBit()
    {
        // K2: 1BC & 0x80 -> vt+0(pbi,0,0) at once; 1BC bit 6 (K3) is not set.
        var l = Limiter(new Graph());
        var pbi = Pbi(50f);
        pbi.Flags1BC = 0x80;
        pbi.Flags1BA = 0x18;
        l.Kill0A01CA4(pbi, 1);
        Assert.Equal(0x80 | 0x20 | 0x08, pbi.Flags1BC);
        Assert.Equal(1f, pbi.Fade168);
    }

    [Theory]
    [InlineData(0x02000000u, true)]      // 0xA35980: (id - 0x02000000) & ~0x02000000 == 0
    [InlineData(0x04000000u, true)]
    [InlineData(0x01000000u, false)]     // the Play fade-in item id (C28 K5 note) is not a stop item
    [InlineData(0x06000000u, false)]
    [InlineData(0u, false)]
    public void M6_026_K2_TheStopItemPredicate(uint id, bool expected)
        => Assert.Equal(expected, WwisePlaybackLimiter.IsStopOrPauseItem0A35980(id));

    [Fact]
    public void M6_026_K2_AStopItemAt0x148TakesRouteA()
    {
        var l = Limiter(new Graph());
        var pbi = Pbi(50f);
        pbi.Item148Id = 0x02000000;
        pbi.Flags1BA = 0x18;
        var cancels = new List<int>();
        l.CancelTransitionA36618 = (_, off) => cancels.Add(off);
        l.Kill0A01CA4(pbi, 1);
        Assert.Equal(0x20 | 0x08, pbi.Flags1BC);                                     // no 0x40: route (a)
        Assert.Equal(new[] { 0x148 }, cancels);                                      // K6: the item at +0x148 is cancelled and cleared
        Assert.Null(pbi.Item148Id);
    }

    [Fact]
    public void M6_026_K4_ARunningTransitionIsReaimedToTheStopItemWithZeroDuration()
    {
        // K4: 1BA & 0x78 != 0 and [pbi+0x144] != 0 -> 0xA366F4(M, item, id 0x02000000, end 0.0f, time 0, curve 4, mode 0); then return (no vt+0).
        var l = Limiter(new Graph());
        var pbi = Pbi(50f);
        pbi.Flags1BA = 0x18;
        pbi.Field144 = 0x1234;
        (WwisePlayingInstance, uint, float, uint, int, int)? call = null;
        l.ReaimTransitionA366F4 = (p, id, end, time, curve, mode) => call = (p, id, end, time, curve, mode);
        l.Kill0A01CA4(pbi, 1);
        Assert.Equal((pbi, 0x02000000u, 0f, 0u, 4, 0), call);
        Assert.Equal(0x40, pbi.Flags1BC);                                            // 0x40 only: vt+0 is not called yet
        Assert.Equal(0x1234u, pbi.Field144);
        // Without the seam the re-aim is a visible stop.
        var l2 = Limiter(new Graph());
        var pbi2 = Pbi(50f);
        pbi2.Flags1BA = 0x18;
        pbi2.Field144 = 1;
        Assert.Throws<WwiseMissingBehaviourException>(() => l2.Kill0A01CA4(pbi2, 1));
    }

    [Fact]
    public void M6_026_K5_WithNoTransitionTheFadeValuesAreZeroedAndThePbiStopped()
    {
        // K5: [pbi+0x144] == 0: pbi+0x168 = 0.0f, pbi+0x40 = 0.0f, vt+0(pbi,0,0).
        var l = Limiter(new Graph());
        var pbi = Pbi(50f);
        pbi.Flags1BA = 0x18;
        l.Kill0A01CA4(pbi, 2);
        Assert.Equal(0, BitConverter.SingleToInt32Bits(pbi.Fade168));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(pbi.MuteFade40));
        Assert.Equal(0x40 | 0x20 | 0x08, pbi.Flags1BC);
        Assert.Equal(0x4A, pbi.Flags1BD);                                            // reason 2
    }

    [Fact]
    public void M6_026_K6_MarkStoppedIsIdempotentAndCancelsTheTransitionItems()
    {
        // K6: 1BC & 0x20 set -> return. Else set it; r1 & ~2 != 0 -> mark only; else cancel +0x144/+0x148 (0xA36618) and zero them, 1BC = (1BC & ~2) | 8.
        var l = Limiter(new Graph());
        var cancels = new List<int>();
        l.CancelTransitionA36618 = (_, off) => cancels.Add(off);
        var pbi = Pbi(50f);
        pbi.Field144 = 5;
        pbi.Item148Id = 0x04000000;
        pbi.Flags1BC = 0x02;
        l.MarkStopped9FF7B8(pbi, 4, 0);                                              // r1 & ~2 != 0: mark only
        Assert.Equal(0x22, pbi.Flags1BC);
        Assert.Empty(cancels);
        l.MarkStopped9FF7B8(pbi, 0, 0);                                              // already marked: return
        Assert.Empty(cancels);
        pbi.Flags1BC = 0x02;
        l.MarkStopped9FF7B8(pbi, 2, 1);                                              // r1 == 2 -> the full body; r2 is unused
        Assert.Equal(new[] { 0x144, 0x148 }, cancels);
        Assert.Equal(0u, pbi.Field144);
        Assert.Null(pbi.Item148Id);
        Assert.Equal(0x28, pbi.Flags1BC);
    }

    [Fact]
    public void M6_026_K6_ThreeDPathIsAVisibleStop()
    {
        var l = Limiter(new Graph());
        var pbi = Pbi(50f);
        pbi.Field0AC = new object();
        Assert.Throws<WwiseMissingBehaviourException>(() => l.MarkStopped9FF7B8(pbi, 0, 0));
    }

    // ------------------------------------------------------------------ 8.1..8.6: 0x9F3274

    [Fact]
    public void M6_026_8_1_TheListIsDescendingByPriority()
    {
        var l = Limiter(new Graph());
        var list = new WwisePbiList();
        foreach (float p in new[] { 50f, 70f, 30f, 60f, 40f }) l.InsertIntoList9F3274(list, Pbi(p));
        Assert.Equal(new[] { 70f, 60f, 50f, 40f, 30f }, list.Items.Select(x => x.Priority1C0).ToArray());
    }

    [Fact]
    public void M6_026_8_2_EqualPriorityKillNewestZeroIsDescendingOn1C8Then1C4()
    {
        // 8.2: [list+0xE] == 0: 1C8 new > mid -> lower index; new < mid -> higher index; equal 1C8 -> compare 1C4 the same way. Unsigned.
        var l = Limiter(new Graph());
        var list = new WwisePbiList { KillNewest = false };
        var a = Pbi(50f, chain: 5, k4: 1);
        var b = Pbi(50f, chain: 9, k4: 1);
        var c = Pbi(50f, chain: 7, k4: 1);
        var d = Pbi(50f, chain: 7, k4: 2);
        var e = Pbi(50f, chain: 0xFFFFFFF0, k4: 0);                                  // unsigned: larger than the rest
        foreach (var p in new[] { a, b, c, d, e }) l.InsertIntoList9F3274(list, p);
        Assert.Equal(new[] { e, b, d, c, a }, list.Items);
    }

    [Fact]
    public void M6_026_8_3_EqualPriorityKillNewestOneIsAscendingOn1C8Then1C4()
    {
        // 8.3: [list+0xE] != 0: 1C8 new < mid -> lower index; new > mid -> higher; equal -> 1C4 new < mid -> lower, else higher.
        var l = Limiter(new Graph());
        var list = new WwisePbiList { KillNewest = true };
        var a = Pbi(50f, chain: 5, k4: 1);
        var b = Pbi(50f, chain: 9, k4: 1);
        var c = Pbi(50f, chain: 7, k4: 1);
        var d = Pbi(50f, chain: 7, k4: 2);
        var e = Pbi(50f, chain: 0xFFFFFFF0, k4: 0);
        foreach (var p in new[] { a, b, c, d, e }) l.InsertIntoList9F3274(list, p);
        Assert.Equal(new[] { a, c, d, b, e }, list.Items);
    }

    [Fact]
    public void M6_026_4_1_4_3_AnExactKeyTieInsertsAtMidForEitherFlag()
    {
        // 4.1/4.2: equal 1C8 and equal 1C4 -> r1 = 0 -> 0x9F3334 stores at mid (the equal element shifts up), for [list+0xE] == 0 and != 0.
        foreach (bool killNewest in new[] { false, true })
        {
            var l = Limiter(new Graph());
            var list = new WwisePbiList { KillNewest = killNewest };
            var a = Pbi(50f, chain: 5, k4: 1);
            l.InsertIntoList9F3274(list, a);
            var same = Pbi(50f, chain: 5, k4: 1);
            Assert.Equal(1, l.InsertIntoList9F3274(list, same));
            Assert.Equal(new[] { same, a }, list.Items);                             // mid = 0
        }
    }

    [Fact]
    public void M6_026_8_1_ANaNPriorityInsertsAtMid()
    {
        // 8.1: unordered -> insert at mid.
        var l = Limiter(new Graph());
        var list = new WwisePbiList();
        var a = Pbi(50f); var b = Pbi(60f);
        l.InsertIntoList9F3274(list, a);
        l.InsertIntoList9F3274(list, b);
        var nan = Pbi(float.NaN);
        l.InsertIntoList9F3274(list, nan);
        Assert.Equal(new[] { nan, b, a }, list.Items);                               // lo 0, hi 1, mid = 0: NaN vs 60 is unordered -> insert at mid (index 0)
    }

    [Fact]
    public void M6_026_8_4_TheArrayGrowsByEightCountsAndRegistersTheEmptyListOnce()
    {
        var l = Limiter(new Graph());
        var list = new WwisePbiList { KeyHi = 1, KeyLo = 2 };
        Assert.Equal(0, list.Capacity);
        Assert.Equal(1, l.InsertIntoList9F3274(list, Pbi(50f)));
        Assert.Equal(8, list.Capacity);                                              // 8.4: (cap+8)*4 bytes
        Assert.Equal((ushort)1, list.Count20);                                       // 8.4: (u16)[list+0x20]++
        Assert.Same(list, Assert.Single(l.GlobalLimiterArray));                      // 8.5: registered when it was empty
        for (int i = 0; i < 7; i++) l.InsertIntoList9F3274(list, Pbi(50f));
        Assert.Equal(8, list.Capacity);
        l.InsertIntoList9F3274(list, Pbi(50f));
        Assert.Equal(16, list.Capacity);
        Assert.Equal((ushort)9, list.Count20);
        Assert.Single(l.GlobalLimiterArray);                                         // only the first insert registers
    }

    [Fact]
    public void M6_026_8_4_AnAllocationFailureReturns2AndInsertsNothing()
    {
        var l = Limiter(new Graph());
        var list = new WwisePbiList();
        l.AllocationFails = () => true;
        Assert.Equal(2, l.InsertIntoList9F3274(list, Pbi(50f)));                     // 0x9F33F0
        Assert.Empty(list.Items);
        Assert.Equal((ushort)0, list.Count20);
        Assert.Empty(l.GlobalLimiterArray);
    }

    [Fact]
    public void M6_026_8_5_TheGlobalLimiterArrayIsDescendingByTheKeyAndGrowsByOne()
    {
        // 8.5: sorted descending by the 64-bit key at [list+0x18] (high then low, unsigned; equal inserts at mid); growth cap+1.
        var l = Limiter(new Graph());
        var keys = new (uint Hi, uint Lo)[] { (0x40000001, 5), (0x40000002, 1), (0x40000001, 9), (0xFFFFFFFF, 0), (0x40000001, 5) };
        var lists = keys.Select(k => new WwisePbiList { KeyHi = k.Hi, KeyLo = k.Lo }).ToArray();
        foreach (var list in lists) l.RegisterList0A39254(list);
        Assert.Equal(5, l.GlobalLimiterArray.Count);
        var order = l.GlobalLimiterArray.Select(x => (x.KeyHi, x.KeyLo)).ToArray();
        Assert.Equal(new (uint, uint)[] { (0xFFFFFFFF, 0), (0x40000002, 1), (0x40000001, 9), (0x40000001, 5), (0x40000001, 5) }, order);
        Assert.Equal(5, l.GlobalLimiterArrayCapacity);                               // grown by 1 each time
        l.UnregisterList0A394C0(lists[3]);                                           // 0xA394C0: linear search, memmove, count--
        Assert.DoesNotContain(lists[3], l.GlobalLimiterArray);
        Assert.Equal(4, l.GlobalLimiterArray.Count);
    }

    [Fact]
    public void M6_026_8_6_TheKeysAreCreationSequenceNumbers()
    {
        // 8.6: pbi+0x1C8 = [0x1052434]++ (initial 1) and pbi+0x1C4 = [0x108DC18]++ (initial 0): each new PBI's keys are one above the previous.
        var a = Pbi(50f);
        var b = Pbi(50f);
        Assert.Equal(a.ChainId + 1, b.ChainId);
        Assert.Equal(a.Key1C4 + 1, b.Key1C4);
        // params+0x7C != 0: 1C8 = params+0x7C and 1BE bit 3 set; 1C4 is still the counter.
        var c = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, ChainId = 0x77 }, 1, TestSource, new byte[0x44], null, false);
        Assert.Equal(0x77u, c.ChainId);
        Assert.Equal(8, c.Flags1BE & 8);
        Assert.Equal(b.Key1C4 + 1, c.Key1C4);
    }

    // ------------------------------------------------------------------ O1..O6: the per-object variant

    private static (WwisePlaybackLimiter L, Graph G) PerObjectRig(ushort max, byte adv0 = 0)
    {
        var g = new Graph(Snd(1, P(adv0: adv0, max: max)));
        return (Limiter(g), g);
    }

    [Fact]
    public void M6_026_O2_O3_ANewGameObjectGetsAnEntryAndItsListIsAppendedOnlyWithANonzeroMax()
    {
        var (l, g) = PerObjectRig(5);
        var pbi = Pbi(50f);
        Assert.Equal(1, l.Walk(g[1], Block(pbi.LimiterArray1EC, go: 7), true, false));
        var limiter = l.LimiterOf(1)!;
        Assert.False(limiter.Global68);
        var slot = Assert.Single(limiter.Map);
        Assert.Equal(7u, slot.Go);
        Assert.Equal((ushort)5, slot.Entry!.List.Max);                               // O3: +0x34 = max
        Assert.Same(slot.Entry.List, Assert.Single(pbi.LimiterArray1EC.Items));      // O2: appended (slot != 0 && keep)
        Assert.Equal(1, limiter.MapCapacity);                                        // O3: the map grows by 1

        // "A node with max 0 and bit6 clear gets an entry anyway on every Play of a new game object; no list is appended." (O3)
        var (l0, g0) = PerObjectRig(0);
        var pbi0 = Pbi(50f);
        Assert.Equal(1, l0.Walk(g0[1], Block(pbi0.LimiterArray1EC, go: 7), true, false));
        Assert.Single(l0.LimiterOf(1)!.Map);
        Assert.Empty(pbi0.LimiterArray1EC.Items);
    }

    [Fact]
    public void M6_026_O2_ADifferentGameObjectHasItsOwnEntry()
    {
        var (l, g) = PerObjectRig(2);
        l.Walk(g[1], Block(Pbi(50f).LimiterArray1EC, go: 7), true, false);
        l.Walk(g[1], Block(Pbi(50f).LimiterArray1EC, go: 8), true, false);
        Assert.Equal(new uint?[] { 7, 8 }, l.LimiterOf(1)!.Map.Select(e => e.Go).ToArray());
        Assert.Equal(2, l.LimiterOf(1)!.MapCapacity);
    }

    [Fact]
    public void M6_026_O4_AFoundEntryChecksAgainstMaxMinusVirtualMinusTheKillCount()
    {
        // O4: n = (u16)e+0x48 - (u16)e+0x4A - [block+0xE]; n < e.max -> r8 = 1 and the list is appended; else 0xA37100(e.list, e.max, prio, go, ...) and [block+0xE]++.
        var (l, g) = PerObjectRig(3);
        l.Walk(g[1], Block(Pbi(50f).LimiterArray1EC, go: 7), true, false);           // creates the entry
        var entry = l.LimiterOf(1)!.Map[0].Entry!;
        var members = new[] { Pbi(50f, go: 7), Pbi(50f, go: 7) };
        foreach (var m in members) l.InsertIntoList9F3274(entry.List, m);
        // Count20 = 2 < 3: no victim, the list is appended.
        var b1 = Block(Pbi(50f).LimiterArray1EC, go: 7);
        Assert.Equal(1, l.Walk(g[1], b1, true, false));
        Assert.Equal(0, b1.Count0E);
        Assert.Same(entry.List, Assert.Single(b1.Array!.Items));
        // Count20 = 3: n = 3 is not < 3 -> the victim check runs.
        var third = Pbi(50f, go: 7);
        l.InsertIntoList9F3274(entry.List, third);
        var b2 = Block(Pbi(50f).LimiterArray1EC, go: 7);
        Assert.Equal(1, l.Walk(g[1], b2, true, false));
        Assert.Equal(1, b2.Count0E);
        Assert.Equal(1, new[] { members[0], members[1], third }.Count(p => (p.Flags1BD & 2) != 0));
        Assert.Same(entry.List, Assert.Single(b2.Array!.Items));                     // appended after the check
        // [block+0xE] is subtracted: with one kill already counted n = 3 - 0 - 1 = 2 < 3 -> no check.
        var b3 = Block(Pbi(50f).LimiterArray1EC, go: 7);
        b3.Count0E = 1;
        Assert.Equal(1, l.Walk(g[1], b3, true, false));
        Assert.Equal(1, b3.Count0E);
    }

    [Fact]
    public void M6_026_O4_ABlock0x11ByteOfZeroAppendsWithoutTheCheck()
    {
        var (l, g) = PerObjectRig(1);
        l.Walk(g[1], Block(Pbi(50f).LimiterArray1EC, go: 7), true, false);
        var entry = l.LimiterOf(1)!.Map[0].Entry!;
        l.InsertIntoList9F3274(entry.List, Pbi(50f, go: 7));
        var b = Block(Pbi(50f).LimiterArray1EC, go: 7, b11: 0);
        Assert.Equal(1, l.Walk(g[1], b, true, false));
        Assert.Equal(0, b.Count0E);
        Assert.Same(entry.List, Assert.Single(b.Array!.Items));
    }

    [Fact]
    public void M6_026_O4_AFoundEntryWithMaxZeroReturns1WithoutAppending()
    {
        var (l, g) = PerObjectRig(0);
        l.Walk(g[1], Block(Pbi(50f).LimiterArray1EC, go: 7), true, false);
        var b = Block(Pbi(50f).LimiterArray1EC, go: 7);
        Assert.Equal(1, l.Walk(g[1], b, true, false));
        Assert.Empty(b.Array!.Items);
    }

    [Fact]
    public void M6_026_O4_AZeroEntryPointerReturns1()
    {
        var (l, g) = PerObjectRig(2);
        l.Walk(g[1], Block(Pbi(50f).LimiterArray1EC, go: 7), true, false);
        l.LimiterOf(1)!.Map[0].Entry = null;
        var b = Block(Pbi(50f).LimiterArray1EC, go: 7);
        Assert.Equal(1, l.Walk(g[1], b, true, false));
        Assert.Empty(b.Array!.Items);
    }

    [Fact]
    public void M6_026_O4_WithoutTheCountFlagAFoundEntryIsAppendedWhenItsMaxIsNonzero()
    {
        var (l, g) = PerObjectRig(2);
        l.Walk(g[1], Block(Pbi(50f).LimiterArray1EC, go: 7), true, false);
        var b = Block(Pbi(50f).LimiterArray1EC, go: 7);
        Assert.Equal(1, l.Walk(g[1], b, count: false, skipGlobal: false));
        Assert.Single(b.Array!.Items);
    }

    [Fact]
    public void M6_026_O3_AnAllocationFailureOfTheEntryReturns2AndOfTheMapDestroysIt()
    {
        var (l, g) = PerObjectRig(2);
        l.Count9FAC54(g[1], false);                                                  // the limiter exists first
        l.AllocationFails = () => true;
        var b = Block(Pbi(50f).LimiterArray1EC, go: 7);
        Assert.Equal(2, l.Walk(g[1], b, count: false, skipGlobal: false));           // O3: entry allocation fails -> 2
        Assert.Empty(l.LimiterOf(1)!.Map);
        Assert.Empty(b.Array!.Items);
        int calls = 0;
        l.AllocationFails = () => ++calls == 2;                                      // the entry succeeds, the map growth fails
        Assert.Equal(2, l.Walk(g[1], Block(Pbi(50f).LimiterArray1EC, go: 7), count: false, skipGlobal: false));
        Assert.Empty(l.LimiterOf(1)!.Map);
    }

    [Fact]
    public void M6_026_1_7_F1_TheEntryRemovalCases()
    {
        // 1.7: not in the map -> return with no idle test; an entry with a nonzero +0x48/+0x4A -> the idle test only; both zero -> free the entry, remove the slot, idle test;
        // F1: a zero entry pointer takes the same slot removal and idle test.
        var (l, g) = PerObjectRig(2);
        l.Walk(g[1], Block(Pbi(50f).LimiterArray1EC, go: 7), true, false);
        var lim = l.LimiterOf(1)!;
        lim.Count60 = 0;                                                             // idle except for the map slot
        l.RemovePerObject9FAA70(g[1], 99);                                           // not found: no idle test, nothing happens
        Assert.NotNull(l.LimiterOf(1));
        Assert.Single(lim.Map);
        var entry = lim.Map[0].Entry!;
        entry.List.Count20 = 1;
        l.RemovePerObject9FAA70(g[1], 7);                                            // +0x48 != 0: kept (the idle test fails on the map)
        Assert.Single(lim.Map);
        entry.List.Count20 = 0;
        entry.List.Virtual22 = 1;
        l.RemovePerObject9FAA70(g[1], 7);                                            // +0x4A != 0: kept
        Assert.Single(lim.Map);
        entry.List.Virtual22 = 0;
        lim.Count60 = 1;
        l.RemovePerObject9FAA70(g[1], 7);                                            // freed and removed; still counted, so not idle
        Assert.Empty(lim.Map);
        Assert.NotNull(l.LimiterOf(1));

        var (l2, g2) = PerObjectRig(2);
        l2.Walk(g2[1], Block(Pbi(50f).LimiterArray1EC, go: 7), true, false);
        l2.LimiterOf(1)!.Count60 = 0;
        l2.LimiterOf(1)!.Map[0].Entry = null;                                        // the zero entry pointer
        l2.RemovePerObject9FAA70(g2[1], 7);                                          // slot removed, then idle -> destroyed
        Assert.Null(l2.LimiterOf(1));
    }

    // ------------------------------------------------------------------ L7: idle

    [Fact]
    public void M6_026_L7_TheIdlePredicate()
    {
        // L7: (s16)+0x60 <= 0, (s16)+0x64 <= 0, +0x58 == 0, +0x5A == 0, [+0xC] == 0, [+4] == 0.
        var l = new WwiseNodeLimiter { NodeId = 1 };
        Assert.True(WwisePlaybackLimiter.IsIdle(l));
        l.Count60 = -1; l.Count64 = -3;
        Assert.True(WwisePlaybackLimiter.IsIdle(l));                                 // signed compares
        l.Count60 = 1; Assert.False(WwisePlaybackLimiter.IsIdle(l)); l.Count60 = 0;
        l.Count64 = 1; Assert.False(WwisePlaybackLimiter.IsIdle(l)); l.Count64 = 0;
        l.List.Count20 = 1; Assert.False(WwisePlaybackLimiter.IsIdle(l)); l.List.Count20 = 0;
        l.List.Virtual22 = 1; Assert.False(WwisePlaybackLimiter.IsIdle(l)); l.List.Virtual22 = 0;
        l.Contexts0C.Add(Pbi(50f)); Assert.False(WwisePlaybackLimiter.IsIdle(l)); l.Contexts0C.Clear();
        l.Map.Add(new WwiseLimiterMapEntry { Go = 1 }); Assert.False(WwisePlaybackLimiter.IsIdle(l)); l.Map.Clear();
        Assert.True(WwisePlaybackLimiter.IsIdle(l));
    }

    [Fact]
    public void M6_026_1_8_F2_TheDestroyCascadesToIdleParentAndBusAndUnsubscribesFirstOutward()
    {
        // 1.8: 0x9F4D40 runs 0x9F4F28 first (the parent by bl, then the bus by tail call, each only if its limiter is idle), then the unsubscribe 0xA19F60, then [L+0xC] = 0 (F2, unconditional)
        // and [node+0x30] = 0. S(1) -> M(2, bus 10 -> bus 11): the innermost is unsubscribed first.
        var g = new Graph(Snd(1, P(parent: 2)), Mix(2, P(bus: 10)), Bus(10, 11), Bus(11, 0));
        var l = Limiter(g);
        var unsub = new List<uint>();
        l.RtpcUnsubscribeA19F60 = x => unsub.Add(x.NodeId);
        l.Count9FAC54(g[1], false);
        foreach (uint id in new uint[] { 1, 2, 10, 11 }) l.LimiterOf(id)!.Count60 = 0;
        l.LimiterOf(1)!.Contexts0C.Add(Pbi(50f));                                    // not idle: 1 is not destroyed by IsIdle, so free the context first
        l.LimiterOf(1)!.Contexts0C.Clear();
        l.DestroyLimiter9F4D40(g[1]);
        Assert.Equal(new uint[] { 11, 10, 2, 1 }, unsub);
        foreach (uint id in new uint[] { 1, 2, 10, 11 }) Assert.Null(l.LimiterOf(id));
        // A parent that is not idle survives.
        var l2 = Limiter(g);
        l2.Count9FAC54(g[1], false);
        l2.LimiterOf(1)!.Count60 = 0;
        l2.LimiterOf(2)!.Count60 = 1;                                                // the parent is still counted
        l2.DestroyLimiter9F4D40(g[1]);
        Assert.Null(l2.LimiterOf(1));
        Assert.NotNull(l2.LimiterOf(2));                                             // Count60 = 1
    }

    [Fact]
    public void M6_026_1_8_TheUnsubscribeIsARequiredSeam()
    {
        var g = new Graph(Snd(1, P()));
        var l = Limiter(g);
        l.Count9FAC54(g[1], false);
        l.LimiterOf(1)!.Count60 = 0;
        l.RtpcUnsubscribeA19F60 = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => l.DestroyIfIdle(g[1]));
    }

    // ------------------------------------------------------------------ W7/W8: the bus body

    [Fact]
    public void M6_026_W7_ABusLimitCheckRunsButItsResultIsIgnored()
    {
        // W7: the bus always uses the global variant and the result is ignored (0x9C4FDC bl 0x9FA01C with no mov r5,r0 after it). Bus max 1 with an equal-priority
        // PBI and KillNewest (bus byte B is 0 in the reader, so use a plain bus and lower the newcomer instead): the walk still returns 1.
        var g = new Graph(Snd(1, P(bus: 10)), Bus(10, 0, max: 1));
        var l = Limiter(g);
        l.Count9FAC54(g[10], true);
        var old = Pbi(50f);
        l.InsertIntoList9F3274(l.LimiterOf(10)!.List, old);
        var pbi = Pbi(50f);
        var block = Block(pbi.LimiterArray1EC, 50f);
        Assert.Equal(1, l.Walk(g[1], block, true, false));
        Assert.NotEqual(0, old.Flags1BD & 2);                                        // the bus's check ran and killed the oldest
        Assert.Equal(1, block.Count0E);
        Assert.Contains(l.LimiterOf(10)!.List, pbi.LimiterArray1EC.Items);
    }

    [Fact]
    public void M6_026_W7_ABusReject2IsNotMergedIntoTheWalk()
    {
        // The same with a newcomer below the PBI: 0xA37100 would reject with 2, which the bus body drops; the walk returns 1.
        var g = new Graph(Snd(1, P(bus: 10)), Bus(10, 0, max: 1));
        var l = Limiter(g);
        l.Count9FAC54(g[10], true);
        l.InsertIntoList9F3274(l.LimiterOf(10)!.List, Pbi(60f));
        Assert.Equal(1, l.Walk(g[1], Block(Pbi(40f).LimiterArray1EC, 40f), true, false));
    }

    [Fact]
    public void M6_026_W8_ABusWithDuckEntriesNeedsTheDuckBodySeamButAnEmptyListDoesNot()
    {
        var withDucks = new Graph(Snd(1, P(bus: 10)), Bus(10, 0, ducks: new[] { (5u, -3f, 0u, 0u) }));
        var l = Limiter(withDucks);
        Assert.Throws<WwiseMissingBehaviourException>(() =>
            l.Walk(withDucks[1], Block(Pbi(50f).LimiterArray1EC), true, false));    // W8: 0x9C4CD0 with a non-empty list runs 0x9C4D04..0x9C4F0C
        var l2 = Limiter(withDucks);
        var called = new List<uint>();
        l2.DuckBodyA9C4CD0 = b => called.Add(b.Id);
        l2.Walk(withDucks[1], Block(Pbi(50f).LimiterArray1EC), true, false);
        Assert.Equal(new uint[] { 10 }, called);
        var empty = new Graph(Snd(1, P(bus: 10)), Bus(10, 0));
        Assert.Equal(1, Limiter(empty).Walk(empty[1], Block(Pbi(50f).LimiterArray1EC), true, false));   // 0x9C4CE0 cmp r5,#0; beq 0x9C4E5C
    }

    [Fact]
    public void M6_026_W7_TheBusFlagsWrittenOnlyWhenTheCountIsOne()
    {
        // W7: if [bus+0x30] != 0 and (s16)+0x60 == 1: [bus+0xCC] bits 0..2 = 1. A second walk (count 2) leaves the value alone.
        var g = new Graph(Snd(1, P(bus: 10)), Bus(10, 0));
        var l = Limiter(g);
        l.Walk(g[1], Block(Pbi(50f).LimiterArray1EC), true, false);
        l.SetBusCC(10, 0x38);                                                        // other bits are preserved, bits 0..2 replaced
        l.Walk(g[1], Block(Pbi(50f).LimiterArray1EC), true, false);
        Assert.Equal((short)2, l.LimiterOf(10)!.Count60);
        Assert.Equal(0x38, l.BusCC(10));
    }

    // ------------------------------------------------------------------ C1..C6: 0xA376C0 and 0xA37880

    [Fact]
    public void M6_026_C1_C3_WithBothThresholdsAtOneTheMemoryTestsAreSkipped()
    {
        // C3: both thresholds 1.0f: "t1 < 1.0f" is false, so 0xA7ABA8 is never read and 0xA376C0 returns 1 with no effect.
        var l = Limiter(new Graph());
        l.PoolStatsA7ABA8 = _ => throw new InvalidOperationException("the pool statistics are read only below the threshold");
        var gp = Pbi(50f);
        l.AppendToGlobalPbiList(gp);
        Assert.Equal(1, l.CheckMemoryA376C0(200f));
        Assert.Equal(0, gp.Flags1BD & 2);
    }

    [Fact]
    public void M6_026_C1_TheRatioCompareIsThresholdAtLeastRatioSkips()
    {
        // C1: ratio = (float)used/(float)total; t1 >= ratio -> skip; else the kill step. total == 0 -> skip. Boundary: 50/100 = 0.5 against 0.5.
        var l = Limiter(new Graph());
        l.MemoryThreshold1 = 0.5f;
        var gp = Pbi(40f);
        l.AppendToGlobalPbiList(gp);
        var pools = new List<int>();
        (uint, uint) stats = (100, 50);
        l.PoolStatsA7ABA8 = pool => { pools.Add(pool); return stats; };
        Assert.Equal(1, l.CheckMemoryA376C0(60f));
        Assert.Equal(new[] { 0 }, pools);                                            // [0x1052418] first
        Assert.Equal(0, gp.Flags1BD & 2);
        stats = (100, 51);                                                           // 0.51 > 0.5: the kill step
        Assert.Equal(1, l.CheckMemoryA376C0(60f));
        Assert.NotEqual(0, gp.Flags1BD & 2);
        var l0 = Limiter(new Graph()) ;
        l0.MemoryThreshold1 = 0.5f;
        l0.PoolStatsA7ABA8 = _ => (0, 7);                                            // total == 0 -> skip
        Assert.Equal(1, l0.CheckMemoryA376C0(200f));
    }

    [Fact]
    public void M6_026_C1_TheSecondTestUsesThePoolAt0x1052428()
    {
        var l = Limiter(new Graph());
        l.MemoryThreshold2 = 0.25f;
        var gp = Pbi(40f);
        l.AppendToGlobalPbiList(gp);
        var pools = new List<int>();
        l.PoolStatsA7ABA8 = pool => { pools.Add(pool); return (100, 26); };
        Assert.Equal(1, l.CheckMemoryA376C0(60f));
        Assert.Equal(new[] { 1 }, pools);
        Assert.NotEqual(0, gp.Flags1BD & 2);
    }

    [Fact]
    public void M6_026_C1_AThresholdBelowOneWithoutThePoolSeamIsAVisibleStop()
    {
        var l = Limiter(new Graph());
        l.MemoryThreshold1 = 0.5f;
        Assert.Throws<WwiseMissingBehaviourException>(() => l.CheckMemoryA376C0(50f));
    }

    [Fact]
    public void M6_026_C2_C28_3_TheKillStepChoosesTheRunningMinimumAndFailsTheEqualOrLowerNewcomer()
    {
        // C2: skip 1BD&2 and 1BE&0x2C; s14 = 101.0f; cand = the last PBI with 1C0 <= s14, s14 = its priority (a running minimum); prio < s14 or == s14 -> return 0;
        // cand == 0 -> return 0 (C28.3); else 0xA01CA4(cand, 3) and 1.
        Func<(WwisePlaybackLimiter L, WwisePlayingInstance[] P)> make = () =>
        {
            var l = Limiter(new Graph());
            l.MemoryThreshold1 = 0.5f;
            l.PoolStatsA7ABA8 = _ => (100, 90);
            var ps = new[] { Pbi(70f), Pbi(40f), Pbi(55f) };
            foreach (var p in ps) l.AppendToGlobalPbiList(p);
            return (l, ps);
        };
        var (l1, p1) = make();
        Assert.Equal(1, l1.CheckMemoryA376C0(60f));                                  // 60 > 40: kills the 40
        Assert.Equal(0, p1[0].Flags1BD & 2);
        Assert.Equal(0, p1[2].Flags1BD & 2);
        Assert.Equal(0x4E, p1[1].Flags1BD);                                          // reason 3: (0x44 & ~0x1C) | (3 << 2) | 2
        var (l2, p2) = make();
        Assert.Equal(0, l2.CheckMemoryA376C0(40f));                                  // == s14: 0
        Assert.All(p2, p => Assert.Equal(0, p.Flags1BD & 2));
        var (l3, p3) = make();
        Assert.Equal(0, l3.CheckMemoryA376C0(30f));                                  // < s14: 0
        var (l4, p4) = make();
        p4[1].Flags1BD |= 2;                                                         // the 40 is dead: skipped, the running minimum is 55
        Assert.Equal(1, l4.CheckMemoryA376C0(56f));
        Assert.NotEqual(0, p4[2].Flags1BD & 2);
        var empty = Limiter(new Graph());
        empty.MemoryThreshold1 = 0.5f;
        empty.PoolStatsA7ABA8 = _ => (100, 90);
        Assert.Equal(0, empty.CheckMemoryA376C0(200f));                              // C28.3: cand == 0 returns 0 (not 1)
        Assert.Equal(0, empty.CheckMemoryA376C0(50f));
    }

    [Fact]
    public void M6_026_C4_TheVoiceCountTestIsSkippedWhileTheCountIsWithinMax()
    {
        // C4: n = [G+0x48] + 1 - [G] (unsigned); max = [G+0x2C] = 256 on shipped data; n <= max -> return 1, no effect.
        var l = Limiter(new Graph());
        l.ApplyStmgMaxVoices(256);
        Assert.Equal((ushort)256, l.GlobalVoiceList.Max);
        for (int i = 0; i < 255; i++) l.AppendToGlobalPbiList(Pbi(50f));            // n = 256
        Assert.Equal(1, l.CheckVoiceCountA37880(50f));
        l.NextSourceCodeA01768 = _ => throw new InvalidOperationException("the pass is not entered while n <= max");
        Assert.Equal(1, l.CheckVoiceCountA37880(50f));
        l.GlobalVirtualCount = 0;
        l.AppendToGlobalPbiList(Pbi(50f));                                           // n = 257 > 256: the pass is entered (and asks for the code of an eligible PBI)
        l.InsertIntoList9F3274(l.GlobalVoiceList, Pbi(50f));
        Assert.Throws<InvalidOperationException>(() => l.CheckVoiceCountA37880(60f));
    }

    [Fact]
    public void M6_026_C4_TheVirtualCountIsSubtracted()
    {
        var l = Limiter(new Graph());
        l.ApplyStmgMaxVoices(2);
        l.AppendToGlobalPbiList(Pbi(50f));
        l.AppendToGlobalPbiList(Pbi(50f));                                           // [G+0x48] = 2, n = 3 > 2
        l.NextSourceCodeA01768 = _ => throw new InvalidOperationException("entered");
        l.InsertIntoList9F3274(l.GlobalVoiceList, Pbi(50f));
        Assert.Throws<InvalidOperationException>(() => l.CheckVoiceCountA37880(60f));
        l.GlobalVirtualCount = 1;                                                    // n = 2 + 1 - 1 = 2 <= 2
        Assert.Equal(1, l.CheckVoiceCountA37880(60f));
    }

    [Fact]
    public void M6_026_C5_ThePassKillsTheCandidateWithCode1AndReason2()
    {
        // C5: voice list [A 70, B 50]; max 2; n = 3. Skip 1BD&2 and 1BE&0x2C; count++ for every eligible before the priority test; prio < pbi.1C0 skips;
        // else c = 0xA01768: c == 0 -> r8 = 1 if max >= count; c != 0 -> s18, code, cand. After: max > count -> 1; prio <= s18 or no cand -> r8 ? 1 : 0x50; code != 1 -> 1; else 0xA01CA4(cand, 2).
        var l = Limiter(new Graph());
        l.ApplyStmgMaxVoices(2);
        var a = Pbi(70f); var b = Pbi(50f);
        l.InsertIntoList9F3274(l.GlobalVoiceList, a);
        l.InsertIntoList9F3274(l.GlobalVoiceList, b);
        for (int i = 0; i < 2; i++) l.AppendToGlobalPbiList(Pbi(50f));
        l.NextSourceCodeA01768 = p => ReferenceEquals(p, b) ? 1 : throw new InvalidOperationException("only B is at or below the newcomer");
        Assert.Equal(1, l.CheckVoiceCountA37880(60f));
        Assert.Equal(0, a.Flags1BD & 2);
        Assert.Equal(0x4A, b.Flags1BD);                                              // reason 2
    }

    [Fact]
    public void M6_026_C5_ACodeOtherThan1DoesNotKillAndAllSkippedReturns0x50()
    {
        var l = Limiter(new Graph());
        l.ApplyStmgMaxVoices(1);
        var a = Pbi(70f);
        l.InsertIntoList9F3274(l.GlobalVoiceList, a);
        l.AppendToGlobalPbiList(Pbi(50f));                                           // n = 2 > 1
        l.NextSourceCodeA01768 = _ => 2;
        Assert.Equal(0x50, l.CheckVoiceCountA37880(60f));                            // 60 < 70: skipped, no candidate, r8 = 0 -> 0x50
        var l2 = Limiter(new Graph());
        l2.ApplyStmgMaxVoices(1);
        var c = Pbi(50f);
        l2.InsertIntoList9F3274(l2.GlobalVoiceList, c);
        l2.AppendToGlobalPbiList(Pbi(50f));
        l2.NextSourceCodeA01768 = _ => 2;
        Assert.Equal(1, l2.CheckVoiceCountA37880(60f));                              // candidate with code 2: return 1, no kill
        Assert.Equal(0, c.Flags1BD & 2);
    }

    // ------------------------------------------------------------------ E1..E3: the per-frame enforcement

    [Fact]
    public void M6_026_E1_TheFrameClearsBit2OfEveryPbiAndEnforcesEveryRegisteredList()
    {
        var l = Limiter(new Graph());
        var a = Pbi(50f); var b = Pbi(50f);
        a.Flags1BE = 0x04; b.Flags1BE = 0x14;
        l.AppendToGlobalPbiList(a);
        l.AppendToGlobalPbiList(b);
        l.PerFrameA39564();
        Assert.Equal(0x00, a.Flags1BE);                                              // 1BE &= ~4
        Assert.Equal(0x10, b.Flags1BE);
    }

    [Fact]
    public void M6_026_E1_TheReRegistrationRunsOnlyWhenG60IsSetAndG61IsClear()
    {
        var l = Limiter(new Graph());
        var empty = Pbi(50f);                                                        // an empty array and 0xE9 bit 2 clear: 0xA0054C would run
        l.AppendToGlobalPbiList(empty);
        l.G60 = 1; l.G61 = 1;
        l.PerFrameA39564();                                                          // G61 set: not entered
        Assert.Equal(1, l.G60);
        l.G61 = 0;
        Assert.Throws<WwiseMissingBehaviourException>(() => l.PerFrameA39564());     // 0xA0054C is an unread body
        var called = new List<WwisePlayingInstance>();
        l.ReRegisterA0054C = called.Add;
        l.PerFrameA39564();
        Assert.Equal(new[] { empty }, called);
        Assert.Equal(0, l.G60);                                                      // [G+0x60] = 0
        empty.LimiterArray1EC.Items.Add(new WwisePbiList());                         // 0xA0049C returns: array non-empty
        called.Clear();
        l.G60 = 1;
        l.PerFrameA39564();
        Assert.Empty(called);
    }

    [Fact]
    public void M6_026_E2_ASurplusListKillsThePbisAfterTheFirstMaxWithReason1()
    {
        // E2: max = [list+0xC]; max == 0 or count <= max -> return. Pass 1 counts PBIs that pass the skips and are audible or have 1BA & 0x78 == 0 until counted == max;
        // pass 2 kills the remaining ones (skipping 1BD&2 and 1BE&8) with reason 1 (a non-global list, virtual byte 0 -> 0xA01CA4).
        var l = Limiter(new Graph());
        var list = new WwisePbiList { Max = 2 };
        var ps = new[] { Pbi(70f), Pbi(60f), Pbi(50f), Pbi(40f) };
        foreach (var p in ps) l.InsertIntoList9F3274(list, p);
        l.EnforceListA3BA4(list);
        Assert.Equal(new[] { 0, 0, 2, 2 }, ps.Select(p => p.Flags1BD & 2).ToArray());
        Assert.Equal(0x46, ps[2].Flags1BD);                                          // reason 1
        // max 0 and count <= max return at once
        var l2 = Limiter(new Graph());
        var list2 = new WwisePbiList { Max = 0 };
        var q = Pbi(50f);
        l2.InsertIntoList9F3274(list2, q);
        l2.EnforceListA3BA4(list2);
        Assert.Equal(0, q.Flags1BD & 2);
        var list3 = new WwisePbiList { Max = 1 };
        var r = Pbi(50f);
        l2.InsertIntoList9F3274(list3, r);
        l2.EnforceListA3BA4(list3);
        Assert.Equal(0, r.Flags1BD & 2);
    }

    [Fact]
    public void M6_026_E2_ThePassOneSkipsDeadAndMarkedPbisAndUsesTheAudibilityTest()
    {
        var l = Limiter(new Graph());
        var list = new WwisePbiList { Max = 1 };
        var a = Pbi(70f); var b = Pbi(60f); var c = Pbi(50f);
        a.Flags1BE |= 8;                                                             // pass 1 skips 1BE&8
        b.NextSourceCache1BB = 0x18;                                                  // pbi+0x1BB & 0x78 != 0 (0x9F3C0C): counts only when audible
        b.Field154 = new WwiseLiveVoice(1, 16);
        foreach (var p in new[] { a, b, c }) l.InsertIntoList9F3274(list, p);
        l.VoiceAudibleA4B3E8 = _ => false;                                           // not audible: b does not count
        c.NextSourceCache1BB = 0;                                                     // c counts (1BB & 0x78 == 0)
        l.EnforceListA3BA4(list);
        // counted: a skipped, b not counted, c counted (== max) -> nothing remains for pass 2.
        Assert.All(new[] { a, b, c }, p => Assert.Equal(0, p.Flags1BD & 2));
        var l2 = Limiter(new Graph());
        var list2 = new WwisePbiList { Max = 1 };
        var d = Pbi(70f); var e = Pbi(60f);
        d.NextSourceCache1BB = 0x18; d.Field154 = new WwiseLiveVoice(1, 16);
        foreach (var p in new[] { d, e }) l2.InsertIntoList9F3274(list2, p);
        l2.VoiceAudibleA4B3E8 = _ => true;
        l2.EnforceListA3BA4(list2);                                                  // d audible: counted; e is killed
        Assert.Equal(0, d.Flags1BD & 2);
        Assert.NotEqual(0, e.Flags1BD & 2);
        var l3 = Limiter(new Graph());
        var list3 = new WwisePbiList { Max = 1 };
        var f = Pbi(70f);
        f.Field154 = new WwiseLiveVoice(1, 16);
        l3.InsertIntoList9F3274(list3, f);
        l3.InsertIntoList9F3274(list3, Pbi(60f));
        Assert.Throws<WwiseMissingBehaviourException>(() => l3.EnforceListA3BA4(list3));   // 0xA4B3E8 is an unread body
    }

    [Fact]
    public void M6_026_E2_E3_AVirtualListVirtualizesInsteadOfKillingAndTheGlobalListUsesReason2()
    {
        // E2 pass 2: [list+0xF] -> 0xA020F4(pbi, reason) else 0xA01CA4; the global voice list uses reason 2 and [G+0x2F] (its virtual byte).
        // E3: code 1 -> the 0xA01CA4 body; code 2 -> 1BE |= 4; else nothing.
        var l = Limiter(new Graph());
        l.ApplyStmgMaxVoices(1);
        var ps = new[] { Pbi(70f), Pbi(60f), Pbi(50f), Pbi(40f) };
        foreach (var p in ps) l.InsertIntoList9F3274(l.GlobalVoiceList, p);
        var codes = new Dictionary<WwisePlayingInstance, int> { [ps[1]] = 1, [ps[2]] = 2, [ps[3]] = 0 };
        l.NextSourceCodeA01768 = p => codes[p];
        l.EnforceListA3BA4(l.GlobalVoiceList);
        Assert.Equal(0x4A, ps[1].Flags1BD);                                          // code 1: killed with reason 2
        Assert.Equal(0x44, ps[2].Flags1BD);                                          // code 2: only 1BE |= 4
        Assert.Equal(4, ps[2].Flags1BE & 4);
        Assert.Equal(0x44, ps[3].Flags1BD);                                          // any other code: nothing
        Assert.Equal(0, ps[3].Flags1BE & 4);
    }

    // ------------------------------------------------------------------ R1..R3: the removal side

    [Fact]
    public void M6_026_R1_TheInsertAppendsTheGlobalListAndInsertsIntoEveryArrayEntry()
    {
        // R1: pbi vt+0xC = 0xA0285C: push the context on [node+0x30]+0xC (ctx+0x24 = old head), subscribe (mask {0xFFFE67BD, 0x3FE3}); if pbi+0xE9 bit 2 is clear,
        // append &G+0x20 to the array and 0x9F3274 into every entry; [pbi+0x140] == 0 -> 2; else 0xA04D48.
        var g = new Graph(Mix(1, P(adv0: 0x04, max: 3)));
        var l = Limiter(g);
        var subs = new List<(string, ulong)>();
        l.RtpcSubscribeA19ECC = (k, _, m) => subs.Add((k, m));
        var registered = new List<WwisePlayingInstance>();
        l.RegisterPlayingIdA04D48 = registered.Add;
        l.Count9FAC54(g[1], false);
        var pbi = Pbi(50f);
        l.Walk(g[1], Block(pbi.LimiterArray1EC), true, false);
        Assert.Equal(1, l.InsertPbiA0285C(g[1], pbi));
        Assert.Equal(new[] { pbi }, l.LimiterOf(1)!.Contexts0C);
        Assert.Contains(("context", 0x3FE3FFFE67BDUL), subs);
        Assert.Equal(2, pbi.LimiterArray1EC.Count);                                  // the mixer's list and G+0x20
        Assert.Same(l.GlobalVoiceList, pbi.LimiterArray1EC.Items[1]);
        Assert.Equal(new[] { pbi }, l.LimiterOf(1)!.List.Items);
        Assert.Equal(new[] { pbi }, l.GlobalVoiceList.Items);
        Assert.Equal((ushort)1, l.LimiterOf(1)!.List.Count20);
        Assert.Equal(new[] { pbi }, registered);
        // the head insert: a second context goes in front
        var second = Pbi(50f);
        l.Walk(g[1], Block(second.LimiterArray1EC), true, false);
        l.InsertPbiA0285C(g[1], second);
        Assert.Equal(new[] { second, pbi }, l.LimiterOf(1)!.Contexts0C);
    }

    [Fact]
    public void M6_026_R1_APbiWithBit2Of0xE9SetIsNotInsertedAndAZeroPlayingIdReturns2()
    {
        var g = new Graph(Mix(1, P(adv0: 0x04, max: 3)));
        var l = Limiter(g);
        var pbi = Pbi(50f);
        pbi.Flags0E9 = 4;
        l.Count9FAC54(g[1], false);
        Assert.Equal(1, l.InsertPbiA0285C(g[1], pbi));
        Assert.Empty(pbi.LimiterArray1EC.Items);
        Assert.Empty(l.GlobalVoiceList.Items);
        var noId = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 0, TargetNodeId = 1 }, 1, TestSource, new byte[0x44], null, false) { Priority1C0 = 50f };
        Assert.Equal(2, l.InsertPbiA0285C(g[1], noId));                              // [pbi+0x140] == 0 -> return 2
    }

    [Fact]
    public void M6_026_R3_a_TheVirtualReleaseDecrementsEveryListAndTheGlobalCount()
    {
        // R3 (a): 1BE bit5: clear it; for every list in the array (u16)[list+0x22]--; 0xA370E4 ([G]--). The inverse (C15 V7-b, C4 [G]++) counts it.
        var l = Limiter(new Graph());
        var pbi = Pbi(50f);
        var lists = new[] { new WwisePbiList(), new WwisePbiList() };
        foreach (var x in lists) pbi.LimiterArray1EC.Items.Add(x);
        l.AcquireVirtual0A0228C(pbi);
        Assert.Equal(0x20, pbi.Flags1BE & 0x20);
        Assert.All(lists, x => Assert.Equal((ushort)1, x.Virtual22));
        Assert.Equal(1u, l.GlobalVirtualCount);
        l.AcquireVirtual0A0228C(pbi);                                                // already set: no change
        Assert.Equal(1u, l.GlobalVirtualCount);
        l.ReleaseVirtual0A022E8(pbi);
        Assert.Equal(0, pbi.Flags1BE & 0x20);
        Assert.All(lists, x => Assert.Equal((ushort)0, x.Virtual22));
        Assert.Equal(0u, l.GlobalVirtualCount);
        l.ReleaseVirtual0A022E8(pbi);                                                // clear: nothing
        Assert.Equal(0u, l.GlobalVirtualCount);
    }

    private static (WwisePlaybackLimiter L, Graph G) WalkedChain(out WwisePlayingInstance pbi, bool insert = false)
    {
        // S(1) -> M(2, global, max 3, bus 10 -> bus 11), walked with object 7.
        var g = new Graph(Snd(1, P(parent: 2)), Mix(2, P(adv0: 0x04, max: 3, bus: 10)), Bus(10, 11), Bus(11, 0));
        var l = Limiter(g);
        pbi = Pbi(50f, go: 7);
        pbi.NodeE0 = g[1];
        l.Walk(g[1], Block(pbi.LimiterArray1EC, go: 7), true, false);
        if (insert) l.InsertPbiA0285C(g[1], pbi);
        return (l, g);
    }

    [Fact]
    public void M6_026_1_1_1_4_TheRemovalWalkUndoesEveryCountTheWalkMadeAndDestroysTheIdleLimiters()
    {
        // 1.1: (a) 0x9FACD0 (+0x60--, +0x62-- when block+0xC bit 0), (c) 0x9FA2EC or 0x9FAA70, (e) the bus once (bit 0 cleared, block+0x10 = 0), (f) the parent. 1.4/1.5/1.8: an idle limiter is
        // destroyed. The walk counted S, M and both buses once (W6); the undo leaves nothing, and the limiters are destroyed in walk order S, M, bus 10, bus 11.
        var (l, g) = WalkedChain(out var pbi);
        Assert.Equal((short)1, l.LimiterOf(1)!.Count60);
        var order = new List<uint>();
        l.RtpcUnsubscribeA19F60 = x => order.Add(x.NodeId);
        var block = new WwiseLimitBlock { Priority = 0f, GameObject = 7, Array = null, Word0C = 3, B11 = 1 };
        l.RemoveNode9ED428(g[1], block);
        Assert.Equal(new uint[] { 1, 2, 10, 11 }, order);
        foreach (uint id in new uint[] { 1, 2, 10, 11 }) Assert.Null(l.LimiterOf(id));
        Assert.Equal(2, block.Word0C);                                               // (e): the bit-0 clear persists
        Assert.Equal(0, l.BusCC(10) & 7);                                            // 1.3: limiter gone, no recovery time -> bits = 0
        Assert.Equal(0, l.BusCC(11) & 7);
    }

    [Fact]
    public void M6_026_1_1_TheUndoDecrementsPlus62OnlyForNodesAtAndBelowTheFirstBus()
    {
        // 1.4: +0x62-- when block+0xC bit 0; the walk (W6) incremented +0x62 the same way, so a parent above the first bus-carrying node has +0x62 == 0 and is decremented with the bit cleared.
        var g = new Graph(Snd(1, P(parent: 2, bus: 10)), Mix(2, P(adv0: 0x04, max: 3)), Bus(10, 0));
        var l = Limiter(g);
        var pbi = Pbi(50f, go: 7);
        pbi.NodeE0 = g[1];
        l.Walk(g[1], Block(pbi.LimiterArray1EC, go: 7), true, false);
        Assert.Equal((short)1, l.LimiterOf(1)!.Count62);
        Assert.Equal((short)0, l.LimiterOf(2)!.Count62);
        l.LimiterOf(2)!.Contexts0C.Add(Pbi(50f));                                    // keep M alive so its counters can be read
        l.RemoveNode9ED428(g[1], new WwiseLimitBlock { GameObject = 7, Word0C = 3, B11 = 1 });
        Assert.Equal((short)0, l.LimiterOf(2)!.Count60);
        Assert.Equal((short)0, l.LimiterOf(2)!.Count62);
    }

    [Fact]
    public void M6_026_1_4_TheCountIsAU16AndTheIdleTestIsSigned()
    {
        // 1.4: (u16)+0x60 is stored decremented (wrapping); the idle test reads it as s16.
        var g = new Graph(Snd(1, P()));
        var l = Limiter(g);
        l.Count9FAC54(g[1], false);
        var lim = l.LimiterOf(1)!;
        lim.Count60 = 0;
        lim.Count62 = 0;
        lim.Map.Add(new WwiseLimiterMapEntry { Go = 1 });                            // not idle whatever the count
        l.DecrementCount9FACD0(g[1], isBus: true);
        Assert.Equal((short)-1, lim.Count60);
        Assert.Equal((short)-1, lim.Count62);
        l.DecrementCount9FACD0(g[1], isBus: false);
        Assert.Equal((short)-2, lim.Count60);
        Assert.Equal((short)-1, lim.Count62);
        lim.Map.Clear();
        l.DecrementCount9FACD0(g[1], isBus: false);                                  // (s16)-3 <= 0: idle -> destroyed
        Assert.Null(l.LimiterOf(1));
        l.DecrementCount9FACD0(g[1], isBus: false);                                  // no limiter: returns
    }

    [Fact]
    public void M6_026_1_3_TheBusTailSetsBits0To2ByTheRecoveryGate()
    {
        // 1.3: after the parent bus, if [bus+0x30] != 0 and +0x60 != 0 return; else bits 0..2 = 2 when [bus+0x64] != 0 && [bus+0x84] != 0 && 0x9C5154 == 1, else 0.
        var g = new Graph(Bus(10, 0, recoveryMs: 500));
        var l = Limiter(g);
        l.SetBusCC(10, 0x39);
        Assert.Throws<WwiseMissingBehaviourException>(() => l.RemoveBus9C5240((WwiseBusNode)g[10], new WwiseLimitBlock()));   // 0x9C5154 is unread
        l.Count9FAC54(g[10], true);
        l.BusRecoveryGateA9C5154 = _ => true;
        l.RemoveBus9C5240((WwiseBusNode)g[10], new WwiseLimitBlock());
        Assert.Equal(0x3A, l.BusCC(10));                                             // bits 0..2 = 2, other bits kept
        l.Count9FAC54(g[10], true);
    }

    [Fact]
    public void M6_026_1_3_ABusStillCountedReturnsBeforeTheTail()
    {
        var g = new Graph(Bus(10, 0));
        var l = Limiter(g);
        l.Count9FAC54(g[10], true);
        l.Count9FAC54(g[10], true);                                                  // +0x60 == 2
        l.SetBusCC(10, 0x39);
        l.RemoveBus9C5240((WwiseBusNode)g[10], new WwiseLimitBlock());
        Assert.Equal((short)1, l.LimiterOf(10)!.Count60);
        Assert.Equal(0x39, l.BusCC(10));                                             // untouched
    }

    [Fact]
    public void M6_026_1_10_1_11_TermRunsItsStepsInOrderWithTheCountUndoFirst()
    {
        // 1.10: (1) 0xA01684; (2, 3) cancel +0x144/+0x148; (4) 1BC &= ~2; (5) 0xA04DE8 when [pbi+0x140] != 0; (6, 7); (8) ctx unlink; (9) idle test; (10..13).
        // 1.11: [pbi+0x1F0] = 0 and 1BD bit 5 are set before the seams run.
        var (l, g) = WalkedChain(out var pbi, insert: true);
        var log = new List<string>();
        pbi.Field144 = 9;
        pbi.Item148Id = 0x04000000;
        pbi.Flags1BC = 0x02 | 0x40;
        l.CancelTransitionA36618 = (_, off) => log.Add("cancel" + off.ToString("X"));
        l.ReleasePlayingIdA04DE8 = _ => log.Add("release:" + (pbi.Flags1BC & 2));
        l.TermSteps6And7 = _ => log.Add($"s67:bit5={pbi.Flags1BD & 0x20},array={pbi.LimiterArray1EC.Count},ctx={l.LimiterOf(1)!.Contexts0C.Count}");
        l.TermSteps10To13 = _ => log.Add($"s1013:ctx={l.LimiterOf(1)?.Contexts0C.Count}");
        l.TermPbiA029DC(pbi);
        Assert.Equal(new[] { "cancel144", "cancel148", "release:0", "s67:bit5=32,array=0,ctx=1", "s1013:ctx=" }, log);
        // The limiters of a PBI that was inserted are gone after the undo (counts back to 0, lists empty, contexts removed).
        foreach (uint id in new uint[] { 1, 2, 10, 11 }) Assert.Null(l.LimiterOf(id));
        Assert.Empty(l.GlobalVoiceList.Items);
        Assert.Equal((ushort)0, l.GlobalVoiceList.Count20);
    }

    [Fact]
    public void M6_026_1_11_F3_TheVirtualPartRunsEvenWhenBit5Of1BDIsSet()
    {
        // F3: the 1BE bit 5 part (each list's +0x22--, [G]--) runs regardless; only 1BD bit 5 skips the list removal and the walk.
        var l = Limiter(new Graph());
        var pbi = Pbi(50f);
        var list = new WwisePbiList();
        pbi.LimiterArray1EC.Items.Add(list);
        l.AcquireVirtual0A0228C(pbi);
        pbi.Flags1BD |= 0x20;
        l.UndoCounts0A01684(pbi);
        Assert.Equal((ushort)0, list.Virtual22);
        Assert.Equal(0u, l.GlobalVirtualCount);
        Assert.Equal(1, pbi.LimiterArray1EC.Count);                                  // no removal, no walk (and no node needed)
    }

    [Fact]
    public void M6_026_1_10_TheTermSeamsAreVisibleStopsWhenUnset()
    {
        var (l, g) = WalkedChain(out var pbi);
        l.TermSteps6And7 = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => l.TermPbiA029DC(pbi));
        var (l2, g2) = WalkedChain(out var pbi2);
        l2.TermSteps10To13 = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => l2.TermPbiA029DC(pbi2));
    }

    // ------------------------------------------------------------------ 2.x: 0x9F3528 and 0xA394C0

    [Fact]
    public void M6_026_2_1_2_2_TheRemovalIsByTheThreeKeysAndDecrementsPlus20OnlyWhenFound()
    {
        var l = Limiter(new Graph());
        var list = new WwisePbiList();
        var a = Pbi(70f); var b = Pbi(60f); var c = Pbi(50f);
        foreach (var x in new[] { a, b, c }) l.InsertIntoList9F3274(list, x);
        // A different object with the same three keys removes the entry (search by keys, not by pointer).
        var clone = Pbi(60f, chain: b.ChainId, k4: b.Key1C4);
        l.Remove9F3528(list, clone);
        Assert.Equal(new[] { a, c }, list.Items);
        Assert.Equal((ushort)2, list.Count20);
        // Not found (a different key): nothing changes.
        l.Remove9F3528(list, Pbi(60f));
        Assert.Equal(new[] { a, c }, list.Items);
        Assert.Equal((ushort)2, list.Count20);
    }

    [Fact]
    public void M6_026_2_2_2_3_2_4_TheGlobalRegistrationIsDroppedAtCountZero()
    {
        var l = Limiter(new Graph());
        var list = new WwisePbiList { KeyHi = 1 };
        var a = Pbi(50f);
        l.InsertIntoList9F3274(list, a);
        Assert.Single(l.GlobalLimiterArray);
        l.Remove9F3528(list, a);                                                     // found, count 0: 0xA394C0, then +0x20--
        Assert.Empty(l.GlobalLimiterArray);
        Assert.Empty(list.Items);
        Assert.Equal((ushort)0, list.Count20);
        // 2.3: not found in a list that was already empty: only the 0xA394C0 tail call.
        var empty = new WwisePbiList { KeyHi = 2 };
        l.RegisterList0A39254(empty);
        l.Remove9F3528(empty, a);
        Assert.Empty(l.GlobalLimiterArray);
        Assert.Equal((ushort)0, empty.Count20);
        // Not found in a non-empty list: it stays registered.
        var other = new WwisePbiList { KeyHi = 3 };
        l.InsertIntoList9F3274(other, Pbi(50f));
        l.Remove9F3528(other, Pbi(40f));
        Assert.Single(l.GlobalLimiterArray);
    }

    [Fact]
    public void M6_026_2_1_O9_ANaNPriorityIsFoundAtMidAndRemovesThatEntry()
    {
        // O9: an unordered compare leaves r2 = 0, so a NaN is "found at mid": 0x9F35D0..0x9F35E4 removes the element at mid.
        var l = Limiter(new Graph());
        var list = new WwisePbiList();
        var a = Pbi(70f); var b = Pbi(60f);
        l.InsertIntoList9F3274(list, a);
        l.InsertIntoList9F3274(list, b);
        l.Remove9F3528(list, Pbi(float.NaN));                                        // lo 0, hi 1, mid 0
        Assert.Equal(new[] { b }, list.Items);
    }

    [Fact]
    public void M6_026_2_1_TheRemovalUsesTheAscendingOrderOfAKillNewestList()
    {
        var l = Limiter(new Graph());
        var list = new WwisePbiList { KillNewest = true };
        var ps = Enumerable.Range(0, 6).Select(_ => Pbi(50f)).ToArray();
        foreach (var x in ps) l.InsertIntoList9F3274(list, x);
        Assert.Equal(ps, list.Items);                                                // ascending on (1C8, 1C4): oldest first
        l.Remove9F3528(list, ps[3]);
        Assert.Equal(new[] { ps[0], ps[1], ps[2], ps[4], ps[5] }, list.Items);
    }

    // ------------------------------------------------------------------ 8.x: reposition, SetPriority, CalcEffectiveParams

    private static WwisePlayingInstance[] Model(bool killNewest, IEnumerable<WwisePlayingInstance> items, WwisePlayingInstance moved, float newPrio)
    {
        // The emulation contract of 0x9F36A0 (missing report 8.1): remove the entry, then place it at the sorted position of (newPrio, 1C8, 1C4): descending priority,
        // ties descending on (1C8, 1C4) when [list+0xE] == 0 and ascending otherwise. Written as a sort, not as the engine's search.
        float Prio(WwisePlayingInstance x) => ReferenceEquals(x, moved) ? newPrio : x.Priority1C0;
        var all = items.ToList();
        var ordered = all.OrderByDescending(Prio);
        ordered = killNewest
            ? ordered.ThenBy(x => x.ChainId).ThenBy(x => x.Key1C4)
            : ordered.ThenByDescending(x => x.ChainId).ThenByDescending(x => x.Key1C4);
        return ordered.ToArray();
    }

    [Fact]
    public void M6_026_8_1_RepositionEqualsRemoveThenSortedReinsertForRandomLists()
    {
        var rng = new Random(20260930);
        for (int trial = 0; trial < 400; trial++)
        {
            bool kn = rng.Next(2) == 0;
            var l = Limiter(new Graph());
            var list = new WwisePbiList { KillNewest = kn };
            int n = rng.Next(1, 12);
            var ps = new List<WwisePlayingInstance>();
            for (int i = 0; i < n; i++)
            {
                var x = Pbi(rng.Next(0, 5) * 10f + 30f);                              // few priorities, so ties are common
                ps.Add(x);
                l.InsertIntoList9F3274(list, x);
            }
            var moved = ps[rng.Next(n)];
            float newPrio = rng.Next(0, 7) * 10f + 20f;
            var expected = Model(kn, list.Items, moved, newPrio);
            ushort count20 = list.Count20;
            float old = moved.Priority1C0;
            l.Reposition9F36A0(list, newPrio, moved);
            Assert.Equal(expected, list.Items);
            Assert.Equal(count20, list.Count20);                                     // unchanged
            Assert.Equal(old, moved.Priority1C0);                                    // 0x9F36A0 does not store the priority
        }
    }

    [Fact]
    public void M6_026_8_1_ARepositionOfAnEntryThatIsNotInTheListChangesNothing()
    {
        var l = Limiter(new Graph());
        var list = new WwisePbiList();
        var a = Pbi(70f); var b = Pbi(60f);
        l.InsertIntoList9F3274(list, a);
        l.InsertIntoList9F3274(list, b);
        l.Reposition9F36A0(list, 10f, Pbi(60f));
        Assert.Equal(new[] { a, b }, list.Items);
    }

    [Fact]
    public void M6_026_8_3_SetPriorityRepositionsEveryListThenStoresAndAnEqualPriorityReturns()
    {
        var l = Limiter(new Graph());
        var l1 = new WwisePbiList(); var l2 = new WwisePbiList();
        var other = Pbi(55f);
        var mover = Pbi(50f);
        foreach (var list in new[] { l1, l2 })
        {
            l.InsertIntoList9F3274(list, other);
            l.InsertIntoList9F3274(list, mover);
            mover.LimiterArray1EC.Items.Add(list);
        }
        Assert.Equal(new[] { other, mover }, l1.Items);
        l.SetPriorityA01A08(mover, 50f);                                             // equal: return
        Assert.Equal(new[] { other, mover }, l1.Items);
        l.SetPriorityA01A08(mover, 60f);
        Assert.Equal(new[] { mover, other }, l1.Items);
        Assert.Equal(new[] { mover, other }, l2.Items);
        Assert.Equal(60f, mover.Priority1C0);
        l.SetPriorityA01A08(mover, float.NaN);                                       // unordered is not equal: it runs and stores
        Assert.True(float.IsNaN(mover.Priority1C0));
    }

    [Fact]
    public void M6_026_8_4_ThePriorityBlockOfCalcEffectiveParamsStoresOnceAndRepositionsOnAChange()
    {
        // 8.4: out = 0x9F6B94; equal to [pbi+0x1CC] and [pbi+0x1D0] -> nothing; else store both, and when the priority differs from [pbi+0x1C0] reposition every list and store it.
        var g = new Graph(Snd(1, P(bits: 1, props: new Dictionary<byte, uint> { [7] = 0x42700000 })));   // priority 60.0f
        var l = Limiter(g);
        var list = new WwisePbiList();
        var other = Pbi(55f);
        var pbi = Pbi(50f);
        pbi.NodeE0 = g[1];
        l.InsertIntoList9F3274(list, other);
        l.InsertIntoList9F3274(list, pbi);
        pbi.LimiterArray1EC.Items.Add(list);
        l.PriorityRefresh9FFE1C(pbi);
        Assert.Equal(0x42700000, BitConverter.SingleToInt32Bits(pbi.Priority1C0));
        Assert.Equal(0x42700000, BitConverter.SingleToInt32Bits(pbi.Field1CC));
        Assert.Equal(new[] { pbi, other }, list.Items);
        pbi.Priority1C0 = 50f;                                                       // a second call with 1CC/1D0 equal does nothing
        l.PriorityRefresh9FFE1C(pbi);
        Assert.Equal(50f, pbi.Priority1C0);
    }

    [Fact]
    public void M6_026_8_4_CalcEffectiveParamsRunsThePriorityBlockOnTheFullPathButNotOnTheEarlyExit()
    {
        // 0x9FFE1C is reached after the mute/fade store at 0x9FFE18; the E9-bit-2 early exit (0x9FFC30) returns before it (R3.1). Expected: the default priority 50.0f (0x42480000) lands in 1CC and 1C0.
        var g = new Graph(Snd(1, P()));
        var l = Limiter(g);
        var path = new WwisePlayPath(l.ParentNode, new WwisePlaySeams { FirstOutputBus9F4BB8 = n => n, BusFlag9C54E8 = _ => true, NodeVtAC = _ => { } });
        var open = Pbi(20f);
        open.NodeE0 = g[1];
        path.CalcEffectiveParams(open, null, l);
        Assert.Equal(0x42480000, BitConverter.SingleToInt32Bits(open.Field1CC));
        Assert.Equal(0x42480000, BitConverter.SingleToInt32Bits(open.Priority1C0));
        var early = Pbi(20f);
        early.NodeE0 = g[1];
        early.Flags0E9 = 4;
        path.CalcEffectiveParams(early, null, l);
        Assert.Equal(0, BitConverter.SingleToInt32Bits(early.Field1CC));                        // untouched
        Assert.Equal(1, early.Flags1BC & 1);                                                     // 0x9FFC30
        Assert.Equal(0x20, early.Flags0E8 & 0x20);
    }

    // ------------------------------------------------------------------ 3.x: the below byte

    [Theory]
    [InlineData(0f, 0x3F7FC105)]
    [InlineData(-20f, 0x3DCD44E6)]
    [InlineData(-60f, 0x3A82DD8E)]
    [InlineData(-80f, 0x38D2306A)]
    [InlineData(-100f, 0x3727C189)]
    [InlineData(6f, 0x3FFED5D6)]
    [InlineData(-1000f, 0)]
    public void M6_026_3_2_LinIsTheFastPolynomialWithNonFusedMultiplyAdds(float x, int expectedBits)
    {
        // Expected bits computed from the row 3.2 formula in an independent float32 evaluation (Python struct rounding after every operation), not from this code.
        // x*0.05f < -37.0f gives 0 (x = -1000 -> -50).
        Assert.Equal(expectedBits, BitConverter.SingleToInt32Bits(WwisePlaybackLimiter.Lin9BEB30(x)));
    }

    [Fact]
    public void M6_026_3_2_TheLinPoolConstantsMatchTheBinaryBytes()
    {
        // 0x9BEEE4..0x9BEEFC: 0.05f, -37.0f, 0x4BD49A78, 0x4E7E0000, 0x3EA67F46, 0x3CAA70DE, 0x3F272DDB, read from libcozmoEngine.so through its ELF load segments. Absent binary: nothing to compare.
        string? so = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null && so is null; d = d.Parent)
        {
            var c = Path.Combine(d.FullName, "resources", "lib", "armeabi-v7a", "libcozmoEngine.so");
            if (File.Exists(c)) so = c;
        }
        if (so is null) return;
        var bytes = File.ReadAllBytes(so);
        uint phoff = BitConverter.ToUInt32(bytes, 0x1C);
        int phentsize = BitConverter.ToUInt16(bytes, 0x2A), phnum = BitConverter.ToUInt16(bytes, 0x2C);
        uint Word(uint va)
        {
            for (int i = 0; i < phnum; i++)
            {
                int ph = (int)phoff + i * phentsize;
                if (BitConverter.ToUInt32(bytes, ph) != 1) continue;                     // PT_LOAD
                uint off = BitConverter.ToUInt32(bytes, ph + 4), vaddr = BitConverter.ToUInt32(bytes, ph + 8), filesz = BitConverter.ToUInt32(bytes, ph + 16);
                if (va >= vaddr && va + 4 <= vaddr + filesz) return BitConverter.ToUInt32(bytes, (int)(off + va - vaddr));
            }
            throw new InvalidOperationException($"0x{va:X} is not in a file-backed load segment");
        }
        Assert.Equal(new uint[] { 0x3D4CCCCD, 0xC2140000, 0x4BD49A78, 0x4E7E0000, 0x3EA67F46, 0x3CAA70DE, 0x3F272DDB },
            Enumerable.Range(0, 7).Select(k => Word(0x9BEEE4 + (uint)k * 4)).ToArray());
    }

    [Fact]
    public void M6_026_3_2_TheThresholdIsTwoToTheMinus16AndAnUnorderedProductIsNotBelow()
    {
        Assert.Equal(0x37800000, BitConverter.SingleToInt32Bits(1f / 65536f));
        var pbi = Pbi(50f);
        Assert.Throws<WwiseMissingBehaviourException>(() => WwisePlaybackLimiter.Below9BEB30(pbi));   // pbi+0x64 has no default (0x9FFDF0..0x9FFEB0)
        pbi.Word64 = 0f;
        pbi.Volume3C = -100f;                                                        // lin ~ 1.0e-5 <= 2^-16 (1.5e-5)
        Assert.True(WwisePlaybackLimiter.Below9BEB30(pbi));
        pbi.Volume3C = -80f;                                                         // ~1.0e-4 > 2^-16
        Assert.False(WwisePlaybackLimiter.Below9BEB30(pbi));
        pbi.Volume3C = -1000f;                                                       // lin 0 -> product 0
        Assert.True(WwisePlaybackLimiter.Below9BEB30(pbi));
        pbi.Volume3C = 0f;
        pbi.MuteFade40 = float.NaN;                                                  // vcmpe unordered: movls not taken
        Assert.False(WwisePlaybackLimiter.Below9BEB30(pbi));
        pbi.MuteFade40 = 0f;
        Assert.True(WwisePlaybackLimiter.Below9BEB30(pbi));
        pbi.MuteFade40 = 1f;
        pbi.Word64 = -100f;                                                          // ctx+0x58 is the third factor
        Assert.True(WwisePlaybackLimiter.Below9BEB30(pbi));
    }

    // ------------------------------------------------------------------ the live entry: WwiseEventRuntime -> WwisePlaybackBridge.PlaySound

    private static void U32(List<byte> b, uint v) => b.AddRange(BitConverter.GetBytes(v));
    private static void U16(List<byte> b, ushort v) => b.AddRange(BitConverter.GetBytes(v));

    private static byte[] Chunk(string tag, byte[] body)
    {
        var b = new List<byte>();
        b.AddRange(System.Text.Encoding.ASCII.GetBytes(tag));
        U32(b, (uint)body.Length);
        b.AddRange(body);
        return b.ToArray();
    }

    private static byte[] BankFile(params (byte Type, byte[] Payload)[] objects)
    {
        var bkhd = new List<byte>();
        U32(bkhd, 120); U32(bkhd, 1); U32(bkhd, 0); U32(bkhd, 0); U32(bkhd, 0);
        var h = new List<byte>();
        U32(h, (uint)objects.Length);
        foreach (var (type, payload) in objects) { h.Add(type); U32(h, (uint)payload.Length); h.AddRange(payload); }
        var file = new List<byte>();
        file.AddRange(Chunk("BKHD", bkhd.ToArray()));
        file.AddRange(Chunk("HIRC", h.ToArray()));
        return file.ToArray();
    }

    private static void NodeBlockEx(List<byte> b, uint parent, byte bits, byte adv0, ushort max, (byte Id, uint Value)[]? props = null, byte adv3 = 0)
    {
        props ??= Array.Empty<(byte, uint)>();
        b.Add(0); b.Add(0); b.Add(0);
        U32(b, 0); U32(b, parent);
        b.Add(bits);
        b.Add((byte)props.Length);
        foreach (var (id, _) in props) b.Add(id);
        foreach (var (_, v) in props) U32(b, v);
        b.Add(0); b.Add(0); b.Add(0);                                                // ranged, positioning, aux
        b.Add(adv0); b.Add(0); U16(b, max); b.Add(adv3); b.Add(0);                  // the 6-byte advanced block
        U32(b, 0);
        U16(b, 0);
    }

    private static (byte, byte[]) SoundObj(uint id, uint parent, byte bits = 0, (byte, uint)[]? props = null, byte adv0 = 0, byte adv3 = 0)
    {
        var b = new List<byte>();
        U32(b, id); U32(b, WwiseSourceFactory.VorbisPlugin); b.Add(1); U32(b, 12345); U32(b, 0); b.Add(0);
        NodeBlockEx(b, parent, bits, adv0, 0, props, adv3);
        return (2, b.ToArray());
    }

    private static (byte, byte[]) MixerObj(uint id, byte adv0, ushort max)
    {
        var b = new List<byte>();
        U32(b, id);
        NodeBlockEx(b, 0, 0, adv0, max);
        U32(b, 0);                                                                   // children
        return (7, b.ToArray());
    }

    private static (byte, byte[]) PlayAction(uint actionId, uint target)
    {
        var a = new List<byte>();
        U32(a, actionId); U16(a, 0x0403); U32(a, target); a.Add(0);
        a.Add(0); a.Add(0);                                                          // props, ranged
        a.Add(3); U32(a, 0);                                                         // play params: curve 3, bank 0
        return (3, a.ToArray());
    }

    private static (byte, byte[]) EventObj(uint eventId, uint actionId)
    {
        var e = new List<byte>();
        U32(e, eventId); U32(e, 1); U32(e, actionId);
        return (4, e.ToArray());
    }

    private sealed class Rig
    {
        public readonly WwiseEventRuntime Runtime;
        public readonly WwisePlaybackBridge Bridge;
        public readonly WwisePlaybackLimiter Limiter;
        public WwisePlayingInstance? LastPbi;
        public bool InitResult = true;
        public Action<WwisePlayingInstance>? OnInit;
        public readonly List<string> Log = new();

        public Rig(IEnumerable<WwiseBank> banks, ushort? stmgMaxVoices = null)
        {
            Runtime = WwiseEndOfEventDoubles.Runtime(banks, new WwiseRng(1));
            Limiter = WwisePlaybackLimiterTestDoubles.Create(Runtime.FindNode);
            if (stmgMaxVoices is { } m) Limiter.ApplyStmgMaxVoices(m);
            // 0xA04D48 / 0xA04DE8 take and release a reference on the playing-id entry (6.2, 1.10 step 5); the event runtime owns the table.
            Limiter.RegisterPlayingIdA04D48 = p => { Log.Add("04D48:" + p.PlayingId); Runtime.RegisterPbiPlayingId(p); };
            Limiter.ReleasePlayingIdA04DE8 = p => { Log.Add("04DE8:" + p.PlayingId); Runtime.ReleasePbiPlayingIdReference(p.PlayingId); };
            Runtime.PlayingIds.Seams.GameObjectLookupA0C238 = _ => null;            // doubles: 0xA0C238 and the teardown bodies 0xA03618 calls are unread; A1C660 records the entry that reached zero
            Runtime.PlayingIds.Seams.A1C660 = item => ZeroEntries.Add(item.PlayingId);
            Runtime.PlayingIds.Seams.A1C65C = _ => { };
            Bridge = new WwisePlaybackBridge
            {
                Limiter = Limiter,
            }.WithTestSeams();
            // The shipped path (C32.1): node vt+0xAC is the unread GetAudioParameters, so the double is where the tests tweak the PBI before the effective parameters are composed.
            // A failed init (OnInit with InitResult = false) is the engine's own failure: Volume -100 dB makes the below byte true, which fails a play whose flag is 0.
            Bridge.PlayPath!.Seams.NodeVtAC = a => { LastPbi = a.Pbi; OnInit?.Invoke(a.Pbi); if (!InitResult) a.Pbi.Volume3C = -100f; };
            Bridge.NextSource9EEDA4 = null;                        // the read 0x9EEDA4 body decides (the test seams install an override)
            Runtime.PlaybackBridge = Bridge;
            Runtime.RegisterGameObject(7);
        }

        public readonly List<uint> ZeroEntries = new();

        public uint Play(uint eventId, uint go = 7)
        {
            uint id = Runtime.PostEvent(eventId, go);
            Runtime.AdvanceFrame();
            return id;
        }

        /// <summary>Calls the bridge's Play entry for a posted (not yet pumped) id, so the playing-id entry exists at reference count 1.</summary>
        public uint PlayDirect(uint soundId, uint eventId, uint? playingId = null, ushort? chain = null, uint go = 7, Action<WwisePlayInitParams>? tweak = null)
        {
            uint id = playingId ?? Runtime.PostEvent(eventId, go);
            var node = Runtime.FindNode(soundId)!;
            var p = new WwisePlayInitParams { PlayingId = id, TargetNodeId = soundId, GameObjectId = go, ChainId = chain ?? 0 };
            tweak?.Invoke(p);
            Bridge.OnPlay(node, id, go, p);
            return id;
        }
    }

    private static WwiseBank Synthetic(params (byte Type, byte[] Payload)[] objects) => WwiseBank.Parse(BankFile(objects), "t.bnk");

    [Fact]
    public void M6_026_P4_C28_1_ASecondPlayUnderAMaxOneGlobalMixerKillsTheFirstThroughTheLiveEntry()
    {
        // Mixer 200 (advanced byte0 0x04 = global, max 1) over Sounds 500 and 501; the same shape as the shipped Cozmo mixer 62050212 (C28.8).
        var bank = Synthetic(
            MixerObj(200, 0x04, 1), SoundObj(500, 200), SoundObj(501, 200),
            PlayAction(100, 500), EventObj(900, 100), PlayAction(101, 501), EventObj(901, 101));
        var rig = new Rig(new[] { bank });

        uint first = rig.Play(900);
        Assert.Equal(new[] { first }, rig.Bridge.Instances.Select(p => p.PlayingId).ToArray());
        var pbi1 = rig.Bridge.Instances[0];
        Assert.Equal(0x42480000, BitConverter.SingleToInt32Bits(pbi1.Priority1C0));  // P2a: the default 50.0f
        Assert.Equal(0x45, pbi1.Flags1BD);                                           // nothing marked yet (first play: G3 max 1 <= 0 is false)
        Assert.Equal(0, pbi1.Flags1BC & 0x20);
        Assert.Equal((ushort)1, rig.Limiter.LimiterOf(200)!.List.Count20);           // R1 inserted it into the mixer's list
        Assert.Equal((ushort)1, rig.Limiter.GlobalVoiceList.Count20);
        Assert.Equal(new[] { pbi1 }, rig.Limiter.GlobalPbiList);                     // C28.5: appended to the global PBI list

        uint second = rig.Play(901);
        var pbi2 = rig.Bridge.Instances[1];
        Assert.Equal(second, pbi2.PlayingId);
        // K1: reason 1 in bits 2..4 and bit 1; K3 -> K6: 1BC bit 6, bit 5, bit 3.
        Assert.Equal(0x47, pbi1.Flags1BD);  // 1BD bit 0 from 0xA00618 (0xA00640)
        Assert.Equal(0x40 | 0x20 | 0x08 | 0x01, pbi1.Flags1BC);  // 1BC bit 0 from CalcEffectiveParams (0x9FFC30)
        Assert.Equal(0x45, pbi2.Flags1BD);                                           // the newcomer plays
        Assert.Equal(0, pbi2.Flags1BC & 0x20);
        Assert.Equal((ushort)2, rig.Limiter.LimiterOf(200)!.List.Count20);           // 0xA01CA4 does not touch the lists (K6)
        Assert.Equal(2, rig.Limiter.GlobalPbiList.Count);
        Assert.Equal(2, rig.Bridge.StartList.Nodes.Count);                           // both were queued for a voice
        // 0xA01CA4 posts no message and starts nothing else: the victim's start-list node is still there and the newcomer's was added.
    }

    private static WwiseBank OneMixerBank(byte adv0, ushort max, byte soundAdv0 = 0, byte soundAdv3 = 0)
        => Synthetic(MixerObj(200, adv0, max), SoundObj(500, 200, adv0: soundAdv0, adv3: soundAdv3),
            PlayAction(100, 500), EventObj(900, 100));

    [Fact]
    public void M6_026_C29_2_ExitA_AKillNewestMixerRejectsAndTermUndoesTheCountsInOrder()
    {
        // Exit (a): the walk returns 2 -> 0xA37E7C: 0xA04D48 first (+1 on the playing-id entry), then Term: 0xA01684 removes the PBI from its lists and walks the node back (S, M, buses), 0xA04DE8 balances.
        var rig = new Rig(new[] { OneMixerBank(0x05, 1) });
        uint first = rig.Play(900);
        var s = rig.Limiter.LimiterOf(500)!; var m = rig.Limiter.LimiterOf(200)!;
        (short, short, short, short) before = (s.Count60, s.Count62, m.Count60, m.Count62);
        Assert.Equal(((short)1, (short)1, (short)1, (short)1), before);
        rig.Log.Clear();

        uint second = rig.Play(900);

        Assert.Equal(new[] { first }, rig.Bridge.Instances.Select(p => p.PlayingId).ToArray());   // the failed PBI is freed
        Assert.Single(rig.Bridge.StartList.Nodes);                                   // and never queued
        Assert.Equal(new[] { first }, rig.Limiter.GlobalPbiList.Select(p => p.PlayingId).ToArray());
        Assert.Equal((s.Count60, s.Count62, m.Count60, m.Count62), before);          // 1.4: the +0x60/+0x62 the walk added are gone again
        Assert.Equal((ushort)1, m.List.Count20);                                     // never inserted, so 0x9F3528 found nothing to decrement
        Assert.Equal(0, rig.Bridge.Instances[0].Flags1BD & 2);                       // the first PBI was not killed
        Assert.Equal(new[] { $"04D48:{second}", $"04DE8:{second}" }, rig.Log);       // 0xA37E7C first, Term's release last
        Assert.NotEqual(0, rig.LastPbi!.Flags1BD & 0x20);                            // 1.11: Term set bit 5 when it ran the undo
        Assert.Empty(rig.LastPbi.LimiterArray1EC.Items);                             // and [pbi+0x1F0] = 0
    }

    [Fact]
    public void M6_026_C29_2_ExitA_TheLonePlayLeavesNoLimiterBehindAndTheReferenceIsBalanced()
    {
        // A first Play cannot be rejected by a limit, so a virtual mixer with a code-1 Sound (flag [sp+0x1C] = 0) makes a 0x50 reject: the Play that follows a higher-priority one fails
        // as a lone... here the lone case is the second Play removing its own entries; the first stays. Balance is checked on the playing-id entry.
        var bank = Synthetic(
            MixerObj(200, 0x06, 1),
            SoundObj(500, 200, bits: 1, props: new[] { ((byte)7, 0x42700000u) }),    // 60.0f
            SoundObj(501, 200, adv0: 0x10, adv3: 1),                                 // behaviour code 1: r6 = 1, [sp+0x1C] = 0 (3.5)
            PlayAction(100, 500), EventObj(900, 100), PlayAction(101, 501), EventObj(901, 101));
        var rig = new Rig(new[] { bank });
        rig.PlayDirect(500, 900);
        uint id2 = rig.Runtime.PostEvent(901, 7);
        Assert.Equal(1, rig.Runtime.OutstandingActionCount(id2));
        rig.PlayDirect(501, 901, id2);                                               // walk 0x50 with [sp+0x1C] == 0: fail (exit a)
        Assert.Single(rig.Bridge.Instances);
        Assert.Equal(1, rig.Runtime.OutstandingActionCount(id2));                    // 0xA04D48 +1 then 0xA04DE8 -1
        Assert.Equal((short)1, rig.Limiter.LimiterOf(200)!.Count60);
    }

    [Fact]
    public void M6_026_C29_2_ExitB_AZeroPlayingIdFailsAfterTheInsertAndTermRemovesEverything()
    {
        // Exit (b): 0xA0285C returns 2 for [pbi+0x140] == 0 after the inserts; no 0xA04D48/0xA04DE8. Term finds the PBI in its lists, undoes the counts and, the Play being alone, every limiter is destroyed.
        var rig = new Rig(new[] { OneMixerBank(0x04, 3) });
        rig.PlayDirect(500, 900, playingId: 0);
        Assert.Empty(rig.Bridge.Instances);
        Assert.Empty(rig.Log);
        foreach (uint id in new uint[] { 500, 200 }) Assert.Null(rig.Limiter.LimiterOf(id));
        Assert.Empty(rig.Limiter.GlobalVoiceList.Items);
        Assert.Equal((ushort)0, rig.Limiter.GlobalVoiceList.Count20);
        Assert.Empty(rig.Limiter.GlobalPbiList);
        Assert.Equal(0, rig.Bridge.StartList.Nodes.Count);
        Assert.NotEqual(0, rig.LastPbi!.Flags1BD & 0x20);
    }

    [Fact]
    public void M6_026_C29_2_ExitC_AFailedStartListEnqueueTermsThePbiThatIsAlreadyInTheLists()
    {
        // Exit (c): 0xA0067C returns != 1 (0x9D3558 out of memory) -> 0xA37AD8 straight to Term: no 0xA04D48 there (the one from 0xA0285C stands) and Term's 0xA04DE8 balances it.
        var rig = new Rig(new[] { OneMixerBank(0x04, 3) });
        rig.Bridge.StartList.AllocationFails = () => true;
        uint id = rig.Runtime.PostEvent(900, 7);
        rig.PlayDirect(500, 900, id);
        Assert.Empty(rig.Bridge.Instances);
        Assert.Equal(new[] { $"04D48:{id}", $"04DE8:{id}" }, rig.Log);
        Assert.Equal(1, rig.Runtime.OutstandingActionCount(id));
        foreach (uint node in new uint[] { 500, 200 }) Assert.Null(rig.Limiter.LimiterOf(node));
        Assert.Empty(rig.Limiter.GlobalPbiList);                                     // 6.7 was not reached
        Assert.Empty(rig.Bridge.StartList.Nodes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void M6_026_C29_2_ExitD_ACreationInitFailureSetsBit5WhateverTheBehaviourCode(int code)
    {
        // Exit (d): 0xA37D24 bne 0xA37E78, and 0xA37E78 is `mov r6,#0` unconditionally, so r6 is 0 whatever the 0x9EEDA4 code was: 0xA37AC8 (ldrbeq ..; orreq #0x20) sets 1BD bit 5
        // and Term does no undo (nothing was counted). 0xA04D48 still runs and Term's release balances it.
        var rig = new Rig(new[] { OneMixerBank(0x04, 3, soundAdv0: 0x10, soundAdv3: (byte)code) });
        rig.Bridge.NodeVt10A379D8 = _ => 0;                                          // code 3: the unread vt+0x10 / vt+0x120 bodies (either result leaves r6 irrelevant here)
        rig.Bridge.NodeVt120A379D8 = (_, _) => 0;
        rig.PlayDirect(500, 900);                                                    // a first Play that keeps its counts
        var s = rig.Limiter.LimiterOf(500)!; var m = rig.Limiter.LimiterOf(200)!;
        rig.InitResult = false;
        rig.Log.Clear();
        uint id = rig.Runtime.PostEvent(900, 7);
        rig.PlayDirect(500, 900, id);
        Assert.Single(rig.Bridge.Instances);
        Assert.Equal((short)1, s.Count60);                                           // an undo would have made these 0
        Assert.Equal((short)1, m.Count60);
        Assert.Equal((ushort)1, m.List.Count20);
        Assert.Equal(new[] { $"04D48:{id}", $"04DE8:{id}" }, rig.Log);
        Assert.Equal(1, rig.Runtime.OutstandingActionCount(id));
        Assert.NotEqual(0, rig.LastPbi!.Flags1BD & 0x20);
    }

    [Fact]
    public void M6_026_3_3_TheFlagZeroPathFailsThePlayWithCode0x29WhenBelowIsSet()
    {
        // 0x9BED50..0x9BED7C with r6 = 1 (0x9BEB74, the shipped path): r7 = ([sp+0x1C] < 1) ? below : 0. With [sp+0x1C] == 0 (behaviour code 1) and below set the init returns 3 with code 0x29 and the
        // Play fails (exit d, 0xA37D20); with below clear it cannot fail; with [sp+0x1C] == 1 (code 2) below never fails the Play. (Verified against the engine: emu_play.py B2.)
        var clear = new Rig(new[] { OneMixerBank(0x04, 3, soundAdv0: 0x10, soundAdv3: 1) });
        clear.PlayDirect(500, 900);
        Assert.Single(clear.Bridge.Instances);
        var set = new Rig(new[] { OneMixerBank(0x04, 3, soundAdv0: 0x10, soundAdv3: 1) });
        set.OnInit = pbi => pbi.Volume3C = -100f;
        set.PlayDirect(500, 900);
        Assert.Empty(set.Bridge.Instances);
        Assert.NotEqual(0, set.LastPbi!.Flags1BD & 0x20);                            // exit (d): bit 5 preset, Term does no undo
        var one = new Rig(new[] { OneMixerBank(0x04, 3, soundAdv0: 0x10, soundAdv3: 2) });
        one.OnInit = pbi => pbi.Volume3C = -100f;
        one.PlayDirect(500, 900);
        Assert.Single(one.Bridge.Instances);
    }

    [Fact]
    public void M6_026_P2_Vt84_TheOutFloatIsScaledByTheGameObjectField64OnlyWhenTheGateIsSet()
    {
        // 0xA37A3C stores 0 to [sp+0x2C]; vt+0x84(node, &[sp+0x2C]) returns only a gate; 0xA37A70..0xA37A8C: gate != 0 -> [sp+0x2C] = [sb+0x64] * [sp+0x2C] as floats; 0xA37CEC hands
        // [sp+0x2C] to 0x9BEB30 as r1, which stores it at pbi+0xE4. Expected bits are the engine's arithmetic: 3.5f = 0x40600000, 2.0f * 3.5f = 7.0f = 0x40E00000.
        var rig = new Rig(new[] { OneMixerBank(0x04, 3) });                         // no override: 0x9F1F80 returns (gate 0, out 0.0f) for every parsed node (P4)
        rig.PlayDirect(500, 900);
        Assert.Equal(0u, rig.LastPbi!.FieldE4);                                      // r1 of 0x9BEB30 = [sp+0x2C] = 0.0f

        var ungated = new Rig(new[] { OneMixerBank(0x04, 3) });
        ungated.Bridge.NodeVt84A9F1F80 = _ => (false, 3.5f);
        ungated.Bridge.GameObjectField64A37A80 = _ => throw new InvalidOperationException("the field is read only when the gate is set");
        ungated.PlayDirect(500, 900);
        Assert.Equal(0x40600000u, ungated.LastPbi!.FieldE4);                        // gate 0: the out value unscaled

        var gated = new Rig(new[] { OneMixerBank(0x04, 3) });
        gated.Bridge.NodeVt84A9F1F80 = _ => (true, 3.5f);
        uint? asked = null;
        gated.Bridge.GameObjectField64A37A80 = o => { asked = o; return 2f; };
        gated.PlayDirect(500, 900);
        Assert.Equal(0x40E00000u, gated.LastPbi!.FieldE4);                          // gate 1: out * [obj+0x64]
        Assert.Equal(7u, asked);                                                     // sb = [params+8], the game object

        var unset = new Rig(new[] { OneMixerBank(0x04, 3) });
        unset.Bridge.NodeVt84A9F1F80 = _ => (true, 1f);
        Assert.Throws<WwiseMissingBehaviourException>(() => unset.PlayDirect(500, 900));   // [obj+0x64] has no source

        var bank = Synthetic(MixerObj(200, 0x04, 3), SoundObj(500, 200, bits: 3), PlayAction(100, 500), EventObj(900, 100));   // offset -10.0f (NodeBase bits 0 and 1)
        var direct = new Rig(new[] { bank });
        direct.Bridge.NodeVt84A9F1F80 = _ => (false, 0f);                            // offset non-zero, gate clear: the direct path
        direct.PlayDirect(500, 900);
        Assert.Equal(unchecked((int)0xC1200000), BitConverter.SingleToInt32Bits(direct.LastPbi!.Field1D0));
        var distance = new Rig(new[] { bank });
        distance.Bridge.NodeVt84A9F1F80 = _ => (true, 1f);
        distance.Bridge.GameObjectField64A37A80 = _ => 1f;
        Assert.Throws<WwiseMissingBehaviourException>(() => distance.PlayDirect(500, 900));   // gate set and offset non-zero: 0xA37BA8..0xA37C5C
    }

    [Fact]
    public void M6_026_0xA37C90_TheSourceStructHalfwordOf8TakesTheUnreadExternalSourceBranch()
    {
        var rig = new Rig(new[] { OneMixerBank(0x04, 3) });
        rig.PlayDirect(500, 900);                                                    // the default is plugin >> 16 of the Sound's source block (P7): 0x00040001 gives 4
        Assert.Single(rig.Bridge.Instances);
        rig.Bridge.SourceStructField16A37C90 = _ => 8;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.PlayDirect(500, 900));
    }

    [Fact]
    public void M6_026_O5_TheType1JoinRunsItsCallsInOrderBeforeTheStartListResultIsUsed()
    {
        // 0xA006F8..0xA00738: 0xA366AC when [pbi+0x144] != 0, 0x9BDA28(pbi+0xC, 1) always, 0x9E808C when [pbi+0x34] != 0; only the type-1 branch (params+0x70 == 1) has them. 0x9BDA28 returns at once
        // when [ctx+0xA0] == 0 and otherwise needs the unread 0x9FD8C0 (flag 1); 0x9E808C walks the V28 list of [pbi+0x34].
        var rig = new Rig(new[] { OneMixerBank(0x04, 3) });
        var log = new List<string>();
        var seams = rig.Bridge.PlayPath!.Seams;
        seams.A9FD8C0 = (_, flag) => log.Add("9BDA28:" + flag);
        var record = new WwiseListRecord { Counter58 = 4, Limit50 = 6, Object30Word8 = 0x77, State48 = 1 };
        seams.RecordsOf34 = _ => { log.Add("9E808C"); return new[] { record }; };
        seams.TransitionInit9A35D44 = (_, _, _) => 1;
        var manager = new WwiseTransitionManager(seams);
        rig.Bridge.PlayPath.Transitions = manager;
        var pbi = Pbi(50f);
        var p = new WwisePlayInitParams { PlayingId = 1, Flag70 = 1 };
        rig.Bridge.PbiPlay(pbi, p);
        Assert.Empty(log);                                                           // [ctx+0xA0] == 0: 0x9BDA28 returns at once; [pbi+0x34] == 0: no 0x9E808C
        pbi.Field0AC = new object(); pbi.Field34 = 1;
        var item = new WwiseTransitionItem { State30 = 1 };
        var fade = new WwisePlayInitParams { PlayingId = 1, Flag70 = 1, Transition = new WwiseFadeInTransition { FadeInTime = 100f } };
        pbi.Field144 = 0;
        rig.Bridge.PbiPlay(pbi, fade);                                               // the fade creates the item (state 1), then the join: 0xA366AC (1 -> 2), 0x9BDA28, 0x9E808C
        Assert.Equal(new[] { "9BDA28:True", "9E808C" }, log);
        Assert.Equal(2, manager.Item(pbi.Field144)!.State30);
        Assert.Equal(5, record.Counter58);                                           // 0x9E80AC: [rec+0x58]++
        Assert.Equal(1, record.State48);
        record.Limit50 = 5;                                                          // the counter reaches the limit on the next pass: [rec+0x4C] = [[rec+0x30]+8], [rec+0x30] = 0
        log.Clear();
        rig.Bridge.PbiPlay(pbi, p);
        Assert.Equal(6, record.Counter58);
        Assert.Equal(0x77u, record.Word4C);
        Assert.Null(record.Object30Word8);
        log.Clear();
        rig.Bridge.PbiPlay(pbi, new WwisePlayInitParams { PlayingId = 1, Flag70 = 0 });       // type 0: none of them
        Assert.Empty(log);
        seams.A9FD8C0 = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Bridge.PbiPlay(pbi, p));
    }

    [Fact]
    public void M6_026_1_10_TheReleaseDecrementsAndReachingZeroRunsTheA03618BodyWithItsUnreadCallees()
    {
        // 0xA04DE8 decrements [entry+0x18] then tail-calls 0xA03618: nothing while either counter is non-zero; at zero the entry is unlinked and torn down, which needs the unread callees
        // (0xA0C238, 0xA1C660, 0xA1C65C) and the unwritten [item+0x20]/[item+0x24], and throws without them.
        var runtime = new WwiseEventRuntime(new[] { OneMixerBank(0x04, 3) }, new WwiseRng(1));
        uint id = runtime.PostEvent(900, null);
        var holder = PbiWithId(id);
        runtime.RegisterPbiPlayingId(holder);                                         // [item+0x18] 0 -> 1 (R1.5)
        Assert.Equal(1, runtime.OutstandingActionCount(id));                          // [item+0x1C]: the in-flight message
        Assert.Equal(1, runtime.PlayingIds.Find(id)!.Count18);
        runtime.ReleasePbiPlayingIdReference(id);                                     // [item+0x18] 1 -> 0, [item+0x1C] is 1: nothing else
        Assert.True(runtime.IsPlaying(id));
        runtime.RegisterPbiPlayingId(holder);
        runtime.PlayingIds.Find(id)!.Count1C = 0;                                     // the message and actions are done
        Assert.Throws<WwiseMissingBehaviourException>(() => runtime.ReleasePbiPlayingIdReference(id));   // both zero: 0xA0C238 is unread
        var zeros = new List<uint>();
        var again = new WwiseEventRuntime(new[] { OneMixerBank(0x04, 3) }, new WwiseRng(1));
        again.PlayingItemFieldsWriter = (item, ev, go) => { item.EventId20 = ev; item.GameObject24 = go ?? WwiseEndOfEventDoubles.DoubleGameObjectForNull; };   // double: the writer of [item+0x20]/[item+0x24] is unread
        again.PlayingIds.Seams.GameObjectLookupA0C238 = _ => null;
        again.PlayingIds.Seams.A1C660 = item => zeros.Add(item.PlayingId);
        again.PlayingIds.Seams.A1C65C = _ => { };
        uint id2 = again.PostEvent(900, null);
        var holder2 = PbiWithId(id2);
        again.RegisterPbiPlayingId(holder2);
        again.PlayingIds.Find(id2)!.Count1C = 0;
        again.ReleasePbiPlayingIdReference(id2);
        Assert.Equal(new[] { id2 }, zeros);
        Assert.False(again.IsPlaying(id2));                                           // unlinked ([mgr+0xC]--)
        var ghost = PbiWithId(9999);
        runtime.RegisterPbiPlayingId(ghost);                                          // 6.2: not found -> no change, pbi+4 stays 0
        Assert.Equal(0u, ghost.Flags4);
        Assert.False(runtime.IsPlaying(9999));
        runtime.ReleasePbiPlayingIdReference(9999);                                   // no entry: nothing
    }

    [Fact]
    public void M6_026_R1_5_TheEntryFlagsAreStoredToPbiPlus4WhenTheEntryExists()
    {
        // 0xA04D48: [item+0x18]++ and [item+0x48] -> pbi+4 (0xA04DD0..0xA04DE0); a null PBI returns 2.
        var table = new WwisePlayingIdTable();
        var item = table.GetOrCreate(5);
        item.Flags48 = 0x100001;
        var pbi = PbiWithId(5);
        Assert.Equal(1, table.RegisterPbiA04D48(pbi));
        Assert.Equal(0x100001u, pbi.Flags4);
        Assert.Equal(1, item.Count18);
        Assert.Equal(2, table.RegisterPbiA04D48(null));
    }

    [Fact]
    public void M6_026_R4_1_EndOfEventUnlinksDecrementsTheGameObjectAndRunsTheCallbackBetweenTheLocks()
    {
        // 0xA03618: both counters zero; flags bit 0 -> callback(1, {cookie, game object, id, event id, ...}); the game object's low 30 bits drop by one with the top two bits kept (0xA0370C..0xA03718);
        // a count that reaches zero runs 0xA0B600.
        var table = new WwisePlayingIdTable();
        var log = new List<string>();
        var obj = new WwiseGameObjectRef { Word7C = 0xC0000001u };
        table.Seams.GameObjectLookupA0C238 = id => { log.Add("A0C238:" + id); return obj; };
        table.Seams.A0B600 = _ => log.Add("A0B600");
        table.Seams.A1C660 = _ => log.Add("A1C660");
        table.Seams.A9A6988 = _ => log.Add("9A6988");
        table.Seams.A1C65C = _ => log.Add("A1C65C");
        WwiseEndOfEventInfo? info = null;
        var item = table.GetOrCreate(77);
        item.Flags48 = 1; item.Cookie44 = 0xC0; item.GameObject24 = 7; item.EventId20 = 900; item.Object28 = new object();
        item.Callback40 = (type, i) => { log.Add("callback:" + type + ":idle=" + table.CallbackIdle1C); info = (WwiseEndOfEventInfo)i; };
        item.Count1C = 1;
        table.EndOfEventA03618(item, 77);                                             // [item+0x1C] != 0: nothing
        Assert.Empty(log);
        item.Count1C = 0;
        table.EndOfEventA03618(item, 77);
        Assert.Equal(new[] { "A0C238:7", "A0B600", "A1C660", "9A6988", "A1C65C", "callback:1:idle=False" }, log);
        Assert.Equal(0xC0000000u, obj.Word7C);                                        // (x - 1) & 0x3FFFFFFF with the top bits kept: 0xC0000001 -> 0xC0000000
        Assert.Equal(new WwiseEndOfEventInfo(0xC0, 7, 77, 900, obj, 77), info);
        Assert.True(table.CallbackIdle1C);
        Assert.Equal(1, table.Broadcasts);
        Assert.Null(table.Find(77));
    }

    [Fact]
    public void M6_026_R4_1_TheControlPathsLastCountAlwaysRunsTheA03618BodyAndStopsAtUnreadFields()
    {
        // 0xA04F54 [item+0x1C]-- then always tail-calls 0xA03618 (no silent removal): with both counters zero the unwritten [item+0x20]/[item+0x24] and the unread callees stop it.
        var runtime = new WwiseEventRuntime(new[] { OneMixerBank(0x04, 3) }, new WwiseRng(1));
        uint id = runtime.PostEvent(900, null);
        runtime.PlayingIds.Find(id)!.Count1C = 0;
        Assert.Throws<WwiseMissingBehaviourException>(() => runtime.PlayingIds.EndOfEventA03618(runtime.PlayingIds.Find(id)!, id));
    }

    [Fact]
    public void M6_026_R4_1_TheInfoBlockAndCallbackAreLoadedBeforeTheUnlinkAndTheTeardown()
    {
        // 0xA03658..0xA03678 load [item+0x44], [item+0x24], [item+0x20], [item+0x40] and store the info block before the unlink (0xA03680..0xA03788), 0xA0C238, and 0xA1C660/0x9A6988/0xA1C65C.
        // (1) an unset field throws with the table unchanged.
        var table = new WwisePlayingIdTable();
        table.Seams.GameObjectLookupA0C238 = _ => null;
        table.Seams.A1C660 = _ => { }; table.Seams.A1C65C = _ => { };
        var unset = table.GetOrCreate(5);
        unset.EventId20 = 1;                                                          // [item+0x24] unset
        Assert.Throws<WwiseMissingBehaviourException>(() => table.EndOfEventA03618(unset, 5));
        Assert.Same(unset, table.Find(5));
        var unsetEvent = table.GetOrCreate(6);
        unsetEvent.GameObject24 = 2;                                                  // [item+0x20] unset
        Assert.Throws<WwiseMissingBehaviourException>(() => table.EndOfEventA03618(unsetEvent, 6));
        Assert.Same(unsetEvent, table.Find(6));
        // (2) a teardown that clears the item's fields does not change the callback's info.
        var item = table.GetOrCreate(7);
        item.EventId20 = 11; item.GameObject24 = 22; item.Cookie44 = 33; item.Flags48 = 1; item.Object28 = new object();
        object? seen = null;
        var original = item.Callback40 = (t, info) => seen = info;
        table.Seams.A1C660 = i => { i.EventId20 = 99; i.GameObject24 = 98; i.Cookie44 = 97; i.Callback40 = null; };
        table.Seams.A9A6988 = _ => { };
        table.EndOfEventA03618(item, 7);
        var got = Assert.IsType<WwiseEndOfEventInfo>(seen);
        Assert.Equal((33u, 22u, 7u, 11u), (got.Cookie, got.GameObject, got.PlayingId, got.EventId));
        Assert.Null(table.Find(7));
    }

    [Theory]
    [InlineData("A0C238")]
    [InlineData("A0B600")]
    [InlineData("A1C660")]
    [InlineData("A9A6988")]
    [InlineData("A1C65C")]
    [InlineData("A05934")]
    public void M6_026_R4_1_EachUnreadCalleeOfA03618ThrowsWhenItsSeamIsUnset(string missing)
    {
        // The callees at 0xA037F4 (A05934, with flag 0x400000), 0xA036F8 (A0C238), 0xA0381C (A0B600, when the object's count reaches 0), 0xA03734 (A1C660), 0xA03748 (9A6988, [item+0x28] != 0), 0xA03750 (A1C65C).
        var table = new WwisePlayingIdTable();
        table.Seams.A05934 = () => { };
        table.Seams.GameObjectLookupA0C238 = _ => new WwiseGameObjectRef { Word7C = 1 };
        table.Seams.A0B600 = _ => { };
        table.Seams.A1C660 = _ => { };
        table.Seams.A9A6988 = _ => { };
        table.Seams.A1C65C = _ => { };
        switch (missing)
        {
            case "A0C238": table.Seams.GameObjectLookupA0C238 = null; break;
            case "A0B600": table.Seams.A0B600 = null; break;
            case "A1C660": table.Seams.A1C660 = null; break;
            case "A9A6988": table.Seams.A9A6988 = null; break;
            case "A1C65C": table.Seams.A1C65C = null; break;
            case "A05934": table.Seams.A05934 = null; break;
        }
        var item = table.GetOrCreate(8);
        item.EventId20 = 1; item.GameObject24 = 2; item.Flags48 = 0x400000; item.Object28 = new object();
        var ex = Assert.Throws<WwiseMissingBehaviourException>(() => table.EndOfEventA03618(item, 8));
        Assert.Contains(missing.Replace("A9A6988", "9A6988"), ex.Message);
    }

    [Fact]
    public void M6_026_R4_1_ABitFourHundredThousandFlagRunsA05934FirstAndNoCallbackWithoutBitZero()
    {
        var table = new WwisePlayingIdTable();
        var log = new List<string>();
        table.Seams.A05934 = () => log.Add("A05934");
        table.Seams.GameObjectLookupA0C238 = _ => null;
        table.Seams.A1C660 = _ => log.Add("A1C660");
        table.Seams.A1C65C = _ => log.Add("A1C65C");
        var item = table.GetOrCreate(9);
        item.EventId20 = 1; item.GameObject24 = 2;                                    // doubles: the writer of these fields is unread
        item.Flags48 = 0x400000;
        item.Callback40 = (_, _) => log.Add("callback");
        table.EndOfEventA03618(item, 9);
        Assert.Equal(new[] { "A05934", "A1C660", "A1C65C" }, log);                    // no bit 0: the callback does not run
        table.Seams.A05934 = null;
        var item2 = table.GetOrCreate(10);
        item2.EventId20 = 1; item2.GameObject24 = 2;
        item2.Flags48 = 0x400000;
        Assert.Throws<WwiseMissingBehaviourException>(() => table.EndOfEventA03618(item2, 10));
    }

    [Fact]
    public void M6_026_R4_7_TheDurationCallbackNeedsBit3AndCarriesTheNineWordInfo()
    {
        var table = new WwisePlayingIdTable();
        var item = table.GetOrCreate(5);
        item.Cookie44 = 3; item.GameObject24 = 7; item.EventId20 = 900;
        int calls = 0;
        WwiseDurationInfo? info = null;
        item.Callback40 = (type, i) => { calls++; Assert.Equal(8, type); info = (WwiseDurationInfo)i; };
        table.DurationA0393C(5, 1.5f, 0.75f, 11, 12, true);
        Assert.Equal(0, calls);                                                       // [item+0x48] bit 3 clear (0xA039B0)
        item.Flags48 = 8;
        table.DurationA0393C(5, 1.5f, 0.75f, 11, 12, true);
        Assert.Equal(new WwiseDurationInfo(3, 7, 5, 900, 1.5f, 0.75f, 11, 12, true), info);
        table.DurationA0393C(6, 1f, 1f, 1, 1, false);                                 // no entry: nothing
        Assert.Equal(1, calls);
    }

    [Fact]
    public void M6_026_8_4_CalcEffectiveParamsReturnsEarlyAt0x9FFC30WhenE9Bit2IsSet()
    {
        // 0x9FFC28: after the reset stage every path reaches `cmp r5,#0` (r5 = [pbi+0xE9] & 4). Set: 0x9FFC30 does 1BC |= 1 and E8 |= 0x20 and returns before the pan values (0x9FBE74) and the priority
        // block (0x9FFE1C), leaving the reset values (volume 0, +0x40 = 1.0f). Clear: the full path runs and node vt+0xAC is called. (emu_play.py B3 / B9.)
        var g = new Graph(Snd(1, P()));
        var l = Limiter(g);
        int acCalls = 0;
        var path = new WwisePlayPath(l.ParentNode, new WwisePlaySeams { FirstOutputBus9F4BB8 = n => n, BusFlag9C54E8 = _ => true, NodeVtAC = a => { acCalls++; a.Pbi.Volume3C = 6f; } });
        var closed = Pbi(50f);
        closed.NodeE0 = g[1];
        closed.Flags0E9 = 4;
        closed.Volume3C = 9f;
        path.CalcEffectiveParams(closed, null, l);
        Assert.Equal(0, acCalls);
        Assert.Equal(1, closed.Flags1BC & 1);
        Assert.Equal(0x20, closed.Flags0E8 & 0x20);
        Assert.Equal(0f, closed.Volume3C);                                           // the reset values, not 9 and not the node's 6 dB
        Assert.Equal(1f, closed.MuteFade40);
        var open = Pbi(50f);
        open.NodeE0 = g[1];
        path.CalcEffectiveParams(open, null, l);
        Assert.Equal(1, acCalls);
        Assert.Equal(6f, open.Volume3C);
    }

    private static List<string> TailLog(Action<WwisePlayingInstance> inTail, Action<WwisePlayInitParams> tweak, out Rig rig)
    {
        rig = new Rig(new[] { OneMixerBank(0x04, 3) });
        var log = new List<string>();
        var b108 = new object(); var p78 = new object();
        WwisePlayInitParams? played = null;
        bool tail = false;
        var inner = rig.Bridge.PlayPath!.Seams.NodeVtAC;
        rig.Bridge.PlayPath.Seams.NodeVtAC = a =>
        {
            inner!(a);
            if (tail && a.Params is not null && ReferenceEquals(a.Params, played)) log.Add("vt24:8C");
        };
        rig.Bridge.TailA023D4 = (pbi, r1) => { log.Add("A023D4:" + r1.ToString("X")); tail = true; inTail(pbi); };
        rig.Bridge.TailA01918 = (_, blk, one) => log.Add("A01918:" + (ReferenceEquals(blk, b108) ? "108" : "?") + "," + one);
        rig.Bridge.TailA9E85C8 = (_, one, ptr) => log.Add("9E85C8:" + one + "," + (ReferenceEquals(ptr, p78) ? "78" : "?"));
        rig.PlayDirect(500, 900, tweak: p => { played = p; p.Word88 = 0x1234; p.Block108 = b108; tweak(p); if (p.Ptr78 is not null) p.Ptr78 = p78; });
        return log;
    }

    [Fact]
    public void M6_026_0xA37FE8_TheTailBranchesOnE8Bit5AndE9Bit0AndTheParamsPointer()
    {
        // 0xA3800C: 0xA023D4(pbi, [params+0x88]). 0xA38010..0xA38018: [pbi+0xE8] bit 5 CLEAR -> 0xA38130 ctx vt+0x24 = CalcEffectiveParams (node vt+0xAC runs again, with the Play params as the block, E8 bit 6 being set);
        // SET -> 0xA3801C..0xA38024: only when [pbi+0xE9] bit 0 is set, ctx vt+0x28 = 0x9FF368 (it clears E9 bit 0). After the real 0x9BEB30 E8 bit 5 is already set (CalcEffectiveParams sets it, 0x9FFC30) and E9 bit 0
        // is clear (0x9FFDE0), so the hook at 0xA023D4 sets the state the branch looks at. 0xA38038: 0xA01918(pbi, params+0x108, 1). 0xA38048..0xA38064: 0x9E85C8([pbi+0x34], 1, [params+0x78]+0x14) only when
        // [params+0x78] != 0 and [pbi+0x34] != 0. Then 0xA00618 (its effect is the loop count in pbi+0x1B8 and 1BD bit 0).
        var clear = TailLog(pbi => pbi.Flags0E8 = 0x40, _ => { }, out var r1);
        Assert.Equal(new[] { "A023D4:1234", "vt24:8C", "A01918:108,1" }, clear);                  // bit 5 clear: CalcEffectiveParams again
        Assert.Equal(0x20, r1.LastPbi!.Flags0E8 & 0x20);
        Assert.Equal(1, r1.LastPbi.LoopCount1B8);                                                  // 0xA00618 ran: no property 0x3A, default 1

        var setNoE9 = TailLog(pbi => { pbi.Flags0E8 = 0x60; pbi.Flags0E9 = 0; }, _ => { }, out _);
        Assert.Equal(new[] { "A023D4:1234", "A01918:108,1" }, setNoE9);                            // neither branch

        var setE9 = TailLog(pbi => { pbi.Flags0E8 = 0x60; pbi.Flags0E9 = 1; pbi.Field98 = 3f; }, _ => { }, out var r9);
        Assert.Equal(new[] { "A023D4:1234", "A01918:108,1" }, setE9);
        Assert.Equal(0, r9.LastPbi!.Flags0E9 & 1);                                                 // 0x9FF3D0 cleared E9 bit 0: vt+0x28 ran
        Assert.Equal(3f, r9.LastPbi.Volume3C);                                                     // [+0x3C] = [+0x98] + [+0x118]

        var withPtr = TailLog(pbi => { pbi.Field34 = 1; pbi.Flags0E8 = 0x60; }, p => p.Ptr78 = new object(), out _);
        Assert.Equal(new[] { "A023D4:1234", "A01918:108,1", "9E85C8:1,78" }, withPtr);

        var ptrNoField34 = TailLog(pbi => pbi.Flags0E8 = 0x60, p => p.Ptr78 = new object(), out _);   // [pbi+0x34] == 0: no 0x9E85C8
        Assert.DoesNotContain(ptrNoField34, x => x.StartsWith("9E85C8"));
        var field34NoPtr = TailLog(pbi => { pbi.Field34 = 1; pbi.Flags0E8 = 0x60; }, _ => { }, out _);   // [params+0x78] == 0: no 0x9E85C8
        Assert.DoesNotContain(field34NoPtr, x => x.StartsWith("9E85C8"));

        var missing = new Rig(new[] { OneMixerBank(0x04, 3) });
        missing.Bridge.TailA01918 = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => missing.PlayDirect(500, 900));
        var missingPath = new Rig(new[] { OneMixerBank(0x04, 3) });
        missingPath.Bridge.PlayPath!.Seams.NodeVtAC = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => missingPath.PlayDirect(500, 900));
    }

    [Fact]
    public void M6_026_7_3_7_6_TheOwnerIsTheCurrentSourcesAndNotVoicePlus8()
    {
        // 7.3 (0xA44A50..0xA44A54, 0xA44BB4..0xA44BBC): pbi = [[voice+0xD4]+0xC]. 7.6: 0xA533FC -> 0xA565D0([voice+0xD4]) loads [src+0xC] and clears ITS 1BA bits 3..6 (0xA01840).
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState()) { DestroyVoiceA9D40C4 = _ => { } }.WithPrePassDoubles();
        var marked = Pbi(50f); marked.Flags1BC = 0x20; marked.Flags1BA = 0x7F;
        var other = Pbi(50f); other.Flags1BA = 0x7F;
        var src = new Src();
        pass.SourceOwner = s => ReferenceEquals(s, src) ? marked : null;
        var voice = new WwiseLiveVoice(1, 16) { State = 0, BusOwner8 = other, Source = src };
        pass.Voices.Add(voice);
        pass.VoicePass();
        Assert.Empty(pass.Voices);                                                   // the source's owner is marked, voice+8's is not
        Assert.Equal(2, voice.State);
        Assert.Equal(0x07, marked.Flags1BA);                                         // the source owner's bits 3..6 were cleared
        Assert.Equal(0x7F, other.Flags1BA);                                          // voice+8's were not
    }

    [Fact]
    public void M6_026_7_3_TheOwnerLookupIsRequiredAndAMissingSourceOrOwnerIsANamedStop()
    {
        var unset = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState()) { DestroyVoiceA9D40C4 = _ => { } }.WithPrePassDoubles();
        unset.Voices.Add(new WwiseLiveVoice(1, 16) { State = 0, Source = new Src() });
        Assert.Throws<WwiseMissingBehaviourException>(() => unset.VoicePass());       // no lookup wired
        var noSource = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState()) { DestroyVoiceA9D40C4 = _ => { }, SourceOwner = _ => Pbi(50f) }.WithPrePassDoubles();
        noSource.Voices.Add(new WwiseLiveVoice(1, 16) { State = 0 });
        Assert.Throws<WwiseMissingBehaviourException>(() => noSource.VoicePass());    // [voice+0xD4] == 0: the engine dereferences it
        var noOwner = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState()) { DestroyVoiceA9D40C4 = _ => { }, SourceOwner = _ => null }.WithPrePassDoubles();
        noOwner.Voices.Add(new WwiseLiveVoice(1, 16) { State = 0, Source = new Src() });
        Assert.Throws<WwiseMissingBehaviourException>(() => noOwner.VoicePass());     // [src+0xC] is not a PBI
        Assert.Throws<ArgumentNullException>(() => new WwiseLiveVoice(1, 16).StopA533FC(null!));
    }

    [Fact]
    public void M6_026_P1_ThePlayHonoursTheSameNextSourceOverrideAs0xA01768()
    {
        var rig = new Rig(new[] { OneMixerBank(0x04, 3) });
        var asked = new List<string>();
        rig.Bridge.NextSource9EEDA4 = (WwiseNode? n, out int i) => { i = 0; return 3; };
        rig.Bridge.NodeVt10A379D8 = _ => { asked.Add("vt10"); return 9; };            // code 3 reached the vt+0x10 test: the override answered P1
        rig.Bridge.NodeVt120A379D8 = (_, _) => 0;
        rig.PlayDirect(500, 900);
        Assert.Equal(new[] { "vt10" }, asked);
    }

    [Fact]
    public void M6_026_C5_AnUnorderedPriorityIsSkippedByTheVoiceCountPass()
    {
        // 0xA37938 vcmpe; blt: an unordered compare takes the skip. The PBI is still counted (count++ comes first), no 0xA01768, no candidate -> r8 = 0 -> 0x50.
        var l = Limiter(new Graph());
        l.ApplyStmgMaxVoices(1);
        l.InsertIntoList9F3274(l.GlobalVoiceList, Pbi(50f));
        l.AppendToGlobalPbiList(Pbi(50f));                                           // n = 2 > 1
        l.NextSourceCodeA01768 = _ => throw new InvalidOperationException("skipped PBIs are not asked");
        Assert.Equal(0x50, l.CheckVoiceCountA37880(float.NaN));
    }

    [Fact]
    public void M6_026_R1_TheContextSubscribeRunsEvenWhenTheNodeHasNoLimiter()
    {
        // 0x9BC5A8 beq 0x9BC5D8: only the limiter-list push (0x9BC5C0..0x9BC5D4) is conditional; 0xA19ECC (0x9BC5D8..0x9BC5F0) always runs.
        var g = new Graph(Snd(1, P()));
        var l = Limiter(g);
        var subs = new List<string>();
        l.RtpcSubscribeA19ECC = (k, _, _) => subs.Add(k);
        Assert.Null(l.LimiterOf(1));
        Assert.Equal(1, l.InsertPbiA0285C(g[1], Pbi(50f)));
        Assert.Equal(new[] { "context" }, subs);
    }

    [Fact]
    public void M6_026_0xA002D4_TheFirstPriorityBlockIsTheCtorsPair()
    {
        var rig = new Rig(new[] { OneMixerBank(0x04, 3) });
        rig.PlayDirect(500, 900);
        Assert.Equal(0x42480000, BitConverter.SingleToInt32Bits(rig.LastPbi!.Field1CC));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(rig.LastPbi.Field1D0));
    }

    [Fact]
    public void M6_026_P4_3_4_TheByteIsBelowXorOneOrCodeZeroAndNotBit3Of1BE()
    {
        // 3.4: byte = ((below ^ 1) | (r6 == 0)) & (((1BE ^ 8) >> 3) & 1). G3 needs it nonzero. Mixer max 1, Sound with behaviour code 2 (r6 = 2, [sp+0x1C] = 1: table default).
        // below true -> byte 0 -> the second Play is not checked and nothing is killed; below false -> byte 1 -> it kills.
        foreach (bool below in new[] { false, true })
        {
            var rig = new Rig(new[] { OneMixerBank(0x04, 1, soundAdv0: 0x10, soundAdv3: 2) });
            rig.PlayDirect(500, 900);
            rig.OnInit = pbi => pbi.Volume3C = below ? -100f : 0f;                   // lin(-100 dB) ~ 1.0e-5 <= 2^-16
            rig.PlayDirect(500, 900);
            Assert.Equal(2, rig.Bridge.Instances.Count);
            Assert.Equal(below ? 0 : 2, rig.Bridge.Instances[0].Flags1BD & 2);
        }
    }

    [Fact]
    public void M6_026_P4_3_4_ACodeZeroPlayIgnoresBelowButAChainIdFromTheParamsClearsTheByte()
    {
        // r6 == 0 makes the first term 1 whatever `below` is; 1BE bit 3 (set when params+0x7C != 0, 8.6) clears the byte, so the limit check is skipped.
        var rig = new Rig(new[] { OneMixerBank(0x04, 1) });
        rig.OnInit = pbi => pbi.Volume3C = -100f;                                    // below, but r6 == 0
        rig.PlayDirect(500, 900);
        rig.PlayDirect(500, 900);
        Assert.NotEqual(0, rig.Bridge.Instances[0].Flags1BD & 2);                    // killed: byte 1
        var rig2 = new Rig(new[] { OneMixerBank(0x04, 1) });
        rig2.PlayDirect(500, 900, chain: 0x55);
        rig2.PlayDirect(500, 900, chain: 0x56);
        Assert.Equal(0, rig2.Bridge.Instances[0].Flags1BD & 2);                      // 1BE bit 3: the byte is 0
    }

    [Fact]
    public void M6_026_3_5_TheBehaviourCodeTable()
    {
        // 3.5: code 1: r6 = 1, [sp+0x1C] = 0. Code 3: params+4 == 0, or vt+0x10 == 9, or vt+0x120 != 0 -> r6 = 2, [sp+0x1C] = 1; else r6 = 1, [sp+0x1C] = 0. Other codes: r6 = code, [sp+0x1C] = 1.
        // A 0x50 reject (virtual mixer, higher-priority PBI present) continues when [sp+0x1C] != 0 and fails when it is 0.
        bool Continues(byte adv3, Action<WwisePlaybackBridge>? seams = null, uint target = 501)
        {
            var bank = Synthetic(
                MixerObj(200, 0x06, 1),
                SoundObj(500, 200, bits: 1, props: new[] { ((byte)7, 0x42700000u) }),
                SoundObj(501, 200, adv0: 0x10, adv3: adv3),
                PlayAction(100, 500), EventObj(900, 100), PlayAction(101, 501), EventObj(901, 101));
            var rig = new Rig(new[] { bank });
            seams?.Invoke(rig.Bridge);
            rig.PlayDirect(500, 900);
            var node = rig.Runtime.FindNode(501)!;
            uint id = rig.Runtime.PostEvent(901, 7);
            rig.Bridge.OnPlay(node, id, 7, new WwisePlayInitParams { PlayingId = id, TargetNodeId = target, GameObjectId = 7 });
            return rig.Bridge.Instances.Count == 2;
        }
        Assert.False(Continues(1));                                                  // code 1
        Assert.True(Continues(0));
        Assert.True(Continues(2));
        Assert.True(Continues(4));
        Assert.True(Continues(15));
        Assert.True(Continues(3, b => { b.NodeVt10A379D8 = _ => 0; b.NodeVt120A379D8 = (_, _) => 0; }, target: 0));      // params+4 == 0
        Assert.True(Continues(3, b => { b.NodeVt10A379D8 = _ => 9; b.NodeVt120A379D8 = (_, _) => 0; }));                  // vt+0x10 == 9
        Assert.True(Continues(3, b => { b.NodeVt10A379D8 = _ => 0; b.NodeVt120A379D8 = (_, _) => 1; }));                  // vt+0x120 != 0
        Assert.False(Continues(3, b => { b.NodeVt10A379D8 = _ => 0; b.NodeVt120A379D8 = (_, _) => 0; }));                 // else r6 = 1, flag 0
        Assert.Throws<WwiseMissingBehaviourException>(() => Continues(3));                                                // unread bodies
    }

    [Fact]
    public void M6_026_5_3_TheGameObjectWordIsTheRegisteredObjectForObjectScopeAndZeroOtherwise()
    {
        var rig = new Rig(new[] { OneMixerBank(0x04, 3) });
        rig.Play(900);
        Assert.Equal(7u, rig.Bridge.Instances[0].GameObject14);                      // Play actions 0x0403 are object scope (all shipped ones)
    }

    [Fact]
    public void M6_026_6_5_APbiWithBits7Of1BAEqualTo2IsMarkedStoppedByPlay()
    {
        var rig = new Rig(new[] { OneMixerBank(0x04, 3) });
        rig.OnInit = pbi => pbi.Flags1BA = 2;
        rig.Play(900);
        Assert.NotEqual(0, rig.Bridge.Instances[0].Flags1BC & 0x20);                 // 0xA0067C: 1BA & 7 == 2 -> vt+0(pbi,0,0)
    }

    [Fact]
    public void M6_026_P3_AFailingMemoryCheckFailsThePlayBeforeAnyPbiExists()
    {
        // P3: 0xA376C0 returning 0 fails the Play (0xA37A9C..0xA37AB8) before the PBI is created (r6 = 0, no PBI). C28.3: with no candidate the kill step returns 0.
        var bank = Synthetic(SoundObj(500, 0), PlayAction(100, 500), EventObj(900, 100));
        var rig = new Rig(new[] { bank });
        rig.Limiter.MemoryThreshold1 = 0.5f;
        rig.Limiter.PoolStatsA7ABA8 = _ => (100, 90);
        rig.Play(900);
        Assert.Empty(rig.Bridge.Instances);
        Assert.Empty(rig.Bridge.StartList.Nodes);
        Assert.Null(rig.Limiter.LimiterOf(500));                                     // the walk never ran
    }

    [Fact]
    public void M6_026_P3_P5_AMemoryKillFreesTheSlotForANewcomerAbovetheCandidate()
    {
        // P3: the kill step chooses the running-minimum PBI of the global PBI list; a higher newcomer kills it (reason 3) and the Play goes on.
        var bank = Synthetic(SoundObj(500, 0), PlayAction(100, 500), EventObj(900, 100));
        var rig = new Rig(new[] { bank });
        var old = Pbi(40f);
        rig.Limiter.AppendToGlobalPbiList(old);
        rig.Limiter.MemoryThreshold1 = 0.5f;
        rig.Limiter.PoolStatsA7ABA8 = _ => (100, 90);
        rig.Play(900);
        Assert.Single(rig.Bridge.Instances);                                         // the Play went on (50 > 40)
        Assert.Equal(0x4E, old.Flags1BD);                                            // reason 3
    }

    [Fact]
    public void M6_026_P5_AVirtualRejectSetsBit2OfTheNewPbiAndTheAnyOtherCodeContinues()
    {
        // P5: a walk result of 0x50 sets pbi.1BE bit 2; with [sp+0x1C] = 1 (behaviour code 0) the Play continues.
        // Mixer byte0 0x06 = virtual + global, max 1, holding a higher-priority PBI: the newcomer is rejected with 0x50 (V3).
        var bank = Synthetic(
            MixerObj(200, 0x06, 1),
            SoundObj(500, 200, bits: 1, props: new[] { ((byte)7, 0x42700000u) }),    // priority 60.0f
            SoundObj(501, 200),
            PlayAction(100, 500), EventObj(900, 100), PlayAction(101, 501), EventObj(901, 101));
        var rig = new Rig(new[] { bank });
        rig.Play(900);
        rig.Play(901);
        Assert.Equal(2, rig.Bridge.Instances.Count);
        Assert.Equal(0, rig.Bridge.Instances[0].Flags1BE & 4);
        Assert.Equal(4, rig.Bridge.Instances[1].Flags1BE & 4);                       // 0x50 marked the newcomer
        Assert.Equal(0, rig.Bridge.Instances[0].Flags1BD & 2);                       // and nothing was killed
    }

    [Fact]
    public void M6_026_P4_ThePlayNeedsALimiterRatherThanSkippingTheWalk()
    {
        var bank = Synthetic(SoundObj(500, 0), PlayAction(100, 500), EventObj(900, 100));
        var runtime = WwiseEndOfEventDoubles.Runtime(new[] { bank }, new WwiseRng(1));
        var bridge = new WwisePlaybackBridge().WithTestSeams();
        runtime.PlaybackBridge = bridge;
        runtime.RegisterGameObject(7);
        runtime.PostEvent(900, 7);
        Assert.Throws<NotSupportedException>(() => runtime.AdvanceFrame());
    }

    [Fact]
    public void M6_026_C28_8_TheShippedCozmoSoundUnderMixer62050212IsKilledBySecondPlayWithReason1()
    {
        // C28.8 (HIRC parse of the shipped banks): Cozmo 62050212 (max 1, global, advanced bytes 04 01 01 00 00 00) covers 1872 Sounds; the buses above are
        // 1723505802 -> 3803692087 with max instances 0 and no duck entry; Init STMG max voices 256. The second Play kills the first (the oldest) via 0xA01CA4 reason 1.
        var lib = RequireLibrary();
        uint? soundId = null;
        foreach (uint id in lib.AllNodeIds.OrderBy(x => x))
        {
            if (lib.Node(id) is not WwiseSoundNode s || !s.Bank.Contains("Cozmo")) continue;
            for (WwiseNode? cur = s; cur is not null; cur = cur.Params.ParentId == 0 ? null : lib.Node(cur.Params.ParentId))
                if (cur.Id == 62050212) { soundId = id; break; }
            if (soundId is not null) break;
        }
        Assert.NotNull(soundId);
        var mixer = lib.Node(62050212)!;
        Assert.Equal(0x04, mixer.Params.AdvancedByte0);                              // C28.8 / B14: byte0 = 4 (global, no KillNewest, no virtual)
        Assert.Equal(1, mixer.Params.AdvancedMaxInstancesRaw & 0x3FF);
        Assert.Equal(1723505802u, mixer.Params.BusId);
        Assert.Equal(256, lib.Stmg!.MaxVoices);

        var synthetic = Synthetic(
            PlayAction(100, soundId!.Value), EventObj(900, 100));
        var rig = new Rig(lib.Banks.Concat(new[] { synthetic }).ToArray(), (ushort)lib.Stmg.MaxVoices);

        uint first = rig.Play(900);
        uint second = rig.Play(900);

        Assert.Equal(new[] { first, second }, rig.Bridge.Instances.Select(p => p.PlayingId).ToArray());
        var pbi1 = rig.Bridge.Instances[0];
        var pbi2 = rig.Bridge.Instances[1];
        Assert.Equal(0x42480000, BitConverter.SingleToInt32Bits(pbi1.Priority1C0));  // every other node resolves to 50.0f (P2b)
        Assert.True(pbi2.Key1C4 > pbi1.Key1C4 && pbi2.ChainId > pbi1.ChainId);       // 8.6
        Assert.Equal(0x47, pbi1.Flags1BD);                                           // 0xA01CA4(pbi, 1)
        Assert.Equal(0x69, pbi1.Flags1BC);                                           // 0x9FF7B8 marked it (K6); a fresh PBI takes route (b) (K3)
        Assert.Equal(0x45, pbi2.Flags1BD);  // 1BD bit 0 from 0xA00618 (0xA00640)
        Assert.Equal(0, pbi2.Flags1BC & 0x20);
        // W6/W7: the mixer's limiter counted both walks; the shared buses have max 0 (C28.8): no list of theirs is in the PBI's array.
        Assert.Equal((short)2, rig.Limiter.LimiterOf(62050212)!.Count60);
        Assert.Equal((short)2, rig.Limiter.LimiterOf(1723505802)!.Count60);
        Assert.Equal((short)2, rig.Limiter.LimiterOf(3803692087)!.Count60);
        Assert.Equal(0, rig.Limiter.LimiterOf(1723505802)!.List.Count20);
        Assert.Equal(2, pbi2.LimiterArray1EC.Count);                                 // the mixer's list and the global voice list
        Assert.Same(rig.Limiter.LimiterOf(62050212)!.List, pbi2.LimiterArray1EC.Items[0]);
        Assert.Same(rig.Limiter.GlobalVoiceList, pbi2.LimiterArray1EC.Items[1]);
    }

    [Fact]
    public void M6_026_C28_8_EveryShippedNodeUnderTheLimitedMixersHasAResolvableChainAndBusesWithoutDucks()
    {
        // C28.8: "all 15 Init buses have max instances 0"; the buses over the Cozmo Sounds carry no duck entry (W8: 0x9C4CD0 is a no-op there).
        var lib = RequireLibrary();
        int buses = 0;
        foreach (uint id in lib.AllNodeIds)
            if (lib.Node(id) is WwiseBusNode b && b.Bank.Contains("Init"))
            {
                Assert.Equal(0, b.MaxInstances);
                Assert.Equal(0, b.ByteB & 0x0F);
                buses++;
            }
        Assert.Equal(15, buses);
        foreach (uint id in new uint[] { 1723505802, 2476517424, 3803692087 })
            Assert.Empty(((WwiseBusNode)lib.Node(id)!).Ducks);
    }

    [Fact]
    public void M6_026_C28_8_ThirteenShippedNodesCarryAMaxInstancesField()
    {
        // C28.8: 13 nodes carry a max-instances field (SFX 10, UI 1, Cozmo 2); Cozmo 62050212 (max 1) and 66225135 (max 5), SFX 615722439 (max 5, per object).
        var lib = RequireLibrary();
        // The census covers the Sound-path classes (Sound, RanSeq, Switch, ActorMixer, Layer); the 26 Music.bnk nodes on the music path are not in it.
        var limited = lib.AllNodeIds.Select(lib.Node)
            .Where(n => n is WwiseSoundNode or WwiseRandomSequenceNode or WwiseSwitchNode or WwiseActorMixerNode or WwiseBlendNode
                        && (n.Params.AdvancedMaxInstancesRaw & 0x3FF) != 0).ToList();
        Assert.Equal(13, limited.Count);
        Assert.Equal(2, limited.Count(n => n!.Bank.Contains("Cozmo")));
        Assert.Equal(1, limited.Single(n => n!.Id == 62050212)!.Params.AdvancedMaxInstancesRaw & 0x3FF);
        Assert.Equal(5, limited.Single(n => n!.Id == 66225135)!.Params.AdvancedMaxInstancesRaw & 0x3FF);
        var perObject = limited.Single(n => n!.Id == 615722439)!;
        Assert.Equal(5, perObject.Params.AdvancedMaxInstancesRaw & 0x3FF);
        Assert.Equal(0, perObject.Params.AdvancedByte0 & 4);                         // B5: bit 2 clear selects the per-object variant
    }

    // ------------------------------------------------------------------ K8: the code-4 tail

    [Fact]
    public void M6_026_K8_TheTermNotificationTailUnlinksTheGlobalListStartNodesAndTerms()
    {
        // 7.9: code 4 unlinks the PBI from the global PBI list ([G+0x48]--), 0x9D3470 (its start-list nodes), Term (vt+0x10), the destructor (frees pbi+0x1EC), the pool free.
        var rig = new Rig(new[] { OneMixerBank(0x04, 5) });
        rig.Play(900);
        var pbi = rig.Bridge.Instances[0];
        Assert.Single(rig.Bridge.StartList.Nodes);
        rig.Bridge.TerminatePbi(pbi);
        Assert.Empty(rig.Limiter.GlobalPbiList);
        Assert.Empty(rig.Bridge.StartList.Nodes);
        Assert.Empty(rig.Bridge.Instances);
        Assert.Equal(0, pbi.LimiterArray1EC.Count);
        Assert.Equal(0x20, pbi.Flags1BD & 0x20);
        foreach (uint id in new uint[] { 500, 200 }) Assert.Null(rig.Limiter.LimiterOf(id));   // the lone PBI's limiters are destroyed
    }

    // ------------------------------------------------------------------ K7: the voice-pass stop (C29.6)

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IWwiseVoiceSource, WwisePlayingInstance> Owners = new();

    private static Src Owned(WwisePlayingInstance pbi)
    {
        var src = new Src();
        Owners.Add(src, pbi);
        return src;
    }

    private sealed class Src : IWwiseVoiceSource
    {
        public int Channels => 1;
        public int SampleRate => 48000;
        public int Result { get; set; } = 0x2D;
        public bool StartStreamSucceeded { get; set; }
        public int StartStream(uint arg1DC, uint arg1E0) => 1;
        public int Render(WwiseVoiceBuffer buffer) { buffer.ValidFrames = buffer.MaxFrames; buffer.Result = Result; return Result; }
    }

    private sealed class VoiceRig
    {
        public readonly Rig R;
        public readonly WwiseVoiceBusPass Pass;
        public readonly WwisePlaybackBridge Bridge;
        public readonly List<string> Calls = new();

        public VoiceRig(IEnumerable<WwiseBank> banks, ushort? stmg = null)
        {
            R = new Rig(banks, stmg);
            Bridge = R.Bridge;
            Pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState()) { Voices = Bridge.Voices };
            var seams = new WwiseVoiceLinkSeams { InitVoiceA54A30 = _ => 1 };
            seams.CloseSource56414 = src => { Calls.Add("close"); Bridge.CloseSourceA56414(src, 0); };   // 0xA56414(src, 0) of the voice Term (7.8)
            Bridge.Linker = new WwiseVoiceLinker(new WwiseMixBusHierarchy(), new WwiseOutputDeviceList(), Bridge.Voices,
                _ => new WwisePbiRouting { Node = new WwiseRoutingNode { Id = 1 } }, seams);
            Bridge.SourceFactory = _ => new Src();
            Bridge.LinkEngineA548B8 = v => v.EngineEC = new object();
            Bridge.StartSourceA56478 = _ => { };                                     // 0xA56478 is unread
            Bridge.Notify38600 = (pbi, code, r2, r3) => Pass.PbiNotifications.Enqueue(Bridge.NotificationA38600(pbi, code, r2));
            Pass.DestroyVoiceA9D40C4 = Bridge.Linker.TeardownVoice;
            Pass.SourceOwner = src => Bridge.TryOwnerOf(src);                        // [[voice+0xD4]+0xC]
            Pass.NodeCleanup = R.Limiter.PerFrameA39564;                             // 0xA39564 (E1/E2)
            Pass.AdvanceTickCounters = Bridge.WalkPendingVoices;                     // 0x9D3CC0
            Pass.DuckPrePass = () => { };                                            // 0xA43D24 is unread (test double; the pass requires the collaborator)
            Pass.PostMixA5495C = _ => { }; Pass.PostMixNoDataReadyA55CC4 = (_, _) => { };                                      // 0xA55CC4 / 0xA5495C are unread
            R.Limiter.VoiceAudibleA4B3E8 = _ => true;                                // 0xA4B3E8 is unread
        }

        /// <summary>One frame in the engine's order (7.x): the start-list drain 0x9D3C98, the voice pass 0xA57FF8, then the queue flush 0xA38420.</summary>
        public void Frame()
        {
            Bridge.DrainStartList();
            Pass.VoicePass();
            Pass.FlushPbiNotifications();
        }
    }

    [Fact]
    public void M6_026_7_3_7_9_AMarkedPbisVoiceIsStoppedDestroyedAndItsPbiTermedInTheFlush()
    {
        var bank = Synthetic(MixerObj(200, 0x04, 1), SoundObj(500, 200), SoundObj(501, 200),
            PlayAction(100, 500), EventObj(900, 100), PlayAction(101, 501), EventObj(901, 101));
        var v = new VoiceRig(new[] { bank });
        v.R.Play(900);
        v.Frame();                                                                   // the voice attaches and starts (state 1)
        var pbi1 = v.Bridge.Instances[0];
        var voice1 = Assert.Single(v.Bridge.Voices);
        Assert.Equal(1, voice1.State);
        Assert.Same(voice1, pbi1.Field154);

        v.R.Play(901);                                                               // the second Play kills the first (reason 1)
        Assert.Equal(0x47, pbi1.Flags1BD);  // 1BD bit 0 from 0xA00618 (0xA00640)
        Assert.Equal(0x69, pbi1.Flags1BC);  // 1BC bit 0 from CalcEffectiveParams (0x9FFC30)
        Assert.Contains(voice1, v.Bridge.Voices);                                    // 0xA01CA4 does not stop the voice (K6)
        Assert.Equal(1, voice1.State);

        v.Frame();                                                                   // the first voice pass after the mark stops and destroys it, the flush Terms the PBI
        Assert.DoesNotContain(voice1, v.Bridge.Voices);
        Assert.Contains("close", v.Calls);                                           // voice Term -> 0xA56414(src, 0)
        Assert.Null(pbi1.Field154);                                                  // 0xA01800: pbi+0x154 = 0
        Assert.DoesNotContain(pbi1, v.Bridge.Instances);                             // PBI Term, dtor, free (7.9)
        var pbi2 = Assert.Single(v.Bridge.Instances);
        Assert.Equal(new[] { pbi2 }, v.R.Limiter.GlobalPbiList);
        Assert.Equal(new[] { pbi2 }, v.R.Limiter.LimiterOf(200)!.List.Items);        // 0x9F3528 removed the first
        Assert.Equal((ushort)1, v.R.Limiter.LimiterOf(200)!.List.Count20);           // counts back to 1
        Assert.Equal((short)1, v.R.Limiter.LimiterOf(200)!.Count60);
        Assert.Null(v.R.Limiter.LimiterOf(500));                                     // the first Sound's limiter counted only that PBI: idle, destroyed (1.4)
        Assert.Equal((short)1, v.R.Limiter.LimiterOf(501)!.Count60);
        Assert.Single(v.Bridge.Voices);                                              // the second voice lives
        Assert.Equal(1, v.Bridge.Voices[0].State);
        Assert.Equal((ushort)1, v.R.Limiter.GlobalVoiceList.Count20);
    }

    [Fact]
    public void M6_026_C28_8_7_TheShippedCozmoSoundIsKilledStoppedAndTermedAfterTheFrames()
    {
        // Two plays of a shipped Cozmo Sound under mixer 62050212 (max 1, global) through WwiseEventRuntime: the second kills the first (C28.8); after the frames the first voice is
        // destroyed (7.7) and its PBI termed by the flush (7.9), leaving the mixer's count at 1.
        var lib = RequireLibrary();
        uint? soundId = null;
        foreach (uint id in lib.AllNodeIds.OrderBy(x => x))
        {
            if (lib.Node(id) is not WwiseSoundNode snd || !snd.Bank.Contains("Cozmo")) continue;
            for (WwiseNode? cur = snd; cur is not null; cur = cur.Params.ParentId == 0 ? null : lib.Node(cur.Params.ParentId))
                if (cur.Id == 62050212) { soundId = id; break; }
            if (soundId is not null) break;
        }
        var synthetic = Synthetic(PlayAction(100, soundId!.Value), EventObj(900, 100));
        var v = new VoiceRig(lib.Banks.Concat(new[] { synthetic }).ToArray(), (ushort)lib.Stmg!.MaxVoices);
        uint first = v.R.Play(900);
        v.Frame();
        var voice1 = Assert.Single(v.Bridge.Voices);
        uint second = v.R.Play(900);
        var pbi1 = v.Bridge.Instances.Single(p => p.PlayingId == first);
        Assert.Equal(0x47, pbi1.Flags1BD);                                           // 0xA01CA4(pbi, 1)
        Assert.Equal(2, v.R.Limiter.LimiterOf(62050212)!.List.Count20);
        Assert.Equal((short)2, v.R.Limiter.LimiterOf(62050212)!.Count60);

        v.Frame();

        Assert.DoesNotContain(voice1, v.Bridge.Voices);
        Assert.Equal(new[] { second }, v.Bridge.Instances.Select(p => p.PlayingId).ToArray());
        Assert.Equal((ushort)1, v.R.Limiter.LimiterOf(62050212)!.List.Count20);
        Assert.Equal((short)1, v.R.Limiter.LimiterOf(62050212)!.Count60);
        Assert.Equal((short)1, v.R.Limiter.LimiterOf(1723505802)!.Count60);          // the shared bus counts came back to one Play
        Assert.Equal((short)1, v.R.Limiter.LimiterOf(3803692087)!.Count60);
        Assert.Single(v.Bridge.Voices);
    }

    [Fact]
    public void M6_026_7_3_7_5_TheStopFlagAndTheResultDecideTheStop()
    {
        // 7.3: sl = (1BC & 0x20) ? ([pbi+0x1F8] == -1) : 0; the mix-result byte forces sl = 1. 7.5: result 0x11 stops on sl or with no pending source; any other result stops on 2 or sl.
        WwiseLiveVoice Make(WwiseVoiceBusPass pass, WwisePlayingInstance pbi, int state = 1)
        {
            var voice = new WwiseLiveVoice(1, 16) { State = state, BusOwner8 = pbi, Source = Owned(pbi) };
            pass.Voices.Add(voice);
            return voice;
        }
        WwiseVoiceBusPass NewPass()
        {
            var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState()).WithPrePassDoubles();
            pass.DestroyVoiceA9D40C4 = _ => { };
            pass.SourceOwner = src => Owners.TryGetValue(src, out var o) ? o : null;
            return pass;
        }
        // state 0 (not started) is not mixed, the result stays 0x2B; a marked PBI still stops it
        var p1 = NewPass(); var marked = Pbi(50f); marked.Flags1BC = 0x20;
        var v1 = Make(p1, marked, state: 0);
        p1.VoicePass();
        Assert.Empty(p1.Voices);
        Assert.Equal(2, v1.State);
        // an unmarked PBI keeps its voice
        var p2 = NewPass(); var alive = Pbi(50f);
        Make(p2, alive, state: 0);
        p2.VoicePass();
        Assert.Single(p2.Voices);
        // the stop clears 1BA bits 3..6 (0xA565D0 -> 0xA01840)
        var p3 = NewPass(); var fading = Pbi(50f); fading.Flags1BA = 0x7F; fading.Flags1BC = 0x20;
        Make(p3, fading, state: 0);
        p3.VoicePass();
        Assert.Equal(0x07, fading.Flags1BA);
        // the decision by result (7.5), with the mix result set as a mix would leave it
        var p4 = NewPass();
        WwiseLiveVoice With(int result, WwisePlayingInstance pbi, bool busParam = false, IWwiseVoiceSource? pending = null)
        {
            var voice = new WwiseLiveVoice(1, 16) { State = 1, BusOwner8 = pbi, Source = Owned(pbi), Pending = pending };
            voice.Buffer.Result = result;
            voice.Buffer.HasBusParam = busParam;
            return voice;
        }
        var free = Pbi(50f);
        WwiseLiveVoice Dec(WwiseLiveVoice v) { p4.StopDecisionA44A58(v); return v; }
        Assert.Equal(1, Dec(With(0x2D, free)).State);        // DataReady, unmarked: runs on
        Assert.Equal(2, Dec(With(0x11, free)).State);        // NoMoreData with no pending source: stop
        Assert.Equal(2, Dec(With(2, free)).State);           // AK_Fail: stop
        Assert.Equal(2, Dec(With(0x2D, free, busParam: true)).State);   // the mix-result byte forces sl
        Assert.Equal(2, Dec(With(0x11, marked, pending: new Src())).State);   // 0x11 with sl: stop even with a pending source
        var pending = new Src();
        p4.ContinueWithPendingSource = (_, next) => ReferenceEquals(next, pending);
        var switched = Dec(With(0x11, free, pending: pending));
        Assert.Equal(1, switched.State);                                              // the continuation succeeded
        Assert.Null(switched.Pending);                                                // [voice+0xD8] = 0
        p4.ContinueWithPendingSource = (_, _) => false;
        Assert.Equal(2, Dec(With(0x11, free, pending: new Src())).State);
        p4.ContinueWithPendingSource = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => p4.StopDecisionA44A58(With(0x11, free, pending: new Src())));
        // pause of a paused-and-running PBI needs the seam
        var p5 = NewPass(); var paused = Pbi(50f); paused.Flags1BC = 0x80;
        Make(p5, paused, state: 1);
        Assert.Throws<WwiseMissingBehaviourException>(() => p5.VoicePass());
        var pausedCalls = new List<WwiseLiveVoice>();
        p5.PauseVoice4C = pausedCalls.Add;
        p5.VoicePass();
        Assert.Single(pausedCalls);
        // a stopped voice needs the destroy seam
        var p6 = NewPass(); p6.DestroyVoiceA9D40C4 = null;
        Make(p6, marked, state: 0);
        Assert.Throws<WwiseMissingBehaviourException>(() => p6.VoicePass());
    }
}
