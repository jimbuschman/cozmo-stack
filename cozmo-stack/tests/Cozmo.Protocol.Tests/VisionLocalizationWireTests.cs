using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class VisionLocalizationWireTests
{
    [Fact]
    public void SetNewPoseSendsLatestVisionStampOnceAfterVisionGate()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot;
        using var pose = new Pose3d(Rotation3d.Identity, new Vec3(13, -4, 0), parent: owner.GetNativeOrigin(owner.CurrentOriginId));
        uint timestamp = owner.StoredState!.Timestamp; int mark = f.Rig.Mark();
        lock (owner.PoseTreeGate) Assert.Equal(0u, owner.SetNewPose(pose));
        uint frame = unchecked(owner.PoseFrameId + 42);
        Assert.Equal(0u, owner.PoseHistory.AddVisionOnlyState(timestamp + 10, pose, frame, .1f, 0));
        Assert.DoesNotContain(f.Rig.SentSince(mark), m => m is AbsoluteLocalizationUpdate);
        f.Rig.Tick();
        var sent = Assert.Single(f.Rig.SentSince(mark).OfType<AbsoluteLocalizationUpdate>());
        Assert.Equal(timestamp + 10, sent.Timestamp); Assert.Equal(frame, sent.PoseFrameId);
        Assert.Equal(owner.CurrentOriginId, sent.PoseOriginId);
        Assert.Equal(13f, sent.PoseX, 4); Assert.Equal(-4f, sent.PoseY, 4);
        Assert.Equal(0, owner.VisionPoseUpdated);
        f.Rig.Tick(); Assert.Single(f.Rig.SentSince(mark).OfType<AbsoluteLocalizationUpdate>());
    }

    [Fact]
    public void FailedVisionDefersPendingUpdateUntilNextSuccessfulTick()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot; using var pose = new Pose3d(Rotation3d.Identity, new Vec3(5, 0, 0), parent: owner.GetNativeOrigin(owner.CurrentOriginId));
        lock (owner.PoseTreeGate) Assert.Equal(0u, owner.SetNewPose(pose));
        var old = f.Rig.Engine.VisionUpdateAllResultsFailed;
        int mark = f.Rig.Mark();
        try
        {
            f.Rig.Engine.VisionUpdateAllResultsFailed = () => true; f.Rig.Tick();
            Assert.Equal(1, owner.VisionPoseUpdated);
            Assert.DoesNotContain(f.Rig.SentSince(mark), m => m is AbsoluteLocalizationUpdate);
            f.Rig.Engine.VisionUpdateAllResultsFailed = () => false; f.Rig.Tick();
            Assert.Single(f.Rig.SentSince(mark).OfType<AbsoluteLocalizationUpdate>());
            Assert.Equal(0, owner.VisionPoseUpdated);
        }
        finally { f.Rig.Engine.VisionUpdateAllResultsFailed = old; }
    }

    [Fact]
    public void MissingStampWarnsAndClearsPendingFlag()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot; owner.PoseHistory.Clear(); owner.MarkVisionPoseUpdated();
        var logs = new List<EngineLogEntry>(); f.Rig.Engine.LogEntry += logs.Add;
        int mark = f.Rig.Mark(); f.Rig.Tick();
        Assert.Equal(0, owner.VisionPoseUpdated);
        Assert.DoesNotContain(f.Rig.SentSince(mark), m => m is AbsoluteLocalizationUpdate);
        Assert.Contains(logs, e => e.Name == "Robot.SendAbsLocUpdate.NoVizPoseFound");
    }

    [Fact]
    public void DelocalizationSendsImmediatelyAndAgainAtEligibleTick()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot; int mark = f.Rig.Mark();
        owner.Delocalize(false);
        Assert.Single(f.Rig.SentSince(mark).OfType<AbsoluteLocalizationUpdate>());
        Assert.Equal(1, owner.VisionPoseUpdated);
        f.Rig.Tick();
        Assert.Equal(2, f.Rig.SentSince(mark).OfType<AbsoluteLocalizationUpdate>().Count());
        Assert.Equal(0, owner.VisionPoseUpdated);
    }

    [Fact]
    public void FirstStateGateAndSendFailureClearAtTheCorrectTick()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot; using var pose = new Pose3d(Rotation3d.Identity, new Vec3(5, 0, 0), parent: owner.GetNativeOrigin(owner.CurrentOriginId));
        lock (owner.PoseTreeGate) Assert.Equal(0u, owner.SetNewPose(pose));
        int attempts = 0;
        owner.SendFault = message => { if (message is AbsoluteLocalizationUpdate) { attempts++; return false; } return null; };
        try
        {
            owner.FirstFullStateHandled = false; f.Rig.Tick();
            Assert.Equal(0, attempts); Assert.Equal(1, owner.VisionPoseUpdated);
            owner.FirstFullStateHandled = true; f.Rig.Tick();
            Assert.Equal(1, attempts); Assert.Equal(0, owner.VisionPoseUpdated);
            f.Rig.Tick(); Assert.Equal(1, attempts);
        }
        finally { owner.SendFault = null; owner.FirstFullStateHandled = true; }
    }
}
