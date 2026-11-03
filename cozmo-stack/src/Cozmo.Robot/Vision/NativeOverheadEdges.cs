using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

// fidelity: M11-101
/// <summary>
/// <c>Anki::Cozmo::OverheadEdgesDetector::Detect</c> 0x006ABE34, grey path (0x006AC07A..0x006AD3A0), transcribed from the approved
/// inventory <c>re-analysis/research/20261103-visit-edge-detector-extraction.md</c> (rows cited inline) and proved bit for bit against the
/// engine's own Detect run under the emulator (<c>re-analysis/tools/emu/emu_overhead_edges.py</c>, NativeOverheadEdgesOracleTests).
/// All arithmetic is binary32 as the engine's; integer conversions are the engine's (truncation for the bounding rect, round to nearest
/// even for the mask polygon and the int16 response).
/// The lift gate is not here: the caller supplies it (<see cref="OverheadEdgesDetector.LiftIsOutOfTheWay"/>), and the oracle runs with a
/// null calibration, so the gate is outside the proof.
/// </summary>
internal static class NativeOverheadEdges
{
    /// <summary>The detector's +0xC and +0x10 as VisionSystem constructs it: 50.0f and 3 (0x006B011E..0x006B012E, row 1).</summary>
    internal static readonly float Threshold = BitConverter.UInt32BitsToSingle(0x42480000);
    internal const uint MinChainPoints = 3;

    /// <summary>Chain gap 5.0f (<c>vmov.f32 s0,#5.0</c>, 0x006AE1B0..0x006AE24C, row 27).</summary>
    internal static readonly float MaxChainGap = BitConverter.UInt32BitsToSingle(0x40A00000);

    /// <summary>
    /// The function-local static <c>Matx&lt;float,7,5&gt;</c> copied from 0xC8E0AC (row 13): rows 0 and 1 as dumped, rows 2..4 zero,
    /// row 5 = -row 1, row 6 = -row 0 (sign bit set).
    /// </summary>
    internal static readonly uint[] KernelBits =
    {
        0x3C89A027, 0x3D9A6B51, 0x3DFE5C92, 0x3D9A6B51, 0x3C89A027,
        0x3D1A6B51, 0x3E2CF41F, 0x3E8E8A72, 0x3E2CF41F, 0x3D1A6B51,
        0, 0, 0, 0, 0,
        0, 0, 0, 0, 0,
        0, 0, 0, 0, 0,
        0xBD1A6B51, 0xBE2CF41F, 0xBE8E8A72, 0xBE2CF41F, 0xBD1A6B51,
        0xBC89A027, 0xBD9A6B51, 0xBDFE5C92, 0xBD9A6B51, 0xBC89A027,
    };

    internal const int KernelRows = 7, KernelCols = 5, AnchorX = 2, AnchorY = 3;

    /// <summary>preprocess2DKernel keeps the non-zero taps in row-major order (row 15): 20 taps, (dy, dx, k).</summary>
    private static readonly (int Dy, int Dx, float K)[] Taps = BuildTaps();

    private static (int, int, float)[] BuildTaps()
    {
        var taps = new List<(int, int, float)>();
        for (int r = 0; r < KernelRows; r++)
            for (int c = 0; c < KernelCols; c++)
            {
                float k = BitConverter.UInt32BitsToSingle(KernelBits[r * KernelCols + c]);
                if (k != 0f) taps.Add((r - AnchorY, c - AnchorX, k));
            }
        return taps.ToArray();
    }

    /// <summary>Lift gate supplied by the caller: true lets the frame through (row 9). <paramref name="top"/> = rect.y and
    /// <paramref name="bottom"/> = rect.y + rect.h of the UNCLIPPED rect (row 10).</summary>
    internal delegate bool LiftGate(int top, int bottom);

    /// <summary>
    /// One call of Detect. Returns the engine's result (always 0 here: its only non-zero return, the lift-versus-camera pose-tree failure
    /// of row 9, cannot arise because the lift and camera poses share the frame's robot pose as their root, see the VisionSystem caller).
    /// <paramref name="frame"/> is the frame Detect appended, or null when it appended none (rows 5 and 9).
    /// </summary>
    internal static int Detect(GrayImage image, ReadOnlySpan<float> h, bool groundVisible, uint status, uint timestamp,
                               LiftGate? liftGate, out OverheadEdgeFrame? frame)
    {
        if (h.Length != 9) throw new ArgumentException("the ground-plane homography has nine elements");
        frame = null;
        int rows = image.Height, cols = image.Width;

        // row 4: 0x006AC086..0x006AC08C -> 0x006AC308..0x006AC368: an empty frame, flag 0, zero quad, no chains
        if (!groundVisible)
        {
            frame = new OverheadEdgeFrame(timestamp, false, new Vec2[4], Array.Empty<OverheadEdgePointChain>());
            return 0;
        }

        // row 5: 0x006AC090..0x006AC09C (status & 0x102) == 0x100, else no frame
        if ((status & 0x102u) != 0x100u) return 0;

        // row 8: GetImageQuad 0x004F78E0 then ClampQuad 0x004F7B74 (its return ignored here)
        var quad = NativeGroundPlaneProjection.Project(h, cols, rows, out _);

        // row 10: Rectangle<int>::InitFromPointContainer 0x006ABB38..0x006ABBD4, vcvt.s32.f32 (truncation), w,h = max-min
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var p in quad)
        {
            int x = TruncS32(p.X), y = TruncS32(p.Y);
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
        }
        int rx = minX, ry = minY, rw = maxX - minX, rh = maxY - minY;

        // row 9: the lift gate on the unclipped rect
        if (liftGate is not null && !liftGate(ry, unchecked(ry + rh))) return 0;

        // row 11: GetROI 0x006AE97C..0x006AE9A8, Intersect 0x00658EC0, half-open with (0,0,cols,rows); empty gives (0,0,0,0)
        {
            long x0 = Math.Max(rx, 0), y0 = Math.Max(ry, 0);
            long x1 = Math.Min((long)rx + rw, cols), y1 = Math.Min((long)ry + rh, rows);
            if (x1 <= x0 || y1 <= y0) { rx = ry = rw = rh = 0; }
            else { rx = (int)x0; ry = (int)y0; rw = (int)(x1 - x0); rh = (int)(y1 - y0); }
        }

        // rows 12-17: filter2D 8U -> 16S over the rect, reflect-101 from the parent image
        var response = new short[rows * cols];
        for (int y = ry; y < ry + rh; y++)
            for (int x = rx; x < rx + rw; x++)
                response[y * cols + x] = Response(image, x, y);

        // rows 18-19: the 0xFF mask with the polygon (corners 0,2,3,1, vcvtr.s32.f32) filled with 0, lineType 8, shift 0; then setTo(0, mask)
        var mask = new byte[rows * cols];
        Array.Fill(mask, (byte)0xFF);
        var poly = new List<(int X, int Y)>(4);
        foreach (int i in new[] { 0, 2, 3, 1 }) poly.Add((RoundS32(quad[i].X), RoundS32(quad[i].Y)));
        OpenCv310.FillConvexPoly(mask, rows, cols, poly, 0, 8, 0);
        for (int i = 0; i < mask.Length; i++) if (mask[i] != 0) response[i] = 0;

        // row 20: the cofactor inverse 0x004F7FB0
        var inv = Invert(h);

        // rows 21-27: columns of the clipped rect, rows bottom-up, strict |v| > threshold
        var chains = new List<OverheadEdgePointChain>();
        for (int x = rx; x <= rx + rw - 1; x++)
        {
            bool done = false;
            for (int row = ry + rh - 1; row >= ry; row--)
            {
                float v = response[row * cols + x];
                if (!(MathF.Abs(v) > Threshold)) continue;
                // row 25: a hit that projects is an edge point; one that does not falls into the clear branch (0x006AD218 bne 0x6AD234)
                if (ToGround(inv, x, row, out float gx, out float gy))
                {
                    AddPoint(chains, new OverheadEdgePoint(new Vec2(gx, gy), v), true);
                    done = true;
                }
                break;
            }
            if (done) continue;
            // row 26: 0x006AD234..0x006AD278, quad[0].x <= (float)x <= quad[2].x (NaN skips), maps (x, rect.y), response 0, kind 0
            float fx = x;
            if (!(quad[0].X <= fx) || !(fx <= quad[2].X)) continue;
            if (ToGround(inv, x, ry, out float cx, out float cy))
                AddPoint(chains, new OverheadEdgePoint(new Vec2(cx, cy), 0f), false);
        }

        // row 29: keep chains with at least MinChainPoints, in build order
        var kept = chains.Where(c => (uint)c.Points.Count >= MinChainPoints).ToList();
        // row 28: XY of GetVisibleGroundQuad(H, cols, rows, z = 0)
        frame = new OverheadEdgeFrame(timestamp, true, VisibleGroundQuad(h, cols, rows), kept);
        return 0;
    }

    /// <summary>
    /// Row 17: s = delta (0.0f); for each non-zero tap in row-major order s = s + k*float(u8) (product rounded, then the sum: vmla.f32,
    /// not fused); saturate_cast&lt;short&gt;: vcvtr.s32.f32 (nearest even) and clamp to [-32768, 32767]. Row 16: reflect-101 at the image
    /// edge, every other pixel from the parent image.
    /// </summary>
    internal static short Response(GrayImage image, int x, int y)
    {
        float s = 0f;
        foreach (var (dy, dx, k) in Taps)
        {
            int sy = Reflect101(y + dy, image.Height), sx = Reflect101(x + dx, image.Width);
            float p = image.Pixels[sy * image.Width + sx];
            s = (float)(s + (float)(k * p));
        }
        return SaturateS16(RoundS32(s));
    }

    /// <summary>The float sum of <see cref="Response"/> before the int16 conversion (corpus coverage only).</summary>
    internal static float RawResponse(GrayImage image, int x, int y)
    {
        float s = 0f;
        foreach (var (dy, dx, k) in Taps)
        {
            int sy = Reflect101(y + dy, image.Height), sx = Reflect101(x + dx, image.Width);
            s = (float)(s + (float)(k * (float)image.Pixels[sy * image.Width + sx]));
        }
        return s;
    }

    private static int Reflect101(int i, int n)
    {
        if (n == 1) return 0;
        while ((uint)i >= (uint)n)
        {
            if (i < 0) i = -i;
            else i = 2 * n - i - 2;
        }
        return i;
    }

    private static short SaturateS16(int v) => (short)Math.Clamp(v, short.MinValue, short.MaxValue);

    /// <summary>vcvtr.s32.f32 under the default FPSCR: nearest, ties to even; saturating; NaN gives 0.</summary>
    internal static int RoundS32(float v)
    {
        if (float.IsNaN(v)) return 0;
        double r = Math.Round((double)v, MidpointRounding.ToEven);
        if (r >= int.MaxValue) return int.MaxValue;
        if (r <= int.MinValue) return int.MinValue;
        return (int)r;
    }

    /// <summary>vcvt.s32.f32: toward zero; saturating; NaN gives 0.</summary>
    internal static int TruncS32(float v)
    {
        if (float.IsNaN(v)) return 0;
        double r = Math.Truncate((double)v);
        if (r >= int.MaxValue) return int.MaxValue;
        if (r <= int.MinValue) return int.MinValue;
        return (int)r;
    }

    /// <summary>
    /// Row 20: the 3x3 cofactor inverse 0x004F7FB0 (cofactor path 0x004F821A..0x004F838E): the determinant in float (0x004F8390) by the
    /// engine's order c*(h*d-e*g) + (a*(e*i-h*f) - b*(i*d-f*g)) at 0x004F8390..0x004F83E8 (bit-identical to the row-0 expansion here:
    /// negation is exact and + and * commute), converted to double and back (0x004F83EC, 0x004F822C); a zero determinant gives the zero
    /// matrix (0x004F8230..0x004F823C); otherwise one vdiv.f32 1/det (0x004F8254) and each cofactor times it (0x004F8242..0x004F8388).
    /// </summary>
    internal static float[] Invert(ReadOnlySpan<float> m)
    {
        var r = new float[9];
        float a = m[0], b = m[1], c = m[2], d = m[3], e = m[4], f = m[5], g = m[6], hh = m[7], i = m[8];
        float[] cof =
        {
            Sub(Mul(e, i), Mul(f, hh)), Sub(Mul(c, hh), Mul(b, i)), Sub(Mul(b, f), Mul(c, e)),
            Sub(Mul(f, g), Mul(d, i)), Sub(Mul(a, i), Mul(c, g)), Sub(Mul(c, d), Mul(a, f)),
            Sub(Mul(d, hh), Mul(e, g)), Sub(Mul(b, g), Mul(a, hh)), Sub(Mul(a, e), Mul(b, d)),
        };
        float det = (float)((float)(Mul(a, cof[0]) + Mul(b, cof[3])) + Mul(c, cof[6]));
        det = (float)(double)det;
        if (det == 0f) return r;
        float inv = 1f / det;
        for (int k = 0; k < 9; k++) r[k] = Mul(cof[k], inv);
        return r;
    }

    private static float Mul(float x, float y) => (float)(x * y);
    private static float Sub(float x, float y) => (float)(x - y);

    /// <summary>
    /// Row 24: 0x006AE0A8: q = Hinv*(float x, float y, 1); q.z &lt;= 0 or NaN fails (sWarningF
    /// VisionSystem.SetEdgePositionHelper.BadProjectedZ, not logged here); else (q.x*(1/q.z), q.y*(1/q.z)).
    /// </summary>
    internal static bool ToGround(float[] inv, int x, int y, out float gx, out float gy)
    {
        float fx = x, fy = y;
        float qx = Dot(inv, 0, fx, fy), qy = Dot(inv, 3, fx, fy), qz = Dot(inv, 6, fx, fy);
        gx = gy = 0;
        if (qz <= 0f) return false;   // 0x006AE0DC vcmpe / 0x006AE0E4 bls: z <= 0 fails; NaN is not LS and takes the success path
        float r = 1f / qz;
        gx = (float)(qx * r);
        gy = (float)(qy * r);
        return true;
    }

    private static float Dot(float[] m, int row, float x, float y)
    {
        float a = (float)(m[row] * x);
        a = (float)(a + (float)(m[row + 1] * y));
        a = (float)(a + (float)(m[row + 2] * 1f));
        return a;
    }

    /// <summary>
    /// Row 28: GetVisibleGroundQuad 0x004F77CC..0x004F78D6 with z = 0: the ground ROI (GetGroundQuad 0x004F7774..0x004F77C2), and only when
    /// ClampQuad returned 1 (cmp.w r8,#1 at 0x004F7812) corners 1 and 3 (table {1,3} at 0xC48F70) replaced by Hinv*(imageQuad[i], 1)
    /// times 1/z', no sign test.
    /// </summary>
    internal static Vec2[] VisibleGroundQuad(ReadOnlySpan<float> h, int cols, int rows)
    {
        var image = NativeGroundPlaneProjection.Project(h, cols, rows, out bool clamped);
        var g = GroundPlaneROI.GroundQuad();
        if (!clamped) return g;
        var inv = Invert(h);
        foreach (int i in new[] { 1, 3 })
        {
            float x = image[i].X, y = image[i].Y;
            float qx = Dot(inv, 0, x, y), qy = Dot(inv, 3, x, y), qz = Dot(inv, 6, x, y);
            float r = 1f / qz;
            g[i] = new Vec2((float)(qx * r), (float)(qy * r));
        }
        return g;
    }

    /// <summary>
    /// Row 27: AddPoint 0x006AE1B0..0x006AE24C: a new chain when the list is empty, or the last chain is non-empty and of the other kind,
    /// or ComputeDistanceBetween&lt;2,float&gt; to its last point is &gt; 5.0f; an empty last chain takes the kind.
    /// </summary>
    internal static void AddPoint(List<OverheadEdgePointChain> chains, OverheadEdgePoint point, bool isBorder)
    {
        var last = chains.Count > 0 ? chains[^1] : null;
        bool start = last is null;
        if (last is not null && last.Points.Count > 0)
        {
            if (last.IsBorder != isBorder) start = true;
            else
            {
                var q = last.Points[^1].Ground;
                float dx = (float)((float)point.Ground.X - (float)q.X), dy = (float)((float)point.Ground.Y - (float)q.Y);
                float dist = MathF.Sqrt((float)((float)(dx * dx) + (float)(dy * dy)));
                if (!(dist <= MaxChainGap)) start = true;   // 0x006AE1E6 vcmpe / 0x006AE1EE bls: a NaN distance starts a new chain
            }
        }
        // A chain is only ever created together with its first point, so the empty-last-chain case cannot arise here.
        if (start)
        {
            last = new OverheadEdgePointChain { IsBorder = isBorder };
            chains.Add(last);
        }
        last!.Points.Add(point);
    }
}
