using Cozmo.Robot.Animation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M11-021 (C3.5): the shipped OpenCV 3.1.0 CLAHE 8U transform and the engine's enum-4 dark test. Every
/// expected value is hand-derived from the citations in <c>.scratch/B-M11/pass2-report.md</c> Q1
/// (clip/redistribute 0x2094E..0x209B8, LUT 0x209BA..0x209F2, interpolation 0x204B8..0x20542) and the
/// engine's dark test (0x006B451A..0x006B457E, threshold 80 at 0xC8E14C), never from the code.
/// </summary>
public class ClaheTests
{
    // ------------------------------------------------------------------ LUT: clip / redistribute (0x2094E..0x209B8)

    /// <summary>
    /// Two peaks of 20 with clipLimit 6: each is clipped to 6, clipped = 14+14 = 28, redistBatch = 0,
    /// residual = 28, so bins 0..27 get +1. With lutScale 1 the LUT is the running sum.
    /// </summary>
    [Fact]
    public void TheClaheLutClipsAndRedistributes()
    {
        var hist = new int[256];
        hist[0] = 20; hist[255] = 20;
        var lut = OpenCv310.ClaheCalcLut(hist, clipLimit: 6, lutScale: 1.0f);
        Assert.Equal(7, lut[0]);       // 6 clipped + 1 redistributed
        Assert.Equal(8, lut[1]);       // 7 + 1
        Assert.Equal(34, lut[27]);     // 7 + 27
        Assert.Equal(34, lut[28]);     // no more redistribution
        Assert.Equal(34, lut[254]);
        Assert.Equal(40, lut[255]);    // 34 + 6
    }

    /// <summary>
    /// A flat histogram of 256 ones with no clip: the running sum is i+1, and the LUT rounds
    /// <c>(float)sum * lutScale</c> through <c>vcvtr.s32.f32</c> (ties to even). With lutScale 0.5,
    /// 0.5 -&gt; 0, 1.5 -&gt; 2, 2.5 -&gt; 2, 3.5 -&gt; 4, 127.5 -&gt; 128.
    /// </summary>
    [Fact]
    public void TheClaheLutRoundsTiesToEvenAndScales()
    {
        var hist = new int[256];
        Array.Fill(hist, 1);
        var lut = OpenCv310.ClaheCalcLut(hist, clipLimit: 0, lutScale: 0.5f);
        Assert.Equal(0, lut[0]);       // 0.5 -> 0
        Assert.Equal(1, lut[1]);       // 1.0 -> 1
        Assert.Equal(2, lut[2]);       // 1.5 -> 2
        Assert.Equal(2, lut[3]);       // 2.0 -> 2
        Assert.Equal(2, lut[4]);       // 2.5 -> 2
        Assert.Equal(3, lut[5]);       // 3.0 -> 3
        Assert.Equal(4, lut[6]);       // 3.5 -> 4
        Assert.Equal(128, lut[255]);   // 128.0
    }

    // ------------------------------------------------------------------ interpolation (0x204B8..0x20542)

    /// <summary>
    /// A 4x4 image, two 2x2 tiles per axis, left tiles 30 and right tiles 20. tileSizeTotal 4 gives
    /// lutScale 63.75 and clipLimit max((int)(32*4/256),1) = 1, so each tile's histogram is clipped to 1 and
    /// bins 0..2 receive the 3 clipped pixels. The 30-tile LUT is [64,128,191,...,191,255,...] and the
    /// 20-tile LUT is [64,128,191,...,191,255,...] from bin 20. At the tile boundary x=2 (xa = xa1 = 0.5)
    /// the 20-valued pixels blend the left tile's LUT at 20 (191) with their own (255): 223.
    /// </summary>
    [Fact]
    public void TheClaheTransformBlendsTheTileLuts()
    {
        var src = new byte[16];
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                src[y * 4 + x] = (byte)(x < 2 ? 30 : 20);
        var dst = OpenCv310.Clahe8U(src, rows: 4, cols: 4, tilesX: 2, tilesY: 2, clipLimit: 32.0);

        Assert.Equal(255, dst[0 * 4 + 0]);   // inside the 30 tile, own LUT at 30
        Assert.Equal(255, dst[0 * 4 + 1]);   // xa = 0, own tile
        Assert.Equal(223, dst[0 * 4 + 2]);   // blend 191 (left) and 255 (own)
        Assert.Equal(255, dst[0 * 4 + 3]);   // xa = 0, own tile
        Assert.Equal(223, dst[2 * 4 + 2]);   // same blend one tile row down
        Assert.Equal(255, dst[2 * 4 + 0]);
    }

    /// <summary>The shipped camera is 320x240, divisible by the 4x4 grid; any other size is the untranscribed branch.</summary>
    [Fact]
    public void TheNonDivisibleClaheBranchIsRefused()
    {
        Assert.Throws<NotSupportedException>(() => OpenCv310.Clahe8U(new byte[3 * 3], rows: 3, cols: 3, tilesX: 4, tilesY: 4, clipLimit: 32.0));
    }

    // ------------------------------------------------------------------ engine dark test (0x006B451A..0x006B457E)

    /// <summary>
    /// The enum-4 dark test sums every 3rd byte of every 3rd row: <c>((cols+2)/3)*((rows+2)/3)</c> samples
    /// (107*80 = 8560 for 320x240). The flag is cleared when <c>sum &gt;= 80*8560</c>, so a frame whose
    /// sampled mean is at least 80 is bright and CLAHE is skipped; below 80 it is applied.
    /// </summary>
    [Fact]
    public void TheClaheDarkTestUsesEveryThirdPixelAgainstEighty()
    {
        var bright = new GrayImage(320, 240); bright.Fill(100);
        Assert.False(VisionSystem.IsDarkForClahe(bright));       // 100 >= 80

        var boundary = new GrayImage(320, 240); boundary.Fill(80);
        Assert.False(VisionSystem.IsDarkForClahe(boundary));     // exactly 80 clears the flag

        var dark = new GrayImage(320, 240); dark.Fill(50);
        Assert.True(VisionSystem.IsDarkForClahe(dark));          // 50 < 80
    }
}