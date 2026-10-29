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
public sealed class SparksRollNativeTests
{
    private const string Id="SparksRollBlock";
    private static NativeRollBlockBehavior Roll(Fixture f)=>Assert.IsType<NativeRollBlockBehavior>(f.Runtime.Bound[Id]);
    private static Activity Activity(Fixture f)=>NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id==Id);
    private static void Admit(Fixture f,byte soft)
    {
        // Supplied host admission and active-spark transition; native Freeplay winner/input producers remain MISSING.
        var manager=f.Robot.BehaviorManagerOwner!;manager.SparkNameInput=id=>id==9?"RollCube":"Count";
        manager.SetNativeRequestedSpark(9,soft);f.Runtime.AdmitActivity(Id);manager.SwitchToNativeRequestedSpark();
    }
    [Fact]
    public void ExactFactoryOwnerAndCacheSixKeepUprightFallbackAndSidePreference()
    {
        // S1-S6: literal native tables/config:155,26,40,8,9,2; cache6 NoAxis, own-parent Z preference.
        using var f=new Fixture();var roll=Roll(f);var activity=Activity(f);var owner=Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);
        Assert.Equal(155,(int)BehaviorID.SparksRollBlock);Assert.False(roll.RotationImportant);Assert.Equal(new[]{6},roll.OwnSlot2C());
        Assert.Equal(9u,roll.NativeBaseInputs!.RequiredUnlock);Assert.Equal((byte?)8,roll.NativeBaseInputs.CheckedExecutableType);
        Assert.Equal(0f,owner.Minimum);Assert.Equal(80f,owner.Maximum);Assert.Equal((byte)1,owner.Repetitions);Assert.True(owner.CanExecute);
        Assert.Equal((AnimationTrigger)0x216,owner.Success);Assert.Equal((AnimationTrigger)0x210,owner.Failure);
        Assert.Equal(new[]{"DriveOffCharger","KnockOverCubes",Id,"SparksPutDownBlock","SparksLookInPlace"},activity.Chooser!.BehaviorIds);
        var upright=f.Add(7,150);f.Robot.Progression!.Unlocked.Add(9);
        Assert.True(roll.IsRunnable(f.Runtime.Context));Assert.Equal(7u,roll.Target);Assert.Empty(f.Runtime.PickupTargets!.GetValid(7));
        var side=f.Add(8,400);side.Pose=new Pose3d(Rotation3d.FromAngleAxis(Radians.Pi*.5f,new(0,1,0)),new(400,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        f.Rig.Tick();Assert.Equal(7u,f.Runtime.PickupTargets.GetBest(6)); // valid retained best wins before the picker.
        f.M.Whiteboard.SetFailedToUse(upright,(ObjectActionFailure)3);f.Rig.Tick();Assert.Equal(8u,f.Runtime.PickupTargets.GetBest(6));
        f.Robot.Progression.Unlocked.Remove(9);Assert.False(roll.IsRunnable(f.Runtime.Context));
        Assert.Empty(f.Rig.SentSince(0).OfType<DockWithObject>());
    }
    [Theory]
    [InlineData(6)] [InlineData(7)]
    public void RollFailureMemoryExpiresAtThirtySecondsRatherThanSixty(int intention)
    {
        // F1 native570692..6AC + wrappers56BC40/56BAB4: age45s SAME pose distinguishes time30 vs60.
        using var f=new Fixture();var obj=f.Add();obj.Pose=new Pose3d(Rotation3d.FromAngleAxis(Radians.Pi*.5f,new(0,1,0)),new(150,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        var cache=f.Runtime.PickupTargets!;Assert.Contains(obj.ObjectId,cache.GetValid(intention));
        f.M.Whiteboard.SetFailedToUse(obj,(ObjectActionFailure)3);f.Rig.Tick();Assert.DoesNotContain(obj.ObjectId,cache.GetValid(intention));
        f.Rig.Tick(45000);Assert.Contains(obj.ObjectId,cache.GetValid(intention));
    }
    [Theory]
    [InlineData(6)] [InlineData(7)]
    public void FreshFortyMillimeterDisplacementIsStillInsideSixtyMillimeterFailureRadius(int intention)
    {
        // F1: real failure snapshot then independent physical40mm pose change; old30mm literal would admit it.
        using var f=new Fixture();var obj=f.Add();var side=Rotation3d.FromAngleAxis(Radians.Pi*.5f,new(0,1,0));
        obj.Pose=new Pose3d(side,new(150,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));var cache=f.Runtime.PickupTargets!;
        Assert.Contains(obj.ObjectId,cache.GetValid(intention));f.M.Whiteboard.SetFailedToUse(obj,(ObjectActionFailure)3);
        obj.Pose=new Pose3d(side,new(190,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));f.Rig.Tick();
        Assert.DoesNotContain(obj.ObjectId,cache.GetValid(intention));
        obj.Pose=new Pose3d(side,new(220,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));f.Rig.Tick();Assert.Contains(obj.ObjectId,cache.GetValid(intention));
    }
    [Theory]
    [InlineData((byte)0)] [InlineData((byte)1)]
    public void ActualConfiguredOwnerRunsGetInRollWireVisualLocalCompletionNeedsAndGetOut(byte soft)
    {
        // S/R/A actual factory+owner+child+helper/Drive/IDock and retained visual verification.
        // Scene physics, firmware acknowledgements, host admission and explicit deselection are independent inputs.
        using var scene=new RollFirmwareScene(behaviorId:Id);var f=scene.F;var roll=Roll(f);var owner=Activity(f).SparkedOwner!;var manager=f.Robot.BehaviorManagerOwner!;
        var order=new List<string>();var local=new List<RobotCompletedAction>();var app=new List<ActionCompletionReport>();var raw=new List<byte[]>();var sparks=new List<(ushort Tag,byte[] Body)>();var triggers=new List<AnimationTrigger[]>();
        IActionRunner? needsRunner=null;int needsState=-1;float needsScore=0;
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint value&&value==2){Assert.Empty(order);order.Add("objective2");}});
        using var completion=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r&&r.ActionType==0x1F)local.Add(r);});
        f.Rig.Engine.ActionCompleted+=r=>{if(r.ActionType==0x1F)app.Add(r);};
        f.Rig.Engine.Needs.Log+=line=>
        {
            if(!line.StartsWith("needs.action_completed RollACube_Sparked:"))return;
            needsState=roll.State;needsScore=(float)roll.RunningScoreBonus;needsRunner=roll.OwnedRunner;
            order.Add("needs40");
        };
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>
        {
            if(tag is 0x83 or 0x84)sparks.Add((tag,body.ToArray()));
            if(tag==0x5A&&body.Length>=8&&BinaryPrimitives.ReadInt32LittleEndian(body.Span[4..])==0x1F)raw.Add(body.ToArray());
        };
        Admit(f,soft);Assert.Equal((byte)26,f.Robot.BehaviorPublicState.State.Activity);
        manager.Update(f.Rig.Engine.Timer.SecondsF,0); // expose the real get-in before the firmware worker can finish it in one Step.
        for(int i=0;i<220&&!f.Runtime.AwaitingParentActivityPick;i++)
        {
            if(manager.Current is NativePlayArbitraryAnimBehavior arbitrary&&arbitrary.Triggers.Count!=0)
            {var current=arbitrary.Triggers.ToArray();if(triggers.Count==0||!triggers[^1].SequenceEqual(current))triggers.Add(current);}
            scene.Step();
        }
        var directory=Path.Combine(".scratch","sparks-roll-build");Directory.CreateDirectory(directory);File.WriteAllLines(Path.Combine(directory,$"configured-soft{soft}.log"),scene.Trace);
        Assert.True(f.Runtime.AwaitingParentActivityPick,string.Join(" | ",scene.Trace.TakeLast(25)));
        // IncreaseScoreWhileActing005BF02C requires +84: hard has no new action after the helper, soft has1FB.
        Assert.Equal(new[]{"objective2","needs40"},order);Assert.Equal(1,needsState);Assert.Equal(soft==0?0f:.8f,needsScore);
        if(soft!=0)Assert.Equal((AnimationTrigger)0x1FB,Assert.IsType<TriggerAnimationAction>(needsRunner).Request.Trigger);
        else Assert.False(needsRunner is TriggerAnimationAction celebration&&celebration.Request.Trigger==(AnimationTrigger)0x1FB);
        Assert.Equal(1u,owner.Count);Assert.Equal(4,owner.State);Assert.Equal(1,scene.Rolls);
        Assert.Contains(triggers,t=>t.SequenceEqual(new[]{(AnimationTrigger)(soft==0?0x211:0x205)}));
        if(soft==0)Assert.Contains(triggers,t=>t.SequenceEqual(new[]{(AnimationTrigger)0x216,(AnimationTrigger)0x212}));else Assert.Contains(triggers,t=>t.SequenceEqual(new[]{(AnimationTrigger)0x23F}));
        var dock=Assert.Single(f.Rig.SentSince(0).OfType<DockWithObject>());Assert.Equal((byte)5,dock.DockAction);Assert.Equal((byte)0,dock.DockingMethod);Assert.False(dock.Field8);
        Assert.Contains(local,r=>r.Result==0&&r.ObjectInteraction is {Word0:var target,Count:1,Flag:false,IsPayloadDefined:false}&&target==scene.Target);
        f.Rig.Engine.DrainAppDelivery();Assert.Empty(app);Assert.Empty(raw); // Roll's M1-034 undefined full-public fields are withheld.
        if(soft==0){Assert.Equal(new ushort[]{0x83,0x84},sparks.Select(s=>s.Tag));Assert.Equal(new byte[]{9,0,0,0},sparks[0].Body);Assert.Equal(new byte[]{1},sparks[1].Body);}else Assert.Empty(sparks);
        Assert.Equal(0x55,manager.RequestedSparkId);Assert.Equal(9,manager.ActiveSparkId);
        // Null returns to the unbuilt parent picker; cleanup occurs ONLY on this supplied changed selection.
        f.Runtime.AdmitActivity("PutDownDispatch");Assert.False(f.Runtime.AwaitingParentActivityPick);
        Assert.DoesNotContain(f.Rig.Robot.Animations.Scheduler.IdleStack,e=>e.Lock=="Activity_"+Id);
    }
    [Theory]
    [InlineData((byte)0)] [InlineData((byte)1)]
    public void ConfiguredCarryingPathUsesCanonicalTurnGroundAndReevaluatesBeforeRoll(byte soft)
    {
        // R2/R4/PB: actual configured owner/child. Independent two-cube scene, motor/firmware and
        // upstream LOCAL44/43 observation boundary inputs; this recovery test does not claim image detection.
        // Rendered-image full Roll execution is covered separately above.
        using var f=new Fixture();var carried=f.Add(7,150);var target=f.Add(8,400);
        target.Pose=new Pose3d(Rotation3d.FromAngleAxis(Radians.Pi*.5f,new(0,1,0)),new(400,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        Assert.Equal(0u,f.M.Docking.SetObjectAsAttachedToLift(7,carried.Markers.First(m=>m.Face==BlockFace.Front).Code));
        f.Robot.Progression!.Unlocked.Add(9);f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);Admit(f,soft);var roll=Roll(f);
        var animations=new HashSet<byte>();int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        try
        {
            for(int i=0;i<40&&roll.RollHelper is null;i++)
            {
                f.Motors();byte tag=f.Rig.Robot.Animations.CurrentTag;
                if(tag!=0&&animations.Add(tag)){f.Rig.Data(new AnimationStarted{AnimId=tag});f.Rig.Data(new AnimationEnded{AnimId=tag});f.Rig.Tick();}
            }
            var helper=Assert.IsType<NativeRollBlockHelper>(roll.RollHelper);var place=Assert.IsType<NativePlaceBlockHelper>(helper.PlaceHelper);
            Assert.Equal(8u,helper.Target);Assert.True(words>=4);Assert.Equal(-MathF.PI/4,place.Angle);Assert.NotNull(place.Turn);
            f.Rig.Engine.ContextRandom.WordSourceForTest=null;bool sent=false;
            for(int i=0;i<80&&place.Status!=BehaviorStatus.Complete;i++)
            {
                f.Motors();
                if(!sent&&f.Rig.SentSince(0).OfType<PlaceObjectOnGround>().Any())
                {sent=true;f.Motors(RobotStatusFlag.IsPickingOrPlacing);f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=(byte)BlockStatus.BlockPlaced});f.Motors(RobotStatusFlag.IsPickingOrPlacing);f.Motors();}
                f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(1,2,1,7));
                f.Rig.Engine.AnimationMessages.Deliver(0x43,true,new ProcessedImageReport(f.Robot.StoredState!.Timestamp,new uint[]{1},0));f.Rig.Tick();
            }
            Assert.True(sent);Assert.Single(f.Rig.SentSince(0).OfType<PlaceObjectOnGround>());Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);Assert.Equal(BehaviorStatus.Complete,place.Status);
            Assert.True(place.Turn!.DestructionObserved);Assert.True(place.Placement!.DestructionObserved);
            f.Rig.Tick();Assert.DoesNotContain(place,f.Runtime.AI.Helpers.StackHelpers);Assert.True(helper.DriveHelper is not null||helper.Runner is not null);Assert.Equal(BehaviorStatus.Running,helper.Status);
        }
        finally{f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }
    [Fact]
    public void ConfiguredOwnerStopRetiresOnlyOwnedRollAndPreservesUnrelatedAction()
    {
        // R8 shared Stop boundary after actual get-in, no terminal overrides or fake winner.
        using var scene=new RollFirmwareScene(behaviorId:Id);var f=scene.F;Admit(f,0);var roll=Roll(f);
        for(int i=0;i<40&&roll.RollHelper is null;i++)scene.Step();var owned=Assert.IsAssignableFrom<ActionRunner>(roll.OwnedRunner);
        var unrelated=new NativeTimedWaitRunner(f.M,100f);f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,unrelated);f.Rig.Tick();
        f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Robot.ActionList.Watcher.Update();
        Assert.True(owned.DestructionObserved);Assert.False(unrelated.DestructionObserved);Assert.Equal(EngineActionResult.Running,unrelated.State);
        Assert.Equal(uint.MaxValue,roll.Target);Assert.False(roll.HasLiveHelper);Assert.Single(f.Robot.BehaviorManagerOwner.InspectStateLights().Entries);f.Robot.ActionList.Clear();
    }
    [Fact]
    public void CacheSixInitialChoicePrefersPickableSideOverNearUprightAndRollRetargetsThroughSix()
    {
        // S4/S5/R6: both independently supplied scene candidates exist BEFORE initial best selection.
        using var f=new Fixture();f.Add(7,120);var side=f.Add(8,400);
        side.Pose=new Pose3d(Rotation3d.FromAngleAxis(Radians.Pi*.5f,new(0,1,0)),new(400,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        Assert.Equal(8u,f.Runtime.PickupTargets!.GetBest(6));
        f.Robot.Progression!.Unlocked.Add(9);var manager=f.Robot.BehaviorManagerOwner!;
        // Explicit manager child admission for exact155; tests only retarget recipient, not parent winner.
        Assert.True(manager.StartAsync(Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());var roll=Roll(f);var first=roll.RollHelper;
        side.Pose=new Pose3d(Rotation3d.Identity,new(400,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        f.M.Whiteboard.SetFailedToUse(side,(ObjectActionFailure)3);f.Rig.Tick();
        Assert.Equal(7u,roll.Target);Assert.NotSame(first,roll.RollHelper);Assert.False(roll.PreDockReached);Assert.Equal(0,roll.State);
    }
    [Theory]
    [InlineData((byte)0,(byte)1,true,0x205)] [InlineData((byte)1,(byte)0,false,0x211)]
    public void RequestedSparkControlsSelectedOutputsWhileActiveSparkControlsGetIn(byte requestedSoft,byte activeSoft,bool started,int getIn)
    {
        // A2/A3 source-input boundary unit: requested+60/64 and active+5C/68 are distinct native fields.
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;manager.SparkNameInput=id=>id==9?"RollCube":"Count";
        manager.SetNativeRequestedSpark(9,activeSoft);manager.SwitchToNativeRequestedSpark();manager.SetNativeRequestedSpark(9,requestedSoft);
        var reports=new List<byte[]>();f.Rig.Engine.EngineToGameMessage+=(tag,b)=>{if(tag==0x83)reports.Add(b.ToArray());};
        f.Runtime.AdmitActivity(Id);f.Rig.Engine.DrainAppDelivery();Assert.Equal(started,reports.Count!=0);
        if(started)Assert.Equal(new byte[]{9,0,0,0},Assert.Single(reports));
        var owner=Activity(f).SparkedOwner!;var chosen=Assert.IsType<NativePlayArbitraryAnimBehavior>(owner.Desired(null,f.Rig.Engine.Timer.SecondsF,f.Runtime.Context));
        Assert.Equal(new[]{(AnimationTrigger)getIn},chosen.Triggers);Assert.Equal(9,manager.ActiveSparkId);Assert.Equal(activeSoft,manager.ActiveSparkSoft);
    }
}
