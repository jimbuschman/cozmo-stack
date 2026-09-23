namespace Cozmo.Robot.Vision;

/// <summary>
/// The engine's <c>MarkerDetector::Parameters</c> (<c>Parameters::Initialize</c> 0x008752F8). Every value is
/// the immediate stored there (NATIVE); the names are those of Anki's fiducial detector, which the parameter
/// order and the pipeline's own profiler labels identify.
/// </summary>
public sealed record QuadDetectorParameters
{
    /// <summary>
    /// <c>scaleImage_numPyramidLevels</c>, the second argument
    /// <c>DetectFiducialMarkers</c> hands <c>ExtractComponentsViaCharacteristicScale_binomial</c>: the word
    /// at the parameters' +4, which <c>Initialize</c> sets to 1 (0x0087530E). One level, so the
    /// characteristic-scale search has a single scale to choose from and the "local mean" is simply the
    /// binomial-filtered image. The engine's loop is general - it downsamples by two per level and
    /// upsamples back with <c>UpsampleByPowerOfTwoBilinear&lt;1..5&gt;</c> - but this build runs it once.
    /// </summary>
    public int PyramidLevels { get; init; } = 1;
    /// <summary>Components with fewer pixels than this are dropped (100).</summary>
    public int MinComponentPixels { get; init; } = 100;
    /// <summary>Components with more pixels than this are dropped (39000).</summary>
    public int MaxComponentPixels { get; init; } = 39000;
    /// <summary>Upper bound on 1-D segments per image (32000).</summary>
    public int MaxSegments { get; init; } = 32000;
    /// <summary>
    /// LOCAL guard on quads per image. The engine's own bound is the capacity of the list it fills - the
    /// 10000-entry scratch list <c>ComputeQuadrilateralsFromConnectedComponents</c> allocates at
    /// 0x00892DA8 - not a parameter; the 512 that used to be recorded here is the quad symmetry
    /// threshold, <see cref="QuadSymmetryThresholdQ8"/>.
    /// </summary>
    public int MaxQuads { get; init; } = 512;
    /// <summary>Upper bound on extracted markers: 500 at the parameters' +0x44 (0x00875386).</summary>
    public int MaxMarkers { get; init; } = 500;

    /// <summary>
    /// <c>quads_minQuadArea</c>, 25 at the parameters' +0x30 (0x00875338), the second argument of
    /// <c>ComputeQuadrilateralsFromConnectedComponents</c> and the first test
    /// <c>IsQuadrilateralReasonable</c> 0x00892B18 makes: the cross product of the first three corners,
    /// taken as an absolute value, must reach it (0x00892B5C).
    /// </summary>
    public int MinQuadArea { get; init; } = 25;

    /// <summary>
    /// <c>quads_quadSymmetryThreshold</c> in 8.8 fixed point: 512 at the parameters' +0x34, so 2.0. The
    /// test at 0x00892CFC..0x00892D22 compares the two triangles either diagonal splits the quad into -
    /// <c>max &lt;&lt; 8</c> against <c>threshold * min</c> - and the quad passes when one of the two
    /// splits is within that factor.
    /// </summary>
    public int QuadSymmetryThresholdQ8 { get; init; } = 512;
    /// <summary>Solid/sparse test: a component's fill of its bounding box must lie in (0.03, 0.8).</summary>
    public double MinFillRatio { get; init; } = 0.03;
    public double MaxFillRatio { get; init; } = 0.8;
    /// <summary>Fraction of the quad the rounded corners may occupy (0.15).</summary>
    public double RoundedCornersFraction { get; init; } = 0.15;
    /// <summary>Two side-length fractions (0.1, 0.1): the shortest side relative to the longest, and to the image.</summary>
    public double MinSideLengthFraction { get; init; } = 0.1;
    /// <summary>Hollow test: the middle row must be at most this full (0.97) for the component to count as a ring.</summary>
    public double MaxHollowRowFill { get; init; } = 0.97;
    /// <summary>Corner refinement: 25 iterations, stop under 0.005 px change, reject over 5 px change (1.01 is the refinement step growth).</summary>
    public int RefinementIterations { get; init; } = 25;
    public double MinCornerChange { get; init; } = 0.005;
    public double MaxCornerChange { get; init; } = 5.0;
    /// <summary><c>Parameters::Initialize</c> +0x3C: 1.01 (0x3F8147AE), the contrast ratio <c>ComputeBrightDarkValues</c> requires.</summary>
    public double MinContrastRatio { get; init; } = 1.01;
    /// <summary>+0x4C: 100 samples, which <c>RefineQuadrilateral</c> spreads as 8 x ceil(100 / 8) over the eight edges.</summary>
    public int RefinementSamples { get; init; } = 100;
    /// <summary>+0x58/+0x5C: 0.15 (0x3E19999A), the corner padding the edge samples keep clear of, in the canonical square.</summary>
    public double RefinePaddingFraction { get; init; } = 0.15;
    /// <summary>+0x60/+0x64: 0.1 (0x3DCCCCCD), where the inner edge of the fiducial's border lies in the canonical square.</summary>
    public double RefineInnerFraction { get; init; } = 0.1;
    /// <summary>Quads closer than this to the image edge are dropped (2 px; NATIVE flag block <c>0x101, 1, 4, 2</c> — the 2).</summary>
    public int MinDistanceFromEdge { get; init; } = 2;
    /// <summary>
    /// <c>scaleImage_thresholdMultiplier</c> in Q16: 0xCCCC at the parameters' +0xC (0x00875314), which is
    /// 0.79998779, and the binarize loop uses it as integers - a pixel is dark when
    /// <c>(scale * 0xCCCC) &gt;&gt; 16 &gt; pixel</c> (0x00890BBE..0x00890BC4). This had been a local guess of 0.75.
    /// </summary>
    public int DarkThresholdQ16 { get; init; } = 0xCCCC;
}

/// <summary>A candidate quadrilateral from the front end, corners in the decoder's order TL, BL, TR, BR.</summary>
public sealed record DetectedQuad(Vec2[] Corners, int ComponentPixels)
{
    public double Area
    {
        get
        {
            // shoelace over the cyclic order TL, TR, BR, BL
            var c = new[] { Corners[0], Corners[2], Corners[3], Corners[1] };
            double a = 0;
            for (int i = 0; i < 4; i++) a += c[i].X * c[(i + 1) % 4].Y - c[(i + 1) % 4].X * c[i].Y;
            return Math.Abs(a) / 2;
        }
    }
}

/// <summary>
/// The quad-extraction front end of <c>Anki::Embedded::DetectFiducialMarkers</c>. The engine's pipeline is
/// <c>ExtractComponentsViaCharacteristicScale_binomial → InvalidateSmallOrLargeComponents →
/// InvalidateSolidOrSparseComponents → InvalidateFilledCenterComponents_hollowRows →
/// ComputeQuadrilateralsFromConnectedComponents → (refine) → ComputeHomographyFromQuad</c>; this class
/// follows that structure with the engine's parameters, but the pixel-level algorithms are LOCAL
/// re-implementations (the originals are Anki's embedded fixed-point code, not transcribed):
///
/// 1. the image is binomial-filtered and each pixel compared with its own filtered value: dark when
///    <c>(filtered * 0xCCCC) &gt;&gt; 16 &gt; pixel</c>. That is the engine's
///    <c>ExtractComponentsViaCharacteristicScale_binomial</c> 0x00890448 with the one pyramid level its
///    parameters ask for - the level loop filters, measures <c>|filtered - image|</c> and keeps the
///    filtered value of the level with the largest response ("ecvcsB_scale_select", 0x00890B14), then
///    binarizes ("ecvcsB_binarize", 0x00890BB6);
/// 2. 8-connected components of dark pixels, filtered by size, fill ratio and hollowness;
/// 3. the four corners come from <see cref="QuadCorners"/>, which is the engine's own chain:
///    <c>TraceNextExteriorBoundary</c> 0x008C6B18 then, with the corner method the parameters select
///    (+0x28 = 1, the switch at 0x00892E24), <c>ExtractLineFitsPeaks</c> 0x008A5DB8. The quad the engine
///    keeps is the clockwise corners re-ordered into its <c>Quadrilateral</c>, which is the decoder's
///    order, and <c>IsQuadrilateralReasonable</c> decides both whether it survives and whether that order
///    needs its middle pair swapped;
/// 4. the corners are not refined here: the engine refines them per marker once it has the marker's
///    homography (<c>VisionMarker::RefineCorners</c>), which <see cref="MarkerDetector"/> does through
///    <see cref="CornerRefinement"/>.
///    This last step is LOCAL: the engine refines later, inside <c>DetectFiducialMarkers</c>.
/// </summary>
public sealed class QuadDetector
{
    public QuadDetector(QuadDetectorParameters? parameters = null) => Parameters = parameters ?? new QuadDetectorParameters();

    public QuadDetectorParameters Parameters { get; }

    /// <summary>Diagnostics from the last run, for the conformance tool.</summary>
    public sealed class Stats
    {
        public int DarkPixels, Components, AfterSize, AfterFill, AfterHollow, Quads, AfterGeometry;
        public override string ToString() =>
            $"dark={DarkPixels} components={Components} size-ok={AfterSize} fill-ok={AfterFill} hollow-ok={AfterHollow} quads={Quads} geometry-ok={AfterGeometry}";
    }

    public Stats LastStats { get; private set; } = new();

    /// <summary>The binary "dark" mask of the last run (1 = dark), for debugging images.</summary>
    public GrayImage? LastMask { get; private set; }

    public IReadOnlyList<DetectedQuad> Detect(GrayImage img)
    {
        var st = new Stats();
        var mask = CharacteristicScaleMask(img);
        LastMask = mask;
        st.DarkPixels = mask.Pixels.Count(p => p != 0);

        var labels = new int[img.Width * img.Height];
        var comps = ConnectedComponents(mask, labels);
        st.Components = comps.Count;
        var quads = new List<DetectedQuad>();
        foreach (var c in comps)
        {
            if (c.Pixels < Parameters.MinComponentPixels || c.Pixels > Parameters.MaxComponentPixels) continue;
            st.AfterSize++;
            double fill = c.Pixels / (double)((c.MaxX - c.MinX + 1) * (c.MaxY - c.MinY + 1));
            if (fill <= Parameters.MinFillRatio || fill >= Parameters.MaxFillRatio) continue;
            st.AfterFill++;
            if (!IsHollow(c, labels, img.Width)) continue;
            st.AfterHollow++;
            var boundary = QuadCorners.ExteriorBoundary(labels, c.Label, img.Width, c.MinX, c.MinY, c.MaxX, c.MaxY);
            if (boundary is null || boundary.Count < 4) continue;
            var corners = QuadCorners.ExtractLineFitsPeaks(boundary, img.Height, img.Width);
            if (corners is null) continue;
            st.Quads++;
            // the engine tests the unrefined quad, in the order its Quadrilateral stores
            if (!IsQuadrilateralReasonable(ToDecoderOrder(corners), img.Width, img.Height, Parameters, out bool swapped)) continue;
            st.AfterGeometry++;
            // the corners are refined per marker, after the homography, by CornerRefinement (MarkerDetector)
            var quad = ToDecoderOrder(corners);
            if (swapped) (quad[1], quad[2]) = (quad[2], quad[1]);
            quads.Add(new DetectedQuad(quad, c.Pixels));
            if (quads.Count >= Parameters.MaxQuads) break;
        }
        LastStats = st;
        return quads;
    }

    // ------------------------------------------------------------------ characteristic scale

    /// <summary>
    /// The engine's characteristic-scale binarization, <c>ExtractComponentsViaCharacteristicScale_binomial</c>
    /// 0x00890448. Per level it downsamples by two, binomial-filters, takes <c>|filtered - image|</c> as the
    /// response (<c>Matrix::Elementwise::ApplyOperation&lt;SumOfAbsDiff&gt;</c> at 0x008907F4), upsamples both
    /// back to full size, and keeps the filtered value wherever the response beats the best so far
    /// (0x00890B14: <c>if (dog &gt; best) { best = dog; scale = filtered; }</c>, with both buffers starting at
    /// zero). Then a pixel is dark when <c>(scale * thresholdQ16) &gt;&gt; 16 &gt; pixel</c> (0x00890BBE).
    ///
    /// With <see cref="QuadDetectorParameters.PyramidLevels"/> at the shipped 1 there is one level and no
    /// downsampling, so the scale image is the binomial-filtered image everywhere the response is non-zero
    /// - a flat neighbourhood leaves the scale at zero and the pixel is never dark, which is the engine's
    /// behaviour and not a special case here.
    /// </summary>
    private GrayImage CharacteristicScaleMask(GrayImage img)
    {
        int w = img.Width, h = img.Height;
        var mask = new GrayImage(w, h);
        var scale = new byte[w * h];
        var best = new byte[w * h];

        for (int level = 0; level < Math.Max(1, Parameters.PyramidLevels); level++)
        {
            var atLevel = level == 0 ? img : DownsampleByTwo(img, level);
            var filtered = BinomialFilter(atLevel);
            int lw = atLevel.Width, lh = atLevel.Height;
            for (int y = 0; y < h; y++)
            {
                int sy = Math.Min(lh - 1, y >> level);
                for (int x = 0; x < w; x++)
                {
                    int sx = Math.Min(lw - 1, x >> level);
                    int f = filtered.Pixels[sy * lw + sx];
                    int response = Math.Abs(f - atLevel.Pixels[sy * lw + sx]);
                    int i = y * w + x;
                    if (response > best[i]) { best[i] = (byte)Math.Min(255, response); scale[i] = (byte)f; }
                }
            }
        }

        for (int i = 0; i < mask.Pixels.Length; i++)
            if ((scale[i] * Parameters.DarkThresholdQ16) >> 16 > img.Pixels[i]) mask.Pixels[i] = 1;
        return mask;
    }

    /// <summary>
    /// <c>ImageProcessing::BinomialFilter&lt;u8,u8,u8&gt;</c> 0x008A2344 (coretech
    /// <c>vision/robot/src/filtering.cpp</c>): the separable five-tap binomial [1 4 6 4 1] / 16, run along
    /// the rows and then down the columns, each pass truncating with <c>&gt;&gt; 4</c>
    /// (0x008A24A2..0x008A24A6). At the borders the off-image taps take the edge pixel's value, which is
    /// what the engine's first two columns do by folding their weights onto it: 11, 4, 1 for the first
    /// column (0x008A243C) and 5, 6, 4, 1 for the second (0x008A2458..0x008A246A).
    /// </summary>
    public static GrayImage BinomialFilter(GrayImage src)
    {
        int w = src.Width, h = src.Height;
        var tmp = new GrayImage(w, h);
        var dst = new GrayImage(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int p0 = src.Pixels[y * w + Math.Max(0, x - 2)], p1 = src.Pixels[y * w + Math.Max(0, x - 1)];
                int p2 = src.Pixels[y * w + x];
                int p3 = src.Pixels[y * w + Math.Min(w - 1, x + 1)], p4 = src.Pixels[y * w + Math.Min(w - 1, x + 2)];
                tmp.Pixels[y * w + x] = (byte)((p0 + 4 * p1 + 6 * p2 + 4 * p3 + p4) >> 4);
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int p0 = tmp.Pixels[Math.Max(0, y - 2) * w + x], p1 = tmp.Pixels[Math.Max(0, y - 1) * w + x];
                int p2 = tmp.Pixels[y * w + x];
                int p3 = tmp.Pixels[Math.Min(h - 1, y + 1) * w + x], p4 = tmp.Pixels[Math.Min(h - 1, y + 2) * w + x];
                dst.Pixels[y * w + x] = (byte)((p0 + 4 * p1 + 6 * p2 + 4 * p3 + p4) >> 4);
            }
        return dst;
    }

    /// <summary>
    /// <c>ImageProcessing::DownsampleByTwo</c>, applied <paramref name="times"/> times: each pass averages
    /// two-by-two blocks (the u16 accumulator in the template argument).
    /// </summary>
    public static GrayImage DownsampleByTwo(GrayImage src, int times)
    {
        var cur = src;
        for (int t = 0; t < times; t++)
        {
            int w = cur.Width / 2, h = cur.Height / 2;
            if (w < 1 || h < 1) break;
            var next = new GrayImage(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int a = cur.Pixels[(2 * y) * cur.Width + 2 * x], b = cur.Pixels[(2 * y) * cur.Width + 2 * x + 1];
                    int c = cur.Pixels[(2 * y + 1) * cur.Width + 2 * x], d = cur.Pixels[(2 * y + 1) * cur.Width + 2 * x + 1];
                    next.Pixels[y * w + x] = (byte)((a + b + c + d) >> 2);
                }
            cur = next;
        }
        return cur;
    }

    // ------------------------------------------------------------------ connected components

    private sealed class Component
    {
        public int Label, Pixels, MinX = int.MaxValue, MinY = int.MaxValue, MaxX = -1, MaxY = -1;
        public double SumX, SumY;
    }

    private List<Component> ConnectedComponents(GrayImage mask, int[] labels)
    {
        int w = mask.Width, h = mask.Height;
        var comps = new List<Component>();
        var stack = new Stack<int>();
        int next = 0;
        for (int i = 0; i < labels.Length; i++)
        {
            if (mask.Pixels[i] == 0 || labels[i] != 0) continue;
            var c = new Component { Label = ++next };
            labels[i] = c.Label;
            stack.Push(i);
            while (stack.Count > 0)
            {
                int p = stack.Pop();
                int x = p % w, y = p / w;
                c.Pixels++; c.SumX += x; c.SumY += y;
                if (x < c.MinX) c.MinX = x; if (x > c.MaxX) c.MaxX = x; if (y < c.MinY) c.MinY = y; if (y > c.MaxY) c.MaxY = y;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int q = ny * w + nx;
                        if (mask.Pixels[q] != 0 && labels[q] == 0) { labels[q] = c.Label; stack.Push(q); }
                    }
                if (c.Pixels > Parameters.MaxComponentPixels * 2) { stack.Clear(); }   // runaway background: stop flooding, it will be rejected
            }
            comps.Add(c);
            if (comps.Count >= Parameters.MaxSegments) break;
        }
        return comps;
    }

    /// <summary><c>InvalidateFilledCenterComponents_hollowRows</c>: the middle rows of a ring have a gap.</summary>
    private bool IsHollow(Component c, int[] labels, int w)
    {
        int midY = (c.MinY + c.MaxY) / 2, midX = (c.MinX + c.MaxX) / 2;
        if (labels[midY * w + midX] == c.Label) return false;
        int filled = 0, width = c.MaxX - c.MinX + 1;
        for (int x = c.MinX; x <= c.MaxX; x++) if (labels[midY * w + x] == c.Label) filled++;
        return filled < width * Parameters.MaxHollowRowFill;
    }

    // ------------------------------------------------------------------ refinement

    /// <summary>
    /// Sub-pixel refinement of the four sides: along each side, sample points (skipping the rounded-corner
    /// fraction), find the strongest dark-to-light transition within ±3 px of the side's normal, fit a line, and
    /// intersect the lines. Iterates until the corners move less than <see cref="QuadDetectorParameters.MinCornerChange"/>.
    /// </summary>
    private Vec2[]? Refine(GrayImage img, Vec2[] cyclic)
    {
        var corners = (Vec2[])cyclic.Clone();
        var original = (Vec2[])cyclic.Clone();
        for (int iter = 0; iter < Parameters.RefinementIterations; iter++)
        {
            var lines = new (Vec2 P, Vec2 D)?[4];
            for (int s = 0; s < 4; s++)
            {
                var p0 = corners[s]; var p1 = corners[(s + 1) % 4];
                var dir = p1 - p0; double len = dir.Length;
                if (len < 4) return null;
                dir = dir * (1 / len);
                // outward normal for clockwise-on-screen order is to the left of the direction
                var normal = new Vec2(dir.Y, -dir.X);
                var pts = new List<Vec2>();
                int samples = Math.Max(6, (int)(len / 2));
                for (int k = 0; k < samples; k++)
                {
                    double t = Parameters.RoundedCornersFraction + (1 - 2 * Parameters.RoundedCornersFraction) * (k + 0.5) / samples;
                    var q = p0 + dir * (t * len);
                    var e = EdgeAlongNormal(img, q, normal, 3.0);
                    if (e is { } ep) pts.Add(ep);
                }
                if (pts.Count < 3) return null;
                lines[s] = FitLine(pts);
            }
            double maxMove = 0;
            var next = new Vec2[4];
            for (int s = 0; s < 4; s++)
            {
                var la = lines[(s + 3) % 4]!.Value; var lb = lines[s]!.Value;
                if (Intersect(la, lb) is not { } x) return null;
                maxMove = Math.Max(maxMove, (x - corners[s]).Length);
                next[s] = x;
            }
            corners = next;
            if (maxMove < Parameters.MinCornerChange) break;
        }
        for (int s = 0; s < 4; s++) if ((corners[s] - original[s]).Length > Parameters.MaxCornerChange + 1.5) return null;
        return corners;
    }

    /// <summary>The sub-pixel position of the strongest brightness step along the normal through <paramref name="q"/>.</summary>
    private static Vec2? EdgeAlongNormal(GrayImage img, Vec2 q, Vec2 normal, double range)
    {
        const double step = 0.5;
        int n = (int)(2 * range / step) + 1;
        var vals = new double[n];
        for (int i = 0; i < n; i++)
        {
            var p = q + normal * (-range + i * step);
            if (p.X < 1 || p.Y < 1 || p.X >= img.Width - 1 || p.Y >= img.Height - 1) return null;
            vals[i] = Bilinear(img, p);
        }
        int bestI = -1; double bestG = 0;
        for (int i = 1; i < n - 1; i++)
        {
            double g = vals[i + 1] - vals[i - 1];     // dark inside (negative offset) to light outside
            if (g > bestG) { bestG = g; bestI = i; }
        }
        if (bestI < 0 || bestG < 8) return null;
        // parabolic interpolation of the gradient peak
        double gm = vals[bestI] - vals[Math.Max(0, bestI - 2)], gp = vals[Math.Min(n - 1, bestI + 2)] - vals[bestI];
        double g0 = bestG;
        double denom = gm - 2 * g0 + gp;
        double off = Math.Abs(denom) > 1e-9 ? Math.Clamp(0.5 * (gm - gp) / denom, -1, 1) : 0;
        return q + normal * (-range + (bestI + off) * step);
    }

    private static double Bilinear(GrayImage img, Vec2 p)
    {
        int x0 = (int)Math.Floor(p.X), y0 = (int)Math.Floor(p.Y);
        double fx = p.X - x0, fy = p.Y - y0;
        double v00 = img[x0, y0], v10 = img[x0 + 1, y0], v01 = img[x0, y0 + 1], v11 = img[x0 + 1, y0 + 1];
        return (v00 * (1 - fx) + v10 * fx) * (1 - fy) + (v01 * (1 - fx) + v11 * fx) * fy;
    }

    private static (Vec2 P, Vec2 D) FitLine(List<Vec2> pts)
    {
        double mx = pts.Average(p => p.X), my = pts.Average(p => p.Y);
        double sxx = 0, sxy = 0, syy = 0;
        foreach (var p in pts) { double dx = p.X - mx, dy = p.Y - my; sxx += dx * dx; sxy += dx * dy; syy += dy * dy; }
        // principal direction of the covariance
        double theta = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        return (new Vec2(mx, my), new Vec2(Math.Cos(theta), Math.Sin(theta)));
    }

    private static Vec2? Intersect((Vec2 P, Vec2 D) a, (Vec2 P, Vec2 D) b)
    {
        double den = a.D.X * b.D.Y - a.D.Y * b.D.X;
        if (Math.Abs(den) < 1e-9) return null;
        var d = b.P - a.P;
        double t = (d.X * b.D.Y - d.Y * b.D.X) / den;
        return a.P + a.D * t;
    }

    // ------------------------------------------------------------------ geometry checks

    /// <summary>
    /// <c>IsQuadrilateralReasonable(quad, minQuadArea, symmetryThreshold, minDistanceFromEdge, width,
    /// height, out swapCorners)</c> 0x00892B18, in its order:
    /// <list type="number">
    /// <item>the cross product of the first three corners must reach <c>minQuadArea</c> in absolute value
    /// (0x00892B42..0x00892B5E); its sign is also what the engine reports as the winding and uses to put
    /// the corners in a canonical order.</item>
    /// <item>the cross products either diagonal makes with the remaining corners must agree in sign - the
    /// two comparisons at 0x00892C68 and 0x00892CCA, each rejecting the quad when the signs differ. That
    /// is convexity.</item>
    /// <item>the two triangles a diagonal splits the quad into must be within the symmetry threshold:
    /// <c>max &lt;&lt; 8 &lt; threshold * min</c>, and one of the two splits passing is enough
    /// (0x00892CFC..0x00892D22).</item>
    /// <item>every corner must be at least <c>minDistanceFromEdge</c> from each side of the image, the
    /// loop at 0x00892D3A comparing against the margin and against width/height less the margin and one.</item>
    /// </list>
    /// The side-length rules this stack used to apply here - a six-pixel floor and a tenth of the longest
    /// side - are not in the engine's test and are gone; <c>minQuadArea</c> is what rejects a degenerate
    /// quad.
    /// </summary>
    public static bool IsQuadrilateralReasonable(Vec2[] c, int width, int height, QuadDetectorParameters? parameters = null) =>
        IsQuadrilateralReasonable(c, width, height, parameters, out _);

    /// <summary>
    /// The test, with the winding flag the engine reports through its <c>bool&amp;</c>: it is set when the
    /// first three corners turn the other way, and both the test itself and the caller then work on the
    /// quad with its middle pair exchanged (0x00892B7E and 0x00892F04). Corners arrive in the engine's
    /// <c>Quadrilateral</c> order - upper left, lower left, upper right, lower right - so the two
    /// diagonals are corners 1-2 and 0-3.
    /// </summary>
    public static bool IsQuadrilateralReasonable(Vec2[] c, int width, int height, QuadDetectorParameters? parameters, out bool swapped)
    {
        var p = parameters ?? new QuadDetectorParameters();
        swapped = false;

        double cross = (c[1].X - c[0].X) * (c[2].Y - c[0].Y) - (c[2].X - c[0].X) * (c[1].Y - c[0].Y);
        if (Math.Abs(cross) < p.MinQuadArea) return false;
        swapped = cross > 0;
        var w = swapped ? new[] { c[0], c[2], c[1], c[3] } : c;

        // convexity: each diagonal's two triangles must turn the same way. The engine compares
        // signbit(), so an exactly collinear triple counts as positive rather than failing.
        static bool Negative(double v) => v < 0;
        double area0 = swapped ? -cross : cross;
        double area3 = (w[1].Y - w[3].Y) * (w[2].X - w[3].X) - (w[1].X - w[3].X) * (w[2].Y - w[3].Y);
        if (Negative(area0) != Negative(area3)) return false;
        double area1 = (w[0].X - w[1].X) * (w[3].Y - w[1].Y) - (w[0].Y - w[1].Y) * (w[3].X - w[1].X);
        double area2 = (w[0].Y - w[2].Y) * (w[3].X - w[2].X) - (w[0].X - w[2].X) * (w[3].Y - w[2].Y);
        if (Negative(area1) != Negative(area2)) return false;

        // one diagonal's two triangles within the threshold, in the engine's fixed point
        bool Symmetric(double a, double b)
        {
            double lo = Math.Min(Math.Abs(a), Math.Abs(b)), hi = Math.Max(Math.Abs(a), Math.Abs(b));
            return hi * 256 < p.QuadSymmetryThresholdQ8 * lo;
        }
        if (!Symmetric(area0, area3) && !Symmetric(area1, area2)) return false;

        int margin = p.MinDistanceFromEdge;
        foreach (var q in w)
            if (q.X < margin || q.Y < margin || q.X >= width - margin - 1 || q.Y >= height - margin - 1)
                return false;
        return true;
    }

    /// <summary>
    /// The clockwise corners in the order the engine's <c>Quadrilateral&lt;s16&gt;</c> holds them, upper
    /// left, lower left, upper right, lower right, which is also what the decoder wants: the permutation
    /// 0, 3, 1, 2 that <c>ComputeQuadrilateralsFromConnectedComponents</c> writes at
    /// 0x00892E88..0x00892EC4. The first corner is whichever one
    /// <c>Quadrilateral::ComputeClockwiseCorners</c> put first, the one nearest the negative x axis from
    /// the centroid - no separate search for a top-left corner.
    /// </summary>
    private static Vec2[] ToDecoderOrder(Vec2[] clockwise) =>
        new[] { clockwise[0], clockwise[3], clockwise[1], clockwise[2] };
}
