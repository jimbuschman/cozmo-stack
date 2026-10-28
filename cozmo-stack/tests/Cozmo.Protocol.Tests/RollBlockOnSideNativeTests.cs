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
public sealed class RollBlockOnSideNativeTests
{
    private const string Id="RollBlockOnSide";
    private static NativeRollBlockBehavior Roll(Fixture f)=>Assert.IsType<NativeRollBlockBehavior>(f.Runtime.Bound[Id]);

    [Fact]
    public void ExactShippedFactoryUsesNativeIdentityDefaultAxisCacheAndActualUnlockGate()
    {
        // Source S1-S7: asset/parser005BB9EE, enum0076B32C, factory0059CF86,
        // ctor005C85DC/defaulttrue and cache005C8CAE. The ID's words do not choose orientation.
        using var f=new Fixture();var roll=Roll(f);
        Assert.Equal(99,(int)BehaviorID.RollBlockOnSide);Assert.Equal(49,(int)BehaviorClass.RollBlock);
        Assert.Equal(Id,roll.Id);Assert.NotSame(f.Runtime.Bound["RollBlockOnSideLowScore"],roll);
        Assert.NotSame(f.Runtime.Bound["Hiking_RollCube"],roll);Assert.True(roll.RotationImportant);
        Assert.Equal(new[]{7},roll.OwnSlot2C());Assert.Equal(9u,roll.NativeBaseInputs!.RequiredUnlock);
        Assert.Equal((byte?)12,roll.NativeBaseInputs.CheckedExecutableType);
        var obj=f.Add();f.Robot.Progression!.Unlocked.Add(9);
        Assert.False(roll.IsRunnable(f.Runtime.Context)); // ROOT +Z upright is excluded by cache7.
        obj.Pose=new Pose3d(Rotation3d.FromAngleAxis(Radians.Pi*.5f,new(0,1,0)),new(150,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        f.Rig.Tick();Assert.True(roll.IsRunnable(f.Runtime.Context));Assert.Equal(obj.ObjectId,roll.Target);
        f.Robot.Progression.Unlocked.Remove(9);Assert.False(roll.IsRunnable(f.Runtime.Context));
        Assert.Empty(f.Rig.SentSince(0).OfType<DockWithObject>());
    }

    [Fact]
    public void ExactConfiguredManagerRunsRealDriveDockVisualVerifyLocalCallbackObjectiveNeedsAndLights()
    {
        // Source S8-S16: actual helper/IDrive/IDock/visual recipients; success005C8B52..8C1A.
        // Explicit manager admission replaces the MISSING PlayAlone/Socialize choice/interlude (P1-P4).
        // Scene supplies independent physical cube and firmware inputs, never helper results or planner goals.
        using var scene=new RollFirmwareScene(behaviorId:Id);var f=scene.F;var manager=f.Robot.BehaviorManagerOwner!;
        var roll=Roll(f);var order=new List<string>();var local=new List<RobotCompletedAction>();var app=new List<ActionCompletionReport>();
        var lights=new List<string>();bool celebration=false;
        f.Rig.Robot.Lights.Cubes.BehaviorStateCallObservedForTest=(id,call,trigger,layer,modifier,_,offset)=>
        {Assert.Equal(scene.Target,id);Assert.Equal(1,layer);Assert.False(modifier);Assert.Equal(0,offset);lights.Add(call+":"+trigger);};
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint value&&value==2)order.Add("objective2");});
        using var completion=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r&&r.ActionType==0x1F)local.Add(r);});
        f.Rig.Engine.ActionCompleted+=r=>{if(r.ActionType==0x1F)app.Add(r);};
        f.Rig.Engine.Needs.Log+=line=>
        {
            if(!line.StartsWith("needs.action_completed RollACube:"))return;
            Assert.Equal(new[]{"objective2"},order);Assert.Equal(1,roll.State);
            var animation=Assert.IsType<TriggerAnimationAction>(roll.OwnedRunner);
            Assert.Equal((AnimationTrigger)0x1FB,animation.Request.Trigger);Assert.Equal(.8f,(float)roll.RunningScoreBonus);
            celebration=true;order.Add("needs39");
        };
        int mark=f.Rig.Mark();Assert.True(manager.StartAsync(Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Same(roll,manager.Current);Assert.False(roll.HardSpark);Assert.True(roll.RollHelper!.Upright);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x3FC90FDB),roll.RollHelper.MaxFaceTurn);
        Assert.Equal(new[]{"Play:InteractingBehaviorLock"},lights);
        Assert.Contains(f.Rig.SentSince(mark),m=>m is CubeID);Assert.Contains(f.Rig.SentSince(mark),m=>m is CubeLights);
        for(int i=0;i<200&&order.Count<2;i++)scene.Step();
        Assert.True(order.Count==2,string.Join(" | ",scene.Trace.TakeLast(30)));
        Assert.Equal(new[]{"objective2","needs39"},order);Assert.True(celebration);Assert.True(roll.PreDockReached);
        Assert.Equal(1,scene.Rolls);Assert.Equal(1u,roll.RollHelper.Attempts);
        var dock=Assert.Single(f.Rig.SentSince(mark).OfType<DockWithObject>());
        Assert.Equal((byte)5,dock.DockAction);Assert.Equal((byte)0,dock.DockingMethod);Assert.Equal((byte)0,dock.Field6);Assert.False(dock.Field8);
        Assert.Contains(local,r=>r.Result==0&&r.ObjectInteraction is {Word0:var target,Count:1,Flag:false,IsPayloadDefined:false}&&target==scene.Target);
        // Source S14/M1-034: defined local result survives; full-public undefined unused IDs are guarded.
        f.Rig.Engine.DrainAppDelivery();Assert.Empty(app);Assert.Single(manager.InspectStateLights().Entries);
    }

    [Fact]
    public void ExactConfiguredManagerStopRetiresOnlyOwnedWorkAndKeepsNativeLightRetention()
    {
        // S10/shared Stop005BD08C..126: own helper/action cleanup, target reset; S16 retains manager lights.
        using var scene=new RollFirmwareScene(behaviorId:Id);var f=scene.F;var manager=f.Robot.BehaviorManagerOwner!;
        Assert.True(manager.StartAsync(Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());var roll=Roll(f);
        var owned=Assert.IsAssignableFrom<ActionRunner>(roll.OwnedRunner);
        var unrelated=new NativeTimedWaitRunner(f.M,100f);f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,unrelated);f.Rig.Tick();
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Robot.ActionList.Watcher.Update();
        Assert.True(owned.DestructionObserved);Assert.False(unrelated.DestructionObserved);Assert.Equal(EngineActionResult.Running,unrelated.State);
        Assert.Equal(uint.MaxValue,roll.Target);Assert.False(roll.HasLiveHelper);Assert.Single(manager.InspectStateLights().Entries);
        f.Robot.ActionList.Clear();
    }
}
