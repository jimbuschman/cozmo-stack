using Cozmo.Robot.Behavior;

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

    /// <summary>The whole quad list of the last pass (ComputeQuadrilateralsFromConnectedComponents' output, up to 500), before the 100-slot cut.</summary>
    public IReadOnlyList<DetectedQuad> LastQuadList { get; private set; } = Array.Empty<DetectedQuad>();

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
        LastResult = 0;
        LastFinalSlots = Array.Empty<FinalSlot>();

        foreach (bool negative in passes)
        {
            var work = BuildPassImage(img, roi, negative);
            var quads = Quads.Detect(work);
            LastQuadList = quads;

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

    /// <summary>
    /// One marker slot of <c>DetectFiducialMarkers</c>' VisionMarker list (0x40 bytes each): the corners as binary32, the homography and
    /// the validity (+0x28): 4 after a good homography, 6 after a failed one (rows 19-22), then 0 (decoded), 2 (too little contrast),
    /// 3 (not a known marker) or 7 (refinement failed).
    /// </summary>
    public sealed record MarkerSlot(DetectedQuad Quad, float[] H, int Validity);

    /// <summary>The slots of the last frame after the homography loop (0x00898F1C), before refinement and decoding.</summary>
    public IReadOnlyList<MarkerSlot> LastSlots { get; private set; } = Array.Empty<MarkerSlot>();

    /// <summary>
    /// A slot after the refine/decode loop (0x00899668, before the erase): its validity, its type (+0x20; 0x27 for validity 2, 3 and 7,
    /// the code for 0; -1 where this stack does not model the slot's untouched type), its corners (refined, and reordered when decoded) and
    /// its homography (the refined one once RefineCorners ran).
    /// </summary>
    public sealed record FinalSlot(int Validity, int Type, Vec2[] Corners, float[] H);

    /// <summary>The slots of the last frame after the refine/decode loop.</summary>
    public IReadOnlyList<FinalSlot> LastFinalSlots { get; private set; } = Array.Empty<FinalSlot>();

    /// <summary>The engine's type for a slot that is not a marker (validity 2, 3, 7): 0x27 (0x008995A8, 0x008A0078 reject).</summary>
    public const int UnknownMarkerType = 0x27;

    /// <summary>
    /// Detect's Result for the last frame: 0, or 0x05000000 when DetectFiducialMarkers returned an error for a pass (row 35,
    /// 0x00898EA0 -&gt; 0x00898830). The only error our inputs reach is the homography loop meeting quad 100 (row 18).
    /// </summary>
    // fidelity: M11-104
    public uint LastResult { get; private set; }

    /// <summary>The error Result DetectFiducialMarkers returns when EstimateHomography fails on the slot past the list (0x008C93E8).</summary>
    public const uint ResultFailInvalidSize = 0x05000000;

    /// <summary>
    /// The per-pass body of <c>DetectFiducialMarkers</c> after the quads (M11-023):
    /// <list type="number">
    /// <item>the marker count is <c>min(max(quads, 0), params+0x6c)</c> = min(quads, 100) (0x00898E38..0x00898E52);</item>
    /// <item>the homography loop runs over every quad (0x00898E62..0x00898F1A): slot i gets the quad's corners as binary32, validity 4 and
    /// <c>ComputeHomographyFromQuad</c>'s H (<see cref="Homography.ComputeFromQuadS16"/>); a numerical failure, or H[2][0] &gt; 8.0 or
    /// H[2][1] &gt; 8.0 (signed binary32 compares against the <c>vmov.f32 s16, #8.0</c> at 0x00898E68), makes it validity 6
    /// (0x00898EE2..0x00898F12). Beyond the 100-slot list the engine writes past its end (slot i·0x40 for i &gt;= 100), which is
    /// undefined; the engine then errors and the pass adds no markers, so under the M1-034 standing rule the stack drops the pass, logs it, and carries on;</item>
    /// <item>for each of the first count slots (0x00899080..0x00899664): the illumination normalisation (params+1) runs before the
    /// validity test (0x008990BE..0x008994CA); an empty region logs and leaves the slot at 4, which the erase drops
    /// (0x0089955A..0x0089957C); a slot that is not validity 4 is put back and skipped (0x008994D4, 0x0089957E); otherwise RefineCorners,
    /// the put-back, and the decode (0x008994D8..0x008995AA, 0x00899030..0x0089907E);</item>
    /// <item>the slots of validity 2 or more are erased in order (0x00899668..0x0089968E).</item>
    /// </list>
    /// </summary>
    // fidelity: M11-023
    private void DecodePass(GrayImage work, IReadOnlyList<DetectedQuad> quads, uint timestamp,
        List<(DetectedQuad, ObservedMarker?, string)> results, List<ObservedMarker> markers)
    {
        int capacity = Quads.Parameters.MaxMarkers;
        int count = Math.Min(Math.Max(quads.Count, 0), capacity);
        var slots = new List<MarkerSlot>(count);
        for (int i = 0; i < quads.Count; i++)
        {
            if (i >= capacity)
            {
                // M1-034 standing rule (operator, 2026-10-13): the engine's homography loop runs past the end of the 100-slot list for
                // every quad beyond it (0x00898E78..0x00898F1A), which is undefined. Under the emulator the engine then reads a slot header
                // past the list, logs "EstimateHomography: homography must be 3x3" and returns 0x05000000, so the pass adds no markers
                // (0x00898EA0 -> 0x00898830), and VisionSystem::Update abandons the frame (VisionSystem.ProcessImage). The pass's markers are dropped here; the frame is dropped there.
                SteppedBehavior.ReportMissing($"M1-034/M11-023 DetectFiducialMarkers homography loop: {quads.Count - capacity} quad(s) beyond the " +
                                              $"{capacity}-slot marker list (0x00898E78..0x00898F1A runs past the list); the pass returns the engine's error and VisionSystem abandons the frame, as VisionSystem::Update does");
                LastSlots = slots;
                LastResult = ResultFailInvalidSize;
                return;
            }
            var q = quads[i];
            var s16 = new short[8];
            for (int k = 0; k < 4; k++) { s16[2 * k] = (short)q.Corners[k].X; s16[2 * k + 1] = (short)q.Corners[k].Y; }
            var h = Homography.ComputeFromQuadS16(s16, out bool failure);
            int validity = failure || h[6] > 8f || h[7] > 8f ? 6 : 4;
            slots.Add(new MarkerSlot(q, h, validity));
        }
        LastSlots = slots;
        var finals = new List<FinalSlot>(count);
        LastFinalSlots = finals;

        for (int i = 0; i < count; i++)
        {
            var slot = slots[i];
            var q = slot.Quad;
            var corners = q.Corners;
            int fi = finals.Count;
            finals.Add(new FinalSlot(slot.Validity, -1, corners, slot.H));
            if (!Decoder.HasLibrary)
            {
                if (slot.Validity != 4) { results.Add((q, null, "the homography failed (validity 6)")); continue; }
                var none = Decoder.Extract(work, corners, null, Quads.Parameters, timestamp, out var why);
                results.Add((q, none, why));
                continue;
            }
            // fidelity: M11-020 — the normalisation runs on every slot, before the validity test (0x008990BE..0x008994CA)
            var region = CornerRefinement.NormalizeIllumination(work, corners, Quads.Parameters);
            if (region is null) { results.Add((q, null, "empty region for its corners")); continue; }
            if (slot.Validity != 4)
            {
                CornerRefinement.RestoreRegion(work, region.Value);   // 0x0089957E..0x00899598
                results.Add((q, null, "the homography failed (validity 6)"));
                continue;
            }
            var hd = new double[9];
            for (int k = 0; k < 9; k++) hd[k] = slot.H[k];
            var hom = new Homography(hd);
            // DetectFiducialMarkers 0x008994CE..0x00899528: each marker's corners are refined against its homography before it is
            // decoded, and a marker whose refinement fails is not decoded at all (validity 7, or 2 for too little contrast).
            var outcome = CornerRefinement.RefineCorners(work, Decoder.Library, ref corners, ref hom, Quads.Parameters);
            CornerRefinement.RestoreRegion(work, region.Value);
            if (outcome == CornerRefinement.Outcome.Skipped)
            {
                results.Add((q, null, "M1-034: a contrast sample falls outside the image buffer; the marker is skipped"));
                continue;                                                          // the slot stays validity 4 and is erased
            }
            var refinedH = hom.H.Select(v => (float)v).ToArray();
            finals[fi] = new FinalSlot(outcome switch { CornerRefinement.Outcome.LowContrast => 2, CornerRefinement.Outcome.Failed => 7, _ => 4 },
                                       outcome == CornerRefinement.Outcome.Refined ? -1 : UnknownMarkerType, corners, refinedH);
            if (outcome != CornerRefinement.Outcome.Refined)
            {
                results.Add((q, null, outcome == CornerRefinement.Outcome.LowContrast ? "too little contrast to refine" : "corner refinement moved the corners too far"));
                continue;
            }
            // fidelity: M11-002, M11-031 — the decoder algorithm and its 1.01 contrast gate
            var m = Decoder.Extract(work, corners, hom, Quads.Parameters, timestamp, out var reason);
            finals[fi] = m is null ? new FinalSlot(3, UnknownMarkerType, corners, refinedH) : new FinalSlot(0, (int)m.Code, m.Corners, refinedH);
            results.Add((q, m, reason));
            if (m is null) continue;
            // M11-022: the engine appends every decoded marker to the ObservedMarker list (0x0087555C emplace_back, no comparison
            // of codes or centres anywhere in Detect 0x008753B6..0x00875566); the same-code merge this stack had is removed.
            markers.Add(m);
        }
    }
}
