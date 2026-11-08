using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class AiHeadControlTests
{
    [Fact]
    public async Task HeadRunsThroughNativeActionAndWatcher()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced(); rig.Calibrate(); rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.3f);
        var owner = rig.Engine.Robot!;
        var completed = new List<RobotCompletedAction>();
        owner.ActionList.RegisterActionEndedCallback(completed.Add);
        int mark = rig.Mark();
        var task = rig.Robot.Motion.QueueAiHeadAngleAsync(0.5f);
        var action = owner.ActionList.GetCurrentAction()!;
        Assert.InRange(action.Tag, 1u, 1_000_000u);
        Assert.Equal(0x12, action.Type);
        rig.Tick();
        var sent = Assert.Single(rig.SentSince(mark).OfType<SetHeadAngle>());
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.5f);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(0u, result.EngineResult);
        var record = Assert.Single(completed.Where(x => x.Tag == action.Tag));
        Assert.Equal((byte)6, record.CompletionVariant);
        Assert.Equal(0, rig.Robot.Motion.LockedTracks);
    }

    [Fact]
    public async Task PendingCancellationDoesNotCancelCurrentAction()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced(); rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.1f);
        var current = rig.Robot.Motion.SetHeadAngleAsync(0.5f); rig.Tick();
        using var cts = new CancellationTokenSource();
        var task = rig.Robot.Motion.QueueAiHeadAngleAsync(0.3f, cts.Token);
        cts.Cancel(); rig.Tick();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(0x02000001u, result.EngineResult);
        Assert.False(current.IsCompleted);
    }

    [Fact]
    public async Task ActiveCancellationAndReactionGateRemainDistinct()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced(); rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.1f);
        using var cts = new CancellationTokenSource();
        var task = rig.Robot.Motion.QueueAiHeadAngleAsync(0.5f, cts.Token);
        rig.Tick(); cts.Cancel(); rig.Tick();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(0x02000000u, result.EngineResult);
        Assert.Equal(0, rig.Robot.Motion.LockedTracks);
        rig.Engine.Robot!.ActionList.ExternalActionsDisabled = true;
        var refused = await rig.Robot.Motion.QueueAiHeadAngleAsync(0.4f);
        Assert.False(refused.Admitted);
        Assert.Null(refused.EngineResult);
    }

    [Fact]
    public async Task UnreadyAndNonfiniteRequestsDoNotEnterActionList()
    {
        using var rig = new M4ControlTests.Rig();
        Assert.False((await rig.Robot.Motion.QueueAiHeadAngleAsync(0.5f)).Admitted);
        rig.ToSynced(); rig.State();
        Assert.False((await rig.Robot.Motion.QueueAiHeadAngleAsync(float.NaN)).Admitted);
    }

    [Fact]
    public async Task ReentrantClippingCancellationAndOwnerReplacementDoNotAdmit()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced(); rig.State();
        using var cts = new CancellationTokenSource();
        void CancelOnClip(EngineLogEntry e) { if (e.Name == "MoveHeadToAngleAction.Constructor.AngleTooHigh") cts.Cancel(); }
        rig.Engine.LogEntry += CancelOnClip;
        var cancelled = await rig.Robot.Motion.QueueAiHeadAngleAsync(1, cts.Token);
        rig.Engine.LogEntry -= CancelOnClip;
        Assert.False(cancelled.Admitted);
        Assert.Null(rig.Engine.Robot!.ActionList.GetCurrentAction());

        var old = rig.Engine.Robot;
        void ReplaceOnClip(EngineLogEntry e)
        {
            if (e.Name != "MoveHeadToAngleAction.Constructor.AngleTooHigh") return;
            rig.Engine.Robots.RemoveRobot(1, false);
            rig.Engine.Robots.AddRobot(1, withRic: false);
        }
        rig.Engine.LogEntry += ReplaceOnClip;
        var replaced = await rig.Robot.Motion.QueueAiHeadAngleAsync(1);
        rig.Engine.LogEntry -= ReplaceOnClip;
        Assert.False(replaced.Admitted);
        Assert.True(replaced.OwnerRetired);
        Assert.NotSame(old, rig.Engine.Robot);
        Assert.Null(rig.Engine.Robot!.ActionList.GetCurrentAction());
    }

    [Fact]
    public async Task QueuedCancellationCannotCancelAnotherRunnerReusingTheTag()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced(); rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.1f);
        var list = rig.Engine.Robot!.ActionList;
        using var cts = new CancellationTokenSource();
        var task = rig.Robot.Motion.QueueAiHeadAngleAsync(0.5f, cts.Token);
        var old = list.GetCurrentAction()!;
        uint tag = old.Tag;
        cts.Cancel(); // The engine has not processed the posted cancellation yet.
        Assert.True(list.Cancel(tag));
        var next = rig.Robot.Motion.SetHeadAngleAsync(0.4f);
        var replacement = list.GetCurrentAction()!;
        Assert.True(replacement.SetTag(tag));
        rig.Tick();
        Assert.Same(replacement, list.GetCurrentAction());
        Assert.False(next.IsCompleted);
        Assert.Equal(0x02000001u, (await task.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
    }

    [Fact]
    public async Task DestroyedOriginalOwnerCompletesPendingRunnerWithItsNativeState()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced(); rig.State();
        var task = rig.Robot.Motion.QueueAiHeadAngleAsync(0.5f);
        rig.Engine.Robots.RemoveRobot(1, false);
        rig.Engine.Robots.AddRobot(1, withRic: false);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.True(result.OwnerRetired);
        Assert.Equal(0x02000001u, result.EngineResult);
        Assert.Null(rig.Engine.Robot!.ActionList.GetCurrentAction());
    }
}
