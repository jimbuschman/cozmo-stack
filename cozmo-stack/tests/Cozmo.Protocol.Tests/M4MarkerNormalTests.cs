using System.Numerics;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class M4MarkerNormalTests
{
    // Expected words are from an independent packed-f32 reference of OV129 C1-C5/R2/N2-N4,
    // not Rotation3d/Transform3d or the production corner/normal helper. Every op is rounded separately.
    private static uint[] Bits(Point3f p)=>new[]{BitConverter.SingleToUInt32Bits(p.X),BitConverter.SingleToUInt32Bits(p.Y),BitConverter.SingleToUInt32Bits(p.Z)};
    private static uint[] Bits(Vec3 p)=>Bits(new Point3f(p));
    private static KnownMarker Marker(double w,double h,Pose3d pose)=>new((MarkerType)9,BlockFace.Top,pose,w){HeightMm=h};

    // reach: unit (direct native corner/normal unit, rectangular marker input; production join is wired by Apply).
    [Fact]
    public void RectangularMarkerNormalHasAreaAndNativeSign()
    {
        using var pose=new Pose3d();var marker=Marker(10,6,pose);
        Assert.Equal(new uint[]{0,0xC2700000,0},Bits(RelativeCubeLights.ComputeNormal(marker,pose)));
        var corners=RelativeCubeLights.Get3dCorners(marker,pose);
        Assert.Equal(new uint[]{0xC0A00000,0,0x40400000},Bits(corners[0]));
        Assert.Equal(new uint[]{0xC0A00000,0,0xC0400000},Bits(corners[1]));
        Assert.Equal(new uint[]{0x40A00000,0,0x40400000},Bits(corners[2]));
        Assert.Equal(new uint[]{0x40A00000,0,0xC0400000},Bits(corners[3]));
    }

    // reach: unit (raw nontrivial quaternion/supplied transform; marker pose and supplied parent are deliberately unrelated,
    // proving this unit neither composes the marker pose nor walks parents; no missing join is substituted).
    [Fact]
    public void NontrivialTransformMatchesEveryCornerBitAndNormal()
    {
        using var parent=new Pose3d(Rotation3d.Identity,new Vec3(1000,2000,3000));
        using var pose=new Pose3d(new Rotation3d(.7,-.2,.3,.6),new Vec3(123.45,-87.65,39.125),parent:parent);
        using var markerPose=new Pose3d(Rotation3d.Identity,new Vec3(99,88,77));
        var marker=Marker(25.0000009,13.1250004,markerPose);
        uint[][] expected={new uint[]{0x42F74333,0xC2B8E667,0x42506666},new uint[]{0x42F28999,0xC2C9B333,0x422A999A},
            new uint[]{0x42FB4333,0xC294E667,0x420E6666},new uint[]{0x42F68999,0xC2A5B333,0x41D13333}};
        var corners=RelativeCubeLights.Get3dCorners(marker,pose);
        for(int i=0;i<4;i++)Assert.Equal(expected[i],Bits(corners[i]));
        Assert.Equal(new uint[]{0x439A5994,0xC2678672,0xC1CDCCF0},Bits(RelativeCubeLights.ComputeNormal(marker,pose)));
    }

    // reach: unit (supplied large translation; tests arithmetic cancellation, no replaced normal or transform).
    [Fact]
    public void LargeTranslationCollapsesCornersBeforeDifferences()
    {
        using var pose=new Pose3d(Rotation3d.Identity,new Vec3(1<<30,1<<30,1<<30));var marker=Marker(25,25,pose);
        foreach(var corner in RelativeCubeLights.Get3dCorners(marker,pose))Assert.Equal(new uint[]{0x4E800000,0x4E800000,0x4E800000},Bits(corner));
        Assert.Equal(new uint[]{0,0,0},Bits(RelativeCubeLights.ComputeNormal(marker,pose)));
    }

    // reach: unit (zero marker dimensions; native arithmetic has no failure/normalization branch).
    [Theory][InlineData(0,6,0x80000000u)][InlineData(10,0,0u)][InlineData(0,0,0u)]
    public void ZeroDimensionsReturnTheArithmeticNormal(double width,double height,uint yBits)
    {
        using var pose=new Pose3d();var marker=Marker(width,height,pose);
        Assert.Equal(new uint[]{0,yBits,0},Bits(RelativeCubeLights.ComputeNormal(marker,pose)));
    }

    // reach: unit (known cube at a large valid translation, direct relative callee; actual root flattening and normal,
    // no substituted normal; public play/wire join is tested in M3M4RelativeBuilderTests).
    [Fact]
    public void CollapsedCornerNormalsKeepFirstMarkerInsteadOfRotatedAxisWinner()
    {
        using var pose=new Pose3d(Rotation3d.Identity,new Vec3(1<<30,1<<30,1<<30));
        var cube=new ObservableObject(1,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1)){Pose=pose};
        // Shipped front is first. Unit-axis normals would select Top (positive Z), but translated corners collapse,
        // every normal is zero and strict max keeps Front. Native side fallback corner6 rotates once.
        Assert.Equal(BlockFace.Front,cube.Markers.First().Face);
        Assert.True(cube.Markers.Single(m=>m.Face==BlockFace.Top).NormalOnObject.Z>0.9);
        var source=Enumerable.Range(0,4).Select(i=>new LedPattern(LedColor.White,LedColor.Blue,(uint)i,0,0,0,0)).ToArray();
        var logs=new List<string>();
        var result=RelativeCubeLights.Apply(cube,source,1,Vector2.Zero,(_,_,key,_)=>logs.Add(key),_=>{});
        Assert.Equal(new[]{source[1],source[2],source[3],source[0]},result!);
        Assert.Contains("ActiveCube.GetCornerClosestToXY.IgnoringBecauseBlockOnSide",logs);
    }
}
