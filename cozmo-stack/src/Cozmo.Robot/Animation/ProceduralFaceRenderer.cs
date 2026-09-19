namespace Cozmo.Robot.Animation;

/// <summary>
/// Draws a <see cref="ProceduralFacePose"/> the way <c>ProceduralFaceDrawer</c> does.
///
/// This is a port of the engine's renderer, not an interpretation of it. Every constant and every step
/// below is read out of <c>libcozmoEngine.so</c>; see <c>re-analysis/PROCEDURAL_FACE.md</c> for the
/// disassembly each one comes from, and for the three things that could not be recovered and are named
/// there rather than guessed at.
///
/// The shape of it:
///
/// <list type="number">
/// <item>Both eyes are drawn as filled polygons on a <b>128 x 64</b> canvas — <c>DrawFace</c> allocates
/// <c>Image(0x40, 0x80)</c>, rows then columns. That is why the transform centre is (64, 32): it is the
/// centre of this canvas, not of the robot's panel.</item>
/// <item>One affine transform is applied over the whole canvas, about (64, 32).</item>
/// <item>Alternate rows are dropped. The engine blanks them and still encodes 64 rows; our verified wire
/// codec carries 32, so the kept rows are taken directly.</item>
/// </list>
/// </summary>
public static class ProceduralFaceRenderer
{
    // ---------------------------------------------------------------- canvas

    /// <summary>The drawing canvas, from <c>Image(0x40, 0x80)</c> in <c>DrawFace</c> at 0x00585B30.</summary>
    public const int CanvasWidth = 128;
    public const int CanvasHeight = 64;

    /// <summary>The centre the whole-face transform turns about — the centre of the canvas above.</summary>
    public const float FaceCentreX = 64f;
    public const float FaceCentreY = 32f;

    /// <summary>
    /// Which scanline parity survives. The engine flips this per animation in <c>InitStream</c>
    /// (<c>rsb r3, r3, #1</c>) and blanks the other parity, which alongside
    /// <c>GetMaxBlinkSpacingTimeForScreenProtection_ms</c> reads as burn-in protection rather than
    /// geometry. It is held fixed here so a pose renders to one bitmap.
    /// </summary>
    public const int FirstScanLine = 0;

    // ---------------------------------------------------------------- the eye's own frame

    /// <summary>The nominal eye, from <c>vmov.f32 s20, #3.0e+01</c> and the 40.0 literal at 0x005853A8.</summary>
    public const float NominalEyeWidth = 30f;
    public const float NominalEyeHeight = 40f;

    /// <summary>Half extents, from <c>vmov.f32 s18, #1.5e+01</c> and the -15/+15, -20/+20 corner fallbacks.</summary>
    public const float EyeHalfWidth = 15f;
    public const float EyeHalfHeight = 20f;

    /// <summary>
    /// Where an eye sits when its centre parameters are zero, from the table at 0x005859DC: it holds
    /// 96.0 then 32.0, and <c>whichEye == 0</c> selects the second. <c>EyeCenterX/Y</c> are pixel offsets
    /// added straight onto these.
    /// </summary>
    public const float LeftEyeCenterX = 32f;
    public const float RightEyeCenterX = 96f;
    public const float EyeCenterY = 32f;

    /// <summary>The <c>delta</c> argument to every <c>cv::ellipse2Poly</c> call in <c>DrawEye</c>.</summary>
    public const int ArcDeltaDegrees = 10;

    private const float Deg = MathF.PI / 180f;

    /// <summary>
    /// C's <c>roundf</c>, which the engine calls on every radius, lid offset and transformed point.
    /// It rounds a half away from zero; .NET's <c>MathF.Round</c> rounds a half to even, which differs
    /// on exactly the values a 0.5 scale produces.
    /// </summary>
    private static float Round(float v) => MathF.Round(v, MidpointRounding.AwayFromZero);

    // ---------------------------------------------------------------- rendering

    /// <summary>Renders a pose to a fresh bitmap. No state is carried between calls.</summary>
    public static FaceBitmap Render(ProceduralFacePose pose)
    {
        var canvas = new byte[CanvasWidth * CanvasHeight];
        DrawEye(canvas, pose.Left, whichEye: 0, LeftEyeCenterX);
        DrawEye(canvas, pose.Right, whichEye: 1, RightEyeCenterX);

        // DrawFace: GetTransformationMatrix(angle, sx, sy, tx, ty, 64, 32) then cv::warpAffine.
        var m = Matrix(pose.FaceAngle, pose.FaceScaleX, pose.FaceScaleY,
                       pose.FaceCenterX, pose.FaceCenterY, FaceCentreX, FaceCentreY);

        var bmp = new FaceBitmap();
        float det = m[0] * m[4] - m[1] * m[3];     // = scaleX * scaleY, the rotation being orthonormal
        if (MathF.Abs(det) < 1e-6f) return bmp;    // a face with no area draws nothing
        float ia = m[4] / det, ib = -m[1] / det, ic = -m[3] / det, id = m[0] / det;

        // warpAffine maps source to destination, so sample by the inverse. Taking row 2y + parity here
        // is the same as the engine blanking the other parity and then encoding all 64 rows.
        for (int y = 0; y < FaceBitmap.Height; y++)
        {
            float cy = 2 * y + FirstScanLine + 0.5f - m[5];
            for (int x = 0; x < FaceBitmap.Width; x++)
            {
                float cx = x + 0.5f - m[2];
                int u = (int)MathF.Floor(ia * cx + ib * cy);
                int v = (int)MathF.Floor(ic * cx + id * cy);
                if ((uint)u < CanvasWidth && (uint)v < CanvasHeight && canvas[v * CanvasWidth + u] != 0)
                    bmp[x, y] = 1;
            }
        }
        return bmp;
    }

    /// <summary>
    /// <c>ProceduralFaceDrawer::GetTransformationMatrix</c> at 0x00584FF8, verbatim:
    /// <code>
    /// row0:   cos*sx    sin*sy    (1 - cos*sx)*cx - sin*sy*cy + tx
    /// row1:  -sin*sx    cos*sy      sin*sx*cx + (1 - cos*sy)*cy + ty
    /// </code>
    /// The sign on row 1's centre term is a positive <c>sin*sx</c>, not the row's own <c>-sin*sx</c>
    /// coefficient. Getting that wrong stops the centre mapping to itself and cancels the vertical half
    /// of a rotation.
    /// </summary>
    private static float[] Matrix(float angleDeg, float sx, float sy, float tx, float ty, float cx, float cy)
    {
        float a = angleDeg * Deg;
        float cos = MathF.Cos(a), sin = MathF.Sin(a);
        return new[]
        {
            cos * sx,  sin * sy,  (1f - cos * sx) * cx - sin * sy * cy + tx,
            -sin * sx, cos * sy,  sin * sx * cx + (1f - cos * sy) * cy + ty,
        };
    }

    /// <summary>
    /// <c>DrawEye</c> at 0x005850E0. The outline is filled, then each lid is filled back to black over it.
    /// The lids overshoot the eye box by a pixel in both axes, which is what makes them mask cleanly.
    /// </summary>
    private static void DrawEye(byte[] canvas, Eye eye, int whichEye, float baseCentreX)
    {
        float cx = baseCentreX + eye[EyeParam.EyeCenterX];
        float cy = EyeCenterY + eye[EyeParam.EyeCenterY];

        // The per-eye matrix turns about (0, 0) — the eye's own origin — and then translates to its
        // centre. So EyeScaleX/Y and EyeAngle scale and spin the eye in place; they do not move it.
        var m = Matrix(eye[EyeParam.EyeAngle], eye[EyeParam.EyeScaleX], eye[EyeParam.EyeScaleY], cx, cy, 0f, 0f);

        Fill(canvas, Place(Outline(eye), whichEye, m), 1);
        Fill(canvas, Place(UpperLid(eye), whichEye, m), 0);
        Fill(canvas, Place(LowerLid(eye), whichEye, m), 0);
    }

    /// <summary>
    /// Carries a local polygon onto the canvas: mirror, transform, round, then add the scanline parity
    /// to y — the engine does all four, in that order, per point.
    ///
    /// The mirror is <c>if (whichEye != 0) x = -x</c>. Only one eye's geometry is ever authored; the
    /// other is its reflection. That is also what makes "inner" mean the same thing on both eyes: local
    /// +x is towards the nose for eye 0 at x=32 and, after reflection, for eye 1 at x=96 as well.
    /// </summary>
    private static List<(float X, float Y)> Place(List<(float X, float Y)> local, int whichEye, float[] m)
    {
        var pts = new List<(float X, float Y)>(local.Count);
        foreach (var (lx, ly) in local)
        {
            float x = whichEye != 0 ? -lx : lx;
            pts.Add((Round(m[0] * x + m[1] * ly + m[2]),
                     Round(m[3] * x + m[4] * ly + m[5]) + FirstScanLine));
        }
        return pts;
    }

    // ---------------------------------------------------------------- the outline

    /// <summary>
    /// The eye outline: four elliptical corner arcs, in the engine's own order, traversed clockwise.
    ///
    /// The corner-to-parameter mapping is the one named in <c>PROCEDURAL_FACE.md</c> as the natural
    /// reading rather than an instruction-level certainty. It is at least self-consistent with the
    /// mirror above: local +x is the inner side for both eyes, and local -y is up.
    /// </summary>
    private static List<(float X, float Y)> Outline(Eye eye)
    {
        var pts = new List<(float X, float Y)>(48);
        Corner(pts, eye, EyeParam.UpperInnerRadiusX, EyeParam.UpperInnerRadiusY, +1, -1, 270, 360);
        Corner(pts, eye, EyeParam.LowerInnerRadiusX, EyeParam.LowerInnerRadiusY, +1, +1, 0, 90);
        Corner(pts, eye, EyeParam.LowerOuterRadiusX, EyeParam.LowerOuterRadiusY, -1, +1, 90, 180);
        Corner(pts, eye, EyeParam.UpperOuterRadiusX, EyeParam.UpperOuterRadiusY, -1, -1, 180, 270);
        return pts;
    }

    /// <summary>
    /// One corner. Radii are whole pixels: <c>round(param * 0.5 * 30)</c> across and
    /// <c>round(param * 0.5 * 40)</c> down, so a parameter of 1.0 rounds the corner away entirely.
    /// A corner whose either radius falls below one pixel is drawn sharp — the engine pushes the exact
    /// box corner instead of calling <c>ellipse2Poly</c>.
    /// </summary>
    private static void Corner(List<(float X, float Y)> pts, Eye eye,
                               EyeParam radiusX, EyeParam radiusY, int sx, int sy, int arcFrom, int arcTo)
    {
        float rx = Round(eye[radiusX] * 0.5f * NominalEyeWidth);
        float ry = Round(eye[radiusY] * 0.5f * NominalEyeHeight);
        if (rx < 1f || ry < 1f)
        {
            pts.Add((sx * EyeHalfWidth, sy * EyeHalfHeight));
            return;
        }
        Arc(pts, sx * (EyeHalfWidth - rx), sy * (EyeHalfHeight - ry), rx, ry, 0f, arcFrom, arcTo);
    }

    // ---------------------------------------------------------------- the lids

    /// <summary>
    /// The upper lid: a quad masking down from above the eye, plus a bend arc bulging into it.
    ///
    /// <c>LidY</c> is a fraction of the <b>full</b> eye height, not the half height — the engine
    /// multiplies by 40, not 20 — so 0 leaves the eye open and 1 closes it completely. The angle tilts
    /// the lid line by <c>-round(tan(angle) * 15)</c> at each end.
    /// </summary>
    private static List<(float X, float Y)> UpperLid(Eye eye)
    {
        float line = Round(eye[EyeParam.UpperLidY] * NominalEyeHeight) - EyeHalfHeight;
        float angle = eye[EyeParam.UpperLidAngle];
        float dx = -Round(MathF.Tan(angle * Deg) * EyeHalfWidth);

        var pts = new List<(float X, float Y)>(24)
        {
            (-(EyeHalfWidth + 1f), line + dx),
            (-(EyeHalfWidth + 1f), -(EyeHalfHeight + 1f)),
            (EyeHalfWidth + 1f, -(EyeHalfHeight + 1f)),
            (EyeHalfWidth + 1f, line - dx),
        };

        float bend = eye[EyeParam.UpperLidBend];
        if (bend != 0f)
            Arc(pts, 0f, line,
                Round(EyeHalfWidth / MathF.Cos(angle * Deg)), Round(bend * NominalEyeHeight),
                angle, 0, 180);
        return pts;
    }

    /// <summary>The lower lid: the same construction reflected, with its bend arc sweeping upwards.</summary>
    private static List<(float X, float Y)> LowerLid(Eye eye)
    {
        float line = EyeHalfHeight - Round(eye[EyeParam.LowerLidY] * NominalEyeHeight);
        float angle = eye[EyeParam.LowerLidAngle];
        float dx = -Round(MathF.Tan(angle * Deg) * EyeHalfWidth);

        var pts = new List<(float X, float Y)>(24)
        {
            (EyeHalfWidth + 1f, line - dx),
            (EyeHalfWidth + 1f, EyeHalfHeight + 1f),
            (-(EyeHalfWidth + 1f), EyeHalfHeight + 1f),
            (-(EyeHalfWidth + 1f), line + dx),
        };

        float bend = eye[EyeParam.LowerLidBend];
        if (bend != 0f)
            Arc(pts, 0f, line,
                Round(EyeHalfWidth / MathF.Cos(angle * Deg)), Round(bend * NominalEyeHeight),
                angle, 180, 360);
        return pts;
    }

    // ---------------------------------------------------------------- primitives

    /// <summary>
    /// <c>cv::ellipse2Poly(centre, axes, angle, arcFrom, arcTo, delta, points)</c>: the arc sampled every
    /// <see cref="ArcDeltaDegrees"/> degrees, with the ellipse itself rotated by <paramref name="angleDeg"/>.
    /// Angles run clockwise on screen, y being down, so 0..180 sweeps the lower half.
    /// </summary>
    private static void Arc(List<(float X, float Y)> pts, float cx, float cy,
                            float ax, float ay, float angleDeg, int arcFrom, int arcTo)
    {
        float a = angleDeg * Deg;
        float ca = MathF.Cos(a), sa = MathF.Sin(a);
        for (int t = arcFrom; ; t += ArcDeltaDegrees)
        {
            if (t > arcTo) t = arcTo;                       // OpenCV always emits the end angle
            float r = t * Deg;
            float x = ax * MathF.Cos(r), y = ay * MathF.Sin(r);
            pts.Add((cx + x * ca - y * sa, cy + x * sa + y * ca));
            if (t >= arcTo) break;
        }
    }

    /// <summary>
    /// Fills a closed polygon, even-odd. The engine's fill is neither in <c>DrawFace</c>'s nor
    /// <c>DrawEye</c>'s imports, so it is inlined or in a helper that was not located; for the simple
    /// closed outlines these polygons form, even-odd and non-zero winding agree.
    /// </summary>
    private static void Fill(byte[] canvas, List<(float X, float Y)> poly, byte value)
    {
        if (poly.Count < 3) return;

        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var p in poly) { if (p.Y < lo) lo = p.Y; if (p.Y > hi) hi = p.Y; }
        int y0 = Math.Max(0, (int)MathF.Floor(lo));
        int y1 = Math.Min(CanvasHeight - 1, (int)MathF.Ceiling(hi));

        var xs = new List<float>(poly.Count);
        for (int y = y0; y <= y1; y++)
        {
            float scan = y + 0.5f;
            xs.Clear();
            for (int i = 0, n = poly.Count; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                if (a.Y == b.Y) continue;
                // Half-open in y, so a vertex shared by two edges is counted once.
                if ((scan >= a.Y && scan < b.Y) || (scan >= b.Y && scan < a.Y))
                    xs.Add(a.X + (scan - a.Y) / (b.Y - a.Y) * (b.X - a.X));
            }
            if (xs.Count < 2) continue;
            xs.Sort();
            for (int k = 0; k + 1 < xs.Count; k += 2)
            {
                int xa = Math.Max(0, (int)MathF.Ceiling(xs[k] - 0.5f));
                int xb = Math.Min(CanvasWidth - 1, (int)MathF.Floor(xs[k + 1] - 0.5f));
                for (int x = xa; x <= xb; x++) canvas[y * CanvasWidth + x] = value;
            }
        }
    }

    /// <summary>
    /// The nominal eye box: both eyes at their base centres, unit scale, every corner half-rounded, lids
    /// open. This is the pose the renderer's constants are measured against in the tests. It is <b>not</b>
    /// the face the robot rests on; that is <see cref="ProceduralFacePose.ShippedNeutral"/>, taken from
    /// the shipped neutral-face animation the engine itself loads.
    /// </summary>
    public static ProceduralFacePose Nominal()
    {
        var pose = new ProceduralFacePose();
        foreach (var eye in new[] { pose.Left, pose.Right })
        {
            eye[EyeParam.EyeScaleX] = 1f;
            eye[EyeParam.EyeScaleY] = 1f;
            foreach (var p in new[] { EyeParam.LowerInnerRadiusX, EyeParam.LowerInnerRadiusY,
                                      EyeParam.UpperInnerRadiusX, EyeParam.UpperInnerRadiusY,
                                      EyeParam.UpperOuterRadiusX, EyeParam.UpperOuterRadiusY,
                                      EyeParam.LowerOuterRadiusX, EyeParam.LowerOuterRadiusY })
                eye[p] = 0.5f;
        }
        return pose;
    }
}
