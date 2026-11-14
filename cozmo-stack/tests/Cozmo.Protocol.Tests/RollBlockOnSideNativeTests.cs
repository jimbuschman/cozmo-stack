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
        // Scene supplies cube geometry and simulated firmware; rendered images use the nonphysical marker branch.
        using var scene=new RollFirmwareScene(behaviorId:Id);var f=scene.F;var manager=f.Robot.BehaviorManagerOwner!;
        var roll=Roll(f);var order=new List<string>();var local=new List<RobotCompletedAction>();var app=new List<ActionCompletionReport>();
        var lights=new List<string>();var stateTrace=new List<string>();bool celebration=false;
        void Capture(string stage){var state=manager.InspectStateLights();stateTrace.Add($"{stage}:current={manager.Current?.Id} running={roll.EngineRunning} class={state.Class} entries={state.Entries.Count} runner={roll.OwnedRunner?.GetType().Name}");}
        f.Rig.Robot.Lights.Cubes.BehaviorStateCallObservedForTest=(id,call,trigger,layer,modifier,_,offset)=>
        {Assert.Equal(scene.Target,id);Assert.Equal(1,layer);Assert.False(modifier);Assert.Equal(0,offset);lights.Add(call+":"+trigger);};
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint value&&value==2)order.Add("objective2");});
        using var completion=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r&&r.ActionType==0x1F)local.Add(r);});
        f.Rig.Engine.ActionCompleted+=r=>{if(r.ActionType==0x1F)app.Add(r);};
        f.Rig.Engine.Needs.Log+=line=>
        {
            if(!line.StartsWith("needs.action_completed RollACube:"))return;
            Capture("needs callback");
            Assert.Same(roll,manager.Current);Assert.True(roll.EngineRunning);Assert.Single(manager.InspectStateLights().Entries);
            Assert.Equal(new[]{"objective2"},order);Assert.Equal(1,roll.State);
            var animation=Assert.IsType<TriggerAnimationAction>(roll.OwnedRunner);
            Assert.Equal((AnimationTrigger)0x1FB,animation.Request.Trigger);Assert.Equal(.8f,(float)roll.RunningScoreBonus);
            celebration=true;order.Add("needs39");
        };
        int mark=f.Rig.Mark();Assert.True(manager.StartAsync(Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());QueuedSelection.Keep(f.Rig.Robot,manager,Id);
        Assert.Same(roll,manager.Current);Assert.False(roll.HardSpark);Assert.True(roll.RollHelper!.Upright);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x3FC90FDB),roll.RollHelper.MaxFaceTurn);
        Assert.Equal(new[]{"Play:InteractingBehaviorLock"},lights);
        Assert.Contains(f.Rig.SentSince(mark),m=>m is CubeID);Assert.Contains(f.Rig.SentSince(mark),m=>m is CubeLights);
        for(int i=0;i<200&&order.Count<2;i++){scene.Step();Capture("step"+i);}
        Assert.True(order.Count==2,string.Join(" | ",scene.Trace.TakeLast(30)));
        Assert.Equal(new[]{"objective2","needs39"},order);Assert.True(celebration);Assert.True(roll.PreDockReached);
        Assert.Equal(1,scene.Rolls);Assert.Equal(1u,roll.RollHelper.Attempts);
        var dock=Assert.Single(f.Rig.SentSince(mark).OfType<DockWithObject>());
        Assert.Equal((byte)5,dock.DockAction);Assert.Equal((byte)0,dock.DockingMethod);Assert.Equal((byte)0,dock.Field6);Assert.False(dock.Field8);
        Assert.Contains(local,r=>r.Result==0&&r.ObjectInteraction is {Word0:var target,Count:1,Flag:false,IsPayloadDefined:false}&&target==scene.Target);
        // Source S14/M1-034: defined local result survives; full-public undefined unused IDs are guarded.
        f.Rig.Engine.DrainAppDelivery();Assert.Empty(app);Capture("final");
        Assert.Same(f.Runtime.Bound["Wait"],manager.Current);var finalLights=manager.InspectStateLights();
        Assert.Empty(finalLights.Entries);Assert.Equal((byte)BehaviorClass.RollBlock,finalLights.Class); // M7-030 0x005A223E..226E
    }

    // M12-036 (CHARGER-BOUNDING-QUAD-1 GetObstacles G6-G11): a located Charger_Basic inside GetObstacles' Z band is an obstacle entry built through its own
    // class slots, and the native roll drive accepts it and reaches DockWithObject.
    // Engine: the predicate 0x0062C1D4 takes the size from slot +0x2C (0x0062C1F0; Charger 0x004EA672 -> +0xF0 = (96, 80, 31), stored 0x004E9D12..0x004E9D32),
    // rz = (own-pose rotation * size).z (0x0062C1FC), the centre z re-rooted (0x0062C200..0x0062C216), the exclusion test 0x0062C220..0x0062C27E, then the
    // quad from slot +0x50 at the own pose (0x0062C280..0x0062C294; base 0x0087713A, corners 0x004E9A50).
    // Hand derivation for the charger placed below, identity rotation at (-300, 0, 0) in the current origin:
    //   rz = (I * (96, 80, 31)).z = 31; centre z = 0; extent [0 - 15.5, 0 + 15.5] = [-15.5, 15.5].
    //   minZ = the robot's re-rooted z (G2), about 0: the scene's states carry no z (measured during the drive: 0 and -3.8146973E-06 from the re-rooting);
    //   maxZ = minZ + GetHeight >= 67.7 (G3, floor 0x42876666 at 0x00516F0C).
    //   Excluded only if (top <= minZ and bottom <= minZ) or (top >= maxZ and bottom >= maxZ): with any minZ in (-15.5, 15.5), 15.5 <= minZ is false and
    //   -15.5 >= maxZ is false -> INCLUDED.
    //   Quad (corners 0x004E9A50 translated by the pose x, y, 0x004E68CC): x in [-300, -204], y in [-40, 40], behind the robot (start x = 0) and clear of
    //   the cube at x = 150, so no rolling pre-action pose's swept footprint reaches it and the drive is unchanged.
    // So the charger is in the list ManipulationSystem.NativeObstacles hands the drive; a stand-in entry there throws NativeActionUnavailable
    // (ManipulationSystem.cs, M12-036/M13-025), so the dock is sent only when the charger's per-class quad was built. Negative controls (run by hand,
    // ROLL-MARKER-OBSERVED-1, then reverted): (1) forcing the charger's PreActionObstacle.For quad to the stand-in made this test fail with no
    // DockWithObject; (2) with that stand-in kept and the charger moved to z = -50 (extent [-65.5, -34.5], both ends <= minZ -> EXCLUDED) the test
    // passed and For was never called for the charger, as the arithmetic predicts. (z = -15.5 is not a usable control: top = 0 > minZ = -3.8E-06.)
    // The list's contents are not observed directly (no stack observable; see M12-036 unresolved). It shows only that, when the charger is included,
    // its slot +0x50 quad is built and accepted; inclusion itself rests on the hand derivation (a wrong exclusion would also pass, control (2)).
    // reach: unit (substituted inputs below); production-form ingress (ImageImuData + CameraGrayFrame through RollFirmwareScene, the engine tick, the configured native behaviour), with the
    // scene's substitutions: explicit manager admission (the PlayAlone/Socialize choice is MISSING), the cube and the charger seeded by AddLocatedObject
    // (instead of the marker observation path), and simulated firmware {"sim":true}.
    [Fact]
    public void M12_036_ALocatedChargerInsideTheObstacleBandIsAcceptedByTheNativeRollDrive()
    {
        using var scene=new RollFirmwareScene(behaviorId:Id);var f=scene.F;var manager=f.Robot.BehaviorManagerOwner!;
        var charger=new ObservableObject(9,ObjectType.Charger_Basic,Array.Empty<KnownMarker>())
        {Pose=new Pose3d(Rotation3d.Identity,new(-300,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known};
        f.M.World.AddLocatedObject(charger);
        Assert.Contains(f.M.World.FindLocatedMatchingObjects(new BlockWorldFilter()),o=>o.ObjectId==9u&&o.Type==ObjectType.Charger_Basic);
        var roll=Roll(f);int mark=f.Rig.Mark();
        Assert.True(manager.StartAsync(Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());QueuedSelection.Keep(f.Rig.Robot,manager,Id);
        for(int i=0;i<200&&!f.Rig.SentSince(mark).OfType<DockWithObject>().Any();i++)scene.Step();
        var dock=Assert.Single(f.Rig.SentSince(mark).OfType<DockWithObject>());
        Assert.Equal((byte)5,dock.DockAction);Assert.Equal((byte)0,dock.DockingMethod);Assert.True(roll.PreDockReached);
        // The charger was still a located object of the current origin when the drive ran.
        var located=Assert.Single(f.M.World.FindLocatedMatchingObjects(new BlockWorldFilter()),o=>o.Type==ObjectType.Charger_Basic);
        Assert.Equal(9u,located.ObjectId);Assert.Equal(-300.0,located.Pose.Translation.X,3);
    }

    [Fact]
    public void ExactConfiguredManagerStopRetiresOnlyOwnedWorkAndKeepsNativeLightRetention()
    {
        // S10/shared Stop005BD08C..126: own helper/action cleanup, target reset; S16 retains manager lights.
        using var scene=new RollFirmwareScene(behaviorId:Id);var f=scene.F;var manager=f.Robot.BehaviorManagerOwner!;
        Assert.True(manager.StartAsync(Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());QueuedSelection.Keep(f.Rig.Robot,manager,Id);var roll=Roll(f);
        var owned=Assert.IsAssignableFrom<ActionRunner>(roll.OwnedRunner);
        var unrelated=new NativeTimedWaitRunner(f.M,100f);f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,unrelated);f.Rig.Tick();
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Robot.ActionList.Watcher.Update();
        Assert.True(owned.DestructionObserved);Assert.False(unrelated.DestructionObserved);Assert.Equal(EngineActionResult.Running,unrelated.State);
        Assert.Equal(uint.MaxValue,roll.Target);Assert.False(roll.HasLiveHelper);Assert.Single(manager.InspectStateLights().Entries);
        f.Robot.ActionList.Clear();
    }
}
