namespace Cozmo.Protocol;

/// <summary>
/// A CLAD robot message: one byte tag (RobotMessageId, the EngineToRobot/RobotToEngine union tag) followed by
/// the packed struct. Field widths of every typed message below were recovered from the official
/// <c>Type::Unpack(CLAD::SafeMessageBuffer)</c> routines in libcozmoEngine.so
/// (re-analysis/protocol/robot_msg_field_widths_official.json); field NAMES come from PyCozmo where its
/// declared widths matched, and are marked "unverified" where they are a guess.
/// </summary>
public abstract class RobotMessage
{
    public abstract RobotMessageId Id { get; }
    public abstract void WriteBody(CladWriter w);

    public byte[] ToBytes()
    {
        var w = new CladWriter().U8((byte)Id);
        WriteBody(w);
        return w.ToArray();
    }

    public MessageInfo? Info => MessageCatalog.Lookup((byte)Id);

    private static readonly Dictionary<RobotMessageId, Func<CladReader, RobotMessage>> Parsers = new()
    {
        [RobotMessageId.State] = RobotState.Read,
        [RobotMessageId.RobotAvailable] = RobotAvailable.Read,
        [RobotMessageId.FirmwareVersion] = FirmwareVersion.Read,
        [RobotMessageId.MfgId] = ManufacturingId.Read,
        [RobotMessageId.SyncTimeAck] = _ => new SyncTimeAck(),
        [RobotMessageId.MotorActionAck] = r => new MotorActionAck(r.U8()),
        [RobotMessageId.AnimState] = AnimationState.Read,
        [RobotMessageId.RobotPoked] = _ => new RobotPoked(),
        [RobotMessageId.BackpackButton] = r => new BackpackButton(r.Bool()),
        [RobotMessageId.MotorCalibration] = r => new MotorCalibration(r.U8(), r.Bool(), r.Bool()),
        [RobotMessageId.ActiveObjectAvailable] = r => new ObjectAvailable(r.U32(), r.U32(), r.I8()),
        [RobotMessageId.FallingStarted] = r => new FallingStarted(r.U32()),
        [RobotMessageId.FallingStopped] = r => new FallingStopped(r.U32(), r.U32(), r.U32()),
        [RobotMessageId.RobotStopped] = r => new RobotStopped(r.U8()),
        // engine->robot messages (so captures of the official app decode too)
        [RobotMessageId.SyncTime] = r => new SyncTime(r.U32(), r.U32()),
        [RobotMessageId.GetMfgInfo] = _ => new GetManufacturingInfo(),
        [RobotMessageId.InitAnimController] = _ => new InitAnimController(),
        [RobotMessageId.HeadAngle] = r => new SetHeadAngle(r.F32(), r.F32(), r.F32(), r.F32(), r.U8()),
        [RobotMessageId.LiftHeight] = r => new SetLiftHeight(r.F32(), r.F32(), r.F32(), r.F32(), r.U8()),
        [RobotMessageId.Drive] = r => new DriveWheels(r.F32(), r.F32(), r.F32(), r.F32()),
        [RobotMessageId.Stop] = _ => new StopAllMotors(),
        [RobotMessageId.SetHeadlight] = r => new SetHeadlight(r.Bool()),
        [RobotMessageId.EnableStopOnCliff] = r => new EnableStopOnCliff(r.Bool()),
        [RobotMessageId.SetBackpackLightsMiddle] = SetBackpackLightsMiddle.Read,
        [RobotMessageId.AbsLocalizationUpdate] = r => new AbsoluteLocalizationUpdate(r.U32(), r.U32(), r.U32(), r.F32(), r.F32(), r.U32()),
        [RobotMessageId.ImageRequest] = r => new ImageRequest(r.U8(), r.U8()),
    };

    /// <summary>Parses tag + body. Unknown or short messages become <see cref="RawRobotMessage"/> (never throws for unknown tags).</summary>
    public static RobotMessage Parse(ReadOnlyMemory<byte> data)
    {
        if (data.Length == 0) throw new FormatException("empty robot message");
        var id = (RobotMessageId)data.Span[0];
        var body = data[1..];
        if (Parsers.TryGetValue(id, out var p))
        {
            try
            {
                var r = new CladReader(body);
                var m = p(r);
                if (r.Remaining == 0) return m;
                return new RawRobotMessage(id, body.ToArray(), $"typed parse left {r.Remaining} trailing bytes");
            }
            catch (FormatException e) { return new RawRobotMessage(id, body.ToArray(), e.Message); }
        }
        return new RawRobotMessage(id, body.ToArray(), null);
    }

    public override string ToString() => $"{Info?.Member ?? "?"}(0x{(byte)Id:x2})";
}

/// <summary>Any message we do not (yet) model: tag + opaque body. Round-trips byte-exactly.</summary>
public sealed class RawRobotMessage : RobotMessage
{
    public override RobotMessageId Id { get; }
    public byte[] Body { get; }
    public string? ParseNote { get; }
    public RawRobotMessage(RobotMessageId id, byte[] body, string? note = null) { Id = id; Body = body; ParseNote = note; }
    public override void WriteBody(CladWriter w) => w.Bytes(Body);
    public override string ToString() => $"{Info?.Member ?? "Unknown"}(0x{(byte)Id:x2}) raw[{Body.Length}] {Hex.Dump(Body, 32)}{(ParseNote is null ? "" : " !" + ParseNote)}";
}

// ---------------------------------------------------------------- robot -> engine

/// <summary>RobotToEngine.state 0xF0, 91 bytes. Official Unpack: u32 u32 u32 RobotPose(5×f32) f32×4 AccelData GyroData f32 u32 u16[4] u16 u8.</summary>
public sealed class RobotState : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.State;
    public uint Timestamp; public uint PoseFrameId; public uint PoseOriginId;
    public float PoseX, PoseY, PoseZ, PoseAngleRad, PosePitchRad;
    public float LeftWheelSpeedMmps, RightWheelSpeedMmps, HeadAngleRad, LiftHeightMm;
    public float AccelX, AccelY, AccelZ, GyroX, GyroY, GyroZ;
    public float BatteryVoltage; public uint Status;
    public ushort[] CliffDataRaw = new ushort[4];
    public ushort BackpackTouchSensorRaw; public byte CurrPathSegment;

    public static RobotState Read(CladReader r) => new()
    {
        Timestamp = r.U32(), PoseFrameId = r.U32(), PoseOriginId = r.U32(),
        PoseX = r.F32(), PoseY = r.F32(), PoseZ = r.F32(), PoseAngleRad = r.F32(), PosePitchRad = r.F32(),
        LeftWheelSpeedMmps = r.F32(), RightWheelSpeedMmps = r.F32(), HeadAngleRad = r.F32(), LiftHeightMm = r.F32(),
        AccelX = r.F32(), AccelY = r.F32(), AccelZ = r.F32(), GyroX = r.F32(), GyroY = r.F32(), GyroZ = r.F32(),
        BatteryVoltage = r.F32(), Status = r.U32(), CliffDataRaw = r.U16Array(4),
        BackpackTouchSensorRaw = r.U16(), CurrPathSegment = r.U8(),
    };
    public override void WriteBody(CladWriter w)
    {
        w.U32(Timestamp).U32(PoseFrameId).U32(PoseOriginId).F32(PoseX).F32(PoseY).F32(PoseZ).F32(PoseAngleRad).F32(PosePitchRad)
         .F32(LeftWheelSpeedMmps).F32(RightWheelSpeedMmps).F32(HeadAngleRad).F32(LiftHeightMm)
         .F32(AccelX).F32(AccelY).F32(AccelZ).F32(GyroX).F32(GyroY).F32(GyroZ).F32(BatteryVoltage).U32(Status);
        foreach (var c in CliffDataRaw) w.U16(c);
        w.U16(BackpackTouchSensorRaw).U8(CurrPathSegment);
    }
    public bool Has(RobotStatusFlag f) => (Status & (uint)f) != 0;
    public override string ToString() =>
        $"RobotState t={Timestamp} pose=({PoseX:F1},{PoseY:F1},{PoseZ:F1} a={PoseAngleRad:F2}) head={HeadAngleRad:F3}rad lift={LiftHeightMm:F1}mm batt={BatteryVoltage:F2}V status=0x{Status:x} cliff=[{string.Join(",", CliffDataRaw)}] seg={CurrPathSegment}";
}

/// <summary>Bit flags of RobotState.Status (official C# Anki.Cozmo.RobotStatusFlag; bit positions from PyCozmo/observed usage).</summary>
[Flags]
public enum RobotStatusFlag : uint
{
    IsMoving = 1 << 0, IsCarryingBlock = 1 << 1, IsPickingOrPlacing = 1 << 2, IsPickedUp = 1 << 3,
    IsBodyAccMode = 1 << 4, IsFalling = 1 << 5, IsAnimating = 1 << 6, IsPathing = 1 << 7,
    LiftInPos = 1 << 8, HeadInPos = 1 << 9, IsAnimBufferFull = 1 << 10, IsAnimatingIdle = 1 << 11,
    IsOnCharger = 1 << 12, IsCharging = 1 << 13, CliffDetected = 1 << 14, AreWheelsMoving = 1 << 15,
    IsChargerOos = 1 << 16,
}

/// <summary>RobotToEngine.robotAvailable 0xC9, 6 bytes (official: u32 u16). PyCozmo "HardwareInfo" (serial_number_head, unknown1, unknown2).</summary>
public sealed class RobotAvailable : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.RobotAvailable;
    public uint SerialNumberHead; public ushort HardwareRevisionUnverified;
    public static RobotAvailable Read(CladReader r) => new() { SerialNumberHead = r.U32(), HardwareRevisionUnverified = r.U16() };
    public override void WriteBody(CladWriter w) => w.U32(SerialNumberHead).U16(HardwareRevisionUnverified);
    public override string ToString() => $"RobotAvailable serial=0x{SerialNumberHead:x8} hw=0x{HardwareRevisionUnverified:x4}";
}

/// <summary>RobotToEngine.firmwareVersion 0xEE, variable (official: u16 + string). The string is the JSON signature header of cozmo.safe.</summary>
public sealed class FirmwareVersion : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.FirmwareVersion;
    public ushort LeadingU16; public string SignatureJson = ""; public string LayoutNote = "";
    public int? Version => TryInt("\"version\"");
    public string? EngineToRobotHash => TryStr("\"messageEngineToRobotHash\"");
    public string? RobotToEngineHash => TryStr("\"messageRobotToEngineHash\"");
    public static FirmwareVersion Read(CladReader r)
    {
        var m = new FirmwareVersion { LeadingU16 = r.U16() };
        // Official Unpack reads a u16 then a string. CLAD string[uint_16] would make that u16 the length itself.
        if (m.LeadingU16 == r.Remaining) { m.SignatureJson = System.Text.Encoding.UTF8.GetString(r.Rest()); m.LayoutNote = "string16"; }
        else if (r.Remaining >= 1 && r.Remaining - 1 == 0) { m.SignatureJson = ""; m.LayoutNote = "u16+empty"; }
        else { m.SignatureJson = System.Text.Encoding.UTF8.GetString(r.Rest()).TrimEnd('\0'); m.LayoutNote = "u16+rest"; }
        return m;
    }
    public override void WriteBody(CladWriter w) { var b = System.Text.Encoding.UTF8.GetBytes(SignatureJson); w.U16((ushort)b.Length).Bytes(b); }
    private string? TryStr(string key) { int i = SignatureJson.IndexOf(key, StringComparison.Ordinal); if (i < 0) return null; int q = SignatureJson.IndexOf('"', i + key.Length + 1); if (q < 0) return null; int e = SignatureJson.IndexOf('"', q + 1); return e < 0 ? null : SignatureJson[(q + 1)..e]; }
    private int? TryInt(string key) { int i = SignatureJson.IndexOf(key, StringComparison.Ordinal); if (i < 0) return null; int c = SignatureJson.IndexOf(':', i); int s = c + 1; while (s < SignatureJson.Length && SignatureJson[s] == ' ') s++; int e = s; while (e < SignatureJson.Length && char.IsDigit(SignatureJson[e])) e++; return int.TryParse(SignatureJson.AsSpan(s, e - s), out var v) ? v : null; }
    public override string ToString() => $"FirmwareVersion v{Version?.ToString() ?? "?"} ({LayoutNote}) {SignatureJson}";
}

/// <summary>RobotToEngine.mfgId 0xED, 12 bytes (official: u32 u32 u32). PyCozmo "BodyInfo": serial_number, body_hw_version, body_color.</summary>
public sealed class ManufacturingId : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.MfgId;
    public uint SerialNumber, BodyHwVersion, BodyColor;
    public static ManufacturingId Read(CladReader r) => new() { SerialNumber = r.U32(), BodyHwVersion = r.U32(), BodyColor = r.U32() };
    public override void WriteBody(CladWriter w) => w.U32(SerialNumber).U32(BodyHwVersion).U32(BodyColor);
    public override string ToString() => $"ManufacturingId serial=0x{SerialNumber:x8} bodyHw={BodyHwVersion} color={BodyColor}";
}

public sealed class SyncTimeAck : RobotMessage { public override RobotMessageId Id => RobotMessageId.SyncTimeAck; public override void WriteBody(CladWriter w) { } }
public sealed class RobotPoked : RobotMessage { public override RobotMessageId Id => RobotMessageId.RobotPoked; public override void WriteBody(CladWriter w) { } }
public sealed record class MotorActionAckData(byte ActionId);
public sealed class MotorActionAck : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.MotorActionAck; public byte ActionId;
    public MotorActionAck(byte id) { ActionId = id; }
    public override void WriteBody(CladWriter w) => w.U8(ActionId);
    public override string ToString() => $"MotorActionAck action={ActionId}";
}
/// <summary>RobotToEngine.animState 0xF1, 15 bytes (official u32 u32 u32 u8 u8 u8).</summary>
public sealed class AnimationState : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.AnimState;
    public uint Timestamp; public int NumAnimBytesPlayed, NumAudioFramesPlayed; public byte EnabledAnimTracks, Tag, ClientDropCount;
    public static AnimationState Read(CladReader r) => new() { Timestamp = r.U32(), NumAnimBytesPlayed = r.I32(), NumAudioFramesPlayed = r.I32(), EnabledAnimTracks = r.U8(), Tag = r.U8(), ClientDropCount = r.U8() };
    public override void WriteBody(CladWriter w) => w.U32(Timestamp).I32(NumAnimBytesPlayed).I32(NumAudioFramesPlayed).U8(EnabledAnimTracks).U8(Tag).U8(ClientDropCount);
    public override string ToString() => $"AnimationState t={Timestamp} animBytes={NumAnimBytesPlayed} audioFrames={NumAudioFramesPlayed} tracks=0x{EnabledAnimTracks:x2} tag={Tag} drops={ClientDropCount}";
}
public sealed class BackpackButton : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.BackpackButton; public bool Pressed;
    public BackpackButton(bool p) { Pressed = p; }
    public override void WriteBody(CladWriter w) => w.Bool(Pressed);
    public override string ToString() => $"BackpackButton pressed={Pressed}";
}
public sealed class MotorCalibration : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.MotorCalibration; public byte MotorId; public bool CalibStarted, AutoStarted;
    public MotorCalibration(byte m, bool c, bool a) { MotorId = m; CalibStarted = c; AutoStarted = a; }
    public override void WriteBody(CladWriter w) => w.U8(MotorId).Bool(CalibStarted).Bool(AutoStarted);
    public override string ToString() => $"MotorCalibration motor={MotorId} started={CalibStarted} auto={AutoStarted}";
}
public sealed class ObjectAvailable : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.ActiveObjectAvailable; public uint FactoryId, ObjectType; public sbyte Rssi;
    public ObjectAvailable(uint f, uint t, sbyte r) { FactoryId = f; ObjectType = t; Rssi = r; }
    public override void WriteBody(CladWriter w) => w.U32(FactoryId).U32(ObjectType).I8(Rssi);
    public override string ToString() => $"ObjectAvailable factory=0x{FactoryId:x8} type={ObjectType} rssi={Rssi}";
}
public sealed class FallingStarted : RobotMessage { public override RobotMessageId Id => RobotMessageId.FallingStarted; public uint TimestampUnverified; public FallingStarted(uint t) { TimestampUnverified = t; } public override void WriteBody(CladWriter w) => w.U32(TimestampUnverified); }
public sealed class FallingStopped : RobotMessage { public override RobotMessageId Id => RobotMessageId.FallingStopped; public uint A, B, C; public FallingStopped(uint a, uint b, uint c) { A = a; B = b; C = c; } public override void WriteBody(CladWriter w) => w.U32(A).U32(B).U32(C); }
public sealed class RobotStopped : RobotMessage { public override RobotMessageId Id => RobotMessageId.RobotStopped; public byte ReasonUnverified; public RobotStopped(byte r) { ReasonUnverified = r; } public override void WriteBody(CladWriter w) => w.U8(ReasonUnverified); public override string ToString() => $"RobotStopped reason={ReasonUnverified}"; }

// ---------------------------------------------------------------- engine -> robot

/// <summary>EngineToRobot.getMfgInfo 0x25, empty. Robot answers with <see cref="ManufacturingId"/>. PyCozmo calls this "Enable".</summary>
public sealed class GetManufacturingInfo : RobotMessage { public override RobotMessageId Id => RobotMessageId.GetMfgInfo; public override void WriteBody(CladWriter w) { } }
/// <summary>EngineToRobot.syncTime 0x4B, 8 bytes (official u32 u32). PyCozmo: timestamp, unknown. Robot answers <see cref="SyncTimeAck"/> and starts RobotState streaming.</summary>
public sealed class SyncTime : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.SyncTime; public uint Timestamp, UnknownUnverified;
    public SyncTime(uint t, uint u = 0) { Timestamp = t; UnknownUnverified = u; }
    public override void WriteBody(CladWriter w) => w.U32(Timestamp).U32(UnknownUnverified);
}
/// <summary>EngineToRobot.initAnimController 0x9F, empty. PyCozmo calls this "EnableAnimationState".</summary>
public sealed class InitAnimController : RobotMessage { public override RobotMessageId Id => RobotMessageId.InitAnimController; public override void WriteBody(CladWriter w) { } }
/// <summary>EngineToRobot.headAngle 0x37 (SetHeadAngle), 17 bytes: f32 angle_rad, f32 max_speed_rad_per_sec, f32 accel_rad_per_sec2, f32 duration_sec, u8 action_id. Robot acks with <see cref="MotorActionAck"/>.</summary>
public sealed class SetHeadAngle : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.HeadAngle;
    public float AngleRad, MaxSpeedRadPerSec, AccelRadPerSec2, DurationSec; public byte ActionId;
    public SetHeadAngle(float a, float s = 10f, float acc = 10f, float d = 0f, byte id = 0) { AngleRad = a; MaxSpeedRadPerSec = s; AccelRadPerSec2 = acc; DurationSec = d; ActionId = id; }
    public override void WriteBody(CladWriter w) => w.F32(AngleRad).F32(MaxSpeedRadPerSec).F32(AccelRadPerSec2).F32(DurationSec).U8(ActionId);
    public override string ToString() => $"SetHeadAngle {AngleRad:F3}rad speed={MaxSpeedRadPerSec} accel={AccelRadPerSec2} dur={DurationSec} action={ActionId}";
}
/// <summary>EngineToRobot.liftHeight 0x36 (SetLiftHeight), 17 bytes, same shape as SetHeadAngle with height_mm.</summary>
public sealed class SetLiftHeight : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.LiftHeight;
    public float HeightMm, MaxSpeedRadPerSec, AccelRadPerSec2, DurationSec; public byte ActionId;
    public SetLiftHeight(float h, float s = 3f, float acc = 20f, float d = 0f, byte id = 0) { HeightMm = h; MaxSpeedRadPerSec = s; AccelRadPerSec2 = acc; DurationSec = d; ActionId = id; }
    public override void WriteBody(CladWriter w) => w.F32(HeightMm).F32(MaxSpeedRadPerSec).F32(AccelRadPerSec2).F32(DurationSec).U8(ActionId);
}
/// <summary>EngineToRobot.drive 0x32 (DriveWheels), 16 bytes: 4 × f32 (lwheel_speed_mmps, rwheel_speed_mmps, lwheel_accel_mmps2, rwheel_accel_mmps2).</summary>
public sealed class DriveWheels : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.Drive; public float LeftSpeed, RightSpeed, LeftAccel, RightAccel;
    public DriveWheels(float l, float r, float la = 0, float ra = 0) { LeftSpeed = l; RightSpeed = r; LeftAccel = la; RightAccel = ra; }
    public override void WriteBody(CladWriter w) => w.F32(LeftSpeed).F32(RightSpeed).F32(LeftAccel).F32(RightAccel);
}
public sealed class StopAllMotors : RobotMessage { public override RobotMessageId Id => RobotMessageId.Stop; public override void WriteBody(CladWriter w) { } }
public sealed class SetHeadlight : RobotMessage { public override RobotMessageId Id => RobotMessageId.SetHeadlight; public bool Enable; public SetHeadlight(bool e) { Enable = e; } public override void WriteBody(CladWriter w) => w.Bool(Enable); }
public sealed class EnableStopOnCliff : RobotMessage { public override RobotMessageId Id => RobotMessageId.EnableStopOnCliff; public bool Enable; public EnableStopOnCliff(bool e) { Enable = e; } public override void WriteBody(CladWriter w) => w.Bool(Enable); }
/// <summary>EngineToRobot.imageRequest 0x4C, 2 bytes (official u8 u8): ImageSendMode {Off=0, Stream=1, SingleShot=2} (C# enum) + resolution (unverified).</summary>
public sealed class ImageRequest : RobotMessage { public override RobotMessageId Id => RobotMessageId.ImageRequest; public byte Mode, ResolutionUnverified; public ImageRequest(byte m, byte r = 0) { Mode = m; ResolutionUnverified = r; } public override void WriteBody(CladWriter w) => w.U8(Mode).U8(ResolutionUnverified); }
/// <summary>EngineToRobot.absLocalizationUpdate 0x45, 24 bytes (official 6 × 4). PyCozmo "SetOrigin": unknown0 u32, pose_frame_id u32, pose_origin_id u32 (=1), pose_x f32, pose_y f32, unknown5 u32 (=0x80000000).</summary>
public sealed class AbsoluteLocalizationUpdate : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.AbsLocalizationUpdate;
    public uint Unknown0, PoseFrameId, PoseOriginId; public float PoseX, PoseY; public uint Unknown5;
    public AbsoluteLocalizationUpdate(uint u0, uint frame, uint origin, float x, float y, uint u5) { Unknown0 = u0; PoseFrameId = frame; PoseOriginId = origin; PoseX = x; PoseY = y; Unknown5 = u5; }
    public static AbsoluteLocalizationUpdate PyCozmoDefault() => new(0, 0, 1, 0, 0, 0x80000000);
    public override void WriteBody(CladWriter w) => w.U32(Unknown0).U32(PoseFrameId).U32(PoseOriginId).F32(PoseX).F32(PoseY).U32(Unknown5);
}

/// <summary>Anki::Cozmo::LightState, 10 bytes (official Size()=10: u16 u16 u8 u8 u8 u8 i16). Colors are RGB 5-5-5 packed (PyCozmo).</summary>
public readonly record struct LightState(ushort OnColor, ushort OffColor, byte OnFrames, byte OffFrames, byte TransitionOnFrames, byte TransitionOffFrames, short Offset)
{
    public const int Size = 10;
    public static LightState Off => new(0, 0, 0, 0, 0, 0, 0);
    public static LightState Solid(ushort color) => new(color, color, 0, 0, 0, 0, 0);
    /// <summary>Pack 8-bit RGB into the 5-5-5 format used on the wire (0bRRRRRGGGGGBBBBB0? — PyCozmo: 32768 colours, verify on hardware).</summary>
    public static ushort Rgb(byte r, byte g, byte b) => (ushort)(((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3));
    public static LightState Read(CladReader r) => new(r.U16(), r.U16(), r.U8(), r.U8(), r.U8(), r.U8(), r.I16());
    public void Write(CladWriter w) => w.U16(OnColor).U16(OffColor).U8(OnFrames).U8(OffFrames).U8(TransitionOnFrames).U8(TransitionOffFrames).I16(Offset);
}

/// <summary>EngineToRobot.setBackpackLightsMiddle 0x03: 3 × LightState (top, middle, bottom RGB LEDs) + u8 (PyCozmo "unknown"; official Size() is variable). 31 bytes as PyCozmo sends it.</summary>
public sealed class SetBackpackLightsMiddle : RobotMessage
{
    public override RobotMessageId Id => RobotMessageId.SetBackpackLightsMiddle;
    public LightState[] States = { LightState.Off, LightState.Off, LightState.Off }; public byte TrailingUnverified;
    public static SetBackpackLightsMiddle Read(CladReader r) { var m = new SetBackpackLightsMiddle(); for (int i = 0; i < 3; i++) m.States[i] = LightState.Read(r); m.TrailingUnverified = r.U8(); return m; }
    public override void WriteBody(CladWriter w) { foreach (var s in States) s.Write(w); w.U8(TrailingUnverified); }
}
