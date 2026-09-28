// fidelity: M6-002
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The exact float NEON inverse MDCT of the Wwise Vorbis decoder (M6-002, correction C9; normative
/// extraction <c>re-analysis/research/20260927-X5-imdct-extraction.md</c>, rows X5-I1..X5-I10 and
/// Appendices A..F).
///
/// This is a phase-by-phase transliteration of the five native instruction ranges, in their native
/// order, into scalar binary32 C#. Each NEON q-register is four binary32 lanes; each d-register is two
/// lanes. Every listed operation is performed in the listed order and rounds to binary32 after that
/// operation (C# <c>float</c> arithmetic rounds per operation and does not contract to FMA), so no
/// algebraic reassociation is done. <c>vrev64.32</c>, <c>vswp</c>, <c>vtrn.32</c> and <c>vld4.32</c>
/// keep their ARM lane semantics (X5 report, lines 22..30).
///
/// <b>Work buffer (X5-I2, Q1).</b> The native entry checks the pointer word at BSS <c>0x0108E648</c>
/// and uses it as the stage/tail source. No writer of that word was located; native ownership and
/// aliasing are <c>RECOVERABLE_GAP</c> (C9). This implementation therefore uses an explicit internal
/// work buffer, as C9 permits, and does <b>not</b> claim native ownership was recovered: after
/// pre-symmetry and the large butterfly the transformed input is copied into the work buffer, the
/// stage network and terminal butterfly run there, and the tail writes back into <paramref name="inout"/>.
/// </summary>
public static partial class WwiseVorbisNative
{
    // ---- NEON lane emulation (X5 report lines 22..30) ----

    // d[] holds all 32 d-registers: d[2*N] and d[2*N+1] are the two lanes of dN; qN is d[4*N..4*N+3].

    private static void VMul(float[] d, int a, int b)
    {
        for (int i = 0; i < 4; i++) d[4 * a + i] = d[4 * a + i] * d[4 * b + i];
    }

    private static void VAdd(float[] d, int a, int b)
    {
        for (int i = 0; i < 4; i++) d[4 * a + i] = d[4 * a + i] + d[4 * b + i];
    }

    private static void VSub(float[] d, int a, int b)
    {
        for (int i = 0; i < 4; i++) d[4 * a + i] = d[4 * a + i] - d[4 * b + i];
    }

    private static void VNeg(float[] d, int a)
    {
        for (int i = 0; i < 4; i++) d[4 * a + i] = -d[4 * a + i];
    }

    private static void VMul(float[] d, int dest, int a, int b)
    {
        for (int i = 0; i < 4; i++) d[4 * dest + i] = d[4 * a + i] * d[4 * b + i];
    }

    private static void VAdd(float[] d, int dest, int a, int b)
    {
        for (int i = 0; i < 4; i++) d[4 * dest + i] = d[4 * a + i] + d[4 * b + i];
    }

    private static void VSub(float[] d, int dest, int a, int b)
    {
        for (int i = 0; i < 4; i++) d[4 * dest + i] = d[4 * a + i] - d[4 * b + i];
    }

    /// <summary>vrev64.32 qa,qb: reverse the two 32-bit lanes within each 64-bit doubleword.</summary>
    private static void VRev64(float[] d, int qa, int qb)
    {
        for (int h = 0; h < 2; h++)
        {
            int a = 4 * qa + 2 * h, b = 4 * qb + 2 * h;
            (d[a], d[a + 1]) = (d[b + 1], d[b]);
        }
    }

    /// <summary>vswp dA,dB: swap the two 64-bit halves.</summary>
    private static void VSwp(float[] d, int da, int db)
    {
        for (int i = 0; i < 2; i++) (d[2 * da + i], d[2 * db + i]) = (d[2 * db + i], d[2 * da + i]);
    }

    /// <summary>vtrn.32 qa,qb: {a0,a1,a2,a3},{b0,b1,b2,b3} -&gt; {a0,b0,a2,b2},{a1,b1,a3,b3}.</summary>
    private static void VTrn(float[] d, int qa, int qb)
    {
        for (int h = 0; h < 2; h++)
        {
            int a = 4 * qa + 2 * h, b = 4 * qb + 2 * h;
            float a0 = d[a], a1 = d[a + 1], b0 = d[b], b1 = d[b + 1];
            d[a] = a0; d[a + 1] = b0; d[b] = a1; d[b + 1] = b1;
        }
    }

    private static void VCopy(float[] d, int qa, int qb)
    {
        for (int i = 0; i < 4; i++) d[4 * qa + i] = d[4 * qb + i];
    }

    private static void VSet(float[] d, int qa, float value)
    {
        for (int i = 0; i < 4; i++) d[4 * qa + i] = value;
    }

    /// <summary>vdup.32 qa, db[lane]: broadcast one d-register lane to all four q lanes.</summary>
    private static void VDup(float[] d, int qa, int db, int lane)
    {
        float v = d[2 * db + lane];
        for (int i = 0; i < 4; i++) d[4 * qa + i] = v;
    }

    /// <summary>vld1.32 {dA,dB},[mem+idx]: load four floats (dA then dB).</summary>
    private static void VLoad4(float[] d, int da, int db, float[] mem, int idx)
    {
        d[2 * da] = mem[idx]; d[2 * da + 1] = mem[idx + 1];
        d[2 * db] = mem[idx + 2]; d[2 * db + 1] = mem[idx + 3];
    }

    /// <summary>vst1.32 {dA,dB},[mem+idx]: store four floats.</summary>
    private static void VStore4(float[] d, int da, int db, float[] mem, int idx)
    {
        mem[idx] = d[2 * da]; mem[idx + 1] = d[2 * da + 1];
        mem[idx + 2] = d[2 * db]; mem[idx + 3] = d[2 * db + 1];
    }

    /// <summary>vld1.64 {dA,dB},[sp+off] / vldr dA,[sp+off]: load four sp floats.</summary>
    private static void VLoadSp4(float[] d, int da, int db, float[] sp, int floatOff)
    {
        d[2 * da] = sp[floatOff]; d[2 * da + 1] = sp[floatOff + 1];
        d[2 * db] = sp[floatOff + 2]; d[2 * db + 1] = sp[floatOff + 3];
    }

    private static void VStoreSp4(float[] d, int da, int db, float[] sp, int floatOff)
    {
        sp[floatOff] = d[2 * da]; sp[floatOff + 1] = d[2 * da + 1];
        sp[floatOff + 2] = d[2 * db]; sp[floatOff + 3] = d[2 * db + 1];
    }

    /// <summary>
    /// vld4.32 {dA,dB,dC,dD},[mem+idx]: de-interleave two consecutive four-float structures.
    /// Structure 0 = mem[idx..idx+3], structure 1 = mem[idx+4..idx+7]; dA = element 0 of both,
    /// dB = element 1, dC = element 2, dD = element 3 (ARM VLD4 lane semantics).
    /// </summary>
    private static void VLoad4Structures(float[] d, int da, int db, int dc, int dd, float[] mem, int idx)
    {
        d[2 * da] = mem[idx]; d[2 * da + 1] = mem[idx + 4];
        d[2 * db] = mem[idx + 1]; d[2 * db + 1] = mem[idx + 5];
        d[2 * dc] = mem[idx + 2]; d[2 * dc + 1] = mem[idx + 6];
        d[2 * dd] = mem[idx + 3]; d[2 * dd + 1] = mem[idx + 7];
    }

    /// <summary>The MDCT control-flow shift (X5-I3, 0x00AB4E78..0x00AB4E94): the lowest set bit of
    /// <paramref name="n"/> at or above bit 5, then <c>shift = 13 - bit</c>.</summary>
    public static int ImdctLowestSetBit5(int n)
    {
        int bit = 5;
        while ((n & (1 << bit)) == 0) bit++;
        return bit;
    }

    /// <summary>
    /// <c>mdct_backward(n, inout)</c> (X5-I1, entry 0x00AB4E34). Two-argument, in place on
    /// <paramref name="inout"/>; the work array is an explicit internal buffer (X5-I2 / C9). Native
    /// ownership of the BSS work pointer is <b>not</b> claimed: the buffer is built here, the butterfly
    /// writes the input region, the input is copied into the work region, the stage network and terminal
    /// butterfly run there, and the tail writes the input region back to <paramref name="inout"/>.
    ///
    /// A single byte-addressed <c>mem</c> array holds the input (floats 0..n), the work copy
    /// (floats n..2n), the vendored trig region, the 13-slot GOT view table, the two stack frames and the
    /// work-pointer slot. The generated phase cores use byte addresses exactly as the native listing.
    /// </summary>
    public static void ImdctBackward(Span<float> inout, int n)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));
        int shift = 13 - ImdctLowestSetBit5(n);                            // X5-I3: rsb r3,r5,#0xd
        int n2 = n / 2;                                                    // X5-I3: asr r2,r7,#1
        int log2n = ImdctLowestSetBit5(n);

        int workBase = n;                                                  // work copy after the butterfly
        int gotBase = ImdctTrigBase + ImdctTrigWords.Length;               // 13-slot GOT view table
        int spButterfly = gotBase + 16;                                    // 0x194-byte frame (101 floats)
        int spStages = spButterfly + 128;                                  // 0x94-byte frame (37 floats)
        int workSlot = spStages + 64;                                      // the work-pointer word
        var mem = new float[workSlot + 4];
        inout.CopyTo(mem.AsSpan(0, n));

        for (int i = 0; i < ImdctTrigWords.Length; i++)
            mem[ImdctTrigBase + i] = BitConverter.UInt32BitsToSingle(ImdctTrigWords[i]);
        FillImdctGot(mem, ImdctTrigBase, gotBase);
        SV(mem, workSlot * 4, workBase * 4);                               // *0x0108E648 model

        ImdctPreSymmetry(mem, n2, shift);                                  // X5-I4: 0x00AB3D28
        ImdctButterfliesCore(mem, n2, shift, gotBase * 4, log2n, spButterfly * 4, workSlot * 4); // X5-I5
        Array.Copy(mem, 0, mem, workBase, n);                              // in -> work
        ImdctStagesTerminalCore(mem, n2, shift, gotBase * 4, log2n, spStages * 4, workSlot * 4); // X5-I6/I7
        ImdctTail(mem, workBase, n, shift, 0);                             // X5-I8: 0x00AB39D8
        mem.AsSpan(0, n).CopyTo(inout);
    }

    // ---- X5-I4 / Appendix A: pre-symmetry, 0x00AB3D28..0x00AB3FB0 ----

    /// <summary>
    /// Transliteration of <c>presymmetry(in, n2, shift)</c> (X5-I4, Appendix A). It uses the five trig
    /// views 296, 512, 440, 368 and 256 at byte offsets <c>(shift+1)*16</c> and <c>shift*16</c>, and a
    /// two-ended 16-float walk with <c>vld4.32</c> de-interleaving.
    /// </summary>
    private static void ImdctPreSymmetry(float[] mem, int n2, int shift)
    {
        if (n2 <= 16) return;                                              // 0x00AB3DC4: bhs when in >= end
        var d = new float[64];
        var sp = new float[28];

        // Fixed trig vectors computed once (0x00AB3D58..0x00AB3DC0).
        VLoad4(d, 16, 17, TrigView(8, 4 * (shift + 1)), 0);                    // q8 = view256[shift+1] (0x3d40/3d58/3d68/3d78)
        VCopy(d, 10, 8);                                                   // q10 = q8
        VLoad4(d, 22, 23, TrigView(12, 4 * shift), 0);                        // q11 = view512[shift]
        VMul(d, 8, 11);                                                    // q8 = q8 * q11
        VLoad4(d, 18, 19, TrigView(10, 4 * shift), 0);                        // q9 = view368[shift] (0x3d50/3d6c/3d80/3da8)
        VMul(d, 13, 10);                                                   // q13 = q10 * q9
        VLoad4(d, 28, 29, TrigView(11, 4 * shift), 0);                        // q14 = view440[shift] (0x3d70/3d8c/3da4/3db4)
        VSub(d, 15, 8, 14);                                                // q15 = q8 - q14
        VLoad4(d, 28, 29, TrigView(9, 4 * shift), 0);                        // q14 = view296[shift] (0x3d84/3da0/3db0/3dbc)
        VSub(d, 14, 13, 14);                                               // q14 = q13 - q14
        VStoreSp4(d, 16, 17, sp, 24);                                      // sp[0x60] = q8 (saved)

        int p = 0, q = n2 - 16;                                            // r0 = in, r1 = in + n2 - 16
        while (p < q)
        {
            int pOld = p, qOld = q;
            VLoadSp4(d, 16, 17, sp, 24);                                   // 0x3dc8: q8 = sp[0x60]
            int ip = p + 8;                                                // 0x3dd0
            VMul(d, 1, 8, 15);                                             // 0x3dd4: q1 = q8 * q15
            int r2 = q + 8;                                                // 0x3dd8
            float sl = d[2 * 30 + 1];                                      // 0x3ddc: sl = d30[1] = q15[1]
            int r7 = p + 4;                                                // 0x3de0
            VMul(d, 8, 8, 14);                                             // 0x3de4: q8 = q8 * q14
            int r6 = p + 12;                                               // 0x3de8
            float sb = d[2 * 28 + 1];                                      // 0x3dec: sb = d28[1] = q14[1]
            int r5 = q + 4, r4 = q + 8, lr = q + 12;                       // 0x3df0..0x3df8
            VLoad4Structures(d, 6, 8, 10, 12, mem, p);                     // 0x3dfc
            VSub(d, 10, 1, 11);                                            // 0x3e00: q10 = q1 - q11
            VSub(d, 8, 8, 9);                                              // 0x3e04: q8 = q8 - q9
            VCopy(d, 7, 6);                                                // 0x3e08
            VCopy(d, 6, 5);                                                // 0x3e0c
            VCopy(d, 5, 4);                                                // 0x3e10
            VCopy(d, 4, 3);                                                // 0x3e14
            VLoad4Structures(d, 0, 2, 4, 6, mem, q);                       // 0x3e18
            q -= 16;                                                       // 0x3e1c
            VCopy(d, 12, 10);                                              // 0x3e20
            VCopy(d, 13, 8);                                               // 0x3e24
            VCopy(d, 9, 5);                                                // 0x3e28
            VLoad4Structures(d, 1, 3, 5, 7, mem, r2);                      // 0x3e2c
            VCopy(d, 8, 4);                                                // 0x3e30
            VCopy(d, 10, 6);                                               // 0x3e34
            float r8v = d[4 * 12];                                         // 0x3e38: r8 = d24[0] = q12[0]
            VCopy(d, 11, 7);                                               // 0x3e3c
            float r2v = d[4 * 13];                                         // 0x3e40: r2 = d26[0] = q13[0]
            VLoad4Structures(d, 17, 19, 21, 23, mem, ip);                  // 0x3e44
            VStoreSp4(d, 24, 25, sp, 16);                                  // 0x3e48: sp[0x40] = q12
            for (int i = 0; i < 8; i++) sp[i] = d[16 + i];                 // 0x3e50: vstmia sp,{d16..d23}
            VCopy(d, 8, 15);                                               // 0x3e54: q8 = q15
            VCopy(d, 9, 14);                                               // 0x3e58: q9 = q14
            VLoadSp4(d, 24, 25, sp, 0);                                   // 0x3e5c: q12 = sp[0x40]
            d[4 * 9] = sb;                                                 // 0x3e60: d18[0] = sb
            d[4 * 8] = sl;                                                 // 0x3e64: d16[0] = sl
            VCopy(d, 10, 9);                                               // 0x3e68: q10 = q9
            sb = d[4 * 9 + 2];                                             // 0x3e6c: sb = d19[0] = q9[2]
            sl = d[4 * 8 + 2];                                             // 0x3e70: sl = d17[0] = q8[2]
            VLoadSp4(d, 14, 15, sp, 8);                                    // 0x3e74: q7 = sp[0x20]
            VMul(d, 6, 7, 14);                                             // 0x3e7c: q6 = q7 * q14
            VStoreSp4(d, 26, 27, sp, 20);                                  // 0x3e80: sp[0x50] = q13
            VMul(d, 13, 12, 15);                                           // 0x3e88: q13 = q12 * q15
            VRev64(d, 9, 3);                                               // 0x3e8c: q9 = vrev64(q3)
            VRev64(d, 5, 1);                                               // 0x3e90: q5 = vrev64(q1)
            VMul(d, 7, 7, 15);                                             // 0x3e94: q7 = q7 * q15
            VRev64(d, 4, 2);                                               // 0x3e98: q4 = vrev64(q2)
            VMul(d, 12, 12, 14);                                           // 0x3e9c: q12 = q12 * q14
            VRev64(d, 11, 0);                                              // 0x3ea0: q11 = vrev64(q0)
            VSwp(d, 8, 9);                                                 // 0x3ea4: vswp d8,d9
            VSub(d, 3, 6, 13);                                             // 0x3ea8: q3 = q6 - q13
            VSwp(d, 22, 23);                                               // 0x3eac: vswp d22,d23
            VLoadSp4(d, 0, 1, sp, 4);                                      // 0x3eb0: q0 = sp[0x10]
            d[2 * 4] = d[2 * 11]; d[2 * 4 + 1] = d[2 * 11 + 1];            // 0x3eb8: d4 = d11
            d[2 * 5] = d[2 * 10]; d[2 * 5 + 1] = d[2 * 10 + 1];            // 0x3ebc: d5 = d10
            VLoadSp4(d, 2, 3, sp, 12);                                     // 0x3ec0: q1 = sp[0x30]
            VAdd(d, 12, 12, 7);                                            // 0x3ec8: q12 = q12 + q7
            VSwp(d, 18, 19);                                               // 0x3ecc: vswp d18,d19
            VRev64(d, 3, 3);                                               // 0x3ed0: q3 = vrev64(q3)
            VMul(d, 13, 2, 15);                                            // 0x3ed4: q13 = q2 * q15
            d[4 * 10 + 1] = sb;                                            // 0x3ed8: d20[1] = sb
            VMul(d, 2, 2, 14);                                             // 0x3edc: q2 = q2 * q14
            d[4 * 8 + 1] = sl;                                             // 0x3ee0: d16[1] = sl
            VMul(d, 7, 9, 14);                                             // 0x3ee4: q7 = q9 * q14
            sl = d[4 * 8 + 3];                                             // 0x3ee8: sl = d17[1] = q8[3]
            VMul(d, 9, 9, 15);                                             // 0x3eec: q9 = q9 * q15
            sb = d[4 * 10 + 3];                                            // 0x3ef0: sb = d21[1] = q10[3]
            VAdd(d, 13, 13, 7);                                            // 0x3ef4: q13 = q13 + q7
            VSwp(d, 6, 7);                                                 // 0x3ef8: vswp d6,d7
            VSub(d, 9, 9, 2);                                              // 0x3efc: q9 = q9 - q2
            VRev64(d, 12, 12);                                             // 0x3f00: q12 = vrev64(q12)
            VStoreSp4(d, 6, 7, sp, 0);                                    // 0x3f04: sp[0x40] = q3
            VSwp(d, 24, 25);                                               // 0x3f08: vswp d24,d25
            VRev64(d, 13, 13);                                             // 0x3f0c: q13 = vrev64(q13)
            VRev64(d, 2, 9);                                               // 0x3f10: q2 = vrev64(q9)
            VCopy(d, 9, 14);                                               // 0x3f14: q9 = q14
            VSwp(d, 26, 27);                                               // 0x3f18: vswp d26,d27
            VSwp(d, 4, 5);                                                 // 0x3f1c: vswp d4,d5
            VLoadSp4(d, 28, 29, sp, 20);                                   // 0x3f20: q14 = sp[0x50]
            d[4 * 8 + 2] = sl;                                             // 0x3f28: d17[0] = sl
            d[4 * 10 + 2] = sb;                                            // 0x3f2c: d21[0] = sb
            d[4 * 8 + 3] = r8v;                                            // 0x3f30: d17[1] = r8
            VMul(d, 6, 4, 8);                                              // 0x3f34: q6 = q4 * q8
            d[4 * 10 + 3] = r2v;                                           // 0x3f38: d21[1] = r2
            VMul(d, 3, 4, 10);                                             // 0x3f3c: q3 = q4 * q10
            VMul(d, 5, 0, 10);                                             // 0x3f40: q5 = q0 * q10
            VMul(d, 4, 11, 10);                                            // 0x3f44: q4 = q11 * q10
            VMul(d, 0, 0, 8);                                              // 0x3f48: q0 = q0 * q8
            VMul(d, 11, 11, 8);                                            // 0x3f4c: q11 = q11 * q8
            VMul(d, 8, 1, 8);                                              // 0x3f50: q8 = q1 * q8
            VMul(d, 10, 1, 10);                                            // 0x3f54: q10 = q1 * q10
            VAdd(d, 3, 11, 3);                                             // 0x3f58: q3 = q11 + q3
            VSub(d, 4, 6, 4);                                              // 0x3f5c: q4 = q6 - q4
            VAdd(d, 8, 5, 8);                                              // 0x3f60: q8 = q5 + q8
            VSub(d, 0, 10, 0);                                             // 0x3f64: q0 = q10 - q0
            VStore4(d, 8, 9, mem, pOld);                                   // 0x3f68: store q4
            VCopy(d, 11, 15);                                              // 0x3f70: q11 = q15
            VStore4(d, 16, 17, mem, r7);                                   // 0x3f74: store q8
            VStore4(d, 6, 7, mem, ip);                                     // 0x3f78: store q3
            VLoadSp4(d, 6, 7, sp, 0);                                     // 0x3f7c: q3 = sp[0x40]
            VStore4(d, 0, 1, mem, r6);                                     // 0x3f80: store q0
            VStore4(d, 6, 7, mem, qOld);                                   // 0x3f84: store q3
            VStore4(d, 26, 27, mem, r5);                                   // 0x3f90: store q13
            VLoadSp4(d, 30, 31, sp, 16);                                   // 0x3f94: q15 = sp[0x40]
            VStore4(d, 24, 25, mem, r4);                                   // 0x3f9c: store q12
            VStore4(d, 4, 5, mem, lr);                                     // 0x3fa0: store q2
            p = pOld + 16;
        }
    }

    /// <summary>A float copy of one of the 13 GOT trig views at a byte offset of <c>4*extraFloats</c>.</summary>
    private static float[] TrigView(int view, int extraFloats)
    {
        var v = new float[4];
        for (int i = 0; i < 4; i++) v[i] = ImdctTrig(view, extraFloats + i);
        return v;
    }

    // ---- X5-I5..I7 byte-addressed helpers and the generated phase cores ----

    /// <summary>The float index where <see cref="ImdctBackward"/> places the vendored trig region in
    /// <c>mem</c>. It is above the largest input (8192 floats) and its work copy.</summary>
    private const int ImdctTrigBase = 16384;

    /// <summary>Integer load at a byte address in <c>mem</c> (raw bits).</summary>
    private static int IV(float[] mem, int addr) => BitConverter.SingleToInt32Bits(mem[addr >> 2]);

    /// <summary>Integer store at a byte address in <c>mem</c> (raw bits).</summary>
    private static void SV(float[] mem, int addr, int v) => mem[addr >> 2] = BitConverter.Int32BitsToSingle(v);

    private static void VLoad4B(float[] d, int da, int db, float[] mem, int addr)
    {
        int i = addr >> 2;
        d[2 * da] = mem[i]; d[2 * da + 1] = mem[i + 1];
        d[2 * db] = mem[i + 2]; d[2 * db + 1] = mem[i + 3];
    }

    private static void VStore4B(float[] d, int da, int db, float[] mem, int addr)
    {
        int i = addr >> 2;
        mem[i] = d[2 * da]; mem[i + 1] = d[2 * da + 1];
        mem[i + 2] = d[2 * db]; mem[i + 3] = d[2 * db + 1];
    }

    private static void VLoadDB(float[] d, int dn, float[] mem, int addr)
    {
        int i = addr >> 2;
        d[2 * dn] = mem[i]; d[2 * dn + 1] = mem[i + 1];
    }

    private static void VStoreDB(float[] d, int dn, float[] mem, int addr)
    {
        int i = addr >> 2;
        mem[i] = d[2 * dn]; mem[i + 1] = d[2 * dn + 1];
    }

    private static void VBroadcast(float[] d, int qa, float[] mem, int addr)
    {
        float v = mem[addr >> 2];
        for (int i = 0; i < 4; i++) d[4 * qa + i] = v;
    }

    private static void VCopyD(float[] d, int da, int db)
    {
        d[2 * da] = d[2 * db]; d[2 * da + 1] = d[2 * db + 1];
    }

    /// <summary>
    /// The 13 GOT slots the butterfly and stage network read (GOT base + 0x5C + raw offset, X5-I9).
    /// Each holds the byte address of its trig view in <c>mem</c>.
    /// </summary>
    private static void FillImdctGot(float[] mem, int trigBase, int gotBase)
    {
        (int Raw, int View)[] entries =
        {
            (-0x58, 256), (-0x44, 512), (-0x40, 368), (-0x3c, 440),
            (-0x38, 296), (-0x34, 108), (-0x30, 72), (-0x2c, 36), (-0x28, 0),
        };
        foreach (var (raw, view) in entries)
            mem[gotBase + (0x5C + raw) / 4] = BitConverter.Int32BitsToSingle((trigBase + view) * 4);
    }

    /// <summary>
    /// X5-I8 / Appendix D: the bit-reversed rotation and tail, 0x00AB39D8..0x00AB3CF0. It gathers
    /// float4s from the work buffer using <c>bitrev9[i]&gt;&gt;(shift-1)</c> and
    /// <c>bitrev9[n/16-i]&gt;&gt;(shift-1)</c>, rotates them with the five tail trig views, multiplies the
    /// four output q-registers by 0x33800000 (2^-24) and stores from both ends of <paramref name="inout"/>.
    /// </summary>
    private static void ImdctTail(float[] mem, int workBase, int n, int shift, int destBase)
    {
        var d = new float[64];
        var sp = new float[32];
        int n16 = n / 16;                                                  // r1 = (n/2)>>3
        int shiftM1 = shift - 1;                                           // r2

        // Fixed trig vectors and unit constants (0x00AB3A5C..0x00AB3AF0).
        VLoad4(d, 16, 17, TrigView(8, 4 * shiftM1), 0); VStoreSp4(d, 16, 17, sp, 24);  // q8 view256[shift-1]
        VLoad4(d, 12, 13, TrigView(7, 4 * shiftM1), 0);                      // q6 view228[shift-1] (0x3a00/3a20/3a4c/3a6c)
        VLoad4(d, 14, 15, TrigView(5, 4 * (shift + 1)), 0);                    // q7 view172[shift+1]
        VCopy(d, 2, 7);                                                    // q2 = q7
        VSet(d, 0, 0.5f);                                                  // q0 = 0.5
        VLoad4(d, 16, 17, TrigView(6, 4 * shiftM1), 0); VStoreSp4(d, 16, 17, sp, 8);   // q8 view200[shift-1] (0x3a08/3a28/3a54/3a9c/3ab8)
        VLoad4(d, 10, 11, TrigView(5, 4 * shiftM1), 0);                      // q5 view172[shift-1]
        float scale24 = BitConverter.UInt32BitsToSingle(0x33800000u);      // q1 = 2^-24 (0x00AB3AB0)
        d[2 * 2] = scale24; d[2 * 2 + 1] = scale24; d[2 * 3] = scale24; d[2 * 3 + 1] = scale24;
        VLoad4(d, 16, 17, TrigView(4, 4 * shiftM1), 0); VStoreSp4(d, 16, 17, sp, 4);   // q8 view144[shift-1]
        VLoad4(d, 8, 9, TrigView(7, 4 * (shift + 1)), 0);                      // q4 view228[shift+1] (0x3a00/3a20/3a84/3ad0)
        VLoad4(d, 16, 17, TrigView(8, 4 * (shift + 1)), 0); VStoreSp4(d, 16, 17, sp, 20); // q8 view256[shift+1]
        VLoad4(d, 16, 17, TrigView(6, 4 * (shift + 1)), 0); VStoreSp4(d, 16, 17, sp, 0); // q8 view200[shift+1] (0x3a08/3a28/3ae0)
        VLoad4(d, 16, 17, TrigView(4, 4 * (shift + 1)), 0); VStoreSp4(d, 16, 17, sp, 12); // q8 view144[shift+1]

        int revIndex = n16;                                                // r8 as a u16 index (pre-decrement)
        int destEnd2 = n / 2 - 4;                                          // r0
        int destMid = n / 4;                                               // r1
        int i = 0;                                                         // r3
        int j = n16 - 1;                                                   // r2

        while (true)
        {
            revIndex--;                                                    // 0x3afc: ldrh ip,[r8,#-2]!
            int ipIdx = ImdctBitRev[revIndex];
            int destStart = 4 * i;                                         // 0x3b00: r4 = in + i*16 bytes
            VLoadSp4(d, 16, 17, sp, 20);                                   // 0x3b04: q8 = sp[0x50]
            i++;                                                           // 0x3b0c
            int lrIdx = ImdctBitRev[i - 1];                                // 0x3b10: bitrev[i]
            VMul(d, 15, 8, 2);                                             // 0x3b14: q15 = q8 * q2
            int destEnd = 4 * j;                                           // 0x3b1c: r5 = in + j*16 bytes
            VMul(d, 9, 8, 4);                                              // 0x3b24: q9 = q8 * q4
            VLoadSp4(d, 16, 17, sp, 12);                                   // 0x3b28: q8 = sp[0x30]
            j--;                                                           // 0x3b30
            int ipShift = ipIdx >> shiftM1;                                // 0x3b34
            int lrShift = lrIdx >> shiftM1;                                // 0x3b38
            VSub(d, 15, 15, 8);                                            // 0x3b40: q15 = q15 - q8
            VLoadSp4(d, 20, 21, sp, 0);                                   // 0x3b54: q10 = sp[0x40]
            VSub(d, 9, 9, 10);                                             // 0x3b58: q9 = q9 - q10
            int ipWork = 4 * ipShift;                                      // 0x3b4c
            int lrWork = 4 * lrShift;                                      // 0x3b50
            // vldr d16,[ip,#0x10]; vldr d17,[ip,#0x18]; vldr d24,[lr,#0x10]; vldr d25,[lr,#0x18]
            d[2 * 16] = mem[workBase + ipWork + 4]; d[2 * 16 + 1] = mem[workBase + ipWork + 5];
            d[2 * 17] = mem[workBase + ipWork + 6]; d[2 * 17 + 1] = mem[workBase + ipWork + 7];
            d[2 * 24] = mem[workBase + lrWork + 4]; d[2 * 24 + 1] = mem[workBase + lrWork + 5];
            d[2 * 25] = mem[workBase + lrWork + 6]; d[2 * 25 + 1] = mem[workBase + lrWork + 7];
            // vld1.64 {d26,d27},[lr:0x40]; vld1.64 {d22,d23},[ip:0x40]
            d[2 * 26] = mem[workBase + lrWork]; d[2 * 26 + 1] = mem[workBase + lrWork + 1];
            d[2 * 27] = mem[workBase + lrWork + 2]; d[2 * 27 + 1] = mem[workBase + lrWork + 3];
            d[2 * 22] = mem[workBase + ipWork]; d[2 * 22 + 1] = mem[workBase + ipWork + 1];
            d[2 * 23] = mem[workBase + ipWork + 2]; d[2 * 23 + 1] = mem[workBase + ipWork + 3];
            float sl = d[2 * 24 + 1];                                      // 0x3b74
            float sb = d[2 * 17 + 1];                                      // 0x3b78
            float r7 = d[2 * 26 + 1];                                      // 0x3b7c
            float r6 = d[2 * 23 + 1];                                      // 0x3b80
            float r5 = d[2 * 25];                                          // 0x3b84
            float r4 = d[2 * 16];                                          // 0x3b88
            float lr = d[2 * 27];                                          // 0x3b8c
            float ip = d[2 * 22];                                          // 0x3b90
            VLoadSp4(d, 28, 29, sp, 24);                                   // 0x3b94: q14 = sp[0x60]
            VMul(d, 10, 14, 6);                                            // 0x3b9c: q10 = q14 * q6
            VLoadSp4(d, 6, 7, sp, 8);                                      // 0x3ba0: q3 = sp[0x20]
            VMul(d, 14, 14, 5);                                            // 0x3ba8: q14 = q14 * q5
            VStoreSp4(d, 8, 9, sp, 0);                                    // 0x3bac: sp[0x40] = q4
            VCopy(d, 4, 9);                                                // 0x3bb0: q4 = q9
            d[2 * 25] = sl;                                                // 0x3bb4
            VSub(d, 10, 10, 3);                                            // 0x3bb8: q10 = q10 - q3
            d[2 * 16] = sb;                                                // 0x3bbc
            d[2 * 27] = r7;                                                // 0x3bc0
            d[2 * 22] = r6;                                                // 0x3bc4
            d[2 * 24 + 1] = r5;                                            // 0x3bc8
            VLoadSp4(d, 6, 7, sp, 4);                                      // 0x3bd0: q3 = sp[0x10]
            VSub(d, 14, 14, 3);                                            // 0x3bd8: q14 = q14 - q3
            d[2 * 17 + 1] = r4;                                            // 0x3bdc
            VSub(d, 3, 12, 8);                                             // 0x3be4: q3 = q12 - q8
            d[2 * 23 + 1] = ip;                                            // 0x3be8
            VAdd(d, 8, 8, 12);                                             // 0x3bec: q8 = q8 + q12
            d[2 * 26 + 1] = lr;                                            // 0x3bf0
            VAdd(d, 12, 11, 13);                                           // 0x3bf4: q12 = q11 + q13
            VStoreSp4(d, 4, 5, sp, 12);                                    // 0x3bf8: sp[0x30] = q2
            VMul(d, 7, 3, 15);                                             // 0x3c00: q7 = q3 * q15
            VStoreSp4(d, 12, 13, sp, 8);                                   // 0x3c04: sp[0x20] = q6
            VMul(d, 3, 3, 9);                                              // 0x3c0c: q3 = q3 * q9
            VStoreSp4(d, 10, 11, sp, 4);                                   // 0x3c10: sp[0x10] = q5
            VMul(d, 9, 12, 9);                                             // 0x3c18: q9 = q12 * q9
            VMul(d, 12, 12, 15);                                           // 0x3c1c: q12 = q12 * q15
            VSub(d, 13, 11, 13);                                           // 0x3c20: q13 = q11 - q13
            VSub(d, 9, 7, 9);                                              // 0x3c24: q9 = q7 - q9
            VAdd(d, 12, 12, 3);                                            // 0x3c28: q12 = q12 + q3
            VMul(d, 7, 9, 0);                                              // 0x3c30: q7 = q9 * q0
            VMul(d, 11, 13, 0);                                            // 0x3c34: q11 = q13 * q0
            VDup(d, 9, 7, 1);                                              // 0x3c38: q9 = vdup(d7[1])
            VDup(d, 3, 7, 1);                                              // 0x3c3c: q3 = vdup(d7[1])
            VMul(d, 12, 12, 0);                                            // 0x3c40: q12 = q12 * q0
            VAdd(d, 13, 14, 10);                                           // 0x3c44: q13 = q14 + q10
            VCopy(d, 2, 15);                                               // 0x3c48: q2 = q15
            VMul(d, 15, 8, 0);                                             // 0x3c4c: q15 = q8 * q0
            VMul(d, 13, 13, 9);                                            // 0x3c50: q13 = q13 * q9
            VSub(d, 8, 7, 11);                                             // 0x3c54: q8 = q7 - q11
            VSub(d, 9, 15, 12);                                            // 0x3c58: q9 = q15 - q12
            VAdd(d, 12, 15, 12);                                           // 0x3c5c: q12 = q15 + q12
            VSub(d, 15, 14, 10);                                           // 0x3c60: q15 = q14 - q10
            VAdd(d, 11, 11, 7);                                            // 0x3c64: q11 = q11 + q7
            VNeg(d, 8);                                                    // 0x3c68: q8 = -q8
            VMul(d, 15, 15, 3);                                            // 0x3c6c: q15 = q15 * q3
            VNeg(d, 11);                                                   // 0x3c70: q11 = -q11
            VMul(d, 7, 8, 13);                                             // 0x3c74: q7 = q8 * q13
            VMul(d, 13, 9, 13);                                            // 0x3c78: q13 = q9 * q13
            VMul(d, 8, 8, 15);                                             // 0x3c7c: q8 = q8 * q15
            VMul(d, 9, 9, 15);                                             // 0x3c80: q9 = q9 * q15
            VCopy(d, 6, 10);                                               // 0x3c84: q6 = q10
            VSub(d, 8, 8, 13);                                             // 0x3c88: q8 = q8 - q13
            VMul(d, 13, 11, 14);                                           // 0x3c8c: q13 = q11 * q14
            VMul(d, 11, 11, 10);                                           // 0x3c90: q11 = q11 * q10
            VMul(d, 10, 12, 10);                                           // 0x3c94: q10 = q12 * q10
            VRev64(d, 8, 8);                                               // 0x3c98: vrev64 q8
            VMul(d, 12, 12, 14);                                           // 0x3c9c: q12 = q12 * q14
            VSwp(d, 16, 17);                                               // 0x3ca0: vswp d16,d17
            VAdd(d, 9, 9, 7);                                              // 0x3ca4: q9 = q9 + q7
            VAdd(d, 10, 10, 13);                                           // 0x3ca8: q10 = q10 + q13
            VSub(d, 12, 11, 12);                                           // 0x3cac: q12 = q11 - q12
            VRev64(d, 9, 9);                                               // 0x3cb0: vrev64 q9
            VMul(d, 8, 8, 1);                                              // 0x3cb4: q8 = q8 * q1 (2^-24)
            VSwp(d, 18, 19);                                               // 0x3cb8: vswp d18,d19
            VMul(d, 10, 10, 1);                                            // 0x3cbc: q10 = q10 * q1
            VMul(d, 9, 9, 1);                                              // 0x3cc0: q9 = q9 * q1
            VMul(d, 12, 12, 1);                                            // 0x3cc4: q12 = q12 * q1
            VStore4(d, 20, 21, mem, destBase + destStart);                 // 0x3cc8: store q10
            VCopy(d, 5, 14);                                               // 0x3ccc: q5 = q14
            VStore4(d, 24, 25, mem, destBase + destMid);                   // 0x3cd0: store q12
            destMid += 4;                                                  // 0x3cd4
            VStore4(d, 18, 19, mem, destBase + destEnd);                   // 0x3cd8: store q9
            VStore4(d, 16, 17, mem, destBase + destEnd2);                  // 0x3cdc: store q8
            destEnd2 -= 4;                                                 // 0x3ce0
            if (!(i < j)) break;                                           // 0x3ce4: blt
        }
    }

    // ---- X5-I5 / Appendix B: large butterfly 0x00AB3FCC..0x00AB4E30 (generated) ----

private static void ImdctButterfliesCore(float[] mem, int points, int shift, int gotBaseBytes, int log2n, int spBytes, int workSlotBytes)
{
    int r0, r1, r2, r3, r4, r5, r6, r7, r8, sb, sl, fp, ip, sp, lr;
    int cmpA = 0, cmpB = 0;
    var d = new float[64];
    r0 = 0; r1 = points; r2 = shift; r3 = 0; r4 = 0; r5 = 0; r6 = 0; r7 = 0;
    r8 = 0; sb = 0; sl = 0; fp = 0; ip = 0; sp = 0; lr = 0;
    sp = unchecked(spBytes + 0x194);
L_00AB3FCC:
L_00AB3FD0:
    cmpA = r1; cmpB = 0;
L_00AB3FD4:
L_00AB3FD8:
    lr = unchecked(r2 + 2);
L_00AB3FDC:
    ip = unchecked((int)0x0058C298u);
L_00AB3FE0:
    r2 = unchecked(r1 + 127);
L_00AB3FE4:
    r7 = unchecked((int)0xFFFFFFCCu);
L_00AB3FE8:
    if (cmpA >= cmpB) r2 = r1;
L_00AB3FEC:
    ip = unchecked(gotBaseBytes + 0x5C);
L_00AB3FF0:
    lr = unchecked(lr << 4);
L_00AB3FF4:
    sp = unchecked(sp - 404);
L_00AB3FF8:
    r2 = unchecked(r2 >> 7);
L_00AB3FFC:
    fp = unchecked((int)0xFFFFFFBCu);
L_00AB4000:
    r6 = unchecked(r1 + 15);
L_00AB4004:
    SV(mem, unchecked(sp + 372), lr);
L_00AB4008:
    if (cmpA >= cmpB) r6 = r1;
L_00AB400C:
    SV(mem, unchecked(sp + 368), r2);
L_00AB4010:
    r6 = unchecked(r6 >> 4);
L_00AB4014:
    r7 = IV(mem, unchecked(ip + r7));
L_00AB4018:
    sl = unchecked((int)0xFFFFFFC4u);
L_00AB401C:
    sb = unchecked((int)0xFFFFFFC0u);
L_00AB4020:
    r8 = unchecked((int)0xFFFFFFC8u);
L_00AB4024:
    r5 = unchecked((int)0xFFFFFFD0u);
L_00AB4028:
    fp = IV(mem, unchecked(ip + fp));
L_00AB402C:
    sl = IV(mem, unchecked(ip + sl));
L_00AB4030:
    sb = IV(mem, unchecked(ip + sb));
L_00AB4034:
    r8 = IV(mem, unchecked(ip + r8));
L_00AB4038:
    r4 = unchecked((int)0xFFFFFFD4u);
L_00AB403C:
    SV(mem, unchecked(sp + 376), r7);
L_00AB4040:
    SV(mem, unchecked(sp + 396), r6);
L_00AB4044:
    r5 = IV(mem, unchecked(ip + r5));
L_00AB4048:
    lr = unchecked((int)0xFFFFFFD8u);
L_00AB404C:
    r2 = IV(mem, unchecked(sp + 372));
L_00AB4050:
    SV(mem, unchecked(sp + 380), r5);
L_00AB4054:
    r4 = IV(mem, unchecked(ip + r4));
L_00AB4058:
    r2 = unchecked(r2 + 128);
L_00AB405C:
    r1 = IV(mem, unchecked(sp + 368));
L_00AB4060:
    r7 = unchecked(r2 + sl);
L_00AB4064:
    SV(mem, unchecked(sp + 384), r4);
L_00AB4068:
    cmpA = r1; cmpB = 0;
L_00AB406C:
    lr = IV(mem, unchecked(ip + lr));
L_00AB4070:
    r1 = unchecked(r2 + fp);
L_00AB4074:
    r4 = IV(mem, unchecked(sp + 372));
L_00AB4078:
    VLoad4B(d, 16, 17, mem, r1);
L_00AB407C:
    r1 = unchecked(r2 + sb);
L_00AB4080:
    SV(mem, unchecked(sp + 388), lr);
L_00AB4084:
    r5 = unchecked(r4 - 16);
L_00AB4088:
    lr = unchecked((int)0xFFFFFFA8u);
L_00AB408C:
    r2 = unchecked(r2 + r8);
L_00AB4090:
    lr = IV(mem, unchecked(ip + lr));
L_00AB4094:
    ip = unchecked(r4 + 16);
L_00AB4098:
    VStoreDB(d, 16, mem, unchecked(sp + 128));
L_00AB409C:
    VStoreDB(d, 17, mem, unchecked(sp + 136));
L_00AB40A0:
    ip = unchecked(ip + lr);
L_00AB40A4:
    lr = unchecked(r4 + lr);
L_00AB40A8:
    SV(mem, unchecked(sp + 392), ip);
L_00AB40AC:
    ip = unchecked(r4 + 144);
L_00AB40B0:
    r4 = IV(mem, unchecked(sp + 380));
L_00AB40B4:
    fp = unchecked(ip + fp);
L_00AB40B8:
    VLoad4B(d, 16, 17, mem, r7);
L_00AB40BC:
    sl = unchecked(ip + sl);
L_00AB40C0:
    r7 = IV(mem, unchecked(sp + 376));
L_00AB40C4:
    sb = unchecked(ip + sb);
L_00AB40C8:
    VStoreDB(d, 16, mem, unchecked(sp + 192));
L_00AB40CC:
    VStoreDB(d, 17, mem, unchecked(sp + 200));
L_00AB40D0:
    r8 = unchecked(ip + r8);
L_00AB40D4:
    ip = unchecked(r7 + r5);
L_00AB40D8:
    VLoad4B(d, 16, 17, mem, r1);
L_00AB40DC:
    r1 = unchecked(r4 + r5);
L_00AB40E0:
    r4 = IV(mem, unchecked(sp + 384));
L_00AB40E4:
    VStoreDB(d, 16, mem, unchecked(sp + 112));
L_00AB40E8:
    VStoreDB(d, 17, mem, unchecked(sp + 120));
L_00AB40EC:
    VLoad4B(d, 16, 17, mem, r2);
L_00AB40F0:
    r2 = unchecked(r4 + r5);
L_00AB40F4:
    VStoreDB(d, 16, mem, unchecked(sp + 176));
L_00AB40F8:
    VStoreDB(d, 17, mem, unchecked(sp + 184));
L_00AB40FC:
    r4 = IV(mem, unchecked(sp + 388));
L_00AB4100:
    VLoad4B(d, 16, 17, mem, ip);
L_00AB4104:
    ip = IV(mem, unchecked(sp + 392));
L_00AB4108:
    r5 = unchecked(r4 + r5);
L_00AB410C:
    VStoreDB(d, 16, mem, unchecked(sp + 96));
L_00AB4110:
    VStoreDB(d, 17, mem, unchecked(sp + 104));
L_00AB4114:
    VLoad4B(d, 30, 31, mem, r1);
L_00AB4118:
    VLoad4B(d, 16, 17, mem, r2);
L_00AB411C:
    VLoad4B(d, 14, 15, mem, r5);
L_00AB4120:
    VStoreDB(d, 16, mem, unchecked(sp + 80));
L_00AB4124:
    VStoreDB(d, 17, mem, unchecked(sp + 88));
L_00AB4128:
    VLoad4B(d, 16, 17, mem, ip);
L_00AB412C:
    VStoreDB(d, 16, mem, unchecked(sp + 352));
L_00AB4130:
    VStoreDB(d, 17, mem, unchecked(sp + 360));
L_00AB4134:
    VLoad4B(d, 16, 17, mem, fp);
L_00AB4138:
    VStoreDB(d, 16, mem, unchecked(sp + 144));
L_00AB413C:
    VStoreDB(d, 17, mem, unchecked(sp + 152));
L_00AB4140:
    VLoad4B(d, 16, 17, mem, sl);
L_00AB4144:
    VStoreDB(d, 16, mem, unchecked(sp + 272));
L_00AB4148:
    VStoreDB(d, 17, mem, unchecked(sp + 280));
L_00AB414C:
    VLoad4B(d, 16, 17, mem, sb);
L_00AB4150:
    VStoreDB(d, 16, mem, unchecked(sp + 160));
L_00AB4154:
    VStoreDB(d, 17, mem, unchecked(sp + 168));
L_00AB4158:
    VLoad4B(d, 16, 17, mem, r8);
L_00AB415C:
    VStoreDB(d, 16, mem, unchecked(sp + 288));
L_00AB4160:
    VStoreDB(d, 17, mem, unchecked(sp + 296));
L_00AB4164:
    VLoad4B(d, 16, 17, mem, lr);
L_00AB4168:
    VStoreDB(d, 16, mem, unchecked(sp + 32));
L_00AB416C:
    VStoreDB(d, 17, mem, unchecked(sp + 40));
L_00AB4170:
    if (cmpA <= cmpB) goto L_00AB4D84;
L_00AB4174:
    r1 = IV(mem, unchecked(sp + 368));
L_00AB4178:
    r2 = unchecked(r6 + unchecked(r6 << 1));
L_00AB417C:
    ip = unchecked(r6 << 5);
L_00AB4180:
    fp = unchecked(r6 << 4);
L_00AB4184:
    r2 = unchecked(r0 + unchecked(r2 << 4));
L_00AB4188:
    VCopy(d, 8, 7);
L_00AB418C:
    sl = unchecked(r3 + unchecked(r1 << 8));
L_00AB4190:
    VCopy(d, 12, 15);
L_00AB4194:
    r1 = 0;
L_00AB4198:
    SV(mem, unchecked(sp + 336), sl);
L_00AB419C:
    goto L_00AB41EC;
L_00AB41A0:
    VLoadDB(d, 18, mem, unchecked(sp + 16));
L_00AB41A4:
    VLoadDB(d, 19, mem, unchecked(sp + 24));
L_00AB41A8:
    VStoreDB(d, 18, mem, unchecked(sp + 80));
L_00AB41AC:
    VStoreDB(d, 19, mem, unchecked(sp + 88));
L_00AB41B0:
    VLoadDB(d, 18, mem, unchecked(sp + 48));
L_00AB41B4:
    VLoadDB(d, 19, mem, unchecked(sp + 56));
L_00AB41B8:
    VStoreDB(d, 18, mem, unchecked(sp + 96));
L_00AB41BC:
    VStoreDB(d, 19, mem, unchecked(sp + 104));
L_00AB41C0:
    VLoad4B(d, 18, 19, mem, sp);
L_00AB41C4:
    VStoreDB(d, 18, mem, unchecked(sp + 112));
L_00AB41C8:
    VStoreDB(d, 19, mem, unchecked(sp + 120));
L_00AB41CC:
    VLoadDB(d, 18, mem, unchecked(sp + 64));
L_00AB41D0:
    VLoadDB(d, 19, mem, unchecked(sp + 72));
L_00AB41D4:
    VStoreDB(d, 20, mem, unchecked(sp + 160));
L_00AB41D8:
    VStoreDB(d, 21, mem, unchecked(sp + 168));
L_00AB41DC:
    VStoreDB(d, 2, mem, unchecked(sp + 144));
L_00AB41E0:
    VStoreDB(d, 3, mem, unchecked(sp + 152));
L_00AB41E4:
    VStoreDB(d, 18, mem, unchecked(sp + 128));
L_00AB41E8:
    VStoreDB(d, 19, mem, unchecked(sp + 136));
L_00AB41EC:
    lr = unchecked(r0 + r1);
L_00AB41F0:
    r5 = unchecked(r1 + ip);
L_00AB41F4:
    VLoadDB(d, 6, mem, unchecked(sp + 32));
L_00AB41F8:
    VLoadDB(d, 7, mem, unchecked(sp + 40));
L_00AB41FC:
    r4 = unchecked(lr + fp);
L_00AB4200:
    r6 = unchecked(r5 - fp);
L_00AB4204:
    r7 = unchecked(r4 + 16);
L_00AB4208:
    VLoadDB(d, 20, mem, unchecked(sp + 96));
L_00AB420C:
    VLoadDB(d, 21, mem, unchecked(sp + 104));
L_00AB4210:
    r6 = unchecked(r0 + r6);
L_00AB4214:
    VMul(d, 1, 3, 10);
L_00AB4218:
    sb = unchecked(r4 + 32);
L_00AB421C:
    VLoad4B(d, 22, 23, mem, r2);
L_00AB4220:
    r4 = unchecked(r4 + 48);
L_00AB4224:
    r8 = unchecked(r2 + 32);
L_00AB4228:
    r5 = unchecked(r0 + r5);
L_00AB422C:
    VCopy(d, 15, 11);
L_00AB4230:
    VLoadDB(d, 22, mem, unchecked(sp + 80));
L_00AB4234:
    VLoadDB(d, 23, mem, unchecked(sp + 88));
L_00AB4238:
    VMul(d, 14, 3, 11);
L_00AB423C:
    sl = unchecked(r3 + 32);
L_00AB4240:
    VLoad4B(d, 12, 13, mem, r7);
L_00AB4244:
    r7 = unchecked(r2 + 48);
L_00AB4248:
    VSub(d, 9, 1, 12);
L_00AB424C:
    r1 = unchecked(r1 + 64);
L_00AB4250:
    VLoad4B(d, 22, 23, mem, r6);
L_00AB4254:
    r6 = unchecked(r2 + 16);
L_00AB4258:
    VSub(d, 2, 6, 11);
L_00AB425C:
    r2 = unchecked(r2 + 64);
L_00AB4260:
    VLoad4B(d, 10, 11, mem, sb);
L_00AB4264:
    sb = unchecked(r3 + 64);
L_00AB4268:
    VAdd(d, 11, 6, 11);
L_00AB426C:
    VLoad4B(d, 8, 9, mem, r4);
L_00AB4270:
    r4 = unchecked(lr + ip);
L_00AB4274:
    VCopy(d, 13, 9);
L_00AB4278:
    VSub(d, 9, 14, 8);
L_00AB427C:
    VLoadDB(d, 12, mem, unchecked(sp + 176));
L_00AB4280:
    VLoadDB(d, 13, mem, unchecked(sp + 184));
L_00AB4284:
    VCopy(d, 14, 3);
L_00AB4288:
    VLoadDB(d, 6, mem, unchecked(sp + 128));
L_00AB428C:
    VLoadDB(d, 7, mem, unchecked(sp + 136));
L_00AB4290:
    VSub(d, 10, 4, 5);
L_00AB4294:
    VStoreDB(d, 26, mem, unchecked(sp + 48));
L_00AB4298:
    VStoreDB(d, 27, mem, unchecked(sp + 56));
L_00AB429C:
    VMul(d, 14, 14, 3);
L_00AB42A0:
    VStoreDB(d, 18, mem, unchecked(sp + 16));
L_00AB42A4:
    VStoreDB(d, 19, mem, unchecked(sp + 24));
L_00AB42A8:
    VAdd(d, 4, 4, 5);
L_00AB42AC:
    VLoad4B(d, 18, 19, mem, r6);
L_00AB42B0:
    r6 = unchecked(r4 + 32);
L_00AB42B4:
    VSub(d, 8, 15, 9);
L_00AB42B8:
    VStoreDB(d, 18, mem, unchecked(sp + 224));
L_00AB42BC:
    VStoreDB(d, 19, mem, unchecked(sp + 232));
L_00AB42C0:
    VMul(d, 9, 2, 13);
L_00AB42C4:
    VLoadDB(d, 26, mem, unchecked(sp + 16));
L_00AB42C8:
    VLoadDB(d, 27, mem, unchecked(sp + 24));
L_00AB42CC:
    VMul(d, 13, 2, 13);
L_00AB42D0:
    VLoadDB(d, 4, mem, unchecked(sp + 192));
L_00AB42D4:
    VLoadDB(d, 5, mem, unchecked(sp + 200));
L_00AB42D8:
    VSub(d, 14, 14, 2);
L_00AB42DC:
    VStoreDB(d, 8, mem, unchecked(sp + 256));
L_00AB42E0:
    VStoreDB(d, 9, mem, unchecked(sp + 264));
L_00AB42E4:
    VStoreDB(d, 18, mem, unchecked(sp + 304));
L_00AB42E8:
    VStoreDB(d, 19, mem, unchecked(sp + 312));
L_00AB42EC:
    VLoadDB(d, 18, mem, unchecked(sp + 112));
L_00AB42F0:
    VLoadDB(d, 19, mem, unchecked(sp + 120));
L_00AB42F4:
    VStoreDB(d, 28, mem, unchecked(sp + 64));
L_00AB42F8:
    VStoreDB(d, 29, mem, unchecked(sp + 72));
L_00AB42FC:
    VLoadDB(d, 28, mem, unchecked(sp + 32));
L_00AB4300:
    VLoadDB(d, 29, mem, unchecked(sp + 40));
L_00AB4304:
    VMul(d, 14, 14, 9);
L_00AB4308:
    VLoad4B(d, 0, 1, mem, r7);
L_00AB430C:
    r7 = unchecked(r4 + 16);
L_00AB4310:
    r4 = unchecked(r4 + 48);
L_00AB4314:
    VLoad4B(d, 2, 3, mem, r8);
L_00AB4318:
    r8 = unchecked(lr + 16);
L_00AB431C:
    VSub(d, 12, 0, 1);
L_00AB4320:
    VLoadDB(d, 18, mem, unchecked(sp + 16));
L_00AB4324:
    VLoadDB(d, 19, mem, unchecked(sp + 24));
L_00AB4328:
    VSub(d, 4, 14, 6);
L_00AB432C:
    VLoadDB(d, 12, mem, unchecked(sp + 48));
L_00AB4330:
    VLoadDB(d, 13, mem, unchecked(sp + 56));
L_00AB4334:
    VMul(d, 5, 10, 9);
L_00AB4338:
    VLoad4B(d, 14, 15, mem, r7);
L_00AB433C:
    r7 = unchecked(lr + 32);
L_00AB4340:
    VAdd(d, 1, 1, 0);
L_00AB4344:
    VLoad4B(d, 6, 7, mem, r4);
L_00AB4348:
    r4 = unchecked(r3 + 144);
L_00AB434C:
    VStore4B(d, 8, 9, mem, sp);
L_00AB4350:
    VMul(d, 4, 10, 6);
L_00AB4354:
    VCopy(d, 10, 9);
L_00AB4358:
    VLoad4B(d, 4, 5, mem, r5);
L_00AB435C:
    r5 = unchecked(r3 + 80);
L_00AB4360:
    VLoad4B(d, 28, 29, mem, r8);
L_00AB4364:
    r8 = unchecked(r3 + 128);
L_00AB4368:
    VSub(d, 13, 4, 13);
L_00AB436C:
    VStoreDB(d, 30, mem, unchecked(sp + 208));
L_00AB4370:
    VStoreDB(d, 31, mem, unchecked(sp + 216));
L_00AB4374:
    VMul(d, 4, 12, 6);
L_00AB4378:
    VLoad4B(d, 30, 31, mem, r6);
L_00AB437C:
    r6 = unchecked(lr + 48);
L_00AB4380:
    VMul(d, 10, 8, 10);
L_00AB4384:
    VStoreDB(d, 22, mem, unchecked(sp + 320));
L_00AB4388:
    VStoreDB(d, 23, mem, unchecked(sp + 328));
L_00AB438C:
    VMul(d, 8, 8, 6);
L_00AB4390:
    VStoreDB(d, 2, mem, unchecked(sp + 240));
L_00AB4394:
    VStoreDB(d, 3, mem, unchecked(sp + 248));
L_00AB4398:
    VMul(d, 12, 12, 9);
L_00AB439C:
    VLoadDB(d, 0, mem, unchecked(sp + 304));
L_00AB43A0:
    VLoadDB(d, 1, mem, unchecked(sp + 312));
L_00AB43A4:
    VAdd(d, 4, 4, 10);
L_00AB43A8:
    VLoadDB(d, 22, mem, unchecked(sp + 208));
L_00AB43AC:
    VLoadDB(d, 23, mem, unchecked(sp + 216));
L_00AB43B0:
    VAdd(d, 5, 0, 5);
L_00AB43B4:
    VLoadDB(d, 18, mem, unchecked(sp + 224));
L_00AB43B8:
    VLoadDB(d, 19, mem, unchecked(sp + 232));
L_00AB43BC:
    VSub(d, 12, 8, 12);
L_00AB43C0:
    VLoadDB(d, 20, mem, unchecked(sp + 144));
L_00AB43C4:
    VLoadDB(d, 21, mem, unchecked(sp + 152));
L_00AB43C8:
    VAdd(d, 9, 11, 9);
L_00AB43CC:
    VLoadDB(d, 22, mem, unchecked(sp + 352));
L_00AB43D0:
    VLoadDB(d, 23, mem, unchecked(sp + 360));
L_00AB43D4:
    VSub(d, 8, 4, 5);
L_00AB43D8:
    VLoad4B(d, 12, 13, mem, lr);
L_00AB43DC:
    lr = unchecked(r3 + 208);
L_00AB43E0:
    VAdd(d, 5, 4, 5);
L_00AB43E4:
    VLoad4B(d, 2, 3, mem, r7);
L_00AB43E8:
    r7 = unchecked(r3 + 192);
L_00AB43EC:
    VSub(d, 4, 2, 7);
L_00AB43F0:
    VLoad4B(d, 0, 1, mem, r6);
L_00AB43F4:
    r6 = unchecked(r3 + 16);
L_00AB43F8:
    VAdd(d, 2, 2, 7);
L_00AB43FC:
    VLoadDB(d, 14, mem, unchecked(sp + 64));
L_00AB4400:
    VLoadDB(d, 15, mem, unchecked(sp + 72));
L_00AB4404:
    VMul(d, 10, 11, 10);
L_00AB4408:
    VStoreDB(d, 4, mem, unchecked(sp + 176));
L_00AB440C:
    VStoreDB(d, 5, mem, unchecked(sp + 184));
L_00AB4410:
    VSub(d, 2, 15, 3);
L_00AB4414:
    VAdd(d, 15, 15, 3);
L_00AB4418:
    VSub(d, 3, 0, 1);
L_00AB441C:
    VAdd(d, 0, 0, 1);
L_00AB4420:
    VLoadDB(d, 2, mem, unchecked(sp + 272));
L_00AB4424:
    VLoadDB(d, 3, mem, unchecked(sp + 280));
L_00AB4428:
    VStoreDB(d, 30, mem, unchecked(sp + 192));
L_00AB442C:
    VStoreDB(d, 31, mem, unchecked(sp + 200));
L_00AB4430:
    VSub(d, 15, 6, 14);
L_00AB4434:
    VAdd(d, 14, 14, 6);
L_00AB4438:
    VSub(d, 1, 10, 1);
L_00AB443C:
    VLoad4B(d, 20, 21, mem, sp);
L_00AB4440:
    VMul(d, 6, 4, 7);
L_00AB4444:
    VStoreDB(d, 0, mem, unchecked(sp + 224));
L_00AB4448:
    VStoreDB(d, 1, mem, unchecked(sp + 232));
L_00AB444C:
    VMul(d, 4, 4, 10);
L_00AB4450:
    VStoreDB(d, 28, mem, unchecked(sp + 208));
L_00AB4454:
    VStoreDB(d, 29, mem, unchecked(sp + 216));
L_00AB4458:
    VMul(d, 10, 2, 10);
L_00AB445C:
    VSub(d, 14, 12, 13);
L_00AB4460:
    VAdd(d, 13, 12, 13);
L_00AB4464:
    VCopy(d, 12, 11);
L_00AB4468:
    VLoadDB(d, 22, mem, unchecked(sp + 160));
L_00AB446C:
    VLoadDB(d, 23, mem, unchecked(sp + 168));
L_00AB4470:
    VSub(d, 10, 6, 10);
L_00AB4474:
    VLoad4B(d, 12, 13, mem, sp);
L_00AB4478:
    VMul(d, 12, 12, 11);
L_00AB447C:
    VLoadDB(d, 22, mem, unchecked(sp + 288));
L_00AB4480:
    VLoadDB(d, 23, mem, unchecked(sp + 296));
L_00AB4484:
    VMul(d, 0, 8, 1);
L_00AB4488:
    VStoreDB(d, 20, mem, unchecked(sp + 304));
L_00AB448C:
    VStoreDB(d, 21, mem, unchecked(sp + 312));
L_00AB4490:
    VMul(d, 2, 2, 7);
L_00AB4494:
    VSub(d, 10, 12, 11);
L_00AB4498:
    VLoadDB(d, 22, mem, unchecked(sp + 320));
L_00AB449C:
    VLoadDB(d, 23, mem, unchecked(sp + 328));
L_00AB44A0:
    VAdd(d, 2, 2, 4);
L_00AB44A4:
    VMul(d, 12, 14, 10);
L_00AB44A8:
    VMul(d, 4, 3, 7);
L_00AB44AC:
    VMul(d, 14, 14, 1);
L_00AB44B0:
    VSub(d, 0, 0, 12);
L_00AB44B4:
    VSub(d, 12, 9, 11);
L_00AB44B8:
    VAdd(d, 9, 9, 11);
L_00AB44BC:
    VLoadDB(d, 22, mem, unchecked(sp + 240));
L_00AB44C0:
    VLoadDB(d, 23, mem, unchecked(sp + 248));
L_00AB44C4:
    VMul(d, 8, 8, 10);
L_00AB44C8:
    VTrn(d, 0, 5);
L_00AB44CC:
    VMul(d, 3, 3, 6);
L_00AB44D0:
    VStoreDB(d, 18, mem, unchecked(sp + 288));
L_00AB44D4:
    VStoreDB(d, 19, mem, unchecked(sp + 296));
L_00AB44D8:
    VAdd(d, 8, 14, 8);
L_00AB44DC:
    VLoadDB(d, 18, mem, unchecked(sp + 256));
L_00AB44E0:
    VLoadDB(d, 19, mem, unchecked(sp + 264));
L_00AB44E4:
    VSub(d, 9, 11, 9);
L_00AB44E8:
    VLoadDB(d, 28, mem, unchecked(sp + 240));
L_00AB44EC:
    VLoadDB(d, 29, mem, unchecked(sp + 248));
L_00AB44F0:
    VMul(d, 11, 15, 6);
L_00AB44F4:
    VLoadDB(d, 12, mem, unchecked(sp + 144));
L_00AB44F8:
    VLoadDB(d, 13, mem, unchecked(sp + 152));
L_00AB44FC:
    VMul(d, 15, 15, 7);
L_00AB4500:
    VLoadDB(d, 14, mem, unchecked(sp + 288));
L_00AB4504:
    VLoadDB(d, 15, mem, unchecked(sp + 296));
L_00AB4508:
    VSub(d, 11, 4, 11);
L_00AB450C:
    VLoadDB(d, 8, mem, unchecked(sp + 256));
L_00AB4510:
    VLoadDB(d, 9, mem, unchecked(sp + 264));
L_00AB4514:
    VAdd(d, 3, 15, 3);
L_00AB4518:
    VStoreDB(d, 12, mem, unchecked(sp + 272));
L_00AB451C:
    VStoreDB(d, 13, mem, unchecked(sp + 280));
L_00AB4520:
    VMul(d, 15, 12, 1);
L_00AB4524:
    VTrn(d, 8, 13);
L_00AB4528:
    VAdd(d, 14, 14, 4);
L_00AB452C:
    VMul(d, 4, 9, 10);
L_00AB4530:
    VMul(d, 12, 12, 10);
L_00AB4534:
    VMul(d, 9, 9, 1);
L_00AB4538:
    VSub(d, 15, 15, 4);
L_00AB453C:
    VLoadDB(d, 8, mem, unchecked(sp + 304));
L_00AB4540:
    VLoadDB(d, 9, mem, unchecked(sp + 312));
L_00AB4544:
    VSub(d, 6, 3, 2);
L_00AB4548:
    VAdd(d, 12, 9, 12);
L_00AB454C:
    VTrn(d, 15, 7);
L_00AB4550:
    VSub(d, 9, 4, 11);
L_00AB4554:
    VAdd(d, 11, 11, 4);
L_00AB4558:
    VCopyD(d, 8, 0);
L_00AB455C:
    VTrn(d, 12, 14);
L_00AB4560:
    VCopyD(d, 9, 30);
L_00AB4564:
    VAdd(d, 2, 3, 2);
L_00AB4568:
    VCopyD(d, 6, 10);
L_00AB456C:
    VStore4B(d, 8, 9, mem, r3);
L_00AB4570:
    VCopyD(d, 7, 14);
L_00AB4574:
    VLoadDB(d, 8, mem, unchecked(sp + 208));
L_00AB4578:
    VLoadDB(d, 9, mem, unchecked(sp + 216));
L_00AB457C:
    VCopyD(d, 0, 1);
L_00AB4580:
    VCopyD(d, 1, 31);
L_00AB4584:
    VLoadDB(d, 30, mem, unchecked(sp + 176));
L_00AB4588:
    VLoadDB(d, 31, mem, unchecked(sp + 184));
L_00AB458C:
    VCopyD(d, 14, 16);
L_00AB4590:
    VSub(d, 15, 15, 4);
L_00AB4594:
    VStore4B(d, 6, 7, mem, sb);
L_00AB4598:
    sb = unchecked(r3 + 96);
L_00AB459C:
    VCopyD(d, 8, 11);
L_00AB45A0:
    VLoadDB(d, 10, mem, unchecked(sp + 224));
L_00AB45A4:
    VLoadDB(d, 11, mem, unchecked(sp + 232));
L_00AB45A8:
    VCopyD(d, 16, 17);
L_00AB45AC:
    VCopyD(d, 17, 25);
L_00AB45B0:
    VLoadDB(d, 6, mem, unchecked(sp + 192));
L_00AB45B4:
    VLoadDB(d, 7, mem, unchecked(sp + 200));
L_00AB45B8:
    VCopyD(d, 9, 15);
L_00AB45BC:
    VSub(d, 5, 5, 3);
L_00AB45C0:
    VStore4B(d, 0, 1, mem, r8);
L_00AB45C4:
    r8 = unchecked(r3 + 160);
L_00AB45C8:
    VMul(d, 0, 6, 1);
L_00AB45CC:
    VStoreDB(d, 16, mem, unchecked(sp + 144));
L_00AB45D0:
    VStoreDB(d, 17, mem, unchecked(sp + 152));
L_00AB45D4:
    VMul(d, 8, 9, 10);
L_00AB45D8:
    VStore4B(d, 8, 9, mem, r7);
L_00AB45DC:
    r7 = unchecked(r3 + 224);
L_00AB45E0:
    VMul(d, 9, 9, 1);
L_00AB45E4:
    VLoadDB(d, 8, mem, unchecked(sp + 208));
L_00AB45E8:
    VLoadDB(d, 9, mem, unchecked(sp + 216));
L_00AB45EC:
    VCopyD(d, 15, 24);
L_00AB45F0:
    VMul(d, 12, 6, 10);
L_00AB45F4:
    VMul(d, 3, 5, 1);
L_00AB45F8:
    VMul(d, 6, 15, 10);
L_00AB45FC:
    VMul(d, 5, 5, 10);
L_00AB4600:
    VMul(d, 15, 15, 1);
L_00AB4604:
    VSub(d, 8, 0, 8);
L_00AB4608:
    VLoadDB(d, 0, mem, unchecked(sp + 176));
L_00AB460C:
    VLoadDB(d, 1, mem, unchecked(sp + 184));
L_00AB4610:
    VAdd(d, 12, 9, 12);
L_00AB4614:
    VAdd(d, 0, 4, 0);
L_00AB4618:
    VLoadDB(d, 8, mem, unchecked(sp + 224));
L_00AB461C:
    VLoadDB(d, 9, mem, unchecked(sp + 232));
L_00AB4620:
    VCopyD(d, 18, 26);
L_00AB4624:
    VTrn(d, 8, 11);
L_00AB4628:
    VCopyD(d, 19, 28);
L_00AB462C:
    VCopyD(d, 28, 27);
L_00AB4630:
    VLoadDB(d, 26, mem, unchecked(sp + 192));
L_00AB4634:
    VLoadDB(d, 27, mem, unchecked(sp + 200));
L_00AB4638:
    VAdd(d, 13, 4, 13);
L_00AB463C:
    VSub(d, 3, 3, 6);
L_00AB4640:
    VLoadDB(d, 12, mem, unchecked(sp + 160));
L_00AB4644:
    VLoadDB(d, 13, mem, unchecked(sp + 168));
L_00AB4648:
    VAdd(d, 15, 15, 5);
L_00AB464C:
    VLoadDB(d, 10, mem, unchecked(sp + 128));
L_00AB4650:
    VLoadDB(d, 11, mem, unchecked(sp + 136));
L_00AB4654:
    VStoreDB(d, 10, mem, unchecked(sp + 192));
L_00AB4658:
    VStoreDB(d, 11, mem, unchecked(sp + 200));
L_00AB465C:
    VCopy(d, 4, 12);
L_00AB4660:
    VTrn(d, 3, 0);
L_00AB4664:
    VTrn(d, 15, 13);
L_00AB4668:
    VLoadDB(d, 10, mem, unchecked(sp + 112));
L_00AB466C:
    VLoadDB(d, 11, mem, unchecked(sp + 120));
L_00AB4670:
    VStoreDB(d, 12, mem, unchecked(sp + 288));
L_00AB4674:
    VStoreDB(d, 13, mem, unchecked(sp + 296));
L_00AB4678:
    VCopy(d, 6, 15);
L_00AB467C:
    VCopyD(d, 30, 17);
L_00AB4680:
    VStoreDB(d, 10, mem, unchecked(sp + 176));
L_00AB4684:
    VStoreDB(d, 11, mem, unchecked(sp + 184));
L_00AB4688:
    VCopyD(d, 31, 7);
L_00AB468C:
    VCopyD(d, 10, 16);
L_00AB4690:
    VCopyD(d, 11, 6);
L_00AB4694:
    VTrn(d, 4, 2);
L_00AB4698:
    VCopyD(d, 6, 22);
L_00AB469C:
    VCopyD(d, 7, 0);
L_00AB46A0:
    VLoadDB(d, 24, mem, unchecked(sp + 96));
L_00AB46A4:
    VLoadDB(d, 25, mem, unchecked(sp + 104));
L_00AB46A8:
    VCopyD(d, 0, 23);
L_00AB46AC:
    VStore4B(d, 10, 11, mem, r6);
L_00AB46B0:
    r6 = unchecked(r3 + 48);
L_00AB46B4:
    VCopyD(d, 22, 8);
L_00AB46B8:
    VStore4B(d, 6, 7, mem, r5);
L_00AB46BC:
    r5 = unchecked(r3 + 112);
L_00AB46C0:
    VCopyD(d, 23, 12);
L_00AB46C4:
    VStore4B(d, 30, 31, mem, r4);
L_00AB46C8:
    r4 = unchecked(r3 + 176);
L_00AB46CC:
    VCopyD(d, 8, 9);
L_00AB46D0:
    VStore4B(d, 0, 1, mem, lr);
L_00AB46D4:
    lr = unchecked(r3 + 240);
L_00AB46D8:
    SV(mem, unchecked(sp + 160), lr);
L_00AB46DC:
    r3 = unchecked(r3 + 256);
L_00AB46E0:
    lr = IV(mem, unchecked(sp + 336));
L_00AB46E4:
    VCopyD(d, 9, 13);
L_00AB46E8:
    VCopyD(d, 12, 4);
L_00AB46EC:
    VStore4B(d, 14, 15, mem, sl);
L_00AB46F0:
    VCopyD(d, 13, 26);
L_00AB46F4:
    cmpA = r3; cmpB = lr;
L_00AB46F8:
    VCopyD(d, 26, 5);
L_00AB46FC:
    lr = IV(mem, unchecked(sp + 160));
L_00AB4700:
    VStore4B(d, 18, 19, mem, sb);
L_00AB4704:
    VLoadDB(d, 18, mem, unchecked(sp + 144));
L_00AB4708:
    VLoadDB(d, 19, mem, unchecked(sp + 152));
L_00AB470C:
    VLoadDB(d, 16, mem, unchecked(sp + 80));
L_00AB4710:
    VLoadDB(d, 17, mem, unchecked(sp + 88));
L_00AB4714:
    VStore4B(d, 18, 19, mem, r8);
L_00AB4718:
    VStore4B(d, 28, 29, mem, r7);
L_00AB471C:
    VStore4B(d, 22, 23, mem, r6);
L_00AB4720:
    VStore4B(d, 12, 13, mem, r5);
L_00AB4724:
    VStore4B(d, 8, 9, mem, r4);
L_00AB4728:
    VStore4B(d, 26, 27, mem, lr);
L_00AB472C:
    if (cmpA != cmpB) goto L_00AB41A0;
L_00AB4730:
    r3 = IV(mem, unchecked(sp + 368));
L_00AB4734:
    sl = IV(mem, unchecked(sp + 336));
L_00AB4738:
    r6 = unchecked(r3 << 2);
L_00AB473C:
    r3 = IV(mem, unchecked(sp + 392));
L_00AB4740:
    VLoad4B(d, 16, 17, mem, r3);
L_00AB4744:
    VStoreDB(d, 16, mem, unchecked(sp + 288));
L_00AB4748:
    VStoreDB(d, 17, mem, unchecked(sp + 296));
L_00AB474C:
    r2 = IV(mem, unchecked(sp + 372));
L_00AB4750:
    r1 = IV(mem, unchecked(sp + 376));
L_00AB4754:
    sb = unchecked(r1 + r2);
L_00AB4758:
    r1 = IV(mem, unchecked(sp + 380));
L_00AB475C:
    r3 = unchecked(r1 + r2);
L_00AB4760:
    r1 = IV(mem, unchecked(sp + 384));
L_00AB4764:
    VLoad4B(d, 16, 17, mem, sb);
L_00AB4768:
    r5 = unchecked(r1 + r2);
L_00AB476C:
    r1 = IV(mem, unchecked(sp + 388));
L_00AB4770:
    VStoreDB(d, 16, mem, unchecked(sp + 144));
L_00AB4774:
    VStoreDB(d, 17, mem, unchecked(sp + 152));
L_00AB4778:
    lr = unchecked(r1 + r2);
L_00AB477C:
    r2 = IV(mem, unchecked(sp + 368));
L_00AB4780:
    VLoad4B(d, 16, 17, mem, r3);
L_00AB4784:
    cmpA = r2; cmpB = 0;
L_00AB4788:
    VStoreDB(d, 16, mem, unchecked(sp + 272));
L_00AB478C:
    VStoreDB(d, 17, mem, unchecked(sp + 280));
L_00AB4790:
    VLoad4B(d, 16, 17, mem, r5);
L_00AB4794:
    VStoreDB(d, 16, mem, unchecked(sp + 160));
L_00AB4798:
    VStoreDB(d, 17, mem, unchecked(sp + 168));
L_00AB479C:
    VLoad4B(d, 16, 17, mem, lr);
L_00AB47A0:
    VStoreDB(d, 16, mem, unchecked(sp + 256));
L_00AB47A4:
    VStoreDB(d, 17, mem, unchecked(sp + 264));
L_00AB47A8:
    if (cmpA <= cmpB) goto L_00AB4D78;
L_00AB47AC:
    ip = IV(mem, unchecked(sp + 396));
L_00AB47B0:
    r2 = unchecked(sl + unchecked(r2 << 8));
L_00AB47B4:
    SV(mem, unchecked(sp + 352), r2);
L_00AB47B8:
    r3 = unchecked(r0 + unchecked(r6 << 4));
L_00AB47BC:
    r1 = unchecked(ip + r6);
L_00AB47C0:
    VLoadDB(d, 14, mem, unchecked(sp + 128));
L_00AB47C4:
    VLoadDB(d, 15, mem, unchecked(sp + 136));
L_00AB47C8:
    r2 = unchecked(ip + r1);
L_00AB47CC:
    sb = unchecked(ip + r2);
L_00AB47D0:
    r1 = unchecked(r0 + unchecked(r1 << 4));
L_00AB47D4:
    r2 = unchecked(r0 + unchecked(r2 << 4));
L_00AB47D8:
    sb = unchecked(r0 + unchecked(sb << 4));
L_00AB47DC:
    goto L_00AB482C;
L_00AB47E0:
    VLoadDB(d, 16, mem, unchecked(sp + 240));
L_00AB47E4:
    VLoadDB(d, 17, mem, unchecked(sp + 248));
L_00AB47E8:
    VStoreDB(d, 16, mem, unchecked(sp + 16));
L_00AB47EC:
    VStoreDB(d, 17, mem, unchecked(sp + 24));
L_00AB47F0:
    VLoadDB(d, 16, mem, unchecked(sp + 336));
L_00AB47F4:
    VLoadDB(d, 17, mem, unchecked(sp + 344));
L_00AB47F8:
    VStoreDB(d, 16, mem, unchecked(sp + 48));
L_00AB47FC:
    VStoreDB(d, 17, mem, unchecked(sp + 56));
L_00AB4800:
    VLoadDB(d, 16, mem, unchecked(sp + 128));
L_00AB4804:
    VLoadDB(d, 17, mem, unchecked(sp + 136));
L_00AB4808:
    VStore4B(d, 16, 17, mem, sp);
L_00AB480C:
    VLoadDB(d, 16, mem, unchecked(sp + 224));
L_00AB4810:
    VLoadDB(d, 17, mem, unchecked(sp + 232));
L_00AB4814:
    VStoreDB(d, 10, mem, unchecked(sp + 160));
L_00AB4818:
    VStoreDB(d, 11, mem, unchecked(sp + 168));
L_00AB481C:
    VStoreDB(d, 4, mem, unchecked(sp + 144));
L_00AB4820:
    VStoreDB(d, 5, mem, unchecked(sp + 152));
L_00AB4824:
    VStoreDB(d, 16, mem, unchecked(sp + 64));
L_00AB4828:
    VStoreDB(d, 17, mem, unchecked(sp + 72));
L_00AB482C:
    ip = unchecked(r3 + 16);
L_00AB4830:
    r0 = unchecked(r1 + 16);
L_00AB4834:
    VLoadDB(d, 28, mem, unchecked(sp + 32));
L_00AB4838:
    VLoadDB(d, 29, mem, unchecked(sp + 40));
L_00AB483C:
    r5 = unchecked(sb + 32);
L_00AB4840:
    r4 = unchecked(sb + 48);
L_00AB4844:
    r7 = unchecked(r1 + 32);
L_00AB4848:
    VLoadDB(d, 18, mem, unchecked(sp + 48));
L_00AB484C:
    VLoadDB(d, 19, mem, unchecked(sp + 56));
L_00AB4850:
    r6 = unchecked(r1 + 48);
L_00AB4854:
    VMul(d, 2, 14, 9);
L_00AB4858:
    lr = unchecked(sb + 16);
L_00AB485C:
    VLoad4B(d, 30, 31, mem, ip);
L_00AB4860:
    r8 = unchecked(r3 + 48);
L_00AB4864:
    ip = unchecked(sl + 192);
L_00AB4868:
    fp = unchecked(sl + 80);
L_00AB486C:
    VStoreDB(d, 30, mem, unchecked(sp + 176));
L_00AB4870:
    VStoreDB(d, 31, mem, unchecked(sp + 184));
L_00AB4874:
    VLoadDB(d, 30, mem, unchecked(sp + 64));
L_00AB4878:
    VLoadDB(d, 31, mem, unchecked(sp + 72));
L_00AB487C:
    VMul(d, 15, 14, 15);
L_00AB4880:
    VLoad4B(d, 12, 13, mem, r1);
L_00AB4884:
    r1 = unchecked(r1 + 64);
L_00AB4888:
    VLoad4B(d, 10, 11, mem, r0);
L_00AB488C:
    r0 = unchecked(r3 + 32);
L_00AB4890:
    VSub(d, 3, 5, 6);
L_00AB4894:
    VLoadDB(d, 22, mem, unchecked(sp + 96));
L_00AB4898:
    VLoadDB(d, 23, mem, unchecked(sp + 104));
L_00AB489C:
    VSub(d, 9, 2, 11);
L_00AB48A0:
    VLoadDB(d, 20, mem, unchecked(sp + 16));
L_00AB48A4:
    VLoadDB(d, 21, mem, unchecked(sp + 24));
L_00AB48A8:
    VMul(d, 8, 14, 10);
L_00AB48AC:
    VLoadDB(d, 22, mem, unchecked(sp + 80));
L_00AB48B0:
    VLoadDB(d, 23, mem, unchecked(sp + 88));
L_00AB48B4:
    VSub(d, 14, 15, 7);
L_00AB48B8:
    VLoad4B(d, 2, 3, mem, r5);
L_00AB48BC:
    r5 = unchecked(r2 + 48);
L_00AB48C0:
    VMul(d, 12, 3, 9);
L_00AB48C4:
    VStoreDB(d, 18, mem, unchecked(sp + 336));
L_00AB48C8:
    VStoreDB(d, 19, mem, unchecked(sp + 344));
L_00AB48CC:
    VSub(d, 8, 8, 11);
L_00AB48D0:
    VLoad4B(d, 14, 15, mem, sp);
L_00AB48D4:
    VCopy(d, 13, 9);
L_00AB48D8:
    VStoreDB(d, 28, mem, unchecked(sp + 224));
L_00AB48DC:
    VStoreDB(d, 29, mem, unchecked(sp + 232));
L_00AB48E0:
    VAdd(d, 5, 5, 6);
L_00AB48E4:
    VLoad4B(d, 18, 19, mem, r4);
L_00AB48E8:
    r4 = unchecked(sl + 64);
L_00AB48EC:
    VSub(d, 10, 9, 1);
L_00AB48F0:
    VLoadDB(d, 28, mem, unchecked(sp + 32));
L_00AB48F4:
    VLoadDB(d, 29, mem, unchecked(sp + 40));
L_00AB48F8:
    VMul(d, 7, 14, 7);
L_00AB48FC:
    VStoreDB(d, 24, mem, unchecked(sp + 80));
L_00AB4900:
    VStoreDB(d, 25, mem, unchecked(sp + 88));
L_00AB4904:
    VCopy(d, 12, 8);
L_00AB4908:
    VMul(d, 8, 3, 8);
L_00AB490C:
    VLoad4B(d, 8, 9, mem, r7);
L_00AB4910:
    r7 = unchecked(r2 + 16);
L_00AB4914:
    VLoad4B(d, 0, 1, mem, r6);
L_00AB4918:
    VAdd(d, 1, 1, 9);
L_00AB491C:
    r6 = unchecked(r2 + 32);
L_00AB4920:
    VSub(d, 11, 0, 4);
L_00AB4924:
    VLoad4B(d, 4, 5, mem, lr);
L_00AB4928:
    lr = unchecked(sl + 128);
L_00AB492C:
    VAdd(d, 0, 0, 4);
L_00AB4930:
    VLoadDB(d, 8, mem, unchecked(sp + 112));
L_00AB4934:
    VLoadDB(d, 9, mem, unchecked(sp + 120));
L_00AB4938:
    VSub(d, 4, 7, 4);
L_00AB493C:
    VStoreDB(d, 20, mem, unchecked(sp + 96));
L_00AB4940:
    VStoreDB(d, 21, mem, unchecked(sp + 104));
L_00AB4944:
    VLoad4B(d, 20, 21, mem, sb);
L_00AB4948:
    VCopy(d, 7, 13);
L_00AB494C:
    sb = unchecked(sb + 64);
L_00AB4950:
    VStoreDB(d, 16, mem, unchecked(sp + 192));
L_00AB4954:
    VStoreDB(d, 17, mem, unchecked(sp + 200));
L_00AB4958:
    VSub(d, 8, 10, 2);
L_00AB495C:
    VLoad4B(d, 6, 7, mem, r0);
L_00AB4960:
    VAdd(d, 10, 10, 2);
L_00AB4964:
    r0 = unchecked(sl + 16);
L_00AB4968:
    VLoad4B(d, 30, 31, mem, r8);
L_00AB496C:
    r8 = unchecked(sl + 144);
L_00AB4970:
    VLoadDB(d, 28, mem, unchecked(sp + 80));
L_00AB4974:
    VLoadDB(d, 29, mem, unchecked(sp + 88));
L_00AB4978:
    VStoreDB(d, 24, mem, unchecked(sp + 240));
L_00AB497C:
    VStoreDB(d, 25, mem, unchecked(sp + 248));
L_00AB4980:
    VMul(d, 12, 11, 12);
L_00AB4984:
    VStoreDB(d, 0, mem, unchecked(sp + 208));
L_00AB4988:
    VStoreDB(d, 1, mem, unchecked(sp + 216));
L_00AB498C:
    VMul(d, 11, 11, 13);
L_00AB4990:
    VLoad4B(d, 0, 1, mem, r7);
L_00AB4994:
    r7 = unchecked(sl + 208);
L_00AB4998:
    VAdd(d, 12, 14, 12);
L_00AB499C:
    VStoreDB(d, 8, mem, unchecked(sp + 128));
L_00AB49A0:
    VStoreDB(d, 9, mem, unchecked(sp + 136));
L_00AB49A4:
    VStoreDB(d, 10, mem, unchecked(sp + 304));
L_00AB49A8:
    VStoreDB(d, 11, mem, unchecked(sp + 312));
L_00AB49AC:
    VLoad4B(d, 8, 9, mem, r6);
L_00AB49B0:
    r6 = unchecked(sl + 96);
L_00AB49B4:
    VLoad4B(d, 10, 11, mem, r5);
L_00AB49B8:
    r5 = unchecked(sl + 32);
L_00AB49BC:
    SV(mem, unchecked(sp + 320), r5);
L_00AB49C0:
    r5 = unchecked(sl + 224);
L_00AB49C4:
    VLoadDB(d, 18, mem, unchecked(sp + 96));
L_00AB49C8:
    VLoadDB(d, 19, mem, unchecked(sp + 104));
L_00AB49CC:
    VLoadDB(d, 12, mem, unchecked(sp + 240));
L_00AB49D0:
    VLoadDB(d, 13, mem, unchecked(sp + 248));
L_00AB49D4:
    VStoreDB(d, 2, mem, unchecked(sp + 80));
L_00AB49D8:
    VStoreDB(d, 3, mem, unchecked(sp + 88));
L_00AB49DC:
    VMul(d, 1, 9, 13);
L_00AB49E0:
    VMul(d, 13, 9, 6);
L_00AB49E4:
    VLoadDB(d, 18, mem, unchecked(sp + 48));
L_00AB49E8:
    VLoadDB(d, 19, mem, unchecked(sp + 56));
L_00AB49EC:
    VMul(d, 6, 8, 6);
L_00AB49F0:
    VLoad4B(d, 4, 5, mem, r2);
L_00AB49F4:
    r2 = unchecked(r2 + 64);
L_00AB49F8:
    VMul(d, 8, 8, 7);
L_00AB49FC:
    VStoreDB(d, 18, mem, unchecked(sp + 96));
L_00AB4A00:
    VStoreDB(d, 19, mem, unchecked(sp + 104));
L_00AB4A04:
    VLoadDB(d, 18, mem, unchecked(sp + 160));
L_00AB4A08:
    VLoadDB(d, 19, mem, unchecked(sp + 168));
L_00AB4A0C:
    VAdd(d, 6, 1, 6);
L_00AB4A10:
    VLoadDB(d, 2, mem, unchecked(sp + 288));
L_00AB4A14:
    VLoadDB(d, 3, mem, unchecked(sp + 296));
L_00AB4A18:
    VSub(d, 13, 8, 13);
L_00AB4A1C:
    VLoadDB(d, 28, mem, unchecked(sp + 192));
L_00AB4A20:
    VLoadDB(d, 29, mem, unchecked(sp + 200));
L_00AB4A24:
    VMul(d, 8, 1, 9);
L_00AB4A28:
    VLoadDB(d, 18, mem, unchecked(sp + 224));
L_00AB4A2C:
    VLoadDB(d, 19, mem, unchecked(sp + 232));
L_00AB4A30:
    VSub(d, 1, 2, 0);
L_00AB4A34:
    VAdd(d, 2, 2, 0);
L_00AB4A38:
    VAdd(d, 0, 4, 5);
L_00AB4A3C:
    VSub(d, 11, 11, 14);
L_00AB4A40:
    VLoad4B(d, 28, 29, mem, r3);
L_00AB4A44:
    r3 = unchecked(r3 + 64);
L_00AB4A48:
    VStoreDB(d, 4, mem, unchecked(sp + 48));
L_00AB4A4C:
    VStoreDB(d, 5, mem, unchecked(sp + 56));
L_00AB4A50:
    VSub(d, 2, 4, 5);
L_00AB4A54:
    VAdd(d, 5, 15, 3);
L_00AB4A58:
    VStoreDB(d, 0, mem, unchecked(sp + 112));
L_00AB4A5C:
    VStoreDB(d, 1, mem, unchecked(sp + 120));
L_00AB4A60:
    VSub(d, 4, 15, 3);
L_00AB4A64:
    VLoadDB(d, 6, mem, unchecked(sp + 176));
L_00AB4A68:
    VLoadDB(d, 7, mem, unchecked(sp + 184));
L_00AB4A6C:
    VSub(d, 15, 14, 3);
L_00AB4A70:
    VAdd(d, 3, 3, 14);
L_00AB4A74:
    VLoadDB(d, 28, mem, unchecked(sp + 256));
L_00AB4A78:
    VLoadDB(d, 29, mem, unchecked(sp + 264));
L_00AB4A7C:
    VStoreDB(d, 10, mem, unchecked(sp + 192));
L_00AB4A80:
    VStoreDB(d, 11, mem, unchecked(sp + 200));
L_00AB4A84:
    VSub(d, 5, 8, 14);
L_00AB4A88:
    VLoadDB(d, 16, mem, unchecked(sp + 128));
L_00AB4A8C:
    VLoadDB(d, 17, mem, unchecked(sp + 136));
L_00AB4A90:
    VSub(d, 7, 6, 12);
L_00AB4A94:
    VSub(d, 14, 11, 13);
L_00AB4A98:
    VStoreDB(d, 6, mem, unchecked(sp + 176));
L_00AB4A9C:
    VStoreDB(d, 7, mem, unchecked(sp + 184));
L_00AB4AA0:
    VMul(d, 0, 2, 9);
L_00AB4AA4:
    VAdd(d, 12, 6, 12);
L_00AB4AA8:
    VAdd(d, 11, 13, 11);
L_00AB4AAC:
    VLoadDB(d, 26, mem, unchecked(sp + 288));
L_00AB4AB0:
    VLoadDB(d, 27, mem, unchecked(sp + 296));
L_00AB4AB4:
    VMul(d, 6, 1, 9);
L_00AB4AB8:
    VMul(d, 1, 1, 8);
L_00AB4ABC:
    VMul(d, 8, 2, 8);
L_00AB4AC0:
    VLoadDB(d, 4, mem, unchecked(sp + 144));
L_00AB4AC4:
    VLoadDB(d, 5, mem, unchecked(sp + 152));
L_00AB4AC8:
    VMul(d, 2, 13, 2);
L_00AB4ACC:
    VLoadDB(d, 26, mem, unchecked(sp + 272));
L_00AB4AD0:
    VLoadDB(d, 27, mem, unchecked(sp + 280));
L_00AB4AD4:
    VMul(d, 3, 7, 5);
L_00AB4AD8:
    VSub(d, 8, 6, 8);
L_00AB4ADC:
    VLoadDB(d, 12, mem, unchecked(sp + 304));
L_00AB4AE0:
    VLoadDB(d, 13, mem, unchecked(sp + 312));
L_00AB4AE4:
    VSub(d, 2, 2, 13);
L_00AB4AE8:
    VMul(d, 13, 4, 9);
L_00AB4AEC:
    VAdd(d, 0, 0, 1);
L_00AB4AF0:
    VStoreDB(d, 16, mem, unchecked(sp + 256));
L_00AB4AF4:
    VStoreDB(d, 17, mem, unchecked(sp + 264));
L_00AB4AF8:
    VMul(d, 1, 14, 2);
L_00AB4AFC:
    VCopy(d, 8, 9);
L_00AB4B00:
    VStoreDB(d, 26, mem, unchecked(sp + 272));
L_00AB4B04:
    VStoreDB(d, 27, mem, unchecked(sp + 280));
L_00AB4B08:
    VMul(d, 14, 14, 5);
L_00AB4B0C:
    VLoadDB(d, 18, mem, unchecked(sp + 128));
L_00AB4B10:
    VLoadDB(d, 19, mem, unchecked(sp + 136));
L_00AB4B14:
    VMul(d, 13, 7, 2);
L_00AB4B18:
    VLoadDB(d, 14, mem, unchecked(sp + 80));
L_00AB4B1C:
    VLoadDB(d, 15, mem, unchecked(sp + 88));
L_00AB4B20:
    VAdd(d, 1, 1, 3);
L_00AB4B24:
    VSub(d, 3, 10, 6);
L_00AB4B28:
    VAdd(d, 6, 10, 6);
L_00AB4B2C:
    VLoadDB(d, 20, mem, unchecked(sp + 208));
L_00AB4B30:
    VLoadDB(d, 21, mem, unchecked(sp + 216));
L_00AB4B34:
    VMul(d, 4, 4, 9);
L_00AB4B38:
    VTrn(d, 1, 12);
L_00AB4B3C:
    VSub(d, 7, 10, 7);
L_00AB4B40:
    VMul(d, 10, 15, 9);
L_00AB4B44:
    VLoadDB(d, 18, mem, unchecked(sp + 208));
L_00AB4B48:
    VLoadDB(d, 19, mem, unchecked(sp + 216));
L_00AB4B4C:
    VMul(d, 15, 15, 8);
L_00AB4B50:
    VLoadDB(d, 16, mem, unchecked(sp + 80));
L_00AB4B54:
    VLoadDB(d, 17, mem, unchecked(sp + 88));
L_00AB4B58:
    VSub(d, 14, 13, 14);
L_00AB4B5C:
    VLoadDB(d, 26, mem, unchecked(sp + 272));
L_00AB4B60:
    VLoadDB(d, 27, mem, unchecked(sp + 280));
L_00AB4B64:
    VSub(d, 10, 13, 10);
L_00AB4B68:
    VAdd(d, 15, 15, 4);
L_00AB4B6C:
    VMul(d, 13, 7, 2);
L_00AB4B70:
    VTrn(d, 14, 11);
L_00AB4B74:
    VMul(d, 4, 3, 5);
L_00AB4B78:
    VMul(d, 7, 7, 5);
L_00AB4B7C:
    VMul(d, 3, 3, 2);
L_00AB4B80:
    VAdd(d, 4, 13, 4);
L_00AB4B84:
    VAdd(d, 9, 8, 9);
L_00AB4B88:
    VLoadDB(d, 16, mem, unchecked(sp + 16));
L_00AB4B8C:
    VLoadDB(d, 17, mem, unchecked(sp + 24));
L_00AB4B90:
    VSub(d, 3, 3, 7);
L_00AB4B94:
    VStoreDB(d, 16, mem, unchecked(sp + 80));
L_00AB4B98:
    VStoreDB(d, 17, mem, unchecked(sp + 88));
L_00AB4B9C:
    SV(mem, unchecked(sp + 208), r6);
L_00AB4BA0:
    r6 = unchecked(sl + 160);
L_00AB4BA4:
    VTrn(d, 4, 6);
L_00AB4BA8:
    VCopyD(d, 26, 2);
L_00AB4BAC:
    VLoadDB(d, 16, mem, unchecked(sp + 256));
L_00AB4BB0:
    VLoadDB(d, 17, mem, unchecked(sp + 264));
L_00AB4BB4:
    VCopyD(d, 27, 8);
L_00AB4BB8:
    VSub(d, 7, 10, 8);
L_00AB4BBC:
    VTrn(d, 3, 9);
L_00AB4BC0:
    VCopyD(d, 8, 3);
L_00AB4BC4:
    VLoadDB(d, 2, mem, unchecked(sp + 48));
L_00AB4BC8:
    VLoadDB(d, 3, mem, unchecked(sp + 56));
L_00AB4BCC:
    VAdd(d, 10, 10, 8);
L_00AB4BD0:
    VStore4B(d, 26, 27, mem, sl);
L_00AB4BD4:
    VSub(d, 8, 15, 0);
L_00AB4BD8:
    VAdd(d, 0, 15, 0);
L_00AB4BDC:
    VLoadDB(d, 30, mem, unchecked(sp + 176));
L_00AB4BE0:
    VLoadDB(d, 31, mem, unchecked(sp + 184));
L_00AB4BE4:
    VSub(d, 15, 15, 1);
L_00AB4BE8:
    VLoadDB(d, 2, mem, unchecked(sp + 192));
L_00AB4BEC:
    VLoadDB(d, 3, mem, unchecked(sp + 200));
L_00AB4BF0:
    VCopyD(d, 26, 24);
L_00AB4BF4:
    VCopyD(d, 27, 12);
L_00AB4BF8:
    VCopyD(d, 24, 25);
L_00AB4BFC:
    VCopyD(d, 25, 13);
L_00AB4C00:
    VLoadDB(d, 12, mem, unchecked(sp + 112));
L_00AB4C04:
    VLoadDB(d, 13, mem, unchecked(sp + 120));
L_00AB4C08:
    VSub(d, 1, 1, 6);
L_00AB4C0C:
    VStore4B(d, 26, 27, mem, r4);
L_00AB4C10:
    r4 = unchecked(sl + 48);
L_00AB4C14:
    VCopyD(d, 27, 6);
L_00AB4C18:
    VCopyD(d, 6, 29);
L_00AB4C1C:
    VStore4B(d, 8, 9, mem, lr);
L_00AB4C20:
    VCopyD(d, 26, 28);
L_00AB4C24:
    lr = unchecked(sl + 112);
L_00AB4C28:
    VStore4B(d, 24, 25, mem, ip);
L_00AB4C2C:
    VMul(d, 14, 7, 2);
L_00AB4C30:
    ip = unchecked(sl + 176);
L_00AB4C34:
    VMul(d, 12, 8, 5);
L_00AB4C38:
    VStoreDB(d, 6, mem, unchecked(sp + 16));
L_00AB4C3C:
    VStoreDB(d, 7, mem, unchecked(sp + 24));
L_00AB4C40:
    VMul(d, 4, 15, 5);
L_00AB4C44:
    VLoadDB(d, 12, mem, unchecked(sp + 176));
L_00AB4C48:
    VLoadDB(d, 13, mem, unchecked(sp + 184));
L_00AB4C4C:
    VMul(d, 3, 7, 5);
L_00AB4C50:
    VMul(d, 7, 8, 2);
L_00AB4C54:
    VMul(d, 8, 15, 2);
L_00AB4C58:
    VMul(d, 15, 1, 5);
L_00AB4C5C:
    VAdd(d, 12, 14, 12);
L_00AB4C60:
    VLoadDB(d, 28, mem, unchecked(sp + 48));
L_00AB4C64:
    VLoadDB(d, 29, mem, unchecked(sp + 56));
L_00AB4C68:
    VAdd(d, 6, 6, 14);
L_00AB4C6C:
    VMul(d, 1, 1, 2);
L_00AB4C70:
    VAdd(d, 8, 8, 15);
L_00AB4C74:
    VTrn(d, 12, 10);
L_00AB4C78:
    VLoad4B(d, 30, 31, mem, sp);
L_00AB4C7C:
    VSub(d, 3, 7, 3);
L_00AB4C80:
    VSub(d, 4, 1, 4);
L_00AB4C84:
    VLoadDB(d, 2, mem, unchecked(sp + 160));
L_00AB4C88:
    VLoadDB(d, 3, mem, unchecked(sp + 168));
L_00AB4C8C:
    VTrn(d, 8, 6);
L_00AB4C90:
    VCopyD(d, 28, 22);
L_00AB4C94:
    VCopyD(d, 29, 18);
L_00AB4C98:
    VStoreDB(d, 2, mem, unchecked(sp + 256));
L_00AB4C9C:
    VStoreDB(d, 3, mem, unchecked(sp + 264));
L_00AB4CA0:
    VCopyD(d, 22, 23);
L_00AB4CA4:
    VCopyD(d, 23, 19);
L_00AB4CA8:
    VLoadDB(d, 2, mem, unchecked(sp + 144));
L_00AB4CAC:
    VLoadDB(d, 3, mem, unchecked(sp + 152));
L_00AB4CB0:
    VLoadDB(d, 14, mem, unchecked(sp + 192));
L_00AB4CB4:
    VLoadDB(d, 15, mem, unchecked(sp + 200));
L_00AB4CB8:
    VLoadDB(d, 18, mem, unchecked(sp + 112));
L_00AB4CBC:
    VLoadDB(d, 19, mem, unchecked(sp + 120));
L_00AB4CC0:
    VAdd(d, 9, 7, 9);
L_00AB4CC4:
    VStoreDB(d, 2, mem, unchecked(sp + 272));
L_00AB4CC8:
    VStoreDB(d, 3, mem, unchecked(sp + 280));
L_00AB4CCC:
    VCopyD(d, 2, 21);
L_00AB4CD0:
    VStoreDB(d, 30, mem, unchecked(sp + 112));
L_00AB4CD4:
    VStoreDB(d, 31, mem, unchecked(sp + 120));
L_00AB4CD8:
    VCopyD(d, 3, 13);
L_00AB4CDC:
    VCopyD(d, 30, 24);
L_00AB4CE0:
    VCopyD(d, 31, 16);
L_00AB4CE4:
    VTrn(d, 3, 0);
L_00AB4CE8:
    VCopyD(d, 16, 25);
L_00AB4CEC:
    VCopyD(d, 24, 20);
L_00AB4CF0:
    VTrn(d, 4, 9);
L_00AB4CF4:
    VCopyD(d, 25, 12);
L_00AB4CF8:
    VStore4B(d, 30, 31, mem, r0);
L_00AB4CFC:
    r0 = unchecked(sl + 240);
L_00AB4D00:
    SV(mem, sp, r0);
L_00AB4D04:
    sl = unchecked(sl + 256);
L_00AB4D08:
    VStore4B(d, 24, 25, mem, fp);
L_00AB4D0C:
    VCopyD(d, 20, 6);
L_00AB4D10:
    r0 = IV(mem, unchecked(sp + 352));
L_00AB4D14:
    VCopyD(d, 21, 8);
L_00AB4D18:
    VStore4B(d, 16, 17, mem, r8);
L_00AB4D1C:
    VCopyD(d, 6, 7);
L_00AB4D20:
    VCopyD(d, 8, 0);
L_00AB4D24:
    cmpA = sl; cmpB = r0;
L_00AB4D28:
    VStore4B(d, 2, 3, mem, r7);
L_00AB4D2C:
    VCopyD(d, 7, 9);
L_00AB4D30:
    r7 = IV(mem, unchecked(sp + 320));
L_00AB4D34:
    VCopyD(d, 9, 18);
L_00AB4D38:
    VLoadDB(d, 16, mem, unchecked(sp + 16));
L_00AB4D3C:
    VLoadDB(d, 17, mem, unchecked(sp + 24));
L_00AB4D40:
    VCopyD(d, 18, 1);
L_00AB4D44:
    VStore4B(d, 26, 27, mem, r7);
L_00AB4D48:
    r7 = IV(mem, unchecked(sp + 208));
L_00AB4D4C:
    VLoadDB(d, 14, mem, unchecked(sp + 64));
L_00AB4D50:
    VLoadDB(d, 15, mem, unchecked(sp + 72));
L_00AB4D54:
    VStore4B(d, 28, 29, mem, r7);
L_00AB4D58:
    VStore4B(d, 16, 17, mem, r6);
L_00AB4D5C:
    r0 = IV(mem, sp);
L_00AB4D60:
    VStore4B(d, 22, 23, mem, r5);
L_00AB4D64:
    VStore4B(d, 20, 21, mem, r4);
L_00AB4D68:
    VStore4B(d, 8, 9, mem, lr);
L_00AB4D6C:
    VStore4B(d, 6, 7, mem, ip);
L_00AB4D70:
    VStore4B(d, 18, 19, mem, r0);
L_00AB4D74:
    if (cmpA != cmpB) goto L_00AB47E0;
L_00AB4D78:
    sp = unchecked(sp + 404);
L_00AB4D7C:
L_00AB4D80:
    return;
L_00AB4D84:
    VLoadDB(d, 16, mem, unchecked(sp + 80));
L_00AB4D88:
    VLoadDB(d, 17, mem, unchecked(sp + 88));
L_00AB4D8C:
    sl = r3;
L_00AB4D90:
    r6 = 0;
L_00AB4D94:
    VStoreDB(d, 16, mem, unchecked(sp + 16));
L_00AB4D98:
    VStoreDB(d, 17, mem, unchecked(sp + 24));
L_00AB4D9C:
    VLoadDB(d, 16, mem, unchecked(sp + 96));
L_00AB4DA0:
    VLoadDB(d, 17, mem, unchecked(sp + 104));
L_00AB4DA4:
    VStoreDB(d, 16, mem, unchecked(sp + 48));
L_00AB4DA8:
    VStoreDB(d, 17, mem, unchecked(sp + 56));
L_00AB4DAC:
    VLoadDB(d, 16, mem, unchecked(sp + 112));
L_00AB4DB0:
    VLoadDB(d, 17, mem, unchecked(sp + 120));
L_00AB4DB4:
    VStore4B(d, 16, 17, mem, sp);
L_00AB4DB8:
    VLoadDB(d, 16, mem, unchecked(sp + 128));
L_00AB4DBC:
    VLoadDB(d, 17, mem, unchecked(sp + 136));
L_00AB4DC0:
    VStoreDB(d, 16, mem, unchecked(sp + 64));
L_00AB4DC4:
    VStoreDB(d, 17, mem, unchecked(sp + 72));
L_00AB4DC8:
    VLoadDB(d, 16, mem, unchecked(sp + 352));
L_00AB4DCC:
    VLoadDB(d, 17, mem, unchecked(sp + 360));
L_00AB4DD0:
    VStoreDB(d, 16, mem, unchecked(sp + 288));
L_00AB4DD4:
    VStoreDB(d, 17, mem, unchecked(sp + 296));
L_00AB4DD8:
    VLoadDB(d, 16, mem, unchecked(sp + 176));
L_00AB4DDC:
    VLoadDB(d, 17, mem, unchecked(sp + 184));
L_00AB4DE0:
    VStoreDB(d, 16, mem, unchecked(sp + 112));
L_00AB4DE4:
    VStoreDB(d, 17, mem, unchecked(sp + 120));
L_00AB4DE8:
    VLoadDB(d, 16, mem, unchecked(sp + 192));
L_00AB4DEC:
    VLoadDB(d, 17, mem, unchecked(sp + 200));
L_00AB4DF0:
    VStoreDB(d, 14, mem, unchecked(sp + 80));
L_00AB4DF4:
    VStoreDB(d, 15, mem, unchecked(sp + 88));
L_00AB4DF8:
    VStoreDB(d, 30, mem, unchecked(sp + 96));
L_00AB4DFC:
    VStoreDB(d, 31, mem, unchecked(sp + 104));
L_00AB4E00:
    VStoreDB(d, 16, mem, unchecked(sp + 128));
L_00AB4E04:
    VStoreDB(d, 17, mem, unchecked(sp + 136));
L_00AB4E08:
    goto L_00AB474C;
L_00AB4E0C:
    // skip .word 0x0058c298
L_00AB4E10:
    // skip .word 0xffffffcc
L_00AB4E14:
    // skip .word 0xffffffbc
L_00AB4E18:
    // skip .word 0xffffffc4
L_00AB4E1C:
    // skip .word 0xffffffc0
L_00AB4E20:
    // skip .word 0xffffffc8
L_00AB4E24:
    // skip .word 0xffffffd0
L_00AB4E28:
    // skip .word 0xffffffd4
L_00AB4E2C:
    // skip .word 0xffffffd8
L_00AB4E30:
    // skip .word 0xffffffa8
    ;
}

    // ---- X5-I6/I7 / Appendix C: stages + terminal butterfly 0x00AB4ED0..0x00AB5A1C (generated) ----

private static void ImdctStagesTerminalCore(float[] mem, int points, int shift, int gotBaseBytes, int log2n, int spBytes, int workSlotBytes)
{
    int r0, r1, r2, r3, r4, r5, r6, r7, r8, sb, sl, fp, ip, sp, lr;
    int cmpA = 0, cmpB = 0;
    var d = new float[64];
    r0 = 0; r1 = points; r2 = shift; r3 = 0; r4 = 0; r5 = 0; r6 = 0; r7 = 0;
    r8 = 0; sb = 0; sl = 0; fp = 0; ip = 0; sp = 0; lr = 0;
    r5 = log2n;
    r8 = workSlotBytes;
    sp = spBytes;
    SV(mem, unchecked(sp + 0x28), unchecked(gotBaseBytes + 0x5C));
    SV(mem, unchecked(sp + 0x64), points);
    SV(mem, unchecked(sp + 0x88), 0);
    SV(mem, unchecked(sp + 0x8C), shift);
L_00AB4ED0:
    cmpA = r5; cmpB = 8;
L_00AB4ED4:
    r3 = unchecked(15 - r5);
L_00AB4ED8:
    if (cmpA <= cmpB) goto L_00AB5248;
L_00AB4EDC:
    r1 = 0;
L_00AB4EE0:
    r2 = unchecked((int)0xFFFFFFA8u);
L_00AB4EE4:
    SV(mem, unchecked(sp + 36), r1);
L_00AB4EE8:
    r3 = unchecked(r3 << 4);
L_00AB4EEC:
    SV(mem, unchecked(sp + 32), r1);
L_00AB4EF0:
    r0 = unchecked(r5 - 8);
L_00AB4EF4:
    r1 = IV(mem, unchecked(sp + 40));
L_00AB4EF8:
    SV(mem, unchecked(sp + 44), r0);
L_00AB4EFC:
    r0 = unchecked(r3 - 16);
L_00AB4F00:
    SV(mem, unchecked(sp + 48), r0);
L_00AB4F04:
    r2 = IV(mem, unchecked(r1 + r2));
L_00AB4F08:
    r7 = unchecked(r3 + r2);
L_00AB4F0C:
    r3 = IV(mem, unchecked(sp + 32));
L_00AB4F10:
    r2 = 1;
L_00AB4F14:
    fp = unchecked(r2 << r3);
L_00AB4F18:
    cmpA = fp; cmpB = 0;
L_00AB4F1C:
    if (cmpA <= cmpB) goto L_00AB5220;
L_00AB4F20:
    r2 = IV(mem, unchecked(sp + 100));
L_00AB4F24:
    sb = IV(mem, unchecked(sp + 40));
L_00AB4F28:
    ip = unchecked((int)0xFFFFFFC8u);
L_00AB4F2C:
    r3 = unchecked(r2 >> r3);
L_00AB4F30:
    r2 = unchecked((int)0xFFFFFFC4u);
L_00AB4F34:
    r0 = unchecked(r3 + 15);
L_00AB4F38:
    r5 = unchecked((int)0xFFFFFFC0u);
L_00AB4F3C:
    r1 = unchecked(r3 + unchecked(r3 >> 31));
L_00AB4F40:
    cmpA = r3; cmpB = 0;
L_00AB4F44:
    lr = unchecked((int)0xFFFFFFBCu);
L_00AB4F48:
    r6 = unchecked(r3 - unchecked((int)3221225488u));
L_00AB4F4C:
    r1 = unchecked(r1 >> 1);
L_00AB4F50:
    sl = IV(mem, unchecked(sb + r2));
L_00AB4F54:
    r4 = unchecked(r1 - unchecked((int)3221225488u));
L_00AB4F58:
    r1 = sb;
L_00AB4F5C:
    r2 = IV(mem, unchecked(r1 + ip));
L_00AB4F60:
    if (cmpA < cmpB) r3 = r0;
L_00AB4F64:
    r1 = IV(mem, unchecked(sp + 48));
L_00AB4F68:
    r3 = unchecked(r3 & ~15);
L_00AB4F6C:
    r0 = IV(mem, unchecked(sp + 36));
L_00AB4F70:
    ip = 0;
L_00AB4F74:
    lr = IV(mem, unchecked(sb + lr));
L_00AB4F78:
    r4 = unchecked(r4 << 2);
L_00AB4F7C:
    sb = IV(mem, unchecked(sb + r5));
L_00AB4F80:
    r5 = unchecked(r1 + r0);
L_00AB4F84:
    SV(mem, unchecked(sp + 28), r3);
L_00AB4F88:
    sl = unchecked(r5 + sl);
L_00AB4F8C:
    r3 = unchecked(r5 + lr);
L_00AB4F90:
    sb = unchecked(r5 + sb);
L_00AB4F94:
    r5 = unchecked(r5 + r2);
L_00AB4F98:
    r6 = unchecked(r6 << 2);
L_00AB4F9C:
    lr = ip;
L_00AB4FA0:
    r1 = unchecked(r6 + 64);
L_00AB4FA4:
    SV(mem, unchecked(sp + 24), r3);
L_00AB4FA8:
    SV(mem, unchecked(sp + 20), r1);
L_00AB4FAC:
    r1 = IV(mem, r8);
L_00AB4FB0:
    r0 = IV(mem, unchecked(sp + 28));
L_00AB4FB4:
    r1 = unchecked(r1 + ip);
L_00AB4FB8:
    VLoad4B(d, 28, 29, mem, r7);
L_00AB4FBC:
    r0 = unchecked(r1 + r0);
L_00AB4FC0:
    SV(mem, sp, r0);
L_00AB4FC4:
    r0 = IV(mem, unchecked(sp + 24));
L_00AB4FC8:
    r2 = unchecked(r1 + r6);
L_00AB4FCC:
    r3 = unchecked(r1 + r4);
L_00AB4FD0:
    VBroadcast(d, 1, mem, sl);
L_00AB4FD4:
    VBroadcast(d, 2, mem, r0);
L_00AB4FD8:
    r0 = IV(mem, sp);
L_00AB4FDC:
    VBroadcast(d, 13, mem, sb);
L_00AB4FE0:
    VBroadcast(d, 15, mem, r5);
L_00AB4FE4:
    goto L_00AB5014;
L_00AB4FE8:
    // skip subseq fp, r8, r4, asr #8
L_00AB4FEC:
    // skip .word 0xffffffdc
L_00AB4FF0:
    // skip .word 0xffffffa8
L_00AB4FF4:
    // skip .word 0xffffffc8
L_00AB4FF8:
    // skip .word 0xffffffc4
L_00AB4FFC:
    // skip .word 0xffffffc0
L_00AB5000:
    // skip .word 0xffffffbc
L_00AB5004:
    // skip svclo #0x6c835e
L_00AB5008:
    // skip mcrlo p15, #6, lr, c3, c5, #0
L_00AB500C:
    VCopy(d, 13, 10);
L_00AB5010:
    VCopy(d, 2, 12);
L_00AB5014:
    VLoadDB(d, 6, mem, unchecked(r2 + 16));
L_00AB5018:
    VLoadDB(d, 7, mem, unchecked(r2 + 24));
L_00AB501C:
    VMul(d, 10, 14, 13);
L_00AB5020:
    r3 = unchecked(r3 - 64);
L_00AB5024:
    r2 = unchecked(r2 - 64);
L_00AB5028:
    VLoadDB(d, 18, mem, unchecked(r2 + 112));
L_00AB502C:
    VLoadDB(d, 19, mem, unchecked(r2 + 120));
L_00AB5030:
    VMul(d, 12, 14, 2);
L_00AB5034:
    VLoadDB(d, 16, mem, unchecked(r2 + 96));
L_00AB5038:
    VLoadDB(d, 17, mem, unchecked(r2 + 104));
L_00AB503C:
    VAdd(d, 0, 8, 9);
L_00AB5040:
    VLoadDB(d, 22, mem, unchecked(r2 + 64));
L_00AB5044:
    VLoadDB(d, 23, mem, unchecked(r2 + 72));
L_00AB5048:
    VAdd(d, 4, 11, 3);
L_00AB504C:
    VSub(d, 11, 11, 3);
L_00AB5050:
    VStoreDB(d, 0, mem, unchecked(r2 + 96));
L_00AB5054:
    VStoreDB(d, 1, mem, unchecked(r2 + 104));
L_00AB5058:
    VSub(d, 3, 9, 8);
L_00AB505C:
    VStoreDB(d, 8, mem, unchecked(r2 + 64));
L_00AB5060:
    VStoreDB(d, 9, mem, unchecked(r2 + 72));
L_00AB5064:
    VSub(d, 12, 12, 1);
L_00AB5068:
    VLoadDB(d, 16, mem, unchecked(r3 + 64));
L_00AB506C:
    VLoadDB(d, 17, mem, unchecked(r3 + 72));
L_00AB5070:
    VSub(d, 10, 10, 15);
L_00AB5074:
    VLoadDB(d, 18, mem, unchecked(r3 + 80));
L_00AB5078:
    VLoadDB(d, 19, mem, unchecked(r3 + 88));
L_00AB507C:
    VCopy(d, 1, 2);
L_00AB5080:
    VAdd(d, 0, 9, 8);
L_00AB5084:
    VSub(d, 9, 9, 8);
L_00AB5088:
    VMul(d, 15, 3, 10);
L_00AB508C:
    VStoreDB(d, 0, mem, unchecked(r2 + 80));
L_00AB5090:
    VStoreDB(d, 1, mem, unchecked(r2 + 88));
L_00AB5094:
    VMul(d, 4, 11, 10);
L_00AB5098:
    VLoadDB(d, 10, mem, unchecked(r3 + 112));
L_00AB509C:
    VLoadDB(d, 11, mem, unchecked(r3 + 120));
L_00AB50A0:
    VMul(d, 8, 9, 10);
L_00AB50A4:
    VLoadDB(d, 12, mem, unchecked(r3 + 96));
L_00AB50A8:
    VLoadDB(d, 13, mem, unchecked(r3 + 104));
L_00AB50AC:
    VMul(d, 0, 9, 12);
L_00AB50B0:
    VSub(d, 9, 5, 6);
L_00AB50B4:
    VMul(d, 3, 3, 12);
L_00AB50B8:
    VMul(d, 11, 11, 12);
L_00AB50BC:
    VMul(d, 7, 9, 10);
L_00AB50C0:
    VMul(d, 9, 9, 12);
L_00AB50C4:
    VAdd(d, 5, 5, 6);
L_00AB50C8:
    VAdd(d, 3, 3, 4);
L_00AB50CC:
    VSub(d, 9, 9, 8);
L_00AB50D0:
    VAdd(d, 0, 0, 7);
L_00AB50D4:
    VStoreDB(d, 10, mem, unchecked(r2 + 112));
L_00AB50D8:
    VStoreDB(d, 11, mem, unchecked(r2 + 120));
L_00AB50DC:
    VSub(d, 8, 11, 15);
L_00AB50E0:
    VStoreDB(d, 6, mem, unchecked(r3 + 64));
L_00AB50E4:
    VStoreDB(d, 7, mem, unchecked(r3 + 72));
L_00AB50E8:
    VStoreDB(d, 18, mem, unchecked(r3 + 112));
L_00AB50EC:
    VStoreDB(d, 19, mem, unchecked(r3 + 120));
L_00AB50F0:
    VCopy(d, 15, 13);
L_00AB50F4:
    VStoreDB(d, 0, mem, unchecked(r3 + 80));
L_00AB50F8:
    VStoreDB(d, 1, mem, unchecked(r3 + 88));
L_00AB50FC:
    VStoreDB(d, 16, mem, unchecked(r3 + 96));
L_00AB5100:
    VStoreDB(d, 17, mem, unchecked(r3 + 104));
L_00AB5104:
    cmpA = r0; cmpB = r3;
L_00AB5108:
    if ((uint)cmpA <= (uint)cmpB) goto L_00AB500C;
L_00AB510C:
    VLoadDB(d, 30, mem, unchecked(r2 + 16));
L_00AB5110:
    VLoadDB(d, 31, mem, unchecked(r2 + 24));
L_00AB5114:
    VMul(d, 3, 14, 10);
L_00AB5118:
    r3 = unchecked(r3 - 64);
L_00AB511C:
    r2 = unchecked(r2 - 64);
L_00AB5120:
    VLoadDB(d, 18, mem, unchecked(r2 + 96));
L_00AB5124:
    VLoadDB(d, 19, mem, unchecked(r2 + 104));
L_00AB5128:
    VMul(d, 1, 14, 12);
L_00AB512C:
    VLoadDB(d, 22, mem, unchecked(r2 + 64));
L_00AB5130:
    VLoadDB(d, 23, mem, unchecked(r2 + 72));
L_00AB5134:
    VAdd(d, 0, 11, 15);
L_00AB5138:
    VLoadDB(d, 16, mem, unchecked(r2 + 112));
L_00AB513C:
    VLoadDB(d, 17, mem, unchecked(r2 + 120));
L_00AB5140:
    VSub(d, 11, 11, 15);
L_00AB5144:
    VAdd(d, 15, 9, 8);
L_00AB5148:
    VStoreDB(d, 0, mem, unchecked(r2 + 64));
L_00AB514C:
    VStoreDB(d, 1, mem, unchecked(r2 + 72));
L_00AB5150:
    VSub(d, 3, 3, 13);
L_00AB5154:
    VSub(d, 8, 9, 8);
L_00AB5158:
    VStoreDB(d, 30, mem, unchecked(r2 + 96));
L_00AB515C:
    VStoreDB(d, 31, mem, unchecked(r2 + 104));
L_00AB5160:
    VCopy(d, 13, 10);
L_00AB5164:
    VSub(d, 15, 1, 2);
L_00AB5168:
    VLoadDB(d, 20, mem, unchecked(r3 + 80));
L_00AB516C:
    VLoadDB(d, 21, mem, unchecked(r3 + 88));
L_00AB5170:
    VMul(d, 1, 11, 3);
L_00AB5174:
    VLoadDB(d, 18, mem, unchecked(r3 + 64));
L_00AB5178:
    VLoadDB(d, 19, mem, unchecked(r3 + 72));
L_00AB517C:
    VAdd(d, 2, 10, 9);
L_00AB5180:
    VSub(d, 9, 9, 10);
L_00AB5184:
    VMul(d, 5, 8, 15);
L_00AB5188:
    VStoreDB(d, 4, mem, unchecked(r2 + 80));
L_00AB518C:
    VStoreDB(d, 5, mem, unchecked(r2 + 88));
L_00AB5190:
    VMul(d, 11, 11, 15);
L_00AB5194:
    VLoadDB(d, 0, mem, unchecked(r3 + 96));
L_00AB5198:
    VLoadDB(d, 1, mem, unchecked(r3 + 104));
L_00AB519C:
    VMul(d, 10, 9, 3);
L_00AB51A0:
    VLoadDB(d, 4, mem, unchecked(r3 + 112));
L_00AB51A4:
    VLoadDB(d, 5, mem, unchecked(r3 + 120));
L_00AB51A8:
    VMul(d, 4, 9, 15);
L_00AB51AC:
    VSub(d, 9, 2, 0);
L_00AB51B0:
    VMul(d, 8, 8, 3);
L_00AB51B4:
    VAdd(d, 2, 2, 0);
L_00AB51B8:
    VMul(d, 0, 9, 3);
L_00AB51BC:
    VMul(d, 9, 9, 15);
L_00AB51C0:
    VSub(d, 1, 1, 5);
L_00AB51C4:
    VStoreDB(d, 4, mem, unchecked(r2 + 112));
L_00AB51C8:
    VStoreDB(d, 5, mem, unchecked(r2 + 120));
L_00AB51CC:
    VSub(d, 4, 0, 4);
L_00AB51D0:
    VAdd(d, 9, 10, 9);
L_00AB51D4:
    VAdd(d, 11, 8, 11);
L_00AB51D8:
    VStoreDB(d, 2, mem, unchecked(r3 + 64));
L_00AB51DC:
    VStoreDB(d, 3, mem, unchecked(r3 + 72));
L_00AB51E0:
    VStoreDB(d, 8, mem, unchecked(r3 + 80));
L_00AB51E4:
    VStoreDB(d, 9, mem, unchecked(r3 + 88));
L_00AB51E8:
    VCopy(d, 2, 12);
L_00AB51EC:
    VCopy(d, 10, 3);
L_00AB51F0:
    VStoreDB(d, 18, mem, unchecked(r3 + 112));
L_00AB51F4:
    VStoreDB(d, 19, mem, unchecked(r3 + 120));
L_00AB51F8:
    VCopy(d, 12, 15);
L_00AB51FC:
    VStoreDB(d, 22, mem, unchecked(r3 + 96));
L_00AB5200:
    VStoreDB(d, 23, mem, unchecked(r3 + 104));
L_00AB5204:
    cmpA = r1; cmpB = r3;
L_00AB5208:
    if ((uint)cmpA <= (uint)cmpB) goto L_00AB510C;
L_00AB520C:
    lr = unchecked(lr + 1);
L_00AB5210:
    r3 = IV(mem, unchecked(sp + 20));
L_00AB5214:
    cmpA = lr; cmpB = fp;
L_00AB5218:
    ip = unchecked(ip + r3);
L_00AB521C:
    if (cmpA != cmpB) goto L_00AB4FAC;
L_00AB5220:
    r3 = IV(mem, unchecked(sp + 32));
L_00AB5224:
    r7 = unchecked(r7 + 16);
L_00AB5228:
    r2 = IV(mem, unchecked(sp + 44));
L_00AB522C:
    r3 = unchecked(r3 + 1);
L_00AB5230:
    SV(mem, unchecked(sp + 32), r3);
L_00AB5234:
    cmpA = r3; cmpB = r2;
L_00AB5238:
    r3 = IV(mem, unchecked(sp + 36));
L_00AB523C:
    r3 = unchecked(r3 + 16);
L_00AB5240:
    SV(mem, unchecked(sp + 36), r3);
L_00AB5244:
    if (cmpA != cmpB) goto L_00AB4F0C;
L_00AB5248:
    r3 = IV(mem, unchecked(sp + 100));
L_00AB524C:
    cmpA = r3; cmpB = 0;
L_00AB5250:
    if (cmpA > cmpB) SV(mem, unchecked(sp + 132), r8);
L_00AB5254:
    if (cmpA > cmpB) r3 = 0;
L_00AB5258:
    if (cmpA > cmpB) SV(mem, unchecked(sp + 80), r3);
L_00AB525C:
    if (cmpA <= cmpB) return;
L_00AB5260:
    r3 = IV(mem, unchecked(sp + 132));
L_00AB5264:
    r2 = IV(mem, unchecked(sp + 80));
L_00AB5268:
    d[15] = BitConverter.UInt32BitsToSingle(0x3EC3EF15u);
L_00AB526C:
    r3 = IV(mem, r3);
L_00AB5270:
    r3 = unchecked(r3 + unchecked(r2 << 2));
L_00AB5274:
    r2 = unchecked(r2 + 128);
L_00AB5278:
    r1 = unchecked(r3 + 272);
L_00AB527C:
    SV(mem, unchecked(sp + 80), r2);
L_00AB5280:
    r2 = unchecked(r3 + 256);
L_00AB5284:
    d[7] = BitConverter.UInt32BitsToSingle(0x3F6C835Eu);
L_00AB5288:
    lr = r1;
L_00AB528C:
    r1 = unchecked(r3 + 288);
L_00AB5290:
    r7 = r2;
L_00AB5294:
    sl = unchecked(r3 + 128);
L_00AB5298:
    r4 = r1;
L_00AB529C:
    r1 = unchecked(r3 + 304);
L_00AB52A0:
    VLoad4B(d, 16, 17, mem, r2);
L_00AB52A4:
    r2 = unchecked(r3 + 64);
L_00AB52A8:
    r5 = r1;
L_00AB52AC:
    r1 = unchecked(r3 + 16);
L_00AB52B0:
    VLoad4B(d, 22, 23, mem, lr);
L_00AB52B4:
    VSub(d, 14, 8, 11);
L_00AB52B8:
    r6 = r1;
L_00AB52BC:
    r1 = unchecked(r3 + 32);
L_00AB52C0:
    VLoad4B(d, 0, 1, mem, r5);
L_00AB52C4:
    VAdd(d, 11, 8, 11);
L_00AB52C8:
    SV(mem, unchecked(sp + 84), r2);
L_00AB52CC:
    ip = r1;
L_00AB52D0:
    r1 = unchecked(r3 + 48);
L_00AB52D4:
    VLoad4B(d, 18, 19, mem, r4);
L_00AB52D8:
    r2 = unchecked(r3 + 80);
L_00AB52DC:
    VSub(d, 12, 9, 0);
L_00AB52E0:
    r0 = r1;
L_00AB52E4:
    VDup(d, 8, 7, 1);
L_00AB52E8:
    r1 = unchecked(r3 + 320);
L_00AB52EC:
    VMul(d, 3, 14, 8);
L_00AB52F0:
    VDup(d, 8, 3, 1);
L_00AB52F4:
    VLoad4B(d, 4, 5, mem, ip);
L_00AB52F8:
    VMul(d, 14, 14, 8);
L_00AB52FC:
    SV(mem, sp, r2);
L_00AB5300:
    VLoad4B(d, 10, 11, mem, r3);
L_00AB5304:
    r2 = unchecked(r3 + 96);
L_00AB5308:
    VAdd(d, 9, 9, 0);
L_00AB530C:
    SV(mem, unchecked(sp + 20), r2);
L_00AB5310:
    VLoad4B(d, 20, 21, mem, r6);
L_00AB5314:
    r2 = unchecked(r3 + 112);
L_00AB5318:
    VSub(d, 13, 10, 5);
L_00AB531C:
    SV(mem, unchecked(sp + 24), r2);
L_00AB5320:
    VLoad4B(d, 16, 17, mem, r0);
L_00AB5324:
    r2 = unchecked(r3 + 432);
L_00AB5328:
    VSub(d, 6, 8, 2);
L_00AB532C:
    SV(mem, unchecked(sp + 88), r1);
L_00AB5330:
    VStore4B(d, 22, 23, mem, r7);
L_00AB5334:
    r1 = unchecked(r3 + 336);
L_00AB5338:
    VAdd(d, 10, 10, 5);
L_00AB533C:
    SV(mem, unchecked(sp + 92), r1);
L_00AB5340:
    VDup(d, 11, 3, 1);
L_00AB5344:
    SV(mem, unchecked(sp + 44), r6);
L_00AB5348:
    d[7] = BitConverter.UInt32BitsToSingle(0x3EC3EF15u);
L_00AB534C:
    r6 = unchecked(r3 + 240);
L_00AB5350:
    SV(mem, unchecked(sp + 28), sl);
L_00AB5354:
    sl = unchecked(r3 + 144);
L_00AB5358:
    VMul(d, 4, 12, 11);
L_00AB535C:
    SV(mem, unchecked(sp + 56), sl);
L_00AB5360:
    d[28] = BitConverter.UInt32BitsToSingle(0x3F6C835Eu);
L_00AB5364:
    r1 = unchecked(r3 + 352);
L_00AB5368:
    SV(mem, unchecked(sp + 48), ip);
L_00AB536C:
    ip = unchecked(r3 + 416);
L_00AB5370:
    VAdd(d, 8, 8, 2);
L_00AB5374:
    SV(mem, unchecked(sp + 52), r0);
L_00AB5378:
    VDup(d, 11, 3, 1);
L_00AB537C:
    SV(mem, unchecked(sp + 120), r2);
L_00AB5380:
    d[7] = BitConverter.UInt32BitsToSingle(0x3F6C835Eu);
L_00AB5384:
    r2 = unchecked(r3 + 192);
L_00AB5388:
    SV(mem, unchecked(sp + 40), r6);
L_00AB538C:
    r6 = unchecked(r3 + 448);
L_00AB5390:
    VMul(d, 12, 12, 11);
L_00AB5394:
    SV(mem, unchecked(sp + 124), r2);
L_00AB5398:
    SV(mem, unchecked(sp + 32), r6);
L_00AB539C:
    r2 = unchecked(r3 + 208);
L_00AB53A0:
    VDup(d, 11, 3, 1);
L_00AB53A4:
    r6 = unchecked(r3 + 464);
L_00AB53A8:
    d[7] = BitConverter.UInt32BitsToSingle(0x3EC3EF15u);
L_00AB53AC:
    r8 = r1;
L_00AB53B0:
    SV(mem, unchecked(sp + 128), r2);
L_00AB53B4:
    r1 = unchecked(r3 + 368);
L_00AB53B8:
    VMul(d, 15, 13, 11);
L_00AB53BC:
    SV(mem, unchecked(sp + 36), r6);
L_00AB53C0:
    SV(mem, unchecked(sp + 104), r7);
L_00AB53C4:
    r6 = unchecked(r3 + 480);
L_00AB53C8:
    VDup(d, 11, 3, 1);
L_00AB53CC:
    VSub(d, 3, 3, 4);
L_00AB53D0:
    SV(mem, unchecked(sp + 72), r6);
L_00AB53D4:
    r6 = unchecked(r3 + 496);
L_00AB53D8:
    VMul(d, 1, 6, 11);
L_00AB53DC:
    SV(mem, unchecked(sp + 76), r6);
L_00AB53E0:
    r7 = IV(mem, unchecked(sp + 100));
L_00AB53E4:
    sb = r1;
L_00AB53E8:
    VStore4B(d, 20, 21, mem, lr);
L_00AB53EC:
    VMul(d, 13, 13, 11);
L_00AB53F0:
    r6 = IV(mem, unchecked(sp + 80));
L_00AB53F4:
    r1 = unchecked(r3 + 160);
L_00AB53F8:
    VStore4B(d, 18, 19, mem, r4);
L_00AB53FC:
    VAdd(d, 10, 12, 14);
L_00AB5400:
    cmpA = r7; cmpB = r6;
L_00AB5404:
    r7 = IV(mem, unchecked(sp + 44));
L_00AB5408:
    VAdd(d, 9, 15, 1);
L_00AB540C:
    VDup(d, 11, 14, 0);
L_00AB5410:
    VStore4B(d, 16, 17, mem, r5);
L_00AB5414:
    sl = r1;
L_00AB5418:
    VMul(d, 11, 6, 11);
L_00AB541C:
    SV(mem, unchecked(sp + 108), lr);
L_00AB5420:
    VStore4B(d, 6, 7, mem, r3);
L_00AB5424:
    r1 = unchecked(r3 + 176);
L_00AB5428:
    lr = IV(mem, unchecked(sp + 84));
L_00AB542C:
    r0 = unchecked(r3 + 400);
L_00AB5430:
    r6 = IV(mem, unchecked(sp + 88));
L_00AB5434:
    fp = r1;
L_00AB5438:
    VStore4B(d, 18, 19, mem, r7);
L_00AB543C:
    r1 = unchecked(r3 + 384);
L_00AB5440:
    VSub(d, 8, 11, 13);
L_00AB5444:
    r7 = IV(mem, unchecked(sp + 48));
L_00AB5448:
    SV(mem, unchecked(sp + 112), r4);
L_00AB544C:
    r2 = unchecked(r3 + 224);
L_00AB5450:
    r4 = IV(mem, sp);
L_00AB5454:
    VStore4B(d, 20, 21, mem, r7);
L_00AB5458:
    r7 = IV(mem, unchecked(sp + 52));
L_00AB545C:
    SV(mem, unchecked(sp + 116), r5);
L_00AB5460:
    r5 = IV(mem, unchecked(sp + 20));
L_00AB5464:
    VStore4B(d, 16, 17, mem, r7);
L_00AB5468:
    r7 = IV(mem, unchecked(sp + 92));
L_00AB546C:
    VLoad4B(d, 4, 5, mem, lr);
L_00AB5470:
    lr = IV(mem, unchecked(sp + 24));
L_00AB5474:
    VLoad4B(d, 16, 17, mem, r6);
L_00AB5478:
    SV(mem, unchecked(sp + 96), r7);
L_00AB547C:
    VLoad4B(d, 24, 25, mem, r7);
L_00AB5480:
    VSub(d, 9, 8, 12);
L_00AB5484:
    VLoad4B(d, 20, 21, mem, r8);
L_00AB5488:
    VAdd(d, 12, 8, 12);
L_00AB548C:
    VLoad4B(d, 28, 29, mem, sb);
L_00AB5490:
    VSub(d, 1, 10, 14);
L_00AB5494:
    VLoad4B(d, 30, 31, mem, r5);
L_00AB5498:
    VAdd(d, 14, 10, 14);
L_00AB549C:
    VLoad4B(d, 22, 23, mem, r4);
L_00AB54A0:
    VSub(d, 3, 11, 2);
L_00AB54A4:
    VLoad4B(d, 20, 21, mem, lr);
L_00AB54A8:
    VSub(d, 8, 10, 15);
L_00AB54AC:
    VStore4B(d, 24, 25, mem, r6);
L_00AB54B0:
    VAdd(d, 11, 11, 2);
L_00AB54B4:
    d[3] = BitConverter.UInt32BitsToSingle(0x3EC3EF15u);
L_00AB54B8:
    VAdd(d, 10, 10, 15);
L_00AB54BC:
    VAdd(d, 12, 8, 3);
L_00AB54C0:
    VStore4B(d, 22, 23, mem, r7);
L_00AB54C4:
    VSub(d, 13, 9, 1);
L_00AB54C8:
    VStore4B(d, 28, 29, mem, r8);
L_00AB54CC:
    VSub(d, 8, 8, 3);
L_00AB54D0:
    d[15] = BitConverter.UInt32BitsToSingle(0x3F3504F3u);
L_00AB54D4:
    VAdd(d, 9, 9, 1);
L_00AB54D8:
    VStore4B(d, 20, 21, mem, sb);
L_00AB54DC:
    r7 = IV(mem, unchecked(sp + 84));
L_00AB54E0:
    VDup(d, 11, 7, 1);
L_00AB54E4:
    SV(mem, unchecked(sp + 84), sl);
L_00AB54E8:
    VDup(d, 10, 7, 1);
L_00AB54EC:
    VMul(d, 13, 13, 11);
L_00AB54F0:
    d[15] = BitConverter.UInt32BitsToSingle(0x3EC3EF15u);
L_00AB54F4:
    VMul(d, 9, 9, 11);
L_00AB54F8:
    VMul(d, 8, 8, 10);
L_00AB54FC:
    VStore4B(d, 26, 27, mem, r7);
L_00AB5500:
    VMul(d, 11, 12, 11);
L_00AB5504:
    VDup(d, 12, 7, 1);
L_00AB5508:
    VStore4B(d, 22, 23, mem, r4);
L_00AB550C:
    r4 = IV(mem, unchecked(sp + 56));
L_00AB5510:
    VStore4B(d, 18, 19, mem, r5);
L_00AB5514:
    r5 = IV(mem, unchecked(sp + 28));
L_00AB5518:
    VStore4B(d, 16, 17, mem, lr);
L_00AB551C:
    lr = r1;
L_00AB5520:
    SV(mem, unchecked(sp + 92), lr);
L_00AB5524:
    VLoad4B(d, 18, 19, mem, r0);
L_00AB5528:
    VLoad4B(d, 16, 17, mem, r1);
L_00AB552C:
    VSub(d, 15, 8, 9);
L_00AB5530:
    VLoad4B(d, 12, 13, mem, r5);
L_00AB5534:
    VAdd(d, 8, 8, 9);
L_00AB5538:
    r5 = IV(mem, unchecked(sp + 120));
L_00AB553C:
    VLoad4B(d, 20, 21, mem, r4);
L_00AB5540:
    VSub(d, 14, 10, 6);
L_00AB5544:
    VDup(d, 9, 14, 0);
L_00AB5548:
    VLoad4B(d, 8, 9, mem, r5);
L_00AB554C:
    VMul(d, 2, 15, 9);
L_00AB5550:
    VLoad4B(d, 18, 19, mem, ip);
L_00AB5554:
    VMul(d, 15, 15, 12);
L_00AB5558:
    VSub(d, 12, 9, 4);
L_00AB555C:
    VLoad4B(d, 2, 3, mem, sl);
L_00AB5560:
    VLoad4B(d, 26, 27, mem, fp);
L_00AB5564:
    VAdd(d, 10, 10, 6);
L_00AB5568:
    VStore4B(d, 16, 17, mem, lr);
L_00AB556C:
    VSub(d, 11, 13, 1);
L_00AB5570:
    lr = IV(mem, unchecked(sp + 28));
L_00AB5574:
    VDup(d, 8, 7, 1);
L_00AB5578:
    VAdd(d, 9, 9, 4);
L_00AB557C:
    VMul(d, 3, 14, 8);
L_00AB5580:
    VDup(d, 8, 14, 0);
L_00AB5584:
    VStore4B(d, 20, 21, mem, r0);
L_00AB5588:
    VMul(d, 14, 14, 8);
L_00AB558C:
    VDup(d, 8, 1, 1);
L_00AB5590:
    VMul(d, 5, 12, 8);
L_00AB5594:
    VDup(d, 8, 14, 0);
L_00AB5598:
    d[28] = BitConverter.UInt32BitsToSingle(0x3EC3EF15u);
L_00AB559C:
    VMul(d, 0, 11, 8);
L_00AB55A0:
    VStore4B(d, 18, 19, mem, ip);
L_00AB55A4:
    VMul(d, 12, 12, 8);
L_00AB55A8:
    VDup(d, 8, 14, 0);
L_00AB55AC:
    VMul(d, 11, 11, 8);
L_00AB55B0:
    VAdd(d, 8, 13, 1);
L_00AB55B4:
    VAdd(d, 10, 12, 15);
L_00AB55B8:
    VSub(d, 13, 2, 5);
L_00AB55BC:
    VAdd(d, 9, 3, 0);
L_00AB55C0:
    VStore4B(d, 16, 17, mem, r5);
L_00AB55C4:
    VSub(d, 8, 11, 14);
L_00AB55C8:
    VStore4B(d, 26, 27, mem, lr);
L_00AB55CC:
    lr = IV(mem, unchecked(sp + 72));
L_00AB55D0:
    VStore4B(d, 18, 19, mem, r4);
L_00AB55D4:
    r4 = IV(mem, unchecked(sp + 36));
L_00AB55D8:
    VStore4B(d, 20, 21, mem, sl);
L_00AB55DC:
    sl = IV(mem, unchecked(sp + 32));
L_00AB55E0:
    VStore4B(d, 16, 17, mem, fp);
L_00AB55E4:
    VLoad4B(d, 16, 17, mem, sl);
L_00AB55E8:
    VLoad4B(d, 22, 23, mem, r4);
L_00AB55EC:
    VAdd(d, 3, 8, 11);
L_00AB55F0:
    VLoad4B(d, 18, 19, mem, lr);
L_00AB55F4:
    r1 = IV(mem, unchecked(sp + 76));
L_00AB55F8:
    VSub(d, 11, 8, 11);
L_00AB55FC:
    lr = IV(mem, unchecked(sp + 124));
L_00AB5600:
    r4 = IV(mem, unchecked(sp + 128));
L_00AB5604:
    VLoad4B(d, 20, 21, mem, r1);
L_00AB5608:
    VAdd(d, 14, 9, 10);
L_00AB560C:
    sl = IV(mem, unchecked(sp + 40));
L_00AB5610:
    VLoad4B(d, 16, 17, mem, lr);
L_00AB5614:
    VSub(d, 9, 9, 10);
L_00AB5618:
    SV(mem, unchecked(sp + 88), r2);
L_00AB561C:
    VLoad4B(d, 20, 21, mem, r4);
L_00AB5620:
    VAdd(d, 15, 10, 8);
L_00AB5624:
    VLoad4B(d, 24, 25, mem, r2);
L_00AB5628:
    r2 = IV(mem, unchecked(sp + 32));
L_00AB562C:
    VSub(d, 8, 8, 10);
L_00AB5630:
    VLoad4B(d, 20, 21, mem, sl);
L_00AB5634:
    VAdd(d, 13, 10, 12);
L_00AB5638:
    VStore4B(d, 6, 7, mem, r2);
L_00AB563C:
    r2 = IV(mem, unchecked(sp + 36));
L_00AB5640:
    VSub(d, 10, 10, 12);
L_00AB5644:
    VStore4B(d, 30, 31, mem, r2);
L_00AB5648:
    r2 = IV(mem, unchecked(sp + 72));
L_00AB564C:
    VStore4B(d, 28, 29, mem, r2);
L_00AB5650:
    r2 = IV(mem, unchecked(sp + 88));
L_00AB5654:
    VStore4B(d, 26, 27, mem, r1);
L_00AB5658:
    r1 = IV(mem, unchecked(sp + 56));
L_00AB565C:
    VStore4B(d, 22, 23, mem, lr);
L_00AB5660:
    VStore4B(d, 20, 21, mem, r4);
L_00AB5664:
    VStore4B(d, 18, 19, mem, r2);
L_00AB5668:
    VStore4B(d, 16, 17, mem, sl);
L_00AB566C:
    sl = IV(mem, unchecked(sp + 44));
L_00AB5670:
    VLoad4B(d, 16, 17, mem, r3);
L_00AB5674:
    VLoad4B(d, 24, 25, mem, sl);
L_00AB5678:
    sl = IV(mem, unchecked(sp + 28));
L_00AB567C:
    VSub(d, 9, 8, 12);
L_00AB5680:
    VLoad4B(d, 0, 1, mem, r1);
L_00AB5684:
    VAdd(d, 12, 8, 12);
L_00AB5688:
    VLoad4B(d, 20, 21, mem, sl);
L_00AB568C:
    VSub(d, 8, 10, 0);
L_00AB5690:
    sl = IV(mem, unchecked(sp + 84));
L_00AB5694:
    VAdd(d, 10, 10, 0);
L_00AB5698:
    VLoad4B(d, 0, 1, mem, fp);
L_00AB569C:
    VLoad4B(d, 28, 29, mem, sl);
L_00AB56A0:
    VStoreDB(d, 20, mem, unchecked(sp + 56));
L_00AB56A4:
    VStoreDB(d, 21, mem, unchecked(sp + 64));
L_00AB56A8:
    VSub(d, 10, 14, 0);
L_00AB56AC:
    sl = IV(mem, unchecked(sp + 48));
L_00AB56B0:
    VAdd(d, 0, 14, 0);
L_00AB56B4:
    VLoad4B(d, 6, 7, mem, lr);
L_00AB56B8:
    VLoad4B(d, 22, 23, mem, sl);
L_00AB56BC:
    sl = IV(mem, unchecked(sp + 52));
L_00AB56C0:
    VAdd(d, 14, 8, 10);
L_00AB56C4:
    VLoad4B(d, 8, 9, mem, r4);
L_00AB56C8:
    VSub(d, 8, 8, 10);
L_00AB56CC:
    VLoad4B(d, 14, 15, mem, sl);
L_00AB56D0:
    VSub(d, 15, 11, 7);
L_00AB56D4:
    sl = IV(mem, unchecked(sp + 20));
L_00AB56D8:
    VLoad4B(d, 20, 21, mem, r7);
L_00AB56DC:
    VAdd(d, 11, 11, 7);
L_00AB56E0:
    VLoad4B(d, 4, 5, mem, sl);
L_00AB56E4:
    VNeg(d, 5);
L_00AB56E8:
    sl = IV(mem, unchecked(sp + 24));
L_00AB56EC:
    VLoad4B(d, 14, 15, mem, r2);
L_00AB56F0:
    VSub(d, 6, 9, 15);
L_00AB56F4:
    VLoad4B(d, 2, 3, mem, sl);
L_00AB56F8:
    VAdd(d, 9, 9, 15);
L_00AB56FC:
    sl = IV(mem, sp);
L_00AB5700:
    VNeg(d, 15);
L_00AB5704:
    VLoad4B(d, 26, 27, mem, sl);
L_00AB5708:
    VSub(d, 14, 14, 6);
L_00AB570C:
    sl = IV(mem, unchecked(sp + 40));
L_00AB5710:
    VSub(d, 8, 9, 8);
L_00AB5714:
    VSub(d, 15, 15, 6);
L_00AB5718:
    VSub(d, 6, 2, 1);
L_00AB571C:
    VAdd(d, 1, 2, 1);
L_00AB5720:
    VSub(d, 2, 3, 4);
L_00AB5724:
    VAdd(d, 4, 3, 4);
L_00AB5728:
    VSub(d, 3, 5, 9);
L_00AB572C:
    VLoad4B(d, 18, 19, mem, sl);
L_00AB5730:
    sl = IV(mem, unchecked(sp + 28));
L_00AB5734:
    d[20] = BitConverter.UInt32BitsToSingle(0x3F3504F3u);
L_00AB5738:
    VDup(d, 5, 10, 0);
L_00AB573C:
    VMul(d, 14, 14, 5);
L_00AB5740:
    d[20] = BitConverter.UInt32BitsToSingle(0x3F3504F3u);
L_00AB5744:
    VDup(d, 5, 10, 0);
L_00AB5748:
    VMul(d, 15, 15, 5);
L_00AB574C:
    VAdd(d, 5, 2, 6);
L_00AB5750:
    VSub(d, 2, 2, 6);
L_00AB5754:
    d[24] = BitConverter.UInt32BitsToSingle(0x3F3504F3u);
L_00AB5758:
    VDup(d, 6, 12, 0);
L_00AB575C:
    VMul(d, 3, 3, 6);
L_00AB5760:
    d[24] = BitConverter.UInt32BitsToSingle(0x3F3504F3u);
L_00AB5764:
    VDup(d, 6, 12, 0);
L_00AB5768:
    VMul(d, 8, 8, 6);
L_00AB576C:
    VSub(d, 6, 10, 13);
L_00AB5770:
    VAdd(d, 10, 10, 13);
L_00AB5774:
    VSub(d, 13, 7, 9);
L_00AB5778:
    VAdd(d, 9, 7, 9);
L_00AB577C:
    VAdd(d, 7, 5, 14);
L_00AB5780:
    VSub(d, 5, 5, 14);
L_00AB5784:
    VSub(d, 14, 13, 6);
L_00AB5788:
    VStore4B(d, 14, 15, mem, r3);
L_00AB578C:
    VAdd(d, 13, 13, 6);
L_00AB5790:
    r3 = IV(mem, unchecked(sp + 44));
L_00AB5794:
    VLoadDB(d, 14, mem, unchecked(sp + 56));
L_00AB5798:
    VLoadDB(d, 15, mem, unchecked(sp + 64));
L_00AB579C:
    VSub(d, 6, 4, 10);
L_00AB57A0:
    VAdd(d, 10, 4, 10);
L_00AB57A4:
    VSub(d, 4, 0, 11);
L_00AB57A8:
    VAdd(d, 11, 0, 11);
L_00AB57AC:
    VSub(d, 0, 7, 12);
L_00AB57B0:
    VAdd(d, 12, 7, 12);
L_00AB57B4:
    VSub(d, 7, 9, 1);
L_00AB57B8:
    VAdd(d, 9, 9, 1);
L_00AB57BC:
    VAdd(d, 1, 14, 3);
L_00AB57C0:
    VSub(d, 3, 14, 3);
L_00AB57C4:
    VAdd(d, 14, 2, 8);
L_00AB57C8:
    VStore4B(d, 2, 3, mem, r3);
L_00AB57CC:
    VSub(d, 8, 2, 8);
L_00AB57D0:
    r3 = IV(mem, unchecked(sp + 48));
L_00AB57D4:
    VAdd(d, 2, 13, 15);
L_00AB57D8:
    VStore4B(d, 10, 11, mem, r3);
L_00AB57DC:
    VSub(d, 13, 13, 15);
L_00AB57E0:
    r3 = IV(mem, unchecked(sp + 52));
L_00AB57E4:
    VStore4B(d, 6, 7, mem, r3);
L_00AB57E8:
    VAdd(d, 3, 6, 4);
L_00AB57EC:
    r3 = IV(mem, sp);
L_00AB57F0:
    VStore4B(d, 28, 29, mem, r7);
L_00AB57F4:
    VSub(d, 14, 7, 0);
L_00AB57F8:
    VStore4B(d, 4, 5, mem, r3);
L_00AB57FC:
    VSub(d, 4, 6, 4);
L_00AB5800:
    r3 = IV(mem, unchecked(sp + 20));
L_00AB5804:
    VAdd(d, 0, 7, 0);
L_00AB5808:
    VStore4B(d, 16, 17, mem, r3);
L_00AB580C:
    VSub(d, 8, 9, 11);
L_00AB5810:
    r3 = IV(mem, unchecked(sp + 24));
L_00AB5814:
    VAdd(d, 9, 9, 11);
L_00AB5818:
    VStore4B(d, 26, 27, mem, r3);
L_00AB581C:
    VSub(d, 13, 10, 12);
L_00AB5820:
    VStore4B(d, 6, 7, mem, sl);
L_00AB5824:
    VAdd(d, 10, 10, 12);
L_00AB5828:
    VStore4B(d, 28, 29, mem, r1);
L_00AB582C:
    sl = IV(mem, unchecked(sp + 84));
L_00AB5830:
    r3 = IV(mem, unchecked(sp + 104));
L_00AB5834:
    r1 = IV(mem, unchecked(sp + 92));
L_00AB5838:
    VStore4B(d, 8, 9, mem, sl);
L_00AB583C:
    sl = IV(mem, unchecked(sp + 40));
L_00AB5840:
    VStore4B(d, 0, 1, mem, fp);
L_00AB5844:
    fp = IV(mem, unchecked(sp + 32));
L_00AB5848:
    VStore4B(d, 26, 27, mem, lr);
L_00AB584C:
    lr = IV(mem, unchecked(sp + 108));
L_00AB5850:
    VStore4B(d, 16, 17, mem, r4);
L_00AB5854:
    r4 = IV(mem, unchecked(sp + 112));
L_00AB5858:
    VStore4B(d, 20, 21, mem, r2);
L_00AB585C:
    r7 = IV(mem, unchecked(sp + 96));
L_00AB5860:
    VStore4B(d, 18, 19, mem, sl);
L_00AB5864:
    sl = r5;
L_00AB5868:
    VLoad4B(d, 16, 17, mem, r3);
L_00AB586C:
    VLoad4B(d, 20, 21, mem, r1);
L_00AB5870:
    VLoad4B(d, 8, 9, mem, r0);
L_00AB5874:
    VLoad4B(d, 24, 25, mem, lr);
L_00AB5878:
    VSub(d, 9, 8, 12);
L_00AB587C:
    VLoad4B(d, 14, 15, mem, r5);
L_00AB5880:
    VAdd(d, 12, 8, 12);
L_00AB5884:
    r5 = IV(mem, unchecked(sp + 116));
L_00AB5888:
    VLoad4B(d, 22, 23, mem, ip);
L_00AB588C:
    VSub(d, 8, 10, 4);
L_00AB5890:
    VLoad4B(d, 28, 29, mem, r4);
L_00AB5894:
    VAdd(d, 10, 10, 4);
L_00AB5898:
    VLoad4B(d, 8, 9, mem, r5);
L_00AB589C:
    VSub(d, 15, 14, 4);
L_00AB58A0:
    VLoad4B(d, 6, 7, mem, fp);
L_00AB58A4:
    VAdd(d, 4, 14, 4);
L_00AB58A8:
    fp = IV(mem, unchecked(sp + 36));
L_00AB58AC:
    goto L_00AB58B8;
L_00AB58B0:
    // skip mcrlo p15, #6, lr, c3, c5, #0
L_00AB58B4:
    // skip svclo #0x3504f3
L_00AB58B8:
    VLoad4B(d, 4, 5, mem, r8);
L_00AB58BC:
    VStore4B(d, 20, 21, mem, sp);
L_00AB58C0:
    VSub(d, 10, 11, 7);
L_00AB58C4:
    VSub(d, 6, 9, 15);
L_00AB58C8:
    VLoad4B(d, 0, 1, mem, fp);
L_00AB58CC:
    fp = IV(mem, unchecked(sp + 72));
L_00AB58D0:
    VAdd(d, 9, 9, 15);
L_00AB58D4:
    VLoad4B(d, 2, 3, mem, sb);
L_00AB58D8:
    VAdd(d, 14, 8, 10);
L_00AB58DC:
    VLoad4B(d, 26, 27, mem, r7);
L_00AB58E0:
    VSub(d, 8, 8, 10);
L_00AB58E4:
    VLoad4B(d, 20, 21, mem, r6);
L_00AB58E8:
    VAdd(d, 11, 11, 7);
L_00AB58EC:
    VLoad4B(d, 14, 15, mem, fp);
L_00AB58F0:
    r2 = IV(mem, unchecked(sp + 76));
L_00AB58F4:
    VNeg(d, 15);
L_00AB58F8:
    VNeg(d, 5);
L_00AB58FC:
    VSub(d, 14, 14, 6);
L_00AB5900:
    VSub(d, 15, 15, 6);
L_00AB5904:
    VSub(d, 6, 2, 1);
L_00AB5908:
    VAdd(d, 1, 2, 1);
L_00AB590C:
    VSub(d, 2, 3, 0);
L_00AB5910:
    VAdd(d, 0, 3, 0);
L_00AB5914:
    VSub(d, 3, 5, 9);
L_00AB5918:
    d[20] = BitConverter.UInt32BitsToSingle(0x3F3504F3u);
L_00AB591C:
    VSub(d, 8, 9, 8);
L_00AB5920:
    VLoad4B(d, 18, 19, mem, r2);
L_00AB5924:
    VDup(d, 5, 10, 0);
L_00AB5928:
    VMul(d, 14, 14, 5);
L_00AB592C:
    d[20] = BitConverter.UInt32BitsToSingle(0x3F3504F3u);
L_00AB5930:
    VDup(d, 5, 10, 0);
L_00AB5934:
    VMul(d, 15, 15, 5);
L_00AB5938:
    VAdd(d, 5, 2, 6);
L_00AB593C:
    VSub(d, 2, 2, 6);
L_00AB5940:
    d[24] = BitConverter.UInt32BitsToSingle(0x3F3504F3u);
L_00AB5944:
    VDup(d, 6, 12, 0);
L_00AB5948:
    VMul(d, 3, 3, 6);
L_00AB594C:
    d[24] = BitConverter.UInt32BitsToSingle(0x3F3504F3u);
L_00AB5950:
    VDup(d, 6, 12, 0);
L_00AB5954:
    VMul(d, 8, 8, 6);
L_00AB5958:
    VSub(d, 6, 10, 13);
L_00AB595C:
    VAdd(d, 10, 10, 13);
L_00AB5960:
    VSub(d, 13, 7, 9);
L_00AB5964:
    VAdd(d, 9, 7, 9);
L_00AB5968:
    VAdd(d, 7, 5, 14);
L_00AB596C:
    VSub(d, 5, 5, 14);
L_00AB5970:
    VSub(d, 14, 13, 6);
L_00AB5974:
    VAdd(d, 13, 13, 6);
L_00AB5978:
    VStore4B(d, 14, 15, mem, r3);
L_00AB597C:
    r3 = IV(mem, unchecked(sp + 36));
L_00AB5980:
    VSub(d, 6, 11, 4);
L_00AB5984:
    VAdd(d, 11, 11, 4);
L_00AB5988:
    VLoad4B(d, 8, 9, mem, sp);
L_00AB598C:
    VSub(d, 7, 0, 10);
L_00AB5990:
    VAdd(d, 10, 0, 10);
L_00AB5994:
    VSub(d, 0, 4, 12);
L_00AB5998:
    VAdd(d, 12, 4, 12);
L_00AB599C:
    VSub(d, 4, 9, 1);
L_00AB59A0:
    VAdd(d, 9, 9, 1);
L_00AB59A4:
    VAdd(d, 1, 14, 3);
L_00AB59A8:
    VSub(d, 3, 14, 3);
L_00AB59AC:
    VAdd(d, 14, 2, 8);
L_00AB59B0:
    VSub(d, 8, 2, 8);
L_00AB59B4:
    VStore4B(d, 2, 3, mem, lr);
L_00AB59B8:
    VAdd(d, 2, 13, 15);
L_00AB59BC:
    VStore4B(d, 10, 11, mem, r4);
L_00AB59C0:
    VSub(d, 13, 13, 15);
L_00AB59C4:
    VStore4B(d, 6, 7, mem, r5);
L_00AB59C8:
    VAdd(d, 3, 7, 6);
L_00AB59CC:
    VStore4B(d, 28, 29, mem, r6);
L_00AB59D0:
    VSub(d, 6, 7, 6);
L_00AB59D4:
    VStore4B(d, 4, 5, mem, r7);
L_00AB59D8:
    VSub(d, 14, 4, 0);
L_00AB59DC:
    VStore4B(d, 16, 17, mem, r8);
L_00AB59E0:
    VAdd(d, 0, 4, 0);
L_00AB59E4:
    VStore4B(d, 26, 27, mem, sb);
L_00AB59E8:
    VSub(d, 8, 9, 11);
L_00AB59EC:
    VStore4B(d, 6, 7, mem, r1);
L_00AB59F0:
    r1 = IV(mem, unchecked(sp + 32));
L_00AB59F4:
    VSub(d, 13, 10, 12);
L_00AB59F8:
    VStore4B(d, 28, 29, mem, r0);
L_00AB59FC:
    VAdd(d, 10, 10, 12);
L_00AB5A00:
    VStore4B(d, 12, 13, mem, ip);
L_00AB5A04:
    VAdd(d, 9, 9, 11);
L_00AB5A08:
    VStore4B(d, 0, 1, mem, sl);
L_00AB5A0C:
    VStore4B(d, 26, 27, mem, r1);
L_00AB5A10:
    VStore4B(d, 16, 17, mem, r3);
L_00AB5A14:
    VStore4B(d, 20, 21, mem, fp);
L_00AB5A18:
    VStore4B(d, 18, 19, mem, r2);
L_00AB5A1C:
    if (cmpA > cmpB) goto L_00AB5260;
}
}
