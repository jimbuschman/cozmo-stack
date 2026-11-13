using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

/// <summary>
/// CHARGER-BOUNDING-QUAD-1 (M13-025, M13-007): FindObjectOnTopOrUnderneathHelper's lambda calls every located candidate's own slot +0x50
/// (P3 0x0062BA7A..0x0062BA86; no family/type check precedes it) and, only after the quads intersect, its slot +0x2C size through
/// GetDimInParentFrame&lt;Z&gt; (P5 0x0062BAA4..0x0062BAB6, 0x005577B4). The call is CanInteract's (0x0063C730): onTop, tolerance 15.0, an empty filter.
///
/// Reference: a light cube at (150, 0, 22), identity. targetZ = 22 + 44*0.5 = 44 (H4 0x0062605E..0x006260A0); refQuad (Block 0x004E62A2, padding 0)
/// spans x in [128, 172], y in [-22, 22].
/// Charger (base 0x0087713A, corners 0x004E9A50, padding 0, pose as passed, translated by the pose x, y 0x004E68CC): identity at (cx, 0) spans
/// x in [cx, cx+96], y in [-40, 40]; its size (0x004EA672 -> +0xF0, stored 0x004E9D12..0x004E9D32) is (96, 80, 31), identity -> Z axis -> 31.
/// ProxObstacle (MarkerlessObject 0x00502C32, size 0x00502DD0 -> GetSizeByType 0x005026EC = (10, 10, 50)): identity at (150, 0) spans x in [145, 155].
/// Height test P6 0x0062BAC2..0x0062BB0E: |targetZ - (cand.z - candDim*0.5)| &lt;= 15 + 1e-5f.
/// </summary>
// reach: unit (NativeConfigurationGeometry.Neighbor called directly with CanInteract's arguments; the charger and the markerless obstacle are inserted
// with AddLocatedObject at chosen poses, substituting the marker observation / AddMarkerlessObject producers; FreeplayTests'
// TheWholeStackDrivesOffTheChargerAndPlaysWithTheCubeItSees is the production-entry case with a located charger).
[Collection("SteppedBehavior missing-report statics")]
public sealed class ChargerBoundingQuadNeighborTests
{
    private static ObservableObject Place(Fixture f,uint id,ObjectType type,Vec3 at)
    {
        var o=new ObservableObject(id,type,Array.Empty<KnownMarker>())
        {Pose=new Pose3d(Rotation3d.Identity,at,parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known};
        f.M.World.AddLocatedObject(o);return o;
    }
    private static ObservableObject? Neighbor(Fixture f,ObservableObject reference)=>
        new NativeConfigurationGeometry(f.M.World,f.Robot).Neighbor(reference,true,15f,new BlockWorldFilter());

    // (a) charger at (300,0,0): x in [300, 396] does not meet [128, 172] -> P4 false, no match, and no throw (the size is never read).
    [Fact]
    public void M13_025_AChargerWhoseQuadMissesTheCubeIsNoNeighbour()
    {
        using var f=new Fixture();var cube=f.Add(7,150,22);Place(f,9,ObjectType.Charger_Basic,new Vec3(300,0,0));
        Assert.Null(Neighbor(f,cube));
    }

    // (b) charger at (100,0,0): x in [100, 196], y in [-40, 40] covers the cube's quad -> P4 true; P6: |44 - (0 - 31*0.5)| = 59.5 > 15.00001 -> no match
    // (census Q3: the charger's top is ~59 mm off).
    [Fact]
    public void M13_025_AChargerUnderTheCubesFootprintFailsTheHeightTest()
    {
        using var f=new Fixture();var cube=f.Add(7,150,22);Place(f,9,ObjectType.Charger_Basic,new Vec3(100,0,0));
        Assert.Null(Neighbor(f,cube));
    }

    // (b2) the same charger at z = 59.5: |44 - (59.5 - 15.5)| = 0 <= 15.00001 -> it is the neighbour, which shows both the charger's quad (P4) and its
    // 31 mm height (+0xF0.z) are the ones read.
    [Fact]
    public void M13_025_AChargerAtTheCubesTopHeightIsTheNeighbour()
    {
        using var f=new Fixture();var cube=f.Add(7,150,22);var charger=Place(f,9,ObjectType.Charger_Basic,new Vec3(100,0,59.5));
        Assert.Same(charger,Neighbor(f,cube));
    }

    // (c) ProxObstacle at (150,0,83): x in [145, 155] inside the cube's quad -> P4 true; P6 with GetSizeByType's 50: |44 - (83 - 25)| = 14 <= 15.00001
    // -> match. (With a 44 mm size it would be |44 - 61| = 17 -> no match.) At z = 95: |44 - 70| = 26 -> no match.
    [Theory]
    [InlineData(83.0,true)]
    [InlineData(95.0,false)]
    public void M13_025_AMarkerlessObstacleUsesItsOwnQuadAndSize(double z,bool expected)
    {
        using var f=new Fixture();var cube=f.Add(7,150,22);var obstacle=Place(f,60,ObjectType.ProxObstacle,new Vec3(150,0,z));
        Assert.Equal(ObjectFamily.MarkerlessObject,obstacle.Family);
        var found=Neighbor(f,cube);
        if(expected)Assert.Same(obstacle,found);else Assert.Null(found);
    }
}

/// <summary>
/// CHARGER-BOUNDING-QUAD-1 GetObstacles (M12-036, M13-025): BlockWorld::GetObstacles 0x00626D44's predicate 0x0062C1D4 takes each located object's size from
/// its slot +0x2C (G6 0x0062C1F0), rz = (own rotation * size).z (G7 0x0062C1FC), the centre z from the re-rooted pose (G8), excludes when both ends are
/// &lt;= minZ or both &gt;= maxZ (G9 0x0062C220..0x0062C27E), and appends (slot +0x50 quad at the own pose, ObjectID) (G10/G11 0x0062C280..0x0062C2E4).
/// Here minZ = 0 (robot pose z) and maxZ = 0 + 100 (the height passed in), as M12RVisBuildTests' band test uses.
/// Charger: size (96, 80, 31) (0x004EA672 -> +0xF0), identity -> rz = 31, extent [z - 15.5, z + 15.5]; quad (base 0x0087713A, corners 0x004E9A50, padding 0)
/// at (100, 0) spans x in [100, 196], y in [-40, 40]. ProxObstacle: size (10, 10, 50) (0x00502DD0 -> 0x005026EC), rz = 50; quad (0x00502C32) at (300, 0)
/// spans x in [295, 305], y in [-5, 5].
/// </summary>
// reach: unit (PreActionValidity.GetObstacles called directly with objects built in the test; ManipulationSystem.NativeObstacles is its production caller,
// exercised by FreeplayTests.TheWholeStackDrivesOffTheChargerAndPlaysWithTheCubeItSees up to the roll's DriveToObject).
[Collection("SteppedBehavior missing-report statics")]
public sealed class ChargerObstacleListTests
{
    private static ObservableObject At(uint id,ObjectType type,double x,double z)=>
        new(id,type,Array.Empty<KnownMarker>()){Pose=new Pose3d(Mat3.Identity,new Vec3(x,0,z)),PoseState=PoseState.Known};
    private static List<PreActionObstacle> Obstacles(params ObservableObject[] objects)=>
        PreActionValidity.GetObstacles(objects,Array.Empty<uint>(),Pose3d.Identity,100.0,0f);

    // (a) charger at (100,0,0): extent [-15.5, 15.5] overlaps [0, 100] -> in, with its own slot +0x50 quad and its ObjectID.
    [Fact]
    public void M12_036_ALocatedChargerAtRobotHeightIsAnObstacleWithItsOwnQuad()
    {
        var o=Assert.Single(Obstacles(At(9,ObjectType.Charger_Basic,100,0)));
        Assert.Equal(9u,o.ObjectId);Assert.False(o.IsStandIn);
        Assert.True(o.Contains(new Point2f(150,0)));Assert.True(o.Contains(new Point2f(101,39)));Assert.True(o.Contains(new Point2f(195,-39)));
        Assert.False(o.Contains(new Point2f(99,0)));Assert.False(o.Contains(new Point2f(197,0)));Assert.False(o.Contains(new Point2f(150,41)));
    }

    // (b) z = -15.5: [-31, 0], both <= 0 -> out (inclusive); z = 115.5: [100, 131], both >= 100 -> out; z = -15: [-30.5, 0.5] -> in.
    [Fact]
    public void M12_036_AChargerEntirelyBelowOrAboveTheBandIsExcluded()
    {
        var result=Obstacles(At(1,ObjectType.Charger_Basic,0,-15.5),At(2,ObjectType.Charger_Basic,0,115.5),At(3,ObjectType.Charger_Basic,0,-15));
        Assert.Equal(new uint[]{3},result.Select(o=>o.ObjectId));
    }

    // (c) ProxObstacle at z = 124: [99, 149], bottom 99 < 100 -> in (a 44 mm size would give [102, 146] -> out); at z = 125: [100, 150] -> out.
    [Fact]
    public void M12_036_AMarkerlessObstacleUsesItsOwnSizeAndQuad()
    {
        var o=Assert.Single(Obstacles(At(60,ObjectType.ProxObstacle,300,124),At(61,ObjectType.ProxObstacle,300,125)));
        Assert.Equal(60u,o.ObjectId);Assert.False(o.IsStandIn);
        Assert.True(o.Contains(new Point2f(304,4)));Assert.False(o.Contains(new Point2f(306,0)));Assert.False(o.Contains(new Point2f(300,6)));
    }
}
