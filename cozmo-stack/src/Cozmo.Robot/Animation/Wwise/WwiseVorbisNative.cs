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
/// One built Vorbis decode table (C7 1a..1h, <c>_make_decode_table</c> 0x00AB96EC). <see cref="DecNodeb"/>
/// is the storage width (1, 2 or 4 bytes) and <see cref="DecLeafw"/> the leaf-word multiple (1 or 2). The
/// entries are the raw stored words widened to <see cref="uint"/>; only the low <c>DecNodeb·8</c> bits of
/// each are meaningful. The internal-node indexing differs by form (C7 6a): the 8/8, 16/16 and 32/32 forms
/// index <c>entry[2·node+bit]</c>, while the 8/16 and 16/32 forms index <c>entry[node+bit]</c> with
/// <c>node</c> a byte/halfword offset and a two-slot leaf payload.
/// </summary>
public sealed record WwiseVorbisDecodeTable(int DecNodeb, int DecLeafw, uint[] Entries);

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
/// <b>Built from correction C6</b> (the pass-2 report <c>re-analysis/evidence/m6-vorbis/vorbis-arithmetic-2.md</c>):
/// the corrected leaf polarity (C6 1e: internal nodes are <c>&gt;= 0</c>, leaves are bit31-set — C5 3a/3b
/// inverted); the residue stage/partition accessors and the class-word split (C6 3a..3e,
/// <see cref="ResidueSplitClassWord"/>); the window
/// combine mirror/negate forms (C6 5d/5e); the floor1 low/high-neighbour scan (C6 7a) and the floor-look
/// right-biased merge sort (C6 4a); and the MDCT's only output scale, 2^-24 at the <c>0x00AB39D8</c> tail
/// (C6 2b, correcting C5 7b's "no decode caller"). C6 also corrects C5 9b's allocation attribution:
/// <c>0x00AB3780</c> allocates and stores the per-channel pointers, <c>0x00AB3520</c> drives the overlap.
///
/// <b>Built from correction C7</b> (the pass-3 report
/// <c>re-analysis/evidence/m6-vorbis/vorbis-arithmetic-3.md</c>, each cited at the code): the codebook fields
/// are <c>dec_nodeb</c> (+0x14 in {1,2,4}), <c>dec_leafw</c> (+0x18 in {1,2}), <c>dec_type</c> (+0x1C) and
/// <c>q_val</c> (+0x38), with <c>_determine_node_bytes</c>/<c>_determine_leaf_words</c> inlined at
/// 0x00ABA314..0x00ABA440; the <c>_make_decode_table</c>/<c>_make_words</c>/<c>decpack</c> builder and its
/// five (nodeb, leafw) forms, correcting C6: the <c>dec_nodeb == 4</c> path writes a 32-bit table and is
/// unexercised by shipped books; floor1 inverse1 0x00AB8E60 (quant table, tristate, fit values, class
/// cascade, sub-books and the unwrap loop); the residue divisor array 0x00AB770C..0x00AB7808; and the
/// decoder dispatch on nodeb/leafw (C7 6a). See <see cref="MakeDecodeTable"/>, <see cref="MakeWords"/>,
/// <see cref="Decpack"/>, <see cref="Floor1Inverse1"/> and <see cref="ResidueDivisorArray"/>.
///
/// <b>Refused, not guessed.</b> The one piece the rows still do not settle stays refused, and
/// <see cref="Decode"/> throws naming it: the float NEON IMDCT kernel 0x00AB4E34 (C5 7a/C7 2d/2e/2f read the
/// stage order and the 13 trig table addresses, but the per-stage butterfly arithmetic and the trig-table
/// indexing are not in the rows). The window default 0x00AB3728 is
/// modelled as a fail-closed refusal whose reachability from a shipped header is UNKNOWN (C6 6b/6c). The
/// end-trim consumption is implemented (C5 10b) but the meaning of its <c>current</c>/<c>returned</c>
/// inputs stays the row's.
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
    /// The one piece the C5/C6/C7 rows still leave open, with its address. Named here so <see cref="Decode"/>
    /// refuses visibly and a caller or test can see exactly what is missing. Correction C7 settled the
    /// decode-table builder together with the codebook field names and <c>_determine_node_bytes</c> /
    /// <c>_determine_leaf_words</c>, the <c>dec_nodeb == 4</c> case, floor1 inverse1 and the residue divisor
    /// array, so they are no longer listed.
    /// </summary>
    public static readonly IReadOnlyList<string> UnreadArithmetic = new[]
    {
        "the float NEON IMDCT kernel 0x00AB4E34 (C7 2a..2h: the stage order presymmetry 0x00AB3D28 -> butterflies 0x00AB3FCC -> the step7/8 loop -> the tail 0x00AB39D8 is read, but the per-stage butterfly arithmetic and the 13 trig-table indices at 0x01004A40 via GOT 0x01040230 are RECOVERABLE_GAP)",
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

    // ---- decode_map and the decode table (C5 3a, C6 1c..1e, C7 1a..1k, 0x00AB96EC / 0x00AB9300 / 0x00AB9BB0) ----

    /// <summary>
    /// The codebook's decode-table node width, <c>codebook+0x14</c> (C7 1a/1k): 1, 2 or 4 bytes, chosen by
    /// <see cref="DetermineNodeBytes"/>. Correction C7 repurposes this from C6's "format selector".
    /// </summary>
    public const int DecNodebByte = 1;

    /// <summary>The halfword node width (C7 1a/1k).</summary>
    public const int DecNodebHalf = 2;

    /// <summary>The word node width (C7 1a/1k); the only 32-bit path, unexercised by shipped books (C7 5c).</summary>
    public const int DecNodebWord = 4;

    /// <summary>The codebook's leaf-word multiple, <c>codebook+0x18</c> (C7 1a/1k): 1 or 2.</summary>
    public const int DecLeafwOne = 1;

    /// <summary>The two-word leaf form (C7 1a/1k).</summary>
    public const int DecLeafwTwo = 2;

    /// <summary>The decode type <c>codebook+0x1C</c> for maptype 0 (C7 1a/1j/5d): the leaf payload is the index.</summary>
    public const int DecTypeIndex = 0;

    /// <summary>The decode type for maptype 1 (C7 1a/1j/5d): the leaf payload is the packed value.</summary>
    public const int DecTypeValue = 1;

    /// <summary>
    /// The inlined <c>_determine_node_bytes</c> (C7 1k, 0x00ABA35C..0x00ABA3C4 and the
    /// <c>0x00ABA65C</c>/<c>0x00ABA7DC</c> leafwidth-3 arms): 4 for <c>used &lt; 2</c>. A
    /// <c>leafwidth == 3</c> is substituted with 4 <b>locally</b> and the rest of the rule still runs
    /// (0x00ABA668 <c>mov ip,#0x10; mov r0,#4; b 0x00ABA378</c>), so for every reachable <c>used</c> the
    /// test passes up to <c>used = 10924</c> and gives <c>2</c>, not 4. Otherwise
    /// <c>leafwidth/2</c> (or 1 when <c>leafwidth == 1</c>) when <c>ilog(3·used−6)+1 &lt;= leafwidth·4</c>,
    /// else <c>leafwidth</c>.
    /// </summary>
    public static int DetermineNodeBytes(int used, int leafWidth)
    {
        if (used < 2) return DecNodebWord;                                  // C7 1k: used<2 -> 4
        if (leafWidth == 3) leafWidth = 4;                                  // C7 1k: leafwidth==3 substituted locally
        if (WwiseCodebookLibrary.ILog((uint)(3 * used - 6)) + 1 <= leafWidth * 4)
            return leafWidth == 1 ? 1 : leafWidth / 2;                      // C7 1k: leafwidth/2 (or 1)
        return leafWidth;                                                   // C7 1k: else leafwidth
    }

    /// <summary>
    /// The inlined <c>_determine_leaf_words</c> (C7 1k): 2 iff the leaf width exceeds the node width, else 1.
    /// </summary>
    public static int DetermineLeafWords(int nodeBytes, int leafWidth)
        => leafWidth > nodeBytes ? DecLeafwTwo : DecLeafwOne;               // C7 1k: leafwidth>nodeb -> 2

    /// <summary>The maptype-0 <c>leafwidth = ilog(entries)/8 + 1</c> (C7 1k, 0x00ABA33C..0x00ABA358).</summary>
    public static int MapType0LeafWidth(int entries) => WwiseCodebookLibrary.ILog((uint)entries) / 8 + 1;

    /// <summary>
    /// The maptype-1 <c>leafwidth = (q_bits·dim + 8)/8</c> (C7 1k, 0x00ABA6F0..0x00ABA6FC); the native shifts
    /// a non-negative value right by three.
    /// </summary>
    public static int MapType1LeafWidth(int qBits, int dim) => (qBits * dim + 8) / 8;

    /// <summary>The tree-walk node step (C5 3a, 0x00AB9C34): <c>node = bit + 2·node</c>.</summary>
    public static int DecodeMapNode(int node, bool bit) => (bit ? 1 : 0) + 2 * node;

    /// <summary>
    /// C6 1e / C7 6a: the 32-bit decode-table leaf marker, bit31 (<c>0x00AB9C4C bic ip,ip,#0x80000000</c>).
    /// The 8- and 16-bit tables mark leaves at bit7/bit15 instead; see <see cref="DecodeTableEntryIsLeaf"/>.
    /// </summary>
    public static bool DecodeMapIsLeaf(uint entry) => (entry & 0x80000000u) != 0;   // C6 1e: bic at the fall-through

    /// <summary>C6 1e: the complement of <see cref="DecodeMapIsLeaf"/>; the walk loops while this holds.</summary>
    public static bool DecodeMapIsInternal(uint entry) => (entry & 0x80000000u) == 0;

    /// <summary>
    /// The decode_map type-1 leaf unpacking (C5 3b / C6 1e, 0x00AB9C4C..0x00AB9CA0): <c>packed = entry &amp;
    /// 0x7FFFFFFF</c>, then <c>dim</c> values of <c>q_bits</c> bits, lowest first (mask
    /// <c>(1&lt;&lt;q_bits)−1</c>).
    /// </summary>
    public static int[] DecodeMapLeaf(uint entry, int qBits, int dim)
    {
        uint packed = entry & 0x7FFFFFFFu;                                 // C6 1e: bit31 is the leaf marker
        uint mask = (1u << qBits) - 1u;                                    // C6 1e: mask = (1<<q_bits)-1
        var values = new int[dim];
        for (int i = 0; i < dim; i++)
        {
            values[i] = (int)(packed & mask);                              // C6 1e: low value first
            packed >>= qBits;                                              // C6 1e: packed >>= q_bits
        }
        return values;
    }

    /// <summary>
    /// The native <c>decpack</c> leaf payload for dec_type 0 and 1 (C7 1j, 0x00AB9440..0x00AB96D8):
    /// <br>- dec_type 0 returns the entry index (0x00AB961C);
    /// <br>- dec_type 1, maptype 1 packs <c>dim</c> values of <c>q_bits</c> bits from the little-endian
    /// <c>q_val</c> u16 array (0x00AB9624..0x00AB96B4);
    /// <br>- dec_type 1, maptype != 1 reads <c>q_bits</c> from the stream (0x00AB95D0..0x00AB9618).
    /// dec_type 2 and 3 (0x00AB9530 / 0x00AB94F8) are unreachable in this engine (C7 5d: the packed lookup
    /// type is one bit, and maptype 1 only ever stores dec_type 1) and are refused.
    /// </summary>
    internal static uint Decpack(int decType, int mapType, long entry, long usedEntry, int dim, int qBits,
        ushort[]? qVal, int quantvals, BitReader reader)
    {
        switch (decType)
        {
            case DecTypeIndex:
                return (uint)entry;                                        // C7 1j: case 0 -> the entry index
            case DecTypeValue when mapType == 1:
            {
                uint ret = 0;
                long e = entry;
                for (int j = 0; j < dim; j++)
                {
                    int off = (int)(e % quantvals);                        // C7 1j: uidivmod
                    e /= quantvals;
                    ret |= (uint)qVal![off] << (qBits * j);                // C7 1j: q_val u16 << q_bits*j
                }
                return ret;
            }
            case DecTypeValue:
            {
                uint ret = 0;
                for (int j = 0; j < dim; j++)
                    ret |= reader.Read(qBits) << (qBits * j);              // C7 1j: read(q_bits) << q_bits*j
                return ret;
            }
            default:
                throw new NotSupportedException(
                    $"Vorbis decpack dec_type {decType} is unreachable in this engine (C7 5d: the packed " +
                    "lookup type is one bit, and maptype 1 only ever stores dec_type 1)");
        }
    }

    /// <summary>
    /// The native <c>_make_words</c> (C7 1i, 0x00AB9300..0x00AB96E8): the lowmem tree builder. It fills
    /// <paramref name="r"/> with 32-bit words: internal nodes store child node indices (non-negative) and
    /// leaves are the <c>decpack(...)|0x80000000</c> word. An overpopulated tree is refused, which is the
    /// native's <c>return −1</c>.
    /// </summary>
    internal static void MakeWords(int[] lengthList, int entries, uint[] r, int quantvals,
        int decType, int mapType, int dim, int qBits, ushort[]? qVal, BitReader reader)
    {
        if (entries < 2)
        {
            r[0] = 0x80000000u;                                            // C7 1i: n<2 -> r[0] = 0x80000000
            return;
        }

        var marker = new uint[33];                                         // C7 1i: marker[33] zeroed
        long count = 0, top = 0;
        for (int i = 0; i < entries; i++)
        {
            int length = lengthList[i];
            if (length == 0) continue;
            uint entry = marker[length];                                   // C7 1i: entry = marker[length]
            long chase = 0;
            if (count != 0 && entry == 0)
                throw new InvalidDataException(
                    "Vorbis codebook is an overpopulated tree (C7 1i: count && !entry returns −1)");

            // chase the tree as far as it is populated, appending new nodes from top (C7 1i chase/node append)
            int j;
            for (j = 0; j < length - 1; j++)
            {
                int bit = (int)((entry >> (length - j - 1)) & 1);          // C7 1i: MSB first
                if (chase >= top)
                {
                    top++;
                    r[chase * 2] = (uint)top;
                    r[chase * 2 + 1] = 0;
                }
                else if (r[chase * 2 + bit] == 0)
                {
                    r[chase * 2 + bit] = (uint)top;
                }
                chase = r[chase * 2 + bit];
            }

            {
                int bit = (int)((entry >> (length - j - 1)) & 1);
                if (chase >= top)
                {
                    top++;
                    r[chase * 2 + 1] = 0;
                }
                r[chase * 2 + bit] = Decpack(decType, mapType, i, count, dim, qBits, qVal, quantvals, reader)
                    | 0x80000000u;                                         // C7 1i: decpack(...)|0x80000000
            }
            count++;

            // the next shorter marker points to the node above (C7 1i marker bump)
            for (j = length; j > 0; j--)
            {
                if ((marker[j] & 1) != 0) { marker[j] = marker[j - 1] << 1; break; }
                marker[j]++;
            }
            // prune: the longer markers dangling from the just-taken node hang from the new one (C7 1i prune)
            for (j = length + 1; j < 33; j++)
            {
                if ((marker[j] >> 1) == entry) { entry = marker[j]; marker[j] = marker[j - 1] << 1; }
                else break;
            }
        }
    }

    /// <summary>
    /// The native <c>_make_decode_table</c> (C7 1b..1h, 0x00AB96EC..0x00AB9BB0): builds the decode table for
    /// one codebook and repacks the 32-bit <c>_make_words</c> work array. The five (nodeb, leafw) forms are
    /// (1,1) 8/8, (1,2) 8/16, (2,1) 16/16, (2,2) 16/32 and (4,*) 32/32. C6's "16-bit store vs 32-bit read"
    /// mismatch is void: the <c>dec_nodeb == 4</c> path writes the 32-bit table directly (C7 1c/5b) and is
    /// unexercised by shipped books (C7 5c).
    /// </summary>
    internal static WwiseVorbisDecodeTable MakeDecodeTable(
        int usedEntries, int leafWidth, int[] lengthList, int entries, int quantvals,
        int decType, int mapType, int dim, int qBits, ushort[]? qVal, BitReader reader)
    {
        int nodeb = DetermineNodeBytes(usedEntries, leafWidth);             // C7 1a/1k
        int leafw = DetermineLeafWords(nodeb, leafWidth);                   // C7 1a/1k

        if (nodeb == DecNodebWord)
        {
            // C7 1c/5b: (used*2+1) words; _make_words writes the table directly with no repack.
            var direct = new uint[usedEntries * 2 + 1];
            MakeWords(lengthList, entries, direct, quantvals, decType, mapType, dim, qBits, qVal, reader);
            return new WwiseVorbisDecodeTable(nodeb, leafw, direct);
        }

        var work = new uint[usedEntries * 2];                               // C7 1d: alloca(used*8)
        MakeWords(lengthList, entries, work, quantvals, decType, mapType, dim, qBits, qVal, reader);

        if (leafw == DecLeafwOne)
        {
            if (nodeb == DecNodebByte)
            {
                // C7 1e: 8/8; the entry is one byte, table[i] = ((work[i] & 0x80000000) >> 24) | work[i].
                var table = new uint[usedEntries * 2 - 2];
                for (int i = 0; i < table.Length; i++)
                    table[i] = (((work[i] & 0x80000000u) >> 24) | work[i]) & 0xFFu;
                return new WwiseVorbisDecodeTable(nodeb, leafw, table);
            }
            if (nodeb == DecNodebHalf)
            {
                // C7 1f: 16/16; the entry is one halfword, ((u16*)table)[i] = ((work[i] & 0x80000000) >> 16) | work[i].
                var table = new uint[usedEntries * 2 - 2];
                for (int i = 0; i < table.Length; i++)
                    table[i] = (((work[i] & 0x80000000u) >> 16) | work[i]) & 0xFFFFu;
                return new WwiseVorbisDecodeTable(nodeb, leafw, table);
            }
        }

        // C7 1g/1h: the two-pass repack that updates the node indexing; top starts at used*3−2.
        int top = usedEntries * 3 - 2;
        if (nodeb == DecNodebByte)
        {
            var table = new uint[usedEntries * 3 - 2];
            for (int i = usedEntries * 2 - 4; i >= 0; i -= 2)
            {
                if ((work[i] & 0x80000000u) != 0)
                {
                    if ((work[i + 1] & 0x80000000u) != 0)
                    {
                        top -= 4;
                        table[top] = ((work[i] >> 8) & 0x7Fu) | 0x80u;
                        table[top + 1] = ((work[i + 1] >> 8) & 0x7Fu) | 0x80u;
                        table[top + 2] = work[i] & 0xFFu;
                        table[top + 3] = work[i + 1] & 0xFFu;
                    }
                    else
                    {
                        top -= 3;
                        table[top] = ((work[i] >> 8) & 0x7Fu) | 0x80u;
                        table[top + 1] = work[work[i + 1] * 2];
                        table[top + 2] = work[i] & 0xFFu;
                    }
                }
                else if ((work[i + 1] & 0x80000000u) != 0)
                {
                    top -= 3;
                    table[top] = work[work[i] * 2];
                    table[top + 1] = ((work[i + 1] >> 8) & 0x7Fu) | 0x80u;
                    table[top + 2] = work[i + 1] & 0xFFu;
                }
                else
                {
                    top -= 2;
                    table[top] = work[work[i] * 2];
                    table[top + 1] = work[work[i + 1] * 2];
                }
                work[i] = (uint)top;
            }
            return new WwiseVorbisDecodeTable(nodeb, leafw, table);
        }
        if (nodeb == DecNodebHalf)
        {
            var table = new uint[usedEntries * 3 - 2];
            for (int i = usedEntries * 2 - 4; i >= 0; i -= 2)
            {
                if ((work[i] & 0x80000000u) != 0)
                {
                    if ((work[i + 1] & 0x80000000u) != 0)
                    {
                        top -= 4;
                        table[top] = ((work[i] >> 16) & 0x7FFFu) | 0x8000u;
                        table[top + 1] = ((work[i + 1] >> 16) & 0x7FFFu) | 0x8000u;
                        table[top + 2] = work[i] & 0xFFFFu;
                        table[top + 3] = work[i + 1] & 0xFFFFu;
                    }
                    else
                    {
                        top -= 3;
                        table[top] = ((work[i] >> 16) & 0x7FFFu) | 0x8000u;
                        table[top + 1] = work[work[i + 1] * 2];
                        table[top + 2] = work[i] & 0xFFFFu;
                    }
                }
                else if ((work[i + 1] & 0x80000000u) != 0)
                {
                    top -= 3;
                    table[top] = work[work[i] * 2];
                    table[top + 1] = ((work[i + 1] >> 16) & 0x7FFFu) | 0x8000u;
                    table[top + 2] = work[i + 1] & 0xFFFFu;
                }
                else
                {
                    top -= 2;
                    table[top] = work[work[i] * 2];
                    table[top + 1] = work[work[i + 1] * 2];
                }
                work[i] = (uint)top;
            }
            return new WwiseVorbisDecodeTable(nodeb, leafw, table);
        }
        throw new InvalidDataException($"Vorbis decode-table node width {nodeb} is not 1, 2 or 4 (C7 1a)");
    }

    /// <summary>
    /// The decode-table entry width in bits (C7 6a, 0x00AB9C00..0x00AB9C18): nodeb 1 → 8, 2 → 16, 4 → 32.
    /// This replaces C6's "format selector" reading of <c>codebook+0x14</c>.
    /// </summary>
    public static int DecodeTableEntryBits(int nodeb) => nodeb switch
    {
        DecNodebByte => 8,                                                  // C7 6a: ldrb
        DecNodebHalf => 16,                                                 // C7 6a: ldrh
        DecNodebWord => 32,                                                 // C7 6a: ldr
        _ => throw new NotSupportedException($"decode-table node width {nodeb} is not 1, 2 or 4 (C7 1a)"),
    };

    /// <summary>
    /// The per-(nodeb, leafw) leaf marker test (C7 6a, 0x00AB9BB0): the 8-bit leafw-1 form marks bit7, the
    /// 8-bit leafw-2 form also marks bit7, the 16-bit leafw-1 form marks bit15, the 16-bit leafw-2 form also
    /// marks bit15, and the 32-bit form marks bit31.
    /// </summary>
    public static bool DecodeTableEntryIsLeaf(int nodeb, uint entry) => nodeb switch
    {
        DecNodebByte => (entry & 0x80u) != 0,                               // C7 6a: 8/8 and 8/16
        DecNodebHalf => (entry & 0x8000u) != 0,                             // C7 6a: 16/16 and 16/32
        DecNodebWord => (entry & 0x80000000u) != 0,                         // C7 6a: 32/32
        _ => throw new NotSupportedException($"decode-table node width {nodeb} is not 1, 2 or 4 (C7 1a)"),
    };

    /// <summary>
    /// The native <c>decode_packed_entry_number</c> (C7 6a, 0x00AB9BB0..0x00AB9C4C): chase the decode table
    /// bit by bit, following the (nodeb, leafw) form, and return the leaf payload, or −1 when no leaf is
    /// reached within <paramref name="maxLength"/> bits. Internal nodes are non-negative for the 32-bit form;
    /// the 8- and 16-bit forms carry their marker at bit7/bit15.
    /// </summary>
    internal static int DecodeMapEntry(WwiseVorbisDecodeTable table, BitReader reader, int maxLength)
    {
        uint chase = 0;
        for (int i = 0; i < maxLength; i++)
        {
            int bit = reader.ReadBit() ? 1 : 0;
            switch (table.DecNodeb)
            {
                case DecNodebByte:
                    if (table.DecLeafw == DecLeafwOne)
                    {
                        chase = Entry(table, (int)(chase * 2) + bit);       // C7 6a: 8/8 t[chase*2+bit]
                        if ((chase & 0x80u) != 0) return (int)(chase & 0x7Fu);
                    }
                    else
                    {
                        uint next = Entry(table, (int)chase + bit);         // C7 6a: 8/16 t[chase+bit]
                        if ((next & 0x80u) != 0)
                        {
                            uint extra = (bit == 0 || (Entry(table, (int)chase) & 0x80u) != 0) ? 1u : 0u;
                            chase = (next << 8) | Entry(table, (int)(chase + (uint)bit + 1 + extra));
                            return (int)(chase & 0x7FFFu);                  // C7 6a: 15-bit payload
                        }
                        chase = next;
                    }
                    break;
                case DecNodebHalf:
                    if (table.DecLeafw == DecLeafwOne)
                    {
                        chase = Entry(table, (int)(chase * 2) + bit);       // C7 6a: 16/16
                        if ((chase & 0x8000u) != 0) return (int)(chase & 0x7FFFu);
                    }
                    else
                    {
                        uint next = Entry(table, (int)chase + bit);         // C7 6a: 16/32 t[chase+bit]
                        if ((next & 0x8000u) != 0)
                        {
                            uint extra = (bit == 0 || (Entry(table, (int)chase) & 0x8000u) != 0) ? 1u : 0u;
                            chase = (next << 16) | Entry(table, (int)(chase + (uint)bit + 1 + extra));
                            return (int)(chase & 0x7FFFFFFFu);              // C7 6a: 31-bit payload
                        }
                        chase = next;
                    }
                    break;
                default:
                    chase = Entry(table, (int)(chase * 2) + bit);           // C7 6a: 32/32
                    if ((chase & 0x80000000u) != 0) return (int)(chase & 0x7FFFFFFFu);
                    break;
            }
        }
        return -1;                                                         // C7 6a: no leaf within dec_maxlength
    }

    private static uint Entry(WwiseVorbisDecodeTable table, int index)
    {
        if (index < 0 || index >= table.Entries.Length)
            throw new InvalidDataException("decode_map walked off the decode table (C7 6a)");
        return table.Entries[index];
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

    // ---- residue stage/partition accessors (correction C6 rows 3a..3e, 0xAB7808..0xAB7E00) ----

    /// <summary>
    /// The residue type dispatch (C6 3a, 0x00AB7434): type 0 shares the type-0/1 branch at 0x00AB770C
    /// (<c>cmp r3,#1; ble</c>); type 2 goes to 0x00AB745C.
    /// </summary>
    public static bool ResidueSharesType01Path(int type) => type <= 1;      // C6 3a: cmp r3,#1; ble

    /// <summary>
    /// The cascade gate (C6 3d, 0x00AB7D48/0x00AB7D4C): the per-class cascade mask
    /// <c>info+4[class]</c> tested against <c>1&lt;&lt;stage</c>.
    /// </summary>
    public static bool ResidueStagePresent(byte cascadeMask, int stage)
        => (cascadeMask & (1 << stage)) != 0;                              // C6 3d: tst r2,(1<<s)

    /// <summary>
    /// The stage-book index (C6 3d, 0x00AB7D70): the book is <c>info+8[class·8 + stage]</c>, where
    /// <paramref name="classDigit"/> is the decoded class (<c>partword[channel][partition]</c>), not the
    /// partition number.
    /// </summary>
    public static int ResidueStageBookIndex(int classDigit, int stage) => classDigit * 8 + stage; // C6 3d

    /// <summary>
    /// The codeword base for a book (C6 3d, 0x00AB7D88/0x00AB7D94): <c>fullbooks + 60·book</c>, i.e.
    /// <c>15·book</c> then <c>&lt;&lt;2</c>.
    /// </summary>
    public static int ResidueBookOffset(int book) => 60 * book;            // C6 3d: rsb 15*book; lsl#2

    /// <summary>
    /// The residue class-word split (C6 3c, 0x00AB7C84/0x00AB7CBC/0x00AB7CC8/0x00AB7CD0/0x00AB7CD8): one
    /// class word is decoded per partition (<c>bl 0x00ABA840</c>) and split across the channels by repeated
    /// unsigned divide (<c>bl 0x4BE310</c> uidiv, <c>strb r0,[r6],#1</c>, <c>mls r7,sb,r3,r7</c>). For each
    /// channel except the last, <c>quotient = running / partword[ch]</c> is stored back into
    /// <paramref name="partword"/> and the remainder carries; the last channel takes the remainder.
    /// The divisor array is the per-channel base computed by the caller and is overwritten in place.
    /// </summary>
    public static void ResidueSplitClassWord(int classWord, byte[] partword)
    {
        if (partword is null) throw new ArgumentNullException(nameof(partword));
        if (partword.Length == 0) throw new ArgumentException("a class word needs at least one channel", nameof(partword));
        int running = classWord;
        for (int ch = 0; ch < partword.Length - 1; ch++)
        {
            int divisor = partword[ch];
            int quotient = divisor == 0 ? 0 : running / divisor;           // C6 3c: uidiv
            partword[ch] = (byte)quotient;                                 // C6 3c: strb r0,[r6],#1
            running -= divisor * quotient;                                 // C6 3c: mls = remainder
        }
        partword[^1] = (byte)running;                                      // C6 3c: last channel takes the remainder
    }

    // ---- IMDCT structure and output scale (correction C6 rows 2a/2b; C5 7a, 0x00AB4E34 / 0x00AB39D8) ----

    /// <summary>
    /// The decode MDCT's only output scale (C6 2b, 0x00AB39D8): the four final vectors are multiplied by
    /// <c>0x33800000</c> = 2^-24 before storing. C6 corrects C5 7b: <c>0x00AB4E34</c> tail-branches into
    /// <c>0x00AB39D8</c> (<c>0x00AB5A44 b 0x00AB39D8</c>), so this is the decode path.
    /// </summary>
    public const float ImdctOutputScale = 1f / 16777216f;                  // C6 2b: 0x33800000 = 2^-24

    /// <summary>
    /// The MDCT control-flow shift (C5 7a, 0x00AB4E78..0x00AB4E94): <c>shift = 13 − ilog2(n)</c>, where
    /// <c>n</c> is the transform size and <c>ilog2</c> is the floor of the base-2 logarithm.
    /// </summary>
    public static int ImdctShift(int n)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));
        return 13 - (WwiseCodebookLibrary.ILog((uint)n) - 1);             // C5 7a: rsb r3,r5,#0xd
    }

    /// <summary>The unit trig constant 0x3F3504F3 = 0.70710677 used by the NEON loop (C5 7a).</summary>
    public const float ImdctUnitTrig = 0.70710677f;

    /// <summary>
    /// Applies the decode MDCT's 2^-24 output scale in place (C6 2b, the four <c>vmul.f32</c>s at
    /// 0x00AB3CB4..0x00AB3CC4). This is the only settled arithmetic step of the tail; the butterfly kernel
    /// and its per-stage trig tables are not in the rows and are refused by <see cref="Decode"/>.
    /// </summary>
    public static void ApplyImdctOutputScale(float[] values)
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        for (int i = 0; i < values.Length; i++) values[i] *= ImdctOutputScale; // C6 2b
    }

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

    // ---- floor1 look arrays (correction C6 rows 4a and 7a, 0x00AB8018 / 0x00AB8D68) ----

    /// <summary>
    /// The floor1 low/high-neighbour scan (C6 7a, 0x00AB8D68..0x00AB8E14): for each post
    /// <c>i = 2..posts-1</c>, the low neighbour is the largest earlier value strictly below
    /// <c>postList[i]</c> and the high neighbour the smallest earlier value strictly above it, scanned over
    /// the earlier posts. Initial state <c>lo=0, hi=1, lx=0, hx=postList[1]</c>. The result is indexed
    /// <c>k = i−2</c>; low is stored at <c>floor+0x14</c> and high at <c>floor+0x10</c>.
    /// </summary>
    public static (byte[] Low, byte[] High) FloorNeighbours(int[] postList)
    {
        int posts = postList.Length;
        int count = posts - 2;
        var low = new byte[count];
        var high = new byte[count];
        for (int k = 0; k < count; k++)
        {
            int i = k + 2;
            int currentx = postList[i];
            int lo = 0, hi = 1;                                            // C6 7a: lo=0, hi=1
            int lx = 0, hx = postList[1];                                  // C6 7a: lx=0, hx=postlist[1]
            for (int j = 0; j < i; j++)
            {
                int x = postList[j];
                if (x < currentx && x > lx) { lo = j; lx = x; }            // C6 7a: keep the largest low
                if (x > currentx && x < hx) { hi = j; hx = x; }            // C6 7a: keep the smallest high
            }
            low[k] = (byte)lo;                                             // C6 7a: str low at floor+0x14
            high[k] = (byte)hi;                                            // C6 7a: str high at floor+0x10
        }
        return (low, high);
    }

    /// <summary>
    /// The floor look helper (C6 4a, 0x00AB8018): a <b>right-biased</b> bottom-up merge sort of the
    /// <c>floor+0x0C</c> byte index array, keyed by the <c>floor+0x08</c> u16 postlist. The caller seeds
    /// the array with the identity permutation <c>0..posts-1</c>, and the sort alternates the caller's
    /// buffer and a stack temp buffer between passes. Returns the index permutation sorted by
    /// <c>postList[index]</c> ascending, with the <b>right</b> element stored first on equal keys
    /// (<c>0x00AB80C8 cmp sl,sb</c>; <c>strblo</c> takes the left only when strictly less, <c>strbhs</c>
    /// takes the right on <c>&gt;=</c>).
    /// </summary>
    public static byte[] FloorSortedIndices(int[] postList)
    {
        int n = postList.Length;
        var src = new byte[n];
        for (int i = 0; i < n; i++) src[i] = (byte)i;                      // C6 4a: identity permutation
        var tmp = new byte[n];
        for (int width = 1; width < n; width *= 2)
        {
            for (int lo = 0; lo < n; lo += 2 * width)
            {
                int mid = Math.Min(lo + width, n);
                int hi = Math.Min(lo + 2 * width, n);
                int a = lo, b = mid, o = lo;
                while (a < mid && b < hi)
                {
                    // C6 4a: compare postlist[idx_a] vs postlist[idx_b]; the left is taken only when
                    // strictly less, so the right is stored first on equal keys (0x00AB80C8/0x00AB80CC/0x00AB80D8).
                    if (postList[src[a]] < postList[src[b]]) tmp[o++] = src[a++];
                    else tmp[o++] = src[b++];
                }
                while (a < mid) tmp[o++] = src[a++];
                while (b < hi) tmp[o++] = src[b++];
            }
            (src, tmp) = (tmp, src);                                       // C6 4a: buffer swap between passes
        }
        return src;
    }

    // ---- floor1 inverse1 (correction C7 rows 3a..3h, 0x00AB8E60) ----

    /// <summary>The floor1 quant table at 0x01016D20 (C7 3a): <c>{256, 128, 86, 64}</c>, indexed by mult−1.</summary>
    public static readonly int[] Floor1QuantLook = { 256, 128, 86, 64 };

    /// <summary>C7 3a: <c>quant_q = Floor1QuantLook[multiplier − 1]</c> (0x00AB8E80).</summary>
    public static int Floor1QuantQ(int multiplier) => Floor1QuantLook[multiplier - 1];

    /// <summary>
    /// The floor1 <c>render_point</c> (C7 3g, 0x00AB90a8..0x00AB90dc): mask the flag bits off both y, then
    /// <c>off = |dy|·(x−x0)/(x1−x0)</c> (signed, truncating toward zero, the native idiv) added to or
    /// subtracted from y0 by the sign of dy.
    /// </summary>
    public static int Floor1RenderPoint(int x0, int x1, int y0, int y1, int x)
    {
        y0 &= 0x7fff; y1 &= 0x7fff;
        int dy = y1 - y0;
        int err = Math.Abs(dy) * (x - x0);                                 // C7 3g: |dy|·(x−x0)
        int off = err / (x1 - x0);                                         // C7 3g: idiv truncates toward zero
        return dy < 0 ? y0 - off : y0 + off;                               // C7 3g
    }

    /// <summary>
    /// The floor1 inverse1 unwrap of one post (C7 3g, 0x00AB90e0..0x00AB9120): with
    /// <c>hiroom = quant_q − predicted</c>, <c>loroom = predicted</c> and <c>room = min(hiroom,loroom)·2</c>,
    /// returns the memo value. <c>val == 0</c> gives <c>predicted|0x8000</c>; <c>val &gt;= room</c> the
    /// <c>hiroom&gt;loroom ? val−loroom : hiroom−val−1</c> difference; otherwise the step-2 case halves the
    /// value, negating the odd branch.
    /// </summary>
    public static int Floor1UnwrapValue(int predicted, int quantQ, int val)
    {
        int hiroom = quantQ - predicted;                                   // C7 3g
        int loroom = predicted;
        int room = Math.Min(hiroom, loroom) << 1;                          // C7 3g: min<<1
        if (val == 0) return predicted | 0x8000;                           // C7 3g: predicted|0x8000
        if (val >= room)                                                   // C7 3g: val>=room
            return (hiroom > loroom ? val - loroom : hiroom - val - 1) + predicted;
        int v = (val & 1) != 0 ? -((val + 1) >> 1) : (val >> 1);           // C7 3g: the step-2 odd/even case
        return v + predicted;
    }

    /// <summary>
    /// The floor1 <c>inverse1</c> decode (C7 3a..3h, 0x00AB8E60..0x00AB9148): the tristate
    /// <c>read(1)==1</c> check (returns false where the native returns 0), the two
    /// <c>read(ilog(quant_q−1))</c> fit values, the per-partition class cascade and sub-books, and the unwrap
    /// loop. <paramref name="decodeBook"/> decodes a book to its entry number (the native
    /// <c>vorbis_book_decode</c>); <paramref name="neighbours"/> is <see cref="FloorNeighbours"/>.
    /// </summary>
    internal static bool Floor1Inverse1(BitReader reader, WwiseVorbisFloorSetup floor, int[] memo,
        (byte[] Low, byte[] High) neighbours, Func<int, int> decodeBook)
    {
        if (reader is null) throw new ArgumentNullException(nameof(reader));
        if (floor is null) throw new ArgumentNullException(nameof(floor));
        if (memo is null) throw new ArgumentNullException(nameof(memo));
        if (decodeBook is null) throw new ArgumentNullException(nameof(decodeBook));

        int quantQ = Floor1QuantQ(floor.Multiplier);                       // C7 3a
        if (reader.Read(1) != 1) return false;                             // C7 3b: tristate read(1) must be 1

        int bits = WwiseCodebookLibrary.ILog((uint)(quantQ - 1));          // C7 3c: ilog(quant_q-1)
        memo[0] = (int)reader.Read(bits);                                  // C7 3c: fit_value[0]
        memo[1] = (int)reader.Read(bits);                                  // C7 3c: fit_value[1]

        int j = 2;
        for (int p = 0; p < floor.Partitions; p++)                         // C7 3d: per partition
        {
            var cls = floor.Classes[floor.PartitionClasses[p]];
            int dim = cls.Dimensions;
            int subs = cls.Subclasses;
            int csub = 1 << subs;                                          // C7 3d: csub = 1<<subs
            int cval = subs == 0 ? 0 : decodeBook(cls.MasterBook);         // C7 3e: cascade word, else 0
            for (int k = 0; k < dim; k++)
            {
                int sub = cls.SubBooks[cval & (csub - 1)];                 // C7 3f: class_subbook[cval & (csub-1)]
                cval >>= subs;                                             // C7 3f: cval >>= class_subs
                memo[j++] = sub < 0 ? 0 : decodeBook(sub);                 // C7 3f: the read(8)==0 sentinel -> memo 0
            }
        }

        int posts = floor.PostList.Length;                                 // C7 3g: posts = count+2
        if (posts <= 2) return true;                                       // C7 3g: posts<=2 skips the unwrap
        for (int i = 2; i < posts; i++)
        {
            int ln = neighbours.Low[i - 2];                                // C7 3g: floor+0x14
            int hn = neighbours.High[i - 2];                               // C7 3g: floor+0x10
            int predicted = Floor1RenderPoint(floor.PostList[ln], floor.PostList[hn],
                memo[ln], memo[hn], floor.PostList[i]);                    // C7 3g: render_point
            memo[i] = Floor1UnwrapValue(predicted, quantQ, memo[i]);       // C7 3g: the unwrap
            memo[ln] &= 0x7fff;                                            // C7 3g: mask the low neighbour
            memo[hn] &= 0x7fff;                                            // C7 3g: mask the high neighbour
        }
        return true;
    }

    // ---- residue divisor array (correction C7 rows 4a..4g, 0x00AB770C..0x00AB7808) ----

    /// <summary>The residue samples per partition, <c>n / grouping</c> (C7 4b, 0x00AB777C).</summary>
    public static int ResidueSamplesPerPartition(int coveredSamples, int grouping) => coveredSamples / grouping;

    /// <summary>The residue <c>partitions_per_word = groupbook-&gt;dim</c> (C7 4b, 0x00AB7448).</summary>
    public static int ResiduePartitionsPerWord(int groupBookDim) => groupBookDim;

    /// <summary>The residue <c>partwords = ceil(spp / partitions_per_word)</c> (C7 4b, 0x00AB7798).</summary>
    public static int ResiduePartwords(int samplesPerPartition, int partitionsPerWord)
        => (samplesPerPartition + partitionsPerWord - 1) / partitionsPerWord;

    /// <summary>
    /// The per-group divisor sequence built at stage 0 (C7 4d, 0x00AB788C..0x00AB78CC):
    /// <c>[partitions^(dim−1), …, partitions, 1]</c>, each multiply truncated to a byte (the native's uxtb).
    /// The last divisor is 1.
    /// </summary>
    public static byte[] ResidueDivisorSequence(int partitions, int dim)
    {
        var seq = new byte[dim];
        seq[dim - 1] = 1;                                                  // C7 4d: the last divisor is 1
        for (int k = dim - 2; k >= 0; k--)
            seq[k] = (byte)(seq[k + 1] * partitions);                      // C7 4d: ×partitions, truncated to a byte
        return seq;
    }

    /// <summary>
    /// The per-channel base stride (C7 4c, 0x00AB77B8..0x00AB77EC):
    /// <c>partword[j] = base + j·dim·partwords</c>.
    /// </summary>
    public static int ResiduePartwordChannelOffset(int channel, int dim, int partwords)
        => channel * dim * partwords;

    /// <summary>
    /// The stage-0 divisor array (C7 4c/4d/4e): every group of every channel carries the
    /// <see cref="ResidueDivisorSequence"/>; the native builds channel 0 and byte-copies it to the rest, and
    /// the per-channel stride is <see cref="ResiduePartwordChannelOffset"/>.
    /// </summary>
    public static byte[][] ResidueDivisorArray(int channels, int partitions, int dim, int partwords)
    {
        if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));
        var seq = ResidueDivisorSequence(partitions, dim);
        var result = new byte[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            result[ch] = new byte[partwords * dim];
            for (int w = 0; w < partwords; w++)
                Array.Copy(seq, 0, result[ch], w * dim, dim);
        }
        return result;
    }

    // ---- window combine and planar output (correction C5 rows 8a/9/9b, 0x01054490 / 0x00AB5A94 / 0x00AB3520) ----

    /// <summary>
    /// Whether a shipped mode can select the 0 window pointer (C6 6c): the mechanism is EXACT_SOURCE, but
    /// whether any shipped header declares a blocksize of 64/128/8192 is UNKNOWN.
    /// </summary>
    public const bool WindowDefaultReachabilityKnown = false;              // C6 6c: UNKNOWN

    /// <summary>
    /// The window table for a block size (C5 8a/8b, 0x00AB3564..0x00AB3738): the dispatch on
    /// <c>blocksize/2</c> in {128, 256, 512, 1024, 2048}. Any other block size takes the native default,
    /// which stores a <b>0 window pointer</b> (<c>0x00AB3728</c>) that <c>0x00AB5A94</c> dereferences — a
    /// NULL dereference (C6 6b). Whether a shipped header can reach it is UNKNOWN (C6 6c), so this fails
    /// closed rather than dereferencing 0.
    /// </summary>
    public static float[] WindowTable(int blockSize) => (blockSize / 2) switch
    {
        128 => VWin256,                                                    // C5 8a: 0x01054490
        256 => VWin512,                                                    // C5 8a: 0x01054690
        512 => VWin1024,                                                   // C5 8a: 0x01054A90
        1024 => VWin2048,                                                  // C5 8a: 0x01055290
        2048 => VWin4096,                                                  // C5 8a: 0x01056290
        _ => throw new NotSupportedException(
            $"the native window dispatch has no table for blocksize/2 = {blockSize / 2}: its default path " +
            "stores a 0 window pointer (0x00AB3728) that 0x00AB5A94 dereferences — a NULL dereference. " +
            "Whether a shipped mode declares blocksize 64/128/8192 is UNKNOWN (C6 6b/6c), so this fails closed"),
    };

    /// <summary>The two-window combine (C5 9 / C6 5b, 0x00AB5BA4..0x00AB5BB0): <c>out = a·wA + b·wB</c>.</summary>
    public static float CombineAdd(float a, float wA, float b, float wB) => a * wA + b * wB;

    /// <summary>
    /// The second-window-only combine (C5 9 / C6 5c, 0x00AB5D64): <c>out = a·wA − b·wB</c> with the
    /// <c>vnmls.f32</c> order <c>a·wA − b·wB</c>.
    /// </summary>
    public static float CombineSub(float a, float wA, float b, float wB) => a * wA - b * wB;

    /// <summary>
    /// The 64-bit mirror (C6 5d, 0x00AB5B8C/0x00AB5B9C and 0x00AB5BB4/0x00AB5BB8): on a 128-bit vector,
    /// <c>vrev64.32</c> followed by <c>vswp</c> reverses the four f32 lanes. Applied to the incoming
    /// window before the multiply and to the result before the store.
    /// </summary>
    public static void Mirror4(float[] v)
    {
        if (v is null || v.Length != 4)
            throw new ArgumentException("the 64-bit mirror is a 4-lane vector (C6 5d)");
        (v[0], v[3]) = (v[3], v[0]);                                       // C6 5d: vrev64.32 + vswp = reverse
        (v[1], v[2]) = (v[2], v[1]);
    }

    /// <summary>
    /// The single-window negate form (C6 5e, 0x00AB5DDC/0x00AB6048): <c>vneg.f32</c>, then store.
    /// </summary>
    public static float[] NegateWindow(float[] w)
    {
        if (w is null) throw new ArgumentNullException(nameof(w));
        var r = new float[w.Length];
        for (int i = 0; i < w.Length; i++) r[i] = -w[i];                   // C6 5e: vneg.f32
        return r;
    }

    /// <summary>
    /// The planar-float frame count, <c>n/2</c> (C5 9b). Correction C6 attributes the allocation and the
    /// per-channel pointer stores to <c>0x00AB3780</c>, not <c>0x00AB3520</c>: <c>0x00AB3520</c> selects
    /// the windows and drives the overlap.
    /// </summary>
    public static int PlanarFrames(int n) => n / 2;                        // C5 9b: n/2 frames

    /// <summary>The per-channel allocation size in bytes, <c>n/2 · 4 · channels</c> (C5 9b; C6: 0x00AB3780).</summary>
    public static int PlanarAllocatedBytes(int n, int channels) => (n / 2) * 4 * channels; // C5 9b: 0x00AB3780

    /// <summary>Channel <c>c</c> at <c>base + c·maxFrames</c> (C5 9b; C6: 0x00AB3780 stores the pointers).</summary>
    public static int PlanarChannelOffset(int channel, int maxFrames) => channel * maxFrames; // C5 9b: base + c·maxFrames

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
    /// type 0 does not get <c>decodevs_add</c> in the native — it shares the type-0/1 <c>decodev_add</c>
    /// branch 0x00AB770C (C6 3a), whose stage/partition accessors and class-word split are now built, but
    /// which no shipped file exercises — and type 2 is hard-wired to two channels. Shipped mono is always
    /// type 1 and shipped stereo always type 2, so both deviations are inert on the shipped library.
    /// </summary>
    public static void CheckResidueDeviation(int type, int channels)
    {
        if (type == 0)
            throw new NotSupportedException(
                "Vorbis residue type 0 shares the type-0/1 branch 0x00AB770C (C6 3a); it is inert on the shipped library (M6-002 gapG 6.8 deviation)");
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
    /// The native decode path, refused. Correction C7 settles the decode-table builder and the codebook field
    /// names (0x00AB96EC / 0x00AB9300), the <c>dec_nodeb == 4</c> case, the floor1 inverse1 decode
    /// (0x00AB8E60) and the residue divisor array (0x00AB770C), on top of C5/C6's setup, decode-map walk,
    /// residue stage/partition accessors, window-combine branches, floor look arrays and IMDCT tail scale.
    /// The one piece still unestablished is the float NEON IMDCT kernel (0x00AB4E34, see
    /// <see cref="UnreadArithmetic"/>). The fidelity rules do not allow a plausible substitute, so this
    /// throws rather than returning samples that are merely close. The method exists so the gap is visible at
    /// the production entry point.</summary>
    public static float[] Decode(WwiseMedia media, WwiseCodebookLibrary codebooks)
    {
        _ = media;
        _ = codebooks;
        throw new NotSupportedException(
            "The native Wwise Vorbis decoder is not implemented: the frozen rows do not settle " +
            string.Join("; ", UnreadArithmetic) + " (M6-002)");
    }
}