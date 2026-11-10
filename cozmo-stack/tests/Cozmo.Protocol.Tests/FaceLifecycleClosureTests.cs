using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class FaceLifecycleClosureTests
{
    private static FaceEntry Observe(PickupPyramidNativeExecutionTests.Fixture f, int id, uint timestamp, bool frontal, string name = "")
    {
        var face = new TrackedFace(new DetectedFace(id, new FaceRect(1, 2, 10, 10),
            new Vec2(2, 3), new Vec2(4, 3), Name: name)
            { NativeFacingCamera = frontal, NativeNameBytes = System.Text.Encoding.UTF8.GetBytes(name) }, timestamp)
        {
            HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(20, 0, 50)),
            NativeTrackingTranslation = new Vec3(20, 0, 50), NativeTrackingRotation = Rotation3d.Identity,
        };
        var origin = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        return f.M.Vision.Faces.AddOrUpdateNativeFace(face, Pose3d.Identity, false, origin.Node!, true,
            requireNativePose: true, expectedOwner: f.Robot)!.Face;
    }

    [Fact]
    public void FrontalCounterThresholdAndNamedEventsFollowNativeOrdering()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var events = new List<EngineLogEntry>(); f.Rig.Engine.LogEntry += e => { if (e.Level == EngineLogLevel.Event) events.Add(e); };
        FaceEntry entry = null!;
        for (uint i = 1; i <= 29; i++) entry = Observe(f, 7, i, true);
        Assert.Equal(29u, entry.NativeFrontalObservations);
        Observe(f, 7, 30, false); Assert.Equal(29u, entry.NativeFrontalObservations);
        Observe(f, 7, 31, true); Assert.Equal(31u, entry.NativeFrontalObservations);
        var persistent = Assert.Single(events.Where(e => e.Name == "robot.vision.face_recognition.persistent_session_only"));
        Assert.Equal("7", persistent.Text); Assert.Equal("0", persistent.Data["$data"]);
        Observe(f, 7, 10032, false);
        Assert.Contains(events, e => e.Name == "robot.vision.face_recognition.re_recognized" && e.Data["$data"] == "0");
        var named = Observe(f, -7, 1, false, "Ada");
        Assert.Contains(events, e => e.Name == "robot.vision.face_recognition.immediate_recognition" && e.Text == "-7" && e.Data["$data"] == "1");
        Observe(f, -7, 2, false); Assert.False(named.HasName); // authentic empty name replaces old name
        Observe(f, 8, 100, false, "Ada");
        Observe(f, 8, 10100, false, "Ada");
        Assert.DoesNotContain(events, e => e.Name == "robot.vision.face_recognition.re_recognized" && e.Text == "8");
        Observe(f, 8, 20101, false, "Ada");
        Assert.Contains(events, e => e.Name == "robot.vision.face_recognition.re_recognized" && e.Text == "8" && e.Data["$data"] == "1");
    }

    [Fact]
    public void ExpiryAndCollisionRemapTelemetryUseDestinationState()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var world = f.M.Vision.Faces;
        var events = new List<EngineLogEntry>(); f.Rig.Engine.LogEntry += e => { if (e.Level == EngineLogLevel.Event) events.Add(e); };
        var old = Observe(f, 1, 10, false); old.NativeFrontalObservations = 30;
        var destination = Observe(f, 2, 10, false); destination.NativeFrontalObservations = 30;
        Assert.True(world.ChangeFaceID(1, 2, ""));
        Assert.Same(destination, world.GetFace(2)); Assert.Equal(30u, destination.NativeFrontalObservations);
        Assert.Contains(events, e => e.Name == "robot.vision.update_face_id" && e.Text == "2" && e.Data["$data"] == "1");
        int deleted = 0;
        using var sub = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4B, true, payload =>
        {
            Assert.Equal(2, Assert.IsType<NativeDeletedFace>(payload).Id);
            Assert.Same(destination, world.GetFace(2));
            Assert.Same(destination, Observe(f, 2, 20, false)); // native pre-erase callback may reenter
            deleted++;
        });
        world.UpdateNative(15011, f.Robot, () => true);
        Assert.Contains(events, e => e.Name == "robot.vision.remove_unobserved_session_only_face" && e.Text == "2" && e.Data["$data"] == "10");
        Assert.Null(world.GetFace(2)); Assert.Equal(1, deleted);
        world.UpdateNative(15012, f.Robot, () => true);
        Assert.Single(events.Where(e => e.Name == "robot.vision.remove_unobserved_session_only_face"));
        Assert.Equal(1, deleted);
    }

    [Theory]
    [InlineData(255, 255)]
    [InlineData(256, 0)]
    [InlineData(257, 1)]
    public void StockReportNarrowsRawNameLengthLikeClad(int length, int written)
    {
        var report = new RobotObservedFaceReport(1, 2, default, new FaceRect(0, 0, 1, 1), "",
            Array.Empty<Vec2>(), Array.Empty<Vec2>(), Array.Empty<Vec2>(), Array.Empty<Vec2>())
        { NativeNameBytes = Enumerable.Repeat((byte)0xC3, length).ToArray() };
        byte[] body = report.PackBody();
        Assert.Equal(written, body[56]);
        Assert.Equal(written, body.Skip(57).TakeWhile(b => b == 0xC3).Count());
    }
}
