namespace Cozmo.Robot.Animation;

/// <summary>
/// The nineteen parameters that describe one of Cozmo's eyes.
///
/// The names and their order are taken from the engine itself: they sit as a contiguous block of strings in
/// <c>libcozmoEngine.so</c> .rodata at 0x00C1D399, and the animation assets carry exactly nineteen floats
/// per eye in that order. Nothing here is inferred from PyCozmo.
/// </summary>
public enum EyeParam
{
    EyeCenterX = 0,
    EyeCenterY = 1,
    EyeScaleX = 2,
    EyeScaleY = 3,
    EyeAngle = 4,
    LowerInnerRadiusX = 5,
    LowerInnerRadiusY = 6,
    UpperInnerRadiusX = 7,
    UpperInnerRadiusY = 8,
    UpperOuterRadiusX = 9,
    UpperOuterRadiusY = 10,
    LowerOuterRadiusX = 11,
    LowerOuterRadiusY = 12,
    UpperLidY = 13,
    UpperLidAngle = 14,
    UpperLidBend = 15,
    LowerLidY = 16,
    LowerLidAngle = 17,
    LowerLidBend = 18,
}

/// <summary>One eye: nineteen parameters, held in the engine's own order.</summary>
public sealed class Eye
{
    /// <summary>How many parameters an eye has. Fixed by the engine and by every asset in the OBB.</summary>
    public const int ParamCount = 19;

    private readonly float[] _p = new float[ParamCount];

    public Eye() { this[EyeParam.EyeScaleX] = 1f; this[EyeParam.EyeScaleY] = 1f; }

    public Eye(IReadOnlyList<float> values)
    {
        if (values.Count != ParamCount)
            throw new ArgumentException($"an eye has exactly {ParamCount} parameters, not {values.Count}", nameof(values));
        for (int i = 0; i < ParamCount; i++) _p[i] = values[i];
    }

    public float this[EyeParam p]
    {
        get => _p[(int)p];
        set => _p[(int)p] = value;
    }

    public float this[int i]
    {
        get => _p[i];
        set => _p[i] = value;
    }

    public float[] ToArray() => (float[])_p.Clone();
    public Eye Clone() => new(_p);

    /// <summary>Linear blend towards another eye. <paramref name="t"/> is 0 here, 1 there.</summary>
    public Eye BlendTo(Eye other, float t)
    {
        var e = new Eye();
        for (int i = 0; i < ParamCount; i++) e._p[i] = _p[i] + (other._p[i] - _p[i]) * t;
        return e;
    }

    public override string ToString() =>
        string.Join(" ", Enum.GetValues<EyeParam>().Select(p => $"{p}={this[p]:F2}"));
}

/// <summary>
/// A complete procedural face: the whole-face transform plus both eyes.
///
/// The transform fields come from the animation schema. What the engine does with the radius, lid and bend
/// parameters when it renders is not established, so <see cref="ProceduralFaceRenderer"/> documents its own
/// interpretation rather than claiming to reproduce the engine's drawing exactly.
/// </summary>
public sealed class ProceduralFacePose
{
    public float FaceAngle { get; set; }
    public float FaceCenterX { get; set; }
    public float FaceCenterY { get; set; }
    public float FaceScaleX { get; set; } = 1f;
    public float FaceScaleY { get; set; } = 1f;
    public Eye Left { get; set; } = new();
    public Eye Right { get; set; } = new();

    public ProceduralFacePose Clone() => new()
    {
        FaceAngle = FaceAngle, FaceCenterX = FaceCenterX, FaceCenterY = FaceCenterY,
        FaceScaleX = FaceScaleX, FaceScaleY = FaceScaleY,
        Left = Left.Clone(), Right = Right.Clone(),
    };

    /// <summary>Linear blend towards another pose, which is how the engine moves between keyframes.</summary>
    public ProceduralFacePose BlendTo(ProceduralFacePose other, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new ProceduralFacePose
        {
            FaceAngle = Lerp(FaceAngle, other.FaceAngle, t),
            FaceCenterX = Lerp(FaceCenterX, other.FaceCenterX, t),
            FaceCenterY = Lerp(FaceCenterY, other.FaceCenterY, t),
            FaceScaleX = Lerp(FaceScaleX, other.FaceScaleX, t),
            FaceScaleY = Lerp(FaceScaleY, other.FaceScaleY, t),
            Left = Left.BlendTo(other.Left, t),
            Right = Right.BlendTo(other.Right, t),
        };
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    public override string ToString() =>
        $"face angle={FaceAngle:F1} centre=({FaceCenterX:F1},{FaceCenterY:F1}) scale=({FaceScaleX:F2},{FaceScaleY:F2})";
}

/// <summary>
/// Draws a <see cref="ProceduralFacePose"/> onto the robot's 128x32 face bitmap.
///
/// <b>This renderer is our own interpretation, not the engine's.</b> The parameter names and their order are
/// authoritative, and the asset values are real, but the engine's <c>ProceduralFaceDrawer</c> has not been
/// disassembled, so how it turns radii, lid angles and bends into pixels is unknown. What is implemented
/// here is the reading that the parameter names and the observed value ranges support: each eye is a
/// rounded rectangle whose corners take their radii from the four corner parameters, with the upper and
/// lower lids cutting in from above and below at an angle.
///
/// The consequence is that expressions will read correctly as expressions, but will not be pixel-identical
/// to the original robot. That is recorded as an open item rather than papered over.
/// </summary>
public static class ProceduralFaceRenderer
{
    /// <summary>Nominal eye size in pixels before scaling, chosen so a neutral face fills the panel sensibly.</summary>
    public const int NominalEyeWidth = 28;
    public const int NominalEyeHeight = 28;
    /// <summary>Where each eye sits when its centre parameters are zero.</summary>
    public const int LeftEyeCenterX = 40;
    public const int RightEyeCenterX = 88;
    public const int EyeCenterY = 16;

    /// <summary>
    /// Renders a pose to a fresh bitmap.
    ///
    /// The whole-face parameters are an affine transform over the finished face, not a change to each
    /// eye. <c>ProceduralFaceDrawer::DrawFace</c> draws both eyes at their nominal positions, builds a 2x3
    /// matrix with <c>GetTransformationMatrix(angle, scaleX, scaleY, transX, transY, 64, 32)</c>, and
    /// applies it with <c>cv::warpAffine</c> over the whole image. That matrix is the familiar
    /// rotate-and-scale-about-a-centre form — its translation column is
    /// <c>(1 - cos*sx)*cx - sin*sy*cy + tx</c> — so the centre terms move eye **positions** as well as
    /// stretching eye geometry, and the angle rotates the eyes around the face rather than spinning each
    /// one in place. The centre Anki passes is the centre of the canvas being drawn.
    ///
    /// Rendering here inverts that matrix per output pixel instead of warping a second buffer, which is
    /// the same result as nearest-neighbour warpAffine without the intermediate image.
    /// </summary>
    public static FaceBitmap Render(ProceduralFacePose pose)
    {
        var bmp = new FaceBitmap();

        float sx = pose.FaceScaleX, sy = pose.FaceScaleY;
        // A face scaled to nothing in either axis has no area to draw.
        if (MathF.Abs(sx) < 1e-4f || MathF.Abs(sy) < 1e-4f) return bmp;

        float angle = pose.FaceAngle * MathF.PI / 180f;
        float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
        const float centreX = FaceBitmap.Width / 2f;
        const float centreY = FaceBitmap.Height / 2f;

        // The forward matrix, exactly as GetTransformationMatrix builds it.
        float a = cos * sx, b = sin * sy;
        float c = -sin * sx, d = cos * sy;
        // The translation column exactly as the engine builds it:
        //   row0: (1 - cos*sx)*cx - sin*sy*cy + tx
        //   row1: + sin*sx*cx + (1 - cos*sy)*cy + ty
        // Note the sign on the row-1 centre term is POSITIVE sin*sx, not the row's own -sin*sx
        // coefficient. Getting that wrong makes the face centre fail to map to itself and cancels the
        // vertical half of a rotation, which is exactly what a rotation test caught here.
        float tx = (1f - a) * centreX - b * centreY + pose.FaceCenterX;
        float ty = (sin * sx) * centreX + (1f - d) * centreY + pose.FaceCenterY;

        // Its inverse. The determinant of the 2x2 part is scaleX * scaleY, because the rotation is
        // orthonormal, so this is well conditioned wherever the face has any area at all.
        float det = a * d - b * c;
        if (MathF.Abs(det) < 1e-6f) return bmp;
        float ia = d / det, ib = -b / det, ic = -c / det, id = a / det;

        for (int py = 0; py < FaceBitmap.Height; py++)
        for (int px = 0; px < FaceBitmap.Width; px++)
        {
            // This output pixel, carried back into the un-transformed face.
            float ox = px + 0.5f - tx, oy = py + 0.5f - ty;
            float fx = ia * ox + ib * oy;
            float fy = ic * ox + id * oy;

            if (InEye(pose, pose.Left, LeftEyeCenterX, fx, fy) ||
                InEye(pose, pose.Right, RightEyeCenterX, fx, fy))
                bmp[px, py] = 1;
        }
        return bmp;
    }

    /// <summary>
    /// Whether a point in the un-transformed face falls inside one eye.
    ///
    /// Only that eye's own 19 parameters are consulted. The whole-face scale, angle and centre have
    /// already been dealt with by the transform, so applying them again here would double them — which is
    /// what made a face-wide stretch widen both eyes without moving them apart.
    /// </summary>
    private static bool InEye(ProceduralFacePose pose, Eye eye, int baseCenterX, float fx, float fy)
    {
        float cx = baseCenterX + eye[EyeParam.EyeCenterX];
        float cy = EyeCenterY + eye[EyeParam.EyeCenterY];
        float w = NominalEyeWidth * Math.Max(0f, eye[EyeParam.EyeScaleX]);
        float h = NominalEyeHeight * Math.Max(0f, eye[EyeParam.EyeScaleY]);
        if (w < 0.5f || h < 0.5f) return false;                 // a closed eye draws nothing

        float halfW = w / 2f, halfH = h / 2f;
        // Corner radii are fractions of the half-size; the assets hold values around 0.5 for a neutral eye.
        float rLowerInner = Frac(eye[EyeParam.LowerInnerRadiusX]), rLowerInnerY = Frac(eye[EyeParam.LowerInnerRadiusY]);
        float rUpperInner = Frac(eye[EyeParam.UpperInnerRadiusX]), rUpperInnerY = Frac(eye[EyeParam.UpperInnerRadiusY]);
        float rUpperOuter = Frac(eye[EyeParam.UpperOuterRadiusX]), rUpperOuterY = Frac(eye[EyeParam.UpperOuterRadiusY]);
        float rLowerOuter = Frac(eye[EyeParam.LowerOuterRadiusX]), rLowerOuterY = Frac(eye[EyeParam.LowerOuterRadiusY]);

        // Lids cut in from the top and bottom as a fraction of the eye height.
        float upperLid = Math.Clamp(eye[EyeParam.UpperLidY], 0f, 1f);
        float lowerLid = Math.Clamp(eye[EyeParam.LowerLidY], 0f, 1f);
        float upperLidAngle = eye[EyeParam.UpperLidAngle] * MathF.PI / 180f;
        float lowerLidAngle = eye[EyeParam.LowerLidAngle] * MathF.PI / 180f;

        // Only the eye's own angle here; the face angle is part of the whole-face transform.
        float eyeAngle = eye[EyeParam.EyeAngle] * MathF.PI / 180f;
        float ca = MathF.Cos(-eyeAngle), sa = MathF.Sin(-eyeAngle);

        {
            // into the eye's own frame
            float dx = fx - cx, dy = fy - cy;
            float ex = dx * ca - dy * sa, ey = dx * sa + dy * ca;
            if (MathF.Abs(ex) > halfW || MathF.Abs(ey) > halfH) return false;

            // rounded corner test, using the radius pair for whichever corner this pixel is in
            bool outer = (baseCenterX == LeftEyeCenterX) ? ex < 0 : ex > 0;   // outer is away from the nose
            float rx, ry;
            if (ey < 0) { (rx, ry) = outer ? (rUpperOuter, rUpperOuterY) : (rUpperInner, rUpperInnerY); }
            else { (rx, ry) = outer ? (rLowerOuter, rLowerOuterY) : (rLowerInner, rLowerInnerY); }

            float cornerX = halfW * (1f - rx), cornerY = halfH * (1f - ry);
            float ax = MathF.Abs(ex), ay = MathF.Abs(ey);
            if (ax > cornerX && ay > cornerY)
            {
                float nx = (ax - cornerX) / MathF.Max(0.001f, halfW - cornerX);
                float ny = (ay - cornerY) / MathF.Max(0.001f, halfH - cornerY);
                if (nx * nx + ny * ny > 1f) return false;       // outside the rounded corner
            }

            // lids, measured from the top and bottom edges and tilted by their angle
            float lidTop = -halfH + upperLid * h + MathF.Tan(upperLidAngle) * ex;
            float lidBottom = halfH - lowerLid * h + MathF.Tan(lowerLidAngle) * ex;
            if (ey < lidTop || ey > lidBottom) return false;

            return true;
        }

        static float Frac(float v) => Math.Clamp(v, 0f, 1f);
    }

    /// <summary>A neutral, wide-open pair of eyes: the face the robot shows when nothing else is happening.</summary>
    public static ProceduralFacePose Neutral()
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
