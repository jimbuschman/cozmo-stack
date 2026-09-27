using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-002 tests for the native Wwise Vorbis decoder.
///
/// The expectations come from the frozen inventory rows (V2, V3, V6, gapG 6.2, 6.3, 6.5, 6.7, 6.10, 6.12),
/// not from what the code returns: every value is hand-computed from the row's bit widths and formulas. The
/// rows settle the setup prefix, the quantization arithmetic, the residue fields and the skip/trim selection;
/// they explicitly leave the floor, residue-inverse, IMDCT, window, overlap-add and end-trim arithmetic
/// unread, so the decoder entry must refuse rather than guess, and that refusal is itself tested.
/// </summary>
public class WwiseVorbisNativeTests
{
    // ---- helpers ----

    /// <summary>Packs (value, bit count) fields LSB-first into a <see cref="BitReader"/>, as Vorbis packs them.</summary>
    private static BitReader Bits(params (uint Value, int Count)[] fields)
    {
        var bytes = new List<byte>();
        uint acc = 0;
        int n = 0;
        foreach (var (value, count) in fields)
            for (int i = 0; i < count; i++)
            {
                acc |= ((value >> i) & 1u) << n;
                if (++n == 8) { bytes.Add((byte)acc); acc = 0; n = 0; }
            }
        if (n > 0) bytes.Add((byte)acc);
        return new BitReader(bytes.ToArray());
    }

    private static byte[] Riff(int channels, int rate, int blockAlign, byte[] data, byte[]? ext = null)
    {
        var fmt = new List<byte>();
        fmt.AddRange(BitConverter.GetBytes((ushort)0xFFFF));
        fmt.AddRange(BitConverter.GetBytes((ushort)channels));
        fmt.AddRange(BitConverter.GetBytes((uint)rate));
        fmt.AddRange(BitConverter.GetBytes((uint)(rate * blockAlign / 64)));
        fmt.AddRange(BitConverter.GetBytes((ushort)blockAlign));
        fmt.AddRange(BitConverter.GetBytes((ushort)4));
        fmt.AddRange(BitConverter.GetBytes((ushort)(ext?.Length ?? 0)));
        if (ext is not null) fmt.AddRange(ext);

        var body = new List<byte>();
        body.AddRange("WAVE"u8.ToArray());
        body.AddRange("fmt "u8.ToArray());
        body.AddRange(BitConverter.GetBytes((uint)fmt.Count));
        body.AddRange(fmt);
        body.AddRange("data"u8.ToArray());
        body.AddRange(BitConverter.GetBytes((uint)data.Length));
        body.AddRange(data);

        var file = new List<byte>();
        file.AddRange("RIFF"u8.ToArray());
        file.AddRange(BitConverter.GetBytes((uint)body.Count));
        body.ForEach(file.Add);
        return file.ToArray();
    }

    // ---- setup: block sizes (V2, 0x00AB6380..0x00AB63DC) ----

    /// <summary>
    /// M6-002 / V2: the sizes are 1&lt;&lt;hdr+0x7C and 1&lt;&lt;hdr+0x7D; the native fails (error −0x85) when
    /// bs0 &lt; 64, bs0 &gt; bs1 or bs1 &gt; 8192. 1&lt;&lt;6 = 64 is the boundary that passes, 1&lt;&lt;13 = 8192
    /// is the upper boundary.
    /// </summary>
    [Fact]
    public void TheBlockSizeCheckIsTheRowsValidation()
    {
        Assert.Equal((64, 2048), WwiseVorbisNative.BlockSizes(6, 11));
        Assert.Equal((64, 8192), WwiseVorbisNative.BlockSizes(6, 13));

        Assert.Throws<InvalidDataException>(() => WwiseVorbisNative.BlockSizes(5, 11));  // bs0 = 32 < 64
        Assert.Throws<InvalidDataException>(() => WwiseVorbisNative.BlockSizes(11, 6));  // bs0 > bs1
        Assert.Throws<InvalidDataException>(() => WwiseVorbisNative.BlockSizes(8, 14));  // bs1 = 16384 > 8192
    }

    // ---- setup: codebook ids and the built-in library (V3, gapG 6.6) ----

    /// <summary>
    /// M6-002 / V3, gapG 6.6, row 0.7: the setup reads <c>read(8)+1</c> codebooks, each a 10-bit index into
    /// the built-in library, and then (no time-domain section) the floor count <c>read(6)+1</c>. The vendored
    /// library is the 598-book set in a 599-entry pointer table.
    /// </summary>
    [Fact]
    public void TheCodebookIdsAreSelectedFromTheBuiltInLibrary()
    {
        var cbl = WwiseAudioSource.TryLoadCodebooks();
        Assert.NotNull(cbl);
        Assert.Equal(598, cbl!.Count);                      // 0.7: 598 books (599-entry table with the end pointer)

        // 3 codebooks, ids 0, 597 (the last legal) and 12; then 2 floors.
        var reader = Bits((2, 8), (0, 10), (597, 10), (12, 10), (1, 6));
        var prefix = WwiseVorbisNative.ReadSetupPrefix(reader, cbl);
        Assert.Equal(new[] { 0, 597, 12 }, prefix.CodebookIds);
        Assert.Equal(2, prefix.FloorCount);

        // an id equal to Count is outside the library and refused, not truncated
        Assert.Throws<InvalidDataException>(() =>
            WwiseVorbisNative.ReadSetupPrefix(Bits((0, 8), (598, 10)), cbl));
    }

    // ---- setup: modes (V3, V6) ----

    /// <summary>
    /// M6-002 / V6 (0x00AB37FC..0x00AB3934): the mode number is read with a hard-coded 1 bit, so only the
    /// stream's first bit matters; 0x02 has bit 0 clear and must read as mode 0, not mode 1.
    /// </summary>
    [Fact]
    public void TheModeNumberIsTheRowsOneBit()
    {
        Assert.Equal(1, WwiseVorbisNative.ReadModeNumber(Bits((1, 1))));
        Assert.Equal(0, WwiseVorbisNative.ReadModeNumber(Bits((0, 2))));
        Assert.Equal(0, WwiseVorbisNative.ReadModeNumber(Bits((2, 2))));
    }

    /// <summary>M6-002 / V3: a mode entry is <c>blockflag read(1)</c> then <c>mapping read(8)</c>.</summary>
    [Fact]
    public void AModeEntryReadsTheBlockFlagThenTheMapping()
    {
        var mode = WwiseVorbisNative.ReadMode(Bits((1, 1), (5, 8)));
        Assert.True(mode.BlockFlag);
        Assert.Equal(5, mode.Mapping);

        var shortMode = WwiseVorbisNative.ReadMode(Bits((0, 1), (0, 8)));
        Assert.False(shortMode.BlockFlag);
        Assert.Equal(0, shortMode.Mapping);
    }

    // ---- setup: residue fields (gapG 6.7, 0x00AB6F54..0x00AB72xx) ----

    /// <summary>
    /// M6-002 / gapG 6.7: type read(2); begin/end/grouping read(24) with grouping stored +1; partitions
    /// read(6)+1; groupbook read(8); the cascade per partition is read(3) low, read(1) flag, read(5) high;
    /// then one read(8) book per present stage. Fields 1, 10, 20, grouping 0, partitions 0, groupbook 7,
    /// cascade 1 with only stage 0 present and book 3 give the hand-computed struct.
    /// </summary>
    [Fact]
    public void TheResidueSetupFieldsAreReadInTheRowsOrder()
    {
        var reader = Bits(
            (1, 2), (10, 24), (20, 24), (0, 24), (0, 6), (7, 8),
            (1, 3), (0, 1), (3, 8));
        var res = WwiseVorbisNative.ReadResidueSetup(reader);

        Assert.Equal(1, res.Type);
        Assert.Equal(10, res.Begin);
        Assert.Equal(20, res.End);
        Assert.Equal(1, res.Grouping);          // 0 + 1
        Assert.Equal(1, res.Partitions);        // 0 + 1
        Assert.Equal(7, res.GroupBook);
        Assert.Equal(new byte[] { 1 }, res.Cascades);
        Assert.Equal(3, res.StageBooks[0][0]);
        Assert.Equal(0, res.StageBooks[0][1]);  // no book read for a clear stage bit
    }

    // ---- codebook quantization (gapG 6.2, 6.3, 6.5) ----

    /// <summary>
    /// M6-002 / gapG 6.2: the stock Tremor integer <c>_float32_unpack</c>. For 0x00100000 the exponent is 0
    /// and the magnitude 0x100000 needs ten left shifts to reach bit 30, so point = 0 − 788 − 10 = −798.
    /// 0x80100000 is the same magnitude with the sign bit, so the mantissa is negated. A zero mantissa is
    /// (0, −9999).
    /// </summary>
    [Fact]
    public void TheFloat32UnpackIsStockTremor()
    {
        Assert.Equal((0, -9999), WwiseVorbisNative.Float32Unpack(0u));

        var (mant, point) = WwiseVorbisNative.Float32Unpack(0x00100000u);
        Assert.Equal(1 << 30, mant);
        Assert.Equal(-798, point);

        var (negMant, negPoint) = WwiseVorbisNative.Float32Unpack(0x80100000u);
        Assert.Equal(-(1 << 30), negMant);
        Assert.Equal(-798, negPoint);
    }

    /// <summary>
    /// M6-002 / gapG 6.3: <c>q_bits = read(4)+1</c>, then the <c>q_seq</c> bit is read and discarded, then
    /// <c>q_del &gt;&gt;= q_bits</c> and <c>q_delp += q_bits</c>. Reading 2 gives q_bits 3; 1024 &gt;&gt; 3 = 128.
    /// </summary>
    [Fact]
    public void TheQbitsSequenceIsReadAndDiscarded()
    {
        var reader = Bits((2, 4), (1, 1));
        Assert.Equal(3, WwiseVorbisNative.ReadQBits(reader));
        Assert.True(WwiseVorbisNative.ReadAndDiscardQSeq(reader));

        var (qDel, qDelp) = WwiseVorbisNative.ApplyQBits(1024, 0, 3);
        Assert.Equal(128, qDel);
        Assert.Equal(3, qDelp);
    }

    /// <summary>
    /// M6-002 / gapG 6.5 (0xAB9BB0..0xABA184): <c>add = q_min shifted by (point − q_minp)</c> and
    /// <c>v = add + ((v·q_del) &gt;&gt; (point − q_delp))</c>. With q_min 3 at q_minp 4 and point 8 the add is
    /// 3 &lt;&lt; 4 = 48; a raw 5 with q_del 1000 at q_delp 2 and point 8 is (5·1000) &gt;&gt; 6 = 78; 48 + 78 = 126.
    /// The negative-shift case shifts left.
    /// </summary>
    [Fact]
    public void TheResidueDequantisationIsTheRowsFormula()
    {
        Assert.Equal(126, WwiseVorbisNative.Dequantize(5, 3, 1000, 8, 4, 2));
        // addShift = 2 − 4 = −2 (3 >> 2 = 0); shift = 2 − 2 = 0 ((5·1000) >> 0 = 5000)
        Assert.Equal(5000, WwiseVorbisNative.Dequantize(5, 3, 1000, 2, 4, 2));
    }

    // ---- output: skip and trim (gapG 6.12) ----

    /// <summary>
    /// M6-002 / gapG 6.12: the loop-back start skip is <c>u16 fmt+0x24</c>; the end trim is <c>u16 fmt+0x32</c>
    /// when the loop count is 1, else <c>u16 fmt+0x26</c>; the normal start skip is 0 unless seeking, when it
    /// is the seek sample position.
    /// </summary>
    [Fact]
    public void TheSkipAndTrimAreSelectedFromTheVorbHeader()
    {
        // fmt+0x18 is the vorb header, so fmt+0x24 = vorb+0x0C, fmt+0x26 = vorb+0x0E, fmt+0x32 = vorb+0x1A.
        var fmt = new byte[0x42];
        BitConverter.GetBytes((ushort)50).CopyTo(fmt, 0x24);   // LoopStartSkip
        BitConverter.GetBytes((ushort)200).CopyTo(fmt, 0x26);  // EndTrimLoop
        BitConverter.GetBytes((ushort)100).CopyTo(fmt, 0x32);  // EndTrimSingle
        var header = WwiseVorbisHeader.Parse(fmt);
        Assert.NotNull(header);
        Assert.Equal(50, header!.LoopStartSkip);
        Assert.Equal(200, header.EndTrimLoop);
        Assert.Equal(100, header.EndTrimSingle);

        Assert.Equal(100, WwiseVorbisNative.EndTrim(1, header));
        Assert.Equal(200, WwiseVorbisNative.EndTrim(2, header));
        Assert.Equal(50, WwiseVorbisNative.LoopStartSkip(header));
        Assert.Equal(0, WwiseVorbisNative.StartSkip(seeking: false, seekPosition: 123));
        Assert.Equal(123, WwiseVorbisNative.StartSkip(seeking: true, seekPosition: 123));
    }

    /// <summary>
    /// M6-002 / gapG 6.12 (0xAB3884..0xAB3910): skip drops leading returned samples. 3 skipped of 5 returned
    /// leaves 2; a skip past the whole packet is carried to the next one.
    /// </summary>
    [Fact]
    public void TheLeadingSkipDropsReturnedSamples()
    {
        int skip = 3;
        Assert.Equal(2, WwiseVorbisNative.ApplyLeadingSkip(5, ref skip));
        Assert.Equal(0, skip);

        int carried = 7;
        Assert.Equal(0, WwiseVorbisNative.ApplyLeadingSkip(5, ref carried));
        Assert.Equal(2, carried);
        Assert.Equal(3, WwiseVorbisNative.ApplyLeadingSkip(5, ref carried));
        Assert.Equal(0, carried);
    }

    // ---- correction C5 arithmetic (re-analysis/evidence/m6-vorbis/vorbis-arithmetic.md) ----

    /// <summary>
    /// M6-002 / correction C5 row 1a..1c (0x00AB88D8): partitions read(5); partitionclass read(4);
    /// dim=read(3)+1; subs=read(2); one subbook read(8)−1 even when subs=0; mult=read(2)+1;
    /// rangebits=read(4); count = Σ dim; posts at postlist[2..]; postlist[0]=0, postlist[1]=1&lt;&lt;rangebits.
    /// The hand-built stream gives partitions 1, class 0 with dim 2, mult 1, rangebits 4 and posts 3, 7.
    /// </summary>
    [Fact]
    public void TheFloor1SetupFieldsAreReadInTheC5Order()
    {
        var reader = Bits((1, 5), (0, 4), (1, 3), (0, 2), (1, 8), (0, 2), (4, 4), (3, 4), (7, 4));
        var floor = WwiseVorbisNative.ReadFloorSetup(reader, codebookCount: 10);

        Assert.Equal(1, floor.Partitions);
        Assert.Equal(new[] { 0 }, floor.PartitionClasses);
        Assert.Single(floor.Classes);
        Assert.Equal(2, floor.Classes[0].Dimensions);
        Assert.Equal(0, floor.Classes[0].Subclasses);
        Assert.Equal(0, floor.Classes[0].MasterBook);
        Assert.Equal(new[] { 0 }, floor.Classes[0].SubBooks);   // read(8)=1 -> 0
        Assert.Equal(1, floor.Multiplier);
        Assert.Equal(4, floor.RangeBits);
        Assert.Equal(new[] { 0, 16, 3, 7 }, floor.PostList);    // [1] = 1<<4
    }

    /// <summary>
    /// M6-002 / correction C5 row 1c (0x00AB8C64 <c>cmp r2,r8</c>, r8 = 1&lt;&lt;rangebits): a floor1 post is
    /// valid iff it is below <c>1 &lt;&lt; rangebits</c>. <see cref="WwiseVorbisNative.ReadFloorSetup"/>
    /// fails closed through this guard; the boundary is 15 accepted / 16 rejected at rangebits 4.
    /// </summary>
    [Fact]
    public void TheFloor1PostRangeCheckIsTheC5Guard()
    {
        Assert.True(WwiseVorbisNative.FloorPostInRange(post: 15, rangeBits: 4));
        Assert.False(WwiseVorbisNative.FloorPostInRange(post: 16, rangeBits: 4));
        Assert.True(WwiseVorbisNative.FloorPostInRange(post: 0, rangeBits: 0));
        Assert.False(WwiseVorbisNative.FloorPostInRange(post: 1, rangeBits: 0));
    }

    /// <summary>
    /// M6-002 / correction C5 row 2a..2c (0x00AB6788): one submap; one coupling step with mag/ang at
    /// ilog(channels−1) = 1 bit each; reserved read(2) = 0; three read(8) per submap (time 9 discarded,
    /// floor 0, residue 1).
    /// </summary>
    [Fact]
    public void TheMappingSetupFieldsAreReadInTheC5Order()
    {
        var reader = Bits((0, 1), (1, 1), (0, 8), (0, 1), (1, 1), (0, 2), (9, 8), (0, 8), (1, 8));
        var map = WwiseVorbisNative.ReadMappingSetup(reader, channels: 2, floorCount: 1, residueCount: 2);

        Assert.Equal(1, map.Submaps);
        Assert.Empty(map.Mux);
        Assert.Equal(new[] { 0 }, map.SubmapFloor);
        Assert.Equal(new[] { 1 }, map.SubmapResidue);
        Assert.Equal(1, map.CouplingSteps);
        Assert.Equal(new[] { (0, 1) }, map.Coupling);
    }

    /// <summary>
    /// M6-002 / correction C6 1e (0x00AB9C4C): <c>packed = entry &amp; 0x7FFFFFFF</c>, then dim values of
    /// q_bits bits lowest first. 21 = 0b10101 with q_bits 3, dim 2 gives 5 then 2; bit 31 is the
    /// <b>leaf</b> marker (C6 corrects C5) and is masked off.
    /// </summary>
    [Fact]
    public void TheDecodeMapLeafPacksDimValuesLowFirst()
    {
        Assert.Equal(new[] { 5, 2 }, WwiseVorbisNative.DecodeMapLeaf(21u, qBits: 3, dim: 2));
        Assert.Equal(new[] { 5, 2 }, WwiseVorbisNative.DecodeMapLeaf(0x80000000u | 21u, qBits: 3, dim: 2));
    }

    /// <summary>
    /// M6-002 / correction C5 rows 4d/4e (0x00ABAA6C, 0x00ABABB8): <c>out[i+j] += tmp[j]</c> for dim
    /// entries, no saturation; decodevv_add toggles channel 0/1 between the entries.
    /// </summary>
    [Fact]
    public void TheResidueDecodevAddAddsDimEntriesWithoutSaturation()
    {
        var output = new[] { 10, 20, 30 };
        WwiseVorbisNative.DecodevAdd(output, new[] { 1, 2 }, index: 1, dim: 2);
        Assert.Equal(new[] { 10, 21, 32 }, output);

        var stereo = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 } };
        WwiseVorbisNative.DecodevvAdd(stereo, new[] { 10, 20 }, index: 0, dim: 2);
        Assert.Equal(new[] { 11, 2, 3 }, stereo[0]);
        Assert.Equal(new[] { 4, 25, 6 }, stereo[1]);
    }

    /// <summary>
    /// M6-002 / correction C5 row 4f (0x00AB6E30): the four integer cases, the right-hand side using the
    /// pair's old values.
    /// </summary>
    [Fact]
    public void TheCouplingInverseIsTheC5IntegerAddSub()
    {
        Assert.Equal((5, 2), WwiseVorbisNative.InverseCoupling(5, 3));      // M>0, A>0: A = M − A
        Assert.Equal((5, 5), WwiseVorbisNative.InverseCoupling(5, 0));      // M>0, A<=0: A = M, M = M + A
        Assert.Equal((-4, -1), WwiseVorbisNative.InverseCoupling(-4, 3));   // M<=0, A>0: A = M + A, M = M
        Assert.Equal((-2, -4), WwiseVorbisNative.InverseCoupling(-4, -2));  // M<=0, A<=0: A = M, M = M − A
    }

    /// <summary>
    /// M6-002 / correction C5 row 5 (0x01058BF0) and row 6b (0x00AB9208): the table's first, second and
    /// last entries, and a render_line whose err reaches adx at x=2. base = 4/5 = 0, sy = 1, ady = 4; at
    /// x=1 err 4 &lt; 5 (y stays 0), at x=2 err 8 >= 5 (y becomes 1), x=3 -> 2, x=4 -> 3.
    /// </summary>
    [Fact]
    public void TheFloorTableAndRenderLineAreTheC5Arithmetic()
    {
        Assert.Equal(256, WwiseVorbisNative.FloorTable.Length);
        Assert.Equal(229f / 32768f, WwiseVorbisNative.FloorTable[0]);       // C5 5: [0] = 229/32768
        Assert.Equal(0.0074462890625f, WwiseVorbisNative.FloorTable[1]);    // C5 5: 244/32768
        Assert.Equal(65536.0f, WwiseVorbisNative.FloorTable[255]);          // C5 5: [255]

        var t = WwiseVorbisNative.FloorTable;
        var d = new float[8];
        Array.Fill(d, 1.0f);
        WwiseVorbisNative.RenderLine(d, x0: 0, y0: 0, x1: 5, y1: 4, t);
        // table[0..3] from C5 row 5 (Appendix A), not read back from the implementation:
        Assert.Equal(new[]
        {
            0.006988525390625f, 0.006988525390625f, 0.0074462890625f, 0.007904052734375f,
            0.0084228515625f, 1f, 1f, 1f,
        }, d);
    }

    /// <summary>
    /// M6-002 / V7 and correction C5 rows 8a/8b (0x01054490, 0x00AB3728), 9 (0x00AB5A94) and 9b
    /// (0x00AB3520): the five window tables and their counts, vwin256's first value (V7: 5.9139e-05) and
    /// last value, the default refusal, the two combine forms and the planar layout.
    /// </summary>
    [Fact]
    public void TheWindowTablesAndCombineAreTheC5Arithmetic()
    {
        // M6-002 / C5 8a: vwin256/512/1024/2048/4096 have 128/256/512/1024/2048 floats.
        Assert.Equal(128, WwiseVorbisNative.WindowTable(256).Length);
        Assert.Equal(256, WwiseVorbisNative.WindowTable(512).Length);
        Assert.Equal(512, WwiseVorbisNative.WindowTable(1024).Length);
        Assert.Equal(1024, WwiseVorbisNative.WindowTable(2048).Length);
        Assert.Equal(2048, WwiseVorbisNative.WindowTable(4096).Length);
        // M6-002 / V7: vwin256 starts 5.9139e-05.
        Assert.Equal(0.0000591390f, WwiseVorbisNative.WindowTable(256)[0]);
        // C5 8a: the tables are byte-for-byte libvorbis window.c, whose vwin256 ends 0.9999999983.
        Assert.Equal(0.9999999983f, WwiseVorbisNative.WindowTable(256)[127]);
        Assert.Throws<NotSupportedException>(() => WwiseVorbisNative.WindowTable(128));   // C5 8b default

        Assert.Equal(3.0f, WwiseVorbisNative.CombineAdd(1f, 1f, 2f, 1f));    // 1*1 + 2*1
        Assert.Equal(-1.0f, WwiseVorbisNative.CombineSub(1f, 1f, 2f, 1f));   // 1*1 − 2*1
        Assert.Equal(1, WwiseVorbisNative.PlanarFrames(2));
        Assert.Equal(3, WwiseVorbisNative.PlanarChannelOffset(3, 1));
    }

    /// <summary>
    /// M6-002 / correction C5 row 10b (0x00AB3884..0x00AB3910): on the end-of-file flag,
    /// <c>current = max(current − trim, returned)</c>; otherwise current is unchanged.
    /// </summary>
    [Fact]
    public void TheEndTrimIsTheC5Consumption()
    {
        Assert.Equal(70, WwiseVorbisNative.ApplyEndTrim(current: 100, returned: 50, trim: 30, endOfFile: true));
        Assert.Equal(50, WwiseVorbisNative.ApplyEndTrim(current: 100, returned: 50, trim: 80, endOfFile: true));
        Assert.Equal(100, WwiseVorbisNative.ApplyEndTrim(current: 100, returned: 50, trim: 30, endOfFile: false));
    }

    // ---- correction C6 arithmetic (re-analysis/evidence/m6-vorbis/vorbis-arithmetic-2.md) ----

    /// <summary>
    /// M6-002 / correction C6 row 1e (0x00AB9C44/0x00AB9C48): the walk loops while the entry is
    /// <b>non-negative</b>, so a 32-bit entry is an internal node when bit31 is clear and a leaf when bit31
    /// is set. This is C5 3a/3b inverted. The leaf unpacking (0x00AB9C4C <c>bic #0x80000000</c>) masks the
    /// marker off; 21 with q_bits 3, dim 2 gives 5 then 2.
    /// </summary>
    [Fact]
    public void TheDecodeMapPolarityIsC6NotC5()
    {
        Assert.True(WwiseVorbisNative.DecodeMapIsLeaf(0x80000000u));
        Assert.False(WwiseVorbisNative.DecodeMapIsLeaf(0x7FFFFFFFu));
        Assert.False(WwiseVorbisNative.DecodeMapIsLeaf(0u));
        Assert.True(WwiseVorbisNative.DecodeMapIsInternal(0u));
        Assert.True(WwiseVorbisNative.DecodeMapIsInternal(0x7FFFFFFFu));
        Assert.False(WwiseVorbisNative.DecodeMapIsInternal(0x80000000u));

        Assert.Equal(new[] { 5, 2 }, WwiseVorbisNative.DecodeMapLeaf(0x80000000u | 21u, qBits: 3, dim: 2));
    }

    /// <summary>
    /// M6-002 / correction C6 rows 1c/1d/1f (0x00AB96EC / 0x00AB9BB0): the builder stores 8-bit entries
    /// with a bit7 leaf marker for format 1 and 16-bit entries with a bit15 marker for format 2, moving
    /// bit31 down; a leaf's payload is stored across the entry and the <b>adjacent</b> table word
    /// (format 1: <c>((entry&amp;0x7f)&lt;&lt;8)|nextByte</c>, format 2:
    /// <c>((entry&amp;0x7fff)&lt;&lt;16)|nextHalfword</c>). It has no 32-bit store; the format-4
    /// single-entry case is refused. The decoder dispatches on the format and reads the marker at the
    /// matching bit.
    /// </summary>
    [Fact]
    public void TheDecodeTableBuilderAndWalkAreTheC6Widths()
    {
        // payload 259 = 0x103: high 7 bits 1 -> entry, low 8 bits 3 -> next byte.
        Assert.Equal((0x80u | 1u, 3u), WwiseVorbisNative.MakeDecodeTableEntry(1, 0x80000000u | 259u));
        // payload 0x00010003: high 15 bits 1 -> entry, low 16 bits 3 -> next halfword.
        Assert.Equal((0x8000u | 1u, 3u), WwiseVorbisNative.MakeDecodeTableEntry(2, 0x80000000u | 0x00010003u));
        Assert.Equal((0u, 0u), WwiseVorbisNative.MakeDecodeTableEntry(1, 0u));  // non-negative -> internal
        Assert.Throws<NotSupportedException>(() => WwiseVorbisNative.MakeDecodeTableEntry(4, 0x80000000u));
        Assert.Throws<NotSupportedException>(() => WwiseVorbisNative.MakeDecodeTableEntry(3, 0u));

        Assert.Equal(8, WwiseVorbisNative.DecodeTableEntryBits(1));
        Assert.Equal(16, WwiseVorbisNative.DecodeTableEntryBits(2));
        Assert.Equal(32, WwiseVorbisNative.DecodeTableEntryBits(3));
        Assert.Throws<NotSupportedException>(() => WwiseVorbisNative.DecodeTableEntryBits(4));

        Assert.True(WwiseVorbisNative.DecodeTableEntryIsLeaf(1, 0x80u));
        Assert.False(WwiseVorbisNative.DecodeTableEntryIsLeaf(1, 0x7Fu));
        Assert.True(WwiseVorbisNative.DecodeTableEntryIsLeaf(2, 0x8000u));
        Assert.False(WwiseVorbisNative.DecodeTableEntryIsLeaf(2, 0x7FFFu));
        Assert.True(WwiseVorbisNative.DecodeTableEntryIsLeaf(32, 0x80000000u));

        // two-word payload: the entry is the high part, the adjacent word the low part.
        Assert.Equal(259u, WwiseVorbisNative.DecodeTableEntryPayload(1, 0x81u, next: 3u));   // 1<<8 | 3
        Assert.Equal(3u, WwiseVorbisNative.DecodeTableEntryPayload(1, 0x80u, next: 3u));     // 0<<8 | 3
        Assert.Equal(0x00010003u, WwiseVorbisNative.DecodeTableEntryPayload(2, 0x8001u, next: 3u)); // 1<<16 | 3
        Assert.Equal(3u, WwiseVorbisNative.DecodeTableEntryPayload(2, 0x8000u, next: 3u));   // 0<<16 | 3
        Assert.Equal(21u, WwiseVorbisNative.DecodeTableEntryPayload(32, 0x80000000u | 21u, next: 0u));

        // 8-bit walk: node1 is a leaf whose payload 21 sits in the entry's high 7 bits and the next byte.
        var eight = new[] { 0u, 0x80u, 21u };
        Assert.Equal(new[] { 5, 2 }, WwiseVorbisNative.DecodeMapWalk(eight, 1, Bits((1, 1)), qBits: 3, dim: 2));

        // a larger 8-bit leaf: payload 259 = 0x103 read as 8-bit values -> {3, 1}.
        var eightWide = new[] { 0u, 0x81u, 3u };
        Assert.Equal(new[] { 3, 1 }, WwiseVorbisNative.DecodeMapWalk(eightWide, 1, Bits((1, 1)), qBits: 8, dim: 2));

        // internal nodes loop: node1 is internal, the leaf at node 3 holds its low byte at t[4].
        var loop = new[] { 0u, 0u, 0u, 0x80u, 21u };
        Assert.Equal(new[] { 5, 2 }, WwiseVorbisNative.DecodeMapWalk(loop, 1, Bits((1, 1), (1, 1)), qBits: 3, dim: 2));

        // a node index beyond the table fails closed rather than reading past it.
        var tooSmall = new[] { 0u };
        Assert.Throws<InvalidDataException>(() => WwiseVorbisNative.DecodeMapWalk(tooSmall, 1, Bits((1, 1)), qBits: 3, dim: 1));

        // 16-bit table: the marker moves to bit15 and the low halfword follows the entry.
        var sixteen = new[] { 0u, 0x8000u, 21u };
        Assert.Equal(new[] { 5, 2 }, WwiseVorbisNative.DecodeMapWalk(sixteen, 2, Bits((1, 1)), qBits: 3, dim: 2));
    }

    /// <summary>
    /// M6-002 / correction C6 rows 3a/3d (0x00AB7434 / 0x00AB7D48..0x00AB7D94): type 0 shares the type-0/1
    /// branch; the cascade gate is <c>info+4[class] &amp; (1&lt;&lt;stage)</c> and the book is
    /// <c>info+8[class·8+stage]</c> with the codeword base <c>fullbooks+60·book</c> and point −8. A small
    /// case: class digit 3, cascade 0b101 (stages 0 and 2), stage-2 book 7 at index 3·8+2 = 26.
    /// </summary>
    [Fact]
    public void TheResidueStageGateAndBookSelectionIsTheC6InnerOffsets()
    {
        Assert.True(WwiseVorbisNative.ResidueSharesType01Path(0));
        Assert.True(WwiseVorbisNative.ResidueSharesType01Path(1));
        Assert.False(WwiseVorbisNative.ResidueSharesType01Path(2));

        byte cascade = 0b00000101;
        Assert.True(WwiseVorbisNative.ResidueStagePresent(cascade, 0));
        Assert.False(WwiseVorbisNative.ResidueStagePresent(cascade, 1));
        Assert.True(WwiseVorbisNative.ResidueStagePresent(cascade, 2));

        Assert.Equal(3 * 8 + 2, WwiseVorbisNative.ResidueStageBookIndex(classDigit: 3, stage: 2));
        Assert.Equal(60 * 5, WwiseVorbisNative.ResidueBookOffset(book: 5));
        Assert.Equal(-8, WwiseVorbisNative.ResiduePoint);

        // C6 3c: one class word split across channels. 250 with divisors {7,5}: 250/7 = 35 rem 5,
        // 5/5 = 1 rem 0, and the last channel takes the remainder 0; 35·7 + 1·5 + 0 = 250.
        var partword = new byte[] { 7, 5, 0 };
        WwiseVorbisNative.ResidueSplitClassWord(classWord: 250, partword);
        Assert.Equal(new byte[] { 35, 1, 0 }, partword);

        // two channels: 1234/10 = 123 rem 4, and the last channel takes the remainder 4.
        var two = new byte[] { 10, 0 };
        WwiseVorbisNative.ResidueSplitClassWord(classWord: 1234, two);
        Assert.Equal(new byte[] { 123, 4 }, two);
    }

    /// <summary>
    /// M6-002 / correction C6 rows 5b/5c/5d/5e (0x00AB5A94): the two-window add <c>a·wA + b·wB</c>, the
    /// second-window-only <c>a·wA − b·wB</c>, the 64-bit mirror (<c>vrev64.32</c>+<c>vswp</c>, a full
    /// 4-lane reversal) and the single-window <c>vneg</c>.
    /// </summary>
    [Fact]
    public void TheWindowCombineMirrorAndNegateAreTheC6Forms()
    {
        Assert.Equal(3f, WwiseVorbisNative.CombineAdd(1f, 1f, 2f, 1f));      // 1·1 + 2·1
        Assert.Equal(-1f, WwiseVorbisNative.CombineSub(1f, 1f, 2f, 1f));     // 1·1 − 2·1

        var v = new[] { 1f, 2f, 3f, 4f };
        WwiseVorbisNative.Mirror4(v);
        Assert.Equal(new[] { 4f, 3f, 2f, 1f }, v);

        Assert.Equal(new[] { -1f, 2f, -3f }, WwiseVorbisNative.NegateWindow(new[] { 1f, -2f, 3f }));
    }

    /// <summary>
    /// M6-002 / correction C6 row 7a (0x00AB8D68..0x00AB8E14): for post i (2..posts−1), low = the largest
    /// earlier value strictly below <c>postlist[i]</c> and high = the smallest strictly above, starting from
    /// lo=0, hi=1. postlist {0, 32, 10, 25, 20}: post 10 has low 0/high 1; post 25 has low 2 (10); post 20
    /// has low 2 (10) and high 3 (25).
    /// </summary>
    [Fact]
    public void TheFloorNeighbourScanIsTheC6Rule()
    {
        var (low, high) = WwiseVorbisNative.FloorNeighbours(new[] { 0, 32, 10, 25, 20 });
        Assert.Equal(new byte[] { 0, 2, 2 }, low);
        Assert.Equal(new byte[] { 1, 1, 3 }, high);
    }

    /// <summary>
    /// M6-002 / correction C6 row 4a (0x00AB8018): a right-biased bottom-up merge sort of the identity
    /// index permutation keyed by the postlist. {0,16,3,7} gives 0,2,3,1; on equal keys the native stores
    /// the right element first (0x00AB80C8/0x00AB80CC/0x00AB80D8: left only when strictly less), so
    /// {0,5,5,1} gives 0,3,2,1, not the left-stable 0,3,1,2.
    /// </summary>
    [Fact]
    public void TheFloorMergeSortKeyOrderIsTheC6RightBiasedOrder()
    {
        Assert.Equal(new byte[] { 0, 2, 3, 1 }, WwiseVorbisNative.FloorSortedIndices(new[] { 0, 16, 3, 7 }));
        Assert.Equal(new byte[] { 0, 3, 2, 1 }, WwiseVorbisNative.FloorSortedIndices(new[] { 0, 5, 5, 1 }));
    }

    /// <summary>
    /// M6-002 / correction C6 rows 2a/2b and C5 7a (0x00AB4E34 / 0x00AB39D8): the MDCT control-flow shift
    /// is 13 − ilog2(n) (2048 → 2, 256 → 5); the tail's only output scale is 2^-24 (0x33800000), applied to
    /// the final vectors; and the unit trig constant is 0x3F3504F3 = 0.70710677.
    /// </summary>
    [Fact]
    public void TheImdctShiftAndTailScaleAreTheC6Facts()
    {
        Assert.Equal(2, WwiseVorbisNative.ImdctShift(2048));
        Assert.Equal(5, WwiseVorbisNative.ImdctShift(256));
        Assert.Equal(1f / 16777216f, WwiseVorbisNative.ImdctOutputScale);
        Assert.Equal(0.70710677f, WwiseVorbisNative.ImdctUnitTrig);

        var x = new[] { 16777216f, -8388608f };
        WwiseVorbisNative.ApplyImdctOutputScale(x);
        Assert.Equal(1f, x[0]);
        Assert.Equal(-0.5f, x[1]);
    }

    /// <summary>
    /// M6-002 / correction C6 row 6b/6c (0x00AB3728): the window default stores a 0 pointer that
    /// 0x00AB5A94 dereferences; whether a shipped header can reach it is UNKNOWN, so the dispatch fails
    /// closed. C6 also attributes the allocation to 0x00AB3780: <c>n/2·4·channels</c> bytes.
    /// </summary>
    [Fact]
    public void TheWindowDefaultFailsClosedAndTheAllocationIsC6()
    {
        Assert.Throws<NotSupportedException>(() => WwiseVorbisNative.WindowTable(128));   // half = 64
        Assert.Throws<NotSupportedException>(() => WwiseVorbisNative.WindowTable(8192));  // half = 4096
        Assert.False(WwiseVorbisNative.WindowDefaultReachabilityKnown);

        Assert.Equal((2 / 2) * 4 * 3, WwiseVorbisNative.PlanarAllocatedBytes(n: 2, channels: 3));
        Assert.Equal(2, WwiseVorbisNative.PlanarFrames(4));
        Assert.Equal(3, WwiseVorbisNative.PlanarChannelOffset(3, 1));
    }

    // ---- deviations, inert on shipped media (gapG 6.8..6.10) ----

    /// <summary>
    /// M6-002 / gapG 6.8..6.10: the shipped deviations are refused rather than defaulted. Type 0 is the unread
    /// type-0 decodev_add branch; type 2 is hard-wired to 2 channels; the mode count must be 2 or the 1-bit
    /// mode read is wrong.
    /// </summary>
    [Fact]
    public void TheUnreadResidueAndModeDeviationsAreRefused()
    {
        Assert.Throws<NotSupportedException>(() => WwiseVorbisNative.CheckResidueDeviation(0, 1));
        Assert.Throws<NotSupportedException>(() => WwiseVorbisNative.CheckResidueDeviation(2, 1));
        WwiseVorbisNative.CheckResidueDeviation(1, 1);     // shipped mono
        WwiseVorbisNative.CheckResidueDeviation(2, 2);     // shipped stereo

        Assert.Throws<NotSupportedException>(() => WwiseVorbisNative.CheckShippedModeCount(3));
        WwiseVorbisNative.CheckShippedModeCount(2);
    }

    // ---- the decoder entry refuses the unread arithmetic ----

    /// <summary>
    /// M6-002 / corrections C5 and C6: C6 settles the decode-table widths/polarity, the residue stage walk,
    /// the window-combine branches, the floor look helper, the floor neighbour scan and the IMDCT tail
    /// scale, so they are no longer refused. The pieces the rows still leave open are the decode-map
    /// builder's tree construction (0x00AB96EC), the <c>codebook+0x14 == 4</c> case, the IMDCT kernel
    /// (0x00AB4E34) and the floor1 inverse1 decode (0x00AB8E60); the decoder entry must name those rather
    /// than return plausible samples.
    /// </summary>
    [Fact]
    public void TheUnreadDecoderArithmeticIsRefusedNotGuessed()
    {
        var media = WwiseMedia.Parse(Riff(1, 48000, 0, new byte[64], new byte[0x2A]));
        var cbl = WwiseAudioSource.TryLoadCodebooks();
        Assert.NotNull(cbl);

        var ex = Assert.Throws<NotSupportedException>(() => WwiseVorbisNative.Decode(media, cbl!));
        Assert.Contains("0x00AB96EC", ex.Message);          // decode-map builder tree construction
        Assert.Contains("codebook+0x14 == 4", ex.Message);  // the single-entry case
        Assert.Contains("0x00AB4E34", ex.Message);          // IMDCT kernel
        Assert.Contains("0x00AB8E60", ex.Message);          // floor1 inverse1
    }

    /// <summary>
    /// The shipped file is loadable, and the native entry refuses it with the cited gaps. Because the rows do
    /// not settle the decode arithmetic, the planar-float output length after skip/trim cannot be asserted
    /// yet; the record's <c>unresolved</c> names the missing pieces. This replaces the length assertion the
    /// M6-002 task would otherwise make.
    /// </summary>
    [Fact]
    public void AShippedVorbisFileIsRefusedByTheNativeDecoderWithTheCitedGaps()
    {
        var lib = WwiseAssets.Library;
        if (lib is null || lib.MediaFileCount == 0) return;   // AssetPresenceTests fails the run when the OBB is absent
        var cbl = WwiseAudioSource.TryLoadCodebooks();
        Assert.NotNull(cbl);

        bool exercised = false;
        foreach (var mid in lib.AllMediaIds)
        {
            var bytes = lib.ReadMedia(mid, out _);
            if (bytes is null) continue;
            WwiseMedia parsed;
            try { parsed = WwiseMedia.Parse(bytes); } catch (InvalidDataException) { continue; }
            if (parsed.Codec != WwiseCodec.Vorbis) continue;

            var ex = Assert.Throws<NotSupportedException>(() => WwiseVorbisNative.Decode(parsed, cbl!));
            Assert.Contains("0x00AB4E34", ex.Message);
            exercised = true;
            break;
        }
        Assert.True(exercised, "the shipped library contains Vorbis media, so the refusal must be exercised");
    }
}