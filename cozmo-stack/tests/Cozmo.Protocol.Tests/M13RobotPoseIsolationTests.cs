using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class M13RobotPoseIsolationTests
{
    [Fact]
    public void ParentlessRandomDriveGoalSucceeds()
    {
        // reach: unit (real drive action/queue, offline firmware; random goal construction from ExplorerBehaviors).
        // 29ddb606: FailedTraversingPath, because RobotPose returned a parented live node.
        using var signals=SignalTestContext.Install();using var rig=new Rig();
        var random=new Random(26);
        double distance=40+random.NextDouble()*60,heading=(50+random.NextDouble()*100)*Math.PI/180;
        using var goal=new Pose3d(Mat3.AboutZ(heading),new Vec3(Math.Cos(heading)*distance,Math.Sin(heading)*distance,0));
        Assert.False(goal.HasParent);
        using var drive=new DriveToPoseAction(rig.M){Goal=goal};
        Assert.Equal(ActionResult.Success,SignalTestContext.Result(drive.RunAsync(default),()=>rig.Pump()));
    }

    [Fact]
    public void KnownFaceWithProductionParentlessHeadPoseSelectsFlipPose()
    {
        // reach: unit (real GetPossiblePoses, explicit cube and face; no detector/firmware fidelity claim).
        // 29ddb606: throws M11-066 caller has not handled unrelated pose roots at face distance comparison.
        using var signals=SignalTestContext.Install();using var rig=new Rig();
        rig.Y=-10;rig.State();
        using var objectPose=new Pose3d(Mat3.Identity,new Vec3(200,0,22));
        objectPose.SetParent(rig.Vision.World.GetNativeOrigin(rig.Vision.World.CurrentOriginId));
        var cube=new ObservableObject(7,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {Pose=objectPose,PoseState=PoseState.Known,OriginId=rig.Vision.World.CurrentOriginId};
        using var head=new Pose3d(Mat3.Identity,new Vec3(130,80,50));using var observer=Pose3d.Identity;
        Assert.False(head.HasParent); // Faces.cs:206 supplies a flattened value too.
        IReadOnlyList<PreActionPose> poses=Array.Empty<PreActionPose>();
        try
        {
            rig.Vision.Faces.AddOrUpdateFace(new TrackedFace(new DetectedFace(3,new FaceRect(0,0,10,10),Name:"Bob"),1000){HeadPose=head},observer,false);
            Assert.Equal(ActionResult.Success,DriveAndFlipBlockAction.GetPossiblePoses(rig.M,cube,out poses,false));
            Assert.Equal(BlockFace.Front,Assert.Single(poses).Marker.Face); // checked M12-035 nearest-pair/face rule.
        }
        finally
        {
            foreach(var pose in poses)pose.WorldPose.Dispose();
            CubePreActionPoses.ClearForSetPose(cube);
            foreach(var marker in cube.Markers)marker.PoseOnObject.Dispose();
            cube.NativePose.Dispose();
        }
    }

    [Fact]
    public void StackReturnTurnUsesTheStoredStartHeading()
    {
        // reach: unit (real behavior/return path with offline firmware and substituted object turn).
        // 29ddb606: return target .9 (current heading), not .25 (stored start heading), due to live-node aliasing.
        using var signals=SignalTestContext.Install();using var rig=new Rig();
        using var cube=new Pose3d(Mat3.Identity,new Vec3(240,60,22));rig.Cube=cube;
        rig.Angle=.25f;rig.State();Assert.Single(rig.Frame().Objects);
        float startHeading=(float)rig.Vision.History.Latest!.Value.RobotPose.AngleAroundZ;
        rig.M.TurnOverride=(_,_,_)=>{rig.Angle=.9f;rig.State();return Task.FromResult(true);};
        var context=new BehaviorContext{Robot=rig.Robot,Triggers=new AnimationTriggerMap()};
        var behavior=new CheckForStackAtIntervalBehavior(rig.M){ActionTaskRunner=SignalTestContext.Schedule};
        try
        {
            behavior.StartAsync(context,new BehaviorScope(),default).GetAwaiter().GetResult();
            int steps=0;
            while(behavior.Update(context,rig.Clock.NowMs/1000))
            {
                rig.Pump();rig.Frame();SignalTestContext.AdvanceBehavior(behavior);
                Assert.True(++steps<10000,"stack-check behavior did not finish");
            }
            Assert.Equal(startHeading,rig.Sent.OfType<AppendPathSegmentPointTurn>().Last().TargetAngleRad);
        }
        finally{behavior.Stop(BehaviorStopReason.Completed);}
    }
}
