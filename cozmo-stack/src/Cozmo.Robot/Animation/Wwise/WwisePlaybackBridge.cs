// fidelity: M6-025, M6-026
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
    public int Enqueue(int type, WwisePlayingInstance pbi, long tick)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        // fidelity: M6-026 (6.6, F6): returns 2 only when the free list is empty and the allocation fails (the cap is 0xFFFFFFFF after init); the node
        // is {next, pbi, tick} with the flag at +0xC and bit 0 of +0xD cleared (0x9D35C8).
        if (AllocationFails?.Invoke() == true) return 2;
        Nodes.Add(new WwiseStartListNode { Pbi = pbi, Type = type, Tick = tick });
        if (type < 2) Gate = true;                                        // 0x9D3598
        return 1;
    }

    /// <summary>The allocation-failure branch of <c>0x9D3558</c> (6.6): true fails the enqueue with 2. Null means it never fails.</summary>
    public Func<bool>? AllocationFails { get; set; }

    /// <summary>
    /// <c>0x9D3644</c> (pass 1): for each ready node, call <paramref name="attach"/> and keep the node only when
    /// it returns 1 (<c>0x9D36EC</c>); otherwise unlink/free it. A node whose PBI already has a voice
    /// (<c>pbi+0x154 != 0</c>, <c>0x9D36B4/0x9D36BC</c>) is skipped and stays in the list. A node with
    /// <c>(pbi+0x1BC&amp;0x20) &amp;&amp; pbi+0x1F8==-1</c> (<c>0x9D36D4..0x9D36E0</c>) is not skipped: it goes to
    /// <c>0x9D379C</c>, is unlinked and freed, and only then <paramref name="destroyed"/> (<c>bl 0xA01800(pbi, 1)</c> at
    /// <c>0x9D3814</c>: <c>pbi+0x154 = 0</c>, then the <c>0xA38600(pbi, 4, 1, 0)</c> notification) runs (C25.3, C26.6).
    /// A node of type above 1 is kept unattached and unfreed (<c>0x9D36C0..0x9D36C8</c>, C26.3).
    /// Returns the kept nodes. The dirty byte (<see cref="Gate"/>) is cleared at its end (<c>0x9D3764..0x9D3770</c>;
    /// C24.7 lists that range, the clear itself is stated by research row 6.2).
    /// </summary>
    public List<WwiseStartListNode> Drain(Func<WwiseStartListNode, int> attach, Action<WwiseStartListNode> destroyed)
    {
        ArgumentNullException.ThrowIfNull(attach);
        ArgumentNullException.ThrowIfNull(destroyed);
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
            // C26.3, 0x9D36C0 ldrb r2,[r4,#0xc]; cmp r2,#1; bhi 0x9D36A0: a node of type above 1 is kept, not
            // attached and not freed; the test sits after the pbi+0x154 test and before the bit5 test.
            if (node.Type > 1)
            {
                i++;
                continue;
            }
            // 0x9D36D4..0x9D36E0 -> 0x9D379C..0x9D3814 (C25.3, C26.6): unlink and free the node, then
            // 0xA01800(pbi, 1) runs after the node is gone (0x9D37A0..0x9D3814).
            if ((node.Pbi.Flags1BC & 0x20) != 0 && node.Pbi.Field1F8 == 0xFFFFFFFF)
            {
                Nodes.RemoveAt(i);
                destroyed(node);                                          // 0x9D3814 bl 0xA01800(pbi, 1)
                continue;
            }
            int r = attach(node);                                         // 0x9D36E8 -> 0xA4304C
            if (r == 1) { kept.Add(node); i++; }                          // 0x9D36EC
            else Nodes.RemoveAt(i);
        }
        Gate = false;                                                     // 0x9D3764..0x9D3770
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
/// <c>0xA00618</c> (B7; it writes <c>pbi+0x1b8</c>/<c>+0x1bd</c> and calls
/// <c>0x9FB994</c>/<c>0x9FF0D8</c>), the fade-in setup <c>0xA366F4</c>/<c>0xA36268</c> plus
/// <c>vt+0x50</c> (B16), <c>0xA42DEC</c>/<c>0x9D40C4</c> (B10) and the node-chain
/// <see cref="WwiseGainNode"/> graph (B9, M6-010's builder) are required seams: the bridge throws when one
/// is needed and not supplied. <c>0xA548B8</c> (<c>voice+0xEC = engine</c>) is built as a required seam and
/// sets <see cref="WwiseLiveVoice.EngineEC"/>. The <c>pbi+0x1F8</c> value is settled at <c>0xFFFFFFFF</c>;
/// the <c>+0x15C</c> word is the settled <c>0x4101</c> ctor default (C23.8) until the required <see cref="SourceFormatWriter15C"/> seam writes it (C26.5) and the <c>+0x14</c> RTPC key remains a
/// caller input (UNKNOWN/RECOVERABLE_GAP).</para>
/// </summary>
public sealed class WwisePlaybackBridge : IWwisePlaybackBridge
{
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

    /// <summary>
    /// M6-026: the playback-limit walker. <c>0xA379D8</c> calls <c>node-&gt;vt+0x90</c> = <c>0x9ED2CC</c> at <c>0xA37D94</c> (P4); <see cref="PlaySound"/> is
    /// that call site on the live path and calls <see cref="WwisePlaybackLimiter.Walk"/>. It replaces the earlier <c>NodeVt90</c> seam. Required: the bridge
    /// throws when it is not supplied.
    /// </summary>
    // fidelity: M6-026
    public WwisePlaybackLimiter? Limiter
    {
        get => _limiter;
        set
        {
            _limiter = value;
            // 0xA01768 is the bridge's own (NextSourceA01768), so the limiter's virtual-path callers (V2, C5, E3) reach the same cache and the same 0x9EEDA4 body.
            if (value is not null) value.NextSourceCodeA01768 ??= pbi => NextSourceA01768(pbi, out _);
        }
    }

    private WwisePlaybackLimiter? _limiter;

    /// <summary>
    /// B7 <c>0xA00618(pbi)</c>: called before PBI Play. It is not a no-op — <c>0xA00628..0xA0064C</c> writes
    /// <c>pbi+0x1b8</c>, <c>pbi+0x1bd</c> and calls <c>0x9FB994</c>/<c>0x9FF0D8</c> (unread). Required; the
    /// bridge throws when it is not supplied.
    /// </summary>
    public Action<WwisePlayingInstance>? BeforePlayA00618 { get; set; }

    /// <summary>
    /// B16 F1: the PBI Play fade-in branch's unread bodies <c>0xA366F4</c> (update) and <c>0xA36268</c>
    /// (create), plus <c>pbi->vt+0x50(pbi, 0xe, arg2[0])</c> (<c>0xA0082C</c>). Required whenever the Play
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
    /// B10 <c>0xA42DEC(voice, pbi)</c>, <c>0x9D40C4</c> and the pending-voice list (M6-025 C23 item 1 and 5):
    /// the voice-to-bus connection creation. Required on the new-voice path; the bridge throws when it is
    /// not supplied. The <c>0xA42DEC</c> return is folded into the start-list node's <c>+0xd</c> and
    /// returned (<c>0xA4317C..0xA43194</c>). Its live-voice list is <see cref="Voices"/>.
    /// </summary>
    // fidelity: M6-025
    public WwiseVoiceLinker? Linker
    {
        get => _linker;
        set
        {
            _linker = value;
            // [source+0xC] (the owner PBI) is the bridge's own registry, so the linker's 0xA544BC reaches the same owner AddSrc stored.
            if (value is not null) value.Seams.SourceOwner ??= TryOwnerOf;
        }
    }

    private WwiseVoiceLinker? _linker;

    /// <summary>
    /// <c>0xA38600(pbi, 4, 1, 0)</c>, the notification (code 4) <c>0xA01800(pbi, 1)</c> tail-calls after it sets
    /// <c>pbi+0x154 = 0</c> (C25.3). Its body is not read (its consumer is the event notification path), so it is
    /// REQUIRED: pass 1 reaching a destroyed node without it throws.
    /// </summary>
    // fidelity: M6-025
    public Action<WwisePlayingInstance, int, int, int>? Notify38600 { get; set; }

    /// <summary>
    /// C26.5: the source <c>StartStream</c> classes overwrite <c>pbi+0x15C..0x15F</c> (the source format,
    /// <c>fmt+0x14</c>) before the voice init reads it (<c>0xA72760..0xA72778</c>, <c>0xA72E74</c>,
    /// <c>0xA73B88..0xA73BA0</c>, <c>0xA75C84</c>, <c>0xAB0C04..0xAB0C24</c>, <c>0xAB138C</c>). Those writers are not
    /// built, so this seam is REQUIRED: it is called right after <c>vt+0x28</c> StartStream runs and must write
    /// <see cref="WwisePlayingInstance.Word15C"/>; unset, <see cref="AddSrc"/> throws
    /// <see cref="WwiseMissingBehaviourException"/>. The ctor default 0x4101 (<c>0xA00338..0xA00374</c>) is only what the
    /// PBI holds until then.
    /// </summary>
    // fidelity: M6-025
    public Action<WwisePlayingInstance, IWwiseVoiceSource>? SourceFormatWriter15C { get; set; }

    /// <summary>
    /// C27 step 8 (<c>0xA55964..0xA55998</c>): the source destructor (<c>vtable[0]</c>) and the pool free
    /// <c>0xA7A988</c> that follow <c>0xA56414(source, 1)</c>. The bodies are not read, so the seam is REQUIRED.
    /// </summary>
    // fidelity: M6-025
    public Action<IWwiseVoiceSource>? SourceDestructAndPoolFree { get; set; }

    /// <summary>
    /// C27 step 8 (<c>0xA56454..0xA56468</c>): whether <c>[pbi+4] &amp; 0x100000</c> is set, the test
    /// <c>0xA56414</c> makes before <c>0xA054D8</c>. Nothing in the inventory says who writes <c>pbi+4</c>, so the seam is
    /// REQUIRED.
    /// </summary>
    // fidelity: M6-025
    public Func<WwisePlayingInstance, bool>? PbiFlag4Bit100000 { get; set; }

    /// <summary>
    /// C27 step 8 (<c>0xA56454..0xA56468</c>): the native call is <c>0xA054D8(*global, [pbi+0x140], source)</c>
    /// (<c>0xA56454..0xA56464</c>), run by <c>0xA56414</c> when <c>[pbi+4] &amp; 0x100000</c>. The arguments are the
    /// global context (<see cref="A054D8Context"/>), the playing id and the source. The body is not read, so the seam is
    /// REQUIRED.
    /// </summary>
    // fidelity: M6-025
    public Action<object, uint, IWwiseVoiceSource>? CallA054D8 { get; set; }

    /// <summary>
    /// The <c>*global</c> first argument of <c>0xA054D8</c> (<c>0xA56454..0xA56464</c>). Which global it is, is not
    /// settled by the inventory (reported MISSING), so it is REQUIRED: reaching <c>0xA054D8</c> with it unset throws.
    /// </summary>
    // fidelity: M6-025
    public object? A054D8Context { get; set; }

    /// <summary>
    /// C27 step 4: <c>0x9EEDA4([pbi+0xE0], out)</c> (<c>0xA01794/0xA017A0</c>), the first-call computation inside <c>0xA01768</c>. The body is read (M6-026 P1a) and lives once, in
    /// <see cref="WwisePlaybackLimiter.BehaviourCode9EEDA4"/>; this property is only an optional override for a caller that supplies its own node model. Unset, the limiter's body is used.
    /// </summary>
    // fidelity: M6-025
    public WwiseNextSource9EEDA4? NextSource9EEDA4 { get; set; }

    /// <summary>
    /// The node's <c>vt+0x120(node, target)</c> (M6-026 3.5, C27 step 4 <c>0xA017C8..0xA017E4</c>): called by <c>0xA379D8</c> for behaviour code 3 and by <c>0xA01768</c> for code 3 (a result of 0
    /// maps to code 1, otherwise 2). One seam serves both callers; its body is unread, so it is REQUIRED when reached.
    /// </summary>
    // fidelity: M6-025, M6-026
    public Func<WwiseNode?, uint, int>? NodeVt120A379D8 { get; set; }

    /// <summary>
    /// C27 step 4 (<c>0xA559E8</c>): the 0x4C allocation through <c>0xA7A7F4</c> that is stored at <c>[voice+0x10]</c>;
    /// null is the allocation failure. The body is not read, so the seam is REQUIRED.
    /// </summary>
    // fidelity: M6-025
    public Func<WwiseLiveVoice, WwiseVoiceSendTable?>? NewVoiceAllocSendTable4C { get; set; }

    /// <summary>
    /// C27 step 5 (<c>0xA55900</c>): <c>0x9BCA68(pbi+0xC, 0)</c>, returning an int. The body is not read (C27 residual),
    /// so the seam is REQUIRED.
    /// </summary>
    // fidelity: M6-025
    public Func<WwisePlayingInstance, int>? Call9BCA68 { get; set; }

    /// <summary>
    /// C27 step 5 (<c>0xA55A08..0xA55A3C</c>): <c>0xA0228C(pbi)</c>, run only when <c>[voice+0xCD]</c> bit0 is set. The body
    /// is not read (C27 residual), so the seam is REQUIRED.
    /// </summary>
    // fidelity: M6-025
    public Action<WwisePlayingInstance>? CallA0228C { get; set; }

    // ---- C24.7: the state-dispatch bodies of the start-list pass 2 (0x9D3864). They are unread
    // (RECOVERABLE_GAP), so each is REQUIRED: reaching one without its seam throws
    // WwiseMissingBehaviourException, so a voice never silently goes dead.

    /// <summary><c>0xA56478([voice+0xD4])</c>, the source start <c>0xA54480</c> runs on a voice whose state is 0 (C24.7).</summary>
    // fidelity: M6-025
    public Action<WwiseLiveVoice>? StartSourceA56478 { get; set; }

    /// <summary><c>voice-&gt;vt+0x4C</c> = <c>0xA53558</c> (dispatch states 1 and 2, C24.7).</summary>
    // fidelity: M6-025
    public Action<WwiseLiveVoice>? VoiceVt4CA53558 { get; set; }

    /// <summary><c>voice-&gt;vt+0x50</c> = <c>0xA5358C</c> (dispatch state 3, C24.7).</summary>
    // fidelity: M6-025
    public Action<WwiseLiveVoice>? VoiceVt50A5358C { get; set; }

    /// <summary><c>voice-&gt;vt+0x54(voice, pbi)</c> = <c>0xA535D8</c> (dispatch state 4, C24.7).</summary>
    // fidelity: M6-025
    public Action<WwiseLiveVoice, WwisePlayingInstance>? VoiceVt54A535D8 { get; set; }

    /// <summary><c>voice-&gt;vt+0x58</c> = <c>0xA53698</c> (dispatch state 5, C24.7).</summary>
    // fidelity: M6-025
    public Action<WwiseLiveVoice>? VoiceVt58A53698 { get; set; }

    /// <summary>B9: the node chain's M6-010 gain graph for a PBI; null selects the reset branch.</summary>
    public Func<WwisePlayingInstance, WwiseGainNode?>? GainNodeFor { get; set; }

    /// <summary>The randomizer ranges for CalcEffectiveParams (M6-010).</summary>
    public WwiseGainRanges? Ranges { get; set; }

    /// <summary>The RTPC store and key for CalcEffectiveParams (M6-009).</summary>
    public WwiseRtpcStore? RtpcStore { get; set; }
    public WwiseGainRtpcKey RtpcKey { get; set; }

    /// <summary>The LCG for CalcEffectiveParams' randomizer draw (M6-007).</summary>
    public WwiseRng? Rng { get; set; }

    /// <summary>B11: builds the source for a PBI; when unset the default factory over <see cref="MediaFor"/> is used. A null it returns is the native null-source path.</summary>
    public Func<WwisePlayingInstance, IWwiseVoiceSource?>? SourceFactory { get; set; }

    /// <summary>The media resolver for the default factory (M6-001/M6-024).</summary>
    public Func<uint, WwiseMedia?>? MediaFor { get; set; }

    /// <summary>The packed codebook library for Vorbis (M6-002).</summary>
    public WwiseCodebookLibrary? Codebooks { get; set; }

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

        var limiter = Limiter ?? throw new NotSupportedException(
            "M6-026 P4: node->vt+0x90 (0x9ED2CC, the playback-limit walker) is not optional; supply a WwisePlaybackLimiter rather than skipping it");

        // fidelity: M6-026 - 0xA379D8 P1..P3, before the PBI exists (P1..P3, 3.5).
        int behaviour = NextSource9EEDA4 is { } over1 ? over1(sound, out _) : limiter.BehaviourCode9EEDA4(sound, out _);   // P1: 0xA37A08 (the same hook as 0xA01768)
        int r6;                                                                // 0xA37A10 mov r6,r0 and the code table (3.5)
        bool flag1C;                                                           // [sp+0x1C]
        switch (behaviour)
        {
            case 1:
                r6 = 1; flag1C = false;                                        // 0xA37A18..0xA37A28
                break;
            case 3:                                                            // 0xA37A0C beq 0xA37B5C
                if (p.TargetNodeId == 0
                    || (NodeVt10A379D8 ?? throw MissingCode3("node vt+0x10"))(sound) == 9
                    || (NodeVt120A379D8 ?? throw MissingCode3("node vt+0x120"))(sound, p.TargetNodeId) != 0)
                { r6 = 2; flag1C = true; }
                else { r6 = 1; flag1C = false; }
                break;
            default:
                r6 = behaviour; flag1C = true;                                 // 0xA37B9C: codes 0, 2, 4..15
                break;
        }
        limiter.Priority9F6B94(sound, out float priority, out float distanceOffset);   // P2: 0xA37A44
        // 0xA37A3C stores 0 to [sp+0x2C]; 0xA37A54..0xA37A6C calls vt+0x84(node, &[sp+0x2C]) (r0 is only a gate); 0xA37A70..0xA37A8C: r0 != 0 -> [sp+0x2C] = [sb+0x64] * [sp+0x2C]
        // (float multiply, sb = [params+8], the game object); 0xA37CEC loads [sp+0x2C] as the r1 of 0x9BEB30.
        var (gate84, out84) = (NodeVt84A9F1F80 ?? throw new WwiseMissingBehaviourException(
            "M6-026 P2: node vt+0x84 (0x9F1F80), called at 0xA37A6C on every Play, is unread; supply NodeVt84A9F1F80"))(sound);
        if (gate84)
        {
            uint objectForField64 = p.GameObjectId ?? throw new WwiseMissingBehaviourException(
                "M6-026 P2: 0xA37A80 reads [game object + 0x64] and the Play has no game object (a null dereference in the engine)");
            float field64 = (GameObjectField64A37A80 ?? throw new WwiseMissingBehaviourException(
                "M6-026 P2: [game object + 0x64] (0xA37A80) has no writer in the inventory; supply GameObjectField64A37A80"))(objectForField64);
            out84 = field64 * out84;
        }
        if (gate84 && distanceOffset != 0f)                                    // 0xA37A70..0xA37A94
            throw new WwiseMissingBehaviourException(
                "M6-026 P2: the distance path 0xA37BA8..0xA37C5C is unread (RECOVERABLE_GAP); no shipped node has a nonzero offset (P2b)");
        if (limiter.CheckMemoryA376C0(priority) == 0) return;                  // P3: 0xA37A9C, no PBI is created
        int voiceCheck = limiter.CheckVoiceCountA37880(priority);              // P3: 0xA37C74
        if (voiceCheck == 2) return;                                           // 0xA37C84 (the function never returns 2, C5)
        if ((SourceStructField16A37C90 ?? throw new WwiseMissingBehaviourException(
                "M6-026 0xA37C90: ldrh [node+0x5C+0x16] (the source struct halfword) is not modelled; supply SourceStructField16A37C90"))(sound) == 8)
            throw new WwiseMissingBehaviourException(
                "M6-026 0xA37C90: [node+0x72] == 8 takes the external-source branch 0xA37E64 (0xA1ED48..0xA38148), which is unread");

        var descriptor = WwiseSourceDescriptor.FromSound(sound);               // node+0x5c
        var pbi = CreatePbi(p, sound.Id, descriptor, continuous: false);      // B4/B5/B6
        pbi.Priority1C0 = priority;                                            // 0xA002D4..0xA002E4: the ctor argument block [sp+0x38]
        pbi.NodeE0 = sound;                                                    // [pbi+0xE0]
        pbi.PriorityBlock = limiter.PriorityRefresh9FFE1C;                     // CalcEffectiveParams priority block (8.4)
        pbi.Field1CC = priority;                                               // 0xA002D4..0xA002E4: the ctor copies the {priority, offset} block
        pbi.Field1D0 = distanceOffset;
        pbi.FieldE4 = BitConverter.SingleToUInt32Bits(out84);                  // 0x9BEB30 r1 = [sp+0x2C] -> this+0xD8 = pbi+0xE4 (A1, 0xA37CEC)
        Instances.Add(pbi);

        if (InitSource9BEB30 is null)
            throw new NotSupportedException(
                "M6-025 B7: 0x9BEB30 (the PBI parameter/source init; its result must be 1) is unread; " +
                "supply the seam rather than skipping it");
        if (!InitSource9BEB30(pbi))                                            // 0xA37D20 cmp r0,#1; bne 0xA37E78: exit (d)
        {
            FailPlayA37E7C(pbi, 0);                                            // 0xA37E78 mov r6,#0 unconditionally: bit 5 is always preset, Term does no undo
            return;
        }
        bool below = WwisePlaybackLimiter.Below9BEB30(pbi);                    // [sp+0x27], written by 0x9BEB30 at 0x9BED60 (3.1, 3.2)
        if (!flag1C && below)
            throw new WwiseMissingBehaviourException(
                "M6-026 3.3: with [sp+0x1C] == 0 and below set, 0x9BEB30 returns 3 (code 0x29) when r7 = (flag < r6') && !(ctx+0xDD & 4) (0x9BED50..0x9BED7C); r6' comes from the unread " +
                "0x9BEB74 / 0x9BEC30 / 0x9BEED8, so the outcome is not decidable. (With below clear r7 is 0 and the Play cannot fail here.)");

        // fidelity: M6-026 - P4: node->vt+0x90(node, &block, r2 = 1, r3 = 0) at 0xA37D94, the block at sp+0x4C.
        var block = new WwiseLimitBlock
        {
            Priority = priority,                                               // +0 [sp+0x38]
            GameObject = p.GameObjectId,                                       // +4 [params+8]
            Array = pbi.LimiterArray1EC,                                       // +8 pbi+0x1EC
            Word0C = 3,                                                        // +0xC
            Count0E = 0,                                                       // +0xE
            B10 = 0,                                                           // +0x10
            // +0x11 = ((below ^ 1) | (r6 == 0)) & (((pbi.1BE ^ 8) >> 3) & 1)  (3.4, 0xA37D5C..0xA37D8C)
            B11 = (byte)(((below ? 0 : 1) | (r6 == 0 ? 1 : 0)) & (((pbi.Flags1BE ^ 8) >> 3) & 1)),
        };
        int walk = limiter.Walk(sound, block, count: true, skipGlobal: false);
        if (walk == 2) { FailPlayA37E7C(pbi, 1); return; }                     // exit (a): 0xA37D98, r6 = 1 (0xA37DC4..0xA37DD0)
        if (walk == 0x50 || voiceCheck == 0x50)                                // O1: 0xA37DAC..0xA37DBC sets 1BE |= 4 before the [sp+0x1C] branch
        {
            pbi.Flags1BE |= 4;
            if (!flag1C) { FailPlayA37E7C(pbi, 1); return; }                   // exit (a): [sp+0x1C] == 0
        }

        // 6.1: pbi vt+0xC = 0xA0285C (the limiter lists), result 1 gates the Play (0xA37FF8 bne 0xA37DD0): exit (b), r6 = 1.
        if (limiter.InsertPbiA0285C(sound, pbi) != 1) { FailPlayA37E7C(pbi, 1); return; }

        // 0xA37FE8..0xA38070, in order (C23.12): 0xA023D4, the E8-bit-5 context call, 0xA01918, 0x9E85C8; then 0xA00618.
        (TailA023D4 ?? throw MissingSeam("0xA023D4 (0xA3800C)"))(pbi, p.Word88);                               // 0xA3800C: r1 = [params+0x88]
        if ((pbi.Flags0E8 & 0x20) == 0)                                                                        // 0xA38010..0xA38018: bit 5 clear -> 0xA38130
            (TailCtxVt24 ?? throw MissingSeam("[[pbi+0xC]+0x24] (0xA38130)"))(pbi, p.Block8C);                  //   vt+0x24(pbi+0xC, params+0x8C), then 0xA38038
        else if ((pbi.Flags0E9 & 1) != 0)                                                                      // bit 5 set: 0xA3801C..0xA38024 tests [pbi+0xE9] bit 0
            (TailCtxVt28 ?? throw MissingSeam("[[pbi+0xC]+0x28] (0xA38024)"))(pbi);                             //   vt+0x28(pbi+0xC)
        (TailA01918 ?? throw MissingSeam("0xA01918 (0xA38038)"))(pbi, p.Block108, 1);                          // 0xA38038: (pbi, params+0x108, 1)
        if (p.Ptr78 is not null && pbi.Field34 != 0)                                                           // 0xA38048..0xA38064
            (TailA9E85C8 ?? throw MissingSeam("0x9E85C8 (0xA38064)"))(pbi, 1, p.Ptr78);                        //   ([pbi+0x34], 1, [params+0x78]+0x14)
        if (BeforePlayA00618 is null)
            throw new NotSupportedException(
                "M6-025 B7/F10: 0xA00618(pbi) is unread (it writes pbi+0x1b8/+0x1bd and calls " +
                "0x9FB994/0x9FF0D8); supply the seam rather than skipping it");
        BeforePlayA00618(pbi);                                                // 0xA38078

        // B9 CalcEffectiveParams is PBI vt+0x44, but no cited row places it on the Play path; it may belong
        // inside the 0x9BEB30 seam. It is not called here (see the record's unresolved).

        // B16: PBI Play 0xA0067C(pbi, params+0xC, (params+0x70==1), 0); a result other than 1 (0xA38098 bne 0xA37AD8) is exit (c).
        if (PbiPlay(pbi, p) != 1) { FailPlayA37E7C(pbi, 1, viaA37E7C: false); return; }

        limiter.AppendToGlobalPbiList(pbi);                                   // 6.7: 0xA380A4..0xA380E0
    }

    private static WwiseMissingBehaviourException MissingCode3(string what)
        => new($"M6-026 3.5: {what} (the code-3 branch of 0xA379D8) is unread; supply the seam");

    /// <summary>Node <c>vt+0x10()</c> for the code-3 branch of <c>0xA379D8</c> (3.5: a result of 9 gives <c>r6 = 2</c>). Unread, required when the code is 3.</summary>
    // fidelity: M6-026
    public Func<WwiseNode, int>? NodeVt10A379D8 { get; set; }

    /// <summary>
    /// Node <c>vt+0x84</c> = <c>0x9F1F80(node, &amp;out)</c>, always called at <c>0xA37A6C</c> (P2): the return is only a gate and the out float (initially 0.0f, <c>0xA37A3C</c>) is
    /// <c>0x9BEB30</c>'s <c>r1</c> (<c>ctx+0xD8</c> = <c>pbi+0xE4</c>, C29 A1), scaled by <c>[game object+0x64]</c> when the gate is non-zero. A non-zero gate with a non-zero distance
    /// offset selects the distance path <c>0xA37BA8..0xA37C5C</c>. The body is unread (RECOVERABLE_GAP), so the seam is required.
    /// </summary>
    // fidelity: M6-026
    public Func<WwiseNode, (bool Gate, float Out)>? NodeVt84A9F1F80 { get; set; }

    /// <summary><c>[game object + 0x64]</c> (<c>0xA37A80</c>): the float the gate-set path multiplies <c>[sp+0x2C]</c> by. Nothing in the inventory writes it, so it is required when the gate is set.</summary>
    // fidelity: M6-026
    public Func<uint, float>? GameObjectField64A37A80 { get; set; }

    /// <summary>
    /// The tail of <c>0xA379D8</c> between the <c>0xA0285C</c> gate and <c>0xA00618</c> (<c>0xA37FE8..0xA38070</c>): <c>0xA023D4(pbi, [params+0x88])</c> at <c>0xA3800C</c>. Unread, required.
    /// </summary>
    // fidelity: M6-026
    public Action<WwisePlayingInstance, uint>? TailA023D4 { get; set; }

    /// <summary><c>[[pbi+0xC]+0x24](pbi+0xC, params+0x8C)</c> (<c>0xA38130</c>), called when <c>[pbi+0xE8]</c> bit 5 is clear (<c>0xA38010..0xA38018</c>). Unread, required when reached.</summary>
    // fidelity: M6-026
    public Action<WwisePlayingInstance, object?>? TailCtxVt24 { get; set; }

    /// <summary><c>[[pbi+0xC]+0x28](pbi+0xC)</c> (<c>0xA38024</c>), called when <c>[pbi+0xE8]</c> bit 5 is set and <c>[pbi+0xE9]</c> bit 0 is set (<c>0xA3801C..0xA38024</c>). Unread, required when reached.</summary>
    // fidelity: M6-026
    public Action<WwisePlayingInstance>? TailCtxVt28 { get; set; }

    /// <summary><c>0xA01918(pbi, params+0x108, 1)</c> (<c>0xA38038</c>). Unread, required.</summary>
    // fidelity: M6-026
    public Action<WwisePlayingInstance, object?, int>? TailA01918 { get; set; }

    /// <summary><c>0x9E85C8([pbi+0x34], 1, [params+0x78]+0x14)</c> (<c>0xA38048..0xA38064</c>), only when <c>[params+0x78] != 0</c> and <c>[pbi+0x34] != 0</c>. Unread, required when reached; the argument is <c>[params+0x78]</c>.</summary>
    // fidelity: M6-026
    public Action<WwisePlayingInstance, int, object>? TailA9E85C8 { get; set; }

    /// <summary>
    /// <c>ldrh [node+0x5C+0x16]</c> (= <c>node+0x72</c>) at <c>0xA37C90</c>: the source struct's halfword. <c>== 8</c> takes the external-source branch <c>0xA37E64</c> (<c>0xA1ED48..0xA38148</c>), which is unread.
    /// The field is not modelled on <see cref="WwiseSourceDescriptor"/>, so the read is a required seam.
    /// </summary>
    // fidelity: M6-026
    public Func<WwiseSoundNode, ushort>? SourceStructField16A37C90 { get; set; }

    /// <summary>
    /// The type-1 join of <c>0xA0067C</c> after <c>0x9D3558</c> (<c>0xA006F8..0xA00738</c>, O5): <c>0xA366AC(mgr, [pbi+0x144])</c> when <c>[pbi+0x144] != 0</c>. Unread, required when reached.
    /// </summary>
    // fidelity: M6-026
    public Action<WwisePlayingInstance>? TransitionA366AC { get; set; }

    /// <summary><c>0x9BDA28(pbi+0xC, 1)</c> in the type-1 join of <c>0xA0067C</c> (<c>0xA00728</c>). Unread, required.</summary>
    // fidelity: M6-026
    public Action<WwisePlayingInstance>? Call9BDA28 { get; set; }

    /// <summary><c>0x9E808C</c> in the type-1 join of <c>0xA0067C</c>, run when <c>[pbi+0x34] != 0</c> (<c>0xA00728</c>). Unread, required when reached.</summary>
    // fidelity: M6-026
    public Action<WwisePlayingInstance>? Call9E808C { get; set; }

    /// <summary><c>src vt+0x2C</c>, the tail of <c>0xA56414</c> (<c>0xA56440..0xA5644C</c>): the source's close. Unread, required.</summary>
    // fidelity: M6-026
    public Action<IWwiseVoiceSource>? SourceClose2C { get; set; }

    /// <summary>
    /// The failure block of <c>0xA379D8</c> after the PBI exists (C29.2). Through <c>0xA37E7C</c>: <c>0xA04D48</c> when <c>[params+0x24] != 0</c>, then <c>0xA37AC8</c> sets <c>pbi.1BD</c> bit 5 only when
    /// <c>r6 == 0</c> (so Term does no count undo, exit (d)); exit (c) enters at <c>0xA37AD8</c> directly. Then Term (<c>vt+0x10</c>), the destructor (<c>vt+4</c>, frees <c>pbi+0x1EC</c>) and the pool free.
    /// </summary>
    // fidelity: M6-026
    private void FailPlayA37E7C(WwisePlayingInstance pbi, int r6, bool viaA37E7C = true)
    {
        var limiter = Limiter!;
        if (viaA37E7C)
        {
            if (pbi.PlayingId != 0) limiter.RegisterPlayingId(pbi);           // 0xA37E7C..0xA37EA0
            if (r6 == 0) pbi.Flags1BD |= 0x20;                                // 0xA37AC8 ldrbeq ..; orreq #0x20
        }
        limiter.TermPbiA029DC(pbi);                                           // vt+0x10
        pbi.LimiterArray1EC.Items.Clear();                                    // vt+4 (0x9FF54C) frees pbi+0x1EC
        Instances.Remove(pbi);                                                // pool free
    }

    /// <summary>
    /// The code-4 tail of the notification flush <c>0xA38420</c> (K8, <c>0xA38480..0xA385D0</c>): unlink the PBI from the global PBI list (<c>[G+0x48]--</c>), <c>0x9D3470</c>
    /// (its start-list nodes), PBI Term (<c>vt+0x10</c>), the destructor (<c>vt+4</c>) and the pool free. The queue that reaches it (<c>0xA38600</c>) stays the
    /// <see cref="Notify38600"/> seam; a <see cref="WwisePbiNotification.Terminate"/> hook calls this.
    /// </summary>
    // fidelity: M6-026
    public void TerminatePbi(WwisePlayingInstance pbi)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        var limiter = Limiter ?? throw new NotSupportedException("M6-026 K8: TerminatePbi needs the WwisePlaybackLimiter");
        limiter.RemoveFromGlobalPbiList(pbi);                                 // 0xA38570..0xA38598
        StartList.Nodes.RemoveAll(n => ReferenceEquals(n.Pbi, pbi));          // 0x9D3470
        limiter.TermPbiA029DC(pbi);                                           // vt+0x10
        pbi.LimiterArray1EC.Items.Clear();                                    // vt+4 (0x9FF54C)
        Instances.Remove(pbi);                                                // pool free
    }

    /// <summary>
    /// B4/B5/B6: the node's <c>vt+0x14</c> creator (<c>0xA02EC8</c> for every shipped class except RanSeq)
    /// and the base PBI ctor <c>0xA000E8</c>.
    /// </summary>
    public WwisePlayingInstance CreatePbi(
        WwisePlayInitParams p, uint targetNodeId, object sourceDescriptor, bool continuous)
        => new(p, targetNodeId, sourceDescriptor, p.Block28, RtpcKey14, continuous);

    // ---------------------------------------------------------------- B16: PBI Play

    /// <summary>
    /// B16 <c>0xA0067C</c>: when <c>arg2[0] != 0</c> the fade-in transition is set up (the
    /// <c>0xA36268</c>/<c>0xA366F4</c> bodies are unread) and <c>pbi-&gt;vt+0x50(pbi, 0xe, iVar1)</c> runs;
    /// then type 0 is enqueued when <c>flag == 0 &amp;&amp; pbi+0x1BA&amp;7 != 1</c>, otherwise
    /// <c>pbi+0x1BC |= 0x80</c> and type 1.
    /// </summary>
    public int PbiPlay(WwisePlayingInstance pbi, WwisePlayInitParams p)
    {
        // F1 / 0xA00694: `cmp r5,#0; bne 0xA00774`. arg2[0] is the transition's first word (the fade-in
        // time); a non-zero transition runs the fade-in branch before the start-list enqueue.
        if (p.Transition is { IsSet: true } transition)
        {
            if (SetupFadeInTransition is null)
                throw new NotSupportedException(
                    "M6-025 B16/F1: the fade-in transition bodies 0xA366F4 (update) and 0xA36268 (create) " +
                    "and pbi->vt+0x50(pbi,0xe,arg2[0]) (0xA0082C) are unread; supply the seam rather than " +
                    "silently skipping the branch");
            uint v144 = SetupFadeInTransition(pbi, transition);          // 0xA007B0/0xA00800/0xA0081C
            pbi.Fade168 = 0f;                                            // 0xA00788 str r3,[r4,#0x168] (r3=0)
            pbi.Flags1BE |= 0x40;                                        // 0xA00814 orr r3,r3,#0x40; strb [r4,#0x1be]
            pbi.Field144 = v144;                                         // pbi+0x144 (value source not cited)
        }

        bool flag = p.Flag70 == 1;
        int r5;
        if (!(flag == false && (pbi.Flags1BA & 7) != 1))
        {
            pbi.Flags1BC |= 0x80;                                             // 0xA006EC
            r5 = StartList.Enqueue(1, pbi, Tick);                             // 0xA006F4
            // O5 (0xA006F8..0xA00738): the join runs before the r5 == 1 test.
            if (pbi.Field144 != 0)
                (TransitionA366AC ?? throw MissingSeam("0xA366AC (0xA00728)"))(pbi);
            (Call9BDA28 ?? throw MissingSeam("0x9BDA28(pbi+0xC, 1) (0xA00728)"))(pbi);
            if (pbi.Field34 != 0)
                (Call9E808C ?? throw MissingSeam("0x9E808C (0xA00728)"))(pbi);
        }
        else
        {
            r5 = StartList.Enqueue(0, pbi, Tick);                             // 0xA006C4
        }
        // fidelity: M6-026 (6.5, 6.6): r5 != 1 returns r5 (0x9D3558 fails only on an allocation failure with an empty free list); then
        // 1BA & 7 == 2 -> pbi vt+0(pbi,0,0); r3 == 0 at the call site, so r5 is returned without the global increment.
        if (r5 != 1) return r5;
        if ((pbi.Flags1BA & 7) == 2)
            (Limiter ?? throw new NotSupportedException("M6-026 6.5: 1BA & 7 == 2 runs pbi vt+0 (0x9FF7B8), the limiter's mark; supply a WwisePlaybackLimiter")).MarkStopped9FF7B8(pbi, 0, 0);
        return r5;
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

        if (pbi.ChainId != 0)
        {
            foreach (var voice in Voices)
            {
                // C25.4: [voice+8] = pbi+0xC of the voice's owner PBI, so [voice+8]+0x1BC is the owner's pbi+0x1C8
                // (0xA430E8). A live voice always has an owner: the native dereferences it.
                var owner = voice.BusOwner8 as WwisePlayingInstance ?? throw new InvalidOperationException(
                    "M6-025 C25.4: a live voice's [voice+8] is not its owner PBI; the native dereferences it (0xA430E8)");
                if (owner.ChainId != pbi.ChainId) continue;                      // 0xA430E8
                AddSrc(voice, pbi, bActive: false);                              // 0xA4311C
                pbi.MarkChainMatchedA01878();                                    // 0xA43120 bl 0xA01878
                return 5;                                                        // 0xA43128
            }
        }

        if (Linker is null)
            throw new NotSupportedException(
                "M6-025 B10/C23: the voice-to-bus connection creation (0xA42DEC, 0x9D40C4 and the pending list) " +
                "needs a WwiseVoiceLinker; supply it rather than skipping the link");

        var created = new WwiseLiveVoice(VoiceChannels, VoiceMaxFrames)          // 0xA43088 (0x540)
        {
            FlagsCD = 1,                                                         // 0xA54650: +0xCD bit0
            State = 0,                                                           // C25.1: [voice+0xDC] = 0 (0xA54798)
            OutputGain = 0f,                                                     // C25.1: [voice+0x1C] = 0 (0xA54678)
        };
        // [voice+0xF0] stays 0 (C25.5: the ctor zeroes it, 0xA5470C..0xA54764); the voice init sets it from pbi+0x15C.
        // 0xA54650 (0xA43094): the voice ctor sets voice+0xCD bit0 (0xA54674 mov r7,#1; 0xA54710 orr r2,r2,r7;
        // 0xA54758 strb r2,[r4,#0xcd]; C23 item 1 row 4). The voice joins the live list only in 0xA42DEC
        // (C23.4), so it is not added here.

        if (LinkEngineA548B8 is null)
            throw new NotSupportedException(
                "M6-025 B10: 0xA548B8(voice, engine) (voice+0xEC = engine) is a caller input; supply the " +
                "seam rather than skipping it");
        LinkEngineA548B8(created);                                               // 0xA430A8

        int r = AddSrc(created, pbi, bActive: true);                             // 0xA430B8
        if (r == 0x3F)
        {
            // 0xA430BC..0xA4315C: the voice joins the pending list 0x108DA2C/0x108DA30 (C23.4, row 5.12),
            // not the live list, and 0xA4304C returns 1 with no connection made.
            Linker.PendingVoices.Add(created);
            return 1;
        }
        if (r == 1)
        {
            int folded = Linker.Link(created, pbi);                              // 0xA43174 bl 0xA42DEC
            // 0xA43180..0xA4318C: bit0 of node+0xd = (folded == 1), not the raw return.
            if (node is not null) node.D = (byte)((node.D & ~1) | (folded == 1 ? 1 : 0));
            return folded;
        }
        Linker.TeardownVoice(created);                                           // 0xA430DC bl 0x9D40C4
        return r;                                                                // 0xA430E0 mov r0,r5 (row 5.12)
    }

    /// <summary>
    /// <c>0xA558AC..0xA55A70</c> AddSrc, in the C27 order (steps 1..10): the source factory (<c>0xA01E24</c>,
    /// <c>0xA562B8</c>); a null source runs <c>0xA01800(pbi, 1)</c> and returns 2; <c>pbi+0x154 = voice</c> on both paths;
    /// on a new voice <c>0xA01768</c> into <c>[voice+0xE4]</c>/<c>[voice+0xE0]</c> and the 0x4C send-table allocation;
    /// the <c>0x9BCA68</c> gate; StartStream <c>0xA56650</c>; the failure destroy <c>0xA56414(source, 1)</c>; then the reuse
    /// store <c>[voice+0xD8]</c> or the new-voice stores. The return value is the raw <c>r6</c> of the native.
    /// </summary>
    // fidelity: M6-025
    public int AddSrc(WwiseLiveVoice voice, WwisePlayingInstance pbi, bool bActive)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(pbi);

        var source = SourceFactory is not null ? SourceFactory(pbi) : DefaultSource(pbi);   // C27 step 1: 0xA01E24, 0xA562B8
        if (source is null)
        {
            DestroyPbiVoiceA01800(pbi);                                          // C27 step 2: 0xA559A4..0xA559B0
            return 2;
        }
        RegisterSourceOwner(source, pbi);                                        // [source+0xC] (C24 header)

        pbi.Field154 = voice;                                                    // C27 step 3: 0xA558EC..0xA558F8

        int r6;
        if (bActive)
        {
            // C27 step 4 (0xA559BC..0xA55A04): 0xA01768(pbi, voice+0xE0), result to [voice+0xE4].
            voice.E4 = NextSourceA01768(pbi, out int index);                     // 0xA559C4, 0xA559D0
            voice.E0 = index;
            if ((voice.SendTable?.Capacity ?? 0) == 0)                           // [voice+0x18] == 0
            {
                var table = (NewVoiceAllocSendTable4C ?? throw MissingSeam(
                    "0xA7A7F4 (the 0x4C send-table allocation, 0xA559E8)"))(voice);
                voice.SendTable = table;                                         // [voice+0x10] = r0, even when 0 (0xA559F0)
                if (table is null)
                    return DestroySourceA56414(source, pbi, 2);                  // 0xA55A70: r6 = 2, then step 8
                table.Capacity = 1;                                              // [voice+0x18] = 1 (0xA559F8)
            }
        }

        // C27 step 5 (0xA55900..0xA55A6C), both paths.
        bool shortcut = false;
        if (voice.E4 != 0)                                                       // 0xA55900
        {
            int gate = (Call9BCA68 ?? throw MissingSeam("0x9BCA68 (0xA55900)"))(pbi);
            if (gate != 0)
            {
                if (voice.E4 == 1)
                    return DestroySourceA56414(source, pbi, 3);                  // 0xA55960: r6 = 3, then step 8
                bool r3 = voice.E0 == 0 && bActive;                              // r3 = E0 != 0 ? 0 : (bActive & 1)
                if (r3)
                {
                    // Bit0 set means the Link init gate (0xA42DF4..0xA42E00) runs 0xA54A30, bit0 clear means it is
                    // skipped (C27 further facts); this shortcut is the only place that clears the bit.
                    if ((voice.FlagsCD & 1) != 0)
                        (CallA0228C ?? throw MissingSeam("0xA0228C (0xA55A08..0xA55A3C)"))(pbi);
                    voice.FlagsCD = (byte)(voice.FlagsCD & ~1);                  // 0xA55A40..0xA55A6C
                    shortcut = true;
                }
            }
        }

        if (shortcut)
        {
            r6 = 1;                                                              // C27 step 5: StartStream skipped
        }
        else
        {
            // C27 step 7 / C30 (0xA55908..0xA55924): 0xA56650(source, [owner+0x1DC], [owner+0x1E0]) with owner = [source+0xC]
            // (r7, 0xA558EC). It returns 1 when [source+0x10] bit0 is already set; otherwise it calls vt+0x28 with those two words and
            // sets the bit only on a raw result of exactly 1. r6 is the raw vt+0x28 result (mov r6,r0, 0xA55920); any value other than
            // 1 or 0x3F goes to step 8 unchanged.
            var owner = OwnerOf(source);                                         // [source+0xC]
            uint a1DC = owner.Read1DC();                                         // 0xA5590C ldr r1,[r7,#0x1dc]
            uint a1E0 = owner.Read1E0();                                         // 0xA55910 ldr r2,[r7,#0x1e0]
            r6 = WwiseVoiceSourceStart.StartA56650(source, a1DC, a1E0, out bool ran);   // 0xA55914 bl 0xA56650
            if (ran)
            {
                // UNRESOLVED (C26.5): the StartStream classes write pbi+0x15C..0x15F inside vt+0x28
                // (0xA72760..0xAB138C). The writer is a required seam called here; its position relative to the
                // rest of AddSrc is not claimed as native ordering. It runs whenever vt+0x28 ran.
                (SourceFormatWriter15C ?? throw new WwiseMissingBehaviourException(
                    "M6-025 C26.5: the source StartStream writers of pbi+0x15C..0x15F (0xA72760..0xAB138C) are not built; " +
                    "supply SourceFormatWriter15C")).Invoke(pbi, source);
            }

            if (r6 != 1 && r6 != 0x3F)
                return DestroySourceA56414(source, pbi, r6);                     // C27 step 7 -> step 8
        }

        if (!bActive)
        {
            voice.Pending = source;                                              // C27 step 9: [voice+0xD8] (0xA55928..0xA55930)
            return r6;
        }

        voice.Source = source;                                                   // C27 step 10: [voice+0xD4] (0xA55934)
        voice.BusOwner8 = OwnerOf(source);                                       // [voice+8] = [source+0xC]+0xC = the owner PBI
        pbi.Flags1BE = (byte)(pbi.Flags1BE & ~8);                                // pbi+0x1BE bit3 cleared
        return r6;
    }

    /// <summary>
    /// C27 step 8: <c>0xA56414(source, 1)</c> (<c>[source+0xC]</c> is the owner PBI; when <c>[pbi+4] &amp; 0x100000</c>,
    /// <c>0xA054D8([pbi+0x140])</c> first, <c>0xA56454..0xA56468</c>; then <c>0xA01800(pbi, 1)</c>, <c>0xA56438</c>, which
    /// clears <c>pbi+0x154</c> and sends the code-4 notification), then the source destructor and the pool free
    /// (<c>0xA55964..0xA55998</c>). Returns <paramref name="result"/> (r6).
    /// </summary>
    // fidelity: M6-025
    private int DestroySourceA56414(IWwiseVoiceSource source, WwisePlayingInstance pbi, int result)
    {
        CloseSourceA56414(source, 1);                                            // 0xA56414(source, 1): 0xA054D8, 0xA01800(pbi, 1), src vt+0x2C
        (SourceDestructAndPoolFree ?? throw MissingSeam(
            "the source destructor and the pool free 0xA7A988 (0xA55964..0xA55998)"))(source);
        return result;
    }

    /// <summary>
    /// C27 step 4 <c>0xA01768(pbi, &amp;out)</c>: a cache in <c>pbi+0x1BB</c>. The out value is <c>pbi+0x1BB &amp; 7</c>, the
    /// return value <c>(pbi+0x1BB &gt;&gt; 3) &amp; 0xF</c>; the first call computes both through <c>0x9EEDA4</c> (the seam)
    /// and sets bit 7 as the cached mark.
    /// </summary>
    // fidelity: M6-025
    private int NextSourceA01768(WwisePlayingInstance pbi, out int index)
    {
        // The native callers other than AddSrc (0xA37258, 0xA373B4, 0xA37578, 0xA37650, 0xA37944, 0xA55B54) share this
        // pbi+0x1BB cache. UNRESOLVED: the bus model WwiseVoiceBusPass.NextSource keeps a separate cache field
        // (WwiseMixBus.NextSource1BB); the two are not unified here.
        if ((pbi.NextSourceCache1BB & 0x80) == 0)
        {
            var node = pbi.NodeE0;                                               // [pbi+0xE0]
            int code, idx;
            if (NextSource9EEDA4 is { } over) code = over(node, out idx);        // an override of the read body
            else
                code = (Limiter ?? throw MissingSeam("the limiter that holds the 0x9EEDA4 body")).BehaviourCode9EEDA4(
                    node ?? throw MissingSeam("[pbi+0xE0] (0xA017A0)"), out idx);   // 0xA017A0
            if (code == 3)                                                       // 0xA017A4 cmp r0,#3
            {
                int r = (NodeVt120A379D8 ?? throw MissingSeam("vt+0x120 (0xA017C8..0xA017E4)"))(node, pbi.TargetNodeId);
                code = r == 0 ? 1 : 2;                                           // only the mapped value is stored
            }
            pbi.NextSourceCache1BB = (byte)(0x80 | (idx & 7) | ((code & 0xF) << 3));
        }
        index = pbi.NextSourceCache1BB & 7;
        return (pbi.NextSourceCache1BB >> 3) & 0xF;
    }

    private static WwiseMissingBehaviourException MissingSeam(string what)
        => new($"M6-025 C27: {what} is unread; supply the seam");

    /// <summary>B12/B13: the default factory over the PBI's descriptor.</summary>
    private IWwiseVoiceSource? DefaultSource(WwisePlayingInstance pbi)
    {
        // Only a null from the factory for an unselected kind is the native null-source path (0xA558E4). A missing
        // descriptor type or media resolver is not a native condition, so it must not look like one.
        if (pbi.SourceDescriptor is not WwiseSourceDescriptor descriptor)
            throw new WwiseMissingBehaviourException(
                "M6-025 C27: the PBI's source descriptor is not a WwiseSourceDescriptor; supply SourceFactory");
        if (MediaFor is null)
            throw new WwiseMissingBehaviourException(
                "M6-025 C27: no MediaFor and no SourceFactory; a missing media resolver is not the native null-source path");
        int mode = WwiseSourceFactory.ModeForStream(descriptor.PluginId, descriptor.StreamType);
        var kind = WwiseSourceFactory.Select(mode, descriptor.PluginId);
        if (kind is null) return null;
        return WwiseSourceFactory.Create(kind.Value, descriptor, MediaFor, Codebooks);
    }

    /// <summary>
    /// <c>0x9D3C98</c> (C24.7, called at <c>0x9AFA7C</c> between <c>0x9FF308</c> and <c>0x9E6D2C</c>, before the
    /// voice pass <c>0xA57FF8</c> of the same frame): pass 1 <c>0x9D3644</c> only when the dirty byte
    /// <c>[0x108DA0C+0x28]</c> (<see cref="WwiseStartList.Gate"/>, <c>0x9D3CA4 ldrb r3,[r3,#0x28]; cmp r3,#0;
    /// beq</c>) is set, then always pass 2 <c>0x9D3864</c> (<c>0x9D3CB8 b 0x9D3864</c>). Returns the number of
    /// nodes pass 1 kept.
    /// </summary>
    // fidelity: M6-025
    public int DrainStartList()
    {
        int kept = 0;
        if (StartList.Gate) kept = RunStartListPass1();                          // 0x9D3CA4..0x9D3CB0
        RunStartListPass2();                                                     // 0x9D3CB8
        return kept;
    }

    /// <summary>
    /// <c>0x9D3644</c> alone (C24.7: the <c>0x9AFA34</c> branch before the frame loop runs it without the
    /// gate test): every node whose PBI has no voice is attached; returns the nodes kept.
    /// </summary>
    // fidelity: M6-025
    public int RunStartListPass1() => StartList.Drain(n => AttachVoice(n.Pbi, n), DestroyNodeA01800).Count;

    /// <summary>
    /// <c>0xA01800(pbi, 1)</c> (<c>0x9D3814</c>, C25.3): <c>pbi+0x154 = 0</c>, then the <c>0xA38600(pbi, 4, 1, 0)</c>
    /// notification. It runs after the node is unlinked and freed (C26.6).
    /// </summary>
    // fidelity: M6-025
    private void DestroyNodeA01800(WwiseStartListNode node) => DestroyPbiVoiceA01800(node.Pbi);

    /// <summary><c>0xA01800(pbi, 1)</c> (C25.3, C26.4): <c>pbi+0x154 = 0</c> then <c>0xA38600(pbi, 4, 1, 0)</c>.</summary>
    // fidelity: M6-025
    private void DestroyPbiVoiceA01800(WwisePlayingInstance pbi, int r1 = 1)
    {
        var notify = Notify38600 ?? throw new WwiseMissingBehaviourException(
            "M6-025 C25.3: 0xA38600 (the notification 0xA01800 tail-calls) is unread; supply the seam");
        pbi.Field154 = null;                                                     // pbi+0x154 = 0
        notify(pbi, 4, r1, 0);
    }

    /// <summary>
    /// <c>0xA56414(src, r1)</c> as the voice Term <c>0xA53EA8</c> calls it (M6-026 7.8): <c>pbi = [src+0xC]</c>; <c>[pbi+4] &amp; 0x100000</c> runs <c>0xA054D8</c>; then <c>0xA01800(pbi, r1)</c>
    /// (<c>pbi+0x154 = 0</c> and the code-4 notification with <c>r1</c>). The source's own <c>vt+0x2C</c> is not modelled. The wiring target of <see cref="WwiseVoiceLinkSeams.CloseSource56414"/>.
    /// </summary>
    // fidelity: M6-026
    public void CloseSourceA56414(IWwiseVoiceSource source, int r1)
    {
        var owner = OwnerOf(source);
        if ((PbiFlag4Bit100000 ?? throw MissingSeam("[pbi+4] & 0x100000 (0xA56454..0xA56468)"))(owner))
            (CallA054D8 ?? throw MissingSeam("0xA054D8 (0xA56468)"))(
                A054D8Context ?? throw MissingSeam("the *global first argument of 0xA054D8 (0xA56454..0xA56464)"),
                owner.PlayingId, source);
        DestroyPbiVoiceA01800(owner, r1);
        (SourceClose2C ?? throw MissingSeam("src vt+0x2C (0xA56440..0xA5644C)"))(source);
    }

    /// <summary>
    /// The queue item <c>0xA38600(pbi, code, r2, 0)</c> makes (7.8, 7.9) for the flush <c>0xA38420</c>: <c>0xA0188C</c> sets <c>1BC = (1BC &amp; ~2) | 0x10</c> for code 4 with <c>r2 == 1</c>; a code-4 item then unlinks
    /// the PBI from the global PBI list, runs <c>0x9D3470</c>, Term, the destructor and the free (<see cref="TerminatePbi"/>). Code 3 (<c>vt+0x38</c>, <c>vt+0x18</c>) is unread.
    /// </summary>
    // fidelity: M6-026
    public WwisePbiNotification NotificationA38600(WwisePlayingInstance pbi, int code, int r2)
        => new()
        {
            Code = code, Reason = r2,
            Handle = _ =>
            {
                if (code == 3) throw new WwiseMissingBehaviourException("M6-026 7.9: notification code 3 (pbi vt+0x38, vt+0x18) is unread");
                if (code == 4 && r2 == 1) pbi.Flags1BC = (byte)((pbi.Flags1BC & ~2) | 0x10);
            },
            Terminate = _ => TerminatePbi(pbi),
        };

    /// <summary>
    /// <c>0x9D3864</c>, pass 2 of the start list (C24.7, research rows 6.4/6.5). The list is walked in key
    /// groups (nodes sharing the key at <c>node+8</c>, <see cref="WwiseStartListNode.Tick"/>). In a group, a node
    /// with <c>+0xD</c> bit0 clear and state at most 1 is left alone when <c>pbi+0x1BC</c> bit5 is set and
    /// <c>pbi+0x1F8 == -1</c> (<c>0x9D39B0..0x9D39C8</c>); otherwise a PBI with no voice, or
    /// <see cref="WwiseVoiceLinker.ProcessPending"/> (<c>0xA431A8</c>) returning 2, unlinks the node and
    /// restarts the scan from the head (<c>0x9D39D4..0x9D39FC</c>, <c>0x9D38A4</c>); 0x3F keeps the node with its
    /// flag clear and skips the whole group this frame (<c>0x9D39E4</c>, <c>0x9D396C..0x9D3990</c>); 1 sets
    /// <c>+0xD</c> bit0 (<c>0x9D39E8..0x9D39F4</c>). A group with no 0x3F then runs each flagged node through the
    /// state dispatch (<c>DispatchNode</c>) and frees it; a node above state 1 with its flag clear goes to
    /// <c>0xA41854</c> (<see cref="FindLiveVoiceA41854"/>), which unlinks it on 0 and otherwise dispatches it when
    /// <c>+0xD</c> bit0 is then set (C25.2). How a key group is delimited (<c>0x9D3930..0x9D3974</c>) is read here as
    /// consecutive nodes with the same key (reported as MISSING).
    /// </summary>
    // fidelity: M6-025
    public void RunStartListPass2()
    {
        var nodes = StartList.Nodes;
        bool restart = true;
        while (restart)
        {
            restart = false;
            int i = 0;
            while (i < nodes.Count)
            {
                int end = i + 1;
                while (end < nodes.Count && nodes[end].Tick == nodes[i].Tick) end++;   // the key group [i, end)

                bool skipGroup = false;
                for (int j = i; j < end; j++)
                {
                    var node = nodes[j];
                    if ((node.D & 1) != 0 || node.Type > 1) continue;                  // 0x9D393C..0x9D3950
                    var pbi = node.Pbi;
                    if ((pbi.Flags1BC & 0x20) != 0 && pbi.Field1F8 == 0xFFFFFFFF) continue;   // 0x9D39B0..0x9D39C8
                    var voice = pbi.Field154;
                    int r = voice is null
                        ? 2
                        : (Linker ?? throw LinkerRequired()).ProcessPending(pbi, voice);      // 0xA431A8
                    if (r == 2)
                    {
                        nodes.RemoveAt(j);                                             // 0x9D39FC..0x9D3A74
                        restart = true;                                                // 0x9D38A4: from the head
                        break;
                    }
                    if (r == 0x3F) skipGroup = true;                                   // 0x9D39E4..0x9D39EC (r6 = 1)
                    else node.D |= 1;                                                  // 0x9D39E8..0x9D39F4
                }
                if (restart) break;

                if (skipGroup)
                {
                    i = end;                                                           // 0x9D396C..0x9D3990
                    continue;
                }

                // Group execution (0x9D38E0..0x9D3C58).
                int before = nodes.Count;
                foreach (var node in nodes.GetRange(i, end - i))
                {
                    if ((node.D & 1) != 0)
                    {
                        DispatchNode(node, node.Pbi.Field154);                         // jump table 0x9D3AA4
                        nodes.Remove(node);                                            // 0x9D3AD4..0x9D3B4C
                    }
                    else if (node.Type > 1)
                    {
                        // 0x9D38F8..0x9D3910 (C25.2): bl 0xA41854(node+4); 0 unlinks the node; otherwise node+0xD
                        // bit0 is re-tested and, if set, control goes to the state dispatch (0x9D3A9C) with the
                        // returned value as the voice.
                        var found = FindLiveVoiceA41854(node);
                        if (found is null) nodes.Remove(node);
                        else if ((node.D & 1) != 0)
                        {
                            DispatchNode(node, found);
                            nodes.Remove(node);                                        // 0x9D3AD4..0x9D3B4C
                        }
                    }
                }
                i = end - (before - nodes.Count);                                      // the group's survivors stay
            }
        }
    }

    /// <summary>
    /// The state dispatch of <c>0x9D3864</c> (jump table <c>0x9D3AA4</c>, C24.7) on
    /// <c>voice = [pbi+0x154]</c>: 0 <c>0xA54480</c>; 1 <c>0xA54480</c> then <c>vt+0x4C</c> (<c>0xA53558</c>);
    /// 2 <c>vt+0x4C</c>; 3 <c>vt+0x50</c> (<c>0xA5358C</c>); 4 <c>vt+0x54(voice,pbi)</c> (<c>0xA535D8</c>);
    /// 5 <c>vt+0x58</c> (<c>0xA53698</c>). A state above 5 takes the table default (<c>0x9D3AA8 b 0x9D3AD4</c>, C25.2):
    /// no call and no throw; the caller frees the node. <paramref name="voice"/> is <c>[pbi+0x154]</c> on the flagged
    /// path and the value <c>0xA41854</c> returned on the state-above-1 path (C25.2); a body that needs it and finds
    /// none is a native null dereference.
    /// </summary>
    private void DispatchNode(WwiseStartListNode node, WwiseLiveVoice? voice)
    {
        var pbi = node.Pbi;
        WwiseLiveVoice V() => voice ?? throw new InvalidOperationException(
            "M6-025 C24.7: a dispatched start-list node has no voice; the native dereferences it");
        switch (node.Type)
        {
            case 0:
                StartOrStop54480(V());
                break;
            case 1:
                StartOrStop54480(V());
                (VoiceVt4CA53558 ?? throw Missing4C()).Invoke(V());
                break;
            case 2:
                (VoiceVt4CA53558 ?? throw Missing4C()).Invoke(V());
                break;
            case 3:
                (VoiceVt50A5358C ?? throw new WwiseMissingBehaviourException("M6-025 C24.7: voice vt+0x50 (0xA5358C) is unread")).Invoke(V());
                break;
            case 4:
                (VoiceVt54A535D8 ?? throw new WwiseMissingBehaviourException("M6-025 C24.7: voice vt+0x54 (0xA535D8) is unread")).Invoke(V(), pbi);
                break;
            case 5:
                (VoiceVt58A53698 ?? throw new WwiseMissingBehaviourException("M6-025 C24.7: voice vt+0x58 (0xA53698) is unread")).Invoke(V());
                break;
            default:
                break;                                                           // 0x9D3AA8 b 0x9D3AD4: frees the node, no call, no throw (C25.2)
        }
    }

    /// <summary>
    /// <c>0xA41854(node+4)</c> (C26.1, <c>0xA4187C..0xA41908</c>): walks the live-voice list (<see cref="Voices"/>). For every
    /// node it tests <c>[[voice+0xD4]+0xC] == pbi</c> (the current source's owner PBI); only when the node type is 4
    /// does it also test <c>[[voice+0xD8]+0xC] == pbi</c> (the pending source). A hit sets <c>node+0xD</c> bit0 when
    /// <c>[voice+0xDC] != 0</c> and returns the voice. With no hit the fallback loads <c>r3 = [pbi+0x154]</c> and
    /// returns it when <c>[[r3+0xD4]+0xC] == pbi</c> or the node type is 4, else 0 (the caller unlinks the node); the
    /// fallback never sets bit0. Term clears <c>[voice+0xD4]</c> (<c>0xA53F04</c>), so a null source never matches.
    /// </summary>
    // fidelity: M6-025
    private WwiseLiveVoice? FindLiveVoiceA41854(WwiseStartListNode node)
    {
        var pbi = node.Pbi;
        foreach (var voice in Voices)
        {
            bool hit = voice.Source is { } current && ReferenceEquals(OwnerOf(current), pbi);   // 0xA4187C..0xA41890
            if (!hit && node.Type == 4 && voice.Pending is { } pending)                        // 0xA41898..0xA418B4
                hit = ReferenceEquals(OwnerOf(pending), pbi);
            if (!hit) continue;
            if (voice.State != 0) node.D |= 1;                                   // 0xA418B8..0xA418C8: node+0xD bit0
            return voice;
        }
        var r3 = pbi.Field154;                                                   // 0xA418D4: the fallback
        if (r3 is null) return null;
        if (node.Type == 4) return r3;
        return r3.Source is { } src && ReferenceEquals(OwnerOf(src), pbi) ? r3 : null;   // [[r3+0xD4]+0xC] == pbi
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<IWwiseVoiceSource, WwisePlayingInstance> _sourceOwners = new();

    /// <summary>
    /// Records <c>[source+0xC]</c>, the owner PBI the source factory stores (C24 header, C26.1). <see cref="AddSrc"/>
    /// records every source it builds; a source that reaches a voice any other way must be registered here.
    /// </summary>
    // fidelity: M6-025
    public void RegisterSourceOwner(IWwiseVoiceSource source, WwisePlayingInstance pbi)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pbi);
        _sourceOwners.AddOrUpdate(source, pbi);
    }

    /// <summary><c>[source+0xC]</c>: the owner PBI of a source the bridge built, or null (the voice pass's <see cref="WwiseVoiceBusPass.SourceOwner"/>).</summary>
    // fidelity: M6-026
    public WwisePlayingInstance? TryOwnerOf(IWwiseVoiceSource source) => _sourceOwners.TryGetValue(source, out var pbi) ? pbi : null;

    private WwisePlayingInstance OwnerOf(IWwiseVoiceSource source)
        => _sourceOwners.TryGetValue(source, out var pbi) ? pbi : throw new WwiseMissingBehaviourException(
            "M6-025 C26.1: a live voice's source cannot report its owner PBI ([source+0xC], 0xA4187C..0xA41890); " +
            "the source-side owner is not built, so register it with RegisterSourceOwner");

    private static WwiseMissingBehaviourException Missing4C()
        => new("M6-025 C24.7: voice vt+0x4C (0xA53558) is unread");

    private static NotSupportedException LinkerRequired()
        => new("M6-025 C24.7: pass 2 needs a WwiseVoiceLinker (0xA431A8); supply it rather than skipping the node");

    /// <summary>
    /// <c>0xA54480(voice)</c> (C24.7 and research 5.14): with <c>[voice+0xDC] == 0</c> run <c>0xA56478([voice+0xD4])</c>
    /// and set the state to 1; state 2 returns; any other state calls <c>voice-&gt;vt+0x48</c>.
    /// </summary>
    private void StartOrStop54480(WwiseLiveVoice voice)
    {
        if (voice.State == 0)
        {
            (StartSourceA56478 ?? throw new WwiseMissingBehaviourException(
                "M6-025 C24.7: 0xA56478 (the source start run by 0xA54480) is unread")).Invoke(voice);
            voice.State = 1;                                                     // [voice+0xDC] = 1
            return;
        }
        if (voice.State == 2) return;
        (voice.VoiceStop48 ?? throw new WwiseMissingBehaviourException(
            "M6-025 C24.7: voice vt+0x48 (called by 0xA54480 for a voice whose state is neither 0 nor 2) is unread")).Invoke();
    }

    /// <summary>
    /// <c>0x9D3CC0(frames)</c> (C24.1, called at <c>0xA44978</c> in the voice pass): walks the pending-voice list
    /// (<see cref="WwiseVoiceLinker.PendingVoices"/>, <c>0x108DA30</c>) with <c>p = [voice+8] = pbi+0xC</c>. When
    /// <c>pbi+0x1BC</c> bit5 is set and <c>pbi+0x1F8 == -1</c>, every start-list node of that PBI is freed, the voice
    /// leaves the list and <c>0x9D40C4(voice, 0)</c> destroys it. Otherwise, when bit7 is clear and
    /// <c>pbi+0x1D8 &gt;= 0</c>, <c>pbi+0x1D8 -= round(frames * pbi+0x164)</c> (<c>+0.5</c>, or <c>-0.5</c> for a
    /// product of 0 or less, then truncated). <c>frames</c> is the u16 at <c>[0x1052440]</c> (loaded at
    /// <c>0xA44970..0xA44974</c>), the same global as the line Init's, so it is
    /// <see cref="WwiseVoiceLinker.LineMaxFrames"/>, converted unsigned (<c>vcvt.f32.u32</c>; C25.7).
    /// </summary>
    // fidelity: M6-025
    public void WalkPendingVoices()
    {
        var linker = Linker ?? throw LinkerRequired();
        uint frames = linker.LineMaxFrames;                                      // 0xA44970..0xA44974, u16
        foreach (var voice in linker.PendingVoices.ToArray())                    // 0x9D3CD0, 0x9D3D70
        {
            var pbi = voice.BusOwner8 as WwisePlayingInstance ?? throw new InvalidOperationException(
                "M6-025 C24.1: a pending voice's [voice+8] is not its PBI (AddSrc stores pbi+0xC)");
            if ((pbi.Flags1BC & 0x20) != 0 && pbi.Field1F8 == 0xFFFFFFFF)        // 0x9D3D10..0x9D3D1C
            {
                StartList.Nodes.RemoveAll(n => ReferenceEquals(n.Pbi, pbi));     // 0x9D3D90..0x9D3E9C
                linker.PendingVoices.Remove(voice);                              // 0x9D3DD8..0x9D3E10
                linker.TeardownVoice(voice);                                     // 0x9D3E14 0x9D40C4(voice, 0)
            }
            else if ((pbi.Flags1BC & 0x80) == 0 && unchecked((int)pbi.StartOffset) >= 0)   // 0x9D3D34
            {
                float product = (float)frames * pbi.Ratio;                       // 0x9D3D38
                int k = (int)(product > 0f ? product + 0.5f : product - 0.5f);   // 0x9D3D30..0x9D3D60
                pbi.StartOffset = unchecked((uint)((int)pbi.StartOffset - k));   // 0x9D3D5C..0x9D3D68
            }
        }
    }
}

/// <summary>
/// <c>0x9EEDA4([pbi+0xE0], out index)</c> (C27 step 4): returns the raw code and writes the out index.
/// </summary>
public delegate int WwiseNextSource9EEDA4(WwiseNode? node, out int index);

/// <summary>
/// The visible stop for a start-list dispatch body that the approved inventory does not settle (C24.7): a voice
/// whose node reaches such a body without a supplied seam fails loudly instead of doing nothing.
/// </summary>
public sealed class WwiseMissingBehaviourException : NotSupportedException
{
    /// <param name="message">What is missing.</param>
    public WwiseMissingBehaviourException(string message) : base(message) { }
}
