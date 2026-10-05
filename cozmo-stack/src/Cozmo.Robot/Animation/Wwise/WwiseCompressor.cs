// fidelity: M6-013, M6-022
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The Compressor's parameter object (<c>0x1C</c> bytes, vptr <c>0x101C9A8</c>; created by <c>0xAA0808</c>, cloned by <c>0xAA05BC</c>). The engine's slots: <c>vt+8</c> SetParam <c>0xAA0660</c>, <c>vt+0xC</c> clone <c>0xAA05BC</c>, <c>vt+0x10</c> <c>0xAA07A0</c>
/// (a zero size installs the defaults, otherwise it forwards to <c>vt+0x18</c>), <c>vt+0x14</c> destroy <c>0xAA0614</c>, <c>vt+0x18</c> SetParamsBlock <c>0xAA0734</c>.
/// <para><b>Field names are inferred from the use sites</b> in <c>0xA9FB28</c>, <c>0xA9FC70</c> and <c>0xAA0298</c> (the artifact carries none): <c>+4</c> threshold (dB), <c>+8</c> ratio, <c>+0xC</c> attack time (s), <c>+0x10</c> release time (s), <c>+0x14</c> the make-up gain as a LINEAR
/// factor (<c>powf(10, dB * 0.05f)</c>), byte <c>+0x18</c> the "process LFE" flag, byte <c>+0x19</c> the "channel link" flag. The 22-byte bank block is <c>{f32 x5, u8, u8}</c> and the block's fifth float is the make-up in dB.</para>
/// <para>A fresh object (<c>0xAA0808</c> sets only the vptr) holds uninitialised pool memory; a field read before any store throws <see cref="WwiseMissingBehaviourException"/>.</para>
/// </summary>
public sealed class WwiseCompressorParams
{
    /// <summary>The block size the bank carries and <c>0xAA0734</c> reads (words 0..4 and bytes <c>0x14</c>, <c>0x15</c>); it ignores the size argument.</summary>
    public const int BlockSize = 22;

    /// <summary>The defaults <c>0xAA07AC..0xAA07EC</c> installs for a zero-size block: <c>-12.0f</c>, <c>4.0f</c>, <c>0.01f</c>, <c>0.1f</c>, <c>1.0f</c> (make-up, linear), bytes 1 and 1 (as bit patterns).</summary>
    public static readonly uint[] DefaultWords = { 0xC1400000, 0x40800000, 0x3C23D70A, 0x3DCCCCCD, 0x3F800000 };

    private const uint PointZeroFive = 0x3D4CCCCD;   // 0xAA0730 / 0xAA079C: the literal 0.05f

    private readonly uint[] _word = new uint[5];     // +4, +8, +0xC, +0x10, +0x14
    private readonly byte[] _byte = new byte[2];     // +0x18, +0x19
    private int _assigned;                           // bit k: field k stored (0..4 words, 5..6 bytes)

    private uint Word(int k) => (_assigned >> k & 1) != 0 ? _word[k] : throw new WwiseMissingBehaviourException(
        $"M6-013 T-F4: the parameter object's word at +0x{4 + 4 * k:X} was read before any store (uninitialised pool memory)");

    private byte Byte(int k) => (_assigned >> (5 + k) & 1) != 0 ? _byte[k] : throw new WwiseMissingBehaviourException(
        $"M6-013 T-F4: the parameter object's byte at +0x{0x18 + k:X} was read before any store (uninitialised pool memory)");

    /// <summary><c>[+4]</c> (threshold, dB).</summary>
    public float Threshold => BitConverter.UInt32BitsToSingle(Word(0));

    /// <summary><c>[+8]</c> (ratio).</summary>
    public float Ratio => BitConverter.UInt32BitsToSingle(Word(1));

    /// <summary><c>[+0xC]</c> (attack, seconds).</summary>
    public float Attack => BitConverter.UInt32BitsToSingle(Word(2));

    /// <summary><c>[+0x10]</c> (release, seconds).</summary>
    public float Release => BitConverter.UInt32BitsToSingle(Word(3));

    /// <summary><c>[+0x14]</c> (the make-up gain, linear).</summary>
    public float Makeup => BitConverter.UInt32BitsToSingle(Word(4));

    /// <summary><c>[+0x14]</c> as the word the engine copies.</summary>
    public uint MakeupBits => Word(4);

    /// <summary>byte <c>[+0x18]</c> (copied to <c>[this+0x38]</c> by Init).</summary>
    public byte Byte18 => Byte(0);

    /// <summary>byte <c>[+0x19]</c> (the worker choice input of Init).</summary>
    public byte Byte19 => Byte(1);

    /// <summary>Creates the parameter object (<c>0xAA0808</c>): the 0x1C-byte allocation <c>alloc-&gt;vt+8</c> and the vptr; null when the allocation fails.</summary>
    public static WwiseCompressorParams? Create(IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        return alloc.Allocate(0x1C) ? new WwiseCompressorParams() : null;       // 0xAA080C..0xAA081C
    }

    /// <summary>The clone <c>vt+0xC</c> (<c>0xAA05BC</c>): allocates 0x1C bytes (null on failure) and copies the 24 bytes after the vptr.</summary>
    public WwiseCompressorParams? Clone(IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        if (!alloc.Allocate(0x1C)) return null;                                 // 0xAA05CC..0xAA05DC
        var c = new WwiseCompressorParams { _assigned = _assigned };
        Array.Copy(_word, c._word, _word.Length);                               // 0xAA05EC..0xAA0604
        Array.Copy(_byte, c._byte, _byte.Length);
        return c;
    }

    /// <summary>The destroy <c>vt+0x14</c> (<c>0xAA0614</c>): a null object returns 1; otherwise the object is freed through <c>alloc-&gt;vt+0xC</c> and 1 is returned.</summary>
    public static int Destroy(WwiseCompressorParams? p, IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        if (p is not null) alloc.Free(p);                                       // 0xAA0618..0xAA0640
        return 1;                                                               // 0xAA0644
    }

    /// <summary>
    /// <c>vt+8</c> SetParam (<c>0xAA0660</c>): a null value or an id above 6 returns 0x1F; ids 0..3 store the 4-byte value at <c>+4</c>, <c>+8</c>, <c>+0xC</c>, <c>+0x10</c>; id 4 stores <c>powf(10.0f, f * 0.05f)</c> at <c>+0x14</c> (the host powf); id 5 stores the byte <c>+0x18</c>, id 6 the byte <c>+0x19</c>. Returns 1.
    /// </summary>
    public int SetParam(uint id, byte[]? value)
    {
        if (value is null) return 0x1F;                                         // 0xAA0660 cmp r2,#0; beq 0xAA0690
        if (id > 6) return 0x1F;                                                // 0xAA0668 cmp r1,#6; addls pc / b 0xAA0690
        if (id <= 4 && value.Length < 4 || id >= 5 && value.Length < 1)
            throw new ArgumentException("the engine reads 4 bytes (ids 0..4) or 1 byte (ids 5, 6) at the value pointer", nameof(value));
        if (id <= 3) Store((int)id, BitConverter.ToUInt32(value, 0));            // 0xAA06AC..0xAA06E4
        else if (id == 4) StoreMakeup(BitConverter.ToUInt32(value, 0));          // 0xAA06EC..0xAA0718
        else StoreByte((int)id - 5, value[0]);                                  // 0xAA069C, 0xAA0720
        return 1;
    }

    /// <summary>
    /// <c>vt+0x10</c> (<c>0xAA07A0</c>): a zero <paramref name="size"/> installs the defaults (<see cref="DefaultWords"/>, bytes 1 and 1) and returns 1; otherwise it forwards to <see cref="SetParamsBlock"/> (<c>vt+0x18</c>, <c>0xAA07F4..0xAA0804</c>).
    /// </summary>
    public int SetParamsBlockOrDefaults(byte[]? block, uint size)
    {
        if (size != 0) return SetParamsBlock(block);                            // 0xAA07A0 cmp r3,#0; bne 0xAA07F4
        for (int k = 0; k < 5; k++) Store(k, DefaultWords[k]);                  // 0xAA07AC..0xAA07EC
        StoreByte(0, 1);
        StoreByte(1, 1);
        return 1;                                                               // 0xAA07C4 mov r0,#1
    }

    /// <summary>
    /// <c>vt+0x18</c> SetParamsBlock (<c>0xAA0734</c>): words 0..3 of the 22-byte block go to <c>+4</c>, <c>+8</c>, <c>+0xC</c>, <c>+0x10</c>; <c>+0x14 = powf(10.0f, word4 * 0.05f)</c>; the bytes at block <c>0x14</c> and <c>0x15</c> go to <c>+0x18</c> and <c>+0x19</c>. Returns 1.
    /// The engine reads 22 bytes whatever the size argument says, so a shorter block is refused.
    /// </summary>
    public int SetParamsBlock(byte[]? block)
    {
        if (block is null || block.Length < BlockSize)
            throw new WwiseMissingBehaviourException("M6-013 T-F4: 0xAA0734 reads 22 bytes at the block pointer whatever the size argument says; a shorter block is not an engine input");
        for (int k = 0; k < 4; k++) Store(k, BitConverter.ToUInt32(block, 4 * k));      // 0xAA0758..0xAA0770
        StoreMakeup(BitConverter.ToUInt32(block, 16));                                  // 0xAA0768..0xAA0790
        StoreByte(0, block[0x14]);                                                      // 0xAA0780, 0xAA0788
        StoreByte(1, block[0x15]);                                                      // 0xAA0784, 0xAA078C
        return 1;
    }

    private void Store(int k, uint v)
    {
        _word[k] = v;
        _assigned |= 1 << k;
    }

    private void StoreByte(int k, byte v)
    {
        _byte[k] = v;
        _assigned |= 1 << (5 + k);
    }

    /// <summary><c>powf(10.0f, f * 0.05f)</c> (<c>0xAA0704</c> / <c>0xAA0774</c>: the float product, then the call with <c>r0 = 0x41200000</c>).</summary>
    private void StoreMakeup(uint dbBits)
    {
        float scaled = BitConverter.UInt32BitsToSingle(dbBits) * BitConverter.UInt32BitsToSingle(PointZeroFive);
        Store(4, BitConverter.SingleToUInt32Bits(WwiseHostMath.Powf(10f, scaled)));
    }
}

/// <summary>
/// The Compressor plug-in (<c>0x006C0003</c>, ARM, vptr <c>0x103DF28</c>; 0x3C-byte object created by <c>0xAA0538</c>): the voice insert-FX / bus effect that carries 1872 of the Cozmo.bnk Sounds' slot 0 and 5 of SFX.bnk's. Slots: <c>+0</c> <c>0xA9FA10</c> (<c>bx lr</c>), <c>+4</c> <c>0xA9FB14</c> (deleting destructor:
/// <c>operator delete(this)</c>), <c>+8</c> Term <c>0xA9FA14</c>, <c>+0xC</c> Reset <c>0xA9FA68</c>, <c>+0x10</c> GetPluginInfo <c>0xA9FAEC</c>, <c>+0x14</c>/<c>+0x18</c> the stubs returning 0, <c>+0x1C</c> Init <c>0xA9FB28</c>, <c>+0x20</c> Execute <c>0xA9FC70</c>, <c>+0x24</c> <c>0xAA05B0</c>.
/// <para><b>Fields</b> (the engine's object): <c>+4</c> the parameter object, <c>+8</c>/<c>+0xC</c> the worker member pointer (Init always stores a direct pointer, <c>+0xC = 0</c>), <c>+0x10</c> the previous make-up gain (the ramp start), <c>+0x14</c> the channels, <c>+0x18</c> the rate, <c>+0x1C</c> the number of state pairs,
/// <c>+0x20</c> the power smoothing coefficient <c>expf(-1.0f / (rate * 0.02322f))</c>, <c>+0x24</c> the state array (pairs <c>{g, p}</c>, 8 bytes each), <c>+0x28</c>/<c>+0x2C</c> the attack time and its coefficient <c>expf(-2.2f / (rate * attack))</c>, <c>+0x30</c>/<c>+0x34</c> the release time and coefficient, byte <c>+0x38</c> the LFE flag.</para>
/// <para><b>Workers.</b> <c>m = (channels == 1)</c>; <c>m &lt; byte[params+0x19]</c> (unsigned) selects the linked worker <c>0xA9FEEC</c> (state 8 bytes, <c>+0x1C = 1</c>), otherwise the per-channel worker <c>0xAA0298</c> with <c>channels * 8</c> state bytes when that byte is 0, else 8 bytes. Both workers are built: the per-channel one
/// (mono, and stereo without the link flag) and the linked one <see cref="RunLinkedWorker"/> (C41.5, row 3.5).</para>
/// <para><b>Numerics.</b> binary32 throughout, non-fused <c>vmla/vmls/vnmls</c> (the product rounded, then the add), scalar VFP operations IEEE; the NEON q-register operations of the make-up stage (<c>vmul.f32</c>, <c>vadd.f32</c>) flush denormal operands and results to zero (a NaN's payload is not modelled: it compares as NaN).
/// The one host seam is <see cref="WwiseHostMath.Expf"/>. The state memory the engine allocates is not zeroed (<c>0xA9FBEC</c> only allocates); the owner calls <see cref="Reset"/> next (the in-place wrapper's tail call <c>0xA79334</c>), and Execute before that throws.</para>
/// </summary>
public sealed class WwiseCompressor : IWwiseEffectPlugin
{
    /// <summary>The plug-in id of the Compressor in the bank (<c>0x006C0003</c>).</summary>
    public const uint PluginId = 0x006C0003;

    /// <summary>The vptr (<c>0x103DF28</c>).</summary>
    public const uint Vptr = 0x103DF28;

    /// <summary>Create <c>0xAA0538</c>.</summary>
    public const uint CreateAddress = 0xAA0538;

    /// <summary>Params create <c>0xAA0808</c>.</summary>
    public const uint ParamsCreateAddress = 0xAA0808;

    /// <summary>Init's allocation failure result (<c>0xA9FBFC moveq r3,#0x34</c>).</summary>
    public const int AllocationFailed = 0x34;

    /// <summary>The worker the object's member pointer names.</summary>
    public enum WorkerKind
    {
        /// <summary>Init has not run (the engine's <c>+8</c> is uninitialised).</summary>
        Unset = 0,

        /// <summary><c>0xAA0298</c>: one state pair per channel.</summary>
        PerChannel = 1,

        /// <summary><c>0xA9FEEC</c>: one gain from the summed squares of all channels (C41.5).</summary>
        Linked = 2,
    }

    private static float Bits(uint b) => BitConverter.UInt32BitsToSingle(b);

    private static readonly float MinusTwoPointTwo = Bits(0xC00CCCCD);   // 0xA9FC60, 0xAA0534
    private static readonly float PowerScale = Bits(0x3CBE37DF);         // 0xA9FC64 (0.02322f)
    private static readonly float Tiny = Bits(0x15F79688);               // 0xAA0500 (1e-25f)
    private static readonly float OneThird = Bits(0x3EAAAAAB);           // 0xAA0504
    private static readonly float Ln2 = Bits(0x3F317218);                // 0xAA050C
    private static readonly float Log10E = Bits(0x3EDE5BD9);             // 0xAA0510
    private static readonly float Point05 = Bits(0x3D4CCCCD);            // 0xAA0518
    private static readonly float MinusThirtySeven = Bits(0xC2140000);   // 0xAA051C
    private static readonly float PowScale = Bits(0x4BD49A78);           // 0xAA0520
    private static readonly float PowBase = Bits(0x4E7E0000);            // 0xAA0524
    private static readonly float PolyC6 = Bits(0x3EA67F46);             // 0xAA0528
    private static readonly float PolyC7 = Bits(0x3CAA70DE);             // 0xAA052C
    private static readonly float PolyC8 = Bits(0x3F272DDB);             // 0xAA0530

    private bool _initialised;
    private bool _stateReady;
    private bool _terminated;
    private WwiseCompressorParams? _params;     // +4
    private WorkerKind _worker;                 // +8 / +0xC
    private float _previousMakeup;              // +0x10
    private uint _channels;                     // +0x14
    private uint _rate;                         // +0x18
    private uint _stateCount;                   // +0x1C
    private float _powerCoef;                   // +0x20
    private float[]? _state;                    // +0x24
    private float _attackSetting;               // +0x28
    private float _attackCoef;                  // +0x2C
    private float _releaseSetting;              // +0x30
    private float _releaseCoef;                 // +0x34
    private byte _lfe;                          // +0x38

    private WwiseCompressor()
    {
    }

    /// <summary>
    /// Create <c>0xAA0538(alloc)</c>: allocates 0x3C bytes through <c>alloc-&gt;vt+8</c> (null returns null), then stores <c>[+4] = [+8] = [+0xC] = [+0x24] = 0</c> and the vptr. The other fields are uninitialised until <see cref="Init"/>.
    /// </summary>
    public static WwiseCompressor? Create(IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        return alloc.Allocate(0x3C) ? new WwiseCompressor() : null;            // 0xAA053C..0xAA0550
    }

    /// <summary><c>+4</c>: the parameter object (null until Init).</summary>
    public WwiseCompressorParams? Params => _params;

    /// <summary>The worker the member pointer names.</summary>
    public WorkerKind Worker => _worker;

    /// <summary><c>+0x10</c>: the make-up gain the next Execute ramps from.</summary>
    public float PreviousMakeup => _previousMakeup;

    /// <summary><c>+0x14</c>.</summary>
    public uint Channels => _channels;

    /// <summary><c>+0x18</c>.</summary>
    public uint Rate => _rate;

    /// <summary><c>+0x1C</c>: the number of state pairs.</summary>
    public uint StateCount => _stateCount;

    /// <summary><c>+0x20</c>.</summary>
    public float PowerCoef => _powerCoef;

    /// <summary><c>+0x28</c>.</summary>
    public float AttackSetting => _attackSetting;

    /// <summary><c>+0x2C</c>.</summary>
    public float AttackCoef => _attackCoef;

    /// <summary><c>+0x30</c>.</summary>
    public float ReleaseSetting => _releaseSetting;

    /// <summary><c>+0x34</c>.</summary>
    public float ReleaseCoef => _releaseCoef;

    /// <summary>byte <c>+0x38</c>.</summary>
    public byte LfeFlag => _lfe;

    /// <summary>The state array as <c>{g, p}</c> pairs (<c>[+0x24]</c>), or null when the allocation failed; a copy.</summary>
    public float[]? StateSnapshot => _state?.ToArray();

    /// <inheritdoc />
    public int Slot14() => 0;                                                   // 0x8DBF38

    /// <inheritdoc />
    public int Slot18() => 0;                                                   // 0x8DBF3C

    /// <inheritdoc />
    public int Slot24() => 0x2D;                                                // 0xAA05B0

    /// <inheritdoc />
    public int GetPluginInfo(out WwisePluginInfo info)
    {
        info = new WwisePluginInfo(3, 0x7E002, 1, 0);                           // 0xA9FAEC..0xA9FB0C
        return 1;
    }

    /// <summary>
    /// Term <c>0xA9FA14(this, alloc)</c>: frees the state array when <c>[+0x24] != 0</c> (<c>alloc-&gt;vt+0xC</c>), runs the destructor <c>vt+0</c> (<c>0xA9FA10</c>, a <c>bx lr</c>), frees the object and returns 1. The deleting destructor <c>vt+4</c> (<c>0xA9FB14</c>) is
    /// <c>operator delete(this)</c>: the managed object has nothing to do there.
    /// </summary>
    public int Term(IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        if (_state is not null) alloc.Free(_state);                             // 0xA9FA1C..0xA9FA38
        alloc.Free(this);                                                       // 0xA9FA4C..0xA9FA5C
        _terminated = true;
        return 1;                                                               // 0xA9FA60
    }

    /// <summary>
    /// Reset <c>0xA9FA68</c>: <c>[+0x1C]</c> state pairs of two floats at <c>[+0x24]</c> are zeroed (a zero count writes nothing); returns 1. Reset before Init reads the uninitialised <c>[+0x1C]</c>; an allocation-failed Init leaves a null state pointer that the zeroing would
    /// write through: both stop visibly.
    /// </summary>
    public int Reset()
    {
        if (!_initialised) throw new WwiseMissingBehaviourException("M6-013 T-F1: Reset (0xA9FA68) reads [this+0x1C], which only Init (0xA9FB28) writes: the engine reads uninitialised pool memory");
        if (_stateCount != 0)
        {
            if (_state is null) throw new InvalidOperationException("0xA9FA68 writes through the null state pointer an allocation-failed Init left in [this+0x24]");
            Array.Clear(_state);                                                // 0xA9FA90..0xA9FAD8: count * 8 bytes
            _stateReady = true;
        }
        return 1;                                                               // 0xA9FAE4
    }

    /// <inheritdoc />
    public int Init(IWwisePluginMemAlloc alloc, object? ctx, object? parameters, WwiseEffectFormat fmt)
    {
        if (parameters is not WwiseCompressorParams p) throw new ArgumentException("the Compressor's parameter object is a WwiseCompressorParams", nameof(parameters));
        return Init(alloc, ctx, p, fmt);
    }

    /// <summary>
    /// Init <c>0xA9FB28(this, alloc, ctx, params, fmt)</c> (<paramref name="ctx"/> is not read): <c>[+4] = params</c>, <c>[+0x18] = rate</c>, <c>[+0x14] = byte [fmt+4]</c>, byte <c>[+0x38] = byte [params+0x18]</c>, <c>[+0x28] = attack</c>, <c>[+0x2C] = expf(-2.2f / (float(rate) * attack))</c>, <c>[+0x30] = release</c>,
    /// <c>[+0x34]</c> likewise; the worker choice and the state allocation (8 bytes for the linked worker and for a mono per-channel one with the link byte set, <c>channels * 8</c> otherwise; <c>[+0x1C]</c> is the pair count); an allocation failure returns 0x34 with <c>[+0x24] = 0</c>;
    /// otherwise <c>[+0x20] = expf(-1.0f / (float(rate) * 0.02322f))</c>, <c>[+0x10] = word [params+0x14]</c> and the result is 1.
    /// </summary>
    public int Init(IWwisePluginMemAlloc alloc, object? ctx, WwiseCompressorParams p, WwiseEffectFormat fmt)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        ArgumentNullException.ThrowIfNull(p);
        _params = p;                                                            // 0xA9FB3C
        float attack = p.Attack;                                                // 0xA9FB40 vldr s14,[r3,#0xc]
        uint rate = fmt.SampleRate;                                             // 0xA9FB44, 0xA9FB50
        byte lfe = p.Byte18;                                                    // 0xA9FB54
        byte link = p.Byte19;                                                   // 0xA9FB5C
        uint makeupBits = p.MakeupBits;                                         // 0xA9FB60
        float release = p.Release;                                              // 0xA9FB64
        _channels = fmt.Channels;                                               // 0xA9FB68
        _rate = rate;
        _lfe = lfe;                                                             // 0xA9FB6C
        _attackSetting = attack;                                                // 0xA9FB74
        _attackCoef = WwiseHostMath.Expf(MinusTwoPointTwo / ((float)rate * attack));          // 0xA9FB70..0xA9FB84, 0xA9FB9C
        _releaseSetting = release;                                              // 0xA9FB8C
        _releaseCoef = WwiseHostMath.Expf(MinusTwoPointTwo / ((float)rate * release));        // 0xA9FB90..0xA9FBA4, 0xA9FBBC

        uint m = _channels == 1 ? 1u : 0u;                                      // 0xA9FBA8..0xA9FBB4 (clz of channels - 1)
        int size;
        if (m < link)                                                           // 0xA9FBB8 cmp r2,r6; bhs 0xA9FC3C
        {
            _worker = WorkerKind.Linked;                                        // 0xA9FBC4..0xA9FBD4 (0xA9FEEC)
            size = 8;
            _stateCount = 1;                                                    // 0xA9FBD8, 0xA9FBDC, 0xA9FBE8
        }
        else
        {
            _worker = WorkerKind.PerChannel;                                    // 0xA9FC3C..0xA9FC54 (0xAA0298)
            if (link == 0) { size = (int)(_channels * 8); _stateCount = _channels; }    // 0xA9FC50 lsleq r1,r3,#3; beq 0xA9FBE0
            else { size = 8; _stateCount = 1; }                                 // 0xA9FC5C b 0xA9FBD8
        }

        _initialised = true;
        if (!alloc.Allocate(size))                                              // 0xA9FBEC..0xA9FBF4: alloc->vt+8(alloc, size)
        {
            _state = null;                                                      // 0xA9FBF8 str r0,[r4,#0x24]
            _stateReady = false;
            return AllocationFailed;                                            // 0xA9FBFC moveq r3,#0x34
        }
        _state = new float[size / 4];
        _stateReady = false;                                                    // the allocation is not zeroed
        _powerCoef = WwiseHostMath.Expf(-1f / ((float)rate * PowerScale));      // 0xA9FC04..0xA9FC20, 0xA9FC2C
        _previousMakeup = BitConverter.UInt32BitsToSingle(makeupBits);          // 0xA9FC28 str r7,[r4,#0x10]
        return 1;                                                               // 0xA9FC24
    }

    private static float Z(float x) => float.IsSubnormal(x) ? (BitConverter.SingleToUInt32Bits(x) >> 31 != 0 ? -0f : 0f) : x;

    /// <summary>The saturating <c>vcvt.u32.f32</c> (round toward zero; NaN and negatives give 0).</summary>
    private static uint SaturatingU32(float v)
    {
        if (float.IsNaN(v) || v <= 0f) return 0;
        if (v >= 4294967296f) return uint.MaxValue;
        return (uint)v;
    }

    /// <summary>
    /// Execute <c>0xA9FC70(this, S)</c>: with <c>u16 [S+0xE] == 0</c> it returns at once (nothing, not even the make-up state, changes). Otherwise it copies the parameters (<c>0xA9FCB4..0xA9FCCC</c>), calls the worker, then applies the make-up gain with the NEON ramp (below) and stores
    /// <c>[+0x10] = g1</c>. The audio is <c>[S]</c> as planar floats with the plane stride <c>u16 [S+0xC]</c> frames and <c>u16 [S+0xE]</c> valid frames.
    /// <para>Make-up stage: <c>chs = byte [S+4]</c> minus 1 when <c>u32 [S+4] &amp; 0x8000</c> and the LFE byte <c>[+0x38]</c> is 0; <c>g0 = [+0x10]</c>, <c>g1 = [params+0x14]</c>. <c>g0 == g1</c> (a NaN is not equal): nothing when <c>chs == 0</c> or <c>g0 == 1.0f</c>, else every frame of
    /// every channel is multiplied by <c>g0</c> (the first <c>4 * (n / 4)</c> frames by NEON, the rest scalar). <c>g0 != g1</c>: per channel the first <c>4 * (n / 4)</c> frames use the lane gains <c>{g0, g0 + d, (g0 + d) + d, ((g0 + d) + d) + d}</c> with <c>d = (g1 - g0) / float(4 * (n / 4))</c>,
    /// advanced by <c>d * 4.0f</c> per block of four; the tail restarts at <c>g0</c> with the step <c>(g1 - g0) / float(n)</c> (also when <c>n &lt; 4</c>, where the whole plane is the tail).</para>
    /// </summary>
    public void Execute(WwiseDecodeState s)
    {
        ArgumentNullException.ThrowIfNull(s);
        int n = s.ValidFrames;                                                  // 0xA9FC7C ldrh r3,[r1,#0xe]
        if (n == 0) return;                                                     // 0xA9FC8C cmp r3,#0; beq 0xA9FDA0
        if (!_initialised || _params is null || _terminated)
            throw new WwiseMissingBehaviourException("M6-013 T-F6: Execute (0xA9FC70) on an object Init has not filled (the engine reads uninitialised pool memory) or after Term");
        var p = _params;
        float g1 = p.Makeup;                                                    // 0xA9FCFC vldr s11,[fp,#-0x24]: local[4] = [params+0x14]
        float p0 = p.Threshold, p1 = p.Ratio, p2 = p.Attack, p3 = p.Release;    // the 24-byte copy 0xA9FCB4..0xA9FCCC

        switch (_worker)                                                        // 0xA9FCA0..0xA9FCE8: the member pointer, direct (Init stores +0xC = 0)
        {
            case WorkerKind.PerChannel:
                RunPerChannelWorker(s, p0, p1, p2, p3);
                break;
            case WorkerKind.Linked:
                RunLinkedWorker(s, p0, p1, p2, p3);                              // 0xA9FEEC (C41.5, row 3.5)
                break;
            default:
                throw new WwiseMissingBehaviourException("M6-013 T-F6: Execute before Init (the engine's worker pointer [this+8] is uninitialised)");
        }

        uint cfg = s.ChannelConfig;                                             // 0xA9FCF8, 0xA9FD04
        uint chs = cfg & 0xFF;
        if (_lfe == 0 && (cfg & 0x8000) != 0) chs--;                            // 0xA9FCEC..0xA9FD0C
        if (chs == uint.MaxValue)
            throw new InvalidOperationException("byte [S+4] is 0 with the 0x8000 flag: the engine's channel count wraps to 0xFFFFFFFF and the loop runs over unmapped memory");

        float g0 = _previousMakeup;                                             // 0xA9FCF0 vldr s12,[r5,#0x10]
        int stride = s.MaxFrames;                                               // u16 [S+0xC]
        float[] data = DataOf(s, chs, stride, n);

        if (g0 == g1)                                                           // 0xA9FD10 vcmp.f32 s12,s11; bne 0xA9FDA8
        {
            if (chs != 0 && g0 != 1f)                                           // 0xA9FD24, 0xA9FD4C
            {
                int lr = n >> 2;
                for (uint c = 0; c < chs; c++)
                {
                    int b = (int)c * stride;
                    int i = 0;
                    for (; i < 4 * lr; i++) data[b + i] = Z(Z(data[b + i]) * Z(g0));      // 0xA9FEBC..0xA9FEC4 vmul.f32 q8,q8,q9 (NEON)
                    for (; i < n; i++) data[b + i] = data[b + i] * g0;                    // 0xA9FD74..0xA9FD84 vmul.f32 s15,s15,s14 (VFP)
                }
            }
        }
        else if (chs != 0)                                                      // 0xA9FDA8 cmp r0,#0; beq 0xA9FD9C
        {
            int lr = n >> 2;
            float d1 = (g1 - g0) / (float)n;                                    // 0xA9FDF0..0xA9FDF8
            for (uint c = 0; c < chs; c++)
            {
                int b = (int)c * stride;
                int i = 0;
                if (lr != 0)                                                    // 0xA9FDD0 cmp lr,#0; bne 0xA9FE28
                {
                    float d = (g1 - g0) / (float)(4 * lr);                      // 0xA9FE28, 0xA9FE38, 0xA9FE3C
                    var lane = new float[4];
                    lane[0] = g0;                                               // 0xA9FE30
                    lane[1] = g0 + d;                                           // 0xA9FE40
                    lane[2] = d + lane[1];                                      // 0xA9FE44
                    lane[3] = d + lane[2];                                      // 0xA9FE4C
                    float step = d * 4f;                                        // 0xA9FE54
                    for (int blk = 0; blk < lr; blk++)                          // 0xA9FE70..0xA9FE88
                    {
                        for (int k = 0; k < 4; k++, i++) data[b + i] = Z(Z(data[b + i]) * Z(lane[k]));        // vmul.f32 q8,q8,q9
                        for (int k = 0; k < 4; k++) lane[k] = Z(Z(lane[k]) + Z(step));                       // vadd.f32 q9,q9,q10
                    }
                }
                float gain = g0;                                                // 0xA9FDFC vmov.f32 s14,s12: the tail restarts at g0
                for (; i < n; i++)                                              // 0xA9FE00..0xA9FE14
                {
                    data[b + i] = data[b + i] * gain;                           // vmul.f32 s15,s15,s14
                    gain = gain + d1;                                           // vadd.f32 s14,s14,s13
                }
            }
        }
        _previousMakeup = g1;                                                   // 0xA9FD98..0xA9FD9C
    }

    private float[] DataOf(WwiseDecodeState s, uint chs, int stride, int n)
    {
        if (s.Data is not float[] data) throw new InvalidOperationException("[S] must be the planar float buffer (the engine dereferences it)");
        if (chs != 0 && (long)(chs - 1) * stride + n > data.Length)
            throw new InvalidOperationException("the engine would read past the audio buffer: channels * stride + frames exceed it");
        return data;
    }

    /// <summary>
    /// The linked worker <c>0xA9FEEC(this, S, P)</c> (C41.5, row 3.5): one gain for all channels. <c>n = [+0x14]</c>, minus 1 when <c>u32 [S+4] &amp; 0x8000</c> and byte <c>[+0x38] == 0</c> (<c>0xA9FEFC..0xA9FF18</c>, <c>0xAA01E4</c>); <c>inv = 1.0f / float(n)</c> (unsigned convert; <c>n == 0</c> gives +inf),
    /// <c>k = 1.0f / P[1] - 1.0f</c>. Attack and release refresh the cached coefficients exactly as the per-channel worker does (<c>0xAA0224..0xAA0248</c>, <c>0xAA01F8..0xAA0220</c>; a NaN time differs). The state pair is <c>{g, ms}</c> at <c>[+0x24]</c>. Per frame:
    /// <c>sum</c> = the non-fused <c>vmla</c> chain of <c>x_c * x_c</c> over the <c>n</c> planes from <c>0.0f</c> (<c>0xAA0088..0xAA00A0</c>; none when <c>n == 0</c>, <c>0xAA01DC</c>); <c>m = 1e-25f + sum * inv</c> (for <c>n == 0</c> that is <c>0 * inf</c> = NaN);
    /// <c>ms = m + (ms - m) * [+0x20]</c>; the fast log of <c>ms</c> as the per-channel worker; <c>lvl = 10.0f * L - P[0]</c> (<c>vnmls</c>); <c>over = lvl</c> when <c>lvl &gt; 0</c>, else <c>0.0f</c> (a NaN gives 0); the coefficient is the release's when <c>over - g &lt; 0</c> or unordered, else the attack's;
    /// <c>g = over + c * (g - over)</c>; <c>y = (g * k) * 0.05f</c>; <c>lin = 0</c> when <c>y &lt; -37.0f</c>, else the fast power; every plane <c>c &lt; n</c> frame is multiplied by <c>lin</c> in place, plane by plane. The plane pointer table (<c>0xA9FFC8..0xAA0048</c>, NEON integer <c>vmla.i32</c> blocks of four plus a scalar
    /// tail) is integer arithmetic only: <c>ptr[c] = pData + 4 * c * u16 [S+0xC]</c>, so there is no flush-to-zero there and the float arithmetic is all scalar VFP (IEEE). After the loop <c>st[1] = ms</c> and <c>st[0] = g</c> (<c>0xAA01C0..0xAA01C4</c>).
    /// </summary>
    private void RunLinkedWorker(WwiseDecodeState s, float p0, float p1, float p2, float p3)
    {
        uint n = _channels;                                                     // 0xA9FF1C ldr r4,[r5,#0x14]
        if ((s.ChannelConfig & 0x8000) != 0 && _lfe == 0) n--;                  // 0xA9FF04..0xA9FF18, 0xAA01E4..0xAA01E8
        float one = 1f;                                                         // 0xA9FF20 vmov.f32 s15,#1.0
        float k = one / p1;                                                     // 0xA9FF34 vdiv.f32 s17,s15,s17
        k = k - one;                                                            // 0xA9FF44 vsub.f32 s17,s17,s15
        float inv = one / (float)n;                                             // 0xA9FF3C vcvt.f32.u32 s16,s12; 0xA9FF4C vdiv.f32 s16,s15,s16
        if (p2 != _attackSetting)                                               // 0xA9FF40 vcmp.f32 s14,s13; bne 0xAA0224
        {
            _attackSetting = p2;                                                // 0xAA0228
            _attackCoef = WwiseHostMath.Expf(MinusTwoPointTwo / (p2 * (float)_rate));         // 0xAA022C..0xAA0244
        }
        if (p3 != _releaseSetting)                                              // 0xA9FF60 vcmp.f32 s15,s14; bne 0xAA01F8
        {
            _releaseSetting = p3;                                               // 0xAA01FC
            _releaseCoef = WwiseHostMath.Expf(MinusTwoPointTwo / (p3 * (float)_rate));        // 0xAA0200..0xAA0218
        }
        if (n == uint.MaxValue) throw new InvalidOperationException("the linked worker's channel count wraps to 0xFFFFFFFF (a zero-channel object with the LFE flag): the engine builds a pointer table of 2^32 entries on a stack block of 8 bytes");

        int frames = s.ValidFrames;                                             // 0xAA0054 ldrh r0,[r6,#0xe]
        int stride = s.MaxFrames;                                               // 0xA9FFB8 ldrh r7,[r6,#0xc] (only read when n != 0)
        if (_state is null || !_stateReady || _state.Length < 2)
            throw new WwiseMissingBehaviourException("M6-013 T-F7/C41.5: the linked worker reads {g, ms} at [this+0x24] (0xA9FF90, 0xA9FF98): Init's allocation is not zeroed (Reset must run first) and an allocation-failed Init leaves it null");
        float[]? data = n == 0 ? null : DataOf(s, n, stride, frames);           // 0xA9FF7C cmp r4,#0; beq 0xAA004C: the table is not built for n == 0

        float g = _state[0];                                                    // 0xA9FF98 vldr s13,[lr]
        float ms = _state[1];                                                   // 0xA9FF90 vldr s12,[lr,#4]
        for (int i = 0; i < frames; i++)                                        // 0xAA0078 subs r0,r0,#1; blo 0xAA01C0
        {
            float sum = 0f;                                                     // 0xAA0088 / 0xAA01DC (0.0f, literal 0xAA0260)
            for (uint c = 0; c < n; c++)                                        // 0xAA0090..0xAA00A0
            {
                float x = data![(int)c * stride + i];
                float xx = x * x;
                sum = sum + xx;                                                 // 0xAA009C vmla.f32 s14,s15,s15
            }
            float prodSum = sum * inv;
            float m = Tiny + prodSum;                                           // 0xAA00A4..0xAA00A8 s15 = 1e-25f; vmla.f32 s15,s14,s16
            float diff = ms - m;                                                // 0xAA00AC vsub.f32 s12,s12,s15
            float prodMs = diff * _powerCoef;
            ms = m + prodMs;                                                    // 0xAA00B0 vmla.f32 s15,s12,s10; 0xAA00BC s12 = s15
            uint bits = BitConverter.SingleToUInt32Bits(ms);                    // 0xAA00B4
            uint mant = bits & 0x7FFFFF;                                        // 0xAA00C0 ubfx r2,r3,#0,#0x17
            int e = (int)((bits >> 23) & 0xFF);                                 // 0xAA00C4 ubfx r3,r3,#0x17,#8
            float f = BitConverter.UInt32BitsToSingle(mant + 0x3F800000);       // 0xAA00CC, 0xAA00D0
            float t = (f - 1f) / (f + 1f);                                      // 0xAA00D8, 0xAA00DC, 0xAA00E0
            float t2 = t * t;                                                   // 0xAA00E4
            float ef = (float)e - 127f;                                         // 0xAA00D4 vcvt.f32.s32; 0xAA00E8 (127.0f 0xAA026C)
            float prodPoly = t2 * OneThird;
            float poly = 1f + prodPoly;                                         // 0xAA00EC vmla.f32 s20,s0,s4
            float lnE = ef * Ln2;                                               // 0xAA00F0
            float t2x = t + t;                                                  // 0xAA00F4
            float prodLn = t2x * poly;
            float sumLn = lnE + prodLn;                                         // 0xAA00F8 vmla.f32 s15,s14,s20
            float lg = sumLn * Log10E;                                          // 0xAA00FC
            float prodL = lg * 10f;                                             // 0xAA0100, 0xAA0108 vnmls.f32 s0,s15,s14 with s0 = threshold
            float lvl = prodL - p0;
            float over = lvl > 0f ? lvl : 0f;                                   // 0xAA010C..0xAA0118: vcmpe; vmovle s15,s2 (a NaN is "le")
            float rise = over - g;                                              // 0xAA011C
            float fall = g - over;                                              // 0xAA0120
            float cf = rise >= 0f ? _attackCoef : _releaseCoef;                 // 0xAA0124..0xAA0130: vmovlt (release), vmovge (attack); unordered is lt
            float prodG = cf * fall;
            g = over + prodG;                                                   // 0xAA0134 vmla.f32 s15,s14,s13
            float y = g * k;                                                    // 0xAA0138
            y = y * Point05;                                                    // 0xAA013C
            float lin;
            if (y < MinusThirtySeven) lin = 0f;                                 // 0xAA0140..0xAA014C bmi 0xAA01D4 (s14 = 0.0f)
            else
            {
                float scaled = y * PowScale;                                    // 0xAA0158 vmla.f32 s15,s14,s1
                float word = PowBase + scaled;
                uint u = SaturatingU32(word);                                   // 0xAA0164 vcvt.u32.f32
                float m2 = BitConverter.UInt32BitsToSingle((u & 0x7FFFFF) + 0x3F800000);       // 0xAA016C, 0xAA0174, 0xAA017C
                float expWord = BitConverter.UInt32BitsToSingle((u >> 23) << 23);              // 0xAA0170, 0xAA0178, 0xAA0188
                float c7 = PolyC7 + m2 * PolyC6;                                // 0xAA0180 vmla.f32 s0,s15,s20
                float c8 = PolyC8 + m2 * c7;                                    // 0xAA0184 vmla.f32 s14,s15,s0
                lin = c8 * expWord;                                             // 0xAA018C
            }
            for (uint c = 0; c < n; c++)                                        // 0xAA0198..0xAA01B4
            {
                int idx = (int)c * stride + i;
                data![idx] = data[idx] * lin;                                   // 0xAA01A4 vmul.f32 s15,s15,s14
            }
        }
        _state[1] = ms;                                                         // 0xAA01C0 vstr s12,[lr,#4]
        _state[0] = g;                                                          // 0xAA01C4 vstr s13,[lr]
    }

    /// <summary>
    /// The per-channel worker <c>0xAA0298(this, S, P)</c> (mono and unlinked stereo). Attack and release refresh the cached coefficients when <c>P[2]</c> or <c>P[3]</c> differs (a NaN differs); the channel count is <c>[+0x14]</c> minus 1 when <c>u32 [S+4] &amp; 0x8000</c> and <c>[+0x38] == 0</c>.
    /// Per frame: <c>q = 1e-25f + x*x</c>; <c>p = q + (p - q) * [+0x20]</c>; the log approximation <c>lg = ((float(e) - 127.0f) * ln2 + 2t(1 + t*t/3)) * log10(e)</c> with <c>e</c> the exponent byte and <c>t = (m - 1) / (m + 1)</c> for the mantissa <c>m</c> in [1, 2) of <c>p</c>;
    /// <c>over = 10 * lg - P[0]</c> (the product rounded, then the subtraction), 0 unless <c>over &gt; 0</c>; the coefficient is the attack's when <c>over - g &gt;= 0</c> and the release's otherwise (a NaN is a release); <c>g = over + a * (g - over)</c>;
    /// <c>y = (g * (1/ratio - 1)) * 0.05f</c>; <c>lin = 0</c> when <c>y &lt; -37.0f</c>, else the engine's fast power; the output is <c>x * lin</c> in place. The pair <c>{g, p}</c> is stored back after each channel.
    /// </summary>
    private void RunPerChannelWorker(WwiseDecodeState s, float p0, float p1, float p2, float p3)
    {
        float slope = 1f / p1;                                                  // 0xAA02BC vdiv.f32 s15,s16,s14
        slope = slope - 1f;                                                     // 0xAA02C8 vsub.f32 s16,s15,s16
        if (p2 != _attackSetting)                                               // 0xAA02C4 vcmp.f32 s13,s12; bne 0xAA04D4
        {
            _attackSetting = p2;                                                // 0xAA04D8
            _attackCoef = WwiseHostMath.Expf(MinusTwoPointTwo / (p2 * (float)_rate));         // 0xAA04DC..0xAA04F4
        }
        if (p3 != _releaseSetting)                                              // 0xAA02E0 vcmp.f32 s15,s14; bne 0xAA04A8
        {
            _releaseSetting = p3;                                               // 0xAA04AC
            _releaseCoef = WwiseHostMath.Expf(MinusTwoPointTwo / (p3 * (float)_rate));        // 0xAA04B0..0xAA04C8
        }
        uint cfg = s.ChannelConfig;                                             // 0xAA02F0
        uint nch = _channels;                                                   // 0xAA030C ldr r0,[r5,#0x14]
        if ((cfg & 0x8000) != 0 && _lfe == 0) nch--;                            // 0xAA02F4..0xAA0308, 0xAA049C
        if (nch == 0) return;                                                   // 0xAA0310 cmp r0,#0; beq 0xAA0494
        if (nch == uint.MaxValue) throw new InvalidOperationException("the worker's channel count wraps to 0xFFFFFFFF (a zero-channel object with the LFE flag): the engine loops over unmapped memory");

        int frames = s.ValidFrames;                                             // 0xAA0318
        int stride = s.MaxFrames;                                               // 0xAA0320
        if (_state is null || !_stateReady || (long)nch * 2 > _state.Length)
            throw new WwiseMissingBehaviourException("M6-013 T-F7: the worker reads the state pairs at [this+0x24]: Init's allocation is not zeroed (Reset must run first) and an allocation-failed Init leaves it null");
        float[] data = DataOf(s, nch, stride, frames);

        for (int c = 0; c < nch; c++)                                           // 0xAA0354..0xAA048C
        {
            float g = _state[2 * c];                                            // 0xAA0360 vldr s13,[ip,#-4]
            float pw = _state[2 * c + 1];                                       // 0xAA035C vldr s10,[ip]
            int b = c * stride;
            for (int i = 0; i < frames; i++)                                    // 0xAA0378..0xAA0478
            {
                float x = data[b + i];                                          // 0xAA0378
                float xx = x * x;
                float q = Tiny + xx;                                            // 0xAA0384 vmla.f32 s15,s12,s12 (s15 = 1e-25f)
                float diff = pw - q;                                            // 0xAA0398
                float prodP = diff * _powerCoef;
                pw = q + prodP;                                                 // 0xAA039C vmla.f32 s15,s10,s22
                uint bits = BitConverter.SingleToUInt32Bits(pw);                // 0xAA03A0
                uint mant = bits & 0x7FFFFF;                                    // 0xAA03B0 ubfx r2,r3,#0,#0x17
                int e = (int)((bits >> 23) & 0xFF);                             // 0xAA03B4 ubfx r3,r3,#0x17,#8
                float m = BitConverter.UInt32BitsToSingle(mant + 0x3F800000);   // 0xAA03B8..0xAA03C0
                float t = (m - 1f) / (m + 1f);                                  // 0xAA03C4..0xAA03CC
                float t2 = t * t;                                               // 0xAA03D0
                float ef = (float)e - 127f;                                     // 0xAA03D4, 0xAA03D8
                float poly = 1f + t2 * OneThird;                                // 0xAA03DC vmla.f32 s26,s25,s20
                float lnE = ef * Ln2;                                           // 0xAA03E0
                float t2x = t + t;                                              // 0xAA03E4
                float sum = lnE + t2x * poly;                                   // 0xAA03E8 vmla.f32 s15,s14,s26
                float lg = sum * Log10E;                                        // 0xAA03EC
                float over = lg * 10f - p0;                                     // 0xAA03F4 vnmls.f32 s14,s15,s24: (lg * 10) - threshold
                if (!(over > 0f)) over = 0f;                                    // 0xAA03F8..0xAA0404: vmovle (a NaN is "le")
                float rise = over - g;                                          // 0xAA0408
                float fall = g - over;                                          // 0xAA040C
                float a = rise >= 0f ? _attackCoef : _releaseCoef;              // 0xAA0410..0xAA041C: vmovlt s2 (release), vmovge s17 (attack)
                g = over + a * fall;                                            // 0xAA0420 vmla.f32 s15,s14,s13
                float y = g * slope;                                            // 0xAA0428
                y = y * Point05;                                                // 0xAA042C
                float lin;
                if (y < MinusThirtySeven) lin = 0f;                             // 0xAA0430..0xAA0438 bmi 0xAA046C (s9 = 0.0f)
                else
                {
                    float scaled = y * PowScale;                                // 0xAA043C vmla.f32 s11,s15,s23
                    float word = PowBase + scaled;
                    uint u = SaturatingU32(word);                               // 0xAA0440 vcvt.u32.f32 (round toward zero)
                    float m2 = BitConverter.UInt32BitsToSingle((u & 0x7FFFFF) + 0x3F800000);       // 0xAA0448..0xAA0458
                    float expWord = BitConverter.UInt32BitsToSingle((u >> 23) << 23);              // 0xAA044C, 0xAA0454, 0xAA0464
                    float c7 = PolyC7 + m2 * PolyC6;                            // 0xAA045C vmla.f32 s7,s15,s6
                    float c8 = PolyC8 + m2 * c7;                                // 0xAA0460 vmla.f32 s8,s15,s7
                    lin = c8 * expWord;                                         // 0xAA0468
                }
                data[b + i] = x * lin;                                          // 0xAA046C, 0xAA0470
            }
            _state[2 * c] = g;                                                  // 0xAA0488 vstr s13,[ip,#-0xc]
            _state[2 * c + 1] = pw;                                             // 0xAA047C vstr s10,[ip]
        }
    }
}
