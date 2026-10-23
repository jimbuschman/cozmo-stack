using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M13ChargerMapTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly Pose3d Root=new("map root") {Id=101};
        internal readonly Pose3d RobotPose;
        internal readonly Pose3d ObjectPose;
        internal readonly BlockWorld World=new();
        internal readonly ObservableObject Object=new(77,ObjectType.Charger_Basic,Array.Empty<KnownMarker>());
        internal readonly MemoryMap Memory=new();
        internal SourceMapComponent Maps=>World.Maps;
        internal Fixture()
        {
            RobotPose=new("robot",Root);ObjectPose=new("charger",Root);
            Object.NativePose.Dispose();Object.InitPose(ObjectPose,PoseState.Known);
            Maps.Maps[Root.Id]=Memory;Maps.RobotPose=()=>RobotPose;Maps.ImageTimestamp=()=>123u;
        }
        internal void Move(float x=0,float y=0,float z=0,float angle=0)
        {
            // 0x004EF598..0x004EF5A2 copies the input; moving that input is not itself a native SetPose.
            Assert.NotSame(ObjectPose.Node,Object.NativePose.Node); // B-M13-22 old setter aliases the two nodes
            ObjectPose.Node!.Transform=new Transform3d(Rotation3d.FromAngleAxis(angle,new Vec3(0,0,1)),new Vec3(x,y,z));
            Object.SetPose(ObjectPose,-1,PoseState.Known);
        }
        internal void Add()=>Maps.UpdateObjectPose(Object,null,PoseState.Unknown);
        public void Dispose(){Maps.Dispose();Object.ChargerMarker?.PoseOnObject.Dispose();Object.NativePose.Dispose();ObjectPose.Dispose();RobotPose.Dispose();Root.Dispose();}
    }

    // reach: unit (supplies owned poses and camera time; real confirmer/world/map caller; generic tree M11-048 remains MISSING).
    // Old code: 0 native inserts (only the unbound height/continuation diagnostic), versus 2.
    [Fact]
    public void InvalidToKnownInsertsTwiceAfterWorldBeforeStateTailReturns()
    {
        using var f=new Fixture();var order=new List<string>();
        f.World.SetObjectPoseChanged=(_,_)=>order.Add("world");
        f.Memory.ObjectInsertForTest=(_,_)=>order.Add("insert");
        f.Memory.ObjectBorderForTest=(_,_,_)=>order.Add("border");
        f.World.BroadcastObjectPoseChanged(f.Object,null,PoseState.Unknown);
        Assert.Equal(new[]{"world","insert","border","insert","border"},order);
    }

    // reach: unit (owned poses/time; real cache and IsSameAs primitive; generic geometry M11-048 remains MISSING).
    // Old code: 0 inserts and no cache entry; new code inserts once and retains that entry across the skip.
    [Theory]
    [InlineData(4f,0f,0f,0f)]
    [InlineData(0f,4f,0f,0f)]
    [InlineData(0f,0f,4f,0f)]
    [InlineData(0f,0f,0f,0.1f)]
    // B-M13-22 old value: input and charger share one pose node; native SetPose copies, so this case now checks independence.
    public void ValidWithinThresholdSkipsWithoutTimestampDedup(float x,float y,float z,float angle)
    {
        using var f=new Fixture();int inserts=0;f.Memory.ObjectInsertForTest=(_,_)=>inserts++;
        f.Add();var cached=f.Maps.CachedObjectPose(77,101)!.Value.Pose;
        f.Move(x,y,z,angle);f.Maps.UpdateObjectPose(f.Object,f.ObjectPose,PoseState.Known);
        Assert.Equal(1,inserts);Assert.Same(cached,f.Maps.CachedObjectPose(77,101)!.Value.Pose);
        f.Move(20);f.Maps.UpdateObjectPose(f.Object,f.ObjectPose,PoseState.Known);
        Assert.Equal(2,inserts); // same image timestamp, outside tolerance still retries
    }

    // reach: unit (owned robot/object poses and time; real height helper/cache and retry dispatch).
    // Old code: no cache entry and no remove/add continuation; new code stores the ORIGINAL high pose then retries.
    [Fact]
    // B-M13-22 old value: input and charger share one pose node; native SetPose copies, so this case now checks independence.
    public void FalseHeightEntryRetriesRemoveThenAddRatherThanTakingAbsentEntryBranch()
    {
        using var f=new Fixture();f.Move(z:200);f.Add();
        var high=f.Maps.CachedObjectPose(77,101)!.Value;
        Assert.False(high.Added);Assert.Equal(200d,high.Pose.Translation.Z);
        Assert.Same(f.Root.Node,high.Pose.Node!.Parent);
        var order=new List<string>();f.Memory.ObjectRemovalForTest=_=>order.Add("remove");
        f.Memory.ObjectInsertForTest=(_,_)=>order.Add("insert");
        f.Move();f.Maps.UpdateObjectPose(f.Object,f.ObjectPose,PoseState.Known);
        Assert.Equal(new[]{"remove","insert"},order);Assert.True(f.Maps.CachedObjectPose(77,101)!.Value.Added);
    }

    // reach: unit (supplies owned robot/object roots and time; tree geometry MISSING at 0x006817D2).
    // Old code: 0 inserts/cache entries. New: 1 per root and no removal for either absent entry.
    [Fact]
    // B-M13-22 old value: input and charger share one pose node; native SetPose copies, so this case now checks independence.
    public void AbsentValidEntryAddsWithoutRemovalAndKeepsSeparateRootEntries()
    {
        using var otherRoot=new Pose3d("other") {Id=202};using var f=new Fixture();
        int inserts=0,removes=0;f.Memory.ObjectInsertForTest=(_,_)=>inserts++;
        f.Memory.ObjectRemovalForTest=_=>removes++;
        f.Maps.UpdateObjectPose(f.Object,f.ObjectPose,PoseState.Known);
        var otherMap=new MemoryMap();otherMap.ObjectInsertForTest=(_,_)=>inserts++;
        otherMap.ObjectRemovalForTest=_=>removes++;
        f.Maps.Maps[202]=otherMap;f.ObjectPose.SetParent(otherRoot);f.RobotPose.SetParent(otherRoot);
        Assert.NotSame(f.ObjectPose.Node,f.Object.NativePose.Node); // B-M13-22 old setter shares input node
        f.Object.SetPose(f.ObjectPose,-1,PoseState.Known);
        f.Maps.UpdateObjectPose(f.Object,f.ObjectPose,PoseState.Known);
        Assert.Equal(2,inserts);Assert.Equal(0,removes);
        Assert.True(f.Maps.CachedObjectPose(77,101)!.Value.Added);Assert.True(f.Maps.CachedObjectPose(77,202)!.Value.Added);
    }

    // reach: unit (supplies owned robot/object poses and camera time; real GetWithRespectTo and height helper).
    // Old code: no true entry. New: true even at world z200, because the actual robot-relative z is zero.
    [Fact]
    // B-M13-22 old value: input and charger share one pose node; native SetPose copies, so this case now checks independence.
    public void HeightUsesRobotRelativePoseRatherThanWorldZ()
    {
        using var f=new Fixture();f.Move(z:200);
        f.RobotPose.Node!.Transform=new Transform3d(Rotation3d.Identity,new Vec3(0,0,200));
        f.Add();Assert.True(f.Maps.CachedObjectPose(77,101)!.Value.Added);
        Assert.Equal(200d,f.Maps.CachedObjectPose(77,101)!.Value.Pose.Translation.Z);
    }

    // reach: unit (owned poses/time; real map gate; height delegate is a trap, not a replacement result).
    // Old and new: 0 inserts/cache entries. Old incorrectly reaches the height delegate once; new reaches it 0 times.
    [Fact]
    public void RootWithoutMapReturnsBeforeHeightAndCachesNothing()
    {
        using var f=new Fixture();f.Maps.Maps.Remove(101);int height=0;
        f.Maps.ObjectPoseRelativeToRobot=(_,_)=>{height++;return null;};
        f.Add();Assert.Equal(0,height);Assert.Null(f.Maps.CachedObjectPose(77,101));
    }

    // reach: unit (supplies owned poses/time; generic FastPolygon join MISSING at 0x006817D2).
    // Old code: 0 inserts, no cache, no border call. New: base type4, both stamps123, true cache BEFORE border.
    [Fact]
    public void AcceptedChargerUsesBaseDataAndCachesEvenWhenTreeInsertCannotComplete()
    {
        using var f=new Fixture();int inserts=0,borders=0;
        f.Memory.ObjectInsertForTest=(polygon,data)=>
        {
            inserts++;Assert.Equal(4,polygon.Length);Assert.IsType<SourceMapData>(data);
            Assert.Equal(MemoryMapContentType.ObstacleCharger,data.Type);Assert.Equal(123u,data.First);Assert.Equal(123u,data.Last);
            Assert.Null(f.Maps.CachedObjectPose(77,101));
        };
        f.Memory.ObjectBorderForTest=(type,mask,data)=>
        {
            borders++;Assert.True(f.Maps.CachedObjectPose(77,101)!.Value.Added);
            Assert.Equal(MemoryMapContentType.InterestingEdge,type);Assert.Equal(0x4F8u,mask);
            Assert.Equal(MemoryMapContentType.NotInterestingEdge,data.Type);Assert.Equal(123u,data.First);Assert.Equal(123u,data.Last);
        };
        f.Add();Assert.Equal(1,inserts);Assert.Equal(1,borders);Assert.True(f.Memory.Tree.Dirty);
        Assert.Empty(f.Memory.Regions); // no polygon-list substitute for missing generic tree geometry
    }

    // reach: unit (supplies poses/time and existing map; real callback with supplied derived ObjectID;
    // whole-map Transform and derived reader MISSING at 0x0068140C/0x0068110E).
    // Old code: no removal callback dispatched; new dispatches once. Type4 preservation itself is an existing invariant.
    [Fact]
    public void ValidToInvalidUsesOldRootAndRemovalCallbackLeavesType4Untouched()
    {
        using var f=new Fixture();var charger=new SourceMapData(MemoryMapContentType.ObstacleCharger,1,2);
        int removes=0;
        f.Memory.ObjectRemovalForTest=removal=>
        {
            removes++;Assert.Equal(77u,removal.ObjectId);Assert.Equal(MemoryMapContentType.ObstacleChargerRemoved,removal.RemovalType);
            Assert.Same(charger,removal.Apply(charger,77));
            var observable=new SourceMapData(MemoryMapContentType.ObstacleObservable,1,2);
            Assert.Same(observable,removal.Apply(observable,78));
            var replaced=removal.Apply(observable,77);Assert.Equal(MemoryMapContentType.ObstacleChargerRemoved,replaced.Type);
            Assert.Equal(123u,replaced.First);Assert.Equal(123u,replaced.Last);
        };
        using var otherRoot=new Pose3d("new root") {Id=202};
        using var otherPose=new Pose3d("new pose",otherRoot);
        f.Object.Pose=otherPose;f.Object.PoseState=PoseState.Unknown;
        f.Maps.UpdateObjectPose(f.Object,f.ObjectPose,PoseState.Known);Assert.Equal(1,removes);
    }

    // reach: unit (substitutes insert overlap; real native ray-tree query, not production charger geometry).
    // Old code: false ray result despite the forced type4 root, because only the empty legacy list was queried.
    [Fact]
    public void ChargerTreeContentHasW1BlockingTypeAndNativeRayCoverage()
    {
        using var f=new Fixture();f.Memory.Tree.OverlapForTest=(_,_)=>2;
        f.Add();Assert.Equal(MemoryMapContentType.ObstacleCharger,f.Memory.Tree.Root.Content.Data.Type);
        Assert.True(MemoryMapTypes.BlocksTheRobot(f.Memory.Tree.Root.Content.Data.Type));
        Assert.Empty(f.Memory.Regions);
        Assert.True(f.Memory.HasCollisionRayWithTypes(new Vec2(0,0),new Vec2(40,0)));
    }

    // reach: production (real contact ingress -> confirmer -> world -> native map insert boundary;
    // generic tree geometry/border and W1 query remain MISSING; no hardware).
    // Old code: 0 native inserts on contact, versus 2; no map-input delegates are substituted here.
    [Fact]
    public void RealContactIngressReachesBothChargerInserts()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        var world=rig.Robot.Vision.World;var map=world.Maps.Current!;int inserts=0;
        map.ObjectInsertForTest=(_,data)=>{Assert.Equal(MemoryMapContentType.ObstacleCharger,data.Type);inserts++;};
        rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger,origin:999));rig.Tick();
        Assert.Equal(2,inserts);
    }
}
