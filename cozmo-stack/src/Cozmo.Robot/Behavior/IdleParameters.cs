namespace Cozmo.Robot.Behavior;

/// <summary>
/// The engine's own keep-alive tunables, with the values it ships.
///
/// Names and order are Anki's, from the decompiled
/// <c>unity/scripts/csharp/Anki.Cozmo/LiveIdleAnimationParameter.cs</c> (30 parameters). The values are
/// the engine's own defaults, recovered by disassembling
/// <c>AnimationStreamer::SetDefaultParams()</c> at 0x0057DB40 in libcozmoEngine.so, which is a flat run
/// of <c>SetParam(index, float)</c> calls. All 30 were read off; the last, <see cref="EyeDartDownMinScale"/>,
/// is set by a tail call at 0x0057DCD0 rather than a regular one.
///
/// These are facts about the shipped engine, not choices made here. A caller may override any of them,
/// but the defaults are what Cozmo actually does.
/// </summary>
public sealed record IdleParameters
{
    // ---- blinking
    /// <summary>3000 ms.</summary>
    public double BlinkSpacingMinMs { get; init; } = 3000;
    /// <summary>4000 ms.</summary>
    public double BlinkSpacingMaxMs { get; init; } = 4000;

    /// <summary>
    /// 1000 ms. How long the robot must be idle before it starts moving, as opposed to only blinking and
    /// darting its eyes. Below this it stays still.
    /// </summary>
    public double TimeBeforeWiggleMotionsMs { get; init; } = 1000;

    // ---- body
    /// <summary>100 ms.</summary>
    public double BodyMovementSpacingMinMs { get; init; } = 100;
    /// <summary>1000 ms.</summary>
    public double BodyMovementSpacingMaxMs { get; init; } = 1000;
    /// <summary>250 ms.</summary>
    public double BodyMovementDurationMinMs { get; init; } = 250;
    /// <summary>1500 ms.</summary>
    public double BodyMovementDurationMaxMs { get; init; } = 1500;
    /// <summary>10 mm/s. Deliberately tiny: this is a shuffle, not a drive.</summary>
    public double BodyMovementSpeedMmps { get; init; } = 10;
    /// <summary>0.5 — half the body moves go straight, half turn.</summary>
    public double BodyMovementStraightFraction { get; init; } = 0.5;

    // ---- lift
    /// <summary>50 ms.</summary>
    public double LiftMovementDurationMinMs { get; init; } = 50;
    /// <summary>500 ms.</summary>
    public double LiftMovementDurationMaxMs { get; init; } = 500;
    /// <summary>250 ms.</summary>
    public double LiftMovementSpacingMinMs { get; init; } = 250;
    /// <summary>2000 ms.</summary>
    public double LiftMovementSpacingMaxMs { get; init; } = 2000;
    /// <summary>35 mm.</summary>
    public double LiftHeightMeanMm { get; init; } = 35;
    /// <summary>8 mm.</summary>
    public double LiftHeightVariabilityMm { get; init; } = 8;

    // ---- head
    /// <summary>50 ms.</summary>
    public double HeadMovementDurationMinMs { get; init; } = 50;
    /// <summary>500 ms.</summary>
    public double HeadMovementDurationMaxMs { get; init; } = 500;
    /// <summary>250 ms.</summary>
    public double HeadMovementSpacingMinMs { get; init; } = 250;
    /// <summary>1000 ms.</summary>
    public double HeadMovementSpacingMaxMs { get; init; } = 1000;
    /// <summary>6 degrees.</summary>
    public double HeadAngleVariabilityDeg { get; init; } = 6;

    // ---- eye darts
    /// <summary>250 ms.</summary>
    public double EyeDartSpacingMinMs { get; init; } = 250;
    /// <summary>1000 ms.</summary>
    public double EyeDartSpacingMaxMs { get; init; } = 1000;
    /// <summary>6 pixels.</summary>
    public double EyeDartMaxDistancePix { get; init; } = 6;
    /// <summary>0.92.</summary>
    public double EyeDartMinScale { get; init; } = 0.92f;
    /// <summary>1.08.</summary>
    public double EyeDartMaxScale { get; init; } = 1.08f;
    /// <summary>50 ms.</summary>
    public double EyeDartMinDurationMs { get; init; } = 50;
    /// <summary>200 ms.</summary>
    public double EyeDartMaxDurationMs { get; init; } = 200;
    /// <summary>0.1 — the eye further from the dart direction grows by this much.</summary>
    public double EyeDartOuterEyeScaleIncrease { get; init; } = 0.1f;
    /// <summary>1.1.</summary>
    public double EyeDartUpMaxScale { get; init; } = 1.1f;
    /// <summary>0.85.</summary>
    public double EyeDartDownMinScale { get; init; } = 0.85f;

    /// <summary>The engine's shipped defaults.</summary>
    public static IdleParameters Default { get; } = new();
}
