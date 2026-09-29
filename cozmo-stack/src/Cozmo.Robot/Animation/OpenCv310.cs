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
        0.0000000f, 0.0174524f, 0.0348995f, 0.0523360f, 0.0697565f, 0.0871557f,
        0.1045285f, 0.1218693f, 0.1391731f, 0.1564345f, 0.1736482f, 0.1908090f,
        0.2079117f, 0.2249511f, 0.2419219f, 0.2588190f, 0.2756374f, 0.2923717f,
        0.3090170f, 0.3255682f, 0.3420201f, 0.3583679f, 0.3746066f, 0.3907311f,
        0.4067366f, 0.4226183f, 0.4383711f, 0.4539905f, 0.4694716f, 0.4848096f,
        0.5000000f, 0.5150381f, 0.5299193f, 0.5446390f, 0.5591929f, 0.5735764f,
        0.5877853f, 0.6018150f, 0.6156615f, 0.6293204f, 0.6427876f, 0.6560590f,
        0.6691306f, 0.6819984f, 0.6946584f, 0.7071068f, 0.7193398f, 0.7313537f,
        0.7431448f, 0.7547096f, 0.7660444f, 0.7771460f, 0.7880108f, 0.7986355f,
        0.8090170f, 0.8191520f, 0.8290376f, 0.8386706f, 0.8480481f, 0.8571673f,
        0.8660254f, 0.8746197f, 0.8829476f, 0.8910065f, 0.8987940f, 0.9063078f,
        0.9135455f, 0.9205049f, 0.9271839f, 0.9335804f, 0.9396926f, 0.9455186f,
        0.9510565f, 0.9563048f, 0.9612617f, 0.9659258f, 0.9702957f, 0.9743701f,
        0.9781476f, 0.9816272f, 0.9848078f, 0.9876883f, 0.9902681f, 0.9925462f,
        0.9945219f, 0.9961947f, 0.9975641f, 0.9986295f, 0.9993908f, 0.9998477f,
        1.0000000f, 0.9998477f, 0.9993908f, 0.9986295f, 0.9975641f, 0.9961947f,
        0.9945219f, 0.9925462f, 0.9902681f, 0.9876883f, 0.9848078f, 0.9816272f,
        0.9781476f, 0.9743701f, 0.9702957f, 0.9659258f, 0.9612617f, 0.9563048f,
        0.9510565f, 0.9455186f, 0.9396926f, 0.9335804f, 0.9271839f, 0.9205049f,
        0.9135455f, 0.9063078f, 0.8987940f, 0.8910065f, 0.8829476f, 0.8746197f,
        0.8660254f, 0.8571673f, 0.8480481f, 0.8386706f, 0.8290376f, 0.8191520f,
        0.8090170f, 0.7986355f, 0.7880108f, 0.7771460f, 0.7660444f, 0.7547096f,
        0.7431448f, 0.7313537f, 0.7193398f, 0.7071068f, 0.6946584f, 0.6819984f,
        0.6691306f, 0.6560590f, 0.6427876f, 0.6293204f, 0.6156615f, 0.6018150f,
        0.5877853f, 0.5735764f, 0.5591929f, 0.5446390f, 0.5299193f, 0.5150381f,
        0.5000000f, 0.4848096f, 0.4694716f, 0.4539905f, 0.4383711f, 0.4226183f,
        0.4067366f, 0.3907311f, 0.3746066f, 0.3583679f, 0.3420201f, 0.3255682f,
        0.3090170f, 0.2923717f, 0.2756374f, 0.2588190f, 0.2419219f, 0.2249511f,
        0.2079117f, 0.1908090f, 0.1736482f, 0.1564345f, 0.1391731f, 0.1218693f,
        0.1045285f, 0.0871557f, 0.0697565f, 0.0523360f, 0.0348995f, 0.0174524f,
        0.0000000f, -0.0174524f, -0.0348995f, -0.0523360f, -0.0697565f, -0.0871557f,
        -0.1045285f, -0.1218693f, -0.1391731f, -0.1564345f, -0.1736482f, -0.1908090f,
        -0.2079117f, -0.2249511f, -0.2419219f, -0.2588190f, -0.2756374f, -0.2923717f,
        -0.3090170f, -0.3255682f, -0.3420201f, -0.3583679f, -0.3746066f, -0.3907311f,
        -0.4067366f, -0.4226183f, -0.4383711f, -0.4539905f, -0.4694716f, -0.4848096f,
        -0.5000000f, -0.5150381f, -0.5299193f, -0.5446390f, -0.5591929f, -0.5735764f,
        -0.5877853f, -0.6018150f, -0.6156615f, -0.6293204f, -0.6427876f, -0.6560590f,
        -0.6691306f, -0.6819984f, -0.6946584f, -0.7071068f, -0.7193398f, -0.7313537f,
        -0.7431448f, -0.7547096f, -0.7660444f, -0.7771460f, -0.7880108f, -0.7986355f,
        -0.8090170f, -0.8191520f, -0.8290376f, -0.8386706f, -0.8480481f, -0.8571673f,
        -0.8660254f, -0.8746197f, -0.8829476f, -0.8910065f, -0.8987940f, -0.9063078f,
        -0.9135455f, -0.9205049f, -0.9271839f, -0.9335804f, -0.9396926f, -0.9455186f,
        -0.9510565f, -0.9563048f, -0.9612617f, -0.9659258f, -0.9702957f, -0.9743701f,
        -0.9781476f, -0.9816272f, -0.9848078f, -0.9876883f, -0.9902681f, -0.9925462f,
        -0.9945219f, -0.9961947f, -0.9975641f, -0.9986295f, -0.9993908f, -0.9998477f,
        -1.0000000f, -0.9998477f, -0.9993908f, -0.9986295f, -0.9975641f, -0.9961947f,
        -0.9945219f, -0.9925462f, -0.9902681f, -0.9876883f, -0.9848078f, -0.9816272f,
        -0.9781476f, -0.9743701f, -0.9702957f, -0.9659258f, -0.9612617f, -0.9563048f,
        -0.9510565f, -0.9455186f, -0.9396926f, -0.9335804f, -0.9271839f, -0.9205049f,
        -0.9135455f, -0.9063078f, -0.8987940f, -0.8910065f, -0.8829476f, -0.8746197f,
        -0.8660254f, -0.8571673f, -0.8480481f, -0.8386706f, -0.8290376f, -0.8191520f,
        -0.8090170f, -0.7986355f, -0.7880108f, -0.7771460f, -0.7660444f, -0.7547096f,
        -0.7431448f, -0.7313537f, -0.7193398f, -0.7071068f, -0.6946584f, -0.6819984f,
        -0.6691306f, -0.6560590f, -0.6427876f, -0.6293204f, -0.6156615f, -0.6018150f,
        -0.5877853f, -0.5735764f, -0.5591929f, -0.5446390f, -0.5299193f, -0.5150381f,
        -0.5000000f, -0.4848096f, -0.4694716f, -0.4539905f, -0.4383711f, -0.4226183f,
        -0.4067366f, -0.3907311f, -0.3746066f, -0.3583679f, -0.3420201f, -0.3255682f,
        -0.3090170f, -0.2923717f, -0.2756374f, -0.2588190f, -0.2419219f, -0.2249511f,
        -0.2079117f, -0.1908090f, -0.1736482f, -0.1564345f, -0.1391731f, -0.1218693f,
        -0.1045285f, -0.0871557f, -0.0697565f, -0.0523360f, -0.0348995f, -0.0174524f,
        -0.0000000f, 0.0174524f, 0.0348995f, 0.0523360f, 0.0697565f, 0.0871557f,
        0.1045285f, 0.1218693f, 0.1391731f, 0.1564345f, 0.1736482f, 0.1908090f,
        0.2079117f, 0.2249511f, 0.2419219f, 0.2588190f, 0.2756374f, 0.2923717f,
        0.3090170f, 0.3255682f, 0.3420201f, 0.3583679f, 0.3746066f, 0.3907311f,
        0.4067366f, 0.4226183f, 0.4383711f, 0.4539905f, 0.4694716f, 0.4848096f,
        0.5000000f, 0.5150381f, 0.5299193f, 0.5446390f, 0.5591929f, 0.5735764f,
        0.5877853f, 0.6018150f, 0.6156615f, 0.6293204f, 0.6427876f, 0.6560590f,
        0.6691306f, 0.6819984f, 0.6946584f, 0.7071068f, 0.7193398f, 0.7313537f,
        0.7431448f, 0.7547096f, 0.7660444f, 0.7771460f, 0.7880108f, 0.7986355f,
        0.8090170f, 0.8191520f, 0.8290376f, 0.8386706f, 0.8480481f, 0.8571673f,
        0.8660254f, 0.8746197f, 0.8829476f, 0.8910065f, 0.8987940f, 0.9063078f,
        0.9135455f, 0.9205049f, 0.9271839f, 0.9335804f, 0.9396926f, 0.9455186f,
        0.9510565f, 0.9563048f, 0.9612617f, 0.9659258f, 0.9702957f, 0.9743701f,
        0.9781476f, 0.9816272f, 0.9848078f, 0.9876883f, 0.9902681f, 0.9925462f,
        0.9945219f, 0.9961947f, 0.9975641f, 0.9986295f, 0.9993908f, 0.9998477f,
        1.0000000f,
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
