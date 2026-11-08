using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class AiHeadControlTests
{
    [Fact]
    public void ObservationUsesTheCurrentOwnersPublishedObjectAndReactionGate()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        var before = rig.Robot.Motion.GetAiBodyObservation();
        Assert.NotNull(before.OwnerToken);
        Assert.Null(before.LastState);
        Assert.False(before.CanRequestHeadAction);
        EngineRobotState? delivered = null, seenInCallback = null;
        rig.Engine.RobotStatePublished += state =>
        {
            delivered = state;
            seenInCallback = rig.Robot.Motion.GetAiBodyObservation().LastState;
        };
        rig.State(head: 0.2f);
        var current = rig.Robot.Motion.GetAiBodyObservation();
        Assert.Same(delivered, current.LastState);
        Assert.Same(delivered, seenInCallback);
        Assert.Equal(before.OwnerToken, current.OwnerToken);
        Assert.True(current.CanRequestHeadAction);
        rig.Engine.Robot!.ActionList.ExternalActionsDisabled = true;
        Assert.False(rig.Robot.Motion.GetAiBodyObservation().CanRequestHeadAction);
        AiBodyObservation? duringRetirement = null;
        rig.Engine.Robot.Lifetime.Bind(-1, _ => duringRetirement = rig.Robot.Motion.GetAiBodyObservation());
        rig.Engine.Robots.RemoveRobot(1, false);
        Assert.Equal(new AiBodyObservation(null, false, null), duringRetirement);
        rig.Engine.Robots.AddRobot(1, withRic: false);
        var replacement = rig.Robot.Motion.GetAiBodyObservation();
        Assert.NotEqual(current.OwnerToken, replacement.OwnerToken);
        Assert.Null(replacement.LastState);
        Assert.False(replacement.CanRequestHeadAction);
    }

    [Fact]
    public async Task LegacyHeadAndLiftCompletionsUseNativeDefaultPayload()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced(); rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
        var list = rig.Engine.Robot!.ActionList;
        var reports = new List<RobotCompletedAction>();
        list.RegisterActionEndedCallback(reports.Add);
        var delivery = new List<string>();
        rig.Engine.ActionCompleted += _ => delivery.Add("completion");
        rig.Engine.RobotStatePublished += _ => delivery.Add("state");
        int mark = rig.Mark();
        var headTask = rig.Robot.Motion.SetHeadAngleAsync(0.4f);
        var head = list.GetCurrentAction()!;
        rig.Tick();
        var headWire = Assert.Single(rig.SentSince(mark).OfType<SetHeadAngle>());
        rig.Data(new MotorActionAck { ActionId = headWire.ActionId }); rig.Tick();
        delivery.Clear();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos, head: 0.4f);
        Assert.True((await headTask.WaitAsync(TimeSpan.FromSeconds(2))).Ok);
        Assert.Equal(new[] { "completion", "state" }, delivery);
        mark = rig.Mark();
        var liftTask = rig.Robot.Motion.SetLiftHeightAsync(60);
        var lift = list.GetCurrentAction()!;
        rig.Tick();
        var liftWire = Assert.Single(rig.SentSince(mark).OfType<SetLiftHeight>());
        rig.Data(new MotorActionAck { ActionId = liftWire.ActionId }); rig.Tick();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos,
            head: 0.4f, liftAngle: MathF.Asin(15f / 66f));
        Assert.True((await liftTask.WaitAsync(TimeSpan.FromSeconds(2))).Ok);
        foreach (var action in new[] { head, lift })
        {
            var report = Assert.Single(reports.Where(r => r.Tag == action.Tag));
            Assert.Equal((byte)6, report.CompletionVariant);
            Assert.Null(report.ObjectInteraction);
            Assert.Equal((byte)6, report.PackBody()[^1]);
        }
    }

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
