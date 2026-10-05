// fidelity: M6-012
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The two channel-to-channel mixing matrices the robot path uses (M6-012, gapE 2.7, gapF 3.1, gapG 5.3).
///
/// The mixer's general 2D panner (0x00A25FF8, gapE 2.6) is not transcribed here. The inventory settles only
/// the cases the shipped robot media take, and the pan result is exposed to
/// <see cref="WwiseMixerConnection"/> as a caller-supplied matrix rather than computed by this class.
/// <list type="bullet">
/// <item><b>mono → mono is 1.0</b> (gapE 2.7): the standard panner takes the same type-1 config, the mono
/// output row is zeroed and <c>matrix[0][0] = 1.0</c>, for both aux and dry. Every shipped mono item is
/// <c>0x4101</c> and Robot_Bus_1 and Cozmo_Robot are <c>0x4101</c> (gapF 3.1).</item>
/// <item><b>stereo → mono is 0.70710677 per channel</b> (gapG 5.3): the panner's table <c>0xFFA970</c> entry
/// <c>0x3F3504F3</c> is written to <c>matrix[ch][0]</c> for FL and FR, and the pan inputs are not used on
/// this path. All 18 robot-routed stereo items are <c>0x3102</c> under the robot actor-mixers (gapG 5.1).</item>
/// </list>
/// The type-0 (anonymous) input into a type-1 output gives an identity diagonal of 1.0 (gapE 2.7); the
/// shipped media do not use it.
/// </summary>
public static class WwiseMixerPan
{
    /// <summary>The mono → mono gain (gapE 2.7): 1.0.</summary>
    public const float MonoToMonoGain = 1.0f;

    /// <summary>The stereo → mono per-channel gain (gapG 5.3): table 0xFFA970's <c>0x3F3504F3</c>.</summary>
    public const float StereoToMonoGain = 0.70710677f;

    /// <summary>The standard channel config type (gapE 2.7, gapF 3.1): <c>0x4101</c>/<c>0x3102</c> both carry <c>1</c>.</summary>
    public const int StandardConfigType = 1;

    /// <summary>The config-type field (bits 8..11) of the packed AkChannelConfig (gapF 3.1, gapG 5.2).</summary>
    public const int ConfigTypeShift = 8;

    /// <summary>The config-type field mask.</summary>
    public const int ConfigTypeMask = 0x0F;

    /// <summary>The channel count (gapF 3.1): the low byte of the config, <c>1</c> for 0x4101 and <c>2</c> for 0x3102.</summary>
    public static int Channels(uint channelConfig) => (int)(channelConfig & 0xFFu);

    /// <summary>The config type (gapF 3.1, gapG 5.2): the nibble at bits 8..11.</summary>
    public static int ConfigType(uint channelConfig) => (int)((channelConfig >> ConfigTypeShift) & (uint)ConfigTypeMask);

    /// <summary>
    /// Whether the channel mask has its LFE bit. The mask starts at bit 12 and LFE is its bit 3, so the
    /// overall bit is 15 (gapE 2.2). Neither <c>0x4101</c> nor <c>0x3102</c> sets it.
    /// </summary>
    public static bool HasLfe(uint channelConfig) => ((channelConfig >> 15) & 1u) != 0u;

    /// <summary>The channel mask field, bits 12..31 of the packed config (gapG 5.2: byte 1's high nibble plus bytes 2-3).</summary>
    public static uint ChannelMask(uint channelConfig) => channelConfig >> 12;

    /// <summary>
    /// The mixing matrix, row-major as <c>inputChannel × outputChannel</c>, for the channel configs the
    /// inventory settles (gapE 2.7, gapG 5.3). Anything else throws: the general panner arithmetic is not
    /// in the rows, so it is refused rather than guessed.
    /// </summary>
    public static float[] MatrixForChannelConfigs(uint inputConfig, uint outputConfig)
    {
        if (HasLfe(inputConfig) || HasLfe(outputConfig))
            throw new NotSupportedException(
                "Wwise mixer: LFE channel mixing is not settled (gapE 2.2 names an LFE->LFE path but gives no arithmetic)");

        int inChannels = Channels(inputConfig), outChannels = Channels(outputConfig);
        int inType = ConfigType(inputConfig), outType = ConfigType(outputConfig);

        if (outType == StandardConfigType && inType == StandardConfigType)
        {
            if (inChannels == 1 && outChannels == 1) return new[] { MonoToMonoGain };
            // gapG 5.3: the stereo -> mono row is taken when out mask & 0x737 == 4 (0x4101 has mask 4).
            if (inChannels == 2 && outChannels == 1 && (ChannelMask(outputConfig) & 0x737u) == 4u)
                return new[] { StereoToMonoGain, StereoToMonoGain };
        }

        if (inType == 0 && outType == StandardConfigType && inChannels == 1 && outChannels == 1)
            return new[] { MonoToMonoGain };   // anonymous -> standard: identity diagonal (gapE 2.7)

        throw new NotSupportedException(
            $"Wwise mixer: channel configs 0x{inputConfig:X4} -> 0x{outputConfig:X4} are outside the settled " +
            "panner rows (gapE 2.7 mono->mono, gapG 5.3 stereo->mono)");
    }
}

/// <summary>
/// One voice-to-bus connection's mixer state (M6-012, gapE 2.1, 2.2, 2.5): a double-buffered gain pair and
/// the prev/next pan matrices.
///
/// The gain pair is refreshed exactly as the native does (gapE 2.5): the previous end gain becomes the start
/// gain, the new target becomes the end gain, and on the voice's first update the start is forced to the end
/// (or to 0 for the <c>conn+0x6C bit2</c> fade-in), so the first bus frame is not ramped. The pan matrix is
/// the caller's "pan result" (gapE 2.6); for the shipped robot media see <see cref="WwiseMixerPan"/>.
/// </summary>
public sealed class WwiseMixerConnection
{
    private readonly int _inputChannels;
    private readonly int _outputChannels;
    private float[]? _prevMatrix;
    private float[]? _nextMatrix;
    private bool _firstUpdate = true;

    /// <param name="inputChannels">The voice buffer's channel count.</param>
    /// <param name="outputChannels">The destination bus buffer's channel count.</param>
    public WwiseMixerConnection(int inputChannels, int outputChannels)
    {
        if (inputChannels < 1) throw new ArgumentOutOfRangeException(nameof(inputChannels));
        if (outputChannels < 1) throw new ArgumentOutOfRangeException(nameof(outputChannels));
        _inputChannels = inputChannels;
        _outputChannels = outputChannels;
    }

    /// <summary>The voice buffer's channel count.</summary>
    public int InputChannels => _inputChannels;

    /// <summary>The destination bus buffer's channel count.</summary>
    public int OutputChannels => _outputChannels;

    /// <summary>True until the first <see cref="Refresh"/> has run (gapE 2.5, <c>voice+0xCD bit3</c>).</summary>
    public bool FirstUpdatePending => _firstUpdate;

    /// <summary>The start gain of the next <see cref="ConsumeBuffer"/>: <c>conn+0x10·conn+8·extra[0]</c> (gapE 2.1).</summary>
    public float StartGain { get; private set; }

    /// <summary>The end gain of the next <see cref="ConsumeBuffer"/>: <c>conn+0x14·conn+0xC·extra[1]</c> (gapE 2.1).</summary>
    public float EndGain { get; private set; }

    /// <summary>The previous (start) pan matrix, row-major <c>input × output</c> (gapE 2.1, <c>conn+0x24</c>).</summary>
    public float[]? PrevMatrix => _prevMatrix;

    /// <summary>The next (end) pan matrix, row-major <c>input × output</c> (gapE 2.1, <c>conn+0x20</c>).</summary>
    public float[]? NextMatrix => _nextMatrix;

    /// <summary>
    /// The per-frame refresh (gapE 2.5, 0x00A4BE34..0x00A4BFAC). It promotes the last end gain to the start
    /// gain and swaps the matrix pointers, then takes the new target. On the voice's first update it forces
    /// the start to the end (no ramp), unless <paramref name="fadeIn"/> is set, in which case the start gain
    /// is 0 (the <c>conn+0x6C bit2</c> fade-in, 0x00A4C0D8..0x00A4C104).
    /// </summary>
    /// <param name="nextMatrix">The new pan matrix (the panner's result), row-major <c>input × output</c>.</param>
    /// <param name="nextGain">The new composed target gain (gapE 2.1's <c>end</c> product, supplied by the caller).</param>
    /// <param name="fadeIn">The first-update fade-in flag (gapE 2.5). Ignored after the first update.</param>
    public void Refresh(float[] nextMatrix, float nextGain, bool fadeIn = false)
    {
        ArgumentNullException.ThrowIfNull(nextMatrix);
        if (nextMatrix.Length != _inputChannels * _outputChannels)
            throw new ArgumentException(
                $"expected {_inputChannels * _outputChannels} matrix entries, got {nextMatrix.Length}", nameof(nextMatrix));

        float promoted = EndGain;
        _prevMatrix = _nextMatrix;
        _nextMatrix = nextMatrix;
        EndGain = nextGain;
        StartGain = promoted;

        if (_firstUpdate)
        {
            _prevMatrix = _nextMatrix;
            // gapE 2.5: prev gains <- next gains, or 0 for the conn+0x6C bit2 fade-in.
            StartGain = fadeIn ? 0f : EndGain;
            _firstUpdate = false;
        }
    }

    /// <summary>
    /// ConsumeBuffer (gapE 2.1, 2.2, 2.3) for the earlier model (planar per-channel arrays; no engine state): it zero-pads every input channel from <paramref name="validFrames"/> to <paramref name="maxFrames"/> and runs the matrix mixer
    /// <see cref="MatrixMixA45E9C"/> with this connection's <see cref="StartGain"/> and <see cref="EndGain"/> as the composed gains. The engine's entry is <see cref="WwiseVoiceConnection.MixA4FBEC"/> (<c>0xA4FBEC</c>), which gates and pads on
    /// <c>u16 [state+0xE]</c> and composes the gains from the connection's own fields.
    /// </summary>
    /// <param name="input">The voice's planar buffer; each channel array must be at least <paramref name="maxFrames"/> long.</param>
    /// <param name="destination">The bus's planar accumulation buffer, one array per output channel.</param>
    /// <param name="validFrames">The voice buffer's valid frame count, zero-padded up to <paramref name="maxFrames"/>.</param>
    /// <param name="maxFrames">The bus frame size <c>bus+0x58</c>.</param>
    public void ConsumeBuffer(float[][] input, float[][] destination, int validFrames, int maxFrames)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(destination);
        if (input.Length != _inputChannels)
            throw new ArgumentException($"expected {_inputChannels} input channels, got {input.Length}", nameof(input));
        if (destination.Length != _outputChannels)
            throw new ArgumentException($"expected {_outputChannels} output channels, got {destination.Length}", nameof(destination));
        if (_nextMatrix is null || _prevMatrix is null)
            throw new InvalidOperationException("Refresh must be called before ConsumeBuffer (gapE 2.5)");
        if (validFrames < 0 || validFrames > maxFrames)
            throw new ArgumentOutOfRangeException(nameof(validFrames));
        if (maxFrames <= 0) return;

        // gapE 2.1: zero-pad the voice buffer from uValidFrames to uMaxFrames, per channel.
        var sources = new ReadOnlyMemory<float>[_inputChannels];
        for (int i = 0; i < _inputChannels; i++)
        {
            var source = input[i];
            if (source.Length < maxFrames)
                throw new ArgumentException($"input channel {i} is shorter than {maxFrames} frames", nameof(input));
            for (int k = validFrames; k < maxFrames; k++) source[k] = 0f;
            sources[i] = source;
        }
        MatrixMixA45E9C(sources, destination, StartGain, EndGain, maxFrames);
    }

    /// <summary>
    /// The matrix mixer <c>0xA45E9C(S, D, {g0, g1}, A, B, 1/frames, frames)</c> (V2-06): the OUTER loop is over the source channels <c>s</c>, the inner over the destination channels <c>d</c>; for each pair <c>start = a * g0</c>,
    /// <c>inc = (1/frames) * ((b * g1) - start)</c> (<c>vmul</c>, <c>vnmls</c>, <c>vmul</c>: float32, not fused, each rounded) and <c>0xA46668(dst[d], src[s], start, inc, frames)</c>. The LFE branch (<c>0xA45FD0..0xA46078</c>) is not adopted: a
    /// source or destination channel configuration with the LFE bit (15) is refused by the callers (<see cref="WwiseMixerPan.MatrixForChannelConfigs"/>, <see cref="WwiseVoiceConnection.MixA4FBEC"/>). The matrices are <c>a = prev[s][d]</c>, <c>b = next[s][d]</c>
    /// in this class's row-major layout (the engine's rows are padded to a multiple of 4 floats: a layout difference only).
    /// </summary>
    internal void MatrixMixA45E9C(ReadOnlyMemory<float>[] sources, float[][] destination, float g0, float g1, int frames)
    {
        float oneOverFrames = 1f / frames;                                           // [bus+0x5C]
        for (int i = 0; i < _inputChannels; i++)                                     // 0xA45F18..0xA45FC4: the outer loop over the source channels
        {
            for (int j = 0; j < _outputChannels; j++)                                // 0xA45F44..0xA45F90
            {
                int m = i * _outputChannels + j;
                float start = _prevMatrix![m] * g0;                                  // 0xA45F6C vmul.f32 s15,s14,s15
                float product = _nextMatrix![m] * g1;                                // 0xA45F7C vnmls.f32 s12,s13,s14: -start + b * g1
                float diff = product - start;
                float inc = oneOverFrames * diff;                                    // 0xA45F80 vmul.f32 s15,s16,s12
                WwiseMixKernels.RampAccumulateA46668(sources[i].Span, destination[j], start, inc, frames);   // 0xA45F88 bl 0xA46668
            }
        }
    }
}

/// <summary>The NEON accumulate kernel of the mixer (M6-012, V2-07).</summary>
public static class WwiseMixKernels
{
    /// <summary>Advanced SIMD flush-to-zero: a denormal operand or result of a NEON q-register f32 operation becomes a zero of the same sign (FPSCR.FZ is not consulted; the scalar VFP operations of the kernel are unaffected).</summary>
    private static float Z(float x) => float.IsSubnormal(x) ? (BitConverter.SingleToUInt32Bits(x) >> 31 != 0 ? -0f : 0f) : x;

    /// <summary>
    /// <c>0xA46668(src, dst, start, inc, frames)</c> (V2-07, 8 frames per iteration; <c>ceil(frames / 8) * 8</c> frames are processed, so a frame count that is not a multiple of 8 overruns the buffers: refused as a visible stop).
    /// <c>inc == 0</c> (a float compare with 0.0, -0.0 included; a NaN is not equal): <c>start == 0</c> returns, else <c>dst[i] += src[i] * lane</c> with the lanes <c>{start, start + 0.0, start + 0.0, start + 0.0}</c>. Otherwise the lane gains of
    /// the first four frames are <c>{start, start + inc, (inc + inc) + start, start + inc * 3.0f}</c> (the fourth by <c>vmla</c>: the product rounded, then added), those of the second four are those plus <c>inc * 4.0f</c>, and after every block of 8
    /// both lane vectors get <c>(inc * 4.0f) + (inc * 4.0f)</c> added (accumulated, NOT <c>start + k * inc</c>); <c>dst = dst + src * lane</c> per lane (not fused). Every q-register operation flushes denormal operands and results to zero (NEON); the scalar lane set-up (<c>vadd/vmla/vmul</c> on s registers) does not.
    /// </summary>
    public static void RampAccumulateA46668(ReadOnlySpan<float> src, Span<float> dst, float start, float inc, int frames)
    {
        if (frames % 8 != 0)
            throw new WwiseMissingBehaviourException("M6-012 V2-07: 0xA46668 processes ceil(frames / 8) * 8 frames; a frame count that is not a multiple of 8 overruns the buffers (not modelled)");
        float zero = 0f;
        if (inc == 0f)                                                              // 0xA46674 vcmp.f32 s15,#0; bne 0xA4671C
        {
            if (start == zero) return;                                              // 0xA466A0..0xA466AC
            float lane = start + zero;                                              // 0xA466B8 vadd.f32 s13,s14,s13
            for (int i = 0; i < frames; i += 8)                                     // 0xA466CC..0xA4670C
            {
                for (int k = 0; k < 8; k++)
                {
                    float g = k % 4 == 0 ? start : lane;                            // lane 0 is the raw start, lanes 1..3 are start + 0.0f
                    float p = Z(Z(src[i + k]) * Z(g));                              // vmul.f32 q11,q11,q12 (NEON: operands and result flushed)
                    dst[i + k] = Z(Z(dst[i + k]) + p);                              // vadd.f32 q9,q9,q11
                }
            }
            return;
        }
        float three = 3f, four = 4f;
        var a = new float[4];                                                       // q12: the lane gains of the first four frames
        a[0] = start;                                                               // 0xA46720 str r2,[r3]
        float twice = inc + inc;                                                    // 0xA4672C vadd.f32 s13,s15,s15
        a[1] = start + inc;                                                         // 0xA4673C vadd.f32 s14,s14,s15
        a[2] = twice + start;                                                       // 0xA46738 vadd.f32 s13,s13,s14
        float triple = inc * three;                                                 // 0xA46728 vmla.f32 s12,s15,s13: s12 = start + inc * 3.0f (the product rounded, then added)
        a[3] = start + triple;
        float inc4 = inc * four;                                                    // 0xA46744 vmul.f32 s15,s15,s12
        var b = new float[4];                                                       // q13: a + 4*inc
        for (int k = 0; k < 4; k++) b[k] = Z(Z(a[k]) + Z(inc4));                    // 0xA46754 vadd.f32 q13,q12,q14 (NEON)
        float step = Z(Z(inc4) + Z(inc4));                                          // 0xA4675C vadd.f32 q14,q14,q14 (NEON)
        for (int i = 0; i < frames; i += 8)                                         // 0xA46760..0xA467A4
        {
            for (int k = 0; k < 4; k++)
            {
                float p0 = Z(Z(src[i + k]) * Z(a[k]));                              // vmul.f32 q11,q11,q12 (NEON)
                float p1 = Z(Z(src[i + 4 + k]) * Z(b[k]));                          // vmul.f32 q10,q10,q13
                dst[i + k] = Z(Z(dst[i + k]) + p0);                                 // vadd.f32 q9,q9,q11
                dst[i + 4 + k] = Z(Z(dst[i + 4 + k]) + p1);                         // vadd.f32 q8,q8,q10
            }
            for (int k = 0; k < 4; k++)
            {
                b[k] = Z(Z(b[k]) + Z(step));                                        // 0xA46784 vadd.f32 q13,q13,q14
                a[k] = Z(Z(a[k]) + Z(step));                                        // 0xA46794 vadd.f32 q12,q12,q14
            }
        }
    }
}
