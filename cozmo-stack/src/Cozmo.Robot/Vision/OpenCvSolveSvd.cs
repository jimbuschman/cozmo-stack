namespace Cozmo.Robot.Vision;

// fidelity: M11-029
/// <summary>
/// <c>cv::solve(A, b, x, DECOMP_SVD)</c> on <c>CV_32F</c> matrices with <c>nb == 1</c>, the call
/// <c>ExtractLineFitsPeaks</c> makes at 0x008A68E0 (<c>blx 0x4BEA90</c> = <c>cv::solve</c>, method 1).
/// Every step is read off the shipped <c>libopencv_core.so</c> (OpenCV 3.1.0 build); nothing here is a library
/// from memory:
/// <list type="bullet">
/// <item><c>cv::solve</c> 0x00059B78: <c>a = transpose(A)</c> (n x m), <c>JacobiSVD</c> on it, <c>u = a</c>, then
/// <c>SVBkSb</c> (calls 0x0005A128/0x00058FE8 and 0x0005A0F4/0x00057110 in the float arm).</item>
/// <item><c>JacobiSVDImpl_&lt;float&gt;</c> 0x00058FE8, called with <c>minval = FLT_MIN</c> (the double
/// 0x3810000000000000, literal 0x0005A3A8) and <c>eps = FLT_EPSILON*2</c> (the float 0x34800000, set at 0x0005A110):
/// sums of squares and dot products in double (<c>vmla.f64</c>, product rounded before the add), the plane rotation
/// in float (<c>vmul.f32</c>/<c>vmla.f32</c>, each product rounded before the add), the <c>hypot</c> of 0x00056EB0.</item>
/// <item><c>SVBkSbImpl_&lt;float&gt;</c> 0x00057110: threshold = (sum of w) * 2^-51 (literal 0x3CC00000_00000000 at 0x000571A6), the
/// per-term products in float, the sums in double, <c>x[j] = (float)(x[j] + s*v[j])</c>.</item>
/// </list>
/// The only library call these use is <c>sqrt</c> (0x0001FB7C, IEEE-exact, the same as <see cref="Math.Sqrt"/>).
/// </summary>
internal static class OpenCvSolveSvd
{
    /// <summary>The float passed as <c>eps</c> to <c>JacobiSVDImpl_&lt;float&gt;</c>: 0x34800000 (2^-22), 0x0005A110.</summary>
    internal static readonly float JacobiEps = BitConverter.Int32BitsToSingle(0x34800000);
    /// <summary>The <c>minval</c> double: 0x3810000000000000 (2^-126), literal at 0x0005A3A8.</summary>
    internal static readonly double JacobiMinVal = BitConverter.Int64BitsToDouble(0x3810000000000000L);
    /// <summary>The SVBkSb threshold factor: 0x3CC0000000000000 (2^-51), literal at 0x000571A6.</summary>
    internal static readonly double SvbkEps = BitConverter.Int64BitsToDouble(0x3CC0000000000000L);
    /// <summary><c>CV_RNG_COEFF</c>: the umull constant 0xF83F630A at 0x0005943C (4164903690).</summary>
    private const ulong RngCoeff = 4164903690UL;
    /// <summary>The RNG state the zero-singular-value branch starts from: 0x12345678 (literal at 0x000595B0).</summary>
    private const ulong RngSeed = 0x12345678UL;

    /// <summary>
    /// <c>cv::solve(A, b, x, DECOMP_SVD)</c> for a float <paramref name="m"/> x <paramref name="n"/> matrix
    /// <paramref name="a"/> (row-major) and a float m x 1 <paramref name="b"/>; returns the n x 1 <c>x</c>.
    /// <c>m &lt; n</c> is the engine's CV_Error "can not solve under-determined linear systems" (0x00059D14, status -5).
    /// </summary>
    internal static float[] Solve(float[] a, float[] b, int m, int n)
    {
        if (m < n) throw new ArgumentException("The function can not solve under-determined linear systems");
        // a = transpose(src), n rows of m
        var at = new float[n * m];
        for (int r = 0; r < m; r++)
            for (int c = 0; c < n; c++)
                at[c * m + r] = a[r * n + c];
        var w = new float[n];
        var vt = new float[n * n];
        JacobiSvd(at, w, vt, m, n, n);
        return Svbksb(m, n, w, at, vt, b);
    }

    /// <summary>The file-local <c>hypot(a, b)</c> of 0x00056EB0 (OpenCV's <c>cv::hypot</c>, not libm's).</summary>
    internal static double Hypot(double a, double b)
    {
        a = Math.Abs(a); b = Math.Abs(b);
        if (a > b)
        {
            b /= a;
            double t = 1.0 + b * b;
            return a * Math.Sqrt(t);
        }
        if (b > 0)
        {
            a /= b;
            double t = 1.0 + a * a;
            return b * Math.Sqrt(t);
        }
        return 0;
    }

    /// <summary><c>JacobiSVDImpl_&lt;float&gt;</c> 0x00058FE8: <paramref name="at"/> is n rows of m, <paramref name="vt"/> n x n, <paramref name="n1"/> = n.</summary>
    internal static void JacobiSvd(float[] at, float[] wOut, float[] vt, int m, int n, int n1)
    {
        var w = new double[n];
        int maxIter = Math.Max(m, 30);                                  // 0x0005903A..0x0005905A
        for (int i = 0; i < n; i++)
        {
            double sd = 0;
            for (int k = 0; k < m; k++) { float t = at[i * m + k]; sd += (double)t * t; }
            w[i] = sd;
            for (int k = 0; k < n; k++) vt[i * n + k] = 0;
            vt[i * n + i] = 1f;
        }

        for (int iter = 0; iter < maxIter; iter++)
        {
            bool changed = false;
            for (int i = 0; i < n - 1; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double a = w[i], p = 0, b = w[j];
                    int ai = i * m, aj = j * m;
                    for (int k = 0; k < m; k++) p += (double)at[ai + k] * at[aj + k];
                    double ab = a * b;
                    if (Math.Abs(p) <= (double)JacobiEps * Math.Sqrt(ab)) continue;     // 0x00059132..0x0005915A

                    p *= 2;
                    double beta = a - b, gamma = Hypot(p, beta);
                    float c, s;
                    if (beta < 0)
                    {
                        double delta = (gamma - beta) * 0.5;
                        s = (float)Math.Sqrt(delta / gamma);
                        c = (float)(p / (gamma * s * 2));
                    }
                    else
                    {
                        c = (float)Math.Sqrt((gamma + beta) / (gamma * 2));
                        s = (float)(p / (gamma * c * 2));
                    }

                    a = b = 0;
                    for (int k = 0; k < m; k++)
                    {
                        float ak = at[ai + k], bk = at[aj + k];
                        float t0 = s * bk + c * ak;
                        float t1 = c * bk + (-s) * ak;
                        at[ai + k] = t0; at[aj + k] = t1;
                        a += (double)t0 * t0; b += (double)t1 * t1;
                    }
                    w[i] = a; w[j] = b;
                    changed = true;

                    int vi = i * n, vj = j * n;
                    for (int k = 0; k < n; k++)
                    {
                        float xi = vt[vi + k], xj = vt[vj + k];
                        float t0 = s * xj + c * xi;
                        float t1 = c * xj + (-s) * xi;
                        vt[vi + k] = t0; vt[vj + k] = t1;
                    }
                }
            if (!changed) break;
        }

        for (int i = 0; i < n; i++)
        {
            double sd = 0;
            for (int k = 0; k < m; k++) { float t = at[i * m + k]; sd += (double)t * t; }
            w[i] = Math.Sqrt(sd);
        }

        for (int i = 0; i < n - 1; i++)
        {
            int j = i;
            for (int k = i + 1; k < n; k++) if (w[j] < w[k]) j = k;
            if (i != j)
            {
                (w[i], w[j]) = (w[j], w[i]);
                for (int k = 0; k < m; k++) (at[i * m + k], at[j * m + k]) = (at[j * m + k], at[i * m + k]);
                for (int k = 0; k < n; k++) (vt[i * n + k], vt[j * n + k]) = (vt[j * n + k], vt[i * n + k]);
            }
        }
        for (int i = 0; i < n; i++) wOut[i] = (float)w[i];

        // 0x000593CA..0x00059570: the left singular vectors; a (near) zero singular value gets a random vector
        // (RNG 0x12345678, the 0x00059428 loop) orthogonalised twice against the earlier rows, 100 tries at most.
        ulong rng = RngSeed;
        for (int i = 0; i < n1; i++)
        {
            double sd = i < n ? w[i] : 0;
            int tries = 100;
            while (!(sd > JacobiMinVal))
            {
                float val0 = (float)(1.0 / m);
                for (int k = 0; k < m; k++)
                {
                    rng = (ulong)(uint)rng * RngCoeff + (rng >> 32);
                    at[i * m + k] = ((uint)rng & 256u) != 0 ? val0 : -val0;
                }
                for (int pass = 0; pass < 2; pass++)
                    for (int j = 0; j < i; j++)
                    {
                        sd = 0;
                        for (int k = 0; k < m; k++) { float prod = at[i * m + k] * at[j * m + k]; sd += prod; }
                        float asum = 0f;
                        for (int k = 0; k < m; k++)
                        {
                            float t = (float)((double)at[i * m + k] - sd * at[j * m + k]);
                            at[i * m + k] = t;
                            asum += Math.Abs(t);
                        }
                        float thr = JacobiEps * 100f;                    // vmul.f32 s24 * 0x42C80000
                        asum = asum > thr ? 1f / asum : 0f;
                        for (int k = 0; k < m; k++) at[i * m + k] *= asum;
                    }
                sd = 0;
                for (int k = 0; k < m; k++) { float t = at[i * m + k]; sd += (double)t * t; }
                sd = Math.Sqrt(sd);
                if (--tries == 0) break;
            }
            float scale = sd > JacobiMinVal ? (float)(1.0 / sd) : 0f;       // 0x0005953C..0x0005954E
            for (int k = 0; k < m; k++) at[i * m + k] *= scale;
        }
    }

    /// <summary><c>SVBkSbImpl_&lt;float&gt;</c> 0x00057110 with uT set, nb = 1, a right-hand side present, incw = 1.</summary>
    internal static float[] Svbksb(int m, int n, float[] w, float[] u, float[] v, float[] b)
    {
        var x = new float[n];
        int nm = Math.Min(m, n);
        double threshold = 0;
        for (int i = 0; i < nm; i++) threshold += w[i];
        threshold *= SvbkEps;
        for (int i = 0; i < nm; i++)
        {
            double wi = w[i];
            if (Math.Abs(wi) <= threshold) continue;
            wi = 1.0 / wi;
            double s = 0;
            for (int j = 0; j < m; j++) { float prod = u[i * m + j] * b[j]; s += prod; }
            s *= wi;
            for (int j = 0; j < n; j++) x[j] = (float)(x[j] + s * v[i * n + j]);
        }
        return x;
    }
}
