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

    /// <summary>
    /// An eye read from an animation asset, clipped as the engine clips it on load:
    /// <c>ProceduralFace::SetEyeArrayHelper</c> at 0x00583790 passes every one of the nineteen values
    /// through <see cref="Clip"/> before storing it.
    /// </summary>
    public static Eye FromAsset(IReadOnlyList<float> values) => new Eye(values).Clipped();

    /// <summary>A copy with every parameter inside the engine's range for it.</summary>
    public Eye Clipped()
    {
        var e = new Eye();
        for (int i = 0; i < ParamCount; i++) e._p[i] = Clip((EyeParam)i, _p[i]);
        return e;
    }

    /// <summary>
    /// The engine's per-parameter limits, from the 16-entry table <c>ProceduralFace::Clip</c> at 0x005847A8
    /// builds at 0x00C5A97C (each entry: parameter, min, max). A value outside its range is clamped and a
    /// warning logged; parameters not in the table (the two centres and the eye angle) are unbounded.
    /// NaN is returned unchanged by the engine after a warning, so it is returned unchanged here.
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

    /// <summary>
    /// Blend towards another eye, as <c>ProceduralFace::Interpolate</c> at 0x00584290 does it:
    /// <paramref name="t"/> is 0 here, 1 there; every parameter is linear except <see cref="EyeParam.EyeAngle"/>,
    /// which is blended as a direction (cosine and sine blended separately, then <c>atan2</c>) so that two
    /// angles interpolate along the shorter arc; and every result goes through <see cref="Clip"/>.
    /// </summary>
    public Eye BlendTo(Eye other, float t)
    {
        var e = new Eye();
        for (int i = 0; i < ParamCount; i++)
        {
            float a = _p[i], b = other._p[i];
            float v = i == (int)EyeParam.EyeAngle ? BlendAngleDeg(a, b, t) : a + (b - a) * t;
            e._p[i] = Clip((EyeParam)i, v);
        }
        return e;
    }

    /// <summary>
    /// The engine's angle interpolation: unchanged when the two angles are equal, otherwise the blended
    /// cosine and sine turned back into degrees with <c>atan2f</c>. Equal cosines or sines are left
    /// unblended, exactly as the engine's <c>ittt ne</c> guards do.
    /// </summary>
    internal static float BlendAngleDeg(float aDeg, float bDeg, float t)
    {
        if (aDeg == bDeg) return aDeg;
        const float deg = MathF.PI / 180f;
        float ca = MathF.Cos(aDeg * deg), cb = MathF.Cos(bDeg * deg);
        float sa = MathF.Sin(aDeg * deg), sb = MathF.Sin(bDeg * deg);
        float c = ca == cb ? ca : (1f - t) * ca + t * cb;
        float s = sa == sb ? sa : (1f - t) * sa + t * sb;
        return MathF.Atan2(s, c) / deg;
    }

    public override string ToString() =>
        string.Join(" ", Enum.GetValues<EyeParam>().Select(p => $"{p}={this[p]:F2}"));
}

/// <summary>
/// A complete procedural face: the whole-face transform plus both eyes.
///
/// The transform fields come from the animation schema, and what the engine does with every one of these
/// parameters when it renders is recovered in <c>re-analysis/PROCEDURAL_FACE.md</c> and ported in
/// <see cref="ProceduralFaceRenderer"/>.
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

    /// <summary>
    /// Blend towards another pose, as <c>ProceduralFace::Interpolate</c> at 0x00584290 does it, which is
    /// how <c>ProceduralFaceKeyFrame::GetInterpolatedFace</c> at 0x004F99E6 moves between keyframes.
    ///
    /// Every parameter is linear except the two angles, which blend as directions (see
    /// <see cref="Eye.BlendAngleDeg"/>); a face scale that would come out negative is set to zero with a
    /// warning in the engine and is zero here; and each eye parameter is clipped to its range. At exactly
    /// 0 or 1 the engine copies the corresponding face outright, which the arithmetic here reproduces.
    ///
    /// Not reproduced: the engine passes the blended face position through
    /// <c>ProceduralFace::SetFacePosition</c> at 0x00583B20, which clamps it so the eyes' bounding box
    /// stays on the 128 x 64 canvas. That only bites when a clip pushes the eyes off the display.
    /// </summary>
    public ProceduralFacePose BlendTo(ProceduralFacePose other, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new ProceduralFacePose
        {
            FaceAngle = Eye.BlendAngleDeg(FaceAngle, other.FaceAngle, t),
            FaceCenterX = Lerp(FaceCenterX, other.FaceCenterX, t),
            FaceCenterY = Lerp(FaceCenterY, other.FaceCenterY, t),
            FaceScaleX = MathF.Max(0f, Lerp(FaceScaleX, other.FaceScaleX, t)),
            FaceScaleY = MathF.Max(0f, Lerp(FaceScaleY, other.FaceScaleY, t)),
            Left = Left.BlendTo(other.Left, t),
            Right = Right.BlendTo(other.Right, t),
        };
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    /// <summary>
    /// Cozmo's resting face, as shipped.
    ///
    /// The engine has no neutral-face constant. <c>AnimationStreamer::AnimationStreamer</c> at 0x00579F78
    /// asks the animation group mapped to <c>AnimationTrigger::NeutralFace</c> for its one animation
    /// (warning "Neutral face animation group %s has %zu animations instead of one" otherwise), takes that
    /// clip's procedural face keyframe and installs it with <c>ProceduralFace::SetResetData</c> at
    /// 0x00583550, so that <c>ProceduralFace::Reset</c> restores it. In this build the map names
    /// <c>ag_neutral_face</c>, whose single entry is <c>anim_neutral_eyes_01</c> (in
    /// <c>anim_singlepose_01.bin</c>), and these are that keyframe's values.
    ///
    /// The eyes are wider than the nominal box, slightly shorter, and pulled towards each other; the
    /// corners are half-rounded and the lids are open. The right eye is not quite the mirror of the left.
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
