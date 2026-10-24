using System.Collections.Concurrent;
using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class M3M4X23BuilderTests
{
    private static readonly TimeSpan Bound=TimeSpan.FromSeconds(10);
    private static (BlockWorld World,ObservableObject Cube,CameraModel Camera,VisionPoseData Pose) World()
    {
        var world=new BlockWorld{BaseStationSeconds=()=>0,CurrentRobotPose=()=>Pose3d.Identity};
        var cube=new ObservableObject(99,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {OriginId=1,PoseState=PoseState.Known,Pose=new(Mat3.Identity,new Vec3(150,0,22))};
        world.AddLocatedObject(cube);
        var pose=new VisionPoseData(0,Pose3d.Identity,0,0,false,false);
        return(world,cube,new CameraModel(CameraCalibration.Nominal(),pose.CameraPose),pose);
    }
    private static object ObjectGate(BlockWorld world)=>typeof(BlockWorld).GetField("_gate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(world)!;
    private static void DuringFrame(BlockWorld world,CameraModel camera,VisionPoseData pose,Action mutate)
    {
        using var inside=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        using var started=new ManualResetEventSlim();using var finished=new ManualResetEventSlim();
        world.OnTreads=()=>{Assert.True(world.PoseChangeWindow);inside.Set();Assert.True(release.Wait(Bound));return false;};
        var frame=Task.Run(()=>world.UpdateObservedMarkers(Array.Empty<ObservedMarker>(),camera,1,pose));Task? writer=null;
        try
        {
            Assert.True(inside.Wait(Bound));
            writer=Task.Run(()=>{started.Set();mutate();finished.Set();});Assert.True(started.Wait(Bound));
            Assert.False(finished.Wait(TimeSpan.FromMilliseconds(100)),"An off-frame mutation entered the native append window.");
            var gate=ObjectGate(world);Assert.True(Monitor.TryEnter(gate));Monitor.Exit(gate);
        }
        finally {release.Set();}
        Assert.True(frame.Wait(Bound));Assert.NotNull(writer);Assert.True(writer!.Wait(Bound));
        Assert.False(world.PoseChangeWindow);
    }
    [Fact]
    public void LiftAttachWaitsForTheFrameBeforeReadingOrWritingWorldPoses()
    {
        var (w,cube,camera,pose)=World();var carrying=new CarryingComponent();var keys=new ConcurrentQueue<string>();w.SourceLog=(l,c,k,t)=>keys.Enqueue(k);
        uint result=uint.MaxValue;
        DuringFrame(w,camera,pose,()=>result=LiftGeometry.SetObjectAsAttachedToLift(carrying,w,null,pose,99,cube.Markers[0].Code));
        Assert.Equal(0u,result);Assert.Equal(99u,carrying.CarriedObjectId);
        Assert.Equal(0,w.PoseChangeCount);Assert.DoesNotContain(keys,k=>k.Contains("FirstPoseForObject"));
    }
    [Theory]
    [InlineData("relativeDirty")] [InlineData("relativeKnown")] [InlineData("carried")] [InlineData("detach")]
    [InlineData("released")] [InlineData("unknown")] [InlineData("moved")] [InlineData("origins")]
    [InlineData("delete")] [InlineData("add")] [InlineData("dirty")] [InlineData("state")]
    [InlineData("clear")] [InlineData("reset")] [InlineData("connect")] [InlineData("moving")]
    [InlineData("originSetter")] [InlineData("modify")] [InlineData("broadcast")]
    public void OtherMutationEntriesWaitOutsideTheFrameWindow(string operation)
    {
        var (w,cube,camera,pose)=World();var keys=new ConcurrentQueue<string>();w.SourceLog=(l,c,k,t)=>keys.Enqueue(k);
        Action mutation=operation switch
        {
            "relativeDirty"=>()=>w.ApplyRelativeObservation(cube,Pose3d.Identity,PoseState.Dirty,null),
            "relativeKnown"=>()=>w.ApplyRelativeObservation(cube,Pose3d.Identity,PoseState.Known,1),
            "carried"=>()=>w.SetCarriedPose(99,Pose3d.Identity),
            "detach"=>()=>w.DetachFromLift(cube,Pose3d.Identity),
            "released"=>()=>w.MarkReleased(99),"unknown"=>()=>w.MarkUnknown(99),
            "moved"=>()=>w.ApplyMovedOrStopped(cube,true),
            "origins"=>()=>w.UpdateObjectOrigins(1,2,Pose3d.Identity),
            "delete"=>()=>w.DeleteLocatedObjects(new BlockWorldFilter()),
            "add"=>()=>w.AddLocatedObject(new ObservableObject(100,ObjectType.Block_LIGHTCUBE2,Array.Empty<KnownMarker>()){OriginId=1,Pose=Pose3d.Identity,PoseState=PoseState.Known}),
            "dirty"=>()=>w.MarkDirty(99),"state"=>()=>w.SetPoseStateHelper(cube,PoseState.Dirty),
            "clear"=>()=>w.ClearConfirmer(),"reset"=>()=>w.ResetToConstructed(),
            "connect"=>()=>w.AddConnectedActiveObject(1,123,ObjectType.Block_LIGHTCUBE1),
            "moving"=>()=>w.SetMoving(99,true),"originSetter"=>()=>w.CurrentOriginId=2,
            "modify"=>()=>w.FindLocatedObjectHelper(new BlockWorldFilter(),o=>o.Pose=Pose3d.Identity,true),
            "broadcast"=>()=>w.BroadcastObjectPoseChanged(cube,null,PoseState.Unknown),
            _=>throw new InvalidOperationException(operation),
        };
        DuringFrame(w,camera,pose,mutation);
        Assert.Equal(0,w.PoseChangeCount);Assert.DoesNotContain(keys,k=>k.Contains("FirstPoseForObject"));
    }
    [Fact]
    public void PoseNotificationsAndFrameInfoCallbacksNeverHoldTheObjectGate()
    {
        var (w,cube,camera,pose)=World();var gate=ObjectGate(w);int sources=0,states=0,configuration=0;
        w.SourceLog=(l,c,k,t)=>{Assert.False(Monitor.IsEntered(gate));if(k.Contains("FirstPoseForObject"))++sources;};
        w.SetObjectPoseChanged=(_,_)=>{Assert.False(Monitor.IsEntered(gate));++configuration;};
        w.PoseStateChanged+=(_,_,_)=>{Assert.False(Monitor.IsEntered(gate));++states;};
        w.OnTreads=()=>{w.BroadcastObjectPoseChanged(cube,null,PoseState.Unknown);return false;};
        w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(),camera,1,pose);
        Assert.Equal(1,sources);Assert.Equal(1,configuration);
        w.ApplyRelativeObservation(cube,Pose3d.Identity,PoseState.Dirty,null);
        Assert.Equal(2,configuration);Assert.Equal(2,states);Assert.Equal(0,w.PoseChangeCount);
    }
    // reach: unit (robot-owned vision instance with direct frame/helper or fixture inputs; full native producer is not claimed).
    [Theory]
    [InlineData("delocalize")] [InlineData("attach")] [InlineData("lift")]
    public void DockingAndDelocalizationTakeFrameBeforeTheRobotPoseGate(string operation)
    {
        using var robot=CozmoRobot.CreateOffline();var vision=robot.Vision; vision.Calibration=CameraCalibration.Nominal(); vision.Enabled=false;
        robot.Engine.Robots.AddRobot(CozmoEngine.RobotId);var native=robot.Engine.Robot!;
        vision.History.Add(new RobotState{Timestamp=1,PoseOriginId=native.CurrentOriginId});
        var (unused,cube,camera,pose)=World();vision.World.AddLocatedObject(cube);
        // A / reach: unit: old rootless cube threw M11-066 on the native live-lift conversion.
        if(operation=="attach")cube.NativePose.SetParent(vision.World.GetNativeOrigin(vision.World.CurrentOriginId));
        var docking=new DockingSystem(robot,vision);
        uint attachResult=uint.MaxValue;
        Action mutate=operation switch
        {
            "delocalize"=>()=>native.Delocalize(false),
            "attach"=>()=>attachResult=docking.SetObjectAsAttachedToLift(99,cube.Markers[0].Code),
            "lift"=>()=>native.ComputeLiftPose(0),
            _=>throw new InvalidOperationException(operation),
        };
        using var started=new ManualResetEventSlim();Task? writer=null;
        Monitor.Enter(vision.World.FrameGate);
        try
        {
            writer=Task.Run(()=>{started.Set();mutate();});Assert.True(started.Wait(Bound));
            Assert.False(writer.Wait(TimeSpan.FromMilliseconds(100)));
            Assert.True(Monitor.TryEnter(native.PoseTreeGate));Monitor.Exit(native.PoseTreeGate);
        }
        finally {Monitor.Exit(vision.World.FrameGate);}
        Assert.NotNull(writer);Assert.True(writer!.Wait(Bound));
        if(operation=="attach")Assert.Equal(0u,attachResult);
        docking.Dispose();
    }
}
