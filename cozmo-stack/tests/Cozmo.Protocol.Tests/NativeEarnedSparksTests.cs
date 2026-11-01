using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeEarnedSparksTests
{
    [Fact]
    public void ActualConfiguredOwnerRejectsUnknownHostPendingHistory()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var earned=Assert.IsType<NativeEarnedSparksBehavior>(f.Runtime.Bound["EarnedSparks"]);
        Assert.Same(earned,f.Robot.BehaviorManagerOwner!.Find("EarnedSparks"));
        var before=earned.EvaluateHostReadiness(f.Runtime.Context);Assert.True(before.Available);Assert.False(before.Runnable);
        f.Rig.Engine.Needs.SparksRewardPending=true;var unknown=earned.EvaluateHostReadiness(f.Runtime.Context);
        Assert.False(unknown.Available);Assert.False(unknown.Runnable);Assert.Null(earned.OwnedRunner);
    }
    [Fact]
    public void ActualConfiguredInterludeSelectsOwnerForSuppliedNativePending()
    {
        // Supplied source-state unit seam, not a natural reward-production claim.
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var earned=Assert.IsType<NativeEarnedSparksBehavior>(f.Runtime.Bound["EarnedSparks"]);
        var social=f.Runtime.FreeplayObserver.Freeplay.SubActivities.Single(a=>a.Id=="Socialize");
        var chooser=Assert.IsType<StrictPriorityChooser>(social.InterludeChooser);
        f.Rig.Engine.Needs.SupplyNativeRewardForTest(true,4,"A");
        var decision=chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        Assert.False(decision.InputUnavailable);Assert.Same(earned,decision.Behavior);Assert.Equal("EarnedSparks",Assert.Single(decision.Scores).Id);
    }
    [Fact]
    public void SuppliedNativePendingUsesLiftSafeAndStopCommunicatesBeforeCancel()
    {
        // Explicit native-state unit input; not a claim that the natural reward producer is built.
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var needs=f.Rig.Engine.Needs;needs.SupplyNativeRewardForTest(true,4,"A");
        f.Robot.CarryingOwner!.SetCarrying(7);
        var earned=Assert.IsType<NativeEarnedSparksBehavior>(f.Runtime.Bound["EarnedSparks"]);
        Assert.True(earned.EvaluateHostReadiness(f.Runtime.Context).Runnable);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(earned.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var action=Assert.IsType<TriggerLiftSafeAnimationAction>(earned.OwnedRunner);
        Assert.Equal((AnimationTrigger)0xA4,action.Request.Trigger);Assert.Equal(1,action.Request.Argument2);Assert.True(action.Request.Argument3);
        Assert.Equal(0,action.Request.Argument4);Assert.Equal(60f,action.Request.Argument5);Assert.False(action.Request.Argument6);
        bool delivered=false;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x65,true,p=>
        {
            var report=Assert.IsType<FreeplaySparksAwardedReport>(p);Assert.Equal(4,report.Amount);Assert.Equal("A",report.DisplayKey);
            Assert.False(needs.SparksRewardPending);Assert.Same(action,earned.OwnedRunner);delivered=true;
        });
        f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        Assert.True(delivered);Assert.False(needs.SparksRewardPending);Assert.Null(earned.OwnedRunner);
        using var scope=new BehaviorScope();earned.AttachRun(f.Runtime.Context,scope);Assert.True(earned.Resume(null,f.Rig.Engine.Timer.SecondsF));
    }
    [Fact]
    public void CommunicationMovesKeyBeforeLocalDeliveryAndNestedStateSurvives()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;needs.SupplyNativeRewardForTest(true,4,"A");
        var raw=new List<byte[]>();var typed=new List<FreeplaySparksAwardedReport>();int legacy=0;
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x65)raw.Add(body.ToArray());};
        needs.FreeplaySparksAwarded+=typed.Add;needs.SparksRewardAwarded+=()=>legacy++;
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x65,true,p=>
        {
            var before=needs.NativeRewardSnapshotForTest();Assert.False(before.Pending);Assert.Equal(4,before.Amount);Assert.Equal("",before.Key);
            needs.SupplyNativeRewardForTest(true,9,"B");
        });
        needs.CommunicateNativeReward(f.Robot);var after=needs.NativeRewardSnapshotForTest();
        Assert.True(after.Pending);Assert.Equal(9,after.Amount);Assert.Equal("B",after.Key);
        f.Rig.Engine.DrainAppDelivery();Assert.Equal(new byte[]{4,0,0,0,1,65},Assert.Single(raw));
        Assert.Equal(new FreeplaySparksAwardedReport(4,"A"),Assert.Single(typed));Assert.Equal(1,legacy);
    }
    [Theory]
    [InlineData(0u)] [InlineData(0x03000018u)]
    public void SuppliedTerminalResultCompletesCommonUpdateAndStopAcknowledges(uint result)
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var needs=f.Rig.Engine.Needs;needs.SupplyNativeRewardForTest(true,4,"A");
        var earned=Assert.IsType<NativeEarnedSparksBehavior>(f.Runtime.Bound["EarnedSparks"]);var manager=f.Robot.BehaviorManagerOwner!;
        Assert.True(manager.StartAsync(earned.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        int reports=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x65,true,_=>reports++);
        var action=Assert.IsAssignableFrom<ActionRunner>(earned.OwnedRunner);action.State=result;f.Robot.ActionList.Update();
        Assert.False(earned.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF*1000));Assert.Equal(0,reports);
        manager.Stop(BehaviorStopReason.Completed,f.Rig.Engine.Timer.SecondsF);Assert.Equal(1,reports);Assert.False(needs.SparksRewardPending);
    }
    [Fact]
    public void NoExternalInterfaceClearsPendingButDoesNotMoveKey()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;needs.SupplyNativeRewardForTest(true,4,"A");var owner=f.Robot;f.Rig.Engine.NeedsContextExternalInterfaceAvailable=false;
        int reports=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x65,true,_=>reports++);
        try {needs.CommunicateNativeReward(owner);var value=needs.NativeRewardSnapshotForTest();Assert.False(value.Pending);Assert.Equal(4,value.Amount);Assert.Equal("A",value.Key);Assert.Equal(0,reports);}
        finally {f.Rig.Engine.NeedsContextExternalInterfaceAvailable=true;}
    }
    [Fact]
    public void DirectCommunicationUsesLiveNeedsContextWithoutRobot()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;
        f.Rig.Engine.Robots.RemoveRobot(1,false);Assert.Null(f.Rig.Engine.Robot);
        needs.SupplyNativeRewardForTest(true,4,"A");FreeplaySparksAwardedReport? received=null;
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x65,true,p=>received=Assert.IsType<FreeplaySparksAwardedReport>(p));
        needs.SparksRewardCommunicatedToUser();Assert.Equal(new FreeplaySparksAwardedReport(4,"A"),received);
        var state=needs.NativeRewardSnapshotForTest();Assert.False(state.Pending);Assert.Equal("",state.Key);
    }
    [Theory]
    [InlineData(256,0)] [InlineData(257,1)]
    public void NativeStringPackingUsesLowLengthByte(int length,int packedLength)
    {
        // Supplied string boundary unit: 006C22F8..2304 narrows before copying.
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;needs.SupplyNativeRewardForTest(true,4,new string('A',length));
        var raw=new List<byte[]>();f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x65)raw.Add(body.ToArray());};
        needs.CommunicateNativeReward(f.Robot);f.Rig.Engine.DrainAppDelivery();var bytes=Assert.Single(raw);
        Assert.Equal(5+packedLength,bytes.Length);Assert.Equal((byte)packedLength,bytes[4]);
        if(packedLength==1)Assert.Equal((byte)65,bytes[5]);
    }
    [Fact]
    public void FalseStopSkipsCommunicationButDirectCommunicatorHasNoPendingGuard()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var needs=f.Rig.Engine.Needs;needs.SupplyNativeRewardForTest(false,4,"A");
        var earned=Assert.IsType<NativeEarnedSparksBehavior>(f.Runtime.Bound["EarnedSparks"]);int reports=0;
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x65,true,_=>reports++);
        // Direct start exercises source Init independently from the false own-predicate.
        using var scope=new BehaviorScope();earned.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        earned.Stop(BehaviorStopReason.Interrupted);Assert.Equal(0,reports);
        needs.CommunicateNativeReward(f.Robot);Assert.Equal(1,reports);Assert.Equal("",needs.NativeRewardSnapshotForTest().Key);
    }
    [Fact]
    public void ReplacementCannotReceiveLateOldActionOrReward()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var needs=f.Rig.Engine.Needs;needs.SupplyNativeRewardForTest(true,4,"A");var original=f.Robot;
        var earned=Assert.IsType<NativeEarnedSparksBehavior>(f.Runtime.Bound["EarnedSparks"]);using var scope=new BehaviorScope();
        earned.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();var action=Assert.IsAssignableFrom<ActionRunner>(earned.OwnedRunner);uint tag=action.Tag;
        f.Rig.Engine.Robots.RemoveRobot(1,false);var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
        var next=Assert.IsType<NativeEarnedSparksBehavior>(replacement.NativeBehaviorRuntime!.Bound["EarnedSparks"]);
        var baseline=needs.NativeRewardSnapshotForTest();int sent=f.Rig.Mark(),reports=0;
        using var report=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x65,true,_=>reports++);
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(tag,action.Type,0,Array.Empty<uint>(),0));
        Assert.Equal(baseline,needs.NativeRewardSnapshotForTest());Assert.Equal(0,reports);Assert.Null(next.OwnedRunner);Assert.False(next.EngineRunning);Assert.Equal(sent,f.Rig.Mark());
        Assert.NotSame(original,replacement);
    }
    [Fact]
    public void TriggerConstructionReentrantStopDoesNotAdmitStaleAction()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var needs=f.Rig.Engine.Needs;needs.SupplyNativeRewardForTest(true,4,"A");
        var earned=Assert.IsType<NativeEarnedSparksBehavior>(f.Runtime.Bound["EarnedSparks"]);int reports=0;bool stopped=false;
        using var report=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x65,true,_=>reports++);
        void OnMissing(string text){if(stopped || !text.Contains("M8-021 trigger DAS"))return;stopped=true;earned.Stop(BehaviorStopReason.Interrupted);}
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=OnMissing;
        try {using var scope=new BehaviorScope();earned.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();}
        finally {SteppedBehavior.MissingReported-=OnMissing;}
        Assert.True(stopped);Assert.Equal(1,reports);Assert.False(needs.SparksRewardPending);Assert.Null(earned.OwnedRunner);Assert.False(earned.EngineRunning);
    }
}
