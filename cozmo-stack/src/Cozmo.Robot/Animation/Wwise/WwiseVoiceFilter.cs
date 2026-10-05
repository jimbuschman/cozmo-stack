// fidelity: M6-011
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The voice filter's LPF/HPF value-to-cutoff map (M6-011, C43.3; LPF 0x00A7A3D8, HPF 0x00A7A4AC).
///
/// The value is a percentage; <c>v &lt; 30</c> (vcmpe, <c>bpl</c>: a NaN is not below 30 and takes the pow path) uses the linear branch
/// <c>7000 + (30-v)*433.333344</c>, otherwise <c>16.7974434 * fastpow2(u32(1065353216 + (100-v)*1042939.94))</c>
/// with the mantissa polynomial <c>0.653043449 + m(0.0208057724 + 0.325189769m)</c>, and the result is capped at
/// <c>0.45 * (float)u32[0x105243C]</c>. The <c>u32</c> conversion is <c>vcvt.u32.f32</c> (round toward zero, saturating, a NaN gives 0).
///
/// The HPF runs the same map on <c>(100-v)</c>. <c>fastpow2</c> is the same exponent/mantissa reconstruction as
/// <c>dBToLin</c> (gapC 1.11): the exponent bits are rebuilt as a power of two and the mantissa, renormalised to
/// <c>[1,2)</c>, goes through the polynomial.
/// </summary>
public static class WwiseVoiceFilterCutoff
{
    /// <summary>The float bit pattern of 1.0 (0x3F800000), the map's base exponent.</summary>
    public const float OneBits = 1065353216f;

    /// <summary>The map's per-unit scale, 1042939.94 (gapE 1.7).</summary>
    public const float Scale = 1042939.94f;

    /// <summary>The map's output scale, 16.7974434 (gapE 1.7).</summary>
    public const float Amplitude = 16.7974434f;

    /// <summary>The linear branch's base, 7000 Hz (gapE 1.7).</summary>
    public const float LinearBase = 7000f;

    /// <summary>The linear branch's per-unit slope, 433.333344 (gapE 1.7).</summary>
    public const float LinearSlope = 433.333344f;

    /// <summary>The value below which the linear branch applies (gapE 1.7).</summary>
    public const float LinearLimit = 30f;

    /// <summary>The cutoff cap fraction of the sample rate (gapE 1.7).</summary>
    public const float NyquistFraction = 0.45f;

    /// <summary>LPF cutoff in Hz for a value 0..100, capped at <c>0.45 * rate</c> (gapE 1.7).</summary>
    public static float LowPass(float value, uint rateHz) => Map(value, rateHz);

    /// <summary>HPF cutoff in Hz: the same map applied to <c>(100-value)</c> (gapE 1.7).</summary>
    public static float HighPass(float value, uint rateHz) => Map(100f - value, rateHz);

    /// <summary>The map itself (gapE 1.7), before the HPF complement. <paramref name="rateHz"/> is <c>u32[0x105243C]</c>.</summary>
    public static float Map(float value, uint rateHz)
    {
        float fc;
        if (value < LinearLimit)
        {
            fc = LinearBase + (LinearLimit - value) * LinearSlope;
        }
        else
        {
            float bits = OneBits + (100f - value) * Scale;
            fc = Amplitude * FastPow2(CvtU32(bits));
        }
        float cap = (float)rateHz * NyquistFraction;
        return fc < cap ? fc : cap;
    }

    /// <summary><c>vcvt.u32.f32</c> (round toward zero, saturating; a NaN gives 0).</summary>
    public static uint CvtU32(float x)
    {
        if (float.IsNaN(x) || x <= 0f) return 0;
        if (x >= 4294967296f) return uint.MaxValue;
        return (uint)x;
    }

    /// <summary>
    /// The fast <c>2^bits</c> used by the cutoff map and <c>dBToLin</c> (gapC 1.11): the exponent bits become a
    /// power of two and the renormalised mantissa goes through the polynomial.
    /// </summary>
    public static float FastPow2(uint bits)
    {
        float scale = BitConverter.Int32BitsToSingle((int)((bits >> 23) << 23));
        float m = BitConverter.Int32BitsToSingle((int)((bits & 0x007FFFFFu) | 0x3F800000u));
        float poly = 0.653043449f + m * (0.0208057724f + 0.325189769f * m);
        return scale * poly;
    }
}

/// <summary>Which band of the voice filter a band record is.</summary>
public enum WwiseVoiceFilterKind
{
    /// <summary>Low pass (0x00A766F0).</summary>
    LowPass,

    /// <summary>High pass (0x00A77480).</summary>
    HighPass,
}

/// <summary>
/// One band of the voice filter node (C43.1..C43.3): the engine's 16-byte band block <c>B</c> <c>{f32 cur @+0, f32 target @+4, u16 steps @+8, s8 countdown @+0xA, dirty @+0xB, first @+0xC, bypass @+0xD}</c> (LPF <c>node+0x170</c> =
/// <c>voice+0x340 / 0x510</c>, HPF <c>node+0x180</c> = <c>voice+0x350 / 0x520</c>), the coefficient block <c>F</c> (LPF <c>node+0x10</c>, HPF <c>node+0xC0</c>: eight NEON matrix rows of four <c>f32</c>, then <c>B0, B1, B2, A1n, A2n</c> at
/// <c>+0x80..+0x90</c>) and the per-channel 16-byte histories <c>{x1, x2, y1, y2}</c> that <c>[F+0xA0]</c> points at. The four parameter ramps of <c>0xA54F1C</c> write this very record (the target setter <see cref="SetTarget"/> is
/// <c>0xA55444</c> / <c>0xA55410</c> / <c>0xA553D8</c> / <c>0xA551C0</c>); the process below is <c>0xA766F0</c> / <c>0xA77480</c>.
/// </summary>
// fidelity: M6-011, M6-022
public sealed class WwiseVoiceFilterBand
{
    /// <summary>The bit pattern of <c>3.14159274f</c> (0xA769D0): pi in float32.</summary>
    internal const uint PiBits = 0x40490FDB;

    /// <summary>The bit pattern of <c>sqrt(2)</c> (0xA769D4).</summary>
    internal const uint Sqrt2Bits = 0x3FB504F3;

    /// <summary>The bit pattern of the bypass threshold <c>0.1f</c> (0xA769E8).</summary>
    internal const uint ThresholdBits = 0x3DCCCCCD;

    private static readonly float Pi = BitConverter.UInt32BitsToSingle(PiBits);
    private static readonly float Sqrt2 = BitConverter.UInt32BitsToSingle(Sqrt2Bits);
    private static readonly float Threshold = BitConverter.UInt32BitsToSingle(ThresholdBits);

    private const double MinNormal = 1.1754943508222875e-38;     // 2^-126

    private static readonly float DefaultNaN = BitConverter.UInt32BitsToSingle(0x7FC00000u);    // NEON: default NaN

    /// <summary>LPF or HPF.</summary>
    public WwiseVoiceFilterKind Kind { get; }

    /// <summary><c>f32 [B+0]</c>: the ramp's current value.</summary>
    public float Current { get; set; }

    /// <summary><c>f32 [B+4]</c>: the target.</summary>
    public float Target { get; set; }

    /// <summary><c>u16 [B+8]</c>: the ramp step (8 = steady).</summary>
    public ushort Steps { get; set; } = 8;

    /// <summary><c>s8 [B+0xA]</c>: the bypass countdown.</summary>
    public sbyte Countdown { get; set; }

    /// <summary><c>byte [B+0xB]</c>: dirty.</summary>
    public byte Dirty { get; set; } = 1;

    /// <summary><c>byte [B+0xC]</c>: first apply.</summary>
    public byte First { get; set; } = 1;

    /// <summary><c>byte [B+0xD]</c>: bypass.</summary>
    public byte Bypass { get; set; } = 1;

    /// <summary>
    /// The coefficient block <c>F</c> as 37 words: rows <c>R0..R7</c> (lane <c>k</c> of row <c>r</c> at <c>4r + k</c>), then <c>B0</c> (32), <c>B1</c> (33), <c>B2</c> (34), <c>A1n</c> (35), <c>A2n</c> (36). The ctor <c>0xA76280</c> state is
    /// the unity matrix (C43.5); every use follows a design.
    /// </summary>
    public float[] F { get; } = new float[37];

    /// <summary>The histories <c>{x1, x2, y1, y2}</c> per channel (<c>[F+0xA0]</c>); null until the init <c>0xA764D4</c> allocated them (or after a failed init).</summary>
    public float[]? History { get; set; }

    /// <summary>Creates a band in the state the init <c>0xA764D4</c> leaves (C43; the ctor <c>0xA76280</c> does not write the band block) with the ctor's coefficient block.</summary>
    public WwiseVoiceFilterBand(WwiseVoiceFilterKind kind)
    {
        Kind = kind;
        SetCtorCoefficients();
    }

    private static float Bf(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    /// <summary>The ctor <c>0xA76280</c>'s coefficient block (C43.5, verifier item 5): R0 = splat(1.0), R1..R3 = 0, R4 = (0,0,-0,0), R5 = (0,-0,0,0), R6 = R7 = (-0,0,0,0), B0 = 1.0, B1 = B2 = 0, A1n = A2n = -0.</summary>
    internal void SetCtorCoefficients()
    {
        Array.Clear(F);
        for (int k = 0; k < 4; k++) F[k] = 1f;
        F[16 + 2] = Bf(0x80000000);
        F[20 + 1] = Bf(0x80000000);
        F[24 + 0] = Bf(0x80000000);
        F[28 + 0] = Bf(0x80000000);
        F[32] = 1f;
        F[35] = Bf(0x80000000);
        F[36] = Bf(0x80000000);
    }

    /// <summary>The state the init <c>0xA764D4</c> writes to a band (<c>0xA76508..0xA76550</c>): cur = target = 0, steps 8, countdown 0, dirty = first = bypass = 1.</summary>
    internal void SetInitState()
    {
        Current = 0f;
        Target = 0f;
        Steps = 8;
        Countdown = 0;
        Dirty = 1;
        First = 1;
        Bypass = 1;
    }

    /// <summary>
    /// The target setter (<c>0xA55444</c> etc.), run by <c>0xA54F1C</c> only when the new target differs from <c>[B+4]</c> (a NaN always differs): <c>[B+0xB] = 1</c>, <c>[B+4] = new</c>, <c>[B+0] = cur + ((oldTarget - cur) * 0.125f) * (float)(s32)u16[B+8]</c>
    /// (vsub, vmul, vcvt.f32.s32, vmla: separately rounded, not fused).
    /// </summary>
    public void SetTarget(float value)
    {
        if (value == Target) return;
        float targetOld = Target;
        Dirty = 1;
        Target = value;
        float step = (targetOld - Current) * 0.125f;
        Current = Current + step * (float)Steps;
    }

    // ------------------------------------------------------------------ the design (A4, A5, A5h)

    private void Design(float v, uint mixRate)
    {
        float fc = Kind == WwiseVoiceFilterKind.LowPass ? WwiseVoiceFilterCutoff.Map(v, mixRate) : WwiseVoiceFilterCutoff.Map(100f - v, mixRate);   // 0xA7A3D8 / 0xA7A4AC
        float a = (fc / (float)mixRate) * Pi;                       // 0xA767A4..0xA767AC: vdiv.f32 then vmul.f32
        float t = WwiseHostMath.Tanf(a);                            // 0xA767B0 bl 0x4AB038 (the phone's libm)
        float b0, b1, a1, a2;
        if (Kind == WwiseVoiceFilterKind.LowPass)
        {
            float c = 1f / t;
            float m = c * Sqrt2;
            float c2 = c * c;
            float den = c2 + (m + 1f);
            b0 = 1f / den;
            b1 = b0 + b0;
            float one_c2 = 1f - c2;
            a1 = -((one_c2 + one_c2) * b0);
            a2 = -(b0 * (c2 + (1f - m)));
        }
        else
        {
            float c2 = t * t;
            float m = t * Sqrt2;
            float s12 = c2 + 1f;
            float den = m + s12;
            b0 = 1f / den;
            b1 = b0 * -2f;
            a1 = b1 * (c2 - 1f);
            a2 = -(b0 * (s12 - m));
        }
        float d = b0 + b0;
        float p1 = b1 + (b0 * a1);
        float q2 = (b0 + (a1 * p1)) + (b0 * a2);
        float q3 = ((b0 * (a1 * a2)) + (a1 * q2)) + (b1 * a2);
        float m1 = b0 + (b1 * a1);
        float m2 = (b1 * a2) + (a1 * m1);
        float m3 = ((b0 * a2) + (a1 * m2)) + (a1 * (b1 * a2));
        float n1 = b0 * a1;
        float n2 = (b0 * a2) + (a1 * n1);
        float n3 = (a1 * (a2 * d)) + (a1 * (a1 * n1));
        float k1 = (a1 * a1) + a2;
        float k2 = (a1 * (a1 * a1)) + (a1 * (a2 + a2));
        float k3 = ((a1 * (a1 * (a2 * 3f))) + (a1 * (a1 * (a1 * a1)))) + (a2 * a2);
        float l1 = a1 * a2;
        float l2 = (a2 * a2) + (a1 * (a1 * a2));
        float l3 = (a1 * (a2 * (a2 + a2))) + (a1 * (a1 * (a1 * a2)));
        float[] f = F;
        float z = 0f;
        // R0 .. R7 (C43.3, report section 5)
        f[0] = b0; f[1] = b0; f[2] = b0; f[3] = b0;
        f[4] = z; f[5] = z; f[6] = z; f[7] = p1;
        f[8] = z; f[9] = z; f[10] = p1; f[11] = q2;
        f[12] = z; f[13] = p1; f[14] = q2; f[15] = q3;
        f[16] = b1; f[17] = m1; f[18] = m2; f[19] = m3;
        f[20] = b0; f[21] = n1; f[22] = n2; f[23] = n3;
        f[24] = a1; f[25] = k1; f[26] = k2; f[27] = k3;
        f[28] = a2; f[29] = l1; f[30] = l2; f[31] = l3;
        f[32] = b0;      // +0x80
        f[33] = b1;      // +0x84
        f[34] = b0;      // +0x88 (B2 = B0)
        f[35] = a1;      // +0x8C
        f[36] = a2;      // +0x90
    }

    // ------------------------------------------------------------------ NEON arithmetic (vmul.f32 / vadd.f32 on q registers: flush-to-zero, default NaN)

    private static float Zin(float x)
    {
        uint b = BitConverter.SingleToUInt32Bits(x);
        return (b & 0x7F800000u) == 0 ? BitConverter.UInt32BitsToSingle(b & 0x80000000u) : x;
    }

    private static float NeonMul(float a, float b)
    {
        double p = (double)Zin(a) * (double)Zin(b);              // exact (24 x 24 bits)
        if (double.IsNaN(p)) return DefaultNaN;
        if (p != 0 && Math.Abs(p) < MinNormal) return p < 0 ? BitConverter.UInt32BitsToSingle(0x80000000u) : 0f;
        return (float)p;
    }

    private static float NeonAdd(float a, float b)
    {
        float r = Zin(a) + Zin(b);
        if (float.IsNaN(r)) return DefaultNaN;
        uint bits = BitConverter.SingleToUInt32Bits(r);
        if ((bits & 0x7F800000u) == 0 && (bits & 0x7FFFFFu) != 0) return BitConverter.UInt32BitsToSingle(bits & 0x80000000u);
        return r;
    }

    // ------------------------------------------------------------------ the kernel K (K1..K3)

    /// <summary>
    /// One channel's samples <c>[start, start + n)</c> of <paramref name="data"/> (K1..K3; <c>0xA76A4C..0xA76D3C</c>, ramp copy <c>0xA76E64..0xA76F7C</c>, HPF <c>0xA777E4..0xA77D04</c>): the history <c>h</c> is
    /// <c>History[hoff..hoff+3]</c>; with <c>(p &amp; 0xF) != 0</c> a scalar head of <c>min((16 - (p &amp; 0xF)) &gt;&gt; 2, n)</c> samples, then <c>(n' &amp; 3)</c> scalar tail samples after the NEON blocks of four.
    /// </summary>
    private void Kernel(float[] data, int start, int n, uint alignBytes, float[] hist, int hoff)
    {
        float[] f = F;
        float x1 = hist[hoff], x2 = hist[hoff + 1], y1 = hist[hoff + 2], y2 = hist[hoff + 3];
        int i = start;
        int remaining = n;
        uint low = (alignBytes + 4u * (uint)start) & 0xF;
        if (low != 0)
        {
            int head = Math.Min((int)((16 - low) >> 2), n);
            for (int k = 0; k < head; k++) Scalar(data, i++, f, ref x1, ref x2, ref y1, ref y2);
            remaining -= head;
        }
        int tail = remaining & 3;
        int blocks = (remaining - tail) >> 2;
        Span<float> output = stackalloc float[4];
        for (int b = 0; b < blocks; b++)
        {
            float a0 = data[i], a1 = data[i + 1], a2 = data[i + 2], a3 = data[i + 3];
            for (int k = 0; k < 4; k++)
            {
                float t13 = NeonAdd(NeonMul(x1, f[16 + k]), NeonMul(x2, f[20 + k]));          // x1*R4 + x2*R5
                float t15 = NeonAdd(NeonMul(y1, f[24 + k]), NeonMul(y2, f[28 + k]));          // y1*R6 + y2*R7
                float t13b = NeonAdd(t13, NeonMul(k switch { 0 => a0, 1 => a1, 2 => a2, _ => a3 }, f[k]));   // + a*R0 (lane-wise)
                float q = NeonAdd(t15, NeonMul(a2, f[4 + k]));                               // + a2*R1
                float r = NeonAdd(NeonMul(a1, f[8 + k]), NeonMul(a0, f[12 + k]));            // a1*R2 + a0*R3
                output[k] = NeonAdd(NeonAdd(t13b, q), r);
            }
            for (int k = 0; k < 4; k++) data[i + k] = output[k];
            x1 = a3; x2 = a2; y1 = output[3]; y2 = output[2];
            i += 4;
        }
        for (int k = 0; k < tail; k++) Scalar(data, i++, f, ref x1, ref x2, ref y1, ref y2);
        hist[hoff] = x1; hist[hoff + 1] = x2; hist[hoff + 2] = y1; hist[hoff + 3] = y2;
    }

    /// <summary>K3 (<c>0xA76C50..0xA76C7C</c>, <c>0xA76CE0..0xA76D0C</c>): <c>y = ((((x2*B2) + x*B0) + x1*B1) + y2*A2n) + y1*A1n</c> as vmul then four vmla (VFP, non-fused, no flush-to-zero).</summary>
    private static void Scalar(float[] data, int index, float[] f, ref float x1, ref float x2, ref float y1, ref float y2)
    {
        float x = data[index];
        float y = x2 * f[34];
        y = y + (x * f[32]);
        y = y + (x1 * f[33]);
        y = y + (y2 * f[36]);
        y = y + (y1 * f[35]);
        data[index] = y;
        x2 = x1; x1 = x; y2 = y1; y1 = y;
    }

    // ------------------------------------------------------------------ the process (A1..A9)

    private float[] RequireHistory(int ch)
    {
        if (History is not { } h || h.Length < 4 * ch)
            throw new InvalidOperationException("M6-011 C43: the filter's history block ([F+0xA0]) is not allocated for " + ch + " channels (the init 0xA764D4 was not run, or failed); the engine dereferences it");
        return h;
    }

    /// <summary>
    /// The band process <c>0xA766F0</c> (LPF) / <c>0xA77480</c> (HPF), rows A1..A9 (C43.2): <paramref name="ch"/> = <c>byte [S+4]</c>, <paramref name="frames"/> = <c>u16 [S+0xE]</c>, <paramref name="stride"/> = <c>u16 [S+0xC]</c>,
    /// <paramref name="alignBytes"/> = the address of <c>[S]</c> modulo 16 (the engine's buffer is 16-byte aligned, P1), <paramref name="mixRate"/> = <c>u32[0x105243C]</c>, <paramref name="chunk"/> = <c>u32[0x105244C]</c>.
    /// </summary>
    public void Process(float[] data, byte ch, ushort frames, ushort stride, uint alignBytes, uint mixRate, uint chunk)
    {
        byte dirty = Dirty;                                          // A1 0xA76700
        byte bypass;
        if (dirty == 0)
        {
            bypass = Bypass;                                         // 0xA76DE0
        }
        else
        {
            Dirty = 0;                                               // 0xA76730
            byte first = First;                                      // 0xA76728
            if (first != 0)                                          // A2
            {
                First = 0;
                Steps = 8;
                Current = Target;                                    // 0xA7673C..0xA76754
                if (Target <= Threshold)                             // vcmpe; bls 0xA76D40 (a NaN target is not bypassed)
                {
                    Bypass = 1;                                      // [B+0xA] is not written
                    bypass = 1;
                }
                else
                {
                    Countdown = 0;                                   // 0xA76770
                    Bypass = 0;                                      // 0xA76774
                    Design(Current, mixRate);                        // A4 at cur
                    bypass = Bypass;                                 // 0xA769C8
                }
            }
            else if (Current <= Threshold && Target <= Threshold)    // A3: cur <= 0.1f (bls) and not target > 0.1f (bhi: a NaN is "higher")
            {
                Bypass = dirty;                                      // 0xA76A10: the dirty byte; [B+0] and [B+0xA] are not written
                Steps = 8;
                bypass = dirty;
            }
            else
            {
                Countdown = 0;                                       // 0xA76A00..0xA76A14
                Bypass = first;
                Steps = 0;
                bypass = first;
            }
        }

        if (bypass != 0)                                             // A6 -> A9
        {
            CopyBypassHistory(data, ch, frames, stride);
            return;
        }
        if (Steps <= 7) RampPath(data, ch, frames, stride, alignBytes, mixRate, chunk);
        else SteadyPath(data, ch, frames, stride, alignBytes);
    }

    /// <summary>A9 (<c>0xA76D40..0xA76DE0</c>; HPF <c>0xA77AC8..0xA77B64</c>): with more than one frame and at least one channel, <c>x1 = y1 = last</c>, <c>x2 = y2 = second-last</c> per channel; the data is not modified.</summary>
    private void CopyBypassHistory(float[] data, byte ch, ushort frames, ushort stride)
    {
        if (frames <= 1) return;                                     // 0xA76D5C cmp r3,#1; bls
        if (ch == 0) return;                                         // 0xA76D68..0xA76D6C
        var h = RequireHistory(ch);
        for (int c = 0; c < ch; c++)
        {
            int p = c * stride + (frames - 2);
            h[4 * c + 2] = data[p + 1];                              // [h+8]
            h[4 * c + 3] = data[p];                                  // [h+0xC]
            h[4 * c] = data[p + 1];                                  // [h+0]
            h[4 * c + 1] = data[p];                                  // [h+4]
        }
    }

    /// <summary>A7 (<c>0xA76DE8..0xA77010</c>, <c>0xA77180..0xA773F8</c>).</summary>
    private void RampPath(float[] data, byte ch, ushort frames, ushort stride, uint alignBytes, uint mixRate, uint chunk)
    {
        float diff = Target - Current;                               // 0xA76DFC, once per call
        if (frames == 0) return;                                     // 0xA76E00
        float[]? h = ch > 0 ? RequireHistory(ch) : null;
        uint pos = 0;
        while (true)
        {
            if (chunk == 0) throw new InvalidOperationException("M6-011 C43 A7: u32[0x105244C] = 0 makes the engine's ramp loop never advance (n = min(frames - pos, 0)); not modelled");
            uint n = Math.Min((uint)frames - pos, chunk);            // 0xA76E2C..0xA76E34, unsigned
            if (Steps <= 7)
            {
                Steps = (ushort)(Steps + 1);                         // 0xA771A0
                float t = (float)Steps * diff;
                float v = Current + (t * 0.125f);                    // vmul then vmla with 0.125
                Design(v, mixRate);
            }
            for (int c = 0; c < ch; c++)
                Kernel(data, c * stride + (int)pos, (int)n, alignBytes, h!, 4 * c);
            pos += n;
            if (frames <= pos) break;                                // 0xA76FF8..0xA76FFC
        }
        if (Steps > 7)                                               // 0xA773FC..0xA77434
        {
            Current = Target;
            if (Target <= Threshold) Countdown = 4;
        }
    }

    /// <summary>A8 (<c>0xA76A40..0xA76BD4</c>) and the countdown tail (<c>0xA76BD8..0xA76C04</c>).</summary>
    private void SteadyPath(float[] data, byte ch, ushort frames, ushort stride, uint alignBytes)
    {
        if (ch > 0)
        {
            var h = RequireHistory(ch);
            for (int c = 0; c < ch; c++)
                Kernel(data, c * stride, frames, alignBytes, h, 4 * c);
        }
        sbyte cd = Countdown;                                        // the tail runs also when ch == 0
        if (cd > 0)
        {
            cd--;
            Countdown = cd;
            if (cd == 0) Bypass = 1;
        }
    }
}

/// <summary>Filter A or B (gapE 1.3).</summary>
public enum WwiseVoiceFilterRole
{
    /// <summary>Filter A, before the aux sends (gapE 1.5).</summary>
    A,

    /// <summary>Filter B, before the first dry mix only; bypassed for every shipped sound (gapE 1.3, 1.5).</summary>
    B,
}

/// <summary>
/// A voice filter node (M6-011, C43): the LPF band and the HPF band (<c>voice+0x1D0</c> / <c>voice+0x3A0</c>), the globals the process reads (<c>u32[0x105243C]</c> the mix rate, <c>u32[0x105244C]</c> the ramp chunk), the init
/// <c>0xA764D4</c>, the Reset <c>0xA7666C</c> and the E1 entry <c>0xA766B8</c> / <c>0xA4C60C</c>. Filter A runs before the aux sends, so it shapes the signal the Robot_Bus_1 tap sees; filter B is dry-path only and is bypassed for every
/// shipped sound.
/// </summary>
// fidelity: M6-011, M6-022
public sealed class WwiseVoiceFilter
{
    /// <summary>The LPF band (<c>node+0x170</c>).</summary>
    public WwiseVoiceFilterBand LowPass { get; }

    /// <summary>The HPF band (<c>node+0x180</c>).</summary>
    public WwiseVoiceFilterBand HighPass { get; }

    /// <summary><c>u32 [0x105243C]</c>: the mix rate the design divides by (initial 0xBB80; <c>SetRate 0xA1C75C</c> writes it).</summary>
    public uint MixRate { get; set; }

    /// <summary><c>u32 [0x105244C]</c>: the ramp chunk N (initial 0x80; <c>SetRate</c> writes <c>floor(rate * 128 / 48000)</c>, <c>0xA1C768..0xA1C794</c>).</summary>
    public uint RampChunk { get; set; }

    /// <param name="role">A or B; the class behaves the same, the role is carried for the compare.</param>
    /// <param name="rateHz">The mix rate (default 48 kHz, M6-018); the chunk is the one <c>SetRate</c> derives from it.</param>
    public WwiseVoiceFilter(WwiseVoiceFilterRole role, int rateHz = WwiseRuntimeSettings.MixRateHz)
    {
        Role = role;
        LowPass = new WwiseVoiceFilterBand(WwiseVoiceFilterKind.LowPass);
        HighPass = new WwiseVoiceFilterBand(WwiseVoiceFilterKind.HighPass);
        MixRate = (uint)rateHz;
        RampChunk = ChunkForRate((uint)rateHz);
    }

    /// <summary><c>SetRate 0xA1C75C</c>'s chunk: <c>((rate &lt;&lt; 7) * 0x057619F1) &gt;&gt; 32 &gt;&gt; 10</c> (<c>umull</c> high word, <c>0xA1C768..0xA1C794</c>) = <c>floor(rate * 128 / 48000)</c>.</summary>
    public static uint ChunkForRate(uint rate) => (uint)(((ulong)(rate << 7) * 0x057619F1UL) >> 32 >> 10);

    /// <summary>Which filter this is.</summary>
    public WwiseVoiceFilterRole Role { get; }

    /// <summary>
    /// The Reset <c>0xA7666C(node)</c> (the filter object's <c>vt+0x14</c> body, reached from <c>0xA4C5D8</c>): <c>first = 1</c> on both bands and both histories zeroed (a null history is skipped); cur, target, steps, countdown, dirty and bypass are untouched.
    /// </summary>
    public void ResetA7666C()
    {
        LowPass.First = 1;                                           // 0xA7667C strb 1,[node+0x17C]
        if (LowPass.History is { } l) Array.Clear(l);
        HighPass.First = 1;                                          // 0xA7669C strb 1,[node+0x18C]
        if (HighPass.History is { } h) Array.Clear(h);
    }

    /// <summary><c>[F+0x190]</c>: the channel word the init stored (<c>0xA764FC</c>); <c>byte [F+0x194]</c> is <see cref="Byte194"/>.</summary>
    // fidelity: M6-022
    public uint Word190 { get; private set; }

    /// <summary><c>byte [F+0x194]</c> (<c>0xA764F0</c>): the init's third argument.</summary>
    // fidelity: M6-022
    public byte Byte194 { get; private set; }

    /// <summary>True when both history blocks are allocated (the init <c>0xA764D4</c> succeeded).</summary>
    public bool IsInitialised => LowPass.History is not null && HighPass.History is not null;

    /// <summary>
    /// <c>0xA764D4(F, word, flag)</c> (<c>0xA54B74</c>, <c>0xA54D44</c>): stores <c>flag</c> and <c>word</c>, puts both bands in the init state (<c>0xA76500..0xA7654C</c>) and allocates the two zeroed <c>(byte word) &lt;&lt; 4</c>-byte history blocks (<c>0xA7655C</c>, <c>0xA7658C</c>):
    /// an allocation failure (<paramref name="allocationFails"/>, called once per block) frees what was made, leaves both history pointers null and returns 2 (<c>0xA765B0..0xA765FC</c>); otherwise 1.
    /// </summary>
    // fidelity: M6-022
    public int InitA764D4(uint word, byte flag, Func<bool>? allocationFails)
    {
        Byte194 = flag;
        Word190 = word;
        LowPass.SetInitState();
        HighPass.SetInitState();
        int floats = (int)(word & 0xFF) * 4;                         // (byte word) << 4 bytes
        if (allocationFails?.Invoke() == true)                       // 0xA76564 beq 0xA765D0 -> 0xA765FC mov r0,#2
        {
            LowPass.History = null;                                  // 0xA76560 str r0,[r4,#0xb0] (the null result)
            HighPass.History = null;                                 // 0xA765F4 str 0,[r4,#0x160]
            return 2;
        }
        LowPass.History = new float[floats];                         // 0xA76570 memset 0
        if (allocationFails?.Invoke() == true)                       // 0xA76598 beq 0xA765B0 ... mov r0,#2
        {
            LowPass.History = null;                                  // 0xA765B0..0xA765CC: the first block is freed and [0xB0] = 0
            HighPass.History = null;
            return 2;
        }
        HighPass.History = new float[floats];                        // 0xA765A4 memset 0
        return 1;
    }

    /// <summary>
    /// The E1 body <c>0xA766B8(node, S)</c>: the LPF process <c>0xA766F0(S, node+0x170, node+0x10, byte [node+0x194])</c> then the HPF <c>0xA77480(S, node+0x180, node+0xC0, ...)</c> on the same pass block, in place. <paramref name="alignBytes"/> is the
    /// address of <c>[S]</c> modulo 16 (the engine's buffer is 16-byte aligned, P1).
    /// </summary>
    public void Process(float[] data, byte channels, ushort frames, ushort stride, uint alignBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (channels != 0 && (long)(channels - 1) * stride + frames > data.Length)
            throw new InvalidOperationException("M6-011 E1: the engine would read past the audio buffer: (channels - 1) * stride + frames exceed it");
        LowPass.Process(data, channels, frames, stride, alignBytes, MixRate, RampChunk);
        HighPass.Process(data, channels, frames, stride, alignBytes, MixRate, RampChunk);
    }

    /// <summary>
    /// <c>0xA4C60C(node, S)</c> (E1, <c>0xA446E0</c>; E2 for filter B, <c>0xA4492C</c>): returns when <c>[S] == 0</c>, else <see cref="Process(float[], byte, ushort, ushort, uint)"/> with <c>byte [S+4]</c>, <c>u16 [S+0xE]</c> and <c>u16 [S+0xC]</c>.
    /// </summary>
    // fidelity: M6-011, M6-022
    public void ProcessA4C60C(WwiseDecodeState s, uint alignBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.Data is null) return;                                  // 0xA4C610 cmp r3,#0; bxeq lr
        var data = s.Data as float[] ?? throw new InvalidOperationException("M6-011 E1: the filter reads planar float[] data");
        Process(data, unchecked((byte)s.ChannelConfig), s.ValidFrames, s.MaxFrames, alignBytes);   // 0xA766B8
    }
}

/// <summary>
/// One node-to-voice connection's LPF/HPF contribution (M6-011, gapE 1.2). In 2D the connection's value is the
/// effective context value; in 3D it is <c>max(ctx, attenuation)</c> (0x00A5C740..0x00A5C774). The 3D
/// attenuation's own source is outside this record: the caller supplies it.
/// </summary>
/// <param name="Is3D">True when the connection takes the 3D path (ctx+0xDC bits 0-1 != 0).</param>
/// <param name="AttenuationLpf">The attenuation's LPF value, used only in 3D.</param>
/// <param name="AttenuationHpf">The attenuation's HPF value, used only in 3D.</param>
/// <param name="LowPassB">The connection's filter-B LPF value (conn+0x54); 0 in the shipped data.</param>
/// <param name="HighPassB">The connection's filter-B HPF value (conn+0x5C); 0 in the shipped data.</param>
public readonly record struct WwiseVoiceFilterConnection(
    bool Is3D,
    float AttenuationLpf = 0f,
    float AttenuationHpf = 0f,
    float LowPassB = 0f,
    float HighPassB = 0f);

/// <summary>The four filter targets a voice computes (gapE 1.2, 1.3), each clamped to 0..100.</summary>
public readonly record struct WwiseVoiceFilterTargets(float LowPassA, float HighPassA, float LowPassB, float HighPassB);

/// <summary>
/// Builds a voice's filter A/B targets (M6-011, gapE 1.1..1.3).
///
/// The effective LPF/HPF is the node-chain sum plus the randomizer draw plus <c>pbi+0xA0</c>/<c>pbi+0xA8</c>
/// (zeroed by the context ctor; the inventory says no other direct writer was found, so it is an input defaulting
/// to zero rather than a value this class invents). The per-connection values come from that effective value
/// (2D) or <c>max(effective, attenuation)</c> (3D). A-LPF/A-HPF are the minimum over the connections, starting
/// at 100; B is the minimum of the connection's B values (0), then <c>max(., outputBus)</c>. Everything is
/// clamped to 0..100.
/// </summary>
public static class WwiseVoiceFilterComposer
{
    /// <summary>The initial target before any connection contributes (gapE 1.2): 100 (0x42C80000).</summary>
    public const float InitialValue = 100f;

    /// <summary>The LPF/HPF target clamp (gapE 1.3).</summary>
    public const float ClampLow = 0f;
    public const float ClampHigh = 100f;

    /// <summary>
    /// Composes the targets. <paramref name="ctxLpfExtra"/>/<paramref name="ctxHpfExtra"/> are
    /// <c>pbi+0xA0</c>/<c>pbi+0xA8</c>, zero in the shipped data. <paramref name="outputBusLpf"/>/
    /// <paramref name="outputBusHpf"/> are the output-bus props <c>pbi+0x68</c>/<c>pbi+0x6C</c>, which no
    /// shipped sound sets.
    /// </summary>
    public static WwiseVoiceFilterTargets Compose(
        float nodeChainLpf,
        float nodeChainHpf,
        float randomizerLpf,
        float randomizerHpf,
        IReadOnlyList<WwiseVoiceFilterConnection> connections,
        float ctxLpfExtra = 0f,
        float ctxHpfExtra = 0f,
        float outputBusLpf = 0f,
        float outputBusHpf = 0f)
    {
        float ctxLpf = nodeChainLpf + randomizerLpf + ctxLpfExtra;
        float ctxHpf = nodeChainHpf + randomizerHpf + ctxHpfExtra;

        float lowPassA = InitialValue, highPassA = InitialValue;
        float lowPassB = InitialValue, highPassB = InitialValue;

        foreach (var connection in connections)
        {
            float lpf = connection.Is3D ? MathF.Max(ctxLpf, connection.AttenuationLpf) : ctxLpf;
            float hpf = connection.Is3D ? MathF.Max(ctxHpf, connection.AttenuationHpf) : ctxHpf;
            lowPassA = MathF.Min(lowPassA, lpf);
            highPassA = MathF.Min(highPassA, hpf);
            lowPassB = MathF.Min(lowPassB, connection.LowPassB);
            highPassB = MathF.Min(highPassB, connection.HighPassB);
        }

        return new(
            Math.Clamp(lowPassA, ClampLow, ClampHigh),
            Math.Clamp(highPassA, ClampLow, ClampHigh),
            Math.Clamp(MathF.Max(lowPassB, outputBusLpf), ClampLow, ClampHigh),
            Math.Clamp(MathF.Max(highPassB, outputBusHpf), ClampLow, ClampHigh));
    }
}
