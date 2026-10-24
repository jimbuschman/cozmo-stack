using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M13PreActionWorldTests
{
    private sealed class ChargerFixture : IDisposable
    {
        internal readonly Pose3d Root=new("root");
        internal readonly Pose3d Frame,Input,Robot;
        internal readonly ObservableObject Charger;
        internal readonly List<PreActionPose> Output=new();
        internal ChargerFixture(float tilt=0)
        {
            Frame=new(Rotation3d.FromAngleAxis(tilt,new Vec3(1,0,0)),new Vec3(1000,0,0),"parent",Root);
            Input=new(Rotation3d.Identity,new Vec3(100,0,0),"charger",Frame);
            Robot=new("robot",Root);Charger=new(901,ObjectType.Charger_Basic,Array.Empty<KnownMarker>());
            Charger.InitPose(Input,PoseState.Known);
        }
        internal bool Query(IReadOnlyList<PreActionObstacle>? obstacles=null,IReadOnlyCollection<MarkerType>? codes=null)
        {
            foreach(var p in Output)p.WorldPose.Dispose();Output.Clear();
            return CubePreActionPoses.GetCurrentPreActionPoses(Charger,new[]{PreActionType.Docking},codes??Array.Empty<MarkerType>(),Robot,0,obstacles??Array.Empty<PreActionObstacle>(),Output);
        }
        public void Dispose()
        {
            foreach(var p in Output)p.WorldPose.Dispose();CubePreActionPoses.ClearForSetPose(Charger);
            Charger.ChargerMarker?.PoseOnObject.Dispose();Charger.NativePose.Dispose();
            Robot.Dispose();Input.Dispose();Frame.Dispose();Root.Dispose();
        }
    }
    // reach: unit (owned parented charger, caller clock/obstacle inputs; actual generator/collection/shared pose tree).
    // Old value: result parent is null and root-relative X is -64 instead of 936; world-constructor MISSING emitted.
    [Fact]
    public void ChargerWorldPoseIsAnIndependentSiblingInTheObjectsParentFrame()
    {
        using var f=new ChargerFixture();Assert.True(f.Query());var pose=Assert.Single(f.Output);
        Assert.Same(f.Charger.NativePose.Node!.Parent,pose.WorldPose.Node!.Parent);
        Assert.NotSame(f.Charger.NativePose.Node,pose.WorldPose.Node);
        Assert.Same(f.Charger.ChargerMarker,pose.Marker);Assert.Equal(PreActionType.Docking,pose.Type);
        Assert.Equal(0u,BitConverter.SingleToUInt32Bits((float)pose.DistanceMm));
        Assert.StartsWith("PreActionPose",pose.WorldPose.Name);
        using var root=pose.WorldPose.GetWithRespectToRoot();
        Assert.InRange(root.Translation.X,935.999,936.001);Assert.InRange(root.Translation.Y,-0.001,0.001);Assert.InRange(root.Translation.Z,6.499,6.501);
        Assert.InRange(pose.HeightToleranceMm,36.9f,37.0f); // local |-64| * raw0x3F13CD3A, not root X936
        Assert.False(f.Query());Assert.Single(f.Output); // return means regeneration, not number appended
        Assert.False(f.Query(codes:new[]{MarkerType.LightCubeI_Front}));Assert.Empty(f.Output);
    }
    // reach: unit (direct derived constructor path with exact stored quaternion/translation and owned parents).
    // Old value: quaternion W=-1 from direct copy; matrix/Rodrigues roundtrip canonical identity gives W=+1.
    [Fact]
    public void DerivedConstructorUsesBForTranslationAndRoundTripsTheStoredRotation()
    {
        using var root=new Pose3d("root");using var obj=new Pose3d(Rotation3d.Identity,new Vec3(10,20,0),parent:root);
        using var stored=new Pose3d(new Rotation3d(-1,0,0,0),new Vec3(3,4,0));using var robot=new Pose3d("robot",root);
        using var world=CubePreActionPoses.ComposeWorld(obj,stored,robot,90,3);Assert.NotNull(world);
        Assert.Equal(1d,world!.Quaternion.W);Assert.Equal(0d,world.Quaternion.X);
        // Source: len5, t*0.2f, then *(5+3), then object translation; a90 does not push it out by90.
        Assert.Equal(0x416CCCCDu,BitConverter.SingleToUInt32Bits((float)world.Translation.X));
        Assert.Equal(0x41D33333u,BitConverter.SingleToUInt32Bits((float)world.Translation.Y));
        Assert.Same(root.Node,world.Node!.Parent);
    }
    // reach: unit (supplied checked Front/Docking stored pose; existing native shared arithmetic).
    // Old X=40.288742 (0x422127AC) is retained; old parent=null is repaired to the object's actual root parent.
    [Fact]
    public void CubeFrontTranslationKeepsTheAgreedOldValueAndItsParentIsRestored()
    {
        using var root=new Pose3d("root");using var obj=new Pose3d(Rotation3d.Identity,new Vec3(200,0,22),parent:root);
        using var stored=new Pose3d(Rotation3d.Identity,new Vec3(-87,0,-22));using var robot=new Pose3d("robot",root);
        using var world=CubePreActionPoses.ComposeWorld(obj,stored,robot,75,0);Assert.NotNull(world);
        Assert.Equal(0x422127ACu,BitConverter.SingleToUInt32Bits((float)world!.Translation.X));
        Assert.Same(root.Node,world.Node!.Parent);
    }
    // reach: unit (owned non-root parent chain and supplied robot point; real two-construction projection path).
    // Old X=40.288742 / raw0x422127AC (flat double projection); native f32/local-vs-root formula gives raw0x42C58848.
    [Fact]
    public void ZeroOffsetProjectionUsesTheCheckedLocalAndRootCoordinatesInFloatOrder()
    {
        using var root=new Pose3d("root");using var frame=new Pose3d(Rotation3d.Identity,new Vec3(1000,1000,0),parent:root);
        using var obj=new Pose3d(Rotation3d.Identity,new Vec3(200,0,22),parent:frame);
        using var stored=new Pose3d(Rotation3d.Identity,new Vec3(-87,0,-22));
        using var robot=new Pose3d(Rotation3d.Identity,new Vec3(1113,1020,0),parent:root);
        using var world=CubePreActionPoses.ComposeWorld(obj,stored,robot,75,0);Assert.NotNull(world);
        // Independently rounded from 0x004DFA22..0x004DFBE6: dx=-925,dy=-1000,b=raw0x416AE8F1.
        Assert.Equal(0x42C58848u,BitConverter.SingleToUInt32Bits((float)world!.Translation.X));
    }
    // reach: unit (parent frame tilted 0.8 rad; actual charger dispatch -> base root upright test).
    // Old value: 1 output, because its unparented local goal appears upright. New root R22=cos(0.8) rejects it.
    [Fact]
    public void TiltedRootOrientationRejectsAnApparentlyUprightLocalChargerGoal()
    {
        using var f=new ChargerFixture(0.8f);Assert.True(f.Query());Assert.Empty(f.Output);
    }
    // reach: unit (exact supplied obstacle quad; actual charger collection, marker/goal root flatten and sweep).
    // Old value: 1 output, sweeping local X around -64; new root sweep starts at 880.1 and hits x900.1.
    [Fact]
    public void ObstacleInTheRootFrameSweepRejectsTheChargerGoalButItsOwnIdDoesNot()
    {
        using var f=new ChargerFixture();
        var quad=new Quadrilateral(new(899,-5),new(901,-5),new(899,5),new(901,5));
        Assert.True(f.Query(new[]{new PreActionObstacle(quad,new(900,0),777)}));Assert.Empty(f.Output);
        Assert.False(f.Query(new[]{new PreActionObstacle(quad,new(900,0),f.Charger.ObjectId)}));Assert.Single(f.Output);
    }
    // reach: unit (root object input exercises the native GetParent(root) fault boundary; no invented successful goal).
    // Old value: a successful parentless Pose3d; M1-034 requires skipping this failed derived result.
    [Fact]
    public void RootObjectPoseSkipsTheDerivedResultInsteadOfInventingAParentlessSuccess()
    {
        using var obj=new Pose3d("root object");using var stored=new Pose3d(Rotation3d.Identity,new Vec3(3,4,0));using var robot=new Pose3d("robot");
        using var world=CubePreActionPoses.ComposeWorld(obj,stored,robot,90,3);Assert.Null(world);
    }
}
