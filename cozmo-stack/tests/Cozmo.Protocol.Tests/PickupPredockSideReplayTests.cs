using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PickupPredockSideReplayTests
{
    // Captured object observations from pickup side HW runs 2 and 3. The original full
    // world and observed-marker history were not recorded: the fixture supplies an empty
    // obstacle world, the original world-origin parent and an observed Bottom marker.
    // This reaches the scoped DriveToObject action's real goal function, not camera solve,
    // planner choice, robot firmware execution or the complete public pickup lifecycle.
    [Theory]
    [InlineData(239.44572f,9.695505f,8.882935f,.7067283f,.023134507f,-.7067282f,.023134507f,-1.640797f,.1162697f,.007103781f,398.815f,20.140f,-3.076146709f,418.772f,21.448f,420f,20f)]
    [InlineData(278.54977f,-10.740843f,14.184526f,.7070018f,-.0121841f,-.7070018f,-.012184103f,48.58995f,-2.696495f,-.05076665f,438.166f,-16.244f,3.107129105f,458.154f,-16.933f,460f,-20f)]
    public void CapturedTiltAdmitsOnlyBottomPickupPredock(float x,float y,float z,float w,float qx,float qy,float qz,
        float robotX,float robotY,float robotYaw,float mainX,float mainY,float yaw,float forwardX,float forwardY,float wireX,float wireY)
    {
        using var f=new Fixture();
        // Latest captured 0xF0 MAIN before the selected 0x44; the source world-origin
        // graph was not captured, so the fixture binds both values to one live root.
        f.X=robotX;f.Y=robotY;f.Yaw=robotYaw;f.Motors();
        using var objectPose=new Pose3d(new Rotation3d(w,qx,qy,qz),new Vec3(x,y,z),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        var cube=new ObservableObject(7,ObjectType.Block_LIGHTCUBE2,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE2))
        {Pose=objectPose,OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known,LastObservedTimestamp=f.Robot.StoredState!.Timestamp};
        cube.LastObservedMarkers=new[]{MarkerType.LightCubeJ_Bottom};
        foreach(var marker in cube.Markers)marker.LastObservedTimestamp=cube.LastObservedTimestamp;
        f.M.World.AddLocatedObject(cube);
        var runner=new NativeDriveToObjectRunner(f.M,f.Robot,7,PreActionType.Docking);
        try
        {
            bool inPosition=false;
            Assert.Equal(ActionResult.Success,runner.Action.GetPossiblePoses(cube,out var goals,ref inPosition));
            var goal=Assert.Single(goals);Assert.Equal(MarkerType.LightCubeJ_Bottom,goal.Marker.Code);Assert.Equal(BlockFace.Bottom,goal.Marker.Face);
            using var root=goal.WorldPose.GetWithRespectToRoot();
            Assert.InRange((float)root.Translation.X,mainX-.12f,mainX+.12f);
            Assert.InRange((float)root.Translation.Y,mainY-.12f,mainY+.12f);
            Assert.InRange((float)root.AngleAroundZ,yaw-.002f,yaw+.002f);
            // The planner's FORWARD frame is 20 mm opposite the goal heading from MAIN.
            // Compare that coordinate to the captured 0x3F approach.
            float fx=(float)root.Translation.X-20f*MathF.Cos((float)root.AngleAroundZ);
            float fy=(float)root.Translation.Y-20f*MathF.Sin((float)root.AngleAroundZ);
            Assert.InRange(fx,forwardX-.12f,forwardX+.12f);Assert.InRange(fy,forwardY-.12f,forwardY+.12f);
            Assert.InRange(MathF.Abs(fx-wireX),0,5);Assert.InRange(MathF.Abs(fy-wireY),0,5); // Recorded 0x3F; planner selection itself is outside this replay.
            var generated=CubePreActionPoses.GenerateStored(cube,PreActionType.Docking);
            try
            {
                var valid=new List<(MarkerType,int)>();
                foreach(var element in generated)
                {
                    using var pose=CubePreActionPoses.ComposeWorld(cube.NativePose,element.Stored,f.Robot.GetCheckedMainPose(),element.DistanceMm,0);
                    if(pose is not null && PreActionValidity.IsPreActionPoseValid(cube,new PreActionPose(element.Type,element.Marker,pose,element.DistanceMm),Array.Empty<PreActionObstacle>()))
                    { Assert.Equal(75f,element.DistanceMm);valid.Add((element.Marker.Code,element.Sb)); }
                }
                Assert.Equal(new[]{(MarkerType.LightCubeJ_Bottom,2)},valid);
            }
            finally { foreach(var element in generated)element.Stored.Dispose(); }
        }
        finally{runner.RetireNeverAdmittedTree();}
    }
}
