using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class AiLiftControlTests
{
    [Fact]
    public async Task PendingPlacementCancellationSendsNoPlacementOrStop()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        f.Add(7); var owner = f.Robot; owner.CarryingOwner!.SetCarrying(7);
        var head = f.Rig.Robot.Motion.SetHeadAngleAsync(.5f); f.Rig.Tick();
        using var cancel = new CancellationTokenSource();
        int mark = f.Rig.Mark();
        var task = f.Rig.Robot.Motion.QueueAiLiftHeightAsync(32, cancel.Token);
        cancel.Cancel(); f.Rig.Tick();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted); Assert.Equal(0x02000001u, result.EngineResult);
        Assert.DoesNotContain(f.Rig.SentSince(mark), m => m is PlaceObjectOnGround or StopAllMotors);
        Assert.False(head.IsCompleted);
        owner.ActionList.Cancel(-1); f.Rig.Tick();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlacementSendFailureAndLostCarryReturnNativeFailures(bool loseCarry)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        f.Add(7); var owner = f.Robot; owner.CarryingOwner!.SetCarrying(7);
        int mark = f.Rig.Mark();
        if (!loseCarry) owner.SendFault = message => message is PlaceObjectOnGround ? false : null;
        var task = f.Rig.Robot.Motion.QueueAiLiftHeightAsync(32);
        if (loseCarry) owner.CarryingOwner.UnsetCarrying();
        f.Rig.Tick();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(loseCarry ? 0x03000011u : 0x03000016u, result.EngineResult);
        Assert.Single(f.Rig.SentSince(mark).OfType<StopAllMotors>());
        Assert.Empty(f.Rig.SentSince(mark).OfType<PlaceObjectOnGround>());
        owner.SendFault = null;
    }

    [Fact]
    public async Task Carrying32RunsActualGroundVerificationAndWithholdsUndefinedAppBody()
    {
        using var scene = new StackFirmwareScene(true);
        var f = scene.F;
        f.X = 200; scene.Motors();
        var local = new List<RobotCompletedAction>();
        var app = new List<ActionCompletionReport>();
        var raw = new List<byte[]>();
        using var sub = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A, true, p => { if (p is RobotCompletedAction r) local.Add(r); });
        f.Rig.Engine.ActionCompleted += app.Add;
        f.Rig.Engine.EngineToGameMessage += Raw;
        void Raw(ushort tag, ReadOnlyMemory<byte> body) { if (tag == 0x5A) raw.Add(body.ToArray()); }
        int mark = f.Rig.Mark();
        var task = f.Rig.Robot.Motion.QueueAiLiftHeightAsync(32);
        var runner = Assert.IsType<Cozmo.Robot.Manipulation.NativePlaceGroundRunner>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(7u, runner.RequiredTrackMask);
        for (int i = 0; i < 80 && !runner.DestructionObserved; i++) scene.Step();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted); Assert.Equal(0u, result.EngineResult);
        Assert.Equal(1, scene.Grounds);
        var messages = f.Rig.SentSince(mark);
        var placement = Assert.Single(messages.OfType<PlaceObjectOnGround>());
        Assert.Equal((0f, 0f, 0f, 100f, 200f, 500f, false), (placement.RelX, placement.RelY, placement.RelAngle, placement.SpeedMmps, placement.AccelMmps2, placement.DecelMmps2, placement.Field6));
        Assert.True(messages.IndexOf(placement) < messages.FindIndex(m => m is StopAllMotors));
        var completed = Assert.Single(local.Where(r => r.Tag == runner.Tag));
        Assert.Equal((byte)0, completed.CompletionVariant);
        Assert.False(completed.ObjectInteraction!.IsPayloadDefined);
        f.Rig.Engine.DrainAppDelivery();
        Assert.DoesNotContain(app, a => a.Tag == runner.Tag);
        Assert.Empty(raw);
        f.Rig.Engine.EngineToGameMessage -= Raw;
    }

    [Fact]
    public async Task VerificationSendOwnerReplacementStopsOldChildContinuation()
    {
        using var scene = new StackFirmwareScene(true);
        var f = scene.F; f.X = 200; f.Head = .7f; scene.Motors();
        var original = f.Robot;
        bool replaced = false, placementSent = false;
        int afterReplacement = -1;
        var originalManager = original.BehaviorManagerOwner!;
        original.SendFault = message =>
        {
            if (message is PlaceObjectOnGround) placementSent = true;
            if (!replaced && placementSent && message is SetHeadAngle or SetBodyAngle or SetLiftHeight)
            {
                replaced = true;
                f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId, false);
                f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId, withRic: false);
                afterReplacement = f.Rig.Mark();
            }
            return null;
        };
        var task = f.Rig.Robot.Motion.QueueAiLiftHeightAsync(32);
        try
        {
            for (int i = 0; i < 80 && !replaced; i++) scene.Step(() => replaced);
            Assert.True(replaced);
            var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted); Assert.True(result.OwnerRetired);
            Assert.DoesNotContain(f.Rig.SentSince(afterReplacement), m => m is SetHeadAngle or SetBodyAngle or SetLiftHeight);
            Assert.False(originalManager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, GroundPlacement.ReactionLockName));
            Assert.False(f.Rig.Engine.Robot!.BehaviorManagerOwner!.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, GroundPlacement.ReactionLockName));
        }
        finally { original.SendFault = null; }
    }

    [Fact]
    public async Task LiftUsesNativeWireAcknowledgementAndDefaultCompletion()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced(); rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.LiftInPos, liftAngle: 0);
        var owner = rig.Engine.Robot!;
        var completed = new List<RobotCompletedAction>();
        owner.ActionList.RegisterActionEndedCallback(completed.Add);
        int mark = rig.Mark();
        var task = rig.Robot.Motion.QueueAiLiftHeightAsync(60);
        var runner = owner.ActionList.GetCurrentAction()!;
        Assert.Equal(0x13, runner.Type);
        Assert.InRange(runner.Tag, 1u, 1_000_000u);
        rig.Tick();
        var wire = Assert.Single(rig.SentSince(mark).OfType<SetLiftHeight>());
        Assert.Equal(60f, wire.HeightMm);
        rig.Data(new MotorActionAck { ActionId = unchecked((byte)(wire.ActionId + 1)) }); rig.Tick();
        Assert.False(task.IsCompleted);
        rig.Data(new MotorActionAck { ActionId = wire.ActionId }); rig.Tick();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.LiftInPos, liftAngle: MathF.Asin(15f / 66f));
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(0u, result.EngineResult);
        Assert.Equal((byte)6, Assert.Single(completed.Where(x => x.Tag == runner.Tag)).CompletionVariant);
        Assert.Equal(0, rig.Robot.Motion.LockedTracks);
    }

    [Fact]
    public async Task Exact32CarryingUsesPlacementAndUnknownOwnerIsRefused()
    {
        using var fixture = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var rig = fixture.Rig;
        var owner = rig.Engine.Robot!;
        fixture.Add(7);
        owner.CarryingOwner!.SetCarrying(7);
        Assert.Equal(7u, owner.CarryingOwner.CarriedObjectId);
        int mark = rig.Mark();
        using var placementCancel = new CancellationTokenSource();
        var manager = owner.BehaviorManagerOwner!;
        Assert.True(manager.IsReactionTriggerEnabled(ReactionTrigger.ObjectPositionUpdated));
        var carried = rig.Robot.Motion.QueueAiLiftHeightAsync(32, placementCancel.Token);
        Assert.Equal(0x18, owner.ActionList.GetCurrentAction()!.Type);
        rig.Tick();
        Assert.Single(rig.SentSince(mark).OfType<PlaceObjectOnGround>());
        Assert.False(manager.IsReactionTriggerEnabled(ReactionTrigger.ObjectPositionUpdated));
        Assert.True(manager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated, GroundPlacement.ReactionLockName));
        placementCancel.Cancel(); rig.Tick();
        Assert.Equal(0x02000000u, (await carried.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        Assert.True(manager.IsReactionTriggerEnabled(ReactionTrigger.ObjectPositionUpdated));
        mark = rig.Mark();
        using var nearbyCancel = new CancellationTokenSource();
        var nearby = rig.Robot.Motion.QueueAiLiftHeightAsync(31.999f, nearbyCancel.Token);
        rig.Tick();
        var nearbyWire = Assert.Single(rig.SentSince(mark).OfType<SetLiftHeight>());
        Assert.Equal(32f, nearbyWire.HeightMm);
        nearbyCancel.Cancel(); rig.Tick();
        Assert.True((await nearby.WaitAsync(TimeSpan.FromSeconds(2))).Admitted);
        mark = rig.Mark();
        var saved = owner.CarryingOwner;
        owner.CarryingOwner = null;
        var unknown = await rig.Robot.Motion.QueueAiLiftHeightAsync(32);
        Assert.False(unknown.Admitted);
        Assert.Contains("carrying state", unknown.Detail);
        owner.CarryingOwner = saved;
        rig.Tick();
        Assert.DoesNotContain(rig.SentSince(mark), m => m is SetLiftHeight);
    }

    [Fact]
    public async Task NegativePresetReactionAndPendingCancellationFollowOwnerQueue()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced(); rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.LiftInPos, liftAngle: 0.5f);
        var owner = rig.Engine.Robot!;
        owner.ActionList.ExternalActionsDisabled = true;
        Assert.False((await rig.Robot.Motion.QueueAiLiftHeightAsync(60)).Admitted);
        owner.ActionList.ExternalActionsDisabled = false;
        var head = rig.Robot.Motion.SetHeadAngleAsync(0.5f); rig.Tick();
        using var cts = new CancellationTokenSource();
        var pending = rig.Robot.Motion.QueueAiLiftHeightAsync(-1f, cts.Token);
        cts.Cancel(); rig.Tick();
        Assert.Equal(0x02000001u, (await pending.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        Assert.False(head.IsCompleted);
        owner.ActionList.Cancel(-1); rig.Tick();
        int mark = rig.Mark();
        using var activeCancel = new CancellationTokenSource();
        var active = rig.Robot.Motion.QueueAiLiftHeightAsync(-1f, activeCancel.Token);
        rig.Tick();
        var wire = Assert.Single(rig.SentSince(mark).OfType<SetLiftHeight>());
        Assert.Equal(92f, wire.HeightMm);
        activeCancel.Cancel(); rig.Tick();
        Assert.Equal(0x02000000u, (await active.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
    }

    [Fact]
    public async Task ReentrantLiftClampOwnerRemovalDoesNotSendOrCompleteTwice()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced(); rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.LiftInPos);
        var owner = rig.Engine.Robot!;
        int completions = 0;
        owner.ActionList.RegisterActionEndedCallback(_ => completions++);
        void ReplaceOnClamp(EngineLogEntry e)
        {
            if (e.Name != "MoveLiftToHeightAction.Init.InvalidHeight") return;
            rig.Engine.Robots.RemoveRobot(1, false);
            rig.Engine.Robots.AddRobot(1, withRic: false);
        }
        rig.Engine.LogEntry += ReplaceOnClamp;
        int mark = rig.Mark();
        var task = rig.Robot.Motion.QueueAiLiftHeightAsync(200);
        rig.Tick();
        rig.Engine.LogEntry -= ReplaceOnClamp;
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.True(result.OwnerRetired);
        Assert.Equal(0x02000000u, result.EngineResult);
        Assert.DoesNotContain(rig.SentSince(mark), m => m is SetLiftHeight);
        Assert.InRange(completions, 0, 1);
    }
}
