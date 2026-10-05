// fidelity: M6-013
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The Peak Limiter's parameter object (<c>0x20</c> bytes, vptr <c>0x101C9D0</c>; created by <c>0xAA2280</c>, cloned by <c>0xAA1FC4</c>; research bus-fx-17 sections 2.6..2.8, C45.2): <c>vt+8</c> SetParam <c>0xAA216C</c>, <c>vt+0xC</c> Clone <c>0xAA1FC4</c>, <c>vt+0x10</c> wrapper <c>0xAA20FC</c> (size 0 installs the defaults, otherwise it
/// tail-calls <c>vt+0x18</c> whatever the block length), <c>vt+0x14</c> destroy <c>0xAA2038</c>, <c>vt+0x18</c> SetParamsBlock <c>0xAA2084</c>.
/// <para><b>Field names are inferred from the use sites</b> (the artifact carries none): <c>+4</c> threshold (dB), <c>+8</c> ratio, <c>+0xC</c> release (s), <c>+0x10</c> the output gain as a LINEAR factor (<c>powf(10.0f, dB * 0.05f)</c>), <c>+0x18</c> look-ahead (s), byte <c>+0x1C</c> ProcessLFE, byte <c>+0x1D</c> ChannelLink,
/// byte <c>+0x14</c> the "release changed" dirty byte, byte <c>+0x1E</c> the "set-up changed" dirty byte. The creator stores only the vptr: the rest is uninitialised pool memory until a block, the defaults or SetParam stores it (<see cref="WwisePluginMemory"/>); an instance is made through <see cref="Clone"/> (which sets both dirty bytes) or a block.</para>
/// </summary>
public sealed class WwiseLimiterParams
{
    /// <summary>The block size the bank carries (five floats and two bytes); <c>0xAA2084</c> reads these 22 bytes whatever the size argument says.</summary>
    public const int BlockSize = 22;

    private static readonly float PointZeroFive = BitConverter.UInt32BitsToSingle(0x3D4CCCCD);   // 0xAA20A4 / 0xAA222C: 0.05f

    private readonly WwisePluginMemory _m = new(0x20, "Peak Limiter parameter object");

    private WwiseLimiterParams()
    {
    }

    /// <summary>Creator <c>0xAA2280(alloc)</c>: a <c>0x20</c>-byte object holding only the vptr; null when the allocation fails.</summary>
    public static WwiseLimiterParams? Create(IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        return alloc.Allocate(0x20) ? new WwiseLimiterParams() : null;           // 0xAA2284..0xAA2298
    }

    /// <summary>The u32 at <paramref name="offset"/> (a test accessor; an unstored word stops visibly).</summary>
    public uint Word(int offset) => _m.U32(offset);

    /// <summary>The byte at <paramref name="offset"/>.</summary>
    public byte Byte(int offset) => _m.U8(offset);

    /// <summary><c>[+4]</c>: the threshold in dB.</summary>
    public float Threshold => _m.F32(4);

    /// <summary><c>[+8]</c>: the ratio.</summary>
    public float Ratio => _m.F32(8);

    /// <summary><c>[+0xC]</c>: the release time in seconds.</summary>
    public float Release => _m.F32(0xC);

    /// <summary><c>[+0x10]</c>: the output gain, a linear factor.</summary>
    public float OutputGain => _m.F32(0x10);

    /// <summary><c>[+0x18]</c>: the look-ahead time in seconds.</summary>
    public float LookAhead => _m.F32(0x18);

    /// <summary>byte <c>[+0x1C]</c>: ProcessLFE.</summary>
    public byte ProcessLfe => _m.U8(0x1C);

    /// <summary>byte <c>[+0x1D]</c>: ChannelLink.</summary>
    public byte ChannelLink => _m.U8(0x1D);

    /// <summary>byte <c>[+0x14]</c>: set by every setter of threshold, ratio, release and output gain; cleared by Execute after it refreshes the release coefficient.</summary>
    public byte DirtyRelease => _m.U8(0x14);

    /// <summary>byte <c>[+0x1E]</c>: set by every setter of the look-ahead and the two flag bytes; cleared by Setup.</summary>
    public byte DirtySetup => _m.U8(0x1E);

    internal void ClearDirtyRelease() => _m.SetU8(0x14, 0);

    internal void ClearDirtySetup() => _m.SetU8(0x1E, 0);

    /// <summary>
    /// Clone <c>vt+0xC</c> (<c>0xAA1FC4</c>): allocates <c>0x20</c> bytes through <c>alloc-&gt;vt+8</c> (null on failure), copies the words at <c>+4..+0x20</c> and sets the bytes <c>+0x14</c> and <c>+0x1E</c> to 1.
    /// </summary>
    public WwiseLimiterParams? Clone(IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        if (!alloc.Allocate(0x20)) return null;                                 // 0xAA1FD4..0xAA1FE4
        var c = new WwiseLimiterParams();
        c._m.CopyFrom(_m, 4, 4, 0x1C);                                          // 0xAA1FE8..0xAA201C ldm / stm of +4..+0x1F
        c._m.SetU8(0x14, 1);                                                    // 0xAA2020
        c._m.SetU8(0x1E, 1);                                                    // 0xAA2028
        return c;
    }

    /// <summary>Destroy <c>vt+0x14</c> (<c>0xAA2038</c>): a null object returns 1; otherwise the object is freed through <c>alloc-&gt;vt+0xC</c> and 1 is returned.</summary>
    public static int Destroy(WwiseLimiterParams? p, IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        if (p is not null) alloc.Free(p);                                       // 0xAA203C..0xAA2064
        return 1;                                                               // 0xAA2068
    }

    /// <summary>
    /// <c>vt+0x10</c> (<c>0xAA20FC</c>): a zero <paramref name="size"/> installs the defaults (<c>0xAA2108..0xAA2154</c>: release 0.2, look-ahead 0.01, threshold -12.0, ratio 10.0, output gain 1.0 (linear), bytes <c>+0x14</c>, <c>+0x1C</c>, <c>+0x1D</c>, <c>+0x1E</c> = 1) and returns 1; otherwise it forwards to
    /// <see cref="SetParamsBlock"/> (<c>vt+0x18</c>) with the block and size unchecked.
    /// </summary>
    public int SetParamsBlockOrDefaults(byte[]? block, uint size)
    {
        if (size != 0) return SetParamsBlock(block);                            // 0xAA20FC cmp r3,#0; bne 0xAA2158
        _m.SetU32(0xC, 0x3E4CCCCD);                                             // 0xAA2118
        _m.SetU32(0x18, 0x3C23D70A);                                            // 0xAA2130
        _m.SetU32(4, 0xC1400000);                                               // 0xAA2148
        _m.SetU32(8, 0x41200000);                                               // 0xAA214C
        _m.SetU32(0x10, 0x3F800000);                                            // 0xAA2150
        _m.SetU8(0x14, 1);                                                      // 0xAA2138
        _m.SetU8(0x1C, 1);                                                      // 0xAA213C
        _m.SetU8(0x1D, 1);                                                      // 0xAA2140
        _m.SetU8(0x1E, 1);                                                      // 0xAA2144
        return 1;
    }

    /// <summary>
    /// <c>vt+0x18</c> SetParamsBlock (<c>0xAA2084</c>): block words 0, 4, 8, 0xC go to <c>+4</c>, <c>+8</c>, <c>+0x18</c>, <c>+0xC</c>; <c>+0x10 = powf(10.0f, word(0x10) * 0.05f)</c> (the host powf); the bytes at block <c>0x14</c> and <c>0x15</c> go to <c>+0x1C</c> and <c>+0x1D</c>; bytes <c>+0x14</c> and <c>+0x1E</c> are set to 1; returns 1.
    /// The engine reads 22 bytes whatever the size argument says, so a shorter block is refused.
    /// </summary>
    public int SetParamsBlock(byte[]? block)
    {
        if (block is null || block.Length < BlockSize)
            throw new WwiseMissingBehaviourException("M6-013 C45.2: 0xAA2084 reads 22 bytes at the block pointer whatever the size argument says; a shorter block is not an engine input");
        _m.SetU32(4, BitConverter.ToUInt32(block, 0));                          // 0xAA208C, 0xAA20A8
        _m.SetU32(8, BitConverter.ToUInt32(block, 4));                          // 0xAA2094, 0xAA20C0
        _m.SetU32(0x18, BitConverter.ToUInt32(block, 8));                       // 0xAA2098, 0xAA20B0
        _m.SetU32(0xC, BitConverter.ToUInt32(block, 0xC));                      // 0xAA209C, 0xAA20BC
        StoreOutputGain(BitConverter.ToUInt32(block, 0x10));                    // 0xAA20A0, 0xAA20B8..0xAA20EC
        _m.SetU8(0x1C, block[0x14]);                                            // 0xAA20D0, 0xAA20E0
        _m.SetU8(0x1D, block[0x15]);                                            // 0xAA20D4, 0xAA20E4
        _m.SetU8(0x14, 1);                                                      // 0xAA20DC
        _m.SetU8(0x1E, 1);                                                      // 0xAA20E8
        return 1;
    }

    /// <summary><c>powf(10.0f, f * 0.05f)</c> (<c>0xAA20B4..0xAA20CC</c> / <c>0xAA2230..0xAA2240</c>: the float product, then the call with <c>r0 = 0x41200000</c>), stored at <c>+0x10</c>.</summary>
    private void StoreOutputGain(uint dbBits)
    {
        float scaled = BitConverter.UInt32BitsToSingle(dbBits) * PointZeroFive;
        _m.SetU32(0x10, BitConverter.SingleToUInt32Bits(WwiseHostMath.Powf(10f, scaled)));
    }

    /// <summary>
    /// <c>vt+8</c> SetParam (<c>0xAA216C</c>; ids above 6 return 0x1F, the value pointer is not checked): 0 stores the word at <c>+4</c> and sets byte <c>+0x14</c>; 1 at <c>+8</c> (<c>+0x14</c>); 2 at <c>+0x18</c> (byte <c>+0x1E</c>); 3 at <c>+0xC</c> (<c>+0x14</c>); 4 stores <c>powf(10.0f, f * 0.05f)</c> at <c>+0x10</c> (<c>+0x14</c>);
    /// 5 stores the byte <c>+0x1C</c> (<c>+0x1E</c>); 6 stores the byte <c>+0x1D</c> (<c>+0x1E</c>). Returns 1.
    /// </summary>
    public int SetParam(uint id, byte[]? value)
    {
        if (id > 6) return 0x1F;                                                // 0xAA216C cmp r1,#6; addls pc / b 0xAA2274
        if (value is null) throw new ArgumentNullException(nameof(value), "the engine dereferences the value pointer (0xAA21B0, no null check)");
        if (id <= 4 && value.Length < 4 || id >= 5 && value.Length < 1)
            throw new ArgumentException("the engine reads 4 bytes (ids 0..4) or 1 byte (ids 5, 6) at the value pointer", nameof(value));
        switch (id)
        {
            case 0: _m.SetU32(4, BitConverter.ToUInt32(value, 0)); _m.SetU8(0x14, 1); break;       // 0xAA21B0
            case 1: _m.SetU32(8, BitConverter.ToUInt32(value, 0)); _m.SetU8(0x14, 1); break;       // 0xAA21CC
            case 2: _m.SetU32(0x18, BitConverter.ToUInt32(value, 0)); _m.SetU8(0x1E, 1); break;    // 0xAA21E8
            case 3: _m.SetU32(0xC, BitConverter.ToUInt32(value, 0)); _m.SetU8(0x14, 1); break;     // 0xAA2204
            case 4: StoreOutputGain(BitConverter.ToUInt32(value, 0)); _m.SetU8(0x14, 1); break;    // 0xAA2220
            case 5: _m.SetU8(0x1C, value[0]); _m.SetU8(0x1E, 1); break;                            // 0xAA2258
            default: _m.SetU8(0x1D, value[0]); _m.SetU8(0x1E, 1); break;                           // 0xAA2194
        }
        return 1;
    }
}

/// <summary>
/// The Peak Limiter plug-in (<c>0x006E0003</c>, ARM, vptr <c>0x103DF58</c>; the <c>0x50</c>-byte object created by <c>0xAA18F4</c>): the Robot_Bus insert effect in slot 2 (Robot_Bus_Peak_Limiter) and the SFX bus's. Slots: <c>+0</c> <c>0xAA0890</c>, <c>+4</c> <c>0xAA092C</c> (deleting destructor), <c>+8</c> Term <c>0xAA0894</c>,
/// <c>+0xC</c> Reset <c>0xAA0940</c>, <c>+0x10</c> GetPluginInfo <c>0xAA0904</c>, <c>+0x14</c> / <c>+0x18</c> the shared stubs <c>0x8DBF38</c> / <c>0x8DBF3C</c> (return 0), <c>+0x1C</c> Init <c>0xAA1B98</c>, <c>+0x20</c> Execute <c>0xAA1BD8</c>, <c>+0x24</c> <c>0xAA1FB8</c> (returns 0x2D).
/// <para><b>Fields</b> (the engine's object): <c>+4</c> / <c>+8</c> the process member pointer (here <see cref="Process"/>), <c>+0xC</c> the parameter object, <c>+0x10</c> the allocator, <c>+0x14</c> the previous output gain, <c>+0x18</c> the rate, <c>+0x1C</c> the channel word (<c>u32 fmt[4]</c>; its low byte is the ring's channel count),
/// <c>+0x24</c> the processed channels, <c>+0x28</c> the detector slots, <c>+0x2C</c> <c>L</c> (the look-ahead in frames), <c>+0x30</c> the detector array (12-byte entries <c>{GR, peak, hold}</c>), <c>+0x34</c> the delay rings (<c>channels * L</c> floats, planar), <c>+0x38</c> the ring write index, <c>+0x3C</c> the tail counter (-1 initially),
/// <c>+0x40</c> the tail maximum, <c>+0x44</c> the release coefficient, <c>+0x48</c> the attack coefficient, byte <c>+0x4C</c> the just-reset flag. Init does not zero the rings or the detectors; the owner calls <see cref="Reset"/> next (Execute before that stops visibly).</para>
/// <para><b>Built:</b> the unlinked / mono path P2 <c>0xAA0EB4</c> (the Robot_Bus path; also the SFX bus's when its channel count is 1). <b>Required stops</b> (the inventory reads them structurally only, C45.4): the linked processes P1 <c>0xAA09B8</c> and P3 <c>0xAA1464</c> and P2's LFE swap of the last channel (<c>0xAA0F28..0xAA1458</c>) throw
/// <see cref="WwiseMissingBehaviourException"/> when Execute reaches them.</para>
/// <para><b>Numerics:</b> binary32 throughout, non-fused <c>vmla</c>/<c>vnmls</c>; the output ramp is <see cref="WwiseOutputGainStage"/>; the libm calls (expf, powf) are the host seams of <see cref="WwiseHostMath"/>.</para>
/// </summary>
public sealed class WwiseLimiterPlugin : IWwiseEffectPlugin
{
    /// <summary>The plug-in id of the Peak Limiter in the bank (<c>0x006E0003</c>).</summary>
    public const uint PluginId = 0x006E0003;

    /// <summary>The vptr (<c>0x103DF58</c>).</summary>
    public const uint Vptr = 0x103DF58;

    /// <summary>The input status (<c>u32 [S+8]</c>) that asks for the tail (NoMoreData, 0x11).</summary>
    public const uint NoMoreData = 0x11;

    /// <summary>The status Execute stores into <c>[S+8]</c> while the tail remains (0x2D, data ready).</summary>
    public const uint DataReady = 0x2D;

    /// <summary>Setup's allocation failure result (<c>0xAA1B68 mov r0,#0x34</c>).</summary>
    public const int AllocationFailed = 0x34;

    /// <summary>The process member pointer Setup stores at <c>+4</c> / <c>+8</c>.</summary>
    public enum ProcessKind
    {
        /// <summary>Setup has not run.</summary>
        Unset = 0,

        /// <summary><c>0xAA0EB4</c>: unlinked or mono (built).</summary>
        P2 = 2,

        /// <summary><c>0xAA09B8</c>: linked, the last channel excluded (LFE) (a required stop).</summary>
        P1 = 1,

        /// <summary><c>0xAA1464</c>: linked (a required stop).</summary>
        P3 = 3,
    }

    private struct Detector
    {
        public float GainReduction;     // +0
        public float Peak;              // +4
        public uint Hold;               // +8
    }

    private static float Bits(uint b) => BitConverter.UInt32BitsToSingle(b);

    private static readonly float MinusTwoPointTwo = Bits(0xC00CCCCD);   // 0xAA1A60 / 0xAA1D58
    private static readonly float Minus127 = Bits(0x42FE0000);           // 127.0f, 0xAA10A8
    private static readonly float OneThird = Bits(0x3EAAAAAB);           // 0xAA10A0
    private static readonly float Ln2 = Bits(0x3F317218);                // 0xAA10AC
    private static readonly float Log10E = Bits(0x3EDE5BD9);             // 0xAA10B0
    private static readonly float MinusThirtySeven = Bits(0xC2140000);   // 0xAA1098
    private static readonly float PowScale = Bits(0x4BD49A78);           // 0xAA109C
    private static readonly float PowBase = Bits(0x4E7E0000);            // 0xAA10A4
    private static readonly float PolyC6 = Bits(0x3EA67F46);             // 0xAA10B4
    private static readonly float PolyC7 = Bits(0x3CAA70DE);             // 0xAA10BC
    private static readonly float PolyC8 = Bits(0x3F272DDB);             // 0xAA10C0
    private const double PointZeroFiveDouble = 0.05;                     // 0xAA1090: 0x3FA999999999999A

    private bool _initialised;
    private bool _terminated;
    private WwiseLimiterParams? _param;         // +0xC
    private IWwisePluginMemAlloc? _alloc;       // +0x10
    private float _previousGain;                // +0x14
    private uint _rate;                         // +0x18
    private uint _channelWord;                  // +0x1C
    private uint _processedChannels;            // +0x24
    private uint _detectorSlots;                // +0x28
    private uint _l;                            // +0x2C
    private Detector[]? _detectors;             // +0x30
    private float[]? _ring;                     // +0x34
    private uint _writeIndex;                   // +0x38
    private bool _writeIndexSet;
    private uint _tail = 0xFFFFFFFF;            // +0x3C
    private uint _tailMax;                      // +0x40
    private float _releaseCoef;                 // +0x44
    private bool _releaseCoefSet;
    private float _attackCoef;                  // +0x48
    private bool _attackCoefSet;
    private bool _justReset;                    // +0x4C
    private bool _ringReady;
    private bool _detectorsReady;
    private ProcessKind _process;

    private WwiseLimiterPlugin()
    {
    }

    /// <summary>
    /// Create <c>0xAA18F4(alloc)</c>: allocates <c>0x50</c> bytes (null returns null), then zeroes <c>+4</c>, <c>+8</c>, <c>+0xC</c>, <c>+0x10</c>, <c>+0x30</c>, <c>+0x34</c>, <c>+0x40</c>, sets the channel byte and the flag bits of <c>+0x1C</c> to 0, <c>+0x3C</c> to -1 and the vptr. <c>+0x38</c>, <c>+0x4C</c>, <c>+0x14</c>, <c>+0x44</c> and <c>+0x48</c> are not
    /// initialised by the creator.
    /// </summary>
    public static WwiseLimiterPlugin? Create(IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        return alloc.Allocate(0x50) ? new WwiseLimiterPlugin() : null;           // 0xAA18F8..0xAA190C
    }

    /// <summary><c>+0xC</c>: the parameter object.</summary>
    public WwiseLimiterParams? Params => _param;

    /// <summary><c>+0x14</c>: the previous output gain.</summary>
    public float PreviousGain => _previousGain;

    /// <summary><c>+0x18</c>: the rate.</summary>
    public uint Rate => _rate;

    /// <summary><c>+0x1C</c>: the channel word.</summary>
    public uint ChannelWord => _channelWord;

    /// <summary><c>+0x24</c>: the processed channels.</summary>
    public uint ProcessedChannels => _processedChannels;

    /// <summary><c>+0x28</c>: the detector slots.</summary>
    public uint DetectorSlots => _detectorSlots;

    /// <summary><c>+0x2C</c>: <c>L</c>, the look-ahead in frames.</summary>
    public uint LookAheadFrames => _l;

    /// <summary><c>+0x38</c>: the ring write index (frames); uninitialised until Setup's ring allocation succeeds.</summary>
    public uint WriteIndex => _writeIndexSet ? _writeIndex : throw new WwiseMissingBehaviourException("M6-013 C45.4: [this+0x38] was read before Setup stored it (uninitialised pool memory)");

    /// <summary><c>+0x38</c>, or null while it is uninitialised.</summary>
    public uint? WriteIndexIfSet => _writeIndexSet ? _writeIndex : null;

    /// <summary><c>+0x3C</c>: the tail counter (0xFFFFFFFF is -1).</summary>
    public uint TailCounter => _tail;

    /// <summary><c>+0x40</c>: the tail maximum.</summary>
    public uint TailMax => _tailMax;

    /// <summary><c>+0x44</c>: the release coefficient (throws before Execute has computed it).</summary>
    public float ReleaseCoefficient => _releaseCoefSet ? _releaseCoef : throw new WwiseMissingBehaviourException("M6-013 C45.4: [this+0x44] was read before Execute stored it (uninitialised pool memory)");

    /// <summary><c>+0x44</c>, or null while it is uninitialised.</summary>
    public float? ReleaseCoefficientIfSet => _releaseCoefSet ? _releaseCoef : null;

    /// <summary><c>+0x48</c>: the attack coefficient.</summary>
    public float AttackCoefficient => _attackCoefSet ? _attackCoef : throw new WwiseMissingBehaviourException("M6-013 C45.4: [this+0x48] was read before Setup stored it (uninitialised pool memory)");

    /// <summary><c>+0x48</c>, or null while it is uninitialised.</summary>
    public float? AttackCoefficientIfSet => _attackCoefSet ? _attackCoef : null;

    /// <summary>byte <c>+0x4C</c>: the just-reset flag.</summary>
    public bool JustReset => _justReset;

    /// <summary>The process function Setup selected (<c>+4</c> / <c>+8</c>).</summary>
    public ProcessKind Process => _process;

    /// <summary>The detector entries <c>[+0x30]</c> as <c>{GR bits, peak bits, hold}</c> words, or null when none is allocated.</summary>
    public (uint GainReduction, uint Peak, uint Hold)[]? DetectorSnapshot =>
        _detectors?.Select(d => (BitConverter.SingleToUInt32Bits(d.GainReduction), BitConverter.SingleToUInt32Bits(d.Peak), d.Hold)).ToArray();

    /// <summary>The delay rings <c>[+0x34]</c> (<c>channels * L</c> floats), or null when none is allocated; a copy.</summary>
    public float[]? RingSnapshot => _ring?.ToArray();

    /// <summary>
    /// A diagnostic, not an engine value: the smallest detector gain (<c>lin</c>) applied since <see cref="ResetDiagnostics"/>. The bus chain's report reads it; it does not feed back into any sample.
    /// </summary>
    public float MinGainObserved { get; private set; } = 1f;

    /// <summary>Clears <see cref="MinGainObserved"/> (the diagnostic only).</summary>
    public void ResetDiagnostics() => MinGainObserved = 1f;

    /// <inheritdoc />
    public int Slot14() => 0;                                                   // 0x8DBF38

    /// <inheritdoc />
    public int Slot18() => 0;                                                   // 0x8DBF3C

    /// <inheritdoc />
    public int Slot24() => 0x2D;                                                // 0xAA1FB8

    /// <inheritdoc />
    public int GetPluginInfo(out WwisePluginInfo info)
    {
        info = new WwisePluginInfo(3, 0x7E002, 1, 0);                           // 0xAA0904..0xAA0924
        return 1;
    }

    /// <summary>Term <c>0xAA0894(this, alloc)</c>: frees the ring when <c>[+0x34] != 0</c> and the detectors when <c>[+0x30] != 0</c> (<c>alloc-&gt;vt+0xC</c>; the pointers are not cleared), runs the destructor, frees the object and returns 1.</summary>
    public int Term(IWwisePluginMemAlloc alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        if (_ring is not null) alloc.Free(_ring);                               // 0xAA089C..0xAA08B8
        if (_detectors is not null) alloc.Free(_detectors);                     // 0xAA08BC..0xAA08D4
        alloc.Free(this);                                                       // 0xAA08E8..0xAA08F8
        _terminated = true;
        return 1;                                                               // 0xAA08FC
    }

    /// <summary>
    /// Reset <c>0xAA0940</c>: when <c>[+0x34] != 0</c> the ring (<c>L * 4 * byte [+0x1C]</c> bytes) is zeroed; when <c>[+0x30] != 0</c> and <c>[+0x28] != 0</c> every word of every 12-byte detector entry is zeroed; byte <c>[+0x4C] = 1</c>; returns 1.
    /// <c>[+0x14]</c>, <c>[+0x38]</c>, <c>[+0x3C]</c> and <c>[+0x44]</c> are not touched.
    /// </summary>
    public int Reset()
    {
        if (!_initialised) throw new WwiseMissingBehaviourException("M6-013 C45.4: Reset (0xAA0940) reads [this+0x34], [this+0x30] and [this+0x2C], which only Create and Init write: the engine reads uninitialised pool memory");
        if (_ring is not null)
        {
            Array.Clear(_ring);                                                 // 0xAA094C..0xAA0968
            _ringReady = true;
        }
        if (_detectors is not null && _detectorSlots != 0)
        {
            Array.Clear(_detectors);                                            // 0xAA096C..0xAA09A8
            _detectorsReady = true;
        }
        _justReset = true;                                                      // 0xAA09AC..0xAA09B0
        return 1;
    }

    /// <inheritdoc />
    public int Init(IWwisePluginMemAlloc alloc, object? ctx, object? parameters, WwiseEffectFormat fmt)
    {
        if (parameters is not WwiseLimiterParams p) throw new ArgumentException("the Peak Limiter's parameter object is a WwiseLimiterParams", nameof(parameters));
        return Init(alloc, ctx, p, fmt);
    }

    /// <summary>
    /// Init <c>0xAA1B98(this, alloc, ctx, param, fmt)</c> (<paramref name="ctx"/> is not read): <c>[+0xC] = param</c>, <c>[+0x10] = alloc</c>, <c>[+0x14] = word [param+0x10]</c>, <c>[+0x18] = u32 [fmt+0]</c>, <c>[+0x1C] = u32 [fmt+4]</c> (the words at <c>fmt+8</c> / <c>fmt+0xA</c> go to <c>+0x20</c> / <c>+0x22</c>, which nothing here reads), then it
    /// tail-calls Setup <c>0xAA19CC</c> and returns its result.
    /// </summary>
    public int Init(IWwisePluginMemAlloc alloc, object? ctx, WwiseLimiterParams p, WwiseEffectFormat fmt)
    {
        ArgumentNullException.ThrowIfNull(alloc);
        ArgumentNullException.ThrowIfNull(p);
        _previousGain = p.OutputGain;                                           // 0xAA1BA0, 0xAA1BC4
        _param = p;                                                             // 0xAA1BAC
        _rate = fmt.SampleRate;                                                 // 0xAA1BB0
        _channelWord = fmt.ChannelWord;                                         // 0xAA1BB4
        _alloc = alloc;                                                         // 0xAA1BB8
        _initialised = true;
        _terminated = false;
        return Setup();                                                         // 0xAA1BD4 b 0xAA19CC
    }

    private static uint SaturatingU32(float v)
    {
        if (float.IsNaN(v) || v <= 0f) return 0;
        if (v >= 4294967296f) return uint.MaxValue;
        return (uint)v;
    }

    /// <summary>
    /// Setup <c>0xAA19CC</c>: frees the ring then the detectors (and clears the pointers); <c>[+0x24] = byte [+0x1C]</c>, minus 1 when the ProcessLFE byte is 0 and <c>u32 [+0x1C] &amp; 0x8000</c>; <c>[+0x28] = ChannelLink ? 1 : [+0x24]</c>; <c>L = [+0x2C] = u32(float(rate) * lookAhead)</c> (truncated);
    /// <c>[+0x48] = expf(-2.2f / (float)(L / 2))</c>; the ring is <c>byte [+0x1C] * L * 4</c> bytes (failure: <c>[+0x34] = 0</c> and 0x34); <c>[+0x38] = 0</c>; the process function (ChannelLink 0, or one processed channel: P2; otherwise P3, except P1 when the LFE bit is set and ProcessLFE is 0); the detectors are
    /// <c>[+0x28] * 12</c> bytes (failure: <c>[+0x30] = 0</c> and 0x34); ChannelLink-and-all done: <c>param[+0x1E] = 0</c>, 1.
    /// </summary>
    public int Setup()
    {
        var alloc = _alloc ?? throw new WwiseMissingBehaviourException("M6-013 C45.4: Setup (0xAA19CC) before Init");
        var p = _param!;
        if (_ring is not null)
        {
            alloc.Free(_ring);                                                  // 0xAA19D0..0xAA19F4
            _ring = null;
        }
        if (_detectors is not null)
        {
            alloc.Free(_detectors);                                             // 0xAA19F8..0xAA1A18
            _detectors = null;
        }
        _ringReady = false;
        _detectorsReady = false;

        uint nch = _channelWord & 0xFF;                                         // 0xAA1A20 ldrb r5,[r4,#0x1c]
        _processedChannels = nch;                                               // 0xAA1A28
        if (p.ProcessLfe == 0 && (_channelWord & 0x8000) != 0)                  // 0xAA1A24, 0xAA1A2C..0xAA1A3C
        {
            _processedChannels = nch - 1;
            if (nch == 0) throw new InvalidOperationException("byte [fmt+4] is 0 with the 0x8000 flag: the engine's processed channel count wraps to 0xFFFFFFFF");
        }
        byte link = p.ChannelLink;                                              // 0xAA1A48
        _detectorSlots = link == 0 ? _processedChannels : 1;                    // 0xAA1A54, 0xAA1A58, 0xAA1A64
        float look = p.LookAhead;                                               // 0xAA1A4C
        _l = SaturatingU32((float)_rate * look);                                // 0xAA1A5C..0xAA1A6C vcvt.f32.u32; vmul.f32; vcvt.u32.f32
        float halfL = (float)_l * 0.5f;                                         // 0xAA1A78 vcvt.f32.u32 s14,s14,#1 (L / 2)
        _attackCoef = WwiseHostMath.Expf(MinusTwoPointTwo / halfL);             // 0xAA1A7C..0xAA1AA0
        _attackCoefSet = true;

        uint bytes = unchecked(nch * (_l << 2));                                // 0xAA1A94, 0xAA1A9C mul r1,r5,r1
        if (bytes > int.MaxValue) throw new InvalidOperationException("the delay ring does not fit a managed array");
        if (!alloc.Allocate((int)bytes))                                        // 0xAA1AA4..0xAA1AB4 alloc->vt+8(alloc, size)
        {
            _ring = null;                                                       // 0xAA1AB0 str r0,[r4,#0x34]
            return AllocationFailed;                                            // 0xAA1B68
        }
        _ring = new float[bytes / 4];
        _writeIndex = 0;                                                        // 0xAA1AC0
        _writeIndexSet = true;

        if (link == 0 || _processedChannels == 1) _process = ProcessKind.P2;    // 0xAA1AC8..0xAA1AE0, 0xAA1B00..0xAA1B08
        else if ((_channelWord & 0x8000) == 0) _process = ProcessKind.P3;       // 0xAA1B0C..0xAA1B14 -> 0xAA1B70
        else if (p.ProcessLfe != 0) _process = ProcessKind.P3;                  // 0xAA1B18..0xAA1B20 -> 0xAA1B70
        else _process = ProcessKind.P1;                                         // 0xAA1B24..0xAA1B38

        if (_detectorSlots != 0)                                                // 0xAA1AE4..0xAA1AEC
        {
            if (!alloc.Allocate(checked((int)(_detectorSlots * 12))))           // 0xAA1B40..0xAA1B58
            {
                _detectors = null;                                              // 0xAA1B5C str r0,[r4,#0x30]
                return AllocationFailed;
            }
            _detectors = new Detector[_detectorSlots];
        }
        p.ClearDirtySetup();                                                    // 0xAA1AF0..0xAA1AF8 strb r2,[r3,#0x1e]
        return 1;
    }

    /// <summary>The fast <c>log10</c> approximation, inline in the process function (<c>0xAA11C4..0xAA121C</c>, research 4.7a).</summary>
    internal static float Log10Approx(float x)
    {
        uint b = BitConverter.SingleToUInt32Bits(x);
        uint e = (b >> 23) & 0xFF;                                              // ubfx r3,r3,#0x17,#8
        float m = BitConverter.UInt32BitsToSingle((b & 0x7FFFFF) + 0x3F800000); // ubfx r2,r3,#0,#0x17; add r2,r2,#0x3f800000
        float z = (m - 1f) / (m + 1f);                                          // vsub; vadd; vdiv
        float z2 = z * z;                                                       // vmul.f32 s14, s15, s15
        float ef = (float)(int)e - Minus127;                                    // vcvt.f32.s32; vsub.f32 s5, s3, s4
        float t = 1f + z2 * OneThird;                                           // vmla.f32 s0(1.0), s14, s2
        float ln = ef * Ln2;                                                    // vmul.f32 s12, s5, s12
        ln = ln + (z + z) * t;                                                  // vmla.f32 s12, s3, s0
        return ln * Log10E;                                                     // vmul.f32 s14, s12, s13
    }

    /// <summary>The detector's target: <c>20 * log10approx(x) - threshold</c> (the product rounded, then the subtraction), or 0.0f unless it is above zero (a NaN gives 0.0f): <c>vnmls.f32 s13, s14, s15</c>; <c>vcmpe.f32 s13, #0</c>; <c>vmovle.f32 s15, s18</c>.</summary>
    private static float Target(float x, float threshold)
    {
        float lg = Log10Approx(x);
        float v = lg * 20f - threshold;
        return v > 0f ? v : 0f;
    }

    /// <summary>The detector's fast power of 10 (<c>0xAA1358..0xAA1388</c>) for <c>e</c> not below -37.0f.</summary>
    internal static float FastPow(float e)
    {
        float t = PowBase + e * PowScale;                                       // vmov.f32 s14, s16; vmla.f32 s14, s20, s17
        uint n = SaturatingU32(t);                                              // vcvt.u32.f32
        float m = BitConverter.UInt32BitsToSingle((n & 0x7FFFFF) + 0x3F800000); // ubfx lr,r3,#0,#0x17; add lr,lr,#0x3f800000
        float expWord = BitConverter.UInt32BitsToSingle((n >> 23) << 23);       // lsr r3,r3,#0x17; lsl r3,r3,#0x17
        float c7 = PolyC7 + m * PolyC6;                                         // vmla.f32 s19, s14, s0
        float c8 = PolyC8 + m * c7;                                             // vmla.f32 s13, s14, s19
        return c8 * expWord;                                                    // vmul.f32 s14, s13, s14
    }

    /// <summary>
    /// Execute <c>0xAA1BD8(this, S)</c> (never sets <c>r0</c> as a result; here a <c>void</c>): (a) when the parameter's release dirty byte <c>[+0x14]</c> is set, <c>[+0x44] = expf(-2.2f / (float(rate) * release))</c> and the byte is cleared; (b) when the set-up dirty byte <c>[+0x1E]</c> is set, Setup runs, a result other than 1 returns,
    /// then Reset runs; (c) with <c>u32 [S+8] == 0x11</c> (NoMoreData) the flush path of the tail; otherwise <c>[+0x3C] = -1</c>; with no valid frames it returns; the process function runs; (d) the output-gain ramp (<see cref="WwiseOutputGainStage"/>, ProcessLFE from the parameter object) and <c>[+0x14] = word [param+0x10]</c>.
    /// </summary>
    public void Execute(WwiseDecodeState s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!_initialised || _terminated || _param is null)
            throw new WwiseMissingBehaviourException("M6-013 C45.4: Execute (0xAA1BD8) on an object Init has not filled (the engine reads uninitialised pool memory) or after Term");
        var p = _param;
        if (p.DirtyRelease != 0)                                                // 0xAA1BFC..0xAA1C04
        {
            float x = (float)_rate * p.Release;                                 // 0xAA1D4C..0xAA1D5C
            _releaseCoef = WwiseHostMath.Expf(MinusTwoPointTwo / x);            // 0xAA1D60..0xAA1D68
            _releaseCoefSet = true;
            p.ClearDirtyRelease();                                              // 0xAA1D78
        }
        if (p.DirtySetup != 0)                                                  // 0xAA1C08..0xAA1C10
        {
            int rc = Setup();                                                   // 0xAA1C18
            if (rc != 1) return;                                                // 0xAA1C1C..0xAA1C24
            Reset();                                                            // 0xAA1C2C..0xAA1C38 vt+0xC
        }

        uint eState = s.Scratch08;                                              // 0xAA1C3C ldr r3,[r4,#8]
        uint l = _l;                                                            // 0xAA1C40
        if (eState == NoMoreData)                                               // 0xAA1C44
        {
            Flush(s, l);                                                        // 0xAA1E00..0xAA1E9C
        }
        else
        {
            _tail = 0xFFFFFFFF;                                                 // 0xAA1C50, 0xAA1C54
        }
        if (s.ValidFrames == 0) return;                                         // 0xAA1C58 cmp r0,#0; beq (the flush path re-enters here with the updated count)

        switch (_process)                                                       // 0xAA1C60..0xAA1C84 the member pointer call
        {
            case ProcessKind.P2:
                ProcessUnlinked(s);
                break;
            case ProcessKind.P1:
                throw new WwiseMissingBehaviourException("M6-013 C45.4: the linked process P1 (0xAA09B8: linked, LFE flag set, ProcessLFE 0) is read structurally only; the arithmetic is not inventoried");
            case ProcessKind.P3:
                throw new WwiseMissingBehaviourException("M6-013 C45.4: the linked process P3 (0xAA1464: ChannelLink set with more than one processed channel) is read structurally only; the arithmetic is not inventoried");
            default:
                throw new WwiseMissingBehaviourException("M6-013 C45.4: Execute before Setup selected a process function (the engine's [this+4] is uninitialised)");
        }

        float newGain = p.OutputGain;                                           // 0xAA1C98 vldr s11,[r3,#0x10]
        WwiseOutputGainStage.Apply(s, p.ProcessLfe != 0, _previousGain, newGain);   // 0xAA1C88..0xAA1DFC
        _previousGain = p.OutputGain;                                           // 0xAA1D38..0xAA1D40 (reloaded) vstr s11,[r5,#0x14]
    }

    /// <summary>
    /// The NoMoreData flush path (<c>0xAA1E00..0xAA1FAC</c>, research 4.6): <c>T = [+0x3C]</c>, <c>L</c>, <c>V = u16 [S+0xE]</c>, <c>M = u16 [S+0xC]</c>. <c>T != 0</c>: with <c>T == -1</c> or <c>V != 0</c> the tail restarts (<c>[+0x3C] = [+0x40] = L</c>); otherwise with <c>R = [+0x40]</c>, <c>L &gt; R</c> (unsigned) sets <c>[+0x40] = L</c> and <c>T = [+0x3C] = L - (R - T)</c>,
    /// else <c>T</c> is kept. <c>T == 0</c>: <c>V == 0</c> returns (the valid count stays 0), else the tail restarts. Then <c>room = M - V</c>: <c>room &gt; T</c> (the tail fits) sets <c>[+0x3C] = 0</c>, zero-fills <c>room</c> frames at frame <c>V</c> of every channel (<c>byte [S+4]</c> planes, LFE included) and sets <c>V = M</c>; otherwise <c>[+0x3C] = T - room</c>, a non-zero
    /// <c>room</c> is zero-filled the same way and <c>V = M</c>; and while <c>[+0x3C] != 0</c> <c>u32 [S+8] = 0x2D</c>.
    /// </summary>
    private void Flush(WwiseDecodeState s, uint l)
    {
        uint t = _tail;
        uint v = s.ValidFrames;
        bool restart;
        if (t != 0)                                                             // 0xAA1E08..0xAA1E0C
        {
            if (t == 0xFFFFFFFF || v != 0) restart = true;                      // 0xAA1F7C..0xAA1F88
            else
            {
                uint r = _tailMax;                                              // 0xAA1F8C
                if (l > r)                                                      // 0xAA1F90 cmp r2,r1; strhi
                {
                    _tailMax = l;                                               // 0xAA1F94
                    t = l - (r - t);                                            // 0xAA1F98, 0xAA1F9C
                    _tail = t;                                                  // 0xAA1FA4
                }
                restart = false;                                                // movls r2,r3 (T kept)
            }
        }
        else
        {
            if (v == 0) { return; }                                             // 0xAA1E10..0xAA1E14 -> 0xAA1C24 (the valid count is 0: the caller returns)
            restart = true;
        }
        if (restart)
        {
            _tail = l;                                                          // 0xAA1E18
            _tailMax = l;                                                       // 0xAA1E1C
            t = l;
        }

        uint m = s.MaxFrames;                                                   // 0xAA1E20
        uint room = unchecked(m - v);                                           // 0xAA1E24 rsb r7,r0,r3
        int planes = (int)(s.ChannelConfig & 0xFF);                             // 0xAA1E38 ldrb r8,[r4,#4]
        if (room > t)                                                           // 0xAA1E28 cmp r7,r2; bls 0xAA1F64 (room <= T is "does not fit")
        {
            _tail = 0;                                                          // 0xAA1E3C
            ZeroFill(s, planes, v, m, room);                                    // 0xAA1E44..0xAA1E80 (room > T >= 0, so room != 0)
            s.ValidFrames = (ushort)m;                                          // 0xAA1E8C
        }
        else
        {
            _tail = unchecked(t - room);                                        // 0xAA1F68..0xAA1F70
            if (room != 0)                                                      // 0xAA1F64 cmp r7,#0; bne 0xAA1E44
            {
                ZeroFill(s, planes, v, m, room);
                s.ValidFrames = (ushort)m;                                      // 0xAA1E8C
            }
            if (_tail != 0) s.Scratch08 = DataReady;                            // 0xAA1E90 cmp r2,#0; movne r3,#0x2d; strne r3,[r4,#8]
        }
    }

    private static void ZeroFill(WwiseDecodeState s, int planes, uint v, uint m, uint room)
    {
        if (planes == 0) return;                                                // 0xAA1E44 cmp r8,#0; beq 0xAA1FAC (no plane to clear)
        if (s.Data is not float[] data) throw new InvalidOperationException("[S] must be the planar float buffer (the engine dereferences it)");
        for (int c = 0; c < planes; c++)
        {
            long start = (long)m * c + v;                                       // 0xAA1E60 mla r3,r3,sb,r0
            if (start + room > data.Length) throw new InvalidOperationException("the engine would write past the audio buffer: the zero fill exceeds it");
            Array.Clear(data, (int)start, (int)room);                           // 0xAA1E78 memset(pData + (M*c + V)*4, 0, room*4)
        }
    }

    /// <summary>
    /// P2 <c>0xAA0EB4(this, S)</c>, unlinked or mono (research 4.7): setup <c>s9 = (float)(((double)((1.0f / ratio) - 1.0f)) * 0.05)</c>, threshold, release and attack coefficients; the LFE swap of the last channel (ProcessLFE 0 and <c>u32 [S+4] &amp; 0x8000</c>, <c>0xAA0F20..0xAA1458</c>) is a required stop. Per processed channel <c>c</c> (its own
    /// detector entry and ring, the ring write index <c>[+0x38]</c> shared as the start): the just-reset prescan over <c>min(V, L)</c> samples, the initial target, then per sample the ring swap (<c>d = ring[w]; ring[w] = x</c>), the peak-hold detector (recompute when <c>hold == 0</c> or <c>|x| &gt; peak</c>), the smoothing
    /// <c>GR = tgt + coef * (GR - tgt)</c> with <c>coef = release</c> unless <c>tgt - GR &gt;= 0</c>, the fast power, <c>out = d * gain</c>. The detector entry is stored back after each channel and <c>[+0x38]</c> is the last channel's final write index.
    /// </summary>
    private void ProcessUnlinked(WwiseDecodeState s)
    {
        var p = _param!;
        float thr = p.Threshold;                                                // 0xAA0EE4 vldr s1,[r3,#4]
        float ratio = p.Ratio;                                                  // 0xAA0EC4 vldr s15,[r3,#8]
        float inv = 1f / ratio;                                                 // 0xAA0EDC vdiv.f32 s15, s14, s15
        float s9 = (float)((double)(inv - 1f) * PointZeroFiveDouble);           // 0xAA0F04..0xAA0F10 vsub; vcvt.f64.f32; vmul.f64; vcvt.f32.f64
        if (!_releaseCoefSet)
            throw new WwiseMissingBehaviourException("M6-013 C45.4: P2 reads the release coefficient [this+0x44] (0xAA0EFC), which only Execute step (a) stores: the parameter object's release dirty byte was clear and this instance never computed it (instances are made through Clone)");
        float rel = _releaseCoef;                                               // 0xAA0EFC vldr s6,[r2,#0x44]
        float att = _attackCoef;                                                // 0xAA0EF0 vldr s7,[r2,#0x48]
        uint chans = _processedChannels;                                        // 0xAA0EF8 [sp+0xC]
        if (p.ProcessLfe == 0 && (s.ChannelConfig & 0x8000) != 0)               // 0xAA0ED4, 0xAA0F14, 0xAA0F18..0xAA0F24
            throw new WwiseMissingBehaviourException("M6-013 C45.4: P2's LFE swap of the last channel (0xAA0F28..0xAA1458: ProcessLFE 0 and the buffer's 0x8000 flag) is read structurally only; the arithmetic is not inventoried");
        if (chans == 0)
        {
            _writeIndex = 0;                                                    // 0xAA10C4..0xAA10D4, 0xAA1418..0xAA1424: r5 = r2 = 0
            _writeIndexSet = true;
            return;
        }

        int valid = s.ValidFrames;                                              // 0xAA10D8 ldrh r3,[r1,#0xe]
        int stride = s.MaxFrames;                                               // 0xAA10E0 ldrh r0,[r1,#0xc]
        if (_l == 0) throw new WwiseMissingBehaviourException("M6-013 C45.4: with L == 0 the engine's chunk loop (0xAA1248..0xAA13B8) never advances (a zero-length ring): it does not terminate");
        int l = (int)_l;
        float[] data = WwiseOutputGainStage.DataOf(s, chans, stride, valid);
        if (_ring is null || !_ringReady || _detectors is null || !_detectorsReady || chans > _detectors.Length || (long)chans * l > _ring.Length)
            throw new WwiseMissingBehaviourException("M6-013 C45.4: P2 reads the ring [this+0x34] and the detector entries [this+0x30] (0xAA112C, 0xAA1130): Setup's allocations are not zeroed (Reset must run first) and an allocation-failed Setup leaves them null");

        int w0 = checked((int)_writeIndex);                                     // 0xAA0EEC ldr r0,[r0,#0x38]
        int lastW = w0;
        for (int c = 0; c < chans; c++)                                         // 0xAA1140..0xAA1400
        {
            ref Detector d = ref _detectors[c];
            float gr = d.GainReduction;                                         // 0xAA115C vldr s11,[sl]
            float peak = d.Peak;                                                // 0xAA1160 vldr s10,[sl,#4]
            uint hold = d.Hold;                                                 // 0xAA1154 ldr ip,[sl,#8]
            int pd = c * stride;
            int pr = c * l;
            if (_justReset)                                                     // 0xAA114C ldrb r3,[r3,#0x4c]; cmp r3,#0
            {
                uint n = Math.Min((uint)valid, _l);                             // 0xAA1168..0xAA1170
                uint rem = n;
                for (int i = 0; i < n; i++, rem--)                              // 0xAA117C..0xAA11A0
                {
                    float a = MathF.Abs(data[pd + i]);                          // vldmia; vabs.f32
                    if (a >= peak)                                              // vcmpe; vmovlt (keeps the peak when a < peak or unordered); movge ip,r3
                    {
                        peak = a;
                        hold = rem;
                    }
                }
                if (c == chans - 1) _justReset = false;                         // 0xAA11A4..0xAA11C0 strbeq r3,[r2,#0x4c]
            }

            float tgt = Target(peak, thr);                                      // 0xAA11C4..0xAA1238
            int wi = w0;
            int dp = 0;
            while (dp < valid)                                                  // 0xAA123C cmp r7,r3; bls 0xAA13BC
            {
                int fp = Math.Min(valid - dp, l - wi);                          // 0xAA1248..0xAA125C
                for (int k = 0; k < fp; k++)
                {
                    float x = data[pd + dp + k];                                // 0xAA1288 vldr s14,[r0]
                    float dly = _ring[pr + wi + k];                             // 0xAA128C vldr s12,[r1]
                    float a = MathF.Abs(x);                                     // 0xAA1290 vabs.f32 s20, s14
                    _ring[pr + wi + k] = x;                                     // 0xAA1294 vstmia r1!, {s14}
                    bool recompute;
                    if (hold == 0) recompute = true;                            // 0xAA1284 cmp ip,#0; beq 0xAA12AC
                    else
                    {
                        hold--;                                                 // 0xAA129C
                        recompute = a > peak;                                   // 0xAA12A0..0xAA12A8 vcmpe; ble 0xAA1318 (a NaN skips)
                    }
                    if (recompute)
                    {
                        hold = _l;                                              // 0xAA12B0 mov ip,r4
                        peak = a;                                               // 0xAA12B8 vmov.f32 s10, s20
                        tgt = Target(a, thr);                                   // 0xAA12BC..0xAA1314
                    }
                    float diff = tgt - gr;                                      // 0xAA1318 vsub.f32 s20, s15, s11
                    float coef = diff >= 0f ? att : rel;                        // 0xAA132C..0xAA133C vcmpe; vmovlt s6 (a NaN is lt); vmovge s7
                    float grMinus = gr - tgt;                                   // 0xAA1328 vsub.f32 s11, s11, s15
                    gr = tgt + coef * grMinus;                                  // 0xAA1340 vmla.f32 s21, s20, s11
                    float e = gr * s9;                                          // 0xAA1344 vmul.f32 s20, s21, s9
                    float gain = e < MinusThirtySeven ? 0f : FastPow(e);        // 0xAA1348..0xAA1354 vcmpe; bmi 0xAA138C (s14 = 0.0)
                    if (gain < MinGainObserved) MinGainObserved = gain;         // diagnostic only
                    data[pd + dp + k] = dly * gain;                             // 0xAA1394 vmul.f32 s14, s12, s14; vstmia r0!, {s14}
                }
                wi += fp;                                                       // 0xAA13A0..0xAA13A4
                dp += fp;
                if (wi == l) wi = 0;                                            // 0xAA13AC cmp r5,r8; moveq r5,sb
            }
            d.GainReduction = gr;                                               // 0xAA13E0 vstr s11,[sl,#-0xc]
            d.Peak = peak;                                                      // 0xAA13F4 vstr s10,[sl,#-8]
            d.Hold = hold;                                                      // 0xAA13F0 str ip,[sl,#-4]
            lastW = wi;
        }
        _writeIndex = (uint)lastW;                                              // 0xAA1418..0xAA1424 str r3,[r2,#0x38]
        _writeIndexSet = true;
    }
}
