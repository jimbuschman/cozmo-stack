using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class ScopedDriveToObjectOwnershipTests
{
    [Fact]
    public void CancelledParentCannotInitializeDriveOrPublishTraffic()
    {
        using var fixture = new PickupPyramidNativeExecutionTests.Fixture();
        fixture.Add(x:300);
        int mark = fixture.Rig.Mark();
        var drive = new NativeDriveToObjectRunner(fixture.M, fixture.Robot, 7, PreActionType.Docking, () => false);
        Assert.Equal(EngineActionResult.Cancelled, drive.Update());
        Assert.Null(drive.Drive);
        Assert.DoesNotContain(fixture.Rig.SentSince(mark), message => message is SetHeadAngle or SetBodyAngle or ClearPath or ExecutePath or AppendPathSegmentLine);
        Assert.True(drive.RetireNeverAdmittedTree());
    }

    [Fact]
    public void ReentrantSuccessorPathSurvivesOldDriveTeardown()
    {
        using var fixture = new PickupPyramidNativeExecutionTests.Fixture();
        var path = fixture.Rig.Robot.Motion.Path;
        path.SetStatus((int)DriveToPoseStatus.FollowingPath);
        var sent = new List<RobotMessage>();
        var old = new DriveToPoseAction(fixture.M, true, false, fixture.Robot, () => true);
        old.LifecycleCallObserved = step =>
        {
            if (step != "Abort") return;
            int result = path.ExecuteCustomPath(new PathSegment[] { new PathSegment.Line(0,0,10,0,100,200,500) },
                false, send: message => { sent.Add(message); return true; });
            Assert.Equal(0, result);
        };
        old.Prep();
        old.WatcherEnding();
        Assert.Contains(sent, message => message is ExecutePath);
        Assert.Equal((int)DriveToPoseStatus.WaitingToBeginPath, path.DriveToPoseStatus);
        Assert.Equal(1, sent.OfType<ClearPath>().Count());
    }
}
