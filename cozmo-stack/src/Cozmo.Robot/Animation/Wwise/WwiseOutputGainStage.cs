// fidelity: M6-013
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The output-gain stage that closes the Parametric EQ's Execute (<c>0xAA2B38..0xAA2C70</c>, NEON <c>0xAA2D20..0xAA2D98</c> and <c>0xAA2D9C..0xAA2DE0</c>) and the Peak Limiter's Execute (<c>0xAA1C88..0xAA1DFC</c>, NEON <c>0xAA1EA0..0xAA1F60</c>): the same code in both plug-ins.
/// <para><b>Channels.</b> <c>ch = byte [S+4]</c>, minus 1 when the ProcessLFE byte of the parameter object is 0 and <c>u32 [S+4] &amp; 0x8000</c>. The planes are at <c>u16 [S+0xC]</c> frames apart, <c>n = u16 [S+0xE]</c> frames valid.</para>
/// <para><b>Equal gains</b> (<c>new == old</c>; a NaN is never equal): nothing when <c>ch == 0</c> or <c>old == 1.0f</c>, else every frame of every channel is multiplied by <c>old</c>: the first <c>4 * (n / 4)</c> frames by NEON (<c>vmul.f32 q8, q8, q9</c>, flush-to-zero), the rest by scalar VFP.</para>
/// <para><b>Unequal gains</b>, per channel: <c>lr = n / 4</c>; if <c>lr != 0</c> the first <c>A = 4 * lr</c> frames use the NEON lane gains <c>{old, old + d, d + (old + d), d + (d + (old + d))}</c> with <c>d = (new - old) / (float)A</c>, each block of four multiplied (<c>vmul.f32 q8, q8, q9</c>) and then the lanes advanced
/// (<c>vadd.f32 q9, q9, q10</c>, <c>q10 = d * 4.0f</c> in every lane), both flush-to-zero; then, for the frames not covered (all of them when <c>n &lt; 4</c>), the scalar tail <b>restarts</b> at <c>g = old</c> with the step <c>d2 = (new - old) / (float)n</c>: <c>x *= g; g += d2</c>.</para>
/// <para>The NEON operations flush denormal operands and results to zero by the ISA (the Advanced SIMD "standard FPSCR value"); the scalar VFP operations follow the phone's FPSCR (HARDWARE_ONLY; IEEE here).</para>
/// </summary>
internal static class WwiseOutputGainStage
{
    /// <summary>A NEON operand or result: a denormal becomes a zero of the same sign.</summary>
    internal static float Z(float x) => float.IsSubnormal(x) ? (BitConverter.SingleToUInt32Bits(x) >> 31 != 0 ? -0f : 0f) : x;

    /// <summary>The plane data of a state, with the access the engine would make checked against the array.</summary>
    internal static float[] DataOf(WwiseDecodeState s, uint planes, int stride, int n)
    {
        if (s.Data is not float[] data) throw new InvalidOperationException("[S] must be the planar float buffer (the engine dereferences it)");
        if (planes != 0 && (long)(planes - 1) * stride + n > data.Length)
            throw new InvalidOperationException("the engine would read past the audio buffer: channels * stride + frames exceed it");
        return data;
    }

    /// <summary>The channel count of the stage: <c>byte [S+4]</c> minus 1 for the LFE flag when the plug-in does not process it.</summary>
    internal static uint Channels(WwiseDecodeState s, bool processLfe)
    {
        uint cfg = s.ChannelConfig;
        uint ch = cfg & 0xFF;
        if (!processLfe && (cfg & 0x8000) != 0) ch--;
        if (ch == uint.MaxValue)
            throw new InvalidOperationException("byte [S+4] is 0 with the 0x8000 flag: the engine's channel count wraps to 0xFFFFFFFF and the loop runs over unmapped memory");
        return ch;
    }

    /// <summary>Applies the stage to <paramref name="s"/>; the caller stores the new gain as its previous gain afterwards.</summary>
    internal static void Apply(WwiseDecodeState s, bool processLfe, float oldGain, float newGain)
    {
        uint ch = Channels(s, processLfe);
        int n = s.ValidFrames;                                                  // ldrh r3,[r4,#0xe]
        int stride = s.MaxFrames;                                               // ldrh r2,[r4,#0xc]
        float[] data = DataOf(s, ch, stride, n);

        if (oldGain == newGain)                                                 // vcmp.f32 s16,s12; bne
        {
            if (ch != 0 && oldGain != 1f)                                       // cmp r0,#0; vcmp.f32 s12,#1.0
            {
                int lr = n >> 2;
                for (uint c = 0; c < ch; c++)
                {
                    int b = (int)c * stride;
                    int i = 0;
                    for (; i < 4 * lr; i++) data[b + i] = Z(Z(data[b + i]) * Z(oldGain));      // vmul.f32 q8, q8, q9 (NEON)
                    for (; i < n; i++) data[b + i] = data[b + i] * oldGain;                    // vmul.f32 s15, s15, s14 (VFP)
                }
            }
            return;
        }

        if (ch == 0) return;                                                    // beq: only the caller's store
        for (uint c = 0; c < ch; c++)
        {
            int b = (int)c * stride;
            int lr = n >> 2;
            int i = 0;
            if (lr != 0)                                                        // cmp lr,#0; bne (NEON)
            {
                float d = (newGain - oldGain) / (float)(4 * lr);                // vsub.f32; vcvt.f32.s32 s14,s9; vdiv.f32
                float l0 = oldGain;
                float l1 = oldGain + d;
                float l2 = d + l1;
                float l3 = d + l2;
                float step = d * 4f;                                            // vmul.f32 s15, s15, s8 (4.0)
                Span<float> lane = stackalloc float[4] { l0, l1, l2, l3 };
                for (int blk = 0; blk < lr; blk++)
                {
                    for (int k = 0; k < 4; k++, i++) data[b + i] = Z(Z(data[b + i]) * Z(lane[k]));       // vmul.f32 q8, q8, q9
                    for (int k = 0; k < 4; k++) lane[k] = Z(Z(lane[k]) + Z(step));                     // vadd.f32 q9, q9, q10
                }
            }
            if (i < n)                                                          // cmp sb,r3; bls
            {
                float d2 = (newGain - oldGain) / (float)(uint)n;                // vsub.f32 s13,s16,s12; vcvt.f32.u32 s11,s10; vdiv.f32
                float g = oldGain;                                              // vmov.f32 s14, s12: the tail restarts at the old gain
                for (; i < n; i++)
                {
                    data[b + i] = data[b + i] * g;                              // vmul.f32 s15, s15, s14
                    g = g + d2;                                                 // vadd.f32 s14, s14, s13
                }
            }
        }
    }
}
