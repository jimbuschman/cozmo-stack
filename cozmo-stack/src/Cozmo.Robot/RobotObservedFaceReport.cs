using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot;

/// <summary>A validity flag and the two values of a face attribute.</summary>
public readonly record struct FaceAttributePair(bool Valid, float First, float Second);

/// <summary>The source-backed stock face observation delivered by the engine.</summary>
public sealed record RobotObservedFaceReport(int FaceId, uint Timestamp, EngineGamePose Pose, FaceRect Rect, string Name,
    IReadOnlyList<Vec2> LeftEye, IReadOnlyList<Vec2> RightEye, IReadOnlyList<Vec2> Nose, IReadOnlyList<Vec2> Mouth)
{
    /// <summary>The disabled optional modes report neutral expression and zero scores.</summary>
    public FacialExpression Expression => FacialExpression.Neutral;
    public FaceAttributePair Smile => default;
    public FaceAttributePair Gaze => default;
    public FaceAttributePair Blink => default;
    public IReadOnlyList<byte> ExpressionScores { get; } = Array.AsReadOnly(new byte[5]);

    internal byte[] PackBody()
    {
        var w = new CladWriter();
        w.I32(FaceId).U32(Timestamp).F32(Pose.X).F32(Pose.Y).F32(Pose.Z)
            .F32(Pose.Q0).F32(Pose.Q1).F32(Pose.Q2).F32(Pose.Q3).U32(Pose.OriginId);
        w.F32((float)Rect.X).F32((float)Rect.Y).F32((float)Rect.Width).F32((float)Rect.Height).String8(Name).I8((sbyte)Expression);
        foreach (var pair in new[] { Smile, Gaze, Blink }) w.Bool(pair.Valid).F32(pair.First).F32(pair.Second);
        foreach (byte score in ExpressionScores) w.U8(score);
        foreach (var points in new[] { LeftEye, RightEye, Nose, Mouth })
        {
            w.U8(checked((byte)points.Count));
            foreach (var point in points) w.F32((float)point.X).F32((float)point.Y);
        }
        return w.ToArray();
    }
}

public sealed partial class CozmoEngine
{
    /// <summary>Raised for a complete native robot-observed-face report.</summary>
    public event Action<RobotObservedFaceReport>? RobotObservedFace;

    internal void PublishRobotObservedFace(RobotObservedFaceReport report)
        => DeliverAnimationReportToApp(0x46, report.PackBody(), CaptureAppEvent(RobotObservedFace, report));
}
