namespace Cozmo.Robot;

internal readonly record struct NativeCliffEventFinished;

public sealed partial class CozmoEngine
{
    // fidelity: M7-019, M1-056
    // SendFinishedReactToCliffMessage6052D0..DA -> Robot.Broadcast (enqueue then synchronous local emit).
    // Union ctor723EE4 writes tag36; Size711F76 returns0. Keep the existing behavior host callback as well.
    internal void ReportCliffEventFinished(Action? hostCallback)
    {
        if(Robot?.HasExternalInterface==true)
        {
            DeliverAnimationReportToApp(0x36,Array.Empty<byte>());
            QueueAppBroadcast(hostCallback);
            AnimationMessages.Deliver(0x36,true,new NativeCliffEventFinished());
        }
        else QueueAppBroadcast(hostCallback);
        Behavior.SteppedBehavior.ReportMissing("M7-019 RobotCliffEventFinished tag0x36 native local subscriber continuity after E2G emit0x00662590 remains unverified; typed bus delivery is built, concrete native recipients are not certified");
    }
}
