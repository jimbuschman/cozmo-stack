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
    /// <summary>The official robot struct calls this liftAngle; PyCozmo reports it as lift height in mm. Unconfirmed which unit the firmware sends.</summary>
    public float LiftHeightMm => LiftAngle;
    public bool Has(RobotStatusFlag f) => (Status & (uint)f) != 0;

    public override string ToString() =>
        $"RobotState t={Timestamp} pose=({PoseX:F1},{PoseY:F1},{PoseZ:F1} a={PoseAngleRad:F2}) " +
        $"head={HeadAngle:F3}rad lift={LiftAngle:F1} batt={BatteryVoltage:F2}V status=0x{Status:x} " +
        $"cliff=[{string.Join(",", CliffDataRaw)}] seg={CurrPathSegment}";
}

/// <summary>Bit flags of <see cref="RobotState.Status"/> (official C# Anki.Cozmo.RobotStatusFlag).</summary>
[Flags]
public enum RobotStatusFlag : uint
{
    IsMoving = 1u << 0, IsCarryingBlock = 1u << 1, IsPickingOrPlacing = 1u << 2, IsPickedUp = 1u << 3,
    IsBodyAccMode = 1u << 4, IsFalling = 1u << 5, IsAnimating = 1u << 6, IsPathing = 1u << 7,
    LiftInPos = 1u << 8, HeadInPos = 1u << 9, IsAnimBufferFull = 1u << 10, IsAnimatingIdle = 1u << 11,
    IsOnCharger = 1u << 12, IsCharging = 1u << 13, CliffDetected = 1u << 14, AreWheelsMoving = 1u << 15,
    IsChargerOos = 1u << 16,
}

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
    /// </summary>
    public string Format(IReadOnlyDictionary<int, string>? names, IReadOnlyDictionary<int, string>? formats)
    {
        string name = names is not null && names.TryGetValue(NameId, out var nm) ? nm : $"name#{NameId}";
        string body = formats is not null && formats.TryGetValue(FormatId, out var f) ? f : $"fmt#{FormatId}";
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
    /// <summary>Pack 8-bit RGB into the robot's 5-5-5 colour word (PyCozmo: 32768 colours; exact bit order unconfirmed on hardware).</summary>
    public static ushort Rgb(byte r, byte g, byte b) => (ushort)(((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3));
}

public sealed partial class SetHeadAngle
{
    public SetHeadAngle(float rad, float maxSpeed = 10f, float accel = 10f, float duration = 0f, byte actionId = 0)
    {
        AngleRad = rad; MaxSpeedRadPerSec = maxSpeed; AccelRadPerSec2 = accel; DurationSec = duration; ActionId = actionId;
    }
    public override string ToString() => $"SetHeadAngle {AngleRad:F3}rad speed={MaxSpeedRadPerSec} accel={AccelRadPerSec2} dur={DurationSec} action={ActionId}";
}

public sealed partial class SetLiftHeight
{
    public SetLiftHeight(float heightMm, float maxSpeed = 3f, float accel = 20f, float duration = 0f, byte actionId = 0)
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
    public SyncTime(uint timestamp, uint unknown = 0) { Timestamp = timestamp; Unknown = unknown; }
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
