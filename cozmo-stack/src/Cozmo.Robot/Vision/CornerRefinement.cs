namespace Cozmo.Robot.Vision;

/// <summary>
/// The engine's sub-pixel corner refinement: <c>VisionMarker::RefineCorners</c> 0x0089FD98, which measures the
/// marker's bright and dark levels with <c>VisionMarker::ComputeBrightDarkValues</c> 0x0089F8E8 and then runs
/// <c>Anki::Embedded::RefineQuadrilateral</c> 0x008C55E0 (quadRefinement.cpp), an inverse-compositional
/// Gauss-Newton refinement of the marker homography against edge samples of the canonical square.
///
/// Arithmetic is single precision throughout, as in the engine. Corners and homographies are in the
/// <see cref="Homography.FromUnitSquare"/> convention: unit-square corners (0,0), (0,1), (1,0), (1,1).
/// </summary>
public static class CornerRefinement
{
    /// <summary>What <c>RefineCorners</c> reports.</summary>
    public enum Outcome
    {
        /// <summary>Refined (or, when the refined quad is unreasonable, the original corners kept).</summary>
        Refined,
        /// <summary><c>ComputeBrightDarkValues</c> found too little contrast: validity 2 (0x0089FECA).</summary>
        LowContrast,
        /// <summary><c>RefineQuadrilateral</c> returned non-zero: the corners moved more than the maximum (0x008C645A).</summary>
        Failed,
    }

    /// <summary>
    /// <c>VisionMarker::RefineCorners</c> 0x0089FD98. <paramref name="corners"/> and <paramref name="h"/> are the
    /// marker's quad and homography; both are replaced by the refined ones.
    /// </summary>
    public static Outcome RefineCorners(GrayImage img, MarkerLibrary lib, ref Vec2[] corners, ref Homography h, QuadDetectorParameters p)
    {
        var (bright, dark, valid) = ComputeBrightDarkValues(img, lib, h, (float)p.MinContrastRatio);
        if (!valid) return Outcome.LowContrast;
        if (p.RefinementIterations < 1) return Outcome.Refined;       // 0x0089FE52: the homography is kept as it is

        var original = (Vec2[])corners.Clone();
        var quad = new float[8];
        for (int i = 0; i < 4; i++) { quad[2 * i] = (float)corners[i].X; quad[2 * i + 1] = (float)corners[i].Y; }
        var hf = new float[9];
        for (int i = 0; i < 9; i++) hf[i] = (float)h.H[i];

        int result = RefineQuadrilateral(quad, hf, img,
            (float)p.RefineInnerFraction, (float)p.RefineInnerFraction, (float)p.RefinePaddingFraction, (float)p.RefinePaddingFraction,
            p.RefinementIterations, bright, dark, p.RefinementSamples, (float)p.MaxCornerChange, (float)p.MinCornerChange,
            out var refinedQuad, out var refinedH);

        var hd = new double[9];
        for (int i = 0; i < 9; i++) hd[i] = refinedH[i];
        h = new Homography(hd);
        var refined = new Vec2[4];
        for (int i = 0; i < 4; i++) refined[i] = new Vec2(refinedQuad[2 * i], refinedQuad[2 * i + 1]);
        corners = refined;
        if (result != 0) return Outcome.Failed;

        // 0x0089FEDA..0x0089FFEA: the corners rounded half away from zero into a saturated s16 quad; when
        // IsQuadrilateralReasonable rejects it, the original corners are put back (the homography is not).
        var rounded = new Vec2[4];
        for (int i = 0; i < 4; i++) rounded[i] = new Vec2(RoundS16(refinedQuad[2 * i]), RoundS16(refinedQuad[2 * i + 1]));
        if (!QuadDetector.IsQuadrilateralReasonable(rounded, img.Width, img.Height, p)) corners = original;
        return Outcome.Refined;
    }

    private static short RoundS16(float v)
    {
        if (v < -32768f) return short.MinValue;
        if (v > 32767f) return short.MaxValue;
        return (short)(v > 0 ? MathF.Floor(v + 0.5f) : MathF.Ceiling(v - 0.5f));
    }

    private static int RoundHalfAway(float v) => (int)(v > 0 ? MathF.Floor(v + 0.5f) : MathF.Ceiling(v - 0.5f));

    /// <summary>
    /// <c>VisionMarker::ComputeBrightDarkValues</c> 0x0089F8E8. For each of the threshold probe pairs, the five
    /// probe offsets around the dark and the bright probe are mapped through the homography and read at the
    /// nearest pixel (half away from zero, 0x0089FB12..0x0089FC60); a pair whose bright mean is below
    /// <paramref name="minContrastRatio"/> times its dark mean ends the measurement as invalid, with that
    /// pair's means reported (0x0089FCA6..0x0089FCAE). Otherwise the means over every sample are reported,
    /// valid when bright exceeds the ratio times dark (0x0089FD26).
    /// </summary>
    public static (float Bright, float Dark, bool Valid) ComputeBrightDarkValues(GrayImage img, MarkerLibrary lib, Homography h, float minContrastRatio)
    {
        float scale = 1f / (1 << MarkerLibrary.NumFractionalBits);
        var hf = new float[9];
        for (int i = 0; i < 9; i++) hf[i] = (float)h.H[i];
        int np = MarkerLibrary.NumProbePoints;
        var ox = new float[np]; var oy = new float[np];
        for (int k = 0; k < np; k++) { ox[k] = scale * lib.ProbePointsX[k]; oy[k] = scale * lib.ProbePointsY[k]; }
        float inv = 1f / np;
        uint totalDark = 0, totalBright = 0;
        for (int t = 0; t < MarkerLibrary.NumThresholdProbes; t++)
        {
            float by = scale * lib.ThresholdBrightY[t], bx = scale * lib.ThresholdBrightX[t];
            float dy = scale * lib.ThresholdDarkY[t], dx = scale * lib.ThresholdDarkX[t];
            uint dark = 0, bright = 0;
            for (int k = 0; k < np; k++)
            {
                dark += Pixel(img, hf, dx + ox[k], dy + oy[k]);
                bright += Pixel(img, hf, bx + ox[k], by + oy[k]);
            }
            float darkMean = inv * dark, brightMean = inv * bright;
            if (brightMean < darkMean * minContrastRatio) return (brightMean, darkMean, false);
            totalDark += dark; totalBright += bright;
        }
        float all = 1f / (MarkerLibrary.NumThresholdProbes * np);
        float b = all * totalBright, d = all * totalDark;
        return (b, d, b > d * minContrastRatio);
    }

    private static byte Pixel(GrayImage img, float[] h, float x, float y)
    {
        float w = 1f / (h[6] * x + h[7] * y + h[8]);
        float u = (h[0] * x + h[1] * y + h[2]) * w;
        float v = (h[3] * x + h[4] * y + h[5]) * w;
        int col = RoundHalfAway(u), row = RoundHalfAway(v);
        return img[Math.Clamp(col, 0, img.Width - 1), Math.Clamp(row, 0, img.Height - 1)];
    }

    /// <summary>
    /// <c>Anki::Embedded::RefineQuadrilateral</c> 0x008C55E0. <paramref name="quad"/> is (x, y) for the four
    /// corners, <paramref name="initialH"/> row-major. Returns 0, or 1 when the refined corners moved more than
    /// <paramref name="maxCornerChange"/> from the initial ones (0x008C644A..0x008C6464).
    /// </summary>
    public static int RefineQuadrilateral(float[] quad, float[] initialH, GrayImage img,
        float innerX, float innerY, float padX, float padY, int iterations, float bright, float dark, int numSamples,
        float maxCornerChange, float minCornerChange, out float[] refinedQuad, out float[] refinedH)
    {
        // the longer diagonal, corner 0 to 3 or corner 1 to 2 (0x008C5620..0x008C56D8)
        float d1x = quad[0] - quad[6], d1y = quad[1] - quad[7];
        float d2x = quad[2] - quad[4], d2y = quad[3] - quad[5];
        float l1 = MathF.Sqrt(d1x * d1x + d1y * d1y), l2 = MathF.Sqrt(d2x * d2x + d2y * d2y);
        float diag = l1 <= l2 ? MathF.Sqrt(d2x * d2x + d2y * d2y) : MathF.Sqrt(d1x * d1x + d1y * d1y);

        // eight edge blocks of m samples each (0x008C570E..0x008C5C62)
        int m = (int)MathF.Ceiling(numSamples * 0.125f);
        int n = 8 * m;
        var xs = new float[n]; var ys = new float[n]; var tx = new float[n]; var ty = new float[n];
        float innerMaxX = MathF.Max(innerX, padX), innerMaxY = MathF.Max(innerY, padY);
        float denom = m - 1;
        // RefineCorners passes the dark level first and the bright second (sp+0x88 then sp+0x8C, which
        // ComputeBrightDarkValues fills as dark and bright), and the offset is second minus first
        // (0x008C583C): the template gradient runs dark to bright.
        float off = (bright - dark) / 255f * 0.5f * (diag / 1.4142135f);

        // outer top and bottom edges, y = 0 and y = 1
        float step = (padX * -2f + 1f) / denom, s = padX;
        for (int i = 0; i < m; i++)
        {
            xs[i] = s; xs[m + i] = s; ys[i] = 0f; ys[m + i] = 1f;
            tx[i] = 0f; tx[m + i] = 0f; ty[i] = -off; ty[m + i] = off;
            s = step + s;
        }
        if (padX == 0f) { tx[0] = -off; tx[m - 1] = off; tx[m] = -off; tx[2 * m - 1] = off; }

        // inner top and bottom edges, y = inner and y = 1 - inner
        step = (1f - (innerMaxX + innerMaxX)) / denom; s = innerX < padX ? padX : innerX;
        for (int i = 0; i < m; i++)
        {
            xs[4 * m + i] = s; ys[4 * m + i] = innerY; xs[5 * m + i] = s; ys[5 * m + i] = 1f - innerY;
            tx[4 * m + i] = 0f; ty[4 * m + i] = off; tx[5 * m + i] = 0f; ty[5 * m + i] = -off;
            s = step + s;
        }
        if (padX == 0f) { tx[4 * m] = off; tx[4 * m + m - 1] = -off; tx[5 * m] = off; tx[5 * m + m - 1] = -off; }

        // outer left and right edges, x = 0 and x = 1
        step = (1f - (padY + padY)) / denom; s = padY;
        for (int i = 0; i < m; i++)
        {
            xs[2 * m + i] = 0f; ys[2 * m + i] = s; xs[3 * m + i] = 1f; ys[3 * m + i] = s;
            tx[2 * m + i] = -off; ty[2 * m + i] = 0f; tx[3 * m + i] = off; ty[3 * m + i] = 0f;
            s = step + s;
        }
        if (padY == 0f) { ty[2 * m] = -off; ty[2 * m + m - 1] = off; ty[3 * m] = -off; ty[3 * m + m - 1] = off; }

        // inner left and right edges, x = inner and x = 1 - inner
        step = (1f - (innerMaxY + innerMaxY)) / denom; s = innerY < padY ? padY : innerY;
        for (int i = 0; i < m; i++)
        {
            xs[6 * m + i] = innerX; ys[6 * m + i] = s; xs[7 * m + i] = 1f - innerX; ys[7 * m + i] = s;
            tx[6 * m + i] = off; ty[6 * m + i] = 0f; tx[7 * m + i] = -off; ty[7 * m + i] = 0f;
            s = step + s;
        }
        if (padY == 0f) { ty[6 * m] = off; ty[6 * m + m - 1] = -off; ty[7 * m] = off; ty[7 * m + m - 1] = -off; }

        // the steepest-descent images: the template gradient times the homography Jacobian at identity
        // (0x008C5CE6..0x008C5D86)
        var j = new float[8, n];
        for (int i = 0; i < n; i++)
        {
            float x = xs[i], y = ys[i], gx = tx[i], gy = ty[i];
            j[0, i] = x * gx; j[1, i] = y * gx; j[2, i] = gx;
            j[3, i] = x * gy; j[4, i] = y * gy; j[5, i] = gy;
            j[6, i] = -(x * x) * gx - x * y * gy;
            j[7, i] = -(x * y) * gx - y * y * gy;
        }

        refinedH = (float[])initialH.Clone();
        refinedQuad = (float[])quad.Clone();
        float mid = (bright + dark) * 0.5f;
        float maxRow = img.Height - 1f, maxCol = img.Width - 1f;
        bool numericalFailure = false;

        for (int iter = 0; iter < iterations; iter++)
        {
            var h = refinedH;
            var ata = new float[8, 8];
            var atb = new float[8];
            for (int i = 0; i < n; i++)
            {
                float x = xs[i], y = ys[i];
                float w = 1f / (h[6] * x + h[7] * y + h[8]);
                float u = (h[0] * x + h[1] * y + h[2]) * w;
                float v = (h[3] * x + h[4] * y + h[5]) * w;
                float u0 = MathF.Floor(u), v0 = MathF.Floor(v), v1 = MathF.Ceiling(v), u1 = MathF.Ceiling(u);
                if (u0 < 0f || v1 > maxRow || v0 < 0f || u1 > maxCol) continue;       // 0x008C5FAC..0x008C5FF2
                int r0 = RoundHalfAway(v0), r1 = RoundHalfAway(v1), c0 = RoundHalfAway(u0);
                float ay = v - v0, ax = u - u0;
                float p00 = img[c0, r0], p10 = img[c0, r1];
                float p01 = c0 + 1 < img.Width ? img[c0 + 1, r0] : 0f;
                float p11 = c0 + 1 < img.Width ? img[c0 + 1, r1] : 0f;
                float top = (1f - ax) * p00 + ax * p01;
                float bottom = (1f - ax) * p10 + ax * p11;
                float value = (1f - ay) * top + ay * bottom;
                float err = (value - mid) * 0.003921569f;
                for (int a = 0; a < 8; a++)
                {
                    float g = j[a, i];
                    ata[a, a] += g * g;
                    for (int b = a + 1; b < 8; b++) ata[a, b] += g * j[b, i];
                    atb[a] += err * g;
                }
            }
            for (int a = 0; a < 8; a++) for (int b = 0; b < a; b++) ata[a, b] = ata[b, a];   // MakeSymmetric

            var delta = SolveCholesky(ata, atb, out numericalFailure);

            // the update [[1+d0, d1, d2], [d3, 1+d4, d5], [d6, d7, 1]], inverted, composed on the right
            // (0x008C620A..0x008C629A)
            var upd = new[] { 1f + delta[0], delta[1], delta[2], delta[3], 1f + delta[4], delta[5], delta[6], delta[7], 1f };
            var inv = Invert3x3(upd);
            var next = Multiply(h, inv);
            if (MathF.Abs(next[8] - 1f) >= 1e-5f)          // 0x008C62A0..0x008C634A
            {
                float h22 = next[8];
                for (int k = 0; k < 9; k++) next[k] /= h22;
            }
            refinedH = next;
            float change = QuadFromHomography(refinedH, refinedQuad);
            if (change < minCornerChange) break;
            if (numericalFailure) break;
        }

        if (numericalFailure)
        {
            refinedQuad = (float[])quad.Clone();
            refinedH = (float[])initialH.Clone();
            return 0;
        }
        var check = (float[])quad.Clone();
        float total = QuadFromHomography(refinedH, check);
        return total > maxCornerChange ? 1 : 0;
    }

    /// <summary>
    /// 0x008C66C4: the corners the homography gives for (0,0), (0,1), (1,0), (1,1), written over
    /// <paramref name="quad"/>; returns the largest distance a corner moved.
    /// </summary>
    private static float QuadFromHomography(float[] h, float[] quad)
    {
        var prev = (float[])quad.Clone();
        // each denominator is taken as a reciprocal and multiplied, as the engine does (0x008C6742..0x008C67A2)
        float w0 = 1f / h[8], w1 = 1f / (h[7] + h[8]), w2 = 1f / (h[6] + h[8]), w3 = 1f / (h[6] + h[7] + h[8]);
        quad[0] = h[2] * w0;                 quad[1] = h[5] * w0;
        quad[2] = (h[1] + h[2]) * w1;        quad[3] = (h[4] + h[5]) * w1;
        quad[4] = (h[0] + h[2]) * w2;        quad[5] = (h[3] + h[5]) * w2;
        quad[6] = (h[0] + h[1] + h[2]) * w3; quad[7] = (h[3] + h[4] + h[5]) * w3;
        float max = 0f;
        for (int i = 0; i < 4; i++)
        {
            float dx = quad[2 * i] - prev[2 * i], dy = quad[2 * i + 1] - prev[2 * i + 1];
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > max) max = d;
        }
        return max;
    }

    private static float[] Multiply(float[] a, float[] b)
    {
        var r = new float[9];
        for (int i = 0; i < 3; i++)
            for (int k = 0; k < 3; k++)
                r[3 * i + k] = a[3 * i] * b[k] + a[3 * i + 1] * b[3 + k] + a[3 * i + 2] * b[6 + k];
        return r;
    }

    private static float[] Invert3x3(float[] m)
    {
        float a = m[0], b = m[1], c = m[2], d = m[3], e = m[4], f = m[5], g = m[6], h = m[7], i = m[8];
        float det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
        float s = 1f / det;
        return new[]
        {
            (e * i - f * h) * s, (c * h - b * i) * s, (b * f - c * e) * s,
            (f * g - d * i) * s, (a * i - c * g) * s, (c * d - a * f) * s,
            (d * h - e * g) * s, (b * g - a * h) * s, (a * e - b * d) * s,
        };
    }

    /// <summary>
    /// The normal equations solved by Cholesky decomposition, as <c>Matrix::SolveLeastSquaresWithCholesky</c>
    /// (0x008C61F4) is asked to; a non-positive pivot is the numerical failure it reports.
    /// </summary>
    private static float[] SolveCholesky(float[,] a, float[] b, out bool failure)
    {
        int n = b.Length;
        var l = new float[n, n];
        failure = false;
        for (int i = 0; i < n; i++)
        {
            for (int k = 0; k <= i; k++)
            {
                float sum = a[i, k];
                for (int p = 0; p < k; p++) sum -= l[i, p] * l[k, p];
                if (i == k)
                {
                    if (sum <= 0f) { failure = true; sum = 1e-20f; }
                    l[i, i] = MathF.Sqrt(sum);
                }
                else l[i, k] = sum / l[k, k];
            }
        }
        var y = new float[n];
        for (int i = 0; i < n; i++) { float sum = b[i]; for (int p = 0; p < i; p++) sum -= l[i, p] * y[p]; y[i] = sum / l[i, i]; }
        var x = new float[n];
        for (int i = n - 1; i >= 0; i--) { float sum = y[i]; for (int p = i + 1; p < n; p++) sum -= l[p, i] * x[p]; x[i] = sum / l[i, i]; }
        return x;
    }
}
