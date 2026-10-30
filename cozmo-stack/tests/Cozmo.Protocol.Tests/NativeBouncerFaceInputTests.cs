using Cozmo.Robot.Vision;
using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class NativeBouncerFaceInputTests
{
    [Fact]
    public void NativeEligibilityFollowsRetainedNodeAncestryAndIdCopy()
    {
        using var rig=new Rig();
        var robot=rig.Robot.Engine.Robot!;
        using var foreign=new Pose3d("foreign");
        var origin=robot.GetNativeOrigin(robot.CurrentOriginId);
        var face=new TrackedFace(new DetectedFace(7,new FaceRect(0,0,10,10)),10);
        uint owners=foreign.Owners;
        var entry=rig.Vision.Faces.AddOrUpdateNativeFace(face,Pose3d.Identity,false,foreign.Node!)!.Face;
        Assert.Equal(owners,foreign.Owners);
        Assert.Empty(rig.Vision.Faces.GetNativeFaceIds(robot));
        foreign.SetParent(origin);
        Assert.Equal(new[]{7},rig.Vision.Faces.GetNativeFaceIds(robot));
        Assert.True(rig.Vision.Faces.ChangeFaceID(7,8));
        Assert.Null(entry.NativeObservationOrigin);
        Assert.Same(foreign.Node,rig.Vision.Faces.GetFace(8)!.NativeObservationOrigin);
        Assert.Equal(new[]{8},rig.Vision.Faces.GetNativeFaceIds(robot));
        var moved=rig.Vision.Faces.GetFace(8)!;
        rig.Vision.Faces.RemoveFaceByID(8);
        Assert.Null(moved.NativeObservationOrigin);
    }

    [Fact]
    public void ObservationResolvesHistoryRootIdThroughRegisteredOriginWithoutAddingOwner()
    {
        // Supplied history fixture; distinct history root and registered origin share only the native ID.
        using var rig=new Rig(); var robot=rig.Robot.Engine.Robot!;
        var history=new RobotStateHistory();
        using var historyRoot=new Pose3d("history"){Id=robot.CurrentOriginId};
        using var pose=new Pose3d("sample",historyRoot);
        uint owners=historyRoot.Owners;
        try
        {
            Assert.Equal(0u,history.AddVisionOnlyState(100,pose,1,0,0));
            var selected=VisionSystem.ResolveFaceObservationOrigin(history,robot,100);
            Assert.Same(robot.GetNativeOrigin(robot.CurrentOriginId).Node,selected);
            Assert.NotSame(historyRoot.Node,selected);
            Assert.Equal(owners,historyRoot.Owners);
            using var absentRoot=new Pose3d("absent"){Id=999999};
            using var absentPose=new Pose3d("sample",absentRoot);
            Assert.Equal(0u,history.AddVisionOnlyState(200,absentPose,2,0,0));
            Assert.Null(VisionSystem.ResolveFaceObservationOrigin(history,robot,200));
        }
        finally { history.Clear(); }
    }
    [Fact]
    public void SuppliedThirdDirectionIsDistinctFromRollAndSurvivesFaceWorldCopy()
    {
        var face=new TrackedFace(new DetectedFace(7,new FaceRect(0,0,10,10),RollRad:0.75) { NativeFaceDirectionThirdDegrees=-30 },10);
        var world=new FaceWorld();
        var observation=world.AddOrUpdateFace(face,Pose3d.Identity,false)!;
        float expected=-30f*BitConverter.Int32BitsToSingle(0x3C8EFA35);
        Assert.Equal(expected,observation.Face.NativeDirectionDcRadians);
        Assert.NotEqual((float)face.Detection.RollRad!.Value,observation.Face.NativeDirectionDcRadians);
        Assert.True(world.ChangeFaceID(7,8));
        Assert.Equal(expected,world.GetFace(8)!.NativeDirectionDcRadians);
        Assert.Null(new TrackedFace(new DetectedFace(9,new FaceRect(0,0,10,10)),10).NativeDirectionDcRadians);
    }
}
