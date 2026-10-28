using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PickupMotionTimestampTests
{
    private static ObservableObject Connect(Fixture f)
    {
        M4ControlTests.ConnectCube(f.Rig);var connected=Assert.Single(f.M.World.ConnectedObjects);
        var cube=f.Add(connected.ObjectId,300);cube.ActiveId=connected.ActiveId;
        Assert.True(cube.ActiveId>=0);return cube;
    }
    private static void Motion(Fixture f,ObservableObject cube,bool moving,uint timestamp)
    {
        f.Rig.Data(moving?new ObjectMoved{ObjectID=unchecked((uint)cube.ActiveId),Timestamp=timestamp}:new ObjectStoppedMoving{ObjectID=unchecked((uint)cube.ActiveId),Timestamp=timestamp});f.Rig.Tick();
    }
    private static void State(Fixture f,uint timestamp)
    {var state=f.Rig.MakeState(origin:999);state.Timestamp=timestamp;f.Rig.Data(state);f.Rig.Tick();Assert.Equal(timestamp,f.Robot.StoredState!.Timestamp);}
    [Fact]
    public void IntakePreservesIndependentCanonicalTransitionStampsAndDirtyGuardDoesNotGateMotion()
    {
        // reach: unit supplied located scene, actual connected/moved/stopped intake; J15 534140..15C/53494E..96A.
        using var f=new Fixture();var cube=Connect(f);var connected=Assert.Single(f.M.World.ConnectedObjects);
        Assert.False(cube.GetNativeMotionState(out uint initial));Assert.Equal(0u,initial);
        Motion(f,cube,true,10);Assert.True(cube.IsMoving);Assert.Equal(10u,cube.NativeMotionChangedAt);Assert.Equal(10u,connected.NativeMotionChangedAt);
        Motion(f,cube,true,20);Assert.Equal(10u,cube.NativeMotionChangedAt);Assert.Equal(10u,connected.NativeMotionChangedAt);
        Motion(f,cube,false,30);Motion(f,cube,false,40);Assert.False(cube.IsMoving);Assert.Equal(30u,cube.NativeMotionChangedAt);Assert.Equal(30u,connected.NativeMotionChangedAt);
        f.M.Docking.Carrying.SetCarrying(cube.ObjectId);cube.PoseState=PoseState.Known;
        Motion(f,cube,true,50);Assert.Equal(PoseState.Known,cube.PoseState);Assert.Equal(50u,cube.NativeMotionChangedAt);
        cube.PoseState=PoseState.Unknown;Motion(f,cube,false,60);Assert.Equal(PoseState.Unknown,cube.PoseState);Assert.Equal(60u,cube.NativeMotionChangedAt);
        // A fresh canonical clone is false/zero even while its connected counterpart retains motion state.
        Motion(f,cube,true,70);var remove=new BlockWorldFilter();remove.AllowedIds.Add(cube.ObjectId);f.M.World.DeleteLocatedObjects(remove);
        var fresh=f.Add(cube.ObjectId,300);fresh.ActiveId=connected.ActiveId;
        Motion(f,fresh,true,80);Assert.Equal(80u,fresh.NativeMotionChangedAt);Assert.Equal(70u,connected.NativeMotionChangedAt);
    }
    [Fact]
    public void FreshCloneResetsMotionWhilePoseOnlyCopyRetainsDestinationState()
    {
        // reach: unit canonical writer/copy recipients; J15 fresh4E0C84/86, Clone4E38EE, CopyWithNewPose506EF8.
        using var f=new Fixture();var source=f.Add(7,300);source.SetNativeMotionState(true,123);
        var clone=source.CloneNative();Assert.False(clone.IsMoving);Assert.Equal(0u,clone.NativeMotionChangedAt);
        var destination=f.Add(8,400);destination.SetNativeMotionState(true,456);destination.SetNativeMotionState(false,789);
        using var pose=new Pose3d(source.NativePose);Assert.Equal(0u,f.M.World.CopyWithNewPose(destination,pose,source));
        Assert.False(destination.IsMoving);Assert.Equal(789u,destination.NativeMotionChangedAt);
        destination.LastObservedTimestamp=999;Assert.Equal(789u,destination.NativeMotionChangedAt);
    }
    [Theory]
    [InlineData(false,1000u,100u,1000u,true)] // recent vision cannot rescue stale motion.
    [InlineData(false,1000u,1000u,1u,false)] // stale vision cannot reject recent motion.
    [InlineData(false,1500u,1000u,1u,false)]
    [InlineData(false,1501u,1000u,1501u,true)]
    [InlineData(true,3000u,1000u,1u,false)]
    [InlineData(true,3001u,1000u,3001u,true)]
    [InlineData(false,100u,4294967195u,1u,false)] // unchecked stop+500 wraps to399.
    [InlineData(false,400u,4294967195u,400u,true)]
    [InlineData(true,1899u,4294967195u,1u,false)] // unchecked stop+2000 wraps to1899.
    [InlineData(true,1900u,4294967195u,1900u,true)]
    public void VerifyUsesMotionStampUnsignedSelectedTimeoutAndCapturedFirstVerify(bool high,uint first,uint stopped,uint visual,bool stale)
    {
        // reach: unit VerifyPrefix with actual raw state + motion intake/scoped load recipient; J14/J15.
        using var f=new Fixture();var cube=Connect(f);Motion(f,cube,true,unchecked(stopped-1));Motion(f,cube,false,stopped);
        cube.LastObservedTimestamp=visual;State(f,first);
        using var action=new PickupObjectAction(f.M,cube.ObjectId);action.SetSelectedDockAction(high?DockAction.PickupHigh:DockAction.PickupLow);
        using var registration=f.M.Docking.RegisterActionHandlers(action,high?DockAction.PickupHigh:DockAction.PickupLow);
        f.Rig.Data(new LiftLoad{Field0=false});f.Rig.Tick();Assert.Equal(2,action.LiftLoadState);Assert.True(cube.ActiveId>=0);
        var result=action.VerifyPrefix(cube,new DockResult(first,true,0,default));
        Assert.Equal(stale?ActionResult.PickupObjectNotSeenRecently:ActionResult.Success,result);Assert.Equal(first,action.VerifyStartedAt);
        if(!stale){State(f,unchecked(first+10000));Assert.Equal(ActionResult.Success,action.VerifyPrefix(cube,new DockResult(0,true,0,default)));Assert.Equal(first,action.VerifyStartedAt);}
    }
    [Fact]
    public void MovingAllowanceStillUsesCurrentStateAgainstFirstVerifyInclusiveFiveHundred()
    {
        using var f=new Fixture();var cube=Connect(f);Motion(f,cube,true,900);State(f,1000);
        using var action=new PickupObjectAction(f.M,cube.ObjectId);action.SetSelectedDockAction(DockAction.PickupLow);
        using var registration=f.M.Docking.RegisterActionHandlers(action,DockAction.PickupLow);f.Rig.Data(new LiftLoad{Field0=false});f.Rig.Tick();
        Assert.Equal(ActionResult.Running,action.VerifyPrefix(cube,new DockResult(0,true,0,default)));
        State(f,1500);Assert.Equal(ActionResult.Running,action.VerifyPrefix(cube,new DockResult(0,true,0,default)));
        State(f,1501);Assert.Equal(ActionResult.PickupObjectStillMoving,action.VerifyPrefix(cube,new DockResult(0,true,0,default)));
    }
}
