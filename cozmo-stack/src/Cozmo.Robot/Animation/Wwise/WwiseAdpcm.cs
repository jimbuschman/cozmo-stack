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
        // Entries 89..255: the engine does not clamp the header's step index (0x00A7A1F4 ldrb [+2] -> r4; the first nibble's step is
        // ldrsh [table + r4*2] at 0x00A7A228), so a header index above 88 reads the i16 words that follow the 89-entry table at 0x00FFD650: three
        // zero words, then the index-adjustment table at 0x00FFD708 (= +0xB8, 16 words), then the rodata after it. The bytes below are those words
        // as they sit in libcozmoEngine.so (the window 0x00FFD650..0x00FFD84F). Only the first nibble can see an index above 88: the index it
        // produces is clamped to 0..88 (0x00A7A2A0..0x00A7A2B0).
        0, 0, 0, -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2,
        4, 6, 8, 8854, -16627, 0, 0, 0, 0, 10227, -16985, -28210, 15817, 3144, -16679, 0,
        0, 0, 0, 0, 0, 0, 0, 3144, 16089, -28209, -16951, 10227, 15783, 0, 0, 0,
        0, 8854, 16141, 0, 0, 28896, -16995, -16658, 16151, 0, 0, 0, 0, 0, 0, 17987,
        -16722, -9356, 15911, -9356, 15911, 17987, 16046, 0, 0, 0, 0, 0, 0, -16658, 16151, 28907,
        15773, 0, 0, 0, 0, -24572, -16616, -21052, -16997, 0, 0, 0, 0, 0, 0, -8929,
        -16870, 20307, -16722, 20307, 16046, -8929, -16870, 0, 0, 0, 0, 0, 0, -21041, 15771, -24572,
        -16616, 0, 0, 10536, -16591, 0, 0, 0, 0, -14103, -17206, -10864, 15609, 10578, -16881, 0,
        0, 0, 0, 0, 0, 0, 0, 10578, -16881, -10864, 15609, -14104, -17206, 0, 0, 0,
        0, 10536, -16591, 0, 0, -24270, -16968, -30123, 16179, 0, 0, 0, 0, 0, 0, 6679,
        15179, -31410, -18230, -31393, 14538, 6680, 15179,
    };

    /// <summary>The largest step index an update produces: <c>cmp r2, #0x58; movge r2, #0x58</c> (0x00A7A2A0..0x00A7A2AC).</summary>
    private const int MaxStepIndex = 88;

    private static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };

    /// <summary>The bytes of a block header, per channel.</summary>
    private const int HeaderBytes = 4;

    /// <summary>The bytes of one channel's block: the decoder consumes 36 (4 header + 32 nibble bytes, 0x00A7A1FC: header + 0x23 is the last byte).</summary>
    private const int PerChannelBytes = 36;

    /// <summary>Samples per channel per block: the header sample plus 63 nibbles.</summary>
    private const int SamplesPerBlock = 64;

    /// <summary>
    /// Decodes a whole file to interleaved 16-bit samples. A trailing partial block is ignored rather than
    /// half-decoded; in this build every file divides exactly, so none is ever dropped.
    /// </summary>
    public static short[] Decode(WwiseMedia media)
    {
        if (media.Codec != WwiseCodec.Adpcm)
            throw new ArgumentException($"not an ADPCM file: format tag 0x{media.FormatTag:X4}", nameof(media));
        // The engine gates only on the format tag (0x00A72704, 0x00A73B2C) and its decoder is generic in the channel count (the fifth argument is
        // the output interleave, 0x00A7A1B0/0x00A7A1C4), so every channel count is decoded: the caller (0x00A72618, 0x00A73EA0, 0x00A74100) runs
        // one pass per channel c over the input at c * 36 bytes (0x00A725FC: r4 * 9 * 4) with the file's block stride, writing output channel c.
        int channels = media.Channels, blockAlign = media.BlockAlign;
        // No channels: the caller's per-channel loop is skipped (0x00A725DC cmp fp, #0; beq 0x00A72664), so nothing is decoded. A block too small to hold the channels' 36-byte
        // sub-blocks would be read out of bounds by the engine; nothing is decoded here either. Neither throws: the voice sources call this from StartStream / Render.
        if (channels < 1 || blockAlign < PerChannelBytes * channels) return Array.Empty<short>();

        var data = media.Data.Span;
        int blocks = data.Length / blockAlign;
        var outBuf = new short[blocks * SamplesPerBlock * channels];

        for (int c = 0; c < channels; c++)
        {
            int predictor = 0, index = 0;
            for (int b = 0; b < blocks; b++)
            {
                // One sub-block per channel and block, each opening with its own predictor and step index; the output is interleaved, so each
                // channel writes every channels-th sample.
                var half = data.Slice(b * blockAlign + c * PerChannelBytes, PerChannelBytes);
                predictor = (short)(half[0] | (half[1] << 8));
                index = half[2];                                  // 0x00A7A1F4 ldrb [+2]: 0..255, NOT clamped (the engine does not clamp it)

                int write = (b * SamplesPerBlock * channels) + c;
                // The header predictor is output sample 0 (0x00A7A208).
                outBuf[write] = (short)predictor;
                write += channels;

                // Bytes +4..+0x22 give two samples each, low nibble first (0x00A7A214..0x00A7A324).
                for (int i = HeaderBytes; i < PerChannelBytes - 1; i++)
                {
                    byte v = half[i];
                    outBuf[write] = Step(v & 0x0F, ref predictor, ref index);
                    write += channels;
                    outBuf[write] = Step(v >> 4, ref predictor, ref index);
                    write += channels;
                }

                // The last byte gives its low nibble only; the high nibble is unused (0x00A7A334..0x00A7A3B4).
                outBuf[write] = Step(half[PerChannelBytes - 1] & 0x0F, ref predictor, ref index);
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
        int step = StepTable[index];      // the first nibble's index is the header byte, 0..255 (see the table window)
        // The product is divided by 8 rounding toward zero: the routine adds 7 to a negative product before the arithmetic shift
        // (0x00A7A240..0x00A7A24C, cmp #0; add #7; movlt; asr #3). A header index above 88 can name a negative step word, where this differs from >> 3.
        int product = (2 * (nibble & 7) + 1) * step;
        int diff = product < 0 ? (product + 7) >> 3 : product >> 3;
        predictor = (nibble & 8) != 0 ? predictor - diff : predictor + diff;
        predictor = Math.Clamp(predictor, short.MinValue, short.MaxValue);
        index = Math.Clamp(index + IndexTable[nibble], 0, MaxStepIndex);      // 0x00A7A2A0..0x00A7A2B0: min(.., 0x58) then max(.., 0)
        return (short)predictor;
    }
}
