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

    // Real retained docking target and incoming rendered frame for a different cube.
    // Target pose/marker are supplied so vision cannot replace the captured quaternion.
    // Packed angle expectations were obtained from shipped ARM 0x877330/0x84AA1C,
    // with documented host libm, not the managed clamp under test.
    [Theory]
    [InlineData(39.999,0x3FFA35DDu)]
    [InlineData(39.9999,0x3FEF7B3Au)]
    [InlineData(40.0,0x3FEF7B36u)]
    [InlineData(40.001,0x3FEF7B14u)]
    [InlineData(140.001,0x408A8264u)]
    [InlineData(140.0001,0x3FA2A47Bu)]
    [InlineData(140.0,0x3FA2A47Fu)]
    [InlineData(139.999,0x3FA2A4A2u)]
    public void RealFrameSendsNativeOracleClampAngle(double tilt,uint expectedBits)
    {
        using var f=new Fixture();
        using var pose=new Pose3d(Rotation3d.Identity,new Vec3(600,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        var target=new ObservableObject(777,ObjectType.Block_LIGHTCUBE2,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE2))
        {Pose=pose,OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known,LastObservedTimestamp=f.Robot.StoredState!.Timestamp};
        f.M.World.AddLocatedObject(target);
        var marker=target.Markers.Single(m=>m.Face==BlockFace.Front);
        var q=Quat(tilt,0)*Quat(22,2);
        using var supplied=new Pose3d(q,new Vec3(-22,0,0),parent:target.NativePose);
        marker.PoseOnObject.Assign(supplied);
        f.Rig.Robot.SetVisionWhileMoving(true);f.Rig.Tick();
        Assert.True(f.M.Docking.StartNativeDock(f.Robot,target.ObjectId,unchecked((short)marker.Code),DockAction.PickupLow,PathMotionProfile.Default));
        int mark=f.Rig.Mark();uint timestamp=f.Robot.StoredState!.Timestamp;
        f.Image(501,cube:true); // real camera framing/detector; rendered cube 1 is not the retained cube 2 target
        Assert.NotEmpty(f.M.Vision.LastResult!.Markers);
        var signal=Assert.Single(f.Rig.SentSince(mark).OfType<DockingErrorSignal>());
        Assert.Equal(timestamp,signal.Timestamp);
        Assert.InRange(signal.XDist,577.999f,578.001f);Assert.InRange(signal.YDist,-.001f,.001f);Assert.InRange(signal.ZDist,21.999f,22.001f);
        Assert.False(signal.Field5);Assert.False(signal.Field6);
        float expected=BitConverter.UInt32BitsToSingle(expectedBits);
        Assert.InRange(signal.Angle,expected-0.00002f,expected+0.00002f);
    }

    [Fact]
    public async Task LegacyDockFrameUsesMarkerRootForGateAndLocalForCorrection()
    {
        // Actual public DockAsync, its PnP/frame recipient and real 0x48 send; synthetic
        // projected corners isolate the legacy entry without claiming detector transport.
        // Shipped ARM oracle: Qx(-40) parent, Qx(52)*Qz(22) local yields angle
        // 0x3FFA35DD; the old local-only gate instead yields 0x3FE8448C.
        using var f=new Fixture();var target=f.Add();var marker=target.Markers.First(m=>m.Face==BlockFace.Front);
        using var cancel=new CancellationTokenSource();
        var dock=f.M.Docking.DockAsync(target,marker,DockAction.PickupLow,PathMotionProfile.Default,cancel:cancel.Token);
        try
        {
        using var robotPose=new Pose3d(Quat(-40,0).Matrix(),Vec3.Zero);
        var pd=new VisionPoseData(999,robotPose,0,0,false,false);
        using var cameraPose=pd.CameraPose;var camera=new CameraModel(f.M.Vision.Calibration!,cameraPose);
        var root=Quat(-40,0)*(Quat(52,0)*Quat(22,2));
        using var markerWorld=new Pose3d(root,cameraPose.Apply(new Vec3(0,0,200)));
        var corners=marker.Corners3d().Select(p=>camera.Project(markerWorld.Apply(p))).ToArray();
        Assert.All(corners,p=>Assert.NotNull(p));
        var observed=new ObservedMarker(marker.Code,corners.Select(p=>p!.Value).ToArray(),999,0,0);
        var frame=new VisionFrameResult(1,999,pd,new[]{observed},Array.Empty<ObjectObservation>(),Array.Empty<ObservableObject>(),TimeSpan.Zero);
        int mark=f.Rig.Mark();
        var onFrame=typeof(DockingSystem).GetMethod("OnFrame",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        onFrame.Invoke(f.M.Docking,new object[]{frame});f.Rig.Tick();
        var signal=Assert.Single(f.Rig.SentSince(mark).OfType<DockingErrorSignal>());
        float expected=BitConverter.UInt32BitsToSingle(0x3FFA35DD);
        Assert.InRange(signal.Angle,expected-.01f,expected+.01f);
        Assert.True(MathF.Abs(signal.Angle-BitConverter.UInt32BitsToSingle(0x3FE8448C))>.1f);
        }
        finally { cancel.Cancel();await dock.WaitAsync(TimeSpan.FromSeconds(5)); }
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
