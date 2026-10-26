using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

public sealed partial class CozmoRobot
{
    /// <summary>Requests the app's needs get-out action. The app action admission bridge remains unbuilt.</summary>
    public uint PlayNeedsGetOutAnimIfNeeded()
    {
        const string gap = "M15-074 QueueSingleAction union20 admission 0x005279A8/0x00527A78/0x00527A8C and app caller tag counter (Robot.cs:586-599) are not bound; the existing DriveToObject factory has no production app caller either. No tag or completion is fabricated";
        SteppedBehavior.ReportMissing(gap);
        throw new InvalidOperationException(gap);
    }
}
