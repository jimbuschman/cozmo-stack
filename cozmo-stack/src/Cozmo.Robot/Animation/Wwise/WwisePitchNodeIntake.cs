// fidelity: M6-022, M6-025
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The pitch node's consumption of the block the source delivered (<c>0xA52DA8..0xA53050</c>: the resample / pitch pass, and the release of the source's block through <c>vt+0xC</c> when it is whole consumed,
/// <c>0xA52F0C..0xA52F18</c>): not read by any adopted row (M6-004's resampler and the node's accumulation of several source calls into the voice's output frame), so it is the one named seam of the source-to-voice
/// contract. <paramref name="held"/> is the node's copy of the state (<c>node+0x60</c>), <paramref name="buffer"/> the voice's buffer, <paramref name="releaseSourceBlock"/> the source's <c>vt+0xC</c>. It returns the result
/// code the pitch node leaves in <c>[state+0x28]</c>.
/// </summary>
public delegate int WwisePitchNodeConsume(WwiseDecodeState held, WwiseVoiceBuffer buffer, Action releaseSourceBlock);

/// <summary>
/// The intake of the pitch node's execute <c>0xA52D4C(node, state)</c> (research live-bodies-6 P01, C36.3): with the result 0x11 it continues at <c>0xA52EBC</c> (the last-buffer path, not read: a named seam). Otherwise,
/// when the node holds no frames (<c>u16[node+0x6E] == 0</c>): a state with 0 valid frames (<c>u16[state+0xE]</c>) and the result 0x2D becomes the result 0x2B (the voice then calls the source again, <c>0xA44784..0xA4478C</c>),
/// else the 0x28-byte state is copied to <c>node+0x60</c> (<c>0xA52D88..0xA52DA4</c>: every word of it but the result at <c>+0x28</c>). With frames held the state is not copied. Then the consumption (<c>0xA52DA8</c>, a seam).
/// </summary>
public sealed class WwisePitchNodeIntake
{
    private readonly Action _releaseSourceBlock;

    /// <summary>Creates the intake for a source whose <c>vt+0xC</c> is <paramref name="releaseSourceBlock"/> (<c>[node+4]</c> is the source, <c>0xA52F0C..0xA52F18</c>).</summary>
    public WwisePitchNodeIntake(Action releaseSourceBlock) => _releaseSourceBlock = releaseSourceBlock ?? throw new ArgumentNullException(nameof(releaseSourceBlock));

    /// <summary><c>[node+0x48]</c>: the frames asked, <c>u16[state+0xC]</c> (stored by <c>0xA53154</c> at the start of the pitch pass <c>0xA53134</c>).</summary>
    public ushort Word48 { get; set; }

    /// <summary>The byte <c>[node+0xB9]</c>, cleared by the pitch pass (<c>0xA5314C</c>); written elsewhere (not read).</summary>
    public byte ByteB9 { get; set; }

    /// <summary>The byte <c>[node+0xB8]</c>: the last buffer was taken (set by <c>0xA52EBC</c>, <c>0xA52EC4</c>). The pitch pass turns it into the result 0x11 when the node holds no frames (<c>0xA53190..0xA5319C</c>).</summary>
    public byte ByteB8 { get; set; }

    /// <summary><c>node+0x60</c>: the copy of the source's io state (0x28 bytes: everything but the result).</summary>
    public WwiseDecodeState Held60 { get; } = new();

    /// <summary><c>u16[node+0x6E]</c>: the valid frames of the held state.</summary>
    public ushort HeldFrames6E => Held60.ValidFrames;

    /// <summary>The consumption from <c>0xA52DA8</c> on (M6-004; unread). Unset: reaching it throws.</summary>
    public WwisePitchNodeConsume? Consume { get; set; }

    /// <summary>The last-buffer path <c>0xA52EBC</c> taken with the result 0x11 (unread). It receives the source's state itself (the intake branches there before the copy to <c>node+0x60</c>). Unset: reaching it throws.</summary>
    public WwisePitchNodeConsume? EndOfStream { get; set; }

    /// <summary>
    /// <c>0xA52D4C(node, state)</c>: see the class summary. Returns the result left in <c>[state+0x28]</c> (0x2B when the source delivered nothing, else the consumption's).
    /// </summary>
    public int IntakeA52D4C(WwiseDecodeState state, WwiseVoiceBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(buffer);
        int result = state.Code28;                                              // 0xA52D54 ldr r3,[r1,#0x28]
        if (result == 0x11)                                                     // 0xA52D5C..0xA52D60 beq 0xA52EBC
            return (EndOfStream ?? throw new WwiseMissingBehaviourException(
                "M6-022 P01: the pitch node's last-buffer path 0xA52EBC (the result 0x11 of the last frames of a play) is not read; supply WwisePitchNodeIntake.EndOfStream"))(state, buffer, _releaseSourceBlock);   // the state itself: 0xA52D54..0xA52D60 branch before the copy
        if (HeldFrames6E == 0)                                                  // 0xA52D64..0xA52D6C ldrh r2,[r0,#0x6e]; bne 0xA52DA8
        {
            if (state.ValidFrames == 0 && result == 0x2D)                       // 0xA52D70..0xA52D78 cmp r2,#0; cmpeq r3,#0x2d
            {
                state.Code28 = 0x2B;                                            // 0xA52D7C..0xA52D80
                return 0x2B;                                                    // 0xA52D84
            }
            var h = Held60;                                                     // 0xA52D88..0xA52DA4: the words +0x00..+0x24
            h.Data = state.Data;
            h.Scratch08 = state.Scratch08;
            h.ChannelConfig = state.ChannelConfig;
            h.MaxFrames = state.MaxFrames;
            h.ValidFrames = state.ValidFrames;
            h.MarkerCount = state.MarkerCount;
            h.Markers = state.Markers;
            h.Word1C = state.Word1C;
            h.Position = state.Position;
            h.Total = state.Total;
            h.Rate = state.Rate;
        }
        return (Consume ?? throw new WwiseMissingBehaviourException(
            "M6-022 P01: the pitch node's consumption 0xA52DA8..0xA53050 (the resample, the accumulation into the voice's output frame, the release through the source's vt+0xC) is not read; supply WwisePitchNodeIntake.Consume"))(Held60, buffer, _releaseSourceBlock);
    }
}
