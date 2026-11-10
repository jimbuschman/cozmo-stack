using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot;

/// <summary>A complete native-shaped pet observation.</summary>
public sealed record RobotObservedPetReport(int PetId, uint Timestamp, uint TimesObserved, float Score, FaceRect Rect, PetType Type)
{
    internal byte[] PackBody() => new CladWriter().I32(PetId).U32(Timestamp).U32(TimesObserved).F32(Score)
        .F32((float)Rect.X).F32((float)Rect.Y).F32((float)Rect.Width).F32((float)Rect.Height).U8((byte)Type).ToArray();
}

public sealed partial class CozmoEngine
{
    /// <summary>Raised for a native-shaped robot-observed-pet report.</summary>
    public event Action<RobotObservedPetReport>? RobotObservedPet;
    internal void PublishRobotObservedPet(RobotObservedPetReport report) =>
        DeliverAnimationReportToApp(0x48, report.PackBody(), CaptureAppEvent(RobotObservedPet, report));
}
