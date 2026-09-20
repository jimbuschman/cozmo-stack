namespace Cozmo.Robot.Vision;

/// <summary>
/// The engine's <c>MarkerDetector::Parameters</c> (<c>Parameters::Initialize</c> 0x008752F8). Every value is
/// the immediate stored there (NATIVE); the names are those of Anki's fiducial detector, which the parameter
/// order and the pipeline's own profiler labels identify.
/// </summary>
public sealed record QuadDetectorParameters
{
    /// <summary>"Only 3 pyramid levels" (0x00875 region assert string).</summary>
    public int PyramidLevels { get; init; } = 3;
    /// <summary>Components with fewer pixels than this are dropped (100).</summary>
    public int MinComponentPixels { get; init; } = 100;
    /// <summary>Components with more pixels than this are dropped (39000).</summary>
    public int MaxComponentPixels { get; init; } = 39000;
    /// <summary>Upper bound on 1-D segments per image (32000).</summary>
    public int MaxSegments { get; init; } = 32000;
    /// <summary>Upper bound on quads per image (512) and on extracted markers (500).</summary>
    public int MaxQuads { get; init; } = 512;
    public int MaxMarkers { get; init; } = 500;
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
    /// <summary>Quads closer than this to the image edge are dropped (2 px; NATIVE flag block <c>0x101, 1, 4, 2</c> — the 2).</summary>
    public int MinDistanceFromEdge { get; init; } = 2;
    /// <summary>LOCAL: a pixel is "dark" when below this fraction of the local mean at its characteristic scale.</summary>
    public double DarkThresholdMultiplier { get; init; } = 0.75;
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
/// 1. a 3-level box pyramid gives each pixel a local mean at its characteristic scale (the scale with the
///    largest deviation); pixels below 0.75 of that mean are "dark";
/// 2. 8-connected components of dark pixels, filtered by size, fill ratio and hollowness;
/// 3. the four corners are the extreme points of the component's boundary (farthest pair, then farthest from
///    that diagonal on each side), ordered clockwise on screen;
/// 4. each side is refined to the sub-pixel edge by fitting a line to gradient maxima, up to 25 times.
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
            var corners = CornersFromBoundary(c, labels, img.Width, img.Height);
            if (corners is null) continue;
            st.Quads++;
            var refined = Refine(img, corners);
            if (refined is null) continue;
            if (!GeometryOk(refined, img)) continue;
            st.AfterGeometry++;
            quads.Add(new DetectedQuad(ToDecoderOrder(refined), c.Pixels));
            if (quads.Count >= Parameters.MaxQuads) break;
        }
        LastStats = st;
        return quads;
    }

    // ------------------------------------------------------------------ characteristic scale

    private GrayImage CharacteristicScaleMask(GrayImage img)
    {
        int w = img.Width, h = img.Height;
        var integral = Integral(img);
        var mask = new GrayImage(w, h);
        // box radii for the pyramid levels: 2, 4, 8 (a 3-level binomial pyramid's support)
        int[] radii = Enumerable.Range(1, Parameters.PyramidLevels).Select(l => 1 << l).ToArray();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int v = img.Pixels[y * w + x];
                double bestMean = v, bestDev = -1;
                foreach (var r in radii)
                {
                    double mean = BoxMean(integral, w, h, x, y, r);
                    double dev = Math.Abs(mean - v);
                    if (dev > bestDev) { bestDev = dev; bestMean = mean; }
                }
                if (v < bestMean * Parameters.DarkThresholdMultiplier) mask.Pixels[y * w + x] = 1;
            }
        return mask;
    }

    private static long[] Integral(GrayImage img)
    {
        int w = img.Width + 1, h = img.Height + 1;
        var s = new long[w * h];
        for (int y = 1; y < h; y++)
        {
            long row = 0;
            for (int x = 1; x < w; x++)
            {
                row += img.Pixels[(y - 1) * img.Width + (x - 1)];
                s[y * w + x] = s[(y - 1) * w + x] + row;
            }
        }
        return s;
    }

    private static double BoxMean(long[] integral, int w, int h, int x, int y, int r)
    {
        int x0 = Math.Max(0, x - r), y0 = Math.Max(0, y - r), x1 = Math.Min(w - 1, x + r), y1 = Math.Min(h - 1, y + r);
        int iw = w + 1;
        long sum = integral[(y1 + 1) * iw + (x1 + 1)] - integral[y0 * iw + (x1 + 1)] - integral[(y1 + 1) * iw + x0] + integral[y0 * iw + x0];
        return sum / (double)((x1 - x0 + 1) * (y1 - y0 + 1));
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

    // ------------------------------------------------------------------ corners

    /// <summary>Extreme points of the boundary: the farthest pair, then the farthest point on each side of that diagonal. Cyclic (clockwise on screen) order.</summary>
    private static Vec2[]? CornersFromBoundary(Component c, int[] labels, int w, int h)
    {
        var boundary = new List<Vec2>();
        for (int y = c.MinY; y <= c.MaxY; y++)
            for (int x = c.MinX; x <= c.MaxX; x++)
            {
                if (labels[y * w + x] != c.Label) continue;
                bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                            || labels[y * w + x - 1] != c.Label || labels[y * w + x + 1] != c.Label
                            || labels[(y - 1) * w + x] != c.Label || labels[(y + 1) * w + x] != c.Label;
                if (edge) boundary.Add(new Vec2(x, y));
            }
        if (boundary.Count < 8) return null;
        // only the outer boundary matters: keep points on the convex hull
        var hull = ConvexHull(boundary);
        if (hull.Count < 4) return null;

        int i0 = 0, i1 = 0; double best = -1;
        for (int i = 0; i < hull.Count; i++)
            for (int j = i + 1; j < hull.Count; j++)
            {
                double d = (hull[i] - hull[j]).Length;
                if (d > best) { best = d; i0 = i; i1 = j; }
            }
        var a = hull[i0]; var b = hull[i1];
        double side(Vec2 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        Vec2? left = null, right = null; double bl = 0, br = 0;
        foreach (var p in hull)
        {
            double s = side(p);
            if (s > bl) { bl = s; left = p; }
            if (s < br) { br = s; right = p; }
        }
        if (left is null || right is null) return null;
        var corners = new[] { a, left.Value, b, right.Value };
        // clockwise on screen (y down): sort by angle around the centroid
        var cen = new Vec2(corners.Average(p => p.X), corners.Average(p => p.Y));
        return corners.OrderBy(p => Math.Atan2(p.Y - cen.Y, p.X - cen.X)).ToArray();
    }

    private static List<Vec2> ConvexHull(List<Vec2> pts)
    {
        var p = pts.OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
        if (p.Count < 3) return p;
        double cross(Vec2 o, Vec2 a, Vec2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var lower = new List<Vec2>();
        foreach (var q in p) { while (lower.Count >= 2 && cross(lower[^2], lower[^1], q) <= 0) lower.RemoveAt(lower.Count - 1); lower.Add(q); }
        var upper = new List<Vec2>();
        for (int i = p.Count - 1; i >= 0; i--) { var q = p[i]; while (upper.Count >= 2 && cross(upper[^2], upper[^1], q) <= 0) upper.RemoveAt(upper.Count - 1); upper.Add(q); }
        lower.RemoveAt(lower.Count - 1); upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
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

    private bool GeometryOk(Vec2[] c, GrayImage img)
    {
        foreach (var p in c)
            if (p.X < Parameters.MinDistanceFromEdge || p.Y < Parameters.MinDistanceFromEdge
                || p.X > img.Width - 1 - Parameters.MinDistanceFromEdge || p.Y > img.Height - 1 - Parameters.MinDistanceFromEdge) return false;
        double minSide = double.MaxValue, maxSide = 0;
        for (int i = 0; i < 4; i++)
        {
            double s = (c[(i + 1) % 4] - c[i]).Length;
            minSide = Math.Min(minSide, s); maxSide = Math.Max(maxSide, s);
        }
        if (minSide < 6 || minSide < Parameters.MinSideLengthFraction * maxSide) return false;
        // convex and consistently oriented
        double sign = 0;
        for (int i = 0; i < 4; i++)
        {
            var a = c[(i + 1) % 4] - c[i]; var b = c[(i + 2) % 4] - c[(i + 1) % 4];
            double z = a.X * b.Y - a.Y * b.X;
            if (sign == 0) sign = Math.Sign(z);
            else if (Math.Sign(z) != sign) return false;
        }
        return true;
    }

    /// <summary>Cyclic clockwise-on-screen order (TL, TR, BR, BL) to the decoder's TL, BL, TR, BR, with the top-left chosen as the corner nearest the image's top-left.</summary>
    private static Vec2[] ToDecoderOrder(Vec2[] cyclic)
    {
        // make sure the order is clockwise on screen (positive shoelace with y down)
        double a = 0;
        for (int i = 0; i < 4; i++) a += cyclic[i].X * cyclic[(i + 1) % 4].Y - cyclic[(i + 1) % 4].X * cyclic[i].Y;
        var cw = a > 0 ? cyclic : new[] { cyclic[0], cyclic[3], cyclic[2], cyclic[1] };
        int tl = 0; double best = double.MaxValue;
        for (int i = 0; i < 4; i++) { double d = cw[i].X + cw[i].Y; if (d < best) { best = d; tl = i; } }
        Vec2 TL = cw[tl], TR = cw[(tl + 1) % 4], BR = cw[(tl + 2) % 4], BL = cw[(tl + 3) % 4];
        return new[] { TL, BL, TR, BR };
    }
}
