// fidelity: M6-011
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The voice filter's LPF/HPF value-to-cutoff map (M6-011, gapE 1.7; LPF 0x00A7A3D8, HPF 0x00A7A4AC).
///
/// The value is a percentage; <c>v &lt; 30</c> uses the linear branch
/// <c>7000 + (30−v)·433.333344</c>, otherwise <c>16.7974434 · fastpow2(u32(1065353216 + (100−v)·1042939.94))</c>
/// with the mantissa polynomial <c>0.653043449 + m(0.0208057724 + 0.325189769m)</c>, and the result is capped at
/// <c>0.45·rate</c>.
///
/// The HPF runs the same map on <c>(100−v)</c>. <c>fastpow2</c> is the same exponent/mantissa reconstruction as
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

    /// <summary>LPF cutoff in Hz for a value 0..100, capped at <c>0.45·rate</c> (gapE 1.7).</summary>
    public static float LowPass(float value, int rateHz) => Map(value, rateHz);

    /// <summary>HPF cutoff in Hz: the same map applied to <c>(100−value)</c> (gapE 1.7).</summary>
    public static float HighPass(float value, int rateHz) => Map(100f - value, rateHz);

    /// <summary>The map itself (gapE 1.7), before the HPF complement.</summary>
    public static float Map(float value, int rateHz)
    {
        float fc;
        if (value < LinearLimit)
        {
            fc = LinearBase + (LinearLimit - value) * LinearSlope;
        }
        else
        {
            float bits = OneBits + (100f - value) * Scale;
            fc = Amplitude * FastPow2((uint)bits);
        }
        float cap = NyquistFraction * rateHz;
        return fc < cap ? fc : cap;
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

/// <summary>
/// A direct-form-I biquad's stored coefficients (M6-011, gapE 1.6). <c>NegA1</c> and <c>NegA2</c> are the stored
/// <c>−a1</c> and <c>−a2</c>, so the difference equation is
/// <c>y = b2·x2 + x·b0 + b1·x1 + (−a2)·y2 + (−a1)·y1</c>.
/// </summary>
public readonly record struct WwiseVoiceBiquadCoefficients(float B0, float B1, float B2, float NegA1, float NegA2)
{
    private static readonly float Sqrt2 = MathF.Sqrt(2f);

    /// <summary>
    /// Butterworth LPF (0x00A7678C): <c>c = 1/tan(π·fc/fs)</c>, <c>b0 = 1/(1+√2c+c²)</c>, <c>b1 = 2b0</c>,
    /// <c>b2 = b0</c>, <c>−a1 = −2(1−c²)b0</c>, <c>−a2 = −(c²−√2c+1)b0</c>.
    /// </summary>
    public static WwiseVoiceBiquadCoefficients LowPass(float fc, float fs)
    {
        float c = 1f / MathF.Tan(MathF.PI * fc / fs);
        float c2 = c * c;
        float b0 = 1f / (1f + Sqrt2 * c + c2);
        return new(b0, 2f * b0, b0, -2f * (1f - c2) * b0, -(c2 - Sqrt2 * c + 1f) * b0);
    }

    /// <summary>
    /// Butterworth HPF (0x00A77500): <c>c = tan(π·fc/fs)</c>, <c>b0 = 1/(c²+√2c+1)</c>, <c>b1 = −2b0</c>,
    /// <c>b2 = b0</c>, <c>−a1 = −2b0(c²−1)</c>, <c>−a2 = −(c²−√2c+1)b0</c>.
    /// </summary>
    public static WwiseVoiceBiquadCoefficients HighPass(float fc, float fs)
    {
        float c = MathF.Tan(MathF.PI * fc / fs);
        float c2 = c * c;
        float b0 = 1f / (c2 + Sqrt2 * c + 1f);
        return new(b0, -2f * b0, b0, -2f * b0 * (c2 - 1f), -(c2 - Sqrt2 * c + 1f) * b0);
    }
}

/// <summary>
/// The scalar direct-form-I biquad (M6-011, gapE 1.6, scalar form at 0x00A76C50..0x00A76C7C). The native's
/// aligned 4-sample blocks use a precomputed NEON block matrix (0x00A767BC..0x00A769C4), which the inventory
/// does not transcribe, so every sample here goes through this scalar form: that is equivalent, not
/// block-exact (MD3).
///
/// The difference equation is evaluated in the row's order:
/// <c>y = b2·x2 + x·b0 + b1·x1 + (−a2)·y2 + (−a1)·y1</c>. State is <c>{x1, x2, y1, y2}</c>.
/// </summary>
public sealed class WwiseVoiceBiquad
{
    private WwiseVoiceBiquadCoefficients _c;
    private float _x1, _x2, _y1, _y2;

    /// <summary>Replaces the coefficients; the history is kept.</summary>
    public void SetCoefficients(WwiseVoiceBiquadCoefficients c) => _c = c;

    /// <summary>The current coefficients (exposed for the compare).</summary>
    public WwiseVoiceBiquadCoefficients Coefficients => _c;

    /// <summary>The stored history, <c>{x1, x2, y1, y2}</c>.</summary>
    public (float X1, float X2, float Y1, float Y2) History => (_x1, _x2, _y1, _y2);

    /// <summary>Filters one sample (gapE 1.6, scalar form).</summary>
    public float Process(float x)
    {
        float y = _c.B2 * _x2 + x * _c.B0 + _c.B1 * _x1 + _c.NegA2 * _y2 + _c.NegA1 * _y1;
        _x2 = _x1;
        _x1 = x;
        _y2 = _y1;
        _y1 = y;
        return y;
    }

    /// <summary>
    /// While bypassed the native still copies the last two samples into all four history slots
    /// (0x00A76DB4/0x00A76DBC/0x00A76DC4/0x00A76DCC, gapE 1.8). The layout is <c>+0=x1, +4=x2, +8=y1,
    /// +0xC=y2</c>, and the native writes <c>x1 = y1 = last</c> and <c>x2 = y2 = second-last</c>.
    /// </summary>
    /// <param name="secondLast">Sample N−2.</param>
    /// <param name="last">Sample N−1.</param>
    public void CopyBypassHistory(float secondLast, float last)
    {
        _x1 = last;
        _x2 = secondLast;
        _y1 = last;
        _y2 = secondLast;
    }

    /// <summary>Clears the four history slots.</summary>
    public void Reset() => (_x1, _x2, _y1, _y2) = (0f, 0f, 0f, 0f);
}

/// <summary>Which band of the voice filter a biquad is.</summary>
public enum WwiseVoiceFilterKind
{
    /// <summary>Low pass (0x00A766F0).</summary>
    LowPass,

    /// <summary>High pass (0x00A77480).</summary>
    HighPass,
}

/// <summary>
/// One band of the voice filter: the value-domain ramp and its biquad (M6-011, gapE 1.4, 1.6, 1.8).
///
/// <b>State (the row's 16-byte struct, gapE 1.4):</b> <c>+0 current, +4 target, +8 u16 steps, +0xA s8
/// countdown, +0xB dirty, +0xC first, +0xD bypassed</c>. The ctor is <c>current = target = 0, steps = 8,
/// dirty = first = bypassed = true</c> (0x00A764D4..0x00A7654C).
/// </summary>
public sealed class WwiseVoiceFilterBand
{
    private readonly WwiseVoiceFilterKind _kind;
    private readonly int _rate;
    private readonly int _chunkSamples;
    private readonly WwiseVoiceBiquad _biquad = new();

    private float _current;      // +0
    private float _target;       // +4
    private int _steps = 8;      // +8, 0..8
    private int _countdown;      // +0xA, the "4 more buffers" counter
    private bool _dirty = true;  // +0xB
    private bool _first = true;  // +0xC
    private bool _bypassed = true; // +0xD
    private bool _ramping;

    /// <param name="kind">LPF or HPF.</param>
    /// <param name="rateHz">The mix rate; the ramp chunk is <c>floor(rate·128/48000)</c> (gapE 1.8).</param>
    public WwiseVoiceFilterBand(WwiseVoiceFilterKind kind, int rateHz = WwiseRuntimeSettings.MixRateHz)
    {
        _kind = kind;
        _rate = rateHz;
        _chunkSamples = Math.Max(1, (int)MathF.Floor(rateHz * 128f / 48000f));
    }

    /// <summary>The ramp's instantaneous value <c>+0</c>.</summary>
    public float Current => _current;

    /// <summary>The setter's target <c>+4</c>.</summary>
    public float Target => _target;

    /// <summary>The ramp's chunk count <c>+8</c>, 0..8.</summary>
    public int Steps => _steps;

    /// <summary>The bypass countdown <c>+0xA</c>.</summary>
    public int Countdown => _countdown;

    /// <summary>The bypass flag <c>+0xD</c>.</summary>
    public bool Bypassed => _bypassed;

    /// <summary>The first-apply flag <c>+0xC</c>.</summary>
    public bool First => _first;

    /// <summary>The per-chunk sample count <c>+0x10</c>: <c>floor(rate·128/48000)</c>, 128 at 48 kHz.</summary>
    public int ChunkSamples => _chunkSamples;

    /// <summary>The stored biquad, for the coefficient/history compare.</summary>
    public WwiseVoiceBiquad Biquad => _biquad;

    /// <summary>
    /// The target setter (gapE 1.4, 0x00A55444..0x00A55474): runs only when the target changes. It first folds
    /// the current ramp position into <c>current</c> — <c>current += (oldTarget−current)·0.125·steps</c> — then
    /// takes the new target and marks the band dirty.
    /// </summary>
    public void SetTarget(float value)
    {
        if (value == _target) return;
        _current = _current + (_target - _current) * 0.125f * _steps;
        _target = value;
        _dirty = true;
    }

    /// <summary>
    /// The per-buffer update and filtering (gapE 1.8 and 1.6). The first apply sets <c>current = target</c> with
    /// no ramp. A target <c>≤ 0.1</c> means bypassed; a ramp down to it filters the ramp buffer and then keeps
    /// the filter running four more buffers (<c>+0xA = 4</c>) before bypassing. <c>+0xA</c> is zeroed when a ramp
    /// starts (0x00A76770, 0x00A76A0C) and armed to 4 only when the ramp finishes with the target at or below
    /// 0.1. A target change whose <c>current</c> and new target are both <c>≤ 0.1</c> bypasses at once, with no
    /// ramp (0x00A769EC..0x00A76A2C). While bypassed the last two samples are copied into all four history
    /// slots. On a change the ramp resets, then each <c>N</c>-sample chunk takes one step and recomputes the
    /// coefficients from <c>current + steps/8·(target−current)</c>; after eight chunks <c>current = target</c>.
    /// </summary>
    public void Process(Span<float> buffer)
    {
        if (_first)
        {
            _current = _target;
            _first = false;
            _dirty = false;
            _ramping = false;
            _countdown = 0;
        }
        else if (_dirty)
        {
            _dirty = false;
            if (_current <= 0.1f && _target <= 0.1f)
            {
                // 0x00A769EC..0x00A76A2C: both current and the new target are ≤ 0.1, so the band bypasses
                // immediately — steps = 8, current = target, and no ramp or countdown filtering.
                _steps = 8;
                _current = _target;
                _bypassed = true;
                _ramping = false;
                _countdown = 0;
            }
            else
            {
                _steps = 0;
                _ramping = true;
                _countdown = 0;   // the ramp-start paths zero +0xA (0x00A76770, 0x00A76A0C)
            }
        }

        DecideBypass();

        if (_bypassed)
        {
            if (buffer.Length >= 2) _biquad.CopyBypassHistory(buffer[^2], buffer[^1]);
            else if (buffer.Length == 1) _biquad.CopyBypassHistory(buffer[0], buffer[0]);
            return;
        }

        if (!_ramping) Design(_current);

        int pos = 0;
        while (pos < buffer.Length)
        {
            if (_ramping)
            {
                _steps++;
                float value = _current + (_steps / 8f) * (_target - _current);
                if (_steps >= 8)
                {
                    _current = _target;
                    _ramping = false;
                    // +0xA = 4 only once the ramp has finished and the target is at or below 0.1.
                    if (_target <= 0.1f) _countdown = 4;
                }
                Design(value);
            }

            int n = Math.Min(_chunkSamples, buffer.Length - pos);
            for (int i = 0; i < n; i++) buffer[pos + i] = _biquad.Process(buffer[pos + i]);
            pos += n;
        }
    }

    /// <summary>Clears the biquad history and puts the band back at its ctor state.</summary>
    public void Reset()
    {
        _biquad.Reset();
        _current = 0f;
        _target = 0f;
        _steps = 8;
        _countdown = 0;
        _dirty = true;
        _first = true;
        _bypassed = true;
        _ramping = false;
    }

    private void DecideBypass()
    {
        if (_target > 0.1f)
        {
            _bypassed = false;
            return;
        }

        // A ramp down to ≤ 0.1 still filters the ramp buffer; +0xA is armed only at ramp completion.
        if (_ramping)
        {
            _bypassed = false;
            return;
        }

        if (_countdown > 0)
        {
            _countdown--;
            _bypassed = false;
            return;
        }

        _bypassed = true;
    }

    private void Design(float value)
    {
        float fc = _kind == WwiseVoiceFilterKind.LowPass
            ? WwiseVoiceFilterCutoff.LowPass(value, _rate)
            : WwiseVoiceFilterCutoff.HighPass(value, _rate);
        _biquad.SetCoefficients(_kind == WwiseVoiceFilterKind.LowPass
            ? WwiseVoiceBiquadCoefficients.LowPass(fc, _rate)
            : WwiseVoiceBiquadCoefficients.HighPass(fc, _rate));
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
/// A voice filter object: an LPF band and an HPF band (M6-011, gapE 1.3, 1.6). Filter A runs before the aux
/// sends, so it shapes the signal the Robot_Bus_1 tap sees; filter B is dry-path only and is bypassed for every
/// shipped sound.
/// </summary>
public sealed class WwiseVoiceFilter
{
    /// <summary>The LPF band.</summary>
    public WwiseVoiceFilterBand LowPass { get; }

    /// <summary>The HPF band.</summary>
    public WwiseVoiceFilterBand HighPass { get; }

    /// <param name="role">A or B; the class behaves the same, the role is carried for the compare.</param>
    /// <param name="rateHz">The mix rate (default 48 kHz, M6-018).</param>
    public WwiseVoiceFilter(WwiseVoiceFilterRole role, int rateHz = WwiseRuntimeSettings.MixRateHz)
    {
        Role = role;
        LowPass = new WwiseVoiceFilterBand(WwiseVoiceFilterKind.LowPass, rateHz);
        HighPass = new WwiseVoiceFilterBand(WwiseVoiceFilterKind.HighPass, rateHz);
    }

    /// <summary>Which filter this is.</summary>
    public WwiseVoiceFilterRole Role { get; }

    /// <summary>Sets both bands' targets (the caller clamps to 0..100, gapE 1.3).</summary>
    public void SetTargets(float lowPass, float highPass)
    {
        LowPass.SetTarget(lowPass);
        HighPass.SetTarget(highPass);
    }

    /// <summary>Runs both bands over the buffer in place.</summary>
    public void Process(Span<float> buffer)
    {
        LowPass.Process(buffer);
        HighPass.Process(buffer);
    }

    /// <summary>Resets both bands and their history.</summary>
    public void Reset()
    {
        LowPass.Reset();
        HighPass.Reset();
    }

    /// <summary><c>[F+0x190]</c>: the channel word the init stored (<c>0xA764FC</c>); <c>byte [F+0x194]</c> is <see cref="Byte194"/>.</summary>
    // fidelity: M6-022
    public uint Word190 { get; private set; }

    /// <summary><c>byte [F+0x194]</c> (<c>0xA764F0</c>): the init's third argument.</summary>
    // fidelity: M6-022
    public byte Byte194 { get; private set; }

    /// <summary>
    /// <c>0xA764D4(F, word, flag)</c> (<c>0xA54B74</c>, <c>0xA54D44</c>): stores <c>flag</c> and <c>word</c>, resets both bands (the blocks' initial state, <c>0xA76500..0xA7654C</c>) and allocates the two <c>(byte word) &lt;&lt; 4</c>-byte blocks (<c>0xA7655C</c>, <c>0xA7658C</c>):
    /// an allocation failure (<paramref name="allocationFails"/>, called once per block) frees what was made and returns 2; otherwise 1.
    /// </summary>
    // fidelity: M6-022
    public int InitA764D4(uint word, byte flag, Func<bool>? allocationFails)
    {
        Byte194 = flag;
        Word190 = word;
        Reset();
        if (allocationFails?.Invoke() == true) return 2;           // 0xA76564 beq 0xA765D0 -> 0xA765FC mov r0,#2
        if (allocationFails?.Invoke() == true) return 2;           // 0xA76598 beq 0xA765B0 ... mov r0,#2
        return 1;
    }
}

/// <summary>
/// One node-to-voice connection's LPF/HPF contribution (M6-011, gapE 1.2). In 2D the connection's value is the
/// effective context value; in 3D it is <c>max(ctx, attenuation)</c> (0x00A5C740..0x00A5C774). The 3D
/// attenuation's own source is outside this record: the caller supplies it.
/// </summary>
/// <param name="Is3D">True when the connection takes the 3D path (ctx+0xDC bits 0-1 ≠ 0).</param>
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
