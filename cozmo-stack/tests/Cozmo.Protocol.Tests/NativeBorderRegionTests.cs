using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

// reach: unit. These call GetBorders' output half and the map recipients directly; the production route is
// NativeVisitInterestingEdgeCanonicalTests. Every expected value below is worked by hand from the cited instructions.
public sealed class NativeBorderRegionTests
{
    private static readonly float Half=BitConverter.UInt32BitsToSingle(0x3F000000);
    private static SourceQuadTree SeedTree(out SourceQuadNode seed,MemoryMapContentType type=MemoryMapContentType.InterestingEdge)
    {
        var tree=new SourceQuadTree();tree.Root.Subdivide(tree.Processor);seed=tree.Root.Children[0]!;
        seed.ForceSet(new SourceNodeContent(new SourceMapData(type,5,6)),tree.Processor);return tree;
    }
    // M11-079 rows 14-22, M11-047 rows 11-13. Seed child0 (40,40) side 80 at root Z 1. Contacts: -y child1, -x child2, then the
    // repeated closing -y contact: centres (40,0,1), (0,40,1), (40,0,1). Row 16 skips the angle gate twice; the third reverses the
    // run (dot -1 < 0x3F7C1BDA) and splits at the previous direction 2; the end emits the return edge. Segment normals: candidate
    // (-t.y,t.x,t.z) 0x00689CD8; dot with N0(1)=(0,-1,0) is positive and stays; dot with N0(2)=(-1,0,0) is negative, so XY negate.
    [Fact]
    public void OneSeedLeafGivesOneRegionWithTwoSegmentsAndItsOwnArea()
    {
        var tree=SeedTree(out var seed);
        var regions=tree.CalculateBorders(MemoryMapContentType.InterestingEdge,7)!;
        var region=Assert.Single(regions);
        float scaled=80f*BitConverter.UInt32BitsToSingle(0x3A83126F);Assert.Equal(scaled*scaled,region.Area);
        Assert.Equal(2,region.Segments.Count);
        float unit=40f*(1f/MathF.Sqrt(3200f));
        Assert.Equal(new Point3f(40,0,1),region.Segments[0].A);Assert.Equal(new Point3f(0,40,1),region.Segments[0].B);
        Assert.Equal(new Point3f(-unit,-unit,0),region.Segments[0].Normal);
        Assert.Equal(new Point3f(0,40,1),region.Segments[1].A);Assert.Equal(new Point3f(40,0,1),region.Segments[1].B);
        Assert.Equal(new Point3f(-unit,-unit,0),region.Segments[1].Normal);
        Assert.Same(seed.Content.Data,region.Segments[0].Content); // 0x00689D46..0x00689D70: the actual shared data
    }
    // Mask gate 0x0068AD2C: a seed whose every contact is outside mask 7 has no waypoints, so the map has content and no border.
    [Fact]
    public void ContentWithNoMaskedContactGivesNoRegion()
    {
        var tree=SeedTree(out _);
        foreach(int i in new[]{1,2,3})tree.Root.Children[i]!.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.ObstacleObservable,1,1)),tree.Processor);
        Assert.Empty(tree.CalculateBorders(MemoryMapContentType.InterestingEdge,7)!);
        Assert.Empty(tree.CalculateBorders(MemoryMapContentType.ObstacleProx,7)!); // empty seed set 0x0068AC24
    }
    // Row 21: an end at a single contact still emits a coincident segment. Row 22's short-segment normal: F(F(N0+N1)*.5f), not normalized.
    [Fact]
    public void SingleEndedContactEmitsTheCoincidentSegmentWithTheHalfSumNormal()
    {
        var tree=new SourceQuadTree();tree.Root.Subdivide(tree.Processor);
        var from=tree.Root.Children[0]!;var to=tree.Root.Children[1]!;
        var region=Assert.Single(SourceBorderRegions.Build(new[]{new SourceBorderWaypoint(from,to,1){End=true}}));
        var segment=Assert.Single(region.Segments);Assert.Equal(segment.A,segment.B);
        Assert.Equal(new Point3f(0,-1,0),segment.Normal); // N0 = N1 = direction 1
    }
    // Row 20: the vector's end flushes nothing, so an unended run leaves no region at all.
    [Fact]
    public void WaypointsWithNoEndFlagFlushNothing()
    {
        var tree=new SourceQuadTree();tree.Root.Subdivide(tree.Processor);
        Assert.Empty(SourceBorderRegions.Build(new[]{new SourceBorderWaypoint(tree.Root.Children[0]!,tree.Root.Children[1]!,1)}));
    }
    // Row 18: collinear cells with unequal NodeContent split, and the area set spans both pieces of the one run (row 22).
    [Fact]
    public void UnequalContentSplitsARunThatSharesOneArea()
    {
        var tree=new SourceQuadTree();tree.Root.Subdivide(tree.Processor);
        var a=tree.Root.Children[0]!;var b=tree.Root.Children[2]!;var neighbor=tree.Root.Children[1]!;
        a.ForceSet(new SourceNodeContent(new ObservableMapData(1,Array.Empty<P2f>(),1)),tree.Processor);
        b.ForceSet(new SourceNodeContent(new ObservableMapData(2,Array.Empty<P2f>(),1)),tree.Processor);
        var region=Assert.Single(SourceBorderRegions.Build(new[]{new SourceBorderWaypoint(a,neighbor,1),new SourceBorderWaypoint(b,neighbor,1){End=true}}));
        Assert.Equal(2,region.Segments.Count);
        float scaled=80f*BitConverter.UInt32BitsToSingle(0x3A83126F);float term=scaled*scaled;Assert.Equal(term+term,region.Area);
    }
    // Center 0x00689B0A: FROM level above TO level selects TO; the point is TO's centre minus half TO's side along the normal.
    [Fact]
    public void CentreUsesTheSmallerNodeOfTheContact()
    {
        var large=new SourceQuadNode(0,0,1,80,3);var small=new SourceQuadNode(50,0,1,20,1);
        Assert.Equal(new Point3f(40,0,1),SourceBorderRegions.Center(new SourceBorderWaypoint(large,small,0)));
        Assert.Equal(new Point3f(60,0,1),SourceBorderRegions.Center(new SourceBorderWaypoint(small,large,0))); // tie rule not used: 1 < 3, FROM
        Assert.Equal(Half,.5f);
    }
    // 0x00680B54..0x00680BB0: inside the ROI type 9 becomes fresh base type 0 with both stamps the image stamp; 0x0068C4BE compares types.
    [Fact]
    public void UncertainTransformTurnsOnlyEdgesInsideTheRoiIntoUnknown()
    {
        var map=new MemoryMap();using var robot=new Pose3d();
        map.InsertPreparedInterestingEdges(new[]{(new Point2f(100,-20),new Point2f(100,20)),(new Point2f(-100,-20),new Point2f(-100,20))},17);
        map.InsertObject(new[]{new Vec2(120,-20),new Vec2(120,20)},new SourceMapData(MemoryMapContentType.ClearOfObstacle,17,17));
        Assert.True(map.HasContentType(MemoryMapContentType.InterestingEdge));
        double area=map.InterestingEdgeAreaM2;Assert.True(area>0);
        map.FlagGroundPlaneRoiInterestingEdgesAsUncertain(robot,99);
        Assert.True(map.HasContentType(MemoryMapContentType.InterestingEdge)); // the edge behind the robot stays
        Assert.True(map.InterestingEdgeAreaM2<area);Assert.True(map.InterestingEdgeAreaM2>0);
        Assert.True(map.NativeContentCounts[MemoryMapContentType.ClearOfObstacle]>0);
    }
    // 0x0067E6EA..0x0067E71C through the override table 0x00687E78: incoming 10 replaces only 9, so clear cells stay clear.
    [Fact]
    public void VisitedQuadReplacesEdgesButNotClearGround()
    {
        var map=new MemoryMap();
        map.InsertPreparedInterestingEdges(new[]{(new Point2f(20,-20),new Point2f(20,20))},17);
        map.InsertObject(new[]{new Vec2(60,-20),new Vec2(60,20)},new SourceMapData(MemoryMapContentType.ClearOfObstacle,17,17));
        map.FlagQuadAsNotInterestingEdges(new[]{new Vec2(80,50),new Vec2(0,50),new Vec2(80,-50),new Vec2(0,-50)},30);
        Assert.False(map.HasContentType(MemoryMapContentType.InterestingEdge));
        Assert.True(map.HasContentType(MemoryMapContentType.NotInterestingEdge));
        Assert.True(map.NativeContentCounts[MemoryMapContentType.ClearOfObstacle]>0);
    }
    // 0x0067DCB0..0x00680962: at state time 120001 the 120 s cutoff is 1, so last 1 expires to base type 0 and last 2 survives.
    [Fact]
    public void AgingExpiresEdgesInTheTreeAtTheCutoff()
    {
        var map=new MemoryMap();
        map.InsertPreparedInterestingEdges(new[]{(new Point2f(20,-20),new Point2f(20,20))},2);
        map.InsertPreparedInterestingEdges(new[]{(new Point2f(-20,-20),new Point2f(-20,20))},1);
        map.TransformAging(SourceMapAging.At(120001));
        Assert.True(map.HasContentType(MemoryMapContentType.InterestingEdge));
        map.TransformAging(SourceMapAging.At(120002));
        Assert.False(map.HasContentType(MemoryMapContentType.InterestingEdge));
    }
    // Whiteboard constructor 0x0056A2A4..0x0056A2BA: +0x50 starts at -1.0f, and the map tail replaces it.
    [Fact]
    public void AccurateEdgeDistanceStartsAtMinusOne()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        Assert.Equal(-1f,f.M.Whiteboard.OverheadEdgeDistance);
        f.M.Whiteboard.SetOverheadEdgeState(3f,float.NaN);Assert.True(float.IsNaN(f.M.Whiteboard.OverheadEdgeDistance));
    }
}
