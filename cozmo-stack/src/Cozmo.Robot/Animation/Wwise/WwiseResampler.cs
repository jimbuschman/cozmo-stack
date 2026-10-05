// fidelity: M6-004
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The host libm seam of the resampler (C38.2): the engine's <c>powf</c> is the phone's libm (PLT <c>0x4D6778</c>, called at <c>0xA46DE0</c>, <c>0xA473FC</c>, <c>0xA47488</c> and <c>0xA477B4</c>), which does not ship, so it is
/// EQUIVALENT_IMPLEMENTATION: <see cref="Powf"/> is the float32 correctly rounded result (double precision <c>pow</c>, rounded once to binary32). It is the ONE place the stack computes it.
/// <para>A one-ulp difference of the phone's powf changes the step by one LSB only for the cents whose <c>ratio * 2^(c/1200) * 65536</c> lands next to a <c>.5</c> boundary: 84 of the 4801 integer cents in [-2400, 2400]
/// for a voice-stage ratio of 1.0; inside [-800,-100] U [-250,250] U [100,600] those are -795, -759, -149, -94, -80, 16, 20, 172, 218, 316, 411, 455, 486, 507 and 525. The shipped values -800, -750, -600, -500 and -230 are not
/// sensitive. The Hijack stage calls it with 0.0 cents, so <c>powf(2, 0) = 1</c> exactly and its step does not depend on libm.</para>
/// </summary>
public static partial class WwiseHostMath
{
    /// <summary>The binary32 correctly rounded <c>x^y</c> (the host stand-in for the phone's <c>powf</c>).</summary>
    public static float Powf(float x, float y) => (float)Math.Pow(x, y);
}

/// <summary>
/// The values the native <c>CAkResampler::Init(fmt, outRate)</c> reads out of the 12-byte format (<c>0xA47038</c>, M6-004): <c>u32 [fmt+0]</c> the input rate, <c>byte [fmt+4]</c> the channel count, <c>u16 [fmt+8]</c> the
/// format word whose low 6 bits (<c>&amp; 0x3F</c>) are 16 (int16) or 32 (float) and whose bits 6..15 are stored as the byte <c>[R+0x56]</c>.
/// </summary>
/// <param name="RawFormat">The format word <c>u16 [fmt+8]</c>.</param>
/// <param name="Channels">The channel count <c>byte [fmt+4]</c>.</param>
/// <param name="SampleRate">The input rate <c>u32 [fmt+0]</c>.</param>
/// <param name="ChannelConfig">The whole word <c>u32 [fmt+4]</c> (optional; see <see cref="ChannelWord"/>).</param>
public readonly record struct WwiseResamplerFormat(int RawFormat, int Channels, int SampleRate, uint? ChannelConfig = null)
{
    /// <summary>The sample-format field of the format word (<c>byte [fmt+8] &amp; 0x3F</c>).</summary>
    public int SampleFormat => RawFormat & WwiseResampler.FormatMask;

    /// <summary>The whole channel-configuration word <c>u32 [fmt+4]</c> whose low byte is the channel count (the Hijack stores it as its out buffer's channel word, <c>0x8DBFA4</c>); the count itself when not given.</summary>
    public uint ChannelWord => ChannelConfig ?? (uint)(Channels & 0xFF);
}

/// <summary>
/// Wwise's <c>CAkResampler</c> at its engine offsets (M6-004; C38.2): the constructor <c>0xA46D70</c>, <c>Init</c> <c>0xA47038</c>, <c>SetPitch</c> <c>0xA47384</c> with its sibling <c>0xA4776C</c>, the format change <c>0xA47528</c>,
/// <c>Execute</c> <c>0xA47178</c> and the kernel table <c>0x103C0B8</c> (the 24 words, indexed by <c>kernel type + 8 * mode</c>), the factor <c>0xA46E30</c>. The state is the object's: the history array <c>[R+0x20]</c>, the input offset
/// <c>+0x24</c>, the output offset <c>+0x28</c>, the phase <c>+0x2C</c>, the current/target step and ramp counter <c>+0x30/+0x34/+0x38</c>, <c>48000 / outRate</c> <c>+0x3C</c>, the output limit <c>+0x40</c>, the mode <c>+0x48</c>,
/// the float ratio <c>+0x4C</c>, the last pitch <c>+0x50</c>, the kernel type, channel and block bytes <c>+0x54..+0x56</c> and the ratio-changed flag <c>+0x57</c>.
///
/// <para>Both stages use it: the voice's pitch node (<see cref="WwisePitchNodeIntake"/>, <c>R = node+8</c>) and the Hijack (<see cref="WwiseHijackFx"/>, <c>R = core+0x14</c>). The input and output buffers are the engine's 0x28-byte states
/// (<see cref="WwiseDecodeState"/>): float kernels read and write planar <c>float[]</c> (channel stride <c>u16 [buf+0xC]</c> frames), the int16 kernels read an interleaved <c>short[]</c> and write planar floats. The host stands in for the
/// engine's memory only where it cannot be modelled: <see cref="AllocationFails"/> is the pool allocator's failure.</para>
///
/// <para><b>Not read (visible stops).</b> The 3+ channel kernels (<c>0xA47824</c>, <c>0xA47B24</c>, <c>0xA47F1C</c>, <c>0xA48264</c>, <c>0xA486FC</c>: no shipped medium has them) throw
/// <see cref="WwiseMissingBehaviourException"/>; so does a kernel type outside the table (<c>0xFF</c>, the engine indexes past the 24 words) and an Execute before <c>Init</c> (the constructor leaves <c>+0x54..+0x56</c> unset).</para>
/// </summary>
public sealed class WwiseResampler
{
    /// <summary><c>Execute</c> result 0x2B (the kernel produced fewer than the limit: more input is needed).</summary>
    public const int DataNeeded = 0x2B;

    /// <summary><c>Execute</c> result 0x2D (the kernel produced exactly the remaining output frames).</summary>
    public const int DataReady = 0x2D;

    /// <summary><c>Execute</c> result 0x11 (the input held no frames, <c>0xA4717C..0xA47188</c>).</summary>
    public const int NoMoreData = 0x11;

    /// <summary>The constructor's phase <c>[R+0x2C] = 0x10000</c> (<c>0xA46D80..0xA46D88</c>); <c>Init</c> does not overwrite it.</summary>
    public const int InitialPhase = 0x10000;

    /// <summary><c>byte [fmt+8] &amp; 0x3F</c> (<c>0xA470D4..0xA470D8</c>).</summary>
    public const int FormatMask = 0x3F;

    /// <summary><c>fmt &amp; 0x3F == 0x10</c>: int16 samples (<c>0xA470DC</c>).</summary>
    public const int FormatInt16 = 16;

    /// <summary><c>fmt &amp; 0x3F == 0x20</c>: float samples (<c>0xA470F4</c>).</summary>
    public const int FormatFloat = 32;

    /// <summary>The ramp counter limit: Execute leaves mode 2 for mode 1 when <c>[R+0x38] &gt;= 0x400</c> (<c>0xA471F8..0xA471FC</c>).</summary>
    public const int RampSpan = 0x400;

    /// <summary>The step of mode 0 (<c>0xA47454</c>): <c>0x10000</c>.</summary>
    public const uint BypassStep = 0x10000;

    private static readonly float Q15 = BitConverter.Int32BitsToSingle(0x38000000);   // 2^-15 (0xA49128 and the other int16 kernels' literal)
    private static readonly float Q16 = BitConverter.Int32BitsToSingle(0x37800000);   // 2^-16 (the float kernels' weight scale, 0xA4A038)
    private static readonly float Q31 = BitConverter.Int32BitsToSingle(0x30000000);   // 2^-31 (the int16 scalar formula)
    private static readonly float Cents1200 = BitConverter.Int32BitsToSingle(0x44960000);   // 1200.0f (0xA47520)
    private static readonly float Scale65536 = BitConverter.Int32BitsToSingle(0x47800000);  // 65536.0f (0xA47524)
    private static readonly float Max16 = BitConverter.Int32BitsToSingle(0x46FFFE00);       // 32767.0f (0xA47528.. history write-back)
    private static readonly float Min16 = BitConverter.Int32BitsToSingle(unchecked((int)0xC7000000));   // -32768.0f

    /// <summary>The kernel type tables <c>0xFFD450</c> (int16 at +0, float at +8): channels 1, 2, 3..4 (and >4 by the caller's rule).</summary>
    private static readonly byte[] KernelTypeInt16 = { 0, 1, 2, 2 };
    private static readonly byte[] KernelTypeFloat = { 4, 5, 6, 6 };

    private byte[]? _history = new byte[0x20];   // [R+0x20]: R itself (0x20 bytes) until Init; null for a pool allocation that failed
    private bool _initialised;

    /// <summary>The pool allocator's failure (host input): called once per pool allocation <c>Init</c> makes (more than 8 channels, <c>0xA4707C</c>); true fails it. Null: the pool never fails.</summary>
    public Func<bool>? AllocationFails { get; set; }

    /// <summary><c>[R+0x24]</c>: the input offset into the held input buffer, in frames.</summary>
    public uint InputOffset24 { get; set; }

    /// <summary><c>[R+0x28]</c>: the output write offset, in frames (the owner resets it: <c>0xA52800</c>, <c>0x8DBFC2</c>, <c>0x8DC02C</c>).</summary>
    public uint OutputOffset28 { get; set; }

    /// <summary><c>[R+0x2C]</c>: the 16.16 phase.</summary>
    public uint Phase2C { get; set; } = InitialPhase;

    /// <summary><c>[R+0x30]</c>: the current step.</summary>
    public uint Step30 { get; set; }

    /// <summary><c>[R+0x34]</c>: the target step.</summary>
    public uint Target34 { get; set; }

    /// <summary><c>[R+0x38]</c>: the ramp counter.</summary>
    public uint Counter38 { get; set; }

    /// <summary><c>[R+0x3C]</c>: <c>48000 / outRate</c> as the unsigned integer division.</summary>
    public uint StepScale3C { get; set; }

    /// <summary><c>[R+0x40]</c>: the output frame limit, written by the owner before Execute.</summary>
    public uint Limit40 { get; set; }

    /// <summary><c>byte [R+0x44]</c>: 1 when the history is a pool allocation.</summary>
    public byte PoolHistory44 { get; private set; }

    /// <summary><c>[R+0x48]</c>: 0 step 0x10000, 1 fixed step, 2 ramp.</summary>
    public uint Mode48 { get; set; }

    /// <summary><c>[R+0x4C]</c>: the float ratio inRate/outRate (the constructor stores 1.0f).</summary>
    public float Ratio4C { get; private set; } = 1f;

    /// <summary><c>[R+0x50]</c>: the last cents (the constructor stores 0).</summary>
    public float Pitch50 { get; private set; }

    /// <summary><c>byte [R+0x54]</c>: the kernel type (0..7, or 0xFF).</summary>
    public byte KernelType54 { get; set; }

    /// <summary><c>byte [R+0x55]</c>: the channel count.</summary>
    public byte Channels55 { get; private set; }

    /// <summary><c>byte [R+0x56]</c>: bits 6..15 of the format word.</summary>
    public byte Byte56 { get; private set; }

    /// <summary><c>byte [R+0x57]</c>: the ratio-changed / first-call flag (the constructor stores 1).</summary>
    public byte Flag57 { get; set; } = 1;

    /// <summary>The history array as the engine holds it (<c>[R+0x20]</c>: float words, or int16 halfwords for the int16 kernels).</summary>
    public Span<byte> History => _history ?? throw new InvalidOperationException("the resampler's history allocation failed (Init returned 2): [R+0x20] is null");

    /// <summary>The mode (alias of <see cref="Mode48"/>).</summary>
    public int Mode => (int)Mode48;

    /// <summary>The ratio (alias of <see cref="Ratio4C"/>).</summary>
    public float Ratio => Ratio4C;

    /// <summary>The current step (alias of <see cref="Step30"/>).</summary>
    public uint CurrentStep => Step30;

    /// <summary>The target step (alias of <see cref="Target34"/>).</summary>
    public uint TargetStep => Target34;

    /// <summary><c>48000 / outRate</c> (alias of <see cref="StepScale3C"/>).</summary>
    public int StepScale => (int)StepScale3C;

    /// <summary>
    /// <c>Init(fmt, outRate)</c> <c>0xA47038</c>: more than 8 channels allocate the history from the pool (<c>channels * 4</c> bytes for float, <c>* 2</c> otherwise, 16-aligned; a failure stores a null history and returns 2),
    /// else the history is <c>R</c> itself; <c>[R+0x3C] = 48000 / outRate</c> (unsigned), <c>[R+0x55] = channels</c>, <c>[R+0x56] = (u16 fmt &gt;&gt; 6) &amp; 0x3FF</c>, <c>[R+0x4C] = float(u32 rate) / float(u32 outRate)</c> and the
    /// kernel type (<c>fmt &amp; 0x3F</c> 0x10: <c>T16[ch-1]</c>, ch-1 &gt; 3 gives 3; 0x20: <c>T32[ch-1]</c>, ch-1 &gt; 3 gives 7; else 0xFF). Returns 1, or 2 on the allocation failure. It does not write the phase, the steps or <c>[R+0x57]</c>.
    /// </summary>
    public int Init(WwiseResamplerFormat fmt, uint outRate)
    {
        if (outRate == 0) throw new ArgumentOutOfRangeException(nameof(outRate), "0xA470B0 divides 48000 by it (__aeabi_uidiv)");
        byte channels = unchecked((byte)fmt.Channels);                               // 0xA47040 ldrb r1,[r1,#4]
        if (channels > 8)                                                            // 0xA47054 bls 0xA4709C
        {
            int perChannel = fmt.SampleFormat == FormatFloat ? 4 : 2;               // 0xA47058..0xA47064 / 0xA4706C lsl #1 / 0xA47154 lsl #2
            if (AllocationFails?.Invoke() == true)                                  // 0xA4707C bl 0xA7A894; 0xA47084 str r0,[r4,#0x20]
            {
                _history = null;
                _initialised = false;
                return 2;                                                           // 0xA47088 moveq r0,#2
            }
            _history = new byte[channels * perChannel];
            PoolHistory44 = 1;                                                      // 0xA47090..0xA47094
        }
        else
        {
            _history = new byte[0x20];                                              // 0xA4709C str r0,[r0,#0x20] (r0 = R)
            PoolHistory44 = 0;                                                      // 0xA470A4
        }
        StepScale3C = 48000u / outRate;                                             // 0xA470A8..0xA470B0 + 0xA470E8
        Channels55 = channels;                                                      // 0xA470C0
        Byte56 = unchecked((byte)((fmt.RawFormat & 0xFFFF) >> 6 & 0x3FF));           // 0xA470C4..0xA470D0
        Ratio4C = (float)unchecked((uint)fmt.SampleRate) / (float)outRate;          // 0xA470B4..0xA470EC vcvt.f32.u32, vdiv.f32
        KernelType54 = TypeFor(fmt.SampleFormat, channels, (byte)0xFF);
        _initialised = true;
        return 1;                                                                   // 0xA47100 mov r0,#1
    }

    /// <summary>The kernel type of <c>0xA47038</c> / <c>0xA47528</c> for a sample format and channel count (<paramref name="other"/> when the format is neither 0x10 nor 0x20).</summary>
    private static byte TypeFor(int sampleFormat, byte channels, byte other)
    {
        int idx = (byte)(channels - 1);                                              // uxtb
        if (sampleFormat == FormatInt16) return idx <= 3 ? KernelTypeInt16[idx] : (byte)3;     // 0xA47134..0xA47150
        if (sampleFormat == FormatFloat) return idx <= 3 ? KernelTypeFloat[idx] : (byte)7;     // 0xA47110..0xA4712C
        return other;
    }

    /// <summary>
    /// <c>step(c)</c> at <c>0xA473E0..0xA4743C</c> (the same code at <c>0xA46DC0</c>, <c>0xA47474</c> and <c>0xA4776C</c>): <c>u32(double(f32(f32(ratio * powf(2, c / 1200)) * 65536)) + 0.5)</c> with the truncating, saturating
    /// <c>vcvt.u32.f64</c> (NaN gives 0); a zero result becomes <c>c &gt; 0 ? 0xFFFFFFFF : 1</c> (a NaN gives 1).
    /// </summary>
    private uint StepFor(float cents)
    {
        float q = cents / Cents1200;                                                // 0xA473F4 vdiv.f32
        float pow = WwiseHostMath.Powf(2f, q);                                      // 0xA473FC bl powf(2.0f, q)
        float a = Ratio4C * pow;                                                    // 0xA47408 vmul.f32
        float b = a * Scale65536;                                                   // 0xA4740C vmul.f32
        double d = (double)b + 0.5;                                                 // 0xA47414..0xA47418
        uint step = VcvtU32F64(d);                                                  // 0xA4741C
        if (step == 0) step = cents > 0f ? 0xFFFFFFFFu : 1u;                        // 0xA4742C..0xA47438 vcmpe.f32 s17,#0; mvngt / movle
        return step;
    }

    private static uint VcvtU32F64(double d)
    {
        if (double.IsNaN(d)) return 0;
        if (d <= 0) return 0;
        if (d >= 4294967296.0) return 0xFFFFFFFFu;
        return (uint)d;                                                             // round toward zero
    }

    /// <summary>
    /// <c>SetPitch(R, cents, flag)</c> <c>0xA47384</c>. With <c>[R+0x57] != 0</c> (the constructor, a ratio change): step, <c>[R+0x50] = c</c>, current = target = step, counter 0x400, <c>[R+0x57] = 0</c>, mode 0 or 1 by the step
    /// (a NaN re-enters the changed branch). Else the same cents (float compare) keep the mode by current against target (equal: 0 / 1, else 2); a changed pitch first folds the ramp of a mode 2 into the current step
    /// (<c>cur + trunc0((tgt - cur) * counter / 1024)</c>, signed 32-bit, a zero becomes 1), zeroes the counter, sets the target and <c>[R+0x50]</c>, then <paramref name="flag"/> false sets current = target and mode 0 / 1, true
    /// keeps the current step (mode 0 / 1 when it equals the target, else 2). The voice passes <c>(u16 [pbi+0x1BE] &amp; 0x380) == 0</c>, the format change true, the Hijack false.
    /// </summary>
    public void SetPitch(float cents, bool flag)
    {
        if (Flag57 != 0)                                                            // 0xA47398..0xA473A0
        {
            uint step0 = StepFor(cents);                                            // 0xA47474..0xA474C4
            Pitch50 = cents;                                                        // 0xA474D0
            Step30 = Target34 = step0;                                              // 0xA474D4, 0xA474D8
            Counter38 = 0x400;                                                      // 0xA474C8, 0xA474E0
            Flag57 = 0;                                                             // 0xA474E4
            if (!float.IsNaN(cents)) { SetModeByStep(Step30); return; }             // 0xA473A8 vcmp s17,s15 (= s17): equal, then 0xA473B4..0xA47450
        }
        else if (cents == Pitch50)                                                  // 0xA473A4..0xA473B0
        {
            if (Step30 == Target34) { SetModeByStep(Step30); return; }              // 0xA473B4..0xA473C0 beq 0xA47450
            Mode48 = 2;                                                             // 0xA473C8..0xA473CC
            return;
        }
        // 0xA473D4: the pitch changed
        if (Mode48 == 2)                                                            // 0xA473D4..0xA473DC beq 0xA474EC
        {
            int cur = unchecked((int)Step30);
            int span = unchecked((int)(Target34 - Step30));                         // 0xA474F8 rsb r3,r1,r3
            int product = unchecked((int)Counter38 * span);                         // 0xA474FC mul
            int biased = unchecked(product + 0x3FC + 3);                            // 0xA47500, 0xA47508
            if (product < 0) product = biased;                                      // 0xA47504 cmp; 0xA4750C movlt
            int folded = unchecked(cur + (product >> 10));                          // 0xA47510 adds r3,r1,r3,asr #10
            if (folded == 0) folded = 1;                                            // 0xA47514 moveq r3,#1
            Step30 = unchecked((uint)folded);                                       // 0xA47518
        }
        Counter38 = 0;                                                              // 0xA473EC
        uint step = StepFor(cents);                                                 // 0xA473E0..0xA4743C
        Target34 = step;                                                            // 0xA47440
        Pitch50 = cents;                                                            // 0xA47444
        if (!flag)                                                                  // 0xA47448 bne 0xA47468
        {
            Step30 = step;                                                          // 0xA4744C
            SetModeByStep(step);                                                    // 0xA47450..0xA47464
            return;
        }
        if (Step30 == step) SetModeByStep(Step30);                                  // 0xA47468..0xA473C0
        else Mode48 = 2;                                                            // 0xA473C4..0xA473CC
    }

    private void SetModeByStep(uint step) => Mode48 = step == BypassStep ? 0u : 1u;   // 0xA47454..0xA47460

    /// <summary>
    /// The sibling <c>0xA4776C(R, cents)</c> (a node <c>vt+0x10</c> body calls it, <c>0xA52888</c>): with <c>[R+0x57] == 0</c> and <c>[R+0x50] == c</c> (float compare) nothing changes; otherwise the step as above,
    /// <c>[R+0x50] = c</c>, current = target = step, counter 0x400 and <c>[R+0x57] = 0</c>, WITHOUT touching the mode.
    /// </summary>
    public void SetPitchSibling4776C(float cents)
    {
        if (Flag57 == 0 && Pitch50 == cents) return;                                // 0xA47774..0xA47790
        uint step = StepFor(cents);                                                 // 0xA47794..0xA477F4
        Pitch50 = cents;                                                            // 0xA47800
        Step30 = Target34 = step;                                                   // 0xA47804, 0xA47808
        Counter38 = 0x400;                                                          // 0xA4780C
        Flag57 = 0;                                                                 // 0xA47810
    }

    /// <summary>
    /// <c>0xA47528(R, F2, cents, OUT, outRate)</c> (the format change of the pending-source arm, <c>0xA52D0C</c>): the history is saved as floats (kernel type &lt;= 3: <c>int16 * 2^-15</c>; types 4..7: the raw floats), the ratio
    /// becomes <c>float(u32 F2.rate) / float(u32 outRate)</c> (a change sets <c>[R+0x57] = 1</c>), <c>SetPitch(cents, true)</c>, <c>[R+0x56] = (u16 fmt &gt;&gt; 6) &amp; 0x3FF</c>, the kernel type from the new format and the UNCHANGED channel
    /// count <c>[R+0x55]</c> (0x10: <c>T16</c>, ch-1 &gt; 3 gives 3; 0x20: <c>T32</c>, ch-1 &gt; 3 gives 7; else 0xFF), and the history is written back (types &lt;= 3: <c>clamp(trunc(f * 32767))</c> as int16, 32767.0 and a NaN give 0x7FFF,
    /// -32768.0 and below give 0x8000; types 4..7 the raw floats; type 0xFF: not written back). The input rate is not compared; the OUT argument (<c>r3</c>) is unused.
    /// </summary>
    public void FormatChangeA47528(WwiseResamplerFormat f2, float cents, uint outRate)
    {
        EnsureInitialised();
        int channels = Channels55;
        var h = History;
        var saved = new float[channels];
        if (KernelType54 <= 3)                                                      // 0xA47550 cmp r1,#3; bhi 0xA47648
        {
            for (int c = 0; c < channels; c++)
                saved[c] = (float)System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(h.Slice(2 * c, 2)) * Q15;   // 0xA47580..0xA47590
        }
        else if (KernelType54 - 4 <= 3 && channels != 0)                            // 0xA47648..0xA47668 memcpy(sp, [R+0x20], ch * 4)
        {
            for (int c = 0; c < channels; c++)
                saved[c] = System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(h.Slice(4 * c, 4));
        }
        float newRatio = (float)unchecked((uint)f2.SampleRate) / (float)outRate;    // 0xA4759C..0xA475B0
        if (!(Ratio4C == newRatio))                                                 // 0xA475B4 vcmp; vstrne
        {
            Ratio4C = newRatio;
            Flag57 = 1;                                                             // 0xA475C0..0xA475C4
        }
        SetPitch(cents, true);                                                      // 0xA475C8..0xA475D4 bl 0xA47384(R, cents, 1)
        Byte56 = unchecked((byte)((f2.RawFormat & 0xFFFF) >> 6 & 0x3FF));            // 0xA475D8..0xA475E0
        int fmt = f2.SampleFormat;
        byte type = TypeFor(fmt, Channels55, 0xFF);
        KernelType54 = type;                                                        // 0xA47604 / 0xA476B0 / 0xA47740
        if (fmt == FormatInt16)
        {
            if (type <= 3)                                                          // 0xA476AC cmp r3,#3; bhi 0xA4774C
            {
                for (int c = 0; c < channels; c++)                                  // 0xA476EC..0xA476E8
                {
                    float f = saved[c] * Max16;                                     // 0xA476FC vmul.f32 s15,s15,s14
                    short v;
                    if (f < Max16) v = f > Min16 ? unchecked((short)(int)f) : unchecked((short)0x8000);   // 0xA47700..0xA47720
                    else v = 0x7FFF;                                                // 0xA4770C movw #0x7fff (also NaN)
                    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(History.Slice(2 * c, 2), v);
                }
            }
        }
        else if (fmt == FormatFloat) WriteBackFloats(saved, type);                  // 0xA4766C..0xA47640
        // another format: type 0xFF, nothing written back (0xA47604..0xA4760C)
    }

    private void WriteBackFloats(float[] saved, byte type)
    {
        if (type - 4 > 3 || Channels55 == 0) return;                                // 0xA47608 cmp r3,#3; 0xA47614 cmp r3,#0
        for (int c = 0; c < Channels55; c++)                                        // 0xA47620..0xA4763C
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(History.Slice(4 * c, 4), saved[c]);
    }

    /// <summary>
    /// <c>0xA46E30(R)</c>: the current pitch factor <c>float(step) * 2^-16 / [R+0x4C]</c> (<c>vcvt.f32.u32 #16</c>, then <c>vdiv.f32</c>); the node stores its bits at <c>[node+0xA4]</c> after every Execute.
    /// </summary>
    public float CurrentFactorA46E30() => (float)Step30 * Q16 / Ratio4C;

    /// <summary><c>0xA47350(R)</c>: <c>[R+0x28] = [R+0x24] = 0</c> (the node's <c>vt+0x14</c> / <c>vt+0x1C</c> reset).</summary>
    public void ResetOffsetsA47350() { OutputOffset28 = 0; InputOffset24 = 0; }

    private void EnsureInitialised()
    {
        if (!_initialised)
            throw new WwiseMissingBehaviourException("M6-004 P1-14: the resampler constructor 0xA46D70 leaves [R+0x54..+0x56] unset; Init (0xA47038) must run before Execute / the format change");
    }

    /// <summary>
    /// <c>Execute(R, IN, OUT)</c> <c>0xA47178</c>: with <c>u16 [IN+0xE] == 0</c> it returns 0x11. Otherwise it loops: <c>table[byte [R+0x54] + 8 * [R+0x48]](IN, OUT, [R+0x40], R)</c>; after the call a mode 2 with
    /// <c>[R+0x38] &gt;= 0x400</c> becomes mode 1 with <c>[R+0x30] = [R+0x34]</c>; it returns the kernel's result when <c>u16 [IN+0xE] == 0</c> or <c>u16 [OUT+0xE] &gt;= [R+0x40]</c> (re-read each time), else it loops.
    /// </summary>
    public int Execute(WwiseDecodeState input, WwiseDecodeState output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        if (input.ValidFrames == 0) return NoMoreData;                               // 0xA47178..0xA47188
        EnsureInitialised();
        while (true)
        {
            var before = (input.ValidFrames, output.ValidFrames, InputOffset24, OutputOffset28, Phase2C, Counter38, Step30, Mode48);
            int result = RunKernel(input, output, KernelType54, Mode48);             // 0xA471D0..0xA471E8
            if (Mode48 == 2 && Counter38 >= RampSpan)                                // 0xA471EC..0xA47210
            {
                Mode48 = 1;
                Step30 = Target34;
            }
            if (input.ValidFrames == 0) return result;                               // 0xA471B4..0xA471BC
            if (output.ValidFrames >= Limit40) return result;                        // 0xA471C0..0xA471CC bhs 0xA47214
            if (before == (input.ValidFrames, output.ValidFrames, InputOffset24, OutputOffset28, Phase2C, Counter38, Step30, Mode48))
                throw new InvalidOperationException("M6-004 P1-14: the kernel made no progress (no input consumed, no output, no state change): the engine's Execute loop 0xA471D0..0xA471CC never ends here");
        }
    }

    private int RunKernel(WwiseDecodeState input, WwiseDecodeState output, byte type, uint mode)
    {
        uint limit = Limit40;
        if (type > 7 || mode > 2)
            throw new WwiseMissingBehaviourException($"M6-004 P1-14: kernel type 0x{type:X2} / mode {mode} indexes past the 24-word table 0x103C0B8 (the engine reads whatever follows)");
        bool isFloat = type >= 4;
        int channels = (type & 3) switch { 0 => 1, 1 => 2, _ => 3 };               // 2 and 3 (and 6, 7) are the 3+ channel rows
        if (mode == 0)
        {
            if (isFloat) return KernelA479F4(input, output, limit);                  // table words 4..7
            if (type == 0) return KernelA48F5C(input, output, limit);
            if (type == 1) return KernelA48C98(input, output, limit);
            throw ThreePlus(0xA47824, type, mode);
        }
        if (channels == 3) throw ThreePlus(mode == 1 ? (isFloat ? 0xA47F1Cu : 0xA47B24u) : (isFloat ? 0xA486FCu : 0xA48264u), type, mode);
        return mode == 1
            ? Interpolate(input, output, limit, isFloat, channels, ramp: false)      // 0xA4913C, 0xA49634, 0xA49E40, 0xA4A03C
            : Interpolate(input, output, limit, isFloat, channels, ramp: true);      // 0xA4A2D8, 0xA4A59C, 0xA4A958, 0xA4AC04
    }

    private static WwiseMissingBehaviourException ThreePlus(uint address, byte type, uint mode)
        => new($"M6-004 P1-14: the 3+ channel kernel 0x{address:X} (kernel type {type}, mode {mode}) is not read (no shipped medium has more than 2 channels)");

    // ------------------------------------------------------------------ the kernels
    //
    // Arguments (IN r0, OUT r1, L r2 = [R+0x40], R r3). rem = L - [R+0x28], A = [R+0x24], B = [R+0x28], V = u16 [IN+0xE], P = [R+0x2C], S = [R+0x30]. A kernel returns 0x2B when it produced fewer than rem frames (and stores
    // [R+0x28] = B + n) and 0x2D when it produced exactly rem ([R+0x28] is left to the owner).

    private static float[] FloatData(WwiseDecodeState s)
        => s.Data as float[] ?? throw new InvalidOperationException("a float kernel reads and writes planar float[] data (the state's data pointer is null or int16)");

    private static short[] Int16Data(WwiseDecodeState s)
        => s.Data as short[] ?? throw new InvalidOperationException("an int16 kernel reads interleaved short[] data (the state's data pointer is null or float)");

    /// <summary>The common tail of the mode 0 kernels: <c>u16 [IN+0xE] = V - n</c>, <c>u16 [OUT+0xE] = B + n</c>, <c>[R+0x2C] = 0x10000</c>, <c>[R+0x24] = (V == n) ? 0 : A + n</c>, 0x2B with <c>[R+0x28] = B + n</c> or 0x2D.</summary>
    private int FinishMode0(WwiseDecodeState input, WwiseDecodeState output, uint v, uint n, uint a, uint b, uint rem)
    {
        input.ValidFrames = unchecked((ushort)(v - n));
        output.ValidFrames = unchecked((ushort)(n + b));
        Phase2C = 0x10000;
        InputOffset24 = v == n ? 0 : unchecked(a + n);
        if (rem != n)
        {
            OutputOffset28 = unchecked(b + n);
            return DataNeeded;
        }
        return DataReady;
    }

    /// <summary>The float bypass <c>0xA479F4</c> (any channel count): per channel a copy of <c>n = min(rem, V)</c> frames; the history takes the last frame of each channel.</summary>
    private int KernelA479F4(WwiseDecodeState input, WwiseDecodeState output, uint limit)
    {
        uint a = InputOffset24, b = OutputOffset28, rem = unchecked(limit - b), v = input.ValidFrames;
        uint n = rem < v ? rem : v;                                                  // 0xA47A14 cmp r2,r3; movlo r3,r1
        int channels = (byte)input.ChannelConfig;                                    // 0xA47A10 ldrb r8,[sl,#4]
        var src = FloatData(input);
        var dst = FloatData(output);
        int inStride = input.MaxFrames, outStride = output.MaxFrames;               // u16 [IN+0xC], u16 [OUT+0xC]
        var hist = _history ?? throw new InvalidOperationException("no history");
        for (int c = 0; c < channels; c++)
        {
            long from = (long)inStride * c + a;
            long to = (long)outStride * c + b;
            if (n != 0) Array.Copy(src, from, dst, to, n);                          // 0xA47A94 memcpy
            if (n == 0 && from + n - 1 < 0) throw new WwiseMissingBehaviourException("M6-004 P1-16b: the float bypass with no frames reads the history from before the block");
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(hist.AsSpan(4 * c, 4), src[from + n - 1]);   // 0xA47A9C..0xA47AB0
        }
        return FinishMode0(input, output, v, n, a, b, rem);
    }

    /// <summary>The int16 "mono" bypass <c>0xA48F5C</c>: one linear run of <c>channels * n</c> int16 values (channels from <c>byte [IN+4]</c>) converted by <c>2^-15</c>; the history takes the last frame's halfwords.</summary>
    private int KernelA48F5C(WwiseDecodeState input, WwiseDecodeState output, uint limit)
    {
        uint a = InputOffset24, b = OutputOffset28, rem = unchecked(limit - b), v = input.ValidFrames;
        uint n = rem < v ? rem : v;                                                  // 0xA48F74 cmp r2,ip; movlo / movhs
        int channels = (byte)input.ChannelConfig;                                    // 0xA48F70 ldrb sb,[r0,#4]
        var src = Int16Data(input);
        var dst = FloatData(output);
        long total = (long)channels * n;
        long from = (long)channels * a, to = (long)channels * b;
        for (long i = 0; i < total; i++)
            dst[to + i] = (float)src[from + i] * Q15;                                // 0xA49000..0xA49084 vcvt.f32.s32, vmul.f32
        var hist = _history ?? throw new InvalidOperationException("no history");
        for (int i = 0; i < channels; i++)                                           // 0xA490B4..0xA490C4: the last frame's halfwords
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(hist.AsSpan(2 * i, 2), src[from + total - channels + i]);
        return FinishMode0(input, output, v, n, a, b, rem);
    }

    /// <summary>
    /// The int16 stereo bypass <c>0xA48C98</c>: the first <c>16 * (n &gt;&gt; 4)</c> frames go through NEON, whose right channel is <c>vrshr.s32 #16</c> of the packed word, i.e. <c>right + (left_u16 &gt;= 0x8000 ? 1 : 0)</c> (one LSB high whenever the
    /// left sample is negative); the last <c>n &amp; 15</c> frames are exact. The history takes the last frame's two halfwords unchanged.
    /// </summary>
    private int KernelA48C98(WwiseDecodeState input, WwiseDecodeState output, uint limit)
    {
        uint a = InputOffset24, b = OutputOffset28, rem = unchecked(limit - b), v = input.ValidFrames;
        uint n = rem < v ? rem : v;                                                  // 0xA48CBC..0xA48CD8
        var src = Int16Data(input);
        var dst = FloatData(output);
        int outStride = output.MaxFrames;                                            // u16 [OUT+0xC]
        long from = (long)a * 2;
        if (n == 0 && from - 2 < 0) throw new WwiseMissingBehaviourException("M6-004 P1-16b: the int16 stereo bypass with no frames reads the history from before the block");
        var hist = _history ?? throw new InvalidOperationException("no history");
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(hist.AsSpan(0, 2), src[from + 2 * (long)n - 2]);   // 0xA48D0C, 0xA48D24
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(hist.AsSpan(2, 2), src[from + 2 * (long)n - 1]);   // 0xA48D38, 0xA48D48
        uint vector = (n >> 4) << 4;
        for (uint k = 0; k < n; k++)
        {
            int left = src[from + 2 * k], right = src[from + 2 * k + 1];
            int shownRight = k < vector ? right + (((ushort)left) >= 0x8000 ? 1 : 0) : right;   // 0xA48D80..0xA48E44 vshl / vrshr.s32
            dst[b + k] = (float)left * Q15;                                          // 0xA48DA8 / 0xA48EC8
            dst[(long)outStride + b + k] = (float)shownRight * Q15;                  // 0xA48DAC / 0xA48ECC
        }
        return FinishMode0(input, output, v, n, a, b, rem);
    }

    // ---- the interpolating kernels (modes 1 and 2)

    /// <summary>One channel's sample access for the interpolating kernels (float planar or int16 interleaved).</summary>
    private readonly struct Samples
    {
        private readonly float[]? _f;
        private readonly short[]? _i;
        private readonly int _stride;
        private readonly int _channels;

        public Samples(WwiseDecodeState s, bool isFloat, int channels)
        {
            if (isFloat) { _f = FloatData(s); _i = null; }
            else { _i = Int16Data(s); _f = null; }
            _stride = s.MaxFrames;
            _channels = channels;
        }

        public float F(int c, long frame) => _f![(long)_stride * c + frame];
        public int I(int c, long frame) => _i![frame * _channels + c];
    }

    private static float LerpFloat(float l, float r, uint f)
    {
        float w = (float)f * Q16;                                                    // vcvt.f32.s32; vmul.f32 (exact: f &lt;= 0xFFFF)
        float d = r - l;                                                             // vsub.f32
        float p = w * d;                                                             // vmla.f32: the product rounded ...
        return l + p;                                                                // ... then the add rounded
    }

    private static float ScalarInt16(int l, int r, uint f)
        => (float)unchecked((int)(((uint)l << 16) + (uint)((r - l) * (int)f))) * Q31;   // mla (x1-x0)*f + (x0<<16) in 32 bits; vcvt.f32.s32; vmul 2^-31

    private static float NeonInt16(int l, int r, uint f)
    {
        float lf = l, rf = r;
        float w = (float)f * Q16;                                                    // vcvt.f32.s32 q9; vmul.f32 q9,q9,q12
        float d = rf - lf;                                                           // vsub.f32 q10,q10,q8
        float p = d * w;                                                             // vmul.f32 q9,q10,q9
        float s = p + lf;                                                            // vadd.f32 q8,q9,q8
        return s * Q15;                                                              // vmul.f32 q8,q8,q11
    }

    private float HistFloat(int c) => System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian((_history ?? throw new InvalidOperationException("no history")).AsSpan(4 * c, 4));
    private int HistInt16(int c) => System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian((_history ?? throw new InvalidOperationException("no history")).AsSpan(2 * c, 2));

    /// <summary>
    /// The interpolating kernels of mode 1 (fixed step; <c>0xA4913C</c> int16 mono, <c>0xA49634</c> int16 stereo, <c>0xA49E40</c> float mono, <c>0xA4A03C</c> float stereo) and mode 2 (the ramp; <c>0xA4A2D8</c>, <c>0xA4A59C</c>,
    /// <c>0xA4A958</c>, <c>0xA4AC04</c>). Mode 1: <c>n1 = min(rem, (S + 0xFFFF - P) / S)</c> outputs with the history as the left sample and <c>in[A]</c> as the right (weight <c>(P + k*S) &amp; 0xFFFF</c>), then
    /// <c>n2 = min(rem - n1, (S - 1 + (V &lt;&lt; 16) - P1) / S)</c> outputs from <c>in[A-1+i]</c>, <c>in[A+i]</c> with <c>i = (P1 + k*S) &gt;&gt; 16</c>; the int16 kernels compute the first <c>4 * ((n2-5)/4 + 1)</c> outputs of phase 2 (when
    /// <c>n2 &gt; 4</c>) with the NEON float formula, everything else with the scalar <c>float(int32((l &lt;&lt; 16) + (r - l) * f)) * 2^-31</c>. Mode 2: <c>q = (0x400 - c) / fp</c>, <c>maxN = min(rem, q)</c>, and the phase advances by
    /// <c>((tgt - cur) * (c + (k+1) * fp) + (cur &lt;&lt; 10)) &gt;&gt; 10</c> after every output (u32 wrap), the loop ending at <c>maxN</c> outputs or when the integer part of the phase exceeds <c>V - 1</c>; the counter stored is
    /// <c>c + n * fp</c>. The tail: <c>r4 = min(P2 &gt;&gt; 16, V)</c>, the history takes <c>in[A-1+r4]</c> when <c>r4 != 0</c>, <c>[R+0x2C] = P2 - (r4 &lt;&lt; 16)</c>, <c>u16 [IN+0xE] = V - r4</c>, <c>u16 [OUT+0xE] = n + B</c>,
    /// <c>[R+0x24] = (r4 == V) ? 0 : r4 + A</c>, then 0x2B (with <c>[R+0x28] = n + B</c>) or 0x2D.
    /// </summary>
    private int Interpolate(WwiseDecodeState input, WwiseDecodeState output, uint limit, bool isFloat, int channels, bool ramp)
    {
        uint a = InputOffset24, b = OutputOffset28, rem = unchecked(limit - b), v = input.ValidFrames, p = Phase2C;
        var src = new Samples(input, isFloat, channels);
        var dst = FloatData(output);
        int outStride = output.MaxFrames;
        void Put(int c, long index, float value) => dst[(long)outStride * c + index] = value;

        uint n;
        uint phaseEnd;
        uint counterEnd = Counter38;
        if (!ramp)
        {
            uint s = Step30;
            if (s == 0) throw new InvalidOperationException("the fixed step is zero: 0xA4916C divides by it");
            uint n1max = unchecked((s + 0xFFFF - p) / s);                            // 0xA49E5C..0xA49E88 __aeabi_uidiv
            uint n1 = rem < n1max ? rem : n1max;
            for (uint k = 0; k < n1; k++)                                            // phase 1: the history is the left sample
            {
                uint f = unchecked(p + k * s) & 0xFFFF;
                for (int c = 0; c < channels; c++)
                {
                    float o = isFloat ? LerpFloat(HistFloat(c), src.F(c, a), f) : ScalarInt16(HistInt16(c), src.I(c, a), f);
                    Put(c, b + k, o);
                }
            }
            uint p1 = unchecked(p + n1 * s);
            uint n2max = unchecked((s - 1 + (v << 16) - p1) / s);                    // 0xA49F28..0xA49F48
            uint rest = unchecked(rem - n1);
            uint n2 = rest < n2max ? rest : n2max;
            uint neon = !isFloat && n2 > 4 ? 4 * ((n2 - 5) / 4 + 1) : 0;             // 0xA49298..0xA492A0 cmp r3,#4; bls; the loop of 4 per iteration while the count left is above 4
            for (uint k = 0; k < n2; k++)                                            // phase 2: in[A-1+i], in[A+i]
            {
                uint ph = unchecked(p1 + k * s);
                long i = ph >> 16;
                uint f = ph & 0xFFFF;
                for (int c = 0; c < channels; c++)
                {
                    float o;
                    if (isFloat) o = LerpFloat(src.F(c, (long)a - 1 + i), src.F(c, (long)a + i), f);
                    else o = k < neon ? NeonInt16(src.I(c, (long)a - 1 + i), src.I(c, (long)a + i), f) : ScalarInt16(src.I(c, (long)a - 1 + i), src.I(c, (long)a + i), f);
                    Put(c, b + n1 + k, o);
                }
            }
            n = n1 + n2;
            phaseEnd = unchecked(p1 + n2 * s);
        }
        else
        {
            uint fp = StepScale3C;
            if (fp == 0) throw new InvalidOperationException("the ramp increment [R+0x3C] is zero (48000 / outRate with outRate > 48000): 0xA4A9F0 divides by it");
            uint c0 = Counter38, cur = Step30, diff = unchecked(Target34 - Step30), baseNum = cur << 10;
            uint q = unchecked((0x400 - c0) / fp);                                   // 0xA4A9D0..0xA4A9F0
            uint maxN = rem < q ? rem : q;
            uint ph = p;
            n = 0;
            counterEnd = c0;
            while (n < maxN)
            {
                long i = ph >> 16;
                if (i > (long)(uint)(v - 1)) break;                                  // 0xA4AAA0 cmp r1,r5; 0xA4AB7C cmp r5,r1; bhi: the integer part above V - 1
                uint f = ph & 0xFFFF;
                for (int c = 0; c < channels; c++)
                {
                    float o;
                    if (isFloat) o = LerpFloat(i == 0 ? HistFloat(c) : src.F(c, (long)a - 1 + i), src.F(c, (long)a + i), f);
                    else o = ScalarInt16(i == 0 ? HistInt16(c) : src.I(c, (long)a - 1 + i), src.I(c, (long)a + i), f);
                    Put(c, b + n, o);
                }
                counterEnd = unchecked(c0 + (n + 1) * fp);
                uint num = unchecked(diff * counterEnd + baseNum);
                ph = unchecked(ph + (num >> 10));
                n++;
            }
            Counter38 = counterEnd;                                                  // 0xA4AAAC str sb,[r3,#0x38]
            phaseEnd = ph;
        }

        uint r4 = phaseEnd >> 16;
        if (r4 >= v) r4 = v;                                                         // 0xA49FC4 cmp r4,sl; movhs r4,sl
        if (r4 != 0)                                                                 // 0xA49FD4..0xA49FEC
        {
            var hist = _history ?? throw new InvalidOperationException("no history");
            for (int c = 0; c < channels; c++)
            {
                if (isFloat) System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(hist.AsSpan(4 * c, 4), src.F(c, (long)a - 1 + r4));
                else System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(hist.AsSpan(2 * c, 2), (short)src.I(c, (long)a - 1 + r4));
            }
        }
        Phase2C = unchecked(phaseEnd - (r4 << 16));                                  // 0xA49FF0..0xA49FF4
        input.ValidFrames = unchecked((ushort)(v - r4));                             // 0xA4A000
        output.ValidFrames = unchecked((ushort)(n + b));                             // 0xA4A008
        InputOffset24 = r4 == v ? 0 : unchecked(r4 + a);                             // 0xA4A004, 0xA4A010..0xA4A018
        if (n != rem)                                                                // 0xA4A01C..0xA4A028
        {
            OutputOffset28 = unchecked(b + n);
            return DataNeeded;
        }
        return DataReady;
    }
}
