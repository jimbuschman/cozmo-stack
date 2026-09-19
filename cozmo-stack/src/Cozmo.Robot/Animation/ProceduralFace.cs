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
