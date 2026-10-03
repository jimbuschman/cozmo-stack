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

    /// <summary>
    /// The M11-029 cluster reject threshold at 0x008A61C0/0x008A64B4: the word <c>0x3F6803C9</c> as a float,
    /// cos(25 deg) = 0.90630776.
    /// </summary>
    public static readonly float Cos25Deg = BitConverter.Int32BitsToSingle(0x3F6803C9);

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
    // fidelity: M11-027
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
        => KMeans(samples, labels, out _, k, maxIterations, epsilon);

    /// <summary>
    /// The same clustering, also returning the final centres (<c>centers</c> in the engine's kmeans call,
    /// 4x2 CV_32F) so the C1.4 cluster reject can use them.
    /// </summary>
    public static void KMeans((float Y, float X)[] samples, int[] labels, out (float Y, float X)[] centers,
                              int k = Clusters, int maxIterations = KMeansMaxIterations, double epsilon = KMeansEpsilon)
    {
        int n = samples.Length;
        if (n == 0) { centers = Array.Empty<(float Y, float X)>(); return; }
        double eps = Math.Max(epsilon, 0);
        eps *= eps;

        var centersD = new double[k, 2];
        var oldCenters = new double[k, 2];
        var counters = new int[k];
        double maxCenterShift = double.MaxValue;

        for (int iter = 0; ;)
        {
            (centersD, oldCenters) = (oldCenters, centersD);

            Array.Clear(centersD);
            Array.Clear(counters);
            for (int i = 0; i < n; i++)
            {
                int c = labels[i];
                centersD[c, 0] += samples[i].Y;
                centersD[c, 1] += samples[i].X;
                counters[c]++;
            }
            if (iter > 0) maxCenterShift = 0;

            for (int c = 0; c < k; c++)
            {
                if (counters[c] != 0) continue;
                int biggest = 0;
                for (int c1 = 1; c1 < k; c1++) if (counters[biggest] < counters[c1]) biggest = c1;
                double scale = 1.0 / counters[biggest];
                double oy = centersD[biggest, 0] * scale, ox = centersD[biggest, 1] * scale;
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
                centersD[biggest, 0] -= samples[farthest].Y; centersD[biggest, 1] -= samples[farthest].X;
                centersD[c, 0] += samples[farthest].Y; centersD[c, 1] += samples[farthest].X;
            }

            for (int c = 0; c < k; c++)
            {
                if (counters[c] == 0) continue;
                double scale = 1.0 / counters[c];
                centersD[c, 0] *= scale; centersD[c, 1] *= scale;
                if (iter > 0)
                {
                    double dy = centersD[c, 0] - oldCenters[c, 0], dx = centersD[c, 1] - oldCenters[c, 1];
                    maxCenterShift = Math.Max(maxCenterShift, dy * dy + dx * dx);
                }
            }

            if (++iter == Math.Max(maxIterations, 2) || maxCenterShift <= eps) break;

            for (int i = 0; i < n; i++)
            {
                int best = 0; double minDist = double.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    double dy = samples[i].Y - centersD[c, 0], dx = samples[i].X - centersD[c, 1];
                    double dist = dy * dy + dx * dx;
                    if (minDist > dist) { minDist = dist; best = c; }
                }
                labels[i] = best;
            }
        }

        centers = new (float Y, float X)[k];
        for (int c = 0; c < k; c++) centers[c] = ((float)centersD[c, 0], (float)centersD[c, 1]);
    }

    /// <summary>
    /// C1.4 (M11-029): for each point, <c>dot(data.row(i), centers.row(labels[i])) &lt; cos(25 deg)</c> sets
    /// the point's label to <c>-1</c> (0x008A64B4..0x008A65F2). The tangent and centre pairs are in the
    /// engine's stored order <c>(y, x)</c>.
    /// </summary>
    // fidelity: M11-029
    public static void ApplyClusterReject((float Y, float X)[] tangents, int[] labels, (float Y, float X)[] centers)
    {
        for (int i = 0; i < tangents.Length; i++)
        {
            int lab = labels[i];
            if (lab < 0) continue;
            float dot = tangents[i].Y * centers[lab].Y + tangents[i].X * centers[lab].X;
            if (dot < Cos25Deg) labels[i] = -1;
        }
    }

    // ------------------------------------------------------------------ line fits and intersections

    /// <summary>
    /// One cluster's line, as the engine's 12-byte record: two binary32 words (slope and intercept, the two floats
    /// <c>solve</c> returns, stored at 0x008A68EE/0x008A68F8) and the <see cref="Swapped"/> byte at +8. <see cref="Swapped"/> is the engine's flag:
    /// false fits <c>y = a x + b</c>, true fits <c>x = a y + b</c>, chosen by which extent of the cluster is larger
    /// (0x008A6714), so a near-vertical side is fitted the way round that stays finite.
    /// </summary>
    public readonly record struct LineFit(float A, float B, bool Swapped);

    /// <summary>
    /// The per-cluster fit at 0x008A667C..0x008A68F8: gather the cluster's boundary points, measure their
    /// x and y extents (0x008A66C0..0x008A6712), pick the wider direction (<c>swapped = xRange &lt; yRange</c>, 0x008A6714..0x008A671A),
    /// build the three <c>CV_32F</c> matrices (A is count x 2 with rows <c>(float u, 1.0f)</c> and b is count x 1 of <c>float v</c>, the loops at
    /// 0x008A67CA..0x008A68AC, <c>u</c> the boundary Y when swapped and X otherwise) and call <c>cv::solve(A, b, x, 1)</c>
    /// (<c>DECOMP_SVD</c>, 0x008A68DE..0x008A68E0), keeping <c>x(0,0)</c> and <c>x(1,0)</c> as the slope and the intercept
    /// (0x008A68E4..0x008A68F8). A cluster of fewer than two points makes the whole extraction fail
    /// (<c>cmp r1, #5</c> on the byte size, 0x008A66BA). The solve is <see cref="OpenCvSolveSvd"/>, the shipped
    /// <c>libopencv_core.so</c> routine; the rank-deficient case is the one it returns, not a formula of this stack's.
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

        int n = members.Count;
        var a = new float[n * 2];
        var b = new float[n];
        for (int r = 0; r < n; r++)
        {
            var p = boundary[members[r]];
            a[r * 2] = swapped ? (float)p.Y : (float)p.X;
            a[r * 2 + 1] = 1.0f;
            b[r] = swapped ? (float)p.X : (float)p.Y;
        }
        var x = OpenCvSolveSvd.Solve(a, b, n, 2);
        return new LineFit(x[0], x[1], swapped);
    }

    /// <summary>
    /// Where two fitted lines meet, in the engine's arrangement at 0x008A6AA8..0x008A6B64, in binary32 with the engine's operation
    /// order (<c>vsub.f32</c>, <c>vdiv.f32</c>, <c>vmul.f32</c>, <c>vadd.f32</c>; the denominator <c>1.0f - a_i*a_j</c>). The result is
    /// returned the way the engine carries it, y first, because the bounds it is then tested against are
    /// the image height and width in that order.
    /// </summary>
    public static (float Y, float X) Intersect(LineFit i, LineFit j)
    {
        if (i.Swapped == j.Swapped)
        {
            float d = i.A - j.A;                  // 0x008A6AC2: s0 = s4 - s0
            float num = j.B - i.B;                // 0x008A6AC6: s2 = s2 - s6
            float u = num / d;                    // 0x008A6ACA
            float prod = i.A * u;                 // 0x008A6ACE
            float v = i.B + prod;                 // 0x008A6AD6
            return i.Swapped ? (u, v) : (v, u);
        }
        if (i.Swapped)                            // x = a_i y + b_i against y = a_j x + b_j (0x008A6AEC..0x008A6B20)
        {
            float aa = j.A * i.A;                 // 0x008A6AF8
            float ab = j.A * i.B;                 // 0x008A6B00
            float den = 1.0f - aa;                // 0x008A6B04
            float num = ab + j.B;                 // 0x008A6B08
            float y = num / den;                  // 0x008A6B0C
            float prod = i.A * y;                 // 0x008A6B10
            float x = i.B + prod;                 // 0x008A6B18
            return (y, x);
        }
        {
            float aa = i.A * j.A;                 // 0x008A6B3A (x = a_j y + b_j against y = a_i x + b_i, 0x008A6B2E..0x008A6B5E)
            float ab = i.A * j.B;                 // 0x008A6B42
            float den = 1.0f - aa;                // 0x008A6B46
            float num = ab + i.B;                 // 0x008A6B4A
            float y = num / den;                  // 0x008A6B4E
            float prod = j.A * y;                 // 0x008A6B52
            float x = j.B + prod;                 // 0x008A6B5A
            return (y, x);
        }
    }

    // ------------------------------------------------------------------ ordering and rounding

    /// <summary>
    /// <c>Quadrilateral&lt;float&gt;::ComputeClockwiseCorners</c> 0x008A1324: the four corners sorted by
    /// the angle they make with their own centroid, ascending (<c>Matrix::InsertionSort</c> is called
    /// with its ascending flag set, 0x008A1440). With y down that runs clockwise on screen, starting at
    /// the corner nearest the negative x axis; a corner sitting exactly on the centroid is given angle
    /// zero rather than an atan2 of two zeros (0x008A13EE).
    /// </summary>
    // fidelity: M11-030
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
    /// (0x008A6C1E..0x008A6C7C), all in binary32: below -32768.0f (0xC7000000) the result is <c>ceilf(-32768.0f + -0.5f)</c>; above
    /// 32767.0f (0x46FFFE00) it is <c>floorf(32767.0f + 0.5f)</c>; otherwise at or below zero (or NaN) <c>ceilf(v + -0.5f)</c> and above zero
    /// <c>floorf(v + 0.5f)</c>. The add is a binary32 add, so 0.49999997f + 0.5f is 1.0f.
    /// </summary>
    // fidelity: M11-029, M11-030
    public static float RoundToS16(float v)
    {
        if (v < -32768.0f) return MathF.Ceiling(-32768.0f + -0.5f);
        if (v > 32767.0f) return MathF.Floor(32767.0f + 0.5f);
        if (!(v > 0.0f)) return MathF.Ceiling(v + -0.5f);
        return MathF.Floor(v + 0.5f);
    }

    // ------------------------------------------------------------------ the whole chain

    /// <summary>
    /// <c>ExtractLineFitsPeaks</c> 0x008A5DB8 end to end: smooth, cluster, fit, intersect, order and
    /// round. Every pair of the four lines is intersected (the loops at 0x008A6A86 and 0x008A6AA8), each
    /// intersection is kept only if it lands inside the image, and exactly four must survive - the
    /// <c>cmp r2, #0x20</c> at 0x008A6BE2, thirty-two bytes of <c>Point&lt;f32&gt;</c>. That is what
    /// rejects a component whose four sides do not actually meet in four places.
    /// </summary>
    // fidelity: M11-029
    public static Vec2[]? ExtractLineFitsPeaks(IReadOnlyList<BoundaryPoint> boundary, int imageHeight, int imageWidth)
    {
        int n = boundary.Count;
        if (n < 1) return null;

        float sigma = n * SigmaPerBoundaryPoint;
        var kernel = GaussianKernel(KernelSize(sigma), sigma);
        var derivative = DifferentiateKernel(kernel);
        var tangents = Tangents(boundary, derivative);

        var labels = InitialLabels(n);
        (float Y, float X)[] centers = Array.Empty<(float Y, float X)>();
        if (n >= Clusters) KMeans(tangents, labels, out centers);

        // C1.4 (M11-029): reject at 0x008A64B4..0x008A65F2. For each point, dot(data.row(i),
        // centers.row(labels[i])) is computed (float-converted at 0x008A65D2..0x008A65DE) and compared at
        // 0x008A65E2; on less-than cos(25 deg) = 0x3F6803C9 the point's label is set to -1 (0x008A65EC/
        // 0x008A65F2). The tangent arrays are unit vectors, so a point more than 25 deg from its cluster
        // centre is discarded and its cluster can no longer be fitted.
        if (n >= Clusters) ApplyClusterReject(tangents, labels, centers);

        var fits = new LineFit[Clusters];
        for (int c = 0; c < Clusters; c++)
        {
            var fit = FitCluster(boundary, labels, c);
            if (fit is null) return null;
            fits[c] = fit.Value;
        }

        // 0x008A6A66..0x008A6A82: the image height and width converted to binary32 (vcvt.f32.s32), compared in binary32
        float height = imageHeight, width = imageWidth;
        var found = new List<Vec2>();
        for (int i = 0; i < Clusters - 1; i++)
            for (int j = i + 1; j < Clusters; j++)
            {
                var (y, x) = Intersect(fits[i], fits[j]);
                // 0x008A6B6A..0x008A6BA0: x >= 0, y < height, y >= 0, x < width, each a binary32 compare that a NaN fails
                if (!(x >= 0f)) continue;
                if (!(y < height)) continue;
                if (!(y >= 0f)) continue;
                if (!(x < width)) continue;
                found.Add(new Vec2(x, y));
            }
        if (found.Count != 4) return null;

        var clockwise = ComputeClockwiseCorners(found);
        var rounded = new Vec2[4];
        for (int i = 0; i < 4; i++) rounded[i] = new Vec2(RoundToS16((float)clockwise[i].X), RoundToS16((float)clockwise[i].Y));
        return rounded;
    }
}
