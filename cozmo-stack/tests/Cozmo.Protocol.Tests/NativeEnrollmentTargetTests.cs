using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeEnrollmentTargetTests
{
    [Fact]
    public void CurrentImageSelectionUsesLastEligibleAndSphericalNativePoseGate()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot;
        var world = f.M.Vision.Faces;
        var origin = owner.GetNativeOrigin(owner.CurrentOriginId);
        void Add(int id, uint time, Vec3 position, string? name = null)
        {
            var face = new TrackedFace(new DetectedFace(id, new FaceRect(0, 0, 20, 20), new Vec2(4,5), new Vec2(9,5), Name: name), time)
            {
                HeadPose = new Pose3d(Rotation3d.Identity, position),
                NativeTrackingTranslation = position,
                NativeTrackingRotation = Rotation3d.Identity,
            };
            Assert.NotNull(world.AddOrUpdateNativeFace(face, Pose3d.Identity, false, origin.Node!,requireNativePose:true,expectedOwner:owner));
        }
        Add(1, 100, new Vec3(0, 0, 50));
        Add(2, 100, new Vec3(80, 80, 50));
        var initial = new NativeEnrollmentTarget();
        initial.Update(f.M, owner, 0, 100, 2, .5f, 1, () => true);
        Assert.Equal(2, initial.Selected); // incoming zero accepts each eligible ID in ascending order

        Add(2, 101, new Vec3(80, 80, 50));
        var prior = new NativeEnrollmentTarget();
        prior.Reset(1);
        prior.Update(f.M, owner, 0, 101, 2, .5f, 1, () => true);
        Assert.Equal(1, prior.Selected); // sqrt(80²+80²) exceeds 100 mm
        Assert.Equal(2, prior.WrongId);
        Assert.Equal(0u, prior.LastObserved);
    }

    [Fact]
    public void SelectedCurrentFaceWinsBeforeNameAndTooManyUsesRecentWindow()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot;
        var world = f.M.Vision.Faces;
        var origin = owner.GetNativeOrigin(owner.CurrentOriginId);
        void Add(int id, uint time, string name)
        {
            var point = new Vec3(id * 10, 0, 50);
            var face = new TrackedFace(new DetectedFace(id, new FaceRect(0, 0, 20, 20), new Vec2(4,5), new Vec2(9,5), Name: name), time)
            {
                HeadPose = new Pose3d(Rotation3d.Identity, point),
                NativeTrackingTranslation = point,
                NativeTrackingRotation = Rotation3d.Identity,
            };
            Assert.NotNull(world.AddOrUpdateNativeFace(face, Pose3d.Identity, false, origin.Node!,requireNativePose:true,expectedOwner:owner));
        }
        Add(1, 1000, "Named");
        Add(2, 999, "");
        var target = new NativeEnrollmentTarget();
        target.Reset(1);
        target.Update(f.M, owner, 0, 1000, 1, .5f, 0, () => true);
        Assert.Equal(0f, target.TooManySince); // native zero-time sentinel can be written repeatedly
        target.Update(f.M, owner, 0, 1000, 1, .5f, 2, () => true);
        Assert.Equal(2f, target.TooManySince);
        target.Update(f.M, owner, 0, 1500, 1, .5f, 3, () => true);
        Assert.Equal(0f, target.TooManySince);
        Add(1, 1501, "Named");
        target.Update(f.M, owner, 0, 1501, 1, .5f, 3, () => true);
        Assert.Equal(1, target.Selected); // existing selected wins before the name gate
        Assert.Equal(1501u, target.LastObserved);
        Assert.Equal(0, target.WrongId);
    }

    [Fact]
    public void NoEyeUpdateKeepsOldNativeTranslationAndUsesFreshRotationAndOrigin()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var world = f.M.Vision.Faces;
        var origin = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        using var laterOrigin = new Pose3d("later origin");
        var first = new TrackedFace(new DetectedFace(1, new FaceRect(0, 0, 20, 20), new Vec2(4, 5), new Vec2(9, 5)), 10)
        {
            HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(20, 30, 40)),
            NativeTrackingTranslation = new Vec3(20, 30, 40),
            NativeTrackingRotation = Rotation3d.Identity,
        };
        Assert.NotNull(world.AddOrUpdateNativeFace(first, Pose3d.Identity, false, origin.Node!,requireNativePose:true,expectedOwner:f.Robot));
        var rotation = Rotation3d.FromAngleAxis(.3f, new Vec3(0, 0, 1));
        var next = new TrackedFace(new DetectedFace(1, new FaceRect(0, 0, 20, 20)), 11)
        {
            HeadPose = new Pose3d(rotation, new Vec3(90, 80, 70)),
            NativeTrackingTranslation = new Vec3(90, 80, 70),
            NativeTrackingRotation = rotation,
        };
        var entry = world.AddOrUpdateNativeFace(next, Pose3d.Identity, false, laterOrigin.Node!,requireNativePose:true,expectedOwner:f.Robot)!.Face;
        Assert.Equal(new Vec3(20, 30, 40), entry.NativeTrackingTranslation);
        Assert.Equal(rotation, entry.NativeTrackingRotation);
        Assert.Same(laterOrigin.Node, entry.NativeObservationOrigin);
    }
}
