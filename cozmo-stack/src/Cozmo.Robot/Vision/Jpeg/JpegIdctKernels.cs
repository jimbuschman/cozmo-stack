namespace Cozmo.Robot.Vision.Jpeg;

// fidelity: M3-001, M3-018
// The one-dimensional inverse DCT kernels of the shipped jidctint.c (rows J81..J94 and J97, whose address ranges and literal
// constants each kernel's comment cites). Every kernel takes the DC term already scaled and biased by the caller (`a`) and the
// dequantised inputs x1..x(k-1), and returns the even half `e` and the odd half `o` of its symmetric output pairs: the output at
// position j is e[j] + o[j] and the one at n-1-j is e[j] - o[j]; an odd n also returns the centre output as the last `e`.
// `cs` is the shift of the centre-pair odd term of the 6-, 10- and 14-point kernels (2 in a column pass, 13 in a row pass).
internal static class JpegIdctKernels
{
    private static int S(uint v) => unchecked((int)v);

    /// <summary>Runs the n-point kernel on x (x[0] ignored, `a` carries it) for the transform of size n.</summary>
    public static void Run(int n, int a, ReadOnlySpan<int> x, int cs, Span<int> e, Span<int> o)
    {
        switch (n)
        {
            case 3: K3(a, x[1], x[2], e, o); break;
            case 4: K4(a, x[1], x[2], x[3], e, o); break;
            case 5: K5(a, x[1], x[2], x[3], x[4], e, o); break;
            case 6: K6(a, x[1], x[2], x[3], x[4], x[5], cs, e, o); break;
            case 7: K7(a, x[1], x[2], x[3], x[4], x[5], x[6], e, o); break;
            case 8: K8(a, x[1], x[2], x[3], x[4], x[5], x[6], x[7], cs, e, o); break;
            case 9: K9(a, x[1], x[2], x[3], x[4], x[5], x[6], x[7], e, o); break;
            case 10: K10(a, x[1], x[2], x[3], x[4], x[5], x[6], x[7], cs, e, o); break;
            case 11: K11(a, x[1], x[2], x[3], x[4], x[5], x[6], x[7], e, o); break;
            case 12: K12(a, x[1], x[2], x[3], x[4], x[5], x[6], x[7], e, o); break;
            case 13: K13(a, x[1], x[2], x[3], x[4], x[5], x[6], x[7], e, o); break;
            case 14: K14(a, x[1], x[2], x[3], x[4], x[5], x[6], x[7], cs, e, o); break;
            case 15: K15(a, x[1], x[2], x[3], x[4], x[5], x[6], x[7], e, o); break;
            case 16: K16(a, x[1], x[2], x[3], x[4], x[5], x[6], x[7], e, o); break;
            default: throw new NotSupportedException("kernel size " + n);
        }
    }

    /// <summary>
    /// Folds e and o into the n outputs of a column pass (<paramref name="column"/>: shifted right by 11, the centre pair of the 6-, 10- and
    /// 14-point kernels as (E &gt;&gt; 11) +- O) or a row pass (shifted right by 18). The caller masks and indexes the range table.
    /// </summary>
    public static void Combine(int n, bool column, ReadOnlySpan<int> e, ReadOnlySpan<int> o, Span<int> outv)
    {
        int shift = column ? 11 : 18;
        int pairs = n / 2;
        int special = column && (n == 6 || n == 10 || n == 14) ? (n - 2) / 4 : -1;
        for (int j = 0; j < pairs; j++)
        {
            if (j == special)
            {
                outv[j] = (e[j] >> 11) + o[j];
                outv[n - 1 - j] = (e[j] >> 11) - o[j];
            }
            else
            {
                outv[j] = (e[j] + o[j]) >> shift;
                outv[n - 1 - j] = (e[j] - o[j]) >> shift;
            }
        }
        if ((n & 1) != 0) outv[pairs] = e[pairs] >> shift;
    }

    // J81: 3-point
    private static void K3(int a, int x1, int x2, Span<int> e, Span<int> o)
    {
        int b = 0x16A1 * x2;
        int c = 0x2731 * x1;
        e[0] = a + b; e[1] = a - 2 * b;
        o[0] = c;
    }

    // J82: 4-point
    private static void K4(int a, int x1, int x2, int x3, Span<int> e, Span<int> o)
    {
        int x2s = x2 << 13;
        int t = 0x1151 * (x1 + x3);
        e[0] = a + x2s; e[1] = a - x2s;
        o[0] = t + 0x187E * x1; o[1] = t + S(0xFFFFC4DF) * x3;
    }

    // J83: 5-point
    private static void K5(int a, int x1, int x2, int x3, int x4, Span<int> e, Span<int> o)
    {
        int b = 0x0B50 * (x2 - x4);
        int c = 0x194C * (x2 + x4);
        e[0] = a + b + c; e[1] = a + b - c; e[2] = a - 4 * b;
        int t = 0x1A9A * (x1 + x3);
        o[0] = t + 0x1071 * x1; o[1] = t + S(0xFFFFBA5C) * x3;
    }

    // J84: 6-point (the centre pair is index 1)
    private static void K6(int a, int x1, int x2, int x3, int x4, int x5, int cs, Span<int> e, Span<int> o)
    {
        int b = 0x16A1 * x4;
        int c = 0x2731 * x2;
        e[0] = a + b + c; e[2] = a + b - c; e[1] = a - 2 * b;
        int t = 0x0BB6 * (x1 + x5);
        o[0] = t + ((x1 + x3) << 13);
        o[2] = t + ((x5 - x3) << 13);
        o[1] = (x1 - x3 - x5) << cs;
    }

    // J85: 7-point
    private static void K7(int a, int x1, int x2, int x3, int x4, int x5, int x6, Span<int> e, Span<int> o)
    {
        int b = 0x1C37 * (x4 - x6);
        int c = 0x0A12 * (x2 - x4);
        e[1] = a + b + c + S(0xFFFFC515) * x4;
        int t = a + 0x28C6 * (x2 + x6);
        e[0] = t + S(0xFFFFFD83) * x6 + b;
        e[2] = t + S(0xFFFFB0F1) * x2 + c;
        e[3] = a + 0x2D41 * (x4 - x2 - x6);
        int u = 0x1DEF * (x1 + x3);
        int v = 0x0573 * (x1 - x3);
        int w = 0x13A3 * (x1 + x5);
        int z = S(0xFFFFD3E1) * (x3 + x5);
        o[0] = u - v + w;
        o[1] = u + v + z;
        o[2] = w + 0x3BDE * x5 + z;
    }

    // J97: 8-point
    public static void K8(int a, int x1, int x2, int x3, int x4, int x5, int x6, int x7, int cs, Span<int> e, Span<int> o)
    {
        int t = 0x1151 * (x2 + x6);
        int k0 = t + 0x187E * x2;
        int k1 = t + S(0xFFFFC4DF) * x6;
        int x4s = x4 << 13;
        e[0] = a + x4s + k0;
        e[3] = a + x4s - k0;
        e[1] = a - x4s + k1;
        e[2] = a - x4s - k1;
        int p = x7 + x3;
        int q = x5 + x1;
        int t2 = 0x25A1 * (p + q);
        int u = t2 + S(0xFFFFC13B) * p;
        int v = t2 + S(0xFFFFF384) * q;
        int rr = S(0xFFFFE333) * (x7 + x1);
        o[3] = rr + u + 0x098E * x7;
        o[0] = rr + v + 0x300B * x1;
        int s = S(0xFFFFADFD) * (x5 + x3);
        o[2] = s + v + 0x41B3 * x5;
        o[1] = s + u + 0x6254 * x3;
    }

    // J86: 9-point (eight inputs)
    private static void K9(int a, int x1, int x2, int x3, int x4, int x5, int x6, int x7, Span<int> e, Span<int> o)
    {
        int b = 0x16A1 * x6;
        int l = a + b;
        int m = a - 2 * b;
        int c = 0x16A1 * (x2 - x4);
        int d = 0x2A87 * (x2 + x4);
        e[0] = l + d - 0x07DC * x4;
        e[2] = l - d + 0x22AB * x2;
        e[3] = l - 0x22AB * x2 + 0x07DC * x4;
        e[1] = m + c;
        e[4] = m - 2 * c;
        int t = S(0xFFFFD8CF) * x3;
        int u = 0x1D17 * (x1 + x5);
        int v = 0x0F7A * (x1 + x7);
        int w = 0x2C91 * (x5 - x7);
        o[0] = u + v - t;
        o[2] = t - w + u;
        o[3] = v + w + t;
        o[1] = 0x2731 * (x1 - x5 - x7);
    }

    // J87: 10-point (the centre pair is index 2)
    private static void K10(int a, int x1, int x2, int x3, int x4, int x5, int x6, int x7, int cs, Span<int> e, Span<int> o)
    {
        int h = 0x249D * x4;
        int l = 0x0DFC * x4;
        int t = 0x1A9A * (x2 + x6);
        int k0 = t + 0x1071 * x2;
        int k1 = t + S(0xFFFFBA5C) * x6;
        e[0] = a + h + k0;
        e[4] = a + h - k0;
        e[1] = a - l + k1;
        e[3] = a - l - k1;
        e[2] = a - 2 * (h - l);
        int p = x3 + x7;
        int q = x3 - x7;
        int t1 = 0x1E6F * p;
        int t2 = 0x09E3 * q;
        int u = (x5 << 13) + t2;
        int v = (x5 << 13) - t2 - (q << 12);
        o[0] = 0x2CB3 * x1 + t1 + u;
        o[4] = 0x0714 * x1 - t1 + u;
        o[1] = 0x2853 * x1 - 0x12CF * p - v;
        o[3] = 0x148C * x1 - 0x12CF * p + v;
        o[2] = (x1 - q - x5) << cs;
    }

    // J88: 11-point
    private static void K11(int a, int x1, int x2, int x3, int x4, int x5, int x6, int x7, Span<int> e, Span<int> o)
    {
        int p = x2, q = x4, r = x6;
        int s = p + r - q;
        int z = a + 0x2B6C * s;
        int u = 0x0DC9 * (q - p);
        int v = 0x517E * (q - r);
        e[1] = z + v + u + S(0xFFFFC5B4) * q;
        e[0] = z + 0x43B5 * r + v;
        e[3] = z + S(0xFFFFCF91) * p + u;
        int k = z + S(0xFFFFDB05) * (p + r);
        e[2] = k + S(0xFFFFE6C3) * r;
        e[4] = k + S(0xFFFFD37D) * p + 0x3E39 * q;
        e[5] = z - 0x58AD * s;
        int aa = x1, bb = x3, cc = x5, dd = x7;
        int l = 0x0CC0 * (aa + bb + cc + dd);
        int u2 = 0x1C6A * (aa + bb);
        int v2 = 0x1574 * (aa + cc);
        int w2 = l + 0x0BB8 * (aa + dd);
        o[0] = u2 + v2 + w2 + S(0xFFFFE276) * aa;
        int z2 = l + S(0xFFFFDAC9) * (bb + cc);
        o[1] = u2 + 0x4258 * bb + z2 + S(0xFFFFC675) * (bb + dd);
        o[2] = v2 + S(0xFFFFD9DA) * cc + z2;
        o[3] = S(0xFFFFC675) * (bb + dd) + 0x4347 * dd + w2;
        o[4] = l + 0x200B * cc + S(0xFFFFD10D) * bb + S(0xFFFFCA16) * dd;
    }

    // J89: 12-point
    private static void K12(int a, int x1, int x2, int x3, int x4, int x5, int x6, int x7, Span<int> e, Span<int> o)
    {
        int b = 0x2731 * x4;
        int l = a + b;
        int m = a - b;
        int c = 0x2BB6 * x2;
        int r = x6 << 13;
        int p = x2 << 13;
        e[1] = a + p - r;
        e[4] = a - p + r;
        e[0] = l + c + r;
        e[5] = l - c - r;
        e[2] = m + c - p - r;
        e[3] = m - c + p + r;
        int aa = x1, bb = x3, cc = x5, dd = x7;
        int j = 0x29CF * bb;
        int k = S(0xFFFFEEAF) * bb;
        int f = S(0xFFFFDE8B) * (cc + dd);
        int t = 0x1B8D * (aa + cc + dd);
        int u = 0x085B * (aa + cc) + t;
        o[0] = u + j + 0x08F7 * aa;
        int w = f + k + S(0xFFFFD0B0) * cc;
        o[2] = u + w;
        o[3] = f + t - j + 0x32C6 * dd;
        o[5] = k + S(0xFFFFEA5C) * aa + S(0xFFFFC08C) * dd + t;
        int rr = aa - dd;
        int ss = bb - cc;
        int v = 0x1151 * (rr + ss);
        o[1] = v + 0x187E * rr;
        o[4] = v + S(0xFFFFC4DF) * ss;
    }

    // J90: 13-point
    private static void K13(int a, int x1, int x2, int x3, int x4, int x5, int x6, int x7, Span<int> e, Span<int> o)
    {
        int p = x2, q = x4, r = x6;
        int s = q + r;
        int d = q - r;
        int f = a + 0x0319 * d;
        int t = 0x24F9 * s;
        e[0] = f + t + 0x2BF1 * p;
        e[2] = f + 0x100C * p - t;
        int f2 = f + 0x0C7C * d;
        int t2 = 0x0A20 * s;
        e[1] = f2 + 0x21E0 * p - t2;
        e[5] = f2 + S(0xFFFFD7EE) * p + t2;
        int h = 0x1DFE * d - a;
        int t3 = 0x0DF2 * s;
        e[3] = S(0xFFFFFA8C) * p - t3 - h;
        e[4] = S(0xFFFFE64B) * p + t3 - h;
        e[6] = a + 0x2D41 * (d - p);
        int aa = x1, bb = x3, cc = x5, dd = x7;
        int u = 0x2A50 * (aa + bb);
        int v = 0x253E * (aa + cc);
        int w = 0x1E02 * (aa + dd);
        o[0] = u + v + w + S(0xFFFFBF5B) * aa;
        int z = S(0xFFFFF52B) * (bb + cc);
        int y1 = 0x1ACB * bb + z;
        int y2 = S(0xFFFFCDB1) * cc + z;
        int xx = S(0xFFFFDAC2) * (bb + dd);
        o[1] = u + y1 + xx;
        int rr = S(0xFFFFEAF8) * (cc + dd);
        o[2] = y2 + v + rr;
        o[3] = rr + w + xx + 0x4694 * dd;
        int ss = 0x0AD5 * (aa + dd);
        int tt = 0x1E02 * (cc - bb);
        o[4] = 0x0A33 * aa + ss + S(0xFFFFF116) * bb + tt;
        o[5] = tt + 0x0C4E * cc + S(0xFFFFC83F) * dd + ss;
    }

    // J91: 14-point (the centre pair is index 3)
    private static void K14(int a, int x1, int x2, int x3, int x4, int x5, int x6, int x7, int cs, Span<int> e, Span<int> o)
    {
        int h = 0x28C6 * x4;
        int l = 0x0A12 * x4;
        int m = 0x1C37 * x4;
        int t = 0x2362 * (x2 + x6);
        int k0 = t + 0x08BD * x2;
        int k1 = t + S(0xFFFFC8FC) * x6;
        int k2 = 0x13A3 * x2 + S(0xFFFFD3E1) * x6;
        e[0] = a + h + k0;
        e[6] = a + h - k0;
        e[1] = a + l + k1;
        e[5] = a + l - k1;
        e[2] = a - m + k2;
        e[4] = a - m - k2;
        e[3] = a - 2 * (h + l - m);
        int aa = x1, bb = x3, cc = x5, dd = x7;
        int u = 0x2AB7 * (aa + bb);
        int v = 0x2652 * (aa + cc);
        int w = 0x0EF2 * (aa - bb);
        int r = dd << 13;
        o[0] = u + v + r + S(0xFFFFDBF0) * aa;
        int s = 0x1814 * (aa + cc);
        o[6] = s + S(0xFFFFDE0B) * aa + w - r;
        int z = S(0xFFFFFAEF) * (bb + cc) - r;
        o[1] = u + z + S(0xFFFFF26E) * bb;
        o[2] = v + z + S(0xFFFFB409) * cc;
        int p = 0x2CF8 * (cc - bb);
        o[4] = r + p + S(0xFFFFC9E6) * cc + s;
        o[5] = w - r + p + 0x1599 * bb;
        o[3] = (aa - bb - cc + dd) << cs;
    }

    // J92: 15-point
    private static void K15(int a, int x1, int x2, int x3, int x4, int x5, int x6, int x7, Span<int> e, Span<int> o)
    {
        int p = x2, q = x4, r = x6;
        int h = 0x0DFC * r;
        int j = 0x249D * r;
        int l = a - h;
        int rr = a + j;
        int m = a - 2 * (j - h);
        int s = p + q;
        int d = p - q;
        int t = 0x2ACE * s;
        int u = 0x0176 * d;
        int v = 0x2E13 * p;
        e[0] = rr + t + u;
        e[3] = l - t + u + v;
        int t2 = 0x1182 * s;
        int u2 = 0x0CC7 * d;
        e[5] = rr - t2 - u2;
        e[6] = l + t2 - u2 - v;
        int t3 = 0x194C * s;
        int u3 = 0x0B50 * d;
        e[1] = l + t3 + u3;
        e[4] = rr - t3 + u3;
        e[2] = m + 2 * u3;
        e[7] = m - 4 * u3;
        int aa = x1, bb = x3, cc = x5, dd = x7;
        int k = 0x2731 * cc;
        int tt = 0x1A9A * (aa + bb - dd);
        o[1] = tt + 0x1071 * aa;
        o[4] = tt + S(0xFFFFBA5C) * (bb - dd);
        int pp = 0x2D02 * (aa - dd) + k;
        int uu = S(0xFFFFE566) * bb;
        int vv = S(0xFFFFD4F6) * bb;
        o[0] = pp + 0x4EA3 * dd - vv;
        o[6] = pp + S(0xFFFFDC67) * aa + uu;
        o[2] = 0x2731 * (aa - dd) - k;
        int ww = 0x1268 * (aa + dd);
        o[3] = ww + 0x0F39 * aa - k + uu;
        o[5] = ww + S(0xFFFFE42F) * dd + k + vv;
    }

    // J93, J94: 16-point
    private static void K16(int a, int x1, int x2, int x3, int x4, int x5, int x6, int x7, Span<int> e, Span<int> o)
    {
        int h = 0x29CF * x4;
        int l = 0x1151 * x4;
        int t = 0x08D4 * (x2 - x6);
        int u = 0x2C63 * (x2 - x6);
        int k0 = u + 0x5203 * x6;
        int k1 = t + 0x1CCD * x2;
        int k2 = u + S(0xFFFFECC2) * x2;
        int k3 = t + S(0xFFFFEFB0) * x6;
        e[0] = a + h + k0;
        e[7] = a + h - k0;
        e[1] = a + l + k1;
        e[6] = a + l - k1;
        e[2] = a - l + k2;
        e[5] = a - l - k2;
        e[3] = a - h + k3;
        e[4] = a - h - k3;
        int aa = x1, b = x3, c = x5, d = x7;
        int uu = 0x2B4E * (aa + b);
        int vv = 0x27E9 * (aa + c);
        int w = 0x22FC * (aa + d);
        int xx = 0x1555 * (aa + c);
        int y = 0x1CB6 * (aa - d);
        int z = 0x0D23 * (aa - b);
        o[0] = uu + vv + w + S(0xFFFFB6D6) * aa;
        o[7] = y + xx + z + S(0xFFFFC542) * aa;
        int tt = 0x0470 * (b + c);
        int pp = 0x024D * b + tt;
        int qq = S(0xFFFFDBFA) * c + tt;
        int rr = 0x2D09 * (c - b);
        o[5] = xx + rr + S(0xFFFFE77A) * c + 0x0D23 * (d - c);
        o[6] = rr + 0x3F1A * b + z + S(0xFFFFD817) * (b + d);
        o[1] = uu + pp + S(0xFFFFEAAB) * (b + d);
        o[3] = w + S(0xFFFFEAAB) * (b + d) + 0x2218 * d + S(0xFFFFD4B2) * (c + d);
        o[2] = vv + qq + S(0xFFFFD4B2) * (c + d);
        o[4] = y + S(0xFFFFD817) * (b + d) + 0x6485 * d + 0x0D23 * (d - c);
    }
}
