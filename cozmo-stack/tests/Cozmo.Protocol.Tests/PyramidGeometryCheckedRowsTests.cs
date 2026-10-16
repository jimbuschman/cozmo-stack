using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using System.Text.RegularExpressions;
using Xunit;
namespace Cozmo.Protocol.Tests;

public sealed class PyramidGeometryCheckedRowsTests
{
    [Fact]
    public void BlockProjectionKeepsEightCanonicalCornersAndPadsBeforeRotation()
    {
        // Canonical4E60D8, expanded dimensions4E62D6..F2: (2,4,6)+2*1=(4,6,8).
        var p=ObjectFootprint.BlockProjectedCorners(new Vec3(2,4,6),Rotation3d.Identity,1);
        Assert.Equal(new[]{new Point2f(-2,-3),new Point2f(2,-3),new Point2f(-2,-3),new Point2f(2,-3),
            new Point2f(-2,3),new Point2f(2,3),new Point2f(-2,3),new Point2f(2,3)},p);
    }
    [Fact]
    public void ActualCubeQuadUsesPassedRotationAndTranslatesTheSortedSlots()
    {
        var cube=new ObservableObject(7,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>())
            {Pose=new Pose3d(Mat3.Identity,new Vec3(999,999,999))};
        using var root=new Pose3d("fixture-root");
        using var pose=new Pose3d(Mat3.AboutZ(Math.PI/6),new Vec3(10,-20,22));pose.SetParent(root);
        var q=Footprint.GetBoundingQuadXY(cube,pose,0);
        // Rotated square44, ranks BL,TL,BR,TR in physical slots; values from +/-22 at30deg.
        var expected=new[]{new Point2f(1.947441f,-50.05256f),new Point2f(-20.05256f,-11.947441f),
            new Point2f(40.05256f,-28.05256f),new Point2f(18.05256f,10.05256f)};
        for(int i=0;i<4;i++){Assert.InRange(MathF.Abs(q[i].X-expected[i].X),0,0.0001f);Assert.InRange(MathF.Abs(q[i].Y-expected[i].Y),0,0.0001f);}
    }
    [Fact]
    public void FourEqualOrUnorderedAnglesTakeNoStrictLessSwap()
    {
        Assert.Equal(new[]{0,1,2,3},ObjectFootprint.SortFourAngles(new[]{0f,0f,0f,0f}));
        Assert.Equal(new[]{0,1,2,3},ObjectFootprint.SortFourAngles(new[]{float.NaN,float.NaN,float.NaN,float.NaN}));
    }
    [Theory]
    [InlineData(0f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-100000f)]
    public void ActualClosestPoseConsumerIgnoresStoredTolerance(float tolerance)
    {
        var marker=CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1)[0];
        var output=new PreActionPoseOutput();
        output.Poses.Add(new PreActionPose(PreActionType.Docking,marker,new Pose3d(Mat3.Identity,new Vec3(100,0,-18)),0){HeightToleranceMm=tolerance});
        output.Poses.Add(new PreActionPose(PreActionType.Docking,marker,new Pose3d(Mat3.Identity,new Vec3(100,0,4)),0){HeightToleranceMm=1});
        DockPreActionPoses.Finish(output,new Pose3d(Mat3.Identity,new Vec3(200,0,22)),Pose3d.Identity,0.13,false);
        Assert.Equal(0,output.ClosestIndex);Assert.Equal(100,output.OffsetX);Assert.Equal(0,output.OffsetY);
    }
    [Fact]
    public void ProductionHasNoExplicitToleranceReadOutsideItsStorageDeclarations()
    {
        string? root=null;for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {var p=Path.Combine(d.FullName,"cozmo-stack","src","Cozmo.Robot");if(Directory.Exists(p)){root=p;break;}}
        Assert.NotNull(root);
        var readers=new List<string>();
        foreach(var file in Directory.EnumerateFiles(root!,"*.cs",SearchOption.AllDirectories))
        {
            string source=Regex.Replace(File.ReadAllText(file),@"(?m)^\s*//.*$","");
            // The two storage shapes may declare/copy the word; the closed native consumer census
            // permits no explicit access. This guard complements, not replaces, the census review.
            source=Regex.Replace(source,@"(\bpublic\s+float\s+)HeightToleranceMm(\s*\{\s*get;\s*init;\s*\})","$1_storage_$2");
            if(Regex.IsMatch(source,@"\bHeightToleranceMm\b"))readers.Add(file);
        }
        Assert.Empty(readers);
    }
}
