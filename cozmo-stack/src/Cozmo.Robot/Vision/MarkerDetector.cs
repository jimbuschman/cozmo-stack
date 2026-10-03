namespace Cozmo.Robot.Vision;

/// <summary>
/// <c>Anki::Embedded::DetectFiducialMarkers</c>: quads from the front end, each decoded against the
/// nearest-neighbour library. Reports why each quad was dropped, so the conformance tool can show its work.
/// </summary>
public sealed class MarkerDetector
{
    public MarkerDetector(QuadDetector? quads = null, MarkerDecoder? decoder = null)
    {
        Quads = quads ?? new QuadDetector();
        Decoder = decoder ?? new MarkerDecoder();
    }

    public QuadDetector Quads { get; }
    public MarkerDecoder Decoder { get; }

    /// <summary>
    /// <c>MarkerDetector::Init</c> 0x008752E0, called by <c>VisionSystem::UpdateCameraCalibration</c>
    /// 0x006B1E78 when a calibration is installed (M11-039): it calls
    /// <c>MarkerDetector::Parameters::Initialize</c> 0x008752F8, restoring the detector's parameters to the
    /// shipped values. (The engine also passes the calibration's width and height, which <c>Initialize</c>
    /// does not use.)
    /// </summary>
    // fidelity: M11-039
    public void Init() => Quads.ResetParameters();

    /// <summary>The quads of the last frame with the decode outcome of each.</summary>
    public IReadOnlyList<(DetectedQuad Quad, ObservedMarker? Marker, string Reason)> LastQuads { get; private set; } = Array.Empty<(DetectedQuad, ObservedMarker?, string)>();

    public IReadOnlyList<ObservedMarker> Detect(GrayImage img, uint timestamp)
    {
        // fidelity: M11-022, M11-023, M11-024, M11-025, M11-026, M11-027, M11-028, M11-029, M11-030, M11-031
        // (the per-marker order is here; the component filters live in QuadDetector, the boundary trace and
        // the line fit in QuadCorners, and the contrast gate and refinement in CornerRefinement)

        // C1.2 M11-022: the ROI/negative mode is MarkerDetector::Parameters+0x7c (0x00875400/0x00875402).
        // Shipped 0 (Parameters::Initialize 0x008753AE writes 0x100 at +0x7c), so the pass list is {false}:
        // a single non-negative pass, the ROI vector is never populated (the size >= 2 guard at
        // 0x0087551C) and the negative branch is inert. The mechanism is transcribed for completeness.
        var passes = Quads.Parameters.NegativeMode switch
        {
            0 => new List<bool> { false },
            1 => new List<bool> { true },
            2 => new List<bool> { false, true },
            _ => new List<bool>(),
        };
        var results = new List<(DetectedQuad, ObservedMarker?, string)>();
        var markers = new List<ObservedMarker>();
        var roi = new List<(int X, int Y, int W, int H)>();

        foreach (bool negative in passes)
        {
            var work = BuildPassImage(img, roi, negative);
            var quads = Quads.Detect(work);

            // G2.6: when the pass list has >= 2 entries, each detected marker's quad becomes a Rectangle<int>
            // ROI entry (InitFromPointContainer 0x006ABB38: truncating vcvt.s32.f32, {xmin,ymin,width,height}).
            if (passes.Count >= 2)
                foreach (var q in quads) roi.Add(BoundingRect(q.Corners));

            DecodePass(work, quads, timestamp, results, markers);
        }
        LastQuads = results;
        return markers;
    }

    /// <summary>
    /// One <c>Detect</c> pass (G2.3/G2.4): if the ROI vector is non-empty, copy the source and fill each ROI
    /// rectangle with <c>0xff</c>; if the pass is negative, <c>GetNegative</c> (0x00871DCC) is
    /// <c>cv::bitwise_not</c>, i.e. <c>255 - pixel</c>.
    /// </summary>
    private static GrayImage BuildPassImage(GrayImage img, List<(int X, int Y, int W, int H)> roi, bool negative)
    {
        GrayImage work = img;
        if (roi.Count > 0)
        {
            work = new GrayImage(img.Width, img.Height, (byte[])img.Pixels.Clone());
            foreach (var r in roi)
                for (int y = r.Y; y < r.Y + r.H; y++)
                    for (int x = r.X; x < r.X + r.W; x++)
                        if (x >= 0 && y >= 0 && x < work.Width && y < work.Height) work[x, y] = 0xff;
        }
        if (negative)
        {
            var neg = new GrayImage(work.Width, work.Height);
            for (int i = 0; i < neg.Pixels.Length; i++) neg.Pixels[i] = (byte)(255 - work.Pixels[i]);
            work = neg;
        }
        return work;
    }

    /// <summary>
    /// <c>InitFromPointContainer</c> 0x006ABB38: the quad's bounding <c>Rectangle&lt;int&gt;</c>
    /// <c>{xmin, ymin, width, height}</c>, each coordinate truncated toward zero.
    /// </summary>
    internal static (int X, int Y, int W, int H) BoundingRect(Vec2[] c)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var p in c)
        {
            int x = (int)(float)p.X, y = (int)(float)p.Y;
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
        }
        // 0x006ABBCA..0x006ABBD0: width = maxX - minX, height = maxY - minY (no +1)
        return (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>The per-marker refine-then-decode loop (M11-023), run once per pass.</summary>
    private void DecodePass(GrayImage work, IReadOnlyList<DetectedQuad> quads, uint timestamp,
        List<(DetectedQuad, ObservedMarker?, string)> results, List<ObservedMarker> markers)
    {
        foreach (var q in quads)
        {
            if (markers.Count >= Quads.Parameters.MaxMarkers) break;
            // DetectFiducialMarkers 0x008994CE..0x00899528: each marker's corners are refined against its
            // homography before it is decoded, and a marker whose refinement fails is not decoded at all
            // (validity 7, or 2 for too little contrast).
            var corners = q.Corners;
            Homography? h = null;
            if (Decoder.HasLibrary)
            {
                h = Homography.FromUnitSquare(corners);
                // the refinement runs on the illumination-normalised region, which is put back before decoding
                // (DetectFiducialMarkers 0x008990C8..0x0089954E)
                var region = CornerRefinement.NormalizeIllumination(work, corners, Quads.Parameters);
                if (region is null) { results.Add((q, null, "empty region for its corners")); continue; }
                var outcome = CornerRefinement.RefineCorners(work, Decoder.Library, ref corners, ref h, Quads.Parameters);
                CornerRefinement.RestoreRegion(work, region.Value);
                if (outcome != CornerRefinement.Outcome.Refined)
                {
                    results.Add((q, null, outcome == CornerRefinement.Outcome.LowContrast ? "too little contrast to refine" : "corner refinement moved the corners too far"));
                    continue;
                }
            }
            // fidelity: M11-002, M11-031 — the decoder algorithm and its 1.01 contrast gate
            var m = Decoder.Extract(work, corners, h, Quads.Parameters, timestamp, out var reason);
            results.Add((q, m, reason));
            if (m is null) continue;
            // M11-022: the engine appends every decoded marker to the ObservedMarker list (0x0087555C emplace_back, no comparison
            // of codes or centres anywhere in Detect 0x008753B6..0x00875566); the same-code merge this stack had is removed.
            markers.Add(m);
        }
    }
}
