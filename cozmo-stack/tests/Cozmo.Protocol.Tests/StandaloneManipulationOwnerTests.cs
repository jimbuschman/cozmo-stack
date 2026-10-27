using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Xunit;
namespace Cozmo.Protocol.Tests;
public sealed class StandaloneManipulationOwnerTests
{
    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)]
    public void ActualRetirementClearsOnlyTheDetectorCreatedByThatRuntime(bool preexisting,bool replacement)
    {
        using var rig=new M4ControlTests.Rig();
        var supplied=new Cozmo.Robot.Vision.OverheadEdgesDetector();
        if(preexisting)rig.Robot.Vision.OverheadEdges=supplied;
        rig.ToSynced();rig.State();var constructed=rig.Robot.Vision.OverheadEdges;
        Assert.NotNull(constructed);if(preexisting)Assert.Same(supplied,constructed);
        if(replacement)rig.Robot.Vision.OverheadEdges=supplied;
        rig.Engine.Robots.RemoveRobot(1,false);
        if(preexisting||replacement)Assert.Same(supplied,rig.Robot.Vision.OverheadEdges);
        else Assert.Null(rig.Robot.Vision.OverheadEdges);
    }

    [Fact]
    public void ControlledClockOwnsPrivateWhiteboardAndBorrowsActualServicesWithoutReplacingHooks()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var canonical=rig.Engine.Robot!.NativeBehaviorRuntime!.Manipulation!;
        var send=rig.Robot.Motion.Path.SendPathMessage;var carrying=rig.Robot.Motion.IsCarryingObject;
        var mapClear=rig.Robot.Vision.World.MapClearQuad;var configurations=canonical.Docking.Configurations;
        double now=500;
        using var input=new Cozmo.Robot.Vision.Pose3d(Cozmo.Robot.Vision.Mat3.Identity,new Cozmo.Robot.Vision.Vec3(100,0,22));
        var inputTranslation=input.Translation;
        canonical.Whiteboard.SetFailedToUse(99,ObjectActionFailure.PickUpObject,input);
        var nativeHistory=canonical.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PickUpObject)[99].Single();
        AIWhiteboard retained=null!;
        using(var manual=ManipulationSystem.CreateStandalone(rig.Robot,rig.Robot.Vision,()=>now))
        {
            retained=manual.Whiteboard;
            Assert.NotSame(canonical.Whiteboard,manual.Whiteboard);
            manual.Whiteboard.SetFailedToUse(7,ObjectActionFailure.PickUpObject,input);
            Assert.Equal(500f,manual.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PickUpObject)[7].Single().TimeSec);
            Assert.Same(canonical.Paths,manual.Paths);Assert.Same(canonical.Follower,manual.Follower);
            Assert.Same(canonical.Docking,manual.Docking);Assert.Same(configurations,manual.Docking.Configurations);
            Assert.Equal(500,manual.ClockSec());manual.ClockSec=()=>1000;Assert.Equal(1000,manual.ClockSec());
            Assert.Equal(rig.Engine.Timer.SecondsF,canonical.ClockSec());
            Assert.Same(send,rig.Robot.Motion.Path.SendPathMessage);Assert.Same(carrying,rig.Robot.Motion.IsCarryingObject);
            Assert.Same(mapClear,rig.Robot.Vision.World.MapClearQuad);
        }
        Assert.Empty(retained.GetObjectFailureTable(ObjectActionFailure.PickUpObject));
        Assert.Equal(inputTranslation,input.Translation);
        Assert.Same(nativeHistory,canonical.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PickUpObject)[99].Single());
        Assert.Equal(inputTranslation,nativeHistory.Pose.Translation);
        Assert.Same(send,rig.Robot.Motion.Path.SendPathMessage);Assert.Same(carrying,rig.Robot.Motion.IsCarryingObject);
        Assert.Same(mapClear,rig.Robot.Vision.World.MapClearQuad);Assert.Same(configurations,canonical.Docking.Configurations);
        using var view=new ManipulationSystem(rig.Robot,rig.Robot.Vision);
        Assert.Throws<InvalidOperationException>(()=>view.ClockSec=()=>123);
        Assert.Equal(rig.Engine.Timer.SecondsF,view.ClockSec());
    }
}
