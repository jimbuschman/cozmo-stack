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
public sealed class StackBlocksNativeTests
{
    private static NativeStackBlocksBehavior Stack(Fixture f)=>Assert.IsType<NativeStackBlocksBehavior>(f.Runtime.Bound["StackBlocks"]);
    private static ObservableObject Add(Fixture f,uint id,float x,Rotation3d? rotation=null,Pose3d? parent=null)
    {
        var obj=new ObservableObject(id,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
        {Pose=new Pose3d(rotation??Rotation3d.Identity,new(x,0,22),parent:parent??f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known,LastObservedTimestamp=f.Robot.StoredState!.Timestamp};
        obj.LastObservedMarkers=obj.Markers.Select(m=>m.Code).ToArray();foreach(var marker in obj.Markers)marker.LastObservedTimestamp=obj.LastObservedTimestamp;
        f.M.World.AddLocatedObject(obj);return obj;
    }
    private static NativeStackBlocksBehavior Admit(StackFirmwareScene scene,bool hard=false)
    {
        var f=scene.F;var manager=f.Robot.BehaviorManagerOwner!;
        if(hard){manager.SparkNameInput=id=>id==10?"StackTwoCubes":"Count";manager.SetNativeRequestedSpark(10,0);manager.SwitchToNativeRequestedSpark();}
        // Explicit canonical manager admission/choice input. This does not admit PlayAlone,
        // evaluate unknown siblings, run its scoring/interlude or create a Sparked owner.
        Assert.True(manager.StartAsync("StackBlocks",f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());return Stack(f);
    }
    [Theory]
    [InlineData(0x3F800000u,0x3F800000u,0x3F800000u,1)]
    [InlineData(0x3F800000u,0x3F800001u,0x3F800001u,2)]
    [InlineData(0x3F800000u,0x3F800001u,0x3F800002u,3)]
    [InlineData(0xBF800000u,0x3F800000u,0x3F800000u,-1)]
    [InlineData(0u,0xBF800000u,0u,-2)] [InlineData(0u,0u,0xBF800000u,-3)]
    [InlineData(0u,0u,0u,-1)] [InlineData(0x80000000u,0u,0u,-1)]
    public void NativeAxisUsesLiteralFloatRowTwoStrictTiesAndSign(uint x,uint y,uint z,int expected)
    {
        // O3-O5 /005507A0..854: raw matrix-carrier unit, not a quaternion construction oracle.
        var matrix=new Mat3(1,0,0,0,1,0,BitConverter.UInt32BitsToSingle(x),BitConverter.UInt32BitsToSingle(y),BitConverter.UInt32BitsToSingle(z));
        Assert.Equal(expected,RotatedParentAxis.GetNativeZ(matrix));
    }
    [Fact]
    public void NativeAxisSeesFloatMatrixNarrowingBeforeDominance()
    {
        // Distinct doubles become equal native matrix words. X wins; no tolerance/upright shortcut.
        var matrix=new Mat3(1,0,0,0,1,0,1,1+1e-8,1+2e-8);
        Assert.Equal(1,RotatedParentAxis.GetNativeZ(matrix));
    }
    [Fact]
    public void EntriesFourTwoAreIndependentAndDependencyPrecedesTheirClockGate()
    {
        using var f=new Fixture();f.Robot.Progression!.Unlocked.Remove(9);Add(f,7,200);Add(f,8,400);
        var cache=f.Runtime.PickupTargets!;Assert.Equal(8u,cache.GetBest(2));Assert.Equal(7u,cache.GetBest(4));
        Assert.Equal(cache.Inspect(4).Last,cache.Inspect(2).Last);Assert.Equal(1,cache.Inspect(4).Rebuilds);Assert.Equal(1,cache.Inspect(2).Rebuilds);
        Assert.Equal(0,cache.Inspect(5).Rebuilds);Assert.Equal(0,cache.Inspect(3).Rebuilds);
        Add(f,6,100);Assert.DoesNotContain(6u,cache.GetValid(4));Assert.Equal(8u,cache.GetBest(2));
        f.Rig.Tick();Assert.Contains(6u,cache.GetValid(4));Assert.Equal(7u,cache.GetBest(4));Assert.Equal(8u,cache.GetBest(2));
        Assert.Equal(6u,cache.GetBest(5));Assert.Equal(7u,cache.GetBest(3)); // independent best state, not aliases.
        f.Robot.CarryingOwner!.SetCarrying(8);Assert.Equal(7u,cache.GetBest(4));f.Rig.Tick();
        Assert.Equal(8u,cache.GetBest(4));Assert.Equal(new uint[]{8},cache.GetValid(4));Assert.Equal(6u,cache.GetBest(2));
    }
    [Fact]
    public void OrdinaryEntryInstallsActualDefaultBestStackTieOrderAndSignedValidOrdering()
    {
        // O2/O6: actual configuration stack8 first beats lowerID1 on an exact distance tie.
        // These are supplied located poses, not planner-generated geometry or a fabricated cache best.
        using var f=new Fixture();f.Robot.Progression!.Unlocked.Remove(9);
        Add(f,7,200);var top=Add(f,8,200);top.Pose=new Pose3d(Rotation3d.Identity,new(200,0,66),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        var loose=Add(f,1,-200);loose.Pose=new Pose3d(Rotation3d.Identity,new(-200,0,66),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        Add(f,uint.MaxValue-1,-500);f.Runtime.Configurations.ForceUpdate();f.Runtime.Configurations.Update();
        Assert.Contains(f.Runtime.Configurations.Snapshot.Stacks,s=>s.Bottom==7&&s.Top==8);
        var cache=f.Runtime.PickupTargets!;Assert.Equal(new uint[]{uint.MaxValue-1,1,8},cache.GetValid(4));Assert.Equal(8u,cache.GetBest(4));
        loose.Pose=new Pose3d(Rotation3d.Identity,new(50,0,66),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));f.Rig.Tick();
        Assert.Equal(8u,cache.GetBest(4)); // Valid old best survives a newly closer candidate.
    }
    [Fact]
    public void TopFourUsesCarriedPointerLocalAxisAndTheLiteralNoCarryUnlockDirection()
    {
        using var f=new Fixture();f.Robot.Progression!.Unlocked.Remove(9);
        using var parent=new Pose3d(Rotation3d.FromAngleAxis(Radians.Pi,new(1,0,0)),new(0,0,44),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        var obj=Add(f,7,200,parent:parent);var cache=f.Runtime.PickupTargets!;
        Assert.True(cache.Top(obj,true));Assert.Contains(7u,cache.GetValid(4));
        f.Robot.Progression.Unlocked.Add(9);Assert.False(cache.Top(obj,true));Assert.Contains(7u,cache.GetValid(4)); // same-time cache retained.
        f.Rig.Tick();Assert.DoesNotContain(7u,cache.GetValid(4));Assert.Contains(7u,cache.GetValid(5));
        f.Robot.CarryingOwner!.SetCarrying(7);Assert.True(cache.Top(obj,true)); // LOCAL+Z despite ROOT-Z.
        var impostor=new ObservableObject(7,obj.Type,CubeGeometry.MarkersFor(obj.Type)){Pose=new Pose3d(Rotation3d.Identity,new(200,0,22),parent:parent)};
        Assert.False(cache.Top(impostor,true)); // explicit candidate predicate unit: same ID is not pointer identity.
        obj.Pose=new Pose3d(Rotation3d.FromAngleAxis(Radians.Pi,new(1,0,0)),new(200,0,22),parent:parent);
        Assert.False(cache.Top(obj,true));Assert.True(cache.Top(obj,false)); // LOCAL-Z while ROOT+Z, carried5 has no axis gate.
    }
    [Fact]
    public void BottomTwoUsesLocalAxisAfterSharedTopExclusionAndCanStack()
    {
        using var f=new Fixture();f.Robot.Progression!.Unlocked.Remove(9);Add(f,7,100);f.Robot.CarryingOwner!.SetCarrying(7);
        using var parent=new Pose3d(Rotation3d.FromAngleAxis(Radians.Pi,new(1,0,0)),new(0,0,44),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        Add(f,8,300,parent:parent);Add(f,9,500,Rotation3d.FromAngleAxis(Radians.Pi,new(1,0,0)),parent);
        var cache=f.Runtime.PickupTargets!;Assert.Contains(8u,cache.GetValid(2));Assert.DoesNotContain(9u,cache.GetValid(2));
        Assert.Contains(9u,cache.GetValid(3));Assert.DoesNotContain(7u,cache.GetValid(2));
    }
    [Fact]
    public void ActualRegularMetadataAndPlayAloneFrontierRemainDistinctFromSparked()
    {
        using var scene=new StackFirmwareScene(false,"StackBlocks",false);var f=scene.F;var stack=Stack(f);
        Assert.Equal(0xA0,(int)BehaviorID.StackBlocks);Assert.Equal(42,(int)NeedsActionId.StackCube);Assert.Equal(new[]{1},stack.OwnSlot2C());
        Assert.Equal(10u,stack.NativeBaseInputs!.RequiredUnlock);Assert.Equal((byte?)9,stack.NativeBaseInputs.CheckedExecutableType);
        var spark=Assert.IsType<NativeStackBlocksBehavior>(f.Runtime.Bound["SparksStackBlock"]);Assert.Equal((byte?)12,spark.NativeBaseInputs!.CheckedExecutableType);
        var missing=new List<string>();SteppedBehavior.MissingReported+=missing.Add;
        try{f.Runtime.AdmitActivity("PlayAlone");Assert.Null(f.Robot.BehaviorManagerOwner!.Current);Assert.Contains(missing,s=>s.Contains("Native activity admission PlayAlone"));}
        finally{SteppedBehavior.MissingReported-=missing.Add;}
        f.Robot.Progression.Unlocked.Remove(10);Assert.False(stack.IsRunnable(f.Runtime.Context));
        f.Robot.Progression.Unlocked.Add(10);Assert.True(stack.IsRunnable(f.Runtime.Context));Assert.Equal(scene.Top,stack.Top);Assert.Equal(scene.Bottom,stack.Bottom);
        f.Robot.Progression.Unlocked.Add(9);f.Rig.Tick();stack.OwnSlot7C();Assert.Equal(new[]{1},stack.OwnSlot2C());
    }
    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public void SuppliedManagerChoiceRunsRegularPipelineBothUnlockModesAndReportsNeeds42(bool carrying,bool rollUnlocked)
    {
        using var scene=new StackFirmwareScene(carrying,"StackBlocks",rollUnlocked);var f=scene.F;var stack=Stack(f);
        var order=new List<string>();var app=new List<ActionCompletionReport>();var local=new List<RobotCompletedAction>();var sparkReports=new List<ushort>();
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint id&&id==40){Assert.Empty(order);Assert.Null(stack.OwnedRunner);order.Add("objective40");}});
        f.Rig.Engine.Needs.Log+=line=>{if(!line.StartsWith("needs.action_completed StackCube:"))return;Assert.Equal(new[]{"objective40"},order);var animation=Assert.IsType<TriggerAnimationAction>(stack.OwnedRunner);Assert.Equal((AnimationTrigger)0x21A,animation.Request.Trigger);Assert.Equal(.8f,(float)stack.RunningScoreBonus);order.Add("needs42");};
        using var completion=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r&&r.ActionType==0x19)local.Add(r);});
        f.Rig.Engine.ActionCompleted+=r=>{if(r.ActionType==0x19)app.Add(r);};f.Rig.Engine.EngineToGameMessage+=(tag,_)=>{if(tag is 0x83 or 0x84)sparkReports.Add(tag);};
        Assert.Same(stack,Admit(scene));
        if(!carrying)Assert.Equal(new Radians(90f).Value,stack.PickupHelper!.MaxTurn); // R7 literal90f through checked Radians recipient.
        for(int i=0;i<160&&order.Count<2;i++)scene.Step();
        Assert.True(order.Count==2,string.Join(" | ",scene.Trace.TakeLast(20)));Assert.Equal(new[]{"objective40","needs42"},order);
        Assert.Equal(carrying?0:1,scene.Pickups);Assert.Equal(1,scene.Placements);Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);
        var place=Assert.Single(local.Where(r=>r.Result==0));Assert.Equal(new ObjectInteractionCompletion(scene.Bottom,0,0,0,0,1,false),place.ObjectInteraction);
        f.Rig.Engine.DrainAppDelivery();Assert.Contains(app,r=>r.Tag==place.Tag&&r.ObjectInteraction==place.ObjectInteraction);Assert.Empty(sparkReports);
        Assert.Contains(f.Rig.SentSince(0).OfType<DockWithObject>(),d=>d.DockAction==(byte)DockAction.PlaceHigh&&d.DockingMethod==0&&!d.Field8);
        Assert.Contains(f.Rig.SentSince(0).OfType<SetLiftHeight>(),l=>l.HeightMm==32&&l.MaxSpeedRadPerSec==10);
        Assert.True(f.Runtime.PickupTargets!.Inspect(rollUnlocked?5:4).Rebuilds>0);Assert.True(f.Runtime.PickupTargets.Inspect(rollUnlocked?3:2).Rebuilds>0);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RegularAssetStillUsesActualHardSparkInputsRatherThanForcingFalse(bool rollUnlocked)
    {
        using var scene=new StackFirmwareScene(true,"StackBlocks",rollUnlocked);var f=scene.F;var stack=Stack(f);var order=new List<string>();
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint id&&id==40){Assert.Empty(order);order.Add("objective40");}});
        f.Rig.Engine.Needs.Log+=s=>{if(!s.StartsWith("needs.action_completed StackCube:"))return;Assert.Null(stack.OwnedRunner);Assert.Equal(new[]{"objective40"},order);order.Add("needs42");};
        Admit(scene,true);for(int i=0;i<140&&order.Count<2;i++)scene.Step();
        Assert.Equal(new[]{"objective40","needs42"},order);Assert.Equal(1,scene.Placements);
    }
    private sealed class Replacement(ManipulationSystem m):SteppedBehavior("regular-stack-replacement-input","Dev")
    {
        public override bool IsRunnable(BehaviorContext context)=>true;
        protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
        protected override void OnStart()=>StartActing(new NativeTimedWaitRunner(m,100),null);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RegularRetargetKeepsTiesReplacesOnceAndStopsOnlyOwnedWork(bool rollUnlocked)
    {
        using var scene=new StackFirmwareScene(true,"StackBlocks",rollUnlocked);var f=scene.F;var stack=Admit(scene);f.Rig.Tick();
        var first=Assert.IsAssignableFrom<ActionRunner>(stack.OwnedRunner);var other=new NativeTimedWaitRunner(f.M,100);
        f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,other,0);f.Rig.Tick();
        Add(f,69,-400);f.Rig.Tick();Assert.Equal(scene.Bottom,stack.Bottom);Assert.False(stack.Replaced); // MAIN remains supplied0; equal400 distances.
        Add(f,70,100);f.Rig.Tick();Assert.Equal(70u,stack.Bottom);Assert.True(stack.Replaced);Assert.True(first.DestructionObserved);
        f.Rig.Tick();var replaced=Assert.IsAssignableFrom<ActionRunner>(stack.OwnedRunner);
        Add(f,71,-50);f.Rig.Tick();Assert.Equal(70u,stack.Bottom);Assert.Same(replaced,stack.OwnedRunner);
        var manager=f.Robot.BehaviorManagerOwner!;var next=new Replacement(f.M);next.BindActionCompletionOwner(f.Rig.Robot,f.Robot);manager.Add(next);
        try
        {
            Assert.True(manager.StartAsync(next.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Assert.True(replaced.DestructionObserved);Assert.False(other.DestructionObserved);Assert.Equal(EngineActionResult.Running,other.State);
            Assert.Equal(uint.MaxValue,stack.Top);Assert.Equal(uint.MaxValue,stack.Bottom);Assert.False(stack.Replaced);Assert.Equal(1,stack.State);
        }
        finally{manager.Stop(BehaviorStopReason.Interrupted,0);next.RetireActionCompletionOwner();f.Robot.ActionList.Clear();}
    }
    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void RegularValidityUsesCurrentModeAndOnlyToleratesTheSavedTopAboveBottom(bool rollUnlocked,bool related)
    {
        // Supplied carry detachment/located scene at the own validation boundary; no action outcome override.
        using var scene=new StackFirmwareScene(true,"StackBlocks",rollUnlocked);var f=scene.F;var stack=Admit(scene);f.Rig.Tick();
        f.M.Docking.ReleaseCarriedObject();var top=f.M.World.GetLocatedObjectByIdHelper(scene.Top)!;var bottom=f.M.World.GetLocatedObjectByIdHelper(scene.Bottom)!;
        top.Pose=new Pose3d(Rotation3d.Identity,new(400,0,related?66:110),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        f.M.Whiteboard.SetFailedToUse(bottom,ObjectActionFailure.StackOnObject);f.Rig.Tick();
        Assert.Equal(related,stack.EngineRunning);if(!related){Assert.False(stack.HasCurrentAction);Assert.Equal(uint.MaxValue,stack.Bottom);}
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RegularActualFailedDockRetriesAndThenCompletesPlacement(bool rollUnlocked)
    {
        using var scene=new StackFirmwareScene(true,"StackBlocks",rollUnlocked){FailPlacements=1};var stack=Admit(scene);
        for(int i=0;i<140&&stack.State!=2;i++)scene.Step();
        Assert.Equal(2,stack.State);Assert.Equal(2,scene.Placements);Assert.Equal(0,scene.Grounds);
        Assert.Contains(scene.Trace,t=>t=="COMPLETE type19 result04000003");Assert.Contains(scene.Trace,t=>t=="COMPLETE type19 result00000000");
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RegularExhaustionKeepsThreeCounterUsesTwoDocksAndSourceValidityCancellation(bool rollUnlocked)
    {
        using var scene=new StackFirmwareScene(true,"StackBlocks",rollUnlocked){FailPlacements=int.MaxValue};var stack=Admit(scene);var f=scene.F;var helper=stack.PlacementHelper!;
        for(int i=0;i<90&&helper.Status!=BehaviorStatus.Failure;i++)scene.Step();
        Assert.Equal(3u,helper.Attempts);Assert.Equal(BehaviorStatus.Failure,helper.Status);Assert.Equal(2,scene.Placements);
        var bottom=f.M.World.GetLocatedObjectByIdHelper(scene.Bottom)!;
        Assert.True(f.M.Whiteboard.DidFailToUse(unchecked((int)scene.Bottom),new[]{ObjectActionFailure.StackOnObject},3f,bottom.NativePose,60f,MathF.PI));
        scene.Step();Assert.False(stack.EngineRunning);Assert.Equal(0,scene.Grounds);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RegularSuppliedPlaceFailureBoundaryRunsGroundAndReturnsToPickup(bool rollUnlocked)
    {
        using var scene=new StackFirmwareScene(true,"StackBlocks",rollUnlocked);var f=scene.F;var stack=Admit(scene);
        bool PlaceOwned()=>stack.PlacementHelper?.Runner is not null&&ReferenceEquals(stack.OwnedRunner,stack.PlacementHelper.Runner);
        for(int i=0;i<70&&!PlaceOwned();i++)scene.Step(PlaceOwned);
        var helper=stack.PlacementHelper!;var place=Assert.IsType<NativePlaceRelRunner>(stack.OwnedRunner);Assert.Same(helper,Assert.Single(f.Runtime.AI.Helpers.StackHelpers));
        Assert.Empty(f.Rig.SentSince(0).OfType<DockWithObject>());Assert.True(f.Robot.ActionList.Cancel(place.Tag));f.Robot.ActionList.Watcher.Update();
        Assert.False(stack.HasCurrentAction);Assert.Equal(BehaviorStatus.Running,helper.Status);
        // Explicit raw failure response input at the source helper callback boundary; no WB mark,
        // so the actual subsequent validation/recovery may continue. No forced runner/helper state.
        helper.Respond(new RobotCompletedAction(123,0x19,0x03000004,Array.Empty<uint>(),0));scene.Motors();
        var recovery=Assert.IsAssignableFrom<CompoundActionSequential>(stack.OwnedRunner);var ground=Assert.Single(recovery.Children.OfType<NativePlaceGroundRunner>());
        for(int i=0;i<80&&!ground.DestructionObserved;i++)scene.Step();
        Assert.True(ground.DestructionObserved);Assert.Equal(0u,ground.State);Assert.True(recovery.DestructionObserved);
        Assert.Equal(1,scene.Grounds);Assert.Equal(0,scene.Placements);Assert.Equal(0,stack.State);Assert.Equal(scene.Top,stack.PickupHelper!.Target);
    }
}
