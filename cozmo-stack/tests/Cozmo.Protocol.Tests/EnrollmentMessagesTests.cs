using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class EnrollmentMessagesTests
{
    // reach: API adapter and game-message hub; no enrollment behavior is started by this test.
    [Fact]
    public void PublicRequestsPostTheAppFieldsAndEmptyCancellationInOrder()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        var received = new List<object?>();
        using var enroll = rig.Engine.AnimationMessages.SubscribeMessage(0x87, false, received.Add);
        using var cancel = rig.Engine.AnimationMessages.SubscribeMessage(0x88, false, received.Add);

        rig.Robot.SetFaceToEnroll("Zoë");
        rig.Robot.SetFaceToEnroll("", 123);
        rig.Robot.CancelFaceEnrollment();
        Assert.Empty(received);
        rig.Tick();

        Assert.Equal(3, received.Count);
        Assert.Equal(new NativeEnrollmentRequest("Zoë", 0, 0, true, true, true), received[0]);
        Assert.Equal(new NativeEnrollmentRequest("", 0, 123, true, true, true), received[1]);
        Assert.Null(received[2]);
    }

    // reach: API input validation only. Empty names still reach the native staged-name handler.
    [Fact]
    public void InputRejectsOnlyNamesOutsideTheCladStringBoundary()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        var received = new List<object?>();
        using var enroll = rig.Engine.AnimationMessages.SubscribeMessage(0x87, false, received.Add);
        Assert.Throws<ArgumentNullException>(() => rig.Robot.SetFaceToEnroll(null!));
        Assert.Throws<ArgumentException>(() => rig.Robot.SetFaceToEnroll(new string('é', 128)));
        string name = new string('a', 255);
        rig.Robot.SetFaceToEnroll(name);
        rig.Tick();
        Assert.Equal(name, Assert.IsType<NativeEnrollmentRequest>(Assert.Single(received)).Name);
    }

    // reach: direct result adapter, not the enrollment state machine. The native local emit is synchronous;
    // the copied raw and typed app views are drained later in their shared FIFO entry.
    [Fact]
    public void ResultPacksNativeBytesAndDeliversLocallyBeforeHostViews()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        var owner = rig.Engine.Robot!;
        var order = new List<string>();
        byte[]? raw = null;
        FaceEnrollmentCompletedReport? host = null;
        rig.Engine.EngineToGameMessage += (tag, body) =>
        {
            if (tag != 0x56) return;
            raw = body.ToArray();
            order.Add("raw");
        };
        rig.Engine.FaceEnrollmentCompleted += report => { host = report; order.Add("typed"); };
        using var local = rig.Engine.AnimationMessages.SubscribeMessage(0x56, true, value =>
        {
            Assert.Equal(new FaceEnrollmentCompletedReport(6, -2, "Zoë"), value);
            Assert.Null(raw);
            order.Add("local");
        });

        owner.PublishEnrollmentResult(6, -2, "Zoë");
        Assert.Equal(new[] { "local" }, order);
        rig.Engine.DrainAppDelivery();
        Assert.Equal(new[] { "local", "raw", "typed" }, order);
        Assert.Equal(new byte[] { 6, 0xFE, 0xFF, 0xFF, 0xFF, 4, 0x5A, 0x6F, 0xC3, 0xAB }, raw);
        Assert.Equal(new FaceEnrollmentCompletedReport(6, -2, "Zoë"), host);
    }

    // reach: direct native report adapter, including its string8 length truncation.
    [Fact]
    public void ResultWireLengthUsesLowByteButTypedViewRetainsTheName()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        byte[]? body = null;
        FaceEnrollmentCompletedReport? host = null;
        rig.Engine.EngineToGameMessage += (tag, data) => { if (tag == 0x56) body = data.ToArray(); };
        rig.Engine.FaceEnrollmentCompleted += report => host = report;
        string name = new string('a', 257);

        rig.Engine.Robot!.PublishEnrollmentResult(7, 0x12345678, name);
        rig.Engine.DrainAppDelivery();
        Assert.Equal(new byte[] { 7, 0x78, 0x56, 0x34, 0x12, 1, (byte)'a' }, body);
        Assert.Equal(name, Assert.IsType<FaceEnrollmentCompletedReport>(host).Name);
    }

    // reach: direct adapter gate and queued input owner check, substituting robot retirement.
    [Fact]
    public void RetiredOwnerCannotDeliverAResultOrItsQueuedRequests()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        var owner = rig.Engine.Robot!;
        var inputs = new List<object?>();
        int local = 0, host = 0;
        using var enroll = rig.Engine.AnimationMessages.SubscribeMessage(0x87, false, inputs.Add);
        using var cancel = rig.Engine.AnimationMessages.SubscribeMessage(0x88, false, inputs.Add);
        using var report = rig.Engine.AnimationMessages.SubscribeMessage(0x56, true, _ => local++);
        rig.Engine.EngineToGameMessage += (tag, _) => { if (tag == 0x56) host++; };

        rig.Robot.SetFaceToEnroll("Alice");
        rig.Robot.CancelFaceEnrollment();
        rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId, false);
        owner.PublishEnrollmentResult(6, 7, "Alice");
        rig.Tick();
        rig.Engine.DrainAppDelivery();
        Assert.Empty(inputs);
        Assert.Equal(0, local);
        Assert.Equal(0, host);
    }

    [Fact]
    public void ResultRequiresExternalInterface()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        var owner = rig.Engine.Robot!;
        owner.HasExternalInterface = false;
        int local = 0, host = 0;
        using var report = rig.Engine.AnimationMessages.SubscribeMessage(0x56, true, _ => local++);
        rig.Engine.EngineToGameMessage += (tag, _) => { if (tag == 0x56) host++; };
        owner.PublishEnrollmentResult(0, 1, "Alice");
        rig.Engine.DrainAppDelivery();
        Assert.Equal(0, local);
        Assert.Equal(0, host);
    }
}
