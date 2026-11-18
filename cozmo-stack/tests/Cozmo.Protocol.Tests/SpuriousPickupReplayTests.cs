using System.Net;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class SpuriousPickupReplayTests
{
    // Native mask at511F04 and immediate candidate/time branches512244..512258;
    // bit-cleared expectations were separately run through the shipped function.
    [Theory]
    [InlineData(0,false)] [InlineData(1,false)] [InlineData(2,false)] [InlineData(3,false)] [InlineData(4,false)]
    [InlineData(0,true)] [InlineData(1,true)] [InlineData(2,true)] [InlineData(3,true)] [InlineData(4,true)]
    public void RecordedStatesMatchNativeClassifierAndOnlyPickedUpBitCausesThesePairs(int index,bool clearPickedUp)
    {
        var window=SpuriousPickupInputs.Windows[index];
        var classifier=new OffTreadsClassifier{IsPhysical=true,HeadCalibrated=true};
        var changes=new List<OffTreadsState>();
        foreach(var packet in window.Packets)
        {
            var state=Assert.IsType<RobotState>(RobotMessage.Parse(Convert.FromHexString(packet.Hex)));
            if(clearPickedUp)state.Status&=~(uint)RobotStatusFlag.IsPickedUp;
            bool changed=classifier.Update(state,packet.NowMs);
            Assert.Equal(clearPickedUp?OffTreadsState.OnTreads:(OffTreadsState)packet.State,classifier.Current);
            Assert.Equal(clearPickedUp?OffTreadsState.OnTreads:(OffTreadsState)packet.Candidate,classifier.Candidate);
            if(!clearPickedUp)Assert.Equal(packet.CandidateTime,classifier.CandidateTimeMs);
            Assert.Equal(packet.FilteredYBits,BitConverter.SingleToUInt32Bits(classifier.FilteredAccel.Y));
            if(changed)
            {
                changes.Add(classifier.Current);
                Assert.Equal(classifier.Current==OffTreadsState.InAir,state.Has(RobotStatusFlag.IsPickedUp));
                // Both ordinary transitions backdate by250, not a250ms wait for a short firmware pulse.
                Assert.Equal(packet.NowMs,unchecked(classifier.CandidateTimeMs+250));
            }
        }
        Assert.Equal(clearPickedUp?[]:Enumerable.Range(0,window.Pairs).SelectMany(_=>new[]{OffTreadsState.InAir,OffTreadsState.OnTreads}).ToArray(),changes);
    }

    private sealed class Gate():ActionRunner(8,0)
    {
        protected override string ActionName=>"initial native reaction gate";
        public override bool CanInterrupt()=>false;
        public override uint CheckIfDone()=>EngineActionResult.Success;
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void RecordedIngressRunsShortPickupThenNativeQuietReturnWithoutInventingAClip(int index)
    {
        var window=SpuriousPickupInputs.Windows[index];
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true,bodyHwVersion:4);
        var rig=f.Rig;var native=f.Robot;var manager=native.BehaviorManagerOwner!;
        rig.Robot.SetEnableFreeplayActivity(false);rig.Tick(4000);manager.Activity=null;
        manager.Stop(BehaviorStopReason.Interrupted,rig.Engine.Timer.SecondsF);
        // Explicit isolated input boundary: leave Pickup/Returned strategies real and enabled.
        // Other high-level activities/reactions from the original world are not reconstructed.
        var disabled=Enumerable.Repeat(true,21).ToArray();
        disabled[(int)ReactionTrigger.RobotPickedUp]=false;disabled[(int)ReactionTrigger.ReturnedToTreads]=false;
        manager.DisableReactionsWithLock("spurious-picked-up-input",disabled,false);
        native.ActionList.QueueAction(QueueActionPosition.Now,new Gate());
        manager.CheckReactions(rig.Engine.Timer.Seconds);native.ActionList.Clear();
        var run=new M15CubeRun("","",IPAddress.Loopback,"",[],[],false,false){Personality=true};
        run.SubscribeBehaviorReports(rig.Engine);
        var debug=new List<string>();manager.Log+=debug.Add;
        var reactions=new List<ReactionTrigger>();manager.ReactionTriggered+=r=>reactions.Add(r.Trigger);
        var reports=new List<ActionCompletionReport>();rig.Engine.ActionCompleted+=reports.Add;
        var states=new List<OffTreadsState>();rig.Robot.Sensors.OffTreadsStateChangedInternal+=(_,to)=>states.Add(to);
        var gaps=new List<string>();SteppedBehavior.ResetMissingForTests();
        rig.Engine.StackLogLine+=line=>{if(line.Contains("MISSING",StringComparison.Ordinal))gaps.Add(line);};
        int mark=rig.Mark();uint elapsed=0;
        foreach(var batch in window.Packets.GroupBy(p=>p.NowMs))
        {
            foreach(var packet in batch)
            {
                var state=Assert.IsType<RobotState>(RobotMessage.Parse(Convert.FromHexString(packet.Hex)));
                state.PoseOriginId=native.CurrentOriginId;state.PoseFrameId=native.PoseFrameId;
                rig.Data(state);
            }
            rig.Tick(batch.Key-elapsed);elapsed=batch.Key;

        }
        rig.Engine.DrainAppDelivery();
        Assert.Equal(Enumerable.Range(0,window.Pairs).SelectMany(_=>new[]{OffTreadsState.InAir,OffTreadsState.OnTreads}),states);
        Assert.True(reactions.SequenceEqual(Enumerable.Range(0,window.Pairs).SelectMany(_=>new[]{ReactionTrigger.RobotPickedUp,ReactionTrigger.ReturnedToTreads})),string.Join(" | ",debug));
        Assert.NotEqual("ReactToPickup",manager.Current?.Id);Assert.NotEqual("ReactToReturnedToTreads",manager.Current?.Id);
        Assert.Equal(OffTreadsState.OnTreads,rig.Robot.Sensors.OffTreadsState);
        // Native pickup starts a0.5f wait60775E; each captured bit pulse ends earlier.
        // Return interrupts it. In frustration the next pickup also cancels the first
        // return wait; only the final0.5f return wait608500 completes quietly.
        Assert.Equal(window.Pairs*2-1,reports.Count(r=>r.ActionType==0x31&&r.Result==EngineActionResult.Cancelled));
        Assert.Equal(1,reports.Count(r=>r.ActionType==0x31&&r.Result==0));
        Assert.Empty(rig.SentSince(mark).OfType<StartOfAnimation>());
        Assert.Empty(rig.SentSince(mark).OfType<StartMotorCalibration>());
        foreach(var id in new[]{BehaviorID.ReactToPickup,BehaviorID.ReactToReturnedToTreads})
        {
            var transitions=run.BehaviorTransitions(id,0,double.MaxValue);
            Assert.Equal(window.Pairs,transitions.Count(e=>PersonalityHw.IsBehaviorStart(e,id)));
            Assert.Equal(window.Pairs,transitions.Count(e=>PersonalityHw.IsBehaviorStop(e,id)));
        }
        var unexpected=gaps.Where(g=>!SharedMarkers.Any(p=>g.StartsWith(p,StringComparison.Ordinal))).Distinct().ToArray();
        Assert.True(unexpected.Length==0,string.Join(Environment.NewLine,unexpected));
    }

    [Fact]
    public void ActualPickupAdmissionRejectsChargerPlatformAsItsNativeVtableDoes()
    {
        using var f=new Fixture(sourceNvEmpty:true,bodyHwVersion:4);
        var pickup=Assert.IsType<ReactToPickupBehavior>(Assert.Single(
            f.Robot.BehaviorManagerOwner!.Reactions,r=>r.Strategy.Trigger==ReactionTrigger.RobotPickedUp).Behavior);
        f.Rig.Robot.Sensors.SetOnChargerPlatform(false);
        Assert.True(pickup.IsRunnableBase(f.Runtime.Context));
        f.Rig.Robot.Sensors.SetOnChargerPlatform(true);
        Assert.False(pickup.IsRunnableBase(f.Runtime.Context)); //102C274 ->59EC12:return0.
        f.Rig.Robot.Sensors.SetOnChargerPlatform(false);
        Assert.True(pickup.IsRunnableBase(f.Runtime.Context));
    }

    private static readonly string[] SharedMarkers=
    [
        "MISSING: M11-077: RemoveOwner is built (0x008448B4..0x00844914)",
        "MISSING: M13-039 client needs cache +0x2C read at 0x005924C4",
        "MISSING: M13-039 client needs cache +0x30 read at 0x00592514",
        "MISSING: M13-039 client needs cache +0x34 read at 0x00592560",
        "MISSING: M8-015 Queue.Update RUNNING VizManager.SetText0x0053F65C",
        "MISSING: M8-015 Queue.Update terminal VizManager.SetText(empty)0x0053F696",
        "MISSING: M8-023/M8-027 ProtoHandle callable destruction0x0066350E",
    ];
}
