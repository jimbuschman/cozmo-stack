using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using System.Reflection;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeBehaviorEndRequestTests
{
    private sealed class PendingRunner:ActionRunner
    {
        internal PendingRunner():base(0x31,0){}
        protected override string ActionName=>"replacement pending";
        public override uint CheckIfDone()=>0x01000000;
        public override bool CanInterrupt()=>false;
    }
    private sealed class UnownedBehavior:IBehavior
    {
        public string Id=>"unit-unowned";public string Class=>"unit";public int Stops;
        public bool IsRunnable(BehaviorContext context)=>true;
        public double EvaluateScore(BehaviorContext context)=>1;
        public Task StartAsync(BehaviorContext context,BehaviorScope scope,CancellationToken cancel)=>Task.CompletedTask;
        public bool Update(BehaviorContext context,double nowMs)=>true;
        public void Stop(BehaviorStopReason reason)=>Stops++;
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EmptyRequestAlwaysReportsNativeWaitToWait(bool initialized)
    {
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);manager.Initialized=initialized;
        var raw=new List<byte[]>();var typed=new List<BehaviorTransitionReport>();
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x87)raw.Add(body.ToArray());};
        f.Rig.Engine.BehaviorTransition+=typed.Add;
        manager.RequestCurrentBehaviorEndImmediately("test");f.Rig.Engine.DrainAppDelivery();
        Assert.Null(manager.Current);Assert.Equal(new byte[]{0xB2,0xB2,0x39,0x0B,0x39,0x0B,0},Assert.Single(raw));
        var report=Assert.Single(typed);Assert.Equal((byte)0xB2,report.OldId);Assert.Equal((byte)0xB2,report.NewId);Assert.Equal("",report.DisplayKey);
    }
    [Fact]
    public void AbsentExternalInterfaceSkipsEmptyTransition()
    {
        using var f=new Fixture();var owner=f.Robot;var manager=owner.BehaviorManagerOwner!;
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);owner.HasExternalInterface=false;
        var reports=new List<BehaviorTransitionReport>();f.Rig.Engine.BehaviorTransition+=reports.Add;
        try {manager.RequestCurrentBehaviorEndImmediately("test");f.Rig.Engine.DrainAppDelivery();Assert.Empty(reports);Assert.Null(manager.Current);}
        finally {owner.HasExternalInterface=true;}
    }
    [Fact]
    public void AbsentExternalInterfaceSkipsReactionReportButClearsReactionState()
    {
        using var f=new Fixture();var owner=f.Robot;var manager=owner.BehaviorManagerOwner!;
        typeof(BehaviorManager).GetField("_currentReaction",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(manager,ReactionTrigger.CliffDetected);
        owner.HasExternalInterface=false;int reports=0,writes=0;var prior=manager.UpdateRobotPropertiesForReaction;
        manager.UpdateRobotPropertiesForReaction=value=>{writes++;prior?.Invoke(value);};
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x88,true,_=>reports++);
        try {manager.RequestCurrentBehaviorEndImmediately("test");Assert.Equal(0,reports);Assert.Equal(1,writes);Assert.Null(manager.CurrentReactionTrigger);}
        finally {owner.HasExternalInterface=true;}
    }
    [Fact]
    public void InactiveCurrentIsClearedWithoutStoppingItAgain()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var manager=f.Robot.BehaviorManagerOwner!;var wait=Assert.IsType<NativeWaitBehavior>(f.Runtime.Bound["Wait"]);
        Assert.True(manager.StartAsync(wait.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        wait.Stop(BehaviorStopReason.Interrupted);Assert.Same(wait,manager.Current);Assert.False(wait.EngineRunning);
        double runningClock=wait.RunningPenaltyClockSec;double? lastRun=f.Runtime.Context.Penalty!.LastRunSec(wait.Id);int starts=wait.ScoreIncreaseCount;BehaviorTransitionReport? observed=null;
        Assert.NotNull(lastRun);f.Rig.Engine.Timer.UpdateTime(100_000_000_000);Assert.NotEqual(lastRun.Value,(double)f.Rig.Engine.Timer.SecondsF);
        using var report=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x87,true,p=>observed=Assert.IsType<BehaviorTransitionReport>(p));
        manager.RequestCurrentBehaviorEndImmediately("test");
        Assert.Null(manager.Current);Assert.False(wait.EngineRunning);Assert.Equal(runningClock,wait.RunningPenaltyClockSec);Assert.Equal(lastRun,f.Runtime.Context.Penalty.LastRunSec(wait.Id));Assert.Equal(starts,wait.ScoreIncreaseCount);
        Assert.NotNull(observed);Assert.Equal((byte)0xB2,observed!.OldId);Assert.Same(wait,f.Runtime.Bound["Wait"]);
    }
    [Fact]
    public void UnownedBehaviorWithoutNativeRunningByteIsUnavailable()
    {
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;var unowned=new UnownedBehavior();manager.Add(unowned);
        Assert.True(manager.StartAsync(unowned.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());var missing=new List<string>();
        void Observe(string text)=>missing.Add(text);SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Observe;
        try {manager.RequestCurrentBehaviorEndImmediately("test");Assert.Same(unowned,manager.Current);Assert.Equal(0,unowned.Stops);Assert.Contains(missing,x=>x.Contains("running-byte owner unavailable"));}
        finally {SteppedBehavior.MissingReported-=Observe;}
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RemovingOrReplacingActualContainerEntryWithdrawsStrongRootProof(bool replace)
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var manager=f.Robot.BehaviorManagerOwner!;var wait=Assert.IsType<NativeWaitBehavior>(f.Runtime.Bound["Wait"]);
        Assert.True(manager.StartAsync(wait.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var entries=Assert.IsAssignableFrom<IDictionary<BehaviorID,IBehavior>>(f.Runtime.Container.Behaviors);
        if(replace)entries[BehaviorID.Wait]=f.Runtime.Bound["DriveOffCharger"];
        else Assert.True(entries.Remove(BehaviorID.Wait));
        var missing=new List<string>();void Observe(string text)=>missing.Add(text);
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Observe;
        try
        {
            manager.RequestCurrentBehaviorEndImmediately("test");
            Assert.Contains(missing,text=>text.Contains("M8-016/M8-027") && text.Contains("strong-root"));
            Assert.Null(manager.Current);Assert.Same(wait,f.Runtime.Bound["Wait"]); // copied lookup is not a root proof
        }
        finally {SteppedBehavior.MissingReported-=Observe;entries[BehaviorID.Wait]=wait;}
    }
    [Fact]
    public void ReactionCallbackRemovingContainerEntryWithdrawsSnapshotProof()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var manager=f.Robot.BehaviorManagerOwner!;var wait=Assert.IsType<NativeWaitBehavior>(f.Runtime.Bound["Wait"]);
        Assert.True(manager.StartAsync(wait.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        typeof(BehaviorManager).GetField("_currentReaction",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(manager,ReactionTrigger.CliffDetected);
        var entries=Assert.IsAssignableFrom<IDictionary<BehaviorID,IBehavior>>(f.Runtime.Container.Behaviors);var missing=new List<string>();
        void Observe(string text)=>missing.Add(text);SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Observe;
        using var report=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x88,true,_=>entries.Remove(BehaviorID.Wait));
        try {manager.RequestCurrentBehaviorEndImmediately("test");Assert.Contains(missing,x=>x.Contains("strong-root"));Assert.Null(manager.Current);}
        finally {SteppedBehavior.MissingReported-=Observe;entries[BehaviorID.Wait]=wait;}
    }
    [Fact]
    public void RobotStoppedEndsActualConfiguredDriveBeforeOuterBroadcast()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        f.Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger|RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId);
        var owner=f.Robot;var manager=owner.BehaviorManagerOwner!;var drive=Assert.IsType<DriveOffChargerBehavior>(f.Runtime.Bound["DriveOffCharger"]);
        Assert.True(manager.StartAsync(drive.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());Assert.True(drive.EngineRunning);Assert.NotNull(drive.OwnedRunner);
        int paths=f.Rig.SentSince(0).OfType<AppendPathSegmentLine>().Count();var order=new List<string>();BehaviorTransitionReport? transition=null;var raw=new List<byte[]>();
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x87)raw.Add(body.ToArray());};
        using var report=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x87,true,p=>{transition=Assert.IsType<BehaviorTransitionReport>(p);order.Add("87");});
        void Stopped(){order.Add("stopped");Assert.Null(manager.Current);Assert.False(drive.EngineRunning);Assert.Null(drive.OwnedRunner);Assert.Equal(paths,f.Rig.SentSince(0).OfType<AppendPathSegmentLine>().Count());}
        f.Rig.Robot.Sensors.RobotStoppedInternal+=Stopped;
        try {f.Rig.Data(new RobotStopped{Field0=1});f.Rig.Tick();}
        finally {f.Rig.Robot.Sensors.RobotStoppedInternal-=Stopped;}
        Assert.Equal(new[]{"87","stopped"},order);Assert.NotNull(transition);
        Assert.Equal((byte)0x0A,transition!.OldId);Assert.Equal((byte)0xB2,transition.NewId);Assert.Equal((byte)0x0B,transition.OldClass);Assert.Equal((byte)0x0C,transition.OldExecutable);
        Assert.Contains(raw,bytes=>bytes.SequenceEqual(new byte[]{0x0A,0xB2,0x0B,0x0C,0x39,0x0B,0}));
    }
    [Fact]
    public void StopStampsRepetitionBeforeOwnedActionCancellationReport()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        f.Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger|RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId);
        var manager=f.Robot.BehaviorManagerOwner!;var drive=Assert.IsType<DriveOffChargerBehavior>(f.Runtime.Bound["DriveOffCharger"]);
        Assert.True(manager.StartAsync(drive.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());uint tag=drive.CurrentRunnerTag;
        f.Rig.Engine.Timer.UpdateTime(9_000_000_000);double? observed=null;
        using var completion=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction a && a.Tag==tag)observed=f.Runtime.Context.Penalty!.LastRunSec(drive.Id);});
        manager.RequestCurrentBehaviorEndImmediately("test");Assert.Equal(9d,observed);
    }
    [Fact]
    public void StopWithoutPenaltySamplesSuppressionAfterCancellationCallback()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        f.Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger|RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId);
        var manager=f.Robot.BehaviorManagerOwner!;var drive=Assert.IsType<DriveOffChargerBehavior>(f.Runtime.Bound["DriveOffCharger"]);
        Assert.True(manager.StartAsync(drive.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());uint tag=drive.CurrentRunnerTag;bool delivered=false;
        f.Rig.Engine.Timer.UpdateTime(9_000_000_000);
        using var completion=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction a && a.Tag==tag){delivered=true;f.Rig.Engine.Timer.UpdateTime(10_000_000_000);}});
        drive.StopWithoutImmediateRepetitionPenalty();
        Assert.True(delivered);Assert.Equal(9d,f.Runtime.Context.Penalty!.LastRunSec(drive.Id));
        Assert.True(f.Runtime.Context.Penalty.IsSuppressed(drive.Id,10.5));Assert.False(f.Runtime.Context.Penalty.IsSuppressed(drive.Id,11));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReactionReportPrecedesReloadAndNullRequestRetainsLights(bool clearTriggerInCallback)
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var manager=f.Robot.BehaviorManagerOwner!;var wait=Assert.IsType<NativeWaitBehavior>(f.Runtime.Bound["Wait"]);
        Assert.True(manager.StartAsync(wait.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var trigger=typeof(BehaviorManager).GetField("_currentReaction",flags)!;var resume=typeof(BehaviorManager).GetField("_resumeAfterReaction",flags)!;
        trigger.SetValue(manager,ReactionTrigger.CliffDetected);resume.SetValue(manager,wait); // supplied running-info unit, same actual configured owner
        manager.SetBehaviorStateLights(BehaviorClass.RollBlock,new[]{new BehaviorStateLightInfo(77,NativeCubeLightTriggers.FromName("Flash"),false,BehaviorStateLightInfo.Zero())},false);
        int writes=0;var prior=manager.UpdateRobotPropertiesForReaction;manager.UpdateRobotPropertiesForReaction=value=>{writes++;prior?.Invoke(value);};
        var order=new List<string>();
        using var transition=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x88,true,p=>
        {
            var value=Assert.IsType<ReactionTransitionReport>(p);Assert.Equal((byte)ReactionTrigger.CliffDetected,value.OldTrigger);Assert.Equal((byte)0x16,value.NewTrigger);
            Assert.Null(manager.Current);Assert.Equal(ReactionTrigger.CliffDetected,manager.CurrentReactionTrigger);Assert.Same(wait,resume.GetValue(manager));order.Add("88");
            if(clearTriggerInCallback)trigger.SetValue(manager,null);
        });
        using var report=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x87,true,_=>order.Add("87"));
        manager.RequestCurrentBehaviorEndImmediately("test");
        Assert.Equal(new[]{"88","87"},order);Assert.Equal(clearTriggerInCallback?0:1,writes);
        Assert.Null(manager.Current);Assert.Null(manager.CurrentReactionTrigger);Assert.Null(resume.GetValue(manager));
        Assert.Single(manager.InspectStateLights().Entries);Assert.Same(wait,f.Runtime.Bound["Wait"]);
    }
    [Fact]
    public void SameOwnerStopCallbackReloadsAndClearsNewCurrent()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        f.Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger|RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId);
        var manager=f.Robot.BehaviorManagerOwner!;var drive=Assert.IsType<DriveOffChargerBehavior>(f.Runtime.Bound["DriveOffCharger"]);
        var wait=Assert.IsType<NativeWaitBehavior>(f.Runtime.Bound["Wait"]);
        Assert.True(manager.StartAsync(drive.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());bool nested=false;
        void OnLog(string line){if(nested || !line.StartsWith("BehaviorManager.RemoveDisableReactionsLock:"))return;nested=true;manager.StartAsync(wait.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult();}
        manager.Log+=OnLog;
        try {manager.RequestCurrentBehaviorEndImmediately("test");}
        finally {manager.Log-=OnLog;}
        Assert.True(nested);Assert.Null(manager.Current);Assert.True(wait.EngineRunning);Assert.Same(wait,f.Runtime.Bound["Wait"]);
    }
    [Fact]
    public void ReplacementDuringReportDoesNotCancelReplacementAction()
    {
        using var f=new Fixture();var old=f.Robot;var manager=old.BehaviorManagerOwner!;
        var pending=new PendingRunner();EngineRobot? replacement=null;
        using var report=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x87,true,_=>
        {
            if(replacement is not null)return;
            f.Rig.Engine.Robots.RemoveRobot(1,false);replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
            replacement.ActionList.QueueAction(QueueActionPosition.AtEnd,pending);
        });
        f.Rig.Data(new RobotStopped{Field0=1});f.Rig.Tick();
        Assert.NotNull(replacement);Assert.Same(replacement,f.Rig.Engine.Robot);
        Assert.Same(pending,replacement!.ActionList.GetCurrentAction());
    }
    [Fact]
    public void ReplacementFromRequestInfoCannotMutateNewManager()
    {
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;EngineRobot? replacement=null;IBehavior? current=null;int mark=-1;
        void OnLog(EngineLogEntry entry)
        {
            if(replacement is not null || entry.Name!="BehaviorManager.RequestCurrentBehaviorEndImmediately")return;
            f.Rig.Engine.Robots.RemoveRobot(1,false);replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
            current=replacement.BehaviorManagerOwner!.Current;mark=f.Rig.Mark();
        }
        f.Rig.Engine.LogEntry+=OnLog;
        try {manager.RequestCurrentBehaviorEndImmediately("test");}
        finally {f.Rig.Engine.LogEntry-=OnLog;}
        Assert.NotNull(replacement);Assert.Same(current,replacement!.BehaviorManagerOwner!.Current);Assert.Equal(mark,f.Rig.Mark());
    }
}
