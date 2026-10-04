// fidelity: M6-002
using System.Buffers.Binary;

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The runtime Vorbis codebook the decoder actually consumes (M6-002, correction C7 rows 1a..1k): the
/// Tremor-lowmem <c>codebook</c> struct field for field, read from the packed library form 0x00ABA188,
/// with <c>dec_nodeb</c>/<c>dec_leafw</c>/<c>dec_type</c>/<c>q_val</c> and a built decode table.
/// </summary>
public sealed class WwiseVorbisCodebook
{
    public int Dimensions { get; init; }
    public int Entries { get; init; }
    public int UsedEntries { get; init; }
    public int DecNodeb { get; init; }
    public int DecLeafw { get; init; }
    public int DecType { get; init; }
    public int DecMaxlength { get; init; }
    public int QMin { get; init; }
    public int QMinp { get; init; }
    public int QDel { get; init; }
    public int QDelp { get; init; }
    public int QBits { get; init; }
    public ushort[] QVal { get; init; } = Array.Empty<ushort>();
    public WwiseVorbisDecodeTable DecodeTable { get; init; } = new(1, 1, Array.Empty<uint>());
}

/// <summary>
/// The parsed Wwise-stripped setup header (M6-002, rows V3/P5-P8): the two block sizes, the codebooks, the
/// floors, the residues, the mappings and the modes, in the native order.
/// </summary>
public sealed class WwiseVorbisSetup
{
    public required int BlockSize0 { get; init; }
    public required int BlockSize1 { get; init; }
    public required WwiseVorbisCodebook[] Codebooks { get; init; }
    public required WwiseVorbisFloorSetup[] Floors { get; init; }
    public required WwiseVorbisResidueSetup[] Residues { get; init; }
    public required WwiseVorbisMappingSetup[] Mappings { get; init; }
    public required WwiseVorbisMode[] Modes { get; init; }

    /// <summary>The block size for a block flag (0 = short, 1 = long); setup+0 / setup+4.</summary>
    public int BlockSize(int flag) => flag == 0 ? BlockSize0 : BlockSize1;
}

public static partial class WwiseVorbisNative
{
    // ---- runtime codebook parse (0x00ABA188; correction C7 rows 1a..1k) ----

    /// <summary>
    /// Reads one packed codebook into the runtime struct (0x00ABA188). The packed form is the ww2ogg one
    /// (V4/gapG 6.1): <c>dim=read(4)</c>, <c>entries=read(14)</c>, an ordered flag (then 5-bit runs) or the
    /// 3-bit length width plus a sparse flag, a 1-bit lookup type, and for lookup 1 the
    /// <c>q_min</c>/<c>q_del</c> 32-bit floats, <c>q_bits=read(4)+1</c>, the discarded <c>q_seq</c> bit and
    /// the <c>quantvals</c> u16 <c>q_val</c> entries. <c>dec_type</c> is always 1 for lookup 1 and 0
    /// otherwise (gapG 6.4/C7 5d), and the decode table is built by <see cref="MakeDecodeTable"/>.
    /// </summary>
    internal static WwiseVorbisCodebook ParseCodebook(BitReader reader, WwiseCodebookLibrary library, int id)
    {
        int dimensions = (int)reader.Read(4);                              // V4: dim = read(4)
        int entries = (int)reader.Read(14);                                // V4: entries = read(14)

        var lengths = new int[entries];
        bool ordered = reader.ReadBit();                                   // V4: ordered read(1)
        if (ordered)
        {
            int current = 0;
            int currentLength = (int)reader.Read(5) + 1;                   // V4: 5-bit initial length (+1)
            while (current < entries)
            {
                int bits = WwiseCodebookLibrary.ILog((uint)(entries - current));
                int number = (int)reader.Read(bits);                       // V4: run length
                for (int i = 0; i < number && current < entries; i++) lengths[current++] = currentLength;
                currentLength++;
            }
        }
        else
        {
            int lengthBits = (int)reader.Read(3);                          // V4: codeword-length length
            bool sparse = reader.ReadBit();                                // V4: sparse read(1)
            for (int i = 0; i < entries; i++)
            {
                bool present = !sparse || reader.ReadBit();
                // 0x00ABA274/0x00ABA420: the stored length is read(lengthBits) + 1 (both paths).
                lengths[i] = present ? (int)reader.Read(lengthBits) + 1 : 0;
            }
        }

        int lookupType = (int)reader.Read(1);                              // gapG 6.1: 1-bit lookup type
        int decType;
        int qBits = 0;
        int qMin = 0, qMinp = 0, qDel = 0, qDelp = 0;
        ushort[] qVal = Array.Empty<ushort>();
        int quantvals = 0;
        int leafWidth;
        if (lookupType == 1)
        {
            var (min, minp) = Float32Unpack(reader.Read(32));              // gapG 6.2
            var (del, delp) = Float32Unpack(reader.Read(32));
            qBits = (int)reader.Read(4) + 1;                               // gapG 6.3: q_bits = read(4)+1
            _ = reader.ReadBit();                                          // gapG 6.3: q_seq read and discarded
            qDel = del >> qBits;                                           // gapG 6.3: q_del >>= q_bits
            qDelp = delp + qBits;                                          // gapG 6.3: q_delp += q_bits
            qMin = min; qMinp = minp;
            quantvals = (int)WwiseCodebookLibrary.QuantVals((uint)entries, (uint)dimensions); // gapG 6.4
            qVal = new ushort[quantvals];
            for (int i = 0; i < quantvals; i++) qVal[i] = (ushort)reader.Read(qBits);         // gapG 6.4
            decType = DecTypeValue;                                        // gapG 6.4: dec_type always 1
            leafWidth = MapType1LeafWidth(qBits, dimensions);              // C7 1k
        }
        else
        {
            decType = DecTypeIndex;                                        // gapG 6.1: entry numbers only
            leafWidth = MapType0LeafWidth(entries);                        // C7 1k
        }

        int used = 0;                                                      // C7 1k: used_entries
        int maxLength = 0;
        foreach (int len in lengths)
        {
            if (len != 0) used++;
            if (len > maxLength) maxLength = len;
        }

        var table = MakeDecodeTable(used, leafWidth, lengths, entries, quantvals, decType, lookupType,
            dimensions, qBits, qVal, reader);                              // C7 1b..1h

        return new WwiseVorbisCodebook
        {
            Dimensions = dimensions,
            Entries = entries,
            UsedEntries = used,
            DecNodeb = table.DecNodeb,
            DecLeafw = table.DecLeafw,
            DecType = decType,
            DecMaxlength = maxLength,
            QMin = qMin,
            QMinp = qMinp,
            QDel = qDel,
            QDelp = qDelp,
            QBits = qBits,
            QVal = qVal,
            DecodeTable = table,
        };
    }

    // ---- setup parse (0x00AB63E0; rows V3 / P5-P8) ----

    /// <summary>
    /// Parses the Wwise-stripped setup header in the native order (V3/P5-P8): codebook count and the 10-bit
    /// library ids, then floor count and bodies, residue count and bodies, mapping count and bodies, and mode
    /// count and entries. There is no time-domain section.
    /// </summary>
    internal static WwiseVorbisSetup ParseSetup(BitReader reader, WwiseCodebookLibrary library, int channels,
        int blockSize0Pow, int blockSize1Pow)
    {
        var (bs0, bs1) = BlockSizes(blockSize0Pow, blockSize1Pow);         // V2

        int codebookCount = (int)reader.Read(8) + 1;                       // P5
        var codebooks = new WwiseVorbisCodebook[codebookCount];
        for (int i = 0; i < codebookCount; i++)
        {
            int id = (int)reader.Read(10);                                 // P5: 10-bit library id
            if (id >= library.Count)
                throw new InvalidDataException($"Vorbis setup names codebook {id}, outside the library (V3)");
            codebooks[i] = ParseCodebook(library.OpenPacked(id), library, id);
        }

        int floorCount = (int)reader.Read(6) + 1;                          // P5
        var floors = new WwiseVorbisFloorSetup[floorCount];
        for (int i = 0; i < floorCount; i++) floors[i] = ReadFloorSetup(reader, codebookCount); // P6/P8

        int residueCount = (int)reader.Read(6) + 1;                        // P5
        var residues = new WwiseVorbisResidueSetup[residueCount];
        for (int i = 0; i < residueCount; i++) residues[i] = ReadResidueSetup(reader); // P8

        int mappingCount = (int)reader.Read(6) + 1;                        // P5
        var mappings = new WwiseVorbisMappingSetup[mappingCount];
        for (int i = 0; i < mappingCount; i++)
            mappings[i] = ReadMappingSetup(reader, channels, floorCount, residueCount); // P7

        int modeCount = (int)reader.Read(6) + 1;                           // P5
        var modes = new WwiseVorbisMode[modeCount];
        for (int i = 0; i < modeCount; i++) modes[i] = ReadMode(reader);   // P5

        CheckShippedModeCount(modeCount);                                  // gapG 6.10 deviation

        return new WwiseVorbisSetup
        {
            BlockSize0 = bs0, BlockSize1 = bs1,
            Codebooks = codebooks, Floors = floors, Residues = residues, Mappings = mappings, Modes = modes,
        };
    }

    // ---- decode_map leaf and book decode (0x00AB9BB0, 0x00ABA840) ----

    /// <summary>
    /// Decodes one codeword's leaf and dequantises its <c>dim</c> values (0x00AB9BB0 with the point −8):
    /// the tree walk <see cref="DecodeMapEntry"/>, the leaf unpack <see cref="DecodeMapLeaf"/>, and
    /// <see cref="Dequantize"/> per value. <paramref name="point"/> is the residue point (−8).
    /// </summary>
    internal static int[] DecodeMapValues(WwiseVorbisCodebook book, BitReader reader, int point)
    {
        int entry = DecodeMapEntry(book.DecodeTable, reader, book.DecMaxlength);
        if (entry < 0)
            throw new InvalidDataException("Vorbis decode_map did not reach a leaf (0x00AB9C48)");
        int[] raw = DecodeMapLeaf((uint)entry, book.QBits, book.Dimensions);
        for (int i = 0; i < raw.Length; i++)
            raw[i] = Dequantize(raw[i], book.QMin, book.QDel, point, book.QMinp, book.QDelp);
        return raw;
    }

    /// <summary>The scalar <c>vorbis_book_decode</c> (0x00ABA840) used for class words: the decode_map tree
    /// walk only, returning the raw leaf entry (no dequantisation — the native 0x00ABA840 does not dequantise;
    /// only the residue <c>decode_map</c> 0x00AB9BB0 does). The floor class and sub-books and the residue
    /// groupbook all use this raw form.</summary>
    internal static int BookDecode(WwiseVorbisCodebook book, BitReader reader, int point)
    {
        _ = point;
        int entry = DecodeMapEntry(book.DecodeTable, reader, book.DecMaxlength);
        if (entry < 0)
            throw new InvalidDataException("Vorbis book decode did not reach a leaf (0x00ABA8E0)");
        return entry;
    }

    // ---- decodev_add / decodevv_add (0x00ABAA6C / 0x00ABABB8) ----

    /// <summary>
    /// <c>decodev_add</c> (0x00ABAA6C): adds codewords of <c>dim</c> values into <paramref name="output"/>
    /// from <paramref name="offset"/> until <paramref name="n"/> positions are covered. The native writes all
    /// <c>dim</c> values of a codeword even when the last one runs past <paramref name="n"/>; the output
    /// buffer is the whole spectrum, so that is reproduced.
    /// </summary>
    internal static void DecodevAdd(WwiseVorbisCodebook book, int[] output, int offset, int n, BitReader reader)
    {
        int i = offset;
        int end = offset + n;
        while (i < end)
        {
            int[] tmp = DecodeMapValues(book, reader, ResiduePoint);
            for (int j = 0; j < book.Dimensions; j++) output[i++] += tmp[j];
        }
    }

    /// <summary>
    /// <c>decodevv_add</c> (0x00ABABB8): the type-2 stereo add. The channel toggles 0/1 and the offset
    /// advances by the old channel index after each value, so a codeword's <c>dim</c> values interleave the
    /// two channels and advance the position by <c>dim/2</c>.
    /// </summary>
    internal static void DecodevvAdd(WwiseVorbisCodebook book, int[][] channels, int offset, int n, BitReader reader)
    {
        int ch = 0;
        int end = offset + n;
        while (offset < end)
        {
            int[] tmp = DecodeMapValues(book, reader, ResiduePoint);
            for (int j = 0; j < book.Dimensions; j++)
            {
                channels[ch][offset] += tmp[j];
                offset += ch;
                ch ^= 1;
            }
        }
    }

    // ---- residue inverse (0x00AB73F8; C5 4a..4f, C6 3a..3e, C7 4a..4g) ----

    /// <summary>
    /// The residue inverse, types 0/1 (0x00AB770C) and 2 (0x00AB745C). <paramref name="channelData"/> are the
    /// per-submap channel buffers and <paramref name="nonzero"/> the per-channel floor flags; the residue
    /// adds the dequantised integers into <paramref name="channelData"/>. The type-2 path is hard-wired to
    /// two channels by <see cref="DecodevvAdd"/> (gapG 6.9).
    /// </summary>
    internal static void ResidueInverse(WwiseVorbisSetup setup, WwiseVorbisResidueSetup info,
        int[][] channelData, bool[] nonzero, BitReader reader, int pcmend)
    {
        int count = channelData.Length;
        if (count == 0) return;
        CheckResidueDeviation(info.Type, count);                           // gapG 6.8/6.9 deviations stay visible

        if (info.Type <= 1)                                                // C6 3a: type 0 shares type 1
        {
            int max = pcmend / 2;                                          // C5 4a: pcmend/2
            int n = Math.Min(max, info.End) - info.Begin;                  // C5 4a
            if (n <= 0) return;

            // C5 4a: compact the nonzero channels.
            var used = new List<int>();
            for (int ch = 0; ch < count; ch++) if (nonzero[ch]) used.Add(ch);
            if (used.Count == 0) return;

            int dim = setup.Codebooks[info.GroupBook].Dimensions;          // C7 4b: partitions_per_word
            int spp = n / info.Grouping;                                   // C7 4b: spp = n / grouping
            if (spp <= 0) return;
            int partwords = ResiduePartwords(spp, dim);                    // C7 4b

            // C7 4c: one byte array per used channel, stride dim*partwords.
            var divisors = new byte[used.Count][];
            for (int j = 0; j < used.Count; j++) divisors[j] = new byte[dim * partwords];

            var groupBook = setup.Codebooks[info.GroupBook];
            for (int stage = 0; stage < info.Stages; stage++)              // C6 3b/C7 4g
            {
                int mask = 1 << stage;
                int pw = 0;
                while (pw < spp)                                           // C6 3e
                {
                    if (stage == 0)                                        // C7 4d/4e/4f
                    {
                        BuildDivisorGroup(divisors[0], pw, dim, info.Partitions);
                        for (int j = 1; j < used.Count; j++)
                            Array.Copy(divisors[0], pw, divisors[j], pw, dim);
                        // 0x00AB7C7C..0x00AB7CE8: ONE class word per used channel, each split into that
                        // channel's own partword array. C6 3c / C7 4f's "one class word per partition" is
                        // wrong; the native's `bl 0xaba840` is inside the channel loop (0x00AB7CE8 `bne`).
                        for (int j = 0; j < used.Count; j++)
                        {
                            int classWord = BookDecode(groupBook, reader, ResiduePoint);
                            SplitClassWord(classWord, divisors[j], pw, dim);
                        }
                    }

                    int sb = pw;
                    int sbEnd = Math.Min(pw + dim, spp);
                    for (; sb < sbEnd; sb++)
                    {
                        for (int j = 0; j < used.Count; j++)
                        {
                            int classDigit = divisors[j][sb];              // C6 3d
                            byte cascade = info.Cascades[classDigit];
                            if (!ResidueStagePresent(cascade, stage)) continue;
                            int book = info.StageBooks[classDigit][stage]; // C6 3d
                            int offset = info.Begin + sb * info.Grouping;  // C6 3d: begin + pw*spp
                            DecodevAdd(setup.Codebooks[book], channelData[j], offset, info.Grouping, reader);
                        }
                    }
                    pw = sb;
                }
            }
            return;
        }

        // Type 2 (0x00AB745C; C5 4c, gapG 6.9): hard-wired to two channels.
        {
            int ch = count;
            int max = ch * pcmend / 2;                                     // C5 4c: pcmend*ch/2
            int n = Math.Min(max, info.End) - info.Begin;
            if (n <= 0) return;

            bool any = false;
            for (int c = 0; c < count; c++) if (nonzero[c]) { any = true; break; }
            if (!any) return;

            int dim = setup.Codebooks[info.GroupBook].Dimensions;
            int spp = n / info.Grouping;
            if (spp <= 0) return;
            int partwords = ResiduePartwords(spp, dim);
            int perChannel = info.Grouping / ch;                           // 0xAB7518: grouping / ch
            var divisors = new byte[dim * partwords];

            var groupBook = setup.Codebooks[info.GroupBook];
            for (int stage = 0; stage < info.Stages; stage++)
            {
                int mask = 1 << stage;
                int pw = 0;
                while (pw < spp)
                {
                    if (stage == 0)
                    {
                        BuildDivisorGroup(divisors, pw, dim, info.Partitions);
                        int classWord = BookDecode(groupBook, reader, ResiduePoint);
                        SplitClassWord(classWord, divisors, pw, dim);
                    }

                    int sb = pw;
                    int sbEnd = Math.Min(pw + dim, spp);
                    for (; sb < sbEnd; sb++)
                    {
                        int classDigit = divisors[sb];
                        byte cascade = info.Cascades[classDigit];
                        if (!ResidueStagePresent(cascade, stage)) continue;
                        int book = info.StageBooks[classDigit][stage];
                        int offset = sb * perChannel + info.Begin / ch;    // 0xAB7648/0xAB76b4
                        DecodevvAdd(setup.Codebooks[book], channelData, offset, perChannel, reader);
                    }
                    pw = sb;
                }
            }
        }
    }

    /// <summary>
    /// The per-group divisor sequence (C7 4d, 0x00AB7884..0x00AB78CC): the group at <paramref name="pw"/>
    /// holds <c>[partitions^(dim−1), …, partitions, 1]</c>, each multiply truncated to a byte.
    /// </summary>
    private static void BuildDivisorGroup(byte[] divisors, int pw, int dim, int partitions)
    {
        divisors[pw + dim - 1] = 1;                                        // C7 4d: the last divisor is 1
        for (int k = dim - 2; k >= 0; k--)
            divisors[pw + k] = (byte)(divisors[pw + k + 1] * partitions);  // C7 4d: uxtb
    }

    /// <summary>
    /// The class-word split (C7 4f, 0x00AB7C84..0x00AB7CE8): for each of the group's <c>dim</c> positions,
    /// <c>quotient = running / divisor</c> is stored and the remainder carries to the next position.
    /// </summary>
    private static void SplitClassWord(int classWord, byte[] divisors, int pw, int dim)
    {
        int running = classWord;
        for (int k = 0; k < dim; k++)
        {
            int divisor = divisors[pw + k];
            int quotient = divisor == 0 ? 0 : (int)((uint)running / (uint)divisor); // C7 4f: uidiv
            divisors[pw + k] = (byte)quotient;
            running -= divisor * quotient;                                 // C7 4f: mls remainder
        }
    }

    // ---- floor1 inverse2 / render (0x00AB915C; C5 6a/6b, C6 4a/4b) ----

    /// <summary>
    /// The floor1 <c>inverse2</c> driver (0x00AB915C): walk the sorted postlist from post 0, render each
    /// post's line with <see cref="RenderLine"/> and the floor dB table, writing the windowed floats into
    /// <paramref name="output"/>. Declined memo values (bit 15) are skipped (C5 6a).
    /// </summary>
    internal static void Floor1Inverse2(WwiseVorbisFloorSetup floor, int[]? memo, float[] output)
    {
        int posts = floor.PostList.Length;
        if (memo is null)
        {
            Array.Clear(output, 0, output.Length);                         // 0xAB92D8
            return;
        }

        int mult = floor.Multiplier;
        int prevPost = 0;
        int prevValue = memo[0] * mult;
        byte[] sorted = FloorSortedIndices(floor.PostList);                // C6 4a
        for (int i = 1; i < posts; i++)
        {
            int j = sorted[i];
            int raw = memo[j];
            if (FloorValueDeclined(raw)) continue;                         // C5 6a
            int x1 = floor.PostList[j];
            int y1 = raw * mult;
            if (x1 > prevPost) RenderLine(output, prevPost, prevValue, x1, y1, FloorTable); // C5 6b
            prevPost = x1;
            prevValue = y1;
        }
    }

    // ---- packet driver: entry 0x00AB3780, inverse 0x00AB6B14, framing 0x00AB7E40 ----

    /// <summary>
    /// The per-media decoder state (the native <c>dsp</c>), with the offsets C11/P9-P26 name: bit reader at
    /// +0x00, channels +0x0C, setup +0x10, per-channel buffers +0x14/+0x18, start +0x1C, end +0x20, previous
    /// block flag +0x24, current block flag +0x28, skip +0x2C, trim +0x2E, window-saved +0x30.
    ///
    /// <b>Buffer geometry (C13 Q3).</b> The native per-channel output buffer <c>dsp+0x14[c]</c> is
    /// <c>bs1/2</c> floats and the overlap <c>dsp+0x18[c]</c> is <c>block_size/4</c> floats. The built
    /// <see cref="ImdctBackward"/> is transliterated to read/write <c>n</c> floats (it derives <c>n/2</c>
    /// internally), so this driver over-allocates <see cref="Work"/> to <c>bs1</c> floats to agree with the
    /// built kernel; that is a memory-sizing note, not a sample difference. The native's own geometry is
    /// recorded here and in the manifest.
    /// </summary>
    internal sealed class DecoderState
    {
        public WwiseVorbisSetup Setup { get; set; } = null!;
        public int Channels { get; set; }
        public int Skip { get; set; }
        public int Trim { get; set; }
        /// <summary>dsp+0x14[c], the internal output buffer. Native size bs1/2 floats; over-allocated to bs1
        /// so the built IMDCT's n-float convention is satisfied (memory sizing only).</summary>
        public float[][] Work { get; set; } = Array.Empty<float[]>();
        /// <summary>dsp+0x18[c], the overlap buffer. Native size block_size/4 floats.</summary>
        public float[][] Overlap { get; set; } = Array.Empty<float[]>();
        public int Start { get; set; } = -1;
        public int End { get; set; }
        public int PreviousFlag { get; set; }
        public int CurrentFlag { get; set; }
        public int WindowSaved { get; set; }
        public bool LastFlag { get; set; }
        public int Consumed { get; set; }

        // The engine-visible allocation state of the decoder (the stream integration, 0xAB3264 / 0xAB3428 / 0xAB7E40; B-M6b-4 batch 5e).
        /// <summary><c>[D+0x14] != 0</c> (and <c>[D+0x18]</c>): the two pointer arrays are allocated (<c>0xAB3264</c> stores them, <c>0xAB3428</c> clears them).</summary>
        public bool ArraysAllocated { get; set; }
        /// <summary><c>[[D+0x18]] != 0</c>: the overlap block is allocated.</summary>
        public bool OverlapAllocated { get; set; }
        /// <summary><c>[[D+0x14]] != 0</c>: the work pointers are assigned (<c>0xAB3780</c> assigns them from the shared block at its start; <c>0xAB3264</c> zeroes the first one).</summary>
        public bool WorkAssigned { get; set; }

        /// <summary>
        /// The process-wide record whose work buffer holds this state's per-channel work slices (<c>work[ch] = [R+8] + ch * slice</c>, <c>0xAB37A8..0xAB37E0</c>); null for the offline decode, which keeps private
        /// <see cref="Work"/> arrays. When set, <see cref="Work"/> is a window onto the shared buffer: it is loaded from the shared buffer at every entry that reads it and stored back after the one that writes it.
        /// </summary>
        public WwiseVorbisSharedWork? SharedWork { get; set; }

        /// <summary>The shared buffer the work pointers were last assigned from (<c>0xAB3780</c> assigns them every packet; nothing else moves them): a pointer into a buffer the record has since replaced is a pointer into freed memory.</summary>
        public float[]? WorkBuffer { get; set; }
    }

    /// <summary>The per-channel slice of the shared work buffer in floats: <c>(((([setup+4] &gt;&gt; 1) &lt;&lt; 2) * channels + 0xF) &amp; ~0xF) / channels</c> bytes (<c>0xAB3794..0xAB37BC</c>, the divide <c>0x4B3E70</c>), divided by 4.</summary>
    internal static int WorkSliceFloats(DecoderState dsp)
    {
        int ch = dsp.Channels;
        int bytes = ((((dsp.Setup.BlockSize1 >> 1) << 2) * ch + 0xF) & ~0xF) / ch;
        return bytes / 4;
    }

    /// <summary>
    /// <c>work[ch]</c> from the shared buffer (the engine reads the shared memory in place; another decoder's packet may have written it since): the first slice floats of each channel's array come from
    /// <c>[R+8] + ch * slice</c>. A no-op for the offline decode.
    /// </summary>
    internal static void LoadSharedWork(DecoderState dsp, bool assignPointers = false)
    {
        var mem = dsp.SharedWork?.Mem;
        if (assignPointers) dsp.WorkBuffer = mem;                          // 0xAB37A8..0xAB37E0: the pointers are (re)assigned from [R+8]
        else if (dsp.SharedWork is not null && !ReferenceEquals(dsp.WorkBuffer, mem))
            throw new WwiseMissingBehaviourException(
                "MISSING: engine reads a freed shared work buffer (use-after-free) | 0xAB3978 / 0xAB3830 / 0xAB3520 through work pointers assigned before another decoder's 0xAB3264 reallocated the buffer (need > [R+4], 0xAB3324) | contents undefined");
        if (mem is null) return;
        int slice = WorkSliceFloats(dsp);
        for (int c = 0; c < dsp.Channels; c++)
        {
            int at = c * slice;
            int n = Math.Min(slice, Math.Min(Math.Max(mem.Length - at, 0), dsp.Work[c].Length));
            if (n > 0) Array.Copy(mem, at, dsp.Work[c], 0, n);
        }
    }

    /// <summary>
    /// The packet inverse wrote <c>work[ch]</c> in the shared buffer (<c>0xAB6B14</c>): only the first <paramref name="floats"/> (the current block's <c>n / 2</c>) floats of each channel's array go back to
    /// <c>[R+8] + ch * slice</c>; what lies beyond them is not the engine's to write (it stays whatever another decoder left there: the engine oracle's interleave scenarios).
    /// </summary>
    internal static void StoreSharedWork(DecoderState dsp, int floats)
    {
        var mem = dsp.SharedWork?.Mem;
        if (mem is null) return;
        int slice = WorkSliceFloats(dsp);
        for (int c = 0; c < dsp.Channels; c++)
        {
            int at = c * slice;
            int n = Math.Min(floats, Math.Min(Math.Max(mem.Length - at, 0), dsp.Work[c].Length));
            if (n > 0) Array.Copy(dsp.Work[c], 0, mem, at, n);
        }
    }

    /// <summary>Creates the decoder state for one media (P9: the stride always uses bs1).</summary>
    internal static DecoderState CreateState(WwiseVorbisSetup setup, int channels, int skip, int trim)
    {
        int workSize = setup.BlockSize1;                                   // over-allocated; see DecoderState
        // The native overlap is block_size/4 floats, but combine region B (Q1.4) reads up to n/2 floats
        // (the report marks that reachability UNKNOWN). Allocate bs1 so the settled region gates do not
        // fault; the extra tail stays zero, which is what a past-the-buffer read would see in practice.
        int overlapSize = setup.BlockSize1;
        var state = new DecoderState
        {
            Setup = setup, Channels = channels, Skip = skip, Trim = trim,
            Work = new float[channels][], Overlap = new float[channels][],
        };
        for (int c = 0; c < channels; c++)
        {
            state.Work[c] = new float[workSize];
            state.Overlap[c] = new float[overlapSize];
        }
        return state;
    }

    /// <summary>
    /// The packet entry (0x00AB3780; P9-P15): the bit-reader state, the hard-coded 1-bit mode number, the
    /// block flag/block size, the first-window copy, start-skip/end-trim, then the tail call to the inverse.
    /// </summary>
    internal static void PacketEntry(DecoderState dsp, ReadOnlyMemory<byte> body, bool endOfFile)
    {
        LoadSharedWork(dsp, assignPointers: true);                         // 0xAB37A8..0xAB37E0: work[ch] = [R+8] + ch * slice (the shared memory, as the other decoders left it)
        var reader = new BitReader(body);
        int modeNumber = (int)reader.Read(ModeBits);                       // P11: hard-coded 1 bit (V6)
        if (modeNumber >= dsp.Setup.Modes.Length)
            throw new InvalidDataException($"Vorbis mode {modeNumber} is outside the setup (V3)");
        var mode = dsp.Setup.Modes[modeNumber];

        dsp.PreviousFlag = dsp.CurrentFlag;                                // P12
        dsp.CurrentFlag = mode.BlockFlag ? 1 : 0;
        int oldBlockSize = dsp.Setup.BlockSize(dsp.PreviousFlag);
        int newBlockSize = dsp.Setup.BlockSize(dsp.CurrentFlag);

        if (dsp.WindowSaved == 0)                                          // P13: first-window copy
        {
            int aligned = (oldBlockSize + 3) & ~3;                         // bytes; == oldBlockSize for a power of two
            int off = aligned / 4, len = aligned / 4;
            for (int c = 0; c < dsp.Channels; c++)
            {
                // memcpy only (0x00AB3780): the rest of the overlap keeps its contents
                int copy = Math.Min(len, Math.Min(dsp.Work[c].Length - off, dsp.Overlap[c].Length));
                if (copy > 0) Array.Copy(dsp.Work[c], off, dsp.Overlap[c], 0, copy);
            }
            dsp.WindowSaved = 1;
        }

        if (ApplySkipAndTrim(dsp, newBlockSize, oldBlockSize, endOfFile)) return;   // P14; a dropped packet returns from 0xAB3780 itself (0xAB3958 / 0xAB3970 pop {..pc}): no inverse 0xAB6B14, the shared buffer untouched, [D+0x30] left as it is

        PacketInverse(dsp, dsp.Setup.Mappings[mode.Mapping], reader);      // P15 tail call
        StoreSharedWork(dsp, dsp.Setup.BlockSize(dsp.CurrentFlag) / 2);    // the inverse wrote n / 2 floats of each work[ch] in the shared buffer
        dsp.WindowSaved = 0;                                               // P23
    }

    /// <summary>The start-skip/end-trim state machine (P14, 0x00AB3878..0x00AB3970); true when the packet is dropped (the entry returns).</summary>
    private static bool ApplySkipAndTrim(DecoderState dsp, int newBlockSize, int oldBlockSize, bool endOfFile)
    {
        if (dsp.Start == -1)                                               // first packet
        {
            dsp.Start = 0;
            dsp.End = 0;
            if (dsp.Skip >= dsp.Setup.BlockSize1 / 2) return true;              // dropped (0xAB3958)
        }
        else
        {
            dsp.Start = 0;
            dsp.End = newBlockSize / 4 + oldBlockSize / 4;
            if (dsp.Skip != 0)
            {
                if (dsp.End >= dsp.Skip)
                {
                    dsp.Start = dsp.Skip;
                    dsp.Skip = 0;
                }
                else
                {
                    dsp.Start = dsp.End;
                    dsp.Skip -= dsp.End;
                    if (dsp.Skip >= dsp.Setup.BlockSize1 / 2) return true;      // dropped (0xAB3970)
                }
            }
        }

        if (endOfFile)                                                     // C5 10b
            dsp.End = ApplyEndTrim(dsp.End, dsp.Start, dsp.Trim, endOfFile: true);
        return false;
    }

    /// <summary>
    /// The per-packet inverse (0x00AB6B14; P16-P23): per channel floor1 inverse1, the coupling-channel
    /// marking, the per-submap residue inverse, the inline coupling inverse, per channel floor1 inverse2 and
    /// <c>mdct_backward(n, pcm[channel])</c>.
    /// </summary>
    internal static void PacketInverse(DecoderState dsp, WwiseVorbisMappingSetup mapping, BitReader reader)
    {
        int channels = dsp.Channels;
        var setup = dsp.Setup;
        int n = setup.BlockSize(dsp.CurrentFlag);                          // P16

        var memos = new int[channels][];                                   // D
        var floorOk = new bool[channels];                                  // C
        var residue = new int[channels][];                                 // A

        for (int ch = 0; ch < channels; ch++)                              // P17: floor1 inverse1
        {
            int submap = mapping.Submaps < 2 ? 0 : mapping.Mux[ch];
            var floor = setup.Floors[mapping.SubmapFloor[submap]];
            var memo = new int[floor.PostList.Length];
            var neighbours = FloorNeighbours(floor.PostList);
            bool ok = Floor1Inverse1(reader, floor, memo, neighbours,
                book => BookDecode(setup.Codebooks[book], reader, ResiduePoint));
            memos[ch] = memo;
            floorOk[ch] = ok;
            residue[ch] = new int[n];                                      // n/2 floats in the native; int here
        }

        for (int i = 0; i < mapping.CouplingSteps; i++)                    // P18: mark coupling channels
        {
            var (mag, ang) = mapping.Coupling[i];
            if (floorOk[mag] || floorOk[ang]) { floorOk[mag] = true; floorOk[ang] = true; }
        }

        for (int s = 0; s < mapping.Submaps; s++)                          // P19: per-submap residue
        {
            var ptrs = new List<int[]>();
            var flags = new List<bool>();
            for (int ch = 0; ch < channels; ch++)
            {
                if (mapping.Submaps >= 2 && mapping.Mux[ch] != s) continue;
                ptrs.Add(residue[ch]);
                flags.Add(floorOk[ch]);
            }
            if (ptrs.Count == 0) continue;
            ResidueInverse(setup, setup.Residues[mapping.SubmapResidue[s]], ptrs.ToArray(), flags.ToArray(),
                reader, n);
        }

        for (int i = mapping.CouplingSteps - 1; i >= 0; i--)               // P20: coupling inverse (integer)
        {
            var (mag, ang) = mapping.Coupling[i];
            int[] a = residue[mag];
            int[] b = residue[ang];
            int half = n / 2;
            for (int k = 0; k < half; k++)
            {
                var (m, a2) = InverseCoupling(a[k], b[k]);
                a[k] = m; b[k] = a2;
            }
        }

        for (int ch = 0; ch < channels; ch++)                              // P21: floor1 inverse2
        {
            int submap = mapping.Submaps < 2 ? 0 : mapping.Mux[ch];
            var floor = setup.Floors[mapping.SubmapFloor[submap]];
            // 0x00AB9180 `beq 0xab92d8`: when floor1 inverse1 returned 0 the memo is null and inverse2
            // clears the buffer (silence for that channel). Otherwise inverse2 reads the integer residue
            // from the work buffer and multiplies it by the floor table in place (0x00AB9224).
            int[]? memo = floorOk[ch] ? memos[ch] : null;
            Array.Clear(dsp.Work[ch], 0, dsp.Work[ch].Length);
            if (memo is not null)
            {
                int half = n / 2;
                for (int k = 0; k < half; k++) dsp.Work[ch][k] = residue[ch][k];
            }
            Floor1Inverse2(floor, memo, dsp.Work[ch]);
        }

        for (int ch = 0; ch < channels; ch++)                              // P22: mdct_backward(n, pcm[ch])
            ImdctBackward(dsp.Work[ch].AsSpan(0, n), n);               // the built kernel takes exactly n floats
    }

    // ---- stream reset 0x00AB3978 (C13 P27-corrected) ----

    /// <summary>
    /// The stream reset (0x00AB3978..0x00AB39D3; C13 P27-corrected): the same per-channel overlap save as the
    /// window driver, using <c>setup[dsp+0x28]</c> (the current block flag), then <c>dsp+0x30 = 1</c>. It
    /// never touches the work buffer or the caller's PCM. The framing reaches it on the loop-termination path
    /// when <c>dsp+0x14[0] != 0</c> (0x00AB7F04/0x00AB7F08), not on the eofflag.
    /// </summary>
    internal static void StreamReset(DecoderState dsp)
    {
        LoadSharedWork(dsp);                                               // 0xAB3984..0xAB39B8 read work[ch] in the shared buffer, whoever wrote it last
        int n = dsp.Setup.BlockSize(dsp.CurrentFlag);                      // R2: setup[dsp+0x28]
        int aligned = (n + 3) & ~3;                                        // R3
        int off = aligned / 4, len = aligned / 4;
        for (int ch = 0; ch < dsp.Channels; ch++)                          // R4-R7
        {
            // memcpy only: the rest of the overlap keeps its contents
            int copy = Math.Min(len, Math.Min(dsp.Work[ch].Length - off, dsp.Overlap[ch].Length));
            if (copy > 0) Array.Copy(dsp.Work[ch], off, dsp.Overlap[ch], 0, copy); // R6: memcpy
        }
        dsp.WindowSaved = 1;                                               // R8
    }

    // ---- window combine 0x00AB5A94 (C13 P24-window / Q1) ----

    /// <summary>
    /// The per-channel window combine, 0x00AB5A94: the float form of Tremor's <c>mdct_unroll_lap</c>. It is
    /// verified bit for bit against the engine's own code under emulation (re-analysis/tools/emu/emu_combine.py,
    /// WwiseVorbisCombineNativeTests). The regions run in this order, their pointers carrying over, and
    /// <c>start</c> and <c>end</c> are consumed as they go:
    /// <list type="bullet">
    /// <item>pre-lap, long to short only: <c>(n1&gt;&gt;2)-(n0&gt;&gt;2)</c> samples, copied from the overlap backwards;</item>
    /// <item>cross-lap A: <c>(*--l)*(*wL++) + (*--r)*(*--wR)</c>;</item>
    /// <item>cross-lap B: <c>(*r++)*(*--wR) - (*l++)*(*wL++)</c>;</item>
    /// <item>post-lap, short to long only: <c>-(*l++)</c>.</item>
    /// </list>
    /// <c>l</c> starts at <c>in + halfLap</c>, <c>r</c> at <c>right + (lW ? n1&gt;&gt;2 : n0&gt;&gt;2)</c>, <c>wL</c> at the
    /// window and <c>wR</c> at <c>window + (n&gt;&gt;1)</c>. The window and <c>halfLap</c> are the long ones only when
    /// both blocks are long. No FMA.
    /// </summary>
    internal static void Combine(int bs0, int bs1, bool previous, bool current,
        float[] input, float[] overlap, float[] w0, float[] w1, Span<float> output, int skip, int end)
    {
        bool both = previous && current;                                   // 0x00AB5AB0: ands sb, r4, lr
        int halfLap = both ? bs1 >> 2 : bs0 >> 2;
        int preLap = previous && !current ? (bs1 >> 2) - (bs0 >> 2) : 0;
        int postLap = !previous && current ? (bs1 >> 2) - (bs0 >> 2) : 0;
        float[] w = both ? w1 : w0;
        int wL = 0;                                                        // the window
        int wR = both ? bs1 >> 1 : bs0 >> 1;                               // window + (n >> 1)
        int l = halfLap;                                                   // in + halfLap
        int r = previous ? bs1 >> 2 : bs0 >> 2;                            // right + (lW ? n1>>2 : n0>>2)
        int start = skip;
        int o = 0;

        if (preLap != 0)                                                   // long to short: a straight copy
        {
            int n = Math.Min(end, preLap), off = Math.Min(start, preLap);
            int post = r - n;
            r -= off; start -= off; end -= n;
            while (r > post) output[o++] = overlap[--r];
        }

        {                                                                  // cross-lap A
            int n = Math.Min(end, halfLap), off = Math.Min(start, halfLap);
            int post = r - n;
            r -= off; l -= off; start -= off; wR -= off; wL += off; end -= n;
            while (r > post)
            {
                --r; --l; --wR;
                output[o++] = input[l] * w[wL] + overlap[r] * w[wR];
                wL++;
            }
        }

        {                                                                  // cross-lap B
            int n = Math.Min(end, halfLap), off = Math.Min(start, halfLap);
            int post = r + n;
            r += off; l += off; start -= off; wR -= off; wL += off; end -= n;
            while (r < post)
            {
                --wR;
                output[o++] = overlap[r] * w[wR] - input[l] * w[wL];
                r++; l++; wL++;
            }
        }

        if (postLap != 0)                                                  // short to long: a negated copy
        {
            int n = Math.Min(end, postLap), off = Math.Min(start, postLap);
            int post = l + n;
            l += off;
            while (l < post) output[o++] = -input[l++];
        }
    }

    // ---- framing 0x00AB7E40 (P1/P2) and the window/overlap driver 0x00AB3520 (P24-P26) ----

    /// <summary>
    /// The packet framing (0x00AB7E40; P1/P2/C13): each audio packet is a u16 size then its body. It loops
    /// until the data is exhausted; when a packet yields samples (<c>dsp.End - dsp.Start != 0</c>) it drives
    /// the window/overlap driver and appends the planar block. On the loop-termination path it calls the
    /// stream reset when <c>dsp+0x14[0] != 0</c>.
    /// </summary>
    internal static float[] DecodeFraming(DecoderState dsp, ReadOnlyMemory<byte> data, int firstAudioOffset,
        uint sampleCount)
    {
        var span = data.Span;
        int offset = firstAudioOffset;
        int total = data.Length;
        int channels = dsp.Channels;
        var blocks = new List<float[]>();
        int produced = 0;
        while (offset + 2 <= total)
        {
            int size = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));
            if (offset + 2 + size > total)
                throw new InvalidDataException("a Vorbis audio packet runs past the end of the data (P1)");
            // 0x00AB7E98..0x00AB7EBC: eofflag = (packet_end == total) && (framing+0x54 & 1). In the shipped
            // source the byte at framing+0x54 is the source's ready flag src+0x90, which the render sets to 1
            // (0x00AB0494 `strb r3,[r4,#0x90]`), so the gate reduces to packet_end == total here.
            bool endOfFile = offset + 2 + size == total;
            dsp.LastFlag = endOfFile;
            PacketEntry(dsp, data.Slice(offset + 2, size), endOfFile);
            offset += 2 + size;

            int available = dsp.End - dsp.Start;
            if (available > 0)
            {
                // 0x00AB7FB4: the driver is handed `available` (its own min is at 0x00AB3558); the total
                // sample count is applied only when assembling the final output.
                float[] block = WindowOverlap(dsp, available, channels);
                if (block.Length > 0) { blocks.Add(block); produced += block.Length / channels; }
            }
        }

        if (dsp.Work.Length > 0 && dsp.Work[0] is not null) StreamReset(dsp); // gate dsp+0x14[0] != 0

        var output = new float[channels * produced];
        int at = 0;
        foreach (var b in blocks) { Array.Copy(b, 0, output, at, b.Length); at += b.Length; }
        int want = (int)Math.Min((long)sampleCount * channels, output.Length);   // the header's SampleCount
        if (want < output.Length) Array.Resize(ref output, want);
        return output;
    }

    /// <summary>
    /// The window/overlap driver 0x00AB3520 (P24-P26/C13): start/end selection, the per-channel combine, the
    /// overlap save, and planar float output. Returns <paramref name="channels"/> planes of
    /// <c>min(n, End-Start)</c> samples; advances <c>dsp+0x1c</c> and sets <c>dsp+0x30 = 1</c>.
    /// </summary>
    internal static float[] WindowOverlap(DecoderState dsp, int n, int channels)
    {
        int start = dsp.Start, end = dsp.End;
        if (start >= end) return Array.Empty<float>();
        LoadSharedWork(dsp);                                               // the combine and the overlap save read work[ch] in the shared buffer
        int available = end - start;
        int frames = Math.Min(n, available);
        int bs0 = dsp.Setup.BlockSize0, bs1 = dsp.Setup.BlockSize1;
        float[] w0 = WindowTable(bs0), w1 = WindowTable(bs1);
        var output = new float[channels * frames];
        for (int ch = 0; ch < channels; ch++)
        {
            Combine(bs0, bs1, dsp.PreviousFlag != 0, dsp.CurrentFlag != 0, dsp.Work[ch], dsp.Overlap[ch],
                w0, w1, output.AsSpan(ch * frames, frames), start, frames + start);

            // The overlap write-back is in the caller (0x00AB3668..0x00AB3698), not the combine.
            int blockSize = dsp.Setup.BlockSize(dsp.CurrentFlag);
            int aligned = (blockSize + 3) & ~3;
            int off = aligned / 4, len = aligned / 4;
            // memcpy only: the rest of the overlap keeps its contents
            int copy = Math.Min(len, Math.Min(dsp.Work[ch].Length - off, dsp.Overlap[ch].Length));
            if (copy > 0) Array.Copy(dsp.Work[ch], off, dsp.Overlap[ch], 0, copy);
        }
        dsp.Start += frames;
        dsp.WindowSaved = 1;
        return output;
    }

    // ---- the decoder entry ----

    /// <summary>
    /// Decodes a shipped Wwise Vorbis <c>.wem</c> to planar float PCM through the native path: the setup
    /// parse (P5-P8), the packet framing (P1/P2), the packet entry (P9-P15), the packet inverse (P16-P23)
    /// and the window/overlap driver (P24-P26). The stream reset (C13) primes the overlap at the end.
    /// </summary>
    public static float[] Decode(WwiseMedia media, WwiseCodebookLibrary codebooks)
    {
        if (media.Codec != WwiseCodec.Vorbis || media.Vorbis is null)
            throw new InvalidDataException("the media is not Wwise Vorbis (M6-002)");
        var header = media.Vorbis;
        var data = media.Data;

        int setupAt = (int)header.SetupPacketOffset;
        if (setupAt + 2 > data.Length)
            throw new InvalidDataException("the Vorbis setup packet offset is past the data (V3)");
        int setupSize = BinaryPrimitives.ReadUInt16LittleEndian(data.Span.Slice(setupAt, 2));
        if (setupAt + 2 + setupSize > data.Length)
            throw new InvalidDataException("the Vorbis setup packet runs past the data (V3)");

        var setup = ParseSetup(new BitReader(data.Slice(setupAt + 2, setupSize)), codebooks, media.Channels,
            header.BlockSize0Pow, header.BlockSize1Pow);

        // The standalone decode models a single play: the normal start skip is 0 (gapG 6.12) and the end
        // trim is the loop-count-1 value (the header's EndTrimSingle). This is a host choice for the
        // standalone entry; the source wrapper supplies the PBI's loop count and seek position.
        int skip = StartSkip(seeking: false, seekPosition: 0);
        int trim = EndTrim(loopCount: 1, header);
        var dsp = CreateState(setup, media.Channels, skip, trim);
        return DecodeFraming(dsp, data, (int)header.FirstAudioPacketOffset, header.SampleCount);
    }
}
