using Cozmo.Protocol;
using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class AiLiftControlTests
{
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
    public async Task Exact32CarryingAndUnknownOwnerAreRefusedBeforeAdmission()
    {
        using var fixture = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var rig = fixture.Rig;
        var owner = rig.Engine.Robot!;
        fixture.Add(7);
        owner.CarryingOwner!.SetCarrying(7);
        Assert.Equal(7u, owner.CarryingOwner.CarriedObjectId);
        int mark = rig.Mark();
        var carried = await rig.Robot.Motion.QueueAiLiftHeightAsync(32);
        Assert.False(carried.Admitted);
        Assert.Contains("placement", carried.Detail);
        Assert.Null(owner.ActionList.GetCurrentAction());
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
