using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
// Polygon fixture calls explicitly supply raw carried ID0 for zero-shift test geometry (substituted tool input).
public sealed class M13NativeMapImportTests
{
    private static string Obb()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {
            var path=Path.Combine(d.FullName,"re-analysis/obb");
            if(MotionPrimitiveSet.FromObb(path) is not null)return path;
        }
        throw new InvalidOperationException("Shipped primitives required");
    }
    private static ObservableMapData Payload(uint id=7)=>new(id,
        new P2f[]{new(300,300),new(320,300),new(320,320),new(300,320)},1);
    private static void Set(SourceQuadNode node,SourceMapData data)=>node.Content=SourceNodeContent.FromShared(data);
    private static SourceQuadTree Tree()
    {
        var tree=new SourceQuadTree(0);
        tree.Root.Side=20;tree.Root.Bounds=new(0,0,20);tree.Root.Depth=1;
        tree.Root.Subdivide(tree.Processor);
        return tree;
    }

    [Fact]
    public void QueryPreservesOutputAndDeduplicatesReferencesInPhysicalPostorder()
    {
        // reach: unit (substituted tree topology/payloads; not camera-observation production coverage).
        // OV42 Q4-Q6 / 0x00688520..0x0068858A: children, own data, identity set; output never cleared.
        var tree=Tree();var shared=Payload();var equalId=Payload();var parent=Payload(9);var prior=Payload(10);
        Set(tree.Root.Children[0]!,shared);Set(tree.Root.Children[1]!,equalId);
        Set(tree.Root.Children[2]!,shared);Set(tree.Root.Children[3]!,prior);Set(tree.Root,parent);
        var output=new List<SourceMapData>{prior};
        tree.FindContentIf(d=>d.Type==MemoryMapContentType.ObstacleObservable?1:0,output);
        Assert.Equal(4,output.Count);
        Assert.Same(prior,output[0]);Assert.Same(shared,output[1]);Assert.Same(equalId,output[2]);Assert.Same(parent,output[3]);
    }

    [Fact]
    public void QueryUsesRootGeometryAndRejectsNonoverlapBeforePredicate()
    {
        // reach: unit (substitutes child position outside actual root; exercises real whole-root geometry).
        // 0x0068A61E..46 constructs MakeQuadXY(root,0)/ImportQuad2d/FastPolygon; 0x0068851A rejects overlap0.
        var tree=Tree();var outside=tree.Root.Children[0]!;
        outside.X=100;outside.Y=100;outside.Bounds=new(100,100,10);
        var excluded=Payload();Set(outside,excluded);var included=Payload(8);Set(tree.Root.Children[1]!,included);
        var visited=new List<SourceMapData>();var output=new List<SourceMapData>();
        tree.FindContentIf(d=>{visited.Add(d);return d.Type==MemoryMapContentType.ObstacleObservable?1:0;},output);
        Assert.DoesNotContain(excluded,visited);Assert.Same(included,Assert.Single(output));
    }

    [Theory] [InlineData(0)] [InlineData(2)] [InlineData(-1)] [InlineData(1)]
    public void QueryRequiresExactlyOneAndReloadsDataAfterCallback(int result)
    {
        // reach: unit (substituted root payload and mutation callback; native callback return/reload semantics).
        // 0x00688584 cmp1 then 0x0068858A ldrd own shared pointer again.
        var tree=new SourceQuadTree(0);var before=Payload();var after=Payload(8);Set(tree.Root,before);
        var output=new List<SourceMapData>();
        tree.FindContentIf(d=>{Assert.Same(before,d);Set(tree.Root,after);return result;},output);
        if(result==1)Assert.Same(after,Assert.Single(output));else Assert.Empty(output);
    }

    [Fact]
    public void ImportUsesStoredTypeThreePolygonWithoutWorldOrIdFiltersAndOwnsSnapshot()
    {
        // reach: unit (substitutes admitted native payload; not camera-observation production coverage).
        // 0x00502424 selects type3 only; 0x004FDF64 reads stored data+0x18, without ObjectId filtering.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var world=rig.Robot.Vision.World;
        var payload=Payload();Set(world.Maps.Current!.Tree.Root,payload);
        Assert.Empty(world.LocatedObjects);
        var env=new LatticeEnvironment(MotionPrimitiveSet.FromObb(Obb())!);
        env.ImportBlockWorldObstacles(world,7,false,new uint[]{7});
        Assert.Equal(1,env.ObstacleCount);
        Assert.True(env.Bucket(0)[0].Poly.Contains(310,310));
        Assert.False(env.Bucket(0)[0].Poly.Contains(0,0));
        var snapshot=env.Bucket(0)[0].Poly.Points.ToArray();
        payload.Polygon[0]=new(-500,-500);Set(world.Maps.Current.Tree.Root,new SourceMapData(MemoryMapContentType.Unknown,0,0));
        Assert.Equal(snapshot,env.Bucket(0)[0].Poly.Points);
    }

    [Theory] [InlineData(22f,1)] [InlineData(200f,0)]
    public void LocatedCubeRequiresRealMapHeightAdmission(float height,int expected)
    {
        // reach: unit (substitutes located cube pose; actual AddLocatedObject/native height admission/import;
        // not camera-observation production coverage). 0x0067EDA2..DC0 gates IsPoseTooHigh(1,15,0).
        // Old world iteration imports the high cube despite no admitted type3 content.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var world=rig.Robot.Vision.World;
        using var pose=new Pose3d(Rotation3d.Identity,new Vec3(200,0,height),
            parent:rig.Engine.Robot!.GetNativeOrigin(rig.Engine.Robot.CurrentOriginId));
        var cube=new ObservableObject(77,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>());
        try
        {
            cube.InitPose(pose,PoseState.Known);world.AddLocatedObject(cube);
            Assert.Contains(cube,world.LocatedObjects);
            var env=new LatticeEnvironment(MotionPrimitiveSet.FromObb(Obb())!);
            env.ImportBlockWorldObstacles(world,null,false);
            Assert.Equal(expected,env.ObstacleCount);
        }
        finally{CubePreActionPoses.ClearForSetPose(cube);cube.NativePose.Dispose();}
    }

    [Theory] [InlineData(MemoryMapContentType.ObstacleCharger)] [InlineData(MemoryMapContentType.ObstacleProx)]
    [InlineData(MemoryMapContentType.ObstacleUnrecognized)] [InlineData(MemoryMapContentType.Cliff)]
    public void ImportExcludesOtherBaseContent(MemoryMapContentType type)
    {
        // reach: unit (substituted native base content; exact type3 consumer predicate 0x00502424).
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var world=rig.Robot.Vision.World;
        Set(world.Maps.Current!.Tree.Root,new SourceMapData(type,0,1));
        var env=new LatticeEnvironment(MotionPrimitiveSet.FromObb(Obb())!);
        env.ImportBlockWorldObstacles(world,null,false);
        Assert.Equal(0,env.ObstacleCount);
    }

    [Fact]
    public void ForgedTypeThreeSkipsOnlyItsPolygonAndReportsPolicy()
    {
        // reach: unit (forged invalid base3 layout; M1-034 policy, never a production producer).
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var world=rig.Robot.Vision.World;
        var tree=world.Maps.Current!.Tree;tree.Root.Subdivide(tree.Processor);
        Set(tree.Root.Children[0]!,Payload());Set(tree.Root.Children[1]!,new SourceMapData(MemoryMapContentType.ObstacleObservable,0,0));
        var diagnostics=new List<string>();void Observe(string text)=>diagnostics.Add(text);
        StackDiagnostics.Reported+=Observe;
        try
        {
            var env=new LatticeEnvironment(MotionPrimitiveSet.FromObb(Obb())!);
            env.ImportBlockWorldObstacles(world,null,false);Assert.Equal(1,env.ObstacleCount);
            Assert.Contains(diagnostics,d=>d.Contains("M1-034 M13-003/M11-074")&&d.Contains("0x004FDF64"));
        }
        finally{StackDiagnostics.Reported-=Observe;}
    }

    [Fact]
    public void MissingCurrentMapRetainsSnapshotAndPlannerCache()
    {
        // reach: unit (missing-current-origin fixture; 0x004FD50C original null dereference, M1-034 skip).
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var world=rig.Robot.Vision.World;
        var env=new LatticeEnvironment(MotionPrimitiveSet.FromObb(Obb())!);var planner=new LatticePlanner(env);
        world.Maps.Current!.Tree.Root.Content.Data.Last=123;Assert.True(planner.ImportBlockWorldObstacles(world,null,false));
        env.ImportObstaclePolygons(new[]{Payload().Polygon},false,0);
        uint origin=world.Maps.CurrentOrigin;world.Maps.CurrentOrigin=uint.MaxValue;
        try
        {
            Assert.False(planner.ImportBlockWorldObstacles(world,null,false));Assert.Equal(1,env.ObstacleCount);
            env.ImportBlockWorldObstacles(world,null,true);Assert.Equal(1,env.ObstacleCount);Assert.False(env.TightPadding);
        }
        finally{world.Maps.CurrentOrigin=origin;}
        // Unchanged timestamp must still compare against123 (the absent-map branch did not alter cache).
        Assert.False(planner.ImportBlockWorldObstacles(world,null,true));Assert.Equal(1,env.ObstacleCount);
    }

    [Fact]
    public void ContactCreatedChargerIsExcludedThroughRealPathComponentDispatch()
    {
        // reach: production (path dispatch with synthetic prior planner snapshot; admitted RobotStates create charger;
        // DriveToPoseAction.Init -> actual PathComponent
        // selection/StartPlanner -> native lattice runtime -> import, no substituted import/helper).
        // 0x00502424 compares3; charger map producer writes4. Old located-object loop imports one charger.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger,origin:rig.Engine.Robot!.CurrentOriginId);
        var world=rig.Robot.Vision.World;
        Assert.Single(world.LocatedObjects.Where(o=>o.Type==ObjectType.Charger_Basic));
        Assert.True(world.Maps.Current!.NativeContentCounts.TryGetValue(MemoryMapContentType.ObstacleCharger,out var cells)&&cells>0);
        // The async planner retains these goal wrappers. Keep the goal alive until m.Dispose joins its worker,
        // including assertion-failure cleanup: reverse disposal is drive -> m/worker join -> goal -> rig.
        using var goal=new Pose3d(Mat3.Identity,new Vec3(200,0,0));
        using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);Assert.True(m.LoadPlanner(Obb()));
        // A stale prior snapshot proves the dispatch really rebuilds rather than observing initial empty buckets.
        m.Planner!.Env.ImportObstaclePolygons(new[]{Payload().Polygon},false,0);Assert.Equal(1,m.Planner.Env.ObstacleCount);
        using var drive=new DriveToPoseAction(m){Goal=goal};
        Assert.Equal(0,drive.Init());
        Assert.Same(m.NativePlanners!.Lattice,m.NativePlanners.Current);
        Assert.Equal(1,rig.Robot.Motion.Path.DriveToPoseStatus);
        Assert.Equal(0,m.Planner.Env.ObstacleCount);
    }
}
