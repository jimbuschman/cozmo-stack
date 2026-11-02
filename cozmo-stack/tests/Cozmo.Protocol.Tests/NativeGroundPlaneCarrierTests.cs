using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class NativeGroundPlaneCarrierTests
{
    [Fact]
    public void MatrixProductAndRetainedLookupUseSourceOrderingAndCopies()
    {
        // 0x004F83F8: row-major dot products; 0x0065341C: lower_bound.
        float[] left = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        float[] right = { 9, 8, 7, 6, 5, 4, 3, 2, 1 };
        Assert.Equal(new float[] { 30, 24, 18, 84, 69, 54, 138, 114, 90 },
            NativeGroundPlaneMatrix.Multiply(left, right));
        // VMUL.F32 then VADD.F32 at 0x004F8414..426 rounds each addition: 2^24 + 1 - 2^24 = 0.
        float[] cancellation = { 16777216f, 1f, -16777216f, 0, 0, 0, 0, 0, 0 };
        float[] column = { 1, 0, 0, 1, 0, 0, 1, 0, 0 };
        Assert.Equal(0f, NativeGroundPlaneMatrix.Multiply(cancellation, column)[0]);
        var lookup = new NativeGroundPlaneLookup();
        float[] first = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        float[] second = { 9, 8, 7, 6, 5, 4, 3, 2, 1 };
        float[] output = Enumerable.Repeat(-7f, 9).ToArray();
        Assert.False(lookup.TryGet(0, output));
        Assert.All(output, n => Assert.Equal(-7f, n));
        lookup.Set(-1, first);
        lookup.Set(1, second);
        first[0] = 99;
        Assert.True(lookup.TryGet(-1, output));
        Assert.Equal(1f, output[0]);
        Assert.True(lookup.TryGet(0, output));
        Assert.Equal(9f, output[0]);
        output[0] = 77;
        Assert.True(lookup.TryGet(1, output));
        Assert.Equal(9f, output[0]);
        lookup.Set(-1, second); // a later calibration overwrites this key and retains the other key.
        Assert.True(lookup.TryGet(1, output));
        Assert.Equal(9f, output[0]);
        Assert.False(lookup.TryGet(2, output));
        Assert.Equal(9f, output[0]);
    }

    [Fact]
    public void ProjectedGroundCornersRetainSourceOrderWhenClampHasNoIntersections()
    {
        // Static 4F7774 ground quad and 4F797C clamp: no side reaches y=239.
        float[] identity = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        Assert.Equal(new[] { new NativeGroundPoint(190, 75), new NativeGroundPoint(40, 20),
            new NativeGroundPoint(190, -75), new NativeGroundPoint(40, -20) },
            NativeGroundPlaneProjection.Project(identity, 320, 240));
    }

    [Fact]
    public void SuppliedNativeMatrixAndProjectionUseIndependentLiteralValues()
    {
        // Supplied native K and relative pose, 85EE62 / 651A7C / 4F83F8; not a physical-camera oracle.
        float[] k={2,1,8, 0,4,16, 0,0,1};
        float[] relative={1,0,4, 0,1,8, 0,0,16};
        var h=NativeGroundPlaneMatrix.Multiply(k,relative);
        Assert.Equal(new float[] {2,1,144, 0,4,288, 0,0,16},h);
        Assert.Equal(new[] {new NativeGroundPoint(37.4375f,36.75f),new NativeGroundPoint(15.25f,23f),
            new NativeGroundPoint(28.0625f,-.75f),new NativeGroundPoint(12.75f,13f)},
            NativeGroundPlaneProjection.Project(h,64,64));
    }

    [Fact]
    public void ClampRunsTheBottomEdgeFromLeftToRightLikeNative()
    {
        // 0x004F7B74 with p1=(0,239), p2=(319,239): edge -319..638. Expected X values are that f32 arithmetic
        // worked by hand outside C#; the opposite direction gives 52.84161376953125 and 85.14837646484375.
        NativeGroundPoint[] points = { new(100.5f,50.25f),new(37.25f,300.75f),new(190,50),new(33,333) };
        NativeGroundPlaneProjection.Clamp(points,320,240);
        Assert.Equal(new NativeGroundPoint(52.841552734375f,239),points[1]);
        Assert.Equal(new NativeGroundPoint(85.14840698242188f,239),points[3]);
    }

    [Fact]
    public void ClampUpdatesBothNearCornersOnlyWhenBothSidesIntersect()
    {
        // Literal source segment inputs: bottom edge y=9, extended x=-10..20.
        NativeGroundPoint[] literal = { new(2,0),new(2,20),new(8,0),new(8,20) };
        NativeGroundPlaneProjection.Clamp(literal,11,10);
        Assert.Equal(new[] { new NativeGroundPoint(2,0),new NativeGroundPoint(2,9),
            new NativeGroundPoint(8,0),new NativeGroundPoint(8,9) },literal);
        NativeGroundPoint[] oneOnly = { new(2,0),new(2,20),new(8,0),new(8,8) };
        NativeGroundPlaneProjection.Clamp(oneOnly,11,10);
        Assert.Equal(new[] { new NativeGroundPoint(2,0),new NativeGroundPoint(2,20),
            new NativeGroundPoint(8,0),new NativeGroundPoint(8,8) },oneOnly);
        // 4F7C4C..86: y=x crosses the y=115 image edge at the midpoint on both sides.
        float[] both = { 1, 0, 0, 1, 0, 0, 0, 0, 1 };
        var clipped=NativeGroundPlaneProjection.Project(both, 320, 116);
        Assert.Equal(new NativeGroundPoint(190,190),clipped[0]);
        Assert.Equal(new NativeGroundPoint(190,190),clipped[2]);
        Assert.InRange(clipped[1].X,114.999f,115.001f); Assert.Equal(115f,clipped[1].Y);
        Assert.InRange(clipped[3].X,114.999f,115.001f); Assert.Equal(115f,clipped[3].Y);
        // y=x+2y crosses on only the positive side, so neither near corner changes.
        float[] one = { 1, 0, 0, 1, 2, 0, 0, 0, 1 };
        Assert.Equal(new[] { new NativeGroundPoint(190,340), new NativeGroundPoint(40,80),
            new NativeGroundPoint(190,40), new NativeGroundPoint(40,0) },
            NativeGroundPlaneProjection.Project(one, 320, 116));
    }
    private static object? GroundField(VisionSystem vision,string name)
        =>typeof(VisionSystem).GetField(name,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(vision);

    [Fact]
    public void RemovalRetiresGroundOwnerAndRebindingStartsFresh()
    {
        var seed=CameraCalibration.Nominal();
        using var rig=new M4ControlTests.Rig(engineOptions:new CozmoEngineOptions{BlockPoolPath="",VisionCalibration=seed});
        rig.ToSynced();var vision=rig.Robot.Vision;var old=rig.Engine.Robot!;
        vision.SetNativeOuterCalibration(seed);
        Assert.Same(old,GroundField(vision,"_groundOwner"));Assert.NotNull(GroundField(vision,"_groundLookup"));
        Assert.Same(seed,GroundField(vision,"_groundOuterCalibration"));
        int epoch=vision.NativeGroundEpochForTest;bool checkedBeforeCallback=false;
        Action onRemoved=() =>
        {
            Assert.Null(GroundField(vision,"_groundOwner"));Assert.Null(GroundField(vision,"_groundLookup"));
            Assert.Null(GroundField(vision,"_groundOuterCalibration"));Assert.Null(GroundField(vision,"_groundInstalled"));
            Assert.Same(seed,GroundField(vision,"_groundPendingOuter"));checkedBeforeCallback=true;
        };
        vision.RemovalInvalidated+=onRemoved;
        rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
        vision.RemovalInvalidated-=onRemoved;
        Assert.True(checkedBeforeCallback);Assert.True(vision.NativeGroundEpochForTest>epoch);
        Assert.Null(GroundField(vision,"_groundOwner"));Assert.Null(GroundField(vision,"_groundLookup"));
        var replacement=rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        Assert.NotNull(replacement);Assert.NotSame(old,replacement);Assert.Same(replacement,GroundField(vision,"_groundOwner"));
        Assert.NotNull(GroundField(vision,"_groundLookup"));Assert.Null(GroundField(vision,"_groundInstalled"));
    }

    [Fact]
    public void ReentrantNewBindingSurvivesTheOldRemovalTail()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var vision=rig.Robot.Vision;var old=rig.Engine.Robot!;
        vision.SetNativeOuterCalibration(CameraCalibration.Nominal());
        EngineRobot? replacement=null;int before=vision.NativeGroundEpochForTest, boundEpoch=0;
        Action onRemoved=() =>
        {
            Assert.Null(GroundField(vision,"_groundOwner"));Assert.Null(GroundField(vision,"_groundLookup"));
            replacement=rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
            Assert.NotNull(replacement);boundEpoch=vision.NativeGroundEpochForTest;
            Assert.Same(replacement,GroundField(vision,"_groundOwner"));
        };
        vision.RemovalInvalidated+=onRemoved;
        rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
        vision.RemovalInvalidated-=onRemoved;
        Assert.NotNull(replacement);Assert.NotSame(old,replacement);
        Assert.Same(replacement,GroundField(vision,"_groundOwner"));
        Assert.NotNull(GroundField(vision,"_groundLookup"));
        Assert.True(boundEpoch>before);Assert.Equal(boundEpoch,vision.NativeGroundEpochForTest);
    }
}
