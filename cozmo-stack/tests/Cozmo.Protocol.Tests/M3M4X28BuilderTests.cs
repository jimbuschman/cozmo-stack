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
    private static void FrameCannotSplitHandler(BlockWorld world,Action<Action> subscribe,Action message,bool publicCallback=false)
    {
        using var inside=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        using var started=new ManualResetEventSlim();using var frameEntered=new ManualResetEventSlim();
        bool held=false,released=false;
        subscribe(()=>{held=Monitor.IsEntered(world.FrameGate);inside.Set();released=release.Wait(Bound);});
        world.BlockConfigurationManagerUpdate=()=>frameEntered.Set();
        var handler=Task.Run(message);Task? frame=null;
        try
        {
            Assert.True(inside.Wait(Bound));Assert.Equal(!publicCallback,held);
            var pose=new VisionPoseData(0,Pose3d.Identity,0,0,false,false);
            var camera=new CameraModel(CameraCalibration.Nominal(),pose.CameraPose);
            frame=Task.Run(()=>{started.Set();world.UpdateObservedMarkers(Array.Empty<ObservedMarker>(),camera,1,pose);});
            Assert.True(started.Wait(Bound));
            if(publicCallback){Assert.True(frame.Wait(Bound));Assert.True(frameEntered.IsSet);}
            else{Assert.False(frame.Wait(TimeSpan.FromMilliseconds(100)));Assert.False(frameEntered.IsSet);}
        }
        finally{release.Set();Assert.True(handler.Wait(Bound));if(frame is not null)Assert.True(frame.Wait(Bound));}
        Assert.True(released);Assert.True(frameEntered.IsSet);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void MovementLocatedLoopKeepsQueryAndAllUpdatesInOneFrameWindow(bool moving)
    {
        using var rig=new M4ControlTests.Rig();var vision=rig.Robot.Vision; vision.Calibration=CameraCalibration.Nominal(); vision.Enabled=false;
        rig.ToSynced();rig.State();var world=vision.World;
        var located=new ObservableObject(99,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {ActiveId=0,OriginId=world.CurrentOriginId,Pose=Pose3d.Identity,PoseState=PoseState.Known};
        world.AddLocatedObject(located);
        // Transparent native recipient observer pauses between MarkObjectDirty and the motion-state store.
        // Public log callbacks now run after frame release and cannot serve as an in-frame pause point.
        var prior=world.ActiveObjectPoseStateChanged;
        FrameCannotSplitHandler(world,pause=>world.ActiveObjectPoseStateChanged=(id,old,state)=>{
            prior?.Invoke(id,old,state);pause();
        },()=>{rig.Data(moving?new ObjectMoved{ObjectID=0}:new ObjectStoppedMoving{ObjectID=0});rig.Tick();});
        Assert.Equal(moving,located.IsMoving);Assert.Equal(PoseState.Dirty,located.PoseState);
    }

    [Fact]
    // reach: unit (located non-active fixture object; native disconnect writes complete before the deferred public diagnostic).
    public void DisconnectLocatedUpdateRemainsInsideOneFrameWindow()
    {
        using var rig=new M4ControlTests.Rig();var vision=rig.Robot.Vision; vision.Calibration=CameraCalibration.Nominal(); vision.Enabled=false;
        rig.ToSynced();rig.State();var world=vision.World;
        rig.Data(new ObjectConnectionState{ObjectID=0,FactoryID=123,ObjectType=ObjectType.Block_LIGHTCUBE1,Connected=true});rig.Tick();
        uint id=world.ConnectedObjectIdForActiveId(0)!.Value;
        var located=new ObservableObject(id,ObjectType.ProxObstacle,Array.Empty<KnownMarker>())
            {ActiveId=0,OriginId=world.CurrentOriginId,Pose=Pose3d.Identity,PoseState=PoseState.Known};
        world.AddLocatedObject(located);
        // The checked C2 NotActive diagnostic is delivered after both guarded writes and frame release.
        // A paused public diagnostic subscriber must allow another frame to finish. Located state is retained.
        FrameCannotSplitHandler(world,pause=>rig.Engine.LogEntry+=entry=>{if(entry.Name=="ObservableObject.SetActiveID.NotActive")pause();},
            ()=>{rig.Data(new ObjectConnectionState{ObjectID=0,FactoryID=123,ObjectType=ObjectType.Block_LIGHTCUBE1,Connected=false});rig.Tick();},publicCallback:true);
        Assert.Equal(PoseState.Known,located.PoseState);Assert.Contains(located,world.LocatedObjects);
    }

    [Fact]
    // reach: unit (native Delocalize entry/component notification; queued public app reports run after the handler).
    public void DelocalizationNotificationRemainsInsideTheHandlerFrameWindow()
    {
        using var rig=new M4ControlTests.Rig();var vision=rig.Robot.Vision; vision.Calibration=CameraCalibration.Nominal(); vision.Enabled=false;
        rig.ToSynced();rig.State();uint before=vision.World.CurrentOriginId;
        FrameCannotSplitHandler(vision.World,pause=>rig.Engine.DelocalizationComponentHook=stage=>{if(stage=="FaceWorld")pause();},
            ()=>rig.Engine.Robot!.Delocalize(false));
        Assert.Equal(before+1,vision.World.CurrentOriginId);
    }

    [Fact]
    // reach: production — actual treads packet, dispatch and engine tick; native frame gate blocks before reset.
    public void DelocalizeWaitsForFrameBeforeResettingMismatchCount()
    {
        using var rig=new M4ControlTests.Rig();var vision=rig.Robot.Vision; vision.Calibration=CameraCalibration.Nominal(); vision.Enabled=false;
        rig.ToSynced();rig.Calibrate();rig.State();var world=vision.World;
        var count=typeof(BlockWorld).GetField("_robot2C0",BindingFlags.Instance|BindingFlags.NonPublic)!;
        count.SetValue(world,(byte)7);using var started=new ManualResetEventSlim();Task? handler=null;
        Monitor.Enter(world.FrameGate);
        try
        {
            handler=Task.Run(()=>{started.Set();rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsPickedUp));rig.Tick();});Assert.True(started.Wait(Bound));
            Assert.False(handler.Wait(TimeSpan.FromMilliseconds(100)));Assert.Equal((byte)7,world.Robot2C0);
        }
        finally{Monitor.Exit(world.FrameGate);if(handler is not null)Assert.True(handler.Wait(Bound));}
        Assert.Equal((byte)0,world.Robot2C0);
    }
}
