// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The start-position and duration functions every source class shares (research live-bodies-6 V20; C34.3 S1): <c>0xA736D4</c> (the start position in samples for a PBI start offset) and <c>vt+0x34 = 0xA72F5C</c> (the
/// duration in milliseconds). Floats are binary32 with non-fused VFP operations, in the engine's order.
/// </summary>
public static class WwiseSourceStart
{
    private static readonly float Thousand = BitConverter.Int32BitsToSingle(0x447A0000);   // the literals at 0xA72FE8, 0xA73914

    /// <summary>
    /// <c>vt+0x34 = 0xA72F5C(S)</c> (S1): <c>loops = u16[pbi+0x1B8]</c>; 0 returns 0.0f. Otherwise, in single precision, <c>(float)u32[S+0x14] + (float)(loops - 1) * (float)u32([S+0x28] + 1 - [S+0x24])</c> (<c>vmla.f32</c>, not fused),
    /// times <c>1000.0f</c>, divided by <c>(float)u32</c> of <c>vt+0x70(S) = 0xA72F50 = [pbi+0x158]</c>. A rate of 0 divides to infinity or NaN as the engine does.
    /// </summary>
    public static float Duration34A72F5C(IWwiseSourceCommon s)
    {
        ArgumentNullException.ThrowIfNull(s);
        ushort loops = s.Pbi.LoopCount1B8;                                      // 0xA72F5C..0xA72F6C
        if (loops == 0) return 0f;                                              // 0xA72F70..0xA72F80 (the literal 0xA72FE4 is 0)
        uint span = unchecked(s.Word28 + 1 - s.Word24);                         // 0xA72F88..0xA72F9C rsb r1,ip,r1
        float s15 = (float)(loops - 1);                                         // 0xA72F8C sub r2,r2,#1; vcvt.f32.s32
        float s14 = (float)span;                                                // 0xA72FB8 vcvt.f32.u32
        float s16 = (float)s.TotalSamples14;                                    // 0xA72FBC vcvt.f32.u32
        float prod = s15 * s14;                                                 // 0xA72FC0 vmla.f32: the product is rounded, then added
        s16 = s16 + prod;
        s16 = s16 * Thousand;                                                   // 0xA72FC4
        float rate = (float)s.Pbi.SourceFormat158;                              // 0xA72FC8 blx vt+0x70; vcvt.f32.u32
        return s16 / rate;                                                      // 0xA72FD4 vdiv.f32
    }

    /// <summary>
    /// <c>0xA736D4(S)</c> (V20), used only with a PBI start offset. Step 1: with bit 0 of <c>[pbi+0x1BE]</c> clear <c>pos = low32((u64)rate * [pbi+0x1B4] / mixRate)</c>; set, the offset is a binary32 fraction
    /// (<c>[pbi+0x1B4]</c> read as a float): <c>ms = (s16[pbi+0x1B8] != 0) ? vt+0x34(S) : float([S+0x14]) * 1000.0f / float(rate)</c>, <c>pos = (u32)(ms * f * float(rate) / 1000.0f)</c>. Step 2, the loop fold with
    /// <c>loops = u16[pbi+0x1B8]</c>: with <c>loops != 1</c>, <c>L0 &lt; L1</c> and <c>pos &gt; L1</c> the position folds into the loop and <c>u16[S+0x38]</c> is set from the passes; otherwise <c>u16[S+0x38] = loops</c>.
    /// Step 3, with bit 1 of <c>[pbi+0x1BE]</c>: the nearest marker (<c>0x9D4DD0</c>) replaces the position and folds again with the current <c>u16[S+0x38]</c>.
    /// </summary>
    public static uint StartPositionA736D4(IWwiseSourceCommon s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var pbi = s.Pbi;
        uint pos;
        bool snap = (pbi.Flags1BE & 2) != 0;                                    // 0xA73748 / 0xA73874 ubfx r6,r6,#1,#1
        uint rate = pbi.SourceFormat158;                                        // vt+0x70 = 0xA72F50
        if ((pbi.Flags1BE & 1) != 0)                                            // 0xA736E4..0xA736EC
        {
            float ms;
            if (unchecked((short)pbi.LoopCount1B8) != 0)                        // 0xA736F4..0xA736FC ldrsh
                ms = Duration34A72F5C(s);                                       // 0xA737B8..0xA737C8 vt+0x34
            else
                ms = (float)s.TotalSamples14 * Thousand / (float)rate;          // 0xA73700..0xA73724
            float f = BitConverter.UInt32BitsToSingle(pbi.Word1B4);             // 0xA7373C vldr s14,[r4,#0x1b4]: the word read as a float
            float v = ms * f;                                                   // 0xA7374C
            v = v * (float)rate;                                                // 0xA73758
            v = v / Thousand;                                                   // 0xA7375C
            pos = WwiseAutoStream.CvtU32(v);                                    // 0xA73760 vcvt.u32.f32
        }
        else
        {
            ulong q = (ulong)rate * pbi.Word1B4 / WwiseRuntimeSettings.MixRateHz;   // 0xA7387C umull; 0xA73880 __aeabi_uldivmod by [0x105243C]
            pos = unchecked((uint)q);                                           // 0xA73884 mov r4,r0
        }
        uint l0 = s.Word24, l1 = s.Word28;                                      // 0xA7376C, 0xA73774
        ushort loops = pbi.LoopCount1B8;                                        // 0xA73778
        if (!FoldLoop(ref pos, loops, l0, l1, s))
            s.LoopCount38 = loops;                                              // 0xA7379C strheq sb,[r5,#0x38]
        if (!snap) return pos;                                                  // 0xA737A4..0xA737B4
        int idx = s.Container2C.Nearest9D4DD0(pos);                             // 0xA737CC..0xA737D4
        if (idx < 0) return pos;                                                // 0xA737D8..0xA737DC
        pos = s.Container2C.Cues[idx].Position;                                 // 0xA737E4 ldr r4,[r0,#4]
        FoldLoop(ref pos, s.LoopCount38, l0, l1, s);                            // 0xA737E8..0xA73910
        return pos;
    }

    /// <summary>
    /// The fold shared by steps 2 and 3 (<c>0xA7388C..0xA738EC</c>, <c>0xA73814..0xA73910</c>): applies when <paramref name="loops"/> != 1, <paramref name="l0"/> &lt; <paramref name="l1"/> and <c>pos &gt; l1</c> (unsigned);
    /// <c>len = l1 + 1 - l0</c>, <c>sl = pos - l0</c>, <c>passes = sl / len</c>; <c>passes &lt; loops</c>: <c>u16[S+0x38] = loops - passes</c> (loops != 0), <c>pos = l0 + sl % len</c>; otherwise <c>loops == 0</c>:
    /// <c>u16[S+0x38] = 0</c>, <c>pos = l0 + sl % len</c>; else <c>u16[S+0x38] = 1</c>, <c>pos = pos - len * (loops - 1)</c>.
    /// </summary>
    private static bool FoldLoop(ref uint pos, ushort loops, uint l0, uint l1, IWwiseSourceCommon s)
    {
        if (!(loops != 1 && l0 < l1 && pos > l1)) return false;                 // 0xA7376C..0xA73798
        uint len = unchecked(l1 + 1 - l0);                                      // 0xA7388C..0xA73894
        uint sl = unchecked(pos - l0);                                          // 0xA73890
        uint passes = len == 0 ? 0 : sl / len;                                  // 0xA738A0 __aeabi_uidiv
        if (passes < loops)
        {
            s.LoopCount38 = loops != 0 ? unchecked((ushort)(loops - passes)) : loops;   // 0xA738CC..0xA738D4, 0xA738B4
            pos = unchecked((len == 0 ? 0 : sl % len) + l0);                    // 0xA738B8..0xA738C4
        }
        else if (loops == 0)
        {
            s.LoopCount38 = 0;                                                  // 0xA738B4
            pos = unchecked((len == 0 ? 0 : sl % len) + l0);                    // 0xA738B8..0xA738C4
        }
        else
        {
            s.LoopCount38 = 1;                                                  // 0xA738F4
            pos = unchecked(pos - len * (uint)(loops - 1));                     // 0xA738F8 mls r4,r7,sb,r4
        }
        return true;
    }
}
