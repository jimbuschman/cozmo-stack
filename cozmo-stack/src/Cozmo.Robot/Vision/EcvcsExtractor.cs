namespace Cozmo.Robot.Vision;

/// <summary>
/// One run of 1s in a mask row: the engine's <c>ConnectedComponentSegment&lt;u16&gt;</c>, 8 bytes
/// <c>{ s16 start, s16 end, u16 row, u16 componentId }</c> (G1.14d).
/// </summary>
public readonly record struct ConnectedComponentSegment(short Start, short End, ushort Row, ushort Id);

/// <summary>
/// A component assembled from the extractor's runs: the sum of its run lengths and the bounding box the
/// later filters (<c>InvalidateSmallOrLargeComponents</c>, the fill/hollow tests) and the boundary trace need.
/// </summary>
public sealed record EcvcsComponent(
    int Id, int Pixels, int MinX, int MinY, int MaxX, int MaxY, IReadOnlyList<ConnectedComponentSegment> Segments);

/// <summary>
/// The live fiducial front end's component extractor, <c>ExtractComponentsViaCharacteristicScale</c>
/// 0x0088F8BC, selected by <c>MarkerDetector::Parameters</c> byte 0 = 1 (the shipped value,
/// <c>Parameters::Initialize</c> 0x008752FC/0x00875304 writes <c>0x0101</c> at +0).
///
/// It is a <b>multi-window box-filter extractor</b>, not the pyramid/binomial path this stack previously
/// implemented: it builds a bank of box means over a scrolling integral image, binarises each row with the
/// largest-adjacent-jump selection (<c>ecvcs_computeBinaryImage_numFilters3</c> 0x0088F61A at the shipped
/// three-window bank), and folds each mask row into the per-row <c>ConnectedComponents</c> DP
/// (<c>Extract2dComponents_PerRow_*</c>).
///
/// Every step traces to the approved inventory (gap pass 1 G1, itself confirmed against the evidence file
/// <c>re-analysis/evidence/m11/ecvcs-extractor.md</c> instruction for instruction).
///
/// Two parts of the engine path are not settled by the inventory and are named rather than guessed:
/// <list type="bullet">
/// <item>the T1/T2 box-reciprocal tables (0xC98958/0xC98A5C) are recovered only at the shipped half-widths
/// 4, 8 and 16, so a window bank that needs another half-width is refused;</item>
/// <item>the meaning of <c>Extract1dComponents</c>' threshold <c>b</c> (0x0089704A, open Q2): shipped
/// <c>b = 0</c>, at which the engine records every run; any other value is refused.</item>
/// </list>
/// </summary>
// fidelity: M11-032
public static class EcvcsExtractor
{
    // ------------------------------------------------------------------ window bank (G1.1b, G1.2c)

    /// <summary>
    /// The window bank the caller builds (G1.1b): size <c>params+0x04 + 2</c>, and
    /// <c>list[i] = params+0x08 &lt;&lt; i</c>. Shipped <c>(1, 4)</c> gives <c>{4, 8, 16}</c> - the
    /// half-widths of a 9, 17 and 33 pixel box.
    /// </summary>
    // fidelity: M11-032
    public static int[] WindowBank(QuadDetectorParameters parameters)
    {
        int count = parameters.WindowCountMinusTwo + 2;
        // G1.2b: the scale count must be in [1, 0x40]; anything else fails the function's validity gate.
        if (count < 1 || count > 0x40)
            throw new ArgumentOutOfRangeException(nameof(parameters), count,
                "the scale count must be in [1, 0x40] (0x0088F928)");
        var list = new int[count];
        for (int i = 0; i < count; i++) list[i] = parameters.WindowBase << i;
        return list;
    }

    /// <summary>
    /// <c>maxScale = max(list[i] + 1)</c> starting from -1 (G1.2c), which is also the integral image's
    /// <c>numBorderPixels</c> (G1.2d): 17 at the shipped windows.
    /// </summary>
    // fidelity: M11-032
    public static int MaxScale(int[] windows)
    {
        int max = -1;
        foreach (int w in windows) max = Math.Max(max, w);
        return max + 1;
    }

    // ------------------------------------------------------------------ box-mean filter (G1.5, G1.7)

    /// <summary>
    /// The fixed-point reciprocal of a <c>(2s+1) x (2s+1)</c> box, from the two tables at
    /// <c>0xC98958</c> (multiplier) and <c>0xC98A5C</c> (shift): <c>T1[s] = round(2^T2[s] / (2s+1)^2)</c>
    /// (F2). The inventory recovers the entries at the shipped half-widths only - 4: 101&gt;&gt;13,
    /// 8: 227&gt;&gt;16, 16: 241&gt;&gt;18. Any other half-width is a named refusal rather than a guessed
    /// reciprocal.
    /// </summary>
    // fidelity: M11-032
    public static (int Mult, int Shift) FilterCoefficients(int halfWidth) => halfWidth switch
    {
        4 => (101, 13),
        8 => (227, 16),
        16 => (241, 18),
        _ => throw new NotSupportedException(
            $"the ecvcs T1/T2 tables (0xC98958/0xC98A5C) are recovered only at the shipped half-widths 4, 8 and 16; " +
            $"half-width {halfWidth} is a RECOVERABLE_GAP (M11-032)"),
    };

    /// <summary>
    /// The full prefix-sum model of the padded image, kept as a test/reference helper. The production path
    /// uses <see cref="ScrollingIntegralImage"/> (the engine's rolling buffer). This reference computes a
    /// prefix-sum <c>(padded rows + 1) x (padded cols + 1)</c> grid with a zero row and column at index 0:
    /// <c>ii[(y+1)*stride + x+1]</c> is the sum of the padded image at or above and left of <c>(y, x)</c>.
    /// </summary>
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
    /// <c>FilterRow_innerLoop&lt;u8&gt;</c> 0x008A5230 (G1.7) on the full prefix-sum reference:
    /// <c>out = (bottomRight - topRight - bottomLeft + topLeft) * mult &gt;&gt; shift</c>
    /// (arithmetic shift, low byte stored). The <c>mult == 1 &amp;&amp; shift == 0</c> case is the identity,
    /// as the engine's branch does.
    /// </summary>
    public static byte BoxMeanAt(int[] integral, int stride, int border, int row, int col, int halfWidth, int mult, int shift)
    {
        int left = col + border - halfWidth, right = col + border + halfWidth;
        int top = row + border - halfWidth, bottom = row + border + halfWidth;
        int sum = integral[(bottom + 1) * stride + (right + 1)]
                - integral[top * stride + (right + 1)]
                - integral[(bottom + 1) * stride + left]
                + integral[top * stride + left];
        int v = (mult == 1 && shift == 0) ? sum : (sum * mult) >> shift;
        return unchecked((byte)v);
    }

    /// <summary>One whole filtered row for a single window on the full prefix-sum reference.</summary>
    public static byte[] BoxMeanRow(int[] integral, int stride, int border, int row, int width, int halfWidth, int mult, int shift)
    {
        var outp = new byte[width];
        for (int x = 0; x < width; x++)
            outp[x] = BoxMeanAt(integral, stride, border, row, x, halfWidth, mult, shift);
        return outp;
    }

    /// <summary>
    /// <c>ScrollingIntegralImage_u8_s32</c> (ctor 0x008A4D34): the engine's rolling prefix-sum buffer of
    /// the padded image. The buffer is <c>rows x (cols + 2*border)</c>; <see cref="ScrollDown"/> 0x008A4DB0
    /// fills it (top border replicated, then the source rows, then the bottom border on a later call), and
    /// <see cref="GetMaxRow"/> 0x008A51E0 reports the last source row that can be filtered before another
    /// scroll. The engine's row bookkeeping (G1.10/G1.11/G1.12/G1.13) is transcribed, not modelled.
    /// </summary>
    // fidelity: M11-032
    public sealed class ScrollingIntegralImage
    {
        private readonly int[] _data;

        public ScrollingIntegralImage(int rows, int cols, int border)
        {
            Rows = rows;
            ImageWidth = cols;
            Border = border;
            PaddedColumns = cols + 2 * border;
            Stride = PaddedColumns;
            _data = new int[rows * Stride];
            RowOffset = -border;
            LastSourceRow = -1;
        }

        public int Rows { get; }
        public int ImageWidth { get; }
        public int Border { get; }
        public int PaddedColumns { get; }
        public int Stride { get; }
        /// <summary>The SII's <c>[+0x1c]</c> (rowOffset): starts at <c>-border</c> and grows by each scroll.</summary>
        public int RowOffset { get; private set; }
        /// <summary>The SII's <c>[+0x18]</c>: the last source row filled.</summary>
        public int LastSourceRow { get; private set; }
        public int[] Data => _data;

        /// <summary>
        /// <c>PadImageRow</c> 0x008A5058: <c>src = image.data + image.stride*row</c>; the first
        /// <c>border</c> bytes are <c>src[0]</c>, the row is copied, and the last <c>border</c> bytes are
        /// <c>src[cols-1]</c>. The engine copies <c>floor(cols/4)</c> words and fills the right border at
        /// <c>border+cols</c>; the live camera width is a multiple of 4, so the tail gap (G1.13) does not
        /// arise here.
        /// </summary>
        private void PadImageRow(GrayImage image, int row, byte[] dest)
        {
            int src = row * image.Width;
            byte first = image.Pixels[src];
            byte last = image.Pixels[src + image.Width - 1];
            for (int i = 0; i < Border; i++) dest[i] = first;
            Array.Copy(image.Pixels, src, dest, Border, image.Width);
            for (int i = 0; i < Border; i++) dest[Border + image.Width + i] = last;
        }

        /// <summary>0x008A4DB0: the initial fill (when <paramref name="arg2"/> == rows) or the scroll fill.</summary>
        public void ScrollDown(GrayImage image, int arg2)
        {
            var temp = new byte[PaddedColumns];
            if (Rows == arg2) InitialFill(image, arg2, temp);
            else ScrollFill(image, arg2, temp);
        }

        /// <summary>G1.11b: prefix-sum row 0, replicate it for the top border, then fill source rows 1.. .</summary>
        private void InitialFill(GrayImage image, int arg2, byte[] temp)
        {
            PadImageRow(image, 0, temp);
            int run = 0;
            for (int i = 0; i < PaddedColumns; i++) { run += temp[i]; _data[i] = run; }

            int fp = 1;
            for (int k = 0; k < Border; k++)
            {
                run = 0;
                int prev = (fp - 1) * Stride, cur = fp * Stride;
                for (int i = 0; i < PaddedColumns; i++) { run += temp[i]; _data[cur + i] = _data[prev + i] + run; }
                fp++;
            }

            arg2 -= Border; arg2 -= 1;
            int r7 = 0;
            if (r7 < Rows && arg2 >= 1)
            {
                while (true)
                {
                    r7++;
                    if (r7 >= Rows) { r7 = Rows - 1; break; }
                    PadImageRow(image, r7, temp);
                    run = 0;
                    int prev = (fp - 1) * Stride, cur = fp * Stride;
                    for (int i = 0; i < PaddedColumns; i++) { run += temp[i]; _data[cur + i] = _data[prev + i] + run; }
                    fp++;
                    arg2--;
                    if (arg2 <= 0) break;
                }
            }
            LastSourceRow = r7;
        }

        /// <summary>G1.11c: shift the kept rows up, advance the rowOffset, then extend with the source rows and the bottom border.</summary>
        private void ScrollFill(GrayImage image, int arg2, byte[] temp)
        {
            int r7 = LastSourceRow;
            int keep = Rows - arg2;
            if (keep >= 1)
                for (int r5 = 0; r5 < keep; r5++)
                    Array.Copy(_data, (arg2 + r5) * Stride, _data, r5 * Stride, Stride);
            RowOffset += arg2;

            int fp = keep;
            if (r7 < Rows && arg2 >= 1)
            {
                while (true)
                {
                    r7++;
                    if (r7 >= Rows) { r7 = Rows - 1; break; }
                    PadImageRow(image, r7, temp);
                    int run = 0;
                    int prev = (fp - 1) * Stride, cur = fp * Stride;
                    for (int i = 0; i < PaddedColumns; i++) { run += temp[i]; _data[cur + i] = _data[prev + i] + run; }
                    fp++;
                    arg2--;
                    if (arg2 <= 0) break;
                }
            }
            if (arg2 >= 1)
            {
                PadImageRow(image, r7, temp);       // r7 == Rows-1: the bottom border replicates the last source row
                while (arg2 > 0)
                {
                    int run = 0;
                    int prev = (fp - 1) * Stride, cur = fp * Stride;
                    for (int i = 0; i < PaddedColumns; i++) { run += temp[i]; _data[cur + i] = _data[prev + i] + run; }
                    fp++;
                    arg2--;
                }
            }
            LastSourceRow = r7;
        }

        /// <summary>
        /// <c>get_maxRow</c> 0x008A51E0: <c>L - arg</c>, or <c>H-1 + O - arg</c> when <c>(H-1) &gt; (L-O)</c>.
        /// </summary>
        public int GetMaxRow(int arg)
        {
            int l = LastSourceRow, o = RowOffset;
            int r0 = l - arg;
            int extra = (Rows - 1) - (l - o);
            if (extra > 0) r0 += extra;
            return r0;
        }

        /// <summary>
        /// <c>FilterRow&lt;u8&gt;</c> 0x0088F538 / <c>FilterRow_innerLoop&lt;u8&gt;</c> 0x008A5230 (G1.6/G1.7):
        /// the box at source <paramref name="row"/>, half-width <paramref name="halfWidth"/>, read from the
        /// rolling buffer at <c>row - rowOffset + k</c> and <c>border + k</c>, scaled by mult/shift.
        /// </summary>
        public byte FilterRowAt(int row, int col, int halfWidth, int mult, int shift)
        {
            int top = row - RowOffset - halfWidth - 1;      // top-1 (exclusive)
            int bottom = row - RowOffset + halfWidth;
            int left = Border - halfWidth - 1 + col;
            int right = Border + halfWidth + col;
            long sum = (long)_data[bottom * Stride + right]
                     - _data[top * Stride + right]
                     - _data[bottom * Stride + left]
                     + _data[top * Stride + left];
            int v = (mult == 1 && shift == 0) ? (int)sum : (int)(((int)sum * mult) >> shift);
            return unchecked((byte)v);
        }
    }

    // ------------------------------------------------------------------ binarise (G1.8, G1.9, B4)

    /// <summary>
    /// <c>ecvcs_computeBinaryImage_numFilters3</c> 0x0088F61A (G1.8), the callback the selector picks when
    /// the scale count is 3 (G1.3). Per pixel it takes the filtered value of the window <b>after</b> the
    /// larger jump between adjacent windows - <c>v = (|f1-f0| &gt; |f2-f1|) ? f1 : f2</c>, a strict
    /// <c>&gt;</c> so an exact tie keeps <c>f2</c> - and marks the pixel dark when
    /// <c>((v * mult) &gt;&gt; 16) &gt; pixel</c>.
    /// </summary>
    // fidelity: M11-032
    public static byte NumFilters3(byte f0, byte f1, byte f2, byte pixel, int mult)
    {
        int v = Math.Abs(f1 - f0) > Math.Abs(f2 - f1) ? f1 : f2;
        return (byte)(((v * mult) >> 16) > pixel ? 1 : 0);
    }

    /// <summary>
    /// <c>ecvcs_computeBinaryImage_numFilters5</c> 0x0088F684 (G1.9): the largest-adjacent-gap selection
    /// over five windows. The selected value is <c>f_{j+1}</c> for the first gap index <c>j</c> that
    /// reaches the maximum, with the tie order g10, then g21, then g32, else f4, and the binarise is
    /// <c>((v * mult) &gt;&gt; 16) &gt; pixel</c>.
    /// </summary>
    // fidelity: M11-032
    public static byte NumFilters5(byte f0, byte f1, byte f2, byte f3, byte f4, byte pixel, int mult)
    {
        int g10 = Math.Abs(f1 - f0), g21 = Math.Abs(f2 - f1), g32 = Math.Abs(f3 - f2), g43 = Math.Abs(f4 - f3);
        int v;
        if (g10 >= g21 && g10 >= g32 && g10 >= g43) v = f1;
        else if (g21 >= g32 && g21 >= g43) v = f2;
        else if (g32 >= g43) v = f3;
        else v = f4;
        return (byte)(((v * mult) >> 16) > pixel ? 1 : 0);
    }

    /// <summary>
    /// <c>ecvcs_computeBinaryImage_numFilters5_thresholdMultiplier1</c> 0x0088F75E (G1.9): the same
    /// selection over five windows, but the binarise has no multiply - <c>v &gt; pixel</c>. Selected only
    /// when the multiplier is 0x10000 (G1.3).
    /// </summary>
    // fidelity: M11-032
    public static byte NumFilters5ThresholdMultiplier1(byte f0, byte f1, byte f2, byte f3, byte f4, byte pixel)
    {
        int g10 = Math.Abs(f1 - f0), g21 = Math.Abs(f2 - f1), g32 = Math.Abs(f3 - f2), g43 = Math.Abs(f4 - f3);
        int v;
        if (g10 >= g21 && g10 >= g32 && g10 >= g43) v = f1;
        else if (g21 >= g32 && g21 >= g43) v = f2;
        else if (g32 >= g43) v = f3;
        else v = f4;
        return (byte)(v > pixel ? 1 : 0);
    }

    /// <summary>
    /// <c>ecvcs_computeBinaryImage</c> (G1.3/B4): the generic callback used when the scale count is neither
    /// 3 nor 5. Same largest-adjacent-gap selection over the N filtered rows, binarised by
    /// <c>((v * mult) &gt;&gt; 16) &gt; pixel</c>. Not exercised at the shipped settings.
    /// </summary>
    // fidelity: M11-032
    public static byte Generic(byte[] filtered, int index, byte pixel, int mult)
    {
        int best = 0, v = filtered[filtered.Length - 1];
        for (int j = 0; j + 1 < filtered.Length; j++)
        {
            int gap = Math.Abs(filtered[j + 1] - filtered[j]);
            if (gap > best) { best = gap; v = filtered[j + 1]; }
        }
        return (byte)(((v * mult) >> 16) > pixel ? 1 : 0);
    }

    /// <summary>
    /// The live extractor's binary "dark" mask end to end (G1.2..G1.9): the window bank, the rolling
    /// integral image, one box-mean row per window, the callback the selector picks (G1.3) and the row
    /// loop's per-row binarise and scroll (G1.4).
    /// </summary>
    // fidelity: M11-032
    public static GrayImage BinaryMask(GrayImage image, QuadDetectorParameters parameters)
    {
        int[] windows = WindowBank(parameters);
        int n = windows.Length;
        var coeffs = new (int Mult, int Shift)[n];
        for (int i = 0; i < n; i++) coeffs[i] = FilterCoefficients(windows[i]);   // refuses an unrecovered half-width
        int border = MaxScale(windows);

        int w = image.Width, h = image.Height;
        var sii = new ScrollingIntegralImage(h, w, border);
        sii.ScrollDown(image, h);                       // 0x0088F990: the initial fill

        var mask = new GrayImage(w, h);
        var rows = new byte[n][];
        for (int i = 0; i < n; i++) rows[i] = new byte[w];
        int mult = parameters.DarkThresholdQ16;

        for (int row = 0; row < h; row++)
        {
            for (int i = 0; i < n; i++)
                for (int x = 0; x < w; x++)
                    rows[i][x] = sii.FilterRowAt(row, x, windows[i], coeffs[i].Mult, coeffs[i].Shift);

            int baseIndex = row * w;
            for (int x = 0; x < w; x++)
            {
                int i = baseIndex + x;
                byte v = n switch
                {
                    3 => NumFilters3(rows[0][x], rows[1][x], rows[2][x], image.Pixels[i], mult),
                    5 => mult == 0x10000
                            ? NumFilters5ThresholdMultiplier1(rows[0][x], rows[1][x], rows[2][x], rows[3][x], rows[4][x], image.Pixels[i])
                            : NumFilters5(rows[0][x], rows[1][x], rows[2][x], rows[3][x], rows[4][x], image.Pixels[i], mult),
                    _ => Generic(Column(rows, x), i, image.Pixels[i], mult),
                };
                mask.Pixels[i] = v;
            }

            // 0x0088FC42..0x0088FC5A: if get_maxRow(maxScale-1) <= row, scroll by rows - 2*maxScale.
            if (sii.GetMaxRow(border - 1) <= row)
                sii.ScrollDown(image, h - 2 * border);
        }
        return mask;

        static byte[] Column(byte[][] rows, int index)
        {
            var col = new byte[rows.Length];
            for (int r = 0; r < rows.Length; r++) col[r] = rows[r][index];
            return col;
        }
    }

    // ------------------------------------------------------------------ per-row DP (G1.14)

    /// <summary>
    /// <c>Extract1dComponents</c> (u16, 0x00896FDC, G1.14b): one segment per run of 1s in a mask row,
    /// <c>{ start, end, row = 0xFFFF, id = 0xFFFF }</c>. A run shorter than <paramref name="a"/> is dropped.
    ///
    /// <b>RECOVERABLE_GAP:</b> <paramref name="b"/>'s meaning was not recovered (open Q2). The shipped value
    /// is <c>b = 0</c>, at which the engine records every run; any other value is a named refusal.
    /// </summary>
    // fidelity: M11-032
    public static List<ConnectedComponentSegment> ExtractRunSegments(ReadOnlySpan<byte> row, int width, ushort rowIndex, int a, int b)
    {
        if (b != 0)
            throw new NotSupportedException(
                $"Extract1dComponents threshold b (0x0089704A) is a RECOVERABLE_GAP (M11-032, open Q2); only the " +
                $"shipped b = 0 is modelled, and b = {b} is refused rather than guessed");

        var segments = new List<ConnectedComponentSegment>();
        int x = 0;
        while (x < width)
        {
            if (row[x] == 0) { x++; continue; }
            int start = x;
            while (x < width && row[x] != 0) x++;
            int end = x - 1;
            if (end - start + 1 < a) continue;
            segments.Add(new ConnectedComponentSegment((short)start, (short)end, 0xFFFF, 0xFFFF));
        }
        return segments;
    }

    /// <summary>
    /// <c>Extract2dComponents_PerRow_Initialize</c> (G1.14a/D3) + <c>NextRow</c> (G1.14c/D5) +
    /// <c>Finalize</c> (G1.14d/D6): scan the mask row by row, merge each run with the previous row's
    /// overlapping runs by union-find (min, no path compression), then resolve every id to its class root.
    ///
    /// The overlap predicate is run overlap - <c>prev.Start &lt;= run.End &amp;&amp; run.Start &lt;= prev.End</c>.
    /// A run that touches no previous run gets a new id. The returned list is the engine's output
    /// accumulator, and <paramref name="maxId"/> is the resolved maximum id.
    /// </summary>
    // fidelity: M11-032
    public static List<ConnectedComponentSegment> ExtractComponents(GrayImage mask, int a, int b, out int maxId)
    {
        int w = mask.Width, h = mask.Height;
        var output = new List<ConnectedComponentSegment>();
        // D3: the component counter starts at 0 and NextRow pre-increments it (0x008940AE: ldrh r0,[r5,#0x10c];
        // adds r0,#1; strh r0,[r5,#0x10c]), so real ids start at 1 and 0 is the invalid sentinel that
        // CompressConnectedComponentSegmentIds and the hollow test use. Index 0 of the parent array is unused.
        var parent = new List<ushort> { 0 };
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

    /// <summary>Union-find find: walk the parent chain; no path compression (G1.14c).</summary>
    private static int Find(List<ushort> parent, int id)
    {
        while (parent[id] != id) id = parent[id];
        return id;
    }

    /// <summary>Union-find union by minimum root, the engine's plain "set the larger to the smaller" (G1.14c).</summary>
    private static void Union(List<ushort> parent, int a, int b)
    {
        int ra = Find(parent, a), rb = Find(parent, b);
        if (ra == rb) return;
        if (ra < rb) parent[rb] = (ushort)ra;
        else parent[ra] = (ushort)rb;
    }

    /// <summary>
    /// <c>Finalize</c> (G1.14d/D6): resolve the parent array to its fixed point (bounded at 0x3E6 passes),
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
    /// <c>InvalidateFilledCenterComponents_hollowRows</c> (u16 body 0x0089614C, C1.5): for each component,
    /// <c>ratio = merged[id] / size[id]</c>, where <c>size[id]</c> is its pixel count and <c>merged[id]</c>
    /// is the sum over rows of the largest inter-run gap on that row. If <c>ratio &lt; hollowFraction</c>
    /// (the parameters' +0x24 = 1.0) the component is invalidated: every one of its segments gets
    /// <c>componentId = 0</c>.
    /// </summary>
    // fidelity: M11-026
    public static void InvalidateFilledCenterComponents(List<ConnectedComponentSegment> segments, double hollowFraction)
    {
        var size = new Dictionary<int, int>();
        var byIdRow = new Dictionary<(int Id, int Row), List<ConnectedComponentSegment>>();
        foreach (var s in segments)
        {
            size[s.Id] = size.GetValueOrDefault(s.Id) + (s.End - s.Start + 1);
            var key = (s.Id, s.Row);
            if (!byIdRow.TryGetValue(key, out var list)) byIdRow[key] = list = new List<ConnectedComponentSegment>();
            list.Add(s);
        }

        var merged = new Dictionary<int, int>();
        foreach (var kv in byIdRow)
        {
            var runs = kv.Value;
            runs.Sort((a, b) => a.Start.CompareTo(b.Start));
            int gap = 0;
            for (int i = 1; i < runs.Count; i++)
            {
                int g = runs[i].Start - runs[i - 1].End - 1;
                if (g > gap) gap = g;
            }
            merged[kv.Key.Id] = merged.GetValueOrDefault(kv.Key.Id) + gap;
        }

        var invalid = new HashSet<int>();
        foreach (var id in size.Keys)
        {
            if (id == 0) continue;
            double ratio = size[id] == 0 ? 0.0 : (double)merged.GetValueOrDefault(id) / size[id];
            if (ratio < hollowFraction) invalid.Add(id);
        }
        for (int i = 0; i < segments.Count; i++)
            if (invalid.Contains(segments[i].Id))
                segments[i] = segments[i] with { Id = 0 };
    }

    /// <summary>
    /// <c>CompressConnectedComponentSegmentIds</c> (0x00894A7C..0x00894B82, C1.5): <c>lookup[0] = 0</c>,
    /// then every used id in ascending order is renumbered <c>1..K</c> and each segment's id replaced.
    /// Returns the recomputed maximum id. The ascending order is observable to the boundary trace and the
    /// sort 0x00898DD0.
    /// </summary>
    // fidelity: M11-026
    public static int CompressIds(List<ConnectedComponentSegment> segments)
    {
        var used = new SortedSet<int>();
        foreach (var s in segments) if (s.Id != 0) used.Add(s.Id);
        var lookup = new Dictionary<int, int> { [0] = 0 };
        int k = 1;
        foreach (var id in used) lookup[id] = k++;
        for (int i = 0; i < segments.Count; i++)
            segments[i] = segments[i] with { Id = (ushort)lookup[segments[i].Id] };
        return k - 1;
    }

    /// <summary>
    /// Groups the resolved segment list into components and computes each one's pixel count and bounding
    /// box, the shape the later filters and the corner stage consume. Invalidated components (id 0) are
    /// dropped. Components are returned in ascending id order, which is the compressed order.
    /// </summary>
    // fidelity: M11-032
    public static List<EcvcsComponent> ToComponents(IReadOnlyList<ConnectedComponentSegment> segments)
    {
        var byId = new Dictionary<int, List<ConnectedComponentSegment>>();
        foreach (var s in segments)
        {
            if (s.Id == 0) continue;
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