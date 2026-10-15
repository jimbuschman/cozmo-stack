using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M3M4X28BuilderTests
{
    private static readonly TimeSpan Bound=TimeSpan.FromSeconds(10);
    // Checked single-engine-thread order: 0x004ED62E before 0x004ED648. Pause a real message
    // handler between its world accesses, then try an actual frame on another thread.
    private static void FrameCannotSplitHandler(BlockWorld world,Action<Action> subscribe,Action message)
    {
        using var inside=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        using var started=new ManualResetEventSlim();using var frameEntered=new ManualResetEventSlim();
        bool held=false,released=false;
        subscribe(()=>{held=Monitor.IsEntered(world.FrameGate);inside.Set();released=release.Wait(Bound);});
        world.BlockConfigurationManagerUpdate=()=>frameEntered.Set();
        var handler=Task.Run(message);Task? frame=null;
        try
        {
            Assert.True(inside.Wait(Bound));Assert.True(held,"The handler released FrameGate between world accesses.");
            var pose=new VisionPoseData(0,Pose3d.Identity,0,0,false,false);
            var camera=new CameraModel(CameraCalibration.Nominal(),pose.CameraPose);
            frame=Task.Run(()=>{started.Set();world.UpdateObservedMarkers(Array.Empty<ObservedMarker>(),camera,1,pose);});
            Assert.True(started.Wait(Bound));Assert.False(frame.Wait(TimeSpan.FromMilliseconds(100)));
            Assert.False(frameEntered.IsSet);
        }
        finally{release.Set();Assert.True(handler.Wait(Bound));if(frame is not null)Assert.True(frame.Wait(Bound));}
        Assert.True(released);Assert.True(frameEntered.IsSet);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void MovementLocatedLoopKeepsQueryAndAllUpdatesInOneFrameWindow(bool moving)
    {
        using var rig=new M4ControlTests.Rig();using var vision=new VisionSystem(rig.Robot,CameraCalibration.Nominal()){Enabled=false};
        rig.ToSynced();rig.State();var world=vision.World;
        var located=new ObservableObject(99,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {ActiveId=0,OriginId=world.CurrentOriginId,Pose=Pose3d.Identity,PoseState=PoseState.Known};
        world.AddLocatedObject(located);
        // Unknown connected ID (-1) differs from located ID 99: the checked warning is after
        // FindLocatedMatchingObjects returned and before ApplyMovedOrStopped.
        FrameCannotSplitHandler(world,pause=>rig.Engine.LogEntry+=e=>{
            if(e.Name==(moving?"Robot.ActiveObjectMoved.":"Robot.ActiveObjectStopped.")+"ActiveObjectInDifferentFramesWithDifferentIDs")pause();
        },()=>{rig.Data(moving?new ObjectMoved{ObjectID=0}:new ObjectStoppedMoving{ObjectID=0});rig.Tick();});
        Assert.Equal(moving,located.IsMoving);Assert.Equal(PoseState.Dirty,located.PoseState);
    }

    [Fact]
    public void DisconnectKeepsLocatedQueryAndMarkUnknownInOneFrameWindow()
    {
        using var rig=new M4ControlTests.Rig();using var vision=new VisionSystem(rig.Robot,CameraCalibration.Nominal()){Enabled=false};
        rig.ToSynced();rig.State();var world=vision.World;
        rig.Data(new ObjectConnectionState{ObjectID=0,FactoryID=123,ObjectType=ObjectType.Block_LIGHTCUBE1,Connected=true});rig.Tick();
        uint id=world.ConnectedObjectIdForActiveId(0)!.Value;
        var located=new ObservableObject(id,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {ActiveId=0,OriginId=world.CurrentOriginId,Pose=Pose3d.Identity,PoseState=PoseState.Known};
        world.AddLocatedObject(located);
        FrameCannotSplitHandler(world,pause=>vision.Log+=line=>{if(line.Contains("disconnected: its pose is no longer known"))pause();},
            ()=>{rig.Data(new ObjectConnectionState{ObjectID=0,FactoryID=123,ObjectType=ObjectType.Block_LIGHTCUBE1,Connected=false});rig.Tick();});
        Assert.Equal(PoseState.Unknown,located.PoseState);
    }

    [Fact]
    public void DelocalizationNotificationRemainsInsideTheHandlerFrameWindow()
    {
        using var rig=new M4ControlTests.Rig();using var vision=new VisionSystem(rig.Robot,CameraCalibration.Nominal()){Enabled=false};
        rig.ToSynced();rig.State();uint before=vision.World.CurrentOriginId;
        FrameCannotSplitHandler(vision.World,pause=>vision.RobotDelocalized+=_=>pause(),
            ()=>typeof(VisionSystem).GetMethod("DelocalizeRobot",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vision,new object[]{false}));
        Assert.Equal(before+1,vision.World.CurrentOriginId);
    }

    [Fact]
    public void DelocalizeWaitsForFrameBeforeResettingMismatchCount()
    {
        using var rig=new M4ControlTests.Rig();using var vision=new VisionSystem(rig.Robot,CameraCalibration.Nominal()){Enabled=false};
        rig.ToSynced();rig.State();var world=vision.World;
        var count=typeof(BlockWorld).GetField("_robot2C0",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var entry=typeof(VisionSystem).GetMethod("DelocalizeRobot",BindingFlags.Instance|BindingFlags.NonPublic)!;
        count.SetValue(world,(byte)7);using var started=new ManualResetEventSlim();Task? handler=null;
        Monitor.Enter(world.FrameGate);
        try
        {
            handler=Task.Run(()=>{started.Set();entry.Invoke(vision,new object[]{false});});Assert.True(started.Wait(Bound));
            Assert.False(handler.Wait(TimeSpan.FromMilliseconds(100)));Assert.Equal((byte)7,world.Robot2C0);
        }
        finally{Monitor.Exit(world.FrameGate);if(handler is not null)Assert.True(handler.Wait(Bound));}
        Assert.Equal((byte)0,world.Robot2C0);
    }
}
