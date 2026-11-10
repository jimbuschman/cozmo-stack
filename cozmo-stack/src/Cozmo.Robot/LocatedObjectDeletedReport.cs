using System.Buffers.Binary;

namespace Cozmo.Robot;

/// <summary>A native report that a located object was deleted.</summary>
public sealed record LocatedObjectDeletedReport(uint ObjectId);

public sealed partial class CozmoEngine
{
    /// <summary>Raised when the engine reports deleting a current-origin object.</summary>
    public event Action<LocatedObjectDeletedReport>? LocatedObjectDeleted;

    internal void PublishLocatedObjectDeleted(uint id)
    {
        byte[] body = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(body, id);
        DeliverAnimationReportToApp(0x4C, body, CaptureAppEvent(LocatedObjectDeleted, new LocatedObjectDeletedReport(id)));
        AnimationMessages.Deliver(0x4C, true, id);
    }
}
