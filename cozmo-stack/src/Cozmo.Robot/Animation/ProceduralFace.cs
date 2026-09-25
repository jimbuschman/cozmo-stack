namespace Cozmo.Robot.Animation;

/// <summary>
/// The nineteen parameters that describe one of Cozmo's eyes.
///
/// The names and their order are taken from the engine itself: they sit as a contiguous block of strings in
/// <c>libcozmoEngine.so</c> .rodata at 0x00C1D399, and the animation assets carry exactly nineteen floats
/// per eye in that order (Unity ProceduralEyeParameter.cs; the clip and combine tables of M5 C9/C10 index them).
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

// fidelity: M5-002, M5-003
/// <summary>One eye: nineteen parameters, held in the engine's own order.</summary>
public sealed class Eye
{
    /// <summary>How many parameters an eye has. Fixed by the engine and by every asset in the OBB.</summary>
    public const int ParamCount = 19;

    private readonly float[] _p = new float[ParamCount];

    /// <summary>
    /// The default eye of <c>ProceduralFace()</c> (gap3 K4, 0x00583660..0x005836A0): every parameter 0 except
    /// EyeScaleX and EyeScaleY (the table at 0x00C5A970 = {2, 3}), which are 1.
    /// </summary>
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

    /// <summary>
    /// An eye read from an animation asset onto a default eye, as <c>ProceduralFace::SetEyeArrayHelper</c>
    /// (0x00583790..0x00583808, C6) stores it: every one of the nineteen values through <see cref="Clip(EyeParam, float, float)"/>,
    /// so a NaN keeps the default eye's value.
    /// </summary>
    public static Eye FromAsset(IReadOnlyList<float> values)
    {
        var e = new Eye();
        for (int i = 0; i < ParamCount; i++) e._p[i] = Clip((EyeParam)i, values[i], e._p[i]);
        return e;
    }

    /// <summary>A copy with every parameter inside the engine's range for it (a NaN kept as it is).</summary>
    public Eye Clipped()
    {
        var e = new Eye();
        for (int i = 0; i < ParamCount; i++) e._p[i] = Clip((EyeParam)i, _p[i]);
        return e;
    }

    /// <summary>
    /// The engine's per-parameter limits (C9, <c>ProceduralFace::Clip</c> 0x005847A8..0x00584908, table 0x00C5A97C):
    /// LidAngles ±45; EyeScale ≥ 0; the eight radii, both lid Y and both bends in [0, 1]; EyeCenterX/Y and EyeAngle are
    /// unclipped. This overload returns a NaN unchanged; the engine keeps the target's previous value for a NaN, which is
    /// <see cref="Clip(EyeParam, float, float)"/>.
    /// </summary>
    public static float Clip(EyeParam p, float value)
    {
        if (float.IsNaN(value)) return value;
        switch (p)
        {
            case EyeParam.UpperLidAngle:
            case EyeParam.LowerLidAngle:
                return Math.Clamp(value, -45f, 45f);
            case EyeParam.EyeScaleX:
            case EyeParam.EyeScaleY:
                return Math.Clamp(value, 0f, float.MaxValue);
            case EyeParam.LowerInnerRadiusX: case EyeParam.LowerInnerRadiusY:
            case EyeParam.UpperInnerRadiusX: case EyeParam.UpperInnerRadiusY:
            case EyeParam.UpperOuterRadiusX: case EyeParam.UpperOuterRadiusY:
            case EyeParam.LowerOuterRadiusX: case EyeParam.LowerOuterRadiusY:
            case EyeParam.UpperLidY: case EyeParam.UpperLidBend:
            case EyeParam.LowerLidY: case EyeParam.LowerLidBend:
                return Math.Clamp(value, 0f, 1f);
            default:
                return value;
        }
    }

    /// <summary>C9 with its NaN rule: a NaN keeps <paramref name="previous"/>, the target's value before the write.</summary>
    public static float Clip(EyeParam p, float value, float previous) => float.IsNaN(value) ? previous : Clip(p, value);

    /// <summary>
    /// Blend towards another eye, as <c>ProceduralFace::Interpolate</c> (C8, 0x00584290..0x0058454C) does it for
    /// 0 &lt; t &lt; 1: onto a default eye, EyeAngle as a unit vector (<see cref="BlendAngleDeg"/>), every other
    /// parameter linear, every result through <see cref="Clip(EyeParam, float, float)"/>.
    /// </summary>
    public Eye BlendTo(Eye other, float t)
    {
        var e = new Eye();
        for (int i = 0; i < ParamCount; i++)
        {
            float a = _p[i], b = other._p[i];
            float v = i == (int)EyeParam.EyeAngle ? BlendAngleDeg(a, b, t) : a + (b - a) * t;
            e._p[i] = Clip((EyeParam)i, v, e._p[i]);
        }
        return e;
    }

    /// <summary>
    /// The unit-vector blend of two angles in degrees (C8): the cosines and sines are mixed and turned back into degrees
    /// with atan2. Equal angles are returned as they are; equal cosines or sines are left unblended.
    /// </summary>
    /// <summary>C4: the engine's rad-to-deg float 0x42652EE1 (180/π), used by the angle blend (0x005842E0).</summary>
    internal static readonly float RadToDeg = BitConverter.Int32BitsToSingle(0x42652EE1);

    internal static float BlendAngleDeg(float aDeg, float bDeg, float t)
    {
        if (aDeg == bDeg) return aDeg;
        const float deg = MathF.PI / 180f;
        float ca = MathF.Cos(aDeg * deg), cb = MathF.Cos(bDeg * deg);
        float sa = MathF.Sin(aDeg * deg), sb = MathF.Sin(bDeg * deg);
        float c = ca == cb ? ca : (1f - t) * ca + t * cb;
        float s = sa == sb ? sa : (1f - t) * sa + t * sb;
        return MathF.Atan2(s, c) * RadToDeg;          // C4: atan2 · 0x42652EE1 (0x005842E0), not / (π/180)
    }

    public override string ToString() =>
        string.Join(" ", Enum.GetValues<EyeParam>().Select(p => $"{p}={this[p]:F2}"));
}

// fidelity: M5-002, M5-003, M5-031
/// <summary>
/// The engine's <c>ProceduralFace</c>: both eyes (left at +0x00, right at +0x4C), the <c>ScanlineDistorter*</c> at +0x98,
/// the face angle +0x9C, the face scale +0xA0/+0xA4 and the face centre +0xA8/+0xAC (gap1 layout; gap3 K4).
/// A new pose is the <c>ProceduralFace()</c> default: every eye parameter 0 except EyeScaleX/Y = 1, face scale 1,
/// centre 0, angle 0, and no distorter (K4).
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

    /// <summary>The face's scan-line distorter (+0x98), deep-copied with the face (copy ctor 0x005836A8, operator= 0x005835B8).</summary>
    public ScanlineDistorter? Distorter { get; set; }

    /// <summary>The canvas the eyes are laid out on: 128 wide, 64 tall (DrawFace's Image(64, 128)).</summary>
    public const int CanvasWidth = 128, CanvasHeight = 64;

    public ProceduralFacePose Clone() => new()
    {
        FaceAngle = FaceAngle, FaceCenterX = FaceCenterX, FaceCenterY = FaceCenterY,
        FaceScaleX = FaceScaleX, FaceScaleY = FaceScaleY,
        Left = Left.Clone(), Right = Right.Clone(),
        Distorter = Distorter?.Clone(),
    };

    /// <summary>
    /// <c>ProceduralFace::GetEyeBoundingBox</c> (gap1 K2, 0x00584568..0x0058463A), with sx, sy the face scale:
    /// xmin = 32 + sx·(cxL − 15·scaleXL), xmax = 96 + sx·(cxR + 15·scaleXR), ymin = 32 + sy·min(cy − 20·scaleY) over both
    /// eyes, ymax = 32 + sy·max(cy + 20·scaleY). The row names only "xmax = 96 + ..."; the right eye's term is completed
    /// here as the mirror of the left eye's, which gives the row's own 17 for a default face.
    /// </summary>
    public (float XMin, float XMax, float YMin, float YMax) GetEyeBoundingBox()
    {
        float sx = FaceScaleX, sy = FaceScaleY;
        float xmin = 32f + sx * (Left[EyeParam.EyeCenterX] - 15f * Left[EyeParam.EyeScaleX]);
        float xmax = 96f + sx * (Right[EyeParam.EyeCenterX] + 15f * Right[EyeParam.EyeScaleX]);
        float ymin = 32f + sy * MathF.Min(Left[EyeParam.EyeCenterY] - 20f * Left[EyeParam.EyeScaleY],
                                          Right[EyeParam.EyeCenterY] - 20f * Right[EyeParam.EyeScaleY]);
        float ymax = 32f + sy * MathF.Max(Left[EyeParam.EyeCenterY] + 20f * Left[EyeParam.EyeScaleY],
                                          Right[EyeParam.EyeCenterY] + 20f * Right[EyeParam.EyeScaleY]);
        return (xmin, xmax, ymin, ymax);
    }

    /// <summary>
    /// <c>ProceduralFace::SetFacePosition</c> (C6, gap1 K3, 0x00583B20..0x00583BF8): the centre clamped so the eye box stays
    /// on the canvas, x ∈ [−xmin, 128 − xmax] and y ∈ [−ymin, 64 − ymax].
    /// </summary>
    public void SetFacePosition(float x, float y)
    {
        var (xmin, xmax, ymin, ymax) = GetEyeBoundingBox();
        FaceCenterX = MathF.Max(-xmin, MathF.Min(CanvasWidth - xmax, x));
        FaceCenterY = MathF.Max(-ymin, MathF.Min(CanvasHeight - ymax, y));
    }

    /// <summary>
    /// <c>ProceduralFace::LookAt(x, y, xMax, yMax, up, down, outer)</c> (gap1 K3, 0x00584158..0x0058428A):
    /// SetFacePosition(x, y); xf = min(|x|/xMax, 1); yf = min((yMax − y)/(2·yMax), 1) with no lower clamp;
    /// sY = down + (up − down)·yf; looking left (x &lt; 0) the left eye's EyeScaleY = Clip((1 + o·xf)·sY) and the right's
    /// Clip((1 − o·xf)·sY), the other way round for x ≥ 0; for y &gt; 0 the left EyeCenterX = 2·min(y/yMax, 1) and the right
    /// the negative of it, otherwise both 0. EyeScaleX is untouched.
    /// </summary>
    public void LookAt(float x, float y, float xMax, float yMax, float up, float down, float outer)
    {
        SetFacePosition(x, y);
        float xf = MathF.Min(MathF.Abs(x) / xMax, 1f);
        float yf = MathF.Min((yMax - y) / (2f * yMax), 1f);
        float sY = down + (up - down) * yf;
        float l = x < 0 ? (1f + outer * xf) * sY : (1f - outer * xf) * sY;
        float r = x < 0 ? (1f - outer * xf) * sY : (1f + outer * xf) * sY;
        Left[EyeParam.EyeScaleY] = Eye.Clip(EyeParam.EyeScaleY, l, Left[EyeParam.EyeScaleY]);
        Right[EyeParam.EyeScaleY] = Eye.Clip(EyeParam.EyeScaleY, r, Right[EyeParam.EyeScaleY]);
        if (y > 0)
        {
            float dd = 2f * MathF.Min(y / yMax, 1f);
            Left[EyeParam.EyeCenterX] = dd;
            Right[EyeParam.EyeCenterX] = -dd;
        }
        else
        {
            Left[EyeParam.EyeCenterX] = 0f;
            Right[EyeParam.EyeCenterX] = 0f;
        }
    }

    /// <summary>
    /// <c>ProceduralFace::Interpolate(a, b, t)</c> (C8, 0x00584290..0x0058454C; gap1 G10):
    /// t = 0 or 1 copies the face outright (operator=, distorter included). Otherwise the result starts from a default
    /// face, so it has no distorter; EyeAngle and the face angle blend as unit vectors, every other parameter linearly;
    /// every eye parameter is Clipped; the centre goes through SetFacePosition before the face scale is set (C2), and a
    /// negative face scale becomes 0.
    /// </summary>
    public static ProceduralFacePose Interpolate(ProceduralFacePose a, ProceduralFacePose b, float t)
    {
        if (t == 0f) return a.Clone();
        if (t == 1f) return b.Clone();
        var r = new ProceduralFacePose
        {
            Left = a.Left.BlendTo(b.Left, t),
            Right = a.Right.BlendTo(b.Right, t),
            FaceAngle = Eye.BlendAngleDeg(a.FaceAngle, b.FaceAngle, t),
        };
        // C2: SetFacePosition runs before the face scale is set (0x005844A8 before 0x00584548), so the clamp uses the
        // default scale 1
        r.SetFacePosition(a.FaceCenterX + (b.FaceCenterX - a.FaceCenterX) * t,
                          a.FaceCenterY + (b.FaceCenterY - a.FaceCenterY) * t);
        r.FaceScaleX = MathF.Max(0f, a.FaceScaleX + (b.FaceScaleX - a.FaceScaleX) * t);
        r.FaceScaleY = MathF.Max(0f, a.FaceScaleY + (b.FaceScaleY - a.FaceScaleY) * t);
        return r;
    }

    /// <summary>
    /// Blend towards another pose (see <see cref="Interpolate"/>); <paramref name="t"/> is clamped to [0, 1].
    /// </summary>
    public ProceduralFacePose BlendTo(ProceduralFacePose other, float t) => Interpolate(this, other, Math.Clamp(t, 0f, 1f));

    /// <summary>Parameters <c>ProceduralFace::Combine</c> adds (table 0x00C5A972 = {0, 1, 4, 14, 17}).</summary>
    private static readonly EyeParam[] CombineAdd =
        { EyeParam.EyeCenterX, EyeParam.EyeCenterY, EyeParam.EyeAngle, EyeParam.UpperLidAngle, EyeParam.LowerLidAngle };

    /// <summary>Parameters it multiplies (table 0x00C5A977 = {2, 3}).</summary>
    private static readonly EyeParam[] CombineMultiply = { EyeParam.EyeScaleX, EyeParam.EyeScaleY };

    /// <summary>
    /// <c>ProceduralFace::Combine(other)</c> (C10, 0x00584648..0x0058478E; gap1 G10), in place: adds EyeCenterX/Y,
    /// EyeAngle and both lid angles of each eye, the face angle and the centre; multiplies EyeScaleX/Y and the face scale;
    /// leaves every other parameter as this face's; no clip. The distorter: when both have one, the other's is copied only
    /// if |its amount at 0.5| is greater (both evaluations draw their jitter, this face's first); when only the other has
    /// one it is copied.
    /// </summary>
    public void Combine(ProceduralFacePose other)
    {
        foreach (var (mine, theirs) in new[] { (Left, other.Left), (Right, other.Right) })
        {
            foreach (var p in CombineAdd) mine[p] += theirs[p];
            foreach (var p in CombineMultiply) mine[p] *= theirs[p];
        }
        FaceAngle += other.FaceAngle;
        FaceCenterX += other.FaceCenterX;
        FaceCenterY += other.FaceCenterY;
        FaceScaleX *= other.FaceScaleX;
        FaceScaleY *= other.FaceScaleY;

        if (other.Distorter is { } od)
        {
            if (Distorter is { } md)
            {
                int mineAmt = Math.Abs(md.GetEyeDistortionAmount(0.5f));
                int theirAmt = Math.Abs(od.GetEyeDistortionAmount(0.5f));
                if (theirAmt > mineAmt) Distorter = od.Clone();
            }
            else Distorter = od.Clone();
        }
    }

    /// <summary><c>ProceduralFace::InitScanlineDistorter(maxAmount, noiseProb)</c> (gap1 G5): a new distorter.</summary>
    public void InitScanlineDistorter(int maxAmount, float noiseProbability) =>
        Distorter = new ScanlineDistorter(maxAmount, noiseProbability);

    /// <summary><c>ProceduralFace::RemoveScanlineDistorter</c> (gap1 G5).</summary>
    public void RemoveScanlineDistorter() => Distorter = null;

    /// <summary>
    /// Cozmo's resting face, as shipped: the first ProceduralFace keyframe of anim_neutral_eyes_01, the one entry of
    /// ag_neutral_face (A2). Kept as literals for the raw-face API and the helpers (policy M5-020); the streamer takes the
    /// neutral face from the loaded clip itself.
    /// </summary>
    public static ProceduralFacePose ShippedNeutral()
    {
        var pose = new ProceduralFacePose();
        pose.Left = Eye.FromAsset(new[]
        {
            9.169666f, 0f, 1.214333f, 0.90528f, 0f,
            0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f,
            0f, 0f, 0f, 0f, 0f, 0f,
        });
        pose.Right = Eye.FromAsset(new[]
        {
            -10.206374f, 0f, 1.222037f, 0.90528f, 0f,
            0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f,
            0f, 0f, 0f, 0f, 0f, 0f,
        });
        return pose;
    }

    public override string ToString() =>
        $"face angle={FaceAngle:F1} centre=({FaceCenterX:F1},{FaceCenterY:F1}) scale=({FaceScaleX:F2},{FaceScaleY:F2})";
}
