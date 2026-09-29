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

// fidelity: M5-006
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

    /// <summary>
    /// True when the clip asks for a straight line rather than an arc. The match is a case-sensitive whole-string
    /// equality (B3: length then <c>memcmp</c>), so "straight" is not STRAIGHT.
    /// </summary>
    public bool IsStraight => RadiusRaw.Equals("STRAIGHT", StringComparison.Ordinal);

    /// <summary>
    /// True when the clip asks the robot to turn on the spot. TURN_IN_PLACE and POINT_TURN, matched case-sensitively as
    /// whole strings (B3).
    /// </summary>
    public bool IsTurnInPlace =>
        RadiusRaw.Equals("TURN_IN_PLACE", StringComparison.Ordinal) ||
        RadiusRaw.Equals("POINT_TURN", StringComparison.Ordinal);

    /// <summary>The turn radius in mm when the token is a number, otherwise null.</summary>
    public float? RadiusMm =>
        float.TryParse(RadiusRaw, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>
    /// B3: <c>ProcessRadiusString</c>'s whole-string, case-sensitive token match: TURN_IN_PLACE and POINT_TURN give 0,
    /// STRAIGHT gives 0x7FFF, and anything else (a digit string included) is null. The JSON path calls only this match
    /// (B5), so a JSON radius of "50" is rejected rather than atoi'd.
    /// </summary>
    public short? TokenRadius => IsTurnInPlace ? TurnInPlaceRadius : IsStraight ? StraightRadius : null;

    /// <summary>True when the token is one the engine understands (a FlatBuffer digit string counts, C4/B2).</summary>
    public bool RadiusIsKnown => IsStraight || IsTurnInPlace || HasDigits(RadiusRaw);

    /// <summary>
    /// The 16-bit radius the engine puts on the wire. The FlatBuffer path's resolution order (B2): a token with any digit
    /// is parsed numerically first (<c>atoi</c>, clamped to int16), then the two symbolic turn tokens, then STRAIGHT. A
    /// token the engine does not recognise makes it log an error and drop the keyframe, which is what null means here.
    /// The JSON path never reaches the digit branch: its string goes to <see cref="TokenRadius"/> (B5).
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
            return TokenRadius;
        }
    }

    private static bool HasDigits(string s)
    {
        foreach (var c in s) if (c is >= '0' and <= '9') return true;
        return false;
    }

    /// <summary>
    /// ProcessRadiusString's speed checks (C4, gap1 S1) for a radius string: with any digit, CheckTurnSpeed (|v| ≥ 221 →
    /// ±220); TURN_IN_PLACE / POINT_TURN, CheckRotationSpeed (|v| &gt; 300 → ±300); STRAIGHT, CheckStraightSpeed (±220).
    /// False for a token the engine does not recognise.
    /// </summary>
    public static bool CheckSpeedForRadiusString(string raw, ref short speed)
    {
        var probe = new BodyKeyframe(0, 0, raw, 0);
        if (probe.EncodedRadius is not { } radius) return false;
        if (!HasDigits(raw) && radius == TurnInPlaceRadius)
        {
            if (Math.Abs((int)speed) > 300) speed = (short)Math.Clamp((int)speed, -300, 300);
        }
        else if (Math.Abs((int)speed) >= 221) speed = (short)Math.Clamp((int)speed, -220, 220);
        return true;
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

// fidelity: M5-016
/// <summary>
/// Set the backpack LEDs. The FlatBuffer table (trigger f0, duration f1, Left f2, Right f3, Front f4, Middle f5,
/// Back f6) is converted to the JSON "BackpackLightsKeyFrame" and loaded by <c>SetMembersFromJson</c> (C16,
/// 0x005758C8..0x00575CBE); the colours are kept here as the float arrays the asset holds and encoded by
/// <see cref="EncodedLeds"/>.
/// </summary>
public sealed record LightsKeyframe(uint TriggerTimeMs, uint DurationTimeMs,
                                    float[] Left, float[] Right, float[] Front, float[] Middle, float[] Back)
    : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Lights;
    public override uint DurationMs => DurationTimeMs;

    /// <summary>
    /// The five LED words of the 0x98 BackpackLights message in the engine's order <b>Left, Front, Middle, Back, Right</b>
    /// (C18, +0x10..+0x18; Q2), read as SetMembersFromJson reads them (gap4 J1.9, <see cref="BackpackColor.TryReadAll"/>);
    /// null when a colour is not an array of 3 or 4 floats, which rejects the keyframe at load.
    /// </summary>
    public ushort[]? EncodedLeds => BackpackColor.TryReadAll(Left, Right, Front, Middle, Back, out var leds) ? leds : null;
}

// fidelity: M5-016
/// <summary>
/// <c>Anki::NamedColors::GetByString</c> (0x0083F780; C5 item 6.1..6.3): the 13-entry map, inserted in this order, with
/// the exact upper-case names and the <c>ColorRGBA</c> bytes r, g, b, a of the shipped globals (C5 item 6.2). The lookup
/// is case-sensitive and exact; a miss logs "Unknown color name '%s', returning default" and returns DEFAULT.
/// LIGHTGRAY exists as a symbol but is not in the map.
/// </summary>
public static class NamedColors
{
    /// <summary>NamedColors::DEFAULT (0xC9742B), the bytes ff cc 00 ff as this stack's 0xRRGGBBAA word.</summary>
    public const uint Default = 0xFFCC00FF;

    private static readonly Dictionary<string, uint> Map = new(StringComparer.Ordinal)
    {
        ["RED"] = 0xFF0000FF,
        ["GREEN"] = 0x00FF00FF,
        ["BLUE"] = 0x0000FFFF,
        ["YELLOW"] = 0xFFFF00FF,
        ["CYAN"] = 0x00FFFFFF,
        ["ORANGE"] = 0xFF7F00FF,
        ["MAGENTA"] = 0xFF00FFFF,
        ["WHITE"] = 0xFFFFFFFF,
        ["BLACK"] = 0x000000FF,
        ["DEFAULT"] = 0xFFCC00FF,
        ["DARKGRAY"] = 0x4C4C4CFF,
        ["DARKGREEN"] = 0x007F00FF,
        ["OFFWHITE"] = 0xCCCCCCCC,
    };

    /// <summary>The 13 names, for a caller that wants to enumerate them.</summary>
    public static IReadOnlyCollection<string> Names => Map.Keys;

    /// <summary>
    /// <c>GetByString</c> (C5 item 6.3): a hit returns the stored colour; a miss logs and returns DEFAULT.
    /// </summary>
    public static uint GetByString(string name, Action<string>? log = null)
    {
        if (Map.TryGetValue(name, out var c)) return c;
        log?.Invoke($"warning: NamedColors.GetByString: Unknown color name '{name}', returning default");
        return Default;
    }

    /// <summary>
    /// A named colour as the four raw components r, g, b, a, so the reused-<c>ColorRGBA</c> reader
    /// (<see cref="BackpackColor.TryReadAll"/>) treats it by the raw rule (C17) and reproduces the same word.
    /// </summary>
    public static float[] AsRawComponents(string name, Action<string>? log = null)
    {
        uint c = GetByString(name, log);
        return new[] { (float)((c >> 24) & 0xFF), (float)((c >> 16) & 0xFF), (float)((c >> 8) & 0xFF), (float)(c & 0xFF) };
    }
}

// fidelity: M5-016
/// <summary>The backpack keyframe colour rules (C17).</summary>
public static class BackpackColor
{
    /// <summary>
    /// The default ColorRGBA: NamedColors::DEFAULT (C5 item 6.2, 0xC9742B), the bytes ff cc 00 ff read as this stack's
    /// 0xRRGGBBAA word (R in the top byte, A in the bottom; C17, 0x0083F490).
    /// </summary>
    public const uint Default = NamedColors.Default;

    /// <summary>
    /// <c>GetColorOptional</c> (C17, 0x0084024C..0x0084050C; gap4 J1.9) into <paramref name="c"/>: the array must hold 3 or
    /// 4 floats (otherwise false); when any of r, g, b is above 1 the values are raw, otherwise ×255; each through
    /// vcvt.u32.f32 (truncation, a negative saturating to 0), stored as a u8; alpha only from a 4th element ≥ 0, by the same
    /// rule (so a 3-element array keeps <paramref name="c"/>'s alpha).
    /// </summary>
    public static bool GetColorOptional(IReadOnlyList<float> v, ref uint c)
    {
        if (v.Count != 3 && v.Count != 4) return false;
        bool raw = v[0] > 1f || v[1] > 1f || v[2] > 1f;
        byte U8(float x)
        {
            float f = raw ? x : x * 255f;
            uint u = float.IsNaN(f) || f <= 0f ? 0u : f >= 4294967295f ? uint.MaxValue : (uint)f;
            return unchecked((byte)u);
        }
        byte r = U8(v[0]), g = U8(v[1]), b = U8(v[2]);
        byte a = (byte)(c & 0xFF);
        if (v.Count == 4 && v[3] >= 0f) a = U8(v[3]);
        c = ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;
        return true;
    }

    /// <summary>One colour onto the default ColorRGBA (a fresh keyframe read); the default when the read fails.</summary>
    public static uint FromArray(IReadOnlyList<float> v)
    {
        uint c = Default;
        GetColorOptional(v, ref c);
        return c;
    }

    /// <summary>
    /// BackpackLightsKeyFrame::SetMembersFromJson's colours (gap4 J1.9): "Back", "Front", "Middle", "Left", "Right" in that
    /// order into one ColorRGBA, default-constructed once and reused; each required. The words come out in the wire order
    /// Left, Front, Middle, Back, Right (C18).
    /// </summary>
    public static bool TryReadAll(IReadOnlyList<float> left, IReadOnlyList<float> right, IReadOnlyList<float> front,
                                  IReadOnlyList<float> middle, IReadOnlyList<float> back, out ushort[] leds)
    {
        leds = new ushort[5];
        uint c = Default;
        if (!GetColorOptional(back, ref c)) return false;
        leds[3] = Encode(c);
        if (!GetColorOptional(front, ref c)) return false;
        leds[1] = Encode(c);
        if (!GetColorOptional(middle, ref c)) return false;
        leds[2] = Encode(c);
        if (!GetColorOptional(left, ref c)) return false;
        leds[0] = Encode(c);
        if (!GetColorOptional(right, ref c)) return false;
        leds[4] = Encode(c);
        return true;
    }

    /// <summary>
    /// The LED word (C17, 0x004FABDC..0x004FAC12): ((r &lt;&lt; 7) &amp; 0x7C00) | ((g &lt;&lt; 2) &amp; 0x3E0) | (b &gt;&gt; 3)
    /// | (a ≠ 0 ? 0x8000 : 0).
    /// </summary>
    public static ushort Encode(uint rgba)
    {
        int r = (int)(rgba >> 24) & 0xFF, g = (int)(rgba >> 16) & 0xFF, b = (int)(rgba >> 8) & 0xFF, a = (int)rgba & 0xFF;
        return (ushort)(((r << 7) & 0x7C00) | ((g << 2) & 0x3E0) | (b >> 3) | (a != 0 ? 0x8000 : 0));
    }
}

/// <summary>Show a pre-rendered face animation by name, from the faceAnimations assets.</summary>
public sealed record FaceAnimationKeyframe(uint TriggerTimeMs, string AnimName) : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Face;
}

// fidelity: M5-033
/// <summary>
/// An animation event: <c>event_id</c> through <c>AnimEventFromString</c> (C15, 0x004FA7A0..0x004FA852); streamed as the
/// 0x95 Event message {u8 event}, once. A name that is not an <see cref="Animation.AnimEvent"/> rejects the keyframe at
/// load.
/// </summary>
public sealed record EventKeyframe(uint TriggerTimeMs, string EventId) : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Event;

    /// <summary>The AnimEvent this names, or null for a name the engine does not recognise ("Count").</summary>
    public AnimEvent? Parsed =>
        EventId != nameof(AnimEvent.Count) && Enum.GetNames<AnimEvent>().Contains(EventId) ? Enum.Parse<AnimEvent>(EventId) : null;
}

/// <summary>AnimEvent (Unity AnimEvent.cs:5-8; C15): the values the 0x95 Event message carries.</summary>
public enum AnimEvent : byte
{
    DEVICE_AUDIO_TRIGGER,
    ENERGY_DRAINCUBE_END,
    TAPPED_BLOCK,
    Count,
}

/// <summary>Remember the current heading, so a later keyframe can turn back to it.</summary>
public sealed record RecordHeadingKeyframe(uint TriggerTimeMs) : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.Body;
}

// fidelity: M5-001
/// <summary>
/// A DeviceAudio keyframe (C5 item 1, D2/D1 row: <c>DeviceAudioKeyFrame</c>): it reads "audioName" only, its
/// <c>GetStreamMessage</c> returns null and <c>PlayOnDevice</c> is empty, so it has no wire effect and owns no track.
/// The DeviceAudio track's own wall-clock handling (A14) is not part of this record's load path.
/// </summary>
public sealed record DeviceAudioKeyframe(uint TriggerTimeMs, string AudioName) : Keyframe(TriggerTimeMs)
{
    public override AnimationTrack Track => AnimationTrack.None;
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

    /// <summary>Whether a rejected keyframe ended the load early (gap1 C2): the keyframes before it are kept.</summary>
    public bool LoadTruncated { get; init; }

    /// <summary>Keyframes on one track, in time order.</summary>
    public IEnumerable<Keyframe> OnTrack(AnimationTrack track) => Keyframes.Where(k => (k.Track & track) != 0);

    public override string ToString() =>
        $"{Name}: {Keyframes.Count} keyframes over {DurationMs} ms, tracks {Tracks}";
}
