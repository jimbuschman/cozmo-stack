namespace Cozmo.Robot.Animation;

// fidelity: M5-015, M5-021, M5-032
/// <summary>
/// <c>ProceduralFaceDrawer</c>: <c>DrawFace</c> and <c>DrawEye</c> onto the engine's 64-row by 128-column canvas, with the
/// shipped OpenCV 3.1.0 functions (<see cref="OpenCv310"/>). Every step is a row of the M5 inventory:
/// <list type="bullet">
/// <item><b>DrawEye</b> (gap1 D1..D6, E3): the outline from four corner arcs, rx = (int)roundf(p·0.5·30),
/// ry = (int)roundf(p·0.5·40), a corner point when either radius is below 1 (M5-015); the two lid polygons with their
/// tan/cos bends; the per-eye transform about (0, 0) translated to (nominalX + EyeCenterX, 32 + EyeCenterY), the second
/// eye mirrored, each point (int)roundf(x) and (int)(roundf(y) + _firstScanLine); the eye box; then fillConvexPoly
/// LINE_4 of the outline with 255, AddOffNoise when the face has a distorter, and the upper and lower lids with 0.</item>
/// <item><b>DrawFace</b> (E1, E2, gap1 G9, gap3 C1..C6): both eyes; the row extent from the eye boxes when the face
/// transform is the identity, otherwise <c>cv::warpAffine</c> INTER_NEAREST, BORDER_CONSTANT 0 with the face matrix and
/// the extent from the transformed box; rows clamped to 0..63; the rows of the <c>_firstScanLine</c> parity cleared in
/// [min, max); and, with a distorter, every kept row shifted by <c>GetEyeDistortionAmount</c>.</item>
/// </list>
/// </summary>
public static class ProceduralFaceRenderer
{
    // ---------------------------------------------------------------- canvas

    /// <summary>The drawing canvas, <c>Image(0x40, 0x80)</c> in <c>DrawFace</c> (E1): 64 rows of 128 columns.</summary>
    public const int CanvasWidth = 128;
    public const int CanvasHeight = 64;

    /// <summary>The centre the whole-face transform turns about (E1's GetTransformationMatrix(..., 64, 32)).</summary>
    public const float FaceCentreX = 64f;
    public const float FaceCentreY = 32f;

    /// <summary>
    /// <c>ProceduralFaceDrawer::_firstScanLine</c> (.bss 0x0105AB48), 0 at start (M3 B3). InitStream toggles it (A11) and
    /// the blink's closed frame flips it at generation time (gap1 K9). DrawEye adds it to every point's y (D4) and
    /// DrawFace clears the rows of its parity (E2: 0 clears even rows). A process static (<see cref="ScanLineState.Process"/>).
    /// </summary>
    public static int FirstScanLine => ScanLineState.Process.Drawer;

    // ---------------------------------------------------------------- the eye's own frame

    /// <summary>The nominal eye, 30 by 40 (the 0.5·30 and 0.5·40 of the radii, gap1 D1).</summary>
    public const float NominalEyeWidth = 30f;
    public const float NominalEyeHeight = 40f;

    /// <summary>Half extents: the corner fallbacks (±15, ±20) of gap1 D1.</summary>
    public const float EyeHalfWidth = 15f;
    public const float EyeHalfHeight = 20f;

    /// <summary>nominalX is 32 for eye 0 and 96 for eye 1 (table 0x005859DC = {96, 32}, index flipped; gap1 D4); y is 32.</summary>
    public const float LeftEyeCenterX = 32f;
    public const float RightEyeCenterX = 96f;
    public const float EyeCenterY = 32f;

    /// <summary>The <c>delta</c> of every <c>cv::ellipse2Poly</c> call in DrawEye (gap1 D1..D3).</summary>
    public const int ArcDeltaDegrees = 10;

    /// <summary>The degrees-to-radians constant of the lid tan and cos: the float 0x3C8EFA35 ([0x005853AC], [0x0058502E]; C2).</summary>
    private static readonly float LidDegToRad = BitConverter.Int32BitsToSingle(0x3C8EFA35);

    /// <summary>C's roundf: half away from zero.</summary>
    private static float RoundF(float v) => MathF.Round(v, MidpointRounding.AwayFromZero);

    /// <summary>An eye's box on the canvas (gap1 D5): (minX, minY, maxX − minX, maxY − minY).</summary>
    public readonly record struct EyeBox(int X, int Y, int Width, int Height);

    // ---------------------------------------------------------------- DrawFace

    /// <summary>
    /// <c>ProceduralFaceDrawer::DrawFace</c> with the drawer's current <see cref="FirstScanLine"/>: the 64 × 128 canvas,
    /// row-major, 255 where lit.
    /// </summary>
    public static byte[] DrawFace(ProceduralFacePose face) => DrawFace(face, FirstScanLine);

    /// <summary><c>DrawFace</c> for an explicit scan-line parity (0 or 1).</summary>
    public static byte[] DrawFace(ProceduralFacePose face, int firstScanLine)
    {
        var img = new byte[CanvasWidth * CanvasHeight];
        var l = DrawEye(face, 0, img, firstScanLine);
        var r = DrawEye(face, 1, img, firstScanLine);

        int rowMin, rowMax;
        if (face.FaceAngle == 0f && face.FaceCenterX == 0f && face.FaceCenterY == 0f
            && face.FaceScaleX == 1f && face.FaceScaleY == 1f)
        {
            // E1: the row extent is the eye boxes' min/max
            rowMin = Math.Min(l.Y, r.Y);
            rowMax = Math.Max(l.Y + l.Height, r.Y + r.Height);
        }
        else
        {
            // E1: GetTransformationMatrix(angle, sx, sy, cx, cy, 64, 32) then cv::warpAffine(img, img, M, Size(128, 64),
            // INTER_NEAREST, BORDER_CONSTANT, 0) (gap3 C1..C6).
            var m = Matrix(face.FaceAngle, face.FaceScaleX, face.FaceScaleY, face.FaceCenterX, face.FaceCenterY,
                           FaceCentreX, FaceCentreY);
            img = OpenCv310.WarpAffineNearest(img, CanvasHeight, CanvasWidth,
                                              new double[] { m[0], m[1], m[2], m[3], m[4], m[5] });
            (rowMin, rowMax) = TransformedRowExtent(m, l, r);
        }

        // E2: rows clamped to 0..63; the rows of the _firstScanLine parity cleared in [min, max)
        rowMin = Math.Clamp(rowMin, 0, CanvasHeight - 1);
        rowMax = Math.Clamp(rowMax, 0, CanvasHeight - 1);
        for (int row = rowMin; row < rowMax; row++)
            if ((row & 1) == firstScanLine) Array.Clear(img, row * CanvasWidth, CanvasWidth);

        // G9: the kept rows shifted by the face's distorter
        if (face.Distorter is { } d && rowMax > rowMin)
        {
            for (int row = rowMin; row < rowMax; row++)
            {
                if ((row & 1) == firstScanLine) continue;
                float f = (float)(row - rowMin) / (rowMax - rowMin);
                int s = d.GetEyeDistortionAmount(f);
                ShiftRow(img, row, s);
            }
        }
        return img;
    }

    /// <summary>
    /// E1, C2 (0x00585CB6..0x00585D98): the row extent of a transformed face: the 4 corners of each eye rectangle, 8 points,
    /// through the face matrix; the min of their floors starting from 63 and the max of their ceilings starting from 0.
    /// </summary>
    internal static (int Min, int Max) TransformedRowExtent(float[] m, EyeBox l, EyeBox r)
    {
        int rowMin = CanvasHeight - 1, rowMax = 0;
        foreach (var b in new[] { l, r })
            foreach (var (cx, cy) in new[] { (b.X, b.Y), (b.X + b.Width, b.Y), (b.X, b.Y + b.Height), (b.X + b.Width, b.Y + b.Height) })
            {
                float ty = m[3] * cx + m[4] * cy + m[5];
                rowMin = Math.Min(rowMin, (int)MathF.Floor(ty));
                rowMax = Math.Max(rowMax, (int)MathF.Ceiling(ty));
            }
        return (rowMin, rowMax);
    }

    /// <summary>
    /// G9: s &gt; 0: memmove(row + s, row, 128 − s) and clear the first s pixels; s &lt; 0: shift left by |s| and clear the
    /// last |s|. |s| is not clamped in the engine; a shift of the whole row or more clears it.
    /// </summary>
    private static void ShiftRow(byte[] img, int row, int s)
    {
        int at = row * CanvasWidth;
        if (s > 0)
        {
            if (s >= CanvasWidth) { Array.Clear(img, at, CanvasWidth); return; }
            Array.Copy(img, at, img, at + s, CanvasWidth - s);
            Array.Clear(img, at, s);
        }
        else if (s < 0)
        {
            int k = -s;
            if (k >= CanvasWidth) { Array.Clear(img, at, CanvasWidth); return; }
            Array.Copy(img, at + k, img, at, CanvasWidth - k);
            Array.Clear(img, at + CanvasWidth - k, k);
        }
    }

    /// <summary>
    /// <c>ProceduralFaceDrawer::GetTransformationMatrix(angle, sx, sy, tx, ty, cx, cy)</c> (E1, gap1 D4), in float:
    /// [[c·sx, s·sy, (1 − c·sx)·cx − s·sy·cy + tx], [−s·sx, c·sy, s·sx·cx + (1 − c·sy)·cy + ty]], the angle in degrees.
    /// </summary>
    internal static float[] Matrix(float angleDeg, float sx, float sy, float tx, float ty, float cx, float cy)
    {
        float a = angleDeg * (MathF.PI / 180f);
        float cos = MathF.Cos(a), sin = MathF.Sin(a);
        return new[]
        {
            cos * sx,  sin * sy,  (1f - cos * sx) * cx - sin * sy * cy + tx,
            -sin * sx, cos * sy,  sin * sx * cx + (1f - cos * sy) * cy + ty,
        };
    }

    // ---------------------------------------------------------------- DrawEye

    /// <summary>
    /// <c>ProceduralFaceDrawer::DrawEye</c> (gap1 D1..D6) for eye <paramref name="whichEye"/> (0 left, 1 right) onto
    /// <paramref name="img"/>; returns the eye box (D5).
    /// </summary>
    public static EyeBox DrawEye(ProceduralFacePose face, int whichEye, byte[] img, int firstScanLine)
    {
        var e = whichEye == 0 ? face.Left : face.Right;

        var outline = Outline(e);
        var lower = LowerLid(e);
        var upper = UpperLid(e);

        // D4: M = GetTransformationMatrix(EyeAngle, EyeScaleX, EyeScaleY, nominalX + EyeCenterX, 32 + EyeCenterY, 0, 0)
        float nominalX = whichEye == 0 ? LeftEyeCenterX : RightEyeCenterX;
        var m = Matrix(e[EyeParam.EyeAngle], e[EyeParam.EyeScaleX], e[EyeParam.EyeScaleY],
                       nominalX + e[EyeParam.EyeCenterX], EyeCenterY + e[EyeParam.EyeCenterY], 0f, 0f);

        // D5: min/max of every transformed point of all three polygons, from (128, 64, 0, 0)
        int minX = CanvasWidth, minY = CanvasHeight, maxX = 0, maxY = 0;
        List<(int X, int Y)> Place(List<(int X, int Y)> local)
        {
            var pts = new List<(int X, int Y)>(local.Count);
            foreach (var (lx0, ly0) in local)
            {
                float lx = whichEye != 0 ? -lx0 : lx0;
                float ly = ly0;
                float px = m[0] * lx + m[1] * ly + m[2];
                float py = m[3] * lx + m[4] * ly + m[5];
                int x = (int)RoundF(px);
                int y = (int)(RoundF(py) + firstScanLine);
                pts.Add((x, y));
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            return pts;
        }
        var outlinePts = Place(outline);
        var lowerPts = Place(lower);
        var upperPts = Place(upper);

        // D6: outline 255, AddOffNoise with a distorter, upper lid 0, lower lid 0
        OpenCv310.FillConvexPoly(img, CanvasHeight, CanvasWidth, outlinePts, 255, 4, 0);
        face.Distorter?.AddOffNoise(m, NominalEyeHeight, NominalEyeWidth, img, CanvasHeight, CanvasWidth);
        if (upperPts.Count > 0) OpenCv310.FillConvexPoly(img, CanvasHeight, CanvasWidth, upperPts, 0, 4, 0);
        if (lowerPts.Count > 0) OpenCv310.FillConvexPoly(img, CanvasHeight, CanvasWidth, lowerPts, 0, 4, 0);

        return new EyeBox(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// gap1 D1: the outline in eye-local integers, UpperInner (270..360 about (15 − rx, ry − 20), else (15, −20)),
    /// LowerInner (0..90 about (15 − rx, 20 − ry), else (15, 20)), LowerOuter (90..180 about (rx − 15, 20 − ry), else
    /// (−15, 20)), UpperOuter (180..270 about (rx − 15, ry − 20), else (−15, −20)); angle 0, delta 10.
    /// </summary>
    private static List<(int X, int Y)> Outline(Eye e)
    {
        var pts = new List<(int X, int Y)>(48);
        Corner(pts, e, EyeParam.UpperInnerRadiusX, EyeParam.UpperInnerRadiusY, +1, -1, 270, 360);
        Corner(pts, e, EyeParam.LowerInnerRadiusX, EyeParam.LowerInnerRadiusY, +1, +1, 0, 90);
        Corner(pts, e, EyeParam.LowerOuterRadiusX, EyeParam.LowerOuterRadiusY, -1, +1, 90, 180);
        Corner(pts, e, EyeParam.UpperOuterRadiusX, EyeParam.UpperOuterRadiusY, -1, -1, 180, 270);
        return pts;
    }

    /// <summary>One corner (M5-015): rx = (int)roundf(p·0.5·30), ry = (int)roundf(p·0.5·40); below 1 in either, the point.</summary>
    private static void Corner(List<(int X, int Y)> pts, Eye e, EyeParam radiusX, EyeParam radiusY,
                               int sx, int sy, int arcFrom, int arcTo)
    {
        int rx = (int)RoundF(e[radiusX] * 0.5f * NominalEyeWidth);
        int ry = (int)RoundF(e[radiusY] * 0.5f * NominalEyeHeight);
        if (rx < 1 || ry < 1)
        {
            pts.Add((sx * 15, sy * 20));
            return;
        }
        // centre: x = sx·(15 − rx), y = sy·(20 − ry)
        pts.AddRange(OpenCv310.Ellipse2Poly(sx * (15 - rx), sy * (20 - ry), rx, ry, 0, arcFrom, arcTo, ArcDeltaDegrees));
    }

    /// <summary>
    /// gap1 D2: ly = (int)roundf(LowerLidY·40); t = round(15·tanf(a·0.0174533)); the polygon (16, 20 − ly + t), (16, 21),
    /// (−16, 21), (−16, 20 − ly − t); if b = roundf(LowerLidBend·40) ≠ 0, the arc ellipse2Poly((0, 20 − ly),
    /// ((int)round(15/cosf(a·deg2rad)), (int)b), (int)a, 180..360, 10) is appended.
    /// </summary>
    private static List<(int X, int Y)> LowerLid(Eye e)
    {
        int ly = (int)RoundF(e[EyeParam.LowerLidY] * 40f);
        float a = e[EyeParam.LowerLidAngle];
        int t = (int)RoundF(15f * MathF.Tan(a * LidDegToRad));
        var pts = new List<(int X, int Y)> { (16, 20 - ly + t), (16, 21), (-16, 21), (-16, 20 - ly - t) };
        float b = RoundF(e[EyeParam.LowerLidBend] * 40f);
        if (b != 0f)
            pts.AddRange(OpenCv310.Ellipse2Poly(0, 20 - ly, (int)RoundF(15f / MathF.Cos(a * LidDegToRad)), (int)b,
                                                (int)a, 180, 360, ArcDeltaDegrees));
        return pts;
    }

    /// <summary>
    /// gap1 D3: uy = (int)roundf(UpperLidY·40); t as in D2 of UpperLidAngle; the polygon (−16, uy − 20 − t), (−16, −21),
    /// (16, −21), (16, uy − 20 + t); if b = roundf(UpperLidBend·40) ≠ 0, ellipse2Poly((0, uy − 20), (round(15/cos a), b),
    /// (int)a, 0..180, 10) is appended.
    /// </summary>
    private static List<(int X, int Y)> UpperLid(Eye e)
    {
        int uy = (int)RoundF(e[EyeParam.UpperLidY] * 40f);
        float a = e[EyeParam.UpperLidAngle];
        int t = (int)RoundF(15f * MathF.Tan(a * LidDegToRad));
        var pts = new List<(int X, int Y)> { (-16, uy - 20 - t), (-16, -21), (16, -21), (16, uy - 20 + t) };
        float b = RoundF(e[EyeParam.UpperLidBend] * 40f);
        if (b != 0f)
            pts.AddRange(OpenCv310.Ellipse2Poly(0, uy - 20, (int)RoundF(15f / MathF.Cos(a * LidDegToRad)), (int)b,
                                                (int)a, 0, 180, ArcDeltaDegrees));
        return pts;
    }

    // ---------------------------------------------------------------- the 128 x 32 picture (this stack's API)

    /// <summary>
    /// A pose as this stack's 128 × 32 <see cref="FaceBitmap"/> (MD3, the raw-bitmap API and tests): the engine canvas
    /// drawn with <paramref name="firstScanLine"/>, and pixel (x, y) lit when either canvas row of pair y is lit
    /// (<see cref="FaceBitmapCodec.Decode"/>'s projection). The stream sends the canvas itself.
    /// </summary>
    public static FaceBitmap Render(ProceduralFacePose pose, int firstScanLine = 0) => Project(DrawFace(pose, firstScanLine));

    /// <summary>The 128 × 32 projection of a 64 × 128 canvas: pair y lit when either of its rows is.</summary>
    public static FaceBitmap Project(byte[] canvas)
    {
        var bmp = new FaceBitmap();
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = 0; x < FaceBitmap.Width; x++)
                if (canvas[2 * y * CanvasWidth + x] != 0 || canvas[(2 * y + 1) * CanvasWidth + x] != 0) bmp[x, y] = 1;
        return bmp;
    }

    /// <summary>
    /// The nominal eye box: both eyes at their base centres, unit scale, every corner half-rounded, lids open. A test
    /// reference, not the face the robot rests on (<see cref="ProceduralFacePose.ShippedNeutral"/>).
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
