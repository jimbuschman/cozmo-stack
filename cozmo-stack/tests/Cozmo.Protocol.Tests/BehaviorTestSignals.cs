using Cozmo.Robot.Behavior;

namespace Cozmo.Protocol.Tests;

internal static class BehaviorTestSignals
{
    public static void WaitForPostedWork(SteppedBehavior behavior)
    {
        using var posted = new ManualResetEventSlim();
        void Signal() => posted.Set();
        behavior.WorkPosted += Signal;
        try
        {
            if (!behavior.HasPostedWork && !posted.Wait(TimeSpan.FromMinutes(2)))
                throw new TimeoutException("The action never posted its completion callback.");
        }
        finally { behavior.WorkPosted -= Signal; }
    }
}
