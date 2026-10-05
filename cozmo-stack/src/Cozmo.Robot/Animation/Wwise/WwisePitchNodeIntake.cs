// fidelity: M6-022, M6-025, M6-004
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The voice's pitch node <c>N = voice+0x100</c> (M6-022 P01, M6-004; C36.3, C38.1): the object whose vtable <c>0x103C610</c> sits between the source and the voice's insert-FX chain. It owns the resampler <c>R = N+8</c>
/// (<see cref="Resampler"/>), the held copy of the source's block <c>IN = N+0x60</c> (<see cref="Held60"/>), the output block <c>OUT = N+0x88</c> (<see cref="Out"/>), the frame count the voice asks for <c>[N+0x48]</c>
/// (<see cref="Word48"/>), the upstream source <c>[N+4]</c> (<see cref="Upstream"/>), the voice <c>[N+0xB0]</c>, the owner PBI <c>[N+0xB4]</c> (<see cref="Pbi"/>) and the bytes <c>[N+0xB8]</c> (the source's last block was
/// taken), <c>[N+0xB9]</c> (the output position was set) and <c>[N+0xBA]</c> (the first-buffer fill is pending).
///
/// <para>The pitch pass <c>0xA53134</c> (<see cref="WwiseLiveVoice"/>, which owns the call order) ends in the intake <see cref="IntakeA52D4C"/>: <c>0xA52D4C</c> (the 0x11 path <c>0xA52EBC</c>, the copy of the source's block to
/// <c>IN</c>, the 0x2D with no frames converted to 0x2B) and the consumption <c>0xA52DA8</c>: the output allocation <c>0xA69A70</c> and the first-buffer silent fill (<c>0xA53030..0xA531A0</c>), the start-offset skip
/// (<c>0xA52DB8..0xA52E10</c>), the resample (<c>0xA47178(R, IN, OUT)</c>), the marker carry (<c>0xA5268C</c>), the output position and the pitch factor, the release of the source's block and the pending-source arm
/// (<c>0xA52F0C..0xA52F84</c>, <c>0xA52B90</c>), and the hand-off of OUT to the voice's state (<c>0xA52ED8</c>). <see cref="ReleaseBufferA52800"/> frees OUT again after the mix.</para>
///
/// <para><b>Visible stops (MISSING).</b> <c>0xA549A0</c> (SwitchToNextSrc, only an outline in inventory row 1.7) is the required seam <see cref="SwitchToNextSrcA549A0"/>; the two context calls the arm makes through the new PBI's object at
/// <c>pbi+0xC</c> (<c>0xA52CB4..0xA52CE0</c>) are the required seams <see cref="ContextVt24A52D38"/> and <see cref="ContextVt28A52CD4"/> (CalcEffectiveParams and <see cref="WwisePlayPath.Recompute9FF368"/>);
/// a zero-sized output allocation (<c>0xA7A894</c>, unread) and the node's <c>vt+0x10..0x1C</c> (loop restart, seek, stop: P1-17, unread) are not modelled.</para>
/// </summary>
public sealed class WwisePitchNodeIntake
{
    private static readonly uint One = BitConverter.SingleToUInt32Bits(1f);

    /// <summary>The upstream source <c>[N+4]</c> (<c>vt+0x24</c> stores it; the pending-source arm stores <c>[voice+0xD4]</c>, <c>0xA52CB0</c>).</summary>
    public IWwisePitchNodeSource? Upstream { get; set; }

    /// <summary>The voice <c>[N+0xB0]</c> (stored by the voice constructor, <c>0xA54834</c>).</summary>
    public WwiseLiveVoice? Voice { get; set; }

    /// <summary>The owner PBI <c>[N+0xB4]</c> (stored by <see cref="InitA5321C"/> and by the pending-source arm): the start offset <c>+0x1B4</c>, the flags <c>+0x1BD/+0x1BE</c> and the first-buffer operands <c>+0x1D8/+0x164</c> are read from it.</summary>
    public WwisePlayingInstance? Pbi { get; set; }

    /// <summary>The resampler <c>R = N+8</c> (the constructor <c>0xA46D70</c> ran in the voice constructor, <c>0xA547A8</c>).</summary>
    public WwiseResampler Resampler { get; } = new();

    /// <summary><c>u16 [node+0x48]</c> (= <c>R+0x40</c>): the frames asked, <c>u16[state+0xC]</c> (stored by <c>0xA53154</c> at the start of the pitch pass; it is the resampler's output limit).</summary>
    public ushort Word48
    {
        get => unchecked((ushort)Resampler.Limit40);
        set => Resampler.Limit40 = value;
    }

    /// <summary>The byte <c>[node+0xB9]</c>, cleared by the pitch pass (<c>0xA5314C</c>) and set when the output position is carried (<c>0xA52EB4</c>).</summary>
    public byte ByteB9 { get; set; }

    /// <summary>The byte <c>[node+0xB8]</c>: the last buffer was taken (set by <c>0xA52EC4</c>; cleared by <c>0xA5321C</c> and by a pending-source switch, <c>0xA52D1C</c>). The pitch pass turns it into the result 0x11 when the node holds no frames (<c>0xA53190..0xA5319C</c>).</summary>
    public byte ByteB8 { get; set; }

    /// <summary>The byte <c>[node+0xBA]</c>: the first-buffer silent fill is pending (set by <c>0xA5321C</c>, cleared at the first output allocation).</summary>
    public byte ByteBA { get; set; }

    /// <summary><c>node+0x60</c>: the copy of the source's io state (0x28 bytes: everything but the result).</summary>
    public WwiseDecodeState Held60 { get; } = NewIdleBuffer();

    /// <summary><c>u16[node+0x6E]</c>: the valid frames of the held state.</summary>
    public ushort HeldFrames6E => Held60.ValidFrames;

    /// <summary><c>node+0x88</c>: the output block the resampler fills; after a pass it is copied to the voice's state and released after the mix.</summary>
    public WwiseDecodeState Out { get; } = NewIdleBuffer();

    /// <summary>The pool allocator's failure (host input; null = the pool never fails): called once per pool allocation the node makes (<c>0xA69A70</c> for OUT, <c>0xA7A7F4</c> for the marker array), in the engine's order.</summary>
    public Func<bool>? AllocationFails { get; set; }

    /// <summary><c>0xA549A0(voice)</c>: SwitchToNextSrc (<c>0xA52C9C</c>), only an outline in the inventory (row 1.7): required when the pending-source arm matches. Unset: MISSING.</summary>
    public Action<WwiseLiveVoice>? SwitchToNextSrcA549A0 { get; set; }

    // The seam must set [voice+0xD4] = [voice+0xD8] (0xA549E8..0xA549F0); the node then performs the verified store [voice+0xD8] = 0 (0xA549EC, 0xA549F8) itself.

    /// <summary>The new PBI's context object (<c>pbi+0xC</c>) <c>vt+0x24</c> (<c>0xA52D38..0xA52D40</c>), called with <c>r1 = 0</c> when bit 5 of <c>[pbi+0xE8]</c> is clear: CalcEffectiveParams (<see cref="WwisePlayPath.CalcEffectiveParams"/>, <c>(pbi, null, limiter)</c>). Unset: MISSING.</summary>
    public Action<WwisePlayingInstance>? ContextVt24A52D38 { get; set; }

    /// <summary>The new PBI's context object <c>vt+0x28</c> (<c>0xA52CD4..0xA52CDC</c>), called when bit 5 of <c>[pbi+0xE8]</c> is set and bit 0 of <c>[pbi+0xE9]</c> is set: <see cref="WwisePlayPath.Recompute9FF368"/>. Unset: MISSING.</summary>
    public Action<WwisePlayingInstance>? ContextVt28A52CD4 { get; set; }

    /// <summary>The reset values of an idle block (<c>0xA54830..0xA54850</c>, <c>0xA52F28..0xA52F58</c>): no data, no markers, eState 0x2B, position -1, <c>1.0f</c>, total -1, rate 1.</summary>
    private static WwiseDecodeState NewIdleBuffer() => new()
    {
        Data = null, ChannelConfig = 0, Scratch08 = 0x2B, MaxFrames = 0, ValidFrames = 0, MarkerCount = 0, Markers = null,
        Position = 0xFFFFFFFF, Word1C = One, Total = 0xFFFFFFFF, Rate = 1,
    };

    /// <summary>
    /// <c>0xA5321C(node, fmt, pbi, outRate)</c> (called by the voice init <c>0xA54A30</c> at <c>0xA54A78</c>): <c>[N+0xB4] = pbi</c>, <c>byte [N+0xB8] = 0</c>, <c>byte [N+0xBA] = 1</c>, then the resampler's <c>Init(fmt, outRate)</c> (<c>0xA47038</c>).
    /// Returns its result (1, or 2 on a pool failure).
    /// </summary>
    public int InitA5321C(WwiseResamplerFormat fmt, WwisePlayingInstance pbi, uint outRate)
    {
        Pbi = pbi;                                                              // 0xA53228
        ByteB8 = 0;                                                             // 0xA5322C
        ByteBA = 1;                                                             // 0xA53234
        Resampler.AllocationFails = AllocationFails;
        return Resampler.Init(fmt, outRate);                                    // 0xA53240 b 0xA47038
    }

    // ------------------------------------------------------------------ 0xA52D4C

    /// <summary>
    /// <c>0xA52D4C(node, state)</c>: a result of 0x11 goes to <see cref="LastBufferA52EBC"/>. Otherwise, when the node holds no frames (<c>u16[node+0x6E] == 0</c>): a state with 0 valid frames (<c>u16[state+0xE]</c>) and the result 0x2D becomes the
    /// result 0x2B (the voice then calls the source again, <c>0xA44784..0xA4478C</c>); else the 0x28-byte state is copied to <c>node+0x60</c> (<c>0xA52D88..0xA52DA4</c>: every word of it but the result at <c>+0x28</c>); with frames held nothing is
    /// copied. Then the consumption <see cref="ConsumeA52DA8"/>. Returns the result left in <c>[state+0x28]</c>.
    /// </summary>
    public int IntakeA52D4C(WwiseDecodeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        int result = state.Code28;                                              // 0xA52D54 ldr r3,[r1,#0x28]
        if (result == 0x11) return LastBufferA52EBC(state);                     // 0xA52D5C..0xA52D60 beq 0xA52EBC
        if (HeldFrames6E == 0)                                                  // 0xA52D64..0xA52D6C ldrh r2,[r0,#0x6e]; bne 0xA52DA8
        {
            if (state.ValidFrames == 0 && result == 0x2D)                       // 0xA52D70..0xA52D78 cmp r2,#0; cmpeq r3,#0x2d
            {
                state.Code28 = 0x2B;                                            // 0xA52D7C..0xA52D80
                return 0x2B;                                                    // 0xA52D84
            }
            CopyStateToHeld(state);                                             // 0xA52D88..0xA52DA4
        }
        return ConsumeA52DA8(state);
    }

    /// <summary>
    /// The last-buffer path <c>0xA52EBC</c> (the result 0x11): <c>byte [node+0xB8] = 1</c>; with no frames held the state is copied to <c>node+0x60</c> (<c>0xA52D88</c>), then the consumption (<c>0xA52DA8</c>) in both cases. (The inventory text
    /// that the 0x11 path "branches before the copy" is wrong: <c>0xA52EC8..0xA52ECC</c> copies when the node holds no frames.)
    /// </summary>
    public int LastBufferA52EBC(WwiseDecodeState state)
    {
        ushort held = HeldFrames6E;                                             // 0xA52EBC ldrh r3,[r0,#0x6e]
        ByteB8 = 1;                                                             // 0xA52EC4 strb r2,[r0,#0xb8]
        if (held == 0) CopyStateToHeld(state);                                  // 0xA52EC8..0xA52ECC beq 0xA52D88
        return ConsumeA52DA8(state);                                            // 0xA52ED0 b 0xA52DA8
    }

    /// <summary>The ten words <c>[state+0x00..0x27]</c> to <c>node+0x60..0x87</c> (<c>0xA52D88..0xA52DA4</c>); the result word is not copied.</summary>
    private void CopyStateToHeld(WwiseDecodeState state)
    {
        var h = Held60;
        h.Data = state.Data;
        h.ChannelConfig = state.ChannelConfig;
        h.Scratch08 = state.Scratch08;
        h.MaxFrames = state.MaxFrames;
        h.ValidFrames = state.ValidFrames;
        h.MarkerCount = state.MarkerCount;
        h.Markers = state.Markers;
        h.Position = state.Position;
        h.Word1C = state.Word1C;
        h.Total = state.Total;
        h.Rate = state.Rate;
    }

    // ------------------------------------------------------------------ 0xA52DA8

    /// <summary>
    /// The consumption <c>0xA52DA8</c> (P1-03..P1-10): the output is allocated when absent and the first buffer's silent frames are written; the start offset is skipped; <c>Execute(R, IN, OUT)</c> runs; the markers of the consumed
    /// range are carried; the output position and the pitch factor are stored; and the result decides between the hand-off of OUT, the source release / pending-source arm, and the failure result.
    /// </summary>
    private int ConsumeA52DA8(WwiseDecodeState state)
    {
        var pbi = Pbi ?? throw new WwiseMissingBehaviourException("M6-022 P1-04: the node's owner PBI [N+0xB4] (stored by 0xA5321C) is not set");
        if (Out.Data is null)                                                   // 0xA52DAC..0xA52DB4 ldr r5,[r5,#0x88]; beq 0xA53030
        {
            int allocated = AllocateOutA69A70(Out, Word48, Held60.ChannelConfig);   // 0xA53030..0xA5303C
            if (allocated != 1)                                                 // 0xA53040..0xA5304C
            {
                state.Code28 = 2;
                return 2;
            }
            if (ByteBA != 0) FirstBufferFill(pbi);                              // 0xA53050..0xA530E4
        }
        // 0xA52DB8: the start offset
        if ((pbi.Flags1BD & 0x80) == 0 && pbi.Word1B4 != 0)                     // 0xA52DBC..0xA52DD4
        {
            int d = unchecked((int)pbi.Word1B4);
            int v = HeldFrames6E;
            if (d >= v) return SkipEverythingA52F9C(state, pbi, d, v);          // 0xA52DD8..0xA52DE0 bge 0xA52F9C
            Resampler.InputOffset24 = unchecked((uint)d);                       // 0xA52DE4
            Held60.ValidFrames = unchecked((ushort)(v - d));                    // 0xA52DE8..0xA52DEC
            pbi.Flags1BE = (byte)(pbi.Flags1BE & 0xFC);                         // 0xA52DF0, 0xA52DF8, 0xA52E00 (bits 0 and 1 cleared)
            pbi.Word1B4 = 0;                                                    // 0xA52DFC
            pbi.Flags1BD = (byte)(pbi.Flags1BD & 0x7F);                         // 0xA52E04 bfi (bit 7 was clear)
        }
        // 0xA52E10: the resample
        uint fp = Resampler.InputOffset24;                                      // 0xA52E1C
        ushort before = HeldFrames6E;                                           // 0xA52E2C
        int r7 = Resampler.Execute(Held60, Out);                                // 0xA52E30 bl 0xA47178(R, IN, OUT)
        ushort consumed = unchecked((ushort)(before - HeldFrames6E));           // 0xA52E34..0xA52E44
        CarryMarkersA5268C(Held60, Out, fp, consumed);                          // 0xA52E50
        if (Held60.Position != 0xFFFFFFFF && ByteB9 == 0)                       // 0xA52E54..0xA52E68
        {
            Out.Position = unchecked(fp + Held60.Position);                     // 0xA52E9C, 0xA52EB0 (after the 16-byte copy of IN+0x18..0x27)
            Out.Word1C = Held60.Word1C;
            Out.Total = Held60.Total;
            Out.Rate = Held60.Rate;
            ByteB9 = 1;                                                         // 0xA52EB4
        }
        Out.Word1C = BitConverter.SingleToUInt32Bits(Resampler.CurrentFactorA46E30());   // 0xA52E6C..0xA52E7C bl 0xA46E30; str r0,[r4,#0xa4]
        if (HeldFrames6E == 0)                                                  // 0xA52E74..0xA52E80
        {
            ReleaseHeldA52F0C(state);                                           // 0xA52F0C..0xA52F64
            if (ByteB8 != 0)                                                    // 0xA52F24 ldrb r1,[r4,#0xb8]; 0xA52F68 beq 0xA52E84
            {
                var voice = Voice ?? throw new WwiseMissingBehaviourException("M6-022 P1-06: the node's voice [N+0xB0] is not set (0xA52F6C reads its pending source [voice+0xD8])");
                r7 = voice.Pending is null ? 0x11 : PendingSourceA52B90();      // 0xA52F6C..0xA52F84
            }
        }
        if (r7 == 0x2D || r7 == 0x11) return HandOffA52ED8(state, r7);          // 0xA52E84..0xA52E8C
        state.Code28 = r7;                                                      // 0xA52E90
        return r7;
    }

    /// <summary>The first-buffer silent fill (<c>0xA53050..0xA530E4</c>, P1-03): with <c>byte [N+0xBA]</c> set, <c>n = trunc_s32(x + (x &gt; 0 ? 0.5f : -0.5f))</c> with <c>x = (float(int32 [pbi+0x1D8]) + [pbi+0x164] * float(u16 [0x1052440])) / [pbi+0x164]</c>
    /// in float32 (<c>vmla</c> not fused); with <c>n &gt; 0</c> the first <c>n</c> frames of every channel of OUT are zeroed (channel stride <c>u16 [N+0x94]</c>) and <c>[R+0x28] = n</c> (OUT's valid count stays 0). The byte is cleared in both cases.</summary>
    private void FirstBufferFill(WwisePlayingInstance pbi)
    {
        float d = (float)unchecked((int)pbi.StartOffset);                       // 0xA5306C vldr s15,[r1,#0x1d8]; vcvt.f32.s32
        float p = pbi.Ratio;                                                    // 0xA53074 vldr s12,[r1,#0x164]
        float f = (float)WwiseLiveVoice.PullFrames1052440;                      // 0xA53070..0xA53080 ldrh r3,[r3] (u16[0x1052440]); vcvt.f32.s32
        float product = p * f;                                                  // 0xA53084 vmla.f32 s15,s12,s13: the product rounded ...
        float x = d + product;                                                  // ... then the add rounded
        float quotient = x / p;                                                 // 0xA53088 vdiv.f32
        float half = quotient <= 0f || float.IsNaN(quotient) ? -0.5f : 0.5f;   // 0xA53090..0xA53098 vcmpe.f32 s15,#0; vmovle (an unordered compare is "le")
        float sum = quotient + half;                                            // 0xA5309C vadd.f32
        int n = SaturatingS32(sum);                                             // 0xA530A0 vcvt.s32.f32
        if (n > 0)                                                              // 0xA530A8..0xA530AC ble 0xA530E0
        {
            // 0xA530B4 bl 0xA4721C: the realloc probe, a stub returning 0, so the path below (0xA530EC) is the live one; the other (0xA530C0..0xA530DC) is dead.
            if (n > Out.MaxFrames)
                throw new WwiseMissingBehaviourException("M6-022 P1-03: the first-buffer silent frames exceed the output block (the engine's memset overruns it); [pbi+0x1D8] is kept below 0 by the voice pass (0xA55228..0xA55244)");
            int channels = (byte)Out.ChannelConfig;                             // 0xA530EC ldrb sb,[r4,#0x8c]
            if (channels != 0)
            {
                var data = (float[])Out.Data!;
                int stride = Out.MaxFrames;                                     // 0xA530F8 ldrh fp,[r4,#0x94]
                for (int c = 0; c < channels; c++)
                    Array.Clear(data, c * stride, n);                           // 0xA5310C..0xA5311C memset(OUT.data + c * stride * 4, 0, n * 4)
            }
            Resampler.OutputOffset28 = unchecked((uint)n);                      // 0xA530DC str r7,[r4,#0x30]
        }
        ByteBA = 0;                                                             // 0xA530E0..0xA530E4
    }

    /// <summary><c>vcvt.s32.f32</c>: round toward zero, saturating, NaN gives 0.</summary>
    private static int SaturatingS32(float v)
    {
        if (float.IsNaN(v)) return 0;
        if (v >= 2147483648f) return int.MaxValue;
        if (v <= -2147483648f) return int.MinValue;
        return (int)v;
    }

    /// <summary>
    /// <c>0xA69A70(OUT, frames, cfg)</c>: allocates <c>frames * (byte cfg) * 4</c> bytes (16-aligned, from the pool) and sets <c>OUT.data</c>, <c>u16 [OUT+0xC] = frames</c>, <c>[OUT+4] = cfg</c>, <c>u16 [OUT+0xE] = 0</c>; returns 1, or 0x34 when the
    /// allocation fails (OUT is then untouched).
    /// </summary>
    private int AllocateOutA69A70(WwiseDecodeState buffer, ushort frames, uint cfg)
    {
        int channels = (byte)cfg;                                               // 0xA69A74 uxtb ip,r2
        int size = checked(frames * channels);
        if (size == 0)
            throw new WwiseMissingBehaviourException("M6-022 P1-03: a zero-sized output allocation (0xA7A894 with size 0) is not read; the pool allocator's result for it is unknown");
        if (AllocationFails?.Invoke() == true) return 0x34;                     // 0xA69A9C bl 0xA7A894; 0xA69AB8 moveq r0,#0x34
        buffer.Data = new float[size];                                          // 0xA69AA4 strne r0,[r4]
        buffer.MaxFrames = frames;                                              // 0xA69AA8
        buffer.ChannelConfig = cfg;                                             // 0xA69AB4
        buffer.ValidFrames = 0;                                                 // 0xA69ABC
        return 1;                                                               // 0xA69AB0
    }

    /// <summary>
    /// The skip-everything path <c>0xA52F9C</c> (P1-10, a start offset at or beyond the held frames): <c>[pbi+0x1B4] = d - v</c>, bit 7 of <c>+0x1BD</c> and bits 0, 1 of <c>+0x1BE</c> cleared, the held frames and <c>u16 [state+0xE]</c> zero, the source's block
    /// released (<c>vt+0xC</c>), IN reset, <c>[state+0x28] = [N+0xB8] ? 0x11 : 0x2B</c>, and the state's data, marker count and markers cleared (no pending-source switch here).
    /// </summary>
    private int SkipEverythingA52F9C(WwiseDecodeState state, WwisePlayingInstance pbi, int d, int v)
    {
        pbi.Word1B4 = unchecked((uint)(d - v));                                 // 0xA52FA0..0xA52FA8
        pbi.Flags1BD = (byte)(pbi.Flags1BD & 0x7F);                             // 0xA52FA4, 0xA52FB0
        pbi.Flags1BE = (byte)(pbi.Flags1BE & 0xFC);                             // 0xA52FAC, 0xA52FB4..0xA52FB8
        Held60.ValidFrames = 0;                                                 // 0xA52FC0
        state.ValidFrames = 0;                                                  // 0xA52FC4
        ReleaseUpstreamA52FBC();                                                // 0xA52FBC..0xA52FD0 vt+0xC
        ResetHeldA52FD4();                                                      // 0xA52FD4..0xA53018
        state.Code28 = ByteB8 != 0 ? 0x11 : 0x2B;                               // 0xA52FDC..0xA5301C
        state.Data = null;                                                      // 0xA53020
        state.MarkerCount = 0;                                                  // 0xA53024
        state.Markers = null;                                                   // 0xA53028
        return state.Code28;
    }

    private void ReleaseUpstreamA52FBC()
        => (Upstream ?? throw new WwiseMissingBehaviourException("M6-022 P1-06: the node's upstream source [N+4] is not set (vt+0xC, the release of the delivered block)")).ReleaseOutput();

    /// <summary><c>0xA69A38(IN)</c> (the held markers are freed: count 0, array null) and the reset of the held block <c>0xA52F28..0xA52F58</c>: data 0, <c>u16 +0x6C = u16 +0x6E = 0</c>, eState 0x2B, position -1, <c>+0x80 = -1</c>, <c>+0x84 = 1</c>, <c>+0x7C = 1.0f</c>.</summary>
    private void ResetHeldA52FD4()
    {
        Held60.MarkerCount = 0;                                                 // 0xA69A60..0xA69A64
        Held60.Markers = null;
        Held60.Data = null;                                                     // 0xA52F28 str r8,[r4,#0x60]
        Held60.ValidFrames = 0;                                                 // 0xA52F34
        Held60.MaxFrames = 0;                                                   // 0xA52F38
        Held60.Scratch08 = 0x2B;                                                // 0xA52F50
        Held60.Position = 0xFFFFFFFF;                                           // 0xA52F40
        Held60.Total = 0xFFFFFFFF;                                              // 0xA52F48
        Held60.Rate = 1;                                                        // 0xA52F54
        Held60.Word1C = One;                                                    // 0xA52F58
    }

    /// <summary>The input fully consumed (<c>0xA52F0C..0xA52F64</c>): the source's block is released, IN is freed and reset, and the state's data, marker count and markers are cleared (<c>u16 [state+0xC]</c> and <c>+0xE</c> are kept).</summary>
    private void ReleaseHeldA52F0C(WwiseDecodeState state)
    {
        ReleaseUpstreamA52FBC();                                                // 0xA52F0C..0xA52F18 vt+0xC (the same call site shape)
        ResetHeldA52FD4();                                                      // 0xA52F1C..0xA52F58
        state.Data = null;                                                      // 0xA52F5C
        state.MarkerCount = 0;                                                  // 0xA52F60
        state.Markers = null;                                                   // 0xA52F64
    }

    /// <summary>
    /// The hand-off <c>0xA52ED8</c> (P1-08): <c>0xA4721C(R)</c> (always 0: the realloc path <c>0xA52F8C</c> is dead), OUT's 0x28 bytes are copied to the state (the state and OUT now point at the same samples) and <c>[state+0x28] = r7</c>.
    /// </summary>
    private int HandOffA52ED8(WwiseDecodeState state, int result)
    {
        state.Data = Out.Data;
        state.ChannelConfig = Out.ChannelConfig;
        state.Scratch08 = Out.Scratch08;
        state.MaxFrames = Out.MaxFrames;
        state.ValidFrames = Out.ValidFrames;
        state.MarkerCount = Out.MarkerCount;
        state.Markers = Out.Markers;
        state.Position = Out.Position;
        state.Word1C = Out.Word1C;
        state.Total = Out.Total;
        state.Rate = Out.Rate;
        state.Code28 = result;                                                  // 0xA52F04
        return result;
    }

    // ------------------------------------------------------------------ 0xA5268C

    /// <summary>
    /// <c>0xA5268C(IN, OUT, lo, len)</c> (P1-07, corrections 1): counts the IN markers whose offset word <c>[e+4]</c> satisfies <c>lo &lt;= off &lt; lo + len</c> (unsigned) and, for a non-zero count <c>n</c>, allocates <c>20 * (u16 [OUT+0x10] + n)</c> bytes,
    /// copies the old OUT entries and appends the selected ones as <c>{e[0], 0, e[8], e[0xC], e[0x10]}</c> (the offset becomes 0), frees the old array (<c>0xA69A38(OUT)</c>: count 0) and stores the new pointer with <c>count = n</c> ONLY (a reader sees
    /// <c>array[0..n)</c>: after several calls the old entries come first). An allocation failure frees OUT's markers (<c>0xA527D0..0xA527DC</c>: count 0, array null) and returns.
    /// </summary>
    private void CarryMarkersA5268C(WwiseDecodeState input, WwiseDecodeState output, uint lo, ushort len)
    {
        if (input.Markers is null || input.MarkerCount == 0) return;            // 0xA52698..0xA526A8
        uint hi = unchecked(lo + len);                                          // 0xA526B0 add r7,r2,r3
        int n = 0;
        for (int i = 0; i < input.MarkerCount; i++)                             // 0xA526B8..0xA526DC
        {
            uint off = input.Markers[i].Offset;
            if (off >= lo && off < hi) n = (ushort)(n + 1);
        }
        if (n == 0) return;                                                     // 0xA526E0..0xA526E4
        if (AllocationFails?.Invoke() == true)                                  // 0xA5271C bl 0xA7A7F4; 0xA52724 beq 0xA527D0
        {
            output.MarkerCount = 0;                                             // 0xA527DC b 0xA69A38 (OUT)
            output.Markers = null;
            return;
        }
        int oldCount = output.MarkerCount;                                      // 0xA52700 ldrh r1,[r1,#0x10]
        var array = new WwiseMarkerWindowEntry[oldCount + n];
        if (output.Markers is not null)                                         // 0xA52728..0xA52740 memcpy the old entries
            Array.Copy(output.Markers, array, Math.Min(oldCount, output.Markers.Length));
        int at = oldCount;
        for (int i = 0; i < input.MarkerCount; i++)                             // 0xA52774..0xA527A8
        {
            var e = input.Markers[i];
            if (e.Offset >= lo && e.Offset < hi)
                array[at++] = new WwiseMarkerWindowEntry(e.Pbi, 0, e.Id, e.Position, e.Label);   // 0xA52794 stm lr,{r5,fp}: {e[0], 0}; 0xA5279C {e[8], e[0xC], e[0x10]}
        }
        output.Markers = array;                                                 // 0xA527BC str r8,[r7,#0x14]
        output.MarkerCount = (ushort)n;                                         // 0xA527B4..0xA527C4 (0xA69A38 zeroed the count: count = 0 + n)
    }

    // ------------------------------------------------------------------ 0xA52800

    /// <summary>
    /// The release <c>0xA52800</c> (node <c>vt+0xC</c>, P1-15), reached through the voice chain after the pull (<c>0xA44B90 -> 0xA5495C</c>): with no output data it returns; else the data is freed (<c>0xA69AC8</c>: data null, <c>u16 +0xC = 0</c>) and
    /// <c>u16 [N+0x96] = u16 [N+0x94] = u16 [N+0x98] = 0</c>, <c>[N+0x9C] = 0</c> (the markers are dropped, NOT freed), <c>[R+0x28] = 0</c>, eState 0x2B, position -1, <c>1.0f</c>, <c>+0xA8 = -1</c>, <c>+0xAC = 1</c>.
    /// </summary>
    public void ReleaseBufferA52800()
    {
        if (Out.Data is null) return;                                           // 0xA52800..0xA52808
        Out.Data = null;                                                        // 0xA52818 bl 0xA69AC8; 0xA52830 str r3,[r4,#0x88]
        Out.ValidFrames = 0;                                                    // 0xA52834
        Out.MaxFrames = 0;                                                      // 0xA52838
        Out.MarkerCount = 0;                                                    // 0xA5283C
        Out.Markers = null;                                                     // 0xA52840
        Resampler.OutputOffset28 = 0;                                           // 0xA52844 str r3,[r4,#0x30]
        Out.Scratch08 = 0x2B;                                                   // 0xA52848
        Out.Position = 0xFFFFFFFF;                                              // 0xA5284C
        Out.Word1C = One;                                                       // 0xA52850
        Out.Total = 0xFFFFFFFF;                                                 // 0xA52854
        Out.Rate = 1;                                                           // 0xA52858
    }

    // ------------------------------------------------------------------ 0xA52B90

    /// <summary>
    /// The pending-source arm <c>0xA52B90(node)</c> (P1-11, correction 5): with <c>pbi' = [[voice+0xD8]+0xC]</c> and <c>D = [pbi'+0x1D8] &gt; 0</c> (signed): <c>[pbi'+0x1D8] = (n &lt;= D) ? D - n : 0</c> with
    /// <c>n = trunc_s32(float(u32 (u16 [N+0x94] - u16 [N+0x96])) * [pbi'+0x164] +- 0.5f)</c> and the result 0x11. Otherwise <c>0xA56650(pending, [pbi'+0x1DC], [pbi'+0x1E0])</c>: 0x3F gives 0x11, anything but 1 gives 2; then the format
    /// compatibility gate (<c>byte [pbi+0x15C]</c> equal, the low nibble of <c>byte [pbi+0x15D]</c> equal, bits 12..31 of the word <c>+0x15C</c> equal; a mismatch gives 0x11) and, on a match, <c>0xA549A0(voice)</c>, <c>[N+0xB4] = pbi'</c>,
    /// <c>[N+4] = [voice+0xD4]</c>, the context call on <c>pbi'+0xC</c>, the source's <c>vt+0x20</c> (the pitch), <c>0xA47528(R, F2 = pbi'+0x158, pitch, OUT, [voice+0xEC])</c>, <c>byte [N+0xB8] = 0</c> and the result 0x2D when
    /// <c>u16 [N+0x96] == [N+0x48]</c> else 0x2B.
    /// </summary>
    private int PendingSourceA52B90()
    {
        var voice = Voice!;
        var pending = voice.Pending!;                                           // 0xA52B90..0xA52B9C
        var pbi2 = (pending as IWwisePitchNodeSource)?.Owner ?? throw new WwiseMissingBehaviourException(
            "M6-022 P1-11: the pending source's owner [[voice+0xD8]+0xC] is only available for a pitch-node source");
        int d = unchecked((int)pbi2.StartOffset);                               // 0xA52BA4 ldr r2,[r5,#0x1d8]
        if (d > 0)                                                              // 0xA52BA8..0xA52BAC ble 0xA52C08
        {
            uint space = unchecked((uint)(Out.MaxFrames - Out.ValidFrames));    // 0xA52BB0..0xA52BC4 rsb r3,r1,r3 (u16 [N+0x94] - u16 [N+0x96])
            float x = (float)space * pbi2.Ratio;                                // 0xA52BC8..0xA52BD0 vcvt.f32.u32; vmul.f32
            float half = x <= 0f || float.IsNaN(x) ? -0.5f : 0.5f;             // 0xA52BD8..0xA52BE0
            int n = SaturatingS32(x + half);                                    // 0xA52BE4..0xA52BEC
            pbi2.StartOffset = unchecked((uint)(n <= d ? d - n : 0));           // 0xA52BF0..0xA52BFC rsble r2,r3,r2; rsbgt r2,r2,r2
            return 0x11;                                                        // 0xA52BBC mov r0,#0x11
        }
        int started = WwiseVoiceSourceStart.StartA56650(pending, pbi2.Read1DC(), pbi2.Read1E0(), out _);   // 0xA52C0C..0xA52C18
        if (started == 0x3F) return 0x11;                                       // 0xA52C1C..0xA52C20 beq 0xA52D30
        if (started != 1) return 2;                                             // 0xA52C24..0xA52C2C
        var pbi1 = Pbi!;                                                        // 0xA52C30 ldr r1,[r4,#0xb4]
        if ((byte)pbi1.Word15C != (byte)pbi2.Word15C) return 0x11;              // 0xA52C58..0xA52C60
        if (((pbi1.Word15C >> 8) & 0xF) != ((pbi2.Word15C >> 8) & 0xF)) return 0x11;   // 0xA52C64..0xA52C74
        uint cur = (pbi1.Word15C & 0xFFFFFF00u) | (byte)pbi2.Word15C;           // 0xA52C78..0xA52C7C (the current word with byte 0 replaced by the new one)
        if (((cur ^ pbi2.Word15C) & 0xFFFFF000u) != 0) return 0x11;             // 0xA52C80..0xA52C94 bic #0xff0; bic #0xf: bits 12..31
        (SwitchToNextSrcA549A0 ?? throw new WwiseMissingBehaviourException(
            "M6-022 P1-11: 0xA549A0 (SwitchToNextSrc, called at 0xA52C9C) is only an outline in inventory row 1.7 (it destroys the current source, sets [voice+0xD4] = [voice+0xD8], voice+8 = the new PBI, sends the start notification 0xA56478, sets +0x388 and clears +0x1BE bit 3); supply WwisePitchNodeIntake.SwitchToNextSrcA549A0 (it must set voice.Source = voice.Pending; the verified instructions 0xA549E8..0xA549F8 also store [voice+0xD8] = 0, which the node does after the seam returns)"))(voice);
        voice.Pending = null;                                                   // 0xA549EC mov r5,#0; 0xA549F8 str r5,[r4,#0xd8]
        Pbi = pbi2;                                                             // 0xA52CA4
        Upstream = voice.Source as IWwisePitchNodeSource ?? throw new WwiseMissingBehaviourException("M6-022 P1-11: after the switch [voice+0xD4] is not a pitch-node source");   // 0xA52CAC..0xA52CB0
        if ((pbi2.Flags0E8 & 0x20) == 0)                                        // 0xA52CB4..0xA52CC0 ldrb r3,[r5,#0xe8]; and #0x20; ands; beq 0xA52D38
            (ContextVt24A52D38 ?? throw new WwiseMissingBehaviourException(
                "M6-022 P1-11: 0xA52D38 calls the new PBI's object (pbi+0xC) vt+0x24 (CalcEffectiveParams); correction 5's post-match order omits it; supply WwisePitchNodeIntake.ContextVt24A52D38"))(pbi2);
        else if ((pbi2.Flags0E9 & 1) != 0)                                      // 0xA52CC4..0xA52CC8 ldrb r3,[r5,#0xe9]; tst r3,#1
            (ContextVt28A52CD4 ?? throw new WwiseMissingBehaviourException(
                "M6-022 P1-11: 0xA52CD4..0xA52CDC call the new PBI's object (pbi+0xC) vt+0x28 (Recompute9FF368); correction 5's post-match order omits it; supply WwisePitchNodeIntake.ContextVt28A52CD4"))(pbi2);
        float cents = Upstream.Owner?.Pitch44 ?? throw new WwiseMissingBehaviourException("M6-022 P1-11: the new source has no owner PBI for vt+0x20");   // 0xA52CE4..0xA52CEC vt+0x20 = 0xA5668C
        var f2 = new WwiseResamplerFormat(pbi2.Byte160 | (pbi2.Byte161 << 8), (byte)pbi2.Word15C, unchecked((int)pbi2.SourceFormat158), pbi2.Word15C);   // 0xA52C40..0xA52C54 (F2 = pbi'+0x158: rate, channel word, u16 format)
        Resampler.AllocationFails = AllocationFails;
        Resampler.FormatChangeA47528(f2, cents, voice.MixRateEC);               // 0xA52D0C bl 0xA47528(R, F2, cents, OUT, [voice+0xEC])
        ByteB8 = 0;                                                             // 0xA52D1C
        return Out.ValidFrames == Word48 ? 0x2D : 0x2B;                         // 0xA52D10..0xA52D28
    }
}
