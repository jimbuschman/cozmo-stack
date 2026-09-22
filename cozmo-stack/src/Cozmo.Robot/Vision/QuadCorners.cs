namespace Cozmo.Robot.Vision;

/// <summary>
/// The engine's corner extraction, the second half of
/// <c>ComputeQuadrilateralsFromConnectedComponents</c> 0x00892D70: a component's exterior boundary
/// (<c>TraceNextExteriorBoundary</c> 0x008C6B18) handed to <c>ExtractLineFitsPeaks</c> 0x008A5DB8, which
/// smooths the boundary with the derivative of a Gaussian, clusters the resulting tangents into four,
/// fits a line to each cluster and intersects them.
///
/// Every constant, loop bound and coordinate convention below is read off the disassembly; the
/// OpenCV calls the engine makes (<c>getGaussianKernel</c>, <c>filter2D</c>, <c>kmeans</c>,
/// <c>solve</c>) are re-implemented here in the same form they are called with, which is what makes the
/// result reproducible: the k-means runs with <c>KMEANS_USE_INITIAL_LABELS</c> and one attempt, so
/// nothing about it is random.
/// </summary>
public static class QuadCorners
{
    /// <summary>
    /// Capacity of the boundary list <c>ComputeQuadrilateralsFromConnectedComponents</c> allocates at
    /// 0x00892DA8 (<c>movw r1, #0x2710</c>). The trace checks it before every append and silently drops
    /// the rest, so a boundary longer than this is truncated rather than rejected.
    /// </summary>
    public const int BoundaryCapacity = 10000;

    /// <summary>
    /// <c>sigma = numPoints / 64</c>: the multiplier 0x3C800000 at 0x008A5E7A, applied to the boundary
    /// length as a float (0x008A5E90).
    /// </summary>
    public const float SigmaPerBoundaryPoint = 1.0f / 64.0f;

    /// <summary>The four k-means arguments at 0x008A6458..0x008A6490: K, COUNT|EPS, 15, 0.1, one attempt, KMEANS_USE_INITIAL_LABELS.</summary>
    public const int Clusters = 4;
    public const int KMeansMaxIterations = 15;
    public const double KMeansEpsilon = 0.1;

    /// <summary>A boundary point, in image coordinates, as the engine's <c>Point&lt;s16&gt;</c>.</summary>
    public readonly record struct BoundaryPoint(short X, short Y);

    // ------------------------------------------------------------------ exterior boundary

    /// <summary>
    /// <c>TraceNextExteriorBoundary</c> 0x008C6B18. The engine does not walk the component pixel by
    /// pixel: it reduces the component to four extent arrays over its bounding box - the least and
    /// greatest x in each row and the least and greatest y in each column - and stitches a staircase
    /// contour out of them in four passes, right side down, bottom leftwards, left side up, top
    /// rightwards. A row or column of the bounding box with no pixel in it makes the trace fail
    /// (0x008C6F70..0x008C6FC6), which is how a component whose bounding box it does not span is
    /// rejected.
    ///
    /// The passes are not symmetric and the asymmetry is deliberate: going down the right side a step
    /// outwards is drawn on the new row and a step inwards on the old one (0x008C7012 and 0x008C704E),
    /// and the two passes that come back up mirror that so the contour closes without repeating a point.
    /// Coordinates are traced inside the bounding box and the box origin is added back at the end
    /// (0x008C72D0..0x008C72E8).
    /// </summary>
    public static List<BoundaryPoint>? ExteriorBoundary(int[] labels, int label, int imageWidth, int minX, int minY, int maxX, int maxY)
    {
        int w = maxX - minX + 1, h = maxY - minY + 1;
        if (w <= 0 || h <= 0) return null;

        var rowMinX = new int[h]; var rowMaxX = new int[h];
        var colMinY = new int[w]; var colMaxY = new int[w];
        Array.Fill(rowMinX, short.MaxValue); Array.Fill(rowMaxX, short.MinValue);
        Array.Fill(colMinY, short.MaxValue); Array.Fill(colMaxY, short.MinValue);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (labels[(minY + y) * imageWidth + minX + x] != label) continue;
                if (x < rowMinX[y]) rowMinX[y] = x;
                if (x > rowMaxX[y]) rowMaxX[y] = x;
                if (y < colMinY[x]) colMinY[x] = y;
                if (y > colMaxY[x]) colMaxY[x] = y;
            }
        for (int y = 0; y < h; y++) if (rowMaxX[y] < 0) return null;
        for (int x = 0; x < w; x++) if (colMaxY[x] < 0) return null;

        var pts = new List<BoundaryPoint>();
        void Emit(int x, int y)
        {
            if (pts.Count < BoundaryCapacity) pts.Add(new BoundaryPoint((short)(x + minX), (short)(y + minY)));
        }

        // pass 1: the right side, top to bottom (0x008C6FCA..0x008C709A)
        Emit(rowMaxX[0], 0);
        if (h >= 2)
            for (int row = 1; row < h; row++)
            {
                int prev = rowMaxX[row - 1], cur = rowMaxX[row];
                if (cur > prev) for (int x = prev; x < cur; x++) Emit(x, row);
                else if (prev > cur) for (int x = prev - 1; x >= cur; x--) Emit(x, row - 1);
                Emit(cur, row);
            }

        // pass 2: the bottom, right to left (0x008C709C..0x008C7156)
        for (int col = rowMaxX[h - 1]; col > rowMinX[h - 1]; col--)
        {
            int next = col - 1, prev = colMaxY[col], cur = colMaxY[next];
            if (prev > cur) for (int y = prev - 1; y >= cur; y--) Emit(col, y);
            else if (cur > prev) for (int y = prev; y < cur; y++) Emit(next, y);
            Emit(next, cur);
        }

        // pass 3: the left side, bottom to top (0x008C7158..0x008C7206)
        for (int row = h - 2; row >= 0; row--)
        {
            int prev = rowMinX[row + 1], cur = rowMinX[row];
            if (cur > prev) for (int x = prev + 1; x <= cur; x++) Emit(x, row + 1);
            else if (cur < prev) for (int x = prev; x > cur; x--) Emit(x, row);
            Emit(cur, row);
        }

        // pass 4: the top, left to right (0x008C7208..0x008C72CA)
        for (int col = rowMinX[0] + 1; col <= rowMaxX[0]; col++)
        {
            int prev = colMinY[col - 1], cur = colMinY[col];
            if (cur > prev) for (int y = prev + 1; y <= cur; y++) Emit(col - 1, y);
            else if (prev > cur) for (int y = prev; y > cur; y--) Emit(col, y);
            Emit(col, cur);
        }

        return pts;
    }

    // ------------------------------------------------------------------ the smoothing kernel

    /// <summary>
    /// <c>cv::getGaussianKernel(ksize, sigma, CV_32F)</c> as the engine calls it at 0x008A5F26. The
    /// small-kernel table OpenCV keeps for <c>sigma &lt;= 0</c> never applies here because sigma is the
    /// boundary length over 64, so the exponential is always evaluated; the sum that normalises it
    /// accumulates the float-rounded taps, as OpenCV's does.
    /// </summary>
    public static float[] GaussianKernel(int ksize, double sigma)
    {
        var k = new float[Math.Max(1, ksize)];
        double sigmaX = sigma > 0 ? sigma : ((k.Length - 1) * 0.5 - 1) * 0.3 + 0.8;
        double scale2X = -0.5 / (sigmaX * sigmaX);
        double sum = 0;
        for (int i = 0; i < k.Length; i++)
        {
            double x = i - (k.Length - 1) * 0.5;
            k[i] = (float)Math.Exp(scale2X * x * x);
            sum += k[i];
        }
        sum = 1.0 / sum;
        for (int i = 0; i < k.Length; i++) k[i] = (float)(k[i] * sum);
        return k;
    }

    /// <summary>
    /// The kernel size at 0x008A5ED6..0x008A5F1A: OpenCV's relation between a Gaussian's size and its
    /// sigma, <c>sigma = 0.3 * ((ksize - 1) * 0.5 - 1) + 0.8</c>, solved for the size, rounded up and
    /// forced odd with an <c>orr #1</c>.
    /// </summary>
    public static int KernelSize(float sigma)
    {
        float v = ((sigma + -0.8f) / 0.3f + 1f);
        v = v + v + 1f;
        int n = (int)MathF.Ceiling(v);
        if (n < 0) n += 1;
        return n | 1;
    }

    /// <summary>
    /// <c>cv::filter2D</c> at 0x008A60B4, run on the Gaussian kernel itself with the 1x3 kernel
    /// [-0.5, 0, +0.5] built at 0x008A602C: the derivative of the Gaussian, which is then what the
    /// boundary is convolved with. filter2D correlates rather than convolves, and its border type is the
    /// 4 the engine passes (<c>BORDER_REFLECT_101</c>), so the two end taps see their own neighbour
    /// twice and come out zero.
    /// </summary>
    public static float[] DifferentiateKernel(float[] kernel)
    {
        int n = kernel.Length;
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            int lo = i - 1, hi = i + 1;
            lo = lo < 0 ? Reflect101(lo, n) : lo;
            hi = hi >= n ? Reflect101(hi, n) : hi;
            d[i] = -0.5f * kernel[lo] + 0.5f * kernel[hi];
        }
        return d;
    }

    private static int Reflect101(int i, int n)
    {
        if (n == 1) return 0;
        if (i < 0) return -i;
        if (i >= n) return 2 * n - i - 2;
        return i;
    }

    /// <summary>
    /// The unit tangent at every boundary point: the derivative-of-Gaussian kernel convolved circularly
    /// with the boundary (0x008A613E..0x008A628A - the index wraps by one whole length at either end,
    /// which is what makes it circular), accumulated in double, then divided by its own length. A point
    /// whose convolution comes out exactly zero keeps the zero vector, as the engine's branch at
    /// 0x008A623A does.
    ///
    /// The pair is kept in the engine's order, row 0 of its 2 x n matrix being y and row 1 x
    /// (0x008A5EA6..0x008A5ED0).
    /// </summary>
    public static (float Y, float X)[] Tangents(IReadOnlyList<BoundaryPoint> boundary, float[] derivative)
    {
        int n = boundary.Count, ks = derivative.Length, half = ks / 2;
        var tangents = new (float Y, float X)[n];
        for (int i = 0; i < n; i++)
        {
            double sy = 0, sx = 0;
            for (int k = 0; k < ks; k++)
            {
                int idx = i - half + k;
                if (idx >= n) idx -= n; else if (idx < 0) idx += n;
                if (idx < 0 || idx >= n) continue;
                sy += derivative[k] * (float)boundary[idx].Y;
                sx += derivative[k] * (float)boundary[idx].X;
            }
            float ty = (float)sy, tx = (float)sx;
            if (tx == 0 && ty == 0) { tangents[i] = (0, 0); continue; }
            float norm = MathF.Sqrt(tx * tx + ty * ty);
            float inv = 1f / norm;
            tangents[i] = (ty * inv, tx * inv);
        }
        return tangents;
    }

    // ------------------------------------------------------------------ k-means

    /// <summary>
    /// The initial labels at 0x008A62D8..0x008A63EE: four equal arcs of the boundary, the quarter points
    /// taken with C's truncating division.
    /// </summary>
    public static int[] InitialLabels(int n)
    {
        var labels = new int[n];
        int q1 = n / 4, q2 = n / 2, q3 = 3 * n / 4;
        for (int i = q1; i < q2; i++) labels[i] = 1;
        for (int i = q2; i < q3; i++) labels[i] = 2;
        for (int i = q3; i < n; i++) labels[i] = 3;
        return labels;
    }

    /// <summary>
    /// <c>cv::kmeans</c> as the engine calls it (0x008A6490): K = 4, one attempt,
    /// <c>KMEANS_USE_INITIAL_LABELS</c>, and a termination criterion of 15 iterations or a centre shift
    /// of 0.1. With the initial labels supplied and a single attempt OpenCV's implementation is
    /// deterministic - it never draws a random centre - so this is Lloyd's algorithm with OpenCV's exact
    /// bookkeeping: centres are the means of the current labels, the epsilon is compared against the
    /// squared shift (kmeans squares it on entry), the iteration count is <c>max(maxCount, 2)</c>, and an
    /// empty cluster steals the point farthest from the centre of the largest one.
    /// </summary>
    public static void KMeans((float Y, float X)[] samples, int[] labels, int k = Clusters,
                              int maxIterations = KMeansMaxIterations, double epsilon = KMeansEpsilon)
    {
        int n = samples.Length;
        if (n == 0) return;
        double eps = Math.Max(epsilon, 0);
        eps *= eps;

        var centers = new double[k, 2];
        var oldCenters = new double[k, 2];
        var counters = new int[k];
        double maxCenterShift = double.MaxValue;

        for (int iter = 0; ;)
        {
            (centers, oldCenters) = (oldCenters, centers);

            Array.Clear(centers);
            Array.Clear(counters);
            for (int i = 0; i < n; i++)
            {
                int c = labels[i];
                centers[c, 0] += samples[i].Y;
                centers[c, 1] += samples[i].X;
                counters[c]++;
            }
            if (iter > 0) maxCenterShift = 0;

            for (int c = 0; c < k; c++)
            {
                if (counters[c] != 0) continue;
                int biggest = 0;
                for (int c1 = 1; c1 < k; c1++) if (counters[biggest] < counters[c1]) biggest = c1;
                double scale = 1.0 / counters[biggest];
                double oy = centers[biggest, 0] * scale, ox = centers[biggest, 1] * scale;
                double maxDist = 0; int farthest = -1;
                for (int i = 0; i < n; i++)
                {
                    if (labels[i] != biggest) continue;
                    double dy = samples[i].Y - oy, dx = samples[i].X - ox;
                    double dist = dy * dy + dx * dx;
                    if (maxDist <= dist) { maxDist = dist; farthest = i; }
                }
                if (farthest < 0) continue;
                counters[biggest]--; counters[c]++;
                labels[farthest] = c;
                centers[biggest, 0] -= samples[farthest].Y; centers[biggest, 1] -= samples[farthest].X;
                centers[c, 0] += samples[farthest].Y; centers[c, 1] += samples[farthest].X;
            }

            for (int c = 0; c < k; c++)
            {
                if (counters[c] == 0) continue;
                double scale = 1.0 / counters[c];
                centers[c, 0] *= scale; centers[c, 1] *= scale;
                if (iter > 0)
                {
                    double dy = centers[c, 0] - oldCenters[c, 0], dx = centers[c, 1] - oldCenters[c, 1];
                    maxCenterShift = Math.Max(maxCenterShift, dy * dy + dx * dx);
                }
            }

            if (++iter == Math.Max(maxIterations, 2) || maxCenterShift <= eps) break;

            for (int i = 0; i < n; i++)
            {
                int best = 0; double minDist = double.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    double dy = samples[i].Y - centers[c, 0], dx = samples[i].X - centers[c, 1];
                    double dist = dy * dy + dx * dx;
                    if (minDist > dist) { minDist = dist; best = c; }
                }
                labels[i] = best;
            }
        }
    }

    // ------------------------------------------------------------------ line fits and intersections

    /// <summary>
    /// One cluster's line. <see cref="Swapped"/> is the engine's flag at the record's +8: false fits
    /// <c>y = a x + b</c>, true fits <c>x = a y + b</c>, chosen by which extent of the cluster is larger
    /// (0x008A6714), so a near-vertical side is fitted the way round that stays finite.
    /// </summary>
    public readonly record struct LineFit(double A, double B, bool Swapped);

    /// <summary>
    /// The per-cluster fit at 0x008A667C..0x008A6A1C: gather the cluster's boundary points, measure their
    /// x and y extents, fit the wider direction with least squares (<c>cv::solve</c> with
    /// <c>DECOMP_SVD</c> of <c>[u 1]</c> against <c>v</c>) and keep the slope, the intercept and which
    /// way round it was. A cluster of fewer than two points makes the whole extraction fail
    /// (0x008A66BA).
    ///
    /// The solve is the same least-squares problem, taken here through the normal equations in double
    /// rather than an SVD in float; where the system is rank deficient - every point sharing one
    /// coordinate - the minimum-norm solution the SVD would return is formed explicitly.
    /// </summary>
    public static LineFit? FitCluster(IReadOnlyList<BoundaryPoint> boundary, int[] labels, int cluster)
    {
        var members = new List<int>();
        for (int i = 0; i < boundary.Count; i++) if (labels[i] == cluster) members.Add(i);
        if (members.Count < 2) return null;

        int maxX = int.MinValue, minX = int.MaxValue, maxY = int.MinValue, minY = int.MaxValue;
        foreach (int i in members)
        {
            var p = boundary[i];
            if (p.X > maxX) maxX = p.X;
            if (p.X < minX) minX = p.X;
            if (p.Y > maxY) maxY = p.Y;
            if (p.Y < minY) minY = p.Y;
        }
        bool swapped = (maxX - minX) < (maxY - minY);

        double su = 0, sv = 0, suu = 0, suv = 0;
        int n = members.Count;
        foreach (int i in members)
        {
            var p = boundary[i];
            double u = swapped ? p.Y : p.X, v = swapped ? p.X : p.Y;
            su += u; sv += v; suu += u * u; suv += u * v;
        }
        double det = suu * n - su * su;
        double a, b;
        if (Math.Abs(det) < 1e-12)
        {
            // every u the same: the minimum-norm least-squares solution of the rank-one system
            double c = n > 0 ? su / n : 0;
            double mean = n > 0 ? sv / n : 0;
            double scale = mean / (c * c + 1);
            a = c * scale; b = scale;
        }
        else
        {
            a = (suv * n - su * sv) / det;
            b = (suu * sv - su * suv) / det;
        }
        return new LineFit(a, b, swapped);
    }

    /// <summary>
    /// Where two fitted lines meet, in the engine's arrangement at 0x008A6AA8..0x008A6B64. The result is
    /// returned the way the engine carries it, y first, because the bounds it is then tested against are
    /// the image height and width in that order.
    /// </summary>
    public static (double Y, double X) Intersect(LineFit i, LineFit j)
    {
        if (i.Swapped == j.Swapped)
        {
            double u = (j.B - i.B) / (i.A - j.A);
            double v = i.A * u + i.B;
            return i.Swapped ? (u, v) : (v, u);
        }
        if (i.Swapped)                       // x = a_i y + b_i against y = a_j x + b_j
        {
            double y = (j.A * i.B + j.B) / (1 - i.A * j.A);
            return (y, i.A * y + i.B);
        }
        double y2 = (i.A * j.B + i.B) / (1 - i.A * j.A);
        return (y2, j.A * y2 + j.B);
    }

    // ------------------------------------------------------------------ ordering and rounding

    /// <summary>
    /// <c>Quadrilateral&lt;float&gt;::ComputeClockwiseCorners</c> 0x008A1324: the four corners sorted by
    /// the angle they make with their own centroid, ascending (<c>Matrix::InsertionSort</c> is called
    /// with its ascending flag set, 0x008A1440). With y down that runs clockwise on screen, starting at
    /// the corner nearest the negative x axis; a corner sitting exactly on the centroid is given angle
    /// zero rather than an atan2 of two zeros (0x008A13EE).
    /// </summary>
    public static Vec2[] ComputeClockwiseCorners(IReadOnlyList<Vec2> corners)
    {
        int n = corners.Count;
        double cx = 0, cy = 0;
        foreach (var p in corners) { cx += p.X; cy += p.Y; }
        cx *= 0.25; cy *= 0.25;

        var angles = new double[n];
        var index = new int[n];
        for (int i = 0; i < n; i++)
        {
            double dx = corners[i].X - cx, dy = corners[i].Y - cy;
            angles[i] = dx == 0 && dy == 0 ? 0 : Math.Atan2(dy, dx);
            index[i] = i;
        }
        // insertion sort, ascending and stable, carrying the indices
        for (int i = 1; i < n; i++)
        {
            double key = angles[i]; int ki = index[i]; int j = i - 1;
            while (j >= 0 && angles[j] > key) { angles[j + 1] = angles[j]; index[j + 1] = index[j]; j--; }
            angles[j + 1] = key; index[j + 1] = ki;
        }
        var sorted = new Vec2[n];
        for (int i = 0; i < n; i++) sorted[i] = corners[index[i]];
        return sorted;
    }

    /// <summary>
    /// The engine's own rounding of a corner into its <c>Quadrilateral&lt;s16&gt;</c>
    /// (0x008A6C30..0x008A6CD4): clamp to the range of an s16 first, then round half away from zero -
    /// <c>ceilf(v - 0.5)</c> at or below zero and <c>floorf(v + 0.5)</c> above it.
    /// </summary>
    public static double RoundToS16(double v)
    {
        double c = v < -32768.0 ? -32768.0 : v > 32767.0 ? 32767.0 : v;
        return c > 0 ? Math.Floor(c + 0.5) : Math.Ceiling(c - 0.5);
    }

    // ------------------------------------------------------------------ the whole chain

    /// <summary>
    /// <c>ExtractLineFitsPeaks</c> 0x008A5DB8 end to end: smooth, cluster, fit, intersect, order and
    /// round. Every pair of the four lines is intersected (the loops at 0x008A6A86 and 0x008A6AA8), each
    /// intersection is kept only if it lands inside the image, and exactly four must survive - the
    /// <c>cmp r2, #0x20</c> at 0x008A6BE2, thirty-two bytes of <c>Point&lt;f32&gt;</c>. That is what
    /// rejects a component whose four sides do not actually meet in four places.
    /// </summary>
    public static Vec2[]? ExtractLineFitsPeaks(IReadOnlyList<BoundaryPoint> boundary, int imageHeight, int imageWidth)
    {
        int n = boundary.Count;
        if (n < 1) return null;

        float sigma = n * SigmaPerBoundaryPoint;
        var kernel = GaussianKernel(KernelSize(sigma), sigma);
        var derivative = DifferentiateKernel(kernel);
        var tangents = Tangents(boundary, derivative);

        var labels = InitialLabels(n);
        if (n >= Clusters) KMeans(tangents, labels);

        var fits = new LineFit[Clusters];
        for (int c = 0; c < Clusters; c++)
        {
            var fit = FitCluster(boundary, labels, c);
            if (fit is null) return null;
            fits[c] = fit.Value;
        }

        var found = new List<Vec2>();
        for (int i = 0; i < Clusters - 1; i++)
            for (int j = i + 1; j < Clusters; j++)
            {
                var (y, x) = Intersect(fits[i], fits[j]);
                if (double.IsNaN(x) || double.IsNaN(y)) continue;
                if (x < 0 || y >= imageHeight || y < 0 || x >= imageWidth) continue;
                found.Add(new Vec2(x, y));
            }
        if (found.Count != 4) return null;

        var clockwise = ComputeClockwiseCorners(found);
        var rounded = new Vec2[4];
        for (int i = 0; i < 4; i++) rounded[i] = new Vec2(RoundToS16(clockwise[i].X), RoundToS16(clockwise[i].Y));
        return rounded;
    }
}
