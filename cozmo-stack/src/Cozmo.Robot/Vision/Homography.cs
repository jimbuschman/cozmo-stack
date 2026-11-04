namespace Cozmo.Robot.Vision;

/// <summary>A planar homography (3x3, row-major), the engine's <c>ComputeHomographyFromQuad</c> result.</summary>
public sealed class Homography
{
    public double[] H { get; }

    public Homography(double[] h)
    {
        if (h.Length != 9) throw new ArgumentException("9 values", nameof(h));
        H = h;
    }

    public Vec2 Apply(Vec2 p)
    {
        double w = H[6] * p.X + H[7] * p.Y + H[8];
        if (Math.Abs(w) < 1e-12) w = 1e-12;
        return new Vec2((H[0] * p.X + H[1] * p.Y + H[2]) / w, (H[3] * p.X + H[4] * p.Y + H[5]) / w);
    }

    /// <summary>
    /// The same product without the divide, so a caller can see the third component: the engine's
    /// image-to-ground mapping refuses a point whose z comes out at zero or below (0x006AE0DC), which is
    /// what a ray that never meets the ground in front of the robot gives.
    /// </summary>
    public (double X, double Y, double W) ApplyHomogeneous(double x, double y) =>
        (H[0] * x + H[1] * y + H[2], H[3] * x + H[4] * y + H[5], H[6] * x + H[7] * y + H[8]);

    public Homography Inverse()
    {
        var m = H;
        double det = m[0] * (m[4] * m[8] - m[5] * m[7]) - m[1] * (m[3] * m[8] - m[5] * m[6]) + m[2] * (m[3] * m[7] - m[4] * m[6]);
        if (Math.Abs(det) < 1e-18) throw new InvalidOperationException("singular homography");
        var inv = new[]
        {
            (m[4] * m[8] - m[5] * m[7]) / det, (m[2] * m[7] - m[1] * m[8]) / det, (m[1] * m[5] - m[2] * m[4]) / det,
            (m[5] * m[6] - m[3] * m[8]) / det, (m[0] * m[8] - m[2] * m[6]) / det, (m[2] * m[3] - m[0] * m[5]) / det,
            (m[3] * m[7] - m[4] * m[6]) / det, (m[1] * m[6] - m[0] * m[7]) / det, (m[0] * m[4] - m[1] * m[3]) / det,
        };
        return new Homography(inv);
    }

    /// <summary>
    /// The homography mapping the unit square TL(0,0), BL(0,1), TR(1,0), BR(1,1) onto four points given in
    /// that order (the quad and marker corner convention throughout this namespace).
    /// </summary>
    public static Homography FromUnitSquare(Vec2[] corners)
    {
        if (corners.Length != 4) throw new ArgumentException("4 corners", nameof(corners));
        var src = new[] { new Vec2(0, 0), new Vec2(0, 1), new Vec2(1, 0), new Vec2(1, 1) };
        return FromPoints(src, corners);
    }

    /// <summary>The binary32 0.1 of the reprojection check, the literal 0x3DCCCCCD at 0x008C9804 (vldr 0x008C96AE).</summary>
    internal static readonly float ReprojectionLimit = BitConverter.Int32BitsToSingle(0x3DCCCCCD);
    /// <summary>The binary32 -0.001 IsConvex adds to each cross product, the literal 0xBA83126F at 0x008C92B4 (vldr 0x008C9238).</summary>
    internal static readonly float ConvexSlack = BitConverter.Int32BitsToSingle(unchecked((int)0xBA83126F));

    /// <summary>
    /// <c>Transformations::ComputeHomographyFromQuad(const Quadrilateral&lt;s16&gt;&amp;, Array&lt;f32&gt;&amp; H, bool&amp; numericalFailure, MemoryStack)</c>
    /// 0x008C9036, the call <c>DetectFiducialMarkers</c> makes for every quad (0x00898E98). All binary32:
    /// <list type="number">
    /// <item>the s16 corners become f32 (<c>vcvt.f32.s32</c>, 0x008C9052..0x008C907C) and go to the f32 overload 0x008C909A, which builds
    /// the unit square (0,0), (0,1), (1,0), (1,1) as the source quad (0x008C90A2..0x008C90BA) and calls
    /// <c>ComputeHomographyFromQuads(src, dst)</c> 0x008C90D4;</item>
    /// <item>when the source or the destination is not convex (<c>Quadrilateral&lt;f32&gt;::IsConvex</c> 0x008C9224, called at 0x008C9104 and
    /// 0x008C910E) H is the identity and the failure flag is set (0x008C91D0..0x008C91F2);</item>
    /// <item>otherwise <c>Matrix::EstimateHomography&lt;f32&gt;</c> 0x008C9340 (see <see cref="EstimateHomography"/>).</item>
    /// </list>
    /// Returns H row-major (h22 = 1, or the identity on failure).
    /// </summary>
    // fidelity: M11-023
    internal static float[] ComputeFromQuadS16(ReadOnlySpan<short> quad, out bool numericalFailure)
    {
        var dst = new float[8];
        for (int i = 0; i < 8; i++) dst[i] = quad[i];
        var src = new float[] { 0f, 0f, 0f, 1f, 1f, 0f, 1f, 1f };
        numericalFailure = false;
        if (!IsConvex(src) || !IsConvex(dst))
        {
            numericalFailure = true;
            return Identity();
        }
        return EstimateHomography(src, dst, ref numericalFailure);
    }

    private static float[] Identity() => new[] { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f };

    /// <summary>
    /// <c>Quadrilateral&lt;f32&gt;::IsConvex</c> 0x008C9224: the corners in <c>ComputeClockwiseCorners&lt;f32&gt;</c> order (0x008A1324, see
    /// <see cref="ClockwiseCornersF32"/>), then for each corner i with the next two (i+1, i+2 mod 4) the binary32 value
    /// <c>((c1.y - c0.y) * (c2.x - c1.x) - (c1.x - c0.x) * (c2.y - c1.y)) + -0.001</c> (0x008C9280..0x008C929C); a value above zero is not
    /// convex (<c>ble</c> 0x008C92A8 continues, so zero, a negative value and NaN pass).
    /// </summary>
    // fidelity: M11-023
    internal static bool IsConvex(float[] quad)
    {
        var c = ClockwiseCornersF32(quad);
        for (int i = 0; i < 4; i++)
        {
            int i1 = (i + 1) % 4, i2 = (i + 2) % 4;
            float ax = c[2 * i1] - c[2 * i];          // s10
            float by = c[2 * i2 + 1] - c[2 * i1 + 1]; // s8
            float bx = c[2 * i2] - c[2 * i1];         // s2
            float ay = c[2 * i1 + 1] - c[2 * i + 1];  // s4
            float p = ax * by;                        // s6
            float q = ay * bx;                        // s2
            float v = (q - p) + ConvexSlack;
            if (v > 0f) return false;
        }
        return true;
    }

    /// <summary>
    /// <c>Quadrilateral&lt;f32&gt;::ComputeClockwiseCorners&lt;f32&gt;</c> 0x008A1324: the centroid is the binary32 sum of the x (and of the y)
    /// from 0 in corner order, times 0.25 (0x008A1392..0x008A13C2); each corner's angle is <c>atan2f(y - cy, x - cx)</c>, or 0 when both
    /// differences are exactly zero (0x008A13DA..0x008A13FC); <c>Matrix::InsertionSort</c> 0x008A1442 orders the corners by ascending angle,
    /// carrying the indices (a strict compare, so equal angles keep their order). atan2f is the host's libm, as the oracle's (the stated
    /// libm boundary of NativeMarkerDetectorOracleTests): the double atan2 rounded to binary32.
    /// </summary>
    // fidelity: M11-023
    internal static float[] ClockwiseCornersF32(float[] quad)
    {
        float sx = 0f, sy = 0f;
        for (int i = 0; i < 4; i++) { sx += quad[2 * i]; sy += quad[2 * i + 1]; }
        float cx = sx * 0.25f, cy = sy * 0.25f;
        var angle = new float[4];
        var index = new int[] { 0, 1, 2, 3 };
        for (int i = 0; i < 4; i++)
        {
            float dx = quad[2 * i] - cx, dy = quad[2 * i + 1] - cy;
            angle[i] = dx == 0f && dy == 0f ? 0f : (float)Math.Atan2(dy, dx);
        }
        for (int i = 1; i < 4; i++)
        {
            float key = angle[i]; int ki = index[i]; int j = i - 1;
            while (j >= 0 && angle[j] > key) { angle[j + 1] = angle[j]; index[j + 1] = index[j]; j--; }
            angle[j + 1] = key; index[j + 1] = ki;
        }
        var outp = new float[8];
        for (int i = 0; i < 4; i++) { outp[2 * i] = quad[2 * index[i]]; outp[2 * i + 1] = quad[2 * index[i] + 1]; }
        return outp;
    }

    /// <summary>
    /// <c>Matrix::EstimateHomography&lt;f32&gt;(original, transformed, H, numericalFailure, scratch)</c> 0x008C9340, for the four points of
    /// the unit square (<paramref name="src"/>) and the quad (<paramref name="dst"/>), all binary32:
    /// <list type="number">
    /// <item>H is set to the identity first (0x008C93FE..0x008C942C);</item>
    /// <item>A (8 x 8) has, per point i with (x, y) = src and (x', y') = dst, row 2i = [0, 0, 0, -x, -y, -1, x·y', y·y'] and row 2i+1 =
    /// [x, y, 1, 0, 0, 0, -(x·x'), -(y·x')]; b has b[2i] = -y' and b[2i+1] = x' (0x008C9484..0x008C9516);</item>
    /// <item>AtA = <c>Matrix::Multiply</c>(Aᵗ, A) 0x008C3A20 and Atb = <c>Matrix::MultiplyTranspose</c>(Aᵗ, b) 0x008BE3F4, both summing in
    /// blocks of four, <c>s += ((p0 + p1) + p2) + p3</c> from 0 (see <see cref="Dot"/>);</item>
    /// <item><c>Matrix::SolveLeastSquaresWithCholesky&lt;f32&gt;</c> 0x0088DE68 solves AtA·x = Atb (<see cref="CornerRefinement.SolveLeastSquaresWithCholesky"/>);
    /// a pivot under FLT_EPSILON sets the failure flag and returns with H still the identity (0x008C95E8..0x008C95EC);</item>
    /// <item>H = [x0 x1 x2; x3 x4 x5; x6 x7 1] (0x008C95F0..0x008C9660);</item>
    /// <item>with four points, each source point is mapped by H (<c>Multiply</c> of H and (x, y, 1), sum from 0 in order), divided by the
    /// third component (<c>vdiv.f32</c>, 0x008C96F2/0x008C970C), and its distance to the destination point is
    /// <c>vsqrt.f32((x' - u)² + (y' - v)²)</c> (0x008C972E..0x008C9742); one distance above 0.1 (0x3DCCCCCD) sets the flag and makes H the
    /// identity (0x008C9774..0x008C97A0).</item>
    /// </list>
    /// </summary>
    // fidelity: M11-023
    // fidelity: M11-103
    internal static float[] EstimateHomography(float[] src, float[] dst, ref bool numericalFailure)
    {
        var h = Identity();
        var a = new float[8, 8];
        var b = new float[8];
        for (int i = 0; i < 4; i++)
        {
            float x = src[2 * i], y = src[2 * i + 1], xp = dst[2 * i], yp = dst[2 * i + 1];
            int r = 2 * i;
            a[r, 0] = 0f; a[r, 1] = 0f; a[r, 2] = 0f; a[r, 3] = -x; a[r, 4] = -y; a[r, 5] = -1f; a[r, 6] = x * yp; a[r, 7] = y * yp;
            a[r + 1, 0] = x; a[r + 1, 1] = y; a[r + 1, 2] = 1f; a[r + 1, 3] = 0f; a[r + 1, 4] = 0f; a[r + 1, 5] = 0f;
            a[r + 1, 6] = -(x * xp); a[r + 1, 7] = -(y * xp);
            b[r] = -yp; b[r + 1] = xp;
        }
        var ata = new float[8, 8];
        var atb = new float[8];
        var ci = new float[8];
        var cj = new float[8];
        for (int i = 0; i < 8; i++)
        {
            for (int k = 0; k < 8; k++) ci[k] = a[k, i];
            for (int j = 0; j < 8; j++)
            {
                for (int k = 0; k < 8; k++) cj[k] = a[k, j];
                ata[i, j] = Dot(ci, cj);
            }
            atb[i] = Dot(ci, b);
        }
        var x8 = CornerRefinement.SolveLeastSquaresWithCholesky(ata, atb, out bool failed);
        if (failed)
        {
            numericalFailure = true;
            return h;
        }
        h = new[] { x8[0], x8[1], x8[2], x8[3], x8[4], x8[5], x8[6], x8[7], 1f };
        for (int k = 0; k < 4; k++)
        {
            float x = src[2 * k], y = src[2 * k + 1];
            float o0 = ((0f + h[0] * x) + h[1] * y) + h[2] * 1f;
            float o1 = ((0f + h[3] * x) + h[4] * y) + h[5] * 1f;
            float o2 = ((0f + h[6] * x) + h[7] * y) + h[8] * 1f;
            float u = o0 / o2, v = o1 / o2;
            float dx = dst[2 * k] - u, dy = dst[2 * k + 1] - v;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > ReprojectionLimit)
            {
                numericalFailure = true;
                return Identity();
            }
        }
        return h;
    }

    /// <summary>
    /// The inner product <c>Matrix::Multiply&lt;f32&gt;</c> 0x008C3A20 and <c>Matrix::MultiplyTranspose&lt;f32&gt;</c> 0x008BE3F4 compute: from
    /// s = 0, blocks of four add <c>((p0 + p1) + p2) + p3</c> to s while a whole block fits (0x008C3AB8..0x008C3B08), then the remaining
    /// products are added one by one (0x008C3B22..0x008C3B3A).
    /// </summary>
    // fidelity: M11-023
    internal static float Dot(float[] u, float[] v)
    {
        int n = u.Length;
        float s = 0f;
        int k = 0;
        if (n >= 4)
        {
            do
            {
                float t = u[k] * v[k] + u[k + 1] * v[k + 1];
                t = t + u[k + 2] * v[k + 2];
                t = t + u[k + 3] * v[k + 3];
                s = s + t;
                k += 4;
            } while (k < n - 3);
            k = n & ~3;
        }
        for (; k < n; k++) s = s + u[k] * v[k];
        return s;
    }

    /// <summary>Direct linear transform from ≥ 4 correspondences (least squares when more).</summary>
    public static Homography FromPoints(Vec2[] src, Vec2[] dst)
    {
        if (src.Length != dst.Length || src.Length < 4) throw new ArgumentException("need at least 4 matching points");
        int n = src.Length;
        // Solve A h = b with h8 = 1 (8 unknowns); fine for the non-degenerate quads the detector produces.
        var a = new double[2 * n, 8];
        var b = new double[2 * n];
        for (int i = 0; i < n; i++)
        {
            double x = src[i].X, y = src[i].Y, u = dst[i].X, v = dst[i].Y;
            a[2 * i, 0] = x; a[2 * i, 1] = y; a[2 * i, 2] = 1; a[2 * i, 6] = -u * x; a[2 * i, 7] = -u * y; b[2 * i] = u;
            a[2 * i + 1, 3] = x; a[2 * i + 1, 4] = y; a[2 * i + 1, 5] = 1; a[2 * i + 1, 6] = -v * x; a[2 * i + 1, 7] = -v * y; b[2 * i + 1] = v;
        }
        var h = LinearAlgebra.SolveLeastSquares(a, b);
        return new Homography(new[] { h[0], h[1], h[2], h[3], h[4], h[5], h[6], h[7], 1.0 });
    }
}

/// <summary>Small dense linear algebra for the pose and homography solvers.</summary>
internal static class LinearAlgebra
{
    /// <summary>Least-squares solution of A x = b through the normal equations with partial pivoting.</summary>
    public static double[] SolveLeastSquares(double[,] a, double[] b)
    {
        int rows = a.GetLength(0), cols = a.GetLength(1);
        var ata = new double[cols, cols];
        var atb = new double[cols];
        for (int i = 0; i < cols; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                double s = 0;
                for (int k = 0; k < rows; k++) s += a[k, i] * a[k, j];
                ata[i, j] = s;
            }
            double t = 0;
            for (int k = 0; k < rows; k++) t += a[k, i] * b[k];
            atb[i] = t;
        }
        return Solve(ata, atb);
    }

    /// <summary>Gaussian elimination with partial pivoting for a square system.</summary>
    public static double[] Solve(double[,] a, double[] b)
    {
        int n = b.Length;
        var m = (double[,])a.Clone();
        var r = (double[])b.Clone();
        for (int c = 0; c < n; c++)
        {
            int piv = c;
            for (int i = c + 1; i < n; i++) if (Math.Abs(m[i, c]) > Math.Abs(m[piv, c])) piv = i;
            if (Math.Abs(m[piv, c]) < 1e-14) throw new InvalidOperationException("singular system");
            if (piv != c)
            {
                for (int j = 0; j < n; j++) (m[c, j], m[piv, j]) = (m[piv, j], m[c, j]);
                (r[c], r[piv]) = (r[piv], r[c]);
            }
            for (int i = c + 1; i < n; i++)
            {
                double f = m[i, c] / m[c, c];
                if (f == 0) continue;
                for (int j = c; j < n; j++) m[i, j] -= f * m[c, j];
                r[i] -= f * r[c];
            }
        }
        var x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double s = r[i];
            for (int j = i + 1; j < n; j++) s -= m[i, j] * x[j];
            x[i] = s / m[i, i];
        }
        return x;
    }
}
