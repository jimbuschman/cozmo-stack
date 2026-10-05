// fidelity: M6-010
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// Property ids the gain composition reads (gapC 1.2, 1.3, 1.4, 1.7, 1.8). The ids that also appear in
/// <see cref="WwiseProp"/> keep that numbering; 0x18/0x19/0x1A are the output-bus trio of gapC 1.2.
/// </summary>
public static class WwiseGainProps
{
    /// <summary>Prop 0, Volume, in dB (gapC 1.3).</summary>
    public const byte Volume = 0;

    /// <summary>Prop 2, Pitch, in cents (gapC 1.3).</summary>
    public const byte Pitch = 2;

    /// <summary>Prop 3, LowPass, in the filter's 0..100 value domain (gapC 1.3).</summary>
    public const byte LowPass = 3;

    /// <summary>Prop 4, HighPass, in the filter's 0..100 value domain (gapC 1.3).</summary>
    public const byte HighPass = 4;

    /// <summary>Prop 5, Bus Volume, in dB (gapC 1.9, 3.2).</summary>
    public const byte BusVolume = 5;

    /// <summary>Prop 6, MakeUpGain (gapC 1.3).</summary>
    public const byte MakeUpGain = 6;

    /// <summary>Id 0xB, MuteRatio (gapC 1.4, 1.11).</summary>
    public const byte MuteRatio = 0x0B;

    /// <summary>Props 0x13..0x16, the four user-aux volumes (gapC 1.8).</summary>
    public const byte UserAuxVolume0 = 0x13;

    /// <summary>Prop 0x17, GameAuxSendVolume, in dB (gapC 1.7).</summary>
    public const byte GameAuxSendVolume = 0x17;

    /// <summary>Prop 0x18, OutputBusVolume, in dB, into io+0x28 (gapC 1.2).</summary>
    public const byte OutputBusVolume = 0x18;

    /// <summary>Prop 0x19, the output bus's HPF, into io+0x30 (gapC 1.2).</summary>
    public const byte OutputBusHighPass = 0x19;

    /// <summary>Prop 0x1A, the output bus's LPF, into io+0x2C (gapC 1.2).</summary>
    public const byte OutputBusLowPass = 0x1A;
}

/// <summary>
/// The <c>paramSelect</c> bitmask <c>GetAudioParameters</c> takes (gapC 1.3, 1.9). The row names the bits by
/// value: <c>bit1</c> Volume, <c>bit2</c> Pitch, <c>bit4</c> LowPass, <c>bit8</c> HighPass, and the bus
/// method clears <c>bit 0x10</c> (Bus Volume). Read here as the masks 0x1, 0x2, 0x4, 0x8 and 0x10.
///
/// <para>MakeUpGain (prop 6) has no named bit in the row; the bundle adder adds it whenever the node
/// carries it, and <see cref="WwiseParamSelect.BusVolume"/> is never summed into the voice volume here
/// because gapC 1.9 clears it and gapC 3.2 applies Bus Volume after the bus's FX instead.</para>
/// </summary>
[Flags]
public enum WwiseParamSelect
{
    /// <summary>Nothing selected.</summary>
    None = 0,

    /// <summary>Selects prop 0, Volume (gapC 1.3).</summary>
    Volume = 0x1,

    /// <summary>Selects prop 2, Pitch (gapC 1.3).</summary>
    Pitch = 0x2,

    /// <summary>Selects prop 3, LowPass (gapC 1.3).</summary>
    LowPass = 0x4,

    /// <summary>Selects prop 4, HighPass (gapC 1.3).</summary>
    HighPass = 0x8,

    /// <summary>Selects prop 5, Bus Volume; the bus method clears it (gapC 1.9).</summary>
    BusVolume = 0x10,

    /// <summary>The four per-node parameters (gapC 1.3).</summary>
    NodeParams = Volume | Pitch | LowPass | HighPass,

    /// <summary>Every bit named in the rows.</summary>
    All = NodeParams | BusVolume,
}

/// <summary>
/// The RTPC key a node's RTPC pull uses: <c>pbi+0x14</c> = {game object, playing id, …} (gapC 1.3). The
/// remaining fields the native key carries (MIDI target, channel, note) are 0/0xFF in the shipped data and
/// are not part of this record; <see cref="Empty"/> is the bus key (gapC 1.9, gapF 1.9).
/// </summary>
public readonly record struct WwiseGainRtpcKey(uint GameObject, uint PlayingId)
{
    /// <summary>The bus / root key {0, 0} (gapC 1.9, gapF 1.9).</summary>
    public static WwiseGainRtpcKey Empty { get; } = new(0, 0);
}

/// <summary>
/// The muted map's key (gapC 1.4, 1.11): the node the insert came from, plus bit0 of the flag the insert
/// source packs into the key. The global reader sets it to 1 (<c>orr r3,r3,#1</c> at 0x9EF494; lookup
/// 0x9EF500) and the object reader clears it to 0 (<c>and r3,r3,#0xfe</c> at 0x9F02E4; lookup 0x9F0350); the
/// compare tests only bit0 (<c>tst r2,#1</c> at 0x9EF510/0x9F0360). So a node carrying MuteRatio in both its
/// global and object bundles is two entries. The product loop 0x9FFDB0..0x9FFDC0 multiplies across the
/// distinct entries.
/// </summary>
public readonly record struct WwiseMutedKey(uint NodeId, byte Flag)
{
    /// <summary>The global (node+0x24) reader's flag bit: bit0 = 1 (gapC 1.4).</summary>
    public const byte GlobalFlag = 1;

    /// <summary>The object (node+0x48) reader's flag bit: bit0 = 0 (gapC 1.4).</summary>
    public const byte ObjectFlag = 0;
}

/// <summary>
/// The output struct <c>GetAudioParameters</c> accumulates into (gapC 1.3, 1.2, 1.7, 1.8): the native
/// <c>io</c> at <c>pbi+0x3C</c>. Volume/LPF/HPF are dB (or the filter value domain), Pitch is cents.
///
/// This is a struct with a <see cref="Create"/> factory because the user-aux slots are arrays and the muted
/// map is a dictionary: always start from <see cref="Create"/> rather than a default instance, whose
/// <c>MutedMap</c> would be null.
/// </summary>
public struct WwiseAudioParameters
{
    /// <summary>io+0: the summed Volume in dB (gapC 1.3).</summary>
    public float VolumeDb;

    /// <summary>io+8: the summed Pitch in cents (gapC 1.3).</summary>
    public float PitchCents;

    /// <summary>io+0xC: the summed LPF value (gapC 1.3).</summary>
    public float LowPass;

    /// <summary>io+0x10: the summed HPF value (gapC 1.3).</summary>
    public float HighPass;

    /// <summary>io+0x18: the summed MakeUpGain (gapC 1.3).</summary>
    public float MakeUpGain;

    /// <summary>io+0x28: the output-bus volume in dB, from the lowest node that overrides the bus (gapC 1.2).</summary>
    public float OutputBusVolumeDb;

    /// <summary>io+0x2C: the output-bus LPF (gapC 1.2; filter B's input via M6-011).</summary>
    public float OutputBusLowPass;

    /// <summary>io+0x30: the output-bus HPF (gapC 1.2; filter B's input via M6-011).</summary>
    public float OutputBusHighPass;

    /// <summary>
    /// The muted map (gapC 1.4, 1.11): a MuteRatio (id 0xB) ≠ 1.0 is inserted here instead of a slot. The
    /// native insert (global 0x9EF484..0x9EF528, object 0x9F02C0..0x9F0378) is keyed by the node the insert
    /// came from plus a flag byte, and <b>overwrites per key</b>. Different nodes are distinct entries and
    /// multiply; the same node's inserts collapse. One factor of <c>pbi+0x40</c> is the product across the
    /// map's distinct keys (<see cref="MutedProduct"/>).
    /// </summary>
    public Dictionary<WwiseMutedKey, float> MutedMap;

    /// <summary>
    /// The product of the muted map's values across its distinct keys; 1.0 when the map is empty (gapC 1.11).
    /// </summary>
    public readonly float MutedProduct()
    {
        float product = 1f;
        if (MutedMap is null) return product;
        foreach (float value in MutedMap.Values) product *= value;
        return product;
    }

    /// <summary>io+0x54: the game-defined aux send volume in dB, decided once (gapC 1.7).</summary>
    public float GameAuxSendVolumeDb;

    /// <summary>io+0x59: whether the game-defined aux has been decided (gapC 1.7).</summary>
    public bool GameAuxDecided;

    /// <summary>io+0x58: the deciding node's "use game-defined aux" bit (gapC 1.7).</summary>
    public bool UseGameAux;

    /// <summary>io+0x5A: whether the user aux has been decided (gapC 1.8).</summary>
    public bool UserAuxDecided;

    /// <summary>io+0x34..0x40: the four user-aux volumes in dB (gapC 1.8).</summary>
    public float[] UserAuxVolumesDb;

    /// <summary>io+0x44..0x50: the four user-aux bus ids (gapC 1.8).</summary>
    public uint[] UserAuxIds;

    /// <summary>An empty accumulator: an empty muted map and the four user-aux slots allocated.</summary>
    public static WwiseAudioParameters Create() => new()
    {
        MutedMap = new Dictionary<WwiseMutedKey, float>(),
        UserAuxVolumesDb = new float[4],
        UserAuxIds = new uint[4],
    };
}

/// <summary>
/// A hierarchy node's gain inputs (gapC 1.1..1.4, 1.7). The links are the two the NodeBase
/// parent/bus AddChild sets: <see cref="Parent"/> is <c>node+0x34</c> and <see cref="OutputBus"/> is
/// <c>node+0x38</c> (gapC 1.1). Everything the rows describe but do not trace the writer of (the additive
/// bundles of gapC 1.4) is a caller input, not invented here.
/// </summary>
public sealed class WwiseGainNode
{
    private static readonly IReadOnlyDictionary<byte, float> NoProps = new Dictionary<byte, float>();
    private static readonly IReadOnlyDictionary<byte, (float Min, float Max)> NoRanges =
        new Dictionary<byte, (float Min, float Max)>();

    /// <summary>The node's id.</summary>
    public uint Id { get; init; }

    /// <summary>node+0x34: the direct parent, or null at the top (gapC 1.1).</summary>
    public WwiseGainNode? Parent { get; init; }

    /// <summary>node+0x38: the override output bus, or null (gapC 1.1).</summary>
    public WwiseGainBus? OutputBus { get; init; }

    /// <summary>The base property bundle at node+0x3C, keyed by property id (gapC 1.3).</summary>
    public IReadOnlyDictionary<byte, float> Props { get; init; } = NoProps;

    /// <summary>The node's RTPC bindings; a binding's <c>ParamId</c> names its target prop (gapC 1.3).</summary>
    public IReadOnlyList<WwiseRtpc> Rtpcs { get; init; } = Array.Empty<WwiseRtpc>();

    /// <summary>
    /// The randomizer ranged bundle at node+0x4C (gapC 1.5): per param, a min and a max. A zero range adds
    /// the min with no LCG draw (gapE 3.2).
    /// </summary>
    public IReadOnlyDictionary<byte, (float Min, float Max)> RangedProps { get; init; } = NoRanges;

    /// <summary>node+0x46 bit0: whether the node+0x18 bundle is read (gapC 1.4).</summary>
    public bool GatedBundleEnabled { get; init; }

    /// <summary>The node+0x18 additive bundle, read only when <see cref="GatedBundleEnabled"/> (gapC 1.4).</summary>
    public IReadOnlyDictionary<byte, float>? GatedBundle { get; init; }

    /// <summary>The node+0x24 additive bundle (gapC 1.4).</summary>
    public IReadOnlyDictionary<byte, float>? GlobalBundle { get; init; }

    /// <summary>The node+0x48 map of additive bundles keyed by the game object in the RTPC key (gapC 1.4).</summary>
    public IReadOnlyDictionary<uint, IReadOnlyDictionary<byte, float>>? ObjectBundles { get; init; }

    /// <summary>node+0x40 bit21: this node overrides the game-defined aux (gapC 1.7).</summary>
    public bool OverrideGameAux { get; init; }

    /// <summary>node+0x59 bit4: the node's "use game-defined aux" bit (gapC 1.7).</summary>
    public bool UseGameDefinedAux { get; init; }

    /// <summary>node+0x40 bits22..25: this node overrides the user aux (gapC 1.8).</summary>
    public bool OverrideUserAux { get; init; }

    /// <summary>The four user-aux bus ids at node+0x54 (gapC 1.8).</summary>
    public IReadOnlyList<uint> UserAuxIds { get; init; } = Array.Empty<uint>();
}

/// <summary>
/// An audio bus's gain-chain inputs (gapC 1.9, 3.2). <see cref="ParentBus"/> is the bus's own <c>+0x38</c>
/// link; the bus is a separate object from a <see cref="WwiseGainNode"/>.
/// </summary>
public sealed class WwiseGainBus
{
    private static readonly IReadOnlyDictionary<byte, float> NoProps = new Dictionary<byte, float>();

    /// <summary>The bus id.</summary>
    public uint Id { get; init; }

    /// <summary>The bus this one sends to, or null at a master bus (gapC 1.9).</summary>
    public WwiseGainBus? ParentBus { get; init; }

    /// <summary>The bus's property bundle; a bus has no ranged bundle (gapD D6.1, gapC 1.9).</summary>
    public IReadOnlyDictionary<byte, float> Props { get; init; } = NoProps;

    /// <summary>The bus's RTPC bindings, evaluated under the empty key (gapC 1.9).</summary>
    public IReadOnlyList<WwiseRtpc> Rtpcs { get; init; } = Array.Empty<WwiseRtpc>();

    /// <summary>
    /// +0x6C: the bus's max-duck, initialized to −96.3 (gapC 1.9). The volume sum gets
    /// <c>max(max-duck, Σ active ducking)</c>.
    /// </summary>
    public float MaxDuckDb { get; init; } = WwiseGain.BusVolumeInitDb;

    /// <summary>+0x8C: the summed active ducking in dB, 0 when nothing is ducking (gapC 1.9).</summary>
    public float DuckingDb { get; init; }
}

/// <summary>
/// The randomizer's ranges struct at <c>pbi+0x118</c> (gapC 1.5): each slot is the sum over the node chain
/// (leaf upward) of that node's drawn ranged value. The store offsets are +0 Volume, +4 MakeUpGain,
/// +8 Pitch, +0xC LPF, +0x10 HPF, but the native draws prop 6 (MakeUpGain → +4) <b>last</b>
/// (0x9EF5D8..0x9EF968; gapE 3.2 gives the draw order).
/// </summary>
public sealed class WwiseGainRanges
{
    /// <summary>+0: the summed Volume range draw.</summary>
    public float Volume;

    /// <summary>+4: the summed MakeUpGain range draw; drawn last (0x9EF968).</summary>
    public float MakeUpGain;

    /// <summary>+8: the summed Pitch range draw.</summary>
    public float Pitch;

    /// <summary>+0xC: the summed LPF range draw.</summary>
    public float LowPass;

    /// <summary>+0x10: the summed HPF range draw.</summary>
    public float HighPass;

    /// <summary>
    /// Adds one node's ranged props (gapC 1.5 gives the store offsets; gapE 3.2 the draw order): for each
    /// present range, <c>min + ((LCG_hi&gt;&gt;1)/2147483647.0)·(max−min)</c>, computed in double; a zero range
    /// adds the min with no draw. The native draws Volume (+0), Pitch (+8), LPF (+0xC), HPF (+0x10), and
    /// MakeUpGain (+4) last (0x9EF5D8..0x9EF968; <c>0x9EF8C8 cmp ip,#6</c>, <c>0x9EF968 vstr s15,[r3,#4]</c>).
    /// </summary>
    internal void Add(WwiseGainNode node, WwiseRng rng)
    {
        Volume += Draw(node, WwiseGainProps.Volume, rng);          // +0
        Pitch += Draw(node, WwiseGainProps.Pitch, rng);            // +8
        LowPass += Draw(node, WwiseGainProps.LowPass, rng);        // +0xC
        HighPass += Draw(node, WwiseGainProps.HighPass, rng);      // +0x10
        MakeUpGain += Draw(node, WwiseGainProps.MakeUpGain, rng);  // +4, drawn last
    }

    private static float Draw(WwiseGainNode node, byte param, WwiseRng rng)
    {
        if (!node.RangedProps.TryGetValue(param, out var range)) return 0f;
        if (range.Max == range.Min) return range.Min;                 // a zero range adds min with no draw
        double fraction = rng.Next() / 2147483647.0;
        return (float)(range.Min + fraction * ((double)range.Max - range.Min));
    }
}

/// <summary>
/// The per-voice randomizer state (gapC 1.5, 1.6): the ranges at <c>pbi+0x118</c> and the
/// <c>pbi+0x1BC</c> bit0 latch. <see cref="BeginPass"/> returns the ranges only while the bit is clear;
/// <see cref="EndPass"/> sets the bit at the end of a normal pass, so later passes pass no ranges and draw
/// nothing (the voice consumes the stored draw through <see cref="WwiseGain.Effective"/>).
/// </summary>
public sealed class WwiseVoiceRandomizer
{
    private readonly WwiseGainRanges _ranges = new();

    /// <summary>The stored ranges at <c>pbi+0x118</c> (gapC 1.5).</summary>
    public WwiseGainRanges Ranges => _ranges;

    /// <summary><c>pbi+0x1BC</c> bit0: true once a pass has drawn the ranges (gapC 1.6).</summary>
    public bool RangesDrawn { get; private set; }

    /// <summary>
    /// The ranges to pass to <see cref="WwiseGain.GetAudioParameters"/> this pass: the stored object while
    /// the bit is clear, otherwise null (gapC 1.6's <c>addeq ip,r4,#0x118</c>).
    /// </summary>
    public WwiseGainRanges? BeginPass() => RangesDrawn ? null : _ranges;

    /// <summary>Sets <c>pbi+0x1BC</c> bit0 at the end of a normal pass (gapC 1.6).</summary>
    public void EndPass() => RangesDrawn = true;
}

/// <summary>The composed effective parameters a voice consumes (gapC 1.6, 1.11, gapE 1.1).</summary>
/// <param name="VolumeDb">pbi+0x3C: the summed Volume plus the randomizer volume.</param>
/// <param name="PitchCents">pbi+0x44: the summed Pitch plus the randomizer pitch.</param>
/// <param name="LowPass">
/// pbi+0x9C's node-chain term: the summed LPF plus the randomizer LPF. M6-011's filter composition adds
/// <c>pbi+0xA0</c> on top.
/// </param>
/// <param name="HighPass">pbi+0xA4's node-chain term, likewise; M6-011 adds <c>pbi+0xA8</c>.</param>
/// <param name="MakeUpGain">The summed MakeUpGain plus its randomizer draw.</param>
/// <param name="MuteFade">pbi+0x40: <c>max(0, Π muted ratios · pbi+0x168 · pbi+0x16C)</c>.</param>
/// <param name="VoiceGain"><c>voice+0x1C = dBToLin(pbi+0x3C) · pbi+0x40</c>.</param>
public readonly record struct WwiseGainEffective(
    float VolumeDb, float PitchCents, float LowPass, float HighPass, float MakeUpGain,
    float MuteFade, float VoiceGain);

// The game-object aux send values, the send gain builder and the user sends of the voice (C40.4) are WwiseGameObjectRef.SetAuxValuesA0BA3C and WwiseAuxRoute (0x9BD368, 0x9D4228). The earlier test-only model of 0x9BD368 that stood here
// (WwiseAuxSendBuilder: a threshold of 0.0001f as a decimal, a caller-supplied emit threshold, no stop at a zero id, no terminator, DbToLinear's own polynomial literals) is replaced by that port.

/// <summary>
/// The M6-010 gain composition: <c>GetAudioParameters</c> over the parent/bus links, the per-voice
/// randomizer, the fast-pow dB→linear conversion, the game-defined aux send gain, the muted dry path and
/// the bus-volume-after-FX placement.
///
/// <para><b>Rows.</b> gapC 1.1..1.11, 2.1..2.8, 3.2 and gapE 1.1. RTPC values are pulled through the M6-009
/// store (<c>0xA11590</c>), which owns the accumulation and the key precedence.</para>
///
/// <para><b>Caller inputs, not invented values.</b> The node graph and its links; the additive bundles of
/// gapC 1.4, whose writers the rows call RECOVERABLE_GAP; the game object's send value; the 3D attenuation;
/// the fade factors <c>pbi+0x168</c>/<c>pbi+0x16C</c> (gapC 5.x); the send-emit thresholds; and the
/// root-note value behind <c>0x9FAE18</c> (gapC 1.10, RECOVERABLE_GAP). Nothing here is wired into the
/// live voice/bus graph yet.</para>
/// </summary>
public static class WwiseGain
{
    /// <summary>The float bit pattern of 1.0 (0x3F800000), the fast pow's exponent base (gapC 1.11).</summary>
    public const float OneBits = 1065353216f;

    /// <summary><c>2²³ · log2(10)</c>, the fast pow's per-unit scale (gapC 1.11).</summary>
    public const float PowScale = 27866352f;

    /// <summary>The <c>y</c> below which dBToLin returns 0: <c>y = 0.05·dB &lt; −37</c>, i.e. dB below −740 (gapC 1.11).</summary>
    public const float MinExponent = -37f;

    /// <summary>The MIDI key byte that means "no key" (gapC 1.10).</summary>
    public const byte NoMidiKey = 0xFF;

    /// <summary>The bus max-duck field's initialization, −96.3 (gapC 1.9).</summary>
    public const float BusVolumeInitDb = -96.3f;

    /// <summary>
    /// The game send's emit threshold address, [0x1052454] (WwiseSendGlobals.Shared: 0x38D2306A after the Init.bnk STMG setter 0x9A080C(-80, 2) (0x37800000 only in the pre-load image); the earlier C10 value 0x38D1B717 was contradicted).
    /// </summary>
    public const uint GameSendThresholdAddress = 0x1052454;

    /// <summary>The user send's emit threshold address, [0x1052450] (C10: -80 dB).</summary>
    public const uint UserSendThresholdAddress = 0x1052450;

    // ------------------------------------------------------------------ GetAudioParameters

    /// <summary>
    /// <c>GetAudioParameters</c> 0x9EF258 (gapC 1.2, 1.3, 1.4, 1.7): accumulates <paramref name="node"/>'s
    /// contributions into <paramref name="io"/>, then recurses.
    ///
    /// <list type="bullet">
    /// <item><b>Links (1.1, 1.2).</b> With <paramref name="doBusCheck"/> set and an output bus, the node's
    /// props 0x18/0x1A/0x19 (plus their RTPCs) fill io+0x28/0x2C/0x30 and the recursion visits both the
    /// parent (<c>node+0x34</c>) and the output bus (<c>node+0x38</c>) with the flag cleared. Otherwise only
    /// the parent is visited, with the flag carried. So the lowest overriding node supplies the output-bus
    /// values and the bus chain.</item>
    /// <item><b>Sums (1.3, 1.4).</b> The base bundle (node+0x3C) is select-gated and adds Volume/Pitch/
    /// LPF/HPF each plus its RTPC, plus MakeUpGain and a MuteRatio insert. The gated bundle (node+0x18) runs
    /// the same path. The global (node+0x24) and game-object (node+0x48) bundles add props 0/2/3/4 with no
    /// select test and never prop 6 (0x9EF2D8..0x9EF454, 0x9F011C..0x9F0228); the global bundle also inserts
    /// its MuteRatio. MuteRatio values overwrite per key in the muted map.</item>
    /// <item><b>Randomizer (1.5).</b> When <paramref name="ranges"/> is non-null each node's ranged props
    /// are drawn into it, in slot order; the first pass passes it, later passes pass null (1.6).</item>
    /// <item><b>Aux (1.7, 1.8).</b> The game-defined aux is decided at the first node with the override bit
    /// set, or at the top node (no parent): io+0x54 takes prop 0x17 plus its RTPC and io+0x58 takes the
    /// node's use bit. The user aux is decided the same way.</item>
    /// </list>
    /// </summary>
    /// <param name="node">The node whose contributions and ancestors are read.</param>
    /// <param name="paramSelect">The per-node sum bits (gapC 1.3).</param>
    /// <param name="io">The accumulator, created with <see cref="WwiseAudioParameters.Create"/>.</param>
    /// <param name="ranges">The randomizer ranges for this pass, or null when already drawn.</param>
    /// <param name="store">The M6-009 RTPC store, or null when no RTPC should be pulled.</param>
    /// <param name="key">The voice's RTPC key (gapC 1.3).</param>
    /// <param name="rng">The global LCG, required when <paramref name="ranges"/> is non-null (gapC 1.5).</param>
    /// <param name="doBusCheck">The native's <c>bDoBusCheck</c> flag (gapC 1.2).</param>
    public static void GetAudioParameters(
        WwiseGainNode? node,
        WwiseParamSelect paramSelect,
        ref WwiseAudioParameters io,
        WwiseGainRanges? ranges = null,
        WwiseRtpcStore? store = null,
        WwiseGainRtpcKey key = default,
        WwiseRng? rng = null,
        bool doBusCheck = true)
    {
        if (node is null) return;
        if (ranges is not null && rng is null)
            throw new ArgumentNullException(nameof(rng),
                "the randomizer ranges are drawn from the global LCG (gapC 1.5); pass one or pass null ranges");

        // gapC 1.3, 1.4: the base and gated bundles run the select-gated path; the global (node+0x24) and
        // game-object (node+0x48) bundles add props 0/2/3/4 with no select test, never prop 6, and they
        // alone insert MuteRatio into the node-keyed muted map.
        AddBundle(node.Props, node.Rtpcs, paramSelect, ref io, key, store);
        if (node.GatedBundleEnabled && node.GatedBundle is not null)
            AddBundle(node.GatedBundle, null, paramSelect, ref io, key, store);
        if (node.GlobalBundle is not null)
        {
            AddUngatedProps(node.GlobalBundle, ref io);
            InsertMuteRatio(node, node.GlobalBundle, WwiseMutedKey.GlobalFlag, ref io);   // 0x9EF484..0x9EF528
        }
        if (node.ObjectBundles is not null && node.ObjectBundles.TryGetValue(key.GameObject, out var objectBundle))
        {
            AddUngatedProps(objectBundle, ref io);
            InsertMuteRatio(node, objectBundle, WwiseMutedKey.ObjectFlag, ref io);         // 0x9F02C0..0x9F0378
        }

        // gapC 1.5: the randomizer ranged draw for this node, once per voice.
        if (ranges is not null && rng is not null) ranges.Add(node, rng);

        // gapC 1.7: game-defined aux, decided once at the first deciding node or the top node.
        if (!io.GameAuxDecided && (node.OverrideGameAux || node.Parent is null))
        {
            io.GameAuxDecided = true;
            io.GameAuxSendVolumeDb += Prop(node.Props, WwiseGainProps.GameAuxSendVolume)
                + Rtpc(node.Rtpcs, WwiseGainProps.GameAuxSendVolume, key, store);
            io.UseGameAux = node.UseGameDefinedAux;
        }

        // gapC 1.8: user aux, decided the same way.
        if (!io.UserAuxDecided && (node.OverrideUserAux || node.Parent is null))
        {
            io.UserAuxDecided = true;
            for (int i = 0; i < 4; i++)
            {
                byte id = (byte)(WwiseGainProps.UserAuxVolume0 + i);
                io.UserAuxVolumesDb[i] += Prop(node.Props, id) + Rtpc(node.Rtpcs, id, key, store);
                if (i < node.UserAuxIds.Count) io.UserAuxIds[i] = node.UserAuxIds[i];
            }
        }

        // gapC 1.2: the output-bus values and the two recursions.
        if (doBusCheck && node.OutputBus is not null)
        {
            AddOutputBusProps(node, ref io, key, store);
            GetAudioParameters(node.Parent, paramSelect, ref io, ranges, store, key, rng, doBusCheck: false);
            GetBusAudioParameters(node.OutputBus, paramSelect, ref io, store);
        }
        else
        {
            GetAudioParameters(node.Parent, paramSelect, ref io, ranges, store, key, rng, doBusCheck);
        }
    }

    /// <summary>
    /// Bus <c>GetAudioParameters</c> 0x9C1F8C (gapC 1.9): clears the Bus Volume select bit, sums the bus's
    /// props 0/2/3/4 plus RTPCs under the empty key (the native searches 0x9C2024/0x9C2574/0x9C2664/0x9C235C
    /// and never reads prop 6 or prop 0xB), adds <c>max(max-duck, Σ ducking)</c> to Volume, and recurses into
    /// the parent bus (<c>+0x38</c>).
    /// </summary>
    public static void GetBusAudioParameters(
        WwiseGainBus? bus,
        WwiseParamSelect paramSelect,
        ref WwiseAudioParameters io,
        WwiseRtpcStore? store = null)
    {
        if (bus is null) return;

        AddBusBundle(bus.Props, bus.Rtpcs, BusParamSelect(paramSelect), ref io, store);
        io.VolumeDb += MathF.Max(bus.MaxDuckDb, bus.DuckingDb);
        GetBusAudioParameters(bus.ParentBus, paramSelect, ref io, store);
    }

    /// <summary>The bus method's cleared select (gapC 1.9): paramSelect bit 0x10 (Bus Volume) is removed.</summary>
    public static WwiseParamSelect BusParamSelect(WwiseParamSelect paramSelect)
        => paramSelect & ~WwiseParamSelect.BusVolume;

    // ------------------------------------------------------------------ conversion and effective values

    /// <summary>
    /// <c>dBToLin</c>, the engine's fast pow (gapC 1.11). With <c>y = 0.05·dB</c>: <c>y &lt; −37</c> gives 0;
    /// otherwise <c>bits = u32(1065353216 + 27866352·y)</c> and the result is
    /// <c>float((bits&gt;&gt;23)&lt;&lt;23) · (0.6530434 + m(0.0208058 + 0.3251898m))</c> with
    /// <c>m</c> the renormalized mantissa <c>(bits &amp; 0x7FFFFF) | 0x3F800000</c>.
    /// </summary>
    public static float DbToLinear(float db)
    {
        float y = 0.05f * db;
        if (y < MinExponent) return 0f;
        uint bits = (uint)(OneBits + PowScale * y);
        return FastPow2(bits);
    }

    /// <summary>The fast <c>2^bits</c> reconstruction that <see cref="DbToLinear"/> wraps (gapC 1.11).</summary>
    private static float FastPow2(uint bits)
    {
        float scale = BitConverter.Int32BitsToSingle((int)((bits >> 23) << 23));
        float m = BitConverter.Int32BitsToSingle((int)((bits & 0x007FFFFFu) | 0x3F800000u));
        return scale * (0.6530434f + m * (0.0208058f + 0.3251898f * m));
    }

    /// <summary>
    /// <c>pbi+0x40 = max(0, Π muted ratios · pbi+0x168 · pbi+0x16C)</c> (gapC 1.11).
    /// <paramref name="mutedProduct"/> is the product across the muted map's distinct keys (gapC 1.4,
    /// <see cref="WwiseAudioParameters.MutedProduct"/>); the two fades are the caller's gapC 5.x values.
    /// </summary>
    public static float MuteFade(float mutedProduct, float fade168, float fade16C)
        => MathF.Max(0f, mutedProduct * fade168 * fade16C);

    /// <summary>
    /// The effective parameters a voice consumes (gapC 1.6, 1.11, gapE 1.1): the summed values plus the
    /// stored randomizer draw, the mute/fade product, and <c>voice+0x1C = dBToLin(pbi+0x3C) · pbi+0x40</c>.
    /// The LPF/HPF here are the node-chain plus randomizer terms only; M6-011's filter composition adds
    /// <c>pbi+0xA0</c>/<c>pbi+0xA8</c> (gapE 1.1).
    /// </summary>
    public static WwiseGainEffective Effective(
        WwiseAudioParameters io, WwiseGainRanges ranges, float fade168, float fade16C)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        float volume = io.VolumeDb + ranges.Volume;                 // pbi+0x3C
        float pitch = io.PitchCents + ranges.Pitch;                 // pbi+0x44
        float lowPass = io.LowPass + ranges.LowPass;                // pbi+0x9C's node-chain term
        float highPass = io.HighPass + ranges.HighPass;             // pbi+0xA4's node-chain term
        float mute = MuteFade(io.MutedProduct(), fade168, fade16C);  // pbi+0x40
        return new(volume, pitch, lowPass, highPass, io.MakeUpGain + ranges.MakeUpGain,
                   mute, DbToLinear(volume) * mute);                // voice+0x1C
    }

    /// <summary>
    /// The Sound override (0xA1DAB8, gapC 1.10): only when the MIDI key byte is not 0xFF,
    /// <c>pitch += 100·(note − root note)</c>. The root note's source, <c>0x9FAE18</c>, is RECOVERABLE_GAP,
    /// so it is a caller input.
    /// </summary>
    public static float SoundOverridePitchCents(byte midiKeyByte, float midiNote, float rootNote)
        => midiKeyByte == NoMidiKey ? 0f : 100f * (midiNote - rootNote);

    /// <summary>
    /// Applies the Sound override once for a Sound's voice (0xA1DAB8 runs after the base call, not per node):
    /// <c>io.PitchCents += 100·(note − root note)</c>, and nothing when the key byte is 0xFF.
    /// </summary>
    public static void ApplySoundOverride(ref WwiseAudioParameters io, byte midiKeyByte, float midiNote, float rootNote)
        => io.PitchCents += SoundOverridePitchCents(midiKeyByte, midiNote, rootNote);

    // ------------------------------------------------------------------ dry path and bus placement

    /// <summary>
    /// Voice refresh (0xA4B608, gapC 2.4): the dry entry gain is
    /// <c>regobj+0x60 × dBToLin(io+0x28 OutputBusVolume)</c>. The robot game objects set
    /// <c>SetGameObjectOutputBusVolume 0.0</c> (gapC 2.2, M6 A11), so their dry path is muted.
    /// </summary>
    public static float DryPathGain(float gameObjectOutputBusVolume, float outputBusVolumeDb)
        => gameObjectOutputBusVolume * DbToLinear(outputBusVolumeDb);

    /// <summary>
    /// Collapsed buses (gapC 2.7): a bus that is not instantiated folds its Bus Volume into the voice's dry
    /// OutputBusVolume instead, so it never reaches the aux send.
    /// </summary>
    public static float FoldCollapsedBusVolume(float outputBusVolumeDb, float busVolumeDb)
        => outputBusVolumeDb + busVolumeDb;

    /// <summary>
    /// gapC 3.2 (0xA4FEF8 → 0xA4D994): <c>GetResultingBuffer</c> runs the bus's FX loop first, then applies
    /// the bus gain <c>dBToLin(Bus Volume)</c> to that output. <paramref name="processFx"/> stands in for
    /// the FX chain (M6-013); the method's shape is the placement: the volume multiplies what the FX
    /// produced, so it is applied after them.
    /// </summary>
    public static float ApplyBusVolumeAfterFx(Func<float, float> processFx, float input, float busVolumeDb)
    {
        ArgumentNullException.ThrowIfNull(processFx);
        return processFx(input) * DbToLinear(busVolumeDb);
    }

    // ------------------------------------------------------------------ per-bundle arithmetic

    private static void AddBundle(
        IReadOnlyDictionary<byte, float> props,
        IReadOnlyList<WwiseRtpc>? rtpcs,
        WwiseParamSelect paramSelect,
        ref WwiseAudioParameters io,
        WwiseGainRtpcKey key,
        WwiseRtpcStore? store)
    {
        if (paramSelect.HasFlag(WwiseParamSelect.Volume))
            io.VolumeDb += Prop(props, WwiseGainProps.Volume) + Rtpc(rtpcs, WwiseGainProps.Volume, key, store);
        if (paramSelect.HasFlag(WwiseParamSelect.Pitch))
            io.PitchCents += Prop(props, WwiseGainProps.Pitch) + Rtpc(rtpcs, WwiseGainProps.Pitch, key, store);
        if (paramSelect.HasFlag(WwiseParamSelect.LowPass))
            io.LowPass += Prop(props, WwiseGainProps.LowPass) + Rtpc(rtpcs, WwiseGainProps.LowPass, key, store);
        if (paramSelect.HasFlag(WwiseParamSelect.HighPass))
            io.HighPass += Prop(props, WwiseGainProps.HighPass) + Rtpc(rtpcs, WwiseGainProps.HighPass, key, store);

        // gapC 1.3: MakeUpGain (prop 6) has no named select bit; it is added whenever the bundle has it.
        io.MakeUpGain += Prop(props, WwiseGainProps.MakeUpGain)
            + Rtpc(rtpcs, WwiseGainProps.MakeUpGain, key, store);

        // gapC 1.4: the base (node+0x3C) and gated (node+0x18) readers never search prop 0xB, so they do
        // not insert MuteRatio; only the global and object readers do (see GetAudioParameters).
    }

    /// <summary>
    /// The node+0x24 (global) and node+0x48 (game-object) bundles (gapC 1.4): props 0/2/3/4 are added
    /// unconditionally — the native readers 0x9EF2D8..0x9EF454 and 0x9F011C..0x9F0228 have no select test —
    /// and prop 6 is never searched.
    /// </summary>
    private static void AddUngatedProps(IReadOnlyDictionary<byte, float> props, ref WwiseAudioParameters io)
    {
        io.VolumeDb += Prop(props, WwiseGainProps.Volume);
        io.PitchCents += Prop(props, WwiseGainProps.Pitch);
        io.LowPass += Prop(props, WwiseGainProps.LowPass);
        io.HighPass += Prop(props, WwiseGainProps.HighPass);
    }

    /// <summary>
    /// The bus base bundle (0x9C1F8C, gapC 1.9): props 0/2/3/4 plus their RTPCs only. The native bus search
    /// never reads prop 6 or prop 0xB, and Bus Volume (prop 5) is cleared by <see cref="BusParamSelect"/>
    /// because gapC 3.2 applies it after the FX.
    /// </summary>
    private static void AddBusBundle(
        IReadOnlyDictionary<byte, float> props,
        IReadOnlyList<WwiseRtpc>? rtpcs,
        WwiseParamSelect paramSelect,
        ref WwiseAudioParameters io,
        WwiseRtpcStore? store)
    {
        if (paramSelect.HasFlag(WwiseParamSelect.Volume))
            io.VolumeDb += Prop(props, WwiseGainProps.Volume)
                + Rtpc(rtpcs, WwiseGainProps.Volume, WwiseGainRtpcKey.Empty, store);
        if (paramSelect.HasFlag(WwiseParamSelect.Pitch))
            io.PitchCents += Prop(props, WwiseGainProps.Pitch)
                + Rtpc(rtpcs, WwiseGainProps.Pitch, WwiseGainRtpcKey.Empty, store);
        if (paramSelect.HasFlag(WwiseParamSelect.LowPass))
            io.LowPass += Prop(props, WwiseGainProps.LowPass)
                + Rtpc(rtpcs, WwiseGainProps.LowPass, WwiseGainRtpcKey.Empty, store);
        if (paramSelect.HasFlag(WwiseParamSelect.HighPass))
            io.HighPass += Prop(props, WwiseGainProps.HighPass)
                + Rtpc(rtpcs, WwiseGainProps.HighPass, WwiseGainRtpcKey.Empty, store);
    }

    /// <summary>
    /// The muted-map insert (gapC 1.4, 1.11; native global 0x9EF484..0x9EF528, object 0x9F02C0..0x9F0378):
    /// keyed by the node plus <paramref name="flag"/>&amp;1 (1 from the global reader, 0 from the object
    /// reader), and the insert overwrites the value at its key. Only these two readers insert.
    /// </summary>
    private static void InsertMuteRatio(
        WwiseGainNode node, IReadOnlyDictionary<byte, float> props, byte flag, ref WwiseAudioParameters io)
    {
        if (props.TryGetValue(WwiseGainProps.MuteRatio, out float ratio) && ratio != 1f)
            io.MutedMap[new WwiseMutedKey(node.Id, (byte)(flag & 1))] = ratio;
    }

    private static void AddOutputBusProps(
        WwiseGainNode node, ref WwiseAudioParameters io, WwiseGainRtpcKey key, WwiseRtpcStore? store)
    {
        io.OutputBusVolumeDb += Prop(node.Props, WwiseGainProps.OutputBusVolume)
            + Rtpc(node.Rtpcs, WwiseGainProps.OutputBusVolume, key, store);
        io.OutputBusLowPass += Prop(node.Props, WwiseGainProps.OutputBusLowPass)
            + Rtpc(node.Rtpcs, WwiseGainProps.OutputBusLowPass, key, store);
        io.OutputBusHighPass += Prop(node.Props, WwiseGainProps.OutputBusHighPass)
            + Rtpc(node.Rtpcs, WwiseGainProps.OutputBusHighPass, key, store);
    }

    private static float Prop(IReadOnlyDictionary<byte, float> props, byte id)
        => props.TryGetValue(id, out float v) ? v : 0f;

    /// <summary>
    /// The RTPC pull (0xA11590, M6-009): the node's bindings whose <c>ParamId</c> is this prop are evaluated
    /// by the value store under the voice's key, and their accumulated value is added. The store owns the
    /// sum/product and the precedence.
    /// </summary>
    private static float Rtpc(IReadOnlyList<WwiseRtpc>? rtpcs, byte param, WwiseGainRtpcKey key, WwiseRtpcStore? store)
    {
        if (rtpcs is null || rtpcs.Count == 0 || store is null) return 0f;

        List<WwiseRtpc>? matched = null;
        foreach (var rtpc in rtpcs)
        {
            if (rtpc.ParamId != param) continue;
            (matched ??= new List<WwiseRtpc>()).Add(rtpc);
        }
        if (matched is null) return 0f;
        return (float)store.Evaluate(matched, key.GameObject, key.PlayingId, out _);
    }
}
