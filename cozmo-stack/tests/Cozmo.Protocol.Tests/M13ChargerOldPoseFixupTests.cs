using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M13ChargerOldPoseFixupTests
{
    private const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
    // reach: unit (supplies charger observations and opens the pose-change window by reflection;
    // invokes the actual named confirmer writers and BroadcastObjectPoseChanged queue, no broadcast substitute).
    // Old value: queued old-pose X is 90 (the NEW charger X); native copy-before-SetPose preserves 10.
    [Theory]
    [InlineData("WritePoseHelper")]
    [InlineData("AddObjectRelativeObservation")]
    [InlineData("ApplyRelativeObservation")]
    public void ChargerWritersDeliverOldPoseToTheWorld(string writer)
    {
        var world=new BlockWorld();using var root=new Pose3d("root"){Id=1};
        using var oldInput=new Pose3d(Rotation3d.Identity,new Vec3(10,0,0),parent:root);
        using var next=new Pose3d(Rotation3d.Identity,new Vec3(90,0,0),parent:root);
        var charger=new ObservableObject(77,ObjectType.Charger_Basic,Array.Empty<KnownMarker>());
        try
        {
            charger.NativePose.Dispose();charger.InitPose(oldInput,PoseState.Known);world.AddLocatedObject(charger);
            typeof(BlockWorld).GetField("_appendPoseChanges",PrivateInstance)!.SetValue(world,true);
            switch(writer)
            {
                case "WritePoseHelper":
                    typeof(BlockWorld).GetMethod(writer,PrivateInstance)!.Invoke(world,new object[]{charger,next,-1d,PoseState.Known,false});break;
                case "AddObjectRelativeObservation":
                    typeof(BlockWorld).GetMethod(writer,PrivateInstance)!.Invoke(world,new object[]{charger,next,charger});break;
                default: world.ApplyRelativeObservation(charger,next,PoseState.Known,1);break;
            }
            var changes=(List<PoseChange>)typeof(BlockWorld).GetField("_poseChanges",PrivateInstance)!.GetValue(world)!;
            var change=Assert.Single(changes);
            Assert.Equal(10d,change.Pose.Translation.X);
            Assert.Equal(90d,charger.Pose.Translation.X);
            Assert.NotSame(charger.NativePose.Node,change.Pose.Node);
            Assert.Same(root.Node,change.Pose.Node!.Parent);
        }
        finally
        {
            world.ResetToConstructed();world.Maps.Dispose();
            charger.ChargerMarker?.PoseOnObject.Dispose();charger.NativePose.Dispose();
            charger.Confirmation?.Pose.Dispose();
        }
    }
}
