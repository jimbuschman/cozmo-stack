// fidelity: M6-013
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// A direct-form-I biquad's stored coefficients, in the order the EQ object keeps them at <c>this+4+0x14*band</c> (<c>0xAA25E0</c>'s store tail <c>0xAA272C</c>): <c>c0 = b0/a0</c>, <c>c1 = b1/a0</c>, <c>c2 = b2/a0</c>, <c>c3 = (-a1)/a0</c>, <c>c4 = (-a2)/a0</c>
/// (<see cref="NegA1"/> and <see cref="NegA2"/> are the stored <c>c3</c> and <c>c4</c>), so the difference equation is <c>y = ((((c2*x2 + x*c0) + c1*x1) + c4*y2) + c3*y1)</c> (<c>0xAA2324</c>).
/// </summary>
public readonly record struct WwiseBiquadCoefficients(float B0, float B1, float B2, float NegA1, float NegA2);

/// <summary>
/// The Parametric EQ coefficient routine <c>0xAA25E0(this, band, record)</c> (research bus-fx-17 section 3.6 with the verifier's build spec; <c>record = {u32 type, f32 gain, f32 freq, f32 Q}</c>), every operation in binary32 in the engine's association.
/// <para><c>fs = (float)u32[this+0x48]</c>; <c>lim = (fs * 0.5f) * 0.9f</c> (<c>0x3F666666</c>); <c>f = freq</c>, and <c>f = lim</c> when <c>f &gt;= lim</c> (a NaN frequency stays NaN). The seven types by <c>addls pc, pc, type, lsl #2</c> (table <c>0xAA262C</c>): 0 LP, 1 HP, 2 BP, 3 notch, 4 low shelf, 5 high shelf, 6 peaking;
/// a type above 6 jumps to the store tail <c>0xAA272C</c> with whatever the registers hold (undefined: <see cref="WwiseMissingBehaviourException"/>). The libm calls (tanf, sinf, cosf, powf, sqrtf) are the host seams of <see cref="WwiseHostMath"/>.</para>
/// </summary>
public static class WwiseEqCoefficients
{
    private static float Bits(uint b) => BitConverter.UInt32BitsToSingle(b);

    private static readonly float Pi = Bits(0x40490FDB);          // 0xAA2948
    private static readonly float TwoPi = Bits(0x40C90FDB);       // 0xAA2954
    private static readonly float Sqrt2 = Bits(0x3FB504F3);       // 0xAA294C
    private static readonly float GainScale = Bits(0x3CCCCCCD);   // 0xAA2950: 0.025f
    private static readonly float Nine = Bits(0x3F666666);        // 0xAA2944: 0.9f

    /// <summary>The coefficient routine's cap fraction of the sample rate: <c>(fs * 0.5f) * 0.9f</c> equals <c>0.45f * fs</c> bit for bit for every integer rate 8000..200000 (verifier V3).</summary>
    public const float NyquistFraction = 0.45f;

    private static float VSqrt(float v)
    {
        float r = MathF.Sqrt(v);                                  // vsqrt.f32
        return float.IsNaN(r) ? WwiseHostMath.Sqrtf(v) : r;       // vcmp s,s; bne -> bl sqrtf (0xAA26AC, 0xAA26D4, 0xAA29C0, 0xAA29E8)
    }

    /// <summary>The five stored coefficients of one band record at the sample rate <paramref name="rate"/> (<c>u32 [this+0x48]</c>).</summary>
    public static WwiseBiquadCoefficients Compute(uint type, float gain, float freq, float q, uint rate)
    {
        float fs = (float)rate;                                   // vldr s12,[r0,#0x48]; vcvt.f32.u32
        float lim = (fs * 0.5f) * Nine;                           // 0xAA2608..0xAA2610
        float f = freq;
        if (f >= lim) f = lim;                                    // 0xAA2614..0xAA261C vcmp; vmovge

        float n0, n1, n2, m1, m2, a0;                             // s14, s13, s10, s16, s15, s11 at the store tail
        switch (type)
        {
            case 0:                                               // low pass 0xAA27F0
            {
                float k = WwiseHostMath.Tanf((f * Pi) / fs);
                float c = 1f / k;
                float c2 = c * c;
                float r = c * Sqrt2;
                float d = c2 + 1f;
                float b0 = 1f / (r + d);
                float t = d - r;
                float u = 1f - c2;
                float two = b0 + b0;
                n0 = b0; n1 = two; n2 = b0;
                m1 = u * two; m2 = t * b0;
                a0 = 1f;
                break;
            }
            case 1:                                               // high pass 0xAA2848
            {
                float k = WwiseHostMath.Tanf((f * Pi) / fs);
                float k2 = k * k;
                float r = k * Sqrt2;
                float d = k2 + 1f;
                float b0 = 1f / (r + d);
                float mTwo = b0 * -2f;
                float e = k2 - 1f;
                float t = d - r;
                n0 = b0; n1 = mTwo; n2 = b0;
                m1 = -(mTwo * e);                                 // vnmul.f32 s16, s13, s16
                m2 = t * b0;
                a0 = 1f;
                break;
            }
            case 2:                                               // band pass 0xAA28A0
            {
                float w = (f * TwoPi) / fs;
                float cs = WwiseHostMath.Cosf(w);
                float sn = WwiseHostMath.Sinf(w);
                float al = sn / (q + q);
                n0 = al; n1 = 0f; n2 = -al;
                m1 = cs * -2f; m2 = 1f - al;
                a0 = al + 1f;
                break;
            }
            case 3:                                               // notch 0xAA28F0
            {
                float w = (f * TwoPi) / fs;
                float cs = WwiseHostMath.Cosf(w);
                float sn = WwiseHostMath.Sinf(w);
                float al = sn / (q + q);
                float c2 = cs * -2f;
                n0 = 1f; n1 = c2; n2 = 1f;
                m1 = c2; m2 = 1f - al;
                a0 = al + 1f;
                break;
            }
            case 4:                                               // low shelf 0xAA2648
            case 5:                                               // high shelf 0xAA295C
            {
                float a = WwiseHostMath.Powf(10f, gain * GainScale);
                float w = (f * TwoPi) / fs;
                float sn = WwiseHostMath.Sinf(w);
                float cs = WwiseHostMath.Cosf(w);
                float inv = 1f / a;
                float v = 2f + (inv + a) * 0f;                    // vmla.f32 s14(2.0), s15, s13(0.0): S = 1, the constant 0.0 is the literal at 0xAA2958
                float sq = VSqrt(v);
                float sa = VSqrt(a);
                float p = a + 1f;
                float m = a - 1f;
                float alp = (sn * 0.5f) * sq;
                float beta = (sa + sa) * alp;
                float mcs = m * cs;
                float pcs = p * cs;
                if (type == 4)
                {
                    float t1 = p - mcs;
                    float t2 = p + mcs;
                    n0 = (t1 + beta) * a;
                    n2 = (t1 - beta) * a;
                    n1 = (a + a) * (m - pcs);
                    m1 = (m + pcs) * -2f;
                    m2 = t2 - beta;
                    a0 = t2 + beta;
                }
                else
                {
                    float t1 = p + mcs;
                    float t2 = p - mcs;
                    float qq = m - pcs;
                    n0 = (t1 + beta) * a;
                    n2 = (t1 - beta) * a;
                    n1 = (a * -2f) * (m + pcs);
                    m1 = qq + qq;
                    m2 = t2 - beta;
                    a0 = t2 + beta;
                }
                break;
            }
            case 6:                                               // peaking 0xAA2774
            {
                float w = (f * TwoPi) / fs;
                float cs = WwiseHostMath.Cosf(w);
                float a = WwiseHostMath.Powf(10f, gain * GainScale);
                float sn = WwiseHostMath.Sinf(w);
                float al = sn / (q + q);
                float alOverA = al / a;
                float alTimesA = al * a;
                float c2 = cs * -2f;
                n0 = alTimesA + 1f; n1 = c2; n2 = 1f - alTimesA;
                m1 = c2; m2 = 1f - alOverA;
                a0 = alOverA + 1f;
                break;
            }
            default:
                throw new WwiseMissingBehaviourException(
                    $"M6-013 C45.3: EQ filter type {type} is above 6: 0xAA25E0 jumps to its store tail 0xAA272C with whatever the registers hold (the output is undefined; the inventory records it as unread)");
        }

        return new WwiseBiquadCoefficients(n0 / a0, n1 / a0, n2 / a0, (-m1) / a0, (-m2) / a0);   // 0xAA272C..0xAA276C: vdiv by a0, vneg before the divide for c3 and c4
    }
}

/// <summary>
/// The Parametric EQ's parameter object (<c>0x4C</c> bytes, vptr <c>0x101C9F8</c>; created by <c>0xAA3178</c>, cloned by <c>0xAA2DF4</c>; research bus-fx-17 section 2.1..2.5, C45.2): <c>vt+8</c> SetParam <c>0xAA2F44</c>, <c>vt+0xC</c> Clone <c>0xAA2DF4</c>, <c>vt+0x10</c> wrapper <c>0xAA30CC</c>
/// (size 0 installs the defaults <c>0xAA30E4..0xAA3160</c>, otherwise it tail-calls <c>vt+0x18</c> whatever the block length), <c>vt+0x14</c> destroy <c>0xAA2E54</c>, <c>vt+0x18</c> SetParamsBlock <c>0xAA2E8C</c>.
/// <para>Layout: three bands at <c>+4 + 0x14*band</c> as <c>{u32 type, f32 gain, f32 freq, f32 Q, u8 on}</c> (<c>+0x14</c>, <c>+0x28</c>, <c>+0x3C</c> are the on bytes), <c>f32 outputDb</c> at <c>+0x40</c>, byte ProcessLFE at <c>+0x44</c>, per-band dirty bytes at <c>+0x48..+0x4A</c>. The creator stores only the vptr and the dirty bytes (1);
/// the rest is uninitialised pool memory until a block, the defaults or SetParam stores it (<see cref="WwisePluginMemory"/>).</para>
/// </summary>
public sealed class WwiseEqParams
{
    /// <summary>The block size the bank carries (3 bands of 17 bytes, the output level and the ProcessLFE byte); <c>0xAA2E8C</c> reads these 56 bytes whatever the size argument says.</summary>
    public const int BlockSize = 56;

    private readonly WwisePluginMemory _m = new(0x4C, "Parametric EQ parameter object");

    private WwiseEqParams()
    {
    }

    /// <summary>Creator <c>0xAA3178(alloc)</c>: a <c>0x4C</c>-byte object with the three dirty bytes set to 1; null when the allocation fails.</summary>
    public static WwiseEqParams? Create(IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        if (!alloc.Allocate(0x4C)) return null;                                 // 0xAA317C..0xAA3190
        var p = new WwiseEqParams();
        for (int b = 0; b < 3; b++) p._m.SetU8(0x48 + b, 1);                    // 0xAA3198..0xAA31AC
        return p;
    }

    /// <summary>The u32 at offset <paramref name="offset"/> of the object (a test accessor; an unstored word stops visibly).</summary>
    public uint Word(int offset) => _m.U32(offset);

    /// <summary>The byte at offset <paramref name="offset"/> of the object.</summary>
    public byte Byte(int offset) => _m.U8(offset);

    /// <summary>Band <paramref name="band"/>'s type word (<c>+4 + 0x14*band</c>).</summary>
    public uint BandType(int band) => _m.U32(4 + 0x14 * band);

    /// <summary>Band gain in dB (<c>+8 + 0x14*band</c>).</summary>
    public float BandGain(int band) => _m.F32(8 + 0x14 * band);

    /// <summary>Band frequency (<c>+0xC + 0x14*band</c>).</summary>
    public float BandFrequency(int band) => _m.F32(0xC + 0x14 * band);

    /// <summary>Band Q (<c>+0x10 + 0x14*band</c>).</summary>
    public float BandQ(int band) => _m.F32(0x10 + 0x14 * band);

    /// <summary>Band on byte (<c>+0x14 + 0x14*band</c>).</summary>
    public byte BandOn(int band) => _m.U8(0x14 + 0x14 * band);

    /// <summary>The raw band record the coefficient routine reads: <c>{type, gain, freq, Q}</c>.</summary>
    internal (uint Type, float Gain, float Freq, float Q) Record(int band) => (BandType(band), BandGain(band), BandFrequency(band), BandQ(band));

    /// <summary>The output level in dB, stored raw (<c>+0x40</c>).</summary>
    public float OutputDb => _m.F32(0x40);

    /// <summary>The ProcessLFE byte (<c>+0x44</c>).</summary>
    public byte ProcessLfe => _m.U8(0x44);

    /// <summary>The band's dirty byte (<c>+0x48 + band</c>).</summary>
    public byte Dirty(int band) => _m.U8(0x48 + band);

    internal void ClearDirty(int band) => _m.SetU8(0x48 + band, 0);

    internal void MarkDirty(int band) => _m.SetU8(0x48 + band, 1);

    /// <summary>
    /// Clone <c>vt+0xC</c> (<c>0xAA2DF4</c>): allocates <c>0x4C</c> bytes through <c>alloc-&gt;vt+8</c> (null on failure), copies the <c>0x44</c> bytes at <c>+4</c> (so the ProcessLFE byte at <c>+0x44</c> is copied) and sets the three dirty bytes to 1.
    /// </summary>
    public WwiseEqParams? Clone(IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        if (!alloc.Allocate(0x4C)) return null;                                 // 0xAA2E08..0xAA2E14
        var c = new WwiseEqParams();
        c._m.CopyFrom(_m, 4, 4, 0x44);                                          // 0xAA2E20..0xAA2E34 memcpy(new+4, this+4, 0x44)
        for (int b = 0; b < 3; b++) c._m.SetU8(0x48 + b, 1);                    // 0xAA2E38..0xAA2E44
        return c;
    }

    /// <summary>Destroy <c>vt+0x14</c> (<c>0xAA2E54</c>): a null object returns 1; otherwise the object is freed through <c>alloc-&gt;vt+0xC</c> and 1 is returned.</summary>
    public static int Destroy(WwiseEqParams? p, IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        if (p is not null) alloc.Free(p);                                       // 0xAA2E5C..0xAA2E80
        return 1;                                                               // 0xAA2E84
    }

    /// <summary>
    /// <c>vt+0x10</c> (<c>0xAA30CC</c>): a zero <paramref name="size"/> installs the defaults (<c>0xAA30E4..0xAA3160</c>: band 0 type 4, gain 0, 120.0, Q 5.0, on; band 1 type 6, 0, 2000.0, 5.0, on; band 2 type 5, 0, 5000.0, 5.0, on; output 0.0, ProcessLFE 1; all dirty) and returns 1; otherwise it forwards to
    /// <see cref="SetParamsBlock"/> (<c>vt+0x18</c>) with the block and size unchecked.
    /// </summary>
    public int SetParamsBlockOrDefaults(byte[]? block, uint size)
    {
        if (size != 0) return SetParamsBlock(block);                            // 0xAA30CC cmp r3,#0; bne 0xAA3164
        SetBand(0, 4, 0f, 120f, 5f, 1);
        SetBand(1, 6, 0f, 2000f, 5f, 1);
        SetBand(2, 5, 0f, 5000f, 5f, 1);
        _m.SetU32(0x40, 0);                                                     // 0xAA3150
        _m.SetU8(0x44, 1);                                                      // 0xAA3120
        for (int b = 0; b < 3; b++) _m.SetU8(0x48 + b, 1);                      // 0xAA3124..0xAA312C
        return 1;
    }

    private void SetBand(int band, uint type, float gain, float freq, float q, byte on)
    {
        int o = 4 + 0x14 * band;
        _m.SetU32(o, type);
        _m.SetU32(o + 4, BitConverter.SingleToUInt32Bits(gain));
        _m.SetU32(o + 8, BitConverter.SingleToUInt32Bits(freq));
        _m.SetU32(o + 0xC, BitConverter.SingleToUInt32Bits(q));
        _m.SetU8(o + 0x10, on);
    }

    /// <summary>
    /// <c>vt+0x18</c> SetParamsBlock (<c>0xAA2E8C</c>): the 56-byte block (three <c>{u32 type, f32 gain, f32 freq, f32 Q, u8 on}</c> at block offsets <c>0</c>, <c>0x11</c>, <c>0x22</c> (unaligned word reads), then <c>f32 outputDb</c> at <c>0x33</c> stored raw and the byte <c>processLFE</c> at <c>0x37</c>) goes to
    /// <c>+4..+0x3C</c>, <c>+0x40</c> and <c>+0x44</c>; the three dirty bytes are set to 1; returns 1. The engine reads 56 bytes whatever the size argument says, so a shorter block is refused.
    /// </summary>
    public int SetParamsBlock(byte[]? block)
    {
        if (block is null || block.Length < BlockSize)
            throw new WwiseMissingBehaviourException("M6-013 C45.2: 0xAA2E8C reads 56 bytes at the block pointer whatever the size argument says; a shorter block is not an engine input");
        for (int b = 0; b < 3; b++)
        {
            int s = 0x11 * b;
            int o = 4 + 0x14 * b;
            _m.SetU32(o, BitConverter.ToUInt32(block, s));                       // 0xAA2E9C / 0xAA2ED8 ...
            _m.SetU32(o + 4, BitConverter.ToUInt32(block, s + 4));
            _m.SetU32(o + 8, BitConverter.ToUInt32(block, s + 8));
            _m.SetU32(o + 0xC, BitConverter.ToUInt32(block, s + 0xC));
            _m.SetU8(o + 0x10, block[s + 0x10]);
        }
        _m.SetU32(0x40, BitConverter.ToUInt32(block, 0x33));                    // 0xAA2EF8 ldr ip,[r1,#0x33]; 0xAA2F28 str ip,[r3,#0x40]
        _m.SetU8(0x44, block[0x37]);                                            // 0xAA2EFC ldrb r1,[r1,#0x37]; 0xAA2F2C strb r1,[r3,#0x44]
        for (int b = 0; b < 3; b++) _m.SetU8(0x48 + b, 1);                      // 0xAA2F30..0xAA2F38
        return 1;
    }

    /// <summary>The vcvt.s32.f32 conversion: toward zero, saturating, a NaN gives 0.</summary>
    internal static int ToInt32Saturating(float v)
    {
        if (float.IsNaN(v)) return 0;
        if (v >= 2147483648f) return int.MaxValue;
        if (v <= -2147483648f) return int.MinValue;
        return (int)v;
    }

    /// <summary>
    /// <c>vt+8</c> SetParam (<c>0xAA2F44</c>): a null value pointer or an id above 16 returns 0x1F. Ids 0..14 address band <c>id / 5</c> (the table <c>0xAA2F74</c>): <c>id % 5</c> 0 stores the float converted to an int (<c>vcvt.s32.f32</c>) as the type, 1 the gain word, 2 the frequency word, 3 the Q word, 4 the on byte
    /// (<c>value != 0.0f</c>; a NaN counts as on) and sets the band's dirty byte; id 15 stores the output-level word raw (no dirty byte); id 16 stores the ProcessLFE byte. Returns 1.
    /// </summary>
    public int SetParam(uint id, byte[]? value)
    {
        if (value is null) return 0x1F;                                         // 0xAA2F44 cmp r2,#0; beq 0xAA2FB8
        if (id > 16) return 0x1F;                                               // 0xAA2F68 cmp r1,#0x10; addls pc / b 0xAA30B0
        if (id == 16)
        {
            if (value.Length < 1) throw new ArgumentException("the engine reads 1 byte at the value pointer", nameof(value));
            _m.SetU8(0x44, value[0]);                                           // 0xAA2FC4
            return 1;
        }
        if (value.Length < 4) throw new ArgumentException("the engine reads 4 bytes at the value pointer", nameof(value));
        uint word = BitConverter.ToUInt32(value, 0);
        if (id == 15)
        {
            _m.SetU32(0x40, word);                                              // 0xAA30A0
            return 1;
        }
        int band = (int)(id / 5);
        int o = 4 + 0x14 * band;
        switch (id % 5)
        {
            case 0: _m.SetU32(o, unchecked((uint)ToInt32Saturating(BitConverter.UInt32BitsToSingle(word)))); break;   // 0xAA2FD8: vcvt.s32.f32
            case 1: _m.SetU32(o + 4, word); break;                              // 0xAA3000
            case 2: _m.SetU32(o + 8, word); break;                              // 0xAA3024
            case 3: _m.SetU32(o + 0xC, word); break;                            // 0xAA3048
            default: _m.SetU8(o + 0x10, (byte)(BitConverter.UInt32BitsToSingle(word) == 0f ? 0 : 1)); break;          // 0xAA306C: vcmp.f32 s15,#0; movne / moveq
        }
        _m.SetU8(0x48 + band, 1);
        return 1;
    }
}

/// <summary>
/// The Parametric EQ plug-in (<c>0x00690003</c>, ARM, vptr <c>0x103DF88</c>; the <c>0x54</c>-byte object created by <c>0xAA257C</c>): the Robot_Bus insert effect in slots 0 and 1 (Robot_Bus_Eq_MasterCurve, Robot_Bus_Eq_HiLowPass). Slots: <c>+0</c> <c>0xAA23EC</c> (<c>bx lr</c>), <c>+4</c> <c>0xAA2474</c> (deleting
/// destructor), <c>+8</c> Term <c>0xAA23F0</c>, <c>+0xC</c> Reset <c>0xAA2488</c>, <c>+0x10</c> GetPluginInfo <c>0xAA244C</c>, <c>+0x14</c> / <c>+0x18</c> the shared stubs <c>0x8DBF38</c> / <c>0x8DBF3C</c> (return 0), <c>+0x1C</c> Init <c>0xAA24B8</c>, <c>+0x20</c> Execute <c>0xAA2A84</c>, <c>+0x24</c> <c>0xAA2DE8</c> (returns 0x2D).
/// <para><b>Fields:</b> <c>+4..+0x3C</c> the three bands' stored coefficients (<c>c0..c4</c>, 5 floats each), <c>+0x40</c> the parameter object, <c>+0x44</c> the processed channels, <c>+0x48</c> the rate, <c>+0x4C</c> the state array (<c>{x1, x2, y1, y2}</c> per channel and band, <c>channels * 0x30</c> bytes), <c>+0x50</c> the previous output gain.
/// Init does not zero the state; the owner calls <see cref="Reset"/> next (a managed state array is zeroed, but Execute before the Reset stops visibly).</para>
/// <para><b>Numerics:</b> binary32 throughout; the biquad is scalar VFP with non-fused multiply-adds; the output-gain stage is <see cref="WwiseOutputGainStage"/> (NEON lanes flush to zero by the ISA; the scalar VFP parts follow the phone's FPSCR, HARDWARE_ONLY, IEEE here). The libm calls are the host seams of <see cref="WwiseHostMath"/>
/// (EQUIVALENT_IMPLEMENTATION; exactness on a given phone is BLOCKED_EXTERNAL).</para>
/// </summary>
public sealed class WwiseEqPlugin : IWwiseEffectPlugin
{
    /// <summary>The plug-in id of the Parametric EQ in the bank (<c>0x00690003</c>).</summary>
    public const uint PluginId = 0x00690003;

    /// <summary>The vptr (<c>0x103DF88</c>).</summary>
    public const uint Vptr = 0x103DF88;

    /// <summary>Init's allocation failure result (<c>0xAA251C moveq r6,#0x34</c>).</summary>
    public const int AllocationFailed = 0x34;

    private bool _initialised;
    private bool _terminated;
    private bool _stateReady;
    private WwiseEqParams? _param;                  // +0x40
    private uint _channels;                         // +0x44
    private uint _rate;                             // +0x48
    private float[]? _state;                        // +0x4C
    private float _previousGain;                    // +0x50
    private bool _gainSet;
    private readonly float[] _coef = new float[15]; // +4 .. +0x3C

    private WwiseEqPlugin()
    {
    }

    /// <summary>Create <c>0xAA257C(alloc)</c>: allocates <c>0x54</c> bytes (null returns null), then stores <c>[+0x40] = 0</c>, <c>[+0x4C] = 0</c> and the vptr; the other fields are uninitialised until <see cref="Init"/>.</summary>
    public static WwiseEqPlugin? Create(IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        return alloc.Allocate(0x54) ? new WwiseEqPlugin() : null;               // 0xAA2580..0xAA2594
    }

    /// <summary><c>+0x40</c>: the parameter object.</summary>
    public WwiseEqParams? Params => _param;

    /// <summary><c>+0x44</c>: the processed channel count.</summary>
    public uint Channels => _channels;

    /// <summary><c>+0x48</c>: the rate.</summary>
    public uint Rate => _rate;

    /// <summary><c>+0x50</c>: the previous output gain.</summary>
    public float PreviousGain => _previousGain;

    /// <summary>The stored coefficients <c>+4 + 0x14*band</c> of a band.</summary>
    public WwiseBiquadCoefficients Coefficients(int band) =>
        new(_coef[5 * band], _coef[5 * band + 1], _coef[5 * band + 2], _coef[5 * band + 3], _coef[5 * band + 4]);

    /// <summary>The state array <c>[+0x4C]</c> (<c>{x1, x2, y1, y2}</c> per channel, band by band), or null when none is allocated; a copy.</summary>
    public float[]? StateSnapshot => _state?.ToArray();

    /// <inheritdoc />
    public int Slot14() => 0;                                                   // 0x8DBF38

    /// <inheritdoc />
    public int Slot18() => 0;                                                   // 0x8DBF3C

    /// <inheritdoc />
    public int Slot24() => 0x2D;                                                // 0xAA2DE8

    /// <inheritdoc />
    public int GetPluginInfo(out WwisePluginInfo info)
    {
        info = new WwisePluginInfo(3, 0x7E002, 1, 0);                           // 0xAA244C..0xAA246C
        return 1;
    }

    /// <summary>Term <c>0xAA23F0(this, alloc)</c>: frees the state array when <c>[+0x4C] != 0</c> (<c>alloc-&gt;vt+0xC</c>) and clears the pointer, runs the destructor <c>vt+0</c> (<c>bx lr</c>), frees the object and returns 1.</summary>
    public int Term(IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        if (_state is not null)
        {
            alloc.Free(_state);                                                 // 0xAA23F8..0xAA2414
            _state = null;                                                      // 0xAA2418..0xAA241C
        }
        alloc.Free(this);                                                       // 0xAA2430..0xAA243C
        _terminated = true;
        return 1;                                                               // 0xAA2444
    }

    /// <summary>Reset <c>0xAA2488</c>: when <c>[+0x4C] != 0</c> the <c>channels * 0x30</c> state bytes are zeroed; the coefficients and <c>[+0x50]</c> are not touched; returns 1.</summary>
    public int Reset()
    {
        if (!_initialised) throw new WwiseMissingBehaviourException("M6-013 C45.3: Reset (0xAA2488) reads [this+0x4C], which only Create and Init write, and [this+0x44], which only Init writes: the engine reads uninitialised pool memory");
        if (_state is not null)
        {
            Array.Clear(_state);                                                // 0xAA2498..0xAA24AC
            _stateReady = true;
        }
        return 1;                                                               // 0xAA24B0
    }

    /// <inheritdoc />
    public int Init(IWwisePluginMemAlloc alloc, object? ctx, object? parameters, WwiseEffectFormat fmt)
    {
        if (parameters is not WwiseEqParams p) throw new ArgumentException("the Parametric EQ's parameter object is a WwiseEqParams", nameof(parameters));
        return Init(alloc, ctx, p, fmt);
    }

    /// <summary>
    /// Init <c>0xAA24B8(this, alloc, ctx, param, fmt)</c> (<paramref name="ctx"/> is not read): <c>[+0x40] = param</c>, <c>[+0x44] = byte [fmt+4]</c>, <c>[+0x48] = u32 [fmt+0]</c>; when <c>u32 [fmt+4] &amp; 0x8000</c> and the parameter object's ProcessLFE byte is 0 the channel count is decremented;
    /// a non-zero count allocates <c>count * 0x30</c> bytes (failure: <c>[+0x4C] = 0</c> and 0x34 is returned with nothing else stored); then the coefficients <c>[+4, +0x40)</c> are zeroed, the three dirty bytes of the parameter object are set, and <c>[+0x50] = powf(10.0f, outputDb * 0.05f)</c>; returns 1.
    /// The state array is NOT zeroed.
    /// </summary>
    public int Init(IWwisePluginMemAlloc alloc, object? ctx, WwiseEqParams p, WwiseEffectFormat fmt)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        ArgumentNullException.ThrowIfNull(p);
        _param = p;                                                             // 0xAA24D4
        uint nch = fmt.Channels;                                                // 0xAA24C8 ldrb r2,[ip,#4]
        _channels = nch;                                                        // 0xAA24D8
        _rate = fmt.SampleRate;                                                 // 0xAA24DC
        if ((fmt.ChannelWord & 0x8000) != 0 && p.ProcessLfe == 0)               // 0xAA24D0 tst lr,#0x8000; 0xAA24E4 ldrb r0,[r3,#0x44]
        {
            if (nch == 0) throw new InvalidOperationException("byte [fmt+4] is 0 with the 0x8000 flag: the engine's channel count wraps to 0xFFFFFFFF and it allocates 0xFFFFFFFF * 0x30 bytes");
            nch--;                                                              // 0xAA24EC subeq r2,r2,#1
            _channels = nch;                                                    // 0xAA24F0
        }
        _initialised = true;
        _terminated = false;
        _stateReady = false;
        if (nch != 0)                                                           // 0xAA24F4 cmp r2,#0; beq 0xAA252C
        {
            if (!alloc.Allocate(checked((int)(nch * 0x30))))                    // 0xAA24FC..0xAA2514: alloc->vt+8(alloc, channels * 0x30)
            {
                _state = null;                                                  // 0xAA2518 str r0,[r5,#0x4C]
                return AllocationFailed;                                        // 0xAA251C moveq r6,#0x34
            }
            _state = new float[nch * 12];                                       // not zeroed in the engine; the owner's Reset zeroes it
        }
        Array.Clear(_coef);                                                     // 0xAA2530..0xAA2540 memset(this+4, 0, 0x3C)
        for (int b = 0; b < 3; b++) p.MarkDirty(b);                             // 0xAA254C, 0xAA2550, 0xAA255C
        _previousGain = WwiseHostMath.Powf(10f, p.OutputDb * OutputScale);      // 0xAA2544..0xAA256C: powf(10.0f, [param+0x40] * 0.05f)
        _gainSet = true;
        return 1;                                                               // 0xAA253C mov r6,#1; 0xAA2570
    }

    private static readonly float OutputScale = BitConverter.UInt32BitsToSingle(0x3D4CCCCD);   // 0xAA2B0C / 0xAA2558: 0.05f

    /// <summary>
    /// Execute <c>0xAA2A84(this, S)</c>: returns at once when <c>[+0x44] == 0</c> or <c>u16 [S+0xE] == 0</c> (nothing changes, not even <c>[+0x50]</c>; <c>S+8</c> is never written). Otherwise: for bands 0, 1, 2 in order, a band whose dirty byte is set has its coefficients recomputed by
    /// <see cref="WwiseEqCoefficients.Compute"/> from the parameter object's record (whether or not it is on) and the dirty byte cleared; <c>newGain = powf(10.0f, outputDb * 0.05f)</c>; then each band whose on byte (read before the recompute) is non-zero runs the biquad <c>0xAA2324</c> over all <c>[+0x44]</c> channels with
    /// the band's coefficients and the state at <c>[+0x4C] + band * channels * 0x10 + 0x10 * channel</c>; then <see cref="WwiseOutputGainStage"/>; <c>[+0x50] = newGain</c>.
    /// </summary>
    public void Execute(WwiseDecodeState s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!_initialised || _terminated || _param is null)
            throw new WwiseMissingBehaviourException("M6-013 C45.3: Execute (0xAA2A84) on an object Init has not filled (the engine reads uninitialised pool memory) or after Term");
        if (_channels == 0) return;                                             // 0xAA2A98..0xAA2AAC
        int valid = s.ValidFrames;
        if (valid == 0) return;                                                 // 0xAA2AB0..0xAA2AB8

        var p = _param;
        Span<bool> on = stackalloc bool[3];
        for (int b = 0; b < 3; b++)
        {
            on[b] = p.BandOn(b) != 0;                                           // 0xAA2AD4 / 0xAA2AE4 / 0xAA2AF4 (sb, r8, r7)
            if (p.Dirty(b) != 0)                                                // 0xAA2AD0 / 0xAA2AE0 / 0xAA2AF0
            {
                var (type, gain, freq, q) = p.Record(b);
                var c = WwiseEqCoefficients.Compute(type, gain, freq, q, _rate); // 0xAA2D0C / 0xAA2CF0 / 0xAA2CD0 bl 0xAA25E0
                _coef[5 * b] = c.B0; _coef[5 * b + 1] = c.B1; _coef[5 * b + 2] = c.B2; _coef[5 * b + 3] = c.NegA1; _coef[5 * b + 4] = c.NegA2;
                p.ClearDirty(b);                                                // 0xAA2D14..0xAA2D18
            }
        }

        float newGain = WwiseHostMath.Powf(10f, p.OutputDb * OutputScale);     // 0xAA2B00..0xAA2B20

        for (int b = 0; b < 3; b++)
            if (on[b]) Biquad(s, b);                                            // 0xAA2B24.. bl 0xAA2324

        WwiseOutputGainStage.Apply(s, p.ProcessLfe != 0, _previousGain, newGain);   // 0xAA2B38..0xAA2C70
        _previousGain = newGain;                                                // 0xAA2BE4 vstr s16,[r5,#0x50]
    }

    /// <summary>The scalar direct-form-I biquad <c>0xAA2324(S, coefficients, state, channels)</c>: per channel <c>y = c2*x2; y += x*c0; y += c1*x1; y += c4*y2; y += c3*y1</c> (non-fused), in place; the state <c>{x1, x2, y1, y2}</c> is stored back as <c>{x_last, x_prev, y_last, y_prev}</c>.</summary>
    private void Biquad(WwiseDecodeState s, int band)
    {
        if (_state is null) throw new InvalidOperationException("0xAA2324 writes through the null state pointer an allocation-failed Init left in [this+0x4C]");
        if (!_stateReady) throw new WwiseMissingBehaviourException("M6-013 C45.3: the biquad reads the state at [this+0x4C] (0xAA2364..0xAA2374): Init's allocation is not zeroed (the owner's Reset must run first)");
        int n = s.ValidFrames;
        int stride = s.MaxFrames;
        float[] data = WwiseOutputGainStage.DataOf(s, _channels, stride, n);
        float c0 = _coef[5 * band], c1 = _coef[5 * band + 1], c2 = _coef[5 * band + 2], c3 = _coef[5 * band + 3], c4 = _coef[5 * band + 4];
        var state = _state!;
        for (int ch = 0; ch < _channels; ch++)
        {
            int st = ((int)(band * _channels) + ch) * 4;
            float x1 = state[st], x2 = state[st + 1], y1 = state[st + 2], y2 = state[st + 3];
            int b = ch * stride;
            for (int i = 0; i < n; i++)
            {
                float x = data[b + i];
                float y = c2 * x2;                                              // 0xAA238C vmul.f32 s15, s7, s10
                y = y + x * c0;                                                 // 0xAA2390 vmla.f32 s15, s12, s9
                y = y + c1 * x1;                                                // 0xAA2394 vmla.f32 s15, s8, s13
                y = y + c4 * y2;                                                // 0xAA2398 vmla.f32 s15, s5, s11
                y = y + c3 * y1;                                                // 0xAA239C vmla.f32 s15, s6, s14
                data[b + i] = y;                                                // 0xAA23A0 vstmia
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
            }
            state[st] = x1; state[st + 1] = x2; state[st + 2] = y1; state[st + 3] = y2;   // 0xAA23B4..0xAA23C8
        }
    }
}
