using System.Collections.Concurrent;
using System.Net;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("ObjectID process statics")]
public sealed class M3M4X16BuilderTests : IDisposable
{
    private readonly bool _error = EngineErrorState.ErrorFlagSet;
    private readonly string _temp = Directory.CreateTempSubdirectory("b3416-").FullName;
    public void Dispose() { EngineErrorState.ErrorFlagSet = _error; Directory.Delete(_temp,true); }
    private CozmoRobot Robot()
    {
        var r = CozmoRobot.CreateForTest(new Port(),()=>0,new CozmoEngineOptions { DasDeviceIdPath = Path.Combine(_temp,Guid.NewGuid().ToString()) });
        r.Engine.InitOfflineLink(new IPEndPoint(IPAddress.Loopback,5551)); return r;
    }
    private sealed class Port : IEngineTransport
    {
        public bool TimedOut => false;
        public event Action<ReceiverEvent>? Received { add {} remove {} }
        public void Start() {} public void Connect(IPAddress ip, bool simulated) {}
        public void Disconnect(IPEndPoint address) {} public void SendData(byte[] data) {}
    }
    private static Pose3d At(double x,double y,double z=0,double yaw=0) => new(Mat3.AboutZ(yaw),new Vec3(x,y,z));
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    // Old code: geometry stops before Expand, omitting InsufficientExpansion from the four-entry log order.
    // Growth/shift remain MISSING: this out-of-root input reaches their existing boundary, not a fabricated insertion.
    [Fact]
    public void LiveCliffUsesConstructorMapSharedIdInputQuadAndRobotNormal()
    {
        using var robot = Robot(); var world = robot.LocalizationWorld;
        Assert.Equal(1u,world.Maps.CurrentOrigin); Assert.NotNull(world.Maps.Current);
        var entries = new ConcurrentQueue<EngineLogEntry>(); var missing = new ConcurrentQueue<string>();
        robot.Engine.LogEntry += entries.Enqueue; robot.Engine.StackLogLine += missing.Enqueue;
        try
        {
        using var robotPose = At(0,0,0);
        world.RobotTimestamp = () => 900; world.CurrentRobotPose = () => robotPose;
        // The reference native counter is shared, never reset by Robot creation: compare adjacent allocations.
        uint preceding = ObjectIdSpace.Set();
        Vec2[]? attempted = null; CliffMapData? data = null;
        world.Maps.Current.CliffInsertForTest = (q,d) => { attempted = q; data = d; };
        // Roll moves the candidate centre in Y; source map uses input, so its quad centre remains (100,200).
        using var input = new Pose3d(Mat3.AboutX(Math.PI/2),new Vec3(100,200,0));
        robot.Sensors.CliffHistoricalPoseForTest = _ => (0,input);
        robot.Engine.Broadcast(new CliffEvent { Timestamp = 100, DetectedFlags = 1 });
        var cliff = Assert.Single(world.LocatedObjects);
        Assert.Equal(preceding+1,cliff.ObjectId); Assert.Equal("Object_-1",cliff.PoseNameNode!.Name);
        Assert.Equal((byte)1,cliff.PoseInitialized);
        Assert.Equal(900u,cliff.LastObservedTimestamp); Assert.Equal(900u,data!.First);
        Assert.Equal(new Vec2(1,0),data.Normal);
        Assert.InRange(cliff.Pose.Translation.Y,174.999,175.001);
        Assert.Equal(100,attempted!.Average(p => p.X),3); Assert.Equal(200,attempted.Average(p => p.Y),3);
        Assert.Equal(0,world.PoseChangeCount);
        Assert.DoesNotContain(entries,e => e.Name.Contains("FirstPoseForObject") || e.Name.Contains("MultipleChanges"));
        Assert.Equal(new[] { "BlockWorld.AddLocatedObject","robot.object_located","QuadTree.Expand.InsufficientExpansion","RobotImplMessaging.HandleCliffEvent.Detected" },entries.Select(e=>e.Name));
        Assert.Contains(missing,s=>s.Contains("M11-048")); Assert.Empty(world.Maps.Current.Regions);
        }
        finally
        {
            robot.Engine.LogEntry -= entries.Enqueue;
            robot.Engine.StackLogLine -= missing.Enqueue;
        }
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void AllOverlapsAreStampedWithInclusiveBoxToleranceAndPiOverride()
    {
        var w = new BlockWorld { RobotTimestamp = () => 777, CurrentRobotPose = () => Pose3d.Identity };
        var a = new ObservableObject(300,ObjectType.CliffDetection,Array.Empty<KnownMarker>()) { OriginId=1,Pose=At(-10,0,25),PoseState=PoseState.Known };
        var b = new ObservableObject(301,ObjectType.CliffDetection,Array.Empty<KnownMarker>()) { OriginId=1,Pose=At(10,0,25),PoseState=PoseState.Known };
        a.PoseNameNode = new PoseTreeNameNode("Object_300") { Parent = new PoseTreeNameNode("Origin1") };
        b.PoseNameNode = new PoseTreeNameNode("Object_301") { Parent = a.PoseNameNode.Parent };
        w.AddLocatedObject(a);w.AddLocatedObject(b);
        var returned = w.AddMarkerlessObject(new Pose3d(Math.PI,new Vec3(0,0,1),Vec3.Zero),ObjectType.CliffDetection);
        Assert.Same(a,returned); Assert.Equal(777u,a.LastObservedTimestamp);Assert.Equal(777u,b.LastObservedTimestamp);
        Assert.Equal(2,w.LocatedObjects.Count); // both boundary hits; angle pi cannot reject
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void FrameCasesKeepParentedFailureSilentAndUseIdentityForOtherAsParent()
    {
        var w=new BlockWorld();var keys=new List<string>();var missing=new List<string>();w.SourceLog=(l,c,k,s)=>keys.Add(k);w.Log+=missing.Add;
        var a=new ObservableObject(100,ObjectType.CliffDetection,Array.Empty<KnownMarker>()) { OriginId=1,PoseNameNode=new PoseTreeNameNode("a") };
        var b=new ObservableObject(101,ObjectType.CliffDetection,Array.Empty<KnownMarker>()) { OriginId=2,Pose=At(500,0),PoseNameNode=new PoseTreeNameNode("b") };
        var method=typeof(BlockWorld).GetMethod("MarkerlessSameAs",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        bool Same()=> (bool)method.Invoke(w,new object[] {a,b,new Vec3(20,40,50)})!;
        a.PoseNameNode!.Parent=new PoseTreeNameNode("Origin1");Assert.False(Same());Assert.Empty(keys);
        a.PoseNameNode.Parent=null;Assert.False(Same());Assert.Equal("Pose3d.IsSameAs.PosesHaveDifferentOrigins",Assert.Single(keys));
        // The other-as-parent branch precedes frame conversion: temp is identity, not b's 500-mm translation.
        a.PoseNameNode.Parent=b.PoseNameNode;Assert.True(Same());
        b.OriginId=1;a.PoseNameNode.Parent=null;Assert.IsType<NotSupportedException>(Assert.Throws<System.Reflection.TargetInvocationException>(()=>Same()).InnerException);Assert.Contains(missing,s=>s.Contains("M12 pose records"));
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void CliffDataCloneSharesNoTimestampsAndEqualityUsesStrictNormalBound()
    {
        var a = new CliffMapData(new Vec2(1,0),100);var clone=(CliffMapData)a.Clone();clone.First=2;clone.Last=999;
        Assert.True(a.Same(clone));Assert.Equal(100u,a.First);
        float tolerance = BitConverter.UInt32BitsToSingle(0x35A00000);
        var zero = new CliffMapData(new Vec2(0,0),0);
        Assert.False(zero.Same(new CliffMapData(new Vec2(tolerance,0),0)));
        Assert.True(zero.Same(new CliffMapData(new Vec2(MathF.BitDecrement(tolerance),0),99)));
        Assert.False(zero.Same(new CliffMapData(new Vec2(float.NaN,0),0)));
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void NativeAreaRetainsParentCreditThroughSubdivisionAndMerge()
    {
        var t = new SourceQuadTree { OverlapForTest = (_,_) => 2 }; // isolates UNKNOWN M11-048 predicate; no geometry claim
        t.Insert(Array.Empty<Vec2>(),new CliffMapData(new Vec2(1,0),7),1);
        double nativeArea = BitConverter.UInt32BitsToSingle(0x3CD1B719); // (160*0x3A83126F)^2, float -> double, checked P callback
        Assert.Equal(nativeArea,t.Processor.ExploredArea);
        t.Root.Subdivide(t.Processor);
        Assert.Equal(nativeArea*2,t.Processor.ExploredArea);
        Assert.Equal(7u,t.Root.Content.Data.First);Assert.Equal(7u,t.Root.Content.Data.Last);
        Assert.Equal(new[] {(40f,40f),(40f,-40f),(-40f,40f),(-40f,-40f)},t.Root.Children.Select(c=>(c!.X,c.Y)));
        var shared=t.Root.Children[0]!.Content.Data;
        t.Root.TryAutoMerge(t.Processor);
        Assert.Empty(t.Root.Children);Assert.Same(shared,t.Root.Content.Data);Assert.Equal(nativeArea*2,t.Processor.ExploredArea);
        Assert.Contains(t.Root,t.Processor.TypeSets[MemoryMapContentType.Cliff]);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ForceRemovalEdgeAreaSwapAndDestroyFollowProcessorCallbacks()
    {
        var tree=new SourceQuadTree();var n=tree.Root;
        n.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.InterestingEdge,1,2)),tree.Processor);
        double a=BitConverter.UInt32BitsToSingle(0x3CD1B719);
        Assert.Equal(a,tree.Processor.EdgeArea);Assert.Equal(a,tree.Processor.ExploredArea);
        n.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.ObstacleChargerRemoved,99,123)),tree.Processor);
        Assert.Equal(MemoryMapContentType.ClearOfObstacle,n.Content.Data.Type);Assert.Equal(123u,n.Content.Data.First);Assert.Equal(123u,n.Content.Data.Last);Assert.Equal(0,tree.Processor.EdgeArea);
        n.Subdivide(tree.Processor);
        var other=new SourceQuadNode(0,0,1,80,3);other.ForceSet(new SourceNodeContent(new CliffMapData(new Vec2(1,0),5)),tree.Processor);
        n.SwapChildrenAndContent(other,tree.Processor);
        Assert.Empty(n.Children);Assert.All(other.Children,c=>Assert.Same(other,c!.Parent));Assert.Equal(a*3.25,tree.Processor.ExploredArea);
        other.DestroyNodes(tree.Processor);Assert.Equal(4,other.Children.Count);Assert.All(other.Children,Assert.Null);
        Assert.Equal(a*2.25,tree.Processor.ExploredArea);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void MergeIncludesChildThreeTimestampsAndWritesSharedChildZero()
    {
        var t = new SourceQuadTree(); t.Root.Subdivide(t.Processor);
        for(int i=0;i<4;i++) t.Root.Children[i]!.ForceSet(new SourceNodeContent(new CliffMapData(new Vec2(1,0),(uint)(i+10))),t.Processor);
        t.Root.Children[3]!.Content.Data.First=1;t.Root.Children[3]!.Content.Data.Last=999;
        var shared=t.Root.Children[0]!.Content.Data;t.Root.TryAutoMerge(t.Processor);
        Assert.Same(shared,t.Root.Content.Data);Assert.Equal(1u,shared.First);Assert.Equal(999u,shared.Last);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ExpansionGateTypoKeyRemovalWarningAndDirtyMatchCheckedRows()
    {
        var tree=new SourceQuadTree();var keys=new List<string>();var missing=new List<string>();tree.SourceLog=(l,c,k,s)=>keys.Add(k);tree.Missing=missing.Add;
        tree.Root.Depth=255;Assert.False(tree.UpgradeRootLevel(new Vec2(0,0),8));tree.Root.Depth=8;Assert.False(tree.UpgradeRootLevel(new Vec2(0,0),8));Assert.Empty(missing);
        tree.Root.Depth=7;tree.UpgradeBodyForTest=(_,_)=>{tree.Root.Depth=8;tree.Root.Side=2560;return true;}; // isolates unread body, verifies checked gate/log only
        Assert.True(tree.UpgradeRootLevel(new Vec2(0,0),8));Assert.Equal("QuadTree.UpdgradeRootLevel",Assert.Single(keys));
        keys.Clear();tree.UpgradeBodyForTest=null;tree.Expand(new Vec2(999,1000),1,()=>false);
        Assert.True(tree.Dirty);Assert.Equal("QuadTree.Expand.InsufficientExpansion",Assert.Single(keys));Assert.Contains(missing,s=>s.Contains("ShiftRoot"));
        var removal=new SourceQuadTree { OverlapForTest=(_,_)=>0, SourceLog=(l,c,k,s)=>keys.Add(k) }; // isolates UNKNOWN overlap, native warning comes before node insert
        removal.Insert(new[] {new Vec2(81,81)},new SourceMapData(MemoryMapContentType.ObstacleChargerRemoved,1,2),1);
        Assert.Equal("QuadTree.Insert.RemovalPolyNotContained",keys[^1]);Assert.False(removal.Dirty);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void MapLifecyclePrintsArgumentOriginAndKeepsCurrentHelperNonNull()
    {
        var m = new SourceMapComponent();Assert.Equal(0u,m.CurrentOrigin);Assert.Empty(m.Maps);
        var log = new List<(EngineLogLevel,string,string)>(); m.SourceLog=(l,c,k,s)=>log.Add((l,k,s));
        var missing = new List<string>();m.Missing=missing.Add;
        m.CreateLocalizedMemoryMap(1,_=>false);Assert.Empty(log);Assert.NotNull(m.Current);
        m.CreateLocalizedMemoryMap(2,id=>id==1);
        Assert.Equal((EngineLogLevel.Event,"MapComponent.memory_map.deleting_zombie_map","2"),Assert.Single(log));
        log.Clear();m.CreateLocalizedMemoryMap(3,_=>false);
        Assert.Equal("3",Assert.Single(log).Item3);Assert.Equal("MapComponent.memory_map.keeping_alive_map",log[0].Item2);
        log.Clear();bool objects=false;m.UpdateMapOrigins(3,4,()=>objects=true);
        Assert.True(objects);Assert.Equal("MapComponent.UpdateObjectOrigins.NullMapFound",Assert.Single(log).Item2);
        Assert.True(EngineErrorState.ErrorFlagSet);Assert.Equal(4u,m.CurrentOrigin);Assert.False(m.Maps.ContainsKey(3));Assert.NotNull(m.Current);
        Assert.Contains(missing,s=>s.Contains("M11-048"));
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void PlayAreaStrictFloatGateRunsBeforeWindowAndNaNSkips()
    {
        double now=60;var w=new BlockWorld { BaseStationSeconds=()=>now };
        var pd=new VisionPoseData(0,Pose3d.Identity,0,0,false,false);var cam=new CameraModel(CameraCalibration.Nominal(),pd.CameraPose);
        var seen=new List<bool>();var keys=new List<string>();w.SourceLog=(l,c,k,s)=>keys.Add(k);
        w.Log+=s=>{if(s.Contains("robot.play_area_size"))seen.Add(w.PoseChangeWindow);};
        w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(),cam,0,pd);Assert.Empty(seen);Assert.False(w.PoseChangeWindow);
        now=61;w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(),cam,0,pd);Assert.False(Assert.Single(seen));
        now=121;w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(),cam,0,pd);Assert.Single(seen); // stored 61, strict 61+60 < now.
        now=double.NaN;w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(),cam,0,pd);Assert.Single(seen);
        Assert.DoesNotContain("robot.play_area_size",keys);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void PoseQueueUsesFirstDuplicateAndFlagZeroBranchesAndAlwaysNotifiesConfiguration()
    {
        var w = new BlockWorld(); var o = new ObservableObject(50,ObjectType.CliffDetection,Array.Empty<KnownMarker>()) { OriginId=1,PoseState=PoseState.Known };
        var keys = new List<string>();int notifications=0;w.SourceLog=(l,c,k,s)=>keys.Add(k);w.SetObjectPoseChanged=(_,_)=>notifications++;
        w.BroadcastObjectPoseChanged(o,null,PoseState.Unknown);Assert.Empty(keys);Assert.Equal(1,notifications);
        typeof(BlockWorld).GetField("_appendPoseChanges",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(w,true);
        w.BroadcastObjectPoseChanged(o,null,PoseState.Unknown);Assert.Equal("BlockWorld.OnObjectPoseChanged.FirstPoseForObject",Assert.Single(keys));Assert.Equal(0,w.PoseChangeCount);
        w.BroadcastObjectPoseChanged(o,Pose3d.Identity,PoseState.Known);Assert.Equal(1,w.PoseChangeCount);
        w.BroadcastObjectPoseChanged(o,Pose3d.Identity,PoseState.Known);Assert.Equal("BlockWorld.OnObjectPoseChanged.MultipleChanges",keys[^1]);Assert.Equal(1,w.PoseChangeCount);Assert.Equal(4,notifications);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void AddAndUpdateFailureLeavesWindowSetAndSkipsTail()
    {
        var w=new BlockWorld { LocalizeRobotResultForTest=()=>7 }; // dependency isolation: M11-044 failure internals not reproduced here
        bool tail=false;w.BlockConfigurationManagerUpdate=()=>tail=true;
        var keys=new List<string>();w.SourceLog=(l,c,k,s)=>keys.Add(k);
        var pd=new VisionPoseData(0,Pose3d.Identity,0,0,false,false);
        var camera=new CameraModel(CameraCalibration.Nominal(),pd.CameraPose);
        // Unknown marker creates no objects, but still enters AddAndUpdateObjects and its result propagation.
        var result=w.UpdateObservedMarkers(new[] {new ObservedMarker(unchecked((MarkerType)ushort.MaxValue),Array.Empty<Vec2>(),1,0,0)},camera,1,pd);
        Assert.Equal(7u,result.Status);Assert.True(w.PoseChangeWindow);Assert.False(tail);
        Assert.Contains("BlockWorld.UpdateObservedMarkers.AddAndUpdateFailed",keys);Assert.True(EngineErrorState.ErrorFlagSet);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void VisionFailureStillCallsDockingBeforeLocalHandlerError()
    {
        using var robot=Robot();var vision=robot.Vision; vision.Calibration=CameraCalibration.Nominal(); vision.Enabled=false;
        vision.World.LocalizeRobotResultForTest=()=>7;
        var order=new List<string>();robot.Engine.LogEntry+=e=> { if(e.Name is "BlockWorld.UpdateObservedMarkers.AddAndUpdateFailed" or "VisionComponent.UpdateVisionResults.BlockWorldUpdateFailed" or "VisionComponent.UpdateAllResults.LocalHandlerFailed") order.Add(e.Name); };
        vision.DockingMarkersProcessed+=_=>order.Add("docking");
        var cal=CameraCalibration.Nominal();var pd=new VisionPoseData(100,Pose3d.Identity,-.15,0,false,false);
        var image=new GrayImage(cal.Columns,cal.Rows);image.Fill(150);
        MarkerRenderer.DrawObject(image,MarkerLibrary.EmbeddedOrNull!,new CameraModel(cal,pd.CameraPose),ObjectType.Block_LIGHTCUBE1,At(150,0,22));
        var result=vision.ProcessImage(image,100,100,pd);
        Assert.NotEmpty(result.Markers);
        Assert.Equal(new[] {"BlockWorld.UpdateObservedMarkers.AddAndUpdateFailed","VisionComponent.UpdateVisionResults.BlockWorldUpdateFailed","docking","VisionComponent.UpdateAllResults.LocalHandlerFailed"},order);
        Assert.True(vision.World.PoseChangeWindow);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void DeletionDefersNotificationsAndNullWarningPrecedesPredicate()
    {
        var w=new BlockWorld { RobotTimestamp=()=>120 };
        var o=new ObservableObject(66,ObjectType.CliffDetection,Array.Empty<KnownMarker>()) { OriginId=1,PoseState=PoseState.Known };
        w.AddLocatedObject(o);var order=new List<string>();w.SetObjectPoseChanged=(id,s)=>order.Add("pose");w.LocatedObjectDeleted+=id=>{Assert.Equal(66u,id);Assert.Null(w.GetLocatedObjectById(id));order.Add("deleted");};
        var f=new BlockWorldFilter();f.AllowedIds.Add(66);w.DeleteLocatedObjects(f);
        Assert.Equal(new[] {"pose","deleted"},order);Assert.Equal(120u,w.TimeOfLastChangeField);
        var field=typeof(BlockWorld).GetField("_located",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        var map=(SortedDictionary<(uint Origin,int Family,int Type,int Id),ObservableObject>)field.GetValue(w)!;
        map[(1,6,15,99)]=null!;bool predicate=false;var keys=new List<string>();w.SourceLog=(l,c,k,s)=>keys.Add(k);
        f.Predicates.Add(_=>{predicate=true;return false;});w.DeleteLocatedObjects(f);
        Assert.False(predicate);Assert.Equal("BlockWorld.DeleteLocatedObjects.NullObject",Assert.Single(keys));Assert.Empty(map);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void NaNInsertLogsBeforeErrorFlagAndDoesNotMutate()
    {
        var t=new SourceQuadTree();bool? atLog=null;t.SourceLog=(l,c,k,s)=>{Assert.Equal("QuadTree.Insert.NaNPoly",k);atLog=EngineErrorState.ErrorFlagSet;};
        EngineErrorState.ErrorFlagSet=false;t.Insert(new[] {new Vec2(float.NaN,0),new Vec2(0,float.NaN)},new CliffMapData(new Vec2(1,0),1),1);
        Assert.False(atLog);Assert.True(EngineErrorState.ErrorFlagSet);Assert.False(t.Dirty);Assert.Equal(0,t.Processor.ExploredArea);
    }
}
