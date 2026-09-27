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
    /// M6-002: the rows settle the framing but not the floor/residue-inverse/IMDCT/window/overlap-add
    /// arithmetic (gapG 6.11 and the bounded residuals name the IMDCT normalisation as RECOVERABLE_GAP), so
    /// the decoder entry must throw and name the unread pieces rather than return plausible samples.
    /// </summary>
    [Fact]
    public void TheUnreadDecoderArithmeticIsRefusedNotGuessed()
    {
        var media = WwiseMedia.Parse(Riff(1, 48000, 0, new byte[64], new byte[0x2A]));
        var cbl = WwiseAudioSource.TryLoadCodebooks();
        Assert.NotNull(cbl);

        var ex = Assert.Throws<NotSupportedException>(() => WwiseVorbisNative.Decode(media, cbl!));
        Assert.Contains("0x00AB88D8", ex.Message);   // floor1 body
        Assert.Contains("0x00AB96EC", ex.Message);   // decode-table builder
        Assert.Contains("0x00AB73F8", ex.Message);   // residue inverse
        Assert.Contains("0x00AB4E34", ex.Message);   // IMDCT
        Assert.Contains("0x01054490", ex.Message);   // windows
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