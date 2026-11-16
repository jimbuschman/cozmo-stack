using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class GuardDogNativeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShippedPlayAloneRunsGuardDogAndPublishesItsResultToAudioAndNeeds(bool win)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var timer = f.Rig.Engine.Timer;
        var guard = Configure(f);
        timer.UpdateTime(10_000_000_000);
        var manager = f.Robot.BehaviorManagerOwner!;
        var needs = new List<NeedsActionId>();
        var audio = new List<BehaviorAudioRequest>();
        var ends = new List<bool>();
        int starts = 0;
        f.Rig.Engine.Needs.NeedsStateSent += needs.Add;
        f.Robot.BehaviorAudioRecipient = audio.Add;
        using var start = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8D, true, _ => starts++);
        using var end = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8E, true, p =>
        { if (p is bool success) ends.Add(success); });

        var activity = f.Runtime.FreeplayObserver.Freeplay.SubActivities.Single(a => a.Id == "PlayAlone");
        activity.OnSelected(timer.SecondsF);
        var choice = activity.Chooser!.GetDesiredActiveBehavior(null, 0, f.Runtime.Context, timer.SecondsF);
        Assert.Same(guard, choice.Behavior);
        Assert.True(manager.StartAsync(guard.Id, timer.SecondsF).GetAwaiter().GetResult());
        int mark = f.Rig.Mark();
        Advance(f, guard);
        Assert.Equal((AnimationTrigger)0xDB, Trigger(guard.OwnedRunner));
        Complete(f, guard);
        Assert.Equal("DriveToBlocks", guard.StateName);
        Advance(f, guard);
        Assert.Equal("Retry[DriveToPose]", guard.OwnedRunner!.NativeName);
        Complete(f, guard);
        Advance(f, guard);
        Assert.Equal((AnimationTrigger)0xDC, Trigger(guard.OwnedRunner));
        Complete(f, guard);
        Assert.Equal(1, starts);
        Assert.Equal(new[] { 0u, 1u, 2u }, f.Rig.SentSince(mark).OfType<StreamObjectAccel>().Where(m => m.Enable).Select(m => m.ObjectID).Order());
        Advance(f, guard);
        Assert.Equal((AnimationTrigger)0xDD, Trigger(guard.OwnedRunner));
        Assert.Equal(0, Assert.IsType<TriggerAnimationAction>(guard.OwnedRunner).Request.Argument2);
        Advance(f, guard);
        Assert.Equal(1, f.Robot.BehaviorPublicState.State.StageTag);
        Assert.Equal(0, f.Robot.BehaviorPublicState.State.Stage(1));
        Assert.Contains(audio, request => request.Slot == 0x14 && request.Id == 0x8379F505 && request.Value == 0x3D69880F);
        Assert.Contains(audio, request => request.Slot == 0x14 && request.Id == 0x972DC63C);

        if (win)
        {
            // Active radio slot 0, with the first raw sample seeding the native high-pass filter.
            f.Rig.Engine.Broadcast(new ObjectAccel { ObjectID = 0, Accel = new ActiveAccel { Z = 1 } });
            for (int i = 0; i < 9; i++)
                f.Rig.Engine.Broadcast(new ObjectAccel { ObjectID = 0, Timestamp = (uint)i + 1, Accel = new ActiveAccel { X = (i & 1) == 0 ? 100 : -100, Z = 1 } });
            Assert.Equal("Fakeout", guard.StateName);
            Advance(f, guard);
            Assert.Equal(1, f.Robot.BehaviorPublicState.State.Stage(1));
            for (uint radio = 0; radio < 3; radio++)
                f.Rig.Engine.Broadcast(new ObjectUpAxisChanged { ObjectID = radio, Timestamp = 100 + radio, UpAxis = UpAxis.ZNegative });
        }
        else
        {
            timer.UpdateTime(70_000_000_000);
            Advance(f, guard);
            Assert.Equal("Sleeping", guard.StateName);
            timer.UpdateTime(70_010_000_000);
        }
        Advance(f, guard);
        var leave = Assert.IsType<CompoundActionSequential>(guard.OwnedRunner);
        Assert.Equal((AnimationTrigger)0xDE, Trigger(leave.Children[0]));
        Assert.Equal((AnimationTrigger)(win ? 0xDA : 0xE0), Trigger(leave.Children[^1]));
        var stage = Assert.Single(leave.Children, c => c.NativeName == "WaitForLambda");
        Assert.Equal(EngineActionResult.Success, stage.CheckIfDone());
        Assert.Equal(win ? 4 : 3, f.Robot.BehaviorPublicState.State.Stage(1));
        Assert.Equal(new[] { win }, ends);
        Assert.Equal(new[] { 0u, 1u, 2u }, f.Rig.SentSince(mark).OfType<StreamObjectAccel>().Where(m => !m.Enable).Select(m => m.ObjectID).Order());

        Complete(f, guard);
        Assert.False(guard.Update(f.Runtime.Context, timer.TimeStampMs));
        manager.Stop(BehaviorStopReason.Completed, timer.SecondsF);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(new[] { win, true }, ends);
        Assert.Equal(4, f.Robot.BehaviorPublicState.State.StageTag);
        Assert.Contains(win ? NeedsActionId.GuardDogWin : NeedsActionId.GuardDogNoInteraction, needs);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadinessUsesRealCubeGeometryAndOnlyAnActualMoveOrDisconnectInterruptsSetup(bool move)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var timer = f.Rig.Engine.Timer;
        var guard = Configure(f);
        timer.UpdateTime(10_000_000_000);
        var context = f.Runtime.Context;
        Assert.True(guard.EvaluateHostReadiness(context).Runnable);
        f.M.Whiteboard.HiccupsActive = true;
        Assert.False(guard.EvaluateHostReadiness(context).Runnable);
        f.M.Whiteboard.HiccupsActive = false;
        Assert.True(guard.EvaluateHostReadiness(context).Runnable);

        var manager = f.Robot.BehaviorManagerOwner!;
        var disconnected = new ObjectConnectionStateMessage(999, 0, ObjectType.Charger_Basic, 0);
        f.Rig.Engine.AnimationMessages.Deliver(0x0D, true, disconnected); // Inactive subscription cannot transition.
        Assert.True(manager.StartAsync(guard.Id, timer.SecondsF).GetAwaiter().GetResult());
        Advance(f, guard);
        f.Rig.Engine.AnimationMessages.Deliver(0x0D, true, new ObjectConnectionStateMessage(999, 0, ObjectType.Charger_Basic, 1));
        Assert.Equal("Init", guard.StateName);
        if (move)
        {
            var reports = new List<ObjectMoved>();
            using var movement = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x0F, true, payload =>
            { if (payload is ObjectMoved report) reports.Add(report); });
            f.Rig.Engine.Broadcast(new ObjectMoved { ObjectID = 0, Timestamp = 131, AxisOfAccel = UpAxis.XPositive });
            Assert.Equal(f.M.World.GetConnectedActiveObjectByActiveIdHelper(0)!.ObjectId, Assert.Single(reports).ObjectID);
            Assert.Equal("SetupInterrupted", guard.StateName);
            Assert.False(guard.Update(context, timer.TimeStampMs));
        }
        else
        {
            f.Rig.Engine.AnimationMessages.Deliver(0x0D, true, disconnected);
            Assert.Equal("BlockDisconnected", guard.StateName);
            Advance(f, guard);
            Assert.Equal((AnimationTrigger)0xD7, Trigger(guard.OwnedRunner));
        }
    }

    private static NativeGuardDogBehavior Configure(PickupPyramidNativeExecutionTests.Fixture f)
    {
        var types = new[] { ObjectType.Block_LIGHTCUBE1, ObjectType.Block_LIGHTCUBE2, ObjectType.Block_LIGHTCUBE3 };
        typeof(OffTreadsClassifier).GetProperty("Current")!.SetValue(f.Rig.Robot.Sensors.OffTreads, OffTreadsState.OnTreads);
        f.Robot.Progression!.SetUnlock(63, true);
        for (uint radio = 0; radio < 3; radio++)
        {
            M4ControlTests.ConnectCube(f.Rig, radio, types[radio]);
            var connected = f.M.World.GetConnectedActiveObjectByActiveIdHelper((int)radio)!;
            var obj = new ObservableObject(connected.ObjectId, types[radio], CubeGeometry.MarkersFor(types[radio]))
            {
                Pose = new Pose3d(Rotation3d.Identity, new Vec3(140 + 45 * radio, 0, 22),
                    parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),
                OriginId = f.Robot.CurrentOriginId,
                PoseState = PoseState.Known
            };
            f.M.World.AddLocatedObject(obj);
        }
        return Assert.IsType<NativeGuardDogBehavior>(f.Runtime.Bound["GuardDog"]);
    }

    private static void Advance(PickupPyramidNativeExecutionTests.Fixture f, NativeGuardDogBehavior guard) =>
        Assert.True(guard.Update(f.Runtime.Context, f.Rig.Engine.Timer.TimeStampMs));

    private static void Complete(PickupPyramidNativeExecutionTests.Fixture f, NativeGuardDogBehavior guard)
    {
        Assert.NotEqual(0u, guard.CurrentRunnerTag);
        f.Rig.Engine.AnimationMessages.Deliver(0x5A, true,
            new RobotCompletedAction(guard.CurrentRunnerTag, 0, 0, Array.Empty<uint>(), 0));
    }

    private static AnimationTrigger Trigger(IActionRunner? action) => Assert.IsType<TriggerAnimationAction>(action).Request.Trigger;
}
