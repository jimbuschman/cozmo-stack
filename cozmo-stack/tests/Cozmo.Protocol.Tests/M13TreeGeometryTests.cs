using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M13TreeGeometryTests
{
    private static Vec2[] Box(float minX, float minY, float maxX, float maxY) =>
        [new(minX,minY),new(minX,maxY),new(maxX,maxY),new(maxX,minY)];

    // An independent integration assertion for geometry controls whose native unit had no old executable owner.
    // 0x00686D3A..DC0 stores cells touched by the polygon; a ray at its first vertex touches one of those cells.
    // Old production InsertObject stops at the geometry MISSING, so the ray is false (no legacy regions added).
    private static void AssertInsertionReachesRay(Vec2[] vertices)
    {
        var map = new MemoryMap();
        map.InsertObject(vertices,new SourceMapData(MemoryMapContentType.ObstacleCharger,1,1));
        Assert.True(map.HasCollisionRayWithTypes(vertices[0],vertices[0]));
    }

    // reach: unit (hand-built node/polygon, then actual map insert/ray; no overlap substitution).
    // Old code: no overlap implementation and no stored cells; AssertInsertionReachesRay yields false.
    // Direct four-code assertions are new native-unit controls, not an alleged defect in the planner's polygon.
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void OverlapPreservesTheFourCodesAndTestsPolygonEdgesAgainstNodeDiagonals(int scenario)
    {
        Vec2[] vertices = scenario switch
        {
            0 => Box(2,2,3,3),                    // disjoint: 0
            1 => [new(0,0),new(2,0)],             // mixed vertices: 1
            2 => Box(-3,-3,3,3),                  // no vertices/edge hits, minimum corner contained: 2
            3 => Box(-.5f,-.5f,.5f,.5f),          // every vertex in node: 3
            _ => [new(-2,0),new(2,0)],             // no inside vertex; edge crosses both diagonals: 1
        };
        var node = new SourceQuadNode(0,0,0,2,0);
        var polygon = new SourceFastPolygon(vertices);
        Assert.Equal(scenario == 4 ? 1 : scenario,polygon.GetOverlapType(node));
        AssertInsertionReachesRay(vertices);
    }

    // reach: unit (non-clockwise input deliberately preserves supplied order; real insert/ray join).
    // Old code: insertion-to-ray false. Contains(center)=true is an existing circle-shortcut control;
    // using that center in overlap would incorrectly yield 2 instead of the native minimum-corner result 0.
    [Fact]
    public void FinalOverlapUsesMinimumCornerRatherThanCenter()
    {
        Vec2[] vertices = [new(-10,-1.25f),new(10,-1.25f),new(10,1.25f),new(-10,1.25f)];
        var polygon = new SourceFastPolygon(vertices);
        Assert.True(polygon.Contains(0,0));
        Assert.False(polygon.Contains(-1,-1));
        Assert.Equal(0,polygon.GetOverlapType(new SourceQuadNode(0,0,0,2,0)));
        AssertInsertionReachesRay(vertices);
    }

    // reach: unit (hand-built clockwise polygon and points; real native insert/ray join).
    // Old code: map insertion-to-ray false. Planner FastPolygonF already returns these Contains values;
    // those checks preserve its supported arithmetic while the final assertion rejects the old unbuilt map join.
    [Theory]
    [InlineData(2f,0f,true)]   // exact boundary: inner equality does not short-circuit; half-plane equality passes
    [InlineData(1.5f,1.5f,true)] // r2=4.5, between inner=4 and outer=8: half-planes must run
    [InlineData(2.01f,0f,false)]
    public void ContainsKeepsStrictCirclesAndInclusiveBoundary(float x,float y,bool expected)
    {
        var vertices = Box(-2,-2,2,2);
        var polygon = new SourceFastPolygon(vertices);
        Assert.Equal(4f,polygon.InnerSquared); Assert.Equal(8f,polygon.OuterSquared);
        Assert.Equal(expected,polygon.Contains(x,y));
        Assert.True(polygon.Contains(float.NaN,0)); // GT/MI and LE preserve unordered control flow
        AssertInsertionReachesRay(vertices);
    }

    // reach: unit (two-point and singleton construction with native insert/ray; no production inputs substituted).
    // Old code: map insertion-to-ray false; the planner's existing radius/centroid arithmetic already matches.
    [Fact]
    public void ConstructionPreservesWeightedCentroidFullSegmentRadiusAndDegenerateNormals()
    {
        var singleton = new SourceFastPolygon([new(7,9)]);
        Assert.Empty(singleton.Edges); Assert.Equal(0,singleton.OuterSquared); Assert.Equal(0,singleton.InnerSquared);
        var line = new SourceFastPolygon([new(0,0),new(3,4)]);
        Assert.Equal(25f,line.OuterSquared); Assert.Equal(0f,line.InnerSquared);
        Assert.Equal(1.5f,line.CenterX); Assert.Equal(2f,line.CenterY); Assert.Equal(2,line.Edges.Length);
        var zero = new SourceFastPolygon([new(1,1),new(1,1)]);
        Assert.True(float.IsNaN(zero.Normals[0].X)); Assert.True(float.IsNaN(zero.Normals[1].Y));
        var weighted = new SourceFastPolygon([new(100000000,0),new(1,0),new(-100000000,0)]);
        Assert.Equal(0f,weighted.CenterX); // weighted per-vertex f32: +33333334, +0.33333334 (lost), -33333334
        AssertInsertionReachesRay([new(0,0),new(3,4)]);
    }

    // reach: unit (explicit segment endpoints, then real map insertion/query).
    // Old map code: false on the insertion/ray assertion. Unit controls cover the native strict epsilon and order.
    [Fact]
    public void SegmentOrientationUsesStrictEpsilonAndUnexpandedEndpointBounds()
    {
        var edge = new SourceLineSegment(new(0,0),new(1,0));
        float e = BitConverter.UInt32BitsToSingle(0x3727C5AC);
        Assert.Equal(0,edge.Orientation(new(.5f,MathF.BitDecrement(e))));
        Assert.Equal(1,edge.Orientation(new(.5f,e))); // t=-e takes GE, not collinear
        Assert.Equal(2,edge.Orientation(new(.5f,MathF.BitIncrement(e))));
        Assert.Equal(1,edge.Orientation(new(.5f,-e)));
        Assert.Equal(2,edge.Orientation(new(float.NaN,0)));
        Assert.True(edge.IntersectsWith(new(new(.5f,-1),new(.5f,1))));
        Assert.True(edge.IntersectsWith(new(new(1,0),new(2,0))));
        Assert.False(edge.IntersectsWith(new(new(MathF.BitIncrement(1f),0),new(2,0))));
        AssertInsertionReachesRay([new(0,0),new(1,0)]);
    }

    // reach: unit (known type4 cell supplied through native InsertObject, no geometry hook).
    // Old code: false because no tree cells were stored. Native query returns cell overlap, not original footprint:
    // point (9,9) lies in the touched 10x10 corner cell but outside this tiny footprint at (1..2,1..2).
    [Fact]
    public void RayCrossingOnlyACornerCellStillCollidesAndFoldsAllElevenTypes()
    {
        var map = new MemoryMap();map.InsertObject(Box(1,1,2,2),new SourceMapData(MemoryMapContentType.ObstacleCharger,1,1));
        var seen = new List<int>();
        Assert.True(map.HasCollisionRayWithTypes(new(9,8),new(9,9),type=>{seen.Add((int)type);return type==MemoryMapContentType.ObstacleCharger;}));
        Assert.Equal(Enumerable.Range(0,11),seen);
        Assert.False(map.HasCollisionRayWithTypes(new(11,8),new(11,9)));
        Assert.False(map.HasCollisionRayWithTypes(new(9,8),new(9,9),_=>false));
    }

    // reach: unit (explicitly subdivided Unknown parent; real recursive ray query with bit0).
    // Old code: false on Unknown, since it queries an empty legacy region list. Native selected internal type0
    // returns overlap!=0 before it would visit the children, even though every child here is unselected type1.
    [Fact]
    public void SelectedInternalUnknownNodeAnswersBeforeItsChildren()
    {
        var map = new MemoryMap();map.Tree.Root.Subdivide(map.Tree.Processor);
        foreach (var c in map.Tree.Root.Children)
            c!.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.ClearOfObstacle,1,1)),map.Tree.Processor);
        Assert.True(map.HasCollisionRayWithTypes(new(0,0),new(40,0),type=>type==MemoryMapContentType.Unknown));
    }

    // reach: production (received synchronized contact -> actual charger/world/pose/map -> native insert and
    // behavior's existing CanDriveIdealDistanceForward API; no pose, map, overlap or query result substitution).
    // Old code: true, because contact inserts stopped at the geometry MISSING and the legacy region list is empty.
    [Fact]
    public async Task ContactChargerMakesFortyMillimeterFaceDriveReturnFalse()
    {
        using var rig = new M4ControlTests.Rig();rig.ToSynced();
        var robot = rig.Engine.Robot!;
        rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger,
            origin:robot.CurrentOriginId,frame:robot.PoseFrameId));rig.Tick();
        Assert.Equal(0u,robot.LastFullStateResult);
        Assert.Single(rig.Robot.Vision.World.LocatedObjects.Where(o=>o.Type==ObjectType.Charger_Basic));
        var map = rig.Robot.Vision.World.Maps.Current!;
        Assert.Empty(map.Regions);
        using var manipulation = new ManipulationSystem(rig.Robot,rig.Robot.Vision);
        using var scope = new BehaviorScope();
        var behavior = new InteractWithFacesBehavior(rig.Robot.Vision,m:manipulation);
        try
        {
            await behavior.StartAsync(new BehaviorContext {Robot=rig.Robot,Triggers=new AnimationTriggerMap(),Map=map},scope,default);
            Assert.False(behavior.CanDriveIdealDistanceForward());
        }
        finally {behavior.Stop(BehaviorStopReason.Completed);}
    }
}
