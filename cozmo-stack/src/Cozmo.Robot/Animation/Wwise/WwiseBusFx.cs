// fidelity: M6-013
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The Parametric EQ filter types the bank can carry (gapC 4.2, 4.3). The values are the u32 type field of a
/// band; the labels are the row's:
/// <c>0 LP, 1 HP, 2 band-pass, 3 notch, 4 low shelf, 5 high shelf, 6 peaking</c>.
/// </summary>
public enum WwiseEqFilterType : uint
{
    /// <summary>Butterworth low pass (gapC 4.3). Q ignored.</summary>
    LowPass = 0,

    /// <summary>Butterworth high pass (gapC 4.3). Q ignored.</summary>
    HighPass = 1,

    /// <summary>RBJ band pass (gapC 4.3).</summary>
    BandPass = 2,

    /// <summary>RBJ notch (gapC 4.3).</summary>
    Notch = 3,

    /// <summary>RBJ low shelf, S = 1 fixed (gapC 4.3). Q unused.</summary>
    LowShelf = 4,

    /// <summary>RBJ high shelf, S = 1 fixed (gapC 4.3). Q unused.</summary>
    HighShelf = 5,

    /// <summary>RBJ peaking (gapC 4.3).</summary>
    Peaking = 6,
}

/// <summary>One EQ band's settings (gapC 4.2): <c>{u32 type, f32 gain dB, f32 freq, f32 Q, u8 on}</c>.</summary>
/// <param name="Type">The u32 type field; see <see cref="WwiseEqFilterType"/>.</param>
/// <param name="GainDb">The band gain in dB. Unused by the Butterworth LP/HP (gapC 4.3).</param>
/// <param name="Frequency">The band frequency in Hz, capped at <c>0.45·fs</c> by the coefficient routine.</param>
/// <param name="Q">The band Q. Ignored by LP/HP and by the shelves (gapC 4.3).</param>
/// <param name="On">Whether the band is applied (gapC 4.4). An off band is skipped.</param>
public readonly record struct WwiseEqBand(uint Type, float GainDb, float Frequency, float Q, bool On)
{
    /// <summary>The band's filter type.</summary>
    public WwiseEqFilterType FilterType => (WwiseEqFilterType)Type;
}

/// <summary>
/// The settings block a Parametric EQ ShareSet carries (M6-013, gapC 4.2, <c>SetParamsBlock 0xAA2E8C</c>,
/// 56 bytes): <c>3 × {u32 type, f32 gain dB, f32 freq, f32 Q, u8 on}</c>, then <c>f32 outputLevel dB</c> and
/// <c>u8 processLFE</c>.
///
/// <para>The two ShareSets on <c>Robot_Bus_1</c> are the shipped values, from Init.bnk HIRC type 18:</para>
/// <list type="bullet">
/// <item><c>0x6767FC1F</c>: band 1 low shelf +2.0 dB @835 Hz (Q 2.1 unused), band 2 peaking −2.5 dB @1359 Hz
/// Q 4.2, band 3 peaking −4.0 dB @5091 Hz Q 1.5, all on; output +1.5 dB.</item>
/// <item><c>0x174901C6</c>: band 1 high pass @333 Hz on, band 2 peaking −4.5 dB @1000 Hz Q 0.5 <b>off</b>,
/// band 3 low pass @14298 Hz on; output 0 dB.</item>
/// </list>
///
/// <para>The row gives the LP/HP bands no gain and no Q; both are ignored by the Butterworth design
/// (gapC 4.3), so they are carried as zero. The row gives <c>processLFE</c> as a field but not its value in
/// either ShareSet; it is not invented here — it defaults to false, and it cannot act on the mono robot bus
/// (<c>0x4101</c>, no LFE) either way.</para>
/// </summary>
public sealed class WwiseEqSettings
{
    /// <summary>The first Robot_Bus_1 EQ ShareSet (gapC 4.2), full name <c>0x6767FC1F</c>.</summary>
    public const uint ShareSet1 = 0x6767FC1F;

    /// <summary>The second Robot_Bus_1 EQ ShareSet (gapC 4.2), full name <c>0x174901C6</c>.</summary>
    public const uint ShareSet2 = 0x174901C6;

    /// <summary>The three bands, in order.</summary>
    public IReadOnlyList<WwiseEqBand> Bands { get; }

    /// <summary>The post-EQ output level in dB (gapC 4.2).</summary>
    public float OutputLevelDb { get; }

    /// <summary>
    /// The <c>processLFE</c> byte (gapC 4.2). The row gives the field but not its shipped value; it is inert
    /// on the mono robot bus, so it is carried without inventing one.
    /// </summary>
    public bool ProcessLfe { get; }

    /// <summary>The ShareSet id this settings block came from, or 0 for a caller-constructed block.</summary>
    public uint ShareSetId { get; }

    private WwiseEqSettings(uint shareSetId, IReadOnlyList<WwiseEqBand> bands, float outputLevelDb, bool processLfe)
    {
        ShareSetId = shareSetId;
        Bands = bands;
        OutputLevelDb = outputLevelDb;
        ProcessLfe = processLfe;
    }

    /// <summary>Builds a settings block for a caller-supplied ShareSet (used for tests and future banks).</summary>
    public static WwiseEqSettings For(uint shareSetId, IReadOnlyList<WwiseEqBand> bands,
                                      float outputLevelDb, bool processLfe = false)
    {
        ArgumentNullException.ThrowIfNull(bands);
        if (bands.Count != 3) throw new ArgumentException("an EQ ShareSet has exactly three bands (gapC 4.2)", nameof(bands));
        return new(shareSetId, bands, outputLevelDb, processLfe);
    }

    /// <summary>Robot_Bus_1 slot 0: <c>0x6767FC1F</c> (gapC 4.2).</summary>
    public static WwiseEqSettings Eq1 { get; } = new(ShareSet1, new[]
    {
        new WwiseEqBand((uint)WwiseEqFilterType.LowShelf, 2.0f, 835f, 2.1f, true),
        new WwiseEqBand((uint)WwiseEqFilterType.Peaking, -2.5f, 1359f, 4.2f, true),
        new WwiseEqBand((uint)WwiseEqFilterType.Peaking, -4.0f, 5091f, 1.5f, true),
    }, 1.5f, false);

    /// <summary>Robot_Bus_1 slot 1: <c>0x174901C6</c> (gapC 4.2).</summary>
    public static WwiseEqSettings Eq2 { get; } = new(ShareSet2, new[]
    {
        new WwiseEqBand((uint)WwiseEqFilterType.HighPass, 0f, 333f, 0f, true),
        new WwiseEqBand((uint)WwiseEqFilterType.Peaking, -4.5f, 1000f, 0.5f, false),
        new WwiseEqBand((uint)WwiseEqFilterType.LowPass, 0f, 14298f, 0f, true),
    }, 0f, false);
}

/// <summary>
/// A direct-form-I biquad's stored coefficients (gapC 4.3): <c>{b0, b1, b2, −a1, −a2} / a0</c>. <see cref="NegA1"/>
/// and <see cref="NegA2"/> are the stored <c>−a1</c> and <c>−a2</c>, so the difference equation is
/// <c>y = ((((b2·x2 + x·b0) + b1·x1) + (−a2)·y2) + (−a1)·y1)</c> (gapC 4.4).
/// </summary>
public readonly record struct WwiseBiquadCoefficients(float B0, float B1, float B2, float NegA1, float NegA2);

/// <summary>
/// The Parametric EQ coefficient routine (M6-013, gapC 4.3, <c>0xAA25E0</c>).
///
/// <para><b>What the row fixes.</b> <c>fs = format rate</c>; <c>fc = min(freq, 0.45·fs)</c>. The filter
/// families and their intermediates are named: Butterworth LP <c>c = 1/tan(π fc/fs)</c> with
/// <c>b0 = 1/(1+√2c+c²)</c>; Butterworth HP <c>c = tan(π fc/fs)</c> with the same <c>b0</c>; RBJ
/// band-pass/notch/peaking with <c>w0 = 2π fc/fs</c>, <c>α = sin/(2Q)</c>, <c>A = 10^(gain/40)</c>; RBJ
/// low/high shelf with <c>S = 1</c> fixed (the <c>(1/S−1)</c> term is the constant 0.0, Q unused) and
/// <c>α = sin·√2/2</c>. Coefficients are stored as <c>{b0, b1, b2, −a1, −a2} / a0</c> and recomputed only
/// when dirty, with no smoothing.</para>
///
/// <para><b>What the row does not transcribe.</b> The row gives the named designs and their intermediates
/// but not the numerator/denominator polynomials themselves. The Butterworth companion terms here are the
/// same design the inventory writes out in full for the voice filter (gapE 1.6: <c>b1 = 2b0</c>,
/// <c>b2 = b0</c>, <c>−a1 = −2(1−c²)b0</c>, <c>−a2 = −(c²−√2c+1)b0</c> for LP, and the HP mirror), and the
/// RBJ terms are the named cookbook's. The shelf <c>α</c> is the row's own <c>sin·√2/2</c>, which is not the
/// usual <c>sin(w0/2)·√2</c>; it is used literally here and flagged as a choice.</para>
/// </summary>
public static class WwiseEqCoefficients
{
    private static readonly float Sqrt2 = MathF.Sqrt(2f);

    /// <summary>
    /// Designs one band's coefficients (gapC 4.3): <c>fc = min(freq, 0.45·fs)</c>, then the filter type's
    /// formulas. The band's <c>On</c> flag is the caller's concern (gapC 4.4 skips an off band).
    /// </summary>
    public static WwiseBiquadCoefficients Design(WwiseEqBand band, float sampleRate)
    {
        if (!(sampleRate > 0f)) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        float fc = MathF.Min(band.Frequency, 0.45f * sampleRate);
        float w0 = 2f * MathF.PI * fc / sampleRate;
        float cos = MathF.Cos(w0);
        float sin = MathF.Sin(w0);

        float b0, b1, b2, a0, a1, a2;
        switch (band.FilterType)
        {
            case WwiseEqFilterType.LowPass:
            {
                float c = 1f / MathF.Tan(MathF.PI * fc / sampleRate);
                float c2 = c * c;
                b0 = 1f / (1f + Sqrt2 * c + c2);
                b1 = 2f * b0;
                b2 = b0;
                a0 = 1f;
                // gapE 1.6 writes the stored values directly: −a1 = −2(1−c²)b0 and −a2 = −(c²−√2c+1)b0.
                // The return below divides by a0 after negating, so these locals carry the standard a1/a2
                // (the negation of the stored ones) and the stored value comes out unchanged.
                a1 = 2f * (1f - c2) * b0;
                a2 = (c2 - Sqrt2 * c + 1f) * b0;
                break;
            }
            case WwiseEqFilterType.HighPass:
            {
                float c = MathF.Tan(MathF.PI * fc / sampleRate);
                float c2 = c * c;
                b0 = 1f / (1f + Sqrt2 * c + c2);
                b1 = -2f * b0;
                b2 = b0;
                a0 = 1f;
                // gapE 1.6: stored −a1 = −2b0(c²−1), −a2 = −(c²−√2c+1)b0. See the LP note.
                a1 = 2f * b0 * (c2 - 1f);
                a2 = (c2 - Sqrt2 * c + 1f) * b0;
                break;
            }
            case WwiseEqFilterType.BandPass:
            {
                float alpha = sin / (2f * band.Q);
                b0 = alpha;
                b1 = 0f;
                b2 = -alpha;
                a0 = 1f + alpha;
                a1 = -2f * cos;
                a2 = 1f - alpha;
                break;
            }
            case WwiseEqFilterType.Notch:
            {
                float alpha = sin / (2f * band.Q);
                b0 = 1f;
                b1 = -2f * cos;
                b2 = 1f;
                a0 = 1f + alpha;
                a1 = -2f * cos;
                a2 = 1f - alpha;
                break;
            }
            case WwiseEqFilterType.LowShelf:
            {
                float a = MathF.Pow(10f, band.GainDb / 40f);
                float alpha = ShelfAlpha(sin);
                float beta = 2f * MathF.Sqrt(a) * alpha;
                b0 = a * ((a + 1f) - (a - 1f) * cos + beta);
                b1 = 2f * a * ((a - 1f) - (a + 1f) * cos);
                b2 = a * ((a + 1f) - (a - 1f) * cos - beta);
                a0 = (a + 1f) + (a - 1f) * cos + beta;
                a1 = -2f * ((a - 1f) + (a + 1f) * cos);
                a2 = (a + 1f) + (a - 1f) * cos - beta;
                break;
            }
            case WwiseEqFilterType.HighShelf:
            {
                float a = MathF.Pow(10f, band.GainDb / 40f);
                float alpha = ShelfAlpha(sin);
                float beta = 2f * MathF.Sqrt(a) * alpha;
                b0 = a * ((a + 1f) + (a - 1f) * cos + beta);
                b1 = -2f * a * ((a - 1f) + (a + 1f) * cos);
                b2 = a * ((a + 1f) + (a - 1f) * cos - beta);
                a0 = (a + 1f) - (a - 1f) * cos + beta;
                a1 = 2f * ((a - 1f) - (a + 1f) * cos);
                a2 = (a + 1f) - (a - 1f) * cos - beta;
                break;
            }
            case WwiseEqFilterType.Peaking:
            {
                float a = MathF.Pow(10f, band.GainDb / 40f);
                float alpha = sin / (2f * band.Q);
                b0 = 1f + alpha * a;
                b1 = -2f * cos;
                b2 = 1f - alpha * a;
                a0 = 1f + alpha / a;
                a1 = -2f * cos;
                a2 = 1f - alpha / a;
                break;
            }
            default:
                throw new NotSupportedException(
                    $"EQ filter type {band.Type} is not one of the seven named in gapC 4.3 (M6-013)");
        }

        return new WwiseBiquadCoefficients(b0 / a0, b1 / a0, b2 / a0, -a1 / a0, -a2 / a0);
    }

    /// <summary>
    /// The shelf's <c>α</c>. The row (gapC 4.3) writes <c>α = sin·√2/2</c> with S = 1; the usual RBJ
    /// S = 1 value is <c>sin(w0/2)·√2</c>. The row's literal expression is used, and the difference is a
    /// recorded choice (see the class remarks).
    /// </summary>
    private static float ShelfAlpha(float sinW0) => sinW0 * (Sqrt2 / 2f);

    /// <summary>The coefficient routine's cap fraction of the sample rate (gapC 4.3): <c>0.45·fs</c>.</summary>
    public const float NyquistFraction = 0.45f;
}

/// <summary>
/// The scalar direct-form-I biquad (M6-013, gapC 4.4, <c>0xAA2324</c>). The accumulation order is the row's:
/// <c>y = ((((b2·x2 + x·b0) + b1·x1) + (−a2)·y2) + (−a1)·y1)</c>, evaluated left to right in float32; state
/// is <c>{x1, x2, y1, y2}</c>.
///
/// <para>The native's aligned 4-sample blocks use NEON (gapC 4.4), where the multiply-adds may not fuse and
/// the order is the block's; the row does not transcribe that ordering, so every sample here goes through
/// this scalar form. That is equivalent arithmetic, not bit-exact (MD3).</para>
/// </summary>
public sealed class WwiseEqBiquad
{
    private WwiseBiquadCoefficients _c;
    private float _x1, _x2, _y1, _y2;

    /// <summary>The current coefficients.</summary>
    public WwiseBiquadCoefficients Coefficients => _c;

    /// <summary>The stored history <c>{x1, x2, y1, y2}</c>.</summary>
    public (float X1, float X2, float Y1, float Y2) History => (_x1, _x2, _y1, _y2);

    /// <summary>Replaces the coefficients; history is kept (gapC 4.3: no smoothing).</summary>
    public void SetCoefficients(WwiseBiquadCoefficients c) => _c = c;

    /// <summary>Filters <c>[from, from + count)</c> in place.</summary>
    public void Process(Span<float> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            float x = buffer[i];
            float y = _c.B2 * _x2 + x * _c.B0 + _c.B1 * _x1 + _c.NegA2 * _y2 + _c.NegA1 * _y1;
            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;
            buffer[i] = y;
        }
    }

    /// <summary>Clears the four history slots.</summary>
    public void Reset() => (_x1, _x2, _y1, _y2) = (0f, 0f, 0f, 0f);
}

/// <summary>
/// The EQ's (and the limiter's) output-gain ramp (M6-013, gapC 4.4). The target is <c>10^(0.05·outLevel)</c>.
/// If it equals the stored previous gain (<c>fx+0x50</c>) it multiplies, skipped when the gain is 1.0;
/// otherwise it ramps linearly over the buffer. <c>Init</c> sets the previous gain to the target, so there is
/// no start ramp.
///
/// <para><b>The quirk.</b> The native ramps in NEON 4-sample blocks, then its scalar tail <b>restarts</b> the
/// ramp from the previous gain instead of continuing it. That restart is reproduced here. The row describes
/// the tail's restart but not the NEON lane increments, so the aligned part uses a single per-sample delta;
/// this is the recorded interpretation, not a bit-exact block (MD3).</para>
/// </summary>
internal sealed class WwiseOutputGainRamp
{
    private float _previousGain = 1f;

    /// <summary>The stored previous gain (<c>fx+0x50</c>).</summary>
    public float PreviousGain => _previousGain;

    /// <summary>Init: the stored previous gain starts equal to the target, so the first buffer does not ramp.</summary>
    public void Init(float targetGain) => _previousGain = targetGain;

    /// <summary>
    /// Applies the output gain to <paramref name="buffer"/>. <paramref name="targetGain"/> is
    /// <c>10^(0.05·outLevel)</c>.
    /// </summary>
    public void Apply(Span<float> buffer, float targetGain)
    {
        float previous = _previousGain;
        if (targetGain == previous)
        {
            if (targetGain != 1f)
            {
                for (int i = 0; i < buffer.Length; i++) buffer[i] *= targetGain;
            }
            return;
        }

        int n = buffer.Length;
        if (n == 0) { _previousGain = targetGain; return; }
        float delta = (targetGain - previous) / n;
        int aligned = n & ~3;
        for (int i = 0; i < aligned; i++) buffer[i] *= previous + (i + 1) * delta;

        // gapC 4.4 quirk: the scalar tail after the NEON 4-sample loop restarts the ramp from prev.
        for (int i = aligned; i < n; i++) buffer[i] *= previous + (i - aligned + 1) * delta;

        _previousGain = targetGain;
    }

    /// <summary>Forgets the previous gain, ready for a fresh piece of audio.</summary>
    public void Reset() => _previousGain = 1f;
}

/// <summary>
/// The Parametric EQ plug-in (M6-013, gapC 4.2..4.4). For each band that is <b>on</b>, a direct-form-I biquad
/// runs in place; then the output gain is applied with the ramp above.
///
/// <para>The shipped bus is mono (<c>0x4101</c>, gapF 3.1), so this class processes one channel. The row's
/// per-planar-channel state (<c>Init</c> allocates <c>numCh·3·16</c> bytes) is not modelled; a multi-channel
/// caller would need the per-channel biquads, which the row does not otherwise change.</para>
///
/// <para>This is standalone: it is not wired into WwisePlayback/WwiseAudioSource/WwiseSongRenderer/
/// AnimationScheduler or the M6-014 bus lifetime.</para>
/// </summary>
public sealed class WwiseParametricEq
{
    private readonly WwiseEqSettings _settings;
    private readonly float _sampleRate;
    private readonly WwiseEqBiquad[] _bands;
    private readonly WwiseOutputGainRamp _outputGain = new();

    /// <summary>Builds an EQ for the settings at a sample rate.</summary>
    public WwiseParametricEq(WwiseEqSettings settings, float sampleRate)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!(sampleRate > 0f)) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        _settings = settings;
        _sampleRate = sampleRate;
        _bands = new WwiseEqBiquad[settings.Bands.Count];
        for (int i = 0; i < settings.Bands.Count; i++)
        {
            _bands[i] = new WwiseEqBiquad();
            _bands[i].SetCoefficients(WwiseEqCoefficients.Design(settings.Bands[i], sampleRate));
        }
        _outputGain.Init(WwiseGain.DbToLinear(settings.OutputLevelDb));
    }

    /// <summary>The settings this EQ runs.</summary>
    public WwiseEqSettings Settings => _settings;

    /// <summary>The stored previous output gain (<c>fx+0x50</c>).</summary>
    public float PreviousOutputGain => _outputGain.PreviousGain;

    /// <summary>The coefficients a band currently carries, for the compare.</summary>
    public WwiseBiquadCoefficients BandCoefficients(int band) => _bands[band].Coefficients;

    /// <summary>The history a band currently carries, for the compare.</summary>
    public (float X1, float X2, float Y1, float Y2) BandHistory(int band) => _bands[band].History;

    /// <summary>
    /// gapC 4.4: runs each <b>on</b> band's biquad in place, in band order, then applies the output gain.
    /// </summary>
    public void Process(Span<float> buffer)
    {
        for (int i = 0; i < _bands.Length; i++)
        {
            if (!_settings.Bands[i].On) continue;
            _bands[i].Process(buffer);
        }

        _outputGain.Apply(buffer, WwiseGain.DbToLinear(_settings.OutputLevelDb));
    }

    /// <summary>Clears every band's history and the output-gain ramp's stored gain.</summary>
    public void Reset()
    {
        foreach (var band in _bands) band.Reset();
        _outputGain.Reset();
        _outputGain.Init(WwiseGain.DbToLinear(_settings.OutputLevelDb));
    }

    /// <summary>The sample rate in Hz.</summary>
    public float SampleRate => _sampleRate;
}

/// <summary>One Peak Limiter settings block (gapC 4.5, <c>0xAA2084</c>, 22 bytes).</summary>
/// <param name="ThresholdDb">The threshold in dB (native <c>+4</c>).</param>
/// <param name="Ratio">The ratio (native <c>+8</c>).</param>
/// <param name="LookAheadSeconds">The look-ahead in seconds (native <c>+0x18</c>).</param>
/// <param name="ReleaseSeconds">The release in seconds (native <c>+0xC</c>).</param>
/// <param name="OutputDb">The output level in dB (native <c>+0x10</c>; stored as <c>powf(10, 0.05·v)</c>).</param>
/// <param name="ProcessLfe">The <c>processLFE</c> byte (native <c>+0x1C</c>).</param>
/// <param name="ChannelLink">The <c>channelLink</c> byte (native <c>+0x1D</c>).</param>
public readonly record struct WwisePeakLimiterSettings(
    float ThresholdDb,
    float Ratio,
    float LookAheadSeconds,
    float ReleaseSeconds,
    float OutputDb,
    bool ProcessLfe,
    byte ChannelLink)
{
    /// <summary>The plug-in's defaults (gapC 4.5, 0xAA20FC): <c>−12, 10, 0.01, 0.2, 1.0, 1, 1</c>.</summary>
    public static WwisePeakLimiterSettings Default { get; } = new(-12f, 10f, 0.01f, 0.2f, 1.0f, true, 1);

    /// <summary>The Robot_Bus_1 limiter ShareSet (gapC 4.5, <c>0xDF2230FF</c>).</summary>
    public const uint ShareSet = 0xDF2230FF;

    /// <summary>
    /// <c>0xDF2230FF</c>: threshold −1.0 dB, ratio 10.8, look-ahead 0.009 s, release 0.041 s, output 0 dB,
    /// processLFE 0, channelLink 0.
    /// </summary>
    public static WwisePeakLimiterSettings RobotBus1 { get; } =
        new(-1f, 10.8f, 0.009f, 0.041f, 0f, false, 0);
}

/// <summary>
/// The Peak Limiter DSP (M6-013, gapC 4.6, 4.7; setup <c>0xAA19CC</c>, simd process <c>0xAA0EB4</c>).
///
/// <list type="bullet">
/// <item><b>Look-ahead length.</b> <c>L = u32(float(sr)·lookahead)</c>; at 48 kHz and 0.009 s that is 431,
/// not 432, because the float product truncates (gapC 4.6).</item>
/// <item><b>Coefficients.</b> <c>attack = expf(−2.2/(L/2))</c>, <c>release = expf(−2.2/(sr·release))</c>
/// (gapC 4.6).</item>
/// <item><b>Per sample</b> (gapC 4.7): write <c>x</c> into the delay line and read the sample from <c>L</c>
/// ago (<c>d</c>); peak-hold with <c>over = max(0, 20·log10(peak) − thr)</c> using the fast log; envelope
/// <c>env = over + c·(env − over)</c> with <c>c = attack</c> when <c>over ≥ env</c> else <c>release</c>;
/// <c>gain = fastpow10(0.05·(1/R − 1)·env)</c> with the factor computed in double; <c>out = d·gain</c>; then
/// the output-gain ramp, identical to the EQ's (same quirk).</item>
/// <item><b>First buffer.</b> A pre-scan seeds the peak over <c>min(frames, L)</c> samples (gapC 4.7).</item>
/// <item><b>Tail.</b> NoMoreData (<c>0x11</c>) extends the output with an <c>L</c>-sample tail and returns
/// <c>0x2D</c> while the tail remains (gapC 4.7).</item>
/// </list>
///
/// <para><b>Not transcribed.</b> The row names the linked (<c>0xAA1464</c>) and LFE (<c>0xAA09B8</c>)
/// process variants but gives no arithmetic for them; the shipped ShareSet is unlinked
/// (<c>channelLink = 0</c>), so this class builds only the unlinked/mono path and refuses the rest.
/// The native's fast log and fast pow are rebuilt exactly as the rows give them; whether the limiter's
/// fastpow10 carries the dBToLin cutoff is not stated, so the shared fast pow is used unchanged.</para>
/// </summary>
public sealed class WwisePeakLimiter
{
    /// <summary>The input status that asks for the tail (gapC 4.7).</summary>
    public const int NoMoreData = 0x11;

    /// <summary>The return the native gives while the tail remains (gapC 4.7).</summary>
    public const int TailRemaining = 0x2D;

    private readonly WwisePeakLimiterSettings _settings;
    private readonly float _sampleRate;
    private readonly int _l;
    private readonly float _attack;
    private readonly float _release;
    private readonly float[] _delay;
    private readonly WwiseOutputGainRamp _outputGain = new();

    private int _writeIndex;
    private int _written;
    private bool _first = true;
    private float _peak;
    private float _held;
    private int _hold;
    private float _over;
    private float _envelope;
    private float _gain = 1f;
    private int _tailRead;

    /// <summary>Builds a limiter for the settings at a sample rate.</summary>
    public WwisePeakLimiter(WwisePeakLimiterSettings settings, float sampleRate)
    {
        if (!(sampleRate > 0f)) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (settings.ChannelLink != 0)
            throw new NotSupportedException(
                "the linked limiter detector (0xAA1464) has no arithmetic in gapC 4.6/4.7 (M6-013); " +
                "only the unlinked/mono path (0xAA0EB4) is built");
        if (!(settings.LookAheadSeconds > 0f))
            throw new ArgumentOutOfRangeException(nameof(settings), "the look-ahead must be positive");

        _settings = settings;
        _sampleRate = sampleRate;
        _l = (int)(sampleRate * settings.LookAheadSeconds);       // u32(float(sr)·lookahead)
        if (_l < 1) throw new ArgumentOutOfRangeException(nameof(settings), "the look-ahead truncates to zero samples");
        _attack = MathF.Exp(-2.2f / (_l / 2f));
        _release = MathF.Exp(-2.2f / (sampleRate * settings.ReleaseSeconds));
        _delay = new float[_l];
        _outputGain.Init(WwiseGain.DbToLinear(settings.OutputDb));
    }

    /// <summary>The settings this limiter runs.</summary>
    public WwisePeakLimiterSettings Settings => _settings;

    /// <summary>The look-ahead length in samples, <c>L</c> (gapC 4.6).</summary>
    public int LookAheadSamples => _l;

    /// <summary>The attack coefficient <c>expf(−2.2/(L/2))</c> (gapC 4.6).</summary>
    public float Attack => _attack;

    /// <summary>The release coefficient <c>expf(−2.2/(sr·release))</c> (gapC 4.6).</summary>
    public float Release => _release;

    /// <summary>The current envelope (gapC 4.7).</summary>
    public float Envelope => _envelope;

    /// <summary>The current gain the detector applies (gapC 4.7).</summary>
    public float Gain => _gain;

    /// <summary>The stored previous output gain (<c>+0x50</c>).</summary>
    public float PreviousOutputGain => _outputGain.PreviousGain;

    /// <summary>
    /// gapC 4.7, the unlinked/mono path: processes <paramref name="buffer"/> in place. The output is the
    /// input delayed by <c>L</c> samples and multiplied by the detector gain and the output-gain ramp.
    /// </summary>
    public void Process(Span<float> buffer)
    {
        if (_first)
        {
            PreScan(buffer);
            _first = false;
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            float x = buffer[i];

            // Write x, read the sample from L samples ago.
            float d = _delay[_writeIndex];
            _delay[_writeIndex] = x;
            _writeIndex = _writeIndex + 1 == _l ? 0 : _writeIndex + 1;
            if (_written < _l) _written++;

            // Peak hold (gapC 4.7).
            float magnitude = MathF.Abs(x);
            if (_hold == 0 || magnitude > _peak)
            {
                _peak = magnitude;
                _hold = _l;
                _over = MathF.Max(0f, 20f * FastLog10(_peak) - _settings.ThresholdDb);
            }
            else
            {
                _hold--;
            }

            // Envelope (gapC 4.7).
            float c = _over >= _envelope ? _attack : _release;
            _envelope = _over + c * (_envelope - _over);

            // Gain: fastpow10(0.05·(1/R − 1)·env), factor in double (gapC 4.7).
            double factor = 1.0 / _settings.Ratio - 1.0;
            float gainsDb = (float)(factor * _envelope);
            _gain = WwiseGain.DbToLinear(gainsDb);

            buffer[i] = d * _gain;
        }

        _outputGain.Apply(buffer, WwiseGain.DbToLinear(_settings.OutputDb));
    }

    /// <summary>
    /// gapC 4.7: NoMoreData (<c>0x11</c>) extends the output with the <c>L</c>-sample tail still in the delay
    /// line. <paramref name="destination"/> receives up to its length in tail samples; the return is
    /// <see cref="TailRemaining"/> while the tail remains and 0 once it is exhausted. The tail samples pass
    /// through the last detector gain and output-gain ramp, the same as a live sample.
    /// </summary>
    public int DrainTail(Span<float> destination)
    {
        int tailLength = _written > 0 ? _l : 0;
        for (int i = 0; i < destination.Length && _tailRead < tailLength; i++)
        {
            // The next output position N reads the delay line at N mod L; the tail is the L delayed
            // samples that the stream has not yet emitted.
            int readIndex = (_writeIndex + _tailRead) % _l;
            destination[i] = _delay[readIndex] * _gain * WwiseGain.DbToLinear(_settings.OutputDb);
            _tailRead++;
        }
        return _tailRead < tailLength ? TailRemaining : 0;
    }

    /// <summary>Clears the detector, the delay line and the output-gain ramp.</summary>
    public void Reset()
    {
        Array.Clear(_delay, 0, _delay.Length);
        _writeIndex = 0;
        _written = 0;
        _tailRead = 0;
        _first = true;
        _peak = 0f;
        _held = 0f;
        _hold = 0;
        _over = 0f;
        _envelope = 0f;
        _gain = 1f;
        _outputGain.Reset();
        _outputGain.Init(WwiseGain.DbToLinear(_settings.OutputDb));
    }

    /// <summary>
    /// gapC 4.7: a first-buffer pre-scan seeds the peak over <c>min(frames, L)</c> samples. The row names the
    /// peak only, so the envelope is left to the normal per-sample update.
    /// </summary>
    private void PreScan(ReadOnlySpan<float> buffer)
    {
        int count = Math.Min(buffer.Length, _l);
        float peak = 0f;
        for (int i = 0; i < count; i++)
        {
            float m = MathF.Abs(buffer[i]);
            if (m > peak) peak = m;
        }
        _peak = peak;
        _held = peak;
        _hold = count > 0 ? _l : 0;
        _over = MathF.Max(0f, 20f * FastLog10(peak) - _settings.ThresholdDb);
    }

    /// <summary>
    /// The engine's fast log (gapC 4.7): <c>ln(x) = (e−127)·ln2 + 2s(1+s²/3)</c> with the mantissa
    /// <c>m ∈ [1,2)</c> and <c>s = (m−1)/(m+1)</c>, then <c>×log10 e</c>. Non-positive input is treated as
    /// zero, where the caller's <c>max(0, …)</c> makes the result 0.
    /// </summary>
    public static float FastLog10(float x)
    {
        if (!(x > 0f)) return float.NegativeInfinity;
        uint bits = (uint)BitConverter.SingleToInt32Bits(x);
        int e = (int)((bits >> 23) & 0xFF);
        float m = BitConverter.Int32BitsToSingle((int)((bits & 0x007FFFFFu) | 0x3F800000u));
        float s = (m - 1f) / (m + 1f);
        float ln = (e - 127) * MathF.Log(2f) + 2f * s * (1f + s * s / 3f);
        return ln * (1f / MathF.Log(10f));
    }
}

/// <summary>
/// The <c>Robot_Bus_1..4</c> FX chain (M6-013, gapC 3.1, 4.1..4.9, addendum). When the bus state is 1 the
/// slots run in order: Parametric EQ <c>0x6767FC1F</c> → Parametric EQ <c>0x174901C6</c> → Peak Limiter
/// <c>0xDF2230FF</c> → Hijack.
///
/// <para>The Hijack is the Anki tap that carries the bus to the robot (M6-015); it does not change the
/// samples, so it is listed as the last slot and left alone. The bus-state gate and the lifetime are the
/// M6-014 bus's job; <see cref="Process"/> applies the three DSP stages, and
/// <see cref="ProcessIfActive"/> adds the state-1 gate for a caller that does not have M6-014.</para>
///
/// <para>This is standalone: it is not wired into WwisePlayback/WwiseAudioSource/WwiseSongRenderer/
/// AnimationScheduler, and it does not implement <c>IWwiseBusInsertFx</c>, so the M6-014 bus cannot run it
/// until a later wiring pass adapts it.</para>
/// </summary>
public sealed class WwiseRobotBusFx
{
    /// <summary>The only bus state in which the FX run (gapC 3.1, D2.4).</summary>
    public const int StateActive = 1;

    /// <summary>The EQ ShareSet in slot 0 (gapC 4.2).</summary>
    public WwiseParametricEq Eq1 { get; }

    /// <summary>The EQ ShareSet in slot 1 (gapC 4.2).</summary>
    public WwiseParametricEq Eq2 { get; }

    /// <summary>The Peak Limiter ShareSet in slot 2 (gapC 4.5).</summary>
    public WwisePeakLimiter Limiter { get; }

    /// <summary>The chain's slots in order, named as the record names them.</summary>
    public IReadOnlyList<string> Slots { get; } = new[]
    {
        "Parametric EQ 0x6767FC1F",
        "Parametric EQ 0x174901C6",
        "Peak Limiter 0xDF2230FF",
        "Hijack (tap for the robot; does not change the samples)",
    };

    /// <summary>Builds the shipped Robot_Bus_1 chain at a sample rate.</summary>
    public WwiseRobotBusFx(float sampleRate = WwiseRuntimeSettings.MixRateHz)
        : this(WwiseEqSettings.Eq1, WwiseEqSettings.Eq2, WwisePeakLimiterSettings.RobotBus1, sampleRate)
    {
    }

    /// <summary>Builds a chain from explicit settings (used for tests and future buses).</summary>
    public WwiseRobotBusFx(WwiseEqSettings eq1, WwiseEqSettings eq2,
                           WwisePeakLimiterSettings limiter, float sampleRate)
    {
        Eq1 = new WwiseParametricEq(eq1, sampleRate);
        Eq2 = new WwiseParametricEq(eq2, sampleRate);
        Limiter = new WwisePeakLimiter(limiter, sampleRate);
    }

    /// <summary>Applies EQ 0x6767FC1F → EQ 0x174901C6 → limiter, in slot order, to a mono bus buffer.</summary>
    public void Process(Span<float> buffer)
    {
        Eq1.Process(buffer);
        Eq2.Process(buffer);
        Limiter.Process(buffer);
    }

    /// <summary>
    /// gapC 3.1: runs <see cref="Process"/> only when the bus state is
    /// <see cref="StateActive"/>, as the native FX loop does.
    /// </summary>
    public void ProcessIfActive(Span<float> buffer, int busState)
    {
        if (busState != StateActive) return;
        Process(buffer);
    }

    /// <summary>Resets both EQs and the limiter.</summary>
    public void Reset()
    {
        Eq1.Reset();
        Eq2.Reset();
        Limiter.Reset();
    }
}