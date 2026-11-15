using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// CAN-INTERACT-ROOT-CHECK-1 (records M12-012, M13-007, M15-020, M15-008; census .scratch/CAN-INTERACT-ROOT-CHECK-1-census.md).
/// DockingComponent::CanInteractWithObjectHelper 0x0063C654 runs, after the family gate (0x0063C65E..0x0063C666), the resting-flat test
/// (0x0063C66C..0x0063C684) and the carried compare (0x0063C686..0x0063C696), GetWithRespectTo(obj pose, Robot::GetPose 0x004EA398, out) and returns 0
/// when it fails (0x0063C6A4..0x0063C6BE): a null node or a different root fails (0x00845A46..0x00845A58, 0x00845A62..0x00845A68). The callers hand
/// that out pose to IsPoseTooHigh (0x0063C5F8/0x0063C60A; 0x0063C824/0x0063C836) and return 0 without it when the helper fails (0x0063C622, 0x0063C84E).
/// IsPoseTooHigh 0x00877954: too high iff D*f1 + f2 + 1e-5 &lt; D*f3 + pose.z, D = 44 for an identity light cube; CanStack (1, 15, 0.5): z &gt; 37.00001;
/// CanPickUp (2, 15, 0.5): z &gt; 81.00001. FindUsableCubesOutOfBeacons (0x0056AF64) and FindFreeCubeToStackOn (0x005E0188) walk origin mode 0, the
/// current origin only. Every expected value below is one of these facts, never what the code returned.
/// </summary>
[Collection("SteppedBehavior missing-report statics")]
public sealed class CanInteractRootCheckTests : IDisposable
{
    private readonly List<Pose3d> _poses=new();
    private readonly List<ObservableObject> _objects=new();
    public void Dispose()
    {
        foreach(var obj in _objects){CubePreActionPoses.ClearForSetPose(obj);foreach(var marker in obj.Markers)marker.PoseOnObject.Dispose();obj.NativePose.Dispose();}
        foreach(var pose in _poses)pose.Dispose();
    }

    private Pose3d Root(Rig rig)=>rig.Vision.World.GetNativeOrigin(rig.Vision.World.CurrentOriginId);
    private Pose3d At(double x,double y,double z,Pose3d parent)
    {var p=new Pose3d(Mat3.Identity,new Vec3(x,y,z));p.SetParent(parent);_poses.Add(p);return p;}

    /// <summary>A light cube located in <paramref name="origin"/> (its key) with its pose under <paramref name="parent"/> (its root), substituting the
    /// observation / delocalization producers that put such an object in the world.</summary>
    private ObservableObject Cube(Rig rig,uint id,ObjectType type,uint origin,Pose3d pose,bool locate=true)
    {
        var o=new ObservableObject(id,type,CubeGeometry.MarkersFor(type)){Pose=pose,PoseState=PoseState.Known,OriginId=origin};
        _objects.Add(o);if(locate)rig.M.World.AddLocatedObject(o);return o;
    }

    // ---------------------------------------------------------------- T1 E4: a different root -> false, no throw

    /// <summary>
    /// M12-012 E4 (0x0063C6B6 GetWithRespectTo; 0x0063C6BC cmp r0,#1; 0x0063C6BE bne -> return 0) and E7 (0x0063C602 cbz -> 0x0063C622; 0x0063C82E cbz -> 0x0063C84E):
    /// a flat, uncarried light cube with nothing on it, keyed in the current origin but with its pose under a second root, makes the helper, CanStackOnTopOfObject and
    /// CanPickUpObject false, with no exception (different roots fail at 0x00845A62..0x00845A68). The same cube under the current root is true for all three
    /// (z 22 &lt;= 37.00001 and &lt;= 81.00001), so the root is what decides.
    /// </summary>
    // reach: unit (DockingSystem's three entries called directly; the cube is inserted with AddLocatedObject under a substituted second root. Production reach
    // through FindUsableCubesOutOfBeacons is cut by the origin-mode-0 filter, T3; a current-origin object under another root is not shown reachable, census Q1).
    [Fact]
    public void M12_012_E4_ACubeUnderASecondRootIsFalseFromAllThreeWithNoThrow()
    {
        using var signals=SignalTestContext.Install();
        using var rig=new Rig();
        uint current=rig.M.World.CurrentOriginId;
        var other=new Pose3d("substituted second root");_poses.Add(other);
        var cube=Cube(rig,8,ObjectType.Block_LIGHTCUBE2,current,At(260,0,22,other));
        Assert.False(PoseTreeNode.HasSameRoot(cube.NativePose.Node,Root(rig).Node));
        Assert.False(rig.M.Docking.CanInteractWithObjectHelper(cube));
        Assert.False(rig.M.Docking.CanStackOnTopOfObject(cube));
        Assert.False(rig.M.Docking.CanPickUpObject(cube));

        using var rig2=new Rig();
        var same=Cube(rig2,8,ObjectType.Block_LIGHTCUBE2,rig2.M.World.CurrentOriginId,At(260,0,22,Root(rig2)));
        Assert.True(rig2.M.Docking.CanInteractWithObjectHelper(same));
        Assert.True(rig2.M.Docking.CanStackOnTopOfObject(same));
        Assert.True(rig2.M.Docking.CanPickUpObject(same));
    }

    // ---------------------------------------------------------------- T2 E6: IsPoseTooHigh gets the out pose (relative z)

    /// <summary>
    /// M12-012 E6: IsPoseTooHigh is given the helper's out pose, the cube's pose with respect to the robot pose (0x0063C5F8/0x0063C60A, 0x0063C824/0x0063C836).
    /// Cube at world z 66 (identity, D = 44):
    /// robot at z 44: relative z 22 -> CanStack: 44+15+1e-5 &lt; 22+22 is false -> not too high -> true (the world z 66 would say too high, 66 &gt; 37.00001);
    /// CanPickUp: 88+15+1e-5 &lt; 22+22 false -> true.
    /// robot at z -16: relative z 82 -> CanStack: 82 &gt; 37.00001 -> false; CanPickUp: 82 &gt; 81.00001 -> false (the world z 66 would say true, 66 &lt;= 81.00001).
    /// </summary>
    // reach: unit (the robot's main pose is assigned directly in the current origin; a robot z other than 0 is substituted, not produced by a robot state).
    [Theory]
    [InlineData(44.0,true,true)]
    [InlineData(-16.0,false,false)]
    public void M12_012_E6_IsPoseTooHighSeesTheHeightRelativeToTheRobot(double robotZ,bool stack,bool pick)
    {
        using var signals=SignalTestContext.Install();
        using var rig=new Rig();
        var robot=rig.Robot.Engine.Robot!;
        var origin=robot.GetNativeOrigin(robot.CurrentOriginId);
        Assert.True(PoseTreeNode.HasSameRoot(origin.Node,Root(rig).Node));
        robot.MainPose.Assign(At(0,0,robotZ,origin));
        var cube=Cube(rig,8,ObjectType.Block_LIGHTCUBE2,rig.M.World.CurrentOriginId,At(260,0,66,Root(rig)));
        Assert.Equal(stack,rig.M.Docking.CanStackOnTopOfObject(cube));
        Assert.Equal(pick,rig.M.Docking.CanPickUpObject(cube));
    }

    // ---------------------------------------------------------------- T3 FindUsableCubesOutOfBeacons: origin mode 0

    /// <summary>
    /// M15-020, FindUsableCubesOutOfBeacons 0x0056AE58: the walk's filter has +0x6C/+0x6D = 0 (0x0056AF64 strh.w r4,[sp,#0x74]; r4 = 0 at 0x0056AEF8), so only the
    /// current origin is walked (FindLocatedObjectHelper 0x0056B040). With one 30 mm beacon at the origin and a flat cube at (200, 0, 22) outside it:
    /// keyed in the current origin and root -> usable (CanPickUp: 22 &lt;= 81.00001; outside the beacon); keyed in an old origin, same root -> empty;
    /// keyed in an old origin under another root -> empty, no throw.
    /// </summary>
    // reach: unit (AIWhiteboard.FindUsableCubesOutOfBeacons called directly; the cube is inserted with AddLocatedObject under a chosen origin key and root,
    // substituting a delocalization the rig does not drive). The wired predicate is the live Docking.CanPickUpObject (ManipulationSystem).
    [Theory]
    [InlineData(0u,false,true)]
    [InlineData(1u,false,false)]
    [InlineData(1u,true,false)]
    public void M15_020_FindUsableCubesOutOfBeaconsWalksTheCurrentOriginOnly(uint originOffset,bool otherRoot,bool usable)
    {
        using var signals=SignalTestContext.Install();
        using var rig=new Rig();
        var wb=rig.M.Whiteboard;
        wb.AddBeacon(At(0,0,0,Root(rig)),30);
        Pose3d parent=Root(rig);
        if(otherRoot){parent=new Pose3d("substituted old-origin root");_poses.Add(parent);}
        Cube(rig,8,ObjectType.Block_LIGHTCUBE2,rig.M.World.CurrentOriginId+originOffset,At(200,0,22,parent));
        var found=wb.FindUsableCubesOutOfBeacons().Select(o=>o.ObjectId).ToArray();
        Assert.Equal(usable?new uint[]{8}:Array.Empty<uint>(),found);
    }

    // ---------------------------------------------------------------- FindFreeCubeToStackOn: origin mode 0

    /// <summary>
    /// M15-008, FindFreeCubeToStackOn 0x005E00EC: FindLocatedObjectHelper 0x005E0246 with the filter's +0x6C/+0x6D = 0 (0x005E0188 strh.w r6,[sp,#0x78]; r6 = 0 at
    /// 0x005E0110): the current origin only. A Known flat cube at (0, 0, 22) inside a 175 mm beacon, nothing carried on it: in the current origin and root it is the
    /// stack target (the M15-024 fixture's case); keyed in an old origin, same root -> null; keyed in an old origin under another root -> null, no throw.
    /// </summary>
    // reach: unit (BringCubeToBeaconBehavior.FindFreeCubeToStackOn called directly, IsUnlocked substituted true; the cube is inserted with AddLocatedObject
    // under a chosen origin key and root).
    [Theory]
    [InlineData(0u,false,true)]
    [InlineData(1u,false,false)]
    [InlineData(1u,true,false)]
    public void M15_008_FindFreeCubeToStackOnWalksTheCurrentOriginOnly(uint originOffset,bool otherRoot,bool found)
    {
        using var signals=SignalTestContext.Install();
        using var rig=new Rig();
        var bring=new BringCubeToBeaconBehavior(rig.M,"Hiking_BringCubeToBeacon",45){IsUnlocked=_=>true};
        var beacon=new AIBeacon(At(0,0,0,Root(rig)),175);
        var carried=Cube(rig,7,ObjectType.Block_LIGHTCUBE1,rig.M.World.CurrentOriginId,At(500,500,22,Root(rig)),locate:false);
        rig.M.Docking.Carrying.SetCarrying(7);
        Pose3d parent=Root(rig);
        if(otherRoot){parent=new Pose3d("substituted old-origin root");_poses.Add(parent);}
        var target=Cube(rig,8,ObjectType.Block_LIGHTCUBE2,rig.M.World.CurrentOriginId+originOffset,At(0,0,22,parent));
        var result=bring.FindFreeCubeToStackOn(carried,beacon);
        if(found)Assert.Same(target,result);else Assert.Null(result);
    }

    // ---------------------------------------------------------------- T4 Neighbor: a null candidate node is rejected

    /// <summary>
    /// M13-007, FindObjectOnTopOrUnderneathHelper's lambda P1 (0x0062BA0E..0x0062BA48) calls GetWithRespectTo(candidate, obj's root): a null candidate node is the
    /// NullInputPose VERIFY and false (0x00845A46..0x00845A58 -> 0x00845CEE movs r0,#0), so that candidate is rejected and the walk goes on (returnFirst,
    /// 0x0061ED82..0x0061ED8E). Reference: a cube at (150, 0, 22), targetZ 44. Candidate 8 (null node) is walked before candidate 9, a cube exactly on top
    /// (|44 - (66 - 22)| = 0 &lt;= 15.00001): the answer is 9 with no exception; with 9 absent it is null.
    /// </summary>
    // reach: unit (NativeConfigurationGeometry.Neighbor called directly with CanInteract's arguments; the null node is made with Dispose, census D-3: no
    // production writer that nulls an object's node is shown).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void M13_007_ANullCandidateNodeIsRejectedNotThrown(bool withValidTop)
    {
        using var f=new Fixture();
        var reference=f.Add(7,150,22);
        var origin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        var nulled=new ObservableObject(8,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
        {Pose=new Pose3d(Rotation3d.Identity,new Vec3(150,0,66),parent:origin),OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known};
        f.M.World.AddLocatedObject(nulled);
        nulled.NativePose.Dispose();
        Assert.Null(nulled.NativePose.Node);
        ObservableObject? top=null;
        if(withValidTop)
        {
            top=new ObservableObject(9,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {Pose=new Pose3d(Rotation3d.Identity,new Vec3(150,0,66),parent:origin),OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known};
            f.M.World.AddLocatedObject(top);
        }
        var found=new NativeConfigurationGeometry(f.M.World,f.Robot).Neighbor(reference,true,15f,new BlockWorldFilter());
        if(withValidTop)Assert.Same(top,found);else Assert.Null(found);
    }
}
