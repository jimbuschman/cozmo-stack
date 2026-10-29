using System.Buffers.Binary;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class SparksPopAWheelieNativeTests
{
    private const string Id="SparksPopAWheelie";
    private static NativePopAWheelieBehavior Pop(Fixture f)=>Assert.IsType<NativePopAWheelieBehavior>(f.Runtime.Bound[Id]);
    private static Activity Activity(Fixture f)=>NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id==Id);
    private static void Admit(Fixture f,byte soft)
    {
        // Supplied admission/request/active inputs, not a recovered Freeplay parent winner.
        var manager=f.Robot.BehaviorManagerOwner!;manager.SparkNameInput=id=>id==13?"PopAWheelieAction":"Count";
        manager.SetNativeRequestedSpark(13,soft);f.Runtime.AdmitActivity(Id);manager.SwitchToNativeRequestedSpark();
    }
    [Fact]
    public void ExactConfigPreservesAbsentExecutableAndConsideredRunAndPutDownPriority()
    {
        // S1-S6: actual assets and native tables; no ordinary considered-run or Roll defaults.
        using var f=new Fixture();var pop=Pop(f);var activity=Activity(f);var owner=Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);
        Assert.Equal(152,(int)BehaviorID.SparksPopAWheelie);Assert.Equal(13u,pop.NativeBaseInputs!.RequiredUnlock);
        Assert.Equal((byte?)12,pop.NativeBaseInputs.CheckedExecutableType);Assert.Equal(41,(int)pop.BehaviorObjective);
        Assert.Equal(0f,owner.Minimum);Assert.Equal(60f,owner.Maximum);Assert.Equal((byte)1,owner.Repetitions);Assert.True(owner.CanExecute);
        Assert.Equal((AnimationTrigger)0x216,owner.Success);Assert.Equal((AnimationTrigger)0x210,owner.Failure);
        Assert.Equal(new[]{"DriveOffCharger","KnockOverCubes","SparksPutDownBlock",Id,"SparksLookInPlace"},activity.Chooser!.BehaviorIds);
        f.Add();Assert.False(pop.IsRunnable(f.Runtime.Context));f.Robot.Progression!.Unlocked.Add(13);Assert.True(pop.IsRunnable(f.Runtime.Context));
        f.Robot.CarryingOwner!.SetCarrying(7);Assert.False(pop.IsRunnable(f.Runtime.Context));
        var chosen=activity.Chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,0).Behavior;
        Assert.Equal("SparksPutDownBlock",chosen!.Id);Assert.Empty(f.Rig.SentSince(0).OfType<DockWithObject>());
    }
    [Theory]
    [InlineData((byte)0)] [InlineData((byte)1)]
    public void ActualOwnerAndFirmwareChildCelebrateThenObjectiveNeedsAndGetOut(byte soft)
    {
        // S/P/A: independent fixed physical scene and firmware pitch/results; no forced action state/count/winner.
        using var scene=new PopFirmwareScene(behaviorId:Id);var f=scene.F;var pop=Pop(f);var owner=Activity(f).SparkedOwner!;var manager=f.Robot.BehaviorManagerOwner!;
        var order=new List<string>();var callbackRunners=new List<(string Event,Type? Type,AnimationTrigger? Trigger)>();var local=new List<RobotCompletedAction>();var app=new List<ActionCompletionReport>();var raw=new List<byte[]>();
        var sparks=new List<(ushort Tag,byte[] Body)>();var triggers=new List<AnimationTrigger[]>();bool predock=false;bool interaction=false;
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>
        {
            if(p is not uint value||value!=22)return;
            order.Add("objective22");callbackRunners.Add(("objective22",pop.OwnedRunner?.GetType(),(pop.OwnedRunner as TriggerAnimationAction)?.Request.Trigger));
        });
        using var complete=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r&&r.ActionType==0x1D)local.Add(r);});
        f.Rig.Engine.ActionCompleted+=r=>{if(r.ActionType==0x1D)app.Add(r);};
        f.Rig.Engine.Needs.Log+=line=>{if(line.StartsWith("needs.action_completed PopAWheelie_Sparked:")){order.Add("needs34");callbackRunners.Add(("needs34",pop.OwnedRunner?.GetType(),(pop.OwnedRunner as TriggerAnimationAction)?.Request.Trigger));}};
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>
        {
            if(tag is 0x83 or 0x84)sparks.Add((tag,body.ToArray()));
            if(tag==0x5A&&body.Length>=8&&BinaryPrimitives.ReadInt32LittleEndian(body.Span[4..])==0x1D)raw.Add(body.ToArray());
        };
        Admit(f,soft);Assert.Equal((byte)24,f.Robot.BehaviorPublicState.State.Activity);manager.Update(f.Rig.Engine.Timer.SecondsF,0);
        for(int i=0;i<250&&!f.Runtime.AwaitingParentActivityPick;i++)
        {
            if(manager.Current is NativePlayArbitraryAnimBehavior arbitrary&&arbitrary.Triggers.Count!=0)
            {var current=arbitrary.Triggers.ToArray();if(triggers.Count==0||!triggers[^1].SequenceEqual(current))triggers.Add(current);}
            scene.Step();
            if(!interaction&&pop.Interaction is {} drive)
            {
                interaction=true;Assert.Equal(soft==0?0f:Radians.Pi*.5f,drive.MaximumFaceTurn);
                if(soft==0)Assert.Null(drive.Face);else{Assert.NotNull(drive.Face);Assert.True(drive.Face.SayName);}
            }
            if(!predock&&pop.CliffChanged)
            {
                predock=true;for(int t=0;t<21;t++)Assert.Equal(new[]{0,1,3,11,12,14,15,20}.Contains(t),manager.HasDisableLock((ReactionTrigger)t,Id+"_behaviorLock"));
                Assert.False(manager.HasDisableLock(ReactionTrigger.CliffDetected,"PopAWheelie_behaviorLock"));
            }
        }
        Directory.CreateDirectory(".scratch/sparks-pop-a-wheelie-1/artifacts");File.WriteAllLines($".scratch/sparks-pop-a-wheelie-1/artifacts/scene-soft{soft}.log",scene.Trace);
        Assert.True(f.Runtime.AwaitingParentActivityPick,string.Join(" | ",scene.Trace.TakeLast(25)));Assert.True(interaction);Assert.True(predock);
        Assert.Equal(new[]{"objective22","needs34"},order);Assert.Equal(new[]{"objective22","needs34"},callbackRunners.Select(r=>r.Event));
        Assert.All(callbackRunners,r=>{Assert.Equal(typeof(TriggerAnimationAction),r.Type);Assert.Equal((AnimationTrigger?)0x21C,r.Trigger);});Assert.Equal(1u,owner.Count);Assert.Equal(4,owner.State);Assert.Equal(1,scene.Rolls);
        Assert.Contains(triggers,t=>t.SequenceEqual(new[]{(AnimationTrigger)(soft==0?0x211:0x206)}));
        Assert.Contains(triggers,t=>t.SequenceEqual(soft==0?new[]{(AnimationTrigger)0x216,(AnimationTrigger)0x212}:new[]{(AnimationTrigger)0x23F}));
        var dock=Assert.Single(f.Rig.SentSince(0).OfType<DockWithObject>());Assert.Equal((byte)9,dock.DockAction);Assert.Empty(f.Rig.SentSince(0).OfType<PlaceObjectOnGround>());
        Assert.Contains(local,r=>r.Result==0&&r.ObjectInteraction is {Word0:var target,Count:1,Flag:false,IsPayloadDefined:false}&&target==scene.Target);
        f.Rig.Engine.DrainAppDelivery();Assert.Empty(app);Assert.Empty(raw); // same M1-034 undefined full-public guard; typed LOCAL survives.
        if(soft==0){Assert.Equal(new ushort[]{0x83,0x84},sparks.Select(s=>s.Tag));Assert.Equal(new byte[]{13,0,0,0},sparks[0].Body);Assert.Equal(new byte[]{1},sparks[1].Body);}else Assert.Empty(sparks);
        Assert.Equal(0x55,manager.RequestedSparkId);Assert.Equal(13,manager.ActiveSparkId);
        Assert.Contains(f.Rig.SentSince(0),m=>m is EnableStopOnCliff{Enable:false});Assert.Single(f.Rig.SentSince(0).OfType<EnableStopOnCliff>().Where(m=>m.Enable));
        // Null waits for parent; supplied changed selection alone owns selected activity cleanup.
        Assert.Equal(soft==0,f.Rig.Robot.Animations.Scheduler.IdleStack.Any(e=>e.Lock=="Activity_"+Id));
        f.Runtime.AdmitActivity("PutDownDispatch");Assert.False(f.Runtime.AwaitingParentActivityPick);
        Assert.DoesNotContain(f.Rig.Robot.Animations.Scheduler.IdleStack,e=>e.Lock=="Activity_"+Id);
        for(int t=0;t<21;t++)Assert.False(manager.HasDisableLock((ReactionTrigger)t,Id+"_behaviorLock"));
    }
    [Theory]
    [InlineData((byte)0,(byte)1,true,0x206)] [InlineData((byte)1,(byte)0,false,0x211)]
    public void RequestedSelectionAndActiveGetInUseSeparateNativeFields(byte requestedSoft,byte activeSoft,bool started,int getIn)
    {
        // S7/A2/A3 boundary: requested fields control selected outputs, active fields control get-in.
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;manager.SparkNameInput=id=>id==13?"PopAWheelieAction":"Count";
        manager.SetNativeRequestedSpark(13,activeSoft);manager.SwitchToNativeRequestedSpark();manager.SetNativeRequestedSpark(13,requestedSoft);
        var reports=new List<byte[]>();f.Rig.Engine.EngineToGameMessage+=(tag,b)=>{if(tag==0x83)reports.Add(b.ToArray());};
        f.Runtime.AdmitActivity(Id);f.Rig.Engine.DrainAppDelivery();Assert.Equal(started,reports.Count!=0);if(started)Assert.Equal(new byte[]{13,0,0,0},Assert.Single(reports));
        var chosen=Assert.IsType<NativePlayArbitraryAnimBehavior>(Activity(f).SparkedOwner!.Desired(null,f.Rig.Engine.Timer.SecondsF,f.Runtime.Context));
        Assert.Equal(new[]{(AnimationTrigger)getIn},chosen.Triggers);Assert.Equal(13,manager.ActiveSparkId);Assert.Equal(activeSoft,manager.ActiveSparkSoft);
    }
}
