namespace Cozmo.Robot.Vision;

/// <summary>
/// One run of 1s in a mask row: the engine's <c>ConnectedComponentSegment&lt;u16&gt;</c>, 8 bytes
/// <c>{ s16 start, s16 end, u16 row, u16 componentId }</c> (M11-034, D7).
/// </summary>
public readonly record struct ConnectedComponentSegment(short Start, short End, ushort Row, ushort Id);

/// <summary>
/// A component assembled from the extractor's runs: the sum of its run lengths and the bounding box the
/// later filters (<c>InvalidateSmallOrLargeComponents</c>, the fill/hollow tests) need. The engine keeps the
/// segment list itself; this is the shape the existing <see cref="QuadDetector"/> stages consume.
/// </summary>
public sealed record EcvcsComponent(
    int Id, int Pixels, int MinX, int MinY, int MaxX, int MaxY, IReadOnlyList<ConnectedComponentSegment> Segments);

/// <summary>
/// The live fiducial front end's component extractor, <c>ExtractComponentsViaCharacteristicScale</c>
/// 0x0088F8BC, selected by <c>FiducialDetectionParameters</c> byte 0 = 1 (the shipped value).
///
/// It is a <b>multi-window box-filter extractor</b>, not the pyramid/binomial path this stack previously
/// implemented: it builds a bank of box means over a scrolling integral image, binarises each row with the
/// largest-adjacent-jump selection (<c>ecvcs_computeBinaryImage_numFilters3</c>), and folds each mask row
/// into the per-row <c>ConnectedComponents</c> DP.
///
/// Every constant and step below traces to <c>re-analysis/evidence/m11/ecvcs-extractor.md</c>. Two parts of
/// the path are explicitly RECOVERABLE_GAP and are <b>not</b> settled here; they are named at their code
/// and in the report:
/// <list type="bullet">
/// <item>the integral image's vertical row bookkeeping (<c>ScrollDown</c> 0x008A4DB0 / <c>get_maxRow</c>
/// 0x008A51E0, open Q1): this models the border by replicating <c>maxScale</c> pixels and rows at both
/// ends, as <c>PadImageRow</c> and the ctor's initial <c>ScrollDown</c> do, but the rolling-buffer offsets
/// were not read line by line;</item>
/// <item>the meaning of <c>Extract1dComponents</c>' threshold <c>b</c> (open Q2): shipped <c>b = 0</c>, and
/// this throws rather than guessing what any other value would do.</item>
/// </list>
/// </summary>
// fidelity: M11-032
public static class EcvcsExtractor
{
    /// <summary>An s16 clamp, the width of the segment's <c>start</c>/<c>end</c> fields (D7).</summary>
    public const int MaxCoordinate = short.MaxValue;

    // ------------------------------------------------------------------ window bank (M11-033, L2/L6)

    /// <summary>
    /// The window bank the caller builds (L2): size <c>params+0x04 + 2</c>, and
    /// <c>list[i] = params+0x08 &lt;&lt; i</c>. Shipped <c>(1, 4)</c> gives <c>{4, 8, 16}</c> - the
    /// half-widths of a 9, 17 and 33 pixel box.
    /// </summary>
    // fidelity: M11-032, M11-033
    public static int[] WindowBank(QuadDetectorParameters parameters)
    {
        int count = parameters.WindowCountMinusTwo + 2;
        // L5: the scale count must be in [1, 0x40]; anything else fails the function's validity gate.
        if (count < 1 || count > 0x40)
            throw new ArgumentOutOfRangeException(nameof(parameters), count,
                "the scale count must be in [1, 0x40] (0x0088F928)");
        var list = new int[count];
        for (int i = 0; i < count; i++) list[i] = parameters.WindowBase << i;
        return list;
    }

    /// <summary>
    /// <c>maxScale = max(list) + 1</c> (L6), which is also the integral image's <c>numBorderPixels</c>
    /// (L7): 17 at the shipped windows.
    /// </summary>
    public static int MaxScale(int[] windows)
    {
        int max = -1;
        foreach (int w in windows) max = Math.Max(max, w);
        return max + 1;
    }

    // ------------------------------------------------------------------ box-mean filter (M11-033, F1-F4)

    /// <summary>
    /// The fixed-point reciprocal of a <c>(2s+1) x (2s+1)</c> box, from the two tables at
    /// <c>0xC98958</c> (multiplier) and <c>0xC98A5C</c> (shift): <c>mult = round(2^shift / (2s+1)^2)</c>.
    ///
    /// The evidence recovers the entries at the three shipped half-widths only - 4: 101&gt;&gt;13 (F2, and the
    /// disassembly's "s=4: 101&gt;&gt;13"), 8: 227&gt;&gt;16, 16: 241&gt;&gt;18. The general table is not in the
    /// evidence, so any other half-width is a named refusal rather than a guessed reciprocal.
    /// </summary>
    // fidelity: M11-033
    public static (int Mult, int Shift) FilterCoefficients(int halfWidth) => halfWidth switch
    {
        4 => (101, 13),
        8 => (227, 16),
        16 => (241, 18),
        _ => throw new NotSupportedException(
            $"the ecvcs T1/T2 tables (0xC98958/0xC98A5C) are recovered only at the shipped half-widths 4, 8 and 16; " +
            $"half-width {halfWidth} is a RECOVERABLE_GAP (M11-033)"),
    };

    /// <summary>
    /// The scrolling integral image the box filter reads (L7/L8/D10): a prefix sum over the padded image,
    /// <c>(image rows + 2*maxScale)</c> by <c>(image cols + 2*maxScale)</c>, with the border pixels and rows
    /// replicated from the nearest edge pixel (<c>PadImageRow</c>).
    ///
    /// The returned array is a prefix-sum <c>(padded rows + 1) x (padded cols + 1)</c> grid with a zero row
    /// and column at index 0: <c>ii[y * stride + x]</c> is the sum of the padded image above and left of
    /// <c>(y, x)</c>. <paramref name="border"/> is the engine's <c>numBorderPixels</c> (17).
    /// <b>RECOVERABLE_GAP:</b> the engine scrolls a rolling buffer whose exact row offsets (<c>ScrollDown</c>,
    /// <c>get_maxRow</c>) were not recovered; a full prefix sum gives the same box sums for the replicated
    /// border and is the model used here.
    /// </summary>
    // fidelity: M11-033
    public static int[] IntegralImage(GrayImage image, int border, out int stride)
    {
        int w = image.Width, h = image.Height;
        int pw = w + 2 * border, ph = h + 2 * border;
        stride = pw + 1;
        var ii = new int[(ph + 1) * stride];
        for (int y = 0; y < ph; y++)
        {
            int sy = Math.Clamp(y - border, 0, h - 1);
            int rowBase = (y + 1) * stride;
            int prevBase = y * stride;
            int run = 0;
            for (int x = 0; x < pw; x++)
            {
                int sx = Math.Clamp(x - border, 0, w - 1);
                run += image.Pixels[sy * w + sx];
                ii[rowBase + x + 1] = ii[prevBase + x + 1] + run;
            }
        }
        return ii;
    }

    /// <summary>
    /// <c>FilterRow_innerLoop&lt;u8&gt;</c> 0x008A5230 (F3/F4): the four prefix-sum corners of the
    /// <c>(2s+1) x (2s+1)</c> box centred on <c>(row, col)</c> give its sum, scaled by the table's
    /// multiplier/shift into a box mean:
    /// <c>out = (bottomRight - topRight - bottomLeft + topLeft) * mult &gt;&gt; shift</c>.
    /// The <c>mult == 1 &amp;&amp; shift == 0</c> case is the identity, as the engine's branch does.
    /// </summary>
    // fidelity: M11-033
    public static byte BoxMeanAt(int[] integral, int stride, int border, int row, int col, int halfWidth, int mult, int shift)
    {
        int left = col + border - halfWidth, right = col + border + halfWidth;
        int top = row + border - halfWidth, bottom = row + border + halfWidth;
        long sum = (long)integral[(bottom + 1) * stride + (right + 1)]
                 - integral[top * stride + (right + 1)]
                 - integral[(bottom + 1) * stride + left]
                 + integral[top * stride + left];
        int v = shift == 0 ? (int)sum : (int)(((int)sum * mult) >> shift);
        return unchecked((byte)v);   // the engine stores the low byte (`strb.w`)
    }

    /// <summary>
    /// One whole filtered row for a single window, the output of <c>ecvcs_filterRows</c> (F1) /
    /// <c>FilterRow&lt;u8&gt;</c> (F3) for every column.
    /// </summary>
    // fidelity: M11-033
    public static byte[] BoxMeanRow(int[] integral, int stride, int border, int row, int width, int halfWidth, int mult, int shift)
    {
        var outp = new byte[width];
        for (int x = 0; x < width; x++)
            outp[x] = BoxMeanAt(integral, stride, border, row, x, halfWidth, mult, shift);
        return outp;
    }

    // ------------------------------------------------------------------ binarise (M11-033, B1 / L12)

    /// <summary>
    /// <c>ecvcs_computeBinaryImage_numFilters3</c> 0x0088F61A (B1), the callback the selector picks when
    /// the scale count is 3 (L12). Per pixel it takes the filtered value of the window <b>after</b> the
    /// larger jump between adjacent windows - <c>v = (|f1-f0| &gt; |f2-f1|) ? f1 : f2</c>, a strict
    /// <c>&gt;</c> so an exact tie keeps <c>f2</c> - and marks the pixel dark when
    /// <c>((v * mult) &gt;&gt; 16) &gt; pixel</c>.
    /// </summary>
    // fidelity: M11-033
    public static byte NumFilters3(byte f0, byte f1, byte f2, byte pixel, int mult)
    {
        int v = Math.Abs(f1 - f0) > Math.Abs(f2 - f1) ? f1 : f2;
        return (byte)(((v * mult) >> 16) > pixel ? 1 : 0);
    }

    /// <summary>
    /// The live extractor's binary "dark" mask end to end: the window bank, one box-mean row per window,
    /// and the <c>numFilters3</c> selection (M11-033).
    ///
    /// At a scale count other than 3 the engine selects a different callback (L12:
    /// <c>numFilters5</c>, <c>numFilters5_thresholdMultiplier1</c> or the generic
    /// <c>ecvcs_computeBinaryImage</c>); none of those is built here, so the count is a named refusal.
    /// </summary>
    // fidelity: M11-032, M11-033
    public static GrayImage BinaryMask(GrayImage image, QuadDetectorParameters parameters)
    {
        int[] windows = WindowBank(parameters);
        if (windows.Length != 3)
            throw new NotSupportedException(
                $"the ecvcs binarise callback selector (L12, 0x0088FA8A) picks numFilters3 only at a scale count of 3; " +
                $"this stack builds only that (shipped) callback, not numFilters5 / _thresholdMultiplier1 / the generic one " +
                $"(M11-032/M11-033)");

        int border = MaxScale(windows);
        var integral = IntegralImage(image, border, out int stride);

        int w = image.Width, h = image.Height;
        var rows = new byte[windows.Length][];
        for (int i = 0; i < windows.Length; i++)
        {
            var (mult, shift) = FilterCoefficients(windows[i]);
            rows[i] = new byte[w * h];
            for (int y = 0; y < h; y++)
                BoxMeanRow(integral, stride, border, y, w, windows[i], mult, shift).CopyTo(rows[i], y * w);
        }

        var mask = new GrayImage(w, h);
        for (int i = 0; i < mask.Pixels.Length; i++)
            mask.Pixels[i] = NumFilters3(rows[0][i], rows[1][i], rows[2][i], image.Pixels[i], parameters.DarkThresholdQ16);
        return mask;
    }

    // ------------------------------------------------------------------ per-row DP (M11-034)

    /// <summary>
    /// A short alias for the run-length floor (D4's argument <c>a</c>): a run is kept when its length is
    /// at least <paramref name="a"/>.
    /// </summary>
    public static bool RunIsLongEnough(int length, int a) => length >= a;

    /// <summary>
    /// <c>Extract1dComponents</c> (u16, 0x00896FD6, D4): one segment per run of 1s in a mask row,
    /// <c>{ start, end, row, id = 0xFFFF }</c>; the id is filled in by <see cref="ExtractComponents"/>.
    /// A run shorter than <paramref name="a"/> is dropped.
    ///
    /// <b>RECOVERABLE_GAP:</b> <paramref name="b"/>'s meaning was not recovered (open Q2). The shipped value
    /// is <c>b = 0</c>, at which the engine records every run; any other value is a named refusal.
    /// </summary>
    // fidelity: M11-034
    public static List<ConnectedComponentSegment> ExtractRunSegments(ReadOnlySpan<byte> row, int width, ushort rowIndex, int a, int b)
    {
        if (b != 0)
            throw new NotSupportedException(
                $"Extract1dComponents threshold b (0x0089704A) is a RECOVERABLE_GAP (M11-034, open Q2); only the " +
                $"shipped b = 0 is modelled, and b = {b} is refused rather than guessed");

        var segments = new List<ConnectedComponentSegment>();
        int x = 0;
        while (x < width)
        {
            if (row[x] == 0) { x++; continue; }
            int start = x;
            while (x < width && row[x] != 0) x++;
            int end = x - 1;
            if (!RunIsLongEnough(end - start + 1, a)) continue;
            segments.Add(new ConnectedComponentSegment((short)start, (short)end, rowIndex, 0xFFFF));
        }
        return segments;
    }

    /// <summary>
    /// <c>Extract2dComponents_PerRow_Initialize</c> (D3) + <c>NextRow</c> (D5) + <c>Finalize</c> (D6): scan
    /// the mask row by row, merge each run with the previous row's overlapping runs by union-find (min, no
    /// path compression), then resolve every id to its class root.
    ///
    /// The overlap predicate is run overlap - <c>prev.Start &lt;= run.End &amp;&amp; run.Start &lt;= prev.End</c>.
    /// A run that touches no previous run is a new component. The returned list is the engine's output
    /// accumulator, and <paramref name="maxId"/> is the resolved maximum id (D6).
    /// </summary>
    // fidelity: M11-034
    public static List<ConnectedComponentSegment> ExtractComponents(GrayImage mask, int a, int b, out int maxId)
    {
        int w = mask.Width, h = mask.Height;
        var output = new List<ConnectedComponentSegment>();
        var parent = new List<ushort>();          // the u16 parent/id array (D3, capacity params+0x40)
        var previous = new List<ConnectedComponentSegment>();

        for (int row = 0; row < h; row++)
        {
            var runs = ExtractRunSegments(mask.Pixels.AsSpan(row * w, w), w, (ushort)row, a, b);
            var current = new List<ConnectedComponentSegment>(runs.Count);
            foreach (var run in runs)
            {
                int id = -1;
                foreach (var prev in previous)
                {
                    if (prev.Start > run.End || run.Start > prev.End) continue;   // no overlap
                    int root = Find(parent, prev.Id);
                    if (id < 0) id = root;
                    else Union(parent, id, root);
                }
                if (id < 0)
                {
                    id = parent.Count;
                    parent.Add((ushort)id);
                }
                var segment = run with { Id = (ushort)id, Row = (ushort)row };
                current.Add(segment);
                output.Add(segment);
            }
            previous = current;
        }

        maxId = Finalize(parent, output);
        return output;
    }

    /// <summary>Union-find find: walk the parent chain; no path compression (D5).</summary>
    private static int Find(List<ushort> parent, int id)
    {
        while (parent[id] != id) id = parent[id];
        return id;
    }

    /// <summary>Union-find union by minimum root, the engine's plain "set the larger to the smaller" (D5).</summary>
    private static void Union(List<ushort> parent, int a, int b)
    {
        int ra = Find(parent, a), rb = Find(parent, b);
        if (ra == rb) return;
        if (ra < rb) parent[rb] = (ushort)ra;
        else parent[ra] = (ushort)rb;
    }

    /// <summary>
    /// <c>Finalize</c> (D6): resolve the parent array to its fixed point (bounded at 0x3E6 passes),
    /// rewrite every segment's id with its class root, and return the maximum resolved id.
    /// </summary>
    private static int Finalize(List<ushort> parent, List<ConnectedComponentSegment> segments)
    {
        const int maxPasses = 0x3E6;
        for (int pass = 0; pass < maxPasses; pass++)
        {
            bool changed = false;
            for (int i = 0; i < parent.Count; i++)
            {
                ushort p = parent[i];
                if (parent[p] != p) { parent[i] = parent[p]; changed = true; }
            }
            if (!changed) break;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            segments[i] = s with { Id = parent[s.Id] };
        }

        int maxId = 0;
        foreach (var s in segments) if (s.Id > maxId) maxId = s.Id;
        return maxId;
    }

    /// <summary>
    /// Groups the resolved segment list into components and computes each one's pixel count and bounding
    /// box, the shape the later filters and the existing corner stage consume. The engine's
    /// <c>CompressConnectedComponentSegmentIds</c> (D8) renumbers surviving components to 1..N; its remap
    /// ordering tail was not recovered, so this keeps the resolved root ids and leaves any renumbering to
    /// the caller. Components are returned in ascending id order.
    /// </summary>
    // fidelity: M11-034
    public static List<EcvcsComponent> ToComponents(IReadOnlyList<ConnectedComponentSegment> segments)
    {
        var byId = new Dictionary<int, List<ConnectedComponentSegment>>();
        foreach (var s in segments)
        {
            if (!byId.TryGetValue(s.Id, out var list)) byId[s.Id] = list = new List<ConnectedComponentSegment>();
            list.Add(s);
        }

        var components = new List<EcvcsComponent>(byId.Count);
        foreach (var id in byId.Keys.OrderBy(k => k))
        {
            var segs = byId[id];
            int pixels = 0, minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            foreach (var s in segs)
            {
                pixels += s.End - s.Start + 1;
                if (s.Start < minX) minX = s.Start;
                if (s.End > maxX) maxX = s.End;
                if (s.Row < minY) minY = s.Row;
                if (s.Row > maxY) maxY = s.Row;
            }
            components.Add(new EcvcsComponent(id, pixels, minX, minY, maxX, maxY, segs));
        }
        return components;
    }
}