using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The renderer measured against the constants and invariants recovered from
/// <c>libcozmoEngine.so</c> — see <c>re-analysis/PROCEDURAL_FACE.md</c>.
///
/// These assert numbers, not appearances. The previous face tests only checked that two separate
/// blobs appeared, which passed equally well with the invented 28x28 eyes at x = 40 and 88 as with
/// the engine's 30x40 eyes at x = 32 and 96 — so they could not have caught the geometry being wrong.
///
/// One thing to keep in mind reading the row arithmetic below: the canvas is 64 rows and the bitmap is
/// 32, so a bitmap row <c>r</c> is canvas row <c>2r</c>, and a vertical distance in the bitmap is half
/// the distance in eye parameters.
/// </summary>
public class ProceduralFaceRendererTests
{
    private static (int MinX, int MaxX, int MinY, int MaxY)? Box(FaceBitmap bmp, int fromX = 0, int toX = FaceBitmap.Width - 1)
    {
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = fromX; x <= toX; x++)
                if (bmp[x, y] != 0)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        return minX == int.MaxValue ? null : (minX, maxX, minY, maxY);
    }

    private static int Lit(FaceBitmap bmp)
    {
        int n = 0;
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = 0; x < FaceBitmap.Width; x++)
                if (bmp[x, y] != 0) n++;
        return n;
    }

    private static ProceduralFacePose Neutral() => ProceduralFaceRenderer.Nominal();

    // ---------------------------------------------------------------- the canvas and (64, 32)

    /// <summary>
    /// The whole point of the (64, 32) question: the transform centre is the centre of a 128x64
    /// drawing canvas, and the robot's 32-row bitmap is that canvas with alternate rows dropped.
    /// </summary>
    [Fact]
    public void TheCanvasIsTwiceTheBitmapAndItsCentreIsTheTransformCentre()
    {
        Assert.Equal(FaceBitmap.Width, ProceduralFaceRenderer.CanvasWidth);
        Assert.Equal(FaceBitmap.Height * 2, ProceduralFaceRenderer.CanvasHeight);
        Assert.Equal(ProceduralFaceRenderer.CanvasWidth / 2f, ProceduralFaceRenderer.FaceCentreX);
        Assert.Equal(ProceduralFaceRenderer.CanvasHeight / 2f, ProceduralFaceRenderer.FaceCentreY);
    }

    /// <summary>The constants recovered from the binary, asserted as constants so they cannot drift.</summary>
    [Fact]
    public void TheRecoveredConstantsAreWhatTheBinaryHolds()
    {
        Assert.Equal(30f, ProceduralFaceRenderer.NominalEyeWidth);      // vmov.f32 s20, #3.0e+01
        Assert.Equal(40f, ProceduralFaceRenderer.NominalEyeHeight);     // literal at 0x005853A8
        Assert.Equal(15f, ProceduralFaceRenderer.EyeHalfWidth);         // vmov.f32 s18, #1.5e+01
        Assert.Equal(20f, ProceduralFaceRenderer.EyeHalfHeight);
        Assert.Equal(32f, ProceduralFaceRenderer.LeftEyeCenterX);       // table at 0x005859DC, entry 1
        Assert.Equal(96f, ProceduralFaceRenderer.RightEyeCenterX);      // table at 0x005859DC, entry 0
        Assert.Equal(32f, ProceduralFaceRenderer.EyeCenterY);           // centreY = EyeCenterY + 32.0
        Assert.Equal(10, ProceduralFaceRenderer.ArcDeltaDegrees);       // every ellipse2Poly delta
    }

    // ---------------------------------------------------------------- nominal geometry

    /// <summary>
    /// A neutral eye spans x = 32 ± 15 and y = 32 ± 20. The shipped fillConvexPoly LINE_4 draws the edges themselves
    /// (M5 gap3 A4, A8), so both edge columns are lit: 31 columns, 17..47. The lids at LidY 0 cover the top and bottom
    /// rows (y −21..−20 and 20..21, gap1 D2, D3) and the even rows are cleared (E2), so rows 13..51 odd remain: 20 bitmap
    /// rows.
    /// </summary>
    [Fact]
    public void ANeutralEyeIsThirtyOneWideAndFortyTall()
    {
        var bmp = ProceduralFaceRenderer.Render(Neutral());
        var left = Box(bmp, 0, 63);
        Assert.NotNull(left);
        var (minX, maxX, minY, maxY) = left!.Value;

        Assert.Equal(31, maxX - minX + 1);
        Assert.Equal(20, maxY - minY + 1);
    }

    /// <summary>
    /// The eyes sit at x = 32 and x = 96, so their centres are 64 px apart. The invented constants put
    /// them at 40 and 88 — a separation of 48 — which is why a face-wide stretch ran them together.
    /// </summary>
    [Fact]
    public void TheEyesAreSixtyFourPixelsApartAtThirtyTwoAndNinetySix()
    {
        var bmp = ProceduralFaceRenderer.Render(Neutral());
        var left = Box(bmp, 0, 63)!.Value;
        var right = Box(bmp, 64, 127)!.Value;

        float leftCentre = (left.MinX + left.MaxX + 1) / 2f;
        float rightCentre = (right.MinX + right.MaxX + 1) / 2f;

        Assert.Equal(ProceduralFaceRenderer.LeftEyeCenterX, leftCentre, 0);
        Assert.Equal(ProceduralFaceRenderer.RightEyeCenterX, rightCentre, 0);
        Assert.Equal(64f, rightCentre - leftCentre, 0);
    }

    /// <summary>
    /// The vertical centre is 32 on the canvas, which is the middle of the bitmap: an eye is centred,
    /// not sitting against an edge.
    /// </summary>
    [Fact]
    public void AnEyeIsCentredVerticallyOnTheCanvas()
    {
        var bmp = ProceduralFaceRenderer.Render(Neutral());
        var left = Box(bmp, 0, 63)!.Value;
        float centreRow = (left.MinY + left.MaxY + 1) / 2f;
        Assert.Equal(ProceduralFaceRenderer.EyeCenterY / 2f, centreRow, 0);
    }

    // ---------------------------------------------------------------- EyeCenter units

    /// <summary>
    /// <c>EyeCenterX</c> is a pixel offset added straight onto the base centre, so moving it by n moves
    /// the eye by exactly n columns. Nothing scales it.
    /// </summary>
    [Theory]
    [InlineData(5f)]
    [InlineData(-7f)]
    public void EyeCentreXIsInCanvasPixels(float dx)
    {
        var baseBox = Box(ProceduralFaceRenderer.Render(Neutral()), 0, 63)!.Value;

        var pose = Neutral();
        pose.Left[EyeParam.EyeCenterX] = dx;
        var movedBox = Box(ProceduralFaceRenderer.Render(pose), 0, 63)!.Value;

        Assert.Equal((int)dx, movedBox.MinX - baseBox.MinX);
        Assert.Equal((int)dx, movedBox.MaxX - baseBox.MaxX);
    }

    /// <summary>
    /// <c>EyeCenterY</c> is in the same canvas pixels, which is why a shift of 4 moves the eye by only
    /// 2 bitmap rows: the bitmap keeps every other canvas row.
    /// </summary>
    [Fact]
    public void EyeCentreYIsInCanvasPixelsSoTheBitmapMovesByHalf()
    {
        var baseBox = Box(ProceduralFaceRenderer.Render(Neutral()), 0, 63)!.Value;

        var pose = Neutral();
        pose.Left[EyeParam.EyeCenterY] = 4f;
        var movedBox = Box(ProceduralFaceRenderer.Render(pose), 0, 63)!.Value;

        Assert.Equal(2, movedBox.MinY - baseBox.MinY);
    }

    // ---------------------------------------------------------------- EyeScale and EyeAngle

    /// <summary>
    /// The per-eye matrix turns about (0, 0) and only then translates, so <c>EyeScaleX</c> stretches the
    /// eye about its own centre and leaves that centre where it was.
    ///
    /// A half scale takes the edges to 32 +/- 7.5, and the engine rounds every transformed point with
    /// <c>roundf</c> — half away from zero — giving 25 and 40 rather than a symmetric pair; the LINE_4 edges are lit
    /// (gap3 A4, A8), so the eye covers columns 25..40: 16 columns, centred at 33.
    /// </summary>
    [Fact]
    public void EyeScaleStretchesAboutTheEyesOwnCentre()
    {
        var pose = Neutral();
        pose.Left[EyeParam.EyeScaleX] = 0.5f;
        var box = Box(ProceduralFaceRenderer.Render(pose), 0, 63)!.Value;

        Assert.Equal((25, 40), (box.MinX, box.MaxX));
        Assert.Equal(33f, (box.MinX + box.MaxX + 1) / 2f);
    }

    /// <summary>
    /// <c>EyeAngle</c> spins the eye in place. A square-ish eye rotated 45 degrees has a wider bounding
    /// box than one that is not, but the same centre.
    /// </summary>
    [Fact]
    public void EyeAngleRotatesTheEyeAboutItsOwnCentre()
    {
        var pose = Neutral();
        pose.Left[EyeParam.EyeAngle] = 45f;
        var box = Box(ProceduralFaceRenderer.Render(pose), 0, 63)!.Value;

        Assert.True(box.MaxX - box.MinX + 1 > 30,
            "a 30x40 eye rotated 45 degrees must have a bounding box wider than 30");
        Assert.Equal(ProceduralFaceRenderer.LeftEyeCenterX, (box.MinX + box.MaxX + 1) / 2f, 0);
    }

    // ---------------------------------------------------------------- lids

    /// <summary>
    /// <c>LidY</c> is a fraction of the full 40 px height, so 1.0 closes the eye completely. Using the
    /// half height instead would leave the bottom half of the eye showing.
    /// </summary>
    [Theory]
    [InlineData(EyeParam.UpperLidY)]
    [InlineData(EyeParam.LowerLidY)]
    public void ALidAtOneClosesTheEyeCompletely(EyeParam lid)
    {
        var pose = Neutral();
        pose.Left[lid] = 1f;
        pose.Right[lid] = 1f;
        Assert.Equal(0, Lit(ProceduralFaceRenderer.Render(pose)));
    }

    /// <summary>
    /// Half way down, the upper lid removes the top half: 40 canvas rows become 20, which is 10 bitmap
    /// rows, and it is the top that goes.
    /// </summary>
    [Fact]
    public void TheUpperLidCutsDownFromTheTopByLidYTimesFortyPixels()
    {
        var open = Box(ProceduralFaceRenderer.Render(Neutral()), 0, 63)!.Value;

        var pose = Neutral();
        pose.Left[EyeParam.UpperLidY] = 0.5f;
        var shut = Box(ProceduralFaceRenderer.Render(pose), 0, 63)!.Value;

        Assert.Equal(10, shut.MaxY - shut.MinY + 1);        // 20 canvas rows
        Assert.Equal(open.MaxY, shut.MaxY);                 // the bottom edge has not moved
        Assert.Equal(10, shut.MinY - open.MinY);            // the top came down by 20 canvas rows
    }

    /// <summary>And the lower lid does the same from below, leaving the top edge alone.</summary>
    [Fact]
    public void TheLowerLidCutsUpFromTheBottom()
    {
        var open = Box(ProceduralFaceRenderer.Render(Neutral()), 0, 63)!.Value;

        var pose = Neutral();
        pose.Left[EyeParam.LowerLidY] = 0.5f;
        var shut = Box(ProceduralFaceRenderer.Render(pose), 0, 63)!.Value;

        Assert.Equal(10, shut.MaxY - shut.MinY + 1);
        Assert.Equal(open.MinY, shut.MinY);
        Assert.Equal(10, open.MaxY - shut.MaxY);
    }

    // ---------------------------------------------------------------- corners and the mirror

    /// <summary>
    /// A corner radius below one pixel is drawn sharp — the engine pushes the box corner rather than
    /// calling <c>ellipse2Poly</c> — so the corner pixel itself is lit. At the neutral 0.5 it is not.
    ///
    /// The corner tested is the left eye's upper inner one, which after the mirror is at local
    /// <c>(+15, -20)</c>: canvas column 46, canvas row 12, which is bitmap row 6.
    /// </summary>
    [Fact]
    public void AZeroRadiusGivesASharpCornerAndANeutralRadiusDoesNot()
    {
        var sharp = Neutral();
        sharp.Left[EyeParam.UpperInnerRadiusX] = 0f;
        sharp.Left[EyeParam.UpperInnerRadiusY] = 0f;

        Assert.NotEqual(0, ProceduralFaceRenderer.Render(sharp)[46, 6]);
        Assert.Equal(0, ProceduralFaceRenderer.Render(Neutral())[46, 6]);
    }

    /// <summary>
    /// The second eye is the first one mirrored: <c>if (whichEye != 0) x = -x</c>. So the same "inner"
    /// parameter puts a sharp corner on the right-hand side of the left eye and the left-hand side of
    /// the right eye — both towards the middle of the face.
    /// </summary>
    [Fact]
    public void TheSecondEyeIsTheFirstMirroredSoInnerMeansTheSameOnBoth()
    {
        var pose = Neutral();
        foreach (var eye in new[] { pose.Left, pose.Right })
        {
            eye[EyeParam.UpperInnerRadiusX] = 0f;
            eye[EyeParam.UpperInnerRadiusY] = 0f;
        }
        var bmp = ProceduralFaceRenderer.Render(pose);

        // Left eye's inner corner is at canvas 32 + 15 = 47, so column 46 is the last lit one.
        Assert.NotEqual(0, bmp[46, 6]);
        // Right eye's inner corner is mirrored to canvas 96 - 15 = 81.
        Assert.NotEqual(0, bmp[81, 6]);

        // The outer corners are still rounded, so their corner pixels are dark.
        Assert.Equal(0, bmp[17, 6]);
        Assert.Equal(0, bmp[110, 6]);
    }

    // ---------------------------------------------------------------- the whole-face transform

    /// <summary>
    /// The matrix is built about (64, 32), so the face centre maps to itself plus the translation. With
    /// no translation and a 180 degree angle the face is point-reflected: the eye on the right appears
    /// on the left. Closing one eye makes which is which unambiguous.
    /// </summary>
    [Fact]
    public void AHalfTurnAboutTheFaceCentreSwapsTheEyes()
    {
        var pose = Neutral();
        pose.Left[EyeParam.UpperLidY] = 1f;             // only the right eye is drawn
        Assert.Null(Box(ProceduralFaceRenderer.Render(pose), 0, 63));
        Assert.NotNull(Box(ProceduralFaceRenderer.Render(pose), 64, 127));

        pose.FaceAngle = 180f;
        var turned = ProceduralFaceRenderer.Render(pose);
        Assert.NotNull(Box(turned, 0, 63));
        Assert.Null(Box(turned, 64, 127));
    }

    /// <summary>
    /// A whole-face scale moves eye positions as well as stretching them, because the matrix has the
    /// centre terms. The eye centres are 32 px either side of the face centre, so scaling by s puts them
    /// at 64 -/+ 32s and their separation becomes 64s.
    ///
    /// 1.25 is used rather than a rounder number because the panel is only 128 wide: past about 1.36 the
    /// outer edge of each eye runs off the side, and a clipped eye no longer reports its true centre.
    /// </summary>
    [Fact]
    public void AWholeFaceScaleMovesTheEyeCentresAboutTheFaceCentre()
    {
        var pose = Neutral();
        pose.FaceScaleX = 1.25f;
        var bmp = ProceduralFaceRenderer.Render(pose);

        var left = Box(bmp, 0, 63)!.Value;
        var right = Box(bmp, 64, 127)!.Value;
        float leftCentre = (left.MinX + left.MaxX + 1) / 2f;
        float rightCentre = (right.MinX + right.MaxX + 1) / 2f;

        Assert.Equal(64f - 32f * 1.25f, leftCentre, 0);     // 24
        Assert.Equal(64f + 32f * 1.25f, rightCentre, 0);    // 104
        Assert.Equal(64f * 1.25f, rightCentre - leftCentre, 0);

        // And each eye is 1.25x wider. The source eye covers columns 17..47; warpAffine INTER_NEAREST (gap3 C2..C6) maps
        // dst x to src (13619 + cvRound(819.2 x)) >> 10, which lands in 17..47 for x = 5..43: 39 columns.
        Assert.Equal((5, 43), (left.MinX, left.MaxX));
    }

    /// <summary>
    /// <c>FaceCenterX/Y</c> is a translation in canvas pixels, added after the scale-about-centre terms.
    /// </summary>
    [Fact]
    public void FaceCentreTranslatesInCanvasPixels()
    {
        var baseBox = Box(ProceduralFaceRenderer.Render(Neutral()), 0, 63)!.Value;

        var pose = Neutral();
        pose.FaceCenterX = 6f;
        var moved = Box(ProceduralFaceRenderer.Render(pose), 0, 63)!.Value;
        Assert.Equal(6, moved.MinX - baseBox.MinX);
    }

    /// <summary>A face scaled to nothing in either axis has no area, so it draws nothing.</summary>
    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    public void ADegenerateScaleDrawsNothing(float sx, float sy)
    {
        var pose = Neutral();
        pose.FaceScaleX = sx;
        pose.FaceScaleY = sy;
        Assert.Equal(0, Lit(ProceduralFaceRenderer.Render(pose)));
    }

    // ---------------------------------------------------------------- the stress case

    /// <summary>
    /// <c>anim_reacttocliff_pickup_01</c> holds the most extreme whole-face transforms in the library,
    /// and this is the pose that merged the eyes into one block on hardware.
    ///
    /// The assertion is arithmetic rather than visual. At <c>FaceScaleX = 1.82</c> the eye centres move
    /// to 64 +/- 32*1.82, that is 5.76 and 122.24, and each eye becomes 30*1.82 = 54.6 wide. So the
    /// inner edge of the left eye lands near 33 and the inner edge of the right near 95, and the middle
    /// of the panel must be empty. With the eyes only 48 apart and 28 wide, as they were, the two spans
    /// overlapped across the centre instead.
    ///
    /// Nothing here is tuned to this clip: the numbers are the recovered constants put through the
    /// recovered matrix.
    /// </summary>
    [Fact]
    public void TheExtremeStretchLeavesTheMiddleOfThePanelEmpty()
    {
        var pose = Neutral();
        pose.FaceScaleX = 1.82f;
        pose.FaceScaleY = 0.07f;
        var bmp = ProceduralFaceRenderer.Render(pose);

        Assert.True(Lit(bmp) > 0, "the pose rendered nothing at all");

        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = 40; x <= 88; x++)
                Assert.Equal(0, bmp[x, y]);
    }

    /// <summary>
    /// The other extreme the same clip holds. At 1.24 the centres move to 24.3 and 103.7 and each eye is
    /// 37.2 wide, so the gap is narrower but still open across the middle.
    /// </summary>
    [Fact]
    public void TheOtherExtremeStretchAlsoLeavesTheMiddleEmpty()
    {
        var pose = Neutral();
        pose.FaceScaleX = 1.24f;
        pose.FaceScaleY = 0.07f;
        var bmp = ProceduralFaceRenderer.Render(pose);

        Assert.True(Lit(bmp) > 0);
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = 50; x <= 78; x++)
                Assert.Equal(0, bmp[x, y]);
    }

    /// <summary>
    /// A squash to 0.07 leaves the face 40*0.07 = 2.8 canvas rows tall about row 32, so at most two
    /// bitmap rows can light and they must be at the middle of the panel. Before this reconstruction the
    /// whole thing collapsed onto one band at the wrong height.
    /// </summary>
    [Fact]
    public void AnExtremeSquashCollapsesToTheMiddleRowsOnly()
    {
        var pose = Neutral();
        pose.FaceScaleY = 0.07f;
        var box = Box(ProceduralFaceRenderer.Render(pose))!.Value;

        Assert.True(box.MaxY - box.MinY + 1 <= 2,
            $"a 0.07 squash lit {box.MaxY - box.MinY + 1} rows; 2.8 canvas rows can reach at most 2");
        Assert.InRange(box.MinY, 15, 16);
    }

    /// <summary>
    /// The clip alternates between these poses repeatedly, so the same pose must render identically
    /// every time with no state carried between calls.
    /// </summary>
    [Fact]
    public void RenderingIsPureAcrossRepeatedPoses()
    {
        var poses = new[] { (1.24f, 0.07f), (1.82f, 0.07f), (1f, 1f) };
        var first = new List<int>();
        for (int pass = 0; pass < 3; pass++)
        {
            int i = 0;
            foreach (var (sx, sy) in poses)
            {
                var pose = Neutral();
                pose.FaceScaleX = sx;
                pose.FaceScaleY = sy;
                int lit = Lit(ProceduralFaceRenderer.Render(pose));
                if (pass == 0) first.Add(lit);
                else Assert.Equal(first[i], lit);
                i++;
            }
        }
    }
}
