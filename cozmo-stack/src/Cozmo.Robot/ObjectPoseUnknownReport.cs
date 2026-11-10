using System.Buffers.Binary;

namespace Cozmo.Robot;

/// <summary>A native report that an object's pose became unknown.</summary>
public sealed record ObjectPoseUnknownReport(uint ObjectId);

public sealed partial class CozmoEngine
{
    /// <summary>Raised for each native object-pose-unknown report.</summary>
    public event Action<ObjectPoseUnknownReport>? ObjectPoseUnknown;
    internal void PublishObjectPoseUnknown(uint id)
    {
        byte[] body = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(body, id);
        var report = new ObjectPoseUnknownReport(id);
        DeliverAnimationReportToApp(0x52, body, CaptureAppEvent(ObjectPoseUnknown, report));
        AnimationMessages.Deliver(0x52, true, report);
    }
}
