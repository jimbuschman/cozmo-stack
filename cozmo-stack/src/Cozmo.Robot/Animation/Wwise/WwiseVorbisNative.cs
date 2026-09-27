// fidelity: M6-002
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>A setup mode table entry: the block flag and the mapping index (M6-002 / inventory V3).</summary>
/// <param name="BlockFlag">Whether the mode uses the long block size (the <c>read(1)</c> blockflag).</param>
/// <param name="Mapping">The mapping index (<c>read(8)</c>); the caller checks it against the mapping count.</param>
public sealed record WwiseVorbisMode(bool BlockFlag, int Mapping);

/// <summary>
/// One residue configuration, in the runtime's field order (M6-002 / inventory gapG 6.7, 0x00AB6F54..0x00AB72xx):
/// <c>{type, stagemasks, stagebooks, begin, end, grouping, u8 partitions, u8 groupbook, u8 stages}</c>.
/// </summary>
/// <param name="Type">The 2-bit residue type; 1 (mono, shipped) or 2 (stereo, shipped).</param>
/// <param name="Begin">The first spectral line the residue covers.</param>
/// <param name="End">The last spectral line the residue covers.</param>
/// <param name="Grouping">The partition grouping, stored as <c>read(24) + 1</c>.</param>
/// <param name="Partitions">The partition count, stored as <c>read(6) + 1</c>.</param>
/// <param name="GroupBook">The book that decodes the class words.</param>
/// <param name="Cascades">One 8-bit cascade mask per partition; bit <c>s</c> means stage <c>s</c> is present.</param>
/// <param name="StageBooks">Per partition, the book id for each stage (0 where the cascade bit is clear).</param>
public sealed record WwiseVorbisResidueSetup(
    int Type, int Begin, int End, int Grouping, int Partitions, int GroupBook,
    byte[] Cascades, byte[][] StageBooks);

/// <summary>
/// The part of the Wwise-stripped setup header the frozen rows settle: the codebook count and its 10-bit
/// library indices, and the floor count that follows the (absent) time-domain section
/// (M6-002 / inventory V3, 0x00AB63E0..0x00AB6474).
/// </summary>
/// <param name="CodebookIds">The library indices, in setup order.</param>
/// <param name="FloorCount">The floor count, <c>read(6) + 1</c>.</param>
public sealed record WwiseVorbisSetupPrefix(IReadOnlyList<int> CodebookIds, int FloorCount);

/// <summary>
/// The runtime's Vorbis decoder (M6-002): an Audiokinetic fork of <b>Tremor lowmem</b>, whose codebook struct
/// matches that branch field for field except that <c>q_seq</c> is missing (gapG 6.6, 0xABA1A8) and whose
/// quantization is always <c>decode_map</c> type 1 (gapG 6.4/6.5).
///
/// This is a standalone, unwired implementation of the parts of the production path the frozen rows settle,
/// plus explicit refusals for the parts they do not. It is deliberately not a from-memory Tremor port: the
/// rows name the algorithm and give the addresses, bit widths and formulas, but they mark the remaining
/// arithmetic RECOVERABLE_GAP (unread), and the fidelity rules do not allow filling those with a plausible
/// implementation.
///
/// <b>Built from the rows</b> (each cited at the code):
/// <list type="bullet">
/// <item>setup: the block size <c>1&lt;&lt;pow</c> and the <c>bs0 &gt;= 64</c> / <c>bs0 &lt;= bs1</c> /
/// <c>bs1 &lt;= 8192</c> check (V2, 0x00AB6380..0x00AB63DC); the codebook count and the 10-bit ids selected
/// from the built-in library at 0x01058290 (V3, 0x00AB6474), byte-identical to ww2ogg's
/// <c>packed_codebooks_aoTuV_603.bin</c> (0.7, gapG 6.6); the residue setup fields (gapG 6.7); the mode entry
/// (V3) and the hard-coded 1-bit mode number (V6, 0x00AB37FC..0x00AB3934);</item>
/// <item>codebook quantization: the stock Tremor <c>_float32_unpack</c> integer arithmetic (gapG 6.2,
/// 0xABA468..0xABA4D4), the <c>q_bits</c>/<c>q_seq</c> post-processing where the sequence bit is read and
/// discarded (gapG 6.3, 0xABA4D8..0xABA520), and the <c>decode_map</c> type-1 dequantisation
/// <c>v = add + ((v·q_del) &gt;&gt; (point − q_delp))</c> with no sequence accumulation (gapG 6.5,
/// 0xAB9BB0..0xABA184);</item>
/// <item>output: the vorb-header start-skip and end-trim selection (gapG 6.12, 0x00AB3244 and
/// 0x00AB0B98..0x00AB0CA8) and the leading-skip consumption.</item>
/// </list>
///
/// <b>Refused, not guessed.</b> The rows read the addresses but not the arithmetic for: the floor1 body
/// (0x00AB88D8), the mapping body (0x00AB6788), the decode-table builder (0x00AB96EC), the residue inverse
/// (0x00AB73F8 and 0x00AB745C), the floor-table contents (0x01058BF0 has only [0] = 229/32768 and
/// [255] = 65536.0 in the rows), the floor1 inverse (0x00AB915C), the float NEON IMDCT (0x00AB4E34; its
/// libvorbis normalisation is the inventory's named RECOVERABLE_GAP at gapG 6.11), the libvorbis float
/// windows (0x01054490) and the overlap-add (0x00AB5A94). The end-trim consumption arithmetic is also
/// refused: gapG 6.12 states <c>end = max(current − trim, returned)</c> but not what <c>current</c> and
/// <c>returned</c> count, so applying it would be a guess. <see cref="Decode"/> throws, naming each piece.
///
/// <b>Deviations, inert on shipped media</b> (gapG 6.8..6.10): a type-0 residue and a type-2 residue with
/// any channel count other than two are refused, and the mode count must be 2 (the 1-bit mode read is exact
/// only then). <see cref="CheckResidueDeviation"/> and <see cref="CheckShippedModeCount"/> make that visible
/// rather than defaulting.
///
/// Not wired into <see cref="WwisePlayback"/>, <see cref="WwiseAudioSource"/>, <see cref="WwiseSongRenderer"/>
/// or the animation scheduler. See the M6-002 record's <c>unresolved</c>.
/// </summary>
public static class WwiseVorbisNative
{
    // ---- settled constants (M6-002) ----

    /// <summary>The mode number is read with a hard-coded 1 bit (V6, 0x00AB37FC..0x00AB3934).</summary>
    public const int ModeBits = 1;

    /// <summary>Every shipped Vorbis setup has exactly two modes (gapG 6.10).</summary>
    public const int ShippedModeCount = 2;

    /// <summary>The smaller legal block size (V2, 0x00AB6380..0x00AB63DC).</summary>
    public const int MinBlockSize = 64;

    /// <summary>The larger legal block size (V2).</summary>
    public const int MaxBlockSize = 8192;

    /// <summary>
    /// The native error returned by the block-size check when it fails: −0x85 (V2). Reported in the
    /// exception message; the managed API throws instead of returning a code.
    /// </summary>
    public const int BlockSizeError = -0x85;

    /// <summary>
    /// The residue and decode pieces the rows leave as RECOVERABLE_GAP, with their addresses. Named here so
    /// <see cref="Decode"/> refuses visibly and a caller or test can see exactly what is missing.
    /// </summary>
    public static readonly IReadOnlyList<string> UnreadArithmetic = new[]
    {
        "floor1 setup body 0x00AB88D8 (V3: floor type not read)",
        "mapping setup body 0x00AB6788 (V3: only the channel count is read)",
        "decode-table builder 0x00AB96EC (gapG 6.4: only the entry into it is read)",
        "residue inverse 0x00AB73F8 type 1 and 0x00AB745C type 2 (gapG 6.8/6.9: vector decode not read)",
        "floor table contents 0x01058BF0 (V7/gapG 6.11: only [0] and [255] are in the rows)",
        "floor1 inverse2 0x00AB915C (V7: render_line not read)",
        "float NEON IMDCT 0x00AB4E34 (V7/gapG 6.11: butterflies and normalisation not read)",
        "libvorbis float windows 0x01054490 (V7: table contents not in the rows)",
        "windowed overlap-add 0x00AB5A94 (V8: arithmetic not read)",
        "end-trim consumption (gapG 6.12: current/returned semantics not read)",
    };

    // ---- setup: block sizes (V2, 0x00AB6380..0x00AB63DC) ----

    /// <summary>
    /// The two block sizes, <c>1 &lt;&lt; hdr+0x7C</c> and <c>1 &lt;&lt; hdr+0x7D</c>, with the native range
    /// check (V2): it fails when <c>bs0 &lt; 64</c>, <c>bs0 &gt; bs1</c> or <c>bs1 &gt; 8192</c> (error −0x85,
    /// <see cref="BlockSizeError"/>).
    /// </summary>
    /// <param name="blockSize0Pow">hdr+0x7C, the short block's exponent.</param>
    /// <param name="blockSize1Pow">hdr+0x7D, the long block's exponent.</param>
    public static (int Short, int Long) BlockSizes(int blockSize0Pow, int blockSize1Pow)
    {
        int bs0 = 1 << blockSize0Pow;
        int bs1 = 1 << blockSize1Pow;
        if (bs0 < MinBlockSize || bs0 > bs1 || bs1 > MaxBlockSize)
            throw new InvalidDataException(
                $"Vorbis block sizes are invalid: bs0 = {bs0}, bs1 = {bs1}; the native check returns " +
                $"{BlockSizeError} (M6-002 V2)");
        return (bs0, bs1);
    }

    // ---- setup: codebooks, residues, modes (V3, gapG 6.7) ----

    /// <summary>
    /// Reads the settled prefix of the Wwise-stripped setup header (V3, 0x00AB63E0..0x00AB6474): the codebook
    /// count (<c>read(8)+1</c>), each 10-bit index selected from the built-in library (0x01058290), the absent
    /// time-domain section, and the floor count (<c>read(6)+1</c>) that follows it.
    ///
    /// The floor bodies themselves are not read by the rows, so this stops at their count. The residue and
    /// mode readers below are used directly where a caller already knows the offset.
    /// </summary>
    internal static WwiseVorbisSetupPrefix ReadSetupPrefix(BitReader reader, WwiseCodebookLibrary library)
    {
        int codebookCount = (int)reader.Read(8) + 1;                     // V3: read(8)+1
        var ids = new int[codebookCount];
        for (int i = 0; i < codebookCount; i++)
        {
            int id = (int)reader.Read(10);                               // V3: read(10) -> table 0x01058290
            if (id >= library.Count)
                throw new InvalidDataException(
                    $"Vorbis setup names codebook {id}, outside the built-in library of {library.Count} (M6-002 V3)");
            ids[i] = id;
        }

        // V3: no time-domain section; the floor count is the next read.
        int floorCount = (int)reader.Read(6) + 1;                        // V3: read(6)+1
        return new WwiseVorbisSetupPrefix(ids, floorCount);
    }

    /// <summary>
    /// One residue configuration in the runtime's field order (gapG 6.7, 0x00AB6F54..0x00AB72xx):
    /// <c>type read(2)</c>, <c>begin/end/grouping read(24)</c> (grouping + 1), <c>partitions read(6)+1</c>,
    /// <c>groupbook read(8)</c>, the cascade as <c>read(3) low</c> + <c>read(1) flag</c> +
    /// <c>read(5) high</c> per partition, then one <c>read(8)</c> book per present cascade stage.
    /// </summary>
    internal static WwiseVorbisResidueSetup ReadResidueSetup(BitReader reader)
    {
        int type = (int)reader.Read(2);
        int begin = (int)reader.Read(24);
        int end = (int)reader.Read(24);
        int grouping = (int)reader.Read(24) + 1;
        int partitions = (int)reader.Read(6) + 1;
        int groupBook = (int)reader.Read(8);

        var cascades = new byte[partitions];
        for (int j = 0; j < partitions; j++)
        {
            uint low = reader.Read(3);
            uint high = 0;
            if (reader.ReadBit()) high = reader.Read(5);
            cascades[j] = (byte)((high << 3) | low);
        }

        var stageBooks = new byte[partitions][];
        for (int j = 0; j < partitions; j++)
        {
            stageBooks[j] = new byte[8];
            for (int s = 0; s < 8; s++)
                if ((cascades[j] & (1 << s)) != 0)
                    stageBooks[j][s] = (byte)reader.Read(8);
        }

        return new WwiseVorbisResidueSetup(type, begin, end, grouping, partitions, groupBook, cascades, stageBooks);
    }

    /// <summary>One mode table entry (V3): <c>blockflag read(1)</c>, then <c>mapping read(8)</c>.</summary>
    internal static WwiseVorbisMode ReadMode(BitReader reader)
    {
        bool blockFlag = reader.ReadBit();
        int mapping = (int)reader.Read(8);
        return new WwiseVorbisMode(blockFlag, mapping);
    }

    /// <summary>
    /// The audio packet's mode number, read with the hard-coded 1 bit (V6, 0x00AB37FC..0x00AB3934:
    /// <c>mov r1,#1; bl 0xAB62E0</c>). There is no packet-type bit and no prev/next window bits.
    /// </summary>
    internal static int ReadModeNumber(BitReader reader) => (int)reader.Read(ModeBits);

    // ---- codebook quantization (gapG 6.2, 6.3, 6.5) ----

    /// <summary>
    /// The stock Tremor <c>_float32_unpack</c> integer arithmetic (gapG 6.2, 0xABA468..0xABA4D4): the sign
    /// bit, the 11-bit exponent, and the 21-bit mantissa shifted left to bit 30 with
    /// <c>point = exp − 788 − shifts</c>. A zero mantissa is <c>(0, −9999)</c>.
    /// </summary>
    public static (int Mantissa, int Point) Float32Unpack(uint value)
    {
        uint magnitude = value & 0x1FFFFF;
        if (magnitude == 0) return (0, -9999);                          // gapG 6.2
        int exponent = (int)((value & 0x7FE00000) >> 21);
        int shifts = 0;
        while ((magnitude & 0x40000000) == 0) { magnitude <<= 1; shifts++; }
        int mantissa = (int)magnitude;
        if ((value & 0x80000000) != 0) mantissa = -mantissa;            // sign by conditional negate
        return (mantissa, exponent - 788 - shifts);
    }

    /// <summary>
    /// The <c>q_bits</c>/<c>q_seq</c> post-processing (gapG 6.3, 0xABA4D8..0xABA520): <c>q_bits = read(4)+1</c>,
    /// the sequence bit is <b>read and discarded</b> (never stored), then <c>q_del &gt;&gt;= q_bits</c> and
    /// <c>q_delp += q_bits</c>.
    /// </summary>
    internal static int ReadQBits(BitReader reader) => (int)reader.Read(4) + 1;

    /// <summary>The discarded <c>q_seq</c> bit (gapG 6.3): read to advance the stream, then never stored.</summary>
    internal static bool ReadAndDiscardQSeq(BitReader reader) => reader.ReadBit();

    /// <summary>The <c>q_del &gt;&gt;= q_bits</c> / <c>q_delp += q_bits</c> step (gapG 6.3).</summary>
    public static (int QDel, int QDelp) ApplyQBits(int qDel, int qDelp, int qBits)
        => (qDel >> qBits, qDelp + qBits);

    /// <summary>
    /// The Tremor <c>decode_map</c> type-1 dequantisation, without sequence accumulation (gapG 6.5,
    /// 0xAB9BB0..0xABA184):
    /// <c>add = q_min shifted by (point − q_minp)</c>,
    /// <c>v = add + ((v·q_del) &gt;&gt; (point − q_delp))</c>, or <c>&lt;&lt;</c> when that shift is negative.
    /// The multiply is a plain 32-bit multiply, as the native's.
    /// </summary>
    /// <param name="value">The raw codeword value (<c>entry &amp; mask</c>).</param>
    /// <param name="qMin">The unpacked <c>q_min</c> (codebook +0x20/+0x24).</param>
    /// <param name="qDel">The unpacked and shifted <c>q_del</c> (codebook +0x28/+0x2C).</param>
    /// <param name="point">The unpacked point.</param>
    /// <param name="qMinp">The point with which <c>q_min</c> was stored.</param>
    /// <param name="qDelp">The point with which <c>q_del</c> was stored (after <c>+= q_bits</c>).</param>
    public static int Dequantize(int value, int qMin, int qDel, int point, int qMinp, int qDelp)
    {
        int addShift = point - qMinp;
        int add = addShift >= 0 ? qMin << addShift : qMin >> -addShift;
        int shift = point - qDelp;
        int product = unchecked(value * qDel);
        int scaled = shift >= 0 ? product >> shift : product << -shift;
        return add + scaled;
    }

    // ---- deviations, inert on shipped media (gapG 6.8..6.10) ----

    /// <summary>
    /// Refuses the residue paths the rows record as deviations rather than defaulting them (gapG 6.8..6.10):
    /// type 0 does not get <c>decodevs_add</c> in the native (it falls into the type-0/1 <c>decodev_add</c>
    /// branch, whose vector arithmetic is unread), and type 2 is hard-wired to two channels. Shipped mono is
    /// always type 1 and shipped stereo always type 2, so both deviations are inert on the shipped library.
    /// </summary>
    public static void CheckResidueDeviation(int type, int channels)
    {
        if (type == 0)
            throw new NotSupportedException(
                "Vorbis residue type 0 is the unread type-0 decodev_add branch 0x00AB770C (M6-002 gapG 6.8 deviation)");
        if (type == 2 && channels != 2)
            throw new NotSupportedException(
                $"Vorbis residue type 2 is hard-wired to 2 channels (0x00ABABB8, M6-002 gapG 6.9); got {channels}");
        if (type is not (1 or 2))
            throw new NotSupportedException($"Vorbis residue type {type} is not one the rows settle (M6-002 gapG 6.8/6.9)");
    }

    /// <summary>
    /// Refuses a mode count other than two: the native reads exactly one mode bit, which is exact only when
    /// <c>modeCount == 2</c> (M6-002 V6, gapG 6.10).
    /// </summary>
    public static void CheckShippedModeCount(int modeCount)
    {
        if (modeCount != ShippedModeCount)
            throw new NotSupportedException(
                $"The native reads a hard-coded 1-bit mode number (0x00AB37FC); mode count {modeCount} would need " +
                $"more bits (M6-002 V6)");
    }

    // ---- output: start-skip and end-trim (gapG 6.12) ----

    /// <summary>
    /// The start skip for the normal path (gapG 6.12): <c>0</c>, or the seek position
    /// <c>pbi+0x1B4</c> when the playing instance's <c>+0x1BD</c> bit 7 is set. The native field's width is
    /// not in the rows, so this takes the sample position as a host value (the same convention as
    /// <see cref="WwiseResamplerFormat"/>).
    /// </summary>
    public static int StartSkip(bool seeking, int seekPosition) => seeking ? seekPosition : 0;

    /// <summary>
    /// The end trim (gapG 6.12): <c>u16 fmt+0x32</c> when the loop count <c>src+0x38</c> is 1, else
    /// <c>u16 fmt+0x26</c>.
    /// </summary>
    public static int EndTrim(int loopCount, WwiseVorbisHeader header)
        => loopCount == 1 ? header.EndTrimSingle : header.EndTrimLoop;

    /// <summary>The loop-back start skip (gapG 6.12): <c>u16 fmt+0x24</c> (<c>src+0x9C</c>).</summary>
    public static int LoopStartSkip(WwiseVorbisHeader header) => header.LoopStartSkip;

    /// <summary>
    /// The leading-skip consumption the row states (gapG 6.12, 0xAB3884..0xAB3910): <c>skip</c> drops leading
    /// returned samples. Returns how many of <paramref name="returned"/> survive and advances
    /// <paramref name="skip"/> past what it consumed. The end-trim consumption is refused — see
    /// <see cref="UnreadArithmetic"/> — so it is not modelled here.
    /// </summary>
    public static int ApplyLeadingSkip(int returned, ref int skip)
    {
        if (returned < 0) throw new ArgumentOutOfRangeException(nameof(returned));
        if (skip <= 0) return returned;
        if (skip >= returned) { skip -= returned; return 0; }
        returned -= skip;
        skip = 0;
        return returned;
    }

    // ---- the decoder (refused: the rows do not settle the arithmetic) ----

    /// <summary>
    /// The native decode path, refused. The rows establish the arithmetic types, the tables' addresses and
    /// the output shape (planar float, skip/trim), but the floor1, residue-inverse, IMDCT, window,
    /// overlap-add and end-trim arithmetic are unread (see <see cref="UnreadArithmetic"/>). The fidelity
    /// rules do not allow a plausible substitute, so this throws rather than returning samples that are
    /// merely close. The method exists so the gap is visible at the production entry point.
    /// </summary>
    public static float[] Decode(WwiseMedia media, WwiseCodebookLibrary codebooks)
    {
        _ = media;
        _ = codebooks;
        throw new NotSupportedException(
            "The native Wwise Vorbis decoder is not implemented: the frozen rows do not settle " +
            string.Join("; ", UnreadArithmetic) + " (M6-002)");
    }
}