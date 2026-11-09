using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeDockingCorrectionTests
{
    private static Rotation3d Quat(double degrees, int axis)
    {
        double half = degrees * Math.PI / 360;
        double c = Math.Cos(half), s = Math.Sin(half);
        return axis switch { 0 => new(c, s, 0, 0), 1 => new(c, 0, s, 0), _ => new(c, 0, 0, s) };
    }

    // reach: native geometry helper. Expected yaw words are from shipped 0x00877330/0x0084AA1C
    // on the local ARM oracle with declared host libm, not values generated from this implementation.
    [Theory]
    [InlineData(0.0, 2, 0x80000000u, false)]
    [InlineData(15.0, 0, 0x00000000u, true)]
    [InlineData(39.999, 0, 0x00000000u, true)]
    [InlineData(40.0, 0, 0x00000000u, false)]
    [InlineData(40.001, 0, 0x00000000u, false)]
    [InlineData(41.0, 1, 0x80000000u, false)]
    [InlineData(89.0, 1, 0x80000000u, true)]
    [InlineData(-170.0, 0, 0x40490FDBu, true)]
    public void FlattenAndYawMatchTheShippedEngineCorpus(double degrees, int axis, uint expectedYaw, bool corrected)
    {
        var input = Quat(degrees, axis);
        var output = NativeDockingGeometry.Clamp(input, input);
        Assert.Equal(corrected, output != input);
        Assert.Equal(expectedYaw, BitConverter.SingleToUInt32Bits(output.AngleAroundZ));
        uint angle = BitConverter.SingleToUInt32Bits((float)((double)output.AngleAroundZ + Math.PI / 2));
        Assert.Equal(expectedYaw == 0x40490FDB ? 0x4096CBE4u : 0x3FC90FDBu, angle);
    }

    [Fact]
    public void ParentFrameSelectsTheGateWhileCorrectionUsesLocalRotation()
    {
        var local = Quat(52, 0);
        var parent = Quat(-40, 0);
        var corrected = NativeDockingGeometry.Clamp(local, parent * local);
        Assert.True(Math.Abs(corrected.X) < 1e-6);
        var smallLocal = Quat(7, 0);
        var rejected = NativeDockingGeometry.Clamp(smallLocal, Quat(40, 0) * smallLocal);
        Assert.Equal(smallLocal, rejected);
    }

    [Fact]
    public void DominantAxisTiesKeepTheEarlierSignedColumn()
    {
        var matrix = new Mat3(1, 0, 0, 0, 1, 0, -0.5, 0.5, 0.5);
        Assert.Equal(-1, RotatedParentAxis.GetNativeZ(matrix));
    }

    // reach: live ImageImuData input, timestamp association, and the docking-specific 0x00656384 gate.
    [Theory]
    [InlineData(0x41B758B4u, false)]
    [InlineData(0x41B758B5u, true)]
    [InlineData(0xC1B758B4u, false)]
    [InlineData(0xC1B758B5u, true)]
    public void DockingBodyGateUsesItsExactThresholdAndIgnoresHeadRate(uint bits, bool rejected)
    {
        using var f = new Fixture();
        var vision = f.M.Vision;
        float rate = BitConverter.UInt32BitsToSingle(bits);
        f.Rig.Data(new ImageImuData { ImageId=101, Line2Number=120, RateY=1000, RateZ=rate });
        f.Rig.Tick();
        uint timestamp = f.Robot.StoredState!.Timestamp;
        var previous = f.Rig.Engine.HasCameraCalibration;
        f.Rig.Engine.HasCameraCalibration = () => false;
        try
        {
            f.Rig.CameraGrayFrame(101, timestamp, Enumerable.Repeat((byte)150, 320*240).ToArray());
            vision.WaitForProcessorIdle();
            Assert.True(vision.NativeDockingRotatingTooFast(f.Robot, timestamp));
            f.Rig.Data(new ImageImuData { ImageId=102, Line2Number=0, RateY=1000, RateZ=rate });
            f.Rig.Tick();
            Assert.True(vision.NativeWorldHasImuBracketForTest(f.Robot, timestamp));
            Assert.Equal(rejected, vision.NativeDockingRotatingTooFast(f.Robot, timestamp));
        }
        finally { f.Rig.Engine.HasCameraCalibration = previous; }
    }

    // reach: native image/marker carrier through the detector, world update and actual 0x48 producer.
    // VisionWhileMoving is the app input that bypasses the independent IMU bracket for this test.
    [Fact]
    public void RealMarkerResultFeedsOriginalOwnerDockingSignal()
    {
        using var f = new Fixture();
        var target = f.Add();
        var marker = target.Markers.First(m => m.Face == BlockFace.Front);
        f.Rig.Robot.SetVisionWhileMoving(true);
        f.Rig.Tick();
        Assert.True(f.M.Docking.StartNativeDock(f.Robot, target.ObjectId, unchecked((short)marker.Code),
            DockAction.PickupLow, PathMotionProfile.Default));
        f.Robot.DockingOwner!.Active = 0; // raw target, not Active, controls the source gate
        int mark = f.Rig.Mark();
        uint timestamp = f.Robot.StoredState!.Timestamp;
        f.Image(501, cube: true);
        Assert.NotEmpty(f.M.Vision.LastResult!.Markers);
        var signal = Assert.Single(f.Rig.SentSince(mark).OfType<DockingErrorSignal>());
        Assert.Equal(timestamp, signal.Timestamp);
        Assert.False(signal.Field5);
        Assert.False(signal.Field6);
    }

    // reach: actual B8 recipient and attachment path with supplied reentrant owner replacement.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReentrantAttachmentCannotPublishIntoReplacementOwner(bool fromChanged)
    {
        using var f = new Fixture();
        var owner = f.Robot;
        var target = f.Add();
        var marker = target.Markers.First(m => m.Face == BlockFace.Front);
        Assert.True(f.M.Docking.StartNativeDock(owner, target.ObjectId, unchecked((short)marker.Code),
            DockAction.PickupLow, PathMotionProfile.Default));
        bool replaced = false;
        void Replace()
        {
            if (replaced) return;
            replaced = true;
            f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId, false);
            f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId, withRic: false);
        }
        if (fromChanged) owner.CarryingOwner!.Changed += _ => Replace();
        else owner.SendFault = message =>
        {
            if (message is not CarryStateUpdate) return null;
            Replace();
            return true;
        };

        f.Rig.Data(new PickAndPlaceResult { Field1=true, Field3=(byte)BlockStatus.BlockPickedUp });
        f.Rig.Tick();
        Assert.True(replaced);
        Assert.NotSame(owner, f.Rig.Engine.Robot);
        Assert.Null(f.Rig.Engine.Robot!.CarryingOwner!.CarriedObjectId);
        Assert.False(target.NativeCarryingTree);
    }

    // reach: real B8 recipient with a same-owner, same-target replacement at the synchronous log callback.
    [Fact]
    public void B8CannotAttachForSameTupleSuccessorStartedByLog()
    {
        using var f = new Fixture();
        var owner = f.Robot;
        var target = f.Add();
        var marker = target.Markers.First(m => m.Face == BlockFace.Front);
        short code = unchecked((short)marker.Code);
        Assert.True(f.M.Docking.StartNativeDock(owner, target.ObjectId, code, DockAction.PickupLow, PathMotionProfile.Default));
        long oldGeneration = f.M.Docking.NativeDockGeneration;
        bool started = false;
        f.M.Docking.Log += message =>
        {
            if (started || !message.StartsWith("PickAndPlaceResult:")) return;
            started = true;
            Assert.True(f.M.Docking.StartNativeDock(owner, target.ObjectId, code, DockAction.PickupLow, PathMotionProfile.Default));
        };
        int mark = f.Rig.Mark();
        f.Rig.Data(new PickAndPlaceResult { Field1=true, Field3=(byte)BlockStatus.BlockPickedUp });
        f.Rig.Tick();
        Assert.True(started);
        Assert.NotEqual(oldGeneration, f.M.Docking.NativeDockGeneration);
        Assert.Null(owner.CarryingOwner!.CarriedObjectId);
        Assert.False(target.NativeCarryingTree);
        Assert.Empty(f.Rig.SentSince(mark).OfType<CarryStateUpdate>());
        Assert.Single(f.Rig.SentSince(mark).OfType<DockWithObject>());
    }

    // reach: owned 0x42 send fault seam; no old send may follow the reentrant successor.
    [Fact]
    public void NativeStartCannotResumeAfterSameTupleSuccessorStartsDuringSend()
    {
        using var f = new Fixture();
        var owner = f.Robot;
        var target = f.Add();
        short code = unchecked((short)target.Markers.First(m => m.Face == BlockFace.Front).Code);
        bool started = false;
        owner.SendFault = message =>
        {
            if (message is not DockWithObject || started) return null;
            started = true;
            Assert.True(f.M.Docking.StartNativeDock(owner, target.ObjectId, code, DockAction.PickupLow, PathMotionProfile.Default));
            return null;
        };
        int mark = f.Rig.Mark();
        Assert.False(f.M.Docking.StartNativeDock(owner, target.ObjectId, code, DockAction.PickupLow, PathMotionProfile.Default));
        Assert.True(started);
        Assert.Single(f.Rig.SentSince(mark).OfType<DockWithObject>());
    }
}
