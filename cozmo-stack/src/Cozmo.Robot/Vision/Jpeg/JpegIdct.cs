namespace Cozmo.Robot.Vision.Jpeg;

// fidelity: M3-001, M3-018
// jddctmgr.c + jidctint.c as shipped (method 0, JDCT_ISLOW): J17..J22, J79..J102. All arithmetic is 32-bit signed with wrap
// (unchecked), shifts are arithmetic, and every output goes through the range-limit table with the 10-bit mask (J19, J20). The 32
// transforms the dispatch of J79 can select (squares 1..16, rectangles 2:1 up to 16x8 and 8x16) are built from the one-dimensional
// kernels of J81..J94 and J97; rows 3..16 take their input from the first eight coefficients of each axis (J86..J94).
internal sealed class SampleArray
{
    public readonly byte[] Data;
    public readonly int Stride;
    public readonly int Rows;

    public SampleArray(int width, int rows)
    {
        Stride = width;
        Rows = rows;
        Data = new byte[(long)width * rows];
    }
}

internal static class JpegIdct
{
    private static int S(uint v) => unchecked((int)v);

    /// <summary>True for the 32 geometries of the dispatch (J79): squares 1..16 and rectangles with a ratio of exactly 2.</summary>
    public static bool Supported(int w, int h) => w >= 1 && h >= 1 && w <= 16 && h <= 16 && (w == h || w == 2 * h || h == 2 * w);

    /// <summary>
    /// The inverse DCT of one block into <paramref name="dst"/>[dstOff + r * dstStride + c], r &lt; h, c &lt; w. <paramref name="coef"/>
    /// holds the block's 64 signed 16-bit coefficients (natural order) from <paramref name="coefOff"/>, <paramref name="q"/> the 64 integer
    /// multipliers, <paramref name="range"/> the 1408-byte range-limit allocation whose sample_range_limit starts at index 256.
    /// </summary>
    public static void Transform(int w, int h, short[] coef, int coefOff, int[] q, byte[] range, byte[] dst, int dstOff, int dstStride)
    {
        int rb = 256 + 128;          // sample_range_limit + CENTERJSAMPLE
        if (w == 8 && h == 8) { Idct8x8(coef, coefOff, q, range, dst, dstOff, dstStride); return; }
        if (w == 1 && h == 1) { dst[dstOff] = range[rb + (((Deq(coef, coefOff, q, 0) + 4) >> 3) & 1023)]; return; }
        if (w == 1 && h == 2)
        {
            int d0 = Deq(coef, coefOff, q, 0), d8 = Deq(coef, coefOff, q, 8);
            dst[dstOff] = range[rb + (((d0 + 4 + d8) >> 3) & 1023)];
            dst[dstOff + dstStride] = range[rb + (((d0 + 4 - d8) >> 3) & 1023)];
            return;
        }
        if (w == 2 && h == 1)
        {
            int d0 = Deq(coef, coefOff, q, 0), d1 = Deq(coef, coefOff, q, 1);
            dst[dstOff] = range[rb + (((d0 + 4 + d1) >> 3) & 1023)];
            dst[dstOff + 1] = range[rb + (((d0 + 4 - d1) >> 3) & 1023)];
            return;
        }
        if (w == 2 && h == 2)
        {
            int d0 = Deq(coef, coefOff, q, 0), d1 = Deq(coef, coefOff, q, 1), d8 = Deq(coef, coefOff, q, 8), d9 = Deq(coef, coefOff, q, 9);
            dst[dstOff] = range[rb + (((d0 + 4 + d8 + d1 + d9) >> 3) & 1023)];
            dst[dstOff + 1] = range[rb + (((d0 + 4 + d8 - d1 - d9) >> 3) & 1023)];
            dst[dstOff + dstStride] = range[rb + (((d0 + 4 - d8 + d1 - d9) >> 3) & 1023)];
            dst[dstOff + dstStride + 1] = range[rb + (((d0 + 4 - d8 - d1 + d9) >> 3) & 1023)];
            return;
        }
        if (w == 2 && h == 4) { Idct2x4(coef, coefOff, q, range, dst, dstOff, dstStride); return; }
        if (w == 4 && h == 2) { Idct4x2(coef, coefOff, q, range, dst, dstOff, dstStride); return; }
        General(w, h, coef, coefOff, q, range, dst, dstOff, dstStride);
    }

    private static int Deq(short[] coef, int off, int[] q, int k) => coef[off + k] * q[k];

    // ---- jpeg_idct_islow (J18, J19, J97), kept as its own body because of the two zero shortcuts
    private static void Idct8x8(short[] coef, int off, int[] q, byte[] range, byte[] dst, int dstOff, int stride)
    {
        Span<int> ws = stackalloc int[64];
        Span<int> e = stackalloc int[4];
        Span<int> o = stackalloc int[4];
        int rb = 256 + 128;
        for (int c = 0; c < 8; c++)
        {
            if (coef[off + c + 8] == 0 && coef[off + c + 16] == 0 && coef[off + c + 24] == 0 && coef[off + c + 32] == 0 &&
                coef[off + c + 40] == 0 && coef[off + c + 48] == 0 && coef[off + c + 56] == 0)
            {
                int dc = (coef[off + c] * q[c]) << 2;
                for (int k = 0; k < 8; k++) ws[k * 8 + c] = dc;
                continue;
            }
            JpegIdctKernels.K8(((Deq(coef, off, q, c)) << 13) + 1024, Deq(coef, off, q, c + 8), Deq(coef, off, q, c + 16), Deq(coef, off, q, c + 24),
                Deq(coef, off, q, c + 32), Deq(coef, off, q, c + 40), Deq(coef, off, q, c + 48), Deq(coef, off, q, c + 56), 13, e, o);
            for (int j = 0; j < 4; j++) { ws[j * 8 + c] = (e[j] + o[j]) >> 11; ws[(7 - j) * 8 + c] = (e[j] - o[j]) >> 11; }
        }
        for (int row = 0; row < 8; row++)
        {
            int w = row * 8;
            int d = dstOff + row * stride;
            if (ws[w + 1] == 0 && ws[w + 2] == 0 && ws[w + 3] == 0 && ws[w + 4] == 0 && ws[w + 5] == 0 && ws[w + 6] == 0 && ws[w + 7] == 0)
            {
                byte dcval = range[rb + (((ws[w] + 16) >> 5) & 1023)];
                for (int k = 0; k < 8; k++) dst[d + k] = dcval;
                continue;
            }
            JpegIdctKernels.K8((ws[w] + 16) << 13, ws[w + 1], ws[w + 2], ws[w + 3], ws[w + 4], ws[w + 5], ws[w + 6], ws[w + 7], 13, e, o);
            for (int j = 0; j < 4; j++)
            {
                dst[d + j] = range[rb + (((e[j] + o[j]) >> 18) & 1023)];
                dst[d + 7 - j] = range[rb + (((e[j] - o[j]) >> 18) & 1023)];
            }
        }
    }

    // ---- 2x4: two columns of the four-point butterfly with no bias or shift, then a two-point row (J95)
    private static void Idct2x4(short[] coef, int off, int[] q, byte[] range, byte[] dst, int dstOff, int stride)
    {
        int rb = 256 + 128;
        Span<int> ws = stackalloc int[8];
        for (int c = 0; c < 2; c++)
        {
            int d0 = Deq(coef, off, q, c), d8 = Deq(coef, off, q, c + 8), d16 = Deq(coef, off, q, c + 16), d24 = Deq(coef, off, q, c + 24);
            int e0 = (d0 + d16) << 13, e1 = (d0 - d16) << 13;
            int t = 0x1151 * (d8 + d24);
            int o0 = t + 0x187E * d8, o1 = t + S(0xFFFFC4DF) * d24;
            ws[0 * 2 + c] = e0 + o0; ws[1 * 2 + c] = e1 + o1; ws[2 * 2 + c] = e1 - o1; ws[3 * 2 + c] = e0 - o0;
        }
        for (int r = 0; r < 4; r++)
        {
            dst[dstOff + r * stride] = range[rb + (((ws[r * 2] + 32768 + ws[r * 2 + 1]) >> 16) & 1023)];
            dst[dstOff + r * stride + 1] = range[rb + (((ws[r * 2] + 32768 - ws[r * 2 + 1]) >> 16) & 1023)];
        }
    }

    // ---- 4x2: four two-point columns without shifts, then two four-point rows with +4 and bits 16..25 (J95)
    private static void Idct4x2(short[] coef, int off, int[] q, byte[] range, byte[] dst, int dstOff, int stride)
    {
        int rb = 256 + 128;
        Span<int> ws = stackalloc int[8];
        for (int c = 0; c < 4; c++)
        {
            int d0 = Deq(coef, off, q, c), d8 = Deq(coef, off, q, c + 8);
            ws[0 * 4 + c] = d0 + d8;
            ws[1 * 4 + c] = d0 - d8;
        }
        for (int r = 0; r < 2; r++)
        {
            int w0 = ws[r * 4], w1 = ws[r * 4 + 1], w2 = ws[r * 4 + 2], w3 = ws[r * 4 + 3];
            int e0 = ((w0 + 4) + w2) << 13, e1 = ((w0 + 4) - w2) << 13;
            int t = 0x1151 * (w1 + w3);
            int o0 = t + 0x187E * w1, o1 = t + S(0xFFFFC4DF) * w3;
            int d = dstOff + r * stride;
            dst[d] = range[rb + (((e0 + o0) >> 16) & 1023)];
            dst[d + 3] = range[rb + (((e0 - o0) >> 16) & 1023)];
            dst[d + 1] = range[rb + (((e1 + o1) >> 16) & 1023)];
            dst[d + 2] = range[rb + (((e1 - o1) >> 16) & 1023)];
        }
    }

    // ---- the general separable form: min(w,8) columns of the h-point kernel (bias 1024, >> 11), then h rows of the w-point kernel
    // (bias 16 << 13, >> 18), J81..J94, J96..J102
    private static void General(int w, int h, short[] coef, int off, int[] q, byte[] range, byte[] dst, int dstOff, int stride)
    {
        int rb = 256 + 128;
        int kc = Math.Min(w, 8);                 // input columns
        int kr = Math.Min(h, 8);                 // input rows
        Span<int> ws = stackalloc int[16 * 8];
        Span<int> x = stackalloc int[8];
        Span<int> e = stackalloc int[8];
        Span<int> o = stackalloc int[8];
        Span<int> outv = stackalloc int[16];
        for (int c = 0; c < kc; c++)
        {
            for (int j = 0; j < kr; j++) x[j] = Deq(coef, off, q, 8 * j + c);
            if (h == 8 && AllAcZero(coef, off, c))
            {
                int dc = (coef[off + c] * q[c]) << 2;
                for (int k = 0; k < 8; k++) ws[k * kc + c] = dc;
                continue;
            }
            if (h == 4)
            {
                // the four-point column pass keeps the DC terms unscaled and shifts only the odd part (J82):
                // E0 = (d0 + d16) << 2, E1 = (d0 - d16) << 2, T = 0x1151 * (d8 + d24) + 1024, O = (T + k * d) >> 11
                int e0 = (x[0] + x[2]) << 2, e1 = (x[0] - x[2]) << 2;
                int t = 0x1151 * (x[1] + x[3]) + 1024;
                int o0 = (t + 0x187E * x[1]) >> 11, o1 = (t + S(0xFFFFC4DF) * x[3]) >> 11;
                ws[0 * kc + c] = e0 + o0; ws[1 * kc + c] = e1 + o1; ws[2 * kc + c] = e1 - o1; ws[3 * kc + c] = e0 - o0;
                continue;
            }
            JpegIdctKernels.Run(h, (x[0] << 13) + 1024, x, 2, e, o);
            JpegIdctKernels.Combine(h, true, e, o, outv);
            for (int k = 0; k < h; k++) ws[k * kc + c] = outv[k];
        }
        for (int r = 0; r < h; r++)
        {
            for (int j = 0; j < kc; j++) x[j] = ws[r * kc + j];
            JpegIdctKernels.Run(w, (x[0] + 16) << 13, x, 13, e, o);
            JpegIdctKernels.Combine(w, false, e, o, outv);
            int d = dstOff + r * stride;
            for (int k = 0; k < w; k++) dst[d + k] = range[rb + (outv[k] & 1023)];
        }
    }

    private static bool AllAcZero(short[] coef, int off, int c)
        => coef[off + c + 8] == 0 && coef[off + c + 16] == 0 && coef[off + c + 24] == 0 && coef[off + c + 32] == 0 &&
           coef[off + c + 40] == 0 && coef[off + c + 48] == 0 && coef[off + c + 56] == 0;
}
