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
