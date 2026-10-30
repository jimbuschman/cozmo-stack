using Cozmo.Robot;
using System.Text.Json;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class HikingBringBeaconCandidatesTests
{
    private static Pose3d NativeOriginPose(Fixture f)
    {
        var origin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        var pose=new Pose3d(Rotation3d.Identity,Vec3.Zero,parent:origin);
        Assert.True(PoseTreeNode.HasSameRoot(pose.Node,origin.Node));
        return pose;
    }
    private static AIBeacon AddNativeBeacon(Fixture f,Pose3d pose,float radius)
    {
        var beacon=f.M.Whiteboard.AddBeacon(pose,radius);
        Assert.True(PoseTreeNode.HasSameRoot(beacon.Pose.Node,f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node));
        return beacon;
    }    // R1-R3/I1/P1: 005DF10C..1A4, 0056AE96..AEF2, 005DF350..37E.
    [Fact]
    public void AdmissionClearsCacheAndStrictCarriedObjectIsTheOnlyCandidate()
    {
        using var f = new Fixture();
        var candidates = new NativeBringBeaconCandidates(f.M, f.Robot, 45f);
        f.Add(7, 200, 22);
        f.Robot.CarryingOwner!.SetCarrying(7);
        Assert.False(candidates.Refresh());
        using var beaconPose=NativeOriginPose(f);AddNativeBeacon(f,beaconPose,175f);
        Assert.True(candidates.Refresh());
        Assert.Equal(7u, Assert.Single(candidates.Entries).Id);
        f.M.Whiteboard.ClearAllBeacons();
        Assert.False(candidates.Refresh());
        Assert.Empty(candidates.Entries);
    }

    // R2: native cooldown is strict and all operands are f32 (005DF148..194).
    [Fact]
    public void RecentFailureSuppressesEvenTheCarriedCandidate()
    {
        using var f = new Fixture();
        f.Add(7, 200, 22);
        f.Robot.CarryingOwner!.SetCarrying(7);
        using var beaconPose=NativeOriginPose(f);var beacon=AddNativeBeacon(f,beaconPose,175f);
        beacon.FailedToFindLocation(1f);
        var candidates = new NativeBringBeaconCandidates(f.M, f.Robot, 45f);
        Assert.False(candidates.Refresh());
        Assert.Empty(candidates.Entries);
    }
    // FC2/FC6 and K2: 0061BDD4..BE4E, 0061C23C..25A, 0056B450..61A.
    // Reach: real native world/whiteboard; object observations are supplied fixture inputs.
    [Fact]
    public void DefinitionCountBelongsToWorldAndIsIndependentOfLocatedObjectsAndOrigins()
    {
        using var f = new Fixture();
        var world = f.M.World;
        Assert.Equal(3u, world.LightCubeDefinitionCount);
        using var origin = NativeOriginPose(f);
        AddNativeBeacon(f,origin, 175f);
        Assert.False(f.M.Whiteboard.AreAllCubesInBeacons());
        f.Add(7, 20, 22);
        Assert.False(f.M.Whiteboard.AreAllCubesInBeacons());
        f.Add(8, 50, 22);
        Assert.False(f.M.Whiteboard.AreAllCubesInBeacons());
        f.Add(9, 80, 22);
        Assert.True(f.M.Whiteboard.AreAllCubesInBeacons());
        f.Robot.Delocalize(false);
        Assert.Equal(3u, world.LightCubeDefinitionCount);
        Assert.Equal(3u, new BlockWorld().LightCubeDefinitionCount);
    }    // D1-D9: 00510D5E..80, 0056A686..6D2, 00620AB8..CE.
    // Reach: actual Robot.Delocalize and canonical whiteboard; supplied poses are fixture inputs.
    [Fact]
    public void DelocalizationClearsBeaconsAndZombiePossibleObjectsButKeepsFailures()
    {
        using var f = new Fixture();
        var board = f.M.Whiteboard;
        board.SetFailedToUse(7, ObjectActionFailure.PickUpObject);
        using var oldPose = NativeOriginPose(f);
        board.PossibleObjectStorage.Add(new(oldPose, ObjectType.Block_LIGHTCUBE1));
        AddNativeBeacon(f,oldPose, 175f);
        int renders = 0;
        board.ClearQuadRenderForTest = op => { if (op == "erase") renders++; };
        f.Robot.Delocalize(false);
        Assert.Null(board.GetActiveBeacon());
        Assert.Empty(board.PossibleObjects);
        Assert.Single(board.GetObjectFailureTable(ObjectActionFailure.PickUpObject));
        Assert.Equal(1, renders);
        f.Robot.Delocalize(false);
        Assert.Equal(2, renders);
        Assert.Single(board.GetObjectFailureTable(ObjectActionFailure.PickUpObject));
    }    // IDrive 0055B2A2..392 / Wait1 0055DE34..DF4E. Reach: real constructed native runners, no firmware completion injected.
    [Fact]
    public void DockProfileStartsCheckedAndWaitOneExistsOnlyWithDrive()
    {
        using var f=new Fixture();f.Add(7,200,22);
        var pickup=new NativeDriveToPickupBeaconRunner(f.M,7);
        var place=new NativeDriveToPlaceOnBeaconRunner(f.M,7);
        try
        {
            Assert.True(pickup.Pickup.CheckPreActionPose);Assert.True(place.Place.CheckPreActionPose);
            Assert.NotNull(pickup.DriveAndWait);Assert.NotNull(place.DriveAndWait);
            Assert.Equal(2,pickup.DriveAndWait!.Children.Count);Assert.Equal(2,place.DriveAndWait!.Children.Count);
        }
        finally{pickup.Prep();pickup.WatcherEnding();place.Prep();place.WatcherEnding();}
    }

    // PR5 00551968..1A98: no observed markers has a defined result.
    [Fact]
    public void NoObservedMarkerReturnsDefinedFailure()
    {
        using var f=new Fixture();var target=f.Add(7,200,22);
        target.LastObservedTimestamp=100;foreach(var marker in target.Markers)marker.LastObservedTimestamp=0;
        uint result=NativeDockMarkerSelection.Observed(target,f.Robot.GetCheckedMainPose(),out var selected);
        Assert.Equal(0x0300001Du,result);Assert.Null(selected);
    }    // A1/A2/G1: 0059C968, 005DEFB0, vtable 010295A4..AC; reach: shipped native factory.
    [Fact]
    public void ShippedNativeFactoryBindsBothCheckedInstances()
    {
        using var f=new Fixture();
        Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["SparksBringCubeToBeacon"]);
    }    // 0059C288: valid unrelated roots return false, through the native whiteboard consumer.
    [Fact]
    public void DifferentValidRootIsRejectedWithoutFlatPoseFallback()
    {
        using var f=new Fixture();f.Add(7,20,22);
        using var unrelated=new Pose3d("unrelated-beacon-root");
        f.M.Whiteboard.AddBeacon(unrelated,175f);
        Assert.False(f.M.Whiteboard.AreAllCubesInBeacons());
        var cube=f.M.World.GetLocatedObjectByIdHelper(7)!;
        Assert.False(f.M.Whiteboard.GetActiveBeacon()!.IsLocWithinBeacon(cube.NativePose,0));
    }

    // Manager-supplied beacon/located/carry/progression inputs; no autonomous Hiking admission asserted.
    // 005DF59C needs callback precedes floor search. Reentrant stop must prevent the old continuation.
    [Fact]
    public void ReentrantStopAtPickupNeedDoesNotQueueFloorAction()
    {
        using var f=new Fixture();f.Add(7,200,22);f.Robot.CarryingOwner!.SetCarrying(7);
        using var pose=NativeOriginPose(f);var beacon=AddNativeBeacon(f,pose,175f);
        Assert.True(PoseTreeNode.HasSameRoot(beacon.Pose.Node,f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node));
        var behavior=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        var manager=f.Robot.BehaviorManagerOwner!;using var replacementScope=new BehaviorScope();
        bool replaced=false;uint replacementTag=0;
        void OnNeed(string line)
        {
            if(replaced||!line.StartsWith("needs.action_completed PickupCube:"))return;
            replaced=true;
            // Manager.Current is intentionally null during Init; stop the actual behavior directly.
            behavior.Stop(BehaviorStopReason.Interrupted);
            behavior.StartAsync(f.Runtime.Context,replacementScope,CancellationToken.None).GetAwaiter().GetResult();
            replacementTag=behavior.CurrentRunnerTag;
        }
        f.Rig.Engine.Needs.Log+=OnNeed;
        try
        {
            Assert.True(behavior.IsRunnable(f.Runtime.Context));
            manager.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult();
            Assert.True(replaced);Assert.NotEqual(0u,replacementTag);
            Assert.Equal(replacementTag,behavior.CurrentRunnerTag);
            Assert.IsType<NativePlaceGroundAtBeaconRunner>(behavior.OwnedRunner);
        }
        finally{f.Rig.Engine.Needs.Log-=OnNeed;behavior.Stop(BehaviorStopReason.Interrupted);}
    }    // Factory 0059C968: supplied standalone context must not acquire the canonical robot owner.
    [Fact]
    public void StandaloneFactoryDoesNotBorrowNativeBehaviorOwner()
    {
        using var f=new Fixture();
        using var config=JsonDocument.Parse("{\"behaviorClass\":\"BringCubeToBeacon\",\"behaviorID\":\"Hiking_BringCubeToBeacon\",\"params\":{\"recentFailureCooldown_sec\":45.0}}");
        var inputs=new BehaviorFactoryContext{Vision=f.M.Vision,Manipulation=f.M};
        var factory=new BehaviorFactory();var container=new BehaviorContainer(factory,inputs,new Dictionary<BehaviorID,JsonElement>());
        Assert.Null(factory.CreateBehavior(container,BehaviorClass.BringCubeToBeacon,inputs,config.RootElement,out var status));
        Assert.Equal(BehaviorCreateStatus.NotImplemented,status);
        Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
    }

    // Runtime ownership reach: retain old handle after the real owner disposal.
    [Fact]
    public void RetiredRuntimeHandleCannotReenterAdmission()
    {
        using var f=new Fixture();var old=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        var context=f.Runtime.Context;var previous=f.Robot;f.Rig.Engine.Robots.RemoveRobot(1,false);
        Assert.False(old.IsRunnable(context));
        var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false);
        Assert.NotNull(replacement);Assert.NotSame(previous,replacement);
        Assert.False(old.IsRunnable(context));
    }    // PR6-PR8 00551B1E..8C: sqrt/re-square can replace an equal d2 when rounding moves the square up.
    // Reach: actual observed-marker selector, supplied native marker poses and timestamp.
    [Fact]
    public void ObservedMarkerEqualDistanceUsesNativeSqrtResquareRecurrence()
    {
        using var f=new Fixture();var obj=f.Add(7,0,0);obj.LastObservedTimestamp=100;
        foreach(var m in obj.Markers)m.LastObservedTimestamp=0;
        var a=obj.Markers[0];var b=obj.Markers[1];a.LastObservedTimestamp=100;b.LastObservedTimestamp=100;
        a.PoseOnObject.Node!.Transform=new(Rotation3d.Identity,new Vec3(608.4283447265625,170.92120361328125,0));
        b.PoseOnObject.Node!.Transform=new(Rotation3d.Identity,new Vec3(608.4283447265625,170.92120361328125,0));
        Assert.Equal(0u,NativeDockMarkerSelection.Observed(obj,f.Robot.GetCheckedMainPose(),out var selected));
        Assert.Same(b,selected);
    }
    // PR8a: two valid but unrelated marker roots leave the native selected pointer null.
    [Fact]
    public void ObservedMarkerNoWinnerIsM1SkipRatherThanDefinedBadMarker()
    {
        using var f=new Fixture();var obj=f.Add(7,0,0);obj.LastObservedTimestamp=100;
        foreach(var m in obj.Markers)m.LastObservedTimestamp=0;
        using var unrelated=new Pose3d("unrelated-marker-root");
        for(int i=0;i<2;i++){obj.Markers[i].LastObservedTimestamp=100;obj.Markers[i].PoseOnObject.SetParent(unrelated);}
        var error=Assert.Throws<NativeActionUnavailable>(()=>NativeDockMarkerSelection.Observed(obj,f.Robot.GetCheckedMainPose(),out _));
        Assert.Contains("M1-034 M12-017",error.Message);
    }    // Host-only constructor reentry guard; no action is admitted or completed.
    [Fact]
    public void StalePartiallyConstructedTreeRetiresOnlyItsOwnTags()
    {
        using var f=new Fixture();f.Add(7,200,22);int calls=0;
        var stale=new NativeDriveToPickupBeaconRunner(f.M,7,()=>++calls<3);
        uint outer=stale.Tag;uint drive=stale.Drive!.Tag;
        Assert.True(stale.RetireNeverAdmittedTree());
        Assert.False(ActionRunnerTagCounter.Global.IsInUse(outer));
        Assert.False(ActionRunnerTagCounter.Global.IsInUse(drive));
        Assert.Null(stale.Watcher);
    }    // 0055B2A2..392: inner drive failure skips Wait1; outer ignores it and leaves B9 set.
    // Reach: real ActionList/runner; deliberately absent target is the supplied world input.
    [Fact]
    public void FailedInnerDriveLeavesPickupPredockCheckEnabled()
    {
        using var f=new Fixture();var action=new NativeDriveToPickupBeaconRunner(f.M,999);
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,action,0);
        try
        {
            for(int i=0;i<20&&!action.DestructionObserved;i++)f.Rig.Tick();
            Assert.True(action.DestructionObserved);
            Assert.Equal(0x03000004u,action.Drive!.State);
            Assert.True(action.Pickup.CheckPreActionPose);
            Assert.Equal(0x03000004u,action.State);
        }
        finally{f.Robot.ActionList.Clear();}
    }    // F1/F4/F6 005DF59C..696 and 005E188C..1B56.
    // Real configured manager/ActionList, supplied beacon/carry/progression inputs and an explicit
    // finite detector, ready-link, clock and firmware model (BringBeaconFirmwareScene).
    // Post-ground observation is supplied at detector-result/world ingress, not pixel detection.
    [Fact]
    public void CanonicalCarriedFloorRunUsesRealOwnedActionsAndNeedsBeforeGround()
    {
        using var scene=new BringBeaconFirmwareScene(carrying:true){SupplyGroundDetectorResult=true};var f=scene.F;
        f.Robot.Progression!.Unlocked.Remove(10);
        using var root=NativeOriginPose(f);
        AddNativeBeacon(f,root,175f);
        var behavior=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        var order=new List<string>();
        void Need(string line){if(line.StartsWith("needs.action_completed PickupCube:"))order.Add("needs");}
        f.Rig.Engine.Needs.Log+=Need;
        var completions=new List<RobotCompletedAction>();
        using var subscription=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction a)completions.Add(a);});
        try
        {
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            var parent=Assert.IsType<NativePlaceGroundAtBeaconRunner>(behavior.OwnedRunner);
            for(int i=0;i<250&&!parent.DestructionObserved;i++){scene.Step();scene.Trace.Add(scene.Describe(parent));}
            string diagnostic=scene.Describe(parent)+" | "+string.Join(" | ",scene.Trace.TakeLast(60));
            Assert.True(parent.DestructionObserved,diagnostic);
            Assert.True(scene.Grounds==1,diagnostic);Assert.Equal(new[]{"needs"},order);
            Assert.True(scene.GroundDetectorInputs>0,diagnostic);
            Assert.True(scene.GroundTargetMarkerObservations>0,diagnostic);
            Assert.True(parent.State==0u,diagnostic);
            Assert.Contains(completions,c=>c.Tag==parent.Tag&&c.Result==0);
        }
        finally{f.Rig.Engine.Needs.Log-=Need;}
    }    // S1-S3 005E00FE..246/005DFDEC..E4A: supplied progression/carry/world inputs,
    // real configured manager and native ActionList; stop before firmware completion.
    [Fact]
    public void CanonicalStackBranchQueuesRealPlaceOnActionWithoutPickupNeed()
    {
        using var f=new Fixture();f.Add(7,200,22);f.Add(8,50,22);
        f.Robot.CarryingOwner!.SetCarrying(7);f.Robot.Progression!.Unlocked.Add(10);
        using var origin=NativeOriginPose(f);
        AddNativeBeacon(f,origin,175f);
        var behavior=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        var needs=new List<string>();void Need(string line)=>needs.Add(line);f.Rig.Engine.Needs.Log+=Need;
        try
        {
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Assert.IsType<NativeDriveToPlaceOnBeaconRunner>(behavior.OwnedRunner);
            Assert.DoesNotContain(needs,line=>line.StartsWith("needs.action_completed PickupCube:"));
            f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
            Assert.Null(behavior.OwnedRunner);
        }
        finally{f.Rig.Engine.Needs.Log-=Need;}
    }    // S3/S4 005DFDEC..E4A and 005E1434..1680. Explicit firmware/detector/ready-link/clock scene inputs;
    // real configured manager, ActionList, child runners and local 0x5A completions.
    [Theory]
    [InlineData(0,1)] [InlineData(1,2)]
    public void CanonicalStackCompletesAndRetriesActualChildAction(int failedPlacements,int expectedPlacements)
    {
        using var scene=new StackFirmwareScene(true,"Hiking_BringCubeToBeacon"){FailPlacements=failedPlacements};var f=scene.F;
        using var beacon=new Pose3d(Rotation3d.Identity,new Vec3(400,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        AddNativeBeacon(f,beacon,175f);
        var behavior=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        var needs=new List<string>();void Need(string line){if(line.StartsWith("needs.action_completed StackCube:"))needs.Add("stack");}
        f.Rig.Engine.Needs.Log+=Need;var completions=new List<RobotCompletedAction>();
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction a)completions.Add(a);});
        try
        {
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Assert.IsType<NativeDriveToPlaceOnBeaconRunner>(behavior.OwnedRunner);
            for(int i=0;i<350&&needs.Count==0;i++)scene.Step();
            Assert.Equal(expectedPlacements,scene.Placements);
            Assert.Equal(new[]{"stack"},needs);
            Assert.Contains(completions,c=>c.ActionType==0x19&&c.Result==0);
            if(failedPlacements!=0)Assert.Contains(completions,c=>(c.Result>>24)==4);
        }
        finally{f.Rig.Engine.Needs.Log-=Need;}
    }    // F3 005E0B84..C32: current-origin intersection checks all families, ignores carried ID,
    // and unavailable virtual footprints are host unavailability, not NoFreePoses.
    [Fact]
    public void StrictFloorQueryUsesCurrentOriginAndDoesNotInventNoncubeFootprints()
    {
        using var f=new Fixture();var carried=f.Add(7,200,22);
        using var candidate=new Pose3d(Rotation3d.Identity,new Vec3(0,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        var query=new NativeBringBeaconGeometry(f.M,f.Robot);
        Assert.True(query.Free(carried,candidate,()=>true));
        var obstacle=f.Add(8,0,22);Assert.False(query.Free(carried,candidate,()=>true));
        using var clean=new Fixture();var cleanCarried=clean.Add(7,200,22);
        using var cleanCandidate=NativeOriginPose(clean);
        var charger=new ObservableObject(9,ObjectType.Charger_Basic,Array.Empty<KnownMarker>())
        {Pose=new Pose3d(Rotation3d.Identity,new Vec3(0,0,0),parent:clean.Robot.GetNativeOrigin(clean.Robot.CurrentOriginId)),OriginId=clean.Robot.CurrentOriginId,PoseState=PoseState.Known};
        clean.M.World.AddLocatedObject(charger);
        Assert.NotNull(clean.M.World.GetLocatedObjectByIdHelper(9));Assert.Null(clean.M.World.GetLocatedObjectByIdHelper(8));
        Assert.Throws<NativeActionUnavailable>(()=>new NativeBringBeaconGeometry(clean.M,clean.Robot).Free(cleanCarried,cleanCandidate,()=>true));
        Assert.Empty(clean.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PlaceObjectAt));
    }    // F3 005E0BEA..C32 / BlockWorld origin filter: an old-origin obstacle does not block a current-origin candidate.
    // Reach: direct strict query with supplied retained old-origin object graph.
    [Fact]
    public void StrictFloorQueryDoesNotConsiderOtherOriginBucket()
    {
        using var f=new Fixture();uint oldId=f.Robot.CurrentOriginId;
        using var oldRoot=new Pose3d(Rotation3d.Identity,Vec3.Zero,parent:f.Robot.GetNativeOrigin(oldId));
        f.Robot.Delocalize(false);var carried=f.Add(7,200,22);
        var oldObstacle=new ObservableObject(8,ObjectType.Block_LIGHTCUBE2,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE2))
        {Pose=new Pose3d(Rotation3d.Identity,new Vec3(0,0,22),parent:oldRoot),OriginId=oldId,PoseState=PoseState.Known};
        f.M.World.AddLocatedObject(oldObstacle);
        using var candidate=new Pose3d(Rotation3d.Identity,new Vec3(0,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        Assert.True(new NativeBringBeaconGeometry(f.M,f.Robot).Free(carried,candidate,()=>true));
    }    // Host stale at QueueAction entry: nested/private reservations retire without watcher/completion.
    [Fact]
    public void AlreadyStaleQueueAdmissionRetiresEntireNeverQueuedSubtree()
    {
        using var f=new Fixture();f.Add(7,200,22);
        var action=new NativeDriveToPickupBeaconRunner(f.M,7);
        var tags=new[]{action.Tag,action.DriveAndWait!.Tag,action.Drive!.Tag,action.Drive.Sequence.Tag,action.Pickup.Tag};
        var completed=new List<uint>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction a)completed.Add(a.Tag);});
        Assert.Equal(2,f.Robot.ActionList.QueueActionGuardedNow(action,0,()=>false));
        Assert.All(tags,tag=>{Assert.False(ActionRunnerTagCounter.Global.IsInUse(tag));Assert.DoesNotContain(tag,completed);});
        Assert.Null(action.Watcher);Assert.True(action.DestructionObserved);
    }    private sealed class ReplacementAction:ActionRunner
    {
        internal ReplacementAction():base(8,0){}
        protected override string ActionName=>"replacement-test";
        public override bool CanInterrupt()=>false;
        public override uint CheckIfDone()=>EngineActionResult.Running;
    }
    // Real RobotManager removal/replacement while Bring owns a queued action;
    // supplied beacon/world input and offline ready-link, no firmware completion injected.
    [Fact]
    public void RealRobotRemovalRetiresActiveOldOwnerWithoutTouchingReplacementAction()
    {
        using var f=new Fixture();f.Add(7,300,22);
        using var root=NativeOriginPose(f);AddNativeBeacon(f,root,175f);
        var old=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        var context=f.Runtime.Context;var previous=f.Robot;
        Assert.True(previous.BehaviorManagerOwner!.StartAsync(old.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.IsType<NativeDriveToPickupBeaconRunner>(old.OwnedRunner);
        f.Rig.Engine.Robots.RemoveRobot(1,false);
        var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;Assert.NotSame(previous,replacement);
        var survivor=new ReplacementAction();replacement.ActionList.QueueAction(QueueActionPosition.AtEnd,survivor);
        Assert.False(old.IsRunnable(context));Assert.False(survivor.DestructionObserved);
        Assert.Same(survivor,replacement.ActionList.GetCurrentAction());
    }    // PR5-PR8a / M1-034 M12-017: direct native leaf Init with supplied marker graphs;
    // this is a unit boundary, not a claimed completed ActionList run.
    [Fact]
    public void PickupLeafNoMarkerWinnerSkipsWithoutFailureMemory()
    {
        using var f=new Fixture();using var executionLease=f.Rig.Engine.EnterSynchronousOwner();var obj=f.Add(7,150,22);obj.LastObservedTimestamp=100;
        foreach(var marker in obj.Markers)marker.LastObservedTimestamp=0;
        using var unrelated=new Pose3d("unrelated-marker-root");
        for(int i=0;i<2;i++){obj.Markers[i].LastObservedTimestamp=100;obj.Markers[i].PoseOnObject.SetParent(unrelated);}
        var leaf=new NativePickupObjectRunner(f.M,7);
        try
        {
            Assert.Throws<NativeActionUnavailable>(()=>leaf.Init());
            Assert.Empty(f.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PickUpObject));
        }
        finally{leaf.Prep();leaf.WatcherEnding();}
    }    // PR1-PR5 0055160C..1A98: direct native pickup leaf Init, supplied timestamp/marker input.
    // The world-selected ID is the native preaction-query side effect, not a fabricated action completion.
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PickupLeafExecutesTheSelectedPredockProfile(bool check)
    {
        using var f=new Fixture();using var executionLease=f.Rig.Engine.EnterSynchronousOwner();var obj=f.Add(7,150,22);obj.LastObservedTimestamp=100;
        foreach(var marker in obj.Markers)marker.LastObservedTimestamp=0;
        obj.Markers[0].LastObservedTimestamp=100;
        f.M.World.SelectedObjectId=123;
        var leaf=new NativePickupObjectRunner(f.M,7,checkPreActionPose:check);
        try
        {
            _=leaf.Init();
            if(check)Assert.Equal(7u,f.M.World.SelectedObjectId);
            else
            {
                Assert.Equal(123u,f.M.World.SelectedObjectId);
                var field=typeof(NativePickupObjectRunner).GetField("_marker",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
                Assert.Equal(unchecked((short)obj.Markers[0].Code),(short)field.GetValue(leaf)!);
            }
        }
        finally{leaf.Prep();leaf.WatcherEnding();}
    }    // F1/F5/I2 005DF59C..73C and 005DF382..3DA: actual manager and shared failure memory;
    // supplied radius-one beacon/carry/clock inputs force the one source grid candidate to fail.
    [Fact]
    public void NativeNoFreePoseStampsBeaconAndMoodAfterPickupNeedWithoutAction()
    {
        using var f=new Fixture();f.Add(7,200,22);f.Robot.CarryingOwner!.SetCarrying(7);f.Robot.Progression!.Unlocked.Remove(10);
        using var pose=NativeOriginPose(f);
        var beacon=AddNativeBeacon(f,pose,1f);f.M.Whiteboard.SetFailedToUse(99,ObjectActionFailure.PlaceObjectAt,pose);
        var savedFailure=Assert.Single(f.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PlaceObjectAt)[99]);
        Assert.True(PoseTreeNode.HasSameRoot(savedFailure.Pose.Node,f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node));
        var behavior=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        var events=(Dictionary<string,float>)typeof(MoodState).GetField("_lastEventSec",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(f.Runtime.Context.Mood)!;
        var order=new List<string>();void Need(string line){if(line.StartsWith("needs.action_completed PickupCube:")){Assert.False(events.ContainsKey("HikingNoLocationAtBeacon"));order.Add("needs");}}
        f.Rig.Engine.Needs.Log+=Need;
        try
        {
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Assert.Equal(new[]{"needs"},order);Assert.Null(behavior.OwnedRunner);
            Assert.Equal(f.Rig.Engine.Timer.SecondsF,beacon.FailedToFindLocationTimeSec);
            Assert.True(events.ContainsKey("HikingNoLocationAtBeacon"));
        }
        finally{f.Rig.Engine.Needs.Log-=Need;}
    }    // F3/F5: unsupported current-origin footprint is host unavailability, not native NoFreePoses.
    // Actual configured manager with supplied carried cube, beacon and noncube object input.
    [Fact]
    public void MissingFootprintDoesNotStampFailureOrEmitNoLocationMood()
    {
        using var f=new Fixture();f.Add(7,200,22);f.Robot.CarryingOwner!.SetCarrying(7);f.Robot.Progression!.Unlocked.Remove(10);
        var charger=new ObservableObject(9,ObjectType.Charger_Basic,Array.Empty<KnownMarker>())
        {Pose=new Pose3d(Rotation3d.Identity,new Vec3(300,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known};
        f.M.World.AddLocatedObject(charger);
        using var pose=NativeOriginPose(f);var beacon=AddNativeBeacon(f,pose,175f);
        var behavior=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        SteppedBehavior.ResetMissingForTests();var missing=new List<string>();void Capture(string value)=>missing.Add(value);SteppedBehavior.MissingReported+=Capture;
        try
        {
            f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult();
            Assert.Null(behavior.OwnedRunner);Assert.Equal(0f,beacon.FailedToFindLocationTimeSec);
            Assert.Contains(missing,x=>x.Contains("native obstacle footprint unavailable"));
            var events=(Dictionary<string,float>)typeof(MoodState).GetField("_lastEventSec",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(f.Runtime.Context.Mood)!;
            Assert.False(events.ContainsKey("HikingNoLocationAtBeacon"));
        }
        finally{SteppedBehavior.MissingReported-=Capture;}
    }    // S4/K1 callback boundary; actual stack action and offline firmware scene, supplied absent mood owner.
    [Fact]
    public void MoodDiagnosticReentryKeepsReplacementActionOwned()
    {
        using var scene=new StackFirmwareScene(true,"Hiking_BringCubeToBeacon");var f=scene.F;
        using var beacon=new Pose3d(Rotation3d.Identity,new Vec3(400,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        AddNativeBeacon(f,beacon,175f);f.Runtime.Context.Mood=null;
        var behavior=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        var replacement=new ReplacementAction();bool entered=false;
        SteppedBehavior.ResetMissingForTests();
        void Missing(string message)
        {
            if(entered||!message.Contains("actual MoodManager unavailable"))return;
            entered=true;f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
            f.Robot.ActionList.QueueAction(QueueActionPosition.AtEnd,replacement);
        }
        SteppedBehavior.MissingReported+=Missing;
        try
        {
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            for(int i=0;i<350&&!entered;i++)scene.Step();
            Assert.True(entered,string.Join(" | ",scene.Trace.TakeLast(30)));
            Assert.False(replacement.DestructionObserved);Assert.Same(replacement,f.Robot.ActionList.GetCurrentAction());
        }
        finally{SteppedBehavior.MissingReported-=Missing;}
    }    // PR1-PR5 0055160C..1A98: direct native PlaceRel leaf Init with supplied carried and marker inputs.
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PlaceRelativeLeafExecutesTheSelectedPredockProfile(bool check)
    {
        using var f=new Fixture();using var executionLease=f.Rig.Engine.EnterSynchronousOwner();f.Add(7,300,22);var obj=f.Add(8,150,22);f.Robot.CarryingOwner!.SetCarrying(7);
        obj.LastObservedTimestamp=100;foreach(var marker in obj.Markers)marker.LastObservedTimestamp=0;obj.Markers[0].LastObservedTimestamp=100;
        f.M.World.SelectedObjectId=123;var leaf=new NativePlaceRelRunner(f.M,8){CheckPreActionPose=check};
        try
        {
            _=leaf.Init();
            if(check)Assert.Equal(8u,f.M.World.SelectedObjectId);
            else
            {
                Assert.Equal(123u,f.M.World.SelectedObjectId);
                var field=typeof(NativePlaceRelRunner).GetField("_marker",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
                Assert.Equal(unchecked((short)obj.Markers[0].Code),(short)field.GetValue(leaf)!);
            }
        }
        finally{leaf.Prep();leaf.WatcherEnding();}
    }    // Host-only synchronous constructor-boundary seam; real configured manager/action ownership.
    [Fact]
    public void RestartAtActionConstructionRetiresStaleTreeAndPreservesReplacement()
    {
        using var f=new Fixture();f.Add(7,300,22);
        using var root=NativeOriginPose(f);AddNativeBeacon(f,root,175f);
        var behavior=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        var manager=f.Robot.BehaviorManagerOwner!;uint staleTag=0;bool entered=false;
        behavior.ActionConstructedForTest=runner=>
        {
            if(entered)return;entered=true;staleTag=runner.Tag;
            manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
            manager.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult();
        };
        try
        {
            manager.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult();
            Assert.True(entered);Assert.False(ActionRunnerTagCounter.Global.IsInUse(staleTag));
            var replacement=Assert.IsType<NativeDriveToPickupBeaconRunner>(behavior.OwnedRunner);
            Assert.NotEqual(staleTag,replacement.Tag);Assert.False(replacement.DestructionObserved);
        }
        finally{behavior.ActionConstructedForTest=null;manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);}
    }    // M15-022 00554794/005549B0: real Ground leaf on the canonical manager/ActionList path.
    // Detector/clock/ready-link/firmware are the explicit BringBeaconFirmwareScene inputs;
    // raw robot state is deliberately withdrawn only after the real ground leaf starts.
    [Fact]
    public void GroundLeafUnavailableRetiresOnlyOwnedRunWithoutFalseCompletionOrFailureMemory()
    {
        using var scene=new BringBeaconFirmwareScene(carrying:true);var f=scene.F;
        f.Robot.Progression!.Unlocked.Remove(10);
        using var root=NativeOriginPose(f);AddNativeBeacon(f,root,175f);
        var behavior=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        var manager=f.Robot.BehaviorManagerOwner!;
        Assert.True(manager.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var parent=Assert.IsType<NativePlaceGroundAtBeaconRunner>(behavior.OwnedRunner);
        for(int i=0;i<250&&!parent.Ground.Initialized&&!parent.DestructionObserved;i++){scene.Step(()=>parent.Ground.Initialized||parent.DestructionObserved);scene.Trace.Add(scene.Describe(parent));}
        Assert.True(parent.Ground.Initialized,scene.Describe(parent)+" | "+string.Join(" | ",scene.Trace.TakeLast(60)));
        Assert.False(parent.DestructionObserved);
        var stateProperty=typeof(EngineRobot).GetProperty("StoredState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        var saved=f.Robot.StoredState;var replacement=new ReplacementAction();bool missing=false;
        var completions=new List<RobotCompletedAction>();
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction a)completions.Add(a);});
        SteppedBehavior.ResetMissingForTests();
        void OnMissing(string text)
        {
            if(missing||!text.Contains("Ground actual raw robot state unavailable"))return;
            missing=true;f.Robot.ActionList.QueueAction(QueueActionPosition.AtEnd,replacement);
        }
        SteppedBehavior.MissingReported+=OnMissing;
        try
        {
            stateProperty.SetValue(f.Robot,null);
            f.Robot.ActionList.Update();
            Assert.True(missing);Assert.True(parent.DestructionObserved);Assert.Null(behavior.OwnedRunner);
            Assert.DoesNotContain(completions,a=>a.Tag==parent.Tag&&a.Result==0);
            Assert.Empty(f.M.Whiteboard.GetObjectFailureTable(ObjectActionFailure.PlaceObjectAt));
            Assert.False(replacement.DestructionObserved);Assert.Same(replacement,f.Robot.ActionList.GetCurrentAction());
        }
        finally{stateProperty.SetValue(f.Robot,saved);SteppedBehavior.MissingReported-=OnMissing;}
    }    private sealed class DriveIdentityProbe:NativeDriveToObjectRunner
    {
        internal DriveIdentityProbe(ManipulationSystem m,uint target):base(m,target){}
        internal int RunInitHelper(ObservableObject supplied)=>InitHelper(supplied);
    }
    // ID2-ID4: 00559366..005593B6 and 00633F88..9E. Real InitHelper/turn construction;
    // the in-position pose-function result is a supplied unit seam, not a planner completion.
    [Theory]
    [InlineData(false,true,0)]  // bottom=7, empty top=-1, supplied clone=-1: skip
    [InlineData(true,true,1)]   // bottom=7, top=8, supplied clone=-1: turn to action target 7
    [InlineData(true,false,0)]  // supplied ordinary top object 8: skip
    [InlineData(false,false,1)] // supplied ordinary object 9: turn to action target 7
    public void DriveInitHelperUsesSuppliedIdentityForBothCarrySlotsAndActionTargetForTurn(bool topOccupied,bool clone,int turns)
    {
        using var f=new Fixture();using var owner=f.Rig.Engine.EnterSynchronousOwner();
        var bottom=f.Add(7,200,22);var top=f.Add(8,300,22);var other=f.Add(9,400,22);
        using var liftPose=new Pose3d();using var topPose=new Pose3d();
        if(topOccupied)f.Robot.CarryingOwner!.AttachToLift(7,bottom.Markers[0],liftPose,8,topPose);
        else f.Robot.CarryingOwner!.SetCarrying(7);
        ObservableObject supplied;
        if(clone)
        {
            supplied=bottom.CloneNative();using var pose=new Pose3d(bottom.NativePose);
            supplied.InitPose(pose,PoseState.Known);Assert.Equal(uint.MaxValue,supplied.ObjectId);
        }
        else supplied=topOccupied?top:other;
        var runner=new DriveIdentityProbe(f.M,7);int seen=-1;uint? turnTarget=null;
        runner.SetPosesFunction((ObservableObject _,out IReadOnlyList<PreActionPose> poses,ref bool inPosition)=>
        {poses=Array.Empty<PreActionPose>();inPosition=true;return ActionResult.Success;});
        runner.BeforeInitialUpdateForTest=children=>
        {
            var found=children.OfType<NativeTurnTowardsObjectRunner>().ToArray();seen=found.Length;
            if(found.Length!=0)turnTarget=(uint)typeof(NativeTurnTowardsObjectRunner).GetField("_id",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(found[0])!;
        };
        try
        {
            _=runner.RunInitHelper(supplied);
            Assert.Equal(turns,seen);if(turns!=0)Assert.Equal(7u,turnTarget);
        }
        finally
        {
            runner.BeforeInitialUpdateForTest=null;runner.Prep();runner.WatcherEnding();
            if(clone)supplied.NativePose.Dispose();f.Robot.CarryingOwner!.UnsetCarrying();
        }
    }}
