// fidelity: M6-004
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The values the native <c>CAkResampler::Init(fmt, outRate)</c> reads out of the audio format (M6-004, M6 0.11,
/// gapB P1). The native format is the Wwise <c>AkAudioFormat</c>; the approved rows settle only its sample-format
/// field (<c>fmt &amp; 0x3F</c> — 16 int16 or 32 float, gapB P1) and the use of its channel count (the kernel row
/// table <c>[0,1,2,2 | 4,5,6,6]</c> by <c>channels−1</c>) and of its input rate (<c>+0x4C = inRate/outRate</c>).
/// The native byte offsets of the channel count and the input rate are not in the rows, so they are carried here
/// as named fields rather than decoded from a packed word: this is a host interface to the algorithm, not a wire
/// layout claim.
/// </summary>
/// <param name="RawFormat">The format word; its low 6 bits (<c>&amp; 0x3F</c>) are 16 (int16) or 32 (float).</param>
/// <param name="Channels">Channel count, 1..4; it selects the kernel row together with the sample format.</param>
/// <param name="SampleRate">The input rate (<c>inRate</c>); <c>Init</c> divides it by <c>outRate</c>.</param>
public readonly record struct WwiseResamplerFormat(int RawFormat, int Channels, int SampleRate)
{
    /// <summary>The sample-format field of the format word (M6 gapB P1).</summary>
    public int SampleFormat => RawFormat & WwiseResampler.FormatMask;
}

/// <summary>
/// Wwise's <c>CAkResampler</c> (M6-004): <c>Init</c> 0x00A47038, <c>SetPitch</c> 0x00A47384, <c>Execute</c>
/// 0x00A47178, the kernel table 0x0103C0B8 and the mono int16/float/ramp kernels 0x00A48F5C, 0x00A4913C,
/// 0x00A49E40 and 0x00A4A2D8.
///
/// This is the resampler in both stages of the robot-audio path:
/// <list type="bullet">
/// <item>the voice stage converts the source rate to the mix rate with a 16.16 integer linear-interpolation
/// kernel (int16 sources), whose bypass reduces to <c>×1/32768</c> (M6 0.11, gapB P2);</item>
/// <item>the Hijack stage converts the mono float mix to 22,320 Hz with a float linear-interpolation kernel
/// (gapB P8, M6 0.11).</item>
/// </list>
/// The pitch is a step <c>u32(ratio·powf(2, cents/1200)·65536 + 0.5)</c>, saturated to <c>0xFFFFFFFF</c>/<c>1</c>
/// when it rounds to zero, and a change ramps over <c>0x400</c> phase units (gapE 3.5, 3.6).
///
/// <b>Read and built.</b> The int16 mono kernels — bypass 0x00A48F5C, interpolating 0x00A4913C and the mode-2
/// ramp 0x00A4A2D8 — and the float mono mode-1 interpolating kernel 0x00A49E40.
///
/// <b>Refused, not guessed.</b> A float <c>Execute</c> is only valid in mode 1: the float mono bypass
/// (0x00A479F4) and the float mono ramp (0x00A4A958, gapE 3.6) were not read, so float modes 0 and 2 throw
/// <see cref="NotSupportedException"/> rather than running the int16-derived step or silently passing through.
/// The stereo int16 kernel 0x00A49634, the float stereo kernels and non-mono channel rows are likewise refused.
/// <c>Execute</c> has one settled tail case: with zero input frames it returns <see cref="NoMoreData"/> (17)
/// before the loop (0x00A4717C..0x00A47188, gapD D2.8).
/// </summary>
public sealed class WwiseResampler
{
    /// <summary><c>Execute</c> result 0x2B: the output could not be filled, the caller must supply more input.</summary>
    public const int DataNeeded = 43;

    /// <summary><c>Execute</c> result 0x2D: <c>+0x40</c> output frames were produced (M6 0.11).</summary>
    public const int DataReady = 45;

    /// <summary><c>Execute</c> result 0x11: the input had zero frames, returned before the loop (0x00A4717C..0x00A47188, gapD D2.8).</summary>
    public const int NoMoreData = 17;

    /// <summary>The ctor's initial phase <c>+0x2C = 0x10000</c> (0x00A46D80..0x00A46D88); <c>Init</c> does not overwrite it.</summary>
    public const int InitialPhase = 0x10000;

    /// <summary>The sample-format field of the format word (gapB P1).</summary>
    public const int FormatMask = 0x3F;

    /// <summary><c>fmt &amp; 0x3F</c> == 16: signed 16-bit samples (gapB P1).</summary>
    public const int FormatInt16 = 16;

    /// <summary><c>fmt &amp; 0x3F</c> == 32: float samples (gapB P1).</summary>
    public const int FormatFloat = 32;

    /// <summary>The ramp length in phase units (gapE 3.5/3.6): <c>0x400</c>.</summary>
    public const int RampSpan = 0x400;

    /// <summary>The step of the unity/bypass mode (gapB P1): <c>0x10000</c>.</summary>
    public const uint BypassStep = 0x10000;

    /// <summary>The kernel row table 0x00FFD450 by channel: <c>[0,1,2,2 | 4,5,6,6]</c> (gapB P1).</summary>
    private static readonly int[] KernelRow = { 0, 1, 2, 2 };

    private float _ratio;       // +0x4C = inRate/outRate
    private int _stepScale;     // +0x3C = 48000/outRate, an integer division (__aeabi_uidiv), the ramp's inc
    private bool _isFloat;
    private int _channels = 1;
    private bool _ratioChanged; // +0x57: set by Init, cleared by the first pitch reset
    private bool _pitchSet;

    private uint _current;      // the ramp's start step
    private uint _target;       // the ramp's end step
    private int _rampPos;       // 0..RampSpan; RampSpan means "not ramping"
    private int _lastCents;
    private bool _hasLastCents;

    // Streaming state. _phase is the native 16.16 position, starting at the ctor's 0x10000
    // (0x00A46D80..0x00A46D88); Init does not overwrite it. The native index is (phase>>16)-1, so the first
    // output samples input[0] (0x00A49F7C: r7=input-4, r4=phase>>16=1 -> input[0]) and the fraction
    // (phase&0xFFFF) is the interpolation weight. _slot is the input index _prev currently holds. The last
    // sample is carried across Execute calls (+0x20).
    private long _phase = InitialPhase;
    private long _slot;
    private bool _primed;
    private short _shortPrev, _shortNext;
    private bool _hasShortNext;
    private float _floatPrev, _floatNext;
    private bool _hasFloatNext;

    /// <summary>
    /// <c>Init(fmt, outRate)</c> 0x00A47038 (M6 0.11): <c>+0x4C = inRate/outRate</c> and <c>+0x3C = 48000/outRate</c>.
    /// The kernel row is chosen by the sample format and channel count (gapB P1).
    /// </summary>
    public void Init(WwiseResamplerFormat fmt, double outRate)
    {
        if (outRate <= 0 || double.IsNaN(outRate) || double.IsInfinity(outRate))
            throw new ArgumentOutOfRangeException(nameof(outRate), outRate, "output rate must be finite and positive");
        if (fmt.SampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(fmt), fmt.SampleRate, "input rate must be positive");

        int sampleFormat = fmt.SampleFormat;
        if (sampleFormat is not (FormatInt16 or FormatFloat))
            throw new NotSupportedException(
                $"Wwise resampler: fmt & 0x3F = {sampleFormat}, neither 16 (int16) nor 32 (float) (M6 gapB P1)");
        if (fmt.Channels is < 1 or > 4)
            throw new NotSupportedException(
                $"Wwise resampler: {fmt.Channels} channels is outside the kernel row table [0,1,2,2 | 4,5,6,6] (M6 gapB P1)");

        _channels = fmt.Channels;
        _isFloat = sampleFormat == FormatFloat;
        _ratio = (float)fmt.SampleRate / (float)outRate;  // +0x4C (float division, as the native)
        _stepScale = (int)(48000u / (uint)outRate);       // +0x3C (__aeabi_uidiv, unsigned integer division)
        _ratioChanged = true;                             // +0x57
        _pitchSet = false;
        _hasLastCents = false;
        _rampPos = RampSpan;
        ResetStream();
    }

    /// <summary>
    /// The kernel-table index (gapB P1): <c>table[(float ? 4 : 0) + (channels−1)] + mode·8</c>, i.e. the row table
    /// <c>[0,1,2,2 | 4,5,6,6]</c> indexed by <c>(format half, channels−1)</c> plus <c>mode·8</c>.
    /// </summary>
    public int KernelIndex => KernelRow[_channels - 1] + (_isFloat ? 4 : 0) + Mode * 8;

    /// <summary>The mode the table row selects (gapB P1): 0 bypass, 1 fixed pitch, 2 ramp.</summary>
    public int Mode
    {
        get
        {
            if (!_pitchSet)
                throw new InvalidOperationException("SetPitch must run before the resampler mode is known");
            if (_current != _target) return 2;
            return _current == BypassStep ? 0 : 1;
        }
    }

    /// <summary>The step the next <c>Execute</c> starts from (the ramp's <c>current</c>).</summary>
    public uint CurrentStep => _current;

    /// <summary>The step the ramp is heading for (the ramp's <c>target</c>).</summary>
    public uint TargetStep => _target;

    /// <summary>The ramp progress, 0..<see cref="RampSpan"/>; <see cref="RampSpan"/> means finished or not ramping.</summary>
    public int RampPosition => _rampPos;

    /// <summary><c>+0x4C</c> = inRate/outRate (M6 0.11).</summary>
    public float Ratio => _ratio;

    /// <summary><c>+0x3C</c> = 48000/outRate as an integer division (<c>__aeabi_uidiv</c>), the ramp's inc (M6 0.11, gapE 3.6).</summary>
    public int StepScale => _stepScale;

    /// <summary>
    /// <c>SetPitch</c> 0x00A47384 (gapE 3.5):
    /// <c>step = u32((double)(ratio·powf(2, cents/1200)·65536) + 0.5)</c>, with a zero result replaced by
    /// <c>cents &gt; 0 ? 0xFFFFFFFF : 1</c>. The first call or a ratio change sets current = target = step and
    /// rampPos = <see cref="RampSpan"/> (immediate). A changed pitch mid-ramp first folds the progress into
    /// current, then starts a new ramp at target.
    /// </summary>
    /// <param name="interp">The consumer's <c>interp</c> flag (0xA53150): when false the change is immediate.</param>
    public void SetPitch(int cents, bool interp = true)
    {
        uint step = ComputeStep(_ratio, cents);

        if (_ratioChanged || !_pitchSet)
        {
            _current = _target = step;
            _rampPos = RampSpan;
            _ratioChanged = false;
            _pitchSet = true;
        }
        else if (_hasLastCents && cents == _lastCents)
        {
            // Same cents: the mode follows current vs target (gapE 3.5). Nothing else changes.
        }
        else
        {
            if (_rampPos < RampSpan)
                _current = (uint)((long)_current + ((long)_target - _current) * _rampPos / RampSpan);
            _rampPos = 0;
            _target = step;
            if (!interp) _current = _target;
        }

        _lastCents = cents;
        _hasLastCents = true;
    }

    /// <summary>
    /// The step formula read from 0x00A473E0..0x00A4741C and gapE 3.5. The product is float, as the native's
    /// <c>powf</c> is; the <c>+0.5</c> and clamp are the native's.
    /// </summary>
    public static uint ComputeStep(float ratio, int cents)
    {
        float product = ratio * MathF.Pow(2f, cents / 1200f) * 65536f;
        double rounded = (double)product + 0.5;
        if (rounded >= 0xFFFFFFFF) return 0xFFFFFFFF;
        uint step = (uint)rounded;
        if (step == 0) step = cents > 0 ? 0xFFFFFFFFu : 1u;   // gapE 3.5
        return step;
    }

    /// <summary>
    /// <c>Execute</c> for an int16 source (voice stage), writing float samples. Zero input frames returns
    /// <see cref="NoMoreData"/> (17) before the loop. Fills up to <paramref name="maxFrames"/> frames (the native
    /// <c>+0x40</c>); returns <see cref="DataReady"/> when it did and <see cref="DataNeeded"/> when the input ran
    /// out first (M6 0.11).
    /// </summary>
    public int Execute(ReadOnlySpan<short> input, Span<float> output, int maxFrames)
    {
        if (input.Length == 0) return NoMoreData;                     // 0x00A4717C..0x00A47188
        if (_isFloat)
            throw new InvalidOperationException("This resampler was Init'd for float input (M6 gapB P1); use the float overload");
        if (_channels != 1)
            throw new NotSupportedException("The stereo int16 kernel 0x00A49634 was not read (M6 gapB P2)");
        EnsurePitchSet();
        if (maxFrames <= 0) return DataReady;

        int i = 0, produced = 0;
        while (produced < maxFrames)
        {
            if (!_primed)
            {
                if (i >= input.Length) break;
                _shortPrev = input[i++];                              // input[0]: the native index is (phase>>16)-1
                _slot = 0;
                _primed = true;
            }

            // Advance to the sample the phase selects: index (phase>>16)-1, so 0x10000 is input[0] and a unit
            // step needs one input per output after the first.
            while ((_phase >> 16) - 1 > _slot)
            {
                if (!_hasShortNext)
                {
                    if (i >= input.Length) break;
                    _shortNext = input[i++];
                    _hasShortNext = true;
                }
                _shortPrev = _shortNext;
                _hasShortNext = false;
                _slot++;
            }
            if ((_phase >> 16) - 1 > _slot) break;                // could not advance: input exhausted

            // Interpolation needs the next sample; the bypass kernel (mode 0) does not.
            if (Mode != 0 && !_hasShortNext)
            {
                if (i >= input.Length) break;
                _shortNext = input[i++];
                _hasShortNext = true;
            }

            output[produced] = Mode == 0
                ? BypassInt16(_shortPrev)                                          // 0x00A48F5C
                : InterpolateInt16(_shortPrev, _shortNext, (int)(_phase & 0xFFFF)); // 0x00A4913C

            _phase += StepForSample(produced);
            produced++;
        }

        AdvanceRamp(produced);
        return produced == maxFrames ? DataReady : DataNeeded;
    }

    /// <summary>
    /// <c>Execute</c> for a float source (the Hijack stage: float, mono, mix rate to 22,320 Hz; gapB P8, M6 0.11).
    /// Only the mode-1 interpolating kernel 0x00A49E40 was read; a float resampler in mode 0 or mode 2 throws
    /// <see cref="NotSupportedException"/>. Zero input frames returns <see cref="NoMoreData"/> (17).
    /// </summary>
    public int Execute(ReadOnlySpan<float> input, Span<float> output, int maxFrames)
    {
        if (input.Length == 0) return NoMoreData;                     // 0x00A4717C..0x00A47188
        if (!_isFloat)
            throw new InvalidOperationException("This resampler was Init'd for int16 input (M6 gapB P1); use the short overload");
        if (_channels != 1)
            throw new NotSupportedException("Only the mono float kernel 0x00A49E40 was read (M6 0.11)");
        EnsurePitchSet();
        if (Mode != 1)
            throw new NotSupportedException(
                $"The float resampler was read only in mode 1 (0x00A49E40); mode {Mode} is the unread float bypass " +
                "0x00A479F4 / ramp 0x00A4A958 (M6 gapE 3.6)");
        if (maxFrames <= 0) return DataReady;

        int i = 0, produced = 0;
        while (produced < maxFrames)
        {
            if (!_primed)
            {
                if (i >= input.Length) break;
                _floatPrev = input[i++];                              // input[0]: the native index is (phase>>16)-1
                _slot = 0;
                _primed = true;
            }

            while ((_phase >> 16) - 1 > _slot)
            {
                if (!_hasFloatNext)
                {
                    if (i >= input.Length) break;
                    _floatNext = input[i++];
                    _hasFloatNext = true;
                }
                _floatPrev = _floatNext;
                _hasFloatNext = false;
                _slot++;
            }
            if ((_phase >> 16) - 1 > _slot) break;                // could not advance: input exhausted

            if (!_hasFloatNext)
            {
                if (i >= input.Length) break;
                _floatNext = input[i++];
                _hasFloatNext = true;
            }

            output[produced] = InterpolateFloat(_floatPrev, _floatNext, (int)(_phase & 0xFFFF));

            _phase += StepForSample(produced);
            produced++;
        }

        AdvanceRamp(produced);
        return produced == maxFrames ? DataReady : DataNeeded;
    }

    /// <summary>
    /// The Hijack's in-place form (the bus buffer is resampled to 22,320 Hz in place; gapB P8). The native kernel
    /// reads and writes the same buffer; managed spans may not alias, so the input is copied to scratch first. For
    /// the Hijack's downsampling ratio the observable output is the same as a true in-place pass. During the copy
    /// `buffer.Length` is the input frame count, so an empty buffer returns <see cref="NoMoreData"/>.
    /// </summary>
    public int ExecuteInPlace(Span<float> buffer, int maxFrames)
    {
        if (buffer.Length == 0) return NoMoreData;                    // 0x00A4717C..0x00A47188
        var scratch = buffer.ToArray();
        return Execute(scratch, buffer, maxFrames);
    }

    // ---- kernels (the rows' exact arithmetic) ----

    /// <summary>Bypass kernel 0x00A48F5C (gapB P2): <c>sample × 1/32768</c> (0x38000000).</summary>
    public static float BypassInt16(short sample) => sample * (1f / 32768f);

    /// <summary>
    /// int16 mono interpolating kernel 0x00A4913C (gapB P2), linear interpolation in Q16:
    /// <c>out = (float)((x0&lt;&lt;16) + (x1−x0)·frac16) · 2^−31</c>.
    /// </summary>
    public static float InterpolateInt16(short x0, short x1, int frac16)
        => (float)((x0 << 16) + (long)(x1 - x0) * frac16) * (1f / 2147483648f);

    /// <summary>
    /// Mono float interpolating kernel 0x00A49E40 (M6 0.11):
    /// <c>out = prev + (phase &amp; 0xFFFF)/65536 · (next − prev)</c>.
    /// </summary>
    public static float InterpolateFloat(float prev, float next, int frac16)
        => prev + frac16 / 65536f * (next - prev);

    // ---- internals ----

    private void EnsurePitchSet()
    {
        if (!_pitchSet)
            throw new InvalidOperationException("SetPitch must be called after Init and before Execute (M6 gapE 3.5)");
    }

    /// <summary>
    /// The step for one output sample: the fixed <c>current</c> in modes 0/1, and the ramp
    /// <c>(current·1024 + diff·(pos0 + inc·(k+1)))&gt;&gt;10</c> in mode 2 (gapE 3.6). <paramref name="k"/> is the
    /// 0-based output index within this Execute.
    /// </summary>
    private long StepForSample(int k)
    {
        if (Mode != 2) return _current;
        int pos = _rampPos + _stepScale * (k + 1);
        if (pos > RampSpan) pos = RampSpan;
        long diff = (long)_target - _current;
        return ((long)_current * 1024 + diff * pos) >> 10;
    }

    /// <summary>Advance the ramp by the samples produced; <c>0x400</c> ends it and current reaches target (gapE 3.6).</summary>
    private void AdvanceRamp(int produced)
    {
        if (_current == _target)
        {
            _rampPos = RampSpan;
            return;
        }
        _rampPos += _stepScale * produced;
        if (_rampPos >= RampSpan)
        {
            _rampPos = RampSpan;
            _current = _target;
        }
    }

    private void ResetStream()
    {
        // Init does not overwrite the phase (the ctor's 0x10000 stands); it only clears the stream position.
        _primed = false;
        _slot = 0;
        _hasShortNext = false;
        _hasFloatNext = false;
    }
}
