using System.Globalization;
using System.Text;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// B-M6b-4 batch 5h, M6-009, C39.1 / C39.2: the listener registration <c>0xA19ECC -> 0x9F7390</c> (both loops, holders 1/2 and 1/3/2, the 0x20-byte registry, the 0x28-byte entries, the lower_bound order of <c>0xA19778</c>, the duplicate rule, growth by one, the
/// activation <c>0xA1008C</c> before the allocation) and the removals <c>0xA19F60 -> 0x9F9064</c>, <c>0xA19D44</c>, <c>0xA1A0B4</c> (<c>0xA198A4</c>, <c>0xA10220</c>). The expected values are the engine's: <see cref="WwiseListenerOracle"/> was written by
/// <c>re-analysis/tools/emu/emu_listener.py</c>, which runs the real functions under Unicorn on hand-built engine registries (see its header for the three stand-ins). Nothing here compares against the C#'s own output.
/// </summary>
public class WwiseListenerTests
{
    private sealed class CaseWorld
    {
        public readonly WwiseRtpcStore Store = new();
        public readonly List<WwisePlayingInstance> Ctx = new();
        public readonly List<WwiseRoutingNode> Nodes = new();
        public readonly List<string> Manager = new();
        public readonly List<int> Collapse = new();
        public int Fail;
        public (int Ctx, int Node, ulong Mask, uint Flag) Reg;
        public (int Kind, int Ctx, int Arg, ulong Mask) Unreg;
        public int[] GList = Array.Empty<int>();
    }

    private static ulong H(string s) => ulong.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    private static uint HU(string s) => uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static WwisePlayingInstance NewCtx(WwiseListenerKey key)
        => new(new WwisePlayInitParams { PlayingId = 5, TargetNodeId = 3, GameObjectId = 7 }, 3, new object(), new byte[0x44], null, continuous: false) { ListenerKey = key };

    private static WwiseListenerKey ParseKey(string[] f)
        => new(HU(f[0]), HU(f[1]), HU(f[2]), (byte)HU(f[3]), (byte)HU(f[4]), HU(f[5]));

    private static WwiseRtpcRegistry? ParseRegistry(string t, List<WwisePlayingInstance> ctx)
    {
        if (t == "-") return null;
        var p = t.Split(',');
        var reg = new WwiseRtpcRegistry { MaskA = H(p[0]), Cache = H(p[1]), Capacity = int.Parse(p[2]), Byte1C = byte.Parse(p[3]) };
        if (p[4] != "_")
            foreach (var e in p[4].Split(';'))
            {
                var f = e.Split('.');
                reg.Entries.Add(new WwiseRtpcListener(ctx[int.Parse(f[7])], ParseKey(f[..6]), H(f[6])));
            }
        return reg;
    }

    private static CaseWorld Parse(string text)
    {
        var w = new CaseWorld();
        var ctxText = new List<(string Key, int N20)>();
        var nodeLines = new List<string[]>();
        foreach (var line in text.Split('\n'))
        {
            var p = line.Split(' ');
            switch (p[0])
            {
                case "F": w.Fail = int.Parse(p[1]); break;
                case "R": w.Reg = (int.Parse(p[1]), int.Parse(p[2]), H(p[3]), uint.Parse(p[4])); break;
                case "U": w.Unreg = (int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]), H(p[4])); break;
                case "G": w.GList = p.Skip(1).Where(x => x.Length > 0).Select(int.Parse).ToArray(); break;
                case "C": ctxText.Add((p[1], int.Parse(p[2]))); break;
                case "N": nodeLines.Add(p); break;
            }
        }
        foreach (var c in ctxText) w.Ctx.Add(NewCtx(ParseKey(c.Key.Split('.'))));
        for (int i = 0; i < nodeLines.Count; i++)
        {
            var p = nodeLines[i];
            // N bus b46 w40 parent out b68 w54 R1 R2 R3 ; the manager-side address of node i is its holder-1 key - 0x10
            uint addr = (uint)(0x100000 + i * 0x200);
            var n = new WwiseRoutingNode { Id = (uint)i, IsBus = p[1] == "1", SubscriptionKey10 = addr + 0x10 };
            n.Byte46 = (byte)HU(p[2]);
            n.Node40 = HU(p[3]);
            n.Word40 = HU(p[3]);
            n.Byte68 = (byte)HU(p[6]);
            n.Word54 = HU(p[7]);
            w.Nodes.Add(n);
        }
        for (int i = 0; i < nodeLines.Count; i++)
        {
            var p = nodeLines[i];
            var n = w.Nodes[i];
            int parent = int.Parse(p[4]), outBus = int.Parse(p[5]);
            n.Parent = parent >= 0 ? w.Nodes[parent] : null;
            n.OutputBus = outBus >= 0 ? w.Nodes[outBus] : null;
            n.Registry14 = ParseRegistry(p[8], w.Ctx);
            n.Registry20 = ParseRegistry(p[9], w.Ctx);
            n.RegistryC8 = ParseRegistry(p[10], w.Ctx);
        }
        for (int i = 0; i < ctxText.Count; i++)
            if (ctxText[i].N20 >= 0) w.Ctx[i].Ctx20Node = w.Nodes[ctxText[i].N20];
        w.Store.AllocationFailsA7A7F4 = n => n == w.Fail;
        w.Store.ManagerCallObserver = c => w.Manager.Add($"M{(c.ToArrayA ? 'A' : 'B')}:{HolderName(w, c.Holder)}:{c.Mask:x}");
        w.Store.CollapseCallObserver = b => w.Collapse.Add(w.Nodes.IndexOf(b));
        return w;
    }

    private static string HolderName(CaseWorld w, uint holder)
    {
        for (int i = 0; i < w.Nodes.Count; i++)
        {
            uint k = w.Nodes[i].SubscriptionKey10;
            if (holder == k) return $"n{i}h1";
            if (holder == k + 0xC) return $"n{i}h2";
            if (holder == k + 0xB4) return $"n{i}h3";
        }
        return "?" + holder.ToString("x");
    }

    private static string RegText(WwiseRtpcRegistry r, CaseWorld w)
    {
        var sb = new StringBuilder();
        sb.Append($"{r.MaskA:x}/{r.Cache:x}/{r.Count}/{r.Capacity}/{r.Byte1C:x}[");
        sb.Append(string.Join(";", r.Entries.Select(e =>
            $"{e.Key.W0:x}.{e.Key.W4:x}.{e.Key.W8:x}.{e.Key.B0C:x}.{e.Key.B10:x}.{e.Key.W14:x}.{e.Mask:x}.{w.Ctx.IndexOf(e.Pbi)}")));
        sb.Append(']');
        return sb.ToString();
    }

    private static string Snapshot(CaseWorld w)
    {
        var parts = new List<string>();
        for (int i = 0; i < w.Nodes.Count; i++)
        {
            var n = w.Nodes[i];
            if (n.Registry14 is { } a) parts.Add($"N{i}H1:{RegText(a, w)}");
            if (n.Registry20 is { } b) parts.Add($"N{i}H2:{RegText(b, w)}");
            if (n.RegistryC8 is { } c) parts.Add($"N{i}H3:{RegText(c, w)}");
        }
        for (int i = 0; i < w.Ctx.Count; i++)
            parts.Add($"X{i}:{(w.Ctx[i].Ctx20Node is { } n20 ? "n" + w.Nodes.IndexOf(n20) : "-")}");
        parts.AddRange(w.Manager);
        parts.AddRange(w.Collapse.Select(i => "K:" + i));
        parts.Add($"Z:{w.Store.AllocationCount}");
        return string.Join(" ", parts);
    }

    private static (string E1, string E2) Run(CaseWorld w)
    {
        w.Store.RegisterListenerA19ECC(w.Ctx[w.Reg.Ctx], w.Nodes[w.Reg.Node], w.Reg.Mask, w.Reg.Flag);
        string e1 = Snapshot(w);
        w.Manager.Clear();
        w.Collapse.Clear();
        switch (w.Unreg.Kind)
        {
            case 1: w.Store.RemoveListenerA19F60(w.Ctx[w.Unreg.Ctx], w.Unreg.Mask, 1); break;
            case 2: w.Store.RemoveListenerInDestructorA19D44(w.Ctx[w.Unreg.Ctx]); break;
            default:
                uint holder = w.Unreg.Arg >= 0 ? w.Nodes[w.Unreg.Arg].SubscriptionKey10 : 0;
                w.Store.UnregisterHolderA1A0B4(holder, w.GList.Select(i => w.Ctx[i]));
                break;
        }
        return (e1, Snapshot(w));
    }

    /// <summary>
    /// C39.1 / C39.2 against the engine: every case registers one listener (0xA19ECC) on a random node/bus graph with registries in holders 1, 2 and 3, then removes it (0xA19F60 with the PBI mask or a random mask, the destructor step 0xA19D44, or the node
    /// destructor step 0xA1A0B4), some with the n-th allocation failing. The registries (mask A, cache, count, capacity, byte +0x1C, every entry's key, mask and context in array order), [ctx+0x20], the manager call log, the 0x9C54E8 call log and the allocation
    /// count must equal the engine's after both steps. M6-009 C39.1 (0x9F7390, 0xA1973C, 0xA19778 lower_bound, duplicate rule, growth by one, activation before the allocation), C39.2 (0x9F9064, 0xA198A4, 0xA10220 sites, 0xA19F60, 0xA19D44, 0xA1A0B4).
    /// </summary>
    [Fact]
    public void C39_EveryRegistryEntryAndManagerCallMatchesTheEnginesOwnOutput()
    {
        var failures = new List<string>();
        int n = 0;
        foreach (var text in WwiseListenerOracle.Cases)
        {
            n++;
            var lines = text.Split('\n');
            string e1 = lines.First(l => l.StartsWith("E1 ")).Substring(3);
            string e2 = lines.First(l => l.StartsWith("E2 ")).Substring(3);
            var w = Parse(text);
            string c1, c2;
            try { (c1, c2) = Run(w); }
            catch (Exception ex) { failures.Add($"case {n - 1}: {ex.GetType().Name}: {ex.Message}"); continue; }
            if (c1 != e1) failures.Add($"case {n - 1} after the registration:\n engine {e1}\n C#     {c1}");
            else if (c2 != e2) failures.Add($"case {n - 1} after the removal:\n engine {e2}\n C#     {c2}");
        }
        Assert.True(n >= 3000, $"the oracle must hold at least 3000 random shapes, it has {n}");
        Assert.True(failures.Count == 0, $"{failures.Count} of {n} cases differ from the engine; first:\n" + string.Join("\n", failures.Take(4)));
    }

    // ------------------------------------------------------------------------------------------------ hand-derived facts of C39.1 (each expected value is in its comment)

    private const ulong L = 0x3FE3FFFE67BDUL;                                                // {0xFFFE67BD, 0x3FE3}: 0x9BC5DC..0x9BC5F0

    private static WwiseListenerKey Key(uint w0 = 7, uint w4 = 0, byte b0c = 0xFF, byte b10 = 0xFF, uint w14 = 0) => new(w0, w4, 0, b0c, b10, w14);

    [Fact]
    public void C39_1_TheActivationRunsBeforeTheAllocationSoAFailedAllocationSkipsTheEntryButLeavesTheSubscriptionMoved()
    {
        // 0x9F7E68..0x9F7E88: [reg+0x14] == 0 -> 0xA1008C(mgr, node+0x10, mask A); only then 0xA19778 and the growth 0x9F8858 (0xA7A7F4 -> 0 -> beq 0x9F8038). Research verification correction 2 (the emulated case: calls [('act', node+0x10, 1)], registry still count 0).
        var store = new WwiseRtpcStore { AllocationFailsA7A7F4 = n => n == 1 };
        var calls = new List<WwiseRtpcManagerCall>();
        store.ManagerCallObserver = calls.Add;
        var node = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, SubscriptionMask14 = 1 };
        var pbi = NewCtx(Key());
        store.RegisterListenerA19ECC(pbi, node, L);
        Assert.Equal(new[] { new WwiseRtpcManagerCall(true, 0x1010, 1UL) }, calls);
        Assert.Equal(0, node.Registry14!.Count);                                              // nothing inserted
        Assert.Equal(0, node.Registry14.Capacity);                                            // the capacity store 0x9F8964 is after the allocation
        Assert.Equal(ulong.MaxValue, node.Registry14.Cache);                                  // [reg+8] &= x is after the insert
        Assert.Equal(1, store.AllocationCount);
        Assert.Same(node, pbi.Ctx20Node);                                                     // 0xA19EF4 still stores [ctx+0x20] (there is no error value)
        // the next registration finds count 0 again: the activation is called again and the allocation (the second) succeeds
        store.RegisterListenerA19ECC(NewCtx(Key()), node, L);
        Assert.Equal(2, calls.Count);
        Assert.Equal(1, node.Registry14.Count);
    }

    [Fact]
    public void C39_1_TheArrayGrowsByExactlyOneEntryAndTheCacheIsTheAndOfTheEntryMasks()
    {
        // 0x9F7F04..0x9F7F10 / 0x9F8858: count >= capacity -> capacity + 1 (never doubles); 0x9F801C..0x9F802C [reg+8] &= x. A = 0x1F: the top node's M holds bits 0, 2, 3, 4 (0x127DF & L), so each x = 0x1D.
        var store = new WwiseRtpcStore();
        var node = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, SubscriptionMask14 = 0x1F };
        for (uint i = 0; i < 4; i++)
        {
            store.RegisterListenerA19ECC(NewCtx(Key(w0: 10 + i)), node, L);
            Assert.Equal((int)i + 1, node.Registry14!.Capacity);                              // 1, 2, 3, 4
            Assert.Equal((int)i + 1, node.Registry14.Count);
        }
        Assert.Equal(0x1DUL, node.Registry14!.Cache);                                         // the AND of four 0x1D masks, from ~0
        Assert.Equal(new uint[] { 10, 11, 12, 13 }, node.Registry14.Entries.Select(e => e.Key.W0).ToArray());   // the lower_bound order
        Assert.Equal(4, store.AllocationCount);
        // removal never shrinks the capacity (0xA19A44..0xA19A9C) and resets the cache to ~0 only when the count reaches 0 (0x9F91F4..0x9F921C)
        foreach (var e in node.Registry14.Entries.ToArray()) store.RemoveListenerA19F60(e.Pbi, L, 1);
        Assert.Equal(0, node.Registry14.Count);
        Assert.Equal(4, node.Registry14.Capacity);
        Assert.Equal(ulong.MaxValue, node.Registry14.Cache);
    }

    [Fact]
    public void C39_1_TheLowerBoundOrderTreatsByteDifferencesUnderTheMaskAsNotLessAndItsLastWordAsBhs()
    {
        // 0xA19778: byte@0xC compares as (b+1)&0x1F, byte@0x10 as (b+1)&0xFF, and equal-under-mask bytes that differ raw count as "not less" (research verification R2). The new entry goes to the first entry not less than it.
        var store = new WwiseRtpcStore();
        var node = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, SubscriptionMask14 = 1 };
        var first = NewCtx(Key(b0c: 0x1F));                                                   // (0x1F+1)&0x1F = 0
        var second = NewCtx(Key(b0c: 0xFF));                                                  // (0xFF+1)&0x1F = 0: a raw-different tie of the first
        var third = NewCtx(Key(b0c: 0x00));                                                   // (0+1)&0x1F = 1: strictly greater than both
        store.RegisterListenerA19ECC(first, node, L);
        store.RegisterListenerA19ECC(second, node, L);                                        // the first entry is "not less" than it: slot 0
        store.RegisterListenerA19ECC(third, node, L);                                         // both entries are less: the end
        Assert.Equal(new[] { second, first, third }, node.Registry14!.Entries.Select(e => e.Pbi).ToArray());
        // byte@0x10: 0xFF -> (0xFF+1)&0xFF = 0 is the smallest; 0x00 -> 1
        var n2 = new WwiseRoutingNode { Id = 2, SubscriptionKey10 = 0x2010, Node40 = 0, SubscriptionMask14 = 1 };
        var a = NewCtx(Key(b10: 0x00)); var b = NewCtx(Key(b10: 0xFF));
        store.RegisterListenerA19ECC(a, n2, L);
        store.RegisterListenerA19ECC(b, n2, L);
        Assert.Equal(new[] { b, a }, n2.Registry14!.Entries.Select(e => e.Pbi).ToArray());
        // the last word: bhs is "not less", so a key with a smaller w14 goes before an equal-or-greater one and an equal one goes before the existing equal entry
        var n3 = new WwiseRoutingNode { Id = 3, SubscriptionKey10 = 0x3010, Node40 = 0, SubscriptionMask14 = 1 };
        var w5 = NewCtx(Key(w14: 5)); var w3 = NewCtx(Key(w14: 3)); var w5b = NewCtx(Key(w14: 5));
        store.RegisterListenerA19ECC(w5, n3, L);
        store.RegisterListenerA19ECC(w3, n3, L);
        store.RegisterListenerA19ECC(w5b, n3, L);
        Assert.Equal(new[] { w3, w5b, w5 }, n3.Registry14!.Entries.Select(e => e.Pbi).ToArray());
    }

    [Fact]
    public void C39_1_ADuplicateIsAnEntryWithAllSixKeyFieldsRawEqualAndTheSameContextAndOnlyItsMaskIsOverwritten()
    {
        // 0x9F7EF0..0x9F7EF8, 0x9F8688..0x9F86E4, 0x9F8AC0..0x9F8ACC: the same context registering again overwrites the mask and changes neither the count, the capacity nor the cache; another context with the same key is a new entry.
        var store = new WwiseRtpcStore();
        var node = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, SubscriptionMask14 = 0x1F };
        var pbi = NewCtx(Key());
        store.RegisterListenerA19ECC(pbi, node, L);                                           // x = 0x1D
        Assert.Equal(0x1DUL, node.Registry14!.Entries.Single().Mask);
        node.SubscriptionMask14 = 0x1F;                                                       // unchanged, so a second register gives the same x
        store.RegisterListenerA19ECC(pbi, node, 0x5UL);                                       // L' = 5: M = 5 & (0x7E3FFFE0000|0x127DF) = 5, x = 5 & 0x1F = 5
        Assert.Equal(1, node.Registry14.Count);
        Assert.Equal(5UL, node.Registry14.Entries.Single().Mask);
        Assert.Equal(1, node.Registry14.Capacity);
        Assert.Equal(0x1DUL, node.Registry14.Cache);                                          // [reg+8] is not recomputed on an overwrite
        var other = NewCtx(Key());                                                            // the same key, another context
        store.RegisterListenerA19ECC(other, node, L);
        Assert.Equal(2, node.Registry14.Count);
        Assert.Equal(new[] { other, pbi }, node.Registry14.Entries.Select(e => e.Pbi).ToArray());   // the new equal-key entry takes the lower_bound slot
        // the removal is by the key and the context identity: another PBI's entry stays (0xA19994)
        store.RemoveListenerA19F60(pbi, L, 1);
        Assert.Equal(new[] { other }, node.Registry14.Entries.Select(e => e.Pbi).ToArray());
    }

    [Fact]
    public void C39_1_TheBusLoopMaskKeepsBit33AndTheHolderOrderIsOneThreeTwo()
    {
        // 0x9F744C..0x9F7454: r8 = 0xFFFD003F, sb = 0x1F, so the bus-loop mask is 0x1FFFFD003F (research verification correction 1): bit 33 survives, and L has bit 33 (0x3FE3 = bits 0, 1, 5..13 of the high word). A bus registry with mask A bit 33 gets x = bit 33
        // at all three holders; the activations run in the order holder 1 (site 0x9F74CC), holder 3 (0x9F76A8), holder 2 (0x9F7884) at the addresses bus+0x10, bus+0xC4, bus+0x1C.
        var calls = new List<WwiseRtpcManagerCall>();
        var store = new WwiseRtpcStore { ManagerCallObserver = calls.Add };
        var bus = new WwiseRoutingNode { Id = 8, IsBus = true, Node40 = 0, SubscriptionKey10 = 0x8010, Byte46 = 4, SubscriptionMask14 = 1UL << 33, ThirdHolderMaskC8 = 1UL << 33, SecondHolderMask20 = 1UL << 33 };
        var pbi = NewCtx(Key());
        store.RegisterListenerA19ECC(pbi, bus, L);                                            // a bus passed as the start node enters the bus branch directly
        Assert.Equal(new[]
        {
            new WwiseRtpcManagerCall(true, 0x8010, 1UL << 33),
            new WwiseRtpcManagerCall(true, 0x8010 + 0xB4, 1UL << 33),
            new WwiseRtpcManagerCall(true, 0x8010 + 0xC, 1UL << 33),
        }, calls);
        Assert.Equal(1UL << 33, store.ListenerMask(0x8010, pbi));
        Assert.Equal(1UL << 33, store.ListenerMask(0x8010 + 0xB4, pbi));
        Assert.Equal(1UL << 33, store.ListenerMask(0x8010 + 0xC, pbi));
        // the removal: holder 1, 3, 2 in that order, each told 0xA10220 because its count is 0 afterwards (0x9F910C, 0x9F915C, 0x9F91AC)
        calls.Clear();
        store.RemoveListenerA19F60(pbi, L, 1);
        Assert.Equal(new[]
        {
            new WwiseRtpcManagerCall(false, 0x8010, 1UL << 33),
            new WwiseRtpcManagerCall(false, 0x8010 + 0xB4, 1UL << 33),
            new WwiseRtpcManagerCall(false, 0x8010 + 0xC, 1UL << 33),
        }, calls);
    }

    [Fact]
    public void C39_2_TheRemovalTellsTheManagerWheneverTheRegistryIsEmptyAfterwardsEvenWhenTheContextWasNotThere()
    {
        // 0x9F90D8 bne 0x9F9214 / fall-through 0x9F90E0: 0xA10220 whenever the registry exists and its count is 0, found or not (research verification D5). A registry that does not exist is skipped (0x9F90C4 beq).
        var calls = new List<WwiseRtpcManagerCall>();
        var store = new WwiseRtpcStore { ManagerCallObserver = calls.Add };
        var withReg = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, SubscriptionMask14 = 4 };
        var noReg = new WwiseRoutingNode { Id = 2, SubscriptionKey10 = 0x2010, Node40 = 0, Parent = withReg };
        var pbi = NewCtx(Key());
        pbi.Ctx20Node = noReg;                                                                // a PBI whose [ctx+0x20] is set but which was never inserted
        store.RemoveListenerA19F60(pbi, L, 1);
        Assert.Equal(new[] { new WwiseRtpcManagerCall(false, 0x1010, 4UL) }, calls);
        Assert.Null(pbi.Ctx20Node);
        // a zero mask returns at once (0x9F9228..0x9F9230) but [ctx+0x20] is still cleared by 0xA19F8C
        pbi.Ctx20Node = noReg;
        calls.Clear();
        store.RemoveListenerA19F60(pbi, 0, 1);
        Assert.Empty(calls);
        Assert.Null(pbi.Ctx20Node);
    }

    [Fact]
    public void C39_2_TheNodeDestructorStepUnregistersEveryContextOfTheHolder()
    {
        // 0xA1A0B4(holder): for every context of the global list with [ctx+0x20]+0x10 == holder: 0x9F9064(node, ctx, &~0, 1), [ctx+0x20] = 0 (0xA1A0F4..0xA1A124); others are left.
        var store = new WwiseRtpcStore();
        var node = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, SubscriptionMask14 = 1 };
        var other = new WwiseRoutingNode { Id = 2, SubscriptionKey10 = 0x2010, Node40 = 0, SubscriptionMask14 = 1 };
        var a = NewCtx(Key(w0: 1)); var b = NewCtx(Key(w0: 2)); var c = NewCtx(Key(w0: 3));
        store.RegisterListenerA19ECC(a, node, L);
        store.RegisterListenerA19ECC(b, other, L);
        store.RegisterListenerA19ECC(c, node, L);
        store.UnregisterHolderA1A0B4(0x1010, new[] { a, b, c });
        Assert.Equal(0, node.Registry14!.Count);
        Assert.Null(a.Ctx20Node);
        Assert.Null(c.Ctx20Node);
        Assert.Same(other, b.Ctx20Node);
        Assert.Equal(1, other.Registry14!.Count);
    }

    // ------------------------------------------------------------------------------------------------ the shipped shapes of C39.4, built from the shipped banks by the stack's own hierarchy reader

    private sealed class Shipped
    {
        public readonly Dictionary<uint, WwiseNode> Nodes = new();
        public readonly Dictionary<uint, WwiseRoutingNode> Routes = new();
        public readonly Dictionary<uint, WwiseObject> Objects = new();
        private uint _next = 0x100000;

        public WwiseRoutingNode Route(uint id)
        {
            if (Routes.TryGetValue(id, out var r)) return r;
            var n = Nodes[id];
            _next += 0x200;
            r = new WwiseRoutingNode { Id = id, IsBus = n is WwiseBusNode, SubscriptionKey10 = _next + 0x10, Node40 = 0 };   // [node+0x40]: only bits 17.. of it matter and every shipped subscription bit is 0..5
            Routes[id] = r;
            uint parent = n.Params.ParentId;
            if (n is WwiseBusNode)
            {
                if (parent != 0 && Nodes.ContainsKey(parent)) r.OutputBus = Route(parent);                                // a bus's [+0x38] is its parent bus (C23 row 10)
            }
            else
            {
                if (parent != 0 && Nodes.ContainsKey(parent)) r.Parent = Route(parent);
                if (n.Params.BusId != 0 && Nodes.ContainsKey(n.Params.BusId)) r.OutputBus = Route(n.Params.BusId);          // OverrideBusId
            }
            ulong a = 0;
            foreach (var rt in n.Params.Rtpcs)
            {
                // the premise of Node40 = 0 (C39.4: every node RTPC param in the shipped banks is 0, 2 or 3; the bus RTPC params are 0 and 5): only bits below 6 are ever subscribed, so [node+0x40] << 17 (bits >= 17) cannot change a result
                Assert.Contains(rt.ParamId, new uint[] { 0, 2, 3, 5 });
                a |= 1UL << (int)WwiseBusWalk.ParamBitTable[rt.ParamId];
            }
            Assert.True(a < (1UL << 6), "the subscribed bits are all below 6");
            if (a != 0) r.SubscriptionMask14 = a;
            return r;
        }

        public uint RootOf(uint id)
        {
            while (Nodes[id].Params.ParentId is var p && p != 0 && Nodes.ContainsKey(p)) id = p;
            return id;
        }
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

    private static Shipped? LoadShipped(string meta, params string[] banks)
    {
        var s = new Shipped();
        foreach (var name in new[] { "Init.bnk" }.Concat(banks))
        {
            var path = Directory.EnumerateFiles(meta, name, SearchOption.AllDirectories).FirstOrDefault();
            if (path is null) return null;
            var bank = WwiseBank.Parse(File.ReadAllBytes(path), name);
            foreach (var o in bank.Objects.Values)
            {
                s.Objects[o.Id] = o;
                var node = WwiseHierarchy.TryRead(o, out _);
                if (node is not null) s.Nodes[node.Id] = node;
            }
        }
        // the shipped Cozmo_Robot channel configuration: C39 verification correction 5 (the Init.bnk bus object carries cfg 0x00004101, bytes 01 41 00 00; [bus+0x68] = 1)
        const uint CozmoRobot = 1723505802;
        var payload = s.Objects[CozmoRobot].Payload.ToArray();
        bool hasCfg = false;
        for (int i = 0; i + 3 < payload.Length; i++)
            if (payload[i] == 0x01 && payload[i + 1] == 0x41 && payload[i + 2] == 0 && payload[i + 3] == 0) hasCfg = true;
        Assert.True(hasCfg, "Init.bnk Bus 1723505802 carries the channel config bytes 01 41 00 00");
        s.Route(CozmoRobot).Byte68 = 1;
        return s;
    }

    [Fact]
    public void C39_4_EventVolumeReachesTheCozmoSoundsUnderItsActorMixersAndRobotVolumeReachesNone()
    {
        // C39.4 (verification correction 7): Cozmo.bnk has 2231 Sounds, 2206 of them under the event_volume ActorMixers 62050212 (1872) and 682998829 (334), registering at the root's holder 1 with mask bit 0; the 25 Switch-rooted ones (20 under 677877281, 5 under
        // 229678261) do not reach event_volume. robot_volume (0x637C1240) sits on Bus 1723505802 (param 5): the first bus of every Cozmo Sound is Cozmo_Robot or Cozmo_Robot_External with 0x9C54E8 == 1 at Cozmo_Robot, so x = M & A = 0 there, the listener is never inserted and the
        // subscription is never activated (it stays in array B); 0xA10220 is attempted at removal and is a no-op.
        var meta = FindMeta();
        if (meta is null) { Assert.Fail("re-analysis/obb/sound_meta (the unpacked shipped banks) was not found above the test binaries"); return; }
        var ship = LoadShipped(meta, "Cozmo.bnk");
        Assert.NotNull(ship);
        const uint EventVolume = 0xD2687048, RobotVolume = 0x637C1240, CozmoRobot = 1723505802;
        Assert.Equal(new uint[] { 62050212, 682998829 }, ship!.Nodes.Values.Where(n => n.Params.Rtpcs.Any(r => r.SourceId == EventVolume)).Select(n => n.Id).OrderBy(x => x).ToArray());
        Assert.Equal(new[] { CozmoRobot }, ship.Nodes.Values.Where(n => n.Params.Rtpcs.Any(r => r.SourceId == RobotVolume)).Select(n => n.Id).ToArray());
        Assert.Equal(5u, ship.Nodes[CozmoRobot].Params.Rtpcs.Single(r => r.SourceId == RobotVolume).ParamId);

        var sounds = ship.Nodes.Values.OfType<WwiseSoundNode>().Where(n => n.Bank == "Cozmo.bnk").ToList();
        Assert.Equal(2231, sounds.Count);
        var byRoot = sounds.GroupBy(n => ship.RootOf(n.Id)).ToDictionary(g => g.Key, g => g.ToList());
        Assert.Equal(1872, byRoot[62050212].Count);
        Assert.Equal(334, byRoot[682998829].Count);
        Assert.Equal(20, byRoot[677877281].Count);
        Assert.Equal(5, byRoot[229678261].Count);

        var calls = new List<WwiseRtpcManagerCall>();
        var store = new WwiseRtpcStore { ManagerCallObserver = calls.Add };
        var ctxs = new Dictionary<uint, WwisePlayingInstance>();
        foreach (var s in sounds)
        {
            var pbi = NewCtx(Key());
            ctxs[s.Id] = pbi;
            store.RegisterListenerA19ECC(pbi, ship.Route(s.Id), L);
        }
        foreach (var root in new uint[] { 62050212, 682998829 })
        {
            uint holder = ship.Route(root).SubscriptionKey10;
            Assert.Equal(1UL, ship.Route(root).SubscriptionMask14);                           // event_volume: param 0 -> subscription bit 0
            Assert.Equal(byRoot[root].Count, store.ListenerCount(holder));                    // every Sound under it
            Assert.All(byRoot[root], s => Assert.Equal(1UL, store.ListenerMask(holder, ctxs[s.Id])));
            Assert.Single(calls, c => c.ToArrayA && c.Holder == holder);                      // the activation 0xA1008C at count 0, once
        }
        foreach (var root in new uint[] { 677877281, 229678261 })                              // the Switch-rooted Sounds are at neither event_volume ActorMixer
            foreach (var mixer in new uint[] { 62050212, 682998829 })
                Assert.All(byRoot[root], s => Assert.Null(store.ListenerMask(ship.Route(mixer).SubscriptionKey10, ctxs[s.Id])));
        // robot_volume: the bus registry exists (mask A = bit 5) but no listener is ever inserted and no activation is made at its holder
        var robot = ship.Route(CozmoRobot);
        Assert.Equal(1UL << 5, robot.SubscriptionMask14);
        Assert.Equal(0, store.ListenerCount(robot.SubscriptionKey10));
        Assert.DoesNotContain(calls, c => c.Holder == robot.SubscriptionKey10);

        // the removal: Term's 0xA19F60 -> 0x9F9064 still attempts Cozmo_Robot (no mask test) and the manager is told 0xA10220 there (count 0, registry exists): a no-op for a subscription that was never moved to array A
        calls.Clear();
        foreach (var s in sounds) store.RemoveListenerA19F60(ctxs[s.Id], L, 1);
        Assert.NotEmpty(calls.Where(c => c.Holder == robot.SubscriptionKey10));
        Assert.All(calls.Where(c => c.Holder == robot.SubscriptionKey10), c => Assert.False(c.ToArrayA));
        foreach (var root in new uint[] { 62050212, 682998829 })
        {
            Assert.Equal(0, store.ListenerCount(ship.Route(root).SubscriptionKey10));
            Assert.Single(calls, c => !c.ToArrayA && c.Holder == ship.Route(root).SubscriptionKey10);   // 0xA10220 when the last listener left
        }
    }

    [Fact]
    public void C39_4_TheDevDebugAndTheBusLevelShapes()
    {
        // C39.4 / verification correction 7: Dev_Debug.bnk has 12 Sounds under ActorMixer 121198006 (event_volume, bit 0) and 2 under 280348264 on the SFX bus (no event_volume subscription in their chains); SFX.bnk has 101 Sounds and UI.bnk 14, whose chains reach the SFX bus
        // 393239870 (RTPC 0x5D3B9143, param 0) and the UI bus 1551306167 (0x667B2280, param 0): the PBIs register at the bus holder 1 (bit 0 passes: 0x20 only removes bit 5).
        var meta = FindMeta();
        if (meta is null) { Assert.Fail("re-analysis/obb/sound_meta (the unpacked shipped banks) was not found above the test binaries"); return; }
        var dev = LoadShipped(meta, "Dev_Debug.bnk");
        Assert.NotNull(dev);
        var devSounds = dev!.Nodes.Values.OfType<WwiseSoundNode>().Where(n => n.Bank == "Dev_Debug.bnk").ToList();
        Assert.Equal(14, devSounds.Count);
        var store = new WwiseRtpcStore();
        var ctxs = devSounds.ToDictionary(s => s.Id, _ => NewCtx(Key()));
        foreach (var s in devSounds) store.RegisterListenerA19ECC(ctxs[s.Id], dev.Route(s.Id), L);
        uint holder = dev.Route(121198006).SubscriptionKey10;
        Assert.Equal(12, store.ListenerCount(holder));
        Assert.Equal(12, devSounds.Count(s => dev.RootOf(s.Id) == 121198006));
        Assert.Equal(2, devSounds.Count(s => dev.RootOf(s.Id) == 280348264));
        Assert.All(devSounds.Where(s => dev.RootOf(s.Id) == 280348264), s => Assert.Null(store.ListenerMask(holder, ctxs[s.Id])));

        foreach (var (bank, bus, expectedSounds) in new[] { ("SFX.bnk", 393239870u, 101), ("UI.bnk", 1551306167u, 14) })
        {
            var ship = LoadShipped(meta, bank);
            Assert.NotNull(ship);
            var sounds = ship!.Nodes.Values.OfType<WwiseSoundNode>().Where(n => n.Bank == bank).ToList();
            Assert.Equal(expectedSounds, sounds.Count);
            var st = new WwiseRtpcStore();
            var pbis = sounds.ToDictionary(s => s.Id, _ => NewCtx(Key()));
            foreach (var s in sounds) st.RegisterListenerA19ECC(pbis[s.Id], ship.Route(s.Id), L);
            Assert.Equal(1UL, ship.Route(bus).SubscriptionMask14);                            // param 0 -> bit 0
            Assert.Equal(expectedSounds, st.ListenerCount(ship.Route(bus).SubscriptionKey10));
            Assert.All(sounds, s => Assert.Equal(1UL, st.ListenerMask(ship.Route(bus).SubscriptionKey10, pbis[s.Id])));
        }
    }

    /// <summary>
    /// M6-009 C39.1: the listener key a PBI carries at registration is the one the engine's ctor 0xA000E8 leaves (0x9BC90C's {game object, 0, 0, 0xFF, 0xFF, 0} then the ctor's overwrites 0xA00384..0xA00408), checked against 400 runs of the real ctor under Unicorn
    /// (WwiseListenerOracle.KeyCases): w4 = the playing id, w8 = the target node id when params+0x84 != 0 and a target exists, byte@0xC = params+0x85, byte@0x10 = params+0x86 for params+0x84 in {0x80, 0x90, 0xA0} else 0xFF, word@0x14 = the PBI address (here its identity).
    /// </summary>
    [Fact]
    public void C39_1_ThePbiKeyIsTheOneTheEnginesCtorLeaves()
    {
        Assert.True(WwiseListenerOracle.KeyCases.Length >= 400);
        foreach (var line in WwiseListenerOracle.KeyCases)
        {
            var f = line.Split(' ');
            uint go = HU(f[0]), playing = HU(f[1]), target = HU(f[2]);
            byte b84 = (byte)HU(f[3]), b85 = (byte)HU(f[4]), b86 = (byte)HU(f[5]);
            var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = playing, TargetNodeId = target, GameObjectId = go, SoundSpecial84 = b84, SoundSpecial85 = b85, SoundSpecial86 = b86 }, target, new object(), new byte[0x44], null, continuous: false);
            var key = pbi.ListenerKey;
            Assert.Equal((HU(f[7]), HU(f[8]), HU(f[9]), (byte)HU(f[10]), (byte)HU(f[11])), (key.W0, key.W4, key.W8, key.B0C, key.B10));
            Assert.Equal(f[12] == "1", key.W14 == pbi.ListenerIdentity);
        }
        // an unset input needed by the key stops visibly: params+0x86 when params+0x84 is 0x90
        var open = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, GameObjectId = 5, SoundSpecial84 = 0x90 }, 3, new object(), new byte[0x44], null, continuous: false);
        Assert.Throws<WwiseMissingBehaviourException>(() => open.ListenerKey);
        // two different PBIs with equal (w0, w4, w8, byte@0xC, byte@0x10) at one holder: the engine orders them by heap address, which the C# cannot reproduce
        var store = new WwiseRtpcStore();
        var node = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1010, Node40 = 0, SubscriptionMask14 = 1 };
        WwisePlayingInstance Make() => new(new WwisePlayInitParams { PlayingId = 5, GameObjectId = 7 }, 3, new object(), new byte[0x44], null, continuous: false);
        store.RegisterListenerA19ECC(Make(), node, L);
        Assert.Throws<WwiseMissingBehaviourException>(() => store.RegisterListenerA19ECC(Make(), node, L));
        Assert.Equal(1, node.Registry14!.Count);
    }
}
