using System.Net;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed partial class ReturnedToTreadsReplayTests
{
    // Existing unresolved allocation/cache/debug boundaries remain visible in both the run and the handoff.
    private static readonly string[] RemainingSharedMarkers=
    [
        "MISSING: M11-077: RemoveOwner is built (0x008448B4..0x00844914)",
        "MISSING: M13-039 client needs cache +0x2C read at 0x005924C4",
        "MISSING: M13-039 client needs cache +0x30 read at 0x00592514",
        "MISSING: M13-039 client needs cache +0x34 read at 0x00592560",
        "MISSING: M8-015 Queue.Update RUNNING VizManager.SetText0x0053F65C",
        "MISSING: M8-015 Queue.Update terminal VizManager.SetText(empty)0x0053F696",
    ];
    private sealed class GateAction():ActionRunner(8,0)
    {
        protected override string ActionName=>"initial action for native reaction gate";
        public override bool CanInterrupt()=>false;
        public override uint CheckIfDone()=>0;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecordedSetdownRunsTheNativeQuietWaitAndDeliversBothReports(bool currentCapture)
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true);
        var rig=f.Rig;var native=f.Robot;var manager=native.BehaviorManagerOwner!;
        rig.Robot.SetEnableFreeplayActivity(false);rig.Tick(4000);
        manager.Activity=null;
        // Native C3 (0x005A3558..0x005A357A) remembers that an engine action has existed.
        native.ActionList.QueueAction(QueueActionPosition.Now,new GateAction());
        manager.CheckReactions(rig.Engine.Timer.Seconds);native.ActionList.Clear();
        var behavior=Assert.IsType<ReactToReturnedToTreadsBehavior>(f.Runtime.Bound["ReactToReturnedToTreads"]);
        Assert.Same(behavior,Assert.Single(manager.Reactions,r=>r.Strategy.Trigger==ReactionTrigger.ReturnedToTreads).Behavior);
        var run=new M15CubeRun("","",IPAddress.Loopback,"",[],[],false,false){Personality=true};
        run.SubscribeBehaviorReports(rig.Engine);
        var completed=new List<ActionCompletionReport>();rig.Engine.ActionCompleted+=completed.Add;
        var reactions=new List<ReactionTrigger>();manager.ReactionTriggered+=sw=>reactions.Add(sw.Trigger);
        SteppedBehavior.ResetMissingForTests(); // A previous fixture must not hide a once-per-process diagnostic.
        var gaps=new List<string>();rig.Engine.StackLogLine+=line=>{if(line.Contains("MISSING"))gaps.Add(line);};
        var packets=currentCapture?CurrentSetdown:OldPutdown;
        double first=packets[0].AtMs,elapsed=0;
        int setdownMark=-1;NativeTimedWaitRunner? wait=null;float waitStarted=0;
        foreach(var batch in packets.GroupBy(p=>Math.Ceiling((p.AtMs-first)/60)*60))
        {
            foreach(var packet in batch)
            {
                var message=RobotMessage.Parse(Convert.FromHexString(packet.Hex));
                if(message is RobotState state)
                {
                    // Fixture binding only: keep captured pitch, flags, IMU, cliff readings and firmware timestamp.
                    state.PoseOriginId=native.CurrentOriginId;state.PoseFrameId=native.PoseFrameId;
                }
                rig.Data(message);
            }
            rig.Tick(batch.Key-elapsed);elapsed=batch.Key;
            if(ReferenceEquals(manager.Current,behavior) && wait is null)
            {
                setdownMark=rig.Mark();wait=Assert.IsType<NativeTimedWaitRunner>(behavior.OwnedRunner);
                waitStarted=rig.Engine.Timer.SecondsF;
                Assert.Equal(0.5f,wait.Duration); // WaitAction(robot,0.5f), 0x00608510..0x00608524.
            }
        }
        rig.Engine.DrainAppDelivery();
        Assert.NotNull(wait);
        Assert.Equal(OffTreadsState.OnTreads,rig.Robot.Sensors.OffTreadsState);
        Assert.False(behavior.Recalibrated);Assert.NotSame(behavior,manager.Current);
        Assert.True(rig.Engine.Timer.SecondsF-waitStarted>0.5f);
        Assert.Contains(completed,r=>r.Tag==wait.Tag && r.Result==0);
        Assert.Contains(ReactionTrigger.ReturnedToTreads,reactions);
        Assert.Contains(ReactionTrigger.RobotPickedUp,reactions);
        Assert.DoesNotContain(ReactionTrigger.RobotFalling,reactions);
        // 0x006085C4..0x0060868E queues no animation/sound and, for these level samples, no calibration.
        Assert.Empty(rig.SentSince(setdownMark).OfType<StartOfAnimation>());
        Assert.Empty(rig.SentSince(setdownMark).OfType<StartMotorCalibration>());
        Assert.Collection(run.BehaviorTransitions(BehaviorID.ReactToReturnedToTreads,0,double.MaxValue),
            start=>Assert.True(PersonalityHw.IsBehaviorStart(start,BehaviorID.ReactToReturnedToTreads)),
            stop=>Assert.True(PersonalityHw.IsBehaviorStop(stop,BehaviorID.ReactToReturnedToTreads)));
        var remaining=gaps.Where(g=>!RemainingSharedMarkers.Any(prefix=>g.StartsWith(prefix,StringComparison.Ordinal))).Distinct().ToArray();
        Assert.True(remaining.Length==0,string.Join(Environment.NewLine,remaining));
    }

    [Fact]
    public async Task ConfiguredWaitCannotIssueOldCommandsAfterRobotReplacement()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        var native=f.Robot;var manager=native.BehaviorManagerOwner!;
        var behavior=Assert.IsType<ReactToReturnedToTreadsBehavior>(f.Runtime.Bound["ReactToReturnedToTreads"]);
        Assert.True(await manager.SwitchToBehaviorBase(behavior,f.Rig.Engine.Timer.SecondsF));
        var wait=Assert.IsType<NativeTimedWaitRunner>(behavior.OwnedRunner);f.Rig.Tick();
        f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);
        f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        int mark=f.Rig.Mark();f.Rig.Tick(800);
        Assert.True(wait.DestructionObserved);
        Assert.False(behavior.IsRunnable(f.Runtime.Context));
        Assert.Empty(f.Rig.SentSince(mark).OfType<StartMotorCalibration>());
        Assert.Empty(f.Rig.SentSince(mark).OfType<StartOfAnimation>());
    }

    [Fact]
    public void PutdownHarnessUsesEngineReportsAndExplainsTheQuietBranch()
    {
        var step=Assert.Single(PersonalityHw.SelectSteps("putdown"));
        Assert.Equal(BehaviorID.ReactToReturnedToTreads,step.Check!.Id);
        Assert.Null(step.Check.Missing);
        Assert.Contains("0x00608500",step.Check.Source);
        Assert.Contains("quietly",step.Instruction);
    }

    [Fact]
    public void NativeDelocalizationClearsTheMovementAccumulator()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        var detector=f.Rig.Robot.Sensors.UnexpectedMovement;
        detector.IsPhysical=true;detector.AnimationStateTag=0;
        var state=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos);
        state.LwheelSpeedMmps=-50;state.RwheelSpeedMmps=50;
        detector.Update(state);state.Timestamp+=30;detector.Update(state);
        Assert.Equal(2,detector.Count);Assert.NotEqual(0u,detector.StartTimestamp);
        Assert.Equal(-100,detector.SumLeft);Assert.Equal(100,detector.SumRight);
        f.Robot.Delocalize(false); // Actual Robot → MovementComponent tail, 0x00510D80 / 0x0063E2E4.
        Assert.Equal(0,detector.Count);Assert.Equal(0u,detector.StartTimestamp);
        Assert.Equal(0,detector.SumLeft);Assert.Equal(0,detector.SumRight);
    }

    [Theory]
    [InlineData(0x55,0,false)]
    [InlineData(0x41,1,false)]
    [InlineData(0x41,0,true)]
    [InlineData(0x41,2,true)] // Init 0x005BCCB8 XORs the stored byte with 1 before its truth test.
    public async Task ConfiguredInitReadsNativeSparkFields(int id,byte soft,bool disabled)
    {
        using var f=new Fixture(sourceNvEmpty:true);
        var manager=f.Robot.BehaviorManagerOwner!;
        manager.SetNativeRequestedSpark(id,soft);manager.SwitchToNativeRequestedSpark();
        var behavior=Assert.IsType<ReactToReturnedToTreadsBehavior>(f.Runtime.Bound["ReactToReturnedToTreads"]);
        Assert.True(await manager.SwitchToBehaviorBase(behavior,f.Rig.Engine.Timer.SecondsF));
        Assert.Equal(disabled,behavior.SparkDisabled);
        Assert.Equal(0x55,behavior.UnlockIdentifier!());
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
    }
}
