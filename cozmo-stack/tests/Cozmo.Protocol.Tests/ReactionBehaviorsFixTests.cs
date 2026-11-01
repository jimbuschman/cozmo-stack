using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Protocol;
using System.Reflection;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class ReactionBehaviorsFixTests
{
    private static void SupplyAcceptedState(M4ControlTests.Rig rig)
    {
        rig.State();
        Assert.True(rig.Engine.Robot!.FirstFullStateHandled);
        Assert.NotNull(rig.Engine.Robot.AcceptedState);
    }
    private static string Resources()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string root = Path.Combine(dir.FullName, "re-analysis", "obb", "assets", "cozmo_resources");
            if (Directory.Exists(Path.Combine(root, "config", "engine", "behaviorSystem"))) return root;
        }
        throw new DirectoryNotFoundException("shipped behavior resources required by reaction test");
    }

    [Fact]
    public void ShakenActualClipLoopsWithinOneOwnedActionUntilStopped()
    {
        string resources = Resources();
        using var signals = SignalTestContext.Install();
        using var rig = new Rig(resources: resources);
        rig.State();
        Assert.True(rig.Robot.Engine.Robot!.FirstFullStateHandled);
        Assert.NotNull(rig.Robot.Engine.Robot.AcceptedState);
        rig.Robot.Animations.ManualTicking = true;
        rig.Robot.Animations.ClockMs = () => rig.Clock.NowMs;
        rig.Robot.Animations.LoadFrom(Path.Combine(resources, "assets"));
        Assert.NotNull(rig.Robot.Animations.DrivingTriggerMap);
        Assert.True(rig.Robot.Engine.Robot.TimeSynced && rig.Robot.Engine.Robot.ReadyToStream);
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = rig.Robot.Animations.DrivingTriggerMap! };
        var behavior = new ReactToRobotShakenBehavior();
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        var action = Assert.IsType<TriggerAnimationAction>(behavior.OwnedRunner);
        int bytes = 0, audio = 0; byte tag = 0; var starts = new List<byte>(); int ends = 0;
        for (int i = 0; i < 1000 && ends < 2; i++)
        {
            foreach (var message in rig.Pump(3))
            {
                byte wireTag = (byte)message.Id;
                if (wireTag is >= 0x90 and <= 0x9B || message is AudioSilence) bytes += message.ToBytes().Length;
                if (message is AudioSample or AudioSilence or EndOfAnimation) audio++;
                if (message is StartOfAnimation start)
                {
                    tag = start.ToBytes()[1]; starts.Add(tag);
                    rig.Send(new AnimationStarted { AnimId = tag });
                }
                if (message is EndOfAnimation) { ends++; rig.Send(new AnimationEnded { AnimId = tag }); }
            }
            rig.Send(new AnimationState { Timestamp = rig.T, NumAnimBytesPlayed = bytes, NumAudioFramesPlayed = audio, EnabledAnimTracks = 7, Tag = tag });
        }
        Assert.True(ends >= 2, "shipped shake clip did not exhaust twice");
        Assert.True(starts.Count >= 2);
        Assert.All(starts, value => Assert.Equal(starts[0], value));
        Assert.Same(action, behavior.OwnedRunner);
        Assert.True(rig.Robot.Animations.IsPlaying);
        behavior.Stop(BehaviorStopReason.Cancelled);
        Assert.Null(behavior.OwnedRunner);
    }

    [Theory]
    [InlineData(true, 0x21u)] [InlineData(false, 0x22u)]
    public void RightedStateReportsAtTheSpecificNativeBranch(bool back, uint expected)
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        SteppedBehavior behavior = back ? new ReactToRobotOnBackBehavior() : new ReactToRobotOnFaceBehavior();
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        var reports = new List<uint>();
        using var local = rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true, p => reports.Add(Assert.IsType<uint>(p)));
        var method = behavior.GetType().GetMethod(back ? "DelayThenFlipDown" : "CheckFlipSuccess", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(behavior, null);
        Assert.Equal(new[] { expected }, reports);
        behavior.Stop(BehaviorStopReason.Completed);
    }

    [Theory]
    [InlineData(0, 0x145)] [InlineData(1, 0x139)] [InlineData(2, 0x1A8)] [InlineData(3, 0x1A8)]
    public void SlopeReadsTheSelectedSevereNeed(int selected, int trigger)
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        if (selected != 3) rig.Engine.Needs.SevereNeeds.Set((NeedId)selected);
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToPlacedOnSlopeBehavior();
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Contains(behavior.Trace, s => s.StartsWith(((AnimationTrigger)trigger).ToString() + ":"));
        behavior.Stop(BehaviorStopReason.Cancelled);
    }

    // 0x00609148..0x0060916C clears selected severe needs except Count; 0x006091AA starts one loop-zero action.
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ShakenClearsSelectedNeedAndOwnsOneContinuousAction(int selected)
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var needs = rig.Engine.Needs.SevereNeeds;
        if (selected != 3) needs.Set((NeedId)selected);
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToRobotShakenBehavior();
        int before = rig.Engine.AnimationMessages.SubscriberCountForTest(RobotCompletedAction.UnionTag, true);
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Equal(3, needs.Selected);
        var action = Assert.IsType<TriggerAnimationAction>(rig.Engine.Robot!.ActionList.GetCurrentAction());
        Assert.Equal((int)AnimationTrigger.DizzyShakeLoop, (int)action.Request.Trigger);
        Assert.Equal(0, action.Request.Argument2);
        Assert.Same(action, behavior.OwnedRunner);
        behavior.Stop(BehaviorStopReason.Cancelled);
        Assert.Null(behavior.OwnedRunner);
        Assert.Equal(before, rig.Engine.AnimationMessages.SubscriberCountForTest(RobotCompletedAction.UnionTag, true));
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        behavior.Stop(BehaviorStopReason.Cancelled);
        Assert.Equal(before, rig.Engine.AnimationMessages.SubscriberCountForTest(RobotCompletedAction.UnionTag, true));
    }

    [Fact]
    public void ShakenFinishingReportsOnlyOnceTheActionIsIdle()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToRobotShakenBehavior();
        var reports = new List<uint>();
        using var local = rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true, p => reports.Add(Assert.IsType<uint>(p)));
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        typeof(ReactToRobotShakenBehavior).GetProperty("CurrentPhase")!.SetValue(behavior, ReactToRobotShakenBehavior.Phase.Finishing);
        Assert.True(behavior.Update(context, 0));
        Assert.Empty(reports);
        typeof(SteppedBehavior).GetMethod("StopActing", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)!.Invoke(behavior, null);
        Assert.False(behavior.Update(context, 1));
        Assert.Equal(new uint[] { 0x24 }, reports);
        behavior.Stop(BehaviorStopReason.Completed);
    }

    [Fact]
    public void ShakenReentrantClearDoesNotStartAStaleAction()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        rig.Engine.Needs.SevereNeeds.Set(NeedId.Play);
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToRobotShakenBehavior();
        bool restarted = false; IActionRunner? replacement = null;
        rig.Engine.LogEntry += entry =>
        {
            if (entry.Name != "SevereNeedsComponent.SevereNeedExpression.Clear" || restarted) return;
            restarted = true;
            behavior.Stop(BehaviorStopReason.Cancelled);
            behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
            replacement = behavior.OwnedRunner;
        };
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.True(restarted);
        Assert.NotNull(replacement);
        Assert.Same(replacement, behavior.OwnedRunner);
        behavior.Stop(BehaviorStopReason.Cancelled);
    }

    [Fact]
    public void ShakenObjectiveListenerCanRestartWithoutOldFinishEndingReplacement()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToRobotShakenBehavior();
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        typeof(SteppedBehavior).GetMethod("StopActing", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)!.Invoke(behavior, null);
        typeof(ReactToRobotShakenBehavior).GetProperty("CurrentPhase")!.SetValue(behavior, ReactToRobotShakenBehavior.Phase.Finishing);
        IActionRunner? replacement = null;
        using var local = rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true, _ =>
        {
            behavior.Stop(BehaviorStopReason.Cancelled);
            behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
            replacement = behavior.OwnedRunner;
        });
        Assert.True(behavior.Update(context, 0));
        Assert.NotNull(replacement);
        Assert.Same(replacement, behavior.OwnedRunner);
        behavior.Stop(BehaviorStopReason.Cancelled);
    }

    // 0x00606C90 returns true; 0x00606D18 stores disconnect reason 4 before tag53.
    [Fact]
    public void ChargerStartsOwnedLiftSafeActionAndCancelsOnlyOnTag84()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToOnChargerBehavior(timeTilSleepAnimationSec: 7, timeTilDisconnectionSec: 11);
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        var action = Assert.IsType<TriggerLiftSafeAnimationAction>(rig.Engine.Robot!.ActionList.GetCurrentAction());
        var handlerField = typeof(ReactToOnChargerBehavior).GetField("_cancelHandler", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var staleHandler = Assert.IsType<Action>(handlerField.GetValue(behavior));
        Assert.Equal((int)AnimationTrigger.PlacedOnCharger, (int)action.Request.Trigger);
        Assert.Equal(RobotDisconnectReason.SleepPlacedOnCharger, rig.Engine.Rcm.Reason);
        Assert.True(behavior.Update(context, 0));
        Assert.True(behavior.Update(context, 100000));
        behavior.StopOnNextActionComplete();
        rig.Engine.Robot.DeliverCancelIdleTimeout();
        Assert.False(behavior.Update(context, 100001));
        behavior.Stop(BehaviorStopReason.Completed);
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        staleHandler();
        Assert.True(behavior.Update(context, 100002));
        behavior.Stop(BehaviorStopReason.Cancelled);
    }

    [Fact]
    public void MotorCalibrationWaitRunsOnTheActionListAndExpiresWithoutATag()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        SupplyAcceptedState(rig);
        using var manipulation = new Cozmo.Robot.Manipulation.ManipulationSystem(rig.Robot, rig.Robot.Vision);
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToMotorCalibrationBehavior(manipulation);
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        var wait = Assert.IsType<Cozmo.Robot.Manipulation.NativeTimedWaitRunner>(behavior.OwnedRunner);
        Assert.Equal(5f, wait.Duration);
        rig.Tick(1);
        rig.Tick(4999);
        Assert.Same(wait, behavior.OwnedRunner);
        rig.Tick(2);
        Assert.Null(behavior.OwnedRunner);
        Assert.False(behavior.Completed);
        behavior.Stop(BehaviorStopReason.Completed);
    }

    [Fact]
    public void ChargerIdlePushCanBeStoppedReentrantlyWithoutLeavingTheIdle()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToOnChargerBehavior();
        using var scope = new BehaviorScope();
        bool stopped = false;
        rig.Engine.LogEntry += entry =>
        {
            if (entry.Name != "AnimationStreamer.PushIdleAnimation" || stopped) return;
            stopped = true;
            behavior.Stop(BehaviorStopReason.Cancelled);
            scope.Dispose();
        };
        behavior.StartAsync(context, scope, default).GetAwaiter().GetResult();
        Assert.True(stopped);
        Assert.DoesNotContain(rig.Robot.Animations.Scheduler.IdleStack, e => e.Lock == behavior.Id);
        Assert.Null(behavior.OwnedRunner);
    }

    [Fact]
    public void FrustrationPushesNativeTripletBeforeCountFailureAndRemovesOnStop()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToFrustrationBehavior("test-frustration", (AnimationTrigger)0x23F, "");
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.True(behavior.InitFailed);
        Assert.Contains(rig.Robot.Animations.DrivingAnimations.Entries, e => e.LockName == behavior.Id &&
            e.Animations == new DrivingAnimations((AnimationTrigger)0x94, (AnimationTrigger)0x90, (AnimationTrigger)0x8C));
        behavior.Stop(BehaviorStopReason.Cancelled);
        Assert.DoesNotContain(rig.Robot.Animations.DrivingAnimations.Entries, e => e.LockName == behavior.Id);
    }

    [Fact]
    public void FrustrationWithoutDriveReportsWhenAnimationCompletesEvenOnFailure()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        SupplyAcceptedState(rig);
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToFrustrationBehavior("test-frustration", AnimationTrigger.FrustratedByFailure, "");
        var localReports = new List<uint>(); var hostReports = new List<byte[]>();
        using var local = rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true, p => localReports.Add(Assert.IsType<uint>(p)));
        rig.Engine.EngineToGameMessage += (tag, p) => { if (tag == 0x8B) hostReports.Add(p.ToArray()); };
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        for (int i = 0; i < 8 && localReports.Count == 0; i++) rig.Tick(33);
        Assert.Equal(new uint[] { 0x1D }, localReports);
        rig.Engine.DrainAppDelivery();
        Assert.Equal(new byte[] { 0x1D, 0, 0, 0 }, Assert.Single(hostReports));
        behavior.Stop(BehaviorStopReason.Completed);
    }

    [Fact]
    public void FrustrationObjectiveListenerRestartSurvivesOldCallback()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        SupplyAcceptedState(rig);
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToFrustrationBehavior("test-frustration", AnimationTrigger.FrustratedByFailure, "");
        IActionRunner? replacement = null;
        using var local = rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true, _ =>
        {
            behavior.Stop(BehaviorStopReason.Cancelled);
            behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
            replacement = behavior.OwnedRunner;
        });
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        for (int i = 0; i < 8 && replacement is null; i++) rig.Tick(33);
        Assert.NotNull(replacement);
        Assert.Same(replacement, behavior.OwnedRunner);
        Assert.True(behavior.EngineRunning);
        behavior.Stop(BehaviorStopReason.Cancelled);
    }

    [Fact]
    public void FrustrationReentrantRestartPreservesReplacementDrivingEntry()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToFrustrationBehavior("test-frustration", (AnimationTrigger)0x23F, "");
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        var handler = rig.Robot.Animations.DrivingAnimations;
        handler.Init(0, 1, false, false);
        bool restarted = false;
        rig.Engine.LogEntry += entry =>
        {
            if (entry.Name != "DrivingAnimationHandler.PopDrivingAnimations" || restarted) return;
            restarted = true;
            behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        };
        behavior.Stop(BehaviorStopReason.Cancelled);
        Assert.True(restarted);
        Assert.Single(handler.Entries.Where(e => e.LockName == behavior.Id));
        behavior.Stop(BehaviorStopReason.Cancelled);
        Assert.DoesNotContain(handler.Entries, e => e.LockName == behavior.Id);
    }

    [Fact]
    public void FrustrationUsesContextGeneratorAndKeepsTheGoalParentAlive()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        SupplyAcceptedState(rig);
        using var manipulation = new Cozmo.Robot.Manipulation.ManipulationSystem(rig.Robot, rig.Robot.Vision);
        rig.Engine.Robot!.MainPose.Node!.Transform = new Cozmo.Robot.Vision.Transform3d(
            Cozmo.Robot.Vision.Rotation3d.FromAngleAxis(0.3f, new(1, 0, 0)), new(10, 20, 30));
        var words = new Queue<uint>(new uint[] { 0, 0x40000000, 0, 0x40000000, 0, 0xC0000000 });
        rig.Engine.ContextRandom.WordSourceForTest = words.Dequeue;
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = ReactToFrustrationBehavior.Major(manipulation);
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        for (int i = 0; i < 8 && behavior.DriveGoal is null; i++) rig.Tick(33);
        Assert.Empty(words);
        var goal = Assert.IsType<Cozmo.Robot.Vision.Pose3d>(behavior.DriveGoal);
        Assert.Equal(337.5f, goal.Translation.X);
        Cozmo.Robot.Vision.Pose3d parent = null!;
        Assert.True(goal.TryGetParent(ref parent));
        using (parent)
        {
            Assert.True(parent.HasParent);
            Assert.True(parent.Owners > 1);
            Assert.InRange(parent.Quaternion.AngleAroundZ, -1.84f, -1.82f);
        }
        Assert.IsType<Cozmo.Robot.Manipulation.DriveToPoseAction>(behavior.OwnedRunner);
        behavior.Stop(BehaviorStopReason.Cancelled);
        Assert.Null(behavior.DriveGoal);
        rig.Engine.ContextRandom.WordSourceForTest = null;
    }

    [Fact]
    public void CliffNoBackupReportsAfterFinishedLocallyAndInQueuedHostOrder()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new ReactToCliffBehavior(rig.Robot);
        var order = new List<string>(); var host = new List<string>();
        behavior.RobotCliffEventFinishedInternal += () => order.Add("finished");
        behavior.RobotCliffEventFinished += () => host.Add("finished");
        rig.Engine.EngineToGameMessage += (tag, _) => { if (tag == 0x8B) host.Add("objective"); };
        using var local = rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true, payload =>
        {
            Assert.Equal(0x1Cu, Assert.IsType<uint>(payload));
            order.Add("objective");
            Assert.Empty(host);
        });
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        behavior.StopOnNextActionComplete();
        Assert.False(rig.Robot.Sensors.CliffDetectedByEvent);
        var transition = typeof(ReactToCliffBehavior).GetMethod("TransitionToBackingUp", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(transition);
        transition!.Invoke(behavior, null);
        Assert.Equal(new[] { "finished", "objective" }, order);
        rig.Engine.DrainAppDelivery();
        Assert.Equal(new[] { "finished", "objective" }, host);
        behavior.Stop(BehaviorStopReason.Cancelled);
    }

    [Fact]
    public void CubeAbsenceAdmitsLiftSafeActionBeforeReporting()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new AcknowledgeCubeMovedBehavior { TargetObjectId = 7 };
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        var stop = typeof(SteppedBehavior).GetMethod("StopActing", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)!;
        stop.Invoke(behavior, null);
        int reports = 0;
        using var local = rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true, p =>
        {
            Assert.Equal(0x19u, Assert.IsType<uint>(p));
            var action = Assert.IsType<TriggerLiftSafeAnimationAction>(behavior.OwnedRunner);
            Assert.Equal((int)AnimationTrigger.CubeMovedUpset, (int)action.Request.Trigger);
            Assert.Equal(AcknowledgeCubeMovedBehavior.Phase.ReactingToBlockAbsence, behavior.CurrentPhase);
            reports++;
        });
        var absence = typeof(AcknowledgeCubeMovedBehavior).GetMethod("TransitionToReactingToBlockAbsence", BindingFlags.NonPublic | BindingFlags.Instance)!;
        absence.Invoke(behavior, null);
        Assert.Equal(1, reports);
        behavior.Stop(BehaviorStopReason.Cancelled);
        Assert.Null(behavior.TargetObjectId);
    }
}
