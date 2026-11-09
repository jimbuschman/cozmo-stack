using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>The immutable engine-to-game RobotObservedObject report for an observed light cube.</summary>
public sealed record RobotObservedObjectReport(uint Timestamp, uint Family, uint Type, uint ObjectId,
    float ImageX, float ImageY, float ImageWidth, float ImageHeight, EngineGamePose Pose,
    float TopOrientationRad, bool IsActive)
{
    /// <summary>Message body from RobotObservedObject::Pack 0x007131E2, without its union tag.</summary>
    public byte[] PackBody() => new CladWriter().U32(Timestamp).U32(Family).U32(Type).U32(ObjectId)
        .F32(ImageX).F32(ImageY).F32(ImageWidth).F32(ImageHeight)
        .F32(Pose.X).F32(Pose.Y).F32(Pose.Z).F32(Pose.Q0).F32(Pose.Q1).F32(Pose.Q2).F32(Pose.Q3).U32(Pose.OriginId)
        .F32(TopOrientationRad).U8(IsActive ? (byte)1 : (byte)0).ToArray();
}

public sealed partial class CozmoEngine
{
    /// <summary>Light-cube observations published by the engine after a confirmed vision update.</summary>
    public event Action<RobotObservedObjectReport>? RobotObservedObject;

    internal void DeliverObservedCubeToApp(RobotObservedObjectReport report) =>
        DeliverAnimationReportToApp(0x44, report.PackBody(), CaptureAppEvent(RobotObservedObject, report));
}
