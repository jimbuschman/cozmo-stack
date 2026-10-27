using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PickupPyramidPilotTests
{
    private static string Resources()
    {
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null;dir=dir.Parent)
        {var path=Path.Combine(dir.FullName,"re-analysis","obb","assets","cozmo_resources");if(File.Exists(Path.Combine(path,"config","engine","behaviorSystem","activities_config.json")))return path;}
        throw new InvalidOperationException("Shipped pickup fixture resources absent");
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new(Resources());
        internal EngineRobot Native=>Rig.Engine.Robot!;
        internal NativeBehaviorRuntime Runtime=>Native.NativeBehaviorRuntime!;
        internal ManipulationSystem M=>Runtime.Manipulation!;
        internal NativePickUpCubeBehavior Pickup=>Assert.IsType<NativePickUpCubeBehavior>(Runtime.Bound["SparksPickupSingleCubeForPyramid"]);
        internal Fixture(){Rig.ToSynced();Rig.State();}
        internal ObservableObject Add(uint id,double x,double y=0,double z=22)
        {
            var obj=new ObservableObject(id,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {Pose=new Pose3d(Rotation3d.Identity,new Vec3(x,y,z),parent:Native.GetNativeOrigin(Native.CurrentOriginId)),PoseState=PoseState.Known,OriginId=Native.CurrentOriginId,LastObservedTimestamp=Native.StoredState!.Timestamp};
            M.World.AddLocatedObject(obj);return obj;
        }
        public void Dispose()=>Rig.Dispose();
    }
    [Fact]
    public void ActualConfiguredFactoryUsesNativeTargetAndActualUnlockState()
    {
        using var f=new Fixture();f.Add(7,100);
        var pickup=f.Pickup;
        f.Native.Progression!.Unlocked.Clear();
        Assert.False(pickup.IsRunnable(f.Runtime.Context));
        f.Native.Progression.Unlocked.Add(6); // explicit actual-owner state input, not a constant eligibility delegate.
        Assert.True(pickup.IsRunnable(f.Runtime.Context));Assert.Equal(7u,pickup.Target);
        Assert.False(pickup.HasCurrentAction);Assert.Null(f.Native.ActionList.GetCurrentAction());
        f.M.Docking.Carrying.SetCarrying(7);
        Assert.False(pickup.IsRunnable(f.Runtime.Context));
        // Specialized activity remains guarded; this fixture never substitutes a whole activity admission.
        Assert.Null(f.Native.BehaviorManagerOwner!.Activity);
    }
    [Fact]
    public void IntentionZeroUsesSignedTieOrderAndRetainsEligibleBestWithinAndBeyondEpsilon()
    {
        using var f=new Fixture();var first=f.Add(0x80000001,100,100);f.Add(1,100,-100);
        var cache=f.Runtime.PickupTargets!;
        Assert.True(cache.Refresh());Assert.Equal(new uint[]{0x80000001,1},cache.ValidIds);Assert.Equal(first.ObjectId,cache.Best);
        first.Pose=new Pose3d(Rotation3d.Identity,new Vec3(500,500,22),parent:f.Native.GetNativeOrigin(f.Native.CurrentOriginId));
        f.Rig.Tick(.001);Assert.True(cache.Refresh());Assert.Equal(first.ObjectId,cache.Best);
        f.Rig.Tick(1);Assert.True(cache.Refresh());Assert.Equal(first.ObjectId,cache.Best); // eligible old best outranks new distance.
        f.M.Docking.Carrying.SetCarrying(first.ObjectId);
        Assert.True(cache.Refresh());Assert.Equal(first.ObjectId,cache.Best); // same captured float clock suppresses rebuild.
        f.Rig.Tick(1);Assert.True(cache.Refresh());Assert.Equal(1u,cache.Best);Assert.Equal(new uint[]{1},cache.ValidIds);
    }
    [Fact]
    public void ActualNativePredicateRejectsObjectOnTopAndHeightRatherThanManualHistoryGate()
    {
        using var f=new Fixture();var bottom=f.Add(7,100);var upper=f.Add(8,100,z:66);
        var cache=f.Runtime.PickupTargets!;
        Assert.False(cache.CanPick(bottom));Assert.True(cache.CanPick(upper));
        upper.Pose=new Pose3d(Rotation3d.Identity,new Vec3(100,0,100),parent:f.Native.GetNativeOrigin(f.Native.CurrentOriginId));
        Assert.False(cache.CanPick(upper));
    }
    private static ActionResult Verify(PickupObjectAction action,ObservableObject? obj,bool succeeded=true)=>(ActionResult)typeof(PickupObjectAction).GetMethod("Verify",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(action,new object?[]{obj,new DockResult(0,succeeded,0,default)})!;
    [Fact]
    public void RealScopedLiftLoadStartsUnknownResetsAndUsesRawStoredTimestampEvenWhenOriginIsRejected()
    {
        using var f=new Fixture();var cube=f.Add(7,100);using var action=new PickupObjectAction(f.M,7);
        using(var registration=f.M.Docking.RegisterActionHandlers(action,DockAction.PickupLow))
        {
            Assert.Equal(0,action.LiftLoadState);
            var state=f.Rig.MakeState(origin:999);state.Timestamp=1000;f.Rig.Data(state);f.Rig.Tick();
            Assert.Equal(1000u,f.Native.StoredState!.Timestamp);
            Assert.Equal(ActionResult.Running,Verify(action,cube));Assert.Equal(1000u,action.VerifyStartedAt);Assert.Equal(1500u,action.LoadDeadline);
            state=f.Rig.MakeState(origin:999);state.Timestamp=1501;f.Rig.Data(state);f.Rig.Tick();
            Assert.Equal(ActionResult.Running,Verify(action,cube));Assert.Equal(1000u,action.VerifyStartedAt);
            Assert.Contains(action.Trace,line=>line.Contains("deadline expired"));
            f.Rig.Engine.Broadcast(new LiftLoad{Field0=false});Assert.Equal(2,action.LiftLoadState);
        }
        using var second=f.M.Docking.RegisterActionHandlers(action,DockAction.PickupLow);
        Assert.Equal(0,action.LiftLoadState);
    }
    // Reach: supplied-entry unit for the checked Verify tail; no child-turn completion or full pickup is claimed.
    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(true,true)]
    public void VerifyTailQueriesAllCurrentTypeMatchesAndRepairsDifferentIdBeforeActualUnset(bool match,bool differentId)
    {
        using var f=new Fixture();var carried=f.Add(7,100);using var action=new PickupObjectAction(f.M,7);
        object?[] args={carried,default(DockAction)};
        Assert.Equal(ActionResult.Success,(ActionResult)typeof(PickupObjectAction).GetMethod("SelectDockActionResult",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(action,args)!);
        if(!match||differentId)carried.Pose=new Pose3d(Rotation3d.Identity,new Vec3(500,0,22),parent:f.Native.GetNativeOrigin(f.Native.CurrentOriginId));
        if(differentId)f.Add(8,100);
        f.M.Docking.Carrying.SetCarrying(7);
        int mark=f.Rig.Mark();
        var result=(ActionResult)typeof(PickupObjectAction).GetMethod("VerifyCarrying",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(action,null)!;
        Assert.Equal(match?ActionResult.PickupRetry:ActionResult.Success,result);
        if(match)
        {
            Assert.False(f.M.Docking.Carrying.IsCarryingObject);
            Assert.Contains(f.Rig.SentSince(mark),message=>message is CarryStateUpdate update&&update.State==CarryState.CARRY_NONE);
            if(differentId){Assert.Null(f.M.World.GetLocatedObjectByIdHelper(8));Assert.Equal(100f,(float)carried.NativePose.Translation.X);}
            else Assert.Same(carried,f.M.World.GetLocatedObjectByIdHelper(7));
        }
        else Assert.True(f.M.Docking.Carrying.IsCarrying(7));
    }

    [Fact]
    public void FailedDockBypassesUnknownLoadPrefixAndHeightUsesCapturedMainParentFrame()
    {
        using var f=new Fixture();var cube=f.Add(7,100);using var action=new PickupObjectAction(f.M,7);
        using var parent=new Pose3d(Rotation3d.Identity,new Vec3(0,0,100),parent:f.Native.GetNativeOrigin(f.Native.CurrentOriginId));
        cube.Pose=new Pose3d(Rotation3d.Identity,new Vec3(100,0,0),parent:parent);
        object?[] args={cube,default(DockAction)};
        Assert.Equal(ActionResult.Success,(ActionResult)typeof(PickupObjectAction).GetMethod("SelectDockActionResult",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(action,args)!);
        Assert.Equal(DockAction.PickupHigh,(DockAction)args[1]!); //savedF8.Z100 minusMAIN.Z0; original localtargetZ0 would choose LOW.
        Assert.Equal(0,action.LiftLoadState);
        Assert.Equal(ActionResult.Running,Verify(action,cube,false)); //only the owned turn remains, no fake failure/success.
        Assert.Equal(0u,action.LoadDeadline);Assert.Equal(f.Native.StoredState!.Timestamp,action.VerifyStartedAt);
    }

    [Fact]
    public void NativeCacheNeverBorrowsStandaloneFailureMemoryAndRetainsPriorPublicationOnUnknownOwner()
    {
        using var f=new Fixture();var cube=f.Add(7,100);var cache=f.Runtime.PickupTargets!;
        using(var manual=ManipulationSystem.CreateStandalone(f.Rig.Robot,f.M.Vision,()=>1000))
        {
            manual.Whiteboard.SetFailedToUse(cube,ObjectActionFailure.PickUpObject);
            Assert.True(cache.Refresh());Assert.Equal(7u,cache.Best);
        }
        var carry=f.Native.CarryingOwner;
        try
        {
            f.Native.CarryingOwner=null;f.Rig.Tick(1);
            Assert.False(cache.Refresh());Assert.Equal(7u,cache.Best);Assert.Equal(new uint[]{7},cache.ValidIds);
        }
        finally{f.Native.CarryingOwner=carry;}
        f.M.Whiteboard.SetFailedToUse(cube,ObjectActionFailure.PickUpObject);f.Rig.Tick(1);
        Assert.True(cache.Refresh());Assert.Equal(uint.MaxValue,cache.Best);Assert.Empty(cache.ValidIds);
    }
    private sealed class ProbeParent:SteppedBehavior
    {
        internal Action<ProbeParent>? Entry;
        internal ProbeParent():base("pickup-parent-unit","Probe"){}
        public override bool IsRunnable(BehaviorContext context)=>true;
        protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
        protected override void OnStart()=>Entry?.Invoke(this);
    }
    private sealed class ControlledRunner(uint result):ActionRunner(22,0)
    {
        protected override string ActionName=>"supplied-unit-pickup-result";
        public override uint CheckIfDone()=>result;
        public override bool CanInterrupt()=>false;
    }
    [Fact]
    public void ActualManagerAvailabilityGuardRetainsPriorActionBeforeUnimplementedPickupExecution()
    {
        using var f=new Fixture();var manager=f.Native.BehaviorManagerOwner!;
        var parent=new ProbeParent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Native);
        parent.Entry=p=>p.StartActing(new ControlledRunner(EngineActionResult.Running),null);
        manager.Add(parent);
        Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var before=f.Native.ActionList.GetCurrentAction();Assert.NotNull(before);
        Assert.False(manager.StartAsync(f.Pickup.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Same(parent,manager.Current);Assert.Same(before,f.Native.ActionList.GetCurrentAction());Assert.True(parent.HasCurrentAction);
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);parent.RetireActionCompletionOwner();
    }
    [Fact]
    public void PublicNativeObserverScoringRetainsPriorActionWhenPickupExecutionIsUnavailable()
    {
        using var f=new Fixture();using var observer=FreeplayStack.CreateNative(f.Rig.Robot);
        var manager=observer.Manager;Assert.Same(f.Native.BehaviorManagerOwner,manager);
        f.Add(7,100);f.Native.Progression!.Unlocked.Add(6);
        var pickup=Assert.IsType<NativePickUpCubeBehavior>(observer.Bound["SparksPickupSingleCubeForPyramid"]);
        Assert.Same(f.Pickup,pickup);Assert.True(pickup.IsRunnable(observer.Context));
        var oldScore=pickup.Score;pickup.Score=1; // Explicit public host-ranking input; no native activity score is claimed.
        var parent=new ProbeParent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Native);
        parent.Entry=p=>p.StartActing(new ControlledRunner(EngineActionResult.Running),null);
        manager.Add(parent);
        try
        {
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            var before=f.Native.ActionList.GetCurrentAction();Assert.NotNull(before);
            manager.Add(pickup);
            var selection=manager.ChooseAndSwitch(f.Rig.Engine.Timer.SecondsF);
            Assert.Equal(parent.Id,selection.Chosen);
            Assert.Contains("unavailable",selection.Reason);
            Assert.Contains(selection.Scores,row=>row.Item1==pickup.Id&&row.Item2>0);
            Assert.Same(parent,manager.Current);Assert.Same(before,f.Native.ActionList.GetCurrentAction());Assert.True(parent.HasCurrentAction);
        }
        finally
        {pickup.Score=oldScore;manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);parent.RetireActionCompletionOwner();}
    }
    [Fact]
    public void SourceResponseWrapperUsesRealParentTagActionListAndPreservesSuppliedOriginalPayload()
    {
        // Supplied original child result unit; actual native pickup/visual execution is unavailable.
        using var f=new Fixture();var manager=f.Native.BehaviorManagerOwner!;
        var parent=new ProbeParent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Native);
        var helper=new NativePickupBlockHelper(f.M,parent,f.Runtime.AI.Helpers.Factory,7,0,false,true);
        RobotCompletedAction? received=null;
        var original=new ControlledRunner(0x03000004);
        parent.Entry=_=>helper.StartWithResponse(original,completed=>received=completed);
        manager.Add(parent);
        Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Equal(original.Tag,parent.CurrentRunnerTag);Assert.Same(original,f.Native.ActionList.GetCurrentAction());
        for(int i=0;i<8&&received is null;i++)f.Rig.Tick(10);
        Assert.NotNull(received);Assert.Equal(original.Tag,received!.Value.Tag);Assert.Equal(0x03000004u,received.Value.Result);
        Assert.False(parent.HasCurrentAction);
        parent.RetireActionCompletionOwner();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void StoredPoseTurnPrecedesComparisonReparentAndParentCancellationPreventsMutation(bool cancelParent)
    {
        // Supplied terminal child input; actual VerifyAsync and world/carry recipients are used.
        using var f=new Fixture();var cube=f.Add(7,100);using var action=new PickupObjectAction(f.M,7);
        f.M.Vision.Calibration=CameraCalibration.Nominal();
        object?[] args={cube,default(DockAction)};
        Assert.Equal(ActionResult.Success,(ActionResult)typeof(PickupObjectAction).GetMethod("SelectDockActionResult",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(action,args)!);
        f.M.Docking.Carrying.SetCarrying(7);
        using var changedParent=new Pose3d(Rotation3d.Identity,new Vec3(500,0,0),parent:f.Native.GetNativeOrigin(f.Native.CurrentOriginId));
        using var cancel=new CancellationTokenSource();
        bool visited=false;
        f.M.Vision.TurnOverride=(pose,angle,ct)=>
        {
            visited=true;Assert.Same(cube.NativePose.Node!.Parent,pose.Node!.Parent);Assert.Equal(0,angle);
            if(cancelParent)cancel.Cancel();else f.Native.GetCheckedMainPose().SetParent(changedParent);
            return Task.FromResult(false); //ordinary child failure is ignored; parent cancellation is not.
        };
        var task=(Task<ActionResult>)typeof(PickupObjectAction).GetMethod("VerifyAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(action,new object?[]{cube,new DockResult(0,false,0,default),cancel.Token})!;
        var result=task.GetAwaiter().GetResult(); //this explicit child input completes synchronously; join before all inputs retire.
        Assert.True(visited);Assert.Equal(cancelParent?ActionResult.CancelledWhileRunning:ActionResult.Success,result);
        Assert.True(f.M.Docking.Carrying.IsCarrying(7));Assert.Same(cube,f.M.World.GetLocatedObjectByIdHelper(7));
        Assert.Equal(100f,(float)cube.NativePose.Translation.X);
    }

    [Fact]
    public void IgnoredNativeMembershipCollectsFreshCandidatesEvenWhenIntentionCacheIsWithinEpsilon()
    {
        //Actual configured target method/native producer; world insertion and direct frame-tail update are supplied unit entries.
        using var f=new Fixture();f.Add(7,100);var cache=f.Runtime.PickupTargets!;
        Assert.True(cache.Refresh());Assert.Equal(new uint[]{7},cache.ValidIds);
        f.Add(8,152);f.Add(9,0,200);f.Runtime.Configurations.Update();
        Assert.Contains(f.M.NativePyramidBases,basis=>basis.MinId==7&&basis.MaxId==8);
        f.Native.Progression!.Unlocked.Add(6);
        Assert.True(f.Pickup.IsRunnable(f.Runtime.Context));Assert.Equal(9u,f.Pickup.Target);
        Assert.Equal(new uint[]{7},cache.ValidIds);Assert.Equal(7u,cache.Best); //freshfilteredcollection is distinct fromepsilon-suppressedcache.
    }

    [Fact]
    public void FactoryWithOnlyManualManipulationDoesNotInventNativeRobotInput()
    {
        using var f=new Fixture();
        var config=f.Rig.Engine.HostBehaviorMap![BehaviorID.SparksPickupSingleCubeForPyramid];
        var container=new BehaviorContainer(new BehaviorFactory(),new BehaviorFactoryContext{Manipulation=f.M,Vision=f.M.Vision},new Dictionary<BehaviorID,System.Text.Json.JsonElement>{{BehaviorID.SparksPickupSingleCubeForPyramid,config}});
        Assert.Empty(container.Behaviors);
        Assert.IsType<NativePickUpCubeBehavior>(f.Runtime.Bound["SparksPickupSingleCubeForPyramid"]); //actualowner positive remainsdistinct.
    }

}
