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
/// Drive the body. <c>radius_mm</c> is a string in the schema, not a number.
///
/// The engine turns that string into a 16-bit radius and sends it to the robot with the speed, letting the
/// firmware do the geometry. <c>BodyMotionKeyFrame::ProcessRadiusString</c> at 0x004FB588 in
/// libcozmoEngine.so resolves the symbolic tokens, and <c>SetMembersFromFlatBuf</c> at 0x004FB494 parses a
/// numeric one with <c>atoi</c> clamped to a signed 16-bit range. That mapping is reproduced exactly by
/// <see cref="EncodedRadius"/>.
/// </summary>
public sealed record BodyKeyframe(uint TriggerTimeMs, uint DurationTimeMs, string RadiusRaw, short Speed)
    : Keyframe(TriggerTimeMs)
{
    /// <summary>The radius the engine sends for a straight line.</summary>
    public const short StraightRadius = short.MaxValue;      // 0x7FFF
    /// <summary>The radius the engine sends for a turn on the spot.</summary>
    public const short TurnInPlaceRadius = 0;

    public override AnimationTrack Track => AnimationTrack.Body;
    public override uint DurationMs => DurationTimeMs;

    /// <summary>True when the clip asks for a straight line rather than an arc.</summary>
    public bool IsStraight => RadiusRaw.Equals("STRAIGHT", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the clip asks the robot to turn on the spot.</summary>
    public bool IsTurnInPlace =>
        RadiusRaw.Equals("TURN_IN_PLACE", StringComparison.OrdinalIgnoreCase) ||
        RadiusRaw.Equals("POINT_TURN", StringComparison.OrdinalIgnoreCase);

    /// <summary>The turn radius in mm when the token is a number, otherwise null.</summary>
    public float? RadiusMm =>
        float.TryParse(RadiusRaw, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>True when the token is one the engine understands.</summary>
    public bool RadiusIsKnown => IsStraight || IsTurnInPlace || HasDigits(RadiusRaw);

    /// <summary>
    /// The 16-bit radius the engine puts on the wire, reproducing its own resolution order: a token with
    /// any digit is parsed numerically first, then the two symbolic turn tokens, then STRAIGHT. A token the
    /// engine does not recognise makes it log an error and drop the keyframe, which is what null means here.
    /// </summary>
    public short? EncodedRadius
    {
        get
        {
            if (HasDigits(RadiusRaw))
            {
                // the engine uses atoi, which stops at the first non-digit and yields 0 on nonsense
                long v = Atoi(RadiusRaw);
                return (short)Math.Clamp(v, short.MinValue, short.MaxValue);
            }
            if (IsTurnInPlace) return TurnInPlaceRadius;
            if (IsStraight) return StraightRadius;
            return null;
        }
    }

    private static bool HasDigits(string s)
    {
        foreach (var c in s) if (c is >= '0' and <= '9') return true;
        return false;
    }

    /// <summary>C's atoi: optional sign, then digits, stopping at the first character that is not one.</summary>
    private static long Atoi(string s)
    {
        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        bool neg = i < s.Length && (s[i] == '-' || s[i] == '+') && s[i++] == '-';
        long v = 0;
        while (i < s.Length && s[i] is >= '0' and <= '9')
        {
            v = v * 10 + (s[i++] - '0');
            if (v > int.MaxValue) { v = int.MaxValue; break; }
        }
        return neg ? -v : v;
    }
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
