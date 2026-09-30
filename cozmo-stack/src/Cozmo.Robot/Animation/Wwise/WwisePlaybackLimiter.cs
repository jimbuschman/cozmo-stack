// fidelity: M6-026
namespace Cozmo.Robot.Animation.Wwise;

// The playback-limit (max-instances) walker (M6-026), built in B-M6b-4 batch 4b from correction C28 and the rows of
// re-analysis/research/20260929-B-M6b-4-batch4b-limits.md (P1..P7, W1..W8, L1..L7, G1..G4, O1..O6, V1..V4, K1..K10,
// 8.1..8.9, C1..C7, E1..E3, R1..R3) with the replacements of the citation check.
//
// Production entry. The engine function is 0xA379D8 (the Sound Play path), which calls node->vt+0x90 = 0x9ED2CC at
// 0xA37D94 (bus class: 0x9C4F30). The C# counterpart is WwisePlaybackBridge.PlaySound (called from
// WwiseEventRuntime.ExecutePlay through IWwisePlaybackBridge.OnPlay), which calls WwisePlaybackLimiter.Walk. The
// limiter is the bridge's Limiter property; there is no other copy of this behaviour.
//
// Every RECOVERABLE_GAP body the shipped path reaches is a required seam below: a property that throws
// WwiseMissingBehaviourException when it is reached unset. Nothing is defaulted.

/// <summary>
/// <c>pbi+0x1EC/+0x1F0/+0x1F4</c>: the array of limiter lists a PBI is in. The walk appends (G2, O5), <c>0xA0285C</c> inserts the
/// PBI into every entry (8.7) and Term removes it from every entry (R3).
/// </summary>
public sealed class WwiseLimiterArray
{
    /// <summary><c>+0x1EC</c> data, <c>+0x1F0</c> count.</summary>
    public List<WwisePbiList> Items { get; } = new();

    /// <summary><c>+0x1F4</c>: the capacity; the walk grows it by 3 when the count reaches it (G2, O5).</summary>
    public int Capacity { get; set; }

    /// <summary><c>[pbi+0x1F0]</c>.</summary>
    public int Count => Items.Count;
}

/// <summary>
/// The list object <c>0x9F3274</c> inserts into: <c>obj+0x38</c> of a node limiter, <c>entry+0x28</c> of a per-object entry and the global
/// voice list <c>G+0x20</c> (L2, 8.9). Offsets: <c>+0</c> data, <c>+4</c> count, <c>+8</c> capacity, <c>+0xC</c> max u16 (the limiter's
/// <c>+0x44</c>), <c>+0xE</c> KillNewest, <c>+0xF</c> virtual, <c>+0x18</c>/<c>+0x1C</c> the 64-bit key, <c>+0x20</c> count u16 (the limiter's
/// <c>+0x58</c>), <c>+0x22</c> virtual count u16 (the limiter's <c>+0x5A</c>).
/// </summary>
public sealed class WwisePbiList
{
    /// <summary>The PBIs in array order (descending priority, 8.1).</summary>
    public List<WwisePlayingInstance> Items { get; } = new();

    /// <summary><c>+8</c>: the capacity; 8.4 grows it by 8.</summary>
    public int Capacity { get; set; }

    /// <summary><c>+0xC</c>: the max instances (u16).</summary>
    public ushort Max { get; set; }

    /// <summary><c>+0xE</c>: nonzero selects the ascending tie order and, in <c>0xA37100</c>, rejects an equal-priority newcomer.</summary>
    public bool KillNewest { get; set; }

    /// <summary><c>+0xF</c>: the virtual flag.</summary>
    public bool Virtual { get; set; }

    /// <summary><c>+0x18</c>: the low word of the sort key of the global limiter array (L3).</summary>
    public uint KeyLo { get; set; }

    /// <summary><c>+0x1C</c>: the high word of that key.</summary>
    public uint KeyHi { get; set; }

    /// <summary><c>+0x20</c> u16: incremented by <c>0x9F3274</c> on every successful insert (8.4).</summary>
    public ushort Count20 { get; set; }

    /// <summary><c>+0x22</c> u16: the number of PBIs in the list that are virtual (R3 (a)).</summary>
    public ushort Virtual22 { get; set; }
}

/// <summary>One slot of a limiter's per-game-object map: <c>{gameObject, entry*}</c> (O3, 8 bytes).</summary>
public sealed class WwiseLimiterMapEntry
{
    /// <summary>The game object key.</summary>
    public uint? Go { get; init; }

    /// <summary>The entry pointer; null is the zero pointer (O4 <c>e == 0</c>).</summary>
    public WwiseObjectLimiter? Entry { get; set; }
}

/// <summary>The 0x50-byte per-game-object entry <c>0x9FA410</c> creates (O3); its PBI list is <c>entry+0x28</c>.</summary>
public sealed class WwiseObjectLimiter
{
    /// <summary><c>+8</c>: the game object.</summary>
    public uint? Go { get; init; }

    /// <summary><c>entry+0x28</c>: the list (max is <c>entry+0x34</c>, KillNewest <c>+0x36</c>, virtual <c>+0x37</c>, counts <c>+0x48/+0x4A</c>).</summary>
    public WwisePbiList List { get; } = new();

    /// <summary>The subscriber's <c>[this+0x20]</c> (<c>0xA19ECC</c>): the node it subscribed on.</summary>
    public uint? SubscriberNode20 { get; set; }
}

/// <summary>The 0x70-byte limiter object <c>0x9F29E8</c> creates for a node or bus (L1, L2).</summary>
public sealed class WwiseNodeLimiter
{
    /// <summary>The owning node's id (<c>[node+0x30]</c> points here).</summary>
    public uint NodeId { get; init; }

    /// <summary><c>obj+0x38</c>: the list.</summary>
    public WwisePbiList List { get; } = new();

    /// <summary><c>+0x68</c> bit 0: 1 selects the global variant <c>0x9FA01C</c>, 0 the per-object variant <c>0x9FA6F8</c> (W3).</summary>
    public bool Global68 { get; set; }

    /// <summary><c>+0x60</c> (s16): incremented by <c>0x9FAC54</c> for every walk (W6).</summary>
    public short Count60 { get; set; }

    /// <summary><c>+0x62</c> (s16): incremented when the bus flag is set (W6).</summary>
    public short Count62 { get; set; }

    /// <summary><c>+0x64</c> (s16): read only by the idle test (L7); nothing built writes it.</summary>
    public short Count64 { get; set; }

    /// <summary><c>+0..+8</c>: the per-game-object map (a linear list of <c>{go, entry*}</c>, grown by 1, O2/O3).</summary>
    public List<WwiseLimiterMapEntry> Map { get; } = new();

    /// <summary>The map's capacity (grown by 1, O3).</summary>
    public int MapCapacity { get; set; }

    /// <summary><c>+0xC</c>: the context list head-first (<c>0x9BC5A8</c> pushes, R1; Term removes, R2).</summary>
    public List<WwisePlayingInstance> Contexts0C { get; } = new();

    /// <summary>The subscriber's <c>[this+0x20]</c> at <c>obj+0x10+0x20</c> (<c>0xA19ECC</c>, L5).</summary>
    public uint? SubscriberNode20 { get; set; }

}

/// <summary>
/// The block <c>0xA379D8</c> builds at <c>sp+0x4C</c> and passes to <c>node vt+0x90</c> (P4, C23 B2): <c>+0</c> priority, <c>+4</c> game
/// object, <c>+8</c> the array (<c>pbi+0x1EC</c>), <c>+0xC</c> u16 (bit 0 = "no node with an output bus passed yet"), <c>+0xE</c> u16 kill
/// count, <c>+0x10</c> byte (stop further limit checks), <c>+0x11</c> byte (allow the check).
/// </summary>
public sealed class WwiseLimitBlock
{
    /// <summary><c>+0</c>.</summary>
    public float Priority { get; set; }

    /// <summary><c>+4</c>.</summary>
    public uint? GameObject { get; set; }

    /// <summary><c>+8</c>; null is the zero pointer Term passes (R3).</summary>
    public WwiseLimiterArray? Array { get; set; }

    /// <summary><c>+0xC</c>: 3 at the call site (P4).</summary>
    public ushort Word0C { get; set; } = 3;

    /// <summary><c>+0xE</c>: the kill count.</summary>
    public ushort Count0E { get; set; }

    /// <summary><c>+0x10</c>.</summary>
    public byte B10 { get; set; }

    /// <summary><c>+0x11</c>.</summary>
    public byte B11 { get; set; }
}

/// <summary>The playback-limit walker, the limiter objects and the process-wide limiter state (<c>0x108DE78</c>, M6-026).</summary>
public sealed class WwisePlaybackLimiter
{
    // ------------------------------------------------------------------ the engine's float constants (bit patterns)

    /// <summary>101.0f, <c>0xA373F0</c> (V1) and <c>0xA379D0</c> (C5) and <c>0xA37864</c> (C2).</summary>
    public const int VictimInitialPriorityBits = 0x42CA0000;

    /// <summary>50.0f: the priority default <c>[0x108DB20+0x1C]</c> (P2a, <c>0x4DE054..0x4DE064</c>).</summary>
    public const int DefaultPriorityBits = 0x42480000;

    /// <summary>-10.0f: the priority distance offset default <c>[0x108DB20+0x20]</c> (P2a, <c>0x4DE04C..0x4DE058</c>).</summary>
    public const int DefaultPriorityOffsetBits = unchecked((int)0xC1200000);

    /// <summary>1.0f: both memory thresholds (C3) and the compare constant of <c>0xA376C0</c>.</summary>
    public const int OneBits = 0x3F800000;

    /// <summary>101.0f as a float.</summary>
    public static float VictimInitialPriority => BitConverter.Int32BitsToSingle(VictimInitialPriorityBits);

    /// <summary>The global voice list's static-ctor maximum <c>0x100</c> (8.9, <c>0x4DE33C..0x4DE344</c>).</summary>
    public const ushort StaticCtorMaxVoices = 0x100;

    // ------------------------------------------------------------------ construction and required seams

    private readonly Func<uint, WwiseNode?> _nodeLookup;
    private readonly Dictionary<uint, WwiseNodeLimiter> _limiters = new();

    /// <param name="nodeLookup">Resolves a node or bus id to its parsed node (the node graph <c>[node+0x34]</c>, <c>[node+0x38]</c> point into).</param>
    public WwisePlaybackLimiter(Func<uint, WwiseNode?> nodeLookup)
    {
        _nodeLookup = nodeLookup ?? throw new ArgumentNullException(nameof(nodeLookup));
        // 8.9: the static ctor 0x4DE2B8 builds the global voice list with max 0x100, KillNewest 1, virtual 1.
        GlobalVoiceList = new WwisePbiList { Max = StaticCtorMaxVoices, KillNewest = true, Virtual = true };
    }

    /// <summary>
    /// The pool allocation failure branch (<c>0xA7A7F4</c> returning null). Null means allocation never fails. Each call is one allocation
    /// attempt at a site the rows name: the limiter object (L1), the list array (8.4), the array growth (G2, O5), the map (O3) and the
    /// global limiter array (8.5).
    /// </summary>
    public Func<bool>? AllocationFails { get; set; }

    /// <summary>
    /// <c>0xA7ABA8(pool, out)</c> (C1): <c>(total, used)</c> of pool 0 (<c>[0x1052418]</c>) or 1 (<c>[0x1052428]</c>). It is read only when a
    /// threshold is below 1.0f, which no shipped Play does (C3). Required then.
    /// </summary>
    public Func<int, (uint Total, uint Used)>? PoolStatsA7ABA8 { get; set; }

    /// <summary><c>[0x108D918]</c> (C1 test 1): 1.0f in every shipped run (C3).</summary>
    public float MemoryThreshold1 { get; set; } = BitConverter.Int32BitsToSingle(OneBits);

    /// <summary><c>[0x108DA68]</c> (C1 test 2): 1.0f in every shipped run (C3).</summary>
    public float MemoryThreshold2 { get; set; } = BitConverter.Int32BitsToSingle(OneBits);

    /// <summary>
    /// <c>0xA19ECC(subscriber, node, {mask}, 1)</c> -> <c>0x9F7390..0x9F82EC</c> (L5, O3, R1): the RTPC-change subscription walk. Its body is
    /// a RECOVERABLE_GAP that "changes nothing on shipped data"; the call is required so it is visible. The argument is the subscriber
    /// kind (<c>"limiter"</c>, <c>"object"</c>, <c>"context"</c>), the node and the 64-bit mask.
    /// </summary>
    public Action<string, WwiseNode, ulong>? RtpcSubscribeA19ECC { get; set; }

    /// <summary><c>0xA01768</c> (C27 step 4, 8.x V2/C5/E3): the PBI's next-source code. <see cref="WwisePlaybackBridge.Limiter"/> sets it to the bridge's own <c>0xA01768</c> when the limiter is attached (unless already set); a limiter used without a bridge must supply it for the virtual paths.</summary>
    public Func<WwisePlayingInstance, int>? NextSourceCodeA01768 { get; set; }

    /// <summary><c>0xA366F4(M, item, id, end, time, curve, mode)</c> (K4): re-aim the running transition at <c>pbi+0x144</c>.</summary>
    public Action<WwisePlayingInstance, uint, float, uint, int, int>? ReaimTransitionA366F4 { get; set; }

    /// <summary><c>0xA36618(M, item, pbi+8)</c> (K6): cancel the transition at <c>pbi+0x144</c> or <c>pbi+0x148</c> (the argument is the offset).</summary>
    public Action<WwisePlayingInstance, int>? CancelTransitionA36618 { get; set; }

    /// <summary><c>0xA19F60(obj+0x10, {0x10000, 0}, 1)</c> (1.8 step 2): the RTPC unsubscribe <c>0x9F4D40</c> makes for every destroyed limiter. RECOVERABLE_GAP, required.</summary>
    public Action<WwiseNodeLimiter>? RtpcUnsubscribeA19F60 { get; set; }

    /// <summary>Term steps 6 and 7 (1.10): <c>0xA1C660(pbi+0xEC, pbi+0x14)</c> and the <c>[pbi+0x10C]</c> free. Unread, required.</summary>
    public Action<WwisePlayingInstance>? TermSteps6And7 { get; set; }

    /// <summary>Term steps 10 to 13 (1.10, O3): <c>0xA1ECBC([pbi+0x150])</c>, <c>[pbi+0x108]</c> <c>vt+0</c>, the <c>[pbi+0x1E8]</c> list, the <c>[pbi+0x150]</c> destroy (<c>0xA1E8F4</c>) and <c>0x9BDC8C(pbi+0xC, 0)</c>. Unread, required.</summary>
    public Action<WwisePlayingInstance>? TermSteps10To13 { get; set; }

    /// <summary>The <c>[bus+0x64] != 0 &amp;&amp; [bus+0x84] != 0 &amp;&amp; 0x9C5154(bus) == 1</c> gate of <c>0x9C5240</c> (1.3). RECOVERABLE_GAP; required only for a bus with a nonzero recovery time.</summary>
    public Func<WwiseBusNode, bool>? BusRecoveryGateA9C5154 { get; set; }

    /// <summary><c>0xA1EC54([pbi+0x150], pbi+0x1DC, pbi+0x1E0, pbi+0x108)</c> (6.1): run by <c>0xA0285C</c> after <c>0xA04D48</c>. Unread, required.</summary>
    public Action<WwisePlayingInstance>? SourceInfoA1EC54 { get; set; }

    /// <summary><c>0xA04DE8(mgr, id, pbi)</c>, the playing-id release Term makes when <c>[pbi+0x140] != 0</c> (1.10 step 5, <c>0xA02C98</c>): decrements the entry's <c>+0x18</c> and tail-calls <c>0xA03618</c> (RECOVERABLE_GAP). Required.</summary>
    public Action<WwisePlayingInstance>? ReleasePlayingIdA04DE8 { get; set; }

    /// <summary>
    /// <c>0xA04D48(mgr, id, pbi, out)</c> (6.2): <c>[entry+0x18]++</c> on the playing-id entry when it exists (nothing when it doesn't); it always returns 1 for a non-null PBI. The entry table is
    /// the event manager's, so the caller supplies it. Required.
    /// </summary>
    public Action<WwisePlayingInstance>? RegisterPlayingIdA04D48 { get; set; }

    /// <summary><c>0xA01DE0</c> -> <c>0xA4B3E8(voice, ..)</c> (E2): whether the PBI's voice is audible. RECOVERABLE_GAP, required when a PBI has a voice.</summary>
    public Func<WwiseLiveVoice, bool>? VoiceAudibleA4B3E8 { get; set; }

    /// <summary><c>0x9C4D04..0x9C4F0C</c> (W8): the duck start/release body of <c>0x9C4CD0</c>, reached only when the bus has duck entries. RECOVERABLE_GAP, required then.</summary>
    public Action<WwiseBusNode>? DuckBodyA9C4CD0 { get; set; }

    /// <summary><c>0xA0054C</c> (8.7): the re-registration body of <c>0xA00494</c>, reached for a PBI with an empty array and <c>pbi+0xE9</c> bit 2 clear. Unread, required.</summary>
    public Action<WwisePlayingInstance>? ReRegisterA0054C { get; set; }

    // ------------------------------------------------------------------ process-wide state (G = 0x108DE78)

    /// <summary>The global voice list <c>G+0x20</c> (8.9): max 0x100 until the STMG max voices is applied, KillNewest 1, virtual 1.</summary>
    public WwisePbiList GlobalVoiceList { get; }

    /// <summary>
    /// <c>0x9A0EA4</c> (8.9, C4): the STMG max voices overwrites the list's <c>+0xC</c> (<c>strh r3,[ip,#0xC]</c>). The shipped Init.bnk value is 256.
    /// </summary>
    public void ApplyStmgMaxVoices(ushort maxVoices) => GlobalVoiceList.Max = maxVoices;

    /// <summary>The global PBI list (<c>[G+0x50]</c>, next <c>+0x104</c>); <c>[G+0x48]</c> is its count (C4).</summary>
    public List<WwisePlayingInstance> GlobalPbiList { get; } = new();

    /// <summary><c>[G]</c>: the number of PBIs with <c>1BE</c> bit 5 (C4).</summary>
    public uint GlobalVirtualCount { get; set; }

    /// <summary><c>[G+0x54..0x5C]</c>: the registered lists sorted descending by their 64-bit key (8.5).</summary>
    public List<WwisePbiList> GlobalLimiterArray { get; } = new();

    /// <summary><c>[G+0x5C]</c>: the array capacity, grown by 1 (8.5).</summary>
    public int GlobalLimiterArrayCapacity { get; set; }

    /// <summary><c>[G+0x60]</c> (E1 (a)); nothing built writes it.</summary>
    public byte G60 { get; set; }

    /// <summary><c>[G+0x61]</c> (E1 (a)); nothing built writes it.</summary>
    public byte G61 { get; set; }

    private readonly Dictionary<uint, byte> _busCC = new();

    /// <summary>A bus's <c>[bus+0xCC]</c> (bits 0..2 are written by the walk (W7) and by <c>0x9C5240</c> (1.3)); it belongs to the bus, not to its limiter.</summary>
    public byte BusCC(uint busId) => _busCC.TryGetValue(busId, out var v) ? v : (byte)0;

    /// <summary>Sets <c>[bus+0xCC]</c> (a test places other bits with it).</summary>
    public void SetBusCC(uint busId, byte value) => _busCC[busId] = value;

    /// <summary>The limiter object of a node or bus (<c>[node+0x30]</c>), or null.</summary>
    public WwiseNodeLimiter? LimiterOf(uint nodeId) => _limiters.TryGetValue(nodeId, out var l) ? l : null;

    // ------------------------------------------------------------------ node facts (B8, D6.1)

    private readonly record struct NodeFacts(
        ushort Max, bool KillNewest, bool Virtual, bool GlobalFlag, bool StopChecks, bool Rtpc16, bool Rtpc17, bool Bit45_4);

    private static NodeFacts Facts(WwiseNode node)
    {
        bool rtpc16 = node.Params.Rtpcs.Any(r => r.ParamId == 0x10);
        bool rtpc17 = node.Params.Rtpcs.Any(r => r.ParamId == 0x11);
        if (node is WwiseBusNode bus)
        {
            // D6.1: byte B b0 -> +0x45 bit2 (KillNewest), b1 -> +0x45 bit3 (virtual), b2 -> +0x47 bit6. +0x45 bit6 has no writer on a bus.
            return new NodeFacts((ushort)(bus.MaxInstances & 0x3FF), (bus.ByteB & 1) != 0, (bus.ByteB & 2) != 0,
                false, (bus.ByteB & 4) != 0, rtpc16, rtpc17, false);
        }
        // B8: byte0 bit0 -> node+0x45 bit2, bit1 -> +0x45 bit3, bit2 -> +0x45 bit6, bit3 -> +0x47 bit6, bit4 -> +0x45 bit4;
        // the u16 & 0x3FF -> node+0x44.
        var p = node.Params;
        return new NodeFacts((ushort)(p.AdvancedMaxInstancesRaw & 0x3FF), (p.AdvancedByte0 & 1) != 0, (p.AdvancedByte0 & 2) != 0,
            (p.AdvancedByte0 & 4) != 0, (p.AdvancedByte0 & 8) != 0, rtpc16, rtpc17, (p.AdvancedByte0 & 0x10) != 0);
    }

    private WwiseNode Resolve(uint id, string what)
        => _nodeLookup(id) ?? throw new WwiseMissingBehaviourException(
            $"M6-026: the {what} {id} is not in the node graph; the inventory does not say what the engine does with a dangling link");

    /// <summary><c>[node+0x34]</c>: the parent node. A bus has none (its parent bus is <c>[bus+0x38]</c>, W7).</summary>
    private WwiseNode? ParentOf(WwiseNode node)
        => node is WwiseBusNode || node.Params.ParentId == 0 ? null : Resolve(node.Params.ParentId, "parent node");

    /// <summary><c>[node+0x38]</c>: the output bus of a node, or the parent bus of a bus.</summary>
    private WwiseBusNode? OutputBusOf(WwiseNode node)
    {
        uint id = node is WwiseBusNode ? node.Params.ParentId : node.Params.BusId;
        if (id == 0) return null;
        return Resolve(id, "bus") as WwiseBusNode ?? throw new WwiseMissingBehaviourException(
            $"M6-026: {id} is linked as a bus but is not a bus object");
    }

    private bool Alloc() => AllocationFails?.Invoke() != true;

    private static WwiseMissingBehaviourException RtpcMissing()
        => new("M6-026 L4/G1/O1: the node carries RTPC parameter 0x10 (max instances); 0xA11590 -> 0xA17724/0xA17878 (the curve evaluation) is a RECOVERABLE_GAP. " +
               "No shipped node has one (C28.8)");

    // ------------------------------------------------------------------ P1a: 0x9EEDA4

    /// <summary>
    /// <c>0x9EEDA4(node, &amp;out)</c> (P1a): climbs from the node while <c>[node+0x45] &amp; 0x10 == 0</c> and a parent exists; returns
    /// <c>[found+0x59] &amp; 0xF</c> and writes <c>([found+0x58] &gt;&gt; 3) &amp; 7</c>. The low nibble of <c>+0x59</c> is stored by node <c>vt+0x130</c> = <c>0x980EF4</c>
    /// (<c>bfi r3,r1,#0,#4</c>) from advanced-settings byte 3 (<c>0x9ED7E4..0x9ED7F4</c>); <c>+0x58</c> bits 3..5 are byte 1 &amp; 7 (B8).
    /// </summary>
    public int BehaviourCode9EEDA4(WwiseNode node, out int outIndex)
    {
        var n = node;
        while (!Facts(n).Bit45_4 && ParentOf(n) is { } parent) n = parent;
        outIndex = n.Params.AdvancedByte1 & 7;
        return n.Params.AdvancedByte3 & 0xF;
    }

    // ------------------------------------------------------------------ P2a: 0x9F6B94

    /// <summary>
    /// <c>0x9F6B94(out, node, gameObj)</c> (P2a): the priority and its distance offset. If a parent exists and <c>[node+0x40] &amp; 1 == 0</c> the
    /// parent answers. Otherwise the priority is property 7 (default 50.0f) and the offset is 0.0f when <c>[node+0x45] &amp; 0x80 == 0</c>, else
    /// property 8 (default -10.0f). <c>[node+0x40]</c> bit 0 is NodeBase bits b0 and <c>[node+0x45]</c> bit 7 is b1 (row 2.3).
    /// </summary>
    public void Priority9F6B94(WwiseNode node, out float priority, out float offset)
    {
        if (ParentOf(node) is { } parent && (node.Params.Bits & 1) == 0)
        {
            Priority9F6B94(parent, out priority, out offset);
            return;
        }
        if (Facts(node).Rtpc17)
            throw new WwiseMissingBehaviourException("M6-026 P2a: node RTPC parameter 0x11 (priority) goes through 0xA11590 -> 0xA17724/0xA17878, a RECOVERABLE_GAP; no shipped node has one");
        priority = node.Params.Float(WwiseProp.Priority) ?? BitConverter.Int32BitsToSingle(DefaultPriorityBits);
        offset = (node.Params.Bits & 2) == 0
            ? 0f
            : node.Params.Float(WwiseProp.PriorityDistanceOffset) ?? BitConverter.Int32BitsToSingle(DefaultPriorityOffsetBits);
    }

    // ------------------------------------------------------------------ key (L3)

    /// <summary>
    /// <c>[node+0x46]</c> bit 2 (5.1, F5): "the node's category is a bus", <c>((cat - 0xA) &amp; ~2 == 0) | (cat == 0)</c> over <c>vt+0x44()</c>. The categories are Sound 3, RanSeq 2, Switch 4,
    /// ActorMixer 1, Layer 5 (bit 0) and Bus 0, Bus2 0xC (bit 1). The music classes' categories are not in the inventory.
    /// </summary>
    public static bool Node46Bit2(WwiseNode node) => node switch
    {
        WwiseBusNode => true,
        WwiseSoundNode or WwiseRandomSequenceNode or WwiseSwitchNode or WwiseActorMixerNode or WwiseBlendNode => false,
        _ => throw new WwiseMissingBehaviourException(
            $"M6-026 5.1: the vt+0x44 category of a {node.Type} node is not in the inventory (only the Sound path and the buses are)"),
    };

    private (uint Lo, uint Hi) KeyFor(WwiseNode node)
    {
        int ancestors = 0;
        for (var p = ParentOf(node); p is not null; p = ParentOf(p)) ancestors++;
        uint hi = (uint)ancestors | (Node46Bit2(node) ? 0x20000000u : 0x40000000u);
        return (node.Id, hi);
    }

    // ------------------------------------------------------------------ L1..L6: 0x9F29E8

    /// <summary>
    /// <c>0x9F29E8(node)</c> (L1..L6): creates the node's limiter object, stores <c>[node+0x30]</c>, then runs <c>node vt+0x11C</c> = <c>0x9F2D20</c>
    /// (<see cref="CreateChain9F2D20"/>) and returns its result. A failed allocation stores <c>[node+0x30] = 0</c> and returns 0.
    /// </summary>
    private int CreateLimiter(WwiseNode node)
    {
        if (!Alloc()) { _limiters.Remove(node.Id); return 0; }                        // L1
        var f = Facts(node);
        if (f.Rtpc16 && f.Max != 0) throw RtpcMissing();                             // L4 gate
        var lim = new WwiseNodeLimiter { NodeId = node.Id, Global68 = f.GlobalFlag };  // +0x68 bit0 = node+0x45 bit6
        lim.List.Max = f.Max;                                                         // +0x44
        lim.List.KillNewest = f.KillNewest;                                           // +0x46 = node+0x45 bit2
        lim.List.Virtual = f.Virtual;                                                 // +0x47 = node+0x45 bit3
        if (f.Max != 0)                                                               // L5: 0x9F2AF8 cmp r3,#0; bne 0x9F2CCC
        {
            (RtpcSubscribeA19ECC ?? throw new WwiseMissingBehaviourException(
                "M6-026 L5: 0xA19ECC -> 0x9F7390..0x9F82EC (the RTPC-change subscription walk) is unread; supply RtpcSubscribeA19ECC"))("limiter", node, 0x10000UL);
            lim.SubscriberNode20 ??= node.Id;                                         // 0xA19ECC: if [this+0x20]==0, [this+0x20] = node
        }
        _limiters[node.Id] = lim;                                                     // [node+0x30] = obj
        var (lo, hi) = KeyFor(node);                                                  // L3
        lim.List.KeyLo = lo;
        lim.List.KeyHi = hi;
        return CreateChain9F2D20(node);                                               // 0x9F2BA8..0x9F2BBC: vt+0x11C
    }

    /// <summary>
    /// <c>node vt+0x11C</c> = <c>0x9F2D20</c> (L6): creates the limiter of the output bus and of the parent node when they have none. Returns 0
    /// only if one of those creations failed.
    /// </summary>
    private int CreateChain9F2D20(WwiseNode node)
    {
        int r4 = 1;
        if (OutputBusOf(node) is { } bus && LimiterOf(bus.Id) is null) r4 = CreateLimiter(bus);
        if (ParentOf(node) is { } parent && LimiterOf(parent.Id) is null)
            if (CreateLimiter(parent) == 0) r4 = 0;
        return r4;
    }

    private void EnsureLimiter(WwiseNode node)
    {
        if (LimiterOf(node.Id) is null) CreateLimiter(node);
    }

    /// <summary>
    /// <c>0x9FAC54(node, isBus)</c> (W6): creates the limiter if missing, then <c>(u16)+0x60++</c> and, when <paramref name="isBus"/>, <c>+0x62++</c>.
    /// Returns 2 if the limiter still does not exist, or if it was just created, <c>0x9F29E8</c> returned 0 and <paramref name="isBus"/> is false; else 1.
    /// </summary>
    public int Count9FAC54(WwiseNode node, bool isBus)
    {
        bool created = false;
        int createResult = 1;
        if (LimiterOf(node.Id) is null) { createResult = CreateLimiter(node); created = true; }
        if (LimiterOf(node.Id) is not { } l) return 2;
        unchecked
        {
            l.Count60++;
            if (isBus) l.Count62++;
        }
        if (created && createResult == 0 && !isBus) return 2;
        return 1;
    }

    // ------------------------------------------------------------------ W2..W7: the walk

    /// <summary>
    /// <c>node vt+0x90</c> (W1): <c>0x9ED2CC</c> for a Sound, RanSeq, Switch, ActorMixer or Layer, <c>0x9C4F30</c> for a bus. <paramref name="count"/> is r2
    /// (the <c>0x9BEB30</c> result, 1 at the call site) and <paramref name="skipGlobal"/> is r3 (0 at the call site). Returns 1, 2 or 0x50 (W5).
    /// </summary>
    public int Walk(WwiseNode node, WwiseLimitBlock block, bool count, bool skipGlobal)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(block);
        return node is WwiseBusNode bus ? WalkBus(bus, block, count, skipGlobal) : WalkNode(node, block, count, skipGlobal);
    }

    /// <summary>The W4 merge: a child result of 1 keeps <paramref name="r5"/>, 0x50 turns 1 into 0x50, anything else replaces it.</summary>
    public static int Merge(int r5, int x)
        => x == 1 ? r5 : x == 0x50 ? (r5 == 1 ? 0x50 : r5) : x;                      // W4

    private int WalkNode(WwiseNode node, WwiseLimitBlock block, bool r2, bool r3)
    {
        int r5 = 1;
        bool check = true;
        if (r2)
        {
            r5 = Count9FAC54(node, (block.Word0C & 1) != 0);                          // W2: 0x9ED2E0 bne 0x9ED3CC
            if (r5 != 1) check = false;                                               // the check is skipped, r5 = 2 carried
        }
        if (check)
        {
            if (block.B10 != 0) r5 = 1;                                               // W3: no check, block+0x10 unchanged
            else
            {
                var l = LimiterOf(node.Id);
                if (l is null || l.Global68) r5 = r3 ? 1 : GlobalCheck9FA01C(node, block, r2);   // W3
                else r5 = PerObjectCheck9FA6F8(node, block, r2);                      // W3 (no r3 test)
                block.B10 = (byte)(Facts(node).StopChecks ? 1 : 0);                   // block+0x10 = [node+0x47] bit6
            }
        }
        byte r6 = block.B10;                                                          // this node's value

        if ((block.Word0C & 1) != 0 && OutputBusOf(node) is { } bus)                  // W4
        {
            block.Word0C = (ushort)(block.Word0C & ~1);
            block.B10 = 0;
            r5 = Merge(r5, WalkBus(bus, block, r2, r3));
        }
        if (ParentOf(node) is { } parent)
        {
            block.B10 = r6;
            r5 = Merge(r5, WalkNode(parent, block, r2, r3));
        }
        return r5;
    }

    private int WalkBus(WwiseBusNode bus, WwiseLimitBlock block, bool r2, bool r3)
    {
        int r5 = r2 ? Count9FAC54(bus, true) : 1;                                     // W7
        if (!r3 && block.B10 == 0)
        {
            GlobalCheck9FA01C(bus, block, r2);                                        // 0x9C4FDC: the result is ignored
            block.B10 = (byte)(Facts(bus).StopChecks ? 1 : 0);
        }
        if (OutputBusOf(bus) is { } parentBus)
            r5 = Merge(r5, WalkBus(parentBus, block, r2, r3));
        if (LimiterOf(bus.Id) is { } l && l.Count60 == 1)
        {
            SetBusCC(bus.Id, (byte)((BusCC(bus.Id) & ~7) | 1));                       // [bus+0xCC] bits 0..2 = 1
            if (bus.Ducks.Count != 0)                                                 // W8: an empty list returns at once (0x9C4CE0)
                (DuckBodyA9C4CD0 ?? throw new WwiseMissingBehaviourException(
                    "M6-026 W8: 0x9C4CD0 on a bus with duck entries runs 0x9C4D04..0x9C4F0C (RECOVERABLE_GAP); supply DuckBodyA9C4CD0"))(bus);
        }
        return r5;
    }

    private void AppendList(WwiseLimiterArray? array, WwisePbiList list)
    {
        if (array is null) throw new WwiseMissingBehaviourException("M6-026 G2/O5: the block's array pointer is 0");
        if (array.Count >= array.Capacity)                                            // G2: grow to cap+3
        {
            if (!Alloc()) return;                                                     // an allocation failure skips the append
            array.Capacity += 3;
        }
        array.Items.Add(list);
    }

    // ------------------------------------------------------------------ G1..G3: 0x9FA01C

    /// <summary>
    /// <c>0x9FA01C(node, block, r2)</c> (G1..G3): the global variant. Returns 1 or the result of <c>0xA37100</c>.
    /// </summary>
    private int GlobalCheck9FA01C(WwiseNode node, WwiseLimitBlock block, bool r2)
    {
        var f = Facts(node);
        int max = f.Max;
        if (f.Rtpc16)
        {
            if (max == 0) { EnsureLimiter(node); return 1; }                          // G1
            throw RtpcMissing();
        }
        EnsureLimiter(node);                                                          // G1: the result is unchecked
        if (max == 0) return 1;
        var l = LimiterOf(node.Id) ?? throw new WwiseMissingBehaviourException(
            "M6-026 G2: with the limiter creation failed the engine appends [node+0x30]+0x38 = 0x38; the inventory does not settle that");
        AppendList(block.Array, l.List);                                              // G2
        if (r2 && block.B11 != 0 && block.Count0E == 0 && max <= l.List.Count20 - l.List.Virtual22)   // G3
        {
            int r = Victim0A37100(l.List, max, block.Priority, null, f.KillNewest, f.Virtual, 1);
            block.Count0E++;
            return r;
        }
        return 1;
    }

    // ------------------------------------------------------------------ O1..O6: 0x9FA6F8, 0x9FA410, 0x9FAA70

    private int PerObjectCheck9FA6F8(WwiseNode node, WwiseLimitBlock block, bool r2)
    {
        var f = Facts(node);
        int max = f.Max;
        if (f.Rtpc16 && max != 0) throw RtpcMissing();                                // O1
        uint? go = block.GameObject;
        var limiter = LimiterOf(node.Id)!;
        var slot = limiter.Map.FirstOrDefault(e => e.Go == go);
        int r8;
        if (slot is null)                                                             // O2: not found
        {
            int k = max;
            bool keep = k != 0;
            (r8, var created) = CreateObjectEntry9FA410(node, limiter, go, (ushort)k);
            if (created is { Entry: { } entry } && keep) AppendList(block.Array, entry.List);
            return r8;
        }
        var e = slot.Entry;                                                           // O4: found
        if (r2)
        {
            if (e is null) return 1;
            if (block.B11 == 0)
            {
                r8 = 1;
                if (e.List.Max != 0) AppendList(block.Array, e.List);
                return r8;
            }
            if (e.List.Max == 0) return 1;
            int n = e.List.Count20 - e.List.Virtual22 - block.Count0E;
            if (n < e.List.Max) { r8 = 1; }
            else
            {
                r8 = Victim0A37100(e.List, e.List.Max, block.Priority, go, f.KillNewest, f.Virtual, 1);
                block.Count0E++;
            }
            AppendList(block.Array, e.List);
            return r8;
        }
        // 10.1: the count flag clear uses the LOCAL max (r8), not the entry's; the entry pointer 0 would append the value 0 (10.3), which no list can be.
        r8 = 1;
        if (max != 0)
        {
            if (e is null) throw new WwiseMissingBehaviourException("M6-026 10.3: a zero entry pointer with a nonzero local max appends a null list pointer to the array");
            AppendList(block.Array, e.List);
        }
        return r8;
    }

    /// <summary><c>0x9FA410(node, go, &amp;slot, max)</c> (O3): returns its result (1, or 2 on an allocation failure) and the map slot (null on failure).</summary>
    private (int Result, WwiseLimiterMapEntry? Slot) CreateObjectEntry9FA410(WwiseNode node, WwiseNodeLimiter limiter, uint? go, ushort max)
    {
        if (!Alloc()) return (2, null);
        var f = Facts(node);
        var entry = new WwiseObjectLimiter { Go = go };
        entry.List.Max = max;                                                         // +0x34
        entry.List.KillNewest = f.KillNewest;                                         // +0x36
        entry.List.Virtual = f.Virtual;                                               // +0x37
        if (max != 0)
        {
            (RtpcSubscribeA19ECC ?? throw new WwiseMissingBehaviourException(
                "M6-026 O3: 0xA19ECC -> 0x9F7390..0x9F82EC is unread; supply RtpcSubscribeA19ECC"))("object", node, 0x10000UL);
            entry.SubscriberNode20 ??= node.Id;
        }
        var (lo, hi) = KeyFor(node);                                                  // +0x40/+0x44 as L3
        entry.List.KeyLo = lo;
        entry.List.KeyHi = hi;
        var existing = limiter.Map.FirstOrDefault(e => e.Go == go);
        if (existing is not null) { existing.Entry = entry; return (1, existing); }   // an existing key's pointer is replaced
        if (limiter.Map.Count >= limiter.MapCapacity)
        {
            if (!Alloc()) return (2, null);                                           // the entry is destroyed, *out = 0
            limiter.MapCapacity += 1;
        }
        var added = new WwiseLimiterMapEntry { Go = go, Entry = entry };
        limiter.Map.Add(added);
        return (1, added);
    }

    // ------------------------------------------------------------------ 1.1..1.9: the removal walk (C29.1)

    /// <summary>
    /// <c>node vt+0x94</c> = <c>0x9ED428(node, block)</c> (1.1, 1.2): (a) <c>0x9FACD0(node, block+0xC &amp; 1)</c>; (b) the limiter is re-read (a can destroy it); (c) a limiter with
    /// <c>+0x68</c> bit 0 set gets <c>0x9FA2EC</c>, otherwise <c>0x9FAA70(node, [block+4])</c>; (d) <c>r6 = block+0x10</c>; (e) with <c>block+0xC</c> bit 0 set and an output bus: clear the
    /// bit, <c>block+0x10 = 0</c>, <c>bus vt+0x94</c>; (f) with a parent: <c>block+0x10 = r6</c> and <c>parent vt+0x94</c>. Only <c>block+0xC</c> bit 0, <c>+4</c> and <c>+0x10</c> are read.
    /// </summary>
    public void RemoveNode9ED428(WwiseNode node, WwiseLimitBlock block)
    {
        if (node is WwiseBusNode bus0) { RemoveBus9C5240(bus0, block); return; }
        DecrementCount9FACD0(node, (block.Word0C & 1) != 0);                          // (a)
        if (LimiterOf(node.Id) is { } l)                                              // (b)
        {
            if (l.Global68) DestroyIfIdle(node);                                      // (c) 0x9FA2EC
            else RemovePerObject9FAA70(node, block.GameObject);                       //     0x9FAA70
        }
        byte r6 = block.B10;                                                          // (d)
        if ((block.Word0C & 1) != 0 && OutputBusOf(node) is { } bus)                  // (e)
        {
            block.Word0C = (ushort)(block.Word0C & ~1);
            block.B10 = 0;
            RemoveBus9C5240(bus, block);
        }
        if (ParentOf(node) is { } parent)                                             // (f)
        {
            block.B10 = r6;
            RemoveNode9ED428(parent, block);
        }
    }

    /// <summary>
    /// <c>0x9C5240(bus, block)</c> (1.3): <c>0x9FACD0(bus, 1)</c>, <c>0x9FA2EC(bus)</c>, the parent bus, then when the bus limiter is gone or its <c>+0x60</c> is 0: <c>[bus+0xCC]</c> bits 0..2 = 2 when
    /// <c>[bus+0x64] != 0 &amp;&amp; [bus+0x84] != 0 &amp;&amp; 0x9C5154 == 1</c>, else 0; then <c>0x9C4CD0</c>.
    /// </summary>
    public void RemoveBus9C5240(WwiseBusNode bus, WwiseLimitBlock block)
    {
        DecrementCount9FACD0(bus, true);
        DestroyIfIdle(bus);                                                           // 0x9FA2EC
        if (OutputBusOf(bus) is { } parent) RemoveBus9C5240(parent, block);
        var l = LimiterOf(bus.Id);
        if (l is not null && l.Count60 != 0) return;
        byte value = 0;
        if (bus.RecoveryMs != 0)                                                      // [bus+0x64] != 0 (D6.1: the recovery time in samples)
            value = (byte)((BusRecoveryGateA9C5154 ?? throw new WwiseMissingBehaviourException(
                "M6-026 1.3: [bus+0x84] and 0x9C5154 (RECOVERABLE_GAP) gate a bus with a recovery time; supply BusRecoveryGateA9C5154"))(bus) ? 2 : 0);
        SetBusCC(bus.Id, (byte)((BusCC(bus.Id) & ~7) | value));
        if (bus.Ducks.Count != 0)
            (DuckBodyA9C4CD0 ?? throw new WwiseMissingBehaviourException(
                "M6-026 W8: 0x9C4CD0 on a bus with duck entries runs 0x9C4D04..0x9C4F0C (RECOVERABLE_GAP); supply DuckBodyA9C4CD0"))(bus);
    }

    /// <summary>
    /// <c>0x9FACD0(node, isBus)</c> (1.4): no limiter returns; <c>(u16)+0x60--</c> (wrapping), <c>+0x62--</c> when <paramref name="isBus"/>; then the idle test and <c>0x9F4D40</c>.
    /// </summary>
    public void DecrementCount9FACD0(WwiseNode node, bool isBus)
    {
        if (LimiterOf(node.Id) is not { } l) return;
        unchecked
        {
            l.Count60--;
            if (isBus) l.Count62--;
        }
        DestroyIfIdle(node);
    }

    /// <summary>
    /// <c>0x9FAA70(node, go)</c> (1.7, F1): a game object not in the map returns with no idle test. An entry with a nonzero <c>+0x48</c> or <c>+0x4A</c> only gets the idle test. Otherwise the entry (or a zero
    /// entry pointer) is freed, the map slot removed and the count stored; when the node still has a limiter the idle test runs.
    /// </summary>
    public void RemovePerObject9FAA70(WwiseNode node, uint? go)
    {
        if (LimiterOf(node.Id) is not { } l) return;
        int i = l.Map.FindIndex(e => e.Go == go);
        if (i < 0) return;
        if (l.Map[i].Entry is { } e && (e.List.Count20 != 0 || e.List.Virtual22 != 0)) { DestroyIfIdle(node); return; }
        l.Map.RemoveAt(i);                                                            // free, slot pointer 0, memmove, count--
        DestroyIfIdle(node);
    }

    // ------------------------------------------------------------------ L7, 1.5, 1.8: the idle predicate and 0x9F4D40

    /// <summary>The idle-limiter predicate (L7, 1.4): <c>(s16)+0x60 &lt;= 0</c>, <c>(s16)+0x64 &lt;= 0</c>, <c>+0x58 == 0</c>, <c>+0x5A == 0</c>, <c>[+0xC] == 0</c>, <c>[+4] == 0</c>.</summary>
    public static bool IsIdle(WwiseNodeLimiter l)
        => l.Count60 <= 0 && l.Count64 <= 0 && l.List.Count20 == 0 && l.List.Virtual22 == 0 && l.Contexts0C.Count == 0 && l.Map.Count == 0;

    /// <summary><c>0x9FA2EC(node)</c> (1.5): the idle test, then <c>0x9F4D40</c> for an idle limiter.</summary>
    public void DestroyIfIdle(WwiseNode node)
    {
        if (LimiterOf(node.Id) is { } l && IsIdle(l)) DestroyLimiter9F4D40(node);
    }

    /// <summary>
    /// <c>0x9F4D40(node)</c> (1.8, F2): (1) <c>0x9F4F28</c>: the parent then the bus, each with a limiter that is idle, are destroyed the same way; (2) <c>0xA19F60</c> unsubscribe; (3..6) the lists are freed and the
    /// object returned to its block, with <c>[+0xC] = 0</c> stored unconditionally; (7) <c>[node+0x30] = 0</c>.
    /// </summary>
    public void DestroyLimiter9F4D40(WwiseNode node)
    {
        if (LimiterOf(node.Id) is not { } l) return;
        if (ParentOf(node) is { } parent && LimiterOf(parent.Id) is { } pl && IsIdle(pl)) DestroyLimiter9F4D40(parent);   // 0x9F4F28: parent, by bl
        if (OutputBusOf(node) is { } bus && LimiterOf(bus.Id) is { } bl && IsIdle(bl)) DestroyLimiter9F4D40(bus);          //           bus, by tail call
        (RtpcUnsubscribeA19F60 ?? throw new WwiseMissingBehaviourException(
            "M6-026 1.8: 0xA19F60 (the RTPC unsubscribe) is a RECOVERABLE_GAP; supply RtpcUnsubscribeA19F60"))(l);
        l.Contexts0C.Clear();                                                         // [L+0xC] = 0, 0x9F4DB0
        _limiters.Remove(node.Id);                                                    // [node+0x30] = 0
    }

    // ------------------------------------------------------------------ V1..V3: 0xA37100

    /// <summary>
    /// <c>0xA37100(list, max, prio, go, killNewest, virtual, &amp;out, reason)</c> (V1..V3). Returns 1, 2 or 0x50; on a kill calls
    /// <see cref="Kill0A01CA4"/> with <paramref name="reason"/>. The out-parameter (<c>[cand+0xE0]</c>) is never read by the callers (G3) and is not returned.
    /// </summary>
    public int Victim0A37100(WwisePbiList? list, int max, float prio, uint? go, bool killNewest, bool virtualFlag, int reason)
    {
        if (list is null) return 1;                                                   // V1
        float s17 = VictimInitialPriority;
        int count = 0;
        WwisePlayingInstance? cand = null;
        int code = 2;
        bool ip = false;
        foreach (var pbi in list.Items)
        {
            if ((pbi.Flags1BD & 2) != 0 || (pbi.Flags1BE & 0x2C) != 0) continue;
            if (go is not null && pbi.GameObject14 != go) continue;                 // [pbi+0x14] != go (5.2, 5.3: 0 for a global-scope action)
            count = (ushort)(count + 1);
            if (!virtualFlag)
            {
                if (prio >= pbi.Priority1C0) { s17 = pbi.Priority1C0; cand = pbi; }   // vcmp/vmovge: NaN is false
            }
            else if (prio >= pbi.Priority1C0)                                         // V2
            {
                int c = (NextSourceCodeA01768 ?? throw new WwiseMissingBehaviourException(
                    "M6-026 V2: 0xA01768 needs NextSourceCodeA01768 (set when a bridge holds this limiter)"))(pbi);
                if (c == 0) { if (count <= max) ip = true; }
                else { s17 = pbi.Priority1C0; cand = pbi; code = c; }
            }
        }
        if (max > count) return 1;                                                    // V3
        if (prio < s17) return ip ? 1 : (virtualFlag ? 0x50 : 2);
        if (prio == s17 && killNewest) return ip ? 1 : (virtualFlag ? 0x50 : 2);
        if (cand is null) return ip ? 1 : (virtualFlag ? 0x50 : 2);
        if (virtualFlag && code != 1) return 1;
        Kill0A01CA4(cand, reason);
        return 1;
    }

    // ------------------------------------------------------------------ K1..K6: 0xA01CA4 and 0x9FF7B8

    /// <summary>
    /// <c>0xA01CA4(pbi, reason)</c> (K1..K5, C28.2). Posts no message: it marks <c>pbi+0x1BD</c>, then paused or a stop item at <c>+0x148</c> gives
    /// <c>vt+0</c>(pbi,0,0); <c>1BA &amp; 0x78 == 0</c> gives <c>vt+0</c>(pbi,0,1); a running transition at <c>+0x144</c> is re-aimed to id
    /// <c>0x02000000</c>, end 0.0f, time 0, curve 4, mode 0; with none, <c>+0x168 = 0.0f</c>, <c>+0x40 = 0.0f</c> and <c>vt+0</c>(pbi,0,0).
    /// </summary>
    public void Kill0A01CA4(WwisePlayingInstance pbi, int reason)
    {
        if ((pbi.Flags1BD & 2) == 0)                                                  // K1
            pbi.Flags1BD = (byte)((pbi.Flags1BD & ~0x1C) | ((reason & 7) << 2) | 2);
        if ((pbi.Flags1BC & 0x80) != 0) { MarkStopped9FF7B8(pbi, 0, 0); return; }     // K2: paused
        if (pbi.Item148Id is { } t && IsStopOrPauseItem0A35980(t)) { MarkStopped9FF7B8(pbi, 0, 0); return; }
        pbi.Flags1BC |= 0x40;                                                         // K3
        if ((pbi.Flags1BA & 0x78) == 0) { MarkStopped9FF7B8(pbi, 0, 1); return; }
        if (pbi.Field144 != 0)                                                        // K4
        {
            (ReaimTransitionA366F4 ?? throw new WwiseMissingBehaviourException(
                "M6-026 K4: 0xA366F4 (the transition re-aim) is a required seam; supply ReaimTransitionA366F4"))(pbi, 0x02000000u, 0f, 0, 4, 0);
            return;
        }
        pbi.Fade168 = 0f;                                                             // K5
        pbi.MuteFade40 = 0f;
        MarkStopped9FF7B8(pbi, 0, 0);
    }

    /// <summary><c>0xA35980(item)</c> (K2): the item id is <c>0x02000000</c> or <c>0x04000000</c> (<c>(id-0x02000000) &amp; ~0x02000000 == 0</c>).</summary>
    public static bool IsStopOrPauseItem0A35980(uint id) => unchecked((id - 0x02000000u) & ~0x02000000u) == 0;

    /// <summary>
    /// <c>pbi vt+0</c> = <c>0x9FF7B8(pbi, r1, r2)</c> (K6): only marks the PBI stopped. It does not call Term, does not notify and does not touch the limiter lists.
    /// <paramref name="r2"/> is unused.
    /// </summary>
    public void MarkStopped9FF7B8(WwisePlayingInstance pbi, int r1, int r2)
    {
        if ((pbi.Flags1BC & 0x20) != 0) return;
        pbi.Flags1BC |= 0x20;
        if ((r1 & ~2) != 0) return;
        if (pbi.Field144 != 0)
        {
            (CancelTransitionA36618 ?? throw new WwiseMissingBehaviourException(
                "M6-026 K6: 0xA36618 (transition cancel) is a required seam; supply CancelTransitionA36618"))(pbi, 0x144);
            pbi.Field144 = 0;
        }
        if (pbi.Item148Id is not null)
        {
            (CancelTransitionA36618 ?? throw new WwiseMissingBehaviourException(
                "M6-026 K6: 0xA36618 (transition cancel) is a required seam; supply CancelTransitionA36618"))(pbi, 0x148);
            pbi.Item148Id = null;
        }
        if (pbi.Field0AC is not null)
            throw new WwiseMissingBehaviourException("M6-026 K6: the 3D path (0x9FEEB8, 0x9FF87C) is unread and not on shipped data");
        pbi.Flags1BC = (byte)((pbi.Flags1BC & ~2) | 8);
    }

    // ------------------------------------------------------------------ 8.1..8.5: 0x9F3274 and the global registration

    /// <summary>
    /// The insert comparator of <c>0x9F3274</c> (8.1..8.3, 4.1..4.3) for a search key against the element at mid: -1 goes to the lower index (<c>hi = mid-1</c>), +1 to the higher (<c>lo = mid+1</c>), 0 stops
    /// at mid (equal keys, and an unordered priority: O9). Descending priority; equal priority is broken by <c>1C8</c> then <c>1C4</c>, descending when <c>[list+0xE] == 0</c>, ascending otherwise.
    /// </summary>
    private static int Direction(bool killNewest, float prio, uint k8, uint k4, WwisePlayingInstance m)
    {
        if (prio > m.Priority1C0) return -1;                                          // vcmpe, bgt
        if (prio < m.Priority1C0) return 1;
        if (prio != m.Priority1C0) return 0;                                          // unordered: at mid
        int c = k8 != m.ChainId ? (k8 > m.ChainId ? 1 : -1) : k4 != m.Key1C4 ? (k4 > m.Key1C4 ? 1 : -1) : 0;
        if (c == 0) return 0;                                                         // 4.1/4.2: equal keys insert at mid
        return killNewest ? c : -c;                                                   // flag 0: a larger key goes first; flag != 0: a smaller one
    }

    private static int SearchPosition(WwisePbiList list, float prio, uint k8, uint k4, IReadOnlyList<WwisePlayingInstance> items, out bool hit)
    {
        int lo = 0, hi = items.Count - 1;
        hit = false;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            int dir = Direction(list.KillNewest, prio, k8, k4, items[mid]);
            if (dir == 0) { hit = true; return mid; }
            if (dir < 0) hi = mid - 1; else lo = mid + 1;
        }
        return lo;
    }

    /// <summary>
    /// <c>0x9F3274(list, pbi)</c> (8.1..8.4, 4.1..4.3): binary search on priority, descending; equal priority is broken by <c>pbi+0x1C8</c> then <c>pbi+0x1C4</c> (unsigned),
    /// descending when <c>[list+0xE]</c> is 0 and ascending otherwise; equal keys insert at mid. Inserts (growing by 8), registers an empty list in the global limiter array (8.5) and
    /// increments <c>+0x20</c>. Returns 1, or 2 when the allocation fails.
    /// </summary>
    public int InsertIntoList9F3274(WwisePbiList list, WwisePlayingInstance pbi)
    {
        int pos = SearchPosition(list, pbi.Priority1C0, pbi.ChainId, pbi.Key1C4, list.Items, out _);
        bool wasEmpty = list.Items.Count == 0;
        if (list.Items.Count >= list.Capacity)
        {
            if (!Alloc()) return 2;                                                   // 0x9F33F0
            list.Capacity += 8;                                                       // 8.4: (cap+8)*4 bytes
        }
        list.Items.Insert(pos, pbi);
        if (wasEmpty) RegisterList0A39254(list);                                      // 0x9F3384
        unchecked { list.Count20++; }                                                 // 0x9F338C..0x9F339C
        return 1;
    }

    /// <summary>
    /// <c>0x9F3528(list, pbi)</c> (2.1..2.3): finds the entry by the three keys (the insert comparator with the PBI's current <c>1C0</c>, <c>1C8</c>, <c>1C4</c>) and removes it; <c>[list+0x20]</c>
    /// is decremented only when found, and a found removal that leaves the list empty calls <c>0xA394C0</c> first. Not found: nothing changes, except that a list that was already empty is
    /// unregistered.
    /// </summary>
    public void Remove9F3528(WwisePbiList list, WwisePlayingInstance pbi)
    {
        int original = list.Items.Count;
        int pos = SearchPosition(list, pbi.Priority1C0, pbi.ChainId, pbi.Key1C4, list.Items, out bool hit);
        if (!hit)
        {
            if (original == 0) UnregisterList0A394C0(list);                           // 0x9F3648..0x9F365C
            return;
        }
        list.Items.RemoveAt(pos);
        if (list.Items.Count == 0) UnregisterList0A394C0(list);                       // 0x9F3690: before the decrement
        unchecked { list.Count20--; }
    }

    /// <summary>
    /// <c>0x9F36A0(list, newPrio, pbi)</c> (8.1): finds the entry with its old <c>1C0</c> and its keys (not found: nothing changes), removes it and inserts it at the sorted position of
    /// <c>(newPrio, 1C8, 1C4)</c>. The count, <c>[list+0x20]</c> and <c>pbi+0x1C0</c> are unchanged.
    /// </summary>
    public void Reposition9F36A0(WwisePbiList list, float newPrio, WwisePlayingInstance pbi)
    {
        int idx = SearchPosition(list, pbi.Priority1C0, pbi.ChainId, pbi.Key1C4, list.Items, out bool hit);
        if (!hit) return;
        list.Items.RemoveAt(idx);
        int pos = SearchPosition(list, newPrio, pbi.ChainId, pbi.Key1C4, list.Items, out _);
        list.Items.Insert(pos, pbi);
    }

    /// <summary>
    /// SetPriority <c>0xA01A08(pbi, new)</c> (8.3): an equal priority returns (an unordered compare is not equal); otherwise <c>0x9F36A0</c> for every list of the array (the count is re-read each step),
    /// then <c>[pbi+0x1C0] = new</c>.
    /// </summary>
    public void SetPriorityA01A08(WwisePlayingInstance pbi, float newPrio)
    {
        if (newPrio == pbi.Priority1C0) return;
        for (int i = 0; i < pbi.LimiterArray1EC.Count; i++) Reposition9F36A0(pbi.LimiterArray1EC.Items[i], newPrio, pbi);
        pbi.Priority1C0 = newPrio;
    }

    /// <summary>
    /// The priority block of CalcEffectiveParams (<c>0x9FFE1C..0x9FFFE4</c>, 8.4): <c>out = 0x9F6B94(..)</c>; when both values equal <c>[pbi+0x1CC]</c>/<c>[pbi+0x1D0]</c> nothing happens; otherwise they are
    /// stored and, when the priority differs from <c>[pbi+0x1C0]</c> (or is unordered), the SetPriority loop runs with it and <c>[pbi+0x1C0]</c> is set. The compare of <c>1C0</c> here has no equal
    /// early return other than the <c>bne</c>.
    /// </summary>
    public void PriorityRefresh9FFE1C(WwisePlayingInstance pbi)
    {
        var node = pbi.NodeE0 ?? throw new WwiseMissingBehaviourException("M6-026 8.4: the PBI's node [pbi+0xE0] is not set");
        Priority9F6B94(node, out float prio, out float offset);
        if (prio == pbi.Field1CC && offset == pbi.Field1D0) return;                   // 0x9FFE98..0x9FFEAC
        pbi.Field1CC = prio;
        pbi.Field1D0 = offset;
        if (prio == pbi.Priority1C0) return;                                          // 0x9FFE40..0x9FFE4C bne 0x9FFFAC
        for (int i = 0; i < pbi.LimiterArray1EC.Count; i++) Reposition9F36A0(pbi.LimiterArray1EC.Items[i], prio, pbi);
        pbi.Priority1C0 = prio;
    }

    /// <summary>
    /// <c>0xA39254(list)</c> (8.5): registers the list in the global limiter array, sorted descending by the 64-bit key (compare high then low, unsigned; equal
    /// inserts at mid), growing by 1. An allocation failure leaves it unregistered.
    /// </summary>
    public void RegisterList0A39254(WwisePbiList list)
    {
        if (GlobalLimiterArray.Count >= GlobalLimiterArrayCapacity)
        {
            if (!Alloc()) return;
            GlobalLimiterArrayCapacity += 1;
        }
        ulong key = ((ulong)list.KeyHi << 32) | list.KeyLo;
        int lo = 0, hi = GlobalLimiterArray.Count - 1, pos = -1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            var m = GlobalLimiterArray[mid];
            ulong mk = ((ulong)m.KeyHi << 32) | m.KeyLo;
            if (key == mk) { pos = mid; break; }
            if (key > mk) hi = mid - 1; else lo = mid + 1;
        }
        if (pos < 0) pos = lo;
        GlobalLimiterArray.Insert(pos, list);
    }

    /// <summary><c>0xA394C0(list)</c> (8.5): removes the list from the global limiter array (linear search, memmove, count--). Its caller is not in the inventory.</summary>
    public void UnregisterList0A394C0(WwisePbiList list)
    {
        int i = GlobalLimiterArray.IndexOf(list);
        if (i >= 0) GlobalLimiterArray.RemoveAt(i);
    }

    // ------------------------------------------------------------------ R1: 0xA0285C (the limiter part) and the global PBI list

    /// <summary>
    /// <c>pbi vt+0xC</c> = <c>0xA0285C</c> (6.1, O4): <c>0x9BC5A8</c> pushes the PBI on the node limiter's context list (when the node has a limiter) and subscribes to RTPC changes; then, if
    /// <c>pbi+0xE9</c> bit 2 is clear, the global voice list is appended to the PBI's array and <c>0x9F3274</c> inserts the PBI into every array entry (result ignored); <c>[pbi+0x140] == 0</c> returns 2;
    /// otherwise <c>0xA04D48</c> (which returns 1 for a non-null PBI, 6.2) and <c>0xA1EC54</c>, and the function returns 1.
    /// </summary>
    public int InsertPbiA0285C(WwiseNode node, WwisePlayingInstance pbi)
    {
        if (LimiterOf(node.Id) is { } l)                                              // 0x9BC5A8 beq 0x9BC5D8: only the push is conditional (0x9BC5C0..0x9BC5D4)
            l.Contexts0C.Insert(0, pbi);                                              // ctx+0x24 = old head
        (RtpcSubscribeA19ECC ?? throw new WwiseMissingBehaviourException(
            "M6-026 R1: 0xA19ECC -> 0x9F7390..0x9F82EC is unread; supply RtpcSubscribeA19ECC"))("context", node, 0x3FE3FFFE67BDUL);   // 0x9BC5D8..0x9BC5F0, unconditional
        if ((pbi.Flags0E9 & 4) == 0)
        {
            AppendList(pbi.LimiterArray1EC, GlobalVoiceList);
            foreach (var list in pbi.LimiterArray1EC.Items.ToArray())
                InsertIntoList9F3274(list, pbi);
        }
        if (pbi.PlayingId == 0) return 2;
        RegisterPlayingId(pbi);
        (SourceInfoA1EC54 ?? throw new WwiseMissingBehaviourException(
            "M6-026 6.1: 0xA1EC54 (run after 0xA04D48 returns 1) is unread; supply SourceInfoA1EC54"))(pbi);
        return 1;
    }

    /// <summary><c>0xA04D48(mgr, id, pbi, out)</c> (6.2), the playing-id reference the Play takes; also called on the failure path of <c>0xA379D8</c> when <c>[params+0x24] != 0</c> (C29.2).</summary>
    public void RegisterPlayingId(WwisePlayingInstance pbi)
        => (RegisterPlayingIdA04D48 ?? throw new WwiseMissingBehaviourException(
            "M6-026 6.2: 0xA04D48 needs the playing-id table; supply RegisterPlayingIdA04D48"))(pbi);

    /// <summary>6.7 <c>0xA380A4..0xA380E0</c>: appends the PBI at the tail of the global PBI list (<c>[[G+0x4C]+0x104] = pbi</c>, <c>[G+0x4C] = pbi</c>, <c>[G+0x48]++</c>).</summary>
    public void AppendToGlobalPbiList(WwisePlayingInstance pbi) => GlobalPbiList.Add(pbi);

    /// <summary>K8 <c>0xA38570..0xA38598</c>: unlinks the PBI from the global PBI list (<c>[G+0x48]--</c>).</summary>
    public void RemoveFromGlobalPbiList(WwisePlayingInstance pbi) => GlobalPbiList.Remove(pbi);

    // ------------------------------------------------------------------ 1.10, 1.11: Term and the count undo

    /// <summary>
    /// <c>0xA0228C(pbi)</c> (O8, C4): sets <c>1BE</c> bit 5, <c>[list+0x22]++</c> for every list of the array and <c>[G]++</c>; <see cref="ReleaseVirtual0A022E8"/> undoes it.
    /// </summary>
    public void AcquireVirtual0A0228C(WwisePlayingInstance pbi)
    {
        if ((pbi.Flags1BE & 0x20) != 0) return;
        pbi.Flags1BE |= 0x20;
        foreach (var list in pbi.LimiterArray1EC.Items) unchecked { list.Virtual22++; }
        GlobalVirtualCount++;
    }

    /// <summary><c>0xA022E8(pbi)</c> (1.11 (a)): clears <c>1BE</c> bit 5, <c>(u16)[list+0x22]--</c> for every list in the array, <c>0xA370E4</c> (<c>[G]--</c>).</summary>
    public void ReleaseVirtual0A022E8(WwisePlayingInstance pbi)
    {
        if ((pbi.Flags1BE & 0x20) == 0) return;
        pbi.Flags1BE = (byte)(pbi.Flags1BE & ~0x20);
        foreach (var list in pbi.LimiterArray1EC.Items) unchecked { list.Virtual22--; }
        unchecked { GlobalVirtualCount--; }
    }

    /// <summary>
    /// <c>0xA01684(pbi)</c> (1.11, F3): (a) the <c>1BE</c> bit 5 part runs regardless (<see cref="ReleaseVirtual0A022E8"/>); (b) if <c>1BD</c> bit 5 is clear: set it, <c>0x9F3528(list, pbi)</c> for each list
    /// (<c>[pbi+0x1F0]</c> re-read each step), <c>[pbi+0x1F0] = 0</c>, then <c>node vt+0x94(node, &amp;block{prio 0.0f, go=[pbi+0x14], array 0, u16 3, u16 0, byte 0, byte 1})</c>. Bit 5 already set: nothing else runs.
    /// </summary>
    public void UndoCounts0A01684(WwisePlayingInstance pbi)
    {
        ReleaseVirtual0A022E8(pbi);
        if ((pbi.Flags1BD & 0x20) != 0) return;
        pbi.Flags1BD |= 0x20;
        for (int i = 0; i < pbi.LimiterArray1EC.Count; i++) Remove9F3528(pbi.LimiterArray1EC.Items[i], pbi);
        var node = pbi.NodeE0 ?? throw new WwiseMissingBehaviourException("M6-026 1.11: the PBI's node [pbi+0xE0] is not set");
        pbi.LimiterArray1EC.Items.Clear();                                            // [pbi+0x1F0] = 0, stored before the call (0xA01738)
        var block = new WwiseLimitBlock
        {
            Priority = 0f, GameObject = pbi.GameObject14, Array = null, Word0C = 3, Count0E = 0, B10 = 0, B11 = 1,
        };
        RemoveNode9ED428(node, block);
    }

    /// <summary>
    /// <c>pbi vt+0x10</c> = <c>0xA029DC</c> (1.10), in order: (1) <see cref="UndoCounts0A01684"/>; (2, 3) cancel <c>+0x144</c> and <c>+0x148</c> (<c>0xA36618</c>); (4) <c>1BC &amp;= ~2</c>; (5) <c>[pbi+0x140] != 0</c>
    /// -> <c>0xA04DE8</c>; (6, 7) <see cref="TermSteps6And7"/>; (8) the limiter context list removal; (9) the idle test on the PBI node's limiter; (10..13) <see cref="TermSteps10To13"/>.
    /// </summary>
    public void TermPbiA029DC(WwisePlayingInstance pbi)
    {
        UndoCounts0A01684(pbi);                                                       // (1)
        if (pbi.Field144 != 0)                                                        // (2)
            (CancelTransitionA36618 ?? throw MissingCancel())(pbi, 0x144);
        if (pbi.Item148Id is not null)                                                // (3)
            (CancelTransitionA36618 ?? throw MissingCancel())(pbi, 0x148);
        pbi.Flags1BC = (byte)(pbi.Flags1BC & ~2);                                     // (4)
        if (pbi.PlayingId != 0)                                                       // (5) 0xA02C98
            (ReleasePlayingIdA04DE8 ?? throw new WwiseMissingBehaviourException(
                "M6-026 1.10: 0xA04DE8 (the playing-id release) is unread; supply ReleasePlayingIdA04DE8"))(pbi);
        (TermSteps6And7 ?? throw new WwiseMissingBehaviourException(
            "M6-026 1.10: steps 6 and 7 (0xA1C660, the [pbi+0x10C] free) are unread; supply TermSteps6And7"))(pbi);
        if (pbi.NodeE0 is { } node)
        {
            LimiterOf(node.Id)?.Contexts0C.Remove(pbi);                               // (8) 0xA02A84..0xA02BEC
            DestroyIfIdle(node);                                                      // (9) 0xA02C00..0xA02C3C
        }
        (TermSteps10To13 ?? throw new WwiseMissingBehaviourException(
            "M6-026 1.10: steps 10 to 13 (0xA1ECBC, 0xA3E27C, 0x9BDC8C, 0xA1E8F4) are unread; supply TermSteps10To13"))(pbi);
    }

    private static WwiseMissingBehaviourException MissingCancel()
        => new("M6-026 K6/1.10: 0xA36618 (transition cancel) is a required seam; supply CancelTransitionA36618");

    // ------------------------------------------------------------------ P4: the below byte (C29.4)

    /// <summary><c>lin(x)</c> of <c>0x9BEB30</c> (3.2): 0 when <c>x*0.05f &lt; -37.0f</c>, else the fast polynomial <c>(c0 + m*(c1 + m*c2)) * 2^k</c> with non-fused multiply-adds.</summary>
    public static float Lin9BEB30(float x)
    {
        float t = x * BitConverter.Int32BitsToSingle(0x3D4CCCCD);                     // 0.05f
        if (t < BitConverter.Int32BitsToSingle(unchecked((int)0xC2140000))) return 0f; // -37.0f
        float scaled = t * BitConverter.Int32BitsToSingle(0x4BD49A78);
        float sum = BitConverter.Int32BitsToSingle(0x4E7E0000) + scaled;
        uint u = FloatToU32(sum);                                                     // vcvt.u32.f32
        float m = BitConverter.Int32BitsToSingle(unchecked((int)((u & 0x7FFFFF) + 0x3F800000)));
        float k = BitConverter.Int32BitsToSingle(unchecked((int)((u >> 23) << 23)));
        float mulA = m * BitConverter.Int32BitsToSingle(0x3EA67F46);
        float inner = BitConverter.Int32BitsToSingle(0x3CAA70DE) + mulA;
        float mulB = m * inner;
        float outer = BitConverter.Int32BitsToSingle(0x3F272DDB) + mulB;
        return outer * k;
    }

    /// <summary><c>vcvt.u32.f32</c>: truncation toward zero, saturating, NaN and negatives to 0.</summary>
    public static uint FloatToU32(float v)
    {
        if (float.IsNaN(v) || v <= 0f) return 0;
        if (v >= 4294967296f) return uint.MaxValue;
        return (uint)v;
    }

    /// <summary>
    /// The <c>below</c> byte <c>0x9BEB30</c> writes at <c>0x9BED60</c> (3.2): <c>product &lt;= 0x37800000</c> (2^-16) with <c>product = lin(pbi+0x3C) * pbi+0x40 * lin(pbi+0x64)</c>; unordered is false.
    /// </summary>
    public static bool Below9BEB30(WwisePlayingInstance pbi)
    {
        float product = Lin9BEB30(pbi.Volume3C) * pbi.MuteFade40 * Lin9BEB30(pbi.ReadWord64());
        return product <= BitConverter.Int32BitsToSingle(0x37800000);
    }

    // ------------------------------------------------------------------ C1..C6: the global checks

    /// <summary>
    /// <c>0xA376C0(prio)</c> (C1, C2, C28.3): two memory tests (a threshold below 1.0f and a used/total ratio above it); when one fails, the kill step scans the global PBI
    /// list for the last non-dead PBI with priority &lt;= 101.0f. Returns 1, or 0 (which fails the Play) when the new priority is not above the candidate's or there is none.
    /// </summary>
    public int CheckMemoryA376C0(float prio)
    {
        float one = BitConverter.Int32BitsToSingle(OneBits);
        if (MemoryThreshold1 < one && MemoryExceeded(0, MemoryThreshold1)) return KillStepA377BC(prio);
        if (MemoryThreshold2 < one && MemoryExceeded(1, MemoryThreshold2)) return KillStepA377BC(prio);
        return 1;
    }

    private bool MemoryExceeded(int pool, float threshold)
    {
        var (total, used) = (PoolStatsA7ABA8 ?? throw new WwiseMissingBehaviourException(
            "M6-026 C1: 0xA7ABA8 (the pool statistics) is read only with a threshold below 1.0f; supply PoolStatsA7ABA8"))(pool);
        if (total == 0) return false;
        float ratio = (float)used / (float)total;
        return threshold < ratio;                                                     // bpl: an unordered compare is not exceeded
    }

    private int KillStepA377BC(float prio)
    {
        float s14 = VictimInitialPriority;
        WwisePlayingInstance? cand = null;
        foreach (var pbi in GlobalPbiList)
        {
            if ((pbi.Flags1BD & 2) != 0 || (pbi.Flags1BE & 0x2C) != 0) continue;
            if (pbi.Priority1C0 <= s14) { s14 = pbi.Priority1C0; cand = pbi; }         // vcmpe/bmi or equal
        }
        if (prio < s14 || prio == s14) return 0;
        if (cand is null) return 0;                                                   // C28.3
        Kill0A01CA4(cand, 3);
        return 1;
    }

    /// <summary>
    /// <c>0xA37880(prio)</c> (C4, C5): <c>n = [G+0x48] + 1 - [G]</c> against the global voice list's max; when <c>n</c> exceeds it, one pass over the global voice list. Returns 1 or
    /// 0x50 and never 2.
    /// </summary>
    public int CheckVoiceCountA37880(float prio)
    {
        uint n = unchecked((uint)GlobalPbiList.Count + 1u - GlobalVirtualCount);
        ushort max = GlobalVoiceList.Max;
        if (n <= max) return 1;
        float s18 = VictimInitialPriority;
        int count = 0;
        bool r8 = false;
        WwisePlayingInstance? cand = null;
        int candCode = 0;
        foreach (var pbi in GlobalVoiceList.Items)
        {
            if ((pbi.Flags1BD & 2) != 0 || (pbi.Flags1BE & 0x2C) != 0) continue;
            count = (ushort)(count + 1);
            if (!(prio >= pbi.Priority1C0)) continue;                                 // 0xA37938 vcmpe; blt: an unordered compare skips
            int c = (NextSourceCodeA01768 ?? throw new WwiseMissingBehaviourException(
                "M6-026 C5: 0xA01768 needs NextSourceCodeA01768 (set when a bridge holds this limiter)"))(pbi);
            if (c == 0) { if (max >= count) r8 = true; }
            else { s18 = pbi.Priority1C0; candCode = c; cand = pbi; }
        }
        if (max > count) return 1;
        if (prio < s18 || prio == s18 || cand is null) return r8 ? 1 : 0x50;
        if (candCode != 1) return 1;
        Kill0A01CA4(cand, 2);
        return 1;
    }

    // ------------------------------------------------------------------ E1..E3: the per-frame enforcement

    /// <summary>
    /// <c>0xA39564</c> (E1), called each frame by the voice pass entry (<c>0xA44948</c>, the <see cref="WwiseVoiceBusPass.NodeCleanup"/> seam): (a) when <c>[G+0x60] != 0 &amp;&amp; [G+0x61] == 0</c>,
    /// <c>0xA00494</c> for every PBI and <c>[G+0x60] = 0</c>; (b) <c>1BE &amp;= ~4</c> for every PBI; (c) <c>0x9F3BA4</c> for every registered list.
    /// </summary>
    public void PerFrameA39564()
    {
        if (G60 != 0 && G61 == 0)
        {
            foreach (var pbi in GlobalPbiList.ToArray()) ReRegisterA00494(pbi);
            G60 = 0;
        }
        foreach (var pbi in GlobalPbiList) pbi.Flags1BE = (byte)(pbi.Flags1BE & ~4);
        for (int i = 0; i < GlobalLimiterArray.Count; i++) EnforceListA3BA4(GlobalLimiterArray[i]);
    }

    private void ReRegisterA00494(WwisePlayingInstance pbi)
    {
        if (pbi.LimiterArray1EC.Count != 0 || (pbi.Flags0E9 & 4) != 0) return;         // 0xA0049C
        (ReRegisterA0054C ?? throw new WwiseMissingBehaviourException(
            "M6-026 8.7: 0xA0054C (the re-registration of a PBI with an empty array) is unread; supply ReRegisterA0054C"))(pbi);
    }

    /// <summary>
    /// <c>0x9F3BA4(list)</c> (E2): with a nonzero max exceeded by the count, the first <c>max</c> counted PBIs stay (pass 1) and the rest are killed (pass 2), with reason 2 for the
    /// global voice list and 1 otherwise, through <c>0xA020F4</c> when the list's virtual byte is set.
    /// </summary>
    public void EnforceListA3BA4(WwisePbiList list)
    {
        int max = list.Max;
        if (max == 0 || list.Items.Count <= max) return;
        var items = list.Items.ToArray();
        int counted = 0, i = 0;
        for (; i < items.Length && counted < max; i++)                                // pass 1: 0x9F3C20 cmp r8,sb; blo
        {
            var pbi = items[i];
            if ((pbi.Flags1BE & 8) != 0 || (pbi.Flags1BD & 2) != 0 || (pbi.Flags1BE & 0x14) != 0) continue;
            if (AudibleA01DE0(pbi) || (pbi.NextSourceCache1BB & 0x78) == 0) counted++;   // 0x9F3C0C ldrb [r5,#0x1bb]; tst #0x78
        }
        for (; i < items.Length; i++)                                                 // pass 2
        {
            var pbi = items[i];
            if ((pbi.Flags1BD & 2) != 0 || (pbi.Flags1BE & 8) != 0) continue;
            bool global = ReferenceEquals(list, GlobalVoiceList);
            int reason = global ? 2 : 1;
            if (list.Virtual) Virtualize0A020F4(pbi, reason); else Kill0A01CA4(pbi, reason);
        }
    }

    private bool AudibleA01DE0(WwisePlayingInstance pbi)
    {
        if (pbi.Field154 is not { } voice) return false;
        return (VoiceAudibleA4B3E8 ?? throw new WwiseMissingBehaviourException(
            "M6-026 E2: 0xA4B3E8 (the audibility test inside 0xA01DE0) is a RECOVERABLE_GAP; supply VoiceAudibleA4B3E8"))(voice);
    }

    /// <summary>
    /// <c>0xA020F4(pbi, reason)</c> (E3): code 1 takes the body of <see cref="Kill0A01CA4"/>, code 2 sets <c>1BE |= 4</c>, anything else does nothing. The code is the
    /// <c>0xA01768</c> cache (through <see cref="NextSourceCodeA01768"/>).
    /// </summary>
    public void Virtualize0A020F4(WwisePlayingInstance pbi, int reason)
    {
        int code = (NextSourceCodeA01768 ?? throw new WwiseMissingBehaviourException(
            "M6-026 E3: 0xA01768 needs NextSourceCodeA01768 (set when a bridge holds this limiter)"))(pbi);
        if (code == 1) Kill0A01CA4(pbi, reason);
        else if (code == 2) pbi.Flags1BE |= 4;
    }
}
