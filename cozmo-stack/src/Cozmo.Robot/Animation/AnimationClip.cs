namespace Cozmo.Robot.Animation;

/// <summary>
/// The tracks an animation can drive. One animation owns a track for its whole length, so two animations
/// cannot fight over the same motor or the same display.
/// </summary>
[Flags]
public enum AnimationTrack
{
    None = 0,
    Face = 1 << 0,
    Audio = 1 << 1,
    Head = 1 << 2,
    Lift = 1 << 3,
    Body = 1 << 4,
    Lights = 1 << 5,
    Event = 1 << 6,
    All = Face | Audio | Head | Lift | Body | Lights | Event,
}

/// <summary>Anything that happens at a point on an animation's timeline.</summary>
public abstract record Keyframe(uint TriggerTimeMs)
{
    /// <summary>The track this keyframe drives.</summary>
    public abstract AnimationTrack Track { get; }
    /// <summary>How long it takes to play out. Zero for instantaneous keyframes.</summary>
    public virtual uint DurationMs => 0;
    /// <summary>When it is finished.</summary>
    public uint EndTimeMs => TriggerTimeMs + DurationMs;
}

/// <summary>Move the lift to a height. The schema calls this height_mm and gives it a single byte.</summary>
public sealed record LiftKeyframe(uint TriggerTimeMs, uint DurationTimeMs, byte HeightMm, byte VariabilityMm)
    : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Lift;
    public override uint DurationMs => DurationTimeMs;
}

/// <summary>Move the head to an angle in degrees.</summary>
public sealed record HeadKeyframe(uint TriggerTimeMs, uint DurationTimeMs, sbyte AngleDeg, byte VariabilityDeg)
    : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Head;
    public override uint DurationMs => DurationTimeMs;
    public float AngleRad => AngleDeg * MathF.PI / 180f;
}

/// <summary>
/// Drive the body. <c>radius_mm</c> is a string in the schema, not a number, so the raw token is kept and a
/// numeric value offered only when it parses. Observed tokens include <c>STRAIGHT</c>.
/// </summary>
public sealed record BodyKeyframe(uint TriggerTimeMs, uint DurationTimeMs, string RadiusRaw, short Speed)
    : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Body;
    public override uint DurationMs => DurationTimeMs;

    /// <summary>True when the clip asks for a straight line rather than an arc.</summary>
    public bool IsStraight => RadiusRaw.Equals("STRAIGHT", StringComparison.OrdinalIgnoreCase);

    /// <summary>The turn radius in mm when the token is a number, otherwise null.</summary>
    public float? RadiusMm =>
        float.TryParse(RadiusRaw, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
}

/// <summary>
/// Play a sound. The event ids are Wwise identifiers into the sound banks, which this milestone does not
/// decode; they are carried through so a later milestone can resolve them.
/// </summary>
public sealed record AudioKeyframe(uint TriggerTimeMs, long[] EventIds, float Volume, float[] Probabilities, bool HasAlts)
    : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Audio;
}

/// <summary>
/// Set the backpack LEDs. Each of the five arrays is a colour, but the channel order and scale are not
/// established, so they are kept as the raw float arrays the asset holds.
/// </summary>
public sealed record LightsKeyframe(uint TriggerTimeMs, uint DurationTimeMs,
                                    float[] Left, float[] Right, float[] Front, float[] Middle, float[] Back)
    : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Lights;
    public override uint DurationMs => DurationTimeMs;
}

/// <summary>Show a pre-rendered face animation by name, from the faceAnimations assets.</summary>
public sealed record FaceAnimationKeyframe(uint TriggerTimeMs, string AnimName) : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Face;
}

/// <summary>A named event the animation raises. The engine uses these to trigger game-side reactions.</summary>
public sealed record EventKeyframe(uint TriggerTimeMs, string EventId) : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Event;
}

/// <summary>Remember the current heading, so a later keyframe can turn back to it.</summary>
public sealed record RecordHeadingKeyframe(uint TriggerTimeMs) : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Body;
}

/// <summary>Turn back to the heading a <see cref="RecordHeadingKeyframe"/> remembered.</summary>
public sealed record TurnToRecordedHeadingKeyframe(uint TriggerTimeMs, uint DurationTimeMs, short OffsetDeg,
                                                   short SpeedDegPerSec, short AccelDegPerSec2, short DecelDegPerSec2,
                                                   ushort ToleranceDeg, ushort NumHalfRevs, bool UseShortestDir)
    : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Body;
    public override uint DurationMs => DurationTimeMs;
}

/// <summary>A procedural face pose: the whole-face transform plus 19 parameters for each eye.</summary>
public sealed record FaceKeyframe(uint TriggerTimeMs, ProceduralFacePose Pose) : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Face;
}

/// <summary>
/// One animation clip, as it sits in the robot's own assets.
///
/// The clip is data. Nothing here decides when a keyframe fires; that is the scheduler's job, so a clip can
/// be inspected, tested and replayed without a robot.
/// </summary>
public sealed class AnimationClip
{
    public string Name { get; init; } = "";
    public IReadOnlyList<Keyframe> Keyframes { get; init; } = Array.Empty<Keyframe>();

    /// <summary>Which tracks this clip touches, so the scheduler knows what it needs to own.</summary>
    public AnimationTrack Tracks { get; init; }

    /// <summary>When the last keyframe finishes.</summary>
    public uint DurationMs { get; init; }

    /// <summary>Keyframes on one track, in time order.</summary>
    public IEnumerable<Keyframe> OnTrack(AnimationTrack track) => Keyframes.Where(k => (k.Track & track) != 0);

    public override string ToString() =>
        $"{Name}: {Keyframes.Count} keyframes over {DurationMs} ms, tracks {Tracks}";
}
