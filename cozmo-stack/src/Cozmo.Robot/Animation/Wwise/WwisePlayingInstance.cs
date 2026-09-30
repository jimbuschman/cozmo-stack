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

    /// <summary><c>+0x1BD</c>: <c>(continuous)&lt;&lt;7 | 0x44</c>.</summary>
    public byte Flags1BD { get; }

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

    /// <summary><c>+0x158</c>: the source-format word; written by the source StartStream.</summary>
    public uint SourceFormat158 { get; set; }

    /// <summary>
    /// <c>+0x1BB</c> (C27, <c>0xA01768</c>): the cache byte. Bits 0..2 are the out value, bits 3..6 the return value,
    /// bit 7 the "cached" mark set by the first call.
    /// </summary>
    // fidelity: M6-025
    public byte NextSourceCache1BB { get; set; }

    /// <summary><c>+0x3C</c>: the effective Volume in dB (CalcEffectiveParams).</summary>
    public float Volume3C { get; private set; }

    /// <summary><c>+0x40</c>: the mute/fade linear factor (CalcEffectiveParams).</summary>
    public float MuteFade40 { get; private set; } = 1f;

    /// <summary><c>+0x44</c>: the effective Pitch in cents (CalcEffectiveParams).</summary>
    public float Pitch44 { get; private set; }

    /// <summary><c>+0x48</c>: the effective LPF (CalcEffectiveParams).</summary>
    public float Lpf48 { get; private set; }

    /// <summary><c>+0x4C</c>: the effective HPF (CalcEffectiveParams).</summary>
    public float Hpf4C { get; private set; }

    /// <summary>
    /// <c>0xA000E8</c>'s stores (B6). <paramref name="block28"/> is the 0x44-byte block (copied to
    /// <c>+0x170</c>); <paramref name="rtpcKey14"/> is the UNKNOWN field, supplied by the caller. <c>+0x1F8</c> is the settled <c>0xFFFFFFFF</c>.
    /// </summary>
    public WwisePlayingInstance(
        WwisePlayInitParams p, uint targetNodeId, object sourceDescriptor, byte[] block28,
        object? rtpcKey14, bool continuous)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(sourceDescriptor);
        ArgumentNullException.ThrowIfNull(block28);
        if (block28.Length != 0x44)
            throw new ArgumentException($"the params block is 0x44 bytes, not {block28.Length}", nameof(block28));

        PlayingId = p.PlayingId;
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

    /// <summary>
    /// The PBI <c>vt+0x44</c> CalcEffectiveParams (M6-025 B9, <c>0x9FFAD4</c>). <paramref name="gainNode"/>
    /// is the node chain's M6-010 model (its builder is M6-010's); <paramref name="ranges"/> is the stored
    /// randomizer draw. A null node is the <c>param_2 == 0</c> reset branch (<c>0x9FFB24/0x9FFB48</c>):
    /// Volume 0, mute/fade 1.
    /// </summary>
    public WwiseGainEffective CalcEffectiveParams(
        WwiseGainNode? gainNode, WwiseParamSelect paramSelect, WwiseGainRanges? ranges,
        WwiseRtpcStore? store = null, WwiseGainRtpcKey key = default, WwiseRng? rng = null)
    {
        var effective = WwiseCalcEffectiveParams.Calculate(
            gainNode, paramSelect, ranges, store, key, rng, Fade168, Fade16C);
        Volume3C = effective.VolumeDb;
        Pitch44 = effective.PitchCents;
        Lpf48 = effective.LowPass;
        Hpf4C = effective.HighPass;
        MuteFade40 = effective.MuteFade;
        Flags1BC |= 1;                                                    // 0x9FFE7C: pbi+0x1bc |= 1
        return effective;
    }
}

/// <summary>
/// The PBI <c>vt+0x44</c> CalcEffectiveParams composition (M6-025 B9). The node-chain sum is M6-010's
/// <see cref="WwiseGain.GetAudioParameters"/> (<c>vt+0xac</c>); the compose (<c>0x9FFD14..0x9FFD74</c>)
/// and the mute/fade product (<c>0x9FFDD0..0x9FFE18</c>) are M6-010's
/// <see cref="WwiseGain.Effective"/>.
/// </summary>
public static class WwiseCalcEffectiveParams
{
    /// <summary>B9: a null node is the reset branch (<c>0x9FFB24/0x9FFB48</c>).</summary>
    public static WwiseGainEffective Reset() => new(0f, 0f, 0f, 0f, 0f, 1f, 1f);

    /// <summary>
    /// B9 3.3..3.6: accumulate the node chain, then compose the effective fields. The <c>0x9FBE74</c> bus
    /// accumulation and the <c>0x9F6B94</c> RTPC update are not read; the node chain passed in is the
    /// caller's M6-010 graph.
    /// </summary>
    public static WwiseGainEffective Calculate(
        WwiseGainNode? gainNode, WwiseParamSelect paramSelect, WwiseGainRanges? ranges,
        WwiseRtpcStore? store, WwiseGainRtpcKey key, WwiseRng? rng, float fade168, float fade16C)
    {
        if (gainNode is null) return Reset();

        var io = WwiseAudioParameters.Create();
        // 0x9FFC68 0x9FBE74 / 0x9FFCEC vt+0xac GetAudioParameters: the node chain's sum. The ranged draw
        // runs only when the pass has the global LCG; the stored ranges are still composed afterwards.
        var drawRanges = rng is null ? null : ranges;
        WwiseGain.GetAudioParameters(gainNode, paramSelect, ref io, drawRanges, store, key, rng);
        // 0x9FFD14..0x9FFD74 compose and 0x9FFDD0..0x9FFE18 mute/fade product.
        return WwiseGain.Effective(io, ranges ?? new WwiseGainRanges(), fade168, fade16C);
    }
}