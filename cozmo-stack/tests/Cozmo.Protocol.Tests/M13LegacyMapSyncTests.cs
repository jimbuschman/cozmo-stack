using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M13LegacyMapSyncTests
{
    private static string Obb()=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../re-analysis/obb"));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FreeplayPreservesTheNativeHeightGateInsteadOfMirroringEveryCube(bool carried)
    {
        // reach: unit (actual FreeplayStack.Tick, BlockWorld native add and face API; supplies a located cube/
        // lift attachment rather than a vision/pickup sequence; M11-037 camera-time join remains MISSING).
        // Old: carried cube blocked the face drive (false); both cases gained one legacy type-3 region.
        // Floor is the positive control: it still blocks, exclusively through native cells.
        using var signals=SignalTestContext.Install();using var rig=new Rig();
        if(carried)rig.State(liftAngle:MathF.Asin((LiftPresets.CarryMm-45f)/66f));
        var world=rig.Vision.World;var native=rig.Robot.Engine.Robot!;var map=Assert.IsType<MemoryMap>(world.Maps.Current);
        using var root=native.MainPose.FindRoot();
        var marker=CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1).First(m=>m.Face==BlockFace.Front);
        using var local=LiftGeometry.ObjectOnLift(marker);
        using var pose=carried?new Pose3d(local):new Pose3d(Rotation3d.Identity,new Vec3(30,0,22),parent:root);
        if(carried)pose.SetParent(native.LiftPose);
        var cube=new ObservableObject(77,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>()){NativeCarryingTree=carried};
        try
        {
            cube.InitPose(pose,PoseState.Known);world.AddLocatedObject(cube);
            if(carried)rig.M.Docking.Carrying.SetCarrying(cube.ObjectId,marker);
            using var relative=cube.NativePose.WithRespectTo(native.MainPose);
            Assert.InRange(relative.Translation.Z,carried?79:21,carried?80:23);
            Assert.Equal(!carried,world.Maps.CachedObjectPose(cube.ObjectId,root.Id)!.Value.Added);
            var context=new BehaviorContext{Robot=rig.Robot,Triggers=new AnimationTriggerMap(),Map=map};
            using var stack=FreeplayStack.Create(Obb(),rig.Robot,context,()=>0,rig.Vision,rig.M,withReactions:false);
            using var scope=new BehaviorScope();var face=new InteractWithFacesBehavior(rig.Vision,m:rig.M);
            try
            {
                await face.StartAsync(context,scope,default);
                Assert.Equal(carried,face.CanDriveIdealDistanceForward());
                stack.Tick(0,rig.Clock.NowMs,rig.Robot,rig.Vision,rig.M);
                Assert.Equal(carried,face.CanDriveIdealDistanceForward());
                Assert.Empty(map.Regions);
                Assert.Equal(!carried,map.Tree.HasCollisionRayWithTypes(new(0,0),new(40,0),1u<<3));
            }
            finally{face.Stop(BehaviorStopReason.Completed);}
        }
        finally{cube.NativePose.Dispose();}
    }

    [Fact]
    public void LegacyRegionsOfEveryTypeLeaveTheNativeRayAnswerUnchanged()
    {
        // reach: unit (deliberate legacy-only inputs, not produced in production; real native ray).
        // Old: selected types 6/7/10 turn the clear ray true. Native wrapper M11-046, 0x0068176E..0x006817A4, only queries the tree.
        foreach(var type in Enum.GetValues<MemoryMapContentType>())
        {
            var map=new MemoryMap();var quad=new[]{new Vec2(30,-1),new Vec2(30,1),new Vec2(40,1),new Vec2(40,-1)};
            bool native=map.Tree.HasCollisionRayWithTypes(new(29,0),new(41,0),1u<<(int)type);
            if(type==MemoryMapContentType.NotInterestingEdge)map.FlagQuadAsNotInterestingEdges(quad);
            else map.Insert(quad,type);
            Assert.Equal(native,map.HasCollisionRayWithTypes(new(29,0),new(41,0),t=>t==type)); // selecting Unknown may hit the untouched native root
            Assert.False(map.HasCollisionRayWithTypes(new(29,0),new(41,0),_=>false));
            map.InsertObject(new[]{new Vec2(0,0)},new SourceMapData(type,1,1));
            Assert.Equal(map.Tree.HasCollisionRayWithTypes(new(0,0),new(0,0),1u<<(int)type),
                map.HasCollisionRayWithTypes(new(0,0),new(0,0),t=>t==type));
        }
    }

    [Fact]
    public void MissingWritersRemainVisibleAtTheirExistingEntries()
    {
        // reach: unit (explicit markerless input and protected runnable unit, no sensor/behavior lifecycle join).
        // Preservation control: old code also reports both gaps. Removing the ray fallback must not hide them.
        var logs=new List<string>();var world=new BlockWorld();
        void Log(string text)=>logs.Add(text);
        world.Log+=Log;
        using var pose=new Pose3d();
        ObservableObject? prox=null;
        try
        {
            prox=world.AddMarkerlessObject(pose,ObjectType.ProxObstacle);
            Assert.Contains(logs,s=>s.Contains("M13-030: MemoryMapData_ProxObstacle 0x00622646"));
        }
        finally{world.Log-=Log;prox?.NativePose.Dispose();world.Maps.Dispose();}
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported+=Log;
        try
        {
            var edge=new VisitInterestingEdgeBehavior("edge",new(false,105,110,15,0,10,20,1));
            var method=typeof(VisitInterestingEdgeBehavior).GetMethod("IsRunnableInternal",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
            Assert.False((bool)method.Invoke(edge,new object[]{null!})!); // null-input branch never reads context
            Assert.Contains(logs,s=>s.Contains("M11-089 live native border-vector/map/RNG/robot-root input join"));
        }
        finally{SteppedBehavior.MissingReported-=Log;}
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void RejectedMarkerlessRouteNamesTheUnknownTypeSevenProducer(bool adding)
    {
        // reach: unit (direct family mapper; no confirmed native type-7 producer/event join, UNKNOWN in row 67).
        // Old: Unknown return, but zero ObstacleUnrecognized producer diagnostics. Return remains native Unknown.
        var logs=new List<string>();
        void Log(string message)=>logs.Add(message);
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Log;
        try
        {
            Assert.Equal(MemoryMapContentType.Unknown,MemoryMapTypes.ContentTypeForFamily(ObjectFamily.MarkerlessObject,adding));
            Assert.Contains(logs,s=>s.Contains("ObstacleUnrecognized (type 7) producer UNKNOWN")&&s.Contains("0x0067F542")&&s.Contains("PARTIAL"));
        }
        finally{SteppedBehavior.MissingReported-=Log;}
    }

    [Fact]
    public void ExploredAreaMissingNamesTheExploredGetterRatherThanTheRootTimestamp()
    {
        // reach: unit (direct cached-area API, diagnostic observer; explored-area accumulation remains MISSING).
        // Old: NaN with the misleading label "explored-area helper+0x38". +0x38 is a u32 root-content timestamp.
        // Checked row 70: M11-045 getter 0x006816E8 reads f64 MemoryMap+0x40, vtable virtual +0x1C.
        var logs=new List<string>();var map=new MemoryMap{Missing=logs.Add};
        Assert.True(double.IsNaN(map.ExploredRegionAreaM2));
        Assert.Contains(logs,s=>s.Contains("explored-area virtual +0x1C")&&s.Contains("0x006816E8")&&s.Contains("MemoryMap+0x40"));
        Assert.DoesNotContain(logs,s=>s.Contains("helper+0x38"));
    }

}
