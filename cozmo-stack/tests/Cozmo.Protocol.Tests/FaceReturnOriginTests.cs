using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class FaceReturnOriginTests
{
    private sealed class SuppliedFace : IFaceDetector
    {
        public bool IsAvailable => true;
        public string Description => "supplied face";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp) =>
            new[] { new DetectedFace(7, new FaceRect(100, 70, 90, 90), new Vec2(120, 110), new Vec2(160, 110), Name: "Ada", RollRad: 0)
                { NativeNewEntryRotatingTooFast = false } };
    }

    [Fact]
    public void CapturedFaceLeavesAndReentersTheCurrentOriginAfterDelocalization()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var world = f.M.Vision.Faces;
        f.M.Vision.FaceDetector = new SuppliedFace();
        f.Head = .1f; f.Motors(); f.Image(410, modes: 1 << 2);
        var original = Assert.IsType<FaceEntry>(world.GetFace(7));
        uint stamp = original.LastObservedTimestamp;
        Assert.True(world.ShouldReturnFace(original, stamp, requireId: true));
        Assert.False(world.ShouldReturnFace(original, stamp + 1));
        Assert.Equal(new[] { 7 }, world.GetFaceIDsObservedSince(stamp, requireId: true));
        var behavior = new PlayAnimWithFaceBehavior(f.M.Vision, "test", Array.Empty<AnimationTrigger>());
        Assert.True(behavior.IsRunnable(f.Runtime.Context));

        f.Robot.Delocalize(false);
        Assert.Same(original, world.GetFace(7));
        Assert.False(world.ShouldReturnFace(original, stamp));
        Assert.Empty(world.GetFaceIDsObservedSince(stamp));
        Assert.False(world.HasAnyFaces(stamp));
        Assert.False(behavior.IsRunnable(f.Runtime.Context));

        f.Motors(); f.Image(411, modes: 1 << 2);
        var refreshed = Assert.IsType<FaceEntry>(world.GetFace(7));
        Assert.True(world.ShouldReturnFace(refreshed, refreshed.LastObservedTimestamp));
        Assert.Equal(new[] { 7 }, world.GetFaceIDsObservedSince(stamp, requireId: true));
        Assert.True(behavior.IsRunnable(f.Runtime.Context));
    }

    [Fact]
    public void NativeEligibilityUsesRootIdentityAndDoesNotRequireTrackingTranslation()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot; var world = f.M.Vision.Faces;
        var origin = owner.GetNativeOrigin(owner.CurrentOriginId);
        using var child = new Pose3d("child", origin);
        using var impostor = new Pose3d("same numeric id") { Id = origin.Id };
        FaceEntry Add(int id, PoseTreeNode root)
        {
            var face = new TrackedFace(new DetectedFace(id, new FaceRect(0, 0, 20, 20), Name: "Named"), 55)
                { HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(10, 0, 50)) };
            return world.AddOrUpdateNativeFace(face, Pose3d.Identity, false, root)!.Face;
        }
        var session = Add(-2, child.Node!);
        var other = Add(3, impostor.Node!);
        Assert.Null(session.NativeTrackingTranslation);
        Assert.True(world.ShouldReturnFace(session, 55));
        Assert.False(world.ShouldReturnFace(session, 55, requireId: true));
        Assert.False(world.ShouldReturnFace(session, 56));
        Assert.False(world.ShouldReturnFace(other, 55));
        Assert.Equal(new[] { -2 }, world.GetFaceIDsObservedSince(55));
        Assert.Empty(world.GetFaceIDsObservedSince(55, requireId: true));
    }

    [Fact]
    public void MissingRegisteredCurrentOriginDoesNotUseTheMainPoseAsFallback()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot; var world = f.M.Vision.Faces;
        var face = new TrackedFace(new DetectedFace(4, new FaceRect(0, 0, 20, 20)), 10)
            { HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(10, 0, 50)) };
        var entry = world.AddOrUpdateNativeFace(face, Pose3d.Identity, false, owner.GetNativeOrigin(owner.CurrentOriginId).Node!)!.Face;
        owner.DestroyPoseOriginOwners();
        Assert.False(world.ShouldReturnFace(entry, 10));
        Assert.Empty(world.GetFaceIDsObservedSince(10));
        Assert.Throws<InvalidOperationException>(() => world.GetNativeFaceIds(owner, 10));
    }

    [Fact]
    public void OwnerChangeDuringQueryDoesNotAdmitTheOldRoot()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var world = f.M.Vision.Faces;
        var owner = f.Robot;
        var face = new TrackedFace(new DetectedFace(4, new FaceRect(0, 0, 20, 20)), 10)
            { HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(10, 0, 50)) };
        var entry = world.AddOrUpdateNativeFace(face, Pose3d.Identity, false,
            owner.GetNativeOrigin(owner.CurrentOriginId).Node!)!.Face;
        var original = world.NativeOwner;
        int calls = 0;
        world.NativeOwner = () => ++calls == 1 ? owner : null;
        try { Assert.False(world.ShouldReturnFace(entry, 10)); }
        finally { world.NativeOwner = original; }
    }

    [Fact]
    public void MissingNativeCarrierIsWithheldAndHostOnlyInputsStayExplicit()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var world = f.M.Vision.Faces; var owner = f.Robot;
        var face = new TrackedFace(new DetectedFace(9, new FaceRect(0, 0, 20, 20)), 10)
            { HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(10, 0, 50)) };
        var entry = world.AddOrUpdateNativeFace(face, Pose3d.Identity, false, owner.GetNativeOrigin(owner.CurrentOriginId).Node!)!.Face;
        entry.NativeObservationOrigin = null;
        bool called = false, underFrameGate = false;
        void Missing(string line) { if (line.Contains("M14-009")) { called = true; underFrameGate = Monitor.IsEntered(f.M.World.FrameGate); } }
        SteppedBehavior.ResetMissingForTests(); SteppedBehavior.MissingReported += Missing;
        try
        {
            Assert.False(world.ShouldReturnFace(entry, 10));
            Assert.True(called); Assert.False(underFrameGate);
            Assert.Throws<InvalidOperationException>(() => world.GetNativeFaceIds(owner, 10));
            var host = new TrackedFace(new DetectedFace(10, new FaceRect(0, 0, 20, 20)), 11)
                { HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(10, 0, 50)) };
            var hostEntry = world.AddOrUpdateFace(host, Pose3d.Identity, false)!.Face;
            Assert.False(world.ShouldReturnFace(hostEntry, 11));
            var detached = new FaceWorld();
            Assert.True(detached.ShouldReturnFace(new FaceEntry { Id = -1, LastObservedTimestamp = 11 }, 11));
        }
        finally { SteppedBehavior.MissingReported -= Missing; }
    }

    [Fact]
    public void ReentrantFaceLogDoesNotHoldTheFaceGateWhileWaitingForTheFrameGate()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var world = f.M.Vision.Faces;
        using var frameHeld = new ManualResetEventSlim();
        using var inspect = new ManualResetEventSlim();
        using var callback = new ManualResetEventSlim();
        bool faceReadWhileFrameHeld = false;
        void Log(string line)
        {
            if (!line.Contains("UpdateFace.NewFace")) return;
            callback.Set();
            _ = world.GetFaceIDsObservedSince(0);
        }
        world.Log += Log;
        var holder = Task.Run(() =>
        {
            lock (f.M.World.FrameGate)
            {
                frameHeld.Set();
                if (!inspect.Wait(TimeSpan.FromSeconds(5))) return;
                var faceRead = Task.Run(() => world.Count);
                faceReadWhileFrameHeld = faceRead.Wait(TimeSpan.FromSeconds(2));
            }
        });
        Assert.True(frameHeld.Wait(TimeSpan.FromSeconds(5)));
        var add = Task.Run(() => world.AddOrUpdateFace(
            new TrackedFace(new DetectedFace(12, new FaceRect(0, 0, 20, 20)), 10)
                { HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(10, 0, 50)) }, Pose3d.Identity, false));
        try
        {
            Assert.True(callback.Wait(TimeSpan.FromSeconds(5)));
            inspect.Set();
            Assert.True(holder.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(add.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(faceReadWhileFrameHeld);
        }
        finally
        {
            inspect.Set();
            holder.Wait(TimeSpan.FromSeconds(5));
            add.Wait(TimeSpan.FromSeconds(5));
            world.Log -= Log;
        }
    }
}
