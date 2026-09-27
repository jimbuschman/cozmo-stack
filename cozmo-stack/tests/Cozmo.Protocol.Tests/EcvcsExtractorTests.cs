using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M11-032/M11-033/M11-034: the live (shipped) <c>ExtractComponentsViaCharacteristicScale</c>
/// 0x0088F8BC. Every expected value here is hand-derived from
/// <c>re-analysis/evidence/m11/ecvcs-extractor.md</c> (the L2 window bank, the F1-F4 box mean and tables,
/// the B1 <c>numFilters3</c> rule, the D4-D7 DP and segment format), never read back from the
/// implementation.
/// </summary>
public class EcvcsExtractorTests
{
    // ------------------------------------------------------------------ M11-033 window bank

    [Fact]
    public void TheWindowBankIsBaseShiftedByIndex()
    {
        var p = new QuadDetectorParameters();
        // L2: size = params+0x04 + 2, list[i] = params+0x08 << i; shipped (1, 4) -> {4, 8, 16}
        Assert.Equal(new[] { 4, 8, 16 }, EcvcsExtractor.WindowBank(p));
        // L6: maxScale = max(list) + 1 = 17
        Assert.Equal(17, EcvcsExtractor.MaxScale(EcvcsExtractor.WindowBank(p)));

        // L5: the scale count must stay in [1, 0x40]
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EcvcsExtractor.WindowBank(p with { WindowCountMinusTwo = 0x40 }));   // 0x42 windows
    }

    [Fact]
    public void TheBoxReciprocalTablesComeFromTheRecoveredEntries()
    {
        // F2 / the disassembly: s=4: 101>>13, s=8: 227>>16, s=16: 241>>18, each round(2^shift/(2s+1)^2)
        Assert.Equal((101, 13), EcvcsExtractor.FilterCoefficients(4));
        Assert.Equal((227, 16), EcvcsExtractor.FilterCoefficients(8));
        Assert.Equal((241, 18), EcvcsExtractor.FilterCoefficients(16));
        // the general T1/T2 table is not recovered, so another half-width is a named refusal
        Assert.Throws<NotSupportedException>(() => EcvcsExtractor.FilterCoefficients(2));
    }

    // ------------------------------------------------------------------ M11-033 box mean

    /// <summary>
    /// F3/F4 on a hand-written 3x3 image: the four prefix-sum corners of a 3x3 box give its sum, the border
    /// replicates the edge pixel (PadImageRow), and the table's multiplier/shift turn the sum into the box
    /// mean. The table head at 0xC98958/0xC98A5C is <c>T1[1]=57, T2[1]=9</c> (<c>round(2^9/9)</c>), so:
    /// centre sum 450 -&gt; <c>450*57&gt;&gt;9 = 50</c>; corner sum 210 -&gt; <c>210*57&gt;&gt;9 = 23</c>.
    /// </summary>
    [Fact]
    public void TheBoxCornersSumTheIntegralImage()
    {
        var img = new GrayImage(3, 3, new byte[]
        {
            10, 20, 30,
            40, 50, 60,
            70, 80, 90,
        });
        var ii = EcvcsExtractor.IntegralImage(img, border: 1, out int stride);

        // centre 3x3 box: the whole image = 10+..+90 = 450, mean 50
        Assert.Equal((byte)50, EcvcsExtractor.BoxMeanAt(ii, stride, border: 1, row: 1, col: 1, halfWidth: 1, mult: 57, shift: 9));
        // corner 3x3 box over replicated rows/cols {0,0,1} x {0,0,1}:
        // 10,10,20 / 10,10,20 / 40,40,50 = 210 -> 210*57>>9 = 23
        Assert.Equal((byte)23, EcvcsExtractor.BoxMeanAt(ii, stride, border: 1, row: 0, col: 0, halfWidth: 1, mult: 57, shift: 9));
        // bottom-right box over replicated rows/cols {1,2,2} x {1,2,2}:
        // 50,60,60 / 80,90,90 / 80,90,90 = 690 -> 690*57>>9 = 76
        Assert.Equal((byte)76, EcvcsExtractor.BoxMeanAt(ii, stride, border: 1, row: 2, col: 2, halfWidth: 1, mult: 57, shift: 9));
    }

    /// <summary>
    /// The fixed point: a flat 9x9 image of 100 at half-width 4 (a 9x9 box, 81 pixels) has sum 8100, and
    /// <c>8100 * 101 &gt;&gt; 13 = 818100 / 8192 = 99</c> (truncating arithmetic shift). At the larger windows
    /// the same flat image gives 100: <c>28900 * 227 &gt;&gt; 16 = 100</c> and <c>108900 * 241 &gt;&gt; 18 = 100</c>.
    /// </summary>
    [Fact]
    public void TheBoxMeanUsesTheRecoveredMultiplierAndShift()
    {
        var img = new GrayImage(9, 9);
        img.Fill(100);
        var ii = EcvcsExtractor.IntegralImage(img, border: 17, out int stride);

        Assert.Equal((byte)99, EcvcsExtractor.BoxMeanAt(ii, stride, border: 17, row: 4, col: 4, halfWidth: 4, mult: 101, shift: 13));
        Assert.Equal((byte)100, EcvcsExtractor.BoxMeanAt(ii, stride, border: 17, row: 4, col: 4, halfWidth: 8, mult: 227, shift: 16));
        Assert.Equal((byte)100, EcvcsExtractor.BoxMeanAt(ii, stride, border: 17, row: 4, col: 4, halfWidth: 16, mult: 241, shift: 18));

        // the row wrapper produces one value per column
        var row = EcvcsExtractor.BoxMeanRow(ii, stride, border: 17, row: 4, width: 9, halfWidth: 4, mult: 101, shift: 13);
        Assert.Equal(9, row.Length);
        Assert.All(row, v => Assert.Equal((byte)99, v));
    }

    // ------------------------------------------------------------------ M11-033 numFilters3

    /// <summary>
    /// B1: <c>v = (|f1-f0| &gt; |f2-f1|) ? f1 : f2</c> with a <b>strict</b> <c>&gt;</c>, so an exact tie
    /// keeps <c>f2</c>. With the gaps tied, a pixel between f1 and f2 is dark only because f2 was taken.
    /// </summary>
    [Fact]
    public void TheSelectionIsStrictAtTheLargestAdjacentJump()
    {
        // gaps 10 and 10 are tied, so v = f2 = 20; pixel 10 is below 20 (and not below f1 = 10)
        Assert.Equal((byte)1, EcvcsExtractor.NumFilters3(f0: 0, f1: 10, f2: 20, pixel: 10, mult: 0x10000));
        // the first gap is larger, so v = f1 = 20
        Assert.Equal((byte)1, EcvcsExtractor.NumFilters3(f0: 0, f1: 20, f2: 10, pixel: 15, mult: 0x10000));
        Assert.Equal((byte)0, EcvcsExtractor.NumFilters3(f0: 0, f1: 20, f2: 10, pixel: 20, mult: 0x10000));
        // no gap: v = f2
        Assert.Equal((byte)1, EcvcsExtractor.NumFilters3(f0: 5, f1: 5, f2: 5, pixel: 4, mult: 0x10000));
        Assert.Equal((byte)0, EcvcsExtractor.NumFilters3(f0: 5, f1: 5, f2: 5, pixel: 5, mult: 0x10000));
    }

    /// <summary>
    /// B1's binarise is <c>((v * mult) &gt;&gt; 16) &gt; pixel</c> with the shipped mult 0xCCCC. For
    /// v = 100: <c>100 * 0xCCCC = 5242800; 5242800 &gt;&gt; 16 = 79</c> (truncating), so 79 is the boundary.
    /// </summary>
    [Fact]
    public void TheBinariseMultipliesAndTruncates()
    {
        byte v = 100;
        Assert.Equal(79, (v * 0xCCCC) >> 16);
        Assert.Equal((byte)1, EcvcsExtractor.NumFilters3(v, v, v, pixel: 78, mult: 0xCCCC));
        Assert.Equal((byte)0, EcvcsExtractor.NumFilters3(v, v, v, pixel: 79, mult: 0xCCCC));
    }

    [Fact]
    public void AFlatImageProducesNoDarkPixels()
    {
        // every box mean is ~100, and 100*0xCCCC>>16 = 79 is not > 100
        var img = new GrayImage(9, 9);
        img.Fill(100);
        var mask = EcvcsExtractor.BinaryMask(img, new QuadDetectorParameters());
        Assert.All(mask.Pixels, v => Assert.Equal((byte)0, v));
    }

    // ------------------------------------------------------------------ M11-034 run extraction

    [Fact]
    public void ARunShorterThanTheFloorIsDropped()
    {
        var row = new byte[] { 1, 1, 0, 1, 0 };

        // a = 2: only the two-pixel run [0,1]
        var kept = EcvcsExtractor.ExtractRunSegments(row, row.Length, rowIndex: 0, a: 2, b: 0);
        var s = Assert.Single(kept);
        Assert.Equal((short)0, s.Start);
        Assert.Equal((short)1, s.End);
        Assert.Equal((ushort)0, s.Row);
        Assert.Equal((ushort)0xFFFF, s.Id);   // D4: the id is filled in by NextRow

        // a = 1: both runs, in order
        var all = EcvcsExtractor.ExtractRunSegments(row, row.Length, rowIndex: 0, a: 1, b: 0);
        Assert.Equal(2, all.Count);
        Assert.Equal((short)3, all[1].Start);
        Assert.Equal((short)3, all[1].End);

        // open Q2: the threshold b is a RECOVERABLE_GAP, so a non-zero value is refused
        Assert.Throws<NotSupportedException>(() => EcvcsExtractor.ExtractRunSegments(row, row.Length, 0, a: 1, b: 1));
    }

    // ------------------------------------------------------------------ M11-034 union-find and segments

    /// <summary>
    /// D5's union-find across two rows. Two runs in the first row and two directly below: each column's
    /// runs overlap, so there are two components of four pixels each.
    /// </summary>
    [Fact]
    public void OverlappingRunsAcrossRowsJoinTheSameComponent()
    {
        var mask = Mask(6, 2, new byte[]
        {
            0, 1, 1, 0, 1, 1,
            0, 1, 1, 0, 1, 1,
        });
        var segments = EcvcsExtractor.ExtractComponents(mask, a: 1, b: 0, out int maxId);
        Assert.Equal(4, segments.Count);
        Assert.Equal(1, maxId);
        var comps = EcvcsExtractor.ToComponents(segments);
        Assert.Equal(2, comps.Count);
        Assert.All(comps, c => Assert.Equal(4, c.Pixels));
    }

    /// <summary>
    /// A single run in the second row that spans both runs above merges them (min, no path compression),
    /// so all three segments share one id and the component is 4 + 4 = 8 pixels.
    /// </summary>
    [Fact]
    public void ARunThatSpansTwoComponentsMergesThem()
    {
        var mask = Mask(6, 2, new byte[]
        {
            0, 1, 1, 0, 1, 1,
            0, 1, 1, 1, 1, 0,
        });
        var segments = EcvcsExtractor.ExtractComponents(mask, a: 1, b: 0, out _);
        Assert.Equal(3, segments.Count);
        var ids = segments.Select(s => s.Id).Distinct().ToList();
        Assert.Single(ids);
        var comp = Assert.Single(EcvcsExtractor.ToComponents(segments));
        Assert.Equal(8, comp.Pixels);
        Assert.Equal(0, comp.MinY);          // the two first-row runs are part of the merge
        Assert.Equal(5, comp.MaxX);          // ...and reach the second run's right edge
    }

    /// <summary>A run with no overlap above gets its own id.</summary>
    [Fact]
    public void ANonOverlappingRunStartsANewComponent()
    {
        var mask = Mask(6, 2, new byte[]
        {
            1, 1, 0, 0, 0, 0,
            0, 0, 0, 1, 1, 0,
        });
        var segments = EcvcsExtractor.ExtractComponents(mask, a: 1, b: 0, out _);
        Assert.Equal(2, EcvcsExtractor.ToComponents(segments).Count);
    }

    /// <summary>
    /// The segment list for a 2x2 block: two runs (one per row), the same component, 4 pixels, bounding box
    /// (1,1)-(2,2). Each segment carries the D7 layout's fields.
    /// </summary>
    [Fact]
    public void TheSegmentListDescribesTheBlock()
    {
        var mask = Mask(4, 4, new byte[]
        {
            0, 0, 0, 0,
            0, 1, 1, 0,
            0, 1, 1, 0,
            0, 0, 0, 0,
        });
        var segments = EcvcsExtractor.ExtractComponents(mask, a: 1, b: 0, out _);
        Assert.Equal(2, segments.Count);
        Assert.Equal((short)1, segments[0].Start);
        Assert.Equal((short)2, segments[0].End);
        Assert.Equal((ushort)1, segments[0].Row);
        Assert.Equal((ushort)2, segments[1].Row);
        Assert.Equal(segments[0].Id, segments[1].Id);

        var comp = Assert.Single(EcvcsExtractor.ToComponents(segments));
        Assert.Equal(4, comp.Pixels);
        Assert.Equal(1, comp.MinX);
        Assert.Equal(1, comp.MinY);
        Assert.Equal(2, comp.MaxX);
        Assert.Equal(2, comp.MaxY);
    }

    /// <summary>Two diagonal runs touch at a corner but share no column, so they stay separate components.</summary>
    [Fact]
    public void RunsWithoutACommonColumnAreSeparateComponents()
    {
        var mask = Mask(4, 2, new byte[]
        {
            1, 0, 0, 0,
            0, 1, 0, 0,
        });
        var segments = EcvcsExtractor.ExtractComponents(mask, a: 1, b: 0, out _);
        Assert.Equal(2, EcvcsExtractor.ToComponents(segments).Count);
    }

    private static GrayImage Mask(int width, int height, byte[] pixels) => new(width, height, pixels);
}