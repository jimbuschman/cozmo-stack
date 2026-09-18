namespace Cozmo.Robot.Animation;

/// <summary>Named faces built from the nineteen-parameter eye model.</summary>
public enum Expression
{
    Neutral,
    Happy,
    Sad,
    Angry,
    Surprised,
    Sleepy,
    Blinking,
    Squinting,
    LookingLeft,
    LookingRight,
    LookingUp,
    LookingDown,
}

/// <summary>
/// A small set of expressions built from the eye parameters.
///
/// <b>These are ours, not Anki's.</b> The parameter names and order come from the engine, and the values in
/// the shipped animation assets show the ranges each one moves through, but the engine's named expressions
/// have not been recovered. What is here is built from the parameter semantics the names imply: a happy face
/// raises the lower lids, an angry one tilts the upper lids inward, and so on.
///
/// To reproduce an original expression exactly, play the animation clip that contains it rather than using
/// one of these. <c>robot.Animations.Play("anim_...")</c> uses the real asset data.
/// </summary>
public static class Expressions
{
    /// <summary>Builds the pose for a named expression.</summary>
    public static ProceduralFacePose Get(Expression e) => e switch
    {
        Expression.Neutral => ProceduralFaceRenderer.Neutral(),
        Expression.Happy => Happy(),
        Expression.Sad => Sad(),
        Expression.Angry => Angry(),
        Expression.Surprised => Surprised(),
        Expression.Sleepy => Sleepy(),
        Expression.Blinking => Blinking(),
        Expression.Squinting => Squinting(),
        Expression.LookingLeft => Looking(-8f, 0f),
        Expression.LookingRight => Looking(8f, 0f),
        Expression.LookingUp => Looking(0f, -5f),
        Expression.LookingDown => Looking(0f, 5f),
        _ => ProceduralFaceRenderer.Neutral(),
    };

    private static ProceduralFacePose Base() => ProceduralFaceRenderer.Neutral();

    private static ProceduralFacePose Both(Action<Eye, bool> apply)
    {
        var p = Base();
        apply(p.Left, true);
        apply(p.Right, false);
        return p;
    }

    /// <summary>Lower lids raised, which is what makes a face read as smiling.</summary>
    private static ProceduralFacePose Happy() => Both((eye, _) =>
    {
        eye[EyeParam.LowerLidY] = 0.35f;
        eye[EyeParam.EyeScaleY] = 0.9f;
        eye[EyeParam.LowerInnerRadiusY] = 0.9f;
        eye[EyeParam.LowerOuterRadiusY] = 0.9f;
    });

    /// <summary>Upper lids down at the outer edge, eyes a little smaller.</summary>
    private static ProceduralFacePose Sad() => Both((eye, isLeft) =>
    {
        eye[EyeParam.UpperLidY] = 0.3f;
        eye[EyeParam.UpperLidAngle] = isLeft ? -18f : 18f;
        eye[EyeParam.EyeScaleY] = 0.85f;
        eye[EyeParam.EyeCenterY] = 2f;
    });

    /// <summary>Upper lids down at the inner edge, the opposite tilt to sadness.</summary>
    private static ProceduralFacePose Angry() => Both((eye, isLeft) =>
    {
        eye[EyeParam.UpperLidY] = 0.32f;
        eye[EyeParam.UpperLidAngle] = isLeft ? 22f : -22f;
        eye[EyeParam.UpperInnerRadiusX] = 0.1f;
        eye[EyeParam.UpperInnerRadiusY] = 0.1f;
    });

    /// <summary>Wide open and round.</summary>
    private static ProceduralFacePose Surprised() => Both((eye, _) =>
    {
        eye[EyeParam.EyeScaleX] = 1.1f;
        eye[EyeParam.EyeScaleY] = 1.15f;
        foreach (var p in Corners) eye[p] = 1f;
    });

    /// <summary>Lids most of the way closed.</summary>
    private static ProceduralFacePose Sleepy() => Both((eye, _) =>
    {
        eye[EyeParam.UpperLidY] = 0.55f;
        eye[EyeParam.LowerLidY] = 0.15f;
    });

    /// <summary>Fully closed: the eyes vanish, which is what a blink frame looks like.</summary>
    private static ProceduralFacePose Blinking() => Both((eye, _) => eye[EyeParam.EyeScaleY] = 0.04f);

    /// <summary>Lids in from both sides.</summary>
    private static ProceduralFacePose Squinting() => Both((eye, _) =>
    {
        eye[EyeParam.UpperLidY] = 0.35f;
        eye[EyeParam.LowerLidY] = 0.35f;
    });

    /// <summary>Both eyes shifted, which is how the robot looks somewhere without moving its head.</summary>
    private static ProceduralFacePose Looking(float dx, float dy) => Both((eye, _) =>
    {
        eye[EyeParam.EyeCenterX] = dx;
        eye[EyeParam.EyeCenterY] = dy;
    });

    private static readonly EyeParam[] Corners =
    {
        EyeParam.LowerInnerRadiusX, EyeParam.LowerInnerRadiusY,
        EyeParam.UpperInnerRadiusX, EyeParam.UpperInnerRadiusY,
        EyeParam.UpperOuterRadiusX, EyeParam.UpperOuterRadiusY,
        EyeParam.LowerOuterRadiusX, EyeParam.LowerOuterRadiusY,
    };
}
