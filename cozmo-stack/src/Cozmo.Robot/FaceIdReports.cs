using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot;

/// <summary>An observed face ID change reported by the engine.</summary>
public sealed record RobotChangedObservedFaceIdReport(int OldId, int NewId);
/// <summary>A face deletion reported by the engine.</summary>
public sealed record RobotDeletedFaceReport(int FaceId);

public sealed partial class CozmoEngine
{
    public event Action<RobotChangedObservedFaceIdReport>? RobotChangedObservedFaceId;
    public event Action<RobotDeletedFaceReport>? RobotDeletedFace;

    internal void PublishFaceIdReport(ushort tag, object payload)
    {
        var writer=new CladWriter();
        if(tag==0x47 && payload is NativeChangedFace changed)
        {
            var report=new RobotChangedObservedFaceIdReport(changed.OldId,changed.NewId);
            writer.I32(changed.OldId).I32(changed.NewId);
            DeliverAnimationReportToApp(tag,writer.ToArray(),CaptureAppEvent(RobotChangedObservedFaceId,report));
        }
        else if(tag==0x4B && payload is NativeDeletedFace deleted)
        {
            var report=new RobotDeletedFaceReport(deleted.Id);
            writer.I32(deleted.Id);
            DeliverAnimationReportToApp(tag,writer.ToArray(),CaptureAppEvent(RobotDeletedFace,report));
        }
        else throw new ArgumentException("Unsupported face report",nameof(payload));
        AnimationMessages.Deliver(tag,true,payload);
    }
}
