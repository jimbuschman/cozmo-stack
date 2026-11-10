using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class LocatedObjectDeletedTests
{
    [Fact]
    public void UnknownThenDeletionReportsPreserveIdsAndDockTarget()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var bottom = f.Add(7, 150, 22);
        f.Add(8, 150, 66);
        var dock = new PickupObjectAction(f.M, 8);
        using var registration = f.M.Docking.RegisterActionHandlers(dock, DockAction.PickupLow);
        var order = new List<string>();
        var raw = new List<(ushort, byte[])>();
        var typed = new List<uint>();
        f.Rig.Engine.EngineToGameMessage += (tag, data) => { if (tag == 0x4C) raw.Add((tag,data.ToArray())); };
        f.Rig.Engine.LocatedObjectDeleted += report => typed.Add(report.ObjectId);
        using var unknown = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x52,true,_ => order.Add("unknown"));
        using var deleted = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4C,true,p =>
        {
            Assert.Null(f.M.World.GetLocatedObjectById(7));
            Assert.Null(f.M.World.GetLocatedObjectById(8));
            order.Add($"deleted:{Assert.IsType<uint>(p)}");
        });
        f.M.World.MarkObjectUnknown(bottom,true);
        Assert.Equal(new[] { "unknown", "unknown", "deleted:7", "deleted:8" },order);
        Assert.Equal(uint.MaxValue,dock.ObjectId);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(new uint[] { 7,8 },typed);
        Assert.Equal(new byte[] { 7,0,0,0 },raw[0].Item2);
        Assert.Equal(new byte[] { 8,0,0,0 },raw[1].Item2);
    }

    [Fact]
    public void NoInterfaceSuppressesNativeButKeepsPublicDeletion()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var obj = f.Add();
        f.Robot.HasExternalInterface = false;
        int native = 0, standalone = 0;
        using var sub = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4C,true,_ => native++);
        f.M.World.LocatedObjectDeleted += _ => standalone++;
        var filter = new BlockWorldFilter(); filter.AllowedIds.Add(obj.ObjectId);
        Assert.Single(f.M.World.DeleteLocatedObjects(filter));
        Assert.Equal(0,native);
        Assert.Equal(1,standalone);
    }

    [Fact]
    public void NonCurrentOriginIsErasedWithoutADeletionReport()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var obj = new ObservableObject(9, ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            { OriginId = f.M.World.CurrentOriginId + 1, PoseState = PoseState.Known };
        f.M.World.AddLocatedObject(obj);
        int native = 0, standalone = 0;
        using var sub = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4C,true,_ => native++);
        f.M.World.LocatedObjectDeleted += _ => standalone++;
        var filter = new BlockWorldFilter { OriginMode = OriginMode.InAnyFrame }; filter.AllowedIds.Add(9);
        Assert.Single(f.M.World.DeleteLocatedObjects(filter));
        Assert.Equal(0,native);
        Assert.Equal(0,standalone);
    }

    [Fact]
    public void ReplacementDuringLocalNotificationStopsRemainingReports()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        using var ownerScope = f.Rig.Engine.EnterSynchronousOwner();
        var world=f.M.World;
        f.Add(7,150,22); f.Add(8,150,66);
        int reports=0;
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4C,true,_ =>
        {
            reports++;
            f.Rig.Engine.Robots.RemoveRobot(1,false);
            f.Rig.Engine.Robots.AddRobot(1,withRic:false);
        });
        var filter=new BlockWorldFilter();filter.AllowedIds.Add(7);filter.AllowedIds.Add(8);
        Assert.Equal(2,world.DeleteLocatedObjects(filter).Count);
        Assert.Equal(1,reports);
    }
}
