// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The Play-created playing instance — the base PBI class (M6-025 B5/B6, <c>0xA000E8</c>; vtable
/// <c>0x103B768</c>). It carries the fields the bridge rows name; fields whose meaning is UNKNOWN are
/// caller inputs rather than invented values.
///
/// <para><b>Settled stores (B6).</b> <c>+0x140</c> playing id, <c>+0x150</c> source descriptor,
/// <c>+0x14C</c> target node, <c>+0x164</c>/<c>+0x168</c>/<c>+0x16C</c> = 1.0, <c>+0x1D8</c> start
/// offset, the chain id (<c>+0x1C8</c> and the <c>+0x1BE</c> bit3), <c>+0x1BD</c> =
/// <c>(continuous)&lt;&lt;7 | 0x44</c>, <c>+0x1BE</c> bits 4..6 from <c>params+0x128</c>,
/// <c>+0x1BF</c> bit2, <c>+0x1F8</c>, <c>+0x1E4</c> = <c>params+0x84</c>, the <c>+0x15C</c>/<c>+0x15D</c>
/// placeholders and the 0x44-byte <c>+0x170</c> block copied from <c>params+0x28</c>.</para>
///
/// <para><b>UNKNOWN, caller-supplied.</b> <c>+0x1F8</c>/<c>+0x1E4</c>/<c>+0x14C</c> meanings
/// (residual); the <c>+0x15C</c>/<c>+0x15D</c> placeholder source bytes; the <c>+0x14</c> RTPC key,
/// whose init <c>0x9BC90C</c> is a RECOVERABLE_GAP; and the 0x44-byte block's contents on the Sound path.</para>
/// </summary>
public sealed class WwisePlayingInstance
{
    private static long s_nextChainId;

    // fidelity: M6-026
    // 0x108DC18 (8.6, C28.1): the process-wide creation counter written to pbi+0x1C4, initial 0, post-incremented
    // (0xA00348..0xA0037C). The other key, pbi+0x1C8, is the chain id below (0x1052434, initial 1).
    private static long s_nextKey1C4;

    /// <summary><c>+0x140</c>: the playing id.</summary>
    public uint PlayingId { get; }

    /// <summary><c>+0x150</c>: the source descriptor the source factory reads.</summary>
    public object SourceDescriptor { get; }

    /// <summary><c>+0x14C</c>: the target node (meaning UNKNOWN beyond "the creator's node").</summary>
    public uint TargetNodeId { get; }

    /// <summary><c>+0x14</c>: the RTPC key (init <c>0x9BC90C</c>, RECOVERABLE_GAP) — caller input.</summary>
    public object? RtpcKey14 { get; }

    /// <summary><c>+0x164</c>: the resampling ratio; 1.0 at creation.</summary>
    public float Ratio { get; set; } = 1f;

    /// <summary><c>+0x168</c>: fade value 1; 1.0 at creation.</summary>
    public float Fade168 { get; set; } = 1f;

    /// <summary><c>+0x16C</c>: fade value 2; 1.0 at creation.</summary>
    public float Fade16C { get; set; } = 1f;

    /// <summary><c>+0x1D8</c>: the start offset in samples (<c>params+0x74</c>).</summary>
    public uint StartOffset { get; set; }

    /// <summary><c>+0x1C8</c>: the chain id (auto-allocated when <c>params+0x7C == 0</c>).</summary>
    public uint ChainId { get; }

    /// <summary><c>+0x1BD</c>: <c>(continuous)&lt;&lt;7 | 0x44</c> at creation. <c>0xA01CA4</c> (M6-026 K1) writes bit 1 and the
    /// reason in bits 2..4; <c>0xA01684</c> (R3) tests and sets bit 5.</summary>
    public byte Flags1BD { get; set; }

    // fidelity: M6-026
    /// <summary><c>+0x1C0</c>: the priority, copied from the ctor's argument block (<c>[sp+0x38]</c>, <c>0xA002D4..0xA002E4</c>,
    /// 8.6). The limiter lists sort on it (8.1); the walker's victim choice compares it (V1).</summary>
    public float Priority1C0 { get; set; }

    /// <summary><c>+0x1C4</c>: the creation sequence number from <c>[0x108DC18]++</c> (initial 0), the second tie key (8.2, 8.3, 8.6).
    /// The setter exists so a test can place a PBI on a key; production code only writes it in the constructor.</summary>
    public uint Key1C4 { get; set; }

    /// <summary><c>+0xE0</c>: the PBI's node (the walker's <c>node vt+0x94</c> call in Term reads it, R3; the victim choice writes it to
    /// its unread out-parameter, V3). Set by the Play path that created the PBI.</summary>
    public WwiseNode? NodeE0 { get; set; }

    /// <summary><c>+0x14</c>: word 0 of the key <c>0x9BC90C(pbi+0xC, [params+8], ..)</c> copies (5.2), written only by the base ctor
    /// <c>0xA000E8</c>. <c>[params+8]</c> is the registered-object identity for an object-scope action and 0 otherwise (5.3, C29.7), so this is
    /// <c>params.GameObjectId</c>: equality with the per-object limiter map key and with <c>go</c> in <c>0xA37100</c> (V1) is identity equality; null is 0.</summary>
    public uint? GameObject14 { get; set; }

    /// <summary>
    /// <c>+0x64</c> (ctx <c>+0x58</c>): the third input of the volume-threshold product in <c>0x9BEB30</c> (C29.4, 3.2). CalcEffectiveParams writes it: <c>0x9FFB64</c> zeroes it only on the reset
    /// path (<c>0x9FFB10</c>; skipped when <c>[r7+0x90] == r6</c>), then <c>0x9FFDF0..0x9FFE04</c> add <c>s16</c>, which is 0.0f only on the <c>0x9FFC1C</c> and <c>0x9FFE88</c> paths and is
    /// <c>0x9C39DC(r6, .., 5)</c> on the <c>0x9FFEB0</c> path (taken when <c>0x9C54E8(r6) == 0</c>, <c>r6</c> = the first output bus <c>0x9F4BB8(node)</c>); <c>0x9C54E8</c> and <c>0x9C39DC</c> are
    /// unread. With <c>[pbi+0xE9] &amp; 4 == 0</c> a <c>memcpy(pbi+0x3C, r7, 0x5C)</c> (<c>0x9FFEE4..0x9FFEF4</c>, reached from <c>0x9FFB04..0x9FFB0C</c> via <c>0x9FFED4</c>) overwrites it too: a third
    /// producer. The C# CalcEffectiveParams does not model those writes, so there is no default: the value is null until the caller supplies it, and reading it unset throws.
    /// </summary>
    public float? Word64 { get; set; }

    /// <summary>Reads <see cref="Word64"/>; unset is a named stop (0x9FFDF0..0x9FFE04, 0x9FFEB0: the value comes from the unread 0x9C54E8 / 0x9C39DC).</summary>
    public float ReadWord64() => Word64 ?? throw new WwiseMissingBehaviourException(
        "M6-026 3.2: pbi+0x64 is written by CalcEffectiveParams (0x9FFB64, 0x9FFDF0..0x9FFE04) from 0x9C54E8 / 0x9C39DC (0x9FFEB0), which are unread; set PBI.Word64");

    /// <summary><c>+0xE8</c> (ctx <c>+0xDC</c>): bit 5 selects the context call at the tail of <c>0xA379D8</c> (<c>0xA38010..0xA38018</c>). CalcEffectiveParams sets it on its early return (<c>0x9FFC30</c>, <c>[pbi+0xE9] &amp; 4</c>).</summary>
    public byte Flags0E8 { get; set; }

    /// <summary><c>+0x1CC</c> and <c>+0x1D0</c>: the last priority and distance offset (8.4). The ctor copies the <c>{priority, offset}</c> block <c>0xA379D8</c> passes (<c>0xA002D4..0xA002E4</c>);
    /// the Play path sets them at creation.</summary>
    public float Field1CC { get; set; }

    /// <summary>See <see cref="Field1CC"/>.</summary>
    public float Field1D0 { get; set; }

    /// <summary><c>+0xE4</c> (ctx <c>+0xD8</c>): the <c>r1</c> stored by <c>0x9BEB30</c> (A1), the result of node <c>vt+0x84</c> in <c>0xA379D8</c>.</summary>
    public uint FieldE4 { get; set; }

    /// <summary><c>+0x34</c>: tested by the type-1 join of <c>0xA0067C</c> (<c>0x9E808C</c> runs when non-zero, O5). Nothing built writes it.</summary>
    public uint Field34 { get; set; }

    /// <summary><c>+0xE9</c>: bit 0 is set by <c>0x9FF41C</c>; bit 2 gates the global voice list append (R1) and the re-registration
    /// <c>0xA00494</c> (8.7). Nothing built writes it.</summary>
    public byte Flags0E9 { get; set; }

    /// <summary><c>+0x148</c>: the pause/stop transition item, as its id (0 pointer is <c>null</c>). <c>0xA01CA4</c> tests it through
    /// <c>0xA35980</c> (K2); <c>0x9FF7B8</c> cancels it (K6).</summary>
    public uint? Item148Id { get; set; }

    /// <summary><c>+0xAC</c>: the 3D emitter word (<c>0x9FEEB8</c> path, K6). Not on shipped data; a non-null value makes the mark throw.</summary>
    public object? Field0AC { get; set; }

    /// <summary><c>+0x1EC/+0x1F0/+0x1F4</c>: the array of limiter lists this PBI is in (the walk appends, <c>0xA0285C</c> inserts, Term removes).</summary>
    public WwiseLimiterArray LimiterArray1EC { get; } = new();

    /// <summary><c>+0x1BE</c>: bit3 set when the chain id came from <c>params+0x7C</c>; bit6 from
    /// <c>params+0x128</c> bit4 (<c>0xA002A8 ubfx r3,r3,#4,#1; 0xA002AC bfi r1,r3,#6,#1</c>).</summary>
    public byte Flags1BE { get; set; }

    /// <summary><c>+0x1BF</c>: bit2 from <c>params+0x128</c> bit2.</summary>
    public byte Flags1BF { get; }

    /// <summary>
    /// <c>+0x1F8</c>: the ctor stores <c>0xFFFFFFFF</c> (<c>0xA0021C mvn r7,#0</c>; <c>0xA00318
    /// str r7,[r4,#0x1f8]</c>). The drain tests it as the -1 sentinel (<c>0x9D36C0</c>).
    /// </summary>
    public uint Field1F8 { get; } = 0xFFFFFFFF;

    /// <summary><c>+0x1BA</c>: read by <c>0xA0067C</c> (<c>&amp;7 != 1</c> chooses start-list type 0).</summary>
    public byte Flags1BA { get; set; }

    /// <summary>
    /// <c>+0x1BC</c>: bit0 set by CalcEffectiveParams (<c>0x9FFE7C</c>); bit7 set by PBI Play's type-1
    /// branch (<c>0xA006EC</c>); bit5 read by the drain (<c>0x9D3644</c>).
    /// </summary>
    public byte Flags1BC { get; set; }

    /// <summary>
    /// <c>+0x144</c>: stored by PBI Play's fade-in branch (<c>0xA0067C</c>, after <c>0xA00814</c>). The
    /// value's source is not in the cited instructions, so it is supplied by the transition seam.
    /// </summary>
    public uint Field144 { get; set; }

    /// <summary>
    /// <c>+0x154</c>: AddSrc stores the voice here (<c>0xA558F8 str r4,[r7,#0x154]</c>); the drain skips
    /// any PBI whose <c>+0x154 != 0</c> (<c>0x9D36B4/0x9D36BC</c>), so an attached PBI is not re-attached.
    /// </summary>
    public WwiseLiveVoice? Field154 { get; set; }

    /// <summary><c>+0x1E4</c>: <c>params+0x84</c> (the Sound special-branch byte; meaning UNKNOWN).</summary>
    public byte Field1E4 { get; }

    /// <summary>
    /// <c>+0x15C</c> (low byte) and <c>+0x15D</c>: the word the voice init copies to <c>[voice+0xF0]</c>
    /// (<c>0xA54B70</c>, C24.2/C25.5). The ctor stores 0 at <c>0xA001F0</c> and the value is the
    /// <c>0x00004101</c> default from <c>0xA00338..0xA00374</c> (C23.8) only until the source StartStream classes overwrite
    /// it (C26.5; the writers are the bridge's required <c>SourceFormatWriter15C</c> seam).
    /// </summary>
    // fidelity: M6-025
    public uint Word15C { get; set; } = 0x00004101;

    /// <summary><c>+0x170</c>: the 0x44-byte block copied from <c>params+0x28</c>.</summary>
    public byte[] Block170 { get; }

    /// <summary>
    /// <c>+0x1DC</c> (the media data pointer) and <c>+0x1E0</c> (its size): the <c>r1</c>/<c>r2</c> of every <c>0xA56650</c> call
    /// (<c>0xA5590C</c>, <c>0xA55910</c>, <c>0xA544D8</c>, <c>0xA544DC</c>; M6-025 C27 step 7, C30.1(d)). The constructor stores 0 in both (<c>0xA00180</c>, <c>0xA002F0</c>, <c>0xA002F4</c>; C31.1 R1.1);
    /// the other writers are <c>0xA0207C</c> (inside <c>0xA01EF4</c>), Term (<c>0xA02AF0</c>, <c>+0x1DC</c> only) and the out-pointers of <c>0xA1EC54</c> at <c>0xA02928</c> / <c>0xA0292C</c>
    /// (<see cref="WwisePbiMedia.StoreSourceInfoA02924"/>). For a streamed sound the pair is the prefetch prefix, not the whole media (C32.2).
    /// </summary>
    // fidelity: M6-025
    public uint Word1DC { get; set; }

    /// <summary>See <see cref="Word1DC"/>.</summary>
    // fidelity: M6-025
    public uint Word1E0 { get; set; }

    /// <summary>Reads <see cref="Word1DC"/> (<c>ldr r1,[pbi,#0x1dc]</c>).</summary>
    // fidelity: M6-025
    public uint Read1DC() => Word1DC;

    /// <summary>Reads <see cref="Word1E0"/> (<c>ldr r2,[pbi,#0x1e0]</c>).</summary>
    // fidelity: M6-025
    public uint Read1E0() => Word1E0;

    /// <summary>
    /// <c>+0x108</c>: the bank object <c>0xA1EC54</c> picked (<c>*r3</c>); the constructor stores 0 (<c>0xA00164</c>). Term runs its <c>vt+0</c> and zeroes it (<c>0xA02AF4..0xA02B14</c>).
    /// </summary>
    // fidelity: M6-025
    public WwiseMediaBank? Obj108 { get; set; }

    /// <summary>
    /// <c>+4</c>: the callback-flag word. The constructor stores 0 (<c>0xA0010C</c>); <c>0xA04D48</c> stores the playing-id item's <c>[item+0x48]</c> here (<c>0xA04DE0</c>, R1.5). Bit <c>0x100000</c> is tested by
    /// <c>0xA56414</c> and <c>0xA56478</c>.
    /// </summary>
    // fidelity: M6-025
    public uint Flags4 { get; set; }

    /// <summary><c>+0x158</c>: the source-format word; written by the source StartStream.</summary>
    public uint SourceFormat158 { get; set; }

    // fidelity: M6-025
    /// <summary><c>+0x160</c> (byte): written by the streamed Vorbis header parse <c>0xAB12B4</c> (<c>0xAB1394</c>, 0x20).</summary>
    public byte Byte160 { get; set; }

    // fidelity: M6-025
    /// <summary><c>+0x161</c> (byte): the channel count the header parse stores (<c>0xAB1388</c>).</summary>
    public byte Byte161 { get; set; }

    // fidelity: M6-025
    /// <summary><c>+0x162</c> (byte): bits 0 and 1 and bit 2 are written by the header parse (<c>0xAB1354..0xAB1370</c>).</summary>
    public byte Byte162 { get; set; }

    // fidelity: M6-025
    /// <summary><c>+0x1B4</c>: the start offset's sample count (<c>0xA74C64</c> stores <c>pos - [S+0x18]</c>; <c>0xAB2284</c> clears it; <c>0xAB227C</c> reads it).</summary>
    public uint Word1B4 { get; set; }

    /// <summary>
    /// <c>+0x1BB</c> (C27, <c>0xA01768</c>): the cache byte. Bits 0..2 are the out value, bits 3..6 the return value,
    /// bit 7 the "cached" mark set by the first call.
    /// </summary>
    // fidelity: M6-025
    public byte NextSourceCache1BB { get; set; }

    /// <summary><c>+0x3C</c>: the effective Volume in dB (CalcEffectiveParams).</summary>
    public float Volume3C { get; set; }

    /// <summary><c>+0x40</c>: the mute/fade linear factor (CalcEffectiveParams). <c>0xA01CA4</c> writes 0.0f (M6-026 K5).</summary>
    public float MuteFade40 { get; set; } = 1f;

    /// <summary><c>+0x44</c>: the effective Pitch in cents (CalcEffectiveParams).</summary>
    public float Pitch44 { get; set; }

    /// <summary><c>+0x48</c>: the effective LPF (CalcEffectiveParams).</summary>
    public float Lpf48 { get; set; }

    /// <summary><c>+0x4C</c>: the effective HPF (CalcEffectiveParams).</summary>
    public float Hpf4C { get; set; }

    // ---- the PBI context and effective-parameter block (C31.3, C32.1): written by CalcEffectiveParams 0x9FFAD4 and 0x9BEB30 (WwisePlayPath)

    /// <summary><c>+0x50</c>, <c>+0x54</c>, <c>+0x5C</c>, <c>+0x68</c>, <c>+0x6C</c>: floats the reset stage <c>0x9FFB10</c> zeroes.</summary>
    public float Field50 { get; set; }

    /// <summary>See <see cref="Field50"/>.</summary>
    public float Field54 { get; set; }

    /// <summary>See <see cref="Field50"/>.</summary>
    public float Field5C { get; set; }

    /// <summary>See <see cref="Field50"/>.</summary>
    public float Field68 { get; set; }

    /// <summary>See <see cref="Field50"/>.</summary>
    public float Field6C { get; set; }

    /// <summary><c>+0x58</c> (byte): bits 0..1 cleared by the reset stage.</summary>
    public byte Byte58 { get; set; }

    /// <summary><c>+0x60</c> (byte): bits 0..1 cleared by the reset stage.</summary>
    public byte Byte60 { get; set; }

    /// <summary><c>+0x70..+0x7F</c>: 16 bytes the reset stage zeroes (<c>0x9FFB70</c>).</summary>
    public byte[] Block70 { get; } = new byte[16];

    /// <summary><c>+0x80..+0x8F</c>: 16 bytes the reset stage zeroes (<c>0x9FFB80</c>).</summary>
    public byte[] Block80 { get; } = new byte[16];

    /// <summary><c>+0x90</c>: zeroed by the reset stage.</summary>
    public uint Word90 { get; set; }

    /// <summary><c>+0x94..+0x97</c> (bytes): zeroed by the reset stage; <c>+0x95</c> and <c>+0x96</c> again before node <c>vt+0xAC</c> (<c>0x9FFCE0</c>, <c>0x9FFCE8</c>).</summary>
    public byte Byte94 { get; set; }

    /// <summary>See <see cref="Byte94"/>.</summary>
    public byte Byte95 { get; set; }

    /// <summary>See <see cref="Byte94"/>.</summary>
    public byte Byte96 { get; set; }

    /// <summary>See <see cref="Byte94"/>.</summary>
    public byte Byte97 { get; set; }

    /// <summary><c>+0x98</c>: <c>pbi+0x3C</c> before the randomizer volume is added (<c>0x9FFD60</c>); <c>0x9FF368</c> adds <c>+0x118</c> to it.</summary>
    public float Field98 { get; set; }

    /// <summary><c>+0x9C</c>: the LPF node-chain term (<c>0x9FFD48</c>).</summary>
    public float Field9C { get; set; }

    /// <summary><c>+0xA0</c>: added to the LPF (<c>0x9FFD54</c>; M6-011's term).</summary>
    public float FieldA0 { get; set; }

    /// <summary><c>+0xA4</c>: the HPF node-chain term (<c>0x9FFD58</c>).</summary>
    public float FieldA4 { get; set; }

    /// <summary><c>+0xA8</c>: added to the HPF (<c>0x9FFD64</c>).</summary>
    public float FieldA8 { get; set; }

    /// <summary><c>+0xC4</c> (ctx <c>+0xB8</c>): 101.0f from the ctx init <c>0x9BCA48</c> and from CalcEffectiveParams (<c>0x9FFC9C</c>).</summary>
    public float FieldC4 { get; set; }

    /// <summary><c>+0xB4</c>, <c>+0xB8</c>, <c>+0xBC</c> (ctx <c>+0xA8..+0xB0</c>): properties 0xC, 0xD, 0xE of the top node (<c>0x9FAEE8</c>).</summary>
    public float PanB4 { get; set; }

    /// <summary>See <see cref="PanB4"/>.</summary>
    public float PanB8 { get; set; }

    /// <summary>See <see cref="PanB4"/>.</summary>
    public float PanBC { get; set; }

    /// <summary><c>+0xC0</c> (byte; ctx <c>+0xB4</c>): <c>[top+0x47] &amp; 1</c> (<c>0x9FAFE4</c>).</summary>
    public byte PanC0 { get; set; }

    /// <summary><c>+0xDC</c> (ctx <c>+0xD0</c>): the context object <c>0x9FB9B8</c> allocates for a node with a 3D positioning object. None is representable, so it is null.</summary>
    public object? CtxD0 { get; set; }

    /// <summary><c>+0x118..+0x12C</c>: the randomizer ranges (<see cref="WwiseGainRanges"/>: volume <c>+0x118</c>, make-up <c>+0x11C</c>, pitch <c>+0x120</c>, LPF <c>+0x124</c>, HPF <c>+0x128</c>).</summary>
    public WwiseGainRanges Ranges118 { get; } = new();

    /// <summary><c>+0x10C</c> (data), <c>+0x110</c> (count): the transition records node <c>vt+0xAC</c> fills and CalcEffectiveParams prunes and multiplies.</summary>
    public List<WwiseTransitionRecord> Transitions10C { get; } = new();

    /// <summary><c>+0x1B8</c> (u16): the loop count <c>0xA00618</c> stores (<c>0xA00634</c>) and the streamed Vorbis source reads (S4).</summary>
    public ushort LoopCount1B8 { get; set; }

    /// <summary>
    /// <c>0xA000E8</c>'s stores (B6). <paramref name="block28"/> is the 0x44-byte block (copied to
    /// <c>+0x170</c>); <paramref name="rtpcKey14"/> is the UNKNOWN field, supplied by the caller. <c>+0x1F8</c> is the settled <c>0xFFFFFFFF</c>.
    /// </summary>
    public WwisePlayingInstance(
        WwisePlayInitParams p, uint targetNodeId, object sourceDescriptor, byte[] block28,
        object? rtpcKey14, bool continuous, bool ctxNodeChainFlag = false)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(sourceDescriptor);
        ArgumentNullException.ThrowIfNull(block28);
        if (block28.Length != 0x44)
            throw new ArgumentException($"the params block is 0x44 bytes, not {block28.Length}", nameof(block28));

        PlayingId = p.PlayingId;
        // 0x9BC90C (the ctx init, C32.1 P8 with the verifier's 0x5D): [ctx+0xDC] = 0x5D, [ctx+0xDD] = bit 0 set, bit 1 clear, bit 2 = the 4th argument (params+0x128 bit 3, 0xA0013C), bit 3 = the node-chain test
        // 0x9BC9FC..0x9BCA1C; the high nibble is uninitialised pool memory and is taken as 0. [ctx+0xB8] = 101.0f (0x9BCA40..0x9BCA48).
        Flags0E8 = 0x5D;
        Flags0E9 = (byte)(1 | (((p.Flags128 >> 3) & 1) << 2) | (ctxNodeChainFlag ? 8 : 0));
        FieldC4 = BitConverter.Int32BitsToSingle(0x42CA0000);
        GameObject14 = p.GameObjectId;                                    // 0xA00104/0xA00144: [params+8] -> 0x9BC90C -> pbi+0x14 (M6-026 5.2)
        TargetNodeId = targetNodeId;
        SourceDescriptor = sourceDescriptor;
        RtpcKey14 = rtpcKey14;
        Field1F8 = 0xFFFFFFFF;                                            // 0xA0021C/0xA00318
        Field1E4 = p.SoundSpecial84;
        Word15C = 0x00004101;                                             // C23.8: 0xA00338..0xA00374
        Block170 = (byte[])block28.Clone();
        StartOffset = p.InitialDelaySamples;
        Ratio = 1f;
        Fade168 = 1f;
        Fade16C = 1f;

        // 0xA002A8/0xA002AC: params+0x128 bit4 -> pbi+0x1BE bit6 only. 0xA002D0: params+0x128 bit2 ->
        // pbi+0x1BF bit2. These stores precede the chain-id stores at 0xA00334/0xA00418, so the chain bit3
        // is applied after them.
        Flags1BD = (byte)((continuous ? 1 << 7 : 0) | 0x44);              // 0xA00288
        Flags1BE = (byte)((Flags1BE & ~0x40) | ((p.Flags128 & 0x10) != 0 ? 0x40 : 0));
        Flags1BF = (byte)((p.Flags128 & 4) != 0 ? 4 : 0);

        // fidelity: M6-026 (8.6): pbi+0x1C4 = [0x108DC18]++ (initial 0), unconditional, 0xA00348..0xA0037C.
        Key1C4 = unchecked((uint)(Interlocked.Increment(ref s_nextKey1C4) - 1));

        // 0xA00334/0xA00410: a zero params+0x7C takes the global 0x1052434++; otherwise the id is taken
        // and pbi+0x1BE bit3 is set (0xA00418).
        if (p.ChainId == 0)
        {
            ChainId = unchecked((uint)Interlocked.Increment(ref s_nextChainId));
        }
        else
        {
            ChainId = p.ChainId;
            Flags1BE |= 8;
        }
    }

    /// <summary>
    /// <c>0xA01878(pbi)</c> (M6-025 F3): sets <c>pbi+0x1BA</c> bits 3..6 = 3
    /// (<c>ldrb r3,[r0,#0x1ba]; mov r2,#3; bfi r3,r2,#3,#4; strb</c>). Called on the chain-match path.
    /// </summary>
    public void MarkChainMatchedA01878()
        => Flags1BA = (byte)((Flags1BA & ~0x78) | (3 << 3));
}
