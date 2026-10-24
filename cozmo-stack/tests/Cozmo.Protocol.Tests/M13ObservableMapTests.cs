using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M13ObservableMapTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static SourceMapData Data(uint id,uint stamp,params P2f[] points)
    {
        var type=typeof(MemoryMap).Assembly.GetType("Cozmo.Robot.Vision.ObservableMapData");
        Assert.NotNull(type); // Old code has no derived payload; the live add returns at its MISSING.
        return (SourceMapData)Activator.CreateInstance(type!,Private,null,new object[]{id,points,stamp},null)!;
    }
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static void Transform(SourceQuadTree tree,Func<SourceMapData,SourceMapData> callback)
    {
        var method=typeof(SourceQuadTree).GetMethod("TransformContent",Private);Assert.NotNull(method);
        method!.Invoke(tree,new object[]{callback});
    }
    private static void SubdivideRoot(SourceQuadTree tree)
    {tree.Root.Side=20;tree.Root.Depth=1;tree.Root.Bounds=new(0,0,20);tree.Root.Subdivide(tree.Processor);}
    // reach: unit (supplies payload inputs; actual constructor, virtual Clone and Equals).
    // Old value: no ObservableMapData class/owned vertex buffer; production cube insertion stops before it.
    [Fact]
    public void PayloadCloneOwnsVerticesAndEqualityUsesOnlyTypeAndId()
    {
        var vertices=new[]{new P2f(1,2),new P2f(3,4)};
        var data=Data(0xFEDCBA98,77,vertices);var clone=data.Clone();
        vertices[0]=new(90,91);Assert.Equal(new P2f(1,2),Field<P2f[]>(data,"Polygon")[0]);
        Assert.NotSame(Field<P2f[]>(data,"Polygon"),Field<P2f[]>(clone,"Polygon"));
        Field<P2f[]>(data,"Polygon")[0]=new(20,21);Assert.Equal(new P2f(1,2),Field<P2f[]>(clone,"Polygon")[0]);
        Assert.Equal(0xFEDCBA98u,Field<uint>(clone,"ObjectId"));Assert.Equal((77u,77u),(clone.First,clone.Last));
        Assert.True(clone.Same(Data(0xFEDCBA98,999,new P2f(100,200))));
        Assert.False(clone.Same(Data(7,77,new P2f(1,2),new P2f(3,4))));
        Assert.False(clone.Same(null));Assert.False(clone.Same(new SourceMapData(MemoryMapContentType.ObstacleCharger,77,77)));
    }
    // reach: unit (supplies a located cube pose through actual BlockWorld/map add; real face behavior API/ray).
    // Old value: no native type3 cube cells; CanDriveIdealDistanceForward returns true.
    [Fact]
    public async Task LocatedCubeBlocksTheFortyMillimeterFaceDrive()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var world=rig.Robot.LocalizationWorld;var map=Assert.IsType<MemoryMap>(world.Maps.Current);
        using var root=rig.Engine.Robot!.MainPose.FindRoot();
        using var pose=new Pose3d(Rotation3d.Identity,new Vec3(30,0,22),parent:root);
        var cube=new ObservableObject(77,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>());
        using var manipulation=new ManipulationSystem(rig.Robot,rig.Robot.Vision);
        using var scope=new BehaviorScope();var behavior=new InteractWithFacesBehavior(rig.Robot.Vision,m:manipulation);
        try
        {
            cube.NativePose.Dispose();cube.InitPose(pose,PoseState.Known);world.AddLocatedObject(cube);
            await behavior.StartAsync(new BehaviorContext{Robot=rig.Robot,Triggers=new AnimationTriggerMap(),Map=map},scope,default);
            Assert.False(behavior.CanDriveIdealDistanceForward());
            Assert.True(map.HasCollisionRayWithTypes(new(0,0),new(40,0),t=>t==MemoryMapContentType.ObstacleObservable));
            Assert.Empty(map.Regions);
        }
        finally{behavior.Stop(BehaviorStopReason.Completed);cube.NativePose.Dispose();}
    }
    // reach: unit (supplies valid derived cells; actual whole-map removal, native overlap and ForceSet).
    // Old value: no derived payload and RemoveObject reports MISSING; a matching type3 cell is never replaced.
    [Fact]
    public void RemovingOneCubePreservesTheOtherCubeAndBaseCharger()
    {
        var map=new MemoryMap();var root=map.Tree.Root;SubdivideRoot(map.Tree);
        root.Children[0]!.ForceSet(new(Data(77,1,new P2f(5,5))),map.Tree.Processor);
        root.Children[1]!.ForceSet(new(Data(78,2,new P2f(5,-5))),map.Tree.Processor);
        root.Children[2]!.ForceSet(new(new SourceMapData(MemoryMapContentType.ObstacleCharger,3,4)),map.Tree.Processor);
        map.Tree.Dirty=false;
        map.RemoveObject(new(77,MemoryMapContentType.ClearOfObstacle,123));
        Assert.Equal(MemoryMapContentType.ClearOfObstacle,root.Children[0]!.Content.Data.Type);
        Assert.IsType<SourceMapData>(root.Children[0]!.Content.Data);
        Assert.Equal((123u,123u),(root.Children[0]!.Content.Data.First,root.Children[0]!.Content.Data.Last));
        Assert.Equal(78u,Field<uint>(root.Children[1]!.Content.Data,"ObjectId"));
        Assert.Equal(MemoryMapContentType.ObstacleCharger,root.Children[2]!.Content.Data.Type);
        Assert.Equal((3u,4u),(root.Children[2]!.Content.Data.First,root.Children[2]!.Content.Data.Last));
        Assert.False(map.Tree.Dirty);
    }
    // reach: unit (valid tree and callback; actual root-quad wrapper and postorder node Transform).
    // Old value: no Transform entry/callback calls (the removal wrapper only emits MISSING).
    [Fact]
    public void TransformVisitsChildrenBeforeOwnNodeAndDropsInternalReplacement()
    {
        var tree=new SourceQuadTree();SubdivideRoot(tree);
        var types=new[]{MemoryMapContentType.Cliff,MemoryMapContentType.InterestingEdge,MemoryMapContentType.ObstacleCharger,MemoryMapContentType.ClearOfCliff};
        for(int i=0;i<4;i++)tree.Root.Children[i]!.ForceSet(new(new SourceMapData(types[i],1,1)),tree.Processor);
        var order=new List<MemoryMapContentType>();
        Transform(tree,data=>
        {
            order.Add(data.Type);
            return data.Type is MemoryMapContentType.Cliff or MemoryMapContentType.Unknown
                ?new SourceMapData(MemoryMapContentType.ClearOfObstacle,9,9):data;
        });
        Assert.Equal(types.Append(MemoryMapContentType.Unknown),order);
        Assert.Equal(MemoryMapContentType.ClearOfObstacle,tree.Root.Children[0]!.Content.Data.Type); // no cliff override-table gate
        Assert.Equal(MemoryMapContentType.Unknown,tree.Root.Content.Data.Type);Assert.False(tree.Dirty);
        var method=typeof(SourceQuadNode).GetMethod("TransformContent",Private);Assert.NotNull(method);
        var polygon=new SourceFastPolygon(new[]{new Vec2(10,10),new Vec2(-10,10),new Vec2(-10,-10),new Vec2(10,-10)});
        Func<SourceMapData,SourceMapData> change=data=>data.Type==MemoryMapContentType.InterestingEdge
            ?new SourceMapData(MemoryMapContentType.Cliff,10,10):data;
        Assert.False((bool)method!.Invoke(tree.Root,new object[]{tree,polygon,change})!);
        Assert.Equal(MemoryMapContentType.Cliff,tree.Root.Children[1]!.Content.Data.Type); // descendant result isn't ORed into root return

    }
    // reach: unit (supplies equal child data; actual child-first merge and newly-leaf callback replacement).
    // Old value: 0 Transform callbacks and root remains internal; native result is 5 callbacks and a leaf.
    [Fact]
    public void TransformMergesBeforeCallingTheNewLeafAndKeepsSharedReplacement()
    {
        var tree=new SourceQuadTree();SubdivideRoot(tree);
        foreach(var child in tree.Root.Children)child!.ForceSet(new(new SourceMapData(MemoryMapContentType.ClearOfCliff,1,1)),tree.Processor);
        int calls=0;var replacement=new SourceMapData(MemoryMapContentType.ClearOfObstacle,99,99);
        Transform(tree,data=>++calls==5?replacement:data);
        Assert.Equal(5,calls);Assert.Empty(tree.Root.Children);Assert.Same(replacement,tree.Root.Content.Data);Assert.False(tree.Dirty);
    }
    // reach: unit (real production contact-created type4 cells; supplies removal input and identity callback).
    // Old value: no whole-map Transform/callback; RemoveObject only reports MISSING.
    [Fact]
    public void ContactChargerRemovalTraversesTheNativeMapAndPreservesItsBaseData()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State(Cozmo.Protocol.RobotStatusFlag.IsBodyAccMode|Cozmo.Protocol.RobotStatusFlag.IsOnCharger);
        var world=rig.Robot.LocalizationWorld;var charger=Assert.Single(world.LocatedObjects.Where(o=>o.Type==ObjectType.Charger_Basic));
        var map=Assert.IsType<MemoryMap>(world.Maps.Current);
        Assert.True(map.HasCollisionRayWithTypes(new(-25,0),new(0,0),t=>t==MemoryMapContentType.ObstacleCharger));
        // Existing API removal path: concrete callback excludes base4 before touching the derived ID.
        map.RemoveObject(new(charger.ObjectId,MemoryMapContentType.ObstacleChargerRemoved,123));
        Assert.True(map.HasCollisionRayWithTypes(new(-25,0),new(0,0),t=>t==MemoryMapContentType.ObstacleCharger));
        // Real traversal, with an observational identity callback, proves this isn't the old empty wrapper.
        int calls=0;Transform(map.Tree,data=>{calls++;return data;});Assert.True(calls>0);
    }
}
