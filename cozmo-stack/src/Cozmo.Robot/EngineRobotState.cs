using Cozmo.Protocol;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

/// <summary>The engine-to-game PoseStruct3d, distinct from the robot's odometry packet.</summary>
public readonly record struct EngineGamePose(float X, float Y, float Z,
    float Q0, float Q1, float Q2, float Q3, uint OriginId);

/// <summary>Immutable engine-to-game RobotState snapshot (109 packed bytes).</summary>
// fidelity: M1-053
public sealed record EngineRobotState(EngineGamePose Pose, float PoseAngleRad, float PosePitchRad,
    float LeftWheelSpeedMmps, float RightWheelSpeedMmps, float HeadAngleRad, float LiftHeightMm,
    float BatteryVoltage, float AccelX, float AccelY, float AccelZ, float GyroX, float GyroY, float GyroZ,
    int CarryingObjectId, int CarryingObjectOnTopId, int HeadTrackingObjectId, int LocalizedToObjectId,
    uint LastImageTimestamp, uint Status, byte GameStatus)
{
    /// <summary>ExternalInterface::RobotState::Pack 0x00711970..0x00711A62, without the message-union tag.</summary>
    public byte[] ToPackedBytes()
    {
        using var stream = new MemoryStream(109);
        using var w = new BinaryWriter(stream);
        w.Write(Pose.X); w.Write(Pose.Y); w.Write(Pose.Z);
        w.Write(Pose.Q0); w.Write(Pose.Q1); w.Write(Pose.Q2); w.Write(Pose.Q3); w.Write(Pose.OriginId);
        w.Write(PoseAngleRad); w.Write(PosePitchRad); w.Write(LeftWheelSpeedMmps); w.Write(RightWheelSpeedMmps);
        w.Write(HeadAngleRad); w.Write(LiftHeightMm); w.Write(BatteryVoltage);
        w.Write(AccelX); w.Write(AccelY); w.Write(AccelZ); w.Write(GyroX); w.Write(GyroY); w.Write(GyroZ);
        w.Write(CarryingObjectId); w.Write(CarryingObjectOnTopId); w.Write(HeadTrackingObjectId);
        w.Write(LocalizedToObjectId); w.Write(LastImageTimestamp); w.Write(Status); w.Write(GameStatus);
        return stream.ToArray();
    }
}

// Input owned by localization (M11-053/055), before the M1 projection narrows the quaternion.
internal readonly record struct EngineLocalizationPose(float X, float Y, float Z,
    double Q0, double Q1, double Q2, double Q3, uint OriginId)
{
    // The existing localization supplier is planar; its missing pose-tree/vision transforms remain M11's.
    internal static EngineLocalizationPose FromPlanar(float x, float y, float z, float angle, uint origin)
    {
        var q = BeaconFloorGeometry.RotationAboutAxis(angle, 0f, 0f, 1f);
        return new(x, y, z, q.W, q.X, q.Y, q.Z, origin);
    }
}

public sealed partial class CozmoEngine
{
    /// <summary>Published after Robot::Update on every tick after the first time-synced RobotState.</summary>
    // fidelity: M1-053
    public event Action<EngineRobotState>? RobotStatePublished;
    internal Func<EngineLocalizationPose>? PublicationPose;
    internal Func<(int Carried, int Top)>? PublicationCarrying;
    internal Func<int>? PublicationHeadTracking;
    internal Func<float>? PublicationHeadAngle;
    internal Func<int>? PublicationLocalizedTo;
    internal Func<uint>? PublicationImageTimestamp;
    internal Func<(byte Localized, sbyte OffTreads)>? PublicationGameStatus;

    // fidelity: M1-053
    // P2a-P2m: all supplied fields come from their owning components. No incoming packet forwarding.
    private void PublishRobotState(EngineRobot robot)
    {
        if (!robot.FirstFullStateHandled) return;
        // The offline harness opens the update gate before supplying its first packet.
        if (robot.StoredState is not { } state) return;
        var p = PublicationPose!();
        var carry = PublicationCarrying!();
        uint status = state.Status;
        if (robot.AnimationStateTag != 0) status |= robot.AnimationStateTag == 0xFF ? 0x840u : 0x40u;
        if (carry.Carried != -1) status |= 2u;
        var game = PublicationGameStatus!();
        float lift = 66f * MathF.Sin(state.LiftAngle); // 0x42840000, 0x0051818E
        lift = lift + 45f;                            // 0x42340000, 0x00518196
        lift = lift + 0f;                             // 0x00000000, 0x0051819E
        var snapshot = new EngineRobotState(new(p.X, p.Y, p.Z, (float)p.Q0, (float)p.Q1, (float)p.Q2, (float)p.Q3, p.OriginId),
            PublicationAngle(p), state.Pose.Pitch, state.LwheelSpeedMmps, state.RwheelSpeedMmps,
            PublicationHeadAngle!(), lift, state.BatteryVoltage,
            state.Accel.X, state.Accel.Y, state.Accel.Z, state.Gyro.X, state.Gyro.Y, state.Gyro.Z,
            carry.Carried, carry.Carried == -1 ? -1 : carry.Top, PublicationHeadTracking!(),
            PublicationLocalizedTo!(), PublicationImageTimestamp!(), status,
            (byte)(game.Localized != 0 && game.OffTreads == 0 ? 1 : 0));
        FanOut(RobotStatePublished, snapshot);
    }

    // fidelity: M1-053
    // Rotation3d::GetAngleAroundZaxis 0x0084AA1C..0x0084AAB8: double products, then float norms.
    internal static float PublicationAngle(EngineLocalizationPose p)
    {
        double xx = p.Q1 * p.Q1, zz = p.Q3 * p.Q3, yy = p.Q2 * p.Q2;
        double zw = p.Q3 * p.Q0, xy = p.Q1 * p.Q2;
        double xzSum = xx + zz, yzSum = yy + zz;
        double minus = xy - zw, plus = xy + zw;
        double twiceYz = yzSum + yzSum, twiceXz = xzSum + xzSum;
        float a = (float)(minus + minus), b = (float)(1.0 - twiceXz);
        float c = (float)(plus + plus), d = (float)(1.0 - twiceYz);
        float n1 = a * a + b * b, n2 = c * c + d * d;
        var operands = PublicationAngleOperands(a, b, c, d, n1, n2);
        float angle = MathF.Atan2(operands.Y, operands.X);
        return CozmoMotion.RescaleRadians(angle);
    }

    // ARM vcmpe/ble also takes the unordered branch (N != V).
    internal static (float Y, float X) PublicationAngleOperands(float a, float b, float c, float d, float n1, float n2)
        => !(n2 > n1) ? (-a, b) : (c, d);
}
