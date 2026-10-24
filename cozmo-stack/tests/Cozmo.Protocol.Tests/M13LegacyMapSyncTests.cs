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
    public void OnlyTheRemainingUnportedLegacyWritersCanBlockTheFallback()
    {
        // reach: unit (explicit legacy inputs and real native ray; prox/unrecognized/quad joins remain MISSING).
        // Old: a legacy type-3 region answered true despite its native owner; new: false. Types 6/7/10 retain true.
        foreach(var type in new[]{MemoryMapContentType.ObstacleObservable,MemoryMapContentType.ObstacleCharger,
                    MemoryMapContentType.Cliff,MemoryMapContentType.InterestingEdge,MemoryMapContentType.ObstacleProx,
                    MemoryMapContentType.ObstacleUnrecognized,MemoryMapContentType.NotInterestingEdge})
        {
            var map=new MemoryMap();var quad=new[]{new Vec2(30,-1),new Vec2(30,1),new Vec2(40,1),new Vec2(40,-1)};
            if(type==MemoryMapContentType.NotInterestingEdge)map.FlagQuadAsNotInterestingEdges(quad);
            else map.Insert(quad,type);
            bool retained=type is MemoryMapContentType.ObstacleProx or MemoryMapContentType.ObstacleUnrecognized or MemoryMapContentType.NotInterestingEdge;
            Assert.Equal(retained,map.HasCollisionRayWithTypes(new(29,0),new(41,0)));
            Assert.False(map.HasCollisionRayWithTypes(new(29,0),new(41,0),_=>false));
        }
    }
}
