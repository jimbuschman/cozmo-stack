// fidelity: M6-025, M6-010, M6-001
namespace Cozmo.Robot.Animation.Wwise;

// The bus walk of CalcEffectiveParams (M6-wwise-bank.md C34.1, rows B1..B9, B11, B17 and the bus parts of B4..B6 of research/20261003-B-M6b-4-live-bodies-4.md with its verification's corrections).
// Every function below was disassembled in libcozmoEngine.so (ARM) when this file was written; addresses are in the comments.
//
// Production entry. Engine: CalcEffectiveParams 0x9FFAD4 (the ctx vt+0x24 = 0xA000E0 the Play path calls at 0x9BEB9C / 0xA38140) calls 0x9BDA6C (0x9FFAF4), 0x9C54E8 (0x9FFC08) and 0x9C39DC (0x9FFEBC);
// the voice linker's 0xA68A44 calls 0x9C39DC for a line's bus (0xA68A54). The C# counterparts are WwisePlayPath.CalcEffectiveParams (-> WwiseBusWalk.FirstOutputBus9BDA6C, WwiseRoutingNode.A9C54E8,
// WwiseBusWalk.A9C39DC) and WwiseVoiceLinker.AddInput (-> WwiseBusWalk.A9C39DC). The node graph is the host's (WwiseRoutingNode, [node+0x34] parent / [node+0x38] output bus); no production code builds
// one yet (the bank-load and Play wiring, C30.W, is parked), so these functions are exercised by tests, with emu_bus.py as the oracle.
//
// Replaces: the test-double seams WwisePlaySeams.FirstOutputBus9F4BB8 / BusFlag9C54E8 / BusVolume9C39DC and WwiseVoiceLinkSeams.BusVolumeParam5 (two seams for the one body 0x9C39DC).
//
// Unread, named (throw WwiseMissingBehaviourException when reached): the bus constructor's callees 0x9F402C / 0xA19F94 / 0x9F40F4, the FX slot change callees (bus vt+0xC4, vt+0x8C), 0x9F5C30, the
// mixer record 0x9C0FC0 (not adopted by C34.1), 0xA4454C, 0x9C62AC.

/// <summary>
/// A bundle as the node stores it: <c>u8 count</c>, <c>count</c> id bytes, then the entries from <c>(count + 4) &amp; ~3</c> (4-byte entries for the base bundle <c>[node+0x3C]</c>, 8-byte entries for the ranged
/// bundle and the state-list bundles). Every reader this file ports (<c>0x9C39DC</c>, <c>0x9F9CDC</c>) reads the FIRST float of the entry whose id byte equals the parameter, so an entry is kept as that word.
/// </summary>
public sealed class WwiseParamBundle
{
    private readonly byte[] _ids;
    private readonly uint[] _firstWords;

    /// <param name="ids">The id bytes, in the bundle's order.</param>
    /// <param name="firstWords">The first 32-bit word of each entry (a float's bits), in the same order.</param>
    public WwiseParamBundle(IReadOnlyList<byte> ids, IReadOnlyList<uint> firstWords)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(firstWords);
        if (ids.Count != firstWords.Count) throw new ArgumentException("one entry per id");
        if (ids.Count > 255) throw new ArgumentException("the count is a byte");
        _ids = ids.ToArray();
        _firstWords = firstWords.ToArray();
    }

    /// <summary>The bundle's count byte.</summary>
    public int Count => _ids.Length;

    /// <summary>The bundle of a parsed property dictionary (the bank order of a dictionary built without removals).</summary>
    public static WwiseParamBundle FromProps(IReadOnlyDictionary<byte, uint> props)
    {
        ArgumentNullException.ThrowIfNull(props);
        return new WwiseParamBundle(props.Keys.ToArray(), props.Values.ToArray());
    }

    /// <summary>
    /// The scan every bundle reader makes (<c>0x9C3A98..0x9C3A8C</c>, <c>0x9C3B38..0x9C3B2C</c>, <c>0x9F9D40..0x9F9D34</c>): id bytes are read from index 0 up and the first byte equal to
    /// <paramref name="id"/> selects its entry; the scan gives up when the index reaches the count. A count of 0 still reads the byte after the count (indeterminate memory), which is not modelled, so it throws.
    /// </summary>
    public uint? Find(byte id)
    {
        if (_ids.Length == 0)
            throw new WwiseMissingBehaviourException("M6-025 B8/B9: a bundle with count 0 makes the scan read the byte after the count byte (indeterminate memory); no shipped bundle has count 0");
        for (int i = 0; i < _ids.Length; i++)
            if (_ids[i] == id) return _firstWords[i];
        return null;
    }
}

/// <summary>One item of a node's state list <c>[node+0x18]</c> (<c>0x9F9CDC</c>): the bundle at <c>[item+0x10]</c> (null for a zero pointer); the list's next link is the list order.</summary>
public sealed class WwiseStateListItem
{
    /// <summary><c>[item+0x10]</c>.</summary>
    public WwiseParamBundle? Bundle10 { get; init; }
}

/// <summary>
/// The FX chunk of a bus (<c>[bus+0x28]</c>, 0x28 bytes, lazily allocated by <c>0x9F5760</c>): <c>+0</c> an int (the version), four slots of 8 bytes with the id at <c>+4 + 8 * slot</c> and the share byte at
/// <c>+9 + 8 * slot</c> (<c>0x9F57FC..0x9F5864</c> zero the rest of the chunk).
/// </summary>
public sealed class WwiseFxChunk
{
    /// <summary><c>+0</c>.</summary>
    public int Version0 { get; set; }

    /// <summary><c>+4 + 8 * slot</c>.</summary>
    public uint[] Ids { get; } = new uint[4];

    /// <summary><c>+9 + 8 * slot</c>.</summary>
    public byte[] Share { get; } = new byte[4];

    /// <summary>The test <c>0x9C54F8..0x9C5524</c>: one of the four ids (<c>+4</c>, <c>+0xC</c>, <c>+0x14</c>, <c>+0x1C</c>) is non-zero.</summary>
    public bool AnyId => Ids[0] != 0 || Ids[1] != 0 || Ids[2] != 0 || Ids[3] != 0;
}

/// <summary>The category <c>vt+0x44</c> returns per node class (B5; every body is <c>mov r0,#n; bx lr</c>), read from the vtable slots <c>[vptr+0x44]</c>.</summary>
public static class WwiseNodeCategory44
{
    /// <summary>Bus, vptr <c>0x103ACE0</c>, <c>0x9C07BC</c>.</summary>
    public const int Bus = 0;

    /// <summary>ActorMixer, vptr <c>0x103CF88</c>, <c>0xA667C8</c>.</summary>
    public const int ActorMixer = 1;

    /// <summary>RanSeq, vptr <c>0x103B860</c>, <c>0xA06BE0</c>.</summary>
    public const int RandomSequence = 2;

    /// <summary>Sound, vptr <c>0x103BAD0</c>, <c>0xA1D350</c>.</summary>
    public const int Sound = 3;

    /// <summary>Switch, vptr <c>0x103BDC8</c>, <c>0xA30780</c>.</summary>
    public const int Switch = 4;

    /// <summary>Layer, vptr <c>0x103B050</c>, <c>0x9D0454</c>.</summary>
    public const int Layer = 5;

    /// <summary>The class of vptr <c>0x103A470</c> (<c>0x990710</c>).</summary>
    public const int Class103A470 = 6;

    /// <summary>The class of vptr <c>0x103A018</c> (<c>0x989868</c>).</summary>
    public const int Class103A018 = 7;

    /// <summary>The class of vptr <c>0x1039EA8</c> (<c>0x9886F0</c>).</summary>
    public const int Class1039EA8 = 8;

    /// <summary>The class of vptr <c>0x103A198</c> (<c>0x98B138</c>).</summary>
    public const int Class103A198 = 9;

    /// <summary>The audio-device bus, vptr <c>0x103D100</c>, <c>0xA67C98</c> (the value the predicates compare with).</summary>
    public const int AudioDeviceBus = 0xC;
}

/// <summary>The unread callees of the bus constructor <c>0x9C3620</c> (named, required).</summary>
public sealed class WwiseBusCtorSeams
{
    /// <summary>The base constructor <c>0x9F402C(bus, id)</c> (<c>0x9C364C</c>): node base fields. Not adopted.</summary>
    public Action<WwiseRoutingNode>? BaseCtor9F402C { get; set; }

    /// <summary><c>0xA19F94(bus+0xC4)</c> (<c>0x9C36D8</c>). Not adopted.</summary>
    public Action<WwiseRoutingNode>? Init9A19F94 { get; set; }

    /// <summary><c>0x9F40F4(bus)</c> (<c>0x9C3730</c>). Not adopted.</summary>
    public Action<WwiseRoutingNode>? Post9F40F4 { get; set; }
}

/// <summary>The unread callees of the bus reader pieces <see cref="WwiseBusWalk.ApplyChannelConfig"/>, <see cref="WwiseBusWalk.ApplyFlagByteC"/>, <see cref="WwiseBusWalk.RegisterFx9F5760"/> and <see cref="WwiseBusWalk.ReadFxList9C0D08"/>.</summary>
public sealed class WwiseBusReaderSeams
{
    /// <summary><c>0xA4454C([bus+8])</c> (<c>0x9C650C</c>): runs when the channel config changed. Not adopted.</summary>
    public Action<uint>? ChannelConfigChangedA4454C { get; set; }

    /// <summary><c>0x9C62AC(bus)</c> (<c>0x9C6560</c>): runs when the flags byte has bit 3. Not adopted.</summary>
    public Action<WwiseRoutingNode>? FlagBit3A9C62AC { get; set; }

    /// <summary>Bus <c>vt+0xC4(bus, 0)</c> (<c>0x9F57D4..0x9F57D8</c>): run when an FX slot's id or share changed. Not adopted.</summary>
    public Action<WwiseRoutingNode>? FxSlotChangedVtC4 { get; set; }

    /// <summary>Bus <c>vt+0x8C(bus, slot)</c> (<c>0x9F57E8..0x9F57EC</c>): run after <see cref="FxSlotChangedVtC4"/>. Not adopted.</summary>
    public Action<WwiseRoutingNode, int>? FxSlotChangedVt8C { get; set; }

    /// <summary><c>0x9F5C30(bus, bypass, -1)</c> (<c>0x9C0DF8</c>, <c>0x9C0E0C</c>). Not adopted.</summary>
    public Action<WwiseRoutingNode, byte>? FxBypassA9F5C30 { get; set; }

    /// <summary>Bus <c>vt+0xE0(bus, id, flag, 0)</c> = <c>0x9C0FC0</c> (<c>0x9C0D5C..0x9C0D60</c>): the mixer record. C34.1 lists its body as open, so it is a required seam; its result is the list reader's.</summary>
    public Func<WwiseRoutingNode, uint, bool, int>? MixerVtE0 { get; set; }

    /// <summary>The allocation failure of <c>0xA7A7F4(pool, 0x28)</c> in <c>0x9F5760</c> (<c>0x9F5810</c>): true returns 0x34 (the chunk pointer is stored as 0).</summary>
    public Func<bool>? AllocationFails { get; set; }
}

/// <summary>The bus walk and the bundle readers (C34.1).</summary>
public static class WwiseBusWalk
{
    /// <summary>The u32 table at <c>0xFFA6C0</c> (GOT <c>0x1040084</c>), indexed by the parameter id: the subscription bit of the parameter (R5). Read from the image.</summary>
    public static readonly uint[] ParamBitTable =
    {
        0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x7, 0x11, 0x2F, 0x8, 0x9, 0xE, 0x12, 0x13, 0x18, 0x2F, 0x2F, 0x2F, 0x2F, 0x27, 0x28, 0x29, 0x2A, 0x26, 0x2B, 0x2C, 0x2D, 0x22, 0x24, 0x23, 0x2F, 0x2F, 0x2F, 0x25,
        0x2F, 0x2F, 0x2F, 0x2F, 0x2F, 0x2F, 0x2F, 0x2F, 0x2F, 0x2F, 0x2F, 0x2F, 0x2F, 0xB, 0xC, 0x2F, 0x2F, 0x2F, 0x2F, 0x2F, 0xD, 0x2F, 0x2F, 0x2F, 0x2F, 0x6, 0x101, 0x10100, 0x100, 0x0,
    };

    /// <summary>
    /// <c>0x9F4BB8(node)</c> (B3): the nearest ancestor-or-self whose <c>[+0x38]</c> output-bus link is set. Loop: <c>r3 = [node+0x38]</c>, non-zero returns it; else <c>node = [node+0x34]</c> and a zero returns 0
    /// (<c>0x9F4BB8..0x9F4BD8</c>).
    /// </summary>
    public static WwiseRoutingNode? A9F4BB8(WwiseRoutingNode? node)
    {
        while (node is not null)
        {
            if (node.OutputBus is { } bus) return bus;                                    // 0x9F4BB8..0x9F4BC0, 0x9F4BD4
            node = node.Parent;                                                           // 0x9F4BC4..0x9F4BCC
        }
        return null;
    }

    /// <summary><c>0x9BDA6C(ctx)</c> (B2, <c>0x9FFAF4</c>): <c>[pbi+0xE9]</c> bit 2 returns 0; otherwise <c>0x9F4BB8([pbi+0xE0])</c> (<c>0x9BDA6C..0x9BDA84</c>).</summary>
    public static WwiseRoutingNode? FirstOutputBus9BDA6C(byte flagsE9, WwiseRoutingNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if ((flagsE9 & 4) != 0) return null;                                              // 0x9BDA6C..0x9BDA74, 0x9BDA80
        return A9F4BB8(node);                                                             // 0x9BDA78..0x9BDA7C
    }

    /// <summary>
    /// <c>0x9F9CDC(node, out, mask)</c> (B8): the state-list reader. It returns at once when <c>[node+0x46]</c> bit 0 is clear or <c>[node+0x18]</c> is 0 (<c>0x9F9CDC..0x9F9CF0</c>). For each item of the list
    /// (<c>[item+8]</c> next) with a bundle at <c>[item+0x10]</c>: mask bit 1 adds the entry of property 0 to <c>out[0]</c> and the entry of property 6 to <c>out[6]</c>; bit 2 property 2 to <c>out[2]</c> (<c>out+8</c>); bit 4
    /// property 3 to <c>out[3]</c> (<c>+0xC</c>); bit 8 property 4 to <c>out[4]</c> (<c>+0x10</c>); bit 0x10 property 5 to <c>out[5]</c> (<c>+0x14</c>) (<c>0x9F9D0C..0x9F9F48</c>). <paramref name="outBlock"/> holds the floats at
    /// <c>out + 4 * index</c>; single-precision adds.
    /// </summary>
    public static void A9F9CDC(WwiseRoutingNode node, float[] outBlock, uint mask)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(outBlock);
        if ((node.Byte46 & 1) == 0) return;                                               // 0x9F9CDC..0x9F9CE4
        if (node.States18 is null || node.States18.Count == 0) return;                    // 0x9F9CE8..0x9F9CF0
        foreach (var item in node.States18)                                               // 0x9F9F48 ldr ip,[ip,#8]
        {
            var bundle = item.Bundle10;                                                   // [item+0x10]
            if (bundle is null) continue;                                                 // every block tests it non-zero
            if ((mask & 1) != 0)                                                          // 0x9F9D0C..0x9F9D10
            {
                if (bundle.Find(0) is { } v0) outBlock[0] = outBlock[0] + BitConverter.UInt32BitsToSingle(v0);   // 0x9F9D64..0x9F9D70
                if (bundle.Find(6) is { } v6) outBlock[6] = outBlock[6] + BitConverter.UInt32BitsToSingle(v6);   // 0x9F9DAC..0x9F9DB8
            }
            if ((mask & 2) != 0 && bundle.Find(2) is { } v2) outBlock[2] = outBlock[2] + BitConverter.UInt32BitsToSingle(v2);   // 0x9F9E10..0x9F9E1C
            if ((mask & 4) != 0 && bundle.Find(3) is { } v3) outBlock[3] = outBlock[3] + BitConverter.UInt32BitsToSingle(v3);   // 0x9F9E74..0x9F9E80
            if ((mask & 8) != 0 && bundle.Find(4) is { } v4) outBlock[4] = outBlock[4] + BitConverter.UInt32BitsToSingle(v4);   // 0x9F9ED8..0x9F9EE4
            if ((mask & 0x10) != 0 && bundle.Find(5) is { } v5) outBlock[5] = outBlock[5] + BitConverter.UInt32BitsToSingle(v5);   // 0x9F9F38..0x9F9F44
        }
    }

    /// <summary>
    /// <c>0x9C39DC(node, flag, p)</c> (B9): the bus's parameter sum, single precision, <c>s16</c> starting at 0.0f. (1) <c>p == 0</c>: <c>0x9F9CDC(node, loc, 1)</c>, <c>s16 = loc[0] + s16</c>; <c>p == 5</c>: mask 0x10,
    /// <c>s16 = loc[5] + s16</c> (<c>0x9C3C18..0x9C3C2C</c>, <c>0x9C3CD0..0x9C3CE4</c>; other <c>p</c> skip it). (2) The base bundle <c>[node+0x3C]</c>: the value of id <c>p</c> (0.0f when absent or the bundle is null) is added (<c>0x9C3A48..0x9C3AC8</c>).
    /// (3) <c>[node+0x14] != 0</c> and bit <c>T[p]</c> of the u64 it points to is set: <c>s16 += 0xA11590(*0x108D908, node+0x10, T[p], {0, 0, 0, 0xFF, 0xFF, 0})</c> (<c>0x9C3ACC..0x9C3B00</c>, <c>0x9C3BF4..0x9C3C14</c>). (4) <c>[node+0x24]</c> and
    /// <c>[[node+0x24]+0xC]</c> non-null: the FIRST float of the ranged entry with id <c>p</c> is added when found (<c>0x9C3B04..0x9C3B5C</c>). (5) <c>p == 0</c>: the list <c>[node+0x8C]</c>; <c>p == 5</c>: <c>[node+0xA8]</c>; the floats at <c>+0x14</c>
    /// of its items are summed in order from 0.0f, <c>m = [node+0x6C] &gt; sum ? [node+0x6C] : sum</c> (<c>vcmp</c>, <c>vmovgt</c>), <c>s16 += m</c> (<c>0x9C3B60..0x9C3BB0</c>). (6) Any other <c>p</c> loads through a null pointer (<c>0x9C3BE8..0x9C3BF0</c>: the
    /// engine faults). (7) <c>r7 = [node+0x38]</c>: zero returns <c>s16</c>; <paramref name="flag"/> 1 recurses <c>0x9C39DC(r7, 1, p)</c> and adds; otherwise the parent is tested first and the walk stops (no recursion) when <c>0x9C54E8(r7)</c> would hold
    /// (<c>0x9C3C38..0x9C3CC4</c>), else it recurses with the same flag (<c>0x9C3CC8</c>, <c>0x9C3BC0..0x9C3BD4</c>).
    /// </summary>
    /// <param name="rtpc">The manager <c>*0x108D908</c>; required when bit <c>T[p]</c> of the node's mask is set.</param>
    public static float A9C39DC(WwiseRoutingNode node, int flag, uint p, WwiseRtpcStore? rtpc)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (p != 0 && p != 5)                                                             // 0x9C3B68..0x9C3BF0: any other p loads through a null pointer (the engine faults) after steps with no observable effect
            throw new InvalidOperationException("0x9C39DC with a parameter other than 0 or 5 loads through a null pointer (0x9C3BE8..0x9C3BF0) and faults");
        var loc = new float[7] { 0f, 1f, 0f, 0f, 0f, 0f, 0f };                            // sp+0x20..0x38 (0x9C3A08..0x9C3A38; [sp+0x24] = 1.0f)
        float s16 = 0f;                                                                   // 0x9C39F4 vldr s16,[pc,#0x2fc] (0x9C3CF8 = 0)
        if (p == 0)
        {
            A9F9CDC(node, loc, 1);                                                        // 0x9C3CD0..0x9C3CD8
            s16 = loc[0] + s16;                                                           // 0x9C3CDC..0x9C3CE0
        }
        else if (p == 5)
        {
            A9F9CDC(node, loc, 0x10);                                                     // 0x9C3C18..0x9C3C20
            s16 = loc[5] + s16;                                                           // 0x9C3C24..0x9C3C28 (sp+0x34)
        }

        float base3C = 0f;                                                                // 0x9C3C30 / 0x9C3CF0: s13 = 0.0f when the bundle is null or the id is absent
        if (node.BaseBundle3C is { } baseBundle && baseBundle.Find((byte)p) is { } bw) base3C = BitConverter.UInt32BitsToSingle(bw);   // 0x9C3A48..0x9C3ABC
        s16 = s16 + base3C;                                                               // 0x9C3AC8

        uint bit = ParamBitTable[p];                                                      // 0x9C3AD4 ldr r2,[r3,r6,lsl #2]
        if (node.SubscriptionMask14 is { } mask && ((mask >> (int)bit) & 1) != 0)         // 0x9C3ADC..0x9C3B00 (T[0] = 0, T[5] = 5: a bit of the u64)
        {
            var store = rtpc ?? throw new WwiseMissingBehaviourException(
                "M6-009 R1: the node's subscription bit is set, so 0x9C39DC calls 0xA11590 on the RTPC manager (*0x108D908); supply it");
            s16 = s16 + store.A11590(node.SubscriptionKey10, bit, WwiseGainRtpcKey.Empty);   // 0x9C3BF4..0x9C3C10
        }

        if (node.RangedBundle24 is { } ranged && ranged.Find((byte)p) is { } rw)         // 0x9C3B04..0x9C3B58 (the ranged entry's FIRST float)
            s16 = s16 + BitConverter.UInt32BitsToSingle(rw);                              // 0x9C3B5C

        IReadOnlyList<float> duck = p == 0 ? node.Duck8C : node.DuckA8;                   // 0x9C3B64 beq 0x9C3CE8 (+0x8C); 0x9C3B70 (+0xA8)
        float sum = 0f;                                                                   // 0x9C3B78 vldr s15,[pc,#0x178]
        foreach (float f in duck) sum = sum + f;                                          // 0x9C3B84..0x9C3B94
        float m = node.MaxDuck6C > sum ? node.MaxDuck6C : sum;                            // 0x9C3B98..0x9C3BA8 vcmp s14,s15; vmovgt
        s16 = s16 + m;                                                                    // 0x9C3BB0

        var r7 = node.OutputBus;                                                          // 0x9C3B9C ldr r7,[r4,#0x38]
        if (r7 is null) return s16;                                                       // 0x9C3BAC..0x9C3BB4
        if (flag == 1)                                                                    // 0x9C3BB8
            return s16 + A9C39DC(r7, flag, p, rtpc);                                      // 0x9C3BC0..0x9C3BD4
        if (r7.A9C54E8()) return s16;                                                     // 0x9C3C38..0x9C3CC4: the parent stops the walk
        return s16 + A9C39DC(r7, flag, p, rtpc);                                          // 0x9C3CC8 -> 0x9C3BC0
    }

    // ------------------------------------------------------------------ the bus constructor and the reader pieces (B4, B6)

    /// <summary>
    /// The bus constructor <c>0x9C3620(id)</c> (B4), after the base constructor <c>0x9F402C</c> (<paramref name="seams"/>): <c>[+0x68]</c> byte 0, <c>[+0x69]</c> low nibble 0, <c>[+0x6C] = 0xC2C0999A</c>, bits 12..31 of <c>[+0x68]</c> 0, the words
    /// <c>+0x48..+0x64</c>, <c>+0x7C..+0x84</c>, <c>+0x98..+0xA0</c>, <c>+0xB4..+0xBC</c> 0 (so <c>[+0x54]</c> is 0), then <c>0xA19F94(bus+0xC4)</c>, <c>[+0xCC] = 0x30</c> (high nibble kept, bits 4..5 set, bit 6 and bit 7 cleared, low nibble cleared:
    /// <c>and #0xF0; orr #0x30; and #0xBF; bfi bit7 = 0</c>), <c>[+0x46]</c> bit 2 = (<c>vt+0x44</c> in {0xA, 0xC}) or <c>vt+0x44 == 0</c>, <c>0x9F40F4(bus)</c>, and the lists <c>+0x70..+0xC0</c> 0 with <c>[+0x80] = [+0x9C] = [+0xB8] = 0x64</c>
    /// (<c>0x9C3650..0x9C377C</c>). Only the fields <see cref="WwiseRoutingNode"/> models are stored.
    /// </summary>
    public static WwiseRoutingNode ConstructBus9C3620(uint id, int category44, WwiseBusCtorSeams seams)
    {
        ArgumentNullException.ThrowIfNull(seams);
        var bus = new WwiseRoutingNode { Id = id, IsBus = true, Category44 = category44 };
        (seams.BaseCtor9F402C ?? throw new WwiseMissingBehaviourException("M6-025 B4: the base constructor 0x9F402C (0x9C364C) is not adopted"))(bus);
        bus.Word68 = 0;                                                                   // 0x9C3654 byte 0; 0x9C365C nibble 0; 0x9C3680 bits 12..31 0
        bus.MaxDuck6C = BitConverter.Int32BitsToSingle(unchecked((int)0xC2C0999A));       // 0x9C3658, 0x9C3668, 0x9C3678
        bus.Word54 = 0;                                                                   // 0x9C36A0
        bus.Duck8C.Clear(); bus.DuckA8.Clear();                                           // 0x9C3758, 0x9C376C (the lists are empty)
        (seams.Init9A19F94 ?? throw new WwiseMissingBehaviourException("M6-025 B4: 0xA19F94 (0x9C36D8) is not adopted"))(bus);
        bus.ByteCC = (byte)((((bus.ByteCC & 0xF0) | 0x30) & 0xBF) & 0x7F);                // 0x9C36DC..0x9C36F8
        bool bit2 = category44 is 0xA or 0xC || category44 == 0;                          // 0x9C3708..0x9C3724
        bus.Byte46 = (byte)((bus.Byte46 & ~4) | (bit2 ? 4 : 0));                          // 0x9C3728..0x9C372C
        (seams.Post9F40F4 ?? throw new WwiseMissingBehaviourException("M6-025 B4: 0x9F40F4 (0x9C3730) is not adopted"))(bus);
        return bus;
    }

    /// <summary>
    /// The channel-config store of the bus reader (B6, <c>0x9C64C4..0x9C6510</c>, <c>0x9C660C..0x9C665C</c>): with <c>(cfg &gt;&gt; 8) &amp; 0xF == 1</c> the channel count <c>[+0x68]</c> byte is the popcount of
    /// <c>(cfg &gt;&gt; 12) &amp; 0x3FF3F</c>, the nibble <c>[+0x69]</c> 1, and bits 12..31 of <c>[+0x68]</c> the masked value; otherwise the byte is <c>cfg &amp; 0xFF</c>, the nibble <c>(cfg &gt;&gt; 8) &amp; 0xF</c> and bits 12..31
    /// <c>cfg &gt;&gt; 12</c>. A change of the byte, the nibble or bits 12..31 calls <c>0xA4454C([bus+8])</c> (seam); then <c>[bus+0x46]</c> bit 0 is set.
    /// </summary>
    public static void ApplyChannelConfig(WwiseRoutingNode bus, uint cfg, WwiseBusReaderSeams seams)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(seams);
        uint oldWord = bus.Word68;                                                        // 0x9C64B0 ldr ip,[r4,#0x68]
        byte oldByte = (byte)oldWord;                                                     // 0x9C64D8
        uint kind = (cfg >> 8) & 0xF;                                                     // 0x9C64C4 ubfx r1,r3,#8,#4
        uint hi = (cfg >> 12) & 0xFFFFF;                                                  // 0x9C64CC, 0x9C64D4
        byte newByte;
        uint nibble;
        uint stored;
        if (kind == 1)                                                                    // 0x9C64C8 cmp r1,#1; 0x9C64DC beq 0x9C660C
        {
            uint masked = 0x3FF3Fu & hi;                                                  // 0x9C660C..0x9C6614
            stored = masked & 0xFFFFF;                                                    // 0x9C661C
            newByte = (byte)System.Numerics.BitOperations.PopCount(masked);               // 0x9C6624..0x9C6638 (x &= x - 1 loop; uxtb)
            nibble = 1;                                                                   // 0x9C6640..0x9C6648
        }
        else
        {
            newByte = (byte)cfg;                                                          // 0x9C64E4 uxtb r3,r3
            nibble = kind;                                                                // 0x9C64EC
            stored = hi;                                                                  // 0x9C64F8
        }
        uint word = (oldWord & ~0xFFu) | newByte;                                         // strb [+0x68]
        word = (word & ~0xF00u) | (nibble << 8);                                          // bfi [+0x69] bits 0..3
        word = (word & 0xFFFu) | (stored << 12);                                          // bfi bits 12..31
        bus.Word68 = word;
        bool changed;
        if (newByte != oldByte) changed = true;                                           // 0x9C6500..0x9C6504 cmp r3,lr; beq 0x9C65E0
        else if ((((word >> 8) ^ (oldWord >> 8)) & 0xF) != 0) changed = true;             // 0x9C65E0..0x9C65EC: the nibble
        else changed = ((oldWord ^ word) & 0xFFFFF000u) != 0;                             // 0x9C65F0..0x9C6608: bits 12..31
        if (changed)
            (seams.ChannelConfigChangedA4454C ?? throw new WwiseMissingBehaviourException("M6-025 B6: 0xA4454C (0x9C650C) is not adopted"))(bus.Id);
        bus.Byte46 = (byte)(bus.Byte46 | 1);                                              // 0x9C6510..0x9C651C
    }

    /// <summary>
    /// The byte C of the bus reader (B6, <c>0x9C6528..0x9C6564</c>): bit 0 sets <c>[bus+0x40] |= 0xE0000</c> (clear: <c>&amp;= ~0xE0000</c>); bit 1 is stored to <c>[bus+0xCC]</c> bit 3; when the flags byte F (read at <c>0x9C646C</c>)
    /// has bit 3 (<c>and #8</c>) <c>0x9C62AC(bus)</c> runs (seam).
    /// </summary>
    public static void ApplyFlagByteC(WwiseRoutingNode bus, byte c, byte f, WwiseBusReaderSeams seams)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(seams);
        uint w40 = bus.Word40;
        w40 = (c & 1) != 0 ? w40 | 0xE0000u : w40 & ~0xE0000u;                            // 0x9C6540, 0x9C6544
        byte cc = bus.ByteCC;
        cc = (byte)((cc & ~8) | (((c >> 1) & 1) << 3));                                   // 0x9C6534, 0x9C654C bfi r1,r3,#3,#1
        bus.Word40 = w40;                                                                 // 0x9C6550
        bus.ByteCC = cc;                                                                  // 0x9C6554
        if ((f & 8) != 0)                                                                 // 0x9C6514 and r6,r6,#8; 0x9C6548 cmp r6,#0
            (seams.FlagBit3A9C62AC ?? throw new WwiseMissingBehaviourException("M6-025 B6: 0x9C62AC (0x9C6560) is not adopted"))(bus);
    }

    /// <summary>
    /// <c>0x9F5760(bus, slot, id, share, version)</c> (B6): a slot above 3 returns 0x1F. The chunk <c>[bus+0x28]</c> is allocated on first use (a failed allocation returns 0x34 with <c>[bus+0x28] = 0</c>). A chunk version above
    /// <paramref name="version"/> (signed) returns 1; else <c>[chunk] = version</c>, and when the share byte or the id differs they are stored and the bus <c>vt+0xC4(bus, 0)</c> and <c>vt+0x8C(bus, slot)</c> run (seams); the result is 1
    /// (<c>0x9F5760..0x9F5864</c>).
    /// </summary>
    public static int RegisterFx9F5760(WwiseRoutingNode bus, int slot, uint id, byte share, int version, WwiseBusReaderSeams seams)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(seams);
        if ((uint)slot > 3) return 0x1F;                                                  // 0x9F5760..0x9F5768 cmp r1,#3; movhi r0,#0x1f
        var chunk = bus.Fx28;
        if (chunk is null)                                                                // 0x9F5788..0x9F578C
        {
            if (seams.AllocationFails?.Invoke() == true) return 0x34;                     // 0x9F5810..0x9F5820, 0x9F5868
            chunk = bus.Fx28 = new WwiseFxChunk();                                        // 0x9F5824..0x9F5858 zero fill
        }
        if (chunk.Version0 > version) return 1;                                           // 0x9F5790..0x9F579C bgt 0x9F57F0
        chunk.Version0 = version;                                                         // 0x9F57A4
        if (chunk.Share[slot] == share && chunk.Ids[slot] == id) return 1;               // 0x9F57A8..0x9F57BC
        chunk.Share[slot] = share;                                                        // 0x9F57C8
        chunk.Ids[slot] = id;                                                             // 0x9F57D0
        (seams.FxSlotChangedVtC4 ?? throw new WwiseMissingBehaviourException("M6-025 B6: bus vt+0xC4 (0x9F57D4) is not adopted"))(bus);
        (seams.FxSlotChangedVt8C ?? throw new WwiseMissingBehaviourException("M6-025 B6: bus vt+0x8C (0x9F57E8) is not adopted"))(bus, slot);
        return 1;                                                                         // 0x9F57F0
    }

    /// <summary>
    /// The FX list of the bus reader <c>0x9C0D08(bus, &amp;cursor)</c> (B6): <c>u8 count</c>; a non-zero count is followed by a bypass byte and <c>count</c> entries of 7 bytes <c>{u8 slot, u32 id, u8 share, u8}</c>; an entry with id 0 is skipped, any
    /// other goes to <c>0x9F5760(bus, slot, id, share != 0, version 0)</c> and a result other than 1 runs <c>0x9F5C30(bus, bypass, -1)</c> and ends with that result; after the loop <c>0x9F5C30(bus, bypass, -1)</c> runs. Then the mixer: <c>u32 id</c>, <c>u8 flag</c>
    /// to the bus <c>vt+0xE0 = 0x9C0FC0(bus, id, flag != 0, 0)</c>, whose body C34.1 does not adopt (the seam <see cref="WwiseBusReaderSeams.MixerVtE0"/>). Every exit sets <c>[bus+0x40] |= 0x1F000</c> and returns the result (<c>0x9C0D08..0x9C0E14</c>).
    /// </summary>
    public static int ReadFxList9C0D08(WwiseRoutingNode bus, ReadOnlySpan<byte> data, ref int pos, WwiseBusReaderSeams seams)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(seams);
        int count = data[pos++];                                                          // 0x9C0D1C ldrb r6,[r2],#1
        int result = 1;
        bool failed = false;
        if (count != 0)
        {
            byte bypass = data[pos++];                                                    // 0x9C0D84 ldrb r8,[r3,#1]; 0x9C0D8C add r3,r3,#2
            for (int i = 0; i < count; i++)                                               // 0x9C0D98 cmp r6,r5
            {
                int slot = data[pos];                                                     // 0x9C0DAC ldrb r1,[r3]
                uint id = BitConverter.ToUInt32(data.Slice(pos + 1, 4));                  // 0x9C0DA4 ldr r2,[r3,#1]
                byte share = data[pos + 5];                                               // 0x9C0DC0 ldrb r3,[r3,#5]
                pos += 7;                                                                 // 0x9C0DB8, 0x9C0DC4: +5, +7
                if (id == 0) continue;                                                    // 0x9C0DB4 cmp r2,#0; beq 0x9C0D98
                int r = RegisterFx9F5760(bus, slot, id, (byte)(share != 0 ? 1 : 0), 0, seams);   // 0x9C0DCC..0x9C0DDC
                if (r != 1)                                                               // 0x9C0DE0..0x9C0DE8
                {
                    (seams.FxBypassA9F5C30 ?? throw new WwiseMissingBehaviourException("M6-025 B6: 0x9F5C30 (0x9C0DF8) is not adopted"))(bus, bypass);
                    result = r;
                    failed = true;
                    break;
                }
            }
            if (!failed)
                (seams.FxBypassA9F5C30 ?? throw new WwiseMissingBehaviourException("M6-025 B6: 0x9F5C30 (0x9C0E0C) is not adopted"))(bus, bypass);
        }
        if (!failed)
        {
            uint mixerId = BitConverter.ToUInt32(data.Slice(pos, 4));                     // 0x9C0D38 ldr r1,[ip],#4
            byte flag = data[pos + 4];                                                    // 0x9C0D48 ldrb r2,[r2,#4]
            pos += 5;                                                                     // 0x9C0D34, 0x9C0D4C
            result = (seams.MixerVtE0 ?? throw new WwiseMissingBehaviourException(
                "M6-025 B6: the bus mixer record vt+0xE0 = 0x9C0FC0 (0x9C0D60) is not adopted by C34.1 (listed open); supply WwiseBusReaderSeams.MixerVtE0"))(bus, mixerId, flag != 0);   // 0x9C0D60 blx ip
        }
        bus.Word40 |= 0x1F000;                                                            // 0x9C0D68..0x9C0D74
        return result;
    }
}
