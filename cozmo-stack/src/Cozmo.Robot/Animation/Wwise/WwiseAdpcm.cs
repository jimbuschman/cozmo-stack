namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// Decoder for the ADPCM Wwise files in this build, which are ordinary IMA ADPCM in fixed blocks.
///
/// That it is plain IMA was established by decoding rather than assumed. A 36-byte block at 44100 Hz with
/// the header's stated 24806 average bytes per second implies 689.05 blocks per second and so exactly 64
/// samples per block, which is what <c>(36 - 4) * 2</c> gives: a four-byte block header followed by 32
/// bytes of nibble pairs, with the header's sample used as the starting predictor and not emitted. Decoding
/// all 220 mono ADPCM files this way converges — peaks land just under full scale with no clipped samples at
/// all — which a wrong nibble order or step table would not do, because IMA diverges and pins to the rails
/// within a few dozen samples when its state is wrong.
///
/// Block layout: a signed 16-bit starting predictor, an unsigned 8-bit step index, one unused byte, then
/// the nibbles, low nibble of each byte first. Blocks are 36 bytes.
///
/// The seven stereo files use a 72-byte block and are **not** decoded; see <see cref="Decode"/>.
/// </summary>
public static class WwiseAdpcm
{
    // The IMA step table and index adjustment, unchanged from the IMA/DVI definition.
    private static readonly short[] StepTable =
    {
        7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45,
        50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307,
        337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552, 1707,
        1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845,
        8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767,
    };

    private static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };

    /// <summary>The bytes of a block header, per channel.</summary>
    private const int HeaderBytes = 4;

    /// <summary>
    /// Decodes a whole file to interleaved 16-bit samples. A trailing partial block is ignored rather than
    /// half-decoded; in this build every file divides exactly, so none is ever dropped.
    /// </summary>
    public static short[] Decode(WwiseMedia media)
    {
        if (media.Codec != WwiseCodec.Adpcm)
            throw new ArgumentException($"not an ADPCM file: format tag 0x{media.FormatTag:X4}", nameof(media));
        int channels = media.Channels, blockAlign = media.BlockAlign;
        // Stereo is refused rather than guessed at. Seven of the 227 ADPCM files are stereo, and neither
        // of the two candidate stereo block layouts decodes them: under both, the byte that should hold
        // each channel's starting step index is outside the table's 0..88 range, and the output pins to
        // full scale for about a tenth of its samples. Whatever Wwise does for stereo here, it is not
        // either arrangement of IMA, and producing plausible-sounding wrong audio would be worse than
        // producing none. All seven are 48 kHz music; the robot's speaker is mono.
        if (channels != 1)
            throw new InvalidDataException(
                $"ADPCM with {channels} channels is not decoded: the stereo block layout is not established");
        if (blockAlign < HeaderBytes * channels || blockAlign % channels != 0)
            throw new InvalidDataException($"ADPCM block of {blockAlign} bytes does not fit {channels} channels");

        var data = media.Data.Span;
        int perChannel = blockAlign / channels;                  // 36 in every file here
        int samplesPerBlock = (perChannel - HeaderBytes) * 2;     // 64
        int blocks = data.Length / blockAlign;
        var outBuf = new short[blocks * samplesPerBlock * channels];

        Span<int> predictor = stackalloc int[2];
        Span<int> index = stackalloc int[2];

        int write = 0;
        for (int b = 0; b < blocks; b++)
        {
            var block = data.Slice(b * blockAlign, blockAlign);

            // Each channel opens with its own predictor and step index.
            for (int c = 0; c < channels; c++)
            {
                int h = c * HeaderBytes;
                predictor[c] = (short)(block[h] | (block[h + 1] << 8));
                index[c] = Math.Clamp(block[h + 2], 0, StepTable.Length - 1);
            }

            int dataStart = HeaderBytes * channels;
            int bodyBytes = blockAlign - dataStart;

            for (int i = 0; i < bodyBytes; i++)
            {
                byte v = block[dataStart + i];
                outBuf[write++] = Step(v & 0x0F, ref predictor[0], ref index[0]);
                outBuf[write++] = Step(v >> 4, ref predictor[0], ref index[0]);
            }
        }
        return outBuf;
    }

    /// <summary>One IMA nibble: adjust the predictor by a fraction of the current step, then move the step.</summary>
    private static short Step(int nibble, ref int predictor, ref int index)
    {
        int step = StepTable[index];
        int diff = step >> 3;
        if ((nibble & 1) != 0) diff += step >> 2;
        if ((nibble & 2) != 0) diff += step >> 1;
        if ((nibble & 4) != 0) diff += step;
        predictor = (nibble & 8) != 0 ? predictor - diff : predictor + diff;
        predictor = Math.Clamp(predictor, short.MinValue, short.MaxValue);
        index = Math.Clamp(index + IndexTable[nibble], 0, StepTable.Length - 1);
        return (short)predictor;
    }
}
