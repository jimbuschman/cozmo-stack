using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class M13HeightAndSmallHullTests
{
    private static ObservableObject Charger() => new(7, ObjectType.Charger_Basic, ChargerGeometry.Markers);

    // reach: unit (explicit matrix substituted; M11-074 live relative-pose map owner remains unbound).
    // M12-012: 0x00557794..D4 selects row2 through GetRotatedParentAxis; size words 96/80/31 at 0x004E9B6C..C90.
    [Fact]
    public void HeightUsesRowOfPassedMatrixInsteadOfColumnOrObjectsOwnPose()
    {
        var charger = Charger();
        var relative = new Mat3(0,1,0, 0,0,1, 1,0,0);
        Assert.Equal(31d, CubeGeometry.DimInParentFrameZ(charger));
        Assert.Equal(96d, CubeGeometry.DimInParentFrameZ(charger, relative));
    }

    // reach: unit (raw row components substituted; M11-074 relative-pose owner remains unbound).
    // M12-012: strict ordered comparisons at 0x005507A0..0x00550854 retain the earlier axis.
    [Theory]
    [InlineData(1, 0, 1, 96)]
    [InlineData(0, 1, 1, 80)]
    [InlineData(1, 1, 1, 96)]
    [InlineData(-1, 0, -1, 96)]
    [InlineData(0, -1, -1, 80)]
    [InlineData(double.NaN, 1, 2, 96)]
    [InlineData(1, double.NaN, 2, 31)]
    [InlineData(1, 2, double.NaN, 80)]
    public void HeightSelectorRetainsEarlierAxisOnTiesAndUnordered(float x, float y, float z, double expected)
    {
        Assert.Equal(expected, CubeGeometry.DimInParentFrameZ(Charger(), new Mat3(1,0,0, 0,1,0, x,y,z)));
    }

    // reach: unit (matrix values supplied directly; shared map owner remains unbound).
    // M12-012: f32 matrix loads before strict comparison; one f32 ULP above X chooses Y.
    [Fact]
    public void HeightSelectorComparesStoredFloatValues()
    {
        Assert.Equal(96d, CubeGeometry.DimInParentFrameZ(Charger(), new Mat3(1,0,0, 0,1,0, 1,1 + Math.Pow(2,-25),0)));
        Assert.Equal(80d, CubeGeometry.DimInParentFrameZ(Charger(), new Mat3(1,0,0, 0,1,0, 1,BitConverter.UInt32BitsToSingle(0x3F800001),0)));
    }

    // reach: unit (supplied relative pose; M11-074 relative-pose map owner remains unbound).
    // M11-088/M12-012: 0x0067EDA2..DC0 supplies (1,15,0); 0x00877990..B2 adds epsilon 0x3727C5AC in f32.
    [Fact]
    public void RelativeHeightKeepsNativeFloatBoundary()
    {
        var relative = new Mat3(0,1,0, 0,0,1, 1,0,0);
        using var equal = new Pose3d(relative, new Vec3(0,0,BitConverter.UInt32BitsToSingle(0x42DE0001)));
        using var above = new Pose3d(relative, new Vec3(0,0,BitConverter.UInt32BitsToSingle(0x42DE0002)));
        Assert.False(CubeGeometry.IsPoseTooHigh(Charger(), equal, 1,15,0));
        Assert.True(CubeGeometry.IsPoseTooHigh(Charger(), above, 1,15,0));
    }

    // reach: unit (point arrays substituted; no live footprint producer assumed).
    // M13-024: convexHull empty return 0x00039174..1FC; minAreaRect zero words 0x0009C7E2..EC; singleton 0x0009CE82..8C.
    [Fact]
    public void EmptySingletonAndRepeatedPointsHaveZeroSizeAndAngle()
    {
        Assert.Empty(MinAreaRectF.ConvexHull(Array.Empty<Point2f>()));
        Assert.Equal(default, MinAreaRectF.Compute(Array.Empty<Point2f>()));
        var point = new Point2f(3,-5);
        var expected = new MinAreaRectF.RotatedRect(3,-5,0,0,0);
        Assert.Equal(expected, MinAreaRectF.Compute(new[]{point}));
        Assert.Equal(expected, MinAreaRectF.Compute(new[]{point,point,point}));
        Assert.All(MinAreaRectF.Points(expected), p => Assert.Equal(point,p));
    }

    // reach: unit (point arrays substituted; no live footprint producer assumed).
    // M13-024: 0x0009CDFC..CEAC midpoint and h1-h0 f32; sqrt/atan2 double, atan2 cast before f32*180 then double/pi.
    [Theory]
    [InlineData(4,0,2,0,16,0)]
    [InlineData(0,4,0,2,16,90)]
    [InlineData(4,4,2,2,32,45)]
    public void TwoPointRectanglesUseNativeLineResult(float x, float y, float cx, float cy, double squaredLength, float degrees)
    {
        var result = MinAreaRectF.Compute(new[]{new Point2f(0,0),new Point2f(x,y)});
        // Independently known geometry: sqrt(16)/sqrt(32) and axis/diagonal angles, converted at native widths.
        float radians = (float)(degrees * Math.PI / 180d);
        float expectedAngle = (float)((double)(radians * 180f) / Math.PI);
        Assert.Equal(cx,result.Cx); Assert.Equal(cy,result.Cy); Assert.Equal((float)Math.Sqrt(squaredLength),result.W); Assert.Equal(0f,result.H);
        Assert.Equal(expectedAngle,result.AngleDeg);
    }

    // reach: unit (collinear fixture points substituted; no live footprint producer assumed).
    // M13-024: clockwise hull retains endpoints; n==2 0x0009CDFC..CEAC. atan2(4,3) conversion is native formula, not port output.
    [Fact]
    public void CollinearHullUsesEndpointsAndFloatAngleScaling()
    {
        var result = MinAreaRectF.Compute(new[]{new Point2f(3,4),new Point2f(1.5f,2),new Point2f(0,0),new Point2f(3,4)});
        float rad = (float)Math.Atan2(4d,3d);
        float angle = (float)((double)(rad * 180f) / Math.PI);
        Assert.Equal(new MinAreaRectF.RotatedRect(1.5f,2,5,0,angle),result);
    }

    // reach: unit (canonical corner list substituted through existing public footprint entry; class producer not exercised).
    // M13-025 0x0087713A -> M13-024 singleton 0x0009CE82..8C -> points 0x00086188 -> translation.
    [Fact]
    public void PublicFootprintEntryAcceptsDegenerateCornerList()
    {
        using var pose = new Pose3d(Mat3.Identity,new Vec3(10,20,0));
        var quad = ObjectFootprint.ObservableObjectBoundingQuadXY(new[]{(3f,5f,0f),(3f,5f,0f)},pose,0);
        for (int i=0;i<4;i++) Assert.Equal(new Point2f(13,25),quad[i]);
    }
}
