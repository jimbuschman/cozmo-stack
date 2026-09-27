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
/// One floor1 class (M6-002 / correction C5 row 1b, 0x00AB8990): <c>dim = read(3) + 1</c>,
/// <c>subs = read(2)</c>, <c>book = subs != 0 ? read(8) : 0</c>, and <c>1 &lt;&lt; subs</c> sub-books
/// stored as <c>read(8) − 1</c> (so <c>−1</c> is the read(8)==0 sentinel).
/// </summary>
public sealed record WwiseVorbisFloorClass(int Dimensions, int Subclasses, int MasterBook, int[] SubBooks);

/// <summary>
/// The floor1 setup correction C5 settles (rows 1a..1e, 0x00AB88D8): partitions, the per-partition class,
/// the classes, the multiplier, rangebits and the postlist. The native entry layout is +0x00 class base,
/// +0x04 partitionclass, +0x08 postlist, +0x0C sorted index, +0x10 high neighbour, +0x14 low neighbour,
/// +0x18 partitions, +0x1C posts (count+2), +0x20 multiplier; <b>there is no floor-type field</b>.
/// </summary>
/// <param name="PostList">The <c>count+2</c> post list; <c>[0] = 0</c>, <c>[1] = 1 &lt;&lt; RangeBits</c>.</param>
public sealed record WwiseVorbisFloorSetup(
    int Partitions, int[] PartitionClasses, WwiseVorbisFloorClass[] Classes,
    int Multiplier, int RangeBits, int[] PostList);

/// <summary>
/// The mapping setup correction C5 settles (rows 2a..2d, 0x00AB6788): submaps, the per-channel mux, the
/// per-submap floor/residue, and the coupling steps. The native entry is +0x00 submaps, +0x04 mux,
/// +0x08 submap {floor,residue} pairs, +0x0C coupling steps, +0x10 mag/ang pairs.
/// </summary>
/// <param name="Mux">The per-channel submap index; empty when there is a single submap.</param>
public sealed record WwiseVorbisMappingSetup(
    int Submaps, int[] Mux, int[] SubmapFloor, int[] SubmapResidue,
    int CouplingSteps, (int Mag, int Ang)[] Coupling);

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
/// <b>Built from correction C5</b> (the arithmetic report <c>re-analysis/evidence/m6-vorbis/vorbis-arithmetic.md</c>,
/// each cited at the code): the floor1 setup fields (C5 1a..1e, 0x00AB88D8); the mapping setup fields (C5
/// 2a..2d, 0x00AB6788); the <c>decode_map</c> tree step and leaf packing (C5 3a/3b, 0x00AB9BB0); the residue
/// <c>decodev_add</c>/<c>decodevv_add</c> and the coupling inverse (C5 4d/4e/4f, 0x00ABAA6C / 0x00ABABB8 /
/// 0x00AB6E30); the 256-entry floor dB table (C5 5, 0x01058BF0); the declined-value test and
/// <c>render_line</c> (C5 6a/6b, 0x00AB915C); the window tables, combine and planar layout (C5 8a/9/9b,
/// 0x01054490 / 0x00AB5A94 / 0x00AB3520); and the start-skip/end-trim consumption (C5 10b, 0x00AB3884).
///
/// <b>Refused, not guessed.</b> Correction C5 still leaves five pieces as RECOVERABLE_GAP, so
/// <see cref="Decode"/> throws and names them: the decode-table builder (0x00AB96EC; its 16-bit store and
/// 32-bit read are unreconciled), the residue inverse stage/partition walk (0xAB7808..0xAB7E00; only its
/// control flow is read), the window-combine per-window branch (0x00AB5A94..0x00AB6038), the floor look
/// helper (0x00AB8018) and the window default (0x00AB3728). The float
/// NEON IMDCT (0x00AB4E34) is also refused: C5 7b contradicts the old normalisation claim, so the output
/// scale is not established. The end-trim consumption is implemented (C5 10b) but the meaning of its
/// <c>current</c>/<c>returned</c> inputs stays the row's.
/// <see cref="Decode"/> throws, naming each piece.
///
/// <b>Deviations, inert on shipped media</b> (gapG 6.8..6.10): a type-0 residue and a type-2 residue with
/// any channel count other than two are refused, and the mode count must be 2 (the 1-bit mode read is exact
/// only then). <see cref="CheckResidueDeviation"/> and <see cref="CheckShippedModeCount"/> make that visible
/// rather than defaulting.
///
/// Not wired into <see cref="WwisePlayback"/>, <see cref="WwiseAudioSource"/>, <see cref="WwiseSongRenderer"/>
/// or the animation scheduler. See the M6-002 record's <c>unresolved</c>.
/// </summary>
public static partial class WwiseVorbisNative
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
    /// The pieces correction C5 still leaves as RECOVERABLE_GAP, with their addresses. Named here so
    /// <see cref="Decode"/> refuses visibly and a caller or test can see exactly what is missing.
    /// </summary>
    public static readonly IReadOnlyList<string> UnreadArithmetic = new[]
    {
        "decode-table builder 0x00AB96EC (C5: the 16-bit store vs the 32-bit read is unreconciled)",
        "residue inverse stage/partition walk 0xAB7808..0xAB7E00 (C5 4b: only the control flow is read; the inner offsets are RECOVERABLE_GAP)",
        "window-combine per-window branch 0x00AB5A94..0x00AB6038 (C5 9: the exact branch selection is RECOVERABLE_GAP)",
        "floor look helper 0x00AB8018 (C5 1d: the sorted-index build is unread)",
        "float NEON IMDCT 0x00AB4E34 (C5 7b: its normalisation is not established; the 2^-24 constants belong to 0x00AB39D8)",
        "window default 0x00AB3728 (C5 8b: the consequence of a 0 window pointer is UNKNOWN)",
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

    // ---- floor1 setup (correction C5 rows 1a..1e, 0x00AB88D8) ----

    /// <summary>
    /// The floor1 setup body (C5 1a..1c, 0x00AB88D8..0x00AB8E14): <c>partitions = read(5)</c>; each
    /// <c>partitionclass = read(4)</c>; <c>maxclass+1</c> classes; <c>mult = read(2)+1</c>;
    /// <c>rangebits = read(4)</c>; <c>count = Σ class_dim[partitionclass[j]]</c>; then <c>count</c>
    /// <c>read(rangebits)</c> posts at <c>postlist[2..]</c> with <c>postlist[0] = 0</c> and
    /// <c>postlist[1] = 1 &lt;&lt; rangebits</c>. Each post is valid iff it is below <c>1 &lt;&lt; rangebits</c>
    /// (C5 1c, 0x00AB8C64), so an out-of-range post is refused.
    ///
    /// The low/high-neighbour scan (C5 1d) and the sorted-index helper (0x00AB8018) are not built here:
    /// the helper is a RECOVERABLE_GAP, so <see cref="Decode"/> still refuses the floor path.
    /// </summary>
    internal static WwiseVorbisFloorSetup ReadFloorSetup(BitReader reader, int codebookCount)
    {
        int partitions = (int)reader.Read(5);                              // C5 1a: read(5)
        var partitionClasses = new int[partitions];
        int maxClass = 0;
        for (int j = 0; j < partitions; j++)
        {
            partitionClasses[j] = (int)reader.Read(4);                     // C5 1a: read(4)
            if (partitionClasses[j] > maxClass) maxClass = partitionClasses[j];
        }

        var classes = new WwiseVorbisFloorClass[maxClass + 1];             // C5 1b: maxclass+1 classes
        for (int j = 0; j <= maxClass; j++)
        {
            int dim = (int)reader.Read(3) + 1;                             // C5 1b: dim = read(3)+1
            int subs = (int)reader.Read(2);                                // C5 1b: subs = read(2)
            int book = subs != 0 ? (int)reader.Read(8) : 0;                // C5 1b: book = subs? read(8) : 0
            if (book >= codebookCount)
                throw new InvalidDataException(
                    $"floor1 class {j} names masterbook {book}, outside {codebookCount} (C5 1b)");
            var subBooks = new int[1 << subs];                             // C5 1b: 1<<subs sub-books
            for (int k = 0; k < subBooks.Length; k++)
            {
                int sub = (int)reader.Read(8) - 1;                         // C5 1b: subbook = read(8)-1
                if (sub >= codebookCount)                                  // valid: < books or == 0xFF (read(8)==0)
                    throw new InvalidDataException(
                        $"floor1 class {j} names sub-book {sub}, outside {codebookCount} (C5 1b)");
                subBooks[k] = sub;
            }
            classes[j] = new WwiseVorbisFloorClass(dim, subs, book, subBooks);
        }

        int multiplier = (int)reader.Read(2) + 1;                          // C5 1c: mult = read(2)+1
        int rangeBits = (int)reader.Read(4);                               // C5 1c: rangebits = read(4)

        int count = 0;                                                     // C5 1c: count = Σ class_dim
        for (int j = 0; j < partitions; j++) count += classes[partitionClasses[j]].Dimensions;

        var postList = new int[count + 2];                                 // C5 1c: u16[count+2]
        for (int k = 0; k < count; k++)
        {
            int post = (int)reader.Read(rangeBits);                        // C5 1c: postlist[k+2] = read(rangebits)
            if (!FloorPostInRange(post, rangeBits))                        // C5 1c: valid iff < 1<<rangebits
                throw new InvalidDataException(
                    $"floor1 post {k} is {post}, not below {1 << rangeBits} (C5 1c, 0x00AB8C64)");
            postList[k + 2] = post;
        }
        postList[0] = 0;                                                   // C5 1c: postlist[0] = 0
        postList[1] = 1 << rangeBits;                                      // C5 1c: postlist[1] = 1<<rangebits

        return new WwiseVorbisFloorSetup(partitions, partitionClasses, classes, multiplier, rangeBits, postList);
    }

    /// <summary>
    /// The floor1 post range test (C5 1c, 0x00AB8C64 <c>cmp r2,r8</c> with <c>r8 = 1&lt;&lt;rangebits</c>):
    /// a post is valid iff it is below <c>1 &lt;&lt; rangebits</c>.
    /// </summary>
    public static bool FloorPostInRange(int post, int rangeBits) => post < (1 << rangeBits);

    // ---- mapping setup (correction C5 rows 2a..2d, 0x00AB6788) ----

    /// <summary>
    /// The mapping setup body (C5 2a..2c, 0x00AB6788): <c>submaps</c> (a flag, then <c>read(4)+1</c>);
    /// coupling steps <c>read(8)+1</c> with <c>mag</c>/<c>ang</c> at <c>ilog(channels−1)</c> bits; the
    /// reserved <c>read(2)</c> which must be 0; the mux <c>read(4)</c> per channel when <c>submaps &gt; 1</c>;
    /// then <b>three</b> <c>read(8)</c> per submap — the time submap (discarded), the floor and the residue.
    /// </summary>
    internal static WwiseVorbisMappingSetup ReadMappingSetup(
        BitReader reader, int channels, int floorCount, int residueCount)
    {
        bool hasSubmaps = reader.ReadBit();                                // C5 2a: submaps flag
        int submaps = hasSubmaps ? (int)reader.Read(4) + 1 : 1;            // C5 2a: read(4)+1

        bool hasCoupling = reader.ReadBit();                               // C5 2a: coupling flag
        var coupling = new List<(int Mag, int Ang)>();
        if (hasCoupling)
        {
            int steps = (int)reader.Read(8) + 1;                           // C5 2a: read(8)+1
            int bits = WwiseCodebookLibrary.ILog((uint)(channels - 1));    // C5 2a: ilog(channels-1)
            for (int j = 0; j < steps; j++)
            {
                int mag = (int)reader.Read(bits);                          // C5 2a: mag
                int ang = (int)reader.Read(bits);                          // C5 2a: ang
                if (mag == ang || mag >= channels || ang >= channels)
                    throw new InvalidDataException($"mapping coupling {j} is invalid (C5 2a)");
                coupling.Add((mag, ang));
            }
        }

        uint reserved = reader.Read(2);                                    // C5 2a: reserved read(2) == 0
        if (reserved != 0)
            throw new InvalidDataException("mapping reserved field is not zero (C5 2a)");

        int[] mux = Array.Empty<int>();
        if (submaps > 1)                                                   // C5 2b: mux per channel
        {
            mux = new int[channels];
            for (int j = 0; j < channels; j++)
            {
                mux[j] = (int)reader.Read(4);
                if (mux[j] >= submaps)
                    throw new InvalidDataException($"mapping mux {j} is out of range (C5 2b)");
            }
        }

        var floor = new int[submaps];                                      // C5 2c: three read(8) per submap
        var residue = new int[submaps];
        for (int i = 0; i < submaps; i++)
        {
            _ = reader.Read(8);                                            // C5 2c: time submap, discarded
            floor[i] = (int)reader.Read(8);
            if (floor[i] >= floorCount)
                throw new InvalidDataException($"mapping submap {i} names floor {floor[i]}, outside {floorCount} (C5 2c)");
            residue[i] = (int)reader.Read(8);
            if (residue[i] >= residueCount)
                throw new InvalidDataException($"mapping submap {i} names residue {residue[i]}, outside {residueCount} (C5 2c)");
        }

        return new WwiseVorbisMappingSetup(submaps, mux, floor, residue, coupling.Count, coupling.ToArray());
    }

    // ---- decode_map (correction C5 rows 3a/3b, 0x00AB9BB0) ----

    /// <summary>The tree-walk node step (C5 3a, 0x00AB9C34): <c>node = bit + 2·node</c>.</summary>
    public static int DecodeMapNode(int node, bool bit) => (bit ? 1 : 0) + 2 * node;

    /// <summary>
    /// The leaf unpacking (C5 3b, 0x00AB9C4C..0x00AB9CA0): <c>packed = entry &amp; 0x7FFFFFFF</c>, then
    /// <c>dim</c> values of <c>q_bits</c> bits, lowest first. The decode table the walk consumes is built by
    /// 0x00AB96EC, which stays RECOVERABLE_GAP, so only the leaf arithmetic is reproduced here.
    /// </summary>
    public static int[] DecodeMapLeaf(uint entry, int qBits, int dim)
    {
        uint packed = entry & 0x7FFFFFFFu;                                 // C5 3b: bit 31 is the internal-node marker
        uint mask = (1u << qBits) - 1u;                                    // C5 3b: mask = (1<<q_bits)-1
        var values = new int[dim];
        for (int i = 0; i < dim; i++)
        {
            values[i] = (int)(packed & mask);                              // C5 3b: low value first
            packed >>= qBits;                                              // C5 3b: packed >>= q_bits
        }
        return values;
    }

    // ---- residue inverse and coupling (correction C5 rows 4d..4f, 0x00ABAA6C / 0x00ABABB8 / 0x00AB6E30) ----

    /// <summary>The point the residue inverse passes to <see cref="Dequantize"/> (C5 4b..4e): −8.</summary>
    public const int ResiduePoint = -8;

    /// <summary>
    /// <c>decodev_add</c> (C5 4d, 0x00ABAA6C): <c>out[i+j] += tmp[j]</c> for the <c>dim</c> entries of one
    /// codeword, 32-bit with no saturation. <paramref name="index"/> is the codeword's position.
    /// </summary>
    public static void DecodevAdd(int[] output, int[] tmp, int index, int dim)
    {
        for (int j = 0; j < dim; j++) output[index + j] += tmp[j];         // C5 4d: out[i+j] += tmp[j]
    }

    /// <summary>
    /// <c>decodevv_add</c> (C5 4e, 0x00ABABB8): as <see cref="DecodevAdd"/> but the channel index toggles
    /// 0/1 between the <c>dim</c> values, so it is hard-wired to two channels.
    /// </summary>
    public static void DecodevvAdd(int[][] output, int[] tmp, int index, int dim)
    {
        int ch = 0;
        for (int j = 0; j < dim; j++)
        {
            output[ch][index + j] += tmp[j];                               // C5 4e: out[ch][i+offset] += tmp[j]
            ch ^= 1;                                                       // C5 4e: eor r5,#1
        }
    }

    /// <summary>
    /// The coupling inverse (C5 4f, 0x00AB6E30): integer add/sub on one mag/ang pair, with the right-hand
    /// side using the pair's old values (the native loads both, then stores).
    /// </summary>
    public static (int Mag, int Ang) InverseCoupling(int mag, int ang)
        => mag > 0
            ? (ang > 0 ? (mag, mag - ang) : (mag + ang, mag))
            : (ang > 0 ? (mag, mag + ang) : (mag - ang, mag));

    // ---- floor1 inverse2 / render_line (correction C5 rows 6a/6b, 0x00AB915C) ----

    /// <summary>
    /// The declined-value test (C5 6a, 0x00AB91CC..0x00AB91D4): a memory value is declined when bit 15 is
    /// set, i.e. <c>memo != (memo &amp; 0x7FFF)</c>. Declined posts are skipped and not rendered.
    /// </summary>
    public static bool FloorValueDeclined(int memo) => memo != (memo & 0x7FFF);

    /// <summary>
    /// <c>render_line</c> (C5 6b, 0x00AB9208..0x00AB92AC): the signed integer line from <c>(x0,y0)</c> to
    /// <c>(x1,y1)</c>, multiplying <paramref name="d"/> in place by <paramref name="table"/> at each integer
    /// <c>y</c>. All in <c>f32</c>; the native divides with a signed integer division (<c>0x4b3e70</c>) that
    /// truncates toward zero, which is C#'s <c>/</c>.
    /// </summary>
    public static void RenderLine(float[] d, int x0, int y0, int x1, int y1, float[] table)
    {
        int dy = y1 - y0;                                                  // C5 6b: dy
        int adx = x1 - x0;                                                 // C5 6b: adx = x - x_prev
        int baseValue = dy / adx;                                          // C5 6b: base = dy / adx
        int sy = dy < 0 ? baseValue - 1 : baseValue + 1;                   // C5 6b: sy
        int ady = Math.Abs(dy) - Math.Abs(baseValue * adx);                // C5 6b: ady = |dy| - |base·adx|
        int y = y0;
        int err = 0;
        d[x0] *= table[y];                                                 // C5 6b: out[x_prev] *= table[y_prev]
        for (int x = x0 + 1; x < x1; x++)
        {
            err += ady;                                                    // C5 6b: err += ady
            if (err >= adx) { err -= adx; y += sy; }                       // C5 6b: err >= adx -> err -= adx; y += sy
            else y += baseValue;                                           // C5 6b: else y += base
            d[x] *= table[y];                                              // C5 6b: out[x] *= table[y]
        }
    }

    // ---- window combine and planar output (correction C5 rows 8a/9/9b, 0x01054490 / 0x00AB5A94 / 0x00AB3520) ----

    /// <summary>
    /// The window table for a block size (C5 8a/8b, 0x00AB3564..0x00AB3738): the dispatch on
    /// <c>blocksize/2</c> in {128, 256, 512, 1024, 2048}. Any other block size takes the native default,
    /// which sets the window pointer to 0 — a RECOVERABLE_GAP whose consequence is UNKNOWN — so this
    /// refuses rather than defaulting.
    /// </summary>
    public static float[] WindowTable(int blockSize) => (blockSize / 2) switch
    {
        128 => VWin256,                                                    // C5 8a: 0x01054490
        256 => VWin512,                                                    // C5 8a: 0x01054690
        512 => VWin1024,                                                   // C5 8a: 0x01054A90
        1024 => VWin2048,                                                  // C5 8a: 0x01055290
        2048 => VWin4096,                                                  // C5 8a: 0x01056290
        _ => throw new NotSupportedException(
            $"the native window dispatch has no table for blocksize/2 = {blockSize / 2}; its default path " +
            "sets the window pointer to 0 and the consequence is UNKNOWN (C5 8b, 0x00AB3728)"),
    };

    /// <summary>The two-window combine (C5 9, 0x00AB5BA4..0x00AB5BB0): <c>out = a·wA + b·wB</c>.</summary>
    public static float CombineAdd(float a, float wA, float b, float wB) => a * wA + b * wB;

    /// <summary>The one-window combine (C5 9, 0x00AB5D64): <c>out = a·w − b·w2</c> (<c>vnmls.f32</c>).</summary>
    public static float CombineSub(float a, float w, float b, float w2) => a * w - b * w2;

    /// <summary>
    /// The planar-float layout (C5 9b, 0x00AB3520): <c>n/2</c> frames per channel, allocated as
    /// <c>n/2 · 4 · channels</c> bytes, and channel <c>c</c> at <c>base + c·maxFrames</c>.
    /// </summary>
    public static int PlanarFrames(int n) => n / 2;                        // C5 9b: n/2 frames

    public static int PlanarChannelOffset(int channel, int maxFrames) => channel * maxFrames;   // C5 9b: base + c·maxFrames

    // ---- end-trim consumption (correction C5 row 10b, 0x00AB3884..0x00AB3910) ----

    /// <summary>
    /// The end-trim consumption (C5 10b): when the end-of-file flag <c>vb+8</c> is set,
    /// <c>current = max(current − trim, returned)</c>; otherwise <c>current</c> is unchanged.
    /// <paramref name="trim"/> is the u16 <c>dsp+0x2E</c> stored by 0x00AB3244 (C5 10a).
    /// </summary>
    public static int ApplyEndTrim(int current, int returned, int trim, bool endOfFile)
        => endOfFile ? Math.Max(current - trim, returned) : current;       // C5 10b

    // ---- deviations, inert on shipped media (gapG 6.8..6.10) ----

    /// <summary>
    /// Refuses the residue paths the rows record as deviations rather than defaulting them (gapG 6.8..6.10):
    /// type 0 does not get <c>decodevs_add</c> in the native (it falls into the type-0/1 <c>decodev_add</c>
    /// branch 0x00AB770C, whose stage walk is still unread), and type 2 is hard-wired to two channels.
    /// Shipped mono is always type 1 and shipped stereo always type 2, so both deviations are inert on the
    /// shipped library.
    /// </summary>
    public static void CheckResidueDeviation(int type, int channels)
    {
        if (type == 0)
            throw new NotSupportedException(
                "Vorbis residue type 0 shares the type-0/1 branch 0x00AB770C, whose stage walk is unread (M6-002 gapG 6.8 deviation)");
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
    /// <paramref name="skip"/> past what it consumed. The end-trim consumption is
    /// <see cref="ApplyEndTrim"/> (correction C5 10b).
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
    /// The native decode path, refused. Correction C5 settles the setup, the leaf arithmetic, the tables and
    /// skip/trim, but the decode-table builder (0x00AB96EC), the residue stage walk (0xAB7808..0xAB7E00),
    /// the window-combine per-window branch (0x00AB5A94..0x00AB6038), the floor look helper (0x00AB8018), the
    /// IMDCT (0x00AB4E34) and the window default (0x00AB3728) are
    /// still unestablished (see <see cref="UnreadArithmetic"/>). The fidelity rules do not allow a plausible
    /// substitute, so this throws rather than returning samples that are merely close. The method exists so
    /// the gap is visible at the production entry point.
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