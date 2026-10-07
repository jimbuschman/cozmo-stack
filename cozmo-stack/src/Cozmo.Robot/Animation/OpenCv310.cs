namespace Cozmo.Robot.Animation;

// fidelity: M5-021, M5-032
/// <summary>
/// The three OpenCV 3.1.0 functions the engine's face drawer calls, ported from the stock 3.1.0 sources for the
/// parameters the engine passes (MD2). The shipped <c>libopencv_imgproc.so</c> (Anki build "143c19f7-dirty") was
/// compared against stock 3.1.0 on exactly these paths and matches (M5 inventory Appendix D, gap3 A1..A10, B1..B3,
/// C1..C6). Every image here is a single-channel 8-bit Mat, row-major, one byte per pixel, step = cols.
/// <list type="bullet">
/// <item><see cref="FillConvexPoly"/>: <c>cv::fillConvexPoly(img, pts, Scalar(v), lineType 4, shift 0)</c>: every
/// edge drawn as a 4-connected <c>LineIterator</c> line (A4, A8, A9, with <see cref="ClipLine"/>, A10), then the
/// span fill over [(x1+0x8000)&gt;&gt;16, (x2+0x8000)&gt;&gt;16] from 16.16 edges stepped by a truncating division
/// (A5..A7).</item>
/// <item><see cref="Ellipse2Poly"/>: the normalisation, the 451-entry <c>SinTable</c>, and <c>cvRound</c> with ties to
/// even (B1..B3).</item>
/// <item><see cref="WarpAffineNearest"/>: <c>cv::warpAffine(src, dst, M, dsize, INTER_NEAREST, BORDER_CONSTANT, 0)</c>
/// with the inverse map, AB_BITS 10 and remapNearest (C1..C6).</item>
/// </list>
/// Rounding assumes the default FPSCR mode, round to nearest with ties to even (MD2).
/// </summary>
public static class OpenCv310
{
    private const int XyShift = 16;
    private const int XyOne = 1 << XyShift;

    /// <summary>
    /// <c>cvRound(double)</c> = <c>saturate_cast&lt;int&gt;(double)</c>, which the ARM build compiles to
    /// <c>vcvtr.s32.f64</c>: round to nearest, ties to even (gap3 Q2, B3), saturating at the int range; NaN gives 0.
    /// </summary>
    public static int CvRound(double v)
    {
        if (double.IsNaN(v)) return 0;
        double r = Math.Round(v, MidpointRounding.ToEven);
        if (r >= int.MaxValue) return int.MaxValue;
        if (r <= int.MinValue) return int.MinValue;
        return (int)r;
    }

    // ------------------------------------------------------------------ clipLine (A10)

    /// <summary>
    /// <c>clipLine(Size, Point&amp;, Point&amp;)</c> (drawing.cpp, gap3 A10): int64 Cohen–Sutherland, y first then x, the
    /// second point's clip using the already-updated first point, truncating divisions. Returns whether the clipped
    /// line lies inside.
    /// </summary>
    public static bool ClipLine(int width, int height, ref int p1x, ref int p1y, ref int p2x, ref int p2y)
    {
        long x1, y1, x2, y2;
        int c1, c2;
        long right = width - 1, bottom = height - 1;

        if (width <= 0 || height <= 0) return false;

        x1 = p1x; y1 = p1y; x2 = p2x; y2 = p2y;
        c1 = (x1 < 0 ? 1 : 0) + (x1 > right ? 1 : 0) * 2 + (y1 < 0 ? 1 : 0) * 4 + (y1 > bottom ? 1 : 0) * 8;
        c2 = (x2 < 0 ? 1 : 0) + (x2 > right ? 1 : 0) * 2 + (y2 < 0 ? 1 : 0) * 4 + (y2 > bottom ? 1 : 0) * 8;

        if ((c1 & c2) == 0 && (c1 | c2) != 0)
        {
            long a;
            if ((c1 & 12) != 0)
            {
                a = c1 < 8 ? 0 : bottom;
                x1 += (a - y1) * (x2 - x1) / (y2 - y1);
                y1 = a;
                c1 = (x1 < 0 ? 1 : 0) + (x1 > right ? 1 : 0) * 2;
            }
            if ((c2 & 12) != 0)
            {
                a = c2 < 8 ? 0 : bottom;
                x2 += (a - y2) * (x2 - x1) / (y2 - y1);
                y2 = a;
                c2 = (x2 < 0 ? 1 : 0) + (x2 > right ? 1 : 0) * 2;
            }
            if ((c1 & c2) == 0 && (c1 | c2) != 0)
            {
                if (c1 != 0)
                {
                    a = c1 == 1 ? 0 : right;
                    y1 += (a - x1) * (y2 - y1) / (x2 - x1);
                    x1 = a;
                    c1 = 0;
                }
                if (c2 != 0)
                {
                    a = c2 == 1 ? 0 : right;
                    y2 += (a - x2) * (y2 - y1) / (x2 - x1);
                    x2 = a;
                    c2 = 0;
                }
            }

            p1x = (int)x1;
            p1y = (int)y1;
            p2x = (int)x2;
            p2y = (int)y2;
        }

        return (c1 | c2) == 0;
    }

    // ------------------------------------------------------------------ Line / LineIterator (A8, A9)

    /// <summary>
    /// The static <c>Line(img, pt1, pt2, color, connectivity)</c> (gap3 A8): connectivity 0 becomes 8 and 1 becomes 4;
    /// a <c>LineIterator(img, pt1, pt2, conn, left_to_right = true)</c> walks the line and each point is written with
    /// the colour's first byte (pix_size 1).
    /// </summary>
    public static void Line(byte[] img, int rows, int cols, int p1x, int p1y, int p2x, int p2y, byte color, int connectivity)
    {
        if (connectivity == 0) connectivity = 8;
        else if (connectivity == 1) connectivity = 4;

        var it = new LineIterator(img, rows, cols, p1x, p1y, p2x, p2y, connectivity, leftToRight: true);
        for (int i = 0; i < it.Count; i++, it.Next()) img[it.Ptr] = color;
    }

    /// <summary>
    /// <c>LineIterator</c> (gap3 A9) over a CV_8UC1 image, as an offset into its buffer: clipLine only when a coordinate
    /// is out of range (a failed clip gives count 0 and the image's start), the left_to_right swap, the dy sign and the
    /// xor swaps; conn 4: err 0, plusDelta = 2dx + 2dy, minusDelta = −2dy, plusStep = step − 1, minusStep = 1,
    /// count = dx + dy + 1; conn 8 as stock. <see cref="Next"/> is operator++: mask = err &gt;&gt; 31;
    /// err += minusDelta + (plusDelta &amp; mask); ptr += minusStep + (plusStep &amp; mask).
    /// </summary>
    public struct LineIterator
    {
        public int Ptr;
        public int Err, PlusDelta, MinusDelta, PlusStep, MinusStep, Count;

        public LineIterator(byte[] img, int rows, int cols, int p1x, int p1y, int p2x, int p2y, int connectivity, bool leftToRight)
        {
            _ = img;
            Count = -1;
            if (connectivity != 8 && connectivity != 4)
                throw new ArgumentException("CV_Assert(connectivity == 8 || connectivity == 4)", nameof(connectivity));

            if ((uint)p1x >= (uint)cols || (uint)p2x >= (uint)cols || (uint)p1y >= (uint)rows || (uint)p2y >= (uint)rows)
            {
                if (!ClipLine(cols, rows, ref p1x, ref p1y, ref p2x, ref p2y))
                {
                    Ptr = 0;
                    Err = PlusDelta = MinusDelta = PlusStep = MinusStep = Count = 0;
                    return;
                }
            }

            int btPix0 = 1, btPix = btPix0;
            int istep = cols;

            int dx = p2x - p1x;
            int dy = p2y - p1y;
            int s = dx < 0 ? -1 : 0;

            if (leftToRight)
            {
                dx = (dx ^ s) - s;
                dy = (dy ^ s) - s;
                p1x ^= (p1x ^ p2x) & s;
                p1y ^= (p1y ^ p2y) & s;
            }
            else
            {
                dx = (dx ^ s) - s;
                btPix = (btPix ^ s) - s;
            }

            Ptr = p1y * istep + p1x * btPix0;

            s = dy < 0 ? -1 : 0;
            dy = (dy ^ s) - s;
            istep = (istep ^ s) - s;

            s = dy > dx ? -1 : 0;

            // conditional swaps
            dx ^= dy & s;
            dy ^= dx & s;
            dx ^= dy & s;

            btPix ^= istep & s;
            istep ^= btPix & s;
            btPix ^= istep & s;

            if (connectivity == 8)
            {
                Err = dx - (dy + dy);
                PlusDelta = dx + dx;
                MinusDelta = -(dy + dy);
                PlusStep = istep;
                MinusStep = btPix;
                Count = dx + 1;
            }
            else
            {
                Err = 0;
                PlusDelta = (dx + dx) + (dy + dy);
                MinusDelta = -(dy + dy);
                PlusStep = istep - btPix;
                MinusStep = btPix;
                Count = dx + dy + 1;
            }
        }

        /// <summary>operator++.</summary>
        public void Next()
        {
            int mask = Err < 0 ? -1 : 0;
            Err += MinusDelta + (PlusDelta & mask);
            Ptr += MinusStep + (PlusStep & mask);
        }
    }

    // ------------------------------------------------------------------ fillConvexPoly (A1..A7)

    /// <summary>
    /// <c>cv::fillConvexPoly(img, points, Scalar(color), lineType, shift)</c> on a CV_8UC1 image (gap3 A1..A7):
    /// the InputArray wrapper and the Mat&amp; overload (null or empty points return; CV_AA on a non-8U depth becomes 8,
    /// which cannot arise here; shift ≤ 16), then the internal <c>FillConvexPoly</c>. The engine always passes lineType 4
    /// and shift 0 (gap1 D6). An empty point vector fails the wrapper's <c>checkVector</c> assert in stock 3.1.0; the
    /// engine skips an empty lid (D6), so this throws rather than guess.
    /// </summary>
    public static void FillConvexPoly(byte[] img, int rows, int cols, IReadOnlyList<(int X, int Y)> v, byte color,
                                      int lineType = 4, int shift = 0)
    {
        if (v.Count == 0)
            throw new ArgumentException("cv::fillConvexPoly: CV_Assert(points.checkVector(2, CV_32S) >= 0) fails for an empty vector");
        if (shift < 0 || shift > XyShift) throw new ArgumentOutOfRangeException(nameof(shift));
        FillConvexPolyInternal(img, rows, cols, v, color, lineType, shift);
    }

    private static void FillConvexPolyInternal(byte[] img, int rows, int cols, IReadOnlyList<(int X, int Y)> v,
                                               byte color, int lineType, int shift)
    {
        const int CvAa = 16;
        int npts = v.Count;
        Span<int> eIdx = stackalloc int[2], eDi = stackalloc int[2], eX = stackalloc int[2], eDx = stackalloc int[2], eYe = stackalloc int[2];

        int delta = shift != 0 ? 1 << (shift - 1) : 0;
        int i, y, imin = 0, left = 0, right = 1, x1, x2;
        int edges = npts;
        int xmin, xmax, ymin, ymax;
        int delta1, delta2;

        if (lineType < CvAa) delta1 = delta2 = XyOne >> 1;
        else { delta1 = XyOne - 1; delta2 = 0; }

        int p0x = v[npts - 1].X << (XyShift - shift);
        int p0y = v[npts - 1].Y << (XyShift - shift);

        xmin = xmax = v[0].X;
        ymin = ymax = v[0].Y;

        for (i = 0; i < npts; i++)
        {
            int px = v[i].X, py = v[i].Y;
            if (py < ymin)
            {
                ymin = py;
                imin = i;
            }

            ymax = Math.Max(ymax, py);
            xmax = Math.Max(xmax, px);
            xmin = Math.Min(xmin, px);

            px <<= XyShift - shift;
            py <<= XyShift - shift;

            if (lineType <= 8)
            {
                if (shift == 0)
                    Line(img, rows, cols, p0x >> XyShift, p0y >> XyShift, px >> XyShift, py >> XyShift, color, lineType);
                else
                    throw new NotSupportedException("Line2 (shift != 0) is not on the engine's path and is not ported (gap3)");
            }
            else
                throw new NotSupportedException("LineAA is not on the engine's path and is not ported (gap3)");
            p0x = px; p0y = py;
        }

        xmin = (xmin + delta) >> shift;
        xmax = (xmax + delta) >> shift;
        ymin = (ymin + delta) >> shift;
        ymax = (ymax + delta) >> shift;

        if (npts < 3 || xmax < 0 || ymax < 0 || xmin >= cols || ymin >= rows)
            return;

        ymax = Math.Min(ymax, rows - 1);
        eIdx[0] = eIdx[1] = imin;

        eYe[0] = eYe[1] = y = ymin;
        eDi[0] = 1;
        eDi[1] = npts - 1;
        eX[0] = eX[1] = 0;
        eDx[0] = eDx[1] = 0;

        int ptr = cols * y;

        do
        {
            if (lineType < CvAa || y < ymax || y == ymin)
            {
                for (i = 0; i < 2; i++)
                {
                    if (y >= eYe[i])
                    {
                        int idx = eIdx[i], di = eDi[i];
                        int xs = 0, xe, ye, ty = 0;

                        for (;;)
                        {
                            ty = (v[idx].Y + delta) >> shift;
                            if (ty > y || edges == 0)
                                break;
                            xs = v[idx].X;
                            idx += di;
                            idx -= ((idx < npts ? 1 : 0) - 1) & npts;   // idx -= idx >= npts ? npts : 0
                            edges--;
                        }

                        ye = ty;
                        xs <<= XyShift - shift;
                        xe = v[idx].X << (XyShift - shift);

                        // no more edges
                        if (y >= ye)
                            return;

                        eYe[i] = ye;
                        eDx[i] = ((xe - xs) * 2 + (ye - y)) / (2 * (ye - y));
                        eX[i] = xs;
                        eIdx[i] = idx;
                    }
                }
            }

            if (eX[left] > eX[right])
            {
                left ^= 1;
                right ^= 1;
            }

            x1 = eX[left];
            x2 = eX[right];

            if (y >= 0)
            {
                int xx1 = (x1 + delta1) >> XyShift;
                int xx2 = (x2 + delta2) >> XyShift;

                if (xx2 >= 0 && xx1 < cols)
                {
                    if (xx1 < 0) xx1 = 0;
                    if (xx2 >= cols) xx2 = cols - 1;
                    for (int x = xx1; x <= xx2; x++) img[ptr + x] = color;      // ICV_HLINE, pix_size 1
                }
            }

            x1 += eDx[left];
            x2 += eDx[right];

            eX[left] = x1;
            eX[right] = x2;
            ptr += cols;
        }
        while (++y <= ymax);
    }

    // ------------------------------------------------------------------ ellipse2Poly (B1..B3)

    /// <summary>The stock 451-entry sine table (drawing.cpp), byte-identical in the shipped build (gap3 B2, 0x000E7910).</summary>
    internal static readonly float[] SinTable =
    {
        BitConverter.Int32BitsToSingle(unchecked((int)0x00000000)), // libopencv_imgproc.so 0x000E7910
        BitConverter.Int32BitsToSingle(unchecked((int)0x3C8EF856)), // libopencv_imgproc.so 0x000E7914
        BitConverter.Int32BitsToSingle(unchecked((int)0x3D0EF2C7)), // libopencv_imgproc.so 0x000E7918
        BitConverter.Int32BitsToSingle(unchecked((int)0x3D565E46)), // libopencv_imgproc.so 0x000E791C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3D8EDC7F)), // libopencv_imgproc.so 0x000E7920
        BitConverter.Int32BitsToSingle(unchecked((int)0x3DB27EB0)), // libopencv_imgproc.so 0x000E7924
        BitConverter.Int32BitsToSingle(unchecked((int)0x3DD6130A)), // libopencv_imgproc.so 0x000E7928
        BitConverter.Int32BitsToSingle(unchecked((int)0x3DF9969D)), // libopencv_imgproc.so 0x000E792C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E0E8365)), // libopencv_imgproc.so 0x000E7930
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E20305E)), // libopencv_imgproc.so 0x000E7934
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E31D0D5)), // libopencv_imgproc.so 0x000E7938
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E43636F)), // libopencv_imgproc.so 0x000E793C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E54E6CE)), // libopencv_imgproc.so 0x000E7940
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E665995)), // libopencv_imgproc.so 0x000E7944
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E77BA60)), // libopencv_imgproc.so 0x000E7948
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E8483ED)), // libopencv_imgproc.so 0x000E794C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E8D2058)), // libopencv_imgproc.so 0x000E7950
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E95B1BE)), // libopencv_imgproc.so 0x000E7954
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E9E377A)), // libopencv_imgproc.so 0x000E7958
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EA6B0E0)), // libopencv_imgproc.so 0x000E795C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EAF1D42)), // libopencv_imgproc.so 0x000E7960
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EB77BFF)), // libopencv_imgproc.so 0x000E7964
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EBFCC70)), // libopencv_imgproc.so 0x000E7968
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EC80DE8)), // libopencv_imgproc.so 0x000E796C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3ED03FC8)), // libopencv_imgproc.so 0x000E7970
        BitConverter.Int32BitsToSingle(unchecked((int)0x3ED8616D)), // libopencv_imgproc.so 0x000E7974
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EE0722D)), // libopencv_imgproc.so 0x000E7978
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EE87171)), // libopencv_imgproc.so 0x000E797C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EF05E95)), // libopencv_imgproc.so 0x000E7980
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EF838F7)), // libopencv_imgproc.so 0x000E7984
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F000000)), // libopencv_imgproc.so 0x000E7988
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F03D989)), // libopencv_imgproc.so 0x000E798C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F07A8CB)), // libopencv_imgproc.so 0x000E7990
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F0B6D76)), // libopencv_imgproc.so 0x000E7994
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F0F2744)), // libopencv_imgproc.so 0x000E7998
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F12D5E7)), // libopencv_imgproc.so 0x000E799C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F167919)), // libopencv_imgproc.so 0x000E79A0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F1A108C)), // libopencv_imgproc.so 0x000E79A4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F1D9BFE)), // libopencv_imgproc.so 0x000E79A8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F211B24)), // libopencv_imgproc.so 0x000E79AC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F248DBA)), // libopencv_imgproc.so 0x000E79B0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F27F37C)), // libopencv_imgproc.so 0x000E79B4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F2B4C25)), // libopencv_imgproc.so 0x000E79B8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F2E9772)), // libopencv_imgproc.so 0x000E79BC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F31D522)), // libopencv_imgproc.so 0x000E79C0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F3504F4)), // libopencv_imgproc.so 0x000E79C4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F3826A7)), // libopencv_imgproc.so 0x000E79C8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F3B39FF)), // libopencv_imgproc.so 0x000E79CC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F3E3EBD)), // libopencv_imgproc.so 0x000E79D0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F4134A6)), // libopencv_imgproc.so 0x000E79D4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F441B7C)), // libopencv_imgproc.so 0x000E79D8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F46F30A)), // libopencv_imgproc.so 0x000E79DC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F49BB13)), // libopencv_imgproc.so 0x000E79E0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F4C7360)), // libopencv_imgproc.so 0x000E79E4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F4F1BBD)), // libopencv_imgproc.so 0x000E79E8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F51B3F2)), // libopencv_imgproc.so 0x000E79EC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F543BCF)), // libopencv_imgproc.so 0x000E79F0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F56B31E)), // libopencv_imgproc.so 0x000E79F4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F5919AE)), // libopencv_imgproc.so 0x000E79F8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F5B6F51)), // libopencv_imgproc.so 0x000E79FC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F5DB3D7)), // libopencv_imgproc.so 0x000E7A00
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F5FE714)), // libopencv_imgproc.so 0x000E7A04
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6208DB)), // libopencv_imgproc.so 0x000E7A08
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F641901)), // libopencv_imgproc.so 0x000E7A0C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F66175D)), // libopencv_imgproc.so 0x000E7A10
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6803CA)), // libopencv_imgproc.so 0x000E7A14
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F69DE1E)), // libopencv_imgproc.so 0x000E7A18
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6BA636)), // libopencv_imgproc.so 0x000E7A1C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6D5BED)), // libopencv_imgproc.so 0x000E7A20
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6EFF20)), // libopencv_imgproc.so 0x000E7A24
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F708FB2)), // libopencv_imgproc.so 0x000E7A28
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F720D82)), // libopencv_imgproc.so 0x000E7A2C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F737870)), // libopencv_imgproc.so 0x000E7A30
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F74D064)), // libopencv_imgproc.so 0x000E7A34
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F76153F)), // libopencv_imgproc.so 0x000E7A38
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7746EA)), // libopencv_imgproc.so 0x000E7A3C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F78654D)), // libopencv_imgproc.so 0x000E7A40
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F797052)), // libopencv_imgproc.so 0x000E7A44
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7A67E2)), // libopencv_imgproc.so 0x000E7A48
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7B4BEC)), // libopencv_imgproc.so 0x000E7A4C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7C1C5D)), // libopencv_imgproc.so 0x000E7A50
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7CD924)), // libopencv_imgproc.so 0x000E7A54
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7D8236)), // libopencv_imgproc.so 0x000E7A58
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7E1782)), // libopencv_imgproc.so 0x000E7A5C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7E98FD)), // libopencv_imgproc.so 0x000E7A60
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7F069E)), // libopencv_imgproc.so 0x000E7A64
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7F605C)), // libopencv_imgproc.so 0x000E7A68
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7FA62F)), // libopencv_imgproc.so 0x000E7A6C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7FD813)), // libopencv_imgproc.so 0x000E7A70
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7FF605)), // libopencv_imgproc.so 0x000E7A74
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F800000)), // libopencv_imgproc.so 0x000E7A78
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7FF605)), // libopencv_imgproc.so 0x000E7A7C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7FD813)), // libopencv_imgproc.so 0x000E7A80
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7FA62F)), // libopencv_imgproc.so 0x000E7A84
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7F605C)), // libopencv_imgproc.so 0x000E7A88
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7F069E)), // libopencv_imgproc.so 0x000E7A8C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7E98FD)), // libopencv_imgproc.so 0x000E7A90
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7E1782)), // libopencv_imgproc.so 0x000E7A94
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7D8236)), // libopencv_imgproc.so 0x000E7A98
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7CD924)), // libopencv_imgproc.so 0x000E7A9C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7C1C5D)), // libopencv_imgproc.so 0x000E7AA0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7B4BEC)), // libopencv_imgproc.so 0x000E7AA4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7A67E2)), // libopencv_imgproc.so 0x000E7AA8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F797052)), // libopencv_imgproc.so 0x000E7AAC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F78654D)), // libopencv_imgproc.so 0x000E7AB0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7746EA)), // libopencv_imgproc.so 0x000E7AB4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F76153F)), // libopencv_imgproc.so 0x000E7AB8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F74D064)), // libopencv_imgproc.so 0x000E7ABC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F737870)), // libopencv_imgproc.so 0x000E7AC0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F720D82)), // libopencv_imgproc.so 0x000E7AC4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F708FB2)), // libopencv_imgproc.so 0x000E7AC8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6EFF20)), // libopencv_imgproc.so 0x000E7ACC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6D5BED)), // libopencv_imgproc.so 0x000E7AD0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6BA636)), // libopencv_imgproc.so 0x000E7AD4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F69DE1E)), // libopencv_imgproc.so 0x000E7AD8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6803CA)), // libopencv_imgproc.so 0x000E7ADC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F66175D)), // libopencv_imgproc.so 0x000E7AE0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F641901)), // libopencv_imgproc.so 0x000E7AE4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6208DB)), // libopencv_imgproc.so 0x000E7AE8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F5FE714)), // libopencv_imgproc.so 0x000E7AEC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F5DB3D7)), // libopencv_imgproc.so 0x000E7AF0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F5B6F51)), // libopencv_imgproc.so 0x000E7AF4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F5919AE)), // libopencv_imgproc.so 0x000E7AF8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F56B31E)), // libopencv_imgproc.so 0x000E7AFC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F543BCF)), // libopencv_imgproc.so 0x000E7B00
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F51B3F2)), // libopencv_imgproc.so 0x000E7B04
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F4F1BBD)), // libopencv_imgproc.so 0x000E7B08
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F4C7360)), // libopencv_imgproc.so 0x000E7B0C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F49BB13)), // libopencv_imgproc.so 0x000E7B10
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F46F30A)), // libopencv_imgproc.so 0x000E7B14
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F441B7C)), // libopencv_imgproc.so 0x000E7B18
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F4134A6)), // libopencv_imgproc.so 0x000E7B1C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F3E3EBD)), // libopencv_imgproc.so 0x000E7B20
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F3B39FF)), // libopencv_imgproc.so 0x000E7B24
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F3826A7)), // libopencv_imgproc.so 0x000E7B28
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F3504F4)), // libopencv_imgproc.so 0x000E7B2C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F31D522)), // libopencv_imgproc.so 0x000E7B30
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F2E9772)), // libopencv_imgproc.so 0x000E7B34
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F2B4C25)), // libopencv_imgproc.so 0x000E7B38
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F27F37C)), // libopencv_imgproc.so 0x000E7B3C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F248DBA)), // libopencv_imgproc.so 0x000E7B40
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F211B24)), // libopencv_imgproc.so 0x000E7B44
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F1D9BFE)), // libopencv_imgproc.so 0x000E7B48
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F1A108C)), // libopencv_imgproc.so 0x000E7B4C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F167919)), // libopencv_imgproc.so 0x000E7B50
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F12D5E7)), // libopencv_imgproc.so 0x000E7B54
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F0F2744)), // libopencv_imgproc.so 0x000E7B58
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F0B6D76)), // libopencv_imgproc.so 0x000E7B5C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F07A8CB)), // libopencv_imgproc.so 0x000E7B60
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F03D989)), // libopencv_imgproc.so 0x000E7B64
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F000000)), // libopencv_imgproc.so 0x000E7B68
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EF838F7)), // libopencv_imgproc.so 0x000E7B6C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EF05E95)), // libopencv_imgproc.so 0x000E7B70
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EE87171)), // libopencv_imgproc.so 0x000E7B74
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EE0722D)), // libopencv_imgproc.so 0x000E7B78
        BitConverter.Int32BitsToSingle(unchecked((int)0x3ED8616D)), // libopencv_imgproc.so 0x000E7B7C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3ED03FC8)), // libopencv_imgproc.so 0x000E7B80
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EC80DE8)), // libopencv_imgproc.so 0x000E7B84
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EBFCC70)), // libopencv_imgproc.so 0x000E7B88
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EB77BFF)), // libopencv_imgproc.so 0x000E7B8C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EAF1D42)), // libopencv_imgproc.so 0x000E7B90
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EA6B0E0)), // libopencv_imgproc.so 0x000E7B94
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E9E377A)), // libopencv_imgproc.so 0x000E7B98
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E95B1BE)), // libopencv_imgproc.so 0x000E7B9C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E8D2058)), // libopencv_imgproc.so 0x000E7BA0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E8483ED)), // libopencv_imgproc.so 0x000E7BA4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E77BA60)), // libopencv_imgproc.so 0x000E7BA8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E665995)), // libopencv_imgproc.so 0x000E7BAC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E54E6CE)), // libopencv_imgproc.so 0x000E7BB0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E43636F)), // libopencv_imgproc.so 0x000E7BB4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E31D0D5)), // libopencv_imgproc.so 0x000E7BB8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E20305E)), // libopencv_imgproc.so 0x000E7BBC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E0E8365)), // libopencv_imgproc.so 0x000E7BC0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3DF9969D)), // libopencv_imgproc.so 0x000E7BC4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3DD6130A)), // libopencv_imgproc.so 0x000E7BC8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3DB27EB0)), // libopencv_imgproc.so 0x000E7BCC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3D8EDC7F)), // libopencv_imgproc.so 0x000E7BD0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3D565E46)), // libopencv_imgproc.so 0x000E7BD4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3D0EF2C7)), // libopencv_imgproc.so 0x000E7BD8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3C8EF856)), // libopencv_imgproc.so 0x000E7BDC
        BitConverter.Int32BitsToSingle(unchecked((int)0x00000000)), // libopencv_imgproc.so 0x000E7BE0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBC8EF856)), // libopencv_imgproc.so 0x000E7BE4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBD0EF2C7)), // libopencv_imgproc.so 0x000E7BE8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBD565E46)), // libopencv_imgproc.so 0x000E7BEC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBD8EDC7F)), // libopencv_imgproc.so 0x000E7BF0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBDB27EB0)), // libopencv_imgproc.so 0x000E7BF4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBDD6130A)), // libopencv_imgproc.so 0x000E7BF8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBDF9969D)), // libopencv_imgproc.so 0x000E7BFC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE0E8365)), // libopencv_imgproc.so 0x000E7C00
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE20305E)), // libopencv_imgproc.so 0x000E7C04
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE31D0D5)), // libopencv_imgproc.so 0x000E7C08
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE43636F)), // libopencv_imgproc.so 0x000E7C0C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE54E6CE)), // libopencv_imgproc.so 0x000E7C10
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE665995)), // libopencv_imgproc.so 0x000E7C14
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE77BA60)), // libopencv_imgproc.so 0x000E7C18
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE8483ED)), // libopencv_imgproc.so 0x000E7C1C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE8D2058)), // libopencv_imgproc.so 0x000E7C20
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE95B1BE)), // libopencv_imgproc.so 0x000E7C24
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE9E377A)), // libopencv_imgproc.so 0x000E7C28
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEA6B0E0)), // libopencv_imgproc.so 0x000E7C2C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEAF1D42)), // libopencv_imgproc.so 0x000E7C30
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEB77BFF)), // libopencv_imgproc.so 0x000E7C34
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEBFCC70)), // libopencv_imgproc.so 0x000E7C38
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEC80DE8)), // libopencv_imgproc.so 0x000E7C3C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBED03FC8)), // libopencv_imgproc.so 0x000E7C40
        BitConverter.Int32BitsToSingle(unchecked((int)0xBED8616D)), // libopencv_imgproc.so 0x000E7C44
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEE0722D)), // libopencv_imgproc.so 0x000E7C48
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEE87171)), // libopencv_imgproc.so 0x000E7C4C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEF05E95)), // libopencv_imgproc.so 0x000E7C50
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEF838F7)), // libopencv_imgproc.so 0x000E7C54
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF000000)), // libopencv_imgproc.so 0x000E7C58
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF03D989)), // libopencv_imgproc.so 0x000E7C5C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF07A8CB)), // libopencv_imgproc.so 0x000E7C60
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF0B6D76)), // libopencv_imgproc.so 0x000E7C64
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF0F2744)), // libopencv_imgproc.so 0x000E7C68
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF12D5E7)), // libopencv_imgproc.so 0x000E7C6C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF167919)), // libopencv_imgproc.so 0x000E7C70
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF1A108C)), // libopencv_imgproc.so 0x000E7C74
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF1D9BFE)), // libopencv_imgproc.so 0x000E7C78
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF211B24)), // libopencv_imgproc.so 0x000E7C7C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF248DBA)), // libopencv_imgproc.so 0x000E7C80
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF27F37C)), // libopencv_imgproc.so 0x000E7C84
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF2B4C25)), // libopencv_imgproc.so 0x000E7C88
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF2E9772)), // libopencv_imgproc.so 0x000E7C8C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF31D522)), // libopencv_imgproc.so 0x000E7C90
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF3504F4)), // libopencv_imgproc.so 0x000E7C94
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF3826A7)), // libopencv_imgproc.so 0x000E7C98
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF3B39FF)), // libopencv_imgproc.so 0x000E7C9C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF3E3EBD)), // libopencv_imgproc.so 0x000E7CA0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF4134A6)), // libopencv_imgproc.so 0x000E7CA4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF441B7C)), // libopencv_imgproc.so 0x000E7CA8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF46F30A)), // libopencv_imgproc.so 0x000E7CAC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF49BB13)), // libopencv_imgproc.so 0x000E7CB0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF4C7360)), // libopencv_imgproc.so 0x000E7CB4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF4F1BBD)), // libopencv_imgproc.so 0x000E7CB8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF51B3F2)), // libopencv_imgproc.so 0x000E7CBC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF543BCF)), // libopencv_imgproc.so 0x000E7CC0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF56B31E)), // libopencv_imgproc.so 0x000E7CC4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF5919AE)), // libopencv_imgproc.so 0x000E7CC8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF5B6F51)), // libopencv_imgproc.so 0x000E7CCC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF5DB3D7)), // libopencv_imgproc.so 0x000E7CD0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF5FE714)), // libopencv_imgproc.so 0x000E7CD4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF6208DB)), // libopencv_imgproc.so 0x000E7CD8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF641901)), // libopencv_imgproc.so 0x000E7CDC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF66175D)), // libopencv_imgproc.so 0x000E7CE0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF6803CA)), // libopencv_imgproc.so 0x000E7CE4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF69DE1E)), // libopencv_imgproc.so 0x000E7CE8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF6BA636)), // libopencv_imgproc.so 0x000E7CEC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF6D5BED)), // libopencv_imgproc.so 0x000E7CF0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF6EFF20)), // libopencv_imgproc.so 0x000E7CF4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF708FB2)), // libopencv_imgproc.so 0x000E7CF8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF720D82)), // libopencv_imgproc.so 0x000E7CFC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF737870)), // libopencv_imgproc.so 0x000E7D00
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF74D064)), // libopencv_imgproc.so 0x000E7D04
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF76153F)), // libopencv_imgproc.so 0x000E7D08
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7746EA)), // libopencv_imgproc.so 0x000E7D0C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF78654D)), // libopencv_imgproc.so 0x000E7D10
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF797052)), // libopencv_imgproc.so 0x000E7D14
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7A67E2)), // libopencv_imgproc.so 0x000E7D18
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7B4BEC)), // libopencv_imgproc.so 0x000E7D1C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7C1C5D)), // libopencv_imgproc.so 0x000E7D20
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7CD924)), // libopencv_imgproc.so 0x000E7D24
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7D8236)), // libopencv_imgproc.so 0x000E7D28
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7E1782)), // libopencv_imgproc.so 0x000E7D2C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7E98FD)), // libopencv_imgproc.so 0x000E7D30
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7F069E)), // libopencv_imgproc.so 0x000E7D34
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7F605C)), // libopencv_imgproc.so 0x000E7D38
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7FA62F)), // libopencv_imgproc.so 0x000E7D3C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7FD813)), // libopencv_imgproc.so 0x000E7D40
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7FF605)), // libopencv_imgproc.so 0x000E7D44
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF800000)), // libopencv_imgproc.so 0x000E7D48
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7FF605)), // libopencv_imgproc.so 0x000E7D4C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7FD813)), // libopencv_imgproc.so 0x000E7D50
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7FA62F)), // libopencv_imgproc.so 0x000E7D54
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7F605C)), // libopencv_imgproc.so 0x000E7D58
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7F069E)), // libopencv_imgproc.so 0x000E7D5C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7E98FD)), // libopencv_imgproc.so 0x000E7D60
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7E1782)), // libopencv_imgproc.so 0x000E7D64
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7D8236)), // libopencv_imgproc.so 0x000E7D68
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7CD924)), // libopencv_imgproc.so 0x000E7D6C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7C1C5D)), // libopencv_imgproc.so 0x000E7D70
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7B4BEC)), // libopencv_imgproc.so 0x000E7D74
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7A67E2)), // libopencv_imgproc.so 0x000E7D78
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF797052)), // libopencv_imgproc.so 0x000E7D7C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF78654D)), // libopencv_imgproc.so 0x000E7D80
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF7746EA)), // libopencv_imgproc.so 0x000E7D84
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF76153F)), // libopencv_imgproc.so 0x000E7D88
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF74D064)), // libopencv_imgproc.so 0x000E7D8C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF737870)), // libopencv_imgproc.so 0x000E7D90
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF720D82)), // libopencv_imgproc.so 0x000E7D94
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF708FB2)), // libopencv_imgproc.so 0x000E7D98
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF6EFF20)), // libopencv_imgproc.so 0x000E7D9C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF6D5BED)), // libopencv_imgproc.so 0x000E7DA0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF6BA636)), // libopencv_imgproc.so 0x000E7DA4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF69DE1E)), // libopencv_imgproc.so 0x000E7DA8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF6803CA)), // libopencv_imgproc.so 0x000E7DAC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF66175D)), // libopencv_imgproc.so 0x000E7DB0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF641901)), // libopencv_imgproc.so 0x000E7DB4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF6208DB)), // libopencv_imgproc.so 0x000E7DB8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF5FE714)), // libopencv_imgproc.so 0x000E7DBC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF5DB3D7)), // libopencv_imgproc.so 0x000E7DC0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF5B6F51)), // libopencv_imgproc.so 0x000E7DC4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF5919AE)), // libopencv_imgproc.so 0x000E7DC8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF56B31E)), // libopencv_imgproc.so 0x000E7DCC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF543BCF)), // libopencv_imgproc.so 0x000E7DD0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF51B3F2)), // libopencv_imgproc.so 0x000E7DD4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF4F1BBD)), // libopencv_imgproc.so 0x000E7DD8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF4C7360)), // libopencv_imgproc.so 0x000E7DDC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF49BB13)), // libopencv_imgproc.so 0x000E7DE0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF46F30A)), // libopencv_imgproc.so 0x000E7DE4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF441B7C)), // libopencv_imgproc.so 0x000E7DE8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF4134A6)), // libopencv_imgproc.so 0x000E7DEC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF3E3EBD)), // libopencv_imgproc.so 0x000E7DF0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF3B39FF)), // libopencv_imgproc.so 0x000E7DF4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF3826A7)), // libopencv_imgproc.so 0x000E7DF8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF3504F4)), // libopencv_imgproc.so 0x000E7DFC
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF31D522)), // libopencv_imgproc.so 0x000E7E00
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF2E9772)), // libopencv_imgproc.so 0x000E7E04
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF2B4C25)), // libopencv_imgproc.so 0x000E7E08
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF27F37C)), // libopencv_imgproc.so 0x000E7E0C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF248DBA)), // libopencv_imgproc.so 0x000E7E10
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF211B24)), // libopencv_imgproc.so 0x000E7E14
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF1D9BFE)), // libopencv_imgproc.so 0x000E7E18
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF1A108C)), // libopencv_imgproc.so 0x000E7E1C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF167919)), // libopencv_imgproc.so 0x000E7E20
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF12D5E7)), // libopencv_imgproc.so 0x000E7E24
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF0F2744)), // libopencv_imgproc.so 0x000E7E28
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF0B6D76)), // libopencv_imgproc.so 0x000E7E2C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF07A8CB)), // libopencv_imgproc.so 0x000E7E30
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF03D989)), // libopencv_imgproc.so 0x000E7E34
        BitConverter.Int32BitsToSingle(unchecked((int)0xBF000000)), // libopencv_imgproc.so 0x000E7E38
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEF838F7)), // libopencv_imgproc.so 0x000E7E3C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEF05E95)), // libopencv_imgproc.so 0x000E7E40
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEE87171)), // libopencv_imgproc.so 0x000E7E44
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEE0722D)), // libopencv_imgproc.so 0x000E7E48
        BitConverter.Int32BitsToSingle(unchecked((int)0xBED8616D)), // libopencv_imgproc.so 0x000E7E4C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBED03FC8)), // libopencv_imgproc.so 0x000E7E50
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEC80DE8)), // libopencv_imgproc.so 0x000E7E54
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEBFCC70)), // libopencv_imgproc.so 0x000E7E58
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEB77BFF)), // libopencv_imgproc.so 0x000E7E5C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEAF1D42)), // libopencv_imgproc.so 0x000E7E60
        BitConverter.Int32BitsToSingle(unchecked((int)0xBEA6B0E0)), // libopencv_imgproc.so 0x000E7E64
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE9E377A)), // libopencv_imgproc.so 0x000E7E68
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE95B1BE)), // libopencv_imgproc.so 0x000E7E6C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE8D2058)), // libopencv_imgproc.so 0x000E7E70
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE8483ED)), // libopencv_imgproc.so 0x000E7E74
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE77BA60)), // libopencv_imgproc.so 0x000E7E78
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE665995)), // libopencv_imgproc.so 0x000E7E7C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE54E6CE)), // libopencv_imgproc.so 0x000E7E80
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE43636F)), // libopencv_imgproc.so 0x000E7E84
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE31D0D5)), // libopencv_imgproc.so 0x000E7E88
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE20305E)), // libopencv_imgproc.so 0x000E7E8C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE0E8365)), // libopencv_imgproc.so 0x000E7E90
        BitConverter.Int32BitsToSingle(unchecked((int)0xBDF9969D)), // libopencv_imgproc.so 0x000E7E94
        BitConverter.Int32BitsToSingle(unchecked((int)0xBDD6130A)), // libopencv_imgproc.so 0x000E7E98
        BitConverter.Int32BitsToSingle(unchecked((int)0xBDB27EB0)), // libopencv_imgproc.so 0x000E7E9C
        BitConverter.Int32BitsToSingle(unchecked((int)0xBD8EDC7F)), // libopencv_imgproc.so 0x000E7EA0
        BitConverter.Int32BitsToSingle(unchecked((int)0xBD565E46)), // libopencv_imgproc.so 0x000E7EA4
        BitConverter.Int32BitsToSingle(unchecked((int)0xBD0EF2C7)), // libopencv_imgproc.so 0x000E7EA8
        BitConverter.Int32BitsToSingle(unchecked((int)0xBC8EF856)), // libopencv_imgproc.so 0x000E7EAC
        BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)), // libopencv_imgproc.so 0x000E7EB0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3C8EF856)), // libopencv_imgproc.so 0x000E7EB4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3D0EF2C7)), // libopencv_imgproc.so 0x000E7EB8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3D565E46)), // libopencv_imgproc.so 0x000E7EBC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3D8EDC7F)), // libopencv_imgproc.so 0x000E7EC0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3DB27EB0)), // libopencv_imgproc.so 0x000E7EC4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3DD6130A)), // libopencv_imgproc.so 0x000E7EC8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3DF9969D)), // libopencv_imgproc.so 0x000E7ECC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E0E8365)), // libopencv_imgproc.so 0x000E7ED0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E20305E)), // libopencv_imgproc.so 0x000E7ED4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E31D0D5)), // libopencv_imgproc.so 0x000E7ED8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E43636F)), // libopencv_imgproc.so 0x000E7EDC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E54E6CE)), // libopencv_imgproc.so 0x000E7EE0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E665995)), // libopencv_imgproc.so 0x000E7EE4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E77BA60)), // libopencv_imgproc.so 0x000E7EE8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E8483ED)), // libopencv_imgproc.so 0x000E7EEC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E8D2058)), // libopencv_imgproc.so 0x000E7EF0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E95B1BE)), // libopencv_imgproc.so 0x000E7EF4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3E9E377A)), // libopencv_imgproc.so 0x000E7EF8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EA6B0E0)), // libopencv_imgproc.so 0x000E7EFC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EAF1D42)), // libopencv_imgproc.so 0x000E7F00
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EB77BFF)), // libopencv_imgproc.so 0x000E7F04
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EBFCC70)), // libopencv_imgproc.so 0x000E7F08
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EC80DE8)), // libopencv_imgproc.so 0x000E7F0C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3ED03FC8)), // libopencv_imgproc.so 0x000E7F10
        BitConverter.Int32BitsToSingle(unchecked((int)0x3ED8616D)), // libopencv_imgproc.so 0x000E7F14
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EE0722D)), // libopencv_imgproc.so 0x000E7F18
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EE87171)), // libopencv_imgproc.so 0x000E7F1C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EF05E95)), // libopencv_imgproc.so 0x000E7F20
        BitConverter.Int32BitsToSingle(unchecked((int)0x3EF838F7)), // libopencv_imgproc.so 0x000E7F24
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F000000)), // libopencv_imgproc.so 0x000E7F28
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F03D989)), // libopencv_imgproc.so 0x000E7F2C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F07A8CB)), // libopencv_imgproc.so 0x000E7F30
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F0B6D76)), // libopencv_imgproc.so 0x000E7F34
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F0F2744)), // libopencv_imgproc.so 0x000E7F38
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F12D5E7)), // libopencv_imgproc.so 0x000E7F3C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F167919)), // libopencv_imgproc.so 0x000E7F40
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F1A108C)), // libopencv_imgproc.so 0x000E7F44
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F1D9BFE)), // libopencv_imgproc.so 0x000E7F48
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F211B24)), // libopencv_imgproc.so 0x000E7F4C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F248DBA)), // libopencv_imgproc.so 0x000E7F50
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F27F37C)), // libopencv_imgproc.so 0x000E7F54
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F2B4C25)), // libopencv_imgproc.so 0x000E7F58
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F2E9772)), // libopencv_imgproc.so 0x000E7F5C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F31D522)), // libopencv_imgproc.so 0x000E7F60
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F3504F4)), // libopencv_imgproc.so 0x000E7F64
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F3826A7)), // libopencv_imgproc.so 0x000E7F68
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F3B39FF)), // libopencv_imgproc.so 0x000E7F6C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F3E3EBD)), // libopencv_imgproc.so 0x000E7F70
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F4134A6)), // libopencv_imgproc.so 0x000E7F74
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F441B7C)), // libopencv_imgproc.so 0x000E7F78
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F46F30A)), // libopencv_imgproc.so 0x000E7F7C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F49BB13)), // libopencv_imgproc.so 0x000E7F80
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F4C7360)), // libopencv_imgproc.so 0x000E7F84
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F4F1BBD)), // libopencv_imgproc.so 0x000E7F88
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F51B3F2)), // libopencv_imgproc.so 0x000E7F8C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F543BCF)), // libopencv_imgproc.so 0x000E7F90
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F56B31E)), // libopencv_imgproc.so 0x000E7F94
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F5919AE)), // libopencv_imgproc.so 0x000E7F98
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F5B6F51)), // libopencv_imgproc.so 0x000E7F9C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F5DB3D7)), // libopencv_imgproc.so 0x000E7FA0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F5FE714)), // libopencv_imgproc.so 0x000E7FA4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6208DB)), // libopencv_imgproc.so 0x000E7FA8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F641901)), // libopencv_imgproc.so 0x000E7FAC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F66175D)), // libopencv_imgproc.so 0x000E7FB0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6803CA)), // libopencv_imgproc.so 0x000E7FB4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F69DE1E)), // libopencv_imgproc.so 0x000E7FB8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6BA636)), // libopencv_imgproc.so 0x000E7FBC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6D5BED)), // libopencv_imgproc.so 0x000E7FC0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F6EFF20)), // libopencv_imgproc.so 0x000E7FC4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F708FB2)), // libopencv_imgproc.so 0x000E7FC8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F720D82)), // libopencv_imgproc.so 0x000E7FCC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F737870)), // libopencv_imgproc.so 0x000E7FD0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F74D064)), // libopencv_imgproc.so 0x000E7FD4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F76153F)), // libopencv_imgproc.so 0x000E7FD8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7746EA)), // libopencv_imgproc.so 0x000E7FDC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F78654D)), // libopencv_imgproc.so 0x000E7FE0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F797052)), // libopencv_imgproc.so 0x000E7FE4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7A67E2)), // libopencv_imgproc.so 0x000E7FE8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7B4BEC)), // libopencv_imgproc.so 0x000E7FEC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7C1C5D)), // libopencv_imgproc.so 0x000E7FF0
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7CD924)), // libopencv_imgproc.so 0x000E7FF4
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7D8236)), // libopencv_imgproc.so 0x000E7FF8
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7E1782)), // libopencv_imgproc.so 0x000E7FFC
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7E98FD)), // libopencv_imgproc.so 0x000E8000
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7F069E)), // libopencv_imgproc.so 0x000E8004
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7F605C)), // libopencv_imgproc.so 0x000E8008
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7FA62F)), // libopencv_imgproc.so 0x000E800C
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7FD813)), // libopencv_imgproc.so 0x000E8010
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F7FF605)), // libopencv_imgproc.so 0x000E8014
        BitConverter.Int32BitsToSingle(unchecked((int)0x3F800000)), // libopencv_imgproc.so 0x000E8018
    };

    /// <summary>
    /// <c>cv::ellipse2Poly(center, axes, angle, arc_start, arc_end, delta, pts)</c> (gap3 B1..B3): angle into 0..360;
    /// start/end swapped if reversed, shifted by ±360 together, and 0..360 when longer than 360; alpha = cos and beta = sin
    /// from the table; for i from arc_start while i &lt; arc_end + delta, step delta: a = min(i, arc_end) (+360 if &lt; 0),
    /// x = a_w·Sin[450−a], y = a_h·Sin[a] in double, pt = (cvRound(cx + x·α − y·β), cvRound(cy + x·β + y·α)), pushed only
    /// when it differs from the previous point; a single point becomes two copies of the centre.
    /// </summary>
    public static List<(int X, int Y)> Ellipse2Poly(int cxi, int cyi, int axisW, int axisH, int angle,
                                                    int arcStart, int arcEnd, int delta)
    {
        var pts = new List<(int X, int Y)>();
        double sizeA = axisW, sizeB = axisH;
        double cx = cxi, cy = cyi;
        int prevX = int.MinValue, prevY = int.MinValue;
        int i;

        while (angle < 0) angle += 360;
        while (angle > 360) angle -= 360;

        if (arcStart > arcEnd)
        {
            i = arcStart;
            arcStart = arcEnd;
            arcEnd = i;
        }
        while (arcStart < 0)
        {
            arcStart += 360;
            arcEnd += 360;
        }
        while (arcEnd > 360)
        {
            arcEnd -= 360;
            arcStart -= 360;
        }
        if (arcEnd - arcStart > 360)
        {
            arcStart = 0;
            arcEnd = 360;
        }

        // sincos(angle, alpha, beta)
        int sa = angle + (angle < 0 ? 360 : 0);
        float beta = SinTable[sa];
        float alpha = SinTable[450 - sa];

        for (i = arcStart; i < arcEnd + delta; i += delta)
        {
            double x, y;
            int a = i;
            if (a > arcEnd) a = arcEnd;
            if (a < 0) a += 360;

            x = sizeA * SinTable[450 - a];
            y = sizeB * SinTable[a];
            // non-fused, in C order (gap3 B3)
            int px = CvRound(cx + x * alpha - y * beta);
            int py = CvRound(cy + x * beta + y * alpha);
            if (px != prevX || py != prevY)
            {
                pts.Add((px, py));
                prevX = px; prevY = py;
            }
        }

        if (pts.Count == 1)
        {
            pts.Clear();
            pts.Add((cxi, cyi));
            pts.Add((cxi, cyi));
        }
        return pts;
    }

    // ------------------------------------------------------------------ warpAffine INTER_NEAREST (C1..C6)

    /// <summary>
    /// <c>cv::warpAffine(img, img, M, Size(cols, rows), INTER_NEAREST, BORDER_CONSTANT, 0)</c> on a CV_8UC1 image (gap3
    /// C1..C6). The engine builds M as <c>Anki::SmallMatrix&lt;2,3,float&gt;</c> and passes it as <c>CV_32FC1</c> (R-ANIM
    /// 10a, 10c); the float-to-double conversion happens inside the shipped <c>cv::warpAffine</c>'s
    /// <c>Mat::convertTo</c> (10e), so it is done here on entry, not by the caller. In place, so the source is cloned
    /// (C1); the widened M is inverted (C2); AB_BITS = 10, adelta[x] = cvRound(M0·x·1024), bdelta[x] = cvRound(M3·x·1024)
    /// (C3); per row X0 = cvRound((M1·y + M2)·1024) + 512 and Y0 with M4/M5; X = (X0 + adelta[x]) &gt;&gt; 10, Y likewise,
    /// each saturated to int16 (C4); remapNearest: inside the source the pixel is copied, outside it is the border value 0
    /// (C5, C6). Returns the destination.
    /// </summary>
    public static byte[] WarpAffineNearest(byte[] src, int rows, int cols, float[] m0)
    {
        var s = (byte[])src.Clone();                // dst.data == src.data → src.clone() (C1)
        var dst = new byte[rows * cols];
        // 10e: the shipped warpAffine's Mat::convertTo(CV_64F) widens the CV_32FC1 matrix here.
        var m = new double[m0.Length];
        for (int i = 0; i < m0.Length; i++) m[i] = m0[i];

        // !(flags & WARP_INVERSE_MAP): invert (C2)
        {
            double d = m[0] * m[4] - m[1] * m[3];
            d = d != 0 ? 1.0 / d : 0;
            double a11 = m[4] * d, a22 = m[0] * d;
            m[0] = a11; m[1] *= -d;
            m[3] *= -d; m[4] = a22;
            double b1 = -m[0] * m[2] - m[1] * m[5];
            double b2 = -m[3] * m[2] - m[4] * m[5];
            m[2] = b1; m[5] = b2;
        }

        const int AbBits = 10;
        const int AbScale = 1 << AbBits;
        const int RoundDelta = AbScale / 2;          // INTER_NEAREST (C4)
        var adelta = new int[cols];
        var bdelta = new int[cols];
        for (int x = 0; x < cols; x++)
        {
            adelta[x] = CvRound(m[0] * x * AbScale);
            bdelta[x] = CvRound(m[3] * x * AbScale);
        }

        for (int y = 0; y < rows; y++)
        {
            int x0 = CvRound((m[1] * y + m[2]) * AbScale) + RoundDelta;
            int y0 = CvRound((m[4] * y + m[5]) * AbScale) + RoundDelta;
            for (int x = 0; x < cols; x++)
            {
                int sx = SaturateShort((x0 + adelta[x]) >> AbBits);
                int sy = SaturateShort((y0 + bdelta[x]) >> AbBits);
                dst[y * cols + x] = (uint)sx < (uint)cols && (uint)sy < (uint)rows ? s[sy * cols + sx] : (byte)0;
            }
        }
        return dst;
    }

    private static int SaturateShort(int v) => v < short.MinValue ? short.MinValue : v > short.MaxValue ? short.MaxValue : v;

    // ================================================================== M11-020: boxFilter / subtract / normalize
    // fidelity: M11-020
    //
    // Transcribed from the shipped OpenCV 3.1.0 (`.scratch/B-M11/opencv-report.md`):
    //  - cv::boxFilter 8U->16S: RowSum<uchar,int> 0x000A2EE8 (exact int32 sliding sum), ColumnSum<int,short>
    //    operator() 0x000AAB44 (`saturate_cast<short>(s0 * (1/(w*h)))`, NEON `cv_vrndq_s32_f32` at 0x000AACD8
    //    / scalar `vcvtr.s32.f64` at 0x000AAD70), borderInterpolate 0x00044214.
    //  - cv::subtract 8U-16S->16S: getSubTab[4] = cv::hal::sub32s 0x0002228F then 32S->16S
    //    saturate_cast<short> (0x0003B142 / helper 0x00038C9E).
    //  - cv::normalize NORM_MINMAX 16S->8U: 0x00040A68 (global minMaxIdx, smin->0/smax->255) then
    //    cvtScale<short,uchar> 0x0003F840 (NEON helper 0x00023B68 ties up / scalar 0x00038C60 ties even).

    /// <summary>BORDER_REFLECT_101, the borderType the engine passes to boxFilter (S1/S25).</summary>
    public const int BorderReflect101 = 4;

    /// <summary>C's <c>DBL_EPSILON</c>, the normalize range guard (S20; not .NET's <c>double.Epsilon</c>).</summary>
    private const double DblEpsilon = 2.2204460492503131e-16;

    /// <summary>
    /// <c>saturate_cast&lt;uchar&gt;(int)</c>: the clamp helpers 0x000A2580 / 0x00038C60, <c>[0,255]</c>.
    /// </summary>
    public static byte SaturateUchar(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);

    /// <summary>
    /// <c>cv::borderInterpolate(p, len, BORDER_REFLECT_101)</c> 0x00044214 (S25): in range unchanged; otherwise
    /// <c>p = -p - 1 + delta</c> for <c>p &lt; 0</c> and <c>p = len - 1 - (p - len) - delta</c> for
    /// <c>p &gt;= len</c>, with <c>delta = 1</c> for REFLECT_101. <c>len == 1</c> gives 0. The engine does not set
    /// BORDER_ISOLATED, so the FilterEngine calls this with the parent image's width/height (S7/S25).
    /// </summary>
    public static int BorderInterpolate(int p, int len, int borderType)
    {
        if ((uint)p < (uint)len) return p;
        if (borderType == BorderReflect101)
        {
            if (len == 1) return 0;
            do
            {
                if (p < 0) p = -p - 1 + 1;
                else p = len - 1 - (p - len) - 1;
            } while ((uint)p >= (uint)len);
        }
        return p;
    }

    /// <summary>
    /// <c>cv_vrndq_s32_f32(a)</c> 0x0007A284 (S9): <c>(int32)(a + copysign(0.5f, a))</c>, i.e. round to
    /// nearest with ties away from zero, then truncate. This is the NEON box-filter column rounding, distinct
    /// from the ties-to-even <see cref="CvRound"/>.
    /// </summary>
    public static int RoundHalfAwayFromZero(float v)
    {
        float r = v + MathF.CopySign(0.5f, v);
        if (r >= int.MaxValue) return int.MaxValue;
        if (r <= int.MinValue) return int.MinValue;
        return (int)r;                                  // vcvt.s32.f32 truncates toward zero
    }

    /// <summary>
    /// <c>RowSum&lt;uchar,int&gt;::operator()</c> 0x000A2EE8 (S5): the exact int32 sum of the
    /// <paramref name="ksizeW"/> source pixels around output column <c>x</c>, with the REFLECT_101 border
    /// mapped against the parent width <paramref name="wholeCols"/>. No rounding or saturation.
    /// </summary>
    public static int[] RowSumU8To32S(byte[] whole, int wholeCols, int row, int roiX, int width, int ksizeW,
                                      int anchorX, int borderType)
    {
        var d = new int[width];
        for (int x = 0; x < width; x++)
        {
            int s = 0;
            for (int dx = 0; dx < ksizeW; dx++)
            {
                int sx = BorderInterpolate(roiX + x - anchorX + dx, wholeCols, borderType);
                s += whole[row * wholeCols + sx];
            }
            d[x] = s;
        }
        return d;
    }

    /// <summary>
    /// <c>ColumnSum&lt;int,short&gt;::operator()</c> 0x000AAB44 (S8/S10): sum the <c>ksizeH</c> int32 row sums
    /// per column, scale by <paramref name="scale"/> and saturate to short. The first
    /// <c>width &amp; ~7</c> columns take the NEON path (<c>float</c> multiply, <see cref="RoundHalfAwayFromZero"/>,
    /// ties away); the remainder takes the scalar path (<c>double</c> multiply, <see cref="CvRound"/>, ties even).
    /// </summary>
    public static void ColumnSum32STo16S(IReadOnlyList<int[]> rows, double scale, short[] dst, int offset, int width)
    {
        int neonEnd = width & ~7;
        for (int x = 0; x < width; x++)
        {
            int s = 0;
            for (int k = 0; k < rows.Count; k++) s += rows[k][x];
            int r;
            if (x < neonEnd)
            {
                float v = (float)s * (float)scale;
                r = RoundHalfAwayFromZero(v);
            }
            else
            {
                r = CvRound((double)s * scale);
            }
            dst[offset + x] = (short)(r < short.MinValue ? short.MinValue : r > short.MaxValue ? short.MaxValue : r);
        }
    }

    /// <summary>
    /// <c>cv::boxFilter(src, dst, CV_16S, ksize, anchor(-1,-1), normalize=true, BORDER_REFLECT_101)</c> on a
    /// CV_8U region view of a CV_8U parent image (S1..S10): the anchor is the centre, the scale is
    /// <c>1/(ksizeW*ksizeH)</c>, the border maps against the parent size, and the rounding is the
    /// position-dependent <see cref="ColumnSum32STo16S"/>. <paramref name="whole"/> is the parent image,
    /// row-major, step <paramref name="wholeCols"/>; the region is <c>(roiX, roiY, roiW, roiH)</c>.
    /// </summary>
    public static short[] BoxFilter8UTo16S(byte[] whole, int wholeRows, int wholeCols,
                                           int roiX, int roiY, int roiW, int roiH, int ksizeW, int ksizeH, int borderType)
    {
        if (borderType != BorderReflect101)
            throw new NotSupportedException($"boxFilter borderType {borderType}; only BORDER_REFLECT_101 (4) is transcribed (M11-020)");
        int anchorX = ksizeW / 2, anchorY = ksizeH / 2;
        double scale = 1.0 / ((double)ksizeW * ksizeH);
        var dst = new short[roiW * roiH];
        var rowSums = new int[ksizeH][];
        for (int y = 0; y < roiH; y++)
        {
            for (int dy = 0; dy < ksizeH; dy++)
            {
                int sy = BorderInterpolate(roiY + y - anchorY + dy, wholeRows, borderType);
                rowSums[dy] = RowSumU8To32S(whole, wholeCols, sy, roiX, roiW, ksizeW, anchorX, borderType);
            }
            ColumnSum32STo16S(rowSums, scale, dst, y * roiW, roiW);
        }
        return dst;
    }

    /// <summary>
    /// <c>cv::subtract(src1=CV_8U, src2=CV_16S, dst=CV_16S)</c> in place (S11..S18): working type CV_32S,
    /// <c>cv::hal::sub32s</c> 0x0002228F, then 32S->16S <c>saturate_cast&lt;short&gt;</c>; result
    /// <c>saturate_cast&lt;short&gt;((int)src1[i] - (int)src2[i])</c>.
    /// </summary>
    public static void Subtract8U16STo16S(byte[] src1, short[] src2, short[] dst, int length)
    {
        for (int i = 0; i < length; i++)
        {
            int d = src1[i] - src2[i];
            dst[i] = (short)(d < short.MinValue ? short.MinValue : d > short.MaxValue ? short.MaxValue : d);
        }
    }

    /// <summary>
    /// <c>cv::normalize(src=CV_16S, dst=CV_8U, alpha=255, beta=0, NORM_MINMAX)</c> (S19..S24): the global
    /// min/max over the flat array, <c>dmin=min(a,b)</c>, <c>dmax=max(a,b)</c>, <c>scale=(dmax-dmin)/(smax-smin)</c>
    /// and <c>shift=dmin-smin*scale</c> (smin-&gt;0, smax-&gt;255) when the range exceeds DBL_EPSILON; then
    /// <c>cvtScale&lt;short,uchar&gt;</c> with the first <c>length &amp; ~7</c> elements on the NEON path
    /// (<c>+0.5</c> then truncate, ties up) and the tail on the scalar path (<c>vcvtr</c>, ties even), both
    /// saturated to <c>[0,255]</c>.
    /// </summary>
    public static void NormalizeMinMax16STo8U(short[] src, byte[] dst, int length, double alpha, double beta)
    {
        int smin = int.MaxValue, smax = int.MinValue;
        for (int i = 0; i < length; i++) { int v = src[i]; if (v < smin) smin = v; if (v > smax) smax = v; }
        double dmin = Math.Min(alpha, beta), dmax = Math.Max(alpha, beta);
        double scale = 1.0, shift = 0.0;
        if (smax - smin > DblEpsilon)
        {
            scale = (dmax - dmin) / (smax - smin);
            shift = dmin - smin * scale;
        }

        int neonEnd = length & ~7;
        for (int i = 0; i < length; i++)
        {
            if (i < neonEnd)
            {
                float v = (float)src[i] * (float)scale + (float)shift;
                float t = v + 0.5f;
                dst[i] = t <= 0f ? (byte)0 : t >= 256f ? (byte)255 : (byte)(int)t;
            }
            else
            {
                float v = (float)shift + (float)src[i] * (float)scale;   // vmla: shift + src*scale
                float r = MathF.Round(v, MidpointRounding.ToEven);       // vcvtr.s32.f32, ties even
                dst[i] = SaturateUchar(r <= 0f ? 0 : r >= 255f ? 255 : (int)r);
            }
        }
    }

    // ================================================================== M11-021: CLAHE (CV_8U)
    // fidelity: M11-021
    //
    // Transcribed from the shipped OpenCV 3.1.0 (`.scratch/B-M11/pass2-report.md` Q1):
    //  - CLAHE_Impl::apply 0x20DAC, the CV_8UC1 branch (divisible: tileW = cols/tilesX,
    //    tileH = rows/tilesY, tileSizeTotal = tileW*tileH, lutScale = 255/tileSizeTotal,
    //    clipLimit = max((int)(clipLimit_*tileSizeTotal/256),1)).
    //  - CLAHE_CalcLut_Body<uchar,256,0>::operator() 0x20834 (clip 0x2094E..0x209B8, LUT 0x209BA..0x209F2).
    //  - CLAHE_Interpolation_Body<uchar>::operator() 0x203FE (per-column tables 0x2129E..0x21370,
    //    interpolation 0x204B8..0x20542).
    // Both the LUT and the interpolation round through vcvtr.s32.f32 (ties to even) then saturate to uchar.
    // The shipped camera is 320x240, so the divisible branch is the live one; the non-divisible
    // copyMakeBorder branch (0x20EC0..0x20F58) is not transcribed and throws.

    /// <summary>
    /// <c>CLAHE_Impl::apply</c> on a CV_8UC1 image, the divisible branch. <paramref name="tilesX"/> and
    /// <paramref name="tilesY"/> are the grid (the engine sets 4x4), <paramref name="clipLimit"/> the
    /// configured clip limit (the engine sets 32.0). The shipped 320x240 gives tileW 80, tileH 60,
    /// tileSizeTotal 4800, lutScale 255/4800 and clipLimit 600.
    /// </summary>
    public static byte[] Clahe8U(byte[] src, int rows, int cols, int tilesX, int tilesY, double clipLimit)
    {
        if (cols % tilesX != 0 || rows % tilesY != 0)
            throw new NotSupportedException(
                "CLAHE_Impl::apply non-divisible branch (copyMakeBorder 0x20EC0..0x20F58) is not transcribed (M11-021)");
        int tileW = cols / tilesX, tileH = rows / tilesY;
        int tileSizeTotal = tileW * tileH;
        float lutScale = (256 - 1) / (float)tileSizeTotal;
        int clip = clipLimit > 0 ? Math.Max((int)(clipLimit * tileSizeTotal / 256), 1) : 0;

        int tiles = tilesX * tilesY;
        var lut = new byte[tiles][];
        var hist = new int[256];
        for (int k = 0; k < tiles; k++)
        {
            Array.Clear(hist);
            int ty = k / tilesX, tx = k % tilesX;
            int x0 = tileW * tx, y0 = tileH * ty;
            for (int y = 0; y < tileH; y++)
                for (int x = 0; x < tileW; x++)
                    hist[src[(y0 + y) * cols + (x0 + x)]]++;
            lut[k] = ClaheCalcLut(hist, clip, lutScale);
        }

        // per-column tables (0x2129E..0x21370): xa/xa1 are the un-clamped fractional weights; c1/c2 are the
        // clamped tile columns.
        float invTw = 1.0f / tileW;
        var xa = new float[cols]; var xa1 = new float[cols];
        var c1 = new int[cols]; var c2 = new int[cols];
        for (int x = 0; x < cols; x++)
        {
            float txf = x * invTw - 0.5f;
            int tx1 = CvFloor(txf);
            int tx2 = tx1 + 1;
            xa[x] = txf - tx1;
            xa1[x] = 1 - xa[x];
            c1[x] = Math.Max(tx1, 0);
            c2[x] = Math.Min(tx2, tilesX - 1);
        }

        // per-row interpolation (0x204B8..0x20542)
        var dst = new byte[rows * cols];
        float invTh = 1.0f / tileH;
        for (int y = 0; y < rows; y++)
        {
            float tyf = y * invTh - 0.5f;
            int ty1 = CvFloor(tyf);
            int ty2 = ty1 + 1;
            float ya = tyf - ty1;
            float ya1 = 1 - ya;
            ty1 = Math.Max(ty1, 0);
            ty2 = Math.Min(ty2, tilesY - 1);
            int rowBase1 = ty1 * tilesX, rowBase2 = ty2 * tilesX;
            for (int x = 0; x < cols; x++)
            {
                int v = src[y * cols + x];
                float a = lut[rowBase1 + c1[x]][v] * xa1[x] + lut[rowBase1 + c2[x]][v] * xa[x];
                float b = lut[rowBase2 + c1[x]][v] * xa1[x] + lut[rowBase2 + c2[x]][v] * xa[x];
                dst[y * cols + x] = SaturateUchar(CvRound(a * ya1 + b * ya));
            }
        }
        return dst;
    }

    /// <summary>
    /// The clip/redistribute/LUT body of <c>CLAHE_CalcLut_Body&lt;uchar,256,0&gt;::operator()</c>
    /// 0x2094E..0x209F2. <paramref name="clipLimit"/> is the already-computed
    /// <c>max((int)(clipLimit_*tileSizeTotal/256),1)</c> (0 disables clipping); <paramref name="lutScale"/>
    /// is <c>255/tileSizeTotal</c>. The histogram is modified in place.
    /// </summary>
    public static byte[] ClaheCalcLut(int[] hist, int clipLimit, float lutScale)
    {
        if (clipLimit > 0)
        {
            int clipped = 0;
            for (int i = 0; i < 256; i++)
                if (hist[i] > clipLimit) { clipped += hist[i] - clipLimit; hist[i] = clipLimit; }
            int redistBatch = clipped / 256;                 // signed, truncate toward zero (0x20980..0x2098C)
            int residual = clipped - redistBatch * 256;
            for (int i = 0; i < 256; i++) hist[i] += redistBatch;
            for (int i = 0; i < residual; i++) hist[i]++;
        }
        var lut = new byte[256];
        int sum = 0;
        for (int i = 0; i < 256; i++)
        {
            sum += hist[i];
            lut[i] = SaturateUchar(CvRound((float)sum * lutScale));   // vcvtr.s32.f32, ties even, then saturate
        }
        return lut;
    }

    /// <summary>OpenCV's <c>cvFloor(float)</c>: floor (toward negative infinity).</summary>
    public static int CvFloor(float v) => (int)MathF.Floor(v);

    /// <summary>
    /// <c>cv::boxFilter(src, dst, ddepth=-1, ksize=(ksizeW,ksizeH), anchor=(-1,-1), normalize=true,
    /// borderType=4)</c> on a CV_8UC1 image: row sums in int32 (<see cref="RowSumU8To32S"/>), column sums
    /// scaled by <c>1/(ksizeW*ksizeH)</c> and saturated to uchar. The rounding follows the shipped
    /// <c>ColumnSum&lt;int,short&gt;</c> split (NEON ties away for the first <c>cols &amp; ~7</c>, scalar
    /// ties even for the tail); the shipped <c>ColumnSum&lt;int,uchar&gt;</c> body was not re-read.
    /// </summary>
    public static byte[] BoxFilter8UTo8U(byte[] src, int rows, int cols, int ksizeW, int ksizeH, int borderType)
    {
        if (borderType != BorderReflect101)
            throw new NotSupportedException($"boxFilter borderType {borderType}; only BORDER_REFLECT_101 (4) is transcribed (M11-021)");
        int anchorX = ksizeW / 2, anchorY = ksizeH / 2;
        double scale = 1.0 / ((double)ksizeW * ksizeH);
        var dst = new byte[rows * cols];
        var rowSums = new int[ksizeH][];
        int neonEnd = cols & ~7;
        for (int y = 0; y < rows; y++)
        {
            for (int dy = 0; dy < ksizeH; dy++)
            {
                int sy = BorderInterpolate(y - anchorY + dy, rows, borderType);
                rowSums[dy] = RowSumU8To32S(src, cols, sy, 0, cols, ksizeW, anchorX, borderType);
            }
            for (int x = 0; x < cols; x++)
            {
                int s = 0;
                for (int k = 0; k < ksizeH; k++) s += rowSums[k][x];
                int r = x < neonEnd ? RoundHalfAwayFromZero((float)s * (float)scale) : CvRound((double)s * scale);
                dst[y * cols + x] = SaturateUchar(r);
            }
        }
        return dst;
    }
}
