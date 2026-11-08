namespace Cozmo.Robot.Vision;

// fidelity: M11-099
/// <summary>
/// The shipped iterative <c>cv::solvePnP</c> as the engine calls it from <c>Camera::ComputeObjectPoseHelper</c>
/// (useExtrinsicGuess false, method 0; engine 0x0085D89E..0x0085D8A2), reduced to the inputs that call makes: Point3f object
/// points, Point2f image points, a binary32 K and a binary32 distortion vector. Built from
/// <c>re-analysis/research/20261004-solvepnp-extraction.md</c> and its instruction companion; the row ids are in the comments.
/// Addresses are <c>libopencv_calib3d.so</c> unless a row says otherwise.
/// Each `a += b*c` below is the shipped VFP multiply then add: RyuJIT does not contract it into a fused operation, so it keeps the
/// separately rounded product. Do not regroup, the accumulation orders are the point.
/// EQUIVALENT_IMPLEMENTATION: libm's cos, sin, acos and exp (bionic is not shipped, M13-023(e)). sqrt is IEEE-exact.
/// Proof: <c>NativeSolvePnPOracleTests</c> against the shipped libraries under Unicorn (<c>re-analysis/tools/emu/emu_solvepnp.py</c>).
/// </summary>
internal static class NativeSolvePnP
{
    internal readonly record struct Output(double[] Rvec, double[] Tvec, int Iterations);

    private static readonly double DblEpsilon = BitConverter.Int64BitsToDouble(0x3CB0000000000000);   // literal 0x00017868, core 0xBE1E8
    private static readonly double PlanarRatio = BitConverter.Int64BitsToDouble(0x3F50624DD2F1A9FC);   // I02 literal 0x00017850
    private static readonly double BasisDegenerate = BitConverter.Int64BitsToDouble(0x3DDB7CDFD9D7BDBB); // I03 literal 0x00017858
    private static readonly double InverseNormFallback = BitConverter.Int64BitsToDouble(0x4330000000000000); // I05 literal 0x00017870, 1/DBL_EPSILON
    // The row's prose gives one fallback for both; the sum takes this one, 2/DBL_EPSILON (vldr d10 at 0x00017890, literal 0x00017B30).
    private static readonly double SumNormFallback = BitConverter.Int64BitsToDouble(0x4340000000000000);
    private static readonly double LmEpsilon = BitConverter.Int64BitsToDouble(0x3E80000000000000);     // O07, assembled at 0x00017BA8..0x00017BBA
    private static readonly double Ln10 = BitConverter.Int64BitsToDouble(0x40026BB1BBB55516);          // L09 literal 0x00025A30
    private static readonly double SmallS = BitConverter.Int64BitsToDouble(0x3EE4F8B588E368F1);        // R06
    private static readonly double SvdRankCutoff = BitConverter.Int64BitsToDouble(0x3CC0000000000000); // B01, 2*DBL_EPSILON
    private const int LmMaxIterations = 20, LmMaxExponent = 16;

    /// <summary>Test seam for the libm boundary above: when set on the calling thread, every cos, sin, acos and exp call goes to it instead of the
    /// host's libm, so the oracle test can replay the exact libm results its fixture's oracle used and compare all other arithmetic bit for bit on
    /// any host (host libms differ in the last bit between Windows builds). Null in production.</summary>
    [ThreadStatic] internal static Func<string, double, double>? LibmOverride;

    private static double Libm(string name, double x, Func<double, double> host) => LibmOverride is { } f ? f(name, x) : host(x);

    /// <summary>A planar set with more than four points reaches the homography's ten-iteration LM refinement (H03), which has no row.</summary>
    internal static bool NeedsUnextractedHomographyRefinement(int points) => points > 4;

    /// <summary>The rotation matrix of a solved rotation vector, for the stack's reprojection diagnostic (no engine consumer).</summary>
    internal static double[] RotationMatrix(double[] rvec)
    {
        var r = new double[9];
        VectorToMatrix(rvec, r, null);
        return r;
    }

    internal static Output Solve(float[] objectPoints, float[] imagePoints, float[] k, float[] distortion)
    {
        int count = objectPoints.Length / 3;
        // Q05..Q07, Q08: exact widening, order preserved.
        var mObj = new double[count * 3];
        for (int i = 0; i < mObj.Length; i++) mObj[i] = objectPoints[i];
        var mImg = new double[count * 2];
        for (int i = 0; i < mImg.Length; i++) mImg[i] = imagePoints[i];
        var dist = Coefficients(distortion);
        bool hasDistortion = distortion.Length != 0;                                // Q03: null when rows*cols == 0
        double fx = k[0], fy = k[4], cx = k[2], cy = k[5];                           // U04, P03: no skew read

        var norm = Undistort(mImg, fx, fy, cx, cy, dist, hasDistortion);             // I01: R null, P identity
        var param = new double[6];
        InitialEstimate(mObj, norm, count, param);
        return Refine(mObj, mImg, fx, fy, cx, cy, dist, count, param);
    }

    /// <summary>U02/P02: the fourteen-slot coefficient array, zeros past the supplied length. Forward tilt (P06) stays out of scope: see the throw.</summary>
    private static double[] Coefficients(float[] d)
    {
        if (d.Length is not (0 or 4 or 5 or 8 or 12 or 14)) throw new ArgumentException($"distortion vector of {d.Length} coefficients: cvUndistortPoints asserts (U02)", nameof(d));
        var k = new double[14];
        for (int i = 0; i < d.Length; i++) k[i] = d[i];
        // The forward tilt matrix (0x14808..0x14AAC, P06) is not in the inventory. The robot's CLAD calibration has 8 coefficients, so this needs a caller that does not exist.
        if (k[12] != 0 || k[13] != 0) throw new NotSupportedException("MISSING: M11-054 forward tilt construction 0x14808..0x14AAC (P06) has no row; tilt coefficients are unsupported");
        return k;
    }

    // -- cvUndistortPoints (imgproc 0x000B9D40), R null, P identity

    private static double[] Undistort(double[] src, double fx, double fy, double cx, double cy, double[] k, bool hasDistortion)
    {
        int n = src.Length / 2;
        var dst = new double[n * 2];
        int iterations = hasDistortion ? 5 : 1;                                      // U02
        double ifx = 1.0 / fx, ify = 1.0 / fy;                                       // U04
        double k0 = k[0], k1 = k[1], p1 = k[2], p2 = k[3], k2 = k[4], k3 = k[5], k4 = k[6], k5 = k[7], s1 = k[8], s2 = k[9], s3 = k[10], s4 = k[11];
        double p1Twice = p1 + p1, p2Twice = p2 + p2;                                 // 0x000BA24A, 0x000BA24E
        for (int i = 0; i < n; i++)
        {
            double xs = (src[i * 2] - cx) * ifx, ys = (src[i * 2 + 1] - cy) * ify;   // U05
            // U06: the identity inverse tilt, each row from zero over columns 0,1,2 of (x,y,1).
            double vx = 0; vx += 1.0 * xs; vx += 0.0 * ys; vx += 0.0;
            double vy = 0; vy += 0.0 * xs; vy += 1.0 * ys; vy += 0.0;
            double vz = 0; vz += 0.0 * xs; vz += 0.0 * ys; vz += 1.0;
            double invZ = vz == 0 ? 1.0 : 1.0 / vz;
            double x0 = invZ * vx, y0 = invZ * vy, x = x0, y = y0;
            for (int it = 0; it < iterations; it++)
            {
                double r2 = y * y; r2 += x * x;                                       // U07
                double a = k4; a += k5 * r2;
                double b = k3; b += a * r2;
                double num = 1.0; num += b * r2;
                a = k1; a += k2 * r2;
                b = k0; b += a * r2;
                double den = 1.0; den += b * r2;
                double yTwice = y + y;
                double icdist = num / den;
                double a3 = r2; a3 += yTwice * y;
                double deltaY = p2Twice * x;                                          // U08
                deltaY = deltaY * y; deltaY += p1 * a3; deltaY += s3 * r2;
                double t = s4 * r2; deltaY += t * r2;
                double xTwice = x + x;
                double a2 = r2; a2 += xTwice * x;
                double px = p1Twice * x;
                double deltaX = p2 * a2; deltaX += px * y; deltaX += s1 * r2;
                t = s2 * r2; deltaX += t * r2;
                double ny = y0 - deltaY, nx = x0 - deltaX;
                y = ny * icdist; x = nx * icdist;
            }
            // U09 with the identity: each row starts y*column1, then x*column0, then column2.
            double xx = y * 0.0; xx += x * 1.0; xx = xx + 0.0;
            double yy = y * 1.0; yy += x * 0.0; yy = yy + 0.0;
            double ww = y * 0.0; ww += x * 0.0; ww = ww + 1.0;
            ww = 1.0 / ww;
            dst[i * 2] = xx * ww; dst[i * 2 + 1] = yy * ww;
        }
        return dst;
    }

    // -- the initial estimate (I02..I07)

    private static void InitialEstimate(double[] obj, double[] norm, int count, double[] param)
    {
        var mean = Mean(obj, count);
        var mm = MulTransposed(obj, count, 3, mean);                                  // I02, G04
        var at = Transpose(mm, 3, 3);
        var w = new double[3]; var v = new double[9];
        NativeRodrigues.JacobiSvd(at, w, v, 3, 3, 3);                                 // I02 flags 5: V carries Vt (S12)
        bool planar = w[2] / w[1] < PlanarRatio || count <= 3;                        // unordered goes to the count test
        if (planar) Planar(obj, norm, count, mean, v, param);
        else Dlt(obj, norm, count, param);
    }

    private static void Planar(double[] obj, double[] norm, int count, double[] mean, double[] v, double[] param)
    {
        double basis = v[5] * v[5]; basis += v[2] * v[2];                             // I03 0x000175F6, 0x000175FE
        if (basis < BasisDegenerate) v = new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        if (Determinant(v) < 0) for (int i = 0; i < 9; i++) v[i] = v[i] * -1.0 + 0.0; // cvScale: convertTo's src*alpha+beta
        var tt = new double[3];                                                       // I03 cvGEMM(V, Mc, -1, 0, 0, T, GEMM_B_T): G07, G08
        for (int r = 0; r < 3; r++)
        {
            double s = 0; for (int c = 0; c < 3; c++) s += v[r * 3 + c] * mean[c];
            s = s + 0.0; s = s + 0.0; s = s + 0.0;
            tt[r] = s * -1.0;
        }
        var xy = new double[count * 2];                                               // I04
        for (int i = 0; i < count; i++)
        {
            double px = obj[i * 3], py = obj[i * 3 + 1], pz = obj[i * 3 + 2];
            double d = v[1] * py; d += v[0] * px; d += v[2] * pz; xy[i * 2] = d + tt[0];
            d = v[4] * py; d += v[3] * px; d += v[5] * pz; xy[i * 2 + 1] = d + tt[1];
        }
        var h = Homography(xy, norm, count);                                          // H01: an empty result leaves H zeroed
        double[] matR; var t = new double[3]; var r0 = new double[3];
        if (AllFinite(h))                                                             // I05 cvCheckArr flag 2
        {
            double h1 = h[3] * h[3]; h1 += h[0] * h[0]; h1 += h[6] * h[6]; h1 = Math.Sqrt(h1);
            double h2 = h[4] * h[4]; h2 += h[1] * h[1]; h2 += h[7] * h[7]; h2 = Math.Sqrt(h2);
            double s1 = h1 < DblEpsilon ? InverseNormFallback : 1.0 / h1;
            for (int i = 0; i < 3; i++) h[i * 3] = h[i * 3] * s1 + 0.0;
            double s2 = h2 < DblEpsilon ? InverseNormFallback : 1.0 / h2;
            for (int i = 0; i < 3; i++) h[i * 3 + 1] = h[i * 3 + 1] * s2 + 0.0;
            double sum = h1 + h2;
            double s3 = sum < DblEpsilon ? SumNormFallback : 2.0 / sum;
            for (int i = 0; i < 3; i++) t[i] = h[i * 3 + 2] * s3 + 0.0;
            double ax = h[0], ay = h[3], az = h[6], bx = h[1], by = h[4], bz = h[7];  // cvCrossProduct of the two columns
            h[2] = ay * bz - az * by; h[5] = az * bx - ax * bz; h[8] = ax * by - ay * bx;
            MatrixToVector(h, r0);
            VectorToMatrix(r0, h, null);
            // I05: cvGEMM(H, T, 1, t, 1, t, 0) and cvGEMM(H, V, 1, 0, 0, R, 0): G01.
            var nt = new double[3];
            for (int i = 0; i < 3; i++)
            {
                double d = h[i * 3 + 1] * tt[1]; d += h[i * 3] * tt[0]; d += h[i * 3 + 2] * tt[2];
                nt[i] = d * 1.0 + t[i] * 1.0;
            }
            t = nt;
            matR = Gemm3x3(h, v);
        }
        else
        {
            matR = new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };                        // not a failure: continues into LM
        }
        MatrixToVector(matR, r0);
        Array.Copy(r0, 0, param, 0, 3); Array.Copy(t, 0, param, 3, 3);
    }

    private static void Dlt(double[] obj, double[] norm, int count, double[] param)
    {
        var l = new double[2 * count * 12];                                           // I06
        for (int i = 0; i < count; i++)
        {
            double mx = obj[i * 3], my = obj[i * 3 + 1], mz = obj[i * 3 + 2], x = -norm[i * 2], y = -norm[i * 2 + 1];
            int r = i * 24;
            l[r + 0] = mx; l[r + 1] = my; l[r + 2] = mz; l[r + 3] = 1;
            l[r + 8] = x * mx; l[r + 9] = x * my; l[r + 10] = x * mz; l[r + 11] = x;
            l[r + 16] = mx; l[r + 17] = my; l[r + 18] = mz; l[r + 19] = 1;
            l[r + 20] = y * mx; l[r + 21] = y * my; l[r + 22] = y * mz; l[r + 23] = y;
        }
        var ll = MulTransposed(l, 2 * count, 12, null);                               // G03
        var at = Transpose(ll, 12, 12);
        var lw = new double[12]; var lv = new double[144];
        NativeRodrigues.JacobiSvd(at, lw, lv, 12, 12, 12);
        var rrt = new double[12]; Array.Copy(lv, 11 * 12, rrt, 0, 12);                // last row as 3x4 (I07)
        double[] rr = { rrt[0], rrt[1], rrt[2], rrt[4], rrt[5], rrt[6], rrt[8], rrt[9], rrt[10] };
        if (Determinant(rr) < 0)
        {
            for (int i = 0; i < 12; i++) rrt[i] = rrt[i] * -1.0 + 0.0;
            rr = new[] { rrt[0], rrt[1], rrt[2], rrt[4], rrt[5], rrt[6], rrt[8], rrt[9], rrt[10] };
        }
        double sc = NormL2(rr, 3, 3);                                                 // a 3-column view of 3x4: not continuous, three planes (N01)
        var u = Transpose(rr, 3, 3);                                                  // flags 7: Ut and Vt (S11, S12)
        var w = new double[3]; var vt = new double[9];
        NativeRodrigues.JacobiSvd(u, w, vt, 3, 3, 3);
        var matR = GemmTransposeA3x3(u, vt);                                          // GEMM_A_T: G06, G09
        double scale = NormL2(matR, 9, 1) / sc;
        var r0 = new double[3];
        MatrixToVector(matR, r0);
        Array.Copy(r0, 0, param, 0, 3);
        param[3] = rrt[3] * scale + 0.0; param[4] = rrt[7] * scale + 0.0; param[5] = rrt[11] * scale + 0.0;
    }

    // -- cvFindHomography, the direct kernel (H01..H07)

    private static double[] Homography(double[] src, double[] dst, int count)
    {
        if (NeedsUnextractedHomographyRefinement(count))
            throw new NotSupportedException("MISSING: M11-054 homography LM refinement for a planar set above four points (0x0005C8CC..0x0005CBEC, H03) has no row");
        var bigM = new double[count * 2]; var m = new double[count * 2];              // H02: narrowed to binary32, then widened (H04)
        for (int i = 0; i < count * 2; i++) { bigM[i] = (float)src[i]; m[i] = (float)dst[i]; }
        double cmx = 0, cmy = 0, cMx = 0, cMy = 0;
        for (int i = 0; i < count; i++) { cmx += m[i * 2]; cmy += m[i * 2 + 1]; cMx += bigM[i * 2]; cMy += bigM[i * 2 + 1]; }
        cmx /= count; cmy /= count; cMx /= count; cMy /= count;
        double smx = 0, smy = 0, sMx = 0, sMy = 0;
        for (int i = 0; i < count; i++)
        {
            smx += Math.Abs(m[i * 2] - cmx); smy += Math.Abs(m[i * 2 + 1] - cmy);
            sMx += Math.Abs(bigM[i * 2] - cMx); sMy += Math.Abs(bigM[i * 2 + 1] - cMy);
        }
        var h = new double[9];
        if (Math.Abs(smx) < DblEpsilon || Math.Abs(smy) < DblEpsilon || Math.Abs(sMx) < DblEpsilon || Math.Abs(sMy) < DblEpsilon) return h; // H05 -> H03 -> H01
        smx = count / smx; smy = count / smy; sMx = count / sMx; sMy = count / sMy;
        double[] invHnorm = { 1.0 / smx, 0, cmx, 0, 1.0 / smy, cmy, 0, 0, 1 };
        double[] hnorm2 = { sMx, 0, -cMx * sMx, 0, sMy, -cMy * sMy, 0, 0, 1 };
        var ltl = new double[81];
        var lx = new double[9]; var ly = new double[9];
        for (int i = 0; i < count; i++)
        {
            double x = (m[i * 2] - cmx) * smx, y = (m[i * 2 + 1] - cmy) * smy;        // H06
            double bx = (bigM[i * 2] - cMx) * sMx, by = (bigM[i * 2 + 1] - cMy) * sMy;
            lx[0] = bx; lx[1] = by; lx[2] = 1; lx[3] = 0; lx[4] = 0; lx[5] = 0; lx[6] = -x * bx; lx[7] = -x * by; lx[8] = -x;
            ly[0] = 0; ly[1] = 0; ly[2] = 0; ly[3] = bx; ly[4] = by; ly[5] = 1; ly[6] = -y * bx; ly[7] = -y * by; ly[8] = -y;
            for (int j = 0; j < 9; j++)
                for (int k = j; k < 9; k++)
                {
                    double t = ly[j] * ly[k]; t += lx[j] * lx[k];
                    ltl[j * 9 + k] = t + ltl[j * 9 + k];
                }
        }
        CompleteSymmFromUpper(ltl, 9);                                                // H07, G10
        var eigW = new double[9]; var eigV = new double[81];
        JacobiEigen(ltl, eigW, eigV, 9);
        var h0 = new double[9]; Array.Copy(eigV, 8 * 9, h0, 0, 9);                    // the final row, the smallest eigenvalue (J07)
        var product = Gemm3x3(Gemm3x3(invHnorm, h0), hnorm2);
        double scale = 1.0 / product[8];
        for (int i = 0; i < 9; i++) h[i] = product[i] * scale + 0.0;
        return h;
    }

    /// <summary>J02..J07: the double Jacobi eigen solver (core 0x00057978), eigenvectors requested.</summary>
    private static void JacobiEigen(double[] a, double[] w, double[] v, int n)
    {
        for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) v[i * n + j] = 0; v[i * n + i] = 1; }
        var indR = new int[n]; var indC = new int[n];
        for (int k = 0; k < n; k++)
        {
            w[k] = a[(n + 1) * k];
            RefreshRow(a, indR, indC, n, k);
        }
        int maxIters = n * n * 30;
        if (n > 1)
            for (int iters = 0; iters < maxIters; iters++)
            {
                int k = 0; double mv = Math.Abs(a[indR[0]]);                          // J03: row candidates, then column candidates
                for (int i = 1; i < n - 1; i++) { double val = Math.Abs(a[n * i + indR[i]]); if (mv < val) { mv = val; k = i; } }
                int l = indR[k];
                for (int i = 1; i < n; i++) { double val = Math.Abs(a[n * indC[i] + i]); if (mv < val) { mv = val; k = indC[i]; l = i; } }
                double p = a[n * k + l];
                if (Math.Abs(p) <= DblEpsilon) break;
                double y = (w[l] - w[k]) * 0.5;                                       // J04
                double t = Math.Abs(y) + OpenCvSolveSvd.Hypot(p, y);
                double s = OpenCvSolveSvd.Hypot(p, t);
                double c = t / s;
                s = p / s; t = (p / t) * p;
                if (y < 0) { s = -s; t = -t; }
                a[n * k + l] = 0;
                w[k] -= t; w[l] += t;
                for (int i = 0; i < k; i++) Rotate(a, n * i + k, n * i + l, c, s);    // J05
                for (int i = k + 1; i < l; i++) Rotate(a, n * k + i, n * i + l, c, s);
                for (int i = l + 1; i < n; i++) Rotate(a, n * k + i, n * l + i, c, s);
                for (int i = 0; i < n; i++) Rotate(v, n * k + i, n * l + i, c, s);
                RefreshRow(a, indR, indC, n, k);                                      // J06
                RefreshRow(a, indR, indC, n, l);
            }
        for (int k = 0; k < n - 1; k++)                                               // J07
        {
            int m = k;
            for (int i = k + 1; i < n; i++) if (w[m] < w[i]) m = i;
            if (k != m)
            {
                (w[m], w[k]) = (w[k], w[m]);
                for (int i = 0; i < n; i++) (v[n * m + i], v[n * k + i]) = (v[n * k + i], v[n * m + i]);
            }
        }
    }

    private static void RefreshRow(double[] a, int[] indR, int[] indC, int n, int idx)
    {
        if (idx < n - 1)
        {
            int m = idx + 1; double mv = Math.Abs(a[n * idx + m]);
            for (int i = idx + 2; i < n; i++) { double val = Math.Abs(a[n * idx + i]); if (mv < val) { mv = val; m = i; } }
            indR[idx] = m;
        }
        if (idx > 0)
        {
            int m = 0; double mv = Math.Abs(a[idx]);
            for (int i = 1; i < idx; i++) { double val = Math.Abs(a[n * i + idx]); if (mv < val) { mv = val; m = i; } }
            indC[idx] = m;
        }
    }

    private static void Rotate(double[] a, int ix, int iy, double c, double s)
    {
        double x = a[ix], y = a[iy];
        a[ix] = x * c - s * y;
        a[iy] = c * y + x * s;
    }

    // -- CvLevMarq (L01..L15) and the loop around it

    private static Output Refine(double[] obj, double[] img, double fx, double fy, double cx, double cy, double[] k, int count, double[] start)
    {
        int nerr = count * 2;
        var param = (double[])start.Clone();
        var prev = new double[6];
        var j = new double[nerr * 6];
        var err = new double[nerr];
        var jtj = new double[36]; var jte = new double[6];
        double prevErrNorm = double.MaxValue;
        int lambdaLg10 = -3, iters = 0;

        // State 1 -> first Jacobian (L04), projection (L03).
        Project(obj, img, param, fx, fy, cx, cy, k, count, err, j);
        for (;;)
        {
            // State 2 (L05).
            var built = MulTransposed(j, nerr, 6, null);
            Array.Copy(built, jtj, 36);
            for (int c = 0; c < 6; c++)                                               // Jt*err: GEMM_A_T, single-column B: G05..G08
            {
                double s0 = 0, s1 = 0, s2 = 0, s3 = 0; int e = 0;
                for (; e <= nerr - 4; e += 4)
                {
                    s0 += j[e * 6 + c] * err[e];
                    s1 += j[(e + 1) * 6 + c] * err[e + 1];
                    s2 += j[(e + 2) * 6 + c] * err[e + 2];
                    s3 += j[(e + 3) * 6 + c] * err[e + 3];
                }
                for (; e < nerr; e++) s0 += j[e * 6 + c] * err[e];
                s0 = s0 + s1; s0 = s0 + s2; s0 = s0 + s3;
                jte[c] = s0 * 1.0;
            }
            Array.Copy(param, prev, 6);
            Step(jtj, jte, prev, param, lambdaLg10);
            if (iters == 0) prevErrNorm = NormL2(err, nerr, 1);
            Project(obj, img, param, fx, fy, cx, cy, k, count, err, null);
            // State 3 (L06).
            double errNorm;
            for (;;)
            {
                errNorm = NormL2(err, nerr, 1);
                if (errNorm > prevErrNorm && ++lambdaLg10 <= LmMaxExponent)
                {
                    Step(jtj, jte, prev, param, lambdaLg10);
                    Project(obj, img, param, fx, fy, cx, cy, k, count, err, null);
                    continue;
                }
                break;
            }
            lambdaLg10 = Math.Max(lambdaLg10 - 1, -16);                               // L07
            if (++iters >= LmMaxIterations || RelativeChange(param, prev) < LmEpsilon) break;
            prevErrNorm = errNorm;                                                    // L08
            Project(obj, img, param, fx, fy, cx, cy, k, count, err, j);
        }
        return new Output(new[] { param[0], param[1], param[2] }, new[] { param[3], param[4], param[5] }, iters);
    }

    /// <summary>CvLevMarq::step (0x00025720), mask all ones (L09..L12); cv::solve method 1 (B04..B09).</summary>
    private static void Step(double[] jtj, double[] jte, double[] prev, double[] param, int lambdaLg10)
    {
        double lambda = Libm("exp", lambdaLg10 * Ln10, Math.Exp);                                  // EQUIVALENT_IMPLEMENTATION: libm exp
        double alpha = 1.0 + lambda;
        var a = (double[])jtj.Clone();
        for (int i = 0; i < 6; i++) a[i * 7] = a[i * 7] * alpha + 0.0;                // L11
        var at = Transpose(a, 6, 6);                                                  // B07
        var w = new double[6]; var vt = new double[36];
        NativeRodrigues.JacobiSvd(at, w, vt, 6, 6, 6);                                // B08
        var delta = BackSubstitute(at, w, vt, jte, 6);                                // B09
        for (int i = 0; i < 6; i++) param[i] = prev[i] - delta[i];                    // L12
    }

    /// <summary>B01..B03: one right-hand side.</summary>
    private static double[] BackSubstitute(double[] u, double[] w, double[] v, double[] b, int n)
    {
        var x = new double[n];
        double threshold = 0;
        for (int i = 0; i < n; i++) threshold += w[i];
        threshold *= SvdRankCutoff;
        for (int i = 0; i < n; i++)
        {
            double wi = w[i];
            if (Math.Abs(wi) <= threshold) continue;
            wi = 1.0 / wi;
            double s = 0;
            for (int j = 0; j < n; j++) s += u[i * n + j] * b[j];
            s *= wi;
            for (int j = 0; j < n; j++) x[j] += s * v[i * n + j];
        }
        return x;
    }

    // -- cvProjectPoints2 (P01..P09). The residual is projection - observation (L03). J is null on an error-only request.

    private static void Project(double[] obj, double[] img, double[] param, double fx, double fy, double cx, double cy, double[] c, int count, double[] err, double[]? j)
    {
        var r = new double[9]; var dRdr = new double[27];
        VectorToMatrix(param, r, dRdr);
        double t0 = param[3], t1 = param[4], t2 = param[5];
        double k0 = c[0], k1 = c[1], k2 = c[2], k3 = c[3], k4 = c[4], k5 = c[5], k6 = c[6], k7 = c[7], k8 = c[8], k9 = c[9], k10 = c[10], k11 = c[11];
        for (int i = 0; i < count; i++)
        {
            double bigX = obj[i * 3], bigY = obj[i * 3 + 1], bigZ = obj[i * 3 + 2];
            double x = bigY * r[1]; x += bigX * r[0]; x += bigZ * r[2]; x = x + t0;   // P04
            double y = bigY * r[4]; y += bigX * r[3]; y += bigZ * r[5]; y = y + t1;
            double z = bigY * r[7]; z += bigX * r[6]; z += bigZ * r[8]; z = z + t2;
            z = z != 0 ? 1.0 / z : 1.0;
            y = y * z; x = x * z;
            double r2 = y * y; r2 += x * x;                                           // P05
            double r4 = r2 * r2, r6 = r4 * r2;
            double xTwice = x + x, a1 = xTwice * y;
            double a2 = r2; a2 += xTwice * x;
            double yTwice = y + y;
            double a3 = r2; a3 += yTwice * y;
            double cdist = 1.0; cdist += r2 * k0; cdist += r4 * k1; cdist += r6 * k4;
            double den = 1.0; den += r2 * k5; den += r4 * k6; den += r6 * k7;
            double icdist2 = 1.0 / den;
            double xc = x * cdist;
            double xd0 = k2 * a1; xd0 += xc * icdist2; xd0 += k3 * a2; xd0 += r2 * k8; xd0 += r4 * k9;
            double yc = y * cdist;
            double yd0 = k2 * a3; yd0 += yc * icdist2; yd0 += k3 * a1; yd0 += r2 * k10; yd0 += r4 * k11;
            // P06: identity tilt, each row from zero over columns 0,1,2 of (xd0,yd0,1).
            double vx = 0; vx += 1.0 * xd0; vx += 0.0 * yd0; vx += 0.0;
            double vy = 0; vy += 0.0 * xd0; vy += 1.0 * yd0; vy += 0.0;
            double vz = 0; vz += 0.0 * xd0; vz += 0.0 * yd0; vz += 1.0;
            double invProj = vz != 0 ? 1.0 / vz : 1.0;
            double ex = cx; ex += (invProj * vx) * fx;
            double ey = cy; ey += (invProj * vy) * fy;
            err[i * 2] = ex - img[i * 2]; err[i * 2 + 1] = ey - img[i * 2 + 1];       // L03
            if (j is null) continue;

            // P07: the tilt derivative. T is the identity: Tij*tiltZ - coordinate*T2j, times inverseZ squared.
            double invSq = invProj * invProj;
            double m00 = vx * 0.0; m00 = (vz * 1.0) - m00; m00 = m00 * invSq;
            double m01 = vx * 0.0; m01 = (vz * 0.0) - m01; m01 = m01 * invSq;
            double m10 = vy * 0.0; m10 = (vz * 0.0) - m10; m10 = m10 * invSq;
            double m11 = vy * 0.0; m11 = (vz * 1.0) - m11; m11 = m11 * invSq;

            // P08: columns 3..5, 0x0001574E..0x000158B8.
            double[] dxdt = { z, 0, -(x * z) }, dydt = { 0, z, -(y * z) };
            double negIc2 = -(icdist2 * icdist2);
            double r2Twice = r2 + r2;
            for (int col = 0; col < 3; col++)
            {
                double dx = dxdt[col], dy = dydt[col];
                double xdx = xTwice * dx, ydy = yTwice * dy;
                double dr2 = xdx + ydy;
                double dc = (k1 + k1) * r2; dc = dc * dr2; dc += dr2 * k0;
                double tmp = k4 * 3.0; tmp = tmp * r4; dc += tmp * dr2;
                double di = (k6 + k6) * r2; di = di * dr2; di += dr2 * k5;
                tmp = k7 * 3.0; tmp = tmp * r4; di += tmp * dr2;
                di = negIc2 * di;
                double a1d = y * dx; a1d += x * dy; a1d = a1d + a1d;
                double dmy = y * dc; dmy = dmy * icdist2; dmy += (dy * cdist) * icdist2; dmy += yc * di;
                dmy += k2 * (dr2 + ydy); dmy += k3 * a1d; dmy += dr2 * k10; dmy += (r2Twice * k11) * dr2;
                double dmx = x * dc; dmx = dmx * icdist2; dmx += (dx * cdist) * icdist2; dmx += xc * di;
                dmx += k2 * a1d; dmx += k3 * (dr2 + xdx); dmx += dr2 * k8; dmx += (r2Twice * k9) * dr2;
                (double ox, double oy) = Tilt2x2(m00, m01, m10, m11, dmx, dmy);
                j[(i * 2) * 6 + 3 + col] = fx * ox;
                j[(i * 2 + 1) * 6 + 3 + col] = fy * oy;
            }

            // P09: columns 0..2, 0x000158DC..0x00015C2E.
            var dx0 = new double[3]; var dy0 = new double[3]; var dz0 = new double[3];
            for (int p = 0; p < 3; p++)
            {
                int b = p * 9;
                double d = bigY * dRdr[b + 1]; d += bigX * dRdr[b]; d += bigZ * dRdr[b + 2]; dx0[p] = d;
                d = bigY * dRdr[b + 4]; d += bigX * dRdr[b + 3]; d += bigZ * dRdr[b + 5]; dy0[p] = d;
                d = bigY * dRdr[b + 7]; d += bigX * dRdr[b + 6]; d += bigZ * dRdr[b + 8]; dz0[p] = d;
            }
            for (int col = 0; col < 3; col++)
            {
                double dz = dz0[col];
                double dxr = dx0[col]; dxr -= x * dz; dxr = z * dxr;
                double dyr = dy0[col]; dyr -= y * dz; dyr = z * dyr;
                double xdx = xTwice * dxr, ydy = yTwice * dyr;
                double dcCoeff = k0; dcCoeff += (k1 + k1) * r2; dcCoeff += (k4 * 3.0) * r4;
                double diCoeff = k5; diCoeff += (k6 + k6) * r2; diCoeff += (k7 * 3.0) * r4;
                double dr2 = xdx + ydy;
                double di = negIc2 * diCoeff;
                double dc = dcCoeff * dr2;
                double a1d = y * dxr; a1d += x * dyr;
                di = di * dr2;
                a1d = a1d + a1d;
                double dmy = y * dc; dmy = dmy * icdist2; dmy += (dyr * cdist) * icdist2; dmy += yc * di;
                dmy += k2 * (dr2 + ydy);
                double prismY = k10; prismY += r2Twice * k11;
                dmy += k3 * a1d; dmy += prismY * dr2;
                double dmx = x * dc; dmx = dmx * icdist2; dmx += (dxr * cdist) * icdist2; dmx += xc * di;
                dmx += k2 * a1d;
                double prismX = k8; prismX += r2Twice * k9;
                dmx += k3 * (dr2 + xdx); dmx += prismX * dr2;
                (double ox, double oy) = Tilt2x2(m00, m01, m10, m11, dmx, dmy);
                j[(i * 2) * 6 + col] = fx * ox;
                j[(i * 2 + 1) * 6 + col] = fy * oy;
            }
        }
    }

    /// <summary>P07's 2x2 helper (0x00013E98): each row starts at zero and accumulates columns 0 then 1.</summary>
    private static (double, double) Tilt2x2(double m00, double m01, double m10, double m11, double x, double y)
    {
        double a = 0; a += m00 * x; a += m01 * y;
        double b = 0; b += m10 * x; b += m11 * y;
        return (a, b);
    }

    // -- cvRodrigues2 (R01..R08), the double paths

    /// <summary>R02..R04. <paramref name="v"/> holds x,y,z in its first three slots; <paramref name="j"/> is the 3x9 derivative or null.</summary>
    private static void VectorToMatrix(double[] v, double[] r, double[]? j)
    {
        double x = v[0], y = v[1], z = v[2];
        double n = y * y; n += x * x; n += z * z;
        double theta = Math.Sqrt(n);
        if (theta < DblEpsilon)                                                       // R02: strict; unordered takes the general branch
        {
            Array.Clear(r); r[0] = r[4] = r[8] = 1;
            if (j is not null)
            {
                Array.Clear(j);
                j[5] = -1; j[7] = 1; j[9 + 2] = 1; j[9 + 6] = -1; j[18 + 1] = -1; j[18 + 3] = 1;
            }
            return;
        }
        double c = Libm("cos", theta, Math.Cos), s = Libm("sin", theta, Math.Sin);                              // EQUIVALENT_IMPLEMENTATION: libm
        double c1 = 1.0 - c;
        double itheta = theta != 0 ? 1.0 / theta : 0.0;
        x = x * itheta; y = y * itheta; z = z * itheta;
        double xx = x * x, xy = x * y, yy = y * y, yz = y * z, xz = x * z, zz = z * z;
        double[] rrt = { xx, xy, xz, xy, yy, yz, xz, yz, zz };
        double[] skew = { 0, -z, y, z, 0, -x, -y, x, 0 };
        for (int k = 0; k < 9; k++)
        {
            double e = c1 * rrt[k]; e += c * Identity[k]; e += s * skew[k];
            r[k] = e;
        }
        if (j is null) return;
        double xTwice = x + x, yTwice = y + y, zTwice = z + z;                        // R04
        double[] drrt =
        {
            xTwice, y, z, y, 0, 0, z, 0, 0,
            0, x, 0, x, yTwice, z, 0, z, 0,
            0, 0, x, 0, 0, y, x, y, zTwice,
        };
        double qq = c1 + c1;
        double a = s; a -= qq * itheta;
        double b = s * itheta;
        double cc = c1 * itheta;
        double d = c - b;
        double ns = -s;
        for (int i = 0; i < 3; i++)
        {
            double u = i == 0 ? x : i == 1 ? y : z;
            double a0 = ns * u, a1 = a * u, a3 = d * u;
            for (int k = 0; k < 9; k++)
            {
                double e = a1 * rrt[k]; e += a0 * Identity[k]; e += cc * drrt[i * 9 + k]; e += a3 * skew[k]; e += b * DSkew[i * 9 + k];
                j[i * 9 + k] = e;
            }
        }
    }

    private static readonly double[] Identity = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
    private static readonly double[] DSkew =
    {
        0, 0, 0, 0, 0, -1, 0, 1, 0,
        0, 0, 1, 0, 0, 0, -1, 0, 0,
        0, -1, 0, 1, 0, 0, 0, 0, 0,
    };

    /// <summary>R05..R08, no Jacobian (the initializer requests none).</summary>
    private static void MatrixToVector(double[] matrix, double[] rvec)
    {
        for (int i = 0; i < 9; i++)
            if (!(matrix[i] >= -100 && matrix[i] < 100)) { rvec[0] = rvec[1] = rvec[2] = 0; return; }   // R05 bounds 0xC059000000000000, 0x4059000000000000
        var u = Transpose(matrix, 3, 3);                                              // flags 7
        var w = new double[3]; var vt = new double[9];
        NativeRodrigues.JacobiSvd(u, w, vt, 3, 3, 3);
        var r = GemmTransposeA3x3(u, vt);
        double x = r[7] - r[5], y = r[2] - r[6], z = r[3] - r[1];                     // R06
        double v = y * y; v += x * x; v += z * z; v = v * 0.25;
        double s = Math.Sqrt(v);
        double c = r[0] + r[4]; c = c + r[8]; c = c - 1.0; c = c * 0.5;
        if (c > 1.0) c = 1.0; else if (c < -1.0) c = -1.0;
        double theta = Libm("acos", c, Math.Acos);                                                  // EQUIVALENT_IMPLEMENTATION: libm
        if (s < SmallS)                                                               // R07
        {
            if (c > 0) { rvec[0] = rvec[1] = rvec[2] = 0; return; }
            double t = (r[0] + 1.0) * 0.5; double rx = Math.Sqrt(t < 0 ? 0 : t);
            t = (r[4] + 1.0) * 0.5; double ry = Math.Sqrt(t < 0 ? 0 : t) * (r[1] < 0 ? -1.0 : 1.0);
            t = (r[8] + 1.0) * 0.5; double rz = Math.Sqrt(t < 0 ? 0 : t) * (r[2] < 0 ? -1.0 : 1.0);
            if (Math.Abs(rx) < Math.Abs(ry) && Math.Abs(rx) < Math.Abs(rz) && (r[5] > 0) != (ry * rz > 0)) rz = -rz;
            double n = ry * ry; n += rx * rx; n += rz * rz;
            theta = theta / Math.Sqrt(n);
            rvec[0] = rx * theta; rvec[1] = ry * theta; rvec[2] = rz * theta;
            return;
        }
        double vth = 1.0 / (s + s);                                                   // R08
        vth = vth * theta;
        rvec[0] = x * vth; rvec[1] = y * vth; rvec[2] = z * vth;
    }

    // -- core helpers

    /// <summary>G12, G13: separate accumulators in point order, then a multiply by the reciprocal of the count.</summary>
    private static double[] Mean(double[] p, int count)
    {
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < count; i++) { x += p[i * 3]; y += p[i * 3 + 1]; z += p[i * 3 + 2]; }
        double scale = 1.0 / (double)(uint)count;
        return new[] { x * scale, y * scale, z * scale };
    }

    /// <summary>G03, G04, G10: the upper triangle by columns, each sum from zero in row order, then the lower half copied. Delta broadcasts one row.</summary>
    private static double[] MulTransposed(double[] a, int rows, int cols, double[]? delta)
    {
        var d = new double[cols * cols];
        var col = new double[rows];
        for (int i = 0; i < cols; i++)
        {
            for (int k = 0; k < rows; k++) col[k] = delta is null ? a[k * cols + i] : a[k * cols + i] - delta[i];
            for (int j = i; j < cols; j++)
            {
                double s = 0;
                if (delta is null) for (int k = 0; k < rows; k++) s += col[k] * a[k * cols + j];
                else for (int k = 0; k < rows; k++) s += col[k] * (a[k * cols + j] - delta[j]);
                d[i * cols + j] = s * 1.0;
            }
        }
        CompleteSymmFromUpper(d, cols);
        return d;
    }

    private static void CompleteSymmFromUpper(double[] a, int n)
    {
        for (int i = 0; i < n; i++) for (int j = 0; j < i; j++) a[i * n + j] = a[j * n + i];
    }

    private static double[] Transpose(double[] a, int rows, int cols)
    {
        var t = new double[rows * cols];
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) t[c * rows + r] = a[r * cols + c];
        return t;
    }

    /// <summary>G01: A*B, alpha 1, beta 0 over a zero C. Each dot starts A[r,1]*B[1,c].</summary>
    private static double[] Gemm3x3(double[] a, double[] b)
    {
        var d = new double[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                double s = a[r * 3 + 1] * b[3 + c]; s += a[r * 3] * b[c]; s += a[r * 3 + 2] * b[6 + c];
                double o = 0.0 * 0.0; o += s * 1.0;
                d[r * 3 + c] = o;
            }
        return d;
    }

    /// <summary>G06, G09: A^T * B with fewer than four output columns: one zero-start sequential sum per output.</summary>
    private static double[] GemmTransposeA3x3(double[] a, double[] b)
    {
        var d = new double[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                double s = 0; for (int k = 0; k < 3; k++) s += a[k * 3 + r] * b[k * 3 + c];
                d[r * 3 + c] = s * 1.0;
            }
        return d;
    }

    /// <summary>N05: the direct cofactor path of cvDet for a scalar double 3x3.</summary>
    private static double Determinant(double[] m)
    {
        double a = m[0], b = m[1], c = m[2], d = m[3], e = m[4], f = m[5], g = m[6], h = m[7], i = m[8];
        double ei = e * i - f * h;
        double di = d * i - f * g;
        double acc = a * ei - b * di;
        double dh = d * h - e * g;
        acc += c * dh;
        return acc;
    }

    /// <summary>N01, N03: <paramref name="planes"/> planes of <paramref name="width"/> scalars; each plane sums in groups of four, then its tail.</summary>
    private static double NormL2(double[] a, int width, int planes)
    {
        double total = 0;
        for (int p = 0; p < planes; p++)
        {
            int o = p * width, i = 0;
            double s = 0;
            for (; i <= width - 4; i += 4)
            {
                double g = a[o + i + 1] * a[o + i + 1]; g += a[o + i] * a[o + i]; g += a[o + i + 2] * a[o + i + 2]; g += a[o + i + 3] * a[o + i + 3];
                s += g;
            }
            for (; i < width; i++) s += a[o + i] * a[o + i];
            total += s;
        }
        return Math.Sqrt(total);
    }

    /// <summary>N02..N04: L2 of the difference over (L2 of the second operand + DBL_EPSILON).</summary>
    private static double RelativeChange(double[] current, double[] previous)
    {
        int n = current.Length, i = 0;
        double s = 0;
        for (; i <= n - 4; i += 4)
        {
            double d0 = current[i] - previous[i], d1 = current[i + 1] - previous[i + 1], d2 = current[i + 2] - previous[i + 2], d3 = current[i + 3] - previous[i + 3];
            double g = d1 * d1; g += d0 * d0; g += d2 * d2; g += d3 * d3;
            s += g;
        }
        for (; i < n; i++) { double d = current[i] - previous[i]; s += d * d; }
        double numerator = Math.Sqrt(s);
        return numerator / (NormL2(previous, n, 1) + DblEpsilon);
    }

    private static bool AllFinite(double[] a)
    {
        foreach (double v in a) if (!double.IsFinite(v)) return false;
        return true;
    }
}
