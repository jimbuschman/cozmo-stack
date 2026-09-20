using System.Buffers.Binary;
using System.Reflection;

namespace Cozmo.Robot.Vision;

/// <summary>
/// <c>Anki::Vision::MarkerType</c>, from the name table <c>Marker::GetNameForCode</c> indexes (GOT slot
/// 0x1038220, 0x0087E0D4). NATIVE. 0x7FFF is <c>MARKER_ANY</c>.
/// </summary>
public enum MarkerType : ushort
{
    Arrow = 0, Bullseye2 = 1, Charger = 2, Invalid = 3,
    LightCubeI_Back = 4, LightCubeI_Bottom = 5, LightCubeI_Front = 6, LightCubeI_Left = 7, LightCubeI_Right = 8, LightCubeI_Top = 9,
    LightCubeJ_Back = 10, LightCubeJ_Bottom = 11, LightCubeJ_Front = 12, LightCubeJ_Left = 13, LightCubeJ_Right = 14, LightCubeJ_Top = 15,
    LightCubeK_Back = 16, LightCubeK_Bottom = 17, LightCubeK_Front = 18, LightCubeK_Left = 19, LightCubeK_Right = 20, LightCubeK_Top = 21,
    Sdk2Circles = 22, Sdk2Diamonds = 23, Sdk2Hexagons = 24, Sdk2Triangles = 25,
    Sdk3Circles = 26, Sdk3Diamonds = 27, Sdk3Hexagons = 28, Sdk3Triangles = 29,
    Sdk4Circles = 30, Sdk4Diamonds = 31, Sdk4Hexagons = 32, Sdk4Triangles = 33,
    Sdk5Circles = 34, Sdk5Diamonds = 35, Sdk5Hexagons = 36, Sdk5Triangles = 37,
    Star5 = 38, Unknown = 39,
    Any = 0x7FFF,
}

/// <summary>
/// The engine's compiled-in nearest-neighbour marker library, <c>VisionMarker::GetNearestNeighborLibrary</c>
/// (0x0089ED1C): 598 probe images of 1024 values, their labels, and the label tables that turn a match into a
/// marker code, a corner order and an orientation. Extracted byte for byte by
/// <c>re-analysis/tools/extract_marker_library.py</c> and embedded as a resource. Every constant here is NATIVE.
/// </summary>
public sealed class MarkerLibrary
{
    public const int GridSize = 32;
    public const int NumProbes = 1024;
    public const int NumFractionalBits = 15;
    public const int NumProbePoints = 5;
    public const int NumThresholdProbes = 12;
    /// <summary>The label the engine treats as "no marker" (149; 150 is also rejected in <c>Extract</c>).</summary>
    public const int InvalidLabel = 149;
    /// <summary>Distance threshold passed from <c>DetectFiducialMarkers</c> (<c>movs r2, #0x32</c>).</summary>
    public const int MatchThreshold = 50;
    /// <summary>The two ctor arguments after the probe tables: <c>5</c> and <c>15</c>.</summary>
    public const int NumProbeSamples = 5, FractionalBits = 15;

    public int NumImages { get; }
    public int NumLabels { get; }
    /// <summary>Row-major [NumImages][NumProbes] probe values.</summary>
    public byte[] Images { get; }
    public ushort[] Labels { get; }
    public MarkerType[] LabelToCode { get; }
    /// <summary>[NumLabels][4]: output corner i is input corner Reorder[label*4+i].</summary>
    public int[] CornerReorder { get; }
    public float[] OrientationDeg { get; }
    /// <summary>Probe grid centres in the unit marker square (fixed point / 2^15).</summary>
    public short[] ProbeCentersX { get; }
    public short[] ProbeCentersY { get; }
    public short[] ProbePointsX { get; }
    public short[] ProbePointsY { get; }
    public short[] ThresholdDarkX { get; }
    public short[] ThresholdDarkY { get; }
    public short[] ThresholdBrightX { get; }
    public short[] ThresholdBrightY { get; }

    /// <summary>SHA-256 of the 3.4.0-1204 library blob, as <c>extract_marker_library.py</c> writes it.</summary>
    public const string ExpectedSha256 = "1b7166ac37b166975f0e5313a3d4486ebae73010e42ffb89bbd0d4536a29c81d";

    /// <summary>Environment variable naming a marker library file to use instead of the embedded one.</summary>
    public const string PathVariable = "COZMO_MARKER_LIBRARY";

    private static readonly Lazy<MarkerLibrary?> _embedded = new(() =>
    {
        var bytes = LoadBytes(out _);
        return bytes is null ? null : Parse(bytes);
    });

    /// <summary>
    /// The library linked into libcozmoEngine.so 3.4.0-1204, or null when it is not available on this
    /// machine. The blob is Anki's data and is not distributed with the repository: the build extracts it
    /// from the user's own <c>libcozmoEngine.so</c> (<c>re-analysis/tools/extract_marker_library.py</c>) and
    /// embeds it; failing that, a file named by <see cref="PathVariable"/> or beside the assembly is used.
    /// </summary>
    public static MarkerLibrary? EmbeddedOrNull => _embedded.Value;

    /// <summary>The library, or an exception that says how to obtain it.</summary>
    public static MarkerLibrary Embedded => _embedded.Value ?? throw new InvalidOperationException(
        "the marker library is not available: run re-analysis/tools/extract_marker_library.py against your libcozmoEngine.so " +
        $"(or set {PathVariable} to the extracted file) and rebuild; the blob is Anki's data and is not in the repository");

    /// <summary>Whether a marker library is available (embedded or on disk).</summary>
    public static bool IsAvailable => _embedded.Value is not null;

    private static byte[]? LoadBytes(out string source)
    {
        var env = Environment.GetEnvironmentVariable(PathVariable);
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) { source = env; return Verified(File.ReadAllBytes(env), source); }
        var asm = typeof(MarkerLibrary).Assembly;
        var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("marker_nn_library.bin", StringComparison.Ordinal));
        if (name is not null)
        {
            using var s = asm.GetManifestResourceStream(name)!;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            source = "embedded resource";
            return Verified(ms.ToArray(), source);
        }
        var beside = Path.Combine(AppContext.BaseDirectory, "marker_nn_library.bin");
        if (File.Exists(beside)) { source = beside; return Verified(File.ReadAllBytes(beside), source); }
        source = "none";
        return null;
    }

    /// <summary>The blob must be the 3.4.0-1204 library: anything else is a different engine build's tables.</summary>
    private static byte[] Verified(byte[] bytes, string source)
    {
        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        if (sha != ExpectedSha256)
            throw new InvalidDataException($"marker library from {source} has sha256 {sha}, expected {ExpectedSha256} (libcozmoEngine.so 3.4.0-1204)");
        return bytes;
    }

    private MarkerLibrary(int numImages, int numLabels, byte[] images, ushort[] labels, MarkerType[] codes, int[] reorder, float[] orient,
                          short[] cx, short[] cy, short[] px, short[] py, short[] dx, short[] dy, short[] bx, short[] by)
    {
        NumImages = numImages; NumLabels = numLabels; Images = images; Labels = labels; LabelToCode = codes; CornerReorder = reorder;
        OrientationDeg = orient; ProbeCentersX = cx; ProbeCentersY = cy; ProbePointsX = px; ProbePointsY = py;
        ThresholdDarkX = dx; ThresholdDarkY = dy; ThresholdBrightX = bx; ThresholdBrightY = by;
    }

    public static MarkerLibrary Parse(ReadOnlySpan<byte> blob)
    {
        if (blob.Length < 24 || !blob[..8].SequenceEqual("CZMKNN02"u8)) throw new FormatException("not a CZMKNN02 marker library");
        int ni = (int)BinaryPrimitives.ReadUInt32LittleEndian(blob[8..]);
        int np = (int)BinaryPrimitives.ReadUInt32LittleEndian(blob[12..]);
        int nl = (int)BinaryPrimitives.ReadUInt32LittleEndian(blob[16..]);
        if (np != NumProbes) throw new FormatException($"library has {np} probes, expected {NumProbes}");
        int o = 24;
        var images = blob.Slice(o, ni * np).ToArray(); o += ni * np;
        var labels = new ushort[ni];
        for (int i = 0; i < ni; i++, o += 2) labels[i] = BinaryPrimitives.ReadUInt16LittleEndian(blob[o..]);
        var codes = new MarkerType[nl];
        for (int i = 0; i < nl; i++, o += 4) codes[i] = (MarkerType)BinaryPrimitives.ReadUInt32LittleEndian(blob[o..]);
        var reorder = new int[nl * 4];
        for (int i = 0; i < nl * 4; i++, o += 4) reorder[i] = (int)BinaryPrimitives.ReadUInt32LittleEndian(blob[o..]);
        var orient = new float[nl];
        for (int i = 0; i < nl; i++, o += 4) orient[i] = BinaryPrimitives.ReadSingleLittleEndian(blob[o..]);
        var cx = S16(blob, ref o, np); var cy = S16(blob, ref o, np); var px = S16(blob, ref o, NumProbePoints); var py = S16(blob, ref o, NumProbePoints);
        var dx = S16(blob, ref o, NumThresholdProbes); var dy = S16(blob, ref o, NumThresholdProbes); var bx = S16(blob, ref o, NumThresholdProbes); var by = S16(blob, ref o, NumThresholdProbes);
        return new MarkerLibrary(ni, nl, images, labels, codes, reorder, orient, cx, cy, px, py, dx, dy, bx, by);
    }

    private static short[] S16(ReadOnlySpan<byte> blob, ref int o, int n)
    {
        var a = new short[n];
        for (int i = 0; i < n; i++, o += 2) a[i] = BinaryPrimitives.ReadInt16LittleEndian(blob[o..]);
        return a;
    }

    public ReadOnlySpan<byte> Image(int row) => Images.AsSpan(row * NumProbes, NumProbes);

    /// <summary>The library rows carrying a label.</summary>
    public IEnumerable<int> RowsForLabel(int label) => Enumerable.Range(0, NumImages).Where(i => Labels[i] == label);

    /// <summary>The labels that decode to a code.</summary>
    public IEnumerable<int> LabelsForCode(MarkerType code) => Enumerable.Range(0, NumLabels).Where(l => LabelToCode[l] == code);

    /// <summary>A probe value as a position within the unit marker square.</summary>
    public static double Fixed(short v) => v / (double)(1 << NumFractionalBits);
}

/// <summary>A marker found in an image: its code and the corners in pixels, ordered TL, BL, TR, BR after the library's reordering.</summary>
public sealed record ObservedMarker(MarkerType Code, Vec2[] Corners, uint Timestamp, int Distance, float OrientationDeg)
{
    public Vec2 Center => new((Corners[0].X + Corners[1].X + Corners[2].X + Corners[3].X) / 4, (Corners[0].Y + Corners[1].Y + Corners[2].Y + Corners[3].Y) / 4);
    public override string ToString() => $"{Code} d={Distance} corners=[{string.Join(" ", Corners)}]";
}

/// <summary>
/// <c>VisionMarker::Extract</c> (0x0089F8B4 region) over the nearest-neighbour library. NATIVE algorithm:
///
/// * <c>GetProbeValues</c> (0x0089EF41): for each of the 1024 probes, the mean of 5 samples at the homography
///   image of (centre + offset) for the 5 offsets in <c>ProbePoints</c>, each sample being the nearest pixel
///   (x rounded as floor(x + 0.5), y as ceil(y − 0.5)).
/// * <c>NearestNeighborLibrary::GetNearestNeighbor</c>: the query is min-max normalised to 0..255
///   (<c>cv::normalize</c> NORM_MINMAX); the distance to each library row is the sum of absolute differences
///   divided by the probe count (integer); best and second best are kept. If they carry different labels, the
///   distance is recomputed over only the probes where the two rows differ by more than the threshold (50),
///   and the match is rejected when that average is ≥ 1.25 × 50.
/// * <c>Extract</c> rejects distance ≥ 50 and labels 149 or 150; otherwise code = labelToCode[label], the
///   corners are reordered by cornerReorder[label] and the orientation is orientationDeg[label].
/// </summary>
public sealed class MarkerDecoder
{
    private readonly MarkerLibrary? _lib;

    /// <summary>Without a library (none embedded on this machine) the decoder decodes nothing and says why; the rest of the vision system still runs.</summary>
    public MarkerDecoder(MarkerLibrary? library = null) => _lib = library ?? MarkerLibrary.EmbeddedOrNull;

    /// <summary>The library in use, or an exception naming how to obtain one.</summary>
    public MarkerLibrary Library => _lib ?? MarkerLibrary.Embedded;

    public bool HasLibrary => _lib is not null;

    /// <summary>The result of one nearest-neighbour query, before the label tables are applied.</summary>
    public readonly record struct Match(int Label, int Distance, int Row, bool Rejected, string Reason);

    /// <summary>
    /// Samples the 1024 probe values of the marker whose unit square maps into the image by
    /// <paramref name="h"/> (a homography from marker coordinates in [0,1]² to pixels).
    /// </summary>
    public byte[] GetProbeValues(GrayImage img, Homography h)
    {
        var _lib = Library;
        var values = new byte[MarkerLibrary.NumProbes];
        for (int i = 0; i < MarkerLibrary.NumProbes; i++)
        {
            int sum = 0;
            double cx = MarkerLibrary.Fixed(_lib.ProbeCentersX[i]), cy = MarkerLibrary.Fixed(_lib.ProbeCentersY[i]);
            for (int k = 0; k < MarkerLibrary.NumProbePoints; k++)
            {
                double x = cx + MarkerLibrary.Fixed(_lib.ProbePointsX[k]);
                double y = cy + MarkerLibrary.Fixed(_lib.ProbePointsY[k]);
                var p = h.Apply(new Vec2(x, y));
                int px = (int)Math.Floor(p.X + 0.5), py = (int)Math.Ceiling(p.Y - 0.5);
                px = Math.Clamp(px, 0, img.Width - 1); py = Math.Clamp(py, 0, img.Height - 1);
                sum += img[px, py];
            }
            values[i] = (byte)(sum / MarkerLibrary.NumProbePoints);
        }
        return values;
    }

    /// <summary>
    /// The engine's brightness gate before classification: the 12 dark probes on the border must be darker
    /// than the 12 bright probes inside it (<c>VisionMarker::ComputeThreshold</c> region). Returns the
    /// mean dark and bright values so a caller can report why a quad was dropped.
    /// </summary>
    public (double Dark, double Bright) ThresholdProbes(GrayImage img, Homography h)
    {
        var _lib = Library;
        double dark = 0, bright = 0;
        for (int i = 0; i < MarkerLibrary.NumThresholdProbes; i++)
        {
            dark += Sample(img, h, MarkerLibrary.Fixed(_lib.ThresholdDarkX[i]), MarkerLibrary.Fixed(_lib.ThresholdDarkY[i]));
            bright += Sample(img, h, MarkerLibrary.Fixed(_lib.ThresholdBrightX[i]), MarkerLibrary.Fixed(_lib.ThresholdBrightY[i]));
        }
        return (dark / MarkerLibrary.NumThresholdProbes, bright / MarkerLibrary.NumThresholdProbes);
    }

    private static int Sample(GrayImage img, Homography h, double x, double y)
    {
        var p = h.Apply(new Vec2(x, y));
        int px = Math.Clamp((int)Math.Floor(p.X + 0.5), 0, img.Width - 1), py = Math.Clamp((int)Math.Ceiling(p.Y - 0.5), 0, img.Height - 1);
        return img[px, py];
    }

    /// <summary><c>NearestNeighborLibrary::GetNearestNeighbor</c>.</summary>
    public Match GetNearestNeighbor(ReadOnlySpan<byte> probeValues, int threshold = MarkerLibrary.MatchThreshold)
    {
        var _lib = Library;
        // cv::normalize(query, query, 0, 255, NORM_MINMAX)
        int min = 255, max = 0;
        foreach (var v in probeValues) { if (v < min) min = v; if (v > max) max = v; }
        var q = new byte[MarkerLibrary.NumProbes];
        double scale = max > min ? 255.0 / (max - min) : 0;
        for (int i = 0; i < q.Length; i++) q[i] = (byte)Math.Round((probeValues[i] - min) * scale);

        int best = int.MaxValue, second = int.MaxValue, bestRow = -1, secondRow = -1;
        for (int r = 0; r < _lib.NumImages; r++)
        {
            var row = _lib.Image(r);
            int sum = 0;
            for (int i = 0; i < MarkerLibrary.NumProbes; i++) sum += Math.Abs(q[i] - row[i]);
            int d = sum / MarkerLibrary.NumProbes;
            if (d < best) { second = best; secondRow = bestRow; best = d; bestRow = r; }
            else if (d < second) { second = d; secondRow = r; }
        }
        if (bestRow < 0) return new Match(MarkerLibrary.InvalidLabel, int.MaxValue, -1, true, "empty library");
        int label = _lib.Labels[bestRow];
        if (secondRow >= 0 && _lib.Labels[secondRow] != label)
        {
            // disambiguate over the probes where the two candidates disagree strongly
            var a = _lib.Image(bestRow); var b = _lib.Image(secondRow);
            int sum = 0, count = 0;
            for (int i = 0; i < MarkerLibrary.NumProbes; i++)
                if (Math.Abs(a[i] - b[i]) > threshold) { sum += Math.Abs(q[i] - a[i]); count++; }
            if (count > 0)
            {
                double avg = (double)sum / count;
                if (avg >= 1.25 * threshold) return new Match(label, best, bestRow, true, $"ambiguous with label {_lib.Labels[secondRow]} (avg {avg:F1} over {count} probes)");
            }
        }
        return new Match(label, best, bestRow, false, "");
    }

    /// <summary>
    /// <c>VisionMarker::Extract</c>: decode the quad with corners TL, BL, TR, BR (pixels). Returns null with a
    /// reason when the quad is not a known marker.
    /// </summary>
    public ObservedMarker? Extract(GrayImage img, Vec2[] quadCorners, uint timestamp, out string reason)
    {
        if (_lib is null) { reason = "no marker library on this machine (extract it from libcozmoEngine.so; see VISION.md)"; return null; }
        var h = Homography.FromUnitSquare(quadCorners);
        var (dark, bright) = ThresholdProbes(img, h);
        if (dark >= bright) { reason = $"border not darker than interior ({dark:F0} vs {bright:F0})"; return null; }
        var probes = GetProbeValues(img, h);
        var m = GetNearestNeighbor(probes);
        if (m.Rejected) { reason = m.Reason; return null; }
        if (m.Distance >= MarkerLibrary.MatchThreshold) { reason = $"distance {m.Distance} >= {MarkerLibrary.MatchThreshold}"; return null; }
        if (m.Label == MarkerLibrary.InvalidLabel || m.Label == MarkerLibrary.InvalidLabel + 1) { reason = $"label {m.Label} is INVALID"; return null; }
        var code = _lib.LabelToCode[m.Label];
        var corners = new Vec2[4];
        for (int i = 0; i < 4; i++) corners[i] = quadCorners[_lib.CornerReorder[m.Label * 4 + i]];
        reason = "";
        return new ObservedMarker(code, corners, timestamp, m.Distance, _lib.OrientationDeg[m.Label]);
    }
}
