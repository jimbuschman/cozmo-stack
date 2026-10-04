using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// B-M6b-4 batch 5f / 5h, C37.1, C39: the PBI Init's listener registration (<c>0xA0285C -> 0x9BC5A8 -> 0xA19ECC -> 0x9F7390</c>) through the live entry, and what stops visibly. Expected masks are computed by hand from the disassembly (the constants are in each
/// comment); the walk itself is compared against the engine in <see cref="WwiseListenerTests"/>. Batch 5h changed this file from the binary: the stops of the partial model (second holder, bus branch, AllowBusBranchRegistryInference, the RemoveRtpcListenerA198A4 seam) are gone because 0x9F7390 and 0x9F9064 are read.
/// </summary>
public class WwiseRtpcListenerWalkTests
{
    private const ulong L = 0x3FE3FFFE67BDUL;                   // {0xFFFE67BD, 0x3FE3}: 0x9BC5DC..0x9BC5F0

    private static WwisePlayingInstance Pbi(uint obj = 7, uint playing = 5)
        => new(new WwisePlayInitParams { PlayingId = playing, TargetNodeId = 3, GameObjectId = obj }, 3, new object(), new byte[0x44], new WwiseGainRtpcKey(obj, playing), continuous: false);

    private static WwiseSoundNode SoundNode()
        => new(3, "t.bnk", new WwiseNodeParams(0, 0, 0, new Dictionary<byte, uint>(), new Dictionary<byte, (float, float)>(), Array.Empty<WwiseRtpc>(),
            Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>()), WwiseSourceFactory.VorbisPlugin, 1, 12345, 0, 0);

    [Fact]
    public void C37_1_ThePbiInitRegistersTheListenerOnTheNodeChainAndNeedsItsRuntimeNode()
    {
        // Registered at Init, up the parent chain, at a holder only where its registry exists (0x9F7E4C), with the mask INTERSECTED: allowed = listener & (0x127DF | [node+0x40] << 17), and the top node adds 0x7E3FFFE0000 (0x9F8758).
        // 0x3FE3FFFE67BD & 0x127DF = 0x279D; & 0x9F = 0x9D; the top node's allowed is 0x7E3FFFE279D.
        var top = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x3010, Node40 = 0, SubscriptionMask14 = (1UL << 32) | 1 };
        var mid = new WwiseRoutingNode { Id = 2, SubscriptionKey10 = 0x2010, Parent = top, Node40 = 0, SubscriptionMask14 = 0x9F };
        var leaf = new WwiseRoutingNode { Id = 3, SubscriptionKey10 = 0x1010, Parent = mid, Node40 = 0 };
        var soundNode = SoundNode();
        var store = new WwiseRtpcStore();
        var seen = new List<string>();
        var limiter = new WwisePlaybackLimiter(_ => soundNode) { RtpcListeners = store, RegisterPlayingIdA04D48 = _ => { }, MediaTable = new WwiseMediaTable(new WwiseBankMemory()) };
        var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 5, TargetNodeId = 3, GameObjectId = 7 }, 3, WwiseSourceDescriptor.FromSound(soundNode), new byte[0x44], new WwiseGainRtpcKey(7, 5), continuous: false) { NodeE0 = soundNode };
        Assert.Throws<WwiseMissingBehaviourException>(() => limiter.InsertPbiA0285C(soundNode, pbi));      // no RuntimeNodeOf
        limiter.RuntimeNodeOf = _ => leaf;
        limiter.RtpcSubscribeA19ECC = (kind, node, mask) => seen.Add($"{kind}/{node.Id}/{mask:X}");
        Assert.Equal(1, limiter.InsertPbiA0285C(soundNode, pbi));
        Assert.Equal(new[] { "context/3/3FE3FFFE67BD" }, seen);
        Assert.Equal(new[] { 0, 1, 1, 0 }, new[] { store.ListenerCount(0x1010), store.ListenerCount(0x2010), store.ListenerCount(0x3010), store.ListenerCount(0x4010) });   // the leaf has no registry
        Assert.Equal(0x9DUL, store.ListenerMask(0x2010, pbi));
        Assert.Equal((1UL << 32) | 1, store.ListenerMask(0x3010, pbi));
        Assert.Same(leaf, pbi.Ctx20Node);                                                    // 0xA19EF4 [ctx+0x20] = node
        var odd = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 6, TargetNodeId = 3, SoundSpecial84 = 0x90 }, 3, WwiseSourceDescriptor.FromSound(soundNode), new byte[0x44], null, continuous: false) { NodeE0 = soundNode };
        Assert.Throws<WwiseMissingBehaviourException>(() => limiter.InsertPbiA0285C(soundNode, odd));   // params+0x84 == 0x90 reads params+0x86 for the key (0xA003E0), which is unset: a visible stop
    }

    [Fact]
    public void C37_1_TheListenerWalkNarrowsTheMaskTakesNode40AndRemovesOnAZeroIntersection()
    {
        // allowed = (~satisfied & listener) & (0x127DF | n40); satisfied |= n40 after each node (0x9F8248), so bit 17 a lower node took (Node40 = 1) is not offered to the next. 0x127DF has bit 0 and no bit 17.
        var upper = new WwiseRoutingNode { Id = 2, SubscriptionKey10 = 0x2010, Node40 = 1, SubscriptionMask14 = (1UL << 17) | 1 };
        var lower = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Parent = upper, Node40 = 1, SubscriptionMask14 = (1UL << 17) | 1 };
        var store = new WwiseRtpcStore();
        var pbi = Pbi();
        store.RegisterListenerA19ECC(pbi, lower, L);
        Assert.Equal((1UL << 17) | 1, store.ListenerMask(0x1010, pbi));
        Assert.Equal(1UL, store.ListenerMask(0x2010, pbi));
        // a zero intersection removes the existing child (0xA198A4, 0x9F86F8): bit 1 is not in the listener
        var lowerAgain = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 1, SubscriptionMask14 = 0x2 };
        store.RegisterListenerA19ECC(pbi, lowerAgain, L);
        Assert.Null(store.ListenerMask(0x1010, pbi));
        // a zero listener registers nowhere (0x9F7DB0)
        var other = Pbi();
        store.RegisterListenerA19ECC(other, upper, 0);
        Assert.Null(store.ListenerMask(0x2010, other));
    }

    [Fact]
    public void C39_1_AWalkOverANodeWithoutNode40StopsVisiblyAndTheSecondHolderAndTheBusBranchAreRead()
    {
        // Replaces C37_1_TheListenerWalkStopsVisiblyAtWhatIsUnread (it asserted the stops of the partial model: the second holder, the bus branch and the AllowBusBranchRegistryInference flag; C39.1 reads all three from 0x9F7390).
        var store = new WwiseRtpcStore();
        Assert.Throws<WwiseMissingBehaviourException>(() => store.RegisterListenerA19ECC(Pbi(), new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010 }, L));      // [node+0x40] not supplied: the walk reads it at 0x9F7DF8
        var calls = new List<WwiseRtpcManagerCall>();
        store.ManagerCallObserver = calls.Add;
        // the second holder (node+0x1C, registry [node+0x20], site 0x9F8080): a top node, so M = L & (0x7E3FFFE0000 | 0x127DF) holds bits 0 and 2; x = M & mask A = 5 (the unnarrowed M: 0x9F8044..0x9F8054)
        var node = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, SecondHolderMask20 = 0x5 };
        var pbi = Pbi();
        store.RegisterListenerA19ECC(pbi, node, L);
        Assert.Equal(5UL, store.ListenerMask(0x101C, pbi));
        Assert.Equal(new[] { new WwiseRtpcManagerCall(true, 0x101C, 5UL) }, calls);                                           // 0xA1008C(mgr, node+0x1C, whole mask A) at count 0
    }

    [Fact]
    public void C39_1_TheBusBranchRegistersAtAHolderOfTheFirstBusAndRemovesWhereTheCollapsedBusTookTheBit()
    {
        // The node loop ends with the first output bus found (0x9F82D4 -> 0x9F73DC). A bus with 0x9C54E8 == 0 (Byte68 0, parent bus present, nothing else set) leaves 0x20 out of satisfied, so a bus registry with bit 5 and bit 0 gets x = M & A where the bus-loop mask is
        // (~0 & L) & 0x1FFFFD003F = 0x1FFFFD003F & 0x3FE3FFFE67BD: bits 0 and 5 are both in it (0x3F), so x = 0x21. With 0x9C54E8 == 1 (Byte68 1) the first bus sets 0x20 before the walk reaches the bus loop (0x9F8274..0x9F8288): bit 5 is gone, x = 1.
        foreach (var (byte68, expected) in new[] { ((byte)0, 0x21UL), ((byte)1, 1UL) })
        {
            var master = new WwiseRoutingNode { Id = 9, IsBus = true, SubscriptionKey10 = 0x9010 };                                        // no parent bus: 0x9C54E8 is 1 (0x9C5560)
            var bus = new WwiseRoutingNode { Id = 8, IsBus = true, SubscriptionKey10 = 0x8010, OutputBus = master, Byte68 = byte68, SubscriptionMask14 = 0x21 };
            var sound = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, OutputBus = bus };
            var store = new WwiseRtpcStore();
            var pbi = Pbi();
            store.RegisterListenerA19ECC(pbi, sound, L);
            Assert.Equal(expected, store.ListenerMask(0x8010, pbi));
            Assert.Equal(1, store.ListenerCount(0x8010));
        }
        // robot_volume on Bus 1723505802 (param 5) with the shipped first bus (Byte68 = 1, C39.4): the listener is never inserted and no activation is made
        var calls = new List<WwiseRtpcManagerCall>();
        var robot = new WwiseRoutingNode { Id = 1723505802, IsBus = true, SubscriptionKey10 = 0x7010, OutputBus = new WwiseRoutingNode { Id = 3803692087, IsBus = true, SubscriptionKey10 = 0x6010 }, Byte68 = 1, SubscriptionMask14 = 1UL << 5 };
        var s2 = new WwiseRtpcStore { ManagerCallObserver = calls.Add };
        var p2 = Pbi();
        s2.RegisterListenerA19ECC(p2, new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, OutputBus = robot }, L);
        Assert.Equal(0, s2.ListenerCount(0x7010));
        Assert.Empty(calls);
    }

    [Fact]
    public void C39_2_TheListenerIsRemovedAtTermsLastStepAndTheDestructorAfterTermRemovesNothing()
    {
        // Term 0xA029DC ends with the tail 0x9BDC8C(ctx, 0) -> 0xA19F60: with [ctx+0x20] != 0, 0x9F9064 removes the PBI (0xA198A4) from the registries of the node chain and the bus chain, then [ctx+0x20] = 0. A set after Term no longer reaches the PBI. The
        // destructor's 0xA19D44 then sees [ctx+0x20] == 0 and removes nothing; a PBI destroyed without Term takes 0x9F9064(node, ctx, &~0, 1) there. (Replaces C37_1_TheListenerIsRemovedAtTermsLastStepAndDestructionAfterTermNeedsNoSeam: the seam RemoveRtpcListenerA198A4 is gone, 0xA198A4 is read.)
        var soundNode = SoundNode();
        var mixer = new WwiseRoutingNode { Id = 2, SubscriptionKey10 = 0x2010, Node40 = 0, SubscriptionMask14 = 1 };
        var leaf = new WwiseRoutingNode { Id = 3, SubscriptionKey10 = 0x1010, Node40 = 0, Parent = mixer };
        var store = new WwiseRtpcStore();
        var calls = new List<WwiseRtpcManagerCall>();
        store.ManagerCallObserver = calls.Add;
        var limiter = WwisePlaybackLimiterTestDoubles.Create(_ => soundNode);          // its TermSteps10To13 double stands for 0xA3E27C / 0xA1E8F4 / the rest of 0x9BDC8C only
        limiter.RtpcListeners = store;
        limiter.RuntimeNodeOf = _ => leaf;
        var curve = new WwiseRtpc(7001, 0, 0, 0, 0, 2, new[] { (0f, -1f, 1u), (1f, 0f, 4u) });
        store.SetRtpcs(7001, 1f, 7, 5, 0, 0, false);
        store.AddSubscription(new WwiseRtpcSubscription { Key1 = 0x2010, Param = 0, Type = 2, Accumulate = 1, Curves = new[] { curve } });
        WwisePlayingInstance NewOne(uint go = 7) => new(new WwisePlayInitParams { PlayingId = 5, TargetNodeId = 3, GameObjectId = go }, 3, WwiseSourceDescriptor.FromSound(soundNode), new byte[0x44], new WwiseGainRtpcKey(7, 5), continuous: false) { NodeE0 = soundNode };
        var unTermed = NewOne(8);                                                              // another game object: two PBIs with equal listener keys at one holder are a visible stop (the engine orders them by heap address)
        Assert.Equal(1, limiter.InsertPbiA0285C(soundNode, unTermed));
        var pbi = NewOne();
        Assert.Equal(1, limiter.InsertPbiA0285C(soundNode, pbi));
        Assert.Same(leaf, pbi.Ctx20Node);
        Assert.Equal(new[] { new WwiseRtpcManagerCall(true, 0x2010, 1UL) }, calls);                                           // the first listener activates the holder (0x9F7E88), the second finds count 1
        calls.Clear();
        store.TransitionGateA1B5FC = (_, _, _) => true;                                // TEST DOUBLE: 0xA1B5FC is unread; the first set of the playing id has no cell
        store.SetRtpcs(7001, 0.6f, 7, 5, 0, 0, false);
        float before = pbi.Field98;
        Assert.NotEqual(0f, before);                                                         // delivered while live
        limiter.TermPbiA029DC(pbi);
        Assert.False(store.HasListener(pbi));
        Assert.Null(pbi.Ctx20Node);                                                          // 0xA19F8C
        Assert.Equal(1, store.ListenerCount(0x2010));                                        // the other PBI is still registered: count 1, so no 0xA10220 (0x9F9274 cmp [reg+0x14])
        Assert.Empty(calls);
        store.SetRtpcs(7001, 0.3f, 7, 5, 0, 0, false);
        Assert.Equal(before, pbi.Field98);                                                   // no longer reached after Term
        limiter.DestroyPbiVt4(pbi);                                                          // 0xA19D44 skips 0x9F9064: nothing removed, nothing called
        Assert.Empty(calls);
        // a PBI destroyed without Term ([ctx+0x20] still set) takes 0x9F9064 in the destructor; the count reaches 0 there, so the manager is told (0xA10220, site 0x9F9290)
        limiter.DestroyPbiVt4(unTermed);
        Assert.False(store.HasListener(unTermed));
        Assert.Null(unTermed.Ctx20Node);
        Assert.Equal(new[] { new WwiseRtpcManagerCall(false, 0x2010, 1UL) }, calls);
    }

    [Fact]
    public void C39_1_AStartNodeWithBit2Of0x46TakesTheBusBranchAtOnce()
    {
        // 0x9F73AC..0x9F73C4 reads [node+0x46] & 4: set takes the bus branch with the node as the first bus (0x9F73D0 str r0,[sp,#0x18]) and satisfied = 0, [sp+0x44] = 0. The bus loop mask is (~0 & L) & 0x1FFFFD003F: bit 0 is in it.
        var store = new WwiseRtpcStore();
        var pbi = Pbi();
        store.RegisterListenerA19ECC(pbi, new WwiseRoutingNode { Id = 1, IsBus = true, SubscriptionKey10 = 0x1010, Node40 = 0, Byte46 = 4, SubscriptionMask14 = 1 }, L);
        Assert.Equal(1UL, store.ListenerMask(0x1010, pbi));
        // a bus with bit 2 clear takes the non-bus loop (0x9F7DA8): the same result there
        var other = Pbi();
        store.RegisterListenerA19ECC(other, new WwiseRoutingNode { Id = 2, IsBus = true, SubscriptionKey10 = 0x2010, Node40 = 0, SubscriptionMask14 = 1 }, L);
        Assert.Equal(1UL, store.ListenerMask(0x2010, other));
    }

    [Fact]
    public void C37_1_TwoSubscriptionsFeedingOnePbiParameterNeedTheOrderOptIn()
    {
        var curve = new WwiseRtpc(7001, 0, 0, 0, 0, 2, new[] { (0f, -1f, 1u), (1f, 0f, 4u) });
        WwiseRtpcStore Build(bool allow)
        {
            var s = new WwiseRtpcStore { AllowSubscriptionOrderApproximation = allow };
            s.SetRtpcs(7001, 1f, 7, 0x100, 0, 0, false);
            s.AddSubscription(new WwiseRtpcSubscription { Key1 = 0x1000, Param = 0, Type = 2, Accumulate = 1, Curves = new[] { curve } });
            s.AddSubscription(new WwiseRtpcSubscription { Key1 = 0x2000, Param = 0, Type = 2, Accumulate = 1, Curves = new[] { curve } });
            var pbi = Pbi(7, 0x100);
            pbi.Word64 = 0;
            s.AddChildA1973C(0x1000, pbi, new WwiseGainRtpcKey(7, 0x100), 1);
            s.AddChildA1973C(0x2000, pbi, new WwiseGainRtpcKey(7, 0x100), 1);
            return s;
        }
        Assert.Throws<WwiseMissingBehaviourException>(() => Build(false).SetRtpcs(7001, 0.5f, 7, 0x100, 0, 0, false));
        Build(true).SetRtpcs(7001, 0.5f, 7, 0x100, 0, 0, false);
    }

    [Fact]
    public void C37_1_ASubscriptionAddedAfterListenersAndATransitionWithAChildAreVisibleStops()
    {
        var curve = new WwiseRtpc(7001, 0, 0, 0, 0, 2, new[] { (0f, -1f, 1u), (1f, 0f, 4u) });
        var s = new WwiseRtpcStore();
        s.Apply(new WwiseStmgParam(7001, 1f, 2, 1f, 1f, false));                             // ramp type 2: the up and down times are 1 s
        s.AddSubscription(new WwiseRtpcSubscription { Key1 = 0x1000, Param = 0, Type = 2, Accumulate = 1, Curves = new[] { curve } });
        s.SetRtpcs(7001, 1f, 7, 0x100, 0, 0, false);                                         // no child yet: a positive-duration set is unobservable
        s.SetRtpcs(7001, 0.5f, 7, 0x100, 0, 0, false);
        s.AddChildA1973C(0x1000, Pbi(7, 0x100), new WwiseGainRtpcKey(7, 0x100), 1);
        Assert.Throws<WwiseMissingBehaviourException>(() => s.SetRtpcs(7001, 0.25f, 7, 0x100, 0, 0, false));   // 0xA13948 -> 0xA0E5E4 would deliver over time
        Assert.Throws<WwiseMissingBehaviourException>(() => s.AddSubscription(new WwiseRtpcSubscription { Key1 = 0x1000, Param = 2, Type = 2, Accumulate = 1, Curves = new[] { curve } }));   // 0xA11624
    }
}
