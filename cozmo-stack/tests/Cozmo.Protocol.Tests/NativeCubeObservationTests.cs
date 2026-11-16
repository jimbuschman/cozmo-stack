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
        Assert.True(NativeObjectObservation.TryCreate(cube,camera,cal,rig.Engine.Robot!,out var report));
        Assert.Equal(123u,report.Timestamp);Assert.Equal(2u,report.Family);Assert.Equal(1u,report.Type);Assert.Equal(17u,report.ObjectId);
        Assert.Equal(0f,report.Pose.X);Assert.Equal(0f,report.Pose.Y);Assert.Equal(100f,report.Pose.Z);
        Assert.Equal(1f,report.Pose.Q0);Assert.Equal(0f,report.Pose.Q1);Assert.Equal(0f,report.Pose.Q2);Assert.Equal(0f,report.Pose.Q3);Assert.Equal(42u,report.Pose.OriginId);
        Assert.Equal(0x80000000u,BitConverter.SingleToUInt32Bits(report.TopOrientationRad));Assert.True(report.IsActive);
        Assert.Equal("7B0000000200000001000000110000007DCB0343F996B74218A461421AA4614200000000000000000000C8420000803F0000000000000000000000002A0000000000008001",Convert.ToHexString(report.PackBody()));
    }
    [Fact]
    public void ChargerSnapshotUsesNativeCornersAndZeroTopAngle()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        using var root = new Pose3d("fixture root") { Id = 42 };
        using var camera = new Pose3d(Rotation3d.Identity, Vec3.Zero, parent: root);
        using var placed = new Pose3d(Rotation3d.Identity, new Vec3(0, 0, 200), parent: root);
        var charger = new ObservableObject(17, ObjectType.Charger_Basic, ChargerGeometry.Markers)
        {
            Pose = placed,
            LastObservedTimestamp = 123
        };
        var calibration = new CameraCalibration
        {
            FocalLengthX = 100, FocalLengthY = 100, CenterX = 0, CenterY = 0, Columns = 320, Rows = 240, DistortionCoefficients = Array.Empty<double>()
        };

        Assert.True(NativeObjectObservation.TryCreate(charger, camera, calibration, rig.Engine.Robot!, out var report));
        // 0x004E9A50 corners: x=0..96, y=-40..40, z=200 and 231 in camera space.
        // With this calibration the extrema are on the z=200 face: (0,-20,48,40).
        Assert.Equal((0f, -20f, 48f, 40f), (report.ImageX, report.ImageY, report.ImageWidth, report.ImageHeight));
        Assert.Equal(0u, BitConverter.SingleToUInt32Bits(report.TopOrientationRad));
        Assert.True(report.IsActive);
        Assert.Equal("7B000000040000000D00000011000000000000000000A0C100004042000020420000000000000000000048430000803F0000000000000000000000002A0000000000000001",
            Convert.ToHexString(report.PackBody())); // independently packed from 0x0061FF5C..0x0061FFD6 and 0x007131E2
        charger.ChargerMarker!.PoseOnObject.Dispose();
        charger.NativePose.Dispose();
    }

    [Fact]
    public void NullCalibrationProducesNativeNanRectangle()
    {
        using var pose=new Pose3d(Rotation3d.Identity,new Vec3(0,0,100));
        var r=NativeObjectObservation.Rectangle(ObjectType.Block_LIGHTCUBE1,pose,null);
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
        var r=NativeObjectObservation.Rectangle(ObjectType.Block_LIGHTCUBE1,pose,cal);
        Assert.Equal(x,BitConverter.SingleToUInt32Bits(r.X));
        Assert.Equal(y,BitConverter.SingleToUInt32Bits(r.Y));
        Assert.Equal(w,BitConverter.SingleToUInt32Bits(r.Width));
        Assert.Equal(h,BitConverter.SingleToUInt32Bits(r.Height));
    }
}
