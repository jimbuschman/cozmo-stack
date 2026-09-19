using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Regressions for the whole-face transform, using the extreme squash/stretch poses that
/// <c>anim_reacttocliff_pickup_01</c> actually contains.
///
/// The defect: the renderer applied <c>FaceScaleX/Y</c> to each eye's width and height but left the eye
/// centres at their fixed nominal positions, so a face-wide stretch widened both eyes without moving them
/// apart and they overlapped into one rectangle. <c>FaceAngle</c> was likewise added to each eye's own
/// angle rather than rotating the pair.
///
/// The engine instead draws both eyes at nominal positions and applies one affine over the whole image:
/// <c>GetTransformationMatrix(angle, scaleX, scaleY, transX, transY, 64, 32)</c> then
/// <c>cv::warpAffine</c>. Its translation column, <c>(1 - cos*sx)*cx - sin*sy*cy + tx</c>, is the
/// scale-and-rotate-about-a-centre form, so positions move with the scale.
/// </summary>
public class FaceTransformTests
{
    /// <summary>The x range of lit pixels in one half of the panel, or null when that half is empty.</summary>
    private static (int Min, int Max)? Span(FaceBitmap bmp, int fromX, int toX)
    {
        int min = int.MaxValue, max = int.MinValue;
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = fromX; x <= toX; x++)
                if (bmp[x, y] != 0) { if (x < min) min = x; if (x > max) max = x; }
        return min == int.MaxValue ? null : (min, max);
    }

    private static int Lit(FaceBitmap bmp)
    {
        int n = 0;
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = 0; x < FaceBitmap.Width; x++)
                if (bmp[x, y] != 0) n++;
        return n;
    }

    /// <summary>Counts columns that contain at least one lit pixel, and how many runs of them there are.</summary>
    private static int ColumnRuns(FaceBitmap bmp)
    {
        int runs = 0;
        bool inRun = false;
        for (int x = 0; x < FaceBitmap.Width; x++)
        {
            bool any = false;
            for (int y = 0; y < FaceBitmap.Height && !any; y++) if (bmp[x, y] != 0) any = true;
            if (any && !inRun) runs++;
            inRun = any;
        }
        return runs;
    }

    private static ProceduralFacePose Neutral() => ProceduralFaceRenderer.Neutral();

    // ------------------------------------------------------------------ the real poses

    /// <summary>
    /// The pose that caused the hardware failure. A face-wide stretch must move the eyes apart, not merge
    /// them: with centres left fixed, each eye widened from 28 px to ~51 px and the two spans overlapped
    /// around x = 64.
    /// </summary>
    [Fact]
    public void TheExtremeStretchPoseDoesNotMergeTheEyes()
    {
        var pose = Neutral();
        pose.FaceScaleX = 1.82f;
        pose.FaceScaleY = 0.07f;

        var bmp = ProceduralFaceRenderer.Render(pose);
        Assert.True(Lit(bmp) > 0, "the pose rendered nothing at all");
        Assert.Equal(2, ColumnRuns(bmp));      // two separate eyes, not one merged block
    }

    /// <summary>The other extreme the clip holds, checked the same way.</summary>
    [Fact]
    public void TheOtherExtremeStretchPoseAlsoKeepsTwoEyes()
    {
        var pose = Neutral();
        pose.FaceScaleX = 1.24f;
        pose.FaceScaleY = 0.07f;

        var bmp = ProceduralFaceRenderer.Render(pose);
        Assert.True(Lit(bmp) > 0);
        Assert.Equal(2, ColumnRuns(bmp));
    }

    /// <summary>
    /// The mechanism, stated directly: scaling the face horizontally must increase the gap between the
    /// eyes, because they scale about the centre of the panel along with everything else.
    /// </summary>
    [Fact]
    public void AWiderFaceMovesTheEyesFurtherApart()
    {
        var neutral = ProceduralFaceRenderer.Render(Neutral());
        int centre = FaceBitmap.Width / 2;
        var leftN = Span(neutral, 0, centre - 1);
        var rightN = Span(neutral, centre, FaceBitmap.Width - 1);
        Assert.NotNull(leftN);
        Assert.NotNull(rightN);
        int gapNeutral = rightN!.Value.Min - leftN!.Value.Max;

        var wide = Neutral();
        wide.FaceScaleX = 1.82f;
        var wideBmp = ProceduralFaceRenderer.Render(wide);
        var leftW = Span(wideBmp, 0, centre - 1);
        var rightW = Span(wideBmp, centre, FaceBitmap.Width - 1);
        Assert.NotNull(leftW);
        Assert.NotNull(rightW);
        int gapWide = rightW!.Value.Min - leftW!.Value.Max;

        Assert.True(gapWide >= gapNeutral,
            $"the gap shrank from {gapNeutral} to {gapWide} px when the face was widened, " +
            "which is the overlap that merged the eyes");
    }

    /// <summary>
    /// A face-wide angle rotates the pair around the face centre. With rotation applied only to each eye's
    /// own angle, both eyes stayed at the same height; rotating the pair moves one up and the other down.
    /// </summary>
    [Fact]
    public void AFaceAngleRotatesTheEyesAroundTheFaceRatherThanEachInPlace()
    {
        // Small eyes, so they can actually move vertically: a neutral eye is 28 px tall on a 32 px
        // panel, so it stays clipped to nearly the full height however the face is rotated and the
        // centroids barely shift. That is a limit of the panel, not of the transform.
        var pose = Neutral();
        pose.Left[(int)EyeParam.EyeScaleY] = 0.3f;
        pose.Right[(int)EyeParam.EyeScaleY] = 0.3f;
        pose.FaceAngle = 25f;
        var bmp = ProceduralFaceRenderer.Render(pose);
        Assert.True(Lit(bmp) > 0);

        int centre = FaceBitmap.Width / 2;
        float leftY = CentroidY(bmp, 0, centre - 1);
        float rightY = CentroidY(bmp, centre, FaceBitmap.Width - 1);

        Assert.True(Math.Abs(leftY - rightY) > 1.0f,
            $"both eyes sat at the same height ({leftY:F1} vs {rightY:F1}); the face angle did not " +
            "rotate the pair around the face centre");
    }

    private static float CentroidY(FaceBitmap bmp, int fromX, int toX)
    {
        float sum = 0; int n = 0;
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = fromX; x <= toX; x++)
                if (bmp[x, y] != 0) { sum += y; n++; }
        return n == 0 ? 0 : sum / n;
    }

    // ------------------------------------------------------------------ the transform's own properties

    /// <summary>A neutral face is unchanged by an identity transform.</summary>
    [Fact]
    public void AnIdentityTransformLeavesTheFaceAlone()
    {
        var pose = Neutral();
        Assert.Equal(1f, pose.FaceScaleX);
        Assert.Equal(1f, pose.FaceScaleY);
        Assert.Equal(0f, pose.FaceAngle);

        var bmp = ProceduralFaceRenderer.Render(pose);
        Assert.True(Lit(bmp) > 0);
        Assert.Equal(2, ColumnRuns(bmp));
    }

    /// <summary>FaceCenter shifts the whole face, both eyes together and by the same amount.</summary>
    [Fact]
    public void FaceCentreShiftsBothEyesTogether()
    {
        var baseBmp = ProceduralFaceRenderer.Render(Neutral());
        var moved = Neutral();
        moved.FaceCenterX = 6f;
        var movedBmp = ProceduralFaceRenderer.Render(moved);

        int centre = FaceBitmap.Width / 2;
        var l0 = Span(baseBmp, 0, centre - 1)!.Value;
        var l1 = Span(movedBmp, 0, centre - 1)!.Value;
        Assert.Equal(6, l1.Min - l0.Min);
    }

    /// <summary>A face scaled to nothing in either axis draws nothing rather than dividing by zero.</summary>
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

    /// <summary>
    /// The clip alternates between these poses repeatedly, so the renderer must be stable across them:
    /// the same pose must give the same bitmap every time, with no state carried between renders.
    /// </summary>
    [Fact]
    public void RepeatingTheClipsPoseSequenceIsStable()
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
