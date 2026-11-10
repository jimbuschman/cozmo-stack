using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class ObjectPoseUnknownTests
{
    [Fact]
    public void StackedInvalidationPublishesOriginalIdBeforeDeletion()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var bottom = f.Add(7, 150, 22);
        var top = f.Add(8, 150, 66);
        var order = new List<string>();
        var raw = new List<byte[]>();
        var typed = new List<uint>();
        f.Rig.Engine.EngineToGameMessage += (tag, body) => { if (tag == 0x52) { order.Add("raw"); raw.Add(body.ToArray()); } };
        f.Rig.Engine.ObjectPoseUnknown += report => { order.Add("typed"); typed.Add(report.ObjectId); };
        uint before = f.Rig.Engine.NativeUiDeliveryCount;
        using var subscription = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x52, true, value =>
        {
            Assert.Equal(7u, Assert.IsType<ObjectPoseUnknownReport>(value).ObjectId);
            Assert.Same(bottom, f.M.World.GetLocatedObjectById(7));
            Assert.True(f.Rig.Engine.NativeUiDeliveryCount > before);
            order.Add("local");
        });
        f.M.World.PoseStateChanged += (_, _, _) => order.Add("deleted");
        var result = f.M.World.MarkObjectUnknown(bottom, true);
        Assert.Equal(2, result.Count);
        Assert.Null(f.M.World.GetLocatedObjectById(7));
        Assert.Null(f.M.World.GetLocatedObjectById(8));
        Assert.Equal(new[] { "local", "local", "deleted", "deleted" }, order);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(new uint[] { 7, 7 }, typed);
        Assert.Equal(2, raw.Count);
        Assert.All(raw, bytes => Assert.Equal(new byte[] { 7, 0, 0, 0 }, bytes));
        Assert.Equal(new[] { "raw", "typed", "raw", "typed" }, order.Skip(4));
    }

    [Fact]
    public void NoExternalInterfaceStillDeletesWithoutPublishing()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var obj = f.Add();
        f.Robot.HasExternalInterface = false;
        int count = 0;
        using var sub = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x52, true, _ => count++);
        uint before = f.Rig.Engine.NativeUiDeliveryCount;
        Assert.Single(f.M.World.MarkObjectUnknown(obj, true));
        Assert.Null(f.M.World.GetLocatedObjectById(7));
        Assert.Equal(0, count);
        Assert.Equal(before, f.Rig.Engine.NativeUiDeliveryCount);
    }

    [Fact]
    public void ManualFlipTerminalUsesNativeUnknownRecipient()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        f.Add();
        int reports = 0;
        using var sub = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x52, true, _ => reports++);
        var action = new FlipBlockAction(f.M, 7);
        action.CheckIfDoneTick(ActionResult.Success);
        Assert.Equal(1, reports);
        Assert.Null(f.M.World.GetLocatedObjectById(7));
        Assert.Equal(ActionResult.Retry, action.CheckIfDoneTick(ActionResult.Retry));
        Assert.Equal(1, reports);
    }

    [Fact]
    public void OriginChangeDuringLocalReportStopsOldDeletion()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var obj = f.Add();
        using var sub = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x52, true,
            _ => f.M.World.CurrentOriginId++);
        Assert.Empty(f.M.World.MarkObjectUnknown(obj, true));
        Assert.Contains(obj, f.M.World.LocatedObjects);
    }

    [Fact]
    public void ReplacementDuringLocalReportStopsOldDeletion()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        using var ownerScope = f.Rig.Engine.EnterSynchronousOwner();
        var world = f.M.World;
        var obj = f.Add();
        using var sub = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x52, true, _ =>
        {
            f.Rig.Engine.Robots.RemoveRobot(1, false);
            f.Rig.Engine.Robots.AddRobot(1, withRic: false);
        });
        Assert.Empty(world.MarkObjectUnknown(obj, true));
    }
}

