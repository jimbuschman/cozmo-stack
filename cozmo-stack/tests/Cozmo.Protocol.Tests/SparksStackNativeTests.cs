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
public sealed class SparksStackNativeTests
{
    private static NativeStackBlocksBehavior Stack(Fixture f)=>Assert.IsType<NativeStackBlocksBehavior>(f.Runtime.Bound["SparksStackBlock"]);
    private static ObservableObject Add(Fixture f,uint id,ObjectType type,float x,float z=22)
    {
        var obj=new ObservableObject(id,type,CubeGeometry.MarkersFor(type)){Pose=new Pose3d(Rotation3d.Identity,new(x,0,z),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known,LastObservedTimestamp=f.Robot.StoredState!.Timestamp};
        obj.LastObservedMarkers=obj.Markers.Select(m=>m.Code).ToArray();foreach(var marker in obj.Markers)marker.LastObservedTimestamp=obj.LastObservedTimestamp;
        f.M.World.AddLocatedObject(obj);return obj;
    }
    private static void Unlock(Fixture f){f.Robot.Progression!.Unlocked.Add(9);f.Robot.Progression.Unlocked.Add(10);}
    [Theory]
    [InlineData(true,(byte)0)] [InlineData(true,(byte)1)]
    [InlineData(false,(byte)0)] [InlineData(false,(byte)1)]
    public void ConfiguredActivityRunsActualPickupPlacementVerifyLiftAndReports(bool carrying,byte soft)
    {
        // S/P/C actual configured factory/activity/cache/helpers/ActionList. Scene, admission and firmware inputs are explicitly supplied.
        using var scene=new StackFirmwareScene(carrying);var f=scene.F;var stack=Stack(f);var manager=f.Robot.BehaviorManagerOwner!;
        var order=new List<string>();var local=new List<RobotCompletedAction>();var raw=new List<byte[]>();var typed=new List<ActionCompletionReport>();var sparks=new List<(ushort Tag,byte[] Body)>();
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>
        {if(p is uint value&&value==40){Assert.Empty(order);order.Add("objective40");Assert.Null(stack.OwnedRunner);}});
        f.Rig.Engine.Needs.Log+=line=>
        {
            if(!line.StartsWith("needs.action_completed StackCube_Sparked:"))return;
            Assert.Equal(new[]{"objective40"},order);
            if(soft!=0)
            {var plain=Assert.IsType<TriggerAnimationAction>(stack.OwnedRunner);Assert.Equal((AnimationTrigger)0x21A,plain.Request.Trigger);Assert.Equal(BitConverter.UInt32BitsToSingle(0x3F4CCCCD),(float)stack.RunningScoreBonus);}
            else Assert.Null(stack.OwnedRunner);
            order.Add("needs43");
        };
        using var completion=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r&&r.ActionType==0x19)local.Add(r);});
        f.Rig.Engine.ActionCompleted+=r=>{if(r.ActionType==0x19)typed.Add(r);};
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>
        {
            if(tag==0x5A&&body.Length>=8&&BinaryPrimitives.ReadInt32LittleEndian(body.Span[4..])==0x19)raw.Add(body.ToArray());
            if(tag is 0x83 or 0x84)sparks.Add((tag,body.ToArray()));
        };
        var diagnostics=new List<string>();f.Rig.Engine.LogEntry+=e=>diagnostics.Add(e.Name+":"+e.Text);
        manager.SparkNameInput=id=>id==10?"StackTwoCubes":"Count";manager.SetNativeRequestedSpark(10,soft);
        f.Runtime.AdmitActivity("SparksStackBlock");manager.SwitchToNativeRequestedSpark();
        Assert.Equal((byte)27,f.Robot.BehaviorPublicState.State.Activity);
        for(int i=0;i<180&&!f.Runtime.AwaitingParentActivityPick;i++)scene.Step();
        var trace=Path.Combine(new DirectoryInfo(f.Rig.Engine.Options.ResourcesPath!).Parent!.Parent!.Parent!.Parent!.FullName,".scratch","sparks-stack-build");Directory.CreateDirectory(trace);
        File.WriteAllLines(Path.Combine(trace,$"pipeline-{carrying}-{soft}.log"),scene.Trace.Concat(diagnostics));
        string state=string.Join(" | ",scene.Trace.TakeLast(15))+" | "+string.Join(" | ",diagnostics.TakeLast(15));
        Assert.True(f.Runtime.AwaitingParentActivityPick,state);Assert.Equal(new[]{"objective40","needs43"},order);
        Assert.Equal(carrying?0:1,scene.Pickups);Assert.Equal(1,scene.Placements);Assert.Equal(0,scene.Grounds);Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);
        var place=Assert.Single(local.Where(r=>r.Result==0));Assert.Equal(new ObjectInteractionCompletion(scene.Bottom,0,0,0,0,1,false),place.ObjectInteraction);Assert.Equal((byte?)0,place.CompletionVariant);
        f.Rig.Engine.DrainAppDelivery();var api=Assert.Single(typed.Where(r=>r.Tag==place.Tag));Assert.Equal(place.ObjectInteraction,api.ObjectInteraction);
        var wire=Assert.Single(raw.Where(b=>BinaryPrimitives.ReadUInt32LittleEndian(b)==place.Tag));
        Assert.Equal(new byte[]{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},wire.AsSpan(wire.Length-18,16).ToArray());Assert.Equal((byte)1,wire[^2]);Assert.Equal((byte)0,wire[^1]);Assert.Equal(scene.Bottom,BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(wire.Length-22)));
        Assert.Contains(f.Rig.SentSince(0).OfType<DockWithObject>(),d=>d.DockAction==(byte)DockAction.PlaceHigh&&d.DockingMethod==0&&!d.Field8&&!d.Field5&&d.Field6==0);
        Assert.Contains(f.Rig.SentSince(0).OfType<SetLiftHeight>(),l=>l.HeightMm==32f&&l.MaxSpeedRadPerSec==10f&&l.AccelRadPerSec2==20f);
        if(soft==0){Assert.Equal(new ushort[]{0x83,0x84},sparks.Select(r=>r.Tag));Assert.Equal(new byte[]{10,0,0,0},sparks[0].Body);Assert.Equal(new byte[]{1},sparks[1].Body);}else Assert.Empty(sparks);
        int replies=sparks.Count;for(int i=0;i<3;i++)f.Rig.Tick();Assert.Equal(replies,sparks.Count);Assert.Equal(0x55,manager.RequestedSparkId);Assert.Equal(10,manager.ActiveSparkId);
        // Explicit supplied changed next-admission resumes the inherited missing parent picker; it does not follow from leaf null alone.
        f.Runtime.AdmitActivity("PutDownDispatch");Assert.False(f.Runtime.AwaitingParentActivityPick);Assert.DoesNotContain(f.Rig.Robot.Animations.Scheduler.IdleStack,e=>e.Lock=="Activity_SparksStackBlock");
    }
    [Fact]
    public void ActualCacheEntriesEnsureDependencyRetainBestAndGateSameFloatTime()
    {
        // C1-C5: supplied located scene/carry input; actual robot-owned shared cache and lazy getter path.
        using var f=new Fixture();Add(f,7,ObjectType.Block_LIGHTCUBE1,200);Add(f,8,ObjectType.Block_LIGHTCUBE2,400);
        var cache=f.Runtime.PickupTargets!;Assert.Equal(8u,cache.GetBest(3));Assert.Equal(7u,cache.GetBest(5));
        Assert.Equal(cache.Inspect(5).Last,cache.Inspect(3).Last);Assert.Equal(1,cache.Inspect(5).Rebuilds);Assert.Equal(1,cache.Inspect(3).Rebuilds);
        Add(f,6,ObjectType.Block_LIGHTCUBE3,100);Assert.DoesNotContain(6u,cache.GetValid(5));Assert.Equal(1,cache.Inspect(5).Rebuilds);
        f.Rig.Tick();Assert.Contains(6u,cache.GetValid(5));Assert.Equal(7u,cache.GetBest(5));Assert.Equal(8u,cache.GetBest(3));
        f.Robot.CarryingOwner!.SetCarrying(8);Assert.Equal(7u,cache.GetBest(5)); // no invented immediate invalidation.
        f.Rig.Tick();Assert.Equal(8u,cache.GetBest(5));Assert.Equal(new uint[]{8},cache.GetValid(5));Assert.Equal(6u,cache.GetBest(3));
    }
    [Fact]
    public void BottomFailureWindowComesFromActualWhiteboardAndCanStack()
    {
        // C4 exact action1/3sec/60mm/PI and Family2; no generic nearest-upright chooser.
        using var f=new Fixture();Add(f,7,ObjectType.Block_LIGHTCUBE1,200);var bottom=Add(f,8,ObjectType.Block_LIGHTCUBE2,400);
        var cache=f.Runtime.PickupTargets!;Assert.Equal(8u,cache.GetBest(3));f.M.Whiteboard.SetFailedToUse(bottom,ObjectActionFailure.StackOnObject);
        Assert.Equal(8u,cache.GetBest(3));f.Rig.Tick();Assert.Empty(cache.GetValid(3));Assert.Equal(uint.MaxValue,cache.GetBest(3));
        f.Rig.Tick(3010);Assert.Equal(8u,cache.GetBest(3));
    }
    [Fact]
    public void ConfiguredFactoryUsesExactStackInstanceAndKnownGetterWithoutOrdinarySubstitution()
    {
        using var f=new Fixture();Unlock(f);Add(f,7,ObjectType.Block_LIGHTCUBE1,200);Add(f,8,ObjectType.Block_LIGHTCUBE2,400);
        var stack=Stack(f);Assert.True(stack.IsRunnable(f.Runtime.Context));Assert.Equal(7u,stack.Top);Assert.Equal(8u,stack.Bottom);Assert.Equal(new[]{0},stack.OwnSlot2C());
        stack.OwnSlot7C();Assert.Equal(7u,stack.Top);Assert.Equal(8u,stack.Bottom);
        var activity=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksStackBlock");Assert.NotNull(activity.SparkedOwner);Assert.Equal(80f,activity.SparkedOwner!.Maximum);
        Assert.Equal(new[]{"DriveOffCharger","KnockOverCubes","SparksStackBlock","SparksPickupSingleCubeToStack","SparksLookInPlace"},activity.Chooser!.BehaviorIds);
    }
    [Fact]
    public void PolicyIsOnlyNormalSelectedLocatedPlacementAndGroundUndefinedCarrierStaysUnavailable()
    {
        // P7/SF4 unit concrete getters; no action result, callback or physical success is supplied.
        using var f=new Fixture();Add(f,8,ObjectType.Block_LIGHTCUBE2,400);var place=new NativePlaceRelRunner(f.M,8);var ground=new NativePlaceGroundRunner(f.M);
        try
        {
            Assert.False(place.GetObjectInteractionCompletion().IsPayloadDefined);place.Action.SetSelectedDockAction(DockAction.PlaceHigh);
            var normal=place.GetObjectInteractionCompletion();Assert.True(normal.IsPayloadDefined);Assert.Equal(new ObjectInteractionCompletion(8,0,0,0,0,1,false),normal);
            Assert.Equal(new byte[]{8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0},normal.PackBody());
            Assert.Equal((byte?)0,ground.GetCompletionVariant());Assert.False(ground.GetObjectInteractionCompletion().IsPayloadDefined);Assert.Equal((byte)1,ground.GetObjectInteractionCompletion().Count);
            Assert.False(place.CanInterrupt());Assert.False(ground.CanInterrupt());Assert.Equal(30f,place.TimeoutSeconds);Assert.Equal(30f,ground.TimeoutSeconds);
        }
        finally{place.Prep();place.WatcherEnding();ground.Prep();ground.WatcherEnding();}
    }
    private static NativeStackBlocksBehavior Admit(StackFirmwareScene scene)
    {
        var f=scene.F;var manager=f.Robot.BehaviorManagerOwner!;
        manager.SparkNameInput=id=>id==10?"StackTwoCubes":"Count";manager.SetNativeRequestedSpark(10,0);
        f.Runtime.AdmitActivity("SparksStackBlock");manager.SwitchToNativeRequestedSpark();var stack=Stack(f);for(int i=0;i<30&&stack.PlacementHelper is null;i++)scene.Step();Assert.True(stack.EngineRunning);return stack;
    }
    private sealed class Replacement(ManipulationSystem m):SteppedBehavior("stack-replacement-input","Dev")
    {
        public override bool IsRunnable(BehaviorContext context)=>true;
        protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
        protected override void OnStart()=>StartActing(new NativeTimedWaitRunner(m,100),null);
    }
    [Fact]
    public void StrictRetargetCancelsOwnedWorkOnceAndManagerReplacementPreservesUnrelatedQueue()
    {
        // S8-S11: independently supplied located cubes, actual configured owner/cache/manager/action retirement.
        using var scene=new StackFirmwareScene(true);var f=scene.F;var stack=Admit(scene);
        f.Rig.Tick();var first=Assert.IsAssignableFrom<ActionRunner>(stack.OwnedRunner);
        var unrelated=new NativeTimedWaitRunner(f.M,100);f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,unrelated,0);f.Rig.Tick();
        Add(f,69,ObjectType.Block_LIGHTCUBE3,-400);f.Rig.Tick();Assert.Equal(scene.Bottom,stack.Bottom);Assert.False(stack.Replaced);
        Add(f,70,ObjectType.Block_LIGHTCUBE3,100);f.Rig.Tick();
        Assert.Equal(70u,stack.Bottom);Assert.True(stack.Replaced);Assert.True(first.DestructionObserved);
        var replacementWork=Assert.IsAssignableFrom<ActionRunner>(stack.OwnedRunner);
        Add(f,71,ObjectType.Block_LIGHTCUBE2,70);f.Rig.Tick();Assert.Equal(70u,stack.Bottom);Assert.Same(replacementWork,stack.OwnedRunner);
        var manager=f.Robot.BehaviorManagerOwner!;var replacement=new Replacement(f.M);replacement.BindActionCompletionOwner(f.Rig.Robot,f.Robot);manager.Add(replacement);
        try
        {
            Assert.True(manager.StartAsync(replacement.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Assert.True(replacementWork.DestructionObserved);Assert.False(unrelated.DestructionObserved);Assert.Equal(EngineActionResult.Running,unrelated.State);
            Assert.Equal(uint.MaxValue,stack.Top);Assert.Equal(uint.MaxValue,stack.Bottom);Assert.False(stack.Replaced);Assert.Equal(1,stack.State);
        }
        finally{manager.Stop(BehaviorStopReason.Interrupted,0);replacement.RetireActionCompletionOwner();f.Robot.ActionList.Clear();}
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void InvalidBottomIsToleratedOnlyWhenItsActualTopNeighborIsTheSavedTop(bool related)
    {
        // Operator-corrected S8 branch: invalid bottom checks Neighbor(BOTTOM), not the top's neighbor.
        using var scene=new StackFirmwareScene(true);var f=scene.F;var stack=Admit(scene);f.Rig.Tick();
        // Supplied physical carry detachment and located stacked scene; no synthetic action result.
        f.M.Docking.ReleaseCarriedObject();
        var top=f.M.World.GetLocatedObjectByIdHelper(scene.Top)!;var bottom=f.M.World.GetLocatedObjectByIdHelper(scene.Bottom)!;
        top.Pose=new Pose3d(Rotation3d.Identity,new(400,0,related?66:110),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        f.M.Whiteboard.SetFailedToUse(bottom,ObjectActionFailure.StackOnObject);f.Rig.Tick();
        Assert.Equal(related,stack.EngineRunning);if(!related){Assert.Equal(uint.MaxValue,stack.Bottom);Assert.False(stack.HasCurrentAction);}
    }
    [Theory]
    [InlineData(0x03000010u)] [InlineData(0x02000000u)]
    public void SuppliedNonterminalResponseWritesOnlyItsSourceStateAndDoesNotRetry(uint result)
    {
        // P3 response boundary unit: explicitly supplied raw result, no claim that native docking produced it.
        using var scene=new StackFirmwareScene(true);var stack=Admit(scene);var helper=Assert.IsType<NativePlaceRelObjectHelper>(stack.PlacementHelper);
        uint attempts=helper.Attempts;var runner=stack.OwnedRunner;helper.Respond(new RobotCompletedAction(123,0x19,result,Array.Empty<uint>(),0));
        Assert.Equal(attempts,helper.Attempts);Assert.Same(runner,stack.OwnedRunner);Assert.Equal(BehaviorStatus.Running,helper.Status);
        Assert.Equal(result==0x03000010?(uint?)scene.Bottom:null,scene.F.M.Whiteboard.NoPreDockPosesObjectId);
    }
    [Fact]
    public void ActualFirmwareFailureRetriesBeforeSuccessfulPlacement()
    {
        using var scene=new StackFirmwareScene(true){FailPlacements=1};var stack=Admit(scene);
        for(int i=0;i<150&&!scene.F.Runtime.AwaitingParentActivityPick;i++)scene.Step();
        Assert.True(scene.F.Runtime.AwaitingParentActivityPick,string.Join(" | ",scene.Trace.TakeLast(20)));
        Assert.Equal(2,scene.Placements);Assert.Equal(0,scene.Grounds);Assert.Equal(2,stack.State);
        Assert.Contains(scene.Trace,t=>t=="COMPLETE type19 result04000003");Assert.Contains(scene.Trace,t=>t=="COMPLETE type19 result00000000");
    }
    [Fact]
    public void SuppliedFailedPlaceResponseRunsActualRecoveryGroundAndNextPickup()
    {
        // S12/P8: explicitly supplied raw BadObject response at actual active Place callback boundary.
        // Real helper pop/maintenance clear/callback and DriveStraight/Ground/Verify follow.
        using var scene=new StackFirmwareScene(true);var f=scene.F;var stack=Admit(scene);
        bool PlaceOwned()=>stack.PlacementHelper?.Runner is not null&&ReferenceEquals(stack.OwnedRunner,stack.PlacementHelper.Runner);
        for(int i=0;i<70&&!PlaceOwned();i++)scene.Step(PlaceOwned);
        var helper=Assert.IsType<NativePlaceRelObjectHelper>(stack.PlacementHelper);var place=Assert.IsType<NativePlaceRelRunner>(stack.OwnedRunner);
        Assert.Same(helper,Assert.Single(f.Runtime.AI.Helpers.StackHelpers));Assert.Equal(0,scene.Placements);
        Assert.Empty(f.Rig.SentSince(0).OfType<DockWithObject>());
        Assert.True(f.Robot.ActionList.Cancel(place.Tag));f.Robot.ActionList.Watcher.Update();
        Assert.False(stack.HasCurrentAction);Assert.Equal(BehaviorStatus.Running,helper.Status);
        helper.Respond(new RobotCompletedAction(123,0x19,0x03000004,Array.Empty<uint>(),0));
        scene.Motors();var recovery=Assert.IsAssignableFrom<CompoundActionSequential>(stack.OwnedRunner);
        var ground=Assert.Single(recovery.Children.OfType<NativePlaceGroundRunner>());
        for(int i=0;i<80&&!ground.DestructionObserved;i++)scene.Step();
        Assert.Equal(0,scene.Placements);Assert.Equal(1,scene.Grounds);Assert.True(ground.DestructionObserved);Assert.Equal(0u,ground.State);
        Assert.True(recovery.DestructionObserved);Assert.Equal(0,stack.State);Assert.NotNull(stack.PickupHelper);Assert.Equal(scene.Top,stack.PickupHelper!.Target);
        Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActualGroundRootKeepsScalarAndDefinedFieldsWhileUndefinedPublicPayloadIsWithheld(bool suppliedProcessedFramesOnly)
    {
        // P8/SF4 standalone actual ActionList root (compound children do not broadcast their own5A).
        // Supplied firmware pose/scene/B8, actual retained Turn/Wait and Clear recipient.
        // Failure supplies processed43 frames without44 at the inherited vision boundary; positive uses real rendered worker.
        using var scene=new StackFirmwareScene(true){GroundProcessedFramesOnly=suppliedProcessedFramesOnly};var f=scene.F;
        f.X=200;scene.Motors();f.M.World.SelectedObjectId=scene.Top;
        var runner=new NativePlaceGroundRunner(f.M);var local=new List<RobotCompletedAction>();var api=new List<ActionCompletionReport>();var raw=new List<byte[]>();var logs=new List<string>();var deleted=new List<uint>();
        f.M.World.LocatedObjectDeleted+=deleted.Add;f.Rig.Engine.LogEntry+=e=>logs.Add(e.Name+":"+e.Text);
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r&&r.ActionType==0x18)local.Add(r);});
        f.Rig.Engine.ActionCompleted+=r=>{if(r.ActionType==0x18)api.Add(r);};
        f.Rig.Engine.EngineToGameMessage+=(tag,b)=>{if(tag==0x5A&&b.Length>=8&&BinaryPrimitives.ReadInt32LittleEndian(b.Span[4..])==0x18)raw.Add(b.ToArray());};
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,runner,0);
        for(int i=0;i<60&&!runner.DestructionObserved;i++)scene.Step();
        Assert.True(runner.DestructionObserved);Assert.Equal(1,scene.Grounds);Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);
        var completed=Assert.Single(local);Assert.Equal(suppliedProcessedFramesOnly?0x0300001Du:0u,completed.Result);Assert.Equal((byte?)0,completed.CompletionVariant);
        var fields=completed.ObjectInteraction!;Assert.Equal(scene.Top,fields.Word0);Assert.Equal((byte)1,fields.Count);Assert.False(fields.Flag);Assert.False(fields.IsPayloadDefined);
        f.Rig.Engine.DrainAppDelivery();Assert.Empty(api);Assert.Empty(raw);Assert.Empty(deleted);Assert.NotNull(f.M.World.GetLocatedObjectByIdHelper(scene.Top));
        if(suppliedProcessedFramesOnly)Assert.Contains(logs,l=>l.Contains("BlockWorld.ClearObjectHelper.ClearingSelectedObject"));
    }
    [Fact]
    public void HiddenGroundSceneDeletionComesFromAmbientNegativeVisionBeforeGroundClear()
    {
        // Bounded diagnosis of focused15: M11-007 MarkUnobserved506FBC→MakingUnknown50702E→DeleteLocated.
        // This is an upstream vision-loss observation, separate from Ground's P9 Clear-only recipient.
        using var scene=new StackFirmwareScene(true){HideGroundCube=true};var f=scene.F;f.X=200;scene.Motors();
        var logs=new List<string>();var deleted=new List<uint>();f.M.World.Log+=logs.Add;f.M.World.LocatedObjectDeleted+=deleted.Add;
        f.Rig.Engine.LogEntry+=e=>logs.Add(e.Name+":"+e.Text);
        var ground=new NativePlaceGroundRunner(f.M);f.Robot.ActionList.QueueAction(QueueActionPosition.Now,ground,0);
        for(int i=0;i<60&&!ground.DestructionObserved;i++)scene.Step();f.Rig.Engine.DrainAppDelivery();
        Directory.CreateDirectory(Path.Combine(".scratch","sparks-stack-build"));File.WriteAllLines(Path.Combine(".scratch","sparks-stack-build","negative-vision-producer.log"),logs);
        Assert.Equal(0x0300001Du,ground.State);Assert.Equal(new[]{scene.Top},deleted);
        Assert.Contains(logs,l=>l.Contains($"ObjectPoseConfirmer.MarkObjectUnobserved.MakingUnknown: object {scene.Top}"));
        Assert.Contains(logs,l=>l.Contains("BlockWorld.ClearObjectHelper.NullObjectPointer"));
    }
    [Fact]
    public void ExhaustedActualAttemptsMarkFailureAndValidityMayStopRecovery()
    {
        // P3/S8/S12: first failed precheck consumes one StartPlace attempt then Drive1;
        // two failed firmware docks exhaust3. Source validation may stop the new recovery.
        using var scene=new StackFirmwareScene(true){FailPlacements=int.MaxValue};var stack=Admit(scene);var f=scene.F;
        var helper=Assert.IsType<NativePlaceRelObjectHelper>(stack.PlacementHelper);
        var logs=new List<string>();f.Rig.Engine.LogEntry+=e=>logs.Add(e.Name+":"+e.Text);
        for(int i=0;i<90&&helper.Status!=BehaviorStatus.Failure;i++)scene.Step();
        Assert.Equal(3u,helper.Attempts);Assert.Equal(BehaviorStatus.Failure,helper.Status);Assert.Equal(2,scene.Placements);
        var bottom=f.M.World.GetLocatedObjectByIdHelper(scene.Bottom)!;
        Assert.True(f.M.Whiteboard.DidFailToUse(unchecked((int)scene.Bottom),new[]{ObjectActionFailure.StackOnObject},3f,bottom.NativePose,60f,MathF.PI));
        scene.Step();Assert.False(stack.EngineRunning);Assert.Equal(0,scene.Grounds);
        Assert.Contains(logs,l=>l.Contains("BehaviorStackBlocks.UpdateInternal.BlocksInvalid"));
        Assert.DoesNotContain(scene.Trace,t=>t=="COMPLETE type18 result00000000");
    }
    [Fact]
    public void NativePreActionSelectsAfterGuardsBeforeObstaclesAndKeepsSelectionOnLaterFailure()
    {
        // SF9 native recipient; independent prior-selected state and missing/actual carried inputs.
        using var f=new Fixture();var target=Add(f,7,ObjectType.Block_LIGHTCUBE1,200);f.M.World.SelectedObjectId=99;
        var main=f.Robot.GetCheckedMainPose();int obstacleCalls=0;
        IReadOnlyList<PreActionObstacle> Obstacles(){obstacleCalls++;Assert.Equal(7u,f.M.World.SelectedObjectId);return Array.Empty<PreActionObstacle>();}
        DockPreActionPoses.GetNative(new(null,PreActionType.PlaceRelative,true,.1,0,false,0),main,null,Obstacles,f.M.World);
        DockPreActionPoses.GetNative(new(target,PreActionType.PlaceRelative,true,.1,0,false,0),main,7,Obstacles,f.M.World);
        Assert.Equal(0,obstacleCalls);Assert.Equal(99u,f.M.World.SelectedObjectId);
        DockPreActionPoses.GetNative(new(target,PreActionType.PlaceRelative,true,.1,0,false,0),main,null,Obstacles,f.M.World);
        Assert.Equal(1,obstacleCalls);Assert.Equal(7u,f.M.World.SelectedObjectId);
        var noPoses=DockPreActionPoses.GetNative(new(target,PreActionType.Entry,true,.1,0,false,0),main,null,Obstacles,f.M.World);
        Assert.Equal(ActionResult.NoPreActionPoses,noPoses.Result);Assert.Equal(7u,f.M.World.SelectedObjectId);
        Assert.False(f.M.World.SelectNativeObject(123));Assert.Equal(7u,f.M.World.SelectedObjectId);
        // Public specification remains pure, including obstacle evaluation.
        f.M.World.SelectedObjectId=99;
        DockPreActionPoses.Get(new(target,PreActionType.PlaceRelative,true,.1,0,false,0),main,null,()=>Array.Empty<PreActionObstacle>());
        Assert.Equal(99u,f.M.World.SelectedObjectId);
    }
    [Theory]
    [InlineData(PreActionType.Docking)] [InlineData(PreActionType.PlaceRelative)]
    public void NativeHelperPrecheckSelectsThroughBothBranchesAndUsesFloatTimestampFreshness(PreActionType type)
    {
        // SF11: rounded f32 last16777219+1000 == rounded now16778220; double would reject.
        using var f=new Fixture();var obj=Add(f,7,ObjectType.Block_LIGHTCUBE1,200);var main=f.Robot.GetCheckedMainPose();
        int calls=0;IReadOnlyList<PreActionObstacle> Obstacles(){calls++;Assert.Equal(7u,f.M.World.SelectedObjectId);return Array.Empty<PreActionObstacle>();}
        f.M.World.SelectedObjectId=99;obj.LastObservedTimestamp=1000;
        var stale=PlaceRelObjectOffsetPoses.IsAtPreActionPoseWithVisualVerificationNative(obj,type,0,0,2001,main,null,f.M.Vision.Calibration!,ObjectPoseQueries.Engine,Obstacles,f.M.World);
        Assert.Equal(ActionResult.VisualObservationFailed,stale);Assert.Equal(0,calls);Assert.Equal(99u,f.M.World.SelectedObjectId);
        obj.LastObservedTimestamp=16777219;
        _=PlaceRelObjectOffsetPoses.IsAtPreActionPoseWithVisualVerificationNative(obj,type,0,0,16778220,main,null,f.M.Vision.Calibration!,ObjectPoseQueries.Engine,Obstacles,f.M.World);
        Assert.Equal(1,calls);Assert.Equal(7u,f.M.World.SelectedObjectId);
    }
}
