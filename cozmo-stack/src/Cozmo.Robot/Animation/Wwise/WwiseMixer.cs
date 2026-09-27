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
    /// ConsumeBuffer (gapE 2.1, 2.2, 2.3). It zero-pads every input channel from
    /// <paramref name="validFrames"/> to <paramref name="maxFrames"/> and accumulates the ramped mix into the
    /// destination planar buffer. For each input channel <c>i</c> and output channel <c>j</c>:
    /// <c>start_ij = prev[i][j]·StartGain</c>, <c>delta_ij = (next[i][j]·EndGain − start_ij)/maxFrames</c>,
    /// and <c>dest[j][k] += input[i][k]·(start_ij + k·delta_ij)</c>. The ramp is linear over one bus frame
    /// and reaches the end at <c>k = maxFrames</c> (gapE 2.3, 2.4).
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
        for (int i = 0; i < _inputChannels; i++)
        {
            var source = input[i];
            if (source.Length < maxFrames)
                throw new ArgumentException($"input channel {i} is shorter than {maxFrames} frames", nameof(input));
            for (int k = validFrames; k < maxFrames; k++) source[k] = 0f;
        }

        // gapE 2.2: start_ij = prev[i][j]·startGain; delta_ij = (next[i][j]·endGain - start_ij)·s16.
        // gapE 2.4: s16 = bus+0x5C = 1/frames.
        float s16 = 1f / maxFrames;
        for (int i = 0; i < _inputChannels; i++)
        {
            var source = input[i];
            for (int j = 0; j < _outputChannels; j++)
            {
                int m = i * _outputChannels + j;
                float start = _prevMatrix[m] * StartGain;
                float delta = (_nextMatrix[m] * EndGain - start) * s16;
                // gapE 2.3: start = 0 and delta = 0 is skipped by the ramp kernel.
                if (start == 0f && delta == 0f) continue;

                var dst = destination[j];
                for (int k = 0; k < maxFrames; k++)
                    dst[k] += source[k] * (start + k * delta);
            }
        }
    }
}
