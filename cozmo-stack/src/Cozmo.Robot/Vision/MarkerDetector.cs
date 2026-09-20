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

    /// <summary>The quads of the last frame with the decode outcome of each.</summary>
    public IReadOnlyList<(DetectedQuad Quad, ObservedMarker? Marker, string Reason)> LastQuads { get; private set; } = Array.Empty<(DetectedQuad, ObservedMarker?, string)>();

    public IReadOnlyList<ObservedMarker> Detect(GrayImage img, uint timestamp)
    {
        var quads = Quads.Detect(img);
        var results = new List<(DetectedQuad, ObservedMarker?, string)>();
        var markers = new List<ObservedMarker>();
        foreach (var q in quads)
        {
            if (markers.Count >= Quads.Parameters.MaxMarkers) break;
            var m = Decoder.Extract(img, q.Corners, timestamp, out var reason);
            results.Add((q, m, reason));
            if (m is null) continue;
            // a black ring can yield the same marker twice (inner and outer edge components); keep the larger
            var dup = markers.FindIndex(x => x.Code == m.Code && (x.Center - m.Center).Length < 4);
            if (dup >= 0)
            {
                if (SideLength(m) > SideLength(markers[dup])) markers[dup] = m;
                continue;
            }
            markers.Add(m);
        }
        LastQuads = results;
        return markers;
    }

    private static double SideLength(ObservedMarker m) => (m.Corners[2] - m.Corners[0]).Length;
}
