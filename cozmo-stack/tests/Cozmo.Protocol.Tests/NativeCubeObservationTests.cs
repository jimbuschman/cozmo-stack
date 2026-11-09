using Cozmo.Robot.Vision;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class NativeCubeObservationTests
{
    [Fact]
    public void FixedPoseSnapshotCarriesFullNativeFields()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        using var root=new Pose3d("fixture root");root.Id=42;
        using var objectPose=new Pose3d(Rotation3d.Identity,new Vec3(0,0,100),parent:root);
        using var camera=new Pose3d(Rotation3d.Identity,Vec3.Zero,parent:root);
        var cube=new ObservableObject(17,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>()) { Pose=objectPose,LastObservedTimestamp=123 };
        var cal=new CameraCalibration { FocalLengthX=100,FocalLengthY=100,CenterX=160,CenterY=120,Rows=240,Columns=320,DistortionCoefficients=Array.Empty<double>() };
        Assert.True(NativeCubeObservation.TryCreate(cube,camera,cal,rig.Engine.Robot!,out var report));
        Assert.Equal(123u,report.Timestamp);Assert.Equal(2u,report.Family);Assert.Equal(1u,report.Type);Assert.Equal(17u,report.ObjectId);
        Assert.Equal(0f,report.Pose.X);Assert.Equal(0f,report.Pose.Y);Assert.Equal(100f,report.Pose.Z);
        Assert.Equal(1f,report.Pose.Q0);Assert.Equal(0f,report.Pose.Q1);Assert.Equal(0f,report.Pose.Q2);Assert.Equal(0f,report.Pose.Q3);Assert.Equal(42u,report.Pose.OriginId);
        Assert.Equal(0x80000000u,BitConverter.SingleToUInt32Bits(report.TopOrientationRad));Assert.True(report.IsActive);
        Assert.Equal("7B0000000200000001000000110000007DCB0343F996B74218A461421AA4614200000000000000000000C8420000803F0000000000000000000000002A0000000000008001",Convert.ToHexString(report.PackBody()));
    }
    [Fact]
    public void NullCalibrationProducesNativeNanRectangle()
    {
        using var pose=new Pose3d(Rotation3d.Identity,new Vec3(0,0,100));
        var r=NativeCubeObservation.Rectangle(pose,null);
        Assert.Equal(0x7FC00000u,BitConverter.SingleToUInt32Bits(r.X));
        Assert.Equal(0x7FC00000u,BitConverter.SingleToUInt32Bits(r.Y));
        Assert.Equal(0x7FC00000u,BitConverter.SingleToUInt32Bits(r.Width));
        Assert.Equal(0x7FC00000u,BitConverter.SingleToUInt32Bits(r.Height));
    }
    [Theory]
    [InlineData(100f,0x4303CB7Du,0x42B796F9u,0x4261A418u,0x4261A41Au)]
    [InlineData(10f,0x42B68000u,0x424D0000u,0x43098000u,0x43098000u)]
    [InlineData(-22f,0x7FC00000u,0x7FC00000u,0x7FC00000u,0x7FC00000u)]
    public void RectangleMatchesShippedNativeOracle(float z,uint x,uint y,uint w,uint h)
    {
        var cal=new CameraCalibration { FocalLengthX=100,FocalLengthY=100,CenterX=160,CenterY=120,Rows=240,Columns=320,DistortionCoefficients=Array.Empty<double>() };
        using var pose=new Pose3d(Rotation3d.Identity,new Vec3(0,0,z));
        var r=NativeCubeObservation.Rectangle(pose,cal);
        Assert.Equal(x,BitConverter.SingleToUInt32Bits(r.X));
        Assert.Equal(y,BitConverter.SingleToUInt32Bits(r.Y));
        Assert.Equal(w,BitConverter.SingleToUInt32Bits(r.Width));
        Assert.Equal(h,BitConverter.SingleToUInt32Bits(r.Height));
    }
}
