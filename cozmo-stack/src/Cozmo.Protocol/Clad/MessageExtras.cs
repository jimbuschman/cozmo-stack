using System.Text;

namespace Cozmo.Protocol;

// Hand-written conveniences on top of the generated codecs. Nothing here changes the wire format;
// it only adds constructors, derived properties and formatting for the messages the stack uses most.

public sealed partial class RobotState
{
    public float PoseX => Pose.X;
    public float PoseY => Pose.Y;
    public float PoseZ => Pose.Z;
    public float PoseAngleRad => Pose.Angle;
    public float PosePitchRad => Pose.Pitch;
    public float HeadAngleRad => HeadAngle;

    /// <summary>
    /// The lift arm's angle in radians, which is what <c>liftAngle</c> carries.
    ///
    /// <c>Robot::UpdateFullRobotState</c> at 0x0051291C loads the field at RobotState+0x2C (this one, right
    /// after <c>headAngle</c> at +0x28) and stores it straight into Robot+0x300 (0x0051295E..0x0051296A),
    /// then passes it to <c>ComputeLiftPose</c>. Robot+0x300 is what <c>Robot::GetLiftHeight</c> at
    /// 0x00516F64 turns into millimetres with <c>45 + 66 * sinf(angle)</c>. PyCozmo's reading of the field
    /// as a height in millimetres was wrong, and an earlier version of this property repeated it.
    /// </summary>
    public float LiftAngleRad => LiftAngle;

    /// <summary>The lift height in millimetres, converted from <see cref="LiftAngleRad"/> as the engine converts it.</summary>
    public float LiftHeightMm => LiftHeightMmFromAngle(LiftAngle);

    /// <summary>Lift arm length: the 66.0 in <c>Robot::ConvertLiftAngleToLiftHeightMM</c> at 0x00516F9C.</summary>
    public const float LiftArmLengthMm = 66f;
    /// <summary>Lift pivot height: the 45.0 added in the same function (a third term there is 0.0).</summary>
    public const float LiftBaseHeightMm = 45f;

    // fidelity: M2-003
    /// <summary>
    /// <c>Robot::ConvertLiftAngleToLiftHeightMM</c> (0x00516F9C): <c>sinf(angle) * 66 + 45 + 0</c>, with no clamp on
    /// this path (GetLiftHeight 0x00516F64..0x00516F8E, literals 0x00516F90/94/98).
    /// </summary>
    public static float LiftHeightMmFromAngle(float angleRad) =>
        MathF.Sin(angleRad) * LiftArmLengthMm + LiftBaseHeightMm;

    /// <summary>
    /// <c>Robot::ConvertLiftHeightToLiftAngleRad</c> (0x005170B0): the height is raised to 32 when below it,
    /// and <c>(height - 45) / 66</c> goes through <c>asinf</c> unless the height is 92 or more, in which case
    /// the constant 0.712121 (that is, (92 - 45) / 66) is used instead. In effect the height is clamped to
    /// the 32..92 mm lift range before the inverse.
    /// </summary>
    public static float LiftAngleRadFromHeight(float heightMm)
    {
        float h = Math.Max(heightMm, 32f);
        float ratio = h < 92f ? (h - LiftBaseHeightMm) / LiftArmLengthMm : 0.712121f;
        return MathF.Asin(ratio);
    }

    public bool Has(RobotStatusFlag f) => (Status & (uint)f) != 0;

    public override string ToString() =>
        $"RobotState t={Timestamp} pose=({PoseX:F1},{PoseY:F1},{PoseZ:F1} a={PoseAngleRad:F2}) " +
        $"head={HeadAngle:F3}rad lift={LiftAngle:F3}rad({LiftHeightMm:F1}mm) batt={BatteryVoltage:F2}V status=0x{Status:x} " +
        $"cliff=[{string.Join(",", CliffDataRaw)}] seg={CurrPathSegment}";
}

// fidelity: M2-002
/// <summary>
/// Bit flags of <see cref="RobotState.Status"/>: the 17 names and values of the engine's EnumToString(RobotStatusFlag)
/// 0x007D57C8, which agree with Unity RobotStatusFlag.cs:8-25.
/// </summary>
[Flags]
public enum RobotStatusFlag : uint
{
    IsMoving = 1u << 0, IsCarryingBlock = 1u << 1, IsPickingOrPlacing = 1u << 2, IsPickedUp = 1u << 3,
    IsBodyAccMode = 1u << 4, IsFalling = 1u << 5, IsAnimating = 1u << 6, IsPathing = 1u << 7,
    LiftInPos = 1u << 8, HeadInPos = 1u << 9, IsAnimBufferFull = 1u << 10, IsAnimatingIdle = 1u << 11,
    IsOnCharger = 1u << 12, IsCharging = 1u << 13, CliffDetected = 1u << 14, AreWheelsMoving = 1u << 15,
    IsChargerOos = 1u << 16,
}

// fidelity: M2-006
/// <summary>
/// FirmwareVersion 0xEE {u16, u16-count u8[]} (Unpack 0x007B907E). The engine's handshake parses the JSON keys
/// build, version, time and sim from msg+4 (M1 G5.2..G5.6, in CozmoEngine). The engine has no
/// messageEngineToRobotHash or messageRobotToEngineHash: <see cref="EngineToRobotHash"/> and
/// <see cref="RobotToEngineHash"/> are diagnostics for the conformance tools only and no engine behaviour
/// reads them; <see cref="Version"/> and <see cref="Build"/> here are likewise for reports and logs.
/// </summary>
public sealed partial class FirmwareVersion
{
    /// <summary>The signature blob decoded as UTF-8: the JSON header of the robot's cozmo.safe image.</summary>
    public string SignatureJson => Encoding.UTF8.GetString(Signature);
    public int? Version => TryInt("\"version\"");
    public string? EngineToRobotHash => TryStr("\"messageEngineToRobotHash\"");
    public string? RobotToEngineHash => TryStr("\"messageRobotToEngineHash\"");
    public string? Build => TryStr("\"build\"");

    private string? TryStr(string key)
    {
        var s = SignatureJson;
        int i = s.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        int q = s.IndexOf('"', i + key.Length + 1);
        if (q < 0) return null;
        int e = s.IndexOf('"', q + 1);
        return e < 0 ? null : s[(q + 1)..e];
    }

    private int? TryInt(string key)
    {
        var s = SignatureJson;
        int i = s.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        int c = s.IndexOf(':', i), b = c + 1;
        while (b < s.Length && s[b] == ' ') b++;
        int e = b;
        while (e < s.Length && char.IsDigit(s[e])) e++;
        return int.TryParse(s.AsSpan(b, e - b), out var v) ? v : null;
    }

    public override string ToString() => $"FirmwareVersion v{Version?.ToString() ?? "?"} robot=0x{RobotId:x4} {SignatureJson}";
}

public sealed partial class PrintTrace
{
    /// <summary>
    /// Resolve against the OBB's config/engine/AnkiLogStringTables.json (nameTable / formatTable).
    /// The 3.4.0 tables still resolve firmware 2457's ids.
    ///
    /// The engine reads the first field as one 4-byte word (PrintTrace Unpack 0x007D4BF6, M2 inventory
    /// Appendix B section 2). How the engine's TracePrinter turns that word into a format-table id was not
    /// read; this decode keeps the capture decode's reading, the word's low 16 bits.
    /// </summary>
    public string Format(IReadOnlyDictionary<int, string>? names, IReadOnlyDictionary<int, string>? formats)
    {
        int formatId = (int)(FormatId & 0xFFFF);
        string name = names is not null && names.TryGetValue(NameId, out var nm) ? nm : $"name#{NameId}";
        string body = formats is not null && formats.TryGetValue(formatId, out var f) ? f : $"fmt#{formatId}";
        string args = Args.Length == 0 ? "" : " [" + string.Join(", ", Args) + "]";
        return $"[{name}] {body}{args}";
    }

    public override string ToString() =>
        $"Trace fmt={FormatId} name={NameId} level={Level} args=[{string.Join(",", Args)}]";
}

public sealed partial class RobotAvailable
{
    public override string ToString() => $"RobotAvailable serial=0x{SerialNumberHead:x8} hw={HwVersion}";
}

public sealed partial class ManufacturingID
{
    public override string ToString() => $"ManufacturingID serial=0x{SerialNumber:x8} bodyHw={BodyHwVersion} color={BodyColor}";
}

public partial struct LightState
{
    public const int Size = 10;
    public static LightState Off => new();
    public static LightState Solid(ushort color) => new() { OnColor = color, OffColor = color };

    /// <summary>
    /// Packs a colour into the 16-bit word the robot's LightState carries, as the engine packs it.
    ///
    /// Read from <c>CubeLightComponent::SendTransitionMessage</c> 0x00638056, which takes the engine's
    /// 32-bit <c>ColorRGBA</c> (red in the top byte, alpha in the bottom) and does exactly:
    ///
    /// <code>
    ///     (c &gt;&gt; 17) &amp; 0x7C00  |  (c &gt;&gt; 14) &amp; 0x03E0  |  (c &gt;&gt; 11) &amp; 0x001F
    /// </code>
    ///
    /// — the top five bits of each channel, red at bit 10, green at bit 5, blue at bit 0 — and then sets
    /// bit 15 when the colour's alpha byte is non-zero (<c>tst.w r5, #0xff</c> / <c>orrne r7, r7, #0x8000</c>).
    /// Every colour in every shipped light config has a non-zero alpha, so the engine sets bit 15 on
    /// every light word it sends, including the black it sends to turn a light off.
    /// </summary>
    // fidelity: M2-005
    public static ushort Rgb(byte r, byte g, byte b, byte a = 255) =>
        (ushort)(((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3) | (a != 0 ? 0x8000 : 0));
}

// fidelity: M2-015
// The builders below copy their arguments into the message verbatim, as the engine's builders do
// (MoveLiftToHeight 0x00640700, MoveHeadToAngle 0x006407CC, the DriveWheels word copies 0x0063F174..).
// The default arguments are the values the original app passes on the game-message path (M4 inventory MD1, MA11:
// Unity Robot.cs:1443-1450 head 10 rad/s, 20 rad/s^2, duration 0; Robot.cs:1638-1646 lift 10, 20, 0).
public sealed partial class SetHeadAngle
{
    public SetHeadAngle(float rad, float maxSpeed = 10f, float accel = 20f, float duration = 0f, byte actionId = 0)
    {
        AngleRad = rad; MaxSpeedRadPerSec = maxSpeed; AccelRadPerSec2 = accel; DurationSec = duration; ActionId = actionId;
    }
    public override string ToString() => $"SetHeadAngle {AngleRad:F3}rad speed={MaxSpeedRadPerSec} accel={AccelRadPerSec2} dur={DurationSec} action={ActionId}";
}

public sealed partial class SetLiftHeight
{
    public SetLiftHeight(float heightMm, float maxSpeed = 10f, float accel = 20f, float duration = 0f, byte actionId = 0)
    {
        HeightMm = heightMm; MaxSpeedRadPerSec = maxSpeed; AccelRadPerSec2 = accel; DurationSec = duration; ActionId = actionId;
    }
}

public sealed partial class DriveWheels
{
    public DriveWheels(float left, float right, float leftAccel = 0f, float rightAccel = 0f)
    {
        LwheelSpeedMmps = left; RwheelSpeedMmps = right; LwheelAccelMmps2 = leftAccel; RwheelAccelMmps2 = rightAccel;
    }
}

public sealed partial class SetHeadlight
{
    public SetHeadlight(bool enable) => Enable = enable;
}

public sealed partial class EnableStopOnCliff
{
    public EnableStopOnCliff(bool enable) => Enable = enable;
}

public sealed partial class SyncTime
{
    // fidelity: M2-004
    /// <summary>
    /// The second word is not spare and it is not zero: <c>Robot::SendSyncTime</c> 0x0051524C builds the
    /// message from <c>BaseStationTimer::GetCurrentTimeStamp()</c> (0x00515266) and the literal
    /// <c>0xC1A00000</c>, which is -20.0f (0x0051526C/0x00515270). The word is an f32: SyncTime's
    /// <c>operator==</c> compares it with <c>vcmp.f32</c> (0x007A3B0A). Its name is not established.
    /// </summary>
    public const float EngineConstant = -20.0f;

    public SyncTime(uint timestamp, float unknown = EngineConstant) { Timestamp = timestamp; Unknown = unknown; }
}

public sealed partial class BackpackLightsMiddle
{
    /// <summary>Top, middle and bottom RGB LEDs.</summary>
    public BackpackLightsMiddle(LightState top, LightState middle, LightState bottom)
        => Field0 = new[] { top, middle, bottom };
}

public sealed partial class MotorActionAck
{
    public override string ToString() => $"MotorActionAck action={ActionId}";
}

public sealed partial class ObjectAvailable
{
    public override string ToString() => $"ObjectAvailable factory=0x{FactoryId:x8} type={ObjectType} rssi={Rssi}";
}

public sealed partial class MotorCalibration
{
    public override string ToString() => $"MotorCalibration motor={MotorID} started={CalibStarted} auto={AutoStarted}";
}

public sealed partial class AnimationState
{
    public override string ToString() =>
        $"AnimationState t={Timestamp} animBytes={NumAnimBytesPlayed} audioFrames={NumAudioFramesPlayed} " +
        $"tracks=0x{EnabledAnimTracks:x2} tag={Tag} drops={ClientDropCount}";
}

public sealed partial class WiFiFlashID
{
    public override string ToString() => $"WiFiFlashID 0x{Field0:x8}";
}
