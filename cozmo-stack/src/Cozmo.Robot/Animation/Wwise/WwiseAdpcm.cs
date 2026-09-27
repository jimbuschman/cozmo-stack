namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The runtime's IMA ADPCM block decoder, 0x00A7A194, ported exactly (M6-003).
///
/// A 36-byte channel block is a signed 16-bit predictor at +0, an 8-bit step index at +2, an unused byte at
/// +3, then nibbles. The predictor is emitted as output sample 0 (0x00A7A208). Bytes +4..+0x22 give two
/// samples each, low nibble first (0x00A7A214..0x00A7A324), and byte +0x23 gives its low nibble only
/// (0x00A7A334..0x00A7A3B4). So a block is the header sample plus 63 nibbles, 64 samples.
///
/// Each nibble n moves the predictor by diff = ((2·(n&amp;7)+1)·step)&gt;&gt;3, subtracted when bit 3 is set
/// (0x00A7A220..0x00A7A250). The predictor is clamped to int16 (0x00A7A260..0x00A7A27C) and the index to
/// 0..88 (0x00A7A2A0..0x00A7A2B0). This is not the IMA shift sum: at step 7 with nibble 7 it gives 13, where
/// the shift sum gives 11.
///
/// The step table is the i16 table at 0x00FFD650 and the index adjustment at 0x00FFD708 (= +0xB8). Stereo is
/// [ch0 36 B][ch1 36 B], interleaved into int16, 64 frames per block (gapB D2).
///
/// The channel-to-half mapping, half c is output channel c, is from the three callers (0x00A72618,
/// 0x00A73EA0, 0x00A74100): each advances the input by one 36-byte half per channel and writes channel c at
/// output stride channels. The approved inventory still labels this mapping RECOVERABLE_GAP pending the
/// manager's record update.
/// </summary>
public static class WwiseAdpcm
{
    // fidelity: M6-003

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
        if (channels is not (1 or 2))
            throw new InvalidDataException($"ADPCM with {channels} channels is not decoded");
        if (blockAlign <= HeaderBytes * channels || blockAlign % channels != 0)
            throw new InvalidDataException($"ADPCM block of {blockAlign} bytes does not fit {channels} channels");

        var data = media.Data.Span;
        int perChannel = blockAlign / channels;                  // 36 in every file here
        int samplesPerBlock = (perChannel - HeaderBytes) * 2;     // 64
        int blocks = data.Length / blockAlign;
        var outBuf = new short[blocks * samplesPerBlock * channels];

        Span<int> predictor = stackalloc int[2];
        Span<int> index = stackalloc int[2];

        for (int b = 0; b < blocks; b++)
        {
            var block = data.Slice(b * blockAlign, blockAlign);

            // One sub-block per channel, each opening with its own predictor and step index. The output is
            // interleaved, so each channel writes every channels-th sample.
            for (int c = 0; c < channels; c++)
            {
                var half = block.Slice(c * perChannel, perChannel);
                predictor[c] = (short)(half[0] | (half[1] << 8));
                index[c] = Math.Clamp(half[2], 0, StepTable.Length - 1);

                int write = (b * samplesPerBlock * channels) + c;
                // The header predictor is output sample 0 (0x00A7A208).
                outBuf[write] = (short)predictor[c];
                write += channels;

                // Bytes +4..+0x22 give two samples each, low nibble first (0x00A7A214..0x00A7A324).
                for (int i = HeaderBytes; i < perChannel - 1; i++)
                {
                    byte v = half[i];
                    outBuf[write] = Step(v & 0x0F, ref predictor[c], ref index[c]);
                    write += channels;
                    outBuf[write] = Step(v >> 4, ref predictor[c], ref index[c]);
                    write += channels;
                }

                // The last byte gives its low nibble only; the high nibble is unused (0x00A7A334..0x00A7A3B4).
                outBuf[write] = Step(half[perChannel - 1] & 0x0F, ref predictor[c], ref index[c]);
            }
        }
        return outBuf;
    }

    /// <summary>
    /// One nibble, exactly as the runtime decodes it (0x00A7A220..0x00A7A2B0): the diff is
    /// ((2·(n&amp;7)+1)·step)&gt;&gt;3, subtracted when bit 3 is set, and the predictor saturates to int16
    /// while the step index clamps to 0..88.
    /// </summary>
    private static short Step(int nibble, ref int predictor, ref int index)
    {
        int step = StepTable[index];
        int diff = ((2 * (nibble & 7) + 1) * step) >> 3;
        predictor = (nibble & 8) != 0 ? predictor - diff : predictor + diff;
        predictor = Math.Clamp(predictor, short.MinValue, short.MaxValue);
        index = Math.Clamp(index + IndexTable[nibble], 0, StepTable.Length - 1);
        return (short)predictor;
    }
}
