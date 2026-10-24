using System.Collections;
using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M13BorderFillTests
{
    private static MemoryMap Patch()
    {
        var map=new MemoryMap();var root=map.Tree.Root;
        root.Side=20;root.Depth=1;root.Bounds=new(0,0,20);root.Subdivide(map.Tree.Processor);
        root.Children[0]!.ForceSet(new(new SourceMapData(MemoryMapContentType.InterestingEdge,1,1)),map.Tree.Processor);
        foreach(int i in new[]{1,2})root.Children[i]!.ForceSet(new(new SourceMapData(MemoryMapContentType.ObstacleObservable,2,2)),map.Tree.Processor);
        return map;
    }
    private static object Field(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(obj)!;
    private static object Combination(MemoryMap map)=>Assert.Single(((IEnumerable)Field(map.Tree.Processor,"Borders")).Cast<object>()).GetType().GetProperty("Value")!.GetValue(Assert.Single(((IEnumerable)Field(map.Tree.Processor,"Borders")).Cast<object>()))!;
    private static object[] Waypoints(object combination)=>((IEnumerable)Field(combination,"Waypoints")).Cast<object>().ToArray();
    // reach: unit (supplies a valid small native tree; actual wrapper, walk, point insertion and ray).
    // Old value: no type-10 native cells; legacy FillObjectBorder only emits MISSING.
    [Fact]
    public void AdjacentTypeNineGetsTypeTenAtItsOwnCentreWithWrapperStamp()
    {
        var map=Patch();map.FillObjectBorder(123);
        var node=map.Tree.Root.Children[0]!;
        Assert.Equal(MemoryMapContentType.NotInterestingEdge,node.Content.Data.Type);
        Assert.Equal((123u,123u),(node.Content.Data.First,node.Content.Data.Last));
        Assert.Equal((5f,5f),(node.X,node.Y));Assert.True(map.Tree.Dirty);Assert.Empty(map.Regions);
        Assert.True(map.HasCollisionRayWithTypes(new(4,5),new(6,5),t=>t==MemoryMapContentType.NotInterestingEdge));
        Assert.Equal(MemoryMapContentType.ObstacleObservable,map.Tree.Root.Children[1]!.Content.Data.Type);
    }
    // reach: unit (supplied tree; same-type output retains cache so its ordered waypoint flags can be observed).
    // Old value: FillBorder returns 0, with no native waypoint vector; new value is 1 unique source-centre insert.
    [Fact]
    public void CheckedContactClosesWithDuplicateAndCleanCacheIsReused()
    {
        var map=Patch();
        int Run()=>map.FillBorder(MemoryMapContentType.InterestingEdge,t=>t==MemoryMapContentType.ObstacleObservable,MemoryMapContentType.InterestingEdge,77);
        Assert.Equal(1,Run());var cache=Combination(map);var first=Waypoints(cache);
        Assert.Equal(3,first.Length);
        Assert.Equal(new[]{1,2,1},first.Select(p=>(int)Field(p,"Direction")));
        Assert.True((bool)Field(first[0],"Start"));Assert.False((bool)Field(first[0],"End"));
        Assert.True((bool)Field(first[2],"End"));Assert.False((bool)Field(first[2],"Start"));
        Assert.Same(Field(first[0],"From"),Field(first[2],"From"));Assert.Same(Field(first[0],"To"),Field(first[2],"To"));
        Assert.False((bool)Field(cache,"Dirty"));Assert.Equal(1,Run());
        Assert.Same(first[0],Waypoints(cache)[0]);
    }
    // reach: unit (valid native tree and ForceSet; actual type-change callback and combination refresh).
    // Old value: FillBorder returns 0 and has no combination invalidation/cache state.
    [Fact]
    public void TypeChangeClearsTheCacheButSameTypeForceSetDoesNot()
    {
        var map=Patch();
        int Run()=>map.FillBorder(MemoryMapContentType.InterestingEdge,t=>t==MemoryMapContentType.ObstacleObservable,MemoryMapContentType.InterestingEdge,77);
        Assert.Equal(1,Run());var cache=Combination(map);var first=Waypoints(cache)[0];
        var neighbor=map.Tree.Root.Children[1]!;
        neighbor.ForceSet(new(new SourceMapData(MemoryMapContentType.ObstacleObservable,8,9)),map.Tree.Processor);
        Assert.False((bool)Field(cache,"Dirty"));Assert.Same(first,Waypoints(cache)[0]);
        neighbor.ForceSet(new(new SourceMapData(MemoryMapContentType.Cliff,8,9)),map.Tree.Processor);
        Assert.True((bool)Field(cache,"Dirty"));Assert.Empty(Waypoints(cache));
        Assert.Equal(1,Run());Assert.False((bool)Field(cache,"Dirty"));Assert.NotSame(first,Waypoints(cache)[0]);
        map.Tree.Root.Children[2]!.ForceSet(new(new SourceMapData(MemoryMapContentType.Cliff,8,9)),map.Tree.Processor);
        Assert.Equal(0,Run());Assert.Empty(Waypoints(cache));
    }
    // reach: unit (supplied tree/types; real type-set remove/reinsert and native walk).
    // Old value: no native border inserts (FillBorder returns 0).
    [Fact]
    public void ReenteredTypeSetSeedsFollowInsertionOrder()
    {
        var map=Patch();var root=map.Tree.Root;
        root.Children[3]!.ForceSet(new(new SourceMapData(MemoryMapContentType.ObstacleObservable,2,2)),map.Tree.Processor);
        root.Children[2]!.ForceSet(new(new SourceMapData(MemoryMapContentType.InterestingEdge,1,1)),map.Tree.Processor);
        root.Children[0]!.ForceSet(new(new SourceMapData(MemoryMapContentType.ClearOfObstacle,1,1)),map.Tree.Processor);
        root.Children[0]!.ForceSet(new(new SourceMapData(MemoryMapContentType.InterestingEdge,1,1)),map.Tree.Processor);
        Assert.Equal(2,map.FillBorder(MemoryMapContentType.InterestingEdge,t=>t==MemoryMapContentType.ObstacleObservable,MemoryMapContentType.InterestingEdge,77));
        Assert.Same(root.Children[2],Field(Waypoints(Combination(map))[0],"From"));
    }
    // reach: unit (supplied tree; actual invalidation during the first direct point insert).
    // Old value: both cells remain type9; FillObjectBorder performs no inserts.
    [Fact]
    public void AllCentresAreSnapshottedBeforeTheFirstInsertClearsWaypoints()
    {
        var map=Patch();var root=map.Tree.Root;
        root.Children[2]!.ForceSet(new(new SourceMapData(MemoryMapContentType.InterestingEdge,1,1)),map.Tree.Processor);
        root.Children[3]!.ForceSet(new(new SourceMapData(MemoryMapContentType.ObstacleObservable,2,2)),map.Tree.Processor);
        map.FillObjectBorder(456);
        Assert.All(new[]{0,2},i=>Assert.Equal(MemoryMapContentType.NotInterestingEdge,root.Children[i]!.Content.Data.Type));
        var cache=Combination(map);Assert.True((bool)Field(cache,"Dirty"));Assert.Empty(Waypoints(cache));
    }
    // reach: production (contact RobotState dispatch -> charger observation -> current map -> FillObjectBorder).
    // Old value: the caller reaches only a FillBorder MISSING, and no border combination exists.
    [Fact]
    public void ContactChargerReachesTheNativeBorderProcessor()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger);
        var map=Assert.IsType<MemoryMap>(rig.Robot.LocalizationWorld.Maps.Current);
        var field=map.Tree.Processor.GetType().GetField("Borders",BindingFlags.Instance|BindingFlags.NonPublic);
        Assert.NotNull(field);Assert.NotEmpty((IEnumerable)field!.GetValue(map.Tree.Processor)!);
    }
}
