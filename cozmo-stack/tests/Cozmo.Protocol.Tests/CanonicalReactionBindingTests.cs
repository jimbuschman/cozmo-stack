using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class CanonicalReactionBindingTests
{
    private static string Resources()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {
            var root=Path.Combine(d.FullName,"re-analysis","obb","assets","cozmo_resources");
            if(Directory.Exists(Path.Combine(root,"config","engine","behaviorSystem"))) return root;
        }
        throw new DirectoryNotFoundException("shipped behavior resources");
    }
    private sealed class ImmediateAction : ActionRunner
    {
        internal ImmediateAction():base(8,0) { }
        protected override string ActionName=>"reaction gate fixture";
        public override bool CanInterrupt()=>false;
        public override uint CheckIfDone()=>0;
    }
    private static void OpenGate(EngineRobot native,BehaviorManager manager)
    {
        native.ActionList.Clear();
        Assert.Equal(0,native.ActionList.QueueAction(QueueActionPosition.Now,new ImmediateAction(),0));
        manager.CheckReactions(native.Engine.Timer.Seconds);
        native.ActionList.Clear();
    }
    private static void SetCalibrated(CozmoRobot robot,string property,bool value)
        =>typeof(RobotStateTracker).GetProperty(property)!.SetValue(robot.State,value);
    private static void LoadAnimations(M4ControlTests.Rig rig)
    {
        rig.Robot.Animations.LoadFrom(Path.Combine(Resources(),"assets"));
        Assert.NotNull(rig.Robot.Animations.Library);
    }

    // 0x005A1842..1898: the actual map retains per-trigger JSON order and registrations.
    // 0x00610E50..6C: the whiteboard ID is consumed and handed to this very Ram instance.
    [Fact]
    public void CanonicalMapUsesShippedOrderAndSameRamRecipientAcrossRebindingAndObserver()
    {
        using var rig=new M4ControlTests.Rig(Resources()); rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var manager=native.BehaviorManagerOwner!;var runtime=native.NativeBehaviorRuntime!;
        var before=manager.Reactions.ToArray();
        var actual=before.Select(r=>(r.Strategy.Trigger,r.Behavior.Id)).ToArray();
        var supported=actual.ToHashSet();
        var expected=ReactionTriggerMap.Load(Resources()).Where(r=>supported.Contains((r.Trigger,r.BehaviorId)))
            .OrderBy(r=>r.Trigger).Select(r=>(r.Trigger,r.BehaviorId)).ToArray();
        Assert.Equal(expected,actual);
        Assert.Equal(new[]{"ReactToFrustrationMinor","ReactToFrustrationMajor"},before.Where(r=>r.Strategy.Trigger==ReactionTrigger.Frustration).Select(r=>r.Behavior.Id));
        Assert.Single(before.Where(r=>r.Strategy.Trigger==ReactionTrigger.MotorCalibration));
        Assert.Single(before.Where(r=>r.Strategy.Trigger==ReactionTrigger.CliffDetected));
        var reg=Assert.Single(before.Where(r=>r.Strategy.Trigger==ReactionTrigger.NoPreDockPoses));
        var ram=Assert.IsType<RamIntoBlockBehavior>(reg.Behavior);
        runtime.Manipulation!.Whiteboard.NoPreDockPosesObjectId=123;
        reg.Strategy.ShouldTriggerBehavior(new ReactionContext(runtime.Context,rig.Engine.Timer.Seconds),ram);
        Assert.Null(runtime.Manipulation.Whiteboard.NoPreDockPosesObjectId);Assert.Equal(123u,ram.PendingTarget);
        manager.BindReactionDependencies(runtime.Manipulation,runtime.Bound);
        using(var observer=FreeplayStack.CreateNative(rig.Robot))
        { Assert.Same(manager,observer.Manager);Assert.Same(runtime.Context,observer.Context); }
        Assert.Equal(before,manager.Reactions);
        Assert.Same(runtime,native.NativeBehaviorRuntime);
        Assert.Same(reg,manager.Reactions.Single(r=>r.Strategy.Trigger==ReactionTrigger.NoPreDockPoses));
    }

    // 0x0060654E..66E and 0x00606742..98: the real ingress selects a five-second
    // wait; the two calibrated bytes are sampled only on the calibration message.
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AutomaticCalibrationStartsCanonicalWaitAndUsesBothFlagsOnTag30(bool observer)
    {
        using var rig=new M4ControlTests.Rig(Resources());LoadAnimations(rig);rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var manager=native.BehaviorManagerOwner!;
        Assert.Same(rig.Robot.Animations.DrivingTriggerMap,native.NativeBehaviorRuntime!.Context.Triggers);
        var behavior=Assert.IsType<ReactToMotorCalibrationBehavior>(manager.Reactions.Single(r=>r.Strategy.Trigger==ReactionTrigger.MotorCalibration).Behavior);
        var strategy=Assert.IsType<GenericReactionStrategy>(manager.Reactions.Single(r=>r.Strategy.Trigger==ReactionTrigger.MotorCalibration).Strategy);
        var diagnostics=new List<string>();
        string Snapshot(string label)=>$"{label}: current={manager.Current?.Id ?? "null"}, trigger={manager.CurrentReactionTrigger}, latched={strategy.Latched}, enabled={manager.IsReactionTriggerEnabled(ReactionTrigger.MotorCalibration)}, initialized={manager.Initialized}, library={rig.Robot.Animations.Library is not null}, initFailed={behavior.InitFailed}, running={behavior.EngineRunning}, tag={behavior.CurrentRunnerTag}, owned={behavior.OwnedRunner?.GetType().Name ?? "null"}, listEmpty={native.ActionList.IsEmpty}, trace=[{string.Join(" | ",behavior.Trace)}]";
        manager.Log+=line=>diagnostics.Add("manager: "+line);
        manager.ReactionTriggered+=sw=>diagnostics.Add(Snapshot("triggered "+sw.Trigger));
        OpenGate(native,manager);
        diagnostics.Add(Snapshot("before ingress"));
        using var handle=observer?FreeplayStack.CreateNative(rig.Robot):null;
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=true,AutoStarted=true});
        rig.Tick(0);
        diagnostics.Add(Snapshot("after tick"));
        manager.CheckReactions(rig.Engine.Timer.Seconds);
        diagnostics.Add(Snapshot("after check"));
        Assert.True(ReferenceEquals(behavior,manager.Current),string.Join(Environment.NewLine,diagnostics));
        var wait=Assert.IsType<NativeTimedWaitRunner>(behavior.OwnedRunner);Assert.Equal(5f,wait.Duration);
        handle?.Dispose();
        SetCalibrated(rig.Robot,nameof(RobotStateTracker.HeadCalibrated),true);
        SetCalibrated(rig.Robot,nameof(RobotStateTracker.LiftCalibrated),true);
        Assert.Same(wait,behavior.OwnedRunner);
        SetCalibrated(rig.Robot,nameof(RobotStateTracker.LiftCalibrated),false);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=false});
        rig.Tick(0);
        Assert.Same(wait,behavior.OwnedRunner);
        SetCalibrated(rig.Robot,nameof(RobotStateTracker.HeadCalibrating),true);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_LIFT,CalibStarted=false});
        rig.Tick(0);
        Assert.True(rig.Robot.State.HeadCalibrating);
        Assert.True(behavior.Completed);Assert.Null(behavior.OwnedRunner);
    }

    [Fact]
    public void AutomaticCalibrationWaitExpiresAtFiveSeconds()
    {
        using var rig=new M4ControlTests.Rig(Resources());LoadAnimations(rig);rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var manager=native.BehaviorManagerOwner!;OpenGate(native,manager);
        Assert.Same(rig.Robot.Animations.DrivingTriggerMap,native.NativeBehaviorRuntime!.Context.Triggers);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=true,AutoStarted=true});
        rig.Tick(0);
        manager.CheckReactions(rig.Engine.Timer.Seconds);
        var behavior=Assert.IsType<ReactToMotorCalibrationBehavior>(manager.Current);
        var wait=Assert.IsType<NativeTimedWaitRunner>(behavior.OwnedRunner);
        rig.Tick(1);rig.Tick(4999);Assert.Same(wait,behavior.OwnedRunner);
        rig.Tick(2);Assert.Null(behavior.OwnedRunner);Assert.False(behavior.Completed);
    }

    [Fact]
    public void FreeplayAdmissionRetainsRunningReaction()
    {
        using var rig=new M4ControlTests.Rig(Resources());LoadAnimations(rig);rig.ToSynced();rig.Calibrate();
        rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger);
        var native=rig.Engine.Robot!;var manager=native.BehaviorManagerOwner!;OpenGate(native,manager);
        rig.Robot.ActivateHighLevelActivity("PutDownDispatch");rig.Tick(10);
        var previous=Assert.IsType<DriveOffChargerBehavior>(manager.Current);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=true,AutoStarted=true});
        rig.Tick(0);manager.CheckReactions(rig.Engine.Timer.Seconds);
        var reaction=Assert.IsType<ReactToMotorCalibrationBehavior>(manager.Current);
        Assert.Same(previous,manager.ParkedBehavior);
        rig.Robot.ActivateHighLevelActivity("Freeplay");rig.Tick(0);
        Assert.Same(reaction,manager.Current);
        Assert.Equal(ReactionTrigger.MotorCalibration,manager.CurrentReactionTrigger);
        Assert.Null(manager.ParkedBehavior);
        Assert.Same(native.NativeBehaviorRuntime!.FreeplayObserver,manager.Activity);
    }
    // 0x0060532A..53A0: the retained canonical behavior queues the real drive, or
    // broadcasts finished before the objective when the event byte is clear.
    [Theory] [InlineData(false)] [InlineData(true)]
    public void RetainedCliffUsesActualDriveOwnerOrReportsFinish(bool cliff)
    {
        using var rig=new M4ControlTests.Rig(Resources());LoadAnimations(rig);rig.ToSynced();rig.State();rig.Calibrate();rig.Tick(0);
        var native=rig.Engine.Robot!;var runtime=native.NativeBehaviorRuntime!;
        Assert.Same(rig.Robot.Animations.DrivingTriggerMap,runtime.Context.Triggers);
        var manager=native.BehaviorManagerOwner!;
        var behavior=Assert.IsType<ReactToCliffBehavior>(manager.Reactions.Single(r=>r.Strategy.Trigger==ReactionTrigger.CliffDetected).Behavior);
        OpenGate(native,manager);
        var order=new List<string>();
        behavior.RobotCliffEventFinishedInternal+=()=>order.Add("finished");
        using var objective=rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint id && id==0x1C)order.Add("objective");});
        rig.Data(new CliffEvent{Timestamp=123,DetectedFlags=1});
        rig.Tick(0);
        manager.CheckReactions(rig.Engine.Timer.Seconds);
        Assert.Same(behavior,manager.Current);
        if(!cliff) { rig.Data(new CliffEvent{Timestamp=124,DetectedFlags=0});rig.Tick(0); }
        // Supplied interruption boundary: stop the real tracked animation and allow its
        // ticket callback to run. This does not claim normal firmware animation completion.
        Assert.True(rig.Robot.Animations.Stop());
        BehaviorTestSignals.WaitForPostedWork(behavior);
        manager.Update(rig.Engine.Timer.Seconds*1000,rig.Engine.Timer.Seconds);
        if(cliff)
        {
            Assert.True(SpinWait.SpinUntil(()=>native.ActionList.GetCurrentAction() is DriveStraightAction,TimeSpan.FromSeconds(5)));
            var drive=Assert.IsType<DriveStraightAction>(native.ActionList.GetCurrentAction());
            Assert.Equal(-60d,drive.DistanceMm);Assert.Equal(100f,drive.SpeedMmps);Assert.True(drive.ShouldPlayAnimation);
            Assert.Empty(order);
        }
        else Assert.Equal(new[]{"finished","objective"},order);
        behavior.Stop(BehaviorStopReason.Cancelled);
    }

    // 0x006113F2..13FC / 0x006114D8..151A. Source-derived observation inputs
    // are delivered through the world's real event to its retained strategy.
    [Fact]
    public void CanonicalObjectStrategyReadsLiveCarryAndRetainedDockTarget()
    {
        using var rig=new M4ControlTests.Rig(Resources());rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var manager=native.BehaviorManagerOwner!;var runtime=native.NativeBehaviorRuntime!;
        var m=runtime.Manipulation!;var world=rig.Robot.Vision.World;
        var strategy=Assert.IsType<ObjectPositionUpdatedStrategy>(manager.Reactions.Single(r=>r.Strategy.Trigger==ReactionTrigger.ObjectPositionUpdated).Strategy);
        strategy.LastImageTimestamp=()=>100;
        var previous=M10Support.RunnableBehaviour("object-binding-previous");manager.Add(previous);
        Assert.True(manager.StartAsync(previous.Id,0).GetAwaiter().GetResult());
        var publish=(Action<Cozmo.Robot.Vision.ObjectObservation>)typeof(Cozmo.Robot.Vision.BlockWorld)
            .GetField("ObjectObserved",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(world)!;
        var pose=new Cozmo.Robot.Vision.Pose3d(Cozmo.Robot.Vision.Mat3.Identity,new Cozmo.Robot.Vision.Vec3(200,0,22));
        void Observe(uint id)
        {
            var obj=new Cozmo.Robot.Vision.ObservableObject(id,ObjectType.Block_LIGHTCUBE1,Cozmo.Robot.Vision.CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1)){Pose=pose};
            publish(new Cozmo.Robot.Vision.ObjectObservation(obj,100,Array.Empty<Cozmo.Robot.Vision.MarkerType>(),pose,Cozmo.Robot.Vision.PoseState.Known,false,0));
        }
        m.Docking.Carrying.SetCarrying(7);Observe(7);
        Assert.False(strategy.ShouldReactToTarget(7));
        Assert.False(strategy.ShouldTriggerBehavior(new ReactionContext(runtime.Context,0),manager.Reactions.Single(r=>r.Strategy==strategy).Behavior));
        m.Docking.Carrying.ClearNativeCarryingFields(false);Observe(7);Assert.False(strategy.ShouldReactToTarget(7));
        native.DockingOwner!.TargetId=8;Observe(8);native.DockingOwner.TargetId=null;
        Observe(8);Assert.False(strategy.ShouldReactToTarget(8));
        Observe(9);Assert.True(strategy.ShouldReactToTarget(9));
        Assert.True(strategy.ShouldTriggerBehavior(new ReactionContext(runtime.Context,0),manager.Reactions.Single(r=>r.Strategy==strategy).Behavior));
    }

    [Fact]
    public void RebindingChangesLiveWhiteboardWithoutReplacingRegistrationsAndRetirementStaysRetired()
    {
        using var rig=new M4ControlTests.Rig(Resources());LoadAnimations(rig);rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var runtime=native.NativeBehaviorRuntime!;var manager=native.BehaviorManagerOwner!;
        Assert.Same(rig.Robot.Animations.DrivingTriggerMap,runtime.Context.Triggers);
        var before=manager.Reactions.ToArray();
        var reg=before.Single(r=>r.Strategy.Trigger==ReactionTrigger.NoPreDockPoses);
        using var other=ManipulationSystem.CreateStandalone(rig.Robot,rig.Robot.Vision,()=>rig.Engine.Timer.Seconds);
        manager.BindReactionDependencies(other,runtime.Bound);
        other.Whiteboard.NoPreDockPosesObjectId=456;
        reg.Strategy.ShouldTriggerBehavior(new ReactionContext(runtime.Context,0),reg.Behavior);
        Assert.Null(other.Whiteboard.NoPreDockPosesObjectId);
        Assert.Equal(456u,Assert.IsType<RamIntoBlockBehavior>(reg.Behavior).PendingTarget);
        manager.BindReactionDependencies(runtime.Manipulation,runtime.Bound);
        Assert.Equal(before,manager.Reactions);
        OpenGate(native,manager);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=true,AutoStarted=true});
        rig.Tick(0);
        manager.CheckReactions(rig.Engine.Timer.Seconds);
        var motor=Assert.IsType<ReactToMotorCalibrationBehavior>(manager.Current);
        Assert.IsType<NativeTimedWaitRunner>(motor.OwnedRunner);
        manager.Dispose();
        Assert.False(motor.EngineRunning);Assert.Null(motor.OwnedRunner);
        manager.BindReactionDependencies(runtime.Manipulation,runtime.Bound);
        rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_LIFT,CalibStarted=false});
        rig.Tick(0);
        Assert.Empty(manager.Reactions);Assert.Null(manager.CheckReactions(rig.Engine.Timer.Seconds));
    }

    [Fact]
    public void StandaloneFreeplayHasItsOwnBoundCalibrationAndDoesNotAlterCanonicalMap()
    {
        using var rig=new M4ControlTests.Rig(Resources());rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var runtime=native.NativeBehaviorRuntime!;var manager=native.BehaviorManagerOwner!;
        var before=manager.Reactions.ToArray();
        using var m=ManipulationSystem.CreateStandalone(rig.Robot,rig.Robot.Vision,()=>rig.Engine.Timer.Seconds);
        var context=new BehaviorContext{Robot=rig.Robot,Triggers=new AnimationTriggerMap()};
        string obbRoot=Directory.GetParent(Directory.GetParent(Resources())!.FullName)!.FullName;
        using var freeplay=FreeplayStack.CreateStandalone(obbRoot,rig.Robot,context,()=>rig.Engine.Timer.Seconds,rig.Robot.Vision,m);
        Assert.NotSame(manager,freeplay.Manager);Assert.Equal(before,manager.Reactions);
        var motor=Assert.IsType<ReactToMotorCalibrationBehavior>(freeplay.Manager.Reactions.Single(r=>r.Strategy.Trigger==ReactionTrigger.MotorCalibration).Behavior);
        motor.StartAsync(context,new BehaviorScope(),default).GetAwaiter().GetResult();
        Assert.Equal(5f,Assert.IsType<NativeTimedWaitRunner>(motor.OwnedRunner).Duration);
        motor.Stop(BehaviorStopReason.Cancelled);
        Assert.Same(runtime,native.NativeBehaviorRuntime);Assert.Equal(before,manager.Reactions);
    }

    // Host callback safety: ReportMissing invokes listeners synchronously. These tests
    // use the actual loaded map and real owners; no native destructor claim is made.
    [Fact]
    public void RamFallbackDiagnosticCanRetireManagerWithoutResurrectingIt()
    {
        using var rig=new M4ControlTests.Rig(Resources());rig.ToSynced();rig.State();
        var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;
        var context=new BehaviorContext{Robot=rig.Robot,Triggers=new AnimationTriggerMap()};
        using var manager=new BehaviorManager(context,null,false);
        manager.InitializeRobotReactions(Resources(),rig.Robot.Vision);
        int calls=0;
        void OnMissing(string message)
        {
            if(!message.Contains("RamIntoBlock configured factory owner unavailable"))return;
            calls++;manager.Dispose();
        }
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=OnMissing;
        try
        {
            manager.BindReactionDependencies(runtime.Manipulation,new Dictionary<string,IBehavior>());
            Assert.Equal(1,calls);Assert.Empty(manager.Reactions);
            manager.BindReactionDependencies(runtime.Manipulation,new Dictionary<string,IBehavior>());
            Assert.Empty(manager.Reactions);
        }
        finally { SteppedBehavior.MissingReported-=OnMissing;SteppedBehavior.ResetMissingForTests(); }
    }

    [Fact]
    public void RamFallbackDiagnosticNestedBindingWinsWithoutDuplicateOrStaleOwner()
    {
        using var rig=new M4ControlTests.Rig(Resources());rig.ToSynced();rig.State();
        var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;
        using var other=ManipulationSystem.CreateStandalone(rig.Robot,rig.Robot.Vision,()=>rig.Engine.Timer.Seconds);
        var context=new BehaviorContext{Robot=rig.Robot,Triggers=new AnimationTriggerMap()};
        using var manager=new BehaviorManager(context,null,false);
        manager.InitializeRobotReactions(Resources(),rig.Robot.Vision);
        var before=manager.Reactions.ToArray();int calls=0;
        void OnMissing(string message)
        {
            if(!message.Contains("RamIntoBlock configured factory owner unavailable"))return;
            calls++;manager.BindReactionDependencies(other,new Dictionary<string,IBehavior>());
        }
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=OnMissing;
        try
        {
            manager.BindReactionDependencies(runtime.Manipulation,new Dictionary<string,IBehavior>());
            Assert.Equal(1,calls);
            var reg=Assert.Single(manager.Reactions.Where(r=>r.Strategy.Trigger==ReactionTrigger.NoPreDockPoses));
            Assert.All(before,r=>Assert.Contains(r,manager.Reactions));
            other.Whiteboard.NoPreDockPosesObjectId=789;
            runtime.Manipulation!.Whiteboard.NoPreDockPosesObjectId=790;
            reg.Strategy.ShouldTriggerBehavior(new ReactionContext(context,0),reg.Behavior);
            Assert.Null(other.Whiteboard.NoPreDockPosesObjectId);
            Assert.Equal(790u,runtime.Manipulation.Whiteboard.NoPreDockPosesObjectId);
            Assert.Equal(789u,Assert.IsType<RamIntoBlockBehavior>(reg.Behavior).PendingTarget);
        }
        finally { SteppedBehavior.MissingReported-=OnMissing;SteppedBehavior.ResetMissingForTests(); }
    }

}
