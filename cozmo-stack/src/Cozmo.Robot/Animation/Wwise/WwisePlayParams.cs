// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The Play fade-in transition object built at <c>sp+0x10</c> and pointed to by the params'
/// <c>+0xC</c> (M6-025 B1, residual Q1a). It is the <c>arg2</c> handed to <c>0xA0067C</c>
/// (<c>0xA3808C/0xA38094</c>); <c>0xA0067C</c> sets the fade up only when <c>arg2[0] != 0</c>.
///
/// <para><b>Rows.</b> <c>sp+0x10</c> = the fade-in time from <c>0xA61110</c> (prop 0x10 + its ranged
/// term, <c>0xA62AF0</c>); <c>sp+0x14</c> = the curve <c>action+0x22 &amp; 0x1F</c> (<c>0xA62A8C</c>);
/// <c>sp+0x18</c> = a zero byte (<c>0xA62A4C</c>). The <c>0xA36268</c>/<c>0xA366F4</c> bodies that consume
/// it are unread, so this object carries the settled fields only.</para>
/// </summary>
public sealed class WwiseFadeInTransition
{
    /// <summary><c>sp+0x10</c>: the fade-in time (<c>0xA62AF0</c>, from <c>0xA61110</c>).</summary>
    public float FadeInTime { get; set; }

    /// <summary><c>sp+0x14</c>: the fade curve, <c>action+0x22 &amp; 0x1F</c> (<c>0xA62A8C</c>).</summary>
    public byte FadeCurve { get; set; }

    /// <summary><c>0xA0067C</c> tests <c>arg2[0] != 0</c> to decide whether to set the fade up.</summary>
    public bool IsSet => FadeInTime != 0f;
}

/// <summary>
/// The Play params struct <c>0xA62A1C</c> builds on the stack (base <c>sp+0x1c</c>, M6-025 B1/B3/B4 and
/// the residual Q1b). Only the fields the Play helper actually writes are present.
///
/// <para><b>Not modelled, because the source does not settle them.</b>
/// <list type="bullet">
/// <item><b><c>+0x14</c>:</b> no store exists in <c>0xA62A1C</c> and none of the Play consumers
/// (<c>0xA000E8</c>, <c>0xA379D8</c>, <c>0xA1D448</c>, <c>0x9F12E0</c>, <c>0xA02494</c>) reads it
/// (residual, "Open questions" 1). It is deliberately absent rather than zero-filled.</item>
/// <item><b><c>+0x28..+0x6B</c>:</b> the only store is the first word (<c>0xA62B7C</c>); the rest is
/// uninitialised stack on the Sound path (residual Q1d). <see cref="Block28"/> is therefore a caller
/// input; the bridge records the gap and does not invent its contents.</item>
/// <item><b><c>+0x18/+0x1C/+0x20</c> semantics</b> (the queued-action custom-param values) are
/// UNKNOWN; the values are carried because the ctor consumes them, not because their meaning is
/// recovered.</item>
/// </list></para>
/// </summary>
public sealed class WwisePlayInitParams
{
    /// <summary><c>+0x04</c>: the resolved target node (<c>0xA62B90</c>).</summary>
    public uint TargetNodeId { get; set; }

    /// <summary><c>+0x08</c>: the game object (queued action <c>+0x34</c>, <c>0xA62BF0</c>).</summary>
    public uint? GameObjectId { get; set; }

    /// <summary><c>+0x0C</c>: the fade-in transition pointer (<c>0xA62BFC</c>).</summary>
    public WwiseFadeInTransition? Transition { get; set; }

    /// <summary><c>+0x10</c>: the custom-params object (queued action <c>+0x14</c>, <c>0xA62BC0</c>).</summary>
    public object? CustomParams { get; set; }

    /// <summary><c>+0x18</c>: queued action <c>+0x1C</c> (<c>0xA62B88</c>); meaning UNKNOWN.</summary>
    public uint CustomParam1 { get; set; }

    /// <summary><c>+0x1C</c>: queued action <c>+0x20</c> (<c>0xA62B8C</c>); meaning UNKNOWN.</summary>
    public uint CustomParam2 { get; set; }

    /// <summary><c>+0x20</c>: queued action <c>+0x24</c> (<c>0xA62B98</c>); meaning UNKNOWN.</summary>
    public uint CustomParam3 { get; set; }

    /// <summary><c>+0x24</c>: the playing id (queued action <c>+0x28</c>, <c>0xA62B94</c>).</summary>
    public uint PlayingId { get; set; }

    /// <summary>
    /// <c>+0x28</c>: the start of the 0x44-byte block <c>0xA000E8</c> copies to <c>pbi+0x170</c>
    /// (<c>0xA00344/0xA0035C</c>). On the Sound path only its first word is written; the rest is
    /// uninitialised stack (residual Q1d), so it is a caller input and the gap is recorded.
    /// </summary>
    public byte[] Block28 { get; set; } = new byte[0x44];

    /// <summary><c>+0x70</c>: 0 in the Play helper; read as <c>(params+0x70 == 1)</c> by <c>0xA0067C</c>.</summary>
    public uint Flag70 { get; set; }

    /// <summary><c>+0x74</c>: the initial delay / start offset (queued action <c>+0x0C</c>, <c>0xA62BC8</c>).</summary>
    public uint InitialDelaySamples { get; set; }

    /// <summary><c>+0x7C</c>: the chain id; 0 in the Play helper, so the ctor auto-allocates one.</summary>
    public uint ChainId { get; set; }

    /// <summary><c>+0x84</c>: the Sound special-branch byte; 0 in the Play helper (<c>0xA62A98</c>).</summary>
    public byte SoundSpecial84 { get; set; }

    /// <summary><c>+0x85</c>: 0xFF in the Play helper (<c>0xA62AF4</c>).</summary>
    public byte SoundSpecial85 { get; set; } = 0xFF;

    /// <summary><c>+0x90</c>: 1.0f in the Play helper (<c>0xA62AF8</c>); not read by the paths read.</summary>
    public float Field90 { get; set; } = 1f;

    /// <summary>
    /// <c>+0x128</c>: the flags byte. bit2 is set for every Play; bit3 is the action's isBus
    /// (<c>0xA62C04</c>); bits 4..6 come from the node's <c>0xA000E8</c> read; bit1 is the special
    /// branch only (<c>0xA62D0C</c>).
    /// </summary>
    public byte Flags128 { get; set; }
}

/// <summary>
/// The source descriptor a Sound embeds at <c>node+0x5c</c> (M6-025 B14, <c>0xA1DA08</c>): the bank's
/// source block <c>{u32 plugin, u8 stream, u32 sourceId, u32 size, u8 bits}</c> (<c>0x9B9CC8..0x9B9DC0</c>).
/// For the Sound path the Play helper passes <c>node+0x5c</c> straight to the PBI creator as
/// <c>param_4</c>, which becomes <c>pbi+0x150</c> (<c>0xA1D46C/0xA1D470</c>, <c>0xA00208</c>).
/// </summary>
public readonly record struct WwiseSourceDescriptor(
    uint PluginId, byte StreamType, uint SourceId, uint InMemorySize, byte SourceBits)
{
    /// <summary>The descriptor a shipped Sound node carries (<c>node+0x5c</c>).</summary>
    public static WwiseSourceDescriptor FromSound(WwiseSoundNode node) =>
        new(node.PluginId, node.StreamType, node.MediaId, node.InMemorySize, node.SourceBits);
}