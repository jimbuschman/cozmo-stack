// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The seam the M6-006 control path calls after a Play target resolves (M6-025 B1). The runtime hands over
/// the resolved node, the playing id, the game object and the Play params struct it built; the bridge owns
/// the Play -> PBI -> voice -> source creation path.
/// </summary>
public interface IWwisePlaybackBridge
{
    /// <summary>
    /// The node's <c>vt+0x128</c> PlayInternal dispatch (<c>0xA62D14/0xA62D20/0xA62D24</c>).
    /// </summary>
    void OnPlay(WwiseNode node, uint playingId, uint? gameObject, WwisePlayInitParams p);
}

/// <summary>
/// One node of the PBI start list (M6-025 B17, <c>0x9D3558</c>): <c>{next, pbi, tick}</c>.
/// </summary>
public sealed class WwiseStartListNode
{
    // fidelity: M6-025
    public required WwisePlayingInstance Pbi { get; init; }
    public required int Type { get; init; }
    public required long Tick { get; init; }

    /// <summary>
    /// <c>node+0xd</c>: the folded return of <c>0xA42DEC</c> on the <c>AddSrc==1</c> path
    /// (<c>0xA4317C..0xA43194</c>). The list node is <c>{next,pbi,tick}</c>, so <c>+0xd</c> is the byte the
    /// native writes there.
    /// </summary>
    public byte D { get; set; }
}

/// <summary>
/// The PBI start list (M6-025 B17): head <c>0x108DA10</c>, tail <c>0x108DA14</c>, count <c>0x108DA24</c>,
/// gate <c>0x108DA34</c>. <c>0x9D3558</c> enqueues; <c>0x9D3644</c> (via <c>0x9D3C98</c>) drains, calling
/// the voice attach per ready PBI and keeping only the nodes the attach returns 1 for.
/// </summary>
public sealed class WwiseStartList
{
    // fidelity: M6-025

    /// <summary>The enqueued nodes (the native list head/tail).</summary>
    public List<WwiseStartListNode> Nodes { get; } = new();

    /// <summary><c>byte[0x108DA34]</c>: set when a type &lt; 2 node is enqueued; read by <c>0x9D3C98</c>.</summary>
    public bool Gate { get; set; }

    /// <summary><c>0x108DA24</c>: the count.</summary>
    public int Count => Nodes.Count;

    /// <summary>
    /// <c>0x9D3558(type, pbi)</c>: append the node; a type below 2 sets the gate.
    /// </summary>
    public void Enqueue(int type, WwisePlayingInstance pbi, long tick)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        Nodes.Add(new WwiseStartListNode { Pbi = pbi, Type = type, Tick = tick });
        if (type < 2) Gate = true;                                        // 0x9D3598
    }

    /// <summary>
    /// <c>0x9D3644</c>: for each ready node, call <paramref name="attach"/> and keep the node only when it
    /// returns 1 (<c>0x9D36EC</c>); otherwise unlink/free it. A node whose PBI already has a voice
    /// (<c>pbi+0x154 != 0</c>, <c>0x9D36B4/0x9D36BC</c>) is skipped, as is the
    /// <c>(pbi+0x1BC&amp;0x20) &amp;&amp; pbi+0x1F8==-1</c> case (<c>0x9D36C0</c>). Returns the kept nodes.
    /// </summary>
    public List<WwiseStartListNode> Drain(Func<WwiseStartListNode, int> attach)
    {
        ArgumentNullException.ThrowIfNull(attach);
        var kept = new List<WwiseStartListNode>();
        int i = 0;
        while (i < Nodes.Count)
        {
            var node = Nodes[i];
            // 0x9D36B4: [pbi+0x154] != 0 -> already attached; 0x9D36A0 advances to the next node WITHOUT
            // unlinking, so the node stays in the list.
            if (node.Pbi.Field154 is not null)
            {
                i++;
                continue;
            }
            // 0x9D36C0: not (pbi+0x1BC&0x20 and pbi+0x1F8 == -1).
            if ((node.Pbi.Flags1BC & 0x20) != 0 && node.Pbi.Field1F8 == 0xFFFFFFFF)
            {
                Nodes.RemoveAt(i);
                continue;
            }
            int r = attach(node);                                         // 0x9D36E8 -> 0xA4304C
            if (r == 1) { kept.Add(node); i++; }                          // 0x9D36EC
            else Nodes.RemoveAt(i);
        }
        return kept;
    }
}

/// <summary>
/// The Play -> PBI -> voice -> source creation bridge (M6-025). It implements the node <c>vt+0x128</c>
/// dispatch, the node <c>vt+0x14</c> PBI creator (<c>0xA02EC8</c>), the base PBI ctor (<c>0xA000E8</c>),
/// <c>CalcEffectiveParams</c> (<c>0x9FFAD4</c>), the voice attach (<c>0xA4304C</c>), <c>AddSrc</c>
/// (<c>0xA558AC</c>) with the source factory (<c>0xA562B8</c>), and the start list (<c>0x9D3558</c> /
/// <c>0x9D3644</c>).
///
/// <para><b>Sound path first, RanSeq second, other classes refused.</b> B2 gives the <c>+0x128</c>
/// PlayInternal per shipped class, but only the Sound body is read in this pass. RanSeq's container path
/// and every other class's <c>+0x128</c> body are UNKNOWN here, so they throw rather than being guessed.</para>
///
/// <para><b>Unread bodies are named seams, never silent defaults.</b> The Sound PlayInternal special branch
/// (<c>params+0x84 == 0x90</c>) is UNKNOWN and throws. <c>0x9BEB30</c> (B7: the result must be 1),
/// <c>node->vt+0x90</c> (B7), <c>0xA00618</c> (B7; it writes <c>pbi+0x1b8</c>/<c>+0x1bd</c> and calls
/// <c>0x9FB994</c>/<c>0x9FF0D8</c>), the fade-in setup <c>0xA366F4</c>/<c>0xA36268</c> plus
/// <c>vt+0x50</c> (B16), <c>0xA42DEC</c>/<c>0x9D40C4</c> (B10) and the node-chain
/// <see cref="WwiseGainNode"/> graph (B9, M6-010's builder) are required seams: the bridge throws when one
/// is needed and not supplied. <c>0xA548B8</c> (<c>voice+0xEC = engine</c>) is built as a required seam and
/// sets <see cref="WwiseLiveVoice.EngineEC"/>. The <c>pbi+0x1F8</c> value is settled at <c>0xFFFFFFFF</c>;
/// the <c>+0x15C</c>/<c>+0x15D</c> placeholder bytes and the <c>+0x14</c> RTPC key remain caller inputs
/// (UNKNOWN/RECOVERABLE_GAP).</para>
/// </summary>
public sealed class WwisePlaybackBridge : IWwisePlaybackBridge
{
    private long _nextVoiceId;

    /// <summary>The PBI start list (<c>0x108DA10</c>).</summary>
    public WwiseStartList StartList { get; } = new();

    /// <summary>The PBIs this bridge created, in creation order.</summary>
    public List<WwisePlayingInstance> Instances { get; } = new();

    /// <summary>The live voices (the native list head <c>0x108DF68</c>; also the voice pass's list).</summary>
    public List<WwiseLiveVoice> Voices { get; } = new();

    /// <summary>The frame's manager tick, stamped into the start-list node (<c>0x9D3558</c>).</summary>
    public long Tick { get; set; }

    /// <summary>The voice buffer's channel count for a newly created voice.</summary>
    public int VoiceChannels { get; set; } = 1;

    /// <summary>The voice buffer's frame count for a newly created voice.</summary>
    public int VoiceMaxFrames { get; set; } = WwiseRuntimeSettings.SamplesPerFrame;

    // ---------------------------------------------------------------- caller seams (unread bodies)

    /// <summary>
    /// B7 <c>0x9BEB30(pbi+0xC, gain, ...)</c>: the source parameter/init; its result must be 1 or the play
    /// aborts. The body is unread, so it is required; the bridge throws when it is not supplied.
    /// </summary>
    public Func<WwisePlayingInstance, bool>? InitSource9BEB30 { get; set; }

    /// <summary>B7 <c>node->vt+0x90(node, &amp;block, 1)</c>: the node's own slot; body not named. Required
    /// on the Sound path (the bridge throws when it is not supplied rather than silently skipping it).</summary>
    public Action<WwisePlayingInstance>? NodeVt90 { get; set; }

    /// <summary>
    /// B7 <c>0xA00618(pbi)</c>: called before PBI Play. It is not a no-op — <c>0xA00628..0xA0064C</c> writes
    /// <c>pbi+0x1b8</c>, <c>pbi+0x1bd</c> and calls <c>0x9FB994</c>/<c>0x9FF0D8</c> (unread). Required; the
    /// bridge throws when it is not supplied.
    /// </summary>
    public Action<WwisePlayingInstance>? BeforePlayA00618 { get; set; }

    /// <summary>
    /// B16 F1: the PBI Play fade-in branch's unread bodies <c>0xA366F4</c> (update) and <c>0xA36268</c>
    /// (create), plus <c>pbi->vt+0x50(pbi, 0xe, arg2[0])</c> (<c>0xA0081C</c>). Required whenever the Play
    /// carries a set transition; the bridge throws when it is not supplied. Returns the value stored at
    /// <c>pbi+0x144</c> (its source instruction was not provided).
    /// </summary>
    public Func<WwisePlayingInstance, WwiseFadeInTransition, uint>? SetupFadeInTransition { get; set; }

    /// <summary>
    /// B10 <c>0xA430A8 bl 0xA548B8(voice, engine)</c>: sets <c>voice+0xEC = engine</c>
    /// (<c>0xA548B8 str r1,[r0,#0xec]</c>). Required on the new-voice path; the engine pointer is a caller
    /// input.
    /// </summary>
    public Action<WwiseLiveVoice>? LinkEngineA548B8 { get; set; }

    /// <summary>
    /// B10 <c>0xA42DEC(voice, pbi)</c>: the <c>AddSrc==1</c> path; body unread. Its return is folded into
    /// the start-list node's <c>+0xd</c> and returned (<c>0xA4317C..0xA43194</c>).
    /// </summary>
    public Func<WwiseLiveVoice, WwisePlayingInstance, int>? LinkVoiceA42DEC { get; set; }

    /// <summary>B10 <c>0x9D40C4(voice, ...)</c>: the failure teardown; body unread.</summary>
    public Action<WwiseLiveVoice, WwisePlayingInstance>? TeardownVoice9D40C4 { get; set; }

    /// <summary>B9: the node chain's M6-010 gain graph for a PBI; null selects the reset branch.</summary>
    public Func<WwisePlayingInstance, WwiseGainNode?>? GainNodeFor { get; set; }

    /// <summary>The randomizer ranges for CalcEffectiveParams (M6-010).</summary>
    public WwiseGainRanges? Ranges { get; set; }

    /// <summary>The RTPC store and key for CalcEffectiveParams (M6-009).</summary>
    public WwiseRtpcStore? RtpcStore { get; set; }
    public WwiseGainRtpcKey RtpcKey { get; set; }

    /// <summary>The LCG for CalcEffectiveParams' randomizer draw (M6-007).</summary>
    public WwiseRng? Rng { get; set; }

    /// <summary>B11: builds the source for a PBI; null falls back to the factory over <see cref="MediaFor"/>.</summary>
    public Func<WwisePlayingInstance, IWwiseVoiceSource?>? SourceFactory { get; set; }

    /// <summary>B11 <c>[source+0xC]+0xC</c>: the object <c>voice+8</c> points at; UNKNOWN, so a seam.</summary>
    public Func<IWwiseVoiceSource, object?>? BusOwnerFor { get; set; }

    /// <summary>B10 <c>[voice+8]+0x1BC</c>: the chain id the attach matches against <c>pbi+0x1C8</c>.</summary>
    public Func<object?, uint>? BusOwnerChainId { get; set; }

    /// <summary>The media resolver for the default factory (M6-001/M6-024).</summary>
    public Func<uint, WwiseMedia?>? MediaFor { get; set; }

    /// <summary>The packed codebook library for Vorbis (M6-002).</summary>
    public WwiseCodebookLibrary? Codebooks { get; set; }

    /// <summary>The <c>pbi+0x15C</c>/<c>+0x15D</c> placeholder bytes (UNKNOWN) the ctor stores.</summary>
    public byte Placeholder15C { get; set; }
    public byte Placeholder15D { get; set; }

    /// <summary>The <c>pbi+0x14</c> RTPC key (init <c>0x9BC90C</c>, RECOVERABLE_GAP).</summary>
    public object? RtpcKey14 { get; set; }

    // ---------------------------------------------------------------- B1: the node dispatch

    /// <summary>
    /// B1/B2: the Play helper's <c>node->vt+0x128(node, params)</c>. Only the Sound PlayInternal body is
    /// read; RanSeq's container path and every other class are UNKNOWN here.
    /// </summary>
    public void OnPlay(WwiseNode node, uint playingId, uint? gameObject, WwisePlayInitParams p)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(p);
        switch (node)
        {
            case WwiseSoundNode sound:
                PlaySound(sound, gameObject, p);
                break;
            case WwiseRandomSequenceNode ranSeq:
                throw new NotSupportedException(
                    "M6-025 B2: the RanSeq container path (PlayInternal 0xA0AFDC, creator 0xA0B22C) is not " +
                    "read in this pass; refusing rather than guessing");
            default:
                throw new NotSupportedException(
                    $"M6-025 B2: the {node.Type} node's +0x128 PlayInternal body is UNKNOWN; refusing");
        }
    }

    // ---------------------------------------------------------------- B3/B4/B5/B6: the Sound path

    /// <summary>
    /// B3: the Sound PlayInternal normal path (<c>0xA1D448</c> -> <c>0xA379D8(node, node+0x5c, params)</c>).
    /// The special <c>params+0x84 == 0x90</c> branch is UNKNOWN and throws.
    /// </summary>
    private void PlaySound(WwiseSoundNode sound, uint? gameObject, WwisePlayInitParams p)
    {
        if (p.SoundSpecial84 == 0x90)
            throw new NotSupportedException(
                "M6-025 B3/2a.3: the Sound PlayInternal special branch (params+0x84==0x90) semantics are " +
                "UNKNOWN; refusing rather than guessing");

        var descriptor = WwiseSourceDescriptor.FromSound(sound);               // node+0x5c
        var pbi = CreatePbi(p, sound.Id, descriptor, continuous: false);      // B4/B5/B6
        Instances.Add(pbi);

        if (InitSource9BEB30 is null)
            throw new NotSupportedException(
                "M6-025 B7: 0x9BEB30 (the PBI parameter/source init; its result must be 1) is unread; " +
                "supply the seam rather than skipping it");
        if (!InitSource9BEB30(pbi)) return;                                   // 0xA37D20 cmp r0,#1

        if (NodeVt90 is null)
            throw new NotSupportedException(
                "M6-025 B7: node->vt+0x90(node, &block, 1) (0xA37D88/0xA37D94) is unread; supply the seam " +
                "rather than skipping it");
        NodeVt90(pbi);

        if (BeforePlayA00618 is null)
            throw new NotSupportedException(
                "M6-025 B7/F10: 0xA00618(pbi) is unread (it writes pbi+0x1b8/+0x1bd and calls " +
                "0x9FB994/0x9FF0D8); supply the seam rather than skipping it");
        BeforePlayA00618(pbi);                                                // 0xA38078

        // B9 CalcEffectiveParams is PBI vt+0x44, but no cited row places it on the Play path; it may belong
        // inside the 0x9BEB30 seam. It is not called here (see the record's unresolved).

        // B16: PBI Play 0xA0067C(pbi, params+0xC, (params+0x70==1), 0).
        PbiPlay(pbi, p);
    }

    /// <summary>
    /// B4/B5/B6: the node's <c>vt+0x14</c> creator (<c>0xA02EC8</c> for every shipped class except RanSeq)
    /// and the base PBI ctor <c>0xA000E8</c>.
    /// </summary>
    public WwisePlayingInstance CreatePbi(
        WwisePlayInitParams p, uint targetNodeId, object sourceDescriptor, bool continuous)
        => new(p, targetNodeId, sourceDescriptor, p.Block28, RtpcKey14,
            Placeholder15C, Placeholder15D, continuous);

    // ---------------------------------------------------------------- B16: PBI Play

    /// <summary>
    /// B16 <c>0xA0067C</c>: when <c>arg2[0] != 0</c> the fade-in transition is set up (the
    /// <c>0xA36268</c>/<c>0xA366F4</c> bodies are unread) and <c>pbi-&gt;vt+0x50(pbi, 0xe, iVar1)</c> runs;
    /// then type 0 is enqueued when <c>flag == 0 &amp;&amp; pbi+0x1BA&amp;7 != 1</c>, otherwise
    /// <c>pbi+0x1BC |= 0x80</c> and type 1.
    /// </summary>
    public void PbiPlay(WwisePlayingInstance pbi, WwisePlayInitParams p)
    {
        // F1 / 0xA00694: `cmp r5,#0; bne 0xA00774`. arg2[0] is the transition's first word (the fade-in
        // time); a non-zero transition runs the fade-in branch before the start-list enqueue.
        if (p.Transition is { IsSet: true } transition)
        {
            if (SetupFadeInTransition is null)
                throw new NotSupportedException(
                    "M6-025 B16/F1: the fade-in transition bodies 0xA366F4 (update) and 0xA36268 (create) " +
                    "and pbi->vt+0x50(pbi,0xe,arg2[0]) (0xA0081C) are unread; supply the seam rather than " +
                    "silently skipping the branch");
            uint v144 = SetupFadeInTransition(pbi, transition);          // 0xA007B0/0xA00800/0xA0081C
            pbi.Fade168 = 0f;                                            // 0xA00788 str r3,[r4,#0x168] (r3=0)
            pbi.Flags1BE |= 0x40;                                        // 0xA00814 orr r3,r3,#0x40; strb [r4,#0x1be]
            pbi.Field144 = v144;                                         // pbi+0x144 (value source not cited)
        }

        bool flag = p.Flag70 == 1;
        if (!(flag == false && (pbi.Flags1BA & 7) != 1))
        {
            pbi.Flags1BC |= 0x80;                                             // 0xA006EC
            StartList.Enqueue(1, pbi, Tick);                                  // 0xA006F4
        }
        else
        {
            StartList.Enqueue(0, pbi, Tick);                                  // 0xA006C4
        }
    }

    // ---------------------------------------------------------------- B10/B11: attach and AddSrc

    /// <summary>
    /// B10 <c>0xA4304C</c>: if the PBI has a chain id, match a live voice by
    /// <c>[voice+8]+0x1BC == pbi+0x1C8</c> and reuse it; otherwise create a 0x540-byte voice, init it and
    /// AddSrc it. Returns the native code (5 match, 1 linked, 0 failed).
    /// </summary>
    public int AttachVoice(WwisePlayingInstance pbi) => AttachVoice(pbi, null);

    private int AttachVoice(WwisePlayingInstance pbi, WwiseStartListNode? node)
    {
        ArgumentNullException.ThrowIfNull(pbi);

        if (pbi.ChainId != 0 && BusOwnerChainId is not null)
        {
            foreach (var voice in Voices)
            {
                if (BusOwnerChainId(voice.BusOwner8) != pbi.ChainId) continue;   // 0xA430E8
                AddSrc(voice, pbi, bActive: false);                              // 0xA4311C
                pbi.MarkChainMatchedA01878();                                    // 0xA43120 bl 0xA01878
                return 5;                                                        // 0xA43128
            }
        }

        var created = new WwiseLiveVoice(VoiceChannels, VoiceMaxFrames)          // 0xA43088 (0x540)
        {
            Id = unchecked((uint)Interlocked.Increment(ref _nextVoiceId)),
        };
        Voices.Add(created);

        if (LinkEngineA548B8 is null)
            throw new NotSupportedException(
                "M6-025 B10: 0xA548B8(voice, engine) (voice+0xEC = engine) is a caller input; supply the " +
                "seam rather than skipping it");
        LinkEngineA548B8(created);                                               // 0xA430A8

        int r = AddSrc(created, pbi, bActive: true);                             // 0xA430B8
        if (r == 0x3F) return 1;                                                 // 0xA430BC -> link, return 1
        if (r == 1)
        {
            if (LinkVoiceA42DEC is null)
                throw new NotSupportedException(
                    "M6-025 B10/F6: 0xA42DEC(voice,pbi) (the AddSrc==1 path) is unread; supply the seam " +
                    "rather than skipping it");
            int folded = LinkVoiceA42DEC(created, pbi);                          // 0xA43174
            // 0xA43180..0xA4318C: bit0 of node+0xd = (folded == 1), not the raw return.
            if (node is not null) node.D = (byte)((node.D & ~1) | (folded == 1 ? 1 : 0));
            return folded;
        }
        if (TeardownVoice9D40C4 is null)
            throw new NotSupportedException(
                "M6-025 B10: 0x9D40C4(voice,...) (the attach failure path) is unread; supply the seam");
        TeardownVoice9D40C4(created, pbi);                                       // 0xA430DC
        return 0;
    }

    /// <summary>
    /// B11 <c>0xA558AC</c>: <c>0xA01E24</c> (plugin/mode), <c>0xA562B8</c> (factory),
    /// <c>0xA56650</c> (StartStream), then <c>voice+0xD4</c>/<c>voice+0xD8</c>, <c>voice+8</c> and the
    /// <c>pbi+0x1BE</c> bit3 clear. Returns the <c>0xA56650</c> code (1 or 0x3F), or 0 when no source was
    /// built.
    /// </summary>
    public int AddSrc(WwiseLiveVoice voice, WwisePlayingInstance pbi, bool bActive)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(pbi);

        var source = SourceFactory?.Invoke(pbi) ?? DefaultSource(pbi);           // 0xA01E24/0xA562B8
        if (source is null) return 0;

        // 0xA558F8: pbi+0x154 = voice, before the 0xA55914 StartStream call, so the failure path returns
        // with +0x154 set.
        pbi.Field154 = voice;

        // 0xA56650: return 1 when [source+0x10] bit0 (StartStreamSucceeded) is already set; otherwise call
        // vt+0x28 and set the bit only when it returns 1, returning that result. A 0x3F result leaves the
        // bit clear.
        int r;
        if (source.StartStreamSucceeded)
        {
            r = 1;                                                               // 0xA5665C
        }
        else
        {
            if (!source.StartStream()) return 0;                                 // 0xA56664 -> vt+0x28
            r = source.StartStreamCode;                                          // 1 (bit set) or 0x3F (bit clear)
        }

        if (source is IWwiseVoiceSourceFormat format)
            pbi.SourceFormat158 = format.SourceFormatWord;                       // B15

        if (bActive) voice.Source = source;                                      // voice+0xD4 (0xA55934)
        else voice.Pending = source;                                             // voice+0xD8

        voice.BusOwner8 = BusOwnerFor?.Invoke(source);                           // voice+8 (0xA55948)
        pbi.Flags1BE = (byte)(pbi.Flags1BE & ~8);                                // clear bit3 (0xA5594C)
        return r;
    }

    /// <summary>B12/B13: the default factory over the PBI's descriptor.</summary>
    private IWwiseVoiceSource? DefaultSource(WwisePlayingInstance pbi)
    {
        if (pbi.SourceDescriptor is not WwiseSourceDescriptor descriptor) return null;
        if (MediaFor is null) return null;
        int mode = WwiseSourceFactory.ModeForStream(descriptor.PluginId, descriptor.StreamType);
        var kind = WwiseSourceFactory.Select(mode, descriptor.PluginId);
        if (kind is null) return null;
        return WwiseSourceFactory.Create(kind.Value, descriptor, MediaFor, Codebooks);
    }

    /// <summary>B17: drains the start list when the gate is set; call this from the V27 group member's
    /// FirstWalk. <c>0x9D3C98</c> gates on <c>byte[0x108DA34]</c> (<c>0x9D3CA4 ldrb r3,[r3,#0x28];
    /// cmp r3,#0; beq</c>), then calls <c>0x9D3644</c>.</summary>
    public int DrainStartList()
    {
        if (!StartList.Gate) return 0;                                           // 0x9D3CA4..0x9D3CAC
        var kept = StartList.Drain(n => AttachVoice(n.Pbi, n));                  // 0x9D3644
        return kept.Count;
    }
}