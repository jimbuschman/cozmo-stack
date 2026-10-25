using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M11RootGrowthTests
{
    // reach: unit (explicit native node content and directions, real helpers/callbacks).
    // Old: UpgradeRootLevel=false, side160/level4 unchanged; no child or area callback.
    [Theory]
    [InlineData(1,1,3,80,80)] [InlineData(1,-1,2,80,-80)]
    [InlineData(-1,1,1,-80,80)] [InlineData(-1,-1,0,-80,-80)]
    [InlineData(float.NaN,float.NaN,0,-80,-80)]
    public void GrowthMovesOldContentToTheOppositeQuadrant(float dx,float dy,int kept,float x,float y)
    {
        var tree=new SourceQuadTree();var root=tree.Root;
        root.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.InterestingEdge,7,11)),tree.Processor);
        var old=root.Content;double area=tree.Processor.ExploredArea,edge=tree.Processor.EdgeArea;
        int generation=tree.Processor.BorderGeneration;
        Assert.True(tree.UpgradeRootLevel(new(dx,dy),8));
        Assert.Same(root,tree.Root);Assert.Equal((x,y,320f,(byte)5),(root.X,root.Y,root.Side,root.Depth));
        Assert.Same(old,root.Children[kept]!.Content);
        Assert.Equal((x-160,y-160,x+160,y+160),(root.Bounds.MinX,root.Bounds.MinY,root.Bounds.MaxX,root.Bounds.MaxY));
        Assert.Equal(area+area,tree.Processor.ExploredArea); // q arrival adds; root's retained credit is not removed
        Assert.Equal(edge,tree.Processor.EdgeArea);Assert.Equal(generation+2,tree.Processor.BorderGeneration);
        for(int i=0;i<4;i++)
        {
            var child=root.Children[i]!;Assert.Same(root,child.Parent);Assert.Equal((160f,(byte)4),(child.Side,child.Depth));
            if(i!=kept)Assert.Equal((MemoryMapContentType.Unknown,11u,11u),(child.Content.Data.Type,child.Content.Data.First,child.Content.Data.Last));
        }
        Assert.Same(root.Children[kept],Assert.Single(tree.Processor.TypeSets[MemoryMapContentType.InterestingEdge]));
    }

    [Fact]
    public void GrowthUsesNegativeEpsilonAndReparentsExistingChildren()
    {
        // reach: unit (supplied nodes/directions); old: false/no recipient; checked Q2 >=-epsilon boundary.
        float boundary=BitConverter.UInt32BitsToSingle(0xB727C5AC);
        var a=new SourceQuadTree();a.Root.Subdivide(a.Processor);var children=a.Root.Children.ToArray();
        Assert.True(a.UpgradeRootLevel(new(boundary,boundary),8));
        var q=a.Root.Children[3]!;Assert.Equal((80f,80f),(a.Root.X,a.Root.Y));
        Assert.Equal(children,q.Children);Assert.All(children,c=>Assert.Same(q,c!.Parent));
        var b=new SourceQuadTree();Assert.True(b.UpgradeRootLevel(new(float.BitDecrement(boundary),float.BitDecrement(boundary)),8));
        Assert.Equal((-80f,-80f),(b.Root.X,b.Root.Y));
    }

    [Fact]
    public void GrowthStopsAtLevelEightAndNeverWraps255()
    {
        // reach: unit; old: first growth=false, limit never reached. Refusal checks are preservation controls.
        var tree=new SourceQuadTree();for(int i=0;i<4;i++)Assert.True(tree.UpgradeRootLevel(new(1,1),8));
        Assert.Equal((2560f,(byte)8),(tree.Root.Side,tree.Root.Depth));
        var snapshot=(tree.Root.X,tree.Root.Y,tree.Root.Side,tree.Root.Content);
        Assert.False(tree.UpgradeRootLevel(new(-1,-1),8));Assert.Equal(snapshot,(tree.Root.X,tree.Root.Y,tree.Root.Side,tree.Root.Content));
        tree.Root.Depth=255;Assert.False(tree.UpgradeRootLevel(new(1,1),255));Assert.Equal((byte)255,tree.Root.Depth);
    }

    // reach: unit (explicit coherent level8 node tree; actual Insert/Expand/Shift/Swap/Destroy and native overlap).
    // Old: initial centre(0,0) remains, all old children remain; shift MISSING instead of moving them.
    // Expected new->old indices are the checked Q3b eight-case table, not generated from the implementation.
    [Theory]
    [InlineData(1,1,-1,-1,-1,0)] [InlineData(1,-1,-1,-1,1,-1)]
    [InlineData(-1,1,-1,2,-1,-1)] [InlineData(-1,-1,3,-1,-1,-1)]
    [InlineData(1,0,-1,-1,0,1)] [InlineData(-1,0,2,3,-1,-1)]
    [InlineData(0,1,-1,0,-1,2)] [InlineData(0,-1,1,-1,3,-1)]
    public void InsertUsesEveryCheckedShiftCase(int dx,int dy,int q0,int q1,int q2,int q3)
    {
        var tree=AtLimit();tree.Root.Subdivide(tree.Processor);
        var old=tree.Root.Children.ToArray();
        for(int i=0;i<4;i++)
        {
            old[i]!.ForceSet(new SourceNodeContent(new ObservableMapData((uint)(i+10),Array.Empty<P2f>(),17)),tree.Processor);
            old[i]!.Subdivide(tree.Processor);
        }
        var descendants=old.Select(n=>n!.Children.ToArray()).ToArray();
        var log=new List<string>();tree.SourceLog=(_,_,key,_)=>log.Add(key);
        tree.Insert(new[]{new Vec2(dx*1920,dy*1920)},new CliffMapData(new(1,0),25),1);
        Assert.Equal((dx*1280f,dy*1280f,2560f,(byte)8),(tree.Root.X,tree.Root.Y,tree.Root.Side,tree.Root.Depth));
        int[] expected={q0,q1,q2,q3};
        for(int i=0;i<4;i++)if(expected[i]>=0)
        {
            var moved=tree.Root.Children[i]!;
            Assert.Equal(descendants[expected[i]],moved.Children);
            Assert.All(moved.Children,c=>Assert.Same(moved,c!.Parent));
            Assert.All(moved.Children,c=>Assert.Equal((uint)(expected[i]+10),Assert.IsType<ObservableMapData>(c!.Content.Data).ObjectId));
            Assert.Equal((old[expected[i]]!.X,old[expected[i]]!.Y),(moved.X,moved.Y));
            Assert.Same(tree.Root,moved.Parent);
        }
        for(int j=0;j<4;j++)if(!expected.Contains(j))
        {
            Assert.Equal(4,old[j]!.Children.Count);Assert.All(old[j]!.Children,Assert.Null); // DestroyNodes, not ClearDescendants
            Assert.All(descendants[j],n=>Assert.DoesNotContain(n,tree.Processor.TypeSets[MemoryMapContentType.ObstacleObservable]));
        }
        Assert.All(tree.Processor.TypeSets[MemoryMapContentType.ObstacleObservable],n=>Assert.DoesNotContain(n,old));
        Assert.Equal(new[]{"QuadTree.ShiftRoot"},log);
        Assert.True(tree.HasCollisionRayWithTypes(new(dx*1920,dy*1920),new(dx*1920,dy*1920),1u<<8));
    }

    [Fact]
    public void LeafShiftRetainsItsContentAndCredit()
    {
        // reach: unit (supplied max-level leaf; real Expand). Old: centre0; new: +1280, content unchanged.
        var tree=AtLimit();tree.Root.ForceSet(new SourceNodeContent(new CliffMapData(new(1,0),42)),tree.Processor);
        var content=tree.Root.Content;double area=tree.Processor.ExploredArea;
        tree.Expand(new[]{new Vec2(1920,0)},1);
        Assert.Equal((1280f,0f),(tree.Root.X,tree.Root.Y));Assert.Empty(tree.Root.Children);
        Assert.Same(content,tree.Root.Content);Assert.Equal(area,tree.Processor.ExploredArea);
    }

    [Fact]
    public void FailedShiftsConsumeBudgetBeforeOneInsert()
    {
        // reach: unit (substitutes only overlap=2 to count the sole recursive insert; actual expansion/refusal).
        // Old: no CantShiftPMx entries; two MISSINGs. Native 0x00685322 ignores false and tests Contains again.
        var tree=AtLimit();var order=new List<string>();int inserts=0;
        tree.SourceLog=(_,_,key,_)=>order.Add(key);
        tree.OverlapForTest=(_,_)=>{inserts++;order.Add("insert");return 2;};
        tree.Insert(new[]{new Vec2(-4000,0),new Vec2(4000,0)},new CliffMapData(new(1,0),1),2);
        Assert.Equal(new[]{"QuadTreeNode.ShiftRoot.CantShiftPMx","QuadTreeNode.ShiftRoot.CantShiftPMx","QuadTree.Expand.InsufficientExpansion","insert"},order);
        Assert.Equal(1,inserts);Assert.True(tree.Dirty);
    }

    [Fact]
    public void RobotFootprintAtTwentyCentimetresReachesTheTree()
    {
        // reach: production (received state -> Robot::Update -> current map/tree/ray). Old ray=false, dropped outside160mm root.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();rig.State(x:200);
        var map=Assert.IsType<MemoryMap>(rig.Robot.LocalizationWorld.Maps.Current);
        Assert.True(map.HasCollisionRayWithTypes(new(190,0),new(200,0),t=>t==MemoryMapContentType.ClearOfObstacle));
        Assert.True(map.Tree.Root.Side>160);Assert.Empty(map.Regions);
    }

    private static SourceQuadTree AtLimit()
    {
        var tree=new SourceQuadTree();tree.Root.Side=2560;tree.Root.Depth=8;tree.Root.Bounds=new(0,0,2560);return tree;
    }
}
