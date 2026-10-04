// fidelity: M6-025, M6-026
namespace Cozmo.Robot.Animation.Wwise;

// The shipped Play path of correction C32.1 (P1..P12) and the CalcEffectiveParams / below-audibility bodies of C31.3, transcribed from libcozmoEngine.so:
// 0x9BEB30 (the PBI context init the Sound Play calls at 0xA37D1C), its callees 0x9FB9B8 / 0x9FAEE8 / 0x9FBE74, CalcEffectiveParams 0x9FFAD4, the partial recompute 0x9FF368,
// 0x9BCA68 (AddSrc's below-audibility test), 0x9F1F80 (node vt+0x84), 0xA00618 (+ the loop count 0xA1E280) and 0xA0067C with the transition manager (0xA36268, 0xA35838,
// 0xA358EC, 0xA366AC, 0xA366D0).
//
// Production entry. The engine function is 0xA379D8 (Sound Play), which calls 0x9BEB30 at 0xA37D1C, 0xA00618 at 0xA38078 and 0xA0067C at 0xA38094. The C# counterpart is
// WwisePlaybackBridge.PlaySound, which calls WwisePlayPath.InitContext9BEB30, BeforePlayA00618 and PbiPlayA0067C. Nothing in production constructs a WwisePlayPath or a
// bridge yet (C30.W is parked); the path is exercised by tests, with doubles for the unread seams.
//
// Unread bodies are named seams (WwisePlaySeams) that throw WwiseMissingBehaviourException when reached unset.
//
// B-M6b-4 batch 5c (C34.1, C34.2): the bus walk (0x9BDA6C, 0x9F4BB8, 0x9C54E8, 0x9C39DC: WwiseBusWalk, WwiseRoutingNode), the RTPC evaluation 0xA11590 (WwiseRtpcStore), the modulator list consumption (0x9E8224, 0x9E62AC,
// 0x9E61B4, 0xA01918: WwiseModulatorManager) and 0x9BE898 with [ctx+0xD0] == 0 replace the test-double seams FirstOutputBus9F4BB8, BusFlag9C54E8, BusVolume9C39DC, A9E8224, A9E62AC, Rtpc9A11590 and CtxObject9BE898.

/// <summary>One record of the PBI's transition list (<c>pbi+0x10C</c>, 12 bytes): the byte at <c>+4</c> (bit 1 keeps the record across the prune <c>0x9FFBAC..0x9FFBF8</c>) and the float at <c>+8</c> (the multiplier).</summary>
public sealed class WwiseTransitionRecord
{
    /// <summary><c>+0</c>.</summary>
    public uint Word0 { get; set; }

    /// <summary><c>+4</c> (byte).</summary>
    public byte Flags4 { get; set; }

    /// <summary><c>+8</c> (float).</summary>
    public float Value8 { get; set; }
}

/// <summary>The arguments of node <c>vt+0xAC</c> (GetAudioParameters) at <c>0x9FFCD0..0x9FFD0C</c>.</summary>
/// <param name="Pbi">The PBI; <c>r1 = pbi+0x3C</c> is its parameter block, <c>r3 = pbi+0x10C</c> its transition list, and the key is <c>pbi+0x14</c>.</param>
/// <param name="Node">The node (<c>r0</c>).</param>
/// <param name="Mask"><c>r2</c>: 0xFFFFFFFF when <c>[pbi+0xE9]</c> bit 3 is set, else 0xFFFFFFDF.</param>
/// <param name="Ranges">The stack word <c>[sp+4]</c>: <c>pbi+0x118</c> when <c>[pbi+0x1BC]</c> bit 0 is clear, else null.</param>
/// <param name="Params">The stack word <c>[sp+8]</c> when <c>[pbi+0xE8]</c> bit 6 is set and the Play params exist: <c>params+0x108</c>; null when <paramref name="Local"/> is passed or <c>r7</c> is 0.</param>
/// <param name="Local">The stack word <c>[sp+8]</c> when <c>[pbi+0xE8]</c> bit 6 is clear: the 12-byte zero block at <c>sp+0x2C</c>.</param>
/// <param name="StackWord0C">The stack word <c>[sp+0xC]</c>: the constant 1 (<c>0x9FFD00</c>, <c>0x9FFD08</c>; C34.1 B18).</param>
/// <param name="StackWord10">The stack word <c>[sp+0x10]</c>: <c>r6</c>, which is 0 on every path (<c>0x9FFC18</c>, <c>0x9FFEB8</c>, <c>0x9FFD04</c>; C34.1 B18).</param>
public sealed record WwiseVtAcArgs(WwisePlayingInstance Pbi, WwiseNode Node, uint Mask, WwiseGainRanges? Ranges, WwisePlayInitParams? Params, WwiseAcLocalBlock? Local, uint StackWord0C = 1, uint StackWord10 = 0)
{
    /// <summary>The out vector <c>r7</c> (<c>[sp+8]</c>): the local block, else <c>params+0x108</c>, else null (<c>0x9FFCAC..0x9FFCBC</c>).</summary>
    public WwiseAcLocalBlock? OutList => Local ?? Params?.Block108;
}

/// <summary>The 12-byte vector <c>{data, count, capacity}</c>: the zero block CalcEffectiveParams passes to node <c>vt+0xAC</c> at <c>sp+0x2C</c> (<c>0x9FFCB0</c>, <c>0x9FFCC4</c>, <c>0x9FFCC8</c>) when <c>[pbi+0xE8]</c> bit 6 is clear, and the vector at <c>params+0x108</c>. Node <c>vt+0xAC</c> may fill it.</summary>
public sealed class WwiseAcLocalBlock
{
    /// <summary><c>[sp+0x2C]</c>: an object the call allocated; CalcEffectiveParams frees it (<c>0x9FFE5C..0x9FFE7C</c>).</summary>
    public uint Word0 { get; set; }

    /// <summary><c>[sp+0x30]</c>: tested by the first-time block (<c>0x9FFF34</c>); cleared when <see cref="Word0"/> is freed.</summary>
    public uint Word4 { get; set; }

    /// <summary><c>[sp+0x34]</c>.</summary>
    public uint Word8 { get; set; }

    /// <summary>
    /// The 20-byte records <see cref="Word0"/> points at (the vector's data); the first <see cref="Word4"/> of them are the vector's elements. The producer is node <c>vt+0xAC</c> -&gt; <c>0x9EF258</c> -&gt; <c>0xA6E848</c> (unread). The same shape
    /// is the vector at <c>params+0x108</c> (<c>+0x108</c> data, <c>+0x10C</c> count, <c>+0x110</c> capacity).
    /// </summary>
    public List<WwiseModulatorOutRecord> Records { get; } = new();

    /// <summary>The elements in use: the first <see cref="Word4"/> records (<c>0x9E62B4..0x9E62C8</c>: <c>end = data + 20 * count</c>).</summary>
    public IReadOnlyList<WwiseModulatorOutRecord> InUse
        => Word4 <= (uint)Records.Count ? Records.GetRange(0, (int)Word4)
            : throw new InvalidOperationException("the vector's count exceeds the records its data pointer addresses");
}

/// <summary>The unread bodies the shipped Play path reaches. Each is required: reaching one unset throws.</summary>
public sealed class WwisePlaySeams
{
    /// <summary>Node <c>vt+0xAC</c> (<c>0x9FFD0C</c>): GetAudioParameters accumulates into the PBI's parameter block and transition list. Its body is the M6-010 model, not wired to the PBI fields; unread here.</summary>
    public Action<WwiseVtAcArgs>? NodeVtAC { get; set; }

    /// <summary>
    /// The three words of the modulator context record (<c>0x9FFF38..0x9FFF98</c>, <c>0xA0193C..0xA01998</c>) the C# PBI does not model: <c>[pbi+0x1E4]</c> (the ctor stores the event dword <c>params[0x84..0x87]</c>, whose upper two bytes are
    /// not stored on the Play path), <c>[pbi+0x1E8]</c> and <c>[pbi+0x1C]</c> (no adopted writer). Required when <c>0x9E62AC</c> runs.
    /// </summary>
    public Func<WwisePlayingInstance, (uint Word1E4, uint Word1E8, uint Word1C)>? ModulatorCtxWords { get; set; }

    /// <summary><c>0x9FB17C</c> (<c>0x9FBED4</c>): the part of <c>0x9FBE74</c> that runs when <c>[ctx+0xD0] != 0</c>. Unread.</summary>
    public Action<WwisePlayingInstance>? A9FB17C { get; set; }

    /// <summary><c>0x9FD8C0</c> / <c>0x9FD8D0</c> (<c>0x9FF2BC</c>, <c>0x9FF2F8</c>), the bodies of <c>0x9BDA28</c> when <c>[ctx+0xA0] != 0</c>. Unread.</summary>
    public Action<WwisePlayingInstance, bool>? A9FD8C0 { get; set; }

    /// <summary><c>0x9FF0D8(*0x108D8E8, [pbi+0xAC], 0x9FB994(node))</c> (<c>0xA00674</c>), the tail of <c>0xA00618</c> when <c>[pbi+0xAC] != 0</c>. Unread.</summary>
    public Action<WwisePlayingInstance>? A9FF0D8 { get; set; }

    /// <summary><c>0x9E808C</c>'s list: the records of the list at <c>[pbi+0x34]</c>. The type is the V28 records (<c>[rec+0x30]</c>, <c>+0x48</c>, <c>+0x4C</c>, <c>+0x50</c>, <c>+0x58</c>).</summary>
    public Func<WwisePlayingInstance, IEnumerable<WwiseListRecord>>? RecordsOf34 { get; set; }

    /// <summary><c>0xA35D44(item, info, tick)</c> (<c>0xA362B8</c>): the transition item's init from its info block; a result of 2 fails the create. Unread.</summary>
    public Func<WwiseTransitionItem, WwiseTransitionInfo, uint, int>? TransitionInit9A35D44 { get; set; }

    /// <summary>The failure call <c>[info.Target]-&gt;vt+0(id, value, 1)</c> (<c>0xA36324..0xA36340</c>) that applies the target value at once. Unread.</summary>
    public Action<WwiseTransitionInfo>? TransitionApplyAtOnce { get; set; }

    /// <summary><c>0xA3587C(item)</c> (<c>0xA36308</c>), the item's teardown before it is freed on the failure exits that happen after the init. Unread.</summary>
    public Action<WwiseTransitionItem>? TransitionTeardownA3587C { get; set; }

    /// <summary>PBI <c>vt+0x14(pbi, 0x1000000, 1.0f, 1)</c> (<c>0xA00850</c>), run when the transition create returned 0. Unread.</summary>
    public Action<WwisePlayingInstance>? PbiVt14 { get; set; }
}

/// <summary>A record of the V28 list <c>0x9E808C</c> walks: <c>[+0x30]</c> an object whose <c>[+8]</c> is copied to <c>[+0x4C]</c>, <c>[+0x48]</c> a state, <c>[+0x50]</c> a limit, <c>[+0x58]</c> a counter.</summary>
public sealed class WwiseListRecord
{
    /// <summary><c>+0xC</c>: cleared for every item by <c>0x9E8224</c> (R6).</summary>
    public uint Word0C { get; set; }

    /// <summary><c>+0x30</c>: the object, or null.</summary>
    public uint? Object30Word8 { get; set; }

    /// <summary><c>+0x48</c>.</summary>
    public int State48 { get; set; }

    /// <summary><c>+0x4C</c>.</summary>
    public uint Word4C { get; set; }

    /// <summary><c>+0x50</c>.</summary>
    public int Limit50 { get; set; }

    /// <summary><c>+0x58</c>.</summary>
    public int Counter58 { get; set; }
}

/// <summary>The info block <c>0xA0067C</c> builds at <c>sp+0x14</c> for <c>0xA36268</c> (<c>0xA007B8..0xA007F4</c>).</summary>
/// <param name="TargetIsPbi8">The <c>pbi+8</c> sub-object (word 0).</param>
/// <param name="Id">Word 1: 0x1000000.</param>
/// <param name="From">Word 2: 0.0f.</param>
/// <param name="To">Word 3: 1.0f.</param>
/// <param name="TimeBits">Word 4: the fade time word <c>[p]</c>.</param>
/// <param name="Curve">Word 5: <c>[p+4]</c>.</param>
/// <param name="Byte0">Byte <c>+0x18</c>: 0.</param>
/// <param name="Byte1">Byte <c>+0x19</c>: 1.</param>
/// <param name="Byte2">Byte <c>+0x1A</c>: 0.</param>
public sealed record WwiseTransitionInfo(WwisePlayingInstance TargetIsPbi8, uint Id, float From, float To, uint TimeBits, uint Curve, byte Byte0, byte Byte1, byte Byte2);

/// <summary>A transition item (0x3C bytes, <c>0xA35838</c>): <c>[0] = 0x20000000</c>, the state word <c>[+0x30]</c>, the factor <c>[+0x38]</c> and the flag byte <c>[+0x34]</c>.</summary>
public sealed class WwiseTransitionItem
{
    private static uint s_nextId = 0x10000000;

    /// <summary>The handle stored at <c>pbi+0x144</c> (the item's address; synthetic).</summary>
    public uint Id { get; } = unchecked(s_nextId += 0x40);

    /// <summary><c>[0]</c>.</summary>
    public uint Word0 { get; set; } = 0x20000000;

    /// <summary><c>[+0x18]</c>.</summary>
    public uint Word18 { get; set; }

    /// <summary><c>[+0x20]</c>.</summary>
    public uint Word20 { get; set; }

    /// <summary><c>[+0x24]</c>.</summary>
    public uint Word24 { get; set; }

    /// <summary><c>[+0x28]</c>.</summary>
    public uint Word28 { get; set; }

    /// <summary><c>[+0x30]</c>: the state (1 starting, 2, 3, 4).</summary>
    public int State30 { get; set; }

    /// <summary><c>[+0x34]</c> (byte): bits 0..2 cleared by the ctor.</summary>
    public byte Flags34 { get; set; }

    /// <summary><c>[+0x38]</c> (float).</summary>
    public float Factor38 { get; set; }
}

/// <summary>
/// The transition manager (<c>*0x108D870</c>): the two item vectors (<c>mgr+0</c> and <c>mgr+0xC</c>, each <c>{data, count, capacity}</c>), the tick at <c>mgr+0x4C</c> and the item create
/// <c>0xA36268</c> with the state toggles <c>0xA366AC</c> / <c>0xA366D0</c> and the factor <c>0xA358EC</c>.
/// </summary>
public sealed class WwiseTransitionManager
{
    private readonly Dictionary<uint, WwiseTransitionItem> _items = new();

    /// <param name="seams">The unread callee bodies (<c>0xA35D44</c>, the failure call).</param>
    public WwiseTransitionManager(WwisePlaySeams seams) => Seams = seams ?? throw new ArgumentNullException(nameof(seams));

    /// <summary><c>mgr+0</c>: the first vector.</summary>
    public List<WwiseTransitionItem> ListA { get; } = new();

    /// <summary><c>mgr+0xC</c>: the second vector (selected by the create's fourth argument 1).</summary>
    public List<WwiseTransitionItem> ListB { get; } = new();

    /// <summary>The capacity of <see cref="ListA"/> (<c>[mgr+8]</c>); grown by 0x80 entries.</summary>
    public int CapacityA { get; set; }

    /// <summary>The capacity of <see cref="ListB"/> (<c>[mgr+0x14]</c>).</summary>
    public int CapacityB { get; set; }

    /// <summary><c>[mgr+0x4C]</c>.</summary>
    public uint Tick { get; set; }

    /// <summary>The pool allocation failure (<c>0xA7A7F4</c> returning null): true fails that one allocation. Each call is one attempt in the engine's order (item, then vector growth).</summary>
    public Func<bool>? AllocationFails { get; set; }

    /// <summary>The unread callee seams.</summary>
    public WwisePlaySeams Seams { get; }

    /// <summary>The item behind a handle stored at <c>pbi+0x144</c>, or null.</summary>
    public WwiseTransitionItem? Item(uint id) => _items.TryGetValue(id, out var item) ? item : null;

    /// <summary>
    /// <c>0xA36268(mgr, info, start, listSelect)</c> (P11): allocates an item (null goes to the failure exit), runs the ctor <c>0xA35838</c> and <c>0xA35D44(item, info, [mgr+0x4C])</c> (2 frees the item and takes the failure exit),
    /// appends it to the vector (<c>mgr+0xC</c> when <paramref name="listSelect"/> is 1), growing it by 0x80 entries when full (an allocation failure frees the item and takes the failure exit), and sets <c>[item+0x30] = 1</c> when
    /// <paramref name="start"/> is non-zero. The failure exit calls <c>[info.Target]-&gt;vt+0(id, to, 1)</c> and returns null.
    /// </summary>
    public WwiseTransitionItem? Create0A36268(WwiseTransitionInfo info, bool start, int listSelect)
    {
        var list = listSelect == 1 ? ListB : ListA;                                  // 0xA36274 addeq r4,r0,#0xc
        if (AllocationFails?.Invoke() == true) return FailureExit(info);             // 0xA36290 bl 0xA7A7F4; 0xA36298 beq 0xA36324
        var item = new WwiseTransitionItem();                                         // 0xA3629C bl 0xA35838
        int r = (Seams.TransitionInit9A35D44 ?? throw new WwiseMissingBehaviourException(
            "M6-025 P11: 0xA35D44 (the transition item init called at 0xA362B8) is unread; supply WwisePlaySeams.TransitionInit9A35D44"))(item, info, Tick);
        if (r == 2) return FailureExit(info, item);                                   // 0xA362BC cmp r0,#2; beq 0xA36304 (item torn down and freed)
        int capacity = listSelect == 1 ? CapacityB : CapacityA;                      // [r4+8]
        if (list.Count >= capacity)                                                   // 0xA362C8 cmp r8,fp; bhs 0xA3634C
        {
            if (AllocationFails?.Invoke() == true) return FailureExit(info, item);    // 0xA36358 bl 0xA7A7F4; 0xA36360 beq 0xA36304
            capacity += 0x80;                                                          // 0xA3634C add fp,fp,#0x80
            if (listSelect == 1) CapacityB = capacity; else CapacityA = capacity;     // 0xA363AC str fp,[r4,#8]
        }
        list.Add(item);                                                                // 0xA362F0 str r5,[sl,r2,lsl #2]; [r4+4] = count + 1
        _items[item.Id] = item;
        if (start) item.State30 = 1;                                                   // 0xA362EC..0xA362FC
        return item;
    }

    private WwiseTransitionItem? FailureExit(WwiseTransitionInfo info, WwiseTransitionItem? item = null)
    {
        if (item is not null)                                                          // 0xA36304..0xA36320: 0xA3587C(item), 0xA35878, the pool free
            (Seams.TransitionTeardownA3587C ?? throw new WwiseMissingBehaviourException(
                "M6-025 P11: 0xA3587C (the item teardown, 0xA36308) is unread; supply WwisePlaySeams.TransitionTeardownA3587C"))(item);
        (Seams.TransitionApplyAtOnce ?? throw new WwiseMissingBehaviourException(
            "M6-025 P11: the failure exit of 0xA36268 calls [info.target]->vt+0(id, to, 1) (0xA36324..0xA36340), whose body is unread; supply WwisePlaySeams.TransitionApplyAtOnce"))(info);
        return null;                                                                   // 0xA36344 mov r0,r5 (r5 = 0)
    }

    /// <summary><c>0xA366AC(mgr, item)</c>: state 1 becomes 2, otherwise state 4 becomes 3. A handle with no item is a null dereference in the engine.</summary>
    public void Toggle366AC(uint id)
    {
        var item = Item(id) ?? throw new InvalidOperationException("M6-025 P11: 0xA366AC reads [item+0x30] of a transition handle that is not an item");
        if (item.State30 == 1) item.State30 = 2;                                      // 0xA366B0..0xA366BC
        else if (item.State30 == 4) item.State30 = 3;                                 // 0xA366C0..0xA366CC
    }

    /// <summary><c>0xA366D0(mgr, item)</c>: state 3 becomes 4, otherwise state 2 becomes 1.</summary>
    public void Toggle366D0(uint id)
    {
        var item = Item(id) ?? throw new InvalidOperationException("M6-025 P11: 0xA366D0 reads [item+0x30] of a transition handle that is not an item");
        if (item.State30 == 3) item.State30 = 4;                                      // 0xA366D4..0xA366E0
        else if (item.State30 == 2) item.State30 = 1;                                 // 0xA366E4..0xA366F0
    }

    /// <summary>
    /// <c>0xA358EC(item, timeMs)</c>: nothing when <paramref name="timeMs"/> is 0 or <c>[item+0x34]</c> bit 2 is clear (<c>0xA358F4..0xA35908</c>). Otherwise
    /// <c>[item+0x38] = expf((float)(-frames) / ((float)timeMs * 0.2f / 1000.0f * 48000.0f))</c> with <c>frames = u16[0x1052440]</c>, all in single precision
    /// (<c>0xA3591C..0xA35960</c>; <c>vcvt.f32.s32</c>, <c>vmul</c>, <c>vdiv</c>). <c>expf</c> is the phone's libm (<c>0x4D0058</c>), so the result carries the platform library's rounding.
    /// </summary>
    public static void SetFactorA358EC(WwiseTransitionItem item, int timeMs, ushort frames, Func<float, float>? expf = null)
    {
        bool bit2 = ((item.Flags34 >> 2) & 1) != 0;                                  // 0xA358F4 ubfx r3,r3,#2,#1
        if (timeMs == 0 || !bit2) return;                                             // 0xA358F8..0xA35910
        float s15 = (float)timeMs;                                                    // 0xA35930 vcvt.f32.s32
        s15 *= BitConverter.Int32BitsToSingle(0x3E4CCCCD);                            // 0xA35940: 0.2f
        s15 /= BitConverter.Int32BitsToSingle(0x447A0000);                            // 0xA35948: 1000.0f
        s15 *= BitConverter.Int32BitsToSingle(0x473B8000);                            // 0xA3594C: 48000.0f
        float s14 = (float)(-(int)frames);                                            // 0xA3593C rsb r3,r3,#0; 0xA35954 vcvt.f32.s32
        item.Factor38 = (expf ?? MathF.Exp)(s14 / s15);                               // 0xA35958 vdiv; 0xA35960 bl expf
    }
}

/// <summary>
/// The shipped Play path (C32.1) and the effective-parameter bodies (C31.3). One instance serves the bridge; it holds the unread-body seams and the node lookup the walks need.
/// </summary>
public sealed class WwisePlayPath
{
    private readonly Func<WwiseNode, WwiseNode?> _parentOf;

    /// <param name="parentOf">The parent node (<c>[node+0x34]</c>); null at the root.</param>
    /// <param name="seams">The unread callee bodies.</param>
    public WwisePlayPath(Func<WwiseNode, WwiseNode?> parentOf, WwisePlaySeams seams)
    {
        _parentOf = parentOf ?? throw new ArgumentNullException(nameof(parentOf));
        Seams = seams ?? throw new ArgumentNullException(nameof(seams));
    }

    /// <summary>The unread callee bodies.</summary>
    public WwisePlaySeams Seams { get; }

    /// <summary>
    /// The runtime node behind a hierarchy node: the engine's node object, whose <c>[+0x34]</c> parent and <c>[+0x38]</c> output-bus links, bus fields and subscription data the bus walk reads. The C# hierarchy nodes are parsed records without those
    /// links and no production code builds the runtime graph yet, so the mapping is the host's. Required when CalcEffectiveParams walks to the bus.
    /// </summary>
    public Func<WwiseNode, WwiseRoutingNode?>? RuntimeNodeOf { get; set; }

    /// <summary>The RTPC manager <c>*0x108D908</c> that <c>0xA11590</c> reads (<see cref="WwiseRtpcStore.A11590"/>). Required when an RTPC bit is evaluated.</summary>
    public WwiseRtpcStore? Rtpc { get; set; }

    /// <summary>The modulator manager <c>*0x10400E8</c> that <c>0x9E62AC</c> / <c>0x9E61B4</c> take as <c>r0</c>. Required when a modulator out list is non-empty.</summary>
    public WwiseModulatorManager? Modulators { get; set; }

    /// <summary>The transition manager (<c>*0x108D870</c>) <c>0xA0067C</c> creates items in. Required for a Play with a fade.</summary>
    public WwiseTransitionManager? Transitions { get; set; }

    /// <summary>The global LCG (<c>0x108D868</c>) the loop count of <c>0xA1E280</c> advances. Required when a Sound has a ranged loop count.</summary>
    public WwiseRng? Rng { get; set; }

    /// <summary>The frames global <c>u16[0x1052440]</c> (C25.7) the factor <c>0xA358EC</c> reads.</summary>
    public Func<ushort>? Frames { get; set; }

    // ------------------------------------------------------------------ node walks

    /// <summary>
    /// The walk <c>0x9F1F80</c>, <c>0x9FB9B8</c> and <c>0x9FBE74</c> make to the node they read: from <paramref name="node"/> up while <c>[node+0x40] &amp; 0xFFE == 0</c> and a parent exists
    /// (<c>0x9F1F98..0x9F1FBC</c>, <c>0x9FB9C4..0x9FBA2C</c>, <c>0x9FBE90..0x9FBEB0</c>). <c>[node+0x40]</c> bits 1..11 are stored by <c>0x9F6D94</c> from positioning bit 0.
    /// </summary>
    public WwiseNode TopPositioningNode(WwiseNode node)
    {
        var n = node;
        while ((n.Params.PositioningBits & 1) == 0 && _parentOf(n) is { } parent) n = parent;
        return n;
    }

    /// <summary>
    /// Node <c>vt+0x84</c> = <c>0x9F1F80(node, &amp;out)</c> (P4): <c>*out = 0</c>, the walk above, and <c>[top+0x2C]</c> (the 3D positioning object, allocated only by <c>0x9FB3EC</c> when positioning bits 0 and 3 are both set) decides: 0 returns
    /// 0 with <c>*out = 0</c>. The node reader refuses bits 0 and 3 together, so no parsed node has <c>[+0x2C]</c> set and the result is always <c>(false, 0.0f)</c>.
    /// </summary>
    public static (bool Gate, float Out) NodeVt84A9F1F80(WwiseNode node)
    {
        if ((node.Params.PositioningBits & 0x09) == 0x09)                            // the reader's refusal (WwiseHierarchy): such a node cannot exist
            throw new WwiseMissingBehaviourException("M6-026 P4: [node+0x2C] is set (positioning bits 0 and 3); the 3D object and its hash lookup (0x9F1FCC..0x9F2098) are not modelled");
        return (false, 0f);                                                           // 0x9F1F8C str r3,[r1]; 0x9F1FC8 beq 0x9F20A0 (r0 = 0)
    }

    /// <summary>
    /// <c>0xA11590(*0x108D908, node+0x10, id, key)</c> with <c>key = r8</c> of <c>0x9FAEE8</c> = <c>pbi+0x14</c> (<c>0x9FAF6C..0x9FAF80</c>, <c>0x9FB10C..0x9FB11C</c>). The key struct <c>{[pbi+0x14], 0, 0, 0xFF, 0xFF, 0}</c> is built by <c>0x9BC90C</c> and copied by
    /// <c>0xA19CDC</c> (unread); a PBI whose <c>RtpcKey14</c> is not a <see cref="WwiseGainRtpcKey"/> has no modelled key.
    /// </summary>
    private float RtpcValue(WwiseNode node, byte id, WwisePlayingInstance pbi)
    {
        var rt = (RuntimeNodeOf ?? throw new WwiseMissingBehaviourException(
            "M6-025 P8: RTPC evaluation (0xA11590) keys on node+0x10 of the runtime node; supply WwisePlayPath.RuntimeNodeOf"))(node)
            ?? throw new WwiseMissingBehaviourException($"M6-025 P8: node {node.Id} has no runtime node");
        var store = Rtpc ?? throw new WwiseMissingBehaviourException("M6-009 R1: RTPC parameter evaluation needs the RTPC manager (*0x108D908); supply WwisePlayPath.Rtpc");
        var key = pbi.RtpcKey14 is WwiseGainRtpcKey k ? k
            : throw new WwiseMissingBehaviourException("M6-025 P8: the PBI key [pbi+0x14] (0x9BC90C / 0xA19CDC) is not a WwiseGainRtpcKey; its layout beyond word 0 is unread");
        return store.A11590(rt.SubscriptionKey10, id, key);
    }

    private static bool HasRtpc(WwiseNode node, int id) => node.Params.Rtpcs.Any(r => r.ParamId == id);

    private static uint PropBits(WwiseNode node, byte id) => node.Params.Props.TryGetValue(id, out var v) ? v : 0u;

    /// <summary>
    /// <c>0x9FAEE8(node, key, out)</c> (P8): stores the node's properties 0xC, 0xD and 0xE (0 when absent) to <c>out[0..2]</c> and <c>[node+0x47] &amp; 1</c> to the byte at <c>out+0xC</c>. RTPC parameters 0x12 / 0x13 (the first two) and 0x18
    /// (the third) replace the property with the <c>0xA11590</c> result and make the function return 1. Returns 0 otherwise. <c>[node+0x47]</c> bit 0 is 0 from the node ctor (<c>0x9F40B4</c>) unless positioning bits 0 and 1 are both set, which
    /// stores positioning bit 2 (<c>0x9ECF7C..0x9ECF8C</c>).
    /// </summary>
    public int A9FAEE8(WwiseNode node, WwisePlayingInstance pbi)
    {
        int r7 = 0;
        bool b12 = HasRtpc(node, 0x12), b13 = HasRtpc(node, 0x13);
        if (b12 || b13)                                                               // 0x9FAF30..0x9FAF3C, 0x9FAF5C: either pan RTPC replaces both property reads
        {
            pbi.PanB4 = b12 ? RtpcValue(node, 0x12, pbi) : 0f;                                  // 0x9FAF80 / 0x9FB11C -> [r5]; 0x9FAF50 stores 0 when only 0x13 is set
            pbi.PanB8 = b13 ? RtpcValue(node, 0x13, pbi) : 0f;                                  // 0x9FB134 -> [r5+4]; 0x9FAF8C stores 0 when only 0x12 is set
            r7 = 1;                                                                   // 0x9FAF74, 0x9FB138
        }
        else
        {
            pbi.PanB4 = BitConverter.UInt32BitsToSingle(PropBits(node, 0xC));         // 0x9FB048: [r5] = property 0xC
            pbi.PanB8 = BitConverter.UInt32BitsToSingle(PropBits(node, 0xD));         // 0x9FB098: [r5+4] = property 0xD
        }
        pbi.PanBC = HasRtpc(node, 0x18)                                               // 0x9FAF9C..0x9FAFB4
            ? RtpcValue(node, 0x18, pbi)                                               //   0xA11590 -> [r5+8]
            : BitConverter.UInt32BitsToSingle(PropBits(node, 0xE));                   //   0x9FB0A0: property 0xE
        bool bit47 = (node.Params.PositioningBits & 3) == 3 && ((node.Params.PositioningBits >> 2) & 1) != 0;
        pbi.PanC0 = (byte)(bit47 ? 1 : 0);                                            // 0x9FAFD8..0x9FAFE4
        return r7;
    }

    /// <summary>
    /// <c>0x9FBE74(node, key, out, ctxD0)</c> (called at <c>0x9FFC68</c>; R3.1): the walk to the top node, then <c>0x9FAEE8(top, key, out)</c>; a non-zero <paramref name="ctxD0"/> (<c>[pbi+0xDC]</c>) continues into <c>0x9FB17C</c>.
    /// </summary>
    public void A9FBE74(WwisePlayingInstance pbi, WwiseNode node)
    {
        var top = TopPositioningNode(node);                                           // 0x9FBE84..0x9FBEB0
        A9FAEE8(top, pbi);                                                            // 0x9FBEBC
        if (pbi.CtxD0 is not null)                                                    // 0x9FBEC0 cmp r5,#0
            (Seams.A9FB17C ?? throw new WwiseMissingBehaviourException("M6-025 R3.1: 0x9FB17C (0x9FBED4) runs when [pbi+0xDC] != 0 and is unread"))(pbi);
    }

    /// <summary>
    /// <c>0x9FB9B8(node, &amp;ctxD0, key, &amp;a, &amp;b, pan)</c> (P8): the top node's <c>[+0x46]</c> gives <c>b = ([+0x46] &gt;&gt; 5) &amp; 3</c> and <c>a = ([+0x46] &gt;&gt; 3) &amp; 3</c>; the ctor value is 0x21 (<c>0x9F4098..0x9F40B0</c>)
    /// and a positioning byte with bits 0 and 1 clears bits 3..4 (<c>0x9ECF90</c>), so the result is (a = 0, b = 1). With <c>[top+0x2C] != 0</c> the 0x40-byte positioning block is copied and <c>[ctx+0xD0]</c> allocated; not representable. An RTPC
    /// parameter 0x17 on the top node replaces <c>a</c> with the <c>0xA11590</c> result converted <c>vcvt.u32.f32</c>. Then <c>0x9FAEE8(top, key, pan)</c>.
    /// </summary>
    public (uint A, uint B) A9FB9B8(WwisePlayingInstance pbi, WwiseNode node)
    {
        var top = TopPositioningNode(node);                                           // 0x9FB9C0..0x9FBA2C
        uint b = (0x21u >> 5) & 3;                                                    // 0x9FBA3C ubfx r3,sb,#5,#2
        uint a = (0x21u >> 3) & 3;                                                    // 0x9FBA7C ubfx sb,sb,#3,#2
        if (HasRtpc(top, 0x17))                                                       // 0x9FBA90..0x9FBAA8
            a = FloatToU32(RtpcValue(top, 0x17, pbi));                                          // 0x9FBAC8..0x9FBAD0
        A9FAEE8(top, pbi);                                                            // 0x9FBAE4 b 0x9FAEE8
        return (a, b);
    }

    private static uint FloatToU32(float v) => WwisePlaybackLimiter.FloatToU32(v);

    // ------------------------------------------------------------------ the bus and the effective parameters

    /// <summary>
    /// <c>0x9BDA6C(ctx)</c> (<c>0x9FFAF4</c>): <c>[pbi+0xE9]</c> bit 2 returns 0; otherwise <c>0x9F4BB8([pbi+0xE0])</c> (<see cref="WwiseBusWalk.FirstOutputBus9BDA6C"/>, B2, B3).
    /// </summary>
    private WwiseRoutingNode? FirstBus(WwisePlayingInstance pbi)
    {
        if ((pbi.Flags0E9 & 4) != 0) return null;                                     // 0x9BDA6C..0x9BDA74, 0x9BDA80
        var node = pbi.NodeE0 ?? throw new WwiseMissingBehaviourException("M6-025 R3.1: [pbi+0xE0] is not set (0x9BDA78 ldr r0,[r0,#0xd4] feeds 0x9F4BB8)");
        var runtime = (RuntimeNodeOf ?? throw new WwiseMissingBehaviourException(
            "M6-025 B3: 0x9F4BB8 walks the runtime node's [+0x38] / [+0x34] links; supply WwisePlayPath.RuntimeNodeOf"))(node)
            ?? throw new WwiseMissingBehaviourException($"M6-025 B3: node {node.Id} has no runtime node");
        return WwiseBusWalk.FirstOutputBus9BDA6C(pbi.Flags0E9, runtime);
    }

    /// <summary>
    /// CalcEffectiveParams <c>0x9FFAD4(pbi, block)</c> (C31.3, R3.1; ctx <c>vt+0x24</c> = <c>0xA000E0</c> -&gt; <c>0x9FFAD4</c>). <paramref name="play"/> is the Play params whose <c>+0x8C</c> address is
    /// <c>r1</c> (the <c>[sp+0x64]</c> of <c>0x9BEB30</c>); null is the <c>r1 = 0</c> of AddSrc's <c>0x9BCA68</c>. Stages: (1) the reset of <c>0x9FFB10..0x9FFB84</c>; (2) the transition-list prune
    /// <c>0x9FFBAC..0x9FFBF8</c>; (3) the bus and <c>pbi+0x64</c> addend; (4) with <c>[pbi+0xE9]</c> bit 2 the early exit <c>0x9FFC30</c>; otherwise the pan values (<c>0x9FBE74</c>), the voice refresh, <c>pbi+0xC4 = 101.0f</c>, the
    /// node's GetAudioParameters, the compose <c>0x9FFD14..0x9FFE18</c>, the priority block <c>0x9FFE1C..0x9FFFE4</c> and the first-time block; both end with <c>1BC |= 1</c> and <c>E8 |= 0x20</c>.
    /// </summary>
    public void CalcEffectiveParams(WwisePlayingInstance pbi, WwisePlayInitParams? play, WwisePlaybackLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        ArgumentNullException.ThrowIfNull(limiter);
        var bus = FirstBus(pbi);                                                       // 0x9FFAF4 bl 0x9BDA6C
        if (play is not null && bus is null && play.Word11C == 0)                          // 0x9FFB04..0x9FFB0C: [r7+0x90] (= params+0x11C, 0 from the Play builder, 0xA62B74) == r6
        {
            // 0x9FFED4: the cached copy runs only with [pbi+0xE9] bit 2 clear (0x9FFEDC bne 0x9FFB10). C34.1: mechanically reachable (first bus null), unreachable on shipped data (every shipped Sound has a first
            // output bus); its body (B12) is not adopted, so it stays a throwing seam.
            if ((pbi.Flags0E9 & 4) == 0)
                throw new WwiseMissingBehaviourException("M6-025 B12: the cached-block path of CalcEffectiveParams (0x9FFEE4..0xA00000, [r7+0x90] == the first output bus) is mechanically reachable but unreachable on shipped data; its body is not adopted by C34.1");
        }
        Reset9FFB10(pbi);
        PruneTransitions(pbi);

        float s16;
        if (bus is null)                                                               // 0x9FFBFC cmp r6,#0; beq 0x9FFE84
        {
            s16 = 0f;
        }
        else if (bus.A9C54E8())                                                        // 0x9FFC08 bl 0x9C54E8; 0x9FFC0C subs r1,r0,#0; non-zero -> 0x9FFC14 (s16 = 0.0f)
        {
            s16 = 0f;
        }
        else
        {
            s16 = WwiseBusWalk.A9C39DC(bus, 0, 5, Rtpc);                               // 0x9FFEB0..0x9FFEBC: 0x9C39DC(r6, 0, 5)
        }
        // r6 is 0 from here on (0x9FFC18 mov r6,#0 / 0x9FFEB8 mov r6,r1).
        if ((pbi.Flags0E9 & 4) != 0)                                                   // 0x9FFC28 cmp r5,#0; beq 0x9FFC54
        {
            pbi.Flags1BC |= 1;                                                         // 0x9FFC30..0x9FFC48
            pbi.Flags0E8 |= 0x20;
            return;
        }

        var node = pbi.NodeE0 ?? throw new WwiseMissingBehaviourException("M6-025 R3.1: [pbi+0xE0] is not set (0x9FFC58 ldr r0,[r4,#0xe0])");
        A9FBE74(pbi, node);                                                            // 0x9FFC68
        if (pbi.Field154 is { } voice) RefreshVoiceVt6C(voice);                        // 0x9FFC6C..0x9FFC80: voice vt+0x6C = 0xA5335C
        pbi.FieldC4 = BitConverter.Int32BitsToSingle(0x42CA0000);                     // 0x9FFC90 movt r3,#0x42ca; 0x9FFC9C str r3,[r4,#0xc4]
        uint mask = (pbi.Flags0E9 & 8) != 0 ? 0xFFFFFFFFu : 0xFFFFFFDFu;               // 0x9FFC94..0x9FFCA8
        var local = new WwiseAcLocalBlock();                                           // 0x9FFCB0, 0x9FFCC4, 0x9FFCC8: [sp+0x2C..0x34] = 0
        WwisePlayInitParams? paramsArg = play;                                         // 0x9FFCB4 addne r7,r7,#0x7c (params+0x108)
        WwiseAcLocalBlock? localArg = null;
        if ((pbi.Flags0E8 & 0x40) == 0) { paramsArg = null; localArg = local; }        // 0x9FFCB8..0x9FFCBC tst r1,#0x40; addeq r7,sp,#0x2c
        // node != r6 (= 0): always, node is non-null.
        pbi.Byte95 = 0;                                                                // 0x9FFCE0, 0x9FFCE8
        pbi.Byte96 = 0;
        var ranges = (pbi.Flags1BC & 1) == 0 ? pbi.Ranges118 : null;                   // 0x9FFCE4, 0x9FFCF0
        (Seams.NodeVtAC ?? throw new WwiseMissingBehaviourException(
            "M6-025 R3.1: node vt+0xAC (GetAudioParameters, 0x9FFD0C) is the M6-010 model and is not wired to the PBI's parameter block; supply WwisePlaySeams.NodeVtAC"))(
            new WwiseVtAcArgs(pbi, node, mask, ranges, paramsArg, localArg));

        // 0x9FFD14..0x9FFD78: the compose.
        float s14 = pbi.Lpf48 + pbi.Ranges118.LowPass;                                 // [0x48] + [0x124]
        float s15 = pbi.Hpf4C + pbi.Ranges118.HighPass;                                // [0x4C] + [0x128]
        pbi.Field9C = s14;                                                             // 0x9FFD48
        s14 = s14 + pbi.FieldA0;                                                       // 0x9FFD54
        pbi.FieldA4 = s15;                                                             // 0x9FFD58
        float s13 = pbi.Volume3C;                                                      // 0x9FFD5C
        pbi.Field98 = s13;                                                             // 0x9FFD60
        s15 = s15 + pbi.FieldA8;                                                       // 0x9FFD64
        pbi.Lpf48 = s14;                                                               // 0x9FFD68
        float pitch = pbi.Pitch44 + pbi.Ranges118.Pitch;                               // 0x9FFD6C
        pbi.Hpf4C = s15;                                                               // 0x9FFD70
        pbi.Pitch44 = pitch;                                                           // 0x9FFD74
        float product = TransitionProduct(pbi);                                        // 0x9FFD7C..0x9FFDC0 (1.0f when the list is empty, 0x9FFFE8)
        product = product * pbi.Fade168;                                               // 0x9FFDE8
        pbi.Flags0E9 = (byte)(pbi.Flags0E9 & ~1);                                      // 0x9FFDE0 bfc r3,#0,#1
        product = product * pbi.Fade16C;                                               // 0x9FFDF8
        float w64 = pbi.Word64 ?? 0f;
        pbi.Word64 = w64 + s16;                                                        // 0x9FFDFC, 0x9FFE04
        pbi.Volume3C = s13 + pbi.Ranges118.Volume;                                     // 0x9FFE00, 0x9FFE0C
        pbi.MuteFade40 = product <= 0f || float.IsNaN(product) ? 0f : product;         // 0x9FFE08..0x9FFE18: vcmpe; vmovle (also for an unordered compare)

        limiter.PriorityRefresh9FFE1C(pbi);                                            // 0x9FFE1C..0x9FFFE4 (8.4)
        if ((pbi.Flags0E8 & 0x40) == 0)                                                // 0x9FFE50..0x9FFE58
            ConsumeModulatorsA01918(pbi, local);                                       // 0x9FFF24..0x9FFFA4: the body of 0xA01918 on r7 = the local block
        if (local.Word0 != 0) local.Word4 = 0;                                         // 0x9FFE5C..0x9FFE7C: the object node vt+0xAC allocated is freed
        pbi.Flags1BC |= 1;                                                             // 0x9FFC30
        pbi.Flags0E8 |= 0x20;
    }

    /// <summary>The reset stage <c>0x9FFB10..0x9FFB84</c>: <c>[+0x58]</c> and <c>[+0x60]</c> bits 0..1 clear; <c>+0x40 = 1.0f</c>; <c>+0x3C</c>, <c>+0x44..+0x54</c>, <c>+0x5C</c>, <c>+0x64..+0x6C</c> zero; <c>+0x70</c> and <c>+0x80</c> (16 bytes each) zero; <c>+0x90</c> and <c>+0x94..+0x97</c> zero.</summary>
    private static void Reset9FFB10(WwisePlayingInstance pbi)
    {
        pbi.Byte58 = (byte)(pbi.Byte58 & 0xFC);                                        // 0x9FFB20, 0x9FFB2C
        pbi.Byte60 = (byte)(pbi.Byte60 & 0xFC);                                        // 0x9FFB28, 0x9FFB30
        pbi.MuteFade40 = 1f;                                                           // 0x9FFB24
        pbi.Volume3C = 0f;                                                             // 0x9FFB48
        pbi.Pitch44 = 0f; pbi.Lpf48 = 0f; pbi.Hpf4C = 0f;                              // 0x9FFB4C..0x9FFB54
        pbi.Field50 = 0f; pbi.Field54 = 0f; pbi.Field5C = 0f;                          // 0x9FFB58..0x9FFB60
        pbi.Word64 = 0f; pbi.Field68 = 0f; pbi.Field6C = 0f;                           // 0x9FFB64..0x9FFB6C
        Array.Clear(pbi.Block70);                                                      // 0x9FFB70
        Array.Clear(pbi.Block80);                                                      // 0x9FFB80
        pbi.Word90 = 0;                                                                // 0x9FFB90
        pbi.Byte94 = pbi.Byte95 = pbi.Byte96 = pbi.Byte97 = 0;                         // 0x9FFB98..0x9FFBA8
    }

    /// <summary>
    /// The prune <c>0x9FFBAC..0x9FFBF8</c>: a record whose flag byte has bit 1 clear is replaced by the last record (when more than one remains) and the count drops; the same index is tested again.
    /// </summary>
    private static void PruneTransitions(WwisePlayingInstance pbi)
    {
        var list = pbi.Transitions10C;
        int i = 0;
        while (i != list.Count)                                                         // 0x9FFBB4 cmp ip,r3; beq 0x9FFBFC
        {
            if ((list[i].Flags4 & 2) != 0) { i++; continue; }                           // 0x9FFBBC..0x9FFBC8
            if (list.Count > 1) list[i] = list[^1];                                     // 0x9FFBCC..0x9FFBDC ldmdbhi / stmhi
            list.RemoveAt(list.Count - 1);                                              // 0x9FFBEC str r5,[r4,#0x110]
        }
    }

    /// <summary>The product of the transition records' values (<c>0x9FFD7C..0x9FFDC0</c>, <c>0x9FF3B0..0x9FF3C0</c>): 1.0f times each record's <c>+8</c> in order.</summary>
    private static float TransitionProduct(WwisePlayingInstance pbi)
    {
        float s15 = 1f;                                                                 // 0x9FFD84 vmov.f32 s15,#1.0
        foreach (var r in pbi.Transitions10C) s15 = s15 * r.Value8;                     // vmul.f32 s15,s15,s14
        return s15;
    }

    /// <summary>
    /// The voice's <c>vt+0x6C</c> = <c>0xA5335C</c> (R2.12): for slots 0..3 the voice's <c>vt+0x70</c> (<c>0xA5338C</c>) does nothing when <c>[voice+0xD4]</c> is null or the slot is empty; a filled slot needs the node's
    /// <c>vt+0xE8</c> (<c>0x9EEF2C</c>, R2.6) and its bypass byte store, which this model does not hold, so it throws.
    /// </summary>
    private static void RefreshVoiceVt6C(WwiseLiveVoice voice)
    {
        for (int i = 0; i < 4; i++)                                                     // 0xA5335C: i = 0..3
        {
            if (voice.Source is null) continue;                                          // 0xA5338C: [voice+0xD4] != 0
            if (voice.InsertFxSlots[i] is null) continue;                               //           [voice+0x370+4i] != 0
            throw new WwiseMissingBehaviourException(
                "M6-025 R2.12: voice vt+0x70 (0xA5338C) with a filled insert-FX slot calls node vt+0xE8 (0x9EEF2C) and stores the bypass byte; that path is not modelled");
        }
    }

    // ------------------------------------------------------------------ 0x9FF368 and 0x9BCA68

    /// <summary>
    /// The partial recompute <c>0x9FF368(pbi)</c> (ctx <c>vt+0x28</c> = <c>0x9FF414</c>, R3.1): the product of the transition records (1.0f when none) times <c>pbi+0x168</c> times <c>pbi+0x16C</c>; <c>[pbi+0xE9]</c> bit 0 is cleared;
    /// <c>pbi+0x3C = pbi+0x98 + pbi+0x118</c>; <c>pbi+0x40 = max(product, 0)</c> (0 for NaN, <c>vmovle</c>).
    /// </summary>
    public static void Recompute9FF368(WwisePlayingInstance pbi)
    {
        float s15 = TransitionProduct(pbi);                                             // 0x9FF37C..0x9FF3C0 / 0x9FF408
        s15 = s15 * pbi.Fade168;                                                        // 0x9FF3D8
        pbi.Flags0E9 = (byte)(pbi.Flags0E9 & ~1);                                       // 0x9FF3D0
        s15 = s15 * pbi.Fade16C;                                                        // 0x9FF3E8
        pbi.Volume3C = pbi.Field98 + pbi.Ranges118.Volume;                              // 0x9FF3EC, 0x9FF3F4
        pbi.MuteFade40 = s15 <= 0f || float.IsNaN(s15) ? 0f : s15;                      // 0x9FF3F0..0x9FF400
    }

    /// <summary>
    /// <c>0x9BCA68(ctx, r1)</c> (R3.1, R3.2, C29.4): with <c>[pbi+0xE8]</c> bit 5 clear the ctx <c>vt+0x24</c> (CalcEffectiveParams, <c>r1 = 0</c> from AddSrc); with bit 5 set and <c>[pbi+0xE9]</c> bit 0 set <c>vt+0x28</c>
    /// (<see cref="Recompute9FF368"/>). Then the below-audibility test: <c>(lin(pbi+0x3C) * pbi+0x40) * lin(pbi+0x64) &lt;= [0x1052454]</c> (0x37800000), 1 when so, 0 otherwise (also for NaN, <c>movls</c> / <c>movhi</c>).
    /// </summary>
    public int A9BCA68(WwisePlayingInstance pbi, WwisePlaybackLimiter limiter)
    {
        if ((pbi.Flags0E8 & 0x20) == 0)                                                 // 0x9BCA70..0x9BCA78
            CalcEffectiveParams(pbi, null, limiter);                                    // 0x9BCB94..0x9BCB9C: vt+0x24(ctx, r1)
        else if ((pbi.Flags0E9 & 1) != 0)                                               // 0x9BCA7C..0x9BCA84
            Recompute9FF368(pbi);                                                       // 0x9BCB7C..0x9BCB84: vt+0x28
        return WwisePlaybackLimiter.Below9BEB30(pbi) ? 1 : 0;                           // 0x9BCA88..0x9BCB6C
    }

    // ------------------------------------------------------------------ 0x9BEB30

    /// <summary>The result of <c>0x9BEB30</c>: its return value, the <c>below</c> byte it stores through <c>[sp+0x68]</c> and the error code through <c>[sp+0x60]</c> (0x29 with result 3).</summary>
    public readonly record struct InitResult(int Result, bool Below, int Code);

    /// <summary>
    /// <c>0x9BEB30(ctx, priority, r2, flag, ..)</c> (P8) on the shipped path. <c>[ctx+0xD8] = priority</c>; with <c>[ctx+0xDD]</c> bit 2 clear and <c>[ctx+0xDC] &amp; 3 == 1</c> (<c>0x5D</c> at creation) the top node's <c>0x9FB9B8</c> sets
    /// <c>[ctx+0xDC]</c> bits 2..3 and 0..1 (<c>0x9BEDD8..0x9BEE04</c>); with no context object (<c>[ctx+0xD0] == 0</c>) bits 0..1 are cleared and the flow reaches <c>0x9BEB70</c>: CalcEffectiveParams (E8 bit 5 clear) or the partial recompute
    /// (bit 5 set and <c>[ctx+0xDD]</c> bit 0 set), then the below byte. <c>r7 = flag</c> against <c>r6 = 1</c> (unsigned): a flag of 0 takes the below byte as r7, and a non-zero r7 with <c>[ctx+0xDD]</c> bit 2 clear returns 3 with code 0x29.
    /// A context object (<c>[ctx+0xD0] != 0</c>) goes to <c>0x9BE898</c>, which is unread.
    /// </summary>
    public InitResult InitContext9BEB30(WwisePlayingInstance pbi, float priority, uint flag, WwisePlayInitParams play, WwisePlaybackLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        ArgumentNullException.ThrowIfNull(play);
        pbi.FieldE4 = BitConverter.SingleToUInt32Bits(priority);                        // 0x9BEB48 str r1,[r0,#0xd8]
        int r5, r6;
        if ((pbi.Flags0E9 & 4) != 0)                                                    // 0x9BEB4C tst r2,#4; bne 0x9BEBA8
        {
            pbi.PanC0 = 0;                                                              // 0x9BEBBC strb r0,[r4,#0xb4]
            pbi.Flags0E8 = (byte)(pbi.Flags0E8 & ~3);                                   // 0x9BEBC0 bfc r1,#0,#2
            pbi.PanB4 = pbi.PanB8 = pbi.PanBC = 0f;                                     // 0x9BEBC4..0x9BEBD0
            if (pbi.CtxD0 is not null) return CtxObject(pbi);                           // 0x9BEBD4 beq 0x9BEB70 not taken
        }
        else if ((pbi.Flags0E8 & 3) == 1)                                               // 0x9BEB54..0x9BEB60 beq 0x9BEDA8
        {
            if (pbi.CtxD0 is not null) return CtxObject(pbi);                           // 0x9BEDA8..0x9BEDB0
            var node = pbi.NodeE0 ?? throw new WwiseMissingBehaviourException("M6-025 P8: [pbi+0xE0] is not set (0x9BEDB4 ldr r0,[r0,#0xd4])");
            var (a, b) = A9FB9B8(pbi, node);                                            // 0x9BEDD4
            byte dc = pbi.Flags0E8;                                                     // 0x9BEDD8
            dc = (byte)((dc & ~0x0C) | (int)((b & 3) << 2));                            // 0x9BEDE4 bfi r3,r1,#2,#2
            dc = (byte)((dc & ~0x03) | (int)(a & 3));                                   // 0x9BEDF0 bfi r3,r1,#0,#2
            pbi.Flags0E8 = dc;                                                          // 0x9BEDF4
            if (pbi.CtxD0 is null) pbi.Flags0E8 = (byte)(pbi.Flags0E8 & ~3);            // 0x9BEDF8..0x9BEE00 (r2 == 0)
            else return CtxObject(pbi);                                                 // 0x9BEE04 bne 0x9BEBDC
        }
        else if (pbi.CtxD0 is not null) return CtxObject(pbi);                           // 0x9BEB64..0x9BEB6C bne 0x9BEBD8

        // 0x9BEB70
        pbi.Flags0E8 = (byte)(pbi.Flags0E8 & ~3);                                       // 0x9BEB7C bfc r3,#0,#2
        r6 = 1; r5 = 1;                                                                 // 0x9BEB74, 0x9BEB78
        if ((pbi.Flags0E8 & 0x20) == 0)                                                 // 0x9BEB88 tst r3,#0x20
            CalcEffectiveParams(pbi, play, limiter);                                    // 0x9BEB90..0x9BEBA0: ctx vt+0x24(ctx, [sp+0x64])
        else if ((pbi.Flags0E9 & 1) != 0)                                               // 0x9BEC48..0x9BEC50
            Recompute9FF368(pbi);                                                       // 0x9BEC54..0x9BEC60: ctx vt+0x28
        // 0x9BEC64: the below byte.
        bool below = WwisePlaybackLimiter.Below9BEB30(pbi);                             // 0x9BEC64..0x9BED48
        uint r7 = flag;
        r7 = r7 >= (uint)r6 ? 0u : (below ? 1u : 0u);                                   // 0x9BED50..0x9BED58 cmp r7,r6; movhs r7,#0; andlo r7,r3,#1
        if (r7 != 0 && (pbi.Flags0E9 & 4) == 0)                                         // 0x9BED5C..0x9BED6C
            return new InitResult(3, below, 0x29);                                      // 0x9BED74..0x9BED84
        return new InitResult(r5, below, 0);                                            // 0x9BED8C
    }

    private static InitResult CtxObject(WwisePlayingInstance pbi)
        => throw new WwiseMissingBehaviourException(
            "M6-025 P8: [ctx+0xD0] != 0 takes 0x9BE898 (0x9BEBE4) with a context object, whose body (0x9BE8D0..) and the branches after it (0x9BEBF0..0x9BEEC4) are unread; no shipped node allocates the context object (positioning bit 3 is never set)");

    /// <summary>
    /// <c>0x9BE898(ctx, out)</c> with <c>[ctx+0xD0] == 0</c> (C34.2 R10): it always returns 2 and never writes <c>*out</c>. With <c>[ctx+0xDD]</c> bit 2 set it clears <c>[ctx+0xB4]</c> (<c>PanC0</c>), <c>[ctx+0xA8..0xB0]</c> (<c>PanB4..PanBC</c>) and
    /// <c>[ctx+0xDC] &amp;= ~3</c> (<c>0x9BE948..0x9BE974</c>); with bit 2 clear and <c>[ctx+0xDC] &amp; 3 == 1</c> it runs <c>0x9FB9B8</c> (which sets <c>[ctx+0xDC]</c> bits 2..3 from <c>(byte46 &gt;&gt; 5) &amp; 3</c> and bits 0..1 from
    /// <c>(byte46 &gt;&gt; 3) &amp; 3</c>, then clears bits 0..1 when <c>[ctx+0xD0]</c> is still 0, <c>0x9BE984..0x9BE9E0</c>); otherwise nothing (<c>0x9BE8C8 -&gt; 0x9BE978</c>). A context object (<c>[ctx+0xD0] != 0</c>) takes <c>0x9BE8D0</c>, which is unread.
    /// The callers that read its out-pointer (<c>0x9FF904</c>, <c>0xA01A64</c>) are not reached from the C# graph (C34.2 R11: their reach is UNKNOWN).
    /// </summary>
    public int A9BE898(WwisePlayingInstance pbi)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        if ((pbi.Flags0E9 & 4) != 0)                                                    // 0x9BE8AC tst r3,#4; bne 0x9BE948
        {
            pbi.PanC0 = 0;                                                              // 0x9BE95C strb r1,[r0,#0xb4]
            pbi.Flags0E8 = (byte)(pbi.Flags0E8 & ~3);                                   // 0x9BE960 bfc r2,#0,#2
            pbi.PanB4 = pbi.PanB8 = pbi.PanBC = 0f;                                     // 0x9BE964..0x9BE970
            if (pbi.CtxD0 is not null) return CtxObject(pbi).Result;                    // 0x9BE974 bne 0x9BE8D0
            return 2;                                                                   // 0x9BE978
        }
        if ((pbi.Flags0E8 & 3) == 1)                                                    // 0x9BE8BC cmp r3,#1; beq 0x9BE984
        {
            if (pbi.CtxD0 is not null) return CtxObject(pbi).Result;                    // 0x9BE984 cmp r4,#0; bne 0x9BE8D0
            var node = pbi.NodeE0 ?? throw new WwiseMissingBehaviourException("M6-025 R10: [pbi+0xE0] is not set (0x9BE98C ldr r0,[r0,#0xd4])");
            var (a, b) = A9FB9B8(pbi, node);                                            // 0x9BE9AC bl 0x9FB9B8
            byte dc = pbi.Flags0E8;                                                     // 0x9BE9B0
            dc = (byte)((dc & ~0x0C) | (int)((b & 3) << 2));                            // 0x9BE9BC bfi r3,r2,#2,#2
            dc = (byte)((dc & ~0x03) | (int)(a & 3));                                   // 0x9BE9C8 bfi r3,r2,#0,#2
            pbi.Flags0E8 = dc;                                                          // 0x9BE9CC
            if (pbi.CtxD0 is not null) return CtxObject(pbi).Result;                    // 0x9BE9DC bne 0x9BE8D0
            pbi.Flags0E8 = (byte)(pbi.Flags0E8 & ~3);                                   // 0x9BE9D0..0x9BE9D8 (r4 == 0)
            return 2;                                                                   // 0x9BE9E0 b 0x9BE978
        }
        if (pbi.CtxD0 is not null) return CtxObject(pbi).Result;                        // 0x9BE8C8 cmp r4,#0; beq 0x9BE978
        return 2;                                                                       // 0x9BE978
    }

    // ------------------------------------------------------------------ the modulator list (C34.2 R6, R7, 0xA01918)

    /// <summary>
    /// <c>0xA01918(pbi, outVec, 1)</c> (the third argument is not read), and the identical body CalcEffectiveParams runs at <c>0x9FFF24..0x9FFFA4</c> with <c>r7</c> = the local block: <c>[pbi+0x34] != 0</c> runs <c>0x9E8224([pbi+0x34])</c>; a non-zero
    /// count <c>[vec+4]</c> builds the 8-word record <c>{[pbi+0x14], [pbi+0x1E4], [pbi+0x1E8], [pbi+0x1C], [pbi+0x140], [pbi+0x1D8], ([pbi+0x1BF] &gt;&gt; 2) &amp; 1, pbi}</c> and calls <c>0x9E62AC(*0x10400E8, vec, rec, pbi+0x34)</c>; then
    /// <c>[pbi+0xE8] |= 0x40</c>. <c>[pbi+0xE8]</c> starts <c>0x5D</c> (bit 6 set), so on the first Play this runs from <c>0xA38044</c> (<c>r1 = params+0x108</c>), not from the CalcEffectiveParams block (C34.2, verification correction 4).
    /// </summary>
    public void ConsumeModulatorsA01918(WwisePlayingInstance pbi, WwiseAcLocalBlock vec)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        ArgumentNullException.ThrowIfNull(vec);
        if (pbi.Field34 != 0)                                                           // 0xA01920..0xA01934 / 0x9FFF24..0x9FFF30
            WwiseModulatorManager.A9E8224((Seams.RecordsOf34 ?? throw new WwiseMissingBehaviourException(
                "M6-025 R6: the list at [pbi+0x34] is not modelled; supply WwisePlaySeams.RecordsOf34"))(pbi));
        if (vec.Word4 != 0)                                                             // 0xA01938..0xA01940 / 0x9FFF34..0x9FFF3C
        {
            var (w1E4, w1E8, w1C) = (Seams.ModulatorCtxWords ?? throw new WwiseMissingBehaviourException(
                "M6-025 R7: the words [pbi+0x1E4], [pbi+0x1E8], [pbi+0x1C] of the modulator context record have no adopted writer; supply WwisePlaySeams.ModulatorCtxWords"))(pbi);
            var ctx = new WwiseModulatorCtxRec(pbi.GameObject14 ?? 0u, w1E4, w1E8, w1C, pbi.PlayingId, pbi.StartOffset, (uint)((pbi.Flags1BF >> 2) & 1), pbi);   // 0xA0194C..0xA01998
            (Modulators ?? throw new WwiseMissingBehaviourException(
                "M6-025 R7: 0x9E62AC takes the modulator manager *0x10400E8; supply WwisePlayPath.Modulators")).A9E62AC(vec.InUse, ctx, pbi);   // 0xA0199C (r3 = pbi+0x34)
        }
        pbi.Flags0E8 |= 0x40;                                                           // 0xA019A0..0xA019A8 / 0x9FFF9C..0x9FFFA4
    }

    // ------------------------------------------------------------------ 0xA00618 and 0xA1E280

    /// <summary>
    /// <c>0xA1E280(node)</c> (P9): the loop count. Property 0x3A of the node's property list gives the base (default 1; the raw u32); the ranged list's property 0x3A gives <c>(min, max)</c> as integers; with <c>max != min</c> the global LCG advances
    /// (<c>x * 0x5851F42D4C957F2D + 1</c>) and <c>trunc_s32(0.5 + ((double)(int)(hi32 &gt;&gt; 1) / 2147483647.0) * (double)(max - min))</c> is added; the result is <c>sxth(base + min + draw)</c>.
    /// </summary>
    public short LoopCountA1E280(WwiseNode node)
    {
        int r5 = 1;                                                                     // 0xA1E28C mov r5,#1
        if (node.Params.Props.TryGetValue(0x3A, out var baseValue)) r5 = unchecked((int)baseValue);   // 0xA1E2C0..0xA1E2DC
        if (node.Params.RangedProps.TryGetValue(0x3A, out var range))                   // 0xA1E2E0..0xA1E32C
        {
            int min = BitConverter.SingleToInt32Bits(range.Min);                        // 0xA1E32C ldr r2,[r4,r3]
            int max = BitConverter.SingleToInt32Bits(range.Max);                        // 0xA1E330 ldr r3,[r1,#4]
            int diff = unchecked(max - min);                                            // 0xA1E334 subs r3,r3,r2
            int draw = 0;
            if (diff != 0)
            {
                uint h = (Rng ?? throw new WwiseMissingBehaviourException(
                    "M6-025 P9: the Sound has a ranged loop count; 0xA1E280 draws from the global LCG (0x108D868), which is not supplied (WwisePlayPath.Rng)")).Next();   // 0xA1E364..0xA1E384
                double d17 = (double)(int)h / BitConverter.Int64BitsToDouble(0x41DFFFFFFFC00000L);   // 0xA1E390 vdiv.f64 with the double at 0xA1E3C0
                double d18 = 0.5 + d17 * (double)diff;                                  // 0xA1E394 vmla.f64
                draw = TruncS32(d18);                                                   // 0xA1E398 vcvt.s32.f64
            }
            r5 = unchecked(r5 + unchecked(min + draw));                                 // 0xA1E3A0..0xA1E3A4
        }
        return unchecked((short)r5);                                                    // 0xA1E3A8 sxth r0,r5
    }

    private static int TruncS32(double v)
    {
        if (double.IsNaN(v)) return 0;
        if (v >= 2147483648.0) return int.MaxValue;
        if (v <= -2147483649.0) return int.MinValue;
        return (int)v;
    }

    /// <summary>
    /// <c>0xA00618(pbi)</c> (P9): <c>[pbi+0x1B8] = u16(0xA1E280([pbi+0xE0]))</c> on every call; when <c>[pbi+0x1BD]</c> bit 0 is set it returns, otherwise it sets the bit and, with <c>[pbi+0xAC] != 0</c>, tail-calls
    /// <c>0x9FF0D8(*0x108D8E8, [pbi+0xAC], 0x9FB994(node))</c> (unread).
    /// </summary>
    public void BeforePlayA00618(WwisePlayingInstance pbi)
    {
        var node = pbi.NodeE0 ?? throw new WwiseMissingBehaviourException("M6-025 P9: [pbi+0xE0] is not set (0xA00620)");
        pbi.LoopCount1B8 = unchecked((ushort)LoopCountA1E280(node));                    // 0xA00624, 0xA00634 strh r0,[r2]
        if ((pbi.Flags1BD & 1) != 0) return;                                            // 0xA00630 tst r3,#1; popne
        pbi.Flags1BD |= 1;                                                              // 0xA00640..0xA00644
        if (pbi.Field0AC is null) return;                                               // 0xA00648..0xA0064C
        (Seams.A9FF0D8 ?? throw new WwiseMissingBehaviourException(
            "M6-025 P9: [pbi+0xAC] != 0 reaches 0x9FF0D8 (0xA00674), which is unread; supply WwisePlaySeams.A9FF0D8"))(pbi);
    }

    // ------------------------------------------------------------------ 0xA0067C

    /// <summary>
    /// <c>0xA0067C(pbi, fade, flagStart, flagTail)</c> (P10, P11, C32.1; the Play passes <c>params+0xC</c>, <c>params+0x70 == 1</c> and 0). The fade time word <c>[fade]</c> decides: non-zero (as bits, so -0.0f counts) sets <c>pbi+0x168 = 0.0f</c> and, with no
    /// transition at <c>pbi+0x144</c>, creates one (<c>0xA36268(mgr, {pbi+8, 0x1000000, 0.0f, 1.0f, time, curve, 0, 1, 0}, 1, 0)</c>, <c>1BE |= 0x40</c>, <c>[pbi+0x144] = item</c>, PBI <c>vt+0x50(pbi, 0xE, time)</c> = <c>0x9FF4D0</c> (a return),
    /// and <c>vt+0x14(pbi, 0x1000000, 1.0f, 1)</c> when the create returned 0) or re-aims the existing one (<c>0xA366F4</c>). Then the start list: <c>flagStart</c> set or <c>[pbi+0x1BA] &amp; 7 == 1</c> marks <c>1BC |= 0x80</c>, appends with kind 1 and runs
    /// <c>0xA366AC</c> (when a transition exists), <c>0x9BDA28(ctx, 1)</c> and <c>0x9E808C([pbi+0x34])</c> (when non-null); otherwise it appends with kind 0. A result other than 1 is returned; then <c>[pbi+0x1BA] &amp; 7 == 2</c> runs PBI <c>vt+0</c>
    /// (<c>0x9FF7B8(pbi, 0, 0)</c>) and a non-zero <paramref name="flagTail"/> increments the global counter.
    /// </summary>
    public int PbiPlayA0067C(WwisePlayingInstance pbi, WwiseFadeInTransition? fade, bool flagStart, bool flagTail,
        WwiseStartList startList, long tick, WwisePlaybackLimiter limiter, Action<WwisePlayingInstance>? onGlobalCounter = null)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        ArgumentNullException.ThrowIfNull(startList);
        ArgumentNullException.ThrowIfNull(limiter);
        uint timeBits = fade is null ? 0u : BitConverter.SingleToUInt32Bits(fade.FadeInTime);   // 0xA00684 ldr r5,[r1]
        uint curve = fade is null ? 0u : fade.FadeCurve;                                         // 0xA0077C ldr ip,[r1,#4]
        if (timeBits != 0)                                                              // 0xA00694 cmp r5,#0; bne 0xA00774
        {
            pbi.Fade168 = 0f;                                                           // 0xA00788 str r3,[r4,#0x168]
            var mgr = Transitions ?? throw new WwiseMissingBehaviourException(
                "M6-025 P10: a fade-in needs the transition manager (*0x108D870); set WwisePlayPath.Transitions");
            if (pbi.Field144 == 0)                                                      // 0xA00780 cmp r2,#0; beq 0xA007B8
            {
                var info = new WwiseTransitionInfo(pbi, 0x1000000, 0f, 1f, timeBits, curve, 0, 1, 0);   // 0xA007B8..0xA007F4
                var item = mgr.Create0A36268(info, true, 0);                            // 0xA00800 bl 0xA36268(mgr, &info, 1, 0)
                pbi.Flags1BE |= 0x40;                                                   // 0xA00814
                pbi.Field144 = item?.Id ?? 0;                                           // 0xA00824
                if (item is null)                                                       // 0xA00830 cmp sl,#0; bne 0xA006A4
                    (Seams.PbiVt14 ?? throw new WwiseMissingBehaviourException(
                        "M6-025 P10: PBI vt+0x14(pbi, 0x1000000, 1.0f, 1) (0xA00850), run when the transition create returned 0, is unread; supply WwisePlaySeams.PbiVt14"))(pbi);
            }
            else
            {
                (limiter.ReaimTransitionA366F4 ?? throw new WwiseMissingBehaviourException(
                    "M6-025 P10: 0xA366F4 (the re-aim of the running transition, 0xA007B0) is unread; supply WwisePlaybackLimiter.ReaimTransitionA366F4"))(
                    pbi, 0x1000000u, 1f, unchecked((uint)timeBits), unchecked((int)curve), 0);
            }
        }

        int r5;
        if (flagStart || (pbi.Flags1BA & 7) == 1)                                       // 0xA006A4..0xA006B8 -> 0xA006E0
        {
            pbi.Flags1BC |= 0x80;                                                       // 0xA006EC
            r5 = startList.Enqueue(1, pbi, tick);                                       // 0xA006F4 bl 0x9D3558(1, pbi)
            if (pbi.Field144 != 0)                                                      // 0xA006F8..0xA00704
                (Transitions ?? throw new WwiseMissingBehaviourException("M6-025 P10: 0xA366AC needs the transition manager"))
                    .Toggle366AC(pbi.Field144);                                         // 0xA00714 bl 0xA366AC
            A9BDA28(pbi, true);                                                         // 0xA0071C..0xA00720
            if (pbi.Field34 != 0)                                                       // 0xA00724..0xA0072C
                A9E808C(pbi);                                                           // 0xA00730 bl 0x9E808C
            if (r5 != 1) return r5;                                                     // 0xA00734 cmp r5,#1; bne 0xA006D4
        }
        else
        {
            r5 = startList.Enqueue(0, pbi, tick);                                       // 0xA006C0..0xA006C4: 0x9D3558(0, pbi)
            if (r5 != 1) return r5;                                                     // 0xA006CC..0xA006D4
        }
        if ((pbi.Flags1BA & 7) == 2)                                                    // 0xA0073C..0xA00748
            limiter.MarkStopped9FF7B8(pbi, 0, 0);                                       // 0xA00858..0xA0086C: PBI vt+0(pbi, 0, 0)
        if (flagTail) onGlobalCounter?.Invoke(pbi);                                     // 0xA0074C..0xA00768: [0x1052440 - ..]++
        return r5;                                                                      // 0xA00758 mov r0,r5
    }

    /// <summary><c>0x9BDA28(ctx, flag)</c> (P12): returns at once when <c>[ctx+0xA0]</c> is 0; otherwise <c>0x9FF2CC</c> (flag 0) or <c>0x9FF290</c> (flag 1), whose bodies call the unread <c>0x9FD8D0</c> / <c>0x9FD8C0</c>.</summary>
    public void A9BDA28(WwisePlayingInstance pbi, bool flag)
    {
        if (pbi.Field0AC is null) return;                                               // 0x9BDA28..0x9BDA30
        (Seams.A9FD8C0 ?? throw new WwiseMissingBehaviourException(
            "M6-025 P12: [ctx+0xA0] != 0 reaches 0x9FD8C0 / 0x9FD8D0 (0x9FF2BC, 0x9FF2F8), which are unread; supply WwisePlaySeams.A9FD8C0"))(pbi, flag);
    }

    /// <summary>
    /// <c>0x9E808C(head)</c> (P12, with the verifier's correction): every record of the list at <c>[pbi+0x34]</c> gets <c>[rec+0x58]++</c>; a record whose state <c>[rec+0x48]</c> is not 3 and whose counter reached its limit
    /// (<c>[rec+0x58] &gt;= [rec+0x50]</c>, signed) with <c>[rec+0x30] != 0</c> stores <c>[[rec+0x30]+8]</c> to <c>[rec+0x4C]</c> and clears <c>[rec+0x30]</c>.
    /// </summary>
    public void A9E808C(WwisePlayingInstance pbi)
    {
        var records = (Seams.RecordsOf34 ?? throw new WwiseMissingBehaviourException(
            "M6-025 P12: the list at [pbi+0x34] is not modelled; supply WwisePlaySeams.RecordsOf34"))(pbi);
        foreach (var rec in records)                                                    // 0x9E809C..0x9E80E0
        {
            rec.Counter58 = unchecked(rec.Counter58 + 1);                               // 0x9E80AC..0x9E80B0
            if (rec.State48 == 3) continue;                                             // 0x9E80A8 cmp r0,#3; beq 0x9E80D8
            if (!(rec.Counter58 >= rec.Limit50)) continue;                              // 0x9E80BC..0x9E80C0 blt 0x9E80D8
            if (rec.Object30Word8 is { } w)                                             // 0x9E80C4..0x9E80D4
            {
                rec.Word4C = w;                                                         //   [rec+0x4C] = [[rec+0x30]+8]
                rec.Object30Word8 = null;                                               //   [rec+0x30] = 0
            }
        }
    }
}
