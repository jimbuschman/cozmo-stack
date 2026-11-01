using System.Reflection;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class SparkedLifecycleNativeTests
{
    private sealed class SuppliedChooser : IBehaviorChooser
    {
        internal IBehavior? Result; internal bool Unavailable; internal int Calls;
        public BehaviorChooserType Type=>BehaviorChooserType.StrictPriority;
        public IReadOnlyList<string> BehaviorIds=>Array.Empty<string>();
        public ChooserDecision GetDesiredActiveBehavior(IBehavior? current,double running,BehaviorContext context,double now)
        { Calls++;return new ChooserDecision(Result,"supplied",Array.Empty<(string,double,string)>()){InputUnavailable=Unavailable}; }
    }
    private const string Config="{\"minTimeSecs\":0,\"maxTimeSecs\":30,\"numberOfRepetitions\":1,\"maxTimeoutForActionComplete\":false,\"behaviorObjective\":\"LaserTracked\"}";
    private static void State(NativeSparkedActivity owner,int value)=>typeof(NativeSparkedActivity).GetProperty("State",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,value);
    [Fact]
    public void DeadlineUsesOrderedFloatComparisonForNan()
    {
        Assert.False(NativeSparkedActivity.Deadline(0,float.NaN,31));
        Assert.False(NativeSparkedActivity.Deadline(float.NaN,0,31));
        Assert.False(NativeSparkedActivity.Deadline(0,0,float.NaN));
    }
    [Fact]
    public void ShippedFourteenConfigsOwnActualSparkedState()
    {
        using var f=new Fixture();
        string[] names={"SparksBuildPyramid","SparksCozmoSings","SparksFireTruckAlarm","SparksFistBump","SparksGatherCubes","SparksKnockOverCubes","SparksPeekABoo","SparksPickUpCube","SparksPopAWheelie","SparksPounceOnMotion","SparksRollBlock","SparksStackBlock","SparksTrackLaser","SparksWorkout"};
        foreach(string name in names)
            Assert.IsType<NativeSparkedActivity>(NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id==name).SparkedOwner);
    }
    [Fact]
    public void RealImageFrameDeliversObservedObjectSynchronously()
    {
        using var f=new Fixture();f.Add(7,150,22);
        var owner=Assert.IsType<NativeSparkedActivity>(NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksKnockOverCubes").SparkedOwner);
        var delivered=new List<uint>();using var subscription=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x44,true,p=>
        {
            if(p is NativeObservedObject item && unchecked(item.Family-1)<=1)
            {delivered.Add(item.ObjectId);Assert.Contains(item.ObjectId,owner.Observed);}
        });
        // M11-007/037: the confirmer requires a second matching observation before publishing an object.
        f.Image(1,cube:true,cubeX:150);f.Motors();f.Image(2,cube:true,cubeX:150);
        Assert.True(delivered.Count!=0,$"result={f.M.Vision.LastResult}; enabled={f.M.Vision.Enabled} modes={f.M.Vision.ModeEnableMask:X} robotTimestamp={f.Robot.StoredState!.Timestamp} head={f.Head} lift={f.Lift} pose={f.Robot.GetCheckedMainPose().Translation} calibration={f.M.Vision.Calibration is not null}");
        Assert.NotEmpty(delivered);Assert.All(delivered,id=>Assert.Contains(id,owner.Observed));
    }
    [Fact]
    public void ConcurrentObservationAndRetirementCannotDeadlockOrMutateRetiredOwner()
    {
        using var f=new Fixture();var owner=Assert.IsType<NativeSparkedActivity>(NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksKnockOverCubes").SparkedOwner);
        using var go=new ManualResetEventSlim(false);
        var delivery=Task.Run(()=>{go.Wait();for(uint i=0;i<1000;i++){f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(1,1,0,i));f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,13u);}});
        var retirement=Task.Run(()=>{go.Wait();owner.Dispose();});go.Set();
        Assert.True(Task.WhenAll(delivery,retirement).Wait(TimeSpan.FromSeconds(10)),"concurrent 44/8B and retirement did not finish");
        uint count=owner.Count;Assert.Empty(owner.Observed);
        f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(1,1,0,9999));f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,13u);
        Assert.Equal(count,owner.Count);Assert.Empty(owner.Observed);
    }
    [Fact]
    public void MissingConfiguredArbitraryOwnerDoesNotCreateSparkedObjectiveProxy()
    {
        using var f=new Fixture();var bound=f.Runtime.Bound.Where(pair=>pair.Key!="PlayArbitraryAnim").ToDictionary(pair=>pair.Key,pair=>pair.Value);
        var tree=ActivityTreeLoader.LoadResources(f.Rig.Engine.Options.ResourcesPath!,bound,f.Robot.BehaviorManagerOwner!.Penalty,f.Robot);
        try
        {
            var spark=NativeBehaviorRuntime.AllActivities(tree).Single(a=>a.Id=="SparksKnockOverCubes");
            Assert.Null(spark.SparkedOwner);Assert.Null(spark.ObjectiveOwner);
        }
        finally {foreach(var activity in NativeBehaviorRuntime.AllActivities(tree)){activity.ObjectiveOwner?.Dispose();activity.GatherOwner?.Dispose();if(activity.Chooser is SelectionChooser selection)selection.Dispose();activity.Strategy.Dispose();}}
    }
    [Fact]
    public void RequestedPeekWritesCapturedConfiguredOwnerDeadline()
    {
        using var f=new Fixture();var activity=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksPeekABoo");
        var owner=Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);
        var peek=f.Robot.NativeConfiguredScoreFields!["SparksPeekABoo"];
        f.Rig.Engine.Timer.UpdateTime(10_000_000_000);f.Robot.BehaviorManagerOwner!.SetNativeRequestedSpark(0x25,1);
        owner.Selected();Assert.False(owner.InputUnavailable);Assert.Equal(38f,peek.SparkPeekDeadline);
    }
    [Fact]
    public void RequestedPeekDoesNotRedirectToReplacementMetadata()
    {
        using var f=new Fixture();var activity=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksPeekABoo");
        var owner=Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);var fields=f.Robot.NativeConfiguredScoreFields!;
        var original=fields["SparksPeekABoo"];var replacement=new NativeScoredFields();fields["SparksPeekABoo"]=replacement;
        try
        {
            f.Robot.BehaviorManagerOwner!.SetNativeRequestedSpark(0x25,1);owner.Selected();
            Assert.True(owner.InputUnavailable);Assert.Null(original.SparkPeekDeadline);Assert.Null(replacement.SparkPeekDeadline);
        }
        finally {fields["SparksPeekABoo"]=original;}
    }
    [Fact]
    public void DestructorRetiresOwnCallbacksWithoutResettingSelectedOutputs()
    {
        using var f=new Fixture();var activity=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksKnockOverCubes");
        var owner=Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);var manager=f.Robot.BehaviorManagerOwner!;
        manager.SparkNameInput=_=>"supplied";manager.SetNativeRequestedSpark(5,0);owner.Selected();
        int driving=f.Rig.Robot.Animations.DrivingAnimations.Entries.Count(e=>e.LockName==activity.Id);
        int objective=f.Rig.Engine.AnimationMessages.SubscriberCountForTest(0x8B,true);int reports=0;
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x84,true,_=>reports++);
        owner.Dispose();
        Assert.Equal(driving,f.Rig.Robot.Animations.DrivingAnimations.Entries.Count(e=>e.LockName==activity.Id));
        Assert.Equal(objective-1,f.Rig.Engine.AnimationMessages.SubscriberCountForTest(0x8B,true));
        Assert.Equal(5,manager.RequestedSparkId);Assert.Equal(0,reports);
    }
    [Fact]
    public void NoExternalInterfaceSkipsHardReportAndOwnSubscriptions()
    {
        using var seed=new Fixture();using var rig=new M4ControlTests.Rig(seed.Rig.Engine.Options.ResourcesPath);
        rig.Engine.BeforeDerivedConstructionForTest=robot=>robot.HasExternalInterface=false;rig.ToSynced();
        var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;
        var owner=Assert.IsType<NativeSparkedActivity>(NativeBehaviorRuntime.AllActivities(runtime.Tree).Single(a=>a.Id=="SparksKnockOverCubes").SparkedOwner);
        int before=rig.Engine.AnimationMessages.SubscriberCountForTest(0x8B,true),reports=0;var missing=new List<string>();
        void Report(string text)=>missing.Add(text);SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Report;
        using var started=rig.Engine.AnimationMessages.SubscribeMessage(0x83,true,_=>reports++);
        try {owner.Selected();Assert.Contains(missing,m=>m.Contains("M1-034/M15-059")&&m.Contains("005B172E"));Assert.Equal(0,reports);owner.Dispose();Assert.Equal(before,rig.Engine.AnimationMessages.SubscriberCountForTest(0x8B,true));}
        finally {SteppedBehavior.MissingReported-=Report;}
    }
    [Fact]
    public void HardDeadlineQueriesChooserThenUsesActualConfiguredWait()
    {
        // Configured unit, supplied chooser results: no TrackLaser physical-body claim.
        using var f=new Fixture();using var config=JsonDocument.Parse(Config);var chooser=new SuppliedChooser();
        var activity=new Activity{Id="SparksTrackLaser",Type="Sparked",Strategy=new ActivityStrategy(),Chooser=chooser};
        using var owner=new NativeSparkedActivity(activity,f.Robot,config.RootElement,f.Runtime.Bound);
        f.Rig.Engine.Timer.UpdateTime(31_000_000_000);State(owner,2);
        Assert.Null(owner.Desired(null,31,f.Runtime.Context));Assert.Equal(1,chooser.Calls);Assert.Equal(2,owner.State);
        var previous=f.Runtime.Bound["PlayArbitraryAnim"];chooser.Result=previous;
        Assert.Same(previous,owner.Desired(previous,31,f.Runtime.Context));Assert.Equal(2,owner.State);
        chooser.Result=null;Assert.Same(f.Runtime.Bound["Wait"],owner.Desired(previous,31,f.Runtime.Context));
        Assert.Equal(3,chooser.Calls);Assert.Equal(3,owner.State);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync("Wait",31).GetAwaiter().GetResult());
        Assert.True(Assert.IsType<NativeWaitBehavior>(f.Robot.BehaviorManagerOwner.Current).EngineRunning);
        State(owner,2);chooser.Unavailable=true;Assert.Null(owner.Desired(previous,31,f.Runtime.Context));Assert.True(owner.InputUnavailable);Assert.Equal(2,owner.State);
    }
    [Fact]
    public void MissingRetainedWaitIsUnavailableOnlyWhenOverrideNeedsIt()
    {
        using var f=new Fixture();using var config=JsonDocument.Parse(Config);var chooser=new SuppliedChooser();
        var activity=new Activity{Id="SparksTrackLaser",Type="Sparked",Strategy=new ActivityStrategy(),Chooser=chooser};
        var bound=f.Runtime.Bound.Where(pair=>pair.Key!="Wait").ToDictionary(pair=>pair.Key,pair=>pair.Value);
        using var owner=new NativeSparkedActivity(activity,f.Robot,config.RootElement,bound);
        f.Rig.Engine.Timer.UpdateTime(31_000_000_000);State(owner,2);Assert.True(owner.CanExecute);
        Assert.Null(owner.Desired(null,31,f.Runtime.Context));Assert.False(owner.InputUnavailable);
        Assert.Null(owner.Desired(f.Runtime.Bound["PlayArbitraryAnim"],31,f.Runtime.Context));Assert.True(owner.InputUnavailable);Assert.Equal(2,owner.State);
    }
    [Fact]
    public void ReloadUsesGenericObjectiveAndPreservesAbsentWaitFlag()
    {
        using var f=new Fixture();using var config=JsonDocument.Parse(Config);var activity=new Activity{Id="SparksTrackLaser",Type="Sparked",Strategy=new ActivityStrategy()};
        using var owner=new NativeSparkedActivity(activity,f.Robot,config.RootElement,f.Runtime.Bound);
        using var changed=JsonDocument.Parse("{\"minTimeSecs\":0,\"maxTimeSecs\":1,\"numberOfRepetitions\":255,\"behaviorObjective\":\"BlockRolled\",\"sparksSuccessTrigger\":\"\"}");
        Assert.True(owner.Reload(changed.RootElement));Assert.False(owner.WaitForAction);Assert.Equal(255,owner.Repetitions);Assert.Equal(0x23F,(int)owner.Success);
        f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,2u);Assert.Equal(1u,owner.Count);
        using var unknown=JsonDocument.Parse("{\"minTimeSecs\":0,\"maxTimeSecs\":1,\"numberOfRepetitions\":0,\"behaviorObjective\":\"blockRolled\"}");
        Assert.True(owner.Reload(unknown.RootElement));f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,0u);Assert.Equal(2u,owner.Count);
    }
    [Fact]
    public void TriggerReloadUsesNativeUnknownAndEmptyRules()
    {
        using var f=new Fixture();using var config=JsonDocument.Parse(Config);
        var activity=new Activity{Id="SparksTrackLaser",Type="Sparked",Strategy=new ActivityStrategy()};
        using var owner=new NativeSparkedActivity(activity,f.Robot,config.RootElement,f.Runtime.Bound);
        using var unknown=JsonDocument.Parse("{\"minTimeSecs\":0,\"maxTimeSecs\":1,\"numberOfRepetitions\":1,\"softSparkTrigger\":\"noSuchTrigger\",\"sparksSuccessTrigger\":\"noSuchTrigger\",\"sparksFailTrigger\":\"noSuchTrigger\"}");
        Assert.True(owner.Reload(unknown.RootElement));Assert.Equal(0,(int)owner.SoftTrigger);Assert.Equal(0,(int)owner.Success);Assert.Equal(0,(int)owner.Failure);
        using var empty=JsonDocument.Parse("{\"minTimeSecs\":0,\"maxTimeSecs\":1,\"numberOfRepetitions\":1,\"softSparkTrigger\":\"\",\"sparksSuccessTrigger\":\"\",\"sparksFailTrigger\":\"\"}");
        Assert.True(owner.Reload(empty.RootElement));Assert.Equal(0,(int)owner.SoftTrigger);Assert.Equal(0x23F,(int)owner.Success);Assert.Equal(0x23F,(int)owner.Failure);
        using var softNull=JsonDocument.Parse("{\"minTimeSecs\":0,\"maxTimeSecs\":1,\"numberOfRepetitions\":1,\"softSparkTrigger\":null}");
        Assert.True(owner.Reload(softNull.RootElement));Assert.Equal(0x23F,(int)owner.SoftTrigger);
    }
    [Fact]
    public void AllFortyTwoNativeObjectiveNamesReachTheirMatchingEvents()
    {
        using var f=new Fixture();using var config=JsonDocument.Parse(Config);
        var activity=new Activity{Id="SparksTrackLaser",Type="Sparked",Strategy=new ActivityStrategy()};
        using var owner=new NativeSparkedActivity(activity,f.Robot,config.RootElement,f.Runtime.Bound);
        // Independent source oracle: native four-byte pointer table 0x01033140.
        string[] names={"Unknown","AnimationPlayed","BlockRolled","BuiltPyramid","DroveAsIntended","EnrolledFaceWithName",
            "FireTruckAlarmPerformed","FistBumpComplete","FistBumpSuccess","FistBumpLeftHanging","FoundFace","GatheredCubes",
            "InteractedWithFace","KnockedOverBlocks","LaserPounced","LaserTracked","PeekABooComplete","PeekABooSuccess",
            "PerformedWorkout","PerformedStrongWorkout","PickedupBlock","PickedUpAndPutDownBlock","PoppedWheelie","PouncedAndCaught",
            "PutDownBlock","ReactedAcknowledgedCubeMoved","ReactedAcknowledgedFace","ReactedAcknowledgedObject","ReactedToCliff",
            "ReactedToFrustration","ReactedToOnCharger","ReactedToPickup","ReactedToPoke","ReactedToRobotOnBack","ReactedToRobotOnFace",
            "ReactedToRobotOnSide","ReactedToRobotShaken","ReactedToUnexpectedMovement","RequestedGame","Singing","StackedBlock","Count"};
        for(uint id=0;id<names.Length;id++)
        {
            string name=names[id];
            using var updated=JsonDocument.Parse(JsonSerializer.Serialize(new {minTimeSecs=0,maxTimeSecs=1,numberOfRepetitions=1,behaviorObjective=name}));
            Assert.True(owner.Reload(updated.RootElement));f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,id);
            Assert.Equal(id+1,owner.Count);
        }
    }
    [Fact]
    public void UnsupportedDelegateReloadStaysUnavailableAcrossAbsentReloadAndSelection()
    {
        using var f=new Fixture();var activity=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksGatherCubes");
        var owner=Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);var old=owner.Delegate;Assert.NotNull(old);Assert.True(owner.CanExecute);
        using var unknown=JsonDocument.Parse("{\"minTimeSecs\":0,\"maxTimeSecs\":1,\"numberOfRepetitions\":1,\"subActivityDelegate\":{\"activityType\":\"BuildPyramid\"}}");
        Assert.False(owner.Reload(unknown.RootElement));Assert.False(owner.CanExecute);Assert.Same(old,owner.Delegate);
        using var absent=JsonDocument.Parse("{\"minTimeSecs\":0,\"maxTimeSecs\":2,\"numberOfRepetitions\":1}");
        Assert.True(owner.Reload(absent.RootElement));Assert.False(owner.CanExecute);
        owner.Selected();Assert.True(owner.InputUnavailable);Assert.False(owner.CanExecute);Assert.Null(owner.Desired(null,0,f.Runtime.Context));
        owner.Update();Assert.True(owner.InputUnavailable);var ended=old!.Strategy.LastEndedSec;
        owner.Deselected();Assert.True(owner.InputUnavailable);Assert.Equal(ended,old.Strategy.LastEndedSec);
    }
    [Fact]
    public void SupportedGatherReloadReplacesRetainedDelegateWhileAbsentKeepsIt()
    {
        using var f=new Fixture();var activity=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksGatherCubes");
        var owner=Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);var previous=owner.Delegate;Assert.NotNull(previous);
        using var absent=JsonDocument.Parse("{\"minTimeSecs\":0,\"maxTimeSecs\":2,\"numberOfRepetitions\":1}");
        Assert.True(owner.Reload(absent.RootElement));Assert.Same(previous,owner.Delegate);
        bool sawPublished=false;previous!.GatherOwner!.BeforeDisposeForTest=()=>{sawPublished=true;Assert.NotSame(previous,owner.Delegate);};
        using var replacement=JsonDocument.Parse("{\"minTimeSecs\":0,\"maxTimeSecs\":3,\"numberOfRepetitions\":1,\"subActivityDelegate\":{\"activityID\":\"GatherCubes\",\"activityType\":\"GatherCubes\",\"activityStrategy\":{\"type\":\"Simple\"},\"behaviorChooser\":{\"type\":\"StrictPriority\",\"behaviors\":[]}}}");
        Assert.True(owner.Reload(replacement.RootElement));Assert.True(sawPublished);Assert.NotSame(previous,owner.Delegate);Assert.NotNull(owner.Delegate!.GatherOwner);Assert.True(owner.CanExecute);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReentrantSourceLogRetirementCannotCompleteOldSpark(bool observed)
    {
        using var f=new Fixture();var activity=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksKnockOverCubes");
        var owner=Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);var manager=f.Robot.BehaviorManagerOwner!;
        manager.SparkNameInput=_=>"supplied";manager.SetNativeRequestedSpark(5,0);manager.SwitchToNativeRequestedSpark();State(owner,4);
        if(observed)f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(1,1,0,7));
        string stopAt=observed?"meta.upgrade_replay_fail_cubes_observed":"meta.upgrade_replay_timeout";int events=0,ended=0;
        void Observe(EngineLogEntry entry){if(entry.Name==stopAt){events++;owner.Dispose();}}
        f.Rig.Engine.LogEntry+=Observe;using var subscription=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x84,true,_=>ended++);
        try {Assert.Null(owner.Desired(null,0,f.Runtime.Context));Assert.Equal(1,events);Assert.Equal(5,manager.RequestedSparkId);Assert.Equal(0,ended);}
        finally {f.Rig.Engine.LogEntry-=Observe;}
    }
    [Fact]
    public void RequestedSparkSetterReentryStopsOldEndedReport()
    {
        using var f=new Fixture();var activity=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksKnockOverCubes");
        var owner=Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);var manager=f.Robot.BehaviorManagerOwner!;
        manager.SparkNameInput=_=>"supplied";manager.SetNativeRequestedSpark(5,0);manager.SwitchToNativeRequestedSpark();
        f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,13u);State(owner,4);int callbacks=0,ended=0;
        void Observe(EngineLogEntry entry){if(entry.Name=="BehaviorManager.SetRequestedSpark"){callbacks++;owner.Dispose();}}
        f.Rig.Engine.LogEntry+=Observe;using var subscription=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x84,true,_=>ended++);
        try {Assert.Null(owner.Desired(null,0,f.Runtime.Context));Assert.Equal(1,callbacks);Assert.Equal(0x55,manager.RequestedSparkId);Assert.Equal(0,ended);}
        finally {f.Rig.Engine.LogEntry-=Observe;}
    }
    [Fact]
    public void CompletionReloadsActiveSparkAfterReentrantRequestSetter()
    {
        using var f=new Fixture();var activity=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksKnockOverCubes");
        var owner=Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);var manager=f.Robot.BehaviorManagerOwner!;
        manager.SparkNameInput=_=>"supplied";manager.SetNativeRequestedSpark(5,0);manager.SwitchToNativeRequestedSpark();
        f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,13u);State(owner,4);bool changed=false;int ended=0;
        void Observe(EngineLogEntry entry)
        {
            if(entry.Name!="BehaviorManager.SetRequestedSpark"||changed)return;
            changed=true;manager.SetNativeRequestedSpark(6,1);manager.SwitchToNativeRequestedSpark();
        }
        f.Rig.Engine.LogEntry+=Observe;using var subscription=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x84,true,_=>ended++);
        try {Assert.Null(owner.Desired(null,0,f.Runtime.Context));Assert.True(changed);Assert.Equal(6,manager.ActiveSparkId);Assert.Equal(6,manager.RequestedSparkId);Assert.Equal(0,ended);}
        finally {f.Rig.Engine.LogEntry-=Observe;}
    }
    [Fact]
    public void NonGatherSparkKnownFalseSecondGateStillRunsNativeInit()
    {
        // Supplied admission and off-treads common gate; the manager and configured owner are actual.
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;manager.SparkNameInput=_=>"supplied";
        manager.SetNativeRequestedSpark(5,1);f.Runtime.AdmitActivity("SparksKnockOverCubes");manager.SwitchToNativeRequestedSpark();
        var animation=Assert.IsType<NativePlayArbitraryAnimBehavior>(f.Runtime.Bound["PlayArbitraryAnim"]);
        animation.RobotState355=()=>true;
        try {manager.Update(0,0);Assert.Same(animation,manager.Current);Assert.True(animation.EngineRunning);}
        finally {animation.RobotState355=null;}
    }
    [Fact]
    public void NonGatherSecondGateUnavailableStopsOldButNeverInitializesNew()
    {
        // Actual configured manager and supplied missing carrying owner at the second gate.
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;manager.SparkNameInput=_=>"supplied";
        manager.SetNativeRequestedSpark(5,1);f.Runtime.AdmitActivity("SparksKnockOverCubes");manager.SwitchToNativeRequestedSpark();
        var prior=Assert.IsType<NativeWaitBehavior>(f.Runtime.Bound["Wait"]);Assert.True(manager.StartAsync("Wait",0).GetAwaiter().GetResult());
        var animation=Assert.IsType<NativePlayArbitraryAnimBehavior>(f.Runtime.Bound["PlayArbitraryAnim"]);var carrying=f.Robot.CarryingOwner;
        int unavailable=0,ended=0;var missing=new List<string>();void Observe(string text)=>missing.Add(text);
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Observe;SteppedBehavior.ReadinessUnavailableObservedForTest=()=>unavailable++;
        using var end=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x84,true,_=>ended++);
        try
        {
            f.Robot.CarryingOwner=null;manager.Update(0,0);
            Assert.Equal(1,unavailable);Assert.Contains(missing,text=>text.Contains("IBehavior native base admission"));
            Assert.False(prior.EngineRunning);Assert.Null(manager.Current);Assert.False(animation.EngineRunning);Assert.Null(animation.OwnedRunner);Assert.Equal(0,ended);
        }
        finally {f.Robot.CarryingOwner=carrying;SteppedBehavior.ReadinessUnavailableObservedForTest=null;SteppedBehavior.MissingReported-=Observe;}
    }
    [Fact]
    public void NonGatherChooserUnavailableDoesNotSwitchPriorOwner()
    {
        // Actual TrackLaser configuration; its unbuilt physical candidate is unavailable, not native false.
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;
        var activity=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksTrackLaser");
        var owner=Assert.IsType<NativeSparkedActivity>(activity.SparkedOwner);var prior=Assert.IsType<NativeWaitBehavior>(f.Runtime.Bound["Wait"]);
        Assert.True(manager.StartAsync("Wait",0).GetAwaiter().GetResult());State(owner,2);
        Assert.Null(owner.Desired(prior,0,f.Runtime.Context));Assert.True(owner.InputUnavailable);Assert.Same(prior,manager.Current);Assert.True(prior.EngineRunning);
    }
}
