using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M11-020: the shipped OpenCV 3.1.0 boxFilter/subtract/normalize routines, transcribed into
/// <see cref="OpenCv310"/>. Every expected value here is hand-derived from the citations in
/// <c>.scratch/B-M11/opencv-report.md</c> (S5/S8/S9/S10/S16/S18/S20/S23/S24/S25), never from the code.
/// </summary>
public class OpenCv310M11Tests
{
    // ------------------------------------------------------------------ borderInterpolate (S25)

    [Fact]
    public void Reflect101BorderInterpolation()
    {
        // p=-1 -> 1, p=-2 -> 2, p=len -> len-2, p=len+1 -> len-3; in range unchanged; len 1 -> 0
        Assert.Equal(1, OpenCv310.BorderInterpolate(-1, 10, OpenCv310.BorderReflect101));
        Assert.Equal(2, OpenCv310.BorderInterpolate(-2, 10, OpenCv310.BorderReflect101));
        Assert.Equal(8, OpenCv310.BorderInterpolate(10, 10, OpenCv310.BorderReflect101));
        Assert.Equal(7, OpenCv310.BorderInterpolate(11, 10, OpenCv310.BorderReflect101));
        Assert.Equal(5, OpenCv310.BorderInterpolate(5, 10, OpenCv310.BorderReflect101));
        Assert.Equal(0, OpenCv310.BorderInterpolate(-3, 1, OpenCv310.BorderReflect101));
    }

    // ------------------------------------------------------------------ rounding primitives (S9/S23/S24)

    [Fact]
    public void TheNeonRoundingIsTiesAwayFromZero()
    {
        // cv_vrndq_s32_f32 = (int32)(v + copysign(0.5, v)), truncate
        Assert.Equal(3, OpenCv310.RoundHalfAwayFromZero(2.5f));
        Assert.Equal(-3, OpenCv310.RoundHalfAwayFromZero(-2.5f));
        Assert.Equal(2, OpenCv310.RoundHalfAwayFromZero(2.4f));
        Assert.Equal(-2, OpenCv310.RoundHalfAwayFromZero(-2.4f));
        // CvRound stays ties-to-even (the scalar tail): 2.5 -> 2, 3.5 -> 4
        Assert.Equal(2, OpenCv310.CvRound(2.5));
        Assert.Equal(4, OpenCv310.CvRound(3.5));
    }

    // ------------------------------------------------------------------ subtract (S16/S18)

    [Fact]
    public void SubtractSaturatesTheIntDifferenceToShort()
    {
        var src1 = new byte[] { 0, 255, 10, 0 };
        var src2 = new short[] { -32768, -5, 30000, 32767 };
        var dst = new short[4];
        OpenCv310.Subtract8U16STo16S(src1, src2, dst, 4);
        Assert.Equal(32767, dst[0]);    // 0 - (-32768) = 32768 -> saturate
        Assert.Equal(260, dst[1]);      // 255 - (-5)
        Assert.Equal(-29990, dst[2]);   // 10 - 30000
        Assert.Equal(-32767, dst[3]);   // 0 - 32767
    }

    // ------------------------------------------------------------------ boxFilter column scale (S8/S10)

    /// <summary>
    /// The column scale's NEON 8-wide block uses ties-away (<c>cv_vrndq_s32_f32</c>, 0x000AACD8) and the
    /// scalar tail ties-even (<c>vcvtr.s32.f64</c>, 0x000AAD70). With scale 0.5, <c>249*0.5 = 124.5</c>:
    /// column 0 (NEON) -> 125, columns 9 and 10 (tail) -> 124. A width that is a multiple of 8 has no tail,
    /// so column 15 is NEON and gives 125.
    /// </summary>
    [Fact]
    public void TheColumnScaleUsesTiesAwayOnTheEightWideBlockAndTiesEvenOnTheTail()
    {
        var rows11 = new[] { new int[] { 249, 0, 0, 0, 0, 0, 0, 0, 0, 249, 249 } };
        var dst11 = new short[11];
        OpenCv310.ColumnSum32STo16S(rows11, 0.5, dst11, 0, 11);
        Assert.Equal(125, dst11[0]);    // NEON block
        Assert.Equal(124, dst11[9]);    // scalar tail
        Assert.Equal(124, dst11[10]);   // scalar tail

        var rows16 = new[] { new int[16] };
        rows16[0][15] = 249;
        var dst16 = new short[16];
        OpenCv310.ColumnSum32STo16S(rows16, 0.5, dst16, 0, 16);
        Assert.Equal(125, dst16[15]);   // full block: NEON
    }

    // ------------------------------------------------------------------ normalize (S20/S23/S24)

    /// <summary>
    /// NORM_MINMAX maps smin-&gt;0 and smax-&gt;255. With a range of 170 the scale is 1.5, so <c>83*1.5 = 124.5</c>:
    /// the NEON block (<c>+0.5</c> then truncate, S23) gives 125; the scalar tail (<c>vcvtr</c>, ties even, S24)
    /// gives 124. A length that is a multiple of 8 has no tail, so index 15 is NEON and gives 125.
    /// </summary>
    [Fact]
    public void NormalizeUsesTiesUpOnTheEightWideBlockAndTiesEvenOnTheTail()
    {
        var src11 = new short[] { 83, 0, 83, 83, 83, 83, 83, 83, 170, 83, 83 };
        var dst11 = new byte[11];
        OpenCv310.NormalizeMinMax16STo8U(src11, dst11, 11, 255.0, 0.0);
        Assert.Equal((byte)125, dst11[0]);   // NEON block
        Assert.Equal((byte)255, dst11[8]);   // smax -> 255
        Assert.Equal((byte)124, dst11[9]);   // scalar tail
        Assert.Equal((byte)124, dst11[10]);  // scalar tail

        var src16 = new short[16];
        src16[1] = 170; src16[15] = 83;
        var dst16 = new byte[16];
        OpenCv310.NormalizeMinMax16STo8U(src16, dst16, 16, 255.0, 0.0);
        Assert.Equal((byte)125, dst16[15]);  // full block: NEON
    }

    // ------------------------------------------------------------------ boxFilter (S1..S10)

    /// <summary>
    /// A 3x3 image with 9 at its centre, a 3x3 box, BORDER_REFLECT_101 on the parent image. The top-left
    /// output box covers rows/cols {-1,0,1} -> {1,0,1}, so the centre pixel appears 4 times: sum 36,
    /// scale 1/9, result 4 (S5/S25).
    /// </summary>
    [Fact]
    public void BoxFilterReflectsTheBorderAgainstTheParentImage()
    {
        var whole = new byte[] { 0, 0, 0, 0, 9, 0, 0, 0, 0 };
        var blur = OpenCv310.BoxFilter8UTo16S(whole, 3, 3, 0, 0, 3, 3, 3, 3, OpenCv310.BorderReflect101);
        Assert.Equal((short)4, blur[0]);
        // the centre output box is the whole image: sum 9, scale 1/9 -> 1
        Assert.Equal((short)1, blur[4]);
    }
}