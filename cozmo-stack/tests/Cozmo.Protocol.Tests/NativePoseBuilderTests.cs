using Point2f=Cozmo.Robot.Manipulation.Point2f;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class NativePoseBuilderTests
{
    private static int Bits(float v)=>BitConverter.SingleToInt32Bits(v);
    [Fact]
    public void RadiansDefaultCopyAndAssignmentsPreserveTheirFlags()
    {
        var a=new Radians();Assert.True(a.RescaleEnabled);Assert.Equal(0,a.Value);
        Assert.Equal(0x40490FDB,Bits(new Radians(BitConverter.Int32BitsToSingle(unchecked((int)0xC0490FDB))).Value));
        var unwrapped=new Radians(10,false);var copy=new Radians(unwrapped);copy.Assign(20);Assert.Equal(20,copy.Value);Assert.False(copy.RescaleEnabled);
        a.Assign(unwrapped);Assert.False(a.RescaleEnabled);a.Assign(30);Assert.Equal(30,a.Value);
        Assert.True(float.IsNaN(new Radians(float.NaN).Value));Assert.Equal(float.PositiveInfinity,new Radians(float.PositiveInfinity).Value);
    }
    [Fact]
    public void QuaternionNormalizationUsesBothCheckedBranchesAndHasNoZeroGuard()
    {
        var fast=new Rotation3d(1,Math.ScaleB(1,-20),0,0).Normalized();
        // R4: n2=1+2^-40; 2/(n2+1) rounds to 1-2^-41; multiplication is separate.
        Assert.Equal(1-Math.ScaleB(1,-41),fast.W);Assert.Equal(Math.ScaleB(1,-20)-Math.ScaleB(1,-61),fast.X);
        Assert.Equal(Rotation3d.Identity,new Rotation3d(2,0,0,0).Normalized());
        Assert.True(double.IsNaN(new Rotation3d(0,0,0,0).Normalized().W));
        var product=new Rotation3d(0,1,0,0)*new Rotation3d(0,0,1,0);Assert.Equal(new Rotation3d(0,0,0,1),product);
        var f=new QuaternionF(2,0,0,0).Normalized();Assert.Equal(1,f.W);Assert.Equal(0,f.X);
    }
    [Fact]
    public void AngleAxisNarrowsProductsThenNormalizesAndPointRotationDoesNotNormalize()
    {
        var q=Rotation3d.FromAngleAxis(0,new Vec3(5,0,0));Assert.Equal(Rotation3d.Identity,q);
        // R3 axis is not normalized first: half-turn axis magnitude 2 halves the narrowed cosf component.
        var scaledAxis=Rotation3d.FromAngleAxis(Radians.Pi,new Vec3(2,0,0));Assert.Equal(unchecked((int)0xB2BBBD2E),Bits((float)scaledAxis.W));
        var fastFloat=new QuaternionF(1,BitConverter.Int32BitsToSingle(0x39800000),0,0).Normalized();Assert.Equal(0x39800000,Bits(fastFloat.X));
        Assert.Equal(new Point3f(4,8,12),new Rotation3d(2,0,0,0).RotatePoint(new Point3f(1,2,3)));
        Assert.Equal(new Vec3(1,-2,-3),new Rotation3d(0,1,0,0).Rotate(new Vec3(1,2,3)));
        // R8 association: m00=1,m01=m02=2. The inner cancellation is exact; adding z first loses 1.
        Assert.Equal(1,new Rotation3d(1,1,1,0).Rotate(new Vec3(16777216,-8388608,0.5)).X);
    }
    [Fact]
    public void RawQuaternionEqualityAndClampsFollowOrderedNativeComparisons()
    {
        var a=new Rotation3d(2,0,0,0);Assert.Equal(0,a.AngleDiff(a));Assert.Equal(0,a.AngleDiff(Rotation3d.Identity));
        var nan=new Rotation3d(double.NaN,0,0,0);Assert.False(nan.Equals(nan));Assert.Equal(0,nan.AngleDiff(Rotation3d.Identity));
        Assert.Equal(0,new Rotation3d(-1,0,0,0).AngleDiff(Rotation3d.Identity));
        Assert.Equal(0x40490FDB,Bits(Rotation3d.Identity.AngleDiff(new Rotation3d(0,1,0,0))));
    }
    [Fact]
    public void YawUsesTheLargerProjectionAndTiesPickNegativeFirstPair()
    {
        // R14 hand operands: q=(1,0,1,1) gives A=5,B=13; q=(1,1,0,1) gives A=13,B=5.
        Assert.Equal(MathF.Atan2(2,-3),new Rotation3d(1,0,1,1).AngleAroundZ);
        Assert.Equal(MathF.Atan2(2,-3),new Rotation3d(1,1,0,1).AngleAroundZ);
        Assert.Equal(unchecked((int)0x80000000),Bits(Rotation3d.Identity.AngleAroundZ));
        Assert.Equal(0x40490FDB,Bits(new Rotation3d(0,0,0,1).AngleAroundZ));
    }
    [Fact]
    public void MatrixRepairRunsAboveOneAndNaNWarnsWithTheOwnedPlaceholder()
    {
        var logs=new List<(EngineLogLevel Level,string Key,string Text)>();using var scope=Pose3d.UseSourceLog((level,_,key,text)=>logs.Add((level,key,text)));
        var over=new Mat3(BitConverter.Int32BitsToSingle(0x3F800001),0,0,0,1,0,0,0,1).Orthonormalized();Assert.Equal(1,over[0,0]);Assert.DoesNotContain(logs,e=>e.Key=="RotationMatrixBase.Renormalize.BadRowNorm");
        var repaired=new Mat3(double.NaN,0,0,0,1,0,0,0,1).Orthonormalized();Assert.Equal(1,repaired[0,0]);Assert.Equal(1,repaired[1,1]);Assert.Equal(1,repaired[2,2]);
        var warning=Assert.Single(logs.Where(e=>e.Key=="RotationMatrixBase.Renormalize.BadRowNorm"));Assert.Equal(EngineLogLevel.Warning,warning.Level);Assert.StartsWith("Norm of row 0 = nan!",warning.Text);Assert.Contains("Row = [MISSING: M11-083",warning.Text);
    }
    [Fact]
    public void RodriguesRoundTripsIdentityHalfTurnAndRejectsNonfiniteMatrix()
    {
        Assert.Equal(Vec3.Zero,NativeRodrigues.MatrixToVector(Mat3.Identity));
        // Exact half-turn matrix supplies a rational SVD input; a sin(pi_f) round trip is not an exact half turn.
        var matrix=new Mat3(1,0,0,0,-1,0,0,0,-1);var vector=NativeRodrigues.MatrixToVector(matrix);
        Assert.Equal(Radians.Pi,(float)vector.X);Assert.Equal(0,(float)vector.Y);Assert.Equal(0,(float)vector.Z);
        Assert.Equal(Vec3.Zero,NativeRodrigues.MatrixToVector(new Mat3(double.NaN,0,0,0,1,0,0,0,1)));
        Assert.True(double.IsNaN(NativeRodrigues.VectorToMatrix(new Vec3(double.NaN,0,0))[0,0]));
    }
    [Fact]
    public void DeepCopyAndAssignmentKeepNodeIdentityAndSharedWrappersObserveWrites()
    {
        using var root=new Pose3d("origin");using var source=new Pose3d(Rotation3d.Identity,new Vec3(2,3,4),"part",root);source.Id=77;
        using var copy=source.DeepCopy();Assert.Equal("part_COPY",copy.Name);Assert.Equal(0u,copy.Id);
        using var wrapper=Pose3d.WrapExisting(copy.Node!);copy.Assign(source);Assert.Same(copy.Node,wrapper.Node);Assert.Equal(new Vec3(2,3,4),wrapper.Translation);
        wrapper.Assign(copy);Assert.Equal("part_COPY_COPY",copy.Name);copy.Assign(copy);Assert.Equal("part_COPY_COPY",copy.Name);
        Assert.Same(root.Node,copy.Node!.Parent);
    }
    [Fact]
    public void ThreeDeepTreeAndSameObjectAliasFollowDistinctNativeResults()
    {
        using var root=new Pose3d(Rotation3d.Identity,new Vec3(10,0,0));
        using var first=new Pose3d(Rotation3d.Identity,new Vec3(20,0,0),"first",root);
        using var second=new Pose3d(Rotation3d.Identity,new Vec3(30,0,0),"second",first);
        using var third=new Pose3d(Rotation3d.Identity,new Vec3(40,0,0),"third",second);
        using var result=new Pose3d();Assert.True(Pose3d.GetWithRespectTo(third,root,result));Assert.Equal(new Vec3(90,0,0),result.Translation);Assert.Same(root.Node,result.Node!.Parent);
        Assert.True(Pose3d.GetWithRespectTo(first,first,result));Assert.Equal(new Vec3(20,0,0),result.Translation);Assert.Same(first.Node,result.Node!.Parent);Assert.Equal("first_COPY",result.Name);
        using var wrap=Pose3d.WrapExisting(first.Node!);Assert.True(Pose3d.GetWithRespectTo(first,wrap,result));Assert.Equal(Vec3.Zero,result.Translation);
    }
    [Fact]
    public void SameAsUsesPositiveDifferenceSphereAndExactRootGate()
    {
        using var root=new Pose3d("origin");using var a=new Pose3d(Rotation3d.Identity,Vec3.Zero,"a",root);using var b=new Pose3d(Rotation3d.Identity,new Vec3(3,4,0),"b",root);
        Assert.True(a.IsSameAs(b,5,Radians.Pi));Assert.False(a.IsSameAs(b,4.5,Radians.Pi));Assert.True(a.IsSameAs(b,new Vec3(3,4,0),Radians.Pi,out var delta));Assert.Equal(new Vec3(3,4,0),delta);
        using var unrelated=new Pose3d(Rotation3d.Identity,Vec3.Zero);using var output=new Pose3d();Assert.False(Pose3d.GetWithRespectTo(a,unrelated,output));
    }
    [Fact]
    public void AliasPrecomposeAndTheTwoDimensionalMultiplyDiscrepancyStayDefined()
    {
        var t=new Transform3d(new Rotation3d(0,0,0,1),new Vec3(1,2,3));t.PreComposeWith(ref t);Assert.Equal(new Vec3(2,4,6),t.Translation);
        var a=new Transform2d(new Radians(),new Point2f(1,0));var b=new Transform2d(new Radians(Radians.Pi*0.5f),new Point2f(2,0));
        var product=a.Compose(b);a.MultiplyBy(ref b);Assert.Equal(3,product.Translation.X);Assert.InRange(MathF.Abs(a.Translation.X-1),0,0.000001f);Assert.Equal(2,a.Translation.Y);
        var pose=new Pose2d(new Transform2d(new Radians(),new Point2f(3,4)));Assert.Equal(new Point3f(0,0,0),pose.PlaneNormal);
    }
    [Fact]
    public void EulerAssignmentRetainsTheExistingDestinationFlag()
    {
        var matrix=Mat3.FromEuler(new Radians(),new Radians(),new Radians());var x=new Radians(8,false);var y=new Radians(8,false);var z=new Radians(8,false);
        Assert.False(matrix.GetEulerAngles(ref x,ref y,ref z));Assert.False(x.RescaleEnabled);Assert.False(y.RescaleEnabled);Assert.False(z.RescaleEnabled);Assert.Equal(0,x.Value);Assert.Equal(0,y.Value);Assert.Equal(0,z.Value);
    }
    [Fact]
    public void DepthOverflowReportsTheCorrectFileAndErrorSideEffect()
    {
        var entries=new List<(EngineLogLevel Level,string Key,string Text)>();using var scope=Pose3d.UseSourceLog((level,_,key,text)=>entries.Add((level,key,text)));
        using var pose=new Pose3d();pose.SetParent(pose);bool prior=EngineErrorState.ErrorFlagSet;EngineErrorState.ErrorFlagSet=false;
        try{Assert.Equal(1001,pose.Node!.Depth());
        Assert.Contains(entries,e=>e.Level==EngineLogLevel.Error&&e.Key=="LoopBoundOverflow"&&e.Text=="../../../../coretech/common/include/anki/common/basestation/math/poseTreeNode.h:393");Assert.True(EngineErrorState.ErrorFlagSet);}
        finally{EngineErrorState.ErrorFlagSet=prior;}
    }    [Fact]
    public void AlternateEulerSetsOnlyTheYFlagAndVectorTransformSupportsAliasing()
    {
        var x=new RadiansOutput(new Radians(0,false));var y=new RadiansOutput(new Radians(0,false));var z=new RadiansOutput(new Radians(0,false));
        var x2=new RadiansOutput(new Radians(0,false));var y2=new RadiansOutput(new Radians(0,false));var z2=new RadiansOutput(new Radians(0,false));
        Assert.False(Mat3.Identity.GetEulerAngles(x,y,z,x2,y2,z2));Assert.True(y2.Value.RescaleEnabled);Assert.False(x2.Value.RescaleEnabled);Assert.False(z2.Value.RescaleEnabled);Assert.Equal(Radians.Pi,y2.Value.Value);
        var transform=new Transform3d(new Rotation3d(2,0,0,0),new Vec3(1,2,3));var points=new List<Vec3>{new(1,1,1),new(2,2,2)};
        transform.ApplyTo(points,points);Assert.Equal(new[]{new Vec3(5,6,7),new Vec3(9,10,11)},points);
        var output=new List<Vec3>{new(99,99,99)};transform.ApplyTo(new[]{new Vec3(1,1,1),new Vec3(2,2,2)},output);Assert.Equal(points,output);
    }

}
