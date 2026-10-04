using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// B-M6b-4 batch 5f, C37.1: the PBI Init's listener registration (<c>0xA0285C -> 0x9BC5A8 -> 0xA19ECC -> 0x9F7390</c>, the non-bus loop <c>0x9F7DA8..0x9F82D4</c>) and what stops visibly. Expected masks are computed by hand from the disassembly
/// (the constants are in each comment); there is no engine oracle for the walk (its callee 0xA1973C and the key words are unread). Nothing here compares against the C#'s own output.
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
        var odd = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 6, TargetNodeId = 3 }, 3, WwiseSourceDescriptor.FromSound(soundNode), new byte[0x44], null, continuous: false) { NodeE0 = soundNode };
        Assert.Throws<WwiseMissingBehaviourException>(() => limiter.InsertPbiA0285C(soundNode, odd));   // a key that is not the pull path's has no modelled key
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
    public void C37_1_TheListenerWalkStopsVisiblyAtWhatIsUnread()
    {
        var store = new WwiseRtpcStore { AllowBusBranchRegistryInference = true };       // TEST opt-in (NOT engine-derived) for the cases below that pass the bus branch
        Assert.Throws<WwiseMissingBehaviourException>(() => store.RegisterListenerA19ECC(Pbi(), new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010 }, L));                         // [node+0x40] not supplied
        Assert.Throws<WwiseMissingBehaviourException>(() => store.RegisterListenerA19ECC(Pbi(), new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, SecondHolderMask20 = 1 }, L));   // node+0x1C holder
        // the bus branch 0x9F73DC..: the first output bus has a registry (robot_volume on Bus 1723505802)
        var bus = new WwiseRoutingNode { Id = 1723505802, IsBus = true, SubscriptionKey10 = 0x9010, SubscriptionMask14 = 1UL << 5, Node40 = 0 };
        Assert.Throws<WwiseMissingBehaviourException>(() => store.RegisterListenerA19ECC(Pbi(), new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, OutputBus = bus }, L));
        // ... or a parent bus of it does
        var parentBus = new WwiseRoutingNode { Id = 5, IsBus = true, SubscriptionKey10 = 0x9110, SubscriptionMask14 = 1 };
        var childBus = new WwiseRoutingNode { Id = 6, IsBus = true, SubscriptionKey10 = 0x9210, OutputBus = parentBus };
        Assert.Throws<WwiseMissingBehaviourException>(() => store.RegisterListenerA19ECC(Pbi(), new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, OutputBus = childBus }, L));
        // by default ANY found output bus stops (the bus branch is unread, zero inference), even one without a registry
        var plainBus0 = new WwiseRoutingNode { Id = 7, IsBus = true, SubscriptionKey10 = 0x9310, Node40 = 0 };
        Assert.Throws<WwiseMissingBehaviourException>(() => new WwiseRtpcStore().RegisterListenerA19ECC(Pbi(), new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, OutputBus = plainBus0 }, L));
        // with the test opt-in a bus chain without a registry is let through, and the PBI still registers on its mixer
        var plainBus = new WwiseRoutingNode { Id = 7, IsBus = true, SubscriptionKey10 = 0x9310, Node40 = 0 };
        var mixer = new WwiseRoutingNode { Id = 2, SubscriptionKey10 = 0x2010, Node40 = 0, SubscriptionMask14 = 1 };
        var pbi = Pbi();
        store.RegisterListenerA19ECC(pbi, new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, Parent = mixer, OutputBus = plainBus }, L);
        Assert.Equal(1UL, store.ListenerMask(0x2010, pbi));
    }

    [Fact]
    public void C37_1_TheListenerIsRemovedAtTermsLastStepAndDestructionAfterTermNeedsNoSeam()
    {
        // Term 0xA029DC ends with the tail 0x9BDC8C(ctx, 0) -> 0xA19F60: with [ctx+0x20] != 0, 0x9F9064 removes the PBI (0xA198A4) and [ctx+0x20] = 0. A set after Term no longer reaches the PBI. The destructor's 0xA19D44 then sees [ctx+0x20] == 0 and removes nothing.
        var soundNode = SoundNode();
        var mixer = new WwiseRoutingNode { Id = 2, SubscriptionKey10 = 0x2010, Node40 = 0, SubscriptionMask14 = 1 };
        var leaf = new WwiseRoutingNode { Id = 3, SubscriptionKey10 = 0x1010, Node40 = 0, Parent = mixer };
        var store = new WwiseRtpcStore();
        var limiter = WwisePlaybackLimiterTestDoubles.Create(_ => soundNode);          // its TermSteps10To13 double stands for 0xA3E27C / 0xA1E8F4 / the rest of 0x9BDC8C only
        limiter.RtpcListeners = store;
        limiter.RuntimeNodeOf = _ => leaf;
        var curve = new WwiseRtpc(7001, 0, 0, 0, 0, 2, new[] { (0f, -1f, 1u), (1f, 0f, 4u) });
        store.SetRtpcs(7001, 1f, 7, 5, 0, 0, false);
        store.AddSubscription(new WwiseRtpcSubscription { Key1 = 0x2010, Param = 0, Type = 2, Accumulate = 1, Curves = new[] { curve } });
        WwisePlayingInstance NewOne() => new(new WwisePlayInitParams { PlayingId = 5, TargetNodeId = 3, GameObjectId = 7 }, 3, WwiseSourceDescriptor.FromSound(soundNode), new byte[0x44], new WwiseGainRtpcKey(7, 5), continuous: false) { NodeE0 = soundNode };
        var unseamed = NewOne();
        Assert.Equal(1, limiter.InsertPbiA0285C(soundNode, unseamed));
        Assert.Throws<WwiseMissingBehaviourException>(() => limiter.TermPbiA029DC(unseamed));   // 0xA198A4 is unread: the required seam
        limiter.RemoveRtpcListenerA198A4 = store.UnregisterListener;                         // HOST / TEST implementation, not engine-derived
        var pbi = NewOne();
        Assert.Equal(1, limiter.InsertPbiA0285C(soundNode, pbi));
        Assert.Same(leaf, pbi.Ctx20Node);
        store.SetRtpcs(7001, 0.6f, 7, 5, 0, 0, false);
        float before = pbi.Field98;
        Assert.NotEqual(0f, before);                                                         // delivered while live
        limiter.TermPbiA029DC(pbi);
        Assert.False(store.HasListener(pbi));
        Assert.Null(pbi.Ctx20Node);                                                          // 0xA19F8C
        store.SetRtpcs(7001, 0.3f, 7, 5, 0, 0, false);
        Assert.Equal(before, pbi.Field98);                                                   // no longer reached after Term
        limiter.RemoveRtpcListenerA198A4 = null;
        limiter.DestroyPbiVt4(pbi);                                                          // 0xA19D44 skips 0x9F9064: no seam needed
        // a PBI destroyed without Term (Ctx20Node still set) takes 0x9F9064 in the destructor: the seam again
        Assert.Throws<WwiseMissingBehaviourException>(() => limiter.DestroyPbiVt4(unseamed));
        limiter.RemoveRtpcListenerA198A4 = store.UnregisterListener;
        limiter.DestroyPbiVt4(unseamed);
        Assert.False(store.HasListener(unseamed));
    }

    [Fact]
    public void C37_1_AStartNodeWithBit2Of0x46TakesTheUnreadBusCategoryLoop()
    {
        // 0x9F73AC..0x9F73C4 reads only [node+0x46] & 4; a bus with bit 2 clear takes the non-bus path (0x9F7DA8).
        var store = new WwiseRtpcStore();
        Assert.Throws<WwiseMissingBehaviourException>(() => store.RegisterListenerA19ECC(Pbi(), new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, Byte46 = 4 }, L));
        var pbi = Pbi();
        store.RegisterListenerA19ECC(pbi, new WwiseRoutingNode { Id = 1, IsBus = true, SubscriptionKey10 = 0x1010, Node40 = 0, SubscriptionMask14 = 1 }, L);
        Assert.Equal(1UL, store.ListenerMask(0x1010, pbi));
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
