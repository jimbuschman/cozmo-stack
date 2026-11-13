using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// RETIRE-LEGACY-DRIVE-API-1 (operator 2026-11-13): the public DriveToPoseAction(m) constructor and its settable Goal/Goals are retired, so tests build the
/// native action: DriveToPoseAction(m, head, manual) and SetNativeGoals (the SetGoals 0x0055A6D0 copy). A goal must be under the robot's current world origin,
/// because DriveToPoseAction::Init re-expresses every goal w.r.t. Robot::GetWorldOrigin and fails 0x03000005 otherwise (I3, 0x0055A8AC..0x0055A91E). A test goal
/// without a parent is therefore given the current origin as its parent (its translation is the same numbers in that frame). This is a test input, not engine
/// behaviour; head and manual are the engine constructor's +0x79 and +0xA4 (I6, P4), chosen per test.
/// </summary>
internal static class NativeDriveTestGoals
{
    internal static DriveToPoseAction To(ManipulationSystem m, Pose3d goal, bool head = false, bool manual = false) => To(m, new[] { goal }, head, manual);

    /// <summary>For unit tests that call CheckIfDone(tick)/ReadyState directly (no Init, so no I3 conversion): the goal copy keeps the test's own frame.</summary>
    internal static DriveToPoseAction Unconverted(ManipulationSystem m, Pose3d goal, bool head = false, bool manual = false) => To(m, new[] { goal }, head, manual, parentToOrigin: false);

    internal static DriveToPoseAction To(ManipulationSystem m, IReadOnlyList<Pose3d> goals, bool head = false, bool manual = false, bool parentToOrigin = true)
    {
        var owner = m.Robot.Engine.Robot ?? throw new InvalidOperationException("test fixture has no native Robot");
        var origin = owner.GetNativeOrigin(owner.CurrentOriginId);
        var parented = goals.Select(g => { var copy = new Pose3d(g); if (parentToOrigin && !copy.HasParent) copy.SetParent(origin); return copy; }).ToList();
        try
        {
            var drive = new DriveToPoseAction(m, head, manual);
            drive.SetNativeGoals(parented);
            return drive;
        }
        finally { foreach (var p in parented) p.Dispose(); }
    }

    /// <summary>Queues a native runner on the robot's ActionList (QueueAction Now, as the retired DriveToPoseAction.RunAsync did) and completes with its result.</summary>
    internal static Task<ActionResult> RunOnActionList(ManipulationSystem m, Cozmo.Robot.ActionRunner runner)
    {
        var owner = m.Robot.Engine.Robot ?? throw new InvalidOperationException("test fixture has no native Robot");
        var done = new TaskCompletionSource<ActionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        runner.AddCompletionCallback(r => { if (r != Cozmo.Robot.EngineActionResult.Running) done.TrySetResult((ActionResult)r); });
        owner.ActionList.QueueAction(Cozmo.Robot.QueueActionPosition.Now, runner, 0);
        return done.Task;
    }
}
