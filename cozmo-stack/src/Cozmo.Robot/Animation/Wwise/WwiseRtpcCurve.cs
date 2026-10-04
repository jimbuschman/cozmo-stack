// fidelity: M6-009
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The engine's RTPC curve evaluation <c>0xA14E28(curve, x, hint, &amp;idx)</c> (C35.1, L5-22..L5-30 of re-analysis/research/20261004-B-M6b-4-live-bodies-5.md as verified), ported in binary32.
///
/// <para>The function is a leaf (no calls): sine, cosine, log10 and 10^y are inline polynomial and bit-trick approximations. <b>Every operation is a binary32 operation, rounded after each step.</b> The VFP
/// <c>vmla</c> / <c>vmls</c> / <c>vnmls</c> of the original are non-fused: the product is rounded to binary32 and then the add or subtract is rounded, so each is written here as two statements (a product into a
/// <c>float</c> local, then the add). The constants are the literal pool <c>0xA15248..0xA152C0</c> as raw bit patterns (<see cref="PoolBits"/>). <c>vcvt.u32.f32</c> truncates toward zero and saturates
/// (negative to 0, 2^32 and above to 0xFFFFFFFF, NaN to 0).</para>
///
/// <para><b>Curve layout.</b> <c>{points*, count, scaling}</c> (<c>[r0]</c>, <c>[r0+4]</c>, <c>[r0+8]</c>), a point is <c>{f32 x, f32 y, u32 interp}</c> (12 bytes), the LEFT point's interp of a segment selects the
/// shape. <b>Segment search</b> (<c>0xA14E28..0xA14E98</c>): <c>i = hint</c>; while <c>i &lt; count - 1</c> (unsigned): <c>x_i &gt;= x</c> (ordered) gives <c>y_i</c> exactly; else <c>x &lt; x_(i+1)</c> (ordered) interpolates
/// segment (i, i+1); else <c>i++</c>. Falling out of the loop gives <c>y_(count-1)</c>. A NaN <c>x</c> fails both compares and ends at <c>y_(count-1)</c>. <c>*idx = i</c> is always written. A count of 0 never reaches the function
/// from a bank (the loader refuses it with 0x1F, L5-06) and the engine would read <c>pts[0]</c> / <c>pts[-1]</c> past the buffer, so it throws <see cref="InvalidOperationException"/> here (the behaviour of the engine is undefined).</para>
///
/// <para><b>Shapes</b> (<c>0xA14F30..0xA14F84</c> dispatch on the left interp: 4 linear, 9 constant, 0..8 the table, &gt;= 10 <c>0xA151FC</c>): <c>t = rnd(rnd(x - x0) / rnd(x1 - x0))</c>. The result then goes through the
/// scaling stage (<c>0xA14E98</c>): 3 is 10^y (<c>y &lt; -37.0</c> gives +0.0, else <c>pow10core</c>), 4 is 10^(0.05 y) (<c>rnd(y * 0.05) &lt; -37.0</c> gives +0.0), 2 is the signed two-term-log dB, anything else leaves y
/// unchanged. Interp &gt;= 10 sets y = 0 first (<c>0xA151FC..0xA15244</c>): scaling 3 enters the pow10 block with y = 0, 4 returns the literal 0x3F7FC105, 2 enters the dB block with +0.0 (result -0.0), the others return +0.0.</para>
/// </summary>
public static class WwiseRtpcCurveA14E28
{
    /// <summary>The literal pool <c>0xA15248..0xA152C0</c>, 31 words, as the engine stores them (C35.1).</summary>
    internal static readonly uint[] PoolBits =
    {
        0xC2140000, 0x4BD49A78, 0x4E7E0000, 0x3EA67F46, 0x3CAA70DE, 0x3F272DDB, 0x42FE0000, 0x3EAAAAAB,   // 0xA15248..0xA15264
        0x3F317218, 0x3EDE5BD9, 0x3D4CCCCD, 0x00000000, 0xB8C08E8F, 0x40490FDB, 0x3B881741, 0x3DAAA5D9,   // 0xA15268..0xA15284
        0x3EFFFFC7, 0x3FC90FDB, 0xB9408E8F, 0x3C081741, 0x3E2AA5D9, 0x3F7FFFC7, 0x39FE3151, 0x3CA0AD34,   // 0xA15288..0xA152A4
        0x3E7D9E76, 0x3A36A2E4, 0xBAA69EB6, 0x3D29EF0F, 0x3EFFF486, 0x3F7FFF90, 0x3F7FC105,               // 0xA152A8..0xA152C0
    };

    private static float P(int index) => BitConverter.UInt32BitsToSingle(PoolBits[index]);

    private static readonly float KMinus37 = P(0);          // 0xA15248 -37.0
    private static readonly float KLog2Ten23 = P(1);        // 0xA1524C 2^23 * log2(10)
    private static readonly float KBias = P(2);             // 0xA15250 0x3F800000 as a float (1065353216.0)
    private static readonly float KPowQ2 = P(3);            // 0xA15254
    private static readonly float KPowQ0 = P(4);            // 0xA15258
    private static readonly float KPowQ1 = P(5);            // 0xA1525C
    private static readonly float K127 = P(6);              // 0xA15260
    private static readonly float KThird = P(7);            // 0xA15264
    private static readonly float KLn2 = P(8);              // 0xA15268
    private static readonly float KLog10E = P(9);           // 0xA1526C
    private static readonly float KFiveHundredth = P(10);   // 0xA15270 0.05f
    private static readonly float KZero = P(11);            // 0xA15274
    private static readonly float KShape3C1 = P(12);        // 0xA15278 (shape 3 half-sine polynomial)
    private static readonly float KPi = P(13);              // 0xA1527C
    private static readonly float KShape3C0 = P(14);        // 0xA15280
    private static readonly float KShape3C2 = P(15);        // 0xA15284
    private static readonly float KShape3C3 = P(16);        // 0xA15288
    private static readonly float KHalfPi = P(17);          // 0xA1528C
    private static readonly float KShape1C1 = P(18);        // 0xA15290 (shape 1 sine polynomial)
    private static readonly float KShape1C0 = P(19);        // 0xA15294
    private static readonly float KShape1C2 = P(20);        // 0xA15298
    private static readonly float KShape1C3 = P(21);        // 0xA1529C
    private static readonly float KShape5C0 = P(22);        // 0xA152A0
    private static readonly float KShape5C1 = P(23);        // 0xA152A4
    private static readonly float KShape5C2 = P(24);        // 0xA152A8
    private static readonly float KShape5C3 = P(25);        // 0xA152AC
    private static readonly float KShape7C1 = P(26);        // 0xA152B0 (shape 7 cosine polynomial)
    private static readonly float KShape7C0 = P(27);        // 0xA152B4
    private static readonly float KShape7C2 = P(28);        // 0xA152B8
    private static readonly float KShape7C3 = P(29);        // 0xA152BC
    private static readonly float KPow10Of0 = P(30);        // 0xA152C0 0x3F7FC105, the literal that interp >= 10 with scaling 4 returns (0xA15238)

    private static float Bits(uint b) => BitConverter.UInt32BitsToSingle(b);

    /// <summary><c>vcvt.u32.f32</c>: round toward zero, saturating; negative to 0, NaN to 0.</summary>
    internal static uint CvtU32(float f)
    {
        if (float.IsNaN(f) || f <= 0f) return 0;
        if (f >= 4294967296f) return uint.MaxValue;
        return (uint)f;
    }

    /// <summary>
    /// <c>0xA14E28(curve, x, hint, &amp;idx)</c> over a bank curve (<see cref="WwiseRtpc.Points"/>, <see cref="WwiseRtpc.Scaling"/>), hint 0 as at all twelve call sites (L5-21). The result is the accumulators' input
    /// (<c>0xA179A0</c>, <c>0xA1784C</c>); the idx output is not read by them.
    /// </summary>
    public static float Evaluate(WwiseRtpc curve, float x)
    {
        ArgumentNullException.ThrowIfNull(curve);
        return Evaluate(curve.Points, curve.Scaling, x, 0, out _);
    }

    /// <summary>The function with all four arguments: the curve (points and the scaling word), <paramref name="x"/>, the start index <paramref name="hint"/> and the idx output.</summary>
    public static float Evaluate(IReadOnlyList<(float From, float To, uint Interp)> points, uint scaling, float x, uint hint, out uint idx)
    {
        ArgumentNullException.ThrowIfNull(points);
        uint count = (uint)points.Count;
        if (count == 0)
            throw new InvalidOperationException("0xA14E28 with a count of 0 reads pts[0] / pts[-1] past the buffer; the loader refuses a count-0 curve (0xA11A68 gate, 0x1F, L5-06) so it is never evaluated");
        uint i = hint;
        uint lr = count - 1;                                       // 0xA14E44 sub lr,r6,#1
        float y;
        while (true)
        {
            if (!(i < lr)) { y = points[(int)(count - 1)].To; break; }                     // 0xA14E78..0xA14E88 blo; 0xA14E8C..0xA14E94
            var p0 = points[(int)i];
            if (p0.From >= x) { y = p0.To; break; }                                        // 0xA14E50..0xA14E58 vcmpe s14,s15; bge 0xA14EC0
            var p1 = points[(int)i + 1];
            if (x < p1.From)                                                               // 0xA14E68..0xA14E70 vcmpe s15,s13; bmi 0xA14F30
            {
                idx = i;                                                                   // 0xA14E9C / 0xA15200
                return Segment(p0, p1, x, scaling);
            }
            i++;                                                                           // 0xA14E74
        }
        idx = i;                                                                           // 0xA14E9C / 0xA14ECC
        return Scale(scaling, y);
    }

    /// <summary>Segment (i, i+1) selected by the left point's interp: the dispatch 0xA14F30..0xA14F84, the shape blocks, then the scaling stage.</summary>
    private static float Segment((float From, float To, uint Interp) p0, (float From, float To, uint Interp) p1, float x, uint scaling)
    {
        float x0 = p0.From, y0 = p0.To, x1 = p1.From, y1 = p1.To;
        uint shape = p0.Interp;                                                            // 0xA14F30 ldr r1,[r4,#8]
        float t, d, prod, y;
        if (shape == 4)                                                                    // 0xA15078..0xA15094
        {
            t = (x - x0) / (x1 - x0);
            d = y1 - y0;
            prod = t * d;
            return Scale(scaling, y0 + prod);
        }
        if (shape == 9) return Scale(scaling, y0);                                         // 0xA14F40 beq 0xA14EC0
        t = (x - x0) / (x1 - x0);                                                          // 0xA14F44..0xA14F54
        if (shape > 8) return Ge10(scaling);                                               // 0xA14F60 b 0xA151FC (interp >= 10 as unsigned)
        float a, b, c, X, X2, p, q, w, u, f;
        switch (shape)
        {
            case 0:                                                                        // 0xA150E4
                a = 1f - t; b = a * a; c = a * b; d = y0 - y1;
                prod = c * d; y = y1 + prod;
                break;
            case 1:                                                                        // 0xA15104
                X = t * KHalfPi; X2 = X * X;
                prod = X2 * KShape1C1; p = KShape1C0 + prod;
                prod = X2 * p; q = prod - KShape1C2;                                       // vnmls
                prod = X2 * q; w = KShape1C3 + prod;
                f = X * w; d = y1 - y0;
                prod = f * d; y = y0 + prod;
                break;
            case 2:                                                                        // 0xA1513C
                a = t - 3f; b = t * a; c = b * 0.5f; d = y0 - y1;
                prod = c * d; y = y0 + prod;
                break;
            case 3:                                                                        // 0xA15098
                if (t <= 0.5f)                                                             // vcmpe; bls 0xA15224 (NaN is not <=)
                {
                    X = t * KPi; X2 = X * X;                                               // 0xA15224, then the block at 0xA1511C
                    prod = X2 * KShape3C1; p = KShape3C0 + prod;
                    prod = X2 * p; q = prod - KShape3C2;
                    prod = X2 * q; w = KShape3C3 + prod;
                    f = X * w;
                }
                else
                {
                    prod = t * KPi; u = KPi - prod;                                        // 0xA150B0 vmls
                    X2 = u * u;
                    prod = X2 * KShape3C1; p = KShape3C0 + prod;
                    prod = X2 * p; q = prod - KShape3C2;
                    prod = X2 * q; w = KShape3C3 + prod;
                    prod = u * w; f = 1f - prod;                                           // 0xA150D0 vmls
                }
                d = y1 - y0;
                prod = f * d; y = y0 + prod;
                break;
            case 5:                                                                        // 0xA1515C
                X = t * KPi; X2 = X * X;
                prod = X2 * KShape5C0; q = prod - KShape5C1;                               // vnmls
                prod = X2 * q; q = KShape5C2 + prod;
                prod = X2 * q; w = KShape5C3 + prod;
                d = y1 - y0;
                prod = w * d; y = y0 + prod;
                break;
            case 6:                                                                        // 0xA15190
                a = t + 1f; b = t * a; c = b * 0.5f; d = y1 - y0;
                prod = c * d; y = y0 + prod;
                break;
            case 7:                                                                        // 0xA151B0
                X = t * KHalfPi; X2 = X * X;
                prod = X2 * KShape7C1; p = KShape7C0 + prod;
                prod = X2 * p; q = prod - KShape7C2;                                       // vnmls
                prod = X2 * q; w = KShape7C3 + prod;
                d = y0 - y1;
                prod = w * d; y = y1 + prod;
                break;
            case 8:                                                                        // 0xA151E8
                b = t * t; c = t * b; d = y1 - y0;
                prod = c * d; y = y0 + prod;
                break;
            default:                                                                       // 4 (0xA14F74 -> 0xA150D8) is taken earlier and 9 too: unreachable
                throw new InvalidOperationException("0xA14F64 table slot " + shape + " is not reachable");
        }
        return Scale(scaling, y);
    }

    /// <summary>Interp code &gt;= 10 (0xA151FC..0xA15244): y = 0, then the scaling stage with its own entries.</summary>
    private static float Ge10(uint scaling)
    {
        if (scaling == 3) return Pow3(0f);                                                 // 0xA15240: s12 = 0 into 0xA14ED4
        if (scaling == 4) return Bits(0x3F7FC105);                                         // 0xA15238: the literal, returned directly
        if (scaling == 2) return Scale2(0f);                                               // 0xA1521C: s12 = 0 into 0xA14F94
        return KZero;                                                                      // 0xA15054
    }

    /// <summary>The scaling stage 0xA14E98..0xA14EB8 (and the repeat at 0xA14EC0..0xA14ED0): 3, 4, 2; every other word returns y unchanged.</summary>
    private static float Scale(uint scaling, float y)
    {
        if (scaling == 3) return Pow3(y);
        if (scaling == 4) return Pow4(y);
        if (scaling == 2) return Scale2(y);
        return y;
    }

    /// <summary>Scaling 3, 0xA14ED4..0xA14F2C: y &lt; -37.0 (binary32, NaN is not less) returns +0.0, else pow10core(y).</summary>
    private static float Pow3(float y)
    {
        if (y < KMinus37) return KZero;                                                    // 0xA15054
        return Pow10Core(y);
    }

    /// <summary>Scaling 4, 0xA1503C..0xA15058: z = rnd(y * 0.05f); z &lt; -37.0 gives +0.0 (the test is bpl, so NaN takes the pow path), else pow10core(z).</summary>
    private static float Pow4(float y)
    {
        float z = y * KFiveHundredth;
        if (z < KMinus37) return KZero;
        return Pow10Core(z);
    }

    /// <summary>
    /// <c>0xA14EE4..0xA14F28</c>: f = 0x4E7E0000 + rnd(y * 2^23 log2 10); u = vcvt.u32.f32(f); m = float with the BITS (u &amp; 0x7FFFFF) + 0x3F800000; k = float with the bits u &amp; 0xFF800000 (both
    /// <c>vmov</c> reinterpretations, C5); a = 0x3CAA70DE + m * 0x3EA67F46; b = 0x3F272DDB + m * a; result b * k. No upper clamp: above y ~ 38.83 the exponent field wraps and the sign bit is set.
    /// </summary>
    private static float Pow10Core(float y)
    {
        float prod = y * KLog2Ten23;
        float f = KBias + prod;
        uint u = CvtU32(f);
        float m = Bits((u & 0x7FFFFFu) + 0x3F800000u);
        float k = Bits(u & 0xFF800000u);
        prod = m * KPowQ2; float a = KPowQ0 + prod;
        prod = m * a; float b = KPowQ1 + prod;
        return b * k;
    }

    /// <summary>
    /// Scaling 2, 0xA14F88..0xA15038 and 0xA15060..0xA15074: sg = +1 with lo = -764.6162109375 for y &lt; 0; else sg = -1 with hi = +764.6162109375 (y &gt;= 0, -0 and NaN). y &lt; -1.0 returns lo; y &gt; 1.0 returns hi.
    /// Otherwise a = 1.0 + rnd(sg * y); e = exponent field of a; m = the mantissa with exponent 0x7F (bit reinterpretation); z = rnd(rnd(m - 1) / rnd(m + 1)); the log is the two-term series
    /// <c>((float(e) - 127) * ln2 + 2z * (1 + z^2/3)) * log10(e)</c>; the result is sg * (20 * that).
    /// </summary>
    private static float Scale2(float y)
    {
        float sg, lo, hi;
        if (y < 0f) { sg = 1f; lo = Bits(0xC43F2770); hi = Bits(0x40C0A8C2); }            // 0xA15060
        else { sg = -1f; lo = Bits(0xC0C0A8C2); hi = Bits(0x443F2770); }                   // 0xA14F94
        if (y < -1f) return lo;                                                            // 0xA14FAC..0xA14FB8 (vmovmi)
        if (y > 1f) return hi;                                                             // 0xA14FC0..0xA14FCC (vmovgt)
        float prod = sg * y;
        float a = 1f + prod;                                                               // 0xA14FD8 vmla
        uint ab = BitConverter.SingleToUInt32Bits(a);
        uint mant = ab & 0x7FFFFFu;                                                        // ubfx #0,#0x17
        uint e = (ab >> 23) & 0xFFu;                                                       // ubfx #0x17,#8
        float ef = (float)(int)e;                                                          // vmov s15,r3; vcvt.f32.s32
        float m = Bits(mant + 0x3F800000u);
        float num = m - 1f;
        float den = m + 1f;
        float z = num / den;
        float z2 = z * z;
        float l = ef - K127;
        prod = z2 * KThird; float k = 1f + prod;                                           // 0xA1501C vmla s14
        l = l * KLn2;
        float zz = z + z;
        prod = zz * k; l = l + prod;                                                       // 0xA15028 vmla
        l = l * KLog10E;
        float v = l * 20f;
        return sg * v;
    }
}
