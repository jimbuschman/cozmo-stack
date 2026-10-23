using System.Collections.Concurrent;
using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("ObjectID process statics")]
public sealed class M3M4X19BuilderTests
{
    private static readonly TimeSpan Bound=TimeSpan.FromSeconds(10);
    private static (CameraModel Camera,VisionPoseData Pose) Frame()
    {var p=new VisionPoseData(0,Pose3d.Identity,0,0,false,false);return(new(CameraCalibration.Nominal(),p.CameraPose),p);}
    [Fact] public void CliffWaitsOnTheActualFrameGateAndNeverEntersItsFlagWindow()
    {
        var w=new BlockWorld{BaseStationSeconds=()=>0,CurrentRobotPose=()=>Pose3d.Identity};var f=Frame();var keys=new ConcurrentQueue<string>();w.SourceLog=(l,c,k,t)=>keys.Enqueue(k);
        using var inside=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var probed=new ManualResetEventSlim();using var finished=new ManualResetEventSlim();
        w.OnTreads=()=>{Assert.True(w.PoseChangeWindow);inside.Set();Assert.True(release.Wait(Bound));return false;};
        var frame=Task.Run(()=>w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(),f.Camera,1,f.Pose));Task? cliff=null;bool obtained=true;
        try
        {
            Assert.True(inside.Wait(Bound));
            cliff=Task.Run(()=>{obtained=Monitor.TryEnter(w.FrameGate);if(obtained)Monitor.Exit(w.FrameGate);probed.Set();w.AddCliffFromCheckedPose(Pose3d.Identity,1,_=>{});finished.Set();});
            Assert.True(probed.Wait(Bound));Assert.False(obtained);Assert.False(finished.IsSet);Assert.Empty(w.LocatedObjects);
        }
        finally {release.Set();}
        Assert.True(frame.Wait(Bound));Assert.NotNull(cliff);Assert.True(cliff!.Wait(Bound));
        Assert.DoesNotContain(keys,k=>k.Contains("FirstPoseForObject"));Assert.Equal(0,w.PoseChangeCount);
    }
    [Fact] public void ConfigurationCallbackNeverHoldsTheObjectGateWhileWaitingForItsOwnGate()
    {
        var w=new BlockWorld{BaseStationSeconds=()=>0};var f=Frame();var config=new BlockConfigurationManager(w,()=>0);
        var configGate=typeof(BlockConfigurationManager).GetField("_updateGate",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(config)!;
        var objectGate=typeof(BlockWorld).GetField("_gate",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(w)!;
        using var reached=new ManualResetEventSlim();w.BlockConfigurationManagerUpdate=()=>{reached.Set();config.Update();};
        Task? frame=null;bool queried=false;
        Monitor.Enter(configGate);
        try
        {
            frame=Task.Run(()=>w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(),f.Camera,0,f.Pose));Assert.True(reached.Wait(Bound));
            if(Monitor.TryEnter(objectGate,TimeSpan.FromSeconds(2)))
            {try{Assert.Empty(w.LocatedObjects);queried=true;}finally{Monitor.Exit(objectGate);}}
        }
        finally {Monitor.Exit(configGate);}
        Assert.NotNull(frame);Assert.True(frame!.Wait(Bound));Assert.True(queried,"The old whole-frame object lock creates an inverse configuration/object lock cycle.");
    }
    // reach: unit (substitutes historical-pose lookup and direct Broadcast; explicit host drain, no full map-insertion claim).
    [Fact] public void TiltedCubeCandidateUsesCheckedFootprintAndCliffReportContinues()
    {
        using var robot=CozmoRobot.CreateOffline();var w=robot.LocalizationWorld;w.CurrentRobotPose=()=>Pose3d.Identity;
        var cube=new ObservableObject(99,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>()){OriginId=1,PoseState=PoseState.Known,Pose=new(Mat3.AboutX(.3),new Vec3(0,0,22))};w.AddLocatedObject(cube);
        var missing=new List<string>();var keys=new List<string>();int reports=0;robot.Engine.StackLogLine+=missing.Add;robot.Engine.LogEntry+=e=>keys.Add(e.Name);robot.Sensors.CliffDetected+=_=>reports++;
        robot.Sensors.CliffHistoricalPoseForTest=_=>(0,Pose3d.Identity);robot.Engine.Broadcast(new CliffEvent{Timestamp=10,DetectedFlags=1});
        Assert.Equal(0,reports);robot.Engine.DrainAppDelivery();
        Assert.DoesNotContain(missing,s=>s.Contains("M13-025") && s.Contains("candidate"));Assert.Contains("RobotImplMessaging.HandleCliffEvent.Detected",keys);Assert.Equal(1,reports);
        // M12-043 now supplies the projected tilted-cube quad. This check covers caller delivery;
        // no full map-insertion result is inferred from the footprint alone.
    }
    [Fact] public void ReconstructedMapMayBeAbsentWithoutThrowingAndAreaIsExplicitlyUnknown()
    {
        var w=new BlockWorld(deferMap:true);w.ResetToConstructed();Assert.Null(w.Maps.Current);
        var f=Frame();w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(),f.Camera,0,f.Pose);
        var missing=new List<string>();var map=new MemoryMap{Missing=missing.Add};Assert.True(double.IsNaN(map.ExploredRegionAreaM2));Assert.Contains(missing,s=>s.Contains("M11-045"));
    }
    [Fact] public void SharedWorldHasOneStackLogSubscription()
    {
        using var robot=CozmoRobot.CreateOffline();var vision=robot.Vision; vision.Calibration=CameraCalibration.Nominal(); vision.Enabled=false;var missing=new List<string>();robot.Engine.StackLogLine+=missing.Add;
        vision.World.BaseStationSeconds=()=>61;var f=Frame();vision.World.UpdateObservedMarkers(Array.Empty<ObservedMarker>(),f.Camera,0,f.Pose);
        Assert.Single(missing.Where(s=>s.Contains("robot.play_area_size")));
    }
    [Fact] public void MapTimeAndUnbuiltPoseNotificationsAreReportedOncePerFrame()
    {
        if(MarkerLibrary.EmbeddedOrNull is null)return;using var rig=new Rig();rig.SightingsPerFrame=1;
        rig.Cube=new(Mat3.Identity,new Vec3(150,-36,22));rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2,new(Mat3.Identity,new Vec3(150,36,22))));rig.OneImage();
        var missing=new List<string>();rig.Vision.World.Log+=missing.Add;rig.OneImage();
        Assert.Single(missing.Where(s=>s.Contains("map last-observed-time")));Assert.InRange(rig.Vision.World.LocatedObjects.Count,2,int.MaxValue);
        var w=new BlockWorld();var o=rig.Vision.World.LocatedObjects[0];var logs=new List<string>();w.Log+=logs.Add;w.BroadcastObjectPoseChanged(o,null,PoseState.Unknown);w.BroadcastObjectPoseChanged(o,null,PoseState.Unknown);
        Assert.Single(logs.Where(s=>s.Contains("M11-037 BlockConfigurationManager::SetObjectPoseChanged")));
    }
    [Fact] public void ReciprocalThenMultiplyChangesTheNativeStrictBarycentricEdge()
    {
        static float F(uint b)=>BitConverter.UInt32BitsToSingle(b);
        var p=new Point2f(F(0x45A2BD78),F(0x45DD5A8F));var a=new Point2f(F(0x45B1A360),F(0x460EC395));var b=new Point2f(F(0x44BC2AFD),F(0xC60A82D0));var c=new Point2f(F(0x4505355F),F(0xC4F46808));
        // Independent float32 emulation of 0x004E0406/12/44/5C/60: gamma=-1.4901161193847656e-7,
        // below 0xB4000000. Direct division gives -8.940696716308594e-8 and incorrectly accepts this point.
        Assert.False(new Quadrilateral(a,b,b,c).Contains(p));
    }
    // reach: unit (same NaN vertices through the native polygon input now required by node containment).
    // Pre-fix test fails with ArgumentException because Vec2[] cannot bind to SourceFastPolygon.
    [Fact] public void ContainsRejectsUnorderedCoordinates()
    {
        var tree=new SourceQuadTree();var method=typeof(SourceQuadTree).GetMethod("Contains",BindingFlags.NonPublic|BindingFlags.Instance)!;
        Assert.False((bool)method.Invoke(tree,new object[]{new SourceFastPolygon(new[]{new Vec2(float.NaN,0)})})!);
        Assert.False((bool)method.Invoke(tree,new object[]{new SourceFastPolygon(new[]{new Vec2(0,float.NaN)})})!);
    }
}
